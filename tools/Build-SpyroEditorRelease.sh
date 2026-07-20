#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
DIST_DIR="$ROOT_DIR/dist/release"
APP_PROJECT="$ROOT_DIR/src/Spyro.Editor.App/Spyro.Editor.App.csproj"
MAC_ENTITLEMENTS="$ROOT_DIR/tools/SpyroEditor.macOS.entitlements"
MAC_BUILD_MODE="${SPYRO_EDITOR_MAC_BUILD_MODE:-}"
MAC_SIGN_IDENTITY="${SPYRO_EDITOR_MAC_SIGN_IDENTITY:-}"
NOTARY_KEYCHAIN_PROFILE="${SPYRO_EDITOR_NOTARY_KEYCHAIN_PROFILE:-}"
NOTARIZATION_TEMP_DIR=""
PROJECT_VERSION="$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' "$APP_PROJECT" | head -n 1)"
BETA_RELEASE_NUMBER="$(sed -n 's:.*<BetaReleaseNumber>\([^<]*\)</BetaReleaseNumber>.*:\1:p' "$APP_PROJECT" | head -n 1)"
PUBLIC_RELEASE_VERSION="$(sed -n 's:.*<PublicReleaseVersion>\([^<]*\)</PublicReleaseVersion>.*:\1:p' "$APP_PROJECT" | head -n 1)"
RELEASE_MANIFEST_SCHEMA_VERSION="$(sed -n 's:.*<ReleaseManifestSchemaVersion>\([^<]*\)</ReleaseManifestSchemaVersion>.*:\1:p' "$APP_PROJECT" | head -n 1)"
PREVIOUS_PUBLIC_RELEASE_VERSION="$(sed -n 's:.*<PreviousPublicReleaseVersion>\([^<]*\)</PreviousPublicReleaseVersion>.*:\1:p' "$APP_PROJECT" | head -n 1)"
[[ "$PROJECT_VERSION" =~ ^([0-9]+\.[0-9]+\.[0-9]+)-beta\.([0-9]+)$ ]] || {
    echo "Unsupported app version '$PROJECT_VERSION'; expected X.Y.Z-beta.N." >&2
    exit 1
}
BASE_VERSION="${BASH_REMATCH[1]}"
BUILD_NUMBER="${BASH_REMATCH[2]}"
[[ "$BETA_RELEASE_NUMBER" =~ ^[1-9][0-9]*$ ]] || {
    echo "Unsupported legacy public beta release '$BETA_RELEASE_NUMBER'; expected a positive integer." >&2
    exit 1
}
[[ "$PUBLIC_RELEASE_VERSION" =~ ^[1-9][0-9]*(\.[1-9][0-9]*)?$ ]] || {
    echo "Unsupported public release version '$PUBLIC_RELEASE_VERSION'; expected a canonical value such as 3 or 3.1." >&2
    exit 1
}
[[ "$PREVIOUS_PUBLIC_RELEASE_VERSION" =~ ^[1-9][0-9]*(\.[1-9][0-9]*)?$ ]] || {
    echo "Unsupported previous public release version '$PREVIOUS_PUBLIC_RELEASE_VERSION'." >&2
    exit 1
}
python3 - "$PREVIOUS_PUBLIC_RELEASE_VERSION" "$PUBLIC_RELEASE_VERSION" <<'PY' || {
import sys

def release_tuple(value):
    major, separator, minor = value.partition(".")
    return int(major), int(minor) if separator else 0

previous = release_tuple(sys.argv[1])
current = release_tuple(sys.argv[2])
raise SystemExit(0 if previous < current else 1)
PY
    echo "Previous public release '$PREVIOUS_PUBLIC_RELEASE_VERSION' must be older than '$PUBLIC_RELEASE_VERSION'." >&2
    exit 1
}
[[ "$RELEASE_MANIFEST_SCHEMA_VERSION" == "1" || "$RELEASE_MANIFEST_SCHEMA_VERSION" == "2" ]] || {
    echo "Unsupported release manifest schema '$RELEASE_MANIFEST_SCHEMA_VERSION'; expected 1 or 2." >&2
    exit 1
}
PUBLIC_RELEASE_MAJOR="${PUBLIC_RELEASE_VERSION%%.*}"
[[ "$PUBLIC_RELEASE_MAJOR" == "$BETA_RELEASE_NUMBER" ]] || {
    echo "Public release '$PUBLIC_RELEASE_VERSION' must retain legacy beta integer '$BETA_RELEASE_NUMBER' as its major component." >&2
    exit 1
}
if [[ "$RELEASE_MANIFEST_SCHEMA_VERSION" == "1" ]]; then
    [[ "$PUBLIC_RELEASE_VERSION" == "$BETA_RELEASE_NUMBER" ]] || {
        echo "Schema 1 is reserved for whole-number compatibility bridges and requires PublicReleaseVersion=$BETA_RELEASE_NUMBER." >&2
        exit 1
    }
else
    [[ "$PUBLIC_RELEASE_VERSION" == *.* ]] || {
        echo "Schema 2 is reserved for canonical dotted releases such as V3.1." >&2
        exit 1
    }
fi
PUBLIC_RELEASE_NAME="Spyro Editor Beta V$PUBLIC_RELEASE_VERSION"
PUBLIC_RELEASE_SLUG="SpyroEditor-Beta-V$PUBLIC_RELEASE_VERSION"
PREVIOUS_PUBLIC_RELEASE_NAME="Spyro Editor Beta V$PREVIOUS_PUBLIC_RELEASE_VERSION"
if [[ "$PUBLIC_RELEASE_VERSION" == *.* ]]; then
    MAC_BUNDLE_SHORT_VERSION="$PUBLIC_RELEASE_VERSION"
else
    MAC_BUNDLE_SHORT_VERSION="$PUBLIC_RELEASE_VERSION.0"
fi
RELEASE_NAME="${1:-$PUBLIC_RELEASE_SLUG}"

mkdir -p "$DIST_DIR"

cleanup_notarization_temp() {
    if [[ -n "$NOTARIZATION_TEMP_DIR" && -d "$NOTARIZATION_TEMP_DIR" ]]; then
        rm -rf "$NOTARIZATION_TEMP_DIR"
    fi
}
trap cleanup_notarization_temp EXIT

is_research_build() {
    [[ "$RELEASE_NAME" == *research* ]]
}

resolve_mac_build_mode() {
    if [[ -n "$MAC_BUILD_MODE" ]]; then
        case "$MAC_BUILD_MODE" in
            production|signed-only|community|local)
                printf '%s\n' "$MAC_BUILD_MODE"
                ;;
            *)
                echo "SPYRO_EDITOR_MAC_BUILD_MODE must be 'production', 'signed-only', 'community', or 'local'." >&2
                return 1
                ;;
        esac
    elif is_research_build; then
        printf 'local\n'
    else
        # Public packages must be Developer ID signed and notarized. A local
        # ad-hoc build is available only through an explicit opt-in.
        printf 'production\n'
    fi
}

resolve_developer_id_identity() {
    local identities
    local identity_count

    command -v security >/dev/null 2>&1 || {
        echo "macOS production signing requires the security command." >&2
        return 1
    }

    if [[ -n "$MAC_SIGN_IDENTITY" ]]; then
        security find-identity -v -p codesigning | grep -Fq "\"$MAC_SIGN_IDENTITY\"" || {
            echo "Developer ID identity '$MAC_SIGN_IDENTITY' is not available in the current keychain." >&2
            return 1
        }
        printf '%s\n' "$MAC_SIGN_IDENTITY"
        return
    fi

    identities="$(security find-identity -v -p codesigning | sed -n 's/^[[:space:]]*[0-9][0-9]*)[[:space:]][0-9A-F]*[[:space:]]"\(Developer ID Application:.*\)"$/\1/p')"
    identity_count="$(printf '%s\n' "$identities" | sed '/^$/d' | wc -l | tr -d ' ')"
    if [[ "$identity_count" != "1" ]]; then
        echo "Expected exactly one Developer ID Application identity, found $identity_count." >&2
        echo "Set SPYRO_EDITOR_MAC_SIGN_IDENTITY to the intended full identity name." >&2
        return 1
    fi

    printf '%s\n' "$identities"
}

for_each_macho_deepest_first() {
    local app_bundle="$1"
    local callback="$2"
    local candidate

    while IFS= read -r -d '' candidate; do
        if file -b "$candidate" | grep -q 'Mach-O'; then
            "$callback" "$candidate"
        fi
    done < <(python3 - "$app_bundle" <<'PY'
import os
import sys

root = os.path.abspath(sys.argv[1])
files = []
for directory, _, names in os.walk(root):
    for name in names:
        path = os.path.join(directory, name)
        files.append(path)
files.sort(key=lambda path: (path.count(os.sep), path), reverse=True)
for path in files:
    sys.stdout.buffer.write(os.fsencode(path) + b"\0")
PY
    )
}

for_each_macos_payload_file_deepest_first() {
    local app_bundle="$1"
    local callback="$2"
    local candidate

    while IFS= read -r -d '' candidate; do
        "$callback" "$candidate"
    done < <(python3 - "$app_bundle/Contents/MacOS" <<'PY'
import os
import sys

root = os.path.abspath(sys.argv[1])
files = []
for directory, _, names in os.walk(root):
    for name in names:
        path = os.path.join(directory, name)
        files.append(path)
files.sort(key=lambda path: (path.count(os.sep), path), reverse=True)
for path in files:
    sys.stdout.buffer.write(os.fsencode(path) + b"\0")
PY
    )
}

for_each_nested_code_bundle_deepest_first() {
    local app_bundle="$1"
    local callback="$2"
    local candidate

    while IFS= read -r -d '' candidate; do
        "$callback" "$candidate"
    done < <(python3 - "$app_bundle" <<'PY'
import os
import sys

root = os.path.abspath(sys.argv[1])
suffixes = (".app", ".appex", ".framework", ".xpc")
bundles = []
for directory, names, _ in os.walk(root):
    for name in names:
        path = os.path.join(directory, name)
        if path != root and name.endswith(suffixes):
            bundles.append(path)
bundles.sort(key=lambda path: (path.count(os.sep), path), reverse=True)
for path in bundles:
    sys.stdout.buffer.write(os.fsencode(path) + b"\0")
PY
    )
}

sign_macos_bundle_local() {
    local app_bundle="$1"
    local main_executable="$app_bundle/Contents/MacOS/Spyro.Editor.App"

    command -v codesign >/dev/null 2>&1 || {
        echo "Local macOS packaging requires codesign." >&2
        return 1
    }

    local sign_local_leaf
    sign_local_leaf() {
        [[ "$1" == "$main_executable" ]] && return
        codesign --force --sign - "$1"
    }
    # A self-contained .NET application keeps assemblies, runtime data, and
    # catalogs beside the executable under Contents/MacOS. codesign classifies
    # that entire subtree as nested payload, so seal every leaf bottom-up.
    for_each_macos_payload_file_deepest_first "$app_bundle" sign_local_leaf
    for_each_nested_code_bundle_deepest_first "$app_bundle" sign_local_leaf
    codesign --force --sign - "$app_bundle"
    codesign --verify --deep --strict --verbose=2 "$app_bundle"
}

sign_macos_bundle_community() {
    local app_bundle="$1"
    local main_executable="$app_bundle/Contents/MacOS/Spyro.Editor.App"

    [[ -f "$MAC_ENTITLEMENTS" ]] || {
        echo "Missing macOS entitlements: $MAC_ENTITLEMENTS" >&2
        return 1
    }
    command -v codesign >/dev/null 2>&1 || {
        echo "Community macOS packaging requires codesign." >&2
        return 1
    }

    local sign_community_leaf
    sign_community_leaf() {
        [[ "$1" == "$main_executable" ]] && return
        if file -b "$1" | grep -q 'Mach-O'; then
            codesign --force --options runtime --sign - "$1"
        else
            codesign --force --sign - "$1"
        fi
    }
    local sign_community_bundle
    sign_community_bundle() {
        codesign --force --options runtime --sign - "$1"
    }

    for_each_macos_payload_file_deepest_first "$app_bundle" sign_community_leaf
    for_each_nested_code_bundle_deepest_first "$app_bundle" sign_community_bundle
    codesign \
        --force \
        --options runtime \
        --entitlements "$MAC_ENTITLEMENTS" \
        --sign - \
        "$app_bundle"
    codesign --verify --deep --strict --verbose=2 "$app_bundle"
}

sign_macos_bundle_developer_id() {
    local app_bundle="$1"
    local main_executable="$app_bundle/Contents/MacOS/Spyro.Editor.App"
    local identity

    [[ -f "$MAC_ENTITLEMENTS" ]] || {
        echo "Missing macOS entitlements: $MAC_ENTITLEMENTS" >&2
        return 1
    }
    command -v codesign >/dev/null 2>&1 || {
        echo "Developer ID macOS signing requires codesign." >&2
        return 1
    }

    identity="$(resolve_developer_id_identity)"
    echo "Signing macOS application with $identity"

    local sign_production_leaf
    sign_production_leaf() {
        [[ "$1" == "$main_executable" ]] && return
        if file -b "$1" | grep -q 'Mach-O'; then
            codesign \
                --force \
                --options runtime \
                --timestamp \
                --sign "$identity" \
                "$1"
        else
            # These generic seals make strict bundle validation treat the .NET
            # assemblies and data beside the executable as prepared payload.
            # The notary service checks each nested signature independently, so
            # these seals need a secure timestamp even though they are not Mach-O.
            codesign \
                --force \
                --timestamp \
                --sign "$identity" \
                "$1"
        fi
    }
    local sign_production_bundle
    sign_production_bundle() {
        codesign \
            --force \
            --options runtime \
            --timestamp \
            --sign "$identity" \
            "$1"
    }

    # Sign every nested payload leaf (including every Mach-O) before its
    # containing code bundle, then sign the outer application last. Do not use
    # --deep for signing: it can conceal an incorrectly signed component.
    for_each_macos_payload_file_deepest_first "$app_bundle" sign_production_leaf
    for_each_nested_code_bundle_deepest_first "$app_bundle" sign_production_bundle
    codesign \
        --force \
        --options runtime \
        --timestamp \
        --entitlements "$MAC_ENTITLEMENTS" \
        --sign "$identity" \
        "$app_bundle"
    codesign --verify --deep --strict --verbose=2 "$app_bundle"
}

notarize_macos_bundle() {
    local app_bundle="$1"
    local notary_zip
    local notary_result
    local notary_status

    [[ -n "$NOTARY_KEYCHAIN_PROFILE" ]] || {
        echo "Public macOS production releases require SPYRO_EDITOR_NOTARY_KEYCHAIN_PROFILE." >&2
        echo "Create one with: xcrun notarytool store-credentials <profile-name>" >&2
        return 1
    }
    command -v xcrun >/dev/null 2>&1 || {
        echo "macOS production notarization requires Xcode command-line tools." >&2
        return 1
    }
    command -v ditto >/dev/null 2>&1 || {
        echo "macOS production notarization requires ditto." >&2
        return 1
    }

    NOTARIZATION_TEMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/spyro-editor-notarization.XXXXXX")"
    notary_zip="$NOTARIZATION_TEMP_DIR/Spyro-Editor-notarization.zip"
    notary_result="$NOTARIZATION_TEMP_DIR/notary-result.json"
    ditto -c -k --keepParent "$app_bundle" "$notary_zip"

    echo "Submitting macOS application for notarization (profile: $NOTARY_KEYCHAIN_PROFILE)"
    if ! xcrun notarytool submit "$notary_zip" \
        --keychain-profile "$NOTARY_KEYCHAIN_PROFILE" \
        --wait \
        --output-format json > "$notary_result"; then
        cat "$notary_result" >&2 || true
        echo "Apple notarization submission failed." >&2
        return 1
    fi
    cat "$notary_result"
    notary_status="$(python3 - "$notary_result" <<'PY'
import json
import sys

with open(sys.argv[1], "r", encoding="utf-8") as stream:
    print(json.load(stream).get("status", ""))
PY
    )"
    [[ "$notary_status" == "Accepted" ]] || {
        echo "Apple notarization did not return Accepted (status: ${notary_status:-missing})." >&2
        return 1
    }

    xcrun stapler staple "$app_bundle"
    xcrun stapler validate "$app_bundle"
    codesign --verify --deep --strict --verbose=2 "$app_bundle"
    spctl --assess --type execute --verbose=2 "$app_bundle"

    rm -rf "$NOTARIZATION_TEMP_DIR"
    NOTARIZATION_TEMP_DIR=""
}

validate_public_changelog_contract() {
    local changelog="$ROOT_DIR/CHANGELOG.md"
    local expected_delta_heading="## Changes since $PREVIOUS_PUBLIC_RELEASE_NAME"
    local release_heading_count

    [[ -f "$changelog" ]] || {
        echo "Missing public release changelog: $changelog" >&2
        return 1
    }
    [[ "$(sed -n '1p' "$changelog")" == "# $PUBLIC_RELEASE_NAME" ]] || {
        echo "CHANGELOG.md must begin with '# $PUBLIC_RELEASE_NAME'." >&2
        return 1
    }
    [[ -z "$(sed -n '2p' "$changelog")" ]] || {
        echo "CHANGELOG.md must contain a blank line after its release heading." >&2
        return 1
    }
    [[ "$(sed -n '3p' "$changelog")" == "$expected_delta_heading" ]] || {
        echo "CHANGELOG.md line 3 must be '$expected_delta_heading'." >&2
        return 1
    }
    release_heading_count="$(grep -Ec '^# Spyro Editor Beta V' "$changelog" || true)"
    [[ "$release_heading_count" == "1" ]] || {
        echo "CHANGELOG.md must contain exactly one public release heading; it is the delta from the immediately previous release, not accumulated history." >&2
        return 1
    }
    awk 'NR > 3 && NF { found = 1 } END { exit(found ? 0 : 1) }' "$changelog" || {
        echo "CHANGELOG.md must describe at least one change after its previous-release heading." >&2
        return 1
    }
}

preflight_release_build() {
    local mac_build_mode

    if is_research_build; then
        return
    fi
    [[ "$RELEASE_NAME" == "$PUBLIC_RELEASE_SLUG" ]] || {
        echo "Public release name '$RELEASE_NAME' must be '$PUBLIC_RELEASE_SLUG'." >&2
        return 1
    }
    validate_public_changelog_contract

    mac_build_mode="$(resolve_mac_build_mode)"
    if [[ "$mac_build_mode" == "local" ]]; then
        return
    fi

    [[ -f "$MAC_ENTITLEMENTS" ]] || {
        echo "Missing macOS entitlements: $MAC_ENTITLEMENTS" >&2
        return 1
    }
    command -v codesign >/dev/null 2>&1 || {
        echo "Developer ID macOS signing requires codesign." >&2
        return 1
    }
    if [[ "$mac_build_mode" == "community" ]]; then
        return
    fi
    resolve_developer_id_identity >/dev/null

    if [[ "$mac_build_mode" == "production" ]]; then
        [[ -n "$NOTARY_KEYCHAIN_PROFILE" ]] || {
            echo "Public macOS production releases require SPYRO_EDITOR_NOTARY_KEYCHAIN_PROFILE." >&2
            echo "Create one with: xcrun notarytool store-credentials <profile-name>" >&2
            return 1
        }
        command -v xcrun >/dev/null 2>&1 || {
            echo "macOS production notarization requires Xcode command-line tools." >&2
            return 1
        }
        command -v ditto >/dev/null 2>&1 || {
            echo "macOS production notarization requires ditto." >&2
            return 1
        }
    fi
}

copy_release_files() {
    local package_dir="$1"
    local support_dir="$package_dir/support"
    mkdir -p "$support_dir/docs"

    cp "$ROOT_DIR/spyro-level-catalog.json" "$support_dir/spyro-level-catalog.json"
    cp "$ROOT_DIR/spyro-object-templates.json" "$support_dir/spyro-object-templates.json"
    cp "$ROOT_DIR/docs/release-user-guide.md" "$support_dir/docs/release-user-guide.md"
    cp "$ROOT_DIR/docs/known-limitations.md" "$support_dir/docs/known-limitations.md"

    find "$ROOT_DIR" -maxdepth 1 \
        \( -name '*-moby-user-overrides.json' -o -name '*-live-validation-overrides.json' -o -name '*-behavior-links.json' \) \
        -exec cp {} "$support_dir/" \;

    if is_research_build; then
        cat > "$package_dir/README.txt" <<'README'
Spyro Editor research build

Start with Launch Spyro Editor Beta Preview to test the normal customer UI.
Use Launch Spyro Editor Research only when you need the advanced research tabs.

Use Open BIN/CUE inside the editor and choose your own Spyro the Dragon disc
image. The editor rebuilds terrain maps and object placement from that selected
disc image, then writes patched test BIN/CUE output to the output folder.

This package does not include game data, BIOS files, emulator files, RAM dumps,
patched discs, or extracted assets.

Use Diagnostics in the editor to copy a crash/runtime-test report or create a
metadata-only support ZIP. Create BIN writes a matching diagnostics report and
support ZIP beside its output and never includes the BIN/CUE in that ZIP.

This research build exposes Advanced tools for disposable candidate tests.
Unsafe append research BINs bypass normal enemy/chest export guards and may
create inert, invisible, or broken objects.

More detail:
  support/docs/release-user-guide.md
  support/docs/known-limitations.md
README
    else
        cp "$ROOT_DIR/CHANGELOG.md" "$package_dir/CHANGELOG.md"
        cat > "$package_dir/README.txt" <<README
$PUBLIC_RELEASE_NAME
Internal build: $PROJECT_VERSION

On macOS, open Spyro Editor.app directly. On Windows, start with
Launch Spyro Editor.bat.

Use Open BIN/CUE inside the editor and choose your own Spyro the Dragon disc
image. The editor rebuilds terrain maps and object placement from that selected
disc image, then writes patched test BIN/CUE output to the output folder.

This package does not include game data, BIOS files, emulator files, RAM dumps,
patched discs, or extracted assets.

Use Diagnostics in the editor to copy a crash/runtime-test report or create a
metadata-only support ZIP. Create BIN writes a matching diagnostics report and
support ZIP beside its output and never includes the BIN/CUE in that ZIP.

The beta includes native all-level sky recoloring, guarded same-disc sky swaps,
three original sky recipes, one-click level terrain palette matching, native
.sky imports, guarded native object lighting with zero object-row reroutes,
guarded native terrain texture swaps with selected-face source-verified
near/fade tints and proven cross-level record relocation, while arbitrary custom
PNG BIN writeback remains blocked, plus the guarded existing-slot chest/enemy Replace
catalogue. Its cross-level chest/enemy choices
remain guarded behind Create Swap Test unless a target recipe has passed live
runtime proof. Blowhard Green Wizard v2 and the exact Magic Crafters T107 to T27
v2 route are composed by normal Create BIN, and
the Toasty v11 standalone bundle is runtime-proven but remains profile-gated in
the editor. Magic Crafters v1's lightning missed Spyro; v2 reuses the target's
existing properties extent without scene-component growth and passed the focused
DuckStation hit/behavior proof. Other Magic Crafters targets and true Add remain
guarded. Wizard Peak now exposes the exact runtime-proven native T6 to pod-matched
Elder Wizard T24 v3 route through Replace; normal Create BIN and Create Swap Test
compose it while both failed T10 candidates remain blocked. Unsafe append
and actor-package probes, smoke tests, and Spring Chest import experiments are
intentionally hidden.

More detail:
  support/docs/release-user-guide.md
  support/docs/known-limitations.md
README
    fi
}

write_release_manifest() {
    local package_dir="$1"
    local rid="$2"
    if is_research_build; then
        return
    fi
    if [[ "$RELEASE_MANIFEST_SCHEMA_VERSION" == "1" ]]; then
        # Whole-number compatibility bridges intentionally keep the exact
        # schema-1 shape consumed by already installed V2 clients. Do not add
        # even optional fields to this branch.
        cat > "$package_dir/release-manifest.json" <<JSON
{
  "schemaVersion": 1,
  "channel": "beta",
  "publicBeta": $BETA_RELEASE_NUMBER,
  "displayName": "$PUBLIC_RELEASE_NAME",
  "internalVersion": "$PROJECT_VERSION",
  "platform": "$rid"
}
JSON
    else
        cat > "$package_dir/release-manifest.json" <<JSON
{
  "schemaVersion": 2,
  "channel": "beta",
  "publicBeta": $BETA_RELEASE_NUMBER,
  "publicVersion": "$PUBLIC_RELEASE_VERSION",
  "displayName": "$PUBLIC_RELEASE_NAME",
  "internalVersion": "$PROJECT_VERSION",
  "platform": "$rid"
}
JSON
    fi
}

write_mac_launcher() {
    local package_dir="$1"
    if is_research_build; then
        cat > "$package_dir/Launch Spyro Editor Research.command" <<'LAUNCHER'
#!/bin/zsh
set -e
cd "$(dirname "$0")"
mkdir -p output
export SPYRO_EDITOR_WORKSPACE="$PWD"
export SPYRO_EDITOR_RELEASE=0
"./Spyro Editor.app/Contents/MacOS/Spyro.Editor.App"
LAUNCHER
        chmod +x "$package_dir/Launch Spyro Editor Research.command"
        cat > "$package_dir/Launch Spyro Editor Beta Preview.command" <<'LAUNCHER'
#!/bin/zsh
set -e
cd "$(dirname "$0")"
export SPYRO_EDITOR_INSTALL_ROOT="$PWD"
export SPYRO_EDITOR_RELEASE=1
"./Spyro Editor.app/Contents/MacOS/Spyro.Editor.App"
LAUNCHER
        chmod +x "$package_dir/Launch Spyro Editor Beta Preview.command"
    fi
}

write_macos_open_instructions() {
    local package_dir="$1"
    cat > "$package_dir/MACOS-OPEN-INSTRUCTIONS.txt" <<'INSTRUCTIONS'
Spyro Editor macOS emergency opening instructions

This build is Developer ID signed and hardened, but it has not completed Apple's
notarization service. First, try opening Spyro Editor.app normally.

If macOS blocks it:
1. Open System Settings > Privacy & Security.
2. Find the message about Spyro Editor in the Security section.
3. Click Open Anyway, authenticate if asked, then confirm Open.

Open Anyway bypasses Apple's missing-notarization warning for this app. Use it
only if this ZIP came from the official Spyro Editor GitHub release; do not use
it for a copy from another source. The normal release path remains notarized.
INSTRUCTIONS
}

write_macos_community_open_instructions() {
    local package_dir="$1"
    cat > "$package_dir/MACOS-OPEN-INSTRUCTIONS.txt" <<'INSTRUCTIONS'
Spyro Editor macOS community opening instructions

This emergency package is ad-hoc signed and hardened, but it is not Apple-signed
or notarized because the Developer ID certificate was unavailable for this
release. macOS is expected to block the first launch.

Only continue if this ZIP came from the official Spyro Editor GitHub release.
The release page publishes the ZIP's SHA-256 digest so the download can be
checked before opening it.

If macOS blocks it:
1. Try opening Spyro Editor.app once so macOS records the block.
2. Open System Settings > Privacy & Security.
3. Find the message about Spyro Editor in the Security section.
4. Click Open Anyway, authenticate if asked, then confirm Open.

Open Anyway accepts the risk for this exact app copy. Do not use it for a copy
from another source. Project files remain outside the replaceable app folder.
INSTRUCTIONS
}

write_mac_app_bundle() {
    local package_dir="$1"
    local app_bundle="$package_dir/Spyro Editor.app"
    local contents_dir="$app_bundle/Contents"
    local macos_dir="$contents_dir/MacOS"
    local resources_dir="$contents_dir/Resources"
    mkdir -p "$macos_dir" "$resources_dir"

    cp "$ROOT_DIR/src/Spyro.Editor.App/Assets/Brand/app-icon.icns" "$resources_dir/AppIcon.icns"

    cat > "$contents_dir/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleDisplayName</key>
    <string>$PUBLIC_RELEASE_NAME</string>
    <key>CFBundleExecutable</key>
    <string>Spyro.Editor.App</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundleIdentifier</key>
    <string>local.spyro.editor</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>Spyro Editor</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>$MAC_BUNDLE_SHORT_VERSION</string>
    <key>CFBundleVersion</key>
    <string>$BUILD_NUMBER</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST

    find "$package_dir/support/app" -mindepth 1 -maxdepth 1 -exec mv {} "$macos_dir/" \;
    chmod +x "$macos_dir/Spyro.Editor.App"

    if ! is_research_build; then
        # Keep the release self-identifying and project-safe even when a Mac user
        # launches or moves the .app without the surrounding portable folder.
        mkdir -p "$macos_dir/support"
        cp -R "$package_dir/support/." "$macos_dir/support/"
        rm -rf "$macos_dir/support/app"
    fi

    # dotnet publish can mark managed assemblies and JSON payloads executable.
    # Inside Contents/MacOS that makes codesign misclassify data as unsigned
    # nested code. Normalize everything to data, then restore the executable
    # bit only for actual Mach-O components.
    find "$macos_dir" -type f -exec chmod a-x {} +
    local make_macho_executable
    make_macho_executable() {
        chmod +x "$1"
    }
    for_each_macho_deepest_first "$app_bundle" make_macho_executable
    chmod +x "$macos_dir/Spyro.Editor.App"
}

write_windows_launcher() {
    local package_dir="$1"
    if is_research_build; then
        cat > "$package_dir/Launch Spyro Editor Research.bat" <<'LAUNCHER'
@echo off
setlocal
cd /d "%~dp0"
if not exist "%~dp0output" mkdir "%~dp0output"
set "SPYRO_EDITOR_WORKSPACE=%CD%"
set "SPYRO_EDITOR_RELEASE=0"
"%~dp0support\app\Spyro.Editor.App.exe"
LAUNCHER
        cat > "$package_dir/Launch Spyro Editor Beta Preview.bat" <<'LAUNCHER'
@echo off
setlocal
cd /d "%~dp0"
set "SPYRO_EDITOR_INSTALL_ROOT=%CD%"
set "SPYRO_EDITOR_RELEASE=1"
"%~dp0support\app\Spyro.Editor.App.exe"
LAUNCHER
    else
        cat > "$package_dir/Launch Spyro Editor.bat" <<'LAUNCHER'
@echo off
setlocal
cd /d "%~dp0"
set "SPYRO_EDITOR_INSTALL_ROOT=%CD%"
set "SPYRO_EDITOR_RELEASE=1"
"%~dp0support\app\Spyro.Editor.App.exe"
LAUNCHER
    fi
}

publish_release_package() {
    local rid="$1"
    local package_dir="$DIST_DIR/$RELEASE_NAME-$rid"
    local zip_path="$DIST_DIR/$RELEASE_NAME-$rid.zip"
    rm -rf "$package_dir"
    rm -f "$zip_path"
    mkdir -p "$package_dir/support/app"

    dotnet publish "$APP_PROJECT" \
        --configuration "$CONFIGURATION" \
        --runtime "$rid" \
        --self-contained true \
        -p:PublishSingleFile=false \
        -p:DebugType=None \
        -p:DebugSymbols=false \
        --output "$package_dir/support/app"
    find "$package_dir/support/app" -type f -name '*.pdb' -delete

    copy_release_files "$package_dir"
    write_release_manifest "$package_dir" "$rid"
    if [[ "$rid" == osx-* ]]; then
        local mac_build_mode
        write_mac_app_bundle "$package_dir"
        write_mac_launcher "$package_dir"
        mac_build_mode="$(resolve_mac_build_mode)"
        case "$mac_build_mode" in
            signed-only)
                write_macos_open_instructions "$package_dir"
                ;;
            community)
                write_macos_community_open_instructions "$package_dir"
                ;;
        esac
        if command -v xattr >/dev/null 2>&1; then
            # Strip provenance, quarantine, Finder, and other machine-local metadata
            # before codesign adds only the signature attributes it needs.
            xattr -cr "$package_dir"
        fi
        case "$mac_build_mode" in
            production)
                sign_macos_bundle_developer_id "$package_dir/Spyro Editor.app"
                notarize_macos_bundle "$package_dir/Spyro Editor.app"
                ;;
            signed-only)
                echo "Building emergency Developer ID-signed macOS package without notarization."
                sign_macos_bundle_developer_id "$package_dir/Spyro Editor.app"
                ;;
            community)
                echo "Building explicitly approved ad-hoc-signed macOS community package."
                sign_macos_bundle_community "$package_dir/Spyro Editor.app"
                ;;
            local)
                echo "Building explicitly local, non-notarized macOS package."
                sign_macos_bundle_local "$package_dir/Spyro Editor.app"
                ;;
        esac
    elif [[ "$rid" == win-* ]]; then
        write_windows_launcher "$package_dir"
    fi

    local unexpected_game_image
    unexpected_game_image="$(find "$package_dir" -type f \( -iname '*.bin' -o -iname '*.cue' \) -print -quit)"
    if [[ -n "$unexpected_game_image" ]]; then
        echo "Refusing to package game BIN/CUE: $unexpected_game_image" >&2
        exit 1
    fi

    if [[ "$rid" == osx-* ]] && command -v ditto >/dev/null 2>&1; then
        # Preserve code signature metadata and, for production, the stapled ticket
        # through the ZIP roundtrip.
        (cd "$DIST_DIR" && ditto -c -k --sequesterRsrc --keepParent "$RELEASE_NAME-$rid" "$RELEASE_NAME-$rid.zip")
    else
        (cd "$DIST_DIR" && zip -qr "$RELEASE_NAME-$rid.zip" "$RELEASE_NAME-$rid")
    fi
    echo "Wrote $package_dir"
    echo "Wrote $zip_path"
}

preflight_release_build

dotnet clean "$APP_PROJECT" --configuration "$CONFIGURATION"
dotnet build "$APP_PROJECT" --configuration "$CONFIGURATION"

publish_release_package "osx-arm64"
publish_release_package "win-x64"

if ! is_research_build && [[ -x "$ROOT_DIR/tools/Verify-SpyroEditorRelease.sh" ]]; then
    echo
    case "$(resolve_mac_build_mode)" in
        signed-only)
            SPYRO_EDITOR_ALLOW_UNNOTARIZED=1 \
                "$ROOT_DIR/tools/Verify-SpyroEditorRelease.sh" "$RELEASE_NAME"
            ;;
        community)
            SPYRO_EDITOR_ALLOW_ADHOC=1 \
                "$ROOT_DIR/tools/Verify-SpyroEditorRelease.sh" "$RELEASE_NAME"
            ;;
        *)
            "$ROOT_DIR/tools/Verify-SpyroEditorRelease.sh" "$RELEASE_NAME"
            ;;
    esac
fi

echo
echo "Release packages are in $DIST_DIR"

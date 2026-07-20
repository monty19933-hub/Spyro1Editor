#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIST_DIR="${DIST_DIR:-$ROOT_DIR/dist/release}"
ALLOW_HOST_PROVENANCE="${SPYRO_EDITOR_ALLOW_HOST_PROVENANCE:-0}"
ALLOW_UNNOTARIZED="${SPYRO_EDITOR_ALLOW_UNNOTARIZED:-0}"
ALLOW_ADHOC="${SPYRO_EDITOR_ALLOW_ADHOC:-0}"
EXPECTED_MAC_TEAM_ID="694865MF93"
[[ "$ALLOW_HOST_PROVENANCE" == "0" || "$ALLOW_HOST_PROVENANCE" == "1" ]] || {
    echo "SPYRO_EDITOR_ALLOW_HOST_PROVENANCE must be 0 or 1." >&2
    exit 2
}
[[ "$ALLOW_UNNOTARIZED" == "0" || "$ALLOW_UNNOTARIZED" == "1" ]] || {
    echo "SPYRO_EDITOR_ALLOW_UNNOTARIZED must be 0 or 1." >&2
    exit 2
}
[[ "$ALLOW_ADHOC" == "0" || "$ALLOW_ADHOC" == "1" ]] || {
    echo "SPYRO_EDITOR_ALLOW_ADHOC must be 0 or 1." >&2
    exit 2
}
[[ "$ALLOW_UNNOTARIZED" == "0" || "$ALLOW_ADHOC" == "0" ]] || {
    echo "SPYRO_EDITOR_ALLOW_UNNOTARIZED and SPYRO_EDITOR_ALLOW_ADHOC are mutually exclusive." >&2
    exit 2
}
usage() {
    echo "Usage: tools/Verify-SpyroEditorRelease.sh <release-name>" >&2
}

fail() {
    echo "Spyro Editor package verification failed: $*" >&2
    exit 1
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || fail "Required command '$1' was not found."
}

require_file() {
    [[ -f "$1" ]] || fail "Required package file is missing: $1"
}

require_directory() {
    [[ -d "$1" ]] || fail "Required package directory is missing: $1"
}

if [[ "$#" -ne 1 ]]; then
    usage
    exit 2
fi

RELEASE_NAME="$1"
[[ "$RELEASE_NAME" =~ ^[A-Za-z0-9._-]+$ ]] || fail "Release name contains an unsafe path character: $RELEASE_NAME"

[[ "$(uname -s)" == "Darwin" ]] || fail "Release verification requires macOS for Developer ID and notarization checks."
for command_name in awk cmp codesign diff dotnet file find plutil python3 rg sed shasum spctl strings unzip xattr xcrun; do
    require_command "$command_name"
done
if [[ "$ALLOW_UNNOTARIZED" == "0" && "$ALLOW_ADHOC" == "0" ]]; then
    require_command syspolicy_check
fi
xcrun --find stapler >/dev/null 2>&1 || fail "Apple's stapler tool was not found through xcrun."

PROJECT_FILE="$ROOT_DIR/src/Spyro.Editor.App/Spyro.Editor.App.csproj"
RELEASE_IDENTITY_TOOL="$ROOT_DIR/src/Spyro.Editor.ReleaseIdentityTool/Spyro.Editor.ReleaseIdentityTool.csproj"
require_file "$PROJECT_FILE"
require_file "$RELEASE_IDENTITY_TOOL"
PROJECT_VERSION="$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' "$PROJECT_FILE" | head -n 1)"
BETA_RELEASE_NUMBER="$(sed -n 's:.*<BetaReleaseNumber>\([^<]*\)</BetaReleaseNumber>.*:\1:p' "$PROJECT_FILE" | head -n 1)"
PUBLIC_RELEASE_VERSION="$(sed -n 's:.*<PublicReleaseVersion>\([^<]*\)</PublicReleaseVersion>.*:\1:p' "$PROJECT_FILE" | head -n 1)"
RELEASE_MANIFEST_SCHEMA_VERSION="$(sed -n 's:.*<ReleaseManifestSchemaVersion>\([^<]*\)</ReleaseManifestSchemaVersion>.*:\1:p' "$PROJECT_FILE" | head -n 1)"
PREVIOUS_PUBLIC_RELEASE_VERSION="$(sed -n 's:.*<PreviousPublicReleaseVersion>\([^<]*\)</PreviousPublicReleaseVersion>.*:\1:p' "$PROJECT_FILE" | head -n 1)"
[[ -n "$PROJECT_VERSION" ]] || fail "Could not read the app version from $PROJECT_FILE"
[[ "$PROJECT_VERSION" =~ ^([0-9]+\.[0-9]+\.[0-9]+)-beta\.([0-9]+)$ ]] || \
    fail "Unsupported app version format '$PROJECT_VERSION'; expected X.Y.Z-beta.N."
BASE_VERSION="${BASH_REMATCH[1]}"
BUILD_NUMBER="${BASH_REMATCH[2]}"
[[ "$BETA_RELEASE_NUMBER" =~ ^[1-9][0-9]*$ ]] || \
    fail "Unsupported legacy public beta release '$BETA_RELEASE_NUMBER'; expected a positive integer."
[[ "$PUBLIC_RELEASE_VERSION" =~ ^[1-9][0-9]*(\.[1-9][0-9]*)?$ ]] || \
    fail "Unsupported public release version '$PUBLIC_RELEASE_VERSION'; expected a canonical value such as 3 or 3.1."
[[ "$PREVIOUS_PUBLIC_RELEASE_VERSION" =~ ^[1-9][0-9]*(\.[1-9][0-9]*)?$ ]] || \
    fail "Unsupported previous public release version '$PREVIOUS_PUBLIC_RELEASE_VERSION'."
python3 - "$PREVIOUS_PUBLIC_RELEASE_VERSION" "$PUBLIC_RELEASE_VERSION" <<'PY' || \
    fail "Previous public release '$PREVIOUS_PUBLIC_RELEASE_VERSION' must be older than '$PUBLIC_RELEASE_VERSION'."
import sys

def release_tuple(value):
    major, separator, minor = value.partition(".")
    return int(major), int(minor) if separator else 0

previous = release_tuple(sys.argv[1])
current = release_tuple(sys.argv[2])
raise SystemExit(0 if previous < current else 1)
PY
[[ "$RELEASE_MANIFEST_SCHEMA_VERSION" == "1" || "$RELEASE_MANIFEST_SCHEMA_VERSION" == "2" ]] || \
    fail "Unsupported release manifest schema '$RELEASE_MANIFEST_SCHEMA_VERSION'; expected 1 or 2."
PUBLIC_RELEASE_MAJOR="${PUBLIC_RELEASE_VERSION%%.*}"
[[ "$PUBLIC_RELEASE_MAJOR" == "$BETA_RELEASE_NUMBER" ]] || \
    fail "Public release '$PUBLIC_RELEASE_VERSION' must retain legacy beta integer '$BETA_RELEASE_NUMBER' as its major component."
if [[ "$RELEASE_MANIFEST_SCHEMA_VERSION" == "1" ]]; then
    [[ "$PUBLIC_RELEASE_VERSION" == "$BETA_RELEASE_NUMBER" ]] || \
        fail "Schema 1 requires PublicReleaseVersion=$BETA_RELEASE_NUMBER for a whole-number compatibility bridge."
else
    [[ "$PUBLIC_RELEASE_VERSION" == *.* ]] || \
        fail "Schema 2 is reserved for canonical dotted releases such as V3.1."
fi
PUBLIC_RELEASE_NAME="Spyro Editor Beta V$PUBLIC_RELEASE_VERSION"
PREVIOUS_PUBLIC_RELEASE_NAME="Spyro Editor Beta V$PREVIOUS_PUBLIC_RELEASE_VERSION"
EXPECTED_RELEASE_NAME="SpyroEditor-Beta-V$PUBLIC_RELEASE_VERSION"
if [[ "$PUBLIC_RELEASE_VERSION" == *.* ]]; then
    MAC_BUNDLE_SHORT_VERSION="$PUBLIC_RELEASE_VERSION"
else
    MAC_BUNDLE_SHORT_VERSION="$PUBLIC_RELEASE_VERSION.0"
fi
[[ "$RELEASE_NAME" == "$EXPECTED_RELEASE_NAME" ]] || \
    fail "Release name '$RELEASE_NAME' must be '$EXPECTED_RELEASE_NAME'."

[[ "$(sed -n '1p' "$ROOT_DIR/CHANGELOG.md")" == "# $PUBLIC_RELEASE_NAME" ]] || \
    fail "CHANGELOG.md must begin with '# $PUBLIC_RELEASE_NAME'."
[[ -z "$(sed -n '2p' "$ROOT_DIR/CHANGELOG.md")" ]] || \
    fail "CHANGELOG.md must contain a blank line after its release heading."
[[ "$(sed -n '3p' "$ROOT_DIR/CHANGELOG.md")" == "## Changes since $PREVIOUS_PUBLIC_RELEASE_NAME" ]] || \
    fail "CHANGELOG.md must be the exact delta headed 'Changes since $PREVIOUS_PUBLIC_RELEASE_NAME'."
[[ "$(grep -Ec '^# Spyro Editor Beta V' "$ROOT_DIR/CHANGELOG.md" || true)" == "1" ]] || \
    fail "CHANGELOG.md must contain exactly one public release heading."
awk 'NR > 3 && NF { found = 1 } END { exit(found ? 0 : 1) }' "$ROOT_DIR/CHANGELOG.md" || \
    fail "CHANGELOG.md must describe at least one change after its previous-release heading."

MAC_DIR="$DIST_DIR/$RELEASE_NAME-osx-arm64"
WIN_DIR="$DIST_DIR/$RELEASE_NAME-win-x64"
MAC_ZIP="$MAC_DIR.zip"
WIN_ZIP="$WIN_DIR.zip"
MAC_APP="$MAC_DIR/Spyro Editor.app"
MAC_EXE="$MAC_APP/Contents/MacOS/Spyro.Editor.App"
MAC_APP_DIR="$MAC_APP/Contents/MacOS"
MAC_OPEN_INSTRUCTIONS="$MAC_DIR/MACOS-OPEN-INSTRUCTIONS.txt"
WIN_APP_DIR="$WIN_DIR/support/app"
WIN_EXE="$WIN_APP_DIR/Spyro.Editor.App.exe"

require_directory "$MAC_DIR"
require_directory "$WIN_DIR"
require_file "$MAC_ZIP"
require_file "$WIN_ZIP"
require_file "$MAC_EXE"
require_file "$WIN_EXE"
[[ -x "$MAC_EXE" ]] || fail "macOS app executable is not executable: $MAC_EXE"
if [[ "$ALLOW_UNNOTARIZED" == "1" || "$ALLOW_ADHOC" == "1" ]]; then
    require_file "$MAC_OPEN_INSTRUCTIONS"
    rg -F 'System Settings > Privacy & Security' "$MAC_OPEN_INSTRUCTIONS" >/dev/null || \
        fail "Emergency macOS opening instructions do not identify Privacy & Security."
    rg -F 'Open Anyway' "$MAC_OPEN_INSTRUCTIONS" >/dev/null || \
        fail "Emergency macOS opening instructions do not identify Open Anyway."
    if [[ "$ALLOW_ADHOC" == "1" ]]; then
        rg -F 'ad-hoc signed' "$MAC_OPEN_INSTRUCTIONS" >/dev/null || \
            fail "Community macOS opening instructions do not disclose the ad-hoc signature."
        rg -F 'not Apple-signed' "$MAC_OPEN_INSTRUCTIONS" >/dev/null || \
            fail "Community macOS opening instructions do not disclose the missing Apple signature."
    else
        rg -F 'missing-notarization warning' "$MAC_OPEN_INSTRUCTIONS" >/dev/null || \
            fail "Emergency macOS opening instructions do not explain the missing-notarization risk."
    fi
else
    [[ ! -e "$MAC_OPEN_INSTRUCTIONS" ]] || \
        fail "A normal notarized release must not contain emergency Open Anyway instructions."
fi

archive_findings() {
    local archive="$1"
    local allow_mac_metadata="${2:-0}"
    unzip -Z1 "$archive" | awk -v allow_mac_metadata="$allow_mac_metadata" '
        BEGIN { IGNORECASE=1 }
        {
            path=$0
            if (path ~ /^\// || path ~ /^[A-Z]:/ || path ~ /(^|\/)\.\.(\/|$)/ || path ~ /\\/) {
                print "unsafe archive path: " path
            }
            if (path ~ /(^|\/)(\.git|_local|editor-cache|bin|obj)(\/|$)/ ||
                (!allow_mac_metadata && path ~ /(^|\/)__MACOSX(\/|$)/)) {
                print "forbidden generated directory: " path
            }
            if (path ~ /(^|\/)(WAD\.WAD|SCUS_[^\/]*|SLUS_[^\/]*|SCES_[^\/]*|SLES_[^\/]*|SCPH[^\/]*)(\/|$)/) {
                print "forbidden game or BIOS file: " path
            }
            if (path ~ /\.(bin|cue|iso|img|chd|ecm|m3u|ccd|sub|toc|bios|rom|mcr|srm|sav|state|ram|dmp|dump|pdb|dbg)(\/)?$/ ||
                path ~ /(^|\/)[^\/]+\.dSYM(\/|$)/) {
                print "forbidden game, save, or debug artifact: " path
            }
            if (path ~ /(^|\/)(\.DS_Store|core)(\/|$)/ ||
                path ~ /(before-clean|mainram|vram|live-capture|runtime-scene|runtime-moby|custom-terrain-textures)/) {
                print "forbidden local or captured artifact: " path
            }
        }
    '
}

validate_archive() {
    local archive="$1"
    local expected_root="$2"
    local allow_mac_metadata="${3:-0}"
    local entries
    local wrong_root
    local findings

    unzip -tq "$archive" >/dev/null
    entries="$(unzip -Z1 "$archive")"
    [[ -n "$entries" ]] || fail "Archive is empty: $archive"
    wrong_root="$(printf '%s\n' "$entries" | awk -v root="$expected_root/" -v allow_mac_metadata="$allow_mac_metadata" '
        index($0, root) == 1 { next }
        allow_mac_metadata && index($0, "__MACOSX/") == 1 { next }
        { print; exit }
    ')"
    [[ -z "$wrong_root" ]] || fail "Archive entry is outside expected root '$expected_root/': $wrong_root"
    findings="$(archive_findings "$archive" "$allow_mac_metadata")"
    [[ -z "$findings" ]] || fail "Archive contains forbidden content: $archive
$findings"
}

tree_findings() {
    local package_dir="$1"
    find "$package_dir" -type f \( \
        -iname 'WAD.WAD' -o -iname 'SCUS_*' -o -iname 'SLUS_*' -o -iname 'SCES_*' -o -iname 'SLES_*' -o -iname 'SCPH*' -o \
        -iname '*.bin' -o -iname '*.cue' -o -iname '*.iso' -o -iname '*.img' -o -iname '*.chd' -o -iname '*.ecm' -o \
        -iname '*.m3u' -o -iname '*.ccd' -o -iname '*.sub' -o -iname '*.toc' -o -iname '*.bios' -o -iname '*.rom' -o \
        -iname '*.mcr' -o -iname '*.srm' -o -iname '*.sav' -o -iname '*.state' -o -iname '*.ram' -o -iname '*.dmp' -o \
        -iname '*.dump' -o -iname '*.pdb' -o -iname '*.dbg' -o -iname '.DS_Store' -o -iname 'core' \
    \) -print
    find "$package_dir" -type d \( \
        -iname '.git' -o -iname '_local' -o -iname 'editor-cache' -o -iname 'bin' -o -iname 'obj' -o -iname '*.dSYM' \
    \) -print
}

validate_package_tree() {
    local package_dir="$1"
    local findings
    local oversized
    local symlink

    findings="$(tree_findings "$package_dir")"
    [[ -z "$findings" ]] || fail "Extracted package contains forbidden content: $package_dir
$findings"
    oversized="$(find "$package_dir" -type f -size +250M -print -quit)"
    [[ -z "$oversized" ]] || fail "Extracted package contains an unexpected file larger than 250 MB: $oversized"
    symlink="$(find "$package_dir" -type l -print -quit)"
    [[ -z "$symlink" ]] || fail "Extracted package contains an unexpected symbolic link: $symlink"
    if rg -I -n '/Users/|/Volumes/|[A-Za-z]:\\Users\\' "$package_dir" >/dev/null; then
        fail "Extracted package contains a machine-local user path: $package_dir"
    fi
}

validate_mac_xattrs() {
    local package_dir="$1"
    python3 - "$package_dir" "$ALLOW_HOST_PROVENANCE" <<'PY'
import os
import subprocess
import sys

root = os.path.abspath(sys.argv[1])
allow_host_provenance = sys.argv[2] == "1"
unexpected = []
for directory, directories, files in os.walk(root):
    paths = [directory]
    paths.extend(os.path.join(directory, name) for name in directories)
    paths.extend(os.path.join(directory, name) for name in files)
    for path in paths:
        result = subprocess.run(["xattr", path], capture_output=True, text=True)
        if result.returncode != 0:
            raise SystemExit(f"Could not inspect extended attributes for {path}: {result.stderr.strip()}")
        attributes = [line.strip() for line in result.stdout.splitlines() if line.strip()]
        for attribute in attributes:
            # Codex Desktop on current macOS attaches this generic three-byte
            # attribute to every child-process-created file and immediately
            # recreates it after xattr -c. Normal release verification remains
            # strict; the explicit opt-in ignores only that host-added key.
            if allow_host_provenance and attribute == "com.apple.provenance":
                continue
            if not attribute.startswith("com.apple.cs."):
                unexpected.append(f"{os.path.relpath(path, root)}: {attribute}")

if unexpected:
    print("Unexpected machine-local extended attributes:", file=sys.stderr)
    for finding in unexpected[:100]:
        print(f"  {finding}", file=sys.stderr)
    raise SystemExit(1)
PY
}

validate_mac_nested_payloads() {
    local app_path="$1"
    local phase="$2"
    local macos_path="$app_path/Contents/MacOS"
    local candidate
    local payload_info
    local payload_format

    while IFS= read -r -d '' candidate; do
        if ! payload_info="$(codesign --display --verbose=4 "$candidate" 2>&1)"; then
            printf '%s\n' "$payload_info" >&2
            fail "$phase macOS payload is not signed: $candidate"
        fi
        if [[ "$ALLOW_ADHOC" == "1" ]]; then
            printf '%s\n' "$payload_info" | rg '^Signature=adhoc$' >/dev/null || \
                fail "$phase macOS payload does not have the expected ad-hoc signature: $candidate"
            if printf '%s\n' "$payload_info" | rg '^Authority=' >/dev/null; then
                fail "$phase ad-hoc macOS payload unexpectedly has a certificate authority: $candidate"
            fi
        else
            printf '%s\n' "$payload_info" | rg '^Authority=Developer ID Application:' >/dev/null || \
                fail "$phase macOS payload is not signed by a Developer ID Application authority: $candidate"
            printf '%s\n' "$payload_info" | rg -F "TeamIdentifier=$EXPECTED_MAC_TEAM_ID" >/dev/null || \
                fail "$phase macOS payload signature is not issued to team $EXPECTED_MAC_TEAM_ID: $candidate"
            printf '%s\n' "$payload_info" | rg '^Timestamp=.+$' | rg -v '^Timestamp=(none)?$' >/dev/null || \
                fail "$phase macOS payload signature does not contain a secure timestamp: $candidate"
        fi

        payload_format="$(file -b "$candidate")"
        if [[ "$payload_format" == *Mach-O* ]]; then
            printf '%s\n' "$payload_info" | rg '^CodeDirectory .*flags=.*\(.*runtime.*\)' >/dev/null || \
                fail "$phase macOS Mach-O payload does not enable the hardened runtime: $candidate"
        fi
    done < <(find "$macos_path" -type f -print0)

    # Future native helpers can live outside Contents/MacOS (for example in a
    # framework bundle). Audit those Mach-O files too without mistaking normal
    # Resources such as the application icon for independently signed code.
    while IFS= read -r -d '' candidate; do
        [[ "$candidate" == "$macos_path/"* ]] && continue
        [[ "$(file -b "$candidate")" == *Mach-O* ]] || continue
        if ! payload_info="$(codesign --display --verbose=4 "$candidate" 2>&1)"; then
            printf '%s\n' "$payload_info" >&2
            fail "$phase macOS Mach-O payload is not signed: $candidate"
        fi
        if [[ "$ALLOW_ADHOC" == "1" ]]; then
            printf '%s\n' "$payload_info" | rg '^Signature=adhoc$' >/dev/null || \
                fail "$phase macOS Mach-O payload does not have the expected ad-hoc signature: $candidate"
            if printf '%s\n' "$payload_info" | rg '^Authority=' >/dev/null; then
                fail "$phase ad-hoc macOS Mach-O payload unexpectedly has a certificate authority: $candidate"
            fi
        else
            printf '%s\n' "$payload_info" | rg '^Authority=Developer ID Application:' >/dev/null || \
                fail "$phase macOS Mach-O payload is not signed by a Developer ID Application authority: $candidate"
            printf '%s\n' "$payload_info" | rg -F "TeamIdentifier=$EXPECTED_MAC_TEAM_ID" >/dev/null || \
                fail "$phase macOS Mach-O payload signature is not issued to team $EXPECTED_MAC_TEAM_ID: $candidate"
            printf '%s\n' "$payload_info" | rg '^Timestamp=.+$' | rg -v '^Timestamp=(none)?$' >/dev/null || \
                fail "$phase macOS Mach-O payload signature does not contain a secure timestamp: $candidate"
        fi
        printf '%s\n' "$payload_info" | rg '^CodeDirectory .*flags=.*\(.*runtime.*\)' >/dev/null || \
            fail "$phase macOS Mach-O payload does not enable the hardened runtime: $candidate"
    done < <(find "$app_path" -type f -print0)
}

validate_mac_distribution() {
    local app_path="$1"
    local phase="$2"
    local signature_info
    local entitlements_file
    local gatekeeper_output
    local stapler_output
    local verification_output

    if ! verification_output="$(codesign --verify --deep --strict --verbose=2 "$app_path" 2>&1)"; then
        printf '%s\n' "$verification_output" >&2
        fail "$phase macOS app does not have a valid strict code signature."
    fi

    signature_info="$(codesign --display --verbose=4 "$app_path" 2>&1)" || \
        fail "$phase macOS app signature details could not be read."
    if [[ "$ALLOW_ADHOC" == "1" ]]; then
        printf '%s\n' "$signature_info" | rg '^Signature=adhoc$' >/dev/null || \
            fail "$phase macOS app does not have the expected ad-hoc signature."
        if printf '%s\n' "$signature_info" | rg '^Authority=' >/dev/null; then
            fail "$phase ad-hoc macOS app unexpectedly has a certificate authority."
        fi
        printf '%s\n' "$signature_info" | rg '^TeamIdentifier=not set$' >/dev/null || \
            fail "$phase ad-hoc macOS app unexpectedly has a signing team."
    else
        printf '%s\n' "$signature_info" | rg '^Authority=Developer ID Application:' >/dev/null || \
            fail "$phase macOS app is not signed by a Developer ID Application authority."
        printf '%s\n' "$signature_info" | rg -F "($EXPECTED_MAC_TEAM_ID)" >/dev/null || \
            fail "$phase macOS Developer ID authority is not issued to team $EXPECTED_MAC_TEAM_ID."
        printf '%s\n' "$signature_info" | rg -F "TeamIdentifier=$EXPECTED_MAC_TEAM_ID" >/dev/null || \
            fail "$phase macOS signature team is not $EXPECTED_MAC_TEAM_ID."
        printf '%s\n' "$signature_info" | rg '^Timestamp=.+$' | rg -v '^Timestamp=(none)?$' >/dev/null || \
            fail "$phase macOS signature does not contain a secure timestamp."
    fi
    printf '%s\n' "$signature_info" | rg '^CodeDirectory .*flags=.*\(.*runtime.*\)' >/dev/null || \
        fail "$phase macOS signature does not enable the hardened runtime."

    entitlements_file="$(mktemp "${TMPDIR:-/tmp}/spyro-editor-entitlements.XXXXXX")"
    if ! codesign --display --entitlements :- "$app_path" >"$entitlements_file" 2>/dev/null; then
        rm -f "$entitlements_file"
        fail "$phase macOS app entitlements could not be read."
    fi
    if ! python3 - "$entitlements_file" <<'PY'
import plistlib
import sys

with open(sys.argv[1], "rb") as stream:
    entitlements = plistlib.load(stream)

if entitlements.get("com.apple.security.cs.allow-jit") is not True:
    raise SystemExit("com.apple.security.cs.allow-jit must be true")
if entitlements.get("com.apple.security.get-task-allow") not in (None, False):
    raise SystemExit("com.apple.security.get-task-allow must be absent or false")
PY
    then
        rm -f "$entitlements_file"
        fail "$phase macOS app does not have the required release-safe .NET JIT entitlements."
    fi
    rm -f "$entitlements_file"

    validate_mac_nested_payloads "$app_path" "$phase"
    if [[ "$ALLOW_UNNOTARIZED" == "1" || "$ALLOW_ADHOC" == "1" ]]; then
        if stapler_output="$(xcrun stapler validate "$app_path" 2>&1)"; then
            fail "$phase emergency signed-only macOS app unexpectedly contains a valid stapled notarization ticket."
        fi
        printf '%s\n' "$stapler_output" | rg -F 'does not have a ticket stapled to it.' >/dev/null || {
            printf '%s\n' "$stapler_output" >&2
            fail "$phase macOS stapler check failed for a reason other than a missing ticket."
        }
        if gatekeeper_output="$(spctl --assess --type execute --verbose=4 "$app_path" 2>&1)"; then
            fail "$phase non-notarized macOS app was unexpectedly accepted by Gatekeeper."
        fi
        if [[ "$ALLOW_UNNOTARIZED" == "1" ]]; then
            printf '%s\n' "$gatekeeper_output" | rg -F 'source=Unnotarized Developer ID' >/dev/null || {
                printf '%s\n' "$gatekeeper_output" >&2
                fail "$phase macOS Gatekeeper rejection was not the expected missing-notarization result."
            }
        fi
    else
        xcrun stapler validate "$app_path" >/dev/null || \
            fail "$phase macOS app does not contain a valid stapled notarization ticket."
        spctl --assess --type execute --verbose=4 "$app_path" || \
            fail "$phase macOS app was rejected by Gatekeeper assessment."
        syspolicy_check distribution "$app_path" >/dev/null || \
            fail "$phase macOS app was rejected by macOS distribution policy."
    fi
}

validate_archive "$MAC_ZIP" "$RELEASE_NAME-osx-arm64" 1
validate_archive "$WIN_ZIP" "$RELEASE_NAME-win-x64"
validate_package_tree "$MAC_DIR"
validate_package_tree "$WIN_DIR"
validate_mac_xattrs "$MAC_DIR" || fail "Built macOS package contains non-signature extended attributes."

file "$MAC_EXE" | rg 'Mach-O 64-bit executable arm64' >/dev/null || \
    fail "macOS executable is not Mach-O arm64: $MAC_EXE"
file "$WIN_EXE" | rg 'PE32\+ executable .*x86-64' >/dev/null || \
    fail "Windows executable is not PE32+ x86-64: $WIN_EXE"

PLIST="$MAC_APP/Contents/Info.plist"
require_file "$PLIST"
if command -v plutil >/dev/null 2>&1; then
    [[ "$(plutil -extract CFBundleVersion raw "$PLIST")" == "$BUILD_NUMBER" ]] || \
        fail "macOS CFBundleVersion does not equal $BUILD_NUMBER."
    [[ "$(plutil -extract CFBundleShortVersionString raw "$PLIST")" == "$MAC_BUNDLE_SHORT_VERSION" ]] || \
        fail "macOS CFBundleShortVersionString does not equal $MAC_BUNDLE_SHORT_VERSION."
    [[ "$(plutil -extract CFBundleDisplayName raw "$PLIST")" == "$PUBLIC_RELEASE_NAME" ]] || \
        fail "macOS CFBundleDisplayName does not equal '$PUBLIC_RELEASE_NAME'."
    [[ "$(plutil -extract CFBundleIdentifier raw "$PLIST")" == "local.spyro.editor" ]] || \
        fail "macOS CFBundleIdentifier is not local.spyro.editor."
fi
validate_mac_distribution "$MAC_APP" "Built"

WIN_LAUNCHER="$WIN_DIR/Launch Spyro Editor.bat"
require_file "$WIN_LAUNCHER"
[[ ! -e "$MAC_DIR/Launch Spyro Editor.command" ]] || \
    fail "macOS package must launch the notarized Spyro Editor.app directly, not through a .command launcher."
rg -F 'SPYRO_EDITOR_RELEASE=1' "$WIN_LAUNCHER" >/dev/null || fail "Windows launcher is not in release mode."
rg -F 'SPYRO_EDITOR_INSTALL_ROOT=%CD%' "$WIN_LAUNCHER" >/dev/null || fail "Windows launcher does not identify its replaceable install root."
if rg -F 'SPYRO_EDITOR_WORKSPACE=%CD%' "$WIN_LAUNCHER" >/dev/null; then
    fail "Release launcher still stores project data inside the replaceable application folder."
fi
[[ "$(head -n 1 "$MAC_DIR/README.txt")" == "$PUBLIC_RELEASE_NAME" ]] || fail "macOS README public release name is stale."
[[ "$(head -n 1 "$WIN_DIR/README.txt")" == "$PUBLIC_RELEASE_NAME" ]] || fail "Windows README public release name is stale."
[[ "$(sed -n '2p' "$MAC_DIR/README.txt")" == "Internal build: $PROJECT_VERSION" ]] || fail "macOS README internal build is stale."
[[ "$(sed -n '2p' "$WIN_DIR/README.txt")" == "Internal build: $PROJECT_VERSION" ]] || fail "Windows README internal build is stale."

for versioned_document in "$ROOT_DIR/README.md" "$ROOT_DIR/docs/release-user-guide.md" "$ROOT_DIR/docs/known-limitations.md" "$ROOT_DIR/CHANGELOG.md"; do
    rg -F "$PUBLIC_RELEASE_NAME" "$versioned_document" >/dev/null || \
        fail "Versioned source document does not identify $PUBLIC_RELEASE_NAME: $versioned_document"
done
rg -F "$PROJECT_VERSION" "$ROOT_DIR/docs/release-user-guide.md" >/dev/null || \
    fail "Release guide does not identify internal build $PROJECT_VERSION."
strings "$MAC_APP_DIR/Spyro.Editor.App.dll" | rg -F "$PROJECT_VERSION" >/dev/null || fail "macOS app DLL version is stale."
strings "$WIN_APP_DIR/Spyro.Editor.App.dll" | rg -F "$PROJECT_VERSION" >/dev/null || fail "Windows app DLL version is stale."
dotnet run --project "$RELEASE_IDENTITY_TOOL" --configuration Release -- \
    "$MAC_APP_DIR/Spyro.Editor.App.dll" "$PUBLIC_RELEASE_VERSION" "$PROJECT_VERSION" || \
    fail "macOS app DLL assembly metadata does not match the numbered release."
dotnet run --project "$RELEASE_IDENTITY_TOOL" --configuration Release -- \
    "$WIN_APP_DIR/Spyro.Editor.App.dll" "$PUBLIC_RELEASE_VERSION" "$PROJECT_VERSION" || \
    fail "Windows app DLL assembly metadata does not match the numbered release."
cmp "$MAC_APP_DIR/Spyro.Editor.Core.dll" "$WIN_APP_DIR/Spyro.Editor.Core.dll" >/dev/null || \
    fail "Mac and Windows packages contain different Spyro.Editor.Core.dll bytes."

compare_packaged_file() {
    local source_path="$1"
    local package_relative_path="$2"
    require_file "$source_path"
    require_file "$MAC_DIR/$package_relative_path"
    require_file "$WIN_DIR/$package_relative_path"
    cmp "$source_path" "$MAC_DIR/$package_relative_path" >/dev/null || \
        fail "macOS package has stale support file: $package_relative_path"
    cmp "$source_path" "$WIN_DIR/$package_relative_path" >/dev/null || \
        fail "Windows package has stale support file: $package_relative_path"
}

compare_packaged_file "$ROOT_DIR/docs/release-user-guide.md" "support/docs/release-user-guide.md"
compare_packaged_file "$ROOT_DIR/docs/known-limitations.md" "support/docs/known-limitations.md"
compare_packaged_file "$ROOT_DIR/spyro-level-catalog.json" "support/spyro-level-catalog.json"
compare_packaged_file "$ROOT_DIR/spyro-object-templates.json" "support/spyro-object-templates.json"
compare_packaged_file "$ROOT_DIR/CHANGELOG.md" "CHANGELOG.md"

validate_release_manifest() {
    local manifest_path="$1"
    local expected_platform="$2"
    require_file "$manifest_path"
    python3 - "$manifest_path" "$RELEASE_MANIFEST_SCHEMA_VERSION" "$BETA_RELEASE_NUMBER" "$PUBLIC_RELEASE_VERSION" "$PUBLIC_RELEASE_NAME" "$PROJECT_VERSION" "$expected_platform" <<'PY'
import json
import sys

path, schema, beta, public_version, display_name, internal_version, platform = sys.argv[1:]
with open(path, "r", encoding="utf-8") as stream:
    manifest = json.load(stream)
expected = {
    "schemaVersion": int(schema),
    "channel": "beta",
    "publicBeta": int(beta),
    "displayName": display_name,
    "internalVersion": internal_version,
    "platform": platform,
}
if schema == "2":
    expected["publicVersion"] = public_version
if manifest != expected:
    raise SystemExit(f"Release manifest mismatch in {path}: {manifest!r} != {expected!r}")
PY
}

validate_release_manifest "$MAC_DIR/release-manifest.json" "osx-arm64"
validate_release_manifest "$WIN_DIR/release-manifest.json" "win-x64"

compare_internal_mac_support_file() {
    local source_path="$1"
    local package_relative_path="$2"
    require_file "$MAC_APP_DIR/support/$package_relative_path"
    cmp "$source_path" "$MAC_APP_DIR/support/$package_relative_path" >/dev/null || \
        fail "macOS app-internal support file is stale: $package_relative_path"
}

compare_internal_mac_support_file "$ROOT_DIR/docs/release-user-guide.md" "docs/release-user-guide.md"
compare_internal_mac_support_file "$ROOT_DIR/docs/known-limitations.md" "docs/known-limitations.md"
compare_internal_mac_support_file "$ROOT_DIR/spyro-level-catalog.json" "spyro-level-catalog.json"
compare_internal_mac_support_file "$ROOT_DIR/spyro-object-templates.json" "spyro-object-templates.json"
[[ ! -d "$MAC_APP_DIR/support/app" ]] || fail "macOS app-internal support copy unexpectedly contains application binaries."

cmp "$ROOT_DIR/spyro-level-catalog.json" "$MAC_APP_DIR/spyro-level-catalog.json" >/dev/null || fail "macOS runtime level catalog is stale."
cmp "$ROOT_DIR/spyro-level-catalog.json" "$WIN_APP_DIR/spyro-level-catalog.json" >/dev/null || fail "Windows runtime level catalog is stale."
cmp "$ROOT_DIR/spyro-object-templates.json" "$MAC_APP_DIR/spyro-object-templates.json" >/dev/null || fail "macOS runtime object templates are stale."
cmp "$ROOT_DIR/spyro-object-templates.json" "$WIN_APP_DIR/spyro-object-templates.json" >/dev/null || fail "Windows runtime object templates are stale."

for source_metadata in \
    "$ROOT_DIR"/*-moby-user-overrides.json \
    "$ROOT_DIR"/*-live-validation-overrides.json \
    "$ROOT_DIR"/*-behavior-links.json; do
    [[ -f "$source_metadata" ]] || continue
    metadata_name="$(basename "$source_metadata")"
    compare_packaged_file "$source_metadata" "support/$metadata_name"
    cmp "$source_metadata" "$MAC_APP_DIR/$metadata_name" >/dev/null || fail "macOS runtime metadata is stale: $metadata_name"
    cmp "$source_metadata" "$WIN_APP_DIR/$metadata_name" >/dev/null || fail "Windows runtime metadata is stale: $metadata_name"
done

cmp "$ROOT_DIR/src/Spyro.Editor.App/Assets/Brand/app-icon.icns" "$MAC_APP/Contents/Resources/AppIcon.icns" >/dev/null || \
    fail "macOS bundle icon differs from the source icon."
cmp "$ROOT_DIR/src/Spyro.Editor.App/Assets/Brand/app-icon.icns" "$MAC_APP_DIR/Assets/Brand/app-icon.icns" >/dev/null || \
    fail "macOS runtime ICNS differs from the source icon."
cmp "$ROOT_DIR/src/Spyro.Editor.App/Assets/Brand/app-icon.ico" "$WIN_APP_DIR/Assets/Brand/app-icon.ico" >/dev/null || \
    fail "Windows runtime ICO differs from the source icon."
for gem_icon in gem-red.png gem-green.png gem-blue.png gem-yellow.png gem-purple.png; do
    SOURCE_GEM_ICON="$ROOT_DIR/src/Spyro.Editor.App/Assets/MobyIcons/$gem_icon"
    require_file "$SOURCE_GEM_ICON"
    require_file "$MAC_APP_DIR/Assets/MobyIcons/$gem_icon"
    require_file "$WIN_APP_DIR/Assets/MobyIcons/$gem_icon"
    cmp "$SOURCE_GEM_ICON" "$MAC_APP_DIR/Assets/MobyIcons/$gem_icon" >/dev/null || \
        fail "macOS runtime gem icon differs from the Spyro 2-derived source asset: $gem_icon"
    cmp "$SOURCE_GEM_ICON" "$WIN_APP_DIR/Assets/MobyIcons/$gem_icon" >/dev/null || \
        fail "Windows runtime gem icon differs from the Spyro 2-derived source asset: $gem_icon"
done

TEMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/spyro-editor-package-verify.XXXXXX")"
trap 'rm -rf "$TEMP_DIR"' EXIT
mkdir -p "$TEMP_DIR/mac" "$TEMP_DIR/win"
if command -v ditto >/dev/null 2>&1; then
    ditto -x -k "$MAC_ZIP" "$TEMP_DIR/mac"
else
    unzip -q "$MAC_ZIP" -d "$TEMP_DIR/mac"
fi
unzip -q "$WIN_ZIP" -d "$TEMP_DIR/win"

MAC_ROUNDTRIP_DIR="$TEMP_DIR/mac/$RELEASE_NAME-osx-arm64"
WIN_ROUNDTRIP_DIR="$TEMP_DIR/win/$RELEASE_NAME-win-x64"
require_directory "$MAC_ROUNDTRIP_DIR"
require_directory "$WIN_ROUNDTRIP_DIR"
validate_package_tree "$MAC_ROUNDTRIP_DIR"
validate_package_tree "$WIN_ROUNDTRIP_DIR"
validate_mac_xattrs "$MAC_ROUNDTRIP_DIR" || fail "macOS archive roundtrip contains non-signature extended attributes."
diff -qr "$MAC_DIR" "$MAC_ROUNDTRIP_DIR" >/dev/null || fail "macOS archive roundtrip differs from its built package directory."
diff -qr "$WIN_DIR" "$WIN_ROUNDTRIP_DIR" >/dev/null || fail "Windows archive roundtrip differs from its built package directory."
[[ -x "$MAC_ROUNDTRIP_DIR/Spyro Editor.app/Contents/MacOS/Spyro.Editor.App" ]] || \
    fail "macOS archive lost the app executable bit."
validate_mac_distribution "$MAC_ROUNDTRIP_DIR/Spyro Editor.app" "Archive-roundtrip"

echo "Package verification passed for $RELEASE_NAME."
shasum -a 256 "$MAC_ZIP" "$WIN_ZIP"
if [[ "$(uname -s)" == "Darwin" ]]; then
    stat -f '%N|%z' "$MAC_ZIP" "$WIN_ZIP"
else
    stat -c '%n|%s' "$MAC_ZIP" "$WIN_ZIP"
fi

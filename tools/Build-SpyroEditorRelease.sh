#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
DIST_DIR="$ROOT_DIR/dist/release"
APP_PROJECT="$ROOT_DIR/src/Spyro.Editor.App/Spyro.Editor.App.csproj"
PROJECT_VERSION="$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' "$APP_PROJECT" | head -n 1)"
BETA_RELEASE_NUMBER="$(sed -n 's:.*<BetaReleaseNumber>\([^<]*\)</BetaReleaseNumber>.*:\1:p' "$APP_PROJECT" | head -n 1)"
[[ "$PROJECT_VERSION" =~ ^([0-9]+\.[0-9]+\.[0-9]+)-beta\.([0-9]+)$ ]] || {
    echo "Unsupported app version '$PROJECT_VERSION'; expected X.Y.Z-beta.N." >&2
    exit 1
}
BASE_VERSION="${BASH_REMATCH[1]}"
BUILD_NUMBER="${BASH_REMATCH[2]}"
[[ "$BETA_RELEASE_NUMBER" =~ ^[1-9][0-9]*$ ]] || {
    echo "Unsupported public beta release '$BETA_RELEASE_NUMBER'; expected a positive integer." >&2
    exit 1
}
PUBLIC_RELEASE_NAME="Spyro Editor Beta V$BETA_RELEASE_NUMBER"
PUBLIC_RELEASE_SLUG="SpyroEditor-Beta-V$BETA_RELEASE_NUMBER"
RELEASE_NAME="${1:-$PUBLIC_RELEASE_SLUG}"

mkdir -p "$DIST_DIR"

is_research_build() {
    [[ "$RELEASE_NAME" == *research* ]]
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

Start with Launch Spyro Editor Research.

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

Start with Launch Spyro Editor.

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
    else
        cat > "$package_dir/Launch Spyro Editor.command" <<'LAUNCHER'
#!/bin/zsh
set -e
cd "$(dirname "$0")"
export SPYRO_EDITOR_INSTALL_ROOT="$PWD"
export SPYRO_EDITOR_RELEASE=1
"./Spyro Editor.app/Contents/MacOS/Spyro.Editor.App"
LAUNCHER
        chmod +x "$package_dir/Launch Spyro Editor.command"
    fi
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
    <string>$BETA_RELEASE_NUMBER.0</string>
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
        write_mac_app_bundle "$package_dir"
        write_mac_launcher "$package_dir"
        if command -v xattr >/dev/null 2>&1; then
            # Strip provenance, quarantine, Finder, and other machine-local metadata
            # before codesign adds only the signature attributes it needs.
            xattr -cr "$package_dir"
        fi
        if command -v codesign >/dev/null 2>&1; then
            codesign --force --deep --sign - "$package_dir/Spyro Editor.app"
        fi
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
        # Preserve the ad-hoc signature's extended attributes through ZIP roundtrip.
        (cd "$DIST_DIR" && ditto -c -k --sequesterRsrc --keepParent "$RELEASE_NAME-$rid" "$RELEASE_NAME-$rid.zip")
    else
        (cd "$DIST_DIR" && zip -qr "$RELEASE_NAME-$rid.zip" "$RELEASE_NAME-$rid")
    fi
    echo "Wrote $package_dir"
    echo "Wrote $zip_path"
}

dotnet clean "$APP_PROJECT" --configuration "$CONFIGURATION"
dotnet build "$APP_PROJECT" --configuration "$CONFIGURATION"

publish_release_package "osx-arm64"
publish_release_package "win-x64"

if ! is_research_build && [[ -x "$ROOT_DIR/tools/Verify-SpyroEditorRelease.sh" ]]; then
    echo
    "$ROOT_DIR/tools/Verify-SpyroEditorRelease.sh" "$RELEASE_NAME"
fi

echo
echo "Release packages are in $DIST_DIR"

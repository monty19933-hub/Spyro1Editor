#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
RELEASE_NAME="${1:-SpyroEditor-release}"
DIST_DIR="$ROOT_DIR/dist/release"
APP_PROJECT="$ROOT_DIR/src/Spyro.Editor.App/Spyro.Editor.App.csproj"

mkdir -p "$DIST_DIR"

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

    cat > "$package_dir/README.txt" <<'README'
Spyro Editor release build

Start with Launch Spyro Editor.

Use Open BIN/CUE inside the editor and choose your own Spyro the Dragon disc
image. The editor rebuilds terrain maps and object placement from that selected
disc image, then writes patched test BIN/CUE output to the output folder.

This package does not include game data, BIOS files, emulator files, RAM dumps,
patched discs, or extracted assets.

Only the cleaner release workflow is exposed here. Research tools, candidate
exporters, terrain beta controls, smoke tests, and Spring Chest import experiments
are intentionally hidden from this build.

More detail:
  support/docs/release-user-guide.md
  support/docs/known-limitations.md
README
}

write_mac_launcher() {
    local package_dir="$1"
    cat > "$package_dir/Launch Spyro Editor.command" <<'LAUNCHER'
#!/bin/zsh
set -e
cd "$(dirname "$0")"
mkdir -p output
open "./Spyro Editor.app"
LAUNCHER
    chmod +x "$package_dir/Launch Spyro Editor.command"
}

write_mac_app_bundle() {
    local package_dir="$1"
    local app_bundle="$package_dir/Spyro Editor.app"
    local contents_dir="$app_bundle/Contents"
    local macos_dir="$contents_dir/MacOS"
    local resources_dir="$contents_dir/Resources"
    mkdir -p "$macos_dir" "$resources_dir"

    cp "$ROOT_DIR/src/Spyro.Editor.App/Assets/Brand/app-icon.icns" "$resources_dir/AppIcon.icns"

    cat > "$contents_dir/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleDisplayName</key>
    <string>Spyro Editor</string>
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
    <string>1.0</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST

    find "$package_dir/support/app" -mindepth 1 -maxdepth 1 -exec mv {} "$macos_dir/" \;
    chmod +x "$macos_dir/Spyro.Editor.App"
}

write_windows_launcher() {
    local package_dir="$1"
    cat > "$package_dir/Launch Spyro Editor.bat" <<'LAUNCHER'
@echo off
setlocal
cd /d "%~dp0"
if not exist "%~dp0output" mkdir "%~dp0output"
set "SPYRO_EDITOR_WORKSPACE=%CD%"
set "SPYRO_EDITOR_RELEASE=1"
"%~dp0support\app\Spyro.Editor.App.exe"
LAUNCHER
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
        --output "$package_dir/support/app"

    copy_release_files "$package_dir"
    if [[ "$rid" == osx-* ]]; then
        write_mac_app_bundle "$package_dir"
        write_mac_launcher "$package_dir"
    elif [[ "$rid" == win-* ]]; then
        write_windows_launcher "$package_dir"
    fi

    (cd "$DIST_DIR" && zip -qr "$RELEASE_NAME-$rid.zip" "$RELEASE_NAME-$rid")
    echo "Wrote $package_dir"
    echo "Wrote $zip_path"
}

dotnet build "$APP_PROJECT" --configuration "$CONFIGURATION"

publish_release_package "osx-arm64"
publish_release_package "win-x64"

echo
echo "Release packages are in $DIST_DIR"

#!/usr/bin/env bash
# Repackages a stable release's packages as the whole-number beta bridge that
# installed Beta V2-V5 updaters can find. Those updaters read only beta
# prereleases named "Spyro Editor Beta VN" with schema-1 manifests, so a stable
# release alone is never offered to them.
#
# The app inside is the stable build, unchanged (the Mac app keeps its
# signature). Its assembly already carries the bridge's legacy identity
# (SpyroEditorBetaRelease, SpyroEditorPublicReleaseVersion and
# SpyroEditorReleaseManifestSchema=1 from the project file), which is what the
# old updaters compare with the manifest. Once opened it is the stable release
# and checks the stable channel from then on.
#
# Usage: DIST_DIR=/folder/with/stable/packages tools/Build-SpyroEditorBetaBridge.sh
# The stable package folders (SpyroEditor-V<stable>-<rid>) must already be in
# DIST_DIR; the bridge folders and zips are written next to them.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP_PROJECT="$ROOT_DIR/src/Spyro.Editor.App/Spyro.Editor.App.csproj"
DIST_DIR="${DIST_DIR:?Set DIST_DIR to the folder holding the stable packages.}"
RIDS="${SPYRO_EDITOR_BRIDGE_RIDS:-osx-arm64 win-x64}"

property() { sed -n "s:.*<$1>\([^<]*\)</$1>.*:\1:p" "$APP_PROJECT" | head -n 1; }
BETA_RELEASE_NUMBER="$(property BetaReleaseNumber)"
PUBLIC_RELEASE_VERSION="$(property PublicReleaseVersion)"
RELEASE_MANIFEST_SCHEMA_VERSION="$(property ReleaseManifestSchemaVersion)"
PREVIOUS_PUBLIC_RELEASE_VERSION="$(property PreviousPublicReleaseVersion)"
source "$ROOT_DIR/tools/Read-ReleaseVersion.sh" "$APP_PROJECT"
STABLE_VERSION="${SPYRO_EDITOR_STABLE_VERSION:-$SOURCE_STABLE_VERSION}"
STABLE_INTERNAL_VERSION="${SPYRO_EDITOR_STABLE_INTERNAL_VERSION:-$SOURCE_STABLE_INTERNAL_VERSION}"

fail() { echo "$*" >&2; exit 1; }
[[ "$BETA_RELEASE_NUMBER" =~ ^[1-9][0-9]*$ ]] || fail "BetaReleaseNumber '$BETA_RELEASE_NUMBER' is not a positive integer."
[[ "$RELEASE_MANIFEST_SCHEMA_VERSION" == "1" && "$PUBLIC_RELEASE_VERSION" == "$BETA_RELEASE_NUMBER" ]] ||
    fail "A bridge needs ReleaseManifestSchemaVersion=1 and PublicReleaseVersion=BetaReleaseNumber (have schema $RELEASE_MANIFEST_SCHEMA_VERSION, public $PUBLIC_RELEASE_VERSION, beta $BETA_RELEASE_NUMBER)."
[[ "$STABLE_VERSION" =~ ^[1-9][0-9]*\.(0|[1-9][0-9]*)(\.[1-9][0-9]*)?$ ]] || fail "Stable version '$STABLE_VERSION' is not canonical."

BRIDGE_NAME="Spyro Editor Beta V$BETA_RELEASE_NUMBER"
BRIDGE_SLUG="SpyroEditor-Beta-V$BETA_RELEASE_NUMBER"
STABLE_NAME="Spyro Editor $STABLE_VERSION"
STABLE_SLUG="SpyroEditor-V$STABLE_VERSION"
CHANGELOG="$ROOT_DIR/docs/beta-v$BETA_RELEASE_NUMBER-bridge-changelog.md"

# The installed updaters compare the packaged CHANGELOG.md with the GitHub
# release body; publish this same file as the bridge release's notes.
[[ -f "$CHANGELOG" ]] || fail "Missing bridge changelog: $CHANGELOG"
[[ "$(sed -n '1p' "$CHANGELOG")" == "# $BRIDGE_NAME" ]] || fail "$CHANGELOG must begin with '# $BRIDGE_NAME'."
[[ "$(sed -n '3p' "$CHANGELOG")" == "## Changes since Spyro Editor Beta V$PREVIOUS_PUBLIC_RELEASE_VERSION" ]] ||
    fail "$CHANGELOG line 3 must be '## Changes since Spyro Editor Beta V$PREVIOUS_PUBLIC_RELEASE_VERSION'."

for rid in $RIDS; do
    case "$rid" in
        osx-arm64) app_dll="Spyro Editor.app/Contents/MacOS/Spyro.Editor.App.dll" ;;
        win-x64) app_dll="support/app/Spyro.Editor.App.dll" ;;
        *) fail "No bridge layout for '$rid'." ;;
    esac
    stable_dir="$DIST_DIR/$STABLE_SLUG-$rid"
    bridge_dir="$DIST_DIR/$BRIDGE_SLUG-$rid"
    bridge_zip="$bridge_dir.zip"
    [[ -d "$stable_dir" ]] || fail "Missing stable package folder: $stable_dir"
    [[ ! -e "$bridge_dir" && ! -e "$bridge_zip" ]] || fail "Refusing to replace an existing bridge package: $bridge_dir"
    [[ -s "$stable_dir/$app_dll" ]] || fail "Stable package has no $app_dll."
    python3 - "$stable_dir/release-manifest.json" "$STABLE_VERSION" "$STABLE_INTERNAL_VERSION" "$rid" <<'PY'
import json,sys
path,version,internal,rid=sys.argv[1:]
m=json.load(open(path,encoding='utf-8'))
expected={"schemaVersion":3,"channel":"stable","publicVersion":version,"displayName":f"Spyro Editor {version}","internalVersion":internal,"platform":rid}
if m!=expected:raise SystemExit(f"{path} is not the stable {version} manifest for {rid}: {m}")
PY
    [[ "$(sed -n '1p' "$stable_dir/README.txt")" == "$STABLE_NAME" ]] || fail "$stable_dir/README.txt does not begin with '$STABLE_NAME'."

    # ditto keeps the Mac app's signature, extended attributes and modes.
    ditto "$stable_dir" "$bridge_dir"
    {
        printf '%s\n' "$BRIDGE_NAME"
        printf 'Internal build: %s\n\n' "$STABLE_INTERNAL_VERSION"
        cat <<README
This is $STABLE_NAME, the stable release, packaged so that the update check in
Spyro Editor Beta V2 to V$PREVIOUS_PUBLIC_RELEASE_VERSION can find it. Use it in place of your beta; your
projects stay in Documents/Spyro Editor. From now on the editor looks for
stable releases instead of betas.

README
        sed '1,2d' "$stable_dir/README.txt" | sed '1{/^$/d;}'
    } > "$bridge_dir/README.txt"
    cp "$CHANGELOG" "$bridge_dir/CHANGELOG.md"
    # The exact schema-1 shape the installed V2 clients read; add no fields.
    cat > "$bridge_dir/release-manifest.json" <<JSON
{
  "schemaVersion": 1,
  "channel": "beta",
  "publicBeta": $BETA_RELEASE_NUMBER,
  "displayName": "$BRIDGE_NAME",
  "internalVersion": "$STABLE_INTERNAL_VERSION",
  "platform": "$rid"
}
JSON
    cmp -s "$stable_dir/$app_dll" "$bridge_dir/$app_dll" || fail "The bridge's app differs from the stable package's."
    if [[ "$rid" == osx-* ]]; then
        codesign --verify --deep --strict "$bridge_dir/Spyro Editor.app"
        (cd "$DIST_DIR" && ditto -c -k --sequesterRsrc --keepParent "$BRIDGE_SLUG-$rid" "$BRIDGE_SLUG-$rid.zip")
    else
        (cd "$DIST_DIR" && zip -qr "$BRIDGE_SLUG-$rid.zip" "$BRIDGE_SLUG-$rid")
    fi
    printf '%s  %s\n' "$(shasum -a 256 "$bridge_zip" | cut -d' ' -f1)" "$(basename "$bridge_zip")"
done
echo "Publish as prerelease '$BRIDGE_NAME', tag beta-v$BETA_RELEASE_NUMBER, notes from $CHANGELOG."

#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
APP_PROJECT="$ROOT_DIR/src/Spyro.Editor.App/Spyro.Editor.App.csproj"
SMOKE_PROJECT="$ROOT_DIR/src/Spyro.Editor.Smoke/Spyro.Editor.Smoke.csproj"
CACHE_TOOL_PROJECT="$ROOT_DIR/src/Spyro.Editor.CacheTool/Spyro.Editor.CacheTool.csproj"
UI_SMOKE_PROJECT="$ROOT_DIR/src/Spyro.Editor.UiSmoke/Spyro.Editor.UiSmoke.csproj"
PLAYABLE_TERRAIN_VIEW_SMOKE_PROJECT="$ROOT_DIR/src/Spyro.Editor.PlayableTerrainViewSmoke/Spyro.Editor.PlayableTerrainViewSmoke.csproj"
IDENTITY_SMOKE_PROJECT="$ROOT_DIR/src/Spyro.Editor.IdentitySmoke/Spyro.Editor.IdentitySmoke.csproj"
MOBY_ICON_SMOKE_PROJECT="$ROOT_DIR/src/Spyro.Editor.MobyIconSmoke/Spyro.Editor.MobyIconSmoke.csproj"
PSX_BLEND_SMOKE_PROJECT="$ROOT_DIR/src/Spyro.Editor.PsxBlendSmoke/Spyro.Editor.PsxBlendSmoke.csproj"
GTE_PROJECTION_SMOKE_PROJECT="$ROOT_DIR/src/Spyro.Editor.GteProjectionSmoke/Spyro.Editor.GteProjectionSmoke.csproj"
NATIVE_TERRAIN_BASE_PROJECTION_SMOKE_PROJECT="$ROOT_DIR/src/Spyro.Editor.NativeTerrainBaseProjectionSmoke/Spyro.Editor.NativeTerrainBaseProjectionSmoke.csproj"
HQ_MATERIAL_SMOKE_PROJECT="$ROOT_DIR/src/Spyro.Editor.HqMaterialCacheSmoke/Spyro.Editor.HqMaterialCacheSmoke.csproj"
PERSISTENCE_SMOKE_PROJECT="$ROOT_DIR/src/Spyro.Editor.PersistenceSmoke/Spyro.Editor.PersistenceSmoke.csproj"
UPDATE_SMOKE_PROJECT="$ROOT_DIR/src/Spyro.Editor.UpdateSmoke/Spyro.Editor.UpdateSmoke.csproj"
STONEHILL_LEVEL_REPLACEMENT_SMOKE_PROJECT="$ROOT_DIR/src/Spyro.Editor.StoneHillLevelReplacementBaselineSmoke/Spyro.Editor.StoneHillLevelReplacementBaselineSmoke.csproj"
STONEHILL_TOWNSQUARE_IDENTITY_SMOKE_PROJECT="$ROOT_DIR/src/Spyro.Editor.StoneHillTownSquareIdentityCandidateSmoke/Spyro.Editor.StoneHillTownSquareIdentityCandidateSmoke.csproj"
STONEHILL_TOWNSQUARE_EDITED_DONOR_SMOKE_PROJECT="$ROOT_DIR/src/Spyro.Editor.StoneHillTownSquareEditedDonorCandidateSmoke/Spyro.Editor.StoneHillTownSquareEditedDonorCandidateSmoke.csproj"
TERRAIN_TEXTURE_PROOF_PROJECTS=(
    "$ROOT_DIR/src/Spyro.Editor.TerrainCatalogSmoke/Spyro.Editor.TerrainCatalogSmoke.csproj"
    "$ROOT_DIR/src/Spyro.Editor.TerrainRelocationStoreSmoke/Spyro.Editor.TerrainRelocationStoreSmoke.csproj"
    "$ROOT_DIR/src/Spyro.Editor.TextureRuntimeControlSmoke/Spyro.Editor.TextureRuntimeControlSmoke.csproj"
    "$ROOT_DIR/src/Spyro.Editor.TextureOwnershipSmoke/Spyro.Editor.TextureOwnershipSmoke.csproj"
    "$ROOT_DIR/src/Spyro.Editor.TextureRelocationSmoke/Spyro.Editor.TextureRelocationSmoke.csproj"
    "$ROOT_DIR/src/Spyro.Editor.TextureInPlaceTransplantSmoke/Spyro.Editor.TextureInPlaceTransplantSmoke.csproj"
    "$ROOT_DIR/src/Spyro.Editor.TerrainTextureExportSmoke/Spyro.Editor.TerrainTextureExportSmoke.csproj"
    "$ROOT_DIR/src/Spyro.Editor.SurfaceFlagSmoke/Spyro.Editor.SurfaceFlagSmoke.csproj"
    "$ROOT_DIR/src/Spyro.Editor.SurfaceLayoutSmoke/Spyro.Editor.SurfaceLayoutSmoke.csproj"
)
SOURCE_SETTINGS="$ROOT_DIR/_local/settings/source-disc.json"
WAD_ANALYSIS="$ROOT_DIR/spyro-wad-analysis.json"
CACHE_DIR="$ROOT_DIR/editor-cache"
PROOF_BIN_DIR="$ROOT_DIR/_local/objects/control-role-proof-bins"

BUILD_ONLY=0
WITH_GRADE_WRITE_READBACK=0
STONEHILL_LEVEL_REPLACEMENT_BASELINE_SMOKE_ONLY=0
STONEHILL_TOWNSQUARE_IDENTITY_CANDIDATE_SMOKE_ONLY=0
STONEHILL_TOWNSQUARE_EDITED_DONOR_CANDIDATE_SMOKE_ONLY=0

usage() {
    cat <<'USAGE'
Usage: tools/Run-SpyroEditorOfflineQa.sh [options]

Builds the Spyro Editor projects in Release configuration and runs a fixed,
offline QA suite. UI checks render in memory; no visible app or DuckStation
window is started.

Default smoke modes:
  identity coverage audit      all 35 caches, stale-cache guards, and precise/
                               generic/unresolved name accounting
  --generic-identity-signature-smoke-only
                               native-class identity guards plus source-signature
                               evidence boundaries for remaining broad names
  --identity-deep-audit-smoke-only
                               refreshes live-test workbench/report artifacts and
                               canonical v2 identity-observation fingerprints
  --visual-category-smoke-only
                               whole-catalog object-category cleanliness plus
                               exact scenery, aircraft, chest, and target guards
  --gem-value-smoke-only       five paired native gem encodings, all-level
                               treasure totals, persistence, and copied-BIN readback
  --placement-smoke-only       placement math and terrain snapping
  --release-link-smoke-only    saved link/companion round trips
  --portal-source-smoke-only   all native portal tables, index bias, move plans,
                               collision surfaces, and trigger triangles
  --cross-level-swap-catalog-smoke-only
                               all-level catalogue and existing-slot guard
  --moby-build-safety-smoke-only
                               Stable/Review/Blocked build-safety classification
  --linked-portal-sky-safety-smoke-only
                               blocks oversized child skies before they expand a
                               linked homeworld portal copy or write a BIN/CUE
  --level-text-smoke-only      all 35 level-name slots and longer-name pointers
  --level-music-smoke-only     built-in music choices and copied-BIN bytes
  --diagnostics-smoke-only     crash/interruption reports and metadata-only ZIP
  persistence smoke           external project redirect, non-destructive legacy
                               migration, conflict preservation, and snapshots
  update smoke                GitHub beta selection and SHA-256/tamper rejection
  Moby raster-icon audit      all 35 caches, atlas contracts/matching, and exact
                               generic-marker coverage
  PSX terrain blend smoke     exhaustive RGB5, STP, ABR, and material-byte rules
  PS1 GTE projection smoke    explicit-register RTPS/RTPT, UNR reciprocal,
                               SXY/SZ FIFOs, and FLAG saturation rules
  native terrain base smoke   explicit raw/simple/full HP queue contexts,
                               near-SXY rerun, CPU outcodes, and raw-slot NCLIP
  raw HQ material cache       all 35 native-load material sidecars, descriptor
                               shapes, raw PSX555/STP words, hashes, and tamper guards
  native Map material smoke  all 35 top-down editor maps use native normal-HQ
                               tiles plus the physical table-2 overview colors
  classifier regression      dormant/non-shipping broad-sheet research remains
                               deterministic without changing source geometry
  --terrain-scene-view-only  shipping Complete Scene keeps every captured face
                               visible/editable in Edit Map and Game Camera
  --terrain-fly-game-view-only
                               all 35 local Game View culling/render checks
  terrain texture proof suite all-level catalog/runtime/ownership coverage,
                               relocation and in-place byte proofs, surface
                               descriptor growth, properties, and readback guards
  --object-smoke-only          object QA with isolated, cleaned disc readbacks

Options:
  --build-only
      Build the app, smoke runner, cache tool, identity/icon audits, PSX blend,
      GTE/base projection, raw-material/classifier proofs, and headless UI
      smoke without tests.

  --with-grade-write-readback
      After the default suite, run the 35-level/1,190-pair environment matrix,
      the Dark Hollow dragon plus extra-object crash guard, and the focused
      environment-grade copied-BIN/readback smoke. These remain offline and
      clean their disposable BIN/CUE files, but temporarily need several
      source-image-sized files of free disk space.

  --stonehill-level-replacement-baseline-smoke-only
      Run only the V5 clean-USA Stone Hill retail-slot manifest and byte-identical
      BIN/CUE baseline proof. Requires the configured clean retail source disc;
      writes no editor-visible level replacement and launches no emulator.

  --stonehill-townsquare-identity-candidate-smoke-only
      Build the pending-runtime V5 Town Square display-identity CUE from the exact
      runtime-proven Stone Hill-slot replacement. Guards the single name-pointer
      sector, completion totals, final hash, and evidence files; launches no emulator.

  --stone-hill-town-square-edited-donor-candidate-smoke
      Build the pending-runtime V5 Town Square T21 X-only control plus the
      isolated +0x50 render-radius diagnostic from the exact runtime-proven
      display-identity control. Both preserve native +0x4A = FF; the diagnostic
      changes render radius only from retail 0x18 to retail 0x20. Guards exact
      rebases, final known hashes, original donor data, evidence, determinism,
      and unsafe-patch rejection; launches no emulator.

  -h, --help
      Show this help.

The default suite deliberately does not pass --export-control-role-proof-bins
or any other proof-disc export switch. Object write/readback checks can use
several GiB temporarily; generated BIN/CUE files are removed and any files
that existed before the run are restored.
USAGE
}

fail() {
    echo "Offline QA error: $*" >&2
    exit 1
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || fail "Required command '$1' was not found."
}

for argument in "$@"; do
    case "$argument" in
        --build-only)
            BUILD_ONLY=1
            ;;
        --with-grade-write-readback)
            WITH_GRADE_WRITE_READBACK=1
            ;;
        --stonehill-level-replacement-baseline-smoke-only)
            STONEHILL_LEVEL_REPLACEMENT_BASELINE_SMOKE_ONLY=1
            ;;
        --stonehill-townsquare-identity-candidate-smoke-only)
            STONEHILL_TOWNSQUARE_IDENTITY_CANDIDATE_SMOKE_ONLY=1
            ;;
        --stone-hill-town-square-edited-donor-candidate-smoke)
            STONEHILL_TOWNSQUARE_EDITED_DONOR_CANDIDATE_SMOKE_ONLY=1
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            usage >&2
            fail "Unknown option '$argument'."
            ;;
    esac
done

if [[ "$BUILD_ONLY" -eq 1 && "$WITH_GRADE_WRITE_READBACK" -eq 1 ]]; then
    fail "--build-only cannot be combined with --with-grade-write-readback."
fi
if [[ "$STONEHILL_LEVEL_REPLACEMENT_BASELINE_SMOKE_ONLY" -eq 1 &&
      ("$BUILD_ONLY" -eq 1 || "$WITH_GRADE_WRITE_READBACK" -eq 1 ||
       "$STONEHILL_TOWNSQUARE_IDENTITY_CANDIDATE_SMOKE_ONLY" -eq 1 ||
       "$STONEHILL_TOWNSQUARE_EDITED_DONOR_CANDIDATE_SMOKE_ONLY" -eq 1) ]]; then
    fail "--stonehill-level-replacement-baseline-smoke-only cannot be combined with another QA mode."
fi
if [[ "$STONEHILL_TOWNSQUARE_IDENTITY_CANDIDATE_SMOKE_ONLY" -eq 1 &&
      ("$BUILD_ONLY" -eq 1 || "$WITH_GRADE_WRITE_READBACK" -eq 1 ||
       "$STONEHILL_TOWNSQUARE_EDITED_DONOR_CANDIDATE_SMOKE_ONLY" -eq 1) ]]; then
    fail "--stonehill-townsquare-identity-candidate-smoke-only cannot be combined with another QA mode."
fi
if [[ "$STONEHILL_TOWNSQUARE_EDITED_DONOR_CANDIDATE_SMOKE_ONLY" -eq 1 &&
      ("$BUILD_ONLY" -eq 1 || "$WITH_GRADE_WRITE_READBACK" -eq 1) ]]; then
    fail "--stone-hill-town-square-edited-donor-candidate-smoke cannot be combined with another QA mode."
fi

require_command dotnet

echo "Spyro Editor offline QA"
echo "Workspace: $ROOT_DIR"
echo "Configuration: $CONFIGURATION"
echo "Safety: compile, command-line smoke, and in-memory UI render only; no visible app or emulator launch."
echo

if [[ "$STONEHILL_LEVEL_REPLACEMENT_BASELINE_SMOKE_ONLY" -eq 1 ]]; then
    echo "Building focused V5 Stone Hill replacement baseline smoke..."
    dotnet build "$STONEHILL_LEVEL_REPLACEMENT_SMOKE_PROJECT" --configuration "$CONFIGURATION" --nologo
    echo
    echo "Running focused V5 Stone Hill replacement baseline smoke..."
    dotnet run --project "$STONEHILL_LEVEL_REPLACEMENT_SMOKE_PROJECT" --configuration "$CONFIGURATION" --no-build -- "$ROOT_DIR"
    exit 0
fi

if [[ "$STONEHILL_TOWNSQUARE_IDENTITY_CANDIDATE_SMOKE_ONLY" -eq 1 ]]; then
    echo "Building focused V5 Town Square display-identity candidate smoke..."
    dotnet build "$STONEHILL_TOWNSQUARE_IDENTITY_SMOKE_PROJECT" --configuration "$CONFIGURATION" --nologo
    echo
    echo "Running focused V5 Town Square display-identity candidate smoke..."
    dotnet run --project "$STONEHILL_TOWNSQUARE_IDENTITY_SMOKE_PROJECT" --configuration "$CONFIGURATION" --no-build -- "$ROOT_DIR"
    exit 0
fi

if [[ "$STONEHILL_TOWNSQUARE_EDITED_DONOR_CANDIDATE_SMOKE_ONLY" -eq 1 ]]; then
    echo "Building focused V5 edited Town Square donor X-only and render-radius candidate smoke..."
    dotnet build "$STONEHILL_TOWNSQUARE_EDITED_DONOR_SMOKE_PROJECT" --configuration "$CONFIGURATION" --nologo
    echo
    echo "Running focused V5 edited Town Square donor X-only and render-radius candidate smoke..."
    dotnet run --project "$STONEHILL_TOWNSQUARE_EDITED_DONOR_SMOKE_PROJECT" --configuration "$CONFIGURATION" --no-build -- "$ROOT_DIR"
    exit 0
fi

echo "[1/14] Building Avalonia app (compile only)..."
dotnet build "$APP_PROJECT" --configuration "$CONFIGURATION" --nologo

echo
echo "[2/14] Building command-line smoke runner..."
dotnet build "$SMOKE_PROJECT" --configuration "$CONFIGURATION" --nologo

echo
echo "[3/14] Building cache tool..."
dotnet build "$CACHE_TOOL_PROJECT" --configuration "$CONFIGURATION" --nologo

echo
echo "[4/14] Building identity coverage audit..."
dotnet build "$IDENTITY_SMOKE_PROJECT" --configuration "$CONFIGURATION" --nologo

echo
echo "[5/14] Building headless UI smoke..."
dotnet build "$UI_SMOKE_PROJECT" --configuration "$CONFIGURATION" --nologo

echo
echo "[6/14] Building dormant broad-sheet classifier regression..."
dotnet build "$PLAYABLE_TERRAIN_VIEW_SMOKE_PROJECT" --configuration "$CONFIGURATION" --nologo

echo
echo "[7/14] Building project persistence smoke..."
dotnet build "$PERSISTENCE_SMOKE_PROJECT" --configuration "$CONFIGURATION" --nologo

echo
echo "[8/14] Building update integrity smoke..."
dotnet build "$UPDATE_SMOKE_PROJECT" --configuration "$CONFIGURATION" --nologo

echo
echo "[9/14] Building terrain texture/property proof smokes..."
for project in "${TERRAIN_TEXTURE_PROOF_PROJECTS[@]}"; do
    dotnet build "$project" --configuration "$CONFIGURATION" --nologo
done

echo
echo "[10/14] Building all-level Moby raster-icon audit..."
dotnet build "$MOBY_ICON_SMOKE_PROJECT" --configuration "$CONFIGURATION" --nologo

echo
echo "[11/14] Building exact PSX terrain blend-kernel smoke..."
dotnet build "$PSX_BLEND_SMOKE_PROJECT" --configuration "$CONFIGURATION" --nologo

echo
echo "[12/14] Building exact PS1 GTE RTPS/RTPT projection smoke..."
dotnet build "$GTE_PROJECTION_SMOKE_PROJECT" --configuration "$CONFIGURATION" --nologo

echo
echo "[13/14] Building native terrain HP base-projection smoke..."
dotnet build "$NATIVE_TERRAIN_BASE_PROJECTION_SMOKE_PROJECT" --configuration "$CONFIGURATION" --nologo

echo
echo "[14/14] Building raw PSX HQ terrain-material cache smoke..."
dotnet build "$HQ_MATERIAL_SMOKE_PROJECT" --configuration "$CONFIGURATION" --nologo

if [[ "$BUILD_ONLY" -eq 1 ]]; then
    echo
    echo "Offline QA build-only mode passed. No app or smoke executable was started."
    exit 0
fi

echo
echo "Running project persistence smoke..."
dotnet run --project "$PERSISTENCE_SMOKE_PROJECT" --configuration "$CONFIGURATION" --no-build

echo
echo "Running update integrity smoke..."
dotnet run --project "$UPDATE_SMOKE_PROJECT" --configuration "$CONFIGURATION" --no-build

echo
echo "Running exact PSX terrain blend-kernel smoke..."
dotnet run --project "$PSX_BLEND_SMOKE_PROJECT" --configuration "$CONFIGURATION" --no-build

echo
echo "Running exact PS1 GTE RTPS/RTPT projection smoke..."
dotnet run --project "$GTE_PROJECTION_SMOKE_PROJECT" --configuration "$CONFIGURATION" --no-build

echo
echo "Running native terrain HP base-projection smoke..."
dotnet run --project "$NATIVE_TERRAIN_BASE_PROJECTION_SMOKE_PROJECT" --configuration "$CONFIGURATION" --no-build

echo
echo "Running raw PSX HQ terrain-material cache smoke..."
dotnet run --project "$HQ_MATERIAL_SMOKE_PROJECT" --configuration "$CONFIGURATION" --no-build -- "$ROOT_DIR"

echo
echo "Running dormant/non-shipping broad-sheet classifier regression..."
dotnet run --project "$PLAYABLE_TERRAIN_VIEW_SMOKE_PROJECT" --configuration "$CONFIGURATION" --no-build -- "$ROOT_DIR"

require_command python3

[[ -f "$SOURCE_SETTINGS" ]] || fail "Missing source-disc settings: $SOURCE_SETTINGS. Select a clean BIN/CUE in the editor once, or create this local settings file before running source-aware QA."
[[ -f "$WAD_ANALYSIS" ]] || fail "Missing WAD analysis cache: $WAD_ANALYSIS. Rebuild the local source-disc analysis before running offline QA."
[[ -d "$CACHE_DIR" ]] || fail "Missing editor cache directory: $CACHE_DIR. Build or link the local editor cache before running object smoke tests."

SOURCE_PATHS="$(python3 - "$SOURCE_SETTINGS" <<'PY'
import json
import sys

settings_path = sys.argv[1]
try:
    with open(settings_path, "r", encoding="utf-8") as stream:
        settings = json.load(stream)
except (OSError, json.JSONDecodeError) as error:
    print(f"Could not read source-disc settings: {error}", file=sys.stderr)
    raise SystemExit(1)

image_path = settings.get("imagePath")
cue_path = settings.get("cuePath")
if not isinstance(image_path, str) or not image_path.strip():
    print("source-disc.json does not contain a non-empty imagePath.", file=sys.stderr)
    raise SystemExit(1)
if not isinstance(cue_path, str) or not cue_path.strip():
    print("source-disc.json does not contain a non-empty cuePath.", file=sys.stderr)
    raise SystemExit(1)

print(f"{image_path}\t{cue_path}")
PY
)" || fail "Source-disc settings are invalid: $SOURCE_SETTINGS"

IFS=$'\t' read -r SOURCE_IMAGE SOURCE_CUE <<< "$SOURCE_PATHS"
[[ -n "$SOURCE_IMAGE" && -n "$SOURCE_CUE" ]] || fail "Source-disc settings did not resolve both imagePath and cuePath."
[[ -f "$SOURCE_IMAGE" ]] || fail "Configured source BIN is missing: $SOURCE_IMAGE"
[[ -f "$SOURCE_CUE" ]] || fail "Configured source CUE is missing: $SOURCE_CUE"

source_hashes() {
    python3 - "$SOURCE_IMAGE" "$SOURCE_CUE" <<'PY'
import hashlib
import sys

def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as stream:
        for chunk in iter(lambda: stream.read(8 * 1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()

print(f"{sha256(sys.argv[1])}\t{sha256(sys.argv[2])}")
PY
}

if ! SOURCE_HASHES_BEFORE="$(source_hashes)"; then
    fail "Could not hash the configured source BIN/CUE before offline QA."
fi
IFS=$'\t' read -r SOURCE_IMAGE_SHA256_BEFORE SOURCE_CUE_SHA256_BEFORE <<< "$SOURCE_HASHES_BEFORE"
[[ "$SOURCE_IMAGE_SHA256_BEFORE" =~ ^[0-9a-f]{64}$ && "$SOURCE_CUE_SHA256_BEFORE" =~ ^[0-9a-f]{64}$ ]] || \
    fail "The source BIN/CUE hash result was incomplete or invalid."

verify_source_hashes() {
    local hashes_after
    local image_hash_after
    local cue_hash_after
    if ! hashes_after="$(source_hashes)"; then
        echo "Offline QA error: Could not hash the configured source BIN/CUE during cleanup." >&2
        return 1
    fi
    IFS=$'\t' read -r image_hash_after cue_hash_after <<< "$hashes_after"
    if [[ ! "$image_hash_after" =~ ^[0-9a-f]{64}$ || ! "$cue_hash_after" =~ ^[0-9a-f]{64}$ ]]; then
        echo "Offline QA error: The source BIN/CUE cleanup hash result was incomplete or invalid." >&2
        return 1
    fi
    if [[ "$image_hash_after" != "$SOURCE_IMAGE_SHA256_BEFORE" ]]; then
        echo "Offline QA error: The source BIN changed during offline QA: before $SOURCE_IMAGE_SHA256_BEFORE, after $image_hash_after." >&2
        return 1
    fi
    if [[ "$cue_hash_after" != "$SOURCE_CUE_SHA256_BEFORE" ]]; then
        echo "Offline QA error: The source CUE changed during offline QA: before $SOURCE_CUE_SHA256_BEFORE, after $cue_hash_after." >&2
        return 1
    fi
}

LEVEL_KEYS="$(python3 - "$ROOT_DIR/spyro-level-catalog.json" <<'PY'
import json
import sys

catalog_path = sys.argv[1]
try:
    with open(catalog_path, "r", encoding="utf-8") as stream:
        catalog = json.load(stream)
except (OSError, json.JSONDecodeError) as error:
    print(f"Could not read level catalog: {error}", file=sys.stderr)
    raise SystemExit(1)

levels = catalog.get("levels")
if not isinstance(levels, list) or not levels:
    print("Level catalog has no levels array.", file=sys.stderr)
    raise SystemExit(1)

for level in levels:
    key = level.get("key") if isinstance(level, dict) else None
    if isinstance(key, str) and key:
        print(key)
PY
)" || fail "The level catalog is missing or invalid: $ROOT_DIR/spyro-level-catalog.json"

MISSING_CACHE_KEYS=()
CACHE_LEVEL_COUNT=0
while IFS= read -r level_key; do
    [[ -n "$level_key" ]] || continue
    CACHE_LEVEL_COUNT=$((CACHE_LEVEL_COUNT + 1))
    if [[ ! -f "$CACHE_DIR/$level_key-mobys.json" ]]; then
        MISSING_CACHE_KEYS+=("$level_key")
    fi
done <<< "$LEVEL_KEYS"

if [[ "${#MISSING_CACHE_KEYS[@]}" -ne 0 ]]; then
    fail "Editor cache is incomplete; missing moby caches for: ${MISSING_CACHE_KEYS[*]}. Cache directory: $CACHE_DIR"
fi

echo
echo "Source BIN: $SOURCE_IMAGE"
echo "Source CUE: $SOURCE_CUE"
echo "Source BIN SHA-256: $SOURCE_IMAGE_SHA256_BEFORE"
echo "Source CUE SHA-256: $SOURCE_CUE_SHA256_BEFORE"
echo "WAD analysis: $WAD_ANALYSIS"
echo "Editor cache: $CACHE_DIR ($CACHE_LEVEL_COUNT/$CACHE_LEVEL_COUNT level moby caches)"

run_smoke() {
    local label="$1"
    shift
    echo
    echo "Running $label ($*)..."
    dotnet run \
        --project "$SMOKE_PROJECT" \
        --configuration "$CONFIGURATION" \
        --no-build \
        -- \
        "$ROOT_DIR" \
        "$@"
}

PROOF_GUARD="$(mktemp "${TMPDIR:-/tmp}/spyro-editor-offline-qa.XXXXXX")"
HASH_GUARD_VERIFIED=0
cleanup_offline_qa() {
    local status=$?
    rm -f "$PROOF_GUARD"
    if [[ "$HASH_GUARD_VERIFIED" -eq 0 ]] && ! verify_source_hashes; then
        status=1
    fi
    trap - EXIT
    exit "$status"
}
trap cleanup_offline_qa EXIT

for project in "${TERRAIN_TEXTURE_PROOF_PROJECTS[@]}"; do
    project_name="$(basename "$(dirname "$project")")"
    project_arguments=("$ROOT_DIR")
    if [[ "$project" == *"Spyro.Editor.TerrainTextureExportSmoke.csproj" ]]; then
        project_arguments+=("--clean-output")
    fi
    echo
    echo "Running $project_name..."
    dotnet run \
        --project "$project" \
        --configuration "$CONFIGURATION" \
        --no-build \
        -- \
        "${project_arguments[@]}"
done

echo
echo "Running focused native-unreferenced art-only terrain export smoke..."
dotnet run \
    --project "$ROOT_DIR/src/Spyro.Editor.TerrainTextureExportSmoke/Spyro.Editor.TerrainTextureExportSmoke.csproj" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR" \
    --art-only-preserve-target-only \
    --clean-output

run_smoke "placement smoke" "--placement-smoke-only"
run_smoke "gem value and treasure-total smoke" "--gem-value-smoke-only"
run_smoke "generic identity/source-signature smoke" "--generic-identity-signature-smoke-only"
run_smoke "whole-catalog visual-category smoke" "--visual-category-smoke-only"
run_smoke "deep identity workbench refresh" "--identity-deep-audit-smoke-only"
run_smoke "release-link smoke" "--release-link-smoke-only"
run_smoke "native portal source/layout smoke" "--portal-source-smoke-only"
run_smoke "cross-level chest/enemy swap catalogue smoke" "--cross-level-swap-catalog-smoke-only"
run_smoke "moby build-safety smoke" "--moby-build-safety-smoke-only"
run_smoke "linked homeworld portal sky capacity smoke" "--linked-portal-sky-safety-smoke-only"
run_smoke "level-name smoke" "--level-text-smoke-only"
run_smoke "level-music smoke" "--level-music-smoke-only"
run_smoke "diagnostics and metadata-only support ZIP smoke" "--diagnostics-smoke-only"
run_smoke "object write/readback smoke" "--object-smoke-only" "--clean-object-smoke-bins"

echo
echo "Running all-level Moby identity coverage audit..."
dotnet run \
    --project "$IDENTITY_SMOKE_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR"

echo
echo "Running all-level Moby raster-icon coverage audit..."
dotnet run \
    --project "$MOBY_ICON_SMOKE_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR"

echo
echo "Running headless release UI smoke..."
dotnet run \
    --project "$UI_SMOKE_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR"

echo
echo "Running focused Moby atlas masking/render UI smoke..."
dotnet run \
    --project "$UI_SMOKE_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR" \
    --moby-atlas-only

echo
echo "Running focused native terrain texture stage/reload/Undo UI smoke..."
dotnet run \
    --project "$UI_SMOKE_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR" \
    --terrain-native-roundtrip-only

echo
echo "Running focused terrain texture gallery UI smoke..."
dotnet run \
    --project "$UI_SMOKE_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR" \
    --terrain-texture-paint-gallery-only

echo
echo "Running focused terrain texture paint/apply/Undo UI smoke..."
dotnet run \
    --project "$UI_SMOKE_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR" \
    --terrain-texture-paint-mode-only

echo
echo "Running focused native-unreferenced art-only Apply/reload/Undo UI smoke..."
dotnet run \
    --project "$UI_SMOKE_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR" \
    --terrain-unreferenced-art-only

echo
echo "Running focused native load-initialized terrain texture/Lod UI smoke..."
dotnet run \
    --project "$UI_SMOKE_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR" \
    --terrain-texture-lod-only

echo
echo "Running all-level native terrain Map material UI smoke..."
dotnet run \
    --project "$UI_SMOKE_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR" \
    --terrain-native-map-materials-only

echo
echo "Running shipping complete-source terrain scene-view UI smoke..."
dotnet run \
    --project "$UI_SMOKE_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR" \
    --terrain-scene-view-only

echo
echo "Running all-level local Game View terrain-culling UI smoke..."
dotnet run \
    --project "$UI_SMOKE_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR" \
    --terrain-fly-game-view-only

echo
echo "Running focused bounded native PSX terrain compositor UI smoke..."
dotnet run \
    --project "$UI_SMOKE_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR" \
    --terrain-bounded-compositor-only

echo
echo "Running focused editor-camera-derived native GTE terrain projection experiment..."
dotnet run \
    --project "$UI_SMOKE_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR" \
    --terrain-native-gte-projection-only

echo
echo "Running focused editor-overview/native-distance terrain LOD UI smoke..."
dotnet run \
    --project "$UI_SMOKE_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR" \
    --viewport-fit-only

echo
echo "Running focused native terrain depth-cue UI smoke..."
dotnet run \
    --project "$UI_SMOKE_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR" \
    --terrain-depth-cue-only

if [[ -d "$PROOF_BIN_DIR" ]] && find "$PROOF_BIN_DIR" -type f \( -name '*.bin' -o -name '*.cue' \) -newer "$PROOF_GUARD" -print -quit | grep -q .; then
    fail "The default report-only suite unexpectedly created or modified a control-role proof BIN/CUE under $PROOF_BIN_DIR."
fi

if [[ "$WITH_GRADE_WRITE_READBACK" -eq 1 ]]; then
    echo
    echo "Optional all-level grade/crash-guard readbacks enabled. Disposable outputs are not retained."
    run_smoke "all-level environment-grade matrix" "--environment-grade-matrix-smoke-only"
    run_smoke "Dark Hollow dragon/object/environment crash guard" "--darkhollow-crash-guard-smoke-only"
    run_smoke "environment-grade write/readback smoke" "--environment-grade-smoke-only"
fi

verify_source_hashes || exit 1
HASH_GUARD_VERIFIED=1

echo
echo "Source-disc SHA-256 guard passed; the configured BIN/CUE remained byte-identical."
echo "Offline QA passed. Neither DuckStation nor a visible Avalonia window was launched."
if [[ "$WITH_GRADE_WRITE_READBACK" -eq 0 ]]; then
    echo "No large retained control-role proof BINs were generated; disposable object readbacks were cleaned."
fi

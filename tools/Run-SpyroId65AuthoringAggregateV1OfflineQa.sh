#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
CONTRACT_PROJECT="$ROOT_DIR/src/Spyro.Editor.Id65AuthoringAggregateV1RuntimeEvidenceContractSmoke/Spyro.Editor.Id65AuthoringAggregateV1RuntimeEvidenceContractSmoke.csproj"
FINALIZER_PROJECT="$ROOT_DIR/src/Spyro.Editor.Id65AuthoringAggregateV1RuntimeEvidenceFinalizer/Spyro.Editor.Id65AuthoringAggregateV1RuntimeEvidenceFinalizer.csproj"
CAPTURE_STAGING="$ROOT_DIR/docs/runtime-evidence/assets/unused-level-65-authoring-aggregate-v1/capture-staging"
FOCUSED_EVIDENCE="$ROOT_DIR/docs/runtime-evidence/unused-level-65-authoring-aggregate-v1-runtime-evidence.json"
EXACT_CAPTURE_STAGING="$ROOT_DIR/docs/runtime-evidence/assets/unused-level-65-authoring-aggregate-exact-v1/capture-staging"
EXACT_FOCUSED_EVIDENCE="$ROOT_DIR/docs/runtime-evidence/unused-level-65-authoring-aggregate-exact-v1-runtime-evidence.json"
SAVE_PROFILE="$ROOT_DIR/_local/v5-stone-hill-level-replacement/duckstation-id65-authoring-aggregate-save-v1"
LEGACY_RUN_ID="id65v3-20260825t000000z-0123456789abcdef"
EXACT_RUN_ID="id65agg1-20260825t120000z-0123456789abcdef"

fail() {
    echo "ID65 aggregate V1 offline QA error: $*" >&2
    exit 1
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || fail "Required command '$1' was not found."
}

require_absent() {
    [[ ! -e "$1" && ! -L "$1" ]] || fail "Expected path to remain absent: $1"
}

require_no_duckstation() {
    if pgrep -x DuckStation >/dev/null 2>&1; then
        fail "DuckStation is running during offline-only QA."
    fi
}

run_capture_refusal() {
    local label="$1"
    shift
    local output
    local result
    set +e
    output="$(python3 \
        "$ROOT_DIR/tools/Capture-SpyroId65AuthoringAggregateV1RuntimeSession.py" \
        "$@" 2>&1)"
    result=$?
    set -e
    [[ "$result" -eq 2 ]] || \
        fail "$label returned $result instead of refusal status 2."
    [[ "$output" == *"profile.no-memory-card-save-persistence"* ]] || \
        fail "$label did not retain the save-persistence blocker."
    [[ "$output" == *"workflow.frozen-v3-seven-session-lone-pad-incompatible"* ]] || \
        fail "$label did not retain the exact-workflow blocker."
}

run_exact_capture_refusal() {
    local label="$1"
    shift
    local output
    local result
    set +e
    output="$(python3 \
        "$ROOT_DIR/tools/Capture-SpyroId65AuthoringAggregateExactV1RuntimeSession.py" \
        "$@" 2>&1)"
    result=$?
    set -e
    [[ "$result" -eq 2 ]] || \
        fail "$label returned $result instead of refusal status 2."
    [[ "$output" == *"EXACT_CAPTURE_ENGINE_NOT_IMPLEMENTED"* ]] || \
        fail "$label did not retain the exact-engine blocker."
    [[ "$output" == *"aggregate.exact-three-session-card-aware-capture-engine"* ]] || \
        fail "$label did not retain the exact production gate."
}

run_finalizer_refusal() {
    local label="$1"
    shift
    local output
    local result
    set +e
    output="$(dotnet run \
        --project "$FINALIZER_PROJECT" \
        --configuration "$CONFIGURATION" \
        --no-build \
        -- "$@" 2>&1)"
    result=$?
    set -e
    [[ "$result" -eq 3 ]] || \
        fail "$label returned $result instead of refusal status 3."
    [[ "$output" == *"profilePinsVerified=false"* ]] || \
        fail "$label did not retain the exact profile-verification gate."
    [[ "$output" == *"exactCaptureEngineImplemented=false"* ]] || \
        fail "$label did not retain the exact-engine implementation gate."
    [[ "$output" == *"productionCaptureEnabled=false"* ]] || \
        fail "$label did not retain the production-capture gate."
    [[ "$output" == *"EXACT_CAPTURE_ENGINE_NOT_IMPLEMENTED"* ]] || \
        fail "$label did not retain the exact-engine blocker identity."
    [[ "$output" == *"aggregate.exact-three-session-card-aware-capture-engine"* ]] || \
        fail "$label did not retain the exact-engine required gate."
    [[ "$output" == *"No runtime evidence was published"* ]] || \
        fail "$label did not retain the no-publication result."
}

require_command dotnet
require_command pgrep
require_command python3
export PYTHONDONTWRITEBYTECODE=1

require_absent "$CAPTURE_STAGING"
require_absent "$FOCUSED_EVIDENCE"
require_absent "$EXACT_CAPTURE_STAGING"
require_absent "$EXACT_FOCUSED_EVIDENCE"
[[ -d "$SAVE_PROFILE" && ! -L "$SAVE_PROFILE" ]] || \
    fail "The pinned save-capable profile is absent or unsafe."
require_no_duckstation

echo "Spyro Editor ID65 authoring aggregate V1 offline QA"
echo "Workspace: $ROOT_DIR"
echo "Configuration: $CONFIGURATION"
echo "Safety: no editor, DuckStation, capture staging, runtime evidence, or input automation."

echo "[1/17] Verifying the no-card aggregate profile adapter..."
python3 "$ROOT_DIR/tools/Build-SpyroId65AuthoringAggregateV1Profile.py" \
    self-test --repository-root "$ROOT_DIR"
python3 "$ROOT_DIR/tools/Build-SpyroId65AuthoringAggregateV1Profile.py" \
    verify --repository-root "$ROOT_DIR"

echo "[2/17] Running the no-card aggregate profile adversarial suite..."
python3 "$ROOT_DIR/tools/Test-SpyroId65AuthoringAggregateV1Profile.py"

echo "[3/17] Verifying the no-card aggregate prelaunch snapshot..."
python3 "$ROOT_DIR/tools/Verify-SpyroId65AuthoringAggregateV1Profile.py" \
    self-test --repository-root "$ROOT_DIR"
python3 "$ROOT_DIR/tools/Verify-SpyroId65AuthoringAggregateV1Profile.py" \
    prelaunch --repository-root "$ROOT_DIR"

echo "[4/17] Verifying the save-capable aggregate profile adapter..."
python3 "$ROOT_DIR/tools/Build-SpyroId65AuthoringAggregateSaveV1Profile.py" \
    self-test --repository-root "$ROOT_DIR"
python3 "$ROOT_DIR/tools/Build-SpyroId65AuthoringAggregateSaveV1Profile.py" \
    verify --repository-root "$ROOT_DIR"

echo "[5/17] Running the save-capable aggregate profile adversarial suite..."
python3 "$ROOT_DIR/tools/Test-SpyroId65AuthoringAggregateSaveV1Profile.py"

echo "[6/17] Verifying the empty-memory-card prelaunch boundary..."
python3 "$ROOT_DIR/tools/Verify-SpyroId65AuthoringAggregateSaveV1Profile.py" \
    self-test --repository-root "$ROOT_DIR"
python3 "$ROOT_DIR/tools/Verify-SpyroId65AuthoringAggregateSaveV1Profile.py" \
    verify-prelaunch --repository-root "$ROOT_DIR"

echo "[7/17] Running the aggregate capture coordinator self-test..."
python3 "$ROOT_DIR/tools/Capture-SpyroId65AuthoringAggregateV1RuntimeSession.py" \
    self-test

echo "[8/17] Running the aggregate capture coordinator deep smoke..."
python3 "$ROOT_DIR/tools/Test-Capture-SpyroId65AuthoringAggregateV1RuntimeSession.py"

echo "[9/17] Proving every legacy live capture operation refuses at both blockers..."
run_capture_refusal "capture preflight" preflight
run_capture_refusal "capture session" capture-session candidate-edge-1
run_capture_refusal "capture verification" verify-staging "$LEGACY_RUN_ID"

echo "[10/17] Running the exact three-session coordinator self-test..."
python3 "$ROOT_DIR/tools/Capture-SpyroId65AuthoringAggregateExactV1RuntimeSession.py" \
    self-test

echo "[11/17] Running the exact three-session coordinator adversarial suite..."
python3 "$ROOT_DIR/tools/Test-Capture-SpyroId65AuthoringAggregateExactV1RuntimeSession.py"

echo "[12/17] Proving every exact live capture operation remains closed..."
run_exact_capture_refusal "exact capture preflight" preflight
run_exact_capture_refusal "exact capture run" capture-run
run_exact_capture_refusal "exact capture resume" resume-run "$EXACT_RUN_ID"
run_exact_capture_refusal "exact capture verification" verify-staging "$EXACT_RUN_ID"
run_exact_capture_refusal "exact capture quarantine" quarantine-abort "$EXACT_RUN_ID"

echo "[13/17] Building the aggregate runtime-evidence contract smoke..."
dotnet build "$CONTRACT_PROJECT" \
    --configuration "$CONFIGURATION" --nologo -warnaserror

echo "[14/17] Running the aggregate runtime-evidence contract smoke..."
dotnet run --project "$CONTRACT_PROJECT" \
    --configuration "$CONFIGURATION" --no-build

echo "[15/17] Building the aggregate evidence finalizer..."
dotnet build "$FINALIZER_PROJECT" \
    --configuration "$CONFIGURATION" --nologo -warnaserror

echo "[16/17] Running the aggregate evidence finalizer fixture smoke..."
dotnet run --project "$FINALIZER_PROJECT" \
    --configuration "$CONFIGURATION" --no-build -- self-test

echo "[17/17] Proving every production finalizer operation remains closed..."
run_finalizer_refusal "finalizer inspect" inspect --run-id "$EXACT_RUN_ID"
run_finalizer_refusal "finalizer finalize" \
    finalize --run-id "$EXACT_RUN_ID" \
    --attestation /tmp/id65-aggregate-v1-offline-qa-nonexistent-attestation.json \
    --attestation-sha256 \
    0000000000000000000000000000000000000000000000000000000000000000
run_finalizer_refusal "finalizer verify" verify

require_absent "$CAPTURE_STAGING"
require_absent "$FOCUSED_EVIDENCE"
require_absent "$EXACT_CAPTURE_STAGING"
require_absent "$EXACT_FOCUSED_EVIDENCE"
require_no_duckstation

echo "ID65_AUTHORING_AGGREGATE_V1_OFFLINE_QA_OK steps=17 exactEngine=false runtime=false createBin=false release=false publish=false"

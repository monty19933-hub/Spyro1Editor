#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
V3_CANDIDATE_PROJECT="$ROOT_DIR/src/Spyro.Editor.UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3RuntimeCandidateSmoke/Spyro.Editor.UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3RuntimeCandidateSmoke.csproj"
V3_EVIDENCE_CONTRACT_PROJECT="$ROOT_DIR/src/Spyro.Editor.Id65V3RuntimeEvidenceContractSmoke/Spyro.Editor.Id65V3RuntimeEvidenceContractSmoke.csproj"
V3_EVIDENCE_FINALIZER_PROJECT="$ROOT_DIR/src/Spyro.Editor.Id65V3RuntimeEvidenceFinalizer/Spyro.Editor.Id65V3RuntimeEvidenceFinalizer.csproj"
V3_REWARD_PUBLISHER_PROJECT="$ROOT_DIR/src/Spyro.Editor.Id65AuthoringRewardStagingRuntimeCandidatePublisherSmoke/Spyro.Editor.Id65AuthoringRewardStagingRuntimeCandidatePublisherSmoke.csproj"
V3_EVIDENCE_PATH="$ROOT_DIR/docs/runtime-evidence/unused-level-65-remote-blank-environment-header-repair-v3-runtime-evidence.json"

fail() {
    echo "ID65 V3 research checkpoint QA error: $*" >&2
    exit 1
}

command -v dotnet >/dev/null 2>&1 || fail "Required command 'dotnet' was not found."
command -v python3 >/dev/null 2>&1 || fail "Required command 'python3' was not found."

echo "Spyro Editor ID65 V3 research checkpoint QA"
echo "Workspace: $ROOT_DIR"
echo "Configuration: $CONFIGURATION"
echo "Safety: offline build/readback tests only; no editor, DuckStation, capture, or input automation."
echo

echo "[1/13] Running the read-only ID65 runtime watcher offline self-test..."
python3 "$ROOT_DIR/tools/Watch-SpyroId65RuntimeState.py" --self-test

echo
echo "[2/13] Running the capture coordinator offline self-test..."
python3 "$ROOT_DIR/tools/Capture-SpyroId65V3RuntimeSession.py" self-test

echo
echo "[3/13] Running the deep capture coordinator offline smoke..."
python3 "$ROOT_DIR/tools/Test-Capture-SpyroId65V3RuntimeSession.py" --offline-only

echo
echo "[4/13] Running the unattended input coordinator offline self-test..."
python3 "$ROOT_DIR/tools/Automate-SpyroId65V3RuntimeSession.py" --self-test

echo
echo "[5/13] Running the unattended input coordinator deep offline smoke..."
python3 "$ROOT_DIR/tools/Test-Automate-SpyroId65V3RuntimeSession.py"

echo
echo "[6/13] Running the isolated DuckStation profile builder offline self-test..."
python3 "$ROOT_DIR/tools/Build-SpyroId65UnattendedProfile.py" self-test

echo
echo "[7/13] Running the isolated DuckStation profile builder deep fixture smoke..."
python3 "$ROOT_DIR/tools/Test-Build-SpyroId65UnattendedProfile.py"

echo
echo "[8/13] Running the external-HOME profile verifier offline self-test..."
python3 "$ROOT_DIR/tools/Verify-SpyroId65UnattendedProfile.py" self-test

echo
echo "[9/13] Running the external-HOME profile verifier deep fixture and production prelaunch smoke..."
python3 "$ROOT_DIR/tools/Test-Verify-SpyroId65UnattendedProfile.py"

echo
echo "[10/13] Building and running the deterministic V3 environment-header candidate smoke..."
dotnet build "$V3_CANDIDATE_PROJECT" --configuration "$CONFIGURATION" --nologo
dotnet run \
    --project "$V3_CANDIDATE_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR"

echo
echo "[11/13] Building and running the fail-closed V3 runtime-evidence contract smoke..."
dotnet build "$V3_EVIDENCE_CONTRACT_PROJECT" --configuration "$CONFIGURATION" --nologo
dotnet run \
    --project "$V3_EVIDENCE_CONTRACT_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR"

echo
echo "[12/13] Building the V3 evidence finalizer and proving missing evidence is refused..."
dotnet build "$V3_EVIDENCE_FINALIZER_PROJECT" --configuration "$CONFIGURATION" --nologo
[[ ! -e "$V3_EVIDENCE_PATH" && ! -L "$V3_EVIDENCE_PATH" ]] || \
    fail "The canonical V3 evidence path already exists; missing-evidence refusal cannot be tested."
set +e
FINALIZER_OUTPUT="$(dotnet run \
    --project "$V3_EVIDENCE_FINALIZER_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- verify 2>&1)"
FINALIZER_STATUS=$?
set -e
printf '%s\n' "$FINALIZER_OUTPUT"
[[ "$FINALIZER_STATUS" -eq 3 ]] || \
    fail "The pending finalizer verify gate returned $FINALIZER_STATUS instead of the required refusal status 3."
[[ "$FINALIZER_OUTPUT" == *"REFUSED:"* ]] || \
    fail "The pending finalizer verify gate did not emit its required REFUSED marker."
[[ "$FINALIZER_OUTPUT" == *"Could not lstat path segment"*"$V3_EVIDENCE_PATH"*"(errno 2)."* ]] || \
    fail "The finalizer refusal was not the exact canonical missing-evidence lstat/errno-2 gate."

echo
echo "[13/13] Building and running the fail-closed Core4 reward publisher smoke..."
dotnet build "$V3_REWARD_PUBLISHER_PROJECT" --configuration "$CONFIGURATION" --nologo
dotnet run \
    --project "$V3_REWARD_PUBLISHER_PROJECT" \
    --configuration "$CONFIGURATION" \
    --no-build \
    -- \
    "$ROOT_DIR"

echo
echo "ID65 V3 research checkpoint QA passed. Runtime, Create BIN, promotion, and release gates remain closed."

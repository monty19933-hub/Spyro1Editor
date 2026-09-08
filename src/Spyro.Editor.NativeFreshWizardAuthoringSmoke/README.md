# Authored fresh Wizard composition checks

This source-free/explicit-source smoke tests the closed authored same-layout
profile alongside the unchanged fixed A/P1 research chain. It does not launch an
emulator or publish an image in either default or `--source` mode.

The authored profile consumes an actual aggregate document, its actual base
compilation and actual Wizard family/resource ancestors. It replaces only the
canonical 324-byte untextured surface owner in an otherwise fixed resource
layout, and compiles one document-owned Wizard route. Unsupported native copy
layouts and extra placements are refused rather than silently substituted.

The 2048-square floor/route fixture is independently checked against a pinned
old P1-derived counterfactual. That reference is not an original-only rebuild:
the actual C# chain separately captures the complete original and builds its
resource, scene, body, executable and detached Disc from source. Full body,
scene and data hashes must agree between the two paths. Original BIN/CUE hashes
are checked again after the source exercise, including on failure.

Initial collision-sphere separation is a narrow source assertion, not proof of
safe AI trajectories, all future collision queries, peak allocation, audio,
rewards, native playability or general cross-level importing. Old fixed candidate
issuance must continue to refuse this different authored Disc.

```sh
dotnet build src/Spyro.Editor.NativeFreshWizardAuthoringSmoke -c Release
dotnet run --project src/Spyro.Editor.NativeFreshWizardAuthoringSmoke -c Release --no-build
dotnet run --project src/Spyro.Editor.NativeFreshWizardAuthoringSmoke -c Release --no-build -- --source /absolute/original.bin /absolute/original.cue
```

Both modes are registered in the common regression runner; source mode is
explicit and requires the user's matching original game files. The source
success marker precedes final cleanup, so require a successful process exit and
the `FRESH_WIZARD_AUTHORING_FINAL_OK` marker as well.

## Explicit exhaustive issued matrix

The 72-row / 46-track source comparison is a separate opt-in. It is **not** run
by default, by ordinary `--source`, or by the common regression runner's shared
source-QA deadline. It needs about 17 minutes on the measured host:

```sh
dotnet run --project src/Spyro.Editor.NativeFreshWizardAuthoringSmoke -c Release --no-build -- --source-issued-matrix /absolute/original.bin /absolute/original.cue
```

This exact three-token mode runs only the matrix source chain, not the ordinary
source suite or publication. Blank, NUL, option-like and duplicate BIN/CUE paths,
unknown/mixed modes and extra arguments refuse before file I/O. Relative paths
remain supported and are resolved before either original is opened.

The unchanged matrix compares actual issued bytes against 72 independently
derived original-source expectation rows, including all 46 music tracks and 67
initial-footprint-admitted rows. Every track travels through its own actual
Compilation → Resource → Scene → Host → Body → EXE → Disc ancestry. Full forward
image hashes are independently expected; the production inverse is a round trip,
not a second independent inverse. There is no Candidate or physical-image output.

The matrix retains its held/current-image whole-source checks and closure. The
new outer arm authenticates both original BIN and CUE and independently attempts
both full named-file final checks even after a failed initial check or matrix
action. Errors remain primary-first. Require exit 0 plus both
`ACTUAL_WIZARD_MATRIX_SOURCE_OK` and `ACTUAL_WIZARD_MATRIX_FINAL_OK`; ordinary
authoring completion markers are not emitted by this arm.

The full 72-row implementation already passed before this dispatch split:
72 rows, 46 tracks, 67 admissions; summed logged row durations 1014.805 seconds.
That retained run used the former combined `--source` dispatch. It is evidence
for the unchanged matrix, not an execution claim for this new standalone CLI.
Neither route asserts future movement safety, native gameplay, audio or export
approval. The ordinary source suite retains its historical sequential final
checks and error ordering; the new arm does not retroactively strengthen them.

## Explicit separated candidate

The separate reviewed modes accept exactly four arguments. They are never
entered by the default or source regression modes:

```sh
dotnet run --project src/Spyro.Editor.NativeFreshWizardAuthoringSmoke -c Release --no-build -- --publish-separated-reviewed /absolute/original.bin /absolute/original.cue /absolute/existing-parent/new-output
dotnet run --project src/Spyro.Editor.NativeFreshWizardAuthoringSmoke -c Release --no-build -- --verify-separated-reviewed /absolute/original.bin /absolute/original.cue /absolute/existing-parent/new-output
```

Publication requires an absent output directory; verification requires the
exact existing three-file set. Both independently capture the actual authored
floor, Wizard route and initial-clearance chain from the original files. The
closed recipe is a 2048-square floor at (8448,8448,8704), Spyro spawn (8960,8960),
and one Wizard at (9984,9984,8704) routed to (9472,9984), with track 19. Outputs
use the distinct `native-id65-fresh-wizard-separated-p1-n32-r01` basename.

Both original files are held and fully rehashed through true EOF again on every
exit; success is reported only after both final checks and both close attempts.
Every output is reopened, including full BIN readback and exact CUE/receipt.
An actual unconfirmed publication preserves its evidence and is never upgraded
by matching bytes, automatically retried, cleaned up, or offered as success.
An acknowledged commit with failed final checks remains a distinct failure.

This command-line research candidate is not a saved-project export, normal
Create BIN feature, or runtime certification. The old unsafe fixed-P1 golden is
retained for regression, not offered as the corrected gameplay candidate.

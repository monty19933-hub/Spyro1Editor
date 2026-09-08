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

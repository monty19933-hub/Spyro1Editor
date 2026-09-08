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

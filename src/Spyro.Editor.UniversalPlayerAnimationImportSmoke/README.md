# Source-backed player animation import checks

Run this optional fixture against the unchanged original USA disc and a workspace containing the complete original `spyro-level-catalog.json`:

```
dotnet run --project src/Spyro.Editor.UniversalPlayerAnimationImportSmoke -c Release -- ORIGINAL_USA_BIN CATALOG_WORKSPACE ARTIFACTS_DIRECTORY
```

The test uses public universal composition and DryRun APIs. It checks Alpine class `0x10F` rows 4 and 1 importing exact local Spyro animation 23, shared slots 13/15 reuse, repeat-donor and Stone Hill class `0x071` cross-enemy deduplication, same-source no-growth, native player/model/scene preservation, final RAM accounting, missing-provider refusal, and insufficient RAM refusal. Magic Crafters' unresolved child `0x13B` must remain refused.

A private Artisans fixture removes native actors to make room; another retains authentic native actors and compares their scene/property bytes against the same deletion-only baseline. The RAM refusal fixture enlarges the retained final model's zero allocation. These changes exist only in copied memory and never alter the source disc or a saved project.

The fresh case issues a real neutral ID65 host through `NativeFreshMainCompilation` and exercises its animation provider connection. Its temporary bootstrap image is deleted in `finally`. The JSON receipt records static composition evidence. This test does not run a game, open the UI, publish packages, or establish complete gameplay dependency coverage.

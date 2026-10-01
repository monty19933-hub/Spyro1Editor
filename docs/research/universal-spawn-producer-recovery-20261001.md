# Native longer child-class producer recovery

The completed earlier static matrix contains 584 cells refusing the class1A5 → child1A6 family. Across 21 original source levels, the actual `ADDIU a0,zero,1A6` is exactly60bytes before its InitGlobal callback. `ConstantSpawns` previously searched only48bytes before the call (52-byte exclusive loop bound). The separate bounded basic-block analyzer already proved this callback and argument on every actual captured source owner.

The existing short path remains unchanged. A missing producer may now use a direct ADDIU/ORI from zero up to60bytes before the call, only when `UniversalDependencyAnalysis` agrees on that exact call PC, InitGlobal callback, callback-load PC and constant argument. It preserves the basic-block analyzer's incoming control targets, ownership gaps, call boundaries, load delays and unknown-indirect-entry refusals. It does not rewrite dynamic properties, aliases or arbitrary earlier constants. No new model, texture, sound, particle or behavior substitution is introduced.

Isolated Release and Debug builds passed with zero compiler warnings/errors. The focused source-free suite has17 checks; the source-backed suite has144 including all21 native owners, producer removal refusal, exact source opcode, independent provenance agreement, mapped assembled producer and source immutability. The assembly check is only a producer-patching check; complete dependency enrollment is assessed by the separate real Compose rescan. Existing class ownership2099/source-free universal97 checks also passed in the private Release mirror.

The affected-cell rescan recomposed all584 old failures using their exact captured source variants, original destination data/overlay, protected VRAM, shared models and player-animation provider. In both configurations,345 now compose and239 remain explicitly RAM-blocked. All345 have unverified dependencies and all runtime statuses remain not-tested. Release and Debug cell results are identical. This is an actual static coverage improvement, not345 new gameplay passes or complete universal closure.

Original USA SHA256: `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`. Earlier full matrix module MVID: `3cb0c426-71e0-40f7-b129-e4bd079db981`. The new private module SHA256 is recorded separately in each `spawn-producer-affected-{release,debug}.json` receipt; the older matrix is never relabeled as using the newer implementation.

From the source root:

```sh
dotnet run --project src/Spyro.Editor.UniversalSpawnProducerSmoke -c Release
dotnet run --project src/Spyro.Editor.UniversalSpawnProducerSmoke -c Release -- \
  "$USA_BIN" "$SOURCE_ROOT" "$REPORT_JSON"
# Optional exact earlier failed-cell rescan:
dotnet run --project src/Spyro.Editor.UniversalSpawnProducerSmoke -c Release -- \
  "$USA_BIN" "$SOURCE_ROOT" "$REPORT_JSON" "$OLD_MATRIX_JSON_GZ"
```

Repeat with Debug. Raw logs and module/source bindings are under `work/universal-v1-continuation/dependencies/spawn-producer-*`. The read-only prior ranking is in `dependency-next-fix-ranking.json`; genuine missing-source assets, reserved texture depth and real RAM/SPU limits retain their separate refusals. No emulator, card, original user project, released package or source image was changed by these checks.

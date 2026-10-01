# Original-overlay player-damage dependency recovery

This source/static check complements primary model-collider analysis. It is not complete player-state dependency closure or gameplay acceptance. Original USA image SHA256: `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.

`UniversalPlayerDamageWrites.AnalyzeOverlay` accepts the original overlay and an owned instruction set. It recovers only a same-basic-block LW of `0x80078A84`, constant OR additions, and SW back to that address. Load-delay consumers, unrelated addresses, overwrite/AND/XOR operations, mutable masks, instruction gaps, call/join boundaries, and unknown indirect entries are declined. The certified state/animation map remains owned by `UniversalPlayerDamageDependencies.MapDamageMask`.

Feed actual copied behavior provenance (`item.SourcePc ?? item.Orig`), not the entire original overlay. Native Wizard Peak `0x80084CA8` LW / `0x80084CC0` ORI 0x800 / `0x80084CC8` SW belongs to class `0x12C`, not Armored Druid `0x11C`. The `0x8007BA74` / `0x8007BA8C` / `0x8007BA94` writer belongs to the common dragon class `0x009`. Mask 0x800 maps to animation IDs 16 and 18. Actual pruned 0x12C/0x11C bundle report tests assert inclusion/exclusion.

Independent original-donor census: 1,368 local model packages, 196 with hurt-animation requirements. All required available records have FF frame selectors and match the admitted shared geometry/texture header. One source model, Terrace Village class 0x1D3, declares mask 0x16 and IDs 13,15,26,32, but its local/shared source supplies neither 26 nor 32. This requires separate activation/consumer proof; no substitute records or bypass are admitted. The 53 admitted shared simple aliases declare no primary colliders. There are 42 registered-class pruned update closures with recovered constant damage writers; this census does not include every spawned/unregistered class or prove all dynamic writes.

An isolated staged merge exercise passed 84 checks: Alpine Ridge animation23 inserted 22,196 exact bytes / 22,528 model growth into an Artisans body already containing a private VRAM change, an edited native row and an appended original enemy model. Model slots and all original model bytes were preserved with exact root relocation; all non-model subfiles remained byte-identical; caller input was unchanged; repeating the merge was a no-op. The canonical save-map EXE hooks still satisfy the damage-map source certificates. This additional RAM must be gated against the actual final composed body; prior capacity receipts are not authority for a larger body.

Reproduce focused checks from the source root:

```sh
dotnet run --project src/Spyro.Editor.UniversalPlayerDamageWritesSmoke -c Release
# Optional unchanged original source fixture; catalog directory is the source root.
dotnet run --project src/Spyro.Editor.UniversalPlayerDamageWritesSmoke -c Release -- "$USA_BIN" "$SOURCE_ROOT"
```

Release and Debug each passed 19 source-free / 25 source-aware checks in isolated mirrors, with no compiler warnings or errors. Logs and detailed source/static receipts: `work/universal-v1-continuation/dependencies/player-writes-{release,debug}.log`, `player-model-dependency-audit.json`, `player-merge-integration-release.json`, and `player-dependency-review-receipt.json` under the workspace. No emulator, disc export, original project, card or package was changed by these checks.

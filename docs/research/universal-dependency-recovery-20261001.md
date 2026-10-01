# Native dependency recovery — 1 October 2026

This change adds source-derived dependency evidence. It does not establish cross-level gameplay, particle-dispatch equivalence, or a new released package. The preserved V1.0 packages and original inputs were not changed.

`UniversalDependencyAnalysis.Calls` recovers callback provenance and constant arguments within one bounded basic block. Incoming branch/jump-table targets, call continuations, and gaps in the captured instruction owner stop recovery. Arguments include the current call's delay slot; the callback target is captured before it. Mutable loads, unknown callback bases, unresolved indirect entries, invalid control transfers in delay slots, load-delay aliases, and overly distant/deep expressions remain unknown. `UniversalDispatchAnalysis.Dependencies` unions these proven constants with its existing evidence. The analysis never invents a model, texture, sound, or behavior.

## Original-source evidence

The authenticated USA image SHA256 is `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.

An independent CFG audit and the bounded production recovery examined 70 original update/init owners across the 35 captured retail levels. Bounded recovery identified 126 previously missed constant call sites, corresponding to 71 unique source/global/dependency combinations. There were no disagreements where the independent CFG analysis retained a known argument. These counts describe whole-function static evidence, not newly working destination combinations.

The source-aware smoke also verifies each of these calls belongs to the stated class's extracted update closure and enters the dependency set:

| Source | Native class | Call PC | Particle argument |
|---|---:|---:|---:|
| Artisans | `187` | `800849E0` | `0E` |
| Alpine Ridge | `098` | `80081644` | `11` |
| Magic Crafters | `10E` | `8008591C` | `08` |
| High Caves | `0E3` | `80082B50` | `1F` |

The previous extractor used short lexical lookbacks and only recognized direct immediate assignments. It missed, for example, Artisans' argument 22 instructions before the call, Alpine Ridge's callback load ten instructions before the call, and native `addu a1,zero,zero` particle arguments.

## Refusals retained

Magic Crafters class `10F`, source row 1, still refuses its spawned class `13B`. The specialized initializer has 79 instructions and reaches the same allocation/default-finalization path as the sentinel initializer; only comparison-tree PCs differ. Its native finalizer `800526A8` dereferences `g_Models[class]` without a null guard. There is no captured local/shared `13B` model registration or dedicated update handler. An initializer-only exception would not prove a usable native dependency.

Clifftown class `0E2`, source row 24, still refuses reserved texture depth. Six package descriptor fields (`2084`, `2124`, `2144`, `2192`, `2212`, `2232`, decimal) have page `FFFF`. They are genuine textured triangle commands with nondegenerate vertex indices. Native `80020380..80020564` consumes their UV words and emits them into GPU packets; no inert condition or owned replacement has been proved. They are not skipped or coerced to another depth.

Independent original-EXE byte comparison authenticated 4,699 instructions in `r_moby.s` and 1,067 in `42CC4.s`, with zero mismatches. The positive local-model sound audit examined 1,235 packages and 1,179 nonempty sound slots: every slot had an existing original source descriptor. This does not prove destination SPU capacity or arbitrary overlay sound-call rebinding.

## Verification and reproduction

Both Release and Debug builds of the focused smoke passed with zero warnings/errors. Each passed 53 checks with the unchanged original source, including 35 source-free checks and the two retained real-source refusal cases. The existing universal source-free suite also passed 97 checks.

From the repository root:

```sh
dotnet run --project src/Spyro.Editor.UniversalDependencySmoke -c Release
# Source-aware, in-memory checks; no exported image or card:
dotnet run --project src/Spyro.Editor.UniversalDependencySmoke -c Release -- \
  '/path/to/unchanged/Spyro the Dragon (USA).bin' "$PWD"
```

Repeat with `-c Debug` for the Debug build. The source-aware mode authenticates the original image hash before reading it. No source ROM, card, or copied runtime asset is included in this project.

Raw evidence and bound source/module hashes are in `work/universal-v1-continuation/dependencies/dependency-recovery-audit.json`, `source-probe-before.json`, `source-probe-after.json`, `native-renderer-ownership.json`, and the focused build/test logs. These are static/source checks; runtime testing of the newly recovered dependencies remains outstanding.

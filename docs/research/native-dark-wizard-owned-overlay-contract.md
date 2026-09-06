# Dark Hollow native Wizard: owned overlay composition

This stage produces detached, source-bound overlay and executable bytes. It does not publish a disc, approve placement, connect the normal exporter, or establish gameplay acceptance. `ActiveDispatchEncoded` means the returned overlay contains the private class hooks; it does not mean an emulator has executed them. Existing adapted Wizard and Town Square V2/V3 profiles remain unchanged.

## Exact owners and source authority

`NativeDarkHollowWizardOverlayPlan.Compile` accepts only the existing private-constructor issuers for the exact Wizard behavior, composed Dark resources, and particle behavior. It requires the genuine scene append, installed particle-7 descriptor, both complete new model registrations, 129 placed rows, and the 90-entry remaining dynamic arena. Placed plus dynamic indices remain below the native byte-sized owner limit. No caller-supplied offsets, fragment metadata, class authorization, or copy-buffer address are accepted.

Original image SHA256 is `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`. The complete original Dark overlay is WAD `0xEF6000 + 0xB000`, SHA256 `9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32`. The donor overlay is WAD `0x2F5B000 + 0x10000`, SHA256 `5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1`. Full original EXE at logical LBA 53875, length `0x66000`, is SHA256 `a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`.

All code uses one canonical Final overlay address domain and runtime base `0x8007AA38`. Every appended byte has an explicit owner, including sector padding. Every external Final reservation from the three fragment linkers must match a declared entry inside an actually materialized block; it cannot merely be a promised address. Missing coverage, overlaps, detached address domains, undeclared entries, wrong payload lengths, and finite over-budget input enumeration are rejected before copying.

## Materialized layout

The original `0xB000` owner is retained in full. In particular, the nonzero tail at overlay `0xAB5C..0xB000` is not available as scratch/code space.

| Appended owner | Overlay offset | Runtime entry/start | Bytes |
| --- | ---: | ---: | ---: |
| Main Wizard/lightning fragments | `B000` | `80085A38` | `8B8` |
| Private class-26 spawn | `B8B8` | `800862F0` | `2FC` |
| Particle 07/41 fragments | `BBB4` | `800865EC` | `380` |
| Four particle continuation bridges | `BF34` | `8008696C` | `20` |
| Native effect-call bridge | `BF54` | `8008698C` | `8` |
| Main context-restoring exit | `BF5C` | `80086994` | `98` |
| Lightning entry adapter | `BFF4` | `80086A2C` | `98` |
| Wizard entry adapter | `C08C` | `80086AC4` | `98` |
| Private class dispatcher | `C124` | `80086B5C` | `44` |
| Explicit zero sector padding | `C168` | `80086BA0` | `698` |

Exactly six original four-byte overlay fields change: the dispatch-entry words at `0x4EC/0x4F0`, and particle table pointers at `0x158/0x240/0x29C/0x384`. All other original overlay bytes are compared exactly in the source-backed regression.

The class hook replaces native PC `8007AF24` with a jump to the appended dispatcher and a NOP delay slot. The dispatcher handles only `0x26` and `0x11B`. For every other native class `0..511`, a separate delay-slot interpreter checks the same native destination and complete GPR state as the original compare/branch/delay window. In particular, the original `SLTI v0,v1,0xFC` delay operation and class-`0xFB` branch destination are replayed, not discarded.

The main adapter preserves GPRs and HI/LO, uses the existing explicit `0x2B0` frame, and establishes donor `fp=1`. All main exits intercept donor `80086D8C` before its cursor load, enter the owned restore sequence, then return to Dark `80082914`. The source-certified class-26 specialization is called only by its four proved native sites; the global `g758CC` callback is not replaced. Native electric damage flags `0x20020`, the four impact children, and the per-child dynamic-capacity guard of 21 remain intact.

Particle fragments use the separately certified operand/stack rebasing and four native bridges: `8442C`, `84448`, `84450`, and `8553C`. The donor particle-07 common tail is retained; no superficially similar destination initializer tail is substituted. Native effect calls enter retained Dark `844A0`. The original global particle callbacks remain at their native destinations; only the four absent selector table entries are changed. Direct EXE calls bind their actual source target and full original executable certificate, not an arbitrary function with the same address label.

See [behavior fragments](native-dark-wizard-behavior-fragment-contract.md), [private spawn specialization](native-dark-wizard-class26-specialization.md), and [particle behavior](native-dark-wizard-particle-behavior-contract.md) for the separate source and ABI boundaries.

## Copy buffer and measured normal-load RAM

The old copy buffer is `80085594`, inside the retained CD overlay owner. It must move even though the first appended instruction starts at `80085A38`. The source-owned EXE initializer at PC `8005A848` (file `0x4B048`) is pinned by the exact 16-byte preimage:

`0880023c945542240880013cd88522ac`

SHA256: `8949add56d6960cd6ab04432154419c31b596758b14ba41f99a63cb962e922aa`.

Only its first two instructions change: the LUI/ADDIU pair now constructs `80087238`, exactly the end of the complete sector-rounded `0xC800` overlay. The following native store to the copy-buffer global and every other EXE byte remain exact. `NativeLevelRuntimeMemoryReport.AnalyzeDarkWizardOverlay` independently decodes this pair, checks the complete EXE complement and composed data identity, then re-walks the native data/model/scene layout.

| Measured normal-load region | Value |
| --- | ---: |
| Copy-buffer start | `80087238` |
| Used level-data bytes, excluding sector padding | `387AC` |
| Model CD bytes | `72800` |
| Scene CD bytes | `11800` |
| Model RAM start | `800BF9E4` |
| Scene RAM start | `801321E4` |
| Scene read end | `801439E4` |
| Source-certified normal reserved-buffer start | `80187BB0` |
| Known normal-load gap | `441CC` |
| Remaining shared dynamic Moby capacity | 90 |

This is not a peak/transient RAM or runtime stack certificate. Particle occupancy, GTE state, frame/helper scratch, transition-time staging, and execution acceptance are not established by a positive gap.

## Regression and independent byte oracle

Final focused Debug gate: zero build warnings/errors; 1,345 source-free checks and 2,934 source-backed `--source-v3` checks passed. The source-backed run includes unchanged Town V2/V3 graph/data/overlay/EXE goldens, the independently reconstructed new output, and explicit RAM endpoints. The tracked independent Node oracle was rerun successfully after promotion from local research evidence.

The read-only Node oracle independently reads MODE2/2352 sectors, reconstructs source CFG relocations, performs the 49 particle operand changes from their native operand grammar, emits the frame adapters/dispatch, and constructs the copy-buffer pair without loading editor assemblies. It reproduces:

- Final overlay, length `C800`: `10f82ee1028a9ad4fa9045d948a88e76545847031b954ac08ad0e74e6e9c7083`.
- Final EXE, length `66000`: `14aa937f198740373b37312eb3054cad46dd1ae05f1920c145d6bc1fde9a8635`.

The independent oracle is tracked at `src/Spyro.Editor.NativeMobyDependencyClosureSmoke/source_overlay_oracle.mjs`. From the repository root, replay it without a .NET build:

```sh
node src/Spyro.Editor.NativeMobyDependencyClosureSmoke/source_overlay_oracle.mjs "Spyro the Dragon (USA).bin"
```

Retained local evidence: `_local/research/dark-wizard-native-closure-20260906/overlay-red-green.log` and `overlay-source-oracle.json`. The JSON records every appended block's extent and hash. This audit produces no candidate BIN and makes no emulator changes.

## Remaining gates

The returned bytes account for the selected code/entry owners and active dispatch encoding, not every transitive native consumer. The exact resource plan still records the global `0x1F` sound-map rebind policy; destination consumers must be audited before general approval. Direct native helpers retain source bytes, but their transitive globals, GTE effects, scratch/stack depth, and dynamic particle behavior need separate source/runtime evidence. The current scene append still uses source-donor placement; actual Dark terrain, routes and mover/collider transforms must be certified before a gameplay test placement is issued.

`CompleteNativeClosureProven`, `GteStateClosureProven`, `TransitiveGlobalClosureProven`, `RuntimeFitProven`, `PlacementApproved`, and `PublicationApproved` therefore all remain false. No suppressed children, altered damage type, replacement player animation, or approximate sound is introduced to pass this stage.

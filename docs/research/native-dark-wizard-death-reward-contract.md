# Native Dark Hollow Wizard death reward and treasure target

This is a source-issued accounting stage, not enemy gameplay or publication approval. `NativeDarkHollowWizardDeathRewardPlan.Capture(originalImagePath, overlay)` binds the exact retail image, the existing source-issued owned-overlay plan, one appended initial Wizard, native shared reward code, and the shared PETE model. It returns a detached EXE with only Dark Hollow's two-byte treasure target changed from 100 to 110 on top of the existing copy-buffer relocation. It does not alter the overlay, native reward behavior, scene, source ROM, or any candidate.

## Exact native reward chain

- The Wizard donor row at WAD `0x2F6B000 + 0x1CE800 + 0x6388` is class `0x11B`, with reward selector byte `+0x53 = 0x56` and initial one-shot state `+0x3A = 0x7F`. Its complete `0x58`-byte SHA-256 is `59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82`.
- The installed main fragment includes native `0x800828D4: JAL 0x8003ABC0`. The source setup is `a0=current Wizard`, `a1=3`, `a2=0`, with delay-slot `a3=0`. WAD `0x2F62E90 + 0x14` has bytes `212060020300052421300000f0ea000c21380000`, SHA-256 `ca83fe86286714e440334c486bb64ca44d014dbd616c8bbbafd9d51503081ad4`. The separate fragment at `0x8007D118` is a later animation/deletion tail, not this gem-emission call.
- Shared EXE `0x8003ABC0` reads `row[0x53] & 0x7F`. Selector `0x56` takes the deterministic branch, not the random-drop branch. `row[0x3A] & 0x80` suppresses duplicate emission; the native path sets that bit before emitting. At `0x8003AD58` the native `g800758CC` callback receives class `0x56` and the Wizard pointer.
- Dark's unchanged EXE initialization binds `g800758CC = 0x80082960`. The native class dispatch reaches `0x80082C38` for class `0x56`; its existing delay-slot owner store is retained. The independent source interpreter confirms owner byte `128` for the appended Wizard, rather than inventing a new persistent identity scheme.
- Class `0x56` uses shared PETE slot 2, not a missing Dark-local registration. Its complete registered owner is WAD `0x7EBF24 + 0x108`, SHA-256 `52404b4a45b86d26298f62a8b97fa92703fdd7ca6ea5cbde025e17ac8ce46ea8`. The source-certified loader, PETE header, and complete shared runtime copy bind that owner; no model import is needed for this gem.
- Shared collection code `0x8003BA9C..0x8003BAB8` maps class `0x56` to value **10** and calls the native award routine `0x8003B854`. This means one 10-value gem, not ten gem instances or a synthetic direct award. That native routine updates global/per-level totals and the existing persistent owner bit. The plan does not bypass the native pickup or persistence path.

The exact source windows are retained as immutable `SourceCertificates`:

| Owner | Original offset | Length | SHA-256 |
| --- | --- | --- | --- |
| EXE death emitter | file `0x2B3C0` | `0x51C` | `979e567dd4bebbdd9cadaf8f884e1110be16f293c6590ba6214941be5be7a4a8` |
| EXE Dark spawn binding | file `0x4B058` | `0x10` | `56569d669a6562cd613c558ecd1ce1dfbcd75a179b903f0291474999fdd4294b` |
| EXE collection/value/award | file `0x2BFC0` | `0x310` | `bdaa92cb7c343607f5bbc7462f3bf54743f8f6842bb70b9f74cee06b712038e7` |
| EXE level-slot mapping | file `0x5DE0` | `0x88` | `b6a57847d9e35a80c4002b0afc3e788a70766277f7c14c06eacc0af1e1e60ce9` |
| Dark spawn dispatch | WAD `0xEFDF28` | `0x158` | `5d147c76a31597acc031424b20419b1fc557aa17efb4fe968732a640aa67d14f` |
| Dark gem initializer | WAD `0xEFE200` | `0x158` | `8f72f98f9287f1a16e599b0bf1d9a29eb169ca1396d9e4bad1b7da371b4e48ae` |
| Dark spawn finalizer | WAD `0xEFE7DC` | `0x60` | `7820c5eca67ea422562f19cd658eda6239392a3b367e5968203a4a6e7c312347` |

EXE offsets include the PS-X EXE `0x800` header. The full original EXE is also bound, SHA-256 `a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`. Every EXE/Dark code certificate is checked again in the composed outputs, independently of the overlay plan's full-byte readback.

## Typed output-only target patch

The native level-slot formula maps Dark Hollow ID 12 to slot 2. Its `u16` treasure target is EXE file `0x5FC3C`, initially 100. The internal patch has separate original/final executable address spaces and owns exactly these two bytes. It requires complete `0x66000`-byte EXE owners before copying and refuses wrong offsets, wrong domains, stale/repeated application, and changed readback. Revert restores the exact input overlay EXE, including its copy-buffer relocation, rather than reverting unrelated work.

The public factory requires exactly 129 rows: the preserved 128 native rows and precisely one class `0x11B` at row 128 with source reward `0x56` and unclaimed active state. Future multiple, missing, edited-reward, or consumed Wizard instances are rejected until separately accounted. The detached original overlay profile remains unchanged.

Output hashes:

- Base overlay EXE: `14aa937f198740373b37312eb3054cad46dd1ae05f1920c145d6bc1fde9a8635`.
- Reward-accounted EXE: `ec1e8ae38fed404f9a28ccc7fbf17d81e92a341aab89906e94c5e6c793f4c3b4`.
- Owned overlay remains `10f82ee1028a9ad4fa9045d948a88e76545847031b954ac08ad0e74e6e9c7083`.

## Reproduction and limits

From the repository root:

```sh
node src/Spyro.Editor.NativeMobyDependencyClosureSmoke/source_death_reward_oracle.mjs 'Spyro the Dragon (USA).bin'
dotnet run --project src/Spyro.Editor.NativeMobyDependencyClosureSmoke -c Debug --no-build
dotnet run --project src/Spyro.Editor.NativeMobyDependencyClosureSmoke -c Debug --no-build -- --source-v3 'Spyro the Dragon (USA).bin'
```

The tracked Node oracle reads raw MODE2 sectors without editor assemblies, reconstructs the existing overlay EXE independently, executes the native class/award compare chains with branch and JAL delay slots, and verifies the new complete EXE hash, untouched complement, and exact revert. It writes no ROM or output payload.

The regression-first run stopped at the intended unimplemented typed patch. After implementation, the focused Debug build passed with zero warnings/errors; source-free checks passed **1,358**, and `--source-v3` passed **2,954**, including the new reward output, the particle lifetime resource guard, and unchanged Town V2/V3 and owned-overlay goldens. The raw-source oracle independently reproduced the final EXE hash. Full logs and the JSON oracle result are retained under `_local/research/dark-wizard-native-closure-20260906/death-reward-*`.

This stage does not prove main-helper/global/GTE closure, resource exhaustion behavior, complete transient stack use, placement, native death/collection/persistence gameplay, or publication safety. Native collection still traverses its shared sound/effect and dynamic allocator paths. `CompleteNativeClosureProven`, `RuntimeVerified`, and `PublicationApproved` remain false. The next integrated experiment must observe normal death, one native gem, +10 on collection, and the expected re-entry/persistent-owner behavior; a changed target field alone is not any of those observations.

# F5: source-bound detached visual behavior for USA Artisans

2026-09-25. This advances the [model assembly](japanese-egg-model-assembly.md) to a closed **detached visual code allocation**, not an activated collectible or render/gameplay approval. All source images, emulator state, cards, settings and preserved candidates remain unchanged. The new `NativeJapaneseEggVisualBehaviorPlan` appends code in memory only; every original USA overlay byte, including all dispatch paths, remains exact.

## Exact dispatch ownership, not an assumed unused table slot

JP Artisans overlay22 is WAD `7F4000/F800`, load base `80084A28`, SHA256 `bcfe62569db265135d5627de1902a9d31da82c72f51acdc461d2872e08f7cdf2`. Class F5 uses the native `(class-1)*4` table at offset24; field3F4 contains `8008DE1C`. Its complete handler is overlay `93F4/2CC`, SHA256 `9ce284e21b76b118f3ed22c328e52ca77026ea1c8ecea400d5b0766ca15692da`.

USA Artisans overlay9 is WAD `7F2800/E000`, load base `8007AA38`, SHA256 `7df6cf2f3ed4a71a123f03621019470bd6fe0cd243f75b1e8c3a115e6b1fcb55`. It uses a **comparison tree**, not the JP class table. Source-backed instruction execution shows:

- F5 update starts in the tree at `8007DA9C`, passes `DB44`, `DB94`, `DBC0`, then `DBD4`, and reaches ordinary next-row continuation `80085780`. There is no F5 update handler in this exact dispatcher. The ordinary USA egg22 instead reaches `8007ED6C`.
- The dynamic constructor comparison starts at `80085854`; F5 passes `858E4`, `858F0`, and reaches generic default `800860D4`. That default calls native `8003A720`, copies parent/player XYZ, and finalizes through `800526A8`. It is not an F5-specific 12-byte initializer, and the new plan neither calls nor installs it.
- The previous exact USA class inventory has no placed/local/shared F5 owner; the existing model-tail compiler additionally refuses any local/shared collision in the actual composed resource. This does not claim all unregistered residual code throughout every USA overlay has been audited.

The selected USA update prefix `3064/268` hashes to `dc92ae336bbacdaf983be2d418820d894c74e8165f0a93a7858e7dd9afaff5a7`; continuation `AD48/4C` to `6184b1194497b8c179effe34b9de247ad493df9698c991c4758f05b48138e169`; dynamic tree `AE1C/134` to `054ccd57ef717942731f0558b57cb70ffa07e04f7ea855c1f8cb83c4f8b98adf`. These and their prologue/default owners are mandatory dependency reads.

One narrowly scoped hook candidate is the existing default leaf at `8007DBD4` (overlay319C), currently `j 80085780; nop`. A later typed F5-only guard could preserve all other classes' default behavior there. This plan intentionally leaves it unchanged: guard ABI, whole comparison-tree preservation, runtime allocation and row admission must be proved before activation.

## Three native fragments, no Japanese pickup state

| JP PC | Length | Retained meaning |
| --- | --- | --- |
| `8008DE1C` | `10` | Read row49 state and row0 private pointer; already initialized branches directly to tick. |
| `8008DEC4` | `C` | Capture authored row14 Z into private0. |
| `8008DEE4` | `80` | Initialize row49 to1, then native bob, phase advance, countdown/random target and turn. |

There are 39 retained instructions, two internal branches, four calls, and three explicitly intercepted fallthroughs. `NativeMipsBehaviorFragmentCodec` owns branch/call relocation and delay slots. No original instruction at these omitted ranges is copied into the visual package:

- `DE2C..DEC4`: Japanese device gate, bank/level index and collected-state suppression.
- `DED0..DEE4`: the source's hard-coded level11 height override.
- `DF64..E0E8`: player proximity/state tests, sound/effects, device-state store0x69 and retirement.

The omitted [Japanese pickup gates and persistence](japanese-collectible-runtime-ownership.md) remain nonportable. No ordinary USA egg counter is reused. The user-selected custom-egg requirement for100% belongs to the separate per-instance manifest/save/percentage integration, not this visual slice.

Visual input obligations are an admitted classF5 row with at least the native58-byte row extent, a separately owned 12-byte private allocation, initial row49=0, and initial private phase/target words zero. The fragment stores only row14 Z, row49 timer, private0 baseline Z, private4 phase and private8 target yaw. The turn helper writes row46 yaw. Initializing once retains the authored baseline; each tick adds the native sine sample shifted right6 and advances phase by80. Timer expiry selects a new16..47 timer and0..255 yaw target with two native random calls. These are visual state semantics, not collectible identity or color ownership.

## Authenticated regional helper bridges

The general fragment codec still rejects substituting a different Original address for a JP Original call. Instead, the source issuer declares explicit **new Final jump bridges**, each backed by complete JP/USA routine evidence. The existing overlay materializer proves all bridge entries are actually present, outside the emitted fragment prefix.

| Purpose | JP routine | USA routine | Evidence |
| --- | --- | --- | --- |
| Sine interpolation | `8001623C/58` | `80016C58/58` | Only instruction+8 changes the table address; all202 table bytes match. Adjacent USA`80016CB0` is cosine and is not substituted. |
| Native RNG | `800675A8/30` | `8006272C/30` | Only four address words change from JPseed`8007F670` to USAseed`80075AC0`; arithmetic matches. |
| Yaw turn | `8003C4B4/E8` | `80038EE0/E8` | Seven differences: native frame-step global address, three leaf calls and two internal jumps. |

The sine table is JP EXE`65DAC/202`, USA EXE`5D3F8/202`, shared SHA256 `8c9989b4ded9012234f3b6a3d7d86c70b6633147ca3c41422e75c2e2d4af7803`. The complete source window includes the final interpolation halfword rather than incorrectly claiming a200-byte read bound.

The yaw helper calls byte-identical angle-distance leaves JP`80016EEC/88` and USA`80017908/88` (SHA256 `cde33c4167af361eeecef5ce94f067205cf7baa5f6aa53db728e4836edb26057`), plus angle-step leaves JP`80016FD4/48` and USA`800179F0/48` (SHA256 `719a346d07b7b2e900b2e1a4adc264d599d7e42fcf2eaa371787e078e732399e`). USA turn consumes native frame-step global`800756C4`, which the original per-row prologue establishes. It does not load the Japanese global`8007F21C`.

The source issuer pins both full exact images via `NativeRegionalSourceSession`, both complete overlays, both complete executables, every helper/window hash, and each exact helper difference set. No caller-authored address, similar-looking routine, or hash label can issue a public binding plan. Neutral fixtures have `SourceCertified=false` and carry no native helper identity.

## Detached allocation and ABI

The existing `NativeOwnedOverlayAppendLayout`/materializer appends a fully owned allocation after all original E000 bytes, not after a guessed used length. The native fragments occupy B4 bytes including their three linker-generated jump/NOP tails. Five eight-byte bridges enter the initializer/state fragments or tail-call the USA sine/random/turn helpers. The existing `NativeMipsOwnedFrameAdapter` supplies a98-byte entry and98-byte restoration block.

Total new used code is20C; sector-padded growth is800. Detached entry is `80088BAC`; the proposed full overlay ends at `80089238`. This address arithmetic is **not** a free-RAM certificate. The complete afterimage SHA256 is `51a22794ef0382b8d520b4875fbf3e8477dc97a2463253a3f01481720c7b1963`.

The adapter preserves all GPRs except the deliberately managed SP, plus HI/LO, and restores SP exactly. The source tick's only direct stack operation stores the fifth turn argument zero at outgoing SP+10. The native turn helper pushes28 further bytes. The nominal adapter frame is2B0, so this checked visual call chain reaches2D8 below the original SP; actual runtime stack fit is still unresolved. The final exit returns to the untouched USA `80085780`, which loads its original cursor from original SP+188.

`NativeMobyDependencyClosureCompiler` links this code to the previously authenticated JP model contribution at Artisans data root1C9800. It requires23 behavioral evidence reads, preserves the entire original USA overlay prefix in its real Final domain, and verifies both code and model payload afterimages. The model-data transaction retains its own complete preservation/inverse proof. The overlay exact inverse refuses changes anywhere, including original bytes, code, bridges and padding.

## Verification and remaining gates

Focused Debug build: zero warnings/errors. Source-free smoke: **513 checks**. Exact JP/USA source smoke: **510 checks**, including both held/named image final checks. Logs are `_local/research/japanese-content-20260925-r01.RCmBCn/egg-visual-behavior-r01-{build,sourcefree,source}.log`.

The independent tiny R3000 test interpreter models branch and load delays. Source-free cases use explicitly synthetic helpers; exact-source cases execute the actual USA helper instructions. Each runs64 ticks through the appended package and adapter, verifying all GPR/HI/LO/SP restoration, the untouched caller cursor, baseline Z/phase behavior, native timer/RNG transitions, correct helper targets and a strict byte-level store whitelist: row14/46/49, the12 private bytes, the USA RNG word, and the declared dynamic stack extent. It also directly executes original USA F5/egg22 update selection and F5 dynamic-constructor selection. This is instruction-level regression, not a PS1 emulator, native renderer observation or gameplay acceptance.

Still required before a noncorrupting activated candidate:

1. Joint overlay/model/scene/copy-buffer RAM ownership and actual stack fit.
2. Typed selective dispatch hook and preserved behavior for every other class.
3. Scene/private-row admission and native model/animation/draw-list initialization; no dynamic F5 allocator is certified here.
4. USA appearance/profile/normal bindings and independently selected egg colors; model geometry plus bobbing does not certify visible rendering.
5. Per-instance pickup, retirement, inventory, custom-egg100% accounting and save-extension hooks; none of the Japanese device state is imported.
6. Exact disc/readback followed by cold boot, two same-level eggs, LAB65, color, pickup, death/reentry and save/load validation.

No existing general compiler, source certificate, shared smoke runner, GUI, export gate, ROM or card writer was changed. New focused project: `Spyro.Editor.NativeJapaneseEggVisualBehaviorSmoke`, no arguments for source-free checks, or `--source <exact-JP-BIN> <exact-USA-BIN>` for source-backed checks.

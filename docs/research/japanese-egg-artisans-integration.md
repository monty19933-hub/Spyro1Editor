# Detached two-F5 Artisans integration

This work composes the [authenticated model](japanese-egg-model-assembly.md) and [visual-only code](japanese-egg-visual-behavior.md) with two independent scene instances. It does not import the Japanese device/pickup/save state, touch a disc or emulator, enable native export, or certify visible rendering. Authored test coordinates are structural test inputs, not inspected safe gameplay placements.

## Actual source owners

JP Artisans data23 has SHA256 `7184fff950efa2bdffa37f97875ef7de2a3219480c61ec3ab37f784778d8cfde`. Its scene is data `1D3800/16000`, SHA256 `aa597ffebcb77ca97c62cf67616c1d52ae6c0e81c009a622f02d6dcc97b5413c`. F5 row193 is scene `E418/58`, SHA256 `1d2b9428be365e08b951218f7daf73530b433e28d40b9455b1b6f60e857cc083`; its private owner is `133A8/C`, twelve zero bytes, SHA256 `15ec7bf0b50732b49f8228e07d24365338f9e3ab994b00af08e5a3bffe55fd8b`. The selected native source is animation0/0, visual state0, profile1.

USA Artisans data10 is WAD `800800/383000`, SHA256 `86cde4f886d97821a3a4b869100b586842dbd4b9fb696558bead274bb547af4f`. Its scene is data `1C9800/14800`, SHA256 `6ca481a134fa5e2801b31cfeb97ec22a8bfb56c46cd0990ecc5718ab61a99342`. Native Moby component `A2A4` has174 placed rows and a2800-byte remaining dynamic arena, capacity91 at native stride70. Properties are `1067C..11ECC`; fixup table is `13ED8..14200`, count201; the final600 bytes are zero padding.

Two appended58-byte rows debit B0 from the existing dynamic arena, leaving capacity89. Two separately allocated C-byte private states enlarge the properties component by18. The two new row-pointer fixups enlarge the fixup table by8, so the total final used-tail change is20, with no scene-sector growth. Original rows and properties remain in place; the complete original following pod/collision components and relocation list move by18. There is no per-level save byte or persistent collectible owner in this visual-only schema.

## Exact normal-load memory accounting

The source Artisans initializer is EXE `4AF18/10`, PC `8005A718`, words `3C028009 24428620 3C018008 AC2285D8`, SHA256 `6ba230cc2d8ffb64dd10aa9dee6f86066e3ad1b18d9d05d4aa6f8c3e8a113a34`. It sets the source copy buffer to **80088620**, not the source overlay's complete sector end80088A38.

The existing `NativeLevelRuntimeMemoryReport` parses used level-data8D31C, full model read4D800 and full scene read14800. Original normal read end is8017793C; source-certified normal reserved-buffer lower bound is80187BB0; gap10274.

An E800 overlay ending80089238 requires a **C18** copy-buffer shift, not merely800. Together with the existing model transaction's1000 growth and unchanged scene length, the proposed normal read end is80179554, gapE65C. This arithmetic must use the source initializer, not replace it with a guessed overlay-end starting address.

There is no `NativeRuntimeMemoryBudget`/`NativeRuntimeMemoryProfile` type in this checkout. The existing report deliberately does not certify transient CD staging, transitions, alternate buffer modes, dynamic high-water use or actual stack fit. The visual helper's2D8 below-caller-SP test footprint is not a real runtime stack bound. A positive normal-load gap is never activation or publication approval.

## Render/appearance boundary

Correction: `NativeJapaneseObjectAppearance` pins the STATIC/simple consumer `80023104/134` and its table, not the animated F5 renderer. Values1/9 in these structural placements are opaque row4F modes, not proved colour profiles. Their `ProfileDescriptor` fields are only numerical projections into that static table. The model-owned near/far RGB arrays, not this table, feed animated F5 shading. See [the render admission contract](japanese-egg-render-admission.md). No independent colour claim is made.

Additional read-only correspondence: JP geometry consumer `80020354/174` maps to USA `800215C8/174`, USA SHA256 `4cd236569c340e49360938ad8397aed87f2a7518d13134c935fd7f32acb856a5`; twelve differing words are regional buffer addresses and three internal jumps. JP `80020550/438` maps to USA `800217C4/438`, SHA256 `35c4a1a822af7832fe9eb1f2d484f6022a202d7a77dfdf5c8a61a3d16cda320e`; its ten differing words are same-relative internal jumps. These are useful next consumer bindings, not a complete draw-list/animation-entry or GTE/pixel certificate. The model's packed shading words must not be relabeled as an established array of XYZ normals.

## Existing owners and selective dispatch

`NativeJapaneseEggArtisansIntegrationPlan.Capture` privately authenticates both source sessions, the existing model/visual ancestors, the original USA Artisans resource, the actual JP donor scene/row, and the destination appearance consumer. Callers may supply exactly two distinct bounded placement records, not arbitrary source hashes, handlers, private schemas or copy addresses. Every admission remains visual-only.

The existing `NativeMobySceneAppendPlan` instantiates two one-role templates with no persistent owner field. It deduplicates one immutable model dependency while assigning rows174/175 and private states11ECC/11ED8. The initial model is added exactly once by `NativeJapaneseEggModelAssemblyPlan`; the existing `NativeLevelDataGrowthLayout` then performs an exact-size scene replacement. Replacing the scene with its original complete bytes and invoking the existing model transaction's inverse recovers the complete original resource. The typed row revision changes only XYZ, the existing native yaw-matrix fields, yaw46 and profile4F; all other source row fields are preserved.

The new overlay uses the existing `NativeOwnedOverlayAppendLayout` and materializer. Every previously linked visual instruction stays at the same address and retains its exact bytes. A1C-byte guard follows the prior20C-byte allocation atPC80088C44; final sector extent remainsE800. Original overlay mutation is limited to the eight-byte default leaf8007DBD4. The guard temporarily subtractsF5 fromv1, tests zero, and restoresv1 in the branch delay slot on **both** paths. F5 jumps to the existing full-register adapter80088BAC; every other value continues to80085780 with the same registers as the original tree. No Japanese pickup continuation is reachable through this guard.

The USA native random word80075AC0 is a shared engine service, explicitly retained from the visual helper proof. It is not falsely reserved as an actor-exclusive global or copied into per-egg identity. Visual RNG sequences may affect each other; baseline, phase, target and timer owners remain independent.

The only existing Core modification is a narrow `NativeLevelRuntimeMemoryReport.AnalyzeJapaneseEggArtisans` adapter. It requires the actual privately issued integration ancestor, full retail EXE/PETE certificates, complete original Artisans bytes, exact final afterimages and inverse, the original4AF18 initializer, only its eight-byte patch owner, and the complete E800 overlay end. Fixtures cannot acquire this certificate. All prior report exclusions and false peak/runtime/publication flags remain intact.

## Verification receipt

2026-09-25: focused Debug build and affected dependency/memory-smoke build each passed with zero warnings/errors. New smoke passed **131,193 source-free checks** and **131,188 exact JP/USA source checks**. Most checks are two assertions for each of all65,536 halfword class values: first native handler target and every GPR after comparison/guard execution. ExactlyF5 changes its target. These counts do not represent that many independent gameplay scenarios.

An independent test interpreter also projects the actual scene relocation list, runs both source-backed visual instances for eight ticks each through the new guard, checks private-state noninterference and all-register restoration, and enforces the previous row/private/RNG/stack store whitelist. It does not emulate the PS1 renderer or assert real stack fit. Other cases verify original row/property/fixup and code complements, exact complete inverses, wrong-source/refusal cases, one model registration, independent profile selection and deterministic placement ordering. The existing `NativeMobyDependencyClosureSmoke` passed, including `NATIVE_LEVEL_RAM_USED_DATA_MODEL_SCENE_AND_DYNAMIC_POOL_BOUNDS_OK`.

Exact-source test inputs are `egg-a=(82000,75000,6500), yaw32, profile1` and `egg-b=(83024,75000,6500), yaw96, profile9`. These are detached structural inputs only. Their complete afterimage SHA256 values are:

- Data: `1aab72cead5930c7c7d8eb4471b8de659cf171a5a8bf57d57dfa8331aa807cb1`.
- Overlay: `9b33824cf59231e501e0fe47187bd326aeab6471b023d3c50766e6a821bfbaca`.
- Executable: `c131b171d4365fc5f4075b39d0f16c5492b1fb962df928a991d4ea5a5b09dc31`.

Logs are `_local/research/japanese-content-20260925-r01.RCmBCn/egg-artisans-integration-r01-{build,sourcefree,source,memory-build,memory-sourcefree}.log`. New standalone project: `Spyro.Editor.NativeJapaneseEggArtisansIntegrationSmoke`; run without arguments for fixtures, or `--source <exact-JP-BIN> <exact-USA-BIN>` for authenticated source tests. No shared runner was edited.

`RequireNativeActivation` deliberately refuses. Next work is the concrete native scene-loader/render-list/animation-entry dependency binding and real peak/stack ownership, followed by separately authorized exact candidate publication and runtime observation. Pickup, inventory, required100% and save/card hooks are not part of this plan. No disc, emulator, card, settings or preserved candidate was written or activated.

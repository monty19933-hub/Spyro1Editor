# Native Green Wizard dependency contract

This is a candidate-only capability, not approval for normal Create BIN or a claim of gameplay correctness. The compiler accepts the exact original USA image SHA-256 `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37` (661,547,040 bytes), then reads and separately binds both native scene/data/overlay/resource snapshots. UI labels, saved donor metadata and matching actor IDs are not evidence.

## What actually moves

The selected donor is Wizard Peak T6, actor `0x011B`. Its private 0x50-byte properties record contains a two-point route and one internal pointer. Route coordinates are translated by the difference between the donor and destination spawn positions. Each destination gets distinct mutable storage; identical payloads do not permit sharing or deduplication.

The destination already has the Green Wizard actor and lightning actor `0x0026`, their native resource roots, and native behavior overlay. Those dependencies remain resident and byte-identical. The final instance takes the destination's native Wizard row policy (Wizard Peak T6 or Magic Crafters T107), retaining the replaced slot's XYZ, orientation matrix, yaw byte, detached pod, culling sector and reward byte. No missing model, texture, animation, particle, overlay, or executable code is imported by this path. Both resident dependency descriptors are included explicitly in the candidate plan.

## Typed ownership and fixups

The shared mutable linker receives separate complete donor and destination scene identities. Source-relative pointer values are interpreted in the donor address space; installed values are rebased into the destination's private extent. The source-bound, code-issued ownership authority binds the complete donor/target evidence, schema, owner row, extent preimage, payload and fixup declarations. It cannot be supplied by authored JSON.

The imported Green Wizard payload grammar is:

- `+0x00`: native internal pointer to payload `+0x28`, registered in the donor's native fixup list.
- `+0x04..+0x2F`: scalar state and route header, byte-equivalent to the destination-native Wizard schema; count at `+0x28` is 2 and flags at `+0x2C` are `0xFFFF0000`.
- `+0x30` and `+0x40`: signed XYZ route points at 0x10-byte stride, each with zero trailing word.

Wizard Peak Elder Wizard owners (`0x011D`) have an internal pointer at `+0x0C` to the route header at `+0x1C`. The route count establishes the exact native extent: `0x24 + count * 0x10`. This is decoded structure, not the distance to another pointer or a search for zero bytes. The old registered internal field is replaced by the imported field at `+0x00`. The complete extent must be inside the native properties component, have the declared sole external owner, and be large enough for 0x50 bytes. Unused tail bytes are preserved.

Magic Crafters T27's mover schema is not generalized. Only its exact source-bound 0x74-byte extent at scene `0xE9BC` is accepted. Its existing fixup at `+0x00` is retained, and the retired internal field at `+0x3C` is removed. The native list compacts in original order with a zero trailing word. The trailing 0x24 bytes of the extent remain unchanged.

The linker rejects undeclared inbound aliases, incomplete old/new pointer coverage, overlapping owners, shared mutable consumers, stale source/payload/extent/certificate hashes, invalid alignment, and fixup-envelope aliases. It does not grow a scene, infer allocator space, or authorize runtime behavior.

## Regression scenarios

`Spyro.Editor.Id65AuthoringMobyDependencyBundleCompilerSmoke --source-backed-native-wizard-candidates <original.bin>` checks these independent native-byte goldens:

| Request | Ownership/result | Complete output scene SHA-256 |
| --- | --- | --- |
| Wizard Peak T6 → T17 | Decoded 11-point Elder extent 0xD4, private 0x50-byte payload | `1b5d51f0d999d19baa6f9c5ca17da62e17dba8ad6fbf39b8fb2d3d4a2a72406f` |
| Wizard Peak T6 → T24 | Decoded 5-point Elder extent 0x74; all four previous T24 goldens unchanged | `7a7fa254e8b67c8775cbaa3434707c6e5624ab81cc97ebb931d4fd56a1d560b2` |
| Wizard Peak T6 → T17 + T24 | Two independent mutable owners; one composed native fixup list | `de85a4700b253417f1a4efc60004b8a8664cd7a9612d3372484d57a9f252cb39` |
| Wizard Peak T6 → Magic Crafters T27 | True cross-level properties/route transfer using resident Magic runtime | `68f283f14267dabdb22432cadcdd7108f9b0f350d75fba910ec1c5658bde7d19` |

Every scenario checks typed write reconstruction, the complete protected scene complement, exact inverse, private pointers, preserved tails and placement/reward fields. Reordered batch requests must produce the same plan. Source snapshots and returned buffers are detached copies.

T25's one-point Elder extent is only 0x34 bytes and is rejected. T10 is explicitly retired because prior gameplay experiments failed to produce a lightning attack; structural similarity cannot revive that failed route. Pod-coupled or unknown owners, duplicate/out-of-range targets, and other selectable donors are rejected. The older runtime-approved T24 resident route remains separate and unchanged; this new candidate compiler never inherits its approval, even for a T24-only request.

## Remaining acceptance

New permutations require their own complete-disc readback and focused gameplay evidence: visibility, translated route behavior, lightning attack/hit, damage/death/retirement, exact reward, nearby actor/collision behavior, and concurrent operation for multiple instances. A translated route may intersect unsuitable destination geometry. Source schema equivalence does not prove path suitability or absence of index/pod-dependent runtime behavior. Arbitrary new actor support still requires independently modeled properties ownership, internal references, companion actors, native code/behavior, graphics/VRAM and any audio dependencies; widening a catalog alone cannot provide those.

## Guarded materialization

`Spyro.Editor.NativeGreenWizardCandidateTool` is a separate candidate-only command, not the normal editor's Create BIN path. Its positional arguments are:

```text
--plan <original.bin> <wizardpeak|magiccrafters> <target-indices>
--publish <original.bin> <original.cue> <new-output-root> <wizardpeak|magiccrafters> <target-indices>
--verify <original.bin> <candidate.bin> <wizardpeak|magiccrafters> <target-indices>
```

The donor remains Wizard Peak T6; target indices are `17,24` for the private batch or `27` for Magic Crafters. `--plan` and `--verify` do not write game images. The standalone `--verify` command verifies the BIN only, not companion CUE/input/receipt files; publication itself checks all four artifacts. Publication recompiles the request from the complete original source; it cannot accept an authored plan or hash as authority. It uses one typed Scene → DiscLogical mapping, preserves the original track geometry through the shared CUE helper, and emits an immutable hash-named candidate directory containing BIN, authored input, receipt, and CUE. Existing directories/artifacts are not overwritten. The launchable CUE is committed last by `ExportArtifactTransaction.PublishNew`.

Staged and published output must both match the complete raw-sector afterimages and every protected physical byte, with the original full-image hash independently verified. Headers, subheaders and unchanged user bytes are preserved; EDC/ECC are rebuilt for modified sectors. Complete source and output scene inverses are checked before materialization. Recovery evidence is retained on transaction failure. This assumes a trusted exclusive local output directory and does not claim hostile-filesystem or power-loss atomicity.

`--source-backed-native-wizard-disc-plan <original.bin>` in the dependency compiler smoke uses read-only source-backed overlay streams, not temporary BINs. It covers the batch and true cross-level output, 19 raw-sector/source/complement/parity/length/cancellation rejections, and four early publisher guards. These are static and artifact-integrity checks, not emulator acceptance.

### First materialized candidates (2026-09-04)

Both sets were published through this new writer, then independently recompiled and whole-image verified. Their common repository-relative root is `_local/native-green-wizard-owned-properties-candidates/`; each directory contains `native-green-wizard-owned-properties-candidate.bin`, its `.cue`, `candidate-input.json`, and `candidate.receipt.json`.

| Candidate | Immutable directory / plan SHA-256 | Full BIN SHA-256 | Physical changes |
| --- | --- | --- | --- |
| Wizard Peak T6 → Magic Crafters T27 | `wizard-fb9e28b4a4cbafa652b3df0104b56cc1e3f0b8066ce7bffb103cb19f702d4083` | `4903db5268b2a99cae0a3a5d4718d7cce79a09b9c04e6351ab08fcb80d19da7e` | 3 sectors, 774 bytes including parity |
| Wizard Peak T6 → T17 + T24 | `wizard-4bd9db4e0f03c603a9f618453b73874304b27061072287cca324fff59eef461a` | `81baad21f3a1be0eaa6e85647930342e671c99cfa1530ed6d7ee9bfc3b1b6b68` | 4 sectors, 462 bytes including parity |

Each BIN is 661,547,040 bytes. Original BIN SHA-256 remained `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`, and original CUE SHA-256 remained `05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766`. A repeat publication with reversed batch request order correctly refused to overwrite the existing exact candidate; all eight final files remained intact and no stage/recovery residue remained. Malformed CLI indices and an unsupported target level returned exit 1 with concise diagnostics.

No emulator interaction or gameplay acceptance was performed for these new candidates. Both receipts explicitly retain `runtimeVerified=false`, `normalCreateBinReady=false`, and `appIntegrated=false`.

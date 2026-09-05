# Next native Moby family: ordinary wooden-chest properties

Date: 2026-09-04. Investigation only: no ROM, emulator, settings, catalogue, or exporter changes.

## Recommendation and present limit

Investigate the ordinary Flame/Charge Chest, native class **0x00C2**, starting with **Artisans T50 and T51**. It is the smallest promising *private-properties* case among the five native exemplars inspected. It is **not yet a safe new dependency-bundle family**: the complete properties schema and native initialization/update accesses still need decoding. Do not turn the observed eight zero bytes into an allocator contract.

The genuinely missing operation is importing an independently owned native instance/dependency bundle, not the already available experimental chest-shell swap. `CrossLevelSwapCatalogBuilder.Classify` deliberately describes ordinary chests as shell candidates; `MobySourcePatchExporter` preserves destination properties `+0x00..03` and behavior bytes `+0x38..45`, and refuses authored overrides of those fields. Those existing routes and dedicated chest profiles must remain unchanged. This note does not reclassify every chest as unsupported or authorize generic true-add.

## Source and read-only capture

Read the current original USA image, not the ID65 locked base or an edited candidate:

`/Users/john/Documents/Codex/2026-04-24/could-you-create-a-level-editor/Spyro the Dragon (USA).bin`

Complete length **661,547,040 bytes**; SHA-256 **`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`**. The isolated read-only probe verified that complete hash before reading native WAD records, used the shared checked disc reader and live ISO WAD extent, and rechecked length at completion. Existing caches supplied candidate indices only; their old Desktop source-path strings were not authority.

Local reproduction probe: `/tmp/spyro-next-moby-evidence-oKIW6l/Probe.csproj`. It references an already-built Core DLL, does not build shared projects, and opens the source read-only. It inspected five detailed records in three levels; scanning each selected level's source pointer table established sharing, not a game-wide audit.

All offsets below are **scene-relative**, except actor roots, which are **level-data-relative**. Native rows are 0x58 bytes; class is the full uint16 at `+0x36`, properties pointer is `+0x00`, and legacy associated data is separately `+0x08`.

| Native exemplar | Row offset / properties pointer | Native ownership observations | Resident actor root |
| --- | --- | --- | --- |
| Artisans T50, `0x00C2` | `0xB3DC` / `0x11394` | Only T50 points here; only registered inbound field into the inspected first 8 bytes is `0xB3DC`; no internal fixup in those 8 bytes | slot 9, `0x1AE664` |
| Artisans T51, `0x00C2` | `0xB434` / `0x1139C` | Only T51 points here; only registered inbound field into the inspected first 8 bytes is `0xB434`; no internal fixup in those 8 bytes | slot 9, `0x1AE664` |
| Artisans T0 tower flag, `0x00AC` | `0xA2AC` / `0x10680` | Shared by T0–T7: five `0x00AC`, two `0x015C`, one `0x0153`; all eight row pointers are registered | slot 3, `0x1881F4` |
| Alpine Ridge T148 small swaying tree, `0x01F4` | `0x9DC4` / `0xF0DC` | Shared by T141–T154, including `0x01F3`, `0x01F4`, and `0x01A5` | slot 16, `0x1CBAF0` |
| Blowhard T12 small swaying tree, `0x01F4` | `0x201DC` / `0x24884` | Shared by T12–T25, including `0x01F4`, `0x01F7`, `0x01F8`, and `0x014D` | slot 6, `0x15B4A0` |

Every sampled row has `+0x08 == 0`; that alone says nothing about decoded route/helper links or runtime-created children. Both sampled chest rows have reward byte `+0x53 == 0x54`.

### What is actually decoded versus still unknown

- **Decoded:** exact native rows, full actor classes, properties pointer values, row sharing, resident root/id registration, and the registered fixup membership described above. Artisans has 201 fixups at scene `0x13ED8`; the active count/list SHA is `7d677b0b8d6ad795a6319bd31e781c4e5f52a76be67ed81ed1b1c6fbdb4775fb`, matching the existing source-bound Grass foundation's independently retained certificate.
- **Observed window, not decoded schema:** T50's `[0x11394,0x1139C)` and T51's `[0x1139C,0x113A4)` contain eight zero bytes each (SHA `af5570f5a1810b7af78caf4bc70a660f0df51e42baf91d4de5b2328de0e83dfc`). The next distinct row pointer is eight bytes later. No native length field or class-specific initializer proof was established for either individual record. Zero bytes and pointer spacing do **not** prove an eight-byte object, writable capacity, immutability, or lack of runtime pointers.
- **Useful counterexample:** the same `0x01F4` scenery class has next-distinct-pointer distances of 0x18 in Alpine Ridge and 4 in Blowhard, and both pointers are shared with other classes. A class label, adjacency scan, or zero-run scan cannot establish its properties extent. Artisans T0 similarly shares a zero prefix with unrelated classes. A private mutable ownership certificate for any one of those shared blocks would be rejected by the current linker's inbound-alias guard, correctly.
- **Resource evidence only:** `MobyIdentityClassifier` records the tower flag as a 15-frame model and the small tree as a 17-frame model with default update dispatch. Those are prior identity observations, not fresh instruction-level closure in this investigation. Their graphics/animation packages were not re-decoded here. A nonzero resident root is not proof of behavior, textures/VRAM, sound, collision, or child-spawn closure.
- **Still unknown for C2:** the class-specific properties allocation/serialization grammar; every initializer/update read and write; whether the two words are mutable state or padding; runtime-created pointers; native sound/particle calls and spawned reward ownership; death/re-entry/save behavior; destination runtime compatibility. No overlay or executable patch is justified by this note.

Capture pins for subsequent regression fixtures:

| Owner | Verified native identity |
| --- | --- |
| Artisans data entry 10 | WAD `0x800800`, length `0x383000`, SHA `86cde4f886d97821a3a4b869100b586842dbd4b9fb696558bead274bb547af4f` |
| Artisans scene | data `0x1C9800`, length `0x14800`, SHA `6ca481a134fa5e2801b31cfeb97ec22a8bfb56c46cd0990ecc5718ab61a99342` |
| Artisans overlay entry 9 | WAD `0x7F2800`, length `0xE000`, SHA `7df6cf2f3ed4a71a123f03621019470bd6fe0cd243f75b1e8c3a115e6b1fcb55` |
| Artisans T50 / T51 rows | `f9590fda039363e5af9466630121efb876ce0a202226706d030f1ab503418988` / `5c6f987340c32d6af27024c0dd305b4d64a4cb0c8905e95d3509aebaf0e4c05b` |
| Alpine Ridge scene | data entry 36, data offset `0x1DB000`, length `0x12000`, SHA `3dba83219b5e70e3a6c821ffc59b26643ca13be75d27581ebe9c421d8f4a46a6` |
| Blowhard scene | data entry 42, data offset `0x16B000`, length `0x27800`, SHA `8f71694352da58b34093d7c7422be5e08cc4b7bfc449fd95185433b25d1ae388` |

## Bounded regression-first next step

1. Add a **read-only C2 schema/behavior certificate**, initially refusing to issue mutable ownership. Trace the exact original loader/initializer and C2 update path, pin executed code and call targets, and derive the serialized extent and complete field grammar. Retain the T50/T51 row/fixup observations as positive readback tests. Add negative cases for fabricated eight-byte lengths, stale source/scene hashes, and a hidden registered inbound pointer. If full extent or accesses cannot be proved, stop at that evidence boundary.
2. Only after that certificate exists, add a **pure existing-owner C2 properties adapter** to `NativeMutableScenePropertiesLinker`. The first T50→T51 native control may be a byte-identical no-op; call it that, not a new working import. Prove complete before/after equality or inverse, protected complement, exact donor/target domains, and preserved destination runtime/root/placement/reward. A second target requires separately decoded ownership. Equal zero payloads must never deduplicate mutable owners. Include shared-reservation, undeclared-alias, changed extent, unknown-pointer and insufficient-capacity rejection tests.
3. Keep independent acceptance gates for a useful cross-level/new-slot candidate: full destination dependency certificate, legal row/properties/fixup capacity, whole-disc readback/publication, then gameplay interaction/reward/re-entry/concurrent-instance tests. The current mutable linker intentionally accepts **existing registered consumers only** (`AppendFixup == false`) and allocates no new scene space. A true-add feature therefore needs a separately proved append/allocation contract; this adapter alone cannot enable it.

The scenery alternatives may eventually use the immutable-sharing linker after exact passive-dispatch and asset closure, but their native aliasing makes them poor first exercises of the new private mutable linker. **No family examined here is ready to have its gate relaxed.**

# Physical Moby rows and native saved identity

Permanent removal reclaims physical placed-row capacity while retaining each surviving logical saved identity. Imported logical IDs remain original native prefix plus persistent ledger slot. Retired logical IDs are never reused. The native 36 × 256 bitmap, serializer, checksum and save schema are unchanged.

`NativeMobySaveMap.Append` attaches an explicit ushort physical-to-logical table and source-owned callable leaf after actual staged resident overlay bytes. `FFFF` is an inactive hole. Its bound update wrapper carries `SMAP` at +8 and leaf pointer at +12. A verified sign marker/jump/NOP prefix is copied exactly to the new update −12 range. Resident growth and the new copy buffer are returned to the normal final RAM gate. Identity maps are a no-op.

The shared gateway at `8003B9A4` occupies the last 48 bytes of the rescheduled original collected-write function. It changes only a2/v0/v1/at, preserving all other registers, and returns identity for a native update without metadata. All original owners are SHA256-guarded together before mutation; canonical after-images are idempotent. No generic code cave or unused row field is claimed.

| Original owner | Bytes | Behavior preserved or translated |
| --- | ---: | --- |
| `8003B7C0` | 148 | Physical placed deletion becomes logical live/checkpoint bit; dynamic pointers remain excluded |
| `8003B854` | 384 | Native award/audio retained; placed ordinals and spawned physical-parent `+56` tags translate to durable logical bits |
| `8001417C` | 292 | Hole skipped before any model/position/collision initialization; checkpoint type summary reads logical bits |
| `800142C8` | 316 | Native durable/checkpoint/kind suppression policy reads logical bits; physical iteration and original 32-byte checkpoint copy retained |
| `80038494` | 136 | Native checkpoint/reward Boolean query reads logical identity; hole returns false |
| `800142B4` | 4 | Original empty-table branch targets the exact relocated checkpoint copy entry at `800143DC` |

Direct EXE bitmap reference audit also covers whole-buffer clearing, checkpoint capture, restore, serializer/deserializer: these copy or clear whole logical arrays and need no translation. Spawn callbacks retain physical parent tags; the shared award reader translates them. A 35-source-overlay immediate/reference census found inline checkpoint calculations in raw overlay entries33/35 (Magic Crafters/Alpine Ridge). Original native survivors keep their physical indices and identity mappings; relocated imported behavior using those inline calculations requires the behavior composer's source-bound rewrite or explicit absence proof. Current complete placed/registered catalog extraction reports verify both query blocks absent from all selected update/specialized-init packages, without extraction failures. The census does not certify arbitrary indirect aliases.

Independent strict scalar tests execute original/emitted native instructions with branch and load delays, integer division and exact mapped RAM ownership. The six-window run passed 6,065 checks across 2,209 cases, including all256 identity collector/deletion rows, checkpoint policy boundaries, constructor-hole skips, logical IDs255 and word boundaries, spawned parents, all seven mapped egg colors, physical0→195 and196→197, isolation and reconstruction after collection. Native word-copy executes exact original instructions. Award/audio/unlink/constructor/position/collision helpers are explicitly labeled ABI effect doubles; these tests are not emulator or memory-card acceptance.

Reproduce after a Release build:

```sh
dotnet run --project src/Spyro.Editor.NativeMobySaveMapSmoke -c Release
python3 tools/Test-NativeMobySaveMap.py
# Optional exact original-source preimage verification; read-only:
python3 tools/Test-NativeMobySaveMap.py --source-image /path/to/original-USA.bin
```

An independent EXE/35-overlay direct jump/branch census found only one exterior reference into the five relocated functions: the empty-table branch at `800142B4`. Its exact native BEQ now targets the asserted emitted copy entry; four empty checkpoint patterns compare with the original native execution. Indirect pointers are outside this census.

The standard regression runner includes both source-free smoke and scalar execution. Native card save/cold-load and gameplay acceptance remain separate receipts owned by the release runtime verification.

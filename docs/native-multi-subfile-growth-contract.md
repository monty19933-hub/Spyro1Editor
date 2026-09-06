# Typed multi-subfile level growth

`NativeSkyWadRelocator` now accepts several growing nested subfiles in the same
level entry. The original single-subfile path is unchanged. The new path uses
`NativeLevelDataGrowthLayout` to decode the native header and the shared
`NativeRelocationMap` to remap model roots; it never treats arbitrary header
words as pointers based on their numeric value.

This is outer byte geometry, not a portable Moby compiler or gameplay proof.
The caller must still discover and certify every internal scene/model/code
reference, insert registered native fixups, supply complete replacement
payloads and satisfy RAM, VRAM, SPU and executable/behavior obligations.
Growing an outer subfile does not discover its internal reference schema.

## Source structure and restrictions

A fresh read of the exact original USA BIN checked all 35 retail level headers:
221 contiguous, sector-aligned offset/length descriptors; 1,403 contiguous,
ascending model roots; 22–53 roots per level. Every root belongs to model
subfile 2. Unused root/class pairs and all bytes after `0x1D0` are zero.
The concatenated 35 original `0x800`-byte headers have SHA-256
`ede176a1ed8158ba18bcbb3034567950a55e2d929eb9cf2d24eb170e1ce25ba9`.

- `0x00..0x4F`: ten offset/length pairs. The first four are VRAM/SPU,
  level data, models and scene; the remaining six describe dragon blobs.
  Populated descriptors cover the complete entry in order; trailing pairs
  must be zero. Each descriptor offset and length is sector aligned.
- `0x50..0x14F`: 64 level-relative u32 model roots. Slot 0 has the native
  special player-animation-stream semantics; this writer only relocates its
  address. It does not interpret it as an ordinary Moby model package.
- `0x150..0x1CF`: 64 u16 class IDs. These remain scalar values, byte-identical.
- `0x1D0..0x7FF`: unsupported reserved fields must remain zero. This requirement
  is a supported-header schema check, not permission to allocate zero bytes.

Every changed subfile gets its own rounded-up `0x800` growth. Three four-byte
appends therefore consume `0x1800`, not one shared sector. Later descriptors,
model roots and outer WAD/EXE positions account for all preceding subfile
growth. Appended payload padding is new storage only; existing source tails
are copied in full. Root registration/table expansion is a separate producer.

Payloads are sorted by subfile and source offset. Duplicate identities,
overlapping source extents, duplicate insertion points, shrinking/misaligned
payloads, stale source coordinates and incorrect aggregate growth fail.
Existing roots intersecting replaced model bytes fail because a new package
binding cannot be inferred. An inserted prefix moves existing roots while the
descriptor continues to anchor the complete output subfile.

The existing staging writer validates every expanded entry before creating
output, keeps its original ISO/gap ownership checks and preserves an existing
output on failure. Source hashes and the original installed app are unchanged.
The helper is not a new standalone writer or normal Create BIN promotion.

## Regression evidence

The new simultaneous data/model/scene fixture first failed through the actual
frozen Core DLL with `A nested-entry relocation batch must target exactly one
subfile.` The frozen DLL was retained at
`/tmp/spyro-multi-subfile-baseline.QgCcM8/Spyro.Editor.Core.dll` during this run.

`Spyro.Editor.NativeRelocationMapSmoke` now exercises the new branch with an
independent literal splice/header oracle; malformed descriptors, roots, IDs,
reserved fields, overlaps, source bounds and late length-prefix failures;
multiple growth sizes up to a 1 MiB payload; and actual 2048/2336/2352 disc
writer output. The disc cases verify patch positions, complete nested-entry
readback, relocated executable equality, protected physical sectors,
independent EDC/ECC, unchanged source and retained output after a late failure.

`--native-header-audit <original.bin>` is read-only. It verifies the complete
original BIN SHA-256, expands data/model/scene tails in memory for every retail
level, and independently checks every original subfile complement, descriptor,
root, inserted tail, class ID and reserved header byte. It passed 2,859 checks.

`--single-subfile-baseline` produced identical complete synthetic-entry hashes
against the frozen and current Core DLLs for all four first subfiles:

| Subfile | SHA-256 |
| --- | --- |
| 0 | `956f2a89f161147bbe3fc051f582c5ac52ec76fa2d2c1d68657bbf23947b55b5` |
| 1 | `b25d39fd494bf2f2b75f7d0349e15006dbc843d4de3de32af7f528279af71ff2` |
| 2 | `9edefd38b02ee343dca418f932c9d9eb162864431f631f8bb02c9531257bae0c` |
| 3 | `bcbe688fd938983e9a16e80338dd07e09edddb9980be858889db976d0c6b195a` |

The adjacent test-only warp smoke correction distinguishes its eight payload
byte changes from 68 required EDC/ECC changes and independently verifies the
entire raw-sector trailer and protected disc complement. The production
`TestLevelWarpPatch` behavior is unchanged.

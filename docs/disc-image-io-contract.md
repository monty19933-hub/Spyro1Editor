# Disc image I/O and CUE contracts

Normal exporters must use `DiscImage.ReadFileBytes`,
`ConvertFileOffsetToImageOffset`, and `WriteFileBytes` for logical file bytes.
The common logical layouts are cooked 2048-byte sectors, headerless MODE2/2336
(8-byte user-data offset), and MODE2/2352 (24-byte user-data offset).

## Logical writes

The writer checks the complete requested range before mutating the stream. It
rejects negative or overflowing addresses, unsupported layouts, truncated sectors,
and raw sectors that are not valid MODE2 Form1 containers. Every touched raw sector
is preflighted, including the last sector of a multi-sector request. The writer
updates EDC/ECC itself; callers must not depend on a separate repair pass. Existing
stale parity is permitted because the touched sector is rebuilt. Untouched sectors
and non-payload/non-parity bytes are preserved.

This is validation-before-mutation, not an operating-system transaction: a device
I/O error during a write can still leave a partial output. Export into a disposable
candidate, verify it, then use the publication transaction for the final artifact
set. Never use a preserved source image as a writable output.

## CUE publication

`DiscCueSheet.BuildForImage` binds the selected data image by its exact source path,
not by the first `FILE` declaration. All production callers pass source identity
explicitly. If an exporter consumes/moves a disposable input, pass its live output
as `layoutImagePath`; the original path still identifies the CUE declaration.
The normal UI coordinator builds CUE text before moving its final intermediate.

Track, index, pregap, and comment lines are preserved. The selected data file must
begin with a data track at `INDEX 01 00:00:00`: stored pregap sectors or leading
audio within that same file require an offset-aware image abstraction and are
rejected. Synthetic `PREGAP` and subsequent track geometry remain unchanged.
Both existing MODE1/2048 and MODE2/2048 declarations are valid for cooked images;
when no source CUE exists the deterministic default is MODE1/2048.

Other CUE files, including separate audio tracks, must exist and are rebound to
absolute source paths. They are not copied. Such outputs remain locally playable
but are not self-contained redistribution packages. UTF-8 without a BOM preserves
Unicode paths and keeps existing ASCII retail CUE output bytes unchanged.

Discovery skips malformed neighboring CUE files; explicitly selecting a malformed
or ambiguously multi-data-file CUE remains an error. Selecting an image explicitly
can bind it within a multi-file CUE without guessing which data file the user meant.

## Executable patches

Moby executable patches use runtime addresses mapped through the live ISO file
extent, not hard-coded physical image offsets. They require a PS-X EXE header with
load base `0x80010000`, and patch bytes must lie within the declared loaded text.
The treasure target table is specifically verified for USA `SCUS_942.28`; both
planning and applying a treasure patch enforce that region contract. Runtime
compatibility with other versions is not implied by a matching filename/header.

## Regression gates

Run these source-free checks from the repository root:

```sh
dotnet run --project src/Spyro.Editor.DiscImageWriteIntegritySmoke
dotnet run --project src/Spyro.Editor.DiscImageReadBoundsSmoke
dotnet run --project src/Spyro.Editor.DiscCueSheetSmoke
dotnet run --project src/Spyro.Editor.MobyTreasureExportSmoke
dotnet run --project src/Spyro.Editor.MobyWadReaderSmoke
```

The write smoke has independent bitwise EDC and finite-field ECC oracles. Its
optional image-path argument exercises the public four-level music exporter,
checks every changed sector and the whole-image untouched complement, verifies
source hashes, and removes only its own temporary output. The CUE smoke's optional
image-path argument compares current retail CUE output against the independent
legacy rewrite and checks that the original CUE is unchanged. These are file-level
acceptance gates; emulator gameplay is a separate test.

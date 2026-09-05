# Source-owned WAD growth allocation

This is a structural byte-correctness gate, not permission to run an arbitrary
private texture candidate. Existing editor capacity, composition and runtime
promotion policies remain unchanged. It is separate from the byte-equivalent
[typed relocation extraction](native-relocation-map-contract.md).

## Root cause and supported operation

Retail WAD.WAD ends immediately before the executable. Growing the WAD moves that
executable into the following unused gap. The gap is canonical zero-payload
MODE2 Form2 padding in the supported retail BIN; an ordinary Form1 logical writer
must reject it. Previously, relocation copied the output and partially wrote it
before reaching that gap. Moving only logical bytes also stranded the old WAD/EXE
EOF/EOR flags at their former physical boundaries.

`NativeWadGrowthSectorPreparation` now owns that narrowly defined allocation.
It does not make `DiscImage.WriteFileBytes` convert Form2/XA sectors.

Before output creation, the actual `NativeSkyWadRelocator.WriteExpandedImage`:

- Resolves both input and output paths, including parent symlinks and platform
  case aliases, through existing persistence path safety.
- Snapshots payloads, checks original WAD/subfile ownership, recomputes the current
  structural plan, and requires every submitted scalar, entry map and patch map
  to agree. It does not silently replace a stale submitted plan.
- Expands every affected nested entry before any output exists, so a bad late
  length prefix or pointer cannot partially publish an earlier entry.
- Proves exact whole-sector WAD/EXE bounds and the following ISO allocation limit,
  including the entire relocated executable, against a bounded recursive directory
  inventory. Legal retail SOURCE/S0 directories are supported; unknown record flags,
  multi-extent/interleaved files, directory cycles/aliases and malformed endian
  fields fail closed. All descendants and directories participate in overlap checks.
- Checks primary/terminator descriptor ownership, volume bounds, and every mandatory
  or optional LE/BE path-table extent. Path-table directory identities must agree
  with the complete directory inventory; metadata cannot live in consumed storage.
- Requires the supported raw source-owner contract: duplicated Form1 data
  subheaders, zero file/channel/coding bytes, and EOF/EOR only at the exact original
  WAD/EXE last sectors. Noncanonical owner metadata is refused rather than relocated
  under an unsupported assumption.
- Checks every newly allocated sector before mutation: zero cooked2048 data, or
  canonical zero-data raw2352/raw2336 padding with valid Form1 integrity or Form2 EDC.
  Sync/MSF and duplicate headers are checked when present. Nonzero XA data is never
  consumed, even if an ISO record would appear to leave a gap.

On a newly created same-directory staging image copied from the held source stream,
all affected physical preimages are checked again before initialization. Only the
approved padding becomes Form1. Only EOF/EOR bits move from the exact old file ends
to the exact new ends; coincident old/new boundaries are handled together. The
shared integrity encoder repairs each emitted raw sector. Logical relocation then
uses the unchanged strict writer, verifies ISO and complete executable readback,
and replaces the requested output only on success. Failed work removes only its
own staging file and retains any existing output. These are local accidental-write
guards, not an adversarial filesystem-race or crash-durability guarantee.

The plan is structural geometry, not a new universal source/content authority or
an arbitrary resource linker. Existing compiler preimages, hashes and promotion
requirements still apply. Filesystem variants outside this bounded inventory and
metadata contract remain unsupported.

## Regression and source-backed evidence

`dotnet run --project src/Spyro.Editor.WadGrowthGapSmoke -c Debug` reports
`WAD_GROWTH_GAP_SMOKE_OK checks=3349`. The frozen pre-fix implementation failed
through the actual writer at synthetic raw sector 45. Tests cover 2048/2336/2352,
one/two/three-sector growth, mixed Form1/Form2 padding, four distinct boundary flag
pairs, shared old/new boundaries, legal child directories, stale plan fields,
late malformed data/metadata, input aliases and unchanged preexisting outputs.
The test oracle uses separate bitwise EDC and GF arithmetic, not the shared encoder.

The real `TextureRecordSectorRelocationSmoke` and `PrivateTextureBatchCompilerSmoke`
now pass, including fixed/fixed, fixed/+0x800 and two +0x800 destinations. A separate
read-only streaming pass checks every changed physical sector with the independent
oracle, the entire untouched complement, the complete relocated executable, and
the original source SHA256:

| Export | Growth | Changed sectors verified | Output SHA256 |
| --- | ---: | ---: | --- |
| Stone Hill single | 2048 | 47,015 | `f7d3e6ef6f23984ac50e3405861495e0f9db99e43c5ccac2d2af4767512597cc` |
| Stone Hill/Magic Crafters mixed | 2048 | 47,055 | `0290976c0c934c541ce70c054af58da4757f76334d489dca30cd4f05dfbc9620` |
| Stone Hill/Dark Hollow two-sector | 4096 | 47,152 | `009853400151c5723941f2927442513b7f8d4b511a1dc8cb1a66c0770134e0e4` |

Original BIN SHA256 remains
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`;
original CUE remains
`05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766`.
The two completed same-size private-batch BIN hashes remain exactly equal to their
frozen pre-extraction baselines. The separate fixed-tail smoke's historical pinned
hash mismatch predates this allocation change and is not promoted or rewritten here.

To repeat the independent raw source readback, pass explicit source and output
paths to the same smoke executable. This is read-only and requires the identified
retail source. These exports remain structural candidates: no new DuckStation,
universal cross-level texture, or gameplay approval follows from these byte checks.

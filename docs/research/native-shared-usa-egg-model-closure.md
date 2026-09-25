# USA shared egg22: bounded geometry and decoder plan

2026-09-25, source evidence and a separate bounded decoder. The investigation
phase was read-only; implementation followed explicit acceptance of the plan.
No emulator operation, original-image mutation, UI enablement, or runtime/import
approval was performed. This is ordinary USA class22, not Japanese dragonfly F5.

## Exact source owners

The existing USA source profile remains the authority: full image SHA256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
The original image was opened read-only and checked before/after inspection,
including the named original. Existing complete PETE owners were rechecked:

- WAD `7B8800/800` header:
  `b9a4de193b5cf0bd9592de15c57aa5daa3713c3575026154b45402f3aceef6f1`.
- WAD `7B9000/39C48` native runtime-copy data, including its established `448`
  overhang beyond the directory allocation:
  `1703ac37e59e480909b6f66dc856d4e5f3ce80551f18cbc8c06129c3b54dd35d`.
- PETE slot3 registers class22 at root `3382C` (payload-relative `3302C`, WAD
  `7EC02C`). Header word=`FF127375`; root-relative pointers +4/+8/+C are
  `10/170/224`. This is the negative simple-model branch, not an animated model.

## Native-derived logical package

| Owner | Root-relative range | Derivation |
| --- | --- | --- |
| Header | `000/010` | Negative model word, three root-relative pointers |
| Vertices | `010/15F` | Signed header byte0=117, three signed bytes each |
| Alignment | `16F/001` | One original zero byte to word boundary |
| Packed RGB | `170/0B4` | All45 entries0..44 referenced by the face consumer |
| Faces | `224/398` | Header byte1=115; fixed8-byte commands |

The logical end is `5BC` (1,468 bytes), SHA256
`b4cf4f6265b275450c5e039d0096f723064057612df6faa9bf9ab46ebff50714`.
It is derived from counts/consumers, **not** from the next registered root.
All117 vertices are referenced. All115 faces are quads, all face flags are0;
there are no texture descriptors in this admitted simple Gouraud path.

Range SHA256s:

- Header: `c137863b85cbcf7d7044b5c3d9a1ca411181c7ff2e052a63e2db276880281716`.
- Vertices: `131d19958c71b885a685815bcb5e1c3f53d32631ce58a7bd21f34a8fd2c16af2`.
- RGB: `8303997e60554a81a4d5d8be62742e45eb79994d14afd2875381b5352508fcb3`.
- Faces: `b7ccc3761e094326f740a2a12082196785ddbac2c72c401d587fa0c6880879ad`.

## Consumer and look-ahead ownership

USA loader `800133E0/26C`, EXE `3BE0/26C`, SHA256
`fede3c414aea2dce0372fce574549d961123cf6456a7a291acef48b9c4af8598`,
takes the negative branch, rebases exactly root+4/+8/+C, and returns a low
physical root pointer. The delay-slot write is part of this loader contract.

Simple vertex consumer `80022FEC/11C`, EXE `137EC/11C`, SHA256
`74978cee8e81a0ca2062f9242b53073591e9f830d35eae77ad36ea74852459ca`:

- `80022FEC` reads root+4; `80023008` signed-loads byte0 as vertex count117.
- `80023010..24` sign-extends each packed byte and doubles its value before GTE.
- `80023044/48` use LWR/LWL for the next unaligned triple and advance3bytes.
  The final iteration preloads four bytes at `16F..172`; the underlying aligned
  memory words lie at `16C/8`. These reads fit owned vertex/alignment/RGB bytes.
- `800230EC..FC` compares root+10 with the vertex pointer. Equality for egg22
  bypasses the per-vertex normal/profile path. There is no normal array before
  these vertices in this model.

Simple face consumer `80023268/6F0`, EXE `13A68/6F0`, SHA256
`c9cef2546764b5b14a58c4d9bc33a938512d378758258b97a469ef900f80cfa8`:

- Header byte1 counts fixed8-byte faces; first word contains four7-bit vertex
  indices at shifts23/16/9/2. Second word contains corresponding RGB indices at
  the same shifts. Equality of third/fourth vertex would select a triangle;
  all115 egg commands have distinct third/fourth vertices.
- All first-word low2bits are0. The indexed RGB path consumes the45-word
  table; profile/normal face branches at `80023534` and `80023720` are absent.
  Thus the verified JP F5 per-instance shader-selection contract must not be
  assumed to recolor ordinary USA22's original Gouraud mesh.
- `8002332C` (or culled-face `8002394C`) preloads the word at the next command;
  loop `800232A8` tests the end, with a delay-slot load at end+4. Hence the
  native renderer reads **eight bytes beyond the logical model end**.

The original next8bytes at `5BC/8` are `0E1236FF10000000`, SHA256
`76df2a96a85a7aedab18e72c11dd27bc9fb7948518fdea7817ddf6a72096fd4c`.
Their independently identified owner is PETE slot4/class54, root `33DE8`, not
part of egg22's logical package. Complete source read-window `0/5C4` SHA256 is
`a587c0fa7ef06bc76812929af86691b90f6731a184bf9dfb0bfc4cb914bed2ce`.
The preloaded values are discarded by the end branch; importing all of class54
is not implied. A future assembly must explicitly provide mapped readable tail
bytes and preserve this read-bound contract rather than silently truncate it.

## Compatibility with the Japanese animated geometry decoder

The ordinary egg is not valid input to `NativeJapaneseEggModel`; that public
capture should remain bound to original JP F5. A separate simple grammar is
needed. This distinction does not mean USA cannot consume the JP animated form:

- USA loader `800133E0/26C` and JP `800128D8/26C` differ at just2 of155 words,
  both relocated absolute jump targets (+44,+1C0). All pointer-format words are
  identical.
- USA animated vertex consumer `80021138/474`, EXE `11938/474`, SHA256
  `11c407e46e92faa2fa65b8b1f9689a99d00eb56aaafcbb25ceefd3fd2a9ebc22`,
  was found by exact packed-stream instruction signature. Compared with JP
  `8001FEC4/474`, 14 of285 words differ:10 relocated jumps,2 scratch-buffer
  address words, and2 screen-clipping constants. Packed absolute/delta decoding,
  output count, stream partition and pipeline look-ahead instructions match.
- Clipping constants differ: USA LUI1/100 versus JP LUI9/E8. Therefore grammar
  agreement is not visual/GTE/runtime equivalence or destination approval.

## Implemented bounded slice

1. A separate bounded simple-Gouraud geometry decoder: signed positive byte
   vertex count1..127, negative header, three root-relative pointers, no normal
   prefix, fixed8-byte faces, flags0, bounded 7-bit indices and RGB extent from
   actual consumers. Unknown normal/flat-shaded variants refuse explicitly.
2. Exact-USA shared22 capture through `NativeRegionalSourceSession`; retain the
   complete PETE-copy identity, class registration, consumer hashes, logical
   package, and a separate read-ahead record with class54 source provenance.
   No adjacent-root-derived size and no changes to broad inventory approval.
3. Source-free truncation/index/alias/normal-prefix/flag/read-ahead tests, then
   exact-original source capture and byte readback. Keep logical geometry
   closure, native read-window closure, and runtime/import approval distinct.

The separate files are `NativeSimpleGouraudModelGeometry.cs` and
`NativeUsaSharedEggModel.cs`. The decoder handles the admitted structural subset
without source authority. The issuer binds original USA source and native
consumer evidence through a private constructor. Neither changes the existing
Japanese F5 capture, shared inventory approval, or animated texture decoder.

Dedicated project: `src/Spyro.Editor.NativeSimpleMobyGeometrySmoke/`.
Its no-argument suite uses synthetic bytes only; `--source <original-usa.bin>`
captures and byte-compares the authentic shared egg and its distinct read-ahead
owner. Success markers are `NATIVE_SIMPLE_MOBY_GEOMETRY_SOURCE_FREE_OK` and
`NATIVE_SIMPLE_MOBY_GEOMETRY_DISC_OK` respectively.

After the parent's regression released its DLLs, the dedicated Debug build
passed with zero warnings/errors. The source-free suite passed **105 checks**;
the exact-USA source suite passed **21 checks**, with `originalFinal=true` and
`candidateWrites=0`. No failure-driven code change or broad-suite rerun was
needed. Logs are retained under
`_local/research/japanese-content-20260925-r01.RCmBCn/`:

- `simple-geometry-focused-r01-build.log`
- `simple-geometry-focused-r01-sourcefree.log`
- `simple-geometry-focused-r01-source.log`

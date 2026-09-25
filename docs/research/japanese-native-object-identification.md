# Japanese native egg, sign, text and appearance evidence

2026-09-25. Read-only source evidence and implemented byte contracts. This is
not a Japanese-object port certificate, candidate disc or runtime acceptance.

## Source identity and reproducibility

The supplied original is
`/Users/john/Desktop/Spyro the Dragon (Japan)/Spyro the Dragon (Japan).bin`,
727179600 bytes, SHA256
`1bcf4353d6a4d4d0db152d2ebd60879ef85516feaf170f364ff419c54f599ea7`.
`SCPS_100.83` is ISO LBA24, 0x70000 bytes, SHA256
`a67c21f441deb3a4df26099d78ddaa29cc66b927a7387718a678eee71664cee4`.
`WAD.WAD` starts at ISO LBA500. These offsets use MODE2/2352 sectors with 24-byte
payload offset and 2048 logical bytes per sector.

The original was opened read-only. The new inspector verifies both the held
handle and the named original before publishing its report. The Core source
session independently verifies the ISO records, selected revision, executable,
directory and final held/named original. No original image, editor state,
emulator settings, R06/R07/R08 artifact or old `_local` result was modified.

`tools/Inspect-SpyroJapaneseObjects.py` produces a deterministic metadata report
for explicit level-data entries and can print bounded MIPS evidence windows.
Its small disassembler leaves unsupported instructions as explicit raw words.
`--output` creates a new file exclusively; it cannot replace an existing file.
`tools/Test-Inspect-SpyroJapaneseObjects.py` has nine source-free checks.

All 35 entries were inspected with:

```text
python3 tools/Inspect-SpyroJapaneseObjects.py <original-japan.bin> \
  --entries 23,25,27,29,31,33,35,37,39,41,43,45,47,49,51,53,55,57,59,61,63,65,67,69,71,73,75,77,79,81,83,85,87,89,91 \
  --output <new-report.json>
```

The actual report is
`_local/research/japanese-content-20260925-r01.RCmBCn/japanese-objects-source-evidence.json`.
It contains every selected row's native bytes, source offsets, hashes, private
text, animation headers and bounded near/far face-command observations.

## Identified classes and placed census

| Native class | Identified behavior | Placed rows | Registered levels |
| --- | --- | ---: | ---: |
| 0x47 | Message sign, independently referenced native text | 330 | 30 |
| 0xF5 | PocketStation dragonfly-egg collectible | 31 | 30 |

There are no selected objects in the five flight data entries33,45,57,69,81.
Every other level has one F5 row except Terrace Village (entry61), which has
two. Gnasty's World (entry83) has one native F5 placement. These are source-row
counts, not a claim that every placed object is visible under every native
PocketStation/save condition. In particular, distinct placements do not prove
distinct native persistence identities.

Artisans data is entry23 at WAD0x803800, SHA256
`7184fff950efa2bdffa37f97875ef7de2a3219480c61ec3ab37f784778d8cfde`.
Its scene is data0x1D3800/0x16000; Moby component is scene0xA1B8/0x6BC0
with197 rows; properties component is scene0x10D78/0x27A4.

| First exact example | Row | Row WAD offset | Property scene offset | Model data offset |
| --- | ---: | --- | --- | --- |
| Sign0x47 | 173 | 0x9E4D38 | 0x12610 | 0x1CC4F4 |
| Dragonfly egg0xF5 | 193 | 0x9E5418 | 0x133A8 | 0x1CCF5C |

Row173 SHA256 is
`476c59a99224be05962a1b8fb19197806874f5bb483a24e9683fbbf24df5d503`;
row193 SHA256 is
`1d2b9428be365e08b951218f7daf73530b433e28d40b9455b1b6f60e857cc083`.
USA class0x22 is the ordinary retail egg, and 0xFA/0xFB already occur in USA;
neither is substituted for these newly identified classes.

## Native dispatch and text ownership observations

Artisans overlay22 is WAD0x7F4000/0xF800 and loads at0x80084A28.
The independently traced loader reads that base from EXE global0x800106F0;
the overlay's internal default branches agree with the same base.
PC0x80087F00..0x80087F3C reads row+0x36, subtracts1, bounds at477 and
indexes the pointer table at0x80084A4C. Class47's slot is overlay0x13C,
target0x80089A74 (overlay0x504C). ClassF5's slot is overlay0x3F4,
target0x8008DE1C (overlay0x93F4).

The sign's source property header is24 bytes:

| Property field | Native observation |
| --- | --- |
| +0,+4,+8 | Three zero source words; runtime state/counters are mutable |
| +0x0C | Scene-relative address of the inline text at property+0x18 |
| +0x10 | Scene-relative address of the zero word at property+0x14 |
| +0x14 | Zero source word |
| +0x18 | Native text bytes, zero terminator, four-byte padding |

The new parser bounds text before the next greater observed row-property
pointer or the property-component end, and refuses row aliases. That boundary
is a conservative observation, not proof of the complete allocation, every
native alias, or complete relocation ownership.

Native relocation declarations add a separate requirement. In Artisans the
relocation field list is at scene0x15528, count267, ending0x15958. SignT173's
row property field0xDD38 and nested fields0x1261C/0x12620 are all declared.
T174's three corresponding fields are also declared. Native integration must
allocate/register these fields; copying a detached private block alone is
insufficient.

The sign handler stores property+0x0C into global0x8007F478, calls0x8002B684,
and enters native game state17. The display consumer at0x8001B21C..0x8001B348
reads zero-terminated bytes, splits0xFF into lines, and calls width routine
0x80017798 and glyph renderer0x80022868. Text does not come from `Moby.Label`.
Importantly, NUL exits without flushing the final line: **every complete
dialog must end FF before NUL**. All330 original signs do. The first edited
reference exposed the missing-final-FF error when its second line disappeared
after reveal completion. Exact instruction proof and the corrected contract
are in `japanese-sign-runtime-portability.md`; the failing candidate is preserved.

The native display stack reserves a line buffer atSP+24..SP+71, with savedS0
atSP+72. Therefore each copied line must leave room for its zero terminator:
47 bytes is the buffer boundary. Observed originals use at most25 bytes per
line,153 bytes per complete message and9 line segments including trailing
breaks. The153/9 authoring limits in the new codec are deliberately conservative
source-derived policy, not claims of native engine capacity or screen fit.

## Native glyphs and font dependencies

This is not ASCII or Shift-JIS. Zero terminates,0x20 is space,0xFF is a line
break, and0x40..0x46 change the current text color. The renderer uses a256-entry
halfword glyph descriptor table at0x800795B8, EXE0x69DB8/0x200, SHA256
`3ebf51c29f337e461de260343b0e11b85eadeec9972b3eefd1afd04b33085913`.
Descriptors select atlas positions and layout; they do not encode Unicode.

WADentry2,0x6F800/0xA000, is the font image, SHA256
`917a2c272859d99b573ee3f79b1a62ca847dff218802b2c65f80bb55cb888ff1`.
EXE0x800120F0..0x80012140 reads0xA000 bytes and uploads VRAM rectangle
x0,y472,width512 words,height40. The standard font usesCLUT0x7FD6;
button glyphs0xF7..0xFA useCLUT0x7F96. Atlas pixels, palettes, descriptor
table, color controls, layout code and modal-sign state are separate required
dependencies for a faithful USA port.

The root task rendered and inspected native font evidence under the same
research directory (`native-font-atlas.png`, `native-font-glyph-grid.png`).
Hiragana0x80..0xB6 was independently inspected. Native ASCII letters do not
map to a Latin alphabet: many undefined entries point to the first hiragana
glyph. The Unicode bridge therefore must reject unsupported Latin/kanji/emoji
and retain explicit native-glyph escapes. Glyph decoding and source-byte
roundtrip are not an in-game text-rendering result. The native katakana
sequences in words such as カメラ and テクニック also provide semantic
cross-checks. An independent native atlas decoder now resolves the earlier
provisional crop issue: descriptors0x0051/0x0151 decode0xC5カ/0xD5ニ using
the transposed layout. Both glyphs were independently inspected in
`font-decoder-glyph-grid.png`. See [native font encoding](japanese-font-encoding.md)
for the complete atlas and Unicode mapping evidence.

## Models, materials and per-instance appearance

Sign rows use animation-byte pairs at+0x3C/+0x3D, not+0x3A/+0x3B:
223 signs select0/0,48 select1/1 and59 select2/2. The three registered model
animations each contain one frame. All30 sign-bearing levels show19 textured
near commands and14 untextured far commands per animation. Artisans near
materials use tpage0x18 andCLUTs0x4E3A/0x4C3A. Geometry/far streams differ
between the three variants; a marker recolor does not reproduce that selection.

All31 egg rows select animation0/0, native shade selector+0x4F=1. The egg
model has one animation with two frames,57 untextured near commands and32
untextured far commands in all30 registered levels. There is no egg-face CLUT
to swap. Model roots and face stream bounds are reported independently; adjacent
roots alone do not prove complete model-package ownership.

JP renderer0x80021E98 loads the entire packed row word at+0x4C. At0x80021EB8
it arithmetic-shifts by22 to form a **byte offset** into the shader descriptor
table0x80076E74 (EXE0x67674). Bits22/23 must be zero for aligned entries;
selectors with their sign bit set address before that base. The new source
certificate captures only the nonnegative32-entry prefix, not a claimed full
table extent. It guards the renderer0x12698/0x134 preimage and the128-byte
descriptor window.

Egg source word0x01000000 selects descriptor0x0A606060: base channels96,96,96
and lighting-control byte0x0A. Descriptor9 is0x0A006000, a different native
green base. Lighting, normals, view direction and native GTE processing still
determine final pixels. The descriptor's base RGB is not a rendered color
certificate. The flat-face branch also receives the packed row word through
MTLO at0x80022008; the low22 bits are preserved when changing a selector.

The USA renderer independently uses the same lookup arithmetic at0x80023104
with table0x8006E3D8 (EXE0x5EBD8). Equal profile indices are not sufficient:

| Index | Japanese descriptor | USA descriptor |
| ---: | --- | --- |
| 1 | 0x0A606060 | 0x0A606060 |
| 9 | 0x0A006000 | 0x0A006000 |
| 11 | 0x0A005860 | 0x0A006060 |
| 16 | 0xCA005860 | 0xCA006060 |

The other30 entries in the captured32-entry prefix are equal. Exact matching
for JP11/16 is absent in that USA prefix; the contract does not invent a
replacement. Zero descriptors remain explicit; no color names are inferred
for them. Custom egg textures require an independently verified textured-face
and UV implementation, because the native egg model is untextured.

## Implemented contracts and verification

`NativeJapaneseSignMessage` owns immutable native bytes/tokens and builds a
fresh24-byte private header with separately relocated text and cursor pointers.
Raw/Unicode decoding remains lossless; `AsCompleteDialog()` explicitly adds
the required final FF. Private-block emission and native edit planning refuse
unfinished raw streams rather than silently losing their final displayed line.
`NativeJapaneseSignSource.CaptureAll` binds each original sign to its native
row, regional data owner, bounded text and three-model variant.
`NativeJapaneseSignUnicodeCodec` is a separate root-owned bridge; the native
codec itself never guesses Unicode from arbitrary bytes.

`NativeJapaneseObjectAppearance.Capture` binds the shader prefix to each
independently authenticated revision and renderer preimage. `DescribeRow`
decodes caller-supplied instance fields without claiming their source provenance.
`WithProfile` returns a detached row with only+0x4F changed and preserves the
other87 bytes. Two copies can have different native selectors independently.
`FindExactProfileMatches` compares packed descriptors across revisions.

`Spyro.Editor.JapaneseSignMessageSmoke` covers independent messages, controls,
pointer relocation, exact source private-block readback, duplicate/overlapping
row references, missing terminators, line-buffer bounds, Unicode/native
roundtrip, independent per-instance appearance, descriptor immutability,
regional lookup preimages, invalid selectors and unaligned packed row words.
Before the terminal-FF runtime correction, the expanded Unicode/appearance/edit-plan/reference-scope
source-free run passed37,166 checks and the full Japanese source run passed3,075
checks. Those results did not detect the missing-line semantic defect. They admit all330 sign and31 egg
appearance rows, checks detached independent profile changes for every row,
and byte-compares all330 private sign blocks to their regenerated native
encoding. The separate actual USA shader capture passes3 checks. The Python
inspector passes9 focused malformed/source-layout tests. Final held-handle and
named-original hashes remain unchanged; no native candidate is written.

## Same-region existing-sign edit plan

`NativeJapaneseSignEditPlan.Compile` accepts an authenticated Japanese source
session and immutable `NativeJapaneseSignEditRequest(Target, Replacement)`
values. Targets originate from `NativeJapaneseSignSource.CaptureAll`; the
planner independently recaptures each requested level and matches its complete
data hash, row hash, message, offsets and target identity. USA, modified-image,
stale-target and duplicate-target requests are refused.

The original allocation boundary is derived from the native terminator:
`align4(24 + originalMessageByteCount + 1)`. The entire block, including its
zero padding, must match `BuildPrivateData` exactly. The next greater observed
properties pointer is only an overlap check, never extra message capacity.
A replacement may use padding already inside that exact block but cannot
enlarge it. Shorter replacements zero-pad the remainder. Native authoring
message and line limits still apply independently.

The planner follows the scene's two length-prefixed components after the
properties component to locate its relocation count/list. Every declared
field must be unique, aligned and bounded. A sign requires exactly its row
base, properties+0x0C and properties+0x10 registrations, with their original
targets. Any other registered field inside the exact sign block, or any
additional registered pointer into it, refuses isolated editing. All 330
original signs satisfy these checks. This is a statement about the declared
native relocation graph, not a claim that arbitrary unregistered values have
been proved to be non-pointers.

The resulting `Writes` retain stable WAD offsets, old/new byte copies and
SHA256 values, target row identity, and relocation-list offset/length/hash.
These offsets are logical WAD offsets, not raw BIN byte positions.
`ApplyToCapturedLevel` returns a detached full-level copy;
`VerifyLevelReadback` requires exact equality of that complete level, including
unchanged rows, pointers, relocation entries and unrelated bytes. The actual
source smoke compiles the message「あ」for all 330 signs and verifies all 30
sign-bearing level readbacks without writing a candidate.

The plan has no disc writer or GUI integration and grants no export,
publication or runtime approval. A separately authorized candidate harness
must still bind the whole source identity, preserve sector integrity, verify
complete candidate readback, and establish native sign interaction/rendering
in the intended runtime.

### Independent private-reference inverse verifier

`tools/Verify-SpyroJapaneseSignReference.py` independently reconstructs the
original Artisans blocks and the exact intended native text for indices
173–187 and 194–196. The initial13-byte message uses C5/BD for ガ and D9/BC for パ;
CA is サ and is not a substitute for ハ. The verifier does not load an editor
DLL, generated edit plan, or receipt success flags. Its inspected standalone
EDC/ECC arithmetic must first reproduce the original touched sectors exactly.
The complete candidate, when supplied, must equal the independently derived
full raw afterimage; CUE geometry, receipt content/hashes, held files and named
files are separately checked.

Eleven source-free tests cover malformed owner/fixup ranges, aliases, missing
terminators, padding, forbidden growth, native byte mapping, raw headers and
parity, whole-image complement refusal, CUE geometry, receipt tampering and
held/named-file replacement. The original-only preview writes no candidate
and predicts SHA256
`6188d217fa972240dd3ac50333eaba83171fb3ca333ad306cd8972786b9bbab4`,
with 2,134 changed raw bytes confined to sectors 5574–5576. Predicted CUE SHA256
is `b163471549b4edf132e05265c2fd8d55d76494c2769ca7ead7de4a4df9004dce`.
A preview is explicitly not an actual-candidate or runtime verification result.
These hashes describe the preserved first reference. Its raw bytes verified
exactly, but its unterminated second line did not remain visible in gameplay.
The corrected contract adds a final FF (14bytes); a new candidate and separate
verification are required. Structural inverse verification is not proof of
native modal display semantics.

No source-only result enables import, verifies final lit colors, proves modal
sign interaction, changes a save, or certifies a native candidate. Remaining
runtime dependencies include complete model/animation/normal decode, renderer
and font residency, native pointer-fixup registration, interaction/modal state,
USA behavior callbacks and the separately traced collectible/save contract.

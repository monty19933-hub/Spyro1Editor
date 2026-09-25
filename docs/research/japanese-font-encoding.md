# SCPS-10083 native sign font and Unicode review

Read-only source evidence, 2026-09-25. No disc, card, emulator, R08 package, or
editor settings were changed. This establishes font decoding and editable text
identity, **not a USA import or in-game rendering certificate**.

## Resolved: カ and ニ are not blank native glyphs

The corrected local draft image already displayed both glyphs when independently
reviewed. A second decoder, `tools/Render-SpyroJapaneseFont.py`, reproduces them
from the authenticated source. The issue was the preview interpretation of
format-1 UVs; no native table or glyph bytes need repair.

| Byte | Unicode | Descriptor | Texture page | UV upper-left | Nontransparent sampled texels | Index-plane SHA256 |
|---|---|---|---|---|---|---|
| C5 | カ | 0051 | 16 | (80,232) | 254 | `42a3ff4c3abef4526e573ea2768216eb18ba3cd378a1f626540665d1bdae2da0` |
| D5 | ニ | 0151 | 17 | (80,232) | 189 | `4c5460551e141b1bed1e208b07cb7eaa8c4a184bdf6e319a3a93dacf451beaa5` |

For format 1, screen X follows texture V and screen Y follows texture U.
The stored rectangle therefore must be transposed, not interpreted as a regular
left-to-right atlas crop. The two pages are distinct: one 4bpp texture-page
step is 64 VRAM words, or 256 texels. Physical decoded X is
`(texturePage - 16) * 256 + U`; physical VRAM Y is `256 + V`.

The decoder derives its unscaled sign-mode quad endpoints from native code.
It previews integer samples inside those endpoints, rather than adding the
right/bottom boundary texels. Format 1 has a 23-by-16 screen-coordinate delta,
except its U=F0 case is 23-by-15 to prevent an 8-bit U endpoint wrap. The
earlier draft's 24-by-16 transposed storage crop includes one extra boundary
column; this is not the cause of blank glyphs. Exact GPU coverage, blending,
dithering, and game-visible appearance still need emulator observation.

The new grid explicitly labels control/undefined bytes. Most undefined table
entries contain descriptor zero, but that does **not** turn those bytes into
valid aliases of hiragana あ. Byte80 is the valid zero-descriptor glyph.

## Source owners and native consumers

Exact source BIN: 727179600 bytes, SHA256
`1bcf4353d6a4d4d0db152d2ebd60879ef85516feaf170f364ff419c54f599ea7`.
MODE2/2352 payload offset24; EXE LBA24, WAD LBA500. The inspector verifies the
held source and the named original before returning; it opens neither for writing.

| Owner | Logical range | SHA256 |
|---|---|---|
| Font upload, WAD entry2 | 6F800 / A000 | `917a2c272859d99b573ee3f79b1a62ca847dff218802b2c65f80bb55cb888ff1` |
| 256 halfword descriptors | EXE69DB8 / 200 | `3ebf51c29f337e461de260343b0e11b85eadeec9972b3eefd1afd04b33085913` |
| Eight color/command words | EXE69D98 / 20 | `6f267eba5b81e5ae2abd5a82d393938a6be96b1db08bf1bd56bcee67b6837f75` |
| Renderer | EXE13068 / 2D4, PC80022868 | `592e7b33f9ed7f898b0ce475a8567aad7d6cd69c7c28a14c9c30cc078d799820` |
| Sign line caller | EXEBA68 / A0, PC8001B268 | `7bcf0a02497ffc0a0c36a334362a6b5c04725cde12063e9dd3e1141902659d6b` |
| Font upload caller | EXE28F0 / 54, PC800120F0 | `6fcc7c159e789ef4a3131119db6b8740ff8926e56e8e680a239eb1d321584ee7` |

PC80012124..80012140 uploads x0,y472,width512 **words**,height40.
The font is 4bpp: low nibble precedes high nibble. Standard CLUT7FD6 resolves
to word x352,y511; bytes F7..FA select CLUT7F96 at x352,y510 and override color
modulation with RGB808080. Other glyphs retain current text modulation.
Palette halfwords retain their STP bit in the report; RGB previews are raw
palette colors, with word0000 transparent, not fully composited game screenshots.

Sign PC8001B2EC calls renderer80022868 with a3=0. Its default mode is width24,
height16. The renderer also has a caller-selected 32-by-20 mode, which this
sign-only decoder deliberately does not claim to model. Alternate large glyph
bytes remain lossless glyph escapes in the Unicode bridge.

## Unicode and formatting contract review

`NativeJapaneseSignUnicodeCodec` was independently reviewed and its default
accent mapping corrected after the source atlas/cursor comparison below. Its
kana sequence matches the decoded native atlas.
Two source witnesses, also present in the full 330-sign byte-roundtrip check:

- Artisans WADentry23 T180: `C5 E1 E6` spells カメラ. Its complete 98-byte
  message SHA256 is `0d6cd8a918515085af3bde6f13d4dad134bb93d218a60c0294751eca5904d3e8`.
- Stone Hill WADentry25 T201: `D2 C7 D5 F6 C7` spells テクニック. Its complete
  57-byte message SHA256 is `6f4d640853186cccc414af4c7ef6f03fbf2e5c0f0576d218c8fec6af650b1235`.

Native semantics relevant to an eventual editor preview:

- 20 advances the cursor without drawing; 00 terminates a native line.
- 40..46 select modulation table entries0..6 without advancing the cursor
  (PC8002293C..80022968). Editable `\c0`..`\c6` are not literal native text.
- FF is split by the modal sign routine before the glyph renderer is called.
  Each line call passes a3=0, so **color resets at a line break**. An editor
  must not assume color controls persist across newlines.
- 01/02 are alternate larger handakuten/dakuten glyphs, with distinct native
  cursor corrections from BC/BD (PC80022910..80022938). All330 original signs
  use BC103 times and BD1708 times, **never01/02**. Byte01's visible bounds are
  x2..14,y0..8 and byte02's are x5..16,y1..6; their cursor is previousX+21.
  BC's bounds are x15..22,y0..5 and BD's are x16..22,y1..5, with cursor
  previousX+2. These are materially different size/placement combinations.
- The initial codec's precomposed kana used01/02 despite preserving all native
  source bytes on roundtrip. That was an authoring-fidelity defect, corrected
  to use BC/BD. The larger forms now remain explicit `\g01` / `\g02` escapes.
  Literal spacing marks U+309B/C are rejected; composed kana, Unicode combining
  marks, or `\gBC` / `\gBD` express the sign-native mark without ambiguity.
- NFC display / NFD input preserves base-kana plus marks. Unknown-but-defined
  glyphs retain `\gHH`; escapes cannot smuggle NUL, line/color controls, space,
  or undefined byte values. Unsupported Latin letters, kanji, emoji, malformed
  UTF-16, and unsupported escape syntax are refused without replacement.
- The native buffer limit is 47 bytes per line, not 47 Unicode characters.
  Message153 bytes / nine line segments are deliberate authoring policies
  based on observed source maxima, not additional native capacities.

After the correction, source-free smoke passes37,097 checks and complete
source smoke passes2,053 checks/330 signs (including concurrently added source
appearance checks). The repository smoke now covers all pairs of184 admitted
bytes, combining-mark triples, malformed Unicode/escapes, source-native accent
identity, and the two native camera/technique witnesses. A separate local review
also passes36,830 checks. The new decoder has seven source-free tests,
including independent synthetic transpose, nibble-order, page-stride, palette,
transparency, control-byte, and bounds cases.

## Reproduction and artifacts

Source-free decoder checks: `python3 -m unittest tools/test_japanese_font.py -v`.
For PNG inspection use a Python environment with Pillow (the bundled workspace
Python already has it):

```text
python3 tools/Render-SpyroJapaneseFont.py --source <original-japan.bin> --output-dir <existing-new-artifact-folder>
```

Outputs are create-only: existing output files are refused. No third-party
download or source mutation is required. The tool depends on the authenticated
read-only image helper in `tools/Inspect-SpyroJapaneseCollectibles.py`.

Current artifact root:
`_local/research/japanese-content-20260925-r01.RCmBCn/`.

- `font-decoder-report.json`:175 glyphs, per-glyph descriptor, UVs, palette,
  texel hash and bounds; SHA256 `e77135aeef828899f44f830e29e4bb588c5b691a76099f3aec186b4e679bcc06`.
- `font-decoder-glyph-grid.png`: independently inspected kana/icon evidence;
  SHA256 `16c36f77b43e49b2bd4adc05dafabe93e5892498c7f9670314a4c9b2ef0501e7`.
- `codec-review/CodecReview.csproj`: independent exhaustive review harness;
  run with `dotnet run --project <path> -c Release`.

Port dependency boundary: atlas upload, descriptor table, CLUTs, color words,
glyph layout/cursor corrections, bounded per-instance message storage, and
modal sign behavior must be carried or reimplemented together. This work does
not weaken destination resource ownership, pointer relocation, save identity,
native import, or runtime validation gates.

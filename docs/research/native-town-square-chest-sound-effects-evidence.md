# Native Peace Keepers chest → Town Square: sound and effects evidence

Read-only original-image investigation, 2026-09-05. This is a source certificate,
not gameplay acceptance. No emulator, installed application, or original image
was modified for this investigation.

## Source and address domains

The original MODE2/2352 BIN is 661,547,040 bytes, SHA-256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
Its complete digest was rechecked during this investigation. Logical 2048-byte
payloads begin at raw-sector byte 24. WAD.WAD starts at LBA 37; SCUS_942.28 starts
at LBA 53875, length `0x66000`, with a `0x800`-byte executable header.

| Native source | WAD entry | WAD-relative beginning |
| --- | ---: | ---: |
| Town Square overlay | 15 | `0x118E800` |
| Town Square data | 16 | `0x119E000` |
| Peace Keepers overlay | 21 | `0x187E000` |
| Peace Keepers data | 22 | `0x1890800` |

Overlay runtime addresses are `0x8007AA38 + overlay-relative offset`. Executable
file offsets are `runtime address - 0x80010000 + 0x800`. Data offsets below are
relative to the corresponding data entry, not model-relative or raw-disc offsets.

## The consequential sound-index distinction

The animated chest package at Peace data `0x1B3978` begins
`010000002fffffffffffffffffffffffffffffff`. Its first sound slot is the byte at
package `+4`, value `0x2F`. This is a **local descriptor index**, not a key into the
global-sound-ID map.

Evidence from actual native instructions:

- Chest state code `0x800842A0..0x800842C8` calls `0x8003851C` with the chest in
  `a0`, sound slot zero in `a1`, and `a2 = 0`.
- `0x8003851C..0x800385BC` gets the actor root through class table `0x80076378`,
  reads package `[4 + sound slot]`, and passes that byte directly to PlaySound
  `0x80055A78`. There is no global-ID-map lookup in this path.
- Animated-package relocation `0x800133E0..0x8001364C` relocates geometry and
  animation pointers but does not remap the four sound-slot bytes.
- Separately, native Key code at Town `0x80083C14..0x80083C28` reads
  `g800761D4[0]` before PlaySound. The imported reward handler reads map key
  `0x37` at Peace `0x8007E87C/0x8007E8B4` and
  `0x8007E998/0x8007E9C4`; the latter path uses shared tail `0x800880FC`.

Leaving package `+4 = 0x2F` unchanged plays Town's **different** local descriptor
47: its sample is 304 bytes, SHA-256
`c914520d1c2b3e3087c36eb4dcc9f823e09b382254e5c8861523db4f3d17693a`.
The correct donor sample is already resident at Town local descriptor **54
(`0x36`)**. Therefore native portability requires the typed sound-slot relocation
`0x2F → 0x36`. It does not require substituting, suppressing, or importing audio.

## Descriptor-to-uploaded-sample binding

Both data headers have first-subfile offset `0x800`. The first `0x80000` bytes of
that subfile are texture data; the sound bank starts at data `0x80800`. Peace's
first-subfile length is `0xDB000` (bank `0x5B000`), Town's is `0xDE000` (bank
`0x5E000`). Native loading at `0x800157C0..0x80015858` reads this bank and sets
SPU transfer address `0x1010`. Sound initialization `0x80012CF0..0x80012D58`
adds `0x1010` to each descriptor's initial sample offset.

Sound component bodies start at Peace data `0x160970` and Town data `0x172614`.
The first `0x100` bytes map global sound IDs; body `+0x100` holds descriptor count
(70/76); descriptors begin at body `+0x104`, stride 20. PlaySound indexes this
table with stride 20 and loads descriptor `+0` as the voice sample address.

| Dependency | Peace / Town local indices | Peace descriptor | Town descriptor | Peace sample | Town sample | Sample length |
| --- | --- | --- | --- | --- | --- | ---: |
| Mapped global ID `0` | 17 / 17 | `0x160BC8` | `0x17286C` | `0x8E100` | `0x8E100` | `0x3C40` |
| Mapped global ID `0x2F` | 18 / 18 | `0x160BDC` | `0x172880` | `0x91D50` | `0x91D50` | `0x1110` |
| Mapped global ID `0x37` | 35 / 35 | `0x160D30` | `0x1729D4` | `0xAF2E0` | `0xAF2E0` | `0xB60` |
| Chest model sound slot 0 | 47 / 54 | `0x160E20` | `0x172B50` | `0xC1B00` | `0xC5420` | `0x1430` |

Each corresponding sample span is exactly byte-equal. These bounded ADPCM
streams contain valid 16-byte frames (predictor at most 4, shift at most 12);
all preceding frame flags are zero and the last frame has end flag 1. Lengths
include that terminal frame, not a guessed next-descriptor boundary. No loop
marker or external loop target occurs in these samples. Mapped-global descriptor
records are fully equal. Chest descriptor bytes `+4..+19` are equal; only its
sample offset differs:

- Peace local 47: `0013040000000000320000040000000000000000`
- Town local 54: `204c040000000000320000040000000000000000`

Sample SHA-256 values:

| Dependency | SHA-256 |
| --- | --- |
| Global `0` | `57a13ba3c0348ecd5212ea7fca7c23d211c9e298e1a3a8ae06f571cd01459610` |
| Global `0x2F` | `fd793da3a87f996104559db586b8f0863c42b3c16d78a4c162235f802c2a11f5` |
| Global `0x37` | `dfb35ed7a3c560113f0e8530bf1efe423ea1306f90f432ec991f4949a000ee31` |
| Chest local 47 → 54 | `f10bef3c98281716bf79cb09c77ff6017c2959d10f11ba4f0b9cda1868c977c3` |

The global-ID `0x2F` equivalence is additional resident evidence; it must not be
confused with the chest's local descriptor `0x2F`.

## Level-specific callbacks, not assumed global behavior

The executable initializer at `0x8005A470` selects by native level ID
`g8007596C`, through jump table `0x800113A4[id]`.

| Level | Initializer | `g800758CC` spawn callback | `g800758E4` particle callback |
| --- | --- | --- | --- |
| Town Square 13 | `0x8005A898` | `0x800872A4` | `0x80088F68` |
| Peace Keepers 20 | `0x8005AA28` | `0x8008A258` | `0x8008BFF0` |

The callbacks are native **overlay** implementations. Their presence is not
inferred from the executable being shared. Town dispatches spawned classes
`0x135..0x137` to `0x800877A0`, Peace to `0x8008A724`. These `0x1AC`-byte native
initialization blocks differ only at offsets `0x18C` and `0x1A4`, the outgoing
branch/jump to their respective common continuations. All other bytes match.

The chest particle call uses `a0 = 0x20` as **count**, `a1 = 0x46` as effect type.
Town particle dispatch table `0x8007ADC4[0x46]` targets `0x8008978C`; Peace table
`0x8007B710[0x46]` targets `0x8008C888`. The corresponding `0xF0`-byte bodies
have equivalent operations with saved-register allocation differences and a
relocated final jump. Their finalization at Town `0x80089E38` / Peace
`0x8008CF10` writes literal line colors and endpoints. This path has no local
texture-descriptor load. This is initialization evidence, not an independent
complete proof of every subsequent update/render instruction or gameplay.

## Resident model and texture equivalence

| Class | Peace package | Town package | Length | Textured faces |
| --- | --- | --- | --- | ---: |
| `0x135` | `0x1BF8E0` | `0x1CC804` | `0x224` | 20 |
| `0x136` | `0x1BFB04` | `0x1CCA28` | `0x1D4` | 16 |
| `0x137` | `0x1BFCD8` | `0x1CCBFC` | `0x2D8` | 28 |

For these exact hashed native sources, the only package differences are the
12-byte texture descriptors at `+0x74 + face * 0x14`. Geometry/animation and
non-descriptor bytes match. The source metal tile uses page `0xE`, UV `(160,160)`,
CLUT `0x5429`; Town uses page `0x1C`, UV `(224,96)`, CLUT `0x7835`. All face UVs
use the corresponding translated positions within the same tile.

The native Key also spawns effect `0xC`: Town table target `0x800892C0`, Peace
`0x8008C338`. Their source paths use the same executable Key coordinate table
at `0x8006E4E0`. The particle texture record is at Peace data `0x160918`, Town
`0x1725C8`, each length 12; texture payloads are respectively
`204063683f5f3a00` and `00e03a7b1fff3d00`. These are repacked descriptors for
the same pixels and palette, not equality of descriptor bytes.

For each tile below, read 32 rows of 16 bytes, with a `0x400`-byte row stride,
and concatenate in row order. Palettes are single 32-byte spans.

| Resource | Peace first pixel row / palette | Town first pixel row / palette | Pixel SHA-256 | Palette SHA-256 |
| --- | --- | --- | --- | --- |
| Metal debris tile | `0x28B50` / `0x54920` | `0x58A70` / `0x78AA0` | `857dd2b39b01a92274a8e402e4842801f2791c05dcc84003f87f0388675a5fbc` | `4f0b74f89de2c8239ad3b552f9e33b704bb1183bab8f2177d102ce661442c488` |
| Key effect `0xC` | `0x50910` / `0x68C60` | `0x78A80` / `0x7BB40` | `9bcc202f8332ba7c898131f5f3ceee702e804832bbdb8d91ea0410d0d888a73e` | `ee46b6121704e60b9643b204ea4670936c1aa640169d2d318b0a5f0aea487aab` |

## Native executable certificates

Offsets include the executable file header. Hash the indicated complete spans.

| Purpose | File offset | Length | SHA-256 |
| --- | --- | --- | --- |
| Model-local sound consumer | `0x28D1C` | `0xA0` | `af902cc21e59f2794bc459d6cfaf528f36c28b73f39fc4221c1fe6d384331e91` |
| Model relocation | `0x3BE0` | `0x26C` | `fede3c414aea2dce0372fce574549d961123cf6456a7a291acef48b9c4af8598` |
| Sound map/descriptor initialization | `0x34F0` | `0x68` | `ecd95b895b0f1dd85b2aed1c926a6e5e818ac3c41ddaf38565e34459808f89a0` |
| SPU bank load | `0x5FC0` | `0x98` | `5161b58b7a0a1616677faf6583f8db591c6d2fa8b741fa2d78cb70579a01162b` |
| PlaySound | `0x46278` | `0x82C` | `d3a63784c8dc509583c63499ccb7ad493ba8bf4af0aaeb4a6bd9fa5aa851a06d` |
| Callback initializer dispatch | `0x4AC70` | `0x30` | `7dba88624efe091d8b34a22b8874a2d12ab577858f7583a324d82f11d14d5c4f` |
| Town initializer | `0x4B098` | `0x98` | `fe4669229aedbbc8ff698d57a237327af36025aaeda0ff5cc2e1c806fd23e50b` |
| Peace initializer | `0x4B228` | `0x98` | `229c1235ff1e6860db55fb922e66eb526356d88f669e9ed41b609ea9f8af1187` |
| Six Key effect coordinates | `0x5ECE0` | `0x48` | `4526484f15d026e6d76826b145f0cd76cbeb21f3897f95e993c904ddefd6b8d7` |

Town initializer's first eight bytes also contain the existing copy-buffer
address and are intentionally relocated by the candidate. Preserve the remainder
of that certificate, not an overlapping promise to keep those eight bytes intact.

## Cross-review gate

Independent rerun of the built source smoke against the exact original BIN:

```text
NATIVE_MOBY_DEPENDENCY_CLOSURE_SMOKE_OK: 329 checks
graph: d484fc13504ac0afc0b7a63e81dd4a6a3a3a7672dd7959654b02fd6103a3edcc
276 nodes; 159 installs; 8 references
data: e442a02d3aa899dca0d5ddac4392f4b602aabb7ac6dc5dd1e4616c00fab6908d
overlay: fd859bff13985e18c3aff74cc889006c48b2743bf57a2b0f9d80662d25a09c2d
executable: 4268f9943233b9f8c24d1d4b0924cba8747547b44b82eddd13f0a2ac10ac17de
```

This snapshot includes the corrected local sound slot and genuinely grown private
properties, with existing rows/fixups and pod/collision suffix checked. Reported
ownership-boundary and CUE publication-order findings were fixed by their owner
and reviewed again. These source checks do not certify runtime sound, animation,
reward retirement, death/reload, or editor promotion.

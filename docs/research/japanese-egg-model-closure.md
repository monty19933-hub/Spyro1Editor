# Japanese F5 egg: source geometry closure

2026-09-25. This is the Artisans model foundation for a regional import, not
approval to import its actor code, save state, sound, or behaviour. No emulator,
original image, settings, card, preserved R08 package, or published build changes.

`NativeJapaneseEggModel.Capture` authenticates the existing SCPS-10083 source
session, WAD entry 23, registration slot 26/class F5, and four executable consumer
windows. The source image SHA256 is
`1bcf4353d6a4d4d0db152d2ebd60879ef85516feaf170f364ff419c54f599ea7`.
Entry23 SHA256 is
`7184fff950efa2bdffa37f97875ef7de2a3219480c61ec3ab37f784778d8cfde`.
The registered model root is data `0x1CCF5C`, WAD `10291036`.

## Owned package

The derived package is `0x808` / 2,056 bytes, SHA256
`da0c1ae7ab529a8ab5b43954452f368adb93c09f1a9b17013c52ee3039f4a810`.
This size follows the consumers below, **not** a distance to another model root.
All ranges below are package-relative. Each is also independently hashed in the
typed result; returned bytes and collections are detached/read-only.

| Owner | Offset | Bytes | Native closure |
| --- | ---: | ---: | --- |
| Root + one animation pointer | `000` | `03C` | count=1; animation root=`03C`; model-data root=`070` |
| Animation + two paired frames | `03C` | `034` | encoding byte +4=0; signed frame count=2; stride8 |
| Near faces | `070` | `2A8` | 2 triangles +55 quads; length prefix owns complete commands |
| Far faces | `318` | `144` | 16 triangles +16 quads |
| Near packed shading words | `45C` | `118` | 70 words, animation+18; first=`FF6A9230` |
| Far packed shading words | `574` | `118` | 70 words, animation+20; first=`006A9230` |
| Frame0 vertex streams | `68C` | `0BC` | 12 absolute words +70 delta halfwords; 82 vertices |
| Frame1 vertex streams | `748` | `0BE` | 13 absolute words +69 delta halfwords; 82 vertices |
| Alignment | `806` | `002` | two authenticated zero bytes to a word boundary |

Near and far both use animation bytes +8/+9=82 output vertices. Near faces use
all82 vertices and all70 shading entries; far faces use42 distinct vertices and24
distinct shading entries. All indices are checked, not merely the face lengths.
The existing `NativeMobyModelTextureLayout` independently decodes the bounded
package: two face tables, zero texture references. There is no egg texture/CLUT
dependency in this package.

The +18/+20 arrays feed the renderer's packed RGB/GTE shading path. Calling them
XYZ **normals** would overstate the grammar: the capture retains their complete
words without inventing normal vectors. No additional normal-pointer field is
present in the admitted encoding-zero header. Geometry coordinates are model
coordinates before scale, per-instance rotation, world translation, and GTE.
The source scale exponent +5 is0. Frame bounds are X[-190,194], Y[-271,287],
Z[-358,-2] for frame0, and X[-190,194], Y[-311,303], Z[-390,-2] for frame1.

## Native consumers and read bounds

- Loader `800128D8`, EXE `30D8/26C`, SHA256
  `67eb37fcb000ba1634438c7d45311481c766e171114dae214429731cb8d3f7a4`:
  root+34 is root-relative model-data; animation pointers at root+38 are
  root-relative; animation+14/+18/+1C/+20 are model-data-relative. The eight
  auxiliary root pointers +14..30 are all zero. Encoding-zero pairs retain the
  first word's upper flags and rebase its low21bits. The second word's low16bits
  would own a face-patch stream; both F5 values are zero, so that branch is absent.
- Vertices `8001FEC4`, EXE `106C4/474`, SHA256
  `cf1bf8f5b7852586b9d63f49d9ccf759fb426f3515f62880d617bee3887a6db6`:
  first-word low21bits locate the frame stream. Auxiliary-word high byte times4
  separates its absolute words from delta halfwords. The current item's bit0
  selects the next read: 0=absolute32, 1=signed delta16. Absolute coordinates
  follow the native signed shifts `w>>21`, `(w<<10)>>21`, `(w<<20)>>19`;
  delta coordinates follow `d>>11`, `(d<<21)>>27`, `(d<<26)>>27`, with shift
  animation+6+1=3 and signs `x+=`, `y-=`, `z-=`. C# uses explicit unchecked
  32-bit intermediate shifts, matching the MIPS words.
- Shading `80020354`, EXE `10B54/174`, SHA256
  `b9df3e24377794671aa0ee4d6f84b716ad36a4c5c761f4ba946550f54e8d8995`:
  animation+2 gives70 entries; near/far pointers and GTE RGB consumers own4bytes
  per entry. The ordinary colour blend also preloads one word after each table.
- Faces `80020550`, EXE `10D50/438`, SHA256
  `46de50c881f11fdc3e79b5e807dd920069c5439be5bb041d905cf1ecf0249a70`:
  vertex indices are first-word bits22/13/4 (9bits each), plus quad third-word
  bits2; shading indices are second-word bits19/10/1, plus quad third-word bits11.
  The admitted untextured commands are8/12bytes. Textured/alternate commands are
  refused by this narrow geometry decoder rather than silently interpreted.

The vertex pipeline reads **one extra item** before testing the output count.
For these two frames that absolute-word look-ahead is at `6BC/4` and `77C/4`,
already inside their delta stream owners. It is not an83rd output vertex. Face
look-aheads are `318/8` and `45C/8`; shading look-aheads are `574/4` and `68C/4`.
All six reads fit other explicitly owned bytes. No read requires appending an
unproven next-model byte. The source frame auxiliary middle bytes45/47 are
retained raw; their shadow-prefix consumer does not enlarge the 82-vertex bound.

## Admission and remaining cross-region work

Only exact source capture sets `SourceGeometryClosureProven=true`. Synthetic
fixture decoding does not issue source authority. Both leave
`NativeImportApproved=false` and `DestinationRuntimeVerified=false`.
This does not widen `NativeMobyModelTextureLayout.CompleteModelClosureProven` for
arbitrary animated models.

The next implementation can assemble the captured relative package into a USA
model owner with an explicit F5 registration and validate the USA loader/renderer
grammar against these exact fields. Actor dispatch, collect identity, per-instance
colours, save extension, counters, sound/effects, and destination runtime capacity
remain separate contracts. In particular, native Japanese per-level collection
bytes do not independently identify multiple eggs in one custom level.

Focused `EggModelCases` exercises complete decoding, immutable byte readback,
all160 truncation lengths of a small synthetic package, pointer/partition/index
refusals, no next-root-size inference, pipeline look-ahead bounds, and all real
source frames through the existing `--source` smoke path.

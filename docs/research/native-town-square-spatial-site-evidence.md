# Town Square locked-chest spatial-site evidence

Status: source-backed research placement evidence, not a mesh, physics, or
gameplay certificate. Coordinates below are raw native integer coordinates.
No renderer culling radius is treated as an actor's physical dimensions.

## Exact source and occupied-site discriminator

The locked USA source image is SHA-256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
The immutable properties/sound V2 image is
`_local/research/native-moby-closure-20260905/town-square-native-closure-v2-r01/town-square-native-closure-v2.bin`,
SHA-256 `d94d90b51c15b82328b6dbeb67474e563ddefc7d7f6c81bd4b61c526c58083fe`.

V2 imported T108 class AE and original T27 class C3 both have
`(132198,111032,11264)`. Original T26 class C3 is also nearby at
`(132198,112589,11264)`. This exact overlap confounds interaction observations;
it is not evidence that either original row should be removed or changed.

The independent read-only probe parses the V2 WAD header, finds Town data at
WAD `0x119F000`, walks four size-prefixed model components to collision, and
compares the whole model subfile (including collision) and all 107 original
actor rows exactly with the original image. It does not infer native row or
collision identity from a cached analysis file.

## Proposed coherent translation

All seven imported instance rows and the five reward-property positions use
the same translation `(-10518,-8744,-3072)`:

| Instance | Raw native XYZ |
| --- | --- |
| T107 key | 119632, 102288, 8192 |
| T108 chest | 121680, 102288, 8192 |
| T109 reward | 121270, 103517, 8704 |
| T110 reward | 121680, 103517, 8704 |
| T111 reward | 121065, 102493, 8704 |
| T112 reward | 121270, 102903, 8704 |
| T113 reward | 121884, 103107, 8704 |

The separate recommended landing is `(121680,100240,8550)`. Its ground offset
358 matches the native Town landing `(125225,100506,8550)` over ground 8192.
This recommendation does not authorize a landing-field write.

Every new center and the recommended landing is over 3072 raw horizontal
units from every one of the 107 original centers, without using vertical
separation to discard actors. The closest pair is key T107 / original T70
class 55, distance 3306.2668. Original T18 class 19D is the closest same-floor
actor to reward T110, distance 3472.2756. These are center distances, not
distances from animated geometry or collision shapes.

## Continuous collision and dry-ground evidence

The collision component is data-relative `0x10A5C0`, length `0x5FAE8`, with
19808 compressed 12-byte triangles and 10500 explicit flag bytes. V2 triangle
table WAD is `0x12C79C4`; subtract `0x1000` for original-image WAD offsets.

For each imported center and landing, the probe clips every floor triangle to
the authored XY square of half-width 512, verifies a nonoverlapping area
partition totaling 1048576, and rejects other collision triangles intersecting
the square's volume from floor+1 through floor+1536. Supporting triangles must
also occur in the native collision tree's applicable cell/group. The square
is an authored ground test, **not a claim that the model fits inside it**.

A chain of overlapping half-width-256 patches, at intervals no longer than
256, gives the same continuous ground/clear-column result from the original
landing to the recommended landing, then the chest, then the key. This is a
terrain corridor test, not a moving-actor or camera clearance guarantee.

All support triangles in these patches and corridor select no special
surface and have compressed Z flags zero:

- Explicit flag `0x3F`: triangles 1347, 1348, 1360, 1362, 1363, 1390, 1396.
- Native default flag `0xFF`: triangles 13889, 13890, 13891, 13893, 13894,
  13922, 13923, 13964, 13965, each beyond explicit flag count 10500.

This default is native code, not just an editor convention: executable
`0x8004DA78`–`0x8004DAA0` selects the explicit flag or `0xFF` and stores
`g80075718`. Executable `0x80057380`–`0x800573C4` masks that value with `0x3F`
and returns -1 for 63 without dereferencing the special-surface table.
Original landing triangle 1348 uses explicit `0x3F`. Thus the proposed support
does not select the damaging-water descriptor that invalidated the separate
Magic Crafters Wizard route experiment.

## Bounds are still an independent gate

The renderer's `+0x50` field is a culling distance, not a physical radius.
Executable `0x80022B20` onwards scales the signed byte by 256 while comparing
actor-camera differences shifted right two. Conflating the documentation's
scaled editor units with the raw coordinates above produces a factor-16
error. Earlier 2048/1536-raw-unit exclusions derived from this field are
withdrawn and must not be used as native clearance evidence.

A narrow independent AE compressed-stream probe follows executable
`0x800531A0`–`0x80053210`. In the single-animation Peace package at
data-relative `0x1B3978`, model-data base is package+`0x68`; the first frame
starts at package+`0xEF0`. Its 103 decoded entries consume exactly 29 absolute
words through `0xF64` and 74 signed delta halfwords through `0xFF8`, exactly
the collider start. Pre-transform local tuples span X[-536,391], Y[-493,511],
Z[-820,2]. Five entries exceed a local ±512 XY square. Native transform/unit
scaling and all render/collider consumers remain to be certified; these local
tuples are not presented as the final world-space mesh bound.

The spatially separated V3 research recipe therefore must explicitly retain
`nativePhysicsOrMeshBoundsProven=false`. Native actor wandering, the complete
key/chest/reward interaction, visuals, effects, collision and camera behavior
still require gameplay evidence. No normal Create BIN/app promotion follows.

## Reproduction helpers

Independent read-only scripts created for this investigation:

- `/tmp/spyro-town-site-evidence.zUerll/probe.mjs`: `validate 121680 102288 8192`,
  `corridor`, and `flags`; compares native collision/model and 107 row preimages.
- `/tmp/spyro-town-site-evidence.zUerll/ae-vertices.mjs`: verifies the full
  original image SHA and reports the bounded compressed-stream result.

Temporary helpers should be retained beside the research candidate before a
future temporary-directory cleanup. No ROM, emulator, app or settings were
changed by these probes.

# Native terrain HQ queued-render contract

Status: bounded integer contract implemented and smoke-locked. This note does
not claim that the editor's floating-point Fly 3D projection reproduces PS1 GTE
screen coordinates, clipping, or tile-local depth.

## Proven producer selection

The retail producer writes normal-HQ records to the queue at environment state
`+0x2000` and close-HQ records to `+0x1E00`. Its exact weighted source depth is:

```text
quad     = sz0 + sz1 + sz2 + sz3
triangle = sz0 + sz1 + sz2 + sz2
```

A face cannot enter either HQ queue when the weighted sum is at or beyond the
current high-poly LOD distance, is at least `0x2000`, or face word 3 bit 7 is
set. For a remaining candidate:

| Source SZ state | Face word 3 bit 6 clear | Face word 3 bit 6 set |
|---|---|---|
| every unique source SZ is at least `0x140` | normal HQ | no HQ |
| any unique source SZ is below `0x140`, weighted sum nonzero | close HQ | normal HQ |
| weighted sum zero | no HQ | no HQ |

Bit 6 is therefore conditional. It is not a global HQ bypass and is not a
global "force normal" flag. Bit 7 is the unconditional HQ bypass.

Producer evidence:

- Quad selection and queue writes: `r_environment.s:1386-1437`, address block
  `0x80026CFC-0x80026DB4`.
- Triangle selection and queue writes: `r_environment.s:1755-1810`, address
  block `0x80027244-0x8002730C`.
- Queue terminators and consumer decode: `r_environment.s:2054-2105`, address
  block `0x80027688-0x8002774C`.

Each queue begins a context run with `contextPointer >> 2`. A face record is its
16-byte-aligned face pointer plus bit 0 copied from the producer's projected
flag union bit `0x10`; triangle records also set bit 1. The consumer restores
the pointer by clearing those low two tag bits.

The all-35 retail overlay smoke currently inventories 190,640 HP faces: zero
have word-3 bit 6 and 1,353 have bit 7. This means correcting the bit-6 semantic
does not change a shipped retail face today, but the distinction is now locked
so future source data cannot silently inherit the old `(word3 & 0xC0)` alias.

## Proven interpolation and base topology

Camera-space coordinate subdivision uses a signed 32-bit add followed by an
arithmetic right shift and signed-halfword store:

```text
midpoint(a, b) = (a + b) >> 1
```

Negative odd sums round toward negative infinity. Packed colors use:

```text
colorMidpoint(a, b) =
    ((a & 0xFFFEFEFF) + (b & 0xFFFEFEFF)) logical>> 1
```

The normal quad builds a 3x3 lattice and emits four row-major GT4 base tiles.
The close quad builds a 5x5 lattice and emits sixteen row-major GT4 base tiles.
The normal triangle builds a six-point order-2 lattice and emits four GT3 base
tiles. The close triangle builds a fifteen-point order-4 lattice and emits
sixteen GT3 base tiles.

Geometry/interpolation evidence:

- Normal quad: `r_environment.s:2107-2271`,
  `0x80027750-0x800279DC`.
- Normal triangle: `r_environment.s:2630-2748`,
  `0x80027F28-0x800280F8`.
- Close quad: `r_environment.s:3101-3462`,
  `0x8002862C-0x80028BCC`.
- Close triangle: `r_environment.s:4123-4351`,
  `0x8002959C-0x80029928`.

The four base-topology tables are preserved verbatim in
`NativeTerrainHqQueuedRenderContract`:

- Normal quad origins `D_8006CF48`: `math.data.s:392-397`.
- Close quad origins `D_8006CF58`: `math.data.s:399-416`.
- Normal triangle descriptors `D_8006D0E8`: `math.data.s:518-523`.
- Close triangle descriptors `D_8006D0F8`: `math.data.s:525-542`.

Their combined 160-byte little-endian SHA-256 fixture is:

```text
B588456351AA40CAE5D86CB88F905C05C92BB5F7CF4358454C11722F2D001AA9
```

The Core contract returns base tiles in the same table order, which is also the
native first-generation FIFO insertion order inside a tied OT bucket.

## Proven tile-local buckets

Every first-generation GT3 base/seam packet uses:

```text
bucket = ((sz0 + sz1 + sz2 + sz2) >> 7)
       + ((faceWord3 & 0x38) >> 1)
```

Every first-generation GT4 base packet uses:

```text
bucket = ((sz0 + sz1 + sz2 + sz3) >> 7)
       + ((faceWord3 & 0x38) >> 1)
```

Bits 6 and 7 never enter the OT bias. The renderer does not clamp the result;
it addresses the world OT directly. The Core API accepts only explicit
generated scratch SZ inputs in `0..0xFFF`, after removing the stored outcode
nibble, and likewise performs no clamp.

Representative equation/insertion evidence:

- Normal seam GT3: `r_environment.s:2464-2515`,
  `0x80027CB0-0x80027D78`.
- Normal quad GT4: `r_environment.s:2525-2628`,
  `0x80027D7C-0x80027F24`.
- Normal triangle GT3: `r_environment.s:2946-3053`,
  `0x800283DC-0x80028578`.
- Close seam GT3: `r_environment.s:3691-3733`,
  `0x80028F1C-0x80028FC4`.
- Close quad GT4: `r_environment.s:3882-4077`,
  `0x80029200-0x800294F0`.
- Close triangle GT3: `r_environment.s:4684-4851`,
  `0x80029E14-0x8002A09C`.

The exact post-base pass order is normal HQ in queue order, then all close
quads, then a second close-queue scan for all close triangles, then recursive
screen-span repair. A single homogeneous close FIFO phase is only a coarse
editor approximation.

## Data sufficiency and explicit blockers

Current cache/scene data is sufficient for:

- four native HP vertex/color slots, including triangle repetition;
- native face words 2 and 3, with words 0 and 1 reconstructible from preserved
  vertex/color slot indexes;
- four near/far packed color values;
- all 4 normal and 16 close initialized eight-byte material descriptors;
- descriptor PSX555/STP texels, CLUT/page/ABR/orientation data;
- fixed subdivision topology, integer midpoint rules, candidate tile ordering,
  and tile bucket equations.

The source-contract guard is now complete for this input: overlay format 6 names
the `raw-words0-3-explicit-word2-word3-material-v2` payload, preserves a typed
word 3 alongside legacy `word4`, and validates all four raw words against the
serialized vertex/color slots, material fields, flip, and depth. Missing,
malformed, or inconsistent source-derived words fail closed in both cache health
and the loader. The all-35 smoke locks 190,640 complete rows at raw-word stream
SHA-256 `3D55BB4D51BA31F55B4CFF3A468FB9D93E90CE3976EA6939638E99F3A9A16673`.
Only overlays with explicitly absent source provenance retain the legacy
permissive parser; a focused format-5 fixture covers that compatibility path.

Current editor projection data is not sufficient for native-exact emission.
The retail path projects every generated lattice vertex through fixed-point GTE
state and retains SXY, SZ, FLAG/outcodes, and NCLIP results. It also performs a
normal-quad center perspective correction, a close-quad four-point correction,
seam generation, and recursive repair when screen-coordinate spans wrap or grow
too large. The recursive pass at `r_environment.s:4852-5258`,
`0x8002A0A0-0x8002A6AC`, replaces packets while preserving their parent bucket.

The editor currently has floating-point yaw/pitch, an editor-selected focal
length, and already-projected corners. Midpoint-before-native-projection is not
equivalent to midpoint-after-editor-projection. A full native claim requires a
separate fixed-point camera/GTE contract supplying generated SXY/SZ/FLAG/NCLIP;
it must not be inferred from the current Fly 3D projection.

## Production recommendation

Do not wire bit 6 or the new tile buckets into production in isolation. Bit 6's
meaning depends on the same `<0x140` integer branch, and no current retail face
uses it. Bit 7 is already behaviorally covered by the current bypass for all
1,353 affected faces. The next safe production step is to split the names of
the two bits and consume `SelectQueuedTier` as one unit only where the caller
can state that its source SZ values are the bounded integer inputs. Tile-local
buckets should remain unwired until generated integer SZ lattices have an
explicit projection provenance; the float viewport must not be relabeled as
native exact.

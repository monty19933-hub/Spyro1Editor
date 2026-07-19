# Native terrain GPU dithering proof

## Retail enable state

Spyro enables `DrawEnv.dtd` for both alternating draw buffers during startup:

- `work/spyro-1-decomp/src/initialization.c:60-70`
- buffer drawing offsets are Y 0 and 240, so both retain the same 4-row
  dither phase (`240 % 4 == 0`)

The terrain renderer emits shaded untextured polygons and modulated textured
polygons. Those polygon classes follow the draw-mode dither bit; raw-texture
and rectangle exceptions do not apply to these terrain packets.

## Exact color equation

The independent software-GPU reference was pinned to DuckStation commit
`e39033c4480cfbb9106e32beb844b0649ad9c2db` (2026-07-13).

Its native 4x4 matrix is:

```text
-4   0  -3   1
 2  -2   3  -1
-3   1  -4   0
 3  -1   2  -2
```

For an untextured interpolated RGB8 channel `c` at framebuffer pixel `(x,y)`:

```text
rgb5 = clamp((c + matrix[y & 3][x & 3]) >> 3, 0, 31)
```

For an 8-bit modulated textured channel:

```text
pre  = (texel5 * shade8) >> 4
rgb5 = clamp((pre + matrix[y & 3][x & 3]) >> 3, 0, 31)
```

The raw zero-word transparency test happens before modulation. The dithered
RGB5 foreground is produced before any STP-gated ABR blend with the existing
RGB5 framebuffer.

Reference anchors in the pinned source:

- `src/core/gpu_types.h:268-273` - matrix
- `src/core/gpu_sw_rasterizer.cpp:18-31` - LUT quantization
- `src/core/gpu_sw_rasterizer.inl:125-169` - zero-word test, textured
  modulation, untextured quantization
- `src/core/gpu.cpp:2963-2971` - polygon enable rules

## Editor contract and boundary

The editor implementation is `PsxGpuDither` with contract
`psx-gpu-native-4x4-dither-before-rgb5-v1`. The bounded rasterizer now retains
RGB8 LP/sentinel colors through interpolation and applies the exact native
matrix/quantization at the final framebuffer coordinate. Textured terrain uses
the exact shift-4, dither, shift-3 order before the existing STP/ABR decision.

This makes the dither stage exact for the bounded rasterizer's supplied pixel,
shade, and texel inputs. It does not make the current barycentric color
interpolation, editor-camera projection, clipping, or GTE behavior retail
exact; those remain separately bounded. Fly 3D also rasterizes at the current
editor viewport dimensions rather than a fixed retail 512x224 draw area. The
matrix phase and integer quantization are exact at those supplied framebuffer
coordinates, but their apparent display scale is not yet a native-resolution
presentation claim.

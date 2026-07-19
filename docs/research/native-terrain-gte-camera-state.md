# Native terrain GTE camera-state audit

Date: 2026-07-16

Scope: the retail Spyro 1 camera/GTE state that feeds `r_environment`, what the
current editor camera and source-scene overlay can reconstruct, and the smallest
truthful implementation boundary. This is a research contract, not a claim that
the editor already reproduces the full retail terrain renderer.

## Conclusion

The editor has enough information to build a deterministic, native-register
projection of the editor's own Fly camera. It does **not** have enough provenance
to claim equivalence to an arbitrary retail gameplay frame. Exact frame
equivalence would require a captured/imported retail camera pose (or a
DuckStation trace) because Fly camera state has floating-point position,
yaw/pitch only, no roll, no exact 12-bit angle provenance, and no camera
occlusion/script state.

The existing integer scene points are numerically sufficient to project every
shipped high-poly terrain face in the current 35-level USA data set. That is true
because every HP-bearing sector audited has zero discarded quarter-unit X/Z
origin residue. The overlay does not currently preserve or certify that fact,
so a durable implementation must add a source-provenance guard before it treats
integer points as universally exact.

The minimum honest feature is therefore:

1. build an explicit retail-style camera matrix/register set from a named editor
   camera adapter;
2. validate or retain the source sector origin residue needed by HP projection;
3. project original LP/HP base vertices at the retail logical 512x240 resolution;
4. expose the result as `editor-camera-derived native base projection`, not as a
   full gameplay-frame or full `r_environment` reproduction.

## Source anchors

The primary retail anchors are:

- Camera matrix construction: `work/spyro-1-decomp/src/camera.c:27-87`.
- Matrix update once per draw: `work/spyro-1-decomp/src/gamestates/draw.c:2669`.
- Retail trig interpolation: `work/spyro-1-decomp/asm/math.s:221-272`,
  `Sin` at `0x80016C58`, `Cos` at `0x80016CB0`.
- Retail in-place `MulMatrix`: `work/spyro-1-decomp/asm/psyq.s:7973-8042`,
  `0x800624E8`.
- GTE initialization and projection constants:
  `work/spyro-1-decomp/src/initialization.c:259-261` and
  `work/spyro-1-decomp/asm/psyq.s:7862-7898,8056-8072`.
- Terrain matrix/camera load:
  `work/spyro-1-decomp/asm/renderers/r_environment.s:32-59`,
  `0x8002592C-0x8002597C`.
- LP decode/project path:
  `r_environment.s:649-703`, `0x8002623C-0x80026310`.
- HP camera scratch/decode/project path:
  `r_environment.s:1023-1055,1073-1093`,
  `0x800267B8-0x80026834` and `0x8002687C-0x800268C8`.
- HP near-camera precision rerun and base outcodes:
  `r_environment.s:1162-1240`, `0x800269CC-0x80026AE8`.
- Generated-HQ repair pass: `r_environment.s:4852-5258`,
  `0x8002A0A0-0x8002A6AC`.
- Current Fly projection: `src/Spyro.Editor.App/Views/EditorViewport.cs:7464-7510`
  and camera shape at line 9853.
- Current game-view axis signs:
  `src/Spyro.Editor.Core/EditorUiDefaults.cs:3-15` and
  `EditorViewport.cs:7554-7572`.
- Current overlay sector metadata load/export:
  `src/Spyro.Editor.Core/Scene/GeometryOverlayLoader.cs:103-127` and
  `src/Spyro.Editor.Core/Exporting/SourceSceneOverlayExporter.cs:303-326`.

## Retail view-matrix construction

Angles are 12-bit turns: `0x1000` is one revolution. Trig values and matrix
elements are Q12 signed integers: `4096` is 1.0.

For camera rotations `rx = m_Rotation.x`, `ry = m_Rotation.y`, and
`rz = m_Rotation.z`, `CameraUpdateMatrices` constructs:

```text
A(ry) = [ 4096       0        0 ]
        [    0    cos(ry) -sin(ry) ]
        [    0    sin(ry)  cos(ry) ]

B(rz) = [ cos(rz)    0     sin(rz) ]
        [    0      4096      0     ]
        [-sin(rz)    0     cos(rz) ]

C(rx) = [ cos(rx)  sin(rx)    0 ]
        [-sin(rx)  cos(rx)    0 ]
        [    0        0     4096 ]
```

The view matrix is:

```text
R = (A * B) * C
```

That parenthesization matters. Each `MulMatrix` is the retail PSYQ routine at
`0x800624E8`; it loads the first matrix into the GTE rotation registers,
projects each column of the second with `MVMVA sf=1,lm=0`, and stores signed
short results back into the first matrix. A floating-point multiply followed by
one final rounding is not equivalent to the two retail truncation stages.

`Sin`/`Cos` first mask the angle with `0xFFF`. For a non-table angle they use:

```text
i    = angle >> 4
frac = angle & 0xF
value = table[i] + (((table[i + 1] - table[i]) * frac) >> 4)
```

The final arithmetic shift is signed. Exact reconstruction must use the retail
tables and this interpolation, not `Math.Sin`/`Math.Cos`.

`g_Camera.m_ViewMatrix` is copied before the later `320/512` row scaling at
`camera.c:78-86`. `r_environment` loads the unscaled view matrix. The scaled
`m_ProjectionMatrix` is not a terrain input.

## GTE control-register state at terrain entry

At `0x8002592C-0x80025958`, `r_environment` writes all nine rotation elements
from `g_Camera.m_ViewMatrix`. It then explicitly writes zero to `TRX`, `TRY`,
and `TRZ` at `0x8002595C-0x80025964`. Camera subtraction is performed by the
terrain unpacking code, not by GTE translation.

| Register | Retail terrain value / status | Evidence and consequence |
| --- | --- | --- |
| `R11..R33` | Exact Q12 `m_ViewMatrix` above | Loaded at `0x80025934-0x80025958`. |
| `TRX/TRY/TRZ` | `0/0/0` | Explicitly cleared at `0x8002595C-0x80025964`. |
| `H` | `341` (`0x155`) | `SetGeomScreen(341)` at `initialization.c:261`; write at `0x80062638`. No later terrain-path write was found. |
| `OFX` | `256 << 16 = 0x01000000` | `SetGeomOffset(256,120)`; the Moby renderer's shared tail restores it at `0x80023958-0x8002395C`. |
| `OFY` | `120 << 16 = 0x00780000` | Same initialization; restored at `0x80023960-0x80023964`. |
| `DQA/DQB` | Ordinary `GS_Playing` entry: `0x100/0` | `func_800584C4` writes DQB at `0x80058518` and DQA at `0x80058520`; it is reached through `func_80058BA8` before environment rendering in the normal draw order. Other gamestate call paths can retain other values. |
| `ZSF3/ZSF4` | Residual, not a fixed terrain value | Initialized to `0x155/0x100` at `0x80062384/0x80062390`, then overwritten by the Moby paths at `0x8001F7F8`, `0x8001F90C`, `0x80020F94`, and `0x800210A8`. |
| `RBK/GBK/BBK` | HP camera scratch values | Overwritten by `r_environment` at `0x800267D8-0x800267E0`; they are not lighting background colors in this use. |

`r_environment` contains no `AVSZ3`/`AVSZ4` and does not read ZSF3/ZSF4.
Terrain ordering depth is computed manually. ZSF3/ZSF4 are therefore not part of
the minimum terrain projection contract. Reproducing their incidental values
would require the precise preceding Moby/model path, but would not change
terrain SXY/SZ.

Likewise, DQA/DQB affect the RTPS `IR0` depth-cue result, not `SXY` or `SZ`, and
the environment renderer does not consume RTPS-generated IR0 for terrain; it
loads IR0 explicitly before its color operations. `DQA=0x100,DQB=0` may be set
for an explicit normal-gameplay register fixture, but those registers must not
be presented as universal entry provenance across every gamestate.

## Position units and terrain input vectors

`g_Camera.m_Position` is fixed16 world position: 16 raw units equal one editor
world unit. The retail paths deliberately use two different camera scales.

### Low-poly input

At `0x80025968-0x8002597C` the renderer loads all three raw camera coordinates
and performs arithmetic `>> 4`:

```text
camX1 = cameraRawX >> 4
camY1 = cameraRawY >> 4
camZ1 = cameraRawZ >> 4
```

For a decoded integer editor world point `(X,Y,Z)`, the exact GTE vector order is:

```text
Vx = camY1 - Y
Vy = camZ1 - Z
Vz = X - camX1
```

The assembly derives the sector-relative terms at `0x8002623C-0x80026254`,
decodes each LP vertex at `0x8002628C-0x800262B4`, and runs RTPS at
`0x800262C0`. LP uses one GTE input unit per editor world unit.

### High-poly input and the background-register scratch

At `0x800267C0-0x800267E0` the raw camera coordinates are logically shifted by
two and written to RBK/GBK/BBK:

```text
camX4 = (uint32)cameraRawX >> 2
camY4 = (uint32)cameraRawY >> 2
camZ4 = (uint32)cameraRawZ >> 2
RBK = camX4
GBK = camY4
BBK = camZ4
```

The use of logical shifts is literal. An implementation should use explicit
unchecked 32-bit semantics instead of silently defining negative floating-point
camera behavior. Retail level coordinates are normally in the positive range.

For the raw sector words `xyPos`, `zPos` and packed HP `vertexWord`, the exact
input construction at `0x80026814-0x800268B0` is:

```text
baseX4 = ((uint32)xyPos >> 14) - camX4
baseY4 = camY4 - ((xyPos & 0xFFFF) << 2)
baseZ4 = camZ4 - ((uint32)zPos >> 14)

localX4 = ((uint32)vertexWord >> 19) & 0x1FFC
localY4 = ((uint32)vertexWord >>  8) & 0x1FFC
localZ4 = ((uint32)vertexWord <<  2) & 0x0FFC

Vx = baseY4 - localY4
Vy = baseZ4 - localZ4
Vz = baseX4 + localX4
```

RTPS begins at `0x800268C8`. HP uses four GTE units per editor world unit.
When the sector's discarded X/Z origin residues are zero, this reduces to:

```text
Vx = camY4 - 4*Y
Vy = camZ4 - 4*Z
Vz = 4*X - camX4
```

This simplification is valid for the audited shipped HP faces, but it must not
be made unconditional for future data without the source guard below.

## RTPS result and fixed logical screen

The environment uses `RTPS sf=1,lm=0`. Conceptually, for input `V` and the
control state above:

```text
MACi.pre = (TRi << 12) + sum(Rij * Vj)
IRi      = signed_saturate(MACi.pre >> 12)
SZ3      = unsigned_saturate(MAC3.pre >> 12)
F        = retail_UNR_divide(H, SZ3)       // saturates on divide overflow
SX2      = clamp(-1024, 1023, (OFX + F*IR1) >> 16)
SY2      = clamp(-1024, 1023, (OFY + F*IR2) >> 16)
```

An exact core must also reproduce 44-bit MAC behavior, the retail UNR reciprocal
table/division, IR/SZ/SXY FIFO updates, and FLAG saturation bits. The equations
above describe units and dependency, not permission to replace the exact GTE
integer behavior with doubles.

The native terrain screen is always a logical 512x240 surface centered at
`(256,120)` with `H=341`. Render into that coordinate system first, then scale
or letterbox for Avalonia presentation. Feeding arbitrary viewport bounds into
the native equation changes the claimed camera and is not equivalent.

## Editor Fly-camera adapter

The current Fly camera is `(X,Y,Z,Yaw,Pitch)` in
`EditorViewport.cs:9853`. Its projection is floating-point, uses a dynamic focal
length, rejects depth `<= 48`, and has no roll (`EditorViewport.cs:7472-7510`).

With the default game-view orientation, the editor maps:

```text
flyViewX = worldX
flyViewY = -worldY
flyViewZ = worldZ
```

For that orientation and zero native roll, the deterministic orientation adapter
is:

```text
nativeWorldX = fly.X
nativeWorldY = -fly.Y
nativeWorldZ = fly.Z

nativeRx = 0
nativeRy = -Pitch * 4096 / (2*pi)
nativeRz = -Yaw   * 4096 / (2*pi)
```

The contract must name its integer quantization and wrap the result to 12 bits.
It must likewise name the position conversion to fixed16, for example checked
rounding of `nativeWorld*16`. Those choices produce a stable editor-derived
camera; they do not recover the exact retail camera integer that once generated
a gameplay frame.

Fly state cannot reconstruct:

- the original raw fixed16 camera position;
- the original 12-bit gameplay rotation integers;
- `m_Rotation.x` roll;
- camera collision/occlusion group and sector-selection provenance;
- scripted, transition, title, or cutscene camera state;
- incidental prior-renderer registers that terrain does not use.

Use an imported pose/trace sidecar when a test requires gameplay-frame
equivalence. Do not require DuckStation merely to render the editor's own named,
quantized camera.

## Overlay data-sufficiency audit

A read-only inventory was run against all 35 current
`editor-cache/*-runtime-scene-editor-overlay.json` files and a user-supplied
clean USA retail BIN. The binary reader crossed
MODE2/2352 logical-sector boundaries and checked the overlay-derived sector
headers against the source BIN before evaluating residues.

Results:

- 35 levels;
- 6,226 sectors total;
- 6,047 HP-bearing sectors;
- 190,640 HP faces;
- zero sector-header mismatches;
- zero HP-bearing sectors with nonzero
  `xOriginResidue = ((uint32)xyPos >> 14) & 3`;
- zero HP-bearing sectors with nonzero
  `zOriginResidue = ((uint32)zPos >> 14) & 3`;
- 179 sectors had nonzero Z residue, but every one was LP-only and had zero HP
  faces;
- the audited special-center/header bit 12 was clear in every current sector.

Therefore, current integer world points contain enough numeric information for
all shipped HP faces. This is a verified property of the current source corpus,
not a property expressed by the overlay schema.

Implementation update (2026-07-16): source-scene overlay format 7 now retains
all four raw sector header words, including `xyPos` and `zPos`, under
`raw-sector-header-words0-3-integer-world-xz-quarter-residue-v1`. The loader
validates the derived center, radius/flags, decoded origin, residues, and exact
HP face-sector coverage. See
`docs/research/native-terrain-hp-coordinate-provenance.md`.

The two schema choices considered were:

1. retain raw `xyPos` and `zPos` (preferred, because it preserves provenance and
   lets the exact HP vector formula work for nonzero residue); or
2. export both two-bit HP origin residues plus a versioned validation result,
   and fail closed if an HP-bearing sector has unavailable/unvalidated residue.

A single global `hpProjectionOriginResiduesZero=true` certificate would be sufficient
for the present corpus only if it is generated from the source bytes, versioned,
and rejected whenever source identity changes. The implementation chose raw
sector words so future nonzero residue data can be represented without a schema
change.

## CPU outcodes, GTE clamps, and GPU draw area

These are three different boundaries and should not be conflated.

### GTE SXY saturation

RTPS clamps each screen component independently to signed 11-bit range:
`-1024..1023`. Saturated SXY is not itself the GPU draw clip.

### Retail environment CPU outcodes

The HP base path loads `0x00010000`, `0x01000000`, and `0x02000000` at
`0x80026844-0x8002684C`. For packed `sxy = (uint16)y<<16 | (uint16)x`, the
tests at `0x80026A8C-0x80026ADC` are exactly:

```text
bit 0x01 if (int32)(sxy - 0x00010000) <= 0
bit 0x02 if (int32)(sxy - 0x01000000) >= 0
bit 0x04 if (int32)(sxy << 16) <= 0
bit 0x08 if (int32)((sxy << 16) - 0x02000000) >= 0
bit 0x10 if the GTE FLAG summary sign is set
```

For ordinary nonnegative, nonsaturated coordinates the first four correspond to
an interior of `x=1..511`, `y=1..255`. Faces are rejected when all their vertices
share one of bits `0x01..0x08`. This is a coarse CPU reject window, not the final
visible raster area.

The generated-HQ repair pass repeats the same packed outcodes at
`0x8002A2EC-0x8002A334` and `0x8002A4B8-0x8002A500`. It also rejects/repairs
large projected spans at `0x8002A5AC-0x8002A638`: pairwise differences must
satisfy `abs(dy) < 0x200` and `abs(dx) < 0x400`.

### GPU draw area and offsets

Graphics initialization defines:

```text
draw buffer 0: rect (0,   8, 512,224), drawing offset y=0
draw buffer 1: rect (0, 248, 512,224), drawing offset y=240
display 0:     rect (0, 240, 512,240)
display 1:     rect (0,   0, 512,240)
```

See `initialization.c:60-70`. After removing the buffer drawing offset, both
buffers have the same logical GPU draw area:

```text
x = 0..511
y = 8..231
```

The GTE center remains `(256,120)` in the 512x240 display space. The two physical
buffer Y offsets differ by 240, which is divisible by four and therefore keeps
the GPU dither phase aligned.

### Near-camera precision rerun

On the HP special projection path, the code at `0x80026A04-0x80026A88` handles
very near vertices. When the original `SZ3 < 0x100` and both original MAC1 and
MAC2 are strictly inside `(-0x100,+0x100)`, it shifts the packed input vector
left four bits, reruns RTPS, and uses the new SXY while retaining the first
source depth for subsequent logic. A base projector that omits this can still be
useful for research, but it must not claim pixel-identical near-camera behavior.

## Smallest safe implementation boundary

Use a contract such as `native-terrain-editor-camera-gte-base-projection-v1`.

### In scope

- Inputs:
  - explicit fixed16 `cameraRawX/Y/Z`;
  - explicit 12-bit `rx/ry/rz`;
  - retail trig tables;
  - raw/validated sector origin provenance;
  - original static LP and HP vertex payloads.
- Matrix:
  - exact two-stage PSYQ `MulMatrix` semantics;
  - unscaled `m_ViewMatrix` only.
- Registers:
  - `TR=0`, `H=341`, `OFX=256<<16`, `OFY=120<<16`;
  - optionally set normal-gameplay `DQA=0x100,DQB=0` for a complete named
    fixture;
  - explicitly exclude ZSF3/ZSF4 because terrain projection does not use them.
- Projection:
  - exact LP and HP vector units above;
  - exact RTPS/RTPT integer behavior;
  - emit SXY, SZ, FLAG, and source-corner identity;
  - perform native work at fixed 512x240, then scale for display.
- Label the Fly adapter output as `editor-camera-derived`.

### Explicitly out of scope for v1

- equivalence to a particular retail gameplay frame without a captured pose;
- camera occlusion groups or exact retail sector-selection state;
- arbitrary-Avalonia-size native projection;
- full NCLIP/backface and environment face-selection behavior;
- recursive near-plane repair/subdivision;
- generated HQ lattice projection and tile-local OT ordering;
- complete CPU/GPU clipping and raster edge rules;
- non-terrain packet interleaving;
- residual full-machine GTE state that has no terrain effect.

This boundary is small enough to verify with register/projection fixtures and
truthful enough to ship as an editor preview foundation. Promotion to a full
native terrain preview should require separate evidence for base OT selection,
near-camera repair, generated-HQ projection, and final GPU raster/clipping.

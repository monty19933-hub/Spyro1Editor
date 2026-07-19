# Native Spyro 1 terrain camera/register foundation

Status: exact standalone fixed-point builder and coordinate adapters are
implemented and smoke-locked. They are not wired to Fly 3D and do not identify
or claim a particular gameplay camera frame.

## Camera matrix and registers

`SpyroNativeTerrainCamera` accepts only an explicitly certified retail camera
position (signed fixed-16 integers) and the three signed 4096-turn rotations.
Uncertified inputs fail closed.

The builder follows `CameraUpdateMatrices` in the local Spyro 1 decomp
(`src/camera.c:27-87`):

```text
R = (Rx(rotation.y) * Ry(rotation.z)) * Rz(-rotation.x)
```

Each sine/cosine value comes from the retail 16-step Q12 table plus the retail
signed linear interpolation (`asm/math.s:221-272`). Matrix products use the
same Q12 arithmetic and signed right shift as PSYQ `MulMatrix`
(`asm/psyq.s:7973-8042`). The 257 interpolation endpoints at `D_8006CC78`
match this SHA-256 as little-endian words:

```text
84565C097F34B5722AC4EB294CB799846852BD56C4C760A630B540496C34EDEA
```

The environment renderer loads the unscaled view rotation and clears GTE
translation at `asm/renderers/r_environment.s:31-51`
(`0x80025934-0x80025964`). The standalone register result therefore uses:

```text
RT  = retail Q12 view rotation
TR  = (0, 0, 0)
OFX = 256 << 16
OFY = 120 << 16
H   = 341
DQA = 0x100
DQB = 0
```

DQA/DQB are retained as explicit register state, but the terrain HQ tier and
tile-bucket contracts consume generated SZ rather than the depth-queue output.

## LP and HP coordinate adapters

The typed packed input preserves sector word `+0x08`, sector word `+0x0C`, and
the selected packed vertex word. The raw-word certification is required so an
editor-space float or a display-space quarter conversion cannot be passed off
as native input.

LP follows `r_environment.s:649-695` (`0x8002623C-0x800262F0`). It decodes a
one-times world lattice and emits this GTE permutation:

```text
VX = (camera.y >> 4) - world.y
VY = (camera.z >> 4) - world.z
VZ = world.x - (camera.x >> 4)
```

HP follows `r_environment.s:1025-1108` (`0x800267C0-0x80026904`). Its world
lattice is four times larger, including the two sector-origin bits that a
display-space divide by four discards:

```text
VX = (camera.y srl 2) - world4.y
VY = (camera.z srl 2) - world4.z
VZ = world4.x - (camera.x srl 2)
```

The HP adapter reproduces the unsigned MIPS logical shifts, 32-bit wrapped
subtractions, and low-halfword GTE writes. It also verifies the corresponding
signed semantic delta is in `[-32768, 32767]`; a coordinate that would only
exist through halfword wrapping fails closed.

## Focused verification

`Spyro.Editor.GteProjectionSmoke` locks:

- the exact retail trig table and Sin/Cos interpolation boundaries;
- zero and three cardinal camera matrices;
- RT/TR/OFX/OFY/H/DQA/DQB constants;
- LP and HP packed-word decodes, coordinate permutations, and scales;
- HP sector-origin low-bit retention;
- negative camera positions through the literal `srl`/wrapped-subtract path;
- uncertified-input rejection and signed-halfword overflow rejection;
- 4,096 deterministic camera/register/coordinate fixtures.

The 4,096-fixture stream SHA-256 is:

```text
FFA14387AA9061304B0C993C4F01F77A0AE2ADB7CA8DDF664B513D9964D6E79D
```

## Production boundary

This foundation intentionally has no `EditorViewport` dependency. Production
wiring still requires a proven source of retail fixed-16 camera position,
signed rotations, raw scene coordinate words, and the surrounding native
projection/NCLIP/FIFO state. Float Fly 3D camera values are not accepted by
this contract.

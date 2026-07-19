# Native terrain HP base-projection contract

Status: exact isolated integer contract implemented and smoke-locked. It is not
wired into Fly 3D or any production terrain packet path.

## Contract boundary

`NativeTerrainBaseProjection` accepts only:

- explicit `PsxGteProjectionRegisters`;
- an explicit initial `PsxGteProjectionFifo` (or the empty FIFO overload);
- an explicit audited queue context; and
- exactly three projection points or four caller-proven raw face slots, each
  already represented as `PsxGteVector`.

The contract name is:

```text
native-terrain-hp-base-projection-explicit-slots-v1
```

It deliberately does not decode face words or guess that a triangle repeats a
fourth slot. Three-point input supports projection/outcode research but does
not claim native face NCLIP. For exact face evaluation, the caller supplies all
four raw slots, including native triangle repetition.

## Explicit queue contexts

The low tagged-pointer bits are tested at
`r_environment.s:1045-1050`, `0x800268B4-0x800268C4`. The API requires one of
three normalized contexts so the full tagged behavior cannot be applied to a
different retail queue:

| API context | Retail branch | Scratch representation |
| --- | --- | --- |
| `RawSxy` | tag bits `0` -> `0x800268C8` | raw packed SXY, no projection-loop common-outside test |
| `SimpleTaggedContextBit0` | tag bits `1` -> `0x80026930` | `SXY << 5` plus the simplified packed outcode |
| `FullTaggedContextBit1` | tag bit `2` set -> `0x800269CC` | full packed outcode below `SZ=0x600`; simplified packed outcode at or beyond it |

Retail tag values `2` and `3` both normalize to
`FullTaggedContextBit1`. Near-camera reruns occur only in that normalized full
context.

## Sequential RTPS and near-camera rerun

Every supplied source slot executes RTPS with `sf=1,lm=0`. The result retains
the original SXY, SZ, FLAG, MAC1, and MAC2 state, as well as the FIFO after the
command.

The full tagged path at `0x80026A04-0x80026A88` reruns a slot only when all
three original-result predicates are true:

```text
original SZ3  <  0x100
-0x100 < original MAC1 < +0x100
-0x100 < original MAC2 < +0x100
```

The bounds are strict. The packed VXY data register is shifted left four and
masked with `0xFFF0FFF0`; the sign-extended VZ register is shifted left four.
Each component is then truncated/sign-extended as a 16-bit GTE vector register.

The rerun replaces the scratch SXY and effective FLAG. The stored terrain depth
remains the first RTPS SZ3. Because the retail code really executes another
RTPS, the returned final GTE FIFO **does include the rerun** before the next
source slot is projected.

## Packed CPU outcodes and reject mask

For the full block at `0x80026A8C-0x80026ADC`, all comparisons retain unchecked
signed MIPS 32-bit packed-word behavior:

```text
0x01 when int32(sxy - 0x00010000) <= 0
0x02 when int32(sxy - 0x01000000) >= 0
0x04 when int32(sxy << 16) <= 0
0x08 when int32((sxy << 16) - 0x02000000) >= 0
0x10 when the active RTPS FLAG has bit 31 set
```

The full tagged branch uses that block only for original `SZ3 < 0x600`. Its
`SZ3 >= 0x600` branch and the simple tagged context use the independently
audited simplified form at `0x80026978-0x800269A0` and
`0x80026B04-0x80026B2C`:

```text
0x01/0x02 use the same first two packed tests
0x0C when (sxy & 0x0000FE00) != 0
FLAG is not consumed
```

Tagged paths store `(sxy << 5) + outcode`. Their common-outside mask is the
bitwise AND of every stored slot word, masked with `0x0F`; any nonzero result is
the retail coarse reject. Raw-SXY context does not run this projection-loop
reject.

## NCLIP/backface values

The contract computes the exact signed NCLIP MAC0 determinant:

```text
SX0*SY1 + SX1*SY2 + SX2*SY0
- SX0*SY2 - SX1*SY0 - SX2*SY1
```

`ComputeGenericThreePointNclipMac0` exposes the exact determinant as a clearly
named mathematical primitive, but three deduplicated points are not labeled as
a retail face-slot result.

Exact face evaluation requires four caller-proven raw slots. It reports the
retail first value over `[0,1,3]` and second value over `[2,1,3]`, matching
`0x80026C30-0x80026C88`. A native triangle must therefore retain its repeated
fourth raw slot; coordinate equality is not used to infer it. The result
contains raw signed MAC0 values. Face-word winding bits and the renderer's
conditional first/second-triangle policy remain the caller's responsibility.

## Focused verification

Run:

```bash
dotnet run --project src/Spyro.Editor.NativeTerrainBaseProjectionSmoke/Spyro.Editor.NativeTerrainBaseProjectionSmoke.csproj --configuration Release
```

The smoke locks:

- all three explicit queue contexts;
- three- and four-slot FIFO/order fixtures;
- strict `SZ/MAC1/MAC2` near-rerun boundaries and packed-component wrap;
- the extra RTPS command's FIFO effect while retaining original SZ;
- full and simplified outcode boundary transitions, including `SZ=0x600`;
- common-outside accept/reject fixtures;
- signed/zero generic NCLIP values, three-point fail-closed behavior, and exact
  caller-proven four-raw-slot order; and
- 4,096 deterministic three/four-point projection sets, including 661 near
  reruns.

The locked SHA-256 is:

```text
1426B5934F92038E5E09383F4AAF61DC3A753233AE530CF654432D69BD426CE3
```

## Remaining limits

This contract is not a complete `r_environment` implementation. It does not
claim or provide:

- retail camera/register derivation or proof that supplied vectors came from
  exact source sector/face words;
- face-word flip/winding policy, sector selection, depth sums, LOD choice, or
  base ordering-table insertion;
- complete CPU/GPU clipping, near-plane polygon repair, raster edge rules, or
  packet interleaving;
- generated HQ lattice projection, HQ seam/repair NCLIP, recursive subdivision,
  or tile-local ordering; or
- EditorViewport, overlay-schema, cache, BIN/CUE, or DuckStation wiring.

Those remain separate provenance and renderer-integration contracts.

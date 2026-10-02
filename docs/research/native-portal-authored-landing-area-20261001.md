# Authored portal arrival floor policy — 2026-10-01

`NativeAuthoredSpawn.RequirePortalArrivalSupported` requires continuous supported
floor over a square extending 192 editor units in each horizontal direction from
the scene-header spawn, after the native one-sixteenth-unit spawn rounding. The
square is 384 units wide. Floor must remain between 22.25 and 128 units below
the rounded spawn across that entire area. Horizontal area radius and vertical
player clearance are deliberately independent. `RequireSupported` retains the
original 22.25-unit horizontal footprint and its original vertical limits.

The guard reuses convex polygon subtraction over the exact native mesh triangle
split: triangle `(0,1,2)` and, for a quad, `(0,2,3)`. It clips each triangle to
the allowed vertical range and refuses slopes with gradient magnitude above
one, then subtracts its supported area from the required square. It accepts
only when no positive-area fragment remains. This avoids a point-sampling grid
that could miss holes. Calculations use spawn-local doubles, with the existing
`1e-9` area residue threshold and 4,096-fragment refusal. Native mesh compilation
continues to enforce whole-unit authored vertices independently. The check
does not modify the document or add floor to saved terrain.

## Why a larger area is required

The USA executable's ordinary incoming-spawn consumer at `800139BC..80013A40`
adds the indexed arrival height and offsets XY by a quarter of the native
trigonometric vector. Incoming animation setup at `800161B0..80016284` and the
state-9 driver at `8002E000..8002E084` also establish and advance the entry
motion. Returning to normal player control does not establish that Spyro has
already settled onto the floor. The ID65 height correction is separately
authenticated by `PatchOrdinaryPortalArrival`; this floor check does not change
any executable bytes, classifications, callbacks, or animation state.

The root agent's R7 runtime trace, with input released before arrival, showed
the height-corrected ID65 player at raw `[8960,8536,17408]` in arrival state 9,
then `[8960,10277,21592]` in normal state. It later crossed the old floor height
8704 between `[8960,6527,9438]` and `[8960,6502,8438]`, outside the authored
128-wide platform. Linear interpolation between those two observations gives
about 153.21 editor units of center displacement behind the spawn. This
interpolation is an estimate between observations, not an exact physics result.
Adding the existing 22.25-unit player footprint exceeds a 160-unit radius.

Trace evidence is preserved at:

`/Users/john/Documents/Codex/2026-09-29/files-pasted-by-the-user-spyro/work/universal-gameplay-20261001/runtime/portal-r7-transition.jsonl`

The 192-unit radius is a conservative authoring requirement chosen from the
source behavior and that observed failure. It is **not a proven maximum native
trajectory across all headings, retail arrival heights, animation variants,
input, obstacles, or physics states**. In particular, higher retail entry
heights can permit more motion before touchdown. The guard certifies continuous
floor area and its vertical band; it does not certify unobstructed airspace or
successful gameplay landing. Further runtime cases remain necessary.

## Scope and checks

The export integration applies this policy to authored retail replacements,
which can be entered through unchanged retail portals, and to fresh ID65 scenes
when configured routes make them portal destinations. Existing authored terrain
is preserved; a refusal asks the user to add a continuous 384-wide landing area
centered on the player start, or move the start to a suitable platform.

Dedicated source-free geometry cases cover 128/320 rejection, exact 384 coverage,
shared triangle edges, a one-unit off-center hole, a sub-unit slit, the exact
128-unit drop boundary, independent 22.25-unit vertical clearance, slopes,
native spawn rounding, large coordinates, and document fingerprint preservation.
These are geometry and regression checks, not additional emulator acceptance.

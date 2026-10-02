# Imported placement visibility, 2 October 2026

Continued from `396228a`. A new imported row now receives the native unrestricted
visibility sector `FF` after placement overrides. A donor sector identifies the
donor's terrain visibility graph and can hide an actor in unrelated terrain.
This also applies to same-level clones because the editor can rebuild terrain.
Existing destination rows retain their original visibility assignments. Ordinary
native distance culling still applies; this change does not disable all culling.

The registered `UniversalPlacementVisibilitySmoke` passes 1,212 checks. It uses
the exact Haunted Towers Firework chest row142 into Peace Keepers, with four
Cannon Gnorc rows removed and physical row2 reused. Absent, zero, original8C,
and FF overrides produce identical output. Surviving actor scalar bytes stay
exact. Four moved native gem clones retain their native behavior and unchanged
original rows. All 107 original Town Square rows independently confirm the
native FF sentinel. Source data remains immutable.

The synthetic particle fixture in `UniversalMobySmoke` now supplies a complete
model-root/SPU/sound layout and checks that particle insertion preserves the
entire following sound component. Its 102 checks pass, as do all 115 regression
runner tests. The editor Release build has zero warnings/errors.

## Controlled comparison and normal export

Evidence root, relative to the task workspace:
`work/universal-finish-20261002/runtime`.

`peace-firework-r2` (invisible) and `peace-firework-r3` (visible) differ by exactly
one user-data byte: source visibility sector8C becomes FF at raw31766994,
LBA13506. The only other 36 changed raw bytes are that sector's EDC/ECC. The
independent receipt is `peace-firework-r3/controlled-diff.json`.

The product normal-export rebuild `peace-firework-r4` uses no sector override
and is byte-identical to r3. It passes 260 independent readback checks covering
the executable, changed level data/overlay, all 254 unrelated WAD entries,
placement and imported/removed classes. Candidate BIN SHA256:
`dd4bde13605d7e197025b7b525129df685b7bf842bc0c84cd7d109cf35176e61`.

## Native gameplay evidence

Both runs use the isolated `Spyro Universal Gameplay Verification.app`, full USA
BIOS, software renderer, recompiler, a fresh private card, normal controller
input, no save states and no RAM writes. The candidate retains normal New Game
flow with an explicit native Peace Keepers entry binding. Original source BIN,
user projects/cards and prior packages are not edited.

The r4 run visibly shows the imported chest, flame ignition, fuse sparks,
explosion, flying debris, Spyro recoil/stars and yellow-to-blue Sparx, then the
blue gem reward. Read-only RAM captures confirm both gem counters increase
from0 to5. The parent reaches state253 and its checkpoint flag after explosion;
the durable flag becomes set after the gem is collected. The earlier r3 native
pause screen also shows five gems. Screenshots and exact launch/capture receipts
are in `peace-firework-r4/runtime-proof.json` and `peace-firework-r3/runtime-proof-r1.json`.

Audio is muted in this isolated emulator and is not assessed. Card save/reload,
death and level reentry are not verified here. This is one bounded successful
imported-object interaction, not complete universal coverage. Positive Flight,
Magic Crafters and Haunted Towers linked-enemy work remains in progress.

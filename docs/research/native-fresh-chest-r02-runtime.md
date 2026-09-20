# Native chest R02: opening works, last reward escapes

September20,2026; source92df9ac. This is a failed complete-reward-collection
test with useful partial successes, not a public editor feature or a complete
Moby lifecycle certificate.

## Exact artifact and session

The isolated run is
`_local/native-v6-chest-runtime-20260920-r02.hpMyEW`.
Its candidate is `candidate-r02/fresh-chest-p9-experiment-r02.cue`.
Full BIN SHA256:
`0e1120a8f9a4c79ea1011bd63147892b10c7e8502204ef5e1038b652285d3998`.
CUE SHA256:
`dc573dd0ce3e11fe75cea34cc129d5a30810f765afc157d38afa1ace9d139c79`.
The physical export passed392 assertions, actual full readback and complete
281270-sector inverse. Final source checks510/511 pass9676/2355; full
regression508 passes343 command steps with zero failures.

DuckStation PID3932 opened that exact661547040-byte BIN, independently checked
with lsof; its command used the exact CUE and slowboot. Full Sony firmware,
game presentation/title and the copied normal save were observed. Entry and
gameplay used native controller inputs only. No foreign savestate, cheat,
RAM/register write, fabricated helper return or changed native physics was
used. The bounded GDB observer read sequentially, with consistent gate rereads
in20 accepted samples00..19; its annotations are not disc authentication.

Paced frame-step requests with intervening AX observations stayed at most20
between observer samples. Earlier rapid requests coalesced, so a requested
step count is not asserted to equal elapsed video or game-update frames.

## What actually happened

| Samples | Observed result |
| --- | --- |
|00..04|Spawn XY8960/8960 remains stable; landing Z9066..9069. No R01 immediate displacement. Wizard attacks are active.|
|06|Key state1; level65/treasure slot35 unchanged at0. Native key pickup occurred.|
|09..10|AE chest enters native opening state2 and advances its private timer.|
|11|Chest retires, key state2; debris310/311,46/kind1 particles and gem emission observed.|
|12..14|All five D reward controllers retire; actual slot35 reaches2.|
|15|Actual slot35 reaches5.|
|16|Actual slot35 reaches9, global67 from58; tracked-gem transition sets key state3. One red gem remains active.|
|17..19|That red gem leaves the floor and falls; actual slot35 remains9.|

All five controllers retiring and key state3 are insufficient completion
criteria: the native tracked-gem transition can occur before every sibling
reward has been collected. The animated HUD was still counting up, so it is
also not the authoritative treasure total.

The final red gem, dynamic index24/class83/state0,2, is at:

| Sample | X | Y | Z |
| --- | ---: | ---: | ---: |
|16|10659|11581|8722|
|17|10559|11677|8702|
|18|10309|11917|7952|
|19|10059|12156|6203|

Floor Y ends11520 at Z8704. The final sample is2501 units below the floor.
The gem is still active; its eventual deletion was not observed. The chest
credited9 of its deterministic10, not the whole level target20. Wizard's own
reward, ordinary-speed idle, death/re-entry and complete reset were not tested.
Wizard damage reduced player health to0 at18 without an observed completed
death. No accepted sample reported renderer exhaustion.

## Engineering consequence

The R02 source-owned initial sphere and landing-column clearance are useful
and their bounded claims remain true. The five saved reward-controller centers
also lie on the floor, but that does not contain emitted reward trajectories.
Do not promote those geometry tests to a gameplay certificate.

Preserve R02 and its explicit coordinates. Investigate native emission motion
and a separately identified layout candidate with adequate landing room.
Keep chest yaw/collision, gem behavior, source-owned offsets, old export
goldens and the public2048 limit unchanged. Any corrected layout needs fresh
complete export/inverse checks and another cold-boot native interaction test.

## Preservation

Observer and DuckStation exited normally. Save State For Resume was unchecked.
After-test INIs were preserved, then both live INIs restored byte-for-byte:
global `c07823d8de99c88b560fb5dc356fd1247aa391b88ff91284d7ef1354c10fb854`;
game `629d9bd3eb9890eedb2bfd9ae2458f7f214272f951a9081f687abe8e5f11d1f3`.
The original memory card remains
`d69b43f8be2585f7b5e7fd1b07f85217ba74c01f102c01d7a22928ee0e9e8ae2`.
Original BIN/CUE were rehashed unchanged. Private card changes were never
copied back. The R06 editor and normal editor projects remain untouched.

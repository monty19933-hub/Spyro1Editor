# Fresh chest R01: failed initial spawn

September20 continuation. Preserve this failure; do not overwrite the candidate
or promote physical correctness into gameplay acceptance. Source checkpoint
`f46ab15` and the preserved R06 editor are separate artifacts.

## Exact candidate and observed boundary

Private evidence root:
`_local/native-v6-chest-runtime-20260919-r01.nqKxBi`.
Candidate: `candidate-r01/fresh-chest-p9-experiment-r01.cue`.
Whole BIN SHA256:
`4ba67d92fef764ab80d4e70a3b233e5b3e75a69e2aa0ae74c4ffb3410c58284b`.
Physical output490 passed544 assertions, including actual readback and the full
281270-sector inverse to Original. Cold boot used full firmware, a copied normal
save and ordinary LAB controller input. The process/open BIN were independently
identified. No foreign state, cheat or RAM/register write was used.

The first native entry showed Spyro falling against an empty background and
eventually Game Over. Observer01 sample01 was already level60/game5 and correctly
refused interpretation. After native restart/reload, accepted samples04/05 were
level65/game0 with all P9/N32 row/private/layout checks passing. However sample04
was already XYZ8710/10968/10220, state6/timer30, beyond floor Y10496. These views
do **not** establish missing terrain or failed geometry loading.

For a closer boundary observation, temporary native speed10% and frame stepping
were used. Observer02 samples00..22 remained loading refusals. Its first accepted
normal sample23 was level65/game0/state6/timer0:

| Observation | Sample23 | Sample24 |
| --- | --- | --- |
| XYZ |8657/8960/9322|8356/8960/9419|
| Both motion vectors |-9696/0/3392|-9632/0/3104|
| State timer |0|2|
| Health/damage |3/0|3/0|
| Dynamic objects |Sparx78 only|Sparx78 and bolt26|

Relative to stored spawn8960/8960/9216, sample23 motion is exactly32 times its
displacement. Sample24 is already west of floor edge8448. This is a sequential
boundary observation, not an in-helper collision trace or proof of the complete
sphere-normalization calculation. It does not explain the first run's later
northward motion. No key pickup, chest opening or reward collection was observed.

## Independent source diagnostics

`source_fresh_chest_spawn_collider_diagnostic.py` authenticates held/current
Original, candidate, source helpers, itself and the frozen sample04 line before
and after use. It pins native instruction windows and actual AE collider bytes.
For this type1 sphere, yaw/matrix/animated vertices do not enter the tested path.

- AE descriptor: `000003A8 02580101 01500000 00000004`.
- Sphere radius600; signed Z offset336; center9472/8960/9040.
- Default player-query radius356; stored spawn8960/8960/9216.
- Squared separation293120 is less than combined-radius squared913936.
- Broad-phase separation524288 is less than squared threshold1669264.

The native response-disable bit is clear; incoming accumulator mask4 must not
be labeled zero-damage just because the observed health stayed full. These
source facts prove a conditional stored-spawn overlap and support the initial
push-out explanation. Root replay492 passed complete image guards. This is not
full GTE/native collision execution. Current placement proves only center
containment; `FootprintClearanceProven` deliberately remains false. The earlier
Wizard clearance check does not cover the newly introduced chest collider.

`source_fresh_chest_collider_lookahead_diagnostic.py` rules out an unrelated
missing-terminator theory. AE's semantic package ends at1008; the last command's
bit is tested by8004E590/594. The8004E598 load delay slot reads four bytes from
the next owned package, but the value is not dispatched. Original next word2
and candidate next word1 have identical final-command behavior. Seven neighbor
values, an unreadable-memory refusal and a non-final hostile control were
checked by a five-instruction native cut. Root replay493 passed. There is no
justification for inserting padding or changing the package's terminator.

## Protocol and preservation

The first observer refused a requested sixth sample after80 video frame steps
queued more than its32-event safety bound. Five earlier samples are retained;
there is no accepted sample06. A shell pipeline without pipefail returned tee's
exit0, not observer success. The replacement pipeline uses pipefail, a fresh
connection and at most20 video-frame steps per read. No guard was relaxed.
Observer491 adds signed health/motion fields and passes nine offline tests,
with no ROM access/socket connection. Input counts are not asserted to equal
game updates; sample timers provide the observed update boundary.

DuckStation exited normally. No resume savestate was written. Both original
INIs were restored byte-for-byte; the original memory-card SHA256 remained
`d69b43f8be2585f7b5e7fd1b07f85217ba74c01f102c01d7a22928ee0e9e8ae2`.
Private autosave changes were never copied back. Protected project, original
ROM and R06 app were not replaced. Full session notes and settings backups live
under the private evidence root.

Observer01 JSONL SHA256:
`5359da0a98f9ac703ac7cec27c067111f8537078f609e6f486e0b08fb5dee802`.
Observer02 JSONL SHA256:
`319e8faecb6034601ea0af7fa5ab7749dc519a059fdeb85e58129759dd5c0766`.
Diagnostic492 SHA256:
`a66004ad9fd42b5c20580adb3d8be2783f6e65b65f32e5bcaf94a47e6c057b3b`.
Diagnostic493 SHA256:
`962d137905cd5036c71840d06d39023163e4175643d929b03c9f46b12c2d64c6`.

Next gate: source-owned initial/landing clearance and a separately identified
corrected candidate, followed by actual native entry and chest lifecycle testing.
Do not hide this failure by clearing velocity, disabling behavior, shrinking the
retail collider or loosening observation guards.

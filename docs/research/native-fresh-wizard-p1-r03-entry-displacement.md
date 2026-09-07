# Fresh Wizard P1 R03: early spawn-displacement evidence

September 7, 2026. The exact research candidate remains unapproved for gameplay.
This run captured the earliest observed loading-to-gameplay transition and strongly
supports a non-damaging collision push-out at the authored spawn. No placement,
native behavior or physics code was changed to obtain this result.

## What the run observed

At10% native pacing, the first entry was sampled before any observed life loss:
game0/state6/timer20, full health3/lives4 and motion away from the small floor.
The native inventory showed LAB0/10. All controller macros were paired and
released. The same native selector then reloaded the same level.

The repeat used bounded loading intervals, followed by22 individual native F6
requests, each with a separate manual read-only observation. Alternate requests
left the Spyro update unchanged: a video frame is not one game update.

| Observation | Game/state/timer | Player XYZ | Both sampled motion vectors |
| --- | --- | --- | --- |
|32, last loading sample|1 /15 /310|9034,10676,10527|current0,0,0; secondary-352,3264,0|
|33, first normal sample|0 /6 /0|9179,8966,9361|7008,192,4640|
|34, next video frame|0 /6 /0|9179,8966,9361|7008,192,4640|
|35, next game update|0 /6 /2|9395,8971,9497|6912,160,4352|

Sample33 had health3, damage accumulator0, lives4, state inputs18/0 and only
Sparx among active dynamic rows; no bolt was sampled. Its accepted scene/model/
callback/layout fields were reread. All report-level gameplay/export approval
flags remain false. Sequential reads are not an atomic CPU trace.

## Why this points to spawn overlap

The actual stored spawn is8960/8960/9216. The source Wizard has a type3 sphere
of radius400; the ordinary player query adds356. Source frame0 vertex0 decodes
to native components(-156,10,-240). With the actual initial identity matrix and
Wizard row8704/8960/8704, its sphere centre is8548/8950/8944.

The saved spawn is only493.789 units from that centre, inside combined radius756.
Crucially, sample33 still has that exact Wizard row, identity matrix, zero angles
and current animation0/frame0. Its player position is756.509 units from the same
centre—just outside the collision boundary. Displacement from the saved spawn is
219/6/145; multiplying by32 gives exactly both observed motion vectors7008/192/4640.

Original native code allows mode1 position push-out even when the incoming
damage flags are zero. The player caller then derives movement from displacement;
the state6 path can retain it. This is a close source/runtime match for initial
overlap producing the unwanted movement. It is not a directly observed in-helper
GTE execution, nor proof that no other operation ran between observations.

The earlier sample9 was already outside the then-current animated sphere. Its
animation1/frame2 differs from sample33; substituting the original frame there
would have been incorrect. Late full health alone was not enough to diagnose this.

The player timer is also not the loader counter. Original loading dispatch and
normal dispatch differ; the latter updates actors before the player. The observed
first normal sample is not a claim to have sampled a pristine playerstate0 or
every initialization instruction.

## Evidence and independent source checks

Exact runtime directory:
`_local/native-fresh-wizard-p1-runtime-20260907-r03.mvWT2n/`.
The complete successful observer02 stream has36 samples0..35 and SHA256
`9a721ac35712da926c67e8b7e12b1f9a2f4392ba52e9ca58ca70a0bedc7bd18e`.
Its final record is session_closed with all final full owner/source checks passed.

Actual candidate BIN/CUE/receipt are the unchanged clean publication under
`fresh-wizard-publication-validation-r01.0eF4Tz/candidate-r01/`, basename
`native-id65-fresh-wizard-p1-n32-r01`. BIN SHA256:
`c4a70f826b1a9a3d4ad8e36512be354b6205d27ea2c7a314776a51067f35b5a6`.
The observer source was frozen at5e16c941…5f870, with both exact native caches
independently bound before any sample. No observer code changed during the run.

Read-only source notes under `_local/research/dark-wizard-native-closure-20260906/`:

- `fresh-p1-spawn-collider-r01.N44ss3/NOTE.md`: complete original/P1 owners,
  native shape/transform and non-damaging push-out. Root and independent peer
  replayed the frozen inspector byte-for-byte; final log SHA73fc76f4…ceecd50.
- `fresh-p1-load-transition-r01.QzMrdW/NOTE.md`: loading/dispatch timing and
  offline sample9 decode. Root replay matched final log SHA59716314…fa1564.

Neither inspector writes a ROM or executes native collision code. These source
facts must not be promoted into universal Moby collision or clearance support.

## Permanent regression witness

`tools/tests/test_fresh_wizard_spawn_clearance_source.py` now preserves this
unsafe case. Root and independent peer fully reviewed its290 lines, including
the native input/output coordinate operands, signed scale, zero-damage response
and confirmed identity/current-pose restriction. Source mode reads the full
original and issued images; recorded mode separately binds the exact closed
36-sample log. Default discovery performs no ROM or recorded-fixture reads.

Root replay: whole Python183 tests with11 explicit-mode skips; the new source/
issued and recorded runs each pass3 tests with the other mode skipped. Existing
source/actual-files/recorded-OS observer26 tests also pass with no skips. Final
new test SHA5583bc07323ec6053ce9ba31e47a49e59182617a2e8f984ad2929bcf59eb3402.
Exact commands and honest test-helper/CLI REDs are retained in
`fresh-spawn-clearance-test-r01.wKvFgT/HANDOFF.md` under the private research parent.
These tests guard the evidence and its limits; the production compiler does not
yet reject unsafe spawn geometry. No new placement or game image was written.

The later offline sample33–35 decoder also reproduced exactly under Root review:
`fresh-p1-entry-sample33-r01.Bumy6o/NOTE.md`. Actual postquery displacement-to-
motion and state6 source windows were independently replayed. Reference position
and divisor were not sampled, so the measured32× relation is not a helper trace.

## Session closure and preservation

DuckStation45593/exec14240 exited0; observer02/exec21415 exited0. The initial
paused connection attempt61928 refused an established-socket query and exited2;
its separate failed stream remains failed, not merged into the successful run.
The second connection was an explicit fresh attempt from safe running Stone Hill.

Settings were configured before connecting. Native disconnect auto-resumed at10%;
Root visibly paused again and quit with Save State For Resume unchecked. No media
capture was made, and no further post-disconnect gameplay outcome is asserted.
After-test settings were retained; both fresh original INIs were restored through
apply_patch and byte comparisons passed. All nine protected full hashes passed,
including original ROM/CUE, original memory card, candidate companions and emulator.
The private copied memory card was never copied back. R03 editor16530 stayed open,
and its package and saved document were unchanged.

## Next bounded fix

Separate neutral placement intent from a source-derived spawn-clearance check.
The current fixed research scene explicitly validates endpoint geometry only;
it does not issue body/spawn safety. Preserve the unsafe P1 as a regression witness
and preserve the original native push-out response. Do not hide the problem by
disabling the Wizard, clearing player movement, or reducing its collision radius.

A corrected research placement still needs a separately issued candidate and
actual idle, navigation, enemy defeat/reward and re-entry tests. Broadphase radius,
one animation frame or inventory coverage is not whole-animation/route clearance.
No corrected candidate or new R03 control is claimed by this report.

# Fresh Wizard P1/N32 R01 — native initialization, failed playability gate

September7,2026. This exact research ROM was cold-booted in the independently
identified local DuckStation, with a private memory card and temporary settings.
It is not a V6 release, R03 GUI integration or universal Moby approval.

## Outcome

Native ID65/LAB loaded the new scene, placed Wizard, Sparx and a Wizard projectile.
Four paused samples passed all fixed native-layout/code/reread gates, with32-slot
capacity, correct resource roots and no sampled renderer exhaustion. Native
inventory displayed LAB0/10, confirming the intended treasure target.

**The scene did not pass playable-entry testing.** Spyro was already just outside
the small floor in the first accepted sample, then fell and lost a life4→3.
After reload he was airborne again. Testing stopped at the native inventory;
Wizard defeat, reward collection and re-entry were not exercised.

|Sample|Observed player state|Meaning and limit|
|---|---|---|
|1, first fade-in|9144,9521,9872; state6/timer4; lives4|Y49 beyond floor maximum9472. Wizard11B and bolt26 active; this is not the exact spawn-write instant.|
|2|9034,10676,10527; state6/timer24|Northward/upward airborne motion; not proof of uninterrupted history or a hit.|
|3|8924,11531,10282; state6/timer44|Still outside authored floor.|
|4|8715,12359,984; state6/timer128; lives3|Native fall/death observed.|
|5, closing inventory|10988,8969,3750; state6/timer88; lives3|After reload, still airborne. Layout payload correctly refuses game_state3; no normal gameplay approval.|

Native electric damage directly selects state7/animation8, but later recovery
can select state6. Ordinary airborne paths can also select6. The captured fields
omit early health, damage flags and motion vectors; they cannot distinguish
entry motion/input, collision or an earlier hit. Do not disable a behavior or
declare a floor defect solely from these late samples.
Exact original-source diagnosis:
`_local/research/dark-wizard-native-closure-20260906/fresh-p1-entry-state6-r01.SyKoZk/NOTE.md`.

## Exact artifact and evidence

Candidate directory:
`_local/research/dark-wizard-native-closure-20260906/fresh-wizard-publication-validation-r01.0eF4Tz/candidate-r01/`.
CUE basename `native-id65-fresh-wizard-p1-n32-r01.cue`;
BIN SHA256 `c4a70f826b1a9a3d4ad8e36512be354b6205d27ea2c7a314776a51067f35b5a6`.
Independent full publication/readback passed before this run; fault-test children
were never launched. See [materializer](native-fresh-wizard-research-materializer.md).

Runtime directory:
`_local/native-fresh-wizard-p1-runtime-20260907-r01.aaM1NY/`.
Its canonical CLOSED journal owns exact settings, macros, PIDs, attempts and
restoration receipts. Actual m-only session03 source hash was
`8bdbe4a5dc3b804501d739929ee86a70b17a5ef5c4ac8526c22a971a758cd9af`;
observer log hash `974d51372ad63d02b088fbdd4f07fae9f543f23414dd9d943495722daae641bf`.
Two initial attempts safely refused before connected/sample output because
native socket acceptance had not yet produced a process-owned descriptor.
No ownership check, timeout or protocol limit was weakened to connect.

Recording `videos/fresh-wizard-p1-r01-native-session.mp4` is95.179002seconds,
45402393bytes,5693 H264640x480 frames plus AAC44100. Full strict decode passed.
SHA256 `af9ed57b8334cad55075ad5c5b312737d8e2b08cf6b74c159338579f856e1e11`.
Audio presence is not listening acceptance, native selector compatibility or
complete lifetime proof. Native screenshot and timestamped extracted frames
are retained separately.

## Closure and preservation

Capture stopped; observer q exited0 with all six final full-file and source
checks passed. DuckStation visibly auto-resumed at the safe in-game inventory
on last disconnect, then was paused through the UI and quit without a resume
state. Its advertised revision's source describes that conditional side effect;
the diagnostic warning was corrected tests-first only after live closure.
An in-game menu is not itself emulator pause.

Both original settings files were restored exactly. All nine protected hashes
passed: original settings/game settings/card/BIN/CUE, candidate three files,
and emulator executable. The private card was never copied back. All owned
emulator/observer sessions are closed; R03 PID16530 and its document are untouched.

Next: capture the early entry/health/motion transition in a separately reviewed
diagnostic run; establish cause before changing native behavior. Neutral whole-
level document work can proceed separately without implying this runtime gate
has passed.

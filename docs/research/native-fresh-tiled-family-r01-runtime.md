# Exact tiled family R01 runtime witness — 2026-09-25

The exact four-tile, two-material, Wizard/Grass/key/chest candidate boots through
the full US firmware, runs in LAB65, reaches native20/20 treasure, and returns
to its authored spawn after an outer-edge fall. This is one bounded recipe and
input history, **not** every Moby, texture, level shape or RNG history.

## Identity and preserved evidence

Repository-relative evidence directory:
`_local/native-v6-tiled-family-runtime-20260925-r01.wqwkPe/`.
Exact CUE: `candidate/fresh-tiled-family-p9-experiment-r01.cue`.

- BIN661547040 bytes SHA256 `689fcf296e26d635d9c5ce6fc3fc687c2b47decce10ade2a0903f0c6bd877192`.
- CUE99 bytes SHA256 `88d8bb7a8c4ebf8e608903b29867610e1ea6e1dd8251d0de23223a2c38355ca9`.
- Canonical document SHA256 `0dade817ab4f9220e5aa3a74e1631e4689bad30ee12e91a943ceb0f6343e8acb`.
- DuckStation0.1-11826-gfe2306b1f installed executable SHA256 `34c62fd34b2a18670007c9383cea0b0222b2ab241f643bdbc5d1620a298ac870`.
- US524288-byte BIOS SHA256 `71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3`.

Writer634 used the isolated6f69fbf compiler, not a later rebuilt DLL. Actual
full output readback and inverse to Original passed before CUE publication;
1297 changed sectors. Post-test originals/candidate hashes are unchanged.
Broad638 at8f5a2e6 passed345 commands, zero failures. Source details remain in
[the resource plan](native-fresh-tiled-family-resource-plan.md).

Session01 PID9477 opened the exact candidate BIN (lsof verified) and only a
loopback GDB listener at127.0.0.1:2345. Full licensed PlayStation firmware screen
was directly seen and captured, followed by normal copied-memory-card load and
native controller LAB entry. No HLE, resume/foreign state, cheat, RAM/register
write, or programmatic execution steering was used. Controller macros were UI
toggle holds:1–4 directions,5 flame,6 charge,7 jump,8 Select; Space pause, J frame
step, F10 screenshot. Normal-speed intervals and small bounded step batches
were observed, not inferred from input requests.

## Runtime observations

`observations/observer01.jsonl` and `observer02.jsonl` contain26 samples, all
bounded and gate-reread-consistent. Labels are not a continuous frame trace;
memory reads are sequential, not atomic. Complete consoles are retained.

|Sample|Observed result|
|---|---|
|00|LAB65, authored spawn, distinct textured four-tile platform, Wizard/Grass/key/chest.0/20, global58. Health2/lives4 already reflects landing-interval damage.|
|01–03|Y seam9984 crossed on floor:8781/9642/9064 to9330/10978/9064. Wizard attacks and later reduces health.|
|06 (failed)|Large frame-step batch exceeded the observer32-event safety cap. No sample accepted. Disconnect resumed emulation; three lives lost during an unsampled interval.|
|07|Same process reconnected, health1/lives1; key global1 and key substate2. Key pickup occurred in the unsampled interval, so its precise animation is not certified.|
|08–10|Directly observed flame defeats Wizard; native class86 gem appears. Actual slot35 rises0->10, global58->68.|
|11–17|Chest solid boundary navigated; jump beyond south edge recovers onto the floor. At10495/9003/9063, chest opening substate2/private timer8 and key animation visibly run.|
|18|Chest and five D controllers retired, key global3, actual19/20, global77. One red class83 gem remains on the floor at10497/10590/8704.|
|19–20|Walk toward that gem across Y seam, then native collection. Actual20/20, global78, only Sparx remains in sampled dynamic pool; renderer exhaustion0. Native inventory visibly reads LAB20/20.|
|21|Deliberate north-edge fall at9828/12601/8637, beyond maximumY11520; treasure20/78.|
|22–24|Native reset restores8960/8960, health3, lives1->0; lands atZ9064/state0. Treasure remains20/78 and key3. Chest/controllers stay retired, Wizard returns with reward selectorFF.|
|25–26|Post-reset controller movement crosses X seam:9104/9175/9063 to10292/9292/9063. Wizard damage reduces health3->2; treasure stays20.|

Saved screenshots actually reopened for visual QA:

- `screenshots/Spyro the Dragon 2026-09-25-08-07-30.png`: full firmware licensing.
- `screenshots/Spyro the Dragon 2026-09-25-08-26-03.png`: native LAB20/20 inventory.
- `screenshots/Spyro the Dragon 2026-09-25-08-29-41.png`: post-reset20, live Wizard attack.

The brown sky replaces the retained Town Square backdrop for this candidate.
Grass is visually present; arbitrary passive behavior or sound fidelity was
not exhaustively assessed. The last gem was reachable, but one successful
trajectory is not a universal chest landing envelope. No second Wizard kill
was completed after reset; its no-reward selector is an observation, not a
full duplicate-reward stress test. Native retail inventory still places LAB
under Gnasty's World; this does not add a new world or Japanese egg inventory.

## Shutdown and next gate

Quit normally with Save State For Resume unchecked; emulator/observer exited0.
After-test INIs retained, both fresh original INIs restored with byte comparisons
and exact hashes. Ordinary card unchanged at
`d69b43f8be2585f7b5e7fd1b07f85217ba74c01f102c01d7a22928ee0e9e8ae2`.
The private copied card is never copied back. Private savestate folder is empty.
`final-preservation-sha256.txt` records all final checks.

This fixed-candidate success does not alter compiler RuntimeVerified,
PublicationApproved, dependency-closure or normal Create BIN flags. The next
gate is the actual saved-document Candidate/publication transaction and UI
integration with exact canonical identity, source leases, stale/ABA refusal,
fault rollback and whole-image inverse tests. Preserved R06/R07 apps remain
unchanged; no new packaged editor is claimed by this report.

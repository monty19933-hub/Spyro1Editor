# Edited fresh LAB R01 — native actions and checkpoint

September 6, 2026. The exact edited landing candidate, not the earlier starter
and not generic UI export, was loaded in DuckStation. Session is **closed**;
all nine protected original/candidate/application identities passed afterward.

## Exact candidate and entry

Directory: `_local/research/native-id65-fresh-edited-document-20260906-r01/candidate-r01/`.
Basename: `native-id65-fresh-edited-landing-track02-r01`.

- BIN: `ecc8f61cc16bf86a2f5117b327c49aede18f5c1e6a602c0e67f2ba58565aed16`.
- CUE: `1fcb9bade2f3c9c59b6e6d7b57bd8a2bd0773534d151374d62fcb2ce442e663c`.
- Receipt: `3b545c4dd0e2102586e9f520dec96fda3ea8e84a088003d5b7a7a5ef701373d7`.
- Plan: `fba3e61a3f93b9dff27d9a5304eb746b7c2ef9aa95a71cfeda6adaee62b571c2`.

Authored floor XY8192..10240, Z8704; spawn9216,9216,9216, heading96;
floorRGB72/132/120, sky44/66/100, music2, zero authored Mobys and N16.
The whole image and companions were separately verified before native testing.
PID92969's open file identified this exact BIN; observer annotations/RAM gates
alone were never treated as disc authentication.

Cold boot loaded a private copy of the user's two-dragon Stone Hill save.
The game's native inventory diagnostic selector entered ID65. No RAM writes,
save states, Free Flight or cheats were used. This is not ordinary portal entry.
Native CPU remained1:1/2MiB, 1x Vulkan/PGXP off. Temporary playback pacing is
separate from CPU clock settings.

## Observed sequence

| Recording / sample | Actual result |
|---|---|
|3:34 / first valid LAB sample|Isolated green floor and blue sky, Spyro/Sparx, no Town Square terrain. XYZ9216,9216,9061 and heading96. Settled Z reflects floor contact, not the original airborne spawn Z.|
|3:38 / inventory|Native inventory identifies LAB, 0/0 level treasure.|
|3:47–3:55 / overshoot and respawn|An attempted accessibility speed edit did not persist. Walking at100% left the tiny floor; sample12421,5256,3094/state6. With no held controls, native death/reload returned to9216,9216,9061 heading96; lives4→3. No collision defect is inferred from leaving the bounds.|
|Verified10% playback / bounded walk|Keyboard edit persisted EmulationSpeed0.1, CPU unchanged. Short Down pulses reached9280,9181,9061 inside the floor.|
|3:56 / flame|Native Circle input produced visible flame; sample9397,9064,9063, six00/kind2 particles plus31 companion42 particles.|
|3:57–3:58 / jump and land|Native Cross input led to state5, Z9164 then9483, and landed Z9060/state0 at unchanged XY9397,9064. Lives remained3.|
|3:58–4:32 / normal-speed idle|Playback restored and verified100%. Approximately34 recorded native seconds after actions, with bounded UI pauses. Same settled position/lives and one companion; no sampled renderer exhaustion.|

The observer used the fixed `edited-landing-track02-r01` profile. Every accepted
sample passed its payload gates and rereads; reads are sequential, not atomic.
The first connection refused after queued pause notifications exceeded its
32-event bound; it produced no accepted sample. Root reidentified the same
process/open image and reconnected, without weakening the bound. The first
pipeline's tee exit0 was not observer success; both transcripts are retained.

Direct key attempts were not assumed successful merely because input was sent.
A temporary action preset was created, but the native Apply dialog reused its
cached original assignments. The observed jump came from the earlier direct
Cross input. No new Cross macro is claimed. The temporary preset was moved into
the session evidence after exit; original global/game settings were restored.

## Evidence and restoration

Session: `_local/native-fresh-edited-runtime-20260906-r01.WRBwJW/`.
See its closed `RUNTIME-SESSION.md`, `observations-reconnected.log` and
`post-restoration-identity.log`. Native screenshots are in `screenshots/`:
16:14:24 landing,16:16:01 LAB inventory,16:31:11 flame,16:35:00 airborne,
16:39:27 final idle (all September6).

Video `videos/fresh-edited-r01-native-selector-actions.mp4`:148988610 bytes,
272.230998 seconds,16284 H264 frames640x480, stereo AAC44100.
SHA256 `5ee3fefa7276b8429963684f5b4ede219520356d5f6a311f19e422a5ca97a016`.
Full strict video/audio decode passed with empty error output. Successful
decoding is not auditory acceptance of track2 or native effects.

Capture stopped, observer exited0, emulator exited0 without resume state.
Original INIs compare byte-for-byte; original BIN/CUE/card, exact candidate's
three files and emulator executable all match pre-session hashes. The installed
editor and saved editor projects were not changed by this runtime session.

Still open: ordinary portal entry, arbitrary geometry/textures/foreign actors,
card persistence, complete audio/presentation, native peak budgets and generic
saved-document UI publication. This narrow test does not certify those gates.

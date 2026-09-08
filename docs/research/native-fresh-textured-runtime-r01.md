# Textured blank-level native test — R01

September 8, 2026. CLOSED; DuckStation exited0 and original settings restored.

The exact fixed-a textured candidate cold-booted through the original game,
loaded a private copy of the existing memory card, and reached the authored floor
using the native inventory LAB sequence. Two imported static materials were
visible: Dark Hollow WAD14/texture0 and Gnasty WAD70/texture0, on a1024×1024 floor
against the authored brown sky. No Town Square scenery appeared in these views.

A private eight-macro controller preset was applied through DuckStation's native
Controller Presets UI after entry. Current on-disk Pad1 bindings were inspected;
all hotkeys remained semantically unchanged. This established usable paired held
input without editing a running INI, debugger writes or another input mechanism.

At normal100% emulation speed, a paired forward pulse visibly moved Spyro off the
small floor into an edge fall. Subsequent native execution returned Spyro/Sparx
to the textured floor. Stationary jump rose above the floor and landed; flame
produced visible fire/smoke effects. Both materials remained visible afterward.
An earlier eight-frame-step attempt showed animation but by itself did not prove
movement. No precise coordinates, health/life count or continuous event tracing
are claimed: this session deliberately used no RAM observer.

These results apply only to this exact research candidate. They do not certify
HQ/close-distance rendering, animated textures, all source records/destinations,
texture painting, mixed Wizard/material exports, audio, resource peaks, ordinary
portal integration, reboot persistence or general release readiness. The test
floor is intentionally tiny and easy to walk off. The immutable receipt's broader
runtime/normal-export/dependency approval flags were not changed by a playtest.

## Exact candidate and test evidence

Repository: `/Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release`.
Candidate directory: `_local/research/textured-publication-next-r01.gsLsn2/candidate-r01`.
Stem: `native-id65-fresh-textured-fixed-a-track19-r01`.

BIN661547040 bytes;1253 changed raw sectors; SHA256:
`998a47930c5f20e36cf87d41bfb9ec3cab237d8d42c443ed706bd50c692611a3`.
CUE109 bytes/SHA`ef08d3e5d1b3b65e2051d2087a582815a4af01d8d459dc12b3228ae5c7928df2`.
Receipt43349 bytes/SHA`0362a2f5a1a2ee3afca00e342d3c80cd7c88089eb7e40271e5ef8db4b4739f48`.

Physical publication/fault gates and new-process full readback passed before
launch; see [publication report](native-fresh-textured-candidate.md).
Independent peer checks bound PID47133/UID501/start12:58:03, exact executable and
CUE command, read-only actual candidate BIN fd15 and all11 held file owners.
Both brackets had no debugger listener. These are OS/disk checks, not RAM proof.

Session: `_local/native-fresh-textured-runtime-20260908-r01.8HKykz`.
Its journal, `PEER-OS-FILE-BASELINE.md`, settings backups, and full pre/post hashes
are retained. Native F10 captures in its `screenshots` directory:

- `Spyro the Dragon 2026-09-08-13-02-18.png`: entry/materials.
- `Spyro the Dragon 2026-09-08-13-05-35.png`: edge fall.
- `Spyro the Dragon 2026-09-08-13-06-21.png`: returned floor/Spyro/Sparx.
- `Spyro the Dragon 2026-09-08-13-07-00.png`: landing.
- `Spyro the Dragon 2026-09-08-13-08-14.png`: jump above the floor.
- `Spyro the Dragon 2026-09-08-13-09-13.png`: flame/materials; SHA256
  `7c87bdf0fbd924bdba88174c6a12a48ffd1f8b268138a1708ee7e4f557d6a89a`.

## Closure and preservation

Native Quit confirmed Save State For Resume unchecked. The UI call timed out as
the app exited, but owned exec57367 closed0 and exact process search was empty.
Both original INIs were restored byte-for-byte only after exit; their copies and
after-test INIs remain in this session. Root's full nine-file pre/post hash logs
are byte-identical: original BIN/CUE/card, candidate3, DuckStation executable,
and global/game settings. No original card copy-back, savestate or video occurred.
No preserved editor app was rebuilt or modified for this test.

# Toasty native Free Flight R02: actual gameplay observations

2026-09-06. This records a bounded, observed gameplay session, not approval of
all destinations, ordinary Create BIN, the editor checkbox, or a release. The
immutable candidate receipt retains its original false runtime/publication flags;
this later evidence does not rewrite that publication-time receipt.

## Exact candidate and session

Candidate directory:
`_local/research/native-free-flight-research-20260906-r02/candidate-r02/`.

| Artifact | SHA-256 |
| --- | --- |
| `toasty-native-free-flight-fast-entry-r02.bin` | `4bde125d6d64c910293b17163b638648c5661b8be5abb3568513446a0888f74a` |
| `toasty-native-free-flight-fast-entry-r02.cue` | `63a490dc0962918dc8cd8a1a1c565d50e3c2e86f5c454231f5e74543197cf7a3` |
| `native-free-flight.receipt.json` | `0eb2b16941dc9d53bc5a56dad254e60b17641c7aed5b1b6b7ef3749aa73bff7b` |

The separate source-bound v2 profile combines the unchanged eleven Toasty-only
flight windows with the existing eight-byte native test-selector patch. It does
not change R01. Explicit Release publication passed88 checks; a separate exact
reopen verification passed88 before launch. Post-game hashes match the table.
See `native-free-flight-fast-entry-research.md` for the mutation boundary.

Retained evidence root:
`_local/overnight-native-20260906-r02.jPIcTb/`.

- `videos/free-flight-r02-fast-entry-native-session-01.mp4`:407.278005 seconds,
  275626423 bytes, H.264640x480 plus AAC44.1kHz stereo, SHA-256
  `19edad2307a1a45142029d0e6ec17eab82eab96c7ff4ca6d5c274464e931cb99`.
- `native-flight-r02-observations.log`: fixed-address, read-only native samples.
- `read-flight.py`: the exact observer; it sends only GDB memory-read packets.
- `duckstation-flight-r02-rosetta.log`: the identified test process log.
- `flight-fast-entry-publication.log`, `flight-fast-entry-independent-reopen.log`:
  separate structural verification, not gameplay certificates.
- `flight-r02-full-media-decode.log`: full ffmpeg decode completed with exit0
  and no errors. Media decoding is not subjective audio-quality acceptance.
- `screenshots/Spyro the Dragon 2026-09-06-03-26-44.png`: native F10 screenshot
  of the final stable landing interval, not a generated illustration.

DuckStation PID36547 was launched under Rosetta with this exact CUE. Its open
BIN and read-only GDB endpoint were checked. The session cold-booted and used a
private copy of the normal memory card; no save state was loaded or created,
no RAM/register/PC writes or breakpoints were used, and cheats were disabled.
Native inputs were sent through the emulator UI. All temporary macro inputs
were released within each input batch; emulation was paused for inspections.
The observer's initial connection pauses emulation; resumption was UI-only.

## What was actually observed

Native numeric player states in this table are decimal:32 is flight0x20,
33 is flap0x21,16 is the falling state, and0 is idle. XYZ uses native world units.
Samples are sequential reads while paused, not an atomic hardware trace.

| Check | Concrete observation |
| --- | --- |
| Unrelated normal level | Cold save loaded Stone Hill11, permission0, normal movement; no flight claimed there. |
| First test entry | Native Inventory sequence selected Toasty14; spawn near`129751,114657,16200`, permission1. This first entry used the diagnostic selector. |
| Actual flight | An airborne Cross transitioned to state32; the imported native flight animation rendered. |
| Ascent beyond retail ceiling | Down changed pitch to613; Z17838 with takeoff height17034, a rise804 above the native512 restriction, still state32. Subsequent Z18440 and18644 were also observed. |
| Descent | Up changed pitch negative; Z18644→18419→18042 while state32 and lives4 remained unchanged. |
| Both turn directions | Left changed yaw171→200; Right changed201→181. XYZ changed during native forward flight. |
| Cross flap | Re-entered after collision; Cross produced state33 with the flap animation visible. |
| Collision response | Recorded native impact/stars followed by a ground-state transition at`124422,112044,16410`; lives remained4. This is a bounded collision observation, not every collision surface or speed. |
| Triangle exit | A frame-visible Triangle pulse produced state16 and decreasing Z. |
| Death and respawn | Falling outside the world consumed one life4→3 and returned to Toasty's spawn with permission1. A fresh jump/Cross entered state32 again. |
| Normal exit isolation | The game's ordinary Exit Level action returned to Artisans10, permission0, with the existing life count2. No test selector was used for this exit. |
| Unrelated rescue | Argus's normal rescue/dialogue sequence ran in Artisans, game state8 and flight permission0. |
| Normal portal reload | Walked/jumped into the actual Toasty entrance; the recorded “Confronting Toasty…” transition loaded level14, permission1. The test selector was not invoked for this reload. |
| Flight after portal reload | Fresh jump and airborne Cross again produced state32 at`129742,114699,17486`. |
| Controlled floor landing | Settled-flight Triangle produced state16 at`128498,112521,16973`, then state0 at the same XY and Z16202, speed0, lives2. Another no-input interval kept identical XYZ/speed/lives and state0 (timer6→26). |

The practical controls observed are native forward flight: jump, press Cross
again while airborne, Down to pull up, Up to descend, Left/Right to turn, Cross
to flap, Triangle to leave flight. This is not hover or noclip, and flying out
of the playable world still kills Spyro.

## Failed attempts and limits are retained

Brief direct-key Triangle attempts were not reliably consumed. The test switched
to a temporary frame-visible Triangle macro. An early pulse immediately after
flight entry also left state32; a later pulse from settled flight produced16.
Do not promise immediate exit at every transition frame without a separate
input-timing proof or usability test.

The earlier sample named `r02-drop-courtyard-result` is **not a landing pass**:
it records another death/respawn, lives3→2, after an unsuccessful early drop
attempt and further unpaused movement. The later short-step samples, with actual
state16→0 and unchanged lives2, are the controlled landing evidence. Neither
the earlier misleading label nor the initial input attempt is silently removed.

The normal entrance needed a jump across its threshold. Earlier walking-only
attempts are not evidence that the portal was broken. The preserved R01 note
that entry had not been achieved describes that earlier session, not this later
successful R02 portal traversal.

This session does not cover every flight state/animation, controller type,
collision speed, death cause, level, texture/audio variant, or long-term save.
Native audio is present in the recording; no subjective sound-quality pass is
claimed. The actual editor checkbox and enabled→disabled generated-output flow
are still unimplemented/unverified. Existing reversible byte-producer and
settings regression tests are separate evidence, not a substitute for that UI
and end-to-end disabled-output gate. No native-enemy transplant was tested here.

## Exact restoration and preservation

Capture was stopped normally, its MP4 finalized and fully decoded. The observer
closed, and DuckStation exited normally with Save State For Resume unchecked.
The UI call timed out as the process closed; process absence and the launch
session's exit0 independently confirmed completion before restoring settings.

Both original configuration files were restored from the exact retained backups,
then checked with byte comparison and SHA-256:

- Main settings:`cd3bfb693556cc08c81b69f56199b61068ac7b483f3d5dae22e2a3931ca55721`.
- SCUS game settings:`629d9bd3eb9890eedb2bfd9ae2458f7f214272f951a9081f687abe8e5f11d1f3`.

This restores the user's original configuration, including their preexisting
Moon Jump setting, rather than leaving the temporary cheat-disabled test profile.
`emulator-settings/settings.ini.after-r02` and `SCUS-94228.ini.after-r02` retain
the final temporary settings. The two owned test input presets were moved out of
the live input-profile directory into `emulator-settings/`, recoverably. The
navigation preset's final macro7 is Triangle; its original R1 version is retained
separately as `navigation-test-preset.ini`. The live preset directory and isolated
test-savestate directory are empty.

The user's original memory card remains SHA-256
`d69b43f8be2585f7b5e7fd1b07f85217ba74c01f102c01d7a22928ee0e9e8ae2`.
The original ROM and CUE were rehashed after gameplay and remain respectively
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37` and
`05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766`.
The installed editor was not rebuilt, replaced or launched during this session.

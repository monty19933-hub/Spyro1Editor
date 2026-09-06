# Imported Wizard R03 — native reward and collected re-entry

Observed September6,2026 with the unchanged exact one-Wizard research ROM.
This closes a useful bounded gameplay sequence, not universal Moby portability,
all-state native closure, or a new release. John authorized emulator operation.

## Exact input and session

Candidate: `_local/research/dark-wizard-native-closure-20260906/candidate-r01/dark-hollow-native-wizard-r01.cue`.
BIN SHA256 `2a87dc12f9398ef873267e0f17792cfe160952c9f1bf12c137f64029863a39db`;
CUE `532be677b7d3aaa575f0c93af53a62630b5b5c9cacaf14db156330291530a414`;
receipt `dd7d490cdfa5cdf60d92e110b1946d4d296277563b77d360e4d6513821a0d32a`.
The actual PID81763 open BIN and loopback-only GDB listener were checked before
connecting the read-only observer. Profile annotations are not authentication.

Evidence root: `_local/native-wizard-runtime-20260906-r03.7xUAjK/`.
Cold boot used a private copy of the original two-dragon card, native menus,
Artisans and the original Dark Hollow portal. No savestate, RAM/register write,
cheat, level selector, Free Flight, original-save edit, or ROM change was used.
Native CPU1:1,2MiB RAM, native1x Vulkan rendering, PGXPoff were retained. Playback
pacing was temporarily50% for the successful first encounter and reset to100%
before exit/re-entry; this is not a CPU-frequency change or measured peak-FPS proof.
Macro toggles were released within each bounded UI call.

## Actually observed

| Recording time | Visible result and separate read-only sample |
| --- | --- |
| 13:31–13:46 | First approach missed the Wizard and struck the wall. A recovery interval left running across tool calls ended in native death/reload, four lives to three. Exact final hit sequence is not asserted. Later recovery intervals were bounded and ended paused. |
| 14:20–14:25 | Normal charge defeated row128/class11B. State2 and dynamic class56 accompanied the visible yellow gem. Lower-room treasure was13 before combat and settled at23 afterward; Wizard becameFD and class56 was no longer active. |
| 14:26–15:03 | Native Exit Level returned to Artisans; walking through the original portal re-entered Dark Hollow. HUD23 remained. Row128 reloaded at92832,25248,10445/state0 with reward selectorFF, formerly56. |
| 15:55–15:58 | A second normal charge defeated the reloaded Wizard. State2/selectorFF produced class0F rather than56; settled stateFD left only the companion active. The native menu visibly retained23 treasure, two dragons and three lives. |

The last case demonstrates the actual tested already-collected response, not
that selectorFF can never spawn anything. Original armored guards and their
rewards are distinguished from the imported Wizard throughout the journal.
Navigation bumps and an unsuccessful short platform jump are not diagnosed as
import regressions. Sequential resource samples do not measure peak demand.

## Retained recording and verification

`videos/wizard-r03-native-combat-session.mp4`:632315303 bytes,958.240998 seconds,
57319 H264 frames and stereo AAC44.1kHz. SHA256:
`298437652877e63041f340dde498425c56cfef90840b88a02ea1f81d7b30f00f`.
Full strict video/audio decoding exited0 with no errors. This is media integrity,
not listening acceptance or complete native sound/music verification.

Root extracted and visually reviewed these files under `screenshots/`:

- `wizard-first-defeat-14m21.png`: visible native yellow reward beside Spyro.
- `wizard-reward-hud23-14m26.png`: complete first post-collection menu/HUD23.
- `native-dark-reentry-15m03.png`: native Dark Hollow starting platform.
- `final-captured-frame-15m58.png`: complete second post-defeat menu/HUD23.

The retained957.9s extraction with `retained-hud23` in its filename shows only
aftermath/partial HUD. Its intended filename is not evidence of a complete HUD;
the final958.19s extraction above supplies that evidence. Full originals remain.
`observations.log`, `RUNTIME-SESSION.md`, media metadata and decode/hash logs are
retained beside the engineering gate reports. Every observer runtime-pass flag
remains false; this report supplies the narrower directly observed milestones.

## Cleanup and outstanding gates

Capture stopped normally. Observer77624 exited0; emulator shell8374 exited0,
PID81763 disappeared, and Save State For Resume was unchecked. After-test global
and per-game INIs were retained. Fresh original INIs were restored byte-for-byte;
`post-restoration-identity.log` confirms all nine pre-session hashes: both INIs,
original memory card, original BIN/CUE, candidate BIN/CUE/receipt and executable.
The installed editor was not replaced. No second candidate was loaded here.

Still open: death during partial collection, disk-card persistence of the new
reward, both simultaneous Wizards and both collection orders, complete audio/
presentation, sustained allocator/particle/render demand, other destinations,
and imported actors in the genuinely fresh scene. New two-Wizard and edited-
fresh research ROMs are separately published/read back, not accepted by R03.

# Corrected fresh Wizard: normal-speed entry (R03)

September 8, 2026. Closed test session; original settings restored.

The same corrected candidate from R02 was cold-booted at normal emulation speed.
The native load-game/inventory entry sequence reached level 65 and the authored
floor, with the Wizard active. Two accepted paused observations recorded Spyro at
8960/8960/9064, inside the floor, and health decreasing from 1 to 0. The second
observation accepted the independently guarded absent-Sparx case.

This establishes bounded normal-speed entry and enemy activity, not satisfactory
movement control, enemy defeat, rewards, audio, portal reentry or universal Moby
portability. Short keyboard taps did not visibly move Spyro. The observer was
closed before controller-settings inspection, and no reliable new gameplay
binding was established. No debug writes, savestates or source edits were used.

## Exact evidence

Private session (relative to this repository):
`_local/native-fresh-wizard-separated-runtime-20260908-r03.fydNL7`.

Candidate stem: `native-id65-fresh-wizard-separated-p1-n32-r01` in
`_local/research/dark-wizard-native-closure-20260906/separated-publication-gates-20260908-r01.PsfP2X/04-separated-candidate`.
BIN SHA256: `e57fedebb59ffc9020de00562baccbc5a6e09bffd5f24f665d34d5f03163cbdb`.

`observer01.jsonl` SHA256:
`f7229047e0e54c5afbdfd778b10f7440a8f29290d07ff05f2b28a33d191c0508`.
It contains connection, two accepted paused samples and clean session close.
Independent peer OS/file checks preceded Root's observer connection. Native UI
frames were viewed during the session; no new PNG evidence file is claimed.

DuckStation PID39832 and observer are no longer running; both processes exited0.
Quit was confirmed without saving a resume state. Both original settings INIs
were restored byte-for-byte after quitting. All nine protected whole-file hashes
matched before/after: original BIN, CUE and memory card, three candidate files,
DuckStation executable and the two settings INIs. The private test card was not
copied back. Original/test settings and final verification remain in the session.

Next gameplay attempt needs reliable held keyboard input configured before its
own cold boot. This closed session is not to be resumed or relabelled as a kill
or reward pass.

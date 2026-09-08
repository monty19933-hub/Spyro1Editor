# Corrected fresh Wizard: held gameplay and rewards (R04)

September 8, 2026. CLOSED; DuckStation exited0 and original settings restored.

The already published and independently verified separated B/P1/N32 candidate
cold-booted through the original game and a private copy of the existing card.
The native inventory LAB sequence reached the authored 2048×2048 floor, Spyro,
Sparx and the attacking Wizard. All gameplay ran at normal100% emulation speed,
with CPU1:1/2MiB, cheats and achievements off, Vulkan1x/PGXP off. No debugger,
RAM observer, savestate, resume save or running-INI modification was used.

After pausing, the reviewed held-gameplay preset was applied through native
Controller Presets. Use Profile Hotkeys/Input Sources remained unchecked;
actual Pad1 bindings and unchanged semantic hotkeys were read back. All macro
toggles had matching release presses. One initial unsupported key spelling
was refused before input; the documented lowercase space key worked.

## Observed native sequence

- Flame defeated the Wizard, with death animation and a visible yellow gem.
- Paired directional input moved Spyro toward the gem. A second movement pulse
  overshot the small floor; the gem followed and was collected during the fall.
- The native death/respawn sequence returned Spyro and the Wizard to the floor.
  Inventory then showed LAB10/10 and total68, versus total58 before entering LAB.
  An earlier partial HUD animation looked like1; the completed inventory is the
  basis for the10-gem result, not that transient digit.
- Flame defeated the respawned Wizard again. This time a purple/white repeat-kill
  orb appeared, not another yellow gem. One directional pulse approached it;
  the orb disappeared and its native HUD indicator appeared.
- A second inventory check still showed LAB10/10 and total68. No duplicate gem
  award was observed after this repeat defeat and pickup.

These are bounded visual gameplay observations for this exact candidate.
They are not RAM ownership/capacity proofs, audio verification, ordinary portal
reentry, reboot/save persistence, arbitrary Wizard recipes or all-Moby support.
The immutable receipt's pending flags remain unchanged. This test resolves the
earlier held-input/defeat/reward gap without relabelling closed R02/R03 evidence.

## Exact evidence and preservation

Session: `_local/native-fresh-wizard-held-runtime-20260908-r04.pnCsce`.
Candidate directory:
`_local/research/dark-wizard-native-closure-20260906/separated-publication-gates-20260908-r01.PsfP2X/04-separated-candidate`.
Stem: `native-id65-fresh-wizard-separated-p1-n32-r01`.
BIN661547040 bytes/SHA256:
`e57fedebb59ffc9020de00562baccbc5a6e09bffd5f24f665d34d5f03163cbdb`.
CUE108 bytes/SHA:
`572af87a5fef9497fa32cab6588cdc87f61304c430dfc5e58c8dd6aae070c556`.
Receipt5875 bytes/SHA:
`7c590c9d1454b9f22b51c643084a6f49220a78c49cc2e657983d9030d1a1932d`.

Root checked exact PID50221/UID501/start13:30:34/executable/CUE command, actual
read-only BIN fd14/inode44374880, mapped executable inode41889985, and no TCP
listener. Existing outbound HTTPS was visible; no debugger connection occurred.
These are OS/disk observations, not independent effective-setting or RAM proof.

Eight native F10 captures remain in the session's `screenshots` directory.
Key captures (September8 timestamps):

- 13:33:52 entry/attacking Wizard.
- 13:34:44 first flame/death;13:34:54 yellow reward.
- 13:35:17 approach;13:36:31 respawned Wizard.
- 13:36:58 first inventory10/10,total68.
- 13:37:19 second defeat/purple orb.
- 13:38:21 final inventory10/10,total68; SHA256
 `f33978063fda97dae95fa1a2f07fb1567d5ef27f345e19fa95a8596a28368518`.
The final on-disk PNG was independently opened by Root after closure.

Native Quit confirmed Save State For Resume unchecked. The accessibility call
timed out while the app exited, but owned exec48620 closed0 and exact process
search was empty. Both fresh original INIs were restored/cmp-verified afterward.
Full nine-file pre/post SHA logs are byte-identical: original BIN/CUE/card,
candidate3, DuckStation executable and both INIs. Final check exec34085 closed0.
After-test INIs and the private card remain retained; the original card was never
copied back or replaced. Test savestate/video directories are empty.

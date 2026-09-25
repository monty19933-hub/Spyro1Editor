# Original Japanese sign reference — September25,2026

The user-supplied original SCPS-10083 CUE/BIN was slow-booted in installed
DuckStation0.1-11826-gfe2306b1f. The held image was independently identified with
`lsof`; its pinned source identity is in the regional source certificate.
PlayStation firmware/logo, Japanese title, private empty-card/new-game flow,
Artisans movement/jumping and native signT185's complete dialog were observed.

The separate test state used fresh memory cards and redirected settings/output
folders. No old save state, HLE/direct-EXE path, cheat, debugger RAM/register
write or controller injection was used. The only available installed firmware
was the user's SCPH-1001 BIOS (SHA256
`71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3`);
existing automatic region selection and disabled CD region check were retained.
This is not a Japanese BIOS compatibility certification.

Source-derived automatic sign activation was corroborated: after stationary
proximity/facing, game mode became17 and the native text pointer was80181F18.
Its bytes `89988A86208686B48298A799BDFF00` exactly match ArtisansT185's original
message. The dialog rendered kana and native dakuten, with its return prompt.
Runtime samples are sequential, not atomic snapshots; source/geometry invariants
are separate from these visible observations.

Local evidence:
`_local/japanese-sign-runtime-20260925-r01.6luDvV/`

- `RUNTIME-SESSION.md`, `observations*.jsonl`, `native-sign185-memory.json`.
- `screenshots/スパイロ・ザ・ドラゴン 2026-09-25-11-12-22.png` — full native dialog.
- `settings/settings.original.ini`, `settings/settings.after-original-session.ini`.

The emulator was closed normally without saving a resume state. The ordinary
global INI, SCUS per-game INI and USA card were rehashed and remain byte-exact
against their preserved baselines. R06/R07/R08 app packages were not changed.

The original-session evidence above is original-source behavior only. The
separate edited-message sessions below extend that narrow evidence, not USA
portability, new placements, GUI integration or egg accounting.

## Edited reference R01: retained failing candidate

`_local/japanese-sign-reference-candidate-20260925-r01/` retains the independently
verified 18-message Artisans edit, BIN SHA256
`6188d217fa972240dd3ac50333eaba83171fb3ca333ad306cd8972786b9bbab4`.
The private cold-boot session is
`_local/japanese-edited-sign-runtime-20260925-r01.CtNEtQ/`.
Sign181 displayed its first line correctly, but its final unterminated line
disappeared after reveal completed. This is a runtime failure, despite exact
physical-image preservation/readback. Screenshot: `11-38-30.png` in that
session's Japanese-titled screenshot folder. R01 was not overwritten.

The native dialog draw loop copies the trailing NUL into its line buffer and
exits without flushing the final line. A final FF newline is therefore required
for a complete dialog, not merely a NUL-terminated valid token stream. All330
original sign messages have this terminator. Raw codec fragments remain
lossless; complete private-data/edit-plan serialization now requires final FF.
`AsCompleteDialog()` appends it explicitly and counts it against the existing
length/line policy. A native-loop regression reproduces the disappearing tail.

## Edited reference R02: two independent complete dialogs and reread

New candidate, never a replacement for R01:
`_local/japanese-sign-reference-candidate-20260925-r02/`.

- BIN727,179,600 bytes, SHA256
  `c5e47f7ad22aaacf9fcb4c516c10415e873e57733b5e2a232ddaa43be40efb32`.
- CUE SHA256 `b163471549b4edf132e05265c2fd8d55d76494c2769ca7ead7de4a4df9004dce`.
- Receipt SHA256 `0f2b6be52f534ceb18dd18ecfb11ea284e4e83b5fe897307cea1f6c0f1d1001b`.
- Exactly18 existing sign messages contain their distinct instance number,
  `ガパ`, newline, color2 `カメラ`, and final newline. Only2,133 physical bytes
  differ, in raw sectors5574..5576, including independently recomputed EDC/ECC.
- Python independently reconstructs the entire expected physical image and
  checks the exact candidate and final named/held original source identities.

R02 runtime evidence:
`_local/japanese-edited-sign-runtime-20260925-r02.OX7AhT/`.
Installed DuckStation was launched with `-slowboot` and the exact new CUE,
full installed SCPH-1001 firmware, no HLE/direct EXE/resume state. `lsof`
identified the exact R02 BIN held by PID41539. This session's early screenshot
is black; it is not a new visual firmware-logo proof. Japanese title, new empty
private card/slot, Artisans, Nestor rescue and ordinary movement were observed.
The earlier original-reference session retains the visible firmware proof.

Normal UI controller input reached two independent signs:

|Sign|Native text pointer|Complete native message bytes|Visible evidence|
|---|---|---|---|
|179|80181D2C|31373920C5BDD9BCFF42C5E1E6FF|12-37-21.png|
|181|80181E04|31383120C5BDD9BCFF42C5E1E6FF|12-48-31.png|
|181 reread|80181E04|same as above|12-51-06.png|

Screenshot names are prefixed `スパイロ・ザ・ドラゴン 2026-09-25-` in the session's
`screenshots/` folder. Both completed dialogs retain gold `ガパ` with native
dakuten/handakuten and green `カメラ` on the second line, plus the return prompt.
Sign181 was dismissed; walking beyond its reset radius returned row state to0.
Returning reopened the same complete message in game mode17, row state2.
These are visible gameplay checks, corroborated by sequential read-only RSP
memory observations in `observations.jsonl`. No memory/register writes, cheats,
save-state resume or injected controller packets were used.

The emulator and read-only observer exited normally. Resume-state saving was
unchecked. Global settings were restored exactly (SHA256
`c07823d8de99c88b560fb5dc356fd1247aa391b88ff91284d7ef1354c10fb854`), and the normal
SCUS settings and USA card retained their baseline hashes. Final source/CUE and
candidate/receipt hashes are retained in `final-source-candidate-hashes.txt`.

This passes edited native messages in their original Japanese level, not sign
relocation/growth, duplicate placement, safe-maximum gameplay, USA font/runtime
installation, GUI Save/Reopen or universal placement. Those remain open gates.

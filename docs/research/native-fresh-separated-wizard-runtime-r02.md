# Corrected fresh Wizard — cold entry, damage and respawn

September 8, 2026. One exact research candidate, controlled 10% native emulation;
not universal Moby portability or a released editor capability.

The separately published candidate BIN SHA256 is
`e57fedebb59ffc9020de00562baccbc5a6e09bffd5f24f665d34d5f03163cbdb`.
It is the clean `04-separated-candidate/native-id65-fresh-wizard-separated-p1-n32-r01`
under `separated-publication-gates-20260908-r01.PsfP2X/`, not a sibling fault-test
output. See [publication contract](native-fresh-separated-wizard-publication.md).

## Observed sequence

Cold boot loaded Stone Hill from a private copy of the user's card, then entered
LAB/ID65 using native inventory controls. No savestate or debugger write was
used. The observer was fixed to this exact source/candidate/executable profile;
all external identity checks and final complete file hashes passed.

| Samples | Accepted observation |
| --- | --- |
| 15 | Game0, player-state0/timer2, exact authored spawn8960/8960/9216, both motion vectors zero. |
| 16 | Player-state6/timer0, exact spawn and zero motion, full health3. |
| 17 | State6/timer2, Z9207, vectors0/0/-288, unchanged X/Y. |
| 18 | Settled at8960/8960/9064, zero motion, health3/lives4. |
| 19–20 | Hands-off Wizard attacks; health2 then1, same floor position. |
| 21 | Health0, Sparx absent/null handle, fresh absence-arm validation passed. |
| 22–23 | Health-1, state30, life count4 then3; Sparx remains absent. |
| 24–25 | Health3 and Sparx restored, state0, lives3, XYZ8960/8960/9060 on the authored floor. |

These are sequential sampled endpoints, not an in-helper execution trace.
Sample16's label `first-gravity-update` described the intended check; its actual
data had zero motion. Earlier loading samples0–14 were expected gate refusals;
loading through timer290 does not establish a universal entry threshold. All
accepted15–25 have payload/reread checks true and no attention. The historical
R01 null-Sparx refusal remains refused; it was never upgraded into evidence.

Root also inspected native images showing the Wizard attacking, Spyro's death
animation, and the restarted level with Sparx and visible life counter3. This
supports one cold-entry, landing, enemy-damage, player-death and respawn cycle.
Enemy defeat/reward, normal portal reentry, full-speed gameplay, audio, allocation
peaks, other placements and textured/Wizard mixtures remain separate gates.

## Closed/restored evidence

Private runtime journal and complete observations:
`_local/native-fresh-wizard-separated-runtime-20260908-r02.CABpLh/`.

Duck PID27834/exec98346 and observer exec26105 closed with exit0. Observer final
full checks passed. Disconnect auto-resume was detected and paused before Quit;
Save State For Resume remained unchecked. Both INIs restored byte-exact and all
nine pre/post hashes matched: original BIN/CUE/card, candidate BIN/CUE/receipt,
Duck executable and both settings files. No card copy-back or R03 replacement.

`observer01.jsonl` SHA256:
`b09368b146d6e6e4050f93671dfddcb1bf31fd377b7fc5cb234f452083d5e082`.

Native screenshots in that folder:

- `screenshots/Spyro the Dragon 2026-09-08-10-15-35.png`, SHA256
  `ef0d8fdbc03d3a74a080ed5345244d121e72c9709e9ae04d1e05f4893748d3ee`.
- `screenshots/Spyro the Dragon 2026-09-08-10-18-42.png`, SHA256
  `b5c5c4b90512a4c656f72e6b0e4d2703dfc8fd05bb2745f5a07629870414ac66`.

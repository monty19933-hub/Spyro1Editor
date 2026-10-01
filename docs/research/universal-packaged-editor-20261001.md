# Packaged editor export and native readback

The final Developer ID signed macOS arm64 1.0.1 application was exercised with
an isolated copy of the saved user project. The original project and previously
released packages were preserved. Application source is `4afc9bf`; all product
and test bytes equal the `3196a94` frozen source used for the 324-check Release
regression and final coverage matrices. Later source-archive changes only add
or update documentation; the delivery receipt verifies this distinction.

## Normal editor workflow

The global object browser placed a Peace Keepers row 5 red gem beside the saved
eye-free egg in the authored scene. Saving and reopening retained both objects,
the two terrain faces and a treasure target of one. Move Face, Edit Point,
Paint Face, Add Terrain, Remove Terrain and Save Terrain were available for
the selected floor. This verifies their availability; the earlier terrain
report retains the separate native edit/collision evidence.

Normal Create BIN/CUE completed for both explicit start choices, authored level
and Artisans. The two outputs used the same scene and edit ledger. Independent
raw readback and 17 source-composition checks confirmed two physical actors,
16 dynamic slots, logical save identities zero and one, fresh treasure target
one, global egg target five and 951,916 bytes of final RAM headroom. All 254
other WAD owners matched the saved-project baseline, including the private
terrain texture, eight terrain paints and existing object edits. The start
choices have identical WAD content and differ only at the two guarded startup
instruction words in the executable.

The final scene SHA-256 is
`ef4ba69ba89de754240c0b4ddff39094abe7abfa75657f4cf803788245f06ad3`.
The ledger SHA-256 is
`15d8289f9ed963dec57739b8795821cf688a37b2e7a65ce3830b85c357390d47`.
The authored-start BIN SHA-256 is
`66e8c1d4822e218cc14fce09f21dfba8235aa30530d707a39b1ed7b04f1d8880`.
The Artisans-start BIN SHA-256 is
`b7180597a18a5bb5558b9bf9c90fe44fbe10136ce6b2d60bfe942f282532fc89`.

## Native run of the exact GUI export

A byte-identical copy of the authored-start BIN cold-booted in an isolated
DuckStation instance using the original USA firmware, software renderer,
recompiler and a new blank memory card. Normal New Game entered the authored
scene. The red gem collected at spawn and awarded exactly one treasure.
Controller-only jumping and rightward movement collected the egg and landed
back on the floor without falling, respawning or position assistance.

Read-only full RAM captures confirmed level index 35, one global and level
gem, both physical rows in state 0xFD and both durable/checkpoint collection
bits set. The inventory visibly showed 1/1 treasure, 1/1 dragonfly egg and
global eggs 1/5. The on-floor egg and inventory icon were eye-free. Both settled
captures contained zero live type-12/kind-2 collection particles and zero
malformed collection stars. Other ordinary ambient particles were retained.

The initial RAM SHA-256 is
`e804f853e64d65022f2b7896d404a0665dcd3321ca10aa40e6b10c7468b2ea07`.
The post-movement RAM SHA-256 is
`81e6c4b5ac8d6373f52e9b1ab77ca910b65752f1583d6659358cd993d41c2b51`.
The private process was closed without a resume state. The blank card remained
byte-identical with SHA-256
`52e9972468ee7f0864c110c06dc13530b71c05e1669c9a4cbe7e8c26d91be220`.
There were no runtime RAM writes or save states. This scene does not contain a
fairy save point, so this receipt makes no card-save/cold-load claim.

Private artifacts are under the task workspace:

- `work/universal-v1-continuation/packaged-gui-v1.0.1/final-two-objects-authored`
- `work/universal-v1-continuation/packaged-gui-v1.0.1/final-two-objects-artisans`
- `work/universal-v1-continuation/verification/final-packaged-1.0.1-independent-acceptance.json`
- `work/universal-v1-continuation/runtime/packaged-two-objects-final-native-receipt.json`
- `work/universal-v1-continuation/runtime/packaged-two-objects-final-inventory.png`

The independent preservation audit also verified all 173 protected original
project files, their snapshot copies, both original discs and the three prior
V1.0 archives. The actual packaged editor and private native process were
closed after testing; the user's earlier running editor was left untouched.

These results cover this exact authored fixture and workflow. They do not
establish gameplay for every composing Moby, final campaign progression,
Windows runtime or macOS notarization. Universal gameplay remains unfinished.

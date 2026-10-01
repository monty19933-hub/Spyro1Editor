# V1 packaged editor verification

The first V1 package was built from product commit `5d64982`, with source-only
legacy-guide redirects in `c206863`. Its stable display version is 1.0, internal
version 1.0.0, and manifest schema 3. The complete package verifier passed for
macOS arm64 and Windows x64. The Mac bundle is Developer ID signed with hardened
runtime; it is not notarized. Windows execution is not verified.

## Actual packaged GUI checkpoint

The exact Mac executable was launched as PID 1339 using the isolated profile
`work/v1-continuation/packaged-gui-v1`. It loaded the saved Artisans project with
the original eight imports, five removals, eight terrain paints and private
texture 68. The Terrain tab visibly exposes Move Face, Edit Point, Paint Face,
Add Terrain, Remove Terrain and Save Terrain.

Normal Create BIN opened the project-scope dialog, listed saved Artisans and
Untitled Level, and preserved old experimental drafts separately. Choosing
Artisans produced a new BIN/CUE with a visible Created status. Its BIN SHA-256 is
`766b171af15469711d3b4543bc819d64f574e39fc89a342cfaf83b22e60015a5`,
byte-identical to the independently verified complete saved-project baseline.
The CUE and both native startup words were independently checked.
Receipt: `verification/packaged-user-baseline-export-receipt.json`.

File > Open Authored Level opened the saved scene in the same viewport and
Terrain panel. Add Terrain copied a selected face, Save Terrain recorded three
faces and one object, and Open Authored Level displayed the saved third face.
Selecting that face and Remove Terrain, then Save Terrain, returned the saved
scene to two faces and one object. All edits were in the isolated copy.

This checkpoint exposed two presentation defects before delivery: Undo Terrain
was disabled immediately after adding an unsaved fresh face, and the panel's
retail-only tail displayed irrelevant native texture-capacity diagnostics in a
fresh scene. The delivery rebuild corrects these, with focused tests and final
package evidence recorded in the release receipt.

## Preservation and evidence boundaries

The original beta54 packages and application remain separate. A post-quit UI
observation briefly auto-launched V1 without the private environment. No editing
action was performed. Its four automatic project metadata/support-document
writes are separately preserved and reverted to the immutable snapshot; game
edits and research files were unchanged. Global current-project/update-check
metadata is recorded separately, without pretending an unrecorded pre-launch
hash exists. Subsequent test exits avoid that auto-launch behavior.

The public packages contain editor code/metadata only: no disc, BIOS, emulator,
card, extracted game asset or test profile. Native gameplay evidence is in the
saved-project, terrain and Druid runtime reports. Package verification is not
Windows runtime, notarization, full-game coverage or human acceptance.

# Fresh-level material persistence and exact saved Wizard pairing

September 8, 2026. Source/editor foundation, not an enabled aggregate exporter.

The existing whole-level document now supports an explicit schema2 material pair
for its two floor triangles. Each slot retains either base floor color or a
retail terrain descriptor (variant, WAD entry and texture ID). Slot order and
duplicate selections are preserved. Existing schema1 bytes remain unchanged;
clearing both schema2 slots to base color does not silently downgrade the file.

All five existing content-reconstruction paths preserve materials through floor
and route edits, addition/removal, reordering, undo/redo and save/reopen. The
window labels the schema and both selections. The schematic explicitly states
that selected materials are not rendered. There is no picker or textured preview
yet. The descriptor records intent, not proof that a source record is portable.
Existing native Wizard compilation explicitly rejects schema2 before source I/O
instead of silently dropping its materials.

A separate internal saved-Wizard context pairs the actual saved snapshot with
its actual authored candidate chain. Equivalent bytes or separately reparsed
objects cannot replace those owners. It reuses the existing final read guard and
raw project/document revision checks; it does not publish or change receipts.
Aggregate export remains disabled until this context spans actual publication.

## Verification

Tests first captured missing-API failures and actual material-loss failures in
Session Replace and Window Apply. The source test also reproduced schema2 being
admitted by the old Wizard boundary before the explicit rejection was added.
Retained evidence is under
`_local/research/dark-wizard-native-closure-20260906/aggregate-material-implementation-r01.wXlUGx`.

Final focused gates: material UI48 plus existing aggregate UI219; session138;
legacy UI65/researchUI333; aggregate689 (354 refusals), Store112, Fork248, Guard170;
legacy floor243/Store83/Lease59/compat38. Actual authored source final232 includes
saved-Wizard binding47 plus its source-free4. Original BIN/CUE before/final hashes
match, and the corrected e57fed... full-image golden is unchanged. No ROM was
published by these tests. Root and an independent peer reviewed the changes;
Root viewed the final headless frame and its unobscured floor/material notice.

Combined all-project Release/default gate:325 checks, zero failures, process
closed exit0. Exact report:
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-kcn758vr/summary.json`.
This includes the separate fixed-a textured candidate work, not gameplay tests.

The preserved R03/R04 packages have not been rebuilt with these changes.

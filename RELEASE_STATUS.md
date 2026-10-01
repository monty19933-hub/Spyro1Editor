# Beta V6 build 54 release-candidate checklist

Application packages were built from `6a2d98e`. Subsequent commits change tests
and evidence documentation only. The release candidate prioritizes the shared editor workflow and usable capacity reclamation.
Fly Anywhere is deferred at the user's request. Broader universal gameplay
coverage remains open; catalog/static results are not runtime acceptance.

- [x] Preserve original checkout, handoff, game images, saves and prior packages.
- [x] Remove added eyes from both egg models and native inventory icons.
- [x] Native Stone Hill pickup, global/per-level 1/5, normal card write and cold load.
- [x] Native authored sign text displayed in Stone Hill.
- [x] Required-egg percentage and bonus predicates: source guards and 4,489 independent instruction checks.
- [x] File > New Level uses the same main viewport and terrain/object tools.
- [x] Authored geometry, collision, material selection, stable IDs, undo/redo and save/reopen.
- [x] Normal Create BIN combines saved retail and fresh content; failed export preserves existing output.
- [x] Normal native New Game enters the fresh level; textured terrain, imported sheep and walking observed.
- [x] Native rotation export and edits-during-save race fixed.
- [x] Cross-level class remapping and dependency closure expanded without prior composed-cell regression.
- [x] Research/flight controls hidden from normal release; saved research flight settings preserved and ignored by release export.
- [x] Permanent deletion: physical reuse, retired logical identities, native cold-load and six guarded save-map owners verified.
- [x] Release/Debug broad runs completed; affected failures corrected and focused retests passed. Source-QA limits are recorded below.
- [x] Developer ID signed macOS arm64 and Windows x64 packages; package identity/content verifier passed.
- [x] Exact final Mac GUI: reopen three authored faces, add default Sheep without painting, save/reopen and repeated Create BIN.
- [x] Exact final packaged export: native New Game entered slot 65, sheep and floor visible, short collision-supported walk passed.
- [x] Fixed untextured fresh-level imports by admitting only the exact empty native terrain texture component.

## Evidence boundaries

Stone Hill card save used position-only assistance to reach a native checkpoint;
collection, native save, checksum and full quit/cold Load Game were observed.
The cold-load and fresh New Game runs did not write game RAM or load save states.
A complete natural endgame playthrough and all egg lifecycle combinations remain
unverified. See `docs/research/native-egg-verification-20260930.md`.

The remapping matrix covers 145,285 variant/destination cells and 7,175 default
cells. 62,686 variant cells composed (32,578 static-only and 30,108 with unverified
behavior dependencies); all matrix runtime cells remain not-tested. That frozen
matrix predates the deletion change and is identified separately in its receipts.
All-Mobys-in-all-levels remains a coverage goal, not a completed release claim.

Fresh native export uses slot 65 and supports bounded authored triangles/quads,
static native terrain materials and admitted universal placements. It does not
automatically author portals or a full campaign. A tested scene does not certify
every possible geometry/material/Moby combination.

The Mac package uses Developer ID signing without notarization. Windows
package checks do not imply Windows runtime acceptance. No source image, firmware,
card, state or extracted native asset belongs in application/source archives.

Private logs and fixtures remain in the task `work` folder. Final deliverables
belong in the new `outputs/SpyroEditor-beta54-release` folder; beta53 RC2 stays
unchanged. User-facing guides are `docs/public-user-guide.md` and
`docs/public-known-limitations.md`.

## Regression evidence and boundaries

Release and Debug each built all 294 smoke projects and passed 464 of 466 initial
runner checks. The two failures (deferred-flight expectation and alternate-profile
visual-parent duplication) were corrected and both focused cases passed in both
configurations. Final native mapper checks passed in both configurations; the
exact final packaged Core passed 6,072 scalar/emission/readback checks, including
all six source-bound native owner windows. These are instruction/byte checks,
not 6,072 gameplay tests. Texture ownership passed 97 source-free assertions in
each configuration, including malformed empty-header refusals.

The bounded source-native suite passed 52 of 68 cases (45 CLI and seven Python);
16 legacy research cases remained unrun at its original 1,800-second wall limit.
A pinned legacy oracle was copied into an isolated mirror, preserving its source
hash. Legacy UI expectations for hidden research controls and retired physical
rows were updated to the release contract and the affected focused checks passed.
Full histories and bounded continuation results are recorded in the delivery's
Verification/release-regression-20260930 files. This is not a blanket all-green
claim for every historical research test.

The final packaged export SHA-256 is
`6fa099c7e718384269281eb261294c43e839bc81780c7fdc516d1ab1d2256e2c`.
It was made through normal Create BIN after saving/reopening the GUI-authored
scene, then cold-booted with a real US BIOS and a new private memory card.
Read-only debugger inspection recorded native level 65, one class-0x00A actor,
spawn position 8705/8888/9060 and a short walk to 8705/9968/9063. No RAM writes
or save states were used. Exact receipts remain in the private task work folder.

Whole-game universal behavior, retail topology editing, full fresh-level campaign
entry/exit/save coverage, all egg lifecycle/endgame cases, Windows execution and
Mac notarization remain open. Fly Anywhere is intentionally deferred. Use the
candidate honestly within those limits; this is not full original-scope acceptance.

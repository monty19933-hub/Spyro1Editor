# Beta V6 build 54 release checklist

The release prioritizes the shared editor workflow and usable capacity reclamation.
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
- [ ] Final integrated permanent-deletion and logical-save mapping verification.
- [ ] Final Release/Debug regression and source checkpoint.
- [ ] New macOS arm64 and Windows x64 packages, archive and exact-package GUI verification.

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

The new Mac package will use Developer ID signing without notarization. Windows
package checks do not imply Windows runtime acceptance. No source image, firmware,
card, state or extracted native asset belongs in application/source archives.

Private logs and fixtures remain in the task `work` folder. Final deliverables
belong in the new `outputs/SpyroEditor-beta54-release` folder; beta53 RC2 stays
unchanged. User-facing guides are `docs/public-user-guide.md` and
`docs/public-known-limitations.md`.

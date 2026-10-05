# Handoff — Spyro Editor 1.1 release-readiness pass (2026-10-05, Claude)

## Where
- Worktree `/Users/john/Documents/Codex/2026-09-29/files-pasted-by-the-user-spyro/outputs/SpyroEditor-source`,
  branch `release/release-ready-20261004` (from `release/universal-gameplay-20261002` + 1.0.10). Nothing is pushed.
- Packages and release documents: task-root `outputs/SpyroEditor-V1.1-release` (`ENGINEERING-REPORT.md` has the
  product source revision, zip checksums and every verification result). 1.0.9 and 1.0.10 packages are untouched.
- Worklist, evidence, test projects, emulator harness: task-root `work/release-ready-20261004`
  (`WORKLIST.md`, `evidence/`, `projects/`, `runtime/`, `tools/`, `gui-final/`).

## What changed (release mode unless noted)
- Shell and wording: `MainWindow.UiShell.cs`, `MainWindow.PlainText.cs`, `MainWindow.Navigation.cs`,
  `MainWindow.BuildSafety.cs` (Export Check), `MainWindow.TerrainPanel.cs`, `MainWindow.PortalRoutes.cs`,
  `MainWindow.StartBlank.cs`, `MainWindow.ExportScope.cs`, Help text in `MainWindow.cs`. Developer-mode strings
  are kept where the older tests run with `SPYRO_EDITOR_RELEASE=0`.
- Keys: `EditorViewport.cs` raises `ObjectRemoveRequested` / `UndoRequested` / `RedoRequested`; nothing acts
  while a text field has focus (`--keyboard-ui`).
- Terrain problem bar and export check: `MainWindow.TerrainProblem*.cs` (`--terrain-problem-ui`).
- Export safety: `ExportScratchSpace` (aggregate scratch and `output/_combined-build` staged images),
  `--export-workflow-ui` (refused export, fixed export, later failed export keeps the last good BIN).
- Fly Anywhere: `MainWindow.FreeFlight.cs` (one auto-saved checkbox), export wiring in `MainWindow.cs`
  (`CreateSavedEditsStageAsync`) and `MainWindow.SceneDocument.cs`; crash fix in
  `NativeFreeFlightPatchBuilder.BuildGlobalPermission` (window now starts at `0x80044038`; permission is
  `(game state == 0)` when airborne or not in gameplay, flight courses untouched). Oracle:
  `Spyro.Editor.NativeFreeFlightSmoke`.
- Built levels: `NativeAuthoredMesh.AppendLattice` splits painted faces wider than 255 units for collision only.
- New Level confirmation and backup: `MainWindow.SceneDocument.cs` (`--new-level-confirm-ui`).
- Version: `Spyro.Editor.App.csproj` 1.1 / 1.1.0, previous stable 1.0.10.

## Tests
- Gate: `python3 tools/Run-SpyroEditorRegression.py` (result for this commit is in the engineering report).
- UI modes: task-root `work/release-ready-20261004/tools/run_ui_modes.sh --with-export` (18 modes; the
  project-bound ones use `projects/existing-copy`, whose discs point at verified local USA and Japanese images).
  `--fly-anywhere-ui` and `--start-blank-export-ui <project> <out> [level]` export real games and run separately.
- Coverage matrix: `tools/run_coverage.sh <out>` then `coverage_matrix.py <out>` in the same folder.
- Emulator: `work/release-ready-20261004/runtime` (isolated DuckStation, GDB port 2470, its own memory card;
  `run.py`, `gdb.py`, `flytest.py`, `flylevels.py`, `eggsave_a.py`/`eggsave_b.py`, `signtest.py`, `blanktest.py`).
  One emulator at a time. Never use the owner's DuckStation profile or any other running emulator.
- Any launch of the app or a UI test must set `SPYRO_EDITOR_WORKSPACE`, `SPYRO_EDITOR_DATA_ROOT`,
  `SPYRO_EDITOR_PROJECTS_ROOT` and `SPYRO_EDITOR_DIAGNOSTICS_DIR` so the owner's project and settings are not
  touched. A copied settings profile must have `Settings/current-project.json` `projectRoot` rewritten first.

## Known stale tests (not in the gate)
- Default no-argument `UiSmoke` (looks for a removed "More…" button).
- `--fresh-main-ui-only` passes four stages, then stops at a fixture whose player start the newer
  safe-start rule rejects.

## Not verified
- Windows package never run. Mac package not notarized. Fly Anywhere not played in 26 normal levels and
  Gnasty's Loot. No whole-campaign playthrough. Universal object behaviour beyond the documented checks.

## Next (only if the owner asks)
- Owner's hands-on review with `PLAYTEST-CHECKLIST.md`; fix what it finds.
- Notarize the Mac app and run the Windows package before any public release.
- The owner's real project still has one folded Artisans corner; the red bar's **Undo that change** fixes it.

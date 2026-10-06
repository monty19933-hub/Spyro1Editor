# Handoff — Spyro Editor 1.2 (owner feedback on 1.1) — 2026-10-05, Claude

## Where
- Same worktree and branch as 1.1 (`release/release-ready-20261004`); nothing is pushed. Packages and documents:
  task-root `outputs/SpyroEditor-V1.2-release` (1.1 and earlier packages untouched). Worklist section E, evidence
  and emulator logs: task-root `work/release-ready-20261004` (`WORKLIST.md`, `evidence/terrain-*`, `evidence/tile-type`,
  `evidence/fly-levels`, `evidence/find-space`, `evidence/runtime/{lower-*,tiletype-*,flytwo-*}`).

## What changed
- Find space: `UniversalCapacityPlanner.Analyze` takes a progress callback and a cancellation token;
  `MainWindow.UniversalMobyLibrary.cs` shows progress, live plans and a Stop button, bounded 6 versions × 48 options.
  Test `--find-space-ui`.
- Terrain limits at edit time (`MainWindow.cs`): `TryPlanTerrainTileMove` / `ApplyTerrainTileMove` (Raise, Lower,
  drags: collision corners only, shared corners joined), `TryCheckTerrainSectorRoom` (sector bands),
  `CollectCollisionTriangleOffenders` (255 step, ±255 sideways, winding), `CollectTerrainExposureOffenders`
  (`TerrainPatchExporter.FindTerrainExposureConflict`), `CollectCollisionIndexOffenders`
  (`TerrainPatchExporter.TryCheckCollisionIndexCapacity` + `TryLoadCollisionLookupFromSource`),
  `TrimTerrainBrushDeltasToFormatLimits` for the brush, `AddJoinedTerrainFollowers`, coverage extended with collision
  corners (`WithCollisionCorners`), `ClassifyTerrainPatchSafety` judged by moved corners.
  Exporter: `PlanSceneSectorRebases` / `RebasedSceneSectorHeader` / `AddSceneSectorRebasePatches` (sector origin
  re-base so a lowered corner can be stored), collision-index refusal names the affected faces; plain wording in
  `MainWindow.TerrainProblems.cs`. Tests `--terrain-multi-edit-ui`, `--terrain-lower-ui` (export + emulator spec),
  `--terrain-survey`, `--collision-lookup-probe`; emulator `runtime/lowertest.py`.
- Tile Type: `MainWindow.TileType.cs` (dialog, plain names, apply through `ApplyNativeTerrainBehaviorToFace`),
  Tiles card button and kind text in `MainWindow.TerrainPanel.cs`; `NativeTerrainSurfaceCatalogBuilder.VisualTriangulations`
  (either quad diagonal) used by the catalogue and `TerrainPatchExporter.AddNativeSurfaceBehaviorPatches`.
  Test `--tile-type-ui` (export + emulator spec); emulator `runtime/tiletypetest.py`.
- Fly Anywhere per level: `NativeFreeFlightAirborneLoader.Build(mask)` (56-bit level mask classification; unlisted
  levels clear inherited flight slots), `NativeFreeFlightPatchBuilder.BuildLevelSet` (entry windows and permission read
  slot 33 at `*(80076378)+0xBC`), `NativeFreeFlightPatchPlan.CaptureLevelSetAsync` / `EnabledLevelIds`,
  `NativeFreeFlightCompositionPlan.CaptureAsync(path, levelIds)` (whole game when all 30 chosen), intent
  `EnabledLevelIds` / `WholeGame`, settings snapshot `EnabledLevelIds` / `WholeGame`, UI `MainWindow.FreeFlight.cs`
  (`BuildFreeFlightLevelChooser`, `SaveFreeFlightLevelAsync`), export wording in `MainWindow.cs`,
  `MainWindow.SceneDocument.cs`, `MainWindow.ExportScope.cs`. Oracle `NativeFreeFlightSmoke` (`LevelSetFreeFlightCases`,
  plus `--dump-native`, `--find-imm`, `--zero-runs`, `--refs-into`, `--level-animations`, `--loader-spare` probes).
  Test `--fly-anywhere-ui` (per-level ticks; exports a two-level game); emulator `runtime/flytest.py`, `flylevels.py`.
- Version: `Spyro.Editor.App.csproj` 1.2 / 1.2.0 (packages rebuilt 6 October with the terrain fixes), previous stable 1.1.

## Pitfalls met
- A UiSmoke case must `Path.GetFullPath` its arguments before setting the workspace: relative paths resolve against the
  cloned project once `Environment.CurrentDirectory` changes, and a non-existent workspace path makes the app fall back
  to the current folder (it wrote terrain edits into the source checkout and into `evidence/work/...`).
- Release projects have no RAM capture: the editor must read the collision lookup from the disc the way Create BIN
  does (`TryLoadCollisionLookupFromSource`), and `SpyroCollisionDecoder.Decode` must never be fed a subfile.
- The level-set loader's first version let a level after a chosen one inherit the resident flight pointer (the native
  stale test keeps valid resident addresses); the fix clears slots 33..35 in unlisted levels, and the oracle now
  simulates the slot loop.

# Handoff — Spyro Editor 1.1 release-readiness pass (2026-10-05, Claude)

## Where
- Worktree `/Users/john/Documents/Codex/2026-09-29/files-pasted-by-the-user-spyro/outputs/SpyroEditor-source`,
  branch `release/release-ready-20261004` (from `release/universal-gameplay-20261002` + 1.0.10). Nothing is pushed.
- Packages (built from `3353dd6`) and release documents: task-root `outputs/SpyroEditor-V1.1-release`.
  `ENGINEERING-REPORT.md` there has the checksums and every verification result. Later commits touch only
  `RELEASE_STATUS.md` and `HANDOFF.md`. 1.0.9 and 1.0.10 packages are untouched.
- Worklist, evidence, test projects, emulator harness: task-root `work/release-ready-20261004`
  (`WORKLIST.md`, `evidence/`, `projects/`, `runtime/`, `tools/`, `gui-final/`, `cleanup/`).

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
- Pointer and selection rules: `EditorViewport.cs` (object and facing drags share the 5-pixel dead zone;
  camera keys ignore Command/Control/Alt), `MainWindow.cs` `RemoveSelectedMoby` (nothing selected afterwards in
  release mode), `MainWindow.UiShell.cs` (list selection does not scroll the side panel).
- Status wording: `MainWindow.PlainText.cs` `PlainStatusForRelease` rewrites working messages for the status
  bar only (`--plain-status-only`). Add new technical messages there or write them plainly.
- Create BIN free-space rule: `ExportScratchSpace.RequiredFreeBytes` / `DescribeShortage`, used at the start of
  `CreateObjectTestBinCoreAsync` (three working copies for ordinary edits, five with built levels).
- Start-up problems: `StartupProblemWindow.cs` + `Program.cs` / `App.cs` (`--startup-problem-ui`).
- Version: `Spyro.Editor.App.csproj` 1.1 / 1.1.0, previous stable 1.0.10.

## Tests
- Gate: `python3 tools/Run-SpyroEditorRegression.py` (352/352 on `3353dd6`).
- Everything in one go: task-root `work/release-ready-20261004/tools/final_chain.sh <log folder>` (UI modes with
  a real export, the Fly Anywhere and Start Blank export tests, start-up and wording tests, then the gate).
- UI modes: task-root `work/release-ready-20261004/tools/run_ui_modes.sh --with-export` (18 modes; the
  project-bound ones use `projects/existing-copy`, whose discs point at verified local USA and Japanese images).
  `--fly-anywhere-ui` and `--start-blank-export-ui <project> <out> [level]` export real games and run separately.
- Coverage matrix: `tools/run_coverage.sh <out>` then `coverage_matrix.py <out>` in the same folder.
- Emulator: `work/release-ready-20261004/runtime` (isolated DuckStation, GDB port 2470, its own memory card;
  `run.py`, `gdb.py`, `flytest.py`, `flylevels.py`, `eggsave_a.py`/`eggsave_b.py`, `signtest.py`, `blanktest.py`).
  One emulator at a time. Never use the owner's DuckStation profile or any other running emulator.
- Packaged app by hand: `gui-final/tools/launch.sh <profile>` starts the unzipped package through LaunchServices
  with an isolated profile in a folder outside Documents (inside Documents macOS asks for folder access and the
  app waits without a window). `gui.py` sends real input only where the system's hit-test says the editor's own
  window is under the pointer.
- Some headless audit runs open real Finder windows on temporary folders; close them afterwards.
- Any launch of the app or a UI test must set `SPYRO_EDITOR_WORKSPACE`, `SPYRO_EDITOR_DATA_ROOT`,
  `SPYRO_EDITOR_PROJECTS_ROOT` and `SPYRO_EDITOR_DIAGNOSTICS_DIR` so the owner's project and settings are not
  touched. A copied settings profile must have `Settings/current-project.json` `projectRoot` rewritten first.

## Known stale tests (not in the gate)
- Default no-argument `UiSmoke` (looks for a removed "More…" button).
- `--fresh-main-ui-only` passes four stages, then stops at a fixture whose player start the newer
  safe-start rule rejects.

## Not verified
- Windows package never run. Mac package signed with the Developer ID found on the build Mac, not notarized.
- Fly Anywhere not played in the other 26 normal levels (Gnasty's Loot among them). Red and pink eggs not seen
  in game in this pass. Start Blank played in Stone Hill only. No whole-campaign playthrough. Universal object
  behaviour beyond the documented checks.
- A real macOS refusal of folder access (the start-up message was tested with an unreadable project instead).

## Small things left as they are
- Create BIN with nothing changed: "No saved edits were found. Change a level name, music track, skybox, object,
  or terrain first."
- The start-up message window logs "session ended cleanly" twice.

## Next (only if the owner asks)
- Owner's hands-on review with `PLAYTEST-CHECKLIST.md`; fix what it finds.
- Notarize the Mac app and run the Windows package before any public release.
- The owner's real project still has one folded Artisans corner; the red bar's **Undo that change** fixes it.

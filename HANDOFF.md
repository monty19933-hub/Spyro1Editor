# Handoff — 1.0.9 inventory column and terrain editing (2026-10-04, Claude)

## Where
- Worktree `/Users/john/Documents/Codex/2026-09-29/files-pasted-by-the-user-spyro/outputs/SpyroEditor-source`,
  branch `release/universal-gameplay-20261002`, 1.0.9 product source `f495eb7` (on top of 1.0.8 `f1ab4d1`).
- Packages: task-root `outputs/SpyroEditor-V1.0.9-release` (community/ad-hoc Mac, Windows unexecuted).
  Evidence and `final-receipt.json`: task-root `work/inventory-terrain-20261004`.

## Inventory (UniversalSpecialBehaviors.cs, UniversalSpecialObjects.cs)
- `InventoryLayout`: guarded immediates for one row size, title/divider positions, stolen-egg icons.
- Header code at `0x800673A0` (call site `0x8001BA04`), row code at the end of `0x8005EC04..0x8005F2A4`
  (site `0x8001C4D4`), last separator via `0x8001C4FC`. Data is now only the egg list.
- HUD egg: `UniversalDragonflyEggs.HudModel` converts the JP egg to the HUD model format read by
  `0x80022D1C` (the in-level animated format hangs the GPU there). Slot `0x1D2`, installed only when empty.
- Callers pass `japanese?.InventoryEggModel()`; without the JP disc the column has no gold egg.

## Terrain (TerrainPatchExporter.cs, EditorViewport.cs, MainWindow.cs, GeometryModels.cs)
- Edited points propagate to every HP vertex slot sharing the native point; sub-unit drift ignored.
- Viewport: 5px drag dead zone, multi-select (`MultiSelectedTerrainIndexes`), whole-tile brush preview.
- MainWindow: fold guard, multi-tile remove/undo, "Whole tiles" checkbox (default on).

## Tests run
- Regression 349/351 (failures pre-exist at f1ab4d1, see receipt); UniversalMobySmoke 103/0;
  TerrainCollisionFanSmoke 16 PASS; isolated-emulator inventory + exported Toasty eggs; package verify.
- Not run: terrain gesture UI automation; universal UI smoke (stale before eggs at f1ab4d1).

## Next
- Optionally add UI automation for multi-select and whole tiles.

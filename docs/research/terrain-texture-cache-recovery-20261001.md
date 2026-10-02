# Terrain texture cache recovery

The inspected live private project at
`/Users/john/Documents/Codex/2026-09-29/files-pasted-by-the-user-spyro/work/universal-gameplay-20261001/portal-gui/projects/default-project`
contained Artisans' editable geometry overlay but no
`editor-cache/terrain-textures/artisans` directory. The protected user project
at `/Users/john/Documents/Spyro Editor/Projects/default-project` contained all
136 normal/close PNGs and both current format-10 native LQ/HQ sidecars.

`ConfigureCompleteTerrainEditing` does not disable textures: it clears
presentation predicates and preserves Complete Scene. That code is unchanged
from baseline `3c1aa36`. In the active renderer, `EditorViewport`, missing
native texture images cause `TryDrawNativeTerrainFace` to return
`UnresolvedFallback`; `DrawTerrainFace` then paints a solid polygon and detail
outline. There is no `SoftwareViewport3D` class in this current source tree.
Ordinary level selection previously loaded cached geometry without ensuring
the corresponding material cache; automatic cache creation occurred through
disc selection or the explicit full cache action.

`NativeTerrainTextureCacheRecovery.EnsureAsync` uses the existing strict
complete-cache validator. It validates PNG dimensions, bytes and hashes plus
the raw HQ/LQ sidecars. Valid cache reuse requires no source disc and does not
write anything. Missing or damaged caches use the existing selected-level
atomic source-bound builder and validated readback. The builder changes the
material cache only, leaving editable geometry intact.

`MainWindow.TerrainTextureRecovery.cs` captures workspace, level-load request,
scene identity, operation generations and the explicit source path before
awaiting recovery. It validates ownership again afterwards. The existing
locked ID65 loader remains separate. Offline cached-geometry editing remains
available; missing source materials return an actionable warning that the
caller must show after applying the scene. A stale owner returns
`OwnerCurrent=false` and must not apply its result.

The ordinary loader now awaits
`EnsureRetailTerrainTexturesForLoadAsync(level)` after ordinary geometry load,
checks its `OwnerCurrent` and the request ID before `ApplyLoadedLevel`, and
appends its optional `Warning` after the usual loaded-scene status. Cache import
and full cache-build completion also retain the warning for the matching level
and workspace. A later successful complete-cache validation clears it.

The independent `Spyro.Editor.TerrainTextureRecoverySmoke` uses the original
USA source BIN and the live private Artisans geometry overlay as read-only
inputs, with a new private output directory. It checks missing cache recovery
(68 source textures), complete material validation, offline valid reuse with
all bytes and timestamps preserved, damaged PNG recovery, missing-source
refusal, unchanged source geometry, and read-only validation of the protected
user Artisans cache. All 12 checks passed. It does not control an app or emulator. Receipt:
`work/universal-gameplay-20261001/fixtures/terrain-texture-recovery-r02/texture-cache-recovery.json`.
The new app partial compiles in an isolated Release build with zero warnings
and errors. The live private app rebuilt all 136 Artisans PNGs through ordinary level
selection and visibly rendered the original grass, masonry and roof materials.
The viewport retained 5,199 faces. CUA selected face 3120, raised all four
vertices by 32, and used Undo Height Only; the editor returned to zero terrain
edits while preserving its textured view. Receipt outside the checkout:
`../../work/universal-gameplay-20261001/portal-gui/texture-and-portal-ui-receipt.json`.
This is editor UI evidence; prior terrain emulator proofs remain separate.

Prior all-level material evidence remains separate from this repair:
`work/universal-v1-continuation/aggregate-final-expected-input/_local/ui-overhaul/terrain-native-map-materials-metrics.json`
records 35 levels, 762,560 verified physical-table-2 corners and complete-scene
material rendering. Its adjacent `terrain-fly-game-view-metrics.json` records
35 levels, native distance suppression in 33 and LP payloads in 32, under
`all-unique-source-sectors-native-distance-hp-lp-full-material-v6`. The source
UI smoke checks nearby Artisans off-group faces remain natively textured,
distant faces transition to LP Gouraud colors, and every source sector remains
eligible. Those existing renderer tests assumed a complete material cache;
the new recovery test covers the missing-cache path that caused this report.

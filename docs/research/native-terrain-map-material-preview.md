# Native terrain Map material preview

## Purpose

The Map viewport is a top-down editing overview. It does not have a retail
gameplay camera, so it cannot select the camera-dependent HP depth-cue endpoint
or claim pixel-exact equivalence to a DuckStation frame. It can still preserve
the level's native material identity instead of replacing each face with one
averaged editor color.

The current contract is:

`native-load-normal-hq-plus-physical-table2-complete-scene-map-v5`

For each eligible native HP face, Map now uses:

- the native-load-initialized normal-HQ 64x64 terrain tile;
- the face's exact raw four-corner topology and UV orientation;
- physical HP color table 2, retained by the legacy serialized `NearColors`
  field and used as the stable camera-less overview endpoint;
- per-corner modulation rather than one averaged `SurfaceColor`; and
- geometry height for ordinary-face painter ordering, without the former
  hardcoded rule that always painted stone/cliff after grass/ground.

Avalonia Multiply uses 255 as neutral while the PS1 GPU texture modulation uses
128 as neutral. Map doubles each source shade channel before Multiply so the
editor overview applies the equivalent modulation scale. This is an editor
presentation conversion, not a replacement for the bounded RGB5 compositor in
Fly 3D.

Native untextured sentinels render their exact four-corner Gouraud colors
without the texture-modulation doubling. Textured water/lava/ooze stay on the
guarded translucent editor underlay until Map has a proven PS1 blend path;
otherwise their native tile would be displayed falsely opaque. Editing and
selection overlays stay above either material path.

Complete Scene is the default and keeps every captured HP face visible with a
material outcome. Nothing is converted to an outline-only substitute or hidden
because it belongs to another environment group. A whole-level overview does
therefore show source sheets and groups together that the game normally submits
only from particular camera contexts; their extracted materials remain intact.
Game Camera uses camera-dependent DPCS and native HP/LP selection over the same
unchanged source scene.

## All-level verification

`Spyro.Editor.UiSmoke --terrain-native-map-materials-only` renders all 35
levels and records a per-level material snapshot. It verifies physical table 2
corner by corner, the exact native normal-HQ and untextured Gouraud paths, zero
unresolved material fallbacks, and that every complete-scene face has a material
outcome with zero outline-only substitutes.

The generated metrics and captures are written under:

`_local/ui-overhaul/terrain-native-map-materials-metrics.json`

`_local/ui-overhaul/terrain-native-map-<level>.png`

The source color fidelity smoke separately retains all decoded HP faces and
guards the retail layout: all four-byte table-1 colors form one contiguous bank,
followed by all four-byte table-2 colors. Format 9 invalidates caches made by the
former incorrect interleaved-pair decoder.

## Boundary with Fly 3D

Map is a source-material editing overview, not a gameplay renderer. It uses the
physical table-2 endpoint consistently because a top-down whole-level view
has no single camera depth. A game-frame equivalence claim still belongs to Fly
3D and requires the retail camera, native-distance HP/LP selection, GTE
projection and clipping, complete
ordering-table traversal, occlusion-group selection, the native skybox, and
runtime texture playback state. Per-level color corrections and global green or
tan adjustments are deliberately excluded.

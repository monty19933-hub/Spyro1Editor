# Spyro Editor 0.1.0-beta.15 Guide

This beta build is the clean editor surface for object, level, sky, and terrain
texture testing. It hides internal research probes and unsafe object candidate
experiments. Cross-level object imports remain post-release research.

## Start

1. Open `Launch Spyro Editor`.
2. Click `Open BIN/CUE`.
3. Choose your own Spyro the Dragon disc image.
4. Wait until the status bar says the editor rebuilt level maps and object caches.
5. Pick a level, change its name or music, or edit supported objects, then click `Create BIN`.

The editor does not include game data, BIOS files, emulator files, RAM dumps,
patched discs, or extracted assets. Patched BIN/CUE output is written to the
package `output` folder. If saved edits are not export-ready, the editor removes
older matching test BIN/CUE output instead of leaving a stale disc to load.

## Reporting Issues

Use GitHub Issues for bugs found in release builds. Include the release zip name,
your operating system, the level, the editor action, and what happened in-game.

Do not upload game images, BIOS files, patched BIN/CUE output, emulator saves,
save states, RAM/VRAM dumps, extracted game files, or texture dumps. Screenshots,
short clips, logs, and editor-generated JSON reports are okay when they do not
contain copied disc data.

## What Is Enabled

- Level browsing from the bundled Spyro level catalog.
- Map View and Fly 3D viewing.
- Terrain maps rebuilt from the BIN/CUE selected by the user.
- Moby/object placement rebuilt from the BIN/CUE selected by the user where the
  level has decoded source tables.
- Terrain-snapped object placement that preserves same-level donor ground
  offsets for copied chests, enemies, and scenery.
- Perspective-correct placement in both Map View and Fly 3D, so object markers
  stay anchored to the terrain point that was clicked while the view moves.
- `Lower Layer` and `Upper Layer` object actions for stepping a selected object
  between overlapping floors at the same X/Y, including enclosed rooms where a
  roof would otherwise be the easiest surface to hit.
- Known object labels and relationships from the bundled metadata.
- Editing all 35 level names from the release-visible `Level Name` panel. For a
  regular level, the saved name is used by its gold portal lettering, entering
  or confronting transition, and guidebook entry. Homeworld names are used by
  the homeworld title, balloon transition, and guidebook heading. Regular
  levels and homeworlds accept up to 19 characters; boss levels accept up to
  16. Longer names share a bounded native string pool, and `Create BIN` reports
  how many characters must be shortened if a large saved batch cannot fit.
- Choosing music for any level from the 46 built-in tracks present on the
  user's disc. A saved choice keeps looping instead of switching back to that
  level's native late-play alternates after 8-12 minutes.
- Editing the native sky in every level from the release-visible `Skybox`
  panel. `Recolor` preserves native geometry and accepts Night, Dusk, manual
  colors, or colors sampled from a user PNG. `Swap From Level` lists skies from
  the user's own disc. Fitting donors stay in place; larger donors use guarded
  WAD expansion and relocate the executable inside the supported original USA
  disc's verified free ISO region. `Import Native .sky` accepts only
  structurally valid Spyro 1 sky blocks within the import and disc-growth
  limits. The playable sky and linked homeworld portal copy are always patched
  together. Geometry swaps and native imports automatically bypass the target
  level's stale sky-occlusion lists so all donor parts can render. `Original
  Sky Preset` includes Stormy Spring, Blazing Desert, and Aurora Dream. Each
  uses geometry read from the user's selected disc plus original palette and
  preview artwork shipped by the editor; no extracted sky block is included in
  the app. For `Swap From Level`, the primary `Match Level Terrain Palette`
  button also matches the
  destination's own near/far landscape lighting and referenced landscape
  texture palettes to the donor. Near and far terrain use one shared affine
  color transform, preserving native vertex blending and low/high-detail color
  averages while smoothing an aggressive dark match. This prevents a terrain
  sector from changing hue or brightness when its LOD changes. Drastic warm,
  cool, or saturation shifts also harmonize all near/far sectors and referenced
  landscape palette tiers toward the donor while preserving local brightness.
  The exporter
  transforms both RGB lighting lanes in every validated high-detail entry while
  preserving their native command bytes. It changes color only, not terrain
  shape or collision. `Advanced Options` stays collapsed unless the user wants
  to adjust strength, brightness, saturation, tint, or terrain patch scope.
  The optional `Match scenery, chests, and creatures` switch uses the game's
  existing neutral object-lighting material to bring ordinary scenery, chests,
  creatures, and dragons into the same environment grade. It never reroutes
  object rows to a reserved material slot, and gem/special-effect materials are
  left unchanged. Entering a level without an object-lighting match restores the
  native neutral value, so the grade cannot leak through a later portal.
  Very large hue changes are still beta: a few distant scenery or terrain LODs
  can retain their native color, and tree LODs are kept consistent rather than
  recoloring only their close model. Spyro's player palettes are excluded.
  `Reset to Normal Level Palette and Skybox` removes both parts of the saved
  environment edit.
- Painting one terrain or building face with an in-level look, actual texture
  art borrowed from another level, custom colors, or an imported palette. A
  local texture slot keeps face-only edits from changing every other face that
  used the original shared texture.
- Importing a PNG over a shared terrain/building texture when changing all
  faces that use that texture is intended. RGB, RGBA, grayscale, and indexed
  PNGs are supported within the editor's image-size limits. The editor previews
  raw image imports from their actual pixels and patches normal and close-detail
  texture descriptors when the level provides them.
- Moving, removing, cloning, and editing supported objects.
- Moving an existing dragon together with its pedestal and native `0x6E` scene-
  link control. Dragging, XYZ edits, nudges, and undo keep the three-row scene
  aligned, including when terrain snapping changes the final height. `Create
  BIN` also shifts the hidden approach-camera XYZ and every XYZ keyframe in the
  later rescue cinematic while preserving the original shot angles and timing.
- Copying and pasting a same-level dragon together with its linked pedestal and
  scene-link row while preserving their native XYZ offsets. Additional rescue-
  scene data for brand-new dragons remains a beta research path.
- Editing the source-derived `Fly-in Landing` marker in every destination
  level. Drag it or enter exact XYZ values to change where Spyro finishes the
  flight from a homeworld. Drag its selected direction arrow or enter an exact
  heading degree to change the approach direction. This does not change the
  separate return-home path.
- Editing all 29 native homeworld portal locations from the release-visible
  `Portal Location` panel. Selecting and moving a portal keeps its lettering,
  companion control, and travel path together. `Create BIN` also moves the blue
  portal plane, the private two-node route used to place and fly Spyro through
  the transition, its real type-6 walk-in collision surface, and the rebuilt
  collision lookup. Decorative stone arches and adding new portals are not
  included in this beta path.
- Editing object yaw/facing rotation for supported gems, enemies, chests,
  scenery, triggers, and pasted objects, with a compass preview and draggable
  selected-object facing arrow; `Create BIN` writes the native rotation matrix
  plus the compatibility yaw byte.
- Replacing an existing object slot with another object from the same level for
  enemies or other classes that are not safe as brand-new adds yet.
- Adding or pasting same-level enemy/chest clones by reusing matching same-level
  source slots when available.
- Continuing ordinary Flame/Charge Chest, native Life Chest, and Large Gnorc
  same-level clones as true-adds after matching reuse slots are consumed. In
  all 22 levels containing native Life Chests, copies preserve the original
  donor and receive independent private runtime blocks. Cross-level Life Chest
  imports remain guarded.
- Showing the remaining safe extra export slots for same-level enemy/chest
  copies in Add Object and copy/paste status text, including whether a proven
  family can true-add after those slots run out.
- Warning immediately after Add Object or copy/paste consumes the last
  known-safe slot for an unproven family. Later objects of that kind may not
  work as intended in-game until a later update expands support.
- Using `Edit Object` > `Change To` when you want to choose the exact source
  slot consumed by a same-level enemy/chest clone.
- The Add Object list only offers simple safe records and donors that already
  exist in the selected level.
- Inspecting trigger/helper-looking mobys as system/control records. They are
  not treated as normal placeable objects until their linked behavior is proven.
- Preserving the proven linked families: native dragon/pedestal/rescue-camera
  scenes, all 29 native homeworld portal triplets, 14 checked-in Return Home
  helper pairs, same-level chest contents, and both Artisans Treasure Gnorc
  reward-trigger clusters. Other trigger/control rows remain protected instead
  of being linked from proximity alone.
- Creating one local patched BIN/CUE from all saved level-name, music, skybox,
  environment-grade, object, and terrain edits across the editor.

## What Is Hidden

- Internal Spring Chest import research tools.
- Cross-level object candidate tests.
- Identity batch tooling.
- Smoke-test and live-RAM helper workflows.
- Internal cache/testing utilities.

If a level shows missing map or object data, use `Open BIN/CUE` again and wait
for the build to finish before changing levels.

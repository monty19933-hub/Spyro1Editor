# Spyro Editor Release Guide

This release build is the clean editor surface for normal object-edit testing.
It hides research tools, candidate exporters, terrain beta controls, and cross-level
object experiments.

## Start

1. Open `Launch Spyro Editor`.
2. Click `Open BIN/CUE`.
3. Choose your own Spyro the Dragon disc image.
4. Wait until the status bar says the editor rebuilt level maps and object caches.
5. Pick a level, move or edit supported objects, then click `Create BIN`.

The editor does not include game data, BIOS files, emulator files, RAM dumps,
patched discs, or extracted assets. Patched BIN/CUE output is written to the
package `output` folder.

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
- Known object labels and relationships from the bundled metadata.
- Moving, removing, cloning, and editing supported objects.
- Replacing an existing object slot with another object from the same level for
  enemies or other classes that are not safe as brand-new adds yet.
- Adding same-level donor clones for testing when that object already exists in
  the loaded level.
- Creating local patched BIN/CUE test output from the user's selected disc image.

## What Is Hidden

- Candidate cross-level object tests.
- Spring Chest import research tools.
- Terrain beta editing controls.
- Identity batch tooling.
- Smoke-test and live-RAM helper workflows.
- Internal cache/testing utilities.

If a level shows missing map or object data, use `Open BIN/CUE` again and wait
for the build to finish before changing levels.

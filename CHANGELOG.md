# Spyro Editor 1.0

## Changes since Spyro Editor Beta V6

- Restored terrain controls in existing levels: move faces and points in all
  three axes, copy faces, remove faces and paint textures. Export keeps close and
  distant surfaces, visibility and collision synchronized, with explicit checks
  for shapes the original game cannot represent.
- Added **Find space for this object…** to the object browser. It compares source
  variants and names objects whose removal would reclaim enough resources.
  Removing objects frees physical slots and reclaims models and textures when
  their last proven user is removed.
- Verified the Wizard Peak Armored Druid in Artisans through native attack, flame
  resistance and charge defeat. The specific Alpine Ridge variant that failed a
  native interaction is refused with an explanation.
- Fixed LP-only terrain overflow sectors entering the wrong native render queue,
  including removal exports that grow the level allocation.
- Fixed the editor camera snapping when removing, restoring or undoing an object.
- Fixed normal BIN/CUE creation when older experimental drafts coexist with the
  current project. The export dialog now chooses Artisans or the saved fresh
  level as the New Game start, while including both sets of saved edits.
- Made terrain texture allocation account for saved foreign objects, including
  their palette space, so the capacity check and export share the same constraints.
- Added full, copyable status details and simplified the normal editing controls.
- Added the V1.0 stable release and updater identity. Earlier beta packages and
  their update compatibility remain preserved.

Dragonfly eggs retain their eye-free game and inventory appearance. Fly Anywhere
remains deferred. See the included user guide and supported-scope document for
native limits and verification boundaries. No game image, firmware, emulator,
memory card or extracted game assets are included.

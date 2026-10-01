# Spyro Editor 1.0.1

- Expanded cross-level object support through verified native class lookups,
  initializer roles and dependency recovery.
- Imported missing player reaction animations with foreign enemies. The Alpine
  Ridge Druid now damages Spyro and allows normal recovery in the Artisans test;
  its animation memory is included in fit checks and removal suggestions.
- Fixed a game hang caused by unused slots left after permanent object removal.
- Fixed imported gems awarding zero and producing incorrect collection effects.
  All five native values passed collection, ordinary card save and cold Load
  Game in Artisans, retaining the correct combined total of 43.
- Expanded verified child-spawn argument recovery without admitting ambiguous
  object or particle dependencies.
- Made **Only what fits here** show completed successful checks and refresh
  compatibility details after checking a source variant.

Universal gameplay coverage remains incomplete. See the supported-scope document
for tested interactions and the game's resource limits.

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
  Fresh terrain copies support Undo immediately, and fresh levels hide irrelevant
  retail texture-capacity diagnostics.
- Added the V1.0 stable release and updater identity. Earlier beta packages and
  their update compatibility remain preserved.

Dragonfly eggs retain their eye-free game and inventory appearance. Fly Anywhere
remains deferred. See the included user guide and supported-scope document for
native limits and verification boundaries. No game image, firmware, emulator,
memory card or extracted game assets are included.

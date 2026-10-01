# Spyro Editor Beta V6

## Changes since Spyro Editor Beta V5

Internal build `0.1.0-beta.54`.

- Fresh levels now use the regular editor viewport, terrain tools, object browser,
  material previews, undo/redo and Save Level. Normal Create BIN includes saved
  retail edits and the authored level. New Game enters the fresh level directly.
- Removed research controls from the normal workflow and added a preview-based
  material chooser for fresh terrain. Fixed saving while edits are still being
  made, repeated terrain addition and native rotation export.
- Object removal now reuses vacated physical slots, omits retired imported rows
  and trims unused trailing rows. Native save mapping preserves collected-state
  identities when new objects occupy old physical positions.
- Expanded cross-level Moby class remapping and shared dependency support.
  Unresolved imports remain explicit; universal gameplay coverage is unfinished.
- Removed added eyes from dragonfly eggs in both game models and inventory.
  Added per-level/global counts, native collection persistence and required-egg
  completion and bonus checks. Native card save and cold-load passed in Stone Hill.
- Editable Japanese signs retain native in-game text rendering.
- Fly Anywhere is deferred to prioritize this release; normal exports ignore
  earlier research flight preferences without deleting them.

See the included user guide and limitations for supported scope. The macOS build
is Developer ID signed and not notarized. Windows execution remains unverified.
No game data, firmware, emulator or save files are included.

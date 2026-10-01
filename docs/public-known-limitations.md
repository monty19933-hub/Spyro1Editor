# Spyro Editor 1.0.1 — supported scope and limitations

- Cross-level imports have broader class remapping and dependency support, but
  **all Mobys working in all levels is not complete**. Static composition does
  not certify AI, sound, collision, triggers or linked behavior. The browser and
  exporter report unresolved dependencies and native resource limits.
- The Wizard Peak Armored Druid source variant has passed appearance, enemy
  attack, flame resistance and charge defeat in the edited Artisans test project.
  The Alpine Ridge row-1 variant now imports its missing player reaction
  animation and has passed native contact damage and recovery in Artisans.
  Enemy defeat and card reload were not exercised in that specific retest.
  Fitting into memory is separate from passing the game's behavior checks.
- Imported red, green, blue, gold and purple gem variants from Jacques passed
  native collection, ordinary card save and cold Load Game in Artisans. Their
  values remained 1, 2, 5, 10 and 25; all five stayed collected after reload.
  This verifies those exact variants and destination, not every source/level pair.
- Fresh levels use the regular editor and normal Create BIN, with authored
  geometry, collision, static native materials and Mobys. This build supports
  one fresh level in slot 65, at most 255 triangles, and the native coordinate,
  collision and memory limits. Vertices use whole editor units; collision vertices
  must stay within the native relative-coordinate range. Split larger faces if
  the exporter requests it.
  A fresh level does not automatically receive a portal network or campaign.
- Existing-level terrain supports guarded XYZ edits, face copying, face removal
  and texture painting through the shared editor controls. Close and distant
  geometry, visibility and native collision are updated together. Native
  coordinate, collision, memory and ownership checks can refuse a particular
  edit. Translucent source faces and ambiguous collision ownership are not
  supported by structural terrain editing. These limits are separate from the
  fresh-level triangle limit.
- Removed objects free reusable physical slots. Models and textures are shared:
  deleting one copy will not free artwork still used by other copies or linked
  objects. **Find space for this object…** names proposed removals and checks
  the resulting composition; you choose which objects to remove. Saved
  collected-state identities remain finite and are not recycled. A successful
  capacity check does not certify every enemy's AI or linked behavior.
- Eye-free eggs, per-level/global inventory, collection and normal card save and
  cold-load were observed in Stone Hill. Checkpoint navigation for that save test
  used position-only assistance. Endgame bonus predicates have source and
  instruction tests; a complete endgame playthrough and every egg/level lifecycle
  combination have not been verified.
- Normal Create BIN includes saved retail edits and the saved fresh level.
  Its dialog chooses where New Game starts. Both choices passed complete
  readback with the existing Artisans project, including its eight imported
  objects and private terrain texture. The saved fresh level also passed native
  New Game, walking, jumping and egg collection with its 1/1 inventory count.
  This does not certify every authored scene or a complete campaign.
- Fly Anywhere is deferred. Its earlier research settings are ignored by normal
  release exports and its controls are hidden from the normal release UI.
- The macOS arm64 release is Developer ID signed, but is not notarized. Windows
  x64 package structure is checked; Windows runtime testing was not available
  on the build machine.
- Use the supported unchanged USA SCUS_942.28 source. Japanese eggs/signs require
  the supported SCPS_100.83 image supplied locally. Custom music import and every
  native texture format are not supported.

Original discs, projects, saves and earlier packages are preserved. Always test
an exported CUE before sharing an edited game or relying on its progression.

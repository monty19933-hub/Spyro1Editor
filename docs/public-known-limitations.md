# Spyro Editor Beta V6 — build 54 limitations

- Cross-level imports have broader class remapping and dependency support, but
  **all Mobys working in all levels is not complete**. Static composition does
  not certify AI, sound, collision, triggers or linked behavior. The browser and
  exporter report unresolved dependencies and native resource limits.
- Fresh levels use the regular editor and normal Create BIN, with authored
  geometry, collision, static native materials and Mobys. This build supports
  one fresh level in slot 65, at most 255 triangles, and the native coordinate,
  collision and memory limits. Vertices use whole editor units; collision vertices
  must stay within the native relative-coordinate range. Split larger faces if
  the exporter requests it.
  A fresh level does not automatically receive a portal network or campaign.
- Retail terrain export supports guarded height changes and texture painting.
  General terrain addition, removal and topology growth in retail levels remain
  unfinished. Fresh levels provide those authoring tools in the same main editor.
  Unsafe geometry, collision or texture combinations stop with an explanation.
- Removed objects free reusable physical slots. Stable collected-state identities
  are retained and are finite: deleting an object does not recycle its saved
  identity. Shared or unresolved resources cannot safely be reclaimed.
- Eye-free eggs, per-level/global inventory, collection and normal card save and
  cold-load were observed in Stone Hill. Checkpoint navigation for that save test
  used position-only assistance. Endgame bonus predicates have source and
  instruction tests; a complete endgame playthrough and every egg/level lifecycle
  combination have not been verified.
- Normal New Game was observed entering a fresh authored level with textured
  terrain, collision and an imported sheep; native walking passed. This is one
  tested scene, not blanket acceptance of every authored combination.
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

# V5 Stone Hill retail-slot replacement

V5 begins with a deliberately narrower goal than adding a 36th level: compile a
replacement for the existing Stone Hill non-flight slot while preserving the
retail game's surrounding contract. Beta V4 remains frozen and published while
this work proceeds on an isolated V5 branch.

## Milestone 1: byte-identical baseline

The first milestone is source-bound infrastructure, not a claim that blank-level
compilation is complete. The versioned
`stonehill-native-level-replacement.json` manifest accepts only the checked clean
USA BIN and records complete preimages for:

- The disc image, WAD, executable, and outer WAD directory.
- Stone Hill's unique level-ID metadata at WAD entry 9.
- Metadata-adjacent entry 10, loaded-entry predecessor 11, and loaded data entry
  12. Entry 11 starts with level ID 12 and must not be mislabeled as Stone Hill
  metadata.
- The complete entry-12 nested header, its strict eight-row packed descriptor
  table, level-data subfile 1, scene/Moby subfile 3, and all 195 native Moby rows.
- The Artisans Stone Hill portal rows T38/T144/T157 and Stone Hill Return Home
  group T177/T179.
- The retail executable that owns level dispatch, save, and progression data.

The baseline planner contains zero writes. Its exporter rejects path aliases,
stale or modified sources, changed slot identities, malformed CUEs, separated
BIN/CUE output directories, and mutable attempts to claim runtime evidence. It
stages and verifies both files before publication and proves the final BIN is
byte-identical to the retail source.

Run the focused proof with a configured clean USA retail BIN/CUE:

```sh
tools/Run-SpyroEditorOfflineQa.sh --stonehill-level-replacement-baseline-smoke-only
```

This milestone is intentionally disconnected from the V4 editor UI and normal
Create BIN. It cannot modify a level.

## Next implementation gates

1. Inventory every entry-12 subfile and classify fixed, relocatable, and
   runtime-addressed regions.
2. Compile replacement terrain, collision, textures, sky, scene, and Moby-table
   payloads into the existing Stone Hill slot while keeping entries 9-11, SCUS,
   portal controls, Return Home controls, and progression data outside the write
   scope.
3. Link only Stone Hill-resident actors first, then import checked cross-level
   actor packages family by family.
4. Create a disposable DuckStation candidate and validate portal entry, camera,
   collision, gems, dragons, enemies, death/reload, Return Home, and save
   persistence.
5. Expose a guarded `Create Custom Level (replaces Stone Hill)` workflow only
   after that candidate passes.
6. Research a new 36th catalog/disc slot only after the complete Stone Hill
   replacement works. A 36th level requires new routing, level-table,
   executable, save/progression, WAD-growth, and runtime-dependency proofs; it is
   not implied by the replacement compiler.

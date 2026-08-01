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
- The preceding Artisans overlay/data pair at WAD entries 9/10 and the Stone
  Hill overlay/data pair at entries 11/12. The first overlay word is a
  PsyQ/linker overlay ID, not the destination level ID.
- The complete entry-12 nested header, its strict eight-row packed descriptor
  table, all eight native subfiles, and all 195 native Moby rows.
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

## Milestone 2: complete Town Square pair - focused runtime pass

Commit `46ae431` added a disposable compiler that installs Town Square's complete
retail overlay/data pair into Stone Hill's existing fixed-capacity entries 11/12.
It keeps the Stone Hill portal/save-slot identity, installs Town Square's checked
level-11 overlay-dispatch addresses, reroutes the now-unsafe Stone Hill title
demo to the native Doctor Shemp demo, and rebuilds Mode2 Form 1 EDC/ECC for every
modified raw sector. Artisans entries 9/10, portal rows T38/T144/T157, the native
Town Square donor pair, and unused Stone Hill capacity tails remain unchanged.

The focused candidate was:

- CUE: `Stone-Hill-slot-Town-Square-complete-level-RUNTIME-CANDIDATE.cue`
- BIN SHA-256:
  `5c23ad350edc6dcdab2c144d9d07a8d8ea98dda9b35c151d0335dd58a636c93d`
- Evidence recorded: 2026-08-01, interactive user report rather than automated
  emulator capture.

The reported DuckStation session entered the Artisans portal labelled Stone
Hill, completed the Town Square fly-in with zero errors, collected gems,
rescued dragons, and returned successfully to Artisans through Return Home. The
transition initially appeared to use Stone Hill's sky before abruptly handing
off to Town Square's sky, with the inverse handoff while flying out. That is a
nonfatal visual transition defect, not evidence of a failed destination load.

This is a focused runtime pass, not full promotion evidence. Enemies, the egg
thief, pause/inventory, death/reload, re-entry, save persistence, title-demo
playback, prolonged stability, and a seamless sky handoff remain unreported.
The generated static-proof JSON correctly retains `runtimeClaim: false`; this
section is the separate durable record of the user-observed runtime slice.

## Next implementation gates

1. **Completed:** inventory and source-bind all eight Stone Hill entry-12
   subfiles and the surrounding overlay/data pairs.
2. **Completed for an unchanged retail donor pair:** install Town Square's
   terrain, collision, textures, sky, scene, actors, and dragon packages in the
   fixed Stone Hill capacity. Arbitrary newly compiled payloads are not implied.
3. **Pending:** compile edited donor terrain/scene/object data and link imported
   actor packages instead of copying one unchanged retail pair.
4. **Partially passed:** portal entry, fly-in, gems, dragons, and Return Home
   passed in DuckStation. Exercise the remaining runtime checks listed above and
   investigate the abrupt sky handoff.
5. **Blocked pending gate 4:** expose a guarded
   `Create Custom Level (replaces Stone Hill)` editor workflow only after the
   complete candidate checklist passes.
6. Research a new 36th catalog/disc slot only after the complete Stone Hill
   replacement works. A 36th level requires new routing, level-table,
   executable, save/progression, WAD-growth, and runtime-dependency proofs; it is
   not implied by the replacement compiler.

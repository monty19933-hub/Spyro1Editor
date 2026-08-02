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

## Milestone 2: complete Town Square pair - runtime proven

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

The reported DuckStation sessions entered the Artisans portal labelled Stone
Hill, completed the Town Square fly-in with zero errors, collected gems,
rescued dragons, defeated enemies, collected the egg thief, used pause and
Inventory, survived death/reload, returned successfully through Return Home,
re-entered the replacement, and preserved progress across save/reload. Normal
movement remained stable and no errors were reported during those checks.

Title-demo playback completed without error and showed Doctor Shemp twice. This
is intentional: the candidate retires retail demo slot 0's Stone Hill metadata
by replacing its level ID (11), duration (860 frames), and 16-byte start pose
with exact copies of retail slot 1's Doctor Shemp metadata (level ID 24 and
1,100 frames). Retail slot 1 remains unchanged, so the resulting four-slot
cycle is Doctor Shemp, Doctor Shemp, Icy Flight, and Wizard Peak. This is a
cosmetic consequence of the safety reroute, not a stuck demo index or a new
runtime defect. No Town Square title demo has been authored or claimed.

The transition initially appeared to use Stone Hill's sky before abruptly
handing off to Town Square's sky, with the inverse handoff while flying out.
The candidate preserves Artisans entries 9/10 and its linked level-11 portal
transition data, so it does not replace Stone Hill's fly-in/out sky state. The
observed handoff is consistent with that preserved transition state yielding to
Town Square's own sky after the transplanted destination finishes loading. This
is a nonfatal visual transition defect, not evidence of a failed destination
load. Static capacity analysis reports that Town Square's corresponding linked
sky is 312 bytes larger than Stone Hill's fixed copy; growing that copy has not
been runtime-tested and remains a separate polish experiment.

The exact retail-pair transplant profile is runtime-proven for this checklist.
Its pre-playtest proof was static-only. After promotion, the guarded V5 artifact
writer emits `runtimeClaim: true` only when the saved intent resolves to this
code-owned profile and the generated BIN matches the exact runtime-proven
SHA-256 above; the currently generated proof beside the candidate therefore
describes the promoted profile. This section remains the durable record of the
user-observed runtime result. Edited or newly compiled donor payloads, arbitrary
donor levels, seamless transition-sky replacement, long-duration soak or broad
emulator-version coverage, normal V4 Create BIN, and a new 36th slot are not
implied.

## Milestone 3: Town Square display identity - pending runtime

The complete-pair candidate deliberately retained retail level/save slot 11, so
its portal, transition, guidebook, and Inventory name remained Stone Hill. The
two retail levels already have identical completion targets: 200 gems, four
dragons, and one egg. No completion-total patch is needed or permitted for this
pair.

The next disposable candidate starts from the exact runtime-proven Milestone 2
BIN and aliases only Stone Hill's indexed level-name pointer at SCUS file offset
`0x5FFF4` to Town Square's existing pointer from `0x5FFFC`. The shared name pool,
level ID 11, save/progression ownership, both completion-total rows, original
Town Square slot 13, WAD, terrain, collision, textures, scene, actors, music,
demo reroute, and transition sky are preserved.

- Directory: `town-square-display-identity-candidate`
- CUE: `Stone-Hill-slot-Town-Square-complete-level-with-Town-Square-display-name-RUNTIME-CANDIDATE.cue`
- BIN SHA-256:
  `71808a4b5e0d0891e4f6f49be8b2712de018a393695b1b606b79e9ecbd3166c9`
- Static diff proof: one logical SCUS byte, 31 physical bytes within one rebuilt
  MODE2 Form 1 sector, and zero changes outside that sector.
- Evidence status: `runtime-candidate-pending-duckstation`; `runtimeClaim` remains
  false until the generated checklist is completed in DuckStation.

Two visible Town Square entries are expected in this experiment. They retain
independent slot-11 and slot-13 save state. Use a fresh game or disposable memory
card because existing Stone Hill progress can legitimately carry into slot 11.

## Next implementation gates

1. **Completed:** inventory and source-bind all eight Stone Hill entry-12
   subfiles and the surrounding overlay/data pairs.
2. **Completed for an unchanged retail donor pair:** install Town Square's
   terrain, collision, textures, sky, scene, actors, and dragon packages in the
   fixed Stone Hill capacity. Arbitrary newly compiled payloads are not implied.
3. **Pending:** compile edited donor terrain/scene/object data and link imported
   actor packages instead of copying one unchanged retail pair.
4. **Completed for the exact retail pair:** the complete portal, gameplay,
   collection, reload, persistence, Return Home, re-entry, and title-demo
   checklist passed in DuckStation. Investigate the abrupt sky handoff as an
   isolated visual-polish candidate without changing the proven pair.
5. **Ready for guarded research integration:** expose the checked Town Square
   retail-pair profile as a separate V5 replacement-test workflow. Do not merge
   it into normal V4 Create BIN or imply that ordinary saved edits are already
   composed into the transplanted payload.
6. **Pending DuckStation:** prove the display-identity-only candidate without
   changing its slot-11 save ownership or the original Town Square slot.
7. Research a new 36th catalog/disc slot only after the complete Stone Hill
   replacement works. A 36th level requires new routing, level-table,
   executable, save/progression, WAD-growth, and runtime-dependency proofs; it is
   not implied by the replacement compiler.

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

## Milestone 3: Town Square display identity - runtime proven

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
- Evidence ID:
  `stonehill-slot-townsquare-display-identity-duckstation-2026-08-01`
- Evidence status: `runtime-proven-identity-profile-guarded` for this exact
  profile and BIN hash only. The recorded result is an interactive user report,
  not an automated emulator capture.

On 2026-08-01, the exact candidate above completed its focused DuckStation
checklist. The former Stone Hill portal, fly-in, guidebook, and Inventory all
displayed Town Square; the level showed the expected 200 gems, four dragons,
and one egg; normal gameplay worked; one gem, one dragon, and the egg each
incremented exactly once; death/reload, Return Home/re-entry, and save/reload
all worked; and retail Town Square remained independently accessible. Music
and transitions behaved the same as the already proven complete-pair
candidate.

The result does not change the candidate's known boundaries. Music remains
Stone Hill's, the brief Stone Hill/Town Square transition-sky handoff remains,
the safety-rerouted title cycle still contains Doctor Shemp twice, and this
second Town Square identity continues to own Stone Hill's slot-11 save state.
Those behaviors were expected and unchanged during the successful checklist;
they are not part of the display-name fix.

The exact evidence record is stored at
`docs/runtime-evidence/stonehill-townsquare-display-identity-2026-08-01.json`.
It binds the report to the profile recipe, runtime-proven Milestone 2 base,
executable pre/post hashes, output BIN hash, and one-sector diff boundary.

Two visible Town Square entries are expected in this experiment. They retain
independent slot-11 and slot-13 save state. Use a fresh game or disposable memory
card because existing Stone Hill progress can legitimately carry into slot 11.
This proof does not authorize arbitrary display names, other donor levels,
edited donor payload composition, seamless transition-sky replacement, music
replacement, normal V4 Create BIN, or a new 36th level.

## Milestone 4: edited Town Square T21 X - focused partial runtime result

The first edited-donor candidate starts from the exact Milestone 3
display-identity BIN and composes one saved Town Square native-object edit into
the transplanted payload. It changes only T21 Red Gem's X coordinate from
`7813.75` to `7685.75`, a movement of 128 world units toward negative X. The
disposable composer intentionally excluded the general object's derived
placement/culling-sector patch so this first runtime question remained limited
to the four-byte X word.

- CUE: `Stone-Hill-slot-Town-Square-edited-T21-X-RUNTIME-CANDIDATE.cue`
- BIN SHA-256:
  `ec3d8e354cf246d704860a6b26968a59cc7f77fe6409e08c299a777b7fc4df8e`
- Base display-identity BIN SHA-256:
  `71808a4b5e0d0891e4f6f49be8b2712de018a393695b1b606b79e9ecbd3166c9`
- Patch: Town Square donor WAD `0x136E8B4`, replacement-slot WAD
  `0xD640B4`, record `+0x0C`, `5C E8 01 00` to `5C E0 01 00`.
- Static boundary: one changed logical WAD byte, 37 changed physical image
  bytes, one rebuilt raw sector, and no changes to the retail donor or base BIN.
- Evidence ID:
  `stonehill-slot-townsquare-edited-donor-t21-x-partial-duckstation-2026-08-01`
- Evidence status: `focused-partial-runtime-observation`; this is not profile
  promotion. The result is an interactive user report, not an automated
  emulator capture.

On 2026-08-01, the user confirmed in DuckStation that T21 was visibly moved in
the exact candidate BIN above. That observation answers only the first focused
question: an editor-authored Town Square object-coordinate patch can survive
composition into the Stone Hill replacement payload and become visible at
runtime.

No collection or interaction result was reported for T21, and no full
replacement regression was performed for this candidate. One-time reward,
cleanup/persistence, other-actor isolation, ordinary movement and combat,
dragon and egg progress, death/reload, Return Home/re-entry, save/reload, and
the original retail Town Square remain unverified for this edited BIN. The
runtime sidecar therefore remains a pending checklist, and normal Create BIN is
not promoted by this observation.

The exact partial evidence record is stored at
`docs/runtime-evidence/stonehill-townsquare-edited-donor-t21-x-2026-08-01.json`.
It records both this deliberately isolated X-only result and the next focused
gate: compose the same X patch together with the genuine exporter-derived T21
placement/culling-sector byte at record `+0x4A`, donor WAD `0x136E8F2`,
replacement-slot WAD `0xD640F2`, from `FF` to `D5`. That X-plus-sector candidate
must receive its own DuckStation result; this X-only observation does not prove
the derived sector value.

## Next implementation gates

1. **Completed:** inventory and source-bind all eight Stone Hill entry-12
   subfiles and the surrounding overlay/data pairs.
2. **Completed for an unchanged retail donor pair:** install Town Square's
   terrain, collision, textures, sky, scene, actors, and dragon packages in the
   fixed Stone Hill capacity. Arbitrary newly compiled payloads are not implied.
3. **Focused partial result for one existing object:** Town Square T21's saved
   X edit was visibly present in DuckStation at BIN SHA-256
   `ec3d8e354cf246d704860a6b26968a59cc7f77fe6409e08c299a777b7fc4df8e`.
   Collection and the full replacement regression remain unverified, so this
   does not promote edited-donor composition. The next isolated gate must pair
   the same X word with the exporter-derived placement/culling-sector byte
   `FF` to `D5`, then validate visibility/culling, interaction, and the full
   checklist. Edited terrain/scene data, other object edits, additions,
   removals, and imported actor packages remain pending.
4. **Completed for the exact retail pair:** the complete portal, gameplay,
   collection, reload, persistence, Return Home, re-entry, and title-demo
   checklist passed in DuckStation. Investigate the abrupt sky handoff as an
   isolated visual-polish candidate without changing the proven pair.
5. **Ready for guarded research integration:** expose the checked Town Square
   retail-pair profile as a separate V5 replacement-test workflow. Do not merge
   it into normal V4 Create BIN or imply that ordinary saved edits are already
   composed into the transplanted payload.
6. **Completed for the exact display-identity profile:** portal, fly-in,
   guidebook, and Inventory naming; 200/4/1 totals; one-time collection;
   gameplay; death/reload; Return Home/re-entry; save/reload; and original Town
   Square independence passed in DuckStation at BIN SHA-256
   `71808a4b5e0d0891e4f6f49be8b2712de018a393695b1b606b79e9ecbd3166c9`.
   Stone Hill music, the transition-sky handoff, duplicate Doctor Shemp demo,
   and slot-11 save ownership remain expected limitations. This gate does not
   promote any other identity recipe.
7. Research a new 36th catalog/disc slot only after the complete Stone Hill
   replacement works. A 36th level requires new routing, level-table,
   executable, save/progression, WAD-growth, and runtime-dependency proofs; it is
   not implied by the replacement compiler.

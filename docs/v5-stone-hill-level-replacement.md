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
disposable composer intentionally excluded the then-editor-derived `+0x4A`
terrain-sector write so this first runtime question remained limited to the
four-byte X word. Later disassembly identified `+0x4A` as a visibility sentinel,
not terrain ownership.

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
It records this deliberately isolated X-only result and the then-pending
`FF`-to-`D5` experiment at record `+0x4A`. That experiment has now received an
independent DuckStation result and was rejected in Milestone 5 below.
The later distance-focused pass used a renamed, byte-identical copy of this
same X-only BIN and rejected the profile for continued far flicker; Milestone 6
and its separate evidence record supersede this initial partial observation.

## Milestone 5: T21 X plus `+0x4A D5` - runtime rejected

The follow-up candidate paired the same T21 X edit with the geometry resolver's
sector result, decimal 213 (`D5`), at donor WAD `0x136E8F2` and replacement-slot
WAD `0xD640F2`. Static preimage, relocation, readback, source-preservation, and
raw-sector-boundary checks passed, but the exact candidate failed its focused
runtime behavior gate.

- CUE:
  `Stone-Hill-slot-Town-Square-edited-T21-X-and-placement-sector-RUNTIME-CANDIDATE.cue`
- BIN SHA-256:
  `034ace2340dce2e71bbfd41df832d984dfebbe330dba6b0325d198cdf8ac0b81`
- Base display-identity BIN SHA-256:
  `71808a4b5e0d0891e4f6f49be8b2712de018a393695b1b606b79e9ecbd3166c9`
- T21 X patch: donor WAD `0x136E8B4`, replacement-slot WAD `0xD640B4`,
  record `+0x0C`, `5C E8 01 00` to `5C E0 01 00`.
- Rejected `+0x4A` patch: donor WAD `0x136E8F2`, replacement-slot WAD
  `0xD640F2`, `FF` to `D5`.
- Static boundary: two changed logical WAD bytes, 40 changed physical image
  bytes, one rebuilt raw sector, and no changes to the retail donor or base BIN.
- Evidence ID:
  `stonehill-slot-townsquare-edited-donor-t21-x-d5-rejected-duckstation-2026-08-01`
- Evidence status: `runtime-rejected-visibility-regression`; profile promotion
  is not authorized. The result is an interactive user report, not an automated
  emulator capture.

On 2026-08-01, the user confirmed in DuckStation that T21 moved and could be
collected exactly once. However, T21 flickered at distance and became stable
only when Spyro moved close. That visibility regression rejects this exact BIN
despite the successful movement and one-time collection observations.

The byte interpretation also has independent structural support. A direct
census of the clean USA Town Square source table found `FF` at record `+0x4A`
in all 107 native rows. The primary Spyro 1 reverse-engineering Moby layout names
that byte `visable` and documents `FF` as visible; the next byte is separately
shadow-related. The pinned source is
[`include/moby.h` lines 150-204 at commit 25d0faaa1cf6cf0d9d918d2a4e80dd07726cc9d3](https://github.com/c0mposer/spyro1-reverse-engineering/blob/25d0faaa1cf6cf0d9d918d2a4e80dd07726cc9d3/include/moby.h#L150-L204).
Its field layout places `visable` at `+0x4A`.

`D5` remains the terrain geometry sector returned for the edited coordinates;
that does not make it a valid Moby visibility-byte value. The next corrective
gate therefore had to preserve native `FF` and apply only the T21 X patch while
explicitly checking visibility from distance. Milestone 6 records that exact
gate and its independent runtime rejection.

The exact rejection record is stored at
`docs/runtime-evidence/stonehill-townsquare-edited-donor-t21-x-d5-rejected-2026-08-01.json`.

## Milestone 6: T21 X with native `+0x4A FF` - runtime rejected

The corrective candidate returned to the exact X-only recipe and preserved the
complete retail T21 record except for the requested X word. Its output is
byte-identical to the original Milestone 4 X-only BIN; the `FF-visible` name
makes the corrected test boundary explicit but does not create a different disc
payload.

- CUE:
  `Stone-Hill-slot-Town-Square-edited-T21-X-FF-visible-RUNTIME-CANDIDATE.cue`
- BIN SHA-256:
  `ec3d8e354cf246d704860a6b26968a59cc7f77fe6409e08c299a777b7fc4df8e`
- Base display-identity BIN SHA-256:
  `71808a4b5e0d0891e4f6f49be8b2712de018a393695b1b606b79e9ecbd3166c9`
- T21 X patch: donor WAD `0x136E8B4`, replacement-slot WAD `0xD640B4`,
  record `+0x0C`, `5C E8 01 00` to `5C E0 01 00`.
- Preserved native fields: pod `+0x43 = FF`, visibility-sector sentinel
  `+0x4A/+0x4B = FF/00`, and render radius `+0x50 = 18`.
- Evidence ID:
  `stonehill-slot-townsquare-edited-donor-t21-x-native-ff-rejected-duckstation-2026-08-01`
- Evidence status: `runtime-rejected-visibility-regression`; profile promotion
  is not authorized. The result is an interactive user report, not an automated
  emulator capture.

On 2026-08-01, the user confirmed in DuckStation that T21 still flickered from
afar in this exact native-`FF` candidate. The result rejects the X-only edited-
donor profile. Preserving `+0x4A = FF` removed the invalid `D5` overwrite but was
not sufficient to restore stable distant rendering.

An independent byte audit found no alternate disc corruption path. Relative to
the exact display-identity base, all 37 changed physical bytes are confined to
raw sector `0x1AED`: one intended user-data byte at image offset `0xF7623D`
(`E8` to `E0` inside the T21 X word), four regenerated EDC bytes, six ECC-P
bytes, and 26 ECC-Q bytes. The retail donor record, identity donor record, and
identity target record match across all `0x58` bytes before the edit. The output
donor remains retail-identical, while the output target differs only at record
`+0x0D`. No WAD growth, extent relocation, executable change, pointer fixup, or
unrelated raw-sector change occurred.

The currently decoded optional scene-list table provides no spatial owner to
patch: its pointer and count are both zero. This is a static disassembly/census
result, not a general proof that no other runtime visibility mechanism exists.
T21's native pod byte is `+0x43 = FF`. Its original and edited fixed-point
coordinates both resolve to collision cell `(15,14)`, so the 128-world-unit X
move did not cross a collision-cell boundary. The edited X coordinate is only
5.75 world units above that cell's lower X boundary, but the collision chain is
not itself proof of the distant render failure.

The next isolated diagnostic keeps the exact X patch and native `+0x4A = FF`,
then changes only T21's clipping/render-radius byte at `+0x50` from `18` to
`20` (donor WAD `0x136E8F8`, replacement-slot WAD `0xD640F8`). This is a
disposable diagnostic, not a proposed general export rule. It must reproduce
the same far-camera test, close approach, one-time collection and cleanup, and
nearby-actor checks before any further conclusion.

The exact native-`FF` rejection record is stored at
`docs/runtime-evidence/stonehill-townsquare-edited-donor-t21-x-native-ff-rejected-2026-08-01.json`.

## Milestone 7: isolated T21 render-radius `20` candidate - runtime rejected

This candidate was generated from the exact display-identity base. It
keeps the proven T21 X edit, preserves native `+0x4A/+0x4B = FF/00` and
`+0x52/+0x53 = 40/FF`, and changes only render radius `+0x50` from the native
loose-gem value `18` to value `20`, which other retail Town Square actor
families use. It is not asserted to be a native loose-gem value.

- CUE:
  `Stone-Hill-slot-Town-Square-edited-T21-X-render-radius-20-RUNTIME-CANDIDATE.cue`
- BIN SHA-256:
  `329420e7f9e492ce69830f63c783421c05c5976fe8b0b04de7d49e52bf87e626`
- Base display-identity BIN SHA-256:
  `71808a4b5e0d0891e4f6f49be8b2712de018a393695b1b606b79e9ecbd3166c9`
- X patch: donor WAD `0x136E8B4`, replacement-slot WAD `0xD640B4`,
  `5C E8 01 00` to `5C E0 01 00`.
- Render-radius patch: donor WAD `0x136E8F8`, replacement-slot WAD
  `0xD640F8`, `18` to `20`.
- Exact diff boundary: two logical WAD bytes and 46 physical BIN bytes in one
  rebuilt MODE2 raw sector; the other 44 bytes are regenerated EDC/ECC.
- Evidence ID:
  `stonehill-slot-townsquare-edited-donor-t21-x-render-radius-pending-duckstation`
- Runtime result (2026-08-03): rejected. The user confirmed that T21 still
  flickered, but only after moving farther away. The patch therefore moved the
  same distance boundary rather than correcting a relocation defect.
- Evidence status: runtime rejected; no profile promotion is authorized.

The focused smoke rejects any alternate radius, secondary patch, unrelated
record, or out-of-range write. It also proves deterministic output, original
Town Square donor preservation, exact target readback, executable preservation,
source preservation, and atomic BIN/CUE publication. The required discriminator
is the same far-camera view that reproduced the rejection. If practical, compare
nearby native T22 from that view, then approach and collect moved T21 exactly
once. The exact rejection record is stored at
`docs/runtime-evidence/stonehill-townsquare-edited-donor-t21-x-render-radius-20-rejected-2026-08-03.json`.

Disassembly explains the observation directly. `RenderShadedMobys` loads
`+0x50/+0x51` at `0x80022B20`. The signed low byte supplies a radius of
`value * 64` editor world units, and the previous-frame bit in `+0x51` adds
128 units of hysteresis. The renderer clears that bit at `0x80022B28` and sets
it again only after every clipping gate succeeds at `0x80022D98`. Thus `18`
gives 1536 units, `20` gives 2048 units, and a camera hovering at either
boundary can naturally alternate the object between drawn and culled frames.

## Milestone 8: maximum-positive T21 render-radius discriminator - runtime rejected

The narrow next candidate changes the same single radius byte from `18` to
`7F`, the largest value that stays on the normal world-render path. Values
`80` through `FF` are explicitly blocked because their signed high bit enters
the renderer's special screen/HUD path. T21 X remains the only position edit;
native `+0x43 = FF`, `+0x4A/+0x4B = FF/00`, ephemeral `+0x51 = 00`, and the
separate `+0x52/+0x53 = 40/FF` update controls remain untouched.

- CUE:
  `Stone-Hill-slot-Town-Square-edited-T21-X-render-radius-7F-RUNTIME-CANDIDATE.cue`
- BIN SHA-256:
  `a3db572356470e697c643e474728b5e75a73fa813fe9868143fb2ea6a4a98f36`
- Render-radius patch: donor WAD `0x136E8F8`, replacement-slot WAD
  `0xD640F8`, `18` to `7F`.
- Exact diff boundary: two logical WAD bytes and 46 physical BIN bytes in one
  rebuilt MODE2 raw sector; the other 44 bytes are regenerated EDC/ECC.
- Evidence ID:
  `stonehill-slot-townsquare-edited-donor-t21-x-render-radius-7f-pending-duckstation`
- Runtime result (2026-08-03): rejected. The user reported that T21 still
  flickered with `+0x50 = 7F`.
- Evidence status: runtime rejected; no profile promotion is authorized.

The maximum-radius test did not eliminate the flicker, so no further `+0x50`
probe is valid. The exact rejection record is stored at
`docs/runtime-evidence/stonehill-townsquare-edited-donor-t21-x-render-radius-7f-rejected-2026-08-03.json`.

## Milestone 9: exclude both decoded distance gates - runtime rejected

No higher `+0x50` value is safe: `7F` is the maximum positive world radius and
`80` through `FF` enter a different renderer. The next checked candidate keeps
the T21 X edit and `+0x50 = 7F`, then changes only the separate scheduling byte
at `+0x52` from native loose-gem `40` to `FF`. The queue builder treats signed
`FF` as unconditional scheduling, and three retail Town Square objects already
use that value. T47 (green gem) and T22 (red gem) remain native matched controls.

- CUE:
  `Stone-Hill-slot-Town-Square-edited-T21-X-render-radius-7F-update-FF-RUNTIME-CANDIDATE.cue`
- BIN SHA-256:
  `580af811c2f3e130f03fc1556da56d8310064a588eb57f819f5129c74ccc9329`
- X patch: donor WAD `0x136E8B4`, replacement-slot WAD `0xD640B4`,
  `5CE80100` to `5CE00100`.
- Render-radius patch: donor WAD `0x136E8F8`, replacement-slot WAD
  `0xD640F8`, `18` to `7F`.
- Update-scheduling patch: donor WAD `0x136E8FA`, replacement-slot WAD
  `0xD640FA`, `40` to `FF`.
- Exact diff boundary: three logical WAD bytes and 53 physical BIN bytes in
  exactly one rebuilt MODE2 raw sector.
- Evidence ID:
  `stonehill-slot-townsquare-edited-donor-t21-x-render-radius-7f-update-ff-rejected-duckstation-2026-08-03`
- Runtime result (2026-08-03): rejected. The user reported that T21 still
  flickered from afar with both `+0x50 = 7F` and `+0x52 = FF`.
- Evidence status: runtime rejected; no profile promotion is authorized.

The follow-up live DuckStation debugger trace sampled T21's `+0x51`
renderer-admitted draw-attempt state for 60 frames at the failing distant view.
T21 reached that state in all 60 samples. This proves that T21 was neither
omitted from the update/draw queue nor rejected by the coarse sphere/frustum
gates. It does not by itself prove visible pixels because `+0x51` is set before
vertex projection, face clipping, ordering-table selection, and primitive
packet emission. Row-field probes end here, and no further guessed Moby byte
changes are authorized.

The exact rejection and trace record is stored at
`docs/runtime-evidence/stonehill-townsquare-edited-donor-t21-x-render-radius-7f-update-ff-rejected-2026-08-03.json`.

## Milestone 10: isolate the wall-intersection cause - safe candidate pending runtime

A second live trace followed moved T21 beyond `+0x51` through the shaded-model
renderer's primitive cursor. T21 emitted GPU packets in all 12 sampled frames,
with packet sizes `132`, `172`, `192`, and `208` bytes. Native red T22 and
green T47 also emitted packets in all 12 matched samples and showed overlapping
packet-size variation as their gem models rotated. The visible T21 collapse is
therefore not whole-model clipping, all-face rejection, or missing GPU packets.

Frame-by-frame crops show the difference: native controls remain 8-15 pixels
tall while rotating, but moved T21 alternates between a 1-2 pixel dark sliver
and a bright white X. The decoded spatial cause is concrete. The rejected
coordinate `(7685.75, 7189.75, 512)` is only `9.25` units from Town Square
sector 213 face 110, a vertical wall spanning X `7652..7770`, Y `7199`, and Z
`512..672`. A loose gem has native radius `0x18 = 24`, so the test coordinate
penetrates the wall envelope by `14.75` units. Spyro 1 painter-sorts both the
rotating gem and wall into the world ordering table without a z-buffer; the
gem is emitted every frame but is alternately overdrawn as its face depths and
silhouette rotate.

The corrected discriminator removes both rejected row-field diagnostics,
preserves native `+0x50 = 18` and `+0x52 = 40`, and changes only T21 X to open
ground at `7600`:

- CUE:
  `Stone-Hill-slot-Town-Square-edited-T21-X-safe-open-ground-RUNTIME-CANDIDATE.cue`
- BIN SHA-256:
  `d6dd17bfd0a374ff7a9bb6aa7966846d0471b98ef15a6814331dcc452f81dd92`
- X patch: donor WAD `0x136E8B4`, replacement-slot WAD `0xD640B4`,
  `5CE80100` to `00DB0100` (world X `7813.75` to `7600`).
- Exact diff boundary: two logical WAD bytes and 41 physical BIN bytes in one
  rebuilt MODE2 raw sector; deterministic re-export matched.
- Spatial boundary: same Z `512`, collision group `1`, decoded nearest-wall
  clearance `31.699`, and nearest-Moby two-radius clearance about `30.57`.
- Evidence ID:
  `stonehill-slot-townsquare-edited-donor-t21-safe-open-ground-pending-duckstation-2026-08-03`
- Evidence status: static and spatial proof only; DuckStation runtime remains
  required before edited-object promotion.

The pending evidence record is stored at
`docs/runtime-evidence/stonehill-townsquare-edited-donor-t21-safe-open-ground-pending-2026-08-03.json`.

## Next implementation gates

1. **Completed:** inventory and source-bind all eight Stone Hill entry-12
   subfiles and the surrounding overlay/data pairs.
2. **Completed for an unchanged retail donor pair:** install Town Square's
   terrain, collision, textures, sky, scene, actors, and dragon packages in the
   fixed Stone Hill capacity. Arbitrary newly compiled payloads are not implied.
3. **Runtime-rejected edited object:** Town Square T21's saved X edit was
   visibly present, but both the X-plus-`D5` candidate at BIN SHA-256
   `034ace2340dce2e71bbfd41df832d984dfebbe330dba6b0325d198cdf8ac0b81`
   and the corrected X-only native-`FF` candidate at BIN SHA-256
   `ec3d8e354cf246d704860a6b26968a59cc7f77fe6409e08c299a777b7fc4df8e`
   flickered at distance. The byte-clean native-`FF` result rules out the
   earlier invalid sector overwrite as the sole cause. The isolated `+0x50`
   `18`-to-`20` diagnostic at BIN SHA-256
   `329420e7f9e492ce69830f63c783421c05c5976fe8b0b04de7d49e52bf87e626`
   moved the native flicker boundary farther away and is runtime-rejected. The
   maximum-positive `7F` discriminator at BIN SHA-256
   `a3db572356470e697c643e474728b5e75a73fa813fe9868143fb2ea6a4a98f36`
   also flickered and is runtime-rejected. The final row-field discriminator
   excludes the separate `+0x52` scheduling gate at BIN SHA-256
   `580af811c2f3e130f03fc1556da56d8310064a588eb57f819f5129c74ccc9329`
   and is also runtime-rejected. A live 60-frame debugger sample reached
   T21's `+0x51` renderer-admitted state in every frame, and a downstream
   12-frame trace recorded nonzero primitive packets every frame. Spatial
   decoding then found that the rejected X position penetrates a vertical
   wall's radius envelope by 14.75 units. The corrected native-field,
   open-ground X-only candidate at SHA-256
   `d6dd17bfd0a374ff7a9bb6aa7966846d0471b98ef15a6814331dcc452f81dd92`
   is pending DuckStation. Edited
   terrain/scene data, other object edits, additions, removals, and imported
   actor packages remain pending.
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

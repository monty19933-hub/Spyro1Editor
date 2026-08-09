# Unused level 65 bootstrap — static audit

Date: 2026-08-03
Source: exact clean USA BIN SHA-256 `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`

## Result

Spyro 1 reserves a complete thirty-sixth level directory row even though retail ships only thirty-five levels. Level ID 65 maps to continuous level index 35 and reads the sixteen-byte WAD directory row at `WAD+0x278`: overlay entry 79 and data entry 80. Both entries are zero in the clean USA disc. The following credits data starts separately at `WAD+0x288` and is not part of the level row.

The first runtime discriminator fills entries 79/80 with aliases to Town Square's already checked retail pair. It does not replace a retail level, grow the WAD, or change any retail payload bytes:

- Entry 79: Town Square overlay `0x118E800`, length `0xF800`.
- Entry 80: Town Square data `0x119E000`, length `0x2E2000`.
- SCUS `0x1CA8`: ID-65 callback pointer `0x8005B6E0 -> 0x8005A898` (Town Square).
- SCUS `0x1DF44`: hidden Inventory level-warp upper bound `0x37 -> 0x38`, admitting ID 65.
- SCUS `0x1E034`: enable the existing hidden Inventory level-warp handler.

The loader and callback dispatch each have a single applicable ID-65 path. The callback table accepts IDs below 100; no second overlay-dispatch table was found. Within WAD and SCUS, logical readback proves that only the reserved WAD row and three guarded executable fields changed. The ISO root directory is byte-identical, and Mode-2 EDC/ECC was rebuilt for every affected raw sector.

## First runtime result and corrected hypothesis

The native-index-35 alias candidate froze on the black entry transition before its first rendered level frame. A read-only DuckStation GDB inspection found an Address Error Load (`Cause 0x00000410`) at bad virtual address `0x40000827`. The in-memory level-35 loader row at `0x8007A948` exactly matched Town Square's level-3 row at `0x8007A748`, proving that reserved WAD rows 79/80 reached RAM correctly. The WAD reader accepts duplicate aligned offset/length pairs, and the ID65 dispatch points at the exact Town Square case block, so a physical payload copy would not isolate this failure.

The stronger pre-frame cause is retail flight classification. During loader state 13, `function_8001364C` treats every level ID whose decimal units digit is `5` as a flight. ID65 therefore enters flight initialization, sets the flight flag, selects movement state `0x20`, and reaches flight-only runtime paths even though its aliased Town Square overlay supplies normal-level callbacks.

The second runtime discriminator keeps ID65, world ID 5, continuous index 35, rows 79/80, and the Town Square dispatch unchanged. The classifier's private divide-by-ten magic is loaded at SCUS `+0x40C0/+0x40CC`; changing only the LUI at `+0x40C0` from `66 66 02 3C` to `00 5E 02 3C` changes the combined constant from `0x66666667` to `0x5E006667`. Exhaustive integer evaluation over IDs 0 through 99 proves that exactly the retail flight IDs 5/15/25/35/45/55 remain flights. ID65 is the only supported level ID affected; unused IDs 75/85/95 also take normal initialization. This avoids a code cave and preserves every retail flight during the focused test. Neither superseded diagnostic is combined with it: SCUS `+0x40F4` retains retail bytes `29 00 44 14`, and `+0x5E58` retains retail bytes `21 10 65 00`.

## Second runtime result

The exact flight-classifier exception candidate at BIN SHA-256 `a77f32d715b614867958a491e25407b24fe0091509ad9eeee546bf3c70cc09e5` passed its focused DuckStation test on 2026-08-03. ID65 rendered Town Square and supported gem collection instead of freezing on entry. The user then loaded the untouched retail Town Square and Gnasty's Loot entries and confirmed that Sunny Flight still entered normally. This confirms the prior crash was the flight-classification mismatch rather than the aliased WAD row or callback dispatch.

The evidence record is `docs/runtime-evidence/unused-level-65-town-square-flight65-exception-focused-pass-2026-08-03.json`. This focused pass does not promote ID65 into the editor: its rows still alias Town Square's physical bytes, its Inventory identity remains placeholder `A`, and its portals, totals, save ownership, and persistence remain deliberately unimplemented.

## Deliberate phase-one limits

- This is a static-proven, runtime-pending research CUE, not an editor feature.
- Rows 79/80 have their own directory identity but alias Town Square's physical bytes.
- The level-name pointer remains the retail placeholder `A`.
- Memory-card insertion must be disabled. Retail save/load code is intentionally untouched and can serialize current level ID 65 plus slot-35 state.
- Do not attack or kill enemies, collect anything, rescue dragons, touch the egg thief, open/break chests, interact with gameplay objects, die/respawn, use a balloonist/save prompt, save, or use Return Home in the first cold-load probe.
- Pause and Inventory may be opened and closed, but do not select `Exit Level` or `Quit Game`; those are separate unproven transitions for portal-to-exit 65.
- Town Square's Return Home would target Gnasty's World and record portal-to-exit 65, but Gnasty's World has no matching portal-65 landing object.
- A normal portal transition has a separate retail `<65` guard at SCUS `0x47910`; the hidden Inventory warp bypasses it. A later portal-addressable candidate must change that guard to `<66` under its own preimage check.
- The initial music table has an index-35 row, but the late alternate music table has only thirty-five rows. Extended-session music remains unproven.

## Third candidate: physically independent payload

After the non-flight control passed, the next candidate copied the Town Square
pair into independent physical storage rather than aliasing the retail pair:

- Entry 79 overlay at `0x6927000`, length `0xF800`.
- Entry 80 data at `0x6936800`, length `0x2E2000`.
- WAD size `0x6927000 -> 0x6C18800` (`+0x2F1800`, 1507 sectors).
- Relocate `SCUS_942.28` from LBA 53875 to 55382; it ends at 55586, before `PETEXA0` at LBA 60000.
- Move the WAD XA EOR/EOF marker from old last sector LBA 53874 to new last sector LBA 55381. Relocated SCUS keeps its native final marker at LBA 55585.
- Patch both-endian ISO root fields and rebuild Mode-2 EDC/ECC for the header, former WAD boundary, append, relocated executable, and root-directory sectors.

The exporter first derives the exact runtime-proven alias BIN, copies SCUS before
overwriting its old extent, initializes the former Form-2 padding with complete
Form-1 sectors, rewrites every destination MSF address, and rebuilds EDC/ECC-P/Q.
The final BIN is fail-closed on SHA-256
`f585e45ff1d795f8b2de64f20e1ed2953bfc7f43adf1c0b47b03e849e4865e48`.
Static readback proves:

- Exactly 1,507 Town Square payload sectors and 204 patched SCUS sectors were copied.
- Rows 79/80 contain `00 70 92 06 00 F8 00 00 00 68 93 06 00 20 2E 00` and point only to the new payload.
- The original Town Square payloads remain byte-identical.
- The ISO root exposes WAD size `0x6C18800` and SCUS LBA 55382.
- Exactly 1,714 raw sectors differ from clean retail, matching the complete checked allowlist.
- Every changed sector has a duplicated Form-1 subheader, correct destination MSF, and valid EDC/ECC.
- The original clean source and total raw image length remain unchanged.

The generated handoff is under
`_local/v5-stone-hill-level-replacement/unused-level-65-physical-clone/`.
It kept memory cards disabled and tested only ID65 load/movement, one loose gem
with reset/re-entry, retail Town Square, Gnasty's Loot, and Sunny Flight.

## Third runtime result

The exact physically independent candidate at BIN SHA-256
`f585e45ff1d795f8b2de64f20e1ed2953bfc7f43adf1c0b47b03e849e4865e48`
passed its focused DuckStation checklist on 2026-08-08. The user confirmed that
every requested check passed: ID65 loaded and remained responsive across
movement and streaming through several sectors, camera, initial music, enemies,
pause and Inventory, and one-gem collection. After the no-card reset and
re-entry, the gem was present again. Retail Town Square, Gnasty's Loot, and
Sunny Flight also continued to load normally, including Sunny Flight's controls
and timer. The supplied Inventory screenshot displayed the expected placeholder
`A`, confirming that display identity was deliberately unchanged.

The evidence record is
`docs/runtime-evidence/unused-level-65-town-square-physical-clone-focused-pass-2026-08-08.json`.
This records only the physical-storage discriminator as a focused runtime pass.
It does not authorize editor integration, an authored name or totals, music-table
extension, portal-to-exit 65, Return Home, saving, memory-card persistence, or
arbitrary level authoring.

## Fourth candidate: authored display-name assignment

The next isolated level-format gate was built as a static-proven, runtime-pending
candidate. It starts from the exact focused-runtime-passed physical clone at
BIN SHA-256
`f585e45ff1d795f8b2de64f20e1ed2953bfc7f43adf1c0b47b03e849e4865e48`
and changes only continuous level-name table slot 35:

- SCUS name-pointer table: `0x5FFF0`.
- ID65/continuous-index-35 pointer: SCUS `0x6007C`.
- Guarded preimage: `64 55 07 80`, pointer `0x80075564` to placeholder `A`.
- Replacement: `E4 01 01 80`, the unchanged retail Town Square pointer
  `0x800101E4` from slot 3.
- The placeholder `A`, retail `TOWN SQUARE`, every other name pointer/string,
  and every non-identity executable byte remain unchanged.

The relocated SCUS remains at LBA 55382 with size `0x66000`. The pointer write
changes exactly three logical executable bytes, and rebuilding MODE2 Form 1
sector LBA 55574 changes 53 physical bytes inside that one raw sector. Static
readback verifies correct duplicated subheaders, MSF, EDC/ECC, the complete
patched executable, and a whole-BIN diff with zero bytes outside that sector.
The passed base BIN is preserved byte-for-byte.

- Profile:
  `unused-level-65-town-square-display-name-clean-usa-disposable-v4`.
- Output BIN SHA-256:
  `9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8`.
- Output SCUS SHA-256:
  `fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442`.
- Generated directory:
  `_local/v5-stone-hill-level-replacement/unused-level-65-display-name/`.

Run the deterministic static export with:

```sh
dotnet run \
  --project src/Spyro.Editor.UnusedLevel65DisplayNameCandidateSmoke/Spyro.Editor.UnusedLevel65DisplayNameCandidateSmoke.csproj \
  --configuration Release -- "$PWD"
```

## Disposable test handoff

Each active ID65 smoke export now publishes an executable
`*-Reveal-in-Finder.command` beside its CUE. The helper resolves the CUE relative
to itself and runs `/usr/bin/open -R` so Finder selects the exact CUE rather than
only opening its containing directory. Generation verifies that the CUE points
to its same-prefix BIN, applies mode `0755` before atomic publication, and leaves
the game image unchanged.

Each adjacent runtime checklist repeats the complete controller input for every
level used by the focused test:

- ID65: `Select; R1, R2, L1, L2, R1, L1, R2, L2; Left, Down`.
- Gnasty's World prerequisite (ID60):
  `Select; R1, R2, L1, L2, R1, L1, R2, L2; Left, Circle`.
- Dream Weavers prerequisite (ID50):
  `Select; R1, R2, L1, L2, R1, L1, R2, L2; Down, Circle`.
- Retail Town Square (ID13): `Select; R1, R2, L1, L2, R1, L1, R2, L2; Cross, Triangle`.
- Gnasty's Loot (ID64): `Select; R1, R2, L1, L2, R1, L1, R2, L2; Left, Right`.
- Sunny Flight (ID15): `Select; R1, R2, L1, L2, R1, L1, R2, L2; Cross, Down`.

These Finder and checklist files are research sidecars only. They do not enter
normal Create BIN, editor workspace, release, or update-channel paths.

The generated checklist keeps every DuckStation cheat and memory-card insertion
disabled. It checks the new Town Square display identity, a short ID65 load/reset
pass, the Gnasty's World ID60 to Dream Weavers ID50 to ID65 bidirectional
Inventory control, retail Town Square, Gnasty's Loot, and Sunny Flight. Authored
totals, music-table extension, portal routing, Return Home, save ownership,
content mutation, editor integration, normal Create BIN, and release/update paths
remain excluded. The exact BIN now passes that focused checklist, but this result
remains research-only and does not authorize promotion into normal Create BIN,
editor integration, release, or update paths.

## Fourth runtime observation: focused display-name pass

The exact display-name candidate at BIN SHA-256
`9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8`
now has a focused DuckStation runtime pass. ID65 loaded and displayed Town Square
instead of A; movement, camera, collision, enemies, pause, and Inventory opening
remained stable; reset/re-entry retained the Town Square identity; and retail
Town Square, Gnasty's Loot, and Sunny Flight each loaded.

The user also reported Gnasty's Loot music, a `4/0` treasure display, and no
apparent homeworld-page cycling. These observations match deliberately unchanged
or progression-gated state rather than the three-byte name-pointer write:

- Initial-music slot 35 at SCUS `0x5F828` remains track 19, Gnasty's Loot.
  Town Square is track 26. The candidate intentionally did not change music.
- Treasure-target slot 35 at SCUS `0x5FC7E` remains `00 00`, so the `/0`
  denominator is expected. The numerator 4 is recorded without interpreting it
  as authored totals or proof that the same loose gem returned after reset.
- Read-only live RAM inspection found `g_LevelCheatActive` at `0x80075880` had
  returned to zero. Inventory page changes were gated by visited-homeworld flags;
  Dream Weavers home index 24 was still unvisited in the current runtime state,
  so Left from the Gnasty/ID65 page was rejected normally.

The runtime environment was not clean for progression evidence. DuckStation's
SCUS-94228 game settings had Moon Jump, over 65,000 jewels, all dragon eggs, and
all dragons freed enabled, and live RAM corroborated the active progression
values. The display name and basic loading/responsiveness observations remain
useful, but gem/reset and totals semantics from this run are contaminated. That
original run's visited-homeworld state made its page-cycling report
non-diagnostic. No emulator setting was changed during inspection.
The exact-run memory-card state was not captured; the current global DuckStation
configuration uses a per-game card and a Spyro card file exists, so the no-card
check must also be explicitly repeated rather than inferred.

With DuckStation cheats confirmed inactive, a later same-session control entered
Dream Weavers and then ID65. Exact memory-card insertion, cold-boot, and resumed-
save-state conditions were not independently captured for that follow-up. D-pad
Left successfully moved from the ID65 page to Dream Weavers, but D-pad Right did
not return to Gnasty's World. Read-only live RAM inspection showed exactly
visited indices 0, 24, and 35: Dream Weavers HOME index 24 was visited, while
Gnasty's World HOME index 30 was not. Retail level load marks only the current
continuous index, so loading child ID65 marks index 35 and does not synthesize
the page's HOME index 30. The one-way result is therefore expected visited-state
gating, not evidence of a page-cycling regression.

The complete no-write Inventory control was to enter Gnasty's World ID60 with
`Select; R1, R2, L1, L2, R1, L1, R2, L2; Left, Circle`, enter Dream Weavers ID50
with `Select; R1, R2, L1, L2, R1, L1, R2, L2; Down, Circle`, and then enter ID65
with `Select; R1, R2, L1, L2, R1, L1, R2, L2; Left, Down`, without resetting
between entries. D-pad Left in Inventory should reach Dream Weavers; after its
page transition finishes, D-pad Right should return to the now-visited Gnasty
page.

The user subsequently reported, "Ok looks like everything here works, including
the left and right cycling of the inventory." This explicitly closes the prepared
bidirectional page control: with both adjacent HOME rows visited, Left reached
Dream Weavers and Right returned to Gnasty's World. The result matches the retail
visited-page gates and does not indicate an Inventory regression from the name
pointer change. Read-only live RAM inspection corroborated visited indices 0, 24,
30, and 35, continuous index 35, Inventory page 5, and a released hidden-cheat
input state; live gem, dragon, and egg values were zero, consistent with inactive
progression cheats. This does not by itself confirm any separate reset/cold-boot
gem restoration, no-resumed-state condition, or memory-card condition.

In the same cheat-disabled follow-up, collecting one red gem produced `1/0`.
After leaving and re-entering ID65 without a reset, that gem remained absent.
This is expected same-session object retirement against the unchanged zero target;
it does not by itself test reset restoration or save ownership. In a later
explicit clarification, the user confirmed that the separate control used an
actual reset/cold boot with DuckStation cheats and memory-card insertion disabled,
showed `0/0` before recollection, and restored the same red gem physically. This
closes clean reset/cold-boot loose-gem restoration, but it does not establish save
ownership, memory-card persistence, or authored totals.

The user's umbrella report that everything in the active generated checklist
worked also closes its remaining rendering and comparison checks: ID65 geometry
and textures remained normal, Sunny Flight retained normal controls and timer,
and every prohibited action in checklist item 9 was avoided. Taken together, the
original load report and the later clean controls pass every focused item. The
initial contaminated `4/0` observation remains historical diagnostic context
only and does not weaken the later clean `0/0` reset result.

The evidence record is
`docs/runtime-evidence/unused-level-65-town-square-display-name-focused-pass-2026-08-08.json`.
Its status is `focused-runtime-pass`, but `promotionAuthorized` remains false.
This closes only the exact display-name discriminator: music ownership and late
music, authored totals, portals, Return Home, saving and memory-card persistence,
content authoring, normal Create BIN, editor integration, and release remain
separate and unauthorized.

## Fifth experiment: first authored collidable terrain in ID65

The next isolated candidate layers one editor-authored terrain copy onto the
exact focused-runtime-passed display-name BIN. ID65 remains a research-only
`LevelDefinition` bound to independent WAD data row 80; it is not added to the
retail catalog, normal Create BIN, editor integration, release, or update paths.

The editor recipe copies native Town Square face `201:0:hp` with texture 27,
keeps the original face, allocates three independent HP vertices, and displaces
the copy by `+64 X`, `+24 Y`, with per-vertex Z offsets `+10`, `+13`, and `+16`.
The authored vertices are `(8616,8196,746)`, `(8625,8234,749)`, and
`(8540,8138,752)`. The cloned native face references are exactly
`2B 2B 2C 2D`; all four raw slots, including the triangle's repeated slot, now
point only to the three new vertices.

The source-derived collision planner now decodes the selected row-80 model
subfile's native component chain and complete 19,808-triangle collision table.
That avoids the earlier duplicate-data heuristic and rebuilds the collision
index entirely inside ID65's physical copy. The exact six patches are:

- HP vertex count at WAD `0x6A3C04C`, one byte, `43` to `46`.
- HP face count at WAD `0x6A3C04E`, one byte, `32` to `33`.
- HP sector repack at WAD `0x6A3C180`, 916 bytes.
- Collision block tree at WAD `0x6A40DE0`, 27,090 bytes.
- Collision lookup blocks at WAD `0x6A47840`, 95,232 bytes.
- Added collision triangle at WAD `0x6A5F1C4`, 12 bytes.

The disposable output is deterministic:

- Base BIN SHA-256:
  `9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8`.
- Output BIN SHA-256:
  `db9230bbe1b8b5b15b52299267b3453cb60ec7494248295f323a40f597d7391f`.
- Output ID65 data SHA-256:
  `9e4d30371b9aeffed1071883848a67e1c80f6df313826bc7f4e5db2e926696b9`.
- Exact logical WAD diff: 108,415 changed bytes, all inside the six pinned
  patch ranges in row 80.
- Exact physical BIN diff: 125,762 changed bytes in 63 rebuilt and verified
  MODE2 Form1 sectors: LBA 54429 and LBA 54438 through 54499.

The BIN, CUE, static proof, runtime checklist, and executable Finder-reveal
helper stay together under
`_local/v5-stone-hill-level-replacement/unused-level-65-authored-terrain-add-copy/`.
The CUE basename is
`Unused-Level-65-Town-Square-first-authored-collidable-terrain-add-copy-RUNTIME-CANDIDATE.cue`;
the adjacent `-Reveal-in-Finder.command` helper resolves and selects that exact
CUE without participating in image composition.

Retail Town Square overlay/data, the ID65 overlay, SCUS, WAD directory, base
BIN, raw image length, and every byte outside those row-80 ranges remain
unchanged. The candidate deliberately does not author low-detail terrain,
vertical side walls, textures, surface behavior, Mobys, music, totals, portals,
Return Home, saving, or persistence.

Static/readback proof is complete, but runtime proof is pending. The focused
DuckStation gate must confirm that the original triangle and exactly one raised
copy appear, the copy is solid while standing/jumping/charging across its top
and edges, nearby gameplay remains stable, retail Town Square does not contain
the copy, and the comparison levels still load. Far-distance disappearance is
recorded separately because this first experiment is HP-only; LP/far-LOD
authoring is the next terrain gate rather than part of this candidate.

## Sixth runtime observation: first terrain discriminator rejected

The user tested the exact authored-terrain candidate and reported: “I honestly
didnt see anything different from the normal townsquare and ID65.” A read-only
post-report check found that the continuing DuckStation process had the exact
candidate BIN open, and the on-disk image still hashed to
`db9230bbe1b8b5b15b52299267b3453cb60ec7494248295f323a40f597d7391f`.
The required visible raised-copy discriminator was therefore not observed, so
this candidate is runtime-rejected and cannot count as an Add Terrain pass.

The static proof's mechanical facts remain valid: six exact row-80 patch ranges,
108,415 logical changed bytes, 125,762 physical changed bytes, and 63
rebuilt/verified MODE2 Form1 sectors still read back exactly. Those facts prove
image construction, not safe construction or recognizable runtime rendering.
The report does not by itself tell us
whether the copied face was submitted to or accepted by the renderer, or
whether it was rendered but too subtle, overlapped, occluded, or difficult to
locate. Collision and far-LOD behavior also remain unproven.

A later read-only GDB inspection narrowed that boundary without changing the
runtime verdict. Live level-ID readback was 65, the hidden selector was inactive,
and the exact authored sector header, three vertices, appended face record, and
matching collision triangle were all present in live ID65 RAM. The base sector
header was absent. This proves that the exact row-80 authored mesh and collision
bytes were loaded at runtime and rules out a stale or wrong CUE. It still does
not prove that the GPU drew the face recognizably. At the time of inspection,
Spyro was about 990 XY units away from the authored face, so that later frame
also cannot serve as the intended close-range view.

The test handoff itself was also not decisive. It described “three loose red
gems” near a bull, but the authored face was actually beside three loose green
gems and the dragon pedestal; the closest red gems were roughly 1,200 XY units
away, and the nearest bull was roughly 609 XY units away. The triangle was only
about 1,183 square XY units, translated by 68 units and raised by 10–16 units,
with no side walls. The incorrect landmark and subtle geometry are sufficient
to reject this candidate-and-handoff combination without claiming a renderer
failure.

A later exact bounds audit also invalidated the candidate independently of the
runtime visibility result. Sector 201 begins at row-80 WAD offset `0x6A3C038`
and is exactly 1,216 bytes long, so contiguous sector 202 begins at
`0x6A3C4F8`. The 916-byte repack patch at `0x6A3C180` ends at `0x6A3C514`:
28 bytes into sector 202's header. The previously reported 56-byte
“slack” was not inter-sector capacity. This exact v1 BIN is structurally unsafe
and must not be loaded or retested. A future Add Terrain implementation must
grow and rebase the environment component rather than writing past a sector.

The evidence record is
`docs/runtime-evidence/unused-level-65-town-square-authored-terrain-add-copy-not-observed-2026-08-08.json`.
Its status is `runtime-rejected-visibility-discriminator-not-observed`, and
`promotionAuthorized` remains false.

Before another add-copy/collision experiment, the next isolated control starts
again from the focused-runtime-passed display-name base and deforms one existing
HP face immediately ahead of the spawn landing. That control changes only two
existing vertex words and keeps all counts, component sizes, collision, LP
terrain, textures, Mobys, and game-contract tables unchanged. It answers only
whether authored row-80 vertex bytes reach the runtime renderer. LP/far-LOD and
component-aware structural growth wait until close-range row-80 visibility is
positively established.

## Seventh static candidate: spawn-visible renderer control

The replacement control starts again from the exact focused-runtime-passed
display-name BIN at SHA-256
`9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8`.
It does not use the rejected v1 terrain BIN. In row 80, HP face `213:37` is the
quad immediately in front of the entry landing. The control raises only its far
edge vertices from Z 512 to Z 1024:

- vertex 39 at WAD `0x6A3EB60`: `20 D8 E7 29` → `20 DA E7 29`;
- vertex 48 at WAD `0x6A3EB84`: `20 D8 E7 39` → `20 DA E7 39`.

Those shared vertices deform exactly faces 25, 26, 29, 37, 38, and 43 into a
128-unit-wide, 512-unit-high ridge directly ahead of Spyro. The landing face
under Spyro remains byte-identical. Face counts, sector sizes, environment and
following-sector headers, occlusion, special-surface data, collision, LP
terrain, textures, Mobys, and SCUS remain unchanged. Collision deliberately
stays on the original flat Z-512 ground, so this candidate is a renderer-only
discriminator and not a collision or Add Terrain test.

The exact output BIN SHA-256 is
`01a170b19303eaab77fb00fbc8cfabf5425a640348c84aa09f8aa4eb5b57e6b1`;
the ID65 data payload SHA-256 is
`c20e5cee4abd551c2536eb23fba9d2a8868193c9991af9ccfe7883429c9b4a57`.
Only two logical WAD bytes change. Rebuilding EDC/ECC changes 44 physical bytes
in raw LBA 54434, and exact readback confines every logical and physical change
to that boundary. A repeat export is deterministic.

The CUE, static proof, runtime checklist, and executable Finder helper are in
`_local/v5-stone-hill-level-replacement/unused-level-65-authored-terrain-render-visibility-control/`.
Runtime status remains pending and `promotionAuthorized` remains false. A
clearly visible ridge in ID65, absent from retail Town Square, will establish
that existing row-80 HP vertices reach the renderer. Only then should a new
component-aware structural Add Terrain candidate be attempted.

## Eighth runtime observation: renderer visibility control passed

The exact renderer-control BIN at SHA-256
`01a170b19303eaab77fb00fbc8cfabf5425a640348c84aa09f8aa4eb5b57e6b1`
passed its narrow DuckStation discriminator. The user reported that the terrain
edit was visible in ID65 and absent from retail Town Square. Read-only
open-file inspection after the report showed that the continuing DuckStation
process held this exact candidate BIN open, and an on-disk hash readback matched
the recorded SHA-256.

The user could walk through and beneath the visible ridge while the original
flat ground remained solid, and Spyro did not fall through that ground into the
death plane in the tested entry area. That is the expected outcome of this
control: only two existing HP vertex words changed, while every collision byte
remained identical to the passed display-name base. The result therefore proves
that authored row-80 HP vertices reach the ID65 renderer and that the visual
edit is isolated from retail Town Square. It does not prove collision editing,
a solid raised surface, a new face, or Add Terrain.

The evidence record is
`docs/runtime-evidence/unused-level-65-town-square-authored-terrain-render-visibility-control-focused-pass-2026-08-08.json`.
Its status is `focused-runtime-pass` for this renderer discriminator only;
`promotionAuthorized` remains false. LP/far-LOD behavior, broader comparison
checks, emulator-state conditions, arbitrary textures or Mobys, normal Create
BIN integration, and release remain outside this pass.

The next isolated candidate must again start from the focused-runtime-passed
display-name base. It should reproduce the same existing-face ridge with
matching source-derived native collision edits while leaving face counts and
component sizes unchanged. Only after the raised surface is visibly and
physically traversable should structural work proceed to component-aware
environment growth for genuinely new vertices, faces, and collision.

## Ninth static candidate: solid existing-triangle control

The collision control starts again from the exact display-name base at SHA-256
`9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8`.
It does not layer changes on the v2 renderer-control BIN. The enormous v2
ridge is intentionally absent because its 512-unit rise exceeds the native
collision format's unsigned 255-unit Z-delta limit and its two edited vertices
are shared by six visual faces.

Instead, the control uses the small green-grass triangle `213:64:hp`, about 77
degrees right of the ID65 entry landing. HP vertex 95 is referenced by that one
face only. It moves from `(8167, 6325, 512)` to `(8167, 6325, 576)` at WAD
`0x6A3EC40`:

- visual word: `20 84 85 5C` → `60 84 85 5C`;
- exact native collision triangle 13853 at `0x6A87B20`:
  `BB 1F 8B DE 0A D9 EA D1 00 02 00 00` →
  `BB 1F 8B DE 0A D9 EA D1 00 02 40 00`.

The original and authored collision triangles occupy the same two lookup
cells, `X31/Y24/Z2` and `X31/Y25/Z2`, through the same lookup words at WAD
`0x6A51682` and `0x6A5191E`. The collision header, tree, blocks, assignment
table, surface flags, all terrain and collision counts, sector and component
sizes, LP terrain, retail Town Square, row-79 overlay, and executable remain
unchanged. Exactly two logical WAD bytes differ. MODE2 Form1 rebuilding changes
74 physical bytes across only raw LBAs 54434 and 54580, with one payload byte
and valid EDC/ECC changes in each sector.

The exact output BIN SHA-256 is
`976a1910264c48fbc7cec8a9a1291f849bcad898fc511bae5d986a7af1c66214`;
the ID65 data payload SHA-256 is
`a824efbcde9481ad9be46f20531a3277ca76f14230c74113b562630ca2c95f35`.
The CUE, proof, checklist, and executable Finder helper are in
`_local/v5-stone-hill-level-replacement/unused-level-65-authored-terrain-solid-existing-triangle-control/`.
Runtime status is pending and `promotionAuthorized` is false.

The focused runtime question is whether walking, charging, jumping, and landing
follow the visible 64-unit grass incline instead of the old flat plane while
retail Town Square remains unchanged. Passing this control will prove one
existing row-80 face can be kept visually and physically coherent. It will not
yet prove Add Terrain.

The first true Add Terrain candidate remains a later, separate gate from the
clean display-name base. Its implementation must grow the environment by 28
bytes inside the existing model-subfile tail, shift and rebase every following
component, append one new HP triangle with independent vertices, reuse only a
genuinely zero-area-in-3D collision record, rebuild the collision index in
existing capacity, and preserve every retail row. That structural candidate is
not authorized until this simpler collision control passes at runtime.

## Tenth runtime observation: small solid-triangle discriminator rejected

The user reported, “Hmm im not seeing it in ID65.” This rejects the exact v3
candidate-and-handoff as a useful visibility discriminator. It is not a
collision failure: the slope was not recognizably located first, so none of the
walking, charging, jumping, landing, or old-flat-plane checks can be inferred.

Read-only corroboration rules out a stale final artifact. DuckStation held the
exact v3 BIN open at SHA-256
`976a1910264c48fbc7cec8a9a1291f849bcad898fc511bae5d986a7af1c66214`,
and recent screen history showed the exact v3 CUE selected before gameplay.
Live level-ID readback was 65. The exact authored vertex word `0x5C858460`,
face-64 record, and collision-triangle-13853 bytes were all present in live RAM.
That proves the candidate bytes loaded; it does not prove that the small face
was visibly framed, submitted to the GPU, or contacted.

A later exact overlap audit proved that the target was structurally invalid as
both a visibility and collision discriminator. Sector 213 HP face 64 lies
beneath overlapping sector 4 HP face 6 at Z 560. The v3 edit raised one target
tip from Z 512 to Z 576, so 15/16 of its XY area remained at or below the
retail face. Only the final 1/16 could protrude, by at most 16 units. The
promised recognizable standalone grass slope therefore did not exist.

The corresponding collision choice was invalid for the same reason. Retail
collision triangles 1189 and 1190 occupy the overlapping Z-560 surface and
shadow edited triangle 13853 in the intended contact area. The exact authored
triangle-13853 bytes were loaded, but they did not provide an isolated surface
for the walking, charging, jumping, or landing checks. This explains the
negative discriminator without indicating a renderer, physical row-80 load,
or general collision-decoding failure.

The handoff also was not practically decisive. Its target was one roughly
4,893.5-square-unit triangle about 296 XY units from spawn. “Turn approximately
77 degrees right” depended on an unstated initial heading, normal gameplay did
not expose the listed world coordinates, and the two-green-gem landmark still
required searching for a same-material grass triangle. The exact candidate is
retired and must not be loaded or retested; the user should not keep hunting
for it.

The exact run also had DuckStation's Moon Jump cheat enabled and a per-game
memory-card configuration active. Those conditions do not invalidate the
no-visibility report or exact runtime-byte readback, but they prevent clean
movement, collision, reset, or persistence conclusions.

The evidence record is
`docs/runtime-evidence/unused-level-65-town-square-authored-terrain-solid-existing-triangle-control-not-observed-2026-08-08.json`.
Its status is `runtime-rejected-visibility-discriminator-not-observed`, and
`promotionAuthorized` remains false. The historical BIN/CUE remains only for
rejected-evidence audit. Its Finder helper, checklist, and static-proof handoff
are poison-pilled in the same manner as the rejected v1 artifact.

The next isolated control must again start from the focused-runtime-passed
display-name base. Candidate selection must first exclude every footprint with
a higher visual face or higher collision triangle. Only then should it create a
broad, unmistakable, vertically isolated existing-surface ramp directly in the
spawn-forward view, remain within the collision format's unsigned 255-unit
Z-delta limit, patch every affected shared HP face and exact matching collision
triangle, and preserve all counts and component sizes. A true
component-growing Add Terrain candidate remains gated until that visible
surface and its traversal both pass.

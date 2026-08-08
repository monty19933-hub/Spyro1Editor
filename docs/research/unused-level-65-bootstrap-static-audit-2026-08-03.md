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

The next isolated level-format gate is an authored ID65 display-name
discriminator. Independently addressed totals, music-table extension, portal
routing, Return Home, content mutation, and memory-card persistence remain
separate later gates.

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

## Later gate after the non-flight control passes

Append a physical copy of the Town Square pair rather than aliasing it:

- Entry 79 overlay at `0x6927000`, length `0xF800`.
- Entry 80 data at `0x6936800`, length `0x2E2000`.
- WAD size `0x6927000 -> 0x6C18800` (`+0x2F1800`, 1507 sectors).
- Relocate `SCUS_942.28` from LBA 53875 to 55382; it ends at 55586, before `PETEXA0` at LBA 60000.
- Patch both-endian ISO root fields and rebuild Mode-2 EDC/ECC for the header, append, relocated executable, and root-directory sectors.

Only after independent storage passes should the experiment add an authored name, totals/music identity, a portal-65 landing pair, and independently validated persistence.

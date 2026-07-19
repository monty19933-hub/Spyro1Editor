# Green Wizard all-level support

This matrix separates a level owning a Green Wizard model row from owning the complete runtime needed by actor `0x011B`: the Wizard handler, lightning actor `0x0026`, shared animation/death paths, particles, textures, per-instance properties, route data, and scene pointer fixups.

## Current editor routes

| Level | Runtime class | Editor status | Exact supported/test route |
|---|---|---|---|
| Blowhard | Resident Wizard + lightning | Verified | Native T0 donor into the checked T7 slot; normal Create BIN and Create Swap Test use resident v2. |
| Magic Crafters | Resident Wizard + lightning | Verified | Native T107 donor into the checked T27 slot; normal Create BIN and Create Swap Test use in-place v2. |
| Wizard Peak | Native donor level | Verified | Native T6 donor into Elder Wizard T24. Retired T10 v1 and v2 were visible/killable and paid one gem but never attacked, including v2 with donor group `0xFF`. Runtime-proven v3 uses T24's already-matching group `0xFF`; normal Create BIN and Create Swap Test compose the exact in-place properties/fixup recipe. |
| Toasty | Full transplanted bundle | Runtime-proven standalone, editor composition pending | Wizard Peak T6 into Toasty T0 with the complete v11 package/overlay/texture/particle bundle. |
| Dark Hollow | No resident bundle | Blocked | Needs model-subfile growth/rebase and new texture/CLUT allocations; no safe candidate is emitted. |
| Haunted Towers | Lightning only | Needs target profile | Actor `0x0026` is resident, but actor `0x011B`, its Wizard package/handler, properties, and textures are not. |

## Levels requiring a full target profile

These levels contain neither the complete resident Green Wizard runtime nor a checked transplanted profile:

- Artisans
- Stone Hill
- Town Square
- Sunny Flight
- Peace Keepers
- Dry Canyon
- Cliff Town
- Ice Cavern
- Doctor Shemp
- Night Flight
- Alpine Ridge
- High Caves
- Crystal Flight
- Beast Makers
- Terrace Village
- Misty Bog
- Tree Tops
- Metalhead
- Wild Flight
- Dream Weavers
- Dark Passage
- Lofty Castle
- Jacques
- Icy Flight
- Gnasty's World
- Gnorc Cove
- Twilight Harbor
- Gnasty Gnorc
- Gnasty's Loot

Each new full-transplant profile must independently prove overlay ABI/relocation, actor-package and root allocation, RAM capacity, texture and CLUT destinations, private properties and route storage, pointer-fixup repair, final-BIN readback, and live DuckStation behavior. Toasty's addresses are not portable to another level.

## Wizard Peak follow-up capacity

The exact T24 v3 route is the runtime-proven in-place replacement. Live RAM from T10 v1
proved its properties and route pointers were valid but its state flags never
entered the native Wizard attack transition. T10 v2 then isolated pod/group
`0xFF` and still did not attack. V3 therefore moved to T24, a native Elder
Wizard slot that already uses detached pod/group `0xFF`; it reuses the unique
`0x74`-byte extent at `0xCAC0`, translates both T6 route points, and replaces
stale fixup `0xCACC` with `0xCAC0` at index 37. T10-T17 have sufficient private
extent but remain retired/unavailable. The user accepted visible lightning,
the translated route, and one gem; live RAM separately confirmed actor/model
initialization, attack state, movement, death, and retirement. T25 has only a
`0x34`-byte extent and cannot use this recipe.

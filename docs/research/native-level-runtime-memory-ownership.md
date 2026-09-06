# Native normal-load RAM and dynamic Moby ownership

Source-bound investigation and read-only report after the registration producer.
Original USA BIN SHA-256 is
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
WAD LBA is 37; EXE LBA is 53875. EXE file offsets below include its `0x800`
header, so file offset = runtime PC minus `8000F800`.

## Actual native address derivation

`80012D58..80013228` walks six length-prefixed components (terrain texture,
environment, occlusion, special surface, collision, cyclorama), a portal count,
each portal's `0x34 + 12*pointCount` header/points plus cyclorama component,
then particle and sound components. Its return is the end of the sound
component, **not the sector-rounded level-data subfile end**. All 35 original
levels have only zero sector padding after that return (20–2028 bytes).

`800158E8..80015900` stores that returned RAM pointer in `g800785E0` as the
model base. The model CD read still requests the complete header model length.
`80015B34..80015BC4` places the scene after that full model length and requests
the complete scene length. Thus known post-load end is:

```text
native copy buffer + used level-data bytes + full model bytes + full scene bytes
```

The initializer `8005A470` indexes its native jump table at `800113A4` by
level ID. Each retail case begins with `LUI v0; ADDIU v0,v0,signedImmediate`
and stores the address into `g800785D8`. The common epilogue at
`8005B6E0..8005B6F4` copies that into active `g800785DC`. This matters when an
overlay transplant changes the copy-buffer binding: original-retail margins
cannot be reused for the modified candidate.

The exact native PETE copy length is `39C48`. The source top reservation word
`g800755A4` is `800`, so its resident base is
`80200000 - 800 - 39C48 = 801C5BB8`. The normal buffer initializer
`8005B6F8..8005B7D4` reserves `6008` bytes then two `1C000` regions, producing
lowest normal reserved address **`80187BB0`**. The PETE loader calls this with
`a0=0` at `8005B8B8`; other direct callers at `80033098` and `80033270` use
`a0=1` and a different allocation mode. Those alternate lifetimes, PETE's
temporary CD staging before its resident copy, stack/scratch/renderer peaks
and transition overlap are **not closed by this report**.

## Dynamic Moby arena is already owned

The scene loader `80013F24..80013FC0` sets the placed-row base, static-row end,
Moby component end and dynamic count, then derives capacity from the remaining
component bytes. The allocator `800524C4..80052564` takes an `0x58`-byte row
upward and an associated `0x18`-byte properties record downward from the same
arena. Consequently capacity is `floor(remainingBytes / 112)`, not the number
of 88-byte records fitting an apparently unused zero run.

Retail capacities span 91–272. Town has 107 placed rows and `2800` remaining
arena bytes, capacity 91. The seven imported key/chest/reward rows consume
`268` bytes, leaving `2598` and capacity **85**. Their larger private properties
are separately owned in the genuine properties component; this does not refund
the native shared dynamic arena consumption.

The native Peace chest break consumer at `800842D0..8008437C` checks
`g800756A8 - g800756A4 >= 21` before emitting its debris (four pairs of classes
`136/137`, then ten class `135` pieces). The particle callback is separate.
This proves a real headroom consumer; it does not establish maximum concurrent
occupancy or authorize spending every remaining slot.

## Exact examples and candidate distinction

| Snapshot | Copy buffer | Used data | Model / scene bytes | Scene read end | Gap below normal buffer |
| --- | --- | --- | --- | --- | --- |
| Retail Town | `80089ECC` | `94508` | `5D000 / 8800` | `80183BD4` | `3FDC` |
| Exact Town V2/V3 | `8008B238` | `94508` | `5E800 / 8800` | `80186740` | **`1470`** |
| Retail Toasty | `80084A10` | `6E220` | `79800 / D800` | `80179C30` | `DF80` |
| Old bloated Wizard/Toasty overlay, before animation 8 | `80092238` | `6E220` | `79800 / D800` | `80187458` | `758` |

All numeric entries in this table are hexadecimal. An earlier message's Town
`1440` gap was arithmetic error: the correct subtraction is `1470` (5232).
Town's derived Moby pointer `8017DF40 + 170 = 8017E0B0` also matches retained
exact-V3 runtime samples; this is a useful endpoint check, not peak safety.

Adding animation 8 to plain retail Toasty grows models by `4000` and leaves
`9F80`. Adding it to plain retail Town exceeds the normal buffer by `24`
bytes. Adding it to the old Wizard/Toasty overlay exceeds by `38A8` **before**
the new sound descriptor's additional `14` used bytes and any further model,
scene, effect or private-state growth. That existing bloated candidate is not a
viable unsuppressed-native closure under its current copy-buffer placement.
Its fixed `F5220` post-copy constant is now explained by source:
`6E220 + 79800 + D800`.

## Ranked retail baselines and next enemy target

These are normal-load geometric gaps, not safe import budgets. Registrations
exclude root 0; `W/B/A8` means local Green Wizard class `11B`, lightning class
`26`, and player animation 8 presence. The final column is **only** global sound
map `1F`'s descriptor index, not sample-equivalence proof. Local absence does
not prove behavior absence, and existing resources still need equivalence.

| Level (native ID) | Gap (hex) | Generic roots | W/B/A8 | Sound map 1F |
| --- | --- | ---: | --- | ---: |
| Wild Flight (45) | 51644 | 43 | 0/0/0 | 0 |
| Dark Hollow (12) | 4DECC | 34 | 0/0/0 | 0 |
| Blowhard (34) | 48088 | 36 | 1/1/1 | 38 |
| Night Flight (25) | 3999C | 36 | 0/0/0 | 0 |
| Gnasty's Loot (64) | 30058 | 37 | 0/0/0 | 0 |
| Crystal Flight (35) | 286A4 | 40 | 0/0/0 | 0 |
| Gnasty's World (60) | 2688C | 21 | 0/0/0 | 0 |
| Icy Flight (55) | 239B0 | 32 | 0/0/0 | 0 |
| Lofty Castle (52) | 21CC8 | 43 | 0/0/0 | 0 |
| Gnasty Gnorc (63) | 195F8 | 31 | 0/0/0 | 0 |
| Dry Canyon (21) | 16D7C | 52 | 0/0/0 | 0 |
| Ice Cavern (23) | 157A4 | 38 | 0/0/0 | 0 |
| Doctor Shemp (24) | 1418C | 28 | 0/0/0 | 0 |
| Cliff Town (22) | 11828 | 42 | 0/0/0 | 0 |
| Jacques (54) | 10EC0 | 36 | 0/0/0 | 0 |
| Artisans (10) | 10274 | 34 | 0/0/0 | 0 |
| Toasty (14) | DF80 | 23 | 0/0/0 | 0 |
| Metalhead (44) | B270 | 44 | 0/0/1 | 0 |
| Stone Hill (11) | 8890 | 36 | 0/0/0 | 0 |
| Gnorc Cove (61) | 72FC | 44 | 0/0/0 | 0 |
| Wizard Peak (33) | 5E3C | 41 | 1/1/1 | 40 |
| Sunny Flight (15) | 58C0 | 46 | 0/0/0 | 0 |
| Terrace Village (41) | 510C | 43 | 0/0/1 | 39 |
| Alpine Ridge (31) | 44C0 | 45 | 0/0/0 | 0 |
| Town Square (13) | 3FDC | 36 | 0/0/0 | 0 |
| Dark Passage (51) | 3AC0 | 47 | 0/0/0 | 0 |
| Beast Makers (40) | 3764 | 43 | 0/0/1 | 38 |
| Misty Bog (42) | 32F4 | 42 | 0/0/0 | 0 |
| Magic Crafters (30) | 29FC | 42 | 1/1/1 | 39 |
| High Caves (32) | 2710 | 40 | 0/0/0 | 0 |
| Peace Keepers (20) | 2420 | 46 | 0/0/0 | 0 |
| Dream Weavers (50) | 1F60 | 42 | 0/0/0 | 0 |
| Haunted Towers (53) | 1B7C | 49 | 0/1/1 | 0 |
| Tree Tops (43) | 16BC | 38 | 0/0/0 | 0 |
| Twilight Harbor (62) | 2BC | 38 | 0/0/0 | 0 |

**Dark Hollow is the strongest next absent-resource ground-level investigation**:
it has `4DECC` baseline gap and 29 free generic tail registrations. This is a
selection for source closure work, not an accepted destination. Native overlay
dispatch, all particles, actual sound samples and their consumers, shared player
animation, scene/private ownership, dry placement and full RAM lifetime must
still be closed. Blowhard has more room than many levels but already has the
Wizard/lightning/player resources, so it would not prove the missing-resource
milestone. Flight levels add gameplay-policy complications despite larger gaps.

## Report implementation and replay

`NativeLevelRuntimeMemoryReport.CaptureOriginal` requires the inventory-issued
source certificate, complete data hash and exact executable/PETE evidence.
`AnalyzePlayerMerge` consumes the source-bound animation producer and retains
the exact original copy-buffer binding. The internal Town adapter recognizes
only the two existing exact data/executable outputs and checks their model
registration plan again. There is no arbitrary caller-supplied address route.
All reports keep `RuntimeFitProven`, `PeakTransientFitProven` and
`PublicationApproved` false. A known overlap is surfaced as a negative gap.

The focused closure smoke's `--source-v3 <original.bin>` mode checks all 35
retail baselines, source/executable/PETE staleness, both retained Town variants,
and real typed Toasty/Town animation-8 merge endpoints. It prints every ranked
baseline under `NATIVE_RAM_NORMAL_BASELINE`. Source-free fixtures cover malformed
component/portal/padding/placed-row ownership, known overlap and dynamic pool
shrink. No candidate or emulator state is changed by these reports.

## Native instruction certificates

| EXE file offset + length | Meaning | SHA-256 |
| --- | --- | --- |
| `3558 + 4D0` | Used-data component walk and return | `fb2af16b9e29594d7845aee8213a1bad2cc7eccdce122d59e5478d15480c6e6e` |
| `46F4 + 168` | Scene Moby arena initialization | `7bfb42fba56df4b4772f4a32f0114940090e0f4029456ed37ea8ac5e871be6d7` |
| `42CC4 + A0` | Dynamic row/properties allocation | `f3f404687b98b5208f97274a1932c967ba8d0bd04337011b53d831f25f592335` |
| `42DB0 + 40` | Dynamic deletion counter path | `4f25a1f234028471ca4131a157b4fbf6b950a7bf4c2507b8a74836739b9da82c` |
| `60E8 + 30` | Used-data return becomes model base | `cb9d880c5ab3ab1f6bb271496fa45608890b79f0cda15f0de9e103ccf04e248e` |
| `6334 + A0` | Scene base and full scene read extent | `079aa9694ebce3a4f7eb9109a7953ce9dc5911c1b27f9a497426de0d4e4acbda` |
| `4BEE0 + F8` | Copy-buffer epilogue and normal/alternate reserved buffers | `13b3b9768f36a0e9b739e8345151c9424fea74d2adde891aa003a8c3048299d2` |

Peace chest headroom consumer: WAD `1887890`, length `C4`, SHA-256
`13f19e7666a180a98b5927545dee6508588816359f434a9636e91a8dd4358815`.

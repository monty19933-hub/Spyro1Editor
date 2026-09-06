# Native Town Square chest: read-only runtime observation contract

2026-09-05, with retained interaction samples through 2026-09-06 UTC.
Source-bound recording guidance, not broad gameplay acceptance. Native
instructions were decoded directly from the original USA image and the exact
published V3 image below. This source/document investigation made no Core,
legacy-helper, emulator, UI, or image changes. This contract does not authorize
RAM writes or injected calls.

## Exact source binding

Paths are relative to the repository root:

- Original: `Spyro the Dragon (USA).bin`, SHA256
  `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
- V3: `_local/research/native-moby-closure-20260905/town-square-native-closure-v3-r01/town-square-native-closure-v3.bin`, SHA256
  `15df0ee1edba6cb48fe1d34df5a5f51a7d6f289de387c499ca0ff04e99b443bf`.

Both complete BIN digests and actual ISO directory records were checked. They
are MODE2/2352: logical payload size 2048, raw payload offset 24. WAD.WAD starts
at LBA 37. SCUS_942.28 is `0x66000` bytes, at original LBA 53875 / V3 LBA 53880.
Executable file offset = `RAM address - 0x80010000 + 0x800`. Town overlay entry
15 remains at WAD `0x118E800`, loaded at `0x8007AA38`; overlay size grows from
`0xF800` to `0x10800`.

| Component | Original SHA256 | V3 SHA256 |
| --- | --- | --- |
| Complete Town overlay | `9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5` | `fd859bff13985e18c3aff74cc889006c48b2743bf57a2b0f9d80662d25a09c2d` |
| Complete executable | `a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998` | `4268f9943233b9f8c24d1d4b0924cba8747547b44b82eddd13f0a2ac10ac17de` |

The following half-open native code spans were independently extracted and are
byte-identical between original and V3; their identities, not cached labels,
bind the interpretation below:

| Code span in RAM | Length | SHA256, equal in original and V3 |
| --- | --- | --- |
| Key pickup core `[0x80083B4C,0x80083CAC)` | `0x160` | `bac4b44c8aaa1e0e382fbd603531810f84c9c916026e655a7a05ff581ea314d4` |
| Collection/award `[0x8003B7C0,0x8003BAD0)` | `0x310` | `bdaa92cb7c343607f5bbc7462f3bf54743f8f6842bb70b9f74cee06b712038e7` |
| HUD `[0x80054584,0x800557CC)` | `0x1248` | `e21414d29b877e50673dac71d5a5a3495dc3e0331aa2e172249360e931099843` |
| Level-slot mapping `[0x800155E0,0x80015668)` | `0x88` | `b6a57847d9e35a80c4002b0afc3e788a70766277f7c14c06eacc0af1e1e60ce9` |
| Placed-row deletion `[0x80052568,0x800525CC)` | `0x64` | `602fc31b1351212e5d5c72c39ac59cc86a9afd0226a9a0549192855588498505` |

V3's relocated native Peace Keepers chest body
`[0x80089ECC,0x8008A60C)` has SHA256
`71b081eb43d0b473d0d2045993c3df1d28ebff426751c14fb52fcd04479cd312`.
Its source-state block at donor `0x80083D68` maps to V3 `0x80089F00`.

## Values to record

All values are little-endian. Use native widths, not nearby cached analyzer
field declarations. Resolve the slot and row base from each live observation;
do not carry pointers across level loads.

| RAM address / expression | Width | Native meaning |
| --- | --- | --- |
| `0x8007596C` | u32 | Current native level ID; Town is 13 |
| `0x80075964` | u32 | Current treasure-table slot; Town is 3 |
| `0x80075830` | u32 | Key lifecycle state, not a boolean |
| `0x80075860` | u32 | Actual global gem total |
| `0x80077420 + 4*slot` | u32 | Actual collected gems for the level; Town `0x8007742C` |
| `0x80077FC8` | u32 | Animated current-level HUD gem counter; can lag actual total |
| `0x8006F438 + 2*slot` | s16 | Level treasure target; Town `0x8006F43E`, retail 200 / V3 210 |
| `0x80075828` | u32 pointer | Placed Moby-row base `B`, stride `0x58` |
| `0x80075758` | u32 pointer | Native chest's tracked spawned-gem identity |

At EXE `0x80015604..0x80015660`, the game derives slot
`6*(level/10 - 1) + level%10` for these positive level IDs and stores it with
`SW` at `0x80015660` (file `0x5E60`). Town 13 therefore selects slot 3.

The native award routine begins at `0x8003B854` (EXE file `0x2C054`). Its
`0x8003B88C..0x8003B8C0` instructions load the slot, form
`0x80077420 + 4*slot`, load both totals with `LW`, add the award amount, then
store global with `AC225860` at `0x8003B8BC` (file `0x2C0BC`) and level with
`AC830000` at `0x8003B8C0` (file `0x2C0C0`). These are **32-bit actual totals**.
Native gem pickup maps classes `0x53..0x57` to amounts `1,2,5,10,25` at
`0x8003BA64..0x8003BAB0`, then calls the award routine with `0C00EE15` at
`0x8003BAB4` (file `0x2C2B4`).

The HUD separately loads `0x80077FC8` at `0x80054BD4..0x80054BD8`, compares it
against the level's actual total, and advances it at `0x80054C4C` (file
`0x4544C`). Its completion comparison loads the halfword target table with
`LH` at `0x80054C68` (file `0x45468`). Thus HUD lag is expected; target 210 is
not collection evidence. The target's Town file offset is `0x5FC3E`.

## Key and chest state evidence

Native Town key pickup sets a register to 1 at `0x80083C70` and writes
`AC295830` (`SW`) at `0x80083C7C`, WAD `0x1197A44`. Key states relevant to this
family are:

- `0`: absent/reset. Chest state-zero code treats key 0 or 3 as unavailable,
  writes 0, and remains locked.
- `1`: available. The key-ready chest requires exactly 1 before its approach,
  height, player-state and facing gates can start the opening sequence.
- `2`: the native chest has opened/consumed the key; V3 `0x8008A568`, WAD
  `0x119E330`, writes 2 after native debris/reward and deletion calls.
- `3`: a collected gem matched the tracked pointer at `0x80075758`. EXE
  `0x8003BA1C..0x8003BA2C` tests that identity and stores 3 (file `0x2C22C`).
  **State 3 does not prove that all chest rewards were collected.**

The V3 source scene has 114 rows. Independently read row identities/positions:
T107 class `0xAD`, `(119632,102288,8192)`; T108 class `0xAE`,
`(121680,102288,8192)`. Runtime observations should record:

| Actor / field | Address | Interpretation |
| --- | --- | --- |
| Key T107 | `B+0x24C8` | Class u16 at `+0x36`; pickup writes byte `+0x49=2` at `0x80083CA0` |
| Chest T108 | `B+0x2520` | Class u16 at `+0x36`; XYZ i32 at `+0x0C/+0x10/+0x14` |
| Chest native state | Chest `+0x49`, u8 | 0 locked, 1 key-ready, 2 opening sequence; not a completed-reward count |
| Chest lifecycle marker | Chest `+0x48`, byte | Native placed-row deletion writes `0xFD` / signed -3 |
| Chest private properties | `P=*(u32*)Chest` | Native owner index at `P+0`; sequence timer u32 at `P+4` |

V3 writes chest state 1 at `0x80089F88` (WAD `0x119DD50`) and state 2 at
`0x8008A0E8` (WAD `0x119DEB0`). Its removal calls `0x80052568` at
`0x8008A558`; the placed-row path writes `0xFD` with `SB` at `0x800525C8`.
The private properties source pointer resolves to `B+0x5DA0`. Use the live row
pointer for recording rather than treating that source-relative offset as a
general allocator contract. State 2 alone does not establish completed removal.

## Exact reward count and value

The native expected output is **six physical gems: four green and two red,
worth ten treasure**. Five reward-controller rows do not mean five gems: the
chest emits its own green gem. The source-bound emitter and donor/V3 selector
proof is retained in [native key/chest conditions](native-town-square-key-pickup-conditions.md#exact-reward-composition-six-gems-ten-treasure).

The source row byte `+0x53 & 0x7F` selects emitted class. T108 chest and
T109/T110/T111/T112/T113 controllers respectively contain
`54,54,53,54,53,54` (hex). Native `0x8003ABC0` emits one gem per invocation;
its second argument selects motion, not quantity. The chest calls it at V3
`0x8008A540`, and each of the five controllers follows its one-shot native
path through `0x8008A8BC`. The retail class-to-award mapping proved above gives
`2+2+1+2+1+2 = 10`; no color/value substitution or legacy helper is involved.

## Before/after observation rule

Bind every recording to the exact candidate digest, native level/slot, row
identities, observation time and action. Capture key state, actual global and
Town totals, HUD counter, target, chest/key row states and properties timer
before interaction, after key pickup, after opening, and after collecting the
visible rewards. Isolate unrelated retail gem pickups when measuring the new
family. For the complete new-family reward observation, require both actual
global and Town deltas of +10; HUD settling is a separate observation. A changed
key state, deleted chest, target 210 or one reward collection is insufficient.
Opening animation, sound, debris, all rewards, death/reload, repeat entry and
normal exit remain separate gameplay gates.

For future native families, reuse the separation of actual inventory, display,
target and actor lifecycle—not these Town-specific pointers or state labels
without fresh native source binding.

## Retained V3 interaction evidence and stop boundary

The parent tested exact V3 from unchanged normal spawn. The retained 24-sample
JSON was independently read for this document:
`_local/runtime-native-closure-20260905/resumed-session-20260905/chest-v3-r01-ram-samples.json`,
SHA256 `442a2ac8df52f78d501ff13d2c2301c9862a6ab00696147a26da130a023a6a42`.
Times below are 2026-09-06 UTC. Native level 13, slot 3 and target 210 remain
bound; this document reviews retained observations rather than operating or
independently watching the emulator.

| Retained time | Key | Actual global / Town | HUD | Relevant row evidence |
| --- | ---: | --- | ---: | --- |
| `02:51:23.119604` baseline | 0 | 0 / 0 | 0 | Key/chest `+0x49=0` |
| `02:59:36.249910` key acquired | 1 | 0 / 0 | 0 | Key `+0x49=2`, chest `+0x49=1` |
| `03:04:40.982619` chest opening completed | 2 | 0 / 0 | 0 | Chest `+0x48/+0x49=FD/02`; parent records visible debris/particles |
| `03:05:31.641214` partial collection | 3 | 7 / 7 | 1 | All five controllers already have `+0x48=FD` |
| `03:06:22.562269` final settled sample | 3 | 10 / 10 | 10 | Chest `FD/02`; all five controllers `+0x48=FD` |

The partial sample concretely demonstrates both warnings: key state 3 and all
controller deletions can precede complete collection, and HUD can lag actual
treasure. Final player XYZ is `(121599,100757,8552)`, lives 4; actual global
and Town deltas are both exactly +10. Together with the parent's six visible
gems, this supports the bounded key/open/reward interaction pass for this exact
candidate—not every native gameplay acceptance gate.

The Mac locked after this interaction, ending the runtime session. No normal
exit, re-entry, death/reload, repeat-entry gating or audio pass is claimed.
Candidate/receipt identities are unchanged and `runtimeProven=false` remains;
no broad promotion follows from these samples.

## Bounded legacy-label caveat

`SpringChestInGamePatchBuilder` contains an older experimental
`AwardTreasureRoutineAddress = 0x800420D4` label. Direct retail instructions at
that address execute player state/effect logic, not the award routine proved
above. This observation neither changes legacy code nor promotes any legacy
helper, injected-call recipe, or manual counter write into this native test.

# Native import resource frontier after b9bcdfb

Read-only investigation during the frozen Release gate. No producer, candidate,
installed app or emulator setting was changed for these findings. Original USA
BIN SHA-256 is
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
All offsets are logical WAD or EXE-file offsets, not raw BIN offsets. WAD LBA is
37, EXE LBA is 53875, and EXE file offset = runtime address minus `8000F800`.

## Registration capacity is not a memory budget

The level header has 64 root offsets at `+0x50` and 64 class IDs at `+0x150`.
Slot 0 is the special Spyro animation stream. The generic loader at
`80015AE8..80015B30` begins at slot 1, stops at the first nonpositive root and
has an upper-exclusive slot bound of 64. Thus there are **63 generic local
registrations**, not 64 arbitrary packages and not a sparse free-slot pool.

The global class table at `80076378` has 512 pointers. Registration loads the
class with signed `LH`, shifts by two and writes without a local bounds check;
an append producer must enforce `0..511` and shared/local identity ownership.
PETE contributes 54 shared registrations including class 0. Absence from the
local header is not proof of absent resources or behavior.

The fresh 35-level audit found contiguous positive prefixes, no hidden later
roots, no duplicate generic local classes and no generic-local/PETE collisions:

| Retail destination | Generic local registrations | Available tail slots |
| --- | ---: | ---: |
| Dry Canyon, fullest | 52 | 11 |
| Town Square | 36 | 27 |
| Gnorc Gnexus, least full | 21 | 42 |

Registered-but-unplaced packages remain owned; no unused-looking root is safe
to reclaim without native consumer analysis. `NativeLevelDataGrowthLayout`
already relocates existing model roots during multi-subfile growth. The next
producer should capture local/PETE bindings and native loader bytes, resolve
resident equivalence or conflicts, append missing packages and consecutive tail
registrations, and emit typed root/class writes while preserving slot 0.
The existing Town AE append is the first byte-equivalence oracle. Negative
tests must cover 63-slot exhaustion, holes, negative/out-of-range classes,
shared conflicts, stale bytes, unowned references and address-space mismatch.

Other independent limits remain:

- GPU upload is `0x80000` bytes to the right-hand `512x512` VRAM words. WAD
  growth does not grow this texture allocation; all existing consumers matter.
- SPU upload starts at `0x1010`, transfer count `0x7EFF0`. Mapping IDs,
  descriptors, pitch and actual sample ownership require separate closure.
- PETE copies `0x39C48` bytes into RAM at `801C5BB8`; native initialization
  reserves further lower buffers. The normal-path lowest derived pointer in
  this audit is `80187BB0`. This is not a complete per-level RAM budget.
- `800158E8..80015900` returns model RAM base; `80015B34..80015B6C` derives
  scene RAM from model length. Model growth moves later runtime allocations.
- Original ISO layout allows `0xB90800` (12,126,208) bytes of total WAD growth
  before PETEXA0 at LBA 60000. This is disc slack, not RAM, VRAM or SPU slack.

## Native electric damage requires a player resource

The lightning handler passes damage flags `0x00020020` at
`8007EB80..8007EB90`. The shared accepted-damage path at
`80041044..80041068` sees bit `0x20`, selects sound map ID `0x1F` and state 7.
State is stored at `80078AD0`; the animation update at `8003CC30..8003CC4C`
uses table `8006C470`, whose state-7 entry is **animation 8**. The renderer at
`80023B04..80023B4C` fetches `gModels[0] + 0x38 + 4*animationID` and
dereferences its frame table without a null guard. This is the shared player
model, not generic lightning class `0x26`.

All 35 original special root-0 streams parse exactly to the next root as:

```text
[s32 animation ID][u32 blob byte length][blob] ... [FFFFFFFF]
```

The loader at `80013230..800133DC` clears stale/unavailable entries among 46
animation slots, installs each record at `gModels[0]+0x38+4*ID`, rebases blob
`+0x14/+0x18` against shared player geometry at `gModels[0]+0x34`, and rebases
blob `+0x10` against its local frame-data base `blob+0x24+4*frameCount`.
Frame-word low 21-bit offsets become packed halfword addresses while preserving
upper bits. These mixed local/shared address domains must not be flattened.

| Level | Stream WAD start | Stream length | Animation 8 |
| --- | ---: | ---: | --- |
| Wizard Peak | `30DF000` | `1A2D0` | First record |
| Magic Crafters | `28B6000` | `14E1C` | Byte-identical record |
| Toasty | `15C7800` | `15A6C` | Absent |
| Town Square | `1311000` | `CC30` | Absent |

Only retail level IDs `30,33,34,40,41,44,53` contain animation 8. Its full
record length is **`0x3E1C`, including the eight-byte header**; blob length is
`0x3E14`, frame count 34. Record SHA-256:
`cf194a65fc9b3b8790f6cd96eae5ef309156d00ad205d85790b2128e57243eb0`.
The resident shared player package is WAD `7B9000`, length `32E1C`, SHA
`46f570bf0ad2d433f8b42874ae40a3e5d7155d7206dddd1cd08a2d4be16e0e76`.
Its animation-8 sentinel at WAD `7B9058` is `FFFFFFFF` and is explicitly cleared
by the loader. Copying class `0x26` does not fill that missing player slot.

A regression-first codec must reject invalid IDs, duplicate records, malformed
lengths/terminators, out-of-bounds frame tables and unowned mixed references,
and round-trip every original stream. A subsequent typed merge can insert the
donor record before the destination terminator and relocate later roots while
preserving every existing record. Parsing alone does not prove safe append,
root-0 lifetime/reset behavior or all transitive frame consumers.

## Electric sound and remaining effects

Wizard Peak sound component is WAD `30DE5B4`; `map[0x1F]=40` selects the
20-byte descriptor at `30DE9DC`:
`e046030000000000320000080000000000000000`.
Its sample is WAD `301FEE0`, length `1D40`, pitch `0800`, SHA-256
`b03fdc973a8966a21326691f1546c19bf9e8d40d2eb15b00bec6cc9789599322`.
Toasty and Town map ID `1F` to descriptor 0, a different sample at pitch `0400`.
The complete donor sample occurs nowhere in either destination bank:

| Bank | WAD start / length | SHA-256 |
| --- | --- | --- |
| Toasty | `150A800 / 4E800` | `ce2f597e496205d6e55c17aa302092ae168bd0258fb77b5db10aad7f1cd98c8f` |
| Town | `121E800 / 5E000` | `1771f66bb5315f8994cf2c843c0d5576f38fb347ae57b74ab9463abc21dd3332` |

Correct native damage therefore needs a real sample allocation, descriptor and
global-ID binding, with other consumers protected. Generic hurt audio is not an
equivalent import. The parent independently re-read the two complete banks,
confirmed sample absence and all reported hashes.

State-7 particle `0x0A` has non-default native spawn/update dispatch in Toasty
(`80083D40` / `80082A44`) and byte-identical pixels/CLUT across donor and both
targets. Generic hurt uses it too. That proves resident art/dispatch, not every
transitive callback. State-7 recovery `800427BC..80042804` is shared EXE code.

Impact logic at `8007EBC8..8007EBF0` emits four children of the same class
`0x26`; its initializer selects state/animation 3 when parent and child classes
match. The already copied complete class-26 package, WAD `3135C88`, length
`564`, includes animation 3. Its SHA is
`9ea5c72ba675e9c6a81433f7408695399e19bc188b4b2a923069d71de601789a`.
Unresolved impact behavior is not proof of a missing additional child model.
Callbacks, allocation, private state, renderer/relocation, particle-7/41 trail
and screen-filter behavior still require complete typed dependency profiles.

Do not simply remove the Toasty experiment's suppressions. Its fixed post-copy
formula leaves only `0x758` bytes before `80187BB0` at overlay size `17800`
(`0x2BC` is its minimum allowed margin, not the actual margin). Animation 8 adds
`0x3E1C` logical bytes and `0x4000` sector-rounded model growth. RAM and native
copy-buffer ownership must be recalculated; WAD fit is insufficient evidence.

## Independent source certificates

Two agents independently traced the player state/animation/renderer chain;
the parent also verified the loader, table, registration and upload span hashes
directly from the ROM and compared the two complete animation records.

| EXE file span | Meaning | SHA-256 |
| --- | --- | --- |
| `3A30 + 1B0` | Player stream loader | `0cca46f0e44c16b8adea088e3081637ec5b04a512dc2c8eac494f9d16732cefb` |
| `5CC70 + 2D` | State-to-animation table | `9f502dce2b68cb8497072c5c9c92d3f914710cdbbcbc9c25c86cbe29a4241e0b` |
| `31844 + 28` | Electric sound/state selection | `d0e9d200cbf22489706549be06f9ae873e877c9be6eb1a3fb93b25503ab58721` |
| `14328 + 68` | Player animation dereference | `caab994e5b0f5cba19fce5b2e7c969bcfbe79aa60f760c8e76cdb48960ac6b42` |
| `61D0 + 260` | Registration and clearing loops | `2f24d2dfd2120e3ad8ddddce72df395ca5e9af6ceedcfa83d8295aa41053e0a4` |
| `5EB0 + 230` | Header / VRAM / SPU loading | `f3cdb7634a2e36f2329959401df8bc6006cd623bd2ea7233e4ca43ea53e42c15` |
| `4BEF8 + E0` | Reserved-buffer arithmetic | `d0b4cf5a7cf9ee407ff413f7261bd5f92f4880d7cee8467852f4d757e6b84295` |

These hashes bind source observations. They are not a new import capability,
complete RAM budget, publication certificate or gameplay pass.

## Minimal shared browser integration

There is currently **no App consumer** of `NativeMobySourceInventory`.
`MainWindow.ObjectGallery.cs:ShowObjectGalleryAsync` filters normal Create BIN
templates; the earlier `GetCreateBinSafeObjectGalleryTemplates` uses
`ObjectGalleryDeduplicationKey` to deduplicate display/chest names. Exact native variants must not
pass through that name-based deduplication. Browse class, donor level and exact
row using `SourceVariantId`; registration-only resources and special class 0
belong in inspection, not invented placement templates.

The regular editor must retain `AddMobyNearSelectionAsync`, `PendingMobyAdd`,
`AddPendingMobyAtPositionAsync`, `UndoMoby`,
`PersistCurrentObjectAndMovementEditsAsync`, `MobyEditStore` and Build Safety.
The lab entry is `MainWindow.Id65BlankLab.cs:BuildId65LevelBuildingObjectManager`;
`SetId65FreshRemotePadMobyChoiceAsync` retains exact catalog identity, canonical
snapshot validation, `SaveExactAsync` CAS, durable reinspection and projection.
Browsing needs no document migration. Unsupported placement must not enter old
schemas, and any future placement schema needs explicit migration.

Show independent statuses for local registration, exact source variant,
dependency evaluation and each capacity domain. Unknown budgets must remain
unknown. ID65 is not a retail inventory target: display that fact rather than
borrowing Town Square residency or turning discovery into Apply/Create BIN.

Required integration regressions: preserve same-name variants; forbid Add for
special/registration-only resources; keep unsupported residency and ID65 from
unlocking writes; prove browse/search/cancel/refresh changes no state; preserve
approved Add/link/Undo/save plans and stale-scene refusal; retain exact lab
schema-3..6 bytes, explicit migrations, stale-CAS and failure rollback behavior.

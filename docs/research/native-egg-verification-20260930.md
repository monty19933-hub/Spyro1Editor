# Native dragonfly egg verification — 2026-09-30

The continuation implements per-level and global collected/available counters,
required-egg completion, and source-guarded bonus gates. The existing eye-free
models, inventory icons, seven palettes, native collection and stable lifetime
bitmap slots are retained.

## Native gameplay and card evidence

A private Stone Hill fixture places five palette variants in native rows 195–199
and an editable Japanese sign in row 200. The exported BIN SHA-256 is
`4f878341e3c97c87be2988781b8a4ddde88e1facf3494f73abde2cec594f5cdb`.
The runtime uses real USA firmware in an isolated DuckStation profile; no HLE or
resume state. The user’s original image and original card were not modified.

Ordinary controller movement collected the red egg (row 197). Actual native
inventory changed from 0/5 to 1/5 globally and for Stone Hill. The visible models
and inventory carry no added eyes. The three authored sign lines also appeared
on approach through ordinary movement. A later position-only debugger navigation
was used to obtain a stable sign screenshot; it did not write progress bits.

A position-only debugger navigation reached an original checkpoint and its save
fairy. The native dragon rescue added a dragon normally. The native Save Game
menu wrote the copied card and displayed GAME SAVED. This is native collection,
serialization and card I/O with assisted checkpoint navigation, not a claim of an
entire unaided playthrough. Six card bytes changed, all within the first native
slot (file offset 0x2200, size 0x600). The stored byte-sum checksum was 1267 and
matched recomputation. Every byte outside that slot was unchanged.

Before-card SHA-256:
`50f2f24df84ec8a3dcdcbc3a4f4d22ffcc0884b5eb235c7c1fdc9102f7a990b1`

After-card SHA-256:
`3dce02bf4bc04da880004732b694a8546b74eab604589b83d1029e8badc96db0`

After completely quitting the emulator, a new process loaded the same card through
Load Game with no save state and no debugger writes. Stone Hill and global
inventory both displayed 1/5, the red model remained suppressed, and the other
four remained present. Read-only native bitmap readback matched exactly. Native
63 gems, 3 dragons, 1 retail thief egg and the required-egg percentage restored.

Private receipts are under the task workspace `work/egg-continuation`:
`after-first-natural-pickup.json`, `native-card-save-readback.json`,
`runtime-cold-load.json`, `native-egg-cold-load.json`, and navigation receipts.
Screenshots in the isolated runtime profile include 16-35-03 (GAME SAVED),
16-55-22 (cold-loaded 1/5 inventory), and 16-59-09 (authored sign text).
Discs, firmware, cards and extracted native assets stay out of source/packages.

## Source-bound progression and unlocks

The native read-only counter accepts a0=-1 for all levels or 0–35 for one native
inventory index. It returns collected/available in v0/v1. Removed placements stay
tombstoned and do not inflate available counts. The inventory hook preserves the
original row sequencing and draws every visible row, including 0/0.

The completion calculation requires all authored eggs as well as retail goals.
The Gnasty’s World bonus portal extends the original 12,000 gems / 80 dragons /
12 thief eggs check with all required dragonfly eggs. Previously unlocked access
is preserved through the native flag branch preceding the edited owner. The
Gnasty’s Loot ending retains its exact 14,000-gem predicate and native player-state
condition while requiring all placed eggs. No new card-format bytes are used.

`tools/Test-UniversalEggProgress.py` independently executes generated R3000 code:
4,489 checks cover percentage, zero/partial/full per-level and global masks,
portal/ending predicates, stack/register preservation and absence of progress
writes. Separately, private original-overlay source guards, zero-egg no-op and
modified-owner refusal passed; 808 additional private scalar gate cases passed.
These are instruction/source tests. A natural endgame unlock playthrough and
all-color/all-level lifecycle coverage are not implied by the Stone Hill test.

## Completion weighting

For N active custom eggs and K collected, ordinary progress is the integer floor
of `(50*min(gems,12000) + 6000*dragons + 10000*(retailEggs+K)) / (12000+100*N)`.
Every custom egg has the same score weight as an original thief egg. Once K=N,
dragons=80, retailEggs=12 and gems>=12000, the native bonus scale is
`100 + floor((gems-12000)/100)`. With N=0 the original native percentage owner is
left unchanged. The separate portal and ending predicates are described above.

## Reused physical row and cold card load

A later Stone Hill export removed original row 0, then reused that physical slot
for a green egg with logical save identity 195. The five eggs occupied physical
rows 0, 195, 196, 197 and 198, while retaining logical identities 195–199. The
sign occupied physical 199, logical 200. The exported image SHA-256 was
`2fbc74db415f536d2762548c407e41ecdfb538a521adc98e0617484de90b8fa3`.

A new native process loaded a copy of the previously written card using ordinary
Load Game, with real firmware, no save state and no debugger writes. The red egg
at physical 196 remained suppressed by logical saved bit 197; the gold egg now at
physical 197 remained visible because its own identity was 198. The replacement
green egg at physical 0 remained visible. Independent read-only RAM checks
confirmed all five states and the native bitmap. The saved card slot was unchanged.

This proves the exact nonempty mapped Stone Hill cold-load case, not all level
lifecycles or every possible Moby deletion. The later empty-level branch repair
has its own instruction-level evidence; it does not change this nonempty path.
Private receipt: `work/egg-continuation/native-deletion-cold-load.json`.

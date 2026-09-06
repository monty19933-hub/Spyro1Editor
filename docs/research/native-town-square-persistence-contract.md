# Town Square V3 native persistence and reload contract

This is source-backed, read-only evidence for the exact experimental V3 BIN
`15df0ee1edba6cb48fe1d34df5a5f51a7d6f289de387c499ca0ff04e99b443bf`,
with the separately observed normal exit/re-entry result recorded below. The
static trace does not establish death or memory-card save acceptance. The
original source remains
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
The native EXE spans below were freshly compared between original and V3 and
are byte-identical, accounting for EXE LBA53875→53880.

## Two different kinds of bits

Native routine `0x8003B7C0` marks a placed source row as deleted in the current
level's live bitset. It bounds the actor pointer against
`[u32[0x80075828], u32[0x80075890])`, divides its offset by the native 88-byte
row stride, and sets the corresponding bit at `0x800778B8`. It does not award
treasure and does not itself set the durable collected-gem bit. Native deletion
`0x80052568` unlinks a placed row and sets `+0x48 = 0xFD`; unlinking alone is
also not a durable collection write.

The Town spawn callback `0x800872DC..0x80087334` assigns an emitted gem's
`+0x56` byte from `(sourceActor - runtimePlacedTableBase) / 88`, provided that
index is below256. Native collection `0x8003B854` uses that byte for dynamically
spawned gems; for placed gems it derives the actual placed-table index directly.
It increments actual global/level treasure and sets:

```text
collectedWord = 0x80077908 + currentLevelSlot * 32 + (sourceIndex >> 5) * 4
collectedMask = 1 << (sourceIndex & 31)
```

The level slot is `u32[0x80075964]`; Town13 uses slot3. Neither the donor's true
index nor the donor's `+0x3A` byte is reused as the spawned gem's saved identity.
All six reward sources are appended after the107 native rows and occupy distinct
indices within the native byte/256-bit capacity.

| Source in V3 | Reward | Saved bit | Town collected word |
| --- | --- | --- | --- |
| T108 chest | Green,2 | `0x00001000` | `0x80077974` |
| T109 controller | Green,2 | `0x00002000` | `0x80077974` |
| T110 controller | Red,1 | `0x00004000` | `0x80077974` |
| T111 controller | Green,2 | `0x00008000` | `0x80077974` |
| T112 controller | Red,1 | `0x00010000` | `0x80077974` |
| T113 controller | Green,2 | `0x00020000` | `0x80077974` |

After collecting all six, the invariant is
`(u32[0x80077974] & 0x0003F000) == 0x0003F000`, with ten additional treasure.
Other native bits may also be set, so do not compare the whole word against
that mask alone. Opening/emitting also marks these six source indices in live
deletion word `0x800778C4`; that live word is not the persistent re-entry oracle.

The native collection array has36 slots of32 bytes. Save serialization code
`0x80059A10..0x80059A20` copies its complete `0x480` bytes into the native save
record at `+0x10C`; load code `0x80059820..0x80059830` copies that range back.
This is a concrete memory-card data-path dependency, not a memory-card runtime
acceptance claim.

## Reload consumes the exact destination identities

Loader `0x800142C8..0x800143AC` iterates the actual reconstructed placed rows
until their native terminator, using each destination index to test the current
level's collected bits. For a collected source whose `+0x3A & 0x7F == 0x7D`, it
calls native deletion `0x80052568` rather than letting that source emit again.
Fresh source reads confirm donor chest T79 and controllers T118–T122 all have
`+0x3A = 0x7D`; V3 T108–T113 preserve it exactly. This branch deletes them whether
or not their live-deletion state was recorded at a checkpoint.

Thus fully collected T108–T113 should reconstruct as deleted (`+0x48 = 0xFD`)
and must not generate a second ten treasure. With partial collection, only the
collected source bits may be treated as permanently consumed; opening a chest
is not proof that every emitted gem has been collected. The uncollected source
rows may legitimately return on reload so their rewards remain obtainable.

New-level entry `0x80015CF8..0x80015D78` clears the `0x68`-byte checkpoint/live
area at `0x80077888`, clears global key state at `0x80075830`, then invokes the
level reconstruction routine `0x8001364C`. It does not clear the level's durable
collection array or collected treasure. On a normal Town re-entry, key state0
is therefore expected, not necessarily the preceding session's state3.

The same-level life-loss route is separate. `0x8002C858..0x8002C888` decrements
lives `0x8007582C` and selects transition mode4 while lives remain (mode5 when
none remain). Mode4 dispatch at `0x8002EE74..0x8002EE84` invokes `0x800144C8`,
which reloads the current actor data and calls `0x8001364C(1)` at `0x80014520`.
This avoids the new-level key/area clear. The loader's own explicit key clear
at `0x800136B4..0x800136C0` applies to level64, not Town13. Do not therefore
expect the same key-global reset on ordinary Town life-loss as on new entry.

Checkpoint capture `0x80059474..0x80059518` copies the32 live deletion bytes from
`0x800778B8` to checkpoint `0x80077898`. Reconstruction `0x800143D4..0x800143FC`
copies the checkpoint back into live deletion. Post-checkpoint live deletion
bits can disappear during this restore even while durable collected bits remain
set. The six collected class7D rows are still suppressed by the separate durable
loader test above; transient deletion bits alone cannot diagnose duplication.

## Key identity is not a reward identity

Key T107 preserves native donor T78's `+0x3A = 0x7F`, `+0x53 = 0xFF` and initial
`+0x49 = 0`. Its pickup routine sets global key state1 and its runtime row state2,
but does not invoke the collected-bit or deletion-bit writers. In particular,
bit `0x00000800` for row107 is not part of the six-reward mask. The chest's own
green gem alone is stored in global pointer `0x80075758`; collecting that exact
gem sets global key state3 at `0x8003BA1C..0x8003BA2C`. State3 therefore does not
prove that all five controller gems were collected; use the actual total and
all six durable bits.

The traced generic reload path does not permanently suppress this key row from
the six reward bits. A fresh key may be visible/recollectible after the chest is
already collected; that alone is not proof of duplicate treasure or an import
omission. Its visibility and global-state behavior should be recorded during
the actual reload test rather than invented as an additional permanent-key
save bit. Do not delete it or change its native classification to satisfy an
assumed expectation.

No native persistence collision or missing save relocation was found for this
fixed114-row recipe. A future generic importer must explicitly account for the
native `<256` emitted-reward owner limit: the existing spawn callback falls back
to owner0 outside that limit. This exact recipe stays within it; arbitrary
larger row tables are not authorized by this evidence.

## Fresh native code certificates

EXE file offsets below include the `0x800` PS-X EXE header.

| Native code | EXE file offset / length | SHA256 |
| --- | --- | --- |
| Live deletion marker `8003B7C0` | `2BFC0 / 94` | `a8378b5d829c8a0661affbb6e50eb64b606c3114e116a38ec1b7137ba8a30ec4` |
| Award and collected bits `8003B854` | `2C054 / 180` | `333f5d194c140bcf1be0a97c556a1d39ad2efb56cf70871c6da5bba23ed6b426` |
| Loader collected-row consumption `800142C8` | `4AC8 / E8` | `6a87e3ae9fbe63a4cee7a33dba019c95da6915a7e69319322f641b77205d1c1d` |
| New-level reset and reconstruction `80015CF8` | `64F8 / 80` | `cfedc8a9ee762683b0cc911480c9eddf87ff8675cb09e7b478e77e9d47b0e032` |
| Placed-row unlink/deleted marker `80052568` | `42D68 / 60` | `69876cc4d93769acb12bc4449c217da1a8b0c7d0b4fe8cee76deef9a7e0f6db9` |

Runtime acceptance remains separate. Capture totals, all six collected bits,
row states, level/slot, lives and key state before and after normal exit/re-entry
and same-level death; also confirm normal camera/collision and visible absence
of duplicate reward spawns. A cold boot with a new game deliberately starts a
fresh collection history and is not a persistence test by itself.

## Observed normal exit and re-entry

The parent runtime test reported a normal exit and Town re-entry at 03:46:52 UTC
in the resumed session, using the unchanged exact V3 candidate. Read-only RAM
observations were:

| Observation | Result |
| --- | --- |
| Level / native slot | 13 / 3 |
| Reconstructed placed-row table base | `0x8017E0B0` |
| Lives | 4, unchanged |
| Town collected treasure / HUD / target | 10 / 10 / 210 |
| Actual global treasure | 13: the ten family treasure plus three original Artisans gems collected approaching the return portal |
| Town persistent collected word `0x80077974` | `0x0003F000` |
| Live deletion word `0x800778C4` | 0 |
| T108–T113 | All six `+0x48 = 0xFD` |
| Key T107 / global key state | Active / 0 |

This is the predicted separation between permanent reward collection and
new-entry transient/key reset. The six reward sources did not respawn after
normal exit/re-entry; the active key is consistent with its separate native
non-reward identity and is not evidence of duplicate treasure. The global 13
does not imply an incorrect family award: the level-specific Town value and all
six source bits remained 10 and `0x3F000` respectively. The parent owns the exact
RAM/screenshot artifacts and main runtime narrative in
[runtime evidence](native-closure-runtime-20260905.md).

Same-level death and a memory-card save/load remain separate pending gates;
this re-entry result must not be relabeled as either.

## Optional native death-route research

A source-only scan finds native special-surface index 2 near the outside of the
starting courtyard. Town's actual special record 2 is type 0 with both parameters
zero; for example original collision triangle 6351, WAD `0x12D9378`
(V3 `0x12DA378`), has flag 2 and vertices
`(122560,93504,7680)`, `(123312,94880,7680)`, `(125824,94528,7680)`.
Its center is approximately 6300 raw XY units from the native landing. This
identifies a nearby native hazard surface, **not a traversable route through the
courtyard railing**. Do not infer a failed death mechanic from jumps that never
reach that surface, or spend the audio/runtime window forcing this optional
route without a clear practical approach.

The closest original class `0x17` bull is T1 at
`(111892,111677,9728)`, approximately 17400 raw XY units from the native landing.
Its unchanged source dispatch at `0x8007DB54..0x8007DB58` reaches native handler
`0x8007E598`. Following the ordinary level route to an existing bull is an
optional gameplay alternative to the courtyard wall, not a source-proven
navigation path or guaranteed death within a prescribed time. This investigation
did not alter any actor, collision, save bit, input, RAM or candidate byte to
make a death test easier.

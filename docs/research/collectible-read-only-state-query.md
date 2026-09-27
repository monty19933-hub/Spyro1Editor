# Read-only collectible state query

September 27, 2026. `NativeCollectibleStateQueryKernel` emits a detached
MIPS-I leaf for one exact manifest/ordinary-slot/bit identity. It grants no
native hook, Ready, row deletion, live memory/card ownership or publication.

## Purpose and contract

The existing pickup caller tests proximity first. A reloaded earned egg outside
pickup range returns NotEligible without reading the ledger and stays drawable.
The [scheduling regression](japanese-egg-pickup-scheduling.md) executes that
counterexample. Duplicate prevention during pickup is not reload suppression.

The query takes `a0=exact compiled ledger base`, `a1=unsigned stable bit ID`.
It reads no player/row state. Results in `v0`:

| Value | Meaning |
| --- | --- |
| 0 | Available: active and unearned, **not collected**. |
| 1 | AlreadyEarned: active earned identity. |
| 2 | Retired: inactive historical identity, distinct from already-earned. |
| 3 | OutOfRange: unsigned ID outside the compiled lifetime. |
| 4 | InvalidLedger: pointer or validated ABI/manifest/state mismatch. |

One compiler path shares the collecting leaf's complete pointer/ABI/manifest/
mask/padding/count checks. Only generation-overflow checking and mutation stores
are omitted. Every path writes zero bytes. Bad pointers/IDs refuse before reads.
An unearned bit at generation FFFFFFFF remains Available, while collection
correctly refuses overflow; an earned bit remains recognizable for suppression.

Input/saved registers, SP/RA and HI/LO retain the existing contract. No calls or
stack frame; reads stay in the compiled live ledger extent, excluding guard and
tail. Card/session controls are not authenticated here. Concurrent mutation must
be excluded: read-only is not an atomic snapshot of a multiple-store update.

Construction is private. Independent review closed an initially internal
arbitrary-bytes constructor that could have mislabeled collecting code as a
query. Review also confirmed unchanged instruction/label order for collecting
mode; its emitted sizes remain3CC/744.

## Verification

Tests first failed with eight missing query-type/result errors. Final Release:

```text
NATIVE_COLLECTIBLE_COLLECTION_KERNEL_OK checks=3456 codeBytes=3CC maximumCodeBytes=744 sourceIo=0 runtime=false save=false
NATIVE_COLLECTIBLE_STATE_QUERY_OK cases=233 checks=3461 codeBytes=388 maximumCodeBytes=700 writes=0 nativeAuthority=false
```

The unchanged strict delay-aware CPU and independent ABI fixtures cover
malformed headers, exact manifest/slot isolation, mask/padding, retirement,
duplicates, overflow, unsigned IDs, empty/dense1408-ID states, random earned
states, registers, bounds and full no-write complements. Private construction
and result-value meanings are regression-tested.

The exact-source scheduling smoke additionally finds AlreadyEarned on the
out-of-range counterexample without stores/player reads. Test-only previsual
query → native placed FD deletion → first native callback/draw-list construction
suppresses the earned row without visual-private initialization. Scheduling
passes77 source checks and13 source-free checks. This orchestration is **not
an installed hook**. Retired rows need an explicit suppression-only binding.
Live selected-save admission, lifetime/concurrency, card transactions and native
inventory/% integration remain required.

Root reran hydration896, pickup caller384, query3461/233 cases, scheduling77/13,
and runner contracts104. Logs:
`_local/collectible-state-components-20260927-r01.F5z2Sz/`.

```sh
dotnet run --project src/Spyro.Editor.NativeCollectibleCollectionKernelSmoke -c Release
```

No app/ROM/emulator/card writes are needed for this source-free test.

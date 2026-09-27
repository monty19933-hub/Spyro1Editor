# Bound egg lifecycle callables

September 27, 2026. Detached code composition, not installed gameplay or save
integration. The unchanged packaged editor is still R08. This implements the
local previsual/postvisual operations identified in
[the scheduling investigation](japanese-egg-pickup-scheduling.md), using the
existing exact-row binding, query, pickup predicate and collection compiler.

## Implemented behavior

`NativeJapaneseEggLifecycleCallables` emits two closed MIPS-I entry points:

- **BeforeVisual** validates the exact row and ledger pointer, then queries
  earned state without reading player proximity. Available returns10. Active
  already-earned calls the source-pinned native placed-delete helper and
  returns11 (Suppressed), without changing the ledger.
- **AfterVisual** validates the same structural preconditions, calls the
  existing pickup attempt, and deletes the placed row only on Collected0 or
  AlreadyEarned1. It preserves the attempt result across the helper. Every
  refusal, ineligible result and generation overflow leaves the row unchanged.

Both entry points require the bound private pointer, authored class, live
state0, exact non-grid indexFFFF and exact final dynamic-base global before
calling any child. Thus the native helper80052568 follows its negative-index,
placed-row path, writing only FD at row+48. Other negative indices are refused.
Generic kill8003B9D4 and grid/dynamic-free-list paths are not admitted. The
complete100-byte helper owner has SHA256
`602fc31b1351212e5d5c72c39ac59cc86a9afd0226a9a0549192855588498505`.

The final placed/dynamic boundary derives from the existing source-bound scene
allocator and final placed count. Source-form dynamic rows are zero; the native
loader creates the runtime sentinel. An initial overly strict source-FF check
failed the fixture and was corrected to this count/allocator ownership rather
than changing source data or weakening the runtime dynamic-base check.

The two functions preserve a0/a1, s0-s7, k0/k1, gp/fp/sp/ra and HI/LO. They use
an explicit20-byte hexadecimal frame, plus the existing attempt20 and nested
predicate20: maximum60. Visual work executes sequentially, not nested inside
the pickup. The ordinary scheduling calculation therefore becomes
`801FFFF8-main20-update20-dispatcher1C0-adapter2B0-max(turn28,lifecycle60)` =
**801FFAE8**, leaving2E8 above801FF800. This is not an IRQ/lifetime certificate.

Manifest, ordinary slot, ledger and child entries must agree. Complete runtime
extents for the functions, child code, native helper, models, scene, ledger,
player state and helper globals are pairwise checked. Fixture/source ancestry
cannot be upgraded or downgraded. Byte copies are defensive. These checks do
not allocate native RAM or establish an exclusive runtime operation.

## Regression evidence

The new smoke initially failed with22 expected missing-type errors. After
implementation and a narrow independent review, both routes passed:

```text
LIFECYCLE_CODE source=False bytes=398 before=800D0000 after=800D01D8 dynamic=8008AC2C
JP_EGG_LIFECYCLE_SOURCE_FREE_OK checks=367 dispatch=false save=false
LIFECYCLE_CODE source=True bytes=398 before=800D0000 after=800D01D8 dynamic=80174480
JP_EGG_LIFECYCLE_SOURCE_OK checks=734 dispatch=false save=false
```

734 is the cumulative367 fixture +367 original-ROM checks, not734 additional
source cases. Code size/addresses above are hexadecimal. The synthetic code
addresses are explicitly test-owned, not allocations inside the live game.

The unchanged delay-aware CPU executes both complete call chains. Tests cover
IDs255 and256 across a bitmap-byte boundary; both eggs collected in one retained
ledger; repeated callback rejection; restored source rows suppressed while the
player is out of range; other-row preservation; every written byte belonging
to the declared stack/ledger/row; nested-frame canaries; no proximity/collection
on previsual query; structural refusals before any child call or ledger/player
read; wrong class/private/live/index/dynamic base; invalid state; overflow;
source/fixture mismatch; wrong manifest/slot/ledger; capacity/alignment and
whole-composition overlap refusals. Original JP/USA sessions are read-only and
rehash their held and named sources after execution.

Run `dotnet run --project src/Spyro.Editor.NativeJapaneseEggLifecycleSmoke -c
Release` for the source-free gate. Append `-- --source <JP-BIN> <USA-BIN>` for
the explicit regional source route. The default regression runner now requires
the source-free success marker; its104 contract tests pass.

## Remaining installation gates

No adapter edges or on-disc bytes are patched. A final code/overlay owner,
complete complement/inverse proof and callback-level execution remain needed
to install these functions. Previsual here means before the egg visual helper,
not before the callback's earlier shared animation-list pass. The separate
scheduling oracle covers those consumers; the callable alone does not.

The caller must keep rows, selected ledger, globals and stack exclusively owned
throughout the operation. The collector's four stores and the later FD store
are not atomic. There is no Ready issuer, native save loading/transaction,
inventory display or percentage consumer. Active-only bindings still refuse
retired manifest identities; a historical suppression-only mapping is separate.
Cold boot, selected-save admission, lifetime/IRQ/reentrancy, card durability and
actual DuckStation pickup/reentry/save-load tests remain required. No universal
Moby placement or collectible-enabled editor build is claimed.

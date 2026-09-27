# Authored egg pickup scheduling: bounded source seam

2026-09-27. Test-only exact-source scheduling work; no Core behavior change,
native hook, disc, app, emulator, settings, card or Ready/residence issuer.
The startup-only full-firmware observation does not grant those authorities.

## Concrete result

The smallest local post-visual seam is the existing adapter's `restore-frame`
entry **`80088B14`**, immediately after the transplanted visual fragment/turn helper and
before restoring the dispatcher registers and tail-jumping to `80085780`.
At this point `s3` is the exact current row and the existing `0x2B0` adapter
frame still owns all saved registers. A future typed composition can call the
existing bound TryCollect, then native placed-row deletion on a successful
collection, and finally use the unchanged restore/continuation. This is a
source-backed local recipe, **not** a newly installed hook or lifetime proof.
The exact owned exit edge is `80088AE4..80088AEC` (J/NOP to `80088B14`). The
separate pre-visual edge is `80088C3C..80088C44` (J/NOP to `80088A38`), after
the full frame is saved. Neither edge has been patched by this task.

Visual writes are row Z `+14`, yaw `+46`, timer `+49`, private baseline/phase/
target and RNG state. They do not write live-state `+48`. Native placed deletion
`80052568` writes exactly `FD` at `+48` on the verified negative-grid-index,
below-dynamic-base path. The following `80085780..800857CC` continuation loads
the cursor from original dispatcher `sp+188`, reads the next row, and eventually
restores the dispatcher frame. It has no store to the retired row.

Retirement is not a replacement for checking the native path preconditions:
exact bound row/private/class and scene generation, live `+48=0`, signed
`+34<0`, and row address below the current dynamic base `g80075890`. Those must
remain stable across the ledger update and deletion. Generic kill/effect
`8003B9D4` remains excluded.

## Actual ordinary ordering

1. Main calls update at `80012230` (`8003385C`). Ordinary update invokes the
   loaded Artisans callback through `g80075734` at `80033AA4`.
2. Artisans callback `8007D9C8` reserves `1C0`. It calls update-list builder
   `80051FEC` at `8007D9F0`, then animation `800522C0` at `8007DA04` (and may
   make a second animation call at `8007DA30` when signed `g800756CC>=3`;
   exactly three selects argument `80000001`, otherwise `80000000`). Tests
   execute tick inputs 1, 3 and 4 and observe one/two/two calls before visual.
3. It walks the cached update list at `800700F4`. At `8007DA54`, `lbu +48`,
   load-delay NOP, `sltiu 80`, branch-delay NOP skip all states `>=80`, including
   FD and FF, before class dispatch. Live zero rows reach the authored guard
   and full-register visual adapter.
4. Native continuation `80085780` advances that cached update cursor. Next
   callback's `80051FEC` rebuild excludes negative states in both its ordinary
   scan and grid-list append path. The retired row is absent before animation
   and visual dispatch on that next callback.
5. Main calls rendering `8001ED5C` at `8001227C`, after update (unless the
   existing main-loop skip flag suppresses rendering). Normal mode zero takes
   the `8001EF80` branch and calls draw-list rebuild `800521C0` at `8001EFC8`
   before its later render calls. FD is omitted. Thus post-visual retirement
   precedes this same-frame normal draw-list construction.

This is the ordinary synchronous route, not every mode/transition/IRQ path.
The real GTE/frustum and unrelated actor handlers are not emulated by this test.
Its native callback fixture narrows the row-table global to the two consecutive
source-authored rows and explicitly supplies visible-list input. Model packages,
loader projections, native initializer, callback, animation, row continuation,
placed deletion and draw-list builder remain their existing exact bytes.

## Two counterexamples that constrain the hook

**Cached list:** deletion changes neither the current update list nor an already
built draw list. Calling native animation with the stale update list still
writes the retired row's `+3C/+40` animation fields; animation does not recheck
`+48`. FD itself survives. Directly invoking the visual helper after FD likewise
still changes visual fields without resurrecting `+48`. Therefore the marker
alone is not evidence that every current-frame consumer has stopped.

**Already earned after reload:** a fresh source-form scene row has live state
zero. With a valid ledger that already contains its active earned bit, the
existing TryCollect returns **NotEligible (9)** when the player is out of range,
never reads the ledger, never calls the collector and leaves the row eligible
for the draw list. Merely retiring on Collected/AlreadyEarned cannot implement
reload suppression. Death/reentry source `800144C8..80014564` calls scene read
`80016500` and initialization `8001364C`; this oracle models restoring source
scene bytes, not completion of that actual CD operation or all transitions.

Manifest **Retired** is distinct from an active **AlreadyEarned** identity. The
existing row binding refuses a retired identity; the collector's inactive-mask
result is Retired (2), including when a historical earned bit remains set.
Neither result should be conflated with missing identity, bad ledger or an
out-of-range native row. No active binding is fabricated for a tombstone.

## Minimum next hook recipe

- Before visual initialization/proximity testing, query the exact Ready ledger
  using the bound persistent full-width ID, independent of player position.
  Valid active-earned state requires suppression; historical inactive/retired
  identity requires a separate typed suppression-only mapping/decision. The
  current active-only TryCollect binding cannot supply that mapping.
- For an active unearned row, run the existing visual tick, then use the
  post-visual/pre-restore seam for TryCollect. On Collected, perform only the
  source-proven placed deletion after validating its path preconditions.
  An AlreadyEarned race/fallback must follow an explicit suppression policy.
  Refusals/overflow must not retire the row or fabricate progress.
- Restore the existing full adapter and continue exactly at `80085780`; do not
  patch a shared draw loop or low-class effect path. Do not add the two nested
  call stacks together: visual and pickup execute sequentially.

The ordinary stack arithmetic is:

`801FFFF8 − main20 − update20 − dispatcher1C0 − adapter2B0 − max(turn28, attempt20+predicate20) = 801FFB08`.

That is `308` above the unchanged stack-reservation floor `801FF800`, not an
IRQ/full-renderer/global-fit certificate. The deepest pickup saved word starts
at adapter-SP minus `30`; the reserved nested extent is the full `40`, including
outgoing area. Native retirement allocates no frame on the admitted path.

Still needed before any installation: typed exact overlay/call-site/code owners
and complement/inverse proof for the composed hook, independent earned/retired
suppression binding, Ready/session/ordinary-slot ownership through the entire
operation, and exclusion of IRQ/reentrant/loader mutation. The collector's four
successful stores and the later FD store are not atomic; this work supplies no
interruption recovery or card transaction.

## Focused regression

`src/Spyro.Editor.NativeJapaneseEggSchedulingSmoke` uses the unchanged
`RenderProbeCpu` and existing typed source issuers/compilers. Its manual ledger
bytes are an oracle fixture, not a production initializer or serializer. No
test alters the shared CPU. A dedicated friend declaration is the sole Core
addition.

Default mode executes the delayed native-state gate against six state values
and checks sequential stack arithmetic without reading sources. Source mode:

```sh
dotnet run --project src/Spyro.Editor.NativeJapaneseEggSchedulingSmoke -c Release -- \
  --source '/Users/john/Desktop/Spyro the Dragon (Japan)/Spyro the Dragon (Japan).bin' \
  '/Users/john/Documents/Codex/2026-04-24/could-you-create-a-level-editor/Spyro the Dragon (USA).bin'
```

The source regression pins complete instruction windows, executes the native
callback to the seam, calls the actual existing predicate/collector, performs
native FD deletion, restores all adapter GPRs and HI/LO, completes native row
iteration, rebuilds draw/update lists and executes the next callback. It also
executes both counterexamples and checks retired-identity refusal/no-write
classification. Original JP/USA sources are reauthenticated on all exits.

Final Release results: zero warnings/errors; source-free **13 checks** and
exact-source **71 checks** passed before the separate state query was added.
The expanded suite now passes **77 source checks**: the
[read-only query](collectible-read-only-state-query.md) recognizes the earned
out-of-range row, and test-only query-before-callback/native FD suppression
excludes it from the first update/draw lists without initializing its visual
private data. This still does not install a wrapper. The executed nested predicate prologue
reached `801FFB08`, and the complete adapter restoration retained all GPRs and
HI/LO after actual collection plus native retirement. These tests model the
seam by pausing/resuming the unchanged CPU at instruction boundaries and
calling the existing detached callables; they do not emit/install the future
wrapper or execute BIOS, IRQ, real CD reload, renderer/GTE or save/card I/O.

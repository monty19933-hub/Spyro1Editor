# Native card-menu egg-data loading: detached integration

September 27, 2026. `NativeCollectibleCardLoadPlan` connects the existing
MIPS-I envelope/SHA validator to the exact USA selected-save and shutdown
seams in a detached overlay/executable pair. It is **not published to a disc**,
does not write a card, and does not copy the prepared shadow to live Ready RAM.
It is a concrete integration checkpoint, not persistent collectible gameplay.

## Native sequence

The source behavior comes from the independently pinned
[card-read completion investigation](collectible-card-read-completion.md).
The two changed overlay instructions retain their original NOP delay slots:

| Source seam | Detached replacement |
| --- | --- |
| `8007CA98`, original selected-tail poll JAL | JAL to Begin; return to `8007CAA0` |
| `8007CC00`, original shutdown JAL | J to Gate; pending returns to `8007CC48`, resolved resumes `8007CC08` |

Begin retains the original blocking poll and its actual arguments/output
pointers. That earlier event is not the new request's completion. It admits
only a bound, deserialized selection (intent 1), ordinary slot matching this
compilation, controller 0/16 matching the menu cursor, u16 header marker,
outer phase 2/substate 5 and a valid native menu buffer. New-game/overwrite
intent does not initialize or reset custom state.

Before queueing, Begin requires no active operation and no unconsumed event.
It snapshots the entire selected native `[0,1400)` prefix, captures the tuple
with a checked local epoch, clears the separate read buffer, and calls the
native read API with offset 0/count `2000`. Only accepted return 1 enters
Waiting. A duplicate Begin cannot consume or replace the outstanding request;
it marks it stale and keeps it pending until it can be drained.

Pump uses the real nonblocking poll. Return 0 remains Waiting; -1 refuses
even if old output words look successful. Return 1 must carry operation 3 and
result 0, with no newly active operation. It then rechecks the captured tuple,
phase, menu pointer/arena anchor and all three copies of the native prefix:
captured snapshot, current menu memory, and newly read file bytes. Only then
does it run the actual envelope/SHA kernels against file offset `1400`.
Corrupt, legacy, mismatched and unsupported bytes never become an empty save.

The loader fills only its separate 512-byte shadow. The private control state
becomes Prepared after that succeeds. Rejected paths preserve the previous
complete shadow; Prepared is not a live residence or memory-card certificate.
The local epoch restarts when the overlay is reloaded and is not a globally
unique card identity. An identical native prefix and small marker do not
prove physical-card identity or close the replacement/save-transaction problem.

Gate yields while Waiting, on an unsafe preflight, or on an unknown state.
It calls original shutdown only for Idle, Prepared or Rejected, then resumes
the original instruction after the replaced call. No native phase-3 store is
reached while the extra operation is pending. Polling does not add a cancellation
path or bound the underlying native service's wait time.

## Owned bytes and the discovered anchor conflict

The full original `3800` card-overlay allocation is retained, including its
padding. Named SHA, envelope-loader, control, shadow, state, digest, workspace,
full-block read buffer and native-prefix snapshot owners append after it.
The current complete padded layout is `A800` bytes, ending at `80085238`.
All generated call entries are declared in the existing typed materializer.
The sole overlay changes in the original prefix are the two four-byte call
instructions. Complete exact inverses reject altered afterimages.

Appending the overlay alone is unsafe. Three native EXE paths initialize a
copy anchor to `8007DDE8`, which is inside even the original allocation. Boot
then clears `5A000` bytes from that address; reload paths use it for resource
reads. These operations would destroy the new code before selection.

The plan therefore relocates exactly these three signed-low LUI/ADDIU pairs
to the final card-overlay end, preserving every other executable byte:

| Instruction PC | EXE file offset | Original words |
| --- | --- | --- |
| `80012830` | `3030` | `3C028008 2442DDE8` |
| `8002E2FC` | `1EAFC` | same |
| `80033624` | `23E24` | same |

The native marker-0 exit that resets the copy anchor to the overlay base is
deliberately unchanged: this temporary owner ends with the card-menu phase.
These three patches do not overlap the earlier Artisans lifecycle copy-anchor
or reserve/bootstrap owners. A future combined executable still needs an
explicit checked composition; unrelated afterimages cannot be substituted.

The dynamic menu check requires `menu=E-2000`, `menu>=overlayEnd+3E000` and
`E<=801FF600`, excluding the protected live reservation. Thus the earlier
`[E-40000,E)` intro staging is also above the enlarged overlay. The complete
checked `200`-byte stack window excludes original/native-service memory,
all appended owners, the live reservation and the dynamic menu buffer **before
any frame write or native call**. The generated frame is `50` bytes; nested
generated loader/SHA depth is `C0` total. The accepted native read's observed
local call chain needs `50` bytes beneath its own entry SP. These are not
proofs for interrupt, BIOS, diagnostics or every later resource phase.

Entry 2's original disk allocation abuts entry 3. Enlarging its directory
length in place is not safe; real image materialization requires relocation
and independent full-disc verification. This plan does not implement that
publication or grant complete runtime RAM/lifetime approval.

## Regression evidence and limits

Missing-type RED preceded implementation. Review found two concrete stack bugs:
menu overlap was checked after frame writes, and a Pump preflight rejection
could lead Gate to shutdown. The menu-alias regression failed before the fix;
both now have Begin/Pump/Gate checks. A second hostile regression demonstrated
stale valid read-buffer reuse; clearing the owner before each new request
fixed that case. Independent re-review found the stack fixes sound. The native
prefix check also now detects same-pointer, same-marker in-memory changes.

The focused Release command below passes **1,363 source-free checks** and
**1,827 cumulative checks** with a separate original-ROM route. Coverage includes all
three slots, repeated pending polls, exact read ABI, consumed-once events,
all observed result-error codes, wrong operation, missing/stale completion,
unexpected new busy work, duplicate selection, tuple/pointer drift, native
prefix mismatch, corrupt envelopes, complete write footprints and protected
live-ledger preservation. Manifest sizes up to 1408 fit; 1409/4096 are refused
without truncating the unchanged detached format.

```sh
dotnet run --project src/Spyro.Editor.NativeCollectibleCardLoadSmoke -c Release
dotnet run --project src/Spyro.Editor.NativeCollectibleCardLoadSmoke -c Release -- '/Users/john/Documents/Codex/2026-04-24/could-you-create-a-level-editor/Spyro the Dragon (USA).bin'
python3 -B tools/test_usa_card_read_completion.py
```

The CPU tests execute the complete original `80067628/F0` poll function,
the emitted controller/loader and every SHA round. Read acceptance and
shutdown are explicitly stubbed boundaries in the offline state-machine cases;
completion globals represent supplied boundary states, not executed callbacks.
The source route separately runs all three actual anchor initializers and the
complete original boot clear, proving both its destructive original footprint
and preservation after relocation. Its 92,160 loop iterations are resumed at
retired instruction boundaries to retain the shared CPU's unchanged 200,000-
step per-call budget; no host memset, register edit or fabricated return
replaces the clear. The first attempt exceeded that budget, and the original
boot fixture initially omitted s0 setup; both test-harness issues were corrected.

The source read-API test reaches its first filename-helper call with actual
generated arguments and source register/frame/admission instructions. It stops
there: the helper uses LWL/LWR unsupported by this bounded interpreter. No
helper success was substituted, and the shared interpreter was not altered.
Actual filename formatting, queue/callback/IRQ/BIOS behavior, service transfer
length, disc boot and native card durability remain runtime gates.

The independent Python oracle passes 14 source-free tests and authenticates
51 source windows, 60 exact instruction contracts and 31 callback targets.
Both tools and the new source-free C# suite are normal regression entries.
The integrated default Release regression passes **261 checks, zero failures**:
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-41fou6r2/summary.json`.
Log `153-run-Spyro.Editor.NativeCollectibleCardLoadSmoke.log` records the
1,363-check marker; the separate explicit source command produced 1,827.
No originals, normal cards/settings, R06/R07/R08 packages or test discs were
modified; no DuckStation session was launched for this checkpoint.

Next: compose the relocated card overlay and all owned EXE patches into a
separate independently verified candidate; establish phase handoff into the
reserved ledger and explicit new-file initialization/durable save behavior.
Inventory X/X, completion percentage, signs, universal placement and flight
save/reload remain within the full active milestone, not claimed by this work.

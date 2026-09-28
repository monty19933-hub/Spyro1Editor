# IRQ-windowed resident scheduler successor — unimplemented

Design handoff against clean `462b6c5`. No production implementation, build,
disc, card, emulator, installation or runtime evidence is added by this document.
The separately tested IRQ controller is not currently selected by any workspace
or scheduler factory. This is not permission to publish a writer candidate.

## Exact bounded composition

Add a closed `ControllerSchedulerIrq` workspace profile and explicit
`CaptureControllerSchedulerIrq` / `CaptureResidentIrq` factories; names are
proposed, not existing APIs. Reuse the source-issued IRQ controller and existing
private geometry-receipt → regenerated scheduler → final data/hash sequence.
Do not accept caller bytes, helper addresses or a caller-asserted IRQ capability.

Retain the `8800` insertion, controller `7300`, immutable scheduler owner
`8016F060/1000`, overlay state `800891F8/40`, `E240/5C0` gate owner and untouched
`FDF8/208`. All numbers here are hexadecimal unless stated otherwise. Recompile
branches/tables; never relocate bytes. Hard-fail either code-owner overflow.
The new emitted size/hash is **not measured**. Existing factories, hash recipes,
buffer addresses and ordinary exports must remain byte-exact.

Production scope is the existing workspace plan, scheduler kernel/plan, plus the
one explicit profile allowlist in `NativeJapaneseEggOverlayProjection`. The
current allowlist must not be bypassed. Keep the existing actual-instance,
complete-source ancestry, current projected globals, immutable-owner byte
checks, immediate-parent inverses and publication refusals.

## Stack and helper ABI

Use the exact typed IRQ controller's existing Enter/Leave helper PCs only after
the overlay guard has admitted its complete final model owner. Those helpers
use frame offsets `40/44/48/4C` for token/held/helper-RA/saved-v0. Therefore the
conservative reuse design requires a **50-byte scheduler frame**, not its
current `30`. Initialize held to zero after safe frame allocation. Do not borrow
the native argument home area or fifth argument at `SP+10`.

Let `S` be SP at the patched `800873D8` particle epilogue, after the native
particle `A0` frame was restored at `873D4`. The source path is native main
`20` → update `20` → save handler `48` → restored particle epilogue; its retained
caller extent above S is `88`. The nested IRQ controller is `50` plus its
preparation maximum `A0`, i.e. `F0`. Consequently:

- Lower bound: `50 + F0 = 140`; exclude **[S−140, S+88)**.
- With live reservation ending at `801FF800`, accept aligned S only in
  **[801FF940, 801FFF78]**. At the lower endpoint, controller-entry SP is
  `801FF8F0`, exactly its own lower bound.
- Existing `801FF910` is insufficient. Even replacing only the controller
  would require `30+F0=120`; it cannot retain the old `110` assumption.

This is a modeled synchronous bound, not BIOS/IRQ/setup stack certification.
Native `662BC` setup remains a separate unproved BIOS boundary. The helper
protocol preserves v0/RA and exits only for saved token1; token0 must stay
disabled. Reconstruct service arguments and reload transient registers after
helper calls. Preserve the scheduler's native saved registers, HI/LO and return
continuations. Do not mask around whole Begin/Pump/Drain entries.

## Required scheduler windows and cleanup

1. Fresh state0: mask the initial Busy/Latch/empty-queue snapshot and arm
   publication. Release before `662BC`. After setup and context validation,
   reacquire and recheck Busy/Latch/queue before publishing op2 Intent and
   calling `665B8`. Keep request return, receipt, Busy/op and latch checks inside
   the window. The known state1/2 path must never be adopted as fresh ownership.
2. Precheck/header polling: enter **before the first Busy read**. Keep the
   matching latch/operation check, mode1 poll, raw return, Consumed receipt and
   all pending/completed postchecks masked. Precheck is op2, with existing
   accepted completion results0/3; header is strictly op3/result0.
3. Header request: queue/idle check → Intent → `66E28` → raw return/receipt →
   native postchecks remain one window. It may continue the just-completed
   precheck window; do not accidentally acquire a nested token.
4. Successful header: verify consumed ownership and exact marker, then release
   **before** original serializer `59864`, controller Begin, bulk preparation,
   SHA or copies. Active Pump and terminal controller Drain use their own short
   internal windows, not an outer scheduler mask.

The concrete cleanup trap is the overlay `context` leaf: its failures branch
directly to overlay `blocked`, bypassing `resident-blocked`. Merely adding Leave
to resident exits can therefore leak a mask. Conversely, unconditional Leave
in overlay cleanup would fetch model code on a fresh guard refusal.

Required routing: a **framed overlay-only cleanup shim** reads only its owned
held slot. Held0 goes directly to existing overlay blocked/finish restoration;
held1 routes to a resident close-window helper, then returns to the unmasked
overlay restoration. Resident blocked/finish routes close the window too;
repeated Leave is inert. Pre-frame `skip-unframed` must never inspect an
unallocated frame or fetch tail code. Preserve zero tail reads, writes **and
instruction fetches** for fresh/global/stack/pre-dispatch context refusals.
Context failure after a window has already started may execute its admitted
resident cleanup, but must restore the token before leaving that owner. Once armed,
refusal stays HELD; never resume the retail writer. Flow5 still does not drain
precheck/header ownership, release buffers or authorize rearm.

## Required RED → GREEN gates

- Legacy source-free/source hashes remain exact; new profile cannot enter old
  factories or substitute a byte-equivalent issuer. Complete original EXE,
  overlay/data complements and exact inverses remain checked.
- Stack endpoints `801FF940`/`801FFF78` accept; adjacent aligned
  `801FF938`/`801FFF80` and former lower bound refuse before any tail access.
- Token0/1 at every pending, completed, rejected, wrong-op, stale tuple,
  marker, generation and context-failure branch; especially context failure
  immediately after native request/poll. Verify frame/ABI and full write sets.
- Instruction-level deferred completion between Busy/Latch/receipt/stage
  operations; prove no owned-mask leak, premature enable or retail write.
  Count windows, not cycles or latency. `662BC`, serializer and bulk work must
  remain outside scheduler-owned windows, including prior-disabled callers.
- Execute actual emitted admission/pickup, fully materialized final workspace,
  precheck/header scheduler and all five controller transfers. Reuse original
  serializer and accepted synchronous request/poll source tests; model IRQ
  veneers and host transfers explicitly. Verify full600/100 readbacks, opaque
  suffix/other slots, dirty retention and terminal-only proposal semantics.
- Preserve clean-save passthrough, unarmed state1/2 refusal, owned mismatch
  retention, reset/no-rearm and complete source-loader geometry tests.

## Source pointers and remaining boundary

- [Scheduler emitter](../../src/Spyro.Editor.Core/Analysis/NativeCollectibleSaveSchedulerKernel.cs):
  `Emit`, overlay `context`, `blocked`, `finish`, resident exit labels.
- [Scheduler source owner](../../src/Spyro.Editor.Core/Analysis/NativeCollectibleSaveSchedulerPlan.cs):
  `Evidence`, `CaptureResident`, current-final owner/global checks.
- [Workspace](../../src/Spyro.Editor.Core/Exporting/NativeCollectibleSaveWorkspacePlan.cs):
  profile selection, private geometry receipt, final materialization/hash.
- [IRQ controller](../../src/Spyro.Editor.Core/Analysis/NativeCollectibleSaveControllerKernel.cs):
  `FrameByteLength`, `MaximumStackByteLength`, critical source pins and helper ABI.
- [Projection allowlist](../../src/Spyro.Editor.Core/Exporting/NativeJapaneseEggOverlayProjection.cs),
  [IRQ CPU tests](../../src/Spyro.Editor.NativeCollectibleSaveControllerSmoke/IrqWindowCases.cs),
  [prior save source audit](collectible-native-save-next-slice.md).

Exclusive foreground/native-service ownership, explicit session revocation,
DMA exclusion, cache visibility, asynchronous retention/reclamation and actual
BIOS/IRQ behavior remain unproved. The controller's narrow drained-preparation
Busy/Latch read exception remains conditional on that exclusive owner; do not
turn it into a general unmasked native-global exception. No live commit,
UI Saved7, reset/rearm, installation or publication is included in this plan.

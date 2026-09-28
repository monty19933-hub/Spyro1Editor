# Native collectible saving: next bounded slice

September 27. No native custom writer is installed or enabled. Admitted Artisans
pickup and native Quit Game have passed the narrow exact-disc R02 check; the
disposable card retained its seed, so that run does not prove saving the pickup.
Keep `DetachedCollectibleSaveCodec` as the format oracle and reuse native SHA
and envelope validation; do not add a streaming SHA abstraction for this tail.

## Transaction boundary

Capture the exact ordinary slot0 bytes after serializer return800321FC, along
with the live bitmap, native tuple, epoch and mutation generation. Native
success7 at8003231C/32324 must wait for the complete custom transaction. The
shared80032320 instruction is also an error-path target;8003232C clears the
native UI timer g78D04, not the card completion latch g75B58. Replacing only
the success pair is insufficient. The shared
epilogue800324B8 restores registers and SP+48.800320C0 is an internal driver
body, not a proven standalone callable entry.

New exact original-only source pins:

- 800321FC/+3C: `5b10b96c51df659639cfa1f76e2842c18acbf8b6202127a97aaec83b0dc95af6`.
- 80032314/+18: `1742cd0f5996ac7606b2eecbba2fd2d7d74c0bf2f36238b923e3eeee87f4fdcc`.

Offsets and sizes below are hexadecimal. Read and validate the old tail before
preparing its replacement; its complete envelope digest must match live+50.
Reject legacy/unknown, corrupt, different-manifest or changed-ancestry tails.
For the fixed two-ID recipe, the131-byte envelope occupies file[1400,1483),
with slot bitmaps1460/1461/1462 and SHA trailer[1463,1483). Hash the preceding
63 bytes (99 decimal), then hash the complete83-byte envelope (131 decimal).
Change only slot0; preserve the125-byte suffix[1483,1500) exactly in the100-byte
aligned transfer. Other ordinary slots, header and remaining tail are untouched.

Require native accepted1, then consumed completion1, operation4 for write or3
for read, result0 and drained busy/latch. Compare ordinary[200,800) against its
entire captured600-byte expected image, then compare the complete expected100
tail. A valid native checksum is not exact ordinary-byte equality. Clear dirty
only after both readbacks and unchanged tuple/epoch/generation/bitmap. Retain
newer mutations as dirty; update the committed digest only in the same session.
Ordinary plus tail in-place writes are not crash-atomic.

## Still-unproved ownership

Card-overlay code/SHA/loader are overwritten; the cold bootstrap is BSS-erased;
the guarded Artisans overlay already reaches10000. Existing200-byte live ledger
and reused[F-600,F) frame scratch are not spare transaction storage. First prove
a resident or leased code/scratch owner and complete caller-stack exclusion,
including native48-byte frame. Retain the exact600 ordinary snapshot or prove
an equally strict alias schedule: current SHA accepts at most400 input bytes,
so it cannot silently substitute a hash of the600-byte ordinary slot.

The detached preparation and non-I/O reference transaction slice below now
exists. A successor emitted controller is under detached CPU regression;
lifetime ownership, native hook integration and real card execution do not exist.
Tests execute admission→pickup→snapshot, equal-checksum but
different ordinary bytes, every padding byte, other-slot preservation, rejected
and stale completions, wrong operations, partial writes/readbacks, selection/
epoch/reset changes, new mutations, and no premature success/dirty clearing.

Existing evidence: `usa-save-buffer-ownership.md`,
`tools/Inspect-SpyroUsaCardReadCompletion.py`, codec and native SHA/loader sources.
This audit grants no card I/O, residency, runtime, inventory or percentage gate.

## Completed detached preparation and reference transaction

`src/Spyro.Editor.Core/Analysis/NativeCollectibleSaveTailKernel.cs` emits the
fixed-two-ID, slot 0 preparer, reusing native state-query, envelope validation
and SHA kernels. Its own code is 0x710 bytes (1,808 decimal); the complete
detached arena is 0x3BA0, including 0x3000 code capacity and 0xBA0 preparation
buffers, snapshots and SHA workspace. The nested stack bound is 0xA0 plus the
excluded native caller's 0x48 frame. These are bounded arithmetic owners, not discovered
free RAM or a native asynchronous lease.

The emitted instructions validate reserve 0xA00, Ready/dirty live state at 801FF600,
native selection, manifest/ABI, opaque epoch, generations and the complete old
envelope digest. They snapshot both inputs, preserve the other two slot bitmaps
and all 125 decimal suffix bytes, and recheck inputs/selection before publishing.
Every refusal preserves both inputs and the previous complete output; temporary
scratch/declared stack may change. Success clears an old prepared marker before
rewriting its record and publishes the new marker last. Host inspection binds
the complete captured live bytes, old tail and prepared digests to the exact
kernel instance. Neither inspection nor the marker grants execution/I/O authority.

`src/Spyro.Editor.Core/Analysis/NativeCollectibleSaveTransaction.cs` is a C#
reference model, **not an emitted or executed native transaction controller**.
It consumes supplied request/completion receipts and detached bytes, enforces
the accepted 1 / completed 1 / op 3-or-4 / result 0 contract and both full readbacks,
and returns only a detached commit proposal. In-memory file simulation verifies
header/other-slot/tail protection; it never opens or writes a real card.

Two reviewed stale-state fixes were recorded RED→GREEN:

- Every Request/Poll/Commit ledger observation tracks the highest observed
  generation and earned bitmap. After observing actual pickup 01→03 / gen 1→2,
  replayed gen 1, removed earned bits (even at a higher generation), and changed
  bits at the same generation refuse. Comparing only with the initial snapshot
  had allowed rollback and premature dirty clearing. Valid newer mutations
  remain dirty when the saved baseline advances.
- `InvalidateSession()` is explicit, idempotent and permanently terminal across
  all five model APIs. Tests execute a fresh admission plus the same pickup to
  reproduce the entire prior ledger, including its opaque epoch, then refuse
  that byte-identical re-admission at seven transaction checkpoints. The caller
  must revoke before selection/reset/card re-entry/replacement ends its session.
  No native revocation hook or nonreusable lease exists; epoch/tuple equality
  does not authenticate identity. Invalidation neither cancels native I/O nor
  releases buffers, changes live RAM or undoes an already returned proposal.

Latest `Spyro.Editor.NativeCollectibleSaveTailSmoke` results: Release and Debug
builds had zero warnings/errors and 4,494 source-free checks each; explicit USA
source mode passed 4,505 checks, eight source pins and unchanged-source validation.
The smoke executes emitted admission→guarded pickup→preparation code, including
every envelope-byte corruption, every opaque suffix byte, all other-slot bitmap
combinations, malformed tuple/ABI/stack and interleaved input changes. Reference
model checks include all 0x600 ordinary bytes, equal-checksum differences, all 0x100
tail bytes, partial/wrong/stale completions and dirty-generation retention.
The latter are model evidence, not native card-controller or runtime evidence.

## Completed detached Artisans workspace projection

`src/Spyro.Editor.Core/Exporting/NativeCollectibleSaveWorkspacePlan.cs` captures
the actual source-certified admitted composition and same complete USA source;
it refuses a different composition instance even when its bytes match. It adds
one nonregistered 0x5000-byte model tail, with 0xC leading alignment, the 0x3BA0
preparer arena, separate 0x600 expected ordinary and 0x600 ordinary readback
buffers, 0x100 tail readback, 0x100 **proposed/unmeasured** future controller state,
and 0x654 trailing
padding. This is not proof that a future native controller fits.

Measured model length grows 0x50000→0x55000 with roots/registrations unchanged.
Scene start shifts 80167D54→8016CD54 and end 8017C554→80181554, leaving 0x645C below
the normal 0xA00 floor 801879B0. The plan accounts for the full scene, not just the
larger tail-only gap, preserves all retained descriptor payloads/header
complements, and provides exact inverses to both admitted parent and original
USA data. Detached final WAD 10 data SHA-256:
`9e50753a043722117f7843acf1a3d249191c847351fa518c4fada6cc870c7bc4`.

Latest `Spyro.Editor.NativeCollectibleSaveWorkspaceSmoke`: 35 source-free and 149
explicit JP+USA source checks. CPU tests execute original model/scene request
argument construction, authored registration/helper windows, scene placement,
row/private fixups and initialization, preserving the entire added tail. CD
completion is modeled by copying exact descriptor bytes, not real I/O. Relocated
preparation code also executes the unadmitted refusal path without writes.

The existing guard/callables still embed the old scene/row/private addresses;
the plan reports required rebindings but does not install them. Installation
explicitly refuses. Native residence, hooks, asynchronous card lease, lifetime,
cache visibility, IRQ safety, full original-model reference closure, full runtime
peaks, future-controller fit, card writes and publication all remain unproved
or disabled. Static source ownership is not an asynchronous lifetime guarantee.
No disc/card was published or mutated by either smoke; no save-runtime gate
or normal Create BIN route is enabled by these slices.

## Residency audit: rejected shortcuts and next owner

Later source inspection identifies the enclosing save function as800314B4,
not320C0: it allocates48 bytes and shares the324B8..D4 epilogue. The direct
EXE caller33998 is dispatched in game mode11. Before selecting its save
handler,31DBC..31E30 calls3CB24,49660,49E8C and the indirect particle callback
g756BC. Saving therefore does not establish an idle overlay/scratch lease.
The existing egg live guard requires mode0 and is not a save-mode guard.
Additional original-only pins:314B4/+A0 =
`4013a8848b14d06d9d7fd8a5d9362937428e513e1c8b8da5ee9181ed20f8c4c8`;
33950/+D0 =
`ba9945221373f4cc42e28fe38c6278d66bff237134da4b2423a117042bbf3cf3`.

Do not enlarge the globalA00 reserve for this slice: unchanged Twilight
Harbor retains onlyBC static gap, so even a600 ordinary snapshot exceeds it
by544, before code/workspace. Existing Artisans overlay padding plus unused
guard capacity totalsCA4 and is already owned, not spare space; two600
ordinary buffers and a100 tail exceed it before code. Boot BIOS heap bounds
also overlap separately loaded engine arenas and grant no safe lease.

The next defensible experiment is a separate nonregistered model-tail owner
for the fixed Artisans composition, structurally like
NativeJapaneseSignFontResidentPlan, with distinct code and mutable buffers.
Current model start80117D54/length50000 leaves a prospective tail80167D54;
current scene end8017C554 hasB45C static gap below theA00 floor801879B0.
A6000 insertion would leave545C, but this is only a sizing example.
Use measured, aligned, disjoint final extents instead of assuming that size.

Model roots remain unchanged; scene/rows/private pointers shift. Re-run the
complete current-final projection, live guard and bindings; no raw append to
the published candidate. Normal full-model-read source windows159FC/+48,
15AD8/+A8 and15B80/+58 underlie the existing font allocation proof.
Require exact descriptor/complement/inverse and executed loader/scene
initialization tests proving new code/workspace survives normal loading.

Static fit does not prove asynchronous ownership. Before issuing any pointer
to native card I/O, prove cancellation/reset/reload/transition draining or
prevent invalidation until completion. A later guard cannot repair DMA into
reused memory. Native callback/IRQ stack, complete caller48 frame, cache
visibility and transfer peaks remain separate blockers. A source-only
preparer or a reference transaction model cannot issue those guarantees.

## Detached controller and integration boundary

`NativeCollectibleSaveControllerKernel` emits Begin, Pump and Revoke entries.
It calls the source-pinned read/write/poll ABI; current tests substitute emitted
service stubs and a host byte-array card. Begin snapshots the exact supplied
0x600 ordinary image and live ledger. One Pump performs at most one nonblocking
poll and one subsequent request. The sequence is old-tail read, ordinary write,
ordinary readback, tail write and tail readback. Both readbacks are full byte
comparisons; known readback buffers are complement-prefilled. Old-tail receipt
semantics still must guarantee the complete transfer because unknown padding
cannot independently detect a lying partial-success receipt.

Success produces a detached ledger proposal only. It does not update live RAM,
publish native UI success7, install a hook or grant a lease. An actual ordinary
serializer-produced snapshot remains an integration requirement. Early pickup
before preparation conservatively refuses before writing; later pickup remains
dirty. Per-observation generation/bitmap high-water and external terminal Revoke
prevent replay of previously observed state, but native hooks must actually
signal revocation before reset/reselection/unload. Revoke does not cancel or
drain an accepted request; all pending buffers must remain owned.

Final focused controller results: Release and Debug each pass3,881 source-free
checks with zero build warnings/errors; explicit USA source mode passes3,890
checks and unchanged-source verification. Existing preparation smoke still
passes4,494. Independent source/ABI review found and fixed an unchecked filename:
all18 bytes, including the terminator, are now checked before every request,
with90 byte/stage corruption cases. These counts include queue refusal,
high-water/revocation, register/stack and complete write-complement checks.
Runner contracts pass111 tests after mandatory source-free and opt-in USA-only
registration. Actual controller code is0x166C (5,740 bytes), below its0x2000 cap.
No native I/O or emulator save is implied by these CPU-service simulations.

The source queue index at g800751B0 is a signed last-enqueued index, initialized
to -1 at80068F30. Helper80068F44 fails to enqueue when the next index reaches4,
but read/write wrappers can still return accepted1. The controller guards every
new request with unsigned `(index+1)<4`, admitting only -1/0/1/2. This avoids the
known false-accepted full-queue path under exclusive ownership; it is not atomic
queue admission or proof against IRQ mutation. Source pins cover the initializer
and complete helper. Existing pending requests are not released merely because
the queue subsequently becomes full.

The complete controller arena is0x7300, including its own0x2000 code capacity,
the complete0x3BA0 preparation arena, three separate0x600 ordinary buffers,
0x100 tail readback, two0x200 ledger images,0x30 state and fixed filename owner.
Its0x40 frame plus preparer's0xA0 nested stack totals0xE0, excluding the native
caller's0x48 frame. Accepted native request paths add at most0x50 below the
controller frame; diagnostic/BIOS/IRQ/asynchronous stack paths remain unproved.
The prior0x5000 workspace projection cannot contain this arena. No existing
published candidate is silently expanded or rebound.

The next source scheduling candidate is an Artisans-owned overlay gate reached
from callback80086134's restored epilogue800873D8/873DC. It must check the exact
mode11 caller return31E08 and final loaded geometry before touching model-tail
code or state. Level setter5A718 installs the callback before overlay/model
loads; callback presence alone does not authenticate loaded geometry. A typed
successor must explicitly reassign an existing alignment owner, not treat zero
bytes as free memory. Native state2 takeover must precede the header completion
poll and original serializer59864; state3 is too late because ordinary writing
has already started. Once owned, no path may fall back into the retail writer.

Before publishing any save disc, execute the actual callback/enclosing0x48-frame
path and prove before-unload/reset/reselection barriers retain all I/O buffers
until drained. Native shutdown6631C spins on busy and is not bounded cancellation.
Real completion/commit, cache visibility, card replacement and full stack safety
remain separate gates. In-place ordinary and custom-tail writes are still not
crash-atomic.

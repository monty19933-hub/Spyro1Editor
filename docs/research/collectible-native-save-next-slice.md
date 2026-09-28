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

An additional source-only serializer test now executes original59864 and its
exact16914/16958 clear/copy and5956C checksum helpers for four seeded ordinary
states. All0x600 bytes match an independent format model, including retail
overlapping egg fields, checksum at58C and zero padding590..600. Only the owned
buffer and original0x20 frame are written. A clearly marked host capture feeds
that exact image into the emitted controller's simulated transaction; no native
adapter performs the copy yet. This adds31 checks, bringing the explicit USA
controller route to3,921; its source-free count remains3,881. Independent source
review confirmed the serializer mapping and original-image verification passed.

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

The existing workspace plan now has a closed `CaptureController` profile,
sharing its descriptor, ownership, inverse and loader implementation with the
unchanged preparation profile. Minimum controller growth is0x7800 **from the
admitted parent**, not appended after the prior0x5000 experiment. Model start
stays80117D54; model length becomes57800. Arena80167D60..8016F060 is preceded by
0xC alignment bytes and followed by0x4F4. Scene becomes8016F554..80183D54, leaving
0x3C5C below the unchanged normal0xA00 floor801879B0. Every code-capacity gap,
buffer and padding byte has an explicit owner; the fixed filename is initialized
from the pinned source. Actual model/scene loader windows preserve the complete
tail, and its loaded Begin refuses an unadmitted ledger without a card call.

Release and Debug each pass70 source-free checks (preparation35/controller35)
and328 explicit JP+USA source checks (preparation149/controller179). The original
preparation data hash remains unchanged. Controller-profile WAD10 SHA-256:
`a27e8e5848f6cb99cdb0ea5c60aa93e4571b4721db665d48a8877f898a525b7e`.
Both complete original images remain unchanged. This proves the detached
allocation and loader contract, not current pickup-guard bindings, native cache,
asynchronous lease, real card I/O or publication. The same plan still refuses
installation; corrected nonuniform model/scene rebinding is the next slice.

The following three-file geometry correction now exists in the shared
`NativeJapaneseEggOverlayProjection`, `NativeJapaneseEggCollectibleBinding` and
`NativeJapaneseEggLifecycleOverlayPlan`. A closed controller-workspace factory
consumes the exact workspace/ancestor instance. Model displacement relative to
the authored E800 baseline stays1800; scene displacement becomes9000. Legacy
uniform `Delta` refuses this nonuniform route rather than moving model roots
incorrectly. Binding now uses the final data domain/hash, model descriptor
length and shifted row/private extents. Existing emitters regenerate only
attempt, lifecycle and live-guard bodies in their same owned capacities and PCs.
All other overlay bytes, old factories/hashes and existing publication guards
remain unchanged. This is still detached; old startup/admitted/publisher routes
deliberately reject it rather than inheriting new authority.

New lifecycle smoke passes162 source-free and417 explicit JP+USA checks in
both Release and Debug. Existing LifecycleOverlay248/571 and AdmittedLifecycle
156/342 Release checks pass with old goldens unchanged. One CPU image executes
actual admission, then loads the complete model/workspace/scene bytes and
executes native callback suppression and pickup plus simulated row reload;
the entire registered model,
controller tail, overlay and other rows survive unchanged. All three stale
parent globals refuse before calls/writes. Independent review found no scoped
blocker. New overlay SHA-256:
`5e5d06273e0c0bbdc393e12fbf11e64544eb5cf4800e275e1933429fecb6926e`.
Data remains the controller workspace hash above. Guard entries stay8008A030,
8008A060 and8008A090; dynamic rows move to8017D480. Projection's retail0x800
reserve gap3E5C is not the workspace's stronger0xA00 gap3C5C. No new disc was
published and no emulator or card was opened by these checks.

Integrated default Release regression after controller/workspace/rebinding and
serializer integration: **286 checks, zero failures**. Report:
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-awrs4fah/summary.json`.
The runner's112 source-free contracts pass, including mandatory registration of
both new smokes and explicit exclusion of the dual-region lifecycle route from
the single-USA source registry. This does not rebuild a preserved app package.

## Pending-request receipts and bounded terminal drain

The controller now records a fixed request kind, intent, raw request return and
raw poll return in its previously reserved state20..2F. Intent precedes the
native call; accepted/rejected/indeterminate receipt publication precedes live
validation or revocation checks. Matching consumed completion is likewise
recorded before later validation can fail. Failed/Revoked no longer loses the
identity of an accepted outstanding transfer. These bytes remain private
exclusive-owner state, not authenticated session or card identities.

The new Drain entry accepts only Idle/Committed/Failed/Revoked, returns a
separate drain result, performs at most one mode1 nonblocking poll and never
issues a request. It bypasses invalidated live/session data, preserves terminal
stage and proposal marker, and cannot publish native success. Error completion
can prove that a transfer was consumed without proving that saving succeeded.
None/Rejected/Consumed receipts do not consume foreign busy/latch state. Intent,
unknown return, wrong operation or lost completion remain pinned; no timeout is
treated as cancellation. The caller must return from tail code into its retained
overlay before considering any release. No rearm entry exists.

Independent review and root review identified the same two-call defect: a
pre-poll contradiction initially returned Indeterminate without poisoning its
Accepted receipt. A later matching foreign completion could then appear owned.
New regressions demonstrate the RED case and require permanent Indeterminate
receipt after that contradiction, including idle/no-latch and foreign operation
followed by a matching latch. Clean None/Rejected/Consumed observations retain
their receipt but still cannot grant release while foreign activity exists.

The complete pinned retail poll67628/F0 executes in the CPU tests, including
pending, success and error completions for all five fixed request kinds. Card
transfer services remain mocked; this is not native I/O or interrupt execution.
Focused Release and Debug each pass4,398 source-free checks (3,881 existing
+517 receipt) and4,439 explicit USA checks including the original serializer.
Workspace70/328 and rebinding162/417 source-free/source checks pass in both
configurations. Code is now1FB4, leaving4C bytes inside the
unchanged2000 capacity; arena7300, state30, frame40 and nestedE0 are unchanged.
Default-fixture code SHA-256:
`9a523808f739324211d576def64a81f9932440196abaa941da105b220a378a52`.
Controller-workspace WAD10 SHA-256 now is
`700e0f54859f0a2759595d2b69e7f91ebeea720ef096de1910a7b6ba3196dd3a`;
the earlier a27e... golden above belongs to checkpointcd0e7c3. Only emitted
controller bytes changed; loader geometry, old preparation profile and the
recompiled lifecycle overlay5e5d... stay unchanged. Nothing is installed into
a disc or packaged editor by this slice.

Integrated default Release after receipt/Drain: **286 checks, zero failures**;
report `/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-fvgf0ulx/summary.json`.
The normal DuckStation settings, per-game settings and USA card, plus both R08
App/Core assemblies, still match their protected hashes. DuckStation remains
closed; no save writer is active.

The next source scheduling candidate is an Artisans-owned overlay gate reached
from callback80086134's restored epilogue800873D8/873DC. It must check the exact
mode11 caller return31E08 and final loaded geometry before touching model-tail
code or state. Level setter5A718 installs the callback before overlay/model
loads; callback presence alone does not authenticate loaded geometry. A typed
successor must explicitly reassign an existing alignment owner, not treat zero
bytes as free memory. The earlier state2-only proposal is superseded below:
fresh state0 ancestry and owned header submission are required. State3 is too
late because ordinary writing has already started. Once owned, no path may fall
back into the retail writer.

Before publishing any save disc, execute the actual callback/enclosing0x48-frame
path and prove before-unload/reset/reselection barriers retain all I/O buffers
until drained. Native shutdown6631C spins on busy and is not bounded cancellation.
Real completion/commit, cache visibility, card replacement and full stack safety
remain separate gates. In-place ordinary and custom-tail writes are still not
crash-atomic.

Normal-mode retention audit narrows the next hook: use the same Artisans
particle epilogue, not an unconditional EXE321FC jump into level memory. The
snapshot semantics remain post-serializer. The final bounded source review
requires observing/owning native state0 rather than adopting an already-running
state1/2. State0 calls662BC (queue initialization and native busy/result/latch
reset), then665B8 submits precheck operation2. Retail ignores that submission
return too. State1 accepts precheck completion1/op2/result0-or3; an extension
must record fresh intent/acceptance and require drained native state before
submitting its own header read. These observations are not an installed gate.

The exact header request is66E28(controller, fixed filename, owned buffer,0),
with length80 at callerSP+10. The proposed buffer is the controller's existing
OrdinaryInput[0..80), not borrowed retail frame scratch. Only after completed
1/op3/result0, busy0/latch0 and matching marker at+7E may the gate call original
59864 into the same complete600 owner, then Begin. This avoids a second host-like
copy and prevents serialization while the header transfer remains outstanding.
Retail32158 ignores header acceptance and unconditionally advances to state2;
neither a native state number nor a repeated selection tuple authenticates a
fresh request. All active custom paths must return to324B8, skipping the retail
ordinary write at32230. Terminal live commit, UI result and repeat-save rearm
remain separate implementation work.

Independently checked USA source pins (addresses/lengths in hex):

| Owner | SHA-256 |
| --- | --- |
| 662BC+60 initialization | `e23d1f4697a7870a5e1e16faef2d1f384818bd3771c69619b2388bdf1ef9fed9` |
| 665B8+7C precheck | `40b72a2bba8ce136e0b4fa5516b6c19bc54495fecd5339e04c9de30d7e011a32` |
| 66E28+10C read submission | `87ba1be6dead52b5b297e0587aad7c37f99b5676e497988c0846859e802f9b6d` |
| 67628+F0 poll | `19553c82e1ac2ff3a3089e3c8b101ade63a08c9620216785f79df4a2d2da3e21` |
| 320C0+DC retail states0/1 | `7e080f0b172aa16b0507420b318a62464533b72d74b34c170259a5e78c292162` |
| 3219C+60 retail header verification/serialization | `12bf74e81aa25da733a13d7b3919c589ecd8afaad0d417c69c25d0d1871342b0` |

Instruction visibility is also explicit unfinished work. The source BIOS
A0/44 stub is626E8; its four direct executable callers are SDK initialization
paths, not a demonstrated normal-level-load flush. A flush called from the
new gate could precede model-tail execution, but cannot by itself establish
initial visibility of that newly loaded overlay gate. Neither loader CPU tests
nor the existence of this stub grants cache/BIOS/full-stack certification.

### Interrupt exclusion: concrete remaining race

The controller's declared external-exclusion requirement is substantive. A
foreground Drain can read Busy3, receive the completion interrupt, then read
Latch1 and correctly refuse its now-contradictory observations even though the
actual transfer was its own. Pump can similarly poll pending just before that
interrupt and then fail its post-poll checks. Receipts alone do not make these
multi-instruction observations atomic.

The source's5DBA4/5DBB4 syscall veneers implement Enter/Exit critical-section
requests. Native67EA0 saves Enter's v0 token and calls Exit only when that saved
token equals1; cleanup680A4 does likewise. A future wrapper must preserve its
arguments, result and token and must not unconditionally enable interrupts.
This is the game's source protocol, not a BIOS implementation or nesting proof.

On admitted, guarded paths,665B8/66E28/670E4 perform bounded RAM helper work and
enqueue;67628 mode1 is call-free and nonblocking. A masking interval must cover
the first Busy/Latch/queue sample through intent, call, receipt and all following
stage/postcondition checks. Masking just a JAL is insufficient. Current opaque
Begin/Pump/Drain entries permit a paired outer wrapper, but Pump also performs
preparation/SHA/copies, so interrupt latency still requires measurement. Native
completion must be allowed between entries; blocking poll mode0 cannot be used
inside such a mask. Diagnostic/error paths remain outside the accepted-path
stack proof.

Do not include initial662BC setup in that claim: its callback registration
resolves to the source leaf5E5D8, but67EA0 opens/enables BIOS events. Accepted
request closure is not authority to mask that setup wholesale. CPU interrupt
exclusion also does not prove DMA exclusion, card identity, cancel safety,
firmware stack bounds or code visibility.

Source pins:5DBA4/10
`505f018057cab576fce3e80a248467f5c8ad5e14c7d2289869e453ad092ef931`,
5DBB4/10
`6e003f701ca622421a42bea9b13f252e5325ecafcb0e85216766a154e060e578`,
67EA0/1E4
`b05c14dbd524a156943c4147bd54f12eb05bb2d005341417f2cfb498d0f330b1`,
680A4/B8
`7ce5526e956186a87f3b39c3ab35aa0a5d6383c684deceb10eb98132c452d4ec`.

All inspected normal save UI, retry, error, shutdown and success branches are
after the31E00 callback. The registered controller callback53C68/+67C, CD
callback16490/+70, exact Artisans tick86134/+12AC and its81-entry dispatch table
showed no legitimate pending-save mode escape. This is support for a bounded
normal-game barrier, not arbitrary RAM-mutation or all-BIOS/IRQ proof. Other-mode
reload handlers existing elsewhere are not themselves a reachable counterexample.
The original particle callback has restored SP before873D8. With that SP=S
and new wrapper frame=W, require `[S-W-E0,S+88)` inside `[801FF800,80200000)`:
main20+update20+save48 is88 above S, not just the controller's assumed48.
ParticleA0 and controllerE0 execute sequentially, so their depths take a maximum.

The receipt/Drain implementation above is a prerequisite, not the scheduler
itself. Repeated-save entry2CCC8 resets retail fields only; one-shot controller
rearm still needs a separate proven post-drain/commit boundary.

### Actual accepted request wrappers and emulator cache boundary

The source smoke now executes the pinned read66E28 and write670E4 wrappers,
their filename prefix67D74, strcat687D4, strlen6276C and enqueue68F44 closure.
Sixteen combinations cover both controller ports, all four admitted queue
indices, read100 at1400 and write600 at200. Tests compare complete request
globals, filename, queue record and retained complements, the fifth stack
argument and restored registers/HI/LO. Synchronous maximum stack is50. This
does not execute a BIOS service, asynchronous callback or memory-card transfer.
Release and Debug each pass4,636 source-free/4,845 USA-source checks, including
238 merge-instruction checks and168 actual-wrapper checks; original3,881 and
517 receipt checks remain. The shared CPU gained tested MIPS-I little-endian
LWL/LWR/SWL/SWR and matching pending-load cancellation, not full CPU certification.
Controller code and workspace data hashes remain unchanged.

Installed DuckStation reports0.1-11826-gfe2306b1f; inspected source is commit
`fe2306b1f0f7dd64cbc9aa8eb12269715ba799b5`. Its RecompilerICache option models
cache tags/stall timing, not cached instruction bytes. CachedInterpreter shares
that block mechanism. R02 used Recompiler/RecompilerICache=false and therefore
does not establish stale-instruction visibility. A future visibility test must
use ExecutionMode=Interpreter from a fresh boot, without switching modes at the
transition (switching clears the cache). Interpreter fetches cached KSEG0 data,
but can recover certain invalid/reserved stale instructions by retrying RAM;
it is still not strict hardware cache certification. No settings were changed.

Primary source: [block reads](https://github.com/stenzek/duckstation/blob/fe2306b1f0f7dd64cbc9aa8eb12269715ba799b5/src/core/cpu_code_cache.cpp#L997),
[ARM64 timing](https://github.com/stenzek/duckstation/blob/fe2306b1f0f7dd64cbc9aa8eb12269715ba799b5/src/core/cpu_recompiler_arm64.cpp#L788),
[cached fetch](https://github.com/stenzek/duckstation/blob/fe2306b1f0f7dd64cbc9aa8eb12269715ba799b5/src/core/cpu_core.cpp#L3020),
[mode change](https://github.com/stenzek/duckstation/blob/fe2306b1f0f7dd64cbc9aa8eb12269715ba799b5/src/core/cpu_core.cpp#L2178),
[fallback bypass](https://github.com/stenzek/duckstation/blob/fe2306b1f0f7dd64cbc9aa8eb12269715ba799b5/src/core/cpu_core.cpp#L3087),
[stale-invalid recovery](https://github.com/stenzek/duckstation/blob/fe2306b1f0f7dd64cbc9aa8eb12269715ba799b5/src/core/cpu_core.cpp#L2084).

### Detached normal-save scheduler

The new source-bound scheduler joins the retained Artisans particle epilogue
to the original save serializer and emitted controller. It is not installed in
any disc. The two existing typed padding owners E240/5C0 and FDF8/208 contain
main5C0, helpers/table1C8 and state40, with zero spare bytes. No overlay/model
growth or copy-anchor shift occurs in this slice. The full parent EXE and
workspace data remain exact; all unassigned overlay bytes and the complete
inverse are checked. Fixed slot0 and two authored eggs remain the only profile.

Only the expected31E08 return, mode11/handler2, complete source-projected
globals and bounded restored stack may enter. Unarmed refusal—including a
clean seed dirty0/gen0—retains the original native path. After fresh state0
ownership, every return skips the retail writer at32230. The scheduler owns
SDK initialization, accepted operation2 precheck, accepted80-byte header read
and matching file marker, then invokes actual59864 into the full owned600-byte
buffer and calls Begin/Pump. Raw request acceptance precedes subsequent checks;
precheck result0-or3 and header result0 are the only successful completions.
Unobserved native state1/2 is never adopted.

Success deliberately remains terminal HELD with a detached proposal. Flow4
calls the controller's bounded Drain; flow5 after failed precheck/header keeps
its receipt and buffers permanently, without drain/cancel/release. It cannot
claim Saved, copy the proposal over live state, rearm or authorize an unload.
Later actual pickups retain their live dirty state. Tests include equal-checksum
but different600-byte readback, foreign completion, stale queue, hostile stack,
source-global mismatch, marker mismatch, return interleaving and native ports0/16.

Focused Release and Debug each pass157 source-free/336 explicit JP+USA checks.
Source mode executes the exact composed overlay and original serializer; native
I/O services and the card are mocked. Independent review found no blocker in
this detached scope. The mandatory runner adds this source-free gate and keeps
the dual-region route out of single-USA source QA; its113 contract tests pass.

Detached overlay SHA256:
`b79a2fac3bd08fa0bf80e726b198084ec3d7b3ee967e569fb1f7e1afad86a57d`.
Unchanged workspace data SHA256:
`700e0f54859f0a2759595d2b69e7f91ebeea720ef096de1910a7b6ba3196dd3a`.
Next use an explicitly allocated scheduler owner for IRQ exclusion, header
draining, live commit and repeat-save lifecycle—not additional unidentified
padding. Cache, native session/retention, full stack and exact-disc gameplay
remain required before installation/publication. R08 is unchanged.

### Instruction measurements and combined native request closure

The test CPU now counts dispatched instructions monotonically across split
Run calls. A separate217-check,32-case repeatable measurement set records exact
state/receipt identities without changing the established controller totals.
For the current fixture, Begin executes17,609 instructions; completed Pump
stages1–5 execute98,670/20,520/20,534/7,740/11,417. Drain pending/complete are
255/312. These include emitted mock-service instructions and exclude host
transfers; they are not cycles, latency, worst-case bounds or approval to mask
an entire entry. Stage1 calls SHA four times. Use short internally revalidated
poll/request/Drain windows, leaving bulk preparation/copies outside the mask.
Preserve the caller's Enter token and never enable an already-disabled caller.

A separate308-check USA-source test executes all five controller transfers
through the actual pinned synchronous read/write wrappers, filename/queue
helpers and nonblocking poll, including both native ports0/16 and a later real
emitted pickup. It compares native request globals/queue entries, full ordinary
and tail images, retained card/source/live complements and ABI/write bounds.
Transfer bytes, queue retirement and interrupt-completion globals are explicitly
simulated by the host; no asynchronous native callback, BIOS or card I/O runs.
This closes the combined synchronous ABI test gap without promoting runtime.

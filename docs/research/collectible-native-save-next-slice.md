# Native collectible saving: next bounded slice

September27 source audit only. No writer is implemented or enabled. Complete
admitted Artisans pickup runtime testing before promoting a save candidate.
Keep `DetachedCollectibleSaveCodec` as the format oracle and reuse native SHA
and envelope validation; do not add a streaming SHA abstraction for this tail.

## Transaction boundary

Capture the exact ordinary slot0 bytes after serializer return800321FC, along
with the live bitmap, native tuple, epoch and mutation generation. Native
success7 at8003231C/32324 must wait for the complete custom transaction. The
shared80032320 instruction is also an error-path target;8003232C clears native
completion state. Replacing only the success pair is insufficient. The shared
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

The smallest implementation is a detached tail-preparation kernel plus native
transaction-controller smoke, followed by source-issued residency and hook
integration. Tests must execute admission→pickup→snapshot, equal-checksum but
different ordinary bytes, every padding byte, other-slot preservation, rejected
and stale completions, wrong operations, partial writes/readbacks, selection/
epoch/reset changes, new mutations, and no premature success/dirty clearing.

Existing evidence: `usa-save-buffer-ownership.md`,
`tools/Inspect-SpyroUsaCardReadCompletion.py`, codec and native SHA/loader sources.
This audit grants no card I/O, residency, runtime, inventory or percentage gate.

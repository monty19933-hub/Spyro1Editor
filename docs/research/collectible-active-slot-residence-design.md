# Fixed-profile collectible residence: selected slot, not three-slot envelope

September 25, 2026. Read-only design; no Core/schema/card/ROM hooks or changes.
Initial scope is a **private newly created research file only**. No migration,
initialization or modification of the user's existing saves is authorized.

## Smaller actual state

The existing detached format has lifetime cap4096, three independent slots,
header60 and SHA256 trailer20. For lifetime count L, let B=ceil(L/8).
Encoded length is `80+3*B`, not always680. For two lifetime IDs, B=1 and the
complete existing v1 envelope is **83 bytes**, requiring **100 bytes** of
80-aligned native transfer. The full680 size occurs only at L=4096.

Gameplay needs the selected ordinary slot's B bytes, not all three slots.
Other slots can remain on card and be streamed unchanged during a transaction.
"Selected slot" is not "only currently active identities": retained earned
bits for retired IDs still occupy their original positions. Size by
**LifetimeCount**, never ActiveCount; do not remap, truncate or recycle bits.
The trusted authored manifest provides immutable instance-to-bit bindings.
The current two-instance placement has not yet been issued such a native hook.

## Concrete conservative 200-byte additional-reserve proposal

Numbers are hex. This is a proposed layout, not an optimal-size theorem or
issued native owner. Increase g755A4 from800 toA00, not the separate stack-top
wordg755A8. New allocation ceiling T'=801FF600. No PETE growth is needed.

| Offset from T' | Proposed live owner |
| --- | --- |
| 000..010 | Guard: native particle sentinel writes000..004; remainder alignment. |
| 010..020 | ABI/magic, phase, selected slot, controller, dirty/status flags, native file identity. |
| 020..030 | Content GUID. |
| 030..050 | Exact manifest SHA256. |
| 050..070 | Last validated card-envelope SHA256; detect changed source before rewriting. |
| 070..080 | Revision, lifetime count, B, active/earned counts, mutation generation. |
| 080..090 | Transaction generation, operation/chunk state, failure and session epoch. |
| 090 onward | Selected-slot lifetime bitmap, length align16(B). |
| Following bitmap | Read-only active-ID mask, length align16(B), for retirement-aware inventory/counting. |

The mask avoids pretending a large GUID/manifest document is already resident.
It must be compiled from, and bound to, the exact manifest; padding/unassigned
bits must be zero. Earned bits remain stable across retirement. Cached counts
are checked/recomputed, not a replacement for the bitmap.

For the two-egg profile, total live footprint isB0, including guard and both
rounded bitmaps. The full200 reservation remains charged. Under this specific
layout admission is `90+2*align16(B)<=200`, hence at most **1408 lifetime bits**
(and therefore at most1408 active bits); current L=2 fits. This is a physical
profile limit, **not a change to the detached4096 schema cap**. A larger profile
must obtain a larger proved owner or fail native admission, preserving data.

Normal floor moves80187BB0→801879B0. The prior exact Twilight Harbor normal
gap2BC would becomeBC, rather than overlap. That is a static arithmetic check,
not whole-engine/IRQ/alternate/transient fit. RAM reservation need not inherit
disc-file sector alignment;200 preserves word/eight/sixteen-byte alignment.
DMA/loader admission must nevertheless be explicitly checked. The existing
PETE probe's800-byte example is not a certificate for this new200 proposal.

## Streaming and transaction obstacles

The native read/write service requires byte counts divisible by80 and owns
the caller's pointer until its asynchronous callback completes. A proposed
80-byte chunk can use the existing gameplay600-byte scratch **only after** the
ordinary native operation has completed and an explicit hook exclusively owns
that scratch until all extension work drains. No death/level/slot transition,
native retry, or competing operation may reuse it. Freeze collectible mutation
or use a checked generation/snapshot protocol; do not quietly discard new bits.

v1 has one SHA256 over all three slots. First stream-validate the entire source
envelope, including manifest, all slot padding bits and checksum, against the
loaded identity. Then stream-copy the two unselected slots unchanged, substitute
only the selected slot, recompute integrity, and read back the result. The
existing managed Inspect/Serialize methods are whole-buffer functions: a native
streaming SHA256/validator and its code, work RAM and stack owners do not yet
exist. Chunking is a design, not permission to call the managed codec on PS1.
The two-egg checksum begins at63 and crosses an80-byte sector boundary; bytes
83..100 are physical padding, not part of the v1 payload or implicitly owned
legacy bytes. New-file wrapper construction must explicitly own padding.

In-place v1 update is not crash-safe: torn writes can invalidate the checksum
for all three slots. A proposed private new-file wrapper can retain two banks,
each with an80-byte versioned generation/identity/checksummed control sector
and the rounded v1 payload. For two eggs this is `2*(80+100)=300`, within the
C00 tail. Write only the inactive bank, keep the previous valid bank intact,
and choose a new bank only after its complete validation/readback; removal,
retries, torn control/data and ambiguous generations require explicit tests.
No assumption of atomic physical-sector writes substitutes for those tests.

Physical admission must separately require
`2*(80+align80(80+3*B))<=C00`. At the detached4096 cap this needsE00, so it
**does not fit** this one-block wrapper. Even two unwrapped680 envelopes need
D00. Refuse that native profile or allocate a separately owned file/block;
never silently reduce the schema limit. Bank resizing/migration is separate.

Finally, a two-bank extension alone does not atomically pair the ordinary
600-byte native slot write with custom progress. A crash between their commits
can leave mismatched generations. Coupled recovery/journaling or an explicitly
proved reconciliation rule, native success-UI gating, cold-boot initialization,
slot/new-game hooks, and the persistent RAM lifetime remain unimplemented.
Unknown, corrupt or other-manifest tails remain preserved/refused. This note
advances a small fixed-profile design only; it grants no real-save authority.

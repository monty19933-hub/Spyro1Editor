# Source-bound native global sound append

This is a detached, original-derived byte producer. It is not a candidate
publisher, a complete Wizard sound closure, or playback/voice/reverb/RAM proof.
It does not change the original image, existing candidates, or the installed app.

## Source and native consumers

`NativeGlobalSoundSource.CaptureAsync` opens and hashes the actual clean USA BIN
before and after capture: 661547040 bytes, SHA-256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
It requires the native MODE2/2352 geometry, WAD LBA37/length0x6927000,
EXE LBA53875/length0x66000, and exact WAD directory. It discovers all 35 level
owners from that directory, not a caller catalog. A merge reopens/rechecks the
actual source and rebinds the destination's complete data hash at both ends.

The checked executable certificates are:

| Native purpose | Runtime / EXE offset | Length | SHA-256 |
| --- | --- | --- | --- |
| Map, descriptor count/stride, sample-address rebase | 80012CF0 / 34F0 | 68 | ecd95b895b0f1dd85b2aed1c926a6e5e818ac3c41ddaf38565e34459808f89a0 |
| Bank CD read and fixed SPU upload | 800156B0 / 5EB0 | 230 | f3cdb7634a2e36f2329959401df8bc6006cd623bd2ea7233e4ca43ea53e42c15 |
| Player electric-damage selector1F | 80041044 / 31844 | 28 | d0e9d200cbf22489706549be06f9ae873e877c9be6eb1a3fb93b25503ab58721 |

The initialization receives the sound component body (after its size word), sets
the map pointer, advances 0x100 bytes to read the descriptor count, and advances
four more to the descriptor array. Records have stride20. With the rebase flag
set, each first word gains0x1010. The level loader separates the first0x80000
bytes of nested subfile0 (VRAM) from its remaining SPU bank. The native upload
starts at SPU0x1010 and writes0x7EFF0 bytes. The producer bounds the **sector-rounded
bank**, not merely the useful appended samples, against that domain.

## Structural codec and address domains

The sound component is found by a bounded native walk: six size-prefixed data
components, portal count and each portal geometry/sky component, then particles,
then sound. Zero/oversized portal point counts, misaligned/out-of-owner lengths,
and unknown outer headers refuse. No byte-pattern search discovers ownership.

The exact component grammar is a leading total length, 256 map bytes at+4,
descriptor count at+0x104, and exactly count×20 descriptor bytes at+0x108.
Counts are1..256; every map value must name a descriptor. Descriptor zero is an
owned resource, never an empty-slot sentinel.

Sample offsets are16-aligned, **SPU-bank-relative** source words. They are not
WAD offsets, level-data offsets, or runtime pointers. The public placement record
separately exposes the uploaded address0x1010+bankOffset; source-level metadata
separately exposes data-relative and WAD-relative component/bank owners.

ADPCM frames are16 bytes. The codec requires predictor0..4, shift0..12, flags0..7,
and a terminating end-bit frame inside the bank. It includes that final frame.
Identical sample starts share immutable decoded ownership; distinct partially
overlapping sample extents refuse. The bank is bounded by the native upload and
the total unique decode work is bounded. Full component/bank copies round-trip
without interpreting padding or opaque descriptor parameters as free space.

Only simple one-shots may be newly imported: flags0 followed by final1, nonzero
pitch≤0x3FFF, descriptor words+4/+12/+16 all zero. Other retail encodings can be
inventoried/round-tripped, or exactly reused when already mapped, but gain no
implicit append semantics. This deliberately leaves looping and unusual
descriptor parameter behavior to a later native profile.

## Mapping and allocation

`CompileMergeAsync(original, donorId, targetId, requests)` accepts distinct
global selectors0..255. Requests are snapshotted and sorted before asynchronous
source work. The default policy is `RequireEquivalent`: the existing mapped
descriptor parameters and actual sample bytes must already equal the donor.

Changing a selector requires `ExplicitRebindUnresolvedConsumers`. The resulting
binding explicitly keeps `ConsumerOwnershipResolved=false`; changing the map can
affect any existing native user of that selector. The source certificates above
prove a consumer exists, not that every destination/shared-EXE consumer has been
enumerated or intentionally rebound. No publication authority follows.

The append algorithm:

- Reuses an exact existing sample extent when possible. Different descriptor
  parameters/pitch may reuse that sample while requiring a new descriptor.
- Preserves the entire original bank, including original sector padding, then
  appends missing sample bytes. It sector-rounds growth and refuses upload
  overflow before materialization. It never takes an apparently empty bank gap.
- Preserves every old descriptor byte, appends new descriptors before the old
  data-subfile suffix/padding, and changes only the sound length/count and
  explicitly requested map bytes. Descriptor byte-index exhaustion refuses.
- Uses the existing typed `NativeLevelDataGrowthLayout` for simultaneous
  subfile0+subfile1 growth, descriptor/root relocation, and all later subfiles.
  Every original nonheader byte is either an exact protected extent or a named
  map/count/length field. All models, player records, scene rows, properties,
  collision, texture bytes, and original padding remain byte-exact after their
  corresponding relocation.
- Replays the final native component/bank parser and compares each requested
  mapped resource with the actual donor descriptor/sample. Output copies and
  collections are detached/immutable.

The whole final-level byte array is **retail-derived**. It must not overwrite a
destination containing later authored edits. Combining this producer with player
animation/model/scene edits requires a trusted common source-bound composition
step; accepting a modified header as the original is not such a step. Internal
consumers can obtain detached typed insertion payloads plus immutable
**original-address** nonheader field mutations. These never include a wholesale
afterimage or replay the sound-only header over a composed header.

## Electric1F source example and independent oracle

Wizard Peak33 descriptor40 (global1F) is at WAD0x30DE9DC, bytes
`e046030000000000320000080000000000000000`. Its sample begins at bank+0x346E0,
WAD0x301FEE0, length0x1D40, pitch0x800; SHA-256
`b03fdc973a8966a21326691f1546c19bf9e8d40d2eb15b00bec6cc9789599322`.
Dark Hollow12, Town Square13, and Toasty14 map1F to their different, owned
descriptor0; none has an equivalent registered electric sample.

The read-only `source_splice_oracle.py` independently reads raw sectors, walks
the native components, splices the sample/descriptor, updates native typed
header fields, and rehashes the unchanged source. It writes no ROM or payload.
The expected whole-level results are:

| Target | Exact samples / descriptors | Rounded total growth | Whole final-level SHA-256 |
| --- | --- | --- | --- |
| Dark Hollow12 | 1D40 / 1 | 2800 | c6b15c291f3e929d8f3a7f73dcbb535bf4c52f1352fa31282606b69ed526fd6f |
| Town Square13 | 1D40 / 1 | 2800 | 331e93b543205b5063d17d32f6083e63942038a26c8a47dbc8f9c96c1c4838cb |
| Toasty14 | 1D40 / 1 | 2800 | 6b58b78bd39fae6d6307760b94ee2ef90e94ef1d1d2117c81a403037c4e4c745 |

The independent oracle passed for all three targets with original pre/post SHA
unchanged. The initial Debug RED build passed with zero warnings/errors and its
first valid decode failed at the deliberate codec stub. Initial sound-only C#
GREEN: Debug zero warnings/errors; source-free59 checks/31 refusals;
source-backed2619 checks/37 refusals, all35 levels and2457 descriptors.

The extended original-based composition regression combines player animation8
with both sound subfile insertions in one `NativeLevelDataGrowthLayout`. It
relocates the exact nonheader field preimages, verifies every other original
model/scene/padding byte, and compares against independent raw-sector oracle
hashes. Total sector growth is0x6800; no composed candidate is published:

| Target | Animation8 + sound whole-level oracle SHA-256 |
| --- | --- |
| Dark Hollow12 | 3965dd80a3e3492348798554e1658921330f2e32ffdc76d455adc4425791a10d |
| Town Square13 | 26fd7d05216c2bfd727479f0aa5daadde854521a352e3bba37e6da82ee828481 |
| Toasty14 | 20f91001daaa4f60dbaf12cbcd9fa4d950a5a3f8aba2da7157fa65300ef65313 |

Final targeted Debug gate: zero warnings/errors; source-free89 checks/35
refusals; source-backed2702 checks/41 refusals. All six whole-level oracle hashes
match. The source-free suite also refuses tiny and exact-length sparse fake BINs
through the actual public source factory and preserves those generated fixtures
until their explicit test cleanup. No original image or candidate is modified.

The test-only composer refuses replacement or split-field shapes and is not a
general public composition/publishing API. A newly discovered empty player
root0 stream (only its terminator) is explicitly tested as currently unsupported:
inserting at the same address as root0 moves that root past the new record under
the current shared boundary policy, and the player producer refuses its final
record-order readback. The supported synthetic fixture retains animation18;
all35 actual source streams remain covered by the existing player gate. Future
editor-owned empty streams need a producer-owned start-boundary rule. This work
does not relax the shared relocation schema or claim empty-stream merge support.

## Source-issued model-local descriptor allocation

`CompileModelSlotMergeAsync` is now a detached source-backed producer. It accepts
an already source-issued `NativeMobySourceInventory`, binds its donor data owner
to the captured sound owner's full hash/length/WAD entry, and reuses the existing
registration parser with one canonical original address-space object. It never
accepts caller package offsets, descriptor indices or declared hashes as source
authority. The actual BIN is reopened and checked before and after compilation.

The first package schema is deliberately bounded to source Wizard33 class11B.
Slots0/1/2 contain local descriptor indices49/50/51. They are **not global map
keys**. The exact package profile rejects class0, unsupported families, duplicate
or out-of-range slots, FF-unbound source slots and changed complete source bytes.
The native model-slot consumer at EXE28D1C lengthA0 is bound by SHA
`af902cc21e59f2794bc459d6cfaf528f36c28b73f39fc4221c1fe6d384331e91`;
the signed-frame-slot and queued-playback consumer at EXE42AC0 length204 by
`2a3dcb9fa1ba59e0d2e740bcbd912b18155266fee95cf2c2e2f580cfeef72324`.
The bounded frame audit observes49/50 queued from animation1/2 frame0; descriptor51
is conservatively retained because the complete native package declares it.

Source-backed regression first rejected a useful owner mismatch: the logical
model payload at WAD3106518 is35AC bytes (SHA24df9ae279f32ba10a8e3bd25880d6989991b9225acf45755e6aaf985d66f529),
but registration root5→root6 owns35B4 bytes, including eight zero padding bytes
(SHA070a57069c8747ada4caacc0394b93e9a970e5ba7ff21159584abdc7a0fd4faf).
Both identities are bound. The package selector plan preserves that complete
owner; a nonzero padding mutation is refused even if the logical payload is exact.

The neutral allocator is shared with the unchanged global API. Local-only imports
need no dummy global request and preserve all256 map bytes. A combined transaction
sorts resources by donor descriptor index, while the v1 global-only adapter keeps
its previous ordering and all six byte/plan goldens. Existing exact descriptors
can be reused at their original indices, including opaque resources; importing
new loop/unknown semantics remains refused. An FF model output is refused by this
specific unbound-slot profile, not by a claimed 255-entry hardware table limit.

The new adapter permits an explicit unresolved global rebind only for Wizard33
electric1F→Dark12. All other requested globals must require exact equivalence.
Native death selector26 reuses Dark descriptor22 with its actual sample and opaque
parameters unchanged. This restriction does not claim every indirect native
consumer is closed; all global bindings retain `ConsumerOwnershipResolved=false`.

Dark's local49/50/51 samples have lengths5430/20D0/1080. Combined with electric40,
the four samples totalA2C0. Preserving the full old70800 bank allocates donor
40/49/50/51 at offsets70800/72540/77970/79A40 and target indices80/81/82/83;
the final rounded bank is7B000. Map1F becomes80, package+4/+5/+6 become81/82/83.
All80 old descriptor bytes, old bank/padding, and all255 other map bytes remain
exact. The independent raw-sector oracle emits no files and supplies new goldens:

| Detached stage | Whole-level SHA-256 |
| --- | --- |
| Local49/50/51 only | 53d5746a7c08ed65e0f827ed89a51c7a0dd97cb76c4c5c1ead51aa84a32ec0f8 |
| Electric40 plus local49/50/51 | 9cd7dfd29bcf194a1a4b2d1a80c94f466dbbbed736159d9fd140bf113bb8fe0e |
| All four plus player animation8 | d0d83a7fb45204fba26f8248f3f6a3bae20c0de22b7596d98db6f5d6cbff0b90 |

The last stage grows three subfiles byF000 total through original-address payloads
and field edits, preserving every original target model and scene byte. It does
not register or place a Wizard. `CopyTextureComposedPackage` separately accepts
the source-issued Dark texture plan, verifies its exact donor/destination ownership
and every declared package patch, and changes only the three sound slot bytes.
All texture-rebased bytes and the eight owner-padding bytes survive. Its full
35B4-byte package hash is
`e3cd4d67f39eb9375a8d17b4c2830050f33b5523c3421e7eba828af9f6eabe6e`.
That remains a detached package, not a final registered actor or ROM afterimage.

The executed local RED had a clean Debug build followed by the deliberate local
allocator stub failure. The subsequent bounded-materialization RED used finite
adversarial lists which advertise Count1 and throw if enumeration reaches max+2;
the old unbounded snapshot hit that tripwire. Global requests, local descriptor
requests and model slots now read at most max+1 elements and require an exact,
nonnegative count match. Short contradictory enumerations are also refused.

Final targeted local GREEN: Debug zero warnings/errors,
121 source-free checks/56 refusals;16520 source-backed checks/74 refusals,
including the nonzero owner-padding negative. All six prior byte goldens and
the three previous global plan hashes are unchanged. Independent read-only
review found no material issue; runtime playback remains untested here.

## Remaining closure work

Still required: complete selected global consumer ownership; unusual/looping sample semantics where needed;
trusted simultaneous composition with animation/model/scene producers; transient
CD/scratch/RAM and SPU voice/reverb lifetime; native runtime audio acceptance.
`RuntimeFitProven`, `RuntimePlaybackProven`, `CompleteConsumerOwnershipProven`,
and `PublicationApproved` remain false. No candidate has been published here.

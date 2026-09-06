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

## Remaining closure work

Dark Hollow also lacks Wizard model-local sound descriptors49/50/51. Those are
package sound-slot indices consumed by the model-local resolver, not global map
selectors. Their respective sample lengths are0x5430,0x20D0,0x1080. Combined with
electric1F, the four samples total0xA2C0; preserving Dark's entire0x70800 bank then
rounding yields0x7B000, within the upload domain. This is read-only capacity
evidence, not an implemented local-slot importer or permission to rebind globals.

Still required: model-local descriptor installation/remaps; complete selected
global consumer ownership; unusual/looping sample semantics where needed;
trusted simultaneous composition with animation/model/scene producers; transient
CD/scratch/RAM and SPU voice/reverb lifetime; native runtime audio acceptance.
`RuntimeFitProven`, `RuntimePlaybackProven`, `CompleteConsumerOwnershipProven`,
and `PublicationApproved` remain false. No candidate has been published here.

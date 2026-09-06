# Native player-animation stream and structural merge

This adds an actual in-memory resource producer, not a new native enemy or
free-flight publication approval. The original source BIN, installed app and
retained candidates are unchanged.

## Source and representation

`NativePlayerAnimationSource.CaptureAsync(originalBin)` is the only public
source-bound issuer. It verifies the exact 661,547,040-byte original USA BIN
SHA-256 before and after reads:

`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`

It binds the WAD/EXE ISO extents, WAD directory, native loader/table/renderer
certificates, shared player package and every one of the 35 original levels.
No caller catalog, receipt, supplied hash or edited candidate grants authority.
The 181 level-local records round-trip exactly. The shared package at WAD
`0x7B9000`, length `0x32E1C`, has SHA
`46f570bf0ad2d433f8b42874ae40a3e5d7155d7206dddd1cd08a2d4be16e0e76`.
Its 22 populated animation slots and geometry domain at package `+0xAA8` are
distinct from level-local record storage.

The special root-0 stream ends at the next native root and uses:

```text
[s32 animation ID][u32 blob length][blob] ... [FFFFFFFF]
```

`NativePlayerAnimationStreamCodec` is the reusable structural decoder. Its
result is not source-ROM authority. It accepts only distinct IDs 0..45,
four-byte-aligned bounded records, the source-proved type-1 encoding and
positive signed frame counts. It rejects unknown encodings, malformed lengths,
missing/noncanonical terminators, trailing bytes and out-of-owner references.
Both stream and shared geometry have explicit 2 MiB decoding budgets; record
and frame work is bounded by the stream bytes and 46-slot domain.

The native loader at `0x80013230..0x800133DC` distinguishes:

| Field | Source reference | Target owner |
| --- | --- | --- |
| Blob `+0x10` | Byte offset | Local data starting at `blob+0x24+4*frameCount` |
| Blob `+0x14/+0x18` | Byte offsets | Shared player geometry |
| Frame words `+0x24+4*i` | Low 21-bit byte offsets; upper bits are flags | Local data; runtime loader converts to packed halfword addresses |

The codec exposes these as separate typed domains/encodings. It proves aligned
reference starts and minimum target extents, **not every variable-length frame
decoder read**. `FramePayloadDecodingProven` remains false. It retains opaque
payloads and frame flags byte-for-byte rather than guessing or repairing them.

`NativePlayerAnimationRecord.Frames` exposes each exact source word, its low
21-bit local offset, loader-preserved upper bits and highest byte separately.
The native frame sound consumers at `0x8004A668..0x8004A710` skip their direct
sound call when that highest byte is `FF`; the captured source certificate
includes these instructions. All 34 frames of animation 8 and all 11 frames
each of animations 33/34/35 have `FF`. This does not prove absence of sounds
triggered by player states, effects or other consumers.

## Real in-memory merge

```csharp
var source = await NativePlayerAnimationSource.CaptureAsync(originalBin);
var electric = await source.CompileMergeAsync(originalBin, 33, 14, [8]);
var flight = await source.CompileMergeAsync(originalBin, 64, 14, [33, 34, 35]);
byte[] completeDestinationLevelEntry = electric.CopyFinalLevelData();
```

Compilation reopens and hashes the actual source before and after reading the
destination, and checks that destination's complete captured data hash. The
request list is copied before asynchronous work. Donor and destination use the
same captured shared geometry; equal-size but different geometry is rejected.
Missing donor resources, differing destination records and local overrides of
owned shared slots fail closed. Exact local reuse and known shared reuse are
byte-identical no-ops. Requested IDs are sorted without reordering old records.

Missing records are inserted immediately before the existing stream terminator.
This is a typed zero-length insertion in subfile 2, with its exact original WAD
address. `NativeLevelDataGrowthLayout` relocates subsequent roots and nested
subfile descriptors. It is not modified or bypassed. Root 0 remains anchored;
its existing records remain exact. Sector padding is newly allocated zero
storage, not presumed disposable source padding.
The smoke also combines the player insertion with independent subfile-1 growth
through the existing multi-subfile composer and checks the old single-subfile
adapter against the same output.

The result independently checks final stream record order/hashes, replays the
complete nested-header schema, rejects header changes outside decoded root and
descriptor fields, and proves preservation of **every original non-header
byte**. This includes all ordinary model packages, VRAM/SPU data, scene rows,
properties, fixups and other nested subfiles. Accessors copy arrays and expose
read-only collections. Internal typed-payload access also returns a copy.

The producer changes one level entry in memory only. It does not relocate the
whole WAD, update ISO records, bind a CUE, publish files, or grant runtime fit.
In particular, animation-8 sound/effect dependencies and the compact Toasty
RAM overlap problem remain separate requirements. All public plan flags for
runtime fit, complete animation consumers and publication remain false.

## First source-backed byte oracles

An independent read-only raw-sector Python splice, without the C# codec or
relocation helper, reproduced both whole-entry hashes below. It concatenated
the original ranges, exact donor records and sector padding, then rewrote only
the known descriptor/root fields. No BIN was produced by that oracle.
The read-only source is retained as
`src/Spyro.Editor.NativePlayerAnimationSmoke/source_splice_oracle.py`; it also
hashes the complete source before and after the two calculations.

| Merge | Exact inserted bytes | Sector growth | Final level-entry SHA-256 |
| --- | ---: | ---: | --- |
| Wizard Peak animation 8 to Toasty | `0x3E1C` | `0x4000` | `25e1a8d8c74a4c9e22596a72847ddd3b8dde155022d38221317d5204a9cc5f5e` |
| Gnasty Loot animations 33/34/35 to Toasty | `0x33C8` | `0x3800` | `251770f0eade8981fd6fca94d3a0bf434baeaff23ee83520dda0bf9d78079c5b` |

Animation 8 record SHA:
`cf194a65fc9b3b8790f6cd96eae5ef309156d00ad205d85790b2128e57243eb0`.
The Magic Crafters record is byte-identical; requesting it plus shared
animation 0 in Magic produces an exact no-op.

## Regression invocation and current evidence

```sh
dotnet build src/Spyro.Editor.NativePlayerAnimationSmoke/Spyro.Editor.NativePlayerAnimationSmoke.csproj -c Debug
dotnet src/Spyro.Editor.NativePlayerAnimationSmoke/bin/Debug/net10.0/Spyro.Editor.NativePlayerAnimationSmoke.dll
dotnet src/Spyro.Editor.NativePlayerAnimationSmoke/bin/Debug/net10.0/Spyro.Editor.NativePlayerAnimationSmoke.dll --source "Spyro the Dragon (USA).bin"
```

Initial RED build passed; the valid-stream test failed at the intentional
unimplemented decoder. Final targeted Debug build passed with zero warnings or
errors. Source-free **81 checks / 45 refusals** and source-backed **642 checks /
49 refusals** passed. The source mode covers all 35 levels and 181 records,
applies the same animation-8 structural merge to all 35 native destinations,
checks exact reuse where resident, and preserves the full original scene and
all non-header complements in every case. Typed WAD-address, single-/multi-
subfile composition, allocation-budget, exact-length fake-source hash refusal,
immutability, frame-high-byte and byte-golden checks are green. The independent
read-only Python splice oracle also passed both full-source hash checks and
both whole-level goldens.

Positive markers are `NATIVE_PLAYER_ANIMATION_SOURCE_FREE_OK` and
`NATIVE_PLAYER_ANIMATION_SOURCE_OK`. This suite never publishes a candidate.

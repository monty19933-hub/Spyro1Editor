# Unissued selected-slot residence descriptor

September 27, 2026. `NativeCollectibleSelectedSlotResidence` implements the
bounded accounting and identity binding in
[the selected-slot design](collectible-active-slot-residence-design.md).
It does not allocate or initialize native RAM, patch an executable, write a
card, install a hook, or issue native residence/lifetime/publication approval.

`Describe` accepts one exact authored manifest and ordinary slot 0, 1 or 2.
The descriptor retains the canonical manifest bytes, revision and digest.
Equivalent canonical manifests are accepted; other content, identities,
revisions or retirement masks are refused. Selected earned bits come from the
existing `DetachedCollectibleSave.CopySlotBits`; no save serializer is added.
Both returned bitmaps are detached copies with zero alignment/unassigned bits.
Retired earned bits remain in the selected bitmap, separate from the active
mask. Even a fully retired manifest retains its lifetime storage requirement.

For lifetime count L, raw bitmap bytes B=`ceil(L/8)` and aligned bytes
A=`align16(B)`. Live footprint is `90+2*A`; the entire additional `200` reserve
is charged regardless of actual footprint. All geometry numbers here are hex
except identity counts. The physical profile admits at most **1408 lifetime
identities** (`1F0` live bytes). The unchanged detached schema cap is **4096**.
`MeasureLifetime` can report zero/oversized profiles; `Describe` refuses zero
or a profile that exceeds the proposed reservation, without truncating history.
Envelope and transfer sizing use the existing codec's header, slot count and
SHA256 size; smoke checks compare the result with its actual serializer.

The proposal changes only g800755A4 from `800` to `A00`, giving reservation
`[801FF600,801FF800)` and normal floor `801879B0`. This class emits no changed
constant bytes. The independent g800755A8 stack origin remains `200000`.
Offsets `00..0F` are the sentinel/alignment guard; metadata starts at `10`.
The two-instance profile uses `B0` live bytes and still charges `200`.

## Shared detached-kernel ABI

These constants describe a proposed layout, not a loaded-state issuer. The
descriptor does not invent a validated card digest, native file identity or
session. The collection-kernel smoke owns its own synthetic initial RAM.

| Offset | Size | Field |
| --- | --- | --- |
| 10 / 14 / 16 | 4 / 2 / 1 | Magic `524C4353`, version 1, phase (ready=1) |
| 17 / 18 / 19 / 1A / 1C | 1 / 1 / 1 / 2 / 4 | Ordinary slot, controller, dirty, native file identity, status |
| 20 / 30 / 50 | 10 / 20 / 20 | Content GUID, exact manifest digest, validated envelope digest |
| 70 / 74 / 76 / 78 / 7A / 7C | 4 / 2 / 2 / 2 / 2 / 4 | Revision, lifetime count, B, active count, earned-active count, mutation generation |
| 80 / 84 / 88 / 8C | 4 each | Transaction generation, operation/chunk state, failure, session epoch |
| 90 / 90+A | A each | Selected lifetime bitmap, immutable active-ID mask |

## Read-only exact-source capture and evidence

`Capture` uses the existing read-only `NativeRegionalSourceSession` for the
original USA image. It checks the exact WAD entry-8 geometry, complete EXE
SHA256, complete PETE-header SHA256, original reserve/stack values and twelve
named instruction windows from `Inspect-SpyroUsaPeteLifetime.py`. These windows
cover boot setup, reserve values, PETE loading, frame allocation, sentinel and
particle writes, alternate transitions, CD staging, forward copy and the BIOS
handoff limitation. This is not the probe's full overlay census or a proof of
indirect/computed pointers. The session verifies held and named original-image
identity after capture. Complete EXE/header beforeimages are retained and are
only exposed by copy; exact-original verification checks every byte.

Focused verification:

```sh
dotnet run --project src/Spyro.Editor.NativeCollectibleSelectedSlotResidenceSmoke --configuration Release
dotnet run --no-build --project src/Spyro.Editor.NativeCollectibleSelectedSlotResidenceSmoke --configuration Release -- --source '/Users/john/Documents/Codex/2026-04-24/could-you-create-a-level-editor/Spyro the Dragon (USA).bin'
```

The initial test-first run failed on the absent descriptor type. Final focused
results are `COLLECTIBLE_SELECTED_SLOT_RESIDENCE_SOURCE_FREE_OK checks=90` and
`COLLECTIBLE_SELECTED_SLOT_RESIDENCE_SOURCE_OK checks=133`. The latter confirms
EXE SHA256 `a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`,
`nativeResidenceIssued=false`, `imageWrites=0` and `cardWrites=0`.
Checks include byte/bit/alignment boundaries, three-slot isolation, retirement,
exact manifest and shared ABI binding, source/complement mutations, truncation,
extension, defensive copies and closed installation/fit/lifetime/card gates.

Whole-engine and transient capacity, DMA/load admission, stack/IRQ lifetime,
cold-boot initialization, card transactions and native completion consumers
remain separate work. No app build, emulator run or real-save acceptance is
claimed by these checks.

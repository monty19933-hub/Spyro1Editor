# Detached per-instance collectible identity and earned-state contract

September 25, 2026. This is an internal, source-free authoring/persistence building
block. It reads/writes byte arrays only. It does not read a disc or card, select a
native offset, allocate native RAM, patch a pickup/save hook, change the editor UI
or authorize publication. All ordinary save bytes and existing native behavior
remain untouched.

## Why a new identity owner is necessary

The evidence in [Japanese collectible runtime ownership](japanese-collectible-runtime-ownership.md)
finds 31 native dragonfly placements but only 30 bank/level state owners. Terrace
Village's two placements alias one byte; the Japanese bank is not one of the
three ordinary save slots. Neither placement order, native level arithmetic nor
appearance can therefore supply the requested independent collectible identity.

The accepted user policy is that added dragonfly eggs are **required for 100%**,
not a bonus above 100%. No percentage formula is implemented here. Future live
and save-menu consumers must implement that policy separately while preserving
ordinary baseline behavior when no dragonfly eggs have been added. Retired IDs
are excluded from the active inventory but their earned history is retained.

## Identity ledger

`Persistence/DetachedCollectibleManifest.cs` assigns each caller-persisted,
nonempty instance GUID one permanent bit ordinal within a nonempty content GUID.
The initial GUID set is sorted by the GUID's 32-character hexadecimal form;
placement enumeration, row compaction, position and color are absent from this
API. The authoring layer must assign an ID once and retain it with that instance;
it must not reconstruct IDs from native row numbers on each build. That actual
authored-instance/native-row binding is still a separate implementation gate.

`CreateNew(contentId, instanceIds)` explicitly starts revision 1. `Advance` takes
explicit additions and retirements. Existing entries and bit indices remain
unchanged, except an active entry may become permanently retired. New IDs are
sorted within their append batch and assigned after all historical entries.
Retired IDs cannot be revived, re-added or replaced. A no-op revision is refused.
Each successor retains the SHA256 of its complete predecessor encoding.

The capacity policy is **4,096 lifetime identities**, including tombstones, not
4,096 currently active objects. Exhaustion is refused; there is no compaction
that might give an earned bit to a new object. The policy is detached and is not
a claim on any native card capacity or RAM allocation.

Serialization is canonical binary format 1. Integers are little endian; GUIDs
are RFC/network byte order, independent of the platform's in-memory GUID layout.

|Manifest offset|Bytes|Owner|
|---|---:|---|
|0x00|8|`SPYCMAN\0` magic|
|0x08|2|Format version, 1|
|0x0A|2|Header length, 80|
|0x0C|4|Exact total length|
|0x10|16|Content GUID|
|0x20|4|Content/manifest revision, starting at 1|
|0x24|4|Lifetime entry count, 0..4096|
|0x28|32|Complete predecessor SHA256; zero only for revision 1|
|0x48|8|Reserved, required zero|
|0x50|24 × count|GUID16, ordinal UInt32, retired byte 0/1, reserved3 zero|
|End − 32|32|SHA256 of all preceding encoded bytes|

`Parse` checks exact length, version, checksum, zero reserved fields, unique
nonempty GUIDs and contiguous ordinals. It proves document consistency, **not
historical authority**. `RequireDirectSuccessorOf` additionally binds the trusted
predecessor, unchanged prior bit owners, permanent retirements and canonical
active additions. Save migration always requires this relationship. The ledger
must be preserved with project content; a self-consistent replacement ledger is
not permission to reinterpret an old save. SHA256 is integrity, not a signature.

## Detached three-slot state

`Persistence/DetachedCollectibleSaveCodec.cs` owns exactly three independent
ordinary-slot bitsets, indexed 0, 1, 2. Bit `i` uses byte `i / 8`, mask
`1 << (i % 8)`. Each slot has `ceil(lifetimeCount / 8)` bytes. No Japanese mode
bank is reused. The save binds the complete exact manifest hash, content GUID,
revision and lifetime count.

|Save offset|Bytes|Owner|
|---|---:|---|
|0x00|8|`SPYCSAV\0` magic|
|0x08|2|Format version, 1|
|0x0A|2|Header length, 96|
|0x0C|4|Exact total length|
|0x10|16|Content GUID|
|0x20|4|Manifest revision|
|0x24|4|Lifetime entry count|
|0x28|2|Ordinary slot count, exactly 3|
|0x2A|2|Bytes per slot|
|0x2C|4|Reserved, required zero|
|0x30|32|SHA256 of complete canonical manifest, including its checksum|
|0x50|16|Reserved, required zero|
|0x60|3 × stride|Slot-major bitsets, unused high bits required zero|
|End − 32|32|SHA256 of all preceding encoded bytes|

Total size is `128 + 3 * ceil(lifetimeCount / 8)`, from 128 to 1,664 bytes. The
separate ledger is bounded at 98,416 bytes. These numbers are codec limits only.
In particular, fitting inside the observed USA file tail does **not** establish
that tail as owned, free, initialized, checked or writable.

`CreateNew` explicitly creates empty detached earned state. It is never invoked
by the decoder. `WithCollected` only sets an active identity's bit and is
idempotent; there is no implicit slot reset. `MigrateTo` accepts only a direct
successor, retains all three slots' old bits (including retired earned bits),
copies them independently across stride growth and leaves appended bits zero.
Multiple revisions require the intermediate trusted ledgers, not a skipped
history or a guessed remapping.

`Inspect` accepts an exact payload, not arbitrary card padding. Compatible data
can yield immutable earned state. Unknown/legacy bytes, unsupported versions,
bad checksums/structure, unknown high bits and another manifest yield an explicit
refusal with an exact opaque byte copy and its SHA256, and **no replacement
state**. All-zero or all-FF bytes are unknown, not automatically new game data.
Rejected bytes are not normalized, upgraded, truncated or reset. The inspector
retains input even if the caller later modifies the original array. This is a
caller-side recovery primitive, not a native legacy-card policy.

## Focused verification and remaining gates

The dedicated `Spyro.Editor.DetachedCollectibleSaveSmoke` takes no paths or
options. It covers placement-order determinism, immutable ownership, explicit
retirement and nonreuse after serialization, three-slot independence, byte-boundary
migration, retained retired earned bits, direct-chain refusal, full lifetime
capacity, all 12,288 slot/instance cells, canonical byte-layout goldens, every
single-byte mutation of the small manifest/save fixtures, structural corruption
with a recomputed checksum, unsupported versions and exact opaque preservation.

Focused Debug build passed with zero warnings/errors. The final source-free run
reported `DETACHED_COLLECTIBLE_SAVE_SOURCE_FREE_OK checks=14207 sourceReads=0
candidateWrites=0 cardWrites=0 nativeOwnership=false`. The small complete manifest
and save SHA256 goldens were independently constructed with Python's standard
`struct`, `uuid` and `hashlib`, without invoking the C# serializer or writing files.

```sh
dotnet build src/Spyro.Editor.DetachedCollectibleSaveSmoke/Spyro.Editor.DetachedCollectibleSaveSmoke.csproj -c Debug
dotnet src/Spyro.Editor.DetachedCollectibleSaveSmoke/bin/Debug/net10.0/Spyro.Editor.DetachedCollectibleSaveSmoke.dll
```

Still required before native integration: a source-bound authored-ID to live-row
table; collision-free RAM lifetime; independent slot selection and reset/new-game
semantics; native pickup/retirement, inventory and chosen completion consumers;
an owned card extent or separate game-owned file; allocation/checksum/I/O and
legacy/full/corrupt/removed/replaced-card handling; then exact readback and runtime
save/reload tests. No native hook or card ownership gate is relaxed by this codec.

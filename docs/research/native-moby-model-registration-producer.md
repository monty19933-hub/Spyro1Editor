# Source-bound append-only model registration

Implemented after `1f91e4b`. This is a reusable model-registration producer
consumed by the existing Town Square native chest compiler. It is not a new
enemy import, a complete runtime-memory certificate or export permission.

## Actual production path

`NativeTownSquareLockedChestClosureCompiler` captures the complete original
Town and Peace data, original executable, PETE header and actual native PETE
runtime-copy extent. `NativeMobyModelRegistrationSource` decodes the native
packed subfile owners and registration prefix. Its generic capture route uses
the non-publicly-constructible `NativeMobySourceLevel` certificate from the
game-wide inventory; Town/Peace adapters additionally pin complete exact data
hashes directly.

The native family graph first closes the actual AE package, its imported
texture/CLUT slices, equivalent resident sound and native consumers.
`NativeMobyModelPackageBinding` then requires an immutable selected package,
one canonical final install, complete registered source-interval extraction,
all source preimages, and a code-owned relocation policy. The registration
producer does not infer unknown package-internal pointer/animation schemas.
Those remain the source-native family factory's obligation. Equal bytes alone
do not establish context-independent resources.

`NativeMobyModelRegistrationPlan` assigns the consecutive tail slot, root/class
fields and sector-rounded model-subfile growth. The Town graph consumes these
values rather than hardcoded slot 37, `+0xE4`, `+0x19A` and model length. Its
typed root reference still targets the actual `actor.ae` install. Final
readback verifies the entire header, every original model-subfile byte,
imported package payload, exact selected dependency context, canonical address
domains and zero new alignment padding. Existing private family allocation
continues to own rows, properties, owner indices and mutable-global exclusion.

No candidate is published by this producer. No original or installed editor
bytes change. The source-bound Town V2/V3 graph, data, overlay, executable and
receipt contracts are preserved; the new registration metadata is detached
from their existing serialized receipts.

## Capacity and ownership boundaries

- Slot 0/class 0 is the special Spyro animation stream, never an imported Moby.
- The generic loader accepts 63 contiguous positive registrations in slots
  1–63. A zero terminates the loop; gaps and later hidden roots are refused.
- Class IDs must fit the native 512-pointer table. Duplicate classes, local
  replacement and PETE/shared collisions are refused. Existing resident reuse
  needs a separate equivalence certificate; the append operation does not
  silently overwrite or rename registrations.
- Complete source model intervals remain owned even when no placed row uses
  them. They are not allocator free space.
- Multiple closed immutable packages append deterministically. Exact repeated
  request identities reuse the same planned allocation; changed payload,
  policy, source/final address domain or class ownership is a conflict. Failure
  leaves all inputs and earlier immutable plans unchanged.
- Model growth is rounded once to `0x800` bytes and moves every later native
  subfile descriptor. This initial producer is exact source → model-tail
  append. Combining it with player-stream prefix insertion needs an explicit
  common typed relocation plan; neither producer may infer the other's shifts
  or accept a modified header under an original-source certificate.
- `RuntimeMemoryCertified` and `NativeImportApproved` remain false. Registration
  fit does not prove main-RAM, VRAM, SPU, dynamic-object pool, behavior, private
  state, effect, player-animation, movement or gameplay fit. The existing exact
  Town recipe retains its independent prior acceptance; a new destination does
  not inherit that acceptance.

Native loader facts and source spans are in
[the resource-frontier audit](native-import-next-resource-frontier.md).
The PETE certificate additionally pins header WAD `7B8800`, length `800`, SHA
`b9a4de193b5cf0bd9592de15c57aa5daa3713c3575026154b45402f3aceef6f1`,
and runtime-copy WAD `7B9000`, length `39C48`, SHA
`1703ac37e59e480909b6f66dc856d4e5f3ce80551f18cbc8c06129c3b54dd35d`.
The latter follows the actual native copy extent, including `448` bytes beyond
PETE's declared WAD entry. It must not be truncated to the declared entry.

## Regression evidence

Tests were added before implementation. The first authorized Debug build
passed with zero warnings/errors. No expected legacy golden was changed.

```sh
dotnet build src/Spyro.Editor.NativeMobyDependencyClosureSmoke --configuration Debug --no-restore
dotnet src/Spyro.Editor.NativeMobyDependencyClosureSmoke/bin/Debug/net10.0/Spyro.Editor.NativeMobyDependencyClosureSmoke.dll
dotnet src/Spyro.Editor.NativeMobyDependencyClosureSmoke/bin/Debug/net10.0/Spyro.Editor.NativeMobyDependencyClosureSmoke.dll --source-v3 'Spyro the Dragon (USA).bin'
```

Results after independent review and bounded-enumeration hardening:
**186 source-free checks; 583 combined source-backed checks**. New
markers:

- `NATIVE_MOBY_APPEND_ONLY_REGISTRATION_PREFIX_CLASS_DOMAIN_AND_CAPACITY_OK`
- `NATIVE_MOBY_REGISTRATION_ALL_35_NATIVE_HEADERS_AND_SPECIAL_PLAYER_SLOT_OK`

Coverage includes two-package append and exact retry, successful typed root
resolution, protected-model/header/payload/padding corruption, stale complete
source bytes, wrong domain, class collision, changed relocation policy,
shared-PETE conflict, full 63-slot refusal, malformed/signed roots and class
IDs, infinite repeated-request ingestion refusal, special player slot preservation,
and all 35 native headers compared with
the independent inventory. Generic occupancy remains 21–52; no existing model
slot is reclaimed. Town moves from 36 to 37 generic registrations, leaving 26
tail slots, without claiming corresponding RAM capacity.

V3 remains graph
`52c68e8595a21ef8807abb6090dfb02d84a8643af114346c3c7c962247c45d6d`
and data
`880c487599e8382447cd7b5c2d1799abec4eafd5af26351f37a22f66ed8ec9b0`.
This focused gate did not run new emulator gameplay or publish a candidate.

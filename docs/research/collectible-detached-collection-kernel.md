# Detached selected-slot collection leaf

September 27, 2026. CPU-only implementation; no installed hook or native RAM owner.

`NativeCollectibleCollectionKernel.Compile` emits a MIPS-I leaf into an exact
`NativeByteExtent` within a supplied final level-data code capacity. Compilation
binds the immutable manifest, ordinary slot 0/1/2 and caller-owned ledger base.
The residence descriptor supplies the ABI constants, geometry and active mask;
there is no second save serializer. No disc or card is read by this compiler.

The leaf takes the exact ledger pointer in `a0` and an unsigned stable lifetime
bit ID in `a1`. It returns `v0`: 0 collected, 1 already earned, 2 retired,
3 out of range, 4 invalid ledger, 5 generation overflow. It preserves every
register except `v0` and temporary registers `t0..t7` (the ABI permits `t8/t9`
as well), including all arguments, `at/v1`, saved registers, `gp/sp/ra` and
HI/LO. There are no calls, tables, native kill operations or stack accesses.

The pointer must match the compiled address, and the unsigned bit must be below
the compiled lifetime count, before any memory read. Code is word aligned and
the ledger is sixteen-byte aligned; the full emitted code and full 0x200 ledger
reservation must fit cached main RAM without overlapping. These geometric
checks do not prove that the engine has allocated either runtime owner.
Empty manifests compile an all-IDs-out-of-range leaf without describing or
issuing a native residence. Lifetime counts over 1408 fail native admission;
the detached format's 4096 cap remains unchanged.

Before any store, the leaf requires exact magic/version/ready phase/slot,
controller 0 or 1, dirty 0 or 1, zero status/operation/failure, exact content GUID,
manifest SHA256/revision/lifetime length/bitmap length/active count and the
entire compiled active mask. Unassigned bits and aligned bitmap padding must
be zero. A software popcount of earned AND active must equal the cached earned
active count. Retired earned bits remain valid history and never count as
active. Native file identity, envelope digest, transaction generation and
session epoch are opaque preserved controls; this leaf does not validate card
integrity or establish their authority.

Retired and duplicate IDs perform no writes. A new active ID also refuses when
mutation generation is `0xFFFFFFFF`; duplicate requests at that generation
still return already-earned. Success writes exactly eight ledger bytes across
four stores: the chosen bitmap byte, earned active count (u16), mutation
generation (u32), and dirty byte (1). It never reads or writes the guard at
0x00..0x10, and leaves the active mask, opaque controls and unused tail intact.
All refusal paths are reached before the first store.

The caller must prevent concurrent mutation and IRQ writers for the entire
call. Four stores do not establish an atomic transaction. This kernel supplies
no memory allocation, cold-boot initialization, selected-slot/death/level
lifetime, pickup dispatch, native row retirement, sound/effects, inventory UI,
memory-card transaction or publication approval. High native row IDs must not
be sent through the generic native kill path whose low-byte dispatch can alias
another class; this kernel makes no such call.

## Focused verification

The source-free smoke project links the existing `RenderProbeCpu` unchanged,
with strict signed arithmetic and R3000 branch and load delays enabled. The
fixture constructs ABI bytes independently. It checks the successful write
set and bit value, untouched complement, all preserved registers and HI/LO,
zero calls/stack, bounds before dereference, malformed metadata/mask/padding,
slot isolation, retirements, duplicates, generation limits, random differential
earned states and dense 1408-ID states. High IDs include 255/256, 511/512,
1023/1024 and 1407. These are bounded CPU tests, not emulator or gameplay proof.

Run from the release repository:

```sh
dotnet run --project src/Spyro.Editor.NativeCollectibleCollectionKernelSmoke/Spyro.Editor.NativeCollectibleCollectionKernelSmoke.csproj
```

Tests were present before implementation; the initial build refused missing
kernel/result types and missing smoke friend visibility. Production code and
its dedicated friend declaration were then added. Final focused result:

```text
NATIVE_COLLECTIBLE_COLLECTION_KERNEL_OK checks=1812 codeBytes=3CC maximumCodeBytes=744 sourceIo=0 runtime=false save=false
```

No ROM, emulator, memory-card, app or publication write is needed for this test.

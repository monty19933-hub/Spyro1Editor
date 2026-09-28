# Native collectible envelope validation and shadow preparation

September 27, 2026. Two new MIPS-I kernels implement the data-validation part
of native save loading. They execute real instructions against the existing
`DetachedCollectibleSaveCodec` format; the CPU tests do not replace checksum
calls with host results. This is not yet a card-menu hook, native selected-save
authentication, live Ready issuer or persistent-save implementation.

## SHA-256 leaf

`NativeSha256Kernel.Compile(capacity, codeAddress, inputAddress,
maximumInputBytes, digestAddress, workspaceAddress)` emits a bounded leaf.
The compiled input capacity is 1..1024 bytes; runtime `a0` must equal that base,
and unsigned `a1` may be 0..capacity. Result `v0` is 0 for success or 1 for an
invalid ABI. Output is the standard 32-byte big-endian digest.

The exercised placement emits 1,780 bytes, including its 64 round constants.
It owns 288 scratch bytes and a conservative 64-byte frame. Actual code size can
vary slightly with literal-address encoding. It runs padding, message-schedule
and compression loops in MIPS-I, reads each input byte exactly once, and never
reads past the supplied length. No helper, allocation, multiplication, HI/LO
mutation or external state is involved. Code/input/digest/workspace extents are
disjoint; complete stack checks precede every data access. Loads and transfers
have explicit delay-slot handling. Math follows FIPS 180-4; this implementation
does not claim cryptographic-module certification.

## Existing envelope to separate candidate state

`NativeCollectibleEnvelopeLoadKernel` is compiled for one canonical manifest
and ordinary slot. The caller supplies these runtime arguments:

| Argument | Required value |
| --- | --- |
| a0 | Exact compiled input address |
| a1 | Exact existing envelope length, including its checksum |
| a2 | Exact address of four context words: slot, controller index, native marker, epoch |

Slot must match the compiled 0..2 choice; controller index must be 0/1, not
native encoding 0/16; the marker must fit u16 without narrowing. Epoch is copied
as opaque u32 metadata. These are bounds checks, **not authentication** of a
selected native slot, callback generation, memory card or file identity.

The emitted reader performs these operations in order:

1. Validate addresses, length, context bounds and the complete nested stack.
2. Compare all 96 canonical header bytes, including reserved fields, content
   identity, revision, lifetime count, slot lengths and exact manifest digest.
3. Reject unassigned bitmap bits in **all three** slots, not only the selected
   slot. Historical earned retired bits remain valid and are retained.
4. Execute SHA-256 on the body and compare every checksum byte. Byte loads make
   the potentially unaligned trailer safe.
5. Execute SHA-256 on the complete envelope for the existing ledger digest.
6. Clear and fill the separate 512-byte shadow image. Copy selected earned bits,
   the exact manifest's active mask, and recompute earned-active count. Publish
   the shadow's prepared phase byte last.

The complete result matches `DetachedCollectibleLedgerImage.Hydrate`, including
zero padding, dirty/operation/generation fields, all identity bytes and counts.
The existing codec and hydration model supply immutable header/template data;
there is no competing save serializer or caller-supplied valid flag.

The tested 257-identity reader emits 2,748 bytes. Its 48-byte frame plus the
SHA leaf's 64-byte frame require **112 bytes** below entry SP. Compilation and
runtime guards exclude both complete code capacities, full input capacity,
context, shadow, digest and scratch. The proposed active ledger at
`801FF600..801FF800` is explicitly excluded from every compiled range and from
the complete nested stack. This reader cannot directly initialize that ledger.
All data remains exclusively caller-owned throughout validation/preparation;
these checks do not establish DMA/IRQ lifetime or a concurrent snapshot.

Arguments a0-a3, s0-s7, k0/k1, gp/fp/sp/ra and HI/LO are preserved. Refusals
return distinct argument, header, unused-bit, checksum or child-hash codes.
Every refusal preserves the entire existing shadow. After checksum work has
begun, scratch/digest/frame contents may change; original input and context
never do. No failure creates an empty replacement save or resets progress.

## Verification

Both production types followed missing-type RED tests. The loader test first
had two test-array inference errors, corrected before the clean four-missing-
type RED. Independent review found no code defect and prompted child-only
stack-collision tests, checksummed mutation of every header byte, accepted
metadata boundaries and exact refusal-result assertions. All passed.

- SHA: 611 checks, 36 primary vectors plus five relocation/RAM-boundary probes.
  Digests match .NET SHA-256 and standard empty, `abc` and multiblock goldens.
  Checks cover exact input/round-constant reads, all memory footprints,
  register preservation, hostile arguments and canaries.
- Envelope loader: 3,797 checks. Cases include 1..1408 lifetime identities,
  all three slots, dense 1408-bit counts, retired/all-retired manifests, every
  corrupted envelope byte, every recomputed-checksum header mutation, each
  slot's invalid padding, metadata boundaries and complete nested-stack guards.
  Every successful image matches all 512 host-codec bytes. The protected active
  reservation remains poisoned and unchanged in every test.
- All instruction tests use the existing `RenderProbeCpu` unchanged, including
  R3000 load/branch delays and its 200,000-step limit. No SHA rounds, service
  returns or native helpers are replaced with synthetic successes.

```sh
dotnet run --project src/Spyro.Editor.NativeSha256Smoke -c Release
dotnet run --project src/Spyro.Editor.NativeCollectibleEnvelopeLoadSmoke -c Release
```

Both suites are source-free default regression gates. They read no ROM/card,
write no image/save, and launch no editor or emulator. The native RAM profile
still admits at most 1408 lifetime identities; the existing detached format's
4096-identity capacity is unchanged and not silently truncated.

The complete default Release regression passes **258 checks, zero failures**:
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-4p7l38fn/summary.json`.
Its logs 149 and 150 confirm the 611 and 3,797 markers above from Release binaries,
not just the earlier individual development runs. The runner-contract suite
also passes after registering both tests; no source/publication default was
added. Original ROMs, normal memory cards/settings and R06/R07/R08 packages
were not modified, and no DuckStation session was launched for this checkpoint.

## Next native boundary

Bind this reader to an owned card-menu buffer and completed asynchronous read,
with the exact selected-save provenance already documented in
[selected-save admission](collectible-selected-save-admission-boundary.md).
Fresh-state creation, legacy/unknown-tail policy, durable transactions and a
validated transfer from shadow into the live residence remain separate work.
No X/X display, percentage consumer, new playable BIN or editor package is
claimed. The full milestone, including universal Mobys, signs and flight
save/reload, remains active.

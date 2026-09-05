# Typed native relocation geometry

`NativeRelocationMap` describes bounded byte ownership in an original and final
address space. Spaces distinguish WAD-file, level-data, scene, executable-file
and logical-disc coordinates. Their identities are explicit object identities:
matching labels do not merge independently constructed domains. A caller must
share the same final disc domain when checking cross-file conflicts.

This is geometry, not source or runtime authority. Source fingerprints, native
ownership evidence, growth limits, patch preimages, and publication permission
remain the responsibility of the existing validated producers and writers.
Neither a label nor a successful mapping proves that a retail catalog is valid
for an arbitrary previously relocated image.

## Contract

- Original and final ranges have checked nonempty extents and explicit revision.
- Preserved regions retain byte length. Inserted ranges have final ownership but
  no fabricated source address. Original/final ownership overlaps are rejected.
- A mapped span must fit one preserved owner region. A span crossing an insertion,
  unowned header, or ownership boundary fails instead of receiving one guessed
  offset. Adjacent writes are allowed; overlapping writes in one domain fail.
- Growth uses checked insertion positions, alignment and complete extents. Table
  append additionally binds row size and original/final row counts.
- Declared little-endian u32 references map both their field and target, validate
  the exact four-byte source encoding, and check alignment/base/unit/capacity.
  Zero-encoded addresses require an explicit non-null policy. The map does not
  discover pointer fields, automatically add native fixups, or write bytes.

`NativeWadRelocationMap` adapts the existing source-validated WAD relocation plan.
It checks every packed original/final entry extent and the complete growth sum,
excludes archive headers, and requires declared internal insertions to account
for every growing entry. The single- and multi-destination private texture
consumers use this same geometry. The texture append builder separately maps
the LQ/HQ table appends and later level-data components through the same core.
Their existing byte preimages, source hashes, limits, and writer authorization
are unchanged.

Executable-file to logical-disc placement is a distinct supported mapping; it
does not reinterpret executable patches as WAD80-relative bytes. Physical
sector conversion and EDC/ECC remain exclusively in the checked `DiscImage`
contract. No CUE, ISO publication, or raw-sector behavior changes here.

## Regression evidence

```sh
dotnet run --project src/Spyro.Editor.NativeRelocationMapSmoke -c Debug
```

The regression first recorded 20 existing production mapper outcomes against the
frozen prior Core DLL. A stronger negative case then reproduced the aggregate
mapper accepting a final entry offset inconsistent with its complete declared
archive growth. That malformed input now fails before any write. This is an
internal composition-input guard, not a claim that the normal UI previously
allowed arbitrary forged relocation plans.

The suite executes all three production consumers on synthetic arrays/a tiny
ISO, checks source preservation and exact mapped preimages, and independently
reconstructs inserted byte sequences to verify span mapping. It covers reference
encodings, cross-domain conflicts, row growth, alignment, immutable snapshots,
and valid addresses near `long.MaxValue`. Real-source texture scenarios remain
the separate byte-equivalence gate; gameplay is not established by these tests.

## Remaining boundary

The normal structural-composition preflight remains in force. This component
does not yet provide a universal live WAD/scene/EXE relocation revision, model
all native pointer owners, retire components, or compose arbitrary direct CLI
writers after growth. New consumers must supply validated ownership and complete
reference obligations before their current safety gates can be relaxed.

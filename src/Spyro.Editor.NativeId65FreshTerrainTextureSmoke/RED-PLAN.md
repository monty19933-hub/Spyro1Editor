# Detached two-material allocation: tests-first boundary

These tests were added before the two production files existed. That initial
test checkpoint changed no normal exporter, Core project, regression runner,
existing oracle, untextured golden, staging/body producer, candidate, source image
or installed application. Subsequent implementation, integration and final
verification are recorded in README.md.
Root's actual Release RED build completed in1.19s with zero warnings and exactly
eight missing allocator/input/region type errors. The log is retained under
`_local/research/fresh-native-textures-20260906/allocator-red-build.log`.

## Tested API

Only two new Core files implement this boundary:

- `Analysis/NativeStaticTerrainTextureAllocation.cs`: one neutral implementation.
- `Exporting/NativeId65FreshTerrainTexturePlan.cs`: a private sealed exact-source
  adapter using that implementation, not a second allocator or fake empty atlas.

The neutral `NativeStaticTerrainTextureInput` carries material ID, source identity,
address-domain identity, relocation policy, complete184-byte record and complete
0x80000-byte source GPU image. Public inputs carry **no source authority**. Exact
two material IDs0/1, bounded independent enumeration, identity lengths, record/GPU
lengths and the supported relocation policy are checked before clones. Inputs with
equal bytes but different address domains must not content-deduplicate.

`NativeStaticTerrainTextureAllocation.Compile(upload, uploadOwners, inputs, clip)`
accepts the complete0xDE000-byte destination upload and an exhaustive disjoint
`NativeId65NormalResourceOwner` partition. Only actual fresh-zero GPU ownership is
allocatable; source-address/provenance combinations must be valid. No GPU owner
can cross into SPU. Protected zero bytes stay protected. This public neutral API
does not convert declared provenance into a private source certificate.

The immutable result exposes ordered complete `Records`, actual `Allocations`,
`ContainedAliases`, allocated/free byte counts and `CopyUploadPayload()`.
Allocation receipts own material/source/target offsets, resource kind/BPP, row
width/count; contained aliases also retain parent source/target offsets. Copies
are detached and failure publishes no partial output/reservations. The source-
free adversarial masks use **two complete records through this exact API**.

`ComposeTwoFaceData(originalData)` is the same neutral result's byte composer. It
checks the bounded current untextured8-byte metadata and two-face HP structural
preimages, constructs native all-LQ then all-HQ metadata, and changes exactly the
two HP material words in the translated tail. It does not claim that arbitrary
neutral tail bytes are valid source resources. The private adapter supplies the
actual already-issued fresh data; no caller data or ownership mask reaches it.

`NativeId65FreshTerrainTexturePlan.CaptureAsync(originalPath, fresh, dark, gnasty,
token)` accepts only the real `NativeId65FreshSceneBundle` and two real sealed
`NativeStaticTerrainTextureSource` issuers: Dark WAD14/T0 first, **Gnasty's World
(ID60), WAD70/T0** second. It verifies/re-reads original EXE/source identity and
the native default clip store. Output hashes bind the exact upstream plan and
source handles. Missing/reversed/reused sources refuse. Neither fake WAD80 nor a
caller-authored LevelDefinition/source certificate is an input.

Outputs include complete upload/data, exact inherited model/scene snapshots and
full-byte readback. `SourceCertified` and bounded `DestinationAllocationProven`
may be true only on this private adapter. Runtime, HQ execution, complete-level
resources and publication remain false. `AdditionalPacketBytesPerPass=24` is
explicitly not a frame-peak budget. No downstream staging/body seam is tested or
proposed in this implementation increment.

## Concrete RED coverage

`AllocationCases.cs` covers full records, shared implementation, immutable copies,
source-domain identity, unknown policy, partial overlap refusal, bounded liar-
Count enumerables, missing/extra inputs, missing/overlapping/stale/unknown owners,
invalid source offsets, zero-valued source and detached owners, GPU/SPU domain
separation, exact eight-hole success and late failure/retry. Geometry countercases
include15-row LQ palette,511-byte HQ CLUT,31-row LQ tile, fragmented aggregate free
bytes,256-row page crossing, and the last legal column/row. Exact clip0x8000 is
required; clip1/129 remain refusals, not reasons to silently add a seventeenth
palette row. The patterned neutral data tail makes complement checks meaningful.

`SourceCases.cs` reconstructs actual upstream sources using existing public
factories. It checks old four-domain byte goldens; both distinct T0 handles; all
46 independently decoded relocated descriptors and full palette/pixel bytes;
31 resource regions/28,160 bytes;16 contained close aliases;17,369 protected zero
bytes; native split metadata; two exact HP writes and the entire translated data
complement; unchanged model/scene/SPU; complete output corruption readback;
private constructor, source selection, cancellation and repeat/retry boundaries.
No exception helper accepts FileNotFound/IOException as a source-authority test.

The finite-input fixture was strengthened before implementation to yield exactly
three items for the maximum of two, then throw; a reader cannot consume max+2 and
still satisfy the test. After first GREEN, three additional test-only cases cover
null input, null owner, and an exhaustive/hash-correct GPU-to-SPU straddle. These
are test-strengthening changes, not changed production behavior or byte goldens.

## Independent raw oracle, already executable before C# GREEN

```sh
python3 src/Spyro.Editor.NativeId65FreshTerrainTextureSmoke/source_texture_allocation_oracle.py 'Spyro the Dragon (USA).bin'
```

The new `source_fresh_upload_fixture.py` is an explicit-return research fork of the
existing source-fresh oracle, pins its upstream/helper hashes and retains all old
source/golden checks. No profiling hooks, return-locals capture, editor DLL or
tracked-old-oracle edits are used. The new allocation oracle independently reads
source sectors, derives all ownership/containment/placements, copies all resource
bytes, re-encodes/re-decodes all46 descriptors and builds the whole new metadata/
data bytes. It writes no ROM or payload. Its first successful result established:

- Upload: `2ab548f540fbfadddc38d96e70a9633a1c6f37c37ee2d1a896919ac30f929333`.
- Data0xA38: `c13c21171e5ed4755f1598914eba89b0b31795b91aaeb2605efc6fed9aa17160`.
- Dark record: `06138c4791b32b848dd81cd4dbab46419d7f3bd1c6b46a6f38194c8645ba7251`.
- Gnasty record: `b57966bf69be49d581dfd73d764155de838906d5d7550ff4d1c85de7c272d100`.

Native source hash/length are checked before and after. Free-space accounting,
record ownership and fixed-LQ source proof do not constitute runtime acceptance.

## First implementation result and build/run modes

The first focused Release implementation build passed in4.93s (5.04s wall), with
zero warnings/errors. Source-free7,572 and source33,022 additional checks passed
(40,594 total). Every pre-C# full-upload/data/record golden and every prior
untextured golden matched on the first run. There was no failure-driven expected-
value adjustment. Build/free/source logs are the sibling `allocator-green-*`
files in the same research directory. The three post-review hostile cases await
the final targeted rerun; do not count them as executed in those earlier totals.

Root and Huygens reviewed both new Core files; Huygens separately reran the raw
oracle. Allocation timing is supported by static ordering review: both bounded
snapshots and all dimensions/provenance/partition bounds precede output clones.
The exception fixtures do not claim to measure transient allocations.

Build this project only. No arguments runs source-free tests and prints
`NATIVE_ID65_FRESH_TERRAIN_TEXTURE_FREE_OK`. Explicit `--source original.bin`
adds read-only private-source tests and a distinct
`NATIVE_ID65_FRESH_TERRAIN_TEXTURE_SOURCE_OK` marker. All other CLI forms refuse;
there is no workspace, CUE, staging, publish, overwrite, or candidate mode.

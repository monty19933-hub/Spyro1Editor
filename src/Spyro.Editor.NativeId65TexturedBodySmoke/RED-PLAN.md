# Closed textured staging/body revision — tests-first plan

Status: implemented after root reviewed the fixtures, independent raw oracle,
actual RED and separate fresh-disc checkpoint. Isolated Release RED had0warnings
and25 intended missing profile/property/named-factory errors,0.49s reported build
and0.59s wall (`textured-body-red-build.log`). First GREEN was3.91s build/4.01s
wall,0warnings/errors,18free+135source. Two additional full evidence-sequence
assertions run against each real scene raised the final gate to18free+139source
=157, final build0.49s with0warnings/errors. No expected hash or prior fixture
semantics changed. All logs are retained under
`_local/research/fresh-native-textures-20260906/` with the `textured-body-` prefix.
No ROM, candidate, App, native execution or publication was changed. The build
slot is released; Core and tests are frozen for root checkpoint review.

## Exact bounded API

Add one internal sealed `NativeId65NormalResourceRevision` in a new Core
`Exporting/NativeId65NormalResourceRevision.cs`. Its constructors are private.
Exactly two internal `From` factories accept the actual privately issued
`NativeId65FreshSceneBundle` or `NativeId65FreshTerrainTexturePlan`. There is no
interface implemented by callers, raw bytes/hash constructor, arbitrary profile
label, `LevelDefinition`, invented WAD80 or neutral allocator source authority.

The revision retains complete copied upload/data/model/scene domains, the exact
upstream plan identity, and the real fresh ancestor's entry profile/evidence.
The texture plan therefore needs only a minimal internal accessor retaining its
actual fresh ancestor (not a reconstruction from its public hashes).

Public read-only `NativeId65NormalResourceProfile` values are precisely:

- `UntexturedGouraudTwoTriangles`
- `DirectLqTwoStaticMaterials`

Stage and body expose `ResourceProfile` from that closed private revision.
Neither factory accepts this enum or a profile string. The existing public
`const ProfileId` is not an instance-revision discriminator.

Keep the sole existing `CaptureAsync` signatures unchanged. Add named APIs:

```csharp
NativeId65NormalUploadStaging.CaptureTexturedAsync(
    string originalImagePath, NativeId65FreshTerrainTexturePlan issuedTexture,
    CancellationToken cancellationToken = default);

NativeId65NormalBodyPlan.CaptureTexturedAsync(
    string originalImagePath, NativeId65FreshTerrainTexturePlan issuedTexture,
    NativeId65NormalUploadStaging issuedStaging,
    CancellationToken cancellationToken = default);
```

The names preserve unambiguous old null-call overload resolution. Before source
I/O, the body requires profile, full upstream PlanSha and full paired UploadSha
to match. Byte-identical repeated capture is valid; unrelated complete scene
intent is not. Null/invalid pair rejection cannot pass via FileNotFound.

## Reviewed minimal Core diff (implemented)

1. New `Exporting/NativeId65NormalResourceRevision.cs` contains the closed adapter
   and the two-value read-only public enum.
2. `NativeId65FreshTerrainTexturePlan.cs`: retain/access the actual source-issued
   fresh ancestor internally. Do not alter existing output or PlanSha JSON.
3. `NativeId65NormalUploadStaging.cs` and `...StagingSource.cs`: common revision
   input, one existing `NativeId65NormalUploadTransferLayout`, same source capture
   and bank validation; new named factory delegates to that same path.
4. `NativeId65NormalBodyPlan.cs` and `...BodySource.cs`: common revision input,
   same existing body assembler/header/root/auxiliary validation. Neutral layout
   accepts only exact data lengths0x8C8/0xA38 and records the actual used length.
   Source validator also requires the actual issued profile and matching data
   length/bytes, entry profile, companion/player prefix and captured source.
5. Added a narrow read-only `ResourceProfile` check to the separately
   owned `NativeId65FreshDiscPlan` before it accepts a body. That issuer remains
   **untextured-only**, additionally requiring its existing exact8C8 used end,
   anchors and source-private shape. This is not textured disc support.

No duplicate assembler, weakened public constructors, ordinary exporter,
Core project, candidate materializer or output writer is part of this change.
Root owns the separate regression-runner registration. A friend-assembly
expansion is not needed: source-free
boundary tests use reflection, and source cases use only real public issuers.

## Preserve legacy serialization, not only output bytes

Adding `ResourceProfile` must NOT append it to either old anonymous PlanSha JSON.
Keep old evidence order, owner provenance and pending strings unchanged. The new
textured branch may explicitly serialize its own new profile metadata while
reusing the same byte assembler. These pre-change retained values are pinned:

| Existing artifact | SHA256 |
|---|---|
| First staging PlanSha | `98bd43761b87de15d2915d1377e0298ce7a0f133d2ee07c2f5672bad503af000` |
| First body PlanSha | `48f1f0b4f7740c8a6277a4b440990e1fd26cb609db2153c3c24fc61d70dbbb94` |
| First whole untextured body | `b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c` |
| Second whole untextured body | `1314b299759ad0f66eed054db853c18e8db6dabda7c56a28cabd3fd74ea39643` |

Historical PlanSha sources are respectively
`_local/research/native-id65-normal-upload-staging-20260906-r01/07-source.log`
and `_local/research/native-id65-normal-body-20260906-r01/04-source.log`.
These are not newly generated expected values.

## Independent oracle established before Core

`source_textured_body_oracle.py` reads and rechecks the complete original BIN,
the exact full Town/EXE/overlay owners, and all16 existing native body source
windows. It assembles source-native model/scene/header/auxiliary bytes, proves
both existing full-body goldens, then substitutes only the actual two-texture
upload and used-data owners. No editor DLL, receipt or C# body assembler is used.

The previous texture allocation oracle gains only an explicit immutable return
API (`CapturedTextureOutput` with bytes/tuple/string); its default CLI and
existing JSON golden are unchanged and replayed successfully. No `sys.setprofile`
or frame-local capture is used. The existing tracked normal-body oracle is not
edited or copied into a second production implementation.

New independently derived values, now pinned in
`source_textured_body_golden.json` before any Core changes:

| Owner | SHA256 |
|---|---|
| First textured full body | `347ab532881afb3975088e0872792192d8567b0abe05e7708f271741b9568cdd` |
| Second textured full body | `34d8d4c7062c0f84ef14bbbd22f66ba3ae33e68116d0877c82f4517f21622ea5` |
| Both full headers, unchanged | `cd5658f83da4a953c5a3e66c848a8be606aabde9fea8b32d049c2bcf364c472d` |
| New complete upload | `2ab548f540fbfadddc38d96e70a9633a1c6f37c37ee2d1a896919ac30f929333` |
| New actual used data | `c13c21171e5ed4755f1598914eba89b0b31795b91aaeb2605efc6fed9aa17160` |
| Old AND new transfer image | `626ae349b98cb4bea955ac53020586f645f872225085a67bc809f46f4ab0fb07` |

Each whole body remains0x1FB800. Actual used data grows0x8C8→0xA38 (+0x170),
while its CD descriptor remains0x1000. Thus padding shrinks0x738→0x5C8 and native
used-end addresses move: model8008A794→8008A904,
scene80099F94→8009A104, known scene end8009CF94→8009D104.
The exact overlay copy-buffer80089ECC and original phase overlap0x36C remain.

All new texture allocations finish below0xFF90, so neither bank80000..DE000 nor
the transfer residue5E000..7F000 changes. The full upload differs even though
the complete transfer bytes are equal. This is the central cross-pair regression,
not grounds to recycle an old staging identity.

## Actual RED fixtures

`RevisionCases.cs` source-free checks:

- Internal sealed revision with private constructors and exactly the two actual
  source-type factories; no neutral allocator/raw bytes/profile-label factory.
- Private stage/body constructors, exact named overload shape and read-only
  instance discriminator; old null call forms remain unambiguous.
- Old/new null inputs refuse before opening deliberately missing paths.

`TexturedBodyCases.cs` original-source opt-in checks:

1. Two real fresh scenes, sharing floor/upload but distinct spawn and heading,
   each use fixed Dark Hollow WAD14/T0 and Gnasty's World WAD70/T0 sources.
   All old PlanSha/bytes and new independently derived body hashes are exact.
2. Old stage/new texture and new stage/old fresh reject, both directions; new
   stage/different textured scene rejects despite equal upload/transfer. All
   rejections use missing paths and a helper that does **not** catch IOException.
3. Every final body byte is compared with the old whole body modified only by
   exact new upload/data plus new padding; full header/model/scene/aux/overlay
   complement, ownership partition, native request/used-end addresses and
   phase-overlap arithmetic are checked.
   Complete ordered read-phase IDs/count and unchanged staging SourceReads/Owners
   are checked before individual comparisons. Final strengthening directly maps
   all six texture evidence records and requires exact complete source-stage,
   fresh-ancestor and body evidence sequences, not only equal output hashes.
4. Changed fields and complements, truncation/suffix, invalid source file,
   cancellation, immutable copies and deterministic retry are checked. The
   original full BIN is rehashed before and after; it is never opened for write.
5. New textured body refuses the existing untextured disc issuer before I/O,
   despite equal total CD size. All staging/body/texture hardware, HQ, peak,
   runtime and publication gates stay false.

No Workspace, CUE, ISO/BIN preparation, output directory, fixture ROM write or
normal-export permission exists in this smoke. Its only arguments are noargs
or `--source original.bin`. Source-free and source markers remain distinct from
all existing smoke markers.

## Reproduction and approval boundary

From repository root, raw read-only checks (already executed successfully):

```sh
python3 -B src/Spyro.Editor.NativeId65FreshTerrainTextureSmoke/source_texture_allocation_oracle.py 'Spyro the Dragon (USA).bin'
python3 -B src/Spyro.Editor.NativeId65TexturedBodySmoke/source_textured_body_oracle.py 'Spyro the Dragon (USA).bin'
```

The isolated root-authorized Release compile observed only the intended missing
profile/property/named API RED. After explicit implementation authorization,
the final focused source-free/source gate passed157. Existing NormalBody also
passed689source-free/791source-inclusive and NormalResource passed327source-free/
15423source-inclusive against the rebuilt Core. Their actual old stage/body plan
hashes and full original outputs were unchanged. These are bounded source tests,
not a full regression, emulator or hardware acceptance claim.

Fixed direct-LQ clip0x8000, all184-byte source records/all16 palette rows and
complete upload ownership are preserved. They do not prove HQ branch reachability,
native frame packet peak, async transfer completion, SPU wrap, full ID65 closure,
rendering, gameplay, source-independent content or publication safety.

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

const string ExpectedDocumentCanonicalSha256 =
    "8f27bfc3f97238402c0b7dd59a081335ae19df1acfaefa9575d9ca7110dce808";
const string ExpectedDocumentContentSha256 =
    "9d853ccfcf18627a46dc730844efc9eeeda28ee9a0d53c70ae3b7144652b4c3b";

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string lockedImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name",
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin");
string lockedCuePath = Path.ChangeExtension(lockedImagePath, ".cue");
Require(File.Exists(lockedImagePath), $"Missing exact locked display-name image: {lockedImagePath}");
Require(File.Exists(lockedCuePath), $"Missing exact locked display-name CUE: {lockedCuePath}");
FileState lockedBefore = SnapshotFile(lockedImagePath);
FileState lockedCueBefore = SnapshotFile(lockedCuePath);
IReadOnlyList<string> lockedDirectoryBefore = SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!);
string smokeRoot = Path.Combine(
    Path.GetTempPath(),
    "Spyro.Editor.Id65AuthoringEditorDocumentSmoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(smokeRoot);

try
{
UnusedLevel65BlankLevelLabWorkspacePaths labPaths =
    UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
        Path.Combine(smokeRoot, "workspace-container"));
Directory.CreateDirectory(labPaths.LockedBaseDirectoryPath);
Directory.CreateDirectory(labPaths.AuthoredEditsDirectoryPath);
File.Copy(lockedImagePath, labPaths.LockedBaseImagePath, overwrite: false);
await File.WriteAllTextAsync(
    labPaths.LockedBaseCuePath,
    DiscImage.BuildCueText(lockedCuePath, Path.GetFileName(labPaths.LockedBaseImagePath)),
    Encoding.ASCII);
UnusedLevel65BlankLevelLabManifest labManifest =
    UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(labPaths);
await File.WriteAllTextAsync(
    labPaths.ManifestPath,
    JsonSerializer.Serialize(labManifest, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    }) + "\n",
    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
Id65AuthoringEditorNativeInputProvider.InputSet nativeInputs =
    await Id65AuthoringEditorNativeInputProvider.LoadAsync(labPaths, labManifest);
Id65AuthoringCompositeLockedSource source = nativeInputs.BoundSource.LockedSource;
Id65V2NativeTextureWitness textureWitness = nativeInputs.PrivateTextureWitness;
Id65MobyDependencyBundleDescriptor bundle = nativeInputs.GrassBundle;

string[] reorderedPlacementIds =
[
    Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId,
    Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId
];
string[] canonicalPlacementIds =
[
    Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId,
    Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId
];
Id65AuthoringEditorDocument document =
    Id65AuthoringEditorDocument.CreateExact(
        includeOptionalTerrain: true,
        placementIds: reorderedPlacementIds);
Id65AuthoringEditorDocument reordered =
    Id65AuthoringEditorDocument.CreateExact(
        includeOptionalTerrain: true,
        placementIds: canonicalPlacementIds);
byte[] documentUtf8 = document.SerializeUtf8();
byte[] reorderedUtf8 = reordered.SerializeUtf8();
Require(documentUtf8.SequenceEqual(reorderedUtf8),
    "The canonical editor document depends on caller placement order.");

Id65AuthoringEditorDocument reloaded = Id65AuthoringEditorDocument.Parse(documentUtf8);
Id65AuthoringEditorDocument reloadedFromString =
    Id65AuthoringEditorDocument.Parse(Encoding.UTF8.GetString(documentUtf8));
Require(reloaded.SerializeUtf8().SequenceEqual(documentUtf8) &&
        reloadedFromString.SerializeUtf8().SequenceEqual(documentUtf8),
    "Canonical UTF-8 editor-document serialization did not round-trip exactly.");
Id65AuthoringManifest lockedManifest = Id65AuthoringModelCompiler.CreateMinimalV3EquivalentManifest();
Require(document.SchemaVersion == Id65AuthoringEditorDocument.CurrentSchemaVersion &&
        document.ProfileId == Id65AuthoringEditorDocument.DocumentProfileId &&
        document.LabProfileId == UnusedLevel65BlankLevelLabProfileRegistry.ProfileId &&
        document.LabProfileVersion == UnusedLevel65BlankLevelLabProfileRegistry.ProfileVersion &&
        document.WorkspaceKey == UnusedLevel65BlankLevelLabProfileRegistry.Key &&
        document.WorkspaceLayoutVersion ==
            UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutVersion &&
        document.WorkspaceLayoutRelativeRoot ==
            UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutRelativeRoot &&
        document.LockedBaseProfileId ==
            UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseProfileId &&
        document.LockedBaseImageSha256 ==
            UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseImageSha256 &&
        document.LockedRow80Sha256 == Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 &&
        document.CompositeProfileId == Id65AuthoringCompositeCompiler.ProfileId &&
        document.PrivateTextureWitnessSha256 ==
            Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 &&
        document.MobyBundleSha256 ==
            Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256 &&
        document.BaselineHandles == new Id65AuthoringEditorBaselineHandles(
            lockedManifest.Spawn.Id,
            lockedManifest.Music.Id,
            lockedManifest.Totals.Id,
            lockedManifest.Exit.Id,
            lockedManifest.Save.Id) &&
        document.MobyPlacements.Select(item => item.TargetTrueIndex).SequenceEqual([88, 107]) &&
        Encoding.UTF8.GetString(documentUtf8) == document.CanonicalJson &&
        Hash(documentUtf8) == document.CanonicalSha256 &&
        document.CanonicalSha256 == ExpectedDocumentCanonicalSha256 &&
        document.ContentSha256 == ExpectedDocumentContentSha256 &&
        Hash(DeriveContentPreimageUtf8(documentUtf8)) == document.ContentSha256 &&
        Id65AuthoringEditorDocument.MaximumSerializedByteCount == 64 * 1024,
    "The canonical editor-document identity, baseline handles, or content envelope changed: " +
    $"canonical={document.CanonicalSha256}; content={document.ContentSha256}.");
Id65AuthoringEditorTerrainIntent declaredTerrain = document.Terrain.Single();
Require(declaredTerrain.Id == Id65AuthoringTerrainMutationCompiler.TriangleId &&
        declaredTerrain.SectorId == "sector.remote-pad.216" && declaredTerrain.SectorIndex == 216 &&
        declaredTerrain.AddedVertex == new Id65AuthoringEditorPoint(
            Id65AuthoringTerrainMutationCompiler.AddedVertexHandle, 496, 496, 512) &&
        declaredTerrain.HighPolyHandles.SequenceEqual(
            ["vertex.remote-pad.b:hp", "vertex.remote-pad.d:hp", "vertex.remote-pad.c:hp"]) &&
        declaredTerrain.LowPolyHandles.SequenceEqual(
            ["vertex.remote-pad.b:lp", "vertex.remote-pad.d:lp", "vertex.remote-pad.c:lp"]) &&
        declaredTerrain.Collision.Id == "tile.remote-pad.1:collision" &&
        declaredTerrain.Collision.VertexHandles.SequenceEqual(
            ["vertex.remote-pad.b", "vertex.remote-pad.c", "vertex.remote-pad.d"]) &&
        declaredTerrain.Occlusion.Id == "occlusion.remote-pad.group0" &&
        declaredTerrain.Occlusion.SectorId == "sector.remote-pad.216" &&
        declaredTerrain.Occlusion.GroupIndex == 0,
    "The editor document did not preserve the exact HP/LP/collision/occlusion stable handles.");

Id65AuthoringEditorProjection firstProjection = Project(document);
Id65AuthoringEditorProjection repeatProjection = Project(reloaded);
Id65CompiledAuthoringComposite first = firstProjection.Compiled;
Id65CompiledAuthoringComposite repeat = repeatProjection.Compiled;
Require(Equivalent(first, repeat), "Repeated editor-document projection was not deterministic.");
Require(firstProjection.DocumentCanonicalSha256 == repeatProjection.DocumentCanonicalSha256 &&
        firstProjection.DocumentCanonicalSha256 == document.CanonicalSha256,
    "Repeated editor-document projection changed document identity.");

Require(first.ManifestSha256 == Id65AuthoringCompositeCompiler.ExpectedCombinedManifestSha256 &&
        first.SourceRow80Sha256 == Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 &&
        first.SourceModelSha256 == Id65AuthoringCompositeCompiler.ExpectedLockedModelSha256 &&
        first.OutputModelSha256 == Id65AuthoringCompositeCompiler.ExpectedCombinedModelSha256 &&
        first.OutputRow80Sha256 == Id65AuthoringCompositeCompiler.ExpectedCombinedOutputRow80Sha256 &&
        first.ChangedByteCount == Id65AuthoringCompositeCompiler.ExpectedCombinedChangedByteCount &&
        first.DiffRangeCount == Id65AuthoringCompositeCompiler.ExpectedCombinedDiffRangeCount &&
        first.DiffManifestSha256 == Id65AuthoringCompositeCompiler.ExpectedCombinedDiffManifestSha256 &&
        first.OwnedRangeMapSha256 == Id65AuthoringCompositeCompiler.ExpectedCombinedOwnedRangeMapSha256 &&
        first.RelocationMapSha256 == Id65AuthoringCompositeCompiler.ExpectedCombinedRelocationMapSha256 &&
        first.RebaseMapSha256 == Id65AuthoringCompositeCompiler.ExpectedCombinedRebaseMapSha256 &&
        first.MobyAssetRelocationMapSha256 ==
            Id65AuthoringCompositeCompiler.ExpectedCombinedMobyAssetRelocationMapSha256 &&
        first.TransactionSha256 == Id65AuthoringCompositeCompiler.ExpectedCombinedTransactionSha256 &&
        first.DeterministicPlanSha256 ==
            Id65AuthoringCompositeCompiler.ExpectedCombinedDeterministicPlanSha256,
    "The editor document did not project to the frozen single-pass composite witness.");
Require(first.TargetOverlaySha256 == Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 &&
        first.GlobalExecutableSha256 == Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256,
    "The projected document changed protected overlay or executable identity.");

Id65AuthoringCompositeTerrainReadback terrain = first.Terrain;
Require(terrain.MandatoryV3CollisionAndEnvironmentHeaderRepairs && terrain.OptionalTrianglePresent &&
        terrain.OptionalTriangle is not null &&
        terrain.OptionalTriangle.TriangleId == Id65AuthoringTerrainMutationCompiler.TriangleId &&
        terrain.OptionalTriangle.AddedVertexHandle == Id65AuthoringTerrainMutationCompiler.AddedVertexHandle &&
        terrain.OptionalTriangle.CollisionTriangleIndex == 19_808 &&
        terrain.OptionalTriangle.TargetCellSequence.SequenceEqual([19_808, 13_995]) &&
        terrain.OptionalTriangle.RenderNormalZ == 25_088 &&
        terrain.OptionalTriangle.CollisionNormalZ == -25_088,
    "The projected optional terrain semantic readback changed.");
Require(first.Texture.WitnessSha256 == Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 &&
        first.Texture.PrivateTextureId == 66 && first.Texture.DonorWadEntry == 70 &&
        first.Texture.DonorTextureId == 12 && first.Texture.TemplateTextureId == 25 &&
        first.Texture.OutputTextureId == 66 && first.Texture.AddedTerrainFacePreservedT25,
    "The projected private-T66 texture semantic readback changed.");
Require(first.MobyPlacements.Select(item => item.TargetTrueIndex).SequenceEqual([88, 107]) &&
        first.MobyPlacements.Single(item => item.TargetTrueIndex == 88).RowSha256 ==
            "4228cf9bf97a8309f26b9f1303174e116916e2f9633388f2346a654e97cdb56f" &&
        first.MobyPlacements.Single(item => item.TargetTrueIndex == 107).RowSha256 ==
            "d42e49248278759d2c2020bba4d4c4db18817bf120d73829a2bbba7dddcef9fb",
    "The projected T88/T107 Moby semantic readback changed.");
Require(first.Capacity.OutputUsedModelBytes == 0x94664 && first.Capacity.OutputZeroTailBytes == 0x19C &&
        first.Capacity.TextureCountAfter == 67 && first.Capacity.HighestTextureId == 66 &&
        first.Capacity.CollisionTriangleCount == 19_809 && first.Capacity.ObjectCountAfter == 108 &&
        first.Capacity.ActorRootCountAfter == 38 && first.Capacity.FixupCountAfter == 0x82,
    "The projected combined capacity readback changed.");
RequireAuthoredRebase(first.HandleRebases, "tile.remote-pad.1", "terrain-tile",
    new Id65LogicalAddress("terrain-tile", 216, 1));
RequireAuthoredRebase(first.HandleRebases, "tile.remote-pad.1:hp", "high-detail-face",
    new Id65LogicalAddress("scene-sector", 216, 1));
RequireAuthoredRebase(first.HandleRebases, "tile.remote-pad.1:lp", "low-detail-face",
    new Id65LogicalAddress("scene-sector", 216, 1));
string[] exactHighHandles =
    ["vertex.remote-pad.b:hp", "vertex.remote-pad.d:hp", "vertex.remote-pad.c:hp"];
string[] exactLowHandles =
    ["vertex.remote-pad.b:lp", "vertex.remote-pad.d:lp", "vertex.remote-pad.c:lp"];
int[] exactVertexOrdinals = [1, 3, 2];
for (int index = 0; index < exactVertexOrdinals.Length; index++)
{
    RequireAuthoredRebase(first.HandleRebases, exactHighHandles[index], "high-detail-vertex",
        new Id65LogicalAddress("scene-sector", 216, exactVertexOrdinals[index]));
    RequireAuthoredRebase(first.HandleRebases, exactLowHandles[index], "low-detail-vertex",
        new Id65LogicalAddress("scene-sector", 216, exactVertexOrdinals[index]));
}
RequireAuthoredRebase(first.HandleRebases, "tile.remote-pad.1:collision", "collision-triangle",
    new Id65LogicalAddress("collision-triangle", 19_808, 0));
RequireAuthoredRebase(first.HandleRebases, "occlusion.remote-pad.group0", "occlusion-membership",
    new Id65LogicalAddress("occlusion-group", 0, 216));
RequireAuthoredRebase(
    first.HandleRebases,
    Id65V2NativeTextureCompositionCompiler.PrivateTextureIntentId,
    "texture-record",
    new Id65LogicalAddress("texture-record", 66, 0));
RequireAuthoredRebase(first.HandleRebases, "terrain.remote-pad.face.0:hp", "high-detail-face",
    new Id65LogicalAddress("scene-sector", 216, 0));
RequireAuthoredRebase(first.HandleRebases, "terrain.remote-pad.face.0:lp", "low-detail-face",
    new Id65LogicalAddress("scene-sector", 216, 0));
RequirePreservedRebase(first.HandleRebases, document.BaselineHandles.Spawn, "landing-record",
    new Id65LogicalAddress("landing-record", 0, 0));
RequirePreservedRebase(first.HandleRebases, document.BaselineHandles.Music, "music-slot",
    new Id65LogicalAddress("music-slot", 35, 0));
RequirePreservedRebase(first.HandleRebases, document.BaselineHandles.Totals, "totals-slot",
    new Id65LogicalAddress("level-slot", 65, 0));
RequirePreservedRebase(first.HandleRebases, document.BaselineHandles.Exit, "exit-slot",
    new Id65LogicalAddress("level-slot", 65, 0));
RequirePreservedRebase(first.HandleRebases, document.BaselineHandles.Save, "save-slot",
    new Id65LogicalAddress("level-slot", 65, 0));

Id65AuthoringEditorDocument noTerrainDocument =
    Id65AuthoringEditorDocument.CreateExact(
        includeOptionalTerrain: false,
        placementIds: canonicalPlacementIds);
Id65AuthoringEditorProjection noTerrainProjection = Project(noTerrainDocument);
Id65CompiledAuthoringComposite noTerrain = noTerrainProjection.Compiled;
Require(noTerrain.Terrain.MandatoryV3CollisionAndEnvironmentHeaderRepairs &&
        !noTerrain.Terrain.OptionalTrianglePresent && noTerrain.Terrain.OptionalTriangle is null &&
        noTerrain.OutputModelSha256 == Id65V2NativeTextureCompositionCompiler.ExpectedOutputModelSha256 &&
        noTerrain.Capacity.OutputUsedModelBytes == 0x94634 &&
        noTerrain.MobyPlacements.Select(item => item.TargetTrueIndex).SequenceEqual([88, 107]),
    "Terrain-absent editor-document projection did not rebuild the clean T66/Moby target.");

byte[] lockedSource = first.CopySourceRow80();
byte[] output = Id65AuthoringCompositeCompiler.ApplyTransactional(first, lockedSource, reverse: false);
byte[] inverse = Id65AuthoringCompositeCompiler.ApplyTransactional(first, output, reverse: true);
Id65AuthoringCompositeRollbackPlan rollbackPlan =
    Id65AuthoringCompositeCompiler.CreateRollbackPlan(first);
byte[] plannedInverse = Id65AuthoringCompositeCompiler.ApplyRollback(rollbackPlan, output);
Require(output.SequenceEqual(first.CopyOutputRow80()) && inverse.SequenceEqual(lockedSource) &&
        plannedInverse.SequenceEqual(lockedSource) && rollbackPlan.RollbackOnly &&
        !rollbackPlan.AcceptedAsCompileInput &&
        Id65AuthoringCompositeCompiler.InvertRelocations(
            Id65AuthoringCompositeCompiler.InvertRelocations(first.Relocations)).SequenceEqual(first.Relocations) &&
        Id65AuthoringCompositeCompiler.InvertHandleRebases(
            Id65AuthoringCompositeCompiler.InvertHandleRebases(first.HandleRebases)).SequenceEqual(first.HandleRebases),
    "The projected composite did not preserve its exact transaction/rollback/inverse contract.");

byte[] escapedDocument = document.SerializeUtf8();
escapedDocument[0] ^= 1;
byte[] escapedOutput = first.CopyOutputRow80();
escapedOutput[0] ^= 1;
byte[] escapedOwned = first.OwnedRanges[0].CopyAfter();
escapedOwned[0] ^= 1;
Require(document.SerializeUtf8().SequenceEqual(documentUtf8) &&
        Hash(first.CopyOutputRow80()) == first.OutputRow80Sha256 &&
        Hash(first.OwnedRanges[0].CopyAfter()) == first.OwnedRanges[0].AfterSha256,
    "Editor-document or projected-composite storage escaped defensive copying.");

int rejectionCount = 0;
void RejectJson(string label, string category, Action<JsonObject> mutate)
{
    byte[] forged = RewriteJson(documentUtf8, mutate);
    ExpectReject(label, () => Id65AuthoringEditorDocument.Parse(forged), category);
    rejectionCount++;
}

RejectJson("profile", "profile", root => SetNamedScalar(root, "profileId", "forged-profile"));
RejectJson("version", "schema", root => SetNamedScalar(root, "schemaVersion", 2));
RejectJson("lab profile", "profile", root => SetNamedScalar(root, "labProfileId", "forged-lab-profile"));
RejectJson("lab version", "profile", root => SetNamedScalar(root, "labProfileVersion", 2));
RejectJson("workspace", "workspace", root => SetFirstMatchingString(root, "workspace", "forged-workspace"));
RejectJson("workspace layout version", "layout", root =>
    SetNamedScalar(root, "workspaceLayoutVersion", 2));
RejectJson("workspace layout root", "layout", root =>
    SetNamedScalar(root, "workspaceLayoutRelativeRoot", "_research/forged"));
RejectJson("locked-base profile", "profile", root =>
    SetNamedScalar(root, "lockedBaseProfileId", "forged-locked-base-profile"));
RejectJson("locked-base image hash", "unsupported or stale", root =>
    SetNamedScalar(root, "lockedBaseImageSha256", new string('0', 64)));
RejectJson("base hash", "locked row-80", root =>
    SetFirstMatchingString(root, "row80Sha256", new string('0', 64)));
RejectJson("composite profile", "profile", root =>
    SetNamedScalar(root, "compositeProfileId", "forged-composite-profile"));
RejectJson("private texture witness pin", "texture witness", root =>
    SetNamedScalar(root, "privateTextureWitnessSha256", new string('0', 64)));
RejectJson("Moby bundle pin", "Moby bundle", root =>
    SetNamedScalar(root, "mobyBundleSha256", new string('0', 64)));
RejectJson("stale HP render handle", "handle", root =>
    ReplaceStringValue(root, "vertex.remote-pad.b:hp", "vertex.remote-pad.stale:hp"));
RejectJson("stale LP render handle", "handle", root =>
    ReplaceStringValue(root, "vertex.remote-pad.b:lp", "vertex.remote-pad.stale:lp"));
RejectJson("stale collision handle", "handle", root =>
    ReplaceStringValue(root, "tile.remote-pad.1:collision", "tile.remote-pad.stale:collision"));
RejectJson("stale support handle", "placement", root =>
    ReplaceStringValue(root, "support.native-apron.t1353", "support.native-apron.stale"));
RejectJson("stale spawn handle", "ownership handles", root =>
    SetNamedScalar(root, "spawn", "spawn.stale"));
RejectJson("stale music handle", "ownership handles", root =>
    SetNamedScalar(root, "music", "music.stale"));
RejectJson("stale totals handle", "ownership handles", root =>
    SetNamedScalar(root, "totals", "totals.stale"));
RejectJson("stale exit handle", "ownership handles", root =>
    SetNamedScalar(root, "exit", "exit.stale"));
RejectJson("stale save handle", "ownership handles", root =>
    SetNamedScalar(root, "save", "save.stale"));
RejectJson("duplicate placement", "duplicate", root => DuplicateFirstArrayEntry(root, "mobyPlacements"));
RejectJson("duplicate terrain owner", "zero or one", root => DuplicateFirstArrayEntry(root, "terrain"));
RejectJson("terrain handle owner overlap", "handles", root =>
    DuplicateFirstStringArrayValue(root, "highPolyHandles"));
RejectJson("placement mismatch", "placement", root =>
    SetFirstMatchingNumberUnderArray(root, "mobyPlacements", "targetTrueIndex", 106));
RejectJson("unknown JSON field", "unknown", root => root.Add("unknownField", true));
RejectJson("unsupported payload field", "unsupported payload/address", root =>
    root.Add("nativePayloadBytes", "00"));
RejectJson("unsupported afterimage field", "unsupported payload/address", root =>
    root.Add("compiledAfterimage", "forged"));
RejectJson("unsupported address field", "unsupported payload/address", root =>
    root.Add("physicalAddress", 0));
RejectJson("content SHA-256 tamper", "content SHA-256", root =>
    SetNamedScalar(root, "contentSha256", new string('0', 64)));
ExpectReject(
    "UTF-8 BOM",
    () => Id65AuthoringEditorDocument.Parse(
        new byte[] { 0xEF, 0xBB, 0xBF }.Concat(documentUtf8).ToArray()),
    "BOM");
rejectionCount++;
ExpectReject(
    "noncanonical UTF-8 JSON",
    () => Id65AuthoringEditorDocument.Parse([.. documentUtf8, (byte)'\n']),
    "canonical UTF-8 JSON");
rejectionCount++;
ExpectReject(
    "oversized canonical JSON",
    () => Id65AuthoringEditorDocument.Parse(
        new byte[Id65AuthoringEditorDocument.MaximumSerializedByteCount + 1]),
    "canonical JSON limit");
rejectionCount++;
ExpectReject(
    "duplicate placement factory input",
    () => Id65AuthoringEditorDocument.CreateExact(
        placementIds:
        [
            Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId,
            Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId
        ]),
    "duplicate");
rejectionCount++;
ExpectReject(
    "unknown placement factory input",
    () => Id65AuthoringEditorDocument.CreateExact(placementIds: ["placement.unknown"]),
    "unsupported");
rejectionCount++;

Id65V2NativeTextureWitness forgedWitness = CopyWitness(
    textureWitness,
    canonicalSha256: new string('0', 64));
ExpectReject("texture witness mismatch", () => Project(document, witness: forgedWitness), "witness");
rejectionCount++;
Id65V2NativeTexturePackedRow[] tamperedRows = textureWitness.CopyPackedRows().ToArray();
Id65V2NativeTexturePackedRow rowToTamper = tamperedRows[0];
byte[] tamperedLowRow = rowToTamper.CopyLow();
tamperedLowRow[0] ^= 1;
tamperedRows[0] = new(
    rowToTamper.TextureId,
    rowToTamper.DonorWadEntry,
    rowToTamper.DonorTextureId,
    rowToTamper.Synthetic,
    tamperedLowRow,
    rowToTamper.CopyHigh(),
    rowToTamper.LowSha256,
    rowToTamper.HighSha256);
Id65V2NativeTextureWitness rowTamperedWitness = CopyWitness(
    textureWitness,
    packedRows: tamperedRows);
Require(rowTamperedWitness.CanonicalSha256 == document.PrivateTextureWitnessSha256,
    "The row-tampered witness did not retain the document's declared expected SHA-256.");
ExpectReject(
    "same-declared-hash texture row tamper",
    () => Project(document, witness: rowTamperedWitness),
    "packed row");
rejectionCount++;
Id65V2NativeTexturePagePatch[] tamperedPatches = textureWitness.CopyPagePatches().ToArray();
Id65V2NativeTexturePagePatch patchToTamper = tamperedPatches[0];
byte[] tamperedPatchAfter = patchToTamper.CopyAfter();
tamperedPatchAfter[0] ^= 1;
tamperedPatches[0] = new(
    patchToTamper.RelativeOffset,
    patchToTamper.CopyBefore(),
    tamperedPatchAfter,
    patchToTamper.BeforeSha256,
    patchToTamper.AfterSha256,
    patchToTamper.Owner);
Id65V2NativeTextureWitness pageTamperedWitness = CopyWitness(
    textureWitness,
    pagePatches: tamperedPatches);
Require(pageTamperedWitness.CanonicalSha256 == document.PrivateTextureWitnessSha256,
    "The page-tampered witness did not retain the document's declared expected SHA-256.");
ExpectReject(
    "same-declared-hash texture page tamper",
    () => Project(document, witness: pageTamperedWitness),
    "page patch");
rejectionCount++;
Id65MobyDependencyBundleDescriptor forgedBundle = CopyBundle(
    bundle,
    canonicalSha256: new string('0', 64));
ExpectReject("Moby bundle mismatch", () => Project(document, suppliedBundle: forgedBundle), "bundle");
rejectionCount++;
Id65MobyRowTemplate[] tamperedBundleRows = bundle.Rows.ToArray();
tamperedBundleRows[0] = tamperedBundleRows[0] with
{
    ActorId = checked((ushort)(tamperedBundleRows[0].ActorId + 1))
};
Id65MobyDependencyBundleDescriptor fullFieldTamperedBundle = CopyBundle(
    bundle,
    rows: tamperedBundleRows);
Require(fullFieldTamperedBundle.CanonicalSha256 == document.MobyBundleSha256,
    "The full-field-tampered bundle did not retain the document's declared expected SHA-256.");
ExpectReject(
    "same-declared-hash Moby bundle full-field tamper",
    () => Project(document, suppliedBundle: fullFieldTamperedBundle),
    "full-field");
rejectionCount++;
ExpectReject(
    "capacity overflow",
    () => Project(document, limits: new Id65AuthoringCompositeCompilerLimits(MaximumObjectCount: 107)),
    "capacity");
rejectionCount++;
byte[] tamperedRow80 = source.CopyRow80();
tamperedRow80[0] ^= 1;
Id65AuthoringCompositeLockedSource tamperedSource =
    new(tamperedRow80, source.CopyTargetOverlay(), source.CopyGlobalExecutable());
ExpectReject("source tamper", () => Project(document, selectedSource: tamperedSource), "source");
rejectionCount++;

Require(rejectionCount == 43, $"The editor-document rejection matrix ran {rejectionCount}, expected 43.");
AssertFailClosedBoundary(document, "document");
AssertFailClosedBoundary(firstProjection, "projection");
AssertFailClosedBoundary(noTerrainProjection, "terrain-absent projection");
Require(!document.ContainsNativePayloadBytes && !document.ContainsPhysicalAddresses &&
        !document.ContainsFilePaths && !document.ContainsCompiledAfterimage &&
        !document.ContainsPublisherState &&
        firstProjection.DocumentIdentityVerified && firstProjection.TerrainHandlesVerified &&
        firstProjection.CollisionMembershipVerified && firstProjection.OcclusionMembershipVerified &&
        firstProjection.PrivateTextureBindingVerified && firstProjection.MobyPlacementBindingVerified &&
        firstProjection.BaselineOwnershipHandlesVerified &&
        firstProjection.SemanticReadbackVerified && firstProjection.ExactFactoriesOnly,
    "The editor document/projector proof or data-exclusion boundary weakened.");
Require(first.BuildsDirectlyFromLockedSource && first.SinglePassCompositeRebuildOwned &&
        !first.AcceptsCompiledSliceAfterimages && !first.AcceptsSlicePlans &&
        !first.CrossSliceCompositionSupported && first.AppliesOneNonOverlappingLedger &&
        first.ExactInverseVerified && first.DeterministicReadbackRequired &&
        !first.WritesFileSystem && !first.WritesDiscImage && !first.WritesCue &&
        !first.RetiredPublisherCalled && !first.AppIntegrated && !first.CreateBinEnabled &&
        !first.NormalCreateBinEnabled && !first.RuntimeCandidateAuthorized &&
        !first.ReleaseIntegrated && !first.PromotionAuthorized && !first.Publishable,
    "The projected composite fail-closed boundary weakened.");
PropertyInfo[] requestProperties = typeof(Id65AuthoringEditorProjectionRequest).GetProperties();
Dictionary<string, Type> expectedRequestProperties = new(StringComparer.Ordinal)
{
    ["Document"] = typeof(Id65AuthoringEditorDocument),
    ["LockedSource"] = typeof(Id65AuthoringCompositeLockedSource),
    ["PrivateTextureWitness"] = typeof(Id65V2NativeTextureWitness),
    ["GrassBundle"] = typeof(Id65MobyDependencyBundleDescriptor),
    ["Limits"] = typeof(Id65AuthoringCompositeCompilerLimits)
};
Require(requestProperties.Length == expectedRequestProperties.Count &&
        requestProperties.All(property =>
            expectedRequestProperties.TryGetValue(property.Name, out Type? expectedType) &&
            property.PropertyType == expectedType),
    "The editor projection request property shape changed.");
ConstructorInfo[] requestConstructors = typeof(Id65AuthoringEditorProjectionRequest).GetConstructors(
    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
ConstructorInfo[] primaryRequestConstructors = requestConstructors
    .Where(constructor => constructor.GetParameters().Length == 5)
    .ToArray();
ConstructorInfo[] copyRequestConstructors = requestConstructors
    .Where(constructor => constructor.GetParameters().Length == 1 &&
        constructor.GetParameters()[0].ParameterType == typeof(Id65AuthoringEditorProjectionRequest))
    .ToArray();
Require(requestConstructors.Length == 2 && primaryRequestConstructors.Length == 1 &&
        copyRequestConstructors.Length == 1 && copyRequestConstructors[0].IsPrivate &&
        ExactParameters(
            primaryRequestConstructors[0],
            [
                typeof(Id65AuthoringEditorDocument),
                typeof(Id65AuthoringCompositeLockedSource),
                typeof(Id65V2NativeTextureWitness),
                typeof(Id65MobyDependencyBundleDescriptor),
                typeof(Id65AuthoringCompositeCompilerLimits)
            ]) &&
        primaryRequestConstructors[0].GetParameters().Take(4).All(parameter => !parameter.HasDefaultValue) &&
        primaryRequestConstructors[0].GetParameters()[4].HasDefaultValue &&
        primaryRequestConstructors[0].GetParameters()[4].DefaultValue is null,
    "The editor projection request constructor surface changed or accepts an unowned input shape.");
MethodInfo[] requestMethods = typeof(Id65AuthoringEditorProjectionRequest).GetMethods(
    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
    BindingFlags.DeclaredOnly);
Require(!requestMethods.Any(method =>
            method.IsStatic && method.ReturnType == typeof(Id65AuthoringEditorProjectionRequest)) &&
        requestMethods.SelectMany(method => method.GetParameters()).All(parameter =>
            !IsForbiddenProjectionInput(parameter.ParameterType)),
    "The editor projection request acquired a static factory or method accepting a path, byte payload, " +
    "static plan, compiled afterimage, or rollback plan.");
MethodInfo[] declaredProjectorMethods = typeof(Id65AuthoringEditorDocumentProjector).GetMethods(
    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
MethodInfo[] projectorMethods = declaredProjectorMethods
    .Where(method => method.Name == "Project")
    .ToArray();
MethodInfo[] publicProjectorMethods = declaredProjectorMethods
    .Where(method => method.IsPublic)
    .ToArray();
Require(projectorMethods.Length == 1 && publicProjectorMethods.Length == 1 &&
        projectorMethods[0] == publicProjectorMethods[0] &&
        projectorMethods[0].ReturnType == typeof(Id65AuthoringEditorProjection) &&
        ExactParameters(projectorMethods[0], [typeof(Id65AuthoringEditorProjectionRequest)]),
    "The editor document projector no longer exposes exactly one Project(request) entrypoint.");
VerifySourceTopology(repositoryRoot);
Require(SnapshotFile(lockedImagePath) == lockedBefore &&
        SnapshotFile(lockedCuePath) == lockedCueBefore &&
        SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!).SequenceEqual(lockedDirectoryBefore),
    "The pure editor-document smoke changed or published beside an input artifact.");

Console.WriteLine(
    "PASS Id65AuthoringEditorDocumentSmoke: canonical/reordered UTF-8 document round-trip, " +
    $"document={firstProjection.DocumentCanonicalSha256}; manifest={first.ManifestSha256}; " +
    $"row80={first.OutputRow80Sha256}; plan={first.DeterministicPlanSha256}; rejects={rejectionCount}; " +
    "terrain present/absent, exact T66 + T88/T107 projection, inverse/rollback, defensive copies, " +
    "and no filesystem/BIN/CUE/App/CreateBIN/runtime/release/promotion/publication.");

Id65AuthoringEditorProjection Project(
    Id65AuthoringEditorDocument selectedDocument,
    Id65AuthoringCompositeLockedSource? selectedSource = null,
    Id65V2NativeTextureWitness? witness = null,
    Id65MobyDependencyBundleDescriptor? suppliedBundle = null,
    Id65AuthoringCompositeCompilerLimits? limits = null) =>
    Id65AuthoringEditorDocumentProjector.Project(new(
        selectedDocument,
        selectedSource ?? source,
        witness ?? textureWitness,
        suppliedBundle ?? bundle,
        limits));
}
finally
{
    if (Directory.Exists(smokeRoot))
    {
        Directory.Delete(smokeRoot, recursive: true);
    }
    Require(!Directory.Exists(smokeRoot),
        "The editor-document smoke retained temporary game data.");
}

static bool Equivalent(Id65CompiledAuthoringComposite left, Id65CompiledAuthoringComposite right) =>
    left.ManifestSha256 == right.ManifestSha256 &&
    left.SourceRow80Sha256 == right.SourceRow80Sha256 &&
    left.OutputRow80Sha256 == right.OutputRow80Sha256 &&
    left.SourceModelSha256 == right.SourceModelSha256 &&
    left.OutputModelSha256 == right.OutputModelSha256 &&
    left.ChangedByteCount == right.ChangedByteCount &&
    left.DiffRangeCount == right.DiffRangeCount &&
    left.DiffManifestSha256 == right.DiffManifestSha256 &&
    left.OwnedRangeMapSha256 == right.OwnedRangeMapSha256 &&
    left.RelocationMapSha256 == right.RelocationMapSha256 &&
    left.RebaseMapSha256 == right.RebaseMapSha256 &&
    left.MobyAssetRelocationMapSha256 == right.MobyAssetRelocationMapSha256 &&
    left.TransactionSha256 == right.TransactionSha256 &&
    left.DeterministicPlanSha256 == right.DeterministicPlanSha256 &&
    left.OwnedRanges.Select(ToOwnedTuple).SequenceEqual(right.OwnedRanges.Select(ToOwnedTuple)) &&
    left.DiffRanges.SequenceEqual(right.DiffRanges) &&
    left.Relocations.SequenceEqual(right.Relocations) &&
    left.HandleRebases.SequenceEqual(right.HandleRebases) &&
    left.MobyAssetRelocations.SequenceEqual(right.MobyAssetRelocations) &&
    left.MobyPlacements.SequenceEqual(right.MobyPlacements) &&
    TerrainEquivalent(left.Terrain, right.Terrain) &&
    left.Texture == right.Texture && left.Capacity == right.Capacity &&
    left.CopyOutputRow80().SequenceEqual(right.CopyOutputRow80());

static bool TerrainEquivalent(
    Id65AuthoringCompositeTerrainReadback left,
    Id65AuthoringCompositeTerrainReadback right) =>
    left.MandatoryV3CollisionAndEnvironmentHeaderRepairs == right.MandatoryV3CollisionAndEnvironmentHeaderRepairs &&
    left.OptionalTrianglePresent == right.OptionalTrianglePresent &&
    left.SectorIndex == right.SectorIndex &&
    left.LowDetailVertexCount == right.LowDetailVertexCount &&
    left.LowDetailFaceCount == right.LowDetailFaceCount &&
    left.HighDetailVertexCount == right.HighDetailVertexCount &&
    left.HighDetailFaceCount == right.HighDetailFaceCount &&
    left.BaseCollisionTriangleIndex == right.BaseCollisionTriangleIndex &&
    left.BaseCollisionTriangleHex == right.BaseCollisionTriangleHex &&
    left.BaseCollisionNormalZ == right.BaseCollisionNormalZ &&
    left.EnvironmentSha256 == right.EnvironmentSha256 &&
    left.SectorSha256 == right.SectorSha256 &&
    left.CollisionSha256 == right.CollisionSha256 &&
    left.CollisionTreeSha256 == right.CollisionTreeSha256 &&
    left.CollisionBlocksSha256 == right.CollisionBlocksSha256 &&
    left.CollisionBlocksUsedBytes == right.CollisionBlocksUsedBytes &&
    left.OcclusionGroupIndex == right.OcclusionGroupIndex &&
    left.OcclusionContainsSector == right.OcclusionContainsSector &&
    TriangleEquivalent(left.OptionalTriangle, right.OptionalTriangle);

static bool TriangleEquivalent(Id65TerrainTriangleReadback? left, Id65TerrainTriangleReadback? right)
{
    if (left is null || right is null)
        return left is null && right is null;
    return left.TriangleId == right.TriangleId &&
        left.AddedVertexHandle == right.AddedVertexHandle &&
        left.PresentInSource == right.PresentInSource &&
        left.PresentInOutput == right.PresentInOutput &&
        left.SectorIndex == right.SectorIndex &&
        left.LowDetailVertexIndex == right.LowDetailVertexIndex &&
        left.HighDetailVertexIndex == right.HighDetailVertexIndex &&
        left.LowDetailFaceIndex == right.LowDetailFaceIndex &&
        left.HighDetailFaceIndex == right.HighDetailFaceIndex &&
        left.CollisionTriangleIndex == right.CollisionTriangleIndex &&
        left.AddedVertexWordHex == right.AddedVertexWordHex &&
        left.LowDetailFaceHex == right.LowDetailFaceHex &&
        left.HighDetailFaceHex == right.HighDetailFaceHex &&
        left.CollisionTriangleHex == right.CollisionTriangleHex &&
        left.RenderNormalZ == right.RenderNormalZ &&
        left.CollisionNormalZ == right.CollisionNormalZ &&
        left.CollisionCell == right.CollisionCell &&
        left.TargetCellSequence.SequenceEqual(right.TargetCellSequence) &&
        left.OcclusionGroupIndex == right.OcclusionGroupIndex;
}

static (string, string, int, int, string, string) ToOwnedTuple(Id65AuthoringCompositeOwnedRange range) =>
    (range.StableId, range.OwnerId, range.DataRelativeOffset, range.ByteLength,
        range.BeforeSha256, range.AfterSha256);

static bool ExactParameters(MethodBase member, IReadOnlyList<Type> expectedTypes)
{
    ParameterInfo[] actual = member.GetParameters();
    return actual.Length == expectedTypes.Count &&
        actual.Select(parameter => parameter.ParameterType).SequenceEqual(expectedTypes);
}

static bool IsForbiddenProjectionInput(Type type)
{
    Type unwrapped = type.IsByRef || type.IsPointer || type.IsArray
        ? type.GetElementType() ?? type
        : type;
    return type == typeof(byte[]) || unwrapped == typeof(string) ||
        unwrapped == typeof(Id65CompiledAuthoringComposite) ||
        unwrapped == typeof(Id65AuthoringCompositeRollbackPlan) ||
        unwrapped.Name.Contains("StaticPlan", StringComparison.Ordinal) ||
        unwrapped.Name.Contains("Compiled", StringComparison.Ordinal) ||
        unwrapped.Name.Contains("Rollback", StringComparison.Ordinal);
}

static void RequireAuthoredRebase(
    IReadOnlyList<Id65StableHandleRebase> rebases,
    string stableId,
    string kind,
    Id65LogicalAddress output)
{
    Id65StableHandleRebase rebase = rebases.Single(item => item.StableId == stableId);
    Require(rebase.Kind == kind && rebase.Source is null && rebase.Output == output,
        $"Authored stable handle `{stableId}` did not map exactly to {output}.");
}

static void RequirePreservedRebase(
    IReadOnlyList<Id65StableHandleRebase> rebases,
    string stableId,
    string kind,
    Id65LogicalAddress address)
{
    Id65StableHandleRebase rebase = rebases.Single(item => item.StableId == stableId);
    Require(rebase.Kind == kind && rebase.Source == address && rebase.Output == address,
        $"Preserved baseline handle `{stableId}` changed from {address}.");
}

static byte[] RewriteJson(byte[] canonicalUtf8, Action<JsonObject> mutate)
{
    JsonObject root = JsonNode.Parse(canonicalUtf8)?.AsObject() ??
        throw new InvalidDataException("The canonical editor document did not parse as a JSON object in smoke.");
    mutate(root);
    return Encoding.UTF8.GetBytes(root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
}

static byte[] DeriveContentPreimageUtf8(byte[] canonicalUtf8)
{
    using JsonDocument parsed = JsonDocument.Parse(canonicalUtf8);
    Require(parsed.RootElement.ValueKind == JsonValueKind.Object,
        "The canonical editor document is not a root object.");
    JsonProperty[] properties = parsed.RootElement.EnumerateObject().ToArray();
    Require(properties.Length > 0 && properties[^1].NameEquals("contentSha256") &&
            properties.Count(property => property.NameEquals("contentSha256")) == 1,
        "The content SHA-256 field is not the sole final root property.");
    using MemoryStream buffer = new();
    using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions
           {
               Indented = false,
               SkipValidation = false
           }))
    {
        writer.WriteStartObject();
        foreach (JsonProperty property in properties[..^1])
        {
            writer.WritePropertyName(property.Name);
            property.Value.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
    return buffer.ToArray();
}

static void SetNamedScalar(JsonObject root, string propertyName, object value)
{
    if (!TryFindProperty(root, propertyName, out JsonObject? owner, out string? actualName))
        throw new InvalidOperationException($"Missing `{propertyName}` in canonical editor document.");
    owner[actualName] = JsonSerializer.SerializeToNode(value, value.GetType());
}

static void SetFirstMatchingString(JsonObject root, string nameFragment, string value)
{
    foreach ((JsonObject owner, string name, JsonNode? node) in EnumerateProperties(root))
    {
        if (name.Contains(nameFragment, StringComparison.OrdinalIgnoreCase) &&
            node is JsonValue scalar && scalar.TryGetValue(out string? _))
        {
            owner[name] = value;
            return;
        }
    }
    throw new InvalidOperationException($"Missing string property containing `{nameFragment}`.");
}

static void ReplaceStringValue(JsonObject root, string expected, string replacement)
{
    foreach ((JsonObject owner, string name, JsonNode? node) in EnumerateProperties(root))
    {
        if (node is JsonValue scalar && scalar.TryGetValue(out string? value) && value == expected)
        {
            owner[name] = replacement;
            return;
        }
        if (node is JsonArray array)
        {
            for (int index = 0; index < array.Count; index++)
            {
                if (array[index] is JsonValue item && item.TryGetValue(out string? itemValue) && itemValue == expected)
                {
                    array[index] = replacement;
                    return;
                }
            }
        }
    }
    throw new InvalidOperationException($"Missing exact string `{expected}` in canonical editor document.");
}

static void DuplicateFirstArrayEntry(JsonObject root, string propertyName)
{
    if (!TryFindProperty(root, propertyName, out JsonObject? owner, out string? actualName) ||
        owner[actualName] is not JsonArray array || array.Count == 0)
        throw new InvalidOperationException($"Missing nonempty `{propertyName}` array in canonical editor document.");
    array.Add(array[0]?.DeepClone());
}

static void DuplicateFirstStringArrayValue(JsonObject root, string propertyName)
{
    if (!TryFindProperty(root, propertyName, out JsonObject? owner, out string? actualName) ||
        owner[actualName] is not JsonArray array || array.Count < 2 || array[0] is null)
        throw new InvalidOperationException($"Missing two-entry `{propertyName}` array in canonical editor document.");
    array[1] = array[0]!.DeepClone();
}

static void SetFirstMatchingNumberUnderArray(
    JsonObject root,
    string arrayPropertyName,
    string numberPropertyName,
    int value)
{
    if (!TryFindProperty(root, arrayPropertyName, out JsonObject? owner, out string? actualName) ||
        owner[actualName] is not JsonArray array)
        throw new InvalidOperationException($"Missing `{arrayPropertyName}` array in canonical editor document.");
    JsonObject first = array.OfType<JsonObject>().FirstOrDefault() ??
        throw new InvalidOperationException($"The `{arrayPropertyName}` array has no object element.");
    if (!TryFindProperty(first, numberPropertyName, out JsonObject? numberOwner, out string? numberName))
        throw new InvalidOperationException($"Missing `{numberPropertyName}` in `{arrayPropertyName}`.");
    numberOwner[numberName] = value;
}

static bool TryFindProperty(
    JsonObject root,
    string propertyName,
    out JsonObject owner,
    out string actualName)
{
    foreach ((JsonObject candidateOwner, string name, JsonNode? _) in EnumerateProperties(root))
    {
        if (name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
        {
            owner = candidateOwner;
            actualName = name;
            return true;
        }
    }
    owner = null!;
    actualName = null!;
    return false;
}

static IEnumerable<(JsonObject Owner, string Name, JsonNode? Value)> EnumerateProperties(JsonNode node)
{
    if (node is JsonObject obj)
    {
        foreach ((string name, JsonNode? value) in obj)
        {
            yield return (obj, name, value);
            if (value is not null)
                foreach (var nested in EnumerateProperties(value))
                    yield return nested;
        }
    }
    else if (node is JsonArray array)
    {
        foreach (JsonNode? value in array)
            if (value is not null)
                foreach (var nested in EnumerateProperties(value))
                    yield return nested;
    }
}

static void AssertFailClosedBoundary(object value, string label)
{
    string[] falseProperties =
    [
        "WritesFileSystem",
        "WritesDiscImage",
        "WritesCue",
        "PersistenceEnabled",
        "AppIntegrated",
        "CreateBinEnabled",
        "NormalCreateBinEnabled",
        "RuntimeCandidateAuthorized",
        "ReleaseIntegrated",
        "PromotionAuthorized",
        "Publishable"
    ];
    foreach (string name in falseProperties)
    {
        PropertyInfo property = value.GetType().GetProperty(name) ??
            throw new InvalidOperationException($"The {label} does not expose fail-closed `{name}`.");
        Require(property.PropertyType == typeof(bool) && property.GetValue(value) is false,
            $"The {label} unexpectedly enabled `{name}`.");
    }
}

static void VerifySourceTopology(string repositoryRoot)
{
    string coreRoot = Path.Combine(repositoryRoot, "src", "Spyro.Editor.Core");
    string smokeRoot = Path.Combine(
        repositoryRoot,
        "src",
        "Spyro.Editor.Id65AuthoringEditorDocumentSmoke");
    string[] files = new[] { coreRoot, smokeRoot }
        .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        .Where(path => !HasPathSegment(path, "bin") && !HasPathSegment(path, "obj"))
        .Order(StringComparer.Ordinal)
        .ToArray();
    string[] symbolOwners = files
        .Where(path => File.ReadAllText(path).Contains(
            "Id65AuthoringEditorDocument",
            StringComparison.Ordinal))
        .Select(path => Path.GetRelativePath(repositoryRoot, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    string[] expectedOwners =
    [
        Path.Combine("src", "Spyro.Editor.Core", "Exporting", "Id65AuthoringEditorDocument.cs"),
        Path.Combine("src", "Spyro.Editor.Core", "Persistence", "Id65AuthoringEditorAggregateCandidateMaterializer.cs"),
        Path.Combine("src", "Spyro.Editor.Core", "Persistence", "Id65AuthoringEditorAggregatePlanProjector.cs"),
        Path.Combine("src", "Spyro.Editor.Core", "Persistence", "Id65AuthoringEditorControlStateCompiler.cs"),
        Path.Combine("src", "Spyro.Editor.Core", "Persistence", "Id65AuthoringEditorDocumentStore.cs"),
        Path.Combine("src", "Spyro.Editor.Core", "Persistence", "Id65AuthoringEditorDocumentWorkspaceInspector.cs"),
        Path.Combine("src", "Spyro.Editor.Core", "Persistence", "Id65AuthoringEditorDocumentWorkspaceWriter.cs"),
        Path.Combine("src", "Spyro.Editor.Core", "Persistence", "PortableProjectMigration.cs"),
        Path.Combine("src", "Spyro.Editor.Id65AuthoringEditorDocumentSmoke", "Program.cs")
    ];
    Require(symbolOwners.SequenceEqual(expectedOwners.Order(StringComparer.Ordinal)),
        $"The editor-document Core/smoke source topology changed: {string.Join(", ", symbolOwners)}.");

    Regex projectorMember = new(
        @"\bId65AuthoringEditorDocumentProjector\s*\.\s*Project\b",
        RegexOptions.CultureInvariant);
    Regex projectorCall = new(
        @"\bId65AuthoringEditorDocumentProjector\s*\.\s*Project\s*\(",
        RegexOptions.CultureInvariant);
    string[] callsiteOwners = files
        .Where(path => projectorCall.Matches(File.ReadAllText(path)).Count > 0)
        .Select(path => Path.GetRelativePath(repositoryRoot, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    string[] expectedCallsiteOwners =
    [
        Path.Combine("src", "Spyro.Editor.Core", "Persistence", "Id65AuthoringEditorAggregatePlanProjector.cs"),
        Path.Combine("src", "Spyro.Editor.Core", "Persistence", "Id65AuthoringEditorDocumentStore.cs"),
        Path.Combine("src", "Spyro.Editor.Id65AuthoringEditorDocumentSmoke", "Program.cs")
    ];
    Require(callsiteOwners.SequenceEqual(expectedCallsiteOwners.Order(StringComparer.Ordinal)) &&
            callsiteOwners.All(relative =>
            {
                string source = File.ReadAllText(Path.Combine(repositoryRoot, relative));
                return projectorCall.Matches(source).Count == 1 &&
                    projectorMember.Matches(source).Count == 1;
            }),
        $"The editor-document projector Core/smoke callsite topology changed: {string.Join(", ", callsiteOwners)}.");

    // This is an intentionally conservative lexical tripwire for common callsite
    // indirections. It is not a compiler/IL call graph proof; that stronger boundary
    // remains a future integration gate.
    Regex usingStaticProjector = new(
        @"\busing\s+static\s+(?:(?:global::)?Spyro\.Editor\.Core\.Exporting\.)?Id65AuthoringEditorDocumentProjector\s*;",
        RegexOptions.CultureInvariant);
    Regex projectorAlias = new(
        @"\busing\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*(?:(?:global::)?Spyro\.Editor\.Core\.Exporting\.)?Id65AuthoringEditorDocumentProjector\s*;",
        RegexOptions.CultureInvariant);
    Regex projectorTypeOf = new(
        @"\btypeof\s*\(\s*(?:(?:global::)?Spyro\.Editor\.Core\.Exporting\.)?Id65AuthoringEditorDocumentProjector\s*\)",
        RegexOptions.CultureInvariant);
    Regex lateBoundKeyword = new(@"\bdynam[i]c\b", RegexOptions.CultureInvariant);
    Regex singularMethodLookup = new(@"\bGetMeth[o]d\s*\(", RegexOptions.CultureInvariant);
    Regex lateBoundInvoke = new(
        @"(?:\.\s*Invok[e]\s*\(|\bInvokeMembe[r]\s*\(|\bDynamicInvok[e]\s*\(|\bCreateDelegat[e]\s*\()",
        RegexOptions.CultureInvariant);
    string smokeRelative = Path.Combine(
        "src",
        "Spyro.Editor.Id65AuthoringEditorDocumentSmoke",
        "Program.cs");
    foreach (string path in files)
    {
        string relative = Path.GetRelativePath(repositoryRoot, path);
        string sourceText = File.ReadAllText(path);
        if (!sourceText.Contains("Id65AuthoringEditorDocumentProjector", StringComparison.Ordinal))
            continue;
        Require(!usingStaticProjector.IsMatch(sourceText) && !projectorAlias.IsMatch(sourceText) &&
                !lateBoundKeyword.IsMatch(sourceText) && !singularMethodLookup.IsMatch(sourceText) &&
                !lateBoundInvoke.IsMatch(sourceText),
            $"The editor-document projector acquired a using-static, alias, late-bound, or reflection call path in {relative}.");
        int typeOfCount = projectorTypeOf.Matches(sourceText).Count;
        Require(typeOfCount == (relative == smokeRelative ? 1 : 0),
            $"The editor-document projector reflection surface changed in {relative}.");
    }
}

static bool HasPathSegment(string path, string segment) =>
    path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Contains(segment, StringComparer.OrdinalIgnoreCase);

static Id65V2NativeTextureWitness CopyWitness(
    Id65V2NativeTextureWitness source,
    IEnumerable<Id65V2NativeTexturePackedRow>? packedRows = null,
    IEnumerable<Id65V2NativeTexturePagePatch>? pagePatches = null,
    string? canonicalSha256 = null) =>
    new(
        source.ProfileId,
        source.DonorManifestId,
        source.DonorCompleteRecordSha256,
        source.SourceTexturePagesSha256,
        source.OutputTexturePagesSha256,
        source.SourceTextureComponentSha256,
        source.OutputTextureComponentSha256,
        source.SourceTextureCount,
        source.OutputTextureCount,
        source.PrivateTextureId,
        source.DonorWadEntry,
        source.DonorTextureId,
        source.MaterialTemplateTextureId,
        packedRows ?? source.CopyPackedRows(),
        pagePatches ?? source.CopyPagePatches(),
        canonicalSha256 ?? source.CanonicalSha256);

static Id65MobyDependencyBundleDescriptor CopyBundle(
    Id65MobyDependencyBundleDescriptor source,
    IEnumerable<Id65MobyRowTemplate>? rows = null,
    string? canonicalSha256 = null) =>
    new(
        source.Id,
        source.SchemaVersion,
        source.FoundationContractSha256,
        rows ?? source.Rows,
        source.Properties,
        source.ActorPackages,
        source.ActorRoots,
        source.Fixups,
        source.ExternalDependencies,
        source.BehaviorClosure,
        source.RuntimeDependencies,
        source.CanonicalJson,
        canonicalSha256 ?? source.CanonicalSha256);

static void ExpectReject(string label, Action action, string requiredText)
{
    bool rejected = false;
    try
    {
        action();
    }
    catch (Exception ex) when (ex.ToString().Contains(requiredText, StringComparison.OrdinalIgnoreCase))
    {
        rejected = true;
    }
    Require(rejected, $"Atomic rejection `{label}` was not observed with text `{requiredText}`.");
}

static FileState SnapshotFile(string path) =>
    new(new FileInfo(path).Length, File.GetLastWriteTimeUtc(path), HashFile(path));

static IReadOnlyList<string> SnapshotDirectory(string path) =>
    Directory.GetFileSystemEntries(path)
        .Select(entry => $"{Path.GetFileName(entry)}|{new FileInfo(entry).Length}|{File.GetLastWriteTimeUtc(entry):O}")
        .Order(StringComparer.Ordinal)
        .ToArray();

static string HashFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexStringLower(SHA256.HashData(stream));
}

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexStringLower(SHA256.HashData(bytes));

static string FindRepositoryRoot(string? start)
{
    DirectoryInfo? current = new(Path.GetFullPath(start ?? Directory.GetCurrentDirectory()));
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "global.json")) &&
            Directory.Exists(Path.Combine(current.FullName, "src", "Spyro.Editor.Core")))
            return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record FileState(long ByteLength, DateTime LastWriteUtc, string Sha256);

using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Workspace;

const string SmokeRootPrefix =
    "Spyro.Editor.Id65AuthoringEditorDocumentWorkspaceInspectorSmoke-";
const string LockedFixtureRelativePath =
    "_local/v5-stone-hill-level-replacement/unused-level-65-display-name/" +
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin";

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string lockedFixturePath = Path.Combine(repositoryRoot, LockedFixtureRelativePath);
Require(File.Exists(lockedFixturePath), $"Missing inspector-smoke fixture: {lockedFixturePath}");
FixtureState fixtureBefore = SnapshotFixture(lockedFixturePath);

string smokeRoot = Path.Combine(
    Path.GetTempPath(),
    $"{SmokeRootPrefix}{Guid.NewGuid():N}");
string workspaceContainer = Path.Combine(smokeRoot, "workspace-container");
Directory.CreateDirectory(smokeRoot);

try
{
    UnusedLevel65BlankLevelLabWorkspacePaths paths =
        UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(workspaceContainer);
    UnusedLevel65BlankLevelLabManifest manifest =
        UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(paths);
    await CreateReadOnlyFixtureWorkspaceAsync(paths, manifest, lockedFixturePath);

    Require(
        Id65AuthoringEditorDocumentWorkspaceInspector.DocumentFileName ==
            Id65AuthoringEditorDocumentStore.FileName &&
        Id65AuthoringEditorDocumentWorkspaceInspector.DocumentFileName ==
            "unusedlevel65blank-authoring-document-v1.json",
        "The public inspector filename is not the canonical store filename.");

    IReadOnlyList<TreeEntry> beforeMissing = SnapshotTree(paths.RootPath);
    Id65AuthoringEditorDocumentReadModel? missing =
        await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(paths, manifest);
    Require(missing == null, "A missing persisted document did not inspect as absent.");
    Require(
        SnapshotTree(paths.RootPath).SequenceEqual(beforeMissing),
        "Missing-document inspection mutated the lab workspace.");
    Require(
        !Directory.Exists(paths.AuthoredEditsDirectoryPath),
        "Missing-document inspection created the authored-edits directory.");

    Directory.CreateDirectory(paths.AuthoredEditsDirectoryPath);
    string documentPath = Id65AuthoringEditorDocumentStore.GetPath(paths);
    Directory.CreateDirectory(documentPath);
    try
    {
        await ExpectRejectedAsync(
            "document-path directory",
            async () => _ = await Id65AuthoringEditorDocumentWorkspaceInspector
                .InspectAsync(paths, manifest),
            "is a directory");
    }
    finally
    {
        Directory.Delete(documentPath);
    }
    string danglingDocumentTarget = Path.Combine(smokeRoot, "missing-document-target.json");
    File.CreateSymbolicLink(documentPath, danglingDocumentTarget);
    try
    {
        await ExpectRejectedAsync(
            "dangling document symbolic link",
            async () => _ = await Id65AuthoringEditorDocumentWorkspaceInspector
                .InspectAsync(paths, manifest),
            "escapes its authored-edits root");
    }
    finally
    {
        File.Delete(documentPath);
    }
    Require(
        !File.Exists(danglingDocumentTarget),
        "Dangling-link refusal created its missing target.");

    Id65AuthoringEditorDocument fullDocument =
        Id65AuthoringEditorDocument.CreateExact(includeOptionalTerrain: true);
    Id65AuthoringEditorDocument noTerrainDocument =
    Id65AuthoringEditorDocument.CreateExact(includeOptionalTerrain: false);
Id65AuthoringEditorDocument residentT25Document =
    Id65AuthoringEditorDocument.CreateExact(
        includeOptionalTerrain: true,
        mandatoryBaseFaceTextureId: 25);
Id65AuthoringEditorDocumentReadModel residentT25Raw =
    Id65AuthoringEditorDocumentWorkspaceInspector.InspectCanonicalDocument(
        residentT25Document.SerializeUtf8());
Require(
    residentT25Raw.SchemaVersion == Id65AuthoringEditorDocument.ResidentT25SchemaVersion &&
    residentT25Raw.ProfileId == Id65AuthoringEditorDocument.ResidentT25DocumentProfileId &&
    residentT25Raw.MandatoryBaseFaceTextureId == 25 &&
    residentT25Raw.DocumentCanonicalSha256 == residentT25Document.CanonicalSha256 &&
    residentT25Raw.DocumentContentSha256 == residentT25Document.ContentSha256 &&
    !residentT25Raw.EnvelopeCanonicalIdentityVerified &&
    !residentT25Raw.DeterministicEnvelopeReadbackVerified &&
    !residentT25Raw.PersistedProjectionIdentityPresent &&
    residentT25Raw.EnvelopeCanonicalSha256.Length == 0,
    "Raw canonical T25 inspection did not remain exact and explicitly non-persisted.");
    byte[] fullEnvelope = BuildCanonicalEnvelope(manifest, fullDocument);
    byte[] noTerrainEnvelope = BuildCanonicalEnvelope(manifest, noTerrainDocument);
    await File.WriteAllBytesAsync(documentPath, fullEnvelope);

    IReadOnlyList<TreeEntry> beforeValid = SnapshotTree(paths.RootPath);
    Id65AuthoringEditorDocumentReadModel inspected = RequirePresent(
        await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(paths, manifest));
    Id65AuthoringEditorDocumentReadModel repeated = RequirePresent(
        await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(paths, manifest));
    Require(
        inspected.EnvelopeCanonicalIdentityVerified &&
        inspected.DeterministicEnvelopeReadbackVerified &&
        inspected.PersistedProjectionIdentityPresent,
        "Persisted T66 inspection lost its verified deterministic envelope identity.");
    Require(
        SnapshotTree(paths.RootPath).SequenceEqual(beforeValid),
        "Valid inspection mutated the lab workspace.");
    AssertExactSemantics(inspected, fullDocument);
    AssertClosedCapabilities(inspected);
    Require(
        inspected.ReadModelCanonicalSha256 == repeated.ReadModelCanonicalSha256 &&
        inspected.EnvelopeCanonicalSha256 == repeated.EnvelopeCanonicalSha256 &&
        inspected.DocumentCanonicalSha256 == repeated.DocumentCanonicalSha256,
        "Repeated inspection was not deterministic.");
    Require(
        inspected.ReadModelCanonicalSha256 == ComputeExpectedReadModelSha256(inspected),
        "The read-model identity does not commit its complete public semantic projection.");
    AssertPublicApiBoundary(inspected);

    using (CancellationTokenSource canceled = new())
    {
        canceled.Cancel();
        IReadOnlyList<TreeEntry> beforeCanceled = SnapshotTree(paths.RootPath);
        await ExpectCanceledAsync(
            async () => _ = await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                paths,
                manifest,
                canceled.Token));
        Require(
            SnapshotTree(paths.RootPath).SequenceEqual(beforeCanceled),
            "Pre-cancelled inspection mutated the lab workspace.");
    }

    byte[] unknownField = RewriteEnvelope(fullEnvelope, root => root["unknownField"] = true);
    await RejectAndRestoreAsync(
        "unknown envelope field",
        unknownField,
        fullEnvelope,
        documentPath,
        paths,
        manifest,
        "unknown field");
    await RejectAndRestoreAsync(
        "oversized envelope",
        new byte[Id65AuthoringEditorDocumentStore.MaximumEnvelopeByteCount + 1],
        fullEnvelope,
        documentPath,
        paths,
        manifest,
        "exceeds the 262144-byte persistence limit");

    UnusedLevel65BlankLevelLabManifest staleManifest = manifest with
    {
        ProfileVersion = manifest.ProfileVersion + 1
    };
    await ExpectRejectedAsync(
        "stale expected manifest",
        async () => _ = await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
            paths,
            staleManifest));

    string outsideDocument = Path.Combine(smokeRoot, "outside-document.json");
    File.Move(documentPath, outsideDocument);
    File.CreateSymbolicLink(documentPath, outsideDocument);
    try
    {
        await ExpectRejectedAsync(
            "document symbolic link",
            async () => _ = await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                paths,
                manifest));
    }
    finally
    {
        File.Delete(documentPath);
        File.Move(outsideDocument, documentPath);
    }
    Require(
        (await File.ReadAllBytesAsync(documentPath)).SequenceEqual(fullEnvelope),
        "Symlink refusal changed the canonical envelope.");

    await ExpectRejectedAsync(
        "valid-envelope readback race",
        async () => _ = await Id65AuthoringEditorDocumentStore
            .InspectEnvelopeIdentityForSmokeAsync(
                paths,
                manifest,
                async () =>
                {
                    string replacement = Path.Combine(
                        paths.AuthoredEditsDirectoryPath,
                        $".{Guid.NewGuid():N}.replacement");
                    await File.WriteAllBytesAsync(replacement, noTerrainEnvelope);
                    File.Move(replacement, documentPath, overwrite: true);
                }),
        "changed during deterministic inspection readback");
    await File.WriteAllBytesAsync(documentPath, fullEnvelope);

    await File.WriteAllBytesAsync(documentPath, noTerrainEnvelope);
    Id65AuthoringEditorDocumentReadModel inspectedNoTerrain = RequirePresent(
        await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(paths, manifest));
    AssertExactSemantics(inspectedNoTerrain, noTerrainDocument);
    Require(
        inspectedNoTerrain.Terrain.Count == 0 &&
        inspectedNoTerrain.ReadModelCanonicalSha256 != inspected.ReadModelCanonicalSha256 &&
        inspectedNoTerrain.ReadModelCanonicalSha256 ==
            ComputeExpectedReadModelSha256(inspectedNoTerrain),
        "A distinct persisted semantic document did not produce a distinct exact read model.");

    HashSet<string> remoteReadModelHashes = new(StringComparer.Ordinal);
    foreach (Id65RemotePadGrassT107Position position in
             Id65RemotePadGrassT107Catalog.Positions)
    {
        Id65AuthoringEditorDocument remoteDocument =
            Id65AuthoringEditorDocument.CreateRemotePadGridExact(
                position.PositionId,
                includeOptionalTerrain: true);
        byte[] remoteEnvelope = BuildCanonicalEnvelope(manifest, remoteDocument);
        await File.WriteAllBytesAsync(documentPath, remoteEnvelope);
        Id65AuthoringEditorDocumentReadModel remote = RequirePresent(
            await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                paths,
                manifest));
        AssertExactSemantics(remote, remoteDocument);
        AssertClosedCapabilities(remote);
        AssertRemotePadPosition(remote, position);
        Id65AuthoringEditorControlState recoveredRemote =
            Id65AuthoringEditorControlStateCompiler.RecoverControlState(remote);
        Require(
            remote.ReadModelSchemaVersion ==
                Id65AuthoringEditorDocumentReadModel.RemotePadGridReadModelSchemaVersion &&
            remote.EnvelopeSchemaVersion ==
                Id65AuthoringEditorDocumentStore.RemotePadGridEnvelopeSchemaVersion &&
            remote.ProjectionContractId ==
                Id65AuthoringEditorDocumentStore.RemotePadGridProjectionContractId &&
            remote.StaticAuthoringProfileId ==
                Id65AuthoringEditorDocumentStore.RemotePadGridStaticAuthoringProfileId &&
            remote.StaticAuthoringCatalogSha256 ==
                Id65RemotePadGrassT107Catalog.CanonicalSha256 &&
            remote.StaticAuthoringSceneGraphSha256 ==
                Id65FreshLevelSceneProjector.ExpectedStructuralGraphCanonicalSha256 &&
            remote.StaticAuthoringProjectionSha256.Length == 64 &&
            remote.HasStaticAuthoringProjection &&
            !remote.HasCompiledCompositeProjection &&
            remote.CompositeManifestSha256.Length == 0 &&
            remote.CompositeOutputRow80Sha256.Length == 0 &&
            remote.CompositeDeterministicPlanSha256.Length == 0 &&
            recoveredRemote.UsesRemotePadT107Grid &&
            recoveredRemote.T107RemotePadPositionId == position.PositionId &&
            recoveredRemote.IncludeGrassAtT107 &&
            remote.ReadModelCanonicalSha256 == ComputeExpectedReadModelSha256(remote) &&
            remoteReadModelHashes.Add(remote.ReadModelCanonicalSha256),
            $"Remote position {position.PositionId} did not inspect as one exact schema-3 static-authoring read model.");
    }
    Require(
        remoteReadModelHashes.Count == Id65RemotePadGrassT107Catalog.Positions.Count,
        "The nine frozen remote positions did not produce nine distinct read-model identities.");

    Id65RemotePadGrassT107Position retainedPosition =
        Id65RemotePadGrassT107Catalog.GetRequiredPosition(
            Id65RemotePadGrassT107Catalog.SouthEastPositionId);
    Id65AuthoringEditorDocument remoteOffDocument =
        Id65AuthoringEditorDocument.CreateRemotePadGridExact(
            retainedPosition.PositionId,
            includeOptionalTerrain: false,
            placementIds: []);
    await File.WriteAllBytesAsync(
        documentPath,
        BuildCanonicalEnvelope(manifest, remoteOffDocument));
    Id65AuthoringEditorDocumentReadModel remoteOff = RequirePresent(
        await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(paths, manifest));
    AssertRemotePadPosition(remoteOff, retainedPosition);
    Require(
        remoteOff.MobyPlacements.Count == 0 &&
        !Id65AuthoringEditorControlStateCompiler
            .RecoverControlState(remoteOff)
            .IncludeGrassAtT107,
        "A dormant remote T107 choice did not retain its exact selected position while off.");

    Id65AuthoringEditorDocument remoteOnDocument =
        Id65AuthoringEditorDocument.CreateRemotePadGridExact(
            retainedPosition.PositionId,
            includeOptionalTerrain: false,
            placementIds: [Id65RemotePadGrassT107Catalog.PlacementId]);
    byte[] remoteOnEnvelope = BuildCanonicalEnvelope(manifest, remoteOnDocument);
    await File.WriteAllBytesAsync(documentPath, remoteOnEnvelope);
    Id65AuthoringEditorDocumentReadModel remoteOn = RequirePresent(
        await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(paths, manifest));
    AssertRemotePadPosition(remoteOn, retainedPosition);
    Require(
        remoteOn.MobyPlacements.Count == 1 &&
        Id65AuthoringEditorControlStateCompiler
            .RecoverControlState(remoteOn)
            .IncludeGrassAtT107,
        "Re-enabling remote T107 did not reuse its exact retained position.");

    string parkedLockedBaseImagePath = Path.Combine(smokeRoot, "parked-locked-base.bin");
    string parkedLockedBaseCuePath = Path.Combine(smokeRoot, "parked-locked-base.cue");
    bool lockedBaseImageParked = false;
    bool lockedBaseCueParked = false;
    try
    {
        File.Move(paths.LockedBaseImagePath, parkedLockedBaseImagePath);
        lockedBaseImageParked = true;
        File.Move(paths.LockedBaseCuePath, parkedLockedBaseCuePath);
        lockedBaseCueParked = true;

        Id65AuthoringEditorDocumentReadModel staticOnlyRemote = RequirePresent(
            await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(paths, manifest));
        AssertExactSemantics(staticOnlyRemote, remoteOnDocument);
        AssertRemotePadPosition(staticOnlyRemote, retainedPosition);
        AssertClosedCapabilities(staticOnlyRemote);

        await File.WriteAllBytesAsync(documentPath, fullEnvelope);
        await ExpectRejectedAsync(
            "legacy inspection without locked BIN/CUE",
            async () => _ = await Id65AuthoringEditorDocumentWorkspaceInspector
                .InspectAsync(paths, manifest),
            "missing its manifest or locked BIN/CUE");
        await File.WriteAllBytesAsync(documentPath, remoteOnEnvelope);
    }
    finally
    {
        if (lockedBaseCueParked && File.Exists(parkedLockedBaseCuePath))
            File.Move(parkedLockedBaseCuePath, paths.LockedBaseCuePath);
        if (lockedBaseImageParked && File.Exists(parkedLockedBaseImagePath))
            File.Move(parkedLockedBaseImagePath, paths.LockedBaseImagePath);
    }
    Require(
        !File.Exists(parkedLockedBaseImagePath) &&
        !File.Exists(parkedLockedBaseCuePath),
        "The static-only inspector smoke retained parked locked-base inputs.");

    await RejectAndRestoreAsync(
        "forged remote static projection",
        RewriteEnvelope(remoteOnEnvelope, root =>
            root["staticAuthoringProjectionSha256"] = new string('0', 64)),
        remoteOnEnvelope,
        documentPath,
        paths,
        manifest,
        "forged or is stale");
    await RejectAndRestoreAsync(
        "legacy schema with remote properties",
        RewriteEnvelope(remoteOnEnvelope, root => root["schemaVersion"] = 1),
        remoteOnEnvelope,
        documentPath,
        paths,
        manifest,
        "unknown field");

    Require(
        SnapshotFixture(lockedFixturePath) == fixtureBefore,
        "Inspection changed the locked-base fixture.");
    Console.WriteLine(
        "PASS Id65AuthoringEditorDocumentWorkspaceInspectorSmoke: legacy schema-1 envelope/read-model behavior plus schema-2 remote static-authoring envelopes, all nine frozen T107 positions, dormant off/on position retention, native-input-free remote inspection with legacy fail-closed contrast, strict forged/malformed refusal, deterministic path-free semantics, and no compiled-afterimage/App/Create-BIN/runtime/release authority passed. Current native reprojection and source freshness remain explicitly false.");
}
finally
{
    if (Directory.Exists(smokeRoot) &&
        Path.GetFileName(smokeRoot).StartsWith(SmokeRootPrefix, StringComparison.Ordinal))
    {
        Directory.Delete(smokeRoot, recursive: true);
    }
    Require(!Directory.Exists(smokeRoot), "The inspector smoke retained its owned workspace.");
}

static async Task CreateReadOnlyFixtureWorkspaceAsync(
    UnusedLevel65BlankLevelLabWorkspacePaths paths,
    UnusedLevel65BlankLevelLabManifest manifest,
    string lockedFixturePath)
{
    Directory.CreateDirectory(paths.LockedBaseDirectoryPath);
    File.CreateSymbolicLink(paths.LockedBaseImagePath, lockedFixturePath);
    string cue =
        $"FILE \"{Path.GetFileName(paths.LockedBaseImagePath)}\" BINARY\n" +
        "  TRACK 01 MODE2/2352\n" +
        "    INDEX 01 00:00:00\n";
    await File.WriteAllTextAsync(
        paths.LockedBaseCuePath,
        cue,
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    options.Converters.Add(new JsonStringEnumConverter());
    string manifestText = JsonSerializer.Serialize(manifest, options) + "\n";
    await File.WriteAllTextAsync(
        paths.ManifestPath,
        manifestText,
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}

static byte[] BuildCanonicalEnvelope(
    UnusedLevel65BlankLevelLabManifest manifest,
    Id65AuthoringEditorDocument document)
{
    if (document.UsesRemotePadT107Grid)
        return BuildRemoteCanonicalEnvelope(manifest, document);

    string compositeManifestSha256 = new('a', 64);
    string compositeOutputRow80Sha256 = new('b', 64);
    string compositeDeterministicPlanSha256 = new('c', 64);
    byte[] content = WriteEnvelope(
        manifest,
        document,
        compositeManifestSha256,
        compositeOutputRow80Sha256,
        compositeDeterministicPlanSha256,
        envelopeContentSha256: null);
    return WriteEnvelope(
        manifest,
        document,
        compositeManifestSha256,
        compositeOutputRow80Sha256,
        compositeDeterministicPlanSha256,
        Hash(content));
}

static byte[] BuildRemoteCanonicalEnvelope(
    UnusedLevel65BlankLevelLabManifest manifest,
    Id65AuthoringEditorDocument document)
{
    (string sceneGraphSha256, string projectionSha256) =
        CaptureRemoteStaticIdentity(document);
    byte[] content = WriteRemoteEnvelope(
        manifest,
        document,
        sceneGraphSha256,
        projectionSha256,
        envelopeContentSha256: null);
    return WriteRemoteEnvelope(
        manifest,
        document,
        sceneGraphSha256,
        projectionSha256,
        Hash(content));
}

static byte[] WriteRemoteEnvelope(
    UnusedLevel65BlankLevelLabManifest manifest,
    Id65AuthoringEditorDocument document,
    string sceneGraphSha256,
    string projectionSha256,
    string? envelopeContentSha256)
{
    using MemoryStream buffer = new();
    using (Utf8JsonWriter json = new(buffer, new JsonWriterOptions
           {
               Indented = false,
               SkipValidation = false
           }))
    {
        json.WriteStartObject();
        json.WriteString("format", Id65AuthoringEditorDocumentStore.EnvelopeFormat);
        json.WriteNumber(
            "schemaVersion",
            Id65AuthoringEditorDocumentStore.RemotePadGridEnvelopeSchemaVersion);
        json.WriteString("workspaceKey", manifest.WorkspaceKey);
        json.WriteNumber("workspaceLayoutVersion", manifest.WorkspaceLayoutVersion);
        json.WriteString("workspaceLayoutRelativeRoot", manifest.WorkspaceLayoutRelativeRoot);
        json.WriteString("labProfileId", manifest.ProfileId);
        json.WriteNumber("labProfileVersion", manifest.ProfileVersion);
        json.WriteString("lockedBaseProfileId", manifest.LockedBaseProfileId);
        json.WriteString("lockedBaseImageSha256", manifest.LockedBaseImageSha256);
        json.WriteString("documentProfileId", document.ProfileId);
        json.WriteString("documentCanonicalSha256", document.CanonicalSha256);
        json.WriteString(
            "projectionContractId",
            Id65AuthoringEditorDocumentStore.RemotePadGridProjectionContractId);
        json.WriteString(
            "staticAuthoringProfileId",
            Id65AuthoringEditorDocumentStore.RemotePadGridStaticAuthoringProfileId);
        json.WriteString(
            "staticAuthoringCatalogSha256",
            Id65RemotePadGrassT107Catalog.CanonicalSha256);
        json.WriteString("staticAuthoringSceneGraphSha256", sceneGraphSha256);
        json.WriteString("staticAuthoringProjectionSha256", projectionSha256);
        if (envelopeContentSha256 != null)
            json.WriteString("envelopeContentSha256", envelopeContentSha256);
        json.WritePropertyName("document");
        using (JsonDocument parsed = JsonDocument.Parse(document.SerializeUtf8()))
            parsed.RootElement.WriteTo(json);
        json.WriteEndObject();
    }
    return buffer.ToArray();
}

static (string SceneGraphSha256, string ProjectionSha256)
    CaptureRemoteStaticIdentity(Id65AuthoringEditorDocument document)
{
    string positionId = document.T107RemotePadPositionId
        ?? throw new InvalidDataException("The remote inspector fixture lost its position.");
    Id65RemotePadGrassT107Position position =
        Id65RemotePadGrassT107Catalog.GetRequiredPosition(positionId);
    string sceneGraphSha256 = document.MandatoryBaseFaceTextureId switch
    {
        66 => document.Terrain.Count == 1
            ? Id65FreshLevelSceneProjector.ExpectedStructuralGraphCanonicalSha256
            : Id65FreshLevelSceneProjector.ExpectedCanonicalSha256,
        25 => document.Terrain.Count == 1
            ? Id65FreshLevelSceneProjector.ExpectedResidentT25StructuralGraphCanonicalSha256
            : Id65FreshLevelSceneProjector.ExpectedResidentT25BaseGraphCanonicalSha256,
        _ => throw new InvalidDataException("The remote inspector fixture lost T25/T66 support.")
    };

    using MemoryStream buffer = new();
    using (Utf8JsonWriter json = new(buffer))
    {
        json.WriteStartObject();
        json.WriteString(
            "profileId",
            Id65AuthoringEditorDocumentStore.RemotePadGridStaticAuthoringProfileId);
        json.WriteNumber("schemaVersion", 1);
        json.WriteString(
            "projectionContractId",
            Id65AuthoringEditorDocumentStore.RemotePadGridProjectionContractId);
        json.WriteString("documentProfileId", document.ProfileId);
        json.WriteString("documentCanonicalSha256", document.CanonicalSha256);
        json.WriteString("documentContentSha256", document.ContentSha256);
        json.WriteString("catalogProfileId", Id65RemotePadGrassT107Catalog.ProfileId);
        json.WriteString(
            "catalogCanonicalSha256",
            Id65RemotePadGrassT107Catalog.CanonicalSha256);
        json.WriteString("positionId", position.PositionId);
        json.WriteString("positionRowSha256", position.RowSha256);
        json.WriteString("sceneProfileId", Id65FreshLevelSceneProjector.ProfileId);
        json.WriteNumber("sceneSchemaVersion", Id65FreshLevelSceneProjector.SchemaVersion);
        json.WriteString("sceneGraphCanonicalSha256", sceneGraphSha256);
        json.WriteString("supportHandle", Id65RemotePadGrassT107Catalog.SupportHandle);
        json.WriteNumber(
            "supportCollisionTrueIndex",
            Id65RemotePadGrassT107Catalog.SupportCollisionTrueIndex);
        json.WriteBoolean("structuralTerrainPresent", document.Terrain.Count == 1);
        json.WriteNumber(
            "mandatoryBaseFaceTextureId",
            document.MandatoryBaseFaceTextureId);
        json.WriteBoolean(
            "grassAtT88Present",
            document.MobyPlacements.Any(item => item.TargetTrueIndex == 88));
        json.WriteBoolean(
            "grassAtT107Present",
            document.MobyPlacements.Any(item => item.TargetTrueIndex == 107));
        json.WriteBoolean("compiledAfterimagePresent", false);
        json.WriteBoolean("runtimeCandidateAuthorized", false);
        json.WriteBoolean("normalCreateBinEnabled", false);
        json.WriteBoolean("releaseAuthorized", false);
        json.WriteEndObject();
    }
    return (sceneGraphSha256, Hash(buffer.ToArray()));
}

static byte[] WriteEnvelope(
    UnusedLevel65BlankLevelLabManifest manifest,
    Id65AuthoringEditorDocument document,
    string compositeManifestSha256,
    string compositeOutputRow80Sha256,
    string compositeDeterministicPlanSha256,
    string? envelopeContentSha256)
{
    using MemoryStream buffer = new();
    using (Utf8JsonWriter json = new(buffer, new JsonWriterOptions
           {
               Indented = false,
               SkipValidation = false
           }))
    {
        json.WriteStartObject();
        json.WriteString("format", Id65AuthoringEditorDocumentStore.EnvelopeFormat);
        json.WriteNumber("schemaVersion", Id65AuthoringEditorDocumentStore.EnvelopeSchemaVersion);
        json.WriteString("workspaceKey", manifest.WorkspaceKey);
        json.WriteNumber("workspaceLayoutVersion", manifest.WorkspaceLayoutVersion);
        json.WriteString("workspaceLayoutRelativeRoot", manifest.WorkspaceLayoutRelativeRoot);
        json.WriteString("labProfileId", manifest.ProfileId);
        json.WriteNumber("labProfileVersion", manifest.ProfileVersion);
        json.WriteString("lockedBaseProfileId", manifest.LockedBaseProfileId);
        json.WriteString("lockedBaseImageSha256", manifest.LockedBaseImageSha256);
        json.WriteString("documentProfileId", document.ProfileId);
        json.WriteString("documentCanonicalSha256", document.CanonicalSha256);
        json.WriteString(
            "projectionContractId",
            Id65AuthoringEditorDocumentStore.ProjectionContractId);
        json.WriteString("compositeProfileId", Id65AuthoringCompositeCompiler.ProfileId);
        json.WriteString("compositeManifestSha256", compositeManifestSha256);
        json.WriteString("compositeOutputRow80Sha256", compositeOutputRow80Sha256);
        json.WriteString(
            "compositeDeterministicPlanSha256",
            compositeDeterministicPlanSha256);
        if (envelopeContentSha256 != null)
            json.WriteString("envelopeContentSha256", envelopeContentSha256);
        json.WritePropertyName("document");
        using (JsonDocument parsed = JsonDocument.Parse(document.SerializeUtf8()))
            parsed.RootElement.WriteTo(json);
        json.WriteEndObject();
    }
    return buffer.ToArray();
}

static void AssertExactSemantics(
    Id65AuthoringEditorDocumentReadModel actual,
    Id65AuthoringEditorDocument expected)
{
    Require(
        actual.SchemaVersion == expected.SchemaVersion &&
        actual.ProfileId == expected.ProfileId &&
        actual.LabProfileId == expected.LabProfileId &&
        actual.LabProfileVersion == expected.LabProfileVersion &&
        actual.WorkspaceKey == expected.WorkspaceKey &&
        actual.WorkspaceLayoutVersion == expected.WorkspaceLayoutVersion &&
        actual.LockedBaseProfileId == expected.LockedBaseProfileId &&
        actual.LockedBaseImageSha256 == expected.LockedBaseImageSha256 &&
        actual.LockedRow80Sha256 == expected.LockedRow80Sha256 &&
        actual.CompositeProfileId == expected.CompositeProfileId &&
        actual.PrivateTextureWitnessSha256 == expected.PrivateTextureWitnessSha256 &&
        actual.MobyBundleSha256 == expected.MobyBundleSha256 &&
        actual.DocumentContentSha256 == expected.ContentSha256 &&
        actual.DocumentCanonicalSha256 == expected.CanonicalSha256 &&
        actual.UsesRemotePadT107Grid == expected.UsesRemotePadT107Grid &&
        actual.T107RemotePadPositionId == expected.T107RemotePadPositionId &&
        actual.T107PlacementSemantics == (expected.UsesRemotePadT107Grid
            ? Id65AuthoringEditorT107PlacementSemantics.RemotePadGridV1
            : Id65AuthoringEditorT107PlacementSemantics.LegacyNativeApronV1),
        "The inspector changed a document identity field.");
    Require(actual.Terrain.Count == expected.Terrain.Count, "Terrain count changed.");
    for (int index = 0; index < actual.Terrain.Count; index++)
    {
        Id65AuthoringEditorTerrainReadModel left = actual.Terrain[index];
        Id65AuthoringEditorTerrainIntent right = expected.Terrain[index];
        Require(
            left.Id == right.Id && left.SectorId == right.SectorId &&
            left.SectorIndex == right.SectorIndex &&
            left.AddedVertex.Handle == right.AddedVertex.Handle &&
            left.AddedVertex.X == right.AddedVertex.X &&
            left.AddedVertex.Y == right.AddedVertex.Y &&
            left.AddedVertex.Z == right.AddedVertex.Z &&
            left.HighPolyHandles.SequenceEqual(right.HighPolyHandles) &&
            left.LowPolyHandles.SequenceEqual(right.LowPolyHandles) &&
            left.TextureIntentId == right.TextureIntentId &&
            left.Collision.Id == right.Collision.Id &&
            left.Collision.VertexHandles.SequenceEqual(right.Collision.VertexHandles) &&
            left.Collision.Assignment == right.Collision.Assignment &&
            left.Collision.TargetCell.X == right.Collision.TargetCell.X &&
            left.Collision.TargetCell.Y == right.Collision.TargetCell.Y &&
            left.Collision.TargetCell.Z == right.Collision.TargetCell.Z &&
            left.Occlusion.Id == right.Occlusion.Id &&
            left.Occlusion.SectorId == right.Occlusion.SectorId &&
            left.Occlusion.GroupIndex == right.Occlusion.GroupIndex,
            "The inspector changed terrain HP/LP/collision/occlusion semantics.");
    }
    Id65AuthoringEditorPrivateTextureSwap texture = expected.PrivateTextureSwap;
    Require(
        actual.PrivateTextureSwap.Id == texture.Id &&
        actual.PrivateTextureSwap.TargetRenderFaceId == texture.TargetRenderFaceId &&
        actual.PrivateTextureSwap.TextureIntentId == texture.TextureIntentId &&
        actual.PrivateTextureSwap.DestinationRecordIndex == texture.DestinationRecordIndex &&
        actual.PrivateTextureSwap.SourceHandle == texture.SourceHandle &&
        actual.PrivateTextureSwap.DonorWadEntry == texture.DonorWadEntry &&
        actual.PrivateTextureSwap.DonorTextureId == texture.DonorTextureId &&
        actual.PrivateTextureSwap.MaterialTemplateTextureId ==
            texture.MaterialTemplateTextureId,
        "The inspector changed private-texture semantics.");
    Require(
        actual.MobyPlacements.Count == expected.MobyPlacements.Count,
        "Moby placement count changed.");
    for (int index = 0; index < actual.MobyPlacements.Count; index++)
    {
        Id65AuthoringEditorMobyPlacementReadModel left = actual.MobyPlacements[index];
        Id65AuthoringEditorMobyPlacement right = expected.MobyPlacements[index];
        Require(
            left.Id == right.Id && left.AtomicGroupId == right.AtomicGroupId &&
            left.BundleId == right.BundleId && left.RowTemplateId == right.RowTemplateId &&
            left.RowPolicy == right.RowPolicy &&
            left.TargetTrueIndex == right.TargetTrueIndex &&
            left.CoordinatePolicy == right.CoordinatePolicy &&
            left.YawByte == right.YawByte && left.SupportHandle == right.SupportHandle &&
            CoordinatesEqual(left.CoordinatesOrDelta, right.CoordinatesOrDelta),
            "The inspector changed Moby placement semantics.");
    }
    Require(
        actual.BaselineHandles.Spawn == expected.BaselineHandles.Spawn &&
        actual.BaselineHandles.Music == expected.BaselineHandles.Music &&
        actual.BaselineHandles.Totals == expected.BaselineHandles.Totals &&
        actual.BaselineHandles.Exit == expected.BaselineHandles.Exit &&
        actual.BaselineHandles.Save == expected.BaselineHandles.Save,
        "The inspector changed baseline ownership handles.");
}

static void AssertRemotePadPosition(
    Id65AuthoringEditorDocumentReadModel actual,
    Id65RemotePadGrassT107Position expected)
{
    Id65RemotePadGrassT107PositionReadModel value = actual.T107RemotePadPosition
        ?? throw new InvalidDataException("The schema-3 read model omitted its remote position.");
    Require(
        actual.UsesRemotePadT107Grid &&
        !actual.RequiresExplicitRemotePadMigration &&
        actual.T107RemotePadPositionId == expected.PositionId &&
        value.Ordinal == expected.Ordinal &&
        value.PositionId == expected.PositionId &&
        value.Label == expected.Label &&
        value.EditorX == expected.EditorX && value.EditorY == expected.EditorY &&
        value.EditorZ == expected.EditorZ && value.RawX == expected.RawX &&
        value.RawY == expected.RawY && value.RawZ == expected.RawZ &&
        value.RowSha256 == expected.RowSha256 &&
        value.IsDefault == expected.IsDefault,
        $"The schema-3 read model changed frozen position {expected.PositionId}.");
}

static bool CoordinatesEqual(
    Id65AuthoringEditorCoordinateReadModel? left,
    Id65AuthoringEditorCoordinate? right) =>
    left == null && right == null ||
    left != null && right is Id65AuthoringEditorCoordinate coordinate &&
    left.X == coordinate.X && left.Y == coordinate.Y && left.Z == coordinate.Z;

static void AssertClosedCapabilities(Id65AuthoringEditorDocumentReadModel value)
{
    Require(
        value.EnvelopeCanonicalIdentityVerified &&
        value.DocumentCanonicalIdentityVerified &&
        value.PersistedProjectionIdentityPresent &&
        value.DeterministicEnvelopeReadbackVerified &&
        value.HasCompiledCompositeProjection == !value.UsesRemotePadT107Grid &&
        value.HasStaticAuthoringProjection == value.UsesRemotePadT107Grid,
        "The inspector omitted a completed canonical-read verification.");
    Require(
        !value.CurrentProjectionRevalidated &&
        !value.CurrentLockedSourceReprojected &&
        !value.CurrentOnDiskFreshnessVerified &&
        !value.ContainsGameBytes &&
        !value.ContainsNativePayloadBytes &&
        !value.ContainsPhysicalAddresses &&
        !value.ContainsFilePaths &&
        !value.ContainsCompiledAfterimage &&
        !value.ContainsPublisherState &&
        !value.PersistenceEnabled &&
        !value.FileSystemMutationPerformed &&
        !value.WritesFileSystem && !value.WritesDiscImage && !value.WritesCue &&
        !value.CarriesAppOrUiAuthority &&
        !value.CreateBinEnabled && !value.NormalCreateBinEnabled &&
        !value.RuntimeCandidateAuthorized && !value.RuntimeProven &&
        !value.ReleaseIntegrated && !value.PromotionAuthorized && !value.Publishable &&
        value.RequiresTrustedExclusiveWorkspaceDirectory &&
        !value.HostileFilesystemRaceSafe && !value.CommonWorkspaceLeaseOwned,
        "The inspect-only result overclaims native, App/UI, Create-BIN, runtime, or release authority.");
}

static void AssertPublicApiBoundary(Id65AuthoringEditorDocumentReadModel sample)
{
    FieldInfo filename = typeof(Id65AuthoringEditorDocumentWorkspaceInspector).GetField(
        nameof(Id65AuthoringEditorDocumentWorkspaceInspector.DocumentFileName),
        BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidDataException("The inspector does not expose DocumentFileName.");
    Require(
        filename.IsLiteral && !filename.IsInitOnly && filename.FieldType == typeof(string),
        "DocumentFileName is not one public compile-time string constant.");
    MethodInfo[] inspectorMethods = typeof(Id65AuthoringEditorDocumentWorkspaceInspector)
        .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
    Require(
        inspectorMethods.Length == 1 && inspectorMethods[0].Name == "InspectAsync" &&
        inspectorMethods[0].GetParameters().Length == 3,
        "The public inspector exposes an unexpected mutation/test API.");

    Type[] publicDtoTypes =
    [
        typeof(Id65AuthoringEditorDocumentReadModel),
        typeof(Id65AuthoringEditorPointReadModel),
        typeof(Id65AuthoringEditorCollisionCellReadModel),
        typeof(Id65AuthoringEditorCollisionReadModel),
        typeof(Id65AuthoringEditorOcclusionReadModel),
        typeof(Id65AuthoringEditorTerrainReadModel),
        typeof(Id65AuthoringEditorPrivateTextureSwapReadModel),
        typeof(Id65RemotePadGrassT107PositionReadModel),
        typeof(Id65AuthoringEditorCoordinateReadModel),
        typeof(Id65AuthoringEditorMobyPlacementReadModel),
        typeof(Id65AuthoringEditorBaselineHandlesReadModel)
    ];
    foreach (Type type in publicDtoTypes)
    {
        Require(
            type.IsPublic && type.IsSealed &&
            type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length == 0,
            $"Public DTO {type.Name} is caller-constructible or mutable by derivation.");
        foreach (PropertyInfo property in type.GetProperties(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            Require(
                property.CanRead && !property.CanWrite && property.GetIndexParameters().Length == 0,
                $"Public DTO property {type.Name}.{property.Name} is not get-only.");
            RequireSafePublicType(property.PropertyType, $"{type.Name}.{property.Name}");
            Require(
                !property.Name.Contains("Json", StringComparison.OrdinalIgnoreCase) &&
                (!property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
                 property.Name == nameof(Id65AuthoringEditorDocumentReadModel.ContainsFilePaths)),
                $"Public DTO property {type.Name}.{property.Name} leaks serialized/path state.");
        }
    }

    AssertReadOnlyCollection(sample.Terrain, "Terrain");
    AssertReadOnlyCollection(sample.MobyPlacements, "MobyPlacements");
    if (sample.Terrain.Count > 0)
    {
        AssertReadOnlyCollection(sample.Terrain[0].HighPolyHandles, "HighPolyHandles");
        AssertReadOnlyCollection(sample.Terrain[0].LowPolyHandles, "LowPolyHandles");
        AssertReadOnlyCollection(
            sample.Terrain[0].Collision.VertexHandles,
            "Collision.VertexHandles");
    }
}

static void RequireSafePublicType(Type type, string role)
{
    Require(
        !type.IsArray && type != typeof(byte[]) &&
        !typeof(Stream).IsAssignableFrom(type) &&
        (!type.IsGenericType ||
         type.GetGenericTypeDefinition() != typeof(Memory<>) &&
         type.GetGenericTypeDefinition() != typeof(ReadOnlyMemory<>)),
        $"Public DTO property {role} exposes byte/buffer/stream state.");
    foreach (Type argument in type.IsGenericType ? type.GetGenericArguments() : [])
        RequireSafePublicType(argument, role);
}

static void AssertReadOnlyCollection<T>(IReadOnlyList<T> values, string role)
{
    Require(
        values is ICollection<T> collection && collection.IsReadOnly,
        $"{role} is not backed by one read-only collection.");
    try
    {
        ((ICollection<T>)values).Add(values[0]);
        throw new InvalidDataException($"{role} accepted external mutation.");
    }
    catch (NotSupportedException)
    {
        // Expected from ReadOnlyCollection<T>.
    }
}

static string ComputeExpectedReadModelSha256(
    Id65AuthoringEditorDocumentReadModel value)
{
    using MemoryStream buffer = new();
    using (Utf8JsonWriter json = new(buffer))
    {
        json.WriteStartObject();
        json.WriteString("format", "spyro-editor-id65-authoring-editor-document-read-model");
        json.WriteNumber("readModelSchemaVersion", value.ReadModelSchemaVersion);
        json.WriteNumber("documentSchemaVersion", value.SchemaVersion);
        json.WriteString("profileId", value.ProfileId);
        if (value.UsesRemotePadT107Grid)
        {
            json.WriteNumber("envelopeSchemaVersion", value.EnvelopeSchemaVersion);
            json.WriteString(
                "t107PlacementSemantics",
                Id65RemotePadGrassT107Catalog.ProfileId);
            WriteExpectedRemotePosition(
                json,
                value.T107RemotePadPosition ?? throw new InvalidDataException(
                    "The schema-3 read model lost its remote position."));
        }
        json.WriteNumber("mandatoryBaseFaceTextureId", value.MandatoryBaseFaceTextureId);
        json.WriteString("labProfileId", value.LabProfileId);
        json.WriteNumber("labProfileVersion", value.LabProfileVersion);
        json.WriteString("workspaceKey", value.WorkspaceKey);
        json.WriteNumber("workspaceLayoutVersion", value.WorkspaceLayoutVersion);
        json.WriteString("lockedBaseProfileId", value.LockedBaseProfileId);
        json.WriteString("lockedBaseImageSha256", value.LockedBaseImageSha256);
        json.WriteString("lockedRow80Sha256", value.LockedRow80Sha256);
        json.WriteString(
            value.UsesRemotePadT107Grid
                ? "documentCompositeProfileId"
                : "compositeProfileId",
            value.CompositeProfileId);
        json.WriteString("privateTextureWitnessSha256", value.PrivateTextureWitnessSha256);
        json.WriteString("mobyBundleSha256", value.MobyBundleSha256);
        json.WriteString("documentContentSha256", value.DocumentContentSha256);
        json.WriteString("documentCanonicalSha256", value.DocumentCanonicalSha256);
        json.WriteString("envelopeCanonicalSha256", value.EnvelopeCanonicalSha256);
        json.WriteString("projectionContractId", value.ProjectionContractId);
        if (value.UsesRemotePadT107Grid)
        {
            json.WriteString("staticAuthoringProfileId", value.StaticAuthoringProfileId);
            json.WriteString(
                "staticAuthoringCatalogSha256",
                value.StaticAuthoringCatalogSha256);
            json.WriteString(
                "staticAuthoringSceneGraphSha256",
                value.StaticAuthoringSceneGraphSha256);
            json.WriteString(
                "staticAuthoringProjectionSha256",
                value.StaticAuthoringProjectionSha256);
            json.WriteBoolean(
                "compiledCompositeProjectionPresent",
                value.HasCompiledCompositeProjection);
        }
        else
        {
            json.WriteString("compositeManifestSha256", value.CompositeManifestSha256);
            json.WriteString("compositeOutputRow80Sha256", value.CompositeOutputRow80Sha256);
            json.WriteString(
                "compositeDeterministicPlanSha256",
                value.CompositeDeterministicPlanSha256);
        }
        json.WritePropertyName("terrain");
        json.WriteStartArray();
        foreach (Id65AuthoringEditorTerrainReadModel terrain in value.Terrain)
            WriteExpectedTerrain(json, terrain);
        json.WriteEndArray();
        WriteExpectedPrivateTexture(json, value.PrivateTextureSwap);
        json.WritePropertyName("mobyPlacements");
        json.WriteStartArray();
        foreach (Id65AuthoringEditorMobyPlacementReadModel placement in value.MobyPlacements)
            WriteExpectedPlacement(json, placement);
        json.WriteEndArray();
        json.WritePropertyName("baselineHandles");
        json.WriteStartObject();
        json.WriteString("spawn", value.BaselineHandles.Spawn);
        json.WriteString("music", value.BaselineHandles.Music);
        json.WriteString("totals", value.BaselineHandles.Totals);
        json.WriteString("exit", value.BaselineHandles.Exit);
        json.WriteString("save", value.BaselineHandles.Save);
        json.WriteEndObject();
        json.WriteEndObject();
    }
    return Hash(buffer.ToArray());
}

static void WriteExpectedRemotePosition(
    Utf8JsonWriter json,
    Id65RemotePadGrassT107PositionReadModel value)
{
    json.WritePropertyName("t107RemotePadPosition");
    json.WriteStartObject();
    json.WriteNumber("catalogSchemaVersion", Id65RemotePadGrassT107Catalog.SchemaVersion);
    json.WriteString("catalogProfileId", Id65RemotePadGrassT107Catalog.ProfileId);
    json.WriteNumber("ordinal", value.Ordinal);
    json.WriteString("positionId", value.PositionId);
    json.WriteString("label", value.Label);
    json.WritePropertyName("editor");
    json.WriteStartObject();
    json.WriteNumber("x", value.EditorX);
    json.WriteNumber("y", value.EditorY);
    json.WriteNumber("z", value.EditorZ);
    json.WriteEndObject();
    json.WritePropertyName("raw");
    json.WriteStartObject();
    json.WriteNumber("x", value.RawX);
    json.WriteNumber("y", value.RawY);
    json.WriteNumber("z", value.RawZ);
    json.WriteEndObject();
    json.WriteString("rowSha256", value.RowSha256);
    json.WriteBoolean("isDefault", value.IsDefault);
    json.WriteEndObject();
}

static void WriteExpectedTerrain(Utf8JsonWriter json, Id65AuthoringEditorTerrainReadModel value)
{
    json.WriteStartObject();
    json.WriteString("id", value.Id);
    json.WriteString("sectorId", value.SectorId);
    json.WriteNumber("sectorIndex", value.SectorIndex);
    json.WritePropertyName("addedVertex");
    json.WriteStartObject();
    json.WriteString("handle", value.AddedVertex.Handle);
    json.WriteNumber("x", value.AddedVertex.X);
    json.WriteNumber("y", value.AddedVertex.Y);
    json.WriteNumber("z", value.AddedVertex.Z);
    json.WriteEndObject();
    WriteExpectedStrings(json, "highPolyHandles", value.HighPolyHandles);
    WriteExpectedStrings(json, "lowPolyHandles", value.LowPolyHandles);
    json.WriteString("textureIntentId", value.TextureIntentId);
    json.WritePropertyName("collision");
    json.WriteStartObject();
    json.WriteString("id", value.Collision.Id);
    WriteExpectedStrings(json, "vertexHandles", value.Collision.VertexHandles);
    json.WriteNumber("assignment", value.Collision.Assignment);
    json.WritePropertyName("targetCell");
    json.WriteStartObject();
    json.WriteNumber("x", value.Collision.TargetCell.X);
    json.WriteNumber("y", value.Collision.TargetCell.Y);
    json.WriteNumber("z", value.Collision.TargetCell.Z);
    json.WriteEndObject();
    json.WriteEndObject();
    json.WritePropertyName("occlusion");
    json.WriteStartObject();
    json.WriteString("id", value.Occlusion.Id);
    json.WriteString("sectorId", value.Occlusion.SectorId);
    json.WriteNumber("groupIndex", value.Occlusion.GroupIndex);
    json.WriteEndObject();
    json.WriteEndObject();
}

static void WriteExpectedPrivateTexture(
    Utf8JsonWriter json,
    Id65AuthoringEditorPrivateTextureSwapReadModel value)
{
    json.WritePropertyName("privateTextureSwap");
    json.WriteStartObject();
    json.WriteString("id", value.Id);
    json.WriteString("targetRenderFaceId", value.TargetRenderFaceId);
    json.WriteString("textureIntentId", value.TextureIntentId);
    json.WriteNumber("destinationRecordIndex", value.DestinationRecordIndex);
    json.WriteString("sourceHandle", value.SourceHandle);
    json.WriteNumber("donorWadEntry", value.DonorWadEntry);
    json.WriteNumber("donorTextureId", value.DonorTextureId);
    json.WriteNumber("materialTemplateTextureId", value.MaterialTemplateTextureId);
    json.WriteEndObject();
}

static void WriteExpectedPlacement(
    Utf8JsonWriter json,
    Id65AuthoringEditorMobyPlacementReadModel value)
{
    json.WriteStartObject();
    json.WriteString("id", value.Id);
    json.WriteString("atomicGroupId", value.AtomicGroupId);
    json.WriteString("bundleId", value.BundleId);
    json.WriteString("rowTemplateId", value.RowTemplateId);
    json.WriteString("rowPolicy", value.RowPolicy);
    json.WriteNumber("targetTrueIndex", value.TargetTrueIndex);
    json.WriteString("coordinatePolicy", value.CoordinatePolicy);
    json.WritePropertyName("coordinatesOrDelta");
    if (value.CoordinatesOrDelta == null)
    {
        json.WriteNullValue();
    }
    else
    {
        json.WriteStartObject();
        json.WriteNumber("x", value.CoordinatesOrDelta.X);
        json.WriteNumber("y", value.CoordinatesOrDelta.Y);
        json.WriteNumber("z", value.CoordinatesOrDelta.Z);
        json.WriteEndObject();
    }
    if (value.YawByte.HasValue)
        json.WriteNumber("yawByte", value.YawByte.Value);
    else
        json.WriteNull("yawByte");
    if (value.SupportHandle == null)
        json.WriteNull("supportHandle");
    else
        json.WriteString("supportHandle", value.SupportHandle);
    json.WriteEndObject();
}

static void WriteExpectedStrings(
    Utf8JsonWriter json,
    string name,
    IEnumerable<string> values)
{
    json.WritePropertyName(name);
    json.WriteStartArray();
    foreach (string value in values)
        json.WriteStringValue(value);
    json.WriteEndArray();
}

static async Task RejectAndRestoreAsync(
    string role,
    byte[] invalidBytes,
    byte[] validBytes,
    string documentPath,
    UnusedLevel65BlankLevelLabWorkspacePaths paths,
    UnusedLevel65BlankLevelLabManifest manifest,
    string requiredText)
{
    await File.WriteAllBytesAsync(documentPath, invalidBytes);
    try
    {
        await ExpectRejectedAsync(
            role,
            async () => _ = await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                paths,
                manifest),
            requiredText);
    }
    finally
    {
        await File.WriteAllBytesAsync(documentPath, validBytes);
    }
}

static async Task ExpectRejectedAsync(
    string role,
    Func<Task> action,
    string? requiredText = null)
{
    Exception? rejection = null;
    try
    {
        await action();
    }
    catch (Exception exception) when (exception is
        InvalidDataException or IOException or InvalidOperationException)
    {
        rejection = exception;
    }
    if (rejection == null)
        throw new InvalidDataException($"{role} unexpectedly succeeded.");
    if (requiredText != null)
    {
        Require(
            rejection.ToString().Contains(requiredText, StringComparison.OrdinalIgnoreCase),
            $"{role} failed for the wrong reason: {rejection.Message}");
    }
}

static async Task ExpectCanceledAsync(Func<Task> action)
{
    try
    {
        await action();
        throw new InvalidDataException("A pre-cancelled inspection unexpectedly succeeded.");
    }
    catch (OperationCanceledException)
    {
        // Expected.
    }
}

static byte[] RewriteEnvelope(byte[] bytes, Action<JsonObject> mutate)
{
    JsonObject root = JsonNode.Parse(bytes)?.AsObject()
        ?? throw new InvalidDataException("The smoke envelope could not be parsed.");
    mutate(root);
    return Encoding.UTF8.GetBytes(root.ToJsonString());
}

static IReadOnlyList<TreeEntry> SnapshotTree(string root)
{
    if (!Directory.Exists(root))
        return [];
    return Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
        .Select(path =>
        {
            string relative = Path.GetRelativePath(root, path)
                .Replace(Path.DirectorySeparatorChar, '/');
            FileInfo file = new(path);
            DirectoryInfo directory = new(path);
            string? linkTarget = file.LinkTarget ?? directory.LinkTarget;
            if (Directory.Exists(path) && linkTarget == null)
            {
                return new TreeEntry(
                    relative,
                    "directory",
                    0,
                    directory.LastWriteTimeUtc.Ticks,
                    null,
                    null);
            }
            long length = file.Length;
            string? sha256 = linkTarget == null && length <= 1024 * 1024
                ? Hash(File.ReadAllBytes(path))
                : null;
            return new TreeEntry(
                relative,
                linkTarget == null ? "file" : "link",
                length,
                file.LastWriteTimeUtc.Ticks,
                linkTarget,
                sha256);
        })
        .OrderBy(entry => entry.RelativeName, StringComparer.Ordinal)
        .ToArray();
}

static FixtureState SnapshotFixture(string path)
{
    FileInfo file = new(path);
    return new FixtureState(file.Length, file.LastWriteTimeUtc.Ticks);
}

static Id65AuthoringEditorDocumentReadModel RequirePresent(
    Id65AuthoringEditorDocumentReadModel? value) =>
    value ?? throw new InvalidDataException("The canonical persisted document inspected as absent.");

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexStringLower(SHA256.HashData(bytes));

static string FindRepositoryRoot(string? supplied)
{
    DirectoryInfo? current = new(Path.GetFullPath(supplied ?? Directory.GetCurrentDirectory()));
    while (current != null)
    {
        if (File.Exists(Path.Combine(current.FullName, "global.json")) &&
            Directory.Exists(Path.Combine(current.FullName, "src", "Spyro.Editor.Core")))
        {
            return current.FullName;
        }
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidDataException(message);
}

internal sealed record TreeEntry(
    string RelativeName,
    string Kind,
    long Length,
    long LastWriteUtcTicks,
    string? LinkTarget,
    string? Sha256);

internal sealed record FixtureState(long Length, long LastWriteUtcTicks);

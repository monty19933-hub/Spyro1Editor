using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Workspace;

const string ExpectedCleanImageSha256 =
    "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string cleanImagePath = Path.Combine(repositoryRoot, "Spyro the Dragon (USA).bin");
string cleanCuePath = Path.Combine(repositoryRoot, "Spyro the Dragon (USA).cue");
Require(File.Exists(cleanImagePath) && File.Exists(cleanCuePath),
    "The schema-5 smoke requires the exact USA BIN/CUE fixtures.");
Require(Hash(await File.ReadAllBytesAsync(cleanImagePath)) == ExpectedCleanImageSha256,
    "The schema-5 smoke source image identity changed.");

Topology[] topologies =
[
    new("zero", false, false, [],
        "979642648fd2fad60377fbe2fccd4a6eb5c0bd897be35fc524a37ba192a88164"),
    new("tile1", true, false, [Id65AuthoringTerrainSetCatalog.Tile1Id],
        "3574b051cea44c4191c37cfbf857ae9582a99b5ea142d5b08aa7dea41dc1ceac"),
    new("tile2", false, true, [Id65AuthoringTerrainSetCatalog.Tile2Id],
        "d14861c149cb4d7284cbe545014573f620edba522fb829832d24d945ee0024fd"),
    new("both", true, true,
        [Id65AuthoringTerrainSetCatalog.Tile1Id, Id65AuthoringTerrainSetCatalog.Tile2Id],
        "c6ce7aa18570486ceb75eff75cd81f4b2557cc954729c64972596e24015bc549")
];

int stateCount = 0;
HashSet<string> documentHashes = new(StringComparer.Ordinal);
Dictionary<string, string> graphHashes = new(StringComparer.Ordinal);
foreach (Topology topology in topologies)
{
    Id65AuthoringTerrainSetManifest catalogManifest =
        Id65AuthoringTerrainSetCatalog.Create(topology.ActiveUnitIds);
    Require(catalogManifest.CanonicalSha256 == topology.ManifestSha256 &&
        catalogManifest.ActiveUnits.Select(item => item.Id)
            .SequenceEqual(topology.ActiveUnitIds),
        $"The {topology.Key} terrain catalog selection changed.");

    foreach (Id65RemotePadMobyKind kind in Enum.GetValues<Id65RemotePadMobyKind>())
    {
        foreach (Id65RemotePadGrassT107Position position in
                 Id65RemotePadGrassT107Catalog.Positions)
        {
            Id65AuthoringEditorControlState state =
                Id65AuthoringEditorControlState.CreateRemotePadMobyChoiceTerrainSet(
                    topology.Tile1,
                    topology.Tile2,
                    kind,
                    position.PositionId);
            Id65AuthoringEditorControlStateCompilation compilation =
                Id65AuthoringEditorControlStateCompiler.Compile(state);
            Id65AuthoringEditorDocument document = compilation.Document;
            Id65AuthoringEditorDocument parsed =
                Id65AuthoringEditorDocument.Parse(document.SerializeUtf8());
            Id65AuthoringEditorDocumentReadModel readModel =
                Id65AuthoringEditorDocumentWorkspaceInspector.InspectCanonicalDocument(
                    parsed.SerializeUtf8());
            Id65AuthoringEditorControlState recovered =
                Id65AuthoringEditorControlStateCompiler.RecoverExactState(parsed);
            Require(document.SchemaVersion ==
                    Id65AuthoringEditorDocument.RemotePadMobyChoiceTerrainSetSchemaVersion &&
                document.ProfileId ==
                    Id65AuthoringEditorDocument.RemotePadMobyChoiceTerrainSetDocumentProfileId &&
                document.UsesRemotePadTerrainSet && document.UsesRemotePadMobyChoice &&
                !document.UsesRemotePadT107Grid &&
                document.MandatoryBaseFaceTextureId == 66 &&
                document.ActiveTerrainUnitIds.SequenceEqual(topology.ActiveUnitIds) &&
                document.TerrainSetManifestSha256 == topology.ManifestSha256 &&
                document.Terrain.Select(item => item.Id)
                    .SequenceEqual(topology.ActiveUnitIds) &&
                document.RemotePadMobyKind == kind &&
                document.RemotePadMobyPositionId == position.PositionId &&
                document.MobyPlacements.Count ==
                    (kind == Id65RemotePadMobyKind.None ? 0 : 1) &&
                parsed.SerializeUtf8().SequenceEqual(document.SerializeUtf8()) &&
                readModel.ReadModelSchemaVersion ==
                    Id65AuthoringEditorDocumentReadModel
                        .RemotePadMobyChoiceTerrainSetReadModelSchemaVersion &&
                readModel.TerrainSelectionSemantics ==
                    Id65AuthoringEditorTerrainSelectionSemantics.RemotePadTerrainSetV1 &&
                readModel.ActiveTerrainUnitIds.SequenceEqual(topology.ActiveUnitIds) &&
                readModel.TerrainSetManifestSha256 == topology.ManifestSha256 &&
                recovered.UsesRemotePadTerrainSet &&
                recovered.IncludeTerrainTile1 == topology.Tile1 &&
                recovered.IncludeTerrainTile2 == topology.Tile2 &&
                recovered.RemotePadMobyKind == kind &&
                recovered.RemotePadMobyPositionId == position.PositionId,
                $"Schema-5 roundtrip failed for {topology.Key}/{kind}/{position.PositionId}.");

            Require(documentHashes.Add(document.CanonicalSha256),
                "Distinct schema-5 states aliased one document canonical identity.");
            stateCount++;
        }
    }
}
Require(stateCount == 108 && documentHashes.Count == 108,
    "The schema-5 state identity space is incomplete or ambiguous.");

List<string> mutableIds =
    [Id65AuthoringTerrainSetCatalog.Tile1Id, Id65AuthoringTerrainSetCatalog.Tile2Id];
Id65AuthoringEditorDocument immutableDocument =
    Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetExact(
        Id65RemotePadMobyKind.None,
        Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
        mutableIds);
mutableIds.Clear();
Require(immutableDocument.ActiveTerrainUnitIds.SequenceEqual(topologies[3].ActiveUnitIds),
    "The schema-5 document retained a mutable active-unit alias.");
ExpectRejected("duplicate terrain ID", () =>
    Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetExact(
        Id65RemotePadMobyKind.None,
        Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
        [Id65AuthoringTerrainSetCatalog.Tile1Id, Id65AuthoringTerrainSetCatalog.Tile1Id]));
ExpectRejected("unknown terrain ID", () =>
    Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetExact(
        Id65RemotePadMobyKind.None,
        Id65RemotePadMobyChoiceCatalog.DefaultPositionId,
        ["tile.remote-pad.unknown"]));
ExpectRejected("noncanonical terrain ID order", () =>
    Id65AuthoringEditorDocument.Parse(System.Text.Encoding.UTF8.GetBytes(
        immutableDocument.CanonicalJson.Replace(
            "\"activeUnitIds\":[\"tile.remote-pad.1\",\"tile.remote-pad.2\"]",
            "\"activeUnitIds\":[\"tile.remote-pad.2\",\"tile.remote-pad.1\"]",
            StringComparison.Ordinal))));
ExpectRejected("forged terrain manifest", () =>
    Id65AuthoringEditorDocument.Parse(System.Text.Encoding.UTF8.GetBytes(
        immutableDocument.CanonicalJson.Replace(
            topologies[3].ManifestSha256,
            new string('0', 64),
            StringComparison.Ordinal))));
ExpectRejected("noncanonical graph input order", () =>
    Id65FreshLevelSceneProjector.GetTerrainSetGraphCanonicalSha256(
        [Id65AuthoringTerrainSetCatalog.Tile2Id, Id65AuthoringTerrainSetCatalog.Tile1Id]));

foreach (bool tile1 in new[] { false, true })
{
    Id65AuthoringEditorControlState schema4 =
        Id65AuthoringEditorControlState.CreateRemotePadMobyChoice(
            tile1,
            Id65RemotePadMobyKind.RedGemT21,
            Id65RemotePadGrassT107Catalog.SouthWestPositionId);
    Id65AuthoringEditorControlState schema5 =
        Id65AuthoringEditorControlState.MigrateRemotePadMobyChoiceToTerrainSet(schema4);
    Require(schema5.UsesRemotePadTerrainSet &&
        schema5.IncludeTerrainTile1 == tile1 && !schema5.IncludeTerrainTile2 &&
        schema5.RemotePadMobyKind == schema4.RemotePadMobyKind &&
        schema5.RemotePadMobyPositionId == schema4.RemotePadMobyPositionId,
        "Schema-4-to-5 state migration did not preserve exact terrain/Moby/position semantics.");
    ExpectRejected("schema-5 reverse/remigration", () =>
        Id65AuthoringEditorControlState.MigrateRemotePadMobyChoiceToTerrainSet(schema5));
}

string storeSource = await File.ReadAllTextAsync(Path.Combine(
    repositoryRoot,
    "src",
    "Spyro.Editor.Core",
    "Persistence",
    "Id65AuthoringEditorDocumentStore.cs"));
Require(!storeSource.Contains("Id65AuthoringTerrainSetCompiler", StringComparison.Ordinal),
    "The schema-5 static Store acquired the physical terrain-set compiler.");

string smokeRoot = Path.Combine(
    Path.GetTempPath(),
    $"Spyro.Editor.Id65AuthoringRemotePadTerrainSetCoreSmoke-{Guid.NewGuid():N}");
Directory.CreateDirectory(smokeRoot);
try
{
    await VerifyDirectSchema5PersistenceAsync(
        cleanImagePath,
        cleanCuePath,
        Path.Combine(smokeRoot, "direct"),
        topologies,
        graphHashes);
    await VerifyExplicitMigrationAsync(
        cleanImagePath,
        cleanCuePath,
        Path.Combine(smokeRoot, "migration"),
        topologies);
}

finally
{
    if (Directory.Exists(smokeRoot))
        Directory.Delete(smokeRoot, recursive: true);
}

Require(graphHashes.Count == 4 &&
    graphHashes.Values.Distinct(StringComparer.Ordinal).Count() == 4 &&
    graphHashes["zero"] ==
        Id65FreshLevelSceneProjector.ExpectedTerrainSetZeroGraphCanonicalSha256 &&
    graphHashes["tile1"] ==
        Id65FreshLevelSceneProjector.ExpectedTerrainSetTile1GraphCanonicalSha256 &&
    graphHashes["tile2"] ==
        Id65FreshLevelSceneProjector.ExpectedTerrainSetTile2GraphCanonicalSha256 &&
    graphHashes["both"] ==
        Id65FreshLevelSceneProjector.ExpectedTerrainSetBothGraphCanonicalSha256,
    "The four schema-5 scene topologies did not produce distinct graph identities.");

Console.WriteLine(
    "ID65_AUTHORING_REMOTE_PAD_TERRAIN_SET_CORE_SMOKE_OK " +
    $"states={stateCount} graphs=" +
    string.Join(',', topologies.Select(item => $"{item.Key}:{graphHashes[item.Key]}")));

static async Task VerifyDirectSchema5PersistenceAsync(
    string cleanImagePath,
    string cleanCuePath,
    string workspaceRoot,
    IReadOnlyList<Topology> topologies,
    IDictionary<string, string> graphHashes)
{
    UnusedLevel65BlankLevelLabBootstrapResult bootstrap =
        await UnusedLevel65BlankLevelLabBootstrapper.BootstrapAsync(new(
            cleanImagePath,
            cleanCuePath,
            workspaceRoot));
    string? envelopeSha256 = null;
    foreach (Topology topology in topologies)
    {
        Id65AuthoringEditorControlState state =
            Id65AuthoringEditorControlState.CreateRemotePadMobyChoiceTerrainSet(
                topology.Tile1,
                topology.Tile2,
                Id65RemotePadMobyKind.None,
                Id65RemotePadMobyChoiceCatalog.DefaultPositionId);
        Id65AuthoringEditorDocumentWriteResult write =
            await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
                bootstrap.Paths,
                bootstrap.Manifest,
                state,
                envelopeSha256);
        Require(write.Document.EnvelopeSchemaVersion ==
                Id65AuthoringEditorDocumentStore
                    .RemotePadMobyChoiceTerrainSetEnvelopeSchemaVersion &&
            write.Document.ProjectionContractId ==
                Id65AuthoringEditorDocumentStore
                    .RemotePadMobyChoiceTerrainSetProjectionContractId &&
            write.Document.StaticAuthoringProfileId ==
                Id65AuthoringEditorDocumentStore
                    .RemotePadMobyChoiceTerrainSetStaticAuthoringProfileId &&
            write.Document.StaticAuthoringTerrainManifestSha256 ==
                topology.ManifestSha256 &&
            write.Document.ActiveTerrainUnitIds.SequenceEqual(topology.ActiveUnitIds) &&
            !write.ExplicitLegacyToRemotePadMigrationPerformed &&
            !write.ExplicitRemotePadMobyChoiceMigrationPerformed &&
            !write.ExplicitRemotePadTerrainSetMigrationPerformed &&
            !write.ProviderOwnedNativeInputGenerationValidatedDuringWrite &&
            write.StaticAuthoringProjectionValidatedDuringWrite,
            $"Direct schema-5 {topology.Key} persistence lost its exact static identity.");
        Id65FreshLevelSceneProjection scene = Id65FreshLevelSceneProjector.ProjectExact(
            UnusedLevel65BlankLevelLabProfileRegistry.Definition,
            write.Document);
        Require(scene.ProfileId == Id65FreshLevelSceneProjector.TerrainSetProfileId &&
            scene.SchemaVersion == Id65FreshLevelSceneProjector.TerrainSetSchemaVersion &&
            scene.UsesTerrainSetProjection && scene.IsPresentationOnly &&
            !scene.NativePersistenceEnabled &&
            !scene.PhysicalMaterializationEnabled && !scene.RuntimeObserved &&
            !scene.WritesWorkspace && !scene.WritesLockedBase &&
            !scene.AuthorizesCreateBin && !scene.AuthorizesRuntimeCandidate &&
            !scene.AuthorizesReleasePromotion &&
            scene.ActiveTerrainUnitIds.SequenceEqual(topology.ActiveUnitIds) &&
            scene.TerrainSetManifestSha256 == topology.ManifestSha256 &&
            scene.StructuralTerrainPresent == (topology.ActiveUnitIds.Length != 0) &&
            scene.RemotePadMobyKind == Id65RemotePadMobyKind.None &&
            scene.VisibleMobyCount == 0 &&
            scene.Geometry.Polygons.Count == 1 + topology.ActiveUnitIds.Length &&
            scene.Geometry.LowDetailPolygons.Count == 1 + topology.ActiveUnitIds.Length &&
            Id65FreshLevelSceneProjector.IsExactProjectionGeometry(scene.Geometry) &&
            scene.GraphCanonicalSha256 ==
                Id65FreshLevelSceneProjector.GetTerrainSetGraphCanonicalSha256(
                    topology.ActiveUnitIds),
            $"Schema-5 fresh-scene projection failed for {topology.Key}.");
        AssertMobyRowPolicy(scene, Id65RemotePadMobyKind.None);
        AssertTopologyGeometry(scene.Geometry, topology);
        graphHashes.Add(topology.Key, scene.GraphCanonicalSha256);
        envelopeSha256 = write.EnvelopeCanonicalSha256;
    }
}

static async Task VerifyExplicitMigrationAsync(
    string cleanImagePath,
    string cleanCuePath,
    string workspaceRoot,
    IReadOnlyList<Topology> topologies)
{
    UnusedLevel65BlankLevelLabBootstrapResult bootstrap =
        await UnusedLevel65BlankLevelLabBootstrapper.BootstrapAsync(new(
            cleanImagePath,
            cleanCuePath,
            workspaceRoot));
    Id65AuthoringEditorControlState schema4 =
        Id65AuthoringEditorControlState.CreateRemotePadMobyChoice(
            includeStructuralTerrain: true,
            Id65RemotePadMobyKind.None,
            Id65RemotePadGrassT107Catalog.CenterPositionId);
    Id65AuthoringEditorDocumentWriteResult schema4Write =
        await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            bootstrap.Paths,
            bootstrap.Manifest,
            schema4,
            expectedPriorEnvelopeCanonicalSha256: null);
    Id65AuthoringEditorControlState migrated =
        Id65AuthoringEditorControlState.MigrateRemotePadMobyChoiceToTerrainSet(schema4);
    await ExpectRejectedAsync("ordinary schema-4-to-5 save", async () => _ =
        await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            bootstrap.Paths,
            bootstrap.Manifest,
            migrated,
            schema4Write.EnvelopeCanonicalSha256));
    await ExpectRejectedAsync("stale schema-4-to-5 CAS", async () => _ =
        await Id65AuthoringEditorDocumentWorkspaceWriter
            .MigrateRemotePadMobyChoiceToTerrainSetExactAsync(
                bootstrap.Paths,
                bootstrap.Manifest,
                migrated,
                new string('0', 64)));

    Id65AuthoringEditorDocumentWriteResult schema5Write =
        await Id65AuthoringEditorDocumentWorkspaceWriter
            .MigrateRemotePadMobyChoiceToTerrainSetExactAsync(
                bootstrap.Paths,
                bootstrap.Manifest,
                migrated,
                schema4Write.EnvelopeCanonicalSha256);
    Require(schema5Write.ExplicitRemotePadTerrainSetMigrationPerformed &&
        !schema5Write.ExplicitLegacyToRemotePadMigrationPerformed &&
        !schema5Write.ExplicitRemotePadMobyChoiceMigrationPerformed &&
        schema5Write.Document.ActiveTerrainUnitIds
            .SequenceEqual(topologies.Single(item => item.Key == "tile1").ActiveUnitIds),
        "The explicit schema-4-to-5 writer receipt lost its one-way migration identity.");

    await ExpectRejectedAsync("schema-5-to-4 ordinary reverse save", async () => _ =
        await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            bootstrap.Paths,
            bootstrap.Manifest,
            schema4,
            schema5Write.EnvelopeCanonicalSha256));
    await ExpectRejectedAsync("schema-5 stale same-family CAS", async () => _ =
        await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            bootstrap.Paths,
            bootstrap.Manifest,
            Id65AuthoringEditorControlState.CreateRemotePadMobyChoiceTerrainSet(
                true,
                true,
                Id65RemotePadMobyKind.RedGemT21,
                Id65RemotePadGrassT107Catalog.NorthEastPositionId),
            schema4Write.EnvelopeCanonicalSha256));

    Id65AuthoringEditorNativeInputProvider.InputSet nativeInputs =
        await Id65AuthoringEditorNativeInputProvider.LoadAsync(
            bootstrap.Paths,
            bootstrap.Manifest);
    Id65AuthoringEditorDocument schema5Document =
        Id65AuthoringEditorControlStateCompiler.Compile(migrated).Document;
    ExpectRejected("legacy physical projector accepted schema 5", () =>
        Id65AuthoringEditorDocumentProjector.Project(new(
            schema5Document,
            nativeInputs.LockedSource,
            nativeInputs.PrivateTextureWitness,
            nativeInputs.GrassBundle)));
}

static void AssertMobyRowPolicy(
    Id65FreshLevelSceneProjection scene,
    Id65RemotePadMobyKind kind)
{
    if (kind == Id65RemotePadMobyKind.None)
    {
        Require(scene.VisibleMobyPresentations.Count == 0,
            "None projected a visible Moby.");
        return;
    }
    Id65FreshLevelMobyPresentation presentation =
        scene.VisibleMobyPresentations.Single();
    if (kind == Id65RemotePadMobyKind.GrassT107)
    {
        Require(presentation.TargetTrueIndex == 107 &&
            presentation.PlacementId == Id65RemotePadGrassT107Catalog.PlacementId &&
            presentation.ExactYawByte == Id65RemotePadGrassT107Catalog.YawByte,
            "Grass lost its exact T107 row/yaw policy.");
    }
    else
    {
        Require(presentation.TargetTrueIndex == 21 &&
            presentation.PlacementId == Id65RemotePadRedGemT21Catalog.PlacementId &&
            presentation.ExactYawByte is null,
            "Red Gem lost its exact retained-row/no-yaw policy.");
    }
}

static void AssertTopologyGeometry(GeometryCandidate geometry, Topology topology)
{
    Require(geometry.Name == Id65FreshLevelSceneProjector.TerrainSetGeometryName,
        "Schema-5 geometry did not use its distinct presentation profile.");
    SceneSectorRenderMetadata sector = geometry.SourceSectors.Single();
    Require(sector.HighDetailVertexCount == 3 + topology.ActiveUnitIds.Length &&
        sector.LowDetailVertexCount == 3 + topology.ActiveUnitIds.Length &&
        sector.HighDetailFaceCount == 1 + topology.ActiveUnitIds.Length &&
        sector.LowDetailFaceCount == 1 + topology.ActiveUnitIds.Length,
        $"The {topology.Key} sector counts are not exact.");
    int[] expectedCollisionOrder = (topology.Tile1, topology.Tile2) switch
    {
        (false, false) => [13_995],
        (true, false) => [19_808, 13_995],
        (false, true) => [19_808, 13_995],
        (true, true) => [19_809, 19_808, 13_995]
    };
    Require(geometry.NativeTerrainOcclusion!.CollisionTriangles
            .Select(item => item.Index).SequenceEqual(expectedCollisionOrder),
        $"The {topology.Key} collision order changed.");

    TerrainPolygon? tile2Hp = geometry.Polygons.SingleOrDefault(item =>
        item.Points.SequenceEqual(
            [new Vector2f(272, 272), new Vector2f(384, 496), new Vector2f(272, 496)]));
    LowDetailTerrainPolygon? tile2Lp = geometry.LowDetailPolygons.SingleOrDefault(item =>
        item.Points.SequenceEqual(
            [new Vector2f(272, 272), new Vector2f(384, 496), new Vector2f(272, 496)]));
    if (!topology.Tile2)
    {
        Require(tile2Hp is null && tile2Lp is null,
            "A non-tile2 topology projected tile2 geometry.");
        return;
    }
    Require(tile2Hp is not null && tile2Lp is not null &&
        WordsHex(
            tile2Hp.NativeFaceWord0,
            tile2Hp.NativeFaceWord1,
            tile2Hp.NativeFaceWord2,
            tile2Hp.NativeFaceWord3) ==
            (topology.Tile1
                ? "000002040101000219024001080A1000"
                : "000002030101000219024001080A1000") &&
        WordsHex(tile2Lp.RawWord0, tile2Lp.RawWord1) ==
            (topology.Tile1 ? "0004210000821000" : "00C3200000821000"),
        $"The {topology.Key} tile2 LP/HP native presentation words changed.");
    NativeTerrainOcclusionTriangle tile2Collision =
        geometry.NativeTerrainOcclusion.CollisionTriangles.First();
    Require(tile2Collision.Index == (topology.Tile1 ? 19_809 : 19_808) &&
        tile2Collision.P1 == new SpyroCollisionPoint(272, 272, 512) &&
        tile2Collision.P2 == new SpyroCollisionPoint(272, 496, 512) &&
        tile2Collision.P3 == new SpyroCollisionPoint(384, 496, 512),
        $"The {topology.Key} tile2 collision winding/index changed.");
}

static string WordsHex(params uint[] words)
{
    byte[] bytes = new byte[words.Length * sizeof(uint)];
    for (int index = 0; index < words.Length; index++)
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4, 4), words[index]);
    return Convert.ToHexString(bytes);
}

static void ExpectRejected(string label, Action action)
{
    try
    {
        action();
    }
    catch (Exception exception) when (exception is
        InvalidDataException or InvalidOperationException or ArgumentException or
        KeyNotFoundException)
    {
        return;
    }
    throw new InvalidOperationException($"The negative case '{label}' was accepted.");
}

static async Task ExpectRejectedAsync(string label, Func<Task> action)
{
    try
    {
        await action();
    }
    catch (Exception exception) when (exception is
        InvalidDataException or InvalidOperationException or IOException or ArgumentException)
    {
        return;
    }
    throw new InvalidOperationException($"The negative case '{label}' was accepted.");
}

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexStringLower(SHA256.HashData(bytes));

static string FindRepositoryRoot(string? start)
{
    DirectoryInfo? current = new(Path.GetFullPath(start ?? Directory.GetCurrentDirectory()));
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
        throw new InvalidOperationException(message);
}

internal sealed record Topology(
    string Key,
    bool Tile1,
    bool Tile2,
    string[] ActiveUnitIds,
    string ManifestSha256);

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

string smokeRoot = Path.Combine(
    Path.GetTempPath(),
    "Spyro.Editor.Id65AuthoringBlankCycloramaDocumentStoreSmoke-" +
        Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(smokeRoot);

try
{
    UnusedLevel65BlankLevelLabWorkspacePaths paths =
        UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
            Path.Combine(smokeRoot, "workspace-container"));
    Directory.CreateDirectory(paths.RootPath);
    Directory.CreateDirectory(paths.AuthoredEditsDirectoryPath);
    UnusedLevel65BlankLevelLabManifest manifest =
        UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(paths);
    await File.WriteAllTextAsync(
        paths.ManifestPath,
        JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        }) + "\n",
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    Id65AuthoringEditorDocumentProjectionInputs staticInputs = new(null);
    string[] emptyTerrain = [];
    string[] tile1 = [Id65AuthoringTerrainSetCatalog.Tile1Id];
    string[] tile2 = [Id65AuthoringTerrainSetCatalog.Tile2Id];
    string[] bothTiles =
    [
        Id65AuthoringTerrainSetCatalog.Tile1Id,
        Id65AuthoringTerrainSetCatalog.Tile2Id
    ];
    string[][] terrainStates = [emptyTerrain, tile1, tile2, bothTiles];
    Id65RemotePadMobyKind[] mobyKinds =
    [
        Id65RemotePadMobyKind.None,
        Id65RemotePadMobyKind.GrassT107,
        Id65RemotePadMobyKind.RedGemT21
    ];

    Require(Id65AuthoringBlankCycloramaCatalog.SchemaVersion == 1,
        "The blank-cyclorama catalog schema changed.");
    Require(
        Id65AuthoringBlankCycloramaCatalog.ProfileId ==
            "id65-authoring-blank-cyclorama-catalog-v1" &&
        Id65AuthoringBlankCycloramaCatalog.BlankSlatePresetId == "blank-slate-v1" &&
        Id65AuthoringBlankCycloramaCatalog.BlankSlateColorHex == "#263447" &&
        Id65AuthoringBlankCycloramaCatalog.CanonicalSha256 ==
            Id65AuthoringBlankCycloramaCatalog.ExpectedCanonicalSha256 &&
        IsSha256(Id65AuthoringBlankCycloramaCatalog.CanonicalSha256),
        "The one code-owned blank-cyclorama preset identity changed.");
    Require(
        !Id65AuthoringBlankCycloramaCatalog.ContainsNativePayloadBytes &&
        !Id65AuthoringBlankCycloramaCatalog.ContainsPhysicalAddresses &&
        !Id65AuthoringBlankCycloramaCatalog.ContainsFilePaths &&
        !Id65AuthoringBlankCycloramaCatalog.ContainsCompiledAfterimage &&
        !Id65AuthoringBlankCycloramaCatalog.ContainsPublisherState &&
        !Id65AuthoringBlankCycloramaCatalog.PersistenceEnabled &&
        !Id65AuthoringBlankCycloramaCatalog.WritesFileSystem &&
        !Id65AuthoringBlankCycloramaCatalog.WritesDiscImage &&
        !Id65AuthoringBlankCycloramaCatalog.WritesCue &&
        !Id65AuthoringBlankCycloramaCatalog.AppIntegrated &&
        !Id65AuthoringBlankCycloramaCatalog.CreateBinEnabled &&
        !Id65AuthoringBlankCycloramaCatalog.NormalCreateBinEnabled &&
        !Id65AuthoringBlankCycloramaCatalog.RuntimeCandidateAuthorized &&
        !Id65AuthoringBlankCycloramaCatalog.ReleaseIntegrated &&
        !Id65AuthoringBlankCycloramaCatalog.PromotionAuthorized &&
        !Id65AuthoringBlankCycloramaCatalog.Publishable,
        "The blank-cyclorama catalog crossed a capability boundary.");

    string initialPositionId = Id65RemotePadGrassT107Catalog.Positions[0].PositionId;
    Id65AuthoringEditorDocument schema5 = Id65AuthoringEditorDocument
        .CreateRemotePadMobyChoiceTerrainSetExact(
            Id65RemotePadMobyKind.None,
            initialPositionId,
            emptyTerrain);
    byte[] schema5CanonicalBefore = schema5.SerializeUtf8();
    Require(
        schema5.SchemaVersion ==
            Id65AuthoringEditorDocument.RemotePadMobyChoiceTerrainSetSchemaVersion &&
        schema5.ProfileId == Id65AuthoringEditorDocument
            .RemotePadMobyChoiceTerrainSetDocumentProfileId &&
        !schema5.UsesBlankCyclorama &&
        !schema5.CanonicalJson.Contains("blankCyclorama", StringComparison.Ordinal) &&
        Id65AuthoringEditorDocument.Parse(schema5CanonicalBefore).SerializeUtf8()
            .SequenceEqual(schema5CanonicalBefore),
        "Schema-5 canonical identity or selector changed while adding schema 6.");

    Id65AuthoringEditorDocumentStoreResult schema5Stored =
        await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            schema5,
            staticInputs,
            expectedPriorEnvelopeCanonicalSha256: null);
    Require(
        Id65AuthoringEditorDocumentStore
            .ValidateRemotePadMobyChoiceTerrainSetStoreResultForAggregate(
                schema5Stored,
                schema5Stored.EnvelopeCanonicalSha256,
                manifest).SerializeUtf8().SequenceEqual(schema5CanonicalBefore),
        "The schema-5 aggregate validator no longer accepts its exact family.");
    await ExpectRejectedAsync(
        "schema-6 validator receiving schema 5",
        async () =>
        {
            _ = Id65AuthoringEditorDocumentStore
                .ValidateRemotePadMobyChoiceTerrainSetBlankCycloramaStoreResultForAggregate(
                    schema5Stored,
                    schema5Stored.EnvelopeCanonicalSha256,
                    manifest);
            await Task.CompletedTask;
        });

    Id65AuthoringEditorDocument firstSchema6 = Id65AuthoringEditorDocument
        .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
            Id65RemotePadMobyKind.None,
            initialPositionId,
            emptyTerrain);
    AssertSchema6Document(firstSchema6);
    AssertStrictTamperRejection(firstSchema6);

    byte[] schema5EnvelopeBeforeRejectedMigration =
        await File.ReadAllBytesAsync(Id65AuthoringEditorDocumentStore.GetPath(paths));
    Id65AuthoringEditorDocument changedMigrationTarget = Id65AuthoringEditorDocument
        .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
            Id65RemotePadMobyKind.GrassT107,
            initialPositionId,
            emptyTerrain);
    await ExpectRejectedAsync(
        "schema-5-to-6 migration that changes Moby intent",
        async () => _ = await Id65AuthoringEditorDocumentStore
            .MigrateRemotePadMobyChoiceTerrainSetToBlankCycloramaPreparedAsync(
                paths,
                manifest,
                changedMigrationTarget,
                staticInputs,
                static staged => staged,
                schema5Stored.EnvelopeCanonicalSha256));
    byte[] schema5EnvelopeAfterRejectedMigration =
        await File.ReadAllBytesAsync(Id65AuthoringEditorDocumentStore.GetPath(paths));
    Require(
        schema5EnvelopeBeforeRejectedMigration.SequenceEqual(
            schema5EnvelopeAfterRejectedMigration),
        "A rejected schema-5-to-6 migration changed the persisted envelope.");
    await ExpectRejectedAsync(
        "ordinary save crossing schema-5-to-6 families",
        async () => _ = await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            firstSchema6,
            staticInputs,
            schema5Stored.EnvelopeCanonicalSha256));

    Id65AuthoringEditorDocumentStoreResult current =
        await Id65AuthoringEditorDocumentStore
            .MigrateRemotePadMobyChoiceTerrainSetToBlankCycloramaPreparedAsync(
                paths,
                manifest,
                firstSchema6,
                staticInputs,
                static staged => staged,
                schema5Stored.EnvelopeCanonicalSha256);
    AssertSchema6Stored(current, manifest);
    Id65AuthoringEditorDocument pathFreeValidated = Id65AuthoringEditorDocumentStore
        .ValidateRemotePadMobyChoiceTerrainSetBlankCycloramaStoreResultForAggregate(
            current with { Path = "/this/path/is-deliberately-not-consulted" },
            current.EnvelopeCanonicalSha256,
            manifest);
    Require(
        pathFreeValidated.SerializeUtf8().SequenceEqual(firstSchema6.SerializeUtf8()),
        "The schema-6 aggregate validator was not path-free.");
    await ExpectRejectedAsync(
        "schema-6 passed to the schema-5-to-6 migration API",
        async () => _ = await Id65AuthoringEditorDocumentStore
            .MigrateRemotePadMobyChoiceTerrainSetToBlankCycloramaPreparedAsync(
                paths,
                manifest,
                changedMigrationTarget,
                staticInputs,
                static staged => staged,
                current.EnvelopeCanonicalSha256));
    Require(
        current.EnvelopeSchemaVersion == Id65AuthoringEditorDocumentStore
            .RemotePadMobyChoiceTerrainSetBlankCycloramaEnvelopeSchemaVersion &&
        current.ProjectionContractId == Id65AuthoringEditorDocumentStore
            .RemotePadMobyChoiceTerrainSetBlankCycloramaProjectionContractId &&
        current.StaticAuthoringProfileId == Id65AuthoringEditorDocumentStore
            .RemotePadMobyChoiceTerrainSetBlankCycloramaStaticAuthoringProfileId,
        "The schema-6 envelope-v5/static-projection-v4 selector is wrong.");

    await ExpectRejectedAsync(
        "schema-5 validator receiving schema 6",
        async () =>
        {
            _ = Id65AuthoringEditorDocumentStore
                .ValidateRemotePadMobyChoiceTerrainSetStoreResultForAggregate(
                    current,
                    current.EnvelopeCanonicalSha256,
                    manifest);
            await Task.CompletedTask;
        });
    await ExpectRejectedAsync(
        "forged cyclorama catalog hash",
        async () =>
        {
            _ = Id65AuthoringEditorDocumentStore
                .ValidateRemotePadMobyChoiceTerrainSetBlankCycloramaStoreResultForAggregate(
                    current with
                    {
                        StaticAuthoringCycloramaCatalogSha256 = new string('0', 64)
                    },
                    current.EnvelopeCanonicalSha256,
                    manifest);
            await Task.CompletedTask;
        });

    HashSet<string> stateHashes = new(StringComparer.Ordinal);
    int stateCount = 0;
    bool skippedFirst = false;
    bool staleCasTested = false;
    foreach (string[] terrainState in terrainStates)
    {
        foreach (Id65RemotePadMobyKind mobyKind in mobyKinds)
        {
            foreach (Id65RemotePadGrassT107Position position in
                     Id65RemotePadGrassT107Catalog.Positions)
            {
                Id65AuthoringEditorDocument state = Id65AuthoringEditorDocument
                    .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
                        mobyKind,
                        position.PositionId,
                        terrainState);
                AssertSchema6Document(state);
                Id65AuthoringEditorDocument reparsed =
                    Id65AuthoringEditorDocument.Parse(state.SerializeUtf8());
                Require(
                    reparsed.SerializeUtf8().SequenceEqual(state.SerializeUtf8()),
                    "A schema-6 state did not round-trip canonical UTF-8 exactly.");
                Require(stateHashes.Add(state.CanonicalSha256),
                    "Two distinct schema-6 states share one document identity.");

                if (terrainState.Length > 1)
                {
                    Id65AuthoringEditorDocument reversed = Id65AuthoringEditorDocument
                        .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
                            mobyKind,
                            position.PositionId,
                            terrainState.Reverse());
                    Require(
                        state.SerializeUtf8().SequenceEqual(reversed.SerializeUtf8()),
                        "Schema-6 terrain selection order was not canonicalized.");
                }

                bool isFirst = terrainState.Length == 0 &&
                    mobyKind == Id65RemotePadMobyKind.None &&
                    position.PositionId == initialPositionId;
                if (isFirst && !skippedFirst)
                {
                    skippedFirst = true;
                }
                else
                {
                    string priorIdentity = current.EnvelopeCanonicalSha256;
                    current = await Id65AuthoringEditorDocumentStore.SaveAsync(
                        paths,
                        manifest,
                        state,
                        staticInputs,
                        priorIdentity);
                    AssertSchema6Stored(current, manifest);
                    if (!staleCasTested)
                    {
                        staleCasTested = true;
                        byte[] afterReplacement = await File.ReadAllBytesAsync(
                            Id65AuthoringEditorDocumentStore.GetPath(paths));
                        await ExpectRejectedAsync(
                            "stale schema-6 CAS",
                            async () => _ = await Id65AuthoringEditorDocumentStore.SaveAsync(
                                paths,
                                manifest,
                                firstSchema6,
                                staticInputs,
                                priorIdentity));
                        byte[] afterRejectedStaleCas = await File.ReadAllBytesAsync(
                            Id65AuthoringEditorDocumentStore.GetPath(paths));
                        Require(
                            afterReplacement.SequenceEqual(afterRejectedStaleCas),
                            "A stale schema-6 CAS changed the persisted envelope.");
                    }
                }
                stateCount++;
            }
        }
    }

    Require(
        stateCount == 108 && stateHashes.Count == 108 && skippedFirst && staleCasTested,
        "The focused smoke did not cover all 4 x 3 x 9 schema-6 states.");
    Id65AuthoringEditorDocumentStoreResult loaded =
        await Id65AuthoringEditorDocumentStore.LoadAsync(
            paths,
            manifest,
            staticInputs) ?? throw new InvalidDataException(
                "The final schema-6 store result was missing.");
    AssertSchema6Stored(loaded, manifest);
    Require(
        loaded.EnvelopeCanonicalSha256 == current.EnvelopeCanonicalSha256 &&
        loaded.Document.SerializeUtf8().SequenceEqual(current.Document.SerializeUtf8()),
        "The final schema-6 load changed its canonical CAS identity.");

    Console.WriteLine(
        "ID65_AUTHORING_BLANK_CYCLORAMA_DOCUMENT_STORE_SMOKE_OK " +
        $"states={stateCount} envelopeSchema={current.EnvelopeSchemaVersion} " +
        $"preset={Id65AuthoringBlankCycloramaCatalog.BlankSlatePresetId} " +
        $"color={Id65AuthoringBlankCycloramaCatalog.BlankSlateColorHex}");
}
finally
{
    if (Directory.Exists(smokeRoot))
        Directory.Delete(smokeRoot, recursive: true);
}

static void AssertSchema6Document(Id65AuthoringEditorDocument document)
{
    Require(
        document.SchemaVersion == Id65AuthoringEditorDocument
            .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion &&
        document.ProfileId == Id65AuthoringEditorDocument
            .RemotePadMobyChoiceTerrainSetBlankCycloramaDocumentProfileId &&
        document.UsesRemotePadMobyChoice &&
        document.UsesRemotePadTerrainSet &&
        document.UsesBlankCyclorama &&
        document.BlankCyclorama is not null,
        "A schema-6 document did not select the exact terrain/Moby/cyclorama family.");
    Id65AuthoringBlankCycloramaIntent cyclorama = document.BlankCyclorama ??
        throw new InvalidDataException("The schema-6 document lost blankCyclorama.");
    Id65AuthoringBlankCycloramaCatalog.RequireExact(cyclorama);
    Require(
        !cyclorama.ContainsNativePayloadBytes &&
        !cyclorama.ContainsPhysicalAddresses &&
        !cyclorama.ContainsFilePaths &&
        !cyclorama.ContainsCompiledAfterimage &&
        !cyclorama.ContainsPublisherState &&
        !cyclorama.PersistenceEnabled &&
        !cyclorama.WritesFileSystem &&
        !cyclorama.WritesDiscImage &&
        !cyclorama.WritesCue &&
        !cyclorama.AppIntegrated &&
        !cyclorama.CreateBinEnabled &&
        !cyclorama.NormalCreateBinEnabled &&
        !cyclorama.RuntimeCandidateAuthorized &&
        !cyclorama.ReleaseIntegrated &&
        !cyclorama.PromotionAuthorized &&
        !cyclorama.Publishable &&
        !document.ContainsNativePayloadBytes &&
        !document.ContainsPhysicalAddresses &&
        !document.ContainsFilePaths &&
        !document.ContainsCompiledAfterimage &&
        !document.ContainsPublisherState &&
        !document.PersistenceEnabled &&
        !document.WritesFileSystem &&
        !document.WritesDiscImage &&
        !document.WritesCue &&
        !document.CreateBinEnabled &&
        !document.NormalCreateBinEnabled &&
        !document.RuntimeCandidateAuthorized &&
        !document.ReleaseIntegrated &&
        !document.PromotionAuthorized &&
        !document.Publishable,
        "A schema-6 document or cyclorama intent crossed a capability boundary.");

    JsonObject root = JsonNode.Parse(document.SerializeUtf8())?.AsObject() ??
        throw new InvalidDataException("The schema-6 document is not a JSON object.");
    JsonObject cycloramaJson = root["blankCyclorama"]?.AsObject() ??
        throw new InvalidDataException("The schema-6 document lost blankCyclorama.");
    Require(
        cycloramaJson.Select(item => item.Key).SequenceEqual(
            ["schemaVersion", "profileId", "presetId", "colorHex"]) &&
        cycloramaJson.Count == 4 &&
        cycloramaJson["presetId"]?.GetValue<string>() == "blank-slate-v1" &&
        cycloramaJson["colorHex"]?.GetValue<string>() == "#263447",
        "The schema-6 cyclorama object is not exact, path-free canonical intent.");
}

static void AssertSchema6Stored(
    Id65AuthoringEditorDocumentStoreResult stored,
    UnusedLevel65BlankLevelLabManifest manifest)
{
    Id65AuthoringEditorDocument exact = Id65AuthoringEditorDocumentStore
        .ValidateRemotePadMobyChoiceTerrainSetBlankCycloramaStoreResultForAggregate(
            stored,
            stored.EnvelopeCanonicalSha256,
            manifest);
    Require(
        exact.SerializeUtf8().SequenceEqual(stored.Document.SerializeUtf8()) &&
        stored.StaticAuthoringCycloramaCatalogSha256 ==
            Id65AuthoringBlankCycloramaCatalog.CanonicalSha256 &&
        !stored.HasCompiledCompositeProjection &&
        stored.HasStaticAuthoringProjection &&
        !stored.ContainsCompositeAfterimage &&
        !stored.ContainsDiscImageBytes &&
        !stored.ContainsCueBytes &&
        !stored.WritesDiscImage &&
        !stored.WritesCue &&
        !stored.AppIntegrated &&
        !stored.CreateBinEnabled &&
        !stored.NormalCreateBinEnabled &&
        !stored.RuntimeCandidateAuthorized &&
        !stored.ReleaseIntegrated &&
        !stored.PromotionAuthorized &&
        !stored.Publishable,
        "A schema-6 store result crossed its static-only capability boundary.");
}

static void AssertStrictTamperRejection(Id65AuthoringEditorDocument document)
{
    ExpectParseRejected("forged blank preset", document, root =>
        root["blankCyclorama"]!.AsObject()["presetId"] = "blank-slate-v2");
    ExpectParseRejected("forged black color", document, root =>
        root["blankCyclorama"]!.AsObject()["colorHex"] = "#000000");
    ExpectParseRejected("extra native offset", document, root =>
        root["blankCyclorama"]!.AsObject()["nativeOffset"] = 1234);
    ExpectParseRejected("reordered cyclorama object", document, root =>
    {
        JsonObject cyclorama = root["blankCyclorama"]!.AsObject();
        JsonNode? schemaVersion = cyclorama["schemaVersion"]?.DeepClone();
        cyclorama.Remove("schemaVersion");
        cyclorama["schemaVersion"] = schemaVersion;
    });
}

static void ExpectParseRejected(
    string role,
    Id65AuthoringEditorDocument document,
    Action<JsonObject> mutate)
{
    JsonObject root = JsonNode.Parse(document.SerializeUtf8())?.AsObject() ??
        throw new InvalidDataException("The schema-6 document did not parse in smoke.");
    mutate(root);
    try
    {
        _ = Id65AuthoringEditorDocument.Parse(
            Encoding.UTF8.GetBytes(root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = false
            })));
    }
    catch (InvalidDataException)
    {
        return;
    }
    throw new InvalidDataException($"The schema-6 parser accepted {role}.");
}

static async Task ExpectRejectedAsync(string role, Func<Task> action)
{
    try
    {
        await action();
    }
    catch (Exception exception) when (exception is
        InvalidDataException or NotSupportedException)
    {
        return;
    }
    throw new InvalidDataException($"The schema-6 store accepted {role}.");
}

static bool IsSha256(string value) =>
    value.Length == 64 && value.All(char.IsAsciiHexDigit);

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidDataException(message);
}

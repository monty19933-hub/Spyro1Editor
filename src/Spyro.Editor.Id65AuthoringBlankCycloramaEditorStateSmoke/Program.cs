using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

const string ExpectedPresetId = "blank-slate-v1";
const string ExpectedColorHex = "#263447";
const string ExpectedCatalogSha256 =
    "a4c3919239f7a5f817555e0c654e8025fab59f864aec3ea1499ea64bf491de75";
const string SmokeRootPrefix =
    "Spyro.Editor.Id65AuthoringBlankCycloramaEditorStateSmoke-";

string smokeRoot = Path.Combine(
    Path.GetTempPath(),
    SmokeRootPrefix + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(smokeRoot);
int rejects = 0;
int publications = 0;

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

    Require(
        Id65AuthoringEditorControlState
            .RemotePadMobyChoiceTerrainSetBlankCycloramaProfileId ==
                "id65-authoring-editor-control-state-remote-pad-moby-choice-terrain-set-blank-cyclorama-v5" &&
        Id65AuthoringEditorControlState
            .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion == 5 &&
        Id65AuthoringEditorDocumentReadModel
            .RemotePadMobyChoiceTerrainSetBlankCycloramaReadModelSchemaVersion == 6,
        "The schema-6 editor-state profile or read-model discriminator changed.");

    string initialPositionId = Id65RemotePadGrassT107Catalog.Positions[0].PositionId;
    Id65AuthoringEditorControlState schema5 = Id65AuthoringEditorControlState
        .CreateRemotePadMobyChoiceTerrainSet(
            includeTerrainTile1: false,
            includeTerrainTile2: false,
            Id65RemotePadMobyKind.None,
            initialPositionId);
    Id65AuthoringEditorControlStateCompilation schema5Compilation =
        Id65AuthoringEditorControlStateCompiler.Compile(schema5);
    Id65AuthoringEditorControlStateCompilation schema5Repeat =
        Id65AuthoringEditorControlStateCompiler.Compile(
            Id65AuthoringEditorControlState.CreateRemotePadMobyChoiceTerrainSet(
                includeTerrainTile1: false,
                includeTerrainTile2: false,
                Id65RemotePadMobyKind.None,
                initialPositionId));
    Require(
        !schema5.UsesBlankCyclorama &&
        schema5.BlankCycloramaPresetId.Length == 0 &&
        schema5.BlankCycloramaColorHex.Length == 0 &&
        schema5Compilation.DocumentCanonicalSha256 ==
            schema5Repeat.DocumentCanonicalSha256 &&
        schema5Compilation.DocumentContentSha256 == schema5Repeat.DocumentContentSha256,
        "Schema-5 control identity changed while adding schema 6.");

    Id65AuthoringEditorDocumentWriteResult schema5Stored =
        await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            schema5,
            expectedPriorEnvelopeCanonicalSha256: null);
    publications++;
    Require(
        schema5Stored.CreatedNewDocument &&
        !schema5Stored.Document.UsesBlankCyclorama &&
        schema5Stored.Document.ReadModelSchemaVersion ==
            Id65AuthoringEditorDocumentReadModel
                .RemotePadMobyChoiceTerrainSetReadModelSchemaVersion &&
        schema5Stored.Document.StaticAuthoringCycloramaCatalogSha256.Length == 0 &&
        !schema5Stored.ExplicitBlankCycloramaMigrationPerformed,
        "The initial schema-5 writer/read-model identity changed.");

    Id65AuthoringEditorControlState migrated = Id65AuthoringEditorControlState
        .MigrateRemotePadMobyChoiceTerrainSetToBlankCyclorama(schema5);
    AssertMigrationPreserved(schema5, migrated);
    await ExpectRejectedAsync(
        "ordinary schema-5-to-6 save",
        async () => _ = await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            migrated,
            schema5Stored.EnvelopeCanonicalSha256));
    rejects++;
    Id65AuthoringEditorControlState changedMigrationTarget =
        Id65AuthoringEditorControlState
            .CreateRemotePadMobyChoiceTerrainSetBlankCyclorama(
                includeTerrainTile1: true,
                includeTerrainTile2: false,
                Id65RemotePadMobyKind.None,
                initialPositionId);
    await ExpectRejectedAsync(
        "schema-5-to-6 migration changing terrain",
        async () => _ = await Id65AuthoringEditorDocumentWorkspaceWriter
            .MigrateRemotePadMobyChoiceTerrainSetToBlankCycloramaExactAsync(
                paths,
                manifest,
                changedMigrationTarget,
                schema5Stored.EnvelopeCanonicalSha256));
    rejects++;

    Id65AuthoringEditorDocumentWriteResult current =
        await Id65AuthoringEditorDocumentWorkspaceWriter
            .MigrateRemotePadMobyChoiceTerrainSetToBlankCycloramaExactAsync(
                paths,
                manifest,
                migrated,
                schema5Stored.EnvelopeCanonicalSha256);
    publications++;
    Require(
        current.ExplicitBlankCycloramaMigrationPerformed &&
        !current.ExplicitLegacyToRemotePadMigrationPerformed &&
        !current.ExplicitRemotePadMobyChoiceMigrationPerformed &&
        !current.ExplicitRemotePadTerrainSetMigrationPerformed,
        "The schema-5-to-6 writer receipt did not report only its explicit migration.");
    AssertSchema6Result(current, migrated, explicitMigration: true);

    HashSet<string> canonicalDocuments = new(StringComparer.Ordinal);
    int states = 0;
    foreach (Id65RemotePadGrassT107Position position in
             Id65RemotePadGrassT107Catalog.Positions)
    foreach (Id65RemotePadMobyKind kind in new[]
             {
                 Id65RemotePadMobyKind.None,
                 Id65RemotePadMobyKind.GrassT107,
                 Id65RemotePadMobyKind.RedGemT21
             })
    foreach ((bool tile1, bool tile2) in new[]
             {
                 (false, false),
                 (true, false),
                 (false, true),
                 (true, true)
             })
    {
        Id65AuthoringEditorControlState state = Id65AuthoringEditorControlState
            .CreateRemotePadMobyChoiceTerrainSetBlankCyclorama(
                tile1,
                tile2,
                kind,
                position.PositionId);
        Id65AuthoringEditorControlStateCompilation compilation =
            Id65AuthoringEditorControlStateCompiler.Compile(state);
        Require(
            compilation.UsesBlankCyclorama &&
            compilation.BlankCycloramaPresetId == ExpectedPresetId &&
            compilation.BlankCycloramaColorHex == ExpectedColorHex &&
            compilation.State.UsesBlankCyclorama &&
            !compilation.ContainsFilePaths && !compilation.ContainsGameBytes &&
            !compilation.ContainsNativePayloadBytes &&
            !compilation.ContainsPhysicalAddresses &&
            !compilation.ContainsCompiledAfterimage &&
            !compilation.NativeInputsDerived && !compilation.PersistencePerformed &&
            !compilation.AggregateMaterializationEnabled &&
            !compilation.WritesFileSystem && !compilation.WritesDiscImage &&
            !compilation.WritesCue && !compilation.CreateBinEnabled &&
            !compilation.NormalCreateBinEnabled &&
            !compilation.RuntimeCandidateAuthorized && !compilation.RuntimeProven &&
            !compilation.ReleaseIntegrated && !compilation.PromotionAuthorized &&
            !compilation.Publishable,
            "A schema-6 control compilation lost its exact intent or capability closure.");
        Require(
            canonicalDocuments.Add(compilation.DocumentCanonicalSha256),
            "Two distinct schema-6 control states collapsed to one document identity.");

        current = await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            state,
            current.EnvelopeCanonicalSha256);
        publications++;
        states++;
        AssertSchema6Result(current, state, explicitMigration: false);
        Id65AuthoringEditorDocumentReadModel inspected =
            await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                paths,
                manifest) ?? throw new InvalidOperationException(
                    "The schema-6 inspector returned no persisted document.");
        AssertSchema6ReadModel(inspected);
        Id65AuthoringEditorControlState recovered =
            Id65AuthoringEditorControlStateCompiler.RecoverControlState(inspected);
        Require(
            Equivalent(state, recovered) &&
            inspected.DocumentCanonicalSha256 ==
                compilation.DocumentCanonicalSha256 &&
            inspected.DocumentContentSha256 == compilation.DocumentContentSha256,
            "A schema-6 persisted read model did not recover its exact control state.");
    }
    Require(states == 108 && canonicalDocuments.Count == 108,
        "The schema-6 editor-state smoke did not exhaust all 108 states.");

    string staleEnvelope = current.EnvelopeCanonicalSha256;
    Id65AuthoringEditorControlState changed = Id65AuthoringEditorControlState
        .CreateRemotePadMobyChoiceTerrainSetBlankCyclorama(
            !current.ControlState.IncludeTerrainTile1,
            current.ControlState.IncludeTerrainTile2,
            current.ControlState.RemotePadMobyKind!.Value,
            current.ControlState.RemotePadMobyPositionId!);
    current = await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
        paths,
        manifest,
        changed,
        staleEnvelope);
    publications++;
    byte[] beforeStaleReject = await File.ReadAllBytesAsync(Path.Combine(
        paths.AuthoredEditsDirectoryPath,
        Id65AuthoringEditorDocumentWorkspaceInspector.DocumentFileName));
    await ExpectRejectedAsync(
        "stale schema-6 CAS",
        async () => _ = await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            migrated,
            staleEnvelope));
    rejects++;
    byte[] afterStaleReject = await File.ReadAllBytesAsync(Path.Combine(
        paths.AuthoredEditsDirectoryPath,
        Id65AuthoringEditorDocumentWorkspaceInspector.DocumentFileName));
    Require(beforeStaleReject.SequenceEqual(afterStaleReject),
        "A stale schema-6 CAS attempt changed the persisted envelope.");

    Id65AuthoringEditorControlState reverseSchema5 =
        Id65AuthoringEditorControlState.CreateRemotePadMobyChoiceTerrainSet(
            changed.IncludeTerrainTile1,
            changed.IncludeTerrainTile2,
            changed.RemotePadMobyKind!.Value,
            changed.RemotePadMobyPositionId!);
    await ExpectRejectedAsync(
        "implicit schema-6-to-5 reverse save",
        async () => _ = await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            reverseSchema5,
            current.EnvelopeCanonicalSha256));
    rejects++;
    await ExpectRejectedAsync(
        "schema-6 source passed to explicit schema-5-to-6 migration",
        async () => _ = await Id65AuthoringEditorDocumentWorkspaceWriter
            .MigrateRemotePadMobyChoiceTerrainSetToBlankCycloramaExactAsync(
                paths,
                manifest,
                changed,
                current.EnvelopeCanonicalSha256));
    rejects++;
    ExpectRejected<InvalidOperationException>(
        "control-state schema-6 remigration",
        () => _ = Id65AuthoringEditorControlState
            .MigrateRemotePadMobyChoiceTerrainSetToBlankCyclorama(changed));
    rejects++;

    Id65AuthoringEditorDocumentReadModel firstInspection =
        await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(paths, manifest)
        ?? throw new InvalidOperationException("Final schema-6 inspection was absent.");
    Id65AuthoringEditorDocumentReadModel repeatedInspection =
        await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(paths, manifest)
        ?? throw new InvalidOperationException("Repeated schema-6 inspection was absent.");
    Require(
        firstInspection.ReadModelCanonicalSha256 ==
            repeatedInspection.ReadModelCanonicalSha256 &&
        firstInspection.DocumentCanonicalSha256 ==
            repeatedInspection.DocumentCanonicalSha256 &&
        firstInspection.EnvelopeCanonicalSha256 ==
            repeatedInspection.EnvelopeCanonicalSha256,
        "Repeated schema-6 inspection changed a canonical identity.");

    Console.WriteLine(
        "ID65_AUTHORING_BLANK_CYCLORAMA_EDITOR_STATE_SMOKE_OK " +
        $"states={states} publications={publications} rejects={rejects} " +
        $"preset={ExpectedPresetId} color={ExpectedColorHex} " +
        $"finalDocument={current.DocumentCanonicalSha256} " +
        $"finalEnvelope={current.EnvelopeCanonicalSha256}");
}
finally
{
    if (Directory.Exists(smokeRoot) &&
        Path.GetFileName(smokeRoot).StartsWith(SmokeRootPrefix, StringComparison.Ordinal))
    {
        Directory.Delete(smokeRoot, recursive: true);
    }
    Require(!Directory.Exists(smokeRoot),
        "The schema-6 editor-state smoke retained its temporary workspace.");
}

static void AssertMigrationPreserved(
    Id65AuthoringEditorControlState source,
    Id65AuthoringEditorControlState migrated)
{
    Require(
        migrated.UsesBlankCyclorama &&
        migrated.BlankCycloramaPresetId == ExpectedPresetId &&
        migrated.BlankCycloramaColorHex == ExpectedColorHex &&
        source.IncludeTerrainTile1 == migrated.IncludeTerrainTile1 &&
        source.IncludeTerrainTile2 == migrated.IncludeTerrainTile2 &&
        source.RemotePadMobyKind == migrated.RemotePadMobyKind &&
        source.RemotePadMobyPositionId == migrated.RemotePadMobyPositionId &&
        source.T107PlacementSemantics == migrated.T107PlacementSemantics &&
        source.TerrainSelectionSemantics == migrated.TerrainSelectionSemantics &&
        source.MandatoryBaseFaceTextureId == migrated.MandatoryBaseFaceTextureId,
        "The explicit schema-5-to-6 control migration changed existing intent.");
}

static void AssertSchema6Result(
    Id65AuthoringEditorDocumentWriteResult result,
    Id65AuthoringEditorControlState requested,
    bool explicitMigration)
{
    Require(
        Equivalent(result.ControlState, requested) &&
        result.ExplicitBlankCycloramaMigrationPerformed == explicitMigration &&
        !result.AutomaticMigrationPerformed &&
        result.ExplicitSaveInvocationRequired &&
        result.ExactControlStateCompiled && result.LegacyTerrainLayerConflictChecked &&
        !result.ProviderOwnedNativeInputGenerationValidatedDuringWrite &&
        !result.LockedSourceRevalidatedDuringWrite &&
        result.CurrentProjectionRevalidatedDuringWrite &&
        result.StaticAuthoringProjectionValidatedDuringWrite &&
        !result.LegacyPriorCompositeProjectionValidatedDuringWrite &&
        result.CompareAndSwapEnforced && result.AtomicPublicationCompleted &&
        result.PersistencePerformed && result.FileSystemMutationPerformed &&
        result.WritesFileSystem && !result.ContainsFilePaths &&
        !result.ContainsGameBytes && !result.ContainsNativePayloadBytes &&
        !result.ContainsPhysicalAddresses && !result.ContainsCompiledAfterimage &&
        !result.CarriesAppOrUiAuthority && !result.WritesDiscImage &&
        !result.WritesCue && !result.AggregateMaterializationEnabled &&
        !result.CreateBinEnabled && !result.NormalCreateBinEnabled &&
        !result.RuntimeCandidateAuthorized && !result.RuntimeProven &&
        !result.ReleaseIntegrated && !result.PromotionAuthorized &&
        !result.Publishable && result.RequiresTrustedExclusiveWorkspaceDirectory &&
        !result.HostileFilesystemRaceSafe && !result.CommonWorkspaceLeaseOwned,
        "A schema-6 writer receipt lost its exact state or capability closure.");
    AssertSchema6ReadModel(result.Document);
}

static void AssertSchema6ReadModel(Id65AuthoringEditorDocumentReadModel document)
{
    Require(
        document.SchemaVersion == 6 &&
        document.ProfileId ==
            "id65-authoring-editor-document-remote-pad-moby-choice-terrain-set-blank-cyclorama-v6" &&
        document.ReadModelSchemaVersion ==
            Id65AuthoringEditorDocumentReadModel
                .RemotePadMobyChoiceTerrainSetBlankCycloramaReadModelSchemaVersion &&
        document.EnvelopeSchemaVersion == 5 &&
        document.ProjectionContractId ==
            "id65-authoring-editor-document-projector-v5" &&
        document.StaticAuthoringProfileId ==
            "id65-authoring-editor-document-remote-pad-moby-choice-terrain-set-blank-cyclorama-static-projection-v4" &&
        document.UsesRemotePadMobyChoice && document.UsesRemotePadTerrainSet &&
        document.UsesBlankCyclorama &&
        document.BlankCycloramaPresetId == ExpectedPresetId &&
        document.BlankCycloramaColorHex == ExpectedColorHex &&
        document.StaticAuthoringCycloramaCatalogSha256 == ExpectedCatalogSha256 &&
        document.StaticAuthoringCatalogSha256.Length == 64 &&
        document.StaticAuthoringSceneGraphSha256.Length == 64 &&
        document.StaticAuthoringProjectionSha256.Length == 64 &&
        document.StaticAuthoringTerrainManifestSha256.Length == 64 &&
        document.HasStaticAuthoringProjection &&
        !document.HasCompiledCompositeProjection &&
        document.EnvelopeCanonicalIdentityVerified &&
        document.DocumentCanonicalIdentityVerified &&
        document.PersistedProjectionIdentityPresent &&
        document.DeterministicEnvelopeReadbackVerified &&
        !document.CurrentProjectionRevalidated &&
        !document.CurrentLockedSourceReprojected &&
        !document.CurrentOnDiskFreshnessVerified &&
        !document.ContainsGameBytes && !document.ContainsNativePayloadBytes &&
        !document.ContainsPhysicalAddresses && !document.ContainsFilePaths &&
        !document.ContainsCompiledAfterimage && !document.ContainsPublisherState &&
        !document.PersistenceEnabled && !document.FileSystemMutationPerformed &&
        !document.WritesFileSystem && !document.WritesDiscImage &&
        !document.WritesCue && !document.CarriesAppOrUiAuthority &&
        !document.CreateBinEnabled && !document.NormalCreateBinEnabled &&
        !document.RuntimeCandidateAuthorized && !document.RuntimeProven &&
        !document.ReleaseIntegrated && !document.PromotionAuthorized &&
        !document.Publishable && document.RequiresTrustedExclusiveWorkspaceDirectory &&
        !document.HostileFilesystemRaceSafe && !document.CommonWorkspaceLeaseOwned,
        "A schema-6 read model lost its exact receipt-backed identity or capability closure.");
}

static bool Equivalent(
    Id65AuthoringEditorControlState left,
    Id65AuthoringEditorControlState right) =>
    left.IncludeTerrainTile1 == right.IncludeTerrainTile1 &&
    left.IncludeTerrainTile2 == right.IncludeTerrainTile2 &&
    left.IncludeGrassAtT88 == right.IncludeGrassAtT88 &&
    left.IncludeGrassAtT107 == right.IncludeGrassAtT107 &&
    left.MandatoryBaseFaceTextureId == right.MandatoryBaseFaceTextureId &&
    left.T107PlacementSemantics == right.T107PlacementSemantics &&
    left.TerrainSelectionSemantics == right.TerrainSelectionSemantics &&
    left.T107RemotePadPositionId == right.T107RemotePadPositionId &&
    left.RemotePadMobyKind == right.RemotePadMobyKind &&
    left.RemotePadMobyPositionId == right.RemotePadMobyPositionId &&
    left.UsesBlankCyclorama == right.UsesBlankCyclorama &&
    left.BlankCycloramaPresetId == right.BlankCycloramaPresetId &&
    left.BlankCycloramaColorHex == right.BlankCycloramaColorHex;

static async Task ExpectRejectedAsync(string label, Func<Task> action)
{
    try
    {
        await action();
    }
    catch (Exception exception) when (exception is InvalidDataException or IOException)
    {
        return;
    }
    throw new InvalidOperationException($"Expected {label} to be rejected.");
}

static void ExpectRejected<TException>(string label, Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }
    throw new InvalidOperationException($"Expected {label} to be rejected.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

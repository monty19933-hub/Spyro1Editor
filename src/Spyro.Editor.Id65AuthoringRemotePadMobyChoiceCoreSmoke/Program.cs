using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Workspace;

const string ExpectedCleanImageSha256 =
    "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string cleanImagePath = Path.Combine(repositoryRoot, "Spyro the Dragon (USA).bin");
string cleanCuePath = Path.Combine(repositoryRoot, "Spyro the Dragon (USA).cue");
Require(File.Exists(cleanImagePath) && File.Exists(cleanCuePath),
    "The schema-4 smoke requires the exact USA BIN/CUE fixtures.");
Require(Hash(await File.ReadAllBytesAsync(cleanImagePath)) == ExpectedCleanImageSha256,
    "The schema-4 smoke source image identity changed.");

Require(Id65RemotePadMobyChoiceCatalog.Choices.Count == 3 &&
    Id65RemotePadMobyChoiceCatalog.CanonicalSha256 ==
        Id65RemotePadMobyChoiceCatalog.ExpectedCanonicalSha256 &&
    !Id65RemotePadMobyChoiceCatalog.RuntimeAcceptancePassed &&
    !Id65RemotePadMobyChoiceCatalog.RuntimeCandidateAuthorized &&
    !Id65RemotePadMobyChoiceCatalog.PhysicalMaterializationEnabled &&
    !Id65RemotePadMobyChoiceCatalog.NormalCreateBinAuthorized &&
    !Id65RemotePadMobyChoiceCatalog.ReleasePublicationAuthorized,
    "The schema-4 choice catalog identity or fail-closed boundary changed.");

int stateCount = 0;
HashSet<string> canonicalDocumentHashes = new(StringComparer.Ordinal);
foreach (bool terrain in new[] { false, true })
{
    foreach (Id65RemotePadMobyKind kind in Enum.GetValues<Id65RemotePadMobyKind>())
    {
        foreach (Id65RemotePadGrassT107Position position in
                 Id65RemotePadGrassT107Catalog.Positions)
        {
            Id65AuthoringEditorControlState state =
                Id65AuthoringEditorControlState.CreateRemotePadMobyChoice(
                    terrain,
                    kind,
                    position.PositionId);
            Id65AuthoringEditorControlStateCompilation compiled =
                Id65AuthoringEditorControlStateCompiler.Compile(state);
            Id65AuthoringEditorDocument document = compiled.Document;
            Id65AuthoringEditorDocument reparsed =
                Id65AuthoringEditorDocument.Parse(document.SerializeUtf8());
            Id65AuthoringEditorDocumentReadModel readModel =
                Id65AuthoringEditorDocumentWorkspaceInspector.InspectCanonicalDocument(
                    reparsed.SerializeUtf8());
            Id65AuthoringEditorControlState recovered =
                Id65AuthoringEditorControlStateCompiler.RecoverExactState(reparsed);

            Require(document.SchemaVersion ==
                    Id65AuthoringEditorDocument.RemotePadMobyChoiceSchemaVersion &&
                document.ProfileId ==
                    Id65AuthoringEditorDocument.RemotePadMobyChoiceDocumentProfileId &&
                document.MandatoryBaseFaceTextureId == 66 &&
                document.UsesRemotePadMobyChoice &&
                !document.UsesRemotePadT107Grid &&
                document.RemotePadMobyKind == kind &&
                document.RemotePadMobyPositionId == position.PositionId &&
                document.Terrain.Count == (terrain ? 1 : 0) &&
                document.MobyPlacements.Count == (kind == Id65RemotePadMobyKind.None ? 0 : 1) &&
                reparsed.SerializeUtf8().SequenceEqual(document.SerializeUtf8()) &&
                readModel.ReadModelSchemaVersion ==
                    Id65AuthoringEditorDocumentReadModel.RemotePadMobyChoiceReadModelSchemaVersion &&
                readModel.RemotePadMobyKind == kind &&
                readModel.RemotePadMobyPositionId == position.PositionId &&
                readModel.RemotePadMobyPosition?.SelectedExactRowSha256 ==
                    Id65RemotePadMobyChoiceCatalog.GetSelectedExactRowSha256(
                        kind,
                        position.PositionId) &&
                recovered.UsesRemotePadMobyChoice &&
                recovered.RemotePadMobyKind == kind &&
                recovered.RemotePadMobyPositionId == position.PositionId &&
                !recovered.IncludeGrassAtT88 &&
                recovered.IncludeGrassAtT107 ==
                    (kind == Id65RemotePadMobyKind.GrassT107) &&
                recovered.IncludeRedGemAtT21 ==
                    (kind == Id65RemotePadMobyKind.RedGemT21),
                $"Schema-4 state failed exact roundtrip: terrain={terrain}, kind={kind}, position={position.PositionId}.");
            Require(canonicalDocumentHashes.Add(document.CanonicalSha256),
                "Two distinct schema-4 states produced the same canonical document identity.");
            stateCount++;
        }
    }
}
Require(stateCount == 54 && canonicalDocumentHashes.Count == 54,
    "The schema-4 state space is not exactly 54 distinct canonical documents.");

Id65AuthoringEditorControlState schema3None =
    Id65AuthoringEditorControlState.CreateRemotePad(
        includeStructuralTerrain: true,
        includeGrassAtT88: false,
        includeGrassAtT107: false,
        Id65RemotePadGrassT107Catalog.CenterPositionId,
        mandatoryBaseFaceTextureId: 66);
Id65AuthoringEditorControlState migratedNone =
    Id65AuthoringEditorControlState.MigrateRemotePadGridToMobyChoice(schema3None);
Id65AuthoringEditorControlState schema3Grass =
    Id65AuthoringEditorControlState.CreateRemotePad(
        includeStructuralTerrain: false,
        includeGrassAtT88: false,
        includeGrassAtT107: true,
        Id65RemotePadGrassT107Catalog.EastPositionId,
        mandatoryBaseFaceTextureId: 66);
Id65AuthoringEditorControlState migratedGrass =
    Id65AuthoringEditorControlState.MigrateRemotePadGridToMobyChoice(schema3Grass);
Require(migratedNone.RemotePadMobyKind == Id65RemotePadMobyKind.None &&
    migratedNone.RemotePadMobyPositionId == schema3None.T107RemotePadPositionId &&
    migratedNone.IncludeStructuralTerrain &&
    migratedGrass.RemotePadMobyKind == Id65RemotePadMobyKind.GrassT107 &&
    migratedGrass.RemotePadMobyPositionId == schema3Grass.T107RemotePadPositionId &&
    !migratedGrass.IncludeStructuralTerrain,
    "Explicit schema-3-to-4 state migration did not preserve terrain/position or exact None/Grass mapping.");
ExpectRejected(
    "schema-4 state passed to legacy-to-schema-3 migration",
    () => Id65AuthoringEditorControlState.MigrateLegacyToRemotePadGrid(
        Id65AuthoringEditorControlState.CreateRemotePadMobyChoice(
            includeStructuralTerrain: false,
            Id65RemotePadMobyKind.RedGemT21,
            Id65RemotePadMobyChoiceCatalog.DefaultPositionId)));
ExpectRejected(
    "schema-3 T88 migration",
    () => Id65AuthoringEditorControlState.MigrateRemotePadGridToMobyChoice(
        Id65AuthoringEditorControlState.CreateRemotePad(
            true,
            includeGrassAtT88: true,
            includeGrassAtT107: false,
            Id65RemotePadGrassT107Catalog.CenterPositionId,
            mandatoryBaseFaceTextureId: 66)));

string smokeRoot = Path.Combine(
    Path.GetTempPath(),
    $"Spyro.Editor.Id65AuthoringRemotePadMobyChoiceCoreSmoke-{Guid.NewGuid():N}");
Directory.CreateDirectory(smokeRoot);
try
{
    UnusedLevel65BlankLevelLabBootstrapResult bootstrap =
        await UnusedLevel65BlankLevelLabBootstrapper.BootstrapAsync(new(
            cleanImagePath,
            cleanCuePath,
            Path.Combine(smokeRoot, "workspace")));
    UnusedLevel65BlankLevelLabWorkspacePaths paths = bootstrap.Paths;
    UnusedLevel65BlankLevelLabManifest manifest = bootstrap.Manifest;
    Id65AuthoringEditorNativeInputProvider.InputSet nativeInputs =
        await Id65AuthoringEditorNativeInputProvider.LoadAsync(paths, manifest);
    Id65AuthoringEditorDocument schema4PhysicalRefusalDocument =
        Id65AuthoringEditorControlStateCompiler.Compile(migratedNone).Document;
    ExpectRejected(
        "schema-4 physical composite projection",
        () => Id65AuthoringEditorDocumentProjector.Project(new(
            schema4PhysicalRefusalDocument,
            nativeInputs.LockedSource,
            nativeInputs.PrivateTextureWitness,
            nativeInputs.GrassBundle)));

    Id65AuthoringEditorDocumentWriteResult schema3Write =
        await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            schema3None,
            expectedPriorEnvelopeCanonicalSha256: null);
    string schema3EnvelopeSha256 = schema3Write.EnvelopeCanonicalSha256;

    await ExpectRejectedAsync(
        "ordinary schema-3-to-4 save",
        async () => _ = await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            migratedNone,
            schema3EnvelopeSha256));
    await ExpectRejectedAsync(
        "direct Red Gem migration",
        async () => _ = await Id65AuthoringEditorDocumentWorkspaceWriter
            .MigrateRemotePadGridToMobyChoiceExactAsync(
                paths,
                manifest,
                Id65AuthoringEditorControlState.CreateRemotePadMobyChoice(
                    true,
                    Id65RemotePadMobyKind.RedGemT21,
                    Id65RemotePadGrassT107Catalog.CenterPositionId),
                schema3EnvelopeSha256));

    Id65AuthoringEditorDocumentWriteResult schema4NoneWrite =
        await Id65AuthoringEditorDocumentWorkspaceWriter
            .MigrateRemotePadGridToMobyChoiceExactAsync(
                paths,
                manifest,
                migratedNone,
                schema3EnvelopeSha256);
    Require(schema4NoneWrite.ExplicitRemotePadMobyChoiceMigrationPerformed &&
        !schema4NoneWrite.ExplicitLegacyToRemotePadMigrationPerformed &&
        !schema4NoneWrite.ProviderOwnedNativeInputGenerationValidatedDuringWrite &&
        schema4NoneWrite.StaticAuthoringProjectionValidatedDuringWrite &&
        schema4NoneWrite.Document.EnvelopeSchemaVersion ==
            Id65AuthoringEditorDocumentStore.RemotePadMobyChoiceEnvelopeSchemaVersion &&
        schema4NoneWrite.Document.ProjectionContractId ==
            Id65AuthoringEditorDocumentStore.RemotePadMobyChoiceProjectionContractId,
        "The explicit schema-3-to-4 write receipt lost its static fail-closed identity.");

    Id65FreshLevelSceneProjection noneScene = Id65FreshLevelSceneProjector.ProjectExact(
        UnusedLevel65BlankLevelLabProfileRegistry.Definition,
        schema4NoneWrite.Document);
    Require(noneScene.RemotePadMobyKind == Id65RemotePadMobyKind.None &&
        noneScene.VisibleMobyCount == 0 && noneScene.StructuralTerrainPresent,
        "Schema-4 None did not project the exact empty fresh scene.");

    string staleEnvelopeSha256 = schema4NoneWrite.EnvelopeCanonicalSha256;
    Id65AuthoringEditorControlState grassState =
        Id65AuthoringEditorControlState.CreateRemotePadMobyChoice(
            true,
            Id65RemotePadMobyKind.GrassT107,
            Id65RemotePadGrassT107Catalog.NorthPositionId);
    Id65AuthoringEditorDocumentWriteResult grassWrite =
        await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            grassState,
            staleEnvelopeSha256);
    Id65FreshLevelSceneProjection grassScene = Id65FreshLevelSceneProjector.ProjectExact(
        UnusedLevel65BlankLevelLabProfileRegistry.Definition,
        grassWrite.Document);
    Require(grassScene.RemotePadMobyKind == Id65RemotePadMobyKind.GrassT107 &&
        grassScene.VisibleMobyCount == 1 &&
        grassScene.VisibleMobyPresentations[0].MobyKind ==
            Id65RemotePadMobyKind.GrassT107 &&
        grassScene.VisibleMobyPresentations[0].TargetTrueIndex == 107 &&
        grassScene.VisibleMobyPresentations[0].ExactYawByte ==
            Id65RemotePadGrassT107Catalog.YawByte,
        "Schema-4 Grass did not project exact static T107 presentation readback.");

    Id65AuthoringEditorControlState redGemState =
        Id65AuthoringEditorControlState.CreateRemotePadMobyChoice(
            false,
            Id65RemotePadMobyKind.RedGemT21,
            Id65RemotePadRedGemT21Catalog.SouthEastPositionId);
    await ExpectRejectedAsync(
        "stale schema-4 CAS",
        async () => _ = await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            redGemState,
            staleEnvelopeSha256));
    Id65AuthoringEditorDocumentReadModel unchanged =
        await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(paths, manifest)
        ?? throw new InvalidDataException("The schema-4 document disappeared after stale CAS rejection.");
    Require(unchanged.EnvelopeCanonicalSha256 == grassWrite.EnvelopeCanonicalSha256 &&
        unchanged.RemotePadMobyKind == Id65RemotePadMobyKind.GrassT107,
        "A stale schema-4 CAS mutated the persisted document.");

    Id65AuthoringEditorDocumentWriteResult redGemWrite =
        await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            redGemState,
            grassWrite.EnvelopeCanonicalSha256);
    Id65AuthoringEditorDocumentReadModel inspected =
        await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(paths, manifest)
        ?? throw new InvalidDataException("The persisted schema-4 Red Gem document was not inspectable.");
    Id65FreshLevelSceneProjection redGemScene = Id65FreshLevelSceneProjector.ProjectExact(
        UnusedLevel65BlankLevelLabProfileRegistry.Definition,
        inspected);
    Id65FreshLevelMobyPresentation gem = redGemScene.VisibleMobyPresentations.Single();
    Require(inspected.EnvelopeCanonicalSha256 == redGemWrite.EnvelopeCanonicalSha256 &&
        inspected.RemotePadMobyKind == Id65RemotePadMobyKind.RedGemT21 &&
        redGemScene.RemotePadMobyKind == Id65RemotePadMobyKind.RedGemT21 &&
        !redGemScene.StructuralTerrainPresent &&
        gem.MobyKind == Id65RemotePadMobyKind.RedGemT21 &&
        gem.TargetTrueIndex == Id65RemotePadRedGemT21Catalog.TargetTrueIndex &&
        gem.ActorId == Id65RemotePadRedGemT21Catalog.ActorClass &&
        gem.ExactYawByte is null &&
        gem.RowSha256 == Id65RemotePadRedGemT21Catalog.GetRequiredPosition(
            Id65RemotePadRedGemT21Catalog.SouthEastPositionId).OutputRowSha256 &&
        !redGemScene.AuthorizesCreateBin &&
        !redGemScene.AuthorizesRuntimeCandidate &&
        !redGemScene.AuthorizesReleasePromotion,
        "Schema-4 Red Gem did not project exact static T21 presentation readback.");
}
finally
{
    if (Directory.Exists(smokeRoot))
        Directory.Delete(smokeRoot, recursive: true);
}

Console.WriteLine(
    "ID65_AUTHORING_REMOTE_PAD_MOBY_CHOICE_CORE_SMOKE_OK " +
    $"states={stateCount} catalog={Id65RemotePadMobyChoiceCatalog.CanonicalSha256}");

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidDataException(message);
}

static void ExpectRejected(string role, Action action)
{
    try
    {
        action();
    }
    catch (Exception exception) when (exception is
        InvalidDataException or InvalidOperationException or ArgumentException)
    {
        return;
    }
    throw new InvalidDataException($"The smoke accepted invalid state: {role}.");
}

static async Task ExpectRejectedAsync(string role, Func<Task> action)
{
    try
    {
        await action();
    }
    catch (Exception exception) when (exception is
        InvalidDataException or InvalidOperationException or ArgumentException)
    {
        return;
    }
    throw new InvalidDataException($"The smoke accepted invalid operation: {role}.");
}

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexStringLower(SHA256.HashData(bytes));

static string FindRepositoryRoot(string? supplied)
{
    if (!string.IsNullOrWhiteSpace(supplied))
        return Path.GetFullPath(supplied);
    DirectoryInfo? current = new(AppContext.BaseDirectory);
    while (current != null)
    {
        if (File.Exists(Path.Combine(current.FullName, "spyro-level-catalog.json")) &&
            Directory.Exists(Path.Combine(current.FullName, "src", "Spyro.Editor.Core")))
        {
            return current.FullName;
        }
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

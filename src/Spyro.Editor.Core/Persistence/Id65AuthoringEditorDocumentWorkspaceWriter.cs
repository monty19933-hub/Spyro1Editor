using System.Text.Json;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

/// <summary>
/// Path-free receipt for one explicit canonical ID65 document publication.
/// It reports work completed during this call but carries no native payload,
/// candidate-output authority, Create BIN permission, or runtime/release proof.
/// </summary>
public abstract class Id65AuthoringEditorDocumentWriteResult
{
    private protected Id65AuthoringEditorDocumentWriteResult()
    {
    }

    public abstract Id65AuthoringEditorControlState ControlState { get; }
    public abstract Id65AuthoringEditorDocumentReadModel Document { get; }
    public abstract bool CreatedNewDocument { get; }
    public abstract bool ExplicitLegacyToRemotePadMigrationPerformed { get; }
    public abstract bool ExplicitRemotePadMobyChoiceMigrationPerformed { get; }
    public abstract bool ExplicitRemotePadTerrainSetMigrationPerformed { get; }
    public bool ReplacedExistingDocument => !CreatedNewDocument;
    public bool AutomaticMigrationPerformed => false;
    public string DocumentCanonicalSha256 => Document.DocumentCanonicalSha256;
    public string DocumentContentSha256 => Document.DocumentContentSha256;
    public string EnvelopeCanonicalSha256 => Document.EnvelopeCanonicalSha256;

    public bool ExplicitSaveInvocationRequired => true;
    public bool LegacyTerrainLayerConflictChecked => true;
    public bool ExactControlStateCompiled => true;
    public bool ProviderOwnedNativeInputGenerationValidatedDuringWrite =>
        !Document.UsesStaticRemotePadAuthoring ||
        ExplicitLegacyToRemotePadMigrationPerformed;
    public bool LockedSourceRevalidatedDuringWrite =>
        ProviderOwnedNativeInputGenerationValidatedDuringWrite;
    public bool CurrentProjectionRevalidatedDuringWrite => true;
    public bool StaticAuthoringProjectionValidatedDuringWrite =>
        Document.UsesStaticRemotePadAuthoring;
    public bool LegacyPriorCompositeProjectionValidatedDuringWrite =>
        ExplicitLegacyToRemotePadMigrationPerformed ||
        (ReplacedExistingDocument && !Document.UsesStaticRemotePadAuthoring);
    public bool CompareAndSwapEnforced => true;
    public bool AtomicPublicationCompleted => true;
    public bool PersistencePerformed => true;
    public bool FileSystemMutationPerformed => true;
    public bool WritesFileSystem => true;
    public bool ContainsFilePaths => false;
    public bool ContainsGameBytes => false;
    public bool ContainsNativePayloadBytes => false;
    public bool ContainsPhysicalAddresses => false;
    public bool ContainsCompiledAfterimage => false;
    public bool CarriesAppOrUiAuthority => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool AggregateMaterializationEnabled => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool RuntimeProven => false;
    public bool ReleaseIntegrated => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;
    public bool RequiresTrustedExclusiveWorkspaceDirectory => true;
    public bool HostileFilesystemRaceSafe => false;
    public bool CommonWorkspaceLeaseOwned => false;
}

/// <summary>
/// Explicit, project-bound writer for the exact ID65 authoring document. This
/// validates the exact workspace manifest and then uses the Store's
/// compare-and-swap publication. Legacy saves and explicit migration additionally
/// validate provider-owned native inputs; fresh schema-3 creates and schema-3
/// updates remain document/static-authoring only. It must not be called from
/// project open, background admission, UI construction, or UI refresh.
/// </summary>
public static class Id65AuthoringEditorDocumentWorkspaceWriter
{
    private const int MaximumLegacyTerrainEditByteCount = 16 * 1024 * 1024;
    private const string LegacyTerrainEditFileName =
        "unusedlevel65blank-terrain-edits.json";

    public static async Task<Id65AuthoringEditorDocumentWriteResult> SaveExactAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest expectedLabManifest,
        Id65AuthoringEditorControlState state,
        string? expectedPriorEnvelopeCanonicalSha256,
        CancellationToken cancellationToken = default) =>
        await SaveCoreAsync(
            paths,
            expectedLabManifest,
            state,
            expectedPriorEnvelopeCanonicalSha256,
            explicitLegacyToRemotePadMigration: false,
            explicitRemotePadMobyChoiceMigration: false,
            explicitRemotePadTerrainSetMigration: false,
            cancellationToken);

    /// <summary>
    /// Performs the only supported semantic-family transition. The caller must
    /// explicitly supply a schema-3 remote-pad state and the exact prior legacy
    /// envelope identity; the store never infers Center or migrates on load/save.
    /// </summary>
    public static async Task<Id65AuthoringEditorDocumentWriteResult>
        MigrateLegacyToRemotePadGridExactAsync(
            UnusedLevel65BlankLevelLabWorkspacePaths paths,
            UnusedLevel65BlankLevelLabManifest expectedLabManifest,
            Id65AuthoringEditorControlState remoteState,
            string expectedPriorEnvelopeCanonicalSha256,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(remoteState);
        if (!remoteState.UsesRemotePadT107Grid ||
            string.IsNullOrWhiteSpace(expectedPriorEnvelopeCanonicalSha256))
        {
            throw new InvalidDataException(
                "Explicit ID65 migration requires a caller-supplied schema-3 remote-pad state and exact prior envelope identity.");
        }

        return await SaveCoreAsync(
            paths,
            expectedLabManifest,
            remoteState,
            expectedPriorEnvelopeCanonicalSha256,
            explicitLegacyToRemotePadMigration: true,
            explicitRemotePadMobyChoiceMigration: false,
            explicitRemotePadTerrainSetMigration: false,
            cancellationToken);
    }

    /// <summary>
    /// Performs the explicit schema-3 to schema-4 semantic-family transition.
    /// The state must already be the exact None/Grass migration result; ordinary
    /// save never upgrades or infers this boundary.
    /// </summary>
    public static async Task<Id65AuthoringEditorDocumentWriteResult>
        MigrateRemotePadGridToMobyChoiceExactAsync(
            UnusedLevel65BlankLevelLabWorkspacePaths paths,
            UnusedLevel65BlankLevelLabManifest expectedLabManifest,
            Id65AuthoringEditorControlState remotePadMobyChoiceState,
            string expectedPriorEnvelopeCanonicalSha256,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(remotePadMobyChoiceState);
        if (!remotePadMobyChoiceState.UsesRemotePadMobyChoice ||
            remotePadMobyChoiceState.RemotePadMobyKind is not
                (Id65RemotePadMobyKind.None or Id65RemotePadMobyKind.GrassT107) ||
            string.IsNullOrWhiteSpace(expectedPriorEnvelopeCanonicalSha256))
        {
            throw new InvalidDataException(
                "Explicit schema-3-to-4 migration requires an exact None/Grass schema-4 state and prior envelope identity.");
        }

        return await SaveCoreAsync(
            paths,
            expectedLabManifest,
            remotePadMobyChoiceState,
            expectedPriorEnvelopeCanonicalSha256,
            explicitLegacyToRemotePadMigration: false,
            explicitRemotePadMobyChoiceMigration: true,
            explicitRemotePadTerrainSetMigration: false,
            cancellationToken);
    }

    /// <summary>
    /// Performs the explicit schema-4 to schema-5 semantic-family transition.
    /// The state must already be the exact terrain-set migration result; ordinary
    /// save never upgrades or infers tile-2 selection at this boundary.
    /// </summary>
    public static async Task<Id65AuthoringEditorDocumentWriteResult>
        MigrateRemotePadMobyChoiceToTerrainSetExactAsync(
            UnusedLevel65BlankLevelLabWorkspacePaths paths,
            UnusedLevel65BlankLevelLabManifest expectedLabManifest,
            Id65AuthoringEditorControlState terrainSetState,
            string expectedPriorEnvelopeCanonicalSha256,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(terrainSetState);
        if (!terrainSetState.UsesRemotePadTerrainSet ||
            string.IsNullOrWhiteSpace(expectedPriorEnvelopeCanonicalSha256))
        {
            throw new InvalidDataException(
                "Explicit schema-4-to-5 migration requires an exact terrain-set state and prior envelope identity.");
        }

        return await SaveCoreAsync(
            paths,
            expectedLabManifest,
            terrainSetState,
            expectedPriorEnvelopeCanonicalSha256,
            explicitLegacyToRemotePadMigration: false,
            explicitRemotePadMobyChoiceMigration: false,
            explicitRemotePadTerrainSetMigration: true,
            cancellationToken);
    }

    private static async Task<Id65AuthoringEditorDocumentWriteResult> SaveCoreAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest expectedLabManifest,
        Id65AuthoringEditorControlState state,
        string? expectedPriorEnvelopeCanonicalSha256,
        bool explicitLegacyToRemotePadMigration,
        bool explicitRemotePadMobyChoiceMigration,
        bool explicitRemotePadTerrainSetMigration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(expectedLabManifest);
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();

        UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(
            expectedLabManifest,
            paths);
        await RequireNoConflictingLegacyTerrainLayerAsync(paths, cancellationToken);
        Id65AuthoringEditorControlStateCompilation compilation =
            Id65AuthoringEditorControlStateCompiler.Compile(state);
        cancellationToken.ThrowIfCancellationRequested();

        bool requiresLegacyNativeInputs =
            !state.UsesRemotePadT107Grid && !state.UsesRemotePadMobyChoice ||
            explicitLegacyToRemotePadMigration;
        Id65AuthoringEditorNativeInputProvider.InputSet? nativeInputs =
            requiresLegacyNativeInputs
                ? await Id65AuthoringEditorNativeInputProvider.LoadAsync(
                    paths,
                    expectedLabManifest,
                    cancellationToken)
                : null;
        cancellationToken.ThrowIfCancellationRequested();
        await RequireNoConflictingLegacyTerrainLayerAsync(paths, cancellationToken);

        Func<Id65AuthoringEditorDocumentStoreResult, Id65AuthoringEditorDocumentWriteResult>
            prepareResult = stored => PrepareResult(
                state,
                compilation,
                stored,
                createdNewDocument: expectedPriorEnvelopeCanonicalSha256 == null,
                explicitLegacyToRemotePadMigration,
                explicitRemotePadMobyChoiceMigration,
                explicitRemotePadTerrainSetMigration);
        Id65AuthoringEditorDocumentProjectionInputs projectionInputs =
            new(nativeInputs);
        return explicitLegacyToRemotePadMigration
            ? await Id65AuthoringEditorDocumentStore.MigratePreparedAsync(
                paths,
                expectedLabManifest,
                compilation.Document,
                projectionInputs,
                prepareResult,
                expectedPriorEnvelopeCanonicalSha256!,
                cancellationToken)
            : explicitRemotePadMobyChoiceMigration
                ? await Id65AuthoringEditorDocumentStore
                    .MigrateRemotePadGridToMobyChoicePreparedAsync(
                        paths,
                        expectedLabManifest,
                        compilation.Document,
                        projectionInputs,
                        prepareResult,
                        expectedPriorEnvelopeCanonicalSha256!,
                        cancellationToken)
            : explicitRemotePadTerrainSetMigration
                ? await Id65AuthoringEditorDocumentStore
                    .MigrateRemotePadMobyChoiceToTerrainSetPreparedAsync(
                        paths,
                        expectedLabManifest,
                        compilation.Document,
                        projectionInputs,
                        prepareResult,
                        expectedPriorEnvelopeCanonicalSha256!,
                        cancellationToken)
            : await Id65AuthoringEditorDocumentStore.SavePreparedAsync(
                paths,
                expectedLabManifest,
                compilation.Document,
                projectionInputs,
                prepareResult,
                expectedPriorEnvelopeCanonicalSha256,
                cancellationToken);
    }

    private static Id65AuthoringEditorDocumentWriteResult PrepareResult(
        Id65AuthoringEditorControlState state,
        Id65AuthoringEditorControlStateCompilation compilation,
        Id65AuthoringEditorDocumentStoreResult stored,
        bool createdNewDocument,
        bool explicitLegacyToRemotePadMigration,
        bool explicitRemotePadMobyChoiceMigration,
        bool explicitRemotePadTerrainSetMigration)
    {
        Id65AuthoringEditorDocumentEnvelopeIdentity identity = new(
            stored.Document,
            stored.EnvelopeCanonicalSha256,
            stored.ProjectionContractId,
            stored.CompositeProfileId,
            stored.CompositeManifestSha256,
            stored.CompositeOutputRow80Sha256,
            stored.CompositeDeterministicPlanSha256)
        {
            StaticAuthoringProfileId = stored.StaticAuthoringProfileId,
            StaticAuthoringCatalogSha256 = stored.StaticAuthoringCatalogSha256,
            StaticAuthoringSceneGraphSha256 = stored.StaticAuthoringSceneGraphSha256,
            StaticAuthoringProjectionSha256 = stored.StaticAuthoringProjectionSha256,
            StaticAuthoringTerrainManifestSha256 =
                stored.StaticAuthoringTerrainManifestSha256
        };
        Id65AuthoringEditorDocumentReadModel readModel = new(identity);
        Id65AuthoringEditorControlState recovered =
            Id65AuthoringEditorControlStateCompiler.RecoverControlState(readModel);
        RequireEquivalent(state, recovered);

        Id65AuthoringEditorDocumentWriteResult result = new WorkspaceWriteResult(
            recovered,
            readModel,
            createdNewDocument,
            explicitLegacyToRemotePadMigration,
            explicitRemotePadMobyChoiceMigration,
            explicitRemotePadTerrainSetMigration);
        ValidateResult(
            result,
            compilation,
            stored,
            createdNewDocument,
            explicitLegacyToRemotePadMigration,
            explicitRemotePadMobyChoiceMigration,
            explicitRemotePadTerrainSetMigration);
        return result;
    }

    private static async Task RequireNoConflictingLegacyTerrainLayerAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string authoredRoot = Path.GetFullPath(paths.AuthoredEditsDirectoryPath);
        string legacyPath = Path.GetFullPath(Path.Combine(
            authoredRoot,
            LegacyTerrainEditFileName));
        PersistencePathSafety.EnsureWritePathWithinRoot(
            paths.RootPath,
            authoredRoot,
            "The ID65 legacy terrain root escapes its versioned lab workspace.");
        PersistencePathSafety.EnsureWritePathWithinRoot(
            authoredRoot,
            legacyPath,
            "The ID65 legacy terrain file escapes its authored-edits root.");

        DirectoryInfo authoredDirectory = new(authoredRoot);
        authoredDirectory.Refresh();
        if (!authoredDirectory.Exists)
            return;
        if (authoredDirectory.LinkTarget != null ||
            (authoredDirectory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "The ID65 authored-edits directory must not be a symbolic link or reparse point.");
        }

        DirectoryInfo legacyDirectory = new(legacyPath);
        legacyDirectory.Refresh();
        if (legacyDirectory.Exists || legacyDirectory.LinkTarget != null)
        {
            throw new InvalidDataException(
                "The reserved ID65 legacy terrain path must not be a directory or symbolic link.");
        }

        FileInfo legacyFile = new(legacyPath);
        legacyFile.Refresh();
        if (!legacyFile.Exists)
            return;
        if (legacyFile.LinkTarget != null ||
            (legacyFile.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "The ID65 legacy terrain file must not be a symbolic link or reparse point.");
        }
        if (legacyFile.Length <= 0 ||
            legacyFile.Length > MaximumLegacyTerrainEditByteCount)
        {
            throw new InvalidDataException(
                "The ID65 legacy terrain file is empty or exceeds its bounded inspection limit.");
        }

        long byteLength = legacyFile.Length;
        long writeTicks = legacyFile.LastWriteTimeUtc.Ticks;
        await using FileStream stream = new(
            legacyPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using JsonDocument document = await JsonDocument.ParseAsync(
            stream,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32
            },
            cancellationToken);
        JsonElement root = document.RootElement;
        JsonProperty[] properties = root.ValueKind == JsonValueKind.Object
            ? root.EnumerateObject().ToArray()
            : [];
        JsonProperty[] countProperties = properties
            .Where(property => property.NameEquals("editCount"))
            .ToArray();
        JsonProperty[] editsProperties = properties
            .Where(property => property.NameEquals("edits"))
            .ToArray();
        if (countProperties.Length != 1 || editsProperties.Length != 1 ||
            countProperties[0].Value.ValueKind != JsonValueKind.Number ||
            !countProperties[0].Value.TryGetInt32(out int editCount) ||
            editCount < 0 ||
            editsProperties[0].Value.ValueKind != JsonValueKind.Array ||
            editsProperties[0].Value.GetArrayLength() != editCount)
        {
            throw new InvalidDataException(
                "The ID65 legacy terrain file must contain one exact editCount matching one edits array.");
        }

        legacyFile.Refresh();
        if (!legacyFile.Exists || legacyFile.Length != byteLength ||
            legacyFile.LastWriteTimeUtc.Ticks != writeTicks)
        {
            throw new IOException(
                "The ID65 legacy terrain file changed during conflict inspection.");
        }
        if (editCount > 0)
        {
            throw new InvalidDataException(
                "Saved legacy ID65 HP-Z/texture terrain edits must be explicitly converted or removed before creating a canonical full-authoring document.");
        }
    }

    private static void RequireEquivalent(
        Id65AuthoringEditorControlState expected,
        Id65AuthoringEditorControlState actual)
    {
        if (expected.IncludeStructuralTerrain != actual.IncludeStructuralTerrain ||
            expected.IncludeTerrainTile2 != actual.IncludeTerrainTile2 ||
            expected.IncludeGrassAtT88 != actual.IncludeGrassAtT88 ||
            expected.IncludeGrassAtT107 != actual.IncludeGrassAtT107 ||
            expected.MandatoryBaseFaceTextureId != actual.MandatoryBaseFaceTextureId ||
            expected.T107PlacementSemantics != actual.T107PlacementSemantics ||
            expected.TerrainSelectionSemantics != actual.TerrainSelectionSemantics ||
            !string.Equals(
                expected.T107RemotePadPositionId,
                actual.T107RemotePadPositionId,
                StringComparison.Ordinal) ||
            expected.RemotePadMobyKind != actual.RemotePadMobyKind ||
            !string.Equals(
                expected.RemotePadMobyPositionId,
                actual.RemotePadMobyPositionId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The saved ID65 document did not recover its requested exact control state.");
        }
    }

    private static void ValidateResult(
        Id65AuthoringEditorDocumentWriteResult result,
        Id65AuthoringEditorControlStateCompilation compilation,
        Id65AuthoringEditorDocumentStoreResult stored,
        bool createdNewDocument,
        bool expectedExplicitLegacyMigration,
        bool expectedExplicitMobyChoiceMigration,
        bool expectedExplicitTerrainSetMigration)
    {
        bool expectedNativeValidation =
            !result.Document.UsesStaticRemotePadAuthoring ||
            expectedExplicitLegacyMigration;
        bool expectedLegacyPriorProjectionValidation =
            expectedExplicitLegacyMigration ||
            (!createdNewDocument && !result.Document.UsesStaticRemotePadAuthoring);
        if (result is not WorkspaceWriteResult ||
            result.DocumentCanonicalSha256 != compilation.DocumentCanonicalSha256 ||
            result.DocumentContentSha256 != compilation.DocumentContentSha256 ||
            result.EnvelopeCanonicalSha256 != stored.EnvelopeCanonicalSha256 ||
            result.ExplicitLegacyToRemotePadMigrationPerformed !=
                expectedExplicitLegacyMigration ||
            result.ExplicitRemotePadMobyChoiceMigrationPerformed !=
                expectedExplicitMobyChoiceMigration ||
            result.ExplicitRemotePadTerrainSetMigrationPerformed !=
                expectedExplicitTerrainSetMigration ||
            result.AutomaticMigrationPerformed ||
            !result.ExplicitSaveInvocationRequired ||
            !result.LegacyTerrainLayerConflictChecked ||
            !result.ExactControlStateCompiled ||
            result.ProviderOwnedNativeInputGenerationValidatedDuringWrite !=
                expectedNativeValidation ||
            result.LockedSourceRevalidatedDuringWrite != expectedNativeValidation ||
            !result.CurrentProjectionRevalidatedDuringWrite ||
            result.StaticAuthoringProjectionValidatedDuringWrite !=
                result.Document.UsesStaticRemotePadAuthoring ||
            result.LegacyPriorCompositeProjectionValidatedDuringWrite !=
                expectedLegacyPriorProjectionValidation ||
            !result.CompareAndSwapEnforced ||
            !result.AtomicPublicationCompleted ||
            !result.PersistencePerformed ||
            !result.FileSystemMutationPerformed ||
            !result.WritesFileSystem ||
            result.ContainsFilePaths || result.ContainsGameBytes ||
            result.ContainsNativePayloadBytes || result.ContainsPhysicalAddresses ||
            result.ContainsCompiledAfterimage || result.CarriesAppOrUiAuthority ||
            result.WritesDiscImage || result.WritesCue ||
            result.AggregateMaterializationEnabled || result.CreateBinEnabled ||
            result.NormalCreateBinEnabled || result.RuntimeCandidateAuthorized ||
            result.RuntimeProven || result.ReleaseIntegrated ||
            result.PromotionAuthorized || result.Publishable ||
            !result.RequiresTrustedExclusiveWorkspaceDirectory ||
            result.HostileFilesystemRaceSafe || result.CommonWorkspaceLeaseOwned)
        {
            throw new InvalidDataException(
                "The ID65 document write receipt lost an exact identity or fail-closed boundary.");
        }
    }

    private sealed class WorkspaceWriteResult :
        Id65AuthoringEditorDocumentWriteResult
    {
        internal WorkspaceWriteResult(
            Id65AuthoringEditorControlState state,
            Id65AuthoringEditorDocumentReadModel document,
            bool createdNewDocument,
            bool explicitLegacyToRemotePadMigration,
            bool explicitRemotePadMobyChoiceMigration,
            bool explicitRemotePadTerrainSetMigration)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(document);
            Id65AuthoringEditorControlState recovered =
                Id65AuthoringEditorControlStateCompiler.RecoverControlState(document);
            RequireEquivalent(state, recovered);

            ControlState = Id65AuthoringEditorControlState.CopyExact(recovered);
            Document = document;
            CreatedNewDocument = createdNewDocument;
            ExplicitLegacyToRemotePadMigrationPerformed =
                explicitLegacyToRemotePadMigration;
            ExplicitRemotePadMobyChoiceMigrationPerformed =
                explicitRemotePadMobyChoiceMigration;
            ExplicitRemotePadTerrainSetMigrationPerformed =
                explicitRemotePadTerrainSetMigration;
        }

        public override Id65AuthoringEditorControlState ControlState { get; }
        public override Id65AuthoringEditorDocumentReadModel Document { get; }
        public override bool CreatedNewDocument { get; }
        public override bool ExplicitLegacyToRemotePadMigrationPerformed { get; }
        public override bool ExplicitRemotePadMobyChoiceMigrationPerformed { get; }
        public override bool ExplicitRemotePadTerrainSetMigrationPerformed { get; }
    }
}

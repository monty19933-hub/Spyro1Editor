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
    public bool ReplacedExistingDocument => !CreatedNewDocument;
    public string DocumentCanonicalSha256 => Document.DocumentCanonicalSha256;
    public string DocumentContentSha256 => Document.DocumentContentSha256;
    public string EnvelopeCanonicalSha256 => Document.EnvelopeCanonicalSha256;

    public bool ExplicitSaveInvocationRequired => true;
    public bool LegacyTerrainLayerConflictChecked => true;
    public bool ExactControlStateCompiled => true;
    public bool ProviderOwnedNativeInputGenerationValidatedDuringWrite => true;
    public bool LockedSourceRevalidatedDuringWrite => true;
    public bool CurrentProjectionRevalidatedDuringWrite => true;
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
/// is deliberately expensive: it validates the locked workspace, loads and
/// validates provider-owned native texture/Moby inputs, reprojects the document,
/// and then uses the Store's compare-and-swap publication. It must not be called from
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(expectedLabManifest);
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        if (state.MandatoryBaseFaceTextureId !=
            Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId)
        {
            throw new NotSupportedException(
                "Resident T25 authoring documents are exact but cannot be persisted until the T25 composite projection contract is frozen.");
        }

        UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(
            expectedLabManifest,
            paths);
        await RequireNoConflictingLegacyTerrainLayerAsync(paths, cancellationToken);
        Id65AuthoringEditorControlStateCompilation compilation =
            Id65AuthoringEditorControlStateCompiler.Compile(state);
        cancellationToken.ThrowIfCancellationRequested();

        Id65AuthoringEditorNativeInputProvider.InputSet nativeInputs =
            await Id65AuthoringEditorNativeInputProvider.LoadAsync(
                paths,
                expectedLabManifest,
                cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await RequireNoConflictingLegacyTerrainLayerAsync(paths, cancellationToken);

        return await Id65AuthoringEditorDocumentStore.SavePreparedAsync(
                paths,
                expectedLabManifest,
                compilation.Document,
                new Id65AuthoringEditorDocumentProjectionInputs(nativeInputs),
                stored => PrepareResult(
                    state,
                    compilation,
                    stored,
                    createdNewDocument:
                        expectedPriorEnvelopeCanonicalSha256 == null),
                expectedPriorEnvelopeCanonicalSha256,
                cancellationToken);
    }

    private static Id65AuthoringEditorDocumentWriteResult PrepareResult(
        Id65AuthoringEditorControlState state,
        Id65AuthoringEditorControlStateCompilation compilation,
        Id65AuthoringEditorDocumentStoreResult stored,
        bool createdNewDocument)
    {
        Id65AuthoringEditorDocumentEnvelopeIdentity identity = new(
            stored.Document,
            stored.EnvelopeCanonicalSha256,
            stored.ProjectionContractId,
            stored.CompositeProfileId,
            stored.CompositeManifestSha256,
            stored.CompositeOutputRow80Sha256,
            stored.CompositeDeterministicPlanSha256);
        Id65AuthoringEditorDocumentReadModel readModel = new(identity);
        Id65AuthoringEditorControlState recovered =
            Id65AuthoringEditorControlStateCompiler.RecoverControlState(readModel);
        RequireEquivalent(state, recovered);

        Id65AuthoringEditorDocumentWriteResult result = new WorkspaceWriteResult(
            recovered,
            readModel,
            createdNewDocument);
        ValidateResult(result, compilation, stored);
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
            expected.IncludeGrassAtT88 != actual.IncludeGrassAtT88 ||
            expected.IncludeGrassAtT107 != actual.IncludeGrassAtT107 ||
            expected.MandatoryBaseFaceTextureId != actual.MandatoryBaseFaceTextureId)
        {
            throw new InvalidDataException(
                "The saved ID65 document did not recover its requested exact control state.");
        }
    }

    private static void ValidateResult(
        Id65AuthoringEditorDocumentWriteResult result,
        Id65AuthoringEditorControlStateCompilation compilation,
        Id65AuthoringEditorDocumentStoreResult stored)
    {
        if (result is not WorkspaceWriteResult ||
            result.DocumentCanonicalSha256 != compilation.DocumentCanonicalSha256 ||
            result.DocumentContentSha256 != compilation.DocumentContentSha256 ||
            result.EnvelopeCanonicalSha256 != stored.EnvelopeCanonicalSha256 ||
            !result.ExplicitSaveInvocationRequired ||
            !result.LegacyTerrainLayerConflictChecked ||
            !result.ExactControlStateCompiled ||
            !result.ProviderOwnedNativeInputGenerationValidatedDuringWrite ||
            !result.LockedSourceRevalidatedDuringWrite ||
            !result.CurrentProjectionRevalidatedDuringWrite ||
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
            bool createdNewDocument)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(document);
            Id65AuthoringEditorControlState recovered =
                Id65AuthoringEditorControlStateCompiler.RecoverControlState(document);
            RequireEquivalent(state, recovered);

            ControlState = new(
                recovered.IncludeStructuralTerrain,
                recovered.IncludeGrassAtT88,
                recovered.IncludeGrassAtT107,
                recovered.MandatoryBaseFaceTextureId);
            Document = document;
            CreatedNewDocument = createdNewDocument;
        }

        public override Id65AuthoringEditorControlState ControlState { get; }
        public override Id65AuthoringEditorDocumentReadModel Document { get; }
        public override bool CreatedNewDocument { get; }
    }
}

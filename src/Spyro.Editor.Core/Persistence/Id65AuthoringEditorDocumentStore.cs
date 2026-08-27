using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Persistence;

/// <summary>
/// Exact, already-validated native inputs needed to re-derive an editor
/// document's projection identity. The store never serializes these inputs.
/// </summary>
internal sealed record Id65AuthoringEditorDocumentProjectionInputs(
    Id65AuthoringEditorNativeInputProvider.InputSet? NativeInputs,
    Id65AuthoringCompositeCompilerLimits? Limits = null);

/// <summary>
/// Strict readback of one persisted editor document and its hash-only
/// projection identity. It contains no compiled output or native payload.
/// </summary>
internal sealed record Id65AuthoringEditorDocumentStoreResult(
    string Path,
    Id65AuthoringEditorDocument Document,
    string EnvelopeCanonicalSha256,
    string ProjectionContractId,
    string CompositeProfileId,
    string CompositeManifestSha256,
    string CompositeOutputRow80Sha256,
    string CompositeDeterministicPlanSha256)
{
    public string DocumentCanonicalSha256 => Document.CanonicalSha256;
    public int EnvelopeSchemaVersion => Document.UsesRemotePadT107Grid
        ? Id65AuthoringEditorDocumentStore.RemotePadGridEnvelopeSchemaVersion
        : Id65AuthoringEditorDocumentStore.EnvelopeSchemaVersion;
    public string StaticAuthoringProfileId { get; init; } = string.Empty;
    public string StaticAuthoringCatalogSha256 { get; init; } = string.Empty;
    public string StaticAuthoringSceneGraphSha256 { get; init; } = string.Empty;
    public string StaticAuthoringProjectionSha256 { get; init; } = string.Empty;
    public bool HasCompiledCompositeProjection => !Document.UsesRemotePadT107Grid;
    public bool HasStaticAuthoringProjection => Document.UsesRemotePadT107Grid;
    public bool ContainsCompositeAfterimage => false;
    public bool ContainsDiscImageBytes => false;
    public bool ContainsCueBytes => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool AppIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool ReleaseIntegrated => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;
    public bool RequiresTrustedExclusiveWorkspaceDirectory => true;
    public bool HostileFilesystemRaceSafe => false;
    public bool CommonWorkspaceLeaseOwned => false;
}

/// <summary>
/// Path-free canonical identity recovered from a persisted editor-document
/// envelope. Unlike a store result, this identity deliberately does not claim
/// that the document was reprojected against the current native inputs.
/// </summary>
internal sealed record Id65AuthoringEditorDocumentEnvelopeIdentity(
    Id65AuthoringEditorDocument Document,
    string EnvelopeCanonicalSha256,
    string ProjectionContractId,
    string CompositeProfileId,
    string CompositeManifestSha256,
    string CompositeOutputRow80Sha256,
    string CompositeDeterministicPlanSha256)
{
    public int EnvelopeSchemaVersion => Document.UsesRemotePadT107Grid
        ? Id65AuthoringEditorDocumentStore.RemotePadGridEnvelopeSchemaVersion
        : Id65AuthoringEditorDocumentStore.EnvelopeSchemaVersion;
    public string StaticAuthoringProfileId { get; init; } = string.Empty;
    public string StaticAuthoringCatalogSha256 { get; init; } = string.Empty;
    public string StaticAuthoringSceneGraphSha256 { get; init; } = string.Empty;
    public string StaticAuthoringProjectionSha256 { get; init; } = string.Empty;
    public bool HasCompiledCompositeProjection => !Document.UsesRemotePadT107Grid;
    public bool HasStaticAuthoringProjection => Document.UsesRemotePadT107Grid;
}

/// <summary>
/// Internal, Core-only persistence boundary for the exact ID65 authoring editor
/// document. It owns one canonical JSON file under the lab's authored-edits
/// directory. It never stores a composite afterimage, native source bytes,
/// BIN/CUE bytes, publisher state, or an authorization flag.
/// </summary>
internal static class Id65AuthoringEditorDocumentStore
{
    public const string EnvelopeFormat =
        "spyro-editor-id65-authoring-editor-document-envelope";
    public const int EnvelopeSchemaVersion = 1;
    public const string ProjectionContractId =
        "id65-authoring-editor-document-projector-v1";
    public const int RemotePadGridEnvelopeSchemaVersion = 2;
    public const string RemotePadGridProjectionContractId =
        "id65-authoring-editor-document-projector-v2";
    public const string RemotePadGridStaticAuthoringProfileId =
        "id65-authoring-editor-document-remote-pad-static-projection-v1";
    public const string FileName =
        "unusedlevel65blank-authoring-document-v1.json";
    public const int MaximumEnvelopeByteCount = 256 * 1024;
    public const bool RequiresTrustedExclusiveWorkspaceDirectory = true;
    public const bool HostileFilesystemRaceSafe = false;
    public const bool CommonWorkspaceLeaseOwned = false;
    internal const string WriterLeaseFileName =
        ".unusedlevel65blank-authoring-document-v1.json.writer.lock";

    private static readonly string[] LegacyEnvelopePropertyNames =
    [
        "format",
        "schemaVersion",
        "workspaceKey",
        "workspaceLayoutVersion",
        "workspaceLayoutRelativeRoot",
        "labProfileId",
        "labProfileVersion",
        "lockedBaseProfileId",
        "lockedBaseImageSha256",
        "documentProfileId",
        "documentCanonicalSha256",
        "projectionContractId",
        "compositeProfileId",
        "compositeManifestSha256",
        "compositeOutputRow80Sha256",
        "compositeDeterministicPlanSha256",
        "envelopeContentSha256",
        "document"
    ];

    private static readonly string[] RemotePadGridEnvelopePropertyNames =
    [
        "format",
        "schemaVersion",
        "workspaceKey",
        "workspaceLayoutVersion",
        "workspaceLayoutRelativeRoot",
        "labProfileId",
        "labProfileVersion",
        "lockedBaseProfileId",
        "lockedBaseImageSha256",
        "documentProfileId",
        "documentCanonicalSha256",
        "projectionContractId",
        "staticAuthoringProfileId",
        "staticAuthoringCatalogSha256",
        "staticAuthoringSceneGraphSha256",
        "staticAuthoringProjectionSha256",
        "envelopeContentSha256",
        "document"
    ];

    public static string GetPath(UnusedLevel65BlankLevelLabWorkspacePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        // CreateManifest performs the code-owned exact workspace-layout check.
        _ = UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(paths);
        string authoredRoot = Path.GetFullPath(paths.AuthoredEditsDirectoryPath);
        string path = Path.GetFullPath(Path.Combine(authoredRoot, FileName));
        PersistencePathSafety.EnsureWritePathWithinRoot(
            paths.RootPath,
            authoredRoot,
            "The ID65 editor-document authored root escapes its versioned lab workspace.");
        PersistencePathSafety.EnsureWritePathWithinRoot(
            authoredRoot,
            path,
            "The ID65 editor-document path escapes its authored-edits root.");
        RefuseSymbolicLink(authoredRoot, "authored-edits directory");
        RefuseSymbolicLink(path, "editor-document destination");
        if (Directory.Exists(path))
            throw new InvalidDataException("The ID65 editor-document destination is a directory.");
        return path;
    }

    public static Task<Id65AuthoringEditorDocumentStoreResult> SaveAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest expectedLabManifest,
        Id65AuthoringEditorDocument document,
        Id65AuthoringEditorDocumentProjectionInputs projectionInputs,
        string? expectedPriorEnvelopeCanonicalSha256,
        CancellationToken cancellationToken = default) =>
        SavePreparedAsync(
            paths,
            expectedLabManifest,
            document,
            projectionInputs,
            static staged => staged,
            expectedPriorEnvelopeCanonicalSha256,
            cancellationToken);

    internal static Task<TPublishedResult> SavePreparedAsync<TPublishedResult>(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest expectedLabManifest,
        Id65AuthoringEditorDocument document,
        Id65AuthoringEditorDocumentProjectionInputs projectionInputs,
        Func<Id65AuthoringEditorDocumentStoreResult, TPublishedResult>
            preparePublishedResult,
        string? expectedPriorEnvelopeCanonicalSha256,
        CancellationToken cancellationToken = default) =>
        SavePreparedCoreAsync(
            paths,
            expectedLabManifest,
            document,
            projectionInputs,
            preparePublishedResult,
            expectedPriorEnvelopeCanonicalSha256,
            DocumentTransitionPolicy.SameSemanticFamilyOnly,
            cancellationToken);

    internal static Task<TPublishedResult> MigratePreparedAsync<TPublishedResult>(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest expectedLabManifest,
        Id65AuthoringEditorDocument document,
        Id65AuthoringEditorDocumentProjectionInputs projectionInputs,
        Func<Id65AuthoringEditorDocumentStoreResult, TPublishedResult>
            preparePublishedResult,
        string expectedPriorEnvelopeCanonicalSha256,
        CancellationToken cancellationToken = default) =>
        SavePreparedCoreAsync(
            paths,
            expectedLabManifest,
            document,
            projectionInputs,
            preparePublishedResult,
            expectedPriorEnvelopeCanonicalSha256,
            DocumentTransitionPolicy.ExplicitLegacyToRemotePadGrid,
            cancellationToken);

    private static async Task<TPublishedResult> SavePreparedCoreAsync<TPublishedResult>(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest expectedLabManifest,
        Id65AuthoringEditorDocument document,
        Id65AuthoringEditorDocumentProjectionInputs projectionInputs,
        Func<Id65AuthoringEditorDocumentStoreResult, TPublishedResult>
            preparePublishedResult,
        string? expectedPriorEnvelopeCanonicalSha256,
        DocumentTransitionPolicy transitionPolicy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(expectedLabManifest);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(projectionInputs);
        ArgumentNullException.ThrowIfNull(preparePublishedResult);
        if (!Enum.IsDefined(transitionPolicy))
            throw new ArgumentOutOfRangeException(nameof(transitionPolicy));
        if (transitionPolicy == DocumentTransitionPolicy.ExplicitLegacyToRemotePadGrid &&
            string.IsNullOrWhiteSpace(expectedPriorEnvelopeCanonicalSha256))
        {
            throw new InvalidDataException(
                "An explicit legacy-to-remote ID65 document migration requires one exact prior envelope identity.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        Id65AuthoringEditorDocument canonicalDocument =
            Id65AuthoringEditorDocument.Parse(document.SerializeUtf8());
        RequireDocumentLabIdentity(canonicalDocument, expectedLabManifest);
        bool requiresLegacyNativeValidation =
            RequiresLegacyNativeValidation(canonicalDocument, transitionPolicy);
        if (requiresLegacyNativeValidation)
            ValidateNativeProjectionInputs(projectionInputs);

        string path = GetPath(paths);
        await RequireExpectedWorkspaceAsync(
            paths,
            expectedLabManifest,
            requiresLegacyNativeValidation,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ProjectionIdentity projection = ProjectAndCaptureIdentity(
            canonicalDocument,
            projectionInputs);
        EnvelopeBytes envelope = BuildEnvelope(
            expectedLabManifest,
            canonicalDocument,
            projection);
        cancellationToken.ThrowIfCancellationRequested();

        string directory = Path.GetDirectoryName(path)
            ?? throw new InvalidDataException("The ID65 editor-document destination has no parent directory.");
        Directory.CreateDirectory(directory);

        // Re-resolve after creation so a raced or pre-existing alias cannot turn
        // the same-directory atomic replace into an external write.
        string resolvedPath = GetPath(paths);
        if (!PathsEqual(path, resolvedPath))
            throw new InvalidDataException("The ID65 editor-document destination changed during preparation.");

        string temporaryPath = Path.Combine(
            directory,
            $".{FileName}.{Guid.NewGuid():N}.tmp");
        PersistencePathSafety.EnsureWritePathWithinRoot(directory, temporaryPath);
        string writerLeasePath = Path.Combine(directory, WriterLeaseFileName);
        PersistencePathSafety.EnsureWritePathWithinRoot(directory, writerLeasePath);
        FileStream? writerLease = null;
        try
        {
            writerLease = AcquireWriterLease(writerLeasePath);
            string leasedPath = GetPath(paths);
            if (!PathsEqual(path, leasedPath))
                throw new InvalidDataException("The ID65 editor-document destination changed while acquiring its writer lease.");
            RecoverOwnedTemporaryResidue(directory, writerLeasePath);
            await RequireExpectedPriorAsync(
                path,
                expectedPriorEnvelopeCanonicalSha256,
                expectedLabManifest,
                projectionInputs,
                canonicalDocument,
                transitionPolicy,
                cancellationToken);
            await WriteThroughAsync(temporaryPath, envelope.CanonicalBytes, cancellationToken);
            byte[] stagedBytes = await ReadEnvelopeBytesAsync(
                temporaryPath,
                "staged editor-document envelope",
                cancellationToken);
            Id65AuthoringEditorDocumentStoreResult staged = ParseAndValidateEnvelope(
                stagedBytes,
                path,
                expectedLabManifest,
                projectionInputs);
            if (!HashEquals(staged.EnvelopeCanonicalSha256, envelope.CanonicalSha256) ||
                !HashEquals(staged.DocumentCanonicalSha256, canonicalDocument.CanonicalSha256))
            {
                throw new InvalidDataException(
                    "The staged ID65 editor-document readback changed before publication.");
            }
            TPublishedResult prepared = preparePublishedResult(staged);
            if (prepared is null)
            {
                throw new InvalidDataException(
                    "The ID65 staged publication result factory returned null.");
            }

            await RequireExpectedWorkspaceAsync(
                paths,
                expectedLabManifest,
                requiresLegacyNativeValidation,
                cancellationToken);
            await RequireExpectedPriorAsync(
                path,
                expectedPriorEnvelopeCanonicalSha256,
                expectedLabManifest,
                projectionInputs,
                canonicalDocument,
                transitionPolicy,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            RefuseSymbolicLink(directory, "authored-edits directory");
            RefuseSymbolicLink(path, "editor-document destination");
            string commitPath = GetPath(paths);
            if (!PathsEqual(path, commitPath))
                throw new InvalidDataException("The ID65 editor-document destination changed before atomic publication.");

            // This is the last fallible authoritative operation. The staged
            // result has already been strictly parsed and projected, so a
            // successful move is reported as success even if best-effort
            // temporary/lease cleanup later encounters an environmental error.
            File.Move(
                temporaryPath,
                path,
                overwrite: expectedPriorEnvelopeCanonicalSha256 != null);
            return prepared;
        }
        finally
        {
            BestEffortDeleteOwnedTemporary(temporaryPath);
            BestEffortDispose(writerLease);
        }
    }

    public static async Task<Id65AuthoringEditorDocumentStoreResult?> LoadAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest expectedLabManifest,
        Id65AuthoringEditorDocumentProjectionInputs projectionInputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(expectedLabManifest);
        ArgumentNullException.ThrowIfNull(projectionInputs);
        cancellationToken.ThrowIfCancellationRequested();

        string path = GetPath(paths);
        await RequireExpectedManifestWorkspaceAsync(
            paths,
            expectedLabManifest,
            cancellationToken);
        if (!File.Exists(path))
        {
            if (projectionInputs.NativeInputs != null)
            {
                ValidateNativeProjectionInputs(projectionInputs);
                await RequireExpectedPublishedWorkspaceAsync(
                    paths,
                    expectedLabManifest,
                    cancellationToken);
            }
            return null;
        }

        RefuseSymbolicLink(path, "editor-document destination");
        byte[] bytes = await ReadEnvelopeBytesAsync(
            path,
            "persisted editor-document envelope",
            cancellationToken);
        Id65AuthoringEditorDocumentEnvelopeIdentity identity =
            ParseAndValidateEnvelopeIdentity(bytes, expectedLabManifest);
        bool requiresLegacyNativeValidation = !identity.Document.UsesRemotePadT107Grid;
        if (requiresLegacyNativeValidation)
        {
            ValidateNativeProjectionInputs(projectionInputs);
            await RequireExpectedPublishedWorkspaceAsync(
                paths,
                expectedLabManifest,
                cancellationToken);
        }
        ValidateProjectionIdentity(identity, projectionInputs);
        Id65AuthoringEditorDocumentStoreResult result =
            CreateStoreResult(path, identity);
        await RequireExpectedWorkspaceAsync(
            paths,
            expectedLabManifest,
            requiresLegacyNativeValidation,
            cancellationToken);
        return result;
    }

    internal static Task<Id65AuthoringEditorDocumentEnvelopeIdentity?>
        InspectEnvelopeIdentityAsync(
            UnusedLevel65BlankLevelLabWorkspacePaths paths,
            UnusedLevel65BlankLevelLabManifest expectedLabManifest,
            CancellationToken cancellationToken = default) =>
        InspectEnvelopeIdentityCoreAsync(
            paths,
            expectedLabManifest,
            afterFirstReadForTesting: null,
            cancellationToken);

    internal static Task<Id65AuthoringEditorDocumentEnvelopeIdentity?>
        InspectEnvelopeIdentityForSmokeAsync(
            UnusedLevel65BlankLevelLabWorkspacePaths paths,
            UnusedLevel65BlankLevelLabManifest expectedLabManifest,
            Func<Task> afterFirstReadForTesting,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(afterFirstReadForTesting);
        return InspectEnvelopeIdentityCoreAsync(
            paths,
            expectedLabManifest,
            afterFirstReadForTesting,
            cancellationToken);
    }

    private static async Task<Id65AuthoringEditorDocumentEnvelopeIdentity?>
        InspectEnvelopeIdentityCoreAsync(
            UnusedLevel65BlankLevelLabWorkspacePaths paths,
            UnusedLevel65BlankLevelLabManifest expectedLabManifest,
            Func<Task>? afterFirstReadForTesting,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(expectedLabManifest);
        cancellationToken.ThrowIfCancellationRequested();

        string path = GetPath(paths);
        await RequireExpectedManifestWorkspaceAsync(
            paths,
            expectedLabManifest,
            cancellationToken);
        if (!ExactPathEntryExists(path, "editor-document destination"))
        {
            await RequireExpectedManifestWorkspaceAsync(
                paths,
                expectedLabManifest,
                cancellationToken);
            string absentPath = GetPath(paths);
            if (!PathsEqual(path, absentPath) ||
                ExactPathEntryExists(
                    absentPath,
                    "repeated absent editor-document destination"))
            {
                throw new InvalidDataException(
                    "The ID65 editor-document destination changed while its absence was being inspected.");
            }
            return null;
        }

        RefuseSymbolicLink(path, "editor-document destination");
        if (!File.Exists(path))
        {
            throw new InvalidDataException(
                "The ID65 editor-document destination must be one regular file.");
        }
        byte[] firstBytes = await ReadEnvelopeBytesAsync(
            path,
            "persisted editor-document envelope inspection",
            cancellationToken);
        Id65AuthoringEditorDocumentEnvelopeIdentity first =
            ParseAndValidateEnvelopeIdentity(
                firstBytes,
                expectedLabManifest);
        bool requiresLegacyNativeValidation = !first.Document.UsesRemotePadT107Grid;
        await RequireExpectedWorkspaceAsync(
            paths,
            expectedLabManifest,
            requiresLegacyNativeValidation,
            cancellationToken);

        if (afterFirstReadForTesting != null)
            await afterFirstReadForTesting();
        cancellationToken.ThrowIfCancellationRequested();

        await RequireExpectedWorkspaceAsync(
            paths,
            expectedLabManifest,
            requiresLegacyNativeValidation,
            cancellationToken);
        string repeatedPath = GetPath(paths);
        if (!PathsEqual(path, repeatedPath) || !File.Exists(repeatedPath))
        {
            throw new InvalidDataException(
                "The ID65 editor-document destination changed before deterministic inspection readback.");
        }
        RefuseSymbolicLink(repeatedPath, "editor-document destination");
        byte[] repeatedBytes = await ReadEnvelopeBytesAsync(
            repeatedPath,
            "repeated editor-document envelope inspection",
            cancellationToken);
        Id65AuthoringEditorDocumentEnvelopeIdentity repeated =
            ParseAndValidateEnvelopeIdentity(
                repeatedBytes,
                expectedLabManifest);
        if (!EnvelopeIdentitiesEqual(first, repeated) ||
            !firstBytes.AsSpan().SequenceEqual(repeatedBytes))
        {
            throw new InvalidDataException(
                "The persisted ID65 editor-document envelope changed during deterministic inspection readback.");
        }

        await RequireExpectedWorkspaceAsync(
            paths,
            expectedLabManifest,
            requiresLegacyNativeValidation,
            cancellationToken);
        string finalPath = GetPath(paths);
        if (!PathsEqual(path, finalPath) || !File.Exists(finalPath))
        {
            throw new InvalidDataException(
                "The ID65 editor-document destination changed after deterministic inspection readback.");
        }
        return repeated;
    }

    private static bool EnvelopeIdentitiesEqual(
        Id65AuthoringEditorDocumentEnvelopeIdentity left,
        Id65AuthoringEditorDocumentEnvelopeIdentity right) =>
        left.EnvelopeCanonicalSha256 == right.EnvelopeCanonicalSha256 &&
        left.ProjectionContractId == right.ProjectionContractId &&
        left.CompositeProfileId == right.CompositeProfileId &&
        left.CompositeManifestSha256 == right.CompositeManifestSha256 &&
        left.CompositeOutputRow80Sha256 == right.CompositeOutputRow80Sha256 &&
        left.CompositeDeterministicPlanSha256 == right.CompositeDeterministicPlanSha256 &&
        left.StaticAuthoringProfileId == right.StaticAuthoringProfileId &&
        left.StaticAuthoringCatalogSha256 == right.StaticAuthoringCatalogSha256 &&
        left.StaticAuthoringSceneGraphSha256 == right.StaticAuthoringSceneGraphSha256 &&
        left.StaticAuthoringProjectionSha256 == right.StaticAuthoringProjectionSha256 &&
        left.Document.SerializeUtf8().SequenceEqual(right.Document.SerializeUtf8());

    private static async Task RequireExpectedPriorAsync(
        string path,
        string? expectedPriorEnvelopeCanonicalSha256,
        UnusedLevel65BlankLevelLabManifest expectedLabManifest,
        Id65AuthoringEditorDocumentProjectionInputs projectionInputs,
        Id65AuthoringEditorDocument desiredDocument,
        DocumentTransitionPolicy transitionPolicy,
        CancellationToken cancellationToken)
    {
        if (expectedPriorEnvelopeCanonicalSha256 == null)
        {
            if (File.Exists(path))
            {
                throw new InvalidDataException(
                    "The ID65 editor-document destination already exists; an exact prior envelope identity is required for replacement.");
            }
            return;
        }

        if (!IsSha256(expectedPriorEnvelopeCanonicalSha256))
        {
            throw new InvalidDataException(
                "The expected prior ID65 editor-document envelope identity is not one SHA-256.");
        }
        if (!File.Exists(path))
        {
            throw new InvalidDataException(
                "The expected prior ID65 editor-document envelope is missing.");
        }

        RefuseSymbolicLink(path, "editor-document destination");
        byte[] priorBytes = await ReadEnvelopeBytesAsync(
            path,
            "prior editor-document envelope",
            cancellationToken);
        Id65AuthoringEditorDocumentEnvelopeIdentity prior =
            ParseAndValidateEnvelopeIdentity(priorBytes, expectedLabManifest);
        if (!HashEquals(
                prior.EnvelopeCanonicalSha256,
                expectedPriorEnvelopeCanonicalSha256))
        {
            throw new InvalidDataException(
                "The persisted ID65 editor document changed after its prior envelope identity was captured.");
        }
        RequireDocumentTransition(prior.Document, desiredDocument, transitionPolicy);
        ValidateProjectionIdentity(prior, projectionInputs);
    }

    private static void RequireDocumentTransition(
        Id65AuthoringEditorDocument prior,
        Id65AuthoringEditorDocument desired,
        DocumentTransitionPolicy transitionPolicy)
    {
        bool priorRemote = prior.UsesRemotePadT107Grid;
        bool desiredRemote = desired.UsesRemotePadT107Grid;
        if (transitionPolicy == DocumentTransitionPolicy.SameSemanticFamilyOnly)
        {
            if (priorRemote != desiredRemote)
            {
                throw new InvalidDataException(
                    "Changing between legacy native-apron and schema-3 remote-pad ID65 placement semantics requires the explicit migration API.");
            }
            return;
        }

        if (transitionPolicy != DocumentTransitionPolicy.ExplicitLegacyToRemotePadGrid ||
            priorRemote || !desiredRemote)
        {
            throw new InvalidDataException(
                "The explicit ID65 document migration supports only legacy native-apron to schema-3 remote-pad semantics.");
        }

        int[] priorTargets = prior.MobyPlacements
            .Select(placement => placement.TargetTrueIndex)
            .Order()
            .ToArray();
        int[] desiredTargets = desired.MobyPlacements
            .Select(placement => placement.TargetTrueIndex)
            .Order()
            .ToArray();
        if (prior.Terrain.Count != desired.Terrain.Count ||
            prior.MandatoryBaseFaceTextureId != desired.MandatoryBaseFaceTextureId ||
            !priorTargets.SequenceEqual(desiredTargets))
        {
            throw new InvalidDataException(
                "The explicit ID65 remote-pad migration must preserve terrain, T88/T107 inclusion, and mandatory texture selection.");
        }
    }

    private static void RecoverOwnedTemporaryResidue(
        string directory,
        string writerLeasePath)
    {
        string reservedPrefix = $".{FileName}.";
        string[] entries = Directory
            .EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(entry => Path.GetFileName(entry).StartsWith(
                reservedPrefix,
                StringComparison.Ordinal))
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToArray();
        foreach (string entry in entries)
        {
            if (PathsEqual(entry, writerLeasePath))
                continue;

            string name = Path.GetFileName(entry);
            if (!IsExactOwnedTemporaryName(name))
            {
                throw new InvalidDataException(
                    $"The ID65 editor-document authored root contains an unknown reserved entry '{name}'.");
            }
            RefuseSymbolicLink(entry, "stale owned temporary");
            if (Directory.Exists(entry) || !File.Exists(entry))
            {
                throw new InvalidDataException(
                    $"The ID65 editor-document stale owned temporary is not a regular file: {name}");
            }
            try
            {
                File.Delete(entry);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new InvalidDataException(
                    $"The ID65 editor-document stale owned temporary could not be removed: {name}",
                    exception);
            }
            if (File.Exists(entry) || Directory.Exists(entry))
            {
                throw new InvalidDataException(
                    $"The ID65 editor-document stale owned temporary survived recovery: {name}");
            }
        }
    }

    private static bool IsExactOwnedTemporaryName(string name)
    {
        string prefix = $".{FileName}.";
        const string suffix = ".tmp";
        if (!name.StartsWith(prefix, StringComparison.Ordinal) ||
            !name.EndsWith(suffix, StringComparison.Ordinal) ||
            name.Length != prefix.Length + 32 + suffix.Length)
        {
            return false;
        }
        ReadOnlySpan<char> identity = name.AsSpan(prefix.Length, 32);
        foreach (char character in identity)
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
                return false;
        }
        return true;
    }

    private static FileStream AcquireWriterLease(string writerLeasePath)
    {
        RefuseSymbolicLink(writerLeasePath, "writer lease");
        if (Directory.Exists(writerLeasePath))
            throw new InvalidDataException("The ID65 editor-document writer lease is a directory.");
        try
        {
            FileStream lease = new(
                writerLeasePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough);
            try
            {
                RefuseSymbolicLink(writerLeasePath, "writer lease");
                if (Directory.Exists(writerLeasePath))
                    throw new InvalidDataException("The ID65 editor-document writer lease is a directory.");
                return lease;
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                "Another active ID65 editor-document writer lease blocks this save.",
                exception);
        }
    }

    private static async Task RequireExpectedPublishedWorkspaceAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest expectedLabManifest,
        CancellationToken cancellationToken)
    {
        UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(
            expectedLabManifest,
            paths);
        UnusedLevel65BlankLevelLabManifest published =
            await UnusedLevel65BlankLevelLabBootstrapper.ValidatePublishedWorkspaceReadOnlyAsync(
                paths,
                cancellationToken);
        if (published != expectedLabManifest ||
            published.WorkspaceKey != UnusedLevel65BlankLevelLabProfileRegistry.Key ||
            published.WorkspaceLayoutVersion !=
                UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutVersion ||
            published.WorkspaceLayoutRelativeRoot !=
                UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutRelativeRoot ||
            published.ProfileId != UnusedLevel65BlankLevelLabProfileRegistry.ProfileId ||
            published.ProfileVersion != UnusedLevel65BlankLevelLabProfileRegistry.ProfileVersion ||
            published.LockedBaseProfileId !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseProfileId ||
            !HashEquals(
                published.LockedBaseImageSha256,
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseImageSha256) ||
            published.PromotionAuthorized ||
            published.NormalCreateBinEnabled)
        {
            throw new InvalidDataException(
                "The ID65 editor-document store is not bound to the exact validated lab workspace/base identity.");
        }
    }

    private static Task RequireExpectedWorkspaceAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest expectedLabManifest,
        bool requiresLegacyNativeValidation,
        CancellationToken cancellationToken) =>
        requiresLegacyNativeValidation
            ? RequireExpectedPublishedWorkspaceAsync(
                paths,
                expectedLabManifest,
                cancellationToken)
            : RequireExpectedManifestWorkspaceAsync(
                paths,
                expectedLabManifest,
                cancellationToken);

    private static async Task RequireExpectedManifestWorkspaceAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest expectedLabManifest,
        CancellationToken cancellationToken)
    {
        UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(
            expectedLabManifest,
            paths);
        PersistencePathSafety.EnsureWritePathWithinRoot(
            paths.RootPath,
            paths.ManifestPath,
            "The ID65 lab manifest path escapes its exact workspace root.");
        RefuseSymbolicLink(paths.RootPath, "lab workspace root");
        RefuseSymbolicLink(paths.ManifestPath, "lab workspace manifest");
        if (!File.Exists(paths.ManifestPath))
            throw new InvalidDataException("The ID65 lab workspace manifest is missing.");

        UnusedLevel65BlankLevelLabManifest published =
            await UnusedLevel65BlankLevelLabBootstrapper.ReadAndValidateManifestAsync(
                paths.ManifestPath,
                paths,
                cancellationToken);
        if (published != expectedLabManifest ||
            published.WorkspaceKey != UnusedLevel65BlankLevelLabProfileRegistry.Key ||
            published.WorkspaceLayoutVersion !=
                UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutVersion ||
            published.WorkspaceLayoutRelativeRoot !=
                UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutRelativeRoot ||
            published.ProfileId != UnusedLevel65BlankLevelLabProfileRegistry.ProfileId ||
            published.ProfileVersion !=
                UnusedLevel65BlankLevelLabProfileRegistry.ProfileVersion ||
            published.LockedBaseProfileId !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseProfileId ||
            !HashEquals(
                published.LockedBaseImageSha256,
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseImageSha256) ||
            published.PromotionAuthorized ||
            published.NormalCreateBinEnabled)
        {
            throw new InvalidDataException(
                "The static ID65 editor-document store is not bound to the exact unpromoted lab manifest identity.");
        }
    }

    private static bool RequiresLegacyNativeValidation(
        Id65AuthoringEditorDocument document,
        DocumentTransitionPolicy transitionPolicy) =>
        !document.UsesRemotePadT107Grid ||
        transitionPolicy == DocumentTransitionPolicy.ExplicitLegacyToRemotePadGrid;

    private static void ValidateNativeProjectionInputs(
        Id65AuthoringEditorDocumentProjectionInputs projectionInputs)
    {
        Id65AuthoringEditorNativeInputProvider.InputSet nativeInputs =
            projectionInputs.NativeInputs ?? throw new InvalidDataException(
                "Legacy ID65 persistence requires exact provider-owned native inputs.");
        Id65AuthoringEditorNativeInputProvider.ValidateForConsumption(nativeInputs);
    }

    private static ProjectionIdentity ProjectAndCaptureIdentity(
        Id65AuthoringEditorDocument document,
        Id65AuthoringEditorDocumentProjectionInputs inputs)
    {
        if (document.UsesRemotePadT107Grid)
            return CaptureRemotePadStaticAuthoringIdentity(document);

        Id65AuthoringEditorNativeInputProvider.InputSet nativeInputs =
            inputs.NativeInputs ?? throw new InvalidDataException(
                "Legacy ID65 document projection requires exact provider-owned native inputs.");
        Id65AuthoringEditorNativeInputProvider.ValidateForConsumption(nativeInputs);

        Id65AuthoringEditorProjection projection =
            Id65AuthoringEditorDocumentProjector.Project(new(
                document,
                nativeInputs.LockedSource,
                nativeInputs.PrivateTextureWitness,
                nativeInputs.GrassBundle,
                inputs.Limits));
        Id65CompiledAuthoringComposite compiled = projection.Compiled;
        if (!HashEquals(projection.DocumentCanonicalSha256, document.CanonicalSha256) ||
            !projection.Document.SerializeUtf8().SequenceEqual(document.SerializeUtf8()) ||
            !projection.DocumentIdentityVerified ||
            !projection.TerrainHandlesVerified ||
            !projection.CollisionMembershipVerified ||
            !projection.OcclusionMembershipVerified ||
            !projection.PrivateTextureBindingVerified ||
            !projection.MobyPlacementBindingVerified ||
            !projection.BaselineOwnershipHandlesVerified ||
            !projection.SemanticReadbackVerified ||
            !projection.ExactFactoriesOnly ||
            projection.WritesFileSystem ||
            projection.WritesDiscImage ||
            projection.WritesCue ||
            projection.AppIntegrated ||
            projection.CreateBinEnabled ||
            projection.NormalCreateBinEnabled ||
            projection.RuntimeCandidateAuthorized ||
            projection.ReleaseIntegrated ||
            projection.PromotionAuthorized ||
            projection.Publishable ||
            compiled.ProfileId != Id65AuthoringCompositeCompiler.ProfileId ||
            !HashEquals(compiled.SourceRow80Sha256, document.LockedRow80Sha256) ||
            !HashEquals(compiled.ManifestSha256, compiled.Manifest.CanonicalSha256) ||
            !HashEquals(compiled.TargetOverlaySha256,
                Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256) ||
            !HashEquals(compiled.GlobalExecutableSha256,
                Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256) ||
            !IsSha256(compiled.ManifestSha256) ||
            !IsSha256(compiled.OutputRow80Sha256) ||
            !IsSha256(compiled.DeterministicPlanSha256) ||
            compiled.WritesFileSystem ||
            compiled.WritesDiscImage ||
            compiled.WritesCue ||
            compiled.AppIntegrated ||
            compiled.CreateBinEnabled ||
            compiled.NormalCreateBinEnabled ||
            compiled.RuntimeCandidateAuthorized ||
            compiled.ReleaseIntegrated ||
            compiled.PromotionAuthorized ||
            compiled.Publishable)
        {
            throw new InvalidDataException(
                "The ID65 editor-document projection identity is incomplete, stale, or production-authorized.");
        }

        return new ProjectionIdentity(
            compiled.ProfileId,
            compiled.ManifestSha256,
            compiled.OutputRow80Sha256,
            compiled.DeterministicPlanSha256);
    }

    private static ProjectionIdentity CaptureRemotePadStaticAuthoringIdentity(
        Id65AuthoringEditorDocument document)
    {
        if (!document.UsesRemotePadT107Grid ||
            document.SchemaVersion != Id65AuthoringEditorDocument.RemotePadGridSchemaVersion ||
            document.ProfileId != Id65AuthoringEditorDocument.RemotePadGridDocumentProfileId ||
            document.T107RemotePadPositionId is not string positionId)
        {
            throw new InvalidDataException(
                "The static ID65 authoring projector requires one exact schema-3 remote-pad document.");
        }

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
            _ => throw new InvalidDataException(
                "The static ID65 authoring projector requires exact T25 or T66 scene support.")
        };
        if (document.Terrain.Count is < 0 or > 1 ||
            Id65RemotePadGrassT107Catalog.CanonicalSha256 !=
                Id65RemotePadGrassT107Catalog.ExpectedCanonicalSha256 ||
            Id65RemotePadGrassT107Catalog.SupportHandle !=
                Id65FreshLevelSceneProjector.CollisionStableId ||
            Id65RemotePadGrassT107Catalog.SupportCollisionTrueIndex !=
                Id65FreshLevelSceneProjector.CollisionTriangleIndex ||
            !Id65RemotePadGrassT107Catalog.IsStrictlyInsideSupportTriangle(
                position.EditorX,
                position.EditorY,
                position.EditorZ))
        {
            throw new InvalidDataException(
                "The schema-3 ID65 document lost its exact catalog or fresh-scene T13995 support binding.");
        }

        byte[] canonical = BuildRemotePadStaticAuthoringProjection(
            document,
            position,
            sceneGraphSha256);
        return new ProjectionIdentity(string.Empty, string.Empty, string.Empty, string.Empty)
        {
            StaticAuthoringProfileId = RemotePadGridStaticAuthoringProfileId,
            StaticAuthoringCatalogSha256 = Id65RemotePadGrassT107Catalog.CanonicalSha256,
            StaticAuthoringSceneGraphSha256 = sceneGraphSha256,
            StaticAuthoringProjectionSha256 = Hash(canonical)
        };
    }

    private static byte[] BuildRemotePadStaticAuthoringProjection(
        Id65AuthoringEditorDocument document,
        Id65RemotePadGrassT107Position position,
        string sceneGraphSha256)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter json = new(buffer, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false
        }))
        {
            json.WriteStartObject();
            json.WriteString("profileId", RemotePadGridStaticAuthoringProfileId);
            json.WriteNumber("schemaVersion", 1);
            json.WriteString("projectionContractId", RemotePadGridProjectionContractId);
            json.WriteString("documentProfileId", document.ProfileId);
            json.WriteString("documentCanonicalSha256", document.CanonicalSha256);
            json.WriteString("documentContentSha256", document.ContentSha256);
            json.WriteString("catalogProfileId", Id65RemotePadGrassT107Catalog.ProfileId);
            json.WriteString("catalogCanonicalSha256", Id65RemotePadGrassT107Catalog.CanonicalSha256);
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
            json.WriteNumber("mandatoryBaseFaceTextureId", document.MandatoryBaseFaceTextureId);
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
        return buffer.ToArray();
    }

    private static void RequireDocumentLabIdentity(
        Id65AuthoringEditorDocument document,
        UnusedLevel65BlankLevelLabManifest expectedLabManifest)
    {
        bool supportedDocument =
            (document.SchemaVersion == Id65AuthoringEditorDocument.CurrentSchemaVersion &&
             document.ProfileId == Id65AuthoringEditorDocument.DocumentProfileId &&
             document.MandatoryBaseFaceTextureId == 66 &&
             !document.UsesRemotePadT107Grid) ||
            (document.SchemaVersion == Id65AuthoringEditorDocument.ResidentT25SchemaVersion &&
             document.ProfileId == Id65AuthoringEditorDocument.ResidentT25DocumentProfileId &&
             document.MandatoryBaseFaceTextureId == 25 &&
             !document.UsesRemotePadT107Grid) ||
            (document.SchemaVersion == Id65AuthoringEditorDocument.RemotePadGridSchemaVersion &&
             document.ProfileId == Id65AuthoringEditorDocument.RemotePadGridDocumentProfileId &&
             document.MandatoryBaseFaceTextureId is 25 or 66 &&
             document.UsesRemotePadT107Grid);
        if (!supportedDocument ||
            document.LabProfileId != expectedLabManifest.ProfileId ||
            document.LabProfileVersion != expectedLabManifest.ProfileVersion ||
            document.WorkspaceKey != expectedLabManifest.WorkspaceKey ||
            document.WorkspaceLayoutVersion != expectedLabManifest.WorkspaceLayoutVersion ||
            document.WorkspaceLayoutRelativeRoot != expectedLabManifest.WorkspaceLayoutRelativeRoot ||
            document.LockedBaseProfileId != expectedLabManifest.LockedBaseProfileId ||
            !HashEquals(document.LockedBaseImageSha256, expectedLabManifest.LockedBaseImageSha256) ||
            !HashEquals(
                document.LockedRow80Sha256,
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256) ||
            document.CompositeProfileId != Id65AuthoringCompositeCompiler.ProfileId ||
            !HashEquals(
                document.PrivateTextureWitnessSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256) ||
            !HashEquals(
                document.MobyBundleSha256,
                Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256) ||
            !IsSha256(document.ContentSha256) ||
            !IsSha256(document.CanonicalSha256) ||
            document.ContainsNativePayloadBytes ||
            document.ContainsPhysicalAddresses ||
            document.ContainsFilePaths ||
            document.ContainsCompiledAfterimage ||
            document.ContainsPublisherState ||
            document.WritesFileSystem ||
            document.WritesDiscImage ||
            document.WritesCue ||
            document.AppIntegrated ||
            document.CreateBinEnabled ||
            document.NormalCreateBinEnabled ||
            document.RuntimeCandidateAuthorized ||
            document.ReleaseIntegrated ||
            document.PromotionAuthorized ||
            document.Publishable)
        {
            throw new InvalidDataException(
                "The ID65 editor document is not bound to the exact non-production lab/base/composite identity.");
        }
    }

    private static EnvelopeBytes BuildEnvelope(
        UnusedLevel65BlankLevelLabManifest labManifest,
        Id65AuthoringEditorDocument document,
        ProjectionIdentity projection)
    {
        byte[] contentBytes = WriteEnvelope(
            labManifest,
            document,
            projection,
            envelopeContentSha256: null);
        string contentSha256 = Hash(contentBytes);
        byte[] canonicalBytes = WriteEnvelope(
            labManifest,
            document,
            projection,
            contentSha256);
        return new EnvelopeBytes(canonicalBytes, contentSha256, Hash(canonicalBytes));
    }

    private static byte[] WriteEnvelope(
        UnusedLevel65BlankLevelLabManifest labManifest,
        Id65AuthoringEditorDocument document,
        ProjectionIdentity projection,
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
            json.WriteString("format", EnvelopeFormat);
            json.WriteNumber("schemaVersion", GetEnvelopeSchemaVersion(document));
            json.WriteString("workspaceKey", labManifest.WorkspaceKey);
            json.WriteNumber("workspaceLayoutVersion", labManifest.WorkspaceLayoutVersion);
            json.WriteString("workspaceLayoutRelativeRoot", labManifest.WorkspaceLayoutRelativeRoot);
            json.WriteString("labProfileId", labManifest.ProfileId);
            json.WriteNumber("labProfileVersion", labManifest.ProfileVersion);
            json.WriteString("lockedBaseProfileId", labManifest.LockedBaseProfileId);
            json.WriteString("lockedBaseImageSha256", labManifest.LockedBaseImageSha256);
            json.WriteString("documentProfileId", document.ProfileId);
            json.WriteString("documentCanonicalSha256", document.CanonicalSha256);
            json.WriteString("projectionContractId", GetProjectionContractId(document));
            if (document.UsesRemotePadT107Grid)
            {
                json.WriteString("staticAuthoringProfileId", projection.StaticAuthoringProfileId);
                json.WriteString(
                    "staticAuthoringCatalogSha256",
                    projection.StaticAuthoringCatalogSha256);
                json.WriteString(
                    "staticAuthoringSceneGraphSha256",
                    projection.StaticAuthoringSceneGraphSha256);
                json.WriteString(
                    "staticAuthoringProjectionSha256",
                    projection.StaticAuthoringProjectionSha256);
            }
            else
            {
                json.WriteString("compositeProfileId", projection.CompositeProfileId);
                json.WriteString("compositeManifestSha256", projection.CompositeManifestSha256);
                json.WriteString("compositeOutputRow80Sha256", projection.CompositeOutputRow80Sha256);
                json.WriteString(
                    "compositeDeterministicPlanSha256",
                    projection.CompositeDeterministicPlanSha256);
            }
            if (envelopeContentSha256 != null)
                json.WriteString("envelopeContentSha256", envelopeContentSha256);
            json.WritePropertyName("document");
            using (JsonDocument parsedDocument = JsonDocument.Parse(document.SerializeUtf8()))
                parsedDocument.RootElement.WriteTo(json);
            json.WriteEndObject();
        }
        return buffer.ToArray();
    }

    private static Id65AuthoringEditorDocumentStoreResult ParseAndValidateEnvelope(
        ReadOnlySpan<byte> bytes,
        string path,
        UnusedLevel65BlankLevelLabManifest expectedLabManifest,
        Id65AuthoringEditorDocumentProjectionInputs projectionInputs)
    {
        Id65AuthoringEditorDocumentEnvelopeIdentity identity =
            ParseAndValidateEnvelopeIdentity(bytes, expectedLabManifest);
        ValidateProjectionIdentity(identity, projectionInputs);
        return CreateStoreResult(path, identity);
    }

    private static void ValidateProjectionIdentity(
        Id65AuthoringEditorDocumentEnvelopeIdentity identity,
        Id65AuthoringEditorDocumentProjectionInputs projectionInputs)
    {
        ProjectionIdentity projected = ProjectAndCaptureIdentity(
            identity.Document,
            projectionInputs);
        ProjectionIdentity persisted = CaptureProjectionIdentity(identity);
        if (!ProjectionIdentitiesEqual(projected, persisted))
        {
            throw new InvalidDataException(
                "The persisted ID65 editor-document projection identity does not match current semantic projection.");
        }
    }

    private static Id65AuthoringEditorDocumentStoreResult CreateStoreResult(
        string path,
        Id65AuthoringEditorDocumentEnvelopeIdentity identity) =>
        new(
            path,
            identity.Document,
            identity.EnvelopeCanonicalSha256,
            identity.ProjectionContractId,
            identity.CompositeProfileId,
            identity.CompositeManifestSha256,
            identity.CompositeOutputRow80Sha256,
            identity.CompositeDeterministicPlanSha256)
        {
            StaticAuthoringProfileId = identity.StaticAuthoringProfileId,
            StaticAuthoringCatalogSha256 = identity.StaticAuthoringCatalogSha256,
            StaticAuthoringSceneGraphSha256 = identity.StaticAuthoringSceneGraphSha256,
            StaticAuthoringProjectionSha256 = identity.StaticAuthoringProjectionSha256
        };

    private static Id65AuthoringEditorDocumentEnvelopeIdentity
        ParseAndValidateEnvelopeIdentity(
            ReadOnlySpan<byte> bytes,
            UnusedLevel65BlankLevelLabManifest expectedLabManifest)
    {
        if (bytes.IsEmpty)
            throw new InvalidDataException("The persisted ID65 editor-document envelope is empty.");
        if (bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
            throw new InvalidDataException("The ID65 editor-document envelope must be UTF-8 without a BOM.");

        try
        {
            using JsonDocument parsed = JsonDocument.Parse(
                bytes.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 64
                });
            JsonElement root = parsed.RootElement;
            int schemaVersion = RequireInt32(root, "schemaVersion");
            bool remotePadGridEnvelope = schemaVersion switch
            {
                EnvelopeSchemaVersion => false,
                RemotePadGridEnvelopeSchemaVersion => true,
                _ => throw new InvalidDataException(
                    "The ID65 editor-document envelope schema version is unsupported.")
            };
            RequireExactProperties(
                root,
                remotePadGridEnvelope
                    ? RemotePadGridEnvelopePropertyNames
                    : LegacyEnvelopePropertyNames);

            string format = RequireString(root, "format");
            string workspaceKey = RequireString(root, "workspaceKey");
            int workspaceLayoutVersion = RequireInt32(root, "workspaceLayoutVersion");
            string workspaceLayoutRelativeRoot = RequireString(
                root,
                "workspaceLayoutRelativeRoot");
            string labProfileId = RequireString(root, "labProfileId");
            int labProfileVersion = RequireInt32(root, "labProfileVersion");
            string lockedBaseProfileId = RequireString(root, "lockedBaseProfileId");
            string lockedBaseImageSha256 = RequireString(root, "lockedBaseImageSha256");
            string documentProfileId = RequireString(root, "documentProfileId");
            string documentCanonicalSha256 = RequireString(root, "documentCanonicalSha256");
            string projectionContractId = RequireString(root, "projectionContractId");
            string compositeProfileId = remotePadGridEnvelope
                ? string.Empty
                : RequireString(root, "compositeProfileId");
            string compositeManifestSha256 = remotePadGridEnvelope
                ? string.Empty
                : RequireString(root, "compositeManifestSha256");
            string compositeOutputRow80Sha256 = remotePadGridEnvelope
                ? string.Empty
                : RequireString(root, "compositeOutputRow80Sha256");
            string compositeDeterministicPlanSha256 = remotePadGridEnvelope
                ? string.Empty
                : RequireString(root, "compositeDeterministicPlanSha256");
            string staticAuthoringProfileId = remotePadGridEnvelope
                ? RequireString(root, "staticAuthoringProfileId")
                : string.Empty;
            string staticAuthoringCatalogSha256 = remotePadGridEnvelope
                ? RequireString(root, "staticAuthoringCatalogSha256")
                : string.Empty;
            string staticAuthoringSceneGraphSha256 = remotePadGridEnvelope
                ? RequireString(root, "staticAuthoringSceneGraphSha256")
                : string.Empty;
            string staticAuthoringProjectionSha256 = remotePadGridEnvelope
                ? RequireString(root, "staticAuthoringProjectionSha256")
                : string.Empty;
            string envelopeContentSha256 = RequireString(root, "envelopeContentSha256");
            JsonElement serializedDocument = RequireProperty(root, "document");
            if (serializedDocument.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("The ID65 editor-document envelope document must be an object.");
            Id65AuthoringEditorDocument document = Id65AuthoringEditorDocument.Parse(
                Encoding.UTF8.GetBytes(serializedDocument.GetRawText()));
            RequireDocumentLabIdentity(document, expectedLabManifest);
            int expectedEnvelopeSchemaVersion = GetEnvelopeSchemaVersion(document);
            string expectedProjectionContractId = GetProjectionContractId(document);

            if (format != EnvelopeFormat ||
                schemaVersion != expectedEnvelopeSchemaVersion ||
                workspaceKey != expectedLabManifest.WorkspaceKey ||
                workspaceLayoutVersion != expectedLabManifest.WorkspaceLayoutVersion ||
                workspaceLayoutRelativeRoot != expectedLabManifest.WorkspaceLayoutRelativeRoot ||
                labProfileId != expectedLabManifest.ProfileId ||
                labProfileVersion != expectedLabManifest.ProfileVersion ||
                lockedBaseProfileId != expectedLabManifest.LockedBaseProfileId ||
                !HashEquals(lockedBaseImageSha256, expectedLabManifest.LockedBaseImageSha256) ||
                documentProfileId != document.ProfileId ||
                !HashEquals(documentCanonicalSha256, document.CanonicalSha256) ||
                projectionContractId != expectedProjectionContractId ||
                !IsSha256(envelopeContentSha256))
            {
                throw new InvalidDataException(
                    "The ID65 editor-document envelope schema/workspace/base/document/projection identity is unsupported or stale.");
            }

            ProjectionIdentity persisted;
            if (remotePadGridEnvelope)
            {
                if (staticAuthoringProfileId != RemotePadGridStaticAuthoringProfileId ||
                    !HashEquals(
                        staticAuthoringCatalogSha256,
                        Id65RemotePadGrassT107Catalog.CanonicalSha256) ||
                    !IsSha256(staticAuthoringSceneGraphSha256) ||
                    !IsSha256(staticAuthoringProjectionSha256))
                {
                    throw new InvalidDataException(
                        "The schema-2 ID65 editor-document envelope static-authoring identity is unsupported or stale.");
                }

                persisted = new(
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty)
                {
                    StaticAuthoringProfileId = staticAuthoringProfileId,
                    StaticAuthoringCatalogSha256 =
                        staticAuthoringCatalogSha256.ToLowerInvariant(),
                    StaticAuthoringSceneGraphSha256 =
                        staticAuthoringSceneGraphSha256.ToLowerInvariant(),
                    StaticAuthoringProjectionSha256 =
                        staticAuthoringProjectionSha256.ToLowerInvariant()
                };

                ProjectionIdentity exactStaticProjection =
                    CaptureRemotePadStaticAuthoringIdentity(document);
                if (!ProjectionIdentitiesEqual(persisted, exactStaticProjection))
                {
                    throw new InvalidDataException(
                        "The schema-2 ID65 editor-document envelope static-authoring identity was forged or is stale.");
                }
            }
            else
            {
                if (compositeProfileId != Id65AuthoringCompositeCompiler.ProfileId ||
                    !IsSha256(compositeManifestSha256) ||
                    !IsSha256(compositeOutputRow80Sha256) ||
                    !IsSha256(compositeDeterministicPlanSha256))
                {
                    throw new InvalidDataException(
                        "The legacy ID65 editor-document envelope composite identity is unsupported or stale.");
                }

                persisted = new(
                    compositeProfileId,
                    compositeManifestSha256.ToLowerInvariant(),
                    compositeOutputRow80Sha256.ToLowerInvariant(),
                    compositeDeterministicPlanSha256.ToLowerInvariant());
            }
            EnvelopeBytes expected = BuildEnvelope(
                expectedLabManifest,
                document,
                persisted);
            if (!HashEquals(envelopeContentSha256, expected.ContentSha256) ||
                !bytes.SequenceEqual(expected.CanonicalBytes))
            {
                throw new InvalidDataException(
                    "The persisted ID65 editor-document envelope is not exact canonical JSON or its content hash is invalid.");
            }

            return new Id65AuthoringEditorDocumentEnvelopeIdentity(
                document,
                expected.CanonicalSha256,
                expectedProjectionContractId,
                persisted.CompositeProfileId,
                persisted.CompositeManifestSha256,
                persisted.CompositeOutputRow80Sha256,
                persisted.CompositeDeterministicPlanSha256)
            {
                StaticAuthoringProfileId = persisted.StaticAuthoringProfileId,
                StaticAuthoringCatalogSha256 = persisted.StaticAuthoringCatalogSha256,
                StaticAuthoringSceneGraphSha256 = persisted.StaticAuthoringSceneGraphSha256,
                StaticAuthoringProjectionSha256 = persisted.StaticAuthoringProjectionSha256
            };
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is
            JsonException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new InvalidDataException(
                "The persisted ID65 editor-document envelope is invalid.",
                exception);
        }
    }

    private static async Task<byte[]> ReadEnvelopeBytesAsync(
        string path,
        string role,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        long length = stream.Length;
        if (length <= 0)
            throw new InvalidDataException($"The ID65 {role} is empty.");
        if (length > MaximumEnvelopeByteCount)
        {
            throw new InvalidDataException(
                $"The ID65 {role} exceeds the {MaximumEnvelopeByteCount}-byte persistence limit.");
        }

        byte[] bytes = GC.AllocateUninitializedArray<byte>((int)length);
        int offset = 0;
        while (offset < bytes.Length)
        {
            int read = await stream.ReadAsync(bytes.AsMemory(offset), cancellationToken);
            if (read == 0)
                throw new InvalidDataException($"The ID65 {role} changed while it was being read.");
            offset += read;
        }
        if (stream.ReadByte() != -1 || stream.Length != length)
            throw new InvalidDataException($"The ID65 {role} changed while it was being read.");
        return bytes;
    }

    private static async Task WriteThroughAsync(
        string temporaryPath,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    private static void BestEffortDeleteOwnedTemporary(string temporaryPath)
    {
        try
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
        catch
        {
            // Publication outcome must not become ambiguous because cleanup
            // failed. A later smoke/audit reports any owned temporary residue.
        }
    }

    private static void BestEffortDispose(FileStream? stream)
    {
        try
        {
            stream?.Dispose();
        }
        catch
        {
            // A successful atomic publication remains successful even if the
            // host reports a late close/flush error on the advisory lease.
        }
    }

    private static void RequireExactProperties(
        JsonElement value,
        IEnumerable<string> expectedNames)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The ID65 editor-document envelope root must be an object.");
        HashSet<string> expected = expectedNames.ToHashSet(StringComparer.Ordinal);
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                throw new InvalidDataException(
                    $"The ID65 editor-document envelope contains duplicate field '{property.Name}'.");
            }
            if (!expected.Contains(property.Name))
            {
                throw new InvalidDataException(
                    $"The ID65 editor-document envelope contains unknown field '{property.Name}'.");
            }
        }
        string? missing = expected.FirstOrDefault(name => !seen.Contains(name));
        if (missing != null)
        {
            throw new InvalidDataException(
                $"The ID65 editor-document envelope is missing field '{missing}'.");
        }
    }

    private static JsonElement RequireProperty(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out JsonElement value))
            throw new InvalidDataException($"The ID65 editor-document envelope is missing field '{name}'.");
        return value;
    }

    private static string RequireString(JsonElement owner, string name)
    {
        JsonElement value = RequireProperty(owner, name);
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"The ID65 editor-document envelope field '{name}' must be a string.");
        string result = value.GetString() ?? "";
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidDataException($"The ID65 editor-document envelope field '{name}' cannot be empty.");
        return result;
    }

    private static int RequireInt32(JsonElement owner, string name)
    {
        JsonElement value = RequireProperty(owner, name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result))
            throw new InvalidDataException($"The ID65 editor-document envelope field '{name}' must be one Int32.");
        return result;
    }

    private static void RefuseSymbolicLink(string path, string role)
    {
        try
        {
            FileInfo file = new(path);
            DirectoryInfo directory = new(path);
            if (!string.IsNullOrWhiteSpace(file.LinkTarget) ||
                !string.IsNullOrWhiteSpace(directory.LinkTarget))
            {
                throw new InvalidDataException(
                    $"The ID65 editor-document {role} cannot be a symbolic link or junction.");
            }
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                $"The ID65 editor-document {role} cannot be inspected safely.",
                exception);
        }
    }

    private static bool ExactPathEntryExists(string path, string role)
    {
        try
        {
            DirectoryInfo directory = new(path);
            if (directory.Exists || !string.IsNullOrWhiteSpace(directory.LinkTarget))
                return true;
            FileInfo file = new(path);
            return file.Exists || !string.IsNullOrWhiteSpace(file.LinkTarget);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                $"The ID65 editor-document {role} cannot be inspected safely.",
                exception);
        }
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(char.IsAsciiHexDigit);

    private static bool HashEquals(string left, string right) =>
        IsSha256(left) && IsSha256(right) &&
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static int GetEnvelopeSchemaVersion(Id65AuthoringEditorDocument document) =>
        document.UsesRemotePadT107Grid
            ? RemotePadGridEnvelopeSchemaVersion
            : EnvelopeSchemaVersion;

    private static string GetProjectionContractId(Id65AuthoringEditorDocument document) =>
        document.UsesRemotePadT107Grid
            ? RemotePadGridProjectionContractId
            : ProjectionContractId;

    private static ProjectionIdentity CaptureProjectionIdentity(
        Id65AuthoringEditorDocumentEnvelopeIdentity identity) =>
        new(
            identity.CompositeProfileId,
            identity.CompositeManifestSha256,
            identity.CompositeOutputRow80Sha256,
            identity.CompositeDeterministicPlanSha256)
        {
            StaticAuthoringProfileId = identity.StaticAuthoringProfileId,
            StaticAuthoringCatalogSha256 = identity.StaticAuthoringCatalogSha256,
            StaticAuthoringSceneGraphSha256 = identity.StaticAuthoringSceneGraphSha256,
            StaticAuthoringProjectionSha256 = identity.StaticAuthoringProjectionSha256
        };

    private static bool ProjectionIdentitiesEqual(
        ProjectionIdentity left,
        ProjectionIdentity right) =>
        left.CompositeProfileId == right.CompositeProfileId &&
        left.CompositeManifestSha256 == right.CompositeManifestSha256 &&
        left.CompositeOutputRow80Sha256 == right.CompositeOutputRow80Sha256 &&
        left.CompositeDeterministicPlanSha256 == right.CompositeDeterministicPlanSha256 &&
        left.StaticAuthoringProfileId == right.StaticAuthoringProfileId &&
        left.StaticAuthoringCatalogSha256 == right.StaticAuthoringCatalogSha256 &&
        left.StaticAuthoringSceneGraphSha256 == right.StaticAuthoringSceneGraphSha256 &&
        left.StaticAuthoringProjectionSha256 == right.StaticAuthoringProjectionSha256;

    private enum DocumentTransitionPolicy
    {
        SameSemanticFamilyOnly,
        ExplicitLegacyToRemotePadGrid
    }

    private sealed record ProjectionIdentity(
        string CompositeProfileId,
        string CompositeManifestSha256,
        string CompositeOutputRow80Sha256,
        string CompositeDeterministicPlanSha256)
    {
        public string StaticAuthoringProfileId { get; init; } = string.Empty;
        public string StaticAuthoringCatalogSha256 { get; init; } = string.Empty;
        public string StaticAuthoringSceneGraphSha256 { get; init; } = string.Empty;
        public string StaticAuthoringProjectionSha256 { get; init; } = string.Empty;
    }

    private sealed record EnvelopeBytes(
        byte[] CanonicalBytes,
        string ContentSha256,
        string CanonicalSha256);
}

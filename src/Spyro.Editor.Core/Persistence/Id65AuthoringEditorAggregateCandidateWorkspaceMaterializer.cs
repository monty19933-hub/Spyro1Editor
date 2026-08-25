using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Skyboxes;

namespace Spyro.Editor.Core.Persistence;

/// <summary>
/// Narrow public receipt for one exact, workspace-local ID65 aggregate
/// candidate. The candidate remains an offline artifact: this receipt grants
/// no App, normal Create BIN, runtime, release, promotion, or publication
/// authority.
/// </summary>
public sealed class Id65AuthoringEditorAggregateCandidateWorkspaceResult
{
    internal Id65AuthoringEditorAggregateCandidateWorkspaceResult(
        Id65AuthoringEditorAggregateCandidateMaterializationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        CandidateDirectoryPath = result.Paths.CandidateDirectoryPath;
        CandidateImagePath = result.Paths.CandidateImagePath;
        CandidateCuePath = result.Paths.CandidateCuePath;
        CandidateReceiptPath = result.Paths.CandidateReceiptPath;
        DocumentEnvelopeSha256 = result.Receipt.DocumentEnvelopeSha256;
        DocumentCanonicalSha256 = result.Receipt.DocumentCanonicalSha256;
        AggregatePlanSha256 = result.Receipt.AggregatePlanSha256;
        OutputRow80Sha256 = result.Receipt.OutputRow80Sha256;
        OutputImageSha256 = result.Receipt.OutputImageSha256;
        CandidateCueSha256 = result.Receipt.CandidateCueSha256;
        ReceiptContentSha256 = result.Receipt.ReceiptContentSha256;
        ChangedRawSectorCount = result.Receipt.ChangedRawSectorCount;
        ChangedPhysicalImageBytes = result.Receipt.ChangedPhysicalImageBytes;
        ReusedExistingCandidate = result.ReusedExistingCandidate;
        CandidateArtifactsWrittenThisCall =
            result.CandidateArtifactsWrittenThisCall;
        FileSystemMutationPerformed = result.FileSystemMutationPerformed;

        if (result.ReusedExistingCandidate ==
                result.CandidateArtifactsWrittenThisCall ||
            (result.CandidateArtifactsWrittenThisCall &&
                !result.FileSystemMutationPerformed) ||
            result.CandidateArtifactsWrittenThisCall !=
                result.AtomicNoReplaceRenameCompletedThisCall ||
            result.CandidateArtifactsWrittenThisCall != result.WritesDiscImage ||
            result.CandidateArtifactsWrittenThisCall != result.WritesBin ||
            result.CandidateArtifactsWrittenThisCall != result.WritesCue ||
            !result.VerifiedAtomicNoReplacePublicationProvenance ||
            !result.OwnedTemporaryResidueRemoved ||
            !result.Receipt.ExactStoreReloadVerified ||
            !result.Receipt.ExactLockedBaseRecaptured ||
            !result.Receipt.ExactRow80ReadbackVerified ||
            !result.Receipt.ExactInverseReadbackVerified ||
            !result.Receipt.ProtectedComplementVerified ||
            !result.Receipt.RetailTownSquarePreserved ||
            !result.Receipt.TargetOverlayPreserved ||
            !result.Receipt.ExecutablePreserved ||
            !result.Receipt.RawSectorIntegrityVerified ||
            !result.Receipt.PhysicalComplementVerified ||
            !result.Receipt.SourceLockedBasePreserved ||
            !result.Receipt.AtomicNoReplaceRenameCompleted ||
            !result.Receipt.ContainsFullDiscImage ||
            !result.Receipt.ContainsCue ||
            !result.Receipt.WorkspaceCandidateWriterAuthorized ||
            result.FinderHandoffPerformed || result.AppIntegrated ||
            result.UiIntegrated || result.CreateBinEnabled ||
            result.NormalCreateBinEnabled ||
            result.RuntimeCandidateAuthorized || result.RuntimeProven ||
            result.ReleaseIntegrated || result.PromotionAuthorized ||
            result.Publishable || result.CurrentOnDiskFreshnessVerified ||
            result.HostileFilesystemRaceSafe ||
            !result.RequiresTrustedExclusiveWorkspaceDirectory ||
            result.CommonWorkspaceLeaseOwned)
        {
            throw new InvalidDataException(
                "The ID65 aggregate candidate crossed an offline materialization boundary.");
        }
    }

    public string CandidateDirectoryPath { get; }
    public string CandidateImagePath { get; }
    public string CandidateCuePath { get; }
    public string CandidateReceiptPath { get; }
    public string DocumentEnvelopeSha256 { get; }
    public string DocumentCanonicalSha256 { get; }
    public string AggregatePlanSha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string OutputImageSha256 { get; }
    public string CandidateCueSha256 { get; }
    public string ReceiptContentSha256 { get; }
    public int ChangedRawSectorCount { get; }
    public long ChangedPhysicalImageBytes { get; }
    public bool ReusedExistingCandidate { get; }
    public bool CandidateArtifactsWrittenThisCall { get; }
    public bool FileSystemMutationPerformed { get; }
    public bool ContainsFilePaths => true;
    public bool ContainsFullDiscImage => true;
    public bool ContainsCue => true;
    public bool ExactStoreReloadVerified => true;
    public bool ExactLockedBaseRecaptured => true;
    public bool ExactRow80ReadbackVerified => true;
    public bool ExactInverseReadbackVerified => true;
    public bool ProtectedComplementVerified => true;
    public bool RetailTownSquarePreserved => true;
    public bool TargetOverlayPreserved => true;
    public bool ExecutablePreserved => true;
    public bool RawSectorIntegrityVerified => true;
    public bool PhysicalComplementVerified => true;
    public bool AtomicNoReplacePublicationVerified => true;
    public bool OwnedTemporaryResidueRemoved => true;
    public bool RequiresExactFullDefaultDocument => true;
    public bool RequiresFocusedRuntimeValidation => true;
    public bool CarriesAppOrUiAuthority => false;
    public bool CarriesCreateBinAuthority => false;
    public bool NormalRetailCreateBinComposed => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool RuntimeProven => false;
    public bool ReleaseIntegrated => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;
    public bool CurrentOnDiskFreshnessVerified => false;
    public bool HostileFilesystemRaceSafe => false;
    public bool RequiresTrustedExclusiveWorkspaceDirectory => true;
    public bool CommonWorkspaceLeaseOwned => false;
}

/// <summary>
/// Explicit, expensive Core bridge from an admitted ID65 workspace to the one
/// currently frozen full-document aggregate candidate. It is never called by
/// workspace open, catalog admission, UI construction, or UI refresh.
/// </summary>
public static class Id65AuthoringEditorAggregateCandidateWorkspaceMaterializer
{
    public static async Task<Id65AuthoringEditorAggregateCandidateWorkspaceResult>
        MaterializeExactAsync(
            UnusedLevel65BlankLevelLabWorkspacePaths paths,
            UnusedLevel65BlankLevelLabManifest expectedLabManifest,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(expectedLabManifest);
        cancellationToken.ThrowIfCancellationRequested();

        UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(
            expectedLabManifest,
            paths);
        Id65AuthoringEditorAggregateCandidateWorkspaceGuard
            .RequireExactCanonicalAuthoredLayer(paths);
        UnusedLevel65BlankLevelLabManifest published =
            await UnusedLevel65BlankLevelLabBootstrapper
                .ValidatePublishedWorkspaceReadOnlyAsync(
                    paths,
                    cancellationToken);
        if (published != expectedLabManifest)
        {
            throw new InvalidDataException(
                "The ID65 aggregate candidate manifest changed from the admitted exact workspace.");
        }

        Id65AuthoringEditorNativeInputProvider.InputSet nativeInputs =
            await Id65AuthoringEditorNativeInputProvider.LoadAsync(
                paths,
                expectedLabManifest,
                cancellationToken);
        Id65AuthoringEditorDocumentStoreResult stored =
            await Id65AuthoringEditorDocumentStore.LoadAsync(
                paths,
                expectedLabManifest,
                new Id65AuthoringEditorDocumentProjectionInputs(nativeInputs),
                cancellationToken) ?? throw new InvalidDataException(
                "Create the canonical ID65 Full Authoring Document before materializing an exact candidate.");
        Id65AuthoringEditorAggregateCandidateWorkspaceGuard
            .RequireExactCanonicalAuthoredLayer(paths);

        Id65AuthoringEditorAggregateCandidateMaterializationResult materialized =
            await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(
                new(paths, expectedLabManifest, stored),
                cancellationToken);
        return new(materialized);
    }
}

internal static class Id65AuthoringEditorAggregateCandidateWorkspaceGuard
{
    internal static void RequireExactCanonicalAuthoredLayer(
        UnusedLevel65BlankLevelLabWorkspacePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        string authoredRoot = Path.GetFullPath(paths.AuthoredEditsDirectoryPath);
        string documentPath = Path.GetFullPath(
            Id65AuthoringEditorDocumentStore.GetPath(paths));
        PersistencePathSafety.EnsureWritePathWithinRoot(
            paths.RootPath,
            authoredRoot,
            "The ID65 aggregate authored root escapes its versioned workspace.");
        PersistencePathSafety.EnsureWritePathWithinRoot(
            authoredRoot,
            documentPath,
            "The ID65 aggregate authoring document escapes its authored root.");

        DirectoryInfo root = new(authoredRoot);
        root.Refresh();
        if (!root.Exists)
        {
            throw new InvalidDataException(
                "Create the canonical ID65 Full Authoring Document before materializing an exact candidate.");
        }
        if (root.LinkTarget != null ||
            (root.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "The ID65 aggregate authored root must not be a symbolic link or reparse point.");
        }

        string[] allowedNames =
        [
            Id65AuthoringEditorDocumentStore.FileName,
            Id65AuthoringEditorDocumentStore.WriterLeaseFileName
        ];
        foreach (string entry in Directory.EnumerateFileSystemEntries(authoredRoot))
        {
            FileSystemInfo info = Directory.Exists(entry)
                ? new DirectoryInfo(entry)
                : new FileInfo(entry);
            info.Refresh();
            string name = Path.GetFileName(entry);
            if (!allowedNames.Contains(name, StringComparer.Ordinal) ||
                info is DirectoryInfo || info.LinkTarget != null ||
                (info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException(
                    $"The ID65 aggregate authored layer contains unsupported entry '{name}'. Remove or explicitly convert it before creating the exact candidate.");
            }
        }

        if (!File.Exists(documentPath))
        {
            throw new InvalidDataException(
                "Create the canonical ID65 Full Authoring Document before materializing an exact candidate.");
        }

        string containerRoot = Path.GetFullPath(paths.ContainerRootPath);
        string key = UnusedLevel65BlankLevelLabProfileRegistry.Key;
        string[] conflictingPaths =
        [
            Path.Combine(containerRoot, $"{key}-native-edits.json"),
            Path.Combine(
                containerRoot,
                NativeMobyPathEditStore.DefaultFileName(key)),
            CustomTerrainTextureStore.ManifestPath(containerRoot, key),
            NativeTerrainTextureRelocationEditStore.ManifestPath(
                containerRoot,
                key),
            Path.Combine(
                containerRoot,
                $"{key}-terrain-material-overrides.json"),
            NativeSkyEditStore.PlanPath(containerRoot, key),
            LevelMusicEditStore.PlanPath(
                containerRoot,
                UnusedLevel65BlankLevelLabProfileRegistry.Definition)
        ];
        foreach (string conflictingPath in conflictingPaths)
        {
            PersistencePathSafety.EnsureWritePathWithinRoot(
                containerRoot,
                conflictingPath,
                "A reserved ID65 legacy authoring path escapes the project workspace.");
            RefuseReservedLegacyEntry(conflictingPath);
        }
    }

    private static void RefuseReservedLegacyEntry(string path)
    {
        try
        {
            DirectoryInfo directory = new(path);
            directory.Refresh();
            FileInfo file = new(path);
            file.Refresh();
            if (directory.Exists || directory.LinkTarget != null ||
                file.Exists || file.LinkTarget != null)
            {
                throw new InvalidDataException(
                    $"The exact ID65 aggregate candidate cannot omit reserved legacy authored entry '{Path.GetFileName(path)}'. Remove or explicitly convert it first.");
            }
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                $"The reserved ID65 legacy authored path '{Path.GetFileName(path)}' could not be inspected safely.",
                exception);
        }
    }
}

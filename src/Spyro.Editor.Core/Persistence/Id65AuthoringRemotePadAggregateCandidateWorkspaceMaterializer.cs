using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

/// <summary>
/// Narrow public receipt for one exact, workspace-local schema-3 remote-pad
/// candidate. It grants no normal Create BIN, runtime, release, promotion, or
/// publication authority.
/// </summary>
public sealed class Id65AuthoringRemotePadAggregateCandidateWorkspaceResult
{
    internal Id65AuthoringRemotePadAggregateCandidateWorkspaceResult(
        Id65AuthoringRemotePadAggregateCandidateMaterializationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        CandidateDirectoryPath = result.Paths.CandidateDirectoryPath;
        CandidateImagePath = result.Paths.CandidateImagePath;
        CandidateCuePath = result.Paths.CandidateCuePath;
        CandidateReceiptPath = result.Paths.CandidateReceiptPath;
        DocumentEnvelopeSha256 = result.Receipt.DocumentEnvelopeSha256;
        DocumentCanonicalSha256 = result.Receipt.DocumentCanonicalSha256;
        StaticAuthoringProjectionSha256 =
            result.Receipt.StaticAuthoringProjectionSha256;
        NativeInputSetSha256 = result.Receipt.NativeInputSetSha256;
        AggregatePlanSha256 = result.Receipt.AggregatePlanSha256;
        OutputRow80Sha256 = result.Receipt.OutputRow80Sha256;
        OutputImageSha256 = result.Receipt.OutputImageSha256;
        CandidateCueSha256 = result.Receipt.CandidateCueSha256;
        ReceiptContentSha256 = result.Receipt.ReceiptContentSha256;
        PositionId = result.Receipt.PositionId;
        StructuralTerrainPresent = result.Plan.StructuralTerrainPresent;
        GrassT107Present = result.Plan.GrassT107Present;
        EditorX = result.Receipt.EditorX;
        EditorY = result.Receipt.EditorY;
        EditorZ = result.Receipt.EditorZ;
        ChangedRawSectorCount = result.Receipt.ChangedRawSectorCount;
        ChangedPhysicalImageBytes = result.Receipt.ChangedPhysicalImageBytes;
        ReusedExistingCandidate = result.ReusedExistingCandidate;
        CandidateArtifactsWrittenThisCall =
            result.CandidateArtifactsWrittenThisCall;
        FileSystemMutationPerformed = result.FileSystemMutationPerformed;

        Id65RemotePadGrassT107Position position =
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(PositionId);
        if (result.Plan.Placement.PositionId != PositionId ||
            result.Plan.Placement.Present != GrassT107Present ||
            position.EditorX != EditorX || position.EditorY != EditorY ||
            position.EditorZ != EditorZ ||
            result.ReusedExistingCandidate ==
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
            !result.Receipt.ExactStaticAuthoringProjectionReloadVerified ||
            !result.Receipt.ExactNativeInputGenerationRecaptured ||
            !result.Receipt.ExactLockedBaseRecaptured ||
            !result.Receipt.Schema3CompilerPinsVerified ||
            !result.Receipt.T107PositionReadbackVerified ||
            !result.Receipt.T88ExcludedVerified ||
            !result.Receipt.RepairedT13995SupportVerified ||
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
                "The schema-3 remote-pad candidate crossed an offline materialization boundary.");
        }
    }

    public string CandidateDirectoryPath { get; }
    public string CandidateImagePath { get; }
    public string CandidateCuePath { get; }
    public string CandidateReceiptPath { get; }
    public string DocumentEnvelopeSha256 { get; }
    public string DocumentCanonicalSha256 { get; }
    public string StaticAuthoringProjectionSha256 { get; }
    public string NativeInputSetSha256 { get; }
    public string AggregatePlanSha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string OutputImageSha256 { get; }
    public string CandidateCueSha256 { get; }
    public string ReceiptContentSha256 { get; }
    public string PositionId { get; }
    public bool StructuralTerrainPresent { get; }
    public bool GrassT107Present { get; }
    public int EditorX { get; }
    public int EditorY { get; }
    public int EditorZ { get; }
    public int ChangedRawSectorCount { get; }
    public long ChangedPhysicalImageBytes { get; }
    public bool ReusedExistingCandidate { get; }
    public bool CandidateArtifactsWrittenThisCall { get; }
    public bool FileSystemMutationPerformed { get; }
    public bool ContainsFilePaths => true;
    public bool ContainsFullDiscImage => true;
    public bool ContainsCue => true;
    public bool ExactStoreReloadVerified => true;
    public bool ExactStaticAuthoringProjectionReloadVerified => true;
    public bool ExactNativeInputGenerationRecaptured => true;
    public bool ExactLockedBaseRecaptured => true;
    public bool Schema3CompilerPinsVerified => true;
    public bool T107PositionReadbackVerified => true;
    public bool T88ExcludedVerified => true;
    public bool RepairedT13995SupportVerified => true;
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
    public bool RequiresExactRemotePadSchema3Document => true;
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
/// Explicit, expensive Core bridge from an admitted ID65 workspace to one
/// frozen schema-3 remote-pad candidate. It is never called by workspace open,
/// catalog admission, UI construction, or UI refresh.
/// </summary>
public static class Id65AuthoringRemotePadAggregateCandidateWorkspaceMaterializer
{
    public static async Task<Id65AuthoringRemotePadAggregateCandidateWorkspaceResult>
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
                "The schema-3 remote-pad candidate manifest changed from the admitted exact workspace.");
        }

        Id65AuthoringEditorDocumentStoreResult stored =
            await Id65AuthoringEditorDocumentStore.LoadAsync(
                paths,
                expectedLabManifest,
                new Id65AuthoringEditorDocumentProjectionInputs(null),
                cancellationToken) ?? throw new InvalidDataException(
                    "Create the canonical schema-3 ID65 Full Authoring Document before materializing a remote-pad candidate.");
        Id65AuthoringEditorAggregateCandidateWorkspaceGuard
            .RequireExactCanonicalAuthoredLayer(paths);

        Id65AuthoringRemotePadAggregateCandidateMaterializationResult materialized =
            await Id65AuthoringRemotePadAggregateCandidateMaterializer
                .MaterializeAsync(
                    new(paths, expectedLabManifest, stored),
                    cancellationToken);
        return new(materialized);
    }
}

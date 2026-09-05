using System.Collections.ObjectModel;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

/// <summary>
/// Narrow public receipt for one disposable, workspace-local schema-5
/// terrain-set/Moby-choice candidate. It grants no normal Create BIN, runtime,
/// release, promotion, or publication authority.
/// </summary>
public sealed class Id65AuthoringRemotePadTerrainSetAggregateCandidateWorkspaceResult
{
    private readonly ReadOnlyCollection<string> _activeTerrainUnitIds;

    internal Id65AuthoringRemotePadTerrainSetAggregateCandidateWorkspaceResult(
        Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializationResult result)
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
        TerrainManifestSha256 = result.Receipt.TerrainManifestSha256;
        _activeTerrainUnitIds = Array.AsReadOnly(
            string.IsNullOrEmpty(result.Receipt.ActiveTerrainUnitIdsCanonical)
                ? []
                : result.Receipt.ActiveTerrainUnitIdsCanonical.Split('\n'));
        MobyKind = result.Plan.MobyKind;
        PositionId = result.Receipt.PositionId;
        NativeInputSetSha256 = result.Receipt.NativeInputSetSha256;
        AggregatePlanSha256 = result.Receipt.AggregatePlanSha256;
        OutputRow80Sha256 = result.Receipt.OutputRow80Sha256;
        OutputImageSha256 = result.Receipt.OutputImageSha256;
        CandidateCueSha256 = result.Receipt.CandidateCueSha256;
        ReceiptContentSha256 = result.Receipt.ReceiptContentSha256;
        ChangedRawSectorCount = result.Receipt.ChangedRawSectorCount;
        ChangedPhysicalImageBytes = result.Receipt.ChangedPhysicalImageBytes;
        Schema5CompilerPinsVerified =
            result.Receipt.Schema5CompilerPinsVerified;
        MobyChoicePositionReadbackVerified =
            result.Receipt.MobyChoicePositionReadbackVerified;
        FourTerrainTopologiesVerified =
            result.Receipt.FourTerrainTopologiesVerified;
        T88ExcludedVerified = result.Receipt.T88ExcludedVerified;
        RepairedT13995SupportVerified =
            result.Receipt.RepairedT13995SupportVerified;
        ReusedExistingCandidate = result.ReusedExistingCandidate;
        CandidateArtifactsWrittenThisCall =
            result.CandidateArtifactsWrittenThisCall;
        FileSystemMutationPerformed = result.FileSystemMutationPerformed;

        Id65RemotePadGrassT107Position position =
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(PositionId);
        string activeTerrainUnitIdsCanonical =
            string.Join('\n', result.Plan.ActiveTerrainUnitIds);
        if (result.Receipt.Format !=
                Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer.Format ||
            result.Receipt.SchemaVersion !=
                Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
                    .ReceiptSchemaVersion ||
            result.Receipt.ProfileId !=
                Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer.ProfileId ||
            result.Receipt.CandidateImageFileName !=
                Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
                    .CandidateImageFileName ||
            result.Receipt.CandidateCueFileName !=
                Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
                    .CandidateCueFileName ||
            result.Receipt.CandidateCueSha256 !=
                Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
                    .ExpectedCandidateCueSha256 ||
            result.Receipt.TargetDataWadEntry !=
                Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
                    .TargetDataWadEntry ||
            result.Receipt.TargetDataWadOffset !=
                Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
                    .TargetDataWadOffset ||
            result.Receipt.TargetDataByteLength !=
                Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
                    .TargetDataByteLength ||
            result.Receipt.FirstRebuiltRawSectorLba !=
                Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
                    .FirstRebuiltRawSectorLba ||
            result.Receipt.RebuiltRawSectorCount !=
                Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
                    .RebuiltRawSectorCount ||
            result.Plan.Document.SchemaVersion !=
                Id65AuthoringEditorDocument.RemotePadMobyChoiceTerrainSetSchemaVersion ||
            result.Plan.EnvelopeCanonicalSha256 != DocumentEnvelopeSha256 ||
            result.Plan.DocumentCanonicalSha256 != DocumentCanonicalSha256 ||
            result.Plan.StaticAuthoringProjectionSha256 !=
                StaticAuthoringProjectionSha256 ||
            result.Plan.NativeInputSetSha256 != NativeInputSetSha256 ||
            result.Plan.DeterministicAggregatePlanSha256 != AggregatePlanSha256 ||
            result.Plan.OutputRow80Sha256 != OutputRow80Sha256 ||
            result.Plan.Placement.Kind != MobyKind ||
            result.Plan.Placement.PositionId != PositionId ||
            !result.Plan.ActiveTerrainUnitIds.SequenceEqual(_activeTerrainUnitIds) ||
            result.Receipt.ActiveTerrainUnitIdsCanonical !=
                activeTerrainUnitIdsCanonical ||
            result.Plan.TerrainManifestSha256 != TerrainManifestSha256 ||
            result.Receipt.MobyKindId !=
                Id65RemotePadMobyChoiceCatalog.GetKindId(MobyKind) ||
            position.PositionId != PositionId ||
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
            !result.Receipt.ExactStoreReloadCasVerifiedThreeTimes ||
            !result.Receipt.ExactStaticAuthoringProjectionReloadVerifiedThreeTimes ||
            !result.Receipt.ExactNativeInputGenerationRecapturedThreeTimes ||
            !result.Receipt.ExactV3TemplateRecapturedThreeTimes ||
            !result.Receipt.ExactLockedBaseRecapturedThreeTimes ||
            !Schema5CompilerPinsVerified ||
            !MobyChoicePositionReadbackVerified ||
            !FourTerrainTopologiesVerified ||
            !T88ExcludedVerified ||
            !RepairedT13995SupportVerified ||
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
            result.Receipt.RuntimeCandidateAuthorized ||
            result.Receipt.RuntimeProven || result.Receipt.SaveAuthorized ||
            result.Receipt.SavePersistenceProven ||
            result.Receipt.TreasureTotal200Proven ||
            result.Receipt.MusicLongPlayProven ||
            result.Receipt.ExitDestination65Proven ||
            result.Receipt.AppIntegrated || result.Receipt.UiIntegrated ||
            result.Receipt.CreateBinEnabled ||
            result.Receipt.NormalCreateBinEnabled ||
            result.Receipt.RetailLevelMutationAuthorized ||
            result.Receipt.ReleaseIntegrated ||
            result.Receipt.ReleaseAuthorized ||
            result.Receipt.PromotionAuthorized ||
            result.Receipt.Publishable ||
            result.Receipt.CurrentOnDiskFreshnessVerified ||
            result.Receipt.HostileFilesystemRaceSafe ||
            !result.Receipt.RequiresTrustedExclusiveWorkspaceDirectory ||
            result.Receipt.CommonWorkspaceLeaseOwned ||
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
                "The schema-5 terrain-set candidate crossed its disposable offline materialization boundary.");
        }
    }

    public string CandidateDirectoryPath { get; }
    public string CandidateImagePath { get; }
    public string CandidateCuePath { get; }
    public string CandidateReceiptPath { get; }
    public string DocumentEnvelopeSha256 { get; }
    public string DocumentCanonicalSha256 { get; }
    public string StaticAuthoringProjectionSha256 { get; }
    public string TerrainManifestSha256 { get; }
    public IReadOnlyList<string> ActiveTerrainUnitIds => _activeTerrainUnitIds;
    public Id65RemotePadMobyKind MobyKind { get; }
    public string PositionId { get; }
    public string NativeInputSetSha256 { get; }
    public string AggregatePlanSha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string OutputImageSha256 { get; }
    public string CandidateCueSha256 { get; }
    public string ReceiptContentSha256 { get; }
    public int ChangedRawSectorCount { get; }
    public long ChangedPhysicalImageBytes { get; }
    public bool Schema5CompilerPinsVerified { get; }
    public bool MobyChoicePositionReadbackVerified { get; }
    public bool FourTerrainTopologiesVerified { get; }
    public bool T88ExcludedVerified { get; }
    public bool RepairedT13995SupportVerified { get; }
    public bool ReusedExistingCandidate { get; }
    public bool CandidateArtifactsWrittenThisCall { get; }
    public bool FileSystemMutationPerformed { get; }
    public bool ContainsFilePaths => true;
    public bool ContainsFullDiscImage => true;
    public bool ContainsCue => true;
    public bool ExactStoreReloadCasVerifiedThreeTimes => true;
    public bool ExactEnvelopeCasVerifiedThreeTimes => true;
    public bool ExactStaticAuthoringProjectionReloadVerifiedThreeTimes => true;
    public bool ExactNativeInputGenerationRecapturedThreeTimes => true;
    public bool ExactV3TemplateRecapturedThreeTimes => true;
    public bool ExactLockedBaseRecapturedThreeTimes => true;
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
    public bool RequiresExactRemotePadSchema5Document => true;
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
/// Explicit, expensive Core bridge from one admitted ID65 workspace and exact
/// schema-5 envelope identity to a disposable BIN/CUE candidate. It is not
/// called by workspace open, UI construction, refresh, or normal Create BIN.
/// </summary>
public static class Id65AuthoringRemotePadTerrainSetAggregateCandidateWorkspaceMaterializer
{
    public static async Task<
        Id65AuthoringRemotePadTerrainSetAggregateCandidateWorkspaceResult>
        MaterializeExactAsync(
            UnusedLevel65BlankLevelLabWorkspacePaths paths,
            UnusedLevel65BlankLevelLabManifest expectedManifest,
            string expectedEnvelopeCanonicalSha256,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(expectedManifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            expectedEnvelopeCanonicalSha256);
        cancellationToken.ThrowIfCancellationRequested();

        UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(
            expectedManifest,
            paths);
        Id65AuthoringEditorAggregateCandidateWorkspaceGuard
            .RequireExactCanonicalAuthoredLayer(paths);
        UnusedLevel65BlankLevelLabManifest published =
            await UnusedLevel65BlankLevelLabBootstrapper
                .ValidatePublishedWorkspaceReadOnlyAsync(
                    paths,
                    cancellationToken);
        if (published != expectedManifest)
        {
            throw new InvalidDataException(
                "The schema-5 terrain-set candidate manifest changed from the admitted exact workspace.");
        }

        Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializationResult
            materialized =
                await Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
                    .MaterializeAsync(
                        new(
                            paths,
                            expectedManifest,
                            expectedEnvelopeCanonicalSha256),
                        cancellationToken);
        return new(materialized);
    }
}

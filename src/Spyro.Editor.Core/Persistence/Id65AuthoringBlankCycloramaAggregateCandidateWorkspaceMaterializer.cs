using System.Collections.ObjectModel;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

/// <summary>
/// Narrow public receipt for one disposable, workspace-local schema-6
/// terrain-set/Moby-choice candidate. It grants no normal Create BIN, runtime,
/// release, promotion, or publication authority.
/// </summary>
public sealed class Id65AuthoringBlankCycloramaAggregateCandidateWorkspaceResult
{
    private readonly ReadOnlyCollection<string> _activeTerrainUnitIds;

    internal Id65AuthoringBlankCycloramaAggregateCandidateWorkspaceResult(
        Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult result)
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
        PresetId = result.Receipt.PresetId;
        ColorHex = result.Receipt.ColorHex;
        CycloramaModelOffset = result.Receipt.CycloramaModelOffset;
        CycloramaRow80Offset = result.Receipt.CycloramaRow80Offset;
        CycloramaWadOffset = result.Receipt.CycloramaWadOffset;
        CycloramaByteLength = result.Receipt.CycloramaByteLength;
        PaletteWordCount = result.Receipt.PaletteWordCount;
        PaletteOwnedRangeCount = result.Receipt.PaletteOwnedRangeCount;
        OutputCycloramaSha256 = result.Receipt.OutputCycloramaSha256;
        GeometrySha256 = result.Receipt.GeometrySha256;
        CommandByteSha256 = result.Receipt.CommandByteSha256;
        NonPaletteSha256 = result.Receipt.NonPaletteSha256;
        OutputImageSha256 = result.Receipt.OutputImageSha256;
        CandidateCueSha256 = result.Receipt.CandidateCueSha256;
        ReceiptContentSha256 = result.Receipt.ReceiptContentSha256;
        ChangedRawSectorCount = result.Receipt.ChangedRawSectorCount;
        ChangedPhysicalImageBytes = result.Receipt.ChangedPhysicalImageBytes;
        Schema6CompilerPinsVerified =
            result.Receipt.Schema6CompilerPinsVerified;
        BlankCycloramaIntentReadbackVerified =
            result.Receipt.BlankCycloramaIntentReadbackVerified;
        CycloramaLayoutReadbackVerified =
            result.Receipt.CycloramaLayoutReadbackVerified;
        EveryPaletteWordReadbackVerified =
            result.Receipt.EveryPaletteWordReadbackVerified;
        CycloramaGeometryReadbackVerified =
            result.Receipt.CycloramaGeometryReadbackVerified;
        CycloramaCommandBytesReadbackVerified =
            result.Receipt.CycloramaCommandBytesReadbackVerified;
        CycloramaNonPaletteReadbackVerified =
            result.Receipt.CycloramaNonPaletteReadbackVerified;
        MobyChoicePositionReadbackVerified =
            result.Receipt.MobyChoicePositionReadbackVerified;
        FourTerrainTopologiesVerified =
            result.Receipt.FourTerrainTopologiesVerified;
        T88ExcludedVerified = result.Receipt.T88ExcludedVerified;
        RepairedT13995SupportVerified =
            result.Receipt.RepairedT13995SupportVerified;
        Wad79Preserved = result.Receipt.Wad79Preserved;
        RetailTownSquareWad15Preserved =
            result.Receipt.RetailTownSquareWad15Preserved;
        RetailTownSquareWad16Preserved =
            result.Receipt.RetailTownSquareWad16Preserved;
        RetailArtisansWad9Preserved =
            result.Receipt.RetailArtisansWad9Preserved;
        RetailArtisansWad10Preserved =
            result.Receipt.RetailArtisansWad10Preserved;
        RetailTownSquareSkyBlockPreserved =
            result.Receipt.RetailTownSquareSkyBlockPreserved;
        RetailArtisansLinkedSkyBlockPreserved =
            result.Receipt.RetailArtisansLinkedSkyBlockPreserved;
        ReusedExistingCandidate = result.ReusedExistingCandidate;
        CandidateArtifactsWrittenThisCall =
            result.CandidateArtifactsWrittenThisCall;
        FileSystemMutationPerformed = result.FileSystemMutationPerformed;

        Id65RemotePadGrassT107Position position =
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(PositionId);
        string activeTerrainUnitIdsCanonical =
            string.Join('\n', result.Plan.ActiveTerrainUnitIds);
        if (result.Receipt.Format !=
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializer.Format ||
            result.Receipt.SchemaVersion !=
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                    .ReceiptSchemaVersion ||
            result.Receipt.ProfileId !=
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializer.ProfileId ||
            result.Receipt.CandidateImageFileName !=
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                    .CandidateImageFileName ||
            result.Receipt.CandidateCueFileName !=
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                    .CandidateCueFileName ||
            result.Receipt.CandidateCueSha256 !=
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                    .ExpectedCandidateCueSha256 ||
            result.Receipt.TargetDataWadEntry !=
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                    .TargetDataWadEntry ||
            result.Receipt.TargetDataWadOffset !=
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                    .TargetDataWadOffset ||
            result.Receipt.TargetDataByteLength !=
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                    .TargetDataByteLength ||
            result.Receipt.FirstRebuiltRawSectorLba !=
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                    .FirstRebuiltRawSectorLba ||
            result.Receipt.RebuiltRawSectorCount !=
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                    .RebuiltRawSectorCount ||
            result.Plan.Document.SchemaVersion !=
                Id65AuthoringEditorDocument
                    .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion ||
            result.Plan.EnvelopeCanonicalSha256 != DocumentEnvelopeSha256 ||
            result.Plan.DocumentCanonicalSha256 != DocumentCanonicalSha256 ||
            result.Plan.StaticAuthoringProjectionSha256 !=
                StaticAuthoringProjectionSha256 ||
            result.Plan.NativeInputSetSha256 != NativeInputSetSha256 ||
            result.Plan.DeterministicAggregatePlanSha256 != AggregatePlanSha256 ||
            result.Plan.OutputRow80Sha256 != OutputRow80Sha256 ||
            result.Plan.PresetId != PresetId ||
            result.Plan.ColorHex != ColorHex ||
            PresetId != Id65AuthoringBlankCycloramaCatalog.BlankSlatePresetId ||
            ColorHex != Id65AuthoringBlankCycloramaCatalog.BlankSlateColorHex ||
            result.Receipt.BlankCycloramaCatalogCanonicalSha256 !=
                Id65AuthoringBlankCycloramaCatalog.CanonicalSha256 ||
            result.Plan.CycloramaModelOffset != CycloramaModelOffset ||
            result.Plan.CycloramaRow80Offset != CycloramaRow80Offset ||
            result.Plan.CycloramaWadOffset != CycloramaWadOffset ||
            result.Plan.CycloramaByteLength != CycloramaByteLength ||
            result.Plan.PaletteWordCount != PaletteWordCount ||
            result.Plan.PaletteOwnedRangeCount != PaletteOwnedRangeCount ||
            result.Plan.OutputCycloramaSha256 != OutputCycloramaSha256 ||
            result.Plan.GeometrySha256 != GeometrySha256 ||
            result.Plan.CommandByteSha256 != CommandByteSha256 ||
            result.Plan.NonPaletteSha256 != NonPaletteSha256 ||
            result.Plan.MobyKind != MobyKind ||
            result.Plan.MobyPositionId != PositionId ||
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
            !Schema6CompilerPinsVerified ||
            !BlankCycloramaIntentReadbackVerified ||
            !CycloramaLayoutReadbackVerified ||
            !EveryPaletteWordReadbackVerified ||
            !CycloramaGeometryReadbackVerified ||
            !CycloramaCommandBytesReadbackVerified ||
            !CycloramaNonPaletteReadbackVerified ||
            !MobyChoicePositionReadbackVerified ||
            !FourTerrainTopologiesVerified ||
            !T88ExcludedVerified ||
            !RepairedT13995SupportVerified ||
            !result.Receipt.ExactRow80ReadbackVerified ||
            !result.Receipt.ExactInverseReadbackVerified ||
            !result.Receipt.ProtectedComplementVerified ||
            !Wad79Preserved ||
            !RetailTownSquareWad15Preserved ||
            !RetailTownSquareWad16Preserved ||
            !RetailArtisansWad9Preserved ||
            !RetailArtisansWad10Preserved ||
            !RetailTownSquareSkyBlockPreserved ||
            !RetailArtisansLinkedSkyBlockPreserved ||
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
                "The schema-6 blank-cyclorama candidate crossed its disposable offline materialization boundary.");
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
    public string PresetId { get; }
    public string ColorHex { get; }
    public int CycloramaModelOffset { get; }
    public int CycloramaRow80Offset { get; }
    public long CycloramaWadOffset { get; }
    public int CycloramaByteLength { get; }
    public int PaletteWordCount { get; }
    public int PaletteOwnedRangeCount { get; }
    public string OutputCycloramaSha256 { get; }
    public string GeometrySha256 { get; }
    public string CommandByteSha256 { get; }
    public string NonPaletteSha256 { get; }
    public string OutputImageSha256 { get; }
    public string CandidateCueSha256 { get; }
    public string ReceiptContentSha256 { get; }
    public int ChangedRawSectorCount { get; }
    public long ChangedPhysicalImageBytes { get; }
    public bool Schema6CompilerPinsVerified { get; }
    public bool BlankCycloramaIntentReadbackVerified { get; }
    public bool CycloramaLayoutReadbackVerified { get; }
    public bool EveryPaletteWordReadbackVerified { get; }
    public bool CycloramaGeometryReadbackVerified { get; }
    public bool CycloramaCommandBytesReadbackVerified { get; }
    public bool CycloramaNonPaletteReadbackVerified { get; }
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
    public bool Wad79Preserved { get; }
    public bool RetailTownSquareWad15Preserved { get; }
    public bool RetailTownSquareWad16Preserved { get; }
    public bool RetailArtisansWad9Preserved { get; }
    public bool RetailArtisansWad10Preserved { get; }
    public bool RetailTownSquareSkyBlockPreserved { get; }
    public bool RetailArtisansLinkedSkyBlockPreserved { get; }
    public bool ExecutablePreserved => true;
    public bool RawSectorIntegrityVerified => true;
    public bool PhysicalComplementVerified => true;
    public bool AtomicNoReplacePublicationVerified => true;
    public bool OwnedTemporaryResidueRemoved => true;
    public bool RequiresExactRemotePadSchema6Document => true;
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
/// schema-6 envelope identity to a disposable BIN/CUE candidate. It is not
/// called by workspace open, UI construction, refresh, or normal Create BIN.
/// </summary>
public static class Id65AuthoringBlankCycloramaAggregateCandidateWorkspaceMaterializer
{
    public static async Task<
        Id65AuthoringBlankCycloramaAggregateCandidateWorkspaceResult>
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
                "The schema-6 blank-cyclorama candidate manifest changed from the admitted exact workspace.");
        }

        Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult
            materialized =
                await Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                    .MaterializeAsync(
                        new(
                            paths,
                            expectedManifest,
                            expectedEnvelopeCanonicalSha256),
                        cancellationToken);
        return new(materialized);
    }
}

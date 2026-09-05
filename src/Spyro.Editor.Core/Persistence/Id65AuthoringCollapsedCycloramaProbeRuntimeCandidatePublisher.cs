using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

internal sealed record Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePublishRequest(
    string ParentSchema7CandidateDirectoryPath,
    string OutputRootPath,
    Id65CompiledAuthoringCollapsedCycloramaGeometryProbe Compiled);

internal sealed record Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePaths(
    string CandidateRootPath,
    string CandidateDirectoryPath,
    string CandidateImagePath,
    string CandidateCuePath,
    string CandidateReceiptPath,
    string WriterLeasePath);

internal sealed record Id65AuthoringCollapsedCycloramaProbeRuntimeCandidateReceipt
{
    public required string Format { get; init; }
    public required int SchemaVersion { get; init; }
    public required string ProfileId { get; init; }
    public required string PublicationIdentitySha256 { get; init; }
    public required string ParentReceiptFormat { get; init; }
    public required int ParentReceiptSchemaVersion { get; init; }
    public required string ParentReceiptProfileId { get; init; }
    public required string ParentCandidateDirectoryName { get; init; }
    public required string ParentCandidateImageFileName { get; init; }
    public required string ParentCandidateCueFileName { get; init; }
    public required string ParentCandidateReceiptFileName { get; init; }
    public required string ParentCandidateImageSha256 { get; init; }
    public required string ParentCandidateCueSha256 { get; init; }
    public required string ParentCandidateReceiptSha256 { get; init; }
    public required string ParentReceiptContentSha256 { get; init; }
    public required string ParentPublicationIdentitySha256 { get; init; }
    public required string DocumentCanonicalSha256 { get; init; }
    public required string ActiveTerrainUnitIdsCanonical { get; init; }
    public required string MobyKindId { get; init; }
    public required string MobyPositionId { get; init; }
    public required string CompilerProfileId { get; init; }
    public required int CompilerSchemaVersion { get; init; }
    public required string CompilerDeterministicPlanSha256 { get; init; }
    public required string Schema7CompilerDeterministicPlanSha256 { get; init; }
    public required string BaselineRow80Sha256 { get; init; }
    public required string OutputRow80Sha256 { get; init; }
    public required string BaselineModelSha256 { get; init; }
    public required string OutputModelSha256 { get; init; }
    public required int CycloramaRow80Offset { get; init; }
    public required long CycloramaWadOffset { get; init; }
    public required int CycloramaByteLength { get; init; }
    public required string BaselineCycloramaSha256 { get; init; }
    public required string OutputCycloramaSha256 { get; init; }
    public required string OutputGeometrySha256 { get; init; }
    public required int CycloramaPartCount { get; init; }
    public required int CycloramaVertexCount { get; init; }
    public required int CycloramaColorCount { get; init; }
    public required int CycloramaPolygonCount { get; init; }
    public required int CandidateCollapsedVertexWordCount { get; init; }
    public required int ChangedVertexWordCount { get; init; }
    public required int ChangedLogicalByteCount { get; init; }
    public required int PolygonChangedByteCount { get; init; }
    public required int OcclusionChangedByteCount { get; init; }
    public required int OcclusionTailChangedByteCount { get; init; }
    public required string EnvironmentSha256 { get; init; }
    public required string OcclusionSha256 { get; init; }
    public required string OcclusionTailSha256 { get; init; }
    public required string CollisionSha256 { get; init; }
    public required string TextureSha256 { get; init; }
    public required string OwnedRangeMapSha256 { get; init; }
    public required string TransactionSha256 { get; init; }
    public required string ProtectedComplementSha256 { get; init; }
    public required string CandidateImageFileName { get; init; }
    public required string CandidateCueFileName { get; init; }
    public required string CandidateReceiptFileName { get; init; }
    public required string OutputImageSha256 { get; init; }
    public required string CandidateCueSha256 { get; init; }
    public required int TargetDataWadEntry { get; init; }
    public required long TargetDataWadOffset { get; init; }
    public required int TargetDataByteLength { get; init; }
    public required int FirstSkyRawSectorLba { get; init; }
    public required int SkyRawSectorCount { get; init; }
    public required int ChangedRawSectorCount { get; init; }
    public required long ChangedPhysicalImageBytes { get; init; }
    public required string PhysicalDiffSha256 { get; init; }
    public required bool ExactParentReceiptVerified { get; init; }
    public required bool ExactParentImageVerified { get; init; }
    public required bool ExactSchema7ProbePreimageVerified { get; init; }
    public required bool ExactSchema7ProbeInverseVerified { get; init; }
    public required bool ExactOutputRow80ReadbackVerified { get; init; }
    public required bool ExactOutputCycloramaReadbackVerified { get; init; }
    public required bool AllThirtyFivePartPointersPreservedExactly { get; init; }
    public required bool AllThirtyFivePartHeadersPreservedExactly { get; init; }
    public required bool PartCountsPreservedExactly { get; init; }
    public required bool ColorTablesPreservedExactly { get; init; }
    public required bool PolygonTablesPreservedExactly { get; init; }
    public required bool FirstVertexWordsPreservedExactly { get; init; }
    public required bool RemainingVertexWordsCollapsedPerPart { get; init; }
    public required bool EntireOcclusionComponentPreservedExactly { get; init; }
    public required bool OcclusionOpaqueTailPreservedExactly { get; init; }
    public required bool CollisionPreservedExactly { get; init; }
    public required bool EnvironmentPreservedExactly { get; init; }
    public required bool TexturePreservedExactly { get; init; }
    public required bool SpawnAndMobyBytesPreservedExactly { get; init; }
    public required bool OnlySkyIntersectingRawSectorsChanged { get; init; }
    public required bool Wad79Preserved { get; init; }
    public required bool RetailTownSquareWad15Preserved { get; init; }
    public required bool RetailTownSquareWad16Preserved { get; init; }
    public required bool RetailArtisansWad9Preserved { get; init; }
    public required bool RetailArtisansWad10Preserved { get; init; }
    public required bool RetailTownSquareSkyBlockPreserved { get; init; }
    public required bool RetailArtisansLinkedSkyBlockPreserved { get; init; }
    public required bool ExecutablePreserved { get; init; }
    public required bool RawSectorIntegrityVerified { get; init; }
    public required bool BcdMsfVerified { get; init; }
    public required bool DuplicatedSubheadersVerified { get; init; }
    public required bool PhysicalComplementVerified { get; init; }
    public required bool ParentSchema7CandidatePreserved { get; init; }
    public required bool ContainsFullDiscImage { get; init; }
    public required bool ContainsCue { get; init; }
    public required bool TransitionalProbeOnly { get; init; }
    public required bool FullyOriginalCyclorama { get; init; }
    public required bool SeparateGuardedCandidatePublisherAuthorized { get; init; }
    public required bool RuntimeCandidateAuthorized { get; init; }
    public required bool RuntimeProven { get; init; }
    public required bool AppIntegrated { get; init; }
    public required bool UiIntegrated { get; init; }
    public required bool DocumentPersistenceIntegrated { get; init; }
    public required bool CreateBinEnabled { get; init; }
    public required bool NormalCreateBinEnabled { get; init; }
    public required bool ReleaseIntegrated { get; init; }
    public required bool PromotionAuthorized { get; init; }
    public required bool Publishable { get; init; }
    public required string ReceiptContentSha256 { get; init; }
}

internal sealed record Id65AuthoringCollapsedCycloramaProbeRuntimeCandidateResult(
    Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePaths Paths,
    Id65AuthoringCollapsedCycloramaProbeRuntimeCandidateReceipt Receipt,
    string ReceiptFileSha256,
    bool ReusedExistingCandidate,
    bool CandidateArtifactsWrittenThisCall,
    bool FileSystemMutationPerformed,
    bool RuntimeProven,
    bool NormalCreateBinEnabled,
    bool ReleaseIntegrated,
    bool Publishable);

/// <summary>
/// Publishes one disposable, content-addressed runtime probe by applying only
/// the collapsed-vertex cyclorama transaction to the exact already-published
/// Tile1 / Grass T107 / Center schema-7 p1 candidate. The parent is immutable.
/// This is transitional runtime evidence and is never a fully original sky,
/// normal Create BIN path, document format, UI path, release, or promotion.
/// </summary>
internal static class Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePublisher
{
    public const string Format =
        "spyro-editor-id65-authoring-collapsed-35-part-cyclorama-probe-schema7-runtime-candidate-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string ProfileId =
        "id65-authoring-collapsed-35-part-cyclorama-probe-schema7-runtime-candidate-publisher-v1";
    public const string CandidateImageFileName =
        "unusedlevel65blank-authoring-collapsed-35-part-cyclorama-probe-schema7-candidate-v1.bin";
    public const string CandidateCueFileName =
        "unusedlevel65blank-authoring-collapsed-35-part-cyclorama-probe-schema7-candidate-v1.cue";
    public const string CandidateReceiptFileName =
        "unusedlevel65blank-authoring-collapsed-35-part-cyclorama-probe-schema7-candidate-v1.receipt.json";
    public const int WadLba =
        Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher.WadLba;
    public const int ExpectedWadByteLength =
        Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher.ExpectedWadByteLength;
    public const int ExecutableLba =
        Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher.ExecutableLba;
    public const int ExecutableByteLength =
        Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher.ExecutableByteLength;
    public const long ExpectedImageByteLength =
        Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher.ExpectedImageByteLength;
    public const int TargetDataWadEntry = 80;
    public const long TargetDataWadOffset =
        Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher.TargetDataWadOffset;
    public const int TargetDataByteLength =
        Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher.TargetDataByteLength;
    public const int FirstSkyRawSectorLba = 54_630;
    public const int SkyRawSectorCount = 17;
    public const int LastSkyRawSectorLba =
        FirstSkyRawSectorLba + SkyRawSectorCount - 1;

    private const int RawSectorByteLength = 2352;
    private const int LogicalSectorByteLength = 2048;
    private const int UserDataOffset = 24;
    private const byte XaDataSubmode = 0x08;
    private const long MaximumReceiptByteCount = 256 * 1024;
    private const long RetailTownSquareSkyBlockWadOffset = 0x13080A8;
    private const long RetailArtisansLinkedSkyBlockWadOffset = 0x96A7EC;
    private const int RetailSkyBlockByteLength = 0x84E4;
    private const string WriterLeaseFileName =
        ".collapsed-35-part-cyclorama-probe.writer.lock";
    private const string TemporaryPrefix =
        ".materialize-collapsed-35-part-cyclorama-probe-";

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions ReceiptJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true
    };

    public static async Task<
        Id65AuthoringCollapsedCycloramaProbeRuntimeCandidateResult> PublishAsync(
            Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePublishRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.ParentSchema7CandidateDirectoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputRootPath);
        ArgumentNullException.ThrowIfNull(request.Compiled);
        cancellationToken.ThrowIfCancellationRequested();
        RequireExactCompilerBoundary(request.Compiled);

        ParentCapture parent = await CaptureParentAsync(
            request.ParentSchema7CandidateDirectoryPath,
            request.Compiled,
            cancellationToken);
        string publicationIdentity = ComputePublicationIdentity(
            parent,
            request.Compiled);
        Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePaths paths =
            CreatePaths(
                request.OutputRootPath,
                request.Compiled.MobyPositionId,
                publicationIdentity);
        if (PathsEqual(
                request.ParentSchema7CandidateDirectoryPath,
                paths.CandidateDirectoryPath) ||
            IsWithin(
                request.ParentSchema7CandidateDirectoryPath,
                paths.CandidateDirectoryPath) ||
            IsWithin(
                paths.CandidateDirectoryPath,
                request.ParentSchema7CandidateDirectoryPath))
        {
            throw new InvalidDataException(
                "The collapsed-cyclorama probe destination overlaps its immutable schema-7 parent.");
        }

        bool rootCreated = !Directory.Exists(paths.CandidateRootPath);
        Directory.CreateDirectory(paths.CandidateRootPath);
        RefuseReparsePoint(paths.CandidateRootPath, "probe candidate root");
        using FileStream writerLease = AcquireWriterLease(paths.WriterLeasePath);
        ParentCapture leasedParent = await CaptureParentAsync(
            request.ParentSchema7CandidateDirectoryPath,
            request.Compiled,
            cancellationToken);
        RequireSameParent(parent, leasedParent);

        AtomicPublication<PublishedPayload> publication =
            await MaterializeAtomicallyAsync(
                paths.CandidateRootPath,
                paths.CandidateDirectoryPath,
                async stagedDirectory => await StageCandidateAsync(
                    stagedDirectory,
                    leasedParent,
                    request.Compiled,
                    publicationIdentity,
                    cancellationToken),
                async candidateDirectory => await VerifyPublishedDirectoryAsync(
                    candidateDirectory,
                    leasedParent,
                    request.Compiled,
                    publicationIdentity,
                    cancellationToken),
                cancellationToken);

        ParentCapture parentAfter = await CaptureParentAsync(
            request.ParentSchema7CandidateDirectoryPath,
            request.Compiled,
            cancellationToken);
        RequireSameParent(leasedParent, parentAfter);
        Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePaths finalPaths =
            PathsForDirectory(paths, paths.CandidateDirectoryPath);
        return new(
            finalPaths,
            publication.Value.Receipt,
            publication.Value.ReceiptFileSha256,
            publication.ReusedExisting,
            CandidateArtifactsWrittenThisCall: publication.PublishedThisCall,
            FileSystemMutationPerformed:
                rootCreated || publication.PublishedThisCall,
            RuntimeProven: false,
            NormalCreateBinEnabled: false,
            ReleaseIntegrated: false,
            Publishable: false);
    }

    private static void RequireExactCompilerBoundary(
        Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled)
    {
        byte[] baseline = compiled.CopySchema7BaselineRow80();
        byte[] output =
            Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                .ApplySchema7StageTransactional(
                    compiled,
                    baseline,
                    reverse: false);
        byte[] restored =
            Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                .ApplySchema7StageTransactional(
                    compiled,
                    output,
                    reverse: true);
        if (compiled.ProfileId !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.ProfileId ||
            compiled.SchemaVersion !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.SchemaVersion ||
            compiled.Document.SchemaVersion !=
                Id65AuthoringEditorDocument
                    .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion ||
            !compiled.ActiveTerrainUnitIds.SequenceEqual(
                [Id65AuthoringTerrainSetCatalog.Tile1Id]) ||
            compiled.MobyKind != Id65RemotePadMobyKind.GrassT107 ||
            compiled.MobyPositionId !=
                Id65RemotePadGrassT107Catalog.CenterPositionId ||
            compiled.Schema7BaselineRow80Sha256 !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                    .ExpectedSchema7Tile1GrassCenterRow80Sha256 ||
            compiled.OutputRow80Sha256 !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                    .ExpectedOutputRow80Sha256 ||
            compiled.BaselineCycloramaSha256 !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                    .ExpectedSchema7CycloramaSha256 ||
            compiled.OutputCycloramaSha256 !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                    .ExpectedOutputCycloramaSha256 ||
            compiled.OutputGeometrySha256 !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                    .ExpectedOutputGeometrySha256 ||
            compiled.OutputModelSha256 !=
                "8b1999117eee41e5083d12ef79f60a5c8628361c96cf8108f87cc529c213155f" ||
            compiled.EnvironmentSha256 !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                    .ExpectedSchema7Tile1EnvironmentSha256 ||
            compiled.OcclusionSha256 !=
                Id65AuthoringSingleSectorSubstrateCompiler
                    .ExpectedOutputOcclusionSha256 ||
            compiled.OcclusionTailSha256 !=
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedVisibilityOpaqueTailSha256 ||
            compiled.CollisionSha256 !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                    .ExpectedSchema7Tile1CollisionSha256 ||
            compiled.CycloramaRow80Offset !=
                Id65AuthoringBlankCycloramaCompiler
                    .OneTerrainCycloramaRow80Offset ||
            compiled.CycloramaWadOffset !=
                Id65AuthoringBlankCycloramaCompiler
                    .OneTerrainCycloramaWadOffset ||
            compiled.CycloramaByteLength !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                    .CycloramaByteLength ||
            WadLba + compiled.CycloramaWadOffset /
                    LogicalSectorByteLength != FirstSkyRawSectorLba ||
            (compiled.CycloramaWadOffset + compiled.CycloramaByteLength - 1) /
                    LogicalSectorByteLength + WadLba !=
                LastSkyRawSectorLba ||
            compiled.CandidateCollapsedVertexWordCount !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                    .ExpectedCandidateCollapsedVertexWordCount ||
            compiled.ChangedVertexWordCount !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                    .ExpectedChangedVertexWordCount ||
            compiled.ChangedLogicalByteCount !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                    .ExpectedChangedLogicalByteCount ||
            compiled.PolygonChangedByteCount != 0 ||
            compiled.OcclusionChangedByteCount != 0 ||
            compiled.OcclusionTailChangedByteCount != 0 ||
            !baseline.SequenceEqual(compiled.CopySchema7BaselineRow80()) ||
            !output.SequenceEqual(compiled.CopyOutputRow80()) ||
            !restored.SequenceEqual(baseline) ||
            Hash(baseline) != compiled.Schema7BaselineRow80Sha256 ||
            Hash(output) != compiled.OutputRow80Sha256 ||
            !compiled.PureInMemory || !compiled.TransitionalProbeOnly ||
            compiled.FullyOriginalCyclorama ||
            !compiled.ExactTile1GrassCenterSchema7BaselineRequired ||
            compiled.CallerSchema7AfterimageAccepted ||
            !compiled.FixedCycloramaCapacityPreserved ||
            !compiled.AllThirtyFivePartPointersPreservedExactly ||
            !compiled.AllThirtyFivePartHeadersPreservedExactly ||
            !compiled.PartCountsPreservedExactly ||
            !compiled.ColorTablesPreservedExactly ||
            !compiled.PolygonTablesPreservedExactly ||
            !compiled.FirstVertexWordsPreservedExactly ||
            !compiled.RemainingVertexWordsCollapsedPerPart ||
            !compiled.DownstreamComponentOffsetsPreserved ||
            !compiled.EnvironmentPreservedExactly ||
            !compiled.EntireOcclusionComponentPreservedExactly ||
            !compiled.SkyVisibilityDirectoryPreservedExactly ||
            !compiled.OcclusionOpaqueTailPreservedExactly ||
            !compiled.CollisionPreservedExactly ||
            !compiled.TexturePreservedExactly ||
            !compiled.SpawnAndMobyBytesPreservedExactly ||
            !compiled.ExactInverseVerified ||
            !compiled.EveryChangedByteOwned ||
            !compiled.OwnedRangesContainOnlyChangedBytes ||
            !compiled.ProtectedComplementVerified ||
            !compiled.ContainsOutputRow80Bytes ||
            compiled.WritesFileSystem || compiled.WritesDiscImage ||
            compiled.WritesCue || compiled.AppIntegrated ||
            compiled.UiIntegrated || compiled.DocumentPersistenceIntegrated ||
            compiled.MaterializerIntegrated || compiled.CreateBinEnabled ||
            compiled.NormalCreateBinEnabled ||
            compiled.RuntimeCandidateAuthorized || compiled.RuntimeProven ||
            compiled.ReleaseIntegrated || compiled.PromotionAuthorized ||
            compiled.Publishable)
        {
            throw new InvalidDataException(
                "The publisher requires the exact guarded collapsed-35-part cyclorama transitional probe compiler boundary.");
        }
        _ = BuildAndVerifyOwnership(compiled, baseline, output);
        VerifyCollapsedSkyReadback(compiled, baseline, output);
    }

    private static async Task<ParentCapture> CaptureParentAsync(
        string parentDirectory,
        Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled,
        CancellationToken cancellationToken)
    {
        string directory = Path.GetFullPath(parentDirectory);
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException(
                "The exact published schema-7 p1 parent candidate directory does not exist.");
        RefuseReparsePoint(directory, "schema-7 p1 parent directory");
        string receiptPath = Path.Combine(
            directory,
            Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
                .CandidateReceiptFileName);
        RefuseReparsePoint(receiptPath, "schema-7 p1 parent receipt");
        byte[] receiptBytes = await ReadBoundedFileAsync(
            receiptPath,
            MaximumReceiptByteCount,
            cancellationToken);
        Id65AuthoringSingleSectorSubstrateRuntimeCandidateReceipt receipt;
        try
        {
            receipt = JsonSerializer.Deserialize<
                Id65AuthoringSingleSectorSubstrateRuntimeCandidateReceipt>(
                    receiptBytes,
                    ReceiptJsonOptions) ?? throw new InvalidDataException(
                        "The schema-7 p1 parent receipt is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The schema-7 p1 parent receipt is not valid JSON.",
                exception);
        }
        byte[] canonicalReceiptBytes = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(receipt, ReceiptJsonOptions) + "\n");
        string receiptContentSha256 = Hash(
            JsonSerializer.SerializeToUtf8Bytes(
                receipt with { ReceiptContentSha256 = "" },
                CompactJsonOptions));
        if (!receiptBytes.SequenceEqual(canonicalReceiptBytes) ||
            receipt.ReceiptContentSha256 != receiptContentSha256 ||
            receipt.Format !=
                Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
                    .Format ||
            receipt.SchemaVersion !=
                Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
                    .ReceiptSchemaVersion ||
            receipt.ProfileId !=
                Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
                    .ProfileId ||
            receipt.CandidateImageFileName !=
                Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
                    .CandidateImageFileName ||
            receipt.CandidateCueFileName !=
                Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
                    .CandidateCueFileName ||
            receipt.CandidateReceiptFileName !=
                Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
                    .CandidateReceiptFileName ||
            receipt.DocumentCanonicalSha256 !=
                compiled.DocumentCanonicalSha256 ||
            receipt.ActiveTerrainUnitIdsCanonical !=
                string.Join('\n', compiled.ActiveTerrainUnitIds) ||
            receipt.MobyKindId !=
                Id65RemotePadMobyChoiceCatalog.GetKindId(compiled.MobyKind) ||
            receipt.MobyPositionId != compiled.MobyPositionId ||
            receipt.CompilerProfileId !=
                Id65AuthoringSingleSectorSubstrateCompiler.ProfileId ||
            receipt.CompilerSchemaVersion !=
                Id65AuthoringSingleSectorSubstrateCompiler.SchemaVersion ||
            receipt.CompilerDeterministicPlanSha256 !=
                compiled.Schema7DeterministicPlanSha256 ||
            receipt.OutputRow80Sha256 !=
                compiled.Schema7BaselineRow80Sha256 ||
            receipt.OutputModelSha256 !=
                compiled.Schema7BaselineModelSha256 ||
            receipt.OutputCycloramaSha256 !=
                compiled.BaselineCycloramaSha256 ||
            receipt.OutputEnvironmentSha256 != compiled.EnvironmentSha256 ||
            receipt.OutputOcclusionSha256 != compiled.OcclusionSha256 ||
            receipt.CollisionSha256 != compiled.CollisionSha256 ||
            receipt.OutputTextureComponentSha256 != compiled.TextureSha256 ||
            receipt.TargetDataWadEntry != TargetDataWadEntry ||
            receipt.TargetDataWadOffset != TargetDataWadOffset ||
            receipt.TargetDataByteLength != TargetDataByteLength ||
            receipt.FirstRebuiltRawSectorLba !=
                Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
                    .FirstRebuiltRawSectorLba ||
            receipt.RebuiltRawSectorCount !=
                Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
                    .RebuiltRawSectorCount ||
            !receipt.ExactParentReceiptVerified ||
            !receipt.ExactParentImageVerified ||
            !receipt.ExactSchema6StagePreimageVerified ||
            !receipt.ExactSchema6StageInverseVerified ||
            !receipt.ExactOutputRow80ReadbackVerified ||
            !receipt.SingleSectorEnvironmentReadbackVerified ||
            !receipt.SingleSectorOcclusionReadbackVerified ||
            !receipt.CollisionPreservedExactly ||
            !receipt.CycloramaPreservedExactly ||
            !receipt.SpawnAndMobyBytesPreservedExactly ||
            !receipt.RawSectorIntegrityVerified ||
            !receipt.PhysicalComplementVerified ||
            !receipt.ParentSchema6CandidatePreserved ||
            !receipt.ContainsFullDiscImage || !receipt.ContainsCue ||
            !receipt.SeparateGuardedCandidatePublisherAuthorized ||
            receipt.RuntimeCandidateAuthorized || receipt.RuntimeProven ||
            receipt.AppIntegrated || receipt.UiIntegrated ||
            receipt.CreateBinEnabled || receipt.NormalCreateBinEnabled ||
            receipt.DocumentPersistenceIntegrated ||
            receipt.ReleaseIntegrated || receipt.PromotionAuthorized ||
            receipt.Publishable)
        {
            throw new InvalidDataException(
                "The parent receipt is not the exact guarded Tile1 / Grass T107 / Center schema-7 p1 publication required by this probe.");
        }

        string expectedDirectoryName =
            $"{receipt.MobyPositionId}-schema7-single-sector-substrate-{receipt.PublicationIdentitySha256}";
        if (Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)) !=
            expectedDirectoryName)
        {
            throw new InvalidDataException(
                "The schema-7 p1 parent directory name is not its exact content-addressed publication identity.");
        }

        string imagePath = Path.Combine(directory, receipt.CandidateImageFileName);
        string cuePath = Path.Combine(directory, receipt.CandidateCueFileName);
        RefuseReparsePoint(imagePath, "schema-7 p1 parent image");
        RefuseReparsePoint(cuePath, "schema-7 p1 parent cue");
        if (!File.Exists(imagePath) || !File.Exists(cuePath))
            throw new FileNotFoundException(
                "The schema-7 p1 parent candidate artifact set is incomplete.");
        string cue = await File.ReadAllTextAsync(
            cuePath,
            Encoding.ASCII,
            cancellationToken);
        if (cue != BuildCanonicalCue(receipt.CandidateImageFileName) ||
            Hash(Encoding.ASCII.GetBytes(cue)) != receipt.CandidateCueSha256)
        {
            throw new InvalidDataException(
                "The schema-7 p1 parent CUE failed exact readback.");
        }
        string imageSha256 = await HashFileAsync(imagePath, cancellationToken);
        if (imageSha256 != receipt.OutputImageSha256)
            throw new InvalidDataException(
                "The immutable schema-7 p1 parent BIN does not match its receipt.");

        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        if (layout.SectorSize != RawSectorByteLength ||
            layout.UserOffset != UserDataOffset)
        {
            throw new InvalidDataException(
                "The schema-7 p1 parent is not the exact MODE2/2352 layout.");
        }
        using FileStream parentImage = new(
            imagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        if (parentImage.Length != ExpectedImageByteLength)
            throw new InvalidDataException(
                "The schema-7 p1 parent full-disc length changed.");
        DiscFileRecord wad = FindWad(parentImage, layout);
        DiscFileRecord executable = FindExecutable(parentImage, layout);
        if (wad.Lba != WadLba || wad.Size != ExpectedWadByteLength ||
            executable.Lba != ExecutableLba ||
            executable.Size != ExecutableByteLength)
        {
            throw new InvalidDataException(
                "A schema-7 p1 parent disc extent changed.");
        }
        byte[] row = DiscImage.ReadFileBytes(
            parentImage,
            layout,
            WadLba,
            TargetDataWadOffset,
            TargetDataByteLength);
        if (!row.SequenceEqual(compiled.CopySchema7BaselineRow80()) ||
            Hash(row) != receipt.OutputRow80Sha256)
        {
            throw new InvalidDataException(
                "The schema-7 p1 parent WAD80 row is not the compiler's exact preimage.");
        }
        return new(
            directory,
            imagePath,
            cuePath,
            receiptPath,
            receipt,
            imageSha256,
            Hash(receiptBytes));
    }

    private static async Task<PublishedPayload> StageCandidateAsync(
        string stagedDirectory,
        ParentCapture parent,
        Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled,
        string publicationIdentity,
        CancellationToken cancellationToken)
    {
        Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePaths paths =
            PathsForDirectory(
                CreatePaths(
                    Path.GetDirectoryName(stagedDirectory) ?? stagedDirectory,
                    compiled.MobyPositionId,
                    publicationIdentity),
                stagedDirectory);
        await CopyExactParentImageAsync(
            parent.ImagePath,
            paths.CandidateImagePath,
            parent.ImageSha256,
            cancellationToken);
        ApplyAndRebuildExactProbeStage(paths.CandidateImagePath, compiled);
        CandidateReadback readback = await VerifyCandidateAsync(
            parent,
            paths.CandidateImagePath,
            compiled,
            cancellationToken);
        Id65AuthoringCollapsedCycloramaProbeRuntimeCandidateReceipt receipt =
            BuildReceipt(parent, compiled, publicationIdentity, readback);
        await WriteNewFileAsync(
            paths.CandidateCuePath,
            Encoding.ASCII.GetBytes(BuildCanonicalCue(CandidateImageFileName)),
            cancellationToken);
        await WriteNewFileAsync(
            paths.CandidateReceiptPath,
            SerializeReceipt(receipt),
            cancellationToken);
        string receiptFileSha256 = await RequireExactPublishedArtifactsAsync(
            paths,
            receipt,
            cancellationToken);
        return new(receipt, receiptFileSha256);
    }

    private static async Task<PublishedPayload> VerifyPublishedDirectoryAsync(
        string candidateDirectory,
        ParentCapture parent,
        Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled,
        string publicationIdentity,
        CancellationToken cancellationToken)
    {
        Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePaths paths =
            PathsForDirectory(
                CreatePaths(
                    Path.GetDirectoryName(candidateDirectory) ??
                        candidateDirectory,
                    compiled.MobyPositionId,
                    publicationIdentity),
                candidateDirectory);
        CandidateReadback readback = await VerifyCandidateAsync(
            parent,
            paths.CandidateImagePath,
            compiled,
            cancellationToken);
        Id65AuthoringCollapsedCycloramaProbeRuntimeCandidateReceipt receipt =
            BuildReceipt(parent, compiled, publicationIdentity, readback);
        string receiptFileSha256 = await RequireExactPublishedArtifactsAsync(
            paths,
            receipt,
            cancellationToken);
        return new(receipt, receiptFileSha256);
    }

    private static void ApplyAndRebuildExactProbeStage(
        string stagedImagePath,
        Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled)
    {
        DiscLayout layout = DiscImage.DetectLayout(stagedImagePath);
        if (layout.SectorSize != RawSectorByteLength ||
            layout.UserOffset != UserDataOffset)
        {
            throw new InvalidDataException(
                "The staged probe changed the raw disc layout.");
        }
        using FileStream image = new(
            stagedImagePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        DiscFileRecord wad = FindWad(image, layout);
        DiscFileRecord executable = FindExecutable(image, layout);
        if (wad.Lba != WadLba || wad.Size != ExpectedWadByteLength ||
            executable.Lba != ExecutableLba ||
            executable.Size != ExecutableByteLength)
        {
            throw new InvalidDataException(
                "The staged probe changed a guarded disc extent.");
        }
        byte[] parentRow = DiscImage.ReadFileBytes(
            image,
            layout,
            WadLba,
            TargetDataWadOffset,
            TargetDataByteLength);
        byte[] outputRow =
            Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                .ApplySchema7StageTransactional(
                    compiled,
                    parentRow,
                    reverse: false);
        if (!outputRow.SequenceEqual(compiled.CopyOutputRow80()))
            throw new InvalidDataException(
                "The collapsed-cyclorama probe afterimage changed before disc write.");
        byte[] outputCyclorama = outputRow.AsSpan(
            compiled.CycloramaRow80Offset,
            compiled.CycloramaByteLength).ToArray();
        DiscImage.WriteFileBytes(
            image,
            layout,
            WadLba,
            compiled.CycloramaWadOffset,
            outputCyclorama);
        int rebuilt = RawMode2Form1SectorIntegrity.RebuildFileRanges(
            image,
            layout,
            WadLba,
            [(compiled.CycloramaWadOffset, compiled.CycloramaByteLength)]);
        int verified = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            image,
            layout,
            [(FirstSkyRawSectorLba, SkyRawSectorCount)]);
        if (rebuilt != SkyRawSectorCount || verified != SkyRawSectorCount)
            throw new InvalidDataException(
                "The collapsed-cyclorama probe raw-sector rebuild envelope changed.");
        VerifySkySectorSubmodes(image, layout);
        image.Flush(flushToDisk: true);
    }

    private static async Task<CandidateReadback> VerifyCandidateAsync(
        ParentCapture parent,
        string candidateImagePath,
        Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled,
        CancellationToken cancellationToken)
    {
        RefuseReparsePoint(candidateImagePath, "probe candidate image");
        if (!File.Exists(candidateImagePath))
            throw new FileNotFoundException(
                "The collapsed-cyclorama probe BIN is missing.");
        DiscLayout parentLayout = DiscImage.DetectLayout(parent.ImagePath);
        DiscLayout outputLayout = DiscImage.DetectLayout(candidateImagePath);
        if (parentLayout != outputLayout ||
            outputLayout.SectorSize != RawSectorByteLength ||
            outputLayout.UserOffset != UserDataOffset)
        {
            throw new InvalidDataException(
                "The probe candidate changed the raw disc layout.");
        }
        await using FileStream parentImage = new(
            parent.ImagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1 << 20,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using FileStream outputImage = new(
            candidateImagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1 << 20,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (parentImage.Length != ExpectedImageByteLength ||
            outputImage.Length != parentImage.Length)
        {
            throw new InvalidDataException(
                "The probe changed the full-disc image length.");
        }
        DiscFileRecord parentWad = FindWad(parentImage, parentLayout);
        DiscFileRecord outputWad = FindWad(outputImage, outputLayout);
        DiscFileRecord parentExecutable = FindExecutable(
            parentImage,
            parentLayout);
        DiscFileRecord outputExecutable = FindExecutable(
            outputImage,
            outputLayout);
        if (parentWad != outputWad ||
            parentExecutable != outputExecutable ||
            outputWad.Lba != WadLba ||
            outputWad.Size != ExpectedWadByteLength ||
            outputExecutable.Lba != ExecutableLba ||
            outputExecutable.Size != ExecutableByteLength)
        {
            throw new InvalidDataException(
                "The probe moved or resized a guarded disc file.");
        }

        byte[] parentRow = DiscImage.ReadFileBytes(
            parentImage,
            parentLayout,
            WadLba,
            TargetDataWadOffset,
            TargetDataByteLength);
        byte[] outputRow = DiscImage.ReadFileBytes(
            outputImage,
            outputLayout,
            WadLba,
            TargetDataWadOffset,
            TargetDataByteLength);
        if (!parentRow.SequenceEqual(compiled.CopySchema7BaselineRow80()) ||
            !outputRow.SequenceEqual(compiled.CopyOutputRow80()) ||
            Hash(parentRow) != compiled.Schema7BaselineRow80Sha256 ||
            Hash(outputRow) != compiled.OutputRow80Sha256)
        {
            throw new InvalidDataException(
                "The collapsed-cyclorama probe WAD80 row failed exact readback.");
        }
        byte[] restored =
            Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                .ApplySchema7StageTransactional(
                    compiled,
                    outputRow,
                    reverse: true);
        if (!restored.SequenceEqual(parentRow))
            throw new InvalidDataException(
                "The probe failed exact schema-7 p2 inverse readback.");
        bool[] owned = BuildAndVerifyOwnership(compiled, parentRow, outputRow);
        VerifyCollapsedSkyReadback(compiled, parentRow, outputRow);
        VerifyOcclusionReadback(parent, compiled, parentRow, outputRow);

        RequireEqualWadComponent(
            parentImage, outputImage, parentLayout,
            0x6927000, 0xF800, "ID65 WAD79 overlay");
        RequireEqualWadComponent(
            parentImage, outputImage, parentLayout,
            0x118E800, 0xF800, "retail Town Square WAD15 overlay");
        RequireEqualWadComponent(
            parentImage, outputImage, parentLayout,
            0x119E000, TargetDataByteLength,
            "retail Town Square WAD16 data");
        RequireEqualWadComponent(
            parentImage, outputImage, parentLayout,
            0x7F2800, 0xE000, "retail Artisans WAD9 overlay");
        RequireEqualWadComponent(
            parentImage, outputImage, parentLayout,
            0x800800, 0x383000, "retail Artisans WAD10 data");
        RequireEqualWadComponent(
            parentImage, outputImage, parentLayout,
            RetailTownSquareSkyBlockWadOffset,
            RetailSkyBlockByteLength,
            "retail Town Square sky block");
        RequireEqualWadComponent(
            parentImage, outputImage, parentLayout,
            RetailArtisansLinkedSkyBlockWadOffset,
            RetailSkyBlockByteLength,
            "retail Artisans linked sky block");
        RequireEqualFileComponent(
            parentImage, outputImage, parentLayout,
            ExecutableLba, 0, ExecutableByteLength,
            "SCUS_942.28 executable");

        int verified = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            outputImage,
            outputLayout,
            [(FirstSkyRawSectorLba, SkyRawSectorCount)]);
        if (verified != SkyRawSectorCount)
            throw new InvalidDataException(
                "The probe sky sectors failed final MODE2 EDC/ECC/MSF readback.");
        VerifySkySectorSubmodes(outputImage, outputLayout);
        PhysicalDiff physical = await ComparePhysicalImagesAsync(
            parentImage,
            outputImage,
            owned,
            compiled.ChangedLogicalByteCount,
            cancellationToken);
        string outputImageSha256 = await HashFileAsync(
            candidateImagePath,
            cancellationToken);
        return new(
            outputImageSha256,
            physical.ChangedRawSectorCount,
            physical.ChangedPhysicalBytes,
            physical.DiffSha256);
    }

    private static bool[] BuildAndVerifyOwnership(
        Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled,
        ReadOnlySpan<byte> baseline,
        ReadOnlySpan<byte> output)
    {
        if (baseline.Length != TargetDataByteLength ||
            output.Length != TargetDataByteLength ||
            compiled.OwnedRanges.Count == 0 ||
            compiled.OwnedRanges.Count != compiled.InverseRanges.Count)
        {
            throw new InvalidDataException(
                "The probe ownership ledger is empty, asymmetric, or bound to the wrong WAD row.");
        }
        bool[] owned = new bool[TargetDataByteLength];
        int ledgerChangedBytes = 0;
        int priorEnd = -1;
        foreach (Id65AuthoringCompositeOwnedRange range in compiled.OwnedRanges)
        {
            int end = checked(range.DataRelativeOffset + range.ByteLength);
            byte[] before = range.CopyBefore();
            byte[] after = range.CopyAfter();
            if (range.OwnerId !=
                    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.OwnerId ||
                range.DataRelativeOffset < compiled.CycloramaRow80Offset ||
                end > compiled.CycloramaRow80Offset +
                    compiled.CycloramaByteLength ||
                range.DataRelativeOffset < priorEnd ||
                range.ByteLength <= 0 ||
                before.Length != range.ByteLength ||
                after.Length != range.ByteLength ||
                !baseline.Slice(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(before) ||
                !output.Slice(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(after))
            {
                throw new InvalidDataException(
                    "A collapsed-cyclorama owned range failed exact before/after readback.");
            }
            for (int offset = range.DataRelativeOffset; offset < end; offset++)
            {
                if (owned[offset] || baseline[offset] == output[offset])
                    throw new InvalidDataException(
                        "The probe ledger overlaps or owns an unchanged byte.");
                owned[offset] = true;
                ledgerChangedBytes++;
            }
            priorEnd = end;
        }
        int changed = 0;
        for (int offset = 0; offset < baseline.Length; offset++)
        {
            if (baseline[offset] == output[offset]) continue;
            changed++;
            if (!owned[offset])
                throw new InvalidDataException(
                    $"The probe changed unowned WAD80 byte 0x{offset:X}.");
        }
        if (changed != ledgerChangedBytes ||
            changed != compiled.ChangedLogicalByteCount)
        {
            throw new InvalidDataException(
                "The collapsed-cyclorama changed-byte cardinality drifted.");
        }
        return owned;
    }

    private static void VerifyCollapsedSkyReadback(
        Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled,
        ReadOnlySpan<byte> parentRow,
        ReadOnlySpan<byte> outputRow)
    {
        byte[] baseline = parentRow.Slice(
            compiled.CycloramaRow80Offset,
            compiled.CycloramaByteLength).ToArray();
        byte[] output = outputRow.Slice(
            compiled.CycloramaRow80Offset,
            compiled.CycloramaByteLength).ToArray();
        Spyro1SkyBlockLayout beforeLayout =
            Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(baseline);
        Spyro1SkyBlockLayout afterLayout =
            Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(output);
        if (Hash(baseline) !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                    .ExpectedSchema7CycloramaSha256 ||
            Hash(output) !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                    .ExpectedOutputCycloramaSha256 ||
            !string.Equals(
                afterLayout.GeometrySha256,
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                    .ExpectedOutputGeometrySha256,
                StringComparison.OrdinalIgnoreCase) ||
            beforeLayout.PartCount != 35 || afterLayout.PartCount != 35 ||
            beforeLayout.VertexCount != 1_915 ||
            afterLayout.VertexCount != 1_915 ||
            beforeLayout.ColorCount != 726 || afterLayout.ColorCount != 726 ||
            beforeLayout.PolygonCount != 2_808 ||
            afterLayout.PolygonCount != 2_808 ||
            beforeLayout.PaletteWordCount != afterLayout.PaletteWordCount ||
            beforeLayout.TrailingBytes != afterLayout.TrailingBytes ||
            beforeLayout.BackgroundColor != afterLayout.BackgroundColor ||
            beforeLayout.BackgroundColorOffset !=
                afterLayout.BackgroundColorOffset ||
            beforeLayout.Parts.Count != afterLayout.Parts.Count)
        {
            throw new InvalidDataException(
                "The published probe sky identity or 35-part structure changed.");
        }

        bool[] allowedVertexBytes = new bool[compiled.CycloramaByteLength];
        int candidateWords = 0;
        int changedWords = 0;
        int changedPolygonBytes = 0;
        for (int partIndex = 0;
             partIndex < beforeLayout.Parts.Count;
             partIndex++)
        {
            Spyro1SkyPartLayout beforePart = beforeLayout.Parts[partIndex];
            Spyro1SkyPartLayout afterPart = afterLayout.Parts[partIndex];
            if (beforePart != afterPart)
                throw new InvalidDataException(
                    $"Probe sky part {partIndex} changed its pointer, header, counts, or table layout.");
            if (!baseline.AsSpan(beforePart.PartOffset, 24)
                    .SequenceEqual(output.AsSpan(afterPart.PartOffset, 24)))
            {
                throw new InvalidDataException(
                    $"Probe sky part {partIndex} changed its 24-byte header.");
            }
            ReadOnlySpan<byte> firstVertex = baseline.AsSpan(
                beforePart.VertexTableOffset,
                4);
            if (!firstVertex.SequenceEqual(output.AsSpan(
                    afterPart.VertexTableOffset,
                    4)))
            {
                throw new InvalidDataException(
                    $"Probe sky part {partIndex} changed its first packed vertex word.");
            }
            for (int vertex = 1; vertex < beforePart.VertexCount; vertex++)
            {
                candidateWords++;
                int offset = checked(beforePart.VertexTableOffset + vertex * 4);
                for (int index = 0; index < 4; index++)
                    allowedVertexBytes[offset + index] = true;
                if (!output.AsSpan(offset, 4).SequenceEqual(firstVertex))
                    throw new InvalidDataException(
                        $"Probe sky part {partIndex} vertex {vertex} does not equal its own first packed word.");
                if (!baseline.AsSpan(offset, 4).SequenceEqual(firstVertex))
                    changedWords++;
            }
            int colorBytes = checked(beforePart.ColorCount * 4);
            if (!baseline.AsSpan(beforePart.ColorTableOffset, colorBytes)
                    .SequenceEqual(output.AsSpan(
                        afterPart.ColorTableOffset,
                        colorBytes)))
            {
                throw new InvalidDataException(
                    $"Probe sky part {partIndex} changed its color table.");
            }
            int polygonBytes = checked(beforePart.PolygonCount * 8);
            ReadOnlySpan<byte> beforePolygons = baseline.AsSpan(
                beforePart.PolygonTableOffset,
                polygonBytes);
            ReadOnlySpan<byte> afterPolygons = output.AsSpan(
                afterPart.PolygonTableOffset,
                polygonBytes);
            changedPolygonBytes += CountChangedBytes(
                beforePolygons,
                afterPolygons);
        }
        int changedBytes = 0;
        for (int index = 0; index < baseline.Length; index++)
        {
            if (baseline[index] == output[index]) continue;
            changedBytes++;
            if (!allowedVertexBytes[index])
                throw new InvalidDataException(
                    $"The probe changed non-vertex cyclorama byte 0x{index:X}.");
        }
        if (candidateWords !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                    .ExpectedCandidateCollapsedVertexWordCount ||
            changedWords !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                    .ExpectedChangedVertexWordCount ||
            changedBytes !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                    .ExpectedChangedLogicalByteCount ||
            changedPolygonBytes != 0 ||
            candidateWords != compiled.CandidateCollapsedVertexWordCount ||
            changedWords != compiled.ChangedVertexWordCount ||
            changedBytes != compiled.ChangedLogicalByteCount)
        {
            throw new InvalidDataException(
                "The published collapsed-vertex word/byte census changed.");
        }
    }

    private static void VerifyOcclusionReadback(
        ParentCapture parent,
        Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled,
        ReadOnlySpan<byte> parentRow,
        ReadOnlySpan<byte> outputRow)
    {
        int offset = parent.Receipt.OcclusionRow80Offset;
        int length = parent.Receipt.OcclusionByteLength;
        ReadOnlySpan<byte> before = parentRow.Slice(offset, length);
        ReadOnlySpan<byte> after = outputRow.Slice(offset, length);
        int tailOffset =
            Id65AuthoringBlankCycloramaCompiler.VisibilityOpaqueTailOffset;
        int tailLength =
            Id65AuthoringBlankCycloramaCompiler.VisibilityOpaqueTailByteLength;
        if (!before.SequenceEqual(after) ||
            Hash(after) != compiled.OcclusionSha256 ||
            CountChangedBytes(before, after) != 0 ||
            !before.Slice(tailOffset, tailLength)
                .SequenceEqual(after.Slice(tailOffset, tailLength)) ||
            Hash(after.Slice(tailOffset, tailLength)) !=
                compiled.OcclusionTailSha256)
        {
            throw new InvalidDataException(
                "The probe changed the entire occlusion component or its opaque sky-visibility tail.");
        }
        Id65AuthoringSingleSectorSubstrateCompiler
            .RequireSingleSectorOcclusion(after);
    }

    private static async Task<PhysicalDiff> ComparePhysicalImagesAsync(
        FileStream parent,
        FileStream output,
        IReadOnlyList<bool> owned,
        int expectedChangedLogicalBytes,
        CancellationToken cancellationToken)
    {
        parent.Position = 0;
        output.Position = 0;
        byte[] before = new byte[RawSectorByteLength];
        byte[] after = new byte[RawSectorByteLength];
        long changedPhysicalBytes = 0;
        int changedLogicalBytes = 0;
        int changedSectors = 0;
        using IncrementalHash diffHash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] identity = new byte[10];
        int sectorCount = checked((int)(parent.Length / RawSectorByteLength));
        for (int lba = 0; lba < sectorCount; lba++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await parent.ReadExactlyAsync(before, cancellationToken);
            await output.ReadExactlyAsync(after, cancellationToken);
            if (before.AsSpan().SequenceEqual(after)) continue;
            changedSectors++;
            if (lba < FirstSkyRawSectorLba || lba > LastSkyRawSectorLba)
                throw new InvalidDataException(
                    $"The probe changed raw sector {lba} outside the exact cyclorama span.");
            if (!before.AsSpan(0, UserDataOffset)
                    .SequenceEqual(after.AsSpan(0, UserDataOffset)))
            {
                throw new InvalidDataException(
                    $"The probe changed MODE2 header/subheader bytes in raw sector {lba}.");
            }
            bool sectorHasOwnedLogicalChange = false;
            for (int index = UserDataOffset;
                 index < UserDataOffset + LogicalSectorByteLength;
                 index++)
            {
                if (before[index] == after[index]) continue;
                long wadOffset = ((long)lba - WadLba) *
                    LogicalSectorByteLength + (index - UserDataOffset);
                long rowRelative = wadOffset - TargetDataWadOffset;
                if (rowRelative < 0 || rowRelative >= owned.Count ||
                    !owned[checked((int)rowRelative)])
                {
                    throw new InvalidDataException(
                        $"The probe changed unowned logical WAD byte 0x{wadOffset:X}.");
                }
                changedLogicalBytes++;
                sectorHasOwnedLogicalChange = true;
            }
            if (!sectorHasOwnedLogicalChange)
                throw new InvalidDataException(
                    "A probe raw sector changed without an owned cyclorama payload change.");
            for (int index = 0; index < RawSectorByteLength; index++)
            {
                if (before[index] == after[index]) continue;
                changedPhysicalBytes++;
                BinaryPrimitives.WriteInt32LittleEndian(
                    identity.AsSpan(0, 4),
                    lba);
                BinaryPrimitives.WriteInt32LittleEndian(
                    identity.AsSpan(4, 4),
                    index);
                identity[8] = before[index];
                identity[9] = after[index];
                diffHash.AppendData(identity);
            }
        }
        if (parent.Position != parent.Length ||
            output.Position != output.Length ||
            changedSectors <= 0 || changedSectors > SkyRawSectorCount ||
            changedPhysicalBytes <= 0 ||
            changedLogicalBytes != expectedChangedLogicalBytes)
        {
            throw new InvalidDataException(
                "The probe physical diff is empty, escaped the sky sectors, or has the wrong logical cardinality.");
        }
        return new(
            changedSectors,
            changedPhysicalBytes,
            Convert.ToHexStringLower(diffHash.GetHashAndReset()));
    }

    private static Id65AuthoringCollapsedCycloramaProbeRuntimeCandidateReceipt
        BuildReceipt(
            ParentCapture parent,
            Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled,
            string publicationIdentity,
            CandidateReadback readback)
    {
        Id65AuthoringCollapsedCycloramaProbeRuntimeCandidateReceipt receipt =
            new()
            {
                Format = Format,
                SchemaVersion = ReceiptSchemaVersion,
                ProfileId = ProfileId,
                PublicationIdentitySha256 = publicationIdentity,
                ParentReceiptFormat = parent.Receipt.Format,
                ParentReceiptSchemaVersion = parent.Receipt.SchemaVersion,
                ParentReceiptProfileId = parent.Receipt.ProfileId,
                ParentCandidateDirectoryName =
                    Path.GetFileName(Path.TrimEndingDirectorySeparator(
                        parent.DirectoryPath)),
                ParentCandidateImageFileName =
                    parent.Receipt.CandidateImageFileName,
                ParentCandidateCueFileName =
                    parent.Receipt.CandidateCueFileName,
                ParentCandidateReceiptFileName =
                    parent.Receipt.CandidateReceiptFileName,
                ParentCandidateImageSha256 = parent.ImageSha256,
                ParentCandidateCueSha256 = parent.Receipt.CandidateCueSha256,
                ParentCandidateReceiptSha256 = parent.ReceiptFileSha256,
                ParentReceiptContentSha256 =
                    parent.Receipt.ReceiptContentSha256,
                ParentPublicationIdentitySha256 =
                    parent.Receipt.PublicationIdentitySha256,
                DocumentCanonicalSha256 = compiled.DocumentCanonicalSha256,
                ActiveTerrainUnitIdsCanonical =
                    string.Join('\n', compiled.ActiveTerrainUnitIds),
                MobyKindId =
                    Id65RemotePadMobyChoiceCatalog.GetKindId(compiled.MobyKind),
                MobyPositionId = compiled.MobyPositionId,
                CompilerProfileId = compiled.ProfileId,
                CompilerSchemaVersion = compiled.SchemaVersion,
                CompilerDeterministicPlanSha256 =
                    compiled.DeterministicPlanSha256,
                Schema7CompilerDeterministicPlanSha256 =
                    compiled.Schema7DeterministicPlanSha256,
                BaselineRow80Sha256 = compiled.Schema7BaselineRow80Sha256,
                OutputRow80Sha256 = compiled.OutputRow80Sha256,
                BaselineModelSha256 = compiled.Schema7BaselineModelSha256,
                OutputModelSha256 = compiled.OutputModelSha256,
                CycloramaRow80Offset = compiled.CycloramaRow80Offset,
                CycloramaWadOffset = compiled.CycloramaWadOffset,
                CycloramaByteLength = compiled.CycloramaByteLength,
                BaselineCycloramaSha256 = compiled.BaselineCycloramaSha256,
                OutputCycloramaSha256 = compiled.OutputCycloramaSha256,
                OutputGeometrySha256 = compiled.OutputGeometrySha256,
                CycloramaPartCount = compiled.OutputLayout.PartCount,
                CycloramaVertexCount = compiled.OutputLayout.VertexCount,
                CycloramaColorCount = compiled.OutputLayout.ColorCount,
                CycloramaPolygonCount = compiled.OutputLayout.PolygonCount,
                CandidateCollapsedVertexWordCount =
                    compiled.CandidateCollapsedVertexWordCount,
                ChangedVertexWordCount = compiled.ChangedVertexWordCount,
                ChangedLogicalByteCount = compiled.ChangedLogicalByteCount,
                PolygonChangedByteCount = compiled.PolygonChangedByteCount,
                OcclusionChangedByteCount = compiled.OcclusionChangedByteCount,
                OcclusionTailChangedByteCount =
                    compiled.OcclusionTailChangedByteCount,
                EnvironmentSha256 = compiled.EnvironmentSha256,
                OcclusionSha256 = compiled.OcclusionSha256,
                OcclusionTailSha256 = compiled.OcclusionTailSha256,
                CollisionSha256 = compiled.CollisionSha256,
                TextureSha256 = compiled.TextureSha256,
                OwnedRangeMapSha256 = compiled.OwnedRangeMapSha256,
                TransactionSha256 = compiled.TransactionSha256,
                ProtectedComplementSha256 =
                    compiled.ProtectedComplementSha256,
                CandidateImageFileName = CandidateImageFileName,
                CandidateCueFileName = CandidateCueFileName,
                CandidateReceiptFileName = CandidateReceiptFileName,
                OutputImageSha256 = readback.OutputImageSha256,
                CandidateCueSha256 = Hash(Encoding.ASCII.GetBytes(
                    BuildCanonicalCue(CandidateImageFileName))),
                TargetDataWadEntry = TargetDataWadEntry,
                TargetDataWadOffset = TargetDataWadOffset,
                TargetDataByteLength = TargetDataByteLength,
                FirstSkyRawSectorLba = FirstSkyRawSectorLba,
                SkyRawSectorCount = SkyRawSectorCount,
                ChangedRawSectorCount = readback.ChangedRawSectorCount,
                ChangedPhysicalImageBytes = readback.ChangedPhysicalBytes,
                PhysicalDiffSha256 = readback.PhysicalDiffSha256,
                ExactParentReceiptVerified = true,
                ExactParentImageVerified = true,
                ExactSchema7ProbePreimageVerified = true,
                ExactSchema7ProbeInverseVerified = true,
                ExactOutputRow80ReadbackVerified = true,
                ExactOutputCycloramaReadbackVerified = true,
                AllThirtyFivePartPointersPreservedExactly = true,
                AllThirtyFivePartHeadersPreservedExactly = true,
                PartCountsPreservedExactly = true,
                ColorTablesPreservedExactly = true,
                PolygonTablesPreservedExactly = true,
                FirstVertexWordsPreservedExactly = true,
                RemainingVertexWordsCollapsedPerPart = true,
                EntireOcclusionComponentPreservedExactly = true,
                OcclusionOpaqueTailPreservedExactly = true,
                CollisionPreservedExactly = true,
                EnvironmentPreservedExactly = true,
                TexturePreservedExactly = true,
                SpawnAndMobyBytesPreservedExactly = true,
                OnlySkyIntersectingRawSectorsChanged = true,
                Wad79Preserved = true,
                RetailTownSquareWad15Preserved = true,
                RetailTownSquareWad16Preserved = true,
                RetailArtisansWad9Preserved = true,
                RetailArtisansWad10Preserved = true,
                RetailTownSquareSkyBlockPreserved = true,
                RetailArtisansLinkedSkyBlockPreserved = true,
                ExecutablePreserved = true,
                RawSectorIntegrityVerified = true,
                BcdMsfVerified = true,
                DuplicatedSubheadersVerified = true,
                PhysicalComplementVerified = true,
                ParentSchema7CandidatePreserved = true,
                ContainsFullDiscImage = true,
                ContainsCue = true,
                TransitionalProbeOnly = true,
                FullyOriginalCyclorama = false,
                SeparateGuardedCandidatePublisherAuthorized = true,
                RuntimeCandidateAuthorized = false,
                RuntimeProven = false,
                AppIntegrated = false,
                UiIntegrated = false,
                DocumentPersistenceIntegrated = false,
                CreateBinEnabled = false,
                NormalCreateBinEnabled = false,
                ReleaseIntegrated = false,
                PromotionAuthorized = false,
                Publishable = false,
                ReceiptContentSha256 = ""
            };
        return receipt with
        {
            ReceiptContentSha256 = ComputeReceiptContentSha256(receipt)
        };
    }

    private static async Task<string> RequireExactPublishedArtifactsAsync(
        Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePaths paths,
        Id65AuthoringCollapsedCycloramaProbeRuntimeCandidateReceipt expected,
        CancellationToken cancellationToken)
    {
        RefuseReparsePoint(paths.CandidateDirectoryPath,
            "probe candidate directory");
        RefuseReparsePoint(paths.CandidateImagePath, "probe candidate BIN");
        RefuseReparsePoint(paths.CandidateCuePath, "probe candidate CUE");
        RefuseReparsePoint(paths.CandidateReceiptPath,
            "probe candidate receipt");
        string[] entries = Directory.EnumerateFileSystemEntries(
                paths.CandidateDirectoryPath,
                "*",
                SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray()!;
        string[] expectedEntries =
        [
            CandidateCueFileName,
            CandidateImageFileName,
            CandidateReceiptFileName
        ];
        Array.Sort(expectedEntries, StringComparer.Ordinal);
        if (!entries.SequenceEqual(expectedEntries, StringComparer.Ordinal))
            throw new InvalidDataException(
                "The probe candidate directory does not contain exactly BIN, CUE, and receipt.");
        string cue = await File.ReadAllTextAsync(
            paths.CandidateCuePath,
            Encoding.ASCII,
            cancellationToken);
        if (cue != BuildCanonicalCue(CandidateImageFileName) ||
            Hash(Encoding.ASCII.GetBytes(cue)) != expected.CandidateCueSha256)
        {
            throw new InvalidDataException(
                "The probe candidate CUE failed canonical readback.");
        }
        byte[] receiptBytes = await ReadBoundedFileAsync(
            paths.CandidateReceiptPath,
            MaximumReceiptByteCount,
            cancellationToken);
        Id65AuthoringCollapsedCycloramaProbeRuntimeCandidateReceipt actual;
        try
        {
            actual = JsonSerializer.Deserialize<
                Id65AuthoringCollapsedCycloramaProbeRuntimeCandidateReceipt>(
                    receiptBytes,
                    ReceiptJsonOptions) ?? throw new InvalidDataException(
                        "The probe candidate receipt is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The probe candidate receipt is not valid JSON.",
                exception);
        }
        if (actual != expected ||
            !receiptBytes.SequenceEqual(SerializeReceipt(actual)) ||
            actual.ReceiptContentSha256 != ComputeReceiptContentSha256(actual))
        {
            throw new InvalidDataException(
                "The probe candidate receipt failed canonical readback.");
        }
        string imageSha256 = await HashFileAsync(
            paths.CandidateImagePath,
            cancellationToken);
        if (imageSha256 != expected.OutputImageSha256)
            throw new InvalidDataException(
                "The probe candidate BIN changed after publication.");
        return Hash(receiptBytes);
    }

    private static Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePaths
        CreatePaths(
            string outputRoot,
            string positionId,
            string publicationIdentity)
    {
        if (!IsCanonicalSha256(publicationIdentity))
            throw new InvalidDataException(
                "The probe publication identity is not canonical SHA-256.");
        _ = Id65RemotePadGrassT107Catalog.GetRequiredPosition(positionId);
        string root = Path.GetFullPath(outputRoot);
        string directoryName =
            $"{positionId}-schema7-collapsed-35-part-cyclorama-probe-{publicationIdentity}";
        string directory = Path.Combine(root, directoryName);
        return new(
            root,
            directory,
            Path.Combine(directory, CandidateImageFileName),
            Path.Combine(directory, CandidateCueFileName),
            Path.Combine(directory, CandidateReceiptFileName),
            Path.Combine(root, WriterLeaseFileName));
    }

    private static Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePaths
        PathsForDirectory(
            Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePaths template,
            string directory) => template with
            {
                CandidateDirectoryPath = directory,
                CandidateImagePath = Path.Combine(
                    directory,
                    CandidateImageFileName),
                CandidateCuePath = Path.Combine(
                    directory,
                    CandidateCueFileName),
                CandidateReceiptPath = Path.Combine(
                    directory,
                    CandidateReceiptFileName)
            };

    private static string ComputePublicationIdentity(
        ParentCapture parent,
        Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            ReceiptSchemaVersion.ToString(CultureInfo.InvariantCulture),
            parent.ReceiptFileSha256,
            parent.ImageSha256,
            parent.Receipt.PublicationIdentitySha256,
            parent.Receipt.OutputRow80Sha256,
            compiled.ProfileId,
            compiled.SchemaVersion.ToString(CultureInfo.InvariantCulture),
            compiled.DeterministicPlanSha256,
            compiled.Schema7BaselineRow80Sha256,
            compiled.OutputRow80Sha256,
            compiled.OutputCycloramaSha256
        ])));

    private static async Task<AtomicPublication<T>> MaterializeAtomicallyAsync<T>(
        string candidateRoot,
        string candidateDirectory,
        Func<string, Task<T>> stageAndVerify,
        Func<string, Task<T>> verifyExisting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stageAndVerify);
        ArgumentNullException.ThrowIfNull(verifyExisting);
        cancellationToken.ThrowIfCancellationRequested();
        if (Directory.Exists(candidateDirectory))
        {
            T existing = await verifyExisting(candidateDirectory);
            return new(
                existing,
                ReusedExisting: true,
                PublishedThisCall: false);
        }
        if (File.Exists(candidateDirectory))
            throw new InvalidDataException(
                "The content-addressed probe path is not a directory.");

        string temporaryRoot = Path.Combine(
            candidateRoot,
            TemporaryPrefix + Guid.NewGuid().ToString("N"));
        string stagedDirectory = Path.Combine(temporaryRoot, "candidate");
        RequireOwnedTemporaryPath(temporaryRoot, candidateRoot);
        Directory.CreateDirectory(stagedDirectory);
        bool moved = false;
        try
        {
            _ = await stageAndVerify(stagedDirectory);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Directory.Move(stagedDirectory, candidateDirectory);
                moved = true;
            }
            catch (IOException) when (Directory.Exists(candidateDirectory))
            {
                T raced = await verifyExisting(candidateDirectory);
                return new(
                    raced,
                    ReusedExisting: true,
                    PublishedThisCall: false);
            }
            T final = await verifyExisting(candidateDirectory);
            return new(
                final,
                ReusedExisting: false,
                PublishedThisCall: true);
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
                DeleteOwnedTemporary(temporaryRoot, candidateRoot);
            if (!moved && Directory.Exists(stagedDirectory))
                throw new InvalidDataException(
                    "The probe publisher leaked its owned staging directory.");
        }
    }

    /// <summary>
    /// Focused no-BIN smoke for the production atomic directory primitive.
    /// It proves deterministic reuse, rejects an existing tamper, and removes
    /// an owned temporary directory after an injected staging failure.
    /// </summary>
    internal static void ValidateCompiledProbeForSmoke(
        Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled) =>
        RequireExactCompilerBoundary(compiled);

    internal static async Task<AtomicPublisherSelfTestResult>
        RunAtomicPublisherSelfTestAsync(
            string emptyRoot,
            CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(emptyRoot);
        if (Directory.Exists(root) &&
            Directory.EnumerateFileSystemEntries(root).Any())
        {
            throw new InvalidDataException(
                "The atomic probe publisher self-test requires an empty owned root.");
        }
        Directory.CreateDirectory(root);
        RefuseReparsePoint(root, "atomic publisher self-test root");
        string identity = Hash(Encoding.UTF8.GetBytes(
            "collapsed-35-part-cyclorama-probe-atomic-self-test-v1"));
        string candidate = Path.Combine(
            root,
            "collapsed-35-part-cyclorama-probe-self-test-" + identity);
        byte[] payload = Encoding.UTF8.GetBytes("exact-probe-payload\n");
        byte[] receipt = Encoding.UTF8.GetBytes(
            "{\"transitionalProbeOnly\":true,\"fullyOriginalCyclorama\":false}\n");

        async Task<string> Stage(string directory)
        {
            await WriteNewFileAsync(
                Path.Combine(directory, "probe.bin"),
                payload,
                cancellationToken);
            await WriteNewFileAsync(
                Path.Combine(directory, "probe.receipt.json"),
                receipt,
                cancellationToken);
            return await Verify(directory);
        }
        async Task<string> Verify(string directory)
        {
            string[] names = Directory.EnumerateFiles(directory)
                .Select(Path.GetFileName)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray()!;
            if (!names.SequenceEqual(
                    new[] { "probe.bin", "probe.receipt.json" },
                    StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    "Atomic probe self-test artifact set changed.");
            }
            byte[] actualPayload = await File.ReadAllBytesAsync(
                Path.Combine(directory, "probe.bin"),
                cancellationToken);
            byte[] actualReceipt = await File.ReadAllBytesAsync(
                Path.Combine(directory, "probe.receipt.json"),
                cancellationToken);
            if (!actualPayload.SequenceEqual(payload) ||
                !actualReceipt.SequenceEqual(receipt))
            {
                throw new InvalidDataException(
                    "Atomic probe self-test detected a tampered candidate.");
            }
            return Hash(actualPayload.Concat(actualReceipt).ToArray());
        }

        AtomicPublication<string> first = await MaterializeAtomicallyAsync(
            root,
            candidate,
            Stage,
            Verify,
            cancellationToken);
        AtomicPublication<string> second = await MaterializeAtomicallyAsync(
            root,
            candidate,
            Stage,
            Verify,
            cancellationToken);
        if (first.Value != second.Value ||
            first.ReusedExisting || !first.PublishedThisCall ||
            !second.ReusedExisting || second.PublishedThisCall)
        {
            throw new InvalidDataException(
                "Atomic probe publication was not deterministic on reuse.");
        }

        string receiptPath = Path.Combine(candidate, "probe.receipt.json");
        await File.WriteAllTextAsync(
            receiptPath,
            "tampered\n",
            Encoding.UTF8,
            cancellationToken);
        bool tamperRejected = false;
        try
        {
            _ = await MaterializeAtomicallyAsync(
                root,
                candidate,
                Stage,
                Verify,
                cancellationToken);
        }
        catch (InvalidDataException)
        {
            tamperRejected = true;
        }
        if (!tamperRejected)
            throw new InvalidDataException(
                "Atomic probe publication accepted a tampered existing candidate.");

        string faultIdentity = Hash(Encoding.UTF8.GetBytes(
            "collapsed-35-part-cyclorama-probe-fault-cleanup-v1"));
        string faultCandidate = Path.Combine(
            root,
            "collapsed-35-part-cyclorama-probe-self-test-" + faultIdentity);
        bool faultRejected = false;
        try
        {
            _ = await MaterializeAtomicallyAsync<string>(
                root,
                faultCandidate,
                async staged =>
                {
                    await WriteNewFileAsync(
                        Path.Combine(staged, "partial.bin"),
                        payload,
                        cancellationToken);
                    throw new InjectedProbePublisherFaultException();
                },
                Verify,
                cancellationToken);
        }
        catch (InjectedProbePublisherFaultException)
        {
            faultRejected = true;
        }
        string[] leakedTemporary = Directory.EnumerateDirectories(
                root,
                TemporaryPrefix + "*",
                SearchOption.TopDirectoryOnly)
            .ToArray();
        if (!faultRejected || Directory.Exists(faultCandidate) ||
            leakedTemporary.Length != 0)
        {
            throw new InvalidDataException(
                "Atomic probe fault injection leaked a final or temporary candidate.");
        }
        return new(
            DeterministicReuseVerified: true,
            ExistingTamperRejected: true,
            InjectedFaultCleanupVerified: true,
            PublishedValueSha256: first.Value);
    }

    private static void RequireSameParent(
        ParentCapture expected,
        ParentCapture actual)
    {
        if (!PathsEqual(expected.DirectoryPath, actual.DirectoryPath) ||
            expected.ImageSha256 != actual.ImageSha256 ||
            expected.ReceiptFileSha256 != actual.ReceiptFileSha256 ||
            expected.Receipt != actual.Receipt)
        {
            throw new InvalidDataException(
                "The immutable schema-7 p1 parent changed during probe publication.");
        }
    }

    private static async Task CopyExactParentImageAsync(
        string sourcePath,
        string destinationPath,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        await using FileStream source = new(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1 << 20,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using FileStream destination = new(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            1 << 20,
            FileOptions.Asynchronous | FileOptions.SequentialScan |
                FileOptions.WriteThrough);
        await source.CopyToAsync(destination, 1 << 20, cancellationToken);
        await destination.FlushAsync(cancellationToken);
        destination.Flush(flushToDisk: true);
        if (destination.Length != source.Length)
            throw new InvalidDataException(
                "The staged schema-7 p1 parent copy was truncated.");
        destination.Position = 0;
        string copiedSha256 = Convert.ToHexStringLower(
            await SHA256.HashDataAsync(destination, cancellationToken));
        if (copiedSha256 != expectedSha256)
            throw new InvalidDataException(
                "The staged schema-7 p1 parent copy changed before probe patching.");
    }

    private static void RequireEqualWadComponent(
        FileStream parent,
        FileStream output,
        DiscLayout layout,
        long wadOffset,
        int byteLength,
        string label) => RequireEqualFileComponent(
            parent,
            output,
            layout,
            WadLba,
            wadOffset,
            byteLength,
            label);

    private static void RequireEqualFileComponent(
        FileStream parent,
        FileStream output,
        DiscLayout layout,
        int fileLba,
        long fileOffset,
        int byteLength,
        string label)
    {
        byte[] before = DiscImage.ReadFileBytes(
            parent,
            layout,
            fileLba,
            fileOffset,
            byteLength);
        byte[] after = DiscImage.ReadFileBytes(
            output,
            layout,
            fileLba,
            fileOffset,
            byteLength);
        if (!before.SequenceEqual(after))
            throw new InvalidDataException(
                $"The probe changed protected {label}.");
    }

    private static void VerifySkySectorSubmodes(
        FileStream image,
        DiscLayout layout)
    {
        for (int lba = FirstSkyRawSectorLba;
             lba <= LastSkyRawSectorLba;
             lba++)
        {
            RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(
                image,
                layout,
                lba,
                XaDataSubmode);
        }
    }

    private static DiscFileRecord FindWad(
        FileStream stream,
        DiscLayout layout) => DiscImage.FindRootFileRecord(
            stream,
            layout,
            name => string.Equals(
                name,
                "WAD.WAD",
                StringComparison.OrdinalIgnoreCase));

    private static DiscFileRecord FindExecutable(
        FileStream stream,
        DiscLayout layout) => DiscImage.FindRootFileRecord(
            stream,
            layout,
            name => name.StartsWith(
                "SCUS_942.28",
                StringComparison.OrdinalIgnoreCase));

    private static FileStream AcquireWriterLease(string path)
    {
        RefuseReparsePoint(path, "probe candidate writer lease");
        try
        {
            return new(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                "Another collapsed-cyclorama probe publisher owns the writer lease.",
                exception);
        }
    }

    private static async Task WriteNewFileAsync(
        string path,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    private static byte[] SerializeReceipt(
        Id65AuthoringCollapsedCycloramaProbeRuntimeCandidateReceipt receipt) =>
        Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(receipt, ReceiptJsonOptions) + "\n");

    private static string ComputeReceiptContentSha256(
        Id65AuthoringCollapsedCycloramaProbeRuntimeCandidateReceipt receipt) =>
        Hash(JsonSerializer.SerializeToUtf8Bytes(
            receipt with { ReceiptContentSha256 = "" },
            CompactJsonOptions));

    private static string BuildCanonicalCue(string imageFileName) =>
        $"FILE \"{imageFileName}\" BINARY\n" +
        "  TRACK 01 MODE2/2352\n" +
        "    INDEX 01 00:00:00\n";

    private static async Task<byte[]> ReadBoundedFileAsync(
        string path,
        long maximumByteCount,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length <= 0 || stream.Length > maximumByteCount)
            throw new InvalidDataException(
                "A guarded probe receipt has an invalid byte length.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        return bytes;
    }

    private static async Task<string> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1 << 20,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexStringLower(
            await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static int CountChangedBytes(
        ReadOnlySpan<byte> before,
        ReadOnlySpan<byte> after)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException(
                "Probe diff inputs have different lengths.");
        int changed = 0;
        for (int index = 0; index < before.Length; index++)
            if (before[index] != after[index]) changed++;
        return changed;
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static bool IsCanonicalSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.Ordinal);

    private static bool IsWithin(string root, string path)
    {
        string fullRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(root));
        string fullPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(path));
        return fullPath.StartsWith(
            fullRoot + Path.DirectorySeparatorChar,
            StringComparison.Ordinal);
    }

    private static void RefuseReparsePoint(string path, string context)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return;
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException(
                $"The {context} is a symbolic link or reparse point.");
    }

    private static void RequireOwnedTemporaryPath(
        string temporaryRoot,
        string candidateRoot)
    {
        string full = Path.GetFullPath(temporaryRoot);
        string parent = Path.GetDirectoryName(full) ?? "";
        string name = Path.GetFileName(full);
        if (!PathsEqual(parent, candidateRoot) ||
            !name.StartsWith(TemporaryPrefix, StringComparison.Ordinal) ||
            name.Length != TemporaryPrefix.Length + 32 ||
            !name.AsSpan(TemporaryPrefix.Length).ToArray().All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f'))
        {
            throw new InvalidDataException(
                "The probe temporary operation escaped its exact owned root.");
        }
    }

    private static void DeleteOwnedTemporary(
        string temporaryRoot,
        string candidateRoot)
    {
        RequireOwnedTemporaryPath(temporaryRoot, candidateRoot);
        Directory.Delete(temporaryRoot, recursive: true);
    }

    internal sealed record AtomicPublisherSelfTestResult(
        bool DeterministicReuseVerified,
        bool ExistingTamperRejected,
        bool InjectedFaultCleanupVerified,
        string PublishedValueSha256);

    private sealed record ParentCapture(
        string DirectoryPath,
        string ImagePath,
        string CuePath,
        string ReceiptPath,
        Id65AuthoringSingleSectorSubstrateRuntimeCandidateReceipt Receipt,
        string ImageSha256,
        string ReceiptFileSha256);

    private sealed record CandidateReadback(
        string OutputImageSha256,
        int ChangedRawSectorCount,
        long ChangedPhysicalBytes,
        string PhysicalDiffSha256);

    private sealed record PhysicalDiff(
        int ChangedRawSectorCount,
        long ChangedPhysicalBytes,
        string DiffSha256);

    private sealed record PublishedPayload(
        Id65AuthoringCollapsedCycloramaProbeRuntimeCandidateReceipt Receipt,
        string ReceiptFileSha256);

    private sealed record AtomicPublication<T>(
        T Value,
        bool ReusedExisting,
        bool PublishedThisCall);

    private sealed class InjectedProbePublisherFaultException : Exception;
}

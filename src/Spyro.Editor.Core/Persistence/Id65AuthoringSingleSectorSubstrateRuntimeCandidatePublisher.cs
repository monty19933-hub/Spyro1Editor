using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

internal sealed record Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublishRequest(
    string ParentSchema6CandidateDirectoryPath,
    string OutputRootPath,
    Id65CompiledAuthoringSingleSectorSubstrate Compiled,
    Id65AuthoringSingleSectorAggregatePlan AggregatePlan);

internal sealed record Id65AuthoringSingleSectorSubstrateRuntimeCandidatePaths(
    string CandidateRootPath,
    string CandidateDirectoryPath,
    string CandidateImagePath,
    string CandidateCuePath,
    string CandidateReceiptPath,
    string WriterLeasePath);

internal sealed record Id65AuthoringSingleSectorSubstrateRuntimeCandidateReceipt
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
    public required string ParentAggregatePlanSha256 { get; init; }
    public required string ParentDocumentEnvelopeSha256 { get; init; }
    public required string DocumentCanonicalSha256 { get; init; }
    public required string ActiveTerrainUnitIdsCanonical { get; init; }
    public required string MobyKindId { get; init; }
    public required string MobyPositionId { get; init; }
    public required string CompilerProfileId { get; init; }
    public required int CompilerSchemaVersion { get; init; }
    public required string CompilerDeterministicPlanSha256 { get; init; }
    public required string AggregateProjectorProfileId { get; init; }
    public required int AggregateProjectorSchemaVersion { get; init; }
    public required string AggregatePlanSha256 { get; init; }
    public required string Schema6CompilerProfileId { get; init; }
    public required string Schema6DeterministicPlanSha256 { get; init; }
    public required string Schema6BaselineRow80Sha256 { get; init; }
    public required string OutputRow80Sha256 { get; init; }
    public required string OutputModelSha256 { get; init; }
    public required string OutputTextureComponentSha256 { get; init; }
    public required string OutputCycloramaSha256 { get; init; }
    public required string CollisionSha256 { get; init; }
    public required int EnvironmentRow80Offset { get; init; }
    public required long EnvironmentWadOffset { get; init; }
    public required int EnvironmentByteLength { get; init; }
    public required int LogicalSourceSectorIndex { get; init; }
    public required int PhysicalOutputSectorIndex { get; init; }
    public required int RetainedSectorByteLength { get; init; }
    public required int EnvironmentPointerGapByteLength { get; init; }
    public required string BaselineEnvironmentSha256 { get; init; }
    public required string OutputEnvironmentSha256 { get; init; }
    public required string RetainedSectorSha256 { get; init; }
    public required string EnvironmentZeroGapSha256 { get; init; }
    public required int OcclusionRow80Offset { get; init; }
    public required long OcclusionWadOffset { get; init; }
    public required int OcclusionByteLength { get; init; }
    public required string Schema6OcclusionSha256 { get; init; }
    public required string OutputOcclusionSha256 { get; init; }
    public required string OcclusionOpaqueTailSha256 { get; init; }
    public required int StageChangedLogicalByteCount { get; init; }
    public required string StageOwnedRangeMapSha256 { get; init; }
    public required string StageTransactionSha256 { get; init; }
    public required string StageProtectedComplementSha256 { get; init; }
    public required string ComponentRelocationMapSha256 { get; init; }
    public required string HandleRebaseMapSha256 { get; init; }
    public required string CandidateImageFileName { get; init; }
    public required string CandidateCueFileName { get; init; }
    public required string CandidateReceiptFileName { get; init; }
    public required string OutputImageSha256 { get; init; }
    public required string CandidateCueSha256 { get; init; }
    public required int TargetDataWadEntry { get; init; }
    public required long TargetDataWadOffset { get; init; }
    public required int TargetDataByteLength { get; init; }
    public required int FirstRebuiltRawSectorLba { get; init; }
    public required int RebuiltRawSectorCount { get; init; }
    public required int ChangedRawSectorCount { get; init; }
    public required long ChangedPhysicalImageBytes { get; init; }
    public required string PhysicalDiffSha256 { get; init; }
    public required bool ExactParentReceiptVerified { get; init; }
    public required bool ExactParentImageVerified { get; init; }
    public required bool ExactSchema6StagePreimageVerified { get; init; }
    public required bool ExactSchema6StageInverseVerified { get; init; }
    public required bool ExactOutputRow80ReadbackVerified { get; init; }
    public required bool SingleSectorEnvironmentReadbackVerified { get; init; }
    public required bool SingleSectorOcclusionReadbackVerified { get; init; }
    public required bool CollisionPreservedExactly { get; init; }
    public required bool CycloramaPreservedExactly { get; init; }
    public required bool SpawnAndMobyBytesPreservedExactly { get; init; }
    public required bool Wad79Preserved { get; init; }
    public required bool RetailTownSquareWad15Preserved { get; init; }
    public required bool RetailTownSquareWad16Preserved { get; init; }
    public required bool RetailArtisansWad9Preserved { get; init; }
    public required bool RetailArtisansWad10Preserved { get; init; }
    public required bool RetailTownSquareSkyBlockPreserved { get; init; }
    public required bool RetailArtisansLinkedSkyBlockPreserved { get; init; }
    public required bool ExecutablePreserved { get; init; }
    public required bool RawSectorIntegrityVerified { get; init; }
    public required bool PhysicalComplementVerified { get; init; }
    public required bool ParentSchema6CandidatePreserved { get; init; }
    public required bool ContainsFullDiscImage { get; init; }
    public required bool ContainsCue { get; init; }
    public required bool SeparateGuardedCandidatePublisherAuthorized { get; init; }
    public required bool RuntimeCandidateAuthorized { get; init; }
    public required bool RuntimeProven { get; init; }
    public required bool AppIntegrated { get; init; }
    public required bool UiIntegrated { get; init; }
    public required bool CreateBinEnabled { get; init; }
    public required bool NormalCreateBinEnabled { get; init; }
    public required bool DocumentPersistenceIntegrated { get; init; }
    public required bool ReleaseIntegrated { get; init; }
    public required bool PromotionAuthorized { get; init; }
    public required bool Publishable { get; init; }
    public required string ReceiptContentSha256 { get; init; }
}

internal sealed record Id65AuthoringSingleSectorSubstrateRuntimeCandidateResult(
    Id65AuthoringSingleSectorSubstrateRuntimeCandidatePaths Paths,
    Id65AuthoringSingleSectorSubstrateRuntimeCandidateReceipt Receipt,
    string ReceiptFileSha256,
    bool ReusedExistingCandidate,
    bool CandidateArtifactsWrittenThisCall,
    bool FileSystemMutationPerformed,
    bool RuntimeProven,
    bool NormalCreateBinEnabled,
    bool ReleaseIntegrated,
    bool Publishable);

/// <summary>
/// Publishes one disposable schema-7 runtime candidate by applying only the
/// pure single-sector substrate stage to an exact, immutable schema-6 p2
/// candidate. It deliberately does not redefine schema-6 artifacts and does
/// not grant normal Create BIN, UI, document-persistence, release, or
/// promotion authority.
/// </summary>
internal static class Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
{
    public const string Format =
        "spyro-editor-id65-authoring-single-sector-substrate-schema7-runtime-candidate-receipt";
    public const int ReceiptSchemaVersion = 1;
    public const string ProfileId =
        "id65-authoring-single-sector-substrate-schema7-runtime-candidate-publisher-v1";
    public const string CandidateImageFileName =
        "unusedlevel65blank-authoring-single-sector-substrate-schema7-candidate-v1.bin";
    public const string CandidateCueFileName =
        "unusedlevel65blank-authoring-single-sector-substrate-schema7-candidate-v1.cue";
    public const string CandidateReceiptFileName =
        "unusedlevel65blank-authoring-single-sector-substrate-schema7-candidate-v1.receipt.json";
    public const int WadLba = 37;
    public const int ExpectedWadByteLength = 0x6C18800;
    public const int ExecutableLba = 55_382;
    public const int ExecutableByteLength = 0x66000;
    public const long ExpectedImageByteLength = 661_547_040;
    public const int TargetDataWadEntry = 80;
    public const long TargetDataWadOffset = 0x6936800;
    public const int TargetDataByteLength = 0x2E2000;
    public const int FirstRebuiltRawSectorLba = 53_906;
    public const int RebuiltRawSectorCount = 1_476;
    public const int LastRebuiltRawSectorLba = 55_381;

    private const int RawSectorByteLength = 2352;
    private const int LogicalSectorByteLength = 2048;
    private const int UserDataOffset = 24;
    private const byte XaDataSubmode = 0x08;
    private const byte XaFinalDataSubmode = 0x89;
    private const long MaximumReceiptByteCount = 256 * 1024;
    private const long RetailTownSquareSkyBlockWadOffset = 0x13080A8;
    private const long RetailArtisansLinkedSkyBlockWadOffset = 0x96A7EC;
    private const int RetailSkyBlockByteLength = 0x84E4;
    private const string WriterLeaseFileName =
        ".single-sector-substrate-schema7-candidate.writer.lock";
    private const string TemporaryPrefix = ".materialize-single-sector-schema7-";

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

    public static async Task<Id65AuthoringSingleSectorSubstrateRuntimeCandidateResult>
        PublishAsync(
            Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublishRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.ParentSchema6CandidateDirectoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputRootPath);
        ArgumentNullException.ThrowIfNull(request.Compiled);
        ArgumentNullException.ThrowIfNull(request.AggregatePlan);
        cancellationToken.ThrowIfCancellationRequested();

        RequireExactCompilerBoundary(request.Compiled);
        RequireExactAggregateBoundary(request.AggregatePlan, request.Compiled);
        ParentCapture parent = await CaptureParentAsync(
            request.ParentSchema6CandidateDirectoryPath,
            request.Compiled,
            cancellationToken);
        if (parent.Receipt.AggregatePlanSha256 !=
            request.AggregatePlan.Schema6AggregatePlanSha256)
        {
            throw new InvalidDataException(
                "The parent receipt does not match the independently recomputed schema-6 aggregate plan.");
        }
        string publicationIdentity = ComputePublicationIdentity(
            parent,
            request.Compiled,
            request.AggregatePlan);
        Id65AuthoringSingleSectorSubstrateRuntimeCandidatePaths paths =
            CreatePaths(
                request.OutputRootPath,
                parent.Receipt.PositionId,
                publicationIdentity);
        if (PathsEqual(
                request.ParentSchema6CandidateDirectoryPath,
                paths.CandidateDirectoryPath) ||
            IsWithin(
                request.ParentSchema6CandidateDirectoryPath,
                paths.CandidateDirectoryPath))
        {
            throw new InvalidDataException(
                "The schema-7 candidate destination overlaps its immutable schema-6 parent.");
        }

        bool rootCreated = !Directory.Exists(paths.CandidateRootPath);
        Directory.CreateDirectory(paths.CandidateRootPath);
        RefuseReparsePoint(paths.CandidateRootPath, "schema-7 candidate root");
        using FileStream writerLease = AcquireWriterLease(paths.WriterLeasePath);

        ParentCapture leasedParent = await CaptureParentAsync(
            request.ParentSchema6CandidateDirectoryPath,
            request.Compiled,
            cancellationToken);
        RequireSameParent(parent, leasedParent);

        if (Directory.Exists(paths.CandidateDirectoryPath))
        {
            CandidateReadback existing = await VerifyCandidateAsync(
                leasedParent,
                paths.CandidateImagePath,
                request.Compiled,
                cancellationToken);
            Id65AuthoringSingleSectorSubstrateRuntimeCandidateReceipt expected =
                BuildReceipt(
                    leasedParent,
                    request.Compiled,
                    request.AggregatePlan,
                    publicationIdentity,
                    existing);
            string receiptFileSha256 = await RequireExactPublishedCandidateAsync(
                paths,
                expected,
                cancellationToken);
            return new(
                paths,
                expected,
                receiptFileSha256,
                ReusedExistingCandidate: true,
                CandidateArtifactsWrittenThisCall: false,
                FileSystemMutationPerformed: rootCreated,
                RuntimeProven: false,
                NormalCreateBinEnabled: false,
                ReleaseIntegrated: false,
                Publishable: false);
        }
        if (File.Exists(paths.CandidateDirectoryPath))
            throw new InvalidDataException(
                "The content-addressed schema-7 candidate path is not a directory.");

        string temporaryRoot = Path.Combine(
            paths.CandidateRootPath,
            TemporaryPrefix + Guid.NewGuid().ToString("N"));
        string stagedDirectory = Path.Combine(temporaryRoot, "candidate");
        RequireOwnedTemporaryPath(temporaryRoot, paths.CandidateRootPath);
        Directory.CreateDirectory(stagedDirectory);
        bool published = false;
        try
        {
            string stagedImage = Path.Combine(
                stagedDirectory,
                CandidateImageFileName);
            string stagedCue = Path.Combine(stagedDirectory, CandidateCueFileName);
            string stagedReceipt = Path.Combine(
                stagedDirectory,
                CandidateReceiptFileName);
            await CopyExactParentImageAsync(
                leasedParent.ImagePath,
                stagedImage,
                leasedParent.ImageSha256,
                cancellationToken);
            ApplyAndRebuildExactSchema6Stage(
                stagedImage,
                request.Compiled);
            CandidateReadback readback = await VerifyCandidateAsync(
                leasedParent,
                stagedImage,
                request.Compiled,
                cancellationToken);
            Id65AuthoringSingleSectorSubstrateRuntimeCandidateReceipt receipt =
                BuildReceipt(
                    leasedParent,
                    request.Compiled,
                    request.AggregatePlan,
                    publicationIdentity,
                    readback);
            await WriteNewFileAsync(
                stagedCue,
                Encoding.ASCII.GetBytes(BuildCanonicalCue()),
                cancellationToken);
            await WriteNewFileAsync(
                stagedReceipt,
                SerializeReceipt(receipt),
                cancellationToken);

            Id65AuthoringSingleSectorSubstrateRuntimeCandidatePaths stagedPaths =
                paths with
                {
                    CandidateDirectoryPath = stagedDirectory,
                    CandidateImagePath = stagedImage,
                    CandidateCuePath = stagedCue,
                    CandidateReceiptPath = stagedReceipt
                };
            _ = await RequireExactPublishedCandidateAsync(
                stagedPaths,
                receipt,
                cancellationToken);
            ParentCapture prepublishParent = await CaptureParentAsync(
                request.ParentSchema6CandidateDirectoryPath,
                request.Compiled,
                cancellationToken);
            RequireSameParent(leasedParent, prepublishParent);

            try
            {
                Directory.Move(stagedDirectory, paths.CandidateDirectoryPath);
                published = true;
            }
            catch (IOException) when (Directory.Exists(paths.CandidateDirectoryPath))
            {
                CandidateReadback raced = await VerifyCandidateAsync(
                    prepublishParent,
                    paths.CandidateImagePath,
                    request.Compiled,
                    cancellationToken);
                Id65AuthoringSingleSectorSubstrateRuntimeCandidateReceipt racedReceipt =
                    BuildReceipt(
                        prepublishParent,
                        request.Compiled,
                        request.AggregatePlan,
                        publicationIdentity,
                        raced);
                _ = await RequireExactPublishedCandidateAsync(
                    paths,
                    racedReceipt,
                    cancellationToken);
                return new(
                    paths,
                    racedReceipt,
                    await HashFileAsync(paths.CandidateReceiptPath, cancellationToken),
                    ReusedExistingCandidate: true,
                    CandidateArtifactsWrittenThisCall: false,
                    FileSystemMutationPerformed: true,
                    RuntimeProven: false,
                    NormalCreateBinEnabled: false,
                    ReleaseIntegrated: false,
                    Publishable: false);
            }

            CandidateReadback finalReadback = await VerifyCandidateAsync(
                prepublishParent,
                paths.CandidateImagePath,
                request.Compiled,
                cancellationToken);
            Id65AuthoringSingleSectorSubstrateRuntimeCandidateReceipt finalReceipt =
                BuildReceipt(
                    prepublishParent,
                    request.Compiled,
                    request.AggregatePlan,
                    publicationIdentity,
                    finalReadback);
            if (finalReceipt != receipt)
                throw new InvalidDataException(
                    "The schema-7 candidate identity changed across atomic publication.");
            string finalReceiptSha256 = await RequireExactPublishedCandidateAsync(
                paths,
                finalReceipt,
                cancellationToken);
            return new(
                paths,
                finalReceipt,
                finalReceiptSha256,
                ReusedExistingCandidate: false,
                CandidateArtifactsWrittenThisCall: true,
                FileSystemMutationPerformed: true,
                RuntimeProven: false,
                NormalCreateBinEnabled: false,
                ReleaseIntegrated: false,
                Publishable: false);
        }
        finally
        {
            if (!published && Directory.Exists(stagedDirectory))
                DeleteOwnedTemporary(temporaryRoot, paths.CandidateRootPath);
            else if (Directory.Exists(temporaryRoot))
                DeleteOwnedTemporary(temporaryRoot, paths.CandidateRootPath);
        }
    }

    private static void RequireExactCompilerBoundary(
        Id65CompiledAuthoringSingleSectorSubstrate compiled)
    {
        if (compiled.ProfileId !=
                Id65AuthoringSingleSectorSubstrateCompiler.ProfileId ||
            compiled.SchemaVersion !=
                Id65AuthoringSingleSectorSubstrateCompiler.SchemaVersion ||
            !compiled.PureInMemory ||
            !compiled.ExactSchema6DocumentRequired ||
            !compiled.BuildsExactSchema6P2BaselineFromLockedInputs ||
            compiled.CallerAfterimagesAccepted ||
            !compiled.ProducesSchema7SingleSectorSubstrate ||
            !compiled.EnvironmentSectorCountIsOne ||
            !compiled.EnvironmentPointerGapZeroed ||
            !compiled.AuthoredOnlyOcclusionRebasedToSector0 ||
            !compiled.InheritedEnvironmentBytesRetired ||
            compiled.InheritedCollisionLookupsRetired ||
            !compiled.CollisionPreservedExactly ||
            !compiled.ExactSchema6StageInverseVerified ||
            !compiled.EveryChangedByteOwned ||
            !compiled.ProtectedComplementVerified ||
            compiled.WritesFileSystem || compiled.WritesDiscImage ||
            compiled.WritesCue || compiled.AppIntegrated ||
            compiled.UiIntegrated || compiled.DocumentPersistenceIntegrated ||
            compiled.CreateBinEnabled || compiled.NormalCreateBinEnabled ||
            compiled.RuntimeCandidateAuthorized || compiled.RuntimeProven ||
            compiled.ReleaseIntegrated || compiled.PromotionAuthorized ||
            compiled.Publishable)
        {
            throw new InvalidDataException(
                "The runtime-candidate publisher requires the exact guarded single-sector compiler boundary.");
        }
    }

    private static void RequireExactAggregateBoundary(
        Id65AuthoringSingleSectorAggregatePlan plan,
        Id65CompiledAuthoringSingleSectorSubstrate compiled)
    {
        byte[] schema6FromPlan = plan.CopySchema6BaselineRow80();
        byte[] outputFromPlan = Id65AuthoringSingleSectorAggregatePlanProjector
            .ApplyStageExact(plan, schema6FromPlan, reverse: false);
        byte[] restoredByPlan = Id65AuthoringSingleSectorAggregatePlanProjector
            .ApplyStageExact(plan, outputFromPlan, reverse: true);
        if (plan.ProfileId !=
                Id65AuthoringSingleSectorAggregatePlanProjector.ProfileId ||
            plan.SchemaVersion !=
                Id65AuthoringSingleSectorAggregatePlanProjector.SchemaVersion ||
            plan.DocumentCanonicalSha256 != compiled.DocumentCanonicalSha256 ||
            plan.Schema6CompilerPlanSha256 !=
                compiled.Schema6DeterministicPlanSha256 ||
            plan.Schema6BaselineRow80Sha256 !=
                compiled.Schema6BaselineRow80Sha256 ||
            plan.OutputRow80Sha256 != compiled.OutputRow80Sha256 ||
            plan.CompilerDeterministicPlanSha256 !=
                compiled.DeterministicPlanSha256 ||
            plan.CompilerStageOwnedRangeMapSha256 !=
                compiled.StageOwnedRangeMapSha256 ||
            plan.CompilerStageTransactionSha256 !=
                compiled.StageTransactionSha256 ||
            plan.CompilerStageProtectedComplementSha256 !=
                compiled.StageProtectedComplementSha256 ||
            !schema6FromPlan.SequenceEqual(
                compiled.CopySchema6BaselineRow80()) ||
            !outputFromPlan.SequenceEqual(compiled.CopyOutputRow80()) ||
            !restoredByPlan.SequenceEqual(schema6FromPlan) ||
            !plan.PureInMemory || !plan.FullExactSchema6DocumentRequired ||
            !plan.Schema6P2AggregateBaselineRecomputed ||
            !plan.ProducesSchema7SingleSectorSubstrate ||
            !plan.ExactSchema6StageDeltaCarried ||
            !plan.ExactSchema6StageInverseVerified ||
            !plan.CompleteRowOwnershipPartitionVerified ||
            !plan.Wad80SoleWritableDestination ||
            !plan.CollisionPreservedExactly ||
            plan.InheritedCollisionLookupsRetired ||
            plan.WritesFileSystem || plan.WritesDiscImage || plan.WritesBin ||
            plan.WritesCue || plan.MaterializerIntegrated ||
            plan.CreateBinEnabled || plan.NormalCreateBinEnabled ||
            plan.RuntimeCandidateAuthorized || plan.RuntimeProven ||
            plan.ReleaseIntegrated || plan.PromotionAuthorized ||
            plan.Publishable)
        {
            throw new InvalidDataException(
                "The publisher requires the exact independently recomputed schema-7 aggregate plan.");
        }
    }

    private static async Task<ParentCapture> CaptureParentAsync(
        string parentDirectory,
        Id65CompiledAuthoringSingleSectorSubstrate compiled,
        CancellationToken cancellationToken)
    {
        string directory = Path.GetFullPath(parentDirectory);
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException(
                "The exact schema-6 p2 parent candidate directory does not exist.");
        RefuseReparsePoint(directory, "schema-6 parent candidate directory");
        string receiptPath = Path.Combine(
            directory,
            Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                .CandidateReceiptFileName);
        RefuseReparsePoint(receiptPath, "schema-6 parent receipt");
        byte[] receiptBytes = await ReadBoundedFileAsync(
            receiptPath,
            MaximumReceiptByteCount,
            cancellationToken);
        Id65AuthoringBlankCycloramaAggregateCandidateReceipt receipt;
        try
        {
            receipt = JsonSerializer.Deserialize<
                Id65AuthoringBlankCycloramaAggregateCandidateReceipt>(
                    receiptBytes,
                    ReceiptJsonOptions) ?? throw new InvalidDataException(
                        "The schema-6 parent receipt is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The schema-6 parent receipt is not valid JSON.",
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
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializer.Format ||
            receipt.SchemaVersion !=
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                    .ReceiptSchemaVersion ||
            receipt.ProfileId !=
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializer.ProfileId ||
            receipt.CompilerProfileId !=
                Id65AuthoringBlankCycloramaCompiler.ProfileId ||
            receipt.CompilerSchemaVersion !=
                Id65AuthoringBlankCycloramaCompiler.SchemaVersion ||
            receipt.OutputRow80Sha256 != compiled.Schema6BaselineRow80Sha256 ||
            receipt.DocumentCanonicalSha256 != compiled.DocumentCanonicalSha256 ||
            receipt.ActiveTerrainUnitIdsCanonical !=
                string.Join('\n', compiled.ActiveTerrainUnitIds) ||
            receipt.MobyKindId !=
                Id65RemotePadMobyChoiceCatalog.GetKindId(compiled.MobyKind) ||
            receipt.PositionId != compiled.MobyPositionId ||
            !receipt.Schema6CompilerPinsVerified ||
            !receipt.ExactRow80ReadbackVerified ||
            !receipt.RawSectorIntegrityVerified ||
            !receipt.PhysicalComplementVerified ||
            !receipt.SourceLockedBasePreserved ||
            !receipt.ContainsFullDiscImage || !receipt.ContainsCue ||
            !receipt.WorkspaceCandidateWriterAuthorized ||
            receipt.RuntimeCandidateAuthorized || receipt.RuntimeProven ||
            receipt.AppIntegrated || receipt.UiIntegrated ||
            receipt.CreateBinEnabled || receipt.NormalCreateBinEnabled ||
            receipt.ReleaseIntegrated || receipt.ReleaseAuthorized ||
            receipt.PromotionAuthorized || receipt.Publishable)
        {
            throw new InvalidDataException(
                "The schema-6 parent receipt is not the exact guarded p2 compiler output required by this stage.");
        }
        string imagePath = Path.Combine(directory, receipt.CandidateImageFileName);
        string cuePath = Path.Combine(directory, receipt.CandidateCueFileName);
        RefuseReparsePoint(imagePath, "schema-6 parent image");
        RefuseReparsePoint(cuePath, "schema-6 parent cue");
        if (!File.Exists(imagePath) || !File.Exists(cuePath))
            throw new FileNotFoundException(
                "The schema-6 parent candidate is incomplete.");
        string cueText = await File.ReadAllTextAsync(
            cuePath,
            Encoding.ASCII,
            cancellationToken);
        if (cueText != BuildCanonicalParentCue(receipt.CandidateImageFileName) ||
            Hash(Encoding.ASCII.GetBytes(cueText)) != receipt.CandidateCueSha256)
        {
            throw new InvalidDataException(
                "The schema-6 parent CUE failed exact readback.");
        }
        string imageSha256 = await HashFileAsync(imagePath, cancellationToken);
        if (imageSha256 != receipt.OutputImageSha256)
            throw new InvalidDataException(
                "The schema-6 parent BIN does not match its immutable receipt.");
        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        if (layout.SectorSize != RawSectorByteLength ||
            layout.UserOffset != UserDataOffset)
        {
            throw new InvalidDataException(
                "The schema-6 parent BIN is not the expected MODE2/2352 image.");
        }
        using (FileStream image = File.OpenRead(imagePath))
        {
            DiscFileRecord wad = FindWad(image, layout);
            DiscFileRecord executable = FindExecutable(image, layout);
            if (image.Length != ExpectedImageByteLength ||
                wad.Lba != WadLba || wad.Size != ExpectedWadByteLength ||
                executable.Lba != ExecutableLba ||
                executable.Size != ExecutableByteLength)
            {
                throw new InvalidDataException(
                    "The schema-6 parent disc extents changed.");
            }
            byte[] row = DiscImage.ReadFileBytes(
                image,
                layout,
                WadLba,
                TargetDataWadOffset,
                TargetDataByteLength);
            if (Hash(row) != compiled.Schema6BaselineRow80Sha256 ||
                !row.SequenceEqual(compiled.CopySchema6BaselineRow80()))
            {
                throw new InvalidDataException(
                    "The parent BIN WAD80 is not the independently reconstructed schema-6 p2 preimage.");
            }
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

    private static void ApplyAndRebuildExactSchema6Stage(
        string stagedImagePath,
        Id65CompiledAuthoringSingleSectorSubstrate compiled)
    {
        DiscLayout layout = DiscImage.DetectLayout(stagedImagePath);
        if (layout.SectorSize != RawSectorByteLength ||
            layout.UserOffset != UserDataOffset)
        {
            throw new InvalidDataException(
                "The staged schema-7 candidate changed the raw disc layout.");
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
                "The staged schema-7 candidate changed a guarded disc extent.");
        }
        byte[] parentRow = DiscImage.ReadFileBytes(
            image,
            layout,
            WadLba,
            TargetDataWadOffset,
            TargetDataByteLength);
        byte[] outputRow =
            Id65AuthoringSingleSectorSubstrateCompiler
                .ApplySchema6StageTransactional(
                    compiled,
                    parentRow,
                    reverse: false);
        if (!outputRow.SequenceEqual(compiled.CopyOutputRow80()))
            throw new InvalidDataException(
                "The schema-7 stage afterimage changed before disc write.");
        DiscImage.WriteFileBytes(
            image,
            layout,
            WadLba,
            TargetDataWadOffset,
            outputRow);
        int rebuilt = RawMode2Form1SectorIntegrity.RebuildFileRanges(
            image,
            layout,
            WadLba,
            [(TargetDataWadOffset, TargetDataByteLength)]);
        int verified = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            image,
            layout,
            [(FirstRebuiltRawSectorLba, RebuiltRawSectorCount)]);
        if (rebuilt != RebuiltRawSectorCount ||
            verified != RebuiltRawSectorCount)
        {
            throw new InvalidDataException(
                "The schema-7 raw-sector rebuild envelope changed.");
        }
        VerifyWad80Submodes(image, layout);
        image.Flush(flushToDisk: true);
    }

    private static async Task<CandidateReadback> VerifyCandidateAsync(
        ParentCapture parent,
        string candidateImagePath,
        Id65CompiledAuthoringSingleSectorSubstrate compiled,
        CancellationToken cancellationToken)
    {
        RefuseReparsePoint(candidateImagePath, "schema-7 candidate image");
        if (!File.Exists(candidateImagePath))
            throw new FileNotFoundException(
                "The schema-7 candidate image is missing.");
        DiscLayout parentLayout = DiscImage.DetectLayout(parent.ImagePath);
        DiscLayout outputLayout = DiscImage.DetectLayout(candidateImagePath);
        if (parentLayout != outputLayout ||
            outputLayout.SectorSize != RawSectorByteLength ||
            outputLayout.UserOffset != UserDataOffset)
        {
            throw new InvalidDataException(
                "The schema-7 candidate changed the raw disc layout.");
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
                "The schema-7 candidate changed the full-disc image length.");
        }
        DiscFileRecord parentWad = FindWad(parentImage, parentLayout);
        DiscFileRecord outputWad = FindWad(outputImage, outputLayout);
        DiscFileRecord parentExecutable = FindExecutable(parentImage, parentLayout);
        DiscFileRecord outputExecutable = FindExecutable(outputImage, outputLayout);
        if (parentWad != outputWad || parentExecutable != outputExecutable ||
            outputWad.Lba != WadLba || outputWad.Size != ExpectedWadByteLength ||
            outputExecutable.Lba != ExecutableLba ||
            outputExecutable.Size != ExecutableByteLength)
        {
            throw new InvalidDataException(
                "The schema-7 candidate moved or resized a guarded disc file.");
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
        if (!parentRow.SequenceEqual(compiled.CopySchema6BaselineRow80()) ||
            !outputRow.SequenceEqual(compiled.CopyOutputRow80()) ||
            Hash(parentRow) != compiled.Schema6BaselineRow80Sha256 ||
            Hash(outputRow) != compiled.OutputRow80Sha256)
        {
            throw new InvalidDataException(
                "The schema-7 candidate WAD80 readback changed.");
        }
        byte[] restored = Id65AuthoringSingleSectorSubstrateCompiler
            .ApplySchema6StageTransactional(
                compiled,
                outputRow,
                reverse: true);
        if (!restored.SequenceEqual(parentRow))
            throw new InvalidDataException(
                "The schema-7 candidate failed exact p2-stage inverse readback.");
        bool[] stageOwned = BuildAndVerifyStageOwnership(
            compiled,
            parentRow,
            outputRow);
        VerifySingleSectorComponents(compiled, parentRow, outputRow);
        RequirePreservedModelComponent(
            compiled,
            "component.collision",
            parentRow,
            outputRow,
            compiled.CollisionSha256);
        RequirePreservedModelComponent(
            compiled,
            "component.cyclorama",
            parentRow,
            outputRow,
            compiled.OutputCycloramaSha256);
        foreach (string stableId in new[]
                 {
                     "component.special-surface", "component.portal-table",
                     "component.particles", "component.sound"
                 })
        {
            RequirePreservedModelComponent(
                compiled,
                stableId,
                parentRow,
                outputRow,
                expectedSha256: null);
        }

        RequireEqualWadComponent(
            parentImage,
            outputImage,
            parentLayout,
            0x6927000,
            0xF800,
            "ID65 WAD79 overlay");
        RequireEqualWadComponent(
            parentImage,
            outputImage,
            parentLayout,
            0x118E800,
            0xF800,
            "retail Town Square WAD15 overlay");
        RequireEqualWadComponent(
            parentImage,
            outputImage,
            parentLayout,
            0x119E000,
            TargetDataByteLength,
            "retail Town Square WAD16 data");
        RequireEqualWadComponent(
            parentImage,
            outputImage,
            parentLayout,
            0x7F2800,
            0xE000,
            "retail Artisans WAD9 overlay");
        RequireEqualWadComponent(
            parentImage,
            outputImage,
            parentLayout,
            0x800800,
            0x383000,
            "retail Artisans WAD10 data");
        RequireEqualWadComponent(
            parentImage,
            outputImage,
            parentLayout,
            RetailTownSquareSkyBlockWadOffset,
            RetailSkyBlockByteLength,
            "retail Town Square sky block");
        RequireEqualWadComponent(
            parentImage,
            outputImage,
            parentLayout,
            RetailArtisansLinkedSkyBlockWadOffset,
            RetailSkyBlockByteLength,
            "retail Artisans linked sky block");
        RequireEqualFileComponent(
            parentImage,
            outputImage,
            parentLayout,
            ExecutableLba,
            0,
            ExecutableByteLength,
            "SCUS_942.28 executable");

        int verified = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            outputImage,
            outputLayout,
            [(FirstRebuiltRawSectorLba, RebuiltRawSectorCount)]);
        if (verified != RebuiltRawSectorCount)
            throw new InvalidDataException(
                "The final schema-7 raw-sector integrity count changed.");
        VerifyWad80Submodes(outputImage, outputLayout);
        PhysicalDiff physical = await ComparePhysicalImagesAsync(
            parentImage,
            outputImage,
            stageOwned,
            compiled.StageChangedByteCount,
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

    private static bool[] BuildAndVerifyStageOwnership(
        Id65CompiledAuthoringSingleSectorSubstrate compiled,
        ReadOnlySpan<byte> parentRow,
        ReadOnlySpan<byte> outputRow)
    {
        if (compiled.StageOwnedRanges.Count == 0 ||
            compiled.StageOwnedRanges.Count != compiled.StageInverseRanges.Count)
        {
            throw new InvalidDataException(
                "The schema-7 stage ownership ledger is empty or asymmetric.");
        }
        bool[] owned = new bool[TargetDataByteLength];
        int ownedBytes = 0;
        foreach (Id65AuthoringCompositeOwnedRange range in
                 compiled.StageOwnedRanges)
        {
            int end = checked(range.DataRelativeOffset + range.ByteLength);
            if (range.DataRelativeOffset < 0 || end > owned.Length ||
                range.ByteLength <= 0 ||
                range.CopyBefore().Length != range.ByteLength ||
                range.CopyAfter().Length != range.ByteLength ||
                !parentRow.Slice(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(range.CopyBefore()) ||
                !outputRow.Slice(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(range.CopyAfter()))
            {
                throw new InvalidDataException(
                    "A schema-7 stage-owned range failed exact before/after readback.");
            }
            for (int offset = range.DataRelativeOffset; offset < end; offset++)
            {
                if (owned[offset])
                    throw new InvalidDataException(
                        "The schema-7 stage ownership ledger overlaps.");
                if (parentRow[offset] == outputRow[offset])
                    throw new InvalidDataException(
                        "A schema-7 stage-owned byte did not change.");
                owned[offset] = true;
                ownedBytes++;
            }
        }
        int changed = 0;
        for (int offset = 0; offset < parentRow.Length; offset++)
        {
            if (parentRow[offset] == outputRow[offset])
                continue;
            changed++;
            if (!owned[offset])
                throw new InvalidDataException(
                    "The schema-7 stage changed an unowned WAD80 byte.");
        }
        if (changed != ownedBytes || changed != compiled.StageChangedByteCount)
            throw new InvalidDataException(
                "The schema-7 stage changed-byte cardinality drifted.");
        return owned;
    }

    private static void VerifySingleSectorComponents(
        Id65CompiledAuthoringSingleSectorSubstrate compiled,
        ReadOnlySpan<byte> parentRow,
        ReadOnlySpan<byte> outputRow)
    {
        Id65AuthoringSingleSectorEnvironmentReadback environment =
            compiled.Environment;
        Id65AuthoringSingleSectorOcclusionReadback occlusion =
            compiled.Occlusion;
        ReadOnlySpan<byte> parentEnvironment = parentRow.Slice(
            environment.Row80Offset,
            environment.ComponentByteLength);
        ReadOnlySpan<byte> outputEnvironment = outputRow.Slice(
            environment.Row80Offset,
            environment.ComponentByteLength);
        ReadOnlySpan<byte> parentOcclusion = parentRow.Slice(
            occlusion.Row80Offset,
            occlusion.ComponentByteLength);
        ReadOnlySpan<byte> outputOcclusion = outputRow.Slice(
            occlusion.Row80Offset,
            occlusion.ComponentByteLength);
        if (Hash(parentEnvironment) != environment.BaselineEnvironmentSha256 ||
            Hash(outputEnvironment) != environment.OutputEnvironmentSha256 ||
            Hash(outputEnvironment.Slice(
                environment.OutputSectorRelativeOffset,
                environment.SectorByteLength)) != environment.RetainedSectorSha256 ||
            Hash(outputEnvironment.Slice(
                environment.OutputSectorRelativeOffset +
                    environment.SectorByteLength,
                environment.PointerGapByteLength)) != environment.ZeroGapSha256 ||
            BinaryPrimitives.ReadInt32LittleEndian(outputEnvironment) !=
                environment.ComponentByteLength ||
            BinaryPrimitives.ReadInt32LittleEndian(outputEnvironment[4..]) != 1 ||
            BinaryPrimitives.ReadInt32LittleEndian(outputEnvironment[8..]) !=
                Id65AuthoringSingleSectorSubstrateCompiler.OutputSectorPointerValue ||
            Hash(parentOcclusion) != occlusion.Schema6OcclusionSha256 ||
            Hash(outputOcclusion) != occlusion.OutputOcclusionSha256)
        {
            throw new InvalidDataException(
                "The schema-7 environment or occlusion physical readback changed.");
        }
        Id65AuthoringSingleSectorSubstrateCompiler.RequireSingleSectorOcclusion(
            outputOcclusion);
    }

    private static void RequirePreservedModelComponent(
        Id65CompiledAuthoringSingleSectorSubstrate compiled,
        string stableId,
        ReadOnlySpan<byte> parentRow,
        ReadOnlySpan<byte> outputRow,
        string? expectedSha256)
    {
        Id65ModelComponentRelocation component = compiled.ComponentRelocations
            .Single(item => item.StableId == stableId);
        int parentOffset = checked(
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset +
            component.SourceRelativeOffset);
        int outputOffset = checked(
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset +
            component.OutputRelativeOffset);
        ReadOnlySpan<byte> before = parentRow.Slice(
            parentOffset,
            component.SourceByteLength);
        ReadOnlySpan<byte> after = outputRow.Slice(
            outputOffset,
            component.OutputByteLength);
        if (!component.ContentsPreserved ||
            component.SourceRelativeOffset != component.OutputRelativeOffset ||
            component.SourceByteLength != component.OutputByteLength ||
            component.SourceSha256 != component.OutputSha256 ||
            Hash(before) != component.SourceSha256 ||
            Hash(after) != component.OutputSha256 ||
            !before.SequenceEqual(after) ||
            expectedSha256 is not null && Hash(after) != expectedSha256)
        {
            throw new InvalidDataException(
                $"The schema-7 stage changed protected model component {stableId}.");
        }
    }

    private static async Task<PhysicalDiff> ComparePhysicalImagesAsync(
        FileStream parent,
        FileStream output,
        IReadOnlyList<bool> stageOwned,
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
            if (before.AsSpan().SequenceEqual(after))
                continue;
            changedSectors++;
            if (lba < FirstRebuiltRawSectorLba ||
                lba > LastRebuiltRawSectorLba)
            {
                throw new InvalidDataException(
                    $"The schema-7 candidate changed raw sector {lba} outside WAD80.");
            }
            bool sectorHasOwnedLogicalChange = false;
            for (int index = UserDataOffset;
                 index < UserDataOffset + LogicalSectorByteLength;
                 index++)
            {
                if (before[index] == after[index])
                    continue;
                long wadOffset = ((long)lba - WadLba) *
                    LogicalSectorByteLength + (index - UserDataOffset);
                long rowRelative = wadOffset - TargetDataWadOffset;
                if (rowRelative < 0 || rowRelative >= stageOwned.Count ||
                    !stageOwned[checked((int)rowRelative)])
                {
                    throw new InvalidDataException(
                        $"The schema-7 candidate changed an unowned logical WAD byte at 0x{wadOffset:X}.");
                }
                sectorHasOwnedLogicalChange = true;
                changedLogicalBytes++;
            }
            if (!sectorHasOwnedLogicalChange)
                throw new InvalidDataException(
                    "A schema-7 raw sector changed without an owned logical payload change.");
            for (int index = 0; index < RawSectorByteLength; index++)
            {
                if (before[index] == after[index])
                    continue;
                if (index < UserDataOffset)
                    throw new InvalidDataException(
                        $"The schema-7 candidate changed raw header/subheader byte {lba}:{index}.");
                changedPhysicalBytes++;
                BinaryPrimitives.WriteInt32LittleEndian(identity.AsSpan(0, 4), lba);
                BinaryPrimitives.WriteInt32LittleEndian(identity.AsSpan(4, 4), index);
                identity[8] = before[index];
                identity[9] = after[index];
                diffHash.AppendData(identity);
            }
        }
        if (parent.Position != parent.Length || output.Position != output.Length ||
            changedSectors <= 0 || changedPhysicalBytes <= 0 ||
            changedLogicalBytes != expectedChangedLogicalBytes)
        {
            throw new InvalidDataException(
                "The schema-7 physical diff is empty or incomplete.");
        }
        return new(
            changedSectors,
            changedPhysicalBytes,
            Convert.ToHexStringLower(diffHash.GetHashAndReset()));
    }

    private static Id65AuthoringSingleSectorSubstrateRuntimeCandidateReceipt
        BuildReceipt(
            ParentCapture parent,
            Id65CompiledAuthoringSingleSectorSubstrate compiled,
            Id65AuthoringSingleSectorAggregatePlan aggregatePlan,
            string publicationIdentity,
            CandidateReadback readback)
    {
        Id65AuthoringSingleSectorSubstrateRuntimeCandidateReceipt receipt = new()
        {
            Format = Format,
            SchemaVersion = ReceiptSchemaVersion,
            ProfileId = ProfileId,
            PublicationIdentitySha256 = publicationIdentity,
            ParentReceiptFormat = parent.Receipt.Format,
            ParentReceiptSchemaVersion = parent.Receipt.SchemaVersion,
            ParentReceiptProfileId = parent.Receipt.ProfileId,
            ParentCandidateDirectoryName = Path.GetFileName(parent.DirectoryPath),
            ParentCandidateImageFileName = parent.Receipt.CandidateImageFileName,
            ParentCandidateCueFileName = parent.Receipt.CandidateCueFileName,
            ParentCandidateReceiptFileName = Path.GetFileName(parent.ReceiptPath),
            ParentCandidateImageSha256 = parent.ImageSha256,
            ParentCandidateCueSha256 = parent.Receipt.CandidateCueSha256,
            ParentCandidateReceiptSha256 = parent.ReceiptFileSha256,
            ParentReceiptContentSha256 = parent.Receipt.ReceiptContentSha256,
            ParentAggregatePlanSha256 = parent.Receipt.AggregatePlanSha256,
            ParentDocumentEnvelopeSha256 = parent.Receipt.DocumentEnvelopeSha256,
            DocumentCanonicalSha256 = compiled.DocumentCanonicalSha256,
            ActiveTerrainUnitIdsCanonical =
                string.Join('\n', compiled.ActiveTerrainUnitIds),
            MobyKindId = Id65RemotePadMobyChoiceCatalog.GetKindId(compiled.MobyKind),
            MobyPositionId = compiled.MobyPositionId,
            CompilerProfileId = compiled.ProfileId,
            CompilerSchemaVersion = compiled.SchemaVersion,
            CompilerDeterministicPlanSha256 = compiled.DeterministicPlanSha256,
            AggregateProjectorProfileId = aggregatePlan.ProfileId,
            AggregateProjectorSchemaVersion = aggregatePlan.SchemaVersion,
            AggregatePlanSha256 = aggregatePlan.DeterministicAggregatePlanSha256,
            Schema6CompilerProfileId = Id65AuthoringBlankCycloramaCompiler.ProfileId,
            Schema6DeterministicPlanSha256 = compiled.Schema6DeterministicPlanSha256,
            Schema6BaselineRow80Sha256 = compiled.Schema6BaselineRow80Sha256,
            OutputRow80Sha256 = compiled.OutputRow80Sha256,
            OutputModelSha256 = compiled.OutputModelSha256,
            OutputTextureComponentSha256 = compiled.OutputTextureComponentSha256,
            OutputCycloramaSha256 = compiled.OutputCycloramaSha256,
            CollisionSha256 = compiled.CollisionSha256,
            EnvironmentRow80Offset = compiled.Environment.Row80Offset,
            EnvironmentWadOffset = compiled.Environment.WadOffset,
            EnvironmentByteLength = compiled.Environment.ComponentByteLength,
            LogicalSourceSectorIndex = compiled.Environment.LogicalSourceSectorIndex,
            PhysicalOutputSectorIndex = compiled.Environment.PhysicalOutputSectorIndex,
            RetainedSectorByteLength = compiled.Environment.SectorByteLength,
            EnvironmentPointerGapByteLength =
                compiled.Environment.PointerGapByteLength,
            BaselineEnvironmentSha256 =
                compiled.Environment.BaselineEnvironmentSha256,
            OutputEnvironmentSha256 = compiled.Environment.OutputEnvironmentSha256,
            RetainedSectorSha256 = compiled.Environment.RetainedSectorSha256,
            EnvironmentZeroGapSha256 = compiled.Environment.ZeroGapSha256,
            OcclusionRow80Offset = compiled.Occlusion.Row80Offset,
            OcclusionWadOffset = compiled.Occlusion.WadOffset,
            OcclusionByteLength = compiled.Occlusion.ComponentByteLength,
            Schema6OcclusionSha256 = compiled.Occlusion.Schema6OcclusionSha256,
            OutputOcclusionSha256 = compiled.Occlusion.OutputOcclusionSha256,
            OcclusionOpaqueTailSha256 = compiled.Occlusion.OpaqueTailSha256,
            StageChangedLogicalByteCount = compiled.StageChangedByteCount,
            StageOwnedRangeMapSha256 = compiled.StageOwnedRangeMapSha256,
            StageTransactionSha256 = compiled.StageTransactionSha256,
            StageProtectedComplementSha256 =
                compiled.StageProtectedComplementSha256,
            ComponentRelocationMapSha256 = compiled.ComponentRelocationMapSha256,
            HandleRebaseMapSha256 = compiled.HandleRebaseMapSha256,
            CandidateImageFileName = CandidateImageFileName,
            CandidateCueFileName = CandidateCueFileName,
            CandidateReceiptFileName = CandidateReceiptFileName,
            OutputImageSha256 = readback.OutputImageSha256,
            CandidateCueSha256 = Hash(Encoding.ASCII.GetBytes(BuildCanonicalCue())),
            TargetDataWadEntry = TargetDataWadEntry,
            TargetDataWadOffset = TargetDataWadOffset,
            TargetDataByteLength = TargetDataByteLength,
            FirstRebuiltRawSectorLba = FirstRebuiltRawSectorLba,
            RebuiltRawSectorCount = RebuiltRawSectorCount,
            ChangedRawSectorCount = readback.ChangedRawSectorCount,
            ChangedPhysicalImageBytes = readback.ChangedPhysicalBytes,
            PhysicalDiffSha256 = readback.PhysicalDiffSha256,
            ExactParentReceiptVerified = true,
            ExactParentImageVerified = true,
            ExactSchema6StagePreimageVerified = true,
            ExactSchema6StageInverseVerified = true,
            ExactOutputRow80ReadbackVerified = true,
            SingleSectorEnvironmentReadbackVerified = true,
            SingleSectorOcclusionReadbackVerified = true,
            CollisionPreservedExactly = true,
            CycloramaPreservedExactly = true,
            SpawnAndMobyBytesPreservedExactly = true,
            Wad79Preserved = true,
            RetailTownSquareWad15Preserved = true,
            RetailTownSquareWad16Preserved = true,
            RetailArtisansWad9Preserved = true,
            RetailArtisansWad10Preserved = true,
            RetailTownSquareSkyBlockPreserved = true,
            RetailArtisansLinkedSkyBlockPreserved = true,
            ExecutablePreserved = true,
            RawSectorIntegrityVerified = true,
            PhysicalComplementVerified = true,
            ParentSchema6CandidatePreserved = true,
            ContainsFullDiscImage = true,
            ContainsCue = true,
            SeparateGuardedCandidatePublisherAuthorized = true,
            RuntimeCandidateAuthorized = false,
            RuntimeProven = false,
            AppIntegrated = false,
            UiIntegrated = false,
            CreateBinEnabled = false,
            NormalCreateBinEnabled = false,
            DocumentPersistenceIntegrated = false,
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

    private static async Task<string> RequireExactPublishedCandidateAsync(
        Id65AuthoringSingleSectorSubstrateRuntimeCandidatePaths paths,
        Id65AuthoringSingleSectorSubstrateRuntimeCandidateReceipt expectedReceipt,
        CancellationToken cancellationToken)
    {
        RefuseReparsePoint(paths.CandidateDirectoryPath, "schema-7 candidate directory");
        RefuseReparsePoint(paths.CandidateImagePath, "schema-7 candidate image");
        RefuseReparsePoint(paths.CandidateCuePath, "schema-7 candidate cue");
        RefuseReparsePoint(paths.CandidateReceiptPath, "schema-7 candidate receipt");
        string cue = await File.ReadAllTextAsync(
            paths.CandidateCuePath,
            Encoding.ASCII,
            cancellationToken);
        if (cue != BuildCanonicalCue() ||
            Hash(Encoding.ASCII.GetBytes(cue)) != expectedReceipt.CandidateCueSha256)
        {
            throw new InvalidDataException(
                "The schema-7 candidate CUE failed exact readback.");
        }
        byte[] receiptBytes = await ReadBoundedFileAsync(
            paths.CandidateReceiptPath,
            MaximumReceiptByteCount,
            cancellationToken);
        Id65AuthoringSingleSectorSubstrateRuntimeCandidateReceipt actual;
        try
        {
            actual = JsonSerializer.Deserialize<
                Id65AuthoringSingleSectorSubstrateRuntimeCandidateReceipt>(
                    receiptBytes,
                    ReceiptJsonOptions) ?? throw new InvalidDataException(
                        "The schema-7 candidate receipt is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The schema-7 candidate receipt is not valid JSON.",
                exception);
        }
        if (actual != expectedReceipt ||
            !receiptBytes.SequenceEqual(SerializeReceipt(actual)) ||
            actual.ReceiptContentSha256 != ComputeReceiptContentSha256(actual))
        {
            throw new InvalidDataException(
                "The schema-7 candidate receipt failed canonical readback.");
        }
        string imageHash = await HashFileAsync(
            paths.CandidateImagePath,
            cancellationToken);
        if (imageHash != expectedReceipt.OutputImageSha256)
            throw new InvalidDataException(
                "The schema-7 candidate BIN changed after publication.");
        return Hash(receiptBytes);
    }

    private static Id65AuthoringSingleSectorSubstrateRuntimeCandidatePaths
        CreatePaths(
            string outputRoot,
            string positionId,
            string publicationIdentity)
    {
        if (!IsCanonicalSha256(publicationIdentity))
            throw new InvalidDataException(
                "The schema-7 publication identity is not canonical SHA-256.");
        _ = Id65RemotePadGrassT107Catalog.GetRequiredPosition(positionId);
        string root = Path.GetFullPath(outputRoot);
        string directoryName =
            $"{positionId}-schema7-single-sector-substrate-{publicationIdentity}";
        string directory = Path.Combine(root, directoryName);
        return new(
            root,
            directory,
            Path.Combine(directory, CandidateImageFileName),
            Path.Combine(directory, CandidateCueFileName),
            Path.Combine(directory, CandidateReceiptFileName),
            Path.Combine(root, WriterLeaseFileName));
    }

    private static string ComputePublicationIdentity(
        ParentCapture parent,
        Id65CompiledAuthoringSingleSectorSubstrate compiled,
        Id65AuthoringSingleSectorAggregatePlan aggregatePlan) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            ReceiptSchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            parent.ReceiptFileSha256,
            parent.ImageSha256,
            parent.Receipt.AggregatePlanSha256,
            compiled.ProfileId,
            compiled.DeterministicPlanSha256,
            aggregatePlan.ProfileId,
            aggregatePlan.DeterministicAggregatePlanSha256,
            compiled.Schema6BaselineRow80Sha256,
            compiled.OutputRow80Sha256
        ])));

    private static void RequireSameParent(ParentCapture expected, ParentCapture actual)
    {
        if (!PathsEqual(expected.DirectoryPath, actual.DirectoryPath) ||
            expected.ImageSha256 != actual.ImageSha256 ||
            expected.ReceiptFileSha256 != actual.ReceiptFileSha256 ||
            expected.Receipt != actual.Receipt)
        {
            throw new InvalidDataException(
                "The schema-6 parent candidate changed during schema-7 publication.");
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
                "The staged schema-7 parent copy was truncated.");
        destination.Position = 0;
        string copiedSha256 = Convert.ToHexStringLower(
            await SHA256.HashDataAsync(destination, cancellationToken));
        if (copiedSha256 != expectedSha256)
            throw new InvalidDataException(
                "The staged schema-7 parent copy changed before patching.");
    }

    private static void RequireEqualWadComponent(
        FileStream parent,
        FileStream output,
        DiscLayout layout,
        long wadOffset,
        int byteLength,
        string label) =>
        RequireEqualFileComponent(
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
                $"The schema-7 stage changed protected {label}.");
    }

    private static void VerifyWad80Submodes(FileStream image, DiscLayout layout)
    {
        for (int lba = FirstRebuiltRawSectorLba;
             lba <= LastRebuiltRawSectorLba;
             lba++)
        {
            RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(
                image,
                layout,
                lba,
                lba == LastRebuiltRawSectorLba
                    ? XaFinalDataSubmode
                    : XaDataSubmode);
        }
    }

    private static DiscFileRecord FindWad(FileStream stream, DiscLayout layout) =>
        DiscImage.FindRootFileRecord(
            stream,
            layout,
            name => string.Equals(
                name,
                "WAD.WAD",
                StringComparison.OrdinalIgnoreCase));

    private static DiscFileRecord FindExecutable(
        FileStream stream,
        DiscLayout layout) =>
        DiscImage.FindRootFileRecord(
            stream,
            layout,
            name => name.StartsWith(
                "SCUS_942.28",
                StringComparison.OrdinalIgnoreCase));

    private static FileStream AcquireWriterLease(string path)
    {
        RefuseReparsePoint(path, "schema-7 candidate writer lease");
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
                "Another schema-7 candidate publisher owns the writer lease.",
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
        Id65AuthoringSingleSectorSubstrateRuntimeCandidateReceipt receipt) =>
        Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(receipt, ReceiptJsonOptions) + "\n");

    private static string ComputeReceiptContentSha256(
        Id65AuthoringSingleSectorSubstrateRuntimeCandidateReceipt receipt) =>
        Hash(JsonSerializer.SerializeToUtf8Bytes(
            receipt with { ReceiptContentSha256 = "" },
            CompactJsonOptions));

    private static string BuildCanonicalCue() =>
        $"FILE \"{CandidateImageFileName}\" BINARY\n" +
        "  TRACK 01 MODE2/2352\n" +
        "    INDEX 01 00:00:00\n";

    private static string BuildCanonicalParentCue(string imageFileName) =>
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
                "A guarded candidate receipt has an invalid byte length.");
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
        string fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        return fullPath.StartsWith(
            fullRoot + Path.DirectorySeparatorChar,
            StringComparison.Ordinal);
    }

    private static void RefuseReparsePoint(string path, string context)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            return;
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
                "The schema-7 temporary operation escaped its exact owned root.");
        }
    }

    private static void DeleteOwnedTemporary(
        string temporaryRoot,
        string candidateRoot)
    {
        RequireOwnedTemporaryPath(temporaryRoot, candidateRoot);
        Directory.Delete(temporaryRoot, recursive: true);
    }

    private sealed record ParentCapture(
        string DirectoryPath,
        string ImagePath,
        string CuePath,
        string ReceiptPath,
        Id65AuthoringBlankCycloramaAggregateCandidateReceipt Receipt,
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
}

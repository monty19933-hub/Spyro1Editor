using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

internal sealed record Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublishRequest(
    string ParentSchema7CandidateDirectoryPath,
    Id65CompiledAuthoringMinimalCycloramaAndVisibility Compiled);

internal sealed class Id65AuthoringSchema8RawSectorPatch
{
    private readonly byte[] _beforeRawSector;
    private readonly byte[] _afterRawSector;

    internal Id65AuthoringSchema8RawSectorPatch(
        int lba,
        byte[] beforeRawSector,
        byte[] afterRawSector,
        int userDataChangedByteCount,
        int physicalChangedByteCount,
        bool intersectsVisibilityDirectory,
        bool intersectsCyclorama)
    {
        Lba = lba;
        _beforeRawSector = beforeRawSector.ToArray();
        _afterRawSector = afterRawSector.ToArray();
        BeforeRawSectorSha256 =
            Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher.Hash(
                _beforeRawSector);
        AfterRawSectorSha256 =
            Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher.Hash(
                _afterRawSector);
        UserDataChangedByteCount = userDataChangedByteCount;
        PhysicalChangedByteCount = physicalChangedByteCount;
        IntersectsVisibilityDirectory = intersectsVisibilityDirectory;
        IntersectsCyclorama = intersectsCyclorama;
    }

    public int Lba { get; }
    public long PhysicalImageOffset => checked((long)Lba * 2352);
    public int RawSectorByteLength => _beforeRawSector.Length;
    public string BeforeRawSectorSha256 { get; }
    public string AfterRawSectorSha256 { get; }
    public int UserDataChangedByteCount { get; }
    public int PhysicalChangedByteCount { get; }
    public bool IntersectsVisibilityDirectory { get; }
    public bool IntersectsCyclorama { get; }
    public byte DuplicatedSubmode => _beforeRawSector[18];
    internal byte[] CopyBeforeRawSector() => _beforeRawSector.ToArray();
    internal byte[] CopyAfterRawSector() => _afterRawSector.ToArray();
    internal Id65AuthoringSchema8RawSectorPatch DeepCopy() => new(
        Lba,
        _beforeRawSector,
        _afterRawSector,
        UserDataChangedByteCount,
        PhysicalChangedByteCount,
        IntersectsVisibilityDirectory,
        IntersectsCyclorama);
}

internal sealed record Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublicationReceipt
{
    public required string Format { get; init; }
    public required int SchemaVersion { get; init; }
    public required string ProfileId { get; init; }
    public required string PublicationIdentitySha256 { get; init; }
    public required string ProspectiveCandidateDirectoryName { get; init; }
    public required string ParentCandidateDirectoryName { get; init; }
    public required string ParentCandidateImageFileName { get; init; }
    public required string ParentCandidateCueFileName { get; init; }
    public required string ParentCandidateReceiptFileName { get; init; }
    public required string ParentPublicationIdentitySha256 { get; init; }
    public required string ParentImageSha256 { get; init; }
    public required string ParentCueSha256 { get; init; }
    public required string ParentReceiptFileSha256 { get; init; }
    public required string ParentReceiptContentSha256 { get; init; }
    public required string DocumentCanonicalSha256 { get; init; }
    public required string CompilerProfileId { get; init; }
    public required int CompilerSchemaVersion { get; init; }
    public required string CompilerDeterministicPlanSha256 { get; init; }
    public required string Schema7BaselineRow80Sha256 { get; init; }
    public required string OutputRow80Sha256 { get; init; }
    public required string OutputModelSha256 { get; init; }
    public required string OutputCycloramaSha256 { get; init; }
    public required string OutputVisibilityDirectorySha256 { get; init; }
    public required string OutputOcclusionSha256 { get; init; }
    public required int AtomicOwnedRangeCount { get; init; }
    public required int AtomicOwnedByteCount { get; init; }
    public required int AtomicChangedLogicalByteCount { get; init; }
    public required string CompilerOwnedRangeMapSha256 { get; init; }
    public required string CompilerTransactionSha256 { get; init; }
    public required string CompilerProtectedComplementSha256 { get; init; }
    public required int RawSectorPatchCount { get; init; }
    public required int RawSectorRunCount { get; init; }
    public required string RawSectorLbasCanonical { get; init; }
    public required int VisibilityDirectoryRawSectorLba { get; init; }
    public required int FirstCycloramaRawSectorLba { get; init; }
    public required int CycloramaRawSectorCount { get; init; }
    public required int PhysicalChangedByteCount { get; init; }
    public required string RawSectorPatchMapSha256 { get; init; }
    public required string PhysicalDiffSha256 { get; init; }
    public required bool ExactParentSchema7SiblingVerified { get; init; }
    public required bool ExactSchema7PreimageVerified { get; init; }
    public required bool ExactSchema7InverseVerified { get; init; }
    public required bool ExactSchema8AfterimageProjected { get; init; }
    public required bool ExactlyTwoFullAtomicRangesVerified { get; init; }
    public required bool EighteenNoncontiguousRawSectorsVerified { get; init; }
    public required bool RawMode2Form1IntegrityVerified { get; init; }
    public required bool BcdMsfVerified { get; init; }
    public required bool DuplicatedSubheadersVerified { get; init; }
    public required bool PhysicalComplementPlanningVerified { get; init; }
    public required bool PlanningOnly { get; init; }
    public required bool PublicationPerformed { get; init; }
    public required bool FileSystemMutationPerformed { get; init; }
    public required bool ContainsFullDiscImage { get; init; }
    public required bool ContainsCue { get; init; }
    public required bool WritesFileSystem { get; init; }
    public required bool WritesDiscImage { get; init; }
    public required bool WritesCue { get; init; }
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

internal sealed class Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublicationPlan
{
    private readonly ReadOnlyCollection<Id65AuthoringSchema8RawSectorPatch>
        _rawSectorPatches;

    internal Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublicationPlan(
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublicationReceipt
            receipt,
        IEnumerable<Id65AuthoringSchema8RawSectorPatch> rawSectorPatches)
    {
        Receipt = receipt with { };
        _rawSectorPatches = Array.AsReadOnly(
            rawSectorPatches.Select(item => item.DeepCopy()).ToArray());
    }

    public Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublicationReceipt
        Receipt { get; }
    public IReadOnlyList<Id65AuthoringSchema8RawSectorPatch>
        RawSectorPatches => _rawSectorPatches;
    public string PublicationIdentitySha256 =>
        Receipt.PublicationIdentitySha256;
    public string ProspectiveCandidateDirectoryName =>
        Receipt.ProspectiveCandidateDirectoryName;
    public string ReceiptContentSha256 => Receipt.ReceiptContentSha256;
    public bool PlanningOnly => true;
    public bool PublicationPerformed => false;
    public bool FileSystemMutationPerformed => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool RuntimeProven => false;
    public bool NormalCreateBinEnabled => false;
    public bool ReleaseIntegrated => false;
    public bool Publishable => false;
}

/// <summary>
/// Builds a deterministic, content-addressed schema-8 publication plan from
/// the exact immutable schema-7 p1 sibling. It projects and integrity-rebuilds
/// only the 18 affected raw MODE2 sectors in memory. It cannot create a BIN,
/// CUE, receipt file, directory, runtime candidate, or release artifact.
/// </summary>
internal static class
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
{
    public const string Format =
        "spyro-editor-id65-authoring-schema8-minimal-cyclorama-and-visibility-guarded-publication-plan";
    public const int ReceiptSchemaVersion = 1;
    public const string ProfileId =
        "id65-authoring-schema8-minimal-cyclorama-and-visibility-guarded-publisher-plan-v1";
    public const int WadLba =
        Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher.WadLba;
    public const long TargetDataWadOffset =
        Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
            .TargetDataWadOffset;
    public const int TargetDataByteLength =
        Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
            .TargetDataByteLength;
    public const int RawSectorByteLength = 2352;
    public const int LogicalSectorByteLength = 2048;
    public const int UserDataOffset = 24;
    public const int VisibilityDirectoryRawSectorLba = 54_438;
    public const int FirstCycloramaRawSectorLba = 54_630;
    public const int CycloramaRawSectorCount = 17;
    public const int LastCycloramaRawSectorLba =
        FirstCycloramaRawSectorLba + CycloramaRawSectorCount - 1;
    public const int ExpectedRawSectorPatchCount = 18;
    public const int ExpectedRawSectorRunCount = 2;
    public const string ExpectedParentPublicationIdentitySha256 =
        "30e0e505cd47ce2daecd21266c89adca4358523b6a15425aedbce743936bd0d1";
    public const string ExpectedParentImageSha256 =
        "52e83343166080ba3ce287a4e8578d992153730e2ffa93b4b604e529499a1139";
    public const string ExpectedParentCueSha256 =
        "45be1d27bd8a428f8d010674b37cc150b1806f8f2d9407f81baf7e764788c0ba";
    public const string ExpectedParentReceiptFileSha256 =
        "5328e11a02106c77945759993d7218d195f15ed6e1804e8079d3b2064c556dcc";
    public const string ExpectedParentReceiptContentSha256 =
        "0735d16740ecf9c8a7ce85c75f19743c48bc62f92e29d5ae97a01da7d98e0348";
    public const int ExpectedPhysicalChangedByteCount = 37_311;
    public const string ExpectedRawSectorPatchMapSha256 =
        "a23ce7b902f22dfc50b0585f4d4913cc33e75c07d96a513d7838753b95d2cd3d";
    public const string ExpectedPhysicalDiffSha256 =
        "1dfa98247e5881255a0c9314aa0da8dc630266b5f73313b6d23e7b15bbe9c927";
    public const string ExpectedPublicationIdentitySha256 =
        "db6f52158bb425d8b2d665f58e6f0c71f7f51933c4f1d054434b2ea088b6c2ef";
    public const string ExpectedReceiptContentSha256 =
        "4bf1655ade1fae703d0c7e287177591dca53d1d5d6581e34265611c407a8af43";
    public const long ExpectedParentImageByteLength =
        Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
            .ExpectedImageByteLength;

    private const int MaximumReceiptByteCount = 256 * 1024;

    private static readonly JsonSerializerOptions ReceiptJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = false
    };

    public static async Task<
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublicationPlan>
        PlanAsync(
            Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublishRequest
                request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.ParentSchema7CandidateDirectoryPath);
        ArgumentNullException.ThrowIfNull(request.Compiled);
        cancellationToken.ThrowIfCancellationRequested();
        RequireExactCompilerBoundary(request.Compiled);

        ParentCapture parent = await CaptureParentAsync(
            request.ParentSchema7CandidateDirectoryPath,
            request.Compiled,
            cancellationToken);
        Id65AuthoringSchema8RawSectorPatch[] patches = BuildRawSectorPatches(
            parent,
            request.Compiled);
        PatchPlanReadback readback = RequireExactPatchPlan(
            request.Compiled,
            parent.Row80,
            patches);
        if (readback.PhysicalChangedByteCount !=
                ExpectedPhysicalChangedByteCount ||
            readback.RawSectorPatchMapSha256 !=
                ExpectedRawSectorPatchMapSha256 ||
            readback.PhysicalDiffSha256 != ExpectedPhysicalDiffSha256)
        {
            throw new InvalidDataException(
                "The exact schema-8 physical patch identity changed.");
        }
        string publicationIdentity = ComputePublicationIdentity(
            parent,
            request.Compiled,
            readback.RawSectorPatchMapSha256);
        if (publicationIdentity != ExpectedPublicationIdentitySha256)
            throw new InvalidDataException(
                "The schema-8 content-addressed publication identity changed.");
        string directoryName =
            request.Compiled.MobyPositionId +
            "-schema8-minimal-cyclorama-and-visibility-" +
            publicationIdentity;
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublicationReceipt
            receipt = BuildReceipt(
                parent,
                request.Compiled,
                patches,
                readback,
                publicationIdentity,
                directoryName);
        if (receipt.ReceiptContentSha256 != ExpectedReceiptContentSha256)
            throw new InvalidDataException(
                "The schema-8 in-memory receipt identity changed.");
        return new(receipt, patches);
    }

    internal static PatchPlanReadback RequireExactPatchPlan(
        Id65CompiledAuthoringMinimalCycloramaAndVisibility compiled,
        ReadOnlySpan<byte> parentRow80,
        IReadOnlyList<Id65AuthoringSchema8RawSectorPatch> patches)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        ArgumentNullException.ThrowIfNull(patches);
        if (parentRow80.Length != TargetDataByteLength ||
            !parentRow80.SequenceEqual(compiled.CopySchema7BaselineRow80()) ||
            patches.Count != ExpectedRawSectorPatchCount ||
            patches.Select(item => item.Lba).Distinct().Count() !=
                patches.Count ||
            !patches.Select(item => item.Lba).SequenceEqual(
                ExpectedRawSectorLbas()))
        {
            throw new InvalidDataException(
                "The schema-8 raw-sector plan lost its exact parent or 18-sector map.");
        }
        byte[] reconstructed = parentRow80.ToArray();
        byte[] expectedOutputRow80 = compiled.CopyOutputRow80();
        int logicalChanged = 0;
        int physicalChanged = 0;
        using IncrementalHash physicalDiffHash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] identity = new byte[10];
        foreach (Id65AuthoringSchema8RawSectorPatch patch in patches)
        {
            byte[] before = patch.CopyBeforeRawSector();
            byte[] after = patch.CopyAfterRawSector();
            if (before.Length != RawSectorByteLength ||
                after.Length != RawSectorByteLength ||
                Hash(before) != patch.BeforeRawSectorSha256 ||
                Hash(after) != patch.AfterRawSectorSha256 ||
                before.SequenceEqual(after) ||
                !before.AsSpan(0, UserDataOffset)
                    .SequenceEqual(after.AsSpan(0, UserDataOffset)) ||
                before[16] != after[16] || before[17] != after[17] ||
                before[18] != after[18] || before[19] != after[19] ||
                before[20] != after[20] || before[21] != after[21] ||
                before[22] != after[22] || before[23] != after[23])
            {
                throw new InvalidDataException(
                    "A schema-8 raw-sector patch lost its exact raw binding or protected header/subheader.");
            }
            RawMode2Form1SectorIntegrity.RequireRawSectorIntegrity(
                before,
                patch.Lba);
            RawMode2Form1SectorIntegrity.RequireRawSectorIntegrity(
                after,
                patch.Lba);
            int sectorLogicalChanged = 0;
            int sectorPhysicalChanged = 0;
            bool intersectsVisibility = false;
            bool intersectsCyclorama = false;
            for (int index = 0; index < LogicalSectorByteLength; index++)
            {
                long wadOffset = checked(
                    ((long)patch.Lba - WadLba) *
                        LogicalSectorByteLength + index);
                long rowRelative = wadOffset - TargetDataWadOffset;
                if (rowRelative < 0 || rowRelative >= TargetDataByteLength)
                    continue;
                int rowIndex = checked((int)rowRelative);
                if (before[UserDataOffset + index] != parentRow80[rowIndex] ||
                    after[UserDataOffset + index] !=
                        expectedOutputRow80[rowIndex])
                {
                    throw new InvalidDataException(
                        "A schema-8 raw-sector user payload lost its exact row projection.");
                }
                reconstructed[rowIndex] = after[UserDataOffset + index];
                if (before[UserDataOffset + index] !=
                    after[UserDataOffset + index])
                    sectorLogicalChanged++;
                intersectsVisibility |= InRange(
                    rowIndex,
                    compiled.VisibilityDirectoryRow80Offset,
                    compiled.VisibilityDirectoryByteLength);
                intersectsCyclorama |= InRange(
                    rowIndex,
                    compiled.CycloramaRow80Offset,
                    compiled.CycloramaByteLength);
            }
            for (int index = 0; index < RawSectorByteLength; index++)
            {
                if (before[index] == after[index]) continue;
                sectorPhysicalChanged++;
                BinaryPrimitives.WriteInt32LittleEndian(
                    identity.AsSpan(0, 4),
                    patch.Lba);
                BinaryPrimitives.WriteInt32LittleEndian(
                    identity.AsSpan(4, 4),
                    index);
                identity[8] = before[index];
                identity[9] = after[index];
                physicalDiffHash.AppendData(identity);
            }
            if (sectorLogicalChanged <= 0 ||
                sectorLogicalChanged != patch.UserDataChangedByteCount ||
                sectorPhysicalChanged != patch.PhysicalChangedByteCount ||
                intersectsVisibility != patch.IntersectsVisibilityDirectory ||
                intersectsCyclorama != patch.IntersectsCyclorama ||
                intersectsVisibility == intersectsCyclorama)
            {
                throw new InvalidDataException(
                    "A schema-8 raw-sector patch lost its exact logical/physical census or ownership class.");
            }
            logicalChanged = checked(logicalChanged + sectorLogicalChanged);
            physicalChanged = checked(physicalChanged + sectorPhysicalChanged);
        }
        if (!reconstructed.SequenceEqual(expectedOutputRow80) ||
            logicalChanged != compiled.ChangedByteCount ||
            patches.Count(item => item.IntersectsVisibilityDirectory) != 1 ||
            patches.Count(item => item.IntersectsCyclorama) !=
                CycloramaRawSectorCount ||
            CountRuns(patches.Select(item => item.Lba)) !=
                ExpectedRawSectorRunCount)
        {
            throw new InvalidDataException(
                "The schema-8 raw-sector projection did not reconstruct the exact compiler afterimage.");
        }
        byte[] restored =
            Id65AuthoringMinimalCycloramaAndVisibilityCompiler
                .ApplySchema7StageTransactional(
                    compiled,
                    reconstructed,
                    reverse: true);
        if (!restored.SequenceEqual(parentRow80))
            throw new InvalidDataException(
                "The schema-8 raw-sector plan failed the compiler inverse.");
        string patchMapSha256 = HashPatchMap(patches);
        return new(
            logicalChanged,
            physicalChanged,
            patchMapSha256,
            Convert.ToHexStringLower(
                physicalDiffHash.GetHashAndReset()));
    }

    private static Id65AuthoringSchema8RawSectorPatch[] BuildRawSectorPatches(
        ParentCapture parent,
        Id65CompiledAuthoringMinimalCycloramaAndVisibility compiled)
    {
        int[] derivedLbas = DeriveRawSectorLbas(compiled);
        if (!derivedLbas.SequenceEqual(ExpectedRawSectorLbas()))
            throw new InvalidDataException(
                "The two schema-8 compiler ranges no longer derive the exact 18 noncontiguous raw sectors.");
        byte[] outputRow = compiled.CopyOutputRow80();
        List<Id65AuthoringSchema8RawSectorPatch> patches = [];
        using FileStream image = new(
            parent.ImagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1 << 20,
            FileOptions.RandomAccess);
        foreach (int lba in derivedLbas)
        {
            byte[] before = new byte[RawSectorByteLength];
            image.Position = checked((long)lba * RawSectorByteLength);
            image.ReadExactly(before);
            RawMode2Form1SectorIntegrity.RequireRawSectorIntegrity(before, lba);
            byte[] afterPayload = before.ToArray();
            int userChanged = 0;
            bool intersectsVisibility = false;
            bool intersectsCyclorama = false;
            for (int index = 0; index < LogicalSectorByteLength; index++)
            {
                long wadOffset = checked(
                    ((long)lba - WadLba) * LogicalSectorByteLength + index);
                long rowRelative = wadOffset - TargetDataWadOffset;
                if (rowRelative < 0 || rowRelative >= TargetDataByteLength)
                    continue;
                int rowIndex = checked((int)rowRelative);
                if (before[UserDataOffset + index] != parent.Row80[rowIndex])
                    throw new InvalidDataException(
                        "The exact schema-7 sibling raw payload differs from its WAD80 row readback.");
                byte replacement = outputRow[rowIndex];
                if (afterPayload[UserDataOffset + index] != replacement)
                    userChanged++;
                afterPayload[UserDataOffset + index] = replacement;
                intersectsVisibility |= InRange(
                    rowIndex,
                    compiled.VisibilityDirectoryRow80Offset,
                    compiled.VisibilityDirectoryByteLength);
                intersectsCyclorama |= InRange(
                    rowIndex,
                    compiled.CycloramaRow80Offset,
                    compiled.CycloramaByteLength);
            }
            byte[] after =
                RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(
                    afterPayload,
                    lba);
            int physicalChanged = CountChangedBytes(before, after);
            if (userChanged <= 0 || physicalChanged < userChanged)
                throw new InvalidDataException(
                    "A derived schema-8 raw sector contains no owned logical change.");
            patches.Add(new(
                lba,
                before,
                after,
                userChanged,
                physicalChanged,
                intersectsVisibility,
                intersectsCyclorama));
        }
        return patches.ToArray();
    }

    private static int[] DeriveRawSectorLbas(
        Id65CompiledAuthoringMinimalCycloramaAndVisibility compiled)
    {
        SortedSet<int> sectors = [];
        foreach (Id65AuthoringCompositeOwnedRange range in
                 compiled.OwnedRanges)
        {
            long wadStart = checked(
                TargetDataWadOffset + range.DataRelativeOffset);
            long wadEnd = checked(wadStart + range.ByteLength - 1);
            int first = checked(WadLba + (int)(wadStart /
                LogicalSectorByteLength));
            int last = checked(WadLba + (int)(wadEnd /
                LogicalSectorByteLength));
            for (int lba = first; lba <= last; lba++) sectors.Add(lba);
        }
        return sectors.ToArray();
    }

    private static int[] ExpectedRawSectorLbas() =>
    [
        VisibilityDirectoryRawSectorLba,
        .. Enumerable.Range(
            FirstCycloramaRawSectorLba,
            CycloramaRawSectorCount)
    ];

    private static async Task<ParentCapture> CaptureParentAsync(
        string parentDirectory,
        Id65CompiledAuthoringMinimalCycloramaAndVisibility compiled,
        CancellationToken cancellationToken)
    {
        string directory = Path.GetFullPath(parentDirectory);
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException(
                "The exact schema-7 sibling directory does not exist.");
        RefuseReparsePoint(directory, "schema-7 sibling directory");
        string imagePath = Path.Combine(
            directory,
            Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
                .CandidateImageFileName);
        string cuePath = Path.Combine(
            directory,
            Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
                .CandidateCueFileName);
        string receiptPath = Path.Combine(
            directory,
            Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
                .CandidateReceiptFileName);
        RefuseReparsePoint(imagePath, "schema-7 sibling image");
        RefuseReparsePoint(cuePath, "schema-7 sibling CUE");
        RefuseReparsePoint(receiptPath, "schema-7 sibling receipt");
        if (!File.Exists(imagePath) || !File.Exists(cuePath) ||
            !File.Exists(receiptPath))
            throw new FileNotFoundException(
                "The exact schema-7 sibling artifact set is incomplete.");
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
                        "The schema-7 sibling receipt is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The schema-7 sibling receipt is invalid JSON.",
                exception);
        }
        string receiptFileSha256 = Hash(receiptBytes);
        string cueSha256 = await HashFileAsync(cuePath, cancellationToken);
        string imageSha256 = await HashFileAsync(imagePath, cancellationToken);
        if (receipt.Format !=
                Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
                    .Format ||
            receipt.SchemaVersion !=
                Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
                    .ReceiptSchemaVersion ||
            receipt.ProfileId !=
                Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
                    .ProfileId ||
            receipt.PublicationIdentitySha256 !=
                ExpectedParentPublicationIdentitySha256 ||
            receipt.OutputImageSha256 != ExpectedParentImageSha256 ||
            imageSha256 != ExpectedParentImageSha256 ||
            receipt.CandidateCueSha256 != ExpectedParentCueSha256 ||
            cueSha256 != ExpectedParentCueSha256 ||
            receiptFileSha256 != ExpectedParentReceiptFileSha256 ||
            receipt.ReceiptContentSha256 !=
                ExpectedParentReceiptContentSha256 ||
            receipt.OutputRow80Sha256 !=
                compiled.Schema7BaselineRow80Sha256 ||
            receipt.CompilerProfileId !=
                Id65AuthoringSingleSectorSubstrateCompiler.ProfileId ||
            receipt.CompilerDeterministicPlanSha256 !=
                compiled.Schema7DeterministicPlanSha256 ||
            receipt.DocumentCanonicalSha256 !=
                compiled.DocumentCanonicalSha256 ||
            receipt.OutputOcclusionSha256 !=
                Id65AuthoringMinimalCycloramaAndVisibilityCompiler
                    .ExpectedSchema7OcclusionSha256 ||
            !receipt.ExactOutputRow80ReadbackVerified ||
            !receipt.RawSectorIntegrityVerified ||
            !receipt.PhysicalComplementVerified ||
            !receipt.ParentSchema6CandidatePreserved ||
            !receipt.ContainsFullDiscImage || !receipt.ContainsCue ||
            !receipt.SeparateGuardedCandidatePublisherAuthorized ||
            receipt.RuntimeCandidateAuthorized || receipt.RuntimeProven ||
            receipt.AppIntegrated || receipt.UiIntegrated ||
            receipt.CreateBinEnabled || receipt.NormalCreateBinEnabled ||
            receipt.ReleaseIntegrated || receipt.PromotionAuthorized ||
            receipt.Publishable)
        {
            throw new InvalidDataException(
                "The parent is not the exact guarded schema-7 Tile1 / Grass T107 / Center sibling.");
        }
        FileInfo imageInfo = new(imagePath);
        if (imageInfo.Length != ExpectedParentImageByteLength)
            throw new InvalidDataException(
                "The schema-7 sibling image length changed.");
        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        if (layout.SectorSize != RawSectorByteLength ||
            layout.UserOffset != UserDataOffset)
            throw new InvalidDataException(
                "The schema-7 sibling is not the exact MODE2/2352 layout.");
        byte[] row80;
        using (FileStream image = new(
            imagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1 << 20,
            FileOptions.RandomAccess))
        {
            DiscFileRecord wad = DiscImage.FindRootFileRecord(
                image,
                layout,
                name => string.Equals(
                    name,
                    "WAD.WAD",
                    StringComparison.OrdinalIgnoreCase));
            if (wad.Lba != WadLba ||
                wad.Size !=
                    Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
                        .ExpectedWadByteLength)
                throw new InvalidDataException(
                    "The schema-7 sibling WAD extent changed.");
            row80 = DiscImage.ReadFileBytes(
                image,
                layout,
                WadLba,
                TargetDataWadOffset,
                TargetDataByteLength);
        }
        if (!row80.SequenceEqual(compiled.CopySchema7BaselineRow80()) ||
            Hash(row80) != compiled.Schema7BaselineRow80Sha256)
            throw new InvalidDataException(
                "The schema-7 sibling row is not the exact compiler preimage.");
        return new(
            directory,
            imagePath,
            cuePath,
            receiptPath,
            imageSha256,
            cueSha256,
            receiptFileSha256,
            receipt,
            row80);
    }

    private static void RequireExactCompilerBoundary(
        Id65CompiledAuthoringMinimalCycloramaAndVisibility compiled)
    {
        if (compiled.ProfileId !=
                Id65AuthoringMinimalCycloramaAndVisibilityCompiler.ProfileId ||
            compiled.SchemaVersion !=
                Id65AuthoringMinimalCycloramaAndVisibilityCompiler
                    .SchemaVersion ||
            compiled.Schema7BaselineRow80Sha256 !=
                Id65AuthoringMinimalCycloramaAndVisibilityCompiler
                    .ExpectedSchema7Tile1GrassCenterRow80Sha256 ||
            compiled.OutputRow80Sha256 !=
                Id65AuthoringMinimalCycloramaAndVisibilityCompiler
                    .ExpectedOutputRow80Sha256 ||
            !string.Equals(
                compiled.OutputCycloramaSha256,
                Id65AuthoringMinimalCycloramaAndVisibilityCompiler
                    .ExpectedOutputCycloramaSha256,
                StringComparison.OrdinalIgnoreCase) ||
            compiled.OutputVisibilityDirectorySha256 !=
                Id65AuthoringMinimalCycloramaAndVisibilityCompiler
                    .ExpectedOutputVisibilityDirectorySha256 ||
            compiled.OutputOcclusionSha256 !=
                Id65AuthoringMinimalCycloramaAndVisibilityCompiler
                    .ExpectedOutputOcclusionSha256 ||
            compiled.OwnedRanges.Count != 2 ||
            compiled.OwnedByteCount != 34_616 ||
            compiled.ChangedByteCount != 32_294 ||
            !compiled.PureInMemory ||
            !compiled.ProducesSchema8AtomicPhysicalStage ||
            !compiled.CycloramaAndVisibilityWrittenAtomically ||
            !compiled.CodeOwnedSkyPartVisibilityVerified ||
            !compiled.ExactInverseVerified ||
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
                "The guarded publisher planner requires the exact closed schema-8 compiler boundary.");
        }
    }

    private static
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublicationReceipt
        BuildReceipt(
            ParentCapture parent,
            Id65CompiledAuthoringMinimalCycloramaAndVisibility compiled,
            IReadOnlyList<Id65AuthoringSchema8RawSectorPatch> patches,
            PatchPlanReadback readback,
            string publicationIdentity,
            string directoryName)
    {
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublicationReceipt
            receipt = new()
            {
                Format = Format,
                SchemaVersion = ReceiptSchemaVersion,
                ProfileId = ProfileId,
                PublicationIdentitySha256 = publicationIdentity,
                ProspectiveCandidateDirectoryName = directoryName,
                ParentCandidateDirectoryName =
                    Path.GetFileName(parent.DirectoryPath),
                ParentCandidateImageFileName =
                    Path.GetFileName(parent.ImagePath),
                ParentCandidateCueFileName = Path.GetFileName(parent.CuePath),
                ParentCandidateReceiptFileName =
                    Path.GetFileName(parent.ReceiptPath),
                ParentPublicationIdentitySha256 =
                    parent.Receipt.PublicationIdentitySha256,
                ParentImageSha256 = parent.ImageSha256,
                ParentCueSha256 = parent.CueSha256,
                ParentReceiptFileSha256 = parent.ReceiptFileSha256,
                ParentReceiptContentSha256 =
                    parent.Receipt.ReceiptContentSha256,
                DocumentCanonicalSha256 = compiled.DocumentCanonicalSha256,
                CompilerProfileId = compiled.ProfileId,
                CompilerSchemaVersion = compiled.SchemaVersion,
                CompilerDeterministicPlanSha256 =
                    compiled.DeterministicPlanSha256,
                Schema7BaselineRow80Sha256 =
                    compiled.Schema7BaselineRow80Sha256,
                OutputRow80Sha256 = compiled.OutputRow80Sha256,
                OutputModelSha256 = compiled.OutputModelSha256,
                OutputCycloramaSha256 =
                    compiled.OutputCycloramaSha256.ToLowerInvariant(),
                OutputVisibilityDirectorySha256 =
                    compiled.OutputVisibilityDirectorySha256,
                OutputOcclusionSha256 = compiled.OutputOcclusionSha256,
                AtomicOwnedRangeCount = compiled.OwnedRanges.Count,
                AtomicOwnedByteCount = compiled.OwnedByteCount,
                AtomicChangedLogicalByteCount = compiled.ChangedByteCount,
                CompilerOwnedRangeMapSha256 = compiled.OwnedRangeMapSha256,
                CompilerTransactionSha256 = compiled.TransactionSha256,
                CompilerProtectedComplementSha256 =
                    compiled.ProtectedComplementSha256,
                RawSectorPatchCount = patches.Count,
                RawSectorRunCount = CountRuns(patches.Select(item => item.Lba)),
                RawSectorLbasCanonical = string.Join(
                    ',',
                    patches.Select(item => item.Lba.ToString(
                        CultureInfo.InvariantCulture))),
                VisibilityDirectoryRawSectorLba =
                    VisibilityDirectoryRawSectorLba,
                FirstCycloramaRawSectorLba = FirstCycloramaRawSectorLba,
                CycloramaRawSectorCount = CycloramaRawSectorCount,
                PhysicalChangedByteCount = readback.PhysicalChangedByteCount,
                RawSectorPatchMapSha256 =
                    readback.RawSectorPatchMapSha256,
                PhysicalDiffSha256 = readback.PhysicalDiffSha256,
                ExactParentSchema7SiblingVerified = true,
                ExactSchema7PreimageVerified = true,
                ExactSchema7InverseVerified = true,
                ExactSchema8AfterimageProjected = true,
                ExactlyTwoFullAtomicRangesVerified = true,
                EighteenNoncontiguousRawSectorsVerified = true,
                RawMode2Form1IntegrityVerified = true,
                BcdMsfVerified = true,
                DuplicatedSubheadersVerified = true,
                PhysicalComplementPlanningVerified = true,
                PlanningOnly = true,
                PublicationPerformed = false,
                FileSystemMutationPerformed = false,
                ContainsFullDiscImage = false,
                ContainsCue = false,
                WritesFileSystem = false,
                WritesDiscImage = false,
                WritesCue = false,
                SeparateGuardedCandidatePublisherAuthorized = false,
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
        string receiptContentSha256 = Hash(
            JsonSerializer.SerializeToUtf8Bytes(receipt, CompactJsonOptions));
        return receipt with
        {
            ReceiptContentSha256 = receiptContentSha256
        };
    }

    private static string ComputePublicationIdentity(
        ParentCapture parent,
        Id65CompiledAuthoringMinimalCycloramaAndVisibility compiled,
        string rawSectorPatchMapSha256) => Hash(Encoding.UTF8.GetBytes(
            string.Join('\n',
            [
                ProfileId,
                ReceiptSchemaVersion.ToString(CultureInfo.InvariantCulture),
                parent.Receipt.PublicationIdentitySha256,
                parent.ImageSha256,
                parent.CueSha256,
                parent.ReceiptFileSha256,
                parent.Receipt.ReceiptContentSha256,
                compiled.DocumentCanonicalSha256,
                compiled.ProfileId,
                compiled.DeterministicPlanSha256,
                compiled.Schema7BaselineRow80Sha256,
                compiled.OutputRow80Sha256,
                compiled.OutputCycloramaSha256.ToLowerInvariant(),
                compiled.OutputVisibilityDirectorySha256,
                compiled.OutputOcclusionSha256,
                compiled.OwnedRangeMapSha256,
                compiled.TransactionSha256,
                compiled.ProtectedComplementSha256,
                rawSectorPatchMapSha256
            ])));

    private static string HashPatchMap(
        IEnumerable<Id65AuthoringSchema8RawSectorPatch> patches)
    {
        StringBuilder canonical = new();
        foreach (Id65AuthoringSchema8RawSectorPatch patch in patches)
        {
            canonical.Append(patch.Lba).Append('|')
                .Append(patch.BeforeRawSectorSha256).Append('|')
                .Append(patch.AfterRawSectorSha256).Append('|')
                .Append(patch.UserDataChangedByteCount).Append('|')
                .Append(patch.PhysicalChangedByteCount).Append('|')
                .Append(patch.IntersectsVisibilityDirectory ? '1' : '0')
                .Append('|')
                .Append(patch.IntersectsCyclorama ? '1' : '0')
                .Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(canonical.ToString()));
    }

    private static int CountRuns(IEnumerable<int> values)
    {
        int[] sorted = values.Order().ToArray();
        if (sorted.Length == 0) return 0;
        int runs = 1;
        for (int index = 1; index < sorted.Length; index++)
            if (sorted[index] != sorted[index - 1] + 1) runs++;
        return runs;
    }

    private static bool InRange(int value, int offset, int length) =>
        value >= offset && value < offset + (long)length;

    private static int CountChangedBytes(
        ReadOnlySpan<byte> before,
        ReadOnlySpan<byte> after)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException(
                "Raw-sector changed-byte inputs differ in length.");
        int count = 0;
        for (int index = 0; index < before.Length; index++)
            if (before[index] != after[index]) count++;
        return count;
    }

    private static async Task<byte[]> ReadBoundedFileAsync(
        string path,
        int maximumByteCount,
        CancellationToken cancellationToken)
    {
        FileInfo info = new(path);
        if (info.Length <= 0 || info.Length > maximumByteCount)
            throw new InvalidDataException(
                "The schema-7 sibling receipt size is invalid.");
        return await File.ReadAllBytesAsync(path, cancellationToken);
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

    private static void RefuseReparsePoint(string path, string label)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException(
                $"The {label} must not be a symbolic link or reparse point.");
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    internal sealed record PatchPlanReadback(
        int LogicalChangedByteCount,
        int PhysicalChangedByteCount,
        string RawSectorPatchMapSha256,
        string PhysicalDiffSha256);

    private sealed record ParentCapture(
        string DirectoryPath,
        string ImagePath,
        string CuePath,
        string ReceiptPath,
        string ImageSha256,
        string CueSha256,
        string ReceiptFileSha256,
        Id65AuthoringSingleSectorSubstrateRuntimeCandidateReceipt Receipt,
        byte[] Row80);
}

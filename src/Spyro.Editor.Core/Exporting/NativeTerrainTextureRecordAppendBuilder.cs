using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Explicit structural-growth authorization for native terrain texture-table
/// expansion. NormalChecked retains the original +0x1000 writer boundary.
/// RuntimeProvenPlus2000 and RuntimeProvenExtended are reserved for exact
/// promotion profiles whose larger writers have separate gameplay evidence.
/// FiftyRowStaticResearch remains
/// deterministic research only and must never be interpreted as normal-release
/// or DuckStation evidence.
/// </summary>
public enum NativeTerrainTextureStructuralGrowthPolicy
{
    NormalChecked,
    RuntimeProvenExtended,
    FiftyRowStaticResearch,
    RuntimeProvenPlus2000
}

/// <summary>
/// Source binding persisted with a private terrain-texture record edit.  A
/// saved edit is rejected when it is replayed against a different disc, level,
/// texture table, or level-data preimage.
/// </summary>
public sealed record NativeTerrainTextureRecordAppendSourceBinding(
    int Version,
    string SourceImageSha256,
    string TargetLevelKey,
    int TargetWadEntry,
    int ExpectedSourceTextureCount,
    string ExpectedTextureComponentSha256,
    string ExpectedLevelDataSha256);

/// <summary>
/// One complete native texture record after a page allocator has assigned its
/// final target-page descriptors.  The append builder never edits descriptor
/// geometry or page storage; it only installs these exact 16-byte LQ and
/// 168-byte HQ rows into a structurally expanded table.
/// </summary>
public sealed record NativeTerrainTexturePackedAppendRecord(
    string StableEditId,
    string DonorLevelKey,
    int DonorWadEntry,
    int DonorTextureId,
    int MaterialTemplateTextureId,
    byte[] LowDetailRow,
    byte[] HighDetailRow,
    string ExpectedLowDetailSha256,
    string ExpectedHighDetailSha256);

/// <summary>
/// A source-bound patch already planned inside the destination level-data
/// subfile.  Texture-table growth consumes and rebases these patches so terrain
/// face, collision, material, and existing descriptor edits do not write to
/// their obsolete pre-growth offsets.
/// </summary>
public sealed record NativeTerrainTextureRecordExistingPatch(
    long WadOffset,
    byte[] Before,
    byte[] After,
    string Kind,
    string RuntimeKey);

public sealed record NativeTerrainTextureRecordAppendRequest(
    string SourceImagePath,
    LevelDefinition TargetLevel,
    NativeTerrainTextureRecordAppendSourceBinding SourceBinding,
    IReadOnlyList<NativeTerrainTexturePackedAppendRecord> Records,
    IReadOnlyList<NativeTerrainTextureRecordExistingPatch> ExistingLevelDataPatches,
    NativeTerrainTexturePrivateRecordStagingSourceProof? StagingSourceProof = null);

public sealed record NativeTerrainTextureRecordAppendResolvedRecord(
    string StableEditId,
    int AssignedTextureId,
    string DonorLevelKey,
    int DonorWadEntry,
    int DonorTextureId,
    int MaterialTemplateTextureId,
    string LowDetailSha256,
    string HighDetailSha256);

public sealed record NativeTerrainTextureRecordRebasedPatchProof(
    string Kind,
    string RuntimeKey,
    long SourceWadOffset,
    long OutputWadOffset,
    int ByteLength,
    string BeforeSha256,
    string AfterSha256);

public sealed record NativeTerrainTextureRecordShiftedComponentProof(
    string Name,
    int SourceOffset,
    int OutputOffset,
    int ByteLength,
    string ComposedSourceSha256,
    string OutputSha256);

public sealed record NativeTerrainTextureRecordAppendPatch(
    long WadOffset,
    int SourceByteLength,
    int OutputByteLength,
    byte[] Before,
    byte[] After,
    string BeforeSha256,
    string AfterSha256,
    string Kind,
    string Description)
{
    public bool IsFixedLength => SourceByteLength == OutputByteLength;
}

public sealed record NativeTerrainTextureRecordAppendPlan(
    string SourceImagePath,
    NativeTerrainTextureRecordAppendSourceBinding SourceBinding,
    int TargetWadEntry,
    long TargetEntryWadOffset,
    int TargetEntryByteLength,
    long LevelDataWadOffset,
    int LevelDataByteLength,
    int OutputLevelDataByteLength,
    int SourceTextureCount,
    int OutputTextureCount,
    int SourceTextureComponentByteLength,
    int OutputTextureComponentByteLength,
    string OutputTextureComponentSha256,
    int RecordCountAdded,
    int GrowthByteCount,
    int SourceUsedByteLength,
    int OutputUsedByteLength,
    int SourceZeroTailByteCount,
    int OutputZeroTailByteCount,
    string WadArchiveHeaderSha256,
    int WadArchiveHeaderByteLength,
    string TargetEntryHeaderSha256,
    int TargetEntryHeaderByteLength,
    bool SevenBitTextureIdsVerified,
    bool FixedSubfileBoundaryPreserved,
    bool ArchiveHeadersRequireNoFixup,
    bool RuntimeTargetsPersistent,
    bool ExistingRowsPreserved,
    bool AppendedRowsCopiedExactly,
    bool ShiftedComponentChainReparsed,
    bool ShiftedTerrainSurfaceSemanticsVerified,
    bool ZeroTailVerified,
    bool ExistingPatchesRebasedExactly,
    bool RequiresDuckStationRuntimeProof,
    IReadOnlyList<NativeTerrainTextureRecordAppendResolvedRecord> ResolvedRecords,
    IReadOnlyList<NativeTerrainTextureRecordRebasedPatchProof> RebasedPatches,
    IReadOnlyList<NativeTerrainTextureRecordShiftedComponentProof> ShiftedComponents,
    NativeTerrainTextureRecordAppendPatch Patch)
{
    public NativeTerrainTextureStructuralGrowthPolicy StructuralGrowthPolicy { get; init; } =
        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked;

    public bool StaticResearchOnly =>
        NativeTerrainTextureRecordAppendBuilder.IsStaticResearchOnly(StructuralGrowthPolicy);
}

public sealed record NativeTerrainTextureRecordAppendExportResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    bool SourcePreimageVerified,
    bool ArchiveHeadersPreserved,
    bool ExactLevelDataReadbackVerified,
    bool RuntimeTargetReadbackVerified);

/// <summary>
/// Reports both raw storage observations and executable writer support.
/// Every <c>Can*</c> member is an implemented-writer capability; theoretical
/// following-gap and later-nested-shift storage is exposed separately and must
/// not be interpreted as export support.
/// </summary>
public sealed record NativeTerrainTextureRecordAppendCapacity(
    string LevelKey,
    string LevelName,
    int TargetWadEntry,
    int SourceTextureCount,
    int SevenBitRecordCapacity,
    int LevelDataSubfileIndex,
    int LevelDataRelativeOffset,
    int LevelDataByteLength,
    int VerifiedLevelDataZeroTailBytes,
    int OneRecordGrowthBytes,
    int AdditionalBytesNeededForOneRecord,
    int? FollowingSubfileIndex,
    int FollowingSubfileRelativeOffset,
    int ImmediateFollowingGapBytes,
    int ImmediateFollowingZeroBytes,
    int SubsequentSubfileCount,
    int TargetEntryTrailingBytes,
    int TargetEntryTrailingZeroBytes,
    int OuterFollowingEntryGapBytes,
    int OuterFollowingEntryZeroBytes,
    int RequiredSectorAlignedEntryGrowthBytes,
    int? AvailableIsoGrowthBytes,
    bool SectorRelocationPlanVerified,
    string SectorRelocationFailure,
    bool CanAppendInsideCurrentSubfile,
    bool CanExtendSubfileWithoutMovingLaterSubfiles,
    bool CanShiftLaterSubfilesInsideCurrentEntry,
    bool WouldRequireTargetEntryGrowth,
    IReadOnlyList<string> Notes)
{
    /// <summary>
    /// Number of contiguous native rows evaluated by this capacity result.
    /// The legacy <see cref="InspectCapacity(string, LevelDefinition, string)"/>
    /// entry point always reports one.
    /// </summary>
    public int RequestedRecordCount { get; init; } = 1;

    /// <summary>Total 184-byte table growth for the requested row batch.</summary>
    public int RequestedRecordGrowthBytes { get; init; } = OneRecordGrowthBytes;

    /// <summary>
    /// Requested row bytes not already covered by the level-data subfile's
    /// verified zero tail.
    /// </summary>
    public int AdditionalBytesNeededForRequestedRecords { get; init; } =
        AdditionalBytesNeededForOneRecord;

    /// <summary>
    /// True when the archive contains enough verified immediate zero storage
    /// to describe a possible subfile extension. This is a storage observation,
    /// not an executable-writer capability.
    /// </summary>
    public bool ImmediateGapExtensionStorageCandidate { get; init; }

    /// <summary>
    /// True when the containing entry has enough verified trailing zero storage
    /// to describe a possible shift of later nested subfiles. This is a storage
    /// observation, not an executable-writer capability.
    /// </summary>
    public bool LaterNestedSubfileShiftStorageCandidate { get; init; }

    /// <summary>
    /// True only when an implemented writer has independently proven the exact
    /// requested capacity strategy.
    /// </summary>
    public bool HasExecutableWriter { get; init; }

    /// <summary>
    /// Implemented writer selected by this capacity result, or "None" when the
    /// result remains fail-closed.
    /// </summary>
    public string ExecutableWriter { get; init; } = "None";

    public NativeTerrainTextureStructuralGrowthPolicy StructuralGrowthPolicy { get; init; } =
        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked;

    public int MaximumAuthorizedSectorGrowthBytes =>
        NativeTerrainTextureRecordAppendBuilder.GetMaximumSectorGrowthBytes(
            StructuralGrowthPolicy);

    public bool StaticResearchOnly =>
        NativeTerrainTextureRecordAppendBuilder.IsStaticResearchOnly(
            StructuralGrowthPolicy);
}

/// <summary>
/// Structurally appends complete native terrain texture records inside a
/// level's fixed-size level-data subfile.  Growth consumes only a verified
/// all-zero tail; WAD and nested-subfile boundaries remain unchanged.  Because
/// the texture table precedes every later component, callers must supply every
/// already-planned patch inside this subfile so the builder can compose and
/// rebase it atomically.
///
/// This builder intentionally does not allocate texture-page storage.  A page
/// allocator must provide final packed rows and its own protected-storage and
/// logical-pixel proofs before this plan may be exported by normal Create BIN.
/// </summary>
public static class NativeTerrainTextureRecordAppendBuilder
{
    public const int CurrentBindingVersion = 1;
    public const int LowDetailRecordBytes = 16;
    public const int HighDetailRecordBytes = 168;
    public const int RecordGrowthBytes = LowDetailRecordBytes + HighDetailRecordBytes;
    public const int MaximumTextureRecordCount = 128;
    public const int MaximumCheckedSectorGrowthBytes = 0x1000;
    public const int MaximumRuntimeProvenPlus2000AppendedRecordCount = 46;
    public const int MaximumRuntimeProvenPlus2000SectorGrowthBytes = 0x2000;
    public const int MaximumRuntimeProvenExtendedAppendedRecordCount = 50;
    public const int MaximumRuntimeProvenExtendedSectorGrowthBytes = 0x2800;
    public const int MaximumStaticResearchAppendedRecordCount = 50;
    public const int MaximumStaticResearchFiftyRowSectorGrowthBytes = 0x2800;

    private const int WadLba = 37;
    private const int SectorBytes = 2048;
    private const int LevelDataSubfileIndex = 1;
    private const int MaximumArchiveHeaderBytes = 1 << 20;
    private const int MaximumPortalCount = 64;
    private const int MaximumPortalPointCount = 4096;

    public static NativeTerrainTextureRecordAppendCapacity InspectCapacity(
        string sourceImagePath,
        LevelDefinition targetLevel,
        string wadAnalysisPath = "") =>
        InspectCapacityForRecordCount(
            sourceImagePath,
            targetLevel,
            requestedRecordCount: 1,
            wadAnalysisPath: wadAnalysisPath);

    /// <summary>
    /// Evaluates one complete contiguous batch of appended native texture rows
    /// under the normal checked one- or two-sector writer policy.
    /// </summary>
    public static NativeTerrainTextureRecordAppendCapacity InspectCapacityForRecordCount(
        string sourceImagePath,
        LevelDefinition targetLevel,
        int requestedRecordCount,
        string wadAnalysisPath = "") =>
        InspectCapacityForRecordCount(
            sourceImagePath,
            targetLevel,
            requestedRecordCount,
            NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
            wadAnalysisPath);

    /// <summary>
    /// Evaluates a contiguous appended-record batch under an explicit structural
    /// growth policy. Extended growth is static-research-only and remains
    /// runtime-unproven even when every structural/readback check succeeds.
    /// </summary>
    public static NativeTerrainTextureRecordAppendCapacity InspectCapacityForRecordCount(
        string sourceImagePath,
        LevelDefinition targetLevel,
        int requestedRecordCount,
        NativeTerrainTextureStructuralGrowthPolicy structuralGrowthPolicy,
        string wadAnalysisPath = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        ArgumentNullException.ThrowIfNull(targetLevel);
        _ = GetMaximumSectorGrowthBytes(structuralGrowthPolicy);
        if (requestedRecordCount <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(requestedRecordCount),
                "Requested native texture-record capacity must be positive.");
        if (!IsRequestedRecordCountAuthorized(
                requestedRecordCount,
                structuralGrowthPolicy))
        {
            throw new InvalidOperationException(
                $"Structural growth policy {structuralGrowthPolicy} permits at most " +
                $"{GetMaximumRequestedRecordCount(structuralGrowthPolicy)} appended " +
                $"native texture record(s); this request requires {requestedRecordCount}.");
        }
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing retail source image.", sourceImagePath);

        DiscLayout discLayout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream image = File.OpenRead(sourceImagePath);
        LevelDataAsset asset = LoadLevelDataAsset(image, discLayout, targetLevel.SourceWadEntry);
        byte[] levelData = DiscImage.ReadFileBytes(
            image,
            discLayout,
            WadLba,
            asset.LevelDataWadOffset,
            asset.LevelDataByteLength);
        if (!TryParseLayout(levelData, out LevelDataLayout? layout, out string reason) || layout == null)
            throw new InvalidDataException($"{targetLevel.DisplayName}'s level-data layout is not proven: {reason}");
        if (requestedRecordCount > MaximumTextureRecordCount - layout.TextureCount)
        {
            throw new InvalidOperationException(
                $"Appending {requestedRecordCount} record(s) to {layout.TextureCount} would require texture id {layout.TextureCount + requestedRecordCount - 1}, beyond the native seven-bit maximum T127.");
        }

        byte[] entryHeader = DiscImage.ReadFileBytes(
            image,
            discLayout,
            WadLba,
            asset.TargetEntryWadOffset,
            asset.TargetEntryHeaderByteLength);
        ArchiveEntrySpan[] nested = ParseArchiveEntrySpans(entryHeader, asset.TargetEntryByteLength);
        ArchiveEntrySpan levelDataSpan = nested.Single(entry => entry.Index == LevelDataSubfileIndex);
        ArchiveEntrySpan? following = nested
            .Where(entry => entry.Offset >= levelDataSpan.EndExclusive && entry.Index != LevelDataSubfileIndex)
            .OrderBy(entry => entry.Offset)
            .FirstOrDefault();
        int followingBoundary = following?.Offset ?? asset.TargetEntryByteLength;
        int immediateGap = Math.Max(0, followingBoundary - levelDataSpan.EndExclusive);
        byte[] immediateGapBytes = immediateGap == 0
            ? Array.Empty<byte>()
            : DiscImage.ReadFileBytes(
                image,
                discLayout,
                WadLba,
                asset.TargetEntryWadOffset + levelDataSpan.EndExclusive,
                immediateGap);
        int immediateZero = CountLeadingZeroBytes(immediateGapBytes);

        int lastNestedEnd = nested.Max(entry => entry.EndExclusive);
        int trailingBytes = Math.Max(0, asset.TargetEntryByteLength - lastNestedEnd);
        byte[] trailing = trailingBytes == 0
            ? Array.Empty<byte>()
            : DiscImage.ReadFileBytes(
                image,
                discLayout,
                WadLba,
                asset.TargetEntryWadOffset + lastNestedEnd,
                trailingBytes);
        int trailingZero = CountLeadingZeroBytes(trailing);

        byte[] wadHeader = DiscImage.ReadFileBytes(
            image,
            discLayout,
            WadLba,
            0,
            asset.WadArchiveHeaderByteLength);
        ArchiveEntrySpan[] wadEntries = ParseArchiveEntrySpans(wadHeader, int.MaxValue);
        ArchiveEntrySpan targetEntry = wadEntries.Single(entry => entry.Index == targetLevel.SourceWadEntry);
        ArchiveEntrySpan? nextWadEntry = wadEntries
            .Where(entry => entry.Offset >= targetEntry.EndExclusive && entry.Index != targetEntry.Index)
            .OrderBy(entry => entry.Offset)
            .FirstOrDefault();
        int outerGap = nextWadEntry == null ? 0 : Math.Max(0, nextWadEntry.Offset - targetEntry.EndExclusive);
        byte[] outerGapBytes = outerGap == 0
            ? Array.Empty<byte>()
            : DiscImage.ReadFileBytes(
                image,
                discLayout,
                WadLba,
                targetEntry.EndExclusive,
                outerGap);
        int outerZero = CountLeadingZeroBytes(outerGapBytes);

        int requestedGrowth = checked(requestedRecordCount * RecordGrowthBytes);
        int additionalNeededForOneRecord = Math.Max(0, RecordGrowthBytes - layout.ZeroTailByteCount);
        int additionalNeeded = Math.Max(0, requestedGrowth - layout.ZeroTailByteCount);
        int subsequentCount = nested.Count(entry => entry.Offset >= levelDataSpan.EndExclusive && entry.Index != LevelDataSubfileIndex);
        bool inside = additionalNeeded == 0;
        bool immediateGapStorageCandidate =
            additionalNeeded > 0 && immediateZero >= additionalNeeded;
        bool laterNestedShiftStorageCandidate =
            additionalNeeded > 0 &&
            subsequentCount > 0 &&
            trailingZero >= additionalNeeded;
        // These two layouts are useful capacity observations, but no exporter
        // implements their required nested-length/offset mutations. Preserve
        // the existing fail-closed routing until a writer proves one.
        const bool immediateGapExtensionWriterSupported = false;
        const bool laterNestedShiftWriterSupported = false;
        bool needsEntryGrowth =
            additionalNeeded > 0 &&
            !immediateGapStorageCandidate &&
            !laterNestedShiftStorageCandidate;
        int requiredSectorGrowth = needsEntryGrowth ? AlignSector(additionalNeeded) : 0;
        int? availableIsoGrowth = null;
        bool sectorRelocationVerified = false;
        string sectorRelocationFailure = "";
        int maximumAuthorizedSectorGrowthBytes =
            GetMaximumSectorGrowthBytes(structuralGrowthPolicy);
        if (needsEntryGrowth &&
            requiredSectorGrowth <= maximumAuthorizedSectorGrowthBytes &&
            !string.IsNullOrWhiteSpace(wadAnalysisPath) &&
            File.Exists(wadAnalysisPath))
        {
            try
            {
                byte[] expandedPayload = new byte[requiredSectorGrowth];
                NativeSkyRelocationPayload relocationPayload = new(
                    PatchIndex: 0,
                    WadLba: WadLba,
                    OriginalWadOffset: asset.LevelDataWadOffset + levelData.Length,
                    StorageWadEntry: targetLevel.SourceWadEntry,
                    ModelBlockOffset: levelData.Length,
                    OriginalLength: 0,
                    Bytes: expandedPayload,
                    SubfileIndex: LevelDataSubfileIndex,
                    RequireLengthPrefix: false);
                NativeSkyWadRelocationPlan relocation = NativeSkyWadRelocator.BuildPlan(
                    sourceImagePath,
                    wadAnalysisPath,
                    [relocationPayload]);
                availableIsoGrowth = relocation.AvailableGrowthBytes;
                sectorRelocationVerified = relocation.Required &&
                    relocation.WadGrowthBytes == requiredSectorGrowth &&
                    relocation.EntryGrowthBytes.GetValueOrDefault(targetLevel.SourceWadEntry) == requiredSectorGrowth &&
                    relocation.RelocatedExecutableLba == relocation.OriginalExecutableLba + (requiredSectorGrowth / SectorBytes);
                if (!sectorRelocationVerified)
                    sectorRelocationFailure = "The existing WAD relocator returned a structurally different growth or executable shift.";
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
            {
                sectorRelocationFailure = ex.Message;
            }
        }
        else if (needsEntryGrowth &&
                 requiredSectorGrowth > maximumAuthorizedSectorGrowthBytes)
        {
            sectorRelocationFailure =
                $"The requested {requestedRecordCount}-row batch needs +0x{requiredSectorGrowth:X} aligned level-data/WAD growth, beyond the currently checked +0x{maximumAuthorizedSectorGrowthBytes:X} structural limit for {structuralGrowthPolicy}.";
        }
        List<string> notes =
        [
            $"The requested {requestedRecordCount}-row batch adds {requestedGrowth} texture-table byte(s); it changes only the texture component size/count and shifts every later component inside level data.",
            IsStaticResearchOnly(structuralGrowthPolicy)
                ? $"Structural growth policy {structuralGrowthPolicy} permits aligned growth through +0x{maximumAuthorizedSectorGrowthBytes:X} for static research only; it supplies no runtime or normal-release authorization."
                : $"Structural growth policy {structuralGrowthPolicy} permits aligned growth through its checked +0x{maximumAuthorizedSectorGrowthBytes:X} ceiling; normal release still requires an exact runtime-proven destination profile.",
            immediateGapStorageCandidate
                ? $"Storage observation only: the level-data subfile has {additionalNeeded} verified zero byte(s) available before nested subfile {following?.Index}, but following-gap extension is UNSUPPORTED by every executable writer. CanExtendSubfileWithoutMovingLaterSubfiles remains false."
                : "There is not enough verified immediate zero gap for a candidate subfile extension; following-gap extension is also unsupported by every executable writer.",
            laterNestedShiftStorageCandidate
                ? $"Storage observation only: the current WAD entry has {trailingZero} leading verified zero trailing byte(s), enough to describe shifting {subsequentCount} later nested subfile(s) by {additionalNeeded}, but later-nested-subfile shifting is UNSUPPORTED by every executable writer. CanShiftLaterSubfilesInsideCurrentEntry remains false."
                : "The current WAD entry does not have enough verified trailing zero storage for a candidate later-subfile shift; that strategy is also unsupported by every executable writer.",
            needsEntryGrowth && outerZero >= additionalNeeded
                ? $"The target WAD entry could grow by {additionalNeeded} byte(s) into verified zero space before the next WAD entry, but that path additionally changes the top-level target-entry length and still requires shifting later nested subfiles."
                : "The packed WAD has no immediate outer-entry gap; preserving native alignment requires one sector of archive growth and relocation of subsequent WAD/ISO data.",
            sectorRelocationVerified
                ? $"NativeSkyWadRelocator independently planned exact +0x{requiredSectorGrowth:X} target-entry/WAD growth, a {requiredSectorGrowth / SectorBytes}-sector executable relocation, and reported {availableIsoGrowth:N0} bytes of safe ISO headroom."
                : string.IsNullOrWhiteSpace(sectorRelocationFailure)
                    ? "No sector-relocation plan was requested for this capacity inspection."
                    : $"The existing sector relocator did not prove this growth: {sectorRelocationFailure}",
            inside
                ? "Executable writer proven: FixedTail consumes only the verified zero tail inside the existing level-data subfile."
                : needsEntryGrowth && sectorRelocationVerified
                    ? $"Executable writer proven: SectorRelocation supplies exact +0x{requiredSectorGrowth:X} aligned growth."
                    : "No executable writer is proven for this capacity result; it remains fail-closed."
        ];
        return new NativeTerrainTextureRecordAppendCapacity(
            LevelCatalog.NormalizeKey(targetLevel.Key),
            targetLevel.DisplayName,
            targetLevel.SourceWadEntry,
            layout.TextureCount,
            MaximumTextureRecordCount - layout.TextureCount,
            LevelDataSubfileIndex,
            checked((int)(asset.LevelDataWadOffset - asset.TargetEntryWadOffset)),
            asset.LevelDataByteLength,
            layout.ZeroTailByteCount,
            RecordGrowthBytes,
            additionalNeededForOneRecord,
            following?.Index,
            following?.Offset ?? -1,
            immediateGap,
            immediateZero,
            subsequentCount,
            trailingBytes,
            trailingZero,
            outerGap,
            outerZero,
            requiredSectorGrowth,
            availableIsoGrowth,
            sectorRelocationVerified,
            sectorRelocationFailure,
            inside,
            immediateGapExtensionWriterSupported,
            laterNestedShiftWriterSupported,
            needsEntryGrowth,
            notes)
        {
            RequestedRecordCount = requestedRecordCount,
            RequestedRecordGrowthBytes = requestedGrowth,
            AdditionalBytesNeededForRequestedRecords = additionalNeeded,
            ImmediateGapExtensionStorageCandidate =
                immediateGapStorageCandidate,
            LaterNestedSubfileShiftStorageCandidate =
                laterNestedShiftStorageCandidate,
            HasExecutableWriter =
                inside || needsEntryGrowth && sectorRelocationVerified,
            ExecutableWriter = inside
                ? "FixedTail"
                : needsEntryGrowth && sectorRelocationVerified
                    ? "SectorRelocation"
                    : "None",
            StructuralGrowthPolicy = structuralGrowthPolicy
        };
    }

    public static int GetMaximumSectorGrowthBytes(
        NativeTerrainTextureStructuralGrowthPolicy policy) =>
        policy switch
        {
            NativeTerrainTextureStructuralGrowthPolicy.NormalChecked =>
                MaximumCheckedSectorGrowthBytes,
            NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenPlus2000 =>
                MaximumRuntimeProvenPlus2000SectorGrowthBytes,
            NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenExtended =>
                MaximumRuntimeProvenExtendedSectorGrowthBytes,
            NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch =>
                MaximumStaticResearchFiftyRowSectorGrowthBytes,
            _ => throw new ArgumentOutOfRangeException(
                nameof(policy),
                policy,
                "Unknown native terrain texture structural-growth policy.")
        };

    public static int GetMaximumRequestedRecordCount(
        NativeTerrainTextureStructuralGrowthPolicy policy) =>
        policy switch
        {
            NativeTerrainTextureStructuralGrowthPolicy.NormalChecked =>
                MaximumTextureRecordCount,
            NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenPlus2000 =>
                MaximumRuntimeProvenPlus2000AppendedRecordCount,
            NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenExtended =>
                MaximumRuntimeProvenExtendedAppendedRecordCount,
            NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch =>
                MaximumStaticResearchAppendedRecordCount,
            _ => throw new ArgumentOutOfRangeException(
                nameof(policy),
                policy,
                "Unknown native terrain texture structural-growth policy.")
        };

    public static bool IsRequestedRecordCountAuthorized(
        int requestedRecordCount,
        NativeTerrainTextureStructuralGrowthPolicy policy) =>
        requestedRecordCount > 0 &&
        requestedRecordCount <= GetMaximumRequestedRecordCount(policy);

    public static bool IsStaticResearchOnly(
        NativeTerrainTextureStructuralGrowthPolicy policy) =>
        policy switch
        {
            NativeTerrainTextureStructuralGrowthPolicy.NormalChecked => false,
            NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenPlus2000 => false,
            NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenExtended => false,
            NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch => true,
            _ => throw new ArgumentOutOfRangeException(
                nameof(policy),
                policy,
                "Unknown native terrain texture structural-growth policy.")
        };

    public static bool IsSectorGrowthAuthorized(
        int growthBytes,
        NativeTerrainTextureStructuralGrowthPolicy policy) =>
        growthBytes > 0 &&
        growthBytes % SectorBytes == 0 &&
        growthBytes <= GetMaximumSectorGrowthBytes(policy);

    public static NativeTerrainTextureRecordAppendSourceBinding InspectSourceBinding(
        string sourceImagePath,
        LevelDefinition targetLevel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        ArgumentNullException.ThrowIfNull(targetLevel);
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing retail source image.", sourceImagePath);

        DiscLayout discLayout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream image = File.OpenRead(sourceImagePath);
        LevelDataAsset asset = LoadLevelDataAsset(image, discLayout, targetLevel.SourceWadEntry);
        byte[] levelData = DiscImage.ReadFileBytes(
            image,
            discLayout,
            WadLba,
            asset.LevelDataWadOffset,
            asset.LevelDataByteLength);
        if (!TryParseLayout(levelData, out LevelDataLayout? layout, out string reason) || layout == null)
            throw new InvalidDataException($"{targetLevel.DisplayName}'s level-data layout is not proven: {reason}");

        return new NativeTerrainTextureRecordAppendSourceBinding(
            CurrentBindingVersion,
            Sha256File(sourceImagePath),
            LevelCatalog.NormalizeKey(targetLevel.Key),
            targetLevel.SourceWadEntry,
            layout.TextureCount,
            Sha256(levelData.AsSpan(0, layout.TextureComponentByteLength)),
            Sha256(levelData));
    }

    public static bool TryBuild(
        NativeTerrainTextureRecordAppendRequest request,
        out NativeTerrainTextureRecordAppendPlan? plan,
        out string failureReason) =>
        TryBuildWithOutputCapacityExtension(request, 0, out plan, out failureReason);

    internal static bool TryBuildWithOutputCapacityExtension(
        NativeTerrainTextureRecordAppendRequest request,
        int outputCapacityExtensionBytes,
        out NativeTerrainTextureRecordAppendPlan? plan,
        out string failureReason)
    {
        plan = null;
        failureReason = "";
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.TargetLevel);
            ArgumentNullException.ThrowIfNull(request.SourceBinding);
            ArgumentNullException.ThrowIfNull(request.Records);
            ArgumentNullException.ThrowIfNull(request.ExistingLevelDataPatches);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceImagePath);
            if (!File.Exists(request.SourceImagePath))
                throw new FileNotFoundException("Missing retail source image.", request.SourceImagePath);
            if (request.TargetLevel.SourceWadEntry < 0)
                throw new InvalidOperationException("The destination level needs a mapped native WAD entry.");
            if (request.Records.Count == 0)
                throw new InvalidOperationException("At least one packed native terrain texture record is required.");
            if (outputCapacityExtensionBytes < 0 ||
                (outputCapacityExtensionBytes != 0 && outputCapacityExtensionBytes % SectorBytes != 0))
            {
                throw new InvalidOperationException("Expanded level-data capacity must be zero or a positive whole-sector byte count.");
            }

            DiscLayout discLayout = DiscImage.DetectLayout(request.SourceImagePath);
            using FileStream image = File.OpenRead(request.SourceImagePath);
            LevelDataAsset asset = LoadLevelDataAsset(
                image,
                discLayout,
                request.TargetLevel.SourceWadEntry);
            byte[] sourceLevelData = DiscImage.ReadFileBytes(
                image,
                discLayout,
                WadLba,
                asset.LevelDataWadOffset,
                asset.LevelDataByteLength);
            if (!TryParseLayout(sourceLevelData, out LevelDataLayout? sourceLayout, out failureReason) || sourceLayout == null)
                return false;
            if (!ValidateBinding(request, sourceLayout, sourceLevelData, out failureReason))
                return false;
            if (!TryValidateRecords(request.Records, sourceLayout.TextureCount, out failureReason))
                return false;

            int outputTextureCount = checked(sourceLayout.TextureCount + request.Records.Count);
            if (outputTextureCount > MaximumTextureRecordCount)
            {
                failureReason =
                    $"Appending {request.Records.Count} record(s) to {sourceLayout.TextureCount} would require texture id {outputTextureCount - 1}, beyond the native seven-bit maximum T127.";
                return false;
            }
            int growth = checked(request.Records.Count * RecordGrowthBytes);
            int outputLevelDataByteLength = checked(sourceLevelData.Length + outputCapacityExtensionBytes);
            int availableZeroTailBytes = checked(sourceLayout.ZeroTailByteCount + outputCapacityExtensionBytes);
            if (availableZeroTailBytes < growth)
            {
                failureReason =
                    $"The prepared level-data capacity has only {availableZeroTailBytes} verified zero-tail bytes; {request.Records.Count} native record(s) need {growth}.";
                return false;
            }

            NativeTerrainTextureRuntimeControlAudit runtimeAudit =
                NativeTerrainTextureRuntimeControlScanner.Inspect(request.SourceImagePath, request.TargetLevel);
            if (!runtimeAudit.Complete || runtimeAudit.TextureCount != sourceLayout.TextureCount)
            {
                failureReason = runtimeAudit.SafetyBlockers.FirstOrDefault()
                    ?? "The destination runtime texture-control audit does not match the bound texture table.";
                return false;
            }
            int[] invalidRuntimeControlTargets = runtimeAudit.Controls
                .Where(control => control.TextureId >= sourceLayout.TextureCount)
                .Select(control => control.TextureId)
                .Distinct()
                .Order()
                .ToArray();
            if (invalidRuntimeControlTargets.Length > 0)
            {
                failureReason =
                    $"Native runtime controls unexpectedly address future appended texture id(s) {string.Join(", ", invalidRuntimeControlTargets)}.";
                return false;
            }

            byte[] composedSource = new byte[outputLevelDataByteLength];
            sourceLevelData.CopyTo(composedSource, 0);
            if (!TryComposeExistingPatches(
                    request.ExistingLevelDataPatches,
                    asset.LevelDataWadOffset,
                    sourceLayout,
                    sourceLevelData,
                    composedSource,
                    request.Records.Count,
                    out NativeTerrainTextureRecordRebasedPatchProof[]? rebasedPatches,
                    out failureReason) ||
                rebasedPatches == null)
            {
                return false;
            }

            int sourceLowStart = 8;
            int sourceHighStart = checked(sourceLowStart + (sourceLayout.TextureCount * LowDetailRecordBytes));
            int sourceTextureEnd = sourceLayout.TextureComponentByteLength;
            int outputHighStart = checked(sourceHighStart + (request.Records.Count * LowDetailRecordBytes));
            int outputTextureEnd = checked(sourceTextureEnd + growth);
            byte[] output = new byte[outputLevelDataByteLength];
            BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(0, 4), outputTextureEnd);
            BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(4, 4), outputTextureCount);

            composedSource.AsSpan(sourceLowStart, sourceHighStart - sourceLowStart)
                .CopyTo(output.AsSpan(sourceLowStart));
            for (int index = 0; index < request.Records.Count; index++)
            {
                request.Records[index].LowDetailRow
                    .AsSpan()
                    .CopyTo(output.AsSpan(sourceHighStart + (index * LowDetailRecordBytes)));
            }
            composedSource.AsSpan(sourceHighStart, sourceTextureEnd - sourceHighStart)
                .CopyTo(output.AsSpan(outputHighStart));
            int appendedHighStart = checked(outputHighStart + (sourceLayout.TextureCount * HighDetailRecordBytes));
            for (int index = 0; index < request.Records.Count; index++)
            {
                request.Records[index].HighDetailRow
                    .AsSpan()
                    .CopyTo(output.AsSpan(appendedHighStart + (index * HighDetailRecordBytes)));
            }
            composedSource.AsSpan(sourceTextureEnd, sourceLayout.UsedByteLength - sourceTextureEnd)
                .CopyTo(output.AsSpan(outputTextureEnd));

            if (!TryParseLayout(output, out LevelDataLayout? outputLayout, out failureReason) || outputLayout == null)
            {
                failureReason = $"Expanded level-data component chain did not reparse: {failureReason}";
                return false;
            }
            if (outputLayout.TextureCount != outputTextureCount ||
                outputLayout.TextureComponentByteLength != outputTextureEnd ||
                outputLayout.UsedByteLength != sourceLayout.UsedByteLength + growth)
            {
                failureReason = "Expanded texture count, component size, or used level-data length did not advance by the exact requested record count.";
                return false;
            }

            bool existingRowsPreserved =
                composedSource.AsSpan(sourceLowStart, sourceHighStart - sourceLowStart)
                    .SequenceEqual(output.AsSpan(sourceLowStart, sourceHighStart - sourceLowStart)) &&
                composedSource.AsSpan(sourceHighStart, sourceTextureEnd - sourceHighStart)
                    .SequenceEqual(output.AsSpan(outputHighStart, sourceTextureEnd - sourceHighStart));
            bool appendedRowsCopied = true;
            for (int index = 0; index < request.Records.Count; index++)
            {
                appendedRowsCopied &= output
                    .AsSpan(sourceHighStart + (index * LowDetailRecordBytes), LowDetailRecordBytes)
                    .SequenceEqual(request.Records[index].LowDetailRow);
                appendedRowsCopied &= output
                    .AsSpan(appendedHighStart + (index * HighDetailRecordBytes), HighDetailRecordBytes)
                    .SequenceEqual(request.Records[index].HighDetailRow);
            }
            bool zeroTailVerified =
                output.AsSpan(outputLayout.UsedByteLength).IndexOfAnyExcept((byte)0) < 0;
            if (!existingRowsPreserved || !appendedRowsCopied || !zeroTailVerified)
            {
                failureReason = "An existing-row, appended-row, or zero-tail structural invariant failed.";
                return false;
            }

            if (sourceLayout.Components.Count != outputLayout.Components.Count)
            {
                failureReason = "The expanded level-data suffix no longer has the same component count.";
                return false;
            }
            List<NativeTerrainTextureRecordShiftedComponentProof> componentProofs = [];
            for (int index = 0; index < sourceLayout.Components.Count; index++)
            {
                LevelDataComponent before = sourceLayout.Components[index];
                LevelDataComponent after = outputLayout.Components[index];
                ReadOnlySpan<byte> expected = composedSource.AsSpan(before.Offset, before.ByteLength);
                ReadOnlySpan<byte> actual = output.AsSpan(after.Offset, after.ByteLength);
                if (!string.Equals(before.Name, after.Name, StringComparison.Ordinal) ||
                    before.ByteLength != after.ByteLength ||
                    after.Offset != before.Offset + growth ||
                    !expected.SequenceEqual(actual))
                {
                    failureReason =
                        $"Shifted suffix component '{before.Name}' was not preserved at its exact old offset +{growth}.";
                    return false;
                }
                componentProofs.Add(new NativeTerrainTextureRecordShiftedComponentProof(
                    before.Name,
                    before.Offset,
                    after.Offset,
                    before.ByteLength,
                    Sha256(expected),
                    Sha256(actual)));
            }

            if (!TryVerifyTerrainSurfaceSemantics(
                    composedSource,
                    output,
                    asset.LevelDataWadOffset,
                    request.TargetLevel,
                    growth,
                    out failureReason))
            {
                return false;
            }
            if (!TryVerifyRebasedPatchResults(
                    request.ExistingLevelDataPatches,
                    rebasedPatches,
                    output,
                    asset.LevelDataWadOffset,
                    out failureReason))
            {
                return false;
            }

            NativeTerrainTextureRecordAppendResolvedRecord[] resolvedRecords = request.Records
                .Select((record, index) => new NativeTerrainTextureRecordAppendResolvedRecord(
                    record.StableEditId,
                    sourceLayout.TextureCount + index,
                    LevelCatalog.NormalizeKey(record.DonorLevelKey),
                    record.DonorWadEntry,
                    record.DonorTextureId,
                    record.MaterialTemplateTextureId,
                    Sha256(record.LowDetailRow),
                    Sha256(record.HighDetailRow)))
                .ToArray();

            NativeTerrainTextureRecordAppendPatch patch = new(
                asset.LevelDataWadOffset,
                sourceLevelData.Length,
                output.Length,
                sourceLevelData.ToArray(),
                output,
                Sha256(sourceLevelData),
                Sha256(output),
                "native-terrain-texture-record-append",
                $"Append {request.Records.Count} complete terrain texture record(s), rebase {rebasedPatches.Length} existing level-data patch(es), and prepare {output.Length - sourceLevelData.Length} byte(s) of structural subfile-capacity growth.");
            plan = new NativeTerrainTextureRecordAppendPlan(
                Path.GetFullPath(request.SourceImagePath),
                request.SourceBinding,
                request.TargetLevel.SourceWadEntry,
                asset.TargetEntryWadOffset,
                asset.TargetEntryByteLength,
                asset.LevelDataWadOffset,
                asset.LevelDataByteLength,
                output.Length,
                sourceLayout.TextureCount,
                outputLayout.TextureCount,
                sourceLayout.TextureComponentByteLength,
                outputLayout.TextureComponentByteLength,
                Sha256(output.AsSpan(0, outputLayout.TextureComponentByteLength)),
                request.Records.Count,
                growth,
                sourceLayout.UsedByteLength,
                outputLayout.UsedByteLength,
                sourceLayout.ZeroTailByteCount,
                outputLayout.ZeroTailByteCount,
                asset.WadArchiveHeaderSha256,
                asset.WadArchiveHeaderByteLength,
                asset.TargetEntryHeaderSha256,
                asset.TargetEntryHeaderByteLength,
                true,
                sourceLevelData.Length == output.Length,
                sourceLevelData.Length == output.Length,
                true,
                existingRowsPreserved,
                appendedRowsCopied,
                true,
                true,
                zeroTailVerified,
                true,
                true,
                resolvedRecords,
                rebasedPatches,
                componentProofs,
                patch);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
        {
            failureReason = ex.Message;
            return false;
        }
    }

    public static async Task<NativeTerrainTextureRecordAppendExportResult> ExportAsync(
        NativeTerrainTextureRecordAppendPlan plan,
        string sourceCuePath,
        string outputPrefix,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPrefix);
        string sourceImagePath = plan.SourceImagePath;
        if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
            throw new FileNotFoundException("The append plan's bound source BIN is unavailable.", sourceImagePath);
        if (!plan.Patch.IsFixedLength)
            throw new InvalidOperationException("An expanded level-data append plan must be written through the sector-relocation composer.");

        ValidatePlanSourcePreimage(plan, sourceImagePath);
        string outputImagePath = Path.GetFullPath(outputPrefix + ".bin");
        string outputCuePath = Path.GetFullPath(outputPrefix + ".cue");
        if (string.Equals(outputImagePath, Path.GetFullPath(sourceImagePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The append output BIN must not overwrite its bound source BIN.");
        string outputDirectory = Path.GetDirectoryName(outputImagePath) ?? ".";
        Directory.CreateDirectory(outputDirectory);
        string temporaryImagePath = outputImagePath + $".{Guid.NewGuid():N}.tmp";
        string temporaryCuePath = outputCuePath + $".{Guid.NewGuid():N}.tmp";
        DeleteStale(outputImagePath);
        DeleteStale(outputCuePath);
        DeleteStale(temporaryImagePath);
        DeleteStale(temporaryCuePath);

        try
        {
            File.Copy(sourceImagePath, temporaryImagePath, true);
            DiscLayout layout = DiscImage.DetectLayout(temporaryImagePath);
            await using (FileStream output = File.Open(
                             temporaryImagePath,
                             FileMode.Open,
                             FileAccess.ReadWrite,
                             FileShare.Read))
            {
                ValidateArchiveHeaders(output, layout, plan);
                byte[] before = DiscImage.ReadFileBytes(
                    output,
                    layout,
                    WadLba,
                    plan.Patch.WadOffset,
                    plan.Patch.SourceByteLength);
                if (!before.SequenceEqual(plan.Patch.Before) ||
                    !HashEquals(Sha256(before), plan.Patch.BeforeSha256))
                {
                    throw new InvalidDataException("The temporary BIN no longer matches the append plan's complete level-data preimage.");
                }
                DiscImage.WriteFileBytes(output, layout, WadLba, plan.Patch.WadOffset, plan.Patch.After);
                output.Flush(flushToDisk: true);
                byte[] readback = DiscImage.ReadFileBytes(
                    output,
                    layout,
                    WadLba,
                    plan.Patch.WadOffset,
                    plan.Patch.OutputByteLength);
                if (!readback.SequenceEqual(plan.Patch.After) ||
                    !HashEquals(Sha256(readback), plan.Patch.AfterSha256))
                {
                    throw new InvalidDataException("Expanded level-data final temporary-BIN readback failed.");
                }
                ValidateArchiveHeaders(output, layout, plan);
            }

            NativeTerrainTextureRuntimeControlAudit runtime =
                NativeTerrainTextureRuntimeControlScanner.Inspect(temporaryImagePath, new LevelDefinition
                {
                    Key = plan.SourceBinding.TargetLevelKey,
                    DisplayName = plan.SourceBinding.TargetLevelKey,
                    SourceWadEntry = plan.TargetWadEntry
                });
            int[] appendedIds = plan.ResolvedRecords.Select(record => record.AssignedTextureId).ToArray();
            if (!runtime.Complete ||
                runtime.TextureCount != plan.OutputTextureCount ||
                appendedIds.Any(textureId => !runtime.IsRuntimePersistentTarget(textureId)))
            {
                throw new InvalidDataException("The appended texture records failed final runtime-control readback.");
            }

            string cueText = DiscImage.BuildCueText(sourceCuePath, Path.GetFileName(outputImagePath));
            await File.WriteAllTextAsync(temporaryCuePath, cueText, Encoding.ASCII, cancellationToken);
            File.Move(temporaryImagePath, outputImagePath, overwrite: true);
            File.Move(temporaryCuePath, outputCuePath, overwrite: true);

            DiscLayout finalLayout = DiscImage.DetectLayout(outputImagePath);
            using (FileStream finalImage = File.OpenRead(outputImagePath))
            {
                ValidateArchiveHeaders(finalImage, finalLayout, plan);
                byte[] finalReadback = DiscImage.ReadFileBytes(
                    finalImage,
                    finalLayout,
                    WadLba,
                    plan.Patch.WadOffset,
                    plan.Patch.OutputByteLength);
                if (!finalReadback.SequenceEqual(plan.Patch.After))
                    throw new InvalidDataException("Final renamed BIN no longer matches the append plan.");
            }

            return new NativeTerrainTextureRecordAppendExportResult(
                outputImagePath,
                outputCuePath,
                Sha256File(outputImagePath),
                true,
                true,
                true,
                true);
        }
        catch
        {
            DeleteStale(outputImagePath);
            DeleteStale(outputCuePath);
            throw;
        }
        finally
        {
            DeleteStale(temporaryImagePath);
            DeleteStale(temporaryCuePath);
        }
    }

    private static bool ValidateBinding(
        NativeTerrainTextureRecordAppendRequest request,
        LevelDataLayout sourceLayout,
        byte[] sourceLevelData,
        out string failureReason)
    {
        failureReason = "";
        NativeTerrainTextureRecordAppendSourceBinding binding = request.SourceBinding;
        if (binding.Version != CurrentBindingVersion)
        {
            failureReason = $"Unsupported private-texture source-binding version {binding.Version}.";
            return false;
        }
        string targetKey = LevelCatalog.NormalizeKey(request.TargetLevel.Key);
        if (!string.Equals(LevelCatalog.NormalizeKey(binding.TargetLevelKey), targetKey, StringComparison.OrdinalIgnoreCase) ||
            binding.TargetWadEntry != request.TargetLevel.SourceWadEntry)
        {
            failureReason = "The private-texture edit identity belongs to a different destination level or WAD entry.";
            return false;
        }
        string imageSha;
        if (request.StagingSourceProof == null)
        {
            imageSha = Sha256File(request.SourceImagePath);
        }
        else
        {
            if (!request.StagingSourceProof.TryValidate(
                    request.SourceImagePath,
                    request.TargetLevel,
                    binding,
                    out failureReason))
            {
                return false;
            }
            imageSha = binding.SourceImageSha256;
        }
        if (!HashEquals(binding.SourceImageSha256, imageSha) ||
            binding.ExpectedSourceTextureCount != sourceLayout.TextureCount ||
            !HashEquals(binding.ExpectedTextureComponentSha256, Sha256(sourceLevelData.AsSpan(0, sourceLayout.TextureComponentByteLength))) ||
            !HashEquals(binding.ExpectedLevelDataSha256, Sha256(sourceLevelData)))
        {
            failureReason = "The source disc, texture-table count, texture component, or level-data preimage no longer matches the saved private-texture edit binding.";
            return false;
        }
        return true;
    }

    private static bool TryValidateRecords(
        IReadOnlyList<NativeTerrainTexturePackedAppendRecord> records,
        int sourceTextureCount,
        out string failureReason)
    {
        failureReason = "";
        HashSet<string> identities = new(StringComparer.OrdinalIgnoreCase);
        foreach ((NativeTerrainTexturePackedAppendRecord record, int index) in records.Select((record, index) => (record, index)))
        {
            if (string.IsNullOrWhiteSpace(record.StableEditId) || !identities.Add(record.StableEditId.Trim()))
            {
                failureReason = $"Packed append record {index} has a missing or duplicate stable edit identity.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(record.DonorLevelKey) || record.DonorWadEntry < 0 || record.DonorTextureId < 0)
            {
                failureReason = $"Packed append record '{record.StableEditId}' is missing donor provenance.";
                return false;
            }
            if (record.MaterialTemplateTextureId < 0 || record.MaterialTemplateTextureId >= sourceTextureCount)
            {
                failureReason =
                    $"Packed append record '{record.StableEditId}' uses destination material template T{record.MaterialTemplateTextureId}, outside the bound T0..T{sourceTextureCount - 1} table.";
                return false;
            }
            if (record.LowDetailRow == null || record.LowDetailRow.Length != LowDetailRecordBytes ||
                record.HighDetailRow == null || record.HighDetailRow.Length != HighDetailRecordBytes)
            {
                failureReason =
                    $"Packed append record '{record.StableEditId}' must contain one exact {LowDetailRecordBytes}-byte LQ row and {HighDetailRecordBytes}-byte HQ row.";
                return false;
            }
            string lowSha = Sha256(record.LowDetailRow);
            string highSha = Sha256(record.HighDetailRow);
            if (string.IsNullOrWhiteSpace(record.ExpectedLowDetailSha256) ||
                string.IsNullOrWhiteSpace(record.ExpectedHighDetailSha256) ||
                !HashEquals(record.ExpectedLowDetailSha256, lowSha) ||
                !HashEquals(record.ExpectedHighDetailSha256, highSha))
            {
                failureReason = $"Packed append record '{record.StableEditId}' no longer matches its LQ/HQ SHA-256 preimages.";
                return false;
            }
        }
        return true;
    }

    private static bool TryComposeExistingPatches(
        IReadOnlyList<NativeTerrainTextureRecordExistingPatch> patches,
        long levelDataWadOffset,
        LevelDataLayout sourceLayout,
        byte[] source,
        byte[] composed,
        int appendCount,
        out NativeTerrainTextureRecordRebasedPatchProof[]? proofs,
        out string failureReason)
    {
        proofs = null;
        failureReason = "";
        int sourceHighStart = checked(8 + (sourceLayout.TextureCount * LowDetailRecordBytes));
        int sourceTextureEnd = sourceLayout.TextureComponentByteLength;
        int lowGrowth = checked(appendCount * LowDetailRecordBytes);
        int fullGrowth = checked(appendCount * RecordGrowthBytes);
        List<(NativeTerrainTextureRecordExistingPatch Patch, int Start, int End, int OutputStart)> normalized = [];
        foreach (NativeTerrainTextureRecordExistingPatch patch in patches)
        {
            if (patch.Before == null || patch.After == null || patch.Before.Length == 0 || patch.Before.Length != patch.After.Length)
            {
                failureReason = $"Existing {patch.Kind} patch at WAD 0x{patch.WadOffset:X} must have equal non-empty before/after bytes.";
                return false;
            }
            long relativeLong = patch.WadOffset - levelDataWadOffset;
            long endLong = relativeLong + patch.Before.Length;
            if (relativeLong < 8 || endLong > sourceLayout.UsedByteLength)
            {
                failureReason = $"Existing {patch.Kind} patch at WAD 0x{patch.WadOffset:X} is outside the relocatable used level-data body.";
                return false;
            }
            int start = (int)relativeLong;
            int end = (int)endLong;
            int outputStart;
            if (end <= sourceHighStart)
                outputStart = start;
            else if (start >= sourceHighStart && end <= sourceTextureEnd)
                outputStart = checked(start + lowGrowth);
            else if (start >= sourceTextureEnd && end <= sourceLayout.UsedByteLength)
                outputStart = checked(start + fullGrowth);
            else
            {
                failureReason = $"Existing {patch.Kind} patch at WAD 0x{patch.WadOffset:X} crosses a texture-table insertion boundary and cannot be rebased atomically.";
                return false;
            }
            if (!source.AsSpan(start, patch.Before.Length).SequenceEqual(patch.Before))
            {
                failureReason = $"Existing {patch.Kind} patch at WAD 0x{patch.WadOffset:X} no longer matches its source-bound before bytes.";
                return false;
            }
            normalized.Add((patch, start, end, outputStart));
        }
        (NativeTerrainTextureRecordExistingPatch Patch, int Start, int End, int OutputStart)[] ordered = normalized
            .OrderBy(item => item.Start)
            .ThenBy(item => item.End)
            .ToArray();
        for (int index = 1; index < ordered.Length; index++)
        {
            if (ordered[index].Start < ordered[index - 1].End)
            {
                failureReason = $"Existing {ordered[index - 1].Patch.Kind} and {ordered[index].Patch.Kind} patches overlap inside the level-data subfile.";
                return false;
            }
        }
        foreach ((NativeTerrainTextureRecordExistingPatch patch, int start, _, _) in ordered)
            patch.After.CopyTo(composed, start);
        proofs = ordered
            .Select(item => new NativeTerrainTextureRecordRebasedPatchProof(
                item.Patch.Kind,
                item.Patch.RuntimeKey,
                item.Patch.WadOffset,
                levelDataWadOffset + item.OutputStart,
                item.Patch.Before.Length,
                Sha256(item.Patch.Before),
                Sha256(item.Patch.After)))
            .ToArray();
        return true;
    }

    private static bool TryVerifyRebasedPatchResults(
        IReadOnlyList<NativeTerrainTextureRecordExistingPatch> patches,
        IReadOnlyList<NativeTerrainTextureRecordRebasedPatchProof> proofs,
        byte[] output,
        long levelDataWadOffset,
        out string failureReason)
    {
        failureReason = "";
        if (patches.Count != proofs.Count)
        {
            failureReason = "The rebased existing-patch proof count changed.";
            return false;
        }
        foreach (NativeTerrainTextureRecordExistingPatch patch in patches)
        {
            NativeTerrainTextureRecordRebasedPatchProof? proof = proofs.SingleOrDefault(candidate =>
                candidate.SourceWadOffset == patch.WadOffset &&
                string.Equals(candidate.Kind, patch.Kind, StringComparison.Ordinal) &&
                string.Equals(candidate.RuntimeKey, patch.RuntimeKey, StringComparison.Ordinal));
            if (proof == null)
            {
                failureReason = $"Existing {patch.Kind} patch at WAD 0x{patch.WadOffset:X} has no unique rebase proof.";
                return false;
            }
            long relativeLong = proof.OutputWadOffset - levelDataWadOffset;
            if (relativeLong < 0 || relativeLong > int.MaxValue || relativeLong + patch.After.Length > output.Length ||
                !output.AsSpan((int)relativeLong, patch.After.Length).SequenceEqual(patch.After))
            {
                failureReason = $"Existing {patch.Kind} patch did not survive at rebased WAD offset 0x{proof.OutputWadOffset:X}.";
                return false;
            }
        }
        return true;
    }

    private static bool TryVerifyTerrainSurfaceSemantics(
        byte[] composedSource,
        byte[] output,
        long levelDataWadOffset,
        LevelDefinition level,
        int growth,
        out string failureReason)
    {
        failureReason = "";
        NativeTerrainSurfaceSourceData before;
        NativeTerrainSurfaceSourceData after;
        try
        {
            before = PortalSourceDataLocator.ParseTerrainSurfaces(
                composedSource,
                levelDataWadOffset,
                level.Key,
                level.DisplayName);
            after = PortalSourceDataLocator.ParseTerrainSurfaces(
                output,
                levelDataWadOffset,
                level.Key,
                level.DisplayName);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or OverflowException)
        {
            failureReason = $"The shifted terrain-surface/collision semantics did not reparse: {ex.Message}";
            return false;
        }
        if (after.SpecialSurfaceComponentWadOffset != before.SpecialSurfaceComponentWadOffset + growth ||
            after.CollisionComponentWadOffset != before.CollisionComponentWadOffset + growth ||
            after.Collision.TriangleCount != before.Collision.TriangleCount ||
            after.Collision.FlagCount != before.Collision.FlagCount ||
            after.SpecialSurfaces.Count != before.SpecialSurfaces.Count ||
            after.CollisionSurfaceTriangles.Count != before.CollisionSurfaceTriangles.Count)
        {
            failureReason = "The shifted terrain-surface or collision counts/offsets changed differently than the table growth.";
            return false;
        }
        for (int index = 0; index < before.SpecialSurfaces.Count; index++)
        {
            PortalSpecialSurfaceRecord left = before.SpecialSurfaces[index];
            PortalSpecialSurfaceRecord right = after.SpecialSurfaces[index];
            if (right.WadOffset != left.WadOffset + growth ||
                right.RelativeOffset != left.RelativeOffset ||
                !right.RawBytes.AsSpan().SequenceEqual(left.RawBytes))
            {
                failureReason = $"Special-surface record {index} did not retain its exact relative layout after the texture-table shift.";
                return false;
            }
        }
        for (int index = 0; index < before.CollisionSurfaceTriangles.Count; index++)
        {
            NativeCollisionSurfaceTriangle left = before.CollisionSurfaceTriangles[index];
            NativeCollisionSurfaceTriangle right = after.CollisionSurfaceTriangles[index];
            if (right.TriangleIndex != left.TriangleIndex ||
                right.FlagByte != left.FlagByte ||
                right.SurfaceIndex != left.SurfaceIndex ||
                right.SurfaceType != left.SurfaceType ||
                right.Param1 != left.Param1 ||
                right.Param2 != left.Param2 ||
                right.TriangleWadOffset != left.TriangleWadOffset + growth ||
                right.FlagWadOffset != ShiftOptionalWadOffset(left.FlagWadOffset, growth) ||
                !SamePoint(left.P1, right.P1, growth) ||
                !SamePoint(left.P2, right.P2, growth) ||
                !SamePoint(left.P3, right.P3, growth))
            {
                failureReason = $"Collision surface triangle {index} changed semantically during the texture-table shift.";
                return false;
            }
        }
        return true;
    }

    private static bool SamePoint(PortalSourcePoint left, PortalSourcePoint right, int growth) =>
        left.X == right.X && left.Y == right.Y && left.Z == right.Z && right.WadOffset == left.WadOffset + growth;

    private static long ShiftOptionalWadOffset(long wadOffset, int growth) =>
        wadOffset < 0 ? wadOffset : checked(wadOffset + growth);

    private static void ValidatePlanSourcePreimage(
        NativeTerrainTextureRecordAppendPlan plan,
        string sourceImagePath)
    {
        if (!HashEquals(Sha256File(sourceImagePath), plan.SourceBinding.SourceImageSha256))
            throw new InvalidDataException("The source BIN no longer matches the append plan's SHA-256 preimage.");
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream source = File.OpenRead(sourceImagePath);
        ValidateArchiveHeaders(source, layout, plan);
        byte[] before = DiscImage.ReadFileBytes(
            source,
            layout,
            WadLba,
            plan.Patch.WadOffset,
            plan.Patch.SourceByteLength);
        if (!before.SequenceEqual(plan.Patch.Before) || !HashEquals(Sha256(before), plan.Patch.BeforeSha256))
            throw new InvalidDataException("The source level-data subfile no longer matches the append plan preimage.");
    }

    private static void ValidateArchiveHeaders(
        FileStream image,
        DiscLayout layout,
        NativeTerrainTextureRecordAppendPlan plan)
    {
        byte[] wadHeader = DiscImage.ReadFileBytes(
            image,
            layout,
            WadLba,
            0,
            plan.WadArchiveHeaderByteLength);
        if (!HashEquals(Sha256(wadHeader), plan.WadArchiveHeaderSha256))
            throw new InvalidDataException("The WAD archive header changed while the fixed level-data subfile was expanded.");
        byte[] targetHeader = DiscImage.ReadFileBytes(
            image,
            layout,
            WadLba,
            plan.TargetEntryWadOffset,
            plan.TargetEntryHeaderByteLength);
        if (!HashEquals(Sha256(targetHeader), plan.TargetEntryHeaderSha256))
            throw new InvalidDataException("The target WAD-entry subfile header changed while its fixed level-data payload was expanded.");
    }

    private static LevelDataAsset LoadLevelDataAsset(
        FileStream image,
        DiscLayout layout,
        int targetWadEntry)
    {
        if (targetWadEntry < 0)
            throw new InvalidOperationException("The destination level has no mapped native WAD entry.");
        byte[] wadPrefix = DiscImage.ReadFileBytes(image, layout, WadLba, 0, 8);
        int wadHeaderLength = checked((int)ReadUInt32(wadPrefix, 0));
        if (wadHeaderLength < 8 || wadHeaderLength > MaximumArchiveHeaderBytes)
            throw new InvalidDataException($"The WAD archive header length 0x{wadHeaderLength:X} is outside the proven range.");
        byte[] wadHeader = DiscImage.ReadFileBytes(image, layout, WadLba, 0, wadHeaderLength);
        int targetSlot = checked(targetWadEntry * 8);
        if (targetSlot + 8 > wadHeader.Length)
            throw new InvalidDataException($"WAD entry {targetWadEntry} is outside the archive header.");
        long targetOffset = ReadUInt32(wadHeader, targetSlot);
        int targetLength = checked((int)ReadUInt32(wadHeader, targetSlot + 4));
        if (targetOffset <= 0 || targetLength < 16)
            throw new InvalidDataException($"WAD entry {targetWadEntry} has an invalid offset/length.");

        byte[] entryPrefix = DiscImage.ReadFileBytes(image, layout, WadLba, targetOffset, 8);
        int entryHeaderLength = checked((int)ReadUInt32(entryPrefix, 0));
        if (entryHeaderLength < 16 || entryHeaderLength > MaximumArchiveHeaderBytes || entryHeaderLength > targetLength)
            throw new InvalidDataException($"WAD entry {targetWadEntry}'s nested header length 0x{entryHeaderLength:X} is outside the proven range.");
        byte[] entryHeader = DiscImage.ReadFileBytes(image, layout, WadLba, targetOffset, entryHeaderLength);
        int subfileSlot = checked(LevelDataSubfileIndex * 8);
        if (subfileSlot + 8 > entryHeader.Length)
            throw new InvalidDataException($"WAD entry {targetWadEntry} has no level-data subfile {LevelDataSubfileIndex}.");
        int subfileOffset = checked((int)ReadUInt32(entryHeader, subfileSlot));
        int subfileLength = checked((int)ReadUInt32(entryHeader, subfileSlot + 4));
        if (subfileOffset < entryHeaderLength || subfileLength <= 0 || (long)subfileOffset + subfileLength > targetLength)
            throw new InvalidDataException($"WAD entry {targetWadEntry}'s level-data subfile boundary is invalid.");

        return new LevelDataAsset(
            targetOffset,
            targetLength,
            targetOffset + subfileOffset,
            subfileLength,
            wadHeader.Length,
            Sha256(wadHeader),
            entryHeader.Length,
            Sha256(entryHeader));
    }

    private static bool TryParseLayout(
        byte[] data,
        out LevelDataLayout? layout,
        out string failureReason)
    {
        layout = null;
        failureReason = "";
        if (data.Length < 8)
        {
            failureReason = "Level-data subfile is shorter than the texture header.";
            return false;
        }
        int textureLength = ReadInt32(data, 0);
        int textureCount = ReadInt32(data, 4);
        long expectedTextureLength = 8L + (textureCount * (long)RecordGrowthBytes);
        if (textureCount <= 0 || textureCount > MaximumTextureRecordCount ||
            expectedTextureLength > int.MaxValue || textureLength != expectedTextureLength || textureLength > data.Length)
        {
            failureReason = $"Terrain texture component has invalid size/count {textureLength}/{textureCount}.";
            return false;
        }

        int cursor = textureLength;
        List<LevelDataComponent> components = [];
        foreach (string name in new[] { "environment", "occlusion", "special surface", "collision", "cyclorama" })
        {
            if (!TryAdvanceComponent(data, ref cursor, name, components, out failureReason))
                return false;
        }
        if (cursor + 4 > data.Length)
        {
            failureReason = "Portal count is outside the level-data subfile.";
            return false;
        }
        int portalCount = ReadInt32(data, cursor);
        if (portalCount is < 0 or > MaximumPortalCount)
        {
            failureReason = $"Portal count {portalCount} is outside the proven range.";
            return false;
        }
        cursor += 4;
        for (int portal = 0; portal < portalCount; portal++)
        {
            if (cursor + 8 > data.Length)
            {
                failureReason = $"Portal {portal} header is outside level data.";
                return false;
            }
            int pointCount = ReadInt32(data, cursor + 4);
            if (pointCount is < 1 or > MaximumPortalPointCount)
            {
                failureReason = $"Portal {portal} point count {pointCount} is outside the proven range.";
                return false;
            }
            long skyboxStartLong = cursor + 0x40L + ((pointCount - 1L) * 12L);
            if (skyboxStartLong < 0 || skyboxStartLong > int.MaxValue || skyboxStartLong >= data.Length)
            {
                failureReason = $"Portal {portal} skybox component offset is invalid.";
                return false;
            }
            int portalStart = cursor;
            cursor = (int)skyboxStartLong;
            if (!TryAdvanceComponent(data, ref cursor, $"portal {portal} structure and cyclorama", components, out failureReason, portalStart))
                return false;
        }
        foreach (string name in new[] { "particle textures", "sound table" })
        {
            if (!TryAdvanceComponent(data, ref cursor, name, components, out failureReason))
                return false;
        }
        if (data.AsSpan(cursor).IndexOfAnyExcept((byte)0) >= 0)
        {
            failureReason = "Bytes after the final native sound component are not an all-zero tail.";
            return false;
        }
        layout = new LevelDataLayout(textureLength, textureCount, cursor, data.Length - cursor, components);
        return true;
    }

    private static bool TryAdvanceComponent(
        byte[] data,
        ref int cursor,
        string name,
        List<LevelDataComponent> components,
        out string failureReason,
        int? spanStart = null)
    {
        failureReason = "";
        int componentStart = cursor;
        if (componentStart < 0 || componentStart + 4 > data.Length)
        {
            failureReason = $"{name} component header is outside level data.";
            return false;
        }
        int length = ReadInt32(data, componentStart);
        if (length < 4 || (long)componentStart + length > data.Length)
        {
            failureReason = $"{name} component length 0x{length:X} is invalid at +0x{componentStart:X}.";
            return false;
        }
        cursor = componentStart + length;
        int start = spanStart ?? componentStart;
        components.Add(new LevelDataComponent(name, start, cursor - start));
        return true;
    }

    private static int ReadInt32(byte[] data, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));

    private static uint ReadUInt32(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));

    private static ArchiveEntrySpan[] ParseArchiveEntrySpans(byte[] header, int archiveLength)
    {
        List<ArchiveEntrySpan> entries = [];
        for (int slot = 0; slot + 8 <= header.Length; slot += 8)
        {
            uint offsetWord = ReadUInt32(header, slot);
            uint lengthWord = ReadUInt32(header, slot + 4);
            if (offsetWord == 0 || lengthWord == 0 ||
                offsetWord > int.MaxValue || lengthWord > int.MaxValue ||
                (long)offsetWord + lengthWord > archiveLength)
            {
                continue;
            }
            entries.Add(new ArchiveEntrySpan(
                slot / 8,
                (int)offsetWord,
                (int)lengthWord));
        }
        return entries.ToArray();
    }

    private static int CountLeadingZeroBytes(ReadOnlySpan<byte> bytes)
    {
        int count = 0;
        while (count < bytes.Length && bytes[count] == 0)
            count++;
        return count;
    }

    private static int AlignSector(int value) =>
        value <= 0 ? 0 : checked(((value + SectorBytes - 1) / SectorBytes) * SectorBytes);

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool HashEquals(string left, string right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void DeleteStale(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record LevelDataAsset(
        long TargetEntryWadOffset,
        int TargetEntryByteLength,
        long LevelDataWadOffset,
        int LevelDataByteLength,
        int WadArchiveHeaderByteLength,
        string WadArchiveHeaderSha256,
        int TargetEntryHeaderByteLength,
        string TargetEntryHeaderSha256);

    private sealed record LevelDataComponent(string Name, int Offset, int ByteLength);

    private sealed record ArchiveEntrySpan(int Index, int Offset, int ByteLength)
    {
        public int EndExclusive => checked(Offset + ByteLength);
    }

    private sealed record LevelDataLayout(
        int TextureComponentByteLength,
        int TextureCount,
        int UsedByteLength,
        int ZeroTailByteCount,
        IReadOnlyList<LevelDataComponent> Components);
}

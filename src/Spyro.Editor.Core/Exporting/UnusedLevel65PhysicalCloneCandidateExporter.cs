using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

public sealed record UnusedLevel65PhysicalCloneCandidateRequest(
    string SourceImagePath,
    string SourceCuePath,
    string OutputImagePath,
    string OutputCuePath);

public sealed record UnusedLevel65PhysicalCloneCandidatePlan(
    DateTimeOffset GeneratedAtUtc,
    string ProfileId,
    string SourceImageSha256,
    string BaseAliasProfileId,
    string BaseAliasImageSha256,
    int LevelId,
    int OverlayDirectoryIndex,
    int DataDirectoryIndex,
    long DonorOverlayWadOffset,
    int DonorOverlayByteLength,
    long DonorDataWadOffset,
    int DonorDataByteLength,
    long TargetOverlayWadOffset,
    long TargetDataWadOffset,
    int OriginalWadByteLength,
    int ExpandedWadByteLength,
    int OriginalExecutableLba,
    int RelocatedExecutableLba,
    int ExecutableSectorCount,
    int NextFileLba,
    string RelocatedExecutableSha256,
    IReadOnlyList<UnusedLevel65PhysicalCloneSectorCopy> PhysicalSectorCopies,
    IReadOnlyList<UnusedLevel65PhysicalCloneXaBoundaryRewrite> XaBoundaryRewrites,
    IReadOnlyList<UnusedLevel65BootstrapPatch> LogicalPatches,
    IReadOnlyList<string> RuntimeChecklist,
    bool RequiresDuckStationRuntimeProof);

public sealed record UnusedLevel65PhysicalCloneCandidateResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    UnusedLevel65PhysicalCloneCandidatePlan Plan,
    int CopiedPayloadSectorCount,
    int CopiedExecutableSectorCount,
    int RebuiltMetadataSectorCount,
    int RetaggedFileBoundarySectorCount,
    int VerifiedMode2Form1SectorCount,
    int ChangedRawSectorCount,
    bool ExactRawDiffBoundaryVerified,
    bool IndependentPayloadReadbackVerified,
    bool RetailPayloadsPreserved,
    bool RootRelocationVerified,
    bool SourceImagePreserved,
    bool AtomicRenameCompleted);

public sealed record UnusedLevel65PhysicalCloneSectorCopy(
    string Label,
    int SourceLba,
    int DestinationLba,
    int SectorCount,
    string LogicalPayloadSha256);

public sealed record UnusedLevel65PhysicalCloneXaBoundaryRewrite(
    int Lba,
    byte ExpectedSubmode,
    byte ReplacementSubmode,
    byte AllowedChangeMask);

/// <summary>
/// Disposable follow-up to the runtime-proven ID65 alias control. It copies the
/// exact Town Square overlay/data pair into new physical WAD storage, relocates
/// the already-patched executable, and updates only the two ISO root records.
/// Identity, portals, totals, music ownership, saving, and editor integration
/// remain deliberately out of scope.
/// </summary>
public static class UnusedLevel65PhysicalCloneCandidateExporter
{
    public const string ProfileId =
        "unused-level-65-town-square-physical-clone-clean-usa-disposable-v3";
    public const string ExpectedOutputImageSha256 =
        "f585e45ff1d795f8b2de64f20e1ed2953bfc7f43adf1c0b47b03e849e4865e48";

    private const int RawSectorBytes = 2352;
    private const int SectorBytes = 0x800;
    private const int WadLba = 37;
    private const int OriginalWadSize = 0x6927000;
    private const int ExpandedWadSize = 0x6C18800;
    private const int OriginalExecutableLba = 53875;
    private const int RelocatedExecutableLba = 55382;
    private const int ExecutableByteLength = 0x66000;
    private const int ExecutableSectorCount = ExecutableByteLength / SectorBytes;
    private const int NextFileLba = 60000;

    private const int DonorOverlayEntryIndex = 15;
    private const int DonorDataEntryIndex = 16;
    private const int TargetOverlayEntryIndex = 79;
    private const int TargetDataEntryIndex = 80;
    private const long DonorOverlayWadOffset = 0x118E800;
    private const int DonorOverlayByteLength = 0xF800;
    private const long DonorDataWadOffset = 0x119E000;
    private const int DonorDataByteLength = 0x2E2000;
    private const long TargetOverlayWadOffset = OriginalWadSize;
    private const long TargetDataWadOffset = TargetOverlayWadOffset + DonorOverlayByteLength;
    private const int OverlaySectorCount = DonorOverlayByteLength / SectorBytes;
    private const int DataSectorCount = DonorDataByteLength / SectorBytes;
    private const int PayloadSectorCount = OverlaySectorCount + DataSectorCount;
    private const int OriginalWadLastLba = OriginalExecutableLba - 1;
    private const int ExpandedWadLastLba = RelocatedExecutableLba - 1;
    private const byte XaDataSubmode = 0x08;
    private const byte XaDataEndSubmode = 0x89;
    private const byte XaEndFlagsMask = 0x81;
    private const int ExpectedChangedRawSectorCount =
        1 + 1 + 1 + PayloadSectorCount + ExecutableSectorCount;

    private const string CleanUsaImageSha256 =
        "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const string TownSquareOverlaySha256 =
        "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5";
    private const string TownSquareDataSha256 =
        "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";

    public static async Task<UnusedLevel65PhysicalCloneCandidateResult> ExportAsync(
        UnusedLevel65PhysicalCloneCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string sourceImage = Path.GetFullPath(request.SourceImagePath);
        string sourceCue = Path.GetFullPath(request.SourceCuePath);
        string outputImage = Path.GetFullPath(request.OutputImagePath);
        string outputCue = Path.GetFullPath(request.OutputCuePath);
        NativeLevelReplacementBaselineExporter.EnsureDistinctRoles(
            sourceImage,
            sourceCue,
            outputImage,
            outputCue);
        NativeLevelReplacementBaselineExporter.ValidateCue(
            sourceCue,
            sourceImage,
            "MODE2/2352");
        if (!PathEquals(Path.GetDirectoryName(outputImage), Path.GetDirectoryName(outputCue)))
            throw new InvalidOperationException("The physical ID65 BIN and CUE must share one directory.");

        string sourceImageSha256 = await HashFileAsync(sourceImage, cancellationToken);
        if (!string.Equals(sourceImageSha256, CleanUsaImageSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Physical ID65 cloning accepts only the exact clean USA BIN ({CleanUsaImageSha256}); selected SHA-256 was {sourceImageSha256}.");
        }

        ValidateStructuralConstants();
        Directory.CreateDirectory(Path.GetDirectoryName(outputImage)!);
        string operationId = Guid.NewGuid().ToString("N");
        string temporaryImage = Path.Combine(
            Path.GetDirectoryName(outputImage)!,
            $".{Path.GetFileName(outputImage)}.{operationId}.tmp");
        string aliasCue = Path.Combine(
            Path.GetDirectoryName(outputImage)!,
            $".{Path.GetFileNameWithoutExtension(outputImage)}.{operationId}.alias.cue");
        string temporaryCue = Path.Combine(
            Path.GetDirectoryName(outputCue)!,
            $".{Path.GetFileName(outputCue)}.{operationId}.tmp");
        string backupImage = Path.Combine(
            Path.GetDirectoryName(outputImage)!,
            $".{Path.GetFileName(outputImage)}.{operationId}.bak");
        string backupCue = Path.Combine(
            Path.GetDirectoryName(outputCue)!,
            $".{Path.GetFileName(outputCue)}.{operationId}.bak");
        bool imageBackedUp = false;
        bool cueBackedUp = false;
        bool imagePublished = false;
        bool cuePublished = false;
        try
        {
            UnusedLevel65BootstrapCandidateResult aliasResult =
                await UnusedLevel65BootstrapCandidateExporter.ExportAsync(
                    new UnusedLevel65BootstrapCandidateRequest(
                        sourceImage,
                        sourceCue,
                        temporaryImage,
                        aliasCue,
                        ExcludeLevel65FromFlightClassification: true),
                    cancellationToken);
            if (!string.Equals(
                    aliasResult.OutputImageSha256,
                    UnusedLevel65BootstrapCandidateExporter.ExpectedFlight65ExceptionOutputImageSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The runtime-proven ID65 alias base changed before physical cloning.");
            }

            TransformEvidence transformed = TransformAliasCandidate(temporaryImage);
            Verification verification = VerifyOutput(
                sourceImage,
                temporaryImage,
                transformed.ExpectedRelocatedExecutable,
                cancellationToken);
            string cueText = DiscImage.BuildCueText(sourceCue, Path.GetFileName(outputImage));
            await File.WriteAllTextAsync(temporaryCue, cueText, Encoding.ASCII, cancellationToken);
            string outputImageSha256 = await HashFileAsync(temporaryImage, cancellationToken);
            if (!string.Equals(outputImageSha256, ExpectedOutputImageSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Physical ID65 candidate SHA-256 is {outputImageSha256}, expected {ExpectedOutputImageSha256}.");
            }

            bool sourcePreserved = string.Equals(
                await HashFileAsync(sourceImage, cancellationToken),
                CleanUsaImageSha256,
                StringComparison.OrdinalIgnoreCase);
            if (!sourcePreserved)
                throw new InvalidDataException("The clean retail source changed during physical ID65 export.");

            if (File.Exists(outputImage))
            {
                File.Move(outputImage, backupImage);
                imageBackedUp = true;
            }
            if (File.Exists(outputCue))
            {
                File.Move(outputCue, backupCue);
                cueBackedUp = true;
            }
            File.Move(temporaryImage, outputImage);
            imagePublished = true;
            File.Move(temporaryCue, outputCue);
            cuePublished = true;
            NativeLevelReplacementBaselineExporter.ValidateCue(
                outputCue,
                outputImage,
                "MODE2/2352");

            byte[] cleanRow = new byte[16];
            byte[] physicalRow = BuildTargetDirectoryRow();
            List<UnusedLevel65BootstrapPatch> patches =
            [
                new(
                    "WAD.WAD",
                    "reserved-level-65-independent-overlay-and-data-directory-row",
                    TargetOverlayEntryIndex * 8L,
                    physicalRow.Length,
                    ToHex(cleanRow),
                    ToHex(physicalRow)),
                .. aliasResult.Plan.Patches.Where(patch =>
                    !string.Equals(
                        patch.Kind,
                        "reserved-level-65-overlay-and-data-directory-row",
                        StringComparison.Ordinal)),
                new(
                    "ISO9660-root",
                    "relocate-SCUS-942-28-extent",
                    0x232,
                    8,
                    "73 D2 00 00 00 00 D2 73",
                    "56 D8 00 00 00 00 D8 56"),
                new(
                    "ISO9660-root",
                    "expand-WAD-WAD-byte-length",
                    0x2B2,
                    8,
                    "00 70 92 06 06 92 70 00",
                    "00 88 C1 06 06 C1 88 00")
            ];
            List<UnusedLevel65PhysicalCloneSectorCopy> physicalCopies =
            [
                new(
                    "relocate patched SCUS before overwriting its old extent",
                    OriginalExecutableLba,
                    RelocatedExecutableLba,
                    ExecutableSectorCount,
                    Hash(transformed.ExpectedRelocatedExecutable)),
                new(
                    "copy Town Square overlay to independent ID65 storage",
                    WadLba + checked((int)(DonorOverlayWadOffset / SectorBytes)),
                    WadLba + checked((int)(TargetOverlayWadOffset / SectorBytes)),
                    OverlaySectorCount,
                    TownSquareOverlaySha256),
                new(
                    "copy Town Square data to independent ID65 storage",
                    WadLba + checked((int)(DonorDataWadOffset / SectorBytes)),
                    WadLba + checked((int)(TargetDataWadOffset / SectorBytes)),
                    DataSectorCount,
                    TownSquareDataSha256)
            ];
            List<UnusedLevel65PhysicalCloneXaBoundaryRewrite> xaBoundaryRewrites =
            [
                new(
                    OriginalWadLastLba,
                    XaDataEndSubmode,
                    XaDataSubmode,
                    XaEndFlagsMask),
                new(
                    ExpandedWadLastLba,
                    XaDataSubmode,
                    XaDataEndSubmode,
                    XaEndFlagsMask)
            ];
            UnusedLevel65PhysicalCloneCandidatePlan plan = new(
                DateTimeOffset.UtcNow,
                ProfileId,
                sourceImageSha256,
                aliasResult.Plan.ProfileId,
                aliasResult.OutputImageSha256,
                UnusedLevel65BootstrapCandidateExporter.LevelId,
                TargetOverlayEntryIndex,
                TargetDataEntryIndex,
                DonorOverlayWadOffset,
                DonorOverlayByteLength,
                DonorDataWadOffset,
                DonorDataByteLength,
                TargetOverlayWadOffset,
                TargetDataWadOffset,
                OriginalWadSize,
                ExpandedWadSize,
                OriginalExecutableLba,
                RelocatedExecutableLba,
                ExecutableSectorCount,
                NextFileLba,
                Hash(transformed.ExpectedRelocatedExecutable),
                physicalCopies,
                xaBoundaryRewrites,
                patches,
                RuntimeChecklist(),
                RequiresDuckStationRuntimeProof: true);
            UnusedLevel65PhysicalCloneCandidateResult result = new(
                outputImage,
                outputCue,
                outputImageSha256,
                plan,
                CopiedPayloadSectorCount: PayloadSectorCount,
                CopiedExecutableSectorCount: ExecutableSectorCount,
                RebuiltMetadataSectorCount: transformed.RebuiltMetadataSectorCount,
                RetaggedFileBoundarySectorCount: transformed.RetaggedFileBoundarySectorCount,
                VerifiedMode2Form1SectorCount: transformed.VerifiedMode2Form1SectorCount,
                ChangedRawSectorCount: verification.ChangedRawSectorCount,
                ExactRawDiffBoundaryVerified: true,
                IndependentPayloadReadbackVerified: true,
                RetailPayloadsPreserved: true,
                RootRelocationVerified: true,
                SourceImagePreserved: true,
                AtomicRenameCompleted: true);
            TryDelete(backupImage);
            imageBackedUp = false;
            TryDelete(backupCue);
            cueBackedUp = false;
            return result;
        }
        catch (Exception exportFailure)
        {
            if (cuePublished)
                TryDelete(outputCue);
            if (imagePublished)
                TryDelete(outputImage);
            List<Exception> recoveryFailures = [];
            Exception? imageRecovery = NativeLevelReplacementBaselineExporter.TryRestoreBackup(
                "BIN",
                backupImage,
                outputImage,
                ref imageBackedUp);
            Exception? cueRecovery = NativeLevelReplacementBaselineExporter.TryRestoreBackup(
                "CUE",
                backupCue,
                outputCue,
                ref cueBackedUp);
            if (imageRecovery != null)
                recoveryFailures.Add(imageRecovery);
            if (cueRecovery != null)
                recoveryFailures.Add(cueRecovery);
            if (recoveryFailures.Count > 0)
            {
                throw new IOException(
                    "Physical ID65 export failed and one or more prior outputs could not be restored.",
                    new AggregateException([exportFailure, .. recoveryFailures]));
            }
            throw;
        }
        finally
        {
            TryDelete(temporaryImage);
            TryDelete(aliasCue);
            TryDelete(temporaryCue);
            if (!imageBackedUp)
                TryDelete(backupImage);
            if (!cueBackedUp)
                TryDelete(backupCue);
        }
    }

    private static TransformEvidence TransformAliasCandidate(string imagePath)
    {
        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        if (layout.SectorSize != RawSectorBytes || layout.UserOffset != 24)
            throw new InvalidDataException("Physical ID65 cloning requires raw MODE2/2352.");
        using FileStream output = File.Open(
            imagePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(output, layout, IsWadName);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(output, layout, IsExecutableName);
        DiscFileRecord nextFile = DiscImage.FindRootFileRecord(output, layout, name =>
            name.StartsWith("PETEXA0", StringComparison.OrdinalIgnoreCase));
        if (wad.Lba != WadLba || wad.Size != OriginalWadSize ||
            executable.Lba != OriginalExecutableLba || executable.Size != ExecutableByteLength ||
            nextFile.Lba != NextFileLba)
        {
            throw new InvalidDataException("The ID65 alias base no longer has the guarded clean-USA ISO layout.");
        }

        byte[] aliasRow = DiscImage.ReadFileBytes(
            output,
            layout,
            WadLba,
            TargetOverlayEntryIndex * 8L,
            16);
        RequireDirectoryEntry(
            aliasRow,
            0,
            DonorOverlayWadOffset,
            DonorOverlayByteLength,
            "aliased ID65 overlay");
        RequireDirectoryEntry(
            aliasRow,
            1,
            DonorDataWadOffset,
            DonorDataByteLength,
            "aliased ID65 data");
        byte[] expectedExecutable = DiscImage.ReadFileBytes(
            output,
            layout,
            OriginalExecutableLba,
            0,
            ExecutableByteLength);

        // The old SCUS extent is the first destination payload range. Preserve
        // it at the relocated LBA before copying Town Square over the old LBA.
        int copiedExecutable = RawMode2Form1SectorIntegrity.CopyAbsoluteSectors(
            output,
            layout,
            OriginalExecutableLba,
            RelocatedExecutableLba,
            ExecutableSectorCount);
        int copiedOverlay = RawMode2Form1SectorIntegrity.CopyAbsoluteSectors(
            output,
            layout,
            WadLba + checked((int)(DonorOverlayWadOffset / SectorBytes)),
            WadLba + checked((int)(TargetOverlayWadOffset / SectorBytes)),
            OverlaySectorCount);
        int copiedData = RawMode2Form1SectorIntegrity.CopyAbsoluteSectors(
            output,
            layout,
            WadLba + checked((int)(DonorDataWadOffset / SectorBytes)),
            WadLba + checked((int)(TargetDataWadOffset / SectorBytes)),
            DataSectorCount);
        if (copiedExecutable != ExecutableSectorCount ||
            copiedOverlay + copiedData != PayloadSectorCount)
        {
            throw new InvalidDataException("Physical ID65 raw-sector copy count changed.");
        }

        int retaggedBoundaries =
            RawMode2Form1SectorIntegrity.RewriteDuplicatedSubmodeFlags(
                output,
                layout,
                OriginalWadLastLba,
                XaDataEndSubmode,
                XaDataSubmode,
                XaEndFlagsMask) +
            RawMode2Form1SectorIntegrity.RewriteDuplicatedSubmodeFlags(
                output,
                layout,
                ExpandedWadLastLba,
                XaDataSubmode,
                XaDataEndSubmode,
                XaEndFlagsMask);

        DiscImage.WriteFileBytes(
            output,
            layout,
            WadLba,
            TargetOverlayEntryIndex * 8L,
            BuildTargetDirectoryRow());
        byte[] rootDirectory = DiscImage.ReadFileBytes(
            output,
            layout,
            layout.RootExtent,
            0,
            layout.RootLength);
        PatchRootRecord(rootDirectory, wad.Name, WadLba, ExpandedWadSize);
        PatchRootRecord(
            rootDirectory,
            executable.Name,
            RelocatedExecutableLba,
            ExecutableByteLength);
        DiscImage.WriteFileBytes(
            output,
            layout,
            layout.RootExtent,
            0,
            rootDirectory);
        int rebuiltMetadata =
            RawMode2Form1SectorIntegrity.RebuildFileRanges(
                output,
                layout,
                WadLba,
                [(TargetOverlayEntryIndex * 8L, 16)]) +
            RawMode2Form1SectorIntegrity.RebuildFileRanges(
                output,
                layout,
                layout.RootExtent,
                [(0, layout.RootLength)]);
        int verified = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            output,
            layout,
            [
                (layout.RootExtent, DivideRoundUp(layout.RootLength, SectorBytes)),
                (WadLba, 1),
                (OriginalWadLastLba, 1),
                (OriginalExecutableLba, PayloadSectorCount),
                (RelocatedExecutableLba, ExecutableSectorCount)
            ]);
        RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(
            output,
            layout,
            OriginalWadLastLba,
            XaDataSubmode);
        RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(
            output,
            layout,
            ExpandedWadLastLba,
            XaDataEndSubmode);
        RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(
            output,
            layout,
            RelocatedExecutableLba + ExecutableSectorCount - 1,
            XaDataEndSubmode);
        if (rebuiltMetadata != 2 || retaggedBoundaries != 2 ||
            verified != ExpectedChangedRawSectorCount)
            throw new InvalidDataException("Physical ID65 metadata rebuild or integrity-verification count changed.");
        output.Flush(flushToDisk: true);
        return new TransformEvidence(
            expectedExecutable,
            rebuiltMetadata,
            retaggedBoundaries,
            verified);
    }

    private static Verification VerifyOutput(
        string sourceImage,
        string outputImage,
        byte[] expectedExecutable,
        CancellationToken cancellationToken)
    {
        DiscLayout sourceLayout = DiscImage.DetectLayout(sourceImage);
        DiscLayout outputLayout = DiscImage.DetectLayout(outputImage);
        using FileStream source = File.OpenRead(sourceImage);
        using FileStream output = File.OpenRead(outputImage);
        if (sourceLayout != outputLayout || source.Length != output.Length)
            throw new InvalidDataException("Physical ID65 cloning changed the raw image layout or length.");

        DiscFileRecord sourceWad = DiscImage.FindRootFileRecord(source, sourceLayout, IsWadName);
        DiscFileRecord outputWad = DiscImage.FindRootFileRecord(output, outputLayout, IsWadName);
        DiscFileRecord sourceExecutable = DiscImage.FindRootFileRecord(source, sourceLayout, IsExecutableName);
        DiscFileRecord outputExecutable = DiscImage.FindRootFileRecord(output, outputLayout, IsExecutableName);
        if (sourceWad.Lba != WadLba || sourceWad.Size != OriginalWadSize ||
            outputWad.Lba != WadLba || outputWad.Size != ExpandedWadSize ||
            sourceExecutable.Lba != OriginalExecutableLba ||
            outputExecutable.Lba != RelocatedExecutableLba ||
            outputExecutable.Size != ExecutableByteLength)
        {
            throw new InvalidDataException("Physical ID65 WAD/SCUS root relocation failed readback.");
        }

        byte[] expectedRoot = DiscImage.ReadFileBytes(
            source,
            sourceLayout,
            sourceLayout.RootExtent,
            0,
            sourceLayout.RootLength);
        PatchRootRecord(expectedRoot, sourceWad.Name, WadLba, ExpandedWadSize);
        PatchRootRecord(
            expectedRoot,
            sourceExecutable.Name,
            RelocatedExecutableLba,
            ExecutableByteLength);
        byte[] actualRoot = DiscImage.ReadFileBytes(
            output,
            outputLayout,
            outputLayout.RootExtent,
            0,
            outputLayout.RootLength);
        VerifyEqual(expectedRoot, actualRoot, "physical ID65 ISO root directory");

        byte[] physicalRow = DiscImage.ReadFileBytes(
            output,
            outputLayout,
            WadLba,
            TargetOverlayEntryIndex * 8L,
            16);
        VerifyEqual(BuildTargetDirectoryRow(), physicalRow, "physical ID65 WAD directory row");
        RequireHash(
            DiscImage.ReadFileBytes(
                output,
                outputLayout,
                WadLba,
                TargetOverlayWadOffset,
                DonorOverlayByteLength),
            TownSquareOverlaySha256,
            "independent ID65 overlay payload");
        RequireHash(
            DiscImage.ReadFileBytes(
                output,
                outputLayout,
                WadLba,
                TargetDataWadOffset,
                DonorDataByteLength),
            TownSquareDataSha256,
            "independent ID65 data payload");
        RequireHash(
            DiscImage.ReadFileBytes(
                output,
                outputLayout,
                WadLba,
                DonorOverlayWadOffset,
                DonorOverlayByteLength),
            TownSquareOverlaySha256,
            "preserved retail Town Square overlay payload");
        RequireHash(
            DiscImage.ReadFileBytes(
                output,
                outputLayout,
                WadLba,
                DonorDataWadOffset,
                DonorDataByteLength),
            TownSquareDataSha256,
            "preserved retail Town Square data payload");
        VerifyEqual(
            expectedExecutable,
            DiscImage.ReadFileBytes(
                output,
                outputLayout,
                RelocatedExecutableLba,
                0,
                ExecutableByteLength),
            "relocated patched SCUS");

        VerifyOriginalWadBoundary(source, output, sourceLayout, cancellationToken);
        int changedRawSectors = VerifyRawDiffBoundary(
            source,
            output,
            sourceLayout,
            cancellationToken);
        return new Verification(changedRawSectors);
    }

    private static void VerifyOriginalWadBoundary(
        FileStream source,
        FileStream output,
        DiscLayout layout,
        CancellationToken cancellationToken)
    {
        const int chunkSize = 1 << 20;
        long allowedStart = TargetOverlayEntryIndex * 8L;
        long allowedEnd = allowedStart + 16;
        for (long offset = 0; offset < OriginalWadSize; offset += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int length = checked((int)Math.Min(chunkSize, OriginalWadSize - offset));
            byte[] before = DiscImage.ReadFileBytes(source, layout, WadLba, offset, length);
            byte[] after = DiscImage.ReadFileBytes(output, layout, WadLba, offset, length);
            for (int index = 0; index < length; index++)
            {
                long logicalOffset = offset + index;
                if (before[index] != after[index] &&
                    (logicalOffset < allowedStart || logicalOffset >= allowedEnd))
                {
                    throw new InvalidDataException(
                        $"Physical ID65 changed original WAD byte 0x{logicalOffset:X} outside rows 79/80.");
                }
            }
        }
    }

    private static int VerifyRawDiffBoundary(
        FileStream source,
        FileStream output,
        DiscLayout layout,
        CancellationToken cancellationToken)
    {
        if (layout.SectorSize != RawSectorBytes || source.Length % RawSectorBytes != 0)
            throw new InvalidDataException("Physical ID65 raw diff requires a complete MODE2/2352 image.");
        HashSet<long> expected =
        [
            layout.RootExtent,
            WadLba,
            OriginalWadLastLba
        ];
        for (int lba = OriginalExecutableLba;
             lba < RelocatedExecutableLba + ExecutableSectorCount;
             lba++)
        {
            expected.Add(lba);
        }

        HashSet<long> changed = [];
        const int sectorsPerChunk = 256;
        byte[] before = new byte[RawSectorBytes * sectorsPerChunk];
        byte[] after = new byte[before.Length];
        long sectorCount = source.Length / RawSectorBytes;
        for (long first = 0; first < sectorCount; first += sectorsPerChunk)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = checked((int)Math.Min(sectorsPerChunk, sectorCount - first));
            int bytes = count * RawSectorBytes;
            source.Position = first * RawSectorBytes;
            output.Position = first * RawSectorBytes;
            source.ReadExactly(before.AsSpan(0, bytes));
            output.ReadExactly(after.AsSpan(0, bytes));
            for (int index = 0; index < count; index++)
            {
                ReadOnlySpan<byte> left = before.AsSpan(index * RawSectorBytes, RawSectorBytes);
                ReadOnlySpan<byte> right = after.AsSpan(index * RawSectorBytes, RawSectorBytes);
                if (!left.SequenceEqual(right))
                    changed.Add(first + index);
            }
        }
        if (!changed.SetEquals(expected))
        {
            string missing = string.Join(",", expected.Except(changed).Take(12));
            string extra = string.Join(",", changed.Except(expected).Take(12));
            throw new InvalidDataException(
                $"Physical ID65 raw diff boundary failed: changed={changed.Count}, expected={expected.Count}, missing=[{missing}], extra=[{extra}].");
        }
        if (changed.Count != ExpectedChangedRawSectorCount)
            throw new InvalidDataException("Physical ID65 changed-sector count no longer matches the checked plan.");
        return changed.Count;
    }

    private static IReadOnlyList<string> RuntimeChecklist() =>
    [
        "Disable memory-card insertion completely and cold boot the candidate.",
        $"From controllable gameplay, press Select to open Inventory; enter {TestLevelWarpPatch.ActivationSequence}; then press Left, then Down to load ID65.",
        "Expected: the placeholder A entry loads Town Square normally from physically independent rows 79/80. Confirm geometry, textures, sky, collision, camera, initial music, enemy animation, pause, and Inventory during a short movement check.",
        "Collect exactly one loose gem, then reset DuckStation. Do not rescue dragons, touch the egg thief, attack or kill enemies, open or break chests, die, save, or use Return Home.",
        $"Cold boot and reach controllable gameplay. Press Select to open Inventory; enter {TestLevelWarpPatch.ActivationSequence}; then press Left, then Down to re-enter ID65. Confirm the collected gem is present again because the no-card reset discarded the session.",
        $"Reset and reach controllable gameplay. Press Select to open Inventory; enter {TestLevelWarpPatch.ActivationSequence}; then press Cross, then Triangle. Verify retail Town Square.",
        $"Reset and reach controllable gameplay. Press Select to open Inventory; enter {TestLevelWarpPatch.ActivationSequence}; then press Left, then Right. Verify Gnasty's Loot.",
        $"Reset and reach controllable gameplay. Press Select to open Inventory; enter {TestLevelWarpPatch.ActivationSequence}; then press Cross, then Down. Verify Sunny Flight and confirm normal flight controls/timer only; do not finish the flight.",
        "Do not select Exit Level or Quit Game in ID65. Portal-to-exit 65, authored identity/totals, alternate music, save ownership, memory-card persistence, and Return Home remain separate later gates."
    ];

    private static byte[] BuildTargetDirectoryRow()
    {
        byte[] row = new byte[16];
        WriteDirectoryEntry(row, 0, TargetOverlayWadOffset, DonorOverlayByteLength);
        WriteDirectoryEntry(row, 8, TargetDataWadOffset, DonorDataByteLength);
        return row;
    }

    private static void ValidateStructuralConstants()
    {
        if (DonorOverlayByteLength % SectorBytes != 0 ||
            DonorDataByteLength % SectorBytes != 0 ||
            ExecutableByteLength % SectorBytes != 0 ||
            TargetDataWadOffset + DonorDataByteLength != ExpandedWadSize ||
            WadLba + (ExpandedWadSize / SectorBytes) != RelocatedExecutableLba ||
            RelocatedExecutableLba + ExecutableSectorCount > NextFileLba ||
            ExpectedChangedRawSectorCount != 1714)
        {
            throw new InvalidDataException("Physical ID65 structural constants no longer balance.");
        }
    }

    private static void PatchRootRecord(byte[] directory, string expectedName, int lba, int size)
    {
        for (int offset = 0; offset < directory.Length;)
        {
            int recordLength = directory[offset];
            if (recordLength == 0)
            {
                offset = ((offset / SectorBytes) + 1) * SectorBytes;
                continue;
            }
            if (recordLength < 34 || offset + recordLength > directory.Length)
                break;
            int nameLength = directory[offset + 32];
            string name = Encoding.ASCII.GetString(directory, offset + 33, nameLength)
                .Replace(";1", "", StringComparison.OrdinalIgnoreCase);
            if (string.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                WriteBothEndianUInt32(directory, offset + 2, checked((uint)lba));
                WriteBothEndianUInt32(directory, offset + 10, checked((uint)size));
                return;
            }
            offset += recordLength;
        }
        throw new InvalidDataException($"ISO root record '{expectedName}' was not found.");
    }

    private static void RequireDirectoryEntry(
        byte[] row,
        int rowIndex,
        long expectedOffset,
        int expectedLength,
        string label)
    {
        int offset = rowIndex * 8;
        long actualOffset = BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(offset, 4));
        int actualLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(offset + 4, 4)));
        if (actualOffset != expectedOffset || actualLength != expectedLength)
            throw new InvalidDataException($"The {label} directory entry changed.");
    }

    private static void WriteDirectoryEntry(byte[] bytes, int offset, long wadOffset, int byteLength)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), checked((uint)wadOffset));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + 4, 4), checked((uint)byteLength));
    }

    private static void WriteBothEndianUInt32(byte[] bytes, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset + 4, 4), value);
    }

    private static void RequireHash(byte[] bytes, string expected, string label)
    {
        string actual = Hash(bytes);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{label} SHA-256 is {actual}, expected {expected}.");
    }

    private static void VerifyEqual(byte[] expected, byte[] actual, string label)
    {
        if (!expected.AsSpan().SequenceEqual(actual))
            throw new InvalidDataException($"{label} failed byte-for-byte readback.");
    }

    private static int DivideRoundUp(int value, int divisor) =>
        checked((value + divisor - 1) / divisor);

    private static string ToHex(IEnumerable<byte> bytes) =>
        string.Join(' ', bytes.Select(value => $"{value:X2}"));

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static bool IsWadName(string name) =>
        name.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase);

    private static bool IsExecutableName(string name) =>
        name.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase);

    private static bool PathEquals(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record TransformEvidence(
        byte[] ExpectedRelocatedExecutable,
        int RebuiltMetadataSectorCount,
        int RetaggedFileBoundarySectorCount,
        int VerifiedMode2Form1SectorCount);

    private sealed record Verification(int ChangedRawSectorCount);
}

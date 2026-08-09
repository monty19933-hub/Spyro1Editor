using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

public sealed record UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateRequest(
    string BaseImagePath,
    string BaseCuePath,
    string OutputImagePath,
    string OutputCuePath);

public sealed record UnusedLevel65AuthoredTerrainSolidTriangleControlPoint(
    int X,
    int Y,
    int Z);

public sealed record UnusedLevel65AuthoredTerrainSolidTriangleControlPatch(
    string Kind,
    string RuntimeKey,
    int VertexIndex,
    int CollisionTriangleIndex,
    long WadOffset,
    string BeforeHex,
    string AfterHex,
    UnusedLevel65AuthoredTerrainSolidTriangleControlPoint OriginalPoint,
    UnusedLevel65AuthoredTerrainSolidTriangleControlPoint AuthoredPoint);

public sealed record UnusedLevel65AuthoredTerrainSolidTriangleControlRawSectorDiff(
    int RawSectorLba,
    int HeaderChangedBytes,
    int PayloadChangedBytes,
    int EdcChangedBytes,
    int ReservedChangedBytes,
    int EccPChangedBytes,
    int EccQChangedBytes,
    int TotalChangedBytes);

public sealed record UnusedLevel65AuthoredTerrainSolidTriangleControlCandidatePlan(
    DateTimeOffset GeneratedAtUtc,
    string ProfileId,
    string BaseProfileId,
    string BaseImageSha256,
    string BaseExecutableSha256,
    string BaseOverlaySha256,
    string BaseDataSha256,
    string ExpectedOutputDataSha256,
    int LevelId,
    int ContinuousLevelIndex,
    int TargetOverlayWadEntry,
    int TargetDataWadEntry,
    long TargetOverlayWadOffset,
    int TargetOverlayByteLength,
    long TargetDataWadOffset,
    int TargetDataByteLength,
    string RuntimeKey,
    int SourceSectorIndex,
    long SourceSectorWadOffset,
    long SourceFaceWadOffset,
    int SourceTextureId,
    UnusedLevel65AuthoredTerrainSolidTriangleControlPoint SpawnPoint,
    IReadOnlyList<UnusedLevel65AuthoredTerrainSolidTriangleControlPoint> OriginalFacePoints,
    IReadOnlyList<UnusedLevel65AuthoredTerrainSolidTriangleControlPatch> Patches,
    IReadOnlyList<int> SourceFaceVertexIndexes,
    IReadOnlyList<int> AffectedFaceIndexes,
    int CollisionTriangleIndex,
    long CollisionTriangleWadOffset,
    long CollisionAssignmentWadOffset,
    int CollisionLookupReferenceCount,
    IReadOnlyList<long> CollisionLookupWadOffsets,
    string CollisionHeaderSha256,
    string CollisionBlockTreeSha256,
    string CollisionBlocksSha256,
    string CollisionAssignmentsSha256,
    string CollisionFlagsSha256,
    IReadOnlyList<int> AffectedRawSectorLbas,
    IReadOnlyList<string> RuntimeChecklist,
    bool CollisionPatched,
    bool CountsAndComponentSizesUnchanged,
    bool RequiresDuckStationRuntimeProof);

public sealed record UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    string OutputDataSha256,
    UnusedLevel65AuthoredTerrainSolidTriangleControlCandidatePlan Plan,
    long ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    IReadOnlyList<UnusedLevel65AuthoredTerrainSolidTriangleControlRawSectorDiff> RawSectorDiffs,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool VertexReadbackVerified,
    bool RawSectorIntegrityVerified,
    bool TerrainCountsPreserved,
    bool TerrainFacePreserved,
    bool UniqueVertexIsolationVerified,
    bool CollisionStructurePreserved,
    bool CollisionTriangleReadbackVerified,
    bool CollisionLookupPreserved,
    bool CollisionAssignmentPreserved,
    bool RetailTownSquarePreserved,
    bool Id65OverlayPreserved,
    bool ExecutablePreserved,
    bool BaseCandidatePreserved,
    bool AtomicRenameCompleted);

/// <summary>
/// Disposable solid-existing-triangle control for physically independent ID65.
/// It starts from the exact focused-runtime-passed display-name candidate,
/// raises one HP vertex used by exactly one live face, and applies the same
/// source-derived height to that face's exact native collision triangle. It
/// does not add a face, change any count, grow/repack a sector or component,
/// or rebuild the collision tree/index. Runtime decides whether visual and
/// playable collision geometry now agree on the authored row-80 slope.
/// </summary>
public static class UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateExporter
{
    public const string ProfileId =
        "unused-level-65-town-square-authored-terrain-solid-existing-triangle-control-clean-usa-disposable-v3";
    public const string ExpectedOutputImageSha256 =
        "976a1910264c48fbc7cec8a9a1291f849bcad898fc511bae5d986a7af1c66214";
    public const string ExpectedOutputDataSha256 =
        "a824efbcde9481ad9be46f20531a3277ca76f14230c74113b562630ca2c95f35";

    public const int LevelId = 65;
    public const int ContinuousLevelIndex = 35;
    public const int TargetOverlayWadEntry = 79;
    public const int TargetDataWadEntry = 80;
    public const long TargetOverlayWadOffset = 0x6927000;
    public const int TargetOverlayByteLength = 0xF800;
    public const long TargetDataWadOffset = 0x6936800;
    public const int TargetDataByteLength = 0x2E2000;
    public const string RuntimeKey = "213:64:hp";
    public const int SourceSectorIndex = 213;
    public const long SourceSectorWadOffset = 0x6A3E8B4;
    public const long SourceFaceWadOffset = 0x6A3F6BC;
    public const int SourceTextureId = 5;
    public const int AuthoredPeakZ = 576;
    public const int AuthoredVertexIndex = 95;
    public const int CollisionTriangleIndex = 13853;
    public const long CollisionTriangleWadOffset = 0x6A87B20;
    public const long CollisionAssignmentWadOffset = 0x6A9C861;

    private const string BaseImageSha256 =
        UnusedLevel65DisplayNameCandidateExporter.ExpectedOutputImageSha256;
    private const string BaseExecutableSha256 =
        "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";
    private const string BaseOverlaySha256 =
        "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5";
    private const string BaseDataSha256 =
        "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";
    private const string OcclusionComponentSha256 =
        "8d609e63622428491bd0d05e32dc159090ba5b4eb4d6c6028323e7f4faa1d47b";
    private const string SpecialSurfaceComponentSha256 =
        "baca2f385dde2fd64259d679e80d6693e66b8e8c18f6900aba6290bb581eda2d";
    private const string CollisionComponentSha256 =
        "84901b6b9faa2f7fb0fce1d3aaa7e00fadf49e2d4bd7b2bcb2a0bb9a77397e2f";
    private const string CollisionHeaderSha256 =
        "73d2390f99633c263650569efc56788d5bf918ff05b02f39bf7b378e255c6e98";
    private const string CollisionBlockTreeSha256 =
        "ad6ce785e5d49ff97c5fb79c967f2d0bcff2f2e2d40ef1137cdce0114e4ea6ed";
    private const string CollisionBlocksSha256 =
        "c2e0d178d39cd8ce8439d44d64987c81e60c93e0eb50f43c4a7ce51a258250d5";
    private const string CollisionAssignmentsSha256 =
        "d1e9140fc8b3ca6fe7edf3f080ee74a9acad3e2373a55de9cf7678103ee29b0d";
    private const string CollisionFlagsSha256 =
        "ef48378248aa661b07d1fa37059177924aa5881ae8863bf778a4ab1917371f92";

    private const long RetailTownSquareOverlayWadOffset = 0x118E800;
    private const int RetailTownSquareOverlayByteLength = 0xF800;
    private const long RetailTownSquareDataWadOffset = 0x119E000;
    private const int RetailTownSquareDataByteLength = 0x2E2000;
    private const long EnvironmentComponentWadOffset = 0x6A17F78;
    private const int EnvironmentComponentByteLength = 0x284A4;
    private const long OcclusionComponentWadOffset = 0x6A4041C;
    private const int OcclusionComponentByteLength = 0x974;
    private const long SpecialSurfaceComponentWadOffset = 0x6A40D90;
    private const int SpecialSurfaceComponentByteLength = 0x30;
    private const long CollisionComponentWadOffset = 0x6A40DC0;
    private const int CollisionComponentByteLength = 0x5FAE8;
    private const long CollisionHeaderWadOffset = CollisionComponentWadOffset;
    private const int CollisionHeaderByteLength = 0x20;
    private const long CollisionBlockTreeWadOffset = 0x6A40DE0;
    private const int CollisionBlockTreeByteLength = 0x6A60;
    private const long CollisionBlocksWadOffset = 0x6A47840;
    private const int CollisionBlocksByteLength = 0x17984;
    private const long CollisionTriangleTableWadOffset = 0x6A5F1C4;
    private const int CollisionTriangleCount = 0x4D60;
    private const int CollisionTriangleTableByteLength = CollisionTriangleCount * 12;
    private const long CollisionAssignmentsWadOffset = 0x6A99244;
    private const int CollisionAssignmentsByteLength = CollisionTriangleCount;
    private const long CollisionFlagsWadOffset = 0x6A9DFA4;
    private const int CollisionFlagsByteLength = 0x2904;
    private const int ExpectedCollisionLookupReferenceCount = 2;
    private static readonly long[] ExpectedCollisionLookupWadOffsets = [0x6A51682, 0x6A5191E];
    private const long ProtectedFollowingSectorHeaderWadOffset = 0x6A3C4F8;
    private const int SceneSectorHeaderByteLength = 28;
    private const int SourceFaceByteLength = 16;
    private const int WadLba = 37;
    private const int ExpectedWadByteLength = 0x6C18800;
    private const int ExecutableLba = 55382;
    private const int ExecutableByteLength = 0x66000;
    private const int RawSectorBytes = 2352;
    private const int UserOffset = 24;
    private const int ExpectedVisualRawSector = 54434;
    private const int ExpectedCollisionRawSector = 54580;
    private const long ExpectedChangedLogicalWadBytes = 2;
    private const long ExpectedChangedPhysicalImageBytes = 74;
    private const byte XaDataSubmode = 0x08;

    private static readonly byte[] ExpectedSourceSectorHeader = Convert.FromHexString(
        "101A091FDC028F025417031D1409E001233015008CB9717DFFFFFFFF");
    private static readonly byte[] ExpectedSourceFace = Convert.FromHexString(
        "5D5D5F5E6D6D6C6B0500000020001000");
    private static readonly byte[] ExpectedProtectedFollowingSectorHeader = Convert.FromHexString(
        "130B04257B41E001000A0024FFFFE0010401010000000007FFFFFFFF");
    private static readonly UnusedLevel65AuthoredTerrainSolidTriangleControlPoint[] OriginalFacePoints =
    [
        new(8056, 6317, 512),
        new(8167, 6325, 512),
        new(8123, 6410, 512)
    ];
    private static readonly UnusedLevel65AuthoredTerrainSolidTriangleControlPatch[] ExpectedPatches =
    [
        new(
            "visual-hp",
            RuntimeKey,
            AuthoredVertexIndex,
            -1,
            0x6A3EC40,
            "20 84 85 5C",
            "60 84 85 5C",
            new(8167, 6325, 512),
            new(8167, 6325, AuthoredPeakZ)),
        new(
            "collision-triangle",
            RuntimeKey,
            -1,
            CollisionTriangleIndex,
            CollisionTriangleWadOffset,
            "BB 1F 8B DE 0A D9 EA D1 00 02 00 00",
            "BB 1F 8B DE 0A D9 EA D1 00 02 40 00",
            new(8167, 6325, 512),
            new(8167, 6325, AuthoredPeakZ))
    ];
    private static readonly int[] SourceFaceVertexIndexes = [93, 93, 95, 94];
    private static readonly int[] AffectedFaceIndexes = [64];

    public static async Task<UnusedLevel65AuthoredTerrainSolidTriangleControlCandidatePlan> BuildPlanAsync(
        UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateRequest request,
        CancellationToken cancellationToken = default) =>
        (await PrepareAsync(request, cancellationToken)).Plan;

    public static async Task<UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateResult> ExportAsync(
        UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        Prepared prepared = await PrepareAsync(request, cancellationToken);
        string baseImage = Path.GetFullPath(request.BaseImagePath);
        string baseCue = Path.GetFullPath(request.BaseCuePath);
        string outputImage = Path.GetFullPath(request.OutputImagePath);
        string outputCue = Path.GetFullPath(request.OutputCuePath);
        NativeLevelReplacementBaselineExporter.EnsureDistinctRoles(
            baseImage,
            baseCue,
            outputImage,
            outputCue);
        if (!PathEquals(Path.GetDirectoryName(outputImage), Path.GetDirectoryName(outputCue)))
            throw new InvalidOperationException("The ID65 solid-triangle BIN and CUE must share one directory.");

        string outputDirectory = Path.GetDirectoryName(outputImage)!;
        Directory.CreateDirectory(outputDirectory);
        string operationId = Guid.NewGuid().ToString("N");
        string temporaryImage = Path.Combine(outputDirectory, $".{Path.GetFileName(outputImage)}.{operationId}.tmp");
        string temporaryCue = Path.Combine(outputDirectory, $".{Path.GetFileName(outputCue)}.{operationId}.tmp");
        string backupImage = Path.Combine(outputDirectory, $".{Path.GetFileName(outputImage)}.{operationId}.bak");
        string backupCue = Path.Combine(outputDirectory, $".{Path.GetFileName(outputCue)}.{operationId}.bak");
        bool imageBackedUp = false;
        bool cueBackedUp = false;
        bool imagePublished = false;
        bool cuePublished = false;
        try
        {
            await DiscImageWorkingCopy.StageAsync(
                baseImage,
                temporaryImage,
                consumeDisposableSource: false,
                cancellationToken);
            DiscLayout layout = DiscImage.DetectLayout(temporaryImage);
            int rebuiltRawSectors;
            await using (FileStream output = new(
                             temporaryImage,
                             FileMode.Open,
                             FileAccess.ReadWrite,
                             FileShare.None))
            {
                foreach (UnusedLevel65AuthoredTerrainSolidTriangleControlPatch patch in ExpectedPatches)
                {
                    byte[] before = ParseHex(patch.BeforeHex);
                    byte[] after = ParseHex(patch.AfterHex);
                    byte[] actualBefore = DiscImage.ReadFileBytes(
                        output,
                        layout,
                        WadLba,
                        patch.WadOffset,
                        before.Length);
                    RequireEqual(actualBefore, before, $"staged {patch.Kind} preimage");
                    DiscImage.WriteFileBytes(output, layout, WadLba, patch.WadOffset, after);
                }

                rebuiltRawSectors = RawMode2Form1SectorIntegrity.RebuildFileRanges(
                    output,
                    layout,
                    WadLba,
                    ExpectedPatches.Select(patch => (patch.WadOffset, ParseHex(patch.AfterHex).Length)).ToArray());
                int verifiedRawSectors = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
                    output,
                    layout,
                    [(ExpectedVisualRawSector, 1), (ExpectedCollisionRawSector, 1)]);
                RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(
                    output,
                    layout,
                    ExpectedVisualRawSector,
                    XaDataSubmode);
                RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(
                    output,
                    layout,
                    ExpectedCollisionRawSector,
                    XaDataSubmode);
                if (rebuiltRawSectors != 2 || verifiedRawSectors != 2)
                    throw new InvalidDataException("The ID65 solid-triangle control did not rebuild exactly two raw sectors.");
                output.Flush(flushToDisk: true);
            }

            Readback readback = VerifyReadback(
                baseImage,
                temporaryImage,
                prepared.BaseLayout,
                cancellationToken);
            string outputImageSha256 = await HashFileAsync(temporaryImage, cancellationToken);
            RequireHash(outputImageSha256, ExpectedOutputImageSha256, "ID65 solid-triangle output BIN");
            RequireHash(readback.OutputDataSha256, ExpectedOutputDataSha256, "ID65 solid-triangle data payload");
            if (readback.ChangedLogicalWadBytes != ExpectedChangedLogicalWadBytes ||
                readback.ChangedPhysicalImageBytes != ExpectedChangedPhysicalImageBytes ||
                readback.ChangedRawSectorCount != 2)
            {
                throw new InvalidDataException(
                    $"The pinned solid-triangle diff changed: logical={readback.ChangedLogicalWadBytes}, physical={readback.ChangedPhysicalImageBytes}, sectors={readback.ChangedRawSectorCount}.");
            }

            string cueText = DiscImage.BuildCueText(baseCue, Path.GetFileName(outputImage));
            await WriteAsciiAtomicallyStagedAsync(temporaryCue, cueText, cancellationToken);
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
            NativeLevelReplacementBaselineExporter.ValidateCue(outputCue, outputImage, "MODE2/2352");

            string baseHashAfter = await HashFileAsync(baseImage, cancellationToken);
            RequireHash(baseHashAfter, BaseImageSha256, "passed display-name base after solid-triangle export");
            TryDelete(backupImage);
            imageBackedUp = false;
            TryDelete(backupCue);
            cueBackedUp = false;

            return new UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateResult(
                outputImage,
                outputCue,
                outputImageSha256,
                readback.OutputDataSha256,
                prepared.Plan,
                readback.ChangedLogicalWadBytes,
                readback.ChangedPhysicalImageBytes,
                rebuiltRawSectors,
                readback.ChangedRawSectorCount,
                readback.RawSectorDiffs,
                ExactLogicalDiffBoundaryVerified: true,
                ExactPhysicalSectorBoundaryVerified: true,
                VertexReadbackVerified: true,
                RawSectorIntegrityVerified: true,
                TerrainCountsPreserved: true,
                TerrainFacePreserved: true,
                UniqueVertexIsolationVerified: true,
                CollisionStructurePreserved: true,
                CollisionTriangleReadbackVerified: true,
                CollisionLookupPreserved: true,
                CollisionAssignmentPreserved: true,
                RetailTownSquarePreserved: true,
                Id65OverlayPreserved: true,
                ExecutablePreserved: true,
                BaseCandidatePreserved: true,
                AtomicRenameCompleted: true);
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
                    "ID65 solid-triangle export failed and prior output recovery was incomplete.",
                    new AggregateException([exportFailure, .. recoveryFailures]));
            }
            throw;
        }
        finally
        {
            TryDelete(temporaryImage);
            TryDelete(temporaryCue);
            if (!imageBackedUp)
                TryDelete(backupImage);
            if (!cueBackedUp)
                TryDelete(backupCue);
        }
    }

    private static async Task<Prepared> PrepareAsync(
        UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string baseImage = RequireExistingFile(request.BaseImagePath, "focused-runtime-passed ID65 display-name BIN");
        string baseCue = RequireExistingFile(request.BaseCuePath, "focused-runtime-passed ID65 display-name CUE");
        string outputImage = Path.GetFullPath(request.OutputImagePath);
        string outputCue = Path.GetFullPath(request.OutputCuePath);
        NativeLevelReplacementBaselineExporter.EnsureDistinctRoles(
            baseImage,
            baseCue,
            outputImage,
            outputCue);
        NativeLevelReplacementBaselineExporter.ValidateCue(baseCue, baseImage, "MODE2/2352");
        string baseImageSha256 = await HashFileAsync(baseImage, cancellationToken);
        RequireHash(baseImageSha256, BaseImageSha256, "focused-runtime-passed ID65 display-name BIN");
        BaseLayout baseLayout = InspectBaseLayout(baseImage);

        UnusedLevel65AuthoredTerrainSolidTriangleControlCandidatePlan plan = new(
            DateTimeOffset.UtcNow,
            ProfileId,
            UnusedLevel65DisplayNameCandidateExporter.ProfileId,
            baseImageSha256,
            BaseExecutableSha256,
            BaseOverlaySha256,
            BaseDataSha256,
            ExpectedOutputDataSha256,
            LevelId,
            ContinuousLevelIndex,
            TargetOverlayWadEntry,
            TargetDataWadEntry,
            TargetOverlayWadOffset,
            TargetOverlayByteLength,
            TargetDataWadOffset,
            TargetDataByteLength,
            RuntimeKey,
            SourceSectorIndex,
            SourceSectorWadOffset,
            SourceFaceWadOffset,
            SourceTextureId,
            new(7827, 6282, 544),
            OriginalFacePoints,
            ExpectedPatches,
            SourceFaceVertexIndexes,
            AffectedFaceIndexes,
            CollisionTriangleIndex,
            CollisionTriangleWadOffset,
            CollisionAssignmentWadOffset,
            ExpectedCollisionLookupReferenceCount,
            ExpectedCollisionLookupWadOffsets,
            CollisionHeaderSha256,
            CollisionBlockTreeSha256,
            CollisionBlocksSha256,
            CollisionAssignmentsSha256,
            CollisionFlagsSha256,
            [ExpectedVisualRawSector, ExpectedCollisionRawSector],
            RuntimeChecklist(),
            CollisionPatched: true,
            CountsAndComponentSizesUnchanged: true,
            RequiresDuckStationRuntimeProof: true);
        return new Prepared(baseLayout, plan);
    }

    private static BaseLayout InspectBaseLayout(string baseImage)
    {
        DiscLayout layout = DiscImage.DetectLayout(baseImage);
        if (layout.SectorSize != RawSectorBytes || layout.UserOffset != UserOffset)
            throw new InvalidDataException("The ID65 solid-triangle control requires the checked MODE2/2352 layout.");
        using FileStream input = File.OpenRead(baseImage);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(
            input,
            layout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord executable = DiscImage.FindRootFileRecord(input, layout, IsExecutableName);
        if (wad.Lba != WadLba || wad.Size != ExpectedWadByteLength)
            throw new InvalidDataException("The passed ID65 WAD extent changed.");
        if (executable.Lba != ExecutableLba || executable.Size != ExecutableByteLength)
            throw new InvalidDataException("The passed ID65 relocated SCUS extent changed.");

        WadEntry retailOverlay = ReadWadEntry(input, layout, 15);
        WadEntry retailData = ReadWadEntry(input, layout, 16);
        WadEntry targetOverlay = ReadWadEntry(input, layout, TargetOverlayWadEntry);
        WadEntry targetData = ReadWadEntry(input, layout, TargetDataWadEntry);
        RequireEntry(retailOverlay, RetailTownSquareOverlayWadOffset, RetailTownSquareOverlayByteLength, "retail Town Square overlay");
        RequireEntry(retailData, RetailTownSquareDataWadOffset, RetailTownSquareDataByteLength, "retail Town Square data");
        RequireEntry(targetOverlay, TargetOverlayWadOffset, TargetOverlayByteLength, "ID65 overlay");
        RequireEntry(targetData, TargetDataWadOffset, TargetDataByteLength, "ID65 data");

        byte[] retailOverlayBytes = DiscImage.ReadFileBytes(input, layout, WadLba, retailOverlay.Offset, retailOverlay.Size);
        byte[] retailDataBytes = DiscImage.ReadFileBytes(input, layout, WadLba, retailData.Offset, retailData.Size);
        byte[] targetOverlayBytes = DiscImage.ReadFileBytes(input, layout, WadLba, targetOverlay.Offset, targetOverlay.Size);
        byte[] targetDataBytes = DiscImage.ReadFileBytes(input, layout, WadLba, targetData.Offset, targetData.Size);
        if (!retailOverlayBytes.SequenceEqual(targetOverlayBytes) || !retailDataBytes.SequenceEqual(targetDataBytes))
            throw new InvalidDataException("The display-name base no longer contains the exact independent Town Square payload clone.");
        RequireHash(Hash(targetOverlayBytes), BaseOverlaySha256, "ID65 overlay payload");
        RequireHash(Hash(targetDataBytes), BaseDataSha256, "ID65 data payload");

        byte[] executableBytes = DiscImage.ReadFileBytes(input, layout, executable.Lba, 0, executable.Size);
        RequireHash(Hash(executableBytes), BaseExecutableSha256, "ID65 display-name executable");
        byte[] sourceSectorHeader = DiscImage.ReadFileBytes(
            input,
            layout,
            WadLba,
            SourceSectorWadOffset,
            SceneSectorHeaderByteLength);
        RequireEqual(sourceSectorHeader, ExpectedSourceSectorHeader, "source sector 213 header");
        RequireEqual(
            DiscImage.ReadFileBytes(input, layout, WadLba, SourceFaceWadOffset, SourceFaceByteLength),
            ExpectedSourceFace,
            "source face 213:64:hp");
        RequireEqual(
            DiscImage.ReadFileBytes(input, layout, WadLba, ProtectedFollowingSectorHeaderWadOffset, SceneSectorHeaderByteLength),
            ExpectedProtectedFollowingSectorHeader,
            "protected following scene-sector header");
        RequireInt32(
            DiscImage.ReadFileBytes(input, layout, WadLba, EnvironmentComponentWadOffset, 4),
            EnvironmentComponentByteLength,
            "environment component length");
        RequireHash(
            Hash(DiscImage.ReadFileBytes(input, layout, WadLba, OcclusionComponentWadOffset, OcclusionComponentByteLength)),
            OcclusionComponentSha256,
            "occlusion component");
        RequireHash(
            Hash(DiscImage.ReadFileBytes(input, layout, WadLba, SpecialSurfaceComponentWadOffset, SpecialSurfaceComponentByteLength)),
            SpecialSurfaceComponentSha256,
            "special-surface component");
        RequireHash(
            Hash(DiscImage.ReadFileBytes(input, layout, WadLba, CollisionComponentWadOffset, CollisionComponentByteLength)),
            CollisionComponentSha256,
            "collision component");
        VerifyCollisionStructureHashes(input, layout, "base");
        VerifyUniqueFaceVertexBinding(input, layout, sourceSectorHeader);
        VerifyCollisionTriangleBinding(input, layout, useAuthoredHeight: false);

        foreach (UnusedLevel65AuthoredTerrainSolidTriangleControlPatch patch in ExpectedPatches)
        {
            byte[] before = ParseHex(patch.BeforeHex);
            byte[] actual = DiscImage.ReadFileBytes(input, layout, WadLba, patch.WadOffset, before.Length);
            RequireEqual(actual, before, $"source {patch.Kind}");
            long patchEnd = checked(patch.WadOffset + before.Length);
            if (patch.WadOffset < TargetDataWadOffset || patchEnd > TargetDataWadOffset + TargetDataByteLength)
                throw new InvalidDataException($"{patch.Kind} escaped ID65 WAD row 80.");
        }

        return new BaseLayout(
            layout,
            wad,
            executable,
            Hash(retailOverlayBytes),
            Hash(retailDataBytes),
            Hash(targetOverlayBytes),
            Hash(executableBytes));
    }

    private static Readback VerifyReadback(
        string baseImagePath,
        string outputImagePath,
        BaseLayout baseLayout,
        CancellationToken cancellationToken)
    {
        DiscLayout outputLayout = DiscImage.DetectLayout(outputImagePath);
        using FileStream baseline = File.OpenRead(baseImagePath);
        using FileStream output = File.OpenRead(outputImagePath);
        if (baseLayout.Layout != outputLayout || baseline.Length != output.Length)
            throw new InvalidDataException("The ID65 solid-triangle control changed the disc layout or raw length.");

        DiscFileRecord outputWad = DiscImage.FindRootFileRecord(
            output,
            outputLayout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord outputExecutable = DiscImage.FindRootFileRecord(output, outputLayout, IsExecutableName);
        if (outputWad != baseLayout.Wad || outputExecutable != baseLayout.Executable)
            throw new InvalidDataException("The ID65 solid-triangle control moved or resized WAD.WAD or SCUS.");

        foreach (UnusedLevel65AuthoredTerrainSolidTriangleControlPatch patch in ExpectedPatches)
        {
            byte[] expected = ParseHex(patch.AfterHex);
            byte[] actual = DiscImage.ReadFileBytes(output, outputLayout, WadLba, patch.WadOffset, expected.Length);
            RequireEqual(actual, expected, $"authored {patch.Kind} readback");
        }
        RequireEqual(
            DiscImage.ReadFileBytes(output, outputLayout, WadLba, SourceSectorWadOffset, SceneSectorHeaderByteLength),
            ExpectedSourceSectorHeader,
            "preserved source sector 213 header");
        RequireEqual(
            DiscImage.ReadFileBytes(output, outputLayout, WadLba, SourceFaceWadOffset, SourceFaceByteLength),
            ExpectedSourceFace,
            "preserved source face 213:64:hp");
        RequireEqual(
            DiscImage.ReadFileBytes(output, outputLayout, WadLba, ProtectedFollowingSectorHeaderWadOffset, SceneSectorHeaderByteLength),
            ExpectedProtectedFollowingSectorHeader,
            "preserved following scene-sector header");
        RequireInt32(
            DiscImage.ReadFileBytes(output, outputLayout, WadLba, EnvironmentComponentWadOffset, 4),
            EnvironmentComponentByteLength,
            "preserved environment component length");
        RequireHash(
            Hash(DiscImage.ReadFileBytes(output, outputLayout, WadLba, OcclusionComponentWadOffset, OcclusionComponentByteLength)),
            OcclusionComponentSha256,
            "preserved occlusion component");
        RequireHash(
            Hash(DiscImage.ReadFileBytes(output, outputLayout, WadLba, SpecialSurfaceComponentWadOffset, SpecialSurfaceComponentByteLength)),
            SpecialSurfaceComponentSha256,
            "preserved special-surface component");
        RequireInt32(
            DiscImage.ReadFileBytes(output, outputLayout, WadLba, CollisionComponentWadOffset, 4),
            CollisionComponentByteLength,
            "preserved collision component length");
        VerifyCollisionStructureHashes(output, outputLayout, "output");
        VerifyCollisionTriangleBinding(output, outputLayout, useAuthoredHeight: true);

        string outputRetailOverlayHash = Hash(DiscImage.ReadFileBytes(
            output,
            outputLayout,
            WadLba,
            RetailTownSquareOverlayWadOffset,
            RetailTownSquareOverlayByteLength));
        string outputRetailDataHash = Hash(DiscImage.ReadFileBytes(
            output,
            outputLayout,
            WadLba,
            RetailTownSquareDataWadOffset,
            RetailTownSquareDataByteLength));
        string outputTargetOverlayHash = Hash(DiscImage.ReadFileBytes(
            output,
            outputLayout,
            WadLba,
            TargetOverlayWadOffset,
            TargetOverlayByteLength));
        byte[] outputTargetData = DiscImage.ReadFileBytes(
            output,
            outputLayout,
            WadLba,
            TargetDataWadOffset,
            TargetDataByteLength);
        string outputExecutableHash = Hash(DiscImage.ReadFileBytes(
            output,
            outputLayout,
            outputExecutable.Lba,
            0,
            outputExecutable.Size));
        RequireHash(outputRetailOverlayHash, baseLayout.RetailOverlaySha256, "retail Town Square overlay readback");
        RequireHash(outputRetailDataHash, baseLayout.RetailDataSha256, "retail Town Square data readback");
        RequireHash(outputTargetOverlayHash, baseLayout.TargetOverlaySha256, "ID65 overlay readback");
        RequireHash(outputExecutableHash, baseLayout.ExecutableSha256, "ID65 executable readback");

        IReadOnlyList<(long Offset, int ByteLength)> allowedRanges = ExpectedPatches
            .Select(patch => (patch.WadOffset, ParseHex(patch.AfterHex).Length))
            .ToArray();
        LogicalDiff logical = CompareLogicalWad(
            baseline,
            output,
            baseLayout.Layout,
            baseLayout.Wad,
            allowedRanges,
            cancellationToken);
        if (logical.OutsideAllowedBytes != 0 || logical.ChangedBytes != ExpectedChangedLogicalWadBytes)
        {
            throw new InvalidDataException(
                $"The solid-triangle logical diff changed {logical.ChangedBytes} byte(s), including {logical.OutsideAllowedBytes} outside the visual word and exact collision triangle.");
        }
        PhysicalDiff physical = ComparePhysicalImages(
            baseline,
            output,
            [ExpectedVisualRawSector, ExpectedCollisionRawSector],
            cancellationToken);
        if (physical.OutsideAllowedSectorBytes != 0 ||
            physical.ChangedBytes != ExpectedChangedPhysicalImageBytes ||
            physical.ChangedRawSectorCount != 2)
        {
            throw new InvalidDataException(
                $"The solid-triangle physical diff changed {physical.ChangedBytes} byte(s), including {physical.OutsideAllowedSectorBytes} outside raw LBAs {ExpectedVisualRawSector} and {ExpectedCollisionRawSector}.");
        }
        UnusedLevel65AuthoredTerrainSolidTriangleControlRawSectorDiff[] expectedRawSectorDiffs =
        [
            new(ExpectedVisualRawSector, 0, 1, 4, 0, 10, 22, 37),
            new(ExpectedCollisionRawSector, 0, 1, 4, 0, 12, 20, 37)
        ];
        if (!physical.RawSectorDiffs.SequenceEqual(expectedRawSectorDiffs))
            throw new InvalidDataException("The exact MODE2 payload/EDC/ECC physical diff breakdown changed.");
        int verifiedRawSectors = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            output,
            outputLayout,
            [(ExpectedVisualRawSector, 1), (ExpectedCollisionRawSector, 1)]);
        RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(
            output,
            outputLayout,
            ExpectedVisualRawSector,
            XaDataSubmode);
        RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(
            output,
            outputLayout,
            ExpectedCollisionRawSector,
            XaDataSubmode);
        if (verifiedRawSectors != 2)
            throw new InvalidDataException("The final solid-triangle raw-sector verification count changed.");

        return new Readback(
            logical.ChangedBytes,
            physical.ChangedBytes,
            physical.ChangedRawSectorCount,
            physical.RawSectorDiffs,
            Hash(outputTargetData));
    }

    private static LogicalDiff CompareLogicalWad(
        FileStream baseline,
        FileStream output,
        DiscLayout layout,
        DiscFileRecord wad,
        IReadOnlyList<(long Offset, int ByteLength)> allowedRanges,
        CancellationToken cancellationToken)
    {
        const int chunkSize = 1 << 20;
        long changed = 0;
        long outside = 0;
        for (long offset = 0; offset < wad.Size; offset += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int length = checked((int)Math.Min(chunkSize, wad.Size - offset));
            byte[] before = DiscImage.ReadFileBytes(baseline, layout, wad.Lba, offset, length);
            byte[] after = DiscImage.ReadFileBytes(output, layout, wad.Lba, offset, length);
            for (int index = 0; index < length; index++)
            {
                if (before[index] == after[index])
                    continue;
                changed++;
                long logicalOffset = offset + index;
                if (!allowedRanges.Any(range =>
                        logicalOffset >= range.Offset &&
                        logicalOffset < range.Offset + range.ByteLength))
                {
                    outside++;
                }
            }
        }
        return new LogicalDiff(changed, outside);
    }

    private static PhysicalDiff ComparePhysicalImages(
        FileStream baseline,
        FileStream output,
        IReadOnlyList<int> allowedRawSectors,
        CancellationToken cancellationToken)
    {
        if (baseline.Length != output.Length || baseline.Length % RawSectorBytes != 0)
            throw new InvalidDataException("The solid-triangle raw diff requires equal complete MODE2/2352 images.");
        HashSet<long> allowed = allowedRawSectors.Select(value => (long)value).ToHashSet();
        const int chunkSize = 1 << 20;
        byte[] before = new byte[chunkSize];
        byte[] after = new byte[chunkSize];
        long changed = 0;
        long outside = 0;
        HashSet<long> changedSectors = [];
        Dictionary<int, int[]> changedBySectorAndRegion = [];
        baseline.Position = 0;
        output.Position = 0;
        for (long offset = 0; offset < baseline.Length; offset += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int length = checked((int)Math.Min(chunkSize, baseline.Length - offset));
            baseline.ReadExactly(before.AsSpan(0, length));
            output.ReadExactly(after.AsSpan(0, length));
            for (int index = 0; index < length; index++)
            {
                if (before[index] == after[index])
                    continue;
                changed++;
                long sector = (offset + index) / RawSectorBytes;
                changedSectors.Add(sector);
                int sectorIndex = checked((int)sector);
                if (!changedBySectorAndRegion.TryGetValue(sectorIndex, out int[]? regionCounts))
                {
                    regionCounts = new int[6];
                    changedBySectorAndRegion[sectorIndex] = regionCounts;
                }
                int withinSector = checked((int)((offset + index) % RawSectorBytes));
                regionCounts[RawSectorRegion(withinSector)]++;
                if (!allowed.Contains(sector))
                    outside++;
            }
        }
        if (!changedSectors.SetEquals(allowed))
            throw new InvalidDataException("The solid-triangle control changed an unexpected raw-sector set.");
        UnusedLevel65AuthoredTerrainSolidTriangleControlRawSectorDiff[] rawSectorDiffs = changedBySectorAndRegion
            .OrderBy(pair => pair.Key)
            .Select(pair => new UnusedLevel65AuthoredTerrainSolidTriangleControlRawSectorDiff(
                pair.Key,
                pair.Value[0],
                pair.Value[1],
                pair.Value[2],
                pair.Value[3],
                pair.Value[4],
                pair.Value[5],
                pair.Value.Sum()))
            .ToArray();
        return new PhysicalDiff(changed, outside, changedSectors.Count, rawSectorDiffs);
    }

    private static int RawSectorRegion(int withinSector) => withinSector switch
    {
        < 24 => 0,
        < 2072 => 1,
        < 2076 => 2,
        < 2084 => 3,
        < 2256 => 4,
        _ => 5
    };

    private static IReadOnlyList<string> RuntimeChecklist() =>
    [
        "Disable every DuckStation cheat and memory-card insertion, then cold boot this exact CUE without resuming a save state.",
        $"From controllable gameplay, press Select to open Inventory; enter {TestLevelWarpPatch.ActivationSequence}; then press Left, then Down. Confirm ID65 still displays TOWN SQUARE and loads normally.",
        "The previous enormous v2 renderer-only ridge is intentionally absent because this v3 starts again from the clean display-name base. Do not look for that ridge: its +512 height exceeds the native collision triangle's unsigned +255 Z-delta encoding, and its edited vertices are shared by six visual faces. Instead, from the entry landing near X 7827, Y 6282, turn approximately 77 degrees right. Find the small green-grass triangle left of and between the two nearest right-side green gems (native objects T24 and T25), centered near X 8115, Y 6351. Its exact footprint is (8056,6317), (8167,6325), (8123,6410). One tip should now rise from Z 512 to Z 576 as a grass slope of roughly 30-37 degrees depending on traversal direction; no other terrain should stretch.",
        "Inspect the authored slope from several ordinary camera angles and confirm native grass texture 5 stays stable. This edits only HP face 213:64 and vertex 95; that vertex is not shared by any other face. Exact source-derived native collision triangle 13853 carries the same +64 height.",
        "Walk slowly up and down the visible slope. Spyro should rise and descend with the rendered surface instead of walking through it or remaining on the old flat Z 512 plane underneath. Then charge across it, jump onto it, and land on it. Report any pass-through, hovering, snag, launch, or fall-through.",
        "Move a short distance away, return to the same triangle, and repeat ordinary movement and camera rotation. Enemies, pause, Inventory, and nearby collision should remain stable. This is an existing-triangle visual/collision coherence control, not an Add Terrain or side-wall test.",
        $"Reset DuckStation, cold boot, and re-enter ID65 with Select; {TestLevelWarpPatch.ActivationSequence}; Left, then Down. Confirm the same solid slope is present again. Do not use Exit Level, Quit Game, Return Home, saving, or a memory card.",
        $"Reset and load original retail Town Square with Select; {TestLevelWarpPatch.ActivationSequence}; Cross, then Triangle. Confirm the authored slope is absent at the same location and retail terrain/collision remain normal.",
        $"Reset and load Gnasty's Loot with Select; {TestLevelWarpPatch.ActivationSequence}; Left, then Right. Confirm it loads normally.",
        $"Reset and load Sunny Flight with Select; {TestLevelWarpPatch.ActivationSequence}; Cross, then Down. Confirm it loads and flight controls remain normal.",
        "Report separately whether the slope is visible, whether walking/charging/jumping/landing follows the visible incline, whether the old flat plane can still be traversed underneath it, whether texture/camera remain stable, and whether all three comparison levels load. A visual slope with matching traversal passes this narrow source-derived native-collision control; it does not yet prove structural Add Terrain.",
        "Do not collect treasure, attack or kill enemies, rescue dragons, touch the egg thief, open or break chests, die, insert a memory card, save, use Return Home, use Exit Level or Quit Game, or run longer than 8 minutes. Face/vertex/triangle counts, sector/component sizes, collision tree/index/assignments, LP terrain, textures, Mobys, music, totals, portals, saving, and persistence remain unchanged."
    ];

    private static void VerifyUniqueFaceVertexBinding(
        FileStream stream,
        DiscLayout layout,
        byte[] sectorHeader)
    {
        const long hpVertexTableWadOffset = 0x6A3EAC4;
        const int hpVertexCount = 140;
        const long hpFaceTableWadOffset = 0x6A3F2BC;
        const int hpFaceCount = 113;
        if (sectorHeader[20] != hpVertexCount || sectorHeader[22] != hpFaceCount)
            throw new InvalidDataException("Source sector 213 HP vertex/face counts changed from 140/113.");

        byte[] faces = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            hpFaceTableWadOffset,
            hpFaceCount * SourceFaceByteLength);
        List<int> references = [];
        for (int faceIndex = 0; faceIndex < hpFaceCount; faceIndex++)
        {
            ReadOnlySpan<byte> slots = faces.AsSpan(faceIndex * SourceFaceByteLength, 4);
            if (slots.Contains((byte)AuthoredVertexIndex))
                references.Add(faceIndex);
        }
        if (!references.SequenceEqual(AffectedFaceIndexes))
        {
            throw new InvalidDataException(
                $"HP vertex {AuthoredVertexIndex} is referenced by face set [{string.Join(',', references)}], expected only face 64.");
        }

        byte[] face = faces.AsSpan(64 * SourceFaceByteLength, SourceFaceByteLength).ToArray();
        RequireEqual(face, ExpectedSourceFace, "isolated source face 213:64:hp");
        if (!face.AsSpan(0, 4).SequenceEqual(SourceFaceVertexIndexes.Select(value => (byte)value).ToArray()) ||
            (BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(8, 4)) & 0x7F) != SourceTextureId)
        {
            throw new InvalidDataException("Source face 213:64 lost its exact [93,93,95,94] slots or texture 5 binding.");
        }

        int[] indexes = [93, 95, 94];
        UnusedLevel65AuthoredTerrainSolidTriangleControlPoint[] expected =
        [
            OriginalFacePoints[0],
            OriginalFacePoints[1],
            OriginalFacePoints[2]
        ];
        for (int index = 0; index < indexes.Length; index++)
        {
            byte[] word = DiscImage.ReadFileBytes(
                stream,
                layout,
                WadLba,
                hpVertexTableWadOffset + (indexes[index] * 4L),
                4);
            UnusedLevel65AuthoredTerrainSolidTriangleControlPoint actual = DecodeSceneVertex(word, sectorHeader);
            if (actual != expected[index])
            {
                throw new InvalidDataException(
                    $"Source HP vertex {indexes[index]} decoded to {actual}, expected {expected[index]}.");
            }
        }
    }

    private static void VerifyCollisionStructureHashes(FileStream stream, DiscLayout layout, string label)
    {
        RequireInt32(
            DiscImage.ReadFileBytes(stream, layout, WadLba, CollisionComponentWadOffset, 4),
            CollisionComponentByteLength,
            $"{label} collision component length");
        byte[] nativeHeader = DiscImage.ReadFileBytes(stream, layout, WadLba, CollisionComponentWadOffset + 4, 0x1C);
        int[] expectedHeaderWords = [CollisionTriangleCount, CollisionFlagsByteLength, 0x1C, 0x6A7C, 0x1E400, 0x58480, 0x5D1E0];
        for (int index = 0; index < expectedHeaderWords.Length; index++)
        {
            int actual = BinaryPrimitives.ReadInt32LittleEndian(nativeHeader.AsSpan(index * 4, 4));
            if (actual != expectedHeaderWords[index])
            {
                throw new InvalidDataException(
                    $"The {label} collision header word {index} is 0x{actual:X}, expected 0x{expectedHeaderWords[index]:X}.");
            }
        }

        RequireHash(Hash(DiscImage.ReadFileBytes(stream, layout, WadLba, CollisionHeaderWadOffset, CollisionHeaderByteLength)), CollisionHeaderSha256, $"{label} collision header");
        RequireHash(Hash(DiscImage.ReadFileBytes(stream, layout, WadLba, CollisionBlockTreeWadOffset, CollisionBlockTreeByteLength)), CollisionBlockTreeSha256, $"{label} collision block tree");
        byte[] blocks = DiscImage.ReadFileBytes(stream, layout, WadLba, CollisionBlocksWadOffset, CollisionBlocksByteLength);
        RequireHash(Hash(blocks), CollisionBlocksSha256, $"{label} collision blocks/index");
        List<long> lookupWadOffsets = [];
        for (int offset = 0; offset + 2 <= blocks.Length; offset += 2)
        {
            if ((BinaryPrimitives.ReadUInt16LittleEndian(blocks.AsSpan(offset, 2)) & 0x7FFF) == CollisionTriangleIndex)
                lookupWadOffsets.Add(CollisionBlocksWadOffset + offset);
        }
        if (!lookupWadOffsets.SequenceEqual(ExpectedCollisionLookupWadOffsets))
        {
            throw new InvalidDataException(
                $"The {label} collision lookup references triangle {CollisionTriangleIndex} at [{string.Join(',', lookupWadOffsets.Select(offset => $"0x{offset:X}"))}], expected 0x6A51682 and 0x6A5191E.");
        }

        RequireHash(Hash(DiscImage.ReadFileBytes(stream, layout, WadLba, CollisionAssignmentsWadOffset, CollisionAssignmentsByteLength)), CollisionAssignmentsSha256, $"{label} collision assignments");
        RequireHash(Hash(DiscImage.ReadFileBytes(stream, layout, WadLba, CollisionFlagsWadOffset, CollisionFlagsByteLength)), CollisionFlagsSha256, $"{label} collision flags");
        byte assignment = DiscImage.ReadFileBytes(stream, layout, WadLba, CollisionAssignmentWadOffset, 1)[0];
        if (assignment != 0)
            throw new InvalidDataException($"The {label} collision assignment for triangle {CollisionTriangleIndex} is {assignment}, expected ordinary surface 0.");
    }

    private static void VerifyCollisionTriangleBinding(
        FileStream stream,
        DiscLayout layout,
        bool useAuthoredHeight)
    {
        UnusedLevel65AuthoredTerrainSolidTriangleControlPatch patch = ExpectedPatches[1];
        byte[] expectedBytes = ParseHex(useAuthoredHeight ? patch.AfterHex : patch.BeforeHex);
        byte[] actualBytes = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            CollisionTriangleWadOffset,
            expectedBytes.Length);
        RequireEqual(actualBytes, expectedBytes, useAuthoredHeight ? "authored collision triangle 13853" : "source collision triangle 13853");

        uint xWord = BinaryPrimitives.ReadUInt32LittleEndian(actualBytes.AsSpan(0, 4));
        uint yWord = BinaryPrimitives.ReadUInt32LittleEndian(actualBytes.AsSpan(4, 4));
        uint zWord = BinaryPrimitives.ReadUInt32LittleEndian(actualBytes.AsSpan(8, 4));
        int x1 = (int)(xWord & 0x3FFF);
        int y1 = (int)(yWord & 0x3FFF);
        int z1 = (int)(zWord & 0x3FFF);
        UnusedLevel65AuthoredTerrainSolidTriangleControlPoint[] points =
        [
            new(x1, y1, z1),
            new(x1 + Signed9((int)((xWord >> 14) & 0x1FF)), y1 + Signed9((int)((yWord >> 14) & 0x1FF)), z1 + (int)((zWord >> 16) & 0xFF)),
            new(x1 + Signed9((int)((xWord >> 23) & 0x1FF)), y1 + Signed9((int)((yWord >> 23) & 0x1FF)), z1 + (int)((zWord >> 24) & 0xFF))
        ];
        UnusedLevel65AuthoredTerrainSolidTriangleControlPoint[] expectedPoints =
        [
            new(8123, 6410, 512),
            new(8167, 6325, useAuthoredHeight ? AuthoredPeakZ : 512),
            new(8056, 6317, 512)
        ];
        if (!points.SequenceEqual(expectedPoints) || (zWord & 0xC000) != 0)
            throw new InvalidDataException("Collision triangle 13853 no longer decodes to the exact face-64 footprint, height, and flags.");
        if (points.Any(point => (point.X >> 8) != 31 || (point.Y >> 8) is < 24 or > 25 || (point.Z >> 8) != 2))
            throw new InvalidDataException("The authored collision triangle escaped its original X31/Y24-25/Z2 lookup cells.");
    }

    private static UnusedLevel65AuthoredTerrainSolidTriangleControlPoint DecodeSceneVertex(
        byte[] vertexWord,
        byte[] sectorHeader)
    {
        uint word = BinaryPrimitives.ReadUInt32LittleEndian(vertexWord);
        uint xyPos = BinaryPrimitives.ReadUInt32LittleEndian(sectorHeader.AsSpan(8, 4));
        uint zPos = BinaryPrimitives.ReadUInt32LittleEndian(sectorHeader.AsSpan(12, 4));
        int sectorX = (int)((xyPos >> 16) & 0xFFFF);
        int sectorY = (int)(xyPos & 0xFFFF);
        int sectorZ = (int)((zPos >> 14) & 0xFFFF) >> 2;
        int x = sectorX + (int)(((word >> 19) & 0x1FFC) >> 2);
        int y = sectorY + (int)(((word >> 8) & 0x1FFC) >> 2);
        int z = sectorZ + (int)(((word << 3) & 0x1FFC) >> 3);
        if (((BinaryPrimitives.ReadUInt16LittleEndian(sectorHeader.AsSpan(4, 2)) >> 12) & 1) == 1)
            z >>= 3;
        return new(x, y, z);
    }

    private static int Signed9(int value) => (value & 0x100) != 0 ? value - 0x200 : value;

    private static WadEntry ReadWadEntry(FileStream stream, DiscLayout layout, int index)
    {
        byte[] row = DiscImage.ReadFileBytes(stream, layout, WadLba, index * 8L, 8);
        return new WadEntry(
            BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(0, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(4, 4)));
    }

    private static void RequireEntry(WadEntry entry, long offset, int size, string label)
    {
        if (entry.Offset != offset || entry.Size != size)
        {
            throw new InvalidDataException(
                $"The {label} WAD row is 0x{entry.Offset:X}+0x{entry.Size:X}, expected 0x{offset:X}+0x{size:X}.");
        }
    }

    private static void RequireInt32(byte[] bytes, int expected, string label)
    {
        if (bytes.Length != sizeof(int) || BinaryPrimitives.ReadInt32LittleEndian(bytes) != expected)
            throw new InvalidDataException($"The {label} changed from 0x{expected:X}.");
    }

    private static void RequireEqual(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected, string label)
    {
        if (!actual.SequenceEqual(expected))
            throw new InvalidDataException($"The {label} bytes changed.");
    }

    private static bool IsExecutableName(string name) =>
        name.StartsWith("SCUS", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith(".EXE", StringComparison.OrdinalIgnoreCase);

    private static string RequireExistingFile(string path, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"The {label} does not exist.", fullPath);
        return fullPath;
    }

    private static byte[] ParseHex(string value) =>
        Convert.FromHexString(new string(value.Where(char.IsAsciiHexDigit).ToArray()));

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {label} SHA-256 is {actual}, expected {expected}.");
    }

    private static bool PathEquals(string? left, string? right) =>
        string.Equals(
            string.IsNullOrWhiteSpace(left) ? "" : Path.GetFullPath(left),
            string.IsNullOrWhiteSpace(right) ? "" : Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static async Task WriteAsciiAtomicallyStagedAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(content);
        await using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    private static void TryDelete(string path)
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

    private sealed record Prepared(
        BaseLayout BaseLayout,
        UnusedLevel65AuthoredTerrainSolidTriangleControlCandidatePlan Plan);
    private sealed record BaseLayout(
        DiscLayout Layout,
        DiscFileRecord Wad,
        DiscFileRecord Executable,
        string RetailOverlaySha256,
        string RetailDataSha256,
        string TargetOverlaySha256,
        string ExecutableSha256);
    private sealed record WadEntry(int Offset, int Size);
    private sealed record LogicalDiff(long ChangedBytes, long OutsideAllowedBytes);
    private sealed record PhysicalDiff(
        long ChangedBytes,
        long OutsideAllowedSectorBytes,
        int ChangedRawSectorCount,
        IReadOnlyList<UnusedLevel65AuthoredTerrainSolidTriangleControlRawSectorDiff> RawSectorDiffs);
    private sealed record Readback(
        long ChangedLogicalWadBytes,
        long ChangedPhysicalImageBytes,
        int ChangedRawSectorCount,
        IReadOnlyList<UnusedLevel65AuthoredTerrainSolidTriangleControlRawSectorDiff> RawSectorDiffs,
        string OutputDataSha256);
}

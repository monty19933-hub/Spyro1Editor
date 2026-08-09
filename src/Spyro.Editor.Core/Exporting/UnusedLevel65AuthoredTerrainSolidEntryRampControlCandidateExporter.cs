using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Exporting;

public sealed record UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateRequest(
    string BaseImagePath,
    string BaseCuePath,
    string OutputImagePath,
    string OutputCuePath,
    Action<string>? TestFaultInjector = null);

public readonly record struct UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint(
    int X,
    int Y,
    int Z);

public sealed record UnusedLevel65AuthoredTerrainSolidEntryRampControlPatch(
    string Kind,
    int VertexIndex,
    int CollisionTriangleIndex,
    long WadOffset,
    string BeforeHex,
    string AfterHex);

public sealed record UnusedLevel65AuthoredTerrainSolidEntryRampControlRawSectorDiff(
    int RawSectorLba,
    int HeaderChangedBytes,
    int PayloadChangedBytes,
    int EdcChangedBytes,
    int ReservedChangedBytes,
    int EccPChangedBytes,
    int EccQChangedBytes,
    int TotalChangedBytes);

public sealed record UnusedLevel65AuthoredTerrainSolidEntryRampControlCollisionBinding(
    int TriangleIndex,
    IReadOnlyList<UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint> SourcePoints,
    IReadOnlyList<UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint> AuthoredPoints,
    bool CyclicWindingPreserved,
    int MinXBlock,
    int MaxXBlock,
    int MinYBlock,
    int MaxYBlock,
    int MinZBlock,
    int MaxZBlock,
    IReadOnlyList<long> LookupWadOffsets,
    int Assignment,
    int Flags);

public sealed record UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidatePlan(
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
    UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint SpawnPoint,
    IReadOnlyList<UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint> OriginalFacePoints,
    IReadOnlyList<UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint> AuthoredFacePoints,
    IReadOnlyList<int> AuthoredVertexIndexes,
    IReadOnlyList<int> AffectedFaceIndexes,
    IReadOnlyList<int> CollisionTriangleIndexes,
    IReadOnlyList<UnusedLevel65AuthoredTerrainSolidEntryRampControlCollisionBinding> CollisionBindings,
    IReadOnlyList<UnusedLevel65AuthoredTerrainSolidEntryRampControlPatch> Patches,
    IReadOnlyList<int> AffectedRawSectorLbas,
    IReadOnlyList<string> VisualFootprintIntersections,
    string CollisionHeaderSha256,
    string CollisionBlockTreeSha256,
    string CollisionBlocksSha256,
    string CollisionAssignmentsSha256,
    string CollisionFlagsSha256,
    IReadOnlyList<string> RuntimeChecklist,
    bool CountsAndComponentSizesUnchanged,
    bool CollisionIndexRebuildRequired,
    bool RequiresDuckStationRuntimeProof);

public sealed record UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    string OutputDataSha256,
    UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidatePlan Plan,
    long ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    IReadOnlyList<UnusedLevel65AuthoredTerrainSolidEntryRampControlRawSectorDiff> RawSectorDiffs,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool VertexReadbackVerified,
    bool TerrainFaceBindingsVerified,
    bool VisualFootprintExposureVerified,
    bool CollisionTriangleReadbackVerified,
    bool CollisionFootprintExposureVerified,
    bool CollisionStructurePreserved,
    bool CollisionAssignmentsPreserved,
    bool RawSectorIntegrityVerified,
    bool RetailTownSquarePreserved,
    bool Id65OverlayPreserved,
    bool ExecutablePreserved,
    bool BaseCandidatePreserved,
    bool AtomicRenameCompleted);

/// <summary>
/// Disposable ID65 collision control at the already runtime-visible entry ridge.
/// It raises the far edge of existing HP face 213:37 by 96 units and rewrites
/// every source collision triangle containing either moved point. No count,
/// sector/component length, collision lookup, assignment, or retail row changes.
/// </summary>
public static class UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter
{
    public const string ProfileId =
        "unused-level-65-town-square-authored-terrain-solid-entry-ramp-control-clean-usa-disposable-v4";

    public const string ExpectedOutputImageSha256 =
        "6f63a7645c0ed07ad21cc884856c52a5df598f5fa81c31441d342f44d5e16f0c";
    public const string ExpectedOutputDataSha256 =
        "90a08be221dedcecfefdf3fb9640e082cdb6b06a98a8edabb6c7410c1116288e";

    public const int LevelId = 65;
    public const int ContinuousLevelIndex = 35;
    public const int TargetOverlayWadEntry = 79;
    public const int TargetDataWadEntry = 80;
    public const long TargetOverlayWadOffset = 0x6927000;
    public const int TargetOverlayByteLength = 0xF800;
    public const long TargetDataWadOffset = 0x6936800;
    public const int TargetDataByteLength = 0x2E2000;
    public const string RuntimeKey = "213:37:hp";
    public const int SourceSectorIndex = 213;
    public const long SourceSectorWadOffset = 0x6A3E8B4;
    public const long SourceFaceWadOffset = 0x6A3F50C;
    public const int SourceTextureId = 28;
    public const int AuthoredRidgeZ = 608;
    public const int AuthoredHeightDelta = 96;

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
    private const long ProtectedFollowingSectorHeaderWadOffset = 0x6A3C4F8;
    private const long SceneSectorChainWadOffset = 0x6A182E0;
    private const long SceneSectorChainEndWadOffset = OcclusionComponentWadOffset;
    private const int SceneSectorCount = 216;
    private const int SceneSectorHeaderByteLength = 28;
    private const int SourceFaceByteLength = 16;
    private const int WadLba = 37;
    private const int ExpectedWadByteLength = 0x6C18800;
    private const int ExecutableLba = 55382;
    private const int ExecutableByteLength = 0x66000;
    private const int RawSectorBytes = 2352;
    private const int UserOffset = 24;
    private const int ExpectedVisualRawSector = 54434;
    private const int ExpectedCollisionRawSector = 54507;
    private const long ExpectedChangedLogicalWadBytes = 42;
    private const long ExpectedChangedPhysicalImageBytes = 267;
    private const byte XaDataSubmode = 0x08;

    private static readonly byte[] ExpectedSourceSectorHeader = Convert.FromHexString(
        "101A091FDC028F025417031D1409E001233015008CB9717DFFFFFFFF");
    private static readonly byte[] ExpectedSourceFace = Convert.FromHexString(
        "283930272D3D352D1C0680FF08F4CFFF");
    private static readonly byte[] ExpectedProtectedFollowingSectorHeader = Convert.FromHexString(
        "130B04257B41E001000A0024FFFFE0010401010000000007FFFFFFFF");
    private static readonly UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint[] OriginalFacePoints =
    [
        new(7762, 6346, 512),
        new(7890, 6346, 512),
        new(7890, 6474, 512),
        new(7762, 6474, 512)
    ];
    private static readonly UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint[] AuthoredFacePoints =
    [
        new(7762, 6346, 512),
        new(7890, 6346, 512),
        new(7890, 6474, AuthoredRidgeZ),
        new(7762, 6474, AuthoredRidgeZ)
    ];
    private static readonly int[] AuthoredVertexIndexes = [39, 48];
    private static readonly int[] AffectedFaceIndexes = [25, 26, 29, 37, 38, 43];
    private static readonly int[] CollisionTriangleIndexes =
        [1353, 1354, 1360, 1361, 1363, 1369, 1370, 1395, 1400, 1401];
    private static readonly string[] ExpectedVisualFootprintIntersections = ["213:37"];
    private static readonly CollisionBindingExpectation[] CollisionBindingExpectations =
    [
        new(1353, 30, 30, 24, 25, 2, 2, [0x6A51672, 0x6A51908]),
        new(1354, 30, 30, 24, 25, 2, 2, [0x6A51670, 0x6A51906]),
        new(1360, 29, 30, 25, 25, 2, 2, [0x6A518E2, 0x6A51904]),
        new(1361, 29, 30, 25, 25, 2, 2, [0x6A518E0, 0x6A51902]),
        new(1363, 29, 30, 24, 25, 2, 2, [0x6A5166A, 0x6A518DC, 0x6A518FE]),
        new(1369, 30, 30, 25, 25, 2, 2, [0x6A518F8]),
        new(1370, 30, 30, 25, 25, 2, 2, [0x6A518F6]),
        new(1395, 30, 31, 25, 25, 2, 2, [0x6A518EC, 0x6A51924]),
        new(1400, 30, 31, 24, 25, 2, 2, [0x6A51660, 0x6A51694, 0x6A518EA, 0x6A51922]),
        new(1401, 30, 31, 24, 25, 2, 2, [0x6A51692, 0x6A518E8, 0x6A51920])
    ];

    private static readonly UnusedLevel65AuthoredTerrainSolidEntryRampControlPatch[] ExpectedPatches =
    [
        new("visual-hp", 39, -1, 0x6A3EB60,
            "20 D8 E7 29", "80 D8 E7 29"),
        new("visual-hp", 48, -1, 0x6A3EB84,
            "20 D8 E7 39", "80 D8 E7 39"),
        new("collision-triangle", -1, 1353, 0x6A63130,
            "52 1E 20 00 4A 19 60 C0 00 02 00 00",
            "D2 1E 60 C0 CA 18 00 40 00 02 00 60"),
        new("collision-triangle", -1, 1354, 0x6A6313C,
            "52 1E 20 40 4A 19 00 C0 00 02 00 00",
            "D2 1E 60 00 CA 18 20 40 00 02 60 60"),
        new("collision-triangle", -1, 1360, 0x6A63184,
            "D2 1D 20 00 CA 19 60 C0 00 02 00 00",
            "D2 1D 20 00 CA 19 60 C0 00 02 60 00"),
        new("collision-triangle", -1, 1361, 0x6A63190,
            "D2 1D 20 40 CA 19 00 C0 00 02 00 00",
            "D2 1D 20 40 CA 19 00 C0 00 02 00 60"),
        new("collision-triangle", -1, 1363, 0x6A631A8,
            "D2 1D 20 40 4A 19 00 C0 00 02 00 00",
            "D2 1D 20 40 4A 19 00 C0 00 02 60 00"),
        new("collision-triangle", -1, 1369, 0x6A631F0,
            "52 1E 20 00 CA 19 60 C0 00 02 00 00",
            "52 1E 20 00 CA 19 60 C0 00 02 60 60"),
        new("collision-triangle", -1, 1370, 0x6A631FC,
            "52 1E 20 40 CA 19 00 C0 00 02 00 00",
            "52 1E 20 40 CA 19 00 C0 00 02 00 60"),
        new("collision-triangle", -1, 1395, 0x6A63328,
            "D2 1E 20 00 CA 19 60 C0 00 02 00 00",
            "D2 1E 20 00 CA 19 60 C0 00 02 00 60"),
        new("collision-triangle", -1, 1400, 0x6A63364,
            "D2 1E 20 00 4A 19 60 C0 00 02 00 00",
            "52 1F 60 C0 CA 18 00 40 00 02 00 60"),
        new("collision-triangle", -1, 1401, 0x6A63370,
            "D2 1E 20 40 4A 19 00 C0 00 02 00 00",
            "52 1F 00 C0 4A 19 60 00 00 02 00 60")
    ];
    private static readonly UnusedLevel65AuthoredTerrainSolidEntryRampControlRawSectorDiff[]
        ExpectedRawSectorDiffs =
    [
        new(54434, 0, 2, 4, 0, 12, 26, 44),
        new(54507, 0, 40, 4, 0, 88, 91, 223)
    ];

    public static async Task<UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidatePlan> BuildPlanAsync(
        UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateRequest request,
        CancellationToken cancellationToken = default) =>
        (await PrepareAsync(request, cancellationToken)).Plan;

    public static async Task<UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateResult> ExportAsync(
        UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        Prepared prepared = await PrepareAsync(request, cancellationToken);
        string baseImage = Path.GetFullPath(request.BaseImagePath);
        string baseCue = Path.GetFullPath(request.BaseCuePath);
        string outputImage = Path.GetFullPath(request.OutputImagePath);
        string outputCue = Path.GetFullPath(request.OutputCuePath);
        NativeLevelReplacementBaselineExporter.EnsureDistinctRoles(baseImage, baseCue, outputImage, outputCue);
        if (!PathEquals(Path.GetDirectoryName(outputImage), Path.GetDirectoryName(outputCue)))
            throw new InvalidOperationException("The solid-entry-ramp BIN and CUE must share one directory.");

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
            await DiscImageWorkingCopy.StageAsync(baseImage, temporaryImage, false, cancellationToken);
            DiscLayout layout = DiscImage.DetectLayout(temporaryImage);
            int rebuiltRawSectors;
            await using (FileStream output = new(
                             temporaryImage,
                             FileMode.Open,
                             FileAccess.ReadWrite,
                             FileShare.None))
            {
                foreach (UnusedLevel65AuthoredTerrainSolidEntryRampControlPatch patch in ExpectedPatches)
                {
                    byte[] before = ParseHex(patch.BeforeHex);
                    byte[] after = ParseHex(patch.AfterHex);
                    RequireEqual(
                        DiscImage.ReadFileBytes(output, layout, WadLba, patch.WadOffset, before.Length),
                        before,
                        $"staged {patch.Kind} {PatchLabel(patch)} preimage");
                    DiscImage.WriteFileBytes(output, layout, WadLba, patch.WadOffset, after);
                }

                rebuiltRawSectors = RawMode2Form1SectorIntegrity.RebuildFileRanges(
                    output,
                    layout,
                    WadLba,
                    ExpectedPatches.Select(patch => (patch.WadOffset, ParseHex(patch.AfterHex).Length)).ToArray());
                int verified = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
                    output,
                    layout,
                    [(ExpectedVisualRawSector, 1), (ExpectedCollisionRawSector, 1)]);
                RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(output, layout, ExpectedVisualRawSector, XaDataSubmode);
                RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(output, layout, ExpectedCollisionRawSector, XaDataSubmode);
                if (rebuiltRawSectors != 2 || verified != 2)
                    throw new InvalidDataException("The solid-entry-ramp control did not rebuild exactly two raw sectors.");
                output.Flush(flushToDisk: true);
            }

            Readback readback = VerifyReadback(baseImage, temporaryImage, prepared.BaseLayout, cancellationToken);
            string outputImageSha256 = await HashFileAsync(temporaryImage, cancellationToken);
            RequirePinnedHash(outputImageSha256, ExpectedOutputImageSha256, "solid-entry-ramp output BIN");
            RequirePinnedHash(readback.OutputDataSha256, ExpectedOutputDataSha256, "solid-entry-ramp ID65 data");
            if (readback.ChangedLogicalWadBytes != ExpectedChangedLogicalWadBytes ||
                readback.ChangedPhysicalImageBytes != ExpectedChangedPhysicalImageBytes ||
                readback.ChangedRawSectorCount != 2 ||
                !readback.RawSectorDiffs.SequenceEqual(ExpectedRawSectorDiffs))
            {
                throw new InvalidDataException(
                    $"The solid-entry-ramp diff changed: logical={readback.ChangedLogicalWadBytes}, physical={readback.ChangedPhysicalImageBytes}, sectors={readback.ChangedRawSectorCount}.");
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
            request.TestFaultInjector?.Invoke("after-image-publication");
            File.Move(temporaryCue, outputCue);
            cuePublished = true;
            NativeLevelReplacementBaselineExporter.ValidateCue(outputCue, outputImage, "MODE2/2352");

            RequireHash(await HashFileAsync(baseImage, cancellationToken), BaseImageSha256, "display-name base after export");
            TryDelete(backupImage);
            imageBackedUp = false;
            TryDelete(backupCue);
            cueBackedUp = false;

            return new(
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
                TerrainFaceBindingsVerified: true,
                VisualFootprintExposureVerified: true,
                CollisionTriangleReadbackVerified: true,
                CollisionFootprintExposureVerified: true,
                CollisionStructurePreserved: true,
                CollisionAssignmentsPreserved: true,
                RawSectorIntegrityVerified: true,
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
                "BIN", backupImage, outputImage, ref imageBackedUp);
            Exception? cueRecovery = NativeLevelReplacementBaselineExporter.TryRestoreBackup(
                "CUE", backupCue, outputCue, ref cueBackedUp);
            if (imageRecovery != null)
                recoveryFailures.Add(imageRecovery);
            if (cueRecovery != null)
                recoveryFailures.Add(cueRecovery);
            if (recoveryFailures.Count > 0)
            {
                throw new IOException(
                    "Solid-entry-ramp export failed and prior output recovery was incomplete.",
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
        UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string baseImage = RequireExistingFile(request.BaseImagePath, "focused-runtime-passed display-name BIN");
        string baseCue = RequireExistingFile(request.BaseCuePath, "focused-runtime-passed display-name CUE");
        NativeLevelReplacementBaselineExporter.EnsureDistinctRoles(
            baseImage,
            baseCue,
            Path.GetFullPath(request.OutputImagePath),
            Path.GetFullPath(request.OutputCuePath));
        NativeLevelReplacementBaselineExporter.ValidateCue(baseCue, baseImage, "MODE2/2352");
        string baseImageHash = await HashFileAsync(baseImage, cancellationToken);
        RequireHash(baseImageHash, BaseImageSha256, "focused-runtime-passed display-name BIN");
        BaseLayout baseLayout = InspectBase(baseImage);
        UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidatePlan plan = new(
            DateTimeOffset.UtcNow,
            ProfileId,
            UnusedLevel65DisplayNameCandidateExporter.ProfileId,
            baseImageHash,
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
            AuthoredFacePoints,
            AuthoredVertexIndexes,
            AffectedFaceIndexes,
            CollisionTriangleIndexes,
            BuildPublicCollisionBindings(),
            ExpectedPatches,
            [ExpectedVisualRawSector, ExpectedCollisionRawSector],
            ExpectedVisualFootprintIntersections,
            CollisionHeaderSha256,
            CollisionBlockTreeSha256,
            CollisionBlocksSha256,
            CollisionAssignmentsSha256,
            CollisionFlagsSha256,
            RuntimeChecklist(),
            true,
            false,
            true);
        return new(baseLayout, plan);
    }

    private static BaseLayout InspectBase(string baseImage)
    {
        DiscLayout layout = DiscImage.DetectLayout(baseImage);
        if (layout.SectorSize != RawSectorBytes || layout.UserOffset != UserOffset)
            throw new InvalidDataException("The solid-entry-ramp control requires MODE2/2352 with user offset 24.");
        using FileStream input = File.OpenRead(baseImage);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(
            input, layout, name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord executable = DiscImage.FindRootFileRecord(input, layout, IsExecutableName);
        if (wad.Lba != WadLba || wad.Size != ExpectedWadByteLength)
            throw new InvalidDataException("The passed ID65 WAD extent changed.");
        if (executable.Lba != ExecutableLba || executable.Size != ExecutableByteLength)
            throw new InvalidDataException("The passed ID65 executable extent changed.");

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
            throw new InvalidDataException("The base no longer contains the exact physically independent Town Square clone.");
        RequireHash(Hash(targetOverlayBytes), BaseOverlaySha256, "ID65 overlay payload");
        RequireHash(Hash(targetDataBytes), BaseDataSha256, "ID65 data payload");
        byte[] executableBytes = DiscImage.ReadFileBytes(input, layout, executable.Lba, 0, executable.Size);
        RequireHash(Hash(executableBytes), BaseExecutableSha256, "ID65 executable");

        VerifyProtectedStructures(input, layout, "base");
        VerifyTerrainBindings(input, layout, authored: false);
        VerifyCollisionBindings(input, layout, authored: false);
        foreach (UnusedLevel65AuthoredTerrainSolidEntryRampControlPatch patch in ExpectedPatches)
        {
            byte[] before = ParseHex(patch.BeforeHex);
            RequireEqual(
                DiscImage.ReadFileBytes(input, layout, WadLba, patch.WadOffset, before.Length),
                before,
                $"source {patch.Kind} {PatchLabel(patch)}");
            if (patch.WadOffset < TargetDataWadOffset ||
                patch.WadOffset + before.Length > TargetDataWadOffset + TargetDataByteLength)
            {
                throw new InvalidDataException($"Patch {PatchLabel(patch)} escaped ID65 WAD row 80.");
            }
        }

        return new(
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
        if (outputLayout != baseLayout.Layout || baseline.Length != output.Length)
            throw new InvalidDataException("The solid-entry-ramp control changed the disc layout or length.");
        DiscFileRecord outputWad = DiscImage.FindRootFileRecord(
            output, outputLayout, name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord outputExecutable = DiscImage.FindRootFileRecord(output, outputLayout, IsExecutableName);
        if (outputWad != baseLayout.Wad || outputExecutable != baseLayout.Executable)
            throw new InvalidDataException("The solid-entry-ramp control moved WAD.WAD or SCUS.");

        foreach (UnusedLevel65AuthoredTerrainSolidEntryRampControlPatch patch in ExpectedPatches)
        {
            byte[] expected = ParseHex(patch.AfterHex);
            RequireEqual(
                DiscImage.ReadFileBytes(output, outputLayout, WadLba, patch.WadOffset, expected.Length),
                expected,
                $"authored {patch.Kind} {PatchLabel(patch)} readback");
        }
        VerifyProtectedStructures(output, outputLayout, "output");
        VerifyTerrainBindings(output, outputLayout, authored: true);
        VerifyCollisionBindings(output, outputLayout, authored: true);

        RequireHash(
            Hash(DiscImage.ReadFileBytes(output, outputLayout, WadLba, RetailTownSquareOverlayWadOffset, RetailTownSquareOverlayByteLength)),
            baseLayout.RetailOverlaySha256,
            "retail Town Square overlay readback");
        RequireHash(
            Hash(DiscImage.ReadFileBytes(output, outputLayout, WadLba, RetailTownSquareDataWadOffset, RetailTownSquareDataByteLength)),
            baseLayout.RetailDataSha256,
            "retail Town Square data readback");
        RequireHash(
            Hash(DiscImage.ReadFileBytes(output, outputLayout, WadLba, TargetOverlayWadOffset, TargetOverlayByteLength)),
            baseLayout.TargetOverlaySha256,
            "ID65 overlay readback");
        byte[] outputData = DiscImage.ReadFileBytes(
            output, outputLayout, WadLba, TargetDataWadOffset, TargetDataByteLength);
        RequireHash(
            Hash(DiscImage.ReadFileBytes(output, outputLayout, outputExecutable.Lba, 0, outputExecutable.Size)),
            baseLayout.ExecutableSha256,
            "ID65 executable readback");

        IReadOnlyList<(long Offset, int ByteLength)> allowedRanges = ExpectedPatches
            .Select(patch => (patch.WadOffset, ParseHex(patch.AfterHex).Length))
            .ToArray();
        LogicalDiff logical = CompareLogicalWad(
            baseline, output, baseLayout.Layout, baseLayout.Wad, allowedRanges, cancellationToken);
        if (logical.OutsideAllowedBytes != 0 || logical.ChangedBytes != ExpectedChangedLogicalWadBytes)
        {
            throw new InvalidDataException(
                $"The solid-entry-ramp logical diff changed {logical.ChangedBytes} bytes, including {logical.OutsideAllowedBytes} outside its exact patches.");
        }
        PhysicalDiff physical = ComparePhysicalImages(
            baseline,
            output,
            [ExpectedVisualRawSector, ExpectedCollisionRawSector],
            cancellationToken);
        int verified = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            output,
            outputLayout,
            [(ExpectedVisualRawSector, 1), (ExpectedCollisionRawSector, 1)]);
        RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(output, outputLayout, ExpectedVisualRawSector, XaDataSubmode);
        RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(output, outputLayout, ExpectedCollisionRawSector, XaDataSubmode);
        if (verified != 2)
            throw new InvalidDataException("The final solid-entry-ramp raw-sector verification count changed.");
        return new(
            logical.ChangedBytes,
            physical.ChangedBytes,
            physical.ChangedRawSectorCount,
            physical.RawSectorDiffs,
            Hash(outputData));
    }

    private static void VerifyProtectedStructures(FileStream stream, DiscLayout layout, string label)
    {
        RequireEqual(
            DiscImage.ReadFileBytes(stream, layout, WadLba, SourceSectorWadOffset, SceneSectorHeaderByteLength),
            ExpectedSourceSectorHeader,
            $"{label} source sector 213 header");
        RequireEqual(
            DiscImage.ReadFileBytes(stream, layout, WadLba, SourceFaceWadOffset, SourceFaceByteLength),
            ExpectedSourceFace,
            $"{label} source face 213:37");
        RequireEqual(
            DiscImage.ReadFileBytes(stream, layout, WadLba, ProtectedFollowingSectorHeaderWadOffset, SceneSectorHeaderByteLength),
            ExpectedProtectedFollowingSectorHeader,
            $"{label} protected sector-202 header");
        RequireInt32(
            DiscImage.ReadFileBytes(stream, layout, WadLba, EnvironmentComponentWadOffset, 4),
            EnvironmentComponentByteLength,
            $"{label} environment component length");
        RequireHash(
            Hash(DiscImage.ReadFileBytes(stream, layout, WadLba, OcclusionComponentWadOffset, OcclusionComponentByteLength)),
            OcclusionComponentSha256,
            $"{label} occlusion component");
        RequireHash(
            Hash(DiscImage.ReadFileBytes(stream, layout, WadLba, SpecialSurfaceComponentWadOffset, SpecialSurfaceComponentByteLength)),
            SpecialSurfaceComponentSha256,
            $"{label} special-surface component");
        RequireInt32(
            DiscImage.ReadFileBytes(stream, layout, WadLba, CollisionComponentWadOffset, 4),
            CollisionComponentByteLength,
            $"{label} collision component length");
        RequireHash(
            Hash(DiscImage.ReadFileBytes(stream, layout, WadLba, CollisionHeaderWadOffset, CollisionHeaderByteLength)),
            CollisionHeaderSha256,
            $"{label} collision header");
        RequireHash(
            Hash(DiscImage.ReadFileBytes(stream, layout, WadLba, CollisionBlockTreeWadOffset, CollisionBlockTreeByteLength)),
            CollisionBlockTreeSha256,
            $"{label} collision block tree");
        RequireHash(
            Hash(DiscImage.ReadFileBytes(stream, layout, WadLba, CollisionBlocksWadOffset, CollisionBlocksByteLength)),
            CollisionBlocksSha256,
            $"{label} collision blocks/index");
        RequireHash(
            Hash(DiscImage.ReadFileBytes(stream, layout, WadLba, CollisionAssignmentsWadOffset, CollisionAssignmentsByteLength)),
            CollisionAssignmentsSha256,
            $"{label} collision assignments");
        RequireHash(
            Hash(DiscImage.ReadFileBytes(stream, layout, WadLba, CollisionFlagsWadOffset, CollisionFlagsByteLength)),
            CollisionFlagsSha256,
            $"{label} collision flags");
    }

    private static void VerifyTerrainBindings(FileStream stream, DiscLayout layout, bool authored)
    {
        const long hpVertexTableWadOffset = 0x6A3EAC4;
        const long hpFaceTableWadOffset = 0x6A3F2BC;
        const int hpVertexCount = 140;
        const int hpFaceCount = 113;
        byte[] header = DiscImage.ReadFileBytes(
            stream, layout, WadLba, SourceSectorWadOffset, SceneSectorHeaderByteLength);
        if (header[20] != hpVertexCount || header[22] != hpFaceCount)
            throw new InvalidDataException("Sector 213 HP counts changed from 140 vertices / 113 faces.");
        byte[] faces = DiscImage.ReadFileBytes(
            stream, layout, WadLba, hpFaceTableWadOffset, hpFaceCount * SourceFaceByteLength);
        int[] refs39 = ReferencingFaces(faces, 39);
        int[] refs48 = ReferencingFaces(faces, 48);
        if (!refs39.SequenceEqual(new[] { 25, 26, 29, 37 }) ||
            !refs48.SequenceEqual(new[] { 29, 37, 38, 43 }) ||
            !refs39.Concat(refs48).Distinct().Order().SequenceEqual(AffectedFaceIndexes))
        {
            throw new InvalidDataException("The exact six-face shared-vertex binding for v39/v48 changed.");
        }
        RequireEqual(
            faces.AsSpan(37 * SourceFaceByteLength, SourceFaceByteLength).ToArray(),
            ExpectedSourceFace,
            "face 213:37 binding");
        int[] indexes = [40, 57, 48, 39];
        IReadOnlyList<UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint> expected =
            authored ? AuthoredFacePoints : OriginalFacePoints;
        for (int corner = 0; corner < indexes.Length; corner++)
        {
            byte[] word = DiscImage.ReadFileBytes(
                stream, layout, WadLba, hpVertexTableWadOffset + (indexes[corner] * 4L), 4);
            UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint point = DecodeSceneVertex(word, header);
            if (point != expected[corner])
            {
                throw new InvalidDataException(
                    $"HP vertex {indexes[corner]} decoded to {point}, expected {expected[corner]}.");
            }
        }
        VerifyVisualFootprintExposure(stream, layout);
    }

    private static void VerifyVisualFootprintExposure(FileStream stream, DiscLayout layout)
    {
        int chainLength = checked((int)(SceneSectorChainEndWadOffset - SceneSectorChainWadOffset));
        byte[] chain = DiscImage.ReadFileBytes(
            stream, layout, WadLba, SceneSectorChainWadOffset, chainLength);
        List<string> intersections = [];
        int offset = 0;
        for (int sectorIndex = 0; sectorIndex < SceneSectorCount; sectorIndex++)
        {
            if (offset < 0 || offset + SceneSectorHeaderByteLength > chain.Length)
                throw new InvalidDataException($"Scene sector {sectorIndex} header escaped the environment chain.");
            int numLpVertices = chain[offset + 16];
            int numLpColors = chain[offset + 17];
            int numLpFaces = chain[offset + 18];
            int numHpVertices = chain[offset + 20];
            int numHpColors = chain[offset + 21];
            int numHpFaces = chain[offset + 22];
            int sizeWords = 7 + numLpVertices + numLpColors + (numLpFaces * 2) +
                numHpVertices + (numHpColors * 2) + (numHpFaces * 4);
            int sizeBytes = checked(sizeWords * 4);
            if (sizeBytes < SceneSectorHeaderByteLength || offset + sizeBytes > chain.Length)
                throw new InvalidDataException($"Scene sector {sectorIndex} has invalid size 0x{sizeBytes:X}.");
            if (sectorIndex == SourceSectorIndex &&
                SceneSectorChainWadOffset + offset != SourceSectorWadOffset)
            {
                throw new InvalidDataException("Scene-chain walk no longer places sector 213 at its pinned WAD offset.");
            }

            int dataStart = offset + SceneSectorHeaderByteLength;
            int hpVertexStartWords = numLpVertices + numLpColors + (numLpFaces * 2);
            int hpColorStartWords = hpVertexStartWords + numHpVertices;
            int hpFaceStartWords = hpColorStartWords + (numHpColors * 2);
            UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint[] vertices =
                new UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint[numHpVertices];
            byte[] header = chain.AsSpan(offset, SceneSectorHeaderByteLength).ToArray();
            for (int vertex = 0; vertex < numHpVertices; vertex++)
            {
                int vertexOffset = dataStart + ((hpVertexStartWords + vertex) * 4);
                vertices[vertex] = DecodeSceneVertex(chain.AsSpan(vertexOffset, 4).ToArray(), header);
            }
            for (int face = 0; face < numHpFaces; face++)
            {
                int faceOffset = dataStart + ((hpFaceStartWords + (face * 4)) * 4);
                ReadOnlySpan<byte> slots = chain.AsSpan(faceOffset, 4);
                if (slots.ToArray().Any(index => index >= vertices.Length))
                    throw new InvalidDataException($"Scene sector {sectorIndex} HP face {face} has an out-of-range vertex slot.");
                List<UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint> polygon = [];
                HashSet<byte> seen = [];
                foreach (byte slot in slots)
                {
                    if (seen.Add(slot))
                        polygon.Add(vertices[slot]);
                }
                if (polygon.Count >= 3 && IntersectsOpenRampFootprint(polygon))
                    intersections.Add($"{sectorIndex}:{face}");
            }
            offset += sizeBytes;
        }
        if (offset != chain.Length || !intersections.SequenceEqual(ExpectedVisualFootprintIntersections))
        {
            throw new InvalidDataException(
                $"Full {SceneSectorCount}-sector visual overlap scan ended at 0x{SceneSectorChainWadOffset + offset:X} " +
                $"with footprint set [{string.Join(',', intersections)}], expected exact end 0x{SceneSectorChainEndWadOffset:X} and [213:37].");
        }
    }

    private static void VerifyCollisionBindings(FileStream stream, DiscLayout layout, bool authored)
    {
        VerifyCollisionPatchDefinitions();
        byte[] table = DiscImage.ReadFileBytes(
            stream, layout, WadLba, CollisionTriangleTableWadOffset, CollisionTriangleTableByteLength);
        byte[] blocks = DiscImage.ReadFileBytes(
            stream, layout, WadLba, CollisionBlocksWadOffset, CollisionBlocksByteLength);
        int targetZ = authored ? AuthoredRidgeZ : 512;
        UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint leftTarget = new(7762, 6474, targetZ);
        UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint rightTarget = new(7890, 6474, targetZ);
        int[] completeSharedPointFan = Enumerable.Range(0, CollisionTriangleCount)
            .Select(index => DecodeCollisionTriangle(table.AsSpan(index * 12, 12), index))
            .Where(triangle => triangle.Points.Contains(leftTarget) || triangle.Points.Contains(rightTarget))
            .Select(triangle => triangle.Index)
            .ToArray();
        if (!completeSharedPointFan.SequenceEqual(CollisionTriangleIndexes))
        {
            throw new InvalidDataException(
                $"The {(authored ? "authored" : "source")} collision shared-point fan is " +
                $"[{string.Join(',', completeSharedPointFan)}], expected every exact ridge row " +
                $"[{string.Join(',', CollisionTriangleIndexes)}].");
        }
        foreach (UnusedLevel65AuthoredTerrainSolidEntryRampControlPatch patch in
                 ExpectedPatches.Where(item => item.CollisionTriangleIndex >= 0))
        {
            byte[] expected = ParseHex(authored ? patch.AfterHex : patch.BeforeHex);
            RequireEqual(
                table.AsSpan(patch.CollisionTriangleIndex * 12, 12).ToArray(),
                expected,
                $"{(authored ? "authored" : "source")} collision triangle {patch.CollisionTriangleIndex}");
            byte assignment = DiscImage.ReadFileBytes(
                stream,
                layout,
                WadLba,
                CollisionAssignmentsWadOffset + patch.CollisionTriangleIndex,
                1)[0];
            if (assignment != 0)
                throw new InvalidDataException($"Collision triangle {patch.CollisionTriangleIndex} assignment changed from ordinary surface 0.");
            CollisionTriangle decoded = DecodeCollisionTriangle(
                table.AsSpan(patch.CollisionTriangleIndex * 12, 12),
                patch.CollisionTriangleIndex);
            CollisionBindingExpectation binding = CollisionBindingExpectations.Single(
                item => item.TriangleIndex == patch.CollisionTriangleIndex);
            CollisionBlockBounds bounds = CollisionBlockBoundsFor(decoded.Points);
            if (bounds != new CollisionBlockBounds(
                    binding.MinXBlock,
                    binding.MaxXBlock,
                    binding.MinYBlock,
                    binding.MaxYBlock,
                    binding.MinZBlock,
                    binding.MaxZBlock))
            {
                throw new InvalidDataException(
                    $"Collision triangle {patch.CollisionTriangleIndex} block bounds changed to {bounds}.");
            }
            long[] refs = FindCollisionLookupReferences(blocks, patch.CollisionTriangleIndex);
            if (!refs.SequenceEqual(binding.LookupWadOffsets))
                throw new InvalidDataException($"Collision triangle {patch.CollisionTriangleIndex} lookup references changed.");
        }

        int[] exposed = Enumerable.Range(0, CollisionTriangleCount)
            .Select(index => DecodeCollisionTriangle(table.AsSpan(index * 12, 12), index))
            .Where(triangle => !IsZeroArea(triangle.Points) && IntersectsOpenRampFootprint(triangle.Points))
            .Select(triangle => triangle.Index)
            .ToArray();
        if (!exposed.SequenceEqual(new[] { 1353, 1354 }))
        {
            throw new InvalidDataException(
                $"The open interior of face 213:37 overlaps collision set [{string.Join(',', exposed)}], expected only 1353/1354.");
        }
    }

    private static void VerifyCollisionPatchDefinitions()
    {
        int[] patchIndexes = ExpectedPatches
            .Where(patch => patch.CollisionTriangleIndex >= 0)
            .Select(patch => patch.CollisionTriangleIndex)
            .ToArray();
        if (!patchIndexes.SequenceEqual(CollisionTriangleIndexes) ||
            !patchIndexes.SequenceEqual(CollisionBindingExpectations.Select(item => item.TriangleIndex)))
        {
            throw new InvalidDataException("The exact collision patch/binding triangle order changed.");
        }
        foreach (UnusedLevel65AuthoredTerrainSolidEntryRampControlPatch patch in
                 ExpectedPatches.Where(item => item.CollisionTriangleIndex >= 0))
        {
            byte[] beforeBytes = ParseHex(patch.BeforeHex);
            byte[] afterBytes = ParseHex(patch.AfterHex);
            CollisionTriangle before = DecodeCollisionTriangle(beforeBytes, patch.CollisionTriangleIndex);
            CollisionTriangle after = DecodeCollisionTriangle(afterBytes, patch.CollisionTriangleIndex);
            UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint[] transformed = before.Points
                .Select(TransformCollisionPoint)
                .ToArray();
            if (!PointSet(transformed).SequenceEqual(PointSet(after.Points)) ||
                !IsCyclicRotation(transformed, after.Points))
            {
                throw new InvalidDataException(
                    $"Collision triangle {patch.CollisionTriangleIndex} is not the exact winding-preserving global ramp-point transformation.");
            }
            uint beforeZWord = BinaryPrimitives.ReadUInt32LittleEndian(beforeBytes.AsSpan(8, 4));
            uint afterZWord = BinaryPrimitives.ReadUInt32LittleEndian(afterBytes.AsSpan(8, 4));
            if ((beforeZWord & 0xC000) != (afterZWord & 0xC000) || (afterZWord & 0xC000) != 0)
                throw new InvalidDataException($"Collision triangle {patch.CollisionTriangleIndex} changed its pinned zero flags.");
            CollisionBindingExpectation binding = CollisionBindingExpectations.Single(
                item => item.TriangleIndex == patch.CollisionTriangleIndex);
            CollisionBlockBounds expectedBounds = new(
                binding.MinXBlock,
                binding.MaxXBlock,
                binding.MinYBlock,
                binding.MaxYBlock,
                binding.MinZBlock,
                binding.MaxZBlock);
            if (CollisionBlockBoundsFor(before.Points) != expectedBounds ||
                CollisionBlockBoundsFor(after.Points) != expectedBounds)
            {
                throw new InvalidDataException($"Collision triangle {patch.CollisionTriangleIndex} changed exact lookup-cell membership bounds.");
            }
        }
    }

    private static IReadOnlyList<UnusedLevel65AuthoredTerrainSolidEntryRampControlCollisionBinding>
        BuildPublicCollisionBindings() => ExpectedPatches
            .Where(patch => patch.CollisionTriangleIndex >= 0)
            .Select(patch =>
            {
                CollisionTriangle source = DecodeCollisionTriangle(
                    ParseHex(patch.BeforeHex),
                    patch.CollisionTriangleIndex);
                CollisionTriangle authored = DecodeCollisionTriangle(
                    ParseHex(patch.AfterHex),
                    patch.CollisionTriangleIndex);
                UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint[] transformed = source.Points
                    .Select(TransformCollisionPoint)
                    .ToArray();
                CollisionBindingExpectation expected = CollisionBindingExpectations.Single(
                    item => item.TriangleIndex == patch.CollisionTriangleIndex);
                return new UnusedLevel65AuthoredTerrainSolidEntryRampControlCollisionBinding(
                    patch.CollisionTriangleIndex,
                    source.Points,
                    authored.Points,
                    IsCyclicRotation(transformed, authored.Points),
                    expected.MinXBlock,
                    expected.MaxXBlock,
                    expected.MinYBlock,
                    expected.MaxYBlock,
                    expected.MinZBlock,
                    expected.MaxZBlock,
                    expected.LookupWadOffsets,
                    Assignment: 0,
                    Flags: 0);
            })
            .ToArray();

    private static UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint TransformCollisionPoint(
        UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint point) => point switch
    {
        (7762, 6474, 512) => new(7762, 6474, AuthoredRidgeZ),
        (7890, 6474, 512) => new(7890, 6474, AuthoredRidgeZ),
        _ => point
    };

    private static string[] PointSet(IEnumerable<UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint> points) =>
        points.Select(point => $"{point.X},{point.Y},{point.Z}")
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static bool IsCyclicRotation(
        IReadOnlyList<UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint> expected,
        IReadOnlyList<UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint> actual)
    {
        if (expected.Count != actual.Count)
            return false;
        for (int shift = 0; shift < expected.Count; shift++)
        {
            bool matches = true;
            for (int index = 0; index < expected.Count; index++)
            {
                if (expected[index] != actual[(index + shift) % actual.Count])
                {
                    matches = false;
                    break;
                }
            }
            if (matches)
                return true;
        }
        return false;
    }

    private static CollisionBlockBounds CollisionBlockBoundsFor(
        IReadOnlyList<UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint> points) => new(
        points.Min(point => point.X) >> 8,
        points.Max(point => point.X) >> 8,
        points.Min(point => point.Y) >> 8,
        points.Max(point => point.Y) >> 8,
        points.Min(point => point.Z) >> 8,
        points.Max(point => point.Z) >> 8);

    private static long[] FindCollisionLookupReferences(byte[] blocks, int triangleIndex)
    {
        List<long> offsets = [];
        for (int offset = 0; offset + 2 <= blocks.Length; offset += 2)
        {
            if ((BinaryPrimitives.ReadUInt16LittleEndian(blocks.AsSpan(offset, 2)) & 0x7FFF) == triangleIndex)
                offsets.Add(CollisionBlocksWadOffset + offset);
        }
        return offsets.ToArray();
    }

    private static int[] ReferencingFaces(byte[] faces, int vertexIndex)
    {
        List<int> result = [];
        for (int face = 0; face < faces.Length / SourceFaceByteLength; face++)
        {
            if (faces.AsSpan(face * SourceFaceByteLength, 4).Contains((byte)vertexIndex))
                result.Add(face);
        }
        return result.ToArray();
    }

    private static bool IntersectsOpenRampFootprint(IReadOnlyList<UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint> points)
    {
        // One-unit inset excludes neighboring triangles that merely share an edge.
        const int minX = 7763;
        const int maxX = 7889;
        const int minY = 6347;
        const int maxY = 6473;
        (int X, int Y)[] polygon = points.Select(point => (point.X, point.Y)).ToArray();
        (int X, int Y)[] rectangle = [(minX, minY), (maxX, minY), (maxX, maxY), (minX, maxY)];
        if (polygon.Any(point => point.X >= minX && point.X <= maxX && point.Y >= minY && point.Y <= maxY))
            return true;
        if (rectangle.Any(point => PointInPolygon(point, polygon)))
            return true;
        for (int a = 0; a < polygon.Length; a++)
        {
            for (int b = 0; b < 4; b++)
            {
                if (SegmentsCross(polygon[a], polygon[(a + 1) % polygon.Length], rectangle[b], rectangle[(b + 1) % 4]))
                    return true;
            }
        }
        return false;
    }

    private static bool PointInPolygon((int X, int Y) point, IReadOnlyList<(int X, int Y)> polygon)
    {
        bool inside = false;
        for (int current = 0, previous = polygon.Count - 1; current < polygon.Count; previous = current++)
        {
            (int X, int Y) a = polygon[current];
            (int X, int Y) b = polygon[previous];
            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (double)(b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private static bool SegmentsCross(
        (int X, int Y) a,
        (int X, int Y) b,
        (int X, int Y) c,
        (int X, int Y) d)
    {
        long a1 = Orient(a, b, c);
        long a2 = Orient(a, b, d);
        long a3 = Orient(c, d, a);
        long a4 = Orient(c, d, b);
        return ((a1 > 0 && a2 < 0) || (a1 < 0 && a2 > 0)) &&
               ((a3 > 0 && a4 < 0) || (a3 < 0 && a4 > 0));
    }

    private static long Orient((int X, int Y) a, (int X, int Y) b, (int X, int Y) c) =>
        ((long)b.X - a.X) * ((long)c.Y - a.Y) - ((long)b.Y - a.Y) * ((long)c.X - a.X);

    private static bool IsZeroArea(IReadOnlyList<UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint> points)
    {
        long ux = points[1].X - points[0].X;
        long uy = points[1].Y - points[0].Y;
        long uz = points[1].Z - points[0].Z;
        long vx = points[2].X - points[0].X;
        long vy = points[2].Y - points[0].Y;
        long vz = points[2].Z - points[0].Z;
        long cx = uy * vz - uz * vy;
        long cy = uz * vx - ux * vz;
        long cz = ux * vy - uy * vx;
        return cx == 0 && cy == 0 && cz == 0;
    }

    private static CollisionTriangle DecodeCollisionTriangle(ReadOnlySpan<byte> bytes, int index)
    {
        uint xWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes[..4]);
        uint yWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
        uint zWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
        int x1 = (int)(xWord & 0x3FFF);
        int y1 = (int)(yWord & 0x3FFF);
        int z1 = (int)(zWord & 0x3FFF);
        UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint[] points =
        [
            new(x1, y1, z1),
            new(
                x1 + Signed9((int)((xWord >> 14) & 0x1FF)),
                y1 + Signed9((int)((yWord >> 14) & 0x1FF)),
                z1 + (int)((zWord >> 16) & 0xFF)),
            new(
                x1 + Signed9((int)((xWord >> 23) & 0x1FF)),
                y1 + Signed9((int)((yWord >> 23) & 0x1FF)),
                z1 + (int)((zWord >> 24) & 0xFF))
        ];
        return new(index, points);
    }

    private static int Signed9(int value) => (value & 0x100) != 0 ? value - 0x200 : value;

    private static UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint DecodeSceneVertex(
        byte[] vertexWord,
        byte[] sectorHeader)
    {
        uint word = BinaryPrimitives.ReadUInt32LittleEndian(vertexWord);
        uint xyPos = BinaryPrimitives.ReadUInt32LittleEndian(sectorHeader.AsSpan(8, 4));
        uint zPos = BinaryPrimitives.ReadUInt32LittleEndian(sectorHeader.AsSpan(12, 4));
        int sectorX = (int)(xyPos >> 16);
        int sectorY = (int)(xyPos & 0xFFFF);
        int sectorZ = (int)((zPos >> 14) & 0xFFFF) >> 2;
        int x = sectorX + (int)(((word >> 19) & 0x1FFC) >> 2);
        int y = sectorY + (int)(((word >> 8) & 0x1FFC) >> 2);
        int z = sectorZ + (int)(((word << 3) & 0x1FFC) >> 3);
        if (((BinaryPrimitives.ReadUInt16LittleEndian(sectorHeader.AsSpan(4, 2)) >> 12) & 1) == 1)
            z >>= 3;
        return new(x, y, z);
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
                        logicalOffset >= range.Offset && logicalOffset < range.Offset + range.ByteLength))
                    outside++;
            }
        }
        return new(changed, outside);
    }

    private static PhysicalDiff ComparePhysicalImages(
        FileStream baseline,
        FileStream output,
        IReadOnlyList<int> allowedRawSectors,
        CancellationToken cancellationToken)
    {
        if (baseline.Length != output.Length || baseline.Length % RawSectorBytes != 0)
            throw new InvalidDataException("Physical comparison requires equal complete MODE2/2352 images.");
        HashSet<long> allowed = allowedRawSectors.Select(value => (long)value).ToHashSet();
        const int chunkSize = 1 << 20;
        byte[] before = new byte[chunkSize];
        byte[] after = new byte[chunkSize];
        long changed = 0;
        long outside = 0;
        HashSet<long> changedSectors = [];
        Dictionary<int, int[]> counts = [];
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
                if (!counts.TryGetValue(sectorIndex, out int[]? regions))
                {
                    regions = new int[6];
                    counts.Add(sectorIndex, regions);
                }
                int within = checked((int)((offset + index) % RawSectorBytes));
                regions[RawSectorRegion(within)]++;
                if (!allowed.Contains(sector))
                    outside++;
            }
        }
        if (!changedSectors.SetEquals(allowed) || outside != 0)
            throw new InvalidDataException("The solid-entry-ramp control changed an unexpected raw-sector set.");
        UnusedLevel65AuthoredTerrainSolidEntryRampControlRawSectorDiff[] diffs = counts
            .OrderBy(pair => pair.Key)
            .Select(pair => new UnusedLevel65AuthoredTerrainSolidEntryRampControlRawSectorDiff(
                pair.Key,
                pair.Value[0],
                pair.Value[1],
                pair.Value[2],
                pair.Value[3],
                pair.Value[4],
                pair.Value[5],
                pair.Value.Sum()))
            .ToArray();
        return new(changed, changedSectors.Count, diffs);
    }

    private static int RawSectorRegion(int withinSector) => withinSector switch
    {
        < 24 => 0,
        < 2072 => 1,
        < 2076 => 2,
        // MODE2 Form 1 has no MODE1-style eight-byte reserved gap. ECC-P
        // begins immediately after EDC at raw byte 2076 and ECC-Q at 2248.
        < 2248 => 4,
        _ => 5
    };

    private static IReadOnlyList<string> RuntimeChecklist() =>
    [
        "Before booting, open DuckStation's SCUS-94228 game settings. Turn Enable Cheats off, confirm Moon Jump is not enabled, and set Memory Card 1 and Memory Card 2 to None. Cold boot this exact CUE; do not resume or load a save state.",
        $"Reach controllable gameplay. For ID65 press Select; then {TestLevelWarpPatch.ActivationSequence}; then press Left, then Down. Confirm the Inventory title is TOWN SQUARE and ID65 loads normally.",
        "When the fly-in ends, stop on the entry landing and do not turn. Use the supplied top-down location guide. The edited ground is directly ahead in Spyro's initial facing: its near edge is 64 world units ahead, its red-marked crest is 192 units ahead, and it returns to flat ground 320 units ahead. Do not search near gems.",
        "Before touching it, confirm a broad native-texture-28 ridge is unmistakably visible across the path. Its center rises from Z 512 to Z 608 and descends to Z 512. Rotate the camera only while staying at the landing; report flicker, gaps, unrelated stretching, or an absent ridge.",
        "Walk straight through the center of the marked route at a slow pace. Spyro should climb the visible 96-unit incline, cross the crest, and descend with the visible surface. He must not pass through it, remain on the old flat Z 512 plane underneath it, hover above it, snag, launch, or fall through.",
        "Turn around beyond the ridge and walk back through the same center line. Then charge straight across in each direction. Report whether walking and charging remain aligned with the visible incline and descent.",
        "Jump onto the center incline and onto the crest, then land on the descent. Report landing, sliding, hovering, snagging, launch, or pass-through separately. The center route is the primary gate; off-center tapered corners are steeper and should be reported separately rather than substituted for the center test.",
        "Move a short distance away, return using the guide, and repeat ordinary camera rotation and one slow center crossing. Movement, camera, enemies, pause, and Inventory should remain stable. LP terrain is unchanged, so this is a close-range gate only.",
        $"Reset, cold boot the same CUE, re-enter ID65 with Select; {TestLevelWarpPatch.ActivationSequence}; Left; Down, and confirm the same visible and solid ridge returns. Do not use Return Home, Exit Level, Quit Game, saving, or a memory card.",
        $"Reset and load retail Town Square: Select; {TestLevelWarpPatch.ActivationSequence}; Cross; Triangle. Confirm the entry area is flat and the ridge is absent.",
        $"Reset and load Gnasty's Loot: Select; {TestLevelWarpPatch.ActivationSequence}; Left; Right. Confirm it loads normally.",
        $"Reset and load Sunny Flight: Select; {TestLevelWarpPatch.ActivationSequence}; Cross; Down. Confirm it loads and flight controls remain normal.",
        "PASS requires: ridge immediately visible only in ID65; slow walking, charging, jumping, and landing follow the center surface in both directions; no old flat plane remains under it; reset reproduces it; retail Town Square is unchanged; Gnasty's Loot and Sunny Flight load. FAIL if the ridge is absent, visual-only/pass-through, the old flat plane remains, collision launches/snags/falls through, retail Town Square changes, or a comparison level fails. This is an existing-face collision-coherence control, not an Add Terrain pass.",
        "Do not collect treasure, attack enemies, rescue dragons, touch the thief, open chests, die, save, use portals, Return Home, Exit Level, or Quit Game, enable cheats, insert a card, or run longer than eight minutes. Counts, sector/component sizes, collision tree/index/assignments, LP terrain, textures, Mobys, music, totals, saving, and persistence remain unchanged."
    ];

    private static WadEntry ReadWadEntry(FileStream stream, DiscLayout layout, int index)
    {
        byte[] row = DiscImage.ReadFileBytes(stream, layout, WadLba, index * 8L, 8);
        return new(
            BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(0, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(4, 4)));
    }

    private static void RequireEntry(WadEntry entry, long offset, int size, string label)
    {
        if (entry.Offset != offset || entry.Size != size)
            throw new InvalidDataException($"The {label} is 0x{entry.Offset:X}+0x{entry.Size:X}, expected 0x{offset:X}+0x{size:X}.");
    }

    private static bool IsExecutableName(string name) =>
        name.StartsWith("SCUS_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SCES_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLES_", StringComparison.OrdinalIgnoreCase);

    private static string RequireExistingFile(string path, string label)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Missing {label}.", fullPath);
        return fullPath;
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{label} SHA-256 is {actual}, expected {expected}.");
    }

    private static void RequirePinnedHash(string actual, string expected, string label)
    {
        if (string.IsNullOrEmpty(expected))
            return;
        RequireHash(actual, expected, label);
    }

    private static void RequireInt32(byte[] bytes, int expected, string label)
    {
        int actual = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (actual != expected)
            throw new InvalidDataException($"{label} is 0x{actual:X}, expected 0x{expected:X}.");
    }

    private static void RequireEqual(byte[] actual, byte[] expected, string label)
    {
        if (!actual.SequenceEqual(expected))
            throw new InvalidDataException($"{label} differs: {Hex(actual)} != {Hex(expected)}.");
    }

    private static byte[] ParseHex(string value) => Convert.FromHexString(value.Replace(" ", ""));
    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).Chunk(2).Select(chars => new string(chars)).Aggregate((a, b) => a + " " + b);
    private static string PatchLabel(UnusedLevel65AuthoredTerrainSolidEntryRampControlPatch patch) =>
        patch.VertexIndex >= 0 ? $"v{patch.VertexIndex}" : $"triangle {patch.CollisionTriangleIndex}";

    private static bool PathEquals(string? left, string? right) =>
        string.Equals(Path.GetFullPath(left ?? ""), Path.GetFullPath(right ?? ""), StringComparison.Ordinal);

    private static async Task WriteAsciiAtomicallyStagedAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(content);
        await using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
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
        catch
        {
            // Preserve the primary export/recovery result.
        }
    }

    private sealed record BaseLayout(
        DiscLayout Layout,
        DiscFileRecord Wad,
        DiscFileRecord Executable,
        string RetailOverlaySha256,
        string RetailDataSha256,
        string TargetOverlaySha256,
        string ExecutableSha256);

    private sealed record Prepared(
        BaseLayout BaseLayout,
        UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidatePlan Plan);

    private sealed record WadEntry(int Offset, int Size);
    private sealed record CollisionTriangle(
        int Index,
        IReadOnlyList<UnusedLevel65AuthoredTerrainSolidEntryRampControlPoint> Points);
    private sealed record CollisionBindingExpectation(
        int TriangleIndex,
        int MinXBlock,
        int MaxXBlock,
        int MinYBlock,
        int MaxYBlock,
        int MinZBlock,
        int MaxZBlock,
        IReadOnlyList<long> LookupWadOffsets);
    private readonly record struct CollisionBlockBounds(
        int MinXBlock,
        int MaxXBlock,
        int MinYBlock,
        int MaxYBlock,
        int MinZBlock,
        int MaxZBlock);
    private sealed record LogicalDiff(long ChangedBytes, long OutsideAllowedBytes);
    private sealed record PhysicalDiff(
        long ChangedBytes,
        int ChangedRawSectorCount,
        IReadOnlyList<UnusedLevel65AuthoredTerrainSolidEntryRampControlRawSectorDiff> RawSectorDiffs);
    private sealed record Readback(
        long ChangedLogicalWadBytes,
        long ChangedPhysicalImageBytes,
        int ChangedRawSectorCount,
        IReadOnlyList<UnusedLevel65AuthoredTerrainSolidEntryRampControlRawSectorDiff> RawSectorDiffs,
        string OutputDataSha256);
}

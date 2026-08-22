using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

public sealed record UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateRequest(
    string BaseImagePath,
    string BaseCuePath,
    string OutputImagePath,
    string OutputCuePath);

public sealed record UnusedLevel65AuthoredTerrainRenderVisibilityControlPoint(
    int X,
    int Y,
    int Z);

public sealed record UnusedLevel65AuthoredTerrainRenderVisibilityControlPatch(
    string Kind,
    string RuntimeKey,
    int VertexIndex,
    long WadOffset,
    string BeforeHex,
    string AfterHex,
    UnusedLevel65AuthoredTerrainRenderVisibilityControlPoint OriginalPoint,
    UnusedLevel65AuthoredTerrainRenderVisibilityControlPoint AuthoredPoint);

public sealed record UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidatePlan(
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
    UnusedLevel65AuthoredTerrainRenderVisibilityControlPoint SpawnPoint,
    IReadOnlyList<UnusedLevel65AuthoredTerrainRenderVisibilityControlPoint> OriginalFacePoints,
    IReadOnlyList<UnusedLevel65AuthoredTerrainRenderVisibilityControlPatch> Patches,
    IReadOnlyList<int> IndirectlyAffectedFaceIndexes,
    IReadOnlyList<int> AffectedRawSectorLbas,
    IReadOnlyList<string> RuntimeChecklist,
    bool VisualOnly,
    bool CollisionIntentionallyUnchanged,
    bool RequiresDuckStationRuntimeProof);

public sealed record UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    string OutputDataSha256,
    UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidatePlan Plan,
    long ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool VertexReadbackVerified,
    bool RawSectorIntegrityVerified,
    bool TerrainCountsPreserved,
    bool TerrainFacePreserved,
    bool CollisionComponentsPreserved,
    bool RetailTownSquarePreserved,
    bool Id65OverlayPreserved,
    bool ExecutablePreserved,
    bool BaseCandidatePreserved,
    bool AtomicRenameCompleted);

/// <summary>
/// Disposable renderer-isolation control for physically independent ID65. It
/// starts from the exact focused-runtime-passed display-name candidate and
/// raises two already-live HP vertices immediately ahead of the entry spawn.
/// It does not add a face, grow or repack a terrain sector, or alter collision.
/// The resulting ridge answers only whether the retail renderer consumes an
/// unmistakable existing-face vertex edit from ID65 row 80.
/// </summary>
public static class UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateExporter
{
    public const string ProfileId =
        "unused-level-65-town-square-authored-terrain-render-visibility-control-clean-usa-disposable-v2";
    public const string ExpectedOutputImageSha256 =
        "01a170b19303eaab77fb00fbc8cfabf5425a640348c84aa09f8aa4eb5b57e6b1";
    public const string ExpectedOutputDataSha256 =
        "c20e5cee4abd551c2536eb23fba9d2a8868193c9991af9ccfe7883429c9b4a57";

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
    public const int AuthoredRidgeZ = 1024;

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
    private const long ProtectedFollowingSectorHeaderWadOffset = 0x6A3C4F8;
    private const int SceneSectorHeaderByteLength = 28;
    private const int SourceFaceByteLength = 16;
    private const int WadLba = 37;
    private const int ExpectedWadByteLength = 0x6C18800;
    private const int ExecutableLba = 55382;
    private const int ExecutableByteLength = 0x66000;
    private const int RawSectorBytes = 2352;
    private const int UserOffset = 24;
    private const int ExpectedAffectedRawSector = 54434;
    private const long ExpectedChangedLogicalWadBytes = 2;
    private const long ExpectedChangedPhysicalImageBytes = 44;
    private const byte XaDataSubmode = 0x08;

    private static readonly byte[] ExpectedSourceSectorHeader = Convert.FromHexString(
        "101A091FDC028F025417031D1409E001233015008CB9717DFFFFFFFF");
    private static readonly byte[] ExpectedSourceFace = Convert.FromHexString(
        "283930272D3D352D1C0680FF08F4CFFF");
    private static readonly byte[] ExpectedProtectedFollowingSectorHeader = Convert.FromHexString(
        "130B04257B41E001000A0024FFFFE0010401010000000007FFFFFFFF");
    private static readonly UnusedLevel65AuthoredTerrainRenderVisibilityControlPoint[] OriginalFacePoints =
    [
        new(7762, 6346, 512),
        new(7890, 6346, 512),
        new(7890, 6474, 512),
        new(7762, 6474, 512)
    ];
    private static readonly UnusedLevel65AuthoredTerrainRenderVisibilityControlPatch[] ExpectedPatches =
    [
        new(
            "visual-hp",
            RuntimeKey,
            39,
            0x6A3EB60,
            "20 D8 E7 29",
            "20 DA E7 29",
            new(7762, 6474, 512),
            new(7762, 6474, AuthoredRidgeZ)),
        new(
            "visual-hp",
            RuntimeKey,
            48,
            0x6A3EB84,
            "20 D8 E7 39",
            "20 DA E7 39",
            new(7890, 6474, 512),
            new(7890, 6474, AuthoredRidgeZ))
    ];
    private static readonly int[] IndirectlyAffectedFaceIndexes = [25, 26, 29, 37, 38, 43];

    public static async Task<UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidatePlan> BuildPlanAsync(
        UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateRequest request,
        CancellationToken cancellationToken = default) =>
        (await PrepareAsync(request, cancellationToken)).Plan;

    public static async Task<UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateResult> ExportAsync(
        UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateRequest request,
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
            throw new InvalidOperationException("The ID65 renderer-visibility BIN and CUE must share one directory.");

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
                foreach (UnusedLevel65AuthoredTerrainRenderVisibilityControlPatch patch in ExpectedPatches)
                {
                    byte[] before = ParseHex(patch.BeforeHex);
                    byte[] after = ParseHex(patch.AfterHex);
                    byte[] actualBefore = DiscImage.ReadFileBytes(
                        output,
                        layout,
                        WadLba,
                        patch.WadOffset,
                        before.Length);
                    RequireEqual(actualBefore, before, $"staged vertex {patch.VertexIndex} preimage");
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
                    [(ExpectedAffectedRawSector, 1)]);
                RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(
                    output,
                    layout,
                    ExpectedAffectedRawSector,
                    XaDataSubmode);
                if (rebuiltRawSectors != 1 || verifiedRawSectors != 1)
                    throw new InvalidDataException("The ID65 renderer-visibility control did not rebuild exactly one raw sector.");
                output.Flush(flushToDisk: true);
            }

            Readback readback = VerifyReadback(
                baseImage,
                temporaryImage,
                prepared.BaseLayout,
                cancellationToken);
            string outputImageSha256 = await HashFileAsync(temporaryImage, cancellationToken);
            RequireHash(outputImageSha256, ExpectedOutputImageSha256, "ID65 renderer-visibility output BIN");
            RequireHash(readback.OutputDataSha256, ExpectedOutputDataSha256, "ID65 renderer-visibility data payload");
            if (readback.ChangedLogicalWadBytes != ExpectedChangedLogicalWadBytes ||
                readback.ChangedPhysicalImageBytes != ExpectedChangedPhysicalImageBytes ||
                readback.ChangedRawSectorCount != 1)
            {
                throw new InvalidDataException(
                    $"The pinned renderer-visibility diff changed: logical={readback.ChangedLogicalWadBytes}, physical={readback.ChangedPhysicalImageBytes}, sectors={readback.ChangedRawSectorCount}.");
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
            RequireHash(baseHashAfter, BaseImageSha256, "passed display-name base after renderer-visibility export");
            TryDelete(backupImage);
            imageBackedUp = false;
            TryDelete(backupCue);
            cueBackedUp = false;

            return new UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateResult(
                outputImage,
                outputCue,
                outputImageSha256,
                readback.OutputDataSha256,
                prepared.Plan,
                readback.ChangedLogicalWadBytes,
                readback.ChangedPhysicalImageBytes,
                rebuiltRawSectors,
                readback.ChangedRawSectorCount,
                ExactLogicalDiffBoundaryVerified: true,
                ExactPhysicalSectorBoundaryVerified: true,
                VertexReadbackVerified: true,
                RawSectorIntegrityVerified: true,
                TerrainCountsPreserved: true,
                TerrainFacePreserved: true,
                CollisionComponentsPreserved: true,
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
                    "ID65 renderer-visibility export failed and prior output recovery was incomplete.",
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
        UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateRequest request,
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

        UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidatePlan plan = new(
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
            IndirectlyAffectedFaceIndexes,
            [ExpectedAffectedRawSector],
            RuntimeChecklist(),
            VisualOnly: true,
            CollisionIntentionallyUnchanged: true,
            RequiresDuckStationRuntimeProof: true);
        return new Prepared(baseLayout, plan);
    }

    private static BaseLayout InspectBaseLayout(string baseImage)
    {
        DiscLayout layout = DiscImage.DetectLayout(baseImage);
        if (layout.SectorSize != RawSectorBytes || layout.UserOffset != UserOffset)
            throw new InvalidDataException("The ID65 renderer-visibility control requires the checked MODE2/2352 layout.");
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
        RequireEqual(
            DiscImage.ReadFileBytes(input, layout, WadLba, SourceSectorWadOffset, SceneSectorHeaderByteLength),
            ExpectedSourceSectorHeader,
            "source sector 213 header");
        RequireEqual(
            DiscImage.ReadFileBytes(input, layout, WadLba, SourceFaceWadOffset, SourceFaceByteLength),
            ExpectedSourceFace,
            "source face 213:37:hp");
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

        foreach (UnusedLevel65AuthoredTerrainRenderVisibilityControlPatch patch in ExpectedPatches)
        {
            byte[] before = ParseHex(patch.BeforeHex);
            byte[] actual = DiscImage.ReadFileBytes(input, layout, WadLba, patch.WadOffset, before.Length);
            RequireEqual(actual, before, $"source vertex {patch.VertexIndex}");
            long patchEnd = checked(patch.WadOffset + before.Length);
            if (patch.WadOffset < TargetDataWadOffset || patchEnd > TargetDataWadOffset + TargetDataByteLength)
                throw new InvalidDataException($"Vertex {patch.VertexIndex} escaped ID65 WAD row 80.");
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
            throw new InvalidDataException("The ID65 renderer-visibility control changed the disc layout or raw length.");

        DiscFileRecord outputWad = DiscImage.FindRootFileRecord(
            output,
            outputLayout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord outputExecutable = DiscImage.FindRootFileRecord(output, outputLayout, IsExecutableName);
        if (outputWad != baseLayout.Wad || outputExecutable != baseLayout.Executable)
            throw new InvalidDataException("The ID65 renderer-visibility control moved or resized WAD.WAD or SCUS.");

        foreach (UnusedLevel65AuthoredTerrainRenderVisibilityControlPatch patch in ExpectedPatches)
        {
            byte[] expected = ParseHex(patch.AfterHex);
            byte[] actual = DiscImage.ReadFileBytes(output, outputLayout, WadLba, patch.WadOffset, expected.Length);
            RequireEqual(actual, expected, $"authored vertex {patch.VertexIndex} readback");
        }
        RequireEqual(
            DiscImage.ReadFileBytes(output, outputLayout, WadLba, SourceSectorWadOffset, SceneSectorHeaderByteLength),
            ExpectedSourceSectorHeader,
            "preserved source sector 213 header");
        RequireEqual(
            DiscImage.ReadFileBytes(output, outputLayout, WadLba, SourceFaceWadOffset, SourceFaceByteLength),
            ExpectedSourceFace,
            "preserved source face 213:37:hp");
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
        RequireHash(
            Hash(DiscImage.ReadFileBytes(output, outputLayout, WadLba, CollisionComponentWadOffset, CollisionComponentByteLength)),
            CollisionComponentSha256,
            "preserved collision component");

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
                $"The renderer-visibility logical diff changed {logical.ChangedBytes} byte(s), including {logical.OutsideAllowedBytes} outside the two vertex words.");
        }
        PhysicalDiff physical = ComparePhysicalImages(
            baseline,
            output,
            [ExpectedAffectedRawSector],
            cancellationToken);
        if (physical.OutsideAllowedSectorBytes != 0 ||
            physical.ChangedBytes != ExpectedChangedPhysicalImageBytes ||
            physical.ChangedRawSectorCount != 1)
        {
            throw new InvalidDataException(
                $"The renderer-visibility physical diff changed {physical.ChangedBytes} byte(s), including {physical.OutsideAllowedSectorBytes} outside raw LBA {ExpectedAffectedRawSector}.");
        }
        int verifiedRawSectors = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            output,
            outputLayout,
            [(ExpectedAffectedRawSector, 1)]);
        RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(
            output,
            outputLayout,
            ExpectedAffectedRawSector,
            XaDataSubmode);
        if (verifiedRawSectors != 1)
            throw new InvalidDataException("The final renderer-visibility raw-sector verification count changed.");

        return new Readback(
            logical.ChangedBytes,
            physical.ChangedBytes,
            physical.ChangedRawSectorCount,
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
            throw new InvalidDataException("The renderer-visibility raw diff requires equal complete MODE2/2352 images.");
        HashSet<long> allowed = allowedRawSectors.Select(value => (long)value).ToHashSet();
        const int chunkSize = 1 << 20;
        byte[] before = new byte[chunkSize];
        byte[] after = new byte[chunkSize];
        long changed = 0;
        long outside = 0;
        HashSet<long> changedSectors = [];
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
                if (!allowed.Contains(sector))
                    outside++;
            }
        }
        if (!changedSectors.SetEquals(allowed))
            throw new InvalidDataException("The renderer-visibility control changed an unexpected raw-sector set.");
        return new PhysicalDiff(changed, outside, changedSectors.Count);
    }

    private static IReadOnlyList<string> RuntimeChecklist() =>
    [
        "Disable every DuckStation cheat and memory-card insertion, then cold boot this exact CUE without resuming a save state.",
        $"From controllable gameplay, press Select to open Inventory; enter {TestLevelWarpPatch.ActivationSequence}; then press Left, then Down. Confirm ID65 still displays TOWN SQUARE and loads normally.",
        "Before interacting with anything, remain near the entry landing at approximately X 7827, Y 6282, Z 544 and look straight ahead along Spyro's landing/fly-in direction; rotate the camera slowly only if needed. An enormous steep ramp/ridge should be unmistakable 64-192 world units ahead: its near edge stays at Z 512 and its far edge rises to Z 1024.",
        "Inspect the ridge from several ordinary camera angles. It is made from existing HP terrain around native face 213:37, retains native texture 28, and deforms the connected face set 213:{25,26,29,37,38,43}. Confirm there is no unrelated stretching visible from the entry area.",
        "This is intentionally a visual-only renderer control. Collision remains the original flat Z 512 ground, so Spyro may walk or charge through the visible slopes or appear below them. Report that behavior, but do not treat it as a collision failure for this candidate.",
        $"Reset DuckStation, cold boot, and re-enter ID65 with Select; {TestLevelWarpPatch.ActivationSequence}; Left, then Down. Confirm the same ridge is present again. Do not use Exit Level, Quit Game, Return Home, saving, or a memory card.",
        $"Reset and load original retail Town Square with Select; {TestLevelWarpPatch.ActivationSequence}; Cross, then Triangle. Confirm the enormous entry-area ridge is absent and retail terrain remains normal.",
        $"Reset and load Gnasty's Loot with Select; {TestLevelWarpPatch.ActivationSequence}; Left, then Right. Confirm it loads normally.",
        $"Reset and load Sunny Flight with Select; {TestLevelWarpPatch.ActivationSequence}; Cross, then Down. Confirm it loads and flight controls remain normal.",
        "Report whether the ridge is visible immediately after entry, whether its texture is stable from several angles, and whether Spyro passes through it on the unchanged flat collision. A visible ridge proves the existing-face HP renderer consumes the authored row-80 vertex words; an absent ridge requires a renderer/draw-list trace before another structural Add Terrain candidate.",
        "Do not collect treasure, attack or kill enemies, rescue dragons, touch the egg thief, open or break chests, die, insert a memory card, save, use Return Home, use Exit Level or Quit Game, or run longer than 6 minutes. Face counts, terrain storage, collision, textures, Mobys, music, totals, portals, saving, and persistence remain unchanged."
    ];

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
        UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidatePlan Plan);
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
        int ChangedRawSectorCount);
    private sealed record Readback(
        long ChangedLogicalWadBytes,
        long ChangedPhysicalImageBytes,
        int ChangedRawSectorCount,
        string OutputDataSha256);
}

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

public sealed record UnusedLevel65AuthoredTerrainAddCopyCandidateRequest(
    string BaseImagePath,
    string BaseCuePath,
    string OutputImagePath,
    string OutputCuePath);

public sealed record UnusedLevel65AuthoredTerrainAddCopyFacePlan(
    string RuntimeKey,
    int TextureId,
    int SourceSectorIndex,
    long SourceSectorWadOffset,
    long SourceFaceWadOffset,
    IReadOnlyList<Vector3f> OriginalVertices,
    IReadOnlyList<Vector3f> AuthoredVertices,
    float DeltaX,
    float DeltaY,
    IReadOnlyList<float> DeltaZ);

public sealed record UnusedLevel65AuthoredTerrainAddCopyCandidatePlan(
    DateTimeOffset GeneratedAtUtc,
    string ProfileId,
    string BaseProfileId,
    string BaseImageSha256,
    string BaseExecutableSha256,
    int LevelId,
    int ContinuousLevelIndex,
    string ResearchLevelKey,
    int TargetOverlayWadEntry,
    int TargetDataWadEntry,
    long TargetOverlayWadOffset,
    int TargetOverlayByteLength,
    long TargetDataWadOffset,
    int TargetDataByteLength,
    long RetailTownSquareOverlayWadOffset,
    int RetailTownSquareOverlayByteLength,
    long RetailTownSquareDataWadOffset,
    int RetailTownSquareDataByteLength,
    long TownSquareToId65Rebase,
    long FutureMobyTableWadOffset,
    int FutureMobyRecordCount,
    string BaseOverlaySha256,
    string BaseDataSha256,
    long SourceSceneWadOffset,
    int SourceSceneSectorCount,
    int SourceSceneHighPolyFaceCount,
    int SourceSceneLowPolyFaceCount,
    int SourceSearchSectorCount,
    int SourceSearchMatchedSectorCount,
    UnusedLevel65AuthoredTerrainAddCopyFacePlan Face,
    TerrainPatchPlan TerrainPatchPlan,
    IReadOnlyList<int> AffectedRawSectorLbas,
    IReadOnlyList<string> RuntimeChecklist,
    bool RequiresDuckStationRuntimeProof);

public sealed record UnusedLevel65AuthoredTerrainAddCopyCandidateResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    string OutputDataSha256,
    UnusedLevel65AuthoredTerrainAddCopyCandidatePlan Plan,
    long ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool TerrainPatchReadbackVerified,
    bool RawSectorIntegrityVerified,
    bool RetailTownSquarePreserved,
    bool Id65OverlayPreserved,
    bool ExecutablePreserved,
    bool BaseCandidatePreserved,
    bool AtomicRenameCompleted);

/// <summary>
/// First authored-content discriminator for the physically independent ID65
/// payload. It regenerates source-derived terrain provenance directly from WAD
/// entry 80, stages one copied HP face with independent vertices and matching
/// collision, and applies only the resulting ID65-data patches. It is not
/// referenced by the retail level catalog, the editor app, or normal Create BIN.
/// </summary>
public static class UnusedLevel65AuthoredTerrainAddCopyCandidateExporter
{
    public static bool Retired => true;
    public static string RetirementReason =>
        "RETIRED: exact v1 BIN db9230bbe1b8b5b15b52299267b3453cb60ec7494248295f323a40f597d7391f crossed 28 bytes into the sector-202 header; preserve it only as rejected evidence and do not publish, load, or retest it. See docs/runtime-evidence/unused-level-65-town-square-authored-terrain-add-copy-not-observed-2026-08-08.json.";

    public const string ProfileId =
        "unused-level-65-town-square-authored-terrain-add-copy-clean-usa-disposable-v1";
    public const string ExpectedOutputImageSha256 =
        "db9230bbe1b8b5b15b52299267b3453cb60ec7494248295f323a40f597d7391f";

    public const int LevelId = 65;
    public const int ContinuousLevelIndex = 35;
    public const int TargetOverlayWadEntry = 79;
    public const int TargetDataWadEntry = 80;
    public const long TargetOverlayWadOffset = 0x6927000;
    public const int TargetOverlayByteLength = 0xF800;
    public const long TargetDataWadOffset = 0x6936800;
    public const int TargetDataByteLength = 0x2E2000;
    public const long RetailTownSquareOverlayWadOffset = 0x118E800;
    public const int RetailTownSquareOverlayByteLength = 0xF800;
    public const long RetailTownSquareDataWadOffset = 0x119E000;
    public const int RetailTownSquareDataByteLength = 0x2E2000;
    public const long TownSquareToId65Rebase =
        TargetDataWadOffset - RetailTownSquareDataWadOffset;
    public const long FutureMobyTableWadOffset = 0x6B06970;
    public const int FutureMobyRecordCount = 107;
    public const string ResearchLevelKey = "unusedlevel65authoring";
    public const string SourceFaceRuntimeKey = "201:0:hp";
    public const int SourceFaceTextureId = 27;
    public const long SourceFaceSectorWadOffset = 0x6A3C038;
    public const long SourceFaceWadOffset = 0x6A3C2F8;
    public const long SourceSceneWadOffset = 0x6A182E0;
    public const float AuthoredDeltaX = 64f;
    public const float AuthoredDeltaY = 24f;

    private const string BaseImageSha256 =
        UnusedLevel65DisplayNameCandidateExporter.ExpectedOutputImageSha256;
    private const string BaseExecutableSha256 =
        "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";
    private const string BaseOverlaySha256 =
        "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5";
    private const string BaseDataSha256 =
        "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";
    private const string ExpectedOutputDataSha256 =
        "9e4d30371b9aeffed1071883848a67e1c80f6df313826bc7f4e5db2e926696b9";
    private const long ExpectedChangedLogicalWadBytes = 108415;
    private const long ExpectedChangedPhysicalImageBytes = 125762;
    private const int ExpectedAffectedRawSectorCount = 63;
    private const int WadLba = 37;
    private const int ExpectedWadByteLength = 0x6C18800;
    private const int RawSectorBytes = 2352;
    private const int UserSectorBytes = 2048;
    private const int UserOffset = 24;
    private const int ExecutableLba = 55382;
    private const int ExecutableByteLength = 0x66000;

    private static readonly float[] AuthoredDeltaZ = [10f, 13f, 16f];
    private static readonly Vector3f[] ExpectedOriginalVertices =
    [
        new(8552f, 8172f, 736f),
        new(8561f, 8210f, 736f),
        new(8476f, 8114f, 736f)
    ];
    private static readonly ExpectedTerrainPatch[] ExpectedTerrainPatches =
    [
        new(
            "terrain-vertex-count-hp",
            0x6A3C04C,
            1,
            "a318c24216defe206feeb73ef5be00033fa9c4a74d0b967f6532a26ca5906d3b",
            "cdb4ee2aea69cc6a83331bbe96dc2caa9a299d21329efb0336fc02a82e1839a8"),
        new(
            "terrain-face-count-hp",
            0x6A3C04E,
            1,
            "36a9e7f1c95b82ffb99743e0c5c4ce95d83c9a430aac59f84ef3cbfab6145068",
            "bb7208bc9b5d7c04f1236a82a0093a5e33f40423d5ba8d4266f7092c3ba43b62"),
        new(
            "terrain-sector-repack-hp-add-copy",
            0x6A3C180,
            916,
            "ef9ce6afbff7a4df0a8d359004819957287b10a37a9847b6163a495dfd5d11fa",
            "70b28ec2405d4a47b4896123f4f85090f3007940984f0c76c318053b860a73d9"),
        new(
            "collision-index-block-tree",
            0x6A40DE0,
            27090,
            "624f40b710336edcd00f5a938357e933db551e8f858af9ea38ba31e5de215b2d",
            "7fc5ac1f4d97a99703930b2895ede36ddc0873e8b360048adc8511ca80a47a93"),
        new(
            "collision-index-blocks",
            0x6A47840,
            95232,
            "c924883afbda930a756f4a5c29620c339bc1489cf4a5d3712ad0ec09527d4d66",
            "cf5f75571a18db3d75d9035b6e86cbb2e8f5d28563a0b5ecaaf3350d5ee05b4c"),
        new(
            "collision-triangle-add-copy",
            0x6A5F1C4,
            12,
            "f085f169d288c2aa8c30b8db595c0f34b2e7094d622f4f71238c02b4dc50bb30",
            "eb0df448fd5ebcf0db54810c7836c75c7dd98023e7e164f4181b816b28a553f3")
    ];

    public static LevelDefinition BuildResearchLevelDefinition() => new()
    {
        Key = ResearchLevelKey,
        ScriptKey = "UnusedLevel65Authoring",
        DisplayName = "ID65 Research - Town Square",
        LevelId = LevelId,
        SourceWadEntry = TargetDataWadEntry,
        // Terrain-only on purpose. Object source-table provenance is recorded
        // in the proof but is not enabled in this discriminator.
        SourceTableWadOffset = "",
        SourceTableRelativeOffset = "",
        SourceRecordCount = 0,
        Confidence = "research-only independent ID65 physical clone",
        RuntimeMobyPointer = ""
    };

    public static async Task<UnusedLevel65AuthoredTerrainAddCopyCandidateResult> ExportAsync(
        UnusedLevel65AuthoredTerrainAddCopyCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (Retired)
            throw new InvalidOperationException(RetirementReason);

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
        if (!PathEquals(Path.GetDirectoryName(outputImage), Path.GetDirectoryName(outputCue)))
            throw new InvalidOperationException("The authored ID65 terrain BIN and CUE must share one directory.");

        NativeLevelReplacementBaselineExporter.ValidateCue(baseCue, baseImage, "MODE2/2352");
        string baseHashBefore = await HashFileAsync(baseImage, cancellationToken);
        RequireHash(baseHashBefore, BaseImageSha256, "focused-runtime-passed ID65 display-name BIN");
        BaseLayout baseLayout = InspectBaseLayout(baseImage);

        string outputDirectory = Path.GetDirectoryName(outputImage)!;
        Directory.CreateDirectory(outputDirectory);
        string operationId = Guid.NewGuid().ToString("N");
        string supportDirectory = Path.Combine(outputDirectory, $".id65-authored-terrain-{operationId}.work");
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
            Directory.CreateDirectory(supportDirectory);
            LevelDefinition level = BuildResearchLevelDefinition();
            PreparedTerrain prepared = await PrepareTerrainAsync(
                baseImage,
                baseCue,
                outputImage,
                outputCue,
                supportDirectory,
                level,
                cancellationToken);

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
                foreach (TerrainPatch patch in prepared.PatchPlan.Patches)
                {
                    long wadOffset = ParseHexLong(patch.WadRelativeOffset);
                    byte[] before = Convert.FromHexString(NormalizeHex(patch.BeforeHexPreview));
                    byte[] after = Convert.FromHexString(NormalizeHex(patch.AfterHexPreview));
                    byte[] actualBefore = DiscImage.ReadFileBytes(
                        output,
                        layout,
                        WadLba,
                        wadOffset,
                        before.Length);
                    if (!actualBefore.SequenceEqual(before))
                    {
                        throw new InvalidDataException(
                            $"The staged ID65 terrain preimage changed for {patch.Kind} at WAD 0x{wadOffset:X}.");
                    }
                    DiscImage.WriteFileBytes(output, layout, WadLba, wadOffset, after);
                }

                rebuiltRawSectors = RawMode2Form1SectorIntegrity.RebuildFileRanges(
                    output,
                    layout,
                    WadLba,
                    prepared.PatchRanges);
                int verifiedRawSectors = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
                    output,
                    layout,
                    prepared.AffectedRawSectorLbas.Select(lba => (lba, 1)));
                if (rebuiltRawSectors != prepared.AffectedRawSectorLbas.Count ||
                    verifiedRawSectors != prepared.AffectedRawSectorLbas.Count)
                {
                    throw new InvalidDataException(
                        "The authored ID65 terrain candidate did not rebuild and verify its exact raw-sector set.");
                }
                output.Flush(flushToDisk: true);
            }

            Readback readback = VerifyReadback(
                baseImage,
                temporaryImage,
                baseLayout,
                prepared,
                cancellationToken);
            string outputImageSha256 = await HashFileAsync(temporaryImage, cancellationToken);
            if (!string.Equals(outputImageSha256, ExpectedOutputImageSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Authored ID65 terrain candidate SHA-256 is {outputImageSha256}, expected {ExpectedOutputImageSha256}.");
            }
            RequireHash(readback.OutputDataSha256, ExpectedOutputDataSha256, "authored ID65 terrain data payload");
            if (readback.ChangedLogicalWadBytes != ExpectedChangedLogicalWadBytes ||
                readback.ChangedPhysicalImageBytes != ExpectedChangedPhysicalImageBytes ||
                prepared.AffectedRawSectorLbas.Count != ExpectedAffectedRawSectorCount)
            {
                throw new InvalidDataException(
                    $"The pinned authored ID65 diff changed: logical={readback.ChangedLogicalWadBytes}, physical={readback.ChangedPhysicalImageBytes}, sectors={prepared.AffectedRawSectorLbas.Count}.");
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
            RequireHash(baseHashAfter, BaseImageSha256, "base ID65 display-name BIN after terrain export");
            TryDelete(backupImage);
            imageBackedUp = false;
            TryDelete(backupCue);
            cueBackedUp = false;

            UnusedLevel65AuthoredTerrainAddCopyCandidatePlan plan = new(
                DateTimeOffset.UtcNow,
                ProfileId,
                UnusedLevel65DisplayNameCandidateExporter.ProfileId,
                baseHashBefore,
                BaseExecutableSha256,
                LevelId,
                ContinuousLevelIndex,
                ResearchLevelKey,
                TargetOverlayWadEntry,
                TargetDataWadEntry,
                TargetOverlayWadOffset,
                TargetOverlayByteLength,
                TargetDataWadOffset,
                TargetDataByteLength,
                RetailTownSquareOverlayWadOffset,
                RetailTownSquareOverlayByteLength,
                RetailTownSquareDataWadOffset,
                RetailTownSquareDataByteLength,
                TownSquareToId65Rebase,
                FutureMobyTableWadOffset,
                FutureMobyRecordCount,
                BaseOverlaySha256,
                BaseDataSha256,
                prepared.Overlay.SceneWadOffset,
                prepared.Overlay.SectorCount,
                prepared.Overlay.HpFaces,
                prepared.Overlay.LpFaces,
                prepared.SourceSearch.SectorCount,
                prepared.SourceSearch.MatchedSectorCount,
                prepared.FacePlan,
                prepared.PatchPlan,
                prepared.AffectedRawSectorLbas,
                RuntimeChecklist(),
                RequiresDuckStationRuntimeProof: true);
            return new UnusedLevel65AuthoredTerrainAddCopyCandidateResult(
                outputImage,
                outputCue,
                outputImageSha256,
                readback.OutputDataSha256,
                plan,
                readback.ChangedLogicalWadBytes,
                readback.ChangedPhysicalImageBytes,
                rebuiltRawSectors,
                readback.ChangedRawSectorCount,
                ExactLogicalDiffBoundaryVerified: true,
                ExactPhysicalSectorBoundaryVerified: true,
                TerrainPatchReadbackVerified: true,
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
                    "Authored ID65 terrain export failed and prior output recovery was incomplete.",
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
            TryDeleteDirectory(supportDirectory);
        }
    }

    private static async Task<PreparedTerrain> PrepareTerrainAsync(
        string baseImage,
        string baseCue,
        string outputImage,
        string outputCue,
        string supportDirectory,
        LevelDefinition level,
        CancellationToken cancellationToken)
    {
        string analysisPath = Path.Combine(supportDirectory, "id65-wad-analysis.json");
        WadAnalysisBuildResult analysis = await WadAnalysisBuilder.BuildAsync(
            baseImage,
            analysisPath,
            cancellationToken: cancellationToken);
        if (analysis.EntryCount <= TargetDataWadEntry)
        {
            throw new InvalidDataException(
                $"The authored ID65 base exposes only {analysis.EntryCount} parsed WAD rows and is missing row {TargetDataWadEntry}.");
        }

        string overlayPath = Path.Combine(supportDirectory, "id65-runtime-scene-editor-overlay.json");
        SourceSceneOverlayResult overlay = await SourceSceneOverlayExporter.ExportAsync(
            baseImage,
            analysisPath,
            level,
            overlayPath,
            cancellationToken: cancellationToken);
        if (overlay.WadEntry != TargetDataWadEntry || overlay.SceneWadOffset != SourceSceneWadOffset)
        {
            throw new InvalidDataException(
                $"The regenerated ID65 scene starts at WAD 0x{overlay.SceneWadOffset:X} in row {overlay.WadEntry}, expected 0x{SourceSceneWadOffset:X} in row {TargetDataWadEntry}.");
        }

        GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
        TerrainPolygon face = geometry.Polygons.SingleOrDefault(candidate =>
            string.Equals(candidate.RuntimeKey, SourceFaceRuntimeKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"The regenerated ID65 scene does not contain {SourceFaceRuntimeKey}.");
        VerifySourceFace(face);

        string sourceSearchPath = Path.Combine(supportDirectory, "id65-terrain-source-search-native.json");
        TerrainSourceSearchResult sourceSearch = await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(
            new SourceDerivedTerrainSourceSearchRequest(
                baseImage,
                sourceSearchPath,
                level,
                geometry),
            cancellationToken);
        TerrainSourceSearchEntry sourceEntry = sourceSearch.Report.Results.Single(entry =>
            string.Equals(entry.Edit, SourceFaceRuntimeKey, StringComparison.OrdinalIgnoreCase));
        if (sourceEntry.FullSectorHits.Count != 1 ||
            ParseHexLong(sourceEntry.FullSectorHits[0].WadOffset) != SourceFaceSectorWadOffset)
        {
            throw new InvalidDataException("The ID65 authored face did not resolve to exactly one row-80 source sector.");
        }

        face.ApplyTerrainVertexDeltas(AuthoredDeltaZ);
        face.ApplyTerrainVertexXYDeltas(
            face.OriginalPoints.Select(_ => new Vector2f(AuthoredDeltaX, AuthoredDeltaY)).ToArray());
        face.StageTerrainAddClone();
        string terrainEditsPath = Path.Combine(supportDirectory, "id65-authored-terrain-edits.json");
        int editCount = await TerrainEditStore.SaveAsync(
            terrainEditsPath,
            [face],
            "ID65 first authored collidable terrain add-copy");
        if (editCount != 1)
            throw new InvalidDataException($"The ID65 terrain discriminator saved {editCount} edits instead of one.");

        TerrainPatchPlan patchPlan = TerrainPatchExporter.BuildPlan(
            baseImage,
            baseCue,
            outputImage,
            outputCue,
            level,
            "",
            sourceSearchPath,
            terrainEditsPath,
            "");
        VerifyPatchPlan(patchPlan);
        IReadOnlyList<(long Offset, int ByteLength)> patchRanges = patchPlan.Patches
            .Select(patch => (ParseHexLong(patch.WadRelativeOffset), patch.ByteLength))
            .OrderBy(range => range.Item1)
            .ToArray();
        VerifyNonOverlappingRanges(patchRanges);
        int[] affectedRawSectorLbas = BuildAffectedRawSectorLbas(patchRanges);

        Vector3f[] authoredVertices = face.Points
            .Select((point, index) => new Vector3f(point.X, point.Y, face.ZValues[index]))
            .ToArray();
        UnusedLevel65AuthoredTerrainAddCopyFacePlan facePlan = new(
            face.RuntimeKey,
            face.TextureId,
            face.SectorIndex,
            face.SectorOffset,
            SourceFaceWadOffset,
            ExpectedOriginalVertices,
            authoredVertices,
            AuthoredDeltaX,
            AuthoredDeltaY,
            AuthoredDeltaZ);
        return new PreparedTerrain(
            overlay,
            sourceSearch.Report,
            patchPlan,
            patchRanges,
            affectedRawSectorLbas,
            facePlan);
    }

    private static void VerifySourceFace(TerrainPolygon face)
    {
        if (face.TextureId != SourceFaceTextureId ||
            face.SectorIndex != 201 ||
            face.FaceIndex != 0 ||
            face.SectorOffset != SourceFaceSectorWadOffset ||
            face.Points.Count != ExpectedOriginalVertices.Length ||
            face.ZValues.Length != ExpectedOriginalVertices.Length ||
            !string.Equals(face.Detail, "hp", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The exact native source face for the authored ID65 terrain discriminator changed.");
        }

        for (int index = 0; index < ExpectedOriginalVertices.Length; index++)
        {
            Vector3f expected = ExpectedOriginalVertices[index];
            if (Math.Abs(face.Points[index].X - expected.X) > 0.001f ||
                Math.Abs(face.Points[index].Y - expected.Y) > 0.001f ||
                Math.Abs(face.ZValues[index] - expected.Z) > 0.001f)
            {
                throw new InvalidDataException($"The exact native source face vertex {index} changed.");
            }
        }
    }

    private static void VerifyPatchPlan(TerrainPatchPlan plan)
    {
        if (plan.Patches.Count != ExpectedTerrainPatches.Length ||
            plan.PatchCount != ExpectedTerrainPatches.Length ||
            plan.TotalPatchedBytes != ExpectedTerrainPatches.Sum(patch => patch.ByteLength))
        {
            throw new InvalidDataException(
                $"The ID65 terrain plan has {plan.Patches.Count} patch record(s) and {plan.TotalPatchedBytes} patched byte(s); expected {ExpectedTerrainPatches.Length} and {ExpectedTerrainPatches.Sum(patch => patch.ByteLength)}.");
        }

        for (int index = 0; index < ExpectedTerrainPatches.Length; index++)
        {
            ExpectedTerrainPatch expected = ExpectedTerrainPatches[index];
            TerrainPatch actual = plan.Patches[index];
            byte[] before = Convert.FromHexString(NormalizeHex(actual.BeforeHexPreview));
            byte[] after = Convert.FromHexString(NormalizeHex(actual.AfterHexPreview));
            long actualOffset = ParseHexLong(actual.WadRelativeOffset);
            if (!string.Equals(actual.Kind, expected.Kind, StringComparison.Ordinal) ||
                actualOffset != expected.WadOffset ||
                actual.ByteLength != expected.ByteLength ||
                before.Length != expected.ByteLength ||
                after.Length != expected.ByteLength ||
                !string.Equals(Hash(before), expected.BeforeSha256, StringComparison.Ordinal) ||
                !string.Equals(Hash(after), expected.AfterSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"The exact ID65 terrain patch {index} changed ({actual.Kind} at 0x{actualOffset:X}+{actual.ByteLength}).");
            }
        }

        TerrainPatch repack = plan.Patches.Single(patch =>
            string.Equals(patch.Kind, "terrain-sector-repack-hp-add-copy", StringComparison.Ordinal));
        byte[] repackedBytes = Convert.FromHexString(NormalizeHex(repack.AfterHexPreview));
        byte[] expectedCloneTail =
        [
            0x2B, 0x2B, 0x2C, 0x2D,
            0x00, 0x00, 0x02, 0x01,
            0x1B, 0x00, 0xA0, 0x01,
            0x10, 0xC4, 0x10, 0x00
        ];
        if (!repackedBytes.AsSpan(repackedBytes.Length - expectedCloneTail.Length).SequenceEqual(expectedCloneTail))
        {
            throw new InvalidDataException(
                "The copied native face no longer ends with four independent cloned vertex references 2B,2B,2C,2D.");
        }

        string[] kinds = plan.Patches.Select(patch => patch.Kind).ToArray();
        string[] requiredKinds =
        [
            "terrain-vertex-count-hp",
            "terrain-face-count-hp",
            "terrain-sector-repack-hp-add-copy",
            "collision-triangle-add-copy"
        ];
        if (requiredKinds.Any(kind => !kinds.Contains(kind, StringComparer.OrdinalIgnoreCase)))
            throw new InvalidDataException("The ID65 terrain plan lost an independent-vertex, face-count, repack, or collision patch.");
        if (plan.Patches.Any(patch =>
                string.Equals(patch.Kind, "terrain-face-append-hp", StringComparison.OrdinalIgnoreCase) ||
                patch.Kind.StartsWith("visual-", StringComparison.OrdinalIgnoreCase) ||
                patch.Kind.Contains("texture", StringComparison.OrdinalIgnoreCase) ||
                patch.Kind.Contains("surface", StringComparison.OrdinalIgnoreCase) ||
                patch.Kind.Contains("side-wall", StringComparison.OrdinalIgnoreCase) ||
                patch.Kind.Contains("remove", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("The ID65 terrain plan escaped the one-face independent add-copy boundary.");
        }
        if (plan.Patches.Count(patch =>
                string.Equals(patch.Kind, "terrain-sector-repack-hp-add-copy", StringComparison.OrdinalIgnoreCase)) != 1 ||
            plan.Patches.Count(patch =>
                string.Equals(patch.Kind, "collision-triangle-add-copy", StringComparison.OrdinalIgnoreCase)) < 1 ||
            plan.SkippedEdits.Count != 0 ||
            plan.CustomTextureImportCount != 0 ||
            plan.NativeTextureRelocationCount != 0 ||
            plan.TerrainSideWalls.Count != 0)
        {
            throw new InvalidDataException("The ID65 terrain plan is partial, skipped, or includes an excluded texture/side-wall edit.");
        }

        foreach (TerrainPatch patch in plan.Patches)
        {
            long start = ParseHexLong(patch.WadRelativeOffset);
            long end = checked(start + patch.ByteLength);
            if (start < TargetDataWadOffset || end > TargetDataWadOffset + TargetDataByteLength)
            {
                throw new InvalidDataException(
                    $"The ID65 terrain plan writes {patch.Kind} outside WAD row 80 at 0x{start:X}..0x{end:X}.");
            }
            if (RangesOverlap(
                    start,
                    end,
                    RetailTownSquareDataWadOffset,
                    RetailTownSquareDataWadOffset + RetailTownSquareDataByteLength))
            {
                throw new InvalidDataException("The ID65 terrain plan overlaps retail Town Square data.");
            }
        }
    }

    private static BaseLayout InspectBaseLayout(string baseImage)
    {
        DiscLayout layout = DiscImage.DetectLayout(baseImage);
        if (layout.SectorSize != RawSectorBytes || layout.UserOffset != UserOffset)
            throw new InvalidDataException("The authored ID65 terrain experiment requires the checked MODE2/2352 layout.");
        using FileStream input = File.OpenRead(baseImage);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(
            input,
            layout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord executable = DiscImage.FindRootFileRecord(
            input,
            layout,
            IsExecutableName);
        if (wad.Lba != WadLba || wad.Size != ExpectedWadByteLength)
            throw new InvalidDataException("The focused-runtime-passed ID65 WAD extent changed.");
        if (executable.Lba != ExecutableLba || executable.Size != ExecutableByteLength)
            throw new InvalidDataException("The focused-runtime-passed relocated SCUS extent changed.");

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
            throw new InvalidDataException("The passed ID65 overlay/data are no longer exact physical Town Square clones.");
        RequireHash(Hash(targetOverlayBytes), BaseOverlaySha256, "ID65 overlay payload");
        RequireHash(Hash(targetDataBytes), BaseDataSha256, "ID65 data payload");

        byte[] executableBytes = DiscImage.ReadFileBytes(input, layout, executable.Lba, 0, executable.Size);
        RequireHash(Hash(executableBytes), BaseExecutableSha256, "ID65 display-name executable");
        return new BaseLayout(
            layout,
            wad,
            executable,
            Hash(retailOverlayBytes),
            Hash(retailDataBytes),
            Hash(executableBytes));
    }

    private static Readback VerifyReadback(
        string baseImagePath,
        string outputImagePath,
        BaseLayout baseLayout,
        PreparedTerrain prepared,
        CancellationToken cancellationToken)
    {
        DiscLayout outputLayout = DiscImage.DetectLayout(outputImagePath);
        using FileStream baseline = File.OpenRead(baseImagePath);
        using FileStream output = File.OpenRead(outputImagePath);
        if (baseLayout.Layout != outputLayout || baseline.Length != output.Length)
            throw new InvalidDataException("The authored ID65 terrain candidate changed the disc layout or raw length.");

        DiscFileRecord outputWad = DiscImage.FindRootFileRecord(
            output,
            outputLayout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord outputExecutable = DiscImage.FindRootFileRecord(output, outputLayout, IsExecutableName);
        if (outputWad != baseLayout.Wad || outputExecutable != baseLayout.Executable)
            throw new InvalidDataException("The authored ID65 terrain candidate moved or resized WAD.WAD or SCUS.");

        foreach (TerrainPatch patch in prepared.PatchPlan.Patches)
        {
            long wadOffset = ParseHexLong(patch.WadRelativeOffset);
            byte[] expected = Convert.FromHexString(NormalizeHex(patch.AfterHexPreview));
            byte[] actual = DiscImage.ReadFileBytes(output, outputLayout, WadLba, wadOffset, expected.Length);
            if (!actual.SequenceEqual(expected))
                throw new InvalidDataException($"Readback failed for {patch.Kind} at WAD 0x{wadOffset:X}.");
        }

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
        if (outputRetailOverlayHash != baseLayout.RetailOverlaySha256 ||
            outputRetailDataHash != baseLayout.RetailDataSha256 ||
            outputTargetOverlayHash != BaseOverlaySha256 ||
            outputExecutableHash != BaseExecutableSha256)
        {
            throw new InvalidDataException("Retail Town Square, the ID65 overlay, or SCUS changed outside the terrain-only candidate.");
        }

        LogicalDiff logical = CompareLogicalWad(
            baseline,
            output,
            baseLayout.Layout,
            baseLayout.Wad,
            prepared.PatchRanges,
            cancellationToken);
        if (logical.OutsideAllowedBytes != 0 || logical.ChangedBytes <= 0)
        {
            throw new InvalidDataException(
                $"The ID65 terrain logical diff changed {logical.ChangedBytes} byte(s), including {logical.OutsideAllowedBytes} outside the exact patch ranges.");
        }
        PhysicalDiff physical = ComparePhysicalImages(
            baseline,
            output,
            prepared.AffectedRawSectorLbas,
            cancellationToken);
        if (physical.OutsideAllowedSectorBytes != 0 ||
            physical.ChangedBytes <= 0 ||
            physical.ChangedRawSectorCount != prepared.AffectedRawSectorLbas.Count)
        {
            throw new InvalidDataException(
                $"The ID65 terrain physical diff changed {physical.ChangedBytes} byte(s), including {physical.OutsideAllowedSectorBytes} outside {prepared.AffectedRawSectorLbas.Count} allowed sector(s).");
        }
        int verifiedRawSectors = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            output,
            outputLayout,
            prepared.AffectedRawSectorLbas.Select(lba => (lba, 1)));
        if (verifiedRawSectors != prepared.AffectedRawSectorLbas.Count)
            throw new InvalidDataException("The final authored ID65 terrain raw-sector verification count changed.");

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
            throw new InvalidDataException("The authored ID65 terrain raw diff requires equal complete MODE2/2352 images.");
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
            throw new InvalidDataException("The authored ID65 terrain candidate changed an unexpected raw-sector set.");
        return new PhysicalDiff(changed, outside, changedSectors.Count);
    }

    private static IReadOnlyList<string> RuntimeChecklist() =>
    [
        "Disable every DuckStation cheat and memory-card insertion, then cold boot this exact CUE without resuming a save state.",
        $"From controllable gameplay, press Select to open Inventory; enter {TestLevelWarpPatch.ActivationSequence}; then press Left, then Down. Confirm ID65 still displays TOWN SQUARE, loads normally, begins with the expected Gnasty's Loot initial music, and still has the deliberately unauthored /0 treasure target.",
        "Find the small authored raised triangle on the flat ground immediately before the line of three loose red gems leading toward a nearby bull (native face 201:0; approximately X 8540-8625, Y 8138-8234, Z 746-752). Do not collect the gems or attack the bull. Confirm the original flat triangle remains and exactly one displaced textured copy is visible.",
        "View the copied face closely and from several ordinary camera angles. Confirm its texture is stable and there is no nearby flicker, stretched geometry, corruption, or invisible duplicate surface.",
        "Walk, stand, jump, and charge across the raised copy, its edges, and the original flat face. Confirm both visible faces are solid and there is no extra or misaligned invisible collision between or around them.",
        "Move around the surrounding area, open pause and Inventory, and confirm nearby terrain, enemies, camera, collision, textures, and the TOWN SQUARE display name remain stable.",
        $"Leave ID65 only by resetting DuckStation. Cold boot and re-enter with Select; {TestLevelWarpPatch.ActivationSequence}; Left, then Down. Confirm the authored face is still present. Do not use Exit Level, Quit Game, Return Home, or saving.",
        $"Reset and load original retail Town Square with Select; {TestLevelWarpPatch.ActivationSequence}; Cross, then Triangle. Confirm the authored raised copy is absent and retail Town Square remains normal.",
        $"Reset and load Gnasty's Loot with Select; {TestLevelWarpPatch.ActivationSequence}; Left, then Right. Confirm it loads normally.",
        $"Reset and load Sunny Flight with Select; {TestLevelWarpPatch.ActivationSequence}; Cross, then Down. Confirm flight controls and timer remain normal.",
        "Record whether the copy disappears only at far camera distance. This first candidate authors HP geometry only; low-detail/far-LOD authoring is a separate next gate, not a failure unless close-range terrain or collision is unstable.",
        "Do not rescue dragons, touch the egg thief, attack or kill enemies, open or break chests, die, insert a memory card, save, use Return Home, use Exit Level or Quit Game, or run longer than 8 minutes. Mobys, textures, surface behavior, totals, alternate music, portals, Return Home, saving, and persistence remain excluded."
    ];

    private static int[] BuildAffectedRawSectorLbas(
        IReadOnlyList<(long Offset, int ByteLength)> ranges)
    {
        SortedSet<int> sectors = [];
        foreach ((long offset, int byteLength) in ranges)
        {
            long first = offset / UserSectorBytes;
            long last = checked((offset + byteLength - 1) / UserSectorBytes);
            for (long sector = first; sector <= last; sector++)
                sectors.Add(checked(WadLba + (int)sector));
        }
        return sectors.ToArray();
    }

    private static void VerifyNonOverlappingRanges(
        IReadOnlyList<(long Offset, int ByteLength)> ranges)
    {
        for (int index = 1; index < ranges.Count; index++)
        {
            (long priorOffset, int priorLength) = ranges[index - 1];
            if (ranges[index].Offset < priorOffset + priorLength)
                throw new InvalidDataException("The authored ID65 terrain patch plan contains overlapping write ranges.");
        }
    }

    private static WadEntry ReadWadEntry(
        FileStream stream,
        DiscLayout layout,
        int index)
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

    private static bool RangesOverlap(long aStart, long aEnd, long bStart, long bEnd) =>
        aStart < bEnd && bStart < aEnd;

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

    private static long ParseHexLong(string value)
    {
        string normalized = value.Trim();
        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[2..];
        return Convert.ToInt64(normalized, 16);
    }

    private static string NormalizeHex(string value) =>
        new(value.Where(char.IsAsciiHexDigit).Select(char.ToUpperInvariant).ToArray());

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static async Task<string> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
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

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record PreparedTerrain(
        SourceSceneOverlayResult Overlay,
        TerrainSourceSearchReport SourceSearch,
        TerrainPatchPlan PatchPlan,
        IReadOnlyList<(long Offset, int ByteLength)> PatchRanges,
        IReadOnlyList<int> AffectedRawSectorLbas,
        UnusedLevel65AuthoredTerrainAddCopyFacePlan FacePlan);

    private sealed record BaseLayout(
        DiscLayout Layout,
        DiscFileRecord Wad,
        DiscFileRecord Executable,
        string RetailOverlaySha256,
        string RetailDataSha256,
        string ExecutableSha256);

    private sealed record WadEntry(int Offset, int Size);
    private sealed record ExpectedTerrainPatch(
        string Kind,
        long WadOffset,
        int ByteLength,
        string BeforeSha256,
        string AfterSha256);
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

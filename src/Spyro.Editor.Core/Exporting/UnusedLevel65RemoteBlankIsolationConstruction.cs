using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal readonly record struct UnusedLevel65RemoteBlankPoint(int X, int Y, int Z);

internal readonly record struct UnusedLevel65RemoteBlankCollisionCell(int X, int Y, int Z);

internal sealed record UnusedLevel65RemoteBlankComponentProof(
    string Name,
    int SourceRelativeOffset,
    int OutputRelativeOffset,
    int ByteLength,
    string SourceSha256,
    string OutputSha256,
    bool ContentsPreserved);

internal sealed record UnusedLevel65RemoteBlankStructuralPatch(
    string Kind,
    int DataRelativeOffset,
    long WadOffset,
    byte[] Before,
    byte[] After);

internal sealed record UnusedLevel65RemoteBlankDiffRange(
    string Kind,
    int DataRelativeOffset,
    long WadOffset,
    int ByteLength,
    string BeforeSha256,
    string AfterSha256);

internal sealed record UnusedLevel65RemoteBlankSceneProof(
    int SourceSectorCount,
    int OutputSectorCount,
    int NewSectorIndex,
    int SourceEnvironmentByteLength,
    int OutputEnvironmentByteLength,
    int EnvironmentGrowthBytes,
    int NewSectorByteLength,
    string NewSectorHex,
    string NewSectorSha256,
    UnusedLevel65RemoteBlankPoint CullCenter,
    int CullRadius,
    int CullFlags,
    UnusedLevel65RemoteBlankPoint EncodingOrigin,
    IReadOnlyList<UnusedLevel65RemoteBlankPoint> Points,
    IReadOnlyList<uint> VertexWords,
    string LowDetailFaceHex,
    string HighDetailFaceHex,
    int MaterialTextureId,
    bool AllInheritedSectorPayloadsPreserved,
    bool ExactHpLpPairing,
    bool OrdinaryCullRoute);

internal sealed record UnusedLevel65RemoteBlankOcclusionProof(
    int GroupCount,
    int Assignment,
    int SourceGroupZeroCount,
    int OutputGroupZeroCount,
    int InsertionRelativeOffset,
    int EnvironmentPortionByteLength,
    IReadOnlyList<int> SourcePointers,
    IReadOnlyList<int> OutputPointers,
    bool GroupZeroInheritedOrderPreserved,
    bool OtherGroupsPreserved,
    bool FixedComponentLength,
    bool TargetSectorOwned);

internal sealed record UnusedLevel65RemoteBlankCollisionProof(
    int TriangleCount,
    int ReusedTriangleIndex,
    string SourceTriangleHex,
    string OutputTriangleHex,
    int SourceAssignment,
    int OutputAssignment,
    UnusedLevel65RemoteBlankCollisionCell SourceCell,
    UnusedLevel65RemoteBlankCollisionCell TargetCell,
    int TargetTreePointerRelativeOffset,
    int SourceOccupiedCellCount,
    int OutputOccupiedCellCount,
    int SourceGroupStartCount,
    int OutputGroupStartCount,
    int BlocksCapacityBytes,
    int OutputBlocksUsedBytes,
    string SourceTreeSha256,
    string OutputTreeSha256,
    string SourceBlocksSha256,
    string OutputBlocksSha256,
    IReadOnlyList<int> SourceCellSequence,
    IReadOnlyList<int> OutputSourceCellSequence,
    IReadOnlyList<int> OutputTargetCellSequence,
    IReadOnlyList<UnusedLevel65RemoteBlankCollisionCell> TargetTouchedCells,
    bool TargetLeafWasVacant,
    bool NativeCellSequencesPreserved,
    bool NativeOrderingPreserved,
    bool UpwardWinding,
    bool FixedComponentLength);

internal sealed record UnusedLevel65RemoteBlankSpawnProof(
    long LandingWadOffset,
    string SourceLandingHex,
    string OutputLandingHex,
    string SourceLandingSha256,
    string OutputLandingSha256,
    int LandingRawX,
    int LandingRawY,
    int LandingRawZ,
    int LandingYaw,
    int PlayerAnchorTrueIndex,
    long PlayerAnchorWadOffset,
    string SourcePlayerAnchorSha256,
    string OutputPlayerAnchorSha256,
    int PlayerRawX,
    int PlayerRawY,
    int PlayerRawZ,
    int GroundRawZ,
    int LandingClearanceRaw,
    int PlayerClearanceRaw,
    int PlayerAboveLandingRaw,
    bool AtomicXyOnly,
    bool DeathPlaneRuntimeVerified,
    bool DeathRespawnRuntimeVerified);

internal sealed record UnusedLevel65RemoteBlankIsolationProof(
    int ScannedInheritedSectorCount,
    int ScannedLowDetailFaceCount,
    int ScannedHighDetailFaceCount,
    int ScannedCollisionTriangleCount,
    int NativeLowDetailInteriorOverlapCount,
    int NativeHighDetailInteriorOverlapCount,
    int NativeCollisionInteriorOverlapCount,
    int NearestNativeCullSectorIndex,
    double MinimumNativeCullSurfaceDistance,
    double MinimumCullSphereSeparation,
    int NearestNativeVertexSectorIndex,
    string NearestNativeVertexLod,
    double MinimumNativeVertexXyDistance,
    int NearestNativeCollisionTriangleIndex,
    double MinimumNativeCollisionVertexXyDistance,
    int InheritedMobyCount,
    int NearestInheritedMobyTrueIndex,
    double MinimumInheritedMobyXyDistance,
    int MinimumInheritedMobyRawX,
    int MaximumInheritedMobyRawX,
    int MinimumInheritedMobyRawY,
    int MaximumInheritedMobyRawY,
    int MinimumInheritedMobyRawZ,
    int MaximumInheritedMobyRawZ,
    bool AuthoredCollisionIsOnlyTargetCellSurface,
    bool NativeMobysPhysicallyDisconnected,
    bool NativeMobyRuntimeInactivityVerified,
    bool FarLodRuntimeRouteVerified);

internal sealed record UnusedLevel65RemoteBlankStaticPlan(
    string ProfileId,
    string SourceImagePath,
    string SourceImageSha256,
    string ExecutableSha256,
    int WadLba,
    int WadByteLength,
    int TargetWadEntry,
    long DataWadOffset,
    int DataByteLength,
    string SourceDataSha256,
    string OutputDataSha256,
    long ModelWadOffset,
    int ModelByteLength,
    string SourceModelSha256,
    string OutputModelSha256,
    int SourceUsedModelByteLength,
    int OutputUsedModelByteLength,
    int SourceZeroTailByteCount,
    int OutputZeroTailByteCount,
    IReadOnlyList<UnusedLevel65RemoteBlankComponentProof> Components,
    UnusedLevel65RemoteBlankSceneProof Scene,
    UnusedLevel65RemoteBlankOcclusionProof Occlusion,
    UnusedLevel65RemoteBlankCollisionProof Collision,
    UnusedLevel65RemoteBlankSpawnProof Spawn,
    UnusedLevel65RemoteBlankIsolationProof Isolation,
    IReadOnlyList<UnusedLevel65RemoteBlankStructuralPatch> StructuralPatches,
    IReadOnlyList<UnusedLevel65RemoteBlankDiffRange> DiffRanges,
    int ChangedDataByteCount,
    string DiffManifestSha256,
    string DeterministicPlanSha256,
    byte[] SourceData,
    byte[] OutputData,
    bool PatchPreimagesVerified,
    bool PatchAllowlistComplete,
    bool ByteInverseVerified,
    bool ProtectedSubfilesPreserved,
    bool InheritedObjectRowsPreserved,
    bool RetailWadEntriesExcluded,
    bool ExecutableExcluded,
    bool SourceImageMutationPossible,
    bool DisposableRuntimeCandidateAuthorized,
    bool PromotionAuthorized,
    bool NormalCreateBinEnabled);

/// <summary>
/// Static-only construction contract for a visually isolated ID65 authoring
/// zone. The contract consumes only the exact locked display-name image and
/// returns preimage-bound in-memory row-80 patches. It never writes a BIN/CUE,
/// touches a retail WAD row or executable, or enables App/Create BIN/release
/// integration.
/// </summary>
internal static class UnusedLevel65RemoteBlankIsolationConstruction
{
    public const string ProfileId =
        "unused-level-65-remote-blank-isolation-sector216-static-clean-usa-v1";
    public const string ExpectedSourceImageSha256 =
        "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
    public const string ExpectedExecutableSha256 =
        "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";
    public const string ExpectedSourceDataSha256 =
        "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";
    public const string ExpectedSourceObjectTableSha256 =
        "2d5743b6895cb6142150812e06eb772b492ab21665edec239e17e98b9ed2af1d";

    public const string ExpectedSourceModelSha256 =
        "1aa6950fe78e71ef4506d823fd33806c13a32838cdb00aadc4e35daffcf17f47";
    public const string ExpectedOutputModelSha256 =
        "72c3fb268f63952ad37707e33be4de4039d4bb73af42e25b52e618e7ec9a5820";
    public const string ExpectedOutputDataSha256 =
        "8d10aa62b134414aec80eec10d1fcede13bd55806ee13f6eb7c752f0fa1a9061";
    public const string ExpectedOutputObjectTableSha256 =
        "f89347baf4f14739030499b21210b8ea46b44f9c4df02a1b58d39fde6ca1e64a";
    public const string ExpectedOutputPlayerAnchorSha256 =
        "2c9d84b35343cbea4366186070e9133ba020db5084ca20dc123cf4057a6c2bd9";
    public const string ExpectedNewSectorSha256 =
        "08f1de7187887ee4bc05bb0dbb0d9fd0c819ad015a39b6f1ae83a1e745ddc07a";
    public const string ExpectedSourceTreeSha256 =
        "ad6ce785e5d49ff97c5fb79c967f2d0bcff2f2e2d40ef1137cdce0114e4ea6ed";
    public const string ExpectedSourceBlocksSha256 =
        "c2e0d178d39cd8ce8439d44d64987c81e60c93e0eb50f43c4a7ce51a258250d5";
    public const string ExpectedOutputTreeSha256 =
        "c77618c5a80fce590c802b197945348d0b4fc04ab666e9b0b911e3a6ff9b2cb9";
    public const string ExpectedOutputBlocksSha256 =
        "2d5838ffd9c991d017733f48cadbb9739131fbd6cec75f69a831c03442c1f34a";
    public const string ExpectedDiffManifestSha256 =
        "a2d5e80c07435b975e26f4debba010e33a93b515e0bb426d25fd2276cfcef205";
    public const string ExpectedDeterministicPlanSha256 =
        "18b92771741a05d5833670a416f076e414778768cb883e786a07b88fba1ebb79";
    public const int ExpectedChangedDataByteCount = 484_336;
    public const int ExpectedDiffRangeCount = 55_386;
    public const int ExpectedOutputBlocksUsedBytes = 0x17960;
    public const int ExpectedNearestCullSectorIndex = 45;
    public const double ExpectedMinimumCullSurfaceDistance = 2_158.980153369932;
    public const double ExpectedMinimumCullSphereSeparation = 1_966.980153369932;
    public const int ExpectedNearestVertexSectorIndex = 45;
    public const string ExpectedNearestVertexLod = "LP";
    public const double ExpectedMinimumVertexXyDistance = 2_701.267850473181;
    public const int ExpectedNearestCollisionTriangleIndex = 19_022;
    public const double ExpectedMinimumCollisionVertexXyDistance = 2_701.267850473181;
    public const int ExpectedNearestMobyTrueIndex = 64;
    public const double ExpectedMinimumMobyXyDistance = 8_106.099746342642;

    public const int WadLba = UnusedLevel65FullAuthoringConstructionTemplate.WadLba;
    public const int WadByteLength = UnusedLevel65FullAuthoringConstructionTemplate.ExpectedWadByteLength;
    public const int TargetWadEntry = 80;
    public const long DataWadOffset = UnusedLevel65FullAuthoringConstructionTemplate.DataEntryWadOffset;
    public const int DataByteLength = UnusedLevel65FullAuthoringConstructionTemplate.DataEntryByteLength;
    public const long ModelWadOffset = UnusedLevel65FullAuthoringConstructionTemplate.ModelSubfileWadOffset;
    public const int ModelByteLength = UnusedLevel65FullAuthoringConstructionTemplate.ModelSubfileByteLength;
    public const int ModelDataRelativeOffset = checked((int)(ModelWadOffset - DataWadOffset));
    public const int SourceSectorCount = 216;
    public const int OutputSectorCount = 217;
    public const int NewSectorIndex = 216;
    public const int EnvironmentGrowthBytes = 0x74;
    public const int NewSectorByteLength = 0x70;
    public const int SourceEnvironmentByteLength = 0x284A4;
    public const int OutputEnvironmentByteLength = 0x28518;
    public const int SourceUsedModelByteLength = 0x94508;
    public const int OutputUsedModelByteLength = 0x9457C;
    public const int SourceZeroTailByteCount = 0x2F8;
    public const int OutputZeroTailByteCount = 0x284;
    public const int MaterialTextureId = 25;
    public const int ReusedCollisionTriangleIndex = 13_995;
    public const int CollisionTriangleCount = 19_808;
    public const int TargetTreePointerRelativeOffset = 0x1254;
    public const int CollisionBlocksCapacityBytes = 0x17984;
    public const int LandingDataRelativeOffset = checked((int)(
        UnusedLevel65FullAuthoringConstructionTemplate.LandingWadOffset - DataWadOffset));
    public const int PlayerAnchorDataRelativeOffset = checked((int)(
        UnusedLevel65FullAuthoringConstructionTemplate.PlayerAnchorWadOffset - DataWadOffset));
    public const int PlayerAnchorXyDataRelativeOffset = PlayerAnchorDataRelativeOffset + 0x0C;
    public const int AuthoredRawX = 6_153;
    public const int AuthoredRawY = 6_154;
    public const int LandingRawZ = 8_550;
    public const int PlayerRawZ = 8_704;
    public const int GroundRawZ = 8_192;
    public const int YawByte = 0x40;

    private const int TextureComponentByteLength = 0x2F78;
    private const int OcclusionComponentByteLength = 0x974;
    private const int SpecialComponentByteLength = 0x30;
    private const int CollisionComponentByteLength = 0x5FAE8;
    private const int CycloramaComponentByteLength = 0x84E4;
    private const int PortalComponentByteLength = 4;
    private const int ParticleComponentByteLength = 0x80;
    private const int SoundComponentByteLength = 0x6F8;
    private const int CollisionFlagsByteLength = 0x2904;
    private const int CollisionTreeRelativeOffset = 0x1C;
    private const int CollisionBlocksRelativeOffset = 0x6A7C;
    private const int CollisionTrianglesRelativeOffset = 0x1E400;
    private const int CollisionAssignmentsRelativeOffset = 0x58480;
    private const int CollisionFlagsRelativeOffset = 0x5D1E0;
    private const int ObjectTableDataRelativeOffset = checked((int)(
        UnusedLevel65FullAuthoringConstructionTemplate.ObjectTableWadOffset - DataWadOffset));
    private const int ObjectRecordCount = UnusedLevel65FullAuthoringConstructionTemplate.ObjectRecordCount;
    private const int ObjectRecordByteLength = 0x58;
    private const int OcclusionInsertionRelativeOffset = 0xC8;
    private const int OcclusionEnvironmentPortionByteLength = 0x71C;
    private const int NewCullRadius = 192;

    private static readonly UnusedLevel65RemoteBlankPoint CullCenter = new(384, 384, 512);
    private static readonly UnusedLevel65RemoteBlankPoint EncodingOrigin = new(256, 256, 512);
    private static readonly UnusedLevel65RemoteBlankPoint[] AuthoredPoints =
    [
        new(272, 272, 512),
        new(496, 272, 512),
        new(384, 496, 512)
    ];
    private static readonly uint[] AuthoredVertexWords = [0x02004000, 0x1E004000, 0x1003C000];
    private static readonly UnusedLevel65RemoteBlankCollisionCell SourceCollisionCell = new(34, 35, 1);
    private static readonly UnusedLevel65RemoteBlankCollisionCell TargetCollisionCell = new(1, 1, 2);
    private static readonly int[] ExpectedSourceCollisionSequence =
    [
        14_001, 14_000, 13_999, 13_998, 13_995, 11_276, 11_234, 11_197,
        11_196, 11_191, 11_190, 11_184, 3_130, 3_129, 3_128, 3_066,
        3_065, 3_064, 3_060, 3_058, 3_057, 3_056, 3_055
    ];
    private static readonly byte[] SourceCollisionTriangle =
        Convert.FromHexString("A7A2140044631900E0010000");
    private static readonly byte[] OutputCollisionTriangle =
        Convert.FromHexString("100138381001007000020000");
    private static readonly byte[] SourceLanding =
        Convert.FromHexString("29E901009A8801006621000000004000");
    private static readonly byte[] OutputLanding =
        Convert.FromHexString("091800000A1800006621000000004000");

    public static UnusedLevel65RemoteBlankStaticPlan BuildStaticPlan(string sourceImagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        string sourcePath = Path.GetFullPath(sourceImagePath);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("The exact locked ID65 display-name BIN is missing.", sourcePath);

        string sourceImageSha256 = HashFile(sourcePath);
        RequireHash(sourceImageSha256, ExpectedSourceImageSha256, "locked display-name BIN");
        DiscLayout layout = DiscImage.DetectLayout(sourcePath);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("The locked display-name BIN is not exact MODE2/2352.");

        using FileStream image = File.OpenRead(sourcePath);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(
            image,
            layout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        if (wad.Lba != WadLba || wad.Size != WadByteLength)
            throw new InvalidDataException("The exact locked WAD.WAD mapping changed.");
        byte[] row = DiscImage.ReadFileBytes(image, layout, wad.Lba, TargetWadEntry * 8L, 8);
        if (ReadUInt32(row, 0) != DataWadOffset || ReadUInt32(row, 4) != DataByteLength)
            throw new InvalidDataException("The exact row-80 directory entry changed.");
        byte[] sourceData = DiscImage.ReadFileBytes(
            image,
            layout,
            wad.Lba,
            DataWadOffset,
            DataByteLength);

        DiscFileRecord executable = DiscImage.FindRootFileRecord(
            image,
            layout,
            name => string.Equals(name, "SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        if (executable.Lba != 55_382 || executable.Size != 0x66000)
            throw new InvalidDataException("The exact relocated executable mapping changed.");
        byte[] executableBytes = DiscImage.ReadFileBytes(
            image,
            layout,
            executable.Lba,
            0,
            executable.Size);
        string executableSha256 = Hash(executableBytes);
        RequireHash(executableSha256, ExpectedExecutableSha256, "relocated executable");

        Composition composition = ComposePinnedData(sourceData, requireWholeDataHash: true);
        RequirePinnedComposition(composition);
        return ToPlan(sourcePath, sourceImageSha256, executableSha256, composition);
    }

    internal static void ValidatePinnedDataForNegativeSmoke(byte[] sourceData)
    {
        ArgumentNullException.ThrowIfNull(sourceData);
        _ = ComposePinnedData(sourceData, requireWholeDataHash: false);
    }

    internal static byte[] ApplyTransactional(
        UnusedLevel65RemoteBlankStaticPlan plan,
        byte[] input,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(input);
        if (input.Length != DataByteLength)
            throw new InvalidDataException("The transactional ID65 buffer has the wrong byte length.");
        IReadOnlyList<UnusedLevel65RemoteBlankStructuralPatch> patches = plan.StructuralPatches;
        ValidatePatchSet(patches);

        foreach (UnusedLevel65RemoteBlankStructuralPatch patch in patches)
        {
            byte[] expected = reverse ? patch.After : patch.Before;
            if (!input.AsSpan(patch.DataRelativeOffset, expected.Length).SequenceEqual(expected))
            {
                throw new InvalidDataException(
                    $"The transactional {patch.Kind} {(reverse ? "afterimage" : "preimage")} changed.");
            }
        }

        byte[] output = input.ToArray();
        foreach (UnusedLevel65RemoteBlankStructuralPatch patch in patches)
        {
            byte[] replacement = reverse ? patch.Before : patch.After;
            replacement.CopyTo(output, patch.DataRelativeOffset);
        }
        return output;
    }

    private static Composition ComposePinnedData(byte[] inputData, bool requireWholeDataHash)
    {
        if (inputData.Length != DataByteLength)
            throw new InvalidDataException($"ID65 row-80 data is 0x{inputData.Length:X} bytes, expected 0x{DataByteLength:X}.");
        byte[] sourceData = inputData.ToArray();
        string sourceDataSha256 = Hash(sourceData);
        if (requireWholeDataHash)
            RequireHash(sourceDataSha256, ExpectedSourceDataSha256, "locked row-80 data");

        RequireEntryHeader(sourceData);
        byte[] sourceModel = sourceData.AsSpan(ModelDataRelativeOffset, ModelByteLength).ToArray();
        ModelLayout sourceLayout = ParseModel(sourceModel);
        RequireSourceLayout(sourceModel, sourceLayout);
        RequireSpawnPreimages(sourceData);
        IsolationScan isolation = ScanIsolation(sourceData, sourceModel, sourceLayout);

        byte[] outputEnvironment = BuildEnvironment(sourceModel, sourceLayout, out byte[] newSector);
        byte[] sourceOcclusion = ComponentBytes(sourceModel, sourceLayout, "occlusion");
        byte[] outputOcclusion = BuildOcclusion(sourceOcclusion, out OcclusionBuild occlusionBuild);
        byte[] sourceCollision = ComponentBytes(sourceModel, sourceLayout, "collision");
        byte[] outputCollision = BuildCollision(sourceCollision, out CollisionBuild collisionBuild);
        byte[] outputModel = BuildOutputModel(
            sourceModel,
            sourceLayout,
            outputEnvironment,
            outputOcclusion,
            outputCollision);
        ModelLayout outputLayout = ParseModel(outputModel);
        RequireOutputLayout(
            sourceModel,
            sourceLayout,
            outputModel,
            outputLayout,
            newSector,
            outputOcclusion,
            outputCollision,
            occlusionBuild,
            collisionBuild);

        byte[] landingAfter = OutputLanding.AsSpan(0, 8).ToArray();
        byte[] playerXyBefore = sourceData.AsSpan(PlayerAnchorXyDataRelativeOffset, 8).ToArray();
        byte[] playerXyAfter = new byte[8];
        WriteInt32(playerXyAfter, 0, AuthoredRawX);
        WriteInt32(playerXyAfter, 4, AuthoredRawY);
        UnusedLevel65RemoteBlankStructuralPatch[] patches =
        [
            new(
                "id65-model",
                ModelDataRelativeOffset,
                ModelWadOffset,
                sourceModel,
                outputModel),
            new(
                "id65-landing-xy",
                LandingDataRelativeOffset,
                DataWadOffset + LandingDataRelativeOffset,
                sourceData.AsSpan(LandingDataRelativeOffset, 8).ToArray(),
                landingAfter),
            new(
                "id65-t92-player-anchor-xy",
                PlayerAnchorXyDataRelativeOffset,
                DataWadOffset + PlayerAnchorXyDataRelativeOffset,
                playerXyBefore,
                playerXyAfter)
        ];
        ValidatePatchSet(patches);
        byte[] outputData = ApplyPatches(sourceData, patches, reverse: false);
        if (!outputData.AsSpan(LandingDataRelativeOffset, SourceLanding.Length).SequenceEqual(OutputLanding))
            throw new InvalidDataException("The coupled authored landing failed exact readback.");
        byte[] outputPlayerAnchor = outputData.AsSpan(PlayerAnchorDataRelativeOffset, ObjectRecordByteLength).ToArray();
        RequireInt32(outputPlayerAnchor, 0x0C, AuthoredRawX, "output T92 X");
        RequireInt32(outputPlayerAnchor, 0x10, AuthoredRawY, "output T92 Y");
        RequireInt32(outputPlayerAnchor, 0x14, PlayerRawZ, "output T92 Z");
        RequireObjectTableIsolation(sourceData, outputData, outputPlayerAnchor);
        RequireProtectedSubfiles(sourceData, outputData);

        byte[] inverse = ApplyPatches(outputData, patches, reverse: true);
        bool inverseVerified = inverse.SequenceEqual(sourceData);
        if (!inverseVerified)
            throw new InvalidDataException("The remote blank-isolation patch set is not byte-invertible to the locked base.");

        UnusedLevel65RemoteBlankDiffRange[] diffRanges = BuildDiffRanges(sourceData, outputData, patches);
        int changedDataBytes = diffRanges.Sum(range => range.ByteLength);
        if (CountChangedBytes(sourceData, outputData) != changedDataBytes ||
            diffRanges.Any(range => !RangeAllowed(range, patches)))
        {
            throw new InvalidDataException("The remote blank-isolation diff escaped its exact three-range allowlist.");
        }
        string diffManifestSha256 = HashDiffManifest(diffRanges);
        string sourceModelSha256 = Hash(sourceModel);
        string outputModelSha256 = Hash(outputModel);
        string outputDataSha256 = Hash(outputData);

        UnusedLevel65RemoteBlankComponentProof[] components = BuildComponentProofs(
            sourceModel,
            sourceLayout,
            outputModel,
            outputLayout);
        UnusedLevel65RemoteBlankSceneProof sceneProof = BuildSceneProof(
            sourceModel,
            sourceLayout,
            outputModel,
            outputLayout,
            newSector);
        UnusedLevel65RemoteBlankOcclusionProof occlusionProof = BuildOcclusionProof(occlusionBuild);
        UnusedLevel65RemoteBlankCollisionProof collisionProof = BuildCollisionProof(collisionBuild);
        UnusedLevel65RemoteBlankSpawnProof spawnProof = BuildSpawnProof(
            sourceData,
            outputData,
            outputPlayerAnchor);
        UnusedLevel65RemoteBlankIsolationProof isolationProof = BuildIsolationProof(
            isolation,
            collisionBuild);
        string deterministicPlanSha256 = HashPlanIdentity(
            sourceDataSha256,
            outputDataSha256,
            sourceModelSha256,
            outputModelSha256,
            diffManifestSha256,
            sceneProof,
            occlusionProof,
            collisionProof,
            spawnProof,
            isolationProof);

        return new Composition(
            sourceData,
            outputData,
            sourceModel,
            outputModel,
            sourceLayout,
            outputLayout,
            sourceDataSha256,
            outputDataSha256,
            sourceModelSha256,
            outputModelSha256,
            outputPlayerAnchor,
            components,
            sceneProof,
            occlusionProof,
            collisionProof,
            spawnProof,
            isolationProof,
            patches,
            diffRanges,
            changedDataBytes,
            diffManifestSha256,
            deterministicPlanSha256,
            inverseVerified);
    }

    private static UnusedLevel65RemoteBlankStaticPlan ToPlan(
        string sourcePath,
        string sourceImageSha256,
        string executableSha256,
        Composition composition) =>
        new(
            ProfileId,
            sourcePath,
            sourceImageSha256,
            executableSha256,
            WadLba,
            WadByteLength,
            TargetWadEntry,
            DataWadOffset,
            DataByteLength,
            composition.SourceDataSha256,
            composition.OutputDataSha256,
            ModelWadOffset,
            ModelByteLength,
            composition.SourceModelSha256,
            composition.OutputModelSha256,
            SourceUsedModelByteLength,
            OutputUsedModelByteLength,
            SourceZeroTailByteCount,
            OutputZeroTailByteCount,
            composition.Components,
            composition.Scene,
            composition.Occlusion,
            composition.Collision,
            composition.Spawn,
            composition.Isolation,
            composition.Patches,
            composition.DiffRanges,
            composition.ChangedDataBytes,
            composition.DiffManifestSha256,
            composition.DeterministicPlanSha256,
            composition.SourceData,
            composition.OutputData,
            PatchPreimagesVerified: true,
            PatchAllowlistComplete: true,
            ByteInverseVerified: composition.InverseVerified,
            ProtectedSubfilesPreserved: true,
            InheritedObjectRowsPreserved: true,
            RetailWadEntriesExcluded: true,
            ExecutableExcluded: true,
            SourceImageMutationPossible: false,
            DisposableRuntimeCandidateAuthorized: false,
            PromotionAuthorized: false,
            NormalCreateBinEnabled: false);

    private static byte[] BuildEnvironment(
        byte[] sourceModel,
        ModelLayout sourceLayout,
        out byte[] newSector)
    {
        ComponentLayout environment = sourceLayout.Component("environment");
        byte[] source = sourceModel.AsSpan(environment.Offset, environment.ByteLength).ToArray();
        if (source.Length != SourceEnvironmentByteLength || ReadInt32(source, 4) != SourceSectorCount)
            throw new InvalidDataException("The source environment boundary changed.");
        int sourcePointerTableEnd = 8 + (SourceSectorCount * 4);
        int outputPointerTableEnd = 8 + (OutputSectorCount * 4);
        int sourcePayloadLength = source.Length - sourcePointerTableEnd;
        newSector = BuildNewSector();
        byte[] output = new byte[OutputEnvironmentByteLength];
        WriteInt32(output, 0, output.Length);
        WriteInt32(output, 4, OutputSectorCount);
        for (int index = 0; index < SourceSectorCount; index++)
        {
            int pointer = ReadInt32(source, 8 + (index * 4));
            WriteInt32(output, 8 + (index * 4), checked(pointer + 4));
        }
        WriteInt32(output, 8 + (NewSectorIndex * 4), SourceEnvironmentByteLength);
        source.AsSpan(sourcePointerTableEnd, sourcePayloadLength)
            .CopyTo(output.AsSpan(outputPointerTableEnd));
        int newSectorOffset = outputPointerTableEnd + sourcePayloadLength;
        if (newSectorOffset != SourceEnvironmentByteLength + 4)
            throw new InvalidDataException("The new sector does not begin at its exact appended pointer.");
        newSector.CopyTo(output, newSectorOffset);
        if (newSectorOffset + newSector.Length != output.Length)
            throw new InvalidDataException("The expanded environment was not consumed exactly.");
        return output;
    }

    private static byte[] BuildNewSector()
    {
        byte[] header = Convert.FromHexString(
            "80018001C000000200010001000000020303010003030109FFFFFFFF");
        byte[] vertices = Convert.FromHexString("004000020040001E00C00310");
        byte[] lowColors = Convert.FromHexString("2D39300030552C0037622C00");
        byte[] lowFace = Convert.FromHexString("0082100000821000");
        byte[] highColors = Convert.FromHexString(
            "99756B00775D6600896A6A00775D6600806268007A5F6800");
        byte[] highFace = Convert.FromHexString("000001020101000219024001080A1000");
        byte[] sector = new byte[NewSectorByteLength];
        int cursor = 0;
        foreach (byte[] bytes in new[] { header, vertices, lowColors, lowFace, vertices, highColors, highFace })
        {
            bytes.CopyTo(sector, cursor);
            cursor += bytes.Length;
        }
        if (cursor != sector.Length)
            throw new InvalidDataException("The exact remote HP+LP sector is not 0x70 bytes.");
        return sector;
    }

    private static byte[] BuildOcclusion(byte[] source, out OcclusionBuild build)
    {
        if (source.Length != OcclusionComponentByteLength || ReadInt32(source, 0) != source.Length ||
            ReadInt32(source, 4) != OcclusionEnvironmentPortionByteLength || ReadInt32(source, 8) != 16)
        {
            throw new InvalidDataException("The exact source occlusion directory changed.");
        }
        IReadOnlyList<int>[] sourceGroups = ParseOcclusionGroups(source, SourceSectorCount, out int[] sourcePointers);
        if (sourcePointers[0] != 0x48 || sourceGroups[0].Count != 124 ||
            sourceGroups[0][8] != 213 || source[OcclusionInsertionRelativeOffset] != 0xFF ||
            source[0x71E] != 0xFF || source[0x71F] != 0xFF)
        {
            throw new InvalidDataException("The exact group-0 terminator or terminal FF padding changed.");
        }

        byte[] output = source.ToArray();
        int environmentEnd = 4 + OcclusionEnvironmentPortionByteLength;
        int shiftEndExclusive = environmentEnd - 2;
        Array.Copy(
            source,
            OcclusionInsertionRelativeOffset,
            output,
            OcclusionInsertionRelativeOffset + 1,
            shiftEndExclusive - OcclusionInsertionRelativeOffset);
        output[OcclusionInsertionRelativeOffset] = checked((byte)NewSectorIndex);
        for (int group = 1; group < sourcePointers.Length; group++)
            WriteInt32(output, 12 + (group * 4), checked(sourcePointers[group] + 1));
        output[environmentEnd - 1] = source[environmentEnd - 1];

        IReadOnlyList<int>[] outputGroups = ParseOcclusionGroups(output, OutputSectorCount, out int[] outputPointers);
        if (!outputGroups[0].SequenceEqual(sourceGroups[0].Concat([NewSectorIndex])) ||
            !sourceGroups.Skip(1).Zip(outputGroups.Skip(1)).All(pair => pair.First.SequenceEqual(pair.Second)) ||
            outputPointers[0] != sourcePointers[0] ||
            !outputPointers.Skip(1).SequenceEqual(sourcePointers.Skip(1).Select(pointer => pointer + 1)))
        {
            throw new InvalidDataException("The fixed-length group-0 occlusion insertion failed semantic readback.");
        }
        build = new(sourceGroups, outputGroups, sourcePointers, outputPointers);
        return output;
    }

    private static byte[] BuildCollision(byte[] source, out CollisionBuild build)
    {
        CollisionLayout layout = ParseCollisionLayout(source);
        byte[] sourceTriangle = source.AsSpan(
            layout.TriangleTableOffset + (ReusedCollisionTriangleIndex * 12),
            12).ToArray();
        if (!sourceTriangle.SequenceEqual(SourceCollisionTriangle))
            throw new InvalidDataException("The exact T13995 zero-area collision row changed.");
        int sourceAssignment = source[layout.AssignmentsOffset + ReusedCollisionTriangleIndex];
        if (sourceAssignment != 0xFF)
            throw new InvalidDataException("The exact T13995 sentinel assignment changed.");

        byte[] sourceTree = source.AsSpan(layout.TreeOffset, layout.TreeCapacityBytes).ToArray();
        byte[] sourceBlocks = source.AsSpan(layout.BlocksOffset, layout.BlocksCapacityBytes).ToArray();
        NativeCollisionIndex native = DecodeCollisionIndex(
            sourceTree,
            sourceBlocks,
            layout.BlocksCapacityBytes,
            CollisionTriangleCount);
        if (native.Cells.Count != 4_252 || native.GroupStartCount != 4_253 ||
            !native.Cells.TryGetValue(SourceCollisionCell, out CollisionCellBinding? sourceCell) ||
            !sourceCell.OrderedTriangleIndexes.SequenceEqual(ExpectedSourceCollisionSequence))
        {
            throw new InvalidDataException("The exact native collision-cell substrate changed.");
        }
        int targetPointer = FindTreeLeafPointer(sourceTree, TargetCollisionCell);
        if (targetPointer != TargetTreePointerRelativeOffset || ReadUInt16(sourceTree, targetPointer) != 0xFFFF ||
            native.Cells.ContainsKey(TargetCollisionCell))
        {
            throw new InvalidDataException("Remote target cell (1,1,2) is no longer the exact vacant 0xFFFF leaf.");
        }
        UnusedLevel65RemoteBlankCollisionCell[] sourceTriangleCells = FindTriangleCells(native, ReusedCollisionTriangleIndex);
        if (!sourceTriangleCells.SequenceEqual([SourceCollisionCell]))
            throw new InvalidDataException("T13995 no longer has unique source-cell ownership.");

        Dictionary<UnusedLevel65RemoteBlankCollisionCell, int[]> desired = native.Cells
            .ToDictionary(pair => pair.Key, pair => pair.Value.OrderedTriangleIndexes.ToArray());
        desired[SourceCollisionCell] = desired[SourceCollisionCell]
            .Where(index => index != ReusedCollisionTriangleIndex)
            .ToArray();
        desired.Add(TargetCollisionCell, [ReusedCollisionTriangleIndex]);

        Dictionary<string, int> emitted = new(StringComparer.Ordinal);
        Dictionary<UnusedLevel65RemoteBlankCollisionCell, int> offsets = [];
        List<ushort> words = [];
        foreach (CollisionCellBinding binding in native.Cells.Values
                     .OrderBy(binding => binding.SourceBlockWordOffset)
                     .ThenBy(binding => binding.Cell.Z)
                     .ThenBy(binding => binding.Cell.Y)
                     .ThenBy(binding => binding.Cell.X))
        {
            EmitCollisionSequence(binding.Cell, desired[binding.Cell], emitted, offsets, words);
        }
        EmitCollisionSequence(TargetCollisionCell, desired[TargetCollisionCell], emitted, offsets, words);
        words.Add(0x8000);
        int usedBytes = checked(words.Count * 2);
        if (usedBytes > layout.BlocksCapacityBytes)
            throw new InvalidDataException("The remote collision index exceeds fixed block capacity.");

        byte[] outputTree = sourceTree.ToArray();
        foreach (CollisionCellBinding binding in native.Cells.Values)
        {
            int pointer = offsets[binding.Cell];
            if (pointer > ushort.MaxValue)
                throw new InvalidDataException("A collision block word pointer exceeds 16-bit storage.");
            WriteUInt16(outputTree, binding.TreePointerByteOffset, checked((ushort)pointer));
        }
        WriteUInt16(outputTree, targetPointer, checked((ushort)offsets[TargetCollisionCell]));
        byte[] outputBlocks = new byte[layout.BlocksCapacityBytes];
        for (int index = 0; index < words.Count; index++)
            WriteUInt16(outputBlocks, index * 2, words[index]);

        byte[] output = source.ToArray();
        outputTree.CopyTo(output, layout.TreeOffset);
        outputBlocks.CopyTo(output, layout.BlocksOffset);
        OutputCollisionTriangle.CopyTo(
            output,
            layout.TriangleTableOffset + (ReusedCollisionTriangleIndex * 12));
        output[layout.AssignmentsOffset + ReusedCollisionTriangleIndex] = 0;

        NativeCollisionIndex authored = DecodeCollisionIndex(
            outputTree,
            outputBlocks,
            usedBytes,
            CollisionTriangleCount);
        if (authored.Cells.Count != native.Cells.Count + 1 ||
            !authored.Cells[SourceCollisionCell].OrderedTriangleIndexes.SequenceEqual(
                ExpectedSourceCollisionSequence.Where(index => index != ReusedCollisionTriangleIndex)) ||
            !authored.Cells[TargetCollisionCell].OrderedTriangleIndexes.SequenceEqual([ReusedCollisionTriangleIndex]) ||
            authored.Cells.Where(pair => pair.Key != SourceCollisionCell && pair.Key != TargetCollisionCell)
                .Any(pair => !native.Cells[pair.Key].OrderedTriangleIndexes.SequenceEqual(pair.Value.OrderedTriangleIndexes)) ||
            !FindTriangleCells(authored, ReusedCollisionTriangleIndex).SequenceEqual([TargetCollisionCell]))
        {
            throw new InvalidDataException("The remote collision repack changed a protected cell or missed its sole target cell.");
        }
        if (outputBlocks.AsSpan(usedBytes).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The compacted collision-block suffix is not zero tail.");
        if (!IsStrictlyDescending(authored.Cells[SourceCollisionCell].OrderedTriangleIndexes) ||
            !IsStrictlyDescending(authored.Cells[TargetCollisionCell].OrderedTriangleIndexes))
        {
            throw new InvalidDataException("The changed collision-cell sequences lost native descending order.");
        }
        if (CollisionTouchedCells(AuthoredPoints).Single() != TargetCollisionCell)
            throw new InvalidDataException("The authored triangle escaped collision cell (1,1,2).");
        (long _, long _, long normalZ) = CollisionNormal(AuthoredPoints[0], AuthoredPoints[1], AuthoredPoints[2]);
        if (normalZ != 50_176)
            throw new InvalidDataException("The authored collision triangle lost exact upward winding.");

        build = new(
            layout,
            native,
            authored,
            sourceTriangle,
            output.AsSpan(layout.TriangleTableOffset + (ReusedCollisionTriangleIndex * 12), 12).ToArray(),
            sourceAssignment,
            output[layout.AssignmentsOffset + ReusedCollisionTriangleIndex],
            sourceTree,
            outputTree,
            sourceBlocks,
            outputBlocks,
            usedBytes,
            targetPointer);
        return output;
    }

    private static void EmitCollisionSequence(
        UnusedLevel65RemoteBlankCollisionCell cell,
        int[] sequence,
        Dictionary<string, int> emitted,
        Dictionary<UnusedLevel65RemoteBlankCollisionCell, int> offsets,
        List<ushort> words)
    {
        if (sequence.Length == 0 || sequence.Any(index => index < 0 || index >= CollisionTriangleCount))
            throw new InvalidDataException($"Collision cell {cell} has an invalid empty/out-of-range sequence.");
        string key = string.Join(',', sequence);
        if (!emitted.TryGetValue(key, out int wordOffset))
        {
            wordOffset = words.Count;
            for (int index = 0; index < sequence.Length; index++)
            {
                ushort word = checked((ushort)sequence[index]);
                if (index == 0)
                    word |= 0x8000;
                words.Add(word);
            }
            emitted.Add(key, wordOffset);
        }
        offsets.Add(cell, wordOffset);
    }

    private static byte[] BuildOutputModel(
        byte[] sourceModel,
        ModelLayout sourceLayout,
        byte[] outputEnvironment,
        byte[] outputOcclusion,
        byte[] outputCollision)
    {
        byte[] output = new byte[ModelByteLength];
        int cursor = 0;
        void Append(byte[] bytes)
        {
            bytes.CopyTo(output, cursor);
            cursor += bytes.Length;
        }
        Append(ComponentBytes(sourceModel, sourceLayout, "texture"));
        Append(outputEnvironment);
        Append(outputOcclusion);
        Append(ComponentBytes(sourceModel, sourceLayout, "special surface"));
        Append(outputCollision);
        Append(ComponentBytes(sourceModel, sourceLayout, "cyclorama"));
        Append(ComponentBytes(sourceModel, sourceLayout, "portal table"));
        Append(ComponentBytes(sourceModel, sourceLayout, "particles"));
        Append(ComponentBytes(sourceModel, sourceLayout, "sound"));
        if (cursor != OutputUsedModelByteLength || output.AsSpan(cursor).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The output model did not end at exact 0x9457C plus zero tail.");
        return output;
    }

    private static void RequireSourceLayout(byte[] model, ModelLayout layout)
    {
        RequireHash(Hash(model), ExpectedSourceModelSha256, "locked source model");
        RequireComponent(layout, "texture", 0x00000, TextureComponentByteLength);
        RequireComponent(layout, "environment", 0x02F78, SourceEnvironmentByteLength);
        RequireComponent(layout, "occlusion", 0x2B41C, OcclusionComponentByteLength);
        RequireComponent(layout, "special surface", 0x2BD90, SpecialComponentByteLength);
        RequireComponent(layout, "collision", 0x2BDC0, CollisionComponentByteLength);
        RequireComponent(layout, "cyclorama", 0x8B8A8, CycloramaComponentByteLength);
        RequireComponent(layout, "portal table", 0x93D8C, PortalComponentByteLength);
        RequireComponent(layout, "particles", 0x93D90, ParticleComponentByteLength);
        RequireComponent(layout, "sound", 0x93E10, SoundComponentByteLength);
        if (layout.Sectors.Count != SourceSectorCount || layout.UsedByteLength != SourceUsedModelByteLength ||
            layout.ZeroTailBytes != SourceZeroTailByteCount || layout.PortalCount != 0 ||
            layout.Sectors.Any(sector => sector.GapBytesAfter != 0) ||
            layout.Sectors.Sum(sector => sector.LowDetailVertexCount) != 2_853 ||
            layout.Sectors.Sum(sector => sector.LowDetailFaceCount) != 1_437 ||
            layout.Sectors.Sum(sector => sector.HighDetailVertexCount) != 5_863 ||
            layout.Sectors.Sum(sector => sector.HighDetailFaceCount) != 3_887)
        {
            throw new InvalidDataException("The exact locked 216-sector source model contract changed.");
        }
    }

    private static void RequireOutputLayout(
        byte[] sourceModel,
        ModelLayout sourceLayout,
        byte[] outputModel,
        ModelLayout outputLayout,
        byte[] newSector,
        byte[] outputOcclusion,
        byte[] outputCollision,
        OcclusionBuild occlusionBuild,
        CollisionBuild collisionBuild)
    {
        RequireComponent(outputLayout, "texture", 0x00000, TextureComponentByteLength);
        RequireComponent(outputLayout, "environment", 0x02F78, OutputEnvironmentByteLength);
        RequireComponent(outputLayout, "occlusion", 0x2B490, OcclusionComponentByteLength);
        RequireComponent(outputLayout, "special surface", 0x2BE04, SpecialComponentByteLength);
        RequireComponent(outputLayout, "collision", 0x2BE34, CollisionComponentByteLength);
        RequireComponent(outputLayout, "cyclorama", 0x8B91C, CycloramaComponentByteLength);
        RequireComponent(outputLayout, "portal table", 0x93E00, PortalComponentByteLength);
        RequireComponent(outputLayout, "particles", 0x93E04, ParticleComponentByteLength);
        RequireComponent(outputLayout, "sound", 0x93E84, SoundComponentByteLength);
        if (outputLayout.Sectors.Count != OutputSectorCount ||
            outputLayout.UsedByteLength != OutputUsedModelByteLength ||
            outputLayout.ZeroTailBytes != OutputZeroTailByteCount || outputLayout.PortalCount != 0 ||
            outputLayout.Sectors.Sum(sector => sector.LowDetailVertexCount) != 2_856 ||
            outputLayout.Sectors.Sum(sector => sector.LowDetailFaceCount) != 1_438 ||
            outputLayout.Sectors.Sum(sector => sector.HighDetailVertexCount) != 5_866 ||
            outputLayout.Sectors.Sum(sector => sector.HighDetailFaceCount) != 3_888)
        {
            throw new InvalidDataException("The exact 217-sector output model contract changed.");
        }
        for (int index = 0; index < SourceSectorCount; index++)
        {
            if (!SectorBytes(sourceModel, sourceLayout.Sectors[index])
                .SequenceEqual(SectorBytes(outputModel, outputLayout.Sectors[index])))
            {
                throw new InvalidDataException($"Inherited scene sector {index} changed during remote append.");
            }
        }
        if (!SectorBytes(outputModel, outputLayout.Sectors[NewSectorIndex]).SequenceEqual(newSector) ||
            !ComponentBytes(outputModel, outputLayout, "occlusion").SequenceEqual(outputOcclusion) ||
            !ComponentBytes(outputModel, outputLayout, "collision").SequenceEqual(outputCollision) ||
            !occlusionBuild.OutputGroups[0].Contains(NewSectorIndex) ||
            !collisionBuild.AuthoredIndex.Cells.ContainsKey(TargetCollisionCell))
        {
            throw new InvalidDataException("The new sector, occlusion owner, or collision owner failed output readback.");
        }
        foreach (string name in new[] { "texture", "special surface", "cyclorama", "portal table", "particles", "sound" })
        {
            if (!ComponentBytes(sourceModel, sourceLayout, name)
                .SequenceEqual(ComponentBytes(outputModel, outputLayout, name)))
            {
                throw new InvalidDataException($"Protected component {name} changed instead of relocating intact.");
            }
        }
    }

    private static void RequireSpawnPreimages(byte[] data)
    {
        byte[] landing = data.AsSpan(LandingDataRelativeOffset, SourceLanding.Length).ToArray();
        if (!landing.SequenceEqual(SourceLanding) || Hash(landing) != UnusedLevel65FullAuthoringConstructionTemplate.LandingSha256)
            throw new InvalidDataException("The exact locked fly-in landing preimage changed.");
        byte[] player = data.AsSpan(PlayerAnchorDataRelativeOffset, ObjectRecordByteLength).ToArray();
        if (Hash(player) != UnusedLevel65FullAuthoringConstructionTemplate.PlayerAnchorSha256)
            throw new InvalidDataException("The exact locked T92 player-anchor preimage changed.");
        RequireInt32(player, 0x0C, 125_225, "source T92 X");
        RequireInt32(player, 0x10, 100_506, "source T92 Y");
        RequireInt32(player, 0x14, PlayerRawZ, "source T92 Z");
        byte[] objectTable = data.AsSpan(ObjectTableDataRelativeOffset, ObjectRecordCount * ObjectRecordByteLength).ToArray();
        RequireHash(Hash(objectTable), ExpectedSourceObjectTableSha256, "locked 107-row object table");
    }

    private static void RequireObjectTableIsolation(byte[] sourceData, byte[] outputData, byte[] outputPlayer)
    {
        byte[] sourceTable = sourceData.AsSpan(
            ObjectTableDataRelativeOffset,
            ObjectRecordCount * ObjectRecordByteLength).ToArray();
        byte[] outputTable = outputData.AsSpan(
            ObjectTableDataRelativeOffset,
            ObjectRecordCount * ObjectRecordByteLength).ToArray();
        for (int index = 0; index < ObjectRecordCount; index++)
        {
            ReadOnlySpan<byte> before = sourceTable.AsSpan(index * ObjectRecordByteLength, ObjectRecordByteLength);
            ReadOnlySpan<byte> after = outputTable.AsSpan(index * ObjectRecordByteLength, ObjectRecordByteLength);
            if (index == UnusedLevel65FullAuthoringConstructionTemplate.PlayerAnchorTrueIndex)
            {
                if (!before[..0x0C].SequenceEqual(after[..0x0C]) ||
                    !before[0x14..].SequenceEqual(after[0x14..]) ||
                    !after.SequenceEqual(outputPlayer))
                {
                    throw new InvalidDataException("T92 changed outside its exact X/Y fields.");
                }
            }
            else if (!before.SequenceEqual(after))
            {
                throw new InvalidDataException($"Inherited Moby row T{index} changed.");
            }
        }
        if (!string.IsNullOrEmpty(ExpectedOutputObjectTableSha256))
            RequireHash(Hash(outputTable), ExpectedOutputObjectTableSha256, "output object table");
    }

    private static void RequireEntryHeader(byte[] data)
    {
        (int Offset, int Length)[] expected =
        [
            (0x000800, 0x0DE000),
            (0x0DE800, 0x094800),
            (0x173000, 0x05D000),
            (0x1D0000, 0x008800),
            (0x1D8800, 0x049800),
            (0x222000, 0x042800),
            (0x264800, 0x05D000),
            (0x2C1800, 0x020800)
        ];
        for (int index = 0; index < expected.Length; index++)
        {
            if (ReadInt32(data, index * 8) != expected[index].Offset ||
                ReadInt32(data, (index * 8) + 4) != expected[index].Length)
            {
                throw new InvalidDataException($"The exact row-80 subfile header {index} changed.");
            }
        }
    }

    private static void RequireProtectedSubfiles(byte[] source, byte[] output)
    {
        (int Offset, int Length)[] protectedSubfiles =
        [
            (0x000000, 0x000800),
            (0x000800, 0x0DE000),
            (0x173000, 0x05D000),
            (0x1D8800, 0x049800),
            (0x222000, 0x042800),
            (0x264800, 0x05D000),
            (0x2C1800, 0x020800)
        ];
        foreach ((int offset, int length) in protectedSubfiles)
        {
            if (!source.AsSpan(offset, length).SequenceEqual(output.AsSpan(offset, length)))
                throw new InvalidDataException($"Protected row-80 range 0x{offset:X}/0x{length:X} changed.");
        }
        int sceneOffset = 0x1D0000;
        int sceneLength = 0x8800;
        for (int index = 0; index < sceneLength; index++)
        {
            int dataOffset = sceneOffset + index;
            bool landingXy = dataOffset >= LandingDataRelativeOffset && dataOffset < LandingDataRelativeOffset + 8;
            bool playerXy = dataOffset >= PlayerAnchorXyDataRelativeOffset && dataOffset < PlayerAnchorXyDataRelativeOffset + 8;
            if (!landingXy && !playerXy && source[dataOffset] != output[dataOffset])
                throw new InvalidDataException("The scene subfile changed outside landing/T92 X/Y.");
        }
    }

    private static UnusedLevel65RemoteBlankComponentProof[] BuildComponentProofs(
        byte[] sourceModel,
        ModelLayout source,
        byte[] outputModel,
        ModelLayout output)
    {
        string[] names =
        [
            "texture", "environment", "occlusion", "special surface", "collision",
            "cyclorama", "portal table", "particles", "sound"
        ];
        return names.Select(name =>
        {
            ComponentLayout before = source.Component(name);
            ComponentLayout after = output.Component(name);
            string beforeHash = Hash(ComponentBytes(sourceModel, source, name));
            string afterHash = Hash(ComponentBytes(outputModel, output, name));
            bool preserved = name is "texture" or "special surface" or "cyclorama" or "portal table" or "particles" or "sound"
                ? beforeHash == afterHash
                : false;
            return new UnusedLevel65RemoteBlankComponentProof(
                name,
                before.Offset,
                after.Offset,
                before.ByteLength,
                beforeHash,
                afterHash,
                preserved);
        }).ToArray();
    }

    private static UnusedLevel65RemoteBlankSceneProof BuildSceneProof(
        byte[] sourceModel,
        ModelLayout source,
        byte[] outputModel,
        ModelLayout output,
        byte[] newSector)
    {
        ParsedSector sector = output.Sectors[NewSectorIndex];
        byte[] sectorBytes = SectorBytes(outputModel, sector);
        if (!sectorBytes.SequenceEqual(newSector) || sector.LowDetailVertexCount != 3 ||
            sector.LowDetailColorCount != 3 || sector.LowDetailFaceCount != 1 ||
            sector.HighDetailVertexCount != 3 || sector.HighDetailColorCount != 3 ||
            sector.HighDetailFaceCount != 1)
        {
            throw new InvalidDataException("The exact new HP+LP sector counts changed.");
        }
        uint[] lowWords = Enumerable.Range(0, 3)
            .Select(index => ReadUInt32(sectorBytes, 28 + (index * 4)))
            .ToArray();
        int highVertexStart = 28 + 12 + 12 + 8;
        uint[] highWords = Enumerable.Range(0, 3)
            .Select(index => ReadUInt32(sectorBytes, highVertexStart + (index * 4)))
            .ToArray();
        if (!lowWords.SequenceEqual(AuthoredVertexWords) || !highWords.SequenceEqual(AuthoredVertexWords))
            throw new InvalidDataException("The exact HP/LP vertex words changed.");
        UnusedLevel65RemoteBlankPoint[] decoded = lowWords.Select(word => DecodeSceneVertex(sectorBytes, word)).ToArray();
        if (!decoded.SequenceEqual(AuthoredPoints))
            throw new InvalidDataException("The new sector vertex words no longer decode to the remote pad.");
        return new(
            source.Sectors.Count,
            output.Sectors.Count,
            NewSectorIndex,
            source.Component("environment").ByteLength,
            output.Component("environment").ByteLength,
            EnvironmentGrowthBytes,
            newSector.Length,
            Convert.ToHexString(newSector),
            Hash(newSector),
            CullCenter,
            NewCullRadius,
            0,
            EncodingOrigin,
            AuthoredPoints,
            AuthoredVertexWords,
            "0082100000821000",
            "000001020101000219024001080A1000",
            MaterialTextureId,
            AllInheritedSectorPayloadsPreserved: Enumerable.Range(0, SourceSectorCount).All(index =>
                SectorBytes(sourceModel, source.Sectors[index])
                    .SequenceEqual(SectorBytes(outputModel, output.Sectors[index]))),
            ExactHpLpPairing: true,
            OrdinaryCullRoute: (ReadUInt16(sectorBytes, 4) & 0xE000) == 0);
    }

    private static UnusedLevel65RemoteBlankOcclusionProof BuildOcclusionProof(OcclusionBuild build) =>
        new(
            build.SourceGroups.Length,
            0,
            build.SourceGroups[0].Count,
            build.OutputGroups[0].Count,
            OcclusionInsertionRelativeOffset,
            OcclusionEnvironmentPortionByteLength,
            build.SourcePointers,
            build.OutputPointers,
            build.OutputGroups[0].Take(build.SourceGroups[0].Count).SequenceEqual(build.SourceGroups[0]),
            build.SourceGroups.Skip(1).Zip(build.OutputGroups.Skip(1))
                .All(pair => pair.First.SequenceEqual(pair.Second)),
            FixedComponentLength: true,
            TargetSectorOwned: build.OutputGroups[0].Last() == NewSectorIndex);

    private static UnusedLevel65RemoteBlankCollisionProof BuildCollisionProof(CollisionBuild build) =>
        new(
            CollisionTriangleCount,
            ReusedCollisionTriangleIndex,
            Convert.ToHexString(build.SourceTriangle),
            Convert.ToHexString(build.OutputTriangle),
            build.SourceAssignment,
            build.OutputAssignment,
            SourceCollisionCell,
            TargetCollisionCell,
            build.TargetTreePointer,
            build.NativeIndex.Cells.Count,
            build.AuthoredIndex.Cells.Count,
            build.NativeIndex.GroupStartCount,
            build.AuthoredIndex.GroupStartCount,
            build.Layout.BlocksCapacityBytes,
            build.OutputBlocksUsedBytes,
            Hash(build.SourceTree),
            Hash(build.OutputTree),
            Hash(build.SourceBlocks),
            Hash(build.OutputBlocks),
            build.NativeIndex.Cells[SourceCollisionCell].OrderedTriangleIndexes,
            build.AuthoredIndex.Cells[SourceCollisionCell].OrderedTriangleIndexes,
            build.AuthoredIndex.Cells[TargetCollisionCell].OrderedTriangleIndexes,
            CollisionTouchedCells(AuthoredPoints),
            TargetLeafWasVacant: true,
            NativeCellSequencesPreserved: build.AuthoredIndex.Cells
                .Where(pair => pair.Key != SourceCollisionCell && pair.Key != TargetCollisionCell)
                .All(pair => build.NativeIndex.Cells[pair.Key].OrderedTriangleIndexes
                    .SequenceEqual(pair.Value.OrderedTriangleIndexes)),
            NativeOrderingPreserved: true,
            UpwardWinding: true,
            FixedComponentLength: true);

    private static UnusedLevel65RemoteBlankSpawnProof BuildSpawnProof(
        byte[] sourceData,
        byte[] outputData,
        byte[] outputPlayer)
    {
        byte[] beforeLanding = sourceData.AsSpan(LandingDataRelativeOffset, 0x10).ToArray();
        byte[] afterLanding = outputData.AsSpan(LandingDataRelativeOffset, 0x10).ToArray();
        byte[] beforePlayer = sourceData.AsSpan(PlayerAnchorDataRelativeOffset, ObjectRecordByteLength).ToArray();
        return new(
            DataWadOffset + LandingDataRelativeOffset,
            Convert.ToHexString(beforeLanding),
            Convert.ToHexString(afterLanding),
            Hash(beforeLanding),
            Hash(afterLanding),
            AuthoredRawX,
            AuthoredRawY,
            LandingRawZ,
            YawByte,
            UnusedLevel65FullAuthoringConstructionTemplate.PlayerAnchorTrueIndex,
            DataWadOffset + PlayerAnchorDataRelativeOffset,
            Hash(beforePlayer),
            Hash(outputPlayer),
            AuthoredRawX,
            AuthoredRawY,
            PlayerRawZ,
            GroundRawZ,
            LandingRawZ - GroundRawZ,
            PlayerRawZ - GroundRawZ,
            PlayerRawZ - LandingRawZ,
            AtomicXyOnly: DifferentByteIndexes(beforeLanding, afterLanding)
                .Concat(DifferentByteIndexes(beforePlayer, outputPlayer).Select(index => index + 0x10))
                .All(index => index is >= 0 and < 8 or >= 0x1C and < 0x24),
            DeathPlaneRuntimeVerified: false,
            DeathRespawnRuntimeVerified: false);
    }

    private static UnusedLevel65RemoteBlankIsolationProof BuildIsolationProof(
        IsolationScan scan,
        CollisionBuild collision) =>
        new(
            SourceSectorCount,
            scan.ScannedLowDetailFaces,
            scan.ScannedHighDetailFaces,
            CollisionTriangleCount,
            scan.NativeLowDetailOverlaps,
            scan.NativeHighDetailOverlaps,
            scan.NativeCollisionOverlaps,
            scan.NearestCullSectorIndex,
            scan.MinimumCullSurfaceDistance,
            scan.MinimumCullSurfaceDistance - NewCullRadius,
            scan.NearestVertexSectorIndex,
            scan.NearestVertexLod,
            scan.MinimumVertexXyDistance,
            scan.NearestCollisionTriangleIndex,
            scan.MinimumCollisionVertexXyDistance,
            ObjectRecordCount - 1,
            scan.NearestMobyTrueIndex,
            scan.MinimumMobyXyDistance,
            scan.MinimumMobyRawX,
            scan.MaximumMobyRawX,
            scan.MinimumMobyRawY,
            scan.MaximumMobyRawY,
            scan.MinimumMobyRawZ,
            scan.MaximumMobyRawZ,
            AuthoredCollisionIsOnlyTargetCellSurface:
                scan.NativeCollisionOverlaps == 0 &&
                collision.AuthoredIndex.Cells[TargetCollisionCell].OrderedTriangleIndexes
                    .SequenceEqual([ReusedCollisionTriangleIndex]),
            NativeMobysPhysicallyDisconnected:
                scan.MinimumMobyXyDistance > 8_000 && scan.NativeCollisionOverlaps == 0,
            NativeMobyRuntimeInactivityVerified: false,
            FarLodRuntimeRouteVerified: false);

    private static IsolationScan ScanIsolation(byte[] data, byte[] model, ModelLayout layout)
    {
        int lowFaces = 0;
        int highFaces = 0;
        int lowOverlaps = 0;
        int highOverlaps = 0;
        double minCull = double.PositiveInfinity;
        int nearestCull = -1;
        double minVertex = double.PositiveInfinity;
        int nearestVertexSector = -1;
        string nearestVertexLod = "";
        foreach (ParsedSector sector in layout.Sectors)
        {
            byte[] bytes = SectorBytes(model, sector);
            int centerX = ReadUInt32(bytes, 0) is uint xy ? (int)(xy >> 16) : 0;
            int centerY = (int)(ReadUInt32(bytes, 0) & 0xFFFF);
            int radius = ReadUInt16(bytes, 4) & 0x0FFF;
            double cullDistance = Math.Sqrt(
                Square(CullCenter.X - centerX) + Square(CullCenter.Y - centerY)) - radius;
            if (cullDistance < minCull)
            {
                minCull = cullDistance;
                nearestCull = sector.Index;
            }

            (UnusedLevel65RemoteBlankPoint[] lowVertices,
             UnusedLevel65RemoteBlankPoint[] highVertices,
             int lowFaceStart,
             int highFaceStart) = DecodeSectorTables(bytes, sector);
            foreach ((UnusedLevel65RemoteBlankPoint[] vertices, string lod) in
                     new[] { (lowVertices, "LP"), (highVertices, "HP") })
            {
                foreach (UnusedLevel65RemoteBlankPoint point in vertices)
                {
                    double distance = Math.Sqrt(Square(CullCenter.X - point.X) + Square(CullCenter.Y - point.Y));
                    if (distance < minVertex)
                    {
                        minVertex = distance;
                        nearestVertexSector = sector.Index;
                        nearestVertexLod = lod;
                    }
                }
            }
            for (int face = 0; face < sector.LowDetailFaceCount; face++)
            {
                int[] slots = ReadPackedSixBitSlots(ReadUInt32(bytes, lowFaceStart + (face * 8)));
                UnusedLevel65RemoteBlankPoint[] points = ResolveFacePoints(lowVertices, slots);
                if (points.Length >= 3 && HasPositiveAreaXyInteriorOverlap(AuthoredPoints, points))
                    lowOverlaps++;
                lowFaces++;
            }
            for (int face = 0; face < sector.HighDetailFaceCount; face++)
            {
                int offset = highFaceStart + (face * 16);
                int[] slots = [bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3]];
                UnusedLevel65RemoteBlankPoint[] points = ResolveFacePoints(highVertices, slots);
                if (points.Length >= 3 && HasPositiveAreaXyInteriorOverlap(AuthoredPoints, points))
                    highOverlaps++;
                highFaces++;
            }
        }

        byte[] collision = ComponentBytes(model, layout, "collision");
        CollisionLayout collisionLayout = ParseCollisionLayout(collision);
        int collisionOverlaps = 0;
        double minCollisionVertex = double.PositiveInfinity;
        int nearestCollisionTriangle = -1;
        for (int index = 0; index < CollisionTriangleCount; index++)
        {
            UnusedLevel65RemoteBlankPoint[] points = DecodeCollisionTriangle(
                collision.AsSpan(collisionLayout.TriangleTableOffset + (index * 12), 12));
            foreach (UnusedLevel65RemoteBlankPoint point in points)
            {
                double distance = Math.Sqrt(Square(CullCenter.X - point.X) + Square(CullCenter.Y - point.Y));
                if (distance < minCollisionVertex)
                {
                    minCollisionVertex = distance;
                    nearestCollisionTriangle = index;
                }
            }
            if (!IsZeroArea(points) && HasPositiveAreaXyInteriorOverlap(AuthoredPoints, points))
                collisionOverlaps++;
        }

        int nearestMoby = -1;
        double minMoby = double.PositiveInfinity;
        int minRawX = int.MaxValue;
        int maxRawX = int.MinValue;
        int minRawY = int.MaxValue;
        int maxRawY = int.MinValue;
        int minRawZ = int.MaxValue;
        int maxRawZ = int.MinValue;
        for (int index = 0; index < ObjectRecordCount; index++)
        {
            if (index == UnusedLevel65FullAuthoringConstructionTemplate.PlayerAnchorTrueIndex)
                continue;
            int offset = ObjectTableDataRelativeOffset + (index * ObjectRecordByteLength);
            int rawX = ReadInt32(data, offset + 0x0C);
            int rawY = ReadInt32(data, offset + 0x10);
            int rawZ = ReadInt32(data, offset + 0x14);
            minRawX = Math.Min(minRawX, rawX);
            maxRawX = Math.Max(maxRawX, rawX);
            minRawY = Math.Min(minRawY, rawY);
            maxRawY = Math.Max(maxRawY, rawY);
            minRawZ = Math.Min(minRawZ, rawZ);
            maxRawZ = Math.Max(maxRawZ, rawZ);
            double sceneX = rawX / 16.0;
            double sceneY = rawY / 16.0;
            double distance = Math.Sqrt(Square(CullCenter.X - sceneX) + Square(CullCenter.Y - sceneY));
            if (distance < minMoby)
            {
                minMoby = distance;
                nearestMoby = index;
            }
        }
        if (lowFaces != 1_437 || highFaces != 3_887 || lowOverlaps != 0 || highOverlaps != 0 ||
            collisionOverlaps != 0 || nearestCull < 0 || nearestVertexSector < 0 ||
            nearestCollisionTriangle < 0 || nearestMoby < 0)
        {
            throw new InvalidDataException("The full native remote-isolation exposure scan failed.");
        }
        return new(
            lowFaces,
            highFaces,
            lowOverlaps,
            highOverlaps,
            collisionOverlaps,
            nearestCull,
            minCull,
            nearestVertexSector,
            nearestVertexLod,
            minVertex,
            nearestCollisionTriangle,
            minCollisionVertex,
            nearestMoby,
            minMoby,
            minRawX,
            maxRawX,
            minRawY,
            maxRawY,
            minRawZ,
            maxRawZ);
    }

    private static ModelLayout ParseModel(byte[] model)
    {
        if (model.Length != ModelByteLength)
            throw new InvalidDataException("The ID65 model subfile length changed.");
        int cursor = 0;
        List<ComponentLayout> components = [];
        foreach (string name in new[] { "texture", "environment", "occlusion", "special surface", "collision", "cyclorama" })
            components.Add(ReadComponent(model, ref cursor, name));
        int portalOffset = cursor;
        int portalCount = ReadInt32(model, cursor);
        if (portalCount != 0)
            throw new InvalidDataException("The remote construction substrate unexpectedly has portals.");
        cursor += 4;
        components.Add(new("portal table", portalOffset, 4, Hash(model.AsSpan(portalOffset, 4))));
        components.Add(ReadComponent(model, ref cursor, "particles"));
        components.Add(ReadComponent(model, ref cursor, "sound"));
        if (model.AsSpan(cursor).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The model suffix is not exact zero tail.");

        ComponentLayout environment = components.Single(component => component.Name == "environment");
        int sectorCount = ReadInt32(model, environment.Offset + 4);
        if (sectorCount <= 0 || sectorCount > 254 || 8L + (sectorCount * 4L) > environment.ByteLength)
            throw new InvalidDataException("The scene-sector pointer table is invalid.");
        List<ParsedSector> sectors = new(sectorCount);
        for (int index = 0; index < sectorCount; index++)
        {
            int pointer = ReadInt32(model, environment.Offset + 8 + (index * 4));
            int sectorOffset = checked(environment.Offset + 4 + pointer);
            int nextOffset = index + 1 < sectorCount
                ? checked(environment.Offset + 4 + ReadInt32(model, environment.Offset + 12 + (index * 4)))
                : environment.Offset + environment.ByteLength;
            if (sectorOffset < environment.Offset + 8 + (sectorCount * 4) || sectorOffset + 28 > nextOffset)
                throw new InvalidDataException($"Scene-sector pointer {index} is invalid.");
            int lpVertices = model[sectorOffset + 16];
            int lpColors = model[sectorOffset + 17];
            int lpFaces = model[sectorOffset + 18];
            int hpVertices = model[sectorOffset + 20];
            int hpColors = model[sectorOffset + 21];
            int hpFaces = model[sectorOffset + 22];
            int length = checked((7 + lpVertices + lpColors + (lpFaces * 2) +
                                  hpVertices + (hpColors * 2) + (hpFaces * 4)) * 4);
            if (sectorOffset + length > nextOffset ||
                model.AsSpan(sectorOffset + length, nextOffset - sectorOffset - length).IndexOfAnyExcept((byte)0) >= 0)
            {
                throw new InvalidDataException($"Scene sector {index} overruns or has nonzero pointer gap.");
            }
            sectors.Add(new(
                index,
                sectorOffset,
                length,
                nextOffset - sectorOffset - length,
                lpVertices,
                lpColors,
                lpFaces,
                hpVertices,
                hpColors,
                hpFaces));
        }
        return new(components, sectors, portalCount, cursor, model.Length - cursor);
    }

    private static ComponentLayout ReadComponent(byte[] model, ref int cursor, string name)
    {
        int length = ReadInt32(model, cursor);
        if (length < 4 || (length & 3) != 0 || cursor + (long)length > model.Length)
            throw new InvalidDataException($"The {name} component length is invalid.");
        ComponentLayout result = new(name, cursor, length, Hash(model.AsSpan(cursor, length)));
        cursor += length;
        return result;
    }

    private static CollisionLayout ParseCollisionLayout(byte[] collision)
    {
        if (collision.Length != CollisionComponentByteLength || ReadInt32(collision, 0) != collision.Length)
            throw new InvalidDataException("The fixed collision component length changed.");
        int header = 4;
        int triangleCount = ReadInt32(collision, header);
        int flagsLength = ReadInt32(collision, header + 4);
        int treeRelative = ReadInt32(collision, header + 8);
        int blocksRelative = ReadInt32(collision, header + 12);
        int trianglesRelative = ReadInt32(collision, header + 16);
        int assignmentsRelative = ReadInt32(collision, header + 20);
        int flagsRelative = ReadInt32(collision, header + 24);
        if (triangleCount != CollisionTriangleCount || flagsLength != CollisionFlagsByteLength ||
            treeRelative != CollisionTreeRelativeOffset || blocksRelative != CollisionBlocksRelativeOffset ||
            trianglesRelative != CollisionTrianglesRelativeOffset ||
            assignmentsRelative != CollisionAssignmentsRelativeOffset || flagsRelative != CollisionFlagsRelativeOffset)
        {
            throw new InvalidDataException("The exact collision header changed.");
        }
        int tree = header + treeRelative;
        int blocks = header + blocksRelative;
        int triangles = header + trianglesRelative;
        int assignments = header + assignmentsRelative;
        int flags = header + flagsRelative;
        if (tree != 0x20 || triangles + (triangleCount * 12) != assignments ||
            assignments + triangleCount > flags || flags + flagsLength != collision.Length ||
            triangles - blocks != CollisionBlocksCapacityBytes)
        {
            throw new InvalidDataException("The collision table partition changed.");
        }
        return new(
            triangleCount,
            tree,
            blocks,
            triangles,
            assignments,
            flags,
            blocks - tree,
            triangles - blocks);
    }

    private static NativeCollisionIndex DecodeCollisionIndex(
        byte[] tree,
        byte[] blocksCapacity,
        int usedBlockBytes,
        int triangleCount)
    {
        if (usedBlockBytes <= 0 || (usedBlockBytes & 1) != 0 || usedBlockBytes > blocksCapacity.Length)
            throw new InvalidDataException("The collision block used length is invalid.");
        byte[] blocks = blocksCapacity.ToArray();
        int usedWords = usedBlockBytes / 2;
        List<int> groupStarts = [];
        for (int wordOffset = 0; wordOffset < usedWords; wordOffset++)
        {
            if ((ReadUInt16(blocks, wordOffset * 2) & 0x8000) != 0)
                groupStarts.Add(wordOffset);
        }
        if (groupStarts.Count < 2 || groupStarts[^1] != usedWords - 1 ||
            ReadUInt16(blocks, groupStarts[^1] * 2) != 0x8000)
        {
            throw new InvalidDataException("The collision blocks lack their exact terminal sentinel.");
        }
        Dictionary<int, int[]> groups = [];
        for (int group = 0; group < groupStarts.Count - 1; group++)
        {
            int start = groupStarts[group];
            int end = groupStarts[group + 1];
            int[] sequence = new int[end - start];
            for (int index = 0; index < sequence.Length; index++)
            {
                ushort word = ReadUInt16(blocks, (start + index) * 2);
                if ((index == 0) != ((word & 0x8000) != 0))
                    throw new InvalidDataException("A collision group start marker is invalid.");
                int triangle = word & 0x7FFF;
                if (triangle >= triangleCount)
                    throw new InvalidDataException("A collision group references an out-of-range triangle.");
                sequence[index] = triangle;
            }
            groups.Add(start, sequence);
        }

        int ReadTreeWord(int offset)
        {
            if (offset < 0 || (offset & 1) != 0 || offset + 2 > tree.Length)
                throw new InvalidDataException("A collision tree word is out of range.");
            return ReadUInt16(tree, offset);
        }
        int ReadLength(int offset)
        {
            int length = ReadTreeWord(offset);
            if (length < 0 || length > 256 || offset + ((length + 1L) * 2L) > tree.Length)
                throw new InvalidDataException("A collision tree segment length is invalid.");
            return length;
        }

        Dictionary<UnusedLevel65RemoteBlankCollisionCell, CollisionCellBinding> cells = [];
        int zLength = ReadLength(0);
        for (int z = 0; z < zLength; z++)
        {
            int yOffset = ReadTreeWord((z + 1) * 2);
            if (yOffset == 0xFFFF)
                continue;
            int yLength = ReadLength(yOffset);
            for (int y = 0; y < yLength; y++)
            {
                int xOffset = ReadTreeWord(yOffset + ((y + 1) * 2));
                if (xOffset == 0xFFFF)
                    continue;
                int xLength = ReadLength(xOffset);
                for (int x = 0; x < xLength; x++)
                {
                    int pointerOffset = xOffset + ((x + 1) * 2);
                    int blockWord = ReadTreeWord(pointerOffset);
                    if (blockWord == 0xFFFF)
                        continue;
                    if (blockWord == groupStarts[^1] || !groups.TryGetValue(blockWord, out int[]? sequence))
                        throw new InvalidDataException("A collision tree leaf points outside an exact block group.");
                    UnusedLevel65RemoteBlankCollisionCell cell = new(x, y, z);
                    cells.Add(cell, new(cell, pointerOffset, blockWord, sequence.ToArray()));
                }
            }
        }
        if (!cells.Values.Select(cell => cell.SourceBlockWordOffset).Distinct().Order()
            .SequenceEqual(groups.Keys.Order()))
        {
            throw new InvalidDataException("The collision blocks contain unreferenced groups.");
        }
        return new(tree.ToArray(), blocks, cells, groupStarts.Count, groupStarts[^1], usedBlockBytes);
    }

    private static int FindTreeLeafPointer(byte[] tree, UnusedLevel65RemoteBlankCollisionCell cell)
    {
        int zLength = ReadUInt16(tree, 0);
        if (cell.Z < 0 || cell.Z >= zLength)
            throw new InvalidDataException("The target Z cell is outside the collision tree root.");
        int yOffset = ReadUInt16(tree, (cell.Z + 1) * 2);
        if (yOffset == 0xFFFF || cell.Y < 0 || cell.Y >= ReadUInt16(tree, yOffset))
            throw new InvalidDataException("The target Y cell is outside an active collision segment.");
        int xOffset = ReadUInt16(tree, yOffset + ((cell.Y + 1) * 2));
        if (xOffset == 0xFFFF || cell.X < 0 || cell.X >= ReadUInt16(tree, xOffset))
            throw new InvalidDataException("The target X cell is outside an active collision segment.");
        return xOffset + ((cell.X + 1) * 2);
    }

    private static UnusedLevel65RemoteBlankCollisionCell[] FindTriangleCells(
        NativeCollisionIndex index,
        int triangle) =>
        index.Cells.Values
            .Where(binding => binding.OrderedTriangleIndexes.Contains(triangle))
            .Select(binding => binding.Cell)
            .OrderBy(cell => cell.Z)
            .ThenBy(cell => cell.Y)
            .ThenBy(cell => cell.X)
            .ToArray();

    private static IReadOnlyList<UnusedLevel65RemoteBlankCollisionCell> CollisionTouchedCells(
        IReadOnlyList<UnusedLevel65RemoteBlankPoint> points)
    {
        int minX = points.Min(point => point.X) >> 8;
        int maxX = points.Max(point => point.X) >> 8;
        int minY = points.Min(point => point.Y) >> 8;
        int maxY = points.Max(point => point.Y) >> 8;
        int minZ = points.Min(point => point.Z) >> 8;
        int maxZ = points.Max(point => point.Z) >> 8;
        List<UnusedLevel65RemoteBlankCollisionCell> cells = [];
        for (int z = minZ; z <= maxZ; z++)
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            if (TriangleTouchesBlock(points, x, y, z))
                cells.Add(new(x, y, z));
        }
        return cells;
    }

    private static bool TriangleTouchesBlock(
        IReadOnlyList<UnusedLevel65RemoteBlankPoint> points,
        int x,
        int y,
        int z)
    {
        int x1 = x << 8;
        int x2 = (x + 1) << 8;
        int y1 = y << 8;
        int y2 = (y + 1) << 8;
        int z1 = z << 8;
        int z2 = (z + 1) << 8;
        if (points.Any(point => point.X >= x1 && point.X < x2 && point.Y >= y1 && point.Y < y2 &&
                                point.Z >= z1 && point.Z < z2))
            return true;
        for (int p = 0; p < 3; p++)
        {
            UnusedLevel65RemoteBlankPoint current = points[p];
            UnusedLevel65RemoteBlankPoint next = points[(p + 1) % 3];
            if (next.X != current.X)
            {
                foreach (int plane in new[] { x1, x2 })
                {
                    if ((current.X >= plane && next.X <= plane) || (current.X <= plane && next.X >= plane))
                    {
                        int testY = current.Y + (((next.Y - current.Y) * (plane - current.X)) / (next.X - current.X));
                        int testZ = current.Z + (((next.Z - current.Z) * (plane - current.X)) / (next.X - current.X));
                        if (testY >= y1 && testY < y2 && testZ >= z1 && testZ < z2)
                            return true;
                    }
                }
            }
            if (next.Y != current.Y)
            {
                foreach (int plane in new[] { y1, y2 })
                {
                    if ((current.Y >= plane && next.Y <= plane) || (current.Y <= plane && next.Y >= plane))
                    {
                        int testX = current.X + (((next.X - current.X) * (plane - current.Y)) / (next.Y - current.Y));
                        int testZ = current.Z + (((next.Z - current.Z) * (plane - current.Y)) / (next.Y - current.Y));
                        if (testX >= x1 && testX < x2 && testZ >= z1 && testZ < z2)
                            return true;
                    }
                }
            }
        }
        return false;
    }

    private static IReadOnlyList<int>[] ParseOcclusionGroups(
        byte[] component,
        int sectorCount,
        out int[] pointers)
    {
        int environmentLength = ReadInt32(component, 4);
        int environmentEnd = 4 + environmentLength;
        int groupCount = ReadInt32(component, 8);
        if (environmentLength < 8 || environmentEnd > component.Length || groupCount != 16 ||
            12 + (groupCount * 4) > environmentEnd)
        {
            throw new InvalidDataException("The occlusion group directory is invalid.");
        }
        pointers = new int[groupCount];
        IReadOnlyList<int>[] groups = new IReadOnlyList<int>[groupCount];
        for (int group = 0; group < groupCount; group++)
        {
            int relative = ReadInt32(component, 12 + (group * 4));
            pointers[group] = relative;
            int offset = 4 + relative;
            if (offset < 12 + (groupCount * 4) || offset >= environmentEnd)
                throw new InvalidDataException($"Occlusion group {group} points outside its directory.");
            List<int> sectors = [];
            while (offset < environmentEnd && component[offset] != 0xFF)
            {
                if (component[offset] >= sectorCount)
                    throw new InvalidDataException($"Occlusion group {group} references invalid sector {component[offset]}.");
                sectors.Add(component[offset++]);
            }
            if (offset >= environmentEnd)
                throw new InvalidDataException($"Occlusion group {group} has no terminator.");
            groups[group] = sectors;
        }
        return groups;
    }

    private static (UnusedLevel65RemoteBlankPoint[] Low,
                    UnusedLevel65RemoteBlankPoint[] High,
                    int LowFaceStart,
                    int HighFaceStart) DecodeSectorTables(byte[] sector, ParsedSector layout)
    {
        int lowVertexStart = 28;
        int lowColorStart = lowVertexStart + (layout.LowDetailVertexCount * 4);
        int lowFaceStart = lowColorStart + (layout.LowDetailColorCount * 4);
        int highVertexStart = lowFaceStart + (layout.LowDetailFaceCount * 8);
        int highColorStart = highVertexStart + (layout.HighDetailVertexCount * 4);
        int highFaceStart = highColorStart + (layout.HighDetailColorCount * 8);
        if (highFaceStart + (layout.HighDetailFaceCount * 16) != sector.Length)
            throw new InvalidDataException($"Sector {layout.Index} table chain changed.");
        UnusedLevel65RemoteBlankPoint[] low = Enumerable.Range(0, layout.LowDetailVertexCount)
            .Select(index => DecodeSceneVertex(sector, ReadUInt32(sector, lowVertexStart + (index * 4))))
            .ToArray();
        UnusedLevel65RemoteBlankPoint[] high = Enumerable.Range(0, layout.HighDetailVertexCount)
            .Select(index => DecodeSceneVertex(sector, ReadUInt32(sector, highVertexStart + (index * 4))))
            .ToArray();
        return (low, high, lowFaceStart, highFaceStart);
    }

    private static UnusedLevel65RemoteBlankPoint DecodeSceneVertex(byte[] sector, uint word)
    {
        uint originXy = ReadUInt32(sector, 8);
        uint originZ = ReadUInt32(sector, 12);
        int originX = (int)(originXy >> 16);
        int originY = (int)(originXy & 0xFFFF);
        int z = (int)((originZ >> 14) & 0xFFFF) >> 2;
        int x = originX + (int)((word >> 21) & 0x7FF);
        int y = originY + (int)((word >> 10) & 0x7FF);
        z += (int)(word & 0x3FF);
        if ((ReadUInt16(sector, 4) & 0x1000) != 0)
            z >>= 3;
        return new(x, y, z);
    }

    private static UnusedLevel65RemoteBlankPoint[] DecodeCollisionTriangle(ReadOnlySpan<byte> bytes)
    {
        uint xWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes[..4]);
        uint yWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
        uint zWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
        UnusedLevel65RemoteBlankPoint p1 = new(
            (int)(xWord & 0x3FFF),
            (int)(yWord & 0x3FFF),
            (int)(zWord & 0x3FFF));
        return
        [
            p1,
            new(
                p1.X + Signed9((int)((xWord >> 14) & 0x1FF)),
                p1.Y + Signed9((int)((yWord >> 14) & 0x1FF)),
                p1.Z + (int)((zWord >> 16) & 0xFF)),
            new(
                p1.X + Signed9((int)((xWord >> 23) & 0x1FF)),
                p1.Y + Signed9((int)((yWord >> 23) & 0x1FF)),
                p1.Z + (int)((zWord >> 24) & 0xFF))
        ];
    }

    private static UnusedLevel65RemoteBlankPoint[] ResolveFacePoints(
        IReadOnlyList<UnusedLevel65RemoteBlankPoint> vertices,
        IReadOnlyList<int> slots)
    {
        if (slots.Any(index => index < 0 || index >= vertices.Count))
            throw new InvalidDataException("A scene face references an out-of-range vertex.");
        List<UnusedLevel65RemoteBlankPoint> points = [];
        HashSet<int> seen = [];
        foreach (int slot in slots)
        {
            if (seen.Add(slot))
                points.Add(vertices[slot]);
        }
        return points.ToArray();
    }

    private static int[] ReadPackedSixBitSlots(uint word) =>
    [
        (int)((word >> 26) & 0x3F),
        (int)((word >> 20) & 0x3F),
        (int)((word >> 14) & 0x3F),
        (int)((word >> 8) & 0x3F)
    ];

    private static bool HasPositiveAreaXyInteriorOverlap(
        IReadOnlyList<UnusedLevel65RemoteBlankPoint> target,
        IReadOnlyList<UnusedLevel65RemoteBlankPoint> candidate)
    {
        if (target.Count != 3 || candidate.Count < 3)
            return false;
        XyPoint[] targetTriangle = target.Select(point => new XyPoint(point.X, point.Y)).ToArray();
        for (int index = 1; index + 1 < candidate.Count; index++)
        {
            XyPoint[] candidateTriangle =
            [
                new(candidate[0].X, candidate[0].Y),
                new(candidate[index].X, candidate[index].Y),
                new(candidate[index + 1].X, candidate[index + 1].Y)
            ];
            if (TriangleIntersectionArea(targetTriangle, candidateTriangle) > 0.000001)
                return true;
        }
        return false;
    }

    private static double TriangleIntersectionArea(
        IReadOnlyList<XyPoint> subject,
        IReadOnlyList<XyPoint> clip)
    {
        List<XyPoint> polygon = subject.ToList();
        double orientation = Cross(clip[0], clip[1], clip[2]);
        if (Math.Abs(orientation) < 0.000001)
            return 0;
        for (int edge = 0; edge < 3 && polygon.Count > 0; edge++)
        {
            XyPoint a = clip[edge];
            XyPoint b = clip[(edge + 1) % 3];
            List<XyPoint> input = polygon;
            polygon = [];
            XyPoint previous = input[^1];
            double previousSide = Cross(a, b, previous);
            bool previousInside = orientation > 0 ? previousSide >= 0 : previousSide <= 0;
            foreach (XyPoint current in input)
            {
                double currentSide = Cross(a, b, current);
                bool currentInside = orientation > 0 ? currentSide >= 0 : currentSide <= 0;
                if (currentInside != previousInside)
                {
                    double denominator = previousSide - currentSide;
                    if (Math.Abs(denominator) > 0.000001)
                    {
                        double t = previousSide / denominator;
                        polygon.Add(new(
                            previous.X + ((current.X - previous.X) * t),
                            previous.Y + ((current.Y - previous.Y) * t)));
                    }
                }
                if (currentInside)
                    polygon.Add(current);
                previous = current;
                previousSide = currentSide;
                previousInside = currentInside;
            }
        }
        if (polygon.Count < 3)
            return 0;
        double twiceArea = 0;
        for (int index = 0; index < polygon.Count; index++)
        {
            XyPoint a = polygon[index];
            XyPoint b = polygon[(index + 1) % polygon.Count];
            twiceArea += (a.X * b.Y) - (a.Y * b.X);
        }
        return Math.Abs(twiceArea) * 0.5;
    }

    private static double Cross(XyPoint a, XyPoint b, XyPoint point) =>
        ((b.X - a.X) * (point.Y - a.Y)) - ((b.Y - a.Y) * (point.X - a.X));

    private static (long X, long Y, long Z) CollisionNormal(
        UnusedLevel65RemoteBlankPoint a,
        UnusedLevel65RemoteBlankPoint b,
        UnusedLevel65RemoteBlankPoint c)
    {
        long abX = b.X - a.X;
        long abY = b.Y - a.Y;
        long abZ = b.Z - a.Z;
        long acX = c.X - a.X;
        long acY = c.Y - a.Y;
        long acZ = c.Z - a.Z;
        return (
            (abY * acZ) - (abZ * acY),
            (abZ * acX) - (abX * acZ),
            (abX * acY) - (abY * acX));
    }

    private static bool IsZeroArea(IReadOnlyList<UnusedLevel65RemoteBlankPoint> points)
    {
        (long x, long y, long z) = CollisionNormal(points[0], points[1], points[2]);
        return x == 0 && y == 0 && z == 0;
    }

    private static UnusedLevel65RemoteBlankDiffRange[] BuildDiffRanges(
        byte[] before,
        byte[] after,
        IReadOnlyList<UnusedLevel65RemoteBlankStructuralPatch> patches)
    {
        List<UnusedLevel65RemoteBlankDiffRange> ranges = [];
        int index = 0;
        while (index < before.Length)
        {
            if (before[index] == after[index])
            {
                index++;
                continue;
            }
            int start = index;
            while (index < before.Length && before[index] != after[index])
                index++;
            int length = index - start;
            UnusedLevel65RemoteBlankStructuralPatch patch = patches.Single(item =>
                start >= item.DataRelativeOffset && start + length <= item.DataRelativeOffset + item.Before.Length);
            ranges.Add(new(
                patch.Kind,
                start,
                DataWadOffset + start,
                length,
                Hash(before.AsSpan(start, length)),
                Hash(after.AsSpan(start, length))));
        }
        return ranges.ToArray();
    }

    private static string HashDiffManifest(IReadOnlyList<UnusedLevel65RemoteBlankDiffRange> ranges)
    {
        StringBuilder text = new();
        foreach (UnusedLevel65RemoteBlankDiffRange range in ranges)
        {
            text.Append(range.Kind).Append('|')
                .Append(range.DataRelativeOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(range.WadOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(range.ByteLength.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(range.BeforeSha256).Append('|').Append(range.AfterSha256).Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string HashPlanIdentity(
        string sourceDataSha256,
        string outputDataSha256,
        string sourceModelSha256,
        string outputModelSha256,
        string diffManifestSha256,
        UnusedLevel65RemoteBlankSceneProof scene,
        UnusedLevel65RemoteBlankOcclusionProof occlusion,
        UnusedLevel65RemoteBlankCollisionProof collision,
        UnusedLevel65RemoteBlankSpawnProof spawn,
        UnusedLevel65RemoteBlankIsolationProof isolation)
    {
        string identity = string.Join('\n',
        [
            ProfileId,
            ExpectedSourceImageSha256,
            sourceDataSha256,
            outputDataSha256,
            sourceModelSha256,
            outputModelSha256,
            diffManifestSha256,
            scene.NewSectorSha256,
            collision.OutputTriangleHex,
            collision.OutputTreeSha256,
            collision.OutputBlocksSha256,
            collision.OutputBlocksUsedBytes.ToString(CultureInfo.InvariantCulture),
            Convert.ToHexString(OutputLanding),
            spawn.OutputPlayerAnchorSha256,
            occlusion.OutputGroupZeroCount.ToString(CultureInfo.InvariantCulture),
            isolation.MinimumCullSphereSeparation.ToString("R", CultureInfo.InvariantCulture),
            isolation.MinimumNativeVertexXyDistance.ToString("R", CultureInfo.InvariantCulture),
            isolation.MinimumInheritedMobyXyDistance.ToString("R", CultureInfo.InvariantCulture)
        ]);
        return Hash(Encoding.UTF8.GetBytes(identity));
    }

    private static void RequirePinnedComposition(Composition composition)
    {
        string outputObjectTableSha256 = Hash(composition.OutputData.AsSpan(
            ObjectTableDataRelativeOffset,
            ObjectRecordCount * ObjectRecordByteLength));
        if (composition.SourceModelSha256 != ExpectedSourceModelSha256 ||
            composition.OutputModelSha256 != ExpectedOutputModelSha256 ||
            composition.OutputDataSha256 != ExpectedOutputDataSha256 ||
            outputObjectTableSha256 != ExpectedOutputObjectTableSha256 ||
            Hash(composition.OutputPlayerAnchor) != ExpectedOutputPlayerAnchorSha256 ||
            composition.Scene.NewSectorSha256 != ExpectedNewSectorSha256 ||
            composition.Collision.SourceTreeSha256 != ExpectedSourceTreeSha256 ||
            composition.Collision.SourceBlocksSha256 != ExpectedSourceBlocksSha256 ||
            composition.Collision.OutputTreeSha256 != ExpectedOutputTreeSha256 ||
            composition.Collision.OutputBlocksSha256 != ExpectedOutputBlocksSha256 ||
            composition.Collision.OutputBlocksUsedBytes != ExpectedOutputBlocksUsedBytes ||
            composition.ChangedDataBytes != ExpectedChangedDataByteCount ||
            composition.DiffRanges.Length != ExpectedDiffRangeCount ||
            composition.DiffManifestSha256 != ExpectedDiffManifestSha256 ||
            composition.DeterministicPlanSha256 != ExpectedDeterministicPlanSha256 ||
            composition.Isolation.NearestNativeCullSectorIndex != ExpectedNearestCullSectorIndex ||
            composition.Isolation.MinimumNativeCullSurfaceDistance != ExpectedMinimumCullSurfaceDistance ||
            composition.Isolation.MinimumCullSphereSeparation != ExpectedMinimumCullSphereSeparation ||
            composition.Isolation.NearestNativeVertexSectorIndex != ExpectedNearestVertexSectorIndex ||
            composition.Isolation.NearestNativeVertexLod != ExpectedNearestVertexLod ||
            composition.Isolation.MinimumNativeVertexXyDistance != ExpectedMinimumVertexXyDistance ||
            composition.Isolation.NearestNativeCollisionTriangleIndex != ExpectedNearestCollisionTriangleIndex ||
            composition.Isolation.MinimumNativeCollisionVertexXyDistance != ExpectedMinimumCollisionVertexXyDistance ||
            composition.Isolation.NearestInheritedMobyTrueIndex != ExpectedNearestMobyTrueIndex ||
            composition.Isolation.MinimumInheritedMobyXyDistance != ExpectedMinimumMobyXyDistance)
        {
            throw new InvalidDataException("The exact remote blank-isolation static plan pins changed.");
        }
    }

    private static byte[] ApplyPatches(
        byte[] input,
        IReadOnlyList<UnusedLevel65RemoteBlankStructuralPatch> patches,
        bool reverse)
    {
        ValidatePatchSet(patches);
        foreach (UnusedLevel65RemoteBlankStructuralPatch patch in patches)
        {
            byte[] expected = reverse ? patch.After : patch.Before;
            if (!input.AsSpan(patch.DataRelativeOffset, expected.Length).SequenceEqual(expected))
                throw new InvalidDataException($"Patch {patch.Kind} lost its exact {(reverse ? "afterimage" : "preimage")}.");
        }
        byte[] output = input.ToArray();
        foreach (UnusedLevel65RemoteBlankStructuralPatch patch in patches)
            (reverse ? patch.Before : patch.After).CopyTo(output, patch.DataRelativeOffset);
        return output;
    }

    private static void ValidatePatchSet(IReadOnlyList<UnusedLevel65RemoteBlankStructuralPatch> patches)
    {
        if (patches.Count != 3 || patches.Any(patch => patch.Before.Length != patch.After.Length ||
            patch.DataRelativeOffset < 0 || patch.DataRelativeOffset + (long)patch.Before.Length > DataByteLength ||
            patch.WadOffset != DataWadOffset + patch.DataRelativeOffset))
        {
            throw new InvalidDataException("The remote blank-isolation structural patch set is invalid.");
        }
        UnusedLevel65RemoteBlankStructuralPatch[] ordered = patches.OrderBy(patch => patch.DataRelativeOffset).ToArray();
        for (int index = 1; index < ordered.Length; index++)
        {
            if (ordered[index - 1].DataRelativeOffset + ordered[index - 1].Before.Length > ordered[index].DataRelativeOffset)
                throw new InvalidDataException("The remote blank-isolation structural patches overlap.");
        }
        if (ordered[0].Kind != "id65-model" || ordered[0].DataRelativeOffset != ModelDataRelativeOffset ||
            ordered[0].Before.Length != ModelByteLength ||
            ordered[1].Kind != "id65-landing-xy" || ordered[1].DataRelativeOffset != LandingDataRelativeOffset ||
            ordered[1].Before.Length != 8 ||
            ordered[2].Kind != "id65-t92-player-anchor-xy" ||
            ordered[2].DataRelativeOffset != PlayerAnchorXyDataRelativeOffset || ordered[2].Before.Length != 8)
        {
            throw new InvalidDataException("The exact model/landing/T92 allowlist changed.");
        }
    }

    private static bool RangeAllowed(
        UnusedLevel65RemoteBlankDiffRange range,
        IReadOnlyList<UnusedLevel65RemoteBlankStructuralPatch> patches) =>
        patches.Any(patch => range.Kind == patch.Kind &&
            range.DataRelativeOffset >= patch.DataRelativeOffset &&
            range.DataRelativeOffset + range.ByteLength <= patch.DataRelativeOffset + patch.Before.Length);

    private static byte[] ComponentBytes(byte[] model, ModelLayout layout, string name)
    {
        ComponentLayout component = layout.Component(name);
        return model.AsSpan(component.Offset, component.ByteLength).ToArray();
    }

    private static byte[] SectorBytes(byte[] model, ParsedSector sector) =>
        model.AsSpan(sector.Offset, sector.ByteLength).ToArray();

    private static void RequireComponent(ModelLayout layout, string name, int offset, int length)
    {
        ComponentLayout component = layout.Component(name);
        if (component.Offset != offset || component.ByteLength != length)
            throw new InvalidDataException($"Component {name} is 0x{component.Offset:X}/0x{component.ByteLength:X}, expected 0x{offset:X}/0x{length:X}.");
    }

    private static bool IsStrictlyDescending(IReadOnlyList<int> values)
    {
        for (int index = 1; index < values.Count; index++)
        {
            if (values[index - 1] <= values[index])
                return false;
        }
        return true;
    }

    private static int[] DifferentByteIndexes(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException("Cannot compare differently sized byte ranges.");
        List<int> result = [];
        for (int index = 0; index < before.Length; index++)
        {
            if (before[index] != after[index])
                result.Add(index);
        }
        return result.ToArray();
    }

    private static int CountChangedBytes(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException("Cannot count changes across differently sized byte ranges.");
        int count = 0;
        for (int index = 0; index < before.Length; index++)
            count += before[index] == after[index] ? 0 : 1;
        return count;
    }

    private static int Signed9(int value) => value >= 256 ? value - 512 : value;
    private static double Square(double value) => value * value;
    private static long Square(long value) => value * value;

    private static int ReadInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new InvalidDataException("A 32-bit read escaped its byte range.");
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new InvalidDataException("A 32-bit read escaped its byte range.");
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset)
    {
        if (offset < 0 || offset + 2 > bytes.Length)
            throw new InvalidDataException("A 16-bit read escaped its byte range.");
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
    }

    private static void WriteInt32(Span<byte> bytes, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(offset, 4), value);

    private static void WriteUInt16(Span<byte> bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.Slice(offset, 2), value);

    private static void RequireInt32(ReadOnlySpan<byte> bytes, int offset, int expected, string label)
    {
        int actual = ReadInt32(bytes, offset);
        if (actual != expected)
            throw new InvalidDataException($"{label} is {actual}, expected {expected}.");
    }

    private static string HashFile(string path)
    {
        using FileStream input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {label} SHA-256 is {actual}, expected {expected}.");
    }

    private sealed record ComponentLayout(string Name, int Offset, int ByteLength, string Sha256);

    private sealed record ParsedSector(
        int Index,
        int Offset,
        int ByteLength,
        int GapBytesAfter,
        int LowDetailVertexCount,
        int LowDetailColorCount,
        int LowDetailFaceCount,
        int HighDetailVertexCount,
        int HighDetailColorCount,
        int HighDetailFaceCount);

    private sealed record ModelLayout(
        IReadOnlyList<ComponentLayout> Components,
        IReadOnlyList<ParsedSector> Sectors,
        int PortalCount,
        int UsedByteLength,
        int ZeroTailBytes)
    {
        public ComponentLayout Component(string name) => Components.Single(component => component.Name == name);
    }

    private sealed record CollisionLayout(
        int TriangleCount,
        int TreeOffset,
        int BlocksOffset,
        int TriangleTableOffset,
        int AssignmentsOffset,
        int FlagsOffset,
        int TreeCapacityBytes,
        int BlocksCapacityBytes);

    private sealed record CollisionCellBinding(
        UnusedLevel65RemoteBlankCollisionCell Cell,
        int TreePointerByteOffset,
        int SourceBlockWordOffset,
        int[] OrderedTriangleIndexes);

    private sealed record NativeCollisionIndex(
        byte[] TreeBytes,
        byte[] BlockBytes,
        IReadOnlyDictionary<UnusedLevel65RemoteBlankCollisionCell, CollisionCellBinding> Cells,
        int GroupStartCount,
        int TerminalSentinelWordOffset,
        int UsedBlockBytes);

    private sealed record CollisionBuild(
        CollisionLayout Layout,
        NativeCollisionIndex NativeIndex,
        NativeCollisionIndex AuthoredIndex,
        byte[] SourceTriangle,
        byte[] OutputTriangle,
        int SourceAssignment,
        int OutputAssignment,
        byte[] SourceTree,
        byte[] OutputTree,
        byte[] SourceBlocks,
        byte[] OutputBlocks,
        int OutputBlocksUsedBytes,
        int TargetTreePointer);

    private sealed record OcclusionBuild(
        IReadOnlyList<int>[] SourceGroups,
        IReadOnlyList<int>[] OutputGroups,
        int[] SourcePointers,
        int[] OutputPointers);

    private sealed record IsolationScan(
        int ScannedLowDetailFaces,
        int ScannedHighDetailFaces,
        int NativeLowDetailOverlaps,
        int NativeHighDetailOverlaps,
        int NativeCollisionOverlaps,
        int NearestCullSectorIndex,
        double MinimumCullSurfaceDistance,
        int NearestVertexSectorIndex,
        string NearestVertexLod,
        double MinimumVertexXyDistance,
        int NearestCollisionTriangleIndex,
        double MinimumCollisionVertexXyDistance,
        int NearestMobyTrueIndex,
        double MinimumMobyXyDistance,
        int MinimumMobyRawX,
        int MaximumMobyRawX,
        int MinimumMobyRawY,
        int MaximumMobyRawY,
        int MinimumMobyRawZ,
        int MaximumMobyRawZ);

    private sealed record Composition(
        byte[] SourceData,
        byte[] OutputData,
        byte[] SourceModel,
        byte[] OutputModel,
        ModelLayout SourceLayout,
        ModelLayout OutputLayout,
        string SourceDataSha256,
        string OutputDataSha256,
        string SourceModelSha256,
        string OutputModelSha256,
        byte[] OutputPlayerAnchor,
        IReadOnlyList<UnusedLevel65RemoteBlankComponentProof> Components,
        UnusedLevel65RemoteBlankSceneProof Scene,
        UnusedLevel65RemoteBlankOcclusionProof Occlusion,
        UnusedLevel65RemoteBlankCollisionProof Collision,
        UnusedLevel65RemoteBlankSpawnProof Spawn,
        UnusedLevel65RemoteBlankIsolationProof Isolation,
        IReadOnlyList<UnusedLevel65RemoteBlankStructuralPatch> Patches,
        UnusedLevel65RemoteBlankDiffRange[] DiffRanges,
        int ChangedDataBytes,
        string DiffManifestSha256,
        string DeterministicPlanSha256,
        bool InverseVerified);

    private readonly record struct XyPoint(double X, double Y);
}

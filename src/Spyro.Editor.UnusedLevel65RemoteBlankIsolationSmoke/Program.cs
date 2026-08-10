using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string lockedBase = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name",
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin");
string wrongBase = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-physical-clone",
    "Unused-Level-65-Town-Square-independent-storage-RUNTIME-CANDIDATE.bin");
Require(File.Exists(lockedBase), $"Missing exact locked display-name BIN: {lockedBase}");
Require(File.Exists(wrongBase), $"Missing wrong-image negative fixture: {wrongBase}");

string sourceHashBefore = HashFile(lockedBase);
long sourceLengthBefore = new FileInfo(lockedBase).Length;
DateTime sourceWriteBefore = File.GetLastWriteTimeUtc(lockedBase);
IReadOnlyDictionary<string, FileState> sourceDirectoryBefore = SnapshotDirectory(Path.GetDirectoryName(lockedBase)!);

UnusedLevel65RemoteBlankStaticPlan first =
    UnusedLevel65RemoteBlankIsolationConstruction.BuildStaticPlan(lockedBase);
UnusedLevel65RemoteBlankStaticPlan second =
    UnusedLevel65RemoteBlankIsolationConstruction.BuildStaticPlan(lockedBase);

Require(
    first.ProfileId == UnusedLevel65RemoteBlankIsolationConstruction.ProfileId &&
    first.SourceImageSha256 == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceImageSha256 &&
    first.ExecutableSha256 == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedExecutableSha256 &&
    first.WadLba == 37 && first.WadByteLength == 0x6C18800 && first.TargetWadEntry == 80 &&
    first.DataWadOffset == 0x6936800 && first.DataByteLength == 0x2E2000 &&
    first.ModelWadOffset == 0x6A15000 && first.ModelByteLength == 0x94800,
    "The exact locked-base image/WAD/row80 identity changed.");
Require(
    first.SourceDataSha256 == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceDataSha256 &&
    first.SourceModelSha256 == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceModelSha256 &&
    first.OutputModelSha256 == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputModelSha256 &&
    first.OutputDataSha256 == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputDataSha256 &&
    first.SourceModelSha256 == second.SourceModelSha256 &&
    first.OutputModelSha256 == second.OutputModelSha256 &&
    first.OutputDataSha256 == second.OutputDataSha256 &&
    first.DiffManifestSha256 == second.DiffManifestSha256 &&
    first.DeterministicPlanSha256 == second.DeterministicPlanSha256 &&
    first.ChangedDataByteCount == second.ChangedDataByteCount &&
    first.DiffRanges.SequenceEqual(second.DiffRanges),
    "Two independent in-memory remote compositions were not deterministic.");

UnusedLevel65RemoteBlankSceneProof scene = first.Scene;
Require(
    scene.SourceSectorCount == 216 && scene.OutputSectorCount == 217 && scene.NewSectorIndex == 216 &&
    scene.SourceEnvironmentByteLength == 0x284A4 && scene.OutputEnvironmentByteLength == 0x28518 &&
    scene.EnvironmentGrowthBytes == 0x74 && scene.NewSectorByteLength == 0x70 &&
    scene.CullCenter == new UnusedLevel65RemoteBlankPoint(384, 384, 512) &&
    scene.CullRadius == 192 && scene.CullFlags == 0 &&
    scene.EncodingOrigin == new UnusedLevel65RemoteBlankPoint(256, 256, 512) &&
    scene.Points.SequenceEqual(new[]
    {
        new UnusedLevel65RemoteBlankPoint(272, 272, 512),
        new UnusedLevel65RemoteBlankPoint(496, 272, 512),
        new UnusedLevel65RemoteBlankPoint(384, 496, 512)
    }) &&
    scene.VertexWords.SequenceEqual(new uint[] { 0x02004000, 0x1E004000, 0x1003C000 }) &&
    scene.LowDetailFaceHex == "0082100000821000" &&
    scene.HighDetailFaceHex == "000001020101000219024001080A1000" &&
    scene.MaterialTextureId == 25 && scene.AllInheritedSectorPayloadsPreserved &&
    scene.NewSectorSha256 == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedNewSectorSha256 &&
    scene.ExactHpLpPairing && scene.OrdinaryCullRoute,
    "The exact sector216 HP/LP scene encoding changed.");

UnusedLevel65RemoteBlankOcclusionProof occlusion = first.Occlusion;
Require(
    occlusion.GroupCount == 16 && occlusion.Assignment == 0 &&
    occlusion.SourceGroupZeroCount == 124 && occlusion.OutputGroupZeroCount == 125 &&
    occlusion.InsertionRelativeOffset == 0xC8 && occlusion.EnvironmentPortionByteLength == 0x71C &&
    occlusion.SourcePointers[0] == 0x48 && occlusion.OutputPointers[0] == 0x48 &&
    occlusion.OutputPointers.Skip(1).SequenceEqual(occlusion.SourcePointers.Skip(1).Select(pointer => pointer + 1)) &&
    occlusion.GroupZeroInheritedOrderPreserved && occlusion.OtherGroupsPreserved &&
    occlusion.FixedComponentLength && occlusion.TargetSectorOwned,
    "The exact fixed-length group0 sector216 occlusion ownership changed.");

UnusedLevel65RemoteBlankCollisionProof collision = first.Collision;
Require(
    collision.TriangleCount == 19_808 && collision.ReusedTriangleIndex == 13_995 &&
    collision.SourceTriangleHex == "A7A2140044631900E0010000" &&
    collision.OutputTriangleHex == "100138381001007000020000" &&
    collision.SourceAssignment == 0xFF && collision.OutputAssignment == 0 &&
    collision.SourceCell == new UnusedLevel65RemoteBlankCollisionCell(34, 35, 1) &&
    collision.TargetCell == new UnusedLevel65RemoteBlankCollisionCell(1, 1, 2) &&
    collision.TargetTreePointerRelativeOffset == 0x1254 &&
    collision.SourceOccupiedCellCount == 4_252 && collision.OutputOccupiedCellCount == 4_253 &&
    collision.BlocksCapacityBytes == 0x17984 &&
    collision.SourceCellSequence.Contains(13_995) &&
    !collision.OutputSourceCellSequence.Contains(13_995) &&
    collision.OutputTargetCellSequence.SequenceEqual(new[] { 13_995 }) &&
    collision.TargetTouchedCells.SequenceEqual(new[] { new UnusedLevel65RemoteBlankCollisionCell(1, 1, 2) }) &&
    collision.TargetLeafWasVacant && collision.NativeCellSequencesPreserved &&
    collision.SourceTreeSha256 == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceTreeSha256 &&
    collision.SourceBlocksSha256 == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceBlocksSha256 &&
    collision.OutputTreeSha256 == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputTreeSha256 &&
    collision.OutputBlocksSha256 == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputBlocksSha256 &&
    collision.OutputBlocksUsedBytes == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputBlocksUsedBytes &&
    collision.NativeOrderingPreserved && collision.UpwardWinding && collision.FixedComponentLength,
    "The exact T13995 vacant-cell collision relocation changed.");

UnusedLevel65RemoteBlankSpawnProof spawn = first.Spawn;
Require(
    spawn.LandingWadOffset == 0x6B06800 &&
    spawn.SourceLandingHex == "29E901009A8801006621000000004000" &&
    spawn.OutputLandingHex == "091800000A1800006621000000004000" &&
    spawn.LandingRawX == 6_153 && spawn.LandingRawY == 6_154 && spawn.LandingRawZ == 8_550 &&
    spawn.LandingYaw == 0x40 && spawn.PlayerAnchorTrueIndex == 92 &&
    spawn.PlayerAnchorWadOffset == 0x6B08910 && spawn.PlayerRawX == 6_153 &&
    spawn.PlayerRawY == 6_154 && spawn.PlayerRawZ == 8_704 && spawn.GroundRawZ == 8_192 &&
    spawn.OutputPlayerAnchorSha256 == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputPlayerAnchorSha256 &&
    Hash(first.OutputData.AsSpan(0x1D0170, 107 * 0x58)) ==
        UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputObjectTableSha256 &&
    spawn.LandingClearanceRaw == 358 && spawn.PlayerClearanceRaw == 512 &&
    spawn.PlayerAboveLandingRaw == 154 && spawn.AtomicXyOnly &&
    !spawn.DeathPlaneRuntimeVerified && !spawn.DeathRespawnRuntimeVerified,
    "The exact atomic landing/T92 XY or vertical relation changed.");

UnusedLevel65RemoteBlankIsolationProof isolation = first.Isolation;
Require(
    isolation.ScannedInheritedSectorCount == 216 &&
    isolation.ScannedLowDetailFaceCount == 1_437 &&
    isolation.ScannedHighDetailFaceCount == 3_887 &&
    isolation.ScannedCollisionTriangleCount == 19_808 &&
    isolation.NativeLowDetailInteriorOverlapCount == 0 &&
    isolation.NativeHighDetailInteriorOverlapCount == 0 &&
    isolation.NativeCollisionInteriorOverlapCount == 0 &&
    isolation.NearestNativeCullSectorIndex == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedNearestCullSectorIndex &&
    isolation.MinimumNativeCullSurfaceDistance == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedMinimumCullSurfaceDistance &&
    isolation.MinimumCullSphereSeparation == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedMinimumCullSphereSeparation &&
    isolation.NearestNativeVertexSectorIndex == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedNearestVertexSectorIndex &&
    isolation.NearestNativeVertexLod == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedNearestVertexLod &&
    isolation.MinimumNativeVertexXyDistance == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedMinimumVertexXyDistance &&
    isolation.NearestNativeCollisionTriangleIndex == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedNearestCollisionTriangleIndex &&
    isolation.MinimumNativeCollisionVertexXyDistance == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedMinimumCollisionVertexXyDistance &&
    isolation.InheritedMobyCount == 106 &&
    isolation.NearestInheritedMobyTrueIndex == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedNearestMobyTrueIndex &&
    isolation.MinimumInheritedMobyXyDistance == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedMinimumMobyXyDistance &&
    isolation.MinimumInheritedMobyRawX == 90_450 && isolation.MaximumInheritedMobyRawX == 144_210 &&
    isolation.MinimumInheritedMobyRawY == 96_573 && isolation.MaximumInheritedMobyRawY == 154_184 &&
    isolation.MinimumInheritedMobyRawZ == 8_192 && isolation.MaximumInheritedMobyRawZ == 15_217 &&
    isolation.AuthoredCollisionIsOnlyTargetCellSurface && isolation.NativeMobysPhysicallyDisconnected &&
    !isolation.NativeMobyRuntimeInactivityVerified && !isolation.FarLodRuntimeRouteVerified,
    "The complete exposure/cull/Moby isolation proof changed.");

Require(
    first.SourceUsedModelByteLength == 0x94508 && first.OutputUsedModelByteLength == 0x9457C &&
    first.SourceZeroTailByteCount == 0x2F8 && first.OutputZeroTailByteCount == 0x284 &&
    first.Components.Count == 9 &&
    first.Components.Where(component =>
        component.Name is "texture" or "special surface" or "cyclorama" or "portal table" or "particles" or "sound")
        .All(component => component.ContentsPreserved && component.SourceSha256 == component.OutputSha256) &&
    first.StructuralPatches.Count == 3 &&
    first.StructuralPatches.Select(patch => patch.Kind).SequenceEqual(new[]
    {
        "id65-model", "id65-landing-xy", "id65-t92-player-anchor-xy"
    }) &&
    first.DiffRanges.Count == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedDiffRangeCount &&
    first.ChangedDataByteCount == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedChangedDataByteCount &&
    first.ChangedDataByteCount == first.DiffRanges.Sum(range => range.ByteLength) &&
    first.DiffManifestSha256 == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedDiffManifestSha256 &&
    first.DeterministicPlanSha256 == UnusedLevel65RemoteBlankIsolationConstruction.ExpectedDeterministicPlanSha256 &&
    first.DiffRanges.All(range => range.Kind is
        "id65-model" or "id65-landing-xy" or "id65-t92-player-anchor-xy") &&
    first.DiffRanges.All(range =>
        range.WadOffset >= first.DataWadOffset &&
        range.WadOffset + range.ByteLength <= first.DataWadOffset + first.DataByteLength) &&
    first.PatchPreimagesVerified && first.PatchAllowlistComplete && first.ByteInverseVerified &&
    first.ProtectedSubfilesPreserved && first.InheritedObjectRowsPreserved &&
    first.RetailWadEntriesExcluded && first.ExecutableExcluded && !first.SourceImageMutationPossible &&
    !first.DisposableRuntimeCandidateAuthorized && !first.PromotionAuthorized && !first.NormalCreateBinEnabled,
    "The static-only patch/inverse/non-promotion boundary changed.");

byte[] forward = UnusedLevel65RemoteBlankIsolationConstruction.ApplyTransactional(
    first,
    first.SourceData,
    reverse: false);
Require(forward.SequenceEqual(first.OutputData), "Transactional forward application differs from composed data.");
byte[] inverse = UnusedLevel65RemoteBlankIsolationConstruction.ApplyTransactional(
    first,
    forward,
    reverse: true);
Require(inverse.SequenceEqual(first.SourceData), "Transactional reverse application differs from locked source data.");

byte[] staleForward = first.SourceData.ToArray();
staleForward[0x0DE800 + 0x2F78 + 4] ^= 0x01;
RequireAtomicReject(
    staleForward,
    () => UnusedLevel65RemoteBlankIsolationConstruction.ApplyTransactional(first, staleForward, reverse: false),
    "stale model preimage");
byte[] staleReverse = first.OutputData.ToArray();
staleReverse[0x1D0000] ^= 0x01;
RequireAtomicReject(
    staleReverse,
    () => UnusedLevel65RemoteBlankIsolationConstruction.ApplyTransactional(first, staleReverse, reverse: true),
    "stale landing afterimage");

int sourceModelData = 0x0DE800;
int sourceCollision = sourceModelData + 0x2BDC0;
int collisionTriangle = sourceCollision + 0x1E404 + (13_995 * 12);
int collisionAssignment = sourceCollision + 0x58484 + 13_995;
int targetLeaf = sourceCollision + 0x20 + 0x1254;
RunPinnedDataReject("sector count", first.SourceData, sourceModelData + 0x2F78 + 4);
RunPinnedDataReject("occlusion group0 terminator", first.SourceData, sourceModelData + 0x2B41C + 0xC8);
RunPinnedDataReject("vacant collision leaf", first.SourceData, targetLeaf);
RunPinnedDataReject("T13995 triangle preimage", first.SourceData, collisionTriangle);
RunPinnedDataReject("T13995 assignment preimage", first.SourceData, collisionAssignment);
RunPinnedDataReject("landing preimage", first.SourceData, 0x1D0000);
RunPinnedDataReject("T92 player-anchor preimage", first.SourceData, 0x1D211C);

RequireThrows(
    () => UnusedLevel65RemoteBlankIsolationConstruction.BuildStaticPlan(wrongBase),
    "wrong locked-base image hash");

Require(
    HashFile(lockedBase) == sourceHashBefore && new FileInfo(lockedBase).Length == sourceLengthBefore &&
    File.GetLastWriteTimeUtc(lockedBase) == sourceWriteBefore &&
    DirectorySnapshotsEqual(sourceDirectoryBefore, SnapshotDirectory(Path.GetDirectoryName(lockedBase)!)),
    "The static remote blank-isolation contract modified its source BIN or artifact directory.");

Console.WriteLine("ID65 remote blank-isolation exact static pins:");
Console.WriteLine($"  source model: {first.SourceModelSha256}");
Console.WriteLine($"  output model: {first.OutputModelSha256}");
Console.WriteLine($"  output data: {first.OutputDataSha256}");
Console.WriteLine($"  output object table: {Hash(first.OutputData.AsSpan(0x1D0170, 107 * 0x58))}");
Console.WriteLine($"  output T92: {spawn.OutputPlayerAnchorSha256}");
Console.WriteLine($"  collision tree: {collision.OutputTreeSha256}");
Console.WriteLine($"  collision blocks: {collision.OutputBlocksSha256}; used=0x{collision.OutputBlocksUsedBytes:X}");
Console.WriteLine($"  diff: changed={first.ChangedDataByteCount:N0}; ranges={first.DiffRanges.Count:N0}; manifest={first.DiffManifestSha256}");
Console.WriteLine($"  plan: {first.DeterministicPlanSha256}");
Console.WriteLine($"  isolation: cull={isolation.MinimumNativeCullSurfaceDistance:R}; separated={isolation.MinimumCullSphereSeparation:R}; vertex={isolation.MinimumNativeVertexXyDistance:R}; collision={isolation.MinimumNativeCollisionVertexXyDistance:R}; moby={isolation.MinimumInheritedMobyXyDistance:R}");
Console.WriteLine("PASS UnusedLevel65RemoteBlankIsolationSmoke: exact locked-base in-memory sector216, fixed occlusion ownership, vacant-cell T13995 collision, coupled spawn, full exposure/isolation, transactional inverse, atomic rejects, and no-write/non-promotion boundaries passed.");

static void RunPinnedDataReject(string label, byte[] source, int offset)
{
    byte[] mutated = source.ToArray();
    mutated[offset] ^= 0x01;
    string hashBefore = Hash(mutated);
    RequireThrows(
        () => UnusedLevel65RemoteBlankIsolationConstruction.ValidatePinnedDataForNegativeSmoke(mutated),
        label);
    Require(Hash(mutated) == hashBefore, $"The rejected {label} fixture was partially mutated.");
}

static void RequireAtomicReject(byte[] input, Func<byte[]> action, string label)
{
    string before = Hash(input);
    RequireThrows(() => _ = action(), label);
    Require(Hash(input) == before, $"The rejected {label} input was partially mutated.");
}

static void RequireThrows(Action action, string label)
{
    try
    {
        action();
    }
    catch (Exception exception) when (exception is InvalidDataException or FileNotFoundException)
    {
        return;
    }
    throw new InvalidOperationException($"The {label} negative fixture was accepted.");
}

static string FindRepositoryRoot(string? supplied)
{
    string current = string.IsNullOrWhiteSpace(supplied)
        ? Directory.GetCurrentDirectory()
        : Path.GetFullPath(supplied);
    while (true)
    {
        if (File.Exists(Path.Combine(current, "spyro-level-catalog.json")) &&
            Directory.Exists(Path.Combine(current, "src", "Spyro.Editor.Core")))
        {
            return current;
        }
        DirectoryInfo? parent = Directory.GetParent(current);
        if (parent == null)
            throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
        current = parent.FullName;
    }
}

static IReadOnlyDictionary<string, FileState> SnapshotDirectory(string path) =>
    Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
        .OrderBy(file => file, StringComparer.Ordinal)
        .ToDictionary(
            file => Path.GetRelativePath(path, file),
            file => new FileState(
                new FileInfo(file).Length,
                File.GetLastWriteTimeUtc(file),
                HashFile(file)),
            StringComparer.Ordinal);

static bool DirectorySnapshotsEqual(
    IReadOnlyDictionary<string, FileState> left,
    IReadOnlyDictionary<string, FileState> right) =>
    left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out FileState? state) && pair.Value == state);

static string HashFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record FileState(long Length, DateTime LastWriteUtc, string Sha256);

using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string foundationPrefix = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-full-authoring-foundation-native-membership",
    "Unused-Level-65-Full-Authoring-Foundation-HP-LP-45deg-NATIVE-MEMBERSHIP-RUNTIME-CANDIDATE");
string foundationImagePath = foundationPrefix + ".bin";
string foundationCuePath = foundationPrefix + ".cue";
Require(File.Exists(foundationImagePath), $"Missing exact foundation BIN: {foundationImagePath}");
Require(File.Exists(foundationCuePath), $"Missing exact foundation CUE: {foundationCuePath}");
string imageHashBefore = HashFile(foundationImagePath);
string cueHashBefore = HashFile(foundationCuePath);
long imageLengthBefore = new FileInfo(foundationImagePath).Length;
long cueLengthBefore = new FileInfo(foundationCuePath).Length;
DateTime imageWriteBefore = File.GetLastWriteTimeUtc(foundationImagePath);
DateTime cueWriteBefore = File.GetLastWriteTimeUtc(foundationCuePath);
string[] artifactFilesBefore = Directory.GetFiles(Path.GetDirectoryName(foundationImagePath)!)
    .Select(Path.GetFullPath).Order(StringComparer.Ordinal).ToArray();

UnusedLevel65FoundationTerrainManifest foundationManifest =
    UnusedLevel65FoundationTerrainComposer.CreateManifest(includeOptionalSecondTile: false);
UnusedLevel65FoundationTerrainManifest twoTileManifest =
    UnusedLevel65FoundationTerrainComposer.CreateManifest(includeOptionalSecondTile: true);
UnusedLevel65FoundationTerrainStaticPlan foundation =
    UnusedLevel65FoundationTerrainComposer.BuildStaticPlan(foundationImagePath, foundationManifest);
UnusedLevel65FoundationTerrainStaticPlan first =
    UnusedLevel65FoundationTerrainComposer.BuildStaticPlan(foundationImagePath, twoTileManifest);
UnusedLevel65FoundationTerrainStaticPlan repeat =
    UnusedLevel65FoundationTerrainComposer.BuildStaticPlan(foundationImagePath, twoTileManifest);
UnusedLevel65FoundationTerrainManifest removedManifest =
    UnusedLevel65FoundationTerrainComposer.RemoveOptionalSecondTile(twoTileManifest);
UnusedLevel65FoundationTerrainStaticPlan inverse =
    UnusedLevel65FoundationTerrainComposer.BuildStaticPlan(foundationImagePath, removedManifest);

Require(foundation.ProfileId == UnusedLevel65FoundationTerrainComposer.ProfileId &&
        first.ProfileId == foundation.ProfileId && repeat.ProfileId == foundation.ProfileId,
    "The exact two-tile composer profile id changed.");
Require(foundation.SourceImageSha256 == UnusedLevel65FoundationTerrainComposer.ExpectedFoundationImageSha256 &&
        first.SourceImageSha256 == foundation.SourceImageSha256 &&
        foundation.SourceModelSha256 == UnusedLevel65FoundationTerrainComposer.ExpectedFoundationModelSha256 &&
        foundation.OutputModelSha256 == UnusedLevel65FoundationTerrainComposer.ExpectedFoundationModelSha256 &&
        first.OutputModelSha256 == UnusedLevel65FoundationTerrainComposer.ExpectedTwoTileModelSha256,
    "The foundation/two-tile model hash oracle changed.");
Require(foundation.OptionalTileCount == 0 && first.OptionalTileCount == 1 &&
        foundation.ModelByteLength == 0x94800 && first.ModelByteLength == 0x94800 &&
        foundation.SourceUsedModelBytes == 0x94538 && foundation.OutputUsedModelBytes == 0x94538 &&
        first.OutputUsedModelBytes == 0x94568 &&
        foundation.SourceZeroTailBytes == 0x2C8 && foundation.OutputZeroTailBytes == 0x2C8 &&
        first.OutputZeroTailBytes == 0x298 &&
        first.EnvironmentGrowthBytes == 0x20 && first.CollisionGrowthBytes == 0x10,
    "The exact model-growth/tail contract changed.");
Require(foundation.OutputModelBytes.SequenceEqual(foundation.SourceModelBytes) &&
        inverse.OutputModelBytes.SequenceEqual(foundation.SourceModelBytes) &&
        inverse.OutputModelSha256 == UnusedLevel65FoundationTerrainComposer.ExpectedFoundationModelSha256 &&
        inverse.OutputZeroTailBytes == 0x2C8,
    "The empty optional manifest did not reproduce the exact foundation model.");

Require(first.Manifest.LockedFoundationTileId == UnusedLevel65FoundationTerrainComposer.LockedTileId &&
        first.Manifest.LockedFoundationTile.TileId == UnusedLevel65FoundationTerrainComposer.LockedTileId &&
        first.Manifest.OptionalTiles.Count == 1 &&
        first.Manifest.OptionalTiles[0].TileId == UnusedLevel65FoundationTerrainComposer.OptionalTileId &&
        first.Manifest.OptionalTiles[0].LowDetailVertexHandles.SequenceEqual(
            new[] { "foundation-b", "foundation-d", "foundation-c" }) &&
        first.Manifest.OptionalTiles[0].HighDetailVertexHandles.SequenceEqual(
            first.Manifest.OptionalTiles[0].LowDetailVertexHandles) &&
        first.Manifest.Vertices.Single(vertex => vertex.Handle == "foundation-d").Point ==
            new UnusedLevel65FoundationTerrainPoint(7954, 6474, 640),
    "The stable handle topology or locked/optional tile ownership changed.");
Require(first.Sector.SectorIndex == 213 && first.Sector.WadOffset == 0x6A3E8B4 &&
        first.Sector.ByteLength == 0x1168 &&
        first.Sector.LowDetailVertexCount == 39 && first.Sector.LowDetailColorCount == 48 &&
        first.Sector.LowDetailFaceCount == 23 &&
        first.Sector.HighDetailVertexCount == 144 && first.Sector.HighDetailColorCount == 185 &&
        first.Sector.HighDetailFaceCount == 115 &&
        first.Sector.Sha256 == UnusedLevel65FoundationTerrainComposer.ExpectedTwoTileSectorSha256,
    "The exact sector-213 HP/LP count, position, or hash changed.");

UnusedLevel65FoundationTerrainCollisionReadback collision = first.Collision;
Require(collision.WadOffset == 0x6A40E10 && collision.ByteLength == 0x5FAF8 &&
        collision.TriangleCount == 19_809 && collision.FlagCount == 0x2904 &&
        collision.TreeCapacityBytes == 0x6A60 && collision.BlocksCapacityBytes == 0x17984 &&
        collision.UsedBlockBytes == 0x17968 && collision.FreeBlockBytes == 0x1C &&
        collision.NativeCellCount == 4_252 && collision.GroupStartCount == 4_246 &&
        collision.AppendedTriangleIndex == 19_808 &&
        collision.AppendedTriangleWadOffset == 0x6A99294 &&
        collision.AppendedAssignmentWadOffset == 0x6A9E000 &&
        collision.AppendedTriangleHex == "D21E10E0CA18204000028080" &&
        collision.TreeSha256 == UnusedLevel65FoundationTerrainComposer.ExpectedTwoTileTreeSha256 &&
        collision.BlocksSha256 == UnusedLevel65FoundationTerrainComposer.ExpectedTwoTileBlocksSha256 &&
        collision.CollisionSha256 == UnusedLevel65FoundationTerrainComposer.ExpectedTwoTileCollisionSha256 &&
        collision.ChangedCells.SequenceEqual(new[]
        {
            new UnusedLevel65FoundationCollisionCell(30, 24, 2),
            new UnusedLevel65FoundationCollisionCell(30, 25, 2),
            new UnusedLevel65FoundationCollisionCell(31, 25, 2)
        }) &&
        collision.AllUnchangedCellSequencesPreserved && collision.NativeDescendingOrderPreserved &&
        collision.AssignmentZeroVerified && collision.ExistingFlagsPreserved &&
        collision.OrdinarySurfaceVerified,
    "The appended collision row, assignment, cells, capacity, ordering, or hashes changed.");

Require(first.Exposure.LowDetailOverlaps.SequenceEqual(
            new[] { "124:0:480:480", "213:1:512:512", "213:3:512:512" }) &&
        first.Exposure.HighDetailOverlaps.SequenceEqual(
            new[] { "213:37:512:512", "213:43:512:512" }) &&
        first.Exposure.CollisionOverlaps.SequenceEqual(
            new[] { "1354:512:512", "1400:512:512", "1401:512:512" }) &&
        first.Exposure.NoHigherNativeLowDetailSurface &&
        first.Exposure.NoHigherNativeHighDetailSurface &&
        first.Exposure.NoHigherNativeCollisionSurface &&
        first.Exposure.AuthoredSurfaceTopmostOverOpenInterior &&
        first.Exposure.SectorCullSphereContainsAllPoints,
    "The exact full-native exposure/cull proof changed.");

Dictionary<string, (long Wad, int Length)> expectedComponents = new(StringComparer.Ordinal)
{
    ["texture"] = (0x6A15000, 0x2F78),
    ["environment"] = (0x6A17F78, 0x284F4),
    ["occlusion"] = (0x6A4046C, 0x974),
    ["special-surface"] = (0x6A40DE0, 0x30),
    ["collision"] = (0x6A40E10, 0x5FAF8),
    ["cyclorama"] = (0x6AA0908, 0x84E4),
    ["portal-table"] = (0x6AA8DEC, 0x4),
    ["particles"] = (0x6AA8DF0, 0x80),
    ["sound"] = (0x6AA8E70, 0x6F8)
};
Require(first.Components.Count == expectedComponents.Count && first.Components.All(component =>
        expectedComponents.TryGetValue(component.Name, out (long Wad, int Length) expected) &&
        component.WadOffset == expected.Wad && component.ByteLength == expected.Length),
    "A two-tile component relocation/length changed.");
Require(first.Components.Single(component => component.Name == "occlusion").Sha256 ==
            UnusedLevel65FoundationTerrainComposer.ExpectedOcclusionSha256 &&
        first.Components.Where(component => component.Name is not "environment" and not "collision")
            .All(component => component.ContentsPreserved),
    "A protected component changed content during two-tile composition.");
Require(first.LockedFoundationTilePreserved && first.ExplicitVertexHandlesVerified &&
        first.HpLpPairingVerified && first.CollisionIndexRepacked &&
        first.OcclusionOwnershipVerified && first.ExactInverseVerified &&
        first.SourceModelPreserved && first.DeterministicReadbackRequired &&
        !first.WritesDiscImage && !first.WritesCue && !first.AppIntegrated &&
        !first.CreateBinEnabled && !first.ReleaseIntegrated &&
        !first.PromotionAuthorized && !first.Publishable,
    "A static-only/no-write/nonpromotion safety flag changed.");

Require(first.OutputModelBytes.SequenceEqual(repeat.OutputModelBytes) &&
        first.OutputModelSha256 == repeat.OutputModelSha256 &&
        first.DeterministicPlanSha256 == UnusedLevel65FoundationTerrainComposer.ExpectedTwoTilePlanSha256 &&
        first.DeterministicPlanSha256 == repeat.DeterministicPlanSha256 &&
        first.Collision.TreeSha256 == repeat.Collision.TreeSha256 &&
        first.Collision.BlocksSha256 == repeat.Collision.BlocksSha256 &&
        first.Collision.ChangedCells.SequenceEqual(repeat.Collision.ChangedCells) &&
        first.Components.SequenceEqual(repeat.Components),
    "Repeated exact two-tile composition is not deterministic.");

int rejectionCount = 0;
void ExpectReject(
    string label,
    UnusedLevel65FoundationTerrainManifest manifest,
    string requiredText,
    UnusedLevel65FoundationTerrainSafetyLimits? limits = null)
{
    bool rejected = false;
    try
    {
        _ = limits == null
            ? UnusedLevel65FoundationTerrainComposer.BuildStaticPlan(foundationImagePath, manifest)
            : UnusedLevel65FoundationTerrainComposer.BuildStaticPlanForSmoke(foundationImagePath, manifest, limits);
    }
    catch (Exception ex) when (ex.ToString().Contains(requiredText, StringComparison.OrdinalIgnoreCase))
    {
        rejected = true;
    }
    Require(rejected, $"Atomic rejection `{label}` was not observed with text `{requiredText}`.");
    rejectionCount++;
    RequireSourceUntouched();
}

UnusedLevel65FoundationTerrainTile exactTile = twoTileManifest.OptionalTiles[0];
UnusedLevel65FoundationTerrainManifest WithOptional(
    IReadOnlyList<UnusedLevel65FoundationTerrainVertex> vertices,
    IReadOnlyList<UnusedLevel65FoundationTerrainTile> tiles) =>
    new(
        twoTileManifest.LockedFoundationTileId,
        vertices,
        twoTileManifest.LockedFoundationTile,
        tiles);

ExpectReject(
    "HP/LP mismatch",
    WithOptional(twoTileManifest.Vertices,
        [exactTile with { HighDetailVertexHandles = new[] { "foundation-b", "foundation-c", "foundation-d" } }]),
    "LP and HP topology");
ExpectReject(
    "cross-sector tile",
    WithOptional(twoTileManifest.Vertices, [exactTile with { SectorIndex = 212 }]),
    "only scene sector 213");
ExpectReject(
    "downward winding",
    WithOptional(twoTileManifest.Vertices,
        [exactTile with
        {
            LowDetailVertexHandles = new[] { "foundation-b", "foundation-c", "foundation-d" },
            HighDetailVertexHandles = new[] { "foundation-b", "foundation-c", "foundation-d" }
        }]),
    "upward winding");

List<UnusedLevel65FoundationTerrainVertex> zeroVertices =
    [.. twoTileManifest.Vertices, new("zero-copy", new(7890, 6346, 512))];
ExpectReject(
    "zero area",
    WithOptional(zeroVertices,
        [exactTile with
        {
            LowDetailVertexHandles = new[] { "foundation-b", "zero-copy", "foundation-c" },
            HighDetailVertexHandles = new[] { "foundation-b", "zero-copy", "foundation-c" }
        }]),
    "zero area");

List<UnusedLevel65FoundationTerrainVertex> cullVertices =
    [.. twoTileManifest.Vertices.Where(vertex => vertex.Handle != "foundation-d"),
        new("foundation-d", new(9000, 6474, 640))];
ExpectReject("outside cull", WithOptional(cullVertices, [exactTile]), "cull sphere");
List<UnusedLevel65FoundationTerrainVertex> packVertices =
    [.. twoTileManifest.Vertices.Where(vertex => vertex.Handle != "foundation-d"),
        new("foundation-d", new(7954, 6474, 800))];
ExpectReject("collision packing", WithOptional(packVertices, [exactTile]), "cannot pack into the native row");

List<UnusedLevel65FoundationTerrainVertex> absentVertices =
[
    .. foundationManifest.Vertices,
    new("absent-e", new(8448, 6400, 512)),
    new("absent-f", new(8500, 6400, 512)),
    new("absent-g", new(8448, 6450, 512))
];
UnusedLevel65FoundationTerrainTile absentTile = exactTile with
{
    LowDetailVertexHandles = new[] { "absent-e", "absent-f", "absent-g" },
    HighDetailVertexHandles = new[] { "absent-e", "absent-f", "absent-g" }
};
ExpectReject("missing collision cell", WithOptional(absentVertices, [absentTile]), "absent from the native tree");
ExpectReject("nonordinary surface", WithOptional(twoTileManifest.Vertices,
        [exactTile with { OrdinaryCollision = false, CollisionSurfaceIndex = 1 }]),
    "Nonordinary collision flags");
ExpectReject("wrong occlusion owner", WithOptional(twoTileManifest.Vertices,
        [exactTile with { OcclusionAssignment = 3 }]),
    "does not own sector");
ExpectReject("insufficient model tail", twoTileManifest, "model-tail bytes",
    new(AvailableModelTailBytes: 0x2F));
ExpectReject("insufficient block capacity", twoTileManifest, "block bytes",
    new(CollisionBlockCapacityBytes: 0x17966));
ExpectReject("collision 15-bit limit", twoTileManifest, "15-bit triangle",
    new(MaximumTriangleCount: 19_808));

UnusedLevel65FoundationTerrainTile[] tooManyFaces = Enumerable.Range(0, 142)
    .Select(index => exactTile with { TileId = $"capacity-face-{index}" })
    .ToArray();
ExpectReject("face count capacity", WithOptional(twoTileManifest.Vertices, tooManyFaces), "face-count byte");

List<UnusedLevel65FoundationTerrainVertex> tooManyVertices = [.. foundationManifest.Vertices];
List<UnusedLevel65FoundationTerrainTile> vertexCapacityTiles = [];
for (int tileIndex = 0; tileIndex < 8; tileIndex++)
{
    string[] handles = new string[3];
    for (int pointIndex = 0; pointIndex < 3; pointIndex++)
    {
        string handle = $"capacity-v-{tileIndex * 3 + pointIndex}";
        handles[pointIndex] = handle;
        int baseX = 7700 + (tileIndex * 5);
        int baseY = 6200 + (tileIndex * 5);
        UnusedLevel65FoundationTerrainPoint point = pointIndex switch
        {
            0 => new(baseX, baseY, 512),
            1 => new(baseX + 4, baseY, 512),
            _ => new(baseX, baseY + 4, 512)
        };
        tooManyVertices.Add(new(handle, point));
    }
    vertexCapacityTiles.Add(exactTile with
    {
        TileId = $"capacity-vertex-{tileIndex}",
        LowDetailVertexHandles = handles,
        HighDetailVertexHandles = handles
    });
}
tooManyVertices.Add(new("capacity-v-24", new(7740, 6240, 512)));
tooManyVertices.Add(new("capacity-v-25", new(7744, 6240, 512)));
string[] lastCapacityHandles = ["capacity-v-25", "capacity-v-24", "capacity-v-0"];
vertexCapacityTiles.Add(exactTile with
{
    TileId = "capacity-vertex-8",
    LowDetailVertexHandles = lastCapacityHandles,
    HighDetailVertexHandles = lastCapacityHandles
});
ExpectReject("LP vertex capacity", WithOptional(tooManyVertices, vertexCapacityTiles), "LP six-bit vertex");

bool removeMissingRejected = false;
try
{
    _ = UnusedLevel65FoundationTerrainComposer.RemoveOptionalSecondTile(foundationManifest);
}
catch (InvalidDataException ex) when (ex.Message.Contains("not active", StringComparison.OrdinalIgnoreCase))
{
    removeMissingRejected = true;
}
Require(removeMissingRejected, "Removing an inactive optional tile was not rejected.");
rejectionCount++;

ExpectReject("tampered tile id", WithOptional(twoTileManifest.Vertices,
        [exactTile with { TileId = "tampered-id" }]),
    "two-tile oracle");

byte[] mutatedModel = foundation.SourceModelBytes.ToArray();
mutatedModel[^1] = 1;
bool wrongModelRejected = false;
try
{
    _ = UnusedLevel65FoundationTerrainComposer.BuildStaticPlanFromModelForSmoke(mutatedModel, twoTileManifest);
}
catch (InvalidDataException ex) when (ex.Message.Contains("accepts only exact foundation model", StringComparison.OrdinalIgnoreCase))
{
    wrongModelRejected = true;
}
Require(wrongModelRejected, "A mutated foundation model was not rejected before composition.");
rejectionCount++;

RequireSourceUntouched();
string[] artifactFilesAfter = Directory.GetFiles(Path.GetDirectoryName(foundationImagePath)!)
    .Select(Path.GetFullPath).Order(StringComparer.Ordinal).ToArray();
Require(artifactFilesAfter.SequenceEqual(artifactFilesBefore),
    "The static composer created or removed a foundation artifact.");

Console.WriteLine("PASS: exact ID65 foundation two-tile/inverse static composer");
Console.WriteLine($"Foundation model: {foundation.OutputModelSha256}");
Console.WriteLine($"Two-tile model: {first.OutputModelSha256}");
Console.WriteLine($"Plan digest: {first.DeterministicPlanSha256}");
Console.WriteLine($"Collision tree/blocks: {first.Collision.TreeSha256}/{first.Collision.BlocksSha256}");
Console.WriteLine($"Tail: 0x{foundation.OutputZeroTailBytes:X}->0x{first.OutputZeroTailBytes:X}->0x{inverse.OutputZeroTailBytes:X}");
Console.WriteLine($"Atomic rejection rows: {rejectionCount}");
Console.WriteLine("Writes: BIN=false CUE=false App=false CreateBIN=false Release=false");

void RequireSourceUntouched()
{
    Require(HashFile(foundationImagePath) == imageHashBefore &&
            HashFile(foundationCuePath) == cueHashBefore &&
            new FileInfo(foundationImagePath).Length == imageLengthBefore &&
            new FileInfo(foundationCuePath).Length == cueLengthBefore &&
            File.GetLastWriteTimeUtc(foundationImagePath) == imageWriteBefore &&
            File.GetLastWriteTimeUtc(foundationCuePath) == cueWriteBefore,
        "The static terrain composer changed its exact foundation BIN/CUE source pair.");
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
            return current;
        DirectoryInfo? parent = Directory.GetParent(current);
        if (parent == null)
            throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
        current = parent.FullName;
    }
}

static string HashFile(string path)
{
    using FileStream input = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

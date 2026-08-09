using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spyro.Editor.Core;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

const string RetailBinSha256 = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";

string workspaceRoot = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Directory.GetCurrentDirectory();
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
string tempRoot = Path.Combine(
    Path.GetTempPath(),
    $"Spyro.Editor.TerrainCollisionFanSmoke-{Guid.NewGuid():N}");

Assert(File.Exists(sourceImagePath), $"Retail source BIN is missing: {sourceImagePath}");
Assert(File.Exists(sourceCuePath), $"Retail source CUE is missing: {sourceCuePath}");
Assert(
    Sha256File(sourceImagePath).Equals(RetailBinSha256, StringComparison.OrdinalIgnoreCase),
    "Retail source BIN hash is not the exact audited fixture.");

LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
Dictionary<string, LevelContext> contexts = new(StringComparer.OrdinalIgnoreCase);
Directory.CreateDirectory(tempRoot);

StableFixture[] stableFixtures =
[
    new(
        Name: "Town Square shared ridge fan",
        LevelKey: "townsquare",
        SectorIndex: 213,
        FaceIndex: 37,
        VertexIndexes: [40, 57, 48, 39],
        OriginalPoints:
        [
            new(7762, 6346, 512),
            new(7890, 6346, 512),
            new(7890, 6474, 512),
            new(7762, 6474, 512)
        ],
        VertexDeltaZ: [0, 0, 96, 96],
        ExpectedAffectedFaces: ["213:25:hp", "213:26:hp", "213:29:hp", "213:37:hp", "213:38:hp", "213:43:hp"],
        FanIndexes: [1353, 1354, 1360, 1361, 1363, 1369, 1370, 1395, 1400, 1401],
        ExpectedAssignments: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        LookupReferences: LookupReferences(
            (1353, [0x12B8E72, 0x12B9108]),
            (1354, [0x12B8E70, 0x12B9106]),
            (1360, [0x12B90E2, 0x12B9104]),
            (1361, [0x12B90E0, 0x12B9102]),
            (1363, [0x12B8E6A, 0x12B90DC, 0x12B90FE]),
            (1369, [0x12B90F8]),
            (1370, [0x12B90F6]),
            (1395, [0x12B90EC, 0x12B9124]),
            (1400, [0x12B8E60, 0x12B8E94, 0x12B90EA, 0x12B9122]),
            (1401, [0x12B8E92, 0x12B90E8, 0x12B9120])),
        SourceFanSha256: "f01b62ca1acf086118c3905addc6f63ad6d6b9ba51c2595077093f27ba89d560",
        PatchedFanSha256: "c3e987a7fa225a1d8d30f59f7d2c2dbb5a86e40b2c6a29030a8dc9ac08a19f00"),
    new(
        Name: "Dark Hollow cyclic-rebase fan",
        LevelKey: "darkhollow",
        SectorIndex: 0,
        FaceIndex: 37,
        VertexIndexes: [60, 60, 16, 39],
        OriginalPoints:
        [
            new(1169, 3653, 1072),
            new(1159, 3572, 1072),
            new(1296, 3584, 1072)
        ],
        VertexDeltaZ: [16, 0, 0],
        ExpectedAffectedFaces: ["0:36:hp", "0:37:hp", "0:38:hp", "0:39:hp"],
        FanIndexes: [2838, 2848, 3002, 3003, 3013, 3054, 3055],
        ExpectedAssignments: [1, 1, 1, 1, 2, 2, 2],
        LookupReferences: LookupReferences(
            (2838, [0x1010064]),
            (2848, [0x1010050]),
            (3002, [0x101003C, 0x10100A0]),
            (3003, [0x101003A, 0x101009E]),
            (3013, [0x100FC54, 0x100FC98, 0x1010038, 0x101009C]),
            (3054, [0x100FC4E, 0x1010028]),
            (3055, [0x100FC4C, 0x1010026])),
        SourceFanSha256: "2583ffcadb5c91a96f58ea204a52bf491198a010bf7a0f3937c75433bcd17237",
        PatchedFanSha256: "daef23d08c99b4c0ba72f9b35adb8b2cdeff21aed413d77aa28d72c2fd9fbbbc"),
    new(
        Name: "Dry Canyon mixed top-and-side fan",
        LevelKey: "drycanyon",
        SectorIndex: 40,
        FaceIndex: 94,
        VertexIndexes: [130, 131, 129, 127],
        OriginalPoints:
        [
            new(9027, 8047, 1136),
            new(9159, 8133, 1136),
            new(9136, 8137, 1136),
            new(9032, 8070, 1136)
        ],
        VertexDeltaZ: [16, 0, 0, 0],
        ExpectedAffectedFaces: ["40:94:hp", "40:96:hp", "40:99:hp", "40:100:hp"],
        FanIndexes: [4643, 4645, 4649, 4654, 4658, 4668],
        ExpectedAssignments: [0, 0, 0, 0, 0, 0],
        LookupReferences: LookupReferences(
            (4643, [0x1C58DC6]),
            (4645, [0x1C58D72, 0x1C58DC2]),
            (4649, [0x1C58DBA]),
            (4654, [0x1C58DB0]),
            (4658, [0x1C58D68, 0x1C58DA8]),
            (4668, [0x1C58D5A, 0x1C58D9C])),
        SourceFanSha256: "6b0018777471ae79714bfeb32310bb2060105df764090bcba00b6df1a1f645a7",
        PatchedFanSha256: "510fe6cf75cc0b2bccf9c2df9b82a54eff821c70ab349198cfae674dab96fa36",
        ProtectedCollisionPoint: new(9027, 8047, 1114)),
    new(
        Name: "Artisans same-cell upper boundary",
        LevelKey: "artisans",
        SectorIndex: 2,
        FaceIndex: 64,
        VertexIndexes: [82, 83, 87, 86],
        OriginalPoints:
        [
            new(6908, 3541, 812),
            new(7009, 3541, 866),
            new(7049, 3421, 841),
            new(6951, 3421, 760)
        ],
        VertexDeltaZ: [0, 0, 0, 7],
        ExpectedAffectedFaces: ["2:64:hp", "2:65:hp", "2:68:hp", "2:69:hp"],
        FanIndexes: [1354, 1360, 1366, 1399, 1400, 1401],
        ExpectedAssignments: [255, 255, 255, 255, 255, 255],
        LookupReferences: LookupReferences(
            (1354, [0x92E88A, 0x92EBEC, 0x92EBFA]),
            (1360, [0x92E884, 0x92EBF8]),
            (1366, [0x92E880, 0x92EBF4, 0x93048C]),
            (1399, [0x92EBF2, 0x93048A]),
            (1400, [0x92EBF0, 0x930470, 0x930488]),
            (1401, [0x92EBD2, 0x92EBEE, 0x93046E, 0x930486])),
        SourceFanSha256: "a7da20a9627e52af86c9d1637d7a1b221d93137d2a6c233a12a3fa7e0d2a1930",
        PatchedFanSha256: "4fc5ab25467dacad2ca7cb45477857eb8746ecc1eb467169bfc44dc57dd53ab3")
];

try
{
    foreach (string levelKey in new[] { "townsquare", "darkhollow", "drycanyon", "artisans", "alpineridge" })
        contexts[levelKey] = await BuildLevelContextAsync(levelKey);

    foreach (StableFixture fixture in stableFixtures)
        await VerifyStableFixtureAsync(fixture);

    await VerifyCellCrossingRejectAsync();
    await VerifyPhysicalTargetConflictRejectAsync();
    await VerifyAlpineAliasRejectAsync();
    await VerifyXyRejectAsync();
    await VerifyLowDetailRejectAsync();
    await VerifyUnavailableCollisionContextRejectAsync();
    await VerifyUnmatchedFanRejectAsync();
    await VerifyHistoricalHiddenControlAliasRejectAsync();
    await VerifyVerticalShadowRejectAsync();

    Assert(
        Sha256File(sourceImagePath).Equals(RetailBinSha256, StringComparison.OrdinalIgnoreCase),
        "The retail source BIN changed while the smoke was running.");
    Console.WriteLine("PASS TerrainCollisionFanSmoke: 4 exact stable fan rows and 9 atomic rejection rows passed.");
}
finally
{
    if (Directory.Exists(tempRoot) &&
        Path.GetFileName(tempRoot).StartsWith("Spyro.Editor.TerrainCollisionFanSmoke-", StringComparison.Ordinal))
    {
        Directory.Delete(tempRoot, recursive: true);
    }
}

async Task<LevelContext> BuildLevelContextAsync(string levelKey)
{
    LevelDefinition level = catalog.FindByKey(levelKey)
        ?? throw new InvalidOperationException($"{levelKey} is missing from the level catalog.");
    string overlayPath = Path.Combine(workspaceRoot, "editor-cache", $"{levelKey}-runtime-scene-editor-overlay.json");
    Assert(File.Exists(overlayPath), $"Missing exact source-derived overlay: {overlayPath}");
    GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
    string sourceSearchPath = Path.Combine(tempRoot, $"{levelKey}-source-derived-search.json");
    TerrainSourceSearchResult sourceSearch = await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(
        new SourceDerivedTerrainSourceSearchRequest(
            sourceImagePath,
            sourceSearchPath,
            level,
            geometry));
    Assert(sourceSearch.Report.MissingSectorCount == 0, $"{level.DisplayName} source-derived map has missing sectors.");
    Assert(sourceSearch.Report.AmbiguousSectorCount == 0, $"{level.DisplayName} source-derived map has ambiguous sectors.");
    return new LevelContext(level, overlayPath, geometry, sourceSearchPath, sourceSearch.Report);
}

async Task VerifyStableFixtureAsync(StableFixture fixture)
{
    LevelContext context = contexts[fixture.LevelKey];
    GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(context.OverlayPath);
    TerrainPolygon face = FindFace(geometry, fixture.SectorIndex, fixture.FaceIndex);
    VerifyFacePreimage(face, fixture.VertexIndexes, fixture.OriginalPoints, fixture.Name);
    VerifyOneSourceHit(context.SourceSearchReport, face.RuntimeKey, fixture.Name);

    face.ApplyTerrainVertexDeltas(fixture.VertexDeltaZ);
    string editsPath = TempPath($"stable-{fixture.LevelKey}-edits.json");
    int editCount = await TerrainEditStore.SaveAsync(editsPath, [face], fixture.Name);
    face.ResetTerrainEdit();
    Assert(editCount == 1, $"{fixture.Name}: normal edit store did not save exactly one edit.");

    TerrainPatchPlan plan = TerrainPatchExporter.BuildPlan(
        sourceImagePath,
        sourceCuePath,
        TempPath($"stable-{fixture.LevelKey}.bin"),
        TempPath($"stable-{fixture.LevelKey}.cue"),
        context.Level,
        "",
        context.SourceSearchPath,
        editsPath);

    Assert(plan.SkippedEdits.Count == 0, $"{fixture.Name}: unexpected exporter skip: {string.Join(" | ", plan.SkippedEdits)}");
    Assert(
        plan.Patches.All(patch =>
            patch.Kind.Equals("visual-hp-topology-safe", StringComparison.Ordinal) ||
            patch.Kind.Equals("collision-triangle-fan", StringComparison.Ordinal)),
        $"{fixture.Name}: a legacy side-wall/index/append/sector-shift patch escaped into the stable plan: " +
        string.Join(", ", plan.Patches.Select(patch => patch.Kind).Distinct()));

    TerrainPatch[] visualPatches = plan.Patches
        .Where(patch => patch.Kind.Equals("visual-hp-topology-safe", StringComparison.Ordinal))
        .ToArray();
    int expectedVisualPatchCount = fixture.VertexDeltaZ
        .Select((delta, index) => (delta, vertex: fixture.VertexIndexes[index]))
        .Where(item => Math.Abs(item.delta) > 0.0001f)
        .Select(item => item.vertex)
        .Distinct()
        .Count();
    Assert(
        visualPatches.Length == expectedVisualPatchCount,
        $"{fixture.Name}: expected {expectedVisualPatchCount} physical visual patch(es), got {visualPatches.Length}.");
    HashSet<string> affectedFaces = visualPatches
        .SelectMany(patch => Regex.Matches(patch.Description, @"\b\d+:\d+:hp\b").Select(match => match.Value))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    Assert(
        affectedFaces.SetEquals(fixture.ExpectedAffectedFaces),
        $"{fixture.Name}: affected shared face closure mismatch. Expected [{string.Join(", ", fixture.ExpectedAffectedFaces)}], " +
        $"got [{string.Join(", ", affectedFaces.Order())}].");

    TerrainPatch[] collisionPatches = plan.Patches
        .Where(patch => patch.Kind.Equals("collision-triangle-fan", StringComparison.Ordinal))
        .ToArray();
    Dictionary<int, TerrainPatch> collisionByIndex = collisionPatches.ToDictionary(ParseTriangleIndex);
    Assert(
        collisionByIndex.Keys.ToHashSet().SetEquals(fixture.FanIndexes),
        $"{fixture.Name}: exact native collision fan mismatch. Expected [{string.Join(",", fixture.FanIndexes)}], " +
        $"got [{string.Join(",", collisionByIndex.Keys.Order())}].");
    Assert(
        plan.PatchCount == visualPatches.Length + fixture.FanIndexes.Length,
        $"{fixture.Name}: stable plan contains an unexpected extra patch.");

    Dictionary<Point3, Point3> targetByOriginal = fixture.OriginalPoints
        .Select((point, index) => (point, target: point with { Z = point.Z + (int)fixture.VertexDeltaZ[index] }))
        .Where(item => item.point != item.target)
        .ToDictionary(item => item.point, item => item.target);
    List<byte> fanBefore = [];
    List<byte> fanAfter = [];
    int protectedBefore = 0;
    int protectedAfter = 0;
    foreach (int triangleIndex in fixture.FanIndexes)
    {
        TerrainPatch patch = collisionByIndex[triangleIndex];
        Assert(patch.ByteLength == 12, $"{fixture.Name}: triangle {triangleIndex} patch is not 12 bytes.");
        byte[] before = ParseHexBytes(patch.BeforeHexPreview);
        byte[] after = ParseHexBytes(patch.AfterHexPreview);
        Assert(before.Length == 12 && after.Length == 12, $"{fixture.Name}: triangle {triangleIndex} preview length changed.");
        Assert(
            ReadWadBytes(sourceImagePath, ParseOffset(patch.WadRelativeOffset), 12).SequenceEqual(before),
            $"{fixture.Name}: triangle {triangleIndex} before bytes are not source-bound to retail.");
        fanBefore.AddRange(before);
        fanAfter.AddRange(after);

        DecodedTriangle decodedBefore = DecodeTriangle(before);
        DecodedTriangle decodedAfter = DecodeTriangle(after);
        Point3[] expectedPoints = decodedBefore.Points
            .Select(point => targetByOriginal.GetValueOrDefault(point, point))
            .ToArray();
        Assert(
            IsCyclicRotation(expectedPoints, decodedAfter.Points),
            $"{fixture.Name}: triangle {triangleIndex} was not rewritten as the exact cyclic target.");
        Assert(
            decodedBefore.ZFlags == decodedAfter.ZFlags && decodedAfter.ZFlags == 0,
            $"{fixture.Name}: triangle {triangleIndex} changed native Z flags.");

        if (fixture.ProtectedCollisionPoint is Point3 protectedPoint)
        {
            protectedBefore += decodedBefore.Points.Count(point => point == protectedPoint);
            protectedAfter += decodedAfter.Points.Count(point => point == protectedPoint);
        }
    }

    Assert(
        Sha256Bytes(fanBefore).Equals(fixture.SourceFanSha256, StringComparison.OrdinalIgnoreCase),
        $"{fixture.Name}: concatenated source fan SHA-256 changed.");
    Assert(
        Sha256Bytes(fanAfter).Equals(fixture.PatchedFanSha256, StringComparison.OrdinalIgnoreCase),
        $"{fixture.Name}: concatenated patched fan SHA-256 changed.");
    if (fixture.ProtectedCollisionPoint != null)
    {
        Assert(protectedBefore > 0, $"{fixture.Name}: protected lower point was absent from the source fan.");
        Assert(protectedAfter == protectedBefore, $"{fixture.Name}: protected lower same-XY point was changed or removed.");
    }

    NativeTerrainOcclusionData occlusion = geometry.NativeTerrainOcclusion
        ?? throw new InvalidOperationException($"{fixture.Name}: native collision assignment inventory is missing.");
    Dictionary<int, int> assignments = occlusion.CollisionTriangles.ToDictionary(triangle => triangle.Index, triangle => triangle.GroupIndex);
    for (int i = 0; i < fixture.FanIndexes.Length; i++)
    {
        int triangleIndex = fixture.FanIndexes[i];
        Assert(
            assignments.TryGetValue(triangleIndex, out int assignment) && assignment == fixture.ExpectedAssignments[i],
            $"{fixture.Name}: triangle {triangleIndex} assignment changed; expected {fixture.ExpectedAssignments[i]}.");
    }

    foreach ((int triangleIndex, long[] lookupOffsets) in fixture.LookupReferences)
    {
        foreach (long lookupOffset in lookupOffsets)
        {
            ushort lookupWord = BinaryPrimitives.ReadUInt16LittleEndian(ReadWadBytes(sourceImagePath, lookupOffset, 2));
            Assert(
                (lookupWord & 0x7FFF) == triangleIndex,
                $"{fixture.Name}: lookup 0x{lookupOffset:X} points to triangle {lookupWord & 0x7FFF}, expected {triangleIndex}.");
            Assert(
                !plan.Patches.Any(patch => PatchTouchesOffset(patch, lookupOffset)),
                $"{fixture.Name}: stable edit changed native lookup membership at 0x{lookupOffset:X}.");
        }
    }

    Console.WriteLine(
        $"PASS stable: {fixture.Name}; visual={visualPatches.Length}, fan={collisionPatches.Length}, " +
        $"source={fixture.SourceFanSha256[..12]}, after={fixture.PatchedFanSha256[..12]}.");
}

async Task VerifyCellCrossingRejectAsync()
{
    await ExpectRejectedAsync(
        "Artisans +8 collision-cell crossing",
        "artisans",
        geometry =>
        {
            TerrainPolygon face = FindFace(geometry, 2, 64);
            face.ApplyTerrainVertexDeltas([0, 0, 0, 8]);
            return [face];
        },
        ["exact native lookup cells", "Collision-cell-changing edits"]);
}

async Task VerifyPhysicalTargetConflictRejectAsync()
{
    await ExpectRejectedAsync(
        "Town Square shared physical target conflict",
        "townsquare",
        geometry =>
        {
            TerrainPolygon face37 = FindFace(geometry, 213, 37);
            TerrainPolygon face25 = FindFace(geometry, 213, 25);
            face37.ApplyTerrainVertexDeltas([0, 0, 96, 96]);
            face25.ApplyTerrainVertexDeltas([0, 0, 104, 0]);
            return [face37, face25];
        },
        ["different packed targets"]);
}

async Task VerifyAlpineAliasRejectAsync()
{
    await ExpectRejectedAsync(
        "Alpine Ridge incomplete coordinate-alias closure",
        "alpineridge",
        geometry =>
        {
            TerrainPolygon face = FindFace(geometry, 111, 47);
            VerifyFacePreimage(
                face,
                [19, 19, 20, 52],
                [new(5776, 6920, 1497), new(5676, 6962, 1497), new(5709, 6904, 1497)],
                "Alpine Ridge alias fixture");
            face.ApplyTerrainVertexDeltas([0, 0, 32]);
            return [face];
        },
        ["independent HP vertex slots", "Coordinate-alias collision ownership is ambiguous"]);
}

async Task VerifyXyRejectAsync()
{
    await ExpectRejectedAsync(
        "Dry Canyon XY move",
        "drycanyon",
        geometry =>
        {
            TerrainPolygon face = FindFace(geometry, 40, 94);
            Vector2f[] points = face.OriginalPoints.ToArray();
            points[0] = new Vector2f(9100, 8150);
            face.ApplyTerrainPointPositions(points);
            return [face];
        },
        ["Z-only movement", "XY edits remain research-only"]);
}

async Task VerifyLowDetailRejectAsync()
{
    LevelContext context = contexts["townsquare"];
    GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(context.OverlayPath);
    LowDetailTerrainPolygon source = geometry.LowDetailPolygons.Single(face => face.SectorIndex == 0 && face.FaceIndex == 0);
    Assert(
        source.VertexIndexes.SequenceEqual([0, 1, 3, 2]) &&
        source.Points.Select((point, index) => new Point3((int)point.X, (int)point.Y, (int)source.ZValues[index]))
            .SequenceEqual(
            [
                new Point3(6617, 6661, 608),
                new Point3(6359, 6665, 608),
                new Point3(6260, 7045, 608),
                new Point3(6128, 6988, 608)
            ]),
        "Town Square LP 0:0 preimage changed.");

    TerrainPolygon lp = new(
        source.Points,
        source.ZValues,
        textureId: -1,
        source.SectorIndex,
        source.FaceIndex,
        detail: "lp",
        ColorRgba.FromRgb(128, 128, 128),
        source.SectorOffset,
        source.FaceOffset,
        source.VertexIndexes,
        cornerPointIndexes: source.CornerPointIndexes);
    lp.ApplyTerrainDeltaZ(1);

    string extendedSourceSearchPath = TempPath("townsquare-lp-source-search.json");
    TerrainSourceSearchEntry sectorEntry = context.SourceSearchReport.Results.First(entry =>
        ParseOffset(entry.SectorOffset) == source.SectorOffset);
    TerrainSourceSearchReport extended = context.SourceSearchReport with
    {
        Results = context.SourceSearchReport.Results
            .Append(sectorEntry with { Edit = lp.RuntimeKey })
            .ToArray()
    };
    await File.WriteAllTextAsync(
        extendedSourceSearchPath,
        JsonSerializer.Serialize(extended, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));

    await ExpectRejectedPreparedAsync(
        "Town Square low-detail geometry edit",
        context,
        [lp],
        ["high-detail (HP) Z edits only", "LP edits remain research-only"],
        extendedSourceSearchPath);
}

async Task VerifyUnavailableCollisionContextRejectAsync()
{
    string dummyRamPath = TempPath("incomplete-collision-context.ram");
    await File.WriteAllBytesAsync(dummyRamPath, new byte[32]);
    await ExpectRejectedAsync(
        "Artisans unavailable collision context",
        "artisans",
        geometry =>
        {
            TerrainPolygon face = FindFace(geometry, 2, 64);
            face.ApplyTerrainVertexDeltas([0, 0, 0, 7]);
            return [face];
        },
        ["complete native collision table and lookup context", "Visual-only geometry export is blocked"],
        ramPath: dummyRamPath);
}

async Task VerifyUnmatchedFanRejectAsync()
{
    await ExpectRejectedAsync(
        "Artisans unmatched native fan",
        "artisans",
        geometry =>
        {
            TerrainPolygon face = FindFace(geometry, 6, 1);
            VerifyFacePreimage(
                face,
                [0, 1, 5, 4],
                [new(3633, 4464, 751), new(3721, 4559, 731), new(3641, 4594, 836), new(3543, 4439, 847)],
                "Artisans unmatched fixture");
            face.ApplyTerrainVertexDeltas([0, 0, 1, 0]);
            return [face];
        },
        ["has no referenced native collision fan", "Visual-only geometry export is blocked"]);
}

async Task VerifyVerticalShadowRejectAsync()
{
    await ExpectRejectedAsync(
        "Town Square alias-closed hidden-under-upper-surface control",
        "townsquare",
        geometry =>
        {
            TerrainPolygon lower = FindFace(geometry, 213, 64);
            TerrainPolygon lowerAliasOwner = FindFace(geometry, 4, 10);
            TerrainPolygon upper = FindFace(geometry, 4, 6);
            VerifyFacePreimage(
                lower,
                [93, 93, 95, 94],
                [new(8056, 6317, 512), new(8167, 6325, 512), new(8123, 6410, 512)],
                "Town Square lower overlap fixture");
            VerifyFacePreimage(
                upper,
                [17, 16, 15, 14],
                [new(8056, 6317, 560), new(8129, 6259, 560), new(8167, 6325, 560), new(8123, 6410, 560)],
                "Town Square upper overlap fixture");
            VerifyFacePreimage(
                lowerAliasOwner,
                [22, 23, 17, 14],
                [new(8123, 6410, 512), new(8056, 6317, 512), new(8056, 6317, 560), new(8123, 6410, 560)],
                "Town Square lower alias owner fixture");
            lower.ApplyTerrainVertexDeltas([0, 0, 16]);
            lowerAliasOwner.ApplyTerrainVertexDeltas([16, 0, 0, 0]);
            return [lower, lowerAliasOwner];
        },
        ["vertically shadowed", "Close vertically overlapping terrain remains blocked"]);
}

async Task VerifyHistoricalHiddenControlAliasRejectAsync()
{
    await ExpectRejectedAsync(
        "Town Square historical one-face hidden control",
        "townsquare",
        geometry =>
        {
            TerrainPolygon lower = FindFace(geometry, 213, 64);
            VerifyFacePreimage(
                lower,
                [93, 93, 95, 94],
                [new(8056, 6317, 512), new(8167, 6325, 512), new(8123, 6410, 512)],
                "Town Square historical one-face fixture");
            lower.ApplyTerrainVertexDeltas([0, 0, 16]);
            return [lower];
        },
        ["independent HP vertex slots", "Coordinate-alias collision ownership is ambiguous"]);
}

async Task ExpectRejectedAsync(
    string name,
    string levelKey,
    Func<GeometryCandidate, IReadOnlyList<TerrainPolygon>> editFactory,
    IReadOnlyList<string> expectedMessageParts,
    string ramPath = "")
{
    LevelContext context = contexts[levelKey];
    GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(context.OverlayPath);
    IReadOnlyList<TerrainPolygon> edits = editFactory(geometry);
    await ExpectRejectedPreparedAsync(name, context, edits, expectedMessageParts, context.SourceSearchPath, ramPath);
}

async Task ExpectRejectedPreparedAsync(
    string name,
    LevelContext context,
    IReadOnlyList<TerrainPolygon> edits,
    IReadOnlyList<string> expectedMessageParts,
    string sourceSearchPath,
    string ramPath = "")
{
    string slug = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
    string editsPath = TempPath($"reject-{slug}-edits.json");
    string outputImagePath = TempPath($"reject-{slug}.bin");
    string outputCuePath = TempPath($"reject-{slug}.cue");
    int editCount = await TerrainEditStore.SaveAsync(editsPath, edits, name);
    Assert(editCount == edits.Count, $"{name}: normal edit store saved {editCount}/{edits.Count} edits.");

    string message;
    try
    {
        TerrainPatchPlan plan = TerrainPatchExporter.BuildPlan(
            sourceImagePath,
            sourceCuePath,
            outputImagePath,
            outputCuePath,
            context.Level,
            ramPath,
            sourceSearchPath,
            editsPath);
        throw new InvalidOperationException(
            $"{name}: exporter returned a partial plan instead of rejecting atomically " +
            $"({plan.PatchCount} patch(es); {string.Join(" | ", plan.SkippedEdits)}).");
    }
    catch (InvalidOperationException ex) when (!ex.Message.StartsWith(name + ": exporter returned", StringComparison.Ordinal))
    {
        message = ex.Message;
    }

    foreach (string expected in expectedMessageParts)
    {
        Assert(
            message.Contains(expected, StringComparison.OrdinalIgnoreCase),
            $"{name}: rejection omitted '{expected}'. Actual: {message}");
    }
    Assert(!File.Exists(outputImagePath), $"{name}: rejection left a BIN artifact.");
    Assert(!File.Exists(outputCuePath), $"{name}: rejection left a CUE artifact.");
    Console.WriteLine($"PASS reject: {name}; {message}");
}

static TerrainPolygon FindFace(GeometryCandidate geometry, int sectorIndex, int faceIndex) =>
    geometry.Polygons.Single(face =>
        face.SectorIndex == sectorIndex &&
        face.FaceIndex == faceIndex &&
        face.Detail.Equals("hp", StringComparison.OrdinalIgnoreCase));

static void VerifyFacePreimage(
    TerrainPolygon face,
    IReadOnlyList<int> expectedVertexIndexes,
    IReadOnlyList<Point3> expectedPoints,
    string name)
{
    Assert(face.VertexIndexes.SequenceEqual(expectedVertexIndexes), $"{name}: native HP vertex slots changed.");
    Point3[] actual = face.OriginalPoints
        .Select((point, index) => new Point3(
            (int)Math.Round(point.X),
            (int)Math.Round(point.Y),
            (int)Math.Round(face.OriginalZValues[index])))
        .ToArray();
    Assert(actual.SequenceEqual(expectedPoints), $"{name}: decoded face coordinate preimage changed.");
}

static void VerifyOneSourceHit(TerrainSourceSearchReport report, string runtimeKey, string name)
{
    TerrainSourceSearchEntry entry = report.Results.Single(result =>
        result.Edit.Equals(runtimeKey, StringComparison.OrdinalIgnoreCase));
    Assert(entry.FullSectorHits.Count == 1, $"{name}: face did not bind to exactly one retail source sector.");
}

static Dictionary<int, long[]> LookupReferences(params (int TriangleIndex, long[] Offsets)[] entries) =>
    entries.ToDictionary(entry => entry.TriangleIndex, entry => entry.Offsets);

static int ParseTriangleIndex(TerrainPatch patch)
{
    Match match = Regex.Match(patch.Description, @"triangle\s+(\d+)", RegexOptions.IgnoreCase);
    Assert(match.Success, $"Collision fan patch description omitted its triangle index: {patch.Description}");
    return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
}

static bool PatchTouchesOffset(TerrainPatch patch, long wadOffset)
{
    long start = ParseOffset(patch.WadRelativeOffset);
    return wadOffset >= start && wadOffset < start + patch.ByteLength;
}

static long ParseOffset(string text)
{
    string value = (text ?? "").Trim();
    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        return long.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    return long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
}

static byte[] ParseHexBytes(string text)
{
    string value = Regex.Replace(text ?? "", "[^0-9A-Fa-f]", "");
    Assert(value.Length % 2 == 0, $"Odd hex preview length: {text}");
    return Convert.FromHexString(value);
}

static DecodedTriangle DecodeTriangle(ReadOnlySpan<byte> bytes)
{
    uint xWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes[..4]);
    uint yWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
    uint zWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
    int x1 = (int)(xWord & 0x3FFF);
    int y1 = (int)(yWord & 0x3FFF);
    int z1 = (int)(zWord & 0x3FFF);
    return new DecodedTriangle(
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
        ],
        zWord & 0xC000u);
}

static int Signed9(int value) => (value & 0x100) != 0 ? value - 0x200 : value;

static bool IsCyclicRotation(IReadOnlyList<Point3> expected, IReadOnlyList<Point3> actual)
{
    if (expected.Count != 3 || actual.Count != 3)
        return false;
    return Enumerable.Range(0, 3).Any(rotation =>
        Enumerable.Range(0, 3).All(index => expected[(index + rotation) % 3] == actual[index]));
}

static byte[] ReadWadBytes(string imagePath, long wadOffset, int length)
{
    (int sectorSize, int userOffset) = DetectDiscLayout(imagePath);
    const int WadLba = 37;
    byte[] result = new byte[length];
    using FileStream stream = File.OpenRead(imagePath);
    int written = 0;
    long logicalOffset = wadOffset;
    while (written < length)
    {
        int sectorOffset = (int)(logicalOffset % 2048);
        int sector = WadLba + checked((int)(logicalOffset / 2048));
        int count = Math.Min(2048 - sectorOffset, length - written);
        stream.Position = ((long)sector * sectorSize) + userOffset + sectorOffset;
        int read = stream.Read(result, written, count);
        if (read != count)
            throw new EndOfStreamException($"Could not read WAD offset 0x{wadOffset:X}.");
        written += read;
        logicalOffset += read;
    }
    return result;
}

static (int SectorSize, int UserOffset) DetectDiscLayout(string imagePath)
{
    using FileStream stream = File.OpenRead(imagePath);
    byte[] signature = new byte[7];
    foreach ((int sectorSize, int userOffset) in new[] { (2048, 0), (2352, 24), (2336, 8) })
    {
        long pvdOffset = (16L * sectorSize) + userOffset;
        if (pvdOffset + 7 > stream.Length)
            continue;
        stream.Position = pvdOffset;
        Array.Clear(signature);
        if (stream.Read(signature) == signature.Length &&
            signature[0] == 1 &&
            signature.AsSpan(1, 5).SequenceEqual("CD001"u8) &&
            signature[6] == 1)
        {
            return (sectorSize, userOffset);
        }
    }
    throw new InvalidOperationException("Could not detect the retail BIN sector layout.");
}

static string Sha256File(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static string Sha256Bytes(IEnumerable<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes.ToArray())).ToLowerInvariant();

string TempPath(string name) => Path.Combine(tempRoot, name);

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed record LevelContext(
    LevelDefinition Level,
    string OverlayPath,
    GeometryCandidate Geometry,
    string SourceSearchPath,
    TerrainSourceSearchReport SourceSearchReport);

sealed record StableFixture(
    string Name,
    string LevelKey,
    int SectorIndex,
    int FaceIndex,
    int[] VertexIndexes,
    Point3[] OriginalPoints,
    float[] VertexDeltaZ,
    string[] ExpectedAffectedFaces,
    int[] FanIndexes,
    int[] ExpectedAssignments,
    Dictionary<int, long[]> LookupReferences,
    string SourceFanSha256,
    string PatchedFanSha256,
    Point3? ProtectedCollisionPoint = null);

sealed record Point3(int X, int Y, int Z);

sealed record DecodedTriangle(Point3[] Points, uint ZFlags);

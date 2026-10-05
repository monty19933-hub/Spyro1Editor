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
    if(Environment.GetEnvironmentVariable("SPYRO_TERRAIN_V1_PLAIN_REMOVAL_ONLY")=="1")
    {
        contexts["artisans"]=await BuildLevelContextAsync("artisans");
        await VerifyPlainArtisansRemovalAsync();
        return;
    }
    foreach (string levelKey in new[] { "townsquare", "darkhollow", "drycanyon", "artisans", "alpineridge" })
        contexts[levelKey] = await BuildLevelContextAsync(levelKey);

    foreach (StableFixture fixture in stableFixtures)
        await VerifyStableFixtureAsync(fixture);

    await VerifyExtendedFixtureAsync("Artisans +8 collision-cell crossing", "artisans", 2, 64, [0, 0, 0, 8], null, true);
    await VerifyReachableArtisansFloorAsync();
    await VerifyCoupledTopologyAsync();
    await VerifyPhysicalTargetConflictRejectAsync();
    await VerifyAlpineAliasRejectAsync();
    await VerifyExtendedFixtureAsync("Dry Canyon XYZ point movement", "drycanyon", 40, 94, [16, 0, 0, 0], new Vector2f(9028, 8047), false);
    await VerifyLowDetailRejectAsync();
    await VerifyUnavailableCollisionContextRejectAsync();
    await VerifyUnmatchedFanRejectAsync();
    await VerifyHistoricalHiddenControlAliasRejectAsync();
    await VerifyVerticalShadowRejectAsync();

    Assert(
        Sha256File(sourceImagePath).Equals(RetailBinSha256, StringComparison.OrdinalIgnoreCase),
        "The retail source BIN changed while the smoke was running.");
    Console.WriteLine("PASS TerrainCollisionFanSmoke: 4 exact fan stages, 2 extended XYZ/index stages, 2 coupled add/remove transactions and 7 atomic refusal cases passed.");
}
finally
{
    if (Environment.GetEnvironmentVariable("SPYRO_TERRAIN_V1_KEEP_FIXTURES")=="1")
        Console.WriteLine("TERRAIN_V1_RETAINED_FIXTURES "+tempRoot);
    else if (Directory.Exists(tempRoot) &&
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
        editsPath, deferCoupledTopology:true);

    {
        var finalPlan=TerrainPatchExporter.BuildPlan(sourceImagePath,sourceCuePath,plan.OutputImagePath,plan.OutputCuePath,
            context.Level,"",context.SourceSearchPath,editsPath);
        Assert(finalPlan.SkippedEdits.Count==0,$"{fixture.Name}: final coupled geometry transaction was not complete.");
        if(fixture.LevelKey=="townsquare")
        {
            string failedBin=TempPath("paired-publication.bin"),failedCue=TempPath("paired-publication.cue");
            byte[] previous=[1,7,9,4];File.WriteAllBytes(failedBin,previous);Directory.CreateDirectory(failedCue);
            bool refused=false;
            try { await TerrainPatchExporter.ExportPreparedPlanAsync(finalPlan with{OutputImagePath=failedBin,OutputCuePath=failedCue}); }
            catch(InvalidDataException) { refused=true; }
            Assert(refused&&File.ReadAllBytes(failedBin).SequenceEqual(previous)&&Directory.Exists(failedCue),"A failed CUE publication must preserve the previous BIN and blocked CUE destination");
            Assert(!File.Exists(Path.ChangeExtension(failedBin,"terrain-patch-plan.json")),"Failed paired publication left a new final report");
            Console.WriteLine("TERRAIN_PREPARED_PUBLICATION_FAILURE_PRESERVED_OK");
        }
        if(finalPlan.NativeLevelReplacement!=null)
        {
            var replacement=finalPlan.NativeLevelReplacement;
            Assert(replacement.After.Length>replacement.Before.Length&&(replacement.After.Length-replacement.Before.Length)%0x800==0,"Growth must use whole native disc sectors");
            AssertNativeEnvironmentQueueAdmission(replacement.Before,replacement.After);
            Assert(replacement.FinalRuntimeEnd<=Spyro.Editor.Core.Universal.UniversalMobyComposer.NormalBufferFloor-Spyro.Editor.Core.Universal.UniversalMobyComposer.RamSafetyMargin,"Growth escapes native RAM owner");
            string? keep=Environment.GetEnvironmentVariable("SPYRO_TERRAIN_V1_KEEP_OUTPUT");
            if(!string.IsNullOrWhiteSpace(keep))
            {
                Directory.CreateDirectory(keep);
                var materialized=await TerrainPatchExporter.ExportPreparedPlanAsync(finalPlan with {OutputImagePath=Path.Combine(keep,fixture.LevelKey+"-lp-growth.bin"),OutputCuePath=Path.Combine(keep,fixture.LevelKey+"-lp-growth.cue")});
                Assert(materialized.WroteImage,"Growing terrain body was not materialized");
                Console.WriteLine($"TERRAIN_V1_GROWTH_CANDIDATE {materialized.OutputCuePath}; RAMend0x{replacement.FinalRuntimeEnd:X}");
            }
        }
    }

    Assert(plan.SkippedEdits.Count == 0, $"{fixture.Name}: unexpected exporter skip: {string.Join(" | ", plan.SkippedEdits)}");
    Assert(
        plan.Patches.All(patch =>
            patch.Kind.Equals("visual-hp-topology-safe", StringComparison.Ordinal) ||
            patch.Kind.Equals("visual-lp-topology-safe", StringComparison.Ordinal) ||
            patch.Kind.Equals("terrain-sector-bounds", StringComparison.Ordinal) ||
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
        plan.PatchCount == visualPatches.Length + fixture.FanIndexes.Length + plan.Patches.Count(patch => patch.Kind is "visual-lp-topology-safe" or "terrain-sector-bounds"),
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

async Task VerifyCoupledTopologyAsync()
{
    LevelContext context=contexts["artisans"];
    foreach(bool remove in new[]{false,true})
    {
        var geometry=GeometryOverlayLoader.LoadFirstCandidate(context.OverlayPath);
        var face=FindFace(geometry,90,0);
        if(remove) face.StageTerrainRemoval();
        else { face.ApplyTerrainVertexXYDeltas(face.OriginalPoints.Select(_=>new Vector2f(160,0)).ToArray()); face.StageTerrainAddClone(); }
        string edits=TempPath(remove?"coupled-remove.json":"coupled-add.json");
        await TerrainEditStore.SaveAsync(edits,[face],remove?"Coupled native remove":"Coupled native copy");
        var plan=TerrainPatchExporter.BuildPlan(sourceImagePath,sourceCuePath,TempPath("topology.bin"),TempPath("topology.cue"),context.Level,"",context.SourceSearchPath,edits);
        Assert(plan.SkippedEdits.Count==0 && plan.Patches.Any(p=>p.Kind=="terrain-coupled-topology"),"Coupled topology did not own the complete terrain transaction.");
        string? root=Environment.GetEnvironmentVariable("SPYRO_TERRAIN_V1_KEEP_OUTPUT");
        if(!string.IsNullOrWhiteSpace(root))
        {
            string prefix=Path.Combine(root,remove?"artisans-floor-remove":"artisans-floor-copy");
            var result=await TerrainPatchExporter.ExportAsync(new TerrainPatchRequest(sourceImagePath,sourceCuePath,prefix,context.Level,"",context.SourceSearchPath,edits,"",true));
            Assert(result.WroteImage,"Coupled topology did not write native output");
            foreach(var p in result.Plan.Patches)
            {
                byte[] expected=ParseHexBytes(p.AfterHexPreview);
                Assert(ReadWadBytes(result.OutputImagePath,ParseOffset(p.WadRelativeOffset),expected.Length).SequenceEqual(expected),"Coupled topology readback mismatch");
            }
            File.Copy(edits,prefix+"-edits.json",true);
            Console.WriteLine($"TERRAIN_V1_TOPOLOGY_CANDIDATE remove={remove} cue={result.OutputCuePath} source90:0 X5248..5376/Y3200..3328/Z566 copied X+160");
        }
        Console.WriteLine($"PASS coupled topology: remove={remove}; patches={plan.PatchCount}");
    }
}

async Task VerifyPlainArtisansRemovalAsync()
{
    var context=contexts["artisans"];
    var geometry=GeometryOverlayLoader.LoadFirstCandidate(context.OverlayPath);
    var face=FindFace(geometry,58,43);
    face.StageTerrainRemoval();
    string edits=TempPath("plain-removal-edits.json");
    await TerrainEditStore.SaveAsync(edits,[face],"Plain approach-ramp removal away from Nestor platform");
    string root=Environment.GetEnvironmentVariable("SPYRO_TERRAIN_V1_KEEP_OUTPUT")??throw new InvalidOperationException("Plain removal evidence needs an isolated output directory.");
    Directory.CreateDirectory(root);
    string prefix=Path.Combine(root,"artisans-plain-ramp-remove");
    var result=await TerrainPatchExporter.ExportAsync(new TerrainPatchRequest(sourceImagePath,sourceCuePath,prefix,context.Level,"",context.SourceSearchPath,edits,"",true));
    Assert(result.WroteImage&&result.Plan.SkippedEdits.Count==0,"Plain terrain removal was not emitted by the normal native exporter");
    var replacement=result.Plan.NativeLevelReplacement??throw new InvalidOperationException("Plain removal must exercise the growing native body.");
    AssertNativeEnvironmentQueueAdmission(replacement.Before,replacement.After);
    Console.WriteLine("TERRAIN_LP_ONLY_NATIVE_QUEUE_ADMISSION_OK");
    File.Copy(edits,prefix+"-edits.json",true);
    Console.WriteLine($"TERRAIN_PLAIN_REMOVAL_CANDIDATE {result.OutputCuePath}; source58:43; probe5312,2800; native Mobys unchanged");
}

void AssertNativeEnvironmentQueueAdmission(byte[] before,byte[] after)
{
    static byte[] EnvironmentComponent(byte[] body)
    {
        int model=BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(8));
        int textures=BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(model));
        int at=model+textures,n=BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(at));
        return body.AsSpan(at,n).ToArray();
    }
    static bool SafeHeader(ReadOnlySpan<byte> header,bool appended)
    {
        if(header[20]!=0)return true;
        ushort flags=BinaryPrimitives.ReadUInt16LittleEndian(header[4..]);
        // Native selector skips HP iff0x4000 is set. Generated overflow has no
        // HP owner at any distance, so0x8000 must also force its LP queue.
        return(flags&0x4000)!=0&&(!appended||(flags&0x8000)!=0)&&(flags&0x2000)==0;
    }
    byte[] oldEnvironment=EnvironmentComponent(before),environment=EnvironmentComponent(after);
    int originalCount=BinaryPrimitives.ReadInt32LittleEndian(oldEnvironment.AsSpan(4));
    int count=BinaryPrimitives.ReadInt32LittleEndian(environment.AsSpan(4));
    int appendedLpOnly=0;
    for(int i=0;i<count;i++)
    {
        int at=4+BinaryPrimitives.ReadInt32LittleEndian(environment.AsSpan(8+4*i));
        var header=environment.AsSpan(at,28);
        Assert(SafeHeader(header,i>=originalCount),$"Sector{i} admits an empty HP vertex table to the native renderer or suppresses its LP owner");
        if(i<originalCount||header[20]!=0)continue;
        appendedLpOnly++;
        byte[] malformed=header.ToArray();malformed[5]&=0xBF;
        Assert(!SafeHeader(malformed,true),"Native queue guard must reject the original missing-HP-disable crash header");
        malformed=header.ToArray();malformed[5]&=0x7F;
        Assert(!SafeHeader(malformed,true),"Generated LP overflow must stay visible within the close HP range");
    }
    Assert(appendedLpOnly>0,"Growing native fixture must exercise at least one LP-only overflow queue");
}

async Task VerifyReachableArtisansFloorAsync()
{
    string? root = Environment.GetEnvironmentVariable("SPYRO_TERRAIN_V1_KEEP_OUTPUT");
    if (string.IsNullOrWhiteSpace(root)) return;
    Directory.CreateDirectory(root);
    LevelContext context = contexts["artisans"];
    GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(context.OverlayPath);
    TerrainPolygon face = FindFace(geometry, 90, 0);
    HashSet<(float X,float Y,float Z)> targets = face.OriginalPoints.Select((p,i)=>(p.X,p.Y,face.OriginalZValues[i])).ToHashSet();
    List<TerrainPolygon> edited = [];
    foreach (TerrainPolygon candidate in geometry.Polygons)
    {
        float[] z = candidate.OriginalPoints.Select((point,i)=>targets.Contains((point.X,point.Y,candidate.OriginalZValues[i]))?8f:0f).ToArray();
        if (z.Any(value=>value!=0)) { candidate.ApplyTerrainVertexDeltas(z); edited.Add(candidate); }
    }
    string path=Path.Combine(root,"artisans-reachable-floor-edits.json");
    await TerrainEditStore.SaveAsync(path,edited,"V1 reachable Artisans floor: shared-corner closure +8");
    TerrainPatchResult result=await TerrainPatchExporter.ExportAsync(new TerrainPatchRequest(sourceImagePath,sourceCuePath,
        Path.Combine(root,"artisans-reachable-floor"),context.Level,"",context.SourceSearchPath,path,"",true));
    Assert(result.WroteImage&&result.Plan.SkippedEdits.Count==0,"Reachable floor did not produce a complete native candidate.");
    foreach(var patch in result.Plan.Patches)
    {
        byte[] after=ParseHexBytes(patch.AfterHexPreview);
        Assert(ReadWadBytes(result.OutputImagePath,ParseOffset(patch.WadRelativeOffset),after.Length).SequenceEqual(after),"Reachable floor readback mismatch.");
    }
    Console.WriteLine($"TERRAIN_V1_REACHABLE_CANDIDATE cue={result.OutputCuePath}; source face90:0 native corners X5248..5376/Y3200..3328/Z566→574; {edited.Count} alias-closed edited faces");
}

async Task VerifyExtendedFixtureAsync(string name, string levelKey, int sector, int faceIndex,
    float[] z, Vector2f? firstPoint, bool requiresIndex)
{
    LevelContext context = contexts[levelKey];
    GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(context.OverlayPath);
    TerrainPolygon face = FindFace(geometry, sector, faceIndex);
    face.ApplyTerrainVertexDeltas(z);
    if (firstPoint != null)
    {
        Vector2f[] xy = face.OriginalPoints.ToArray(); xy[0] = firstPoint.Value;
        face.ApplyTerrainPointPositions(xy);
    }
    string edits = TempPath($"extended-{levelKey}.json");
    await TerrainEditStore.SaveAsync(edits, [face], name);
    TerrainPatchPlan plan = TerrainPatchExporter.BuildPlan(sourceImagePath, sourceCuePath,
        TempPath($"extended-{levelKey}.bin"), TempPath($"extended-{levelKey}.cue"), context.Level,
        "", context.SourceSearchPath, edits, deferCoupledTopology:requiresIndex);
    if(requiresIndex)
    {
        var complete=TerrainPatchExporter.BuildPlan(sourceImagePath,sourceCuePath,plan.OutputImagePath,plan.OutputCuePath,context.Level,"",context.SourceSearchPath,edits);
        Assert(complete.SkippedEdits.Count==0,"Complete native lookup+LP transaction failed");
    }
    Assert(plan.SkippedEdits.Count == 0, name + ": skipped a requested edit");
    Assert(plan.Patches.Any(p => p.Kind == "visual-hp-topology-safe") && plan.Patches.Any(p => p.Kind == "collision-triangle-fan"), name + ": missing coupled visual/collision patches");
    Assert(plan.Patches.Any(p => p.Kind.StartsWith("collision-index-", StringComparison.Ordinal)) == requiresIndex, name + ": unexpected lookup index ownership");
    foreach (TerrainPatch patch in plan.Patches)
    {
        byte[] before = ParseHexBytes(patch.BeforeHexPreview);
        Assert(ReadWadBytes(sourceImagePath, ParseOffset(patch.WadRelativeOffset), before.Length).SequenceEqual(before), name + ": exact original preimage mismatch");
        if (patch.Kind != "collision-triangle-fan") continue;
        DecodedTriangle source = DecodeTriangle(before), target = DecodeTriangle(ParseHexBytes(patch.AfterHexPreview));
        Assert(source.ZFlags == target.ZFlags, name + ": native collision flags changed");
        Assert(target.Points.Distinct().Count() == 3, name + ": collision triangle became degenerate");
    }
    string? keepRoot = Environment.GetEnvironmentVariable("SPYRO_TERRAIN_V1_KEEP_OUTPUT");
    if (!string.IsNullOrWhiteSpace(keepRoot) && levelKey == "artisans")
    {
        Directory.CreateDirectory(keepRoot);
        string prefix = Path.Combine(keepRoot, "artisans-cell-crossing");
        TerrainPatchResult result = await TerrainPatchExporter.ExportAsync(new TerrainPatchRequest(
            sourceImagePath, sourceCuePath, prefix, context.Level, "", context.SourceSearchPath, edits, "", true));
        Assert(result.WroteImage, name + ": normal exporter did not write candidate");
        if(result.Plan.NativeLevelReplacement is { } replacement)
            Assert(Spyro.Editor.Core.Universal.UniversalDiscImage.Read(result.OutputImagePath).EntryBytes(context.Level.SourceWadEntry).SequenceEqual(replacement.After),"Complete growing lookup+LP body readback mismatch");
        else foreach (TerrainPatch patch in result.Plan.Patches)
        {
            byte[] expected = ParseHexBytes(patch.AfterHexPreview);
            Assert(ReadWadBytes(result.OutputImagePath, ParseOffset(patch.WadRelativeOffset), expected.Length).SequenceEqual(expected),
                name + ": candidate patch readback differs");
        }
        File.Copy(edits, Path.Combine(keepRoot, "artisans-edits.json"), true);
        Console.WriteLine($"TERRAIN_V1_CANDIDATE cue={result.OutputCuePath}; BIN/readback=verified; face=2:64:hp; point4 Z+8");
    }
    Console.WriteLine($"PASS exact native fan/index stage: {name}; patches={plan.PatchCount}, lookupRebuilt={requiresIndex}.");
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
        // Every native copy of the shared point now moves together; the shared face then
        // rises into the close surface above it, which stays blocked.
        ["vertically shadowed", "Close vertically overlapping terrain remains blocked"]);
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
        ["high-detail (HP) source faces", "direct LP edits are not supported"],
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
        // The shared point's native copies now move together; the close surface above still blocks it.
        ["vertically shadowed", "Close vertically overlapping terrain remains blocked"]);
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

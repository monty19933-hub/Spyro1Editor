using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.SurfaceLayoutSmoke;

string workspaceRoot = ResolveWorkspaceRoot(args);
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
if (!File.Exists(sourceImagePath))
    throw new FileNotFoundException("The native surface-layout smoke needs Spyro the Dragon (USA).bin in the workspace.", sourceImagePath);

RunReuseSyntheticSmoke();
RunAppendPromotionCompositionSyntheticSmoke();
RunNegativeSyntheticGuards();
RetailMatrixResult matrix = RunAllLevelRetailWaterMatrix(workspaceRoot, sourceImagePath);
WriteReport(workspaceRoot, sourceImagePath, matrix);

Console.WriteLine(
    $"Native terrain surface layout smoke passed: synthetic append/reuse/guards and {matrix.Levels.Count}/35 retail levels ({matrix.ReusedCount} reuse, {matrix.AppendedCount} append)." );

static void RunReuseSyntheticSmoke()
{
    NativeTerrainSurfaceSignature water = new(0, 0, 0);
    byte[] waterRaw = BuildRecord(0, 0, 0, 12);
    SyntheticSurfaceLevelDataFixture fixture = SyntheticSurfaceLevelDataFixture.Build([waterRaw]);
    NativeTerrainSurfaceSourceData source = ParseFixture(fixture, "synthetic-reuse");

    bool ok = NativeTerrainSurfaceLayoutComposer.TryBuild(
        fixture.Bytes,
        source,
        [new NativeTerrainSurfaceBehaviorAssignment(6, water, null, "reuse:6", water.Label)],
        Array.Empty<NativeTerrainSurfaceExistingPatch>(),
        out NativeTerrainSurfaceLayoutPlan? plan,
        out string reason);
    Assert(ok && plan != null, $"Synthetic descriptor reuse failed: {reason}");
    NativeTerrainSurfaceLayoutPlan result = plan!;
    Assert(result.OldSurfaceCount == 1 && result.NewSurfaceCount == 1 && result.DescriptorGrowthBytes == 0,
        "Descriptor reuse unexpectedly changed the special-surface table.");
    Assert(result.ResolvedDescriptors is [{ SurfaceIndex: 0, Appended: false }],
        "Descriptor reuse did not resolve to existing surface index 0.");
    Assert(result.OldFlagCount == 4 && result.NewFlagCount == 5 && result.FlagGrowthBytes == 4,
        "Descriptor reuse did not compose the expected collision flag promotion.");
    Assert(result.TriangleRemaps is [{ StagingTriangleIndex: 4, TargetTriangleIndex: 6 }],
        "Descriptor reuse did not preserve the existing promoter's remap behavior.");

    byte[] final = ApplyPlan(fixture.Bytes, fixture.LevelDataWadOffset, result);
    NativeTerrainSurfaceSourceData reparsed = PortalSourceDataLocator.ParseTerrainSurfaces(
        final,
        fixture.LevelDataWadOffset,
        "synthetic-reuse",
        "Synthetic reuse");
    Assert(reparsed.SpecialSurfaces.Count == 1 &&
           reparsed.SpecialSurfaces[0].RelativeOffset == source.SpecialSurfaces[0].RelativeOffset &&
           reparsed.SpecialSurfaces[0].RawBytes.SequenceEqual(waterRaw),
        "Descriptor reuse changed the existing raw record or pointer.");
    Assert(reparsed.CollisionSurfaceTriangles[4] is
        { SurfaceIndex: 0, SurfaceType: 0, Param1: 0, Param2: 0 },
        "The reused water descriptor did not survive semantic reparse on the remapped triangle.");
}

static void RunAppendPromotionCompositionSyntheticSmoke()
{
    byte[][] oldRawRecords =
    [
        BuildRecord(4, 0, 0, 4),
        BuildRecord(7, 3, 0, 8),
        BuildRecord(0, 1, 2, 12),
        BuildRecord(3, 9, 10, 20)
    ];
    SyntheticSurfaceLevelDataFixture fixture = SyntheticSurfaceLevelDataFixture.Build(oldRawRecords, zeroTailBytes: 96);
    NativeTerrainSurfaceSourceData source = ParseFixture(fixture, "synthetic-append");
    NativeTerrainSurfaceSignature water = new(0, 0, 0);
    byte[] waterRaw = BuildRecord(0, 0, 0, 12);
    NativeTerrainSurfaceDescriptorImport donor = MakeDonor(water, waterRaw, "artisans", 0);

    int targetTriangleByte = fixture.TrianglesStart + (6 * 12);
    int suffixPayloadByte = fixture.CollisionEnd + 4;
    byte changedTriangleByte = (byte)(fixture.Bytes[targetTriangleByte] ^ 0x5A);
    const byte changedSuffixByte = 0x6D;
    NativeTerrainSurfaceExistingPatch[] existingPatches =
    [
        new(
            fixture.LevelDataWadOffset + targetTriangleByte,
            [fixture.Bytes[targetTriangleByte]],
            [changedTriangleByte],
            "synthetic-collision-triangle-edit"),
        new(
            fixture.LevelDataWadOffset + suffixPayloadByte,
            [fixture.Bytes[suffixPayloadByte]],
            [changedSuffixByte],
            "synthetic-cyclorama-edit")
    ];

    bool ok = NativeTerrainSurfaceLayoutComposer.TryBuild(
        fixture.Bytes,
        source,
        [new NativeTerrainSurfaceBehaviorAssignment(6, water, donor, "append:6", water.Label)],
        existingPatches,
        out NativeTerrainSurfaceLayoutPlan? plan,
        out string reason);
    Assert(ok && plan != null, $"Synthetic descriptor append/composition failed: {reason}");
    NativeTerrainSurfaceLayoutPlan result = plan!;
    Assert(result.WadOffset == source.SpecialSurfaceComponentWadOffset,
        "An appended descriptor did not produce one atomic patch beginning at the special-surface component.");
    Assert(result.OldSurfaceCount == 4 && result.NewSurfaceCount == 5 && result.DescriptorGrowthBytes == 16,
        "The water descriptor did not add one pointer plus its 12-byte raw record.");
    Assert(result.NewCollisionWadOffset == result.OldCollisionWadOffset + 16,
        "The collision component did not move by the descriptor growth size.");
    Assert(result.OldFlagCount == 4 && result.NewFlagCount == 5 && result.FlagGrowthBytes == 4,
        "The append did not compose collision flag growth.");
    Assert(result.ZeroTailBytesBefore == 96 && result.ZeroTailBytesAfter == 76,
        "Descriptor plus flag growth did not consume exactly 20 verified zero-tail bytes.");
    Assert(result.ConsumedPatchWadOffsets.Order().SequenceEqual(existingPatches.Select(patch => patch.WadOffset).Order()),
        "Collision/suffix patches were not consumed into the atomic relocated layout.");
    Assert(result.ResolvedDescriptors is [{ SurfaceIndex: 4, Appended: true, RawByteLength: 12 }],
        "The missing water descriptor did not resolve to appended index 4.");

    byte[] final = ApplyPlan(fixture.Bytes, fixture.LevelDataWadOffset, result);
    NativeTerrainSurfaceSourceData reparsed = PortalSourceDataLocator.ParseTerrainSurfaces(
        final,
        fixture.LevelDataWadOffset,
        "synthetic-append",
        "Synthetic append");
    for (int index = 0; index < oldRawRecords.Length; index++)
    {
        Assert(reparsed.SpecialSurfaces[index].RawBytes.SequenceEqual(oldRawRecords[index]),
            $"Existing variable-length raw record {index} changed during append.");
        Assert(reparsed.SpecialSurfaces[index].RelativeOffset == source.SpecialSurfaces[index].RelativeOffset + 4,
            $"Existing raw record pointer {index} was not adjusted by one new pointer word.");
    }
    Assert(reparsed.SpecialSurfaces[4].RawBytes.SequenceEqual(waterRaw) &&
           reparsed.SpecialSurfaces[4] is { Type: 0, Param1: 0, Param2: 0 },
        "The appended raw water descriptor failed exact readback.");
    Assert(reparsed.CollisionSurfaceTriangles[4] is
        { SurfaceIndex: 4, SurfaceType: 0, Param1: 0, Param2: 0 },
        "The remapped collision triangle did not resolve to the appended water descriptor.");
    Assert(reparsed.CollisionSurfaceTriangles[4].FlagByte == 0xC4,
        "The appended surface assignment did not preserve the implicit high flag bits.");

    int finalTriangleTable = checked((int)(reparsed.Collision.TriangleTableWadOffset - fixture.LevelDataWadOffset));
    byte[] expectedTargetTriangle = fixture.TriangleBytes(fixture.Bytes, 6);
    expectedTargetTriangle[0] = changedTriangleByte;
    Assert(final.AsSpan(finalTriangleTable + (4 * 12), 12).SequenceEqual(expectedTargetTriangle),
        "The existing target-triangle edit did not travel with its promoted geometry.");

    int finalCollisionStart = checked((int)(reparsed.CollisionComponentWadOffset - fixture.LevelDataWadOffset));
    int finalCollisionEnd = checked(finalCollisionStart + ReadInt32(final, finalCollisionStart));
    int sourceSuffixLength = fixture.UsedLevelEnd - fixture.CollisionEnd;
    byte[] expectedSuffix = fixture.Bytes.AsSpan(fixture.CollisionEnd, sourceSuffixLength).ToArray();
    expectedSuffix[4] = changedSuffixByte;
    Assert(final.AsSpan(finalCollisionEnd, sourceSuffixLength).SequenceEqual(expectedSuffix),
        "The parsed cyclorama/portal/particle/sound suffix was not preserved byte-for-byte with its existing edit.");
    Assert(final.AsSpan(fixture.UsedLevelEnd + 20).IndexOfAnyExcept((byte)0) < 0,
        "The composed synthetic level-data tail is not all zero.");
}

static void RunNegativeSyntheticGuards()
{
    NativeTerrainSurfaceSignature water = new(0, 0, 0);
    byte[] waterRaw = BuildRecord(0, 0, 0, 12);
    NativeTerrainSurfaceDescriptorImport validDonor = MakeDonor(water, waterRaw, "artisans", 0);

    SyntheticSurfaceLevelDataFixture baseFixture = SyntheticSurfaceLevelDataFixture.Build([BuildRecord(4, 0, 0, 4)]);
    NativeTerrainSurfaceSourceData baseSource = ParseFixture(baseFixture, "negative-base");

    ExpectBlocked(
        baseFixture,
        baseSource,
        [new NativeTerrainSurfaceBehaviorAssignment(6, water, null, "missing", water.Label)],
        [],
        "raw donor record",
        "missing donor");

    NativeTerrainSurfaceDescriptorImport badHash = validDonor with { RawSha256 = new string('0', 64) };
    ExpectBlocked(
        baseFixture,
        baseSource,
        [new NativeTerrainSurfaceBehaviorAssignment(6, water, badHash, "hash", water.Label)],
        [],
        "SHA-256",
        "bad donor hash");

    NativeTerrainSurfaceDescriptorImport wrongRaw = validDonor with
    {
        RawRecord = BuildRecord(0, 0, 1, 12),
        RawSha256 = ""
    };
    ExpectBlocked(
        baseFixture,
        baseSource,
        [new NativeTerrainSurfaceBehaviorAssignment(6, water, wrongRaw, "raw", water.Label)],
        [],
        "decodes as",
        "raw/signature mismatch");

    NativeTerrainSurfaceDescriptorImport shortRaw = validDonor with
    {
        RawRecord = BuildRecord(0, 0, 0, 8),
        RawSha256 = ""
    };
    ExpectBlocked(
        baseFixture,
        baseSource,
        [new NativeTerrainSurfaceBehaviorAssignment(6, water, shortRaw, "short", water.Label)],
        [],
        "requires 12",
        "malformed raw length");

    NativeTerrainSurfaceSignature linked = new(7, 11, 0);
    NativeTerrainSurfaceDescriptorImport linkedDonor = MakeDonor(
        linked,
        BuildRecord(7, 11, 0, 8),
        "linked-source",
        0);
    ExpectBlocked(
        baseFixture,
        baseSource,
        [new NativeTerrainSurfaceBehaviorAssignment(6, linked, linkedDonor, "linked", linked.Label)],
        [],
        "not a portable",
        "nonportable descriptor");

    SyntheticSurfaceLevelDataFixture noTail = SyntheticSurfaceLevelDataFixture.Build(
        [BuildRecord(4, 0, 0, 4)],
        flagCount: 4,
        zeroTailBytes: 16);
    NativeTerrainSurfaceSourceData noTailSource = ParseFixture(noTail, "negative-tail");
    ExpectBlocked(
        noTail,
        noTailSource,
        [new NativeTerrainSurfaceBehaviorAssignment(6, water, validDonor, "tail", water.Label)],
        [],
        "only 16",
        "combined zero-tail capacity");

    byte[][] maximumRecords = Enumerable.Range(0, 63)
        .Select(_ => BuildRecord(4, 0, 0, 4))
        .ToArray();
    SyntheticSurfaceLevelDataFixture fullTable = SyntheticSurfaceLevelDataFixture.Build(maximumRecords, zeroTailBytes: 512);
    NativeTerrainSurfaceSourceData fullTableSource = ParseFixture(fullTable, "negative-count");
    ExpectBlocked(
        fullTable,
        fullTableSource,
        [new NativeTerrainSurfaceBehaviorAssignment(6, water, validDonor, "count", water.Label)],
        [],
        "index 63 is reserved",
        "maximum descriptor count");

    NativeTerrainSurfaceExistingPatch paddingOwner = new(
        baseFixture.LevelDataWadOffset + baseFixture.UsedLevelEnd,
        [baseFixture.Bytes[baseFixture.UsedLevelEnd]],
        [0x66],
        "padding-owner");
    ExpectBlocked(
        baseFixture,
        baseSource,
        [new NativeTerrainSurfaceBehaviorAssignment(6, water, validDonor, "padding", water.Label)],
        [paddingOwner],
        "zero padding",
        "padding owner");

    NativeTerrainSurfaceExistingPatch badBefore = new(
        baseFixture.LevelDataWadOffset + baseFixture.TrianglesStart,
        [(byte)(baseFixture.Bytes[baseFixture.TrianglesStart] ^ 0xFF)],
        [0x44],
        "bad-before");
    ExpectBlocked(
        baseFixture,
        baseSource,
        [new NativeTerrainSurfaceBehaviorAssignment(6, water, validDonor, "before", water.Label)],
        [badBefore],
        "does not match",
        "existing patch before-image");

    NativeTerrainSurfaceSignature lava = new(0, 0, 1);
    ExpectBlocked(
        baseFixture,
        baseSource,
        [
            new NativeTerrainSurfaceBehaviorAssignment(6, water, validDonor, "conflict-water", water.Label),
            new NativeTerrainSurfaceBehaviorAssignment(
                6,
                lava,
                MakeDonor(lava, BuildRecord(0, 0, 1, 12), "peacekeepers", 0),
                "conflict-lava",
                lava.Label)
        ],
        [],
        "assigned both",
        "conflicting triangle assignments");
}

static RetailMatrixResult RunAllLevelRetailWaterMatrix(string workspaceRoot, string sourceImagePath)
{
    LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
    LevelDefinition[] levels = catalog.Levels
        .Where(level => level.SourceWadEntry >= 0)
        .ToArray();
    Assert(levels.Length == 35, $"Retail water matrix found {levels.Length} mapped levels, expected 35.");

    LevelDefinition artisans = catalog.FindByKey("artisans")
        ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
    NativeTerrainSurfaceSourceData artisansSource = PortalSourceDataLocator.LocateTerrainSurfaces(sourceImagePath, artisans);
    PortalSpecialSurfaceRecord waterRecord = artisansSource.SpecialSurfaces.Single(record =>
        record.Type == 0 && record.Param1 == 0 && record.Param2 == 0);
    NativeTerrainSurfaceSignature water = new(0, 0, 0);
    NativeTerrainSurfaceDescriptorImport waterDonor = MakeDonor(
        water,
        waterRecord.RawBytes,
        artisans.Key,
        waterRecord.Index);

    List<RetailLevelResult> results = [];
    foreach (LevelDefinition level in levels)
    {
        NativeTerrainSurfaceSourceData source = PortalSourceDataLocator.LocateTerrainSurfaces(sourceImagePath, level);
        byte[] levelData = RetailLevelDataReader.ReadLogicalWadBytes(
            sourceImagePath,
            source.LevelDataWadOffset,
            source.LevelDataByteLength);
        PortalSpecialSurfaceRecord? existingWater = source.SpecialSurfaces.FirstOrDefault(record =>
            record.Type == 0 && record.Param1 == 0 && record.Param2 == 0);
        bool expectedAppend = existingWater == null;
        Assert(source.Collision.FlagCount < source.Collision.TriangleCount,
            $"{level.DisplayName} has no beyond-count triangle for the focused promotion matrix.");
        int targetTriangleIndex = source.Collision.FlagCount;
        byte[][] oldRaw = source.SpecialSurfaces.Select(record => record.RawBytes.ToArray()).ToArray();
        int[] oldPointers = source.SpecialSurfaces.Select(record => record.RelativeOffset).ToArray();

        bool ok = NativeTerrainSurfaceLayoutComposer.TryBuild(
            levelData,
            source,
            [new NativeTerrainSurfaceBehaviorAssignment(
                targetTriangleIndex,
                water,
                waterDonor,
                $"{level.Key}:{targetTriangleIndex}",
                water.Label)],
            Array.Empty<NativeTerrainSurfaceExistingPatch>(),
            out NativeTerrainSurfaceLayoutPlan? plan,
            out string reason);
        Assert(ok && plan != null, $"{level.DisplayName} water layout plan failed: {reason}");
        NativeTerrainSurfaceLayoutPlan built = plan!;
        int expectedDescriptorGrowth = expectedAppend ? 16 : 0;
        int expectedWaterIndex = existingWater?.Index ?? source.SpecialSurfaces.Count;
        Assert(built.DescriptorGrowthBytes == expectedDescriptorGrowth,
            $"{level.DisplayName} descriptor growth is {built.DescriptorGrowthBytes}, expected {expectedDescriptorGrowth}.");
        Assert(built.NewSurfaceCount == source.SpecialSurfaces.Count + (expectedAppend ? 1 : 0),
            $"{level.DisplayName} reported the wrong final descriptor count.");
        Assert(built.ResolvedDescriptors.Single().SurfaceIndex == expectedWaterIndex &&
               built.ResolvedDescriptors.Single().Appended == expectedAppend,
            $"{level.DisplayName} resolved water to the wrong target descriptor.");

        byte[] final = ApplyPlan(levelData, source.LevelDataWadOffset, built);
        NativeTerrainSurfaceSourceData reparsed = PortalSourceDataLocator.ParseTerrainSurfaces(
            final,
            source.LevelDataWadOffset,
            level.Key,
            level.DisplayName);
        Assert(reparsed.LevelDataByteLength == source.LevelDataByteLength,
            $"{level.DisplayName} changed the fixed level-data block length.");
        Assert(reparsed.CollisionComponentWadOffset == source.CollisionComponentWadOffset + expectedDescriptorGrowth,
            $"{level.DisplayName} collision component moved by the wrong amount.");
        Assert(reparsed.Collision.TriangleCount == source.Collision.TriangleCount &&
               reparsed.Collision.FlagCount == source.Collision.FlagCount + 1,
            $"{level.DisplayName} collision counts failed final readback.");
        Assert(reparsed.CollisionSurfaceTriangles[targetTriangleIndex] is
            { SurfaceIndex: var surfaceIndex, SurfaceType: 0, Param1: 0, Param2: 0 } &&
            surfaceIndex == expectedWaterIndex,
            $"{level.DisplayName} target triangle did not reparse as damaging water.");

        int pointerShift = expectedAppend ? 4 : 0;
        for (int index = 0; index < oldRaw.Length; index++)
        {
            Assert(reparsed.SpecialSurfaces[index].RawBytes.SequenceEqual(oldRaw[index]),
                $"{level.DisplayName} changed existing raw descriptor {index}.");
            Assert(reparsed.SpecialSurfaces[index].RelativeOffset == oldPointers[index] + pointerShift,
                $"{level.DisplayName} changed existing pointer {index} by the wrong amount.");
        }
        Assert(reparsed.SpecialSurfaces[expectedWaterIndex].RawBytes.SequenceEqual(waterRecord.RawBytes),
            $"{level.DisplayName} water descriptor raw bytes differ from the Artisans donor.");

        int oldCollisionStart = checked((int)(source.CollisionComponentWadOffset - source.LevelDataWadOffset));
        int oldCollisionEnd = checked(oldCollisionStart + ReadInt32(levelData, oldCollisionStart));
        int oldUsedEnd = levelData.Length - source.FlagPromotionCapacity.VerifiedZeroTailBytes;
        int newCollisionStart = checked((int)(reparsed.CollisionComponentWadOffset - reparsed.LevelDataWadOffset));
        int newCollisionEnd = checked(newCollisionStart + ReadInt32(final, newCollisionStart));
        int suffixLength = oldUsedEnd - oldCollisionEnd;
        Assert(final.AsSpan(newCollisionEnd, suffixLength).SequenceEqual(levelData.AsSpan(oldCollisionEnd, suffixLength)),
            $"{level.DisplayName} changed the collision suffix while relocating it.");
        int finalUsedEnd = final.Length - reparsed.FlagPromotionCapacity.VerifiedZeroTailBytes;
        Assert(final.AsSpan(finalUsedEnd).IndexOfAnyExcept((byte)0) < 0,
            $"{level.DisplayName} final zero tail contains nonzero bytes.");
        Assert(built.ZeroTailBytesAfter == reparsed.FlagPromotionCapacity.VerifiedZeroTailBytes,
            $"{level.DisplayName} plan/readback zero-tail counts disagree.");

        results.Add(new RetailLevelResult(
            level.Key,
            level.DisplayName,
            expectedAppend ? "append" : "reuse",
            source.SpecialSurfaces.Count,
            reparsed.SpecialSurfaces.Count,
            expectedWaterIndex,
            waterRecord.RawBytes.Length,
            Sha256(waterRecord.RawBytes),
            built.DescriptorGrowthBytes,
            built.FlagGrowthBytes,
            built.ZeroTailBytesBefore,
            built.ZeroTailBytesAfter,
            targetTriangleIndex,
            true));
    }

    int reused = results.Count(result => result.Resolution == "reuse");
    int appended = results.Count(result => result.Resolution == "append");
    Assert(reused == 17 && appended == 18,
        $"Retail water matrix resolved {reused} reuse/{appended} append, expected 17/18.");
    return new RetailMatrixResult(results, reused, appended);
}

static void WriteReport(string workspaceRoot, string sourceImagePath, RetailMatrixResult matrix)
{
    string reportRoot = Path.Combine(workspaceRoot, "_local", "smoke", "native-terrain-surface-layout");
    Directory.CreateDirectory(reportRoot);
    string jsonPath = Path.Combine(reportRoot, "native-terrain-surface-layout-smoke.json");
    string markdownPath = Path.Combine(reportRoot, "native-terrain-surface-layout-smoke.md");
    var report = new
    {
        generatedAtUtc = DateTimeOffset.UtcNow,
        sourceImage = sourceImagePath,
        passed = matrix.Levels.Count == 35 && matrix.Levels.All(level => level.Passed),
        levelCount = matrix.Levels.Count,
        reusedCount = matrix.ReusedCount,
        appendedCount = matrix.AppendedCount,
        note = "In-memory structural proof only. No BIN/CUE was written and live Spyro damage still requires DuckStation validation.",
        levels = matrix.Levels
    };
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

    StringBuilder markdown = new();
    markdown.AppendLine("# Native Terrain Surface Layout Smoke");
    markdown.AppendLine();
    markdown.AppendLine("**PASSED** - synthetic descriptor/promotion guards and all 35 retail levels reparsed successfully in memory.");
    markdown.AppendLine();
    markdown.AppendLine($"- Source: `{sourceImagePath}`");
    markdown.AppendLine($"- Reused resident damaging-water descriptor: {matrix.ReusedCount}");
    markdown.AppendLine($"- Appended Artisans damaging-water descriptor: {matrix.AppendedCount}");
    markdown.AppendLine("- No BIN/CUE was written; live damage remains a separate DuckStation proof.");
    markdown.AppendLine();
    markdown.AppendLine("| Level | Resolution | Surfaces | Water index | Descriptor growth | Flag growth | Zero tail | Triangle | Result |");
    markdown.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
    foreach (RetailLevelResult level in matrix.Levels)
    {
        markdown.AppendLine(
            $"| {level.LevelName} | {level.Resolution} | {level.OldSurfaceCount}->{level.NewSurfaceCount} | {level.WaterSurfaceIndex} | {level.DescriptorGrowthBytes} | {level.FlagGrowthBytes} | {level.ZeroTailBytesBefore}->{level.ZeroTailBytesAfter} | {level.TargetTriangleIndex} | {(level.Passed ? "PASS" : "FAIL")} |");
    }
    File.WriteAllText(markdownPath, markdown.ToString());
}

static void ExpectBlocked(
    SyntheticSurfaceLevelDataFixture fixture,
    NativeTerrainSurfaceSourceData source,
    IReadOnlyList<NativeTerrainSurfaceBehaviorAssignment> assignments,
    IReadOnlyList<NativeTerrainSurfaceExistingPatch> existingPatches,
    string expectedReason,
    string label)
{
    bool ok = NativeTerrainSurfaceLayoutComposer.TryBuild(
        fixture.Bytes,
        source,
        assignments,
        existingPatches,
        out NativeTerrainSurfaceLayoutPlan? plan,
        out string reason);
    Assert(!ok && plan == null && reason.Contains(expectedReason, StringComparison.OrdinalIgnoreCase),
        $"Negative guard '{label}' did not block with '{expectedReason}': ok={ok}, reason={reason}");
}

static NativeTerrainSurfaceSourceData ParseFixture(SyntheticSurfaceLevelDataFixture fixture, string key) =>
    PortalSourceDataLocator.ParseTerrainSurfaces(
        fixture.Bytes,
        fixture.LevelDataWadOffset,
        key,
        key);

static NativeTerrainSurfaceDescriptorImport MakeDonor(
    NativeTerrainSurfaceSignature signature,
    byte[] raw,
    string sourceLevelKey,
    int sourceSurfaceIndex) =>
    new(signature, raw.ToArray(), sourceLevelKey, sourceSurfaceIndex, Sha256(raw));

static byte[] BuildRecord(int type, int param1, int param2, int length)
{
    if (length < 4 || (length & 3) != 0)
        throw new ArgumentOutOfRangeException(nameof(length));
    byte[] raw = new byte[length];
    BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(0, 4), type);
    if (length >= 8)
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(4, 4), param1);
    if (length >= 12)
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(8, 4), param2);
    for (int offset = 12; offset < length; offset += 4)
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(offset, 4), unchecked((type * 0x10000) + offset));
    return raw;
}

static byte[] ApplyPlan(byte[] source, long levelDataWadOffset, NativeTerrainSurfaceLayoutPlan plan)
{
    byte[] final = source.ToArray();
    int relative = checked((int)(plan.WadOffset - levelDataWadOffset));
    Assert(relative >= 0 && relative + plan.After.Length <= final.Length,
        "The surface layout plan lies outside its level-data block.");
    Assert(final.AsSpan(relative, plan.Before.Length).SequenceEqual(plan.Before),
        "The surface layout plan's before-image does not match its source block.");
    plan.After.CopyTo(final, relative);
    return final;
}

static int ReadInt32(byte[] bytes, int offset) =>
    BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));

static string Sha256(byte[] bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

static string ResolveWorkspaceRoot(string[] args)
{
    if (args.Length > 0 && Directory.Exists(args[0]))
        return Path.GetFullPath(args[0]);
    DirectoryInfo? current = new(Environment.CurrentDirectory);
    while (current != null)
    {
        if (File.Exists(Path.Combine(current.FullName, "spyro-level-catalog.json")))
            return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro editor workspace root.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record RetailLevelResult(
    string LevelKey,
    string LevelName,
    string Resolution,
    int OldSurfaceCount,
    int NewSurfaceCount,
    int WaterSurfaceIndex,
    int RawByteLength,
    string RawSha256,
    int DescriptorGrowthBytes,
    int FlagGrowthBytes,
    int ZeroTailBytesBefore,
    int ZeroTailBytesAfter,
    int TargetTriangleIndex,
    bool Passed);

internal sealed record RetailMatrixResult(
    IReadOnlyList<RetailLevelResult> Levels,
    int ReusedCount,
    int AppendedCount);

using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Skyboxes;

string root = Path.GetFullPath(args.ElementAtOrDefault(0) ?? Directory.GetCurrentDirectory());
string sourceImage = Path.GetFullPath(args.ElementAtOrDefault(1) ?? Path.Combine(root, "Spyro the Dragon (USA).bin"));
string wadAnalysis = Path.Combine(root, "spyro-wad-analysis.json");
Require(File.Exists(sourceImage), $"Missing source image: {sourceImage}");
Require(File.Exists(wadAnalysis), $"Missing WAD analysis: {wadAnalysis}");

LevelCatalog catalog = LevelCatalog.Load(root);
LevelDefinition target = catalog.FindByKey("stonehill") ?? throw new InvalidDataException("Stone Hill is missing.");
LevelDefinition flightDonor = catalog.FindByKey("crystalflight") ?? throw new InvalidDataException("Crystal Flight is missing.");
Spyro1SkyBlockReport skyReport = Spyro1SkyBlockAnalyzer.Analyze(sourceImage, wadAnalysis, catalog);
Spyro1LevelSkyBlockLayout targetLayout = skyReport.Levels.Single(level => level.Key == target.Key);
HashSet<string> flightKeys = new(StringComparer.OrdinalIgnoreCase)
{
    "sunnyflight", "nightflight", "crystalflight", "wildflight", "icyflight"
};
LevelDefinition safeDonor = catalog.Levels
    .Where(level => level.Key != target.Key && !flightKeys.Contains(level.Key))
    .Where(level => skyReport.Levels.Single(layout => layout.Key == level.Key).SkyBlocks[0].ByteLength <= targetLayout.SkyBlocks[0].ByteLength)
    .OrderBy(level => skyReport.Levels.Single(layout => layout.Key == level.Key).SkyBlocks[0].ByteLength)
    .First();

string temp = Path.Combine(Path.GetTempPath(), $"spyro-sky-visibility-smoke-{Environment.ProcessId}");
Directory.CreateDirectory(temp);
try
{
    NativeSkyEditPlan flightEdit = Swap(target, flightDonor);
    string blockedPrefix = Path.Combine(temp, "blocked-flight-to-ground");
    try
    {
        await NativeSkyPatchExporter.ExportBatchAsync(Request(blockedPrefix, flightEdit, writeImage: false, research: false));
        throw new InvalidOperationException("A normal flight-to-ground sky plan was not blocked.");
    }
    catch (NativeSkyFlightDonorException ex)
    {
        Require(ex.TargetLevelKey == target.Key && ex.DonorLevelKey == flightDonor.Key,
            "The flight-donor exception named the wrong target or donor.");
    }
    Require(!File.Exists($"{blockedPrefix}.bin") &&
            !File.Exists($"{blockedPrefix}.cue") &&
            !File.Exists($"{blockedPrefix}.native-sky-patch-plan.json"),
        "The blocked flight donor left a partial artifact.");

    NativeSkyPatchResult research = await NativeSkyPatchExporter.ExportBatchAsync(
        Request(Path.Combine(temp, "research-flight-to-ground"), flightEdit, writeImage: false, research: true));
    AssertScopedPlan(research.Plan, target.LevelId, linkedPortalLevelId: 10);

    NativeSkyEditPlan safeEdit = Swap(target, safeDonor);
    NativeSkyPatchResult safe = await NativeSkyPatchExporter.ExportBatchAsync(
        Request(Path.Combine(temp, "safe-ground-to-ground"), safeEdit, writeImage: true, research: false));
    Require(safe.WroteImage && File.Exists(safe.OutputImagePath), "The safe scoped write did not produce its disposable image.");
    AssertScopedPlan(safe.Plan, target.LevelId, linkedPortalLevelId: 10);

    long hookImageOffset = ParseHex(safe.Plan.SkyOcclusionPatchImageOffset);
    long payloadImageOffset = ParseHex(safe.Plan.SkyOcclusionPayloadImageOffset);
    using FileStream output = File.OpenRead(safe.OutputImagePath);
    uint hookWord = ReadWord(output, hookImageOffset);
    Require(hookWord == 0x0C01CD00, $"Scoped hook word changed: 0x{hookWord:X8}.");
    byte[] payload = ReadExact(output, payloadImageOffset, 0x100);
    Require(BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4)) == 0x3C088007,
        "Scoped payload no longer starts by loading the retail global page.");
    Require(BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(13 * 4, 4)) == 0x03E00008 &&
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(14 * 4, 4)) == 0x8CC40000,
        "Scoped payload no longer replays the retail occlusion-group load for unrelated levels.");
    for (int levelId = 0; levelId <= 64; levelId++)
    {
        byte expected = safe.Plan.SkyOcclusionScopeLevelIds.Contains(levelId) ? (byte)1 : (byte)0;
        Require(payload[0x80 + levelId] == expected, $"Scoped level-table byte {levelId} is wrong.");
    }
    Require(payload[0x80 + 12] == 0, "Unedited Dark Hollow was accidentally placed in Stone Hill's sky bypass scope.");

    Console.WriteLine(
        $"PASS: normal {flightDonor.DisplayName}->{target.DisplayName} blocked atomically; research plan remained explicit; " +
        $"{safeDonor.DisplayName}->{target.DisplayName} wrote guarded hook 0x{hookWord:X8} and exact scope [{string.Join(", ", safe.Plan.SkyOcclusionScopeLevelIds)}].");
}
finally
{
    if (Directory.Exists(temp))
        Directory.Delete(temp, recursive: true);
}

NativeSkyBatchPatchRequest Request(string prefix, NativeSkyEditPlan edit, bool writeImage, bool research) => new(
    SourceImagePath: sourceImage,
    SourceCuePath: Path.ChangeExtension(sourceImage, ".cue"),
    WadAnalysisPath: wadAnalysis,
    WorkspacePath: root,
    OutputPrefix: prefix,
    Catalog: catalog,
    Edits: [new NativeSkyBatchEdit(target, edit)],
    WriteImage: writeImage,
    AllowUnprovenLinkedPortalExpansion: research);

static NativeSkyEditPlan Swap(LevelDefinition target, LevelDefinition donor) => new(
    Version: 1,
    SavedAt: DateTimeOffset.UtcNow,
    LevelKey: target.Key,
    LevelName: target.DisplayName,
    Mode: NativeSkyEditPlan.SwapMode,
    PalettePreset: "",
    CustomPaletteHex: "",
    DonorLevelKey: donor.Key,
    ImportedSkyPath: "",
    ImportedSkySha256: "");

static void AssertScopedPlan(NativeSkyPatchPlan plan, int targetLevelId, int linkedPortalLevelId)
{
    Require(plan.SkyOcclusionBypassApplied, "Geometry swap did not install scoped sky visibility.");
    Require(plan.SkyOcclusionScope == "edited-destinations-and-linked-portal-contexts",
        $"Unexpected sky visibility scope '{plan.SkyOcclusionScope}'.");
    Require(plan.SkyOcclusionScopeLevelIds.SequenceEqual(new[] { linkedPortalLevelId, targetLevelId }.OrderBy(value => value)),
        $"Expected only destination/portal contexts {linkedPortalLevelId},{targetLevelId}; got {string.Join(",", plan.SkyOcclusionScopeLevelIds)}.");
    Require(plan.SkyOcclusionPatchRuntimeAddress == "0x80051F90" &&
            plan.SkyOcclusionPayloadRuntimeAddress == "0x80073400",
        "Scoped hook/payload addresses changed.");
    Require(plan.ExecutablePatchCount == 2,
        $"Fresh scoped sky write expected two guarded executable ranges, got {plan.ExecutablePatchCount}.");
}

static uint ReadWord(FileStream stream, long offset) =>
    BinaryPrimitives.ReadUInt32LittleEndian(ReadExact(stream, offset, 4));

static byte[] ReadExact(FileStream stream, long offset, int length)
{
    byte[] bytes = new byte[length];
    stream.Position = offset;
    stream.ReadExactly(bytes);
    return bytes;
}

static long ParseHex(string value) =>
    Convert.ToInt64(value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value, 16);

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

RunSyntheticCompanionSmoke();
if (args.Contains("--live-artisans", StringComparer.OrdinalIgnoreCase))
    RunLiveArtisansPlanSmoke();

Console.WriteLine("Far-LOD texture companion smoke passed.");

static void RunSyntheticCompanionSmoke()
{
    byte[] sector = BuildSyntheticSector(textureId: 5, lpCommand: 0x30);
    Dictionary<int, NativeTerrainTextureLowDetailReplacement> replacements = new()
    {
        [5] = new(
            5,
            new NativeTerrainTextureRepresentativeColor(80, 80, 80),
            new NativeTerrainTextureRepresentativeColor(160, 40, 80))
    };
    NativeTerrainTextureLowDetailCompanionPatch patch =
        NativeTerrainTextureLowDetailCompanion.Build(sector, replacements);
    Assert(patch.HasChanges, "Synthetic affected sector did not produce an LP companion.");
    Assert(patch.AffectedHighDetailFaceCount == 1, "Synthetic HP face coverage changed.");
    Assert(patch.AffectedLowDetailCornerCount == 4, "Synthetic LP corner coverage changed.");
    Assert(patch.ChangedLowDetailColorCount == 4, "Synthetic LP color coverage changed.");
    for (int color = 0; color < 4; color++)
    {
        int offset = color * 4;
        Assert(patch.After[offset] == 177, $"Synthetic LP red {color} is {patch.After[offset]}, expected 177.");
        Assert(patch.After[offset + 1] == 62, $"Synthetic LP green {color} is {patch.After[offset + 1]}, expected 62.");
        Assert(patch.After[offset + 2] == 100, $"Synthetic LP blue {color} is {patch.After[offset + 2]}, expected 100.");
        Assert(patch.After[offset + 3] == 0x30, $"Synthetic LP command byte {color} changed.");
    }

    NativeTerrainTextureLowDetailCompanionPatch unrelated =
        NativeTerrainTextureLowDetailCompanion.Build(
            sector,
            new Dictionary<int, NativeTerrainTextureLowDetailReplacement>
            {
                [6] = new(
                    6,
                    new NativeTerrainTextureRepresentativeColor(80, 80, 80),
                    new NativeTerrainTextureRepresentativeColor(160, 40, 80))
            });
    Assert(!unrelated.HasChanges, "Unrelated texture id changed LP bytes.");
    Assert(unrelated.Before.SequenceEqual(unrelated.After), "Unrelated LP table is not byte-identical.");

    byte[] invalid = BuildSyntheticSector(textureId: 5, lpCommand: 0x55);
    bool rejected = false;
    try
    {
        _ = NativeTerrainTextureLowDetailCompanion.Build(invalid, replacements);
    }
    catch (InvalidDataException ex)
    {
        rejected = ex.Message.Contains("command byte", StringComparison.OrdinalIgnoreCase);
    }
    Assert(rejected, "Invalid LP command bytes were not rejected fail-closed.");
}

static void RunLiveArtisansPlanSmoke()
{
    string workspaceRoot = ResolveWorkspaceRoot();
    string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
    string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
    Assert(File.Exists(sourceImagePath), $"Missing retail source BIN: {sourceImagePath}");
    Assert(File.Exists(sourceCuePath), $"Missing retail source CUE: {sourceCuePath}");

    string projectRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Spyro Editor",
        "Projects",
        "default-project");
    string sourceSearchPath = Path.Combine(
        projectRoot,
        "_local",
        "terrain",
        "artisans-runtime-terrain-source-search-native.json");
    string terrainEditsPath = Path.Combine(projectRoot, "artisans-terrain-edits.json");
    string relocationPath = Path.Combine(
        projectRoot,
        "artisans-native-terrain-texture-relocations.json");
    Assert(File.Exists(sourceSearchPath), $"Missing live source-search map: {sourceSearchPath}");
    Assert(File.Exists(terrainEditsPath), $"Missing live terrain edits: {terrainEditsPath}");
    Assert(File.Exists(relocationPath), $"Missing live texture relocations: {relocationPath}");

    LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
    LevelDefinition artisans = catalog.FindByKey("artisans")
        ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
    string outputRoot = Path.Combine(workspaceRoot, "_local", "smoke", "far-lod-texture-companion");
    Directory.CreateDirectory(outputRoot);
    TerrainPatchPlan plan = TerrainPatchExporter.BuildPlan(
        sourceImagePath,
        sourceCuePath,
        Path.Combine(outputRoot, "artisans-far-lod-companion.bin"),
        Path.Combine(outputRoot, "artisans-far-lod-companion.cue"),
        artisans,
        ramPath: "",
        sourceSearchPath,
        terrainEditsPath,
        customTexturesPath: "",
        nativeTextureRelocationsPath: relocationPath);

    TerrainPatch[] companions = plan.Patches
        .Where(patch => string.Equals(
            patch.Kind,
            "native-terrain-texture-lp-companion",
            StringComparison.Ordinal))
        .ToArray();
    Assert(companions.Length > 0, "Live Artisans plan has no LP companion patches.");
    Assert(companions.All(patch => patch.ByteLength > 0 && patch.ByteLength % 4 == 0),
        "Live Artisans LP companion patch length is not an RGB-command table.");
    foreach (TerrainPatch patch in companions)
    {
        byte[] before = ParseHex(patch.BeforeHexPreview);
        byte[] after = ParseHex(patch.AfterHexPreview);
        Assert(before.Length == patch.ByteLength && after.Length == patch.ByteLength,
            $"{patch.Label} preview length changed.");
        for (int offset = 3; offset < before.Length; offset += 4)
        {
            Assert(before[offset] is 0x00 or 0x30,
                $"{patch.Label} has non-native LP command 0x{before[offset]:X2}.");
            Assert(after[offset] == before[offset],
                $"{patch.Label} changed LP command byte {offset / 4}.");
        }
    }
    Assert(plan.Notes.Any(note => note.Contains(
            "far-LOD synchronization patched",
            StringComparison.OrdinalIgnoreCase)),
        "Live Artisans plan did not report LP companion coverage.");
    Assert(!plan.SkippedEdits.Any(edit =>
            edit.Contains("LP companion", StringComparison.OrdinalIgnoreCase) ||
            edit.Contains("far-LOD", StringComparison.OrdinalIgnoreCase)),
        $"Live Artisans plan contains a companion-specific blocker: {string.Join(" | ", plan.SkippedEdits.Where(edit => edit.Contains("LP companion", StringComparison.OrdinalIgnoreCase) || edit.Contains("far-LOD", StringComparison.OrdinalIgnoreCase)))}");

    string reportPath = Path.Combine(outputRoot, "artisans-far-lod-companion-smoke.txt");
    File.WriteAllText(
        reportPath,
        string.Join(
            Environment.NewLine,
            $"LP companion patches: {companions.Length:N0}",
            $"LP companion bytes: {companions.Sum(patch => patch.ByteLength):N0}",
            $"Total terrain plan patches: {plan.PatchCount:N0}",
            plan.Notes.Last()));
    Console.WriteLine($"Live Artisans report: {reportPath}");
}

static byte[] BuildSyntheticSector(int textureId, byte lpCommand)
{
    const int lpVertices = 4;
    const int lpColors = 4;
    const int lpFaces = 1;
    const int hpVertices = 4;
    const int hpColors = 0;
    const int hpFaces = 1;
    int byteLength = (7 + lpVertices + lpColors + (lpFaces * 2) + hpVertices + (hpColors * 2) + (hpFaces * 4)) * 4;
    byte[] bytes = new byte[byteLength];
    bytes[16] = lpVertices;
    bytes[17] = lpColors;
    bytes[18] = lpFaces;
    bytes[20] = hpVertices;
    bytes[21] = hpColors;
    bytes[22] = hpFaces;

    int lpColorStart = 28 + (lpVertices * 4);
    for (int index = 0; index < lpColors; index++)
    {
        int offset = lpColorStart + (index * 4);
        bytes[offset] = 100;
        bytes[offset + 1] = 100;
        bytes[offset + 2] = 100;
        bytes[offset + 3] = lpCommand;
    }

    int lpFaceStart = lpColorStart + (lpColors * 4);
    uint slots = PackSixBitSlots(0, 1, 2, 3);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(lpFaceStart, 4), slots);
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(lpFaceStart + 4, 4), slots);
    int hpVertexStart = lpFaceStart + 8;
    int hpFaceStart = hpVertexStart + (hpVertices * 4);
    bytes[hpFaceStart] = 0;
    bytes[hpFaceStart + 1] = 1;
    bytes[hpFaceStart + 2] = 2;
    bytes[hpFaceStart + 3] = 3;
    BinaryPrimitives.WriteUInt32LittleEndian(
        bytes.AsSpan(hpFaceStart + 8, 4),
        (uint)(textureId & 0x7F));
    return bytes;
}

static uint PackSixBitSlots(int first, int second, int third, int fourth) =>
    ((uint)(first & 0x3F) << 26) |
    ((uint)(second & 0x3F) << 20) |
    ((uint)(third & 0x3F) << 14) |
    ((uint)(fourth & 0x3F) << 8);

static byte[] ParseHex(string text) =>
    text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(part => Convert.ToByte(part, 16))
        .ToArray();

static string ResolveWorkspaceRoot()
{
    DirectoryInfo? current = new(AppContext.BaseDirectory);
    while (current != null)
    {
        if (File.Exists(Path.Combine(current.FullName, "spyro-level-catalog.json")))
            return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate spyro-level-catalog.json.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

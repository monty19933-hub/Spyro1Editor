using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Skyboxes;
using Spyro.Editor.Core.Workspace;

const string CompanionPatchKind = "native-terrain-texture-lp-companion";

string projectRoot = Option("--project=") ??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Spyro Editor", "Projects", "default-project");
EditorWorkspace workspace = EditorWorkspace.Find(projectRoot);
LevelCatalog catalog = LevelCatalog.Load(workspace.RootPath);
LevelDefinition artisans = catalog.FindByKey("artisans")
    ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");

string sourceImage = Option("--source=") ??
    Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        "Spyro the Dragon (USA).bin");
string sourceCue = Option("--cue=") ?? Path.ChangeExtension(sourceImage, ".cue");
string wadAnalysis = Option("--analysis=") ??
    Path.Combine(workspace.RootPath, "spyro-wad-analysis.json");
string outputDirectory = Option("--output=") ??
    Path.Combine(workspace.RootPath, "output", "artisans-outer-terrain-fix-test");
string terrainEdits = Path.Combine(workspace.RootPath, "artisans-terrain-edits.json");
string textureRelocations = NativeTerrainTextureRelocationEditStore.ManifestPath(
    workspace.RootPath,
    artisans.Key);
string skyEditPath = Path.Combine(
    workspace.RootPath,
    $"{artisans.Key}-skybox-edit-plan.json");
string sourceSearch = Path.Combine(
    workspace.RootPath,
    "_local",
    "terrain",
    "artisans-runtime-terrain-source-search-native.json");
string ramPath = TerrainPatchDataLocator.FindRamDump(workspace, artisans.Key);

RequireFile(sourceImage, "retail source BIN");
RequireFile(sourceCue, "retail source CUE");
RequireFile(wadAnalysis, "WAD analysis");
RequireFile(terrainEdits, "saved Artisans terrain manifest");
RequireFile(textureRelocations, "saved Artisans texture-relocation manifest");
RequireFile(skyEditPath, "saved Artisans sky/environment manifest");
RequireFile(sourceSearch, "Artisans native terrain source search");
var discLayout = DetectDiscLayout(sourceImage);

NativeSkyEditPlan skyEdit = NativeSkyEditStore.Load(workspace.RootPath, artisans)
    ?? throw new InvalidDataException("The Artisans sky/environment manifest did not load.");
if (!skyEdit.EnvironmentGrade.Enabled)
    throw new InvalidDataException("The live Artisans sky manifest does not have environment matching enabled.");
if (string.IsNullOrWhiteSpace(skyEdit.EnvironmentGrade.DonorLevelKey))
    throw new InvalidDataException("The live Artisans environment grade has no donor level.");

IReadOnlyList<NativeTerrainTextureRelocationEdit> savedRelocations =
    NativeTerrainTextureRelocationEditStore.Load(workspace.RootPath, artisans.Key);
if (savedRelocations.Count == 0)
    throw new InvalidDataException("The live Artisans project has no saved native texture relocations.");

Directory.CreateDirectory(outputDirectory);
string inputDirectory = Path.Combine(outputDirectory, "input-snapshot");
Directory.CreateDirectory(inputDirectory);

string[] trackedInputs =
[
    terrainEdits,
    textureRelocations,
    skyEditPath,
    wadAnalysis,
    sourceSearch
];
Dictionary<string, string> inputHashes = trackedInputs.ToDictionary(
    path => Path.GetFileName(path) ??
        throw new InvalidDataException($"Tracked input has no file name: {path}"),
    Sha256,
    StringComparer.OrdinalIgnoreCase);
foreach (string input in trackedInputs)
    File.Copy(input, Path.Combine(inputDirectory, Path.GetFileName(input)), true);

string terrainPrefix = Path.Combine(
    outputDirectory,
    "01-Artisans-live-project-terrain");
TerrainPatchResult terrainResult = await TerrainPatchExporter.ExportAsync(
    new TerrainPatchRequest(
        SourceImagePath: sourceImage,
        SourceCuePath: sourceCue,
        OutputPrefix: terrainPrefix,
        Level: artisans,
        RamPath: ramPath,
        SourceSearchPath: sourceSearch,
        TerrainEditsPath: terrainEdits,
        CustomTexturesPath: "",
        WriteImage: true,
        NativeTextureRelocationsPath: textureRelocations));

Assert(terrainResult.WroteImage, "The terrain stage did not emit a BIN.");
RequireFile(terrainResult.OutputImagePath, "terrain-stage BIN");
RequireFile(terrainResult.OutputCuePath, "terrain-stage CUE");
RequireFile(terrainResult.OutputPlanPath, "terrain-stage plan");
Assert(
    terrainResult.Plan.NativeTextureRelocationCount == savedRelocations.Count,
    $"The terrain plan represented {terrainResult.Plan.NativeTextureRelocationCount} of " +
    $"{savedRelocations.Count} saved texture relocations.");
Assert(
    terrainResult.Plan.NativeTextureRelocations.All(summary =>
        summary.RuntimeControlVerified &&
        summary.OwnershipVerified &&
        summary.ExactIndexedPixelsVerified &&
        summary.ExactPalettesVerified &&
        summary.LogicalReadbackVerified &&
        summary.TargetDescriptorMaterialPolicyVerified),
    "At least one saved texture relocation did not pass every native readback proof.");
Assert(
    !terrainResult.Plan.SkippedEdits.Any(IsNativeTextureSkip),
    "At least one native texture relocation was skipped: " +
    string.Join("; ", terrainResult.Plan.SkippedEdits.Where(IsNativeTextureSkip).Take(3)));

TerrainPatch[] companionPatches = terrainResult.Plan.Patches
    .Where(patch => string.Equals(
        patch.Kind,
        CompanionPatchKind,
        StringComparison.OrdinalIgnoreCase))
    .ToArray();
Assert(
    companionPatches.Length > 0,
    $"The live Artisans build emitted no {CompanionPatchKind} patches.");

int companionChangedRgbBytes = 0;
foreach (TerrainPatch patch in companionPatches)
{
    Assert(
        patch.RuntimeKey.StartsWith("sector-0x", StringComparison.OrdinalIgnoreCase),
        $"Companion patch {patch.Label} has unexpected runtime key {patch.RuntimeKey}.");
    Assert(
        patch.ByteLength > 0 && patch.ByteLength % 4 == 0,
        $"Companion patch {patch.RuntimeKey} is not a complete 4-byte LP color table.");

    long imageOffset = ParseOffset(patch.ImageOffset);
    long wadOffset = ParseOffset(patch.WadRelativeOffset);
    (long Start, long End)[] rawRanges = MapFileRangeToImageRanges(
        discLayout,
        fileLba: 37,
        wadOffset,
        patch.ByteLength);
    Assert(
        rawRanges.Length > 0 && rawRanges[0].Start == imageOffset,
        $"Companion patch {patch.RuntimeKey} image/WAD offsets disagree.");
    byte[] before = ReadDiscFileRange(
        sourceImage,
        discLayout,
        fileLba: 37,
        wadOffset,
        patch.ByteLength);
    byte[] after = ReadDiscFileRange(
        terrainResult.OutputImagePath,
        discLayout,
        fileLba: 37,
        wadOffset,
        patch.ByteLength);
    AssertPreview(before, patch.BeforeHexPreview, patch.RuntimeKey, "before");
    AssertPreview(after, patch.AfterHexPreview, patch.RuntimeKey, "after");

    int changedRgbBytes = 0;
    for (int index = 0; index < patch.ByteLength; index += 4)
    {
        byte command = before[index + 3];
        Assert(
            command is 0x00 or 0x30,
            $"Companion patch {patch.RuntimeKey} contains unsupported LP command byte 0x{command:X2}.");
        Assert(
            after[index + 3] == command,
            $"Companion patch {patch.RuntimeKey} changed LP command byte {index + 3}.");
        for (int channel = 0; channel < 3; channel++)
        {
            if (before[index + channel] != after[index + channel])
                changedRgbBytes++;
        }
    }

    Assert(
        changedRgbBytes > 0,
        $"Companion patch {patch.RuntimeKey} did not change any LP RGB bytes.");
    companionChangedRgbBytes += changedRgbBytes;
}

VerifyOnlyDeclaredTerrainBytesChanged(
    sourceImage,
    terrainResult.OutputImagePath,
    terrainResult.Plan.Patches,
    discLayout);

string gradePrefix = Path.Combine(
    outputDirectory,
    "02-Artisans-live-project-environment");
NativeEnvironmentGradePatchResult gradeResult =
    await NativeEnvironmentGradeExporter.ExportBatchAsync(
        new NativeEnvironmentGradeBatchPatchRequest(
            SourceImagePath: terrainResult.OutputImagePath,
            SourceCuePath: terrainResult.OutputCuePath,
            WadAnalysisPath: wadAnalysis,
            OutputPrefix: gradePrefix,
            Catalog: catalog,
            Edits:
            [
                new NativeEnvironmentGradeBatchEdit(
                    artisans,
                    skyEdit.EnvironmentGrade)
            ],
            WriteImage: true));

Assert(gradeResult.WroteImage, "The environment-grade stage did not emit a BIN.");
RequireFile(gradeResult.OutputImagePath, "environment-grade BIN");
RequireFile(gradeResult.OutputCuePath, "environment-grade CUE");
RequireFile(gradeResult.OutputPlanPath, "environment-grade plan");
Assert(
    gradeResult.Plan.EditedLevelCount == 1 &&
    gradeResult.Plan.Matches.Count == 1 &&
    string.Equals(
        gradeResult.Plan.Matches[0].TargetLevelKey,
        artisans.Key,
        StringComparison.OrdinalIgnoreCase),
    "The environment-grade stage did not target only Artisans.");
Assert(
    string.Equals(
        gradeResult.Plan.Matches[0].DonorLevelKey,
        skyEdit.EnvironmentGrade.DonorLevelKey,
        StringComparison.OrdinalIgnoreCase),
    "The environment-grade donor does not match the saved Artisans sky manifest.");
Assert(
    gradeResult.Plan.SceneColorPatchCount > 0 &&
    gradeResult.Plan.Patches.Any(patch =>
        string.Equals(
            patch.Kind,
            "environment-scene-lp-colors",
            StringComparison.OrdinalIgnoreCase)) &&
    gradeResult.Plan.Patches.Any(patch =>
        string.Equals(
            patch.Kind,
            "environment-scene-hp-colors",
            StringComparison.OrdinalIgnoreCase)),
    "The environment-grade stage did not grade both native LP and HP scene colors.");
Assert(
    gradeResult.Plan.TexturePalettePatchCount == 0,
    "Environment matching rewrote terrain texture palettes after the saved donor transplants.");

string finalPrefix = Path.Combine(
    outputDirectory,
    "Spyro Editor - Artisans - Outer Terrain Fix Test");
NativeSkyPatchResult skyResult = await NativeSkyPatchExporter.ExportBatchAsync(
    new NativeSkyBatchPatchRequest(
        SourceImagePath: gradeResult.OutputImagePath,
        SourceCuePath: gradeResult.OutputCuePath,
        WadAnalysisPath: wadAnalysis,
        WorkspacePath: workspace.RootPath,
        OutputPrefix: finalPrefix,
        Catalog: catalog,
        Edits: [new NativeSkyBatchEdit(artisans, skyEdit)],
        WriteImage: true));

Assert(skyResult.WroteImage, "The sky stage did not emit a final BIN.");
RequireFile(skyResult.OutputImagePath, "final Artisans BIN");
RequireFile(skyResult.OutputCuePath, "final Artisans CUE");
RequireFile(skyResult.OutputPlanPath, "final sky plan");
Assert(
    skyResult.Plan.EditedLevelCount == 1 &&
    skyResult.Plan.EditedLevelNames.Count == 1 &&
    string.Equals(
        skyResult.Plan.EditedLevelNames[0],
        artisans.DisplayName,
        StringComparison.OrdinalIgnoreCase),
    "The final sky stage did not target only Artisans.");
Assert(skyResult.Plan.PatchCount > 0, "The saved Artisans sky edit produced no patch.");
string cueText = await File.ReadAllTextAsync(skyResult.OutputCuePath);
Assert(
    cueText.Contains(
        Path.GetFileName(skyResult.OutputImagePath),
        StringComparison.Ordinal),
    "The final CUE does not reference the final Artisans BIN.");

foreach ((string fileName, string beforeHash) in inputHashes)
{
    string livePath = trackedInputs.Single(path =>
        string.Equals(
            Path.GetFileName(path),
            fileName,
            StringComparison.OrdinalIgnoreCase));
    Assert(
        string.Equals(beforeHash, Sha256(livePath), StringComparison.Ordinal),
        $"Live project input {fileName} changed while the focused build was running.");
}

string verificationPath = Path.Combine(
    outputDirectory,
    "artisans-outer-terrain-fix-verification.json");
await File.WriteAllTextAsync(
    verificationPath,
    JsonSerializer.Serialize(
        new
        {
            generatedAt = DateTimeOffset.Now,
            project = workspace.RootPath,
            sourceImage,
            sourceImageSha256 = Sha256(sourceImage),
            inputHashes,
            savedTextureRelocationCount = savedRelocations.Count,
            terrain = new
            {
                terrainResult.OutputImagePath,
                terrainResult.OutputCuePath,
                terrainResult.OutputPlanPath,
                terrainResult.Plan.PatchCount,
                terrainResult.Plan.TotalPatchedBytes,
                terrainResult.Plan.NativeTextureRelocationCount,
                terrainResult.Plan.NativeTextureRelocationBytePatchCount,
                companionPatchCount = companionPatches.Length,
                companionChangedRgbBytes,
                skippedGeometryEditCount = terrainResult.Plan.SkippedEdits.Count,
                skippedGeometryEditSamples = terrainResult.Plan.SkippedEdits.Take(12)
            },
            environment = new
            {
                gradeResult.OutputImagePath,
                gradeResult.OutputCuePath,
                gradeResult.OutputPlanPath,
                gradeResult.Plan.PatchCount,
                gradeResult.Plan.SceneColorPatchCount,
                gradeResult.Plan.TexturePalettePatchCount,
                target = gradeResult.Plan.Matches[0].TargetLevelKey,
                donor = gradeResult.Plan.Matches[0].DonorLevelKey
            },
            sky = new
            {
                skyResult.OutputImagePath,
                skyResult.OutputCuePath,
                skyResult.OutputPlanPath,
                skyResult.Plan.PatchCount,
                skyResult.Plan.RelocatedWad,
                skyResult.Plan.WadGrowthBytes,
                skyResult.Plan.SkyOcclusionBypassApplied
            },
            finalImageSha256 = Sha256(skyResult.OutputImagePath),
            checks = new
            {
                exactCompanionReadback = true,
                lpCommandBytesPreserved = true,
                terrainChangesConfinedToDeclaredPatches = true,
                savedInputsStableDuringBuild = true
            }
        },
        new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine("Artisans live-project outer-terrain rebuild passed.");
Console.WriteLine($"Saved relocation rows: {savedRelocations.Count}");
Console.WriteLine(
    $"LP companion patches: {companionPatches.Length} " +
    $"({companionChangedRgbBytes:N0} changed RGB byte(s))");
Console.WriteLine(
    $"Terrain patches: {terrainResult.Plan.PatchCount:N0}; " +
    $"saved geometry skips: {terrainResult.Plan.SkippedEdits.Count:N0}");
Console.WriteLine(
    $"Environment patches: {gradeResult.Plan.PatchCount:N0}; " +
    $"sky patches: {skyResult.Plan.PatchCount:N0}");
Console.WriteLine($"Final CUE: {skyResult.OutputCuePath}");
Console.WriteLine($"Verification: {verificationPath}");

return;

string? Option(string prefix) => args
    .FirstOrDefault(argument =>
        argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?
    [prefix.Length..];

static bool IsNativeTextureSkip(string text) =>
    text.Contains(
        "native terrain texture",
        StringComparison.OrdinalIgnoreCase) ||
    text.Contains(
        "native texture relocation",
        StringComparison.OrdinalIgnoreCase) ||
    text.Contains(
        "atomic terrain swap blocked",
        StringComparison.OrdinalIgnoreCase);

static void VerifyOnlyDeclaredTerrainBytesChanged(
    string sourcePath,
    string outputPath,
    IReadOnlyList<TerrainPatch> patches,
    (int SectorSize, int UserOffset) discLayout)
{
    FileInfo sourceInfo = new(sourcePath);
    FileInfo outputInfo = new(outputPath);
    Assert(
        sourceInfo.Length == outputInfo.Length,
        "The terrain-only stage unexpectedly changed the disc image length.");

    (long Start, long End)[] ranges = MergeRanges(
        patches.SelectMany(patch =>
            MapFileRangeToImageRanges(
                discLayout,
                fileLba: 37,
                ParseOffset(patch.WadRelativeOffset),
                patch.ByteLength)));

    using FileStream source = File.OpenRead(sourcePath);
    using FileStream output = File.OpenRead(outputPath);
    byte[] before = new byte[1024 * 1024];
    byte[] after = new byte[before.Length];
    long absolute = 0;
    int rangeIndex = 0;
    while (true)
    {
        int beforeCount = source.Read(before);
        int afterCount = output.Read(after);
        Assert(beforeCount == afterCount, "Terrain-stage source/output reads diverged.");
        if (beforeCount == 0)
            break;

        for (int index = 0; index < beforeCount; index++)
        {
            if (before[index] == after[index])
                continue;
            long changedOffset = absolute + index;
            while (rangeIndex < ranges.Length &&
                   changedOffset >= ranges[rangeIndex].End)
            {
                rangeIndex++;
            }
            Assert(
                rangeIndex < ranges.Length &&
                changedOffset >= ranges[rangeIndex].Start &&
                changedOffset < ranges[rangeIndex].End,
                $"Terrain stage changed undeclared image byte 0x{changedOffset:X}.");
        }
        absolute += beforeCount;
    }
}

static (long Start, long End)[] MergeRanges(
    IEnumerable<(long Start, long End)> input)
{
    (long Start, long End)[] ordered = input
        .Where(range => range.End > range.Start)
        .OrderBy(range => range.Start)
        .ThenBy(range => range.End)
        .ToArray();
    if (ordered.Length == 0)
        return [];

    List<(long Start, long End)> merged = [ordered[0]];
    foreach ((long Start, long End) range in ordered.Skip(1))
    {
        (long Start, long End) prior = merged[^1];
        if (range.Start <= prior.End)
        {
            merged[^1] = (prior.Start, Math.Max(prior.End, range.End));
            continue;
        }
        merged.Add(range);
    }
    return merged.ToArray();
}

static byte[] ReadDiscFileRange(
    string path,
    (int SectorSize, int UserOffset) layout,
    int fileLba,
    long fileOffset,
    int length)
{
    byte[] result = new byte[length];
    using FileStream stream = File.OpenRead(path);
    int remaining = length;
    int written = 0;
    long absolute = fileOffset;
    while (remaining > 0)
    {
        int sectorOffset = (int)(absolute % 2048);
        int sector = fileLba + (int)(absolute / 2048);
        int toRead = Math.Min(2048 - sectorOffset, remaining);
        stream.Position =
            ((long)sector * layout.SectorSize) +
            layout.UserOffset +
            sectorOffset;
        stream.ReadExactly(result.AsSpan(written, toRead));
        written += toRead;
        remaining -= toRead;
        absolute += toRead;
    }
    return result;
}

static (long Start, long End)[] MapFileRangeToImageRanges(
    (int SectorSize, int UserOffset) layout,
    int fileLba,
    long fileOffset,
    int length)
{
    List<(long Start, long End)> ranges = [];
    int remaining = length;
    long absolute = fileOffset;
    while (remaining > 0)
    {
        int sectorOffset = (int)(absolute % 2048);
        int sector = fileLba + (int)(absolute / 2048);
        int toRead = Math.Min(2048 - sectorOffset, remaining);
        long start =
            ((long)sector * layout.SectorSize) +
            layout.UserOffset +
            sectorOffset;
        ranges.Add((start, checked(start + toRead)));
        remaining -= toRead;
        absolute += toRead;
    }
    return ranges.ToArray();
}

static (int SectorSize, int UserOffset) DetectDiscLayout(string path)
{
    using FileStream stream = File.OpenRead(path);
    byte[] descriptor = new byte[7];
    foreach ((int sectorSize, int userOffset) in new[]
             {
                 (2048, 0),
                 (2352, 24),
                 (2336, 8)
             })
    {
        long offset = (16L * sectorSize) + userOffset;
        if (stream.Length < offset + 7)
            continue;
        stream.Position = offset;
        stream.ReadExactly(descriptor);
        if (descriptor[0] == 1 &&
            Encoding.ASCII.GetString(descriptor[1..6]) == "CD001")
        {
            return (sectorSize, userOffset);
        }
    }
    throw new InvalidDataException(
        "The retail source image has no readable ISO9660 primary volume descriptor.");
}

static void AssertPreview(
    byte[] actual,
    string preview,
    string runtimeKey,
    string side)
{
    byte[] expected = ParseHex(preview);
    Assert(
        expected.Length <= actual.Length &&
        actual.AsSpan(0, expected.Length).SequenceEqual(expected),
        $"Companion patch {runtimeKey} {side} preview did not match disc readback.");
}

static byte[] ParseHex(string text)
{
    string[] parts = (text ?? "")
        .Split(
            [' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
    return parts.Select(part =>
        byte.Parse(part, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
        .ToArray();
}

static long ParseOffset(string text)
{
    string value = (text ?? "").Trim();
    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
    {
        return long.Parse(
            value[2..],
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture);
    }
    return long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
}

static string Sha256(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

static void RequireFile(string path, string label)
{
    if (!File.Exists(path))
        throw new FileNotFoundException($"The {label} was not found.", path);
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Workspace;

const int TargetTrueIndex = 166;
const int RawXDelta = 16;

EditorWorkspace workspace = EditorWorkspace.Find(args.FirstOrDefault());
LevelCatalog catalog = LevelCatalog.Load(workspace.RootPath);
LevelDefinition stoneHill = catalog.FindByKey("stonehill")
    ?? throw new InvalidOperationException("Stone Hill is missing from the level catalog.");
string sourceImagePath = DiscImageLocator.FindImage(workspace);
string sourceCuePath = DiscImageLocator.FindCueForImage(sourceImagePath);
if (!File.Exists(sourceImagePath) || !File.Exists(sourceCuePath))
    throw new FileNotFoundException("The focused path candidate needs the clean retail BIN/CUE configured in this workspace.");

string outputDirectory = Path.Combine(
    workspace.RootPath,
    "_local",
    "research",
    "egg-thief-path-runtime");
Directory.CreateDirectory(outputDirectory);
string outputPrefix = Path.Combine(
    outputDirectory,
    "Stone-Hill-T166-current-node-plus-1-unit-PATH-ONLY-RUNTIME-CANDIDATE");
string nativeManifestPath = Path.Combine(outputDirectory, "stonehill-empty-native-edits.json");
string pathManifestPath = Path.Combine(outputDirectory, "stonehill-T166-path-only-edits.json");
string proofPath = outputPrefix + "-proof.json";
string checklistPath = outputPrefix + "-runtime-checklist.md";

foreach (string stalePath in new[]
         {
             outputPrefix + ".bin",
             outputPrefix + ".cue",
             outputPrefix + ".moby-source-patch-plan.json",
             nativeManifestPath,
             pathManifestPath,
             proofPath,
             checklistPath
         })
{
    if (File.Exists(stalePath))
        File.Delete(stalePath);
}

await File.WriteAllTextAsync(
    nativeManifestPath,
    new JsonObject
    {
        ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"),
        ["editor"] = "Spyro.Editor.EggThiefPathRuntimeCandidateSmoke",
        ["levelName"] = stoneHill.DisplayName,
        ["editCount"] = 0,
        ["edits"] = new JsonArray()
    }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

IReadOnlyList<NativeMobyPath> paths = EggThiefPathLocator.Locate(sourceImagePath, stoneHill);
NativeMobyPath path = paths.Single(candidate => candidate.OwnerTrueIndex == TargetTrueIndex);
NativePathNode currentNode = path.Nodes[path.CurrentNode];
currentNode.SetRawPosition(
    checked(currentNode.OriginalRawX + RawXDelta),
    currentNode.OriginalRawY,
    currentNode.OriginalRawZ);
Assert(
    await NativeMobyPathEditStore.SaveAsync(pathManifestPath, stoneHill.DisplayName, paths) == 1,
    "The focused candidate did not save exactly one owned path.");

string sourceSha256Before = Sha256File(sourceImagePath);
MobySourcePatchPlan normalPlan = MobySourcePatchExporter.BuildPlan(
    sourceImagePath,
    sourceCuePath,
    outputPrefix + "-normal-should-not-write.bin",
    outputPrefix + "-normal-should-not-write.cue",
    stoneHill,
    nativeManifestPath,
    nativeMobyPathEditsPath: pathManifestPath);
Assert(
    normalPlan.Patches.All(patch =>
        !patch.Kind.Equals("native-moby-path-node-xyz", StringComparison.OrdinalIgnoreCase)),
    "Normal Create BIN unexpectedly promoted an unproven egg-thief path edit.");
MobyBuildSafetyLevelReport normalSafety =
    MobyBuildSafetyInspector.InspectLevel(sourceImagePath, stoneHill, normalPlan);
Assert(
    normalSafety.Issues.Any(issue =>
        issue.Status == MobyBuildSafetyStatus.Blocked &&
        issue.EditorTrueIndex == TargetTrueIndex &&
        issue.CanNavigate),
    "The normal-build runtime gate did not produce a targeted, navigable T166 Build Safety issue.");

MobySourcePatchResult result = await MobySourcePatchExporter.ExportAsync(
    new MobySourcePatchRequest(
        SourceImagePath: sourceImagePath,
        SourceCuePath: sourceCuePath,
        OutputPrefix: outputPrefix,
        Level: stoneHill,
        NativeEditsPath: nativeManifestPath,
        WriteImage: true,
        NativeMobyPathEditsPath: pathManifestPath,
        AllowUnprovenNativeMobyPathResearchPatches: true));
Assert(result.WroteImage, "The research-only path candidate did not write its disposable BIN/CUE.");
MobySourcePatch pathPatch = result.Plan.Patches.Single(patch =>
    patch.Kind.Equals("native-moby-path-node-xyz", StringComparison.OrdinalIgnoreCase));
Assert(pathPatch.TrueIndex == TargetTrueIndex, "The focused patch lost its T166 owner identity.");
Assert(pathPatch.ByteLength == NativeMobyPathPatchExporter.CoordinateBytes, "The focused patch is not exactly one XYZ triplet.");
Assert(
    result.Plan.Patches.Count == 1,
    $"The path-only candidate unexpectedly contains {result.Plan.Patches.Count} patches instead of one.");
Assert(
    ReadImageBytes(result.OutputImagePath, pathPatch.ImageOffset, pathPatch.ByteLength)
        .SequenceEqual(ParseHex(pathPatch.AfterHexPreview)),
    "The final disposable BIN did not read back the exact planned XYZ bytes.");
long patchImageOffset = ParseOffset(pathPatch.ImageOffset);
(long physicalDifferenceCount, long outsidePlannedRangeDifferenceCount) =
    CountPhysicalDifferences(
        sourceImagePath,
        result.OutputImagePath,
        patchImageOffset,
        pathPatch.ByteLength);
Assert(
    physicalDifferenceCount > 0 && outsidePlannedRangeDifferenceCount == 0,
    "The disposable BIN contains a physical byte difference outside its one planned XYZ range.");
Assert(
    string.Equals(sourceSha256Before, Sha256File(sourceImagePath), StringComparison.OrdinalIgnoreCase),
    "The clean retail source BIN changed while creating the disposable candidate.");
Assert(File.Exists(result.OutputCuePath), "The focused candidate CUE is missing.");

var proof = new
{
    generatedAt = DateTimeOffset.UtcNow,
    status = "RUNTIME-CANDIDATE / RESEARCH-ONLY / PATH-ONLY",
    sourceImagePath,
    sourceSha256 = sourceSha256Before,
    sourcePreserved = true,
    outputImagePath = result.OutputImagePath,
    outputCuePath = result.OutputCuePath,
    outputPlanPath = result.OutputPlanPath,
    level = stoneHill.DisplayName,
    ownerTrueIndex = TargetTrueIndex,
    pathNodeCount = path.NodeCount,
    editedNodeIndexZeroBased = currentNode.Index,
    editedNodeNumberOneBased = currentNode.Index + 1,
    rawDelta = new { x = RawXDelta, y = 0, z = 0 },
    editorUnitDelta = new { x = RawXDelta / 16f, y = 0f, z = 0f },
    patchCount = result.Plan.PatchCount,
    patchBytes = result.Plan.TotalPatchedBytes,
    physicalDifferingByteCount = physicalDifferenceCount,
    physicalDifferencesOutsidePlannedRange = outsidePlannedRangeDifferenceCount,
    exactFinalReadback = true,
    normalCreateBinBlockedPendingRuntimeEvidence = true,
    requiredTestCondition = "Disable all DuckStation cheats, especially Have All Dragon Eggs."
};
await File.WriteAllTextAsync(
    proofPath,
    JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));

await File.WriteAllTextAsync(
    checklistPath,
    $"""
    # Stone Hill T166 egg-thief path runtime candidate

    This is a **disposable, path-only research CUE**. It changes exactly the three
    XYZ words for T166 path node {currentNode.Index + 1}; only X differs, by
    {RawXDelta} raw units ({RawXDelta / 16f:0.###} editor/world unit).

    ## Before launching

    - Disable **all DuckStation cheats** for SCUS-94228.
    - In particular, **Have All Dragon Eggs must be OFF**.
    - Cold boot this CUE; do not load a save state created with the earlier combined build.

    ## Runtime checks

    1. Enter Stone Hill normally and approach the egg thief nearest T166.
    2. Confirm the thief activates without malformed polygons, a freeze, or a reset.
    3. Follow it for at least one complete loop so the route wraps from its last node to its first.
    4. Catch the thief and confirm the egg is awarded once.
    5. Die/reload, then leave and re-enter Stone Hill and confirm nearby actors remain normal.

    Passing static readback is not promotion. Record the DuckStation result before
    normal Create BIN is allowed to export edited egg-thief paths.
    """);

Console.WriteLine("PASS: normal Create BIN failed closed and targeted Stone Hill T166 pending runtime proof.");
Console.WriteLine("PASS: research override wrote exactly one guarded 12-byte XYZ patch with exact final-BIN readback.");
Console.WriteLine($"PASS: all {physicalDifferenceCount} physical source/output byte difference(s) are inside that one planned range.");
Console.WriteLine($"CUE: {result.OutputCuePath}");
Console.WriteLine($"Proof: {proofPath}");
Console.WriteLine($"Checklist: {checklistPath}");

static byte[] ReadImageBytes(string imagePath, string imageOffset, int length)
{
    long offset = ParseOffset(imageOffset);
    byte[] bytes = new byte[length];
    using FileStream stream = File.OpenRead(imagePath);
    stream.Position = offset;
    stream.ReadExactly(bytes);
    return bytes;
}

static (long DifferenceCount, long OutsidePlannedRangeCount) CountPhysicalDifferences(
    string sourcePath,
    string outputPath,
    long plannedOffset,
    int plannedLength)
{
    const int BufferSize = 1024 * 1024;
    byte[] sourceBuffer = new byte[BufferSize];
    byte[] outputBuffer = new byte[BufferSize];
    using FileStream source = File.OpenRead(sourcePath);
    using FileStream output = File.OpenRead(outputPath);
    if (source.Length != output.Length)
        throw new InvalidOperationException("The path-only candidate changed the retail image length.");

    long differences = 0;
    long outside = 0;
    long absoluteOffset = 0;
    while (absoluteOffset < source.Length)
    {
        int requested = (int)Math.Min(BufferSize, source.Length - absoluteOffset);
        source.ReadExactly(sourceBuffer.AsSpan(0, requested));
        output.ReadExactly(outputBuffer.AsSpan(0, requested));
        for (int index = 0; index < requested; index++)
        {
            if (sourceBuffer[index] == outputBuffer[index])
                continue;
            long differenceOffset = absoluteOffset + index;
            differences++;
            if (differenceOffset < plannedOffset ||
                differenceOffset >= plannedOffset + plannedLength)
            {
                outside++;
            }
        }
        absoluteOffset += requested;
    }

    return (differences, outside);
}

static long ParseOffset(string value)
{
    string text = value.Trim();
    return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        ? Convert.ToInt64(text[2..], 16)
        : Convert.ToInt64(text);
}

static byte[] ParseHex(string text) =>
    text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(part => Convert.ToByte(part, 16))
        .ToArray();

static string Sha256File(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

if (args.Length < 1 || !File.Exists(args[0]))
    throw new InvalidOperationException("Pass a clean Spyro the Dragon (USA) BIN as the first argument.");

string sourceImagePath = Path.GetFullPath(args[0]);
string workspacePath = args.Length >= 2 ? Path.GetFullPath(args[1]) : FindWorkspaceRoot();
LevelCatalog catalog = LevelCatalog.Load(workspacePath);
string[] levelKeys =
[
    "stonehill",
    "townsquare",
    "peacekeepers",
    "drycanyon",
    "clifftown",
    "magiccrafters",
    "alpineridge",
    "highcaves",
    "wizardpeak"
];
LevelDefinition[] levels = levelKeys
    .Select(key => catalog.FindByKey(key) ?? throw new InvalidOperationException($"Missing level '{key}' from catalog."))
    .ToArray();

IReadOnlyList<NativeMobyPath> paths = EggThiefPathLocator.Locate(sourceImagePath, levels);
Dictionary<(string Level, int TrueIndex), int> expectedNodeCounts = new()
{
    [("stonehill", 166)] = 13,
    [("townsquare", 88)] = 13,
    [("peacekeepers", 44)] = 12,
    [("drycanyon", 87)] = 13,
    [("clifftown", 146)] = 10,
    [("magiccrafters", 0)] = 8,
    [("magiccrafters", 14)] = 1,
    [("alpineridge", 43)] = 17,
    [("highcaves", 17)] = 16,
    [("highcaves", 134)] = 1,
    [("wizardpeak", 26)] = 1,
    [("wizardpeak", 139)] = 16
};

Assert(paths.Count == 12, $"Expected 12 native egg-thief paths, found {paths.Count}.");
Assert(paths.Sum(path => path.NodeCount) == 121, "Expected 121 total retail egg-thief path nodes.");
foreach (NativeMobyPath path in paths)
{
    (string, int) key = (path.LevelKey, path.OwnerTrueIndex);
    Assert(expectedNodeCounts.TryGetValue(key, out int expected), $"Unexpected owner {path.LevelKey} T{path.OwnerTrueIndex}.");
    Assert(path.NodeCount == expected, $"{path.LevelName} T{path.OwnerTrueIndex} node count changed.");
    Assert(path.OwnerNativeClass == EggThiefPathLocator.EggThiefNativeClass, "Non-thief path escaped the locator.");
    Assert(path.PropertiesPointer > 0, $"{path.LevelName} T{path.OwnerTrueIndex} did not decode m_Props from record +0x00.");
    Assert(
        path.PathPointer == path.PropertiesPointer + EggThiefPathLocator.EggThiefPathOffsetInProperties,
        $"{path.LevelName} T{path.OwnerTrueIndex} no longer uses the verified m_Props+0x5C wrapper.");
    Assert(path.Nodes.All(node => node.CoordinateWadOffset >= path.PathWadOffset + 8), "Node offset precedes PathData nodes.");
    Assert(
        NativeMobyPathTraversalProfileRegistry.Resolve(path)?.ClosingTraversal ==
            NativeMobyPathClosingTraversal.CyclicForwardOrReverse,
        $"{path.LevelName} T{path.OwnerTrueIndex} lost its disassembly-proven cyclic traversal profile.");
}

NativeMobyPathPatchPlan noEditPlan = NativeMobyPathPatchExporter.BuildPlan(paths);
Assert(noEditPlan.Patches.Count == 0, "An untouched retail path set did not plan as a byte-identical no-op.");

NativeMobyPath editedPath = paths.Single(path => path.LevelKey == "townsquare" && path.OwnerTrueIndex == 88);
NativePathNode editedNode = editedPath.Nodes[0];
editedNode.SetRawPosition(checked(editedNode.OriginalRawX + 16), editedNode.OriginalRawY, editedNode.OriginalRawZ);
NativeMobyPathPatchPlan patchPlan = NativeMobyPathPatchExporter.BuildPlan(paths);
Assert(patchPlan.EditedPathCount == 1 && patchPlan.EditedNodeCount == 1, "One node edit did not produce one path patch.");
NativeMobyPathCoordinatePatch patch = patchPlan.Patches.Single();
Assert(patch.ByteLength == 12 && patch.Before.Length == 12, "Path patch is not XYZ-only.");
Assert(patch.WadOffset == editedNode.CoordinateWadOffset, "Path patch targets the wrong native node offset.");
Assert(BinaryPrimitives.ReadInt32LittleEndian(patch.After.AsSpan(0, 4)) == editedNode.OriginalRawX + 16, "Edited X was not planned.");
Assert(patch.Before.AsSpan(4, 8).SequenceEqual(patch.After.AsSpan(4, 8)), "Unedited Y/Z bytes changed.");

string tempDirectory = Path.Combine(Path.GetTempPath(), $"spyro-native-path-smoke-{Guid.NewGuid():N}");
Directory.CreateDirectory(tempDirectory);
try
{
    string noEditImagePath = Path.Combine(tempDirectory, "no-edit-byte-identity.bin");
    byte[] noEditBefore = Enumerable.Range(0, 257).Select(index => (byte)(index * 37)).ToArray();
    await File.WriteAllBytesAsync(noEditImagePath, noEditBefore);
    NativeMobyPathPatchExporter.ApplyToImage(noEditImagePath, noEditPlan);
    Assert(
        (await File.ReadAllBytesAsync(noEditImagePath)).SequenceEqual(noEditBefore),
        "Applying an empty native path plan changed the target bytes.");

    string sourceCachePath = Path.Combine(tempDirectory, "townsquare-mobys.json");
    LevelDefinition townSquare = levels.Single(level => level.Key == "townsquare");
    await SourceMobyCacheBuilder.BuildAsync(sourceImagePath, townSquare, sourceCachePath);
    Moby cachedThief = MobyLoader.LoadCached(sourceCachePath).Single(moby => moby.TrueIndex == 88);
    Assert(cachedThief.PropertiesPointer == editedPath.PropertiesPointer, "Source cache did not preserve record +0x00 as PropertiesPointer.");
    Assert(cachedThief.PropertiesPointer != cachedThief.SpecialDataPointer, "PropertiesPointer was aliased back to legacy +0x08 data.");

    string storePath = Path.Combine(tempDirectory, "townsquare-native-moby-path-edits.json");
    int saved = await NativeMobyPathEditStore.SaveAsync(storePath, "Town Square", paths);
    Assert(saved == 1, "Versioned path store did not save one edited path.");
    using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(storePath)))
    {
        Assert(document.RootElement.GetProperty("format").GetString() == NativeMobyPathEditStore.Format, "Path store format tag changed.");
        Assert(document.RootElement.GetProperty("version").GetInt32() == NativeMobyPathEditStore.CurrentVersion, "Path store version changed.");
    }

    IReadOnlyList<NativeMobyPath> reloadedPaths = EggThiefPathLocator.Locate(sourceImagePath, levels);
    NativeMobyPathEditLoadResult load = NativeMobyPathEditStore.Load(storePath, reloadedPaths);
    Assert(load.AppliedPathCount == 1 && load.AppliedNodeCount == 1 && load.BlockedReasons.Count == 0, "Saved node edit did not reload cleanly.");
    NativePathNode reloaded = reloadedPaths.Single(path => path.LevelKey == "townsquare" && path.OwnerTrueIndex == 88).Nodes[0];
    Assert(reloaded.RawX == reloaded.OriginalRawX + 16, "Reloaded path node lost its edited X.");
    NativeMobyPathPatchPlan reloadedPlan = NativeMobyPathPatchExporter.BuildPlan(reloadedPaths);
    Assert(reloadedPlan.Patches.Count == 1 && reloadedPlan.Patches[0].After.SequenceEqual(patch.After), "Reloaded edit changed patch bytes.");

    string staleStorePath = Path.Combine(tempDirectory, "townsquare-stale-native-moby-path-edits.json");
    JsonObject staleDocument = JsonNode.Parse(await File.ReadAllTextAsync(storePath))!.AsObject();
    staleDocument["edits"]!.AsArray()[0]!.AsObject()["originalPathSha256"] = new string('0', 64);
    await File.WriteAllTextAsync(staleStorePath, staleDocument.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

    string nativeMobyManifestPath = Path.Combine(tempDirectory, "townsquare-native-moby-edits.json");
    await File.WriteAllTextAsync(nativeMobyManifestPath, """{"editCount":0,"edits":[]}""");
    MobySourcePatchPlan staleSourcePlan = MobySourcePatchExporter.BuildPlan(
        sourceImagePath,
        sourceCuePath: "",
        outputImagePath: Path.Combine(tempDirectory, "stale-path-output.bin"),
        outputCuePath: Path.Combine(tempDirectory, "stale-path-output.cue"),
        townSquare,
        nativeMobyManifestPath,
        nativeMobyPathEditsPath: staleStorePath);
    MobySourceEditOutcome staleOutcome = staleSourcePlan.EditOutcomes!
        .Single(outcome => string.Equals(outcome.EditKind, "native-path", StringComparison.Ordinal));
    Assert(staleOutcome.EditorTrueIndex == 88, "Stale path rejection lost its owning thief T88 target.");
    Assert(staleOutcome.SkippedReasons.Single().Contains("no longer matches", StringComparison.OrdinalIgnoreCase), "Stale path rejection lost its blocking preimage reason.");

    MobyBuildSafetyLevelReport staleSafety = MobyBuildSafetyInspector.InspectLevel(sourceImagePath, townSquare, staleSourcePlan);
    MobyBuildSafetyIssue staleIssue = staleSafety.Issues.Single(issue =>
        issue.EditorTrueIndex == 88 &&
        issue.Message.Contains("egg-thief path", StringComparison.OrdinalIgnoreCase));
    Assert(staleIssue.Status == MobyBuildSafetyStatus.Blocked, "Stale path preimage did not become a blocked Build Safety issue.");
    Assert(staleIssue.CanNavigate, "Stale path Build Safety issue cannot navigate to the owning thief T88.");
}
finally
{
    Directory.Delete(tempDirectory, recursive: true);
}

Console.WriteLine($"PASS: decoded {paths.Count} egg-thief paths / {paths.Sum(path => path.NodeCount)} nodes from {Path.GetFileName(sourceImagePath)}.");
Console.WriteLine("PASS: PropertiesPointer uses Moby +0x00; legacy SpecialDataPointer remains independent.");
Console.WriteLine("PASS: untouched paths produce an empty plan and applying it is byte-identical.");
Console.WriteLine("PASS: version-1 persistence round-trips and patch planning writes exactly 12 XYZ bytes per edited node.");
Console.WriteLine("PASS: stale path preimages are blocked and Build Safety targets the owning thief T88.");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static string FindWorkspaceRoot()
{
    DirectoryInfo? directory = new(AppContext.BaseDirectory);
    while (directory != null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "spyro-level-catalog.json")))
            return directory.FullName;
        directory = directory.Parent;
    }
    throw new InvalidOperationException("Could not locate spyro-level-catalog.json; pass the workspace root as the second argument.");
}

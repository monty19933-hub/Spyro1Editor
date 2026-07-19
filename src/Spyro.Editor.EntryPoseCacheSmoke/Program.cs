using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Workspace;

string workspacePath = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal)) ??
    Directory.GetCurrentDirectory();
EditorWorkspace workspace = EditorWorkspace.Find(workspacePath);
LevelCatalog catalog = LevelCatalog.Load(workspace.RootPath);
if (catalog.Levels.Count != PortableLevelEntryPoseCache.ExpectedRetailLevelCount)
{
    throw new InvalidOperationException(
        $"Entry-pose smoke requires the complete {PortableLevelEntryPoseCache.ExpectedRetailLevelCount}-level retail catalog; found {catalog.Levels.Count}.");
}

string sourceImage = DiscImageLocator.FindImage(workspace);
if (!File.Exists(sourceImage))
    throw new InvalidOperationException("Entry-pose smoke requires the configured retail Spyro BIN.");

string temporaryRoot = Path.Combine(Path.GetTempPath(), $"spyro-entry-pose-cache-smoke-{Guid.NewGuid():N}");
string temporaryCache = Path.Combine(temporaryRoot, "editor-cache");
Directory.CreateDirectory(temporaryCache);
try
{
    PortableLevelEntryPoseCacheBuildResult build = await PortableLevelEntryPoseCache.BuildAsync(
        sourceImage,
        temporaryCache,
        catalog,
        overwrite: true);
    if (build.ReusedExistingCache ||
        build.LevelCount != PortableLevelEntryPoseCache.ExpectedRetailLevelCount ||
        !File.Exists(Path.Combine(temporaryCache, PortableLevelEntryPoseCache.FileName)))
    {
        throw new InvalidOperationException("The focused build did not create one complete entry-pose sidecar.");
    }

    if (Directory.EnumerateFiles(temporaryRoot, "*.bin", SearchOption.AllDirectories).Any() ||
        Directory.EnumerateFiles(temporaryRoot, "source-disc.json", SearchOption.AllDirectories).Any())
    {
        throw new InvalidOperationException("The supposedly portable smoke workspace contains a source BIN or source-disc pointer.");
    }

    if (!PortableLevelEntryPoseCache.TryLoadComplete(
            temporaryRoot,
            catalog,
            out PortableLevelEntryPoseCacheSnapshot snapshot,
            out string loadError))
    {
        throw new InvalidOperationException($"The generated all-level entry-pose sidecar did not load: {loadError}");
    }

    if (snapshot.Contract != PortableLevelEntryPoseCache.Contract ||
        snapshot.FormatVersion != PortableLevelEntryPoseCache.FormatVersion ||
        snapshot.Poses.Count != PortableLevelEntryPoseCache.ExpectedRetailLevelCount ||
        snapshot.SourceImageByteLength != new FileInfo(sourceImage).Length ||
        snapshot.SourceImageSha256.Length != 64)
    {
        throw new InvalidOperationException("The loaded entry-pose cache lost its contract, completeness, or source provenance.");
    }

    foreach (LevelDefinition level in catalog.Levels)
    {
        if (!snapshot.TryGetPose(level.Key, out PortableLevelEntryPose cached))
            throw new InvalidOperationException($"Key lookup did not return {level.DisplayName}.");

        FlyInLandingData retail = FlyInLandingLocator.Locate(sourceImage, level);
        if (cached.LevelId != level.LevelId ||
            cached.SourceWadEntry != level.SourceWadEntry ||
            cached.WadOffset != retail.WadOffset ||
            cached.RawX != retail.RawX ||
            cached.RawY != retail.RawY ||
            cached.RawZ != retail.RawZ ||
            cached.YawByte != retail.YawByte ||
            cached.EntryDataByteLength != retail.EntryDataByteLength)
        {
            throw new InvalidOperationException($"{level.DisplayName}'s portable pose differs from its retail entry record.");
        }
    }

    LevelDefinition[] homeworlds = catalog.Levels.Where(level => level.LevelId % 10 == 0).ToArray();
    if (homeworlds.Length != 6 || homeworlds.Any(level => !snapshot.TryGetPose(level.Key, out _)))
        throw new InvalidOperationException("The portable entry-pose cache does not include all six homeworlds.");

    if (!PortableLevelEntryPoseCache.TryLoadPose(
            temporaryRoot,
            catalog,
            "Magic Crafters",
            out PortableLevelEntryPose normalizedLookup,
            out string lookupError) ||
        normalizedLookup.LevelKey != "magiccrafters")
    {
        throw new InvalidOperationException($"Normalized key lookup failed: {lookupError}");
    }

    PortableLevelEntryPoseCacheBuildResult portableReuse = await PortableLevelEntryPoseCache.BuildAsync(
        Path.Combine(temporaryRoot, "deliberately-missing-source.bin"),
        temporaryCache,
        catalog,
        overwrite: false);
    if (!portableReuse.ReusedExistingCache || portableReuse.LevelCount != snapshot.Poses.Count)
        throw new InvalidOperationException("A complete sidecar was not reusable after the original source BIN became unavailable.");

    IReadOnlyDictionary<string, int> expectedGroups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["artisans"] = 17,
        ["peacekeepers"] = 0,
        ["magiccrafters"] = 0,
        ["dreamweavers"] = 15
    };
    foreach ((string levelKey, int expectedGroup) in expectedGroups)
    {
        PortableLevelEntryPose pose = snapshot.TryGetPose(levelKey, out PortableLevelEntryPose cached)
            ? cached
            : throw new InvalidOperationException($"Known-group fixture is missing cached pose {levelKey}.");
        string overlayPath = Path.Combine(
            workspace.RootPath,
            "editor-cache",
            $"{levelKey}-runtime-scene-editor-overlay.json");
        if (!File.Exists(overlayPath))
            throw new InvalidOperationException($"Known-group fixture is missing portable overlay {overlayPath}.");

        GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
        NativeTerrainOcclusionData occlusion = geometry.NativeTerrainOcclusion ??
            throw new InvalidOperationException($"{levelKey} has no native terrain occlusion payload.");
        if (!occlusion.TryResolveGroup(
                pose.X,
                pose.Y,
                pose.Z + 220.0,
                out int actualGroup,
                out int triangleIndex,
                out double floorZ) ||
            actualGroup != expectedGroup)
        {
            throw new InvalidOperationException(
                $"{levelKey} retail entry resolved group {actualGroup}, expected {expectedGroup}; triangle {triangleIndex}, floor Z {floorZ:0.###}.");
        }

        Console.WriteLine(
            $"{levelKey}: raw ({pose.RawX}, {pose.RawY}, {pose.RawZ}), yaw {pose.YawByte}, " +
            $"entry-camera group {actualGroup}, triangle {triangleIndex}.");
    }

    string sidecarPath = Path.Combine(temporaryCache, PortableLevelEntryPoseCache.FileName);
    byte[] completeSidecar = File.ReadAllBytes(sidecarPath);
    JsonObject incomplete = JsonNode.Parse(completeSidecar)?.AsObject() ??
        throw new InvalidOperationException("Could not parse the generated entry-pose sidecar for completeness testing.");
    JsonArray rows = incomplete["levels"]?.AsArray() ??
        throw new InvalidOperationException("The generated entry-pose sidecar has no levels array.");
    rows.RemoveAt(rows.Count - 1);
    File.WriteAllText(sidecarPath, incomplete.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    if (PortableLevelEntryPoseCache.TryLoadComplete(temporaryRoot, catalog, out _, out string incompleteError) ||
        !incompleteError.Contains("34/35", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Completeness validation accepted a missing level or returned an unclear diagnostic: {incompleteError}");
    }
    File.WriteAllBytes(sidecarPath, completeSidecar);

    Console.WriteLine(
        $"Portable entry-pose cache smoke passed: {snapshot.Poses.Count}/35 exact retail poses, " +
        $"{homeworlds.Length}/6 homeworlds, normalized key lookup, source-free reuse, strict completeness, and 4/4 known occlusion groups.");
}
finally
{
    try
    {
        Directory.Delete(temporaryRoot, recursive: true);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
    }
}

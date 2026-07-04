using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Cache;

public sealed record PortableEditorCacheResult(int LevelCount, int MobyCacheCount, int OverlayCacheCount, string CachePath, bool ReusedExistingCache = false);

public static class PortableEditorCacheBuilder
{
    public static async Task<PortableEditorCacheResult> BuildAsync(
        EditorWorkspace workspace,
        LevelCatalog catalog,
        bool overwrite = true,
        bool fastReuseExistingCache = false,
        CancellationToken cancellationToken = default)
    {
        string cacheDir = Path.Combine(workspace.RootPath, "editor-cache");
        Directory.CreateDirectory(cacheDir);

        if (!overwrite && fastReuseExistingCache)
        {
            PortableEditorCacheResult? existing = await TryReuseCompleteExistingCacheAsync(catalog, cacheDir, cancellationToken);
            if (existing != null)
                return existing;
        }

        await EnsureWadAnalysisAsync(workspace, cancellationToken);

        List<object> summary = new();
        int mobyCacheCount = 0;
        int overlayCacheCount = 0;

        foreach (LevelDefinition level in catalog.Levels)
        {
            string mobyCacheFile = Path.Combine(cacheDir, $"{level.Key}-mobys.json");
            string overlayCacheFile = Path.Combine(cacheDir, $"{level.Key}-runtime-scene-editor-overlay.json");
            string overlayHealth = "";

            bool hasMobyCache = File.Exists(mobyCacheFile) && !overwrite;
            if (!hasMobyCache)
            {
                if (TryBuildSourceMobyCache(workspace, level, mobyCacheFile))
                {
                    hasMobyCache = true;
                }
                else
                {
                    string ramPath = FindRamPath(workspace, level.Key);
                    if (File.Exists(ramPath))
                    {
                        IReadOnlyList<Moby> mobys = MobyLoader.LoadRamDump(ramPath);
                        await WriteMobyCacheAsync(mobyCacheFile, level, ramPath, mobys, cancellationToken);
                        hasMobyCache = true;
                    }
                }
            }

            bool hasOverlay = File.Exists(overlayCacheFile) && !overwrite;
            if (!hasOverlay)
            {
                string overlayPath = workspace.ResolveFile($"{level.Key}-runtime-scene-editor-overlay.json", "generated-research");
                if (File.Exists(overlayPath))
                {
                    GeometryCacheHealthIssue? sourceIssue = ShouldInspectExistingOverlay(level.Key)
                        ? GeometryCacheHealth.InspectOverlay(level.Key, overlayPath)
                        : null;
                    if (sourceIssue?.BlocksLoading == true)
                    {
                        overlayHealth = sourceIssue.Message;
                    }
                    else
                    {
                        File.Copy(overlayPath, overlayCacheFile, true);
                        hasOverlay = true;
                    }
                }
            }
            if (hasOverlay)
            {
                GeometryCacheHealthIssue? cacheIssue = ShouldInspectExistingOverlay(level.Key)
                    ? GeometryCacheHealth.InspectOverlay(level.Key, overlayCacheFile)
                    : null;
                if (cacheIssue?.BlocksLoading == true)
                {
                    hasOverlay = false;
                    overlayHealth = cacheIssue.Message;
                }
            }
            if (!hasOverlay && TryBuildSourceOverlay(workspace, level, overlayCacheFile, out string sourceOverlayHealth))
            {
                hasOverlay = true;
                overlayHealth = sourceOverlayHealth;
            }

            if (hasMobyCache)
                mobyCacheCount++;
            if (hasOverlay)
                overlayCacheCount++;

            summary.Add(BuildSummaryRow(level, hasMobyCache, hasOverlay, overlayHealth));
        }

        await WriteIndexAsync(cacheDir, catalog, summary, cancellationToken);
        return new PortableEditorCacheResult(catalog.Levels.Count, mobyCacheCount, overlayCacheCount, cacheDir);
    }

    private static async Task<PortableEditorCacheResult?> TryReuseCompleteExistingCacheAsync(
        LevelCatalog catalog,
        string cacheDir,
        CancellationToken cancellationToken)
    {
        List<object> summary = new();
        int mobyCacheCount = 0;
        int overlayCacheCount = 0;
        foreach (LevelDefinition level in catalog.Levels)
        {
            string mobyCacheFile = Path.Combine(cacheDir, $"{level.Key}-mobys.json");
            string overlayCacheFile = Path.Combine(cacheDir, $"{level.Key}-runtime-scene-editor-overlay.json");
            bool hasMobyCache = File.Exists(mobyCacheFile);
            bool hasOverlay = File.Exists(overlayCacheFile);
            string overlayHealth = "";
            if (!hasMobyCache || !hasOverlay)
                return null;

            if (ShouldInspectExistingOverlay(level.Key))
            {
                GeometryCacheHealthIssue? cacheIssue = GeometryCacheHealth.InspectOverlay(level.Key, overlayCacheFile);
                if (cacheIssue?.BlocksLoading == true)
                    return null;

                overlayHealth = cacheIssue?.Message ?? "";
            }

            mobyCacheCount++;
            overlayCacheCount++;
            summary.Add(BuildSummaryRow(level, hasMobyCache, hasOverlay, overlayHealth));
        }

        await WriteIndexAsync(cacheDir, catalog, summary, cancellationToken);
        return new PortableEditorCacheResult(catalog.Levels.Count, mobyCacheCount, overlayCacheCount, cacheDir, ReusedExistingCache: true);
    }

    private static bool ShouldInspectExistingOverlay(string levelKey)
    {
        return string.Equals(levelKey, "gnastysloot", StringComparison.OrdinalIgnoreCase);
    }

    private static object BuildSummaryRow(LevelDefinition level, bool hasMobyCache, bool hasOverlay, string overlayHealth)
    {
        return new
        {
            level.Key,
            level.DisplayName,
            hasMobyCache,
            hasOverlay,
            overlayHealth,
            mobyCache = hasMobyCache ? $"editor-cache/{level.Key}-mobys.json" : "",
            overlayCache = hasOverlay ? $"editor-cache/{level.Key}-runtime-scene-editor-overlay.json" : ""
        };
    }

    private static async Task WriteIndexAsync(string cacheDir, LevelCatalog catalog, IReadOnlyList<object> summary, CancellationToken cancellationToken)
    {
        var index = new
        {
            generatedBy = "Spyro.Editor.Core",
            generatedAt = DateTime.Now.ToString("o"),
            purpose = "Portable editor cache index for the Mac/Windows native editor.",
            levelCount = catalog.Levels.Count,
            levels = summary
        };

        await using FileStream indexStream = File.Create(Path.Combine(cacheDir, "index.json"));
        await JsonSerializer.SerializeAsync(indexStream, index, cancellationToken: cancellationToken);
    }

    private static async Task EnsureWadAnalysisAsync(EditorWorkspace workspace, CancellationToken cancellationToken)
    {
        string sourceImage = DiscImageLocator.FindImage(workspace);
        if (!File.Exists(sourceImage))
            return;

        string wadAnalysis = WadAnalysisLocator.Find(workspace);
        if (File.Exists(wadAnalysis))
            return;

        await WadAnalysisBuilder.BuildAsync(sourceImage, Path.Combine(workspace.RootPath, "spyro-wad-analysis.json"), cancellationToken: cancellationToken);
    }

    private static string FindRamPath(EditorWorkspace workspace, string levelKey)
    {
        string[] candidates = levelKey == "stonehill"
            ? ["stonehill-before-clean.bin", "stonehill-before-gem-clean.bin", "duckstation-mainram-fresh-stonehill.bin"]
            : [$"{levelKey}-before-clean.bin"];

        foreach (string candidate in candidates)
        {
            string path = workspace.ResolveFile(candidate, "game-and-capture-artifacts");
            if (File.Exists(path))
                return path;
        }

        return Path.Combine(workspace.RootPath, candidates[0]);
    }

    private static bool TryBuildSourceMobyCache(EditorWorkspace workspace, LevelDefinition level, string outputPath)
    {
        if (!level.HasSourceTable)
            return false;

        string sourceImage = DiscImageLocator.FindImage(workspace);
        if (!File.Exists(sourceImage))
            return false;

        try
        {
            SourceMobyCacheBuilder.BuildAsync(sourceImage, level, outputPath).GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException or EndOfStreamException)
        {
            return false;
        }
    }

    private static bool TryBuildSourceOverlay(EditorWorkspace workspace, LevelDefinition level, string outputPath, out string overlayHealth)
    {
        overlayHealth = "";
        if (level.SourceWadEntry < 0)
            return false;

        string sourceImage = DiscImageLocator.FindImage(workspace);
        string wadAnalysis = WadAnalysisLocator.Find(workspace);
        if (!File.Exists(sourceImage) || !File.Exists(wadAnalysis))
            return false;

        try
        {
            SourceSceneOverlayResult result = SourceSceneOverlayExporter.Export(sourceImage, wadAnalysis, level, outputPath);
            overlayHealth = $"Recovered from source WAD entry {result.WadEntry}: {result.SectorCount} sectors, {result.PolygonCount} faces.";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException or EndOfStreamException)
        {
            overlayHealth = $"Source-derived overlay recovery failed: {ex.Message}";
            return false;
        }
    }

    private static async Task WriteMobyCacheAsync(string path, LevelDefinition level, string ramPath, IReadOnlyList<Moby> mobys, CancellationToken cancellationToken)
    {
        var root = new
        {
            generatedBy = "Spyro.Editor.Core",
            generatedAt = DateTime.Now.ToString("o"),
            purpose = "Portable native editor moby cache decoded from a clean DuckStation RAM capture.",
            levelKey = level.Key,
            displayName = level.DisplayName,
            levelId = level.LevelId,
            sourceRamFile = Path.GetFileName(ramPath),
            recordStride = "0x58",
            mobyCount = mobys.Count,
            mobys = mobys.Select(moby => new
            {
                moby.Index,
                moby.TrueIndex,
                moby.LegacyIndex,
                x = Math.Round(moby.Position.X, 4),
                y = Math.Round(moby.Position.Y, 4),
                z = Math.Round(moby.Position.Z, 4),
                rawX = ToRawCoordinate(moby.Position.X),
                rawY = ToRawCoordinate(moby.Position.Y),
                rawZ = ToRawCoordinate(moby.Position.Z),
                typeHex = $"0x{moby.Type:X2}",
                stateHex = $"0x{moby.State:X2}",
                runtimeAddress = $"0x{moby.RuntimeAddress:X8}",
                specialDataPointer = $"0x{moby.SpecialDataPointer:X8}",
                sourceByte36Hex = $"0x{moby.SourceByte36:X2}",
                sourceByte37Hex = $"0x{moby.SourceByte37:X2}",
                sourceByte4FHex = $"0x{moby.SourceByte4F:X2}",
                flag4AHex = $"0x{moby.Flag4A:X2}",
                flag4BHex = $"0x{moby.Flag4B:X2}"
            })
        };

        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, root, cancellationToken: cancellationToken);
    }

    private static int ToRawCoordinate(float value)
    {
        return (int)Math.Round(value * 16f);
    }
}

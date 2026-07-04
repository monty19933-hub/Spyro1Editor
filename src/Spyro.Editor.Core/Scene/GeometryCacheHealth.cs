using System.Text.Json;
using Spyro.Editor.Core;

namespace Spyro.Editor.Core.Scene;

public sealed record GeometryCacheHealthIssue(
    string LevelKey,
    string OverlayPath,
    string Severity,
    string Message,
    bool BlocksLoading);

public static class GeometryCacheHealth
{
    public static GeometryCacheHealthIssue? InspectOverlay(string levelKey, string overlayPath)
    {
        if (!File.Exists(overlayPath))
            return null;

        try
        {
            using FileStream stream = File.OpenRead(overlayPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (IsKnownBadGnastysLootOverlay(levelKey, document.RootElement))
            {
                return new GeometryCacheHealthIssue(
                    levelKey,
                    overlayPath,
                    "bad-capture",
                    "Gnasty's Loot terrain cache matches the Gnasty's World homeworld capture. The editor is hiding this terrain until a true Gnasty's Loot RAM capture or source-derived overlay is available.",
                    BlocksLoading: true);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static bool IsKnownBadGnastysLootOverlay(string levelKey, JsonElement root)
    {
        if (!string.Equals(levelKey, "gnastysloot", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!root.TryGetProperty("candidates", out JsonElement candidates) || candidates.ValueKind != JsonValueKind.Array)
            return false;

        JsonElement candidate = candidates.EnumerateArray().FirstOrDefault();
        if (candidate.ValueKind == JsonValueKind.Undefined)
            return false;

        string sceneAddress = JsonValue.GetString(candidate, "sceneRuntimeAddress");
        int sectorCount = JsonValue.GetInt32(candidate, "sectorCount", -1);
        int validPolygons = JsonValue.GetInt32(candidate, "validPolygons", -1);
        if (!string.Equals(sceneAddress, "0x800873E4", StringComparison.OrdinalIgnoreCase)
            || sectorCount != 115
            || validPolygons != 3435)
        {
            return false;
        }

        if (!candidate.TryGetProperty("projectedBounds", out JsonElement bounds) || bounds.ValueKind != JsonValueKind.Object)
            return false;

        return JsonValue.GetInt32(bounds, "minX", -1) == 415
            && JsonValue.GetInt32(bounds, "maxX", -1) == 9880
            && JsonValue.GetInt32(bounds, "minY", -1) == 420
            && JsonValue.GetInt32(bounds, "maxY", -1) == 9875;
    }
}

using System.Text.Json;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Scene;

public static class TerrainMaterialClassifier
{
    private static readonly Dictionary<string, Dictionary<int, string>> BuiltInOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["stonehill"] = new Dictionary<int, string>
        {
            [30] = "sand",
            [32] = "water"
        },
        ["artisans"] = new Dictionary<int, string>
        {
            [39] = "grass",
            [64] = "grass"
        },
        ["peacekeepers"] = new Dictionary<int, string>
        {
            [0] = "water",
            [1] = "water",
            [2] = "water",
            [12] = "stone",
            [13] = "stone",
            [14] = "stone",
            [15] = "stone",
            [16] = "stone",
            [18] = "stone",
            [19] = "stone",
            [23] = "stone",
            [24] = "stone",
            [25] = "stone",
            [26] = "stone",
            [30] = "cliff",
            [31] = "cliff",
            [32] = "cliff",
            [33] = "cliff",
            [34] = "cliff",
            [35] = "cliff",
            [36] = "cliff"
        },
        ["beastmakers"] = new Dictionary<int, string>
        {
            [41] = "ooze"
        },
        ["icecavern"] = new Dictionary<int, string>
        {
            [3] = "ice",
            [4] = "ice",
            [11] = "ice",
            [12] = "ice",
            [14] = "ice",
            [15] = "ice",
            [16] = "ice",
            [17] = "ice",
            [18] = "ice",
            [21] = "ice",
            [23] = "ice",
            [26] = "ice",
            [32] = "ice",
            [33] = "ice",
            [34] = "ice",
            [35] = "ice",
            [36] = "ice",
            [39] = "ice",
            [40] = "ice",
            [41] = "ice",
            [42] = "ice",
            [47] = "ice",
            [50] = "ice",
            [51] = "ice",
            [52] = "ice",
            [53] = "ice",
            [54] = "ice"
        },
        ["clifftown"] = new Dictionary<int, string>
        {
            [35] = "lava",
            [36] = "lava",
            [38] = "lava"
        },
        ["jacques"] = new Dictionary<int, string>
        {
            [52] = "lava",
            [53] = "lava"
        },
        ["toasty"] = new Dictionary<int, string>
        {
            [50] = "lava",
            [51] = "lava",
            [52] = "lava"
        },
        ["gnastysloot"] = new Dictionary<int, string>
        {
            [64] = "water",
            [14] = "water",
            [47] = "water",
            [3] = "water",
            [11] = "water",
            [24] = "lava",
            [68] = "grass",
            [72] = "grass",
            [127] = "grass",
            [28] = "grass",
            [27] = "grass",
            [10] = "grass",
            [61] = "grass",
            [8] = "grass",
            [26] = "grass",
            [25] = "grass",
            [38] = "brick",
            [53] = "brick",
            [45] = "brick",
            [46] = "brick",
            [41] = "brick",
            [42] = "brick",
            [43] = "brick",
            [44] = "brick",
            [39] = "brick",
            [40] = "brick",
            [30] = "brick",
            [32] = "brick",
            [33] = "brick",
            [29] = "brick",
            [34] = "brick",
            [35] = "brick",
            [36] = "brick",
            [48] = "brick",
            [50] = "brick"
        }
    };

    public static int Apply(string levelKey, string workspaceRoot, GeometryCandidate geometry)
    {
        Dictionary<int, string> overrides = LoadOverrides(levelKey, workspaceRoot);
        Dictionary<int, MaterialAssignment> assignments = BuildTextureAssignments(geometry, overrides);
        TerrainBehaviorProofFile behaviorProofs = TerrainBehaviorProofStore.Load(workspaceRoot, levelKey);
        int classified = 0;

        foreach (TerrainPolygon polygon in geometry.Polygons)
        {
            MaterialAssignment assignment = assignments.TryGetValue(polygon.TextureId, out MaterialAssignment value)
                ? value
                : new MaterialAssignment("unknown", "unclassified");
            polygon.SetSurface(assignment.Surface, polygon.FaceColor, assignment.Source);
            TerrainBehaviorRule? behaviorRule = FindBehaviorRule(behaviorProofs, polygon.TextureId, assignment.Surface);
            TerrainBehaviorClassifier.Apply(polygon, behaviorRule);
            classified++;
        }

        return classified;
    }

    public static IReadOnlyDictionary<string, int> CountSurfaces(GeometryCandidate? geometry)
    {
        Dictionary<string, int> counts = new(StringComparer.OrdinalIgnoreCase);
        if (geometry == null)
            return counts;

        foreach (TerrainPolygon polygon in geometry.Polygons)
        {
            string surface = NormalizeSurfaceName(polygon.Surface);
            counts[surface] = counts.TryGetValue(surface, out int count) ? count + 1 : 1;
        }

        return counts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    public static string FormatSurface(string surface)
    {
        surface = NormalizeSurfaceName(surface);
        if (surface == "ooze")
            return "Goo";

        return surface.Length == 0 ? "Unknown" : char.ToUpperInvariant(surface[0]) + surface[1..];
    }

    public static async Task SaveOverrideAsync(string levelKey, string workspaceRoot, int textureId, string surface, CancellationToken cancellationToken = default)
    {
        if (textureId < 0)
            return;

        Dictionary<int, string> overrides = LoadUserOverrides(levelKey, workspaceRoot);
        surface = NormalizeSurfaceName(surface);
        if (surface == "unknown")
            overrides.Remove(textureId);
        else
            overrides[textureId] = surface;

        string path = Path.Combine(workspaceRoot, $"{levelKey}-terrain-material-overrides.json");
        var root = new
        {
            generatedAt = DateTime.Now.ToString("s"),
            editor = "Spyro.Editor.Core",
            levelKey,
            note = "Editor-side terrain material labels keyed by texture ID. These guide native preview/material auditing; behavior still requires collision proof.",
            overrides = overrides
                .OrderBy(pair => pair.Key)
                .Select(pair => new { textureId = pair.Key, surface = pair.Value })
                .ToList()
        };

        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, root, cancellationToken: cancellationToken);
    }

    private static Dictionary<int, string> LoadOverrides(string levelKey, string workspaceRoot)
    {
        Dictionary<int, string> result = new();
        if (BuiltInOverrides.TryGetValue(levelKey, out Dictionary<int, string>? builtIns))
        {
            foreach ((int textureId, string surface) in builtIns)
                result[textureId] = surface;
        }

        foreach ((int textureId, string surface) in LoadUserOverrides(levelKey, workspaceRoot))
            result[textureId] = surface;

        return result;
    }

    private static TerrainBehaviorRule? FindBehaviorRule(TerrainBehaviorProofFile proofFile, int textureId, string surface)
    {
        string normalizedSurface = NormalizeSurfaceName(surface);
        return proofFile.Rules
            .Where(rule => rule.TextureId == textureId || string.Equals(rule.Surface, normalizedSurface, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(rule => BehaviorConfidenceRank(rule.Confidence))
            .ThenByDescending(rule => rule.ObservationCount)
            .FirstOrDefault();
    }

    private static int BehaviorConfidenceRank(string confidence)
    {
        return confidence switch
        {
            "proven" => 3,
            "observed" => 2,
            "noisy" => 1,
            _ => 0
        };
    }

    private static Dictionary<int, string> LoadUserOverrides(string levelKey, string workspaceRoot)
    {
        Dictionary<int, string> result = new();
        string path = Path.Combine(workspaceRoot, $"{levelKey}-terrain-material-overrides.json");
        if (!File.Exists(path))
            return result;

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("overrides", out JsonElement overrides) || overrides.ValueKind != JsonValueKind.Array)
                return result;

            foreach (JsonElement item in overrides.EnumerateArray())
            {
                int textureId = JsonValue.GetInt32(item, "textureId", -1);
                string surface = NormalizeSurfaceName(JsonValue.GetString(item, "surface"));
                if (textureId >= 0 && surface != "unknown")
                    result[textureId] = surface;
            }
        }
        catch
        {
            return result;
        }

        return result;
    }

    private static Dictionary<int, MaterialAssignment> BuildTextureAssignments(GeometryCandidate geometry, Dictionary<int, string> overrides)
    {
        Dictionary<int, MaterialAssignment> result = new();
        foreach (IGrouping<int, TerrainPolygon> group in geometry.Polygons.GroupBy(polygon => polygon.TextureId))
        {
            if (overrides.TryGetValue(group.Key, out string? overrideSurface))
            {
                result[group.Key] = new MaterialAssignment(NormalizeSurfaceName(overrideSurface), "override");
                continue;
            }

            ColorRgba average = AverageFaceColor(group);
            result[group.Key] = new MaterialAssignment(InferSurface(average), "texture color");
        }

        return result;
    }

    private static ColorRgba AverageFaceColor(IEnumerable<TerrainPolygon> polygons)
    {
        int count = 0;
        int r = 0;
        int g = 0;
        int b = 0;
        foreach (TerrainPolygon polygon in polygons)
        {
            count++;
            r += polygon.FaceColor.R;
            g += polygon.FaceColor.G;
            b += polygon.FaceColor.B;
        }

        if (count == 0)
            return ColorRgba.FromRgb(92, 135, 104);

        return ColorRgba.FromRgb(r / count, g / count, b / count);
    }

    private static string InferSurface(ColorRgba color)
    {
        int max = Math.Max(color.R, Math.Max(color.G, color.B));
        int min = Math.Min(color.R, Math.Min(color.G, color.B));

        if (color.B >= color.R + 32 && color.B >= color.G + 24)
            return "water";

        if (color.R >= 172 && color.R >= color.G + 40 && color.G >= color.B + 20 && color.B <= 105)
            return "lava";

        if (color.G >= color.R + 16 && color.G >= color.B + 16)
            return "grass";

        if (color.R >= 168 && color.G >= 145 && color.B <= 126 && color.R >= color.B + 34)
            return "sand";

        if (max - min <= 24 || max < 115)
            return "stone";

        if (color.R >= color.G && color.G >= color.B && color.R >= color.B + 22)
            return "stone";

        return "ground";
    }

    private static ColorRgba GetSurfaceColor(string surface, ColorRgba fallback)
    {
        return NormalizeSurfaceName(surface) switch
        {
            "grass" => ColorRgba.FromRgb(82, 151, 82),
            "water" => ColorRgba.FromRgb(68, 139, 220),
            "ice" => ColorRgba.FromRgb(116, 170, 215),
            "lava" => ColorRgba.FromRgb(221, 88, 52),
            "ooze" => ColorRgba.FromRgb(74, 149, 89),
            "sand" => ColorRgba.FromRgb(202, 178, 103),
            "stone" => ColorRgba.FromRgb(128, 125, 116),
            "brick" => ColorRgba.FromRgb(158, 98, 62),
            "cliff" => ColorRgba.FromRgb(153, 145, 104),
            "ground" => ColorRgba.FromRgb(145, 116, 85),
            _ => fallback
        };
    }

    public static string NormalizeSurfaceName(string surface)
    {
        surface = (surface ?? "").Trim().ToLowerInvariant();
        return surface switch
        {
            "rock" => "stone",
            "brickwork" => "brick",
            "red stone" => "brick",
            "castle" => "brick",
            "dirt" => "ground",
            "dry dirt" => "ground",
            "dry ground" => "ground",
            "beach" => "sand",
            "sand/beach" => "sand",
            "wall" => "cliff",
            "icy" => "ice",
            "snow" => "ice",
            "goo" => "ooze",
            "acid" => "ooze",
            "swamp" => "ooze",
            "" => "unknown",
            _ => surface
        };
    }

    private readonly record struct MaterialAssignment(string Surface, string Source);
}

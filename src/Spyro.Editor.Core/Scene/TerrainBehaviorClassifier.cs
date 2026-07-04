namespace Spyro.Editor.Core.Scene;

public static class TerrainBehaviorClassifier
{
    public static void Apply(TerrainPolygon polygon, TerrainBehaviorRule? rule = null)
    {
        TerrainBehaviorAssignment assignment = rule == null
            ? Classify(polygon)
            : ClassifyFromRule(rule);
        polygon.SetBehavior(assignment.Behavior, assignment.Source, assignment.Confidence, assignment.Note);
    }

    public static IReadOnlyDictionary<string, int> CountBehaviors(GeometryCandidate? geometry)
    {
        Dictionary<string, int> counts = new(StringComparer.OrdinalIgnoreCase);
        if (geometry == null)
            return counts;

        foreach (TerrainPolygon polygon in geometry.Polygons)
        {
            string behavior = NormalizeBehavior(polygon.Behavior);
            counts[behavior] = counts.TryGetValue(behavior, out int count) ? count + 1 : 1;
        }

        return counts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    public static string FormatBehavior(string behavior)
    {
        behavior = NormalizeBehavior(behavior);
        return behavior switch
        {
            "solid-candidate" => "Solid candidate",
            "solid-observed" => "Solid observed",
            "solid-proven" => "Solid proven",
            "hazard-candidate" => "Hazard candidate",
            "damage-observed" => "Damage observed",
            "damage-proven" => "Damage proven",
            "state-response-observed" => "State response observed",
            "state-response-proven" => "State response proven",
            "steep-or-wall-candidate" => "Steep/wall candidate",
            "unknown" => "Unknown",
            _ => behavior.Length == 0 ? "Unknown" : char.ToUpperInvariant(behavior[0]) + behavior[1..].Replace('-', ' ')
        };
    }

    private static TerrainBehaviorAssignment ClassifyFromRule(TerrainBehaviorRule rule)
    {
        return new TerrainBehaviorAssignment(
            NormalizeBehavior(rule.Behavior),
            "RAM behavior proof",
            string.IsNullOrWhiteSpace(rule.Confidence) ? "observed" : rule.Confidence,
            rule.Note);
    }

    private static TerrainBehaviorAssignment Classify(TerrainPolygon polygon)
    {
        string surface = TerrainMaterialClassifier.NormalizeSurfaceName(polygon.Surface);
        if (surface is "water" or "lava" or "ooze")
        {
            return new TerrainBehaviorAssignment(
                "hazard-candidate",
                "visual material",
                "candidate",
                $"{TerrainMaterialClassifier.FormatSurface(surface)}-looking faces should be treated as possible Spyro damage/death terrain. Stone Hill collision audit shows packed CollTri zFlags do not separate hazards from solid ground, so exact behavior likely lives in another response table/control system.");
        }

        if ((polygon.MaxZ - polygon.MinZ) >= 180f || surface == "cliff")
        {
            return new TerrainBehaviorAssignment(
                "steep-or-wall-candidate",
                "geometry slope",
                "candidate",
                "Large height spread usually behaves like steep terrain or walls, but exact wall/slide behavior still needs response-table proof beyond the packed collision triangle coordinate data.");
        }

        if (surface is "grass" or "sand" or "stone" or "ground" or "ice")
        {
            return new TerrainBehaviorAssignment(
                "solid-candidate",
                "visual material",
                "candidate",
                "Likely ordinary solid ground; exact Spyro response is not proven by the visible terrain words or the currently decoded packed collision zFlags.");
        }

        return new TerrainBehaviorAssignment(
            "unknown",
            "unclassified",
            "unknown",
            "No reliable behavior label yet.");
    }

    private static string NormalizeBehavior(string behavior)
    {
        behavior = (behavior ?? "").Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(behavior) ? "unknown" : behavior;
    }

    private readonly record struct TerrainBehaviorAssignment(string Behavior, string Source, string Confidence, string Note);
}

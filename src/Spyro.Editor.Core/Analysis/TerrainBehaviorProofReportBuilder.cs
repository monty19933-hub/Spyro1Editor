using System.Globalization;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Analysis;

public static class TerrainBehaviorProofReportBuilder
{
    public static async Task<TerrainBehaviorProofReports> BuildAsync(
        EditorWorkspace workspace,
        LevelCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        string cacheDir = Path.Combine(workspace.RootPath, "editor-cache");
        if (!Directory.Exists(cacheDir))
            return new TerrainBehaviorProofReports(
                new TerrainBehaviorProofTargetReport
                {
                    GeneratedAt = DateTimeOffset.Now.ToString("s", CultureInfo.InvariantCulture),
                    Summary = "No editor-cache directory was found; build the portable cache first."
                },
                new TerrainBehaviorProofSummaryReport
                {
                    GeneratedAt = DateTimeOffset.Now.ToString("s", CultureInfo.InvariantCulture),
                    Summary = "No editor-cache directory was found; build the portable cache first."
                });

        List<TerrainBehaviorProofTarget> targets = new();
        List<TerrainBehaviorProofSurfaceSummary> summaries = new();
        foreach (string overlayPath in Directory.EnumerateFiles(cacheDir, "*-runtime-scene-editor-overlay.json").OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fileName = Path.GetFileName(overlayPath);
            string levelKey = fileName.Replace("-runtime-scene-editor-overlay.json", "", StringComparison.OrdinalIgnoreCase);
            GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
            TerrainMaterialClassifier.Apply(levelKey, workspace.RootPath, geometry);
            LevelDefinition? level = catalog.FindByKey(levelKey);
            TerrainBehaviorProofFile proofFile = TerrainBehaviorProofStore.Load(workspace.RootPath, levelKey);

            List<Moby> mobys = LoadLevelMobys(workspace, cacheDir, levelKey);
            List<Moby> controls = mobys.Where(IsBehaviorControlMoby).ToList();
            List<TerrainPolygon> solidFaces = geometry.Polygons.Where(IsLikelySolidMaterial).ToList();
            List<TerrainPolygon> hazardFaces = geometry.Polygons.Where(IsHazardMaterial).ToList();

            foreach (IGrouping<string, TerrainPolygon> surfaceGroup in geometry.Polygons
                .GroupBy(face => TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface))
                .Where(group => group.Key is "water" or "lava" or "ooze"))
            {
                targets.AddRange(BuildSurfaceProofTargets(
                    levelKey,
                    level?.DisplayName ?? levelKey,
                    surfaceGroup.Key,
                    surfaceGroup,
                    controls,
                    solidFaces,
                    proofFile));
            }

            targets.AddRange(BuildSolidControlTargets(
                levelKey,
                level?.DisplayName ?? levelKey,
                solidFaces,
                hazardFaces,
                controls,
                proofFile));

            foreach (IGrouping<string, TerrainPolygon> surfaceGroup in geometry.Polygons
                .GroupBy(face => TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface))
                .Where(group => ShouldIncludeSummarySurface(group.Key, proofFile)))
            {
                HashSet<int> textureIds = surfaceGroup.Select(face => face.TextureId).Where(textureId => textureId >= 0).ToHashSet();
                List<TerrainBehaviorRule> rules = proofFile.Rules
                    .Where(rule => textureIds.Contains(rule.TextureId) || string.Equals(rule.Surface, surfaceGroup.Key, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(rule => ConfidenceRank(rule.Confidence))
                    .ThenBy(rule => rule.TextureId)
                    .ToList();

                summaries.Add(new TerrainBehaviorProofSurfaceSummary
                {
                    LevelKey = levelKey,
                    DisplayName = level?.DisplayName ?? levelKey,
                    Surface = surfaceGroup.Key,
                    FaceCount = surfaceGroup.Count(),
                    TextureCount = textureIds.Count,
                    BehaviorSummary = BuildBehaviorSummary(surfaceGroup),
                    ProofSummary = BuildProofRuleSummary(rules),
                    ProofStatusRank = rules.Count == 0 ? 0 : rules.Max(rule => ConfidenceRank(rule.Confidence)),
                    NextStep = BuildSummaryNextStep(surfaceGroup.Key, rules)
                });
            }

            await Task.Yield();
        }

        List<TerrainBehaviorProofTarget> orderedHazardTargets = targets
            .Where(target => target.CaptureKind == "hazard-touch")
            .OrderBy(target => target.Surface, StringComparer.OrdinalIgnoreCase)
            .ThenBy(target => target.ProofStatusRank)
            .ThenBy(target => target.NearestControlDistance ?? double.MaxValue)
            .ThenBy(target => target.NearestSolidOrHazardDistance ?? double.MaxValue)
            .Take(64)
            .ToList();
        List<TerrainBehaviorProofTarget> orderedSolidTargets = targets
            .Where(target => target.CaptureKind == "solid-control")
            .OrderBy(target => target.ProofStatusRank)
            .ThenBy(target => target.NearestSolidOrHazardDistance ?? double.MaxValue)
            .ThenBy(target => target.NearestControlDistance ?? double.MaxValue)
            .Take(48)
            .ToList();

        TerrainBehaviorProofTargetReport targetReport = new()
        {
            GeneratedAt = DateTimeOffset.Now.ToString("s", CultureInfo.InvariantCulture),
            Summary = "Concrete terrain behavior capture targets. Use hazard rows for water/lava/ooze before/after touches and solid-control rows to prove the response does not happen on ordinary ground.",
            Targets = orderedHazardTargets.Concat(orderedSolidTargets).ToList()
        };

        TerrainBehaviorProofSummaryReport summaryReport = new()
        {
            GeneratedAt = DateTimeOffset.Now.ToString("s", CultureInfo.InvariantCulture),
            Summary = "Terrain behavior proof status by level and surface. Candidate behavior comes from material classification; observed/proven status requires RAM before/after captures.",
            Surfaces = summaries
                .OrderBy(item => SummarySurfaceRank(item.Surface))
                .ThenBy(item => item.ProofStatusRank)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        return new TerrainBehaviorProofReports(targetReport, summaryReport);
    }

    public static async Task<TerrainBehaviorProofReportPaths> WriteAsync(
        TerrainBehaviorProofReports reports,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        JsonSerializerOptions options = new() { WriteIndented = true };
        string targetsJsonPath = Path.Combine(outputDirectory, "terrain-behavior-proof-targets.json");
        string targetsMarkdownPath = Path.Combine(outputDirectory, "terrain-behavior-proof-targets.md");
        string summaryJsonPath = Path.Combine(outputDirectory, "terrain-behavior-proof-summary.json");
        string summaryMarkdownPath = Path.Combine(outputDirectory, "terrain-behavior-proof-summary.md");
        await File.WriteAllTextAsync(targetsJsonPath, JsonSerializer.Serialize(reports.Targets, options), cancellationToken);
        await File.WriteAllTextAsync(targetsMarkdownPath, BuildTargetsMarkdown(reports.Targets), cancellationToken);
        await File.WriteAllTextAsync(summaryJsonPath, JsonSerializer.Serialize(reports.Summary, options), cancellationToken);
        await File.WriteAllTextAsync(summaryMarkdownPath, BuildSummaryMarkdown(reports.Summary), cancellationToken);
        return new TerrainBehaviorProofReportPaths(targetsJsonPath, targetsMarkdownPath, summaryJsonPath, summaryMarkdownPath);
    }

    public static string BuildTargetsMarkdown(TerrainBehaviorProofTargetReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Terrain Behavior Proof Targets");
        builder.AppendLine();
        builder.AppendLine(report.Summary);
        builder.AppendLine();
        builder.AppendLine("## Hazard Touch Targets");
        builder.AppendLine();
        AppendTargetTable(builder, report.Targets.Where(target => target.CaptureKind == "hazard-touch").Take(48));
        builder.AppendLine();
        builder.AppendLine("## Solid Control Targets");
        builder.AppendLine();
        AppendTargetTable(builder, report.Targets.Where(target => target.CaptureKind == "solid-control").Take(48));
        builder.AppendLine();
        builder.AppendLine("## Capture Rule");
        builder.AppendLine();
        builder.AppendLine("Use each row as a before/after RAM pair location. A behavior becomes observed after one clean player response and proven only after repeat evidence plus a solid-control sample that does not show that response.");
        return builder.ToString();
    }

    public static string BuildSummaryMarkdown(TerrainBehaviorProofSummaryReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Terrain Behavior Proof Summary");
        builder.AppendLine();
        builder.AppendLine(report.Summary);
        builder.AppendLine();
        builder.AppendLine("| Level | Surface | Faces | Textures | Editor behavior | Proof state | Next step |");
        builder.AppendLine("|---|---|---:|---:|---|---|---|");
        foreach (TerrainBehaviorProofSurfaceSummary surface in report.Surfaces)
        {
            builder.AppendLine($"| {surface.DisplayName} | {TerrainMaterialClassifier.FormatSurface(surface.Surface)} | {surface.FaceCount} | {surface.TextureCount} | {EscapeMarkdownCell(surface.BehaviorSummary)} | {EscapeMarkdownCell(surface.ProofSummary)} | {EscapeMarkdownCell(surface.NextStep)} |");
        }

        return builder.ToString();
    }

    private static List<Moby> LoadLevelMobys(EditorWorkspace workspace, string cacheDir, string levelKey)
    {
        string mobyPath = Path.Combine(cacheDir, $"{levelKey}-mobys.json");
        List<Moby> mobys = File.Exists(mobyPath)
            ? MobyLoader.LoadCached(mobyPath).ToList()
            : new List<Moby>();
        if (mobys.Count > 0)
            MobyMetadataEnricher.Apply(workspace, levelKey, mobys);

        return mobys;
    }

    private static IEnumerable<TerrainBehaviorProofTarget> BuildSurfaceProofTargets(
        string levelKey,
        string displayName,
        string surface,
        IEnumerable<TerrainPolygon> faces,
        IReadOnlyList<Moby> controls,
        IReadOnlyList<TerrainPolygon> solidFaces,
        TerrainBehaviorProofFile proofFile)
    {
        foreach (IGrouping<int, TerrainPolygon> textureGroup in faces.GroupBy(face => face.TextureId))
        {
            TerrainPolygon bestFace = textureGroup
                .OrderBy(face => DistanceToNearestControl(face, controls))
                .ThenBy(face => FindNearestFace2D(face.Center, solidFaces, out double solidDistance) == null ? double.MaxValue : solidDistance)
                .First();
            Moby? nearestControl = FindNearestControl(bestFace, controls, out double controlDistance);
            TerrainPolygon? nearestSolid = FindNearestFace2D(bestFace.Center, solidFaces, out double nearestSolidDistance);
            TerrainBehaviorRule? rule = BestProofRule(proofFile, textureGroup.Key, surface);
            yield return BuildProofTarget(
                levelKey,
                displayName,
                "hazard-touch",
                surface,
                textureGroup.Key,
                textureGroup.Count(),
                bestFace,
                nearestControl,
                controlDistance,
                nearestSolid,
                nearestSolidDistance,
                rule);
        }
    }

    private static IEnumerable<TerrainBehaviorProofTarget> BuildSolidControlTargets(
        string levelKey,
        string displayName,
        IReadOnlyList<TerrainPolygon> solidFaces,
        IReadOnlyList<TerrainPolygon> hazardFaces,
        IReadOnlyList<Moby> controls,
        TerrainBehaviorProofFile proofFile)
    {
        if (solidFaces.Count == 0 || hazardFaces.Count == 0)
            yield break;

        foreach (IGrouping<int, TerrainPolygon> textureGroup in solidFaces.GroupBy(face => face.TextureId))
        {
            TerrainPolygon bestFace = textureGroup
                .OrderBy(face => FindNearestFace2D(face.Center, hazardFaces, out double hazardDistance) == null ? double.MaxValue : hazardDistance)
                .ThenBy(face => DistanceToNearestControl(face, controls))
                .First();
            Moby? nearestControl = FindNearestControl(bestFace, controls, out double controlDistance);
            TerrainPolygon? nearestHazard = FindNearestFace2D(bestFace.Center, hazardFaces, out double nearestHazardDistance);
            string surface = TerrainMaterialClassifier.NormalizeSurfaceName(bestFace.Surface);
            TerrainBehaviorRule? rule = BestProofRule(proofFile, textureGroup.Key, surface);
            yield return BuildProofTarget(
                levelKey,
                displayName,
                "solid-control",
                surface,
                textureGroup.Key,
                textureGroup.Count(),
                bestFace,
                nearestControl,
                controlDistance,
                nearestHazard,
                nearestHazardDistance,
                rule);
        }
    }

    private static TerrainBehaviorProofTarget BuildProofTarget(
        string levelKey,
        string displayName,
        string captureKind,
        string surface,
        int textureId,
        int textureFaceCount,
        TerrainPolygon face,
        Moby? nearestControl,
        double nearestControlDistance,
        TerrainPolygon? companionFace,
        double companionDistance,
        TerrainBehaviorRule? rule)
    {
        string proofStatus = rule == null
            ? "needed"
            : $"{rule.Confidence}:{TerrainBehaviorClassifier.FormatBehavior(rule.Behavior)}";
        return new TerrainBehaviorProofTarget
        {
            LevelKey = levelKey,
            DisplayName = displayName,
            CaptureKind = captureKind,
            Surface = surface,
            TextureId = textureId,
            TextureFaceCount = textureFaceCount,
            RuntimeKey = face.RuntimeKey,
            X = Math.Round(face.Center.X, 2),
            Y = Math.Round(face.Center.Y, 2),
            Z = Math.Round(face.AvgZ, 2),
            NearestControlId = nearestControl == null ? "" : $"T{nearestControl.TrueIndex}",
            NearestControlLabel = nearestControl?.DisplayLabel ?? "",
            NearestControlDistance = nearestControl == null ? null : Math.Round(nearestControlDistance, 2),
            NearestControlZDelta = nearestControl == null ? null : Math.Round(nearestControl.Position.Z - face.AvgZ, 2),
            NearestSolidOrHazardRuntimeKey = companionFace?.RuntimeKey ?? "",
            NearestSolidOrHazardDistance = companionFace == null ? null : Math.Round(companionDistance, 2),
            ExistingProof = proofStatus,
            ProofStatusRank = rule == null ? 0 : ConfidenceRank(rule.Confidence),
            NextCapture = BuildNextCaptureText(captureKind, surface, textureId, rule)
        };
    }

    private static string BuildNextCaptureText(string captureKind, string surface, int textureId, TerrainBehaviorRule? rule)
    {
        if (rule?.Confidence == "proven")
            return "Already proven; use only as regression check after edits.";

        if (captureKind == "solid-control")
            return $"Capture before/after with Spyro walking on {surface} texture {textureId}; expect no health/i-frame/state response.";

        if (rule?.Confidence == "observed")
            return $"Repeat {surface} texture {textureId} before/after touch, then pair it with a solid-control capture in the same level.";

        return $"Capture before/after at the moment Spyro touches {surface} texture {textureId}.";
    }

    private static TerrainBehaviorRule? BestProofRule(TerrainBehaviorProofFile proofFile, int textureId, string surface)
    {
        string normalizedSurface = TerrainMaterialClassifier.NormalizeSurfaceName(surface);
        return proofFile.Rules
            .Where(rule => rule.TextureId == textureId || string.Equals(rule.Surface, normalizedSurface, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(rule => ConfidenceRank(rule.Confidence))
            .ThenByDescending(rule => rule.ObservationCount)
            .FirstOrDefault();
    }

    private static Moby? FindNearestControl(TerrainPolygon face, IReadOnlyList<Moby> controls, out double distance)
    {
        Moby? nearest = null;
        distance = double.MaxValue;
        foreach (Moby control in controls)
        {
            double current = DistanceXY(face.Center.X, face.Center.Y, control.Position.X, control.Position.Y);
            if (current >= distance)
                continue;

            distance = current;
            nearest = control;
        }

        return nearest;
    }

    private static double DistanceToNearestControl(TerrainPolygon face, IReadOnlyList<Moby> controls)
    {
        return FindNearestControl(face, controls, out double distance) == null ? double.MaxValue : distance;
    }

    private static TerrainPolygon? FindNearestFace2D(Vector2f position, IReadOnlyList<TerrainPolygon> faces, out double distance)
    {
        TerrainPolygon? nearest = null;
        distance = double.MaxValue;
        foreach (TerrainPolygon face in faces)
        {
            double current = DistanceXY(position.X, position.Y, face.Center.X, face.Center.Y);
            if (current >= distance)
                continue;

            distance = current;
            nearest = face;
        }

        return nearest;
    }

    private static bool IsBehaviorControlMoby(Moby moby)
    {
        if (moby.IsChest || moby.IsChestContent || moby.IsGemLike)
            return false;

        if (moby.VisualKind == MobyVisualKind.Control)
            return true;

        string text = $"{moby.DisplayLabel} {moby.CandidateKind} {moby.Evidence} {moby.BehaviorNote}".ToLowerInvariant();
        return text.Contains("trigger", StringComparison.Ordinal) ||
            text.Contains("camera", StringComparison.Ordinal) ||
            text.Contains("helper", StringComparison.Ordinal) ||
            text.Contains("marker", StringComparison.Ordinal) ||
            text.Contains("control", StringComparison.Ordinal) ||
            text.Contains("system", StringComparison.Ordinal);
    }

    private static bool IsHazardMaterial(TerrainPolygon face)
    {
        string surface = TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface);
        return surface is "water" or "lava" or "ooze";
    }

    private static bool IsLikelySolidMaterial(TerrainPolygon face)
    {
        string surface = TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface);
        return surface is "grass" or "sand" or "stone" or "ground" or "ice";
    }

    private static bool ShouldIncludeSummarySurface(string surface, TerrainBehaviorProofFile proofFile)
    {
        if (surface is "water" or "lava" or "ooze" or "grass" or "sand" or "stone" or "ground" or "ice")
            return true;

        return proofFile.Rules.Any(rule => string.Equals(rule.Surface, surface, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildBehaviorSummary(IEnumerable<TerrainPolygon> faces)
    {
        return string.Join(", ", faces
            .GroupBy(face => face.Behavior, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .Select(group => $"{TerrainBehaviorClassifier.FormatBehavior(group.Key)} {group.Count()}"));
    }

    private static string BuildProofRuleSummary(IReadOnlyList<TerrainBehaviorRule> rules)
    {
        if (rules.Count == 0)
            return "not captured yet";

        return string.Join(", ", rules
            .GroupBy(rule => rule.Confidence, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => ConfidenceRank(group.Key))
            .Select(group => $"{group.Key} {group.Count()}"));
    }

    private static string BuildSummaryNextStep(string surface, IReadOnlyList<TerrainBehaviorRule> rules)
    {
        bool hasProven = rules.Any(rule => string.Equals(rule.Confidence, "proven", StringComparison.OrdinalIgnoreCase));
        bool hasObserved = rules.Any(rule => string.Equals(rule.Confidence, "observed", StringComparison.OrdinalIgnoreCase));
        if (hasProven)
            return "Proven for at least one matching texture; use Proof Targets only as regression checks after terrain edits.";

        if (surface is "water" or "lava" or "ooze")
        {
            if (hasObserved)
                return "Repeat the touch capture and pair it with a solid-control capture in the same level.";

            return "Use Proof Targets to select a face, then capture before/after RAM at the moment Spyro touches it.";
        }

        if (surface is "grass" or "sand" or "stone" or "ground")
        {
            if (hasObserved)
                return "Repeat the walk/no-response capture to promote this solid-control proof.";

            return "Capture before/after RAM with Spyro walking on this surface; it should not change health, i-frames, or state.";
        }

        return "Classify the surface first, then capture a before/after RAM pair if it appears to affect Spyro.";
    }

    private static int SummarySurfaceRank(string surface)
    {
        return surface switch
        {
            "water" => 0,
            "lava" => 1,
            "ooze" => 2,
            "grass" => 3,
            "sand" => 4,
            "stone" => 5,
            "ground" => 6,
            _ => 7
        };
    }

    private static int ConfidenceRank(string confidence)
    {
        return confidence switch
        {
            "proven" => 3,
            "observed" => 2,
            "noisy" => 1,
            _ => 0
        };
    }

    private static double DistanceXY(float ax, float ay, float bx, float by)
    {
        double dx = ax - bx;
        double dy = ay - by;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static void AppendTargetTable(StringBuilder builder, IEnumerable<TerrainBehaviorProofTarget> targets)
    {
        builder.AppendLine("| Level | Surface | Texture | Runtime key | XYZ | Nearest control | Companion | Existing proof | Next capture |");
        builder.AppendLine("|---|---|---:|---|---|---|---|---|---|");
        foreach (TerrainBehaviorProofTarget target in targets)
        {
            string control = string.IsNullOrWhiteSpace(target.NearestControlId)
                ? ""
                : $"{target.NearestControlId} {target.NearestControlLabel} d {target.NearestControlDistance:0.##} dz {target.NearestControlZDelta:0.##}";
            string companion = string.IsNullOrWhiteSpace(target.NearestSolidOrHazardRuntimeKey)
                ? ""
                : $"{target.NearestSolidOrHazardRuntimeKey} d {target.NearestSolidOrHazardDistance:0.##}";
            builder.AppendLine($"| {target.DisplayName} | {target.Surface} | {target.TextureId} ({target.TextureFaceCount}) | `{target.RuntimeKey}` | {target.X:0.##}, {target.Y:0.##}, {target.Z:0.##} | {EscapeMarkdownCell(control)} | {companion} | {EscapeMarkdownCell(target.ExistingProof)} | {EscapeMarkdownCell(target.NextCapture)} |");
        }
    }

    private static string EscapeMarkdownCell(string value)
    {
        return (value ?? "").Replace("|", "/", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }
}

public sealed record TerrainBehaviorProofReports(
    TerrainBehaviorProofTargetReport Targets,
    TerrainBehaviorProofSummaryReport Summary);

public sealed record TerrainBehaviorProofReportPaths(
    string TargetsJsonPath,
    string TargetsMarkdownPath,
    string SummaryJsonPath,
    string SummaryMarkdownPath);

public sealed class TerrainBehaviorProofTargetReport
{
    public string GeneratedAt { get; init; } = "";
    public string Summary { get; init; } = "";
    public IReadOnlyList<TerrainBehaviorProofTarget> Targets { get; init; } = [];
}

public sealed class TerrainBehaviorProofSummaryReport
{
    public string GeneratedAt { get; init; } = "";
    public string Summary { get; init; } = "";
    public IReadOnlyList<TerrainBehaviorProofSurfaceSummary> Surfaces { get; init; } = [];
}

public sealed class TerrainBehaviorProofSurfaceSummary
{
    public string LevelKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Surface { get; init; } = "";
    public int FaceCount { get; init; }
    public int TextureCount { get; init; }
    public string BehaviorSummary { get; init; } = "";
    public string ProofSummary { get; init; } = "";
    public int ProofStatusRank { get; init; }
    public string NextStep { get; init; } = "";
}

public sealed class TerrainBehaviorProofTarget
{
    public string LevelKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string CaptureKind { get; init; } = "";
    public string Surface { get; init; } = "";
    public int TextureId { get; init; }
    public int TextureFaceCount { get; init; }
    public string RuntimeKey { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
    public string NearestControlId { get; init; } = "";
    public string NearestControlLabel { get; init; } = "";
    public double? NearestControlDistance { get; init; }
    public double? NearestControlZDelta { get; init; }
    public string NearestSolidOrHazardRuntimeKey { get; init; } = "";
    public double? NearestSolidOrHazardDistance { get; init; }
    public string ExistingProof { get; init; } = "";
    public int ProofStatusRank { get; init; }
    public string NextCapture { get; init; } = "";
}

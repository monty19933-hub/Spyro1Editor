using System.Text.Json;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Scene;

public static class TerrainBehaviorProofStore
{
    public static async Task<TerrainBehaviorProofFile> RecordObservationAsync(
        string workspaceRoot,
        string levelKey,
        int textureId,
        string surface,
        string runtimeKey,
        TerrainBehaviorRamPairReport report,
        CancellationToken cancellationToken = default)
    {
        if (textureId < 0)
            return Load(workspaceRoot, levelKey);

        TerrainBehaviorProofFile file = Load(workspaceRoot, levelKey);
        TerrainBehaviorObservation observation = TerrainBehaviorObservation.FromReport(
            levelKey,
            textureId,
            surface,
            runtimeKey,
            report);

        List<TerrainBehaviorObservation> observations = file.Observations.ToList();
        observations.Add(observation);
        List<TerrainBehaviorRule> rules = BuildRules(observations);
        TerrainBehaviorProofFile updated = new()
        {
            GeneratedAt = DateTime.Now.ToString("s"),
            LevelKey = levelKey,
            Note = "Terrain behavior evidence keyed by texture ID. Observed means a RAM pair saw the response once; proven requires repeat evidence plus a solid-control sample.",
            Observations = observations,
            Rules = rules
        };

        string path = ProofPath(workspaceRoot, levelKey);
        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, updated, NewJsonOptions(), cancellationToken);
        return updated;
    }

    public static TerrainBehaviorRule? FindBestRule(string workspaceRoot, string levelKey, int textureId, string surface)
    {
        TerrainBehaviorProofFile file = Load(workspaceRoot, levelKey);
        string normalizedSurface = TerrainMaterialClassifier.NormalizeSurfaceName(surface);
        return file.Rules
            .Where(rule => rule.TextureId == textureId || string.Equals(rule.Surface, normalizedSurface, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(rule => RuleRank(rule.Confidence))
            .ThenByDescending(rule => rule.ObservationCount)
            .FirstOrDefault();
    }

    public static TerrainBehaviorProofFile Load(string workspaceRoot, string levelKey)
    {
        string path = ProofPath(workspaceRoot, levelKey);
        if (!File.Exists(path))
            return new TerrainBehaviorProofFile { LevelKey = levelKey };

        try
        {
            using FileStream stream = File.OpenRead(path);
            TerrainBehaviorProofFile? file = JsonSerializer.Deserialize<TerrainBehaviorProofFile>(stream, NewJsonOptions());
            return file ?? new TerrainBehaviorProofFile { LevelKey = levelKey };
        }
        catch
        {
            return new TerrainBehaviorProofFile { LevelKey = levelKey };
        }
    }

    public static string ProofPath(string workspaceRoot, string levelKey)
    {
        return Path.Combine(workspaceRoot, $"{levelKey}-terrain-behavior-proofs.json");
    }

    private static List<TerrainBehaviorRule> BuildRules(IReadOnlyList<TerrainBehaviorObservation> observations)
    {
        bool hasSolidControl = observations.Any(observation =>
            observation.Response == "none" &&
            TerrainMaterialClassifier.NormalizeSurfaceName(observation.Surface) is "grass" or "sand" or "stone" or "ground");

        List<TerrainBehaviorRule> rules = new();
        foreach (IGrouping<string, TerrainBehaviorObservation> group in observations.GroupBy(RuleKey, StringComparer.OrdinalIgnoreCase))
        {
            TerrainBehaviorObservation latest = group.OrderByDescending(item => item.GeneratedAt, StringComparer.OrdinalIgnoreCase).First();
            int directCount = group.Count(item => item.DirectPlayerResponse && item.CleanSample);
            int stateCount = group.Count(item => item.Response == "state-change" && item.CleanSample);
            int noResponseCount = group.Count(item => item.Response == "none" && item.CleanSample);

            string response;
            string confidence;
            string note;
            if (directCount >= 2 && hasSolidControl)
            {
                response = "damage";
                confidence = "proven";
                note = "Repeated clean player damage/i-frame response with at least one solid-ground control sample.";
            }
            else if (directCount >= 1)
            {
                response = "damage";
                confidence = "observed";
                note = "A clean RAM pair observed Spyro health/i-frame response once. Repeat this and capture a solid-ground control before treating it as exact.";
            }
            else if (stateCount >= 2 && hasSolidControl)
            {
                response = "state-change";
                confidence = "proven";
                note = "Repeated clean Spyro state response with at least one solid-ground control sample.";
            }
            else if (stateCount >= 1)
            {
                response = "state-change";
                confidence = "observed";
                note = "A clean RAM pair observed a Spyro state response once. Repeat before treating it as exact.";
            }
            else if (noResponseCount >= 2)
            {
                response = "solid";
                confidence = "proven";
                note = "Repeated clean samples showed no damage/state response.";
            }
            else if (noResponseCount >= 1)
            {
                response = "solid";
                confidence = "observed";
                note = "One clean sample showed no damage/state response.";
            }
            else
            {
                response = latest.Response;
                confidence = "noisy";
                note = "Existing captures include level changes, collectable side effects, or unclear player response.";
            }

            rules.Add(new TerrainBehaviorRule(
                TextureId: latest.TextureId,
                Surface: latest.Surface,
                Behavior: BehaviorForResponse(response, confidence),
                Response: response,
                Confidence: confidence,
                ObservationCount: group.Count(),
                DirectPlayerResponseCount: directCount,
                SolidControlCount: hasSolidControl ? observations.Count(item => item.Response == "none" && item.CleanSample) : 0,
                LastRuntimeKey: latest.RuntimeKey,
                Note: note));
        }

        return rules
            .OrderBy(rule => rule.TextureId)
            .ThenBy(rule => rule.Surface, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BehaviorForResponse(string response, string confidence)
    {
        string suffix = string.Equals(confidence, "proven", StringComparison.OrdinalIgnoreCase) ? "proven" : "observed";
        return response switch
        {
            "damage" => $"damage-{suffix}",
            "state-change" => $"state-response-{suffix}",
            "solid" or "none" => $"solid-{suffix}",
            _ => "unknown"
        };
    }

    private static string RuleKey(TerrainBehaviorObservation observation)
    {
        return $"{observation.TextureId}:{TerrainMaterialClassifier.NormalizeSurfaceName(observation.Surface)}";
    }

    private static int RuleRank(string confidence)
    {
        return confidence switch
        {
            "proven" => 3,
            "observed" => 2,
            "noisy" => 1,
            _ => 0
        };
    }

    private static JsonSerializerOptions NewJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }
}

public sealed class TerrainBehaviorProofFile
{
    public string GeneratedAt { get; init; } = "";
    public string LevelKey { get; init; } = "";
    public string Note { get; init; } = "";
    public IReadOnlyList<TerrainBehaviorObservation> Observations { get; init; } = [];
    public IReadOnlyList<TerrainBehaviorRule> Rules { get; init; } = [];
}

public sealed record TerrainBehaviorRule(
    int TextureId,
    string Surface,
    string Behavior,
    string Response,
    string Confidence,
    int ObservationCount,
    int DirectPlayerResponseCount,
    int SolidControlCount,
    string LastRuntimeKey,
    string Note);

public sealed record TerrainBehaviorObservation(
    string GeneratedAt,
    string LevelKey,
    int TextureId,
    string Surface,
    string RuntimeKey,
    string TerrainEvent,
    int HealthBefore,
    int HealthAfter,
    int IFramesBefore,
    int IFramesAfter,
    string StateBefore,
    string StateAfter,
    bool DirectPlayerResponse,
    bool StateChanged,
    bool CleanSample,
    string Response,
    string Interpretation)
{
    public static TerrainBehaviorObservation FromReport(
        string levelKey,
        int textureId,
        string surface,
        string runtimeKey,
        TerrainBehaviorRamPairReport report)
    {
        bool healthDropped = report.After.Spyro.Health < report.Before.Spyro.Health;
        bool iFramesStarted = report.After.Spyro.IFrames > report.Before.Spyro.IFrames;
        bool stateChanged = report.SpyroChanges.Any(change => change.Field is "stateHex" or "stateSubHex" or "grounded" or "isGliding");
        bool levelChanged = report.GlobalChanges.Any(change => string.Equals(change.Field, "levelId", StringComparison.OrdinalIgnoreCase));
        bool counterChanged = report.GlobalChanges.Any(change => change.Field is "globalGemCount" or "globalDragonCount" or "globalEggCount");
        bool clean = !levelChanged && !counterChanged;
        string response = healthDropped || iFramesStarted
            ? "damage"
            : stateChanged ? "state-change" : "none";

        return new TerrainBehaviorObservation(
            GeneratedAt: report.GeneratedAt,
            LevelKey: levelKey,
            TextureId: textureId,
            Surface: TerrainMaterialClassifier.NormalizeSurfaceName(surface),
            RuntimeKey: runtimeKey,
            TerrainEvent: report.TerrainEvent,
            HealthBefore: report.Before.Spyro.Health,
            HealthAfter: report.After.Spyro.Health,
            IFramesBefore: report.Before.Spyro.IFrames,
            IFramesAfter: report.After.Spyro.IFrames,
            StateBefore: report.Before.Spyro.StateHex,
            StateAfter: report.After.Spyro.StateHex,
            DirectPlayerResponse: healthDropped || iFramesStarted,
            StateChanged: stateChanged,
            CleanSample: clean,
            Response: response,
            Interpretation: report.Interpretation);
    }
}

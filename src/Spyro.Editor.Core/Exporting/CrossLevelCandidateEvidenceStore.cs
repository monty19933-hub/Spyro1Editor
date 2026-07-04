using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal static class CrossLevelCandidateEvidenceStore
{
    public static CrossLevelCandidateEvidence FindBestEvidence(string workspaceRoot, string targetLevelKey, string recipeId, string expectedRecipeFingerprint = "")
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || string.IsNullOrWhiteSpace(recipeId))
            return CrossLevelCandidateEvidence.NotFound;

        string objectsDir = Path.Combine(workspaceRoot, "_local", "objects");
        if (!Directory.Exists(objectsDir))
            return CrossLevelCandidateEvidence.NotFound;

        CrossLevelCandidateEvidence best = CrossLevelCandidateEvidence.NotFound;
        foreach (string resultPath in Directory.EnumerateFiles(objectsDir, "*.candidate-result.json"))
        {
            CrossLevelCandidateEvidence evidence = ReadEvidence(resultPath, targetLevelKey, recipeId, expectedRecipeFingerprint);
            if (!evidence.Decisive)
                continue;
            if (!best.Decisive || evidence.LastWrittenUtc > best.LastWrittenUtc)
                best = evidence;
        }

        return best;
    }

    public static CrossLevelCandidateEvidence FindPassedEvidence(string workspaceRoot, string targetLevelKey, string recipeId, string expectedRecipeFingerprint = "")
    {
        CrossLevelCandidateEvidence evidence = FindBestEvidence(workspaceRoot, targetLevelKey, recipeId, expectedRecipeFingerprint);
        return evidence.Passed ? evidence : CrossLevelCandidateEvidence.NotFound;
    }

    private static CrossLevelCandidateEvidence ReadEvidence(string resultPath, string targetLevelKey, string recipeId, string expectedRecipeFingerprint)
    {
        try
        {
            using FileStream stream = File.OpenRead(resultPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("candidate", out JsonElement candidate) || candidate.ValueKind != JsonValueKind.Object)
                return CrossLevelCandidateEvidence.NotFound;

            string levelKey = ReadString(candidate, "levelKey");
            if (!string.Equals(levelKey, targetLevelKey, StringComparison.OrdinalIgnoreCase))
                return CrossLevelCandidateEvidence.NotFound;

            if (!CandidateIncludesRecipe(candidate, recipeId, expectedRecipeFingerprint))
                return CrossLevelCandidateEvidence.NotFound;

            JsonElement result = root.TryGetProperty("result", out JsonElement resultElement) && resultElement.ValueKind == JsonValueKind.Object
                ? resultElement
                : root;
            string status = ReadString(result, "status");
            bool objectAppeared = ReadBool(result, "objectAppeared") == true;
            bool mobyRecordsPresent = ReadBoolOrFallback(result, "mobyRecordsPresent", objectAppeared);
            bool actorRootsPresent = ReadBoolOrFallback(result, "actorRootsPresent", objectAppeared);
            bool behaviorChecksPassed = BehaviorChecksPassed(root);
            bool passed = StatusLooksPassed(status) &&
                ReadBool(result, "bootedLevel") == true &&
                objectAppeared &&
                mobyRecordsPresent &&
                actorRootsPresent &&
                ReadBool(result, "behaviorCorrect") == true &&
                behaviorChecksPassed &&
                ReadBool(result, "noNearbyRegression") == true;
            bool failed = StatusLooksFailed(status) ||
                ReadBool(result, "bootedLevel") == false ||
                ReadBool(result, "objectAppeared") == false ||
                ReadBool(result, "mobyRecordsPresent") == false ||
                ReadBool(result, "actorRootsPresent") == false ||
                ReadBool(result, "behaviorCorrect") == false ||
                BehaviorChecksFailed(root) ||
                ReadBool(result, "noNearbyRegression") == false;

            return new CrossLevelCandidateEvidence(passed, failed, resultPath, status, ReadString(result, "notes"), File.GetLastWriteTimeUtc(resultPath));
        }
        catch
        {
            return CrossLevelCandidateEvidence.NotFound;
        }
    }

    private static bool CandidateIncludesRecipe(JsonElement candidate, string recipeId, string expectedRecipeFingerprint)
    {
        if (!candidate.TryGetProperty("recipes", out JsonElement recipes) || recipes.ValueKind != JsonValueKind.Array)
            return false;

        foreach (JsonElement recipe in recipes.EnumerateArray())
        {
            if (recipe.ValueKind != JsonValueKind.Object ||
                !string.Equals(ReadString(recipe, "recipeId"), recipeId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(expectedRecipeFingerprint))
                return true;

            string actualFingerprint = ReadString(recipe, "recipeFingerprint");
            if (string.IsNullOrWhiteSpace(actualFingerprint))
                actualFingerprint = ReadString(recipe, "fingerprint");
            return string.Equals(actualFingerprint, expectedRecipeFingerprint, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool StatusLooksPassed(string status)
    {
        return status.Contains("pass", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("verified", StringComparison.OrdinalIgnoreCase);
    }

    private static bool StatusLooksFailed(string status)
    {
        return status.Contains("fail", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("reject", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("blocked", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static bool? ReadBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static bool ReadBoolOrFallback(JsonElement element, string propertyName, bool fallback)
    {
        bool? value = ReadBool(element, propertyName);
        return value ?? fallback;
    }

    private static bool BehaviorChecksPassed(JsonElement root)
    {
        if (!root.TryGetProperty("behaviorChecks", out JsonElement checks) || checks.ValueKind != JsonValueKind.Array)
            return true;

        bool any = false;
        foreach (JsonElement check in checks.EnumerateArray())
        {
            if (check.ValueKind != JsonValueKind.Object)
                continue;
            any = true;
            if (ReadBool(check, "passed") != true)
                return false;
        }

        return any;
    }

    private static bool BehaviorChecksFailed(JsonElement root)
    {
        if (!root.TryGetProperty("behaviorChecks", out JsonElement checks) || checks.ValueKind != JsonValueKind.Array)
            return false;

        foreach (JsonElement check in checks.EnumerateArray())
        {
            if (check.ValueKind == JsonValueKind.Object && ReadBool(check, "passed") == false)
                return true;
        }

        return false;
    }
}

internal sealed record CrossLevelCandidateEvidence(
    bool Passed,
    bool Failed,
    string ResultPath,
    string Status,
    string Notes,
    DateTime LastWrittenUtc)
{
    public bool Decisive => Passed || Failed;

    public static CrossLevelCandidateEvidence NotFound { get; } = new(false, false, "", "", "", DateTime.MinValue);
}

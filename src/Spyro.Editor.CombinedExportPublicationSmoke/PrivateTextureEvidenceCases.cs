using System.Reflection;
using System.Text.Json;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

internal static class PrivateTextureEvidenceCases
{
    public static string Run()
    {
        MethodInfo format = typeof(MainWindow).GetMethod("BuildPrivateTextureExportStepSummary",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Private texture summaries still abbreviate static target readback as runtime evidence; the shared truthful formatter is missing.");
        int checkedSummaries = 0;
        foreach (AppendedPrivateTerrainTexturePromotionProfile profile in AppendedPrivateTerrainTexturePromotionProfileRegistry.Profiles)
        foreach (bool aggregate in new[] { false, true })
        foreach (bool targetReadback in new[] { false, true })
        {
            NativeTerrainTexturePrivateRecordBatchPlan plan = MakePlan(profile, 1);
            using JsonDocument json = Serialize(plan, aggregate, targetReadback, "historical recipe control");
            JsonElement entry = RequireCommon(json, plan, aggregate, targetReadback);
            Require(entry.GetProperty("historicalRuntimeRecipeMatched").GetBoolean() == profile.RuntimeProven,
                "Exact recipe evidence was not distinguished from this output's absent gameplay verification.");
            JsonElement historical = entry.GetProperty("historicalRuntimeRecipeEvidence");
            if (profile.RuntimeProven)
            {
                Require(historical.GetProperty("profileId").GetString() == profile.Id &&
                    historical.GetProperty("evidenceId").GetString() == profile.EvidenceId &&
                    historical.GetProperty("runtimeProofOutputSha256").GetString() == profile.RuntimeProofOutputSha256 &&
                    historical.GetProperty("scope").GetString() == "historical-recipe-only-not-current-output",
                    "Historical proof lost its exact registry identity or was attributed to the current BIN.");
            }
            else Require(historical.ValueKind == JsonValueKind.Null, "A static-only profile acquired historical runtime evidence.");
        }

        AppendedPrivateTerrainTexturePromotionProfile artisans = AppendedPrivateTerrainTexturePromotionProfileRegistry.Profiles
            .Single(profile => profile.TargetLevelKey == "artisans");
        NativeTerrainTexturePrivateRecordBatchPlan exact = MakePlan(artisans, 1);
        NativeTerrainTexturePrivateRecordBatchPlan[] notHistorical =
        [
            exact with { StructuralGrowthPolicy = NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch },
            exact with { SourceBinding = exact.SourceBinding with { SourceImageSha256 = new string('0', 64) } },
            exact with { SourceBinding = exact.SourceBinding with { ExpectedLevelDataSha256 = new string('0', 64) } },
            exact with { SourceBinding = exact.SourceBinding with { ExpectedTextureComponentSha256 = new string('0', 64) } },
            exact with { SourceBinding = exact.SourceBinding with { TargetLevelKey = "unknown" } },
            exact with { WriterKind = NativeTerrainTexturePrivateImageWriterKind.SectorRelocation },
            MakePlan(artisans, artisans.RuntimeProvenMaxAppendedRecords + 1)
        ];
        foreach (NativeTerrainTexturePrivateRecordBatchPlan plan in notHistorical)
        foreach (bool aggregate in new[] { false, true })
        {
            using JsonDocument json = Serialize(plan, aggregate, true,
                "Runtime-proven Artisans historical recipe matched; this untrusted diagnostic wording is not evidence");
            JsonElement entry = RequireCommon(json, plan, aggregate, true);
            Require(!entry.GetProperty("historicalRuntimeRecipeMatched").GetBoolean() &&
                entry.GetProperty("historicalRuntimeRecipeEvidence").ValueKind == JsonValueKind.Null,
                "A research/mismatched/over-capacity request acquired historical proof from its level name or reason string.");
        }
        return $"privateTextureEvidenceSummaries={checkedSummaries} legacyReadbackPreserved=1 currentOutputRuntimeUnverified=1 historicalRecipeScopeExact=1";

        JsonDocument Serialize(NativeTerrainTexturePrivateRecordBatchPlan plan, bool aggregate, bool targetReadback, string reason)
        {
            object summary = format.Invoke(null,
                ["Artisans runtime-proven", plan, 7, 11, aggregate ? (int?)2 : null, true, targetReadback, true, true, reason])!;
            // Match the actual combined manifest's heterogeneous steps shape,
            // rather than inspecting only the helper's in-memory properties.
            return JsonDocument.Parse(JsonSerializer.Serialize(new { steps = new[] { summary } }));
        }
        JsonElement RequireCommon(JsonDocument json, NativeTerrainTexturePrivateRecordBatchPlan plan, bool aggregate, bool targetReadback)
        {
            checkedSummaries++;
            JsonElement entry = json.RootElement.GetProperty("steps")[0];
            Require(entry.GetProperty("runtimeReadback").GetBoolean() == targetReadback &&
                entry.GetProperty("runtimeTargetReadback").GetBoolean() == targetReadback &&
                !entry.GetProperty("runtimeVerified").GetBoolean() &&
                entry.GetProperty("readbackScope").GetString() == "static-native-target-data-only" &&
                entry.GetProperty("runtimeEvidenceScope").GetString() == "current-output-gameplay-unverified",
                "Static target bytes were mislabeled as current-output gameplay proof, or the legacy value changed.");
            Require(entry.GetProperty("kind").GetString() == (aggregate
                    ? "terrain-appended-private-multi-level-aggregate" : "terrain-appended-private-exclusive") &&
                entry.GetProperty("writer").GetString() == plan.WriterKind.ToString() &&
                entry.GetProperty("appendedRecords").GetInt32() == plan.AppendedPrivateEdits.Count &&
                entry.GetProperty("existingRecordOverrides").GetInt32() == plan.ExistingRecordEdits.Count &&
                entry.GetProperty("ordinaryPatches").GetInt32() == 7 &&
                entry.GetProperty("texturePagePatches").GetInt32() == 11 &&
                entry.GetProperty("exactReadback").GetBoolean() && entry.GetProperty("globalLogicalReadback").GetBoolean() &&
                entry.GetProperty("sourcePreserved").GetBoolean() && entry.GetProperty("privateGateReason").ValueKind == JsonValueKind.String,
                "The diagnostic correction changed existing private-step manifest values.");
            Require(aggregate
                    ? entry.GetProperty("multiDestinationCount").GetInt32() == 2
                    : !entry.TryGetProperty("multiDestinationCount", out _),
                "Single/aggregate summaries no longer preserve their previous distinct field shapes.");
            return entry;
        }
    }

    private static NativeTerrainTexturePrivateRecordBatchPlan MakePlan(AppendedPrivateTerrainTexturePromotionProfile profile, int count)
    {
        NativeTerrainTextureRecordAppendSourceBinding binding = new(profile.Retail.BindingVersion,
            profile.Retail.SourceImageSha256, profile.TargetLevelKey, profile.Retail.TargetWadEntry,
            profile.Retail.ExpectedSourceTextureCount, profile.Retail.ExpectedTextureComponentSha256, profile.Retail.ExpectedLevelDataSha256);
        NativeTerrainTextureRelocationEdit[] appended = Enumerable.Range(0, count).Select(index =>
            new NativeTerrainTextureRelocationEdit(binding.ExpectedSourceTextureCount + index, "donor", "Donor", 12, index,
                "fixture", "both", "", "", "", TargetRecordKind: NativeTerrainTextureTargetRecordKind.AppendedPrivate)).ToArray();
        return new(DateTimeOffset.UnixEpoch, "not-read.bin", binding.SourceImageSha256, "not-read.cue", "not-written",
            profile.TargetLevelKey, binding.TargetWadEntry, binding, [], appended, [], [], [], profile.WriterKind,
            null, null, true, true, true, true, true, true, []) { StructuralGrowthPolicy = profile.StructuralGrowthPolicy };
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    // Diagnostic projection only: this does not authorize a writer or promote
    // this output. RuntimeTargetReadbackVerified checks native target data,
    // not execution in DuckStation. Keep the old key as an explicit alias.
    private static object BuildPrivateTextureExportStepSummary(
        string level, NativeTerrainTexturePrivateRecordBatchPlan plan,
        int ordinaryPatches, int texturePagePatches, int? multiDestinationCount,
        bool exactReadback, bool runtimeTargetReadback, bool globalLogicalReadback,
        bool sourcePreserved, string privateGateReason)
    {
        bool historicalRecipeMatched = AppendedPrivateTerrainTexturePromotionProfileRegistry.TryAuthorizeNormalRelease(
            plan.SourceBinding, plan.WriterKind, plan.AppendedPrivateEdits.Count, plan.StructuralGrowthPolicy,
            out AppendedPrivateTerrainTexturePromotionProfile? historical, out _);
        Dictionary<string, object?> summary = new()
        {
            ["level"] = level,
            ["kind"] = multiDestinationCount.HasValue
                ? "terrain-appended-private-multi-level-aggregate" : "terrain-appended-private-exclusive",
            ["writer"] = plan.WriterKind.ToString(),
            ["appendedRecords"] = plan.AppendedPrivateEdits.Count,
            ["existingRecordOverrides"] = plan.ExistingRecordEdits.Count,
            ["ordinaryPatches"] = ordinaryPatches,
            ["texturePagePatches"] = texturePagePatches,
            ["exactReadback"] = exactReadback,
            ["runtimeReadback"] = runtimeTargetReadback,
            ["runtimeTargetReadback"] = runtimeTargetReadback,
            ["runtimeVerified"] = false,
            ["readbackScope"] = "static-native-target-data-only",
            ["runtimeEvidenceScope"] = "current-output-gameplay-unverified",
            ["historicalRuntimeRecipeMatched"] = historicalRecipeMatched,
            ["historicalRuntimeRecipeEvidence"] = historicalRecipeMatched && historical != null
                ? new
                {
                    profileId = historical.Id,
                    evidenceId = historical.EvidenceId,
                    runtimeProofOutputSha256 = historical.RuntimeProofOutputSha256,
                    scope = "historical-recipe-only-not-current-output"
                } : null,
            ["globalLogicalReadback"] = globalLogicalReadback,
            ["sourcePreserved"] = sourcePreserved,
            ["privateGateReason"] = privateGateReason
        };
        if (multiDestinationCount.HasValue)
            summary["multiDestinationCount"] = multiDestinationCount.Value;
        return summary;
    }
}

using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public enum SpecialChestSafetySeverity
{
    Info,
    Review,
    Block
}

public enum SpecialChestStructuralPlanStatus
{
    Ready,
    Review,
    Blocked
}

public sealed record SpecialChestSafetyIssue(
    string Code,
    SpecialChestSafetySeverity Severity,
    string Message);

public sealed record SpecialChestStructuralPlanningRequest(
    SpecialChestFamily Family,
    string TargetLevelKey,
    string DiscImageSha256,
    int RequestedBundleInstances = 1,
    int ExistingBundleInstances = 0,
    int ExistingSourceRecordCount = 0,
    int SourceRecordLimit = 256,
    bool HasOtherObjectEdits = false,
    bool HasOtherStructuralRelocations = false,
    bool AllowResearchCandidate = false);

public sealed record SpecialChestStructuralPlan(
    SpecialChestStructuralPlanningRequest Request,
    SpecialChestBundleProfile? Profile,
    SpecialChestStructuralPlanStatus Status,
    bool CanWriteNormalBuild,
    bool CanStageResearchCandidate,
    int? ProjectedSourceRecordCount,
    int? ProjectedTreasureDelta,
    int? ProjectedLifeDelta,
    int? ProjectedWadGrowthBytes,
    IReadOnlyList<SpecialChestSafetyIssue> Issues)
{
    public bool Blocked => Status == SpecialChestStructuralPlanStatus.Blocked;
}

/// <summary>
/// Builds a non-mutating special-chest composition plan.  This is the shared
/// hook for future editor, Build Safety, and exporter integrations; it does not
/// write a BIN and cannot promote research-only profiles.
/// </summary>
public static class SpecialChestStructuralPlanner
{
    public static SpecialChestStructuralPlan Plan(SpecialChestStructuralPlanningRequest request)
    {
        SpecialChestBundleProfile? profile = SpecialChestBundleProfileRegistry.Find(
            request.Family,
            request.TargetLevelKey,
            request.DiscImageSha256);
        if (profile == null)
        {
            string normalizedTarget = LevelCatalog.NormalizeKey(request.TargetLevelKey);
            return new SpecialChestStructuralPlan(
                request,
                Profile: null,
                Status: SpecialChestStructuralPlanStatus.Blocked,
                CanWriteNormalBuild: false,
                CanStageResearchCandidate: false,
                ProjectedSourceRecordCount: null,
                ProjectedTreasureDelta: null,
                ProjectedLifeDelta: null,
                ProjectedWadGrowthBytes: null,
                Issues:
                [
                    new SpecialChestSafetyIssue(
                        "unsupported-disc-or-level-profile",
                        SpecialChestSafetySeverity.Block,
                        $"No {request.Family} profile exists for target '{normalizedTarget}' and disc SHA-256 '{request.DiscImageSha256}'. Only exact checked disc fingerprints may use structural chest plans.")
                ]);
        }

        List<SpecialChestSafetyIssue> issues =
            SpecialChestStructuralSafetyValidator.Validate(profile, request).ToList();
        AddEvidenceGate(profile, request, issues);

        SpecialChestStructuralPlanStatus status = issues.Any(issue => issue.Severity == SpecialChestSafetySeverity.Block)
            ? SpecialChestStructuralPlanStatus.Blocked
            : issues.Any(issue => issue.Severity == SpecialChestSafetySeverity.Review)
                ? SpecialChestStructuralPlanStatus.Review
                : SpecialChestStructuralPlanStatus.Ready;

        int? projectedSourceRecordCount = profile.StructuralBudget.SourceRowsAdded is int rows
            ? checked(request.ExistingSourceRecordCount + (rows * request.RequestedBundleInstances))
            : null;
        int? projectedTreasureDelta = profile.StructuralBudget.TreasureDelta is int treasure
            ? checked(treasure * request.RequestedBundleInstances)
            : null;
        int? projectedLifeDelta = profile.Declaration.Reward.LifeDelta is int life &&
            profile.Declaration.Reward.Evidence != SpecialChestComponentEvidence.Unmapped
            ? checked(life * request.RequestedBundleInstances)
            : null;
        int? projectedWadGrowth = profile.StructuralBudget.WadGrowthBytes is int growth
            ? checked(growth * request.RequestedBundleInstances)
            : null;

        bool canWriteNormalBuild = status == SpecialChestStructuralPlanStatus.Ready && profile.NormalCreateBinReady;
        bool canStageResearchCandidate =
            status != SpecialChestStructuralPlanStatus.Blocked &&
            request.AllowResearchCandidate &&
            profile.Availability is SpecialChestBundleAvailability.CandidatePlanOnly or SpecialChestBundleAvailability.NativeClosurePresent;

        return new SpecialChestStructuralPlan(
            request,
            profile,
            status,
            canWriteNormalBuild,
            canStageResearchCandidate,
            projectedSourceRecordCount,
            projectedTreasureDelta,
            projectedLifeDelta,
            projectedWadGrowth,
            issues);
    }

    private static void AddEvidenceGate(
        SpecialChestBundleProfile profile,
        SpecialChestStructuralPlanningRequest request,
        List<SpecialChestSafetyIssue> issues)
    {
        switch (profile.Availability)
        {
            case SpecialChestBundleAvailability.NormalCreateBinReady:
                issues.Add(new SpecialChestSafetyIssue(
                    "runtime-proven-profile",
                    SpecialChestSafetySeverity.Info,
                    $"Profile '{profile.Id}' is tied to exporter feature '{profile.RequiredExporterFeature}' and runtime proof {profile.RuntimeProofOutputSha256}."));
                break;

            case SpecialChestBundleAvailability.CandidatePlanOnly:
                issues.Add(new SpecialChestSafetyIssue(
                    request.AllowResearchCandidate ? "research-candidate-review" : "normal-build-runtime-proof-required",
                    request.AllowResearchCandidate ? SpecialChestSafetySeverity.Review : SpecialChestSafetySeverity.Block,
                    request.AllowResearchCandidate
                        ? $"Profile '{profile.Id}' may produce planning evidence only. It still requires a disposable exact-target BIN and focused DuckStation proof before promotion."
                        : $"Profile '{profile.Id}' is static plan-only evidence and is blocked from normal Create BIN."));
                break;

            case SpecialChestBundleAvailability.NativeClosurePresent:
                issues.Add(new SpecialChestSafetyIssue(
                    request.AllowResearchCandidate ? "native-instance-review" : "native-instance-proof-required",
                    request.AllowResearchCandidate ? SpecialChestSafetySeverity.Review : SpecialChestSafetySeverity.Block,
                    request.AllowResearchCandidate
                        ? "The family exists natively in this level, but a new instance still needs independent properties/companions and focused runtime proof."
                        : "Native retail presence is not proof that an arbitrary added copy is safe; normal Create BIN remains blocked until an instance recipe passes runtime smoke."));
                break;

            case SpecialChestBundleAvailability.Blocked:
                issues.Add(new SpecialChestSafetyIssue(
                    "dependency-closure-incomplete",
                    SpecialChestSafetySeverity.Block,
                    $"Profile '{profile.Id}' is blocked: {profile.EvidenceNote} Required work: {string.Join(" ", profile.RequiredWork)}"));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(profile));
        }
    }
}

/// <summary>
/// Shared structural checks kept separate from UI and image-writing code.  A
/// future Build Safety hook can surface these exact issue codes and messages;
/// a future composition coordinator can refuse plans with Block severity.
/// </summary>
public static class SpecialChestStructuralSafetyValidator
{
    public static IReadOnlyList<SpecialChestSafetyIssue> Validate(
        SpecialChestBundleProfile profile,
        SpecialChestStructuralPlanningRequest request)
    {
        List<SpecialChestSafetyIssue> issues = [];
        string target = LevelCatalog.NormalizeKey(request.TargetLevelKey);
        if (profile.Family != request.Family ||
            !string.Equals(profile.TargetLevelKey, target, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(profile.Disc.ImageSha256, request.DiscImageSha256, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Block(
                "profile-key-mismatch",
                "The selected special-chest profile does not match the requested family, target level, and disc fingerprint."));
            return issues;
        }

        if (request.RequestedBundleInstances <= 0)
            issues.Add(Block("invalid-instance-count", "A special-chest plan must request at least one bundle instance."));
        if (request.ExistingBundleInstances < 0)
            issues.Add(Block("invalid-existing-bundle-count", "The existing bundle-instance count cannot be negative."));
        if (request.ExistingSourceRecordCount < 0 || request.SourceRecordLimit <= 0)
            issues.Add(Block("invalid-source-record-budget", "Source-record counts must be non-negative and the record limit must be positive."));
        if (issues.Any(issue => issue.Severity == SpecialChestSafetySeverity.Block))
            return issues;

        SpecialChestStructuralBudget budget = profile.StructuralBudget;
        int totalInstances = checked(request.ExistingBundleInstances + request.RequestedBundleInstances);
        if (budget.MaxBundleInstances is int maxInstances && totalInstances > maxInstances)
        {
            issues.Add(Block(
                "bundle-instance-limit",
                $"Profile '{profile.Id}' permits at most {maxInstances} bundle instance(s); the plan would contain {totalInstances}."));
        }

        if (budget.SourceRowsAdded is int sourceRowsAdded)
        {
            int projected = checked(request.ExistingSourceRecordCount + (sourceRowsAdded * request.RequestedBundleInstances));
            if (projected > request.SourceRecordLimit)
            {
                issues.Add(Block(
                    "source-record-limit",
                    $"The plan projects {projected} source rows, beyond the configured limit of {request.SourceRecordLimit}."));
            }
        }
        else
        {
            issues.Add(Review(
                "source-row-budget-unmapped",
                "This profile has no exact source-row allocation yet; it cannot be promoted until the row and runtime-slot budgets are frozen."));
        }

        if (!budget.Exact)
        {
            issues.Add(Review(
                "structural-budget-unmapped",
                "WAD growth, runtime slots, treasure/life delta, pointer fixups, and instance limits are not yet an exact checked budget."));
        }

        int unmappedComponents = profile.Declaration.ComponentEvidence()
            .Count(evidence => evidence == SpecialChestComponentEvidence.Unmapped);
        if (unmappedComponents > 0)
        {
            issues.Add(Review(
                "declarative-closure-unmapped",
                $"Profile '{profile.Id}' still has {unmappedComponents} unmapped row/package/registration/code/allocation/resource/reward declaration(s)."));
        }

        if (profile.Constraints.DisallowOtherObjectEdits && request.HasOtherObjectEdits)
        {
            issues.Add(Block(
                "object-edit-allocation-conflict",
                $"Profile '{profile.Id}' reserves an atomic source/scene allocation and cannot currently be combined with other object edits in the same target level."));
        }

        if (profile.Constraints.DisallowOtherStructuralRelocations && request.HasOtherStructuralRelocations)
        {
            issues.Add(Block(
                "structural-relocation-conflict",
                $"Profile '{profile.Id}' owns a checked WAD/executable relocation and cannot be stacked with another structural relocation yet."));
        }

        if (profile.Constraints.AtomicBundle)
        {
            issues.Add(new SpecialChestSafetyIssue(
                "atomic-bundle",
                SpecialChestSafetySeverity.Info,
                "Visible shell/key objects, hidden companions, reward rows, properties, handlers, and textures must be saved, moved, undone, and exported as one bundle."));
        }

        return issues;
    }

    private static SpecialChestSafetyIssue Block(string code, string message) =>
        new(code, SpecialChestSafetySeverity.Block, message);

    private static SpecialChestSafetyIssue Review(string code, string message) =>
        new(code, SpecialChestSafetySeverity.Review, message);
}

using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public static class MobyBuildSafetyInspector
{
    public const int MobyRecordStride = 0x58;
    public const int DynamicPropsBytes = 24;
    public const int DynamicAllocationBytes = MobyRecordStride + DynamicPropsBytes;
    public const int PersistentStaticMobyCapacity = 256;

    private const int WadLba = 37;
    private const int MaximumPlausibleComponentBytes = 32 * 1024 * 1024;

    public const string LoaderSourceUrl =
        "https://github.com/TheMobyCollective/spyro-1/blob/4e7b7f06e552a20b9fad76e8ee67e68027b39082/src/loaders.c#L630-L648";
    public const string AllocatorSourceUrl =
        "https://github.com/TheMobyCollective/spyro-1/blob/4e7b7f06e552a20b9fad76e8ee67e68027b39082/asm/42CC4.s#L12-L57";
    public const string CheckpointSourceUrl =
        "https://github.com/TheMobyCollective/spyro-1/blob/4e7b7f06e552a20b9fad76e8ee67e68027b39082/include/checkpoint.h#L17-L21";
    public const string SpawnSourceUrl =
        "https://github.com/TheMobyCollective/spyro-1/blob/4e7b7f06e552a20b9fad76e8ee67e68027b39082/include/overlays/moby_spawn.inc.h#L13-L71";

    public static MobyBuildSafetyReport Inspect(
        string sourceImagePath,
        IReadOnlyList<MobyBuildSafetyInput> inputs)
    {
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image for the build safety inspection.", sourceImagePath);

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        List<MobyBuildSafetyLevelReport> levels = [];
        foreach (MobyBuildSafetyInput input in inputs)
        {
            if (!string.IsNullOrWhiteSpace(input.PlanError))
            {
                levels.Add(BuildUnresolvedLevelReport(input.Level, input.Plan, input.PlanError));
                continue;
            }

            try
            {
                levels.Add(InspectLevel(stream, layout, input.Level, input.Plan));
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or OverflowException)
            {
                levels.Add(BuildUnresolvedLevelReport(input.Level, input.Plan, ex.Message));
            }
        }

        MobyBuildSafetyStatus overall = levels.Count == 0
            ? MobyBuildSafetyStatus.Stable
            : levels.Max(level => level.Status);
        return new MobyBuildSafetyReport(
            GeneratedAt: DateTimeOffset.Now,
            SourceImagePath: Path.GetFullPath(sourceImagePath),
            Status: overall,
            Levels: levels,
            SourceReferences:
            [
                LoaderSourceUrl,
                AllocatorSourceUrl,
                CheckpointSourceUrl,
                SpawnSourceUrl
            ]);
    }

    public static MobyBuildSafetyLevelReport InspectLevel(
        string sourceImagePath,
        LevelDefinition level,
        MobySourcePatchPlan plan)
    {
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        return InspectLevel(stream, layout, level, plan);
    }

    private static MobyBuildSafetyLevelReport InspectLevel(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        MobySourcePatchPlan plan)
    {
        long tableWadOffset = ParseOffset(level.SourceTableWadOffset, "source moby table");
        NativeMobyComponentLayout native = LocateNativeMobyComponent(stream, layout, level, tableWadOffset);

        MobySourcePatch? countPatch = plan.Patches.LastOrDefault(patch =>
            string.Equals(patch.Kind, "moby-source-count", StringComparison.OrdinalIgnoreCase));
        int plannedRuntimeCount = countPatch != null && TryParseLittleEndianInt32(countPatch.AfterHexPreview, out int patchedCount)
            ? patchedCount
            : native.RuntimeSourceCount;

        int highestPlannedCatalogIndex = plan.Patches
            .Where(patch => IsSourceRecordAppend(patch, plan.RecordStride))
            .Select(patch => patch.TrueIndex + 1)
            .DefaultIfEmpty(0)
            .Max();
        if (highestPlannedCatalogIndex > 0)
            plannedRuntimeCount = Math.Max(plannedRuntimeCount, highestPlannedCatalogIndex - native.CatalogPrefixRows);

        ArtisansNativeLockedChestRuntimeBundleIntent? artisansLockedChestBundle = plan.ArtisansNativeLockedChestRuntimeBundle;
        if (artisansLockedChestBundle != null)
            plannedRuntimeCount = Math.Max(plannedRuntimeCount, artisansLockedChestBundle.SourceRuntimeCountAfter);

        plannedRuntimeCount = Math.Max(native.RuntimeSourceCount, plannedRuntimeCount);
        int plannedCatalogCount = checked(plannedRuntimeCount + native.CatalogPrefixRows);
        int trueAppendCount = Math.Max(0, plannedRuntimeCount - native.RuntimeSourceCount);
        int sourceSlotReuseCount = plan.Patches.Count(patch =>
            patch.Kind.Contains("slot", StringComparison.OrdinalIgnoreCase) &&
            (patch.Kind.Contains("clone", StringComparison.OrdinalIgnoreCase) ||
             patch.Kind.Contains("reuse", StringComparison.OrdinalIgnoreCase)));
        bool componentRepacked = plan.Patches.Any(patch =>
            patch.Kind.Contains("moby-component-repack", StringComparison.OrdinalIgnoreCase) ||
            patch.Kind.Contains("moby-arena-repack", StringComparison.OrdinalIgnoreCase));

        long projectedArenaBytesLong = componentRepacked
            ? native.DynamicArenaBytes
            : native.ComponentEndWadOffset -
              (native.RuntimeRowsWadOffset + ((long)plannedRuntimeCount * MobyRecordStride));
        int projectedArenaBytes = projectedArenaBytesLong is > int.MaxValue or < int.MinValue
            ? int.MinValue
            : (int)projectedArenaBytesLong;
        int nativeDynamicCapacity = native.DynamicArenaBytes / DynamicAllocationBytes;
        int projectedDynamicCapacity = projectedArenaBytes < 0
            ? -1
            : projectedArenaBytes / DynamicAllocationBytes;
        int runtimeSlotsConsumed = projectedDynamicCapacity < 0
            ? nativeDynamicCapacity
            : Math.Max(0, nativeDynamicCapacity - projectedDynamicCapacity);
        int persistentIndexHeadroom = PersistentStaticMobyCapacity - plannedRuntimeCount;

        List<string> findings = [];
        List<string> recommendations = [];
        MobyBuildSafetyStatus status = MobyBuildSafetyStatus.Stable;

        if (trueAppendCount == 0)
        {
            findings.Add("No true source-row append is planned; the native shared runtime Moby/props arena remains unchanged.");
        }
        else if (componentRepacked)
        {
            findings.Add($"{trueAppendCount} true source-row append(s) are paired with a Moby component repack, preserving the native runtime arena.");
        }
        else
        {
            status = MobyBuildSafetyStatus.Review;
            findings.Add($"{trueAppendCount} true source-row append(s) consume {trueAppendCount * MobyRecordStride:N0} bytes from the level's shared runtime Moby/props arena.");
            findings.Add($"The projected dynamic allocation budget changes from {nativeDynamicCapacity} to {Math.Max(0, projectedDynamicCapacity)} slot(s); {runtimeSlotsConsumed} runtime slot(s) are lost after allocator rounding.");
            recommendations.Add("Prefer source-slot replacement for stability until the Level Moby component repacker is available.");
        }

        if (native.RuntimeRowAlignmentDeltaBytes != 0)
        {
            findings.Add(
                $"This level's native runtime rows begin {Math.Abs(native.RuntimeRowAlignmentDeltaBytes)} byte(s) " +
                $"{(native.RuntimeRowAlignmentDeltaBytes < 0 ? "before" : "after")} the catalog-aligned prefix boundary.");
            if (trueAppendCount > 0 && !componentRepacked)
            {
                status = MobyBuildSafetyStatus.Blocked;
                findings.Add("The current catalog-stride append location would not align with the native runtime Moby array.");
                recommendations.Add("Keep edits within existing source rows for this level until its source-table alignment is handled by a component repacker.");
            }
        }

        if (plannedRuntimeCount > PersistentStaticMobyCapacity)
        {
            status = MobyBuildSafetyStatus.Blocked;
            findings.Add($"The build plans {plannedRuntimeCount} runtime static Mobys, exceeding the engine's {PersistentStaticMobyCapacity}-index killed/collected bookkeeping range.");
            recommendations.Add("Reduce static source rows or use a separately proven dynamic-spawn strategy; do not write this build with the stock persistent-state arrays.");
        }
        else if (persistentIndexHeadroom == 0)
        {
            status = plan.SkippedEdits.Any(IsBlockingNativeMovementRejection)
                ? MobyBuildSafetyStatus.Blocked
                : Max(status, MobyBuildSafetyStatus.Review);
            findings.Add("All 256 persistent static Moby indexes would be occupied; there is no bookkeeping headroom for another source row.");
        }

        if (projectedArenaBytes < 0)
        {
            status = MobyBuildSafetyStatus.Blocked;
            findings.Add("The planned static rows extend past the native Level Moby component boundary.");
            recommendations.Add("A component repack is required before these rows can be exported.");
        }
        else if (trueAppendCount > 0 && projectedDynamicCapacity <= 0)
        {
            status = MobyBuildSafetyStatus.Blocked;
            findings.Add("No complete runtime Moby plus 24-byte props allocation remains after the planned appends.");
            recommendations.Add("Remove true appends or repack the component while preserving its original runtime arena.");
        }

        if (plan.SkippedEdits.Count > 0)
        {
            status = Max(status, MobyBuildSafetyStatus.Review);
            findings.Add($"{plan.SkippedEdits.Count} saved object edit(s) are guarded and will not be written by normal Create BIN.");
            recommendations.Add("Review skipped edits before testing so the editor view and generated BIN are not mistaken for an exact match.");
        }

        MobyActorPackageImportPreview[] blockedPackageImports = plan.PackageImportPreviews
            .Where(preview =>
                preview.RecipeStatus.StartsWith("in-game-blocked", StringComparison.OrdinalIgnoreCase) ||
                preview.RecipeStatus.StartsWith("blocked", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (blockedPackageImports.Length > 0)
        {
            status = MobyBuildSafetyStatus.Blocked;
            findings.Add($"{blockedPackageImports.Length} actor-package recipe(s) are blocked by failed in-game behavior validation: {string.Join(", ", blockedPackageImports.Select(preview => preview.RecipeId))}.");
            recommendations.Add("Remove or replace the blocked cross-level object before creating a BIN; candidate mode cannot override a known behavior failure.");
        }
        else if (plan.PackageImportPreviews.Any(preview => !preview.CanWriteImage))
        {
            status = Max(status, MobyBuildSafetyStatus.Review);
            findings.Add("At least one actor-package or cross-level dependency remains test-only.");
        }

        if (sourceSlotReuseCount > 0)
            findings.Add($"{sourceSlotReuseCount} object add/copy operation(s) reuse existing source slots and do not increase the static source count.");

        if (trueAppendCount > 0)
        {
            findings.Add("A copied 0x58-byte source row is not equivalent to the game's class-specific SpawnMoby initialization; linked props, pods, routes, controllers, and rewards remain family-specific requirements.");
        }

        if (artisansLockedChestBundle != null)
        {
            findings.Add(
                $"The runtime-proven Artisans native Key + Locked Chest V2 bundle reserves T174-T180 atomically: two visible objects plus five hidden reward-marker rows, with matching scene props and a fixed +{artisansLockedChestBundle.TreasureDelta} treasure contract.");
            recommendations.Add("Keep this first promoted bundle as exactly one Key/chest pair; standalone copies, duplicate pairs, and additional Artisans object edits are intentionally blocked before export.");
        }

        if (status == MobyBuildSafetyStatus.Stable)
            recommendations.Add("The inspected object patch plan preserves both the native static bookkeeping range and runtime allocation capacity.");

        List<MobyBuildSafetyIssue> issues = BuildIssues(
            level,
            plan,
            status,
            trueAppendCount,
            componentRepacked,
            plannedRuntimeCount,
            nativeDynamicCapacity,
            projectedDynamicCapacity,
            projectedArenaBytes,
            native.RuntimeRowAlignmentDeltaBytes);
        if (issues.Count > 0)
            status = (MobyBuildSafetyStatus)Math.Max((int)status, (int)issues.Max(issue => issue.Status));

        return new MobyBuildSafetyLevelReport(
            LevelKey: level.Key,
            LevelName: level.DisplayName,
            Status: status,
            CatalogSourceRecordCount: level.SourceRecordCount,
            SourceRuntimeRecordCount: native.RuntimeSourceCount,
            SourceCatalogRecordCount: native.RuntimeSourceCount + native.CatalogPrefixRows,
            PlannedRuntimeRecordCount: plannedRuntimeCount,
            PlannedCatalogRecordCount: plannedCatalogCount,
            CatalogPrefixRows: native.CatalogPrefixRows,
            TrueAppendCount: trueAppendCount,
            SourceSlotReuseCount: sourceSlotReuseCount,
            SkippedEditCount: plan.SkippedEdits.Count,
            ComponentWadOffset: native.ComponentStartWadOffset,
            ComponentByteLength: native.ComponentByteLength,
            SourceDynamicArenaBytes: native.DynamicArenaBytes,
            ProjectedDynamicArenaBytes: Math.Max(0, projectedArenaBytes),
            SourceDynamicCapacity: nativeDynamicCapacity,
            ProjectedDynamicCapacity: Math.Max(0, projectedDynamicCapacity),
            RuntimeSlotsConsumed: runtimeSlotsConsumed,
            PersistentIndexHeadroom: persistentIndexHeadroom,
            RuntimeRowAlignmentDeltaBytes: native.RuntimeRowAlignmentDeltaBytes,
            ComponentRepacked: componentRepacked,
            Issues: issues,
            Findings: findings,
            Recommendations: recommendations);
    }

    private static List<MobyBuildSafetyIssue> BuildIssues(
        LevelDefinition level,
        MobySourcePatchPlan plan,
        MobyBuildSafetyStatus levelStatus,
        int trueAppendCount,
        bool componentRepacked,
        int plannedRuntimeCount,
        int nativeDynamicCapacity,
        int projectedDynamicCapacity,
        int projectedArenaBytes,
        int runtimeRowAlignmentDeltaBytes)
    {
        List<MobyBuildSafetyIssue> issues = [];
        IReadOnlyList<MobySourceEditOutcome> outcomes = plan.EditOutcomes ?? [];
        HashSet<string> representedSkippedReasons = new(StringComparer.Ordinal);
        HashSet<string> representedPackageRecipes = new(StringComparer.OrdinalIgnoreCase);
        bool appendBlocked = !componentRepacked && trueAppendCount > 0 &&
            (runtimeRowAlignmentDeltaBytes != 0 ||
             plannedRuntimeCount > PersistentStaticMobyCapacity ||
             projectedArenaBytes < 0 ||
             projectedDynamicCapacity <= 0);

        foreach (MobySourceEditOutcome outcome in outcomes)
        {
            bool packageBlocked = outcome.PackageOutcomes.Any(package => IsBlockedPackageStatus(package.RecipeStatus));
            foreach (MobySourceEditSafetyFinding finding in outcome.SafetyFindings ?? [])
            {
                issues.Add(new MobyBuildSafetyIssue(
                    Code: finding.Code,
                    Status: finding.Status,
                    Message: finding.Message,
                    LevelKey: level.Key,
                    LevelName: level.DisplayName,
                    EditorTrueIndex: outcome.EditorTrueIndex >= 0 ? outcome.EditorTrueIndex : null,
                    MobyLabel: outcome.MobyLabel));
            }
            foreach (string skippedReason in outcome.SkippedReasons)
            {
                representedSkippedReasons.Add(skippedReason);
                bool nativeMovementBlocked = IsBlockingNativeMovementRejection(skippedReason);
                issues.Add(new MobyBuildSafetyIssue(
                    Code: "skipped-edit",
                    Status: packageBlocked || nativeMovementBlocked
                        ? MobyBuildSafetyStatus.Blocked
                        : MobyBuildSafetyStatus.Review,
                    Message: TrimMobyLabelPrefix(skippedReason, outcome.MobyLabel),
                    LevelKey: level.Key,
                    LevelName: level.DisplayName,
                    EditorTrueIndex: outcome.EditorTrueIndex >= 0 ? outcome.EditorTrueIndex : null,
                    MobyLabel: outcome.MobyLabel));
            }

            foreach (MobySourceEditPackageOutcome package in outcome.PackageOutcomes)
            {
                representedPackageRecipes.Add(PackageRecipeKey(package.RecipeId, package.TemplateId));
                bool blocked = IsBlockedPackageStatus(package.RecipeStatus);
                if (!blocked && package.CanWriteImage)
                    continue;

                string recipe = string.IsNullOrWhiteSpace(package.RecipeId) ? package.TemplateId : package.RecipeId;
                string reason = string.IsNullOrWhiteSpace(package.GuardReason)
                    ? blocked
                        ? "its in-game behavior validation is blocked"
                        : "its actor-package dependency is still test-only"
                    : package.GuardReason;
                issues.Add(new MobyBuildSafetyIssue(
                    Code: blocked ? "actor-package-blocked" : "actor-package-test-only",
                    Status: blocked ? MobyBuildSafetyStatus.Blocked : MobyBuildSafetyStatus.Review,
                    Message: $"Actor package {recipe}: {reason}",
                    LevelKey: level.Key,
                    LevelName: level.DisplayName,
                    EditorTrueIndex: outcome.EditorTrueIndex >= 0 ? outcome.EditorTrueIndex : null,
                    MobyLabel: outcome.MobyLabel));
            }

            if (!componentRepacked && outcome.PatchKinds.Any(IsMobyRecordAppendKind))
            {
                int slotsLost = Math.Max(0, nativeDynamicCapacity - Math.Max(0, projectedDynamicCapacity));
                string message = appendBlocked
                    ? BuildBlockedAppendMessage(plannedRuntimeCount, projectedArenaBytes, projectedDynamicCapacity, runtimeRowAlignmentDeltaBytes)
                    : $"This added Moby uses a new 0x{MobyRecordStride:X}-byte source row in the shared runtime arena; the level loses {slotsLost} allocator slot(s) across all planned appends.";
                issues.Add(new MobyBuildSafetyIssue(
                    Code: appendBlocked ? "source-row-append-blocked" : "source-row-append",
                    Status: appendBlocked ? MobyBuildSafetyStatus.Blocked : MobyBuildSafetyStatus.Review,
                    Message: message,
                    LevelKey: level.Key,
                    LevelName: level.DisplayName,
                    EditorTrueIndex: outcome.EditorTrueIndex >= 0 ? outcome.EditorTrueIndex : null,
                    MobyLabel: outcome.MobyLabel));
            }
        }

        if (plan.ArtisansNativeLockedChestRuntimeBundle is ArtisansNativeLockedChestRuntimeBundleIntent lockedChestBundle)
        {
            int slotsLost = Math.Max(0, nativeDynamicCapacity - Math.Max(0, projectedDynamicCapacity));
            issues.Add(new MobyBuildSafetyIssue(
                Code: "artisans-native-locked-chest-runtime-bundle-v2",
                Status: MobyBuildSafetyStatus.Review,
                Message: $"Runtime-proven atomic Key + Locked Chest V2 reserves output T174-T180 (7 source rows / {lockedChestBundle.AppendedSourceRowCount * MobyRecordStride:N0} bytes). The projected dynamic allocator capacity is {nativeDynamicCapacity}->{Math.Max(0, projectedDynamicCapacity)} ({slotsLost} slot(s) consumed); the handler, reward props, +10 treasure, model, and private textures are resolved rather than an unresolved package risk.",
                LevelKey: level.Key,
                LevelName: level.DisplayName,
                EditorTrueIndex: lockedChestBundle.LockedChestEditorTrueIndex,
                MobyLabel: lockedChestBundle.LockedChestLabel));
        }

        if (outcomes.Count == 0 && !componentRepacked)
        {
            foreach (MobySourcePatch patch in plan.Patches.Where(patch => IsSourceRecordAppend(patch, plan.RecordStride)))
            {
                int slotsLost = Math.Max(0, nativeDynamicCapacity - Math.Max(0, projectedDynamicCapacity));
                issues.Add(new MobyBuildSafetyIssue(
                    Code: appendBlocked ? "source-row-append-blocked" : "source-row-append",
                    Status: appendBlocked ? MobyBuildSafetyStatus.Blocked : MobyBuildSafetyStatus.Review,
                    Message: appendBlocked
                        ? BuildBlockedAppendMessage(plannedRuntimeCount, projectedArenaBytes, projectedDynamicCapacity, runtimeRowAlignmentDeltaBytes)
                        : $"This added Moby uses a new 0x{MobyRecordStride:X}-byte source row in the shared runtime arena; the level loses {slotsLost} allocator slot(s) across all planned appends.",
                    LevelKey: level.Key,
                    LevelName: level.DisplayName,
                    EditorTrueIndex: patch.TrueIndex >= 0 ? patch.TrueIndex : null,
                    MobyLabel: patch.MobyLabel));
            }
        }

        foreach (string skippedReason in plan.SkippedEdits.Where(reason => !representedSkippedReasons.Contains(reason)))
        {
            bool nativeMovementBlocked = IsBlockingNativeMovementRejection(skippedReason);
            issues.Add(new MobyBuildSafetyIssue(
                Code: "skipped-edit-unresolved",
                Status: nativeMovementBlocked ? MobyBuildSafetyStatus.Blocked : MobyBuildSafetyStatus.Review,
                Message: skippedReason,
                LevelKey: level.Key,
                LevelName: level.DisplayName,
                EditorTrueIndex: null,
                MobyLabel: ""));
        }

        foreach (MobyActorPackageImportPreview preview in plan.PackageImportPreviews)
        {
            string key = PackageRecipeKey(preview.RecipeId, preview.TemplateId);
            if (representedPackageRecipes.Contains(key))
                continue;
            bool blocked = IsBlockedPackageStatus(preview.RecipeStatus);
            if (!blocked && preview.CanWriteImage)
                continue;

            issues.Add(new MobyBuildSafetyIssue(
                Code: blocked ? "actor-package-blocked-unresolved" : "actor-package-test-only-unresolved",
                Status: blocked ? MobyBuildSafetyStatus.Blocked : MobyBuildSafetyStatus.Review,
                Message: string.IsNullOrWhiteSpace(preview.GuardReason)
                    ? $"Actor package {preview.RecipeId} is {(blocked ? "blocked" : "test-only")}."
                    : preview.GuardReason,
                LevelKey: level.Key,
                LevelName: level.DisplayName,
                EditorTrueIndex: null,
                MobyLabel: preview.Label));
        }

        MobyBuildSafetyStatus highestIssueStatus = issues.Count == 0
            ? MobyBuildSafetyStatus.Stable
            : issues.Max(issue => issue.Status);
        if (levelStatus > highestIssueStatus)
        {
            issues.Insert(0, new MobyBuildSafetyIssue(
                Code: "level-wide-safety-limit",
                Status: levelStatus,
                Message: BuildLevelWideIssueMessage(
                    levelStatus,
                    trueAppendCount,
                    plannedRuntimeCount,
                    projectedArenaBytes,
                    projectedDynamicCapacity,
                    runtimeRowAlignmentDeltaBytes),
                LevelKey: level.Key,
                LevelName: level.DisplayName,
                EditorTrueIndex: null,
                MobyLabel: ""));
        }

        return issues
            .DistinctBy(issue => (issue.Code, issue.EditorTrueIndex, issue.MobyLabel, issue.Message))
            .OrderByDescending(issue => issue.Status)
            .ThenBy(issue => issue.EditorTrueIndex ?? int.MaxValue)
            .ThenBy(issue => issue.MobyLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsMobyRecordAppendKind(string kind) =>
        string.Equals(kind, "moby-record-append", StringComparison.OrdinalIgnoreCase);

    private static bool IsBlockingNativeMovementRejection(string reason)
    {
        bool movement = reason.Contains("run-to", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("dragon rescue scene", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("egg-thief path", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("egg thief", StringComparison.OrdinalIgnoreCase);
        if (!movement)
            return false;
        return reason.Contains("no longer matches", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("not present", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("zero", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("overflow", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("outside", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlockedPackageStatus(string status) =>
        status.StartsWith("in-game-blocked", StringComparison.OrdinalIgnoreCase) ||
        status.StartsWith("blocked", StringComparison.OrdinalIgnoreCase);

    private static string PackageRecipeKey(string recipeId, string templateId) =>
        $"{recipeId}\u001F{templateId}";

    private static string TrimMobyLabelPrefix(string message, string label)
    {
        string prefix = $"{label}:";
        return message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? message[prefix.Length..].TrimStart()
            : message;
    }

    private static string BuildBlockedAppendMessage(
        int plannedRuntimeCount,
        int projectedArenaBytes,
        int projectedDynamicCapacity,
        int runtimeRowAlignmentDeltaBytes)
    {
        if (runtimeRowAlignmentDeltaBytes != 0)
            return "This added Moby needs a new source row, but this level's native runtime-row alignment is not currently safe for appends.";
        if (plannedRuntimeCount > PersistentStaticMobyCapacity)
            return $"This added Moby contributes to {plannedRuntimeCount} static rows, beyond the engine's {PersistentStaticMobyCapacity}-index persistent bookkeeping limit.";
        if (projectedArenaBytes < 0)
            return "This added Moby would extend the static source rows past the native Level Moby component boundary.";
        if (projectedDynamicCapacity <= 0)
            return "This added Moby leaves no complete runtime Moby plus props allocation in the shared arena.";
        return "This added Moby requires a new source row that is blocked by the level's current safety limits.";
    }

    private static string BuildLevelWideIssueMessage(
        MobyBuildSafetyStatus status,
        int trueAppendCount,
        int plannedRuntimeCount,
        int projectedArenaBytes,
        int projectedDynamicCapacity,
        int runtimeRowAlignmentDeltaBytes)
    {
        if (trueAppendCount > 0 && status == MobyBuildSafetyStatus.Blocked)
            return BuildBlockedAppendMessage(plannedRuntimeCount, projectedArenaBytes, projectedDynamicCapacity, runtimeRowAlignmentDeltaBytes);
        if (plannedRuntimeCount > PersistentStaticMobyCapacity)
            return $"The build plans {plannedRuntimeCount} static rows, beyond the engine's {PersistentStaticMobyCapacity}-index persistent bookkeeping limit.";
        if (status == MobyBuildSafetyStatus.Review && trueAppendCount > 0)
            return $"{trueAppendCount} new source row(s) consume space in the shared runtime Moby/props arena.";
        return status == MobyBuildSafetyStatus.Blocked
            ? "This level has a blocking build-safety condition that is not tied to one editable Moby."
            : "This level has a build-safety condition that needs review and is not tied to one editable Moby.";
    }

    private static MobyBuildSafetyLevelReport BuildUnresolvedLevelReport(
        LevelDefinition level,
        MobySourcePatchPlan plan,
        string reason)
    {
        int appendPatches = plan.Patches.Count(patch => IsSourceRecordAppend(patch, plan.RecordStride));
        return new MobyBuildSafetyLevelReport(
            LevelKey: level.Key,
            LevelName: level.DisplayName,
            Status: MobyBuildSafetyStatus.Blocked,
            CatalogSourceRecordCount: level.SourceRecordCount,
            SourceRuntimeRecordCount: level.SourceRecordCount,
            SourceCatalogRecordCount: level.SourceRecordCount,
            PlannedRuntimeRecordCount: level.SourceRecordCount + appendPatches,
            PlannedCatalogRecordCount: level.SourceRecordCount + appendPatches,
            CatalogPrefixRows: 0,
            TrueAppendCount: appendPatches,
            SourceSlotReuseCount: 0,
            SkippedEditCount: plan.SkippedEdits.Count,
            ComponentWadOffset: 0,
            ComponentByteLength: 0,
            SourceDynamicArenaBytes: 0,
            ProjectedDynamicArenaBytes: 0,
            SourceDynamicCapacity: 0,
            ProjectedDynamicCapacity: 0,
            RuntimeSlotsConsumed: 0,
            PersistentIndexHeadroom: PersistentStaticMobyCapacity - (level.SourceRecordCount + appendPatches),
            RuntimeRowAlignmentDeltaBytes: 0,
            ComponentRepacked: false,
            Issues:
            [
                new MobyBuildSafetyIssue(
                    Code: "native-component-unresolved",
                    Status: MobyBuildSafetyStatus.Blocked,
                    Message: $"The native Level Moby component could not be resolved safely: {reason}",
                    LevelKey: level.Key,
                    LevelName: level.DisplayName,
                    EditorTrueIndex: null,
                    MobyLabel: "")
            ],
            Findings: [$"The native Level Moby component could not be resolved safely: {reason}"],
            Recommendations: ["Do not create an object build for this level until its native component layout is mapped."]);
    }

    private static NativeMobyComponentLayout LocateNativeMobyComponent(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long tableWadOffset)
    {
        List<NativeMobyComponentLayout> candidates = [];
        for (int countDelta = -4; countDelta <= MobyRecordStride; countDelta += 4)
        {
            TryAddLayoutCandidate(
                stream,
                layout,
                tableWadOffset,
                tableWadOffset + countDelta,
                level.SourceRecordCount,
                candidates);
        }

        NativeMobyComponentLayout? best = candidates
            .OrderByDescending(candidate => candidate.RuntimeSourceCount + candidate.CatalogPrefixRows == level.SourceRecordCount)
            .ThenBy(candidate => Math.Abs(candidate.RuntimeRowAlignmentDeltaBytes))
            .ThenBy(candidate => Math.Abs((candidate.RuntimeSourceCount + candidate.CatalogPrefixRows) - level.SourceRecordCount))
            .ThenBy(candidate => candidate.CatalogPrefixRows)
            .FirstOrDefault();
        if (best == null)
        {
            throw new InvalidOperationException(
                $"Could not locate {level.DisplayName}'s native Level Moby component around WAD offset 0x{tableWadOffset:X}.");
        }

        return best;
    }

    private static void TryAddLayoutCandidate(
        FileStream stream,
        DiscLayout layout,
        long tableWadOffset,
        long countWadOffset,
        int catalogCount,
        List<NativeMobyComponentLayout> candidates)
    {
        try
        {
            int componentLength = ReadWadInt32(stream, layout, countWadOffset - 4);
            int runtimeCount = ReadWadInt32(stream, layout, countWadOffset);
            if (componentLength < 8 || componentLength > MaximumPlausibleComponentBytes || runtimeCount < 0 || runtimeCount > 4096)
                return;

            int catalogPrefixRows = catalogCount - runtimeCount;
            if (catalogPrefixRows is < 0 or > 2)
                return;

            long componentStart = countWadOffset - 4;
            long componentEnd = componentStart + componentLength;
            long rowsWadOffset = countWadOffset + 4;
            long dynamicStart = rowsWadOffset + ((long)runtimeCount * MobyRecordStride);
            long arenaLength = componentEnd - dynamicStart;
            if (arenaLength < 0 || arenaLength > MaximumPlausibleComponentBytes)
                return;

            long catalogAlignedRowsWadOffset = tableWadOffset + ((long)catalogPrefixRows * MobyRecordStride);
            long alignmentDelta = rowsWadOffset - catalogAlignedRowsWadOffset;
            if (alignmentDelta is < -16 or > 16)
                return;

            candidates.Add(new NativeMobyComponentLayout(
                ComponentStartWadOffset: componentStart,
                ComponentEndWadOffset: componentEnd,
                ComponentByteLength: componentLength,
                RuntimeRowsWadOffset: rowsWadOffset,
                RuntimeSourceCount: runtimeCount,
                CatalogPrefixRows: catalogPrefixRows,
                RuntimeRowAlignmentDeltaBytes: checked((int)alignmentDelta),
                DynamicArenaBytes: checked((int)arenaLength)));
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or OverflowException)
        {
        }
    }

    private static int ReadWadInt32(FileStream stream, DiscLayout layout, long wadOffset)
    {
        byte[] bytes = DiscImage.ReadFileBytes(stream, layout, WadLba, wadOffset, 4);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    private static bool IsSourceRecordAppend(MobySourcePatch patch, int recordStride)
    {
        return patch.TrueIndex >= 0 &&
            patch.ByteLength == recordStride &&
            patch.Kind.Contains("append", StringComparison.OrdinalIgnoreCase) &&
            !patch.Kind.Contains("special", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseLittleEndianInt32(string preview, out int value)
    {
        value = 0;
        try
        {
            byte[] bytes = preview
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(4)
                .Select(part => Convert.ToByte(part, 16))
                .ToArray();
            if (bytes.Length != 4)
                return false;
            value = BinaryPrimitives.ReadInt32LittleEndian(bytes);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static long ParseOffset(string text, string label)
    {
        string value = text?.Trim() ?? "";
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToInt64(value[2..], 16);
        if (long.TryParse(value, out long result))
            return result;
        throw new InvalidOperationException($"Invalid {label} offset '{text}'.");
    }

    private static MobyBuildSafetyStatus Max(MobyBuildSafetyStatus left, MobyBuildSafetyStatus right) =>
        left >= right ? left : right;

    private sealed record NativeMobyComponentLayout(
        long ComponentStartWadOffset,
        long ComponentEndWadOffset,
        int ComponentByteLength,
        long RuntimeRowsWadOffset,
        int RuntimeSourceCount,
        int CatalogPrefixRows,
        int RuntimeRowAlignmentDeltaBytes,
        int DynamicArenaBytes);
}

public static class MobyBuildSafetyReportWriter
{
    public static async Task<MobyBuildSafetyWriteResult> WriteAsync(
        MobyBuildSafetyReport report,
        string jsonPath,
        string markdownPath,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath) ?? ".");
        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath) ?? ".");
        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
            Encoding.UTF8,
            cancellationToken);
        await File.WriteAllTextAsync(markdownPath, BuildMarkdown(report), Encoding.UTF8, cancellationToken);
        return new MobyBuildSafetyWriteResult(Path.GetFullPath(jsonPath), Path.GetFullPath(markdownPath));
    }

    private static string BuildMarkdown(MobyBuildSafetyReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Spyro Editor Build Safety");
        builder.AppendLine();
        builder.AppendLine($"- Generated: {report.GeneratedAt:O}");
        builder.AppendLine($"- Source image: `{Path.GetFileName(report.SourceImagePath)}`");
        builder.AppendLine($"- Overall status: **{report.StatusLabel}**");
        builder.AppendLine();
        builder.AppendLine("| Level | Status | Static rows | Runtime capacity | True appends | Slot reuse | Skipped |");
        builder.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (MobyBuildSafetyLevelReport level in report.Levels)
        {
            builder.AppendLine(
                $"| {level.LevelName} | {level.StatusLabel} | {level.SourceRuntimeRecordCount} -> {level.PlannedRuntimeRecordCount} | {level.SourceDynamicCapacity} -> {level.ProjectedDynamicCapacity} | {level.TrueAppendCount} | {level.SourceSlotReuseCount} | {level.SkippedEditCount} |");
        }

        foreach (MobyBuildSafetyLevelReport level in report.Levels)
        {
            builder.AppendLine();
            builder.AppendLine($"## {level.LevelName}: {level.StatusLabel}");
            builder.AppendLine();
            if (level.ComponentByteLength > 0)
            {
                builder.AppendLine($"- Level Moby component: WAD `0x{level.ComponentWadOffset:X}`, {level.ComponentByteLength:N0} bytes");
                builder.AppendLine($"- Shared runtime arena: {level.SourceDynamicArenaBytes:N0} -> {level.ProjectedDynamicArenaBytes:N0} bytes");
                builder.AppendLine($"- Dynamic Moby + 24-byte props capacity: {level.SourceDynamicCapacity} -> {level.ProjectedDynamicCapacity}");
                builder.AppendLine($"- Runtime row alignment delta: {level.RuntimeRowAlignmentDeltaBytes:+#;-#;0} bytes");
            }
            else
            {
                builder.AppendLine("- Level Moby component: unresolved");
            }
            builder.AppendLine($"- Persistent static-index headroom: {level.PersistentIndexHeadroom}");
            builder.AppendLine($"- Component repacked: {(level.ComponentRepacked ? "yes" : "no")}");
            if (level.Issues.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("### Issues");
                foreach (MobyBuildSafetyIssue issue in level.Issues)
                {
                    string target = issue.CanNavigate
                        ? $"T{issue.EditorTrueIndex} {issue.MobyLabel}".TrimEnd()
                        : "Level-wide";
                    builder.AppendLine($"- **{issue.StatusLabel} · {target}:** {issue.Message}");
                }
            }
            builder.AppendLine();
            builder.AppendLine("### Findings");
            foreach (string finding in level.Findings)
                builder.AppendLine($"- {finding}");
            builder.AppendLine();
            builder.AppendLine("### Next actions");
            foreach (string recommendation in level.Recommendations)
                builder.AppendLine($"- {recommendation}");
        }

        builder.AppendLine();
        builder.AppendLine("## Native source references");
        foreach (string source in report.SourceReferences)
            builder.AppendLine($"- {source}");
        builder.AppendLine();
        builder.AppendLine("The report contains editor metadata and byte counts only. It does not include game data.");
        return builder.ToString();
    }
}

public enum MobyBuildSafetyStatus
{
    Stable = 0,
    Review = 1,
    Blocked = 2
}

public sealed record MobyBuildSafetyInput(
    LevelDefinition Level,
    MobySourcePatchPlan Plan,
    string PlanError = "");

public sealed record MobyBuildSafetyReport(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    MobyBuildSafetyStatus Status,
    IReadOnlyList<MobyBuildSafetyLevelReport> Levels,
    IReadOnlyList<string> SourceReferences)
{
    public string StatusLabel => Status switch
    {
        MobyBuildSafetyStatus.Stable => "Stable",
        MobyBuildSafetyStatus.Review => "Review",
        MobyBuildSafetyStatus.Blocked => "Blocked",
        _ => Status.ToString()
    };

    public int TrueAppendCount => Levels.Sum(level => level.TrueAppendCount);
    public int RuntimeSlotsConsumed => Levels.Sum(level => level.RuntimeSlotsConsumed);
    public int SkippedEditCount => Levels.Sum(level => level.SkippedEditCount);
}

public sealed record MobyBuildSafetyLevelReport(
    string LevelKey,
    string LevelName,
    MobyBuildSafetyStatus Status,
    int CatalogSourceRecordCount,
    int SourceRuntimeRecordCount,
    int SourceCatalogRecordCount,
    int PlannedRuntimeRecordCount,
    int PlannedCatalogRecordCount,
    int CatalogPrefixRows,
    int TrueAppendCount,
    int SourceSlotReuseCount,
    int SkippedEditCount,
    long ComponentWadOffset,
    int ComponentByteLength,
    int SourceDynamicArenaBytes,
    int ProjectedDynamicArenaBytes,
    int SourceDynamicCapacity,
    int ProjectedDynamicCapacity,
    int RuntimeSlotsConsumed,
    int PersistentIndexHeadroom,
    int RuntimeRowAlignmentDeltaBytes,
    bool ComponentRepacked,
    IReadOnlyList<MobyBuildSafetyIssue> Issues,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> Recommendations)
{
    public string StatusLabel => Status switch
    {
        MobyBuildSafetyStatus.Stable => "Stable",
        MobyBuildSafetyStatus.Review => "Review",
        MobyBuildSafetyStatus.Blocked => "Blocked",
        _ => Status.ToString()
    };
}

public sealed record MobyBuildSafetyIssue(
    string Code,
    MobyBuildSafetyStatus Status,
    string Message,
    string LevelKey,
    string LevelName,
    int? EditorTrueIndex,
    string MobyLabel)
{
    public bool CanNavigate => EditorTrueIndex is >= 0;
    public string TargetLabel => CanNavigate
        ? $"T{EditorTrueIndex}  {MobyLabel}".TrimEnd()
        : "Level-wide";
    public string StatusLabel => Status switch
    {
        MobyBuildSafetyStatus.Stable => "Stable",
        MobyBuildSafetyStatus.Review => "Review",
        MobyBuildSafetyStatus.Blocked => "Blocked",
        _ => Status.ToString()
    };
}

public sealed record MobyBuildSafetyWriteResult(string JsonPath, string MarkdownPath);

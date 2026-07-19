using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public static class MobyCandidateValidationReportWriter
{
    public static async Task<string> WriteForPlanAsync(string planPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(planPath))
            throw new FileNotFoundException("Missing candidate patch plan.", planPath);

        await using FileStream stream = File.OpenRead(planPath);
        MobySourcePatchPlan? plan = await JsonSerializer.DeserializeAsync<MobySourcePatchPlan>(stream, cancellationToken: cancellationToken);
        if (plan == null)
            throw new InvalidDataException($"Could not read candidate patch plan {planPath}.");

        MobySourcePatchResult result = new(plan.OutputImagePath, plan.OutputCuePath, planPath, plan, File.Exists(plan.OutputImagePath));
        return await WriteAsync(result, cancellationToken);
    }

    public static async Task<string> WriteAsync(MobySourcePatchResult result, CancellationToken cancellationToken = default)
    {
        string reportPath = Path.ChangeExtension(result.OutputCuePath, ".candidate-validation.md");
        MobySourcePatchPlan plan = result.Plan;
        List<MobyActorPackageImportPreview> writablePreviews = plan.PackageImportPreviews
            .Where(preview => preview.CanWriteImage && !string.IsNullOrWhiteSpace(preview.TemplateId))
            .ToList();
        List<MobyActorPackageImportPreview> unwritablePreviews = plan.PackageImportPreviews
            .Where(preview => !preview.CanWriteImage && !string.IsNullOrWhiteSpace(preview.TemplateId))
            .ToList();
        List<MobySourcePatch> existingSlotSwapPatches = plan.Patches
            .Where(patch => string.Equals(patch.Kind, "cross-level-existing-slot-candidate", StringComparison.OrdinalIgnoreCase))
            .ToList();
        MobySourcePatch? fastEntryPatch = plan.Patches.SingleOrDefault(TestLevelWarpPatch.IsPatch);
        string resultPath = await EnsureCandidateResultTemplateAsync(result, writablePreviews, existingSlotSwapPatches, cancellationToken);
        CandidateResultEvidence evidence = ReadCandidateResultEvidence(resultPath);

        StringBuilder builder = new();
        builder.AppendLine("# Candidate Object Test");
        builder.AppendLine();
        builder.AppendLine("This is a disposable emulator test for cross-level objects that are not yet normal-editor proof.");
        builder.AppendLine();
        builder.AppendLine($"- Level: {plan.LevelName}");
        builder.AppendLine($"- CUE: `{result.OutputCuePath}`");
        builder.AppendLine($"- BIN: `{result.OutputImagePath}`");
        builder.AppendLine($"- Patch plan: `{result.OutputPlanPath}`");
        builder.AppendLine($"- Patches: {plan.PatchCount}");
        builder.AppendLine($"- Patched bytes: {plan.TotalPatchedBytes}");
        builder.AppendLine($"- Result file: `{resultPath}`");
        builder.AppendLine();

        if (fastEntryPatch != null && TestLevelWarpPatch.TryGetTargetLevelId(fastEntryPatch, out int targetLevelId))
        {
            builder.AppendLine("## Fast Level Entry");
            builder.AppendLine();
            builder.AppendLine("This disposable Swap Test includes a guarded shortcut to the target level; normal `Create BIN` does not include it.");
            builder.AppendLine();
            builder.AppendLine("1. Boot this candidate CUE and reach any playable level.");
            builder.AppendLine("2. Press **Select** to open Inventory and wait until it accepts input.");
            builder.AppendLine($"3. Enter **{TestLevelWarpPatch.ActivationSequence}**.");
            builder.AppendLine($"4. Press **{TestLevelWarpPatch.TargetSelectionText(targetLevelId)}** to load **{plan.LevelName}** from the mounted candidate.");
            builder.AppendLine();
            builder.AppendLine("The shortcut can be entered again after another level loads, so no per-level save state is required.");
            builder.AppendLine();
        }

        builder.AppendLine("## In-Game Evidence");
        builder.AppendLine();
        builder.AppendLine("| Field | Value |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| Status | {EscapeMarkdownCell(evidence.Status)} |");
        builder.AppendLine($"| Tested at | {EscapeMarkdownCell(evidence.TestedAt)} |");
        builder.AppendLine($"| Tester | {EscapeMarkdownCell(evidence.Tester)} |");
        builder.AppendLine($"| Emulator | {EscapeMarkdownCell(evidence.Emulator)} |");
        builder.AppendLine($"| Booted level | {FormatNullableEvidence(evidence.BootedLevel)} |");
        builder.AppendLine($"| Object appeared | {FormatNullableEvidence(evidence.ObjectAppeared)} |");
        builder.AppendLine($"| Moby records present | {FormatNullableEvidence(evidence.MobyRecordsPresent)} |");
        builder.AppendLine($"| Actor roots present | {FormatNullableEvidence(evidence.ActorRootsPresent)} |");
        builder.AppendLine($"| Behavior correct | {FormatNullableEvidence(evidence.BehaviorCorrect)} |");
        builder.AppendLine($"| No nearby regression | {FormatNullableEvidence(evidence.NoNearbyRegression)} |");
        builder.AppendLine($"| Notes | {EscapeMarkdownCell(evidence.Notes)} |");
        builder.AppendLine();

        if (evidence.BehaviorChecks.Count > 0)
        {
            builder.AppendLine("## Required Behavior Proof");
            builder.AppendLine();
            builder.AppendLine("| Check | Result |");
            builder.AppendLine("|---|---|");
            foreach (CandidateBehaviorCheck check in evidence.BehaviorChecks)
                builder.AppendLine($"| {EscapeMarkdownCell(check.Label)} | {FormatNullableEvidence(check.Passed)} |");
            builder.AppendLine();
        }

        if (writablePreviews.Count > 0)
        {
            builder.AppendLine("## Writable Cross-Level Imports");
            builder.AppendLine();
            builder.AppendLine("| Object | Template | Family | Recipe | Status | Copy patches | Root writes | Guard |");
            builder.AppendLine("|---|---|---|---|---|---:|---:|---|");
            foreach (MobyActorPackageImportPreview preview in writablePreviews)
            {
                builder.AppendLine(
                    $"| {EscapeMarkdownCell(preview.Label)} | {EscapeMarkdownCell(preview.TemplateId)} | {EscapeMarkdownCell(preview.Family)} | {EscapeMarkdownCell(preview.RecipeId)} | {EscapeMarkdownCell(preview.RecipeStatus)} | {preview.CopySegments.Count} | {preview.RootEntries.Count} | {EscapeMarkdownCell(preview.GuardReason)} |");
            }
            builder.AppendLine();

            builder.AppendLine("## Actor Package Safety Detail");
            builder.AppendLine();
            foreach (MobyActorPackageImportPreview preview in writablePreviews)
            {
                builder.AppendLine($"### {preview.Label}");
                builder.AppendLine();
                builder.AppendLine("| Copy | Source | Target | Bytes | Target safety | Target was zero | Source image | Target image |");
                builder.AppendLine("|---:|---|---|---:|---|---|---|---|");
                for (int i = 0; i < preview.CopySegments.Count; i++)
                {
                    MobyActorPackageCopyPreview copy = preview.CopySegments[i];
                    builder.AppendLine(
                        $"| {i + 1} | {EscapeMarkdownCell(copy.SourceStart)} | {EscapeMarkdownCell(copy.TargetStart)} | {copy.ByteLength} | {EscapeMarkdownCell(copy.TargetSafety)} | {copy.TargetBeforeIsZero} | {EscapeMarkdownCell(copy.SourceImageOffset)} | {EscapeMarkdownCell(copy.TargetImageOffset)} |");
                }
                builder.AppendLine();

                builder.AppendLine("| Root op | Slot | Actor ID | Target root | Existing root | Existing actor | Root safe | Actor safe | Note |");
                builder.AppendLine("|---|---|---|---|---|---|---|---|---|");
                foreach (MobyActorPackageRootPreview root in preview.RootEntries)
                {
                    builder.AppendLine(
                        $"| {EscapeMarkdownCell(root.Operation)} | {EscapeMarkdownCell(root.TargetRootSlot)} | {EscapeMarkdownCell(root.TargetActorId)} | {EscapeMarkdownCell(root.TargetRoot)} | {EscapeMarkdownCell(root.ExistingRoot)} | {EscapeMarkdownCell(root.ExistingActorId)} | {root.RootSlotSafe} | {root.ActorIdSlotSafe} | {EscapeMarkdownCell(root.Note)} |");
                }
                builder.AppendLine();
            }
        }

        if (existingSlotSwapPatches.Count > 0)
        {
            builder.AppendLine("## Existing-Slot Swap Candidates");
            builder.AppendLine();
            builder.AppendLine("These candidates replace existing rows and do not increase the level source-object count.");
            builder.AppendLine();
            builder.AppendLine("| Object | Target slot | Bytes | Candidate detail |");
            builder.AppendLine("|---|---:|---:|---|");
            foreach (MobySourcePatch patch in existingSlotSwapPatches)
            {
                builder.AppendLine(
                    $"| {EscapeMarkdownCell(patch.MobyLabel)} | T{patch.TrueIndex} | {patch.ByteLength} | {EscapeMarkdownCell(patch.Description)} |");
            }
            builder.AppendLine();
        }

        if (unwritablePreviews.Count > 0)
        {
            builder.AppendLine("## Guarded Or Unmapped Imports");
            builder.AppendLine();
            builder.AppendLine("| Object | Template | Status | Reason |");
            builder.AppendLine("|---|---|---|---|");
            foreach (MobyActorPackageImportPreview preview in unwritablePreviews)
            {
                builder.AppendLine(
                    $"| {EscapeMarkdownCell(preview.Label)} | {EscapeMarkdownCell(preview.TemplateId)} | {EscapeMarkdownCell(preview.RecipeStatus)} | {EscapeMarkdownCell(preview.GuardReason)} |");
            }
            builder.AppendLine();
        }

        builder.AppendLine("## Patch Summary");
        builder.AppendLine();
        builder.AppendLine("| Kind | Count | Bytes |");
        builder.AppendLine("|---|---:|---:|");
        foreach (var group in plan.Patches
            .GroupBy(patch => patch.Kind)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"| {EscapeMarkdownCell(group.Key)} | {group.Count()} | {group.Sum(patch => patch.ByteLength)} |");
        }
        builder.AppendLine();

        if (plan.SkippedEdits.Count > 0)
        {
            builder.AppendLine("## Skipped Edits");
            builder.AppendLine();
            foreach (string skipped in plan.SkippedEdits)
                builder.AppendLine($"- {skipped}");
            builder.AppendLine();
        }

        builder.AppendLine("## In-Game Checklist");
        builder.AppendLine();
        builder.AppendLine("- [ ] Boot the level from this CUE and confirm it does not hang on the loading/flying screen.");
        foreach (MobySourcePatch patch in existingSlotSwapPatches)
            AppendExistingSlotSwapChecklist(builder, patch);
        foreach (MobyActorPackageImportPreview preview in DistinctChecklistPreviews(writablePreviews))
            AppendFamilyChecklist(builder, preview);
        builder.AppendLine("- [ ] If anything hangs, disappears, or corrupts nearby actors, keep the recipe guarded and test a new candidate instead of using normal Create BIN.");

        await File.WriteAllTextAsync(reportPath, builder.ToString(), Encoding.UTF8, cancellationToken);
        return reportPath;
    }

    private static async Task<string> EnsureCandidateResultTemplateAsync(
        MobySourcePatchResult result,
        IReadOnlyList<MobyActorPackageImportPreview> writablePreviews,
        IReadOnlyList<MobySourcePatch> existingSlotSwapPatches,
        CancellationToken cancellationToken)
    {
        string resultPath = Path.ChangeExtension(result.OutputCuePath, ".candidate-result.json");
        if (File.Exists(resultPath) && !CandidateResultIsUntested(resultPath))
            return resultPath;

        List<string> checklist = new()
        {
            "Boot the level from this CUE and confirm it does not hang on the loading/flying screen."
        };
        foreach (MobySourcePatch patch in existingSlotSwapPatches)
            AddExistingSlotSwapChecklist(checklist, patch);
        foreach (MobyActorPackageImportPreview preview in DistinctChecklistPreviews(writablePreviews))
            AddFamilyChecklist(checklist, preview);
        checklist.Add("If anything hangs, disappears, or corrupts nearby actors, keep the recipe guarded and test a new candidate instead of using normal Create BIN.");
        CandidateBehaviorCheck[] behaviorChecks = BuildBehaviorChecks(writablePreviews)
            .Concat(BuildExistingSlotSwapBehaviorChecks(existingSlotSwapPatches))
            .DistinctBy(check => check.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var template = new
        {
            generatedBy = "Spyro.Editor",
            purpose = "Record the actual in-game DuckStation result for this disposable cross-level moby candidate. Edit result fields after testing; this file is not overwritten by later exports.",
            candidate = new
            {
                levelKey = result.Plan.LevelKey,
                levelName = result.Plan.LevelName,
                cuePath = result.OutputCuePath,
                binPath = result.OutputImagePath,
                patchPlanPath = result.OutputPlanPath,
                recipes = writablePreviews.Select(preview => new
                {
                    label = preview.Label,
                    templateId = preview.TemplateId,
                    family = preview.Family,
                    recipeId = preview.RecipeId,
                    recipeStatus = preview.RecipeStatus,
                    recipeFingerprint = CrossLevelCandidateRecipeFingerprint.Create(preview)
                }).ToArray(),
                existingSlotSwaps = existingSlotSwapPatches.Select(patch => new
                {
                    label = patch.MobyLabel,
                    targetTrueIndex = patch.TrueIndex,
                    patchKind = patch.Kind,
                    sourceCountUnchanged = patch.Description.Contains("Source object count remains", StringComparison.OrdinalIgnoreCase),
                    description = patch.Description
                }).ToArray()
            },
            result = new
            {
                status = "untested",
                testedAt = "",
                tester = "",
                emulator = "DuckStation",
                bootedLevel = (bool?)null,
                objectAppeared = (bool?)null,
                mobyRecordsPresent = (bool?)null,
                actorRootsPresent = (bool?)null,
                behaviorCorrect = (bool?)null,
                noNearbyRegression = (bool?)null,
                notes = ""
            },
            behaviorChecks = behaviorChecks.Select(check => new
            {
                id = check.Id,
                label = check.Label,
                passed = (bool?)null
            }).ToArray(),
            checklist
        };

        await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8, cancellationToken);
        return resultPath;
    }

    private static bool CandidateResultIsUntested(string resultPath)
    {
        try
        {
            using FileStream stream = File.OpenRead(resultPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement result = document.RootElement.TryGetProperty("result", out JsonElement resultElement) && resultElement.ValueKind == JsonValueKind.Object
                ? resultElement
                : document.RootElement;
            string status = ReadJsonString(result, "status", "untested");
            return string.IsNullOrWhiteSpace(status) ||
                string.Equals(status, "untested", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static CandidateResultEvidence ReadCandidateResultEvidence(string resultPath)
    {
        try
        {
            using FileStream stream = File.OpenRead(resultPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            JsonElement result = document.RootElement.TryGetProperty("result", out JsonElement resultElement) && resultElement.ValueKind == JsonValueKind.Object
                ? resultElement
                : document.RootElement;

            return new CandidateResultEvidence(
                Status: ReadJsonString(result, "status", "untested"),
                TestedAt: ReadJsonString(result, "testedAt"),
                Tester: ReadJsonString(result, "tester"),
                Emulator: ReadJsonString(result, "emulator", "DuckStation"),
                BootedLevel: ReadNullableBool(result, "bootedLevel"),
                ObjectAppeared: ReadNullableBool(result, "objectAppeared"),
                MobyRecordsPresent: ReadNullableBool(result, "mobyRecordsPresent"),
                ActorRootsPresent: ReadNullableBool(result, "actorRootsPresent"),
                BehaviorCorrect: ReadNullableBool(result, "behaviorCorrect"),
                NoNearbyRegression: ReadNullableBool(result, "noNearbyRegression"),
                Notes: ReadJsonString(result, "notes"),
                BehaviorChecks: ReadBehaviorChecks(root));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new CandidateResultEvidence("unreadable result file", "", "", "", null, null, null, null, null, null, ex.Message, []);
        }
    }

    private static IReadOnlyList<CandidateBehaviorCheck> ReadBehaviorChecks(JsonElement root)
    {
        if (!root.TryGetProperty("behaviorChecks", out JsonElement checks) || checks.ValueKind != JsonValueKind.Array)
            return [];

        List<CandidateBehaviorCheck> result = [];
        foreach (JsonElement item in checks.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            result.Add(new CandidateBehaviorCheck(
                Id: ReadJsonString(item, "id"),
                Label: ReadJsonString(item, "label", "Behavior check"),
                Passed: ReadNullableBool(item, "passed")));
        }
        return result;
    }

    private static void AppendFamilyChecklist(StringBuilder builder, MobyActorPackageImportPreview preview)
    {
        foreach (string item in BuildFamilyChecklist(preview))
            builder.AppendLine($"- [ ] {item}");
    }

    private static void AddFamilyChecklist(List<string> checklist, MobyActorPackageImportPreview preview)
    {
        checklist.AddRange(BuildFamilyChecklist(preview));
    }

    private static void AppendExistingSlotSwapChecklist(StringBuilder builder, MobySourcePatch patch)
    {
        foreach (string item in BuildExistingSlotSwapChecklist(patch))
            builder.AppendLine($"- [ ] {item}");
    }

    private static void AddExistingSlotSwapChecklist(List<string> checklist, MobySourcePatch patch)
    {
        checklist.AddRange(BuildExistingSlotSwapChecklist(patch));
    }

    private static IEnumerable<string> BuildExistingSlotSwapChecklist(MobySourcePatch patch)
    {
        string label = string.IsNullOrWhiteSpace(patch.MobyLabel) ? $"T{patch.TrueIndex}" : patch.MobyLabel;
        if (TryGetResidentWizardChecklistContext(patch, out ResidentWizardChecklistContext resident))
        {
            yield return $"Confirm {label} appears as a correctly textured Green Wizard at existing slot T{patch.TrueIndex}.";
            yield return $"Approach from several angles; confirm the Wizard follows its {resident.ChecklistRouteDescription} without snapping to {resident.DonorLevelName} T{resident.DonorTrueIndex}'s original location.";
            yield return "Observe at least three complete casts; every cast must spawn a moving, textured lightning bolt and trail instead of looping only the cast animation.";
            yield return "Let at least two separate lightning bolts hit Spyro; confirm each applies normal damage and Spyro does not stretch, blur, freeze, or become corrupted.";
            yield return $"Flame or charge {label}; confirm exactly one death animation, one death sound, and the selected slot's original gem reward.";
            yield return "Wait at least 10 seconds after collecting the gem; confirm the Wizard stays retired and neither its death animation nor sound repeats.";
            yield return LevelCatalog.NormalizeKey(patch.LevelKey) is "magiccrafters" or "wizardpeak"
                ? "Run around the replacement area after the fight; confirm terrain collision remains stable with the in-place properties/fixup edit."
                : "Run around the replacement area after the fight; confirm terrain collision remains stable after the scene component shift.";
            yield return $"Confirm the other native {resident.TargetLevelName} Wizards, nearby enemies, particles, portals, and collision still behave normally.";
            yield break;
        }

        yield return $"Confirm {label} appears at existing slot T{patch.TrueIndex} and remains visible from several camera angles.";
        yield return $"Interact with or defeat {label}; confirm it reacts at normal speed and does not hang the game.";
        yield return $"Confirm {label}'s reward, gem, or chest contents are sensible for the reused slot.";
        yield return "Confirm nearby enemies, chests, dragons, portals, and collision still behave normally.";
    }

    private static IEnumerable<CandidateBehaviorCheck> BuildExistingSlotSwapBehaviorChecks(IEnumerable<MobySourcePatch> patches)
    {
        foreach (MobySourcePatch patch in patches)
        {
            string id = $"existing-slot-t{Math.Max(0, patch.TrueIndex)}";
            string label = string.IsNullOrWhiteSpace(patch.MobyLabel) ? $"T{patch.TrueIndex}" : patch.MobyLabel;
            if (TryGetResidentWizardChecklistContext(patch, out ResidentWizardChecklistContext resident))
            {
                yield return new CandidateBehaviorCheck($"{id}-wizard-visible", $"{label} appears as a correctly textured Green Wizard at T{patch.TrueIndex}.", null);
                yield return new CandidateBehaviorCheck($"{id}-wizard-route", $"{label} uses the private {resident.RoutePointLabel} route anchored at the selected slot instead of snapping to the native {resident.DonorLevelName} T{resident.DonorTrueIndex} donor route.", null);
                yield return new CandidateBehaviorCheck($"{id}-wizard-cast", "At least three consecutive casts each spawn a moving, correctly textured lightning bolt and trail.", null);
                yield return new CandidateBehaviorCheck($"{id}-wizard-hit", "At least two separate lightning bolts damage Spyro normally without stretching, blurring, freezing, or corruption.", null);
                yield return new CandidateBehaviorCheck($"{id}-wizard-death", $"{label} produces exactly one death animation/sound, drops the reused slot's original gem, and remains retired for at least 10 seconds.", null);
                yield return new CandidateBehaviorCheck($"{id}-wizard-collision", "Terrain and object collision remain stable around the replacement after the fight.", null);
                yield return new CandidateBehaviorCheck($"{id}-wizard-nearby", $"Other native {resident.TargetLevelName} Wizards, nearby enemies, particles, portals, and collision still behave normally.", null);
                continue;
            }

            yield return new CandidateBehaviorCheck($"{id}-visible", $"{label} appears at T{patch.TrueIndex} and stays visible from several camera angles.", null);
            yield return new CandidateBehaviorCheck($"{id}-behavior", $"{label} can be interacted with or defeated and behaves at normal speed.", null);
            yield return new CandidateBehaviorCheck($"{id}-reward", $"{label}'s reward, gem, or chest contents are sensible for the reused slot.", null);
            yield return new CandidateBehaviorCheck($"{id}-nearby", "Nearby enemies, chests, dragons, portals, and collision still behave normally.", null);
        }
    }

    private static bool TryGetResidentWizardChecklistContext(
        MobySourcePatch patch,
        out ResidentWizardChecklistContext context)
    {
        context = null!;
        if (!GreenWizardResidentSwapComposer.IsResidentWizardPatch(patch))
            return false;

        LevelRuntimeBundleProfile? profile = GreenWizardRuntimeBundleCatalog.FindProfile(patch.LevelKey);
        if (profile == null)
            return false;

        string routePointLabel = profile.InstanceLayout.RoutePointCount switch
        {
            1 => "one-point",
            2 => "two-point",
            int count => $"{count}-point"
        };
        context = new ResidentWizardChecklistContext(
            FormatLevelName(patch.LevelKey),
            FormatLevelName(profile.InstanceLayout.PropertiesDonorLevelKey),
            profile.InstanceLayout.PropertiesDonorTrueIndex,
            routePointLabel,
            LevelCatalog.NormalizeKey(patch.LevelKey) == "magiccrafters"
                ? $"private translated route ({routePointLabel})"
                : $"private {routePointLabel} route");
        return true;
    }

    private static string FormatLevelName(string levelKey) =>
        LevelCatalog.NormalizeKey(levelKey) switch
        {
            "blowhard" => "Blowhard",
            "magiccrafters" => "Magic Crafters",
            "wizardpeak" => "Wizard Peak",
            string normalized => string.IsNullOrWhiteSpace(normalized) ? "the level" : normalized
        };

    private sealed record ResidentWizardChecklistContext(
        string TargetLevelName,
        string DonorLevelName,
        int DonorTrueIndex,
        string RoutePointLabel,
        string ChecklistRouteDescription);

    private static IEnumerable<MobyActorPackageImportPreview> DistinctChecklistPreviews(IEnumerable<MobyActorPackageImportPreview> previews)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (MobyActorPackageImportPreview preview in previews)
        {
            string key = $"{preview.Family}|{preview.RecipeId}";
            if (seen.Add(key))
                yield return preview;
        }
    }

    private static IEnumerable<string> BuildFamilyChecklist(MobyActorPackageImportPreview preview)
    {
        string family = preview.Family.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
        if (family.Contains("springchest", StringComparison.OrdinalIgnoreCase))
        {
            if (preview.RecipeMode.Contains("CopyOnlyNoRoot", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                yield return "This candidate copies actor package bytes but does not register actor roots or add the custom helper/reward row.";
                yield return "Do not expect final Spring Chest behavior; use this only to prove whether copied package bytes alone are safe to load.";
                yield break;
            }

            if (preview.RecipeMode.Contains("RecordsOnly", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                yield return "This candidate intentionally skips actor package copies, actor root registrations, and the custom helper/reward row.";
                yield return "Do not expect final Spring Chest behavior; use this only to prove whether the imported records/special data are safe to load.";
                yield break;
            }

            if (preview.RecipeMode.Contains("RewardRowNoExeHelper", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                yield return "Confirm whether the Spring Chest appears where you placed it.";
                yield return "Flame the chest and confirm whether the game stays stable with the hidden reward row present.";
                yield return "This candidate intentionally skips the custom Spring Chest helper; do not treat normal chest behavior as a failure of this reward-row isolation test.";
                yield break;
            }

            if (preview.RecipeMode.Contains("ArmOnlyHelper", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                yield return "Confirm whether the Spring Chest appears where you placed it.";
                yield return "Approach and flame the chest to confirm the arm-only helper hook stays stable.";
                yield return "This candidate intentionally skips reward spawning and post-break object movement; do not expect final Spring Chest pop behavior yet.";
                yield break;
            }

            if (preview.RecipeMode.Contains("StackHookOnly", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                yield return "Confirm whether the Spring Chest appears where you placed it.";
                yield return "Approach and flame the chest to confirm the stack-save pass-through helper hook stays stable.";
                yield return "This candidate writes no chest fields; expect normal chest behavior if the stack-save hook itself is safe.";
                yield break;
            }

            if (preview.RecipeMode.Contains("EntryHookOnly", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                yield return "Confirm whether the Spring Chest appears where you placed it.";
                yield return "Approach and flame the chest to confirm the entry-trampoline helper stays stable.";
                yield return "This candidate writes no chest fields; expect normal chest behavior if the routine-entry trampoline itself is safe.";
                yield break;
            }

            if (preview.RecipeMode.Contains("HookOnly", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                yield return "Confirm whether the Spring Chest appears where you placed it.";
                yield return "Approach and flame the chest to confirm the pass-through helper hook stays stable.";
                yield return "This candidate writes no chest fields; expect normal chest behavior if the hook itself is safe.";
                yield break;
            }

            if (preview.RecipeMode.Contains("FirstEmptyRoot", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                yield return "Confirm the Spring Chest appears where you placed it.";
                yield return "Break the Spring Chest and confirm it uses spring-chest behavior, not normal Flame/Charge chest behavior.";
                yield return "Break one normal chest nearby afterward to confirm vanilla chest behavior still works.";
                yield break;
            }

            if (preview.RecipeMode.Contains("NativeContainedHelperRowsNoExeHelper", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                if (preview.RecipeMode.Contains("T80Only", StringComparison.OrdinalIgnoreCase))
                {
                    yield return "Confirm the single Peace Keepers T80 Spring Chest appears where it was placed.";
                    yield return "Flame and charge the Spring Chest to confirm whether the native contained-helper rows T118-T122 complete the Spring Chest behavior.";
                }
                else
                {
                    yield return "Confirm the Peace Keepers T80-T83 Spring Chest cluster appears where it was placed.";
                    yield return "Flame and charge each spring object to confirm whether the hidden native contained-helper rows T118-T122 complete the Spring Chest behavior.";
                }
                yield return "Break one normal chest nearby afterward to confirm Stone Hill's native 0x00C2 chest package still looks and rewards normally.";
                yield break;
            }

            if (preview.RecipeMode.Contains("T80T83SharedCluster", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                yield return "Confirm the Peace Keepers T80-T83 Spring Chest cluster appears where it was placed.";
                yield return "Flame and charge each spring object to confirm whether the Spring Chest actor package is stable without the contained-helper rows.";
                yield return "Break one normal chest nearby afterward to confirm Stone Hill's native 0x00C2 chest package still looks and rewards normally.";
                yield break;
            }

            if (preview.RecipeMode.Contains("SharedCluster", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                yield return "Confirm both Peace Keepers spring-pair records appear where they were placed.";
                yield return "Flame and charge each spring object to confirm whether the native T70/T71 pair works when both records share one copied spring-data cluster.";
                yield return "Break one normal chest nearby afterward to confirm Stone Hill's native 0x00C2 chest package still looks and rewards normally.";
                yield break;
            }

            if (preview.RecipeMode.Contains("T70T71Pair", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                yield return "Confirm both Peace Keepers spring-pair records appear where they were placed.";
                yield return "Flame and charge each spring object to confirm whether the native T70/T71 pair supplies the missing hit/link behavior.";
                yield return "Break one normal chest nearby afterward to confirm Stone Hill's native 0x00C2 chest package still looks and rewards normally.";
                yield break;
            }

            if (preview.RecipeMode.Contains("Peacekeepers00C2", StringComparison.OrdinalIgnoreCase) ||
                preview.RecipeMode.Contains("Native00C2", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                yield return "Confirm the Spring Chest appears where you placed it.";
                yield return "Flame and charge the chest to confirm whether the donor Peace Keepers 0x00C2 chest/controller package finally lets the imported T70 spring chest react.";
                yield return "Break one normal chest nearby afterward to confirm the disposable 0x00C2 package swap did not break vanilla chest rewards.";
                yield break;
            }

            if (preview.RecipeMode.Contains("ExtendedCluster", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                yield return "Confirm the Spring Chest appears where you placed it.";
                if (preview.RecipeMode.Contains("Rebased", StringComparison.OrdinalIgnoreCase))
                {
                    yield return "Flame and charge the chest to confirm whether the larger shared Peace Keepers T70 spring-data cluster plus runtime-shaped flag4A 0x10 and the internal package rebase trigger real Spring Chest behavior.";
                }
                else
                {
                    yield return preview.RecipeMode.Contains("Flag10", StringComparison.OrdinalIgnoreCase)
                        ? "Flame and charge the chest to confirm whether the larger shared Peace Keepers T70 spring-data cluster plus runtime-shaped flag4A 0x10 triggers real Spring Chest behavior."
                        : "Flame and charge the chest to confirm whether the larger shared Peace Keepers T70 spring-data cluster triggers real Spring Chest behavior.";
                }
                yield return "Break one normal chest nearby afterward to confirm vanilla chest behavior still works.";
                yield break;
            }

            if (preview.RecipeMode.Contains("ExtendedSpecial", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                yield return "Confirm the Spring Chest appears where you placed it.";
                yield return preview.RecipeMode.Contains("Flag10", StringComparison.OrdinalIgnoreCase)
                    ? "Flame and charge the chest to confirm whether the extended Peace Keepers T70 special-data stream plus runtime-shaped flag4A 0x10 triggers real Spring Chest behavior."
                    : "Flame and charge the chest to confirm whether the extended Peace Keepers T70 special-data stream triggers real Spring Chest behavior.";
                yield return "Break one normal chest nearby afterward to confirm vanilla chest behavior still works.";
                yield break;
            }

            if (preview.RecipeMode.Contains("NativeContextRowsNoExeHelper", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                yield return "Confirm the Spring Chests appear where you placed them.";
                yield return "Watch for extra copied Peace Keepers context objects or loose gems appearing near the test area; note anything visible or strange.";
                yield return "Flame and charge each Spring Chest to confirm whether the copied context rows now trigger real Spring Chest behavior.";
                yield return "Break one normal chest nearby afterward to confirm vanilla chest behavior still works.";
                yield break;
            }

            if (preview.RecipeMode.Contains("NativeRewardRowsNoExeHelper", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                yield return "Confirm the Spring Chests appear where you placed them.";
                yield return "Watch whether the copied native reward gems appear loose immediately; if they do, note their colors and positions.";
                yield return "Flame and charge each Spring Chest to confirm whether the native reward rows now trigger real Spring Chest behavior.";
                yield return "Break one normal chest nearby afterward to confirm vanilla chest behavior still works.";
                yield break;
            }

            if (preview.RecipeMode.Contains("SafeNativeSpringEffectOnlyStackHelper", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm the target level boots past the flying/loading screen and the Spring Chest appears at the placed location.";
                yield return "Flame and charge the chest; each hit path should trigger one Spring Chest pop with one blue-gem visual following the short rise, hang, and drop arc.";
                yield return "Let the gem miss Spyro once; it should return to the chest and the chest should become usable again without Sparx targeting a parked reward.";
                yield return "On the next pop, touch the blue gem; the treasure count should increase by exactly +5, then the chest and visual should retire once with no repeated sound, reward, or animation.";
                yield return "Break one normal chest nearby afterward and confirm native chest behavior, portals, dragons, and surrounding actors remain normal.";
                yield break;
            }

            if (preview.RecipeMode.Contains("NoControllerNoExeHelper", StringComparison.OrdinalIgnoreCase) ||
                preview.RecipeMode.Contains("Native0149T70", StringComparison.OrdinalIgnoreCase) ||
                preview.RecipeMode.Contains("Native0149T91T131", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                yield return "Confirm the Spring Chest appears where you placed it.";
                yield return "Flame and charge the chest to confirm whether the native 0x0149 row triggers real Spring Chest behavior by itself.";
                yield return "Break one normal chest nearby afterward to confirm vanilla chest behavior still works.";
                yield break;
            }

            if (preview.RecipeMode.Contains("NoExeHelper", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Confirm Stone Hill boots past the flying/loading screen.";
                yield return "Confirm whether the Spring Chest appears where you placed it.";
                yield return "This candidate intentionally skips the custom Spring Chest helper/reward row; do not treat missing pop/reward behavior as a failure of the actor-package load test.";
                yield break;
            }

            yield return "Confirm the Spring Chest appears where you placed it.";
            yield return "Break the Spring Chest and confirm it uses spring-chest behavior, not normal chest or gem behavior.";
            yield return "Break one normal chest in the same level afterward to confirm the imported package did not break vanilla chests.";
        }
        else if (family.Contains("lockedchest", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Confirm the matching key is visible and collectible.";
            yield return "Confirm the Key Chest stays locked before the key is collected.";
            yield return "Collect the matching key, open the Key Chest, and confirm its reward behaves normally.";
            yield return "Confirm nearby portals, dragons, and existing chests still behave normally.";
        }
        else if (family.Contains("fireworkchest", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Confirm the Firework Chest appears with its fuse/projectile controller and linked reward rows present.";
            yield return "Trigger it with flame and charge as appropriate; the firework should launch and explode once using the native timing, effects, and sound.";
            yield return "Confirm every linked gem is awarded exactly once and the chest remains retired without repeating explosions or rewards.";
            yield return "Confirm nearby chests, enemies, portals, dragons, and effects still behave normally.";
        }
        else if (family.Contains("multigemchest", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Confirm the 3x Flame Chest appears with its hidden fan/controller and all linked reward rows present.";
            yield return "Trigger the chest and confirm the full native multi-explosion sequence plays once with the expected effects and sound.";
            yield return "Confirm all rewards appear and count exactly once, then the chest and controller retire without replaying the sequence.";
            yield return "Confirm nearby chests, enemies, portals, dragons, and effects still behave normally.";
        }
        else if (family.Contains("enemytransform", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"Confirm {preview.Label} appears as the imported enemy and can be defeated without breaking nearby actors.";
            if (preview.RecipeMode.Contains("ReplaceUnused00EARoot", StringComparison.OrdinalIgnoreCase))
                yield return "Confirm Toasty's treasure maximum remains 100 and the imported enemy drops the target slot's original red gem.";
            else
                yield return "Confirm the defeated enemy's reward or collection behavior is sane.";
            yield return "Confirm other enemies in the level still behave normally.";
        }
        else
        {
            yield return $"Confirm {preview.Label} appears and behaves like the source-level object.";
        }
    }

    private static IEnumerable<CandidateBehaviorCheck> BuildBehaviorChecks(IEnumerable<MobyActorPackageImportPreview> previews)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (MobyActorPackageImportPreview preview in DistinctChecklistPreviews(previews))
        {
            foreach (CandidateBehaviorCheck check in BuildFamilyBehaviorChecks(preview))
            {
                if (seen.Add(check.Id))
                    yield return check;
            }
        }
    }

    private static IEnumerable<CandidateBehaviorCheck> BuildFamilyBehaviorChecks(MobyActorPackageImportPreview preview)
    {
        string family = preview.Family.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
        if (family.Contains("springchest", StringComparison.OrdinalIgnoreCase))
        {
            if (preview.RecipeMode.Contains("CopyOnlyNoRoot", StringComparison.OrdinalIgnoreCase))
            {
                yield return new CandidateBehaviorCheck("springchest-copyonly-boots", "Stone Hill boots past the flying/loading screen with copied Spring Chest package bytes but no actor roots.", null);
                yield return new CandidateBehaviorCheck("springchest-copyonly-no-regression", "Nearby normal chests or actors still behave normally enough to continue testing.", null);
                yield break;
            }

            if (preview.RecipeMode.Contains("RecordsOnly", StringComparison.OrdinalIgnoreCase))
            {
                yield return new CandidateBehaviorCheck("springchest-recordsonly-boots", "Stone Hill boots past the flying/loading screen with only imported Spring Chest records/special data.", null);
                yield return new CandidateBehaviorCheck("springchest-recordsonly-no-regression", "Nearby normal chests or actors still behave normally enough to continue testing.", null);
                yield break;
            }

            if (preview.RecipeMode.Contains("RewardRowNoExeHelper", StringComparison.OrdinalIgnoreCase))
            {
                yield return new CandidateBehaviorCheck("springchest-rewardrow-boots", "Stone Hill boots past the flying/loading screen with the hidden reward row present.", null);
                yield return new CandidateBehaviorCheck("springchest-rewardrow-appears", "Spring Chest appears at the placed location.", null);
                yield return new CandidateBehaviorCheck("springchest-rewardrow-no-crash", "Flaming or approaching the chest does not crash or hang the game.", null);
                yield break;
            }

            if (preview.RecipeMode.Contains("ArmOnlyHelper", StringComparison.OrdinalIgnoreCase))
            {
                yield return new CandidateBehaviorCheck("springchest-armonly-boots", "Stone Hill boots past the flying/loading screen with the arm-only helper hook.", null);
                yield return new CandidateBehaviorCheck("springchest-armonly-appears", "Spring Chest appears at the placed location.", null);
                yield return new CandidateBehaviorCheck("springchest-armonly-no-crash", "Approaching or flaming the chest does not crash or hang the game.", null);
                yield break;
            }

            if (preview.RecipeMode.Contains("StackHookOnly", StringComparison.OrdinalIgnoreCase))
            {
                yield return new CandidateBehaviorCheck("springchest-stack-hookonly-boots", "Stone Hill boots past the flying/loading screen with the stack-save pass-through helper hook.", null);
                yield return new CandidateBehaviorCheck("springchest-stack-hookonly-appears", "Spring Chest appears at the placed location.", null);
                yield return new CandidateBehaviorCheck("springchest-stack-hookonly-no-crash", "Approaching or flaming the chest does not crash or hang the game.", null);
                yield break;
            }

            if (preview.RecipeMode.Contains("EntryHookOnly", StringComparison.OrdinalIgnoreCase))
            {
                yield return new CandidateBehaviorCheck("springchest-entry-hookonly-boots", "Stone Hill boots past the flying/loading screen with the entry-trampoline helper hook.", null);
                yield return new CandidateBehaviorCheck("springchest-entry-hookonly-appears", "Spring Chest appears at the placed location.", null);
                yield return new CandidateBehaviorCheck("springchest-entry-hookonly-no-crash", "Approaching or flaming the chest does not crash or hang the game.", null);
                yield break;
            }

            if (preview.RecipeMode.Contains("HookOnly", StringComparison.OrdinalIgnoreCase))
            {
                yield return new CandidateBehaviorCheck("springchest-hookonly-boots", "Stone Hill boots past the flying/loading screen with the pass-through helper hook.", null);
                yield return new CandidateBehaviorCheck("springchest-hookonly-appears", "Spring Chest appears at the placed location.", null);
                yield return new CandidateBehaviorCheck("springchest-hookonly-no-crash", "Approaching or flaming the chest does not crash or hang the game.", null);
                yield break;
            }

            if (preview.RecipeMode.Contains("FirstEmptyRoot", StringComparison.OrdinalIgnoreCase))
            {
                yield return new CandidateBehaviorCheck("springchest-first-empty-root-boots", "Stone Hill boots past the flying/loading screen with the contiguous 0x0149 root.", null);
                yield return new CandidateBehaviorCheck("springchest-first-empty-root-appears", "Spring Chest appears at the placed location.", null);
                yield return new CandidateBehaviorCheck("springchest-first-empty-root-behaves", "Breaking it uses Spring Chest behavior instead of normal Flame/Charge chest behavior.", null);
                yield return new CandidateBehaviorCheck("springchest-first-empty-root-normal-chest-regression", "A normal chest in the same level still breaks and rewards normally afterward.", null);
                yield break;
            }

            if (preview.RecipeMode.Contains("NativeContainedHelperRowsNoExeHelper", StringComparison.OrdinalIgnoreCase))
            {
                if (preview.RecipeMode.Contains("T80Only", StringComparison.OrdinalIgnoreCase))
                {
                    yield return new CandidateBehaviorCheck("springchest-t80-contained-helper-boots", "Stone Hill boots past the flying/loading screen with native T80 0x0149 replacing unused 0x000E and Stone Hill's native 0x00C2 package preserved.", null);
                    yield return new CandidateBehaviorCheck("springchest-t80-contained-helper-appears", "The single Peace Keepers T80 Spring Chest appears at the placed location.", null);
                    yield return new CandidateBehaviorCheck("springchest-t80-contained-helper-behaves", "Flaming or charging the Spring Chest uses Spring Chest behavior with hidden contained-helper rows T118-T122.", null);
                    yield return new CandidateBehaviorCheck("springchest-t80-contained-helper-normal-chest-regression", "A normal chest in the same level still looks, breaks, and rewards normally afterward.", null);
                }
                else
                {
                    yield return new CandidateBehaviorCheck("springchest-t80t83-contained-helper-boots", "Stone Hill boots past the flying/loading screen with native T80-T83 0x0149 records replacing unused 0x000E and Stone Hill's native 0x00C2 package preserved.", null);
                    yield return new CandidateBehaviorCheck("springchest-t80t83-contained-helper-appears", "The Peace Keepers T80-T83 Spring Chest cluster appears at the placed location.", null);
                    yield return new CandidateBehaviorCheck("springchest-t80t83-contained-helper-behaves", "Flaming or charging the cluster uses Spring Chest behavior with hidden contained-helper rows T118-T122.", null);
                    yield return new CandidateBehaviorCheck("springchest-t80t83-contained-helper-normal-chest-regression", "A normal chest in the same level still looks, breaks, and rewards normally afterward.", null);
                }
                yield break;
            }

            if (preview.RecipeMode.Contains("T80T83SharedCluster", StringComparison.OrdinalIgnoreCase))
            {
                yield return new CandidateBehaviorCheck("springchest-t80t83-no-contained-helper-boots", "Stone Hill boots past the flying/loading screen with native T80-T83 0x0149 records replacing unused 0x000E and Stone Hill's native 0x00C2 package preserved.", null);
                yield return new CandidateBehaviorCheck("springchest-t80t83-no-contained-helper-appears", "The Peace Keepers T80-T83 Spring Chest cluster appears at the placed location.", null);
                yield return new CandidateBehaviorCheck("springchest-t80t83-no-contained-helper-stable", "Flaming or charging the cluster stays stable without the contained-helper rows.", null);
                yield return new CandidateBehaviorCheck("springchest-t80t83-no-contained-helper-normal-chest-regression", "A normal chest in the same level still looks, breaks, and rewards normally afterward.", null);
                yield break;
            }

            if (preview.RecipeMode.Contains("SharedCluster", StringComparison.OrdinalIgnoreCase))
            {
                yield return new CandidateBehaviorCheck("springchest-t70t71-shared-cluster-boots", "Stone Hill boots past the flying/loading screen with native T70/T71 0x0149 replacing unused 0x000E and Stone Hill's native 0x00C2 package preserved.", null);
                yield return new CandidateBehaviorCheck("springchest-t70t71-shared-cluster-appears", "Both Peace Keepers spring-pair records appear at the placed location.", null);
                yield return new CandidateBehaviorCheck("springchest-t70t71-shared-cluster-behaves", "Flaming or charging either object uses Spring Chest behavior when both paired records point into one copied shared spring-data cluster.", null);
                yield return new CandidateBehaviorCheck("springchest-t70t71-shared-cluster-normal-chest-regression", "A normal chest in the same level still looks, breaks, and rewards normally afterward.", null);
                yield break;
            }

            if (preview.RecipeMode.Contains("T70T71Pair", StringComparison.OrdinalIgnoreCase))
            {
                yield return new CandidateBehaviorCheck("springchest-t70t71-pair-boots", "Stone Hill boots past the flying/loading screen with native T70/T71 0x0149 replacing unused 0x000E and Stone Hill's native 0x00C2 package preserved.", null);
                yield return new CandidateBehaviorCheck("springchest-t70t71-pair-appears", "Both Peace Keepers spring-pair records appear at the placed location.", null);
                yield return new CandidateBehaviorCheck("springchest-t70t71-pair-behaves", "Flaming or charging either object uses Spring Chest behavior with the paired native Peace Keepers spring records.", null);
                yield return new CandidateBehaviorCheck("springchest-t70t71-pair-normal-chest-regression", "A normal chest in the same level still looks, breaks, and rewards normally afterward.", null);
                yield break;
            }

            if (preview.RecipeMode.Contains("Peacekeepers00C2", StringComparison.OrdinalIgnoreCase) ||
                preview.RecipeMode.Contains("Native00C2", StringComparison.OrdinalIgnoreCase))
            {
                yield return new CandidateBehaviorCheck("springchest-native00c2-boots", "Stone Hill boots past the flying/loading screen with Peace Keepers 0x00C2 replacing Stone Hill 0x00C2 and native T70 0x0149 replacing unused 0x000E.", null);
                yield return new CandidateBehaviorCheck("springchest-native00c2-appears", "Spring Chest appears at the placed location.", null);
                yield return new CandidateBehaviorCheck("springchest-native00c2-behaves", "Flaming or charging it uses Spring Chest behavior with the donor Peace Keepers 0x00C2 package plus the larger shared spring-data cluster, runtime-shaped flag4A 0x10, and internal package rebase.", null);
                yield return new CandidateBehaviorCheck("springchest-native00c2-normal-chest-regression", "A normal chest in the same level still breaks and rewards normally afterward.", null);
                yield break;
            }

            if (preview.RecipeMode.Contains("ExtendedCluster", StringComparison.OrdinalIgnoreCase))
            {
                string suffix = preview.RecipeMode.Contains("Flag10", StringComparison.OrdinalIgnoreCase) ? "-flag10" : "";
                if (preview.RecipeMode.Contains("Rebased", StringComparison.OrdinalIgnoreCase))
                    suffix += "-rebased";
                string behaviorLabel = preview.RecipeMode.Contains("Rebased", StringComparison.OrdinalIgnoreCase)
                    ? "Flaming or charging it uses Spring Chest behavior with the larger shared Peace Keepers T70 spring-data cluster, runtime-shaped flag4A 0x10, and internal package rebase."
                    : preview.RecipeMode.Contains("Flag10", StringComparison.OrdinalIgnoreCase)
                        ? "Flaming or charging it uses Spring Chest behavior with the larger shared Peace Keepers T70 spring-data cluster and runtime-shaped flag4A 0x10."
                        : "Flaming or charging it uses Spring Chest behavior with the larger shared Peace Keepers T70 spring-data cluster.";
                yield return new CandidateBehaviorCheck($"springchest-extended-cluster{suffix}-boots", "Stone Hill boots past the flying/loading screen with native T70 0x0149 replacing unused 0x000E.", null);
                yield return new CandidateBehaviorCheck($"springchest-extended-cluster{suffix}-appears", "Spring Chest appears at the placed location.", null);
                yield return new CandidateBehaviorCheck($"springchest-extended-cluster{suffix}-behaves", behaviorLabel, null);
                yield return new CandidateBehaviorCheck($"springchest-extended-cluster{suffix}-normal-chest-regression", "A normal chest in the same level still breaks and rewards normally afterward.", null);
                yield break;
            }

            if (preview.RecipeMode.Contains("ExtendedSpecial", StringComparison.OrdinalIgnoreCase))
            {
                string suffix = preview.RecipeMode.Contains("Flag10", StringComparison.OrdinalIgnoreCase) ? "-flag10" : "";
                string behaviorLabel = preview.RecipeMode.Contains("Flag10", StringComparison.OrdinalIgnoreCase)
                    ? "Flaming or charging it uses Spring Chest behavior with the extended Peace Keepers T70 special-data stream and runtime-shaped flag4A 0x10."
                    : "Flaming or charging it uses Spring Chest behavior with the extended Peace Keepers T70 special-data stream.";
                yield return new CandidateBehaviorCheck($"springchest-extended-special{suffix}-boots", "Stone Hill boots past the flying/loading screen with native T70 0x0149 replacing unused 0x000E.", null);
                yield return new CandidateBehaviorCheck($"springchest-extended-special{suffix}-appears", "Spring Chest appears at the placed location.", null);
                yield return new CandidateBehaviorCheck($"springchest-extended-special{suffix}-behaves", behaviorLabel, null);
                yield return new CandidateBehaviorCheck($"springchest-extended-special{suffix}-normal-chest-regression", "A normal chest in the same level still breaks and rewards normally afterward.", null);
                yield break;
            }

            if (preview.RecipeMode.Contains("SafeNativeSpringEffectOnlyStackHelper", StringComparison.OrdinalIgnoreCase))
            {
                yield return new CandidateBehaviorCheck("springchest-v159-boots", "The target level boots past the flying/loading screen with the complete controller, shell, helper, and blank reward-row bundle.", null);
                yield return new CandidateBehaviorCheck("springchest-v159-appears", "The Spring Chest appears at the placed location.", null);
                yield return new CandidateBehaviorCheck("springchest-v159-pop-arc", "Flame and charge each produce one blue-gem visual using the short rise, hang, and drop arc.", null);
                yield return new CandidateBehaviorCheck("springchest-v159-miss-return", "An uncollected gem returns to the chest and rearms it without becoming a parked Sparx target.", null);
                yield return new CandidateBehaviorCheck("springchest-v159-collect-once", "Touching the next blue gem awards exactly +5 and retires the chest/visual once without repeated animation, sound, or reward.", null);
                yield return new CandidateBehaviorCheck("springchest-v159-nearby-regression", "A nearby native chest and surrounding actors still behave normally afterward.", null);
                yield break;
            }

            if (preview.RecipeMode.Contains("NoControllerNoExeHelper", StringComparison.OrdinalIgnoreCase) ||
                preview.RecipeMode.Contains("Native0149T70", StringComparison.OrdinalIgnoreCase))
            {
                yield return new CandidateBehaviorCheck("springchest-native-t70-boots", "Stone Hill boots past the flying/loading screen with native T70 0x0149 replacing unused 0x000E.", null);
                yield return new CandidateBehaviorCheck("springchest-native-t70-appears", "Spring Chest appears at the placed location.", null);
                yield return new CandidateBehaviorCheck("springchest-native-t70-behaves", "Flaming or charging it uses Spring Chest behavior instead of doing nothing or behaving like a normal chest.", null);
                yield return new CandidateBehaviorCheck("springchest-native-t70-normal-chest-regression", "A normal chest in the same level still breaks and rewards normally afterward.", null);
                yield break;
            }

            if (preview.RecipeMode.Contains("NoExeHelper", StringComparison.OrdinalIgnoreCase))
            {
                yield return new CandidateBehaviorCheck("springchest-nohelper-boots", "Stone Hill boots past the flying/loading screen.", null);
                yield return new CandidateBehaviorCheck("springchest-nohelper-appears", "Spring Chest appears at the placed location, if the actor package can instantiate without the helper.", null);
                yield return new CandidateBehaviorCheck("springchest-nohelper-no-regression", "Nearby normal chests or actors still behave normally enough to continue testing.", null);
                yield break;
            }

            yield return new CandidateBehaviorCheck("springchest-appears", "Spring Chest appears at the placed location.", null);
            yield return new CandidateBehaviorCheck("springchest-pops", "Breaking it uses Spring Chest pop behavior instead of normal chest or gem behavior.", null);
            yield return new CandidateBehaviorCheck("springchest-normal-chest-regression", "A normal chest in the same level still breaks and rewards normally afterward.", null);
        }
        else if (family.Contains("lockedchest", StringComparison.OrdinalIgnoreCase))
        {
            yield return new CandidateBehaviorCheck("key-visible-collectible", "The matching key is visible and collectible.", null);
            yield return new CandidateBehaviorCheck("keychest-locked-before-key", "The Key Chest stays locked before the key is collected.", null);
            yield return new CandidateBehaviorCheck("keychest-opens-after-key", "After collecting the key, the Key Chest opens and rewards normally.", null);
            yield return new CandidateBehaviorCheck("keychest-nearby-regression", "Nearby portals, dragons, and existing chests still behave normally.", null);
        }
        else if (family.Contains("fireworkchest", StringComparison.OrdinalIgnoreCase))
        {
            yield return new CandidateBehaviorCheck("fireworkchest-bundle-present", "The Firework Chest, fuse/projectile controller, and linked reward rows all appear in the target level.", null);
            yield return new CandidateBehaviorCheck("fireworkchest-native-sequence", "Triggering the chest launches and explodes the firework once with native timing, effects, and sound.", null);
            yield return new CandidateBehaviorCheck("fireworkchest-reward-once", "Every linked gem is awarded exactly once and the chest remains retired without repeated explosions or rewards.", null);
            yield return new CandidateBehaviorCheck("fireworkchest-nearby-regression", "Nearby chests, enemies, portals, dragons, and effects still behave normally.", null);
        }
        else if (family.Contains("multigemchest", StringComparison.OrdinalIgnoreCase))
        {
            yield return new CandidateBehaviorCheck("multigemchest-bundle-present", "The 3x Flame Chest, hidden fan/controller, and all linked reward rows appear in the target level.", null);
            yield return new CandidateBehaviorCheck("multigemchest-native-sequence", "Triggering the chest plays the complete native multi-explosion sequence once with the expected effects and sound.", null);
            yield return new CandidateBehaviorCheck("multigemchest-reward-once", "All rewards count exactly once and the chest/controller retire without replaying the sequence.", null);
            yield return new CandidateBehaviorCheck("multigemchest-nearby-regression", "Nearby chests, enemies, portals, dragons, and effects still behave normally.", null);
        }
        else if (family.Contains("enemytransform", StringComparison.OrdinalIgnoreCase))
        {
            yield return new CandidateBehaviorCheck("enemy-appears-as-import", $"{preview.Label} appears as the imported enemy.", null);
            yield return new CandidateBehaviorCheck("enemy-defeatable", "The imported enemy can be defeated.", null);
            if (preview.RecipeMode.Contains("ReplaceUnused00EARoot", StringComparison.OrdinalIgnoreCase))
                yield return new CandidateBehaviorCheck("enemy-toasty-native-reward", "Toasty's treasure maximum remains 100 and the imported enemy drops the target slot's original red gem.", null);
            else
                yield return new CandidateBehaviorCheck("enemy-reward-sane", "The defeated enemy's reward or collection behavior is sane.", null);
            yield return new CandidateBehaviorCheck("enemy-nearby-regression", "Other enemies in the level still behave normally.", null);
        }
        else
        {
            yield return new CandidateBehaviorCheck($"{preview.TemplateId}-behaves", $"{preview.Label} appears and behaves like the source-level object.", null);
        }
    }

    private static string FormatNullableEvidence(bool? value)
    {
        return value.HasValue ? value.Value ? "yes" : "no" : "not recorded";
    }

    private static string ReadJsonString(JsonElement element, string propertyName, string fallback = "")
    {
        return element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : fallback;
    }

    private static bool? ReadNullableBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
            return null;
        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static string EscapeMarkdownCell(string value)
    {
        return (value ?? "").Replace("|", "/", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }

    private sealed record CandidateResultEvidence(
        string Status,
        string TestedAt,
        string Tester,
        string Emulator,
        bool? BootedLevel,
        bool? ObjectAppeared,
        bool? MobyRecordsPresent,
        bool? ActorRootsPresent,
        bool? BehaviorCorrect,
        bool? NoNearbyRegression,
        string Notes,
        IReadOnlyList<CandidateBehaviorCheck> BehaviorChecks);

    private sealed record CandidateBehaviorCheck(
        string Id,
        string Label,
        bool? Passed);
}

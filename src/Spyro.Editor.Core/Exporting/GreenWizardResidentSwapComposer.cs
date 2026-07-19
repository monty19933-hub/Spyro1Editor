using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public enum GreenWizardResidentSwapBuildMode
{
    Candidate,
    Normal
}

public sealed record GreenWizardResidentSwapRoute(
    string TargetLevelKey,
    string RecipeId,
    string DonorLevelKey,
    int DonorTrueIndex,
    int PropertiesBytes,
    int RoutePointCount,
    bool NormalCreateBinReady);

public sealed record GreenWizardResidentSwapCompositionSummary(
    string RecipeId,
    string TargetLevelKey,
    string ProfileFingerprint,
    GreenWizardResidentSwapBuildMode BuildMode,
    int TargetTrueIndex,
    string OutputImagePath,
    string OutputImageSha256,
    string OutputPlanPath,
    string RuntimeProofOutputSha256);

/// <summary>
/// Resolves and composes the small class of Green Wizard replacements whose
/// destination already owns the checked Wizard/lightning runtime, including
/// the native Wizard Peak donor level itself. Candidate
/// and normal-build authority are deliberately separate: static resident
/// evidence can create an isolated candidate, while normal Create BIN also
/// requires a runtime-proven profile tied to the registered recipe.
/// </summary>
public static class GreenWizardResidentSwapComposer
{
    private const ushort GreenWizardActorId = 0x011B;
    private const ushort LightningActorId = 0x0026;
    private const int RecordStride = 0x58;
    private const int ActorIdOffset = 0x36;
    private const string ExistingSlotPatchKind = "cross-level-existing-slot-candidate";

    public static bool TryResolve(
        LevelDefinition targetLevel,
        GreenWizardResidentSwapBuildMode buildMode,
        out GreenWizardResidentSwapRoute route,
        out string reason)
    {
        route = null!;
        reason = "";
        string targetKey = LevelCatalog.NormalizeKey(targetLevel.Key);
        if (!TryGetRegisteredRecipeId(targetKey, out string recipeId))
        {
            reason = $"{targetLevel.DisplayName} does not have a registered resident Green Wizard structural writer.";
            return false;
        }

        LevelRuntimeBundleProfile? profile = GreenWizardRuntimeBundleCatalog.FindProfile(targetKey);
        RuntimeBundleCompatibilityResult compatibility = GreenWizardRuntimeBundleCatalog.Evaluate(targetKey);
        if (targetKey.Equals("wizardpeak", StringComparison.OrdinalIgnoreCase))
        {
            bool nativeStructuralReady =
                profile?.Deployment == RuntimeBundleDeploymentKind.Native &&
                profile.Evidence is RuntimeBundleEvidenceKind.NativeGame or RuntimeBundleEvidenceKind.RuntimeProven &&
                compatibility.Status == RuntimeBundleCompatibilityStatus.Ready &&
                compatibility.Deployment == RuntimeBundleDeploymentKind.Native &&
                compatibility.CanStageInstance &&
                !compatibility.NeedsLevelBundleInstall &&
                profile.ResidentActorIds.Contains(GreenWizardActorId) &&
                profile.ResidentActorIds.Contains(LightningActorId) &&
                profile.InstanceLayout.PropertiesDonorTrueIndex == 6 &&
                profile.InstanceLayout.PropertiesBytes == 0x50 &&
                profile.InstanceLayout.RoutePointCount == 2 &&
                profile.InstanceLayout.SwapPointerFixupDelta == 0 &&
                !profile.InstanceLayout.PreserveTargetPodOrGroup &&
                profile.InstanceLayout.TranslateDonorRouteFromSpawn;
            if (!nativeStructuralReady)
            {
                reason = "Wizard Peak's native Green Wizard T6 runtime/profile no longer matches the checked T24 pod-matched candidate writer.";
                return false;
            }
            bool nativeNormalReady =
                profile!.Evidence == RuntimeBundleEvidenceKind.RuntimeProven &&
                compatibility.NormalCreateBinReady &&
                !compatibility.RequiresRuntimeSmoke &&
                string.Equals(profile.RuntimeProofRecipeId, recipeId, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(profile.RuntimeProofOutputSha256);
            if (buildMode == GreenWizardResidentSwapBuildMode.Normal && !nativeNormalReady)
            {
                reason = "Wizard Peak's exact T6-to-T24 Green Wizard route is missing its runtime-proof recipe or final-image hash.";
                return false;
            }

            route = new GreenWizardResidentSwapRoute(
                targetKey,
                WizardPeakGreenWizardNativeSwapExporter.RecipeId,
                "wizardpeak",
                6,
                0x50,
                2,
                NormalCreateBinReady: nativeNormalReady);
            return true;
        }

        RuntimeBundleResidentDetection? resident = profile?.ResidentDetection;
        RuntimeBundleResidentPropertiesLayout? properties = resident?.PropertiesLayout;
        if (profile == null ||
            resident == null ||
            properties == null ||
            profile.Deployment != RuntimeBundleDeploymentKind.ResidentActor ||
            compatibility.Deployment != RuntimeBundleDeploymentKind.ResidentActor ||
            !compatibility.CanStageInstance ||
            resident.DataEntry != targetLevel.SourceWadEntry ||
            !profile.ResidentActorIds.Contains(GreenWizardActorId) ||
            !profile.ResidentActorIds.Contains(LightningActorId) ||
            !profile.InstanceLayout.PreserveTargetPodOrGroup ||
            !profile.InstanceLayout.TranslateDonorRouteFromSpawn ||
            (targetKey.Equals("magiccrafters", StringComparison.OrdinalIgnoreCase)
                ? profile.InstanceLayout.SwapPointerFixupDelta != -1
                : profile.InstanceLayout.SwapPointerFixupDelta != 1) ||
            properties.PropertiesBytes != profile.InstanceLayout.PropertiesBytes ||
            properties.RoutePointCount != profile.InstanceLayout.RoutePointCount ||
            !string.Equals(
                LevelCatalog.NormalizeKey(profile.InstanceLayout.PropertiesDonorLevelKey),
                targetKey,
                StringComparison.OrdinalIgnoreCase))
        {
            reason = $"{targetLevel.DisplayName}'s runtime-bundle profile is not a writable target-pod-preserving resident recipe.";
            return false;
        }

        bool recognizedStatus = compatibility.Status is
            RuntimeBundleCompatibilityStatus.Candidate or RuntimeBundleCompatibilityStatus.Ready;
        if (!recognizedStatus)
        {
            reason = $"{targetLevel.DisplayName}'s resident Green Wizard profile is {compatibility.Status}, not Candidate or Ready.";
            return false;
        }

        bool normalReady =
            compatibility.Status == RuntimeBundleCompatibilityStatus.Ready &&
            compatibility.NormalCreateBinReady &&
            !compatibility.RequiresRuntimeSmoke &&
            profile.Evidence == RuntimeBundleEvidenceKind.RuntimeProven &&
            string.Equals(profile.RuntimeProofRecipeId, recipeId, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(profile.RuntimeProofOutputSha256);
        if (buildMode == GreenWizardResidentSwapBuildMode.Normal && !normalReady)
        {
            reason = $"{targetLevel.DisplayName}'s registered resident Green Wizard recipe is candidate-only until its runtime proof is promoted.";
            return false;
        }

        route = new GreenWizardResidentSwapRoute(
            targetKey,
            recipeId,
            LevelCatalog.NormalizeKey(profile.InstanceLayout.PropertiesDonorLevelKey),
            profile.InstanceLayout.PropertiesDonorTrueIndex,
            profile.InstanceLayout.PropertiesBytes,
            profile.InstanceLayout.RoutePointCount,
            normalReady);
        return true;
    }

    public static bool IsTemplateEligible(
        LevelDefinition targetLevel,
        string family,
        string sourceLevelKey,
        int sourceTrueIndex,
        GreenWizardResidentSwapBuildMode buildMode,
        out GreenWizardResidentSwapRoute route,
        out string reason)
    {
        if (!string.Equals(family, "enemyTransform", StringComparison.OrdinalIgnoreCase))
        {
            route = null!;
            reason = "The template is not a Green Wizard enemy-transform recipe.";
            return false;
        }

        if (!TryResolve(targetLevel, buildMode, out route, out reason))
            return false;

        if (!string.Equals(
                LevelCatalog.NormalizeKey(sourceLevelKey),
                route.DonorLevelKey,
                StringComparison.OrdinalIgnoreCase) ||
            sourceTrueIndex != route.DonorTrueIndex)
        {
            reason =
                $"{targetLevel.DisplayName}'s resident recipe requires native donor " +
                $"{route.DonorLevelKey} T{route.DonorTrueIndex}.";
            route = null!;
            return false;
        }

        return true;
    }

    public static bool IsTemplateEligible(
        LevelDefinition targetLevel,
        string family,
        string sourceLevelKey,
        int sourceTrueIndex,
        int actorId,
        bool sourceRecordHasPerInstanceData,
        bool targetActorRootPresent,
        GreenWizardResidentSwapBuildMode buildMode,
        out GreenWizardResidentSwapRoute route,
        out string reason)
    {
        if (actorId != GreenWizardActorId || !sourceRecordHasPerInstanceData || !targetActorRootPresent)
        {
            route = null!;
            reason = actorId != GreenWizardActorId
                ? $"The selected donor is actor 0x{actorId:X4}, not Green Wizard 0x{GreenWizardActorId:X4}."
                : !sourceRecordHasPerInstanceData
                ? "The native Green Wizard donor has no checked per-instance properties pointer."
                : "The destination does not expose a loaded Green Wizard actor root.";
            return false;
        }

        return IsTemplateEligible(
            targetLevel,
            family,
            sourceLevelKey,
            sourceTrueIndex,
            buildMode,
            out route,
            out reason);
    }

    public static bool IsResidentWizardPatch(MobySourcePatch patch)
    {
        if (!string.Equals(patch.Kind, ExistingSlotPatchKind, StringComparison.OrdinalIgnoreCase) ||
            !TryReadActorId(patch.AfterHexPreview, out ushort actorId) ||
            actorId != GreenWizardActorId)
        {
            return false;
        }

        LevelRuntimeBundleProfile? profile = GreenWizardRuntimeBundleCatalog.FindProfile(patch.LevelKey);
        return profile?.Deployment is RuntimeBundleDeploymentKind.Native or RuntimeBundleDeploymentKind.ResidentActor &&
            TryGetRegisteredRecipeId(LevelCatalog.NormalizeKey(patch.LevelKey), out _);
    }

    public static int CountResidentWizardPatches(MobySourcePatchPlan plan) =>
        plan.Patches.Count(IsResidentWizardPatch);

    public static GreenWizardResidentSwapCompositionSummary ApplyAndVerify(
        string imagePath,
        LevelDefinition level,
        MobySourcePatchPlan sourcePatchPlan,
        string outputPlanPath,
        GreenWizardResidentSwapBuildMode buildMode)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("The resident Green Wizard base image is missing.", imagePath);
        if (!string.Equals(
                LevelCatalog.NormalizeKey(sourcePatchPlan.LevelKey),
                LevelCatalog.NormalizeKey(level.Key),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The resident Green Wizard plan targets '{sourcePatchPlan.LevelKey}', not '{level.Key}'.");
        }

        MobySourcePatch[] matches = sourcePatchPlan.Patches
            .Where(IsResidentWizardPatch)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"The guarded resident Green Wizard writer supports exactly one existing-slot replacement; found {matches.Length}.");
        }

        if (!TryResolve(level, buildMode, out GreenWizardResidentSwapRoute route, out string reason))
            throw new InvalidOperationException(reason);

        MobySourcePatch patch = matches[0];
        if (!string.Equals(
                LevelCatalog.NormalizeKey(patch.LevelKey),
                route.TargetLevelKey,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The resident Green Wizard patch targets '{patch.LevelKey}', not registered target '{route.TargetLevelKey}'.");
        }

        switch (route.TargetLevelKey)
        {
            case "wizardpeak":
                WizardPeakGreenWizardNativeSwapExporter.ApplyAndVerify(
                    imagePath,
                    level,
                    sourcePatchPlan,
                    outputPlanPath);
                break;
            case "blowhard":
                BlowhardGreenWizardResidentSwapExporter.ApplyAndVerify(
                    imagePath,
                    level,
                    sourcePatchPlan,
                    outputPlanPath);
                break;
            case "magiccrafters":
                MagicCraftersGreenWizardResidentSwapExporter.ApplyAndVerify(
                    imagePath,
                    level,
                    sourcePatchPlan,
                    outputPlanPath);
                break;
            default:
                throw new InvalidOperationException(
                    $"No resident Green Wizard composer is registered for '{route.TargetLevelKey}'.");
        }

        if (!File.Exists(outputPlanPath))
            throw new InvalidDataException("The resident Green Wizard writer did not persist its verification plan.");

        LevelRuntimeBundleProfile profile = GreenWizardRuntimeBundleCatalog.FindProfile(level.Key) ??
            throw new InvalidOperationException($"The resident Green Wizard profile for '{level.Key}' disappeared during composition.");
        using FileStream stream = File.OpenRead(imagePath);
        string outputSha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (buildMode == GreenWizardResidentSwapBuildMode.Normal &&
            route.NormalCreateBinReady &&
            !string.Equals(outputSha256, profile.RuntimeProofOutputSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Normal Create BIN did not reproduce {level.DisplayName}'s runtime-proven Green Wizard image: expected {profile.RuntimeProofOutputSha256}, found {outputSha256}.");
        }
        return new GreenWizardResidentSwapCompositionSummary(
            route.RecipeId,
            route.TargetLevelKey,
            profile.Fingerprint,
            buildMode,
            patch.TrueIndex,
            imagePath,
            outputSha256,
            outputPlanPath,
            profile.RuntimeProofOutputSha256);
    }

    private static bool TryGetRegisteredRecipeId(string targetLevelKey, out string recipeId)
    {
        recipeId = LevelCatalog.NormalizeKey(targetLevelKey) switch
        {
            "wizardpeak" => WizardPeakGreenWizardNativeSwapExporter.RecipeId,
            "blowhard" => BlowhardGreenWizardResidentSwapExporter.RecipeId,
            "magiccrafters" => MagicCraftersGreenWizardResidentSwapExporter.RecipeId,
            _ => ""
        };
        return !string.IsNullOrWhiteSpace(recipeId);
    }

    private static bool TryReadActorId(string hex, out ushort actorId)
    {
        actorId = 0;
        try
        {
            string normalized = new((hex ?? "").Where(Uri.IsHexDigit).ToArray());
            if (normalized.Length != RecordStride * 2)
                return false;
            byte[] record = Convert.FromHexString(normalized);
            actorId = (ushort)(record[ActorIdOffset] | (record[ActorIdOffset + 1] << 8));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

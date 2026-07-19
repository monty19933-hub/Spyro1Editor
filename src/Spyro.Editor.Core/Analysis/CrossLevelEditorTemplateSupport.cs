using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Analysis;

public static class CrossLevelEditorTemplateSupport
{
    public static CrossLevelTemplateLevelStatus ResolveLevelStatus(
        LevelDefinition? currentLevel,
        string sourceLevelKey,
        string family,
        string supportStatus,
        string workspaceRoot = "")
    {
        if (ArtisansNativeLockedChestRuntimeBundleCompatibility.IsTemplate(sourceLevelKey, family))
            return ArtisansNativeLockedChestRuntimeBundleCompatibility.Resolve(currentLevel);

        if (GreenWizardRuntimeBundleCompatibility.IsTemplate(sourceLevelKey, family))
        {
            GreenWizardRuntimeBundleLevelSupport wizardSupport =
                GreenWizardRuntimeBundleCompatibility.Resolve(currentLevel, workspaceRoot);
            string shortLabel = wizardSupport.Status switch
            {
                GreenWizardRuntimeBundleStatus.Verified => "ready here",
                GreenWizardRuntimeBundleStatus.Candidate => "candidate here",
                GreenWizardRuntimeBundleStatus.Unsupported when !string.IsNullOrWhiteSpace(wizardSupport.RecipeId) => "blocked here",
                _ => "preview here"
            };
            return new CrossLevelTemplateLevelStatus(
                shortLabel,
                wizardSupport.Explanation,
                wizardSupport.RecipeId,
                wizardSupport.NormalCreateBinReady,
                wizardSupport.SwapPlaceable,
                wizardSupport.TrueAddPlaceable);
        }

        if (string.Equals(supportStatus, "supported-lightweight-object", StringComparison.OrdinalIgnoreCase))
            return new CrossLevelTemplateLevelStatus("ready", "Ready in this level as a lightweight source-record add.", "", true, true, true);

        if (currentLevel == null)
            return new CrossLevelTemplateLevelStatus("preview", "Preview only until a level is loaded.", "", false, false, false);

        if (string.Equals(supportStatus, "experimental-source-record-candidate", StringComparison.OrdinalIgnoreCase))
            return new CrossLevelTemplateLevelStatus("candidate here", $"{currentLevel.DisplayName} can place this as a source-record-only candidate. Normal Create BIN keeps it guarded; Create Candidate BIN writes a disposable test so you can see whether this level already has the needed actor behavior loaded.", "", false, true, true);

        CrossLevelActorPackageRecipe? recipe = CrossLevelActorPackageRecipeCatalog.FindPreferred(currentLevel.Key, sourceLevelKey, family, workspaceRoot);
        if (recipe == null && !string.IsNullOrWhiteSpace(workspaceRoot))
        {
            try
            {
                EditorWorkspace workspace = new(workspaceRoot);
                LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
                string sourceImagePath = DiscImageLocator.FindImage(workspace);
                recipe = CrossLevelChestPackageRecipePlanner.TryCreateRecipe(workspace, catalog, sourceImagePath, currentLevel.Key, sourceLevelKey, family);
            }
            catch
            {
                recipe = null;
            }
        }
        if (recipe == null)
            return new CrossLevelTemplateLevelStatus("preview here", $"Preview only in {currentLevel.DisplayName}; no package recipe is mapped for this level yet.", "", false, false, false);

        if (recipe.Status.StartsWith("in-game-blocked", StringComparison.OrdinalIgnoreCase) ||
            recipe.Status.StartsWith("blocked", StringComparison.OrdinalIgnoreCase))
        {
            return new CrossLevelTemplateLevelStatus(
                "blocked here",
                $"{currentLevel.DisplayName} has recipe {recipe.Id}, but it is currently {FormatSupportStatus(recipe.Status)}.",
                recipe.Id,
                false,
                false,
                false);
        }

        CrossLevelCandidateEvidence evidence = CrossLevelCandidateEvidenceStore.FindBestEvidence(workspaceRoot, currentLevel.Key, recipe.Id);
        if (evidence.Passed)
            return new CrossLevelTemplateLevelStatus("ready here", $"{currentLevel.DisplayName} has passed in-game evidence for recipe {recipe.Id}. Normal Create BIN can include it.", recipe.Id, true, true, true);

        if (string.Equals(recipe.Status, "experimental-image-write", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(recipe.Status, "experimental-plan-only", StringComparison.OrdinalIgnoreCase))
        {
            string evidenceNote = evidence.Failed
                ? " The previous in-game test failed, so the editor is keeping this as a guarded candidate."
                : "";
            return new CrossLevelTemplateLevelStatus("candidate here", $"{currentLevel.DisplayName} has recipe {recipe.Id}, but it still needs a passing in-game candidate test before normal Create BIN can use it.{evidenceNote}", recipe.Id, false, true, true);
        }

        return new CrossLevelTemplateLevelStatus("planned here", $"{currentLevel.DisplayName} has recipe {recipe.Id}, but it is still {FormatSupportStatus(recipe.Status)}.", recipe.Id, false, false, false);
    }

    public static bool CanTransform(MobyVisualKind visualKind, string family)
    {
        return family switch
        {
            "enemyTransform" => visualKind == MobyVisualKind.Actor,
            "lockedChest" or "springChest" or "fireworkChest" or "multiGemChest" => visualKind == MobyVisualKind.Chest,
            "key" => visualKind == MobyVisualKind.Key,
            _ => false
        };
    }

    private static string FormatSupportStatus(string status)
    {
        return status switch
        {
            "experimental-image-write" => "experimental test BIN writer",
            "experimental-plan-only" => "mapped but plan-only",
            "in-game-blocked-load-freeze" => "blocked by in-game loading freeze",
            "in-game-blocked-bad-level-state" => "blocked because the level loads into an invalid empty/ocean state",
            "in-game-blocked-missing-target-overlay-behavior" => "blocked because the target level does not contain this actor's behavior code",
            "in-game-blocked-invalid-actor-package-subfile" => "blocked because the actor package was placed outside the level's model subfile",
            "in-game-blocked-dynamic-life-statue-dependency" => "blocked because the replaced actor is dynamically spawned by the native Life Chest",
            "in-game-blocked-psyq-interrupt-state-overwrite" => "blocked because its EXE payload overwrites live PsyQ interrupt-system state",
            "blocked-invalid-actor-package-subfile" => "blocked because the planned package range is outside the level's model subfile",
            "experimental-actor-package-swap" => "needs an actor package swap",
            "experimental-actor-package-import" => "needs an actor package import",
            "experimental-source-record-candidate" => "guarded source-record-only candidate",
            "supported-lightweight-object" => "ready lightweight source-record add",
            "supported-target-runtime-bundle" => "ready target-specific runtime bundle",
            _ when string.IsNullOrWhiteSpace(status) => "unknown",
            _ => status.Replace('-', ' ')
        };
    }
}

public sealed record CrossLevelTemplateLevelStatus(
    string ShortLabel,
    string FullLabel,
    string RecipeId,
    bool Ready,
    bool Placeable,
    bool TrueAddPlaceable);

public sealed record ArtisansNativeLockedChestAddPolicy(
    bool OfferPair,
    bool OfferStandaloneKey,
    bool OfferLockedChestForExistingKey)
{
    public bool AllowLockedChestTemplate => OfferPair || OfferLockedChestForExistingKey;

    public static ArtisansNativeLockedChestAddPolicy Resolve(int addedKeyCount, int addedLockedChestCount)
    {
        if (addedKeyCount < 0)
            throw new ArgumentOutOfRangeException(nameof(addedKeyCount));
        if (addedLockedChestCount < 0)
            throw new ArgumentOutOfRangeException(nameof(addedLockedChestCount));

        bool noKey = addedKeyCount == 0;
        bool exactlyOneKey = addedKeyCount == 1;
        bool noLockedChest = addedLockedChestCount == 0;
        return new ArtisansNativeLockedChestAddPolicy(
            OfferPair: noKey && noLockedChest,
            OfferStandaloneKey: noKey,
            OfferLockedChestForExistingKey: exactlyOneKey && noLockedChest);
    }
}

/// <summary>
/// Editor-facing gate for the one runtime-proven native Key + Locked Chest bundle in Artisans.
/// The older actor-root slot-swap recipe remains blocked historical evidence; this route is a
/// separate handler/model/reward/texture composition and must never be generalized from labels.
/// </summary>
public static class ArtisansNativeLockedChestRuntimeBundleCompatibility
{
    public const string TargetLevelKey = "artisans";
    public const string SourceLevelKey = "peacekeepers";
    public const string KeyTemplateId = "common.key.peacekeepers.t78";
    public const string LockedChestTemplateId = "common.locked_chest.peacekeepers.t79";
    public const string RequiredExporterFeature = "ArtisansNativeLockedChestRuntimeBundleV2";
    public const string RecipeId = ArtisansNativeLockedChestVisualCandidateExporter.RecipeId;

    public static bool IsTemplate(string sourceLevelKey, string family) =>
        string.Equals(LevelCatalog.NormalizeKey(sourceLevelKey), SourceLevelKey, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(family, "lockedChest", StringComparison.OrdinalIgnoreCase);

    public static bool IsKeyTemplateId(string templateId) =>
        string.Equals(templateId, KeyTemplateId, StringComparison.OrdinalIgnoreCase);

    public static bool IsLockedChestTemplateId(string templateId) =>
        string.Equals(templateId, LockedChestTemplateId, StringComparison.OrdinalIgnoreCase);

    public static CrossLevelTemplateLevelStatus Resolve(LevelDefinition? currentLevel)
    {
        if (currentLevel == null)
        {
            return new CrossLevelTemplateLevelStatus(
                "preview",
                "Preview only until Artisans is loaded. The native Key + Locked Chest bundle is target-specific.",
                RecipeId,
                false,
                false,
                false);
        }

        if (!string.Equals(LevelCatalog.NormalizeKey(currentLevel.Key), TargetLevelKey, StringComparison.OrdinalIgnoreCase))
        {
            return new CrossLevelTemplateLevelStatus(
                "preview here",
                $"The Artisans transplant profile is not used in {currentLevel.DisplayName}. Levels that already contain a native 0x00AE Locked Chest use their same-level clone path; missing-chest levels still need their own handler, actor-package, texture-allocation, and runtime-proof profile.",
                RecipeId,
                false,
                false,
                false);
        }

        if (!ArtisansNativeLockedChestRuntimeBundleComposer.IsAvailable)
        {
            return new CrossLevelTemplateLevelStatus(
                "blocked here",
                $"The Artisans native Key + Locked Chest recipe {RecipeId} is runtime-proven, but normal Create BIN does not currently expose its structural composer.",
                RecipeId,
                false,
                false,
                false);
        }

        return new CrossLevelTemplateLevelStatus(
            "ready here",
            "Verified in Artisans for one Key + Key Chest pair. Normal Create BIN composes the native Key, Locked Chest handler/model, five delayed reward markers, privately rebased textures, six gem objects totaling +10 treasure, death/persistence path, and the checked WAD/EXE relocation. This profile intentionally permits one pair only.",
            RecipeId,
            true,
            true,
            true);
    }
}

public enum GreenWizardRuntimeBundleStatus
{
    Verified,
    Candidate,
    Unsupported
}

public sealed record GreenWizardRuntimeBundleLevelSupport(
    GreenWizardRuntimeBundleStatus Status,
    string Explanation,
    string RecipeId,
    bool SourceEvidencePresent,
    bool TargetActorRootPresent,
    bool SwapPlaceable,
    bool CandidateWritable,
    bool NormalCreateBinReady,
    bool PerInstancePropertiesAndRoute,
    bool TrueAddPlaceable);

/// <summary>
/// Editor-facing safety adapter for the Green Wizard dependency bundle. This is deliberately
/// target-specific: an actor-root match is useful evidence, but it does not prove that another
/// level has the Wizard update handler, lightning dependency, particles, or per-instance route.
/// </summary>
public static class GreenWizardRuntimeBundleCompatibility
{
    public const string TemplateId = "enemy.green_wizard.wizardpeak.t6";
    public const string BlowhardTemplateId = "enemy.green_wizard.blowhard.t0";
    public const string MagicCraftersTemplateId = "enemy.green_wizard.magiccrafters.t107";
    public const string SourceLevelKey = "wizardpeak";
    public const int SourceTrueIndex = 6;
    public const int WizardPeakFocusedTargetTrueIndex = 24;
    public const string BlowhardSourceLevelKey = "blowhard";
    public const int BlowhardSourceTrueIndex = 0;
    public const string MagicCraftersSourceLevelKey = "magiccrafters";
    public const int MagicCraftersSourceTrueIndex = 107;
    public const int MagicCraftersFocusedTargetTrueIndex = 27;
    public const int ActorId = 0x011B;
    public const string ToastyV11RecipeId = "toasty.wizardpeak.greenWizard.compactDependencyBundle.propsFixup.impactBurstQuarantine.genericHurtDamage.textureDependencies.lightningTrailParticleTexture.v11";
    public const string MagicCraftersResidentCandidateRecipeId = MagicCraftersGreenWizardResidentSwapExporter.RecipeId;

    // Set to true only when the normal/candidate editor exporter actually composes the proven
    // Toasty v11 bundle. The standalone research exporter existing in the assembly is not enough.
    public static bool ToastyV11ComposedExporterAvailable => false;

    // The generic cross-level special-data allocator cannot copy Green Wizard T6: its 0xC350
    // pointer is scene-relative and the 0x40-byte properties block contains a route pointer that
    // must be rebased. Blowhard uses its dedicated v2 resident writer in both normal and candidate builds.
    public static bool BlowhardDependencyAwareCandidateWriterAvailable => true;
    public static bool MagicCraftersDependencyAwareCandidateWriterAvailable => true;

    public static bool IsTemplate(string sourceLevelKey, string family)
    {
        string sourceKey = LevelCatalog.NormalizeKey(sourceLevelKey);
        return string.Equals(family, "enemyTransform", StringComparison.OrdinalIgnoreCase) &&
            sourceKey is SourceLevelKey or BlowhardSourceLevelKey or MagicCraftersSourceLevelKey;
    }

    public static bool IsGreenWizardDonor(string sourceLevelKey, int sourceTrueIndex, int actorId)
    {
        if (actorId != ActorId)
            return false;

        string sourceKey = LevelCatalog.NormalizeKey(sourceLevelKey);
        return sourceKey switch
        {
            SourceLevelKey => sourceTrueIndex == SourceTrueIndex,
            BlowhardSourceLevelKey => sourceTrueIndex == BlowhardSourceTrueIndex,
            MagicCraftersSourceLevelKey => sourceTrueIndex == MagicCraftersSourceTrueIndex,
            _ => false
        };
    }

    /// <summary>
    /// The first runtime-proven Magic Crafters resident-Wizard recipe has capacity and relocation
    /// proof for one exact destination only. This helper deliberately does not make the rest of
    /// the level's enemies eligible for dependency-bundle replacement.
    /// </summary>
    public static bool IsFocusedResidentCandidateTarget(string targetLevelKey, Moby moby)
    {
        if (!string.Equals(LevelCatalog.NormalizeKey(targetLevelKey), MagicCraftersSourceLevelKey, StringComparison.OrdinalIgnoreCase) ||
            moby.TrueIndex != MagicCraftersFocusedTargetTrueIndex ||
            moby.IsAdded ||
            moby.IsRemoved)
        {
            return false;
        }

        int originalType = moby.OriginalType >= 0 ? moby.OriginalType : moby.Type;
        int originalState = moby.OriginalState >= 0 ? moby.OriginalState : moby.State;
        int originalActorLow = moby.OriginalSourceByte36 >= 0 ? moby.OriginalSourceByte36 : moby.SourceByte36;
        int originalActorHigh = moby.OriginalSourceByte37 >= 0 ? moby.OriginalSourceByte37 : moby.SourceByte37;
        int originalByte4F = moby.OriginalSourceByte4F >= 0 ? moby.OriginalSourceByte4F : moby.SourceByte4F;
        int originalFlag4A = moby.OriginalFlag4A >= 0 ? moby.OriginalFlag4A : moby.Flag4A;
        int originalReward = moby.OriginalFlag4B >= 0 ? moby.OriginalFlag4B : moby.Flag4B;
        return originalType == 0x20 &&
            originalState == 0x00 &&
            originalActorLow == 0x0F &&
            originalActorHigh == 0x01 &&
            originalByte4F == 0x02 &&
            originalFlag4A == 0x10 &&
            originalReward == 0x55;
    }

    public static bool IsFocusedWizardPeakCandidateTarget(string targetLevelKey, Moby moby)
    {
        if (!string.Equals(LevelCatalog.NormalizeKey(targetLevelKey), SourceLevelKey, StringComparison.OrdinalIgnoreCase) ||
            moby.TrueIndex != WizardPeakFocusedTargetTrueIndex ||
            moby.IsAdded ||
            moby.IsRemoved)
        {
            return false;
        }

        int originalType = moby.OriginalType >= 0 ? moby.OriginalType : moby.Type;
        int originalState = moby.OriginalState >= 0 ? moby.OriginalState : moby.State;
        int originalActorLow = moby.OriginalSourceByte36 >= 0 ? moby.OriginalSourceByte36 : moby.SourceByte36;
        int originalActorHigh = moby.OriginalSourceByte37 >= 0 ? moby.OriginalSourceByte37 : moby.SourceByte37;
        int originalByte4F = moby.OriginalSourceByte4F >= 0 ? moby.OriginalSourceByte4F : moby.SourceByte4F;
        int originalFlag4A = moby.OriginalFlag4A >= 0 ? moby.OriginalFlag4A : moby.Flag4A;
        int originalReward = moby.OriginalFlag4B >= 0 ? moby.OriginalFlag4B : moby.Flag4B;
        return originalType == 0x20 &&
            originalState == 0x00 &&
            originalActorLow == 0x1D &&
            originalActorHigh == 0x01 &&
            originalByte4F == 0x00 &&
            originalFlag4A == 0x10 &&
            originalReward == 0x56;
    }

    public static GreenWizardRuntimeBundleLevelSupport Resolve(
        LevelDefinition? currentLevel,
        string workspaceRoot)
    {
        if (currentLevel == null)
        {
            return Unsupported(
                "Unsupported until a destination level is loaded. Green Wizard swaps need target-specific actor/behavior evidence; true Add also needs native Wizard properties and a patrol route.");
        }

        string targetKey = LevelCatalog.NormalizeKey(currentLevel.Key);
        RuntimeBundleCompatibilityResult bundleCompatibility = GreenWizardRuntimeBundleCatalog.Evaluate(targetKey);
        LevelRuntimeBundleProfile? bundleProfile = GreenWizardRuntimeBundleCatalog.FindProfile(targetKey);
        string evidenceSourceLevelKey = bundleProfile?.InstanceLayout.PropertiesDonorLevelKey ?? SourceLevelKey;
        int evidenceSourceTrueIndex = bundleProfile?.InstanceLayout.PropertiesDonorTrueIndex ?? SourceTrueIndex;
        GreenWizardDiscEvidence evidence = ReadDiscEvidence(
            currentLevel,
            workspaceRoot,
            evidenceSourceLevelKey,
            evidenceSourceTrueIndex);
        if (!evidence.SourceRecordPresent)
        {
            return Unsupported(
                $"Unsupported in {currentLevel.DisplayName}: {FormatLevelName(evidenceSourceLevelKey)} T{evidenceSourceTrueIndex} could not be confirmed as actor 0x{ActorId:X4} with native per-instance data in the selected original game image.");
        }

        if (targetKey.Equals("wizardpeak", StringComparison.OrdinalIgnoreCase))
        {
            bool runtimeProvenExactRoute =
                bundleCompatibility.NormalCreateBinReady &&
                !bundleCompatibility.RequiresRuntimeSmoke &&
                bundleProfile?.Evidence == RuntimeBundleEvidenceKind.RuntimeProven &&
                string.Equals(
                    bundleProfile.RuntimeProofRecipeId,
                    WizardPeakGreenWizardNativeSwapExporter.RecipeId,
                    StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(bundleProfile.RuntimeProofOutputSha256);
            if (!evidence.TargetActorRootPresent ||
                bundleCompatibility.Status != RuntimeBundleCompatibilityStatus.Ready ||
                bundleProfile?.Deployment != RuntimeBundleDeploymentKind.Native ||
                !runtimeProvenExactRoute)
            {
                return Unsupported(
                    "Unsupported in Wizard Peak because the native actor root or exact T6-to-T24 runtime-proof recipe could not be confirmed. The editor will not infer Wizard support from labels alone.");
            }

            return new GreenWizardRuntimeBundleLevelSupport(
                GreenWizardRuntimeBundleStatus.Verified,
                "Verified in Wizard Peak for the exact native T6-to-Elder-Wizard-T24 path. Runtime-proven v3 uses T24's already-matching detached pod/group 0xFF, installs T6's private 0x50-byte/two-point properties inside T24's unique 0x74-byte extent, preserves T24 placement/yaw/culling/reward and the trailing 0x24 bytes, and replaces stale fixup 0xCACC with 0xCAC0 at index 37 without changing the 0xBF count or shifting scene components. The user accepted visible lightning, the translated route, and one gem; live RAM separately confirmed actor/model initialization, attack state, movement, death, and retirement. Normal Create BIN and Create Swap Test compose this exact recipe. T10 v1/v2, other target slots, and true Add remain blocked.",
                WizardPeakGreenWizardNativeSwapExporter.RecipeId,
                true,
                true,
                true,
                true,
                true,
                true,
                false);
        }

        if (targetKey.Equals("blowhard", StringComparison.OrdinalIgnoreCase))
        {
            bool runtimeProvenResidentRoute =
                bundleCompatibility.Status == RuntimeBundleCompatibilityStatus.Ready &&
                bundleCompatibility.NormalCreateBinReady &&
                bundleProfile?.Deployment == RuntimeBundleDeploymentKind.ResidentActor &&
                bundleProfile.Evidence == RuntimeBundleEvidenceKind.RuntimeProven &&
                string.Equals(
                    bundleProfile.RuntimeProofRecipeId,
                    BlowhardGreenWizardResidentSwapExporter.RecipeId,
                    StringComparison.OrdinalIgnoreCase);
            bool guardedResidentRoute =
                bundleCompatibility.Status == RuntimeBundleCompatibilityStatus.Candidate &&
                bundleProfile?.Deployment == RuntimeBundleDeploymentKind.ResidentActor;
            if (!evidence.TargetActorRootPresent ||
                (!runtimeProvenResidentRoute && !guardedResidentRoute))
            {
                return Unsupported(
                    "Unsupported in Blowhard: actor 0x011B was not confirmed before the native actor-root terminator in the selected original game image.");
            }

            if (runtimeProvenResidentRoute)
            {
                return new GreenWizardRuntimeBundleLevelSupport(
                    GreenWizardRuntimeBundleStatus.Verified,
                    "Verified in Blowhard with the resident v2 recipe. Normal Create BIN and Create Swap Test both compose the checked private 0x40 properties block, translated one-point route, target pod/placement/yaw/reward preservation, scene-component shift, and 0x65-to-0x66 pointer-fixup append. Live DuckStation proof covered two real lightning hits, one terminating death animation/sound, one gem, persistent retirement, and normal nearby behavior. True Add stays unavailable because this profile proves one existing-slot allocation, not arbitrary appended instances.",
                    BlowhardGreenWizardResidentSwapExporter.RecipeId,
                    true,
                    true,
                    true,
                    BlowhardDependencyAwareCandidateWriterAvailable,
                    true,
                    false,
                    false);
            }

            return new GreenWizardRuntimeBundleLevelSupport(
                GreenWizardRuntimeBundleStatus.Candidate,
                "Candidate in Blowhard: the target image already loads actor 0x011B, and Create Swap Test statically checks a private native 0x40 properties block, translated one-point route, and 0x65-to-0x66 pointer-fixup append while preserving the selected enemy's placement and reward. Only live DuckStation promotion is pending. True Add stays unavailable because the current profile proves one guarded existing-slot allocation, not arbitrary appended instances.",
                "blowhard.native.greenWizard.resident-root",
                true,
                true,
                true,
                BlowhardDependencyAwareCandidateWriterAvailable,
                false,
                false,
                false);
        }

        if (targetKey.Equals(MagicCraftersSourceLevelKey, StringComparison.OrdinalIgnoreCase))
        {
            bool runtimeProvenResidentRoute =
                bundleCompatibility.Status == RuntimeBundleCompatibilityStatus.Ready &&
                bundleCompatibility.NormalCreateBinReady &&
                bundleProfile?.Deployment == RuntimeBundleDeploymentKind.ResidentActor &&
                bundleProfile.Evidence == RuntimeBundleEvidenceKind.RuntimeProven &&
                bundleProfile.ResidentDetection != null &&
                string.Equals(
                    bundleProfile.RuntimeProofRecipeId,
                    MagicCraftersGreenWizardResidentSwapExporter.RecipeId,
                    StringComparison.OrdinalIgnoreCase);
            if (!evidence.TargetActorRootPresent ||
                !runtimeProvenResidentRoute ||
                !MagicCraftersDependencyAwareCandidateWriterAvailable)
            {
                return Unsupported(
                    "Unsupported in Magic Crafters: the exact resident actor roots, dispatch handlers, T107 properties layout, T27 private extent, runtime proof, and in-place writer were not all confirmed.",
                    MagicCraftersResidentCandidateRecipeId,
                    sourceEvidencePresent: true,
                    targetActorRootPresent: evidence.TargetActorRootPresent);
            }

            return new GreenWizardRuntimeBundleLevelSupport(
                GreenWizardRuntimeBundleStatus.Verified,
                "Verified in Magic Crafters for the exact T107-to-T27 path. The level already owns the native actor 0x011B Wizard and actor 0x0026 lightning roots and handlers. Runtime-proven v2 reuses T27's unique 0x74-byte properties extent for T107's private 0x50-byte/two-point layout, preserves placement, yaw, culling, pod/group, reward, and the trailing 0x24 bytes, and removes stale target fixup 0xE9F8 (0xBE to 0xBD) without resizing or shifting any scene component. Normal Create BIN and Create Swap Test both compose this exact recipe. True Add and replacement of other Magic Crafters slots remain guarded until each allocation is proven.",
                MagicCraftersResidentCandidateRecipeId,
                true,
                true,
                true,
                true,
                true,
                false,
                false);
        }

        if (targetKey.Equals("toasty", StringComparison.OrdinalIgnoreCase))
        {
            bool runtimeBundleProofPresent =
                bundleCompatibility.Status == RuntimeBundleCompatibilityStatus.Ready &&
                bundleProfile?.Deployment == RuntimeBundleDeploymentKind.Transplanted &&
                bundleProfile.Evidence == RuntimeBundleEvidenceKind.RuntimeProven &&
                string.Equals(bundleProfile.RuntimeProofRecipeId, ToastyV11RecipeId, StringComparison.OrdinalIgnoreCase);
            if (!runtimeBundleProofPresent || !ToastyV11ComposedExporterAvailable)
            {
                return Unsupported(
                    $"Unsupported from the editor in Toasty for now: the Green Wizard v11 bundle is runtime-verified, but Create BIN/Create Candidate BIN do not yet compose {ToastyV11RecipeId}. Use Replace after that exporter route is connected; true Add remains guarded until its properties/route allocator is exposed.",
                    ToastyV11RecipeId,
                    sourceEvidencePresent: true,
                    targetActorRootPresent: evidence.TargetActorRootPresent);
            }

            return new GreenWizardRuntimeBundleLevelSupport(
                GreenWizardRuntimeBundleStatus.Verified,
                "Verified in Toasty with the v11 Green Wizard dependency bundle. Replace is available; normal Create BIN is enabled only because the composed exporter route is present. True Add remains unavailable until the bundle exposes a reusable per-instance properties/route allocator.",
                ToastyV11RecipeId,
                true,
                evidence.TargetActorRootPresent,
                true,
                false,
                true,
                false,
                false);
        }

        string compatibilityDetail = bundleCompatibility.Findings.Count > 0
            ? $" {string.Join(" ", bundleCompatibility.Findings)}"
            : "";
        return Unsupported(
            $"Unsupported in {currentLevel.DisplayName}: the runtime-bundle catalog status is {bundleCompatibility.Status}. Replace stays unavailable even if a coincidental actor id is found; Add also requires a proven properties/route allocator.{compatibilityDetail}",
            sourceEvidencePresent: true,
            targetActorRootPresent: evidence.TargetActorRootPresent);
    }

    public static bool CanWriteResidentExistingSlotCandidate(
        string targetLevelKey,
        string sourceLevelKey,
        int sourceTrueIndex,
        int actorId,
        bool sourceRecordHasPerInstanceData,
        bool targetActorRootPresent)
    {
        if (!IsResidentExistingSlotCandidateProfile(
                targetLevelKey,
                sourceLevelKey,
                sourceTrueIndex,
                actorId,
                sourceRecordHasPerInstanceData,
                targetActorRootPresent))
        {
            return false;
        }

        return LevelCatalog.NormalizeKey(targetLevelKey) switch
        {
            SourceLevelKey => true,
            BlowhardSourceLevelKey => BlowhardDependencyAwareCandidateWriterAvailable,
            MagicCraftersSourceLevelKey => MagicCraftersDependencyAwareCandidateWriterAvailable,
            _ => false
        };
    }

    public static bool IsResidentExistingSlotCandidateProfile(
        string targetLevelKey,
        string sourceLevelKey,
        int sourceTrueIndex,
        int actorId,
        bool sourceRecordHasPerInstanceData,
        bool targetActorRootPresent)
    {
        if (actorId != ActorId ||
            !sourceRecordHasPerInstanceData ||
            !targetActorRootPresent)
        {
            return false;
        }

        LevelRuntimeBundleProfile? profile = GreenWizardRuntimeBundleCatalog.FindProfile(targetLevelKey);
        bool nativeWizardPeakProfile = profile?.Deployment == RuntimeBundleDeploymentKind.Native &&
            profile.Evidence == RuntimeBundleEvidenceKind.NativeGame;
        bool residentProfile = profile?.Deployment == RuntimeBundleDeploymentKind.ResidentActor &&
            profile.ResidentDetection != null &&
            profile.Evidence is RuntimeBundleEvidenceKind.StaticResidentDetection or RuntimeBundleEvidenceKind.RuntimeProven;
        return profile != null &&
            (nativeWizardPeakProfile || residentProfile) &&
            profile.ResidentActorIds.Contains((ushort)ActorId) &&
            string.Equals(
                LevelCatalog.NormalizeKey(profile.InstanceLayout.PropertiesDonorLevelKey),
                LevelCatalog.NormalizeKey(sourceLevelKey),
                StringComparison.OrdinalIgnoreCase) &&
            profile.InstanceLayout.PropertiesDonorTrueIndex == sourceTrueIndex;
    }

    private static GreenWizardRuntimeBundleLevelSupport Unsupported(
        string explanation,
        string recipeId = "",
        bool sourceEvidencePresent = false,
        bool targetActorRootPresent = false) =>
        new(
            GreenWizardRuntimeBundleStatus.Unsupported,
            explanation,
            recipeId,
            sourceEvidencePresent,
            targetActorRootPresent,
            false,
            false,
            false,
            false,
            false);

    private static GreenWizardDiscEvidence ReadDiscEvidence(
        LevelDefinition targetLevel,
        string workspaceRoot,
        string sourceLevelKey,
        int sourceTrueIndex)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
            return GreenWizardDiscEvidence.None;

        try
        {
            EditorWorkspace workspace = new(workspaceRoot);
            LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
            LevelDefinition? sourceLevel = catalog.FindByKey(sourceLevelKey);
            if (sourceLevel == null ||
                !sourceLevel.HasSourceTable ||
                sourceTrueIndex < 0 ||
                sourceTrueIndex >= sourceLevel.SourceRecordCount ||
                !targetLevel.HasSourceTable ||
                string.IsNullOrWhiteSpace(targetLevel.SourceTableRelativeOffset))
            {
                return GreenWizardDiscEvidence.None;
            }

            string imagePath = DiscImageLocator.FindImage(workspace);
            if (!File.Exists(imagePath))
                return GreenWizardDiscEvidence.None;

            DiscLayout layout = DiscImage.DetectLayout(imagePath);
            using FileStream stream = File.OpenRead(imagePath);
            long sourceTableWadOffset = ParseNumber(sourceLevel.SourceTableWadOffset);
            byte[] sourceRecord = DiscImage.ReadFileBytes(
                stream,
                layout,
                37,
                sourceTableWadOffset + ((long)sourceTrueIndex * 0x58),
                0x58);
            int sourceActorId = sourceRecord[0x36] | (sourceRecord[0x37] << 8);
            bool sourceRecordPresent =
                sourceRecord[0x50] == 0x20 &&
                sourceActorId == ActorId &&
                BitConverter.ToUInt32(sourceRecord, 0) != 0;

            long targetTableWadOffset = ParseNumber(targetLevel.SourceTableWadOffset);
            long targetTableRelativeOffset = ParseNumber(targetLevel.SourceTableRelativeOffset);
            long targetEntryBase = targetTableWadOffset - targetTableRelativeOffset;
            bool targetActorRootPresent = false;
            for (int index = 0; index < 64; index++)
            {
                uint root = BitConverter.ToUInt32(
                    DiscImage.ReadFileBytes(stream, layout, 37, targetEntryBase + 0x50 + (index * 4L), 4),
                    0);
                if (root == 0)
                    break;

                int actorId = BitConverter.ToUInt16(
                    DiscImage.ReadFileBytes(stream, layout, 37, targetEntryBase + 0x150 + (index * 2L), 2),
                    0);
                if (actorId == ActorId)
                {
                    targetActorRootPresent = true;
                    break;
                }
            }

            return new GreenWizardDiscEvidence(sourceRecordPresent, targetActorRootPresent);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or FormatException or OverflowException or UnauthorizedAccessException)
        {
            return GreenWizardDiscEvidence.None;
        }
    }

    private static long ParseNumber(string value)
    {
        string text = (value ?? "").Trim();
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToInt64(text[2..], 16)
            : Convert.ToInt64(text, 10);
    }

    private static string FormatLevelName(string levelKey) =>
        LevelCatalog.NormalizeKey(levelKey) switch
        {
            SourceLevelKey => "Wizard Peak",
            BlowhardSourceLevelKey => "Blowhard",
            MagicCraftersSourceLevelKey => "Magic Crafters",
            _ => levelKey
        };

    private readonly record struct GreenWizardDiscEvidence(bool SourceRecordPresent, bool TargetActorRootPresent)
    {
        public static GreenWizardDiscEvidence None { get; } = new(false, false);
    }
}

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
        if (string.Equals(supportStatus, "supported-lightweight-object", StringComparison.OrdinalIgnoreCase))
            return new CrossLevelTemplateLevelStatus("ready", "Ready in this level as a lightweight source-record add.", "", true, true);

        if (currentLevel == null)
            return new CrossLevelTemplateLevelStatus("preview", "Preview only until a level is loaded.", "", false, false);

        if (string.Equals(supportStatus, "experimental-source-record-candidate", StringComparison.OrdinalIgnoreCase))
            return new CrossLevelTemplateLevelStatus("candidate here", $"{currentLevel.DisplayName} can place this as a source-record-only candidate. Normal Create BIN keeps it guarded; Create Candidate BIN writes a disposable test so you can see whether this level already has the needed actor behavior loaded.", "", false, true);

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
            return new CrossLevelTemplateLevelStatus("preview here", $"Preview only in {currentLevel.DisplayName}; no package recipe is mapped for this level yet.", "", false, false);

        CrossLevelCandidateEvidence evidence = CrossLevelCandidateEvidenceStore.FindBestEvidence(workspaceRoot, currentLevel.Key, recipe.Id);
        if (evidence.Passed)
            return new CrossLevelTemplateLevelStatus("ready here", $"{currentLevel.DisplayName} has passed in-game evidence for recipe {recipe.Id}. Normal Create BIN can include it.", recipe.Id, true, true);

        if (string.Equals(recipe.Status, "experimental-image-write", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(recipe.Status, "experimental-plan-only", StringComparison.OrdinalIgnoreCase))
        {
            string evidenceNote = evidence.Failed
                ? " The previous in-game test failed, so the editor is keeping this as a guarded candidate."
                : "";
            return new CrossLevelTemplateLevelStatus("candidate here", $"{currentLevel.DisplayName} has recipe {recipe.Id}, but it still needs a passing in-game candidate test before normal Create BIN can use it.{evidenceNote}", recipe.Id, false, true);
        }

        if (recipe.Status.StartsWith("in-game-blocked", StringComparison.OrdinalIgnoreCase))
            return new CrossLevelTemplateLevelStatus("blocked here", $"{currentLevel.DisplayName} has recipe {recipe.Id}, but it is currently {FormatSupportStatus(recipe.Status)}.", recipe.Id, false, false);

        return new CrossLevelTemplateLevelStatus("planned here", $"{currentLevel.DisplayName} has recipe {recipe.Id}, but it is still {FormatSupportStatus(recipe.Status)}.", recipe.Id, false, false);
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
            "experimental-actor-package-swap" => "needs an actor package swap",
            "experimental-actor-package-import" => "needs an actor package import",
            "experimental-source-record-candidate" => "guarded source-record-only candidate",
            "supported-lightweight-object" => "ready lightweight source-record add",
            _ when string.IsNullOrWhiteSpace(status) => "unknown",
            _ => status.Replace('-', ' ')
        };
    }
}

public sealed record CrossLevelTemplateLevelStatus(string ShortLabel, string FullLabel, string RecipeId, bool Ready, bool Placeable);

using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Workspace;

public enum LevelEditorExperience
{
    ExistingLevel,
    FreshBlankLevel
}

public static class LevelEditorExperienceResolver
{
    public static LevelEditorExperience Resolve(LevelDefinition? level) =>
        UnusedLevel65BlankLevelLabProfileRegistry.IsLabLevel(level)
            ? LevelEditorExperience.FreshBlankLevel
            : LevelEditorExperience.ExistingLevel;
}

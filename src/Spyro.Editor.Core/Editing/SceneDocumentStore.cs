using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Editing;

public sealed record SceneTargetBinding(string LevelKey, int LevelId, int BodyWadEntry,
    int OverlayWadEntry, string SourceSha256, int NativePrefixRows)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LevelKey) || LevelKey != LevelCatalog.NormalizeKey(LevelKey) ||
            LevelKey.Any(c => !char.IsAsciiLetterOrDigit(c)) || LevelId is < 10 or > 64 || LevelId % 10 > 4 ||
            BodyWadEntry is < 10 or > 78 || OverlayWadEntry != BodyWadEntry - 1 || NativePrefixRows is < 0 or > 256 ||
            SourceSha256 != UniversalSourceLibrary.OriginalUsaSha256)
            throw new InvalidDataException("The blank level target is not an authenticated normal retail level.");
    }
    public LevelDefinition Resolve(LevelCatalog catalog)
    {
        Validate();
        var level = catalog.FindByKey(LevelKey) ?? throw new InvalidDataException("The blank level target is missing from the source catalog.");
        if (level.LevelId != LevelId || level.SourceWadEntry != BodyWadEntry)
            throw new InvalidDataException("The blank level target does not match its source catalog slot.");
        return level;
    }
}

public static class SceneDocumentStore
{
    public static string PathForTarget(string workspace, string key)
    {
        if (key == "authored65") return Path.Combine(workspace, SceneDocument.FileName);
        if (string.IsNullOrWhiteSpace(key) || key != LevelCatalog.NormalizeKey(key) || key.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new InvalidDataException("Invalid scene target key.");
        return Path.Combine(workspace, key + "-replacement.scene.json");
    }
    public static SceneDocument? ReadForTarget(string workspace, LevelDefinition level, LevelCatalog catalog)
    {
        string path = PathForTarget(workspace, level.Key);
        if (!File.Exists(path)) return null;
        var doc = SceneDocument.Read(path);
        if (doc.ResolveTarget(catalog).Key != level.Key)
            throw new InvalidDataException("The saved replacement scene belongs to another level.");
        return doc;
    }
    public static IReadOnlyList<SceneDocument> LoadOverrides(string workspace, LevelCatalog catalog) =>
        ReadAll(workspace, catalog).Where(d => d.TargetBinding != null).ToArray();
    public static IReadOnlyList<SceneDocument> ReadAll(string workspace, LevelCatalog catalog)
    {
        List<SceneDocument> scenes = [];
        if (File.Exists(PathForTarget(workspace,"authored65")))
            scenes.Add(ReadForTarget(workspace,SceneDocument.NativeTarget(""),catalog)!);
        foreach (var level in catalog.Levels.Where(l => l.LevelId is >= 10 and <= 64 && l.LevelId % 10 <= 4))
            if (ReadForTarget(workspace,level,catalog) is {} doc) scenes.Add(doc);
        return scenes;
    }
}

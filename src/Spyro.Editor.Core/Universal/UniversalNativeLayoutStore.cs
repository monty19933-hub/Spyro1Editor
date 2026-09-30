using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Universal;

/// <summary>Binds lifetime relative slots to the unchanged native prefix. Never moves collected
/// identities when another export stage changes the row count.</summary>
public static class UniversalNativeLayoutStore
{
    private sealed record Binding(int SchemaVersion, string SourceSha256, string LevelKey, int NativePrefixRows);

    public static string PathFor(string workspace, string levelKey) =>
        Path.Combine(workspace, LevelCatalog.NormalizeKey(levelKey) + "-universal-layout-v1.json");

    public static void Validate(string workspace, string levelKey, int nativeRows)
    {
        string path = PathFor(workspace, levelKey);
        if (!File.Exists(path)) return;
        var binding = JsonSerializer.Deserialize<Binding>(File.ReadAllBytes(path));
        if (binding == null || binding.SchemaVersion != 1 || binding.SourceSha256 != UniversalSourceLibrary.OriginalUsaSha256 ||
            binding.LevelKey != LevelCatalog.NormalizeKey(levelKey))
            throw new InvalidDataException("The saved object layout belongs to a different source or an unsupported version. Export stopped to protect collected state.");
        if (binding.NativePrefixRows != nativeRows)
            throw new InvalidDataException($"{levelKey}: another object export changed the rows before the imported objects ({binding.NativePrefixRows} to {nativeRows}). Restore that edit, or use a separate project and new game; this export would move saved collectible identities.");
    }

    public static void Bind(string workspace, string levelKey, int nativeRows)
    {
        Validate(workspace, levelKey, nativeRows);
        string path = PathFor(workspace, levelKey);
        if (File.Exists(path)) return;
        string temporary = path + ".pending-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new Binding(1, UniversalSourceLibrary.OriginalUsaSha256,
                LevelCatalog.NormalizeKey(levelKey), nativeRows), new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path); // concurrent changes are refused, never overwritten
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

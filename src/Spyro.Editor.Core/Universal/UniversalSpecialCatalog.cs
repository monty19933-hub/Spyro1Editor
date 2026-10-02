using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Universal;

/// <summary>Catalogue entries that do not come from a retail USA row: objects recovered from the
/// Japanese release (dragonfly eggs and signs), with native USA behavior written for them.</summary>
public static class UniversalSpecialCatalog
{
    public const string SourceKey = "japan";
    public const string TemplatePrefix = "special:";
    private const int EggModelBytes = 2056, SignModelBytes = 2664, EggCodeBytes = 0x150, SignCodeBytes = 0x1C0;

    public static bool IsSpecial(UniversalMobyVariant v) => v.SourceLevelKey is SourceKey or UniversalHighValueGems.SourceKey;

    public static bool NeedsRenderHook(string? id) => id == UniversalDragonflyEggs.SignId || UniversalHighValueGems.IsId(id);

    public static string? SpecialIdForClass(int classId) =>
        classId == UniversalHighValueGems.TealClass ? UniversalHighValueGems.TealId
        : classId == UniversalHighValueGems.RainbowClass ? UniversalHighValueGems.RainbowId
        : classId == UniversalDragonflyEggs.SignClass ? UniversalDragonflyEggs.SignId
        : UniversalDragonflyEggs.Variants.FirstOrDefault(v => v.Class == classId).Id;

    public static string DisplayName(int classId) =>
        classId == UniversalHighValueGems.TealClass ? "Teal gem (50)"
        : classId == UniversalHighValueGems.RainbowClass ? "Rainbow gem (100)"
        : classId == UniversalDragonflyEggs.SignClass ? "Sign"
        : UniversalDragonflyEggs.Variants.FirstOrDefault(v => v.Class == classId).Name ?? $"Object 0x{classId:X3}";

    public static IReadOnlyList<UniversalMobyCatalogEntry> Entries(bool japaneseDiscAvailable)
    {
        string? restriction = japaneseDiscAvailable ? null :
            "This comes from the Japanese version of Spyro. Press \"Choose Japanese disc…\" at the top of this window to unlock it.";
        var eggs = UniversalDragonflyEggs.Variants.Select(v => new UniversalMobyVariant(v.Class, SourceKey,
            UniversalDragonflyEggs.JapaneseEggRow, 16, EggModelBytes, EggCodeBytes, true, false, true)).ToList();
        var sign = new UniversalMobyVariant(UniversalDragonflyEggs.SignClass, SourceKey, UniversalDragonflyEggs.JapaneseSignRow,
            UniversalSpecialBehaviors.SignLineBytes * UniversalSpecialBehaviors.SignLines, SignModelBytes, SignCodeBytes, true, false, true);
        return
        [
            HighValueGemEntry(UniversalHighValueGems.TealClass, "Teal gem (50)", 50),
            HighValueGemEntry(UniversalHighValueGems.RainbowClass, "Rainbow gem (100)", 100),
            new(eggs[0].ClassId, "Dragonfly Egg", "Collectibles", "Dragonfly Eggs", eggs, eggs[0], [SourceKey], restriction,
                "Choose its colour above. Collection updates the level and global dragonfly-egg counters and completion score. The game's memory-card save preserves collected eggs. Newly earned bonus access also requires every placed dragonfly egg."),
            new(sign.ClassId, "Sign", "Interactive", "Signs", [sign], sign, [SourceKey], restriction,
                "When Spyro stands next to it, your message (up to three short lines) appears on screen. After placing it, select it and press Edit to type the message."),
        ];
    }

    private static UniversalMobyCatalogEntry HighValueGemEntry(int classId, string name, int value)
    {
        var variant = new UniversalMobyVariant(classId, UniversalHighValueGems.SourceKey, -1,
            16, 1024, 4096, true, false, true);
        return new(classId, name, "Collectibles", "Gems", [variant], variant,
            [UniversalHighValueGems.SourceKey], null,
            $"A bright, double-size gem with a sparkling effect. Adds {value} to collected treasure and this level's available total. Collected state is saved by the game. Uses your USA disc; no Japanese disc is needed.");
    }
}

/// <summary>Finds and remembers the user's original Japanese disc (read only).</summary>
public static class UniversalJapaneseDiscLocator
{
    private static readonly string[] Names = ["Spyro the Dragon (Japan).bin", "Spyro the Dragon (Japan)-CLEAN-ORIGINAL.bin"];
    private static readonly Dictionary<(string, long, DateTime), bool> Verified = [];

    public static string ConfigPath(EditorWorkspace workspace) => Path.Combine(workspace.RootPath, "_local", "settings", "japanese-disc.json");

    public static bool IsOriginal(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        var info = new FileInfo(path);
        var key = (info.FullName, info.Length, info.LastWriteTimeUtc);
        lock (Verified) if (Verified.TryGetValue(key, out bool hit)) return hit;
        bool ok;
        using (var fs = File.OpenRead(path)) ok = Convert.ToHexStringLower(SHA256.HashData(fs)) == UniversalJapaneseSource.OriginalJapanSha256;
        lock (Verified) Verified[key] = ok;
        return ok;
    }

    /// <summary>Configured disc if still valid, else a conventionally named disc near the workspace; "" if none.</summary>
    public static string Find(EditorWorkspace workspace)
    {
        try
        {
            string cfg = ConfigPath(workspace);
            if (File.Exists(cfg))
            {
                using var doc = JsonDocument.Parse(File.ReadAllBytes(cfg));
                if (doc.RootElement.TryGetProperty("imagePath", out var p) && p.GetString() is { } s && File.Exists(s)) return s;
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        foreach (string path in WorkspaceArtifactSearchPolicy.EnumerateCandidateRoots(workspace).SelectMany(r => Names.Select(n => Path.Combine(r, n))))
            if (File.Exists(path)) return path;
        return "";
    }

    public static void Configure(EditorWorkspace workspace, string path)
    {
        if (!IsOriginal(path)) throw new InvalidDataException("That file is not the unchanged original Spyro the Dragon (Japan) disc image (SCPS-10083).");
        string cfg = ConfigPath(workspace);
        Directory.CreateDirectory(Path.GetDirectoryName(cfg)!);
        File.WriteAllText(cfg, JsonSerializer.Serialize(new { selectedAt = DateTime.Now.ToString("o"), imagePath = Path.GetFullPath(path) },
            new JsonSerializerOptions { WriteIndented = true }));
    }
}

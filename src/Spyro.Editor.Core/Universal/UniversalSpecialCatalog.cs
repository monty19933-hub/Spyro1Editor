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

    public static bool IsSpecial(UniversalMobyVariant v) => v.SourceLevelKey is SourceKey or UniversalHighValueGems.SourceKey or UniversalCrashBoxes.SourceKey
        or UniversalCrashEnemies.YotdSourceKey;

    public static bool RequiresJapaneseSource(string? id) => id != null && !UniversalHighValueGems.IsId(id) && !RequiresCrashSource(id) && !RequiresYearOfTheDragonSource(id);

    public static bool RequiresCrashSource(string? id) => UniversalCrashBoxes.IsId(id) || UniversalCrashEnemies.IsCrashId(id) || UniversalCrashPlayer.IsId(id);

    public static bool RequiresYearOfTheDragonSource(string? id) => UniversalCrashEnemies.IsYotdId(id);

    public static bool NeedsRenderHook(string? id) => id == UniversalDragonflyEggs.SignId;

    public static string? SpecialIdForClass(int classId) =>
        classId == UniversalHighValueGems.TealClass ? UniversalHighValueGems.TealId
        : classId == UniversalHighValueGems.RainbowClass ? UniversalHighValueGems.RainbowId
        : classId == UniversalDragonflyEggs.SignClass ? UniversalDragonflyEggs.SignId
        : UniversalCrashBoxes.IsPlaceableClass(classId) ? UniversalCrashBoxes.IdForClass(classId)
        : UniversalCrashEnemies.IsPlaceableClass(classId) ? UniversalCrashEnemies.IdForClass(classId)
        : UniversalDragonflyEggs.Variants.FirstOrDefault(v => v.Class == classId).Id;

    public static string DisplayName(int classId) =>
        classId == UniversalHighValueGems.TealClass ? "Teal gem (50)"
        : classId == UniversalHighValueGems.RainbowClass ? "Rainbow gem (100)"
        : classId == UniversalDragonflyEggs.SignClass ? "Sign"
        : UniversalCrashBoxes.NameForClass(classId) is { } crashName ? crashName
        : UniversalCrashEnemies.NameForClass(classId) is { } enemyName ? enemyName
        : UniversalDragonflyEggs.Variants.FirstOrDefault(v => v.Class == classId).Name ?? $"Object 0x{classId:X3}";

    public static IReadOnlyList<UniversalMobyCatalogEntry> Entries(bool japaneseDiscAvailable, bool crashDiscAvailable = false, bool yotdDiscAvailable = false)
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
                "Choose its color above. When Spyro picks it up, the inventory counts it for this level and for the whole game, the memory card remembers it, and it counts toward completing the game. The bonus level also needs every dragonfly egg you placed."),
            new(sign.ClassId, "Sign", "Interactive", "Signs", [sign], sign, [SourceKey], restriction,
                "When Spyro stands next to it, your message (up to three short lines) appears on screen. After placing it, select it and press Edit to type the message."),
            .. CrashEntries(crashDiscAvailable),
            .. YotdEntries(yotdDiscAvailable)
        ];
    }

    public const int CrashBoxModelBytes = 1000, CrashBoxCodeBytes = 0xA00, CrashEnemyCodeBytes = 0x1400;
    /// <summary>Model bytes per enemy, its robe-off model included (the converted packages are a little smaller).</summary>
    public static int CrashEnemyModelBytes(UniversalCrashEnemies.Enemy e) =>
        e == UniversalCrashEnemies.Frog ? 15000 : e == UniversalCrashEnemies.Knight ? 28000 : e == UniversalCrashEnemies.Rhynoc ? 23000
        : e == UniversalCrashEnemies.Crab ? 18000 : 41000;

    private static IEnumerable<UniversalMobyCatalogEntry> YotdEntries(bool yotdDiscAvailable)
    {
        string? restriction = yotdDiscAvailable ? null :
            "This creature comes from Spyro: Year of the Dragon. Choose your disc with \"Year of the Dragon disc…\" in Game Settings → Music.";
        string[] sources = [UniversalCrashEnemies.YotdSourceKey];
        UniversalMobyVariant Creature(UniversalCrashEnemies.Enemy e) =>
            new(e.Class, UniversalCrashEnemies.YotdSourceKey, -1, 16, CrashEnemyModelBytes(e), CrashEnemyCodeBytes, true, false, true);
        var rhynoc = Creature(UniversalCrashEnemies.Rhynoc);
        var crab = Creature(UniversalCrashEnemies.Crab);
        yield return new(rhynoc.ClassId, UniversalCrashEnemies.Rhynoc.Name, "Enemies", "Spyro: Year of the Dragon", [rhynoc], rhynoc, sources, restriction,
            "A blue rhynoc from Lost Fleet. He guards his spot, runs at Spyro when he sees him and belly-flops on him (a hit costs a Sparx level), and walks back when Spyro gets away. Flame him, charge him or jump on him to knock him flying; he drops a green gem (2), which counts toward the level's treasure.");
        yield return new(crab.ClassId, UniversalCrashEnemies.Crab.Name, "Enemies", "Spyro: Year of the Dragon", [crab], crab, sources, restriction,
            "A big-eyed red crab from Lost Fleet. It sidles up to Spyro and slams its claws down in front of it (a hit costs a Sparx level). Flame it, charge it or jump on it to knock it flying; it drops a blue gem (5), which counts toward the level's treasure.");
    }

    private static IEnumerable<UniversalMobyCatalogEntry> CrashEntries(bool crashDiscAvailable)
    {
        string? restriction = crashDiscAvailable ? null :
            "This uses box art from Crash Bandicoot: Warped. Press \"Crash disc…\" at the top of this window to choose your copy.";
        UniversalMobyVariant Variant(int cls) => new(cls, UniversalCrashBoxes.SourceKey, -1, 16, CrashBoxModelBytes, CrashBoxCodeBytes, true, false, true);
        var crates = UniversalCrashBoxes.Crates.Select(c => Variant(c.Class)).ToList();
        var tnt = Variant(UniversalCrashBoxes.TntClass);
        var nitro = Variant(UniversalCrashBoxes.NitroClass);
        var nitroSwitch = Variant(UniversalCrashBoxes.SwitchClass);
        string[] sources = [UniversalCrashBoxes.SourceKey];
        yield return new(crates[1].ClassId, "Crash crate", "Interactive", "Crash Bandicoot", crates, crates[1], sources, restriction,
            "Breaks when Spyro flames it, charges it or jumps on it (he bounces off, Crash style), and drops what you chose above: a gem, which counts toward the level's treasure, or a butterfly. Stays broken like Spyro's baskets.");
        yield return new(tnt.ClassId, "TNT crate", "Interactive", "Crash Bandicoot", [tnt], tnt, sources, restriction,
            "Jump on it and it counts down 3, 2, 1, then explodes. Flaming or charging it sets it off at once. The blast costs Spyro one Sparx level if he's close, and breaks nearby crates and sets off other TNT and Nitro.");
        yield return new(nitro.ClassId, "Nitro crate", "Interactive", "Crash Bandicoot", [nitro], nitro, sources, restriction,
            "Hops in place and explodes when anything touches it, taking all of Spyro's health: touching or charging it is fatal. Flame it from a distance, or set it off with a TNT blast.");
        yield return new(nitroSwitch.ClassId, "Nitro switch box", "Interactive", "Crash Bandicoot", [nitroSwitch], nitroSwitch, sources, restriction,
            "The green \"!\" box. Jump on it (Spyro bounces off), flame it or charge it, and every Nitro crate in the level explodes; the ones out of sight go up as soon as they come into view. It goes in a flash once pressed, and is back if Spyro leaves the level and returns.");
        string? enemyRestriction = crashDiscAvailable ? null :
            "This enemy comes from Crash Bandicoot: Warped. Press \"Crash disc…\" at the top of this window to choose your copy.";
        UniversalMobyVariant Enemy(UniversalCrashEnemies.Enemy e) =>
            new(e.Class, UniversalCrashEnemies.SourceKey, -1, 16, CrashEnemyModelBytes(e), CrashEnemyCodeBytes, true, false, true);
        var frog = Enemy(UniversalCrashEnemies.Frog);
        var knight = Enemy(UniversalCrashEnemies.Knight);
        var wizard = Enemy(UniversalCrashEnemies.Wizard);
        yield return new(frog.ClassId, UniversalCrashEnemies.Frog.Name, "Enemies", "Crash Bandicoot", [frog], frog, sources, enemyRestriction,
            "A Toad Village frog. It sits croaking, then hops towards Spyro when it sees him; touching it costs a Sparx level. Flame it, charge it or jump on it and it is knocked flying, Crash style, leaving a butterfly.");
        yield return new(knight.ClassId, UniversalCrashEnemies.Knight.Name, "Enemies", "Crash Bandicoot", [knight], knight, sources, enemyRestriction,
            "A Toad Village knight. He guards his spot, marches up to Spyro and swings his sword (a hit costs a Sparx level), and walks back when Spyro gets away. Flame him, charge him or jump on his head to knock him flying; he drops a green gem (2), which counts toward the level's treasure.");
        yield return new(wizard.ClassId, UniversalCrashEnemies.Wizard.Name, "Enemies", "Crash Bandicoot", [wizard], wizard, sources, enemyRestriction,
            "A Gee Wiz lab assistant in wizard's robes. He turns to face Spyro and casts a zap when Spyro is close and in front of him. Flame him, charge him or jump on him and his robe blows off before he is knocked flying; he drops a blue gem (5), which counts toward the level's treasure.");
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

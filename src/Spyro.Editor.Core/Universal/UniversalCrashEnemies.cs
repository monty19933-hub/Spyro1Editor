using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Spyro.Editor.Core.Crash;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.YearOfTheDragon;

namespace Spyro.Editor.Core.Universal;

/// <summary>Crash Bandicoot: Warped's medieval enemies as Spyro objects: their own models, textures, animations
/// and sounds converted from the user's Crash disc (<see cref="CrashSpyroModel"/>), with Spyro behaviour written
/// for them (<see cref="UniversalSpecialBehaviors"/>). Sizes keep Crash's proportions against the Crash crates
/// (a crate's 407,000 Crash units are the crate's 736 Spyro units).</summary>
public static class UniversalCrashEnemies
{
    public const string SourceKey = UniversalCrashBoxes.SourceKey;
    public const double CrashUnitsPerSpyroUnit = 553;
    // Toad Village (frogs, knights) and Gee Wiz (wizards), checked against the original files.
    public const string ToadVillageFile = "S0/S000000B.NSF";
    public const string ToadVillageSha256 = "f5744a2434bcbae0adf067232c0dddbcef0ad915aa59386c44abaae123cdd44c";
    public const string GeeWizFile = "S0/S000000F.NSF";
    public const string GeeWizSha256 = "35b60c0b49bbc4ea6d715b834deb1b34376f8a8ba3d91d3428433a352f100523";

    /// <summary>Package sound slots every enemy shares: 0 knocked flying, 1 stomped (Crash's bounce); the rest are
    /// the enemy's own, played by its animations.</summary>
    public const int FlySlot = 0, BounceSlot = 1;

    public sealed record Model(int Class, string Name, CrashSpyroModel.Animation[] Animations);

    /// <summary>Behaviour distances in world units: touch reach, model height, Spyro's height when standing on it,
    /// sight, leash from home (0 = stays put), speed per update, stomp reach, and attack start / hit reach.</summary>
    public sealed record Tuning(int Touch, int Top, int Stand, int Sight, int Leash, int Speed, int Stomp, int Attack, int Reach);

    /// <param name="Size">Size against Crash's own proportions (1 = a crate's size to Crash): smaller sits better
    /// next to Spyro, who is about crate height.</param>
    /// <param name="Companion">A second model the enemy switches to when it is hit (the wizard losing his robe).</param>
    public sealed record Enemy(string Id, string Name, string File, string Sha256, Model Main, string[] Sounds, byte Reward,
        double Size, Tuning Tuning, Model? Companion = null)
    {
        public int Class => Main.Class;
        /// <summary>The behaviour (UniversalSpecialBehaviors.CrashEnemyFrog, -Knight or -Wizard) when it is not the
        /// Crash enemy's own: a creature from another game that hops, or guards, marches and attacks.</summary>
        public int Kind { get; init; } = -1;
        /// <summary>A creature from Spyro: Year of the Dragon: its animated model set in Lost Fleet's model block and the
        /// animations its behaviour plays (as a Crash enemy's: guard, march, attack for a knight).</summary>
        public int YotdSet { get; init; } = -1;
        public YearOfTheDragonSpyroModel.Animation[] YotdAnimations { get; init; } = [];
        /// <summary>The creature's own sound slots it keeps, in package order (0 knocked flying, 1 jumped on, then
        /// those its animations play).</summary>
        public int[] YotdSoundSlots { get; init; } = [];
        public bool FromYearOfTheDragon => YotdSet >= 0;
    }

    // Class IDs: free like the Crash boxes' (no retail level registers, places or dispatches them, no executable or
    // overlay instruction uses them as an immediate); Spyro.Editor.CrashEnemySmoke proves this against the disc.
    public static readonly Enemy Frog = new("crash-frog", "Crash frog", ToadVillageFile, ToadVillageSha256,
        new(0x165, "FrobG",
        [
            new("FrobV", true, [(0, 2), (15, 3)]),      // 0 hop: croak as it crouches, thud as it lands
            new("Fd1bV", true),                          // 1 knocked flying
            new("Fd2bV", true),                          // 2 sitting, throat pulsing
        ]), ["fli0A", "bnc0A", "FCrbA", "FLGbA"], 0xFF, 0.6, new(700, 560, 1230, 4500, 1600, 28, 560, 0, 0));

    public static readonly Enemy Knight = new("crash-knight", "Crash knight", ToadVillageFile, ToadVillageSha256,
        new(0x16E, "Kn10G",
        [
            new("Kn10V", true),                          // 0 guard
            new("Kn40V", true, [(0, 3), (4, 3)]),        // 1 march, footsteps
            new("Kn20V", true, [(3, 2)]),                // 2 overhead swing
        ]), ["fli0A", "bnc0A", "KSSbA", "KFSbA"], 0x54, 0.8, new(620, 1770, 2330, 5200, 1600, 18, 450, 1850, 2200));

    public static readonly Enemy Wizard = new("crash-wizard", "Crash wizard", GeeWizFile, GeeWizSha256,
        new(0x16F, "WA1fG",
        [
            new("WA0fV", true),                          // 0 standing
            new("WA3fV", true),                          // 1 pointing
            new("WA4fV", true, [(0, 2), (12, 3)]),       // 2 spell: wind-up, then the zap as the staff comes down
        ]), ["fli0A", "bnc0A", "WA1fA", "WA2fA", "WA5fA"], 0x55, 0.8, new(600, 2130, 2690, 6000, 0, 0, 420, 3400, 3800),
        new(0x171, "WA2fG",
        [
            new("Wa5fV", true, [(0, 4)]),                // 0 robe blown off
            new("Wa1fV", true),                          // 1 standing in his underwear
        ]));

    // Lost Fleet's creatures (Spyro: Year of the Dragon), from the user's own disc, with their own sounds. Year of the
    // Dragon's world and model units are Spyro the Dragon's. Their attacks are stretched (each frame twice) so the hit
    // lands on the frames the knight behaviour reads (6..9): the rhynoc's belly flop, the crab's claw slam.
    public const string YotdFile = "Lost Fleet (WAD 137, models)";
    /// <summary>Catalogue and asset source of Lost Fleet's creatures (the user's Spyro: Year of the Dragon disc).</summary>
    public const string YotdSourceKey = "yearofthedragon";
    public static readonly Enemy Rhynoc = new("yotd-rhynoc", "Lost Fleet rhynoc", YotdFile, YearOfTheDragonModels.LostFleetModelsSha256,
        new(0x175, "rhynoc", []), [], 0x54, 1.0, new(690, 740, 1300, 5000, 1600, 26, 450, 1100, 1300))
    {
        Kind = 1, YotdSet = 0x7BB5C,
        YotdAnimations = [new(0, true), new(1, true), new(2, false, 1, 2)],   // breathing, run cycle, belly flop
        // Roar (knocked flying), grunt (jumped on), then the run's huff and footsteps and the belly flop's landing.
        YotdSoundSlots = [0, 1, 3, 5, 6],
    };
    public static readonly Enemy Crab = new("yotd-crab", "Lost Fleet crab", YotdFile, YearOfTheDragonModels.LostFleetModelsSha256,
        new(0x1A2, "crab", []), [], 0x55, 1.0, new(800, 1180, 1740, 4000, 1000, 10, 500, 1000, 1200))
    {
        Kind = 1, YotdSet = 0x866FC,
        YotdAnimations = [new(0, true), new(1, true), new(2, false, 1, 2)],   // still, claws swaying, claw slam
        // Breaking apart (knocked flying), the claw slam's crash (jumped on, and on the slam). Its claw click is left out:
        // in Lost Fleet it ticks on the still pose, which this crab plays while guarding.
        YotdSoundSlots = [0, 2],
    };

    public static IReadOnlyList<Enemy> Enemies { get; } = [Frog, Knight, Wizard, Rhynoc, Crab];
    public static IReadOnlyList<Enemy> CrashEnemies => [.. Enemies.Where(e => !e.FromYearOfTheDragon)];
    public static IReadOnlyList<Enemy> YotdCreatures => [.. Enemies.Where(e => e.FromYearOfTheDragon)];
    public static bool IsYotdId(string? id) => id != null && YotdCreatures.Any(e => e.Id == id);
    public static bool IsCrashId(string? id) => id != null && CrashEnemies.Any(e => e.Id == id);

    public static bool IsId(string? id) => id != null && Enemies.Any(e => e.Id == id);
    /// <summary>Every class the enemies use, companions included.</summary>
    public static bool IsClass(int classId) => AllClasses.Contains(classId);
    public static bool IsPlaceableClass(int classId) => Enemies.Any(e => e.Class == classId);
    public static string? IdForClass(int classId) => Enemies.FirstOrDefault(e => e.Class == classId)?.Id;
    public static string? NameForClass(int classId) => Enemies.FirstOrDefault(e => e.Class == classId)?.Name;
    public static IReadOnlyList<int> AllClasses => [.. Enemies.SelectMany(e => e.Companion == null ? [e.Class] : new[] { e.Class, e.Companion.Class })];
    public static Enemy? ForClass(int classId) => Enemies.FirstOrDefault(e => e.Class == classId || e.Companion?.Class == classId);

    /// <summary>Treasure an enemy adds to its level's total (the gem it drops).</summary>
    public static int TreasureValue(int classId) =>
        Enemies.FirstOrDefault(e => e.Class == classId) is { } e && GemValue.TryFromIdByte(e.Reward, out var gem) ? gem.Value : 0;

    private sealed record Cache(string DiscPath, IReadOnlyList<UniversalSpecialAsset> Assets);
    private static readonly ConditionalWeakTable<UniversalSourceLibrary, Cache> Cached = new();

    public static IReadOnlyList<UniversalSpecialAsset> Assets(UniversalSourceLibrary library, CrashWarpedDisc disc)
    {
        if (Cached.TryGetValue(library, out var hit) && hit.DiscPath == disc.ImagePath) return hit.Assets;
        var dark = library.Get("darkhollow").Data;
        int basketRow = Enumerable.Range(0, dark.Scene().RowCount).First(i => BinaryPrimitives.ReadUInt16LittleEndian(dark.Row(i).AsSpan(0x36)) == UniversalCrashBoxes.BasketClass);
        byte[] row = dark.Row(basketRow);
        List<UniversalSpecialAsset> assets = [];
        foreach (var file in CrashEnemies.GroupBy(e => (e.File, e.Sha256)))
        {
            // One texture image per Crash level file: art the enemies share is stored (and imported) once.
            var nsf = disc.Level(file.Key.File, file.Key.Sha256);
            var art = new CrashSpyroModel.Art(nsf);
            foreach (var e in file)
            {
                var sounds = UniversalCrashBoxes.SoundSource(dark, [.. e.Sounds.Select(nsf.Sound)]);
                byte[] slots = [.. Enumerable.Range(0, e.Sounds.Length).Select(i => (byte)i)];
                UniversalSpecialAsset Asset(string id, string name, Model m)
                {
                    var built = CrashSpyroModel.Build(nsf, m.Name, m.Animations, new(CrashUnitsPerSpyroUnit / e.Size), slots, art);
                    byte[] template = row.ToArray();
                    BinaryPrimitives.WriteUInt16LittleEndian(template.AsSpan(0x36), (ushort)m.Class);
                    template[0x53] = e.Reward;
                    // Animation 0 from its first frame at its own speed (the game's animation step reads +0x3C..+0x43).
                    template.AsSpan(0x3C, 5).Clear();
                    template[0x41] = built.Package[BinaryPrimitives.ReadInt32LittleEndian(built.Package.AsSpan(0x38)) + 0xC];
                    return new(id, name, m.Class, UniversalSpecialBehavior.CrashEnemy, built.Package, art.Vram, sounds, template) { SourceKey = SourceKey };
                }
                var main = Asset(e.Id, e.Name, e.Main);
                assets.Add(e.Companion == null ? main : main with { Companions = [Asset(e.Id + "-hit", e.Name + " (hit)", e.Companion)] });
            }
        }
        Cached.AddOrUpdate(library, new(disc.ImagePath, assets));
        return assets;
    }

    private static readonly ConditionalWeakTable<UniversalSourceLibrary, Cache> CachedYotd = new();

    /// <summary>Lost Fleet's creatures, converted from the user's Spyro: Year of the Dragon disc (one shared texture
    /// image), with Dark Hollow's basket row as their template like the Crash enemies.</summary>
    public static IReadOnlyList<UniversalSpecialAsset> YotdAssets(UniversalSourceLibrary library, Spyro.Editor.Core.Music.YearOfTheDragonDisc disc)
    {
        if (CachedYotd.TryGetValue(library, out var hit) && hit.DiscPath == disc.ImagePath) return hit.Assets;
        var dark = library.Get("darkhollow").Data;
        int basketRow = Enumerable.Range(0, dark.Scene().RowCount).First(i => BinaryPrimitives.ReadUInt16LittleEndian(dark.Row(i).AsSpan(0x36)) == UniversalCrashBoxes.BasketClass);
        byte[] row = dark.Row(basketRow);
        var level = YearOfTheDragonModels.LostFleet(disc);
        var art = new YearOfTheDragonSpyroModel.Art(level);
        List<UniversalSpecialAsset> assets = [];
        foreach (var e in YotdCreatures)
        {
            var ids = level.SoundSlots(e.YotdSet);
            var sounds = e.YotdSoundSlots.Select(k => level.Sound(ids[k])).ToArray();
            var source = sounds.Length == 0 ? dark : UniversalCrashBoxes.SoundSource(dark, [.. sounds.Select(x => x.Adpcm)], [.. sounds.Select(x => x.Pitch)]);
            var built = YearOfTheDragonSpyroModel.Build(level, e.YotdSet, e.Main.Name, e.YotdAnimations, e.Size, art: art, soundSlots: e.YotdSoundSlots);
            byte[] template = row.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(template.AsSpan(0x36), (ushort)e.Class);
            template[0x53] = e.Reward;
            template.AsSpan(0x3C, 5).Clear();
            template[0x41] = built.Package[BinaryPrimitives.ReadInt32LittleEndian(built.Package.AsSpan(0x38)) + 0xC];
            assets.Add(new(e.Id, e.Name, e.Class, UniversalSpecialBehavior.CrashEnemy, built.Package, art.Vram, source, template) { SourceKey = YotdSourceKey });
        }
        CachedYotd.AddOrUpdate(library, new(disc.ImagePath, assets));
        return assets;
    }
}

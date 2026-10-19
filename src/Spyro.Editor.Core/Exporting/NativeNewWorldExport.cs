using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Sky;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Create BIN's New World: the project's world (<see cref="NewWorldDocument"/>) goes into a finished game image
/// through the level window (<see cref="NativeLevelWindow"/>).
/// <list type="bullet">
/// <item>Homeworld 70 is compiled as a rebuilt homeworld on <see cref="TemplateKey"/>'s slot (in scratch images only:
/// Artisans itself is untouched); its doorways to 71-74 are built on the template's doorways to 11-14
/// (<see cref="TemplateDestination"/>). Like 71-74 it runs on the fresh template's initializer (Town Square's case) with
/// its own copy buffer, init and update callbacks.</item>
/// <item>Levels 71-74 are compiled as File › New Level's level is and run on its initializer (Town Square's case).</item>
/// <item>Each level's overlay and data go to new WAD entries from <see cref="FirstEntry"/>; its fixed facts (name,
/// music, kill height, gem and dragon totals, initializer callbacks) to the window's records. Objects are composed per
/// level; the executable may change only where the window carries it (<see cref="RequireOnlyWindowChanges"/>).</item>
/// <item>The way in (<see cref="WorldEntranceDocument"/>, optional) is built into the game's homeworld the world is entered
/// from (<see cref="NativeWorldEntrance"/>, after every other edit of that homeworld); homeworld 70 then needs its doorway
/// back there, and that doorway needs the way in.</item>
/// <item>The draw-buffer guard bounds each level by its near distance; the window is installed last, then the card
/// menu's save overlay is patched, and the image is written with its readback checked.</item>
/// </list></summary>
public static class NativeNewWorldExport
{
    public const string TemplateKey = "artisans";
    public const int TemplateLevelId = 10, FirstEntry = 102;
    /// <summary>Normal arrival height (the retail arrival table's word for ground levels).</summary>
    private const int ArrivalHeight = 8192;
    private const uint Kill = 0x8006E9A4, GemTotals = 0x8006F438, DragonTotals = 0x8006F414;

    /// <summary>The template homeworld's doorway a world doorway is built on: 71-74 take Artisans' doorways to 11-14, the
    /// way back to a homeworld of the game Artisans' doorway to Sunny Flight (15).</summary>
    public static int TemplateDestination(int destination) => destination switch
    {
        >= 71 and <= 74 => destination - 60,
        10 or 20 or 30 or 40 or 50 or 60 => 15,
        _ => throw new InvalidDataException($"A doorway in your new world's homeworld leads to its levels (71-74) or back to the game's homeworld, not to level {destination}."),
    };

    /// <summary>One level of the world with its objects as Create BIN places them.</summary>
    public sealed record WorldLevel(SceneDocument Document, IReadOnlyList<UniversalMobyPlacement> Placements);

    /// <summary>What was built (for the status line and tests).</summary>
    public sealed record Result(IReadOnlyList<WindowGuest> Guests, IReadOnlyDictionary<int, int> NearDistances, bool AfterMovies,
        WorldEntranceResult? Entrance = null);

    /// <summary>Adds the world to the finished image at <paramref name="imagePath"/> (rewritten in place).</summary>
    public static async Task<Result> ApplyAsync(UniversalSourceLibrary donors, LevelCatalog catalog, NewWorldDocument world,
        IReadOnlyList<WorldLevel> levels, string imagePath, string scratch, Func<string, UniversalSpecialAsset?>? special,
        CancellationToken token = default, int? newGameStart = null, WorldEntranceDocument? entrance = null)
    {
        if (levels.Count == 0) throw new InvalidDataException("Your new world has no levels to build.");
        if (NativeLevelWindow.NameRefusal(world.Name) is { } worldWhy) throw new InvalidDataException("The world's name: " + worldWhy);
        if (levels.All(l => l.Document.WorldLevelId != NewWorldDocument.Homeworld))
            throw new InvalidDataException("Your new world has no homeworld to build.");
        foreach (var level in levels)
        {
            if (level.Document.WorldLevelId is not int id || id is < NativeLevelWindow.FirstId or > NativeLevelWindow.LastId)
                throw new InvalidDataException($"{level.Document.Name} is not a level of your new world.");
            if (NativeLevelWindow.NameRefusal(level.Document.Name) is { } why) throw new InvalidDataException($"{level.Document.Name}: {why}");
            if (MusicTrackCatalog.IsCustom(level.Document.MusicTrack))
                throw new InvalidDataException($"{level.Document.Name} plays a song from another game; levels of your new world play Spyro's own music.");
            // 71-74 refuse dragons as File › New Level's level does (their composition); the homeworld's own (its
            // template's) dragons are not proven in the window yet.
            if (level.Document.WorldLevelId == NewWorldDocument.Homeworld && UniversalDragonRows.FirstDragon(level.Placements, donors) != null)
                throw new InvalidDataException($"{level.Document.Name} has a dragon, but dragons can't be rescued in your new world's homeworld yet. Remove it.");
        }
        // The way in and the way back go together, between the world and the homeworld it is entered from.
        var entryHome = catalog.FindByKey(world.EntryHomeworldKey) ?? throw new InvalidDataException("The level catalog has no " + world.EntryHomeworldKey + ".");
        var homeDocument = levels.First(l => l.Document.WorldLevelId == NewWorldDocument.Homeworld).Document;
        var backs = (homeDocument.Portals ?? []).Where(p => !NewWorldDocument.LevelSlots.Contains(p.DestinationLevelId)).ToList();
        if (backs.Any(p => p.DestinationLevelId != entryHome.LevelId))
            throw new InvalidDataException($"{homeDocument.Name}'s doorway back leads to {catalog.Levels.FirstOrDefault(l => l.LevelId == backs[0].DestinationLevelId)?.DisplayName}, " +
                $"but your world is entered from {entryHome.DisplayName}. Point it at {entryHome.DisplayName}.");
        if (entrance != null && !string.Equals(entrance.HomeworldKey, entryHome.Key, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The way into your world stands in {catalog.FindByKey(entrance.HomeworldKey)?.DisplayName ?? entrance.HomeworldKey}, " +
                $"but your world is entered from {entryHome.DisplayName}. Place it in {entryHome.DisplayName}.");
        if (entrance != null && backs.Count == 0)
            throw new InvalidDataException($"{homeDocument.Name} needs a doorway back to {entryHome.DisplayName}, or Spyro can't leave your world: add one with Add Doorway in {homeDocument.Name}.");
        if (entrance == null && backs.Count > 0)
            throw new InvalidDataException($"{homeDocument.Name}'s doorway back to {entryHome.DisplayName} needs the way into your world there: place it in {entryHome.DisplayName} (World Entrance).");
        Directory.CreateDirectory(scratch);
        // The world's levels resolve like the game's (doorway names, sky and lettering read the catalog).
        var worldCatalog = new LevelCatalog([.. catalog.Levels, .. levels.Select(l => NewWorldDocument.Target(l.Document.WorldLevelId!.Value, l.Document.Name))]);
        byte[] original = donors.Disc.Exe;
        var template = catalog.FindByKey(TemplateKey) ?? throw new InvalidDataException("The level catalog has no Artisans.");
        // A doorway carries its destination's sky, always with a part (a sky without one hangs the flight, see NativeWorldEntrance).
        byte[]? SkyOf(int id) =>
            levels.FirstOrDefault(l => l.Document.WorldLevelId == id)?.Document is { } d ? NativeWorldEntrance.DoorwaySky(NativeWorldEntrance.SceneSky(donors.ImagePath, d))
            : id == entryHome.LevelId && LevelSkyResolver.Original(donors.ImagePath, entryHome.SourceWadEntry) is { } back ? NativeWorldEntrance.DoorwaySky(back) : null;

        Dictionary<int, byte[]> entries = [];
        List<(WorldLevel Level, int Template, int Music, int Kill, int Gems, int Dragons, IReadOnlyDictionary<uint, uint> Bindings)> built = [];
        Dictionary<int, int> near = [];
        bool saveMap = false;
        int entry = FirstEntry;
        foreach (var level in levels.OrderBy(l => l.Document.WorldLevelId))
        {
            token.ThrowIfCancellationRequested();
            var doc = level.Document; int id = doc.WorldLevelId!.Value;
            byte[] overlay, data, exe; int slot, templateId;
            if (id == NewWorldDocument.Homeworld)
            {
                // A rebuilt homeworld on the template's slot: its overlay brings the doorway code.
                int prefix = new UniversalLevelData(donors.Disc.EntryBytes(template.SourceWadEntry)).Scene().RowCount;
                var home = doc with { TargetBinding = new(template.Key, template.LevelId, template.SourceWadEntry, template.SourceWadEntry - 1,
                    UniversalSourceLibrary.OriginalUsaSha256, prefix) };
                var compiled = await NativeClearedLevelCompilation.CaptureAsync(donors.ImagePath, home, worldCatalog,
                    Path.Combine(scratch, $"world-{id}-host.bin"), token, SkyOf);
                byte[] hostExe = NativeClearedRetailBinding.Prepare(original, original.ToArray(), template);
                var result = compiled.NeedsComposition(level.Placements) ? compiled.ComposeWithExecutable(donors, level.Placements, hostExe, special) : null;
                if (result != null) saveMap |= RequireOnlyWindowChanges(doc.Name, hostExe, result.Executable, template.LevelId, 0);
                overlay = result?.Overlay ?? compiled.CopyOverlay(); data = result?.LevelData ?? compiled.CopyLevelData();
                // It runs on the fresh template's case: a compiled overlay's six library callbacks sit where Town Square's
                // do (Artisans' are elsewhere), so only its copy buffer, init and update differ (set by the window).
                exe = result?.Executable ?? hostExe; slot = 0; templateId = NativeLevelWindow.FreshTemplate;
                near[id] = compiled.NearDistance;
            }
            else
            {
                var compilation = await NativeFreshMainCompilation.CaptureAsync(donors.ImagePath, doc, Path.Combine(scratch, $"world-{id}-host.bin"), token);
                var composed = compilation.Compose(donors, level.Placements, special);
                saveMap |= RequireOnlyWindowChanges(doc.Name, compilation.CopyExecutable(), composed.Executable, NativeLevelWindow.FreshTemplate, NativeLevelWindow.WindowIndex);
                overlay = composed.Overlay; data = composed.LevelData; exe = composed.Executable;
                slot = NativeLevelWindow.WindowIndex; templateId = NativeLevelWindow.FreshTemplate;
                near[id] = compilation.NearDistance;
            }
            // The music table holds the track number itself (LevelMusicPatchExporter's level-track word).
            int music = doc.MusicTrack;
            int originalKill = (int)Word(original, Kill + 4 * (uint)(id == NewWorldDocument.Homeworld ? 0 : NativeLevelWindow.WindowIndex));
            int kill = NativeKillHeights.For(id == NewWorldDocument.Homeworld ? originalKill : 1024, NativeKillHeights.Lowest(doc));
            int gems = BinaryPrimitives.ReadUInt16LittleEndian(exe.AsSpan(Off(GemTotals + 2 * (uint)slot)));
            int dragons = exe[Off(DragonTotals + (uint)slot)];
            var bindings = UniversalExeBindings.Read(exe, id == NewWorldDocument.Homeworld ? template.LevelId : NativeLevelWindow.FreshTemplate)
                .ToDictionary(p => p.Key, p => p.Value.Value);
            entries[entry] = overlay; entries[entry + 1] = data;
            built.Add((level, templateId, music, kill, gems, dragons, bindings));
            entry += 2;
        }

        // The finished image: its executable already carries every other edit; the window goes in last.
        var image = UniversalDiscImage.Read(imagePath);
        byte[] final = image.Exe.ToArray();
        // The way in, built into the entry homeworld as the finished game has it (its WAD rows rebuilt to the new length).
        byte[] wad = image.Wad;
        Dictionary<int, byte[]> rebuilt = [];
        WorldEntranceResult? entranceResult = null;
        if (entrance != null)
        {
            entranceResult = NativeWorldEntrance.Apply(donors, catalog, entrance.Spec(), image.EntryBytes(entryHome.SourceWadEntry),
                image.EntryBytes(entryHome.SourceWadEntry - 1), final, NativeWorldEntrance.SceneSky(donors.ImagePath, homeDocument));
            rebuilt[entryHome.SourceWadEntry] = entranceResult.LevelData; rebuilt[entryHome.SourceWadEntry - 1] = entranceResult.Overlay;
            wad = image.RebuildWad(rebuilt);
        }
        var placement = NativeLevelWindowDisc.Place(image, wad, entries, final.Length);
        NativeDrawBufferGuard.PatchExe(final, near);
        // A level whose objects keep their saved identities in other rows carries a save map: its game-wide hooks.
        if (saveMap) NativeMobySaveMap.PatchExe(final);
        int at = FirstEntry;
        var guests = built.Select(b =>
        {
            var g = new WindowGuest(b.Level.Document.WorldLevelId!.Value, b.Level.Document.Name, b.Template, b.Music, b.Kill, ArrivalHeight,
                b.Gems, b.Dragons, at, at + 1, b.Bindings);
            at += 2;
            return g;
        }).ToArray();
        NativeLevelWindow.Install(final, placement.Table, guests, world.Name);
        if (newGameStart is int start)
        {
            // New Game starts in a level of the world (USA 0x80012780 and 0x8002D4F0: addiu v0,zero,<level id>).
            if (guests.All(g => g.LevelId != start)) throw new InvalidDataException($"New Game can only start in a level of the world, and level {start} is not one.");
            foreach (uint address in new uint[] { 0x80012780, 0x8002D4F0 })
            {
                if (Word(final, address) >> 16 != 0x2402) throw new InvalidDataException("The executable's New Game start differs from the USA game's.");
                BinaryPrimitives.WriteUInt32LittleEndian(final.AsSpan(Off(address)), 0x24020000u | (uint)start);
            }
        }
        NativeLevelWindow.PatchSaveOverlayInWad(placement.Wad);
        string pending = imagePath + ".world-" + Guid.NewGuid().ToString("N");
        try
        {
            NativeLevelWindowDisc.Write(image, pending, placement, final);
            foreach (var (e, bytes) in entries.Concat(rebuilt))
                if (!NativeLevelWindowDisc.ReadEntry(pending, e).AsSpan().SequenceEqual(bytes))
                    throw new InvalidDataException($"Your new world's WAD entry {e} failed readback.");
            if (!UniversalDiscImage.Read(pending).Exe.AsSpan().SequenceEqual(final))
                throw new InvalidDataException("The executable with your new world failed readback.");
            File.Move(pending, imagePath, overwrite: true);
        }
        finally { if (File.Exists(pending)) File.Delete(pending); }
        return new Result(guests, near, placement.AfterMovies, entranceResult);
    }

    /// <summary>Composing a level's objects may change the executable only where the window carries the change into the
    /// finished game: the initializer callbacks of the slot it was compiled on, that slot's gem and dragon totals, and the
    /// save map's game-wide hooks (<see cref="NativeMobySaveMap"/>; true: the level needs them). Any other change (a feature
    /// that patches the game's code for that level) would be lost, so it is refused.</summary>
    internal static bool RequireOnlyWindowChanges(string level, byte[] before, byte[] after, int compiledLevelId, int slot)
    {
        if (before.Length != after.Length) throw new InvalidDataException($"{level}: composing its objects changed the executable's size.");
        var allowed = new HashSet<int>();
        void Allow(uint address, int length) { for (int i = 0; i < length; i++) allowed.Add(Off(address) + i); }
        foreach (var b in UniversalExeBindings.Read(after, compiledLevelId).Values)
        {
            if (b.LuiAddress != 0) Allow(b.LuiAddress, 4);
            if (b.AddiuAddress != 0) Allow(b.AddiuAddress, 4);
        }
        Allow(GemTotals + 2 * (uint)slot, 2);
        Allow(DragonTotals + (uint)slot, 1);
        var windows = NativeMobySaveMap.Windows();
        bool saveMap = windows.All(w => after.AsSpan(Off(w.Address), w.Code.Length).SequenceEqual(w.Code)) &&
            windows.Any(w => !before.AsSpan(Off(w.Address), w.Code.Length).SequenceEqual(w.Code));
        if (saveMap) foreach (var w in windows) Allow(w.Address, w.Code.Length);
        List<string> changes = [];
        for (int at = 0; at < before.Length;)
        {
            if (before[at] == after[at] || allowed.Contains(at)) { at++; continue; }
            int start = at;
            while (at < before.Length && before[at] != after[at] && !allowed.Contains(at)) at++;
            changes.Add($"0x{start + UniversalExeBindings.ExeBase:X8} ({at - start} bytes)");
        }
        if (changes.Count > 0)
            throw new InvalidDataException($"{level} has objects that need the game's code changed for that level, which a level of your new " +
                $"world cannot have yet (executable {string.Join(", ", changes.Take(8))}{(changes.Count > 8 ? ", …" : "")}).");
        return saveMap;
    }

    private static int Off(uint address) => (int)(address - UniversalExeBindings.ExeBase);
    private static uint Word(byte[] exe, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(Off(address)));
}

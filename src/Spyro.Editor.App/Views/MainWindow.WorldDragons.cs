using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

/// <summary>Dragons in a level of the new world. Such a level has no place in the game and so no dragons of its own:
/// it takes the dragons of one level of the game as guests (<see cref="UniversalDragonGuests"/>: their rescue scenes,
/// their level's code). One level's, because one level's code frees them all; six at most, because a level's file has
/// room for six rescue scenes. Create BIN counts them (<see cref="NativeDragonTotals"/>).</summary>
public sealed partial class MainWindow
{
    /// <summary>The source level and class of an object brought in from a level of the game (class -1: not one).</summary>
    private static (string Key, int Class) DragonSourceOf(Moby moby, UniversalSourceLibrary lib)
    {
        if (!moby.IsAdded || moby.IsRemoved || string.IsNullOrEmpty(moby.CrossLevelSourceLevelKey) || moby.CrossLevelSourceTrueIndex < 0) return ("", -1);
        return (moby.CrossLevelSourceLevelKey, LevelCompletionCheck.SourceClass(lib)(moby.CrossLevelSourceLevelKey, moby.CrossLevelSourceTrueIndex));
    }

    /// <summary>The levels of the game whose dragon statues stand in the open level, with how many each.</summary>
    private Dictionary<string, int> DragonSourcesHere(UniversalSourceLibrary lib)
    {
        Dictionary<string, int> sources = new(StringComparer.OrdinalIgnoreCase);
        foreach (var m in _currentMobys)
            if (DragonSourceOf(m, lib) is { Class: LevelCompletionCheck.DragonStatueClass } statue)
                sources[statue.Key] = sources.GetValueOrDefault(statue.Key) + 1;
        return sources;
    }

    /// <summary>True in a level where dragons stand as guests: a level of the new world (its homeworld too).</summary>
    private bool IsDragonGuestLevel => _freshSceneDocument is { WorldLevelId: not null };

    /// <summary>Null, or why the dragon <paramref name="variant"/> can't be added to the open level of the new world: the
    /// level already has dragons of another level of the game, or six of them.</summary>
    private string? WorldDragonAddRefusal(UniversalSourceLibrary lib, UniversalMobyVariant variant)
    {
        if (!IsDragonGuestLevel || _currentLevel == null || UniversalSpecialCatalog.IsSpecial(variant)) return null;
        int c = LevelCompletionCheck.SourceClass(lib)(variant.SourceLevelKey, variant.SourceRow);
        if (c != UniversalDragonRows.Statue && c != UniversalDragonRows.Dragon && !UniversalDragonRows.IsPedestal(c)) return null;
        var here = DragonSourcesHere(lib);
        string Name(string key) => _retailCatalog.FindByKey(key)?.DisplayName ?? key;
        string level = _freshSceneDocument!.Name;
        if (here.Keys.FirstOrDefault(k => !string.Equals(k, variant.SourceLevelKey, StringComparison.OrdinalIgnoreCase)) is { } other)
            return $"{level} already has {(here[other] == 1 ? "a dragon" : "dragons")} from {Name(other)}, and a level's dragons all come from one level of the game " +
                   $"(each level frees its dragons with its own code, and a level can run one level's). Add another of {WorldAccess.Possessive(Name(other))} dragons here, or put this one in another level of your world.";
        if (here.Values.Sum() >= UniversalDragonGuests.MaxStatues)
            return $"{level} already has {UniversalDragonGuests.MaxStatues} dragons, and a level can hold no more: each dragon's rescue scene is a part of the level's file, which has room for {UniversalDragonGuests.MaxStatues}.";
        return null;
    }

    /// <summary>The level of the game whose dragons Random objects places in the open level of the new world (null in any
    /// other level, which has its place's own): the level whose dragons already stand here, else the homeworld takes
    /// Artisans' (it is built on Artisans' place) and a level those of the level its objects are chosen from.</summary>
    private string? WorldDragonLevel(string chosenLevelKey, UniversalSourceLibrary lib)
    {
        if (!IsDragonGuestLevel) return null;
        if (DragonSourcesHere(lib).Keys.FirstOrDefault() is { } present) return present;
        return _freshSceneDocument!.WorldLevelId == NewWorldDocument.Homeworld ? NativeNewWorldExport.TemplateKey : chosenLevelKey;
    }

    /// <summary>That level's dragons (statue rows) not yet in the open level, the ones whose rescue scenes need the least
    /// game memory first, as many as the level has room for.</summary>
    private List<int> WorldDragonRows(UniversalSourceLibrary lib, string key)
    {
        UniversalLevelData data;
        try { data = lib.Get(key).Data; } catch (InvalidDataException) { return []; }
        var here = DragonSourcesHere(lib);
        if (here.Keys.Any(k => !string.Equals(k, key, StringComparison.OrdinalIgnoreCase))) return [];
        var placed = _currentMobys.Select(m => DragonSourceOf(m, lib) is { Class: LevelCompletionCheck.DragonStatueClass } s && string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase) ? m.CrossLevelSourceTrueIndex : -1).ToHashSet();
        return [.. LevelCompletionCheck.OriginalRows(data).Dragons.Where(r => !placed.Contains(r))
            .OrderBy(r => UniversalDragonGuests.SceneRoom(data, key, r)).ThenBy(r => r).Take(Math.Max(0, UniversalDragonGuests.MaxStatues - here.Values.Sum()))];
    }
}

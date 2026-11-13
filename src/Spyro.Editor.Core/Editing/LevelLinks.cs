using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Editing;

/// <summary>A level as File ▾ › Level Links… names it: <see cref="Name"/> in sentences ("Stone Hill", "SUNNY GLEN"),
/// <see cref="Label"/> in lists ("SUNNY GLEN (built in Stone Hill's place)"), <see cref="SlotName"/> the game's level whose
/// place it has.</summary>
public sealed record LevelLinkPlace(int LevelId, string Name, string Label, string SlotName, bool Built, bool IsNewLevel)
{
    public bool IsHomeworld => LevelId % 10 == 0;
    public override string ToString() => Label;
}

/// <summary>One doorway of a homeworld: which of the game's doorways it is (<see cref="Portal"/>, named by where it leads in
/// the game: <see cref="DoorwayName"/>) and where it leads in this project (<see cref="LeadsTo"/>). A doorway of a homeworld
/// rebuilt in the project is not <see cref="Routable"/>: it always leads to the level in its original place.</summary>
public sealed record LevelLinkDoorway(PortalEndpoint Portal, string HomeworldKey, string HomeworldName, int OriginalLevelId,
    string DoorwayName, int LeadsTo, bool Routable)
{
    public bool Changed => LeadsTo != OriginalLevelId;
}

/// <summary>A homeworld and its doorways. <see cref="RebuiltAs"/>: the name of the level built in its place (its doorways are
/// that level's own, made with Terrain › Doorways).</summary>
public sealed record LevelLinkHomeworld(string Key, int LevelId, string Name, string? RebuiltAs, IReadOnlyList<LevelLinkDoorway> Doorways)
{
    public bool Rebuilt => RebuiltAs != null;
}

/// <summary>Whether a level has a Return Home vortex, as far as the project's saved files tell: <see cref="Present"/> null when
/// not known; <see cref="Expected"/> when the game's own level has one (a level built in its place should too).</summary>
public sealed record ReturnHomeVortex(bool? Present, bool Expected)
{
    public static ReturnHomeVortex Unknown { get; } = new(null, false);
}

/// <summary>Where Return Home (the level's vortex, or Exit Level in the pause menu) brings Spyro back out: the saved choice
/// (<see cref="Saved"/>, null: none saved), the doorway the game uses (<see cref="Effective"/>: the saved one, or else the
/// level's own doorway of the game), and the doorway "automatic" stands for (<see cref="Automatic"/>: the doorway that leads
/// to the level, its own first; null when none does). <see cref="LeadingHere"/>: every doorway that leads to it.</summary>
public sealed record LevelLinkReturn(LevelLinkPlace Level, PortalEndpoint? Saved, PortalEndpoint? Effective, PortalEndpoint? Automatic,
    IReadOnlyList<LevelLinkDoorway> LeadingHere, ReturnHomeVortex Vortex)
{
    public bool IsAutomatic => Effective == Automatic || Automatic == null && Saved == null;
}

/// <summary>One level of the project's own world and whether its homeworld has a doorway to it.</summary>
public sealed record LevelLinkWorldLevel(int LevelId, string Name, bool HasDoorway, ReturnHomeVortex Vortex);

/// <summary>The project's own world (File ▾ › New World…): how players get there and back (<see cref="Access"/>, one
/// sentence, <see cref="WorldAccess.Describe"/>), its homeworld's doorways, its balloonist, any doorway in or out and its
/// place on the balloon route (<see cref="Place"/>: as Create BIN builds it; <see cref="Balloon"/>: as chosen, null when
/// none was). Its links are set by the world's own tools, so the map only reports them.</summary>
public sealed record LevelLinkWorld(string Name, string HomeworldName, string EntryHomeworldKey, string EntryHomeworldName,
    string? EntranceHomeworldKey, IReadOnlyList<LevelLinkWorldLevel> Levels, int? WayBackTo, WorldBalloonPlace? Balloon)
{
    public string Access { get; init; } = "";
    /// <summary>Reached by balloon only (no doorway in, none back out).</summary>
    public bool BalloonOnly { get; init; } = true;
    public bool HasBalloonist { get; init; }
    public WorldBalloonPlace Place { get; init; } = WorldBalloonPlace.Default;
}

/// <summary>Something about the links worth saying, in plain words. <see cref="StopsCreateBin"/>: Create BIN cannot make the
/// game as it is. <see cref="Fix"/> (with <see cref="FixLabel"/>): a change to the saved links that puts it right.</summary>
public sealed record LevelLinkProblem(string Text, bool StopsCreateBin, IReadOnlyList<int> Levels,
    string? FixLabel = null, Func<PortalRouteDocument, PortalRouteDocument>? Fix = null);

public sealed record LevelLinksMap(IReadOnlyList<LevelLinkHomeworld> Homeworlds, IReadOnlyList<LevelLinkPlace> Destinations,
    IReadOnlyList<LevelLinkReturn> Returns, LevelLinkWorld? World, IReadOnlyList<LevelLinkProblem> Problems)
{
    public IEnumerable<LevelLinkDoorway> Doorways => Homeworlds.SelectMany(h => h.Doorways);
    public LevelLinkDoorway? Doorway(PortalEndpoint portal) => Doorways.FirstOrDefault(d => d.Portal == portal);
    public LevelLinkReturn? ReturnOf(int levelId) => Returns.FirstOrDefault(r => r.Level.LevelId == levelId);
    public LevelLinkPlace? Place(int levelId) => Destinations.FirstOrDefault(d => d.LevelId == levelId);
}

/// <summary>File ▾ › Level Links…: the whole project's doorways and Return Home in one place. Built on the saved portal
/// routes (<see cref="PortalRouteDocument"/>, the model Create BIN puts in the game) and on the levels the project builds
/// (<see cref="SceneDocument"/>s, the new world). <see cref="Map"/> says how every level is reached and left;
/// <see cref="Send"/> and <see cref="SetReturn"/> make the route changes the window offers, keeping the way back with the
/// way in: a doorway sent to a level brings Spyro back out of itself, and the level it led to before comes back out of
/// the doorway that still leads there.</summary>
public sealed class LevelLinks
{
    /// <summary>The game's homeworlds in their order (the keys <see cref="HomeworldPortalControlCatalog"/> uses).</summary>
    public static IReadOnlyList<string> HomeworldKeys => NewWorldDocument.HomeworldKeys;

    private readonly LevelCatalog _catalog;
    private readonly Dictionary<string, SceneDocument> _built = new(StringComparer.OrdinalIgnoreCase);
    private readonly SceneDocument? _newLevel;
    private readonly NewWorldDocument? _world;
    private readonly IReadOnlyList<SceneDocument> _worldScenes;
    private readonly WorldEntranceDocument? _entrance;
    private readonly Func<int, ReturnHomeVortex> _vortex;

    /// <param name="scenes">The levels the project builds in the game's places, and its new level (as
    /// <see cref="SceneDocumentStore.ReadAll"/> reads them).</param>
    /// <param name="vortex">Whether a level (by id) has its Return Home vortex; null: not known for any.</param>
    public LevelLinks(LevelCatalog catalog, IReadOnlyList<SceneDocument> scenes, NewWorldDocument? world = null,
        IReadOnlyList<SceneDocument>? worldScenes = null, WorldEntranceDocument? entrance = null, Func<int, ReturnHomeVortex>? vortex = null)
    {
        _catalog = catalog;
        foreach (var scene in scenes)
        {
            if (scene.WorldLevelId != null) continue;
            if (scene.TargetBinding?.LevelKey is { } key) _built[LevelCatalog.NormalizeKey(key)] = scene;
            else _newLevel = scene;
        }
        _world = world;
        _worldScenes = worldScenes ?? [];
        _entrance = entrance;
        _vortex = vortex ?? (_ => ReturnHomeVortex.Unknown);
    }

    /// <summary>The project has File ▾ › New Level's level (id 65), so doorways may lead to it.</summary>
    public bool HasNewLevel => _newLevel != null;

    private SceneDocument? BuiltIn(string key) => _built.GetValueOrDefault(LevelCatalog.NormalizeKey(key));

    /// <summary>The game's doorway that leads to <paramref name="levelId"/> (null: none, as for the new level).</summary>
    public static PortalEndpoint? OriginalDoorway(int levelId) =>
        HomeworldPortalControlCatalog.All.FirstOrDefault(p => p.DestinationLevelId == levelId) is { } d ? new(d.HomeworldKey, d.PathTrueIndex) : null;

    /// <summary>A level that is left by Return Home: the game's levels other than the homeworlds, and the new level.</summary>
    public static bool HasReturnHome(int levelId) => PortalRouteDocument.IsLevelId(levelId) && levelId % 10 != 0;

    /// <summary>A level's name and list label (rebuilt levels by the name they were given).</summary>
    public LevelLinkPlace PlaceOf(int levelId)
    {
        if (levelId == 65)
            return _newLevel is { } own
                ? new(65, own.Name, $"{own.Name} (your new level)", "the new level", true, true)
                : new(65, "the new level", "The new level (not made yet)", "the new level", false, true);
        var level = _catalog.Levels.FirstOrDefault(l => l.LevelId == levelId);
        string slot = level?.DisplayName ?? HomeworldPortalControlCatalog.All.FirstOrDefault(p => p.DestinationLevelId == levelId)?.DestinationName ?? $"level {levelId}";
        if (level != null && BuiltIn(level.Key) is { } built)
        {
            bool same = string.Equals(built.Name.Trim(), slot, StringComparison.OrdinalIgnoreCase);
            return new(levelId, built.Name, same ? $"{slot} (rebuilt)" : $"{built.Name} (built in {Possessive(slot)} place)", slot, true, false);
        }
        return new(levelId, slot, levelId % 10 == 0 ? $"{slot} (a homeworld)" : slot, slot, false, false);
    }

    /// <summary>Every doorway of the game's six homeworlds and where it leads with <paramref name="routes"/>: a homeworld
    /// rebuilt in the project has the doorways its own level was given (each to the level of its original place).</summary>
    public IReadOnlyList<LevelLinkHomeworld> Homeworlds(PortalRouteDocument routes)
    {
        List<LevelLinkHomeworld> homes = [];
        foreach (string key in HomeworldKeys)
        {
            var level = _catalog.FindByKey(key);
            string name = level?.DisplayName ?? key;
            var built = BuiltIn(key);
            var definitions = HomeworldPortalControlCatalog.ForLevel(key);
            List<LevelLinkDoorway> doorways = [];
            if (built != null)
            {
                foreach (var portal in (built.Portals ?? []).OrderBy(p => p.DestinationLevelId))
                    if (definitions.FirstOrDefault(d => d.DestinationLevelId == portal.DestinationLevelId) is { } d)
                        doorways.Add(new(new(key, d.PathTrueIndex), key, name, d.DestinationLevelId, d.DestinationName + " doorway", d.DestinationLevelId, false));
            }
            else
                foreach (var d in definitions)
                {
                    PortalEndpoint endpoint = new(key, d.PathTrueIndex);
                    doorways.Add(new(endpoint, key, name, d.DestinationLevelId, d.DestinationName + " doorway", routes.DestinationFor(endpoint), true));
                }
            homes.Add(new(key, level?.LevelId ?? (homes.Count + 1) * 10, name, built?.Name, doorways));
        }
        return homes;
    }

    /// <summary>The doorway "automatic" stands for: of the doorways that lead to <paramref name="levelId"/>, the level's own
    /// doorway of the game (Return Home through it needs no saved choice), else the first that can be routed; null when
    /// no doorway leads there.</summary>
    private static PortalEndpoint? AutomaticFor(IReadOnlyList<LevelLinkHomeworld> homes, int levelId)
    {
        var leading = homes.SelectMany(h => h.Doorways).Where(d => d.LeadsTo == levelId).ToList();
        var own = OriginalDoorway(levelId);
        if (own != null && leading.Any(d => d.Portal == own)) return own;
        return leading.FirstOrDefault(d => d.Routable)?.Portal;
    }

    private static void RequireRoutable(PortalEndpoint portal, IReadOnlyList<LevelLinkHomeworld> homes)
    {
        var definition = portal.Resolve();
        var home = homes.First(h => h.Key == definition.HomeworldKey);
        if (home.Rebuilt)
            throw new InvalidDataException($"{home.Name} is rebuilt in your project ({home.RebuiltAs}): its doorways always lead to the levels in their own places. " +
                $"Change them with Terrain › Doorways in {home.RebuiltAs}.");
    }

    /// <summary>The levels a doorway may be sent to: the game's levels (as the project has them) and the new level when the
    /// project has one. Homeworlds are not offered (Spyro arriving in a homeworld through a doorway was never tried in the game).</summary>
    public IReadOnlyList<LevelLinkPlace> DestinationChoices() =>
        [.. _catalog.Levels.Where(l => HasReturnHome(l.LevelId)).Select(l => l.LevelId).Concat(HasNewLevel ? [65] : []).Distinct().OrderBy(id => id).Select(PlaceOf)];

    /// <summary>The routes with <paramref name="portal"/> sent to <paramref name="levelId"/>. Return Home from that level then
    /// brings Spyro back out of this doorway; the level it led to before, if it came back out of it, now comes back out of the
    /// doorway that leads there (none: the game's own one).</summary>
    public PortalRouteDocument Send(PortalRouteDocument routes, PortalEndpoint portal, int levelId)
    {
        var homes = Homeworlds(routes);
        RequireRoutable(portal, homes);
        if (!PortalRouteDocument.IsLevelId(levelId)) throw new InvalidDataException("Choose a level for the doorway.");
        if (levelId == 65 && !HasNewLevel) throw new InvalidDataException("The project has no new level yet: make one with File ▾ › New Level… first.");
        int before = routes.DestinationFor(portal);
        if (before == levelId) return routes;
        var definition = portal.Resolve();
        var next = levelId == definition.DestinationLevelId ? routes.WithOriginalDestination(portal) : routes.WithDestination(portal, levelId, false);
        if (HasReturnHome(levelId)) next = next.WithReturn(levelId, portal == OriginalDoorway(levelId) ? null : portal);
        if (HasReturnHome(before) && (next.Returns.FirstOrDefault(r => r.LevelId == before)?.Portal ?? OriginalDoorway(before)) == portal)
            next = Automatic(next, before);
        return next.Validate();
    }

    /// <summary>The routes with Return Home from <paramref name="levelId"/> bringing Spyro back out of
    /// <paramref name="doorway"/> (null: "automatic", the doorway that leads there).</summary>
    public PortalRouteDocument SetReturn(PortalRouteDocument routes, int levelId, PortalEndpoint? doorway)
    {
        if (!HasReturnHome(levelId)) throw new InvalidDataException("Only a level (not a homeworld) has a Return Home.");
        if (doorway == null) return Automatic(routes, levelId).Validate();
        RequireRoutable(doorway, Homeworlds(routes));
        return routes.WithReturn(levelId, doorway == OriginalDoorway(levelId) ? null : doorway);
    }

    private PortalRouteDocument Automatic(PortalRouteDocument routes, int levelId)
    {
        var automatic = AutomaticFor(Homeworlds(routes), levelId);
        return routes.WithReturn(levelId, automatic == null || automatic == OriginalDoorway(levelId) ? null : automatic);
    }

    /// <summary>The whole map with <paramref name="routes"/>; <paramref name="doorwaysLeftOut"/>: Fix It For Me left the
    /// doorway links out of the game (Create BIN makes the game's own).</summary>
    public LevelLinksMap Map(PortalRouteDocument routes, bool doorwaysLeftOut = false)
    {
        var homes = Homeworlds(routes);
        var doorways = homes.SelectMany(h => h.Doorways).ToList();
        string DoorwayPhrase(PortalEndpoint portal)
        {
            var d = portal.Resolve();
            var home = homes.FirstOrDefault(h => h.Key == d.HomeworldKey);
            return $"{Possessive(home?.Name ?? d.HomeworldKey)} {d.DestinationName} doorway";
        }
        List<LevelLinkProblem> stops = [], problems = [], notes = [];

        // Saved links Create BIN cannot make: a doorway of a rebuilt homeworld (its place in the game is the built level's).
        foreach (var home in homes.Where(h => h.Rebuilt))
        {
            var mine = HomeworldPortalControlCatalog.ForLevel(home.Key).Select(d => new PortalEndpoint(home.Key, d.PathTrueIndex)).ToHashSet();
            var sent = routes.Destinations.Where(d => mine.Contains(d.Portal)).ToList();
            var back = routes.Returns.Where(r => mine.Contains(r.Portal)).ToList();
            if (sent.Count + back.Count == 0) continue;
            List<string> what = [];
            if (sent.Count > 0) what.Add(sent.Count == 1 ? $"its {sent[0].Portal.Resolve().DestinationName} doorway to {PlaceOf(sent[0].DestinationLevelId).Name}" : $"{sent.Count} of its doorways to other levels");
            if (back.Count > 0) what.Add(back.Count == 1 ? $"Return Home from {PlaceOf(back[0].LevelId).Name} through it" : $"Return Home from {back.Count} levels through it");
            stops.Add(new($"{home.Name} is rebuilt as {home.RebuiltAs}, so the links saved for the game's own {home.Name} ({Join(what)}) can't go into the game: Create BIN stops on them.",
                true, [.. sent.Select(s => s.DestinationLevelId), .. back.Select(b => b.LevelId)], "Forget Those Links",
                r => (r with { Destinations = [.. r.Destinations.Where(d => !mine.Contains(d.Portal))], Returns = [.. r.Returns.Where(x => !mine.Contains(x.Portal))] }).Validate()));
        }
        if (!HasNewLevel && (routes.Destinations.Any(d => d.DestinationLevelId == 65) || routes.Returns.Any(r => r.LevelId == 65)))
            stops.Add(new("A doorway leads to the new level, but the project has no new level (File ▾ › New Level… makes one): Create BIN stops on it.", true, [65],
                "Send Them Back", r => (r with { Destinations = [.. r.Destinations.Where(d => d.DestinationLevelId != 65)], Returns = [.. r.Returns.Where(x => x.LevelId != 65)] }).Validate()));

        List<LevelLinkReturn> returns = [];
        var levelIds = _catalog.Levels.Where(l => HasReturnHome(l.LevelId)).Select(l => l.LevelId).Concat(HasNewLevel ? [65] : []).Distinct().OrderBy(id => id);
        foreach (int id in levelIds)
        {
            var place = PlaceOf(id);
            var leading = doorways.Where(d => d.LeadsTo == id).ToList();
            var saved = routes.Returns.FirstOrDefault(r => r.LevelId == id)?.Portal;
            var effective = saved ?? OriginalDoorway(id);
            var automatic = AutomaticFor(homes, id);
            returns.Add(new(place, saved, effective, automatic, leading, _vortex(id)));
            if (leading.Count == 0)
                problems.Add(new(id == 65
                    ? $"{place.Name} has no doorway leading to it yet: Spyro only gets there when New Game starts in it. Send a doorway to it under Doorways."
                    : $"{place.Name} has no doorway leading to it, so Spyro can't get into it.", false, [id]));
            else if (leading.Count > 1)
            {
                string through = effective != null && leading.Any(d => d.Portal == effective)
                    ? $"Return Home goes back through the {effective.Resolve().DestinationName} one"
                    : effective == null ? "Return Home has no doorway to come back out of" : $"Return Home goes back through {DoorwayPhrase(effective)}";
                problems.Add(new($"{Count(leading.Count)} doorways lead to {place.Name} ({Join(leading.Select(d => DoorwayPhrase(d.Portal)).ToList())}); {through}.", false, [id]));
            }
            else if (effective != null && effective != leading[0].Portal)
            {
                var other = doorways.FirstOrDefault(d => d.Portal == effective);
                problems.Add(new(other == null
                    ? $"Return Home from {place.Name} brings Spyro back out of {DoorwayPhrase(effective)}, which is no longer in its rebuilt homeworld."
                    : $"Return Home from {place.Name} brings Spyro back out of {DoorwayPhrase(effective)}, which leads to {PlaceOf(other.LeadsTo).Name}, not out of the doorway that leads to it.",
                    false, [id]));
            }
            var vortex = _vortex(id);
            if (vortex.Present == false && (vortex.Expected || place.Built))
                notes.Add(new(!vortex.Expected
                    ? $"{place.Name} has no Return Home vortex: Spyro can leave it only from the pause menu (Exit Level). Add Object can add one (search for \"Return Home\")."
                    : place.Built
                        ? $"{place.Name} has no Return Home vortex (the game's {place.SlotName} has one): Spyro can leave it only from the pause menu (Exit Level). File ▾ › Fill In What the Level Needs… adds it."
                        : $"{place.Name} has lost its Return Home vortex: Spyro can leave it only from the pause menu (Exit Level). Put it back (undo the removal, or Add Object: search for \"Return Home\").",
                    false, [id]));
        }

        LevelLinkWorld? world = null;
        if (_world != null)
        {
            var home = _worldScenes.FirstOrDefault(s => s.WorldLevelId == NewWorldDocument.Homeworld);
            // The world is called by its homeworld's name (Create BIN saves it so).
            var named = _world.NamedAfter(home?.Name);
            string homeName = home?.Name ?? named.Name;
            var entry = _catalog.FindByKey(named.EntryHomeworldKey);
            string entryName = entry?.DisplayName ?? named.EntryHomeworldKey;
            var portals = home?.Portals ?? [];
            List<LevelLinkWorldLevel> levels = [];
            foreach (int id in named.LevelIds.Where(id => id != NewWorldDocument.Homeworld))
            {
                var scene = _worldScenes.FirstOrDefault(s => s.WorldLevelId == id);
                if (scene == null) continue;
                bool door = portals.Any(p => p.DestinationLevelId == id);
                levels.Add(new(id, scene.Name, door, _vortex(id)));
                if (!door)
                    problems.Add(new($"{scene.Name} (in your world) has no doorway in {homeName} yet, so Spyro can't get into it. Open {homeName} and use Terrain › Doorways › Add Doorway….", false, [id]));
                if (_vortex(id).Present == false)
                    notes.Add(new($"{scene.Name} (in your world) has no Return Home vortex: Spyro can leave it only from the pause menu (Exit Level). Add Object can add one (search for \"Return Home\").", false, [id]));
            }
            var backs = home == null ? [] : WorldAccess.WaysBack(home);
            int? wayBack = backs.Count > 0 ? backs[0].DestinationLevelId : null;
            bool balloonist = home != null && WorldAccess.HasBalloonist(home);
            var place = named.BalloonPlaceWith(balloonist);
            if (named.BalloonOnly)
            {
                // By balloon only: the homeworld's balloonist flies Spyro back; there is no doorway in or out.
                if (home != null && !balloonist)
                    stops.Add(new($"{homeName} has no balloonist, so Spyro could not fly back out of your world: Create BIN stops until it has one. " +
                        $"Fix It For Me then stands one near where Spyro arrives, or place him yourself in {homeName} (Objects › Random objects, \"The way out\").", true, [NewWorldDocument.Homeworld]));
                if (backs.Count > 0)
                    notes.Add(new($"{homeName} still has a doorway back out to {PlaceOf(backs[0].DestinationLevelId).Name} from before. Your world is reached by balloon, so it isn't needed: " +
                        $"Create BIN takes it out (the level as it was is kept in the project's backups). To keep it, choose a doorway as well with Terrain › World Entrance in {entryName}.", false, [NewWorldDocument.Homeworld]));
            }
            else if (home != null && backs.Count == 0)
                stops.Add(new($"{homeName} has no doorway back to {entryName}, so Spyro could not leave your world: Create BIN stops until it has one. Open {homeName} and use Terrain › Doorways › Add Doorway… ({entryName}).", true, [NewWorldDocument.Homeworld]));
            else if (entry != null && backs.Any(p => p.DestinationLevelId != entry.LevelId))
                stops.Add(new($"{Possessive(homeName)} doorway back leads to {PlaceOf(backs.First(p => p.DestinationLevelId != entry.LevelId).DestinationLevelId).SlotName}, but your world is entered from {entryName}: Create BIN stops. Point it at {entryName} (Terrain › Doorways in {homeName}).", true, [NewWorldDocument.Homeworld]));
            world = new(named.Name, homeName, named.EntryHomeworldKey, entryName, _entrance?.HomeworldKey, levels, wayBack, named.Balloon)
            {
                Access = WorldAccess.Describe(named, place, id => PlaceOf(id).Name), BalloonOnly = named.BalloonOnly, HasBalloonist = balloonist, Place = place,
            };
        }

        if (doorwaysLeftOut && routes.HasEdits)
            stops.Insert(0, new("Fix It For Me left these doorway links out of the game: Create BIN makes the game's own doorways until you put them back (More › Left Out of the Game…).", false, []));
        // Every level a doorway may be sent to, and any other level one leads to now (so each list can show it).
        var choices = DestinationChoices();
        return new(homes, [.. choices.Concat(doorways.Select(d => d.LeadsTo).Distinct().Where(id => choices.All(c => c.LevelId != id)).OrderBy(id => id).Select(PlaceOf))],
            returns, world, [.. stops, .. problems, .. notes]);
    }

    /// <summary>"Artisans'", "Stone Hill's".</summary>
    public static string Possessive(string name) => name.EndsWith('s') || name.EndsWith('S') ? name + "'" : name + "'s";
    private static string Count(int n) => n switch { 2 => "Two", 3 => "Three", 4 => "Four", 5 => "Five", _ => n.ToString() };
    private static string Join(IReadOnlyList<string> parts) => parts.Count <= 1 ? string.Join("", parts) : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1];
}

/// <summary>Whether a level has its Return Home vortex (class 0x009), read from what the project saved: a level you build
/// from its scene's objects, one of the game's levels from the editor's object cache of the original level and the
/// level's saved object changes. Nothing is read from a disc.</summary>
public static class ReturnHomeVortexCheck
{
    /// <summary>The objects include a Return Home vortex that is not deleted.</summary>
    public static bool Has(IEnumerable<Moby> objects) => objects.Any(m => !m.IsRemoved && m.ClassId == LevelCompletionCheck.ReturnHomeClass);

    /// <summary>A level built from <paramref name="scene"/>; <paramref name="slotHasOne"/>: the game's level in its place has
    /// one (null: not known).</summary>
    public static ReturnHomeVortex ForScene(SceneDocument scene, bool? slotHasOne) =>
        new(Has(scene.Objects.Select(o => o.Value)), slotHasOne ?? false);

    /// <summary>The rows of the game's own level <paramref name="levelKey"/> that are its Return Home vortexes, from the
    /// project's object cache (null when the cache has not been made).</summary>
    public static IReadOnlyList<int>? OwnRows(string workspaceRoot, string levelKey)
    {
        string path = Path.Combine(workspaceRoot, "editor-cache", levelKey + "-mobys.json");
        if (!File.Exists(path)) return null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            if (!document.RootElement.TryGetProperty("mobys", out var rows) || rows.ValueKind != JsonValueKind.Array) return null;
            List<int> exits = [];
            foreach (var row in rows.EnumerateArray())
                if ((JsonValue.GetInt32(row, "sourceByte36Hex", -1) | JsonValue.GetInt32(row, "sourceByte37Hex", 0) << 8) == LevelCompletionCheck.ReturnHomeClass)
                    exits.Add(JsonValue.GetInt32(row, "trueIndex", -1));
            return exits;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>One of the game's levels as the project changes it (its saved <c>&lt;key&gt;-native-edits.json</c>, if
    /// any): its own vortexes not deleted, or one added.</summary>
    public static ReturnHomeVortex ForGameLevel(string workspaceRoot, string levelKey)
    {
        var own = OwnRows(workspaceRoot, levelKey);
        if (own == null) return ReturnHomeVortex.Unknown;
        string path = Path.Combine(workspaceRoot, levelKey + "-native-edits.json");
        if (!File.Exists(path)) return new(own.Count > 0, own.Count > 0);
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            HashSet<int> removed = [];
            bool added = false;
            if (document.RootElement.TryGetProperty("edits", out var edits) && edits.ValueKind == JsonValueKind.Array)
                foreach (var edit in edits.EnumerateArray())
                {
                    bool isAdded = JsonValue.GetBoolean(edit, "added"), isRemoved = JsonValue.GetBoolean(edit, "removed");
                    int cls = JsonValue.GetInt32(edit, "sourceByte36Hex", -1) | JsonValue.GetInt32(edit, "sourceByte37Hex", 0) << 8;
                    if (isRemoved && !isAdded) removed.Add(JsonValue.GetInt32(edit, "trueIndex", -1));
                    else if (isAdded && !isRemoved && cls == LevelCompletionCheck.ReturnHomeClass) added = true;
                }
            return new(added || own.Any(r => !removed.Contains(r)), own.Count > 0);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return new(null, own.Count > 0); }
    }
}

using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Editing;

/// <summary>How players reach the project's own world and come back. Like the game's own worlds, by balloon: the
/// balloonist of the homeworld before it on the route (<see cref="NewWorldDocument.BalloonPlaceWith"/>) flies Spyro there
/// once his price is met, and the world's own balloonist flies him back (and on to the next world). Nothing is built into
/// the game's homeworlds and the world's homeworld has no doorway back out (<see cref="NewWorldDocument.BalloonOnly"/>), so
/// its homeworld must have a balloonist (<see cref="HasBalloonist(SceneDocument)"/>). A doorway as well is the user's own
/// choice (<see cref="NewWorldDocument.Doorway"/>, Terrain › World Entrance); a world off the balloon route is reached
/// through its doorway only. <see cref="Describe"/> says it in one sentence for every window that mentions it.</summary>
public static class WorldAccess
{
    /// <summary>The game's six balloonists (one class per homeworld, Artisans' first).</summary>
    public const int FirstBalloonistClass = UniversalBalloonistRows.BalloonistClass, LastBalloonistClass = FirstBalloonistClass + 5;

    /// <summary>A balloonist that is not deleted.</summary>
    public static bool IsBalloonist(Moby m) =>
        !m.IsRemoved && m.SourceByte37 == 0 && m.SourceByte36 is >= FirstBalloonistClass and <= LastBalloonistClass;

    public static bool HasBalloonist(IEnumerable<Moby> objects) => objects.Any(IsBalloonist);
    public static bool HasBalloonist(SceneDocument homeworld) => HasBalloonist(homeworld.Objects.Select(o => o.Value));

    /// <summary>The world's homeworld's doorways back out to the game (every doorway not to one of its levels 71-74).</summary>
    public static IReadOnlyList<AuthoredPortal> WaysBack(SceneDocument homeworld) =>
        [.. (homeworld.Portals ?? []).Where(p => !NewWorldDocument.LevelSlots.Contains(p.DestinationLevelId))];

    /// <summary>"Artisans'", "GLOOMY FEN's".</summary>
    public static string Possessive(string name) => name.EndsWith('s') || name.EndsWith('S') ? name + "'" : name + "'s";

    /// <summary>One sentence on how players get to the world and back, for <paramref name="place"/> (the route Create BIN
    /// builds). <paramref name="name"/>: the name of one of the game's homeworlds (10-60).</summary>
    public static string Describe(NewWorldDocument world, WorldBalloonPlace place, Func<int, string> name)
    {
        string w = world.Name, own = Possessive(w);
        string entry = name((NewWorldDocument.HomeworldKeys.ToList().IndexOf(world.EntryHomeworldKey) + 1) * 10);
        if (!place.OnRoute)
            return $"Players reach {w} through its doorway in {entry} (Links › World Entrance there) and leave through {own} doorway back to {entry}; " +
                   "it is not on the balloon route, so the balloonists do not fly there.";
        var route = place.Route()!;
        int[] order = NativeBalloonTravel.Order(route);
        int q = Array.IndexOf(order, NewWorldDocument.Homeworld);
        string before = name(order[q - 1]);
        string sentence;
        if (q == order.Length - 1)
            sentence = $"Players reach {w} by balloon at the end of the route: once {Possessive(before)} balloonist has opened every world, " +
                       $"every balloonist's menu lists {w}, and {own} balloonist {place.Balloonist} flies Spyro back to any of them.";
        else
        {
            string there = route.ToWorld.Kind == BalloonRequirementKind.Nothing
                ? $"{Possessive(before)} balloonist flies Spyro there whenever he asks"
                : $"{Possessive(before)} balloonist flies Spyro there once he has {route.ToWorld.Done()}";
            string next = name(order[q + 1]);
            string on = route.Onward.Kind == BalloonRequirementKind.Nothing ? $"and on to {next}" : $"and on to {next} once he has {route.Onward.Done()}";
            sentence = $"Players reach {w} by balloon: {there}, and {own} balloonist {place.Balloonist} flies him back to {before} ({on}).";
        }
        if (world.Doorway)
            sentence += $" They can also walk in through its doorway in {entry} and out through {own} doorway back (a doorway as well is your choice: Links › World Entrance in {entry}).";
        return sentence;
    }

    /// <summary>How far from where Spyro arrives (walking, in <see cref="LevelFill.Cell"/> cells) a balloonist is stood: at
    /// least 8 cells (256 units: the balloon Spyro arrives in comes down behind him), about 12 at best.</summary>
    public const int NearestSteps = 8, BestSteps = 12;

    /// <summary>Where a balloonist (and his balloon, which hangs over the ground beside him) can stand in the world's
    /// homeworld: level, open, plain floor Spyro can walk to from where he arrives, near it and ahead of him to the left
    /// so he sees him on arrival, clear of the level's objects and doorways, with room overhead for the balloon. Facing
    /// where Spyro arrives. Null when the level has no such place.</summary>
    public static LevelFill.Spot? BalloonistSpot(SceneDocument homeworld)
    {
        var grid = new LevelFill.FloorGrid(homeworld);
        var spawn = homeworld.Spawn;
        var walk = grid.Walk(spawn);
        if (walk.Count == 0) return null;
        var taken = homeworld.Objects.Where(o => !o.Value.IsRemoved).Select(o => o.Value.Position).ToList();
        var doors = (homeworld.Portals ?? []).Select(p => p.Position).ToList();
        double want = homeworld.Heading * Math.PI / 128 + Math.PI / 4;
        static float D2(Vector3f a, Vector3f b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
        Vector3f? best = null;
        double bestScore = double.MaxValue;
        foreach (var (place, steps) in walk)
        {
            if (steps < NearestSteps || !grid.IsLevel(place) || grid.Openness(place, 4) < 4) continue;
            var p = grid.Point(place);
            if (p.X < 256 || p.Y < 256 || p.X > 16127 || p.Y > 16127) continue;
            if (D2(p, spawn) < 256 * 256 || taken.Any(t => D2(t, p) < 160 * 160) || doors.Any(d => D2(d, p) < 320 * 320)) continue;
            // Room overhead for the balloon: no floor or roof above within a few cells.
            bool roofed = false;
            for (int dx = -3; dx <= 3 && !roofed; dx++)
                for (int dy = -3; dy <= 3 && !roofed; dy++)
                    roofed = grid.Floors(p.X + dx * LevelFill.Cell, p.Y + dy * LevelFill.Cell).Any(f => f.Z > p.Z + 12 && f.Z < p.Z + 480);
            if (roofed) continue;
            double off = Math.Abs(Math.IEEERemainder(Math.Atan2(p.Y - spawn.Y, p.X - spawn.X) - want, 2 * Math.PI));
            double score = Math.Abs(steps - BestSteps) + off * 6;
            if (score < bestScore) { bestScore = score; best = p; }
        }
        return best is { } spot ? new(spot, LevelFill.Facing(spot, spawn)) : null;
    }
}

using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Editing;

/// <summary>A level a doorway can lead to: its id and name, and whether Create BIN can make that doorway work today.</summary>
public sealed record DoorwayDestination(int LevelId, string Name, bool Taken, string? Note)
{
    public override string ToString() => Note == null ? Name : $"{Name} ({Note})";
}

/// <summary>Terrain › Doorways in a level you build: adding, moving, turning, reshaping, retargeting and removing a
/// homeworld's doorways together with their frames. Every operation returns a new scene; the frame's faces are ordinary
/// scene faces listed by their doorway (<see cref="AuthoredPortal.FrameFaces"/>), rebuilt from the doorway's shape and
/// place, keeping their textures.</summary>
public static class SceneDoorways
{
    /// <summary>Original doorways (made before the Doorways tool) are framed as Lost Fleet's were: a border this wide and
    /// this deep to each side (<see cref="NativeHubPortals.Frame"/>'s defaults).</summary>
    public const float OriginalFrameWidth = 14, OriginalFrameDepth = 14;
    /// <summary>How a doorway to one of the new world's levels is listed until Create BIN builds the new world.</summary>
    /// <summary>The note on the new world homeworld's doorway back to the game's homeworld it is entered from.</summary>
    public const string WayBackNote = "the way back out of your world";

    /// <summary>Why the scene cannot have doorways (plain words), or null when it can: a doorway needs the doorway code of a
    /// homeworld's own program, so only a homeworld you rebuilt (or the new world's homeworld) has them.</summary>
    public static string? Unavailable(SceneDocument? scene, LevelCatalog catalog)
    {
        if (scene == null) return "Doorways are added in a level you build. Open one, or rebuild a homeworld with Start Blank.";
        if (scene.WorldLevelId == NewWorldDocument.Homeworld && scene.TargetBinding == null) return null;
        if (scene.WorldLevelId is not null)
            return "This is one of your new world's levels. Doorways belong to its homeworld: open the world's homeworld to add the doorways that lead here.";
        if (scene.TargetBinding is not { } binding)
            return "Doorways only work in a homeworld (Artisans, Peace Keepers, Magic Crafters, Beast Makers, Dream Weavers or Gnasty's World): " +
                   "the game keeps its doorway code in those levels. This level opens from New Game; it has no doorway code.";
        if (binding.LevelId % 10 != 0 || HomeworldPortalControlCatalog.ForLevel(binding.LevelKey).Count == 0)
        {
            string name = catalog.FindByKey(binding.LevelKey)?.DisplayName ?? binding.LevelKey;
            return $"Doorways only work in a homeworld: the game keeps its doorway code in Artisans, Peace Keepers, Magic Crafters, Beast Makers, " +
                   $"Dream Weavers and Gnasty's World. This level is built in {name}'s place, which has none. Use Return Home or an exit vortex to leave it.";
        }
        return null;
    }

    /// <summary>The levels a doorway of this scene can lead to: a rebuilt homeworld's original destinations (their doorway
    /// code, path, lettering and return point come from the original homeworld), or the new world's levels.</summary>
    /// <param name="wayBack">For the new world's homeworld: the game's homeworld the world is entered from (its doorway
    /// there leads back), or null.</param>
    public static IReadOnlyList<DoorwayDestination> Destinations(SceneDocument scene, LevelCatalog catalog, Func<int, string?>? worldLevelName = null,
        int? wayBack = null)
    {
        var used = scene.Portals?.Select(p => p.DestinationLevelId).ToHashSet() ?? [];
        if (scene.WorldLevelId == NewWorldDocument.Homeworld && scene.TargetBinding == null)
        {
            var levels = NewWorldDocument.LevelSlots.Where(id => worldLevelName?.Invoke(id) != null)
                .Select(id => new DoorwayDestination(id, worldLevelName!(id)!, used.Contains(id), null));
            if (wayBack is int home && catalog.Levels.FirstOrDefault(l => l.LevelId == home) is { } back)
                levels = levels.Append(new DoorwayDestination(home, back.DisplayName, used.Contains(home), WayBackNote));
            return levels.ToArray();
        }
        if (scene.TargetBinding is not { } binding) return [];
        return HomeworldPortalControlCatalog.ForLevel(binding.LevelKey)
            .Select(p => new DoorwayDestination(p.DestinationLevelId, catalog.Levels.FirstOrDefault(l => l.LevelId == p.DestinationLevelId)?.DisplayName ?? p.DestinationName,
                used.Contains(p.DestinationLevelId), null)).ToArray();
    }

    public static AuthoredPortal? Find(SceneDocument scene, int destination) => scene.Portals?.FirstOrDefault(p => p.DestinationLevelId == destination);

    /// <summary>The doorway whose frame has this face.</summary>
    public static AuthoredPortal? OwnerOf(SceneDocument scene, string faceId) =>
        scene.Portals?.FirstOrDefault(p => p.FrameFaces?.Contains(faceId, StringComparer.Ordinal) == true);

    /// <summary>The frame a doorway has where it stands: its shape's (<see cref="DoorwayShapes.Frame"/>), or for an original
    /// doorway the border Lost Fleet's were given around its original opening (needs that opening:
    /// <paramref name="original"/>); null when neither is known.</summary>
    public static IReadOnlyList<DoorwayFrameFace>? FrameGeometry(AuthoredPortal portal, Func<int, NativeHubPortals.DoorwayTemplate?>? original)
    {
        if (DoorwayShapes.Find(portal.Shape) is { } shape)
            return DoorwayShapes.Frame(shape, portal.SizeScale, portal.Position, portal.Heading);
        if (original?.Invoke(portal.DestinationLevelId) is not { } template) return null;
        var (outline, normal) = NativeHubPortals.PlaceOriginal(template, portal);
        return NativeHubPortals.FrameGeometry(outline, normal, OriginalFrameWidth, OriginalFrameDepth);
    }

    /// <summary>Adds a doorway with its frame (new faces in <paramref name="portal"/>'s frame textures).</summary>
    public static SceneDocument Add(SceneDocument scene, AuthoredPortal portal, Func<int, NativeHubPortals.DoorwayTemplate?>? original = null)
    {
        var portals = scene.Portals ?? [];
        if (portals.Any(p => p.DestinationLevelId == portal.DestinationLevelId))
            throw new InvalidDataException("This level already has a doorway to that level. Move or reshape that one instead.");
        if (portals.Count >= NativeHubPortals.MaxDoorways)
            throw new InvalidDataException($"A homeworld can have at most {NativeHubPortals.MaxDoorways} doorways.");
        if (portal.FaceMaterial == null || portal.SideMaterial == null)
            throw new InvalidDataException("Choose the doorway frame's texture first.");
        portal = portal with { Position = Whole(portal.Position) };
        var geometry = FrameGeometry(portal, original) ?? throw new InvalidDataException("This doorway has no shape.");
        var faces = geometry.Select(f => NewFace(f, f.Part == DoorwayFramePart.Face ? portal.FaceMaterial! : portal.SideMaterial!)).ToArray();
        var added = portal with { FrameFaces = faces.Select(f => f.Id).ToArray() };
        var result = scene with { Surfaces = [.. scene.Surfaces, .. faces], Portals = [.. portals, added] };
        result.Validate();
        return result;
    }

    /// <summary>Replaces the doorway to <paramref name="destination"/> with <paramref name="changed"/> (moved, turned,
    /// reshaped, resized, retargeted or given new frame textures) and rebuilds its frame there. With the same shape and
    /// size every frame face keeps its identity, texture, shade and ground (a face the user removed stays removed); with
    /// another shape the new frame takes, for its front and back and for its sides, the texture most of the old frame's
    /// faces of that part show. With <paramref name="repaint"/> every face of that part of the frame takes the doorway's
    /// new texture for it (<see cref="AuthoredPortal.FaceMaterial"/> or <see cref="AuthoredPortal.SideMaterial"/>).</summary>
    public static SceneDocument Update(SceneDocument scene, int destination, AuthoredPortal changed,
        Func<int, NativeHubPortals.DoorwayTemplate?>? original = null, DoorwayFramePart? repaint = null)
    {
        var portals = (scene.Portals ?? []).ToList();
        int index = portals.FindIndex(p => p.DestinationLevelId == destination);
        if (index < 0) throw new InvalidDataException("That doorway is no longer in this level.");
        var old = portals[index];
        if (changed.DestinationLevelId != destination && portals.Any(p => p.DestinationLevelId == changed.DestinationLevelId))
            throw new InvalidDataException("This level already has a doorway to that level.");
        changed = changed with { Position = Whole(changed.Position) };
        var surfaces = scene.Surfaces.ToList();
        var oldIds = old.FrameFaces ?? [];
        var oldGeometry = FrameGeometry(old, original);
        var newGeometry = FrameGeometry(changed, original);
        if (newGeometry == null)
        {
            // An original doorway whose original opening is not at hand: it moves without its frame.
            portals[index] = changed with { FrameFaces = old.FrameFaces };
            return Finish(scene, surfaces, portals);
        }
        bool sameLayout = oldGeometry != null && oldIds.Count == newGeometry.Count && oldGeometry.Count == newGeometry.Count &&
            Enumerable.Range(0, newGeometry.Count).All(i => oldGeometry[i].Part == newGeometry[i].Part);
        // The frame's own textures: given ones, else what most of its faces of that part show, else the doorway's.
        AuthoredMaterial? Dominant(DoorwayFramePart part)
        {
            if (oldGeometry == null || oldGeometry.Count != oldIds.Count) return null;
            return oldIds.Select((id, i) => (Face: surfaces.FirstOrDefault(s => s.Id == id), Part: oldGeometry[i].Part))
                .Where(x => x.Face?.Material != null && x.Part == part).GroupBy(x => x.Face!.Material!)
                .OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
        }
        var faceMaterial = (repaint == DoorwayFramePart.Face ? changed.FaceMaterial : null) ?? Dominant(DoorwayFramePart.Face) ?? changed.FaceMaterial ?? old.FaceMaterial;
        var sideMaterial = (repaint == DoorwayFramePart.Side ? changed.SideMaterial : null) ?? Dominant(DoorwayFramePart.Side) ?? changed.SideMaterial ?? old.SideMaterial;
        if (faceMaterial == null || sideMaterial == null)
            throw new InvalidDataException("Choose the doorway frame's texture first.");
        List<string> ids;
        if (sameLayout && repaint == null)
        {
            ids = [.. oldIds];
            for (int i = 0; i < ids.Count; i++)
            {
                int at = surfaces.FindIndex(s => s.Id == ids[i]);
                if (at >= 0) surfaces[at] = surfaces[at] with { Vertices = newGeometry[i].Corners };
            }
        }
        else if (sameLayout)
        {
            // A new texture for one part of the same frame: every face of that part takes it.
            ids = [.. oldIds];
            for (int i = 0; i < ids.Count; i++)
            {
                int at = surfaces.FindIndex(s => s.Id == ids[i]);
                if (at < 0) continue;
                surfaces[at] = newGeometry[i].Part == repaint
                    ? surfaces[at] with { Vertices = newGeometry[i].Corners, Material = repaint == DoorwayFramePart.Face ? faceMaterial : sideMaterial }
                    : surfaces[at] with { Vertices = newGeometry[i].Corners };
            }
        }
        else
        {
            var oldSet = oldIds.ToHashSet(StringComparer.Ordinal);
            surfaces.RemoveAll(s => oldSet.Contains(s.Id));
            var faces = newGeometry.Select(f => NewFace(f, f.Part == DoorwayFramePart.Face ? faceMaterial : sideMaterial)).ToArray();
            surfaces.AddRange(faces);
            ids = faces.Select(f => f.Id).ToList();
        }
        portals[index] = changed with { FrameFaces = ids, FaceMaterial = faceMaterial, SideMaterial = sideMaterial };
        return Finish(scene, surfaces, portals);
    }

    /// <summary>Removes the doorway to <paramref name="destination"/> and its frame.</summary>
    public static SceneDocument Remove(SceneDocument scene, int destination)
    {
        var portals = (scene.Portals ?? []).ToList();
        var old = portals.FirstOrDefault(p => p.DestinationLevelId == destination) ?? throw new InvalidDataException("That doorway is no longer in this level.");
        portals.Remove(old);
        var frame = (old.FrameFaces ?? []).ToHashSet(StringComparer.Ordinal);
        return Finish(scene, scene.Surfaces.Where(s => !frame.Contains(s.Id)).ToList(), portals);
    }

    /// <summary>An original doorway (made before the Doorways tool, as Lost Fleet's): finds the frame
    /// <see cref="NativeHubPortals.Frame"/> gave it, face for face, and records those faces and their textures as its frame
    /// so it moves and changes with it. Returns the scene unchanged and 0 when no such face is there.</summary>
    public static (SceneDocument Scene, int Found) AdoptOriginalFrame(SceneDocument scene, int destination, NativeHubPortals.DoorwayTemplate template)
    {
        var portals = (scene.Portals ?? []).ToList();
        int index = portals.FindIndex(p => p.DestinationLevelId == destination);
        if (index < 0 || portals[index].Shape != null || portals[index].FrameFaces != null) return (scene, 0);
        var portal = portals[index];
        var geometry = FrameGeometry(portal, d => d == destination ? template : null)!;
        var claimed = (scene.Portals ?? []).SelectMany(p => p.FrameFaces ?? []).ToHashSet(StringComparer.Ordinal);
        // Faces near the doorway, by their corners (order kept: the frame builder wrote them as they are).
        var (cx, cy) = (portal.Position.X, portal.Position.Y);
        var near = scene.Surfaces.Where(s => !claimed.Contains(s.Id) && s.Vertices.All(v => Math.Abs(v.X - cx) < 400 && Math.Abs(v.Y - cy) < 400)).ToList();
        List<string> ids = [];
        int found = 0;
        foreach (var f in geometry)
        {
            var match = near.FirstOrDefault(s => s.Vertices.Length == f.Corners.Length &&
                s.Vertices.Zip(f.Corners).All(p => Math.Abs(p.First.X - p.Second.X) <= 0.51f && Math.Abs(p.First.Y - p.Second.Y) <= 0.51f && Math.Abs(p.First.Z - p.Second.Z) <= 0.51f));
            if (match != null) { ids.Add(match.Id); near.Remove(match); found++; }
            else ids.Add(Guid.NewGuid().ToString("N"));       // a face that is not there (stays absent when the frame moves)
        }
        if (found == 0) return (scene, 0);
        AuthoredMaterial? Most(DoorwayFramePart part) => ids.Select((id, i) => (Face: scene.Surfaces.FirstOrDefault(s => s.Id == id), geometry[i].Part))
            .Where(x => x.Face?.Material != null && x.Part == part).GroupBy(x => x.Face!.Material!).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
        portals[index] = portal with { FrameFaces = ids, FaceMaterial = Most(DoorwayFramePart.Face), SideMaterial = Most(DoorwayFramePart.Side) };
        var result = scene with { Portals = portals };
        result.Validate();
        return (result, found);
    }

    /// <summary>The heading turned by <paramref name="steps"/> steps of 1/24 turn (15°): headings are 256ths of a turn, so
    /// 15° steps land on the nearest 256th (within 0.7°).</summary>
    public static int Turn(int heading, int steps)
    {
        double k = Math.Round(heading * 24 / 256.0) + steps;
        return ((int)Math.Round(k * 256 / 24) % 256 + 256) % 256;
    }

    /// <summary>A heading of 0-255 in whole degrees (0 = east, 90 = north).</summary>
    public static int Degrees(int heading) => (int)Math.Round(heading * 360 / 256.0) % 360;

    /// <summary>The heading (0-255) that points from (<paramref name="fromX"/>, <paramref name="fromY"/>) towards
    /// (<paramref name="toX"/>, <paramref name="toY"/>), on a 15° step.</summary>
    public static int HeadingTowards(double fromX, double fromY, double toX, double toY)
    {
        double angle = Math.Atan2(toY - fromY, toX - fromX);
        return Turn((int)Math.Round(angle * 128 / Math.PI) & 0xFF, 0);
    }

    private static AuthoredSurface NewFace(DoorwayFrameFace f, AuthoredMaterial material) =>
        new(Guid.NewGuid().ToString("N"), f.Corners, f.Shade, material, Enumerable.Repeat(f.Shade, f.Corners.Length).ToArray(),
            Enumerable.Repeat(f.Shade, f.Corners.Length).ToArray());

    private static Vector3f Whole(Vector3f v) => new(MathF.Round(v.X), MathF.Round(v.Y), MathF.Round(v.Z));

    private static SceneDocument Finish(SceneDocument scene, List<AuthoredSurface> surfaces, List<AuthoredPortal> portals)
    {
        var result = scene with { Surfaces = surfaces, Portals = portals.Count == 0 ? null : portals };
        result.Validate();
        return result;
    }
}

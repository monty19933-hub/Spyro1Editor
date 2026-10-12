using System.Text.Json;
using System.Text.Json.Nodes;

namespace Spyro.Editor.Core.Editing;

/// <summary>A level you build moved across the game's map, everything in it together: its faces, the player start, its
/// objects (and routes they were given), doorways, distant meshes, gem spots and collision-only faces. Used to make room
/// when a level sits against an edge of the map (0-16383 units each way) and you build towards it.</summary>
public static class SceneMove
{
    public const int MapEdge = 16383;

    /// <summary>The room on each side of everything the scene holds: west and south down to 0, east and north up to
    /// <see cref="MapEdge"/>.</summary>
    public static (float West, float South, float East, float North) Room(SceneDocument scene)
    {
        var points = Points(scene).ToList();
        if (points.Count == 0) return (0, 0, MapEdge, MapEdge);
        return (points.Min(p => p.X), points.Min(p => p.Y), MapEdge - points.Max(p => p.X), MapEdge - points.Max(p => p.Y));
    }

    /// <summary>A copy of the scene moved by whole units east (<paramref name="dx"/>) and north (<paramref name="dy"/>).</summary>
    public static SceneDocument Moved(SceneDocument scene, int dx, int dy)
    {
        var root = JsonNode.Parse(scene.Serialize())!.AsObject();
        void Point(JsonNode? p) { if (p is JsonObject o) { o["X"] = (float)o["X"]! + dx; o["Y"] = (float)o["Y"]! + dy; } }
        void Points(JsonNode? list) { if (list is JsonArray a) foreach (var p in a) Point(p); }
        Point(root["Spawn"]);
        foreach (var s in root["Surfaces"]?.AsArray() ?? []) Points(s?["Vertices"]);
        foreach (var c in root["CollisionFaces"]?.AsArray() ?? []) Points(c?["Vertices"]);
        foreach (var m in root["FarMeshes"]?.AsArray() ?? []) Points(m?["Vertices"]);
        Points(root["GemSpots"]);
        foreach (var p in root["Portals"]?.AsArray() ?? []) Point(p?["Position"]);
        foreach (var o in root["Objects"]?.AsArray() ?? [])
        {
            var value = o?["Value"];
            Point(value?["Position"]); Point(value?["OriginalPosition"]);
            // A route's nodes are raw game units (editor units x 16).
            foreach (var node in value?["UniversalPathNodes"]?.AsArray() ?? [])
                if (node is JsonArray xyz && xyz.Count >= 2) { xyz[0] = (int)xyz[0]! + dx * 16; xyz[1] = (int)xyz[1]! + dy * 16; }
        }
        var moved = JsonSerializer.Deserialize<SceneDocument>(root.ToJsonString()) ?? throw new InvalidDataException("The moved level could not be read back.");
        moved.Validate();
        return moved;
    }

    private static IEnumerable<Primitives.Vector3f> Points(SceneDocument scene) =>
        scene.Surfaces.SelectMany(s => s.Vertices)
            .Concat(scene.CollisionFaces?.SelectMany(c => c.Vertices) ?? [])
            .Concat(scene.FarMeshes?.SelectMany(m => m.Vertices) ?? [])
            .Concat(scene.GemSpots ?? [])
            .Concat(scene.Portals?.Select(p => p.Position) ?? [])
            .Concat(scene.Objects.Where(o => !o.Value.IsRemoved).Select(o => o.Value.Position))
            .Append(scene.Spawn);
}

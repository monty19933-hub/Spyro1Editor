using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>A rebuilt homeworld's doorways, taken from the original homeworld and moved to where the scene puts them.
/// A doorway is: a record in the level's portal table (facing normal, the outline of its opening, the row of its path
/// object, the destination, and the destination's sky that the game draws through the opening), type-6 entry
/// triangles in the collision (Spyro touching them walks in), and three objects: the path object (0x18E; Spyro
/// walks its two nodes in and, coming back, out), the floating name (class 1) and the return controller (0x11E,
/// which carries the same path).</summary>
public sealed class NativeHubPortals
{
    /// <summary>One doorway: its moved record bytes, entry triangles (editor units) and objects.</summary>
    public sealed record Doorway(int DestinationLevelId, byte[] Record, IReadOnlyList<(Vector3f A, Vector3f B, Vector3f C)> Triggers,
        IReadOnlyList<UniversalMobyPlacement> Objects)
    {
        /// <summary>The opening's outline (editor units, in the record's order) and its facing (unit, horizontal).</summary>
        public IReadOnlyList<Vector3f> Outline { get; init; } = [];
        public Vector3f Normal { get; init; }
    }

    public string HomeworldKey { get; }
    public IReadOnlyList<Doorway> Doorways { get; }
    private NativeHubPortals(string key, IReadOnlyList<Doorway> doorways, IReadOnlyList<int> saveIdentities, int letters)
    { HomeworldKey = key; Doorways = doorways; SaveIdentities = saveIdentities; SpawnedLetters = letters; }

    /// <summary>Each floating name spawns one object per letter into the level's spare object slots (the original
    /// homeworld keeps them as rows), so the rebuilt level reserves that many more.</summary>
    public int SpawnedLetters { get; }

    /// <summary>The portal section that replaces a built level's empty one: count, then the records.</summary>
    public byte[] Section()
    {
        byte[] s = new byte[4 + Doorways.Sum(d => d.Record.Length)];
        BinaryPrimitives.WriteInt32LittleEndian(s, Doorways.Count);
        int at = 4;
        foreach (var d in Doorways) { d.Record.CopyTo(s, at); at += d.Record.Length; }
        return s;
    }

    /// <summary>Entry triangles with their ground kind (type 6: destination, doorway index).</summary>
    public IEnumerable<(Vector3f A, Vector3f B, Vector3f C, AuthoredSurfaceKind Kind)> Triggers() =>
        Doorways.SelectMany((d, i) => d.Triggers.Select(t => (t.A, t.B, t.C, AuthoredSurfaceKind.Portal(d.DestinationLevelId, i))));

    /// <summary>The objects every doorway needs, appended to the level's placements (path object first).</summary>
    /// <summary>Save identities the doorway objects take: original decorations' (never set on any card), so the
    /// level's own objects keep every free identity.</summary>
    public IReadOnlyList<int> SaveIdentities { get; }

    public IReadOnlyList<UniversalMobyPlacement> WithObjects(IReadOnlyList<UniversalMobyPlacement> placements)
    {
        var objects = Doorways.SelectMany(d => d.Objects).ToArray();
        if (objects.Length > SaveIdentities.Count)
            throw new InvalidDataException($"The original homeworld has {SaveIdentities.Count} unused save identities; {objects.Length} doorway objects need one each.");
        return [.. placements, .. objects.Select((o, i) => o with { SaveIdentity = SaveIdentities[i] })];
    }

    /// <summary>After composition: each record names the physical row of its path object.</summary>
    public void BindPathRows(byte[] levelData, IReadOnlyList<UniversalMobyPlacement> placements, IReadOnlyList<int> rows)
    {
        var data = new UniversalLevelData(levelData);
        var (offset, length) = data.Subfiles()[1];
        int cursor = offset;
        for (int i = 0; i < 6; i++) cursor += checked((int)BinaryPrimitives.ReadUInt32LittleEndian(levelData.AsSpan(cursor)));
        if (BinaryPrimitives.ReadInt32LittleEndian(levelData.AsSpan(cursor)) != Doorways.Count)
            throw new InvalidDataException("The rebuilt homeworld's portal table is not where it was written.");
        int record = cursor + 4;
        for (int k = 0; k < Doorways.Count; k++)
        {
            string id = PathId(Doorways[k].DestinationLevelId);
            int index = Enumerable.Range(0, placements.Count).Single(i => placements[i].EditorId == id);
            if (rows[index] < 0) throw new InvalidDataException("A doorway's path object was not placed.");
            BinaryPrimitives.WriteInt32LittleEndian(levelData.AsSpan(record + 0x18), rows[index]);
            record += Doorways[k].Record.Length;
        }
        if (record > offset + length) throw new InvalidDataException("The rebuilt homeworld's portal table overran its data.");
    }

    public static string PathId(int destination) => $"hub-portal-{destination}-path";

    /// <summary>A frame around a doorway's opening: a border <paramref name="width"/> units wide along its sides and top
    /// (not the floor edge), reaching <paramref name="depth"/> units in front of and behind the opening, mitred at the
    /// corners and cut into pieces about six widths long (a level's terrain data is near the game's limit). The front and back faces take
    /// <paramref name="face"/>, the rest <paramref name="side"/>; the posts reach a little below the floor.</summary>
    public static IReadOnlyList<AuthoredSurface> Frame(Doorway doorway, AuthoredMaterial face, AuthoredMaterial side,
        float width = 14, float depth = 14, ColorRgba? light = null)
    {
        var o = doorway.Outline.Select(p => (X: (double)p.X, Y: (double)p.Y, Z: (double)p.Z)).ToArray();
        int n = o.Length;
        if (n < 3) throw new InvalidDataException("A doorway opening needs at least three points.");
        // The floor edge joins the two lowest points; the frame runs around the others, from one floor point to the other.
        int low = Enumerable.Range(0, n).OrderBy(i => o[i].Z).First();
        int next = (low + 1) % n, prev = (low + n - 1) % n;
        int start = o[next].Z <= o[prev].Z ? next : low;          // chain start: the floor point after the floor edge
        var chain = Enumerable.Range(0, n).Select(k => o[(start + k) % n]).ToArray();
        var nx = doorway.Normal.X; var ny = doorway.Normal.Y;
        var c = (X: o.Average(p => p.X), Y: o.Average(p => p.Y), Z: o.Average(p => p.Z));
        // In the doorway's plane: the direction along it is (-ny, nx, 0); outward perpendiculars of each edge.
        (double X, double Y, double Z) Perp(int k)
        {
            var a = chain[k]; var b = chain[k + 1];
            double ex = b.X - a.X, ey = b.Y - a.Y, ez = b.Z - a.Z;
            double px = ny * ez, py = -nx * ez, pz = nx * ey - ny * ex;              // normal x edge
            double l = Math.Sqrt(px * px + py * py + pz * pz);
            px /= l; py /= l; pz /= l;
            double mx = (a.X + b.X) / 2 - c.X, my = (a.Y + b.Y) / 2 - c.Y, mz = (a.Z + b.Z) / 2 - c.Z;
            return px * mx + py * my + pz * mz < 0 ? (-px, -py, -pz) : (px, py, pz);
        }
        var perps = Enumerable.Range(0, n - 1).Select(Perp).ToArray();
        var outer = new (double X, double Y, double Z)[n];
        for (int k = 0; k < n; k++)
        {
            (double X, double Y, double Z) dir;
            if (k == 0) dir = perps[0];
            else if (k == n - 1) dir = perps[n - 2];
            else
            {
                var a = perps[k - 1]; var b = perps[k];
                double mx = a.X + b.X, my = a.Y + b.Y, mz = a.Z + b.Z, ml = Math.Sqrt(mx * mx + my * my + mz * mz);
                mx /= ml; my /= ml; mz /= ml;
                double cos = mx * a.X + my * a.Y + mz * a.Z;
                dir = (mx / cos, my / cos, mz / cos);
            }
            outer[k] = (chain[k].X + dir.X * width, chain[k].Y + dir.Y * width, chain[k].Z + dir.Z * width);
        }
        // The posts reach below the floor.
        foreach (int k in new[] { 0, n - 1 }) { chain[k].Z -= 8; outer[k].Z -= 8; }
        var tint = light ?? new ColorRgba(128, 128, 128);
        var shade = new ColorRgba((byte)(tint.R * 3 / 4), (byte)(tint.G * 3 / 4), (byte)(tint.B * 3 / 4));
        List<AuthoredSurface> faces = [];
        Vector3f V((double X, double Y, double Z) p, double s) => new((float)Math.Round(p.X + nx * s), (float)Math.Round(p.Y + ny * s), (float)Math.Round(p.Z));
        void Quad(AuthoredMaterial m, ColorRgba col, params Vector3f[] v)
        {
            if (v.Distinct().Count() < 4) return;
            faces.Add(new(Guid.NewGuid().ToString("N"), v, col, m, [col, col, col, col], [col, col, col, col]));
        }
        for (int k = 0; k + 1 < n; k++)
        {
            var (i0, i1, o0, o1) = (chain[k], chain[k + 1], outer[k], outer[k + 1]);
            double len = Math.Sqrt(Math.Pow(i1.X - i0.X, 2) + Math.Pow(i1.Y - i0.Y, 2) + Math.Pow(i1.Z - i0.Z, 2));
            int pieces = Math.Max(1, (int)Math.Round(len / (6 * width)));
            for (int q = 0; q < pieces; q++)
            {
                (double X, double Y, double Z) L((double X, double Y, double Z) a, (double X, double Y, double Z) b, double t) => (a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
                double t0 = q / (double)pieces, t1 = (q + 1) / (double)pieces;
                var a0 = L(i0, i1, t0); var a1 = L(i0, i1, t1); var b0 = L(o0, o1, t0); var b1 = L(o0, o1, t1);
                Quad(face, tint, V(a0, depth), V(b0, depth), V(b1, depth), V(a1, depth));        // front
                Quad(face, tint, V(a0, -depth), V(a1, -depth), V(b1, -depth), V(b0, -depth));    // back
                Quad(side, tint, V(b0, depth), V(b0, -depth), V(b1, -depth), V(b1, depth));      // outer
                Quad(side, shade, V(a0, -depth), V(a0, depth), V(a1, depth), V(a1, -depth));     // inside the opening
            }
        }
        return faces;
    }

    /// <param name="destinationSky">The sky each destination level (by id) actually has in this export, or null to
    /// keep the retail copy. A doorway's record carries a whole copy of its destination's sky: the game draws it in the
    /// doorway and through the first half of the entry flight (and the second half of the flight home), until the
    /// destination's own data has loaded (USA 0x80015420, 0x800159CC), so a stale copy shows a different sky midway.</param>
    public static NativeHubPortals? Build(string originalUSA, LevelCatalog catalog, SceneDocument document,
        Func<int, byte[]?>? destinationSky = null)
    {
        if (document.Portals is not { Count: > 0 } portals) return null;
        var home = document.ResolveTarget(catalog);
        var key = document.TargetBinding!.LevelKey;
        var source = PortalSourceDataLocator.Locate(originalUSA, catalog.FindByKey(key)!);
        DiscLayout layout = DiscImage.DetectLayout(originalUSA);
        using var stream = File.OpenRead(originalUSA);
        byte[] Wad(long at, int length) => DiscImage.ReadFileBytes(stream, layout, 37, at, length);
        var library = new UniversalSourceLibrary(originalUSA, catalog);
        var homeData = library.Get(key).Data;
        List<Doorway> doorways = [];
        foreach (var portal in portals)
        {
            var original = source.Portals.Single(p => p.DestinationLevelId == portal.DestinationLevelId);
            var control = HomeworldPortalControlCatalog.ForLevel(key).Single(d => d.DestinationLevelId == portal.DestinationLevelId);
            // The opening's bottom middle (its lowest outline points) moves to the scene's position; the normal turns to
            // the scene's heading.
            int pointCount = original.PointCount;
            byte[] head = Wad(original.WadOffset, 0x20 + 12 * pointCount + 24);
            int skyAt = 0x20 + 12 * pointCount + 20;
            int skyLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(skyAt)));
            byte[] record = Wad(original.WadOffset, skyAt + skyLength);
            if (destinationSky?.Invoke(portal.DestinationLevelId) is { } sky)
            {
                if (!Spyro1SkyBlockAnalyzer.IsValidStandaloneSkyBlock(sky) || BinaryPrimitives.ReadUInt32LittleEndian(sky) != sky.Length)
                    throw new InvalidDataException($"The sky of level {portal.DestinationLevelId} is not one native sky block.");
                record = [.. record.AsSpan(0, skyAt), .. sky];
            }
            var outline = Enumerable.Range(0, pointCount).Select(i => (X: I(record, 0x20 + 12 * i), Y: I(record, 0x24 + 12 * i), Z: I(record, 0x28 + 12 * i))).ToArray();
            int bottom = outline.Min(p => p.Z);
            var low = outline.Where(p => p.Z - bottom <= 16).ToArray();
            double ax = low.Average(p => p.X), ay = low.Average(p => p.Y), az = bottom;
            double from = Math.Atan2(I(record, 0x0C), I(record, 0x08)), to = portal.Heading * Math.PI / 128;
            double turn = to - from, cos = Math.Cos(turn), sin = Math.Sin(turn);
            double tx = portal.Position.X * 16, ty = portal.Position.Y * 16, tz = portal.Position.Z * 16;
            (int X, int Y, int Z) Move(double x, double y, double z)
            {
                double dx = x - ax, dy = y - ay;
                return ((int)Math.Round(tx + dx * cos - dy * sin), (int)Math.Round(ty + dx * sin + dy * cos), (int)Math.Round(tz + z - az));
            }
            double nx = I(record, 0x08), ny = I(record, 0x0C);
            W(record, 0x08, (int)Math.Round(nx * cos - ny * sin)); W(record, 0x0C, (int)Math.Round(nx * sin + ny * cos));
            W(record, 0x18, 0);   // the path object's row, bound after composition
            for (int i = 0; i < pointCount; i++)
            {
                var (x, y, z) = Move(outline[i].X, outline[i].Y, outline[i].Z);
                W(record, 0x20 + 12 * i, x); W(record, 0x24 + 12 * i, y); W(record, 0x28 + 12 * i, z);
            }
            // Entry triangles are in editor units.
            var triggers = source.TriggerTriangles.Where(t => t.PortalIndex == original.Index).Select(t =>
            {
                Vector3f P(PortalSourcePoint q) { var (x, y, z) = Move(q.X * 16.0, q.Y * 16.0, q.Z * 16.0); return new(MathF.Round(x / 16f), MathF.Round(y / 16f), MathF.Round(z / 16f)); }
                return (P(t.P1), P(t.P2), P(t.P3));
            }).ToArray();
            int yawTurn = (int)Math.Round(turn * 128 / Math.PI);
            UniversalMobyPlacement Object(int row, string id)
            {
                byte[] r = homeData.Row(row);
                var (x, y, z) = Move(I(r, 0x0C), I(r, 0x10), I(r, 0x14));
                var template = homeData.CaptureRow(key, row);
                Dictionary<int, int> overrides = [];
                // Path blocks reached from the properties: node count (byte 0; byte 1 is the current node), a direction
                // word, then nodes of x, y, z, 0.
                foreach (var (_, target) in template.InternalPointers)
                {
                    var p = template.Properties;
                    if (target + 8 > p.Length) continue;
                    int count = p[target];
                    if (count is < 1 or > 16 || p[target + 1] >= count || p[target + 2] != 0 || p[target + 3] != 0 || target + 8 + 16 * count > p.Length) continue;
                    for (int n = 0; n < count; n++)
                    {
                        int at = target + 8 + 16 * n;
                        var (px, py, pz) = Move(I(p, at), I(p, at + 4), I(p, at + 8));
                        overrides[at] = px; overrides[at + 4] = py; overrides[at + 8] = pz;
                    }
                }
                return new(key, row, x, y, z, Yaw: (r[0x46] + yawTurn) & 0xFF, EditorId: id)
                { PropertyWordOverrides = overrides.Count > 0 ? overrides : null };
            }
            int bias = source.SourceMobyIndexBias;
            UniversalMobyPlacement[] objects =
            [
                Object(control.PathTrueIndex - bias, PathId(portal.DestinationLevelId)),
                Object(control.LetteringTrueIndex - bias, $"hub-portal-{portal.DestinationLevelId}-name"),
                Object(control.CompanionTrueIndex - bias, $"hub-portal-{portal.DestinationLevelId}-return"),
            ];
            double nl = Math.Sqrt((double)I(record, 0x08) * I(record, 0x08) + (double)I(record, 0x0C) * I(record, 0x0C));
            doorways.Add(new(portal.DestinationLevelId, record, triggers, objects)
            {
                Outline = Enumerable.Range(0, pointCount).Select(i => new Vector3f(I(record, 0x20 + 12 * i) / 16f, I(record, 0x24 + 12 * i) / 16f, I(record, 0x28 + 12 * i) / 16f)).ToArray(),
                Normal = new((float)(I(record, 0x08) / nl), (float)(I(record, 0x0C) / nl), 0),
            });
        }
        int letters = portals.Sum(p => catalog.Levels.Single(l => l.LevelId == p.DestinationLevelId).DisplayName.Count(c => !char.IsWhiteSpace(c)));
        return new(key, doorways, UniversalSaveIdentities.Reusable(library.Get(key), library.SharedClasses), letters);
    }

    private static int I(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
    private static void W(byte[] b, int at, int v) => BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), v);
}

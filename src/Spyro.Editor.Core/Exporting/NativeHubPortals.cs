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
        float width = 14, float depth = 14, ColorRgba? light = null) =>
        FrameGeometry(doorway.Outline, doorway.Normal, width, depth, light).Select(f =>
            new AuthoredSurface(Guid.NewGuid().ToString("N"), f.Corners, f.Shade, f.Part == DoorwayFramePart.Face ? face : side,
                [f.Shade, f.Shade, f.Shade, f.Shade], [f.Shade, f.Shade, f.Shade, f.Shade])).ToArray();

    /// <summary>The faces of <see cref="Frame"/> (corners, part and shade) around an opening (editor units, in the record's
    /// order) facing <paramref name="normalIn"/>.</summary>
    public static IReadOnlyList<DoorwayFrameFace> FrameGeometry(IReadOnlyList<Vector3f> outline, Vector3f normalIn,
        float width = 14, float depth = 14, ColorRgba? light = null)
    {
        var o = outline.Select(p => (X: (double)p.X, Y: (double)p.Y, Z: (double)p.Z)).ToArray();
        int n = o.Length;
        if (n < 3) throw new InvalidDataException("A doorway opening needs at least three points.");
        // The floor edge joins the two lowest points; the frame runs around the others, from one floor point to the other.
        int low = Enumerable.Range(0, n).OrderBy(i => o[i].Z).First();
        int next = (low + 1) % n, prev = (low + n - 1) % n;
        int start = o[next].Z <= o[prev].Z ? next : low;          // chain start: the floor point after the floor edge
        var chain = Enumerable.Range(0, n).Select(k => o[(start + k) % n]).ToArray();
        var nx = normalIn.X; var ny = normalIn.Y;
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
        List<DoorwayFrameFace> faces = [];
        Vector3f V((double X, double Y, double Z) p, double s) => new((float)Math.Round(p.X + nx * s), (float)Math.Round(p.Y + ny * s), (float)Math.Round(p.Z));
        void Quad(DoorwayFramePart part, ColorRgba col, params Vector3f[] v)
        {
            if (v.Distinct().Count() < 4) return;
            faces.Add(new(v, part, col));
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
                Quad(DoorwayFramePart.Face, tint, V(a0, depth), V(b0, depth), V(b1, depth), V(a1, depth));        // front
                Quad(DoorwayFramePart.Face, tint, V(a0, -depth), V(a1, -depth), V(b1, -depth), V(b0, -depth));    // back
                Quad(DoorwayFramePart.Side, tint, V(b0, depth), V(b0, -depth), V(b1, -depth), V(b1, depth));      // outer
                Quad(DoorwayFramePart.Side, shade, V(a0, -depth), V(a0, depth), V(a1, depth), V(a1, -depth));     // inside the opening
            }
        }
        return faces;
    }

    /// <summary>A homeworld's original doorway to one level: its record as the disc has it (head, opening, the 20-byte sky
    /// header and the destination's sky), its entry triangles and the rows of its three objects.</summary>
    public sealed record DoorwayTemplate(string HomeworldKey, int DestinationLevelId, byte[] Record,
        IReadOnlyList<(PortalSourcePoint P1, PortalSourcePoint P2, PortalSourcePoint P3)> Triggers,
        HomeworldPortalControlDefinition Control, int SourceMobyIndexBias)
    {
        public int PointCount => I(Record, 4);
        public (int X, int Y, int Z) Point(int i) => (I(Record, 0x20 + 12 * i), I(Record, 0x24 + 12 * i), I(Record, 0x28 + 12 * i));
        /// <summary>Where the 20-byte sky header starts (after the opening's points).</summary>
        public int StubAt => 0x20 + 12 * PointCount;
        public int SkyAt => StubAt + 20;
        /// <summary>The bottom middle of the opening (game units): the middle of its lowest points, at the lowest height.</summary>
        public (double X, double Y, double Z) BottomMiddle
        {
            get
            {
                var points = Enumerable.Range(0, PointCount).Select(Point).ToArray();
                int bottom = points.Min(p => p.Z);
                var low = points.Where(p => p.Z - bottom <= 16).ToArray();
                return (low.Average(p => p.X), low.Average(p => p.Y), bottom);
            }
        }
        public double FacingAngle => Math.Atan2(I(Record, 0x0C), I(Record, 0x08));
    }

    /// <summary>The rigid move from a template doorway to the scene's: its bottom middle to <see cref="AuthoredPortal.Position"/>
    /// (game units: editor units x 16), its facing turned to <see cref="AuthoredPortal.Heading"/>.</summary>
    public readonly record struct DoorwayPlacement(double Ax, double Ay, double Az, double Turn, double Tx, double Ty, double Tz)
    {
        public static DoorwayPlacement For(DoorwayTemplate template, AuthoredPortal portal)
        {
            var (ax, ay, az) = template.BottomMiddle;
            return new(ax, ay, az, portal.Heading * Math.PI / 128 - template.FacingAngle, portal.Position.X * 16, portal.Position.Y * 16, portal.Position.Z * 16);
        }
        public (int X, int Y, int Z) Move(double x, double y, double z)
        {
            double cos = Math.Cos(Turn), sin = Math.Sin(Turn), dx = x - Ax, dy = y - Ay;
            return ((int)Math.Round(Tx + dx * cos - dy * sin), (int)Math.Round(Ty + dx * sin + dy * cos), (int)Math.Round(Tz + z - Az));
        }
        public (int X, int Y) Rotate(double x, double y)
        {
            double cos = Math.Cos(Turn), sin = Math.Sin(Turn);
            return ((int)Math.Round(x * cos - y * sin), (int)Math.Round(x * sin + y * cos));
        }
    }

    /// <summary>The original opening of <paramref name="template"/> moved to <paramref name="portal"/> (editor units, the
    /// record's order) and its facing (unit, horizontal).</summary>
    public static (IReadOnlyList<Vector3f> Outline, Vector3f Normal) PlaceOriginal(DoorwayTemplate template, AuthoredPortal portal)
    {
        var place = DoorwayPlacement.For(template, portal);
        var outline = Enumerable.Range(0, template.PointCount).Select(i =>
        {
            var (x, y, z) = template.Point(i);
            var (mx, my, mz) = place.Move(x, y, z);
            return new Vector3f(mx / 16f, my / 16f, mz / 16f);
        }).ToArray();
        var (nx, ny) = place.Rotate(I(template.Record, 0x08), I(template.Record, 0x0C));
        double nl = Math.Sqrt((double)nx * nx + (double)ny * ny);
        return (outline, new((float)(nx / nl), (float)(ny / nl), 0));
    }

    /// <summary>The doorway templates of a homeworld (the original game's records, triangles and object rows), by destination.</summary>
    public static IReadOnlyDictionary<int, DoorwayTemplate> Templates(string originalUSA, LevelCatalog catalog, string homeworldKey)
    {
        var source = PortalSourceDataLocator.Locate(originalUSA, catalog.FindByKey(homeworldKey) ?? throw new InvalidDataException($"No homeworld {homeworldKey}."));
        DiscLayout layout = DiscImage.DetectLayout(originalUSA);
        using var stream = File.OpenRead(originalUSA);
        byte[] Wad(long at, int length) => DiscImage.ReadFileBytes(stream, layout, 37, at, length);
        Dictionary<int, DoorwayTemplate> templates = [];
        foreach (var original in source.Portals)
        {
            var control = HomeworldPortalControlCatalog.ForLevel(homeworldKey).Single(d => d.DestinationLevelId == original.DestinationLevelId);
            int pointCount = original.PointCount;
            byte[] head = Wad(original.WadOffset, 0x20 + 12 * pointCount + 24);
            int skyAt = 0x20 + 12 * pointCount + 20;
            int skyLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(skyAt)));
            byte[] record = Wad(original.WadOffset, skyAt + skyLength);
            var triggers = source.TriggerTriangles.Where(t => t.PortalIndex == original.Index).Select(t => (t.P1, t.P2, t.P3)).ToArray();
            templates[original.DestinationLevelId] = new(homeworldKey, original.DestinationLevelId, record, triggers, control, source.SourceMobyIndexBias);
        }
        return templates;
    }

    /// <summary>Said when a scene of the new world (homeworld 70) is given to the doorway compiler on its own: its doorways
    /// are built only by Create BIN's New World step (<see cref="NativeNewWorldExport"/>), on its template homeworld.</summary>
    public const string NewWorldNotExportable =
        "Your new world's doorways are built by Create BIN together with the whole world (its homeworld is made on Artisans' doorways).";

    /// <param name="destinationSky">The sky each destination level (by id) actually has in this export, or null to
    /// keep the retail copy. A doorway's record carries a whole copy of its destination's sky: the game draws it in the
    /// doorway and through the first half of the entry flight (and the second half of the flight home), until the
    /// destination's own data has loaded (USA 0x80015420, 0x800159CC), so a stale copy shows a different sky midway.</param>
    public static NativeHubPortals? Build(string originalUSA, LevelCatalog catalog, SceneDocument document,
        Func<int, byte[]?>? destinationSky = null)
    {
        if (document.Portals is not { Count: > 0 } portals) return null;
        if (document.TargetBinding == null && document.WorldLevelId != null) throw new InvalidDataException(NewWorldNotExportable);
        // The new world's homeworld (70) is compiled on a template homeworld; each doorway to 71-74 takes the template's
        // doorway to 11-14, the way back the one to 15, and is pointed at its own level (return token, entry triangles,
        // the name's level number).
        bool world = document.WorldLevelId == NewWorldDocument.Homeworld;
        if (portals.Count > MaxDoorways)
            throw new InvalidDataException($"A homeworld can have at most {MaxDoorways} doorways (the game keeps {MaxDoorways}); this one has {portals.Count}.");
        var home = document.ResolveTarget(catalog);
        var key = document.TargetBinding!.LevelKey;
        var templates = Templates(originalUSA, catalog, key);
        var library = new UniversalSourceLibrary(originalUSA, catalog);
        var homeData = library.Get(key).Data;
        List<Doorway> doorways = [];
        foreach (var portal in portals)
        {
            int templateDestination = world ? NativeNewWorldExport.TemplateDestination(portal.DestinationLevelId) : portal.DestinationLevelId;
            if (!templates.TryGetValue(templateDestination, out var template))
                throw new InvalidDataException($"The homeworld has no doorway to level {templateDestination} to build this doorway on.");
            var control = template.Control;
            var (record, triggers, place) = PlaceRecord(template, portal, destinationSky?.Invoke(portal.DestinationLevelId));
            // +0x1C: the level Spyro returns from to arrive at this doorway (the original's own destination).
            if (world) BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(0x1C), portal.DestinationLevelId);
            int pointCount = I(record, 4);
            int yawTurn = (int)Math.Round(place.Turn * 128 / Math.PI);
            UniversalMobyPlacement Object(int row, string id)
            {
                byte[] r = homeData.Row(row);
                var (x, y, z) = place.Move(I(r, 0x0C), I(r, 0x10), I(r, 0x14));
                var captured = homeData.CaptureRow(key, row);
                Dictionary<int, int> overrides = [];
                // Path blocks reached from the properties: node count (byte 0; byte 1 is the current node), a direction
                // word, then nodes of x, y, z, 0.
                foreach (var (_, target) in captured.InternalPointers)
                {
                    var p = captured.Properties;
                    if (target + 8 > p.Length) continue;
                    int count = p[target];
                    if (count is < 1 or > 16 || p[target + 1] >= count || p[target + 2] != 0 || p[target + 3] != 0 || target + 8 + 16 * count > p.Length) continue;
                    for (int n = 0; n < count; n++)
                    {
                        int at = target + 8 + 16 * n;
                        var (px, py, pz) = place.Move(I(p, at), I(p, at + 4), I(p, at + 8));
                        overrides[at] = px; overrides[at + 4] = py; overrides[at + 8] = pz;
                    }
                }
                return new(key, row, x, y, z, Yaw: (r[0x46] + yawTurn) & 0xFF, EditorId: id)
                { PropertyWordOverrides = overrides.Count > 0 ? overrides : null };
            }
            int bias = template.SourceMobyIndexBias;
            UniversalMobyPlacement[] objects =
            [
                Object(control.PathTrueIndex - bias, PathId(portal.DestinationLevelId)),
                world ? Object(control.LetteringTrueIndex - bias, $"hub-portal-{portal.DestinationLevelId}-name") with
                    { PropertyWordOverrides = new Dictionary<int, int> { [0] = NativeLevelWindow.VirtualIndex(portal.DestinationLevelId) } }
                    : Object(control.LetteringTrueIndex - bias, $"hub-portal-{portal.DestinationLevelId}-name"),
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

    /// <summary>A doorway's record and entry triangles where the scene puts it: <paramref name="template"/>'s record (with
    /// <paramref name="sky"/> in place of its copy of the destination's sky, when given) moved so the bottom middle of its
    /// opening stands at <see cref="AuthoredPortal.Position"/> facing <see cref="AuthoredPortal.Heading"/>, and for a doorway
    /// with a <see cref="AuthoredPortal.Shape"/> that shape's opening in place of the original one. The path object's row
    /// (+0x18) is left 0 for <see cref="BindPathRows"/>.</summary>
    public static (byte[] Record, IReadOnlyList<(Vector3f A, Vector3f B, Vector3f C)> Triggers, DoorwayPlacement Place) PlaceRecord(
        DoorwayTemplate template, AuthoredPortal portal, byte[]? sky = null)
    {
        // The opening's bottom middle (its lowest outline points) moves to the scene's position; the normal turns to
        // the scene's heading.
        int pointCount = template.PointCount;
        byte[] record = template.Record.ToArray();
        int skyAt = template.SkyAt;
        if (sky != null)
        {
            if (!Spyro1SkyBlockAnalyzer.IsValidStandaloneSkyBlock(sky) || BinaryPrimitives.ReadUInt32LittleEndian(sky) != sky.Length)
                throw new InvalidDataException($"The sky of level {portal.DestinationLevelId} is not one native sky block.");
            record = [.. record.AsSpan(0, skyAt), .. sky];
        }
        var place = DoorwayPlacement.For(template, portal);
        var (nx, ny) = place.Rotate(I(record, 0x08), I(record, 0x0C));
        W(record, 0x08, nx); W(record, 0x0C, ny);
        W(record, 0x18, 0);   // the path object's row, bound after composition
        IReadOnlyList<(Vector3f A, Vector3f B, Vector3f C)> triggers;
        if (DoorwayShapes.Find(portal.Shape) is { } shape)
        {
            // A shaped doorway: the shape's opening replaces the original's (the sky header and sky follow it), facing
            // exactly along the heading; its entry triangles fill the opening.
            var outline = DoorwayShapes.OutlineInLevel(shape, portal.SizeScale, portal.Position, portal.Heading)
                .Select(p => ((int)Math.Round(p.X * 16), (int)Math.Round(p.Y * 16), (int)Math.Round(p.Z * 16))).ToArray();
            var (fx, fy) = DoorwayShapes.Facing(portal.Heading);
            byte[] head = record.AsSpan(0, 0x20).ToArray();
            W(head, 0x04, outline.Length); W(head, 0x08, (int)Math.Round(fx * 4096)); W(head, 0x0C, (int)Math.Round(fy * 4096)); W(head, 0x10, 0);
            byte[] points = new byte[12 * outline.Length];
            for (int i = 0; i < outline.Length; i++) { W(points, 12 * i, outline[i].Item1); W(points, 12 * i + 4, outline[i].Item2); W(points, 12 * i + 8, outline[i].Item3); }
            record = [.. head, .. points, .. record.AsSpan(template.StubAt)];
            pointCount = outline.Length;
            triggers = EntryTriangles(outline.Select(p => new Vector3f(p.Item1 / 16f, p.Item2 / 16f, p.Item3 / 16f)).ToArray());
        }
        else
        {
            for (int i = 0; i < pointCount; i++)
            {
                var (px, py, pz) = template.Point(i);
                var (x, y, z) = place.Move(px, py, pz);
                W(record, 0x20 + 12 * i, x); W(record, 0x24 + 12 * i, y); W(record, 0x28 + 12 * i, z);
            }
            // Entry triangles are in editor units.
            triggers = template.Triggers.Select(t =>
            {
                Vector3f P(PortalSourcePoint q) { var (x, y, z) = place.Move(q.X * 16.0, q.Y * 16.0, q.Z * 16.0); return new(MathF.Round(x / 16f), MathF.Round(y / 16f), MathF.Round(z / 16f)); }
                return (P(t.P1), P(t.P2), P(t.P3));
            }).ToArray();
        }
        return (record, triggers, place);
    }

    /// <summary>The game keeps at most this many doorways per homeworld (its loaded-doorway table, USA 0x80078640, holds
    /// six pointers).</summary>
    public const int MaxDoorways = 6;

    /// <summary>A shaped opening's entry triangles (editor units): a fan from its first point, as the game's own doorways
    /// have them, each triangle in both turns (Spyro meets it from either side), cut so that no triangle spans more than
    /// 240 units along an axis (a collision triangle holds at most 255).</summary>
    public static IReadOnlyList<(Vector3f A, Vector3f B, Vector3f C)> EntryTriangles(IReadOnlyList<Vector3f> outline)
    {
        List<(Vector3f, Vector3f, Vector3f)> result = [];
        static float Span(Vector3f a, Vector3f b, Vector3f c) => new[]
        {
            Math.Max(a.X, Math.Max(b.X, c.X)) - Math.Min(a.X, Math.Min(b.X, c.X)),
            Math.Max(a.Y, Math.Max(b.Y, c.Y)) - Math.Min(a.Y, Math.Min(b.Y, c.Y)),
            Math.Max(a.Z, Math.Max(b.Z, c.Z)) - Math.Min(a.Z, Math.Min(b.Z, c.Z)),
        }.Max();
        static Vector3f Mid(Vector3f a, Vector3f b) => new(MathF.Round((a.X + b.X) / 2), MathF.Round((a.Y + b.Y) / 2), MathF.Round((a.Z + b.Z) / 2));
        void Add(Vector3f a, Vector3f b, Vector3f c, int depth)
        {
            if (a == b || b == c || a == c) return;
            if (Span(a, b, c) > 240 && depth < 6)
            {
                var ab = Mid(a, b); var bc = Mid(b, c); var ca = Mid(c, a);
                Add(a, ab, ca, depth + 1); Add(ab, b, bc, depth + 1); Add(ca, bc, c, depth + 1); Add(ab, bc, ca, depth + 1);
                return;
            }
            result.Add((a, b, c));
            result.Add((a, c, b));
        }
        for (int i = 1; i + 1 < outline.Count; i++)
            Add(new(MathF.Round(outline[0].X), MathF.Round(outline[0].Y), MathF.Round(outline[0].Z)),
                new(MathF.Round(outline[i].X), MathF.Round(outline[i].Y), MathF.Round(outline[i].Z)),
                new(MathF.Round(outline[i + 1].X), MathF.Round(outline[i + 1].Y), MathF.Round(outline[i + 1].Z)), 0);
        return result;
    }

    private static int I(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
    private static void W(byte[] b, int at, int v) => BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), v);
}

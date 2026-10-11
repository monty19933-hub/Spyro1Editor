using System.Buffers.Binary;

namespace Spyro.Editor.Core.Crash;

/// <summary>An axis-aligned box of a Crash 3 level in world units.</summary>
public readonly record struct CrashBox(int X, int Y, int Z, int W, int H, int D)
{
    public bool ContainsXZ(double x, double z) => x >= X && x < X + W && z >= Z && z < Z + D;
    /// <summary>Horizontal distance from a point to the box (0 inside).</summary>
    public double DistanceXZ(double x, double z)
    {
        double dx = Math.Max(0, Math.Max(X - x, x - (X + W))), dz = Math.Max(0, Math.Max(Z - z, z - (Z + D)));
        return Math.Sqrt(dx * dx + dz * dz);
    }
}

/// <summary>An entity of a zone (a box, an enemy, Crash's start "obj_willy"...): its name, type, subtype and its path
/// points in world units.</summary>
public sealed record CrashEntity(string Name, int Type, int? Subtype, IReadOnlyList<CrashPoint> Positions)
{
    /// <summary>Crash's start in a level (type 0, "obj_willy").</summary>
    public bool IsPlayerStart => Type == 0;
    /// <summary>A box (crate, TNT, Nitro, checkpoint...; a stacked crate sits 400 units higher).</summary>
    public bool IsBox => Type == 34;
}

/// <summary>A collision leaf: its value and box. The value is odd; (value &gt;&gt; 1) = flags &lt;&lt; 9 | surface &lt;&lt; 3 | kind,
/// kind 0 and 2 solid (2 is what walls the level's corridor in), 1 ground (surface: the sound and look of the ground),
/// 4 a deadly fall (pits; with flags 6). Empty space is not a leaf.</summary>
public readonly record struct CrashCollisionLeaf(int Value, double X, double Y, double Z, double W, double H, double D)
{
    public int Kind => (Value >> 1) & 7;
    public int Surface => (Value >> 4) & 63;
    public int Flags => Value >> 10;
    public bool IsGround => Kind == 1;
    public bool IsDeadly => Kind == 4;
    public bool IsSolid => !IsGround && !IsDeadly;
    public double Top => Y + H;
}

/// <summary>A zone's collision octree (zone item 1), decoded as found by probing the USA release (2026-10-10) against the
/// drawn ground: int32 box x, y, z, w, h, d at 0; the root node at 0x1C and the maximum depths along x, y and z at
/// 0x1E, 0x20 and 0x22. A node is a 16-bit value: 0 empty, odd a leaf (<see cref="CrashCollisionLeaf"/>), otherwise the
/// byte offset (in the item) of its children. A node splits every axis whose depth is not used up, so it has 8, 4 or 2
/// children, ordered with x as the most significant choice, then y, then z (lower half first).</summary>
public sealed class CrashZoneCollision
{
    private readonly byte[] _data;
    public CrashZoneCollision(byte[] item)
    {
        if (item.Length < 0x24) throw new InvalidDataException("A Crash zone's collision item is too short.");
        _data = item;
        Box = new(I(0), I(4), I(8), I(12), I(16), I(20));
        DepthX = S(0x1E); DepthY = S(0x20); DepthZ = S(0x22);
        if (Box.W <= 0 || Box.H <= 0 || Box.D <= 0 || DepthX is < 0 or > 15 || DepthY is < 0 or > 15 || DepthZ is < 0 or > 15)
            throw new InvalidDataException("A Crash zone's collision header is invalid.");
    }
    private int I(int o) => BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(o));
    private int S(int o) => BinaryPrimitives.ReadInt16LittleEndian(_data.AsSpan(o));
    private int Node(int o) => o >= 0 && o + 2 <= _data.Length ? BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(o))
        : throw new InvalidDataException("A Crash zone's collision octree points past its item.");

    public CrashBox Box { get; }
    public int DepthX { get; }
    public int DepthY { get; }
    public int DepthZ { get; }
    /// <summary>The smallest cell size along each axis.</summary>
    public (double X, double Y, double Z) Cell => (Box.W / (double)(1 << DepthX), Box.H / (double)(1 << DepthY), Box.D / (double)(1 << DepthZ));

    /// <summary>Every leaf of the octree.</summary>
    public IEnumerable<CrashCollisionLeaf> Leaves() => Collect(null, null);

    /// <summary>The leaves of the vertical column through (x, z), bottom first.</summary>
    public IReadOnlyList<CrashCollisionLeaf> Column(double x, double z)
    {
        if (!Box.ContainsXZ(x, z)) return [];
        var list = Collect(x, z).ToList();
        list.Sort((a, b) => a.Y.CompareTo(b.Y));
        return list;
    }

    private IEnumerable<CrashCollisionLeaf> Collect(double? px, double? pz)
    {
        var stack = new Stack<(int Offset, double X, double Y, double Z, double W, double H, double D, int Dx, int Dy, int Dz, int Depth)>();
        stack.Push((0x1C, Box.X, Box.Y, Box.Z, Box.W, Box.H, Box.D, DepthX, DepthY, DepthZ, 0));
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            int v = Node(n.Offset);
            if ((v & 1) != 0) { yield return new(v, n.X, n.Y, n.Z, n.W, n.H, n.D); continue; }
            if (v == 0) continue;
            if (n.Depth > 48) throw new InvalidDataException("A Crash zone's collision octree is too deep.");
            bool sx = n.Dx > 0, sy = n.Dy > 0, sz = n.Dz > 0;
            double w = sx ? n.W / 2 : n.W, h = sy ? n.H / 2 : n.H, d = sz ? n.D / 2 : n.D;
            int k = 0;
            for (int bx = 0; bx <= (sx ? 1 : 0); bx++)
                for (int by = 0; by <= (sy ? 1 : 0); by++)
                    for (int bz = 0; bz <= (sz ? 1 : 0); bz++, k++)
                    {
                        double x = n.X + bx * w, y = n.Y + by * h, z = n.Z + bz * d;
                        if (px is double qx && (qx < x || qx >= x + w)) continue;
                        if (pz is double qz && (qz < z || qz >= z + d)) continue;
                        stack.Push((v + 2 * k, x, y, z, w, h, d, Math.Max(0, n.Dx - 1), Math.Max(0, n.Dy - 1), Math.Max(0, n.Dz - 1), n.Depth + 1));
                    }
        }
    }
}

/// <summary>A zone (NSF entry type 7): its scenery (item 0: int32 count, scenery IDs at 4 + 48 x i), its collision
/// (item 1) and its entities (items 2 on: a 16-byte header with the property count at +12, 8-byte property headers
/// (id, offset - 12, type, element size, rows); property 0x2C name, 0x4B positions (int32 count, int16 x, y, z relative
/// to the zone), 0xA9 type, 0xAA subtype).</summary>
public sealed record CrashZone(string Name, CrashBox Box, IReadOnlyList<string> Scenery, CrashZoneCollision Collision, IReadOnlyList<CrashEntity> Entities)
{
    public const int EntryType = 7;

    public static CrashZone Read(CrashEntry entry)
    {
        if (entry.Type != EntryType || entry.Items.Count < 2) throw new InvalidDataException($"{entry.Name} is not a Crash zone.");
        byte[] head = entry.Items[0];
        int count = Math.Clamp(BinaryPrimitives.ReadInt32LittleEndian(head), 0, 8);
        var scenery = Enumerable.Range(0, count).Where(i => 8 + 48 * i <= head.Length)
            .Select(i => CrashNsf.Name(BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(4 + 48 * i)))).ToArray();
        var collision = new CrashZoneCollision(entry.Items[1]);
        var box = collision.Box;
        List<CrashEntity> entities = [];
        foreach (byte[] it in entry.Items.Skip(2))
            if (ReadEntity(it, box) is { } e) entities.Add(e);
        return new(entry.Name, box, scenery, collision, entities);
    }

    private static CrashEntity? ReadEntity(byte[] it, CrashBox box)
    {
        if (it.Length < 16) return null;
        int n = BinaryPrimitives.ReadInt32LittleEndian(it.AsSpan(12));
        if (n is <= 0 or >= 64 || 16 + 8 * n > it.Length) return null;
        var props = Enumerable.Range(0, n).Select(i => (Id: BinaryPrimitives.ReadUInt16LittleEndian(it.AsSpan(16 + 8 * i)),
            Offset: BinaryPrimitives.ReadUInt16LittleEndian(it.AsSpan(18 + 8 * i)) + 12)).ToArray();
        Dictionary<int, byte[]> d = [];
        for (int k = 0; k < n; k++)
        {
            int start = props[k].Offset, end = k + 1 < n ? props[k + 1].Offset : it.Length;
            if (start < 0 || end > it.Length || end < start) return null;
            d[props[k].Id] = it[start..end];
        }
        if (!d.TryGetValue(0x4B, out var pos) || !d.TryGetValue(0xA9, out var type) || pos.Length < 4 || type.Length < 8) return null;
        string name = d.TryGetValue(0x2C, out var nm) && nm.Length > 4 ? System.Text.Encoding.Latin1.GetString(nm, 4, nm.Length - 4).Split('\0')[0] : "";
        int cnt = BinaryPrimitives.ReadInt32LittleEndian(pos);
        if (cnt < 0 || 4 + 6 * cnt > pos.Length) return null;
        var points = Enumerable.Range(0, cnt).Select(i => new CrashPoint(box.X + BinaryPrimitives.ReadInt16LittleEndian(pos.AsSpan(4 + 6 * i)),
            box.Y + BinaryPrimitives.ReadInt16LittleEndian(pos.AsSpan(6 + 6 * i)), box.Z + BinaryPrimitives.ReadInt16LittleEndian(pos.AsSpan(8 + 6 * i)))).ToArray();
        int? sub = d.TryGetValue(0xAA, out var s) && s.Length >= 8 ? BinaryPrimitives.ReadInt32LittleEndian(s.AsSpan(4)) : null;
        return new(name, BinaryPrimitives.ReadInt32LittleEndian(type.AsSpan(4)), sub, points);
    }
}

/// <summary>A Crash 3 level's terrain: its scenery, zones and texture pages, and its main area (the largest group of
/// non-sky scenery lying within 3,000 units of each other) with the zones along it, in name order (Crash numbers its
/// zones along the path).</summary>
public sealed class CrashLevelData
{
    public CrashLevelData(IReadOnlyDictionary<string, CrashScenery> scenery, IReadOnlyDictionary<string, CrashZone> zones, IReadOnlyDictionary<string, byte[]> pages)
    {
        Scenery = scenery; Zones = zones; Pages = pages;
        Clusters = FindClusters();
        Main = Clusters.Count > 0 ? Clusters[0] : [];
        var main = Main.ToHashSet(StringComparer.Ordinal);
        MainZones = [.. zones.Values.OrderBy(z => z.Name, StringComparer.Ordinal).Where(z =>
        {
            var visible = z.Scenery.Where(n => !scenery.TryGetValue(n, out var sc) || !sc.IsSky).ToList();
            return z.Scenery.Count > 0 && z.Scenery.Count(main.Contains) * 2 >= visible.Count;
        })];
    }

    public static CrashLevelData Read(CrashNsf nsf) => new(
        nsf.Entries.Where(e => e.Type == CrashScenery.EntryType && e.Items.Count == 7).ToDictionary(e => e.Name, CrashScenery.Read, StringComparer.Ordinal),
        nsf.Entries.Where(e => e.Type == CrashZone.EntryType && e.Items.Count >= 2).ToDictionary(e => e.Name, CrashZone.Read, StringComparer.Ordinal),
        nsf.Pages);

    public IReadOnlyDictionary<string, CrashScenery> Scenery { get; }
    public IReadOnlyDictionary<string, CrashZone> Zones { get; }
    public IReadOnlyDictionary<string, byte[]> Pages { get; }
    /// <summary>Groups of non-sky scenery whose boxes lie within <see cref="ClusterMargin"/> of each other, most faces first.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Clusters { get; }
    public IReadOnlyList<string> Main { get; }
    public IReadOnlyList<CrashZone> MainZones { get; }
    public const int ClusterMargin = 3000;

    public IEnumerable<(CrashScenery Scenery, CrashSceneryFace Face)> MainFaces() => Main.SelectMany(n => Scenery[n].Faces.Select(f => (Scenery[n], f)));

    /// <summary>Crash's start (the first obj_willy along the main zones), or null.</summary>
    public CrashPoint? PlayerStart => MainZones.SelectMany(z => z.Entities).FirstOrDefault(e => e.IsPlayerStart && e.Positions.Count > 0)?.Positions[0];

    private List<IReadOnlyList<string>> FindClusters()
    {
        var names = Scenery.Where(s => !s.Value.IsSky && s.Value.Vertices.Count > 0).Select(s => s.Key).Order(StringComparer.Ordinal).ToList();
        var box = names.ToDictionary(n => n, n =>
        {
            var v = Scenery[n].Vertices;
            return (v.Min(p => p.X), v.Max(p => p.X), v.Min(p => p.Y), v.Max(p => p.Y), v.Min(p => p.Z), v.Max(p => p.Z));
        });
        var parent = names.ToDictionary(n => n, n => n);
        string Find(string a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
        for (int i = 0; i < names.Count; i++)
            for (int j = i + 1; j < names.Count; j++)
            {
                var a = box[names[i]]; var b = box[names[j]];
                if (a.Item1 - ClusterMargin <= b.Item2 && b.Item1 - ClusterMargin <= a.Item2 && a.Item3 - ClusterMargin <= b.Item4 && b.Item3 - ClusterMargin <= a.Item4 &&
                    a.Item5 - ClusterMargin <= b.Item6 && b.Item5 - ClusterMargin <= a.Item6)
                    parent[Find(names[i])] = Find(names[j]);
            }
        return [.. names.GroupBy(Find).Select(g => (IReadOnlyList<string>)[.. g.Order(StringComparer.Ordinal)])
            .OrderByDescending(g => g.Sum(n => Scenery[n].Faces.Count)).ThenBy(g => g[0], StringComparer.Ordinal)];
    }
}

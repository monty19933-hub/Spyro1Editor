using System.Buffers.Binary;

namespace Spyro.Editor.Core.Universal;

public enum UniversalSpecialBehavior { DragonflyEgg, Sign, HighValueGem }

/// <summary>A placeable object recovered from the Japanese release (not present in the USA game).</summary>
public sealed record UniversalSpecialAsset(string Id, string Name, int ClassId, UniversalSpecialBehavior Behavior,
    byte[] Package, byte[] SourceVram, UniversalLevelData SourceData, byte[] RowTemplate)
{
    public string SourceKey { get; init; } = UniversalSpecialCatalog.SourceKey;
}

/// <summary>Dragonfly egg colour variants. Native is the Japanese model's own palette; the others are
/// hue rotations of the complete near/far vertex colour tables.</summary>
public static class UniversalDragonflyEggs
{
    public const int JapaneseEggClass = 0xF5;
    public const int JapaneseSignClass = 0x47;
    public const int JapaneseArtisansDataEntry = 23;
    public const int JapaneseEggRow = 193;
    public const int JapaneseSignRow = 173;

    // USA class IDs unused by every retail level, the shared model table and engine spawns.
    public static readonly (string Id, string Name, int Class, double Hue, double Saturation, double Brightness)[] Variants =
    [
        ("dragonfly-egg-native", "Dragonfly Egg (native colours)", 0x1E9, 0, 1, 1),
        ("dragonfly-egg-blue", "Dragonfly Egg (blue)", 0x1F2, 110, 1, 1),
        ("dragonfly-egg-red", "Dragonfly Egg (red)", 0x1E8, -130, 1, 1),
        ("dragonfly-egg-gold", "Dragonfly Egg (gold)", 0x1EC, -85, 1.1, 1.1),
        ("dragonfly-egg-purple", "Dragonfly Egg (purple)", 0x1F0, 160, 1, 1),
        ("dragonfly-egg-pink", "Dragonfly Egg (pink)", 0x1F9, -165, 0.8, 1.15),
        ("dragonfly-egg-white", "Dragonfly Egg (pearl white)", 0x1D4, 0, 0.1, 1.35),
    ];

    public const int SignClass = 0x1D0;
    public const string SignId = "sign";

    public static bool IsEggClass(int c) => Variants.Any(v => v.Class == c);

    /// <summary>Inventory picture tint for a colour variant: the native egg's colour put through the
    /// variant's own transform, scaled for the pale egg picture (0x80 = unchanged).</summary>
    public static (int R, int G, int B) IconTint(int variant)
    {
        var v = Variants[variant];
        var (r, g, b) = Transform(0x38, 0xD8, 0x38, v.Hue, v.Saturation, v.Brightness);
        static int S(byte c) => Math.Clamp(c * 0xA0 / 0xFF, 0x10, 0xFF);
        return (S(r), S(g), S(b));
    }

    public static int VariantIndex(string id) => Array.FindIndex(Variants, v => v.Id == id);

    /// <summary>The egg as a HUD model (the format of the inventory's spinning chest and dragon, drawn by
    /// 0x80022D1C): byte 0 vertex count, byte 1 face count, +4 positions, +8 colours (unused when normals are
    /// present), +0xC faces, +0x10 one normal per vertex. Positions and normals are signed bytes (x, y, z with
    /// -z up); faces are two words of four 7-bit indices (vertices, then normals; a triangle repeats its third).
    /// Pointers are absolute for <paramref name="address"/>.</summary>
    public static byte[] HudModel(byte[] package, uint address, int height = 64)
    {
        byte[] p = package;
        const int anim = 0x3C, md = 0x70;
        uint W(int o) => BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(o));
        if (W(0) != 1 || W(0x34) != md || W(0x38) != anim || p[anim + 4] != 0)
            throw new InvalidDataException("Unexpected dragonfly egg model layout.");
        var src = UniversalEggEyes.DebugFrames(p)[0];
        int top = src.Max(v => Math.Abs(v.Z));
        double scale = (double)height / top;
        var verts = src.Select(v => ((int)Math.Round(v.X * scale), (int)Math.Round(v.Y * scale), (int)Math.Round(v.Z * scale))).ToList();
        if (verts.Count > 127 || verts.Any(v => Math.Abs(v.Item1) > 127 || Math.Abs(v.Item2) > 127 || Math.Abs(v.Item3) > 127))
            throw new InvalidDataException("The egg does not fit the HUD model format.");
        static (double X, double Y, double Z) Sub((int, int, int) a, (int, int, int) b) => (a.Item1 - b.Item1, a.Item2 - b.Item2, a.Item3 - b.Item3);
        static (double X, double Y, double Z) Cross((double X, double Y, double Z) a, (double X, double Y, double Z) b) =>
            (a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        (double X, double Y, double Z) N(int a, int b, int c) => Cross(Sub(verts[b], verts[a]), Sub(verts[c], verts[a]));

        // Native faces: tri = 8 bytes, quad (bit 31) = 12 bytes with the fourth index in the third word.
        int faces = md + (int)W(anim + 0x14), length = (int)W(faces);
        var polys = new List<int[]>();
        for (int q = faces + 4; q < faces + 4 + length;)
        {
            uint a = W(q); bool quad = (a & 0x80000000) != 0;
            int i0 = (int)(a >> 22 & 511), i1 = (int)(a >> 13 & 511), i2 = (int)(a >> 4 & 511);
            if (quad)
            {
                int i3 = (int)(W(q + 8) >> 2 & 511);
                // GPU quads are drawn as (0,1,2)+(1,2,3): keep the order whose two halves face the same way.
                double z = Dot(N(i0, i1, i2), N(i3, i2, i1)), c = Dot(N(i0, i1, i3), N(i2, i3, i1));
                polys.Add(z >= c ? [i0, i1, i2, i3] : [i0, i1, i3, i2]);
            }
            else polys.Add([i0, i1, i2]);
            q += quad ? 12 : 8;
        }
        if (polys.Count > 255) throw new InvalidDataException("The egg has too many faces for a HUD model.");
        // HUD faces are visible when cross(b-a, c-a) points into the model (inventory arrow and chest convention).
        var centre = (verts.Average(v => v.Item1), verts.Average(v => v.Item2), verts.Average(v => v.Item3));
        (double X, double Y, double Z) FromCentre(int[] f) => (
            (verts[f[0]].Item1 + verts[f[1]].Item1 + verts[f[2]].Item1) / 3.0 - centre.Item1,
            (verts[f[0]].Item2 + verts[f[1]].Item2 + verts[f[2]].Item2) / 3.0 - centre.Item2,
            (verts[f[0]].Item3 + verts[f[1]].Item3 + verts[f[2]].Item3) / 3.0 - centre.Item3);
        double native = polys.Sum(f => Math.Sign(Dot(N(f[0], f[1], f[2]), FromCentre(f))));
        if (native > 0) // native outward winding: mirror every face (a quad keeps its diagonal)
            for (int i = 0; i < polys.Count; i++)
                polys[i] = polys[i].Length == 3 ? [polys[i][0], polys[i][2], polys[i][1]] : [polys[i][0], polys[i][2], polys[i][1], polys[i][3]];

        // Per-corner normals: average of the faces around the vertex that bend less than 60 degrees from this face.
        static (double X, double Y, double Z) Unit((double X, double Y, double Z) v) { double l = Math.Sqrt(Dot(v, v)); return l == 0 ? (0, 0, -1) : (v.X / l, v.Y / l, v.Z / l); }
        var faceNormal = polys.Select(f => { var n = N(f[0], f[1], f[2]); return Unit((-n.X, -n.Y, -n.Z)); }).ToList();
        var around = Enumerable.Range(0, verts.Count).Select(v => Enumerable.Range(0, polys.Count).Where(f => polys[f].Contains(v)).ToList()).ToList();
        var palette = new List<(int, int, int)>();
        var cornerNormals = new List<int[]>();
        foreach (var (f, fi) in polys.Select((f, i) => (f, i)))
        {
            var ids = new int[f.Length];
            for (int k = 0; k < f.Length; k++)
            {
                var sum = (X: 0.0, Y: 0.0, Z: 0.0);
                foreach (int g in around[f[k]])
                    if (Dot(faceNormal[g], faceNormal[fi]) > 0.5) sum = (sum.X + faceNormal[g].X, sum.Y + faceNormal[g].Y, sum.Z + faceNormal[g].Z);
                var u = Unit(sum);
                var q = ((int)Math.Round(u.X * 127), (int)Math.Round(u.Y * 127), (int)Math.Round(u.Z * 127));
                int at = palette.IndexOf(q);
                if (at < 0) { at = palette.Count; palette.Add(q); }
                ids[k] = at;
            }
            cornerNormals.Add(ids);
        }
        if (palette.Count > 127)
        {
            // Merge to the nearest kept normal until the palette fits.
            var keep = palette.Take(127).ToList();
            for (int i = 0; i < cornerNormals.Count; i++)
                for (int k = 0; k < cornerNormals[i].Length; k++)
                    if (cornerNormals[i][k] >= 127)
                    {
                        var q = palette[cornerNormals[i][k]];
                        cornerNormals[i][k] = Enumerable.Range(0, 127).MaxBy(j => keep[j].Item1 * q.Item1 + keep[j].Item2 * q.Item2 + keep[j].Item3 * q.Item3);
                    }
            palette = keep;
        }
        int count = Math.Max(verts.Count, palette.Count);
        while (verts.Count < count) verts.Add(verts[0]);
        while (palette.Count < count) palette.Add((0, 0, -127));

        static int Align(int n) => (n + 3) & ~3;
        int normals = 0x10, positions = Align(normals + 3 * count), colours = Align(positions + 3 * count), faceTable = colours + 4;
        byte[] r = new byte[faceTable + 8 * polys.Count];
        r[0] = (byte)count; r[1] = (byte)polys.Count; r[2] = (byte)Math.Min(127, verts.Max(v => Math.Max(Math.Abs(v.Item1), Math.Max(Math.Abs(v.Item2), Math.Abs(v.Item3))))); r[3] = 0xFF;
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(4), address + (uint)positions);
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(8), address + (uint)colours);
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(12), address + (uint)faceTable);
        for (int i = 0; i < count; i++)
        {
            r[normals + 3 * i] = (byte)(sbyte)palette[i].Item1; r[normals + 3 * i + 1] = (byte)(sbyte)palette[i].Item2; r[normals + 3 * i + 2] = (byte)(sbyte)palette[i].Item3;
            r[positions + 3 * i] = (byte)(sbyte)verts[i].Item1; r[positions + 3 * i + 1] = (byte)(sbyte)verts[i].Item2; r[positions + 3 * i + 2] = (byte)(sbyte)verts[i].Item3;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(colours), 0x007F7F7F);
        static uint Pack(int[] i) => (uint)(i[0] << 23 | i[1] << 16 | i[2] << 9 | (i.Length == 4 ? i[3] : i[2]) << 2);
        for (int f = 0; f < polys.Count; f++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(faceTable + 8 * f), Pack(polys[f]));
            BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(faceTable + 8 * f + 4), Pack(cornerNormals[f]));
        }
        return r;
    }

    /// <summary>Applies a hue/saturation/brightness transform to both 70-entry colour tables of each animation.</summary>
    public static byte[] Recolour(byte[] package, double hue, double saturation, double brightness)
    {
        byte[] p = package.ToArray();
        if (hue == 0 && saturation == 1 && brightness == 1) return p;
        int n = BinaryPrimitives.ReadInt32LittleEndian(p);
        int md = BinaryPrimitives.ReadInt32LittleEndian(p.AsSpan(0x34));
        HashSet<(int, int)> tables = [];
        for (int i = 0; i < n; i++)
        {
            uint raw = BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(0x38 + 4 * i));
            if (raw == uint.MaxValue) continue;
            int count = BinaryPrimitives.ReadInt16LittleEndian(p.AsSpan((int)raw + 2));
            foreach (int field in new[] { 0x18, 0x20 })
            {
                int off = BinaryPrimitives.ReadInt32LittleEndian(p.AsSpan((int)raw + field));
                if (off > 0) tables.Add((md + off, count));
            }
        }
        foreach (var (start, count) in tables)
            for (int k = 0; k < count; k++)
            {
                int o = start + 4 * k;
                if (o + 3 > p.Length) break;
                var (r, g, b) = Transform(p[o], p[o + 1], p[o + 2], hue, saturation, brightness);
                p[o] = r; p[o + 1] = g; p[o + 2] = b;
            }
        return p;
    }

    private static (byte, byte, byte) Transform(byte r8, byte g8, byte b8, double hue, double sat, double bright)
    {
        double r = r8 / 255.0, g = g8 / 255.0, b = b8 / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), l = (max + min) / 2, h = 0, s = 0;
        if (max != min)
        {
            double d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
            h *= 60;
        }
        h = ((h + hue) % 360 + 360) % 360; s = Math.Clamp(s * sat, 0, 1); l = Math.Clamp(l * bright, 0, 1);
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s, pp = 2 * l - q;
        double Hue(double t) { t = (t % 1 + 1) % 1; return t < 1 / 6.0 ? pp + (q - pp) * 6 * t : t < 0.5 ? q : t < 2 / 3.0 ? pp + (q - pp) * (2 / 3.0 - t) * 6 : pp; }
        double hh = h / 360;
        return s == 0 ? (B(l), B(l), B(l)) : (B(Hue(hh + 1 / 3.0)), B(Hue(hh)), B(Hue(hh - 1 / 3.0)));
        static byte B(double v) => (byte)Math.Clamp(Math.Round(v * 255), 0, 255);
    }
}

/// <summary>Reads recovered objects from the unchanged Japanese disc (read only).</summary>
public sealed class UniversalJapaneseSource
{
    public const string OriginalJapanSha256 = "1bcf4353d6a4d4d0db152d2ebd60879ef85516feaf170f364ff419c54f599ea7";
    private readonly UniversalLevelData _artisans;

    public UniversalJapaneseSource(string japaneseImagePath)
    {
        ImagePath = japaneseImagePath;
        var disc = UniversalDiscImage.ReadAny(japaneseImagePath, "SCPS_100.83");
        _artisans = new UniversalLevelData(disc.EntryBytes(UniversalDragonflyEggs.JapaneseArtisansDataEntry));
        if (!_artisans.Registrations().Any(r => r.ClassId == UniversalDragonflyEggs.JapaneseEggClass))
            throw new InvalidDataException("The selected Japanese disc does not contain the native dragonfly egg model.");
    }

    public string ImagePath { get; }

    public static void RequireOriginal(string path)
    {
        using var fs = File.OpenRead(path);
        if (Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(fs)) != OriginalJapanSha256)
            throw new InvalidDataException("Dragonfly eggs and signs are copied from the unchanged original Spyro the Dragon (Japan) disc. Select that disc in the editor settings.");
    }

    public IReadOnlyList<UniversalSpecialAsset> Assets()
    {
        List<UniversalSpecialAsset> list = [];
        byte[] eggPkg = _artisans.Package(UniversalDragonflyEggs.JapaneseEggClass);
        byte[] eggRow = _artisans.Row(UniversalDragonflyEggs.JapaneseEggRow);
        byte[] vram = _artisans.Vram().ToArray();
        foreach (var v in UniversalDragonflyEggs.Variants)
            list.Add(new(v.Id, v.Name, v.Class, UniversalSpecialBehavior.DragonflyEgg,
                UniversalDragonflyEggs.Recolour(eggPkg, v.Hue, v.Saturation, v.Brightness), vram, _artisans, eggRow));
        list.Add(new(UniversalDragonflyEggs.SignId, "Sign", UniversalDragonflyEggs.SignClass, UniversalSpecialBehavior.Sign,
            _artisans.Package(UniversalDragonflyEggs.JapaneseSignClass), vram, _artisans, _artisans.Row(UniversalDragonflyEggs.JapaneseSignRow)));
        return list;
    }

    public UniversalSpecialAsset? Find(string id) => Assets().FirstOrDefault(a => a.Id == id);

    /// <summary>The native egg model package, the source of the inventory column's gold egg (see
    /// <see cref="UniversalDragonflyEggs.HudModel"/>).</summary>
    public byte[] InventoryEggModel() => _artisans.Package(UniversalDragonflyEggs.JapaneseEggClass);
}

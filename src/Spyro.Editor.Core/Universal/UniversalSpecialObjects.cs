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
}

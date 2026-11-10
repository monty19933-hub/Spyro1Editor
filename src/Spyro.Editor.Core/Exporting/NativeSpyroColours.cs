using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Spyro's colours (Game Settings, the whole game): any colour for each part of his body in the games Create BIN
/// makes, in every level, on the title screen and in the cutscenes.
/// <list type="bullet">
/// <item>Spyro's model (the resident player package) draws every face with one of 17 four-bit texture palettes in VRAM
/// (<see cref="Palettes"/>); the final colour is the palette colour times the face's vertex light, which is grey. Every
/// level's VRAM image (subfile 0 of its WAD entry, the right half of VRAM) carries the same 17 palettes at the same places,
/// so recolouring them there recolours Spyro in that level.</item>
/// <item>The palettes are not one per part: several mix body purple with belly tan, wing-root brown or the eyes' whites
/// and pupils. <see cref="Owners"/> says which part each palette colour belongs to (worked out on the emulator by painting
/// palettes and from the faces that use them); the mouth's palette is left alone.</item>
/// <item>A part keeps its shading: each of its colours keeps its lightness and saturation relative to the part's
/// average (weighted by how many texels use it), and that average becomes the chosen colour: a darker choice scales
/// every colour towards black, a lighter one towards white; saturation keeps each colour's difference from the
/// average.</item>
/// <item>The cutscenes and the title screen (WAD entries 3 to 6) draw Spyro with copies of the palettes elsewhere in
/// their VRAM images; every copy there is recoloured. In levels the palettes' own places are, and the copies of a
/// stand-in Spyro (Gnasty Gnorc's level swaps one in for him in a scripted moment) with their distance shades; other
/// objects that share a copy of one of his palettes (life chests, some dragons) keep their colours.</item>
/// </list>
/// Only palettes still in the USA game's colours are changed, so applying the colours again changes nothing.
/// <see cref="ApplyForExport(IDictionary{int, byte[]}, Func{int, byte[]})"/> recolours the game Create BIN makes when
/// the export's scope (<see cref="Use"/>, opened with the project's colours) asks for it.</summary>
public static class NativeSpyroColours
{
    /// <summary>A part of Spyro that takes one colour. <paramref name="Code"/> is its letter in <see cref="Owners"/>.</summary>
    public sealed record Part(char Code, string Key, string Name, string Description);

    public static readonly IReadOnlyList<Part> Parts =
    [
        new('B', "body", "Body", "His head, back, legs, snout and tail"),
        new('Y', "belly", "Belly", "His chest and belly plates, and under his tail"),
        new('H', "horns", "Horns", "The two horns on his head"),
        new('S', "spikes", "Head spikes", "The spiky fins behind his head"),
        new('W', "wings", "Wing frame", "The bones and edges of his wings, where they join his back"),
        new('M', "wingSkin", "Wing skin", "The skin between the wing bones"),
        new('T', "tailTip", "Tail tip", "The arrowhead at the end of his tail"),
        new('E', "eyes", "Eyes", "The whites of his eyes"),
        new('P', "pupils", "Pupils", "The dark middles of his eyes"),
    ];

    public static Part? FindPart(string key) => Parts.FirstOrDefault(p => p.Key == key);

    /// <summary>Spyro's 17 texture palettes (GPU CLUT words: VRAM x = (id &amp; 63) * 16, y = id &gt;&gt; 6), all on texture
    /// page 0x1F's column of palettes.</summary>
    public static readonly ushort[] Palettes =
        [0x6B7D, 0x6BBD, 0x6BFD, 0x787F, 0x78BF, 0x78FF, 0x793F, 0x797F, 0x79BF, 0x79FF, 0x7A3F, 0x7A7F, 0x7ABF, 0x7AFF, 0x7B3F, 0x7B7F, 0x7BBF];

    /// <summary>The part each palette colour belongs to (a letter of <see cref="Parts"/>; '.' none: transparent colour 0,
    /// unused, or the mouth's palette 0x7B7F).</summary>
    private static readonly string[] Owners =
    [
        ".BBBBBBBBBBB....", // 0x6B7D head, legs, flanks
        ".EEBPBEBBEEBPPPB", // 0x6BBD eyes: whites, pupils, lids
        ".YBYBYBYBBYBBYYY", // 0x6BFD chest and belly with the purple around them
        ".WWWWWWWWWWWWWWW", // 0x787F wing bones
        ".MMMMMMMMMMMMMMM", // 0x78BF wing skin
        ".BBBBBBBBBBBBBBB", // 0x78FF back and neck
        ".BBBBBBBBBBBBBBB", // 0x793F snout and jaw
        ".MMMMMMMMMMMMMMM", // 0x797F wing tips
        ".BBBYBBYBBYBBYBB", // 0x79BF sides of the belly
        ".TTTTTTTTTTTTTTT", // 0x79FF tail arrowhead
        ".HHHHHHHHHHHHHHH", // 0x7A3F horns
        ".BBBBBBBBBBBBBBB", // 0x7A7F tail
        ".YBYYBYYBYYYBYYY", // 0x7ABF under the belly and tail
        ".SSSSSSSSSSSSSSS", // 0x7AFF head spikes
        ".WWBWWBBBWWWWBWB", // 0x7B3F wing roots on his back
        "................", // 0x7B7F mouth
        ".YBYBYYBBBYBBYYY", // 0x7BBF chest
    ];

    /// <summary>How many texels of Spyro's faces use each palette colour (from the faces' texture areas): a part's
    /// average is weighted by it, so the colour most of the part shows becomes the chosen one.</summary>
    private static readonly int[][] Weights =
    [
        [0, 38887, 13707, 3472, 3114, 2108, 6533, 2800, 803, 535, 88, 24, 0, 0, 0, 0],
        [0, 42, 718, 1, 181, 65, 65, 92, 82, 60, 42, 133, 45, 25, 38, 90],
        [0, 380, 905, 547, 604, 323, 483, 1307, 512, 2221, 280, 409, 477, 234, 352, 275],
        [0, 2550, 2234, 1332, 610, 638, 1568, 1798, 230, 1148, 1124, 600, 240, 124, 230, 38],
        [0, 425, 20, 399, 86, 309, 195, 388, 351, 268, 144, 183, 139, 79, 101, 81],
        [0, 6483, 362, 2819, 4868, 219, 1033, 2268, 506, 5836, 679, 241, 871, 158, 941, 210],
        [0, 2903, 2333, 1905, 3573, 2379, 2137, 1451, 733, 394, 1388, 462, 1612, 335, 725, 110],
        [0, 158, 48, 128, 22, 122, 100, 116, 30, 0, 92, 72, 62, 50, 24, 32],
        [0, 210, 46, 2464, 54, 204, 974, 78, 148, 346, 50, 56, 196, 68, 198, 188],
        [0, 518, 296, 771, 636, 214, 635, 482, 655, 499, 488, 70, 325, 337, 258, 453],
        [0, 1870, 1572, 1314, 1880, 453, 1474, 1326, 859, 1336, 816, 945, 818, 824, 514, 575],
        [0, 428, 200, 2712, 228, 394, 1024, 628, 144, 394, 348, 432, 186, 176, 660, 144],
        [0, 128, 263, 214, 302, 236, 336, 519, 184, 136, 439, 321, 329, 212, 376, 55],
        [0, 137, 398, 514, 292, 305, 576, 762, 411, 255, 333, 222, 413, 384, 166, 166],
        [0, 58, 56, 586, 56, 98, 136, 312, 170, 162, 86, 48, 106, 54, 90, 30],
        [0, 104, 14, 1588, 32, 356, 32, 52, 218, 30, 100, 22, 14, 20, 42, 38],
        [0, 36, 260, 56, 140, 226, 110, 120, 184, 556, 58, 100, 46, 84, 90, 46],
    ];

    /// <summary>SHA-256 of the USA game's 17 palettes (32 bytes each, in <see cref="Palettes"/> order).</summary>
    private const string OriginalHash = "0c416c7e4bf5d2acbefedb621f06a44bb4075de658b8fdb188e24c2b6db18e17";

    public const int VramBytes = 0x80000, PaletteBytes = 32;

    /// <summary>The WAD entries whose every copy of Spyro's palettes is his (the opening, the title screen and the
    /// cutscenes); in other entries only the palettes' own places are.</summary>
    public static bool IsCutsceneEntry(int wadEntry) => wadEntry is >= 3 and <= 6;

    /// <summary>Byte offset of a palette's own place in a right-half VRAM image.</summary>
    public static int PaletteOffset(ushort palette) => (palette >> 6) * 1024 + ((palette & 63) * 16 - 512) * 2;

    /// <summary>The 17 palettes at their own places in a VRAM image, or null when they are not the USA game's colours
    /// (another game, or already recoloured).</summary>
    public static byte[]? Originals(ReadOnlySpan<byte> vram)
    {
        if (vram.Length < VramBytes) return null;
        byte[] rows = new byte[Palettes.Length * PaletteBytes];
        for (int k = 0; k < Palettes.Length; k++) vram.Slice(PaletteOffset(Palettes[k]), PaletteBytes).CopyTo(rows.AsSpan(k * PaletteBytes));
        return Convert.ToHexStringLower(SHA256.HashData(rows)) == OriginalHash ? rows : null;
    }

    /// <summary>The palettes with the parts in <paramref name="colours"/> (part key -> colour) recoloured; parts not
    /// listed keep their colours.</summary>
    public static byte[] Recolour(ReadOnlySpan<byte> originals, IReadOnlyDictionary<string, (byte R, byte G, byte B)> colours)
    {
        if (originals.Length != Palettes.Length * PaletteBytes) throw new ArgumentException("Spyro has 17 palettes of 32 bytes.");
        byte[] result = originals.ToArray();
        foreach (var (key, target) in colours)
        {
            var part = FindPart(key) ?? throw new InvalidDataException($"Spyro has no part called \"{key}\".");
            var (meanL, meanS) = Average(originals, part.Code);
            var (th, ts, tl) = ToHsl(target.R / 255.0, target.G / 255.0, target.B / 255.0);
            for (int k = 0; k < Palettes.Length; k++)
                for (int i = 0; i < 16; i++)
                {
                    if (Owners[k][i] != part.Code) continue;
                    ushort word = Word(originals, k, i);
                    var (_, s, l) = ToHsl(Channel(word, 0), Channel(word, 5), Channel(word, 10));
                    // The part's average lightness becomes the chosen colour's, every colour moving with it: towards black in
                    // proportion when the choice is darker (so even pure white takes the colour), towards white in
                    // proportion when it is lighter. Saturation keeps each colour's difference from the part's average (a grey
                    // choice stays grey), so white eyes take a colour too.
                    double nl = tl <= meanL ? tl * l / Math.Max(meanL, 1e-6) : 1 - (1 - tl) * (1 - l) / Math.Max(1 - meanL, 1e-6);
                    double ns = ts <= 0.001 ? 0 : Math.Clamp(ts + s - meanS, 0, 1);
                    var (r, g, b) = FromHsl(th, ns, Math.Clamp(nl, 0, 1));
                    int colour = (int)Math.Round(r * 31) | (int)Math.Round(g * 31) << 5 | (int)Math.Round(b * 31) << 10;
                    // 0 is the transparent colour; the game's own darkest blue (1 << 10) stands for black.
                    if (colour == 0) colour = 1 << 10;
                    BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(k * PaletteBytes + 2 * i), (ushort)(colour | word & 0x8000));
                }
        }
        return result;
    }

    /// <summary>Each part's colour in the game (its colours' weighted average), for showing beside the choice.</summary>
    public static IReadOnlyDictionary<string, (byte R, byte G, byte B)> OriginalColours(ReadOnlySpan<byte> originals)
    {
        Dictionary<string, (byte, byte, byte)> result = [];
        foreach (var part in Parts)
        {
            double r = 0, g = 0, b = 0, total = 0;
            for (int k = 0; k < Palettes.Length; k++)
                for (int i = 0; i < 16; i++)
                {
                    if (Owners[k][i] != part.Code) continue;
                    double w = Math.Max(Weights[k][i], 1);
                    ushort word = Word(originals, k, i);
                    r += Channel(word, 0) * w; g += Channel(word, 5) * w; b += Channel(word, 10) * w; total += w;
                }
            result[part.Key] = ((byte)Math.Round(r / total * 255), (byte)Math.Round(g / total * 255), (byte)Math.Round(b / total * 255));
        }
        return result;
    }

    /// <summary>A copy of a WAD entry with Spyro's palettes recoloured (<paramref name="originals"/> -> <paramref
    /// name="recoloured"/>), or null when the entry has no VRAM image or none of his palettes in the original colours.</summary>
    public static byte[]? RecolourEntry(int wadEntry, ReadOnlySpan<byte> entry, ReadOnlySpan<byte> originals, ReadOnlySpan<byte> recoloured)
    {
        int vram = VramOffset(entry);
        if (vram < 0) return null;
        List<(int At, int Palette)> places = [];
        if (IsCutsceneEntry(wadEntry))
        {
            for (int at = 0; at < VramBytes; at += PaletteBytes)
            {
                var row = entry.Slice(vram + at, PaletteBytes);
                for (int k = 0; k < Palettes.Length; k++)
                    if (row.SequenceEqual(originals.Slice(k * PaletteBytes, PaletteBytes))) { places.Add((at, k)); break; }
            }
        }
        else if (Originals(entry.Slice(vram, VramBytes)) != null)
            for (int k = 0; k < Palettes.Length; k++) places.Add((PaletteOffset(Palettes[k]), k));
        if (places.Count == 0) return null;
        byte[] copy = entry.ToArray();
        foreach (var (at, k) in places) recoloured.Slice(k * PaletteBytes, PaletteBytes).CopyTo(copy.AsSpan(vram + at));
        if (!IsCutsceneEntry(wadEntry))
            foreach (var (at, k) in StandInCopies(copy, entry, vram, originals))
                RecolourShaded(copy.AsSpan(vram, VramBytes), entry.Slice(vram, VramBytes), at, recoloured.Slice(k * PaletteBytes, PaletteBytes));
        return copy;
    }

    /// <summary>Where the game keeps a whole Spyro as an ordinary object model: Gnasty Gnorc's level (WAD entry 76), class
    /// 0x19E, the stand-in that level swaps in for him. The editor draws it to show his colours before a game is made.</summary>
    public const int StandInWadEntry = 76, StandInClass = 0x19E;

    /// <summary>The stand-in Spyro's model package and the texture memory to draw it with, in the given colours (none:
    /// his own), from Gnasty Gnorc's level data as it is in the original game; null when the level has no such model.</summary>
    public static (byte[] Package, byte[] Vram)? PreviewModel(byte[] standInLevel, IReadOnlyDictionary<string, (byte R, byte G, byte B)> colours)
    {
        int vram = VramOffset(standInLevel);
        if (vram < 0) return null;
        byte[] level = standInLevel;
        if (colours.Count > 0 && Originals(standInLevel.AsSpan(vram, VramBytes)) is { } originals)
            level = RecolourEntry(StandInWadEntry, standInLevel, originals, Recolour(originals, colours)) ?? standInLevel;
        var data = new Universal.UniversalLevelData(level);
        if (data.Registrations().All(r => r.ClassId != StandInClass)) return null;
        return (data.Package(StandInClass), level.AsSpan(vram, VramBytes).ToArray());
    }

    /// <summary>A model that is Spyro's stand-in uses at least this many of his 17 palettes.</summary>
    private const int StandInPalettes = 14;

    /// <summary>Copies of Spyro's palettes that belong to a stand-in Spyro: an object of the level drawn with (nearly) all
    /// of his palettes, as the one Gnasty Gnorc's level swaps in for him in a scripted moment (class 0x19E, his whole
    /// model with 16 palette copies). Found from the level's models: a model's texture commands (palette id, then the
    /// page word four bytes on) naming copies of at least <see cref="StandInPalettes"/> of his palettes. Objects that
    /// merely share a colour with him (life chests, some dragons) name one or two and are left alone.</summary>
    private static List<(int At, int Palette)> StandInCopies(byte[] level, ReadOnlySpan<byte> original, int vram, ReadOnlySpan<byte> originals)
    {
        Dictionary<ushort, int> copies = [];
        for (int at = 0; at < VramBytes; at += PaletteBytes)
        {
            var row = original.Slice(vram + at, PaletteBytes);
            for (int k = 0; k < Palettes.Length; k++)
                if (at != PaletteOffset(Palettes[k]) && row.SequenceEqual(originals.Slice(k * PaletteBytes, PaletteBytes)))
                {
                    // The GPU palette id of this place: x in 16-pixel steps (the image is VRAM's right half), y.
                    copies[(ushort)(at / 1024 << 6 | (at % 1024 / 2 + 512) / 16)] = k;
                    break;
                }
        }
        List<(int, int)> result = [];
        if (copies.Values.Distinct().Count() < StandInPalettes) return result;
        var data = new Universal.UniversalLevelData(level);
        var files = data.Subfiles();
        if (files.Count < 3) return result;
        var (modelsAt, modelsLength) = files[2];
        int[] roots = [.. data.Registrations().Select(r => r.Root).Where(r => r >= modelsAt && r < modelsAt + modelsLength).Distinct().Order()];
        HashSet<ushort> taken = [];
        for (int i = 0; i < roots.Length; i++)
        {
            int end = i + 1 < roots.Length ? roots[i + 1] : modelsAt + modelsLength;
            HashSet<ushort> used = [];
            for (int p = (roots[i] + 3 & ~3) + 2; p + 6 <= end; p += 4)
            {
                ushort id = BinaryPrimitives.ReadUInt16LittleEndian(original[p..]);
                if (copies.ContainsKey(id) && (BinaryPrimitives.ReadUInt16LittleEndian(original[(p + 4)..]) & 0xFF80) == 0) used.Add(id);
            }
            if (used.Select(id => copies[id]).Distinct().Count() >= StandInPalettes) taken.UnionWith(used);
        }
        foreach (ushort id in taken) result.Add((PaletteOffset(id), copies[id]));
        return result;
    }

    /// <summary>An object's palette in its new colours: the palette itself and, when the seven rows under it are its
    /// distance shades (the object renderer fades a far object through them; each has the palette's transparent
    /// colours), those rows faded as they were. Every shade row is, channel by channel, a straight-line function of the
    /// palette's colours (a fade towards the level's haze), fitted on the old colours and applied to the new.</summary>
    internal static void RecolourShaded(Span<byte> vram, ReadOnlySpan<byte> originalVram, int at, ReadOnlySpan<byte> palette)
    {
        const int Rows = 8, RowBytes = 1024;
        palette.CopyTo(vram[at..]);
        if (at + (Rows - 1) * RowBytes + PaletteBytes > VramBytes) return;
        ushort[,] shades = new ushort[Rows, 16];
        for (int row = 0; row < Rows; row++)
            for (int i = 0; i < 16; i++)
                shades[row, i] = BinaryPrimitives.ReadUInt16LittleEndian(originalVram[(at + row * RowBytes + 2 * i)..]);
        ushort Old(int row, int i) => shades[row, i];
        for (int row = 1; row < Rows; row++)
            for (int i = 0; i < 16; i++)
                if (Old(row, i) == 0 != (Old(0, i) == 0)) return;   // not this palette's shades
        for (int row = 1; row < Rows; row++)
        {
            double[] a = new double[3], b = new double[3];
            for (int c = 0; c < 3; c++)
            {
                double n = 0, sx = 0, sy = 0, sxx = 0, sxy = 0;
                for (int i = 0; i < 16; i++)
                {
                    if (Old(0, i) == 0) continue;
                    double x = Old(0, i) >> 5 * c & 31, y = Old(row, i) >> 5 * c & 31;
                    n++; sx += x; sy += y; sxx += x * x; sxy += x * y;
                }
                double variance = n * sxx - sx * sx;
                a[c] = n >= 2 && variance > 0.5 ? (n * sxy - sx * sy) / variance : 1;
                b[c] = n > 0 ? (sy - a[c] * sx) / n : 0;
            }
            for (int i = 0; i < 16; i++)
            {
                ushort old = Old(row, i);
                ushort near = BinaryPrimitives.ReadUInt16LittleEndian(palette[(2 * i)..]);
                if (old == 0 || near == Old(0, i)) continue;   // transparent, or a colour that stays: its shades stay
                int colour = 0;
                for (int c = 0; c < 3; c++)
                    colour |= Math.Clamp((int)Math.Round(a[c] * (near >> 5 * c & 31) + b[c]), 0, 31) << 5 * c;
                if (colour == 0) colour = 1 << 10;
                BinaryPrimitives.WriteUInt16LittleEndian(vram[(at + row * RowBytes + 2 * i)..], (ushort)(colour | old & 0x8000));
            }
        }
    }

    /// <summary>Where an entry's right-half VRAM image starts (its first subfile, at 0x800 in level and cutscene
    /// entries), or -1.</summary>
    private static int VramOffset(ReadOnlySpan<byte> entry)
    {
        if (entry.Length < 0x800 + VramBytes) return -1;
        int offset = BinaryPrimitives.ReadInt32LittleEndian(entry), length = BinaryPrimitives.ReadInt32LittleEndian(entry[4..]);
        return offset == 0x800 && length >= VramBytes && (long)offset + VramBytes <= entry.Length ? offset : -1;
    }

    /// <summary>The 17 original palettes from the first WAD entry that still has them at their own places, or null.</summary>
    public static byte[]? FindOriginals(Func<int, byte[]> entryBytes)
    {
        for (int entry = 0; entry < 256; entry++)
        {
            byte[] bytes = entryBytes(entry);
            int vram = VramOffset(bytes);
            if (vram >= 0 && !IsCutsceneEntry(entry) && Originals(bytes.AsSpan(vram, VramBytes)) is { } rows) return rows;
        }
        return null;
    }

    // ---- Create BIN's choice ----
    private static Scope? s_scope;
    private static readonly object ScopeLock = new();

    /// <summary>Whether the game Create BIN is making gets new colours from Game Settings: Spyro's, or the egg
    /// thieves' (<see cref="NativeEggThiefColours"/>, applied by the same export steps).</summary>
    public static bool Enabled => SpyroEnabled || NativeEggThiefColours.Enabled;
    /// <summary>Whether Spyro himself gets new colours.</summary>
    public static bool SpyroEnabled => Volatile.Read(ref s_scope)?.Colours.Count > 0;

    /// <summary>Create BIN's colours until the scope is disposed (scopes nest; none or an empty set: his own).</summary>
    public static Scope Use(IReadOnlyDictionary<string, (byte R, byte G, byte B)>? colours)
    {
        foreach (string key in colours?.Keys ?? [])
            if (FindPart(key) == null) throw new InvalidDataException($"Spyro has no part called \"{key}\".");
        lock (ScopeLock)
        {
            var scope = new Scope(colours ?? new Dictionary<string, (byte, byte, byte)>(), s_scope);
            Volatile.Write(ref s_scope, scope);
            return scope;
        }
    }

    /// <summary>The export's last step before its WAD is written: every entry the colours change is added to
    /// <paramref name="replacements"/> (a replaced entry is recoloured in place of its replacement). Returns how many
    /// entries changed.</summary>
    public static int ApplyForExport(IDictionary<int, byte[]> replacements, Func<int, byte[]> entryBytes)
    {
        int thieves = NativeEggThiefColours.ApplyForExport(replacements, entryBytes);
        var colours = Volatile.Read(ref s_scope)?.Colours;
        if (colours is not { Count: > 0 }) return thieves;
        byte[] Current(int entry) => replacements.TryGetValue(entry, out var bytes) ? bytes : entryBytes(entry);
        byte[]? originals = FindOriginals(Current);
        if (originals == null) return thieves; // nothing left in the original colours: already applied
        byte[] recoloured = Recolour(originals, colours);
        int changed = thieves;
        for (int entry = 0; entry < 256; entry++)
            if (RecolourEntry(entry, Current(entry), originals, recoloured) is { } copy) { replacements[entry] = copy; changed++; }
        return changed;
    }

    /// <summary>Recolours only these entries (levels an export adds to a game whose other entries already have the
    /// colours, as the new world's). Returns how many changed.</summary>
    public static int ApplyToEntries(IDictionary<int, byte[]> entries)
    {
        int thieves = NativeEggThiefColours.ApplyToEntries(entries);
        var colours = Volatile.Read(ref s_scope)?.Colours;
        if (colours is not { Count: > 0 }) return thieves;
        byte[]? originals = null;
        foreach (var (entry, bytes) in entries)
        {
            int vram = VramOffset(bytes);
            if (vram >= 0 && !IsCutsceneEntry(entry) && Originals(bytes.AsSpan(vram, VramBytes)) is { } rows) { originals = rows; break; }
        }
        if (originals == null) return thieves;
        byte[] recoloured = Recolour(originals, colours);
        int changed = thieves;
        foreach (int entry in entries.Keys.ToArray())
            if (RecolourEntry(entry, entries[entry], originals, recoloured) is { } copy) { entries[entry] = copy; changed++; }
        return changed;
    }

    /// <summary>The same for a complete WAD.WAD, in place (entries keep their places and lengths).</summary>
    public static int ApplyForExport(byte[] wad)
    {
        int thieves = NativeEggThiefColours.ApplyForExport(wad);
        var colours = Volatile.Read(ref s_scope)?.Colours;
        if (colours is not { Count: > 0 }) return thieves;
        (int Offset, int Length) Entry(int i) => (BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(i * 8)), BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(i * 8 + 4)));
        byte[] Bytes(int i)
        {
            var (o, l) = Entry(i);
            return l <= 0 || o < 0 || (long)o + l > wad.Length ? [] : wad.AsSpan(o, l).ToArray();
        }
        byte[]? originals = FindOriginals(Bytes);
        if (originals == null) return thieves;
        byte[] recoloured = Recolour(originals, colours);
        int changed = thieves;
        for (int entry = 0; entry < 256; entry++)
            if (RecolourEntry(entry, Bytes(entry), originals, recoloured) is { } copy) { copy.CopyTo(wad, Entry(entry).Offset); changed++; }
        return changed;
    }

    public sealed class Scope : IDisposable
    {
        private readonly Scope? _previous;
        private bool _disposed;
        internal Scope(IReadOnlyDictionary<string, (byte R, byte G, byte B)> colours, Scope? previous)
        { Colours = new Dictionary<string, (byte R, byte G, byte B)>(colours); _previous = previous; }
        public IReadOnlyDictionary<string, (byte R, byte G, byte B)> Colours { get; }
        private readonly List<IDisposable> _with = [];
        /// <summary>Another Game Settings scope that lasts as long as this one (the egg thieves' colour).</summary>
        public Scope With(IDisposable other) { _with.Add(other); return this; }
        public void Dispose()
        {
            lock (ScopeLock)
            {
                if (_disposed) return;
                _disposed = true;
                if (ReferenceEquals(Volatile.Read(ref s_scope), this)) Volatile.Write(ref s_scope, _previous);
            }
            foreach (var other in _with) other.Dispose();
        }
    }

    // ---- colour arithmetic ----
    private static ushort Word(ReadOnlySpan<byte> rows, int palette, int index) =>
        BinaryPrimitives.ReadUInt16LittleEndian(rows[(palette * PaletteBytes + 2 * index)..]);
    private static double Channel(ushort word, int shift) => (word >> shift & 31) / 31.0;

    private static (double L, double S) Average(ReadOnlySpan<byte> rows, char code)
    {
        double l = 0, s = 0, total = 0;
        for (int k = 0; k < Palettes.Length; k++)
            for (int i = 0; i < 16; i++)
            {
                if (Owners[k][i] != code) continue;
                double w = Math.Max(Weights[k][i], 1);
                ushort word = Word(rows, k, i);
                var (_, hs, hl) = ToHsl(Channel(word, 0), Channel(word, 5), Channel(word, 10));
                l += hl * w; s += hs * w; total += w;
            }
        return total == 0 ? (0.5, 0) : (l / total, s / total);
    }

    /// <summary>RGB (0..1) to hue (0..1), saturation and lightness, as Python's colorsys.rgb_to_hls.</summary>
    public static (double H, double S, double L) ToHsl(double r, double g, double b)
    {
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), l = (max + min) / 2;
        if (max == min) return (0, 0, l);
        double d = max - min, s = l <= 0.5 ? d / (max + min) : d / (2 - max - min);
        double h = max == r ? (g - b) / d : max == g ? 2 + (b - r) / d : 4 + (r - g) / d;
        h /= 6;
        if (h < 0) h += 1;
        return (h, s, l);
    }

    /// <summary>Hue, saturation, lightness (0..1) to RGB (0..1).</summary>
    public static (double R, double G, double B) FromHsl(double h, double s, double l)
    {
        if (s == 0) return (l, l, l);
        double m2 = l <= 0.5 ? l * (1 + s) : l + s - l * s, m1 = 2 * l - m2;
        static double V(double m1, double m2, double hue)
        {
            hue = (hue % 1 + 1) % 1;
            if (hue < 1 / 6.0) return m1 + (m2 - m1) * hue * 6;
            if (hue < 0.5) return m2;
            if (hue < 2 / 3.0) return m1 + (m2 - m1) * (2 / 3.0 - hue) * 6;
            return m1;
        }
        return (V(m1, m2, h + 1 / 3.0), V(m1, m2, h), V(m1, m2, h - 1 / 3.0));
    }
}

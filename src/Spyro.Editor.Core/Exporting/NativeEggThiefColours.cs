using System.Buffers.Binary;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>The egg thieves' colour (Game Settings, the whole game): any colour in place of their blue in the games
/// Create BIN makes.
/// <para>A thief (class 0x21, nine of the game's levels) is 78 faces, 76 of them textured; his shading is grey, so his
/// look is his texture palettes, which each level keeps in its own texture memory: one of sixteen blues for his whole
/// robed body (73 faces), two of greys for his eyes, and under each the seven dimmer rows the game fades a far object
/// through. The blues are recoloured towards the chosen colour keeping their light and shade (as Spyro's colours
/// are, <see cref="NativeSpyroColours"/>); the eyes stay. The thief's model is found by what it is, not by its class
/// number, so thieves added to levels the editor built (where classes are renumbered) are recoloured too.</para></summary>
public static class NativeEggThiefColours
{
    public const int ThiefClass = 0x21;

    /// <summary>The thief's shading table: 22 greys, the same in every level.</summary>
    private static readonly uint[] Shading =
    [
        0x7F7F7F, 0x777777, 0x707070, 0x7F7F7F, 0x494949, 0x7F7F7F, 0x7F7F7F, 0x515151, 0x8E8E8E, 0x7F7F7F, 0x7F7F7F,
        0x000000, 0xBFBFBF, 0x3A3A3A, 0x3A3A3A, 0x3A3A3A, 0x3A3A3A, 0x3A3A3A, 0x4C4C4C, 0x6B6B6B, 0x4C4C4C, 0xADADAD,
    ];
    private const int PlainFaces = 2, TexturedFaces = 76;

    /// <summary>The palettes (GPU CLUT ids, with how many faces use each) of a thief's model, or null when the model
    /// is not an egg thief's.</summary>
    public static IReadOnlyDictionary<ushort, int>? Palettes(ReadOnlySpan<byte> package)
    {
        if (package.Length < 0x60 || U32(package, 0) != 5) return null;
        int md = (int)U32(package, 0x34), animation = (int)U32(package, 0x38);
        if (md <= 0 || animation <= 0 || animation + 0x2C > package.Length || BinaryPrimitives.ReadUInt16LittleEndian(package[(animation + 2)..]) != Shading.Length) return null;
        long colours = md + (long)U32(package, animation + 0x18), table = md + (long)U32(package, animation + 0x14);
        if (colours < 0 || colours + 4 * Shading.Length > package.Length || table < 0 || table + 8 > package.Length) return null;
        for (int k = 0; k < Shading.Length; k++)
            if ((U32(package, (int)colours + 4 * k) & 0xFFFFFF) != Shading[k]) return null;
        long end = table + 4 + U32(package, (int)table);
        if (end > package.Length) return null;
        Dictionary<ushort, int> palettes = [];
        int plain = 0, textured = 0;
        for (int p = (int)table + 4; p + 8 <= end;)
        {
            uint word = U32(package, p);
            bool quad = (word & 0x80000000) != 0, texture = (word & 2) != 0 || quad && (word & 4) != 0;
            int bytes = quad ? texture ? 24 : 12 : texture ? 20 : 8;
            if (p + bytes > end) return null;
            if (texture)
            {
                int t = p + (quad ? 12 : 8);
                ushort palette = BinaryPrimitives.ReadUInt16LittleEndian(package[(t + 2)..]), page = BinaryPrimitives.ReadUInt16LittleEndian(package[(t + 6)..]);
                // Sixteen-colour textures only (the thief's all are).
                if ((page >> 7 & 3) == 0) palettes[palette] = palettes.GetValueOrDefault(palette) + 1;
                textured++;
            }
            else plain++;
            p += bytes;
        }
        return plain == PlainFaces && textured == TexturedFaces && palettes.Count > 0 ? palettes : null;
    }

    /// <summary>Where a level's thief models are (start and end in the level's bytes), by what they are.</summary>
    private static List<(int Start, int End)> Models(byte[] level)
    {
        List<(int, int)> found = [];
        try
        {
            var data = new UniversalLevelData(level);
            var files = data.Subfiles();
            if (files.Count < 3) return found;
            var (modelsAt, modelsLength) = files[2];
            int[] roots = [.. data.Registrations().Select(r => r.Root).Where(r => r >= modelsAt && r < modelsAt + modelsLength).Distinct().Order()];
            for (int i = 0; i < roots.Length; i++)
            {
                int end = Math.Min(level.Length, i + 1 < roots.Length ? roots[i + 1] : modelsAt + modelsLength);
                if (end > roots[i] && Palettes(level.AsSpan(roots[i], end - roots[i])) != null) found.Add((roots[i], end));
            }
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException or OverflowException) { }
        return found;
    }

    private static int VramOffset(ReadOnlySpan<byte> entry)
    {
        if (entry.Length < 0x800 + NativeSpyroColours.VramBytes) return -1;
        int offset = BinaryPrimitives.ReadInt32LittleEndian(entry), length = BinaryPrimitives.ReadInt32LittleEndian(entry[4..]);
        return offset == 0x800 && length >= NativeSpyroColours.VramBytes && (long)offset + NativeSpyroColours.VramBytes <= entry.Length ? offset : -1;
    }

    /// <summary>The thief palettes of a level that hold his colour: where each is in the level's bytes, with the
    /// colours of it that are his blue (the saturated ones about the hue most of them share).</summary>
    private static List<(int At, bool[] His)> Robes(byte[] level)
    {
        List<(int, bool[])> robes = [];
        int vram = VramOffset(level);
        if (vram < 0) return robes;
        HashSet<ushort> seen = [];
        foreach (var (start, end) in Models(level))
            foreach (var (palette, faces) in Palettes(level.AsSpan(start, end - start))!.OrderByDescending(p => p.Value).ThenBy(p => p.Key))
            {
                int x = (palette & 63) * 16, y = palette >> 6;
                if (x < 512 || y >= 512 || !seen.Add(palette)) continue;
                int at = vram + NativeSpyroColours.PaletteOffset(palette);
                var words = Enumerable.Range(0, 16).Select(i => BinaryPrimitives.ReadUInt16LittleEndian(level.AsSpan(at + 2 * i))).ToArray();
                var hsl = words.Select(Hsl).ToArray();
                // A colour that is a colour (not a grey, a white or a near black): its strongest and weakest channels well apart.
                bool[] vivid = [.. words.Select(w => (w & 0x7FFF) != 0 && Math.Max(w & 31, Math.Max(w >> 5 & 31, w >> 10 & 31)) - Math.Min(w & 31, Math.Min(w >> 5 & 31, w >> 10 & 31)) >= 5)];
                if (vivid.Count(v => v) < 8) continue;   // his eyes: greys and whites
                // The hue most of them share (hues are a circle: by their mean direction).
                double sx = Enumerable.Range(0, 16).Where(i => vivid[i]).Sum(i => Math.Cos(hsl[i]!.Value.H * 2 * Math.PI)), sy = Enumerable.Range(0, 16).Where(i => vivid[i]).Sum(i => Math.Sin(hsl[i]!.Value.H * 2 * Math.PI));
                double hue = (Math.Atan2(sy, sx) / (2 * Math.PI) + 1) % 1;
                bool[] his = [.. Enumerable.Range(0, 16).Select(i => vivid[i] && Math.Min(Math.Abs(hsl[i]!.Value.H - hue), 1 - Math.Abs(hsl[i]!.Value.H - hue)) <= 0.14)];
                if (his.Count(b => b) >= 8) robes.Add((at, his));
            }
        return robes;
    }

    private static (double H, double S, double L)? Hsl(ushort word) =>
        (word & 0x7FFF) == 0 ? null : NativeSpyroColours.ToHsl((word & 31) / 31.0, (word >> 5 & 31) / 31.0, (word >> 10 & 31) / 31.0);

    /// <summary>The thieves' own colour in a level (the average of their blues), or null when the level has no thief.</summary>
    public static (byte R, byte G, byte B)? OriginalColour(byte[] level)
    {
        double r = 0, g = 0, b = 0; int n = 0;
        foreach (var (at, his) in Robes(level))
            for (int i = 0; i < 16; i++)
            {
                if (!his[i]) continue;
                ushort word = BinaryPrimitives.ReadUInt16LittleEndian(level.AsSpan(at + 2 * i));
                r += word & 31; g += word >> 5 & 31; b += word >> 10 & 31; n++;
            }
        return n == 0 ? null : ((byte)Math.Round(r / n * 255 / 31), (byte)Math.Round(g / n * 255 / 31), (byte)Math.Round(b / n * 255 / 31));
    }

    /// <summary>A copy of a level (a WAD entry) with its egg thieves in <paramref name="colour"/>, or null when it has
    /// none or they are that colour already.</summary>
    public static byte[]? Recolour(byte[] level, (byte R, byte G, byte B) colour)
    {
        var robes = Robes(level);
        if (robes.Count == 0) return null;
        int vram = VramOffset(level);
        byte[] copy = (byte[])level.Clone();
        var (th, ts, tl) = NativeSpyroColours.ToHsl(colour.R / 255.0, colour.G / 255.0, colour.B / 255.0);
        bool changed = false;
        foreach (var (at, his) in robes)
        {
            var hsl = Enumerable.Range(0, 16).Select(i => Hsl(BinaryPrimitives.ReadUInt16LittleEndian(level.AsSpan(at + 2 * i)))).ToArray();
            double meanL = Enumerable.Range(0, 16).Where(i => his[i]).Average(i => hsl[i]!.Value.L), meanS = Enumerable.Range(0, 16).Where(i => his[i]).Average(i => hsl[i]!.Value.S);
            byte[] palette = level.AsSpan(at, NativeSpyroColours.PaletteBytes).ToArray();
            // Already that colour (a game made with it and built on again): left as it is.
            double Mean(int shift) => Enumerable.Range(0, 16).Where(i => his[i]).Average(i => BinaryPrimitives.ReadUInt16LittleEndian(level.AsSpan(at + 2 * i)) >> shift & 31);
            if (Math.Abs(Mean(0) - colour.R * 31 / 255.0) + Math.Abs(Mean(5) - colour.G * 31 / 255.0) + Math.Abs(Mean(10) - colour.B * 31 / 255.0) < 1.6) continue;
            for (int i = 0; i < 16; i++)
            {
                if (!his[i]) continue;
                var (_, s, l) = hsl[i]!.Value;
                // His light and shade kept: a darker colour draws every blue towards black, a lighter one towards white.
                double light = tl <= meanL ? l * tl / Math.Max(meanL, 1e-6) : 1 - (1 - l) * (1 - tl) / Math.Max(1 - meanL, 1e-6);
                double rich = ts < 0.06 ? ts : Math.Clamp(ts + (s - meanS), 0.02, 1);
                var (r, g, b) = NativeSpyroColours.FromHsl(th, rich, Math.Clamp(light, 0, 1));
                ushort old = BinaryPrimitives.ReadUInt16LittleEndian(level.AsSpan(at + 2 * i));
                int word = (int)Math.Round(r * 31) | (int)Math.Round(g * 31) << 5 | (int)Math.Round(b * 31) << 10;
                if (word == 0) word = 1 << 10;
                BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(2 * i), (ushort)(word | old & 0x8000));
            }
            if (palette.AsSpan().SequenceEqual(level.AsSpan(at, NativeSpyroColours.PaletteBytes))) continue;
            // The palette and the seven dimmer rows under it.
            NativeSpyroColours.RecolourShaded(copy.AsSpan(vram, NativeSpyroColours.VramBytes), level.AsSpan(vram, NativeSpyroColours.VramBytes), at - vram, palette);
            changed = true;
        }
        return changed ? copy : null;
    }

    /// <summary>A thief's model and the texture memory to draw him with in <paramref name="colour"/> (null: his own),
    /// from a level that has one; null when it has none.</summary>
    public static (byte[] Package, byte[] Vram)? PreviewModel(byte[] level, (byte R, byte G, byte B)? colour)
    {
        var models = Models(level);
        int vram = VramOffset(level);
        if (models.Count == 0 || vram < 0) return null;
        byte[] drawn = colour is { } c ? Recolour(level, c) ?? level : level;
        return (level.AsSpan(models[0].Start, models[0].End - models[0].Start).ToArray(), drawn.AsSpan(vram, NativeSpyroColours.VramBytes).ToArray());
    }

    // ---- Create BIN's choice ----
    private static Scope? s_scope;
    private static readonly object ScopeLock = new();

    /// <summary>Whether the game Create BIN is making gets another colour for its egg thieves.</summary>
    public static bool Enabled => Volatile.Read(ref s_scope)?.Colour != null;

    /// <summary>Create BIN's colour until the scope is disposed (scopes nest; null: their own blue).</summary>
    public static Scope Use((byte R, byte G, byte B)? colour)
    {
        lock (ScopeLock)
        {
            var scope = new Scope(colour, s_scope);
            Volatile.Write(ref s_scope, scope);
            return scope;
        }
    }

    /// <summary>The export's last step (with Spyro's colours): every level whose thieves change is added to
    /// <paramref name="replacements"/>. Returns how many levels changed.</summary>
    public static int ApplyForExport(IDictionary<int, byte[]> replacements, Func<int, byte[]> entryBytes)
    {
        if (Volatile.Read(ref s_scope)?.Colour is not { } colour) return 0;
        int changed = 0;
        for (int entry = 0; entry < 256; entry++)
        {
            byte[] bytes = replacements.TryGetValue(entry, out var replaced) ? replaced : entryBytes(entry);
            if (Recolour(bytes, colour) is { } copy) { replacements[entry] = copy; changed++; }
        }
        return changed;
    }

    /// <summary>Recolours only these entries (levels an export adds, as the new world's).</summary>
    public static int ApplyToEntries(IDictionary<int, byte[]> entries)
    {
        if (Volatile.Read(ref s_scope)?.Colour is not { } colour) return 0;
        int changed = 0;
        foreach (int entry in entries.Keys.ToArray())
            if (Recolour(entries[entry], colour) is { } copy) { entries[entry] = copy; changed++; }
        return changed;
    }

    /// <summary>The same for a complete WAD.WAD, in place (entries keep their places and lengths).</summary>
    public static int ApplyForExport(byte[] wad)
    {
        if (Volatile.Read(ref s_scope)?.Colour is not { } colour) return 0;
        int changed = 0;
        for (int entry = 0; entry < 256; entry++)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(entry * 8)), length = BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(entry * 8 + 4));
            if (length <= 0 || offset < 0 || (long)offset + length > wad.Length) continue;
            if (Recolour(wad.AsSpan(offset, length).ToArray(), colour) is { } copy) { copy.CopyTo(wad, offset); changed++; }
        }
        return changed;
    }

    public sealed class Scope : IDisposable
    {
        private readonly Scope? _previous;
        private bool _disposed;
        internal Scope((byte R, byte G, byte B)? colour, Scope? previous) { Colour = colour; _previous = previous; }
        public (byte R, byte G, byte B)? Colour { get; }
        public void Dispose()
        {
            lock (ScopeLock)
            {
                if (_disposed) return;
                _disposed = true;
                if (ReferenceEquals(Volatile.Read(ref s_scope), this)) Volatile.Write(ref s_scope, _previous);
            }
        }
    }

    private static uint U32(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
}

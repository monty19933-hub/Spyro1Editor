using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Creatures' colours, level by level (Game Settings, this level): any colour for a kind of creature in one
/// level of the games Create BIN makes, so the same gnorc can be red in one level and blue in the next.
/// <para>A creature's look is its model's own colours (the colour table its untextured faces are drawn in) and the
/// palettes of its textures, which every level keeps for itself (the model in the level's data, the palettes in the
/// level's texture memory, each with the seven dimmer rows the game fades a far object through). Of those colours the
/// ones about the hue most of the creature has are "its colour": they are moved to the chosen colour keeping their light
/// and shade (as Spyro's and the egg thieves' are, <see cref="NativeSpyroColours"/>); skin, metal, eyes and whatever
/// else is of another hue stay. A model is found by its shape (<see cref="Signature"/>), not its class number, so
/// creatures copied into levels the editor built, where classes are renumbered and textures moved, are found too.</para>
/// <para>Palettes Spyro's own textures use are never touched. Two kinds of a level that share a palette share its
/// colour: it takes the colour of the first of them that has one.</para></summary>
public static class NativeCreatureColours
{
    /// <summary>What a model is, whatever its class number and wherever its textures are: its animation count, its first
    /// animation's vertex, colour and face counts, and a hash of its faces' corners.</summary>
    public sealed record Signature(int Animations, int Vertices, int Colours, int PlainFaces, int TexturedFaces, string Faces)
    {
        /// <summary>A short name for settings files and lists.</summary>
        public string Id => $"{Animations:x}-{Vertices:x}-{Colours:x}-{PlainFaces:x}-{TexturedFaces:x}-{Faces[..16]}";
    }

    /// <summary>A kind of creature in a level: its class there, what it is, its own colour (the average of the colours
    /// that are "its colour") and how much of it that colour covers (face corners and palette colours).</summary>
    public sealed record Kind(int ClassId, Signature Model, (byte R, byte G, byte B) Colour, int Weight);

    private const double Band = 0.14;           // of the hue circle, either side of the creature's hue
    private const int MinimumWeight = 6;

    private sealed record Face(bool Textured, int[] Colours, ushort Palette, ushort Page);
    private sealed record Model(int Animations, int Vertices, int ColourCount, List<Face> Faces, List<(int Table, int Count)> ColourTables, string FaceHash)
    {
        public Signature Signature => new(Animations, Vertices, ColourCount, Faces.Count(f => !f.Textured), Faces.Count(f => f.Textured), FaceHash);
    }

    /// <summary>An animated model's faces and colour tables, or null when the package is not one this reads (a simple
    /// model such as a gem's, or an unknown layout).</summary>
    private static Model? Read(ReadOnlySpan<byte> package)
    {
        if (package.Length < 0x60) return null;
        int animations = BinaryPrimitives.ReadInt32LittleEndian(package);
        if (animations is <= 0 or > 1024 || 0x38 + 4 * animations > package.Length) return null;
        int md = (int)U32(package, 0x34);
        if (md <= 0 || md >= package.Length) return null;
        Model? model = null;
        List<(int, int)> tables = [];
        for (int n = 0; n < animations; n++)
        {
            int animation = (int)U32(package, 0x38 + 4 * n);
            if (animation <= 0 || animation + 0x2C > package.Length) continue;
            int count = BinaryPrimitives.ReadUInt16LittleEndian(package[(animation + 2)..]);
            long colours = md + (long)U32(package, animation + 0x18), table = md + (long)U32(package, animation + 0x14);
            if (count is <= 0 or > 511 || colours <= 0 || colours + 4L * count > package.Length || table <= 0 || table + 8 > package.Length) continue;
            if (!tables.Contains(((int)colours, count))) tables.Add(((int)colours, count));
            if (model != null) continue;
            long end = table + 4 + U32(package, (int)table);
            if (end > package.Length || end <= table + 4) return null;
            List<Face> faces = [];
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            Span<byte> word4 = stackalloc byte[4];
            for (int p = (int)table + 4; p < end;)
            {
                if (p + 8 > end) return null;
                uint word = U32(package, p), colour = U32(package, p + 4);
                bool quad = (word & 0x80000000) != 0, textured = (word & 2) != 0 || quad && (word & 4) != 0;
                int bytes = quad ? textured ? 24 : 12 : textured ? 20 : 8;
                if (p + bytes > end) return null;
                List<int> corners = [(int)(colour >> 19 & 511), (int)(colour >> 10 & 511), (int)(colour >> 1 & 511)];
                uint fourth = 0;
                if (quad) { fourth = U32(package, p + 8); corners.Add((int)(fourth >> 11 & 511)); }
                if (corners.Any(c => c >= count)) return null;
                ushort palette = 0, page = 0;
                if (textured)
                {
                    int t = p + (quad ? 12 : 8);
                    palette = BinaryPrimitives.ReadUInt16LittleEndian(package[(t + 2)..]); page = BinaryPrimitives.ReadUInt16LittleEndian(package[(t + 6)..]);
                }
                faces.Add(new(textured, [.. corners], palette, page));
                // The shape: which corners and which colours each face joins (not where its texture is).
                BinaryPrimitives.WriteUInt32LittleEndian(word4, word); hash.AppendData(word4);
                BinaryPrimitives.WriteUInt32LittleEndian(word4, colour); hash.AppendData(word4);
                BinaryPrimitives.WriteUInt32LittleEndian(word4, fourth); hash.AppendData(word4);
                p += bytes;
            }
            if (faces.Count == 0) return null;
            model = new(animations, package[animation + 8], count, faces, tables, Convert.ToHexStringLower(hash.GetHashAndReset()));
        }
        return model;
    }

    /// <summary>What a model package is (null: not a creature's animated model).</summary>
    public static Signature? SignatureOf(ReadOnlySpan<byte> package) => Read(package)?.Signature;

    /// <summary>The level's models: class, where the package is, and the model.</summary>
    private static List<(int ClassId, int Start, int End, Model Model)> Models(byte[] level)
    {
        List<(int, int, int, Model)> found = [];
        try
        {
            var data = new UniversalLevelData(level);
            var files = data.Subfiles();
            if (files.Count < 3) return found;
            var (modelsAt, modelsLength) = files[2];
            var registrations = data.Registrations().Where(r => r.Root >= modelsAt && r.Root < modelsAt + modelsLength).GroupBy(r => r.Root).Select(g => g.First()).OrderBy(r => r.Root).ToArray();
            for (int i = 0; i < registrations.Length; i++)
            {
                int start = registrations[i].Root, end = Math.Min(level.Length, i + 1 < registrations.Length ? registrations[i + 1].Root : modelsAt + modelsLength);
                if (end > start && Read(level.AsSpan(start, end - start)) is { } model) found.Add((registrations[i].ClassId, start, end, model));
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

    /// <summary>One colour of a creature: where it is (a 4-byte entry of a model's colour table, or a 2-byte colour of a
    /// palette in the level's texture memory), what it is, and how much of the creature it covers.</summary>
    private sealed record Sample(int At, bool Palette, int PaletteAt, double R, double G, double B, double Weight)
    {
        public (double H, double S, double L) Hsl => NativeSpyroColours.ToHsl(R, G, B);
        /// <summary>A colour that is a colour (not a grey, a white or a near black): its strongest and weakest channels
        /// apart by a fifth of the strongest and by something to see (dark shades of a colour count).</summary>
        public bool Vivid
        {
            get
            {
                double high = Math.Max(R, Math.Max(G, B)), low = Math.Min(R, Math.Min(G, B));
                return high - low >= 2 / 31.0 && high - low >= 0.22 * high;
            }
        }
    }

    /// <summary>A model's colours in a level: the colour-table entries its untextured faces use and the colours of its
    /// textures' sixteen-colour palettes (not Spyro's).</summary>
    private static List<Sample> Samples(byte[] level, int start, Model model, int vram)
    {
        List<Sample> samples = [];
        // Untextured faces: their corners' entries, in every colour table the model's animations have.
        var plain = model.Faces.Where(f => !f.Textured).SelectMany(f => f.Colours).GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        foreach (var (table, count) in model.ColourTables)
            foreach (var (index, uses) in plain)
            {
                if (index >= count) continue;
                int at = start + table + 4 * index;
                if (at + 4 > level.Length) continue;
                samples.Add(new(at, false, 0, level[at] / 255.0, level[at + 1] / 255.0, level[at + 2] / 255.0, uses / (double)model.ColourTables.Count));
            }
        if (vram < 0) return samples;
        foreach (var group in model.Faces.Where(f => f.Textured && (f.Page >> 7 & 3) == 0).GroupBy(f => f.Palette))
        {
            ushort palette = group.Key;
            int x = (palette & 63) * 16, y = palette >> 6;
            if (x < 512 || y >= 512 || NativeSpyroColours.Palettes.Contains(palette)) continue;
            int at = vram + NativeSpyroColours.PaletteOffset(palette);
            if (at + NativeSpyroColours.PaletteBytes > level.Length) continue;
            for (int i = 0; i < 16; i++)
            {
                ushort word = BinaryPrimitives.ReadUInt16LittleEndian(level.AsSpan(at + 2 * i));
                if ((word & 0x7FFF) == 0) continue;
                // Three corners' worth for every face the palette colours, shared among its colours.
                samples.Add(new(at + 2 * i, true, at, (word & 31) / 31.0, (word >> 5 & 31) / 31.0, (word >> 10 & 31) / 31.0, group.Count() * 3 / 16.0));
            }
        }
        return samples;
    }

    /// <summary>The samples that are the creature's colour: the vivid ones about the hue most of them have.</summary>
    private static List<Sample> Own(List<Sample> samples)
    {
        var vivid = samples.Where(s => s.Vivid).ToList();
        if (vivid.Count == 0) return [];
        // The heaviest stretch of the hue circle (a 24th at a time, with its neighbours), then the mean hue about it.
        double[] bins = new double[24];
        foreach (var s in vivid) bins[(int)(s.Hsl.H * 24) % 24] += s.Weight * (0.4 + s.Hsl.S);
        int best = Enumerable.Range(0, 24).OrderByDescending(i => bins[(i + 23) % 24] + bins[i] + bins[(i + 1) % 24]).ThenBy(i => i).First();
        double centre = (best + 0.5) / 24;
        static double Apart(double a, double b) => Math.Min(Math.Abs(a - b), 1 - Math.Abs(a - b));
        var near = vivid.Where(s => Apart(s.Hsl.H, centre) <= 2.0 / 24).ToList();
        double sx = near.Sum(s => Math.Cos(s.Hsl.H * 2 * Math.PI) * s.Weight), sy = near.Sum(s => Math.Sin(s.Hsl.H * 2 * Math.PI) * s.Weight);
        double hue = (Math.Atan2(sy, sx) / (2 * Math.PI) + 1) % 1;
        return [.. vivid.Where(s => Apart(s.Hsl.H, hue) <= Band)];
    }

    private static (byte R, byte G, byte B) Mean(List<Sample> own)
    {
        double w = own.Sum(s => s.Weight);
        return ((byte)Math.Round(own.Sum(s => s.R * s.Weight) / w * 255), (byte)Math.Round(own.Sum(s => s.G * s.Weight) / w * 255), (byte)Math.Round(own.Sum(s => s.B * s.Weight) / w * 255));
    }

    /// <summary>The kinds of a level that have a colour to change, by class.</summary>
    public static IReadOnlyList<Kind> Kinds(byte[] level)
    {
        int vram = VramOffset(level);
        List<Kind> kinds = [];
        foreach (var (classId, start, _, model) in Models(level))
        {
            var own = Own(Samples(level, start, model, vram));
            double weight = own.Sum(s => s.Weight);
            if (weight >= MinimumWeight) kinds.Add(new(classId, model.Signature, Mean(own), (int)Math.Round(weight)));
        }
        return kinds;
    }

    /// <summary>A creature's new colours, worked out once from the game's own level it comes from: for its model's colour
    /// table, the entries that change; for its textures, the palettes that change (each named by the first face that uses
    /// it, since a level the editor built keeps the model's faces but puts its textures elsewhere) with their sixteen new
    /// colours (-1: as it is). Putting a recipe into a level writes these same colours whatever the level has there now,
    /// so an export that goes over a level more than once, or a game built on again, changes nothing the second time.</summary>
    public sealed record Recipe(Signature Model, IReadOnlyDictionary<int, (byte R, byte G, byte B)> Table, IReadOnlyList<(int Face, int[] Words)> Palettes);

    /// <summary>The recipe for a creature of <paramref name="sourceLevel"/> (the game's own level it comes from) in
    /// <paramref name="colour"/>, or null when the level has no such model or it has no colour to change.</summary>
    public static Recipe? RecipeFor(byte[] sourceLevel, Signature model, (byte R, byte G, byte B) colour)
    {
        int vram = VramOffset(sourceLevel);
        var found = Models(sourceLevel).FirstOrDefault(m => m.Model.Signature == model);
        if (found.Model == null) return null;
        var own = Own(Samples(sourceLevel, found.Start, found.Model, vram));
        double total = own.Sum(s => s.Weight);
        if (total < MinimumWeight) return null;
        var (th, ts, tl) = NativeSpyroColours.ToHsl(colour.R / 255.0, colour.G / 255.0, colour.B / 255.0);
        double meanL = own.Sum(s => s.Hsl.L * s.Weight) / total, meanS = own.Sum(s => s.Hsl.S * s.Weight) / total;
        (double R, double G, double B) New(Sample s)
        {
            var (_, sat, light) = s.Hsl;
            // Light and shade kept: a darker colour draws every shade towards black, a lighter one towards white.
            double l = tl <= meanL ? light * tl / Math.Max(meanL, 1e-6) : 1 - (1 - light) * (1 - tl) / Math.Max(1 - meanL, 1e-6);
            double rich = ts < 0.06 ? ts : Math.Clamp(ts + (sat - meanS), 0.02, 1);
            return NativeSpyroColours.FromHsl(th, rich, Math.Clamp(l, 0, 1));
        }
        Dictionary<int, (byte R, byte G, byte B)> table = [];
        var (firstTable, _) = found.Model.ColourTables[0];
        foreach (var s in own.Where(s => !s.Palette))
        {
            int index = (s.At - found.Start - firstTable) / 4;
            // The first colour table's entries stand for every table's (the same face corners use them).
            if (s.At < found.Start + firstTable || index < 0 || s.At != found.Start + firstTable + 4 * index || table.ContainsKey(index)) continue;
            var (r, g, b) = New(s);
            table[index] = ((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
        }
        List<(int Face, int[] Words)> palettes = [];
        foreach (var group in own.Where(s => s.Palette).GroupBy(s => s.PaletteAt))
        {
            int[] words = Enumerable.Repeat(-1, 16).ToArray();
            foreach (var s in group)
            {
                var (r, g, b) = New(s);
                int word = (int)Math.Round(r * 31) | (int)Math.Round(g * 31) << 5 | (int)Math.Round(b * 31) << 10;
                words[(s.At - group.Key) / 2] = word == 0 ? 1 << 10 : word;
            }
            int face = found.Model.Faces.FindIndex(f => f.Textured && (f.Page >> 7 & 3) == 0 && vram + NativeSpyroColours.PaletteOffset(f.Palette) == group.Key);
            if (face >= 0) palettes.Add((face, words));
        }
        return table.Count == 0 && palettes.Count == 0 ? null : new(model, table, palettes);
    }

    /// <summary>A copy of a level (a WAD entry) with the recipes' creatures in their colours, or null when none of them is
    /// there or all have those colours already.</summary>
    public static byte[]? Apply(byte[] level, IReadOnlyList<Recipe> recipes)
    {
        if (recipes.Count == 0) return null;
        int vram = VramOffset(level);
        byte[] copy = (byte[])level.Clone();
        bool changed = false;
        HashSet<int> donePalettes = [];
        foreach (var (_, start, _, model) in Models(level))
        {
            var signature = model.Signature;
            Recipe? recipe = null;
            for (int k = 0; k < recipes.Count && recipe == null; k++) if (recipes[k].Model == signature) recipe = recipes[k];
            if (recipe == null) continue;
            foreach (var (table, count) in model.ColourTables)
                foreach (var (index, rgb) in recipe.Table)
                {
                    int at = start + table + 4 * index;
                    if (index >= count || at + 4 > level.Length || level[at] == rgb.R && level[at + 1] == rgb.G && level[at + 2] == rgb.B) continue;
                    copy[at] = rgb.R; copy[at + 1] = rgb.G; copy[at + 2] = rgb.B;
                    changed = true;
                }
            if (vram < 0) continue;
            foreach (var (face, words) in recipe.Palettes)
            {
                if (face >= model.Faces.Count || !model.Faces[face].Textured || (model.Faces[face].Page >> 7 & 3) != 0) continue;
                ushort id = model.Faces[face].Palette;
                int x = (id & 63) * 16, y = id >> 6;
                if (x < 512 || y >= 512 || NativeSpyroColours.Palettes.Contains(id)) continue;
                int at = vram + NativeSpyroColours.PaletteOffset(id);
                if (at + NativeSpyroColours.PaletteBytes > level.Length || !donePalettes.Add(at)) continue;
                byte[] palette = level.AsSpan(at, NativeSpyroColours.PaletteBytes).ToArray();
                for (int i = 0; i < 16; i++)
                {
                    if (words[i] < 0) continue;
                    ushort old = BinaryPrimitives.ReadUInt16LittleEndian(level.AsSpan(at + 2 * i));
                    BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(2 * i), (ushort)(words[i] | old & 0x8000));
                }
                if (palette.AsSpan().SequenceEqual(level.AsSpan(at, NativeSpyroColours.PaletteBytes))) continue;
                // The palette and the seven dimmer rows under it.
                NativeSpyroColours.RecolourShaded(copy.AsSpan(vram, NativeSpyroColours.VramBytes), level.AsSpan(vram, NativeSpyroColours.VramBytes), at - vram, palette);
                changed = true;
            }
        }
        return changed ? copy : null;
    }

    /// <summary>A copy of one of the game's own levels with its creatures in <paramref name="choices"/> in their colours
    /// (each recipe made from the level itself), or null when none of them is there or changes.</summary>
    public static byte[]? Recolour(byte[] level, IReadOnlyList<(Signature Model, (byte R, byte G, byte B) Colour)> choices) =>
        Apply(level, [.. choices.Select(c => RecipeFor(level, c.Model, c.Colour)).OfType<Recipe>()]);

    /// <summary>A creature's model and the texture memory to draw it with in <paramref name="colour"/> (null: its own),
    /// from a level that has it; null when it has none.</summary>
    public static (byte[] Package, byte[] Vram)? PreviewModel(byte[] level, Signature model, (byte R, byte G, byte B)? colour)
    {
        int vram = VramOffset(level);
        if (vram < 0) return null;
        var found = Models(level).FirstOrDefault(m => m.Model.Signature == model);
        if (found.Model == null) return null;
        byte[] drawn = colour is { } c ? Recolour(level, [(model, c)]) ?? level : level;
        return (drawn.AsSpan(found.Start, found.End - found.Start).ToArray(), drawn.AsSpan(vram, NativeSpyroColours.VramBytes).ToArray());
    }

    /// <summary>A colour for a creature that keeps its own light and richness and takes another hue (0..1 of the circle):
    /// what Randomize gives.</summary>
    public static (byte R, byte G, byte B) WithHue((byte R, byte G, byte B) own, double hue)
    {
        var (_, s, l) = NativeSpyroColours.ToHsl(own.R / 255.0, own.G / 255.0, own.B / 255.0);
        var (r, g, b) = NativeSpyroColours.FromHsl((hue % 1 + 1) % 1, Math.Clamp(Math.Max(s, 0.55), 0, 1), Math.Clamp(l, 0.28, 0.62));
        return ((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
    }

    // ---- Create BIN's choices ----
    private static Scope? s_scope;
    private static readonly object ScopeLock = new();

    /// <summary>Whether the game Create BIN is making has creatures in chosen colours.</summary>
    public static bool Enabled => Volatile.Read(ref s_scope)?.Levels.Count > 0;

    /// <summary>Create BIN's choices until the scope is disposed (scopes nest): for each level (its key), its creatures'
    /// recipes; <paramref name="levelOfEntry"/> says which level a WAD entry of the game's own is.</summary>
    public static Scope Use(IReadOnlyDictionary<string, IReadOnlyList<Recipe>>? levels, Func<int, string?>? levelOfEntry)
    {
        lock (ScopeLock)
        {
            var scope = new Scope(levels ?? new Dictionary<string, IReadOnlyList<Recipe>>(), levelOfEntry ?? (_ => null), s_scope);
            Volatile.Write(ref s_scope, scope);
            return scope;
        }
    }

    private static IReadOnlyList<Recipe>? For(Scope scope, int entry, Func<int, string?>? levelOfEntry) =>
        (levelOfEntry ?? scope.LevelOfEntry)(entry) is { } key && scope.Levels.TryGetValue(key, out var choices) && choices.Count > 0 ? choices : null;

    /// <summary>The export's last step (with Spyro's colours): every level whose creatures change is added to
    /// <paramref name="replacements"/>. Returns how many levels changed.</summary>
    public static int ApplyForExport(IDictionary<int, byte[]> replacements, Func<int, byte[]> entryBytes)
    {
        if (Volatile.Read(ref s_scope) is not { Levels.Count: > 0 } scope) return 0;
        int changed = 0;
        for (int entry = 0; entry < 256; entry++)
        {
            if (For(scope, entry, null) is not { } choices) continue;
            byte[] bytes = replacements.TryGetValue(entry, out var replaced) ? replaced : entryBytes(entry);
            if (Apply(bytes, choices) is { } copy) { replacements[entry] = copy; changed++; }
        }
        return changed;
    }

    /// <summary>Recolours only these entries (levels an export adds, as the new world's, whose levels
    /// <paramref name="levelOfEntry"/> names; null: the game's own).</summary>
    public static int ApplyToEntries(IDictionary<int, byte[]> entries, Func<int, string?>? levelOfEntry = null)
    {
        if (Volatile.Read(ref s_scope) is not { Levels.Count: > 0 } scope) return 0;
        int changed = 0;
        foreach (int entry in entries.Keys.ToArray())
            if (For(scope, entry, levelOfEntry) is { } choices && Apply(entries[entry], choices) is { } copy) { entries[entry] = copy; changed++; }
        return changed;
    }

    /// <summary>The same for a complete WAD.WAD, in place (entries keep their places and lengths).</summary>
    public static int ApplyForExport(byte[] wad)
    {
        if (Volatile.Read(ref s_scope) is not { Levels.Count: > 0 } scope) return 0;
        int changed = 0;
        for (int entry = 0; entry < 256; entry++)
        {
            if (For(scope, entry, null) is not { } choices) continue;
            int offset = BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(entry * 8)), length = BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(entry * 8 + 4));
            if (length <= 0 || offset < 0 || (long)offset + length > wad.Length) continue;
            if (Apply(wad.AsSpan(offset, length).ToArray(), choices) is { } copy) { copy.CopyTo(wad, offset); changed++; }
        }
        return changed;
    }

    public sealed class Scope : IDisposable
    {
        private readonly Scope? _previous;
        private bool _disposed;
        internal Scope(IReadOnlyDictionary<string, IReadOnlyList<Recipe>> levels, Func<int, string?> levelOfEntry, Scope? previous)
        { Levels = new Dictionary<string, IReadOnlyList<Recipe>>(levels, StringComparer.OrdinalIgnoreCase); LevelOfEntry = levelOfEntry; _previous = previous; }
        public IReadOnlyDictionary<string, IReadOnlyList<Recipe>> Levels { get; }
        public Func<int, string?> LevelOfEntry { get; }
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

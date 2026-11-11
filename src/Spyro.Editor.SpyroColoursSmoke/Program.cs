using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Universal;

// Spyro's colours (NativeSpyroColours) and their Game Settings file (SpyroColourSettings). Default: source-free (the saved
// colours, the recolour on made-up palettes, Create BIN's scope). --source <USA.bin>: on the USA game's WAD, every level,
// the title screen and the cutscenes change only in Spyro's palettes; other objects' copies stay; again changes nothing.
int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
void Refuse<T>(Action action, string message) where T : Exception { try { action(); } catch (T) { checks++; return; } throw new Exception(message); }
ushort Word(ReadOnlySpan<byte> rows, int palette, int i) => BinaryPrimitives.ReadUInt16LittleEndian(rows[(palette * 32 + 2 * i)..]);
(double R, double G, double B) Rgb(ushort w) => ((w & 31) / 31.0, (w >> 5 & 31) / 31.0, (w >> 10 & 31) / 31.0);
var red = new Dictionary<string, (byte R, byte G, byte B)> { ["body"] = (200, 30, 40) };

if (args.Length == 0)
{
    string root = Path.Combine(Path.GetTempPath(), "spyro-colours-smoke-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        Check(SpyroColourSettings.Load(root).Count == 0 && !File.Exists(SpyroColourSettings.PathFor(root)), "a project has his own colours until it changes one");
        SpyroColourSettings.Save(root, new Dictionary<string, (byte, byte, byte)> { ["horns"] = (1, 2, 3), ["body"] = (255, 0, 128) });
        var loaded = SpyroColourSettings.Load(root);
        Check(loaded.Count == 2 && loaded["horns"] == (1, 2, 3) && loaded["body"] == (255, 0, 128), "the colours are saved with the project");
        Check(File.ReadAllText(SpyroColourSettings.PathFor(root)).Contains("#FF0080"), "as #RRGGBB");
        SpyroColourSettings.Save(root, new Dictionary<string, (byte, byte, byte)>());
        Check(!File.Exists(SpyroColourSettings.PathFor(root)), "back to his own colours, the file goes");
        File.WriteAllText(SpyroColourSettings.PathFor(root), "{\"schemaVersion\":1,\"colours\":{\"tail\":\"#112233\"}}");
        Refuse<InvalidDataException>(() => SpyroColourSettings.Load(root), "a part Spyro doesn't have is refused");
        File.WriteAllText(SpyroColourSettings.PathFor(root), "{\"schemaVersion\":1,\"colours\":{\"body\":\"red\"}}");
        Refuse<InvalidDataException>(() => SpyroColourSettings.Load(root), "a colour that is not #RRGGBB is refused");
        File.WriteAllText(SpyroColourSettings.PathFor(root), "{\"schemaVersion\":2,\"colours\":{}}");
        Refuse<InvalidDataException>(() => SpyroColourSettings.Load(root), "an unknown version is refused");
        Refuse<ArgumentException>(() => SpyroColourSettings.Save(root, new Dictionary<string, (byte, byte, byte)> { ["wing"] = (0, 0, 0) }), "saving an unknown part is refused");
    }
    finally { Directory.Delete(root, recursive: true); }
    Check(SpyroColourSettings.TryParse(" #0a0B0c ", out var parsed) && parsed == (10, 11, 12) && !SpyroColourSettings.TryParse("#12345", out _), "colours parse as #RRGGBB");

    // Made-up palettes: every colour a mid purple with some spread, colour 0 transparent.
    byte[] rows = new byte[17 * 32];
    for (int k = 0; k < 17; k++)
        for (int i = 1; i < 16; i++)
        {
            int v = 8 + (i + k) % 9;
            BinaryPrimitives.WriteUInt16LittleEndian(rows.AsSpan(k * 32 + 2 * i), (ushort)(v | (v - 3) << 5 | (v + 6) << 10));
        }
    byte[] body = NativeSpyroColours.Recolour(rows, red);
    Check(Enumerable.Range(0, 17).All(k => Word(body, k, 0) == 0), "the transparent colour stays transparent");
    Check(body.AsSpan(15 * 32, 32).SequenceEqual(rows.AsSpan(15 * 32, 32)), "the mouth keeps its colours");
    Check(body.AsSpan(10 * 32, 32).SequenceEqual(rows.AsSpan(10 * 32, 32)) && body.AsSpan(13 * 32, 32).SequenceEqual(rows.AsSpan(13 * 32, 32)),
        "parts not chosen (horns, head spikes) keep theirs");
    var reds = Enumerable.Range(1, 11).Select(i => Rgb(Word(body, 0, i))).ToArray();
    Check(reds.All(c => c.R > c.G + 0.2 && c.R > c.B + 0.2), "the body's palette turns red");
    var light = Enumerable.Range(1, 11).Select(i => NativeSpyroColours.ToHsl(Rgb(Word(rows, 0, i)).R, Rgb(Word(rows, 0, i)).G, Rgb(Word(rows, 0, i)).B).L).ToArray();
    var newLight = reds.Select(c => NativeSpyroColours.ToHsl(c.R, c.G, c.B).L).ToArray();
    Check(Enumerable.Range(0, 11).All(a => Enumerable.Range(0, 11).All(b => light[a] <= light[b] - 0.04 ? newLight[a] <= newLight[b] + 0.02 : true)), "its shading keeps its order");
    Check(Word(body, 1, 2) == Word(rows, 1, 2) && Word(body, 1, 3) != Word(rows, 1, 3), "in a mixed palette only the part's colours change (eye white stays, eyelid turns)");
    // Eyes whose whites are pure white still take a darker colour (the whites are most of the eye).
    byte[] white = rows.ToArray();
    BinaryPrimitives.WriteUInt16LittleEndian(white.AsSpan(32 + 2 * 2), 0x7FFF);
    var yellow = NativeSpyroColours.Recolour(white, new Dictionary<string, (byte R, byte G, byte B)> { ["eyes"] = (255, 224, 102) });
    var eyeWhite = Rgb(Word(yellow, 1, 2));
    Check(eyeWhite.R > eyeWhite.B + 0.2 && eyeWhite.G > eyeWhite.B + 0.2, "pure white eyes turn yellow");
    var black = NativeSpyroColours.Recolour(rows, new Dictionary<string, (byte R, byte G, byte B)> { ["body"] = (0, 0, 0) });
    Check(Enumerable.Range(1, 11).All(i => Word(black, 0, i) != 0), "black never becomes the transparent colour");
    Refuse<InvalidDataException>(() => NativeSpyroColours.Recolour(rows, new Dictionary<string, (byte R, byte G, byte B)> { ["tail"] = (0, 0, 0) }), "an unknown part is refused");
    var own = NativeSpyroColours.OriginalColours(rows);
    Check(own.Count == NativeSpyroColours.Parts.Count && own["body"].B > own["body"].R, "each part has its own colour to show");
    Check(NativeSpyroColours.Originals(new byte[0x80000]) == null, "palettes that are not the USA game's are not Spyro's");

    // Create BIN's scope.
    Check(!NativeSpyroColours.Enabled, "no export, no colours");
    byte[] wad = new byte[0x1000];
    Check(NativeSpyroColours.ApplyForExport(wad) == 0 && wad.All(b => b == 0), "with the scope off the game is left alone");
    using (NativeSpyroColours.Use(red))
    {
        Check(NativeSpyroColours.Enabled, "the export asks for colours");
        using (NativeSpyroColours.Use(null)) Check(!NativeSpyroColours.Enabled, "an inner scope can give him his own");
        Check(NativeSpyroColours.Enabled, "the outer choice comes back");
        Check(NativeSpyroColours.ApplyForExport(wad) == 0, "a WAD without Spyro's palettes is left alone");
    }
    Check(!NativeSpyroColours.Enabled, "the scope ends with the export");
    Refuse<InvalidDataException>(() => NativeSpyroColours.Use(new Dictionary<string, (byte R, byte G, byte B)> { ["fins"] = (1, 1, 1) }), "a scope with an unknown part is refused");

    // ---- a look of its own for a level ----
    string project = Path.Combine(Path.GetTempPath(), "spyro-level-colours-smoke-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(project);
    try
    {
        SpyroColourSettings.Save(project, red);
        SpyroColourSettings.SaveLevel(project, "artisans", new Dictionary<string, (byte, byte, byte)> { ["horns"] = (1, 2, 3) });
        SpyroColourSettings.SaveLevel(project, "darkhollow", new Dictionary<string, (byte, byte, byte)>());
        var book = SpyroColourSettings.LoadAll(project);
        Check(book.Whole.Count == 1 && book.Levels.Count == 2 && book.Levels["artisans"]["horns"] == (1, 2, 3) && book.Levels["darkhollow"].Count == 0 && book.Levels.ContainsKey("ARTISANS"),
            "a level keeps a look of its own beside the whole game's (none of its parts changed: his own colours there)");
        Check(SpyroColourSettings.Load(project).Count == 1, "the whole game's look reads as before (as editors before 2.1 read the file)");
        SpyroColourSettings.Save(project, new Dictionary<string, (byte, byte, byte)>());
        Check(File.Exists(SpyroColourSettings.PathFor(project)) && File.ReadAllText(SpyroColourSettings.PathFor(project)).Contains("\"colours\": {}") && SpyroColourSettings.LoadAll(project).Levels.Count == 2,
            "the whole game back to his own colours: the levels' looks stay, and the file still has its colours");
        SpyroColourSettings.SaveLevel(project, "artisans", null); SpyroColourSettings.SaveLevel(project, "darkhollow", null);
        Check(!File.Exists(SpyroColourSettings.PathFor(project)), "with no look anywhere the file goes");
        Refuse<ArgumentException>(() => SpyroColourSettings.SaveLevel(project, "artisans", new Dictionary<string, (byte, byte, byte)> { ["fins"] = (1, 1, 1) }), "a level's look with an unknown part is refused");
        using (NativeSpyroColours.Use(null, new Dictionary<string, IReadOnlyDictionary<string, (byte R, byte G, byte B)>> { ["artisans"] = red }, _ => null))
            Check(NativeSpyroColours.SpyroEnabled && NativeSpyroColours.Enabled, "a look for one level alone makes the export ask for colours");
        using (NativeSpyroColours.Use(null, new Dictionary<string, IReadOnlyDictionary<string, (byte R, byte G, byte B)>> { ["artisans"] = new Dictionary<string, (byte R, byte G, byte B)>() }, _ => null))
            Check(!NativeSpyroColours.SpyroEnabled, "his own colours everywhere, said level by level, ask for none");

        // ---- creatures' colours, level by level ----
        Check(CreatureColourSettings.Load(project).Count == 0, "a project's creatures have their own colours until one is changed");
        string gnorc = CreatureColourSettings.KindKey("artisans", 0x72);
        Check(gnorc == "artisans:72" && CreatureColourSettings.TryParseKind(gnorc, out string from, out int cls) && from == "artisans" && cls == 0x72 &&
              !CreatureColourSettings.TryParseKind("artisans", out _, out _) && !CreatureColourSettings.TryParseKind("artisans:xyz", out _, out _), "a kind is named by its level and class");
        CreatureColourSettings.SaveLevel(project, "artisans", new Dictionary<string, (byte, byte, byte)> { [gnorc] = (200, 36, 36) });
        CreatureColourSettings.SaveLevel(project, "darkhollow", new Dictionary<string, (byte, byte, byte)> { ["darkhollow:73"] = (40, 80, 210), ["darkhollow:a5"] = (40, 80, 210) });
        var creatures = CreatureColourSettings.Load(project);
        Check(creatures.Count == 2 && creatures["artisans"][gnorc] == (200, 36, 36) && creatures["darkhollow"].Count == 2 && File.ReadAllText(CreatureColourSettings.PathFor(project)).Contains("#C82424"),
            "each level keeps its own creatures' colours, as #RRGGBB");
        CreatureColourSettings.SaveLevel(project, "artisans", new Dictionary<string, (byte, byte, byte)>());
        Check(CreatureColourSettings.Load(project).Count == 1, "a level back to its own colours leaves the other level's");
        CreatureColourSettings.SaveLevel(project, "darkhollow", new Dictionary<string, (byte, byte, byte)>());
        Check(!File.Exists(CreatureColourSettings.PathFor(project)), "with no colour anywhere the file goes");
        File.WriteAllText(CreatureColourSettings.PathFor(project), "{\"schemaVersion\":1,\"levels\":{\"artisans\":{\"nonsense\":\"#112233\"}}}");
        Refuse<InvalidDataException>(() => CreatureColourSettings.Load(project), "a kind that is not a level and class is refused");
        Check(!NativeCreatureColours.Enabled && NativeCreatureColours.SignatureOf(new byte[0x100]) == null && NativeCreatureColours.Kinds(new byte[0x1000]).Count == 0 &&
              NativeCreatureColours.Recolour(new byte[0x1000], []) == null, "what is not a level or a creature's model has no creatures to colour");
        var turned = NativeCreatureColours.WithHue((60, 140, 40), 0.0);
        Check(turned.R > turned.G + 60 && turned.R > turned.B + 60, "a creature's colour with its hue turned to red is red");
    }
    finally { Directory.Delete(project, recursive: true); }
    Console.WriteLine($"SPYRO_COLOURS_FREE_OK checks={checks}");
    return 0;
}

if (args.Length >= 2 && args[0] == "--source")
{
    var disc = UniversalDiscImage.Read(args[1]);
    byte[]? originals = NativeSpyroColours.FindOriginals(disc.EntryBytes);
    Check(originals != null, "the USA game has Spyro's palettes");
    var own = NativeSpyroColours.OriginalColours(originals!);
    Check(own["body"].B > own["body"].R && own["body"].R > own["body"].G, "his body is purple");
    Check(own["belly"].R > own["belly"].B && own["horns"].R > own["horns"].B && own["eyes"].R > 180, "his belly and horns are warm, his eyes white");

    var all = new Dictionary<string, (byte R, byte G, byte B)>
    {
        ["body"] = (40, 200, 60), ["belly"] = (255, 130, 30), ["horns"] = (250, 250, 250), ["spikes"] = (240, 20, 190), ["wings"] = (0, 110, 220),
        ["wingSkin"] = (120, 220, 255), ["tailTip"] = (255, 60, 50), ["eyes"] = (255, 220, 0), ["pupils"] = (170, 10, 200),
    };
    byte[] recoloured = NativeSpyroColours.Recolour(originals!, all);
    Dictionary<int, byte[]> replacements = [];
    int changed;
    using (NativeSpyroColours.Use(all)) changed = NativeSpyroColours.ApplyForExport(replacements, disc.EntryBytes);
    int[] cutscenes = [3, 4, 5, 6];
    int[] levels = Enumerable.Range(0, 35).Select(i => 10 + 2 * i).ToArray();
    Check(changed == 39 && replacements.Count == 39 && cutscenes.Concat(levels).All(replacements.ContainsKey), "35 levels, the title screen and 3 cutscenes change");
    // Gnasty Gnorc's level (entry 76) swaps a stand-in Spyro in for him: its 16 palette copies stand in two columns of
    // eight-row blocks (the palette and seven distance shades) from VRAM (704, 296) and (720, 256).
    const int GnastyGnorc = 76;
    bool StandInRow(int row) { int y = row / 1024, x = 512 + row % 1024 / 2; return x == 704 && y is >= 296 and < 352 || x == 720 && y is >= 256 and < 328; }
    int standInRows = 0;
    foreach (var (entry, bytes) in replacements)
    {
        byte[] before = disc.EntryBytes(entry);
        Check(bytes.Length == before.Length, $"entry {entry} keeps its length");
        int vram = 0x800, diff = 0;
        string? wrong = null;
        for (int row = 0; row < 0x80000 && wrong == null; row += 32)
        {
            if (bytes.AsSpan(vram + row, 32).SequenceEqual(before.AsSpan(vram + row, 32))) continue;
            diff++;
            if (entry == GnastyGnorc && StandInRow(row)) { standInRows++; continue; }
            int k = Enumerable.Range(0, 17).FirstOrDefault(p => before.AsSpan(vram + row, 32).SequenceEqual(originals!.AsSpan(p * 32, 32)), -1);
            if (k < 0 || !bytes.AsSpan(vram + row, 32).SequenceEqual(recoloured.AsSpan(k * 32, 32))) wrong = "something other than Spyro's palettes";
            else if (!cutscenes.Contains(entry) && row != NativeSpyroColours.PaletteOffset(NativeSpyroColours.Palettes[k])) wrong = "a palette copy away from its own place";
        }
        if (wrong == null && (!bytes.AsSpan(0, vram).SequenceEqual(before.AsSpan(0, vram)) || !bytes.AsSpan(vram + 0x80000).SequenceEqual(before.AsSpan(vram + 0x80000))))
            wrong = "a byte outside its VRAM image";
        Check(wrong == null && diff > 0, $"entry {entry} changes only Spyro's palettes, to his new colours ({wrong ?? "nothing changed"})");
    }
    // The stand-in: 15 of its 16 copies change (the mouth's does not), each with its seven shades; a copy's first row is
    // his new palette, and its shades fade as before (towards the same haze: the last shade of the body's copy moves
    // less than its first row does: here to 43%).
    // (a last shade can already be the haze colour itself and stay as it is)
    Check(standInRows is > 15 * 7 and <= 15 * 8, $"the stand-in Spyro's 15 changed palettes change with their shades ({standInRows} rows)");
    {
        byte[] was = disc.EntryBytes(GnastyGnorc), now = replacements[GnastyGnorc];
        int body = 0x800 + 296 * 1024 + (704 - 512) * 2;   // palette 0x6B7D's copy
        Check(was.AsSpan(body, 32).SequenceEqual(originals!.AsSpan(0, 32)) && now.AsSpan(body, 32).SequenceEqual(recoloured.AsSpan(0, 32)), "a copy's first row is his new palette");
        int Moved(int row) => Enumerable.Range(1, 11).Sum(i => { ushort a = BinaryPrimitives.ReadUInt16LittleEndian(was.AsSpan(body + row * 1024 + 2 * i)), b = BinaryPrimitives.ReadUInt16LittleEndian(now.AsSpan(body + row * 1024 + 2 * i));
            return Math.Abs((a & 31) - (b & 31)) + Math.Abs((a >> 5 & 31) - (b >> 5 & 31)) + Math.Abs((a >> 10 & 31) - (b >> 10 & 31)); });
        Check(Moved(0) > Moved(4) && Moved(4) > Moved(7) && Moved(7) > 0, $"its shades fade towards the same haze, each further row changing less ({Moved(0)}, {Moved(4)}, {Moved(7)})");
        Check(Enumerable.Range(0, 8).All(row => BinaryPrimitives.ReadUInt16LittleEndian(now.AsSpan(body + row * 1024)) == 0), "the transparent colour stays transparent in every shade");
    }
    // Artisans' dragons share copies of Spyro's back palette (VRAM 784, 448): they keep their colours.
    int dragonCopy = 0x800 + 448 * 1024 + (784 - 512) * 2;
    Check(replacements[10].AsSpan(dragonCopy, 32).SequenceEqual(disc.EntryBytes(10).AsSpan(dragonCopy, 32)), "other objects' copies in a level stay");
    Check(disc.EntryBytes(10).AsSpan(dragonCopy, 32).SequenceEqual(originals!.AsSpan(5 * 32, 32)), "(that copy is the back palette)");
    using (NativeSpyroColours.Use(all))
    {
        var again = new Dictionary<int, byte[]>(replacements);
        Check(NativeSpyroColours.ApplyForExport(again, disc.EntryBytes) == 0, "applying the colours again changes nothing");
        // The whole-WAD form (the retail export's last step) makes the same game.
        byte[] wad = disc.Wad.ToArray();
        Check(NativeSpyroColours.ApplyForExport(wad) == 39, "the whole WAD: the same 39 entries");
        bool same = true;
        for (int e = 0; e < 256; e++)
        {
            int o = BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(e * 8)), l = BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(e * 8 + 4));
            if (l == 0) continue;
            byte[] expected = replacements.TryGetValue(e, out var r) ? r : disc.EntryBytes(e);
            same &= wad.AsSpan(o, l).SequenceEqual(expected);
        }
        Check(same, "every WAD entry is the same either way");
        // Levels an export adds (the new world's) take the colours on their own.
        var added = new Dictionary<int, byte[]> { [102] = disc.EntryBytes(10), [103] = disc.EntryBytes(9) };
        Check(NativeSpyroColours.ApplyToEntries(added) == 1 && added[102].AsSpan().SequenceEqual(replacements[10]) && added[103].AsSpan().SequenceEqual(disc.EntryBytes(9)),
            "an added level is recoloured, its overlay left alone");
    }
    // The picture of Spyro in the editor: the stand-in's model with the texture memory in his colours.
    {
        byte[] level = disc.EntryBytes(NativeSpyroColours.StandInWadEntry);
        var plain = NativeSpyroColours.PreviewModel(level, new Dictionary<string, (byte R, byte G, byte B)>());
        var look = NativeSpyroColours.PreviewModel(level, all);
        Check(plain is { } o && look is { } l && o.Package.Length == 41868 && o.Package.AsSpan().SequenceEqual(l.Package) &&
            o.Vram.AsSpan().SequenceEqual(level.AsSpan(0x800, 0x80000)) && l.Vram.AsSpan().SequenceEqual(replacements[NativeSpyroColours.StandInWadEntry].AsSpan(0x800, 0x80000)),
            "the preview draws the stand-in's model with the texture memory the game will have");
        Check(NativeSpyroColours.PreviewModel(disc.EntryBytes(10), all) == null, "a level without the stand-in has no preview model");
    }
    // One part only: the eyes. Body palettes stay as they were.
    var eyes = NativeSpyroColours.Recolour(originals!, new Dictionary<string, (byte R, byte G, byte B)> { ["eyes"] = (255, 0, 0) });
    Check(eyes.AsSpan(0, 32).SequenceEqual(originals!.AsSpan(0, 32)) && !eyes.AsSpan(32, 32).SequenceEqual(originals!.AsSpan(32, 32)), "eyes only: the eye palette changes, the body's stays");

    // ---- a look for one level: Artisans green, Dark Hollow his own, the rest of the game red ----
    {
        var green = new Dictionary<string, (byte R, byte G, byte B)> { ["body"] = (40, 200, 60) };
        var perLevel = new Dictionary<string, IReadOnlyDictionary<string, (byte R, byte G, byte B)>> { ["artisans"] = green, ["darkhollow"] = new Dictionary<string, (byte R, byte G, byte B)>() };
        string? LevelOf(int entry) => entry == 10 ? "artisans" : entry == 14 ? "darkhollow" : entry == 12 ? "stonehill" : null;
        Dictionary<int, byte[]> made = [];
        using (NativeSpyroColours.Use(red, perLevel, LevelOf)) NativeSpyroColours.ApplyForExport(made, disc.EntryBytes);
        byte[] redRows = NativeSpyroColours.Recolour(originals!, red), greenRows = NativeSpyroColours.Recolour(originals!, green);
        bool Has(byte[] entry, byte[] rows) => Enumerable.Range(0, 17).All(k => entry.AsSpan(0x800 + NativeSpyroColours.PaletteOffset(NativeSpyroColours.Palettes[k]), 32).SequenceEqual(rows.AsSpan(k * 32, 32)));
        Check(Has(made[10], greenRows) && !made.ContainsKey(14) && Has(made[12], redRows) && Has(made[16], redRows) && made.Count == 38,
            "a level with its own look has it, a level given his own colours is left alone, and every other level and the cutscenes have the whole game's");
        Dictionary<int, byte[]> again = new(made);
        int second;
        using (NativeSpyroColours.Use(red, perLevel, LevelOf)) second = NativeSpyroColours.ApplyForExport(again, disc.EntryBytes);
        Check(second == 0, "applied again, nothing changes (" + second + ": " + string.Join(",", again.Where(e => !made.TryGetValue(e.Key, out var m) || !ReferenceEquals(m, e.Value)).Select(e => e.Key)) + ")");
        // Levels an export adds (the new world's) say which they are themselves.
        Dictionary<int, byte[]> added = new() { [103] = disc.EntryBytes(10), [105] = disc.EntryBytes(12) };
        using (NativeSpyroColours.Use(red, new Dictionary<string, IReadOnlyDictionary<string, (byte R, byte G, byte B)>> { ["world70"] = green }, _ => null))
            NativeSpyroColours.ApplyToEntries(added, e => e == 103 ? "world70" : e == 105 ? "world71" : null);
        Check(Has(added[103], greenRows) && Has(added[105], redRows), "a new world's level has its own look, the next one the whole game's");
    }

    // ---- creatures' colours ----
    {
        byte[] artisans = disc.EntryBytes(10), hollow = disc.EntryBytes(14), stone = disc.EntryBytes(12);
        var kinds = NativeCreatureColours.Kinds(artisans);
        var gnorc = kinds.First(k => k.ClassId == 0x72);
        Check(kinds.Count >= 20 && gnorc.Colour.G > gnorc.Colour.R + 30 && gnorc.Colour.G > gnorc.Colour.B + 30, "Artisans' gnorc is found, and it is green");
        int with = Enumerable.Range(0, 35).Count(i => NativeCreatureColours.Kinds(disc.EntryBytes(10 + 2 * i)).Count > 0);
        Check(with == 35, $"every level has creatures with a colour ({with})");
        var library = new UniversalLevelData(artisans);
        Check(NativeCreatureColours.SignatureOf(library.Package(0x72)) == gnorc.Model, "a model's signature is the same from its package as in its level");
        (byte R, byte G, byte B) crimson = (200, 36, 36);
        byte[]? redLevel = NativeCreatureColours.Recolour(artisans, [(gnorc.Model, crimson)]);
        Check(redLevel != null && redLevel.Length == artisans.Length, "the gnorc takes a colour");
        var after = NativeCreatureColours.Kinds(redLevel!);
        var now = after.First(k => k.ClassId == 0x72);
        Check(now.Model == gnorc.Model && now.Colour.R > now.Colour.G + 60 && now.Colour.R > now.Colour.B + 60, $"it is red, and still the same model (#{now.Colour.R:X2}{now.Colour.G:X2}{now.Colour.B:X2})");
        int others = kinds.Count(k => k.ClassId != 0x72 && after.FirstOrDefault(a => a.ClassId == k.ClassId)?.Colour == k.Colour);
        Check(others >= kinds.Count - 3, $"the level's other kinds keep their colours ({others} of {kinds.Count - 1})");
        // His own palettes are never a creature's.
        Check(Enumerable.Range(0, 17).All(k => redLevel.AsSpan(0x800 + NativeSpyroColours.PaletteOffset(NativeSpyroColours.Palettes[k]), 32).SequenceEqual(artisans.AsSpan(0x800 + NativeSpyroColours.PaletteOffset(NativeSpyroColours.Palettes[k]), 32))),
            "Spyro's own palettes are left alone");
        Check(redLevel.AsSpan(0, 0x800).SequenceEqual(artisans.AsSpan(0, 0x800)), "the level's layout is as it was");
        Check(NativeCreatureColours.Recolour(stone, [(gnorc.Model, crimson)]) == null || NativeCreatureColours.Kinds(stone).Any(k => k.Model == gnorc.Model),
            "a level without that model is left alone");
        var preview = NativeCreatureColours.PreviewModel(artisans, gnorc.Model, crimson);
        var plainPreview = NativeCreatureColours.PreviewModel(artisans, gnorc.Model, null);
        Check(preview is { } p && plainPreview is { } q && p.Package.Length == q.Package.Length && !p.Package.AsSpan().SequenceEqual(q.Package) && q.Vram.AsSpan().SequenceEqual(artisans.AsSpan(0x800, 0x80000)),
            "the card's picture draws the creature's model in the colour (its own: the level's as it is)");
        // Level by level through the export: Artisans' gnorc red, Dark Hollow's large gnorc blue, Stone Hill untouched.
        var large = NativeCreatureColours.Kinds(hollow).First(k => k.ClassId == 0x73);
        (byte R, byte G, byte B) blue = (40, 80, 210);
        var choices = new Dictionary<string, IReadOnlyList<NativeCreatureColours.Recipe>>
        { ["artisans"] = [NativeCreatureColours.RecipeFor(artisans, gnorc.Model, crimson)!], ["darkhollow"] = [NativeCreatureColours.RecipeFor(hollow, large.Model, blue)!] };
        // A recipe from the game's own level colours the same model wherever it is, and again changes nothing.
        Check(NativeCreatureColours.Apply(redLevel!, choices["artisans"]) == null && NativeCreatureColours.Apply(artisans, choices["artisans"])!.AsSpan().SequenceEqual(redLevel),
            "a kind's recipe gives the same level every time, and nothing the second time");
        Dictionary<int, byte[]> made = [];
        int changedLevels;
        using (NativeCreatureColours.Use(choices, e => e == 10 ? "artisans" : e == 14 ? "darkhollow" : null))
        {
            Check(NativeCreatureColours.Enabled && NativeSpyroColours.Enabled, "creatures' colours alone make the export ask for colours");
            changedLevels = NativeSpyroColours.ApplyForExport(made, disc.EntryBytes);
        }
        var blueNow = NativeCreatureColours.Kinds(made[14]).First(k => k.ClassId == 0x73).Colour;
        Check(changedLevels == 2 && made.Count == 2 && made[10].AsSpan().SequenceEqual(redLevel) && blueNow.B > blueNow.R + 60 && blueNow.B > blueNow.G + 30,
            $"through the export only the two levels change: Artisans' gnorc red, Dark Hollow's large gnorc blue (#{blueNow.R:X2}{blueNow.G:X2}{blueNow.B:X2})");
        Check(!NativeCreatureColours.Enabled, "the scope ends with the export");
    }
    Console.WriteLine($"SPYRO_COLOURS_SOURCE_OK checks={checks}");
    return 0;
}
Console.Error.WriteLine("usage: SpyroColoursSmoke [--source <USA.bin>]");
return 2;

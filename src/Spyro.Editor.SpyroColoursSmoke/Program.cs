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
    foreach (var (entry, bytes) in replacements)
    {
        byte[] before = disc.EntryBytes(entry);
        Check(bytes.Length == before.Length, $"entry {entry} keeps its length");
        int vram = 0x800, diff = 0;
        string? wrong = null;
        for (int i = 0; i < bytes.Length && wrong == null; i++)
        {
            if (bytes[i] == before[i]) continue;
            int at = i - vram, row = at - at % 32;
            if (at < 0 || at >= 0x80000) { wrong = "a byte outside its VRAM image"; break; }
            int k = Enumerable.Range(0, 17).FirstOrDefault(p => before.AsSpan(vram + row, 32).SequenceEqual(originals!.AsSpan(p * 32, 32)), -1);
            if (k < 0 || !bytes.AsSpan(vram + row, 32).SequenceEqual(recoloured.AsSpan(k * 32, 32))) wrong = "something other than Spyro's palettes";
            else if (!cutscenes.Contains(entry) && row != NativeSpyroColours.PaletteOffset(NativeSpyroColours.Palettes[k])) wrong = "a palette copy away from its own place";
            diff++;
        }
        Check(wrong == null && diff > 0, $"entry {entry} changes only Spyro's palettes, to his new colours ({wrong ?? "nothing changed"})");
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
    // One part only: the eyes. Body palettes stay as they were.
    var eyes = NativeSpyroColours.Recolour(originals!, new Dictionary<string, (byte R, byte G, byte B)> { ["eyes"] = (255, 0, 0) });
    Check(eyes.AsSpan(0, 32).SequenceEqual(originals!.AsSpan(0, 32)) && !eyes.AsSpan(32, 32).SequenceEqual(originals!.AsSpan(32, 32)), "eyes only: the eye palette changes, the body's stays");
    Console.WriteLine($"SPYRO_COLOURS_SOURCE_OK checks={checks}");
    return 0;
}
Console.Error.WriteLine("usage: SpyroColoursSmoke [--source <USA.bin>]");
return 2;

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

/// <summary><c>--creature-colours-lab &lt;usa disc&gt; &lt;catalog root&gt; &lt;level key&gt; &lt;out.png&gt;</c>: every kind of a level that
/// <see cref="NativeCreatureColours"/> finds a colour for, drawn in its own colour, in red, in blue and in a turned hue
/// (the picture the editor's cards draw), with a line about each.</summary>
internal static class CreatureColoursLab
{
    /// <summary><c>--creature-colours-check &lt;created bin&gt; &lt;usa disc&gt; &lt;catalog root&gt; &lt;creature-colours.json of the step&gt;
    /// [world level ids in the game, e.g. 70,71]</c>: in the created game, every kind the step coloured has that colour in
    /// that level (found by what its model is), and no kind has it in a level that was not coloured.</summary>
    public static int Check(string bin, string disc, string root, string stepJson, string? worldIds)
    {
        var catalog = LevelCatalog.Load(root);
        var library = new UniversalSourceLibrary(disc, catalog);
        using var step = System.Text.Json.JsonDocument.Parse(File.ReadAllText(stepJson));
        string levelKey = step.RootElement.GetProperty("level").GetString()!;
        if (!Spyro.Editor.Core.Persistence.SpyroColourSettings.TryParse(step.RootElement.GetProperty("colour").GetString(), out var colour)) throw new ArgumentException("colour");
        byte[] entry;
        if (Spyro.Editor.Core.Editing.NewWorldDocument.IsWorldKey(levelKey, out int world))
        {
            int[] ids = [.. (worldIds ?? "70").Split(',').Select(int.Parse).Order()];
            entry = NativeLevelWindowDisc.ReadEntry(bin, NativeNewWorldExport.FirstEntry + 2 * Array.IndexOf(ids, world) + 1);
        }
        else entry = UniversalDiscImage.Read(bin).EntryBytes(catalog.FindByKey(levelKey)!.SourceWadEntry);
        var inGame = NativeCreatureColours.Kinds(entry);
        int failed = 0, checks = 0;
        var (th, _, _) = NativeSpyroColours.ToHsl(colour.R / 255.0, colour.G / 255.0, colour.B / 255.0);
        foreach (var kind in step.RootElement.GetProperty("kinds").EnumerateArray())
        {
            string key = kind.GetProperty("key").GetString()!, name = kind.GetProperty("name").GetString()!;
            Spyro.Editor.Core.Persistence.CreatureColourSettings.TryParseKind(key, out string source, out int classId);
            var model = NativeCreatureColours.SignatureOf(library.Get(source).Data.Package(classId));
            var own = NativeCreatureColours.Kinds(library.Disc.EntryBytes(catalog.FindByKey(source)!.SourceWadEntry)).First(k => k.ClassId == classId);
            var found = inGame.FirstOrDefault(k => k.Model == model);
            checks++;
            if (found == null) { failed++; Console.WriteLine($"FAIL {name} ({key}): its model is not in the game's {levelKey}"); continue; }
            var (h, _, _) = NativeSpyroColours.ToHsl(found.Colour.R / 255.0, found.Colour.G / 255.0, found.Colour.B / 255.0);
            double apart = Math.Min(Math.Abs(h - th), 1 - Math.Abs(h - th));
            bool ok = apart <= 0.05;
            if (!ok) failed++;
            Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {name} ({key}) as class 0x{found.ClassId:X3} in the game's {levelKey}: #{found.Colour.R:X2}{found.Colour.G:X2}{found.Colour.B:X2} (its own #{own.Colour.R:X2}{own.Colour.G:X2}{own.Colour.B:X2}, asked #{colour.R:X2}{colour.G:X2}{colour.B:X2})");
        }
        Console.WriteLine(failed == 0 ? $"CREATURE_COLOURS_CHECK_OK checks={checks}" : $"CREATURE_COLOURS_CHECK_FAILED {failed}/{checks}");
        return failed == 0 ? 0 : 1;
    }

    public static void Run(string disc, string root, string levelKey, string output)
    {
        var catalog = LevelCatalog.Load(root);
        var library = new UniversalSourceLibrary(disc, catalog);
        var level = catalog.FindByKey(levelKey) ?? throw new ArgumentException("Unknown level " + levelKey);
        byte[] bytes = library.Disc.EntryBytes(level.SourceWadEntry);
        var kinds = NativeCreatureColours.Kinds(bytes);
        Console.WriteLine($"{level.DisplayName}: {kinds.Count} kinds with a colour");
        foreach (var entry in UniversalMobyCatalogBuilder.Build(library, catalog, (_, _) => null))
            foreach (var v in entry.Variants.Where(v => string.Equals(v.SourceLevelKey, level.Key, StringComparison.OrdinalIgnoreCase)))
                if (entry.Category is "Enemies" or "NPCs")
                    Console.WriteLine($"  catalog: 0x{v.ClassId:X3} {entry.Name} [{entry.Category} / {entry.SubCategory}] local model {v.HasLocalModel}, shared {v.SharedModel}, restriction {v.RestrictionReason ?? "none"}");
        (byte, byte, byte)?[] looks = [null, (220, 40, 40), (40, 80, 220), null];
        const int tile = UniversalMobyModelPreview.Size;
        byte[] sheet = new byte[tile * looks.Length * tile * Math.Max(1, kinds.Count) * 4];
        int stride = tile * looks.Length * 4;
        for (int k = 0; k < kinds.Count; k++)
        {
            var kind = kinds[k];
            Console.WriteLine($"  class 0x{kind.ClassId:X3}: its colour #{kind.Colour.R:X2}{kind.Colour.G:X2}{kind.Colour.B:X2}, weight {kind.Weight}, {kind.Model.PlainFaces} plain + {kind.Model.TexturedFaces} textured faces, {kind.Model.Id}");
            for (int n = 0; n < looks.Length; n++)
            {
                var colour = n == 3 ? NativeCreatureColours.WithHue(kind.Colour, 0.83) : looks[n];
                var model = NativeCreatureColours.PreviewModel(bytes, kind.Model, colour);
                if (model == null) continue;
                UniversalMobyModelPreview.Frame frame;
                try { frame = UniversalMobyModelPreview.Decode(model.Value.Package, model.Value.Vram); }
                catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException or OverflowException) { Console.WriteLine("    no picture: " + e.Message); break; }
                if (frame.Pixels == null) continue;
                for (int y = 0; y < tile; y++) Array.Copy(frame.Pixels, y * tile * 4, sheet, (k * tile + y) * stride + n * tile * 4, tile * 4);
            }
        }
        // Unset pixels (transparent) on a mid grey.
        for (int i = 0; i < sheet.Length; i += 4) if (sheet[i + 3] == 0) { sheet[i] = sheet[i + 1] = sheet[i + 2] = 96; sheet[i + 3] = 255; }
        var bitmap = new WriteableBitmap(new PixelSize(tile * looks.Length, tile * Math.Max(1, kinds.Count)), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using (var buffer = bitmap.Lock())
            for (int y = 0; y < bitmap.PixelSize.Height; y++) Marshal.Copy(sheet, y * stride, buffer.Address + y * buffer.RowBytes, stride);
        bitmap.Save(output);
        Console.WriteLine("CREATURE_COLOURS_LAB " + output);
    }
}

using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

/// <summary>Source-free checks of the Environment tab's "Snow on trees and plants" (2026-10-10 report: "the snowy trees
/// are white until you get close and then green again"). The object renderer draws a textured face with palette row
/// (1024 - v) / 128 while its colour words slide from the near table to the far one by the same share
/// (0x800215B0..0x80021650), row 0 up close (0x80021730) and the untextured far table past that; the checks follow a
/// retail Stone Hill tree through all of it.</summary>
static class PlantSnowCases
{
    // Retail Stone Hill (WAD entry 12) tree palettes and their seven distance-shade rows: foliage 0x6E29 and bark 0x6E28.
    static readonly string[] Foliage =
    [
        "00001626160615E516461A6711C515E616051A4611A51A661A2615C51A0611E5",
        "00001A271A0719E61A471E6815C619E71A061E4715A61E671E2719C61E0715E6",
        "00001E281E081DE71E48224919C71DE81E07224819C7224822281DC7220819E7",
        "00002229220921E82229264A21E821E92208262921C82649262921E8260921E8",
        "00002A2A2A0A29EA2A2A2A4B25EA29EA2A0A2A2A25CA2A4A2A2A29EA2A0A25EA",
        "00002E0C2E0C2E0B2E2C322C2DEB2E0C2E0B322C2DEB322C320C2DEB320C2E0B",
        "0000360D360D360D362D362D31ED360D360D362D31ED362D360D35ED360D320D",
        "00003A0E3A0E3A0E3A0E3A2E3A0E3A0E3A0E3A0E39EE3A2E3A0E3A0E3A0E3A0E",
    ];
    static readonly string[] Bark =
    [
        "0000192C190B1D4D192B190A1D2C1D4C214E150A214D216E14EA1D2D1D4E0000",
        "00001D2C1D2B214D1D2B1D2A212C214C254E192A254D256E190A212D214E0000",
        "0000214D214C256E214C214B254D256D296E1D4B296E298E1D2B254E256E0000",
        "0000256D256D298E256D256C296D298D2D8F216C2D8E2DAF214C296E298F0000",
        "0000298E298D2DAE298D298D2D8E2DAE31AF298D31AE31AF296D2D8E2DAF0000",
        "000031AE31AE31AF31AE31AD31AE31AE35AF2DAD35AF35CF2D8D31AF31AF0000",
        "000035CF35CE35CF35CE35CE35CF35CF39CF35CE39CF39CF35AE35CF35CF0000",
        "000039EF39EF39EF39EF39EF39EF39EF3DF039EF3DEF3DF039CF39EF39F00000",
    ];
    const int Row = NativeModelPalettes.VramRowBytes;

    static int checks;
    static void Check(bool ok, string message) { if (!ok) throw new Exception("Plant snow: " + message); checks++; }
    static (int R, int G, int B) Rgb(ushort c) => ((c & 31) << 3, (c >> 5 & 31) << 3, (c >> 10 & 31) << 3);
    static ushort Word(byte[] b, int at) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(at));
    static double Luminance(double r, double g, double b) => 0.2126 * r + 0.7152 * g + 0.0722 * b;
    static bool Green(double r, double g, double b) => g - Math.Max(r, b) > 8;

    /// <summary>Texture pages holding the two retail palettes side by side at row <paramref name="y"/>.</summary>
    static byte[] Pages(int y, int height = 16)
    {
        byte[] pages = new byte[height * Row];
        for (int k = 0; k < 8 && y + k < height; k++)
        {
            Convert.FromHexString(Foliage[k]).CopyTo(pages, (y + k) * Row + 0x100);
            Convert.FromHexString(Bark[k]).CopyTo(pages, (y + k) * Row + 0x120);
        }
        // The retail rows are stored as little-endian words; the hex above is written as words, so swap each pair.
        for (int at = 0; at < pages.Length; at += 2) (pages[at], pages[at + 1]) = (pages[at + 1], pages[at]);
        return pages;
    }

    static byte[] Apply(byte[] pages, IEnumerable<NativeModelPalettes.Change> changes)
    {
        byte[] after = pages.ToArray();
        foreach (var change in changes)
        {
            Check(pages.AsSpan(change.Offset, change.Before.Length).SequenceEqual(change.Before), "a change's before bytes are the page bytes");
            change.After.CopyTo(after, change.Offset);
        }
        return after;
    }

    /// <summary>A static tree model: one single-frame animation, four colour words, a textured foliage triangle
    /// lit by words 0 (sunlit) and 1 (shade) plus the bark word 2, and an untextured green leaf of word 3.</summary>
    static byte[] Tree()
    {
        byte[] package = new byte[0x200];
        void Put(int at, int value) => BinaryPrimitives.WriteInt32LittleEndian(package.AsSpan(at), value);
        const int model = 0x100, animation = 0x40;
        Put(0, 1); Put(0x34, model); Put(0x38, animation);
        BinaryPrimitives.WriteInt16LittleEndian(package.AsSpan(animation), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan(animation + 2), 4);
        Put(animation + 0x14, 0x00); Put(animation + 0x18, 0x40); Put(animation + 0x1C, 0); Put(animation + 0x20, 0x60);
        Put(model, 28); // face table: one textured triangle (20 bytes) and one untextured triangle (8 bytes)
        Put(model + 4, 2); Put(model + 8, 0 << 19 | 1 << 10 | 2 << 1);
        BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan(model + 12 + 2), 0x6E29);
        Put(model + 24, 0); Put(model + 28, 3 << 19 | 3 << 10 | 3 << 1);
        byte[][] near = [[188, 182, 152], [81, 81, 81], [119, 87, 49], [40, 150, 40]];
        byte[][] far = [[59, 175, 39], [27, 83, 24], [119, 87, 49], [40, 150, 40]];
        for (int i = 0; i < 4; i++) { near[i].CopyTo(package, model + 0x40 + i * 4); far[i].CopyTo(package, model + 0x60 + i * 4); }
        return package;
    }

    public static void Run()
    {
        // 1. Every row the renderer can draw a snowy tree's foliage with is snowed, not just row 0.
        byte[] pages = Pages(4), snowed = Apply(pages, NativeModelPalettes.SnowClutRows(pages, 4 * Row + 0x100));
        for (int k = 0; k < 8; k++)
            for (int i = 1; i < 16; i++)
            {
                int at = (4 + k) * Row + 0x100 + i * 2;
                var (r, g, b) = Rgb(Word(snowed, at)); var (r0, g0, b0) = Rgb(Word(pages, at));
                Check(Green(r0, g0, b0) || k > 0, "the retail foliage row 0 is green");
                Check(!Green(r, g, b), $"foliage shade row {k} colour {i} is no longer green ({r},{g},{b})");
                Check(Luminance(r, g, b) > Luminance(r0, g0, b0), $"foliage shade row {k} colour {i} is lighter");
                Check((Word(snowed, at) & 0x8000) == (Word(pages, at) & 0x8000), "mask bit kept");
            }
        for (int k = 0; k < 8; k++)
            Check(Word(snowed, (4 + k) * Row + 0x100) == 0, "transparent colour kept in every row");
        for (int i = 1; i < 16; i++)
        {
            // The rows still fade from row 0 toward grey: each further row is less saturated.
            double Chroma(int k) { var (r, g, b) = Rgb(Word(snowed, (4 + k) * Row + 0x100 + i * 2)); return Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)); }
            Check(Chroma(7) <= Chroma(1) + 8, $"colour {i}: the far shade row is not more saturated than the near one");
        }
        Check(Apply(pages, NativeModelPalettes.SnowClutRows(pages, 4 * Row + 0x120)).SequenceEqual(pages), "bark palette and its rows unchanged");
        // Rows past the texture pages are absent, not written.
        byte[] shortPages = Pages(12);
        var cut = NativeModelPalettes.SnowClutRows(shortPages, 12 * Row + 0x100);
        Check(cut.Count == 4 && cut.All(c => c.Offset + c.After.Length <= shortPages.Length), "only the rows inside the pages");

        // 2. The exporter's palette choice: all eight rows, and nothing when a row is shared.
        var changes = NativeEnvironmentGradeExporter.PlantSnowTextureChanges(pages, [4 * Row + 0x100, 4 * Row + 0x120], [], []);
        Check(changes.Select(c => c.Offset).SequenceEqual(Enumerable.Range(0, 8).Select(k => (4 + k) * Row + 0x100)), "plant palette snowed in rows 0..7");
        Check(NativeEnvironmentGradeExporter.PlantSnowTextureChanges(pages, [4 * Row + 0x100], [7 * Row + 0x100], []).Count == 0,
            "a palette whose shade row is another model's palette row is left alone");
        Check(NativeEnvironmentGradeExporter.PlantSnowTextureChanges(pages, [4 * Row + 0x100], [0 * Row + 0x100], []).Count == 0,
            "a palette inside another model's shade rows is left alone");
        Check(NativeEnvironmentGradeExporter.PlantSnowTextureChanges(pages, [4 * Row + 0x100], [], [(10 * Row + 0x110, 32)]).Count == 0,
            "a palette whose shade row overlaps the landscape's palette is left alone");
        Check(NativeEnvironmentGradeExporter.PlantSnowTextureChanges(pages, [4 * Row + 0x100], [4 * Row + 0x120], []).Count == 8,
            "a neighbouring palette in the same rows does not block it");

        // 3. The model's colour words: the near words that light the textured foliage keep their brightness (they
        //    multiply the texture, 0x80 = as stored), the far words and the untextured leaf take the snow colour.
        byte[] tree = Tree();
        var words = NativeModelPalettes.Snow(tree) ?? throw new Exception("Plant snow: the tree is a plant");
        byte[] model = tree.ToArray();
        foreach (var change in words) change.After.CopyTo(model, change.Offset);
        double[] Near(byte[] p, int i) => [p[0x140 + i * 4], p[0x141 + i * 4], p[0x142 + i * 4]];
        double[] Far(byte[] p, int i) => [p[0x160 + i * 4], p[0x161 + i * 4], p[0x162 + i * 4]];
        for (int i = 0; i < 2; i++)
        {
            double before = Luminance(Near(tree, i)[0], Near(tree, i)[1], Near(tree, i)[2]), after = Luminance(Near(model, i)[0], Near(model, i)[1], Near(model, i)[2]);
            Check(Math.Abs(after - before) <= before * 0.05, $"textured light word {i} keeps its brightness ({before:F0} -> {after:F0})");
            Check(Near(model, i)[2] > Near(model, i)[0], $"textured light word {i} takes the snow's cool tint");
            Check(!Green(Far(model, i)[0], Far(model, i)[1], Far(model, i)[2]) && Luminance(Far(model, i)[0], Far(model, i)[1], Far(model, i)[2]) > 120, $"far word {i} is snow");
        }
        Check(Near(model, 2).SequenceEqual(Near(tree, 2)) && Far(model, 2).SequenceEqual(Far(tree, 2)), "bark words unchanged");
        Check(!Green(Near(model, 3)[0], Near(model, 3)[1], Near(model, 3)[2]) && Luminance(Near(model, 3)[0], Near(model, 3)[1], Near(model, 3)[2]) > 150,
            "the untextured leaf's near colour is snow");

        // 4. What the player sees: a foliage texel under the sunlit and the shaded word, from row 0 up close through
        //    every distance-shade row (colour words sliding near -> far), then the far table. Never green, never the
        //    flat white that hid the shading up close, and the shade side bluer than the sunlit side.
        byte[] page = Apply(pages, changes);
        for (int k = 0; k <= 8; k++)
        {
            double[] Seen(int word, int colour)
            {
                if (k == 8) return Far(model, word);
                var (r, g, b) = Rgb(Word(page, (4 + k) * Row + 0x100 + colour * 2));
                double[] texel = [r, g, b], light = Near(model, word).Zip(Far(model, word), (n, f) => n + (f - n) * k / 8.0).ToArray();
                return texel.Zip(light, (t, l) => Math.Min(255, t * l / 128)).ToArray();
            }
            for (int colour = 1; colour < 16; colour++)
            {
                double[] sun = Seen(0, colour), shade = Seen(1, colour);
                Check(!Green(sun[0], sun[1], sun[2]) && !Green(shade[0], shade[1], shade[2]), $"distance band {k}: snowy foliage is not green");
                Check(Luminance(shade[0], shade[1], shade[2]) < Luminance(sun[0], sun[1], sun[2]) - 20, $"distance band {k}: the shade side stays darker than the sunlit side");
                Check(shade[2] > shade[0], $"distance band {k}: the shade side is frosty blue");
            }
        }
        Console.WriteLine($"PLANT_SNOW_SOURCE_FREE_OK checks={checks}");
    }
}

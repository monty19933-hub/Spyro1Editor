using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Universal;

// Spyro's supercharge animations in built levels and in the game's own levels given supercharge ground with Tile Type…
// (NativeSuperchargeAnimations). Default: source-free (when a scene or a level's saved terrain edits need them, the
// texture rectangles a face table draws from, the refusal wording). --source <USA.bin> <workspace>: the real records and
// tables, a New Level and a rebuilt Tree Tops compiled with and without supercharge ground, tampering, every original
// level taking (or carrying, or refusing) the records, and Create BIN's object stage on Artisans. Lab: --retail-survey
// <USA.bin> <workspace> prints each original level's RAM and glow texture for the records.
int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
void Refuse(Action action, string message) { try { action(); } catch (InvalidDataException) { checks++; return; } throw new Exception(message); }
const float O = 2048, Z = 600;
AuthoredSurface Tile(int i, int j, AuthoredSurfaceKind? kind = null) =>
    new AuthoredSurface(Guid.NewGuid().ToString("N"), [new(O + i * 256, O + j * 256, Z), new(O + (i + 1) * 256, O + j * 256, Z),
        new(O + (i + 1) * 256, O + (j + 1) * 256, Z), new(O + i * 256, O + (j + 1) * 256, Z)], new(128, 128, 128), null) { Kind = kind };
SceneDocument Floor(string name, AuthoredSurfaceKind? middle) => SceneDocument.New(name) with
{
    // A 4 x 4 floor, the start in the middle of a plain 512-wide square (rebuilt slots need a landing for portal
    // arrivals), the kind under test on the far corner tile.
    Spawn = new(O + 384, O + 384, Z + 32), Heading = 64,
    Surfaces = [.. Enumerable.Range(0, 16).Select(k => Tile(k % 4, k / 4, k == 15 ? middle : null))],
};

if (args.Length == 0)
{
    // Only supercharge ground needs them, painted or collision-only; the other kinds do not.
    Check(NativeSuperchargeAnimations.Needed(Floor("A", AuthoredSurfaceKind.Supercharge)), "a supercharge face needs the animations");
    Check(!NativeSuperchargeAnimations.Needed(Floor("B", null)), "plain ground does not");
    foreach (var kind in AuthoredSurfaceKind.All.Where(k => k != AuthoredSurfaceKind.Supercharge))
        Check(!NativeSuperchargeAnimations.Needed(Floor("C", kind)), $"kind {kind.Type} does not");
    Check(NativeSuperchargeAnimations.Needed(Floor("D", null) with
    {
        CollisionFaces = [new([new(O, O, Z), new(O + 100, O, Z), new(O, O + 100, Z)], AuthoredSurfaceKind.Supercharge)]
    }), "a collision-only supercharge face needs them");
    Check(NativeSuperchargeAnimations.AnimationIds.SequenceEqual([25, 45]) && NativeSuperchargeAnimations.ByteLength == 13920,
        "animations 25 and 45, 13,920 bytes");
    string message = NativeSuperchargeAnimations.NoRoomMessage("MY LEVEL", 1234);
    Check(message.Contains("MY LEVEL") && message.Contains("supercharge") && message.Contains("13,920") && message.Contains("1,234") &&
        !message.Contains("0x"), "plain refusal wording");

    // Face-table texture rectangles: a synthetic table with every command shape of the renderer grammar.
    static byte[] Descriptor(byte u0, byte v0, byte u1, byte v1, ushort palette, ushort page)
    {
        byte[] d = new byte[12];
        d[0] = u0; d[1] = v0; d[4] = u1; d[5] = v0; d[8] = u0; d[9] = v1; d[10] = u1; d[11] = v1;
        BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(2), palette); BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(6), page);
        return d;
    }
    List<byte> table = [];
    void Command(uint word, int size, int field, byte[]? descriptor)
    {
        byte[] c = new byte[size]; BinaryPrimitives.WriteUInt32LittleEndian(c, word);
        descriptor?.CopyTo(c, field); table.AddRange(c);
    }
    Command(0x00000000, 8, -1, null);                                             // untextured triangle
    Command(0x00000002, 20, 8, Descriptor(96, 64, 127, 95, 0x7BFF, 0x3F));        // textured triangle: the glow tile
    Command(0x80000002, 24, 12, Descriptor(64, 64, 95, 95, 0x7BBF, 0x1F));        // textured quad: its neighbour
    Command(0x80000000, 12, -1, null);                                            // untextured quad
    Command(0x80000004, 20, 8, Descriptor(100, 70, 110, 80, 0x7BFF, 0x3F));       // inside the glow tile again
    byte[] package = new byte[16 + 4 + table.Count];
    BinaryPrimitives.WriteInt32LittleEndian(package.AsSpan(16), table.Count);
    table.ToArray().CopyTo(package, 20);
    var ranges = NativeSuperchargeAnimations.TextureRanges(package, 16, out int commands);
    Check(commands == 3, "three textured commands");
    // Page 0x1F: x 960 (words), y 256; a 4-bit texel is half a byte. Rows of u 64..127 merge into one 32-byte run.
    int Row(int v, int u) => (256 + v) * 1024 + (960 - 512) * 2 + u / 2;
    Check(Enumerable.Range(64, 32).All(v => ranges.Contains((Row(v, 64), 32))), "u 64..127 rows merged per line");
    Check(ranges.Contains((495 * 1024 + (1008 - 512) * 2, 32)) && ranges.Contains((494 * 1024 + (1008 - 512) * 2, 32)),
        "both palettes (0x7BFF, 0x7BBF) kept");
    Check(ranges.Count == 34 && ranges.Zip(ranges.Skip(1)).All(p => p.First.Offset + p.First.Length < p.Second.Offset), "sorted, disjoint");
    byte[] overrun = package.ToArray(); BinaryPrimitives.WriteInt32LittleEndian(overrun.AsSpan(16), table.Count + 4);
    Refuse(() => NativeSuperchargeAnimations.TextureRanges(overrun, 16, out _), "a table past its package is refused");
    byte[] cut = package.ToArray(); BinaryPrimitives.WriteInt32LittleEndian(cut.AsSpan(16), 12);
    Refuse(() => NativeSuperchargeAnimations.TextureRanges(cut, 16, out _), "a command past its table is refused");
    byte[] deep = package.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(deep.AsSpan(20 + 8 + 8 + 6), 0x11F);
    Refuse(() => NativeSuperchargeAnimations.TextureRanges(deep, 16, out _), "an 8-bit texture page is refused");

    // The game's own levels: a supercharge tile in the saved terrain edits (however it was given) needs the records,
    // unless the level is one of the seven that carry them.
    int[] owners = [30, 32, 33, 43, 52, 53, 62];
    Check(Enumerable.Range(10, 55).Where(NativeSuperchargeAnimations.CarriedBy).SequenceEqual(owners), "the seven supercharge levels carry the records");
    string edits = Path.Combine(Path.GetTempPath(), "spyro-supercharge-edits-" + Guid.NewGuid().ToString("N") + ".json");
    void Edits(params (int Type, int Param2, string Mode)[] tiles) => File.WriteAllText(edits, System.Text.Json.JsonSerializer.Serialize(new
    {
        editCount = tiles.Length,
        edits = tiles.Select(t => new Dictionary<string, object>
        {
            ["runtimeKey"] = "60:45:hp", ["structureEditMode"] = t.Mode, ["nativeSurfaceBehaviorEdit"] = true,
            ["nativeSurfaceType"] = t.Type, ["nativeSurfaceParam1"] = 0, ["nativeSurfaceParam2"] = t.Param2
        })
    }));
    try
    {
        Check(!NativeSuperchargeAnimations.NeededByTerrainEdits(10, edits), "no saved edits: nothing needed");
        Edits((4, 0, "none"));
        Check(NativeSuperchargeAnimations.NeededByTerrainEdits(10, edits), "Artisans with a supercharge tile needs the records");
        Check(owners.All(id => !NativeSuperchargeAnimations.NeededByTerrainEdits(id, edits)), "the supercharge levels already have them");
        Edits((0, 1, "none"), (5, 0, "none"), (1, 0, "none"));
        Check(!NativeSuperchargeAnimations.NeededByTerrainEdits(10, edits), "lava, invisible walls and damage need nothing");
        Edits((4, 0, "remove-face"));
        Check(!NativeSuperchargeAnimations.NeededByTerrainEdits(10, edits), "a removed supercharge tile needs nothing");
        Edits((0, 1, "none"), (4, 0, "none"), (0, 0, "remove-face"));
        Check(UniversalLiquidSplashes.LiquidsInTerrainEdits(edits).SequenceEqual([1]) && NativeSuperchargeAnimations.NeededByTerrainEdits(21, edits),
            "the same edits still give the liquids (lava, not the removed water)");
    }
    finally { File.Delete(edits); }
    string retail = NativeSuperchargeAnimations.RetailNoRoomMessage("Dark Passage", 320);
    Check(retail.StartsWith("Dark Passage has supercharge ground") && retail.Contains("13,920") && retail.Contains("320 bytes short") &&
        retail.Contains("Tile Type…") && retail.Contains("8 MB") && !retail.Contains("0x"), "plain retail refusal wording");
    Console.WriteLine($"SUPERCHARGE_FREE_OK checks={checks}");
    return 0;
}

if (args.Length >= 3 && args[0] == "--retail-survey")
{
    string disc = Path.GetFullPath(args[1]);
    var catalog = LevelCatalog.Load(args[2]);
    var animations = await NativeSuperchargeAnimations.CaptureAsync(disc);
    var original = UniversalDiscImage.Read(disc);
    foreach (var level in catalog.Levels.Where(l => l.SourceWadEntry > 0).OrderBy(l => l.LevelId))
    {
        var data = new UniversalLevelData(original.EntryBytes(level.SourceWadEntry));
        uint copy = new UniversalOverlay(original.EntryBytes(level.SourceWadEntry - 1), UniversalExeBindings.Read(original.Exe, level.LevelId)).CopyBuffer;
        string vram;
        try { animations.RequireDestination(animations.Merge(data).Bytes); vram = "glow ok"; }
        catch (InvalidDataException e) { vram = "REFUSED: " + e.Message; }
        var merged = animations.Merge(data);
        bool exceeds8;
        using (GameRam.Use(GameMemoryMode.Emulator8MB)) exceeds8 = GameRam.Exceeds(merged, copy, data, copy);
        Console.WriteLine($"{level.LevelId,2} {level.Key,-14} grow {merged.Bytes.Length - data.Bytes.Length,6} shortfall before {GameRam.Shortfall(data, copy),8} after {GameRam.Shortfall(merged, copy),8} exceeds {GameRam.Exceeds(merged, copy, data, copy)} (8 MB {exceeds8}) {vram}");
    }
    return 0;
}

if (args.Length >= 3 && args[0] == "--source")
{
    string disc = Path.GetFullPath(args[1]);
    var catalog = LevelCatalog.Load(args[2]);
    string scratch = Path.Combine(Path.GetTempPath(), "spyro-supercharge-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(scratch);
    try
    {
        var animations = await NativeSuperchargeAnimations.CaptureAsync(disc);
        var vram = animations.ProtectedVram.ToArray();
        // The glow tile (page 0x1F u 96..127 v 64..95: 16 bytes of each row) and its palette 0x7BFF.
        Check(Enumerable.Range(320, 32).All(y => vram.Any(r => r.Offset <= y * 1024 + 944 && r.Offset + r.Length >= y * 1024 + 960)),
            "the glow tile is kept");
        Check(vram.Any(r => r.Offset <= 495 * 1024 + 992 && r.Offset + r.Length >= 495 * 1024 + 1024), "palette 0x7BFF is kept");

        static int[] Ids(byte[] data, bool exactEnd = true)
        {
            int start = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0x50)), end = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0x54));
            List<int> ids = [];
            for (int at = start; ;)
            {
                int id = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at));
                // The two records keep the terminator at the declared end (no alignment gap), so the strict decoders (Fly
                // Anywhere's among them) still read it. (A composer import of an odd-sized record may add the native
                // four-byte gap: exactEnd false.)
                if (id == -1) { if (exactEnd && at + 4 != end) throw new Exception($"player stream terminator at 0x{at:X}, declared end 0x{end:X} (ids {string.Join(",", ids)})"); break; }
                ids.Add(id); at += 8 + BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at + 4));
                if (at >= end) throw new Exception("player stream has no terminator");
            }
            return [.. ids];
        }
        string RecordHash(byte[] data, int id)
        {
            int at = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0x50));
            while (BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at)) != id) at += 8 + BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at + 4));
            return Convert.ToHexStringLower(SHA256.HashData(data.AsSpan(at, 8 + BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at + 4)))));
        }
        int ModelBytes(byte[] data) => new UniversalLevelData(data).Subfiles()[2].Length;
        void RequireSupercharge(byte[] data, string what, int[]? more = null)
        {
            Check(Ids(data, more == null).SequenceEqual([18, 19, 29, 40, 25, 45, .. more ?? []]), what + ": Town's player stream plus 25 and 45" +
                $" (has {string.Join(",", Ids(data, false))})");
            Check(RecordHash(data, 25) == "d5f15368f5fa3fa88022fa3c7cac77d13549dee0ba3c612cc926ee67c8663d52" &&
                RecordHash(data, 45) == "d3cf9d6358ce272a5dd5c49c7591f702879a75545655619b861d62ba65335742", what + ": the original records");
        }

        // File > New Level (ID65): with and without supercharge ground.
        var plain = await NativeFreshMainCompilation.CaptureAsync(disc, Floor("PLAIN", null), Path.Combine(scratch, "plain.bin"));
        File.Delete(Path.Combine(scratch, "plain.bin"));
        var super = await NativeFreshMainCompilation.CaptureAsync(disc, Floor("SUPER", AuthoredSurfaceKind.Supercharge), Path.Combine(scratch, "super.bin"));
        byte[] plainData = plain.CopyLevelData(), superData = super.CopyLevelData();
        Check(plain.Supercharge == null && Ids(plainData).SequenceEqual([18, 19, 29, 40]), "no supercharge: Town's stream unchanged");
        Check(super.Supercharge != null, "supercharge ground carries the certificate");
        RequireSupercharge(superData, "New Level");
        Check(ModelBytes(superData) - ModelBytes(plainData) is >= 13920 and < 13920 + 0x800, "the models grow by the two records (sector-rounded)");
        super.Supercharge!.RequireDestination(superData); checks++;
        Check(super.Supercharge.Merge(new UniversalLevelData(superData.ToArray())).Bytes.AsSpan().SequenceEqual(superData), "merging again changes nothing");
        // Tampering: a changed record byte, a missing record, a changed glow texel are all refused.
        {
            int at = BinaryPrimitives.ReadInt32LittleEndian(superData.AsSpan(0x50));
            while (BinaryPrimitives.ReadInt32LittleEndian(superData.AsSpan(at)) != 45) at += 8 + BinaryPrimitives.ReadInt32LittleEndian(superData.AsSpan(at + 4));
            byte[] changed = superData.ToArray(); changed[at + 0x60] ^= 1;
            Refuse(() => super.Supercharge.RequireDestination(changed), "a changed animation 45 is refused");
            Refuse(() => super.Supercharge.RequireDestination(plainData), "a level without the records is refused");
            byte[] texel = superData.ToArray(); texel[new UniversalLevelData(texel).Subfiles()[0].Offset + 330 * 1024 + 950] ^= 0x11;
            Refuse(() => super.Supercharge.RequireDestination(texel), "a changed glow texel is refused");
        }
        // Objects composed into the level keep both records and the glow texture (a spring chest, and a Wizard Peak
        // wizard, whose own level has supercharge ground).
        var donors = new UniversalSourceLibrary(disc, catalog);
        int G(float editor) => (int)(editor * 16);
        var composed = super.Compose(donors, [new UniversalMobyPlacement("peacekeepers", 70, G(O + 640), G(O + 640), G(Z)),
            new UniversalMobyPlacement("wizardpeak", 6, G(O + 640), G(O + 900), G(Z))]);
        // The wizard's own hurt reactions (Wizard Peak's records) follow the supercharge records.
        RequireSupercharge(composed.LevelData, "New Level with objects", [.. Ids(composed.LevelData, false).Skip(6)]);
        Check(Ids(composed.LevelData, false).Length > 6, "the wizard brought its Spyro reactions after the supercharge records");
        Console.WriteLine("New Level with a spring chest and a wizard: player stream " + string.Join(",", Ids(composed.LevelData, false)));
        super.Supercharge.RequireDestination(composed.LevelData); checks++;
        File.Delete(Path.Combine(scratch, "super.bin"));
        // A collision-only supercharge face is enough.
        var collisionOnly = Floor("COLLISION", null) with
        {
            CollisionFaces = [new([new(O + 600, O + 600, Z + 40), new(O + 700, O + 600, Z + 40), new(O + 600, O + 700, Z + 40)], AuthoredSurfaceKind.Supercharge)]
        };
        var hidden = await NativeFreshMainCompilation.CaptureAsync(disc, collisionOnly, Path.Combine(scratch, "hidden.bin"));
        File.Delete(Path.Combine(scratch, "hidden.bin"));
        RequireSupercharge(hidden.CopyLevelData(), "collision-only supercharge");

        // Start Blank in Tree Tops, a level that has supercharge ground and both records: the rebuilt body is the
        // kernel's, whose player stream is Town's, so it needs them added like any built level.
        var original = UniversalDiscImage.Read(disc);
        var treeTops = catalog.FindByKey("treetops") ?? throw new Exception("Tree Tops is missing from the catalog");
        Check(treeTops.LevelId == 43, "Tree Tops is level 43");
        Check(Ids(original.EntryBytes(treeTops.SourceWadEntry)).Contains(25) && Ids(original.EntryBytes(treeTops.SourceWadEntry)).Contains(45),
            "retail Tree Tops carries 25 and 45");
        var binding = new SceneTargetBinding("treetops", 43, treeTops.SourceWadEntry, treeTops.SourceWadEntry - 1,
            UniversalSourceLibrary.OriginalUsaSha256, new UniversalLevelData(original.EntryBytes(treeTops.SourceWadEntry)).Scene().RowCount);
        var blank = await NativeClearedLevelCompilation.CaptureAsync(disc, Floor("TREE TOPS", null) with { TargetBinding = binding }, catalog,
            Path.Combine(scratch, "blank.bin"));
        File.Delete(Path.Combine(scratch, "blank.bin"));
        Check(blank.Supercharge == null && Ids(blank.CopyLevelData()).SequenceEqual([18, 19, 29, 40]), "rebuilt Tree Tops without supercharge: Town's stream");
        var rebuilt = await NativeClearedLevelCompilation.CaptureAsync(disc, Floor("TREE TOPS", AuthoredSurfaceKind.Supercharge) with { TargetBinding = binding },
            catalog, Path.Combine(scratch, "rebuilt.bin"));
        File.Delete(Path.Combine(scratch, "rebuilt.bin"));
        Check(rebuilt.Supercharge != null, "rebuilt Tree Tops with supercharge carries the certificate");
        RequireSupercharge(rebuilt.CopyLevelData(), "rebuilt Tree Tops");
        rebuilt.Supercharge!.RequireDestination(rebuilt.CopyLevelData()); checks++;

        // The game's own levels (Tile Type… supercharge ramp; charging down it crashed Artisans the same way): each either
        // carries both records or takes them exactly, its texture memory already holding the glow texture; five have no
        // room for them in the 2 MB mode (the 8 MB mode fits them) and are refused in plain words.
        string[] tight = ["peacekeepers", "beastmakers", "mistybog", "dreamweavers", "darkpassage"];
        int added = 0, carried = 0, refused = 0;
        foreach (var level in catalog.Levels.Where(l => l.SourceWadEntry > 0 && l.LevelId != 65).OrderBy(l => l.LevelId))
        {
            byte[] bytes = original.EntryBytes(level.SourceWadEntry);
            var data = new UniversalLevelData(bytes);
            uint copy = new UniversalOverlay(original.EntryBytes(level.SourceWadEntry - 1), UniversalExeBindings.Read(original.Exe, level.LevelId)).CopyBuffer;
            int[] before = Ids(bytes, false);
            if (NativeSuperchargeAnimations.CarriedBy(level.LevelId))
            {
                Check(before.Contains(25) && before.Contains(45) &&
                    animations.MergeRetail(data, copy, level.DisplayName).Bytes.AsSpan().SequenceEqual(bytes), $"{level.DisplayName} carries both records: unchanged");
                carried++;
                continue;
            }
            Check(!before.Contains(25) && !before.Contains(45), $"{level.DisplayName} has neither record");
            if (tight.Contains(level.Key))
            {
                try { animations.MergeRetail(data, copy, level.DisplayName); throw new Exception($"{level.DisplayName} took the records without room"); }
                catch (InvalidDataException e)
                {
                    Check(e.Message.StartsWith(level.DisplayName + " has supercharge ground") && e.Message.Contains("bytes short") && !e.Message.Contains("0x"),
                        $"{level.DisplayName}: plain refusal ({e.Message})");
                }
                using (GameRam.Use(GameMemoryMode.Emulator8MB))
                    RequireRetail(animations.MergeRetail(data, copy, level.DisplayName).Bytes, before, level.DisplayName + " (8 MB)");
                refused++;
                continue;
            }
            RequireRetail(animations.MergeRetail(data, copy, level.DisplayName).Bytes, before, level.DisplayName);
            added++;
        }
        Check(added == 23 && carried == 7 && refused == 5, $"23 levels take the records, 7 carry them, 5 lack room ({added}/{carried}/{refused})");
        void RequireRetail(byte[] merged, int[] before, string what)
        {
            Check(Ids(merged).SequenceEqual([.. before, 25, 45]), $"{what}: its player stream plus 25 and 45");
            Check(RecordHash(merged, 25) == "d5f15368f5fa3fa88022fa3c7cac77d13549dee0ba3c612cc926ee67c8663d52" &&
                RecordHash(merged, 45) == "d3cf9d6358ce272a5dd5c49c7591f702879a75545655619b861d62ba65335742", what + ": the original records");
            animations.RequireDestination(merged); checks++;
        }
        // Tile Type… measures the original level once: Dark Passage is 320 bytes short, Artisans fits, Tree Tops has them.
        Check(await NativeSuperchargeAnimations.RetailShortfallAsync(disc, catalog.FindByKey("darkpassage")!) == 320 &&
            await NativeSuperchargeAnimations.RetailShortfallAsync(disc, catalog.FindByKey("artisans")!) == 0 &&
            await NativeSuperchargeAnimations.RetailShortfallAsync(disc, treeTops) == 0, "Tile Type…'s room check");

        // Create BIN's object stage (UniversalMobyExport): supercharge ground alone, with an object, and without room.
        var artisans = catalog.FindByKey("artisans")!;
        byte[] artisansBefore = original.EntryBytes(artisans.SourceWadEntry);
        string alone = Path.Combine(scratch, "artisans-supercharge.bin");
        var only = UniversalMobyExport.Apply(donors, disc, alone, catalog, new Dictionary<string, IReadOnlyList<UniversalMobyPlacement>>(),
            superchargeLevels: ["artisans", "treetops"]);
        Check(only.SuperchargeLevelKeys.SequenceEqual(["artisans"]) && only.Levels.Count == 0, "only Artisans needed the records; no objects composed");
        {
            var image = UniversalDiscImage.Read(alone);
            RequireRetail(image.EntryBytes(artisans.SourceWadEntry), Ids(artisansBefore, false), "Artisans after Create BIN");
            Check(image.Exe.AsSpan().SequenceEqual(original.Exe) && image.EntryBytes(artisans.SourceWadEntry - 1).AsSpan().SequenceEqual(original.EntryBytes(artisans.SourceWadEntry - 1)) &&
                image.EntryBytes(treeTops.SourceWadEntry).AsSpan().SequenceEqual(original.EntryBytes(treeTops.SourceWadEntry)),
                "the executable, Artisans' code and Tree Tops are untouched");
        }
        File.Delete(alone);
        // A Stone Hill object brings its model and textures; they go round the glow texture.
        string withObject = Path.Combine(scratch, "artisans-supercharge-object.bin");
        var composedRetail = UniversalMobyExport.Apply(donors, disc, withObject, catalog, new Dictionary<string, IReadOnlyList<UniversalMobyPlacement>>
        {
            ["artisans"] = [new UniversalMobyPlacement("stonehill", 1, G(5758), G(1619), G(576))]
        }, superchargeLevels: ["artisans"]);
        var objectLevel = composedRetail.Levels.Single().Compose;
        Check(objectLevel.NewRowIndices.Count == 1 && objectLevel.Classes.Any(c => c.ModelImported) && objectLevel.VramBytesUsed > 0 &&
            composedRetail.SuperchargeLevelKeys.SequenceEqual(["artisans"]), "the Stone Hill object (model and textures) and the records together");
        {
            byte[] final = UniversalDiscImage.Read(withObject).EntryBytes(artisans.SourceWadEntry);
            Check(Ids(final, false).Take(Ids(artisansBefore, false).Length + 2).SequenceEqual([.. Ids(artisansBefore, false), 25, 45]), "Artisans with the object keeps 25 and 45");
            animations.RequireDestination(final); checks++;
        }
        File.Delete(withObject);
        string noRoom = Path.Combine(scratch, "darkpassage-supercharge.bin");
        try
        {
            UniversalMobyExport.Apply(donors, disc, noRoom, catalog, new Dictionary<string, IReadOnlyList<UniversalMobyPlacement>>(), superchargeLevels: ["darkpassage"]);
            throw new Exception("Dark Passage took the records without room");
        }
        catch (InvalidDataException e)
        {
            Check(e.Message.StartsWith("Dark Passage has supercharge ground") && e.Message.Contains("320 bytes short") && !File.Exists(noRoom),
                "Create BIN refuses Dark Passage in plain words and writes nothing: " + e.Message);
        }
        Console.WriteLine($"SUPERCHARGE_SOURCE_OK checks={checks}");
        return 0;
    }
    finally { Directory.Delete(scratch, true); }
}
Console.Error.WriteLine("usage: [--source <original USA.bin> <workspace with spyro-level-catalog.json>]");
return 2;

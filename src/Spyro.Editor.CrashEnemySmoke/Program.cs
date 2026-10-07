using System.Buffers.Binary;
using Spyro.Editor.Core.Crash;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

// Crash enemies (frog, knight, wizard): free checks (behaviour assembles, class IDs, catalog, vertex encoding),
// source checks (USA disc + Crash disc: converted models, art, sounds, class IDs unused by every retail level,
// behaviour distances against the built models), and tools:
//   --assets USA.bin workspace Crash.bin                 print each converted model's numbers
//   --model Crash.bin file sha model size outPrefix anim[:i] ...   convert one model to .pkg/.vram files
//   --export USA.bin workspace Crash.bin out.bin layout.json      a test image: {"level", "objects": [{"id","x","y","z","yaw"?}]}
int checks = 0;
void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); checks++; }
bool Refuses(Action action) { try { action(); return false; } catch (InvalidDataException) { return true; } }

if (args.Length >= 8 && args[0] == "--model")
{
    var nsf = new CrashWarpedDisc(args[1]).Level(args[2], args[3]);
    var anims = args.Skip(7).Select(a => new CrashSpyroModel.Animation(a.Split(':')[0], a.EndsWith(":i"))).ToList();
    var r = CrashSpyroModel.Build(nsf, args[4], anims, new(UniversalCrashEnemies.CrashUnitsPerSpyroUnit / double.Parse(args[5])));
    File.WriteAllBytes(args[6] + ".pkg", r.Package); File.WriteAllBytes(args[6] + ".vram", r.Vram);
    Console.WriteLine($"{r.Package.Length} bytes, {r.VertexCount} vertices, {r.FaceCount} faces, height {r.Height}, capsule radius {r.Radius}, frames {string.Join(",", r.FrameCounts)}, scale x{1 << r.ScaleShift}");
    return;
}
if (args.Length == 4 && args[0] == "--assets")
{
    var lib = new UniversalSourceLibrary(args[1], LevelCatalog.Load(args[2]));
    foreach (var asset in UniversalCrashEnemies.Assets(lib, new CrashWarpedDisc(args[3])).SelectMany(x => new[] { x }.Concat(x.Companions)))
    {
        var m = Decoded.Read(asset.Package);
        Console.WriteLine($"{asset.Id} class {asset.ClassId:X3}: {asset.Package.Length} bytes, anims {m.Frames.Count}, verts {m.Vertices}, faces {m.NearFaces}/{m.FarFaces}, " +
            $"scale x{m.Scale}, capsule top {m.CapsuleTop} radius {m.CapsuleRadius}, speed {asset.RowTemplate[0x41]}, reward {asset.RowTemplate[0x53]:X2}");
    }
    return;
}
if (args.Length == 6 && args[0] == "--export")
{
    var catalog = LevelCatalog.Load(args[2]);
    var lib = new UniversalSourceLibrary(args[1], catalog);
    var disc = new CrashWarpedDisc(args[3]);
    using var layout = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(args[5]));
    string levelKey = layout.RootElement.GetProperty("level").GetString()!;
    var placements = layout.RootElement.GetProperty("objects").EnumerateArray().Select(b => new UniversalMobyPlacement(UniversalCrashEnemies.SourceKey, -1,
        b.GetProperty("x").GetInt32() * 16, b.GetProperty("y").GetInt32() * 16, b.GetProperty("z").GetInt32() * 16,
        Yaw: b.TryGetProperty("yaw", out var yaw) ? yaw.GetInt32() : 0, SpecialId: b.GetProperty("id").GetString())).ToList();
    var result = UniversalMobyExport.Apply(lib, args[1], args[4], catalog,
        new Dictionary<string, IReadOnlyList<UniversalMobyPlacement>> { [levelKey] = placements }, crash: disc);
    Console.WriteLine($"exported {result.OutputImagePath} sha256={result.OutputImageSha256} treasure {result.Levels.Single().Compose.TreasureDelta:+0;-0}");
    foreach (var c in result.Levels.Single().Compose.Classes.Where(c => UniversalCrashEnemies.IsClass(c.DestinationClassId) || UniversalCrashBoxes.IsClass(c.DestinationClassId)))
        Console.WriteLine($"  class 0x{c.DestinationClassId:X3} model={c.ModelImported} bytes={c.ModelBytes} tex={c.Textures} snd={c.Sounds}");
    Console.WriteLine("CRASH_ENEMY_EXPORT_OK");
    return;
}

// ---- Free checks.
var enemies = UniversalCrashEnemies.Enemies;
var all = UniversalCrashEnemies.AllClasses;
Check(enemies.Count == 3 && all.Count == 4 && all.Distinct().Count() == 4, "frog, knight, wizard and the wizard's robe-off model");
Check(enemies.All(e => UniversalCrashEnemies.IsPlaceableClass(e.Class)) && !UniversalCrashEnemies.IsPlaceableClass(UniversalCrashEnemies.Wizard.Companion!.Class),
    "the robe-off model is not placeable on its own");
Check(!all.Intersect(UniversalCrashBoxes.AllClasses).Any(), "no clash with the Crash boxes");
int[] others = [UniversalHighValueGems.TealClass, UniversalHighValueGems.RainbowClass, UniversalDragonflyEggs.SignClass, UniversalSpecialBehaviors.EggHeaderClass,
    .. UniversalDragonflyEggs.Variants.Select(v => v.Class)];
Check(!all.Intersect(others).Any(), "no clash with the other editor objects");
foreach (var e in enemies)
{
    string id = UniversalSpecialCatalog.SpecialIdForClass(e.Class)!;
    Check(id == e.Id && UniversalSpecialCatalog.RequiresCrashSource(id) && !UniversalSpecialCatalog.RequiresJapaneseSource(id), $"{e.Id} needs the Crash disc only");
    Check(UniversalSpecialCatalog.DisplayName(e.Class) == e.Name && e.Name.StartsWith("Crash "), $"{e.Id} is named {e.Name}");
    Check(e.Sounds.Length <= 16 && e.Sounds[UniversalCrashEnemies.FlySlot] == "fli0A" && e.Sounds[UniversalCrashEnemies.BounceSlot] == "bnc0A", $"{e.Id}: shared sound slots");
    foreach (var m in e.Companion == null ? [e.Main] : new[] { e.Main, e.Companion })
        Check(m.Animations.SelectMany(a => a.Sounds ?? []).All(s => s.Slot < e.Sounds.Length), $"{e.Id}: animation sounds name existing slots");
}
Check(UniversalCrashEnemies.TreasureValue(UniversalCrashEnemies.Frog.Class) == 0 && UniversalCrashEnemies.TreasureValue(UniversalCrashEnemies.Knight.Class) == 2 &&
    UniversalCrashEnemies.TreasureValue(UniversalCrashEnemies.Wizard.Class) == 5, "frog: butterfly; knight: green gem (2); wizard: blue gem (5)");
var entries = UniversalSpecialCatalog.Entries(true, true).Where(e => e.Category == "Enemies" && e.Default.SourceLevelKey == UniversalCrashEnemies.SourceKey).ToList();
Check(entries.Count == 3 && entries.All(e => e.RestrictionReason == null && e.SubCategory == "Crash Bandicoot") &&
    entries.Select(e => e.ClassId).SequenceEqual(enemies.Select(e => e.Class)), "catalog: Enemies › Crash Bandicoot: frog, knight, wizard");
Check(UniversalSpecialCatalog.Entries(true).Where(e => e.Category == "Enemies" && e.Default.SourceLevelKey == UniversalCrashEnemies.SourceKey).All(e => e.RestrictionReason != null),
    "without the Crash disc the enemies explain what is needed");
Check(Refuses(() => UniversalSpecialBehaviors.CrashEnemyUpdate([0x57], "bad")), "retail class refused");
Check(Refuses(() => UniversalSpecialBehaviors.CrashEnemyUpdate([UniversalCrashEnemies.Frog.Class, UniversalCrashEnemies.Frog.Class], "bad")), "duplicate refused");
Check(Refuses(() => UniversalSpecialBehaviors.CrashEnemyUpdate([UniversalCrashEnemies.Wizard.Class], "bad")), "a wizard without his robe-off model refused");
foreach (var set in new[] { all, [UniversalCrashEnemies.Frog.Class], [UniversalCrashEnemies.Knight.Class], [UniversalCrashEnemies.Wizard.Class, UniversalCrashEnemies.Wizard.Companion!.Class] })
{
    var (code, labels) = UniversalBehaviorBundleBuilder.Assemble([UniversalSpecialBehaviors.CrashEnemyUpdate(set, "crash_enemy_upd")], 0x80090000);
    Check(code.Length > 0 && code.Length <= UniversalSpecialCatalog.CrashEnemyCodeBytes, $"behaviour fits its catalog estimate ({code.Length} bytes for {set.Count} classes)");
    Check(labels["crash_enemy_upd"] == 0x80090000, "entry label at the base");
}
// Vertex words: the stored point is the requested one give or take the sign/flag bits, deltas reach within half a step.
var rng = new Random(7);
for (int n = 0; n < 200; n++)
{
    var pts = Enumerable.Range(0, 40).Select(_ => (rng.Next(-1000, 1001), rng.Next(-1000, 1001), rng.Next(-1000, 401))).ToArray();
    // Every other point close to the one before (delta-coded), all within the +-1000 the converter keeps to.
    for (int k = 1; k < pts.Length; k += 2)
        pts[k] = (Math.Clamp(pts[k - 1].Item1 + rng.Next(-40, 41), -1000, 1000), Math.Clamp(pts[k - 1].Item2 + rng.Next(-40, 41), -1000, 1000),
            Math.Clamp(pts[k - 1].Item3 + rng.Next(-40, 41), -1000, 400));
    foreach (int shift in new[] { 1, 2, 3 })
    {
        var (bytes, absolute) = CrashSpyroModel.Encode(pts, shift);
        var back = Decoded.Unpack(bytes, absolute, pts.Length, shift);
        Check(back.Zip(pts).All(z => Math.Abs(z.First.X - z.Second.Item1) <= Math.Max(2, 1 << shift) && Math.Abs(z.First.Y - z.Second.Item2) <= Math.Max(2, 1 << shift) &&
            Math.Abs(z.First.Z - z.Second.Item3) <= Math.Max(4, 1 << shift)), $"vertex round trip (shift {shift})");
    }
}
if (args.Length == 0) { Console.WriteLine($"CRASH_ENEMY_FREE_OK checks={checks}"); return; }

// ---- Source checks: USA.bin catalog-workspace Crash.bin.
if (args.Length != 3) throw new ArgumentException("USA.bin catalog-workspace Crash.bin");
var catalog2 = LevelCatalog.Load(args[1]);
var library = new UniversalSourceLibrary(args[0], catalog2);
var disc2 = new CrashWarpedDisc(args[2]);
var assets = UniversalCrashEnemies.Assets(library, disc2);
Check(ReferenceEquals(assets, UniversalCrashEnemies.Assets(library, disc2)), "asset cache reused");
Check(assets.Select(a => a.Id).SequenceEqual(enemies.Select(e => e.Id)) && assets.All(a => a.Behavior == UniversalSpecialBehavior.CrashEnemy && a.SourceKey == UniversalCrashEnemies.SourceKey),
    "one asset per enemy");
var wizard = assets.Single(a => a.Id == UniversalCrashEnemies.Wizard.Id);
Check(wizard.Companions.Count == 1 && wizard.Companions[0].ClassId == UniversalCrashEnemies.Wizard.Companion!.Class, "the wizard brings his robe-off model");
Check(ReferenceEquals(assets[0].SourceVram, assets[1].SourceVram) && ReferenceEquals(wizard.SourceVram, wizard.Companions[0].SourceVram) &&
    !ReferenceEquals(assets[0].SourceVram, wizard.SourceVram), "one shared texture image per Crash level file");
foreach (var (asset, enemy) in assets.SelectMany(a => new[] { a }.Concat(a.Companions)).Select(a => (a, UniversalCrashEnemies.ForClass(a.ClassId)!)))
{
    var model = enemy.Class == asset.ClassId ? enemy.Main : enemy.Companion!;
    var nsf = disc2.Level(enemy.File, enemy.Sha256);
    var m = Decoded.Read(asset.Package);
    Check(m.Frames.Select(f => f.Count).SequenceEqual(model.Animations.Select(a => nsf.Entry(a.Name).Items.Count)), $"{asset.Id}: every frame of each Crash animation");
    Check(m.Vertices is > 0 and <= 255 && m.NearFaces > 0 && m.FarFaces == m.NearFaces, $"{asset.Id}: {m.Vertices} vertices, {m.NearFaces} faces with a far copy each");
    Check(m.MaxStored <= 1000, $"{asset.Id}: stored coordinates within the game's range ({m.MaxStored})");
    Check(m.FacesValid, $"{asset.Id}: faces name existing vertices and colours");
    Check(m.Textures.All(t => t.PageX >= 8 && t.PageY == 0 && t.Depth <= 1 && t.ClutY >= 256), $"{asset.Id}: art in the image's top half, palettes from row 256");
    Check(m.Frames.SelectMany(f => f).All(s => s == 0xFF || s < enemy.Sounds.Length), $"{asset.Id}: frame sounds name the enemy's slots");
    Check(asset.Package.AsSpan(4, 16).ToArray().Select((b, i) => i < enemy.Sounds.Length ? b == i : b == 0xFF).All(x => x), $"{asset.Id}: sound slots 0..{enemy.Sounds.Length - 1}");
    Check(BinaryPrimitives.ReadUInt16LittleEndian(asset.RowTemplate.AsSpan(0x36)) == asset.ClassId && asset.RowTemplate[0x53] == enemy.Reward &&
        asset.RowTemplate[0x41] == 32 && asset.RowTemplate.AsSpan(0x3C, 5).ToArray().All(b => b == 0), $"{asset.Id}: row starts animation 0 at speed 32 with its reward");
    if (asset.ClassId == enemy.Class)
    {
        // Behaviour distances against the built model: height, and where Spyro stands on its capsule (+556,
        // measured in the emulator on the knight).
        Check(Math.Abs(enemy.Tuning.Top - m.Height) <= 40, $"{asset.Id}: tuned height {enemy.Tuning.Top} vs model {m.Height}");
        Check(Math.Abs(enemy.Tuning.Stand - (m.CapsuleTop + 556)) <= 60, $"{asset.Id}: tuned standing height {enemy.Tuning.Stand} vs capsule top {m.CapsuleTop} + 556");
        Check(enemy.Tuning.Touch > m.CapsuleRadius && enemy.Tuning.Stomp > m.CapsuleRadius, $"{asset.Id}: touch and stomp reach beyond the capsule");
    }
    var layout = new Spyro.Editor.Core.Exporting.NativeLevelSoundLayout(asset.SourceData.Bytes);
    for (int i = 0; i < enemy.Sounds.Length; i++)
    {
        var dsc = layout.Sound.Descriptors[i];
        Check(dsc.IsSupportedOneShot && dsc.Pitch == 0x400 && dsc.Sample.CopyBytes().SequenceEqual(nsf.Sound(enemy.Sounds[i])), $"{asset.Id}: sound {i} is Crash's {enemy.Sounds[i]}");
    }
}
// Class IDs: unused by every retail level's registrations and rows, the shared model table and any small
// immediate in the executable and every overlay (the boxes' rule).
HashSet<int> used = [.. library.SharedClasses];
void Immediates(byte[] bytes)
{
    for (int o = 0; o + 4 <= bytes.Length; o += 4)
    {
        uint w = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(o));
        if (UniversalMips.Op(w) is 9 or 10 or 11 or 12 or 13 or 14 && UniversalMips.Imm(w) is > 0 and < 0x200) used.Add(UniversalMips.Imm(w));
    }
}
Immediates(library.Disc.Exe.ToArray());
int levels = 0;
foreach (var level in catalog2.Levels.Where(l => l.SourceWadEntry > 0))
{
    var (_, data, overlay) = library.Get(level.Key);
    foreach (var r in data.Registrations()) used.Add(r.ClassId);
    for (int i = 0; i < data.Scene().RowCount; i++) used.Add(BinaryPrimitives.ReadUInt16LittleEndian(data.Row(i).AsSpan(0x36)));
    Immediates(overlay.Bytes.ToArray());
    levels++;
}
Check(levels >= 35, $"every retail level scanned ({levels})");
Check(all.All(c => !used.Contains(c)), "enemy classes unused by retail code and data: " + string.Join(",", all.Where(used.Contains).Select(c => $"0x{c:X3}")));
UniversalCrashBoxes.RequireDestination(library.Get("darkhollow").Overlay, library.Disc.Exe);
Console.WriteLine($"CRASH_ENEMY_SOURCE_OK checks={checks}");

/// <summary>A Spyro animated object package read back the way the game reads it.</summary>
sealed record Decoded(int Vertices, int NearFaces, int FarFaces, int Scale, int MaxStored, int Height, bool FacesValid,
    int CapsuleTop, int CapsuleRadius, IReadOnlyList<IReadOnlyList<int>> Frames, IReadOnlyList<(int PageX, int PageY, int Depth, int ClutY)> Textures)
{
    public static Decoded Read(byte[] p)
    {
        int I(int o) => BinaryPrimitives.ReadInt32LittleEndian(p.AsSpan(o));
        int anims = I(0), md = I(0x34), h0 = I(0x38), count = p[h0 + 8], colours = BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(h0 + 2));
        int maxStored = 0, height = 0;
        List<IReadOnlyList<int>> frames = [];
        for (int a = 0; a < anims; a++)
        {
            int h = I(0x38 + 4 * a), n = BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(h)), shift = p[h + 6] + 1, scale = 1 << p[h + 5];
            if (p[h + 8] != count || p[h + 5] != p[h0 + 5] || I(h + 0x14) != I(h0 + 0x14)) throw new InvalidDataException("animations disagree");
            List<int> sounds = [];
            for (int f = 0; f < n; f++)
            {
                uint packed = (uint)I(h + 0x24 + 8 * f), aux = (uint)I(h + 0x28 + 8 * f);
                if ((aux & 0xFFFF) != 0) throw new InvalidDataException("unexpected face patch");
                int start = md + (int)(packed & 0x1FFFFF), absolute = (int)(aux >> 24);
                var v = Unpack(p.AsSpan(start).ToArray(), absolute, count, shift);
                maxStored = Math.Max(maxStored, v.Max(q => Math.Max(Math.Abs(q.X), Math.Max(Math.Abs(q.Y), Math.Abs(q.Z)))));
                if (a == 0 && f == 0) height = -v.Min(q => q.Z) * scale;
                sounds.Add((int)(packed >> 24));
            }
            frames.Add(sounds);
        }
        bool valid = true; int near = 0, far = 0;
        List<(int, int, int, int)> textures = [];
        foreach (int table in new[] { 0x14, 0x1C })
        {
            int t = md + I(h0 + table), end = t + 4 + I(t);
            for (int q = t + 4; q < end;)
            {
                uint w = (uint)I(q), c = (uint)I(q + 4);
                bool quad = (w & 0x80000000) != 0, tex = (w & 2) != 0;
                valid &= !quad && new[] { w >> 22 & 511, w >> 13 & 511, w >> 4 & 511 }.All(i => i < count) && new[] { c >> 19 & 511, c >> 10 & 511, c >> 1 & 511 }.All(i => i < colours);
                if (tex)
                {
                    int page = BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(q + 14)), clut = BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(q + 10));
                    textures.Add((page & 15, page >> 4 & 1, page >> 7 & 3, clut >> 6));
                }
                q += tex ? 20 : 8;
                if (table == 0x14) near++; else far++;
            }
        }
        int trailer = (int)((uint)I(0x14) & 0x7FFFFFFF);
        return new(count, near, far, 1 << p[h0 + 5], maxStored, height, valid,
            BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(trailer)), BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(trailer + 6)), frames, textures);
    }

    public static List<(int X, int Y, int Z)> Unpack(byte[] data, int absoluteCount, int count, int shift)
    {
        List<(int, int, int)> v = [];
        int abs = 0, delta = absoluteCount * 4, x = 0, y = 0, z = 0, flag = 0;
        for (int i = 0; i < count; i++)
        {
            int w;
            if (flag == 0)
            {
                if (abs >= absoluteCount * 4) throw new InvalidDataException("absolute vertices overrun");
                w = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(abs)); abs += 4;
                x = w >> 21; y = (w << 10) >> 21; z = (w << 20) >> 19;
            }
            else
            {
                w = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(delta)); delta += 2;
                x += (w >> 11) << shift; y -= ((w << 21) >> 27) << shift; z -= ((w << 26) >> 27) << shift;
            }
            v.Add((x, y, z)); flag = w & 1;
        }
        if (abs != absoluteCount * 4) throw new InvalidDataException("absolute vertices left over");
        return v;
    }
}

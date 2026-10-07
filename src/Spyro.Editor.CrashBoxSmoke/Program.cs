using System.Buffers.Binary;
using Spyro.Editor.Core.Crash;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

// Crash boxes: free checks (behaviour assembles, class IDs, catalog), source checks (USA disc + Crash disc:
// box art, models, class IDs unused by every retail level), and --export (a test image with boxes in a level).
int checks = 0;
void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); checks++; }
bool Refuses(Action action) { try { action(); return false; } catch (InvalidDataException) { return true; } }

var all = UniversalCrashBoxes.AllClasses;
Check(all.Distinct().Count() == 11, "eleven distinct box classes");
Check(all.All(UniversalCrashBoxes.IsClass) && !UniversalCrashBoxes.IsClass(UniversalCrashBoxes.BasketClass), "box classes recognised");
Check(UniversalCrashBoxes.Crates.Select(c => c.Reward).SequenceEqual(new byte[] { 0xFF, 0x53, 0x54, 0x55, 0x56, 0x57 }), "crate rewards: butterfly then the five gem classes");
Check(UniversalCrashBoxes.TreasureValue(UniversalCrashBoxes.Crates[0].Class) == 0, "a butterfly crate adds no treasure");
foreach (var c in UniversalCrashBoxes.Crates.Skip(1))
    Check(GemValue.TryFromIdByte(c.Reward, out var g) && UniversalCrashBoxes.TreasureValue(c.Class) == g.Value && g.Value > 0, $"{c.Name} adds its gem's value");
Check(new[] { 1, 2, 5, 10, 25 }.SequenceEqual(UniversalCrashBoxes.Crates.Skip(1).Select(c => UniversalCrashBoxes.TreasureValue(c.Class))), "gem values 1, 2, 5, 10, 25");
foreach (int c in all.Where(UniversalCrashBoxes.IsPlaceableClass))
{
    string id = UniversalSpecialCatalog.SpecialIdForClass(c)!;
    Check(UniversalCrashBoxes.IsId(id) && UniversalSpecialCatalog.RequiresCrashSource(id) && !UniversalSpecialCatalog.RequiresJapaneseSource(id), $"{id} needs the Crash disc only");
}
Check(UniversalCrashBoxes.TntClasses.Skip(1).All(c => !UniversalCrashBoxes.IsPlaceableClass(c)), "countdown faces are not placeable on their own");
var entries = UniversalSpecialCatalog.Entries(true, true).Where(e => e.Default.SourceLevelKey == UniversalCrashBoxes.SourceKey && UniversalCrashBoxes.IsClass(e.ClassId)).ToList();
Check(entries.Count == 3 && entries[0].Variants.Count == 6 && entries.All(e => e.RestrictionReason == null), "catalog: crate (6 contents), TNT, Nitro");
Check(UniversalSpecialCatalog.Entries(true).Where(e => e.Default.SourceLevelKey == UniversalCrashBoxes.SourceKey).All(e => e.RestrictionReason != null), "without the Crash disc the boxes explain what is needed");
Check(Refuses(() => UniversalSpecialBehaviors.CrashBoxUpdate([0x57], "bad")), "retail class refused");
Check(Refuses(() => UniversalSpecialBehaviors.CrashBoxUpdate([UniversalCrashBoxes.TntClass, UniversalCrashBoxes.TntClass], "bad")), "duplicate refused");
var (code, labels) = UniversalBehaviorBundleBuilder.Assemble([UniversalSpecialBehaviors.CrashBoxUpdate(all, "crash_box_upd")], 0x80090000);
Check(code.Length > 0 && code.Length <= UniversalSpecialCatalog.CrashBoxCodeBytes, $"behaviour fits its catalog estimate ({code.Length} bytes)");
Check(labels["crash_box_upd"] == 0x80090000, "entry label at the base");
if (args.Length == 0) { Console.WriteLine($"CRASH_BOX_FREE_OK checks={checks} code={code.Length}"); return; }

if (args.Length is not (3 or 5) || args.Length == 5 && args[3] != "--export")
    throw new ArgumentException("USA.bin catalog-workspace Crash.bin [--export out.bin]");
var catalog = LevelCatalog.Load(args[1]);
var lib = new UniversalSourceLibrary(args[0], catalog);
var disc = new CrashWarpedDisc(args[2]);
var assets = UniversalCrashBoxes.Assets(lib, disc);
Check(ReferenceEquals(assets, UniversalCrashBoxes.Assets(lib, disc)), "asset cache reused");
Check(assets.Count == 8 && assets.All(a => a.SourceKey == UniversalCrashBoxes.SourceKey && a.Behavior == UniversalSpecialBehavior.CrashBox), "six crates, TNT and Nitro");
var tnt = assets.Single(a => a.Id == UniversalCrashBoxes.TntId);
Check(tnt.Companions.Select(c => c.ClassId).SequenceEqual(UniversalCrashBoxes.TntClasses.Skip(1)), "TNT brings its 3-2-1 faces");
foreach (var a in assets.Concat(tnt.Companions))
{
    Check(a.Package.Length == 1000 && a.SourceVram.Length == 0x80000, $"{a.Id}: basket-sized package and a VRAM image");
    Check(BinaryPrimitives.ReadUInt32LittleEndian(a.Package.AsSpan(0x14)) == 0x800003D0 && BinaryPrimitives.ReadUInt16LittleEndian(a.Package.AsSpan(0x3D0)) == 0x2E0,
        $"{a.Id}: basket collision block kept");
    (byte, byte) slots = a.Id.StartsWith("crash-crate") ? ((byte)0, (byte)0xFF) : a.ClassId == UniversalCrashBoxes.NitroClass ? ((byte)3, (byte)4) : ((byte)1, (byte)2);
    Check((a.Package[4], a.Package[5]) == slots && a.Package.AsSpan(6, 14).ToArray().All(b => b == 0xFF), $"{a.Id}: Crash sounds in slots 0 and 1");
    Check(BinaryPrimitives.ReadUInt16LittleEndian(a.RowTemplate.AsSpan(0x36)) == a.ClassId, $"{a.Id}: row has its class");
    var (vertices, nearFaces, farFaces) = Cube(a.Package);
    Check(vertices.Count == 8 && nearFaces == 6 && farFaces == 6, $"{a.Id}: an eight-vertex six-face cube");
    Check(vertices.Min(v => v.Z) == -736 && vertices.Max(v => v.Z) == 0 && vertices.All(v => Math.Abs(Math.Abs(v.X) - 368) <= 1 && Math.Abs(Math.Abs(v.Y) - 368) <= 1),
        $"{a.Id}: 736 units, standing on its base (model z is negative upwards)");
}
var soundLayout = new Spyro.Editor.Core.Exporting.NativeLevelSoundLayout(tnt.SourceData.Bytes);
var nsfBoxes = disc.BoxLevel();
for (int i = 0; i < UniversalCrashBoxes.SoundNames.Length; i++)
{
    var dsc = soundLayout.Sound.Descriptors[i];
    Check(dsc.IsSupportedOneShot && dsc.Pitch == 0x400 && dsc.Sample.CopyBytes().SequenceEqual(nsfBoxes.Sound(UniversalCrashBoxes.SoundNames[i])),
        $"sound {i} is Crash's {UniversalCrashBoxes.SoundNames[i]} as an 11 kHz one-shot");
}
Check(nsfBoxes.Sound("Ttb0A").Length < 1024 && nsfBoxes.Sound("tnt0A").Length > 8192, "a short beep and a long explosion");
Check(assets.All(a => ReferenceEquals(a.SourceData, tnt.SourceData)), "one shared sound source");
Check(assets.Where(a => a.Id.StartsWith("crash-crate")).Select(a => a.RowTemplate[0x53]).SequenceEqual(UniversalCrashBoxes.Crates.Select(c => c.Reward)), "crate rows carry their rewards");

// Class IDs: unused by every retail level's registrations and rows, the shared model table and any small
// immediate in the executable and every overlay (where a level could test or spawn a class).
HashSet<int> used = [.. lib.SharedClasses];
Dictionary<int, string> why = lib.SharedClasses.ToDictionary(c => c, _ => "shared");
if (Environment.GetEnvironmentVariable("CRASH_BOX_LIST_FREE") == "1") { Console.WriteLine("shared: " + string.Join(",", lib.SharedClasses.Order().Select(c => $"{c:X3}"))); }
void Immediates(byte[] bytes)
{
    for (int o = 0; o + 4 <= bytes.Length; o += 4)
    {
        uint w = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(o));
        if (UniversalMips.Op(w) is 9 or 10 or 11 or 12 or 13 or 14 && UniversalMips.Imm(w) is > 0 and < 0x200) { used.Add(UniversalMips.Imm(w)); why.TryAdd(UniversalMips.Imm(w), $"immediate at {o:X}"); }
    }
}
Immediates(lib.Disc.Exe.ToArray());
int levels = 0;
foreach (var level in catalog.Levels.Where(l => l.SourceWadEntry > 0))
{
    var (_, data, overlay) = lib.Get(level.Key);
    foreach (var r in data.Registrations()) { used.Add(r.ClassId); why.TryAdd(r.ClassId, level.Key + " registration"); }
    for (int i = 0; i < data.Scene().RowCount; i++) { int rc = BinaryPrimitives.ReadUInt16LittleEndian(data.Row(i).AsSpan(0x36)); used.Add(rc); why.TryAdd(rc, $"{level.Key} row {i}"); }
    Immediates(overlay.Bytes.ToArray());
    levels++;
}
Check(levels >= 35, $"every retail level scanned ({levels})");
if (Environment.GetEnvironmentVariable("CRASH_BOX_LIST_FREE") == "1")
    Console.WriteLine("free: " + string.Join(",", Enumerable.Range(0x100, 0x100).Where(c => !used.Contains(c)).Select(c => $"{c:X3}")));
Check(all.All(c => !used.Contains(c)), "box classes unused by retail code and data: " + string.Join(",", all.Where(used.Contains).Select(c => $"0x{c:X3} ({why.GetValueOrDefault(c)})")));
int[] others = [UniversalHighValueGems.TealClass, UniversalHighValueGems.RainbowClass, UniversalDragonflyEggs.SignClass, UniversalSpecialBehaviors.EggHeaderClass,
    .. UniversalDragonflyEggs.Variants.Select(v => v.Class)];
Check(!all.Intersect(others).Any(), "no clash with the other editor objects");

// Ground levels have the explosion particles; flight levels refuse.
int ground = 0;
foreach (var level in catalog.Levels.Where(l => l.SourceWadEntry > 0))
{
    var types = UniversalCrashBoxes.ParticleTypes(lib.Get(level.Key).Overlay);
    if (types != null && types.Contains(2) && types.Contains(70)) ground++;
}
Check(ground >= 28, $"explosion effects in every ground level ({ground})");
Check(Refuses(() => UniversalCrashBoxes.RequireDestination(lib.Get("sunnyflight").Overlay, lib.Disc.Exe)), "flight level refused");
UniversalCrashBoxes.RequireDestination(lib.Get("darkhollow").Overlay, lib.Disc.Exe);
// An empty crate drops what the level's own enemies drop (the native reward routine's butterfly path).
foreach (var level in catalog.Levels.Where(l => l.SourceWadEntry > 0 && UniversalCrashBoxes.ParticleTypes(lib.Get(l.Key).Overlay) is { } t && t.Contains(2)))
{
    var registered = lib.Get(level.Key).Data.Registrations().Select(r => r.ClassId).ToHashSet();
    Check(new[] { 0x0E, 0x0F, 0x10 }.All(c => registered.Contains(c) || lib.SharedClasses.Contains(c)), $"{level.Key} has the butterflies an empty crate drops");
}

if (Environment.GetEnvironmentVariable("CRASH_BOX_DUMP_SOUNDS") == "1")
{
    var sl = new Spyro.Editor.Core.Exporting.NativeLevelSoundLayout(lib.Get("darkhollow").Data.Bytes);
    foreach (var dsc in sl.Sound.Descriptors.Take(80))
        Console.WriteLine($"  {dsc.Index:X2} {Convert.ToHexString(dsc.CopyDescriptor())} bytes={dsc.Sample.ByteLength} oneshot={dsc.IsSupportedOneShot}");
    Console.WriteLine($"  bank {sl.BankOffset:X} length {sl.VramLength - 0x80000:X} component {sl.Sound.ComponentByteLength}");
}
if (args.Length == 3) { Console.WriteLine($"CRASH_BOX_SOURCE_OK checks={checks}"); return; }

// Test image from a layout file: {"level": key, "boxes": [{"id": special id, "x", "y", "z": editor units}]}.
using var layout = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(Environment.GetEnvironmentVariable("CRASH_BOX_LAYOUT")
    ?? throw new ArgumentException("Set CRASH_BOX_LAYOUT to a layout JSON file.")));
string levelKey = layout.RootElement.GetProperty("level").GetString()!;
var placements = layout.RootElement.GetProperty("boxes").EnumerateArray().Select(b => new UniversalMobyPlacement(UniversalCrashBoxes.SourceKey, -1,
    b.GetProperty("x").GetInt32() * 16, b.GetProperty("y").GetInt32() * 16, b.GetProperty("z").GetInt32() * 16, Yaw: 0,
    SpecialId: b.GetProperty("id").GetString())).ToList();
var result = UniversalMobyExport.Apply(lib, args[0], args[4], catalog,
    new Dictionary<string, IReadOnlyList<UniversalMobyPlacement>> { [levelKey] = placements }, crash: disc);
int expectedTreasure = placements.Sum(p => UniversalCrashBoxes.TreasureValue(assets.Single(a => a.Id == p.SpecialId).ClassId));
var dark = result.Levels.Single();
Check(dark.Compose.TreasureDelta == expectedTreasure, $"treasure grows by the crates' gems ({dark.Compose.TreasureDelta} of {expectedTreasure})");
Console.WriteLine($"exported {result.OutputImagePath} sha256={result.OutputImageSha256}");
foreach (var c in dark.Compose.Classes.Where(c => UniversalCrashBoxes.IsClass(c.DestinationClassId)))
    Console.WriteLine($"  class 0x{c.DestinationClassId:X3} model={c.ModelImported} bytes={c.ModelBytes} tex={c.Textures} snd={c.Sounds}");
var written = UniversalDiscImage.Read(args[4]);
var destinationLevel = catalog.FindByKey(levelKey)!;
var placedSound = new Spyro.Editor.Core.Exporting.NativeLevelSoundLayout(written.EntryBytes(destinationLevel.SourceWadEntry));
foreach (string name in UniversalCrashBoxes.SoundNames)
{
    byte[] sample = nsfBoxes.Sound(name);
    var hit = placedSound.Sound.Descriptors.FirstOrDefault(dsc => dsc.Sample.CopyBytes().SequenceEqual(sample));
    Console.WriteLine(hit == null ? $"  sound {name}: not in the level" : $"  sound {name}: descriptor {hit.Index} SPU 0x{0x1010 + hit.Sample.BankOffset:X5}");
}
Console.WriteLine($"CRASH_BOX_EXPORT_OK checks={checks}");

// Vertices and face counts of a single-frame animated model (absolute vertex words only).
static (List<(int X, int Y, int Z)> Vertices, int Near, int Far) Cube(byte[] p)
{
    int I(int o) => BinaryPrimitives.ReadInt32LittleEndian(p.AsSpan(o));
    int md = I(0x34), anim = I(0x38), count = p[anim + 8];
    int start = md + (I(anim + 0x24) & 0x1FFFFF);
    List<(int, int, int)> v = [];
    for (int i = 0; i < count; i++)
    {
        int w = I(start + 4 * i);
        if ((w & 1) != 0) throw new InvalidDataException("delta vertices are not expected in a box");
        v.Add((w >> 21, (w << 10) >> 21, (w << 20) >> 19));
    }
    int Faces(int field)
    {
        int table = md + I(anim + field), end = table + 4 + I(table), n = 0;
        for (int q = table + 4; q < end; n++)
        {
            uint w = (uint)I(q); bool quad = (w & 0x80000000) != 0, tex = (w & 2) != 0;
            q += quad ? (tex ? 24 : 12) : (tex ? 20 : 8);
        }
        return n;
    }
    return (v, Faces(0x14), Faces(0x1C));
}

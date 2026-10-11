using System.Buffers.Binary;
using Spyro.Editor.Core.Crash;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Music;
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
var enemies = UniversalCrashEnemies.CrashEnemies;
var creatures = UniversalCrashEnemies.YotdCreatures;
var all = UniversalCrashEnemies.AllClasses;
Check(enemies.Count == 3 && creatures.Count == 2 && all.Count == 6 && all.Distinct().Count() == 6,
    "frog, knight, wizard and the wizard's robe-off model; Lost Fleet's rhynoc and crab");
foreach (var e in creatures)
{
    string id = UniversalSpecialCatalog.SpecialIdForClass(e.Class)!;
    Check(id == e.Id && UniversalSpecialCatalog.RequiresYearOfTheDragonSource(id) && !UniversalSpecialCatalog.RequiresCrashSource(id) &&
        !UniversalSpecialCatalog.RequiresJapaneseSource(id), $"{e.Id} needs the Year of the Dragon disc only");
    Check(UniversalSpecialCatalog.DisplayName(e.Class) == e.Name && e.Name.StartsWith("Lost Fleet ") && e.Kind == UniversalSpecialBehaviors.CrashEnemyKnight,
        $"{e.Id} is named {e.Name} and guards, marches and attacks");
    // The knight behaviour hits on attack frames 6..9: the stretched attack must reach them.
    var attack = e.YotdAnimations[2];
    Check(e.YotdAnimations.Length == 3 && attack.Repeat == 2, $"{e.Id}: guard, march and a stretched attack");
}
Check(UniversalCrashEnemies.TreasureValue(UniversalCrashEnemies.Rhynoc.Class) == 2 && UniversalCrashEnemies.TreasureValue(UniversalCrashEnemies.Crab.Class) == 5,
    "rhynoc: green gem (2); crab: blue gem (5)");
var yotdEntries = UniversalSpecialCatalog.Entries(true, true, true).Where(e => e.Default.SourceLevelKey == UniversalCrashEnemies.YotdSourceKey).ToList();
Check(yotdEntries.Count == 2 && yotdEntries.All(e => e.Category == "Enemies" && e.SubCategory == "Spyro: Year of the Dragon" && e.RestrictionReason == null) &&
    UniversalSpecialCatalog.Entries(true, true).Where(e => e.Default.SourceLevelKey == UniversalCrashEnemies.YotdSourceKey).All(e => e.RestrictionReason != null),
    "catalog: Enemies › Spyro: Year of the Dragon, locked until the disc is chosen");
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
// Hidden parts and the depth range, on a small model: two triangles sharing an edge, the furthest vertex `reach` world
// units from the origin (each coordinate within the converter's 1000, so it is stored as it is).
{
    (double X, double Y, double Z)[] Pose(double reach, bool collapsed) =>
        [(0, 0, 0), (reach / Math.Sqrt(2), reach / Math.Sqrt(2), 0), (0, 0, -200), collapsed ? (0, 0, -200) : (-200, 0, 0)];
    CrashSpyroModel.Triangle Tri(params int[] v) => new(v, [(128, 128, 128), (128, 128, 128), (128, 128, 128)], false, null, (128, 128, 128));
    CrashSpyroModel.Triangle[] tris = [Tri(0, 1, 2), Tri(1, 3, 2)];
    CrashSpyroModel.Result Make(double reach, bool collapse, bool hides) => CrashSpyroModel.Package("test",
        [new("stand", true), new("part gone", true, HidesParts: hides)],
        [[Pose(reach, false), Pose(reach, false)], [Pose(reach, collapse), Pose(reach, collapse)]], tris, false, null, new byte[16]);
    // The knight's march was Crash 3's headless Kn40V: an animation that folds a part of the model away is refused
    // unless it says it means to (the wizard's robe once it is blown off).
    Check(Refuses(() => Make(400, true, false)), "an animation hiding a part of the model is refused");
    var hiding = Make(400, true, true);
    Check(hiding.HiddenShares[0] == 0 && hiding.HiddenShares[1] > 0.1 && Math.Abs(Decoded.Read(hiding.Package).Hidden[1] - hiding.HiddenShares[1]) < 0.02,
        "an animation marked as hiding parts converts, and the hidden share reads back from the package");
    // Header +0xB: the smallest depth range that holds the model's furthest vertex (the object renderer drops faces
    // more than 512 << B stored units in front of the origin and overruns its 288-entry table 640 << B behind it).
    foreach (var (reach, shift) in new[] { (400.0, 0), (560.0, 1), (1000.0, 1), (1131.0, 2) })
    {
        var m = Decoded.Read(Make(reach, false, false).Package);
        Check(m.DepthShifts.All(b => b == shift) && m.Reach.All(r => r + 4 <= 512 << shift) && (shift == 0 || m.Reach.Max() + 4 > 512 << (shift - 1)),
            $"depth range for a model reaching {reach}: +0xB = {shift} ({string.Join(",", m.DepthShifts)})");
    }
}
// Every Crash enemy animation keeps the whole model on screen except those declared to hide parts.
Check(enemies.SelectMany(e => e.Companion == null ? [e.Main] : new[] { e.Main, e.Companion }).SelectMany(m => m.Animations)
    .Where(a => a.HidesParts).Select(a => a.Name).SequenceEqual(["Wa1fV"]), "only the wizard's robe-off standing animation hides a part (the robe)");
Check(!UniversalCrashEnemies.Knight.Main.Animations.Any(a => a.Name == "Kn40V"), "the knight does not march with Crash 3's headless Kn40V");
// Playable Crash: the controller assembles for his body and legs only; the placements that add him; his levels.
Check(UniversalBehaviorBundleBuilder.Assemble([UniversalSpecialBehaviors.CrashPlayerUpdate([UniversalCrashPlayer.BodyClass, UniversalCrashPlayer.LegsClass], "crash_player_upd")], 0x80090000).Code.Length > 0,
    "playable Crash's controller assembles");
Check(Refuses(() => UniversalSpecialBehaviors.CrashPlayerUpdate([UniversalCrashPlayer.BodyClass], "bad")), "Crash without his legs refused");
var plain = new List<UniversalMobyPlacement> { new("darkhollow", 3, 1, 2, 3) };
var withCrash = UniversalCrashPlayer.WithPlayer(plain);
Check(withCrash.Count == 3 && withCrash[1].SpecialId == UniversalCrashPlayer.Id && withCrash[2].SpecialId == UniversalCrashPlayer.LegsId &&
    withCrash.All(p => p.PersistentSlot == null), "Crash joins a level's placements");
Check(ReferenceEquals(UniversalCrashPlayer.WithPlayer(withCrash), withCrash), "Crash is added once");
var ledgered = new List<UniversalMobyPlacement> { new("darkhollow", 3, 1, 2, 3, EditorId: "a", PersistentSlot: 0), new("", -1, 0, 0, 0, EditorId: "b", PersistentSlot: 1, Retired: true) };
var ledgeredCrash = UniversalCrashPlayer.WithPlayer(ledgered);
Check(ledgeredCrash[2].PersistentSlot == 2 && ledgeredCrash[3].PersistentSlot == 3 && ledgeredCrash.Select(p => p.EditorId).Distinct().Count() == 4,
    "Crash takes the next two save identities after a ledger");
Check(UniversalCrashPlayer.IsPlayableLevel(10) && UniversalCrashPlayer.IsPlayableLevel(12) && UniversalCrashPlayer.IsPlayableLevel(64) &&
    !UniversalCrashPlayer.IsPlayableLevel(15) && !UniversalCrashPlayer.IsPlayableLevel(55) && !UniversalCrashPlayer.IsPlayableLevel(65), "Crash plays every normal level but the flight levels");
Check(UniversalSpecialCatalog.RequiresCrashSource(UniversalCrashPlayer.Id) && !UniversalSpecialCatalog.RequiresJapaneseSource(UniversalCrashPlayer.LegsId), "Crash needs the Crash disc only");
Check(Refuses(() => UniversalCrashPlayer.PatchExe(new byte[0x66000])), "the hide-Spyro hook refuses an unknown executable");
// Crash in Spyro's place during a dragon rescue: the hook and the scene draw run on a test CPU against a small world
// (hide flag, game state, rescue phase, model table, object table, Spyro); the object pass is a stub that records the
// list it is given and moves the close-record end (which the scene draw must put back); the list clear is the game's.
{
    const uint Ret = 0x80001000, Rows = 0x80100000, Stack = 0x801FF000, Sparkle = Rows, Body = Rows + 0x58, Legs = Rows + 0xB0;
    const uint StatueModel = 0x80110000, Records = 0x80072500;
    bool Run(uint gameState = 8, uint hide = 1, uint bodyModel = 0x80120000, uint phase = 1, uint timer = 40, uint loader = 3)
    {
        var cpu = new RenderProbeCpu();
        void Words(uint at, uint[] words) { for (int i = 0; i < words.Length; i++) cpu.Put(at + 4 * (uint)i, words[i]); }
        Words(UniversalCrashPlayer.DrawHookHost, UniversalCrashPlayer.DrawHookWords());
        Words(UniversalCrashPlayer.SceneDrawHost, UniversalCrashPlayer.SceneDrawWords());
        Words(UniversalCrashPlayer.ObjectPass, [0x3C018007, 0xAC3F5F00, UniversalMips.JrRa, 0]);   // sw ra over the record end
        Words(UniversalCrashPlayer.ObjectListClear, [0x00C43020, 0x20C6FFFC, 0xAC850000, 0x1486FFFE, 0x20840004, UniversalMips.JrRa, 0]);
        Words(UniversalCrashPlayer.ObjectDraw, [UniversalMips.JrRa, 0]);
        cpu.Put(0x80075814, hide); cpu.Put(0x800757D8, gameState); cpu.Put(0x80075828, Rows);
        cpu.Put(UniversalCrashPlayer.RescuePhase, phase); cpu.Put(UniversalCrashPlayer.RescueTimer, timer); cpu.Put(UniversalCrashPlayer.RescueLoader, loader);
        cpu.Put(0x80076378 + 4 * 0xFA, StatueModel); cpu.Put(0x80076378 + 4 * 0x19A, bodyModel); cpu.Put(0x80076378 + 4 * 0x19B, 0x80121000);
        foreach (var (row, cls) in new[] { (Sparkle, 0xFBu), (Body, (uint)UniversalCrashPlayer.BodyClass), (Legs, (uint)UniversalCrashPlayer.LegsClass) })
        { cpu.Put(row + 0x36, cls, 2); cpu.Put(row + 0x48, 0, 1); cpu.Put(row + 0xC, 7); cpu.Put(row + 0x46, 9, 1); }
        cpu.Put(Rows + 3 * 0x58 + 0x48, 0xFF, 1);
        cpu.Put(0x80078A58, 1000); cpu.Put(0x80078A5C, 2000); cpu.Put(0x80078A60, 3000); cpu.Put(0x80078A66, 0x40, 1);
        for (uint a = 0; a <= UniversalCrashPlayer.ObjectListBytes; a += 4) cpu.Put(UniversalCrashPlayer.ObjectList + a, 0xDEAD0000 + a);
        cpu.Put(UniversalCrashPlayer.CloseRecordsEnd, Records);
        uint[] listed = [];
        cpu.BeforeInstruction = pc => { if (pc == UniversalCrashPlayer.ObjectPass) listed = [cpu.Get(UniversalCrashPlayer.ObjectList), cpu.Get(UniversalCrashPlayer.ObjectList + 4), cpu.Get(UniversalCrashPlayer.ObjectList + 8)]; };
        for (int r = 16; r < 24; r++) cpu.Regs[r] = 0x5150 + (uint)r;
        cpu.Regs[29] = Stack; cpu.Regs[31] = Ret;
        cpu.Run(UniversalCrashPlayer.DrawHookHost, hide == 0 ? UniversalCrashPlayer.SpyroDrawResume : Ret);
        if (hide == 0) { Check(cpu.Regs[1] == 0x80077DD8 && cpu.Calls.Count == 0, "without the flag Spyro's own drawing runs on"); return false; }
        Check(cpu.Regs[29] == Stack && Enumerable.Range(16, 8).All(r => cpu.Regs[r] == 0x5150 + (uint)r), "Crash's scene draw keeps the stack and saved registers");
        bool drew = cpu.Calls.Contains(UniversalCrashPlayer.ObjectPass);
        if (drew)
        {
            Check(cpu.Calls.SequenceEqual([UniversalCrashPlayer.ObjectPass, UniversalCrashPlayer.ObjectListClear, UniversalCrashPlayer.ObjectDraw]),
                "the object pass, the list clear and the draw, as the game's renderers call them");
            Check(listed.SequenceEqual([Body, Legs, 0u]), "Crash's body and legs are the only objects drawn");
            Check(new[] { Body, Legs }.All(o => cpu.Get(o + 0xC) == 1000 && cpu.Get(o + 0x10) == 2000 &&
                cpu.Get(o + 0x14) == (uint)(3000 - UniversalSpecialBehaviors.CrashPlayerFeet) && cpu.Get(o + 0x46, 1) == 0x40) && cpu.Get(Sparkle + 0xC) == 7,
                "Crash stands where the scene puts Spyro, facing his way; nothing else moves");
            Check(Enumerable.Range(0, UniversalCrashPlayer.ObjectListBytes / 4).All(i => cpu.Get(UniversalCrashPlayer.ObjectList + 4 * (uint)i) == 0) &&
                cpu.Get(UniversalCrashPlayer.ObjectList + UniversalCrashPlayer.ObjectListBytes) == 0xDEAD0000 + (uint)UniversalCrashPlayer.ObjectListBytes &&
                cpu.Get(UniversalCrashPlayer.CloseRecordsEnd) == Records, "the list area is cleared as the game clears it and the close-record end put back");
        }
        else Check(cpu.Calls.Count == 0 && cpu.Get(Body + 0xC) == 7 && cpu.Get(UniversalCrashPlayer.ObjectList) == 0xDEAD0000, "nothing drawn, nothing changed");
        return drew;
    }
    Check(Run() && Run(phase: 5) && Run(phase: 7, timer: 15) && Run(loader: 0) && Run(loader: 6), "a dragon rescue draws Crash in Spyro's place");
    Check(!Run(phase: 6, loader: 4) && !Run(phase: 6, loader: 5), "not while the rescue reloads the model area after the scene");
    Check(!Run(gameState: 0) && !Run(gameState: 4) && !Run(gameState: 9), "other scenes draw nobody (they may load over the models)");
    Check(!Run(bodyModel: StatueModel - 0x800), "a model below the statue (in the rescue's stream area) is not drawn");
    Check(!Run(phase: 7, timer: 16), "the rescue's last phase, which draws the level's objects itself, does not draw Crash twice");
    Run(hide: 0);
    Check(UniversalCrashPlayer.DrawHookWords().Length <= UniversalCrashPlayer.DrawHookHostWords &&
        UniversalCrashPlayer.SceneDrawWords().Length <= UniversalCrashPlayer.SceneDrawHostWords, "the hook and the scene draw fit their hosts");
}
// Crash 3 music in Spyro's unused XA slots: the catalog keeps Spyro's own 48/46 tracks and adds the songs apart.
Check(MusicTrackCatalog.Tracks.Count == 48 && MusicTrackCatalog.SelectableTracks.Count == 46, "Spyro's own track lists unchanged");
Check(MusicTrackCatalog.CustomTracks.Count == CrashMusic.Songs.Count + YearOfTheDragonDisc.LevelSongs.Count && MusicTrackCatalog.CustomTracks.All(t => t.IsSelectable && t.TrackId >= 100),
    "each Crash and Year of the Dragon song is a selectable custom track outside the XA ids");
Check(MusicTrackCatalog.FindAny(YearOfTheDragonDisc.LostFleet.TrackId)?.CustomSlot == MusicTrackCatalog.UnusedSlotB && MusicTrackCatalog.IsYearOfTheDragon(YearOfTheDragonDisc.LostFleet.TrackId) &&
    !MusicTrackCatalog.IsYearOfTheDragon(CrashMusic.ToadVillage.TrackId), "Lost Fleet plays from unused slot B, apart from Toad Village");
Check(Refuses(() => CustomMusicSlots.Encode(MusicTrackCatalog.FindAny(YearOfTheDragonDisc.LostFleet.TrackId)!, null, "/nonexistent.bin")),
    "a Year of the Dragon song without its disc is refused");
Check(MusicTrackCatalog.FindAny(CrashMusic.ToadVillage.TrackId)?.CustomSlot == MusicTrackCatalog.UnusedSlotA && MusicTrackCatalog.Find(CrashMusic.ToadVillage.TrackId) == null,
    "Toad Village plays from unused slot A and is not a built-in track");
Check(MusicTrackCatalog.PickerTracks.Count == 46 + MusicTrackCatalog.CustomTracks.Count, "the picker offers Spyro's tracks and the Crash songs");
Check(MusicTrackCatalog.CustomTracks.Select(t => t.CustomSlot).Distinct().Count() == MusicTrackCatalog.CustomTracks.Count, "one slot per song");
{
    // XA round trip on a chord with a sweep: the encoder must stay well above 4-bit ADPCM's usual ~30 dB.
    short[] pcm = new short[2 * XaAudio.FramesPerSector * 4];
    for (int i = 0; i < pcm.Length / 2; i++)
    {
        double t = i / (double)XaAudio.SampleRate;
        pcm[2 * i] = (short)(9000 * Math.Sin(2 * Math.PI * 220 * t) + 5000 * Math.Sin(2 * Math.PI * (440 + 2000 * t) * t));
        pcm[2 * i + 1] = (short)(8000 * Math.Sin(2 * Math.PI * 330 * t) + 3000 * Math.Sin(2 * Math.PI * 1250 * t));
    }
    var el = new XaAudio.ChannelState(); var er = el; var dl = el; var dr = el;
    double signal = 0, error = 0; byte[] sector = new byte[XaAudio.SectorDataBytes]; short[] back = new short[2 * XaAudio.FramesPerSector];
    for (int s = 0; s < 4; s++)
    {
        var slice = pcm.AsSpan(2 * s * XaAudio.FramesPerSector, 2 * XaAudio.FramesPerSector);
        XaAudio.EncodeSector(slice, ref el, ref er, sector);
        XaAudio.DecodeSector(sector, ref dl, ref dr, back);
        for (int i = 0; i < back.Length; i++) { signal += (double)slice[i] * slice[i]; error += (double)(back[i] - slice[i]) * (back[i] - slice[i]); }
    }
    Check(10 * Math.Log10(signal / error) > 35, $"XA ADPCM round trip ({10 * Math.Log10(signal / error):0.0} dB)");
    Check(el.Old == dl.Old && er.Older == dr.Older, "encoder and decoder agree on the channel history");
}
if (args.Length == 0) { Console.WriteLine($"CRASH_ENEMY_FREE_OK checks={checks}"); return; }

// ---- Source checks: USA.bin catalog-workspace Crash.bin [YearOfTheDragon.bin].
if (args.Length is not (3 or 4)) throw new ArgumentException("USA.bin catalog-workspace Crash.bin [YearOfTheDragon.bin]");
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
    Check(m.Reach.Zip(m.DepthShifts).All(x => x.First + 4 <= 512 << x.Second),
        $"{asset.Id}: every animation's depth range holds its furthest vertex ({string.Join(", ", m.Reach.Zip(m.DepthShifts).Select(x => $"{x.First:F0}/B{x.Second}"))})");
    Check(m.Hidden.Select((share, i) => model.Animations[i].HidesParts ? share > CrashSpyroModel.HiddenShareLimit : share <= CrashSpyroModel.HiddenShareLimit).All(x => x),
        $"{asset.Id}: no animation hides part of the model unless it means to ({string.Join(", ", m.Hidden.Select(h => $"{h:P1}"))})");
    if (asset.ClassId == UniversalCrashEnemies.Knight.Class)
        Check(m.Hidden.All(h => h == 0), "the knight keeps its head (every face of its guard pose) in its guard, march and swing");
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
// Playable Crash: body and legs from Crash's own model, drawn only, sharing one texture image.
var player = UniversalCrashPlayer.Assets(library, disc2);
Check(player.Count == 2 && player[0].ClassId == UniversalCrashPlayer.BodyClass && player[1].ClassId == UniversalCrashPlayer.LegsClass &&
    player.All(a => a.Behavior == UniversalSpecialBehavior.CrashPlayer) && ReferenceEquals(player[0].SourceVram, player[1].SourceVram), "playable Crash: body and legs, one texture image");
var toad = disc2.Level(UniversalCrashEnemies.ToadVillageFile, UniversalCrashEnemies.ToadVillageSha256);
var crashModel = new CrashModel(toad.Entry(UniversalCrashPlayer.ModelName).Items);
int bodyVertices = Decoded.Read(player[0].Package).Vertices, legVertices = Decoded.Read(player[1].Package).Vertices;
Check(bodyVertices + legVertices >= crashModel.VertexCount - crashModel.SpecialVertexCount && bodyVertices <= 255 && legVertices <= 255,
    $"Crash's {crashModel.VertexCount - crashModel.SpecialVertexCount} vertices as {bodyVertices} + {legVertices}");
foreach (var a in player)
{
    var m = Decoded.Read(a.Package);
    Check(BinaryPrimitives.ReadUInt32LittleEndian(a.Package.AsSpan(0x14)) == 0, $"{a.Id}: no collision (Spyro keeps his own)");
    Check(m.FacesValid && m.MaxStored <= 1000 && m.FarFaces == m.NearFaces, $"{a.Id}: faces valid, coordinates within the game's range");
    Check(m.Reach.Zip(m.DepthShifts).All(x => x.First + 4 <= 512 << x.Second) && m.Hidden.All(h => h <= CrashSpyroModel.HiddenShareLimit),
        $"{a.Id}: depth ranges hold every animation, none hides a part");
    Check(m.Frames.Select(f => f.Count).SequenceEqual(UniversalCrashPlayer.Animations.Select(an => (toad.Entry(an.Name).Items.Count + an.Step - 1) / an.Step)),
        $"{a.Id}: every kept frame of each animation");
}
Check(player[0].Package.AsSpan(4, 16).ToArray().Select((b, i) => i < UniversalCrashPlayer.Sounds.Length ? b == i : b == 0xFF).All(x => x) &&
    player[1].Package.AsSpan(4, 16).ToArray().All(b => b == 0xFF), "the body carries Crash's sounds, the legs none");
byte[] hooked = library.Disc.Exe.ToArray(); UniversalCrashPlayer.PatchExe(hooked);
byte[] hookedAgain = hooked.ToArray(); UniversalCrashPlayer.PatchExe(hookedAgain);
int changedBytes = Enumerable.Range(0, hooked.Length).Count(i => hooked[i] != library.Disc.Exe[i]);
int hookBytes = 4 * (UniversalCrashPlayer.DrawHookWords().Length + UniversalCrashPlayer.SceneDrawWords().Length + 2);
Check(changedBytes is > 0 && changedBytes <= hookBytes && hookedAgain.AsSpan().SequenceEqual(hooked), $"the hide-Spyro hook and Crash's scene draw install once ({changedBytes} bytes)");
{
    // Only the call site and the two hosts change; an executable with the first release's hook (hide only) is upgraded
    // to the same bytes; a changed host is refused.
    int Off(uint a) => (int)(a - UniversalExeBindings.ExeBase);
    bool Inside(int o, uint host, int words) => o >= Off(host) && o < Off(host) + 4 * words;
    Check(Enumerable.Range(0, hooked.Length).Where(i => hooked[i] != library.Disc.Exe[i]).All(o => Inside(o, UniversalCrashPlayer.SpyroDraw, 2) ||
        Inside(o, UniversalCrashPlayer.DrawHookHost, UniversalCrashPlayer.DrawHookHostWords) || Inside(o, UniversalCrashPlayer.SceneDrawHost, UniversalCrashPlayer.SceneDrawHostWords)),
        "only the call site and the two unused hosts change");
    byte[] first = library.Disc.Exe.ToArray();
    uint[] firstHook = UniversalCrashPlayer.FirstDrawHook();
    for (int i = 0; i < firstHook.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(first.AsSpan(Off(UniversalCrashPlayer.DrawHookHost) + 4 * i), firstHook[i]);
    BinaryPrimitives.WriteUInt32LittleEndian(first.AsSpan(Off(UniversalCrashPlayer.SpyroDraw)), UniversalMips.EncodeJ(2, UniversalCrashPlayer.DrawHookHost));
    BinaryPrimitives.WriteUInt32LittleEndian(first.AsSpan(Off(UniversalCrashPlayer.SpyroDraw) + 4), 0);
    UniversalCrashPlayer.PatchExe(first);
    Check(first.AsSpan().SequenceEqual(hooked), "the first release's hide-only hook is upgraded to the scene draw");
    byte[] changed = library.Disc.Exe.ToArray(); changed[Off(UniversalCrashPlayer.SceneDrawHost) + 4 * 20] ^= 1;
    Check(Refuses(() => UniversalCrashPlayer.PatchExe(changed)), "a changed scene-draw host is refused");
    byte[] tampered = hooked.ToArray(); tampered[Off(UniversalCrashPlayer.SceneDrawHost) + 4] ^= 1;
    Check(Refuses(() => UniversalCrashPlayer.PatchExe(tampered)), "an installed hook that differs is refused");
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
Check(!used.Contains(UniversalCrashPlayer.BodyClass) && !used.Contains(UniversalCrashPlayer.LegsClass), "playable Crash's classes unused by retail code and data");
UniversalCrashBoxes.RequireDestination(library.Get("darkhollow").Overlay, library.Disc.Exe);
// Toad Village into a copy of the USA disc's slot A: laid out like Spyro's own tracks, decoding back to the render.
{
    string scratch = Path.Combine(Path.GetTempPath(), "spyro-crash-music-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(scratch);
    try
    {
        string image = Path.Combine(scratch, "music.bin");
        File.Copy(args[0], image);
        var track = MusicTrackCatalog.FindAny(CrashMusic.ToadVillage.TrackId)!;
        var (instruments, sequence) = CrashMusic.Load(disc2, CrashMusic.ToadVillage);
        Check(Math.Abs(CrashMusic.PassSeconds(sequence) - 103.38) < 0.1 && instruments.Programs.Count == 16, "Toad Village: 103 s per pass, 16 instruments");
        var song = CustomMusicSlots.Install(image, [track], disc2).Single();
        Check(song.Slot == 30 && song.Passes == 2 && song.Sectors.Length == 3876 && song.EndMark == 31008, $"two passes in slot 30 ({song.Sectors.Length} sectors, mark {song.EndMark})");
        int lba = 147184, eof = -1, audio = 0, badEdc = 0;
        var dl = new XaAudio.ChannelState(); var dr = dl;
        List<short> decoded = []; short[] buffer = new short[2 * XaAudio.FramesPerSector]; byte[] raw = new byte[2352];
        using (var stream = File.OpenRead(image))
            for (int k = 6; ; k += 8)
            {
                stream.Seek((lba + k) * 2352L, SeekOrigin.Begin); stream.ReadExactly(raw);
                if ((raw[18] & 4) == 0) break;
                audio++; if (raw[18] == 0xE4) eof = k;
                if (BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(2348)) != XaAudio.Form2Edc(raw)) badEdc++;
                XaAudio.DecodeSector(raw.AsSpan(24), ref dl, ref dr, buffer); decoded.AddRange(buffer);
            }
        Check(audio == 3876 && eof == 31006 && badEdc == 0, $"audio sectors, end-of-file flag and EDCs on disc ({audio}, {eof}, {badEdc})");
        byte[] exe = UniversalDiscImage.Read(image).Exe.ToArray();
        Check(BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(CustomMusicSlots.EndTableOffset(exe.ToArray(), image) + 4 * 30)) == 31008, "slot 30's loop mark in the image's executable");
        short[] reference = CrashMusic.RenderLoop(instruments, sequence, XaAudio.SampleRate, 2, 1938 * XaAudio.FramesPerSector);
        double signal = 0, error = 0;
        for (int i = 0; i < reference.Length; i++) { signal += (double)reference[i] * reference[i]; error += (double)(decoded[i] - reference[i]) * (decoded[i] - reference[i]); }
        double rms = Math.Sqrt(signal / reference.Length);
        Check(10 * Math.Log10(signal / error) > 30 && rms is > 4000 and < 9000, $"decodes back to the render ({10 * Math.Log10(signal / error):0.0} dB) at Spyro's loudness (rms {rms:0})");
    }
    finally { Directory.Delete(scratch, true); }
}
if (args.Length == 4)
{
    // Lost Fleet's creatures from the Year of the Dragon disc: read, converted, tuned to their built models.
    var yotd = new Spyro.Editor.Core.Music.YearOfTheDragonDisc(args[3]);
    var made = UniversalCrashEnemies.YotdAssets(library, yotd);
    Check(ReferenceEquals(made, UniversalCrashEnemies.YotdAssets(library, yotd)), "creature cache reused");
    Check(made.Select(a => a.Id).SequenceEqual(creatures.Select(e => e.Id)) && made.All(a => a.Behavior == UniversalSpecialBehavior.CrashEnemy &&
        a.SourceKey == UniversalCrashEnemies.YotdSourceKey) && ReferenceEquals(made[0].SourceVram, made[1].SourceVram), "one asset per creature, one shared texture image");
    var lostFleet = Spyro.Editor.Core.YearOfTheDragon.YearOfTheDragonModels.LostFleet(yotd);
    foreach (var (asset, e) in made.Select(a => (a, UniversalCrashEnemies.ForClass(a.ClassId)!)))
    {
        var m = Decoded.Read(asset.Package);
        var source = lostFleet.Animated(e.YotdSet);
        Check(m.Frames.Select(f => f.Count).SequenceEqual(e.YotdAnimations.Select(a => (source[a.Index].Frames.Count + a.Step - 1) / a.Step * a.Repeat)),
            $"{asset.Id}: every kept frame of each animation");
        Check(m.Frames[2].Count >= 10, $"{asset.Id}: the attack reaches the behaviour's hit frames 6..9");
        Check(m.Vertices is > 0 and <= 255 && m.NearFaces > 0 && m.FarFaces == m.NearFaces && m.MaxStored <= 1000 && m.FacesValid,
            $"{asset.Id}: {m.Vertices} vertices, {m.NearFaces} faces, stored coordinates {m.MaxStored}");
        Check(m.Reach.Zip(m.DepthShifts).All(x => x.First + 4 <= 512 << x.Second) && m.Hidden.All(h => h <= CrashSpyroModel.HiddenShareLimit),
            $"{asset.Id}: depth ranges hold every animation, none hides a part");
        Check(m.Textures.All(t => t.PageX >= 8 && t.Depth <= 2), $"{asset.Id}: art in the level's half of texture memory");
        int n = e.YotdSoundSlots.Length;
        Check(n >= 2 && asset.Package.AsSpan(4, 16).ToArray().Select((b, i) => i < n ? b == i : b == 0xFF).All(x => x), $"{asset.Id}: sound slots 0..{n - 1}");
        var soundLayout = new Spyro.Editor.Core.Exporting.NativeLevelSoundLayout(asset.SourceData.Bytes);
        var soundIds = lostFleet.SoundSlots(e.YotdSet);
        for (int i = 0; i < n; i++)
        {
            var want = lostFleet.Sound(soundIds[e.YotdSoundSlots[i]]);
            var dsc = soundLayout.Sound.Descriptors[i];
            Check(dsc.IsSupportedOneShot && dsc.Pitch == want.Pitch && dsc.Sample.CopyBytes().SequenceEqual(want.Adpcm),
                $"{asset.Id}: sound {i} is Lost Fleet's sound {soundIds[e.YotdSoundSlots[i]]} ({want.Adpcm.Length} bytes, pitch 0x{want.Pitch:X})");
        }
        Check(m.Frames.SelectMany(f => f).All(s => s == 0xFF || s < n) && m.Frames.SelectMany(f => f).Any(s => s != 0xFF),
            $"{asset.Id}: its animations play its own sounds");
        Check(Math.Abs(e.Tuning.Top - m.Height) <= 40, $"{asset.Id}: tuned height {e.Tuning.Top} vs model {m.Height}");
        Check(Math.Abs(e.Tuning.Stand - (m.CapsuleTop + 556)) <= 60, $"{asset.Id}: tuned standing height {e.Tuning.Stand} vs capsule top {m.CapsuleTop} + 556");
        Check(e.Tuning.Touch > m.CapsuleRadius && e.Tuning.Stomp > m.CapsuleRadius, $"{asset.Id}: touch and stomp reach beyond the capsule");
        Check(BinaryPrimitives.ReadUInt16LittleEndian(asset.RowTemplate.AsSpan(0x36)) == asset.ClassId && asset.RowTemplate[0x53] == e.Reward,
            $"{asset.Id}: row carries its class and reward");
        Console.WriteLine($"  {asset.Id}: {asset.Package.Length} bytes, {m.Vertices} vertices, {m.NearFaces} faces, height {m.Height}, frames {string.Join(",", m.Frames.Select(f => f.Count))}");
    }
}
Console.WriteLine($"CRASH_ENEMY_SOURCE_OK checks={checks}");

/// <summary>A Spyro animated object package read back the way the game reads it.</summary>
/// <param name="DepthShifts">Each animation's header +0xB: the object renderer keeps faces from 512 &lt;&lt; B stored
/// units in front of the origin to 640 &lt;&lt; B behind it.</param>
/// <param name="Reach">Each animation's furthest stored vertex from the origin.</param>
/// <param name="Hidden">Each animation's share of the model's surface (first frame) whose faces shrink to nothing in all
/// of its frames: a part the animation hides.</param>
sealed record Decoded(int Vertices, int NearFaces, int FarFaces, int Scale, int MaxStored, int Height, bool FacesValid,
    int CapsuleTop, int CapsuleRadius, IReadOnlyList<IReadOnlyList<int>> Frames, IReadOnlyList<(int PageX, int PageY, int Depth, int ClutY)> Textures,
    IReadOnlyList<int> DepthShifts, IReadOnlyList<double> Reach, IReadOnlyList<double> Hidden)
{
    public static Decoded Read(byte[] p)
    {
        int I(int o) => BinaryPrimitives.ReadInt32LittleEndian(p.AsSpan(o));
        int anims = I(0), md = I(0x34), h0 = I(0x38), count = p[h0 + 8], colours = BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(h0 + 2));
        int maxStored = 0, height = 0;
        List<IReadOnlyList<int>> frames = [];
        List<List<List<(int X, int Y, int Z)>>> shapes = [];
        List<int> depthShifts = []; List<double> reach = [];
        for (int a = 0; a < anims; a++)
        {
            int h = I(0x38 + 4 * a), n = BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(h)), shift = p[h + 6] + 1, scale = 1 << p[h + 5];
            if (p[h + 8] != count || p[h + 5] != p[h0 + 5] || I(h + 0x14) != I(h0 + 0x14)) throw new InvalidDataException("animations disagree");
            List<int> sounds = [];
            shapes.Add([]); depthShifts.Add(p[h + 0xB]); reach.Add(0);
            for (int f = 0; f < n; f++)
            {
                uint packed = (uint)I(h + 0x24 + 8 * f), aux = (uint)I(h + 0x28 + 8 * f);
                if ((aux & 0xFFFF) != 0) throw new InvalidDataException("unexpected face patch");
                int start = md + (int)(packed & 0x1FFFFF), absolute = (int)(aux >> 24);
                var v = Unpack(p.AsSpan(start).ToArray(), absolute, count, shift);
                maxStored = Math.Max(maxStored, v.Max(q => Math.Max(Math.Abs(q.X), Math.Max(Math.Abs(q.Y), Math.Abs(q.Z)))));
                if (a == 0 && f == 0) height = -v.Min(q => q.Z) * scale;
                shapes[a].Add(v);
                reach[a] = Math.Max(reach[a], v.Max(q => Math.Sqrt((double)q.X * q.X + (double)q.Y * q.Y + (double)q.Z * q.Z)));
                sounds.Add((int)(packed >> 24));
            }
            frames.Add(sounds);
        }
        bool valid = true; int near = 0, far = 0;
        List<(int, int, int, int)> textures = [];
        List<int[]> tris = [];
        foreach (int table in new[] { 0x14, 0x1C })
        {
            int t = md + I(h0 + table), end = t + 4 + I(t);
            for (int q = t + 4; q < end;)
            {
                uint w = (uint)I(q), c = (uint)I(q + 4);
                bool quad = (w & 0x80000000) != 0, tex = (w & 2) != 0;
                valid &= !quad && new[] { w >> 22 & 511, w >> 13 & 511, w >> 4 & 511 }.All(i => i < count) && new[] { c >> 19 & 511, c >> 10 & 511, c >> 1 & 511 }.All(i => i < colours);
                if (table == 0x14 && valid) tris.Add([(int)(w >> 22 & 511), (int)(w >> 13 & 511), (int)(w >> 4 & 511)]);
                if (tex)
                {
                    int page = BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(q + 14)), clut = BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(q + 10));
                    textures.Add((page & 15, page >> 4 & 1, page >> 7 & 3, clut >> 6));
                }
                q += tex ? 20 : 8;
                if (table == 0x14) near++; else far++;
            }
        }
        static double Area(List<(int X, int Y, int Z)> v, int[] t)
        {
            var (a, b, c) = (v[t[0]], v[t[1]], v[t[2]]);
            double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z, wx = c.X - a.X, wy = c.Y - a.Y, wz = c.Z - a.Z;
            double nx = uy * wz - uz * wy, ny = uz * wx - ux * wz, nz = ux * wy - uy * wx;
            return Math.Sqrt(nx * nx + ny * ny + nz * nz) / 2;
        }
        double[] rest = [.. tris.Select(t => valid ? Area(shapes[0][0], t) : 0)];
        double total = rest.Sum();
        var hidden = shapes.Select(fr => total <= 0 ? 0 : Enumerable.Range(0, tris.Count)
            .Where(t => rest[t] > 0 && fr.All(v => Area(v, tris[t]) < 0.02 * rest[t])).Sum(t => rest[t]) / total).ToArray();
        int trailer = (int)((uint)I(0x14) & 0x7FFFFFFF);
        return new(count, near, far, 1 << p[h0 + 5], maxStored, height, valid,
            trailer == 0 ? 0 : BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(trailer)), trailer == 0 ? 0 : BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(trailer + 6)),
            frames, textures, depthShifts, reach, hidden);
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

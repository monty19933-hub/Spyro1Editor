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
    Check(m.Frames.Select(f => f.Count).SequenceEqual(UniversalCrashPlayer.Animations.Select(an => (toad.Entry(an.Name).Items.Count + an.Step - 1) / an.Step)),
        $"{a.Id}: every kept frame of each animation");
}
Check(player[0].Package.AsSpan(4, 16).ToArray().Select((b, i) => i < UniversalCrashPlayer.Sounds.Length ? b == i : b == 0xFF).All(x => x) &&
    player[1].Package.AsSpan(4, 16).ToArray().All(b => b == 0xFF), "the body carries Crash's sounds, the legs none");
byte[] hooked = library.Disc.Exe.ToArray(); UniversalCrashPlayer.PatchExe(hooked);
byte[] hookedAgain = hooked.ToArray(); UniversalCrashPlayer.PatchExe(hookedAgain);
int changedBytes = Enumerable.Range(0, hooked.Length).Count(i => hooked[i] != library.Disc.Exe[i]);
Check(changedBytes is > 0 and <= 48 && hookedAgain.AsSpan().SequenceEqual(hooked), $"the hide-Spyro hook installs once ({changedBytes} bytes)");
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

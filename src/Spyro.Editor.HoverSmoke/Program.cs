using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Universal;

// Spyro 3's hover (NativeHover), its wing flap (NativeHoverFlap) and its Game Settings choice (SpyroMovesSettings). Default:
// source-free (the saved choice, the new state-16 entry and input step, Create BIN's scope, refusals). --source <USA.bin>:
// installed into the USA executable it changes exactly its two places, again changes nothing, refuses tampered code, and
// leaves the other executable features' places and the state -> animation table alone; the flap goes into every level
// that lacks it and has room (Tree Tops, Haunted Towers and Twilight Harbor have none with 2 MB), once.
int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
void Refuse(Action action, string message) { try { action(); } catch (InvalidDataException) { checks++; return; } throw new Exception(message); }
uint Word(byte[] b, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan((int)(address - 0x8000F800)));
int Imm(uint w) => (short)(w & 0xFFFF);
uint Target(uint w, uint at) => (uint)(at + 4 + 4 * Imm(w));

if (args.Length == 0)
{
    // The saved choice: none by default, a file only while it is on.
    string root = Path.Combine(Path.GetTempPath(), "hover-smoke-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        Check(!SpyroMovesSettings.LoadHover(root) && !File.Exists(SpyroMovesSettings.PathFor(root)), "a project has no hover until it is turned on");
        SpyroMovesSettings.SaveHover(root, true);
        Check(SpyroMovesSettings.LoadHover(root) && File.Exists(SpyroMovesSettings.PathFor(root)), "turned on, it is saved with the project");
        SpyroMovesSettings.SaveHover(root, false);
        Check(!SpyroMovesSettings.LoadHover(root) && !File.Exists(SpyroMovesSettings.PathFor(root)), "turned off, the file goes");
        File.WriteAllText(SpyroMovesSettings.PathFor(root), "{\"schemaVersion\":1}");
        Refuse(() => SpyroMovesSettings.LoadHover(root), "a file without the choice is refused, not read as off");
        File.WriteAllText(SpyroMovesSettings.PathFor(root), "{\"schemaVersion\":2,\"hover\":true}");
        Refuse(() => SpyroMovesSettings.LoadHover(root), "an unknown version is refused");
    }
    finally { Directory.Delete(root, recursive: true); }

    // The new entry: 24 words in place, the hover and the drop, the flap only when the level has its record.
    uint[] code = NativeHover.EnterCode();
    uint At(int slot) => NativeHover.EnterAddress + 4 * (uint)slot;
    Check(code.Length == 24 && code.All(w => w != 0), "the entry fills its 24 words");
    Check(UniversalMips.Op(code[5]) == 4 && UniversalMips.BranchTarget(code[5], At(5)) == At(9), "from the glide (15) it branches to the hover");
    Check(UniversalMips.Op(code[7]) == 5 && UniversalMips.BranchTarget(code[7], At(7)) == At(17), "from anything but the other glide (23) it skips the hover");
    Check(code[13] == UniversalMips.EncodeI(9, 0, 2, NativeHover.RiseSpeed) && NativeHover.RiseSpeed is > 0 and < 0x8000, "the hover sets an upward speed");
    Check(code[3] == UniversalMips.EncodeI(9, 0, 12, NativeHover.DropAnimation) && code[16] == UniversalMips.EncodeI(9, 0, 12, NativeHover.FlapAnimation),
        "the drop keeps the drop pose, the hover flaps");
    Check(UniversalMips.Op(code[12]) == 35 && Imm(code[12]) == 0x38 + 4 * NativeHover.FlapAnimation && UniversalMips.Op(code[14]) == 4 &&
        UniversalMips.BranchTarget(code[14], At(14)) == At(17), "the flap only when the player package's slot for it is filled");
    Check(UniversalMips.Op(code[21]) == 40 && (uint)(0x80070000 + Imm(code[21])) == NativeHover.DropAnimationByte, "it writes state 16's animation byte");
    Check(UniversalMips.Op(code[22]) == 2 && ((code[22] & 0x3FFFFFF) << 2 | 0x80000000) == NativeHover.SharedTail, "it continues with state 6's steps");
    Check(code.Count(w => UniversalMips.IsStore(w)) == 5, "five fields are written: vertical and forward speed, flight sub-state, the animation byte, sideways speed");

    // The input step: the drop pose goes on as the game does; a rising flap is quick; then the drop pose comes back; never a new glide.
    uint[] frame = NativeHover.FrameCode();
    uint F(int slot) => NativeHover.FrameAddress + 4 * (uint)slot;
    Check(frame.Length == 15, "the input step takes the first 15 words of the old ✕ test");
    Check(UniversalMips.Op(frame[5]) == 4 && Target(frame[5], F(5)) == NativeHover.InputContinue && frame[4] == UniversalMips.EncodeI(9, 0, 11, NativeHover.DropAnimation),
        "in the drop pose it goes on as the game does");
    Check(UniversalMips.Op(frame[7]) == 7 && Target(frame[7], F(7)) == NativeHover.InputContinue && frame[6] == UniversalMips.EncodeI(9, 0, 12, NativeHover.FlapSpeed) &&
        frame[8] == UniversalMips.EncodeI(43, 1, 12, -0x7550) && NativeHover.FlapSpeed is > 4 and <= 16, "a rising flap plays at the flutter's speed");
    Check(frame[9] == UniversalMips.EncodeI(40, 9, 11, Imm(frame[2])) && frame[10] == UniversalMips.EncodeI(9, 0, 12, NativeHover.DropSpeed) &&
        frame[11] == UniversalMips.EncodeI(43, 1, 12, -0x7550), "after the rise the drop pose goes back into state 16's byte, at its own speed");
    Check(frame[12] == UniversalMips.EncodeI(9, 0, 11, 15) && frame[14] == UniversalMips.EncodeI(43, 1, 11, -0x754C), "and his animation is marked as the glide's");
    Check(UniversalMips.Op(frame[13]) == 2 && ((frame[13] & 0x3FFFFFF) << 2 | 0x80000000) == NativeHover.InputContinue, "every way ends where the ✕ test went on without ✕");
    Check(!frame.Any(w => UniversalMips.Op(w) == 35 && Imm(w) == 0x7378), "it no longer reads the new presses: no glide again until Spyro lands");

    // Create BIN's scope: off unless the export asks; scopes nest and end; levels without the flap are noted.
    Check(!NativeHover.Enabled, "no export, no hover");
    byte[] blank = new byte[0x66000];
    NativeHover.ApplyForExport(blank);
    Check(blank.All(b => b == 0), "with the scope off the executable is left alone");
    using (var scope = NativeHover.Use(true))
    {
        Check(NativeHover.Enabled, "the export asks for the hover");
        using (NativeHover.Use(false)) Check(!NativeHover.Enabled, "an inner scope can turn it off");
        Check(NativeHover.Enabled, "the outer choice comes back");
        Refuse(() => NativeHover.ApplyForExport(blank), "an executable that is not the USA game's is refused");
        Check(blank.All(b => b == 0), "and left untouched");
        Check(NativeHover.CurrentLevelsWithoutFlap.Count == 0 && NativeHoverFlap.NoRoomNote([]) == "", "no level without the flap yet, nothing to say");
        await NativeHoverFlap.ApplyForExportAsync("/nonexistent.bin", new Dictionary<int, byte[]>(), _ => [], blank, []).ContinueWith(_ => { });
        Check(NativeHoverFlap.NoRoomNote(["Twilight Harbor"]).Contains("Twilight Harbor") && NativeHoverFlap.NoRoomNote(["Twilight Harbor"]).Contains("without flapping"),
            "a level without room is named");
    }
    Check(!NativeHover.Enabled && NativeHover.CurrentLevelsWithoutFlap.Count == 0, "the scope ends with the export");
    Refuse(() => NativeHover.Install(new byte[0x1000]), "a truncated executable is refused");
    Console.WriteLine($"HOVER_FREE_OK checks={checks}");
    return 0;
}

if (args.Length >= 2 && args[0] == "--source")
{
    var disc = UniversalDiscImage.Read(args[1]);
    byte[] original = disc.Exe.ToArray();
    Check(!NativeHover.IsInstalled(original), "the USA game has no hover");
    byte[] exe = original.ToArray();
    NativeHover.Install(exe);
    Check(NativeHover.IsInstalled(exe), "installed");
    var patches = NativeHover.Patches();
    var covered = new bool[exe.Length];
    foreach (var (address, before, after) in patches)
    {
        Check(original.AsSpan((int)(address - 0x8000F800), before.Length).SequenceEqual(before), $"0x{address:X8} holds the USA game's code");
        covered.AsSpan((int)(address - 0x8000F800), after.Length).Fill(true);
    }
    Check(Enumerable.Range(0, exe.Length).All(i => exe[i] == original[i] || covered[i]), "nothing else changes");
    Check(Enumerable.Range(0, 24).All(i => Word(exe, NativeHover.EnterAddress + 4 * (uint)i) == NativeHover.EnterCode()[i]), "the new entry is in place");
    Check(Enumerable.Range(0, 15).All(i => Word(exe, NativeHover.FrameAddress + 4 * (uint)i) == NativeHover.FrameCode()[i]), "the new input step is in place");
    Check(original[(int)(0x8006C4A0 - 0x8000F800) + 4 * NativeHover.DropAnimation + 3] == NativeHover.DropSpeed &&
        original[(int)(0x8006C4A0 - 0x8000F800) + 4 * NativeHover.FlapAnimation + 3] == 4, "the game plays the drop pose and the flight levels' flap at speed 4");
    Check(original[(int)(NativeHover.DropAnimationByte - 0x8000F800)] == NativeHover.DropAnimation, "the game's own state 16 animation is the drop pose");
    byte[] again = exe.ToArray(); NativeHover.Install(again);
    Check(again.AsSpan().SequenceEqual(exe), "installing again changes nothing");

    // Refusals leave the executable untouched.
    byte[] tail = original.ToArray(); tail[(int)(NativeHover.SharedTail - 0x8000F800) + 4] ^= 1; byte[] tailCopy = tail.ToArray();
    Refuse(() => NativeHover.Install(tail), "changed falling code is refused");
    Check(tail.AsSpan().SequenceEqual(tailCopy), "untouched");
    byte[] other = original.ToArray(); other[(int)(NativeHover.FrameAddress - 0x8000F800) + 12] ^= 0x10; byte[] otherCopy = other.ToArray();
    Refuse(() => NativeHover.Install(other), "another change at state 16's input step is refused");
    Check(other.AsSpan().SequenceEqual(otherCopy), "untouched");

    // The other executable features keep their places (in both orders) and the shared animation table is the USA one.
    byte[] a = original.ToArray(); NativeExtendedRam.PatchExe(a); NativeMobySaveMap.PatchExe(a); NativeDrawBufferGuard.PatchExe(a, new Dictionary<int, int> { [65] = 1536 });
    NativeHover.Install(a);
    Check(NativeHover.IsInstalled(a) && NativeExtendedRam.IsInstalled(a), "after the 8 MB placement, save map and draw-buffer guard");
    byte[] c = original.ToArray(); NativeHover.Install(c); NativeExtendedRam.PatchExe(c); NativeMobySaveMap.PatchExe(c); NativeDrawBufferGuard.PatchExe(c, new Dictionary<int, int> { [65] = 1536 });
    Check(c.AsSpan().SequenceEqual(a), "and before them: the same game either way");
    Check(exe.AsSpan((int)(0x8006C470 - 0x8000F800), 45).SequenceEqual(original.AsSpan((int)(0x8006C470 - 0x8000F800), 45)), "the state -> animation table other features check is unchanged");

    // The flap: every level of the game that lacks it and has room, once.
    string repo = AppContext.BaseDirectory;
    while (!File.Exists(Path.Combine(repo, "spyro-level-catalog.json"))) repo = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(repo)) ?? throw new IOException("No level catalog.");
    var catalog = LevelCatalog.Load(repo);
    var flap = await NativeHoverFlap.CaptureAsync(args[1]);
    Check(catalog.Levels.Count == 35 && catalog.Levels.Count(l => NativeHoverFlap.CarriedBy(l.LevelId)) == 6 &&
        catalog.Levels.All(l => flap.Carries(disc.EntryBytes(l.SourceWadEntry)) == NativeHoverFlap.CarriedBy(l.LevelId)), "the flight levels and Gnasty's Loot carry the flap, no other level");
    Dictionary<int, byte[]> replacements = [];
    IReadOnlyList<string> noRoom;
    using (GameRam.Use(GameMemoryMode.PlayStation2MB)) noRoom = flap.ApplyForExport(replacements, disc.EntryBytes, exe, catalog.Levels, disc);
    Check(noRoom.Order().SequenceEqual(["Haunted Towers", "Tree Tops", "Twilight Harbor"]), "with 2 MB three levels have no room: " + string.Join(", ", noRoom));
    Check(replacements.Count == 26 && replacements.All(r => r.Value.Length == disc.EntryBytes(r.Key).Length + UniversalLevelData.Align(NativeHoverFlap.ByteLength, 0x800) && flap.Carries(r.Value)),
        "the other 26 levels get it (4,512 bytes, three more sectors each)");
    var artisans = catalog.FindByKey("artisans")!;
    Check(replacements.ContainsKey(artisans.SourceWadEntry) && !replacements.Keys.Any(e => e % 2 == 1 && e < 80 && e != artisans.SourceWadEntry && !catalog.Levels.Any(l => l.SourceWadEntry == e)),
        "only level data changes, never an overlay");
    var data = new UniversalLevelData(replacements[artisans.SourceWadEntry]);
    var shipped = new UniversalLevelData(disc.EntryBytes(artisans.SourceWadEntry));
    Check(data.Subfiles()[0] == shipped.Subfiles()[0] && data.Bytes.AsSpan(data.Subfiles()[0].Offset, data.Subfiles()[0].Length).SequenceEqual(shipped.Bytes.AsSpan(shipped.Subfiles()[0].Offset, shipped.Subfiles()[0].Length)),
        "Artisans' texture memory is unchanged");
    var again2 = new Dictionary<int, byte[]>(replacements);
    using (GameRam.Use(GameMemoryMode.PlayStation2MB)) flap.ApplyForExport(again2, disc.EntryBytes, exe, catalog.Levels, disc);
    Check(again2.Count == replacements.Count && again2.All(r => ReferenceEquals(r.Value, replacements[r.Key])), "adding it again changes nothing");
    // The flap never costs a level its place on a PlayStation: the 8 MB mode leaves the same three without it ...
    Dictionary<int, byte[]> eight = [];
    using (GameRam.Use(GameMemoryMode.Emulator8MB))
    {
        var none = flap.ApplyForExport(eight, disc.EntryBytes, exe, catalog.Levels, disc);
        Check(none.Order().SequenceEqual(["Haunted Towers", "Tree Tops", "Twilight Harbor"]) && eight.Count == 26,
            "with 8 MB the same three stay without it, so the game still fits 2 MB: " + string.Join(", ", none));
        Check(NativeExtendedRam.Survey(exe, e => eight.TryGetValue(e, out var b) ? b : disc.EntryBytes(e), catalog.Levels, disc).All(f => f.FitsPlayStation),
            "every level of the game with the flap still fits a PlayStation");
        // ... while a level that needs 8 MB anyway takes it there, and has no room with 2 MB.
        var treeTops = catalog.Levels.Single(l => l.LevelId == 43);
        uint copy = new UniversalOverlay(disc.EntryBytes(treeTops.SourceWadEntry - 1), UniversalExeBindings.Read(exe, treeTops.LevelId)).CopyBuffer;
        var grown = new UniversalLevelData(disc.EntryBytes(treeTops.SourceWadEntry));
        grown.GrowSubfile(3, 0x4000);
        byte[]? withFlap = flap.Merge(grown.Bytes, copy);
        Check(withFlap != null && flap.Carries(withFlap), "a level already past 2 MB takes the flap with 8 MB");
        using (GameRam.Use(GameMemoryMode.PlayStation2MB)) Check(flap.Merge(grown.Bytes, copy) == null, "and has no room for it with 2 MB");
    }
    Console.WriteLine($"HOVER_SOURCE_OK checks={checks}");
    return 0;
}
Console.Error.WriteLine("usage: HoverSmoke [--source <USA.bin>]");
return 2;

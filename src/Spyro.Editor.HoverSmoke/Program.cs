using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Universal;

// Spyro 3's hover (NativeHover) and its Game Settings choice (SpyroMovesSettings). Default: source-free (the saved
// choice, the new state-16 entry's code, Create BIN's scope, refusals). --source <USA.bin>: installed into the USA
// executable it changes exactly its two places, again changes nothing, refuses tampered code, and leaves the other
// executable features' places alone (8 MB scene placement, save map, draw-buffer guard, the state -> animation table).
int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
void Refuse(Action action, string message) { try { action(); } catch (InvalidDataException) { checks++; return; } throw new Exception(message); }
uint Word(byte[] b, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan((int)(address - 0x8000F800)));

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

    // The new entry: 24 words in place, branches to the hover and to the stores, the jump into state 6's steps.
    uint[] code = NativeHover.EnterCode();
    uint At(int slot) => NativeHover.EnterAddress + 4 * (uint)slot;
    Check(code.Length == 24 && code.Skip(16).All(w => w == 0), "the entry keeps its 24 words, the unused end is nop");
    Check(UniversalMips.Op(code[5]) == 4 && UniversalMips.BranchTarget(code[5], At(5)) == At(9), "from the glide (15) it branches to the hover");
    Check(UniversalMips.Op(code[7]) == 5 && UniversalMips.BranchTarget(code[7], At(7)) == At(11), "from anything but the other glide (23) it skips the hover");
    Check(code[9] == UniversalMips.EncodeI(9, 0, 2, NativeHover.RiseSpeed) && NativeHover.RiseSpeed is > 0 and < 0x8000, "the hover sets an upward speed");
    Check(UniversalMips.Op(code[14]) == 2 && ((code[14] & 0x3FFFFFF) << 2 | 0x80000000) == NativeHover.SharedTail, "it continues with state 6's steps");
    Check(code.Count(w => UniversalMips.IsStore(w)) == 4, "four fields are written: vertical and forward speed, flight sub-state, sideways speed");

    // Create BIN's scope: off unless the export asks; scopes nest and end.
    Check(!NativeHover.Enabled, "no export, no hover");
    byte[] blank = new byte[0x66000];
    NativeHover.ApplyForExport(blank);
    Check(blank.All(b => b == 0), "with the scope off the executable is left alone");
    using (NativeHover.Use(true))
    {
        Check(NativeHover.Enabled, "the export asks for the hover");
        using (NativeHover.Use(false)) Check(!NativeHover.Enabled, "an inner scope can turn it off");
        Check(NativeHover.Enabled, "the outer choice comes back");
        Refuse(() => NativeHover.ApplyForExport(blank), "an executable that is not the USA game's is refused");
        Check(blank.All(b => b == 0), "and left untouched");
    }
    Check(!NativeHover.Enabled, "the scope ends with the export");
    Refuse(() => NativeHover.Install(new byte[0x1000]), "a truncated executable is refused");
    Console.WriteLine($"HOVER_FREE_OK checks={checks}");
    return 0;
}

if (args.Length >= 2 && args[0] == "--source")
{
    byte[] original = UniversalDiscImage.Read(args[1]).Exe.ToArray();
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
    Check(Word(exe, NativeHover.RegrabAddress) == 0x30420000, "state 16 no longer reads ✕ to glide again");
    byte[] again = exe.ToArray(); NativeHover.Install(again);
    Check(again.AsSpan().SequenceEqual(exe), "installing again changes nothing");

    // Refusals leave the executable untouched.
    byte[] tail = original.ToArray(); tail[(int)(NativeHover.SharedTail - 0x8000F800) + 4] ^= 1; byte[] tailCopy = tail.ToArray();
    Refuse(() => NativeHover.Install(tail), "changed falling code is refused");
    Check(tail.AsSpan().SequenceEqual(tailCopy), "untouched");
    byte[] other = original.ToArray(); other[(int)(NativeHover.RegrabAddress - 0x8000F800)] ^= 0x10; byte[] otherCopy = other.ToArray();
    Refuse(() => NativeHover.Install(other), "another change at the ✕ test is refused");
    Check(other.AsSpan().SequenceEqual(otherCopy), "untouched");

    // The other executable features keep their places (in both orders) and the shared animation table is the USA one.
    byte[] a = original.ToArray(); NativeExtendedRam.PatchExe(a); NativeMobySaveMap.PatchExe(a); NativeDrawBufferGuard.PatchExe(a, new Dictionary<int, int> { [65] = 1536 });
    byte[] b = a.ToArray();
    NativeHover.Install(a);
    Check(NativeHover.IsInstalled(a) && NativeExtendedRam.IsInstalled(a), "after the 8 MB placement, save map and draw-buffer guard");
    byte[] c = original.ToArray(); NativeHover.Install(c); NativeExtendedRam.PatchExe(c); NativeMobySaveMap.PatchExe(c); NativeDrawBufferGuard.PatchExe(c, new Dictionary<int, int> { [65] = 1536 });
    Check(c.AsSpan().SequenceEqual(a), "and before them: the same game either way");
    Check(exe.AsSpan((int)(0x8006C470 - 0x8000F800), 45).SequenceEqual(original.AsSpan((int)(0x8006C470 - 0x8000F800), 45)), "the state -> animation table other features check is unchanged");
    Console.WriteLine($"HOVER_SOURCE_OK checks={checks}");
    return 0;
}
Console.Error.WriteLine("usage: HoverSmoke [--source <USA.bin>]");
return 2;

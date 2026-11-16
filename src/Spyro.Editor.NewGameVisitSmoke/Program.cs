// New Game starting outside Artisans marks Artisans visited, so Load Game lists the saves (NativeNewGameArtisansVisit).
//   (no arguments)        source-free checks on a made-up executable holding the USA level-entry words
//   --source USA.bin      the original game's executable (read only): the words are where the fix expects them, nothing
//                         branches into them, a game starting in Artisans stays byte for byte the same, every other start
//                         changes exactly the two start words and the two level-entry words
using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Universal;

int checks = 0;
void Check(bool ok, string what) { checks++; if (!ok) throw new Exception("FAILED: " + what); }
void Refuse(Action act, string what) { try { act(); } catch (InvalidDataException) { checks++; return; } throw new Exception("NOT REFUSED: " + what); }
const uint Base = 0x8000F800;
int Off(uint address) => checked((int)(address - Base));
uint Word(byte[] exe, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(Off(address)));
void Put(byte[] exe, uint address, uint word) => BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(Off(address)), word);
const uint First = NativeNewGameArtisansVisit.FirstAddress, Second = NativeNewGameArtisansVisit.SecondAddress;

if (args.Length >= 2 && args[0] == "--source") return Source(args[1]);
return Free();

int Free()
{
    // A made-up executable with the USA game's level-entry words (0x80015CF8..0x80015D78).
    var exe = new byte[0x66000];
    (uint, uint)[] game =
    [
        (0x80015CF8, 0x3C138007), (0x80015CFC, 0x26737888), (0x80015D5C, 0x3C018007), (0x80015D60, 0xAC205830),
        (0x80015D64, 0x3C018007), (0x80015D68, 0xAC2056C8), (0x80015D6C, 0x3C018007), (0x80015D70, 0xAC22587C),
        (0x80015D74, 0x0C004D93), (0x80015D78, 0x02002021),
    ];
    foreach (var (a, w) in game) Put(exe, a, w);
    var original = exe.ToArray();

    Check(!NativeNewGameArtisansVisit.Needed(10), "a game starting in Artisans needs nothing");
    foreach (int id in new[] { 11, 12, 15, 20, 21, 30, 46, 60, 64, 65, 70, 71, 74 })
        Check(NativeNewGameArtisansVisit.Needed(id), $"New Game starting in level {id} marks Artisans visited");

    var on = exe.ToArray(); NativeNewGameArtisansVisit.Apply(on, true);
    Check(NativeNewGameArtisansVisit.IsInstalled(on) && !NativeNewGameArtisansVisit.IsInstalled(original), "installed only when asked");
    Check(Word(on, First) == 0x24040001 && Word(on, Second) == 0xA26415F0, "the two words: addiu a0,zero,1 / sb a0,0x15F0(s3)");
    Check(Enumerable.Range(0, on.Length).Count(i => on[i] != original[i]) <= 8 &&
          Enumerable.Range(0, on.Length).Where(i => on[i] != original[i]).All(i => i / 4 == Off(First) / 4 || i / 4 == Off(Second) / 4),
        "nothing else changes");
    // Decode what the two instructions do: a0 = 1, then the byte at s3 + 0x15F0, with s3 built by the game's lui/addiu.
    uint set = Word(on, First), mark = Word(on, Second);
    Check(set >> 26 == 9 && ((set >> 21) & 31) == 0 && ((set >> 16) & 31) == 4 && (short)(set & 0xFFFF) == 1, "addiu a0,zero,1");
    Check(mark >> 26 == 0x28 && ((mark >> 21) & 31) == 19 && ((mark >> 16) & 31) == 4, "sb a0,imm(s3)");
    uint s3 = (Word(on, 0x80015CF8) & 0xFFFF) << 16;
    s3 = (uint)(s3 + (short)(Word(on, 0x80015CFC) & 0xFFFF));
    Check(s3 == 0x80077888 && (uint)(s3 + (short)(mark & 0xFFFF)) == NativeNewGameArtisansVisit.ArtisansVisited && NativeNewGameArtisansVisit.ArtisansVisited == 0x80078E78,
        "it writes Artisans' visited byte (0x80078E78, the save record's byte 0x40)");
    // The replaced words were both "lui at,0x8007" with at already 0x80070000 from 0x80015D5C: the stores after them still see it.
    Check(Word(original, First) == 0x3C018007 && Word(original, Second) == 0x3C018007 && Word(original, 0x80015D5C) == 0x3C018007, "only repeated lui at,0x8007 are replaced");

    var twice = on.ToArray(); NativeNewGameArtisansVisit.Apply(twice, true);
    Check(twice.AsSpan().SequenceEqual(on), "installing twice changes nothing");
    var back = on.ToArray(); NativeNewGameArtisansVisit.Apply(back, false);
    Check(back.AsSpan().SequenceEqual(original), "taken out: the game's words back, byte for byte");
    var off = original.ToArray(); NativeNewGameArtisansVisit.Apply(off, false);
    Check(off.AsSpan().SequenceEqual(original), "a game starting in Artisans is left exactly as it was");

    // Off never touches words it did not write, even unusual ones; on refuses them and changes nothing.
    var other = original.ToArray(); Put(other, Second, 0x12345678); var otherBefore = other.ToArray();
    NativeNewGameArtisansVisit.Apply(other, false);
    Check(other.AsSpan().SequenceEqual(otherBefore), "off leaves another change's words alone");
    Refuse(() => NativeNewGameArtisansVisit.Apply(other, true), "another change owns the words");
    Check(other.AsSpan().SequenceEqual(otherBefore), "refusal changes nothing");
    foreach (var (a, _) in game)
    {
        if (a == First || a == Second) continue;
        var changed = original.ToArray(); Put(changed, a, 0); var before = changed.ToArray();
        Refuse(() => NativeNewGameArtisansVisit.Apply(changed, true), $"level-entry code differing at 0x{a:X8}");
        Check(changed.AsSpan().SequenceEqual(before), $"refusal at 0x{a:X8} changes nothing");
    }
    Refuse(() => NativeNewGameArtisansVisit.Apply(new byte[0x100], true), "a too-short executable");
    var tiny = new byte[0x100]; NativeNewGameArtisansVisit.Apply(tiny, false);
    Check(tiny.All(b => b == 0), "off on a too-short executable does nothing");

    Console.WriteLine($"NEW_GAME_VISIT_FREE_OK checks={checks}");
    return 0;
}

int Source(string usa)
{
    var exe = UniversalDiscImage.Read(usa).Exe;
    Check(exe.Length == 0x66000, "the USA executable");
    Check(Word(exe, First) == 0x3C018007 && Word(exe, Second) == 0x3C018007, "the game's two words are lui at,0x8007");
    var probe = exe.ToArray(); NativeNewGameArtisansVisit.Apply(probe, true);
    Check(NativeNewGameArtisansVisit.IsInstalled(probe), "the fix fits the original level-entry code");

    // Nothing in the executable branches or jumps into 0x80015CFC..0x80015D78 (the code there runs straight from the
    // jump-table case at 0x80015CCC, so s3 and at hold what the two new instructions expect), and no table points into it.
    const uint lo = 0x80015CF8, hi = 0x80015D78;
    int into = 0, cases = 0;
    for (uint a = 0x80010000; a < 0x80010000 + (uint)(exe.Length - 0x800); a += 4)
    {
        uint w = Word(exe, a), op = w >> 26;
        if (op is 1 or 4 or 5 or 6 or 7 or 20 or 21 or 22 or 23)
        {
            uint t = (uint)(a + 4 + ((short)(w & 0xFFFF) << 2));
            if (t > lo && t <= hi && (a < lo || a >= hi)) into++;
        }
        else if (op is 2 or 3)
        {
            uint t = (a & 0xF0000000) | ((w & 0x3FFFFFF) << 2);
            if (t > lo && t <= hi) into++;
        }
        if (w > lo && w <= hi) into++;
        if (w == 0x80015CCC) cases++;
    }
    Check(into == 0 && cases == 1, "nothing enters the level-entry code between s3's setup and the call after the new words");

    // A game starting in Artisans: byte for byte the original.
    var artisans = exe.ToArray(); NativeClearedRetailBinding.ApplyNewGameStart(exe, artisans, 10);
    Check(artisans.AsSpan().SequenceEqual(exe), "New Game in Artisans: the executable is the game's own");
    var freshArtisans = exe.ToArray(); NativeFreshNewGameEntry.Apply(exe, freshArtisans, false);
    Check(freshArtisans.AsSpan().SequenceEqual(exe), "the fresh-level path starting in Artisans: the game's own");

    // Every other start: the two start words and the two level-entry words, nothing else; back to the game for Artisans.
    foreach (int id in NativeClearedRetailBinding.Census(exe).Select(p => p.LevelId).Append(65))
    {
        var staged = exe.ToArray(); NativeClearedRetailBinding.ApplyNewGameStart(exe, staged, id);
        var words = Enumerable.Range(0, exe.Length / 4).Where(i => Word(staged, Base + (uint)i * 4) != Word(exe, Base + (uint)i * 4)).Select(i => Base + (uint)i * 4).ToArray();
        if (id == 10) Check(words.Length == 0, "level 10 changes nothing");
        else Check(words.SequenceEqual(new[] { 0x80012780u, 0x80015D64u, 0x80015D6Cu, 0x8002D4F0u }) && NativeNewGameArtisansVisit.IsInstalled(staged),
            $"New Game in level {id}: its start and Artisans' visit, nothing else");
        NativeClearedRetailBinding.ApplyNewGameStart(exe, staged, 10);
        Check(staged.AsSpan().SequenceEqual(exe), $"level {id} then Artisans again: the game's own executable");
    }
    var fresh = exe.ToArray(); NativeFreshNewGameEntry.Apply(exe, fresh, true);
    Check(NativeNewGameArtisansVisit.IsInstalled(fresh) && Word(fresh, 0x80012780) == 0x24020041, "the fresh-level path starting in the new level marks Artisans too");

    Console.WriteLine($"NEW_GAME_VISIT_SOURCE_OK checks={checks}");
    return 0;
}

using System.Buffers.Binary;

namespace Spyro.Editor.Core.Universal;

/// <summary>Hands classes that the destination's own update switch handles over to imported behavior. A rebuilt
/// level's kernel overlay carries its donor homeworld's handlers for the dragons (0xFA statue, 0x6E dragon), whose code
/// differs per homeworld, while the game's rescue loader finds the statue by its native class number: the level's own
/// dragons must keep that number and run their own code. The switch's class load is followed by a store through the
/// assembler register (lui at, hi; s* rt, lo(at)) just before the first class compare; the hook replaces that pair
/// with a jump to a stub that does the store, then sends released classes down the switch's default path (as class
/// 0x7FFF, which no level uses), so only the imported handler updates them.</summary>
public static class UniversalNativeRelease
{
    private const int At = 1;

    public static int Bytes(int classes) => 4 * (2 + 2 * classes + 2 + 3);

    /// <summary>Writes the stub at <paramref name="stub"/> and the hook into <paramref name="overlay"/> (the destination's
    /// bytes at <see cref="UniversalOverlay.Base"/>, already holding the bundle).</summary>
    public static void Install(byte[] overlay, UniversalOverlay destination, IReadOnlyCollection<int> classes, uint stub)
    {
        if (classes.Count == 0) return;
        var sw = UniversalDispatchAnalysis.FindUpdateSwitch(destination);
        int creg = sw.ClassRegister;
        // The first instruction after the class load that reads the class register is the first compare.
        uint compare = sw.TreeStart;
        for (; ; compare += 4)
        {
            if (compare >= destination.FunctionEnd(sw.Function)) throw new InvalidDataException("The level's update switch has no class compare.");
            uint w = destination.Word(compare);
            if (UniversalMips.Rs(w) == creg || (UniversalMips.Op(w) is 0 or 4 or 5 && UniversalMips.Rt(w) == creg)) break;
        }
        uint pair = compare - 8, lui = destination.Word(pair), store = destination.Word(pair + 4);
        if (pair < sw.TreeStart || UniversalMips.Op(lui) != 15 || UniversalMips.Rt(lui) != At || !UniversalMips.IsStore(store) || UniversalMips.Rs(store) != At ||
            UniversalMips.Rt(store) == creg)
            throw new InvalidDataException("The level's update switch does not begin the way the dragon release hook expects.");
        // Nothing may enter between the pair's two words.
        for (uint a = destination.CodeStart; a + 4 <= destination.End; a += 4)
        {
            uint w = destination.Word(a);
            if (UniversalMips.IsBranch(w) && !UniversalMips.IsJr(w) && !UniversalMips.IsJalr(w) && UniversalMips.BranchTarget(w, a) == pair + 4)
                throw new InvalidDataException("Code branches into the dragon release hook's site.");
        }
        for (uint a = UniversalOverlay.Base; a + 4 <= destination.End; a += 4)
            if (destination.Word(a) == pair + 4) throw new InvalidDataException("A table points into the dragon release hook's site.");
        var trace = UniversalDispatchAnalysis.Trace(destination, sw.TreeStart, UniversalDispatchAnalysis.SentinelClass, sw.Seeds, creg);
        if (trace.Stop != sw.DefaultStop) throw new InvalidDataException("The level's update switch has no default path for released classes.");
        foreach (int c in classes)
            if (c is <= 0 or >= 0x8000 || !UniversalDispatchAnalysis.Handles(destination, sw, c))
                throw new InvalidDataException($"Class 0x{c:X3} is not one the level's own switch handles.");

        List<uint> code = [store];
        var ordered = classes.Order().ToArray();
        int release = 2 + 2 * ordered.Length + 2;
        code.Add(UniversalMips.EncodeI(9, 0, At, ordered[0]));                                         // addiu at, zero, class
        for (int i = 0; i < ordered.Length; i++)
        {
            code.Add(UniversalMips.EncodeI(4, creg, At, release - (code.Count + 1)));                  // beq class, at, release
            code.Add(i + 1 < ordered.Length ? UniversalMips.EncodeI(9, 0, At, ordered[i + 1]) : UniversalMips.Nop);
        }
        code.Add(UniversalMips.EncodeJ(2, compare)); code.Add(lui);                                    // j compare; lui at, hi
        code.Add(UniversalMips.EncodeI(9, 0, creg, UniversalDispatchAnalysis.SentinelClass));           // release: class = 0x7FFF
        code.Add(UniversalMips.EncodeJ(2, compare)); code.Add(lui);
        if (code.Count * 4 != Bytes(ordered.Length)) throw new InvalidOperationException("Dragon release stub size changed.");
        int Off(uint address) => checked((int)(address - UniversalOverlay.Base));
        if (stub % 4 != 0 || Off(stub) + code.Count * 4 > overlay.Length) throw new InvalidDataException("The dragon release stub does not fit the overlay.");
        for (int i = 0; i < code.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(overlay.AsSpan(Off(stub) + 4 * i), code[i]);
        BinaryPrimitives.WriteUInt32LittleEndian(overlay.AsSpan(Off(pair)), UniversalMips.EncodeJ(2, stub));
        BinaryPrimitives.WriteUInt32LittleEndian(overlay.AsSpan(Off(pair) + 4), lui);
    }
}

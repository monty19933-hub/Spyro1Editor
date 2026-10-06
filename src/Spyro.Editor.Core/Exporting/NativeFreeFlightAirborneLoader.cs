using System.Buffers.Binary;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Exact in-place replacement of the local player-stream loader.
/// Missing normal flight slots reuse resident airborne slot16. Native local
/// records are then loaded normally and take precedence (including level64).
/// No new asset, RAM field, allocator or frame-pointer encoding is introduced.
/// The controller continues to request native free-flight states32..34.</summary>
internal static class NativeFreeFlightAirborneLoader
{
    internal const uint Address = 0x80013230;
    internal const int ByteLength = 0x1B0;
    internal const int ResidentAnimationId = 16;
    internal const string SourceSha256 = "0cca46f0e44c16b8adea088e3081637ec5b04a512dc2c8eac494f9d16732cefb";
    private const string BeforeHex = "f8ffbd2721308000212000000880083cfc850825ffff07240780033c7863638c80100400212843003800a38c0000028d000000002b106200030040140000000002006714000000003800a0ac010084242e008228f0ff4014000000000000c38c000000004f0060041f00093cffff2935e0ff0b3c0400c6240000ca8c0400c6240780023c7863428c803803002110e200380046ac0780033c7863638c000000002110e3003800448c3400628c1400838c0000000021104300140082ac0780033c7863638c000000002110e3003800448c3400628c1800838c0000000021104300180082ac0780023c7863428c000000002110e2003800448c00000000000082841000838c80100200240042242140c20021180301100083ac0780023c7863428c000000002110e2003800428c21200000000043840000000015006018240045240000a38c000000002410690021104800431002002410490024186b00211862000000a3ac0780023c7863428c000000002110e2003800428c0100842400004284000000002a108200edff40140400a5242130ca000000c38c00000000b6ff61040400c6240800bd270800e00300000000";

    /// <summary>Bytes of the rewritten loader actually holding code; the rest of the owner is zero.</summary>
    internal static int UsedBytes { get; private set; }

    /// <summary>Bit (levelId - 10) of the mask the level-set loader consults; 56 bits cover IDs 10..65.</summary>
    internal static ulong LevelMask(IEnumerable<int> levelIds)
    {
        ulong mask = 0;
        foreach (int id in levelIds)
        {
            if (id < 10 || id > 65)
                throw new ArgumentException("Free Flight level masks cover IDs 10..65 only.", nameof(levelIds));
            mask |= 1UL << (id - 10);
        }
        return mask;
    }

    /// <summary>
    /// The global loader fills the missing flight slots 33..35 of every normal level. With a mask it fills
    /// them only for the levels in the mask (the user's choice, plus native64): the mask replaces the
    /// remainder classification, so flight courses and unlisted normal levels keep the native clear.
    /// Everything after the classification is byte-identical to the global loader.
    /// </summary>
    internal static NativeFreeFlightWindow Build(ulong? enabledLevelMask = null)
    {
        List<uint> code = [];
        Dictionary<string, int> labels = [];
        List<(int Index, int Op, int Rs, int Rt, string Label)> branches = [];
        List<(int Index, string Label)> jumps = [];
        void Emit(uint word) => code.Add(word);
        void Label(string name) => labels.Add(name, code.Count);
        void Branch(int op, int rs, int rt, string label)
        { branches.Add((code.Count, op, rs, rt, label)); Emit(0); }
        void Jump(string label) { jumps.Add((code.Count, label)); Emit(0); }
        // t4 caches the root table; preserve it in the original8-byte stack
        // owner. All other scratch registers were already caller-saved here.
        Emit(I(9, 29, 29, -8)); Emit(I(43, 29, 12, 0)); Emit(R(4, 0, 6, 0, 0x21));
        Emit(I(15, 0, 2, 0x8007)); Emit(I(35, 2, 12, 0x6378)); Emit(I(35, 2, 9, 0x596C));
        Emit(I(15, 0, 8, 0x8008)); Emit(I(35, 8, 8, -0x7A04));
        if (enabledLevelMask is ulong mask)
        {
            // t1 = 0 (fill) when bit (level - 10) of the mask is set, else 1 (native clear). The 56-bit
            // mask is two immediates: word 0 for IDs 10..41, word 1 for 42..65. Out-of-range IDs clear.
            uint low = (uint)(mask & 0xFFFFFFFFUL), high = (uint)(mask >> 32);
            Emit(I(9, 9, 3, -10)); Emit(I(11, 3, 2, 56));
            Branch(4, 2, 0, "classified"); Emit(I(9, 0, 9, 1));
            Emit(I(15, 0, 1, (int)(low >> 16))); Emit(I(13, 1, 1, (int)(low & 0xFFFF)));
            Emit(I(11, 3, 2, 32)); Branch(5, 2, 0, "shift"); Emit(0);
            Emit(I(15, 0, 1, (int)(high >> 16))); Emit(I(13, 1, 1, (int)(high & 0xFFFF)));
            Emit(I(9, 3, 3, -32));
            Label("shift"); Emit(R(3, 1, 2, 0, 6)); // srlv v0, at, v1
            Emit(I(12, 2, 2, 1)); Emit(I(14, 2, 9, 1));
        }
        else
        {
            // Source catalog IDs are10..64 with remainder0..4; issued fresh65
            // has the same normal-player contract. Currentlevel is read directly
            // so a previous flight-course classifier cannot leak across a load.
            Emit(I(9, 9, 3, -10)); Emit(I(11, 3, 2, 56));
            Branch(4, 2, 0, "classified"); Emit(I(9, 0, 9, 1));
            Emit(I(9, 0, 2, 55)); Branch(4, 3, 2, "normal"); Emit(0);
            Label("modulo"); Emit(I(11, 3, 2, 10)); Branch(5, 2, 0, "remainder"); Emit(0);
            Jump("modulo"); Emit(I(9, 3, 3, -10));
            Label("remainder"); Emit(I(11, 3, 9, 5)); Emit(I(14, 9, 9, 1));
            Jump("classified"); Emit(0);
            Label("normal"); Emit(I(9, 0, 9, 0));
        }
        Label("classified"); Emit(I(9, 0, 7, -1)); Emit(I(9, 0, 4, 0)); Emit(R(12, 0, 5, 0, 0x21));
        Label("clear-loop"); Emit(I(35, 5, 3, 0x38)); Emit(I(9, 4, 2, -33));
        Emit(I(11, 2, 10, 3)); Emit(R(3, 8, 2, 0, 0x2B));
        if (enabledLevelMask != null)
        {
            // A chosen level leaves the resident fallback in slots 33..35, and that pointer is a valid resident
            // address the native stale test keeps. A level outside the set must not inherit it: its flight slots
            // are always cleared here, and a course's or level64's own records are loaded afterwards as before.
            Branch(4, 10, 0, "validity"); Emit(0); Branch(5, 9, 0, "clear"); Emit(0);
            Label("validity");
        }
        Branch(5, 2, 0, "clear"); Emit(0); Branch(5, 3, 7, "next-slot"); Emit(0);
        Label("clear"); Branch(4, 9, 0, "potential"); Emit(R(0, 0, 3, 0, 0x21));
        Jump("store"); Emit(0);
        Label("potential"); Branch(4, 10, 0, "store"); Emit(0);
        Emit(I(35, 12, 3, 0x78)); Emit(0);
        Label("store"); Emit(I(43, 5, 3, 0x38));
        Label("next-slot"); Emit(I(9, 4, 4, 1)); Emit(I(10, 4, 2, 46));
        Branch(5, 2, 0, "clear-loop"); Emit(I(9, 5, 5, 4));
        Emit(I(35, 6, 3, 0)); Emit(I(15, 0, 9, 0x1F));
        Branch(1, 3, 0, "done"); Emit(I(13, 9, 9, 0xFFFF));
        Emit(I(15, 0, 11, 0xFFE0)); Emit(I(9, 6, 6, 4));
        Label("record"); Emit(I(35, 6, 10, 0)); Emit(I(9, 6, 6, 4));
        Emit(R(0, 3, 7, 2, 0)); Emit(R(7, 12, 2, 0, 0x21)); Emit(I(43, 2, 6, 0x38));
        Emit(R(6, 0, 4, 0, 0x21)); Emit(I(35, 12, 2, 0x34)); Emit(I(35, 4, 3, 0x14));
        Emit(0); Emit(R(3, 2, 3, 0, 0x21)); Emit(I(43, 4, 3, 0x14));
        Emit(I(35, 4, 3, 0x18)); Emit(I(33, 4, 2, 0));
        // Delayed LH lets this ADDU use the old geometry base; the next SLL
        // sees the loaded signed frame count. This is tested on strict R3000
        // load-delay semantics against the complete original loader.
        Emit(R(3, 2, 3, 0, 0x21)); Emit(I(43, 4, 3, 0x18));
        Emit(R(0, 2, 2, 2, 0)); Emit(I(9, 2, 2, 0x24)); Emit(R(6, 2, 8, 0, 0x21));
        Emit(I(35, 4, 3, 0x10)); Emit(I(9, 4, 5, 0x24)); Emit(R(8, 3, 3, 0, 0x21));
        Emit(I(43, 4, 3, 0x10)); Emit(I(33, 4, 3, 0)); Emit(I(9, 0, 7, 0));
        Branch(6, 3, 0, "record-end"); Emit(0);
        Label("frame"); Emit(I(35, 5, 3, 0)); Emit(0);
        Emit(R(3, 9, 2, 0, 0x24)); Emit(R(2, 8, 2, 0, 0x21)); Emit(R(0, 2, 2, 1, 3));
        Emit(R(2, 9, 2, 0, 0x24)); Emit(R(3, 11, 3, 0, 0x24)); Emit(R(3, 2, 3, 0, 0x21));
        Emit(I(43, 5, 3, 0)); Emit(I(33, 4, 2, 0)); Emit(I(9, 7, 7, 1));
        Emit(R(7, 2, 2, 0, 0x2A)); Branch(5, 2, 0, "frame"); Emit(I(9, 5, 5, 4));
        Label("record-end"); Emit(R(6, 10, 6, 0, 0x21)); Emit(I(35, 6, 3, 0)); Emit(0);
        Branch(1, 3, 1, "record"); Emit(I(9, 6, 6, 4));
        Label("done"); Emit(I(35, 29, 12, 0)); Emit(I(9, 29, 29, 8)); Emit(R(31, 0, 0, 0, 8)); Emit(0);
        foreach (var b in branches) code[b.Index] = I(b.Op, b.Rs, b.Rt, labels[b.Label] - b.Index - 1);
        foreach (var j in jumps) code[j.Index] = 0x08000000 | ((Address + (uint)labels[j.Label] * 4) >> 2 & 0x03FFFFFF);
        if (code.Count * 4 > ByteLength) throw new InvalidDataException("Resident airborne loader exceeded its original function owner.");
        UsedBytes = code.Count * 4;
        byte[] after = new byte[ByteLength];
        for (int i = 0; i < code.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(after.AsSpan(i * 4), code[i]);
        ExecutablePatchSafety.GuardPatchRange(Address, ByteLength, "Global resident airborne loader");
        return new(enabledLevelMask == null ? "normal-resident-airborne-loader" : "level-set-resident-airborne-loader", Address, Convert.FromHexString(BeforeHex), after);
    }

    private static uint I(int op, int rs, int rt, int immediate) => (uint)op << 26 | (uint)rs << 21 | (uint)rt << 16 | unchecked((ushort)immediate);
    private static uint R(int rs, int rt, int rd, int shift, int fn) => (uint)rs << 21 | (uint)rt << 16 | (uint)rd << 11 | (uint)shift << 6 | (uint)fn;
}

using System.Buffers.Binary;
using System.Security.Cryptography;

internal static class NativeScalarCases
{
    private const uint ChoicePc = 0x80027244, PacketPc = 0x80027508;
    private const uint NativeClip = 0x8000;
    private const string ClipInitializationHex = "008002340780013c605720ac0880013ccc8522ac";
    private const string LoaderHex = "b0ffbd27213080004800b0af2180c0000400c6244c00bfaf0000c38c0400c6240880043cc88584240880013cc08526ac001103002130c200000083ac0880013cc48526ac0000028e0000000021300202";
    private const string BaseHex = "0880013ca88521242400358c1800338c";
    private const string FadePreparationHex = "0100a5310200a010826815000020ad21";
    private const string ChoiceHex = "2050220020504301205043012258aa0247fe601900e04b212c0061058000ab312a00601dc0fe2b2015006005c0fe4b2013006005c0fe6b20110060054000ab312200601d01002b330400601d010039378258190000008baf04009c23f0ff2b22024909002058690102006b3500008baf04009c2302000b20dd9a0008ffffcba22bfe40114000ab31efff601d01002b3302002b330400601d02003937825819000000abaf0400bd23f0ff2b22024909002058690102006b350000abaf0400bd2304000b20dd9a0008ffffcba2f4ff2c8ec2510a003800ab31";
    private const string PacketHex = "40410a0022400d0100100821f8ff2c8e00006d220300001d02420800000008200800ad2101000220ffffc2a27f008a3100510a0020504d01000e0c00090021040009053c360002200700c2a3030e010001002220320040107f0021300051010020504d010000418d0400428d00038c3180450800200828000600801d00ff8c21001f23200c00c1af1800c2af7d9d00082400c3af0700801d00ff8c211f0023200c00c3af001f42201800c2af7d9d00082400c1af0700801d000000001f1f21200c00c1afe11e43201800c3af7d9d00082400c2af001f23200c00c3afe1ff42201800c2af1f1f21202400c1af0000c5af205874010000618d00007ead0500201002141e0000003ea4020022a0dd9a00082800de2304007eaddd9a00082800de231400c18f2000c28f1000c38f1c00c48f1000c1af1800c2af0c00c3af1400c4af300001200700c1a30006013c0000c1af205874010000618d00007ead0500201002141e0000003ea4020022a0dd9a00081c00de2304007eaddd9a00081c00de23";
    private const uint Table = 0x300000, Face = 0x310000, Packet = 0x200000, Ordering = 0x210000, State = 0x220000;

    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        // Synthetic independent machine self-tests distinguish old/new values
        // across native load delay and exactly one executed branch delay slot.
        var delayed = new Machine(); delayed.Registers[1] = 0x100; delayed.Registers[2] = 7;
        delayed.Put(0x100, 11);
        delayed.Execute(0x1000, Words(0x8C220000, 0x24430001, 0x24440002), 0x100C);
        check(delayed.Registers[3] == 8 && delayed.Registers[4] == 13, "Scalar oracle executes the PSX one-instruction load delay.");
        var branch = new Machine();
        branch.Execute(0x1000, Words(0x10000002, 0x24020007, 0x24020009, 0x2403000B), 0x1010);
        check(branch.Registers[2] == 7 && branch.Registers[3] == 11, "Scalar oracle executes the taken-branch delay, skips only the intervening word.");
        reject(() => new Machine().Execute(0x1000, Words(0xFFFFFFFF), 0x1004), "Unknown scalar instruction refuses rather than inventing GTE behavior.");

        var initialization = new Machine();
        initialization.Execute(0x80012CB4, Convert.FromHexString(ClipInitializationHex), 0x80012CC8);
        check(initialization.Get(0x800785CC) == NativeClip,
            "Exact native initializer stores clip8000; fresh texture source issuance does not grant arbitrary clip authority.");

        foreach (uint count in new uint[] { 0, 1, 2, 128 })
        {
            var load = new Machine(); load.Registers[4] = Table; load.Registers[29] = 0x900000;
            load.Put(Table, 8 + count * 184); load.Put(Table + 4, count);
            load.Execute(0x80012D58, Convert.FromHexString(LoaderHex), 0x80012DA8);
            check(load.Get(0x800785C0) == Table + 8 && load.Get(0x800785C4) == Table + 8 + count * 16 &&
                  load.Get(0x800785C8) == count && load.Registers[6] == Table + 8 + count * 184,
                "Exact native loader binds separate LQ/HQ tables and advances the complete184*N component.");
            load.Put(0x800785CC, NativeClip);
            load.Execute(0x800267A8, Convert.FromHexString(BaseHex), 0x800267B8);
            // The final LW's delay belongs to the following native instruction;
            // execute its actual LUI in the same window before testing s3.
            var baseLoad = new Machine(); baseLoad.Put(0x800785CC, NativeClip); baseLoad.Put(0x800785C0, Table + 8);
            baseLoad.Execute(0x800267A8, Convert.FromHexString(BaseHex).Concat(Words(0x3C038007)).ToArray(), 0x800267BC);
            check(baseLoad.Registers[19] == Table + 8 && baseLoad.Registers[21] == NativeClip,
                "Renderer actually loads LQbase and clip through native source structure offsets, with load delay.");
        }

        var direct = Choice(0x80);
        check(direct.LastPc == 0x80027310 && direct.Writes.All(a => a < 0x400000 || a >= 0x500100),
            "Actual source flag80 reaches direct triangle path without writing normal/close queues.");
        var ordinary = Choice(0);
        check(ordinary.LastPc == 0x80026B74 && ordinary.Writes.Contains(0x400000),
            "The no80 control actually enqueues a normal face; bypass is not merely a comment.");
        byte[] raw = Convert.FromHexString(StaticSourceCases.DarkRecord);
        foreach (uint id in new uint[] { 0, 1, 127 })
        {
            foreach (uint fade in Enumerable.Range(0, 16).Select(i => (uint)i))
            {
                var cpu = Emit(id, 128 + fade * 8 + 1, raw);
                uint first = BinaryPrimitives.ReadUInt32LittleEndian(raw);
                uint second = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4));
                uint expected = unchecked(first + (fade << 22));
                check(cpu.LastPc == 0x80026B74 && cpu.Registers[30] == Packet + 0x28 &&
                      cpu.Get(Packet) == 0x09000000 && cpu.GetByte(Packet + 7) == 0x34,
                    "Nonnegative recordID emits native GT3 opcode34, nine GPU words and40-byte packet with jump delay.");
                check(cpu.Get(Packet + 0xC) == expected && cpu.Get(Packet + 0x18) == second &&
                      cpu.Get(Packet + 0x24) == unchecked(expected + 0x1F00),
                    "Native orientation0 UV corners and CLUT fade preserve actual descriptor arithmetic.");
                check(cpu.Reads.Contains(Table + id * 16) && cpu.Reads.Contains(Table + id * 16 + 4),
                    "Native id*16 selects the actual LQ table, not HQ or an editor atlas cell.");
            }
            var near = Emit(id, 0, raw);
            check(near.Reads.Contains(Table + id * 16 + 8) && near.Reads.Contains(Table + id * 16 + 12),
                "Nonpositive fade branch selects the second native LQ descriptor, after resetting its delayed shift result.");
        }
        var untextured = Emit(0xFF, 0, raw);
        check(untextured.GetByte(Packet + 7) == 0x30 && untextured.Registers[30] == Packet + 0x1C &&
              untextured.Get(Packet) == 0x06000000 && !untextured.Reads.Any(a => a >= Table && a < Table + 2048),
            "Old FF untextured packet, byte budget and absent texture dereference stay distinct.");
        // The earlier universal-clip claim was false. Keep both counterexamples:
        // native rounding reaches row16 although depthSum is inside those clips.
        // This is a fixture-only hostile input, not an accepted authored setting.
        foreach (var (clip, sum) in new[] { (1u, 0u), (129u, 128u) })
        {
            var outside = Emit(0, sum >> 7, raw, clip);
            uint first = BinaryPrimitives.ReadUInt32LittleEndian(raw);
            check(sum < clip && outside.Get(Packet + 0xC) == unchecked(first + (16u << 22)),
                "Actual native packet arithmetic disproves arbitrary-clip safety: clip1/129 can select missing palette row16.");
        }
        bool bounded = true;
        for (int sum = 0; sum < NativeClip; sum++)
        {
            int phase = ((sum >> 7) << 5) - (int)(NativeClip >> 2) + 0x1000;
            int row = phase > 0 ? phase >> 8 : 0;
            bounded &= row is >= 0 and <= 15;
        }
        check(bounded, "Only source-default clip8000 with visible fixed80 depth is proven within the sixteen source distance palettes.");
    }

    private static Machine Choice(uint flags)
    {
        var cpu = new Machine();
        cpu.Registers[1] = cpu.Registers[2] = cpu.Registers[3] = 512;
        cpu.Registers[21] = NativeClip; cpu.Registers[13] = flags;
        cpu.Registers[28] = 0x400000; cpu.Registers[29] = 0x500000;
        cpu.Registers[17] = Face + 16; cpu.Registers[22] = State + 1;
        cpu.Execute(ChoicePc, Convert.FromHexString(ChoiceHex), 0x80027310, 0x80026B74);
        return cpu;
    }
    private static Machine Emit(uint material, uint depthQuotient, byte[] record, uint clip = NativeClip)
    {
        var cpu = new Machine();
        cpu.Registers[10] = depthQuotient; cpu.Registers[13] = 0x80; cpu.Registers[21] = clip;
        cpu.Registers[19] = Table; cpu.Registers[17] = Face + 16; cpu.Registers[22] = State + 1;
        cpu.Registers[20] = Ordering; cpu.Registers[30] = Packet;
        cpu.Put(Face + 8, material); cpu.Put(Ordering, 0);
        foreach (int offset in new[] { 4, 8, 0x10, 0x14, 0x1C, 0x20 }) cpu.Put(Packet + (uint)offset, 0x00102030);
        cpu.Registers[5] = 0x708050;
        cpu.Execute(0x80027334, Convert.FromHexString(FadePreparationHex), 0x80027344);
        if (cpu.Registers[13] != clip >> 2) throw new InvalidDataException("Native flag80 delay did not prepare clip>>2.");
        cpu.Registers[5] = 0x708050;
        // Exact source274A4 stamps GT3 before the FF override decision.
        cpu.Execute(0x800274A4, Words(0x3C013400, 0x00A12825, 0xAFC50004), 0x800274B0);
        for (int i = 0; i < 16; i++) cpu.Memory[Table + (material & 127) * 16 + (uint)i] = record[i];
        cpu.Execute(PacketPc, Convert.FromHexString(PacketHex), 0x80026B74);
        return cpu;
    }

    internal static void VerifyOriginal(string path, Action<bool, string> check)
    {
        using var image = File.OpenRead(path);
        void Identity()
        {
            check(image.Length == 661547040, "Exact source image length."); image.Position = 0;
            check(Convert.ToHexStringLower(SHA256.HashData(image)) == "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37" &&
                  image.Position == 661547040 && image.Length == 661547040, "Exact source identity and final stream position/length.");
        }
        Identity();
        foreach (var (pc, expected) in new[] { (0x80012CB4u, Convert.FromHexString(ClipInitializationHex)),
            (0x80012D58u, Convert.FromHexString(LoaderHex)),
            (0x800267A8u, Convert.FromHexString(BaseHex).Concat(Words(0x3C038007)).ToArray()),
            (0x80027334u, Convert.FromHexString(FadePreparationHex)), (ChoicePc, Convert.FromHexString(ChoiceHex)),
            (PacketPc, Convert.FromHexString(PacketHex)), (0x800274A4u, Words(0x3C013400, 0x00A12825, 0xAFC50004)) })
        {
            byte[] actual = new byte[expected.Length];
            for (int i = 0; i < actual.Length;)
            {
                long file = pc - 0x8000F800L + i;
                int n = Math.Min(actual.Length - i, 2048 - (int)(file % 2048));
                image.Position = (53875 + file / 2048) * 2352 + 24 + file % 2048;
                image.ReadExactly(actual.AsSpan(i, n)); i += n;
            }
            check(actual.SequenceEqual(expected), "Scalar fixture is exactly the original native code window, not a rewritten equation.");
        }
        Identity();
    }
    private static byte[] Words(params uint[] words)
    {
        byte[] result = new byte[words.Length * 4];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(i * 4), words[i]);
        return result;
    }

    // Bounded scalar fixture, not a PSX emulator: ADD/SUB wrap intentionally;
    // no overflow exception, COP2/GTE, GPU rasterization or real RAM is modeled.
    private sealed class Machine
    {
        internal uint[] Registers { get; } = new uint[32];
        internal Dictionary<uint, byte> Memory { get; } = [];
        internal HashSet<uint> Reads { get; } = [];
        internal HashSet<uint> Writes { get; } = [];
        internal uint LastPc { get; private set; }
        internal void Put(uint at, uint value, int width = 4)
        { for (int i = 0; i < width; i++) { Memory[at + (uint)i] = (byte)(value >> (8 * i)); Writes.Add(at + (uint)i); } }
        internal byte GetByte(uint at) => Memory.TryGetValue(at, out byte value) ? value : throw new InvalidDataException($"Unowned scalar read {at:X8}");
        internal uint Get(uint at, int width = 4)
        { uint result = 0; for (int i = 0; i < width; i++) { result |= (uint)GetByte(at + (uint)i) << (8 * i); Reads.Add(at + (uint)i); } return result; }
        internal void Execute(uint start, byte[] code, params uint[] exits)
        {
            uint pc = start; uint? delayedBranch = null; (int Register, uint Value)? delayedLoad = null;
            for (int step = 0; step < 1024; step++)
            {
                if (exits.Contains(pc)) { LastPc = pc; return; }
                if (pc < start || pc - start > code.Length - 4 || (pc & 3) != 0)
                    throw new InvalidDataException($"Scalar PC escaped exact source window: {pc:X8}");
                uint word = BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan((int)(pc - start)));
                int op = (int)(word >> 26), rs = (int)(word >> 21) & 31, rt = (int)(word >> 16) & 31;
                int rd = (int)(word >> 11) & 31, shift = (int)(word >> 6) & 31;
                uint imm = word & 65535; int signed = (short)imm;
                uint? branch = null; (int Register, uint Value)? load = null; int written = -1;
                void Set(int r, uint value) { if (r != 0) Registers[r] = value; written = r; }
                void Branch(bool taken) { if (taken) branch = unchecked(pc + 4 + (uint)(signed * 4)); }
                uint Address() => unchecked(Registers[rs] + (uint)signed);
                switch (op)
                {
                    case 0:
                        switch (word & 63)
                        {
                            case 0: Set(rd, Registers[rt] << shift); break;
                            case 2: Set(rd, Registers[rt] >> shift); break;
                            case 3: Set(rd, (uint)((int)Registers[rt] >> shift)); break;
                            case 32: case 33: Set(rd, unchecked(Registers[rs] + Registers[rt])); break;
                            case 34: case 35: Set(rd, unchecked(Registers[rs] - Registers[rt])); break;
                            case 37: Set(rd, Registers[rs] | Registers[rt]); break;
                            default: throw new InvalidDataException("Unsupported R-type source instruction.");
                        } break;
                    case 1:
                        if (rt is not (0 or 1)) throw new InvalidDataException("Unsupported REGIMM.");
                        Branch(rt == 0 ? (int)Registers[rs] < 0 : (int)Registers[rs] >= 0); break;
                    case 2: branch = ((pc + 4) & 0xF0000000) | ((word & 0x3FFFFFF) << 2); break;
                    case 4: Branch(Registers[rs] == Registers[rt]); break;
                    case 6: Branch((int)Registers[rs] <= 0); break;
                    case 7: Branch((int)Registers[rs] > 0); break;
                    case 8: case 9: Set(rt, unchecked(Registers[rs] + (uint)signed)); break;
                    case 12: Set(rt, Registers[rs] & imm); break;
                    case 13: Set(rt, Registers[rs] | imm); break;
                    case 15: Set(rt, imm << 16); break;
                    case 35: load = (rt, Get(Address())); break;
                    case 40: Put(Address(), Registers[rt], 1); break;
                    case 41: Put(Address(), Registers[rt], 2); break;
                    case 43: Put(Address(), Registers[rt]); break;
                    default: throw new InvalidDataException("Unsupported source scalar opcode.");
                }
                if (delayedLoad is { } prior && prior.Register != written && prior.Register != 0) Registers[prior.Register] = prior.Value;
                if (delayedBranch != null && branch != null) throw new InvalidDataException("Branch inside branch delay is outside scalar fixture.");
                pc = delayedBranch ?? pc + 4; delayedBranch = branch; delayedLoad = load; Registers[0] = 0;
            }
            throw new InvalidDataException("Scalar fixture exceeded step budget.");
        }
    }
}

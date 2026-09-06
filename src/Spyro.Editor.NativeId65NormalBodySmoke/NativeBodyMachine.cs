using System.Buffers.Binary;

/// <summary>
/// Bounded scalar fixture only. No CD, SPU, DMA, GPU, scheduler or native CPU
/// emulation. Code and memory have explicit owners; unsupported operations fail.
/// </summary>
internal sealed class NativeBodyMachine
{
    internal const uint ReturnPc = 0x800FFFF0;
    internal uint[] Registers { get; } = new uint[32];
    private readonly Dictionary<uint, uint> _code = [];
    private readonly List<Region> _regions = [];
    internal int WordStores { get; private set; }
    internal uint LastStop { get; private set; }

    internal void Code(uint address, ReadOnlySpan<byte> bytes)
    {
        if (address % 4 != 0 || bytes.Length == 0 || bytes.Length % 4 != 0 || bytes.Length > 0x1000 ||
            _code.Count + bytes.Length / 4 > 0x4000)
            throw new InvalidDataException("Unbounded scalar code owner.");
        for (int i = 0; i < bytes.Length; i += 4)
            _code.Add(checked(address + (uint)i), BinaryPrimitives.ReadUInt32LittleEndian(bytes[i..]));
    }

    internal void Map(uint address, ReadOnlySpan<byte> bytes, bool writable)
    {
        ulong end = (ulong)address + (uint)bytes.Length;
        if (bytes.Length <= 0 || bytes.Length > 0x100000 || end > uint.MaxValue ||
            _regions.Count >= 32 || _regions.Sum(r => (long)r.Bytes.Length) + bytes.Length > 0x200000 ||
            _regions.Any(r => address < (ulong)r.Address + (uint)r.Bytes.Length && r.Address < end))
            throw new InvalidDataException("Unbounded or aliased scalar memory owner.");
        _regions.Add(new(address, bytes.ToArray(), writable));
    }

    internal byte ReadByte(uint address)
    {
        Region region = Find(address);
        return region.Bytes[checked((int)(address - region.Address))];
    }

    internal uint ReadWord(uint address)
    {
        if (address % 4 != 0)
            throw new InvalidDataException("Unaligned scalar word read.");
        uint value = 0;
        for (int i = 0; i < 4; i++)
            value |= (uint)ReadByte(checked(address + (uint)i)) << (8 * i);
        return value;
    }

    internal void SeedWord(uint address, uint value) => WriteWord(address, value);

    private Region Find(uint address) => _regions.FirstOrDefault(r => address >= r.Address &&
        (ulong)address < (ulong)r.Address + (uint)r.Bytes.Length)
        ?? throw new InvalidDataException($"Scalar read/write has no owner at {address:X8}.");

    private void WriteByte(uint address, byte value)
    {
        Region region = Find(address);
        if (!region.Writable)
            throw new InvalidDataException($"Scalar write crossed protected owner at {address:X8}.");
        region.Bytes[checked((int)(address - region.Address))] = value;
    }

    private void WriteWord(uint address, uint value)
    {
        if (address % 4 != 0)
            throw new InvalidDataException("Unaligned scalar word write.");
        for (int i = 0; i < 4; i++)
            WriteByte(checked(address + (uint)i), (byte)(value >> (8 * i)));
        WordStores++;
    }

    internal void Execute(uint pc, uint stop, uint alternativeStop = 0)
    {
        uint? delayedBranch = null;
        (int Register, uint Value)? delayedLoad = null;
        for (int steps = 0; steps < 100000; steps++)
        {
            if (pc == stop || (alternativeStop != 0 && pc == alternativeStop))
            {
                if (delayedBranch != null || delayedLoad != null)
                    throw new InvalidDataException("Scalar stop has an unretired delay.");
                LastStop = pc;
                return;
            }
            if (!_code.TryGetValue(pc, out uint word))
                throw new InvalidDataException($"Unbound scalar instruction {pc:X8}.");
            int rs = (int)(word >> 21) & 31;
            int rt = (int)(word >> 16) & 31;
            int rd = (int)(word >> 11) & 31;
            int shift = (int)(word >> 6) & 31;
            short immediate = (short)word;
            uint address = unchecked(Registers[rs] + (uint)immediate);
            uint? nextBranch = null;
            (int Register, uint Value)? nextLoad = null;
            (int Register, uint Value)? write = null;
            switch (word >> 26)
            {
                case 0:
                    switch (word & 63)
                    {
                        case 0: write = (rd, Registers[rt] << shift); break;
                        case 2: write = (rd, Registers[rt] >> shift); break;
                        case 8: nextBranch = Registers[rs]; break;
                        case 9:
                            nextBranch = Registers[rs];
                            write = (rd, pc + 8);
                            break;
                        case 0x20:
                            long sum = (long)(int)Registers[rs] + (int)Registers[rt];
                            if (sum < int.MinValue || sum > int.MaxValue)
                                throw new InvalidDataException("Unsupported native signed ADD overflow.");
                            write = (rd, unchecked((uint)sum));
                            break;
                        case 0x21: write = (rd, unchecked(Registers[rs] + Registers[rt])); break;
                        case 0x23: write = (rd, unchecked(Registers[rs] - Registers[rt])); break;
                        case 0x2B: write = (rd, Registers[rs] < Registers[rt] ? 1u : 0u); break;
                        default: throw new InvalidDataException("Unsupported scalar SPECIAL instruction.");
                    }
                    break;
                case 1:
                    if (rt is not (0 or 1))
                        throw new InvalidDataException("Unsupported scalar REGIMM instruction.");
                    nextBranch = ((int)Registers[rs] < 0) == (rt == 0)
                        ? unchecked(pc + 4 + (uint)(immediate * 4)) : pc + 8;
                    break;
                case 2:
                    nextBranch = ((pc + 4) & 0xF0000000) | ((word & 0x3FFFFFF) << 2);
                    break;
                case 3:
                    nextBranch = ((pc + 4) & 0xF0000000) | ((word & 0x3FFFFFF) << 2);
                    write = (31, pc + 8);
                    break;
                case 4:
                    nextBranch = Registers[rs] == Registers[rt]
                        ? unchecked(pc + 4 + (uint)(immediate * 4)) : pc + 8;
                    break;
                case 5:
                    nextBranch = Registers[rs] != Registers[rt]
                        ? unchecked(pc + 4 + (uint)(immediate * 4)) : pc + 8;
                    break;
                case 8:
                    long immediateSum = (long)(int)Registers[rs] + immediate;
                    if (immediateSum < int.MinValue || immediateSum > int.MaxValue)
                        throw new InvalidDataException("Unsupported native signed ADDI overflow.");
                    write = (rt, unchecked((uint)immediateSum));
                    break;
                case 9: write = (rt, address); break;
                case 10: write = (rt, (int)Registers[rs] < immediate ? 1u : 0u); break;
                case 11: write = (rt, Registers[rs] < unchecked((uint)immediate) ? 1u : 0u); break;
                case 13: write = (rt, Registers[rs] | (ushort)immediate); break;
                case 15: write = (rt, (uint)(ushort)immediate << 16); break;
                case 35: nextLoad = (rt, ReadWord(address)); break;
                case 40: WriteByte(address, (byte)Registers[rt]); break;
                case 43: WriteWord(address, Registers[rt]); break;
                default: throw new InvalidDataException("Unsupported scalar instruction.");
            }
            // Following-instruction reads saw the old register above. Its direct
            // write wins a same-register load retirement; otherwise retire now.
            if (delayedLoad is { } load && load.Register != 0 && write?.Register != load.Register)
                Registers[load.Register] = load.Value;
            if (write is { } changed && changed.Register != 0)
                Registers[changed.Register] = changed.Value;
            Registers[0] = 0;
            if (delayedBranch != null && nextBranch != null)
                throw new InvalidDataException("Branch in scalar delay slot is unsupported.");
            pc = delayedBranch ?? pc + 4;
            delayedBranch = nextBranch;
            delayedLoad = nextLoad;
        }
        throw new InvalidDataException("Bounded scalar instruction budget exhausted.");
    }

    internal static byte[] Words(params uint[] words)
    {
        byte[] bytes = new byte[words.Length * 4];
        for (int i = 0; i < words.Length; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), words[i]);
        return bytes;
    }

    internal static void SelfTest(Action<bool, string> check, Action<Action, string> reject)
    {
        var machine = new NativeBodyMachine();
        machine.Code(0x80000000, Words(0x8C220000, 0x24430000, 0x24440000, 0x10000001,
            0x8C250000, 0x24A60000, 0x24A70000, 0x03E00008, 0));
        machine.Map(0x1000, Words(0x12345678), false);
        machine.Registers[1] = 0x1000;
        machine.Registers[2] = 0xAAAA;
        machine.Registers[5] = 0xBBBB;
        machine.Registers[31] = ReturnPc;
        machine.Execute(0x80000000, ReturnPc);
        check(machine.Registers[3] == 0xAAAA && machine.Registers[4] == 0x12345678 &&
            machine.Registers[6] == 0xBBBB && machine.Registers[7] == 0x12345678,
            "scalar oracle preserves load delay and a load in taken branch delay slot");
        reject(() => machine.ReadWord(0x1004), "unmapped scalar read refuses");
        reject(() => machine.SeedWord(0x1000, 0), "protected scalar write refuses");
        var incomplete = new NativeBodyMachine();
        incomplete.Code(0x80000000, Words(0x10000001, 0, 0));
        reject(() => incomplete.Execute(0x80000000, 0x80000004), "branch delay cannot be skipped at a chosen stop");
        var call = new NativeBodyMachine();
        call.Code(0x80000000, Words(0x0C000400, 0x27E40000));
        call.Execute(0x80000000, 0x80001000);
        check(call.Registers[31] == 0x80000008 && call.Registers[4] == 0x80000008,
            "JAL return PC is visible to the actual following delay-slot instruction");
    }

    private sealed record Region(uint Address, byte[] Bytes, bool Writable);
}

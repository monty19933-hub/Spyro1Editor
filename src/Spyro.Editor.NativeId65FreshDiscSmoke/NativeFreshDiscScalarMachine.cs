using System.Buffers.Binary;

// Test-only bounded integer slices, not a CPU, GPU, helper-body or runtime emulator.
// Every instruction fetch and data access needs an explicit fixture owner. A stop
// cannot discard either R3000 delay; a later slice is a new declared entry witness.
internal sealed class NativeFreshDiscScalarMachine(
    byte[] executable, IReadOnlyList<(uint Start, int Length)> codeOwners)
{
    internal const uint ExecutableBase = 0x8000F800;
    private sealed record Region(uint Base, byte[] Bytes, bool Writable);
    private readonly List<Region> regions = [];
    internal uint[] R { get; } = new uint[32];
    internal uint Hi { get; private set; }
    internal List<uint> Trace { get; } = [];
    internal List<(uint Address, int Width)> Reads { get; } = [];
    internal List<(uint Address, int Width, uint Value)> Writes { get; } = [];

    internal void Map(uint address, ReadOnlySpan<byte> bytes, bool writable = false)
    {
        int length = bytes.Length;
        if (regions.Count >= 32 || length is <= 0 or > 4096 ||
            (ulong)address + (uint)length > 0x1_0000_0000UL ||
            regions.Any(r => (ulong)address < (ulong)r.Base + (uint)r.Bytes.Length &&
                             (ulong)r.Base < (ulong)address + (uint)length))
            throw new InvalidDataException("Invalid, overlapping or over-budget scalar data owner.");
        regions.Add(new(address, bytes.ToArray(), writable));
    }

    internal void Word(uint address, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        Map(address, bytes, true);
    }

    // Fixture initialization is not counted as a native store.
    internal void SeedWord(uint address, uint value)
    {
        var region = Owner(address, 4);
        if (!region.Writable) throw new InvalidDataException("Protected scalar seed.");
        BinaryPrimitives.WriteUInt32LittleEndian(region.Bytes.AsSpan(checked((int)(address - region.Base)), 4), value);
    }

    internal uint Read(uint address, int width = 4)
    {
        var region = Owner(address, width);
        var bytes = region.Bytes.AsSpan(checked((int)(address - region.Base)), width);
        Reads.Add((address, width));
        return width switch
        {
            1 => bytes[0],
            2 => BinaryPrimitives.ReadUInt16LittleEndian(bytes),
            4 => BinaryPrimitives.ReadUInt32LittleEndian(bytes),
            _ => throw new InvalidDataException("Unsupported scalar width.")
        };
    }

    private Region Owner(uint address, int width)
    {
        if (width is not (1 or 2 or 4) || address % width != 0)
            throw new InvalidDataException("Misaligned or unsupported scalar access.");
        return regions.FirstOrDefault(r => address >= r.Base &&
                (ulong)address + (uint)width <= (ulong)r.Base + (uint)r.Bytes.Length)
            ?? throw new InvalidDataException($"Unowned scalar access {address:X8}/{width}.");
    }

    private void Write(uint address, int width, uint value)
    {
        var region = Owner(address, width);
        if (!region.Writable) throw new InvalidDataException("Protected scalar write.");
        var bytes = region.Bytes.AsSpan(checked((int)(address - region.Base)), width);
        if (width == 1) bytes[0] = unchecked((byte)value);
        else if (width == 4) BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        else throw new InvalidDataException("Unsupported scalar store width.");
        Writes.Add((address, width, value));
    }

    internal uint Run(uint pc, params uint[] stops) => RunBounded(pc, stops, 4096);

    internal uint RunBounded(uint pc, IReadOnlyList<uint> stops, int maximum)
    {
        if (maximum is <= 0 or > 4096 || stops.Count is <= 0 or > 16)
            throw new InvalidDataException("Invalid scalar execution budget.");
        uint? branch = null;
        (int Register, uint Value)? load = null;
        for (var step = 0; step < maximum; step++)
        {
            if (stops.Contains(pc))
            {
                if (branch is not null || load is not null)
                    throw new InvalidDataException("Pending delay at scalar stop.");
                return pc;
            }
            if (pc % 4 != 0 || pc < ExecutableBase + 0x800 ||
                (ulong)pc + 4 > (ulong)ExecutableBase + (uint)executable.Length ||
                !codeOwners.Any(p => p.Start <= pc && (ulong)pc + 4 <= (ulong)p.Start + (uint)p.Length))
                throw new InvalidDataException($"Unbound scalar instruction {pc:X8}.");
            var instruction = BinaryPrimitives.ReadUInt32LittleEndian(
                executable.AsSpan(checked((int)(pc - ExecutableBase)), 4));
            Trace.Add(pc);
            int op = (int)(instruction >> 26), rs = (int)(instruction >> 21) & 31,
                rt = (int)(instruction >> 16) & 31, rd = (int)(instruction >> 11) & 31,
                shift = (int)(instruction >> 6) & 31, fn = (int)instruction & 63;
            int immediate = unchecked((short)instruction);
            // Capture operands BEFORE retiring the previous instruction's load.
            uint a = R[rs], b = R[rt];
            var oldBranch = branch;
            var oldLoad = load;
            branch = null;
            load = null;
            (int Register, uint Value)? write = null;
            unchecked
            {
                if (op == 0)
                {
                    switch (fn)
                    {
                        case 0: write = (rd, b << shift); break;
                        case 2: write = (rd, b >> shift); break;
                        case 3: write = (rd, (uint)((int)b >> shift)); break;
                        case 8: branch = a; break;
                        case 0x10: write = (rd, Hi); break;
                        case 0x18: Hi = (uint)(((long)(int)a * (int)b) >> 32); break;
                        case 0x21: write = (rd, a + b); break;
                        case 0x23: write = (rd, a - b); break;
                        case 0x2A: write = (rd, (int)a < (int)b ? 1u : 0u); break;
                        default: throw Unsupported(pc, instruction);
                    }
                }
                else switch (op)
                {
                    case 2:
                    case 3:
                        branch = ((pc + 4) & 0xF0000000) | ((instruction & 0x3FFFFFF) << 2);
                        if (op == 3) write = (31, pc + 8);
                        break;
                    case 4:
                    case 5:
                        branch = ((a == b) == (op == 4)) ? pc + 4 + (uint)(4 * immediate) : pc + 8;
                        break;
                    case 6:
                    case 7:
                        branch = (((int)a <= 0) == (op == 6)) ? pc + 4 + (uint)(4 * immediate) : pc + 8;
                        break;
                    case 9: write = (rt, a + (uint)immediate); break;
                    case 10: write = (rt, (int)a < immediate ? 1u : 0u); break;
                    case 11: write = (rt, a < (uint)immediate ? 1u : 0u); break;
                    case 12: write = (rt, a & (instruction & 65535)); break;
                    case 13: write = (rt, a | (instruction & 65535)); break;
                    case 15: write = (rt, instruction << 16); break;
                    case 33:
                    case 35:
                    case 36:
                        uint value = Read(a + (uint)immediate, op == 33 ? 2 : op == 35 ? 4 : 1);
                        if (op == 33) value = (uint)(short)value;
                        load = (rt, value);
                        break;
                    case 40:
                    case 43:
                        Write(a + (uint)immediate, op == 40 ? 1 : 4, b);
                        break;
                    default: throw Unsupported(pc, instruction);
                }
                if (oldBranch is not null && branch is not null)
                    throw new InvalidDataException("Branch in scalar delay slot.");
                if (oldLoad is { } previous && previous.Register != 0 &&
                    (write is null || write.Value.Register != previous.Register))
                    R[previous.Register] = previous.Value;
                if (write is { Register: not 0 } current) R[current.Register] = current.Value;
                R[0] = 0;
                pc = oldBranch ?? pc + 4;
            }
        }
        throw new InvalidDataException("Finite scalar instruction budget exhausted.");
    }

    private static InvalidDataException Unsupported(uint pc, uint instruction) =>
        new($"Unsupported scalar instruction {instruction:X8} at {pc:X8}.");
}

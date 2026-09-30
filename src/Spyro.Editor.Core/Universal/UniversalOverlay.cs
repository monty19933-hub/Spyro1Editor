using System.Buffers.Binary;

namespace Spyro.Editor.Core.Universal;

/// <summary>One EXE per-level binding: a LUI/ADDIU pair that stores an address into a global.</summary>
public sealed record UniversalExeBinding(uint Global, uint LuiAddress, uint AddiuAddress, uint Value);

/// <summary>Reads the EXE's per-level initializer (jump table 0x800113A4) that binds overlay callbacks.</summary>
public static class UniversalExeBindings
{
    public const uint ExeBase = 0x8000F800;
    public const uint LevelInitJumpTable = 0x800113A4;
    public const uint UpdateGlobal = 0x80075734;
    public const uint InitGlobal = 0x800758CC;
    public const uint ParticleSpawnGlobal = 0x800758E4;
    public const uint ParticleUpdateGlobal = 0x800756BC;
    public const uint CopyBufferGlobal = 0x800785D8;
    private const uint CommonTailWithV0 = 0x8005B6D8, CommonTail = 0x8005B6E0;

    public static uint Word(ReadOnlySpan<byte> exe, uint address) =>
        BinaryPrimitives.ReadUInt32LittleEndian(exe[(int)(address - ExeBase)..]);

    public static IReadOnlyDictionary<uint, UniversalExeBinding> Read(ReadOnlySpan<byte> exe, int levelId)
    {
        uint a = Word(exe, LevelInitJumpTable + (uint)levelId * 4);
        Dictionary<uint, UniversalExeBinding> result = [];
        var regs = new (uint Lui, uint Addiu, bool Pair)[32];
        for (int i = 0; i < 96; i++)
        {
            uint w = Word(exe, a);
            int op = UniversalMips.Op(w), rs = UniversalMips.Rs(w), rt = UniversalMips.Rt(w);
            if (op == 15) regs[rt] = (a, 0, false);
            else if (op == 9 && rs == rt && regs[rt].Lui != 0 && !regs[rt].Pair) regs[rt] = (regs[rt].Lui, a, true);
            else if (op == 43 && regs[rt].Pair && regs[rs].Lui != 0 && !regs[rs].Pair)
            {
                uint global = (uint)((UniversalMips.Imm(Word(exe, regs[rs].Lui)) << 16) + UniversalMips.SImm(w));
                result[global] = Make(exe, global, regs[rt].Lui, regs[rt].Addiu);
            }
            else if (op == 2)
            {
                uint target = UniversalMips.BranchTarget(w, a);
                if (target is CommonTailWithV0 or CommonTail)
                {
                    if (target == CommonTailWithV0 && regs[2].Pair)
                        result[ParticleUpdateGlobal] = Make(exe, ParticleUpdateGlobal, regs[2].Lui, regs[2].Addiu);
                    break;
                }
                a = target - 4;
            }
            a += 4;
        }
        return result;
    }

    private static UniversalExeBinding Make(ReadOnlySpan<byte> exe, uint global, uint lui, uint addiu)
    {
        uint value = (uint)((UniversalMips.Imm(Word(exe, lui)) << 16) + UniversalMips.SImm(Word(exe, addiu)));
        return new UniversalExeBinding(global, lui, addiu, value);
    }

    public static void Patch(byte[] exe, UniversalExeBinding binding, uint value)
    {
        int o1 = (int)(binding.LuiAddress - ExeBase), o2 = (int)(binding.AddiuAddress - ExeBase);
        uint w1 = BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(o1));
        uint w2 = BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(o2));
        uint current = (uint)((UniversalMips.Imm(w1) << 16) + UniversalMips.SImm(w2));
        if (current != binding.Value)
            throw new InvalidDataException($"EXE binding for 0x{binding.Global:X8} no longer holds its captured value.");
        BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(o1), UniversalMips.WithImm(w1, UniversalMips.Hi(value)));
        BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(o2), UniversalMips.WithImm(w2, UniversalMips.Lo(value)));
    }
}

/// <summary>A level overlay image loaded at 0x8007AA38 plus its EXE-bound callbacks.</summary>
public sealed class UniversalOverlay
{
    public const uint Base = 0x8007AA38;
    private readonly byte[] _bytes;
    private readonly uint[] _starts;

    public UniversalOverlay(byte[] bytes, IReadOnlyDictionary<uint, UniversalExeBinding> bindings)
    {
        _bytes = bytes;
        Bindings = bindings;
        Update = Required(UniversalExeBindings.UpdateGlobal);
        Init = Required(UniversalExeBindings.InitGlobal);
        CopyBuffer = Required(UniversalExeBindings.CopyBufferGlobal);
        SortedSet<uint> starts = [];
        foreach (var b in bindings.Values) if (Contains(b.Value)) starts.Add(b.Value);
        for (int o = 0; o + 4 <= bytes.Length; o += 4)
        {
            uint w = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(o));
            if (UniversalMips.IsJal(w))
            {
                uint t = UniversalMips.BranchTarget(w, Base + (uint)o);
                if (Contains(t)) starts.Add(t);
            }
        }
        _starts = starts.ToArray();
        CodeStart = _starts.Length > 0 ? _starts[0] : End;
    }

    public IReadOnlyDictionary<uint, UniversalExeBinding> Bindings { get; }
    public uint Update { get; }
    public uint Init { get; }
    public uint CopyBuffer { get; }
    public uint CodeStart { get; }
    public uint End => Base + (uint)_bytes.Length;
    public ReadOnlySpan<byte> Bytes => _bytes;
    public bool Contains(uint a) => a >= Base && a < End;

    public uint Word(uint a)
    {
        if (!Contains(a) || a + 4 > End) throw new InvalidDataException($"Overlay read 0x{a:X8} is outside the overlay.");
        return BinaryPrimitives.ReadUInt32LittleEndian(_bytes.AsSpan((int)(a - Base)));
    }

    public uint FunctionOf(uint a)
    {
        int i = Array.BinarySearch(_starts, a);
        if (i >= 0) return _starts[i];
        i = ~i - 1;
        if (i < 0) throw new InvalidDataException($"0x{a:X8} precedes every known overlay function.");
        return _starts[i];
    }

    public uint FunctionEnd(uint f)
    {
        int i = Array.BinarySearch(_starts, f);
        if (i < 0) throw new InvalidDataException($"0x{f:X8} is not a known overlay function start.");
        return i + 1 < _starts.Length ? _starts[i + 1] : End;
    }

    private uint Required(uint global) => Bindings.TryGetValue(global, out var b)
        ? b.Value : throw new InvalidDataException($"Level initializer does not bind global 0x{global:X8}.");
}

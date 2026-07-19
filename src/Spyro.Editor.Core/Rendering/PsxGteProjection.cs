using System.Numerics;

namespace Spyro.Editor.Core.Rendering;

[Flags]
public enum PsxGteFlag : uint
{
    None = 0,
    Ir0Saturated = 1u << 12,
    Sy2Saturated = 1u << 13,
    Sx2Saturated = 1u << 14,
    Mac0Underflow = 1u << 15,
    Mac0Overflow = 1u << 16,
    DivideOverflow = 1u << 17,
    Sz3OrOtzSaturated = 1u << 18,
    ColorBSaturated = 1u << 19,
    ColorGSaturated = 1u << 20,
    ColorRSaturated = 1u << 21,
    Ir3Saturated = 1u << 22,
    Ir2Saturated = 1u << 23,
    Ir1Saturated = 1u << 24,
    Mac3Underflow = 1u << 25,
    Mac2Underflow = 1u << 26,
    Mac1Underflow = 1u << 27,
    Mac3Overflow = 1u << 28,
    Mac2Overflow = 1u << 29,
    Mac1Overflow = 1u << 30,
    Error = 1u << 31
}

public readonly record struct PsxGteVector(short X, short Y, short Z);

public readonly record struct PsxGteScreenPoint(short X, short Y)
{
    public uint Packed => unchecked((ushort)X | ((uint)(ushort)Y << 16));

    public static PsxGteScreenPoint FromPacked(uint value) =>
        new(unchecked((short)value), unchecked((short)(value >> 16)));
}

public readonly record struct PsxGteRotationMatrix(
    short M11,
    short M12,
    short M13,
    short M21,
    short M22,
    short M23,
    short M31,
    short M32,
    short M33)
{
    public static PsxGteRotationMatrix Identity { get; } = new(
        0x1000, 0, 0,
        0, 0x1000, 0,
        0, 0, 0x1000);
}

public readonly record struct PsxGteTranslation(int X, int Y, int Z)
{
    public static PsxGteTranslation Zero { get; } = new(0, 0, 0);
}

/// <summary>
/// Explicit GTE control-register subset consumed by RTPS/RTPT. OFX and OFY
/// retain their native signed 16.16 format; matrix entries retain their signed
/// 1.3.12 format.
/// </summary>
public readonly record struct PsxGteProjectionRegisters(
    PsxGteRotationMatrix Rotation,
    PsxGteTranslation Translation,
    int Ofx,
    int Ofy,
    ushort H,
    short Dqa,
    int Dqb);

public readonly record struct PsxGteProjectionCommand(bool ShiftFraction, bool LimitMode)
{
    /// <summary>The sf=1, lm=0 encoding used by Spyro's retail environment RTPS instructions.</summary>
    public static PsxGteProjectionCommand SpyroEnvironment { get; } = new(
        ShiftFraction: true,
        LimitMode: false);
}

public readonly record struct PsxGteProjectionFifo(
    PsxGteScreenPoint Sxy0,
    PsxGteScreenPoint Sxy1,
    PsxGteScreenPoint Sxy2,
    ushort Sz0,
    ushort Sz1,
    ushort Sz2,
    ushort Sz3)
{
    public static PsxGteProjectionFifo Empty { get; } = new(
        default,
        default,
        default,
        0,
        0,
        0,
        0);
}

/// <summary>
/// Observable register state after a single RTPS or RTPT instruction. The
/// SXY/SZ FIFOs and FLAG are the production projection contract; MAC/IR and
/// the last perspective factor are retained so boundary tests can lock the
/// hardware's intermediate truncation and reciprocal behavior.
/// </summary>
public readonly record struct PsxGteProjectionResult(
    PsxGteProjectionFifo Fifo,
    PsxGteFlag Flag,
    int Mac0,
    int Mac1,
    int Mac2,
    int Mac3,
    short Ir0,
    short Ir1,
    short Ir2,
    short Ir3,
    uint LastPerspectiveFactor)
{
    public bool HasError => (Flag & PsxGteFlag.Error) != 0;
}

/// <summary>
/// Integer PS1 GTE RTPS/RTPT projection over explicit register inputs.
///
/// The implementation preserves the hardware's sequential signed-44-bit MAC
/// truncation, sf/lm quirks, SXY/SZ FIFO movement, UNR reciprocal division,
/// saturation flags, and FLAG bit-31 summary. It intentionally models no
/// emulator aspect-ratio or PGXP extension and has no Avalonia dependency.
/// </summary>
public static class PsxGteProjection
{
    public const string Contract = "psx-gte-rtps-rtpt-explicit-registers-v1";
    public const uint FlagErrorSummaryMask = 0x7F87E000;
    public const uint MaximumPerspectiveFactor = 0x1FFFF;

    private const long Mac0Minimum = int.MinValue;
    private const long Mac0Maximum = int.MaxValue;
    private const long Mac123Minimum = -(1L << 43);
    private const long Mac123Maximum = (1L << 43) - 1;
    private const long Mac123Mask = (1L << 44) - 1;
    private const long Mac123Sign = 1L << 43;
    private const int IrMinimum = short.MinValue;
    private const int IrMaximum = short.MaxValue;
    private const int Ir0Maximum = 0x1000;

    private static readonly byte[] UnrTable = BuildUnrTable();

    /// <summary>
    /// The 257-entry hardware reciprocal seed table, generated from its exact
    /// integer definition. The final entry is the documented clamped zero.
    /// </summary>
    public static ReadOnlySpan<byte> UnrTableEntries => UnrTable;

    public static PsxGteProjectionResult Rtps(
        in PsxGteProjectionRegisters registers,
        in PsxGteProjectionFifo initialFifo,
        PsxGteVector vertex,
        PsxGteProjectionCommand command)
    {
        var context = new ProjectionContext(registers, initialFifo, command);
        context.Project(vertex, last: true);
        return context.Finish();
    }

    public static PsxGteProjectionResult Rtpt(
        in PsxGteProjectionRegisters registers,
        in PsxGteProjectionFifo initialFifo,
        PsxGteVector vertex0,
        PsxGteVector vertex1,
        PsxGteVector vertex2,
        PsxGteProjectionCommand command)
    {
        var context = new ProjectionContext(registers, initialFifo, command);
        context.Project(vertex0, last: false);
        context.Project(vertex1, last: false);
        context.Project(vertex2, last: true);
        return context.Finish();
    }

    /// <summary>
    /// Exact unsigned UNR division used by RTPS/RTPT. A saturated 0x1FFFF
    /// result does not necessarily imply overflow; <paramref name="overflow"/>
    /// is set only by the native H &gt;= 2*SZ condition.
    /// </summary>
    public static uint DividePerspective(ushort h, ushort sz, out bool overflow)
    {
        uint numerator = h;
        uint denominator = sz;
        if ((denominator * 2u) <= numerator)
        {
            overflow = true;
            return MaximumPerspectiveFactor;
        }

        overflow = false;
        int shift = denominator == 0
            ? 16
            : BitOperations.LeadingZeroCount(denominator) - 16;
        numerator <<= shift;
        denominator <<= shift;

        uint divisor = denominator | 0x8000u;
        int tableIndex = (int)(((divisor & 0x7FFFu) + 0x40u) >> 7);
        int x = 0x101 + UnrTable[tableIndex];
        int d = (((int)divisor * -x) + 0x80) >> 8;
        uint reciprocal = (uint)(((x * (0x20000 + d)) + 0x80) >> 8);
        uint result = (uint)((((ulong)numerator * reciprocal) + 0x8000u) >> 16);
        return Math.Min(MaximumPerspectiveFactor, result);
    }

    public static PsxGteFlag ApplyErrorSummary(PsxGteFlag flag) =>
        (flag & (PsxGteFlag)FlagErrorSummaryMask) != 0
            ? flag | PsxGteFlag.Error
            : flag & ~PsxGteFlag.Error;

    private static byte[] BuildUnrTable()
    {
        var table = new byte[257];
        for (int index = 0; index < table.Length; index++)
        {
            int estimate = (((0x40000 / (index + 0x100)) + 1) / 2) - 0x101;
            table[index] = (byte)Math.Clamp(estimate, 0, byte.MaxValue);
        }
        return table;
    }

    private struct ProjectionContext
    {
        private readonly PsxGteProjectionRegisters _registers;
        private readonly int _shift;
        private readonly bool _limitMode;

        private PsxGteScreenPoint _sxy0;
        private PsxGteScreenPoint _sxy1;
        private PsxGteScreenPoint _sxy2;
        private ushort _sz0;
        private ushort _sz1;
        private ushort _sz2;
        private ushort _sz3;
        private PsxGteFlag _flag;
        private int _mac0;
        private int _mac1;
        private int _mac2;
        private int _mac3;
        private short _ir0;
        private short _ir1;
        private short _ir2;
        private short _ir3;
        private uint _lastPerspectiveFactor;

        public ProjectionContext(
            in PsxGteProjectionRegisters registers,
            in PsxGteProjectionFifo initialFifo,
            PsxGteProjectionCommand command)
        {
            _registers = registers;
            _shift = command.ShiftFraction ? 12 : 0;
            _limitMode = command.LimitMode;
            _sxy0 = initialFifo.Sxy0;
            _sxy1 = initialFifo.Sxy1;
            _sxy2 = initialFifo.Sxy2;
            _sz0 = initialFifo.Sz0;
            _sz1 = initialFifo.Sz1;
            _sz2 = initialFifo.Sz2;
            _sz3 = initialFifo.Sz3;
        }

        public void Project(PsxGteVector vertex, bool last)
        {
            PsxGteRotationMatrix matrix = _registers.Rotation;
            PsxGteTranslation translation = _registers.Translation;

            long x = TransformRow(
                translation.X,
                matrix.M11,
                matrix.M12,
                matrix.M13,
                vertex,
                1);
            long y = TransformRow(
                translation.Y,
                matrix.M21,
                matrix.M22,
                matrix.M23,
                vertex,
                2);
            long z = TransformRow(
                translation.Z,
                matrix.M31,
                matrix.M32,
                matrix.M33,
                vertex,
                3);

            _mac1 = TruncateMac(x, _shift, 1);
            _mac2 = TruncateMac(y, _shift, 2);
            _mac3 = TruncateMac(z, _shift, 3);
            _ir1 = TruncateIr(_mac1, _limitMode ? 0 : IrMinimum, IrMaximum, 1, setFlag: true);
            _ir2 = TruncateIr(_mac2, _limitMode ? 0 : IrMinimum, IrMaximum, 2, setFlag: true);

            // RTP always tests IR3 saturation from MAC3 SAR 12, independent
            // of sf/lm, then writes the separately lm-clamped MAC3 value.
            _ = TruncateIr(unchecked((int)(z >> 12)), IrMinimum, IrMaximum, 3, setFlag: true);
            _ir3 = TruncateIr(_mac3, _limitMode ? 0 : IrMinimum, IrMaximum, 3, setFlag: false);

            PushSz(unchecked((int)(z >> 12)));

            _lastPerspectiveFactor = DividePerspective(_registers.H, _sz3, out bool divideOverflow);
            if (divideOverflow)
                _flag |= PsxGteFlag.DivideOverflow;

            long sx = ((long)_lastPerspectiveFactor * _ir1) + _registers.Ofx;
            long sy = ((long)_lastPerspectiveFactor * _ir2) + _registers.Ofy;
            CheckMacOverflow(sx, 0);
            CheckMacOverflow(sy, 0);
            PushSxy(unchecked((int)(sx >> 16)), unchecked((int)(sy >> 16)));

            if (last)
            {
                long depthCue = ((long)_lastPerspectiveFactor * _registers.Dqa) + _registers.Dqb;
                _mac0 = TruncateMac(depthCue, 0, 0);
                _ir0 = TruncateIr(
                    unchecked((int)(depthCue >> 12)),
                    0,
                    Ir0Maximum,
                    0,
                    setFlag: true);
            }
        }

        public PsxGteProjectionResult Finish()
        {
            _flag = ApplyErrorSummary(_flag);
            return new PsxGteProjectionResult(
                new PsxGteProjectionFifo(
                    _sxy0,
                    _sxy1,
                    _sxy2,
                    _sz0,
                    _sz1,
                    _sz2,
                    _sz3),
                _flag,
                _mac0,
                _mac1,
                _mac2,
                _mac3,
                _ir0,
                _ir1,
                _ir2,
                _ir3,
                _lastPerspectiveFactor);
        }

        private long TransformRow(
            int translation,
            short m0,
            short m1,
            short m2,
            PsxGteVector vertex,
            int macIndex)
        {
            long value = ((long)translation << 12) + ((long)m0 * vertex.X);
            value = SignExtendMac123(value, macIndex);
            value += (long)m1 * vertex.Y;
            value = SignExtendMac123(value, macIndex);
            value += (long)m2 * vertex.Z;
            return SignExtendMac123(value, macIndex);
        }

        private long SignExtendMac123(long value, int macIndex)
        {
            CheckMacOverflow(value, macIndex);
            long truncated = value & Mac123Mask;
            return (truncated & Mac123Sign) != 0
                ? truncated | ~Mac123Mask
                : truncated;
        }

        private int TruncateMac(long value, int shift, int macIndex)
        {
            CheckMacOverflow(value, macIndex);
            return unchecked((int)(value >> shift));
        }

        private short TruncateIr(
            int value,
            int minimum,
            int maximum,
            int irIndex,
            bool setFlag)
        {
            int clamped = Math.Clamp(value, minimum, maximum);
            if (setFlag && clamped != value)
            {
                _flag |= irIndex switch
                {
                    0 => PsxGteFlag.Ir0Saturated,
                    1 => PsxGteFlag.Ir1Saturated,
                    2 => PsxGteFlag.Ir2Saturated,
                    3 => PsxGteFlag.Ir3Saturated,
                    _ => throw new ArgumentOutOfRangeException(nameof(irIndex))
                };
            }
            return checked((short)clamped);
        }

        private void PushSz(int value)
        {
            int clamped = Math.Clamp(value, 0, ushort.MaxValue);
            if (clamped != value)
                _flag |= PsxGteFlag.Sz3OrOtzSaturated;

            _sz0 = _sz1;
            _sz1 = _sz2;
            _sz2 = _sz3;
            _sz3 = (ushort)clamped;
        }

        private void PushSxy(int x, int y)
        {
            int clampedX = Math.Clamp(x, -1024, 1023);
            int clampedY = Math.Clamp(y, -1024, 1023);
            if (clampedX != x)
                _flag |= PsxGteFlag.Sx2Saturated;
            if (clampedY != y)
                _flag |= PsxGteFlag.Sy2Saturated;

            _sxy0 = _sxy1;
            _sxy1 = _sxy2;
            _sxy2 = new PsxGteScreenPoint((short)clampedX, (short)clampedY);
        }

        private void CheckMacOverflow(long value, int macIndex)
        {
            long minimum = macIndex == 0 ? Mac0Minimum : Mac123Minimum;
            long maximum = macIndex == 0 ? Mac0Maximum : Mac123Maximum;
            if (value < minimum)
            {
                _flag |= macIndex switch
                {
                    0 => PsxGteFlag.Mac0Underflow,
                    1 => PsxGteFlag.Mac1Underflow,
                    2 => PsxGteFlag.Mac2Underflow,
                    3 => PsxGteFlag.Mac3Underflow,
                    _ => throw new ArgumentOutOfRangeException(nameof(macIndex))
                };
            }
            else if (value > maximum)
            {
                _flag |= macIndex switch
                {
                    0 => PsxGteFlag.Mac0Overflow,
                    1 => PsxGteFlag.Mac1Overflow,
                    2 => PsxGteFlag.Mac2Overflow,
                    3 => PsxGteFlag.Mac3Overflow,
                    _ => throw new ArgumentOutOfRangeException(nameof(macIndex))
                };
            }
        }
    }
}

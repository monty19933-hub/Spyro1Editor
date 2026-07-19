namespace Spyro.Editor.Core.Rendering;

[Flags]
public enum NativeTerrainCpuOutcode : byte
{
    None = 0,
    Top = 1 << 0,
    Bottom = 1 << 1,
    Left = 1 << 2,
    Right = 1 << 3,
    ProjectionError = 1 << 4
}

/// <summary>
/// The low two tagged-pointer bits selected at r_environment 0x800268B4-
/// 0x800268C4. Values 2 and 3 both select FullTaggedContextBit1.
/// </summary>
public enum NativeTerrainBaseProjectionContext
{
    RawSxy = 0,
    SimpleTaggedContextBit0 = 1,
    FullTaggedContextBit1 = 2
}

public enum NativeTerrainBaseScratchEncoding
{
    RawSxy,
    PackedSimpleOutcode,
    PackedFullOutcode
}

/// <summary>
/// One source slot after the retail HP base RTPS/outcode sequence. Depth is
/// always the first RTPS SZ3. ScreenPoint and EffectiveFlag come from the
/// optional near-camera rerun when one occurred.
/// </summary>
public readonly record struct NativeTerrainBaseProjectedSlot(
    int SlotIndex,
    PsxGteVector Source,
    PsxGteProjectionResult OriginalProjection,
    PsxGteProjectionResult? NearCameraRerunProjection,
    PsxGteScreenPoint ScreenPoint,
    ushort Depth,
    PsxGteFlag EffectiveFlag,
    NativeTerrainCpuOutcode Outcode,
    NativeTerrainBaseScratchEncoding ScratchEncoding,
    uint StoredScratchWord)
{
    public bool UsedNearCameraRerun => NearCameraRerunProjection.HasValue;
}

/// <summary>
/// Raw signed NCLIP MAC0 values over four caller-proven native face slots. The
/// renderer first tests [0,1,3], then can test [2,1,3]. For a native triangle,
/// the caller must supply the real repeated fourth raw slot; this type does not
/// infer repetition from coordinate equality.
/// </summary>
public readonly record struct NativeTerrainRawFourSlotNclip(
    int FirstMac0,
    int SecondMac0);

public sealed record NativeTerrainBaseProjectionResult(
    NativeTerrainBaseProjectionContext Context,
    NativeTerrainBaseProjectedSlot[] Slots,
    PsxGteProjectionFifo FinalFifo,
    NativeTerrainCpuOutcode CommonOutsideMask,
    NativeTerrainRawFourSlotNclip? RawFourSlotNclip)
{
    public bool IsCommonOutsideRejected => CommonOutsideMask != NativeTerrainCpuOutcode.None;
}

/// <summary>
/// Standalone integer contract for the independently verifiable HP base
/// projection steps in Spyro 1's retail r_environment renderer. The explicit
/// context input prevents the tagged full path from being applied to raw or
/// simple tagged queues.
///
/// It runs one RTPS per caller-supplied native source slot, preserves command
/// FIFO order, conditionally performs the 0x80026A04 near-camera SXY rerun,
/// packs the selected CPU outcode form, computes the common-outside mask, and
/// exposes exact raw four-slot NCLIP MAC0 values when four native slots were
/// supplied.
///
/// Three-point input remains a projection/outcode set and has no claimed
/// retail face NCLIP. Exact face evaluation requires four raw slots, including
/// the caller-proven repeated fourth slot for a native triangle. This type
/// never invents repetition and does not derive GTE registers, source vectors,
/// face flags, clipping, HQ vertices, or GPU packets.
/// </summary>
public static class NativeTerrainBaseProjection
{
    public const string Contract = "native-terrain-hp-base-projection-explicit-slots-v1";

    public const ushort NearCameraDepthExclusiveMaximum = 0x100;
    public const ushort FullOutcodeDepthExclusiveMaximum = 0x600;
    public const int NearCameraMacExclusiveMagnitude = 0x100;
    public const byte CommonOutsideBits = 0x0F;

    private const int PackedTopBoundary = 0x00010000;
    private const int PackedBottomBoundary = 0x01000000;
    private const int ShiftedRightBoundary = 0x02000000;
    private const uint PackedVxyScaleMask = 0xFFF0FFF0;

    public static NativeTerrainBaseProjectionContext NormalizeRetailTaggedPointerBits(
        int lowTwoBits)
    {
        if ((lowTwoBits & ~0x3) != 0)
            throw new ArgumentOutOfRangeException(nameof(lowTwoBits), lowTwoBits, null);
        if ((lowTwoBits & 0x2) != 0)
            return NativeTerrainBaseProjectionContext.FullTaggedContextBit1;
        return lowTwoBits == 1
            ? NativeTerrainBaseProjectionContext.SimpleTaggedContextBit0
            : NativeTerrainBaseProjectionContext.RawSxy;
    }

    public static NativeTerrainBaseProjectionResult Project(
        in PsxGteProjectionRegisters registers,
        NativeTerrainBaseProjectionContext context,
        ReadOnlySpan<PsxGteVector> nativeSourceSlots)
    {
        PsxGteProjectionFifo initialFifo = PsxGteProjectionFifo.Empty;
        return Project(registers, initialFifo, context, nativeSourceSlots);
    }

    public static NativeTerrainBaseProjectionResult Project(
        in PsxGteProjectionRegisters registers,
        in PsxGteProjectionFifo initialFifo,
        NativeTerrainBaseProjectionContext context,
        ReadOnlySpan<PsxGteVector> nativeSourceSlots)
    {
        if (nativeSourceSlots.Length is not (3 or 4))
        {
            throw new ArgumentException(
                "Native terrain base projection requires exactly three projection points or four caller-proven raw face slots.",
                nameof(nativeSourceSlots));
        }
        if (context is < NativeTerrainBaseProjectionContext.RawSxy or
            > NativeTerrainBaseProjectionContext.FullTaggedContextBit1)
        {
            throw new ArgumentOutOfRangeException(nameof(context), context, null);
        }

        var projected = new NativeTerrainBaseProjectedSlot[nativeSourceSlots.Length];
        PsxGteProjectionFifo fifo = initialFifo;

        for (int slotIndex = 0; slotIndex < nativeSourceSlots.Length; slotIndex++)
        {
            PsxGteVector source = nativeSourceSlots[slotIndex];
            PsxGteProjectionResult original = PsxGteProjection.Rtps(
                registers,
                fifo,
                source,
                PsxGteProjectionCommand.SpyroEnvironment);
            fifo = original.Fifo;

            PsxGteProjectionResult? rerun = null;
            PsxGteScreenPoint screenPoint = original.Fifo.Sxy2;
            PsxGteFlag effectiveFlag = original.Flag;

            if (context == NativeTerrainBaseProjectionContext.FullTaggedContextBit1 &&
                ShouldUseNearCameraRerun(original))
            {
                PsxGteVector scaledSource = ScaleForNearCameraRerun(source);
                PsxGteProjectionResult scaled = PsxGteProjection.Rtps(
                    registers,
                    fifo,
                    scaledSource,
                    PsxGteProjectionCommand.SpyroEnvironment);
                rerun = scaled;
                fifo = scaled.Fifo;
                screenPoint = scaled.Fifo.Sxy2;
                effectiveFlag = scaled.Flag;
            }

            NativeTerrainCpuOutcode outcode;
            NativeTerrainBaseScratchEncoding scratchEncoding;
            uint storedScratchWord;
            switch (context)
            {
                case NativeTerrainBaseProjectionContext.RawSxy:
                    outcode = NativeTerrainCpuOutcode.None;
                    scratchEncoding = NativeTerrainBaseScratchEncoding.RawSxy;
                    storedScratchWord = screenPoint.Packed;
                    break;

                case NativeTerrainBaseProjectionContext.SimpleTaggedContextBit0:
                    outcode = ComputeSimpleCpuOutcode(screenPoint);
                    scratchEncoding = NativeTerrainBaseScratchEncoding.PackedSimpleOutcode;
                    storedScratchWord = PackScreenAndOutcode(screenPoint, outcode);
                    break;

                case NativeTerrainBaseProjectionContext.FullTaggedContextBit1:
                    bool usesFullOutcode = original.Fifo.Sz3 < FullOutcodeDepthExclusiveMaximum;
                    outcode = usesFullOutcode
                        ? ComputeFullCpuOutcode(screenPoint, effectiveFlag)
                        : ComputeSimpleCpuOutcode(screenPoint);
                    scratchEncoding = usesFullOutcode
                        ? NativeTerrainBaseScratchEncoding.PackedFullOutcode
                        : NativeTerrainBaseScratchEncoding.PackedSimpleOutcode;
                    storedScratchWord = PackScreenAndOutcode(screenPoint, outcode);
                    break;

                default:
                    throw new InvalidOperationException($"Unexpected projection context {context}.");
            }

            projected[slotIndex] = new NativeTerrainBaseProjectedSlot(
                slotIndex,
                source,
                original,
                rerun,
                screenPoint,
                original.Fifo.Sz3,
                effectiveFlag,
                outcode,
                scratchEncoding,
                storedScratchWord);
        }

        NativeTerrainCpuOutcode commonOutside = context == NativeTerrainBaseProjectionContext.RawSxy
            ? NativeTerrainCpuOutcode.None
            : ComputeCommonOutsideMask(projected);
        NativeTerrainRawFourSlotNclip? rawFourSlotNclip = projected.Length == 4
            ? ComputeRetailFourSlotNclip(projected)
            : null;
        return new NativeTerrainBaseProjectionResult(
            context,
            projected,
            fifo,
            commonOutside,
            rawFourSlotNclip);
    }

    /// <summary>
    /// The strict branch predicates at 0x80026A18-0x80026A54. The first RTPS
    /// depth and MAC1/MAC2 are tested before any packed-vector scaling.
    /// </summary>
    public static bool ShouldUseNearCameraRerun(in PsxGteProjectionResult original) =>
        original.Fifo.Sz3 < NearCameraDepthExclusiveMaximum &&
        original.Mac1 > -NearCameraMacExclusiveMagnitude &&
        original.Mac1 < NearCameraMacExclusiveMagnitude &&
        original.Mac2 > -NearCameraMacExclusiveMagnitude &&
        original.Mac2 < NearCameraMacExclusiveMagnitude;

    /// <summary>
    /// Reproduces mfc2/sll/and/mtc2 at 0x80026A58-0x80026A78. VXY is shifted
    /// as one packed register and masked with 0xFFF0FFF0; VZ is read as a
    /// sign-extended 16-bit register, shifted in 32-bit MIPS arithmetic, then
    /// truncated/sign-extended again by mtc2.
    /// </summary>
    public static PsxGteVector ScaleForNearCameraRerun(PsxGteVector source)
    {
        uint packedVxy = unchecked((uint)(ushort)source.X | ((uint)(ushort)source.Y << 16));
        uint shiftedVxy = unchecked(packedVxy << 4) & PackedVxyScaleMask;

        uint signExtendedVz = unchecked((uint)(int)source.Z);
        uint shiftedVz = unchecked(signExtendedVz << 4);
        return new PsxGteVector(
            unchecked((short)shiftedVxy),
            unchecked((short)(shiftedVxy >> 16)),
            unchecked((short)shiftedVz));
    }

    /// <summary>
    /// Exact signed packed-word tests from 0x80026A8C-0x80026ADC. These are
    /// intentionally expressed as unchecked 32-bit operations rather than as
    /// simplified X/Y comparisons.
    /// </summary>
    public static NativeTerrainCpuOutcode ComputeFullCpuOutcode(
        PsxGteScreenPoint screenPoint,
        PsxGteFlag flag)
    {
        int packed = unchecked((int)screenPoint.Packed);
        NativeTerrainCpuOutcode outcode = NativeTerrainCpuOutcode.None;

        if (unchecked(packed - PackedTopBoundary) <= 0)
            outcode |= NativeTerrainCpuOutcode.Top;
        if (unchecked(packed - PackedBottomBoundary) >= 0)
            outcode |= NativeTerrainCpuOutcode.Bottom;

        int shiftedX = unchecked(packed << 16);
        if (shiftedX <= 0)
            outcode |= NativeTerrainCpuOutcode.Left;
        if (unchecked(shiftedX - ShiftedRightBoundary) >= 0)
            outcode |= NativeTerrainCpuOutcode.Right;

        if (unchecked((int)(uint)flag) < 0)
            outcode |= NativeTerrainCpuOutcode.ProjectionError;

        return outcode;
    }

    /// <summary>
    /// Exact simplified tagged-path tests at 0x80026978-0x800269A0 and
    /// 0x80026B04-0x80026B2C. The X test deliberately emits bits 0x0C as a
    /// pair and does not consume FLAG.
    /// </summary>
    public static NativeTerrainCpuOutcode ComputeSimpleCpuOutcode(
        PsxGteScreenPoint screenPoint)
    {
        int packed = unchecked((int)screenPoint.Packed);
        NativeTerrainCpuOutcode outcode = NativeTerrainCpuOutcode.None;

        if (unchecked(packed - PackedTopBoundary) <= 0)
            outcode |= NativeTerrainCpuOutcode.Top;
        if (unchecked(packed - PackedBottomBoundary) >= 0)
            outcode |= NativeTerrainCpuOutcode.Bottom;
        if ((packed & 0x0000FE00) != 0)
            outcode |= NativeTerrainCpuOutcode.Left | NativeTerrainCpuOutcode.Right;

        return outcode;
    }

    public static uint PackScreenAndOutcode(
        PsxGteScreenPoint screenPoint,
        NativeTerrainCpuOutcode outcode) =>
        unchecked((screenPoint.Packed << 5) + (byte)outcode);

    public static NativeTerrainCpuOutcode ComputeCommonOutsideMask(
        ReadOnlySpan<NativeTerrainBaseProjectedSlot> slots)
    {
        if (slots.Length is not (3 or 4))
        {
            throw new ArgumentException(
                "The retail common-outside test requires three triangle slots or four quad slots.",
                nameof(slots));
        }

        uint common = uint.MaxValue;
        foreach (NativeTerrainBaseProjectedSlot slot in slots)
        {
            if (slot.ScratchEncoding == NativeTerrainBaseScratchEncoding.RawSxy)
            {
                throw new ArgumentException(
                    "Raw-SXY slots do not participate in the tagged common-outside test.",
                    nameof(slots));
            }
            common &= slot.StoredScratchWord;
        }

        return (NativeTerrainCpuOutcode)(common & CommonOutsideBits);
    }

    /// <summary>
    /// Exact NCLIP determinant/MAC0 equation. Projected GTE SXY inputs are
    /// bounded to -1024..1023, so the retail terrain use cannot overflow MAC0.
    /// </summary>
    public static int ComputeGenericThreePointNclipMac0(
        PsxGteScreenPoint sxy0,
        PsxGteScreenPoint sxy1,
        PsxGteScreenPoint sxy2)
    {
        long value =
            ((long)sxy0.X * sxy1.Y) +
            ((long)sxy1.X * sxy2.Y) +
            ((long)sxy2.X * sxy0.Y) -
            ((long)sxy0.X * sxy2.Y) -
            ((long)sxy1.X * sxy0.Y) -
            ((long)sxy2.X * sxy1.Y);
        return unchecked((int)value);
    }

    /// <summary>
    /// Evaluates the two exact retail NCLIP register orders over four
    /// caller-proven raw face slots. A native triangle must retain its repeated
    /// fourth raw slot. Three deduplicated points are intentionally rejected.
    /// </summary>
    public static NativeTerrainRawFourSlotNclip ComputeRetailFourSlotNclip(
        ReadOnlySpan<NativeTerrainBaseProjectedSlot> slots)
    {
        if (slots.Length != 4)
        {
            throw new ArgumentException(
                "Retail terrain face NCLIP requires four caller-proven raw slots, including native triangle repetition.",
                nameof(slots));
        }

        int firstMac0 = ComputeGenericThreePointNclipMac0(
            slots[0].ScreenPoint,
            slots[1].ScreenPoint,
            slots[3].ScreenPoint);
        int secondMac0 = ComputeGenericThreePointNclipMac0(
            slots[2].ScreenPoint,
            slots[1].ScreenPoint,
            slots[3].ScreenPoint);
        return new NativeTerrainRawFourSlotNclip(firstMac0, secondMac0);
    }
}

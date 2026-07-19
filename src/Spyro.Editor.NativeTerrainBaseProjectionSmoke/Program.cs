using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Rendering;

const string expectedFixtureSha256 = "1426B5934F92038E5E09383F4AAF61DC3A753233AE530CF654432D69BD426CE3";
const int expectedNearRerunCount = 661;

Assert(NativeTerrainBaseProjection.Contract ==
       "native-terrain-hp-base-projection-explicit-slots-v1",
    "Native terrain base-projection contract name changed.");
AssertSlotCountAndFifoOrder();
AssertNearCameraBoundaries();
AssertPackedOutcodeBoundaries();
AssertCommonOutsideReject();
AssertNclipAndNativeSlotOrder();

(string fixtureSha256, int nearRerunCount) = BuildFixtureSha256();
Assert(fixtureSha256 == expectedFixtureSha256,
    $"Native terrain base-projection fixture changed: {fixtureSha256}; " +
    $"near reruns {nearRerunCount}.");
Assert(nearRerunCount == expectedNearRerunCount,
    $"Native terrain near-rerun coverage changed: {nearRerunCount}.");

Console.WriteLine("Native terrain base-projection smoke passed.");
Console.WriteLine($"- Contract: {NativeTerrainBaseProjection.Contract}");
Console.WriteLine("- Inputs: explicit GTE registers plus caller-proven 3/4 native source slots");
Console.WriteLine("- Locked: sequential RTPS/FIFO, near SXY rerun, packed outcodes/common reject, raw four-slot NCLIP MAC0");
Console.WriteLine($"- 4,096 projection-set SHA-256: {fixtureSha256} ({nearRerunCount} near reruns)");

static void AssertSlotCountAndFifoOrder()
{
    Assert(NativeTerrainBaseProjection.NormalizeRetailTaggedPointerBits(0) ==
               NativeTerrainBaseProjectionContext.RawSxy &&
           NativeTerrainBaseProjection.NormalizeRetailTaggedPointerBits(1) ==
               NativeTerrainBaseProjectionContext.SimpleTaggedContextBit0 &&
           NativeTerrainBaseProjection.NormalizeRetailTaggedPointerBits(2) ==
               NativeTerrainBaseProjectionContext.FullTaggedContextBit1 &&
           NativeTerrainBaseProjection.NormalizeRetailTaggedPointerBits(3) ==
               NativeTerrainBaseProjectionContext.FullTaggedContextBit1,
        "Retail tagged-pointer low-bit context selection changed.");

    PsxGteProjectionRegisters registers = IdentityRegisters(0, 0, 64);
    PsxGteProjectionFifo initial = new(
        new PsxGteScreenPoint(-3, -4),
        new PsxGteScreenPoint(5, 6),
        new PsxGteScreenPoint(7, 8),
        11,
        12,
        13,
        14);
    PsxGteVector[] triangle =
    [
        new PsxGteVector(30, 40, 300),
        new PsxGteVector(50, 60, 400),
        new PsxGteVector(70, 80, 500)
    ];

    NativeTerrainBaseProjectionResult result =
        NativeTerrainBaseProjection.Project(
            registers,
            initial,
            NativeTerrainBaseProjectionContext.FullTaggedContextBit1,
            triangle);
    Assert(result.Slots.Length == 3 &&
           result.Slots.Select(slot => slot.Source).SequenceEqual(triangle) &&
           result.Slots.Select(slot => slot.SlotIndex).SequenceEqual([0, 1, 2]),
        "Triangle source slots were reordered or synthesized.");
    Assert(result.Slots.All(slot => !slot.UsedNearCameraRerun),
        "The non-near FIFO fixture unexpectedly reran RTPS.");
    Assert(result.FinalFifo.Sxy0 == result.Slots[0].ScreenPoint &&
           result.FinalFifo.Sxy1 == result.Slots[1].ScreenPoint &&
           result.FinalFifo.Sxy2 == result.Slots[2].ScreenPoint,
        "Three sequential RTPS commands did not leave source slots 0/1/2 in SXY0/1/2.");
    Assert(result.FinalFifo.Sz0 == initial.Sz3 &&
           result.FinalFifo.Sz1 == 300 &&
           result.FinalFifo.Sz2 == 400 &&
           result.FinalFifo.Sz3 == 500,
        "Three sequential RTPS commands changed the retail SZ FIFO order.");

    PsxGteVector[] quad =
    [
        new PsxGteVector(1, 2, 300),
        new PsxGteVector(3, 4, 400),
        new PsxGteVector(5, 6, 500),
        new PsxGteVector(7, 8, 600)
    ];
    NativeTerrainBaseProjectionResult quadResult =
        NativeTerrainBaseProjection.Project(
            registers,
            initial,
            NativeTerrainBaseProjectionContext.FullTaggedContextBit1,
            quad);
    Assert(quadResult.FinalFifo.Sxy0 == quadResult.Slots[1].ScreenPoint &&
           quadResult.FinalFifo.Sxy1 == quadResult.Slots[2].ScreenPoint &&
           quadResult.FinalFifo.Sxy2 == quadResult.Slots[3].ScreenPoint &&
           quadResult.FinalFifo.Sz0 == 300 &&
           quadResult.FinalFifo.Sz1 == 400 &&
           quadResult.FinalFifo.Sz2 == 500 &&
           quadResult.FinalFifo.Sz3 == 600,
        "Four sequential RTPS commands changed the retail SXY/SZ FIFO order.");

    AssertThrows(() => NativeTerrainBaseProjection.Project(
            registers,
            NativeTerrainBaseProjectionContext.FullTaggedContextBit1,
            new PsxGteVector[2]),
        "Two source slots were accepted as a native terrain face.");
    AssertThrows(() => NativeTerrainBaseProjection.Project(
            registers,
            NativeTerrainBaseProjectionContext.FullTaggedContextBit1,
            new PsxGteVector[5]),
        "Five source slots were accepted as a native terrain face.");

    foreach (NativeTerrainBaseProjectionContext context in Enum.GetValues<NativeTerrainBaseProjectionContext>())
    {
        NativeTerrainBaseProjectionResult modeResult =
            NativeTerrainBaseProjection.Project(registers, context, triangle);
        Assert(modeResult.Context == context && modeResult.Slots.All(slot =>
                slot.ScratchEncoding == context switch
                {
                    NativeTerrainBaseProjectionContext.RawSxy => NativeTerrainBaseScratchEncoding.RawSxy,
                    NativeTerrainBaseProjectionContext.SimpleTaggedContextBit0 => NativeTerrainBaseScratchEncoding.PackedSimpleOutcode,
                    NativeTerrainBaseProjectionContext.FullTaggedContextBit1 => NativeTerrainBaseScratchEncoding.PackedFullOutcode,
                    _ => throw new InvalidOperationException()
                }),
            $"Projection context {context} did not select its audited scratch encoding.");
    }
}

static void AssertNearCameraBoundaries()
{
    PsxGteVector scaled = NativeTerrainBaseProjection.ScaleForNearCameraRerun(
        new PsxGteVector(-1, 1, short.MaxValue));
    Assert(scaled == new PsxGteVector(-16, 16, -16),
        $"Packed near-camera shift lost MIPS/16-bit wrap semantics: {scaled}.");

    scaled = NativeTerrainBaseProjection.ScaleForNearCameraRerun(
        new PsxGteVector(unchecked((short)0x1234), unchecked((short)0xF234), 1));
    Assert(scaled == new PsxGteVector(0x2340, 0x2340, 0x10),
        $"Packed VXY shift/mask allowed cross-component carry: {scaled}.");

    PsxGteProjectionRegisters registers = IdentityRegisters(256 << 16, 120 << 16, 64);
    PsxGteVector[] lowerBoundaries =
    [
        new PsxGteVector(-256, 0, 255),
        new PsxGteVector(-255, 255, 255),
        new PsxGteVector(0, 256, 255)
    ];
    NativeTerrainBaseProjectionResult lower =
        NativeTerrainBaseProjection.Project(
            registers,
            NativeTerrainBaseProjectionContext.FullTaggedContextBit1,
            lowerBoundaries);
    Assert(!lower.Slots[0].UsedNearCameraRerun &&
           lower.Slots[1].UsedNearCameraRerun &&
           !lower.Slots[2].UsedNearCameraRerun,
        "Near-camera MAC lower/upper strict boundaries changed.");
    NativeTerrainBaseProjectionResult rawEligible =
        NativeTerrainBaseProjection.Project(
            registers,
            NativeTerrainBaseProjectionContext.RawSxy,
            lowerBoundaries);
    NativeTerrainBaseProjectionResult simpleEligible =
        NativeTerrainBaseProjection.Project(
            registers,
            NativeTerrainBaseProjectionContext.SimpleTaggedContextBit0,
            lowerBoundaries);
    Assert(rawEligible.Slots.All(slot => !slot.UsedNearCameraRerun) &&
           simpleEligible.Slots.All(slot => !slot.UsedNearCameraRerun),
        "A raw/simple tagged queue incorrectly entered the context-bit-1 near rerun.");

    PsxGteVector[] upperBoundaries =
    [
        new PsxGteVector(255, -255, 255),
        new PsxGteVector(256, 0, 255),
        new PsxGteVector(0, 0, 256)
    ];
    NativeTerrainBaseProjectionResult upper =
        NativeTerrainBaseProjection.Project(
            registers,
            NativeTerrainBaseProjectionContext.FullTaggedContextBit1,
            upperBoundaries);
    Assert(upper.Slots[0].UsedNearCameraRerun &&
           !upper.Slots[1].UsedNearCameraRerun &&
           !upper.Slots[2].UsedNearCameraRerun,
        "Near-camera MAC/depth exclusive upper boundaries changed.");
    Assert(upper.Slots[0].Depth == 255 &&
           upper.Slots[0].NearCameraRerunProjection!.Value.Fifo.Sz3 == 4080 &&
           upper.Slots[0].ScreenPoint ==
           upper.Slots[0].NearCameraRerunProjection!.Value.Fifo.Sxy2,
        "Near-camera rerun did not retain original SZ while replacing SXY.");

    PsxGteProjectionFifo initial = new(default, default, default, 10, 11, 12, 13);
    PsxGteVector[] ordered =
    [
        new PsxGteVector(1, 2, 255),
        new PsxGteVector(3, 4, 256),
        new PsxGteVector(5, 6, 300)
    ];
    NativeTerrainBaseProjectionResult orderedResult =
        NativeTerrainBaseProjection.Project(
            registers,
            initial,
            NativeTerrainBaseProjectionContext.FullTaggedContextBit1,
            ordered);
    Assert(orderedResult.Slots[0].UsedNearCameraRerun &&
           orderedResult.Slots[1].OriginalProjection.Fifo.Sxy0 ==
           orderedResult.Slots[0].OriginalProjection.Fifo.Sxy2,
        "The near rerun was not inserted into the sequential RTPS FIFO stream.");
    Assert(orderedResult.FinalFifo.Sz0 == 255 &&
           orderedResult.FinalFifo.Sz1 == 4080 &&
           orderedResult.FinalFifo.Sz2 == 256 &&
           orderedResult.FinalFifo.Sz3 == 300,
        "The extra near-camera RTPS command changed documented SZ FIFO order.");
}

static void AssertPackedOutcodeBoundaries()
{
    AssertHasOutcode(new PsxGteScreenPoint(0, 1), NativeTerrainCpuOutcode.Top, true);
    AssertHasOutcode(new PsxGteScreenPoint(1, 1), NativeTerrainCpuOutcode.Top, false);
    AssertHasOutcode(new PsxGteScreenPoint(256, 255), NativeTerrainCpuOutcode.Bottom, false);
    AssertHasOutcode(new PsxGteScreenPoint(0, 256), NativeTerrainCpuOutcode.Bottom, true);
    AssertHasOutcode(new PsxGteScreenPoint(0, 120), NativeTerrainCpuOutcode.Left, true);
    AssertHasOutcode(new PsxGteScreenPoint(1, 120), NativeTerrainCpuOutcode.Left, false);
    AssertHasOutcode(new PsxGteScreenPoint(511, 120), NativeTerrainCpuOutcode.Right, false);
    AssertHasOutcode(new PsxGteScreenPoint(512, 120), NativeTerrainCpuOutcode.Right, true);

    NativeTerrainCpuOutcode error = NativeTerrainBaseProjection.ComputeFullCpuOutcode(
        new PsxGteScreenPoint(256, 120),
        PsxGteFlag.Error | PsxGteFlag.DivideOverflow);
    Assert(error == NativeTerrainCpuOutcode.ProjectionError,
        $"FLAG sign did not map exactly to outcode bit 0x10: 0x{(byte)error:X2}.");

    PsxGteScreenPoint packedPoint = new(-3, 7);
    NativeTerrainCpuOutcode packedOutcode =
        NativeTerrainCpuOutcode.Top | NativeTerrainCpuOutcode.ProjectionError;
    uint expected = unchecked((packedPoint.Packed << 5) + (byte)packedOutcode);
    Assert(NativeTerrainBaseProjection.PackScreenAndOutcode(packedPoint, packedOutcode) == expected,
        "Packed SXY/outcode word no longer matches the retail sll/add sequence.");

    NativeTerrainCpuOutcode simpleZero =
        NativeTerrainBaseProjection.ComputeSimpleCpuOutcode(new PsxGteScreenPoint(0, 120));
    NativeTerrainCpuOutcode simpleWide =
        NativeTerrainBaseProjection.ComputeSimpleCpuOutcode(new PsxGteScreenPoint(512, 120));
    Assert((simpleZero & (NativeTerrainCpuOutcode.Left | NativeTerrainCpuOutcode.Right)) == 0 &&
           (simpleWide & (NativeTerrainCpuOutcode.Left | NativeTerrainCpuOutcode.Right)) ==
           (NativeTerrainCpuOutcode.Left | NativeTerrainCpuOutcode.Right),
        "Simple tagged X mask no longer emits the retail paired 0x0C bits.");

    PsxGteProjectionRegisters registers = IdentityRegisters(0, 0, 341);
    NativeTerrainBaseProjectionResult fullDepthBoundary = NativeTerrainBaseProjection.Project(
        registers,
        NativeTerrainBaseProjectionContext.FullTaggedContextBit1,
        new PsxGteVector[]
        {
            new(1, 120, 0x5FF),
            new(1, 120, 0x600),
            new(1, 120, 0x601)
        });
    Assert(fullDepthBoundary.Slots[0].ScratchEncoding ==
               NativeTerrainBaseScratchEncoding.PackedFullOutcode &&
           fullDepthBoundary.Slots[1].ScratchEncoding ==
               NativeTerrainBaseScratchEncoding.PackedSimpleOutcode &&
           fullDepthBoundary.Slots[2].ScratchEncoding ==
               NativeTerrainBaseScratchEncoding.PackedSimpleOutcode,
        "Full tagged 0x600 depth branch no longer selects the simplified outcode block exactly.");
}

static void AssertCommonOutsideReject()
{
    PsxGteProjectionRegisters registers = IdentityRegisters(0, 0, 400);
    NativeTerrainBaseProjectionResult allLeft = NativeTerrainBaseProjection.Project(
        registers,
        NativeTerrainBaseProjectionContext.FullTaggedContextBit1,
        new PsxGteVector[]
        {
            new(0, 10, 400),
            new(0, 20, 400),
            new(0, 30, 400)
        });
    Assert(allLeft.CommonOutsideMask == NativeTerrainCpuOutcode.Left &&
           allLeft.IsCommonOutsideRejected,
        $"Three common-left slots produced mask 0x{(byte)allLeft.CommonOutsideMask:X2}.");

    NativeTerrainBaseProjectionResult mixed = NativeTerrainBaseProjection.Project(
        registers,
        NativeTerrainBaseProjectionContext.FullTaggedContextBit1,
        new PsxGteVector[]
        {
            new(0, 10, 400),
            new(1, 20, 400),
            new(0, 30, 400),
            new(0, 40, 400)
        });
    Assert(mixed.CommonOutsideMask == NativeTerrainCpuOutcode.None &&
           !mixed.IsCommonOutsideRejected,
        $"Mixed inside/left slots were falsely common-rejected: 0x{(byte)mixed.CommonOutsideMask:X2}.");
}

static void AssertNclipAndNativeSlotOrder()
{
    PsxGteScreenPoint a = new(0, 0);
    PsxGteScreenPoint b = new(10, 0);
    PsxGteScreenPoint c = new(0, 10);
    Assert(NativeTerrainBaseProjection.ComputeGenericThreePointNclipMac0(a, b, c) == 100 &&
           NativeTerrainBaseProjection.ComputeGenericThreePointNclipMac0(a, c, b) == -100 &&
           NativeTerrainBaseProjection.ComputeGenericThreePointNclipMac0(a, new(5, 0), b) == 0,
        "NCLIP signed MAC0 determinant/backface values changed.");

    PsxGteProjectionRegisters registers = IdentityRegisters(0, 0, 400);
    PsxGteVector[] triangle =
    [
        new(0, 0, 400),
        new(10, 0, 400),
        new(0, 10, 400)
    ];
    NativeTerrainBaseProjectionResult triResult =
        NativeTerrainBaseProjection.Project(
            registers,
            NativeTerrainBaseProjectionContext.RawSxy,
            triangle);
    Assert(triResult.RawFourSlotNclip is null,
        "A deduplicated three-point set was falsely promoted to exact retail face NCLIP.");

    PsxGteVector[] rawTriangle =
    [
        new(0, 0, 400),
        new(10, 0, 400),
        new(0, 10, 400),
        new(0, 10, 400)
    ];
    NativeTerrainBaseProjectionResult rawTriangleResult =
        NativeTerrainBaseProjection.Project(
            registers,
            NativeTerrainBaseProjectionContext.RawSxy,
            rawTriangle);
    Assert(rawTriangleResult.RawFourSlotNclip == new NativeTerrainRawFourSlotNclip(100, 0),
        "Caller-proven native triangle repetition did not preserve raw [0,1,3]/[2,1,3] NCLIP order.");

    PsxGteVector[] quad =
    [
        new(0, 0, 400),
        new(10, 0, 400),
        new(0, 10, 400),
        new(10, 10, 400)
    ];
    NativeTerrainBaseProjectionResult quadResult =
        NativeTerrainBaseProjection.Project(
            registers,
            NativeTerrainBaseProjectionContext.RawSxy,
            quad);
    Assert(quadResult.RawFourSlotNclip == new NativeTerrainRawFourSlotNclip(100, 100),
        "Quad NCLIP did not preserve retail [0,1,3] then [2,1,3] order.");
}

static (string Sha256, int NearRerunCount) BuildFixtureSha256()
{
    const int fixtureCount = 4096;
    using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    uint random = 0xB453_71E9;
    byte[] rowBuffer = new byte[420];
    int nearRerunCount = 0;

    for (int fixtureIndex = 0; fixtureIndex < fixtureCount; fixtureIndex++)
    {
        int slotCount = (fixtureIndex & 1) == 0 ? 3 : 4;
        var sourceSlots = new PsxGteVector[slotCount];
        PsxGteProjectionRegisters registers;

        if ((fixtureIndex & 3) == 0)
        {
            registers = IdentityRegisters(256 << 16, 120 << 16, 341);
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                sourceSlots[slotIndex] = new PsxGteVector(
                    NextRange(ref random, -320, 320),
                    NextRange(ref random, -320, 320),
                    NextRange(ref random, 1, 255));
            }
        }
        else if ((fixtureIndex & 3) == 1)
        {
            registers = IdentityRegisters(
                NextRange(ref random, -256, 768) << 16,
                NextRange(ref random, -128, 512) << 16,
                unchecked((ushort)NextRange(ref random, 1, 1024)));
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                sourceSlots[slotIndex] = new PsxGteVector(
                    NextRange(ref random, -2048, 2048),
                    NextRange(ref random, -2048, 2048),
                    NextRange(ref random, 256, 4096));
            }
        }
        else
        {
            PsxGteRotationMatrix rotation = new(
                NextRange(ref random, -4096, 4096), NextRange(ref random, -4096, 4096), NextRange(ref random, -4096, 4096),
                NextRange(ref random, -4096, 4096), NextRange(ref random, -4096, 4096), NextRange(ref random, -4096, 4096),
                NextRange(ref random, -4096, 4096), NextRange(ref random, -4096, 4096), NextRange(ref random, -4096, 4096));
            registers = new PsxGteProjectionRegisters(
                rotation,
                new PsxGteTranslation(
                    NextRangeInt(ref random, -32768, 32768),
                    NextRangeInt(ref random, -32768, 32768),
                    NextRangeInt(ref random, -32768, 32768)),
                NextInt(ref random),
                NextInt(ref random),
                NextUShort(ref random),
                NextShort(ref random),
                NextInt(ref random));
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
                sourceSlots[slotIndex] = NextVector(ref random);
        }

        PsxGteProjectionFifo initialFifo = new(
            new PsxGteScreenPoint(NextShort(ref random), NextShort(ref random)),
            new PsxGteScreenPoint(NextShort(ref random), NextShort(ref random)),
            new PsxGteScreenPoint(NextShort(ref random), NextShort(ref random)),
            NextUShort(ref random),
            NextUShort(ref random),
            NextUShort(ref random),
            NextUShort(ref random));
        NativeTerrainBaseProjectionContext context =
            (NativeTerrainBaseProjectionContext)(fixtureIndex % 3);
        NativeTerrainBaseProjectionResult result =
            NativeTerrainBaseProjection.Project(registers, initialFifo, context, sourceSlots);
        nearRerunCount += result.Slots.Count(slot => slot.UsedNearCameraRerun);

        Span<byte> row = rowBuffer;
        row.Clear();
        int rowLength = SerializeFixture(result, row);
        hash.AppendData(row[..rowLength]);
    }

    return (Convert.ToHexString(hash.GetHashAndReset()), nearRerunCount);
}

static int SerializeFixture(
    NativeTerrainBaseProjectionResult result,
    Span<byte> destination)
{
    int offset = 0;
    WriteByte(destination, ref offset, (byte)result.Context);
    WriteByte(destination, ref offset, checked((byte)result.Slots.Length));
    WriteByte(destination, ref offset, (byte)result.CommonOutsideMask);
    WriteByte(destination, ref offset, result.IsCommonOutsideRejected ? (byte)1 : (byte)0);
    WriteByte(destination, ref offset, result.RawFourSlotNclip.HasValue ? (byte)1 : (byte)0);
    WriteInt32(destination, ref offset, result.RawFourSlotNclip?.FirstMac0 ?? int.MinValue);
    WriteInt32(destination, ref offset, result.RawFourSlotNclip?.SecondMac0 ?? int.MinValue);
    WriteFifo(destination, ref offset, result.FinalFifo);

    foreach (NativeTerrainBaseProjectedSlot slot in result.Slots)
    {
        WriteByte(destination, ref offset, checked((byte)slot.SlotIndex));
        WriteInt16(destination, ref offset, slot.Source.X);
        WriteInt16(destination, ref offset, slot.Source.Y);
        WriteInt16(destination, ref offset, slot.Source.Z);
        WriteProjectionSnapshot(destination, ref offset, slot.OriginalProjection);
        WriteByte(destination, ref offset, slot.UsedNearCameraRerun ? (byte)1 : (byte)0);
        WriteProjectionSnapshot(
            destination,
            ref offset,
            slot.NearCameraRerunProjection.GetValueOrDefault());
        WriteUInt32(destination, ref offset, slot.ScreenPoint.Packed);
        WriteUInt16(destination, ref offset, slot.Depth);
        WriteUInt32(destination, ref offset, (uint)slot.EffectiveFlag);
        WriteByte(destination, ref offset, (byte)slot.Outcode);
        WriteByte(destination, ref offset, (byte)slot.ScratchEncoding);
        WriteUInt32(destination, ref offset, slot.StoredScratchWord);
    }

    return offset;
}

static void WriteProjectionSnapshot(
    Span<byte> destination,
    ref int offset,
    PsxGteProjectionResult projection)
{
    WriteUInt32(destination, ref offset, (uint)projection.Flag);
    WriteFifo(destination, ref offset, projection.Fifo);
    WriteInt32(destination, ref offset, projection.Mac1);
    WriteInt32(destination, ref offset, projection.Mac2);
}

static void WriteFifo(
    Span<byte> destination,
    ref int offset,
    PsxGteProjectionFifo fifo)
{
    WriteUInt32(destination, ref offset, fifo.Sxy0.Packed);
    WriteUInt32(destination, ref offset, fifo.Sxy1.Packed);
    WriteUInt32(destination, ref offset, fifo.Sxy2.Packed);
    WriteUInt16(destination, ref offset, fifo.Sz0);
    WriteUInt16(destination, ref offset, fifo.Sz1);
    WriteUInt16(destination, ref offset, fifo.Sz2);
    WriteUInt16(destination, ref offset, fifo.Sz3);
}

static PsxGteProjectionRegisters IdentityRegisters(int ofx, int ofy, ushort h) =>
    new(
        PsxGteRotationMatrix.Identity,
        PsxGteTranslation.Zero,
        ofx,
        ofy,
        h,
        0x100,
        0);

static void AssertHasOutcode(
    PsxGteScreenPoint screenPoint,
    NativeTerrainCpuOutcode bit,
    bool expected)
{
    NativeTerrainCpuOutcode actual =
        NativeTerrainBaseProjection.ComputeFullCpuOutcode(screenPoint, PsxGteFlag.None);
    Assert(actual.HasFlag(bit) == expected,
        $"Packed outcode {bit} at {screenPoint} was {actual.HasFlag(bit)}, expected {expected}; full 0x{(byte)actual:X2}.");
}

static void AssertThrows(Action action, string message)
{
    try
    {
        action();
    }
    catch (ArgumentException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static short NextRange(ref uint state, int inclusiveMinimum, int inclusiveMaximum)
{
    return checked((short)NextRangeInt(ref state, inclusiveMinimum, inclusiveMaximum));
}

static int NextRangeInt(ref uint state, int inclusiveMinimum, int inclusiveMaximum)
{
    uint width = checked((uint)(inclusiveMaximum - inclusiveMinimum + 1));
    return inclusiveMinimum + (int)(Next(ref state) % width);
}

static PsxGteVector NextVector(ref uint state) =>
    new(NextShort(ref state), NextShort(ref state), NextShort(ref state));

static uint Next(ref uint state)
{
    state = unchecked((state * 1_664_525u) + 1_013_904_223u);
    return state;
}

static int NextInt(ref uint state) => unchecked((int)Next(ref state));
static ushort NextUShort(ref uint state) => unchecked((ushort)Next(ref state));
static short NextShort(ref uint state) => unchecked((short)Next(ref state));

static void WriteByte(Span<byte> destination, ref int offset, byte value) =>
    destination[offset++] = value;

static void WriteUInt16(Span<byte> destination, ref int offset, ushort value)
{
    BinaryPrimitives.WriteUInt16LittleEndian(destination[offset..], value);
    offset += sizeof(ushort);
}

static void WriteInt16(Span<byte> destination, ref int offset, short value)
{
    BinaryPrimitives.WriteInt16LittleEndian(destination[offset..], value);
    offset += sizeof(short);
}

static void WriteUInt32(Span<byte> destination, ref int offset, uint value)
{
    BinaryPrimitives.WriteUInt32LittleEndian(destination[offset..], value);
    offset += sizeof(uint);
}

static void WriteInt32(Span<byte> destination, ref int offset, int value)
{
    BinaryPrimitives.WriteInt32LittleEndian(destination[offset..], value);
    offset += sizeof(int);
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

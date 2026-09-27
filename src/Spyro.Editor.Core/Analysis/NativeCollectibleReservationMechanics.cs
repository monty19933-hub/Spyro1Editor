using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// Exact-USA reserve-word afterimage and inverse, not a native residence issuer.
/// The existing source-bound residence owns the manifest and original EXE/PETE.
/// This class changes only a detached executable copy; no ROM/card writer,
/// initialization hook, runtime allocation or whole-engine fit is supplied.
/// </summary>
internal sealed class NativeCollectibleReservationMechanics
{
    private const uint RamEnd = 0x80200000, PeteNativeBytes = 0x39C48, PeteDirectoryBytes = 0x3A000;
    private readonly byte[] original, executable;
    private static readonly NativeCollectibleSelectedSlotResidence.SourceSpan ClearSource = new(
        0x80016914, 0x1C, "572bd776ba509d54a3848293f5cf78731da439e3b203d100b9cb6fb2ec60decc",
        "actual word-clear helper called by frame allocator, including return delay");

    internal sealed record Geometry(uint Reserve, uint AllocationCeiling, uint PeteResidentBase,
        uint NormalFloor, uint StartupCdStart, uint StartupCdEnd, uint PeteTemporaryStart,
        uint PeteReadEnd, uint PeteCopySource, uint PeteCopySourceEnd, uint PeteActualCopyReadEnd,
        uint PeteReadOverhang, uint PeteActualReadOverlap, uint BootStackPointer, uint HeapByteLength);

    private NativeCollectibleReservationMechanics(NativeCollectibleSelectedSlotResidence residence,
        byte[] original, byte[] executable)
    {
        Residence = residence; this.original = original.ToArray(); this.executable = executable.ToArray();
        OriginalExecutableSha256 = Hash(this.original); ExecutableSha256 = Hash(this.executable);
        OriginalGeometry = MeasureReserve(residence.OriginalReserveValue);
        ProposedGeometry = MeasureReserve(residence.ProposedReserveValue);
        var beforeSpace = new NativeAddressSpace(NativeAddressKind.ExecutableFile, OriginalExecutableSha256,
            NativeAddressRevision.Original, this.original.Length);
        var afterSpace = new NativeAddressSpace(NativeAddressKind.ExecutableFile, ExecutableSha256,
            NativeAddressRevision.Final, this.executable.Length);
        ReserveBeforeimage = new(beforeSpace, residence.ReserveConstantOffset, 4);
        ReserveAfterimage = new(afterSpace, residence.ReserveConstantOffset, 4);
        Evidence = Array.AsReadOnly(residence.Evidence.Append(ClearSource).ToArray());
    }

    internal NativeCollectibleSelectedSlotResidence Residence { get; }
    internal string OriginalExecutableSha256 { get; }
    internal string ExecutableSha256 { get; }
    internal Geometry OriginalGeometry { get; }
    internal Geometry ProposedGeometry { get; }
    internal NativeByteExtent ReserveBeforeimage { get; }
    internal NativeByteExtent ReserveAfterimage { get; }
    internal IReadOnlyList<NativeCollectibleSelectedSlotResidence.SourceSpan> Evidence { get; }
    internal bool SourceEvidenceBound => true;
    internal bool NativeResidenceIssued => false;
    internal bool Installed => false;
    internal bool LifetimeProven => false;
    internal bool WholeEngineFitProven => false;
    internal bool CardIoApproved => false;
    internal bool PublicationApproved => false;

    internal static NativeCollectibleReservationMechanics Create(NativeCollectibleSelectedSlotResidence residence)
    {
        ArgumentNullException.ThrowIfNull(residence);
        if (!residence.SourceEvidenceBound)
            throw new InvalidDataException("Reservation mechanics require the source-bound residence, not neutral geometry.");
        byte[] original = residence.CopyOriginalExecutable(), pete = residence.CopyOriginalPeteHeader();
        residence.RequireExactOriginal(original, pete);
        NativeCollectibleSelectedSlotResidence.ValidateSourceBeforeimages(original, pete);
        if (Hash(original.AsSpan(checked((int)(ClearSource.Pc - 0x8000F800)), ClearSource.ByteLength)) != ClearSource.Sha256)
            throw new InvalidDataException("Native frame allocator's actual clear helper beforeimage changed.");
        byte[] executable = original.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(executable.AsSpan(residence.ReserveConstantOffset), residence.ProposedReserveValue);
        var plan = new NativeCollectibleReservationMechanics(residence, original, executable);
        if (plan.ProposedGeometry.AllocationCeiling != residence.ProposedBase ||
            plan.OriginalGeometry.AllocationCeiling != residence.ProposedEnd ||
            plan.ProposedGeometry.NormalFloor != residence.ProposedNormalFloor ||
            !plan.RevertExact(executable).AsSpan().SequenceEqual(original))
            throw new InvalidDataException("Reserve afterimage lost the exact residence geometry or original inverse.");
        return plan;
    }

    // Neutral arithmetic for the two exact fixed profiles only. It cannot create
    // an afterimage, prove asynchronous transfers, or certify any level's fit.
    internal static Geometry MeasureReserve(uint reserve)
    {
        if (reserve is not (0x800 or 0xA00))
            throw new InvalidDataException("Only the original 800 and proposed A00 reserve mechanics are described.");
        uint top = RamEnd - reserve, resident = top - PeteNativeBytes;
        uint temporary = top - 2 * PeteDirectoryBytes, readEnd = temporary + PeteDirectoryBytes;
        uint copySource = temporary + 0x800, copyEnd = copySource + PeteNativeBytes;
        uint actualReadEnd = copySource + ((PeteNativeBytes + 15) & ~15u);
        return new(reserve, top, resident, resident - 0x6008 - 2 * 0x1C000,
            0x801C0000 - reserve, top, temporary, readEnd, copySource, copyEnd, actualReadEnd,
            actualReadEnd - readEnd, actualReadEnd - resident, 0x801FFFF8,
            0x200000 - 8 - reserve - 0x7AA38);
    }

    internal byte[] CopyOriginalExecutable() => original.ToArray();
    internal byte[] CopyExecutable() => executable.ToArray();

    internal byte[] ApplyToExactOriginal(ReadOnlySpan<byte> bytes)
    {
        if (!bytes.SequenceEqual(original))
            throw new InvalidDataException("Reserve afterimage requires the complete exact original executable, including its complement.");
        return executable.ToArray();
    }

    internal byte[] RevertExact(ReadOnlySpan<byte> bytes)
    {
        if (!bytes.SequenceEqual(executable))
            throw new InvalidDataException("Reserve inverse requires the complete exact afterimage, including its complement.");
        byte[] reverted = bytes.ToArray();
        int offset = Residence.ReserveConstantOffset;
        original.AsSpan(offset, 4).CopyTo(reverted.AsSpan(offset, 4));
        if (!reverted.AsSpan().SequenceEqual(original))
            throw new InvalidDataException("Reserve inverse did not reconstruct every original executable byte.");
        return reverted;
    }

    internal void RequireNativeInstallation() => throw new InvalidDataException(
        "Reservation mechanics do not issue initialization, lifetime, whole-engine fit, card or publication authority.");

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

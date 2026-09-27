using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// Detached cold-boot prototype in the intersection of the exact original EXE
/// load extent and its immediate BSS clear. This is not a BIOS-loader, cache,
/// IRQ, native RAM-lifetime or installation certificate. The stub clears only
/// the paired proposed reservation, then leaves permanently for original startup.
/// </summary>
internal sealed class NativeCollectibleColdBootBootstrap
{
    private const uint StubStart = 0x80075640, LoadedEnd = 0x80075800, Startup = 0x8005B8E0;
    private const int TailOffset = 0x65E40, TailBytes = 0x1C0;
    private readonly byte[] ancestor, executable, stub;
    internal sealed record SourceSpan(string Role, int ExecutableOffset, int ByteLength, string Sha256);
    private static readonly SourceSpan[] SourceSpans =
    [
        new("exact loader header including entry, transfer, BSS and initial stack fields", 0, 0x80,
            "347f9647eac84734506473cda2d4c3d5e175e517b97a78fa1343e26079333d46"),
        new("complete loaded tail subsequently cleared by original startup", TailOffset, TailBytes,
            "5c55c8f4db4010ba9203d83536d0609856af8c847ac039e37e7dde8fbd574b61"),
        new("original BSS zero loop including branch delay slot", 0x4C0E0, 0x24,
            "7896dd616818db767dc8a988879948e6011c1b4c1600aff3762ec7dc645c1f48"),
    ];

    private NativeCollectibleColdBootBootstrap(NativeCollectibleReservationMechanics reservation,
        byte[] ancestor, byte[] executable, byte[] stub)
    {
        Reservation = reservation; this.ancestor = ancestor.ToArray(); this.executable = executable.ToArray();
        this.stub = stub.ToArray(); ExecutableSha256 = Hash(this.executable);
        var finalSpace = new NativeAddressSpace(NativeAddressKind.ExecutableFile, ExecutableSha256,
            NativeAddressRevision.Final, this.executable.Length);
        EntryField = new(finalSpace, 0x10, 4); TailOwner = new(finalSpace, TailOffset, TailBytes);
        StubOwner = new(finalSpace, TailOffset, this.stub.Length);
        Evidence = Array.AsReadOnly(SourceSpans.ToArray());
    }

    internal NativeCollectibleReservationMechanics Reservation { get; }
    internal string ExecutableSha256 { get; }
    internal uint EntryPc => StubStart;
    internal uint OriginalEntryPc => Startup;
    internal uint LedgerStart => Reservation.Residence.ProposedBase;
    internal uint LedgerEnd => Reservation.Residence.ProposedEnd;
    internal int StubByteLength => stub.Length;
    internal NativeByteExtent EntryField { get; }
    internal NativeByteExtent TailOwner { get; }
    internal NativeByteExtent StubOwner { get; }
    internal IReadOnlyList<SourceSpan> Evidence { get; }
    internal bool SourceEvidenceBound => true;
    internal bool BiosLoadProven => false;
    internal bool InstructionCacheProven => false;
    internal bool HandoffBufferOwnershipProven => false;
    internal bool InterruptSafetyProven => false;
    internal bool NativeResidenceIssued => false;
    internal bool Installed => false;
    internal bool LifetimeProven => false;
    internal bool WholeEngineFitProven => false;
    internal bool CardIoApproved => false;
    internal bool PublicationApproved => false;
    internal bool PersistentCodeIssued => false;

    internal static NativeCollectibleColdBootBootstrap Create(NativeCollectibleReservationMechanics reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        byte[] original = reservation.CopyOriginalExecutable(), ancestor = reservation.CopyExecutable();
        RequireOriginalSource(original);
        if (!reservation.SourceEvidenceBound || !reservation.RevertExact(ancestor).AsSpan().SequenceEqual(original) ||
            Word(ancestor, 0x65DA4) != 0xA00 || reservation.Residence.ProposedBase != 0x801FF600 ||
            reservation.Residence.ProposedEnd != 0x801FF800)
            throw new InvalidDataException("Cold bootstrap requires its exact paired source-bound A00 reservation afterimage.");

        uint first = reservation.Residence.ProposedBase, end = reservation.Residence.ProposedEnd;
        // Only v0, v1 and at are changed. Original startup overwrites all three
        // before use. There are no loads, calls, stack accesses or special-register
        // writes; the J (not JAL) preserves incoming RA for the original BSS save.
        uint[] words =
        [
            0x3C020000 | (first >> 16), 0x34420000 | (first & 0xFFFF),
            0x3C030000 | (end >> 16), 0x34630000 | (end & 0xFFFF),
            0xAC400000, // sw zero,0(v0)
            0x24420004, // addiu v0,v0,4
            0x0043082B, // sltu at,v0,v1
            0x1420FFFC, 0, // bne at,zero to store; branch delay NOP
            0x08000000 | ((Startup >> 2) & 0x03FFFFFF), 0, // tail J; delay NOP
        ];
        byte[] stub = new byte[words.Length * 4];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(stub.AsSpan(i * 4), words[i]);
        RequireStubExtent(StubStart, stub.Length);
        byte[] final = ancestor.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(final.AsSpan(0x10), StubStart);
        stub.CopyTo(final, TailOffset);
        var plan = new NativeCollectibleColdBootBootstrap(reservation, ancestor, final, stub);
        if (!plan.RevertExact(final).AsSpan().SequenceEqual(original))
            throw new InvalidDataException("Bootstrap and reserve inverse failed complete original reconstruction.");
        return plan;
    }

    // Geometry refusal only: this cannot issue a source owner or arbitrary code.
    internal static void RequireStubExtent(uint entryPc, int byteLength)
    {
        if (byteLength <= 0 || byteLength % 4 != 0 || entryPc % 4 != 0 || entryPc < StubStart ||
            (ulong)entryPc + (uint)byteLength > LoadedEnd)
            throw new InvalidDataException("Stub is unaligned, oversized or outside the exact loaded-and-cleared ephemeral extent.");
    }

    internal static void RequireOriginalSource(ReadOnlySpan<byte> bytes)
    {
        NativeJapaneseEggMemoryReport.RequireSourceExecutable(bytes);
        foreach (SourceSpan span in SourceSpans)
            if (Hash(bytes.Slice(span.ExecutableOffset, span.ByteLength)) != span.Sha256)
                throw new InvalidDataException("Cold bootstrap original beforeimage changed: " + span.Role);
        if (Word(bytes, 0x10) != Startup || Word(bytes, 0x14) != 0 || Word(bytes, 0x18) != 0x80010000 ||
            Word(bytes, 0x1C) != 0x65800 || (ulong)Word(bytes, 0x18) + Word(bytes, 0x1C) != LoadedEnd ||
            Word(bytes, 0x20) != 0 || Word(bytes, 0x24) != 0 || Word(bytes, 0x28) != 0 || Word(bytes, 0x2C) != 0 ||
            Word(bytes, 0x30) != 0x801FFFF0 || Word(bytes, 0x34) != 0 ||
            bytes.Slice(TailOffset, TailBytes).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("Original header and complete ephemeral-tail geometry differ from this fixed prototype.");
    }

    internal byte[] CopyExecutable() => executable.ToArray();
    internal byte[] CopyStub() => stub.ToArray();

    internal byte[] ApplyToExactReservation(ReadOnlySpan<byte> bytes)
    {
        if (!bytes.SequenceEqual(ancestor))
            throw new InvalidDataException("Bootstrap requires every byte of its paired reservation ancestor, including header, tail and complement.");
        return executable.ToArray();
    }

    internal byte[] RevertToReservationExact(ReadOnlySpan<byte> bytes)
    {
        if (!bytes.SequenceEqual(executable))
            throw new InvalidDataException("Bootstrap inverse requires the exact complete afterimage, including entry, tail and complement.");
        byte[] reverted = bytes.ToArray();
        ancestor.AsSpan(0x10, 4).CopyTo(reverted.AsSpan(0x10, 4));
        ancestor.AsSpan(TailOffset, TailBytes).CopyTo(reverted.AsSpan(TailOffset, TailBytes));
        if (!reverted.AsSpan().SequenceEqual(ancestor))
            throw new InvalidDataException("Bootstrap inverse did not recover the complete paired reservation ancestor.");
        return reverted;
    }

    internal byte[] RevertExact(ReadOnlySpan<byte> bytes) => Reservation.RevertExact(RevertToReservationExact(bytes));

    internal void RequireNativeInstallation() => throw new InvalidDataException(
        "Ephemeral bootstrap is uninstalled: BIOS load/cache/handoff/IRQ ownership and native lifetime remain unproved.");

    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

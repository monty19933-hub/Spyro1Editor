using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// An unissued selected-slot RAM proposal bound to an exact authored manifest.
/// Source capture retains original USA beforeimages; it never patches the reserve,
/// initializes RAM, writes a card or proves that this owner survives native transitions.
/// </summary>
internal sealed class NativeCollectibleSelectedSlotResidence
{
    internal const int ReservationBytes = 0x200;
    internal const int MaximumAdmittedLifetimeInstances = 1408;
    internal const int MetadataOffset = 0x10, MetadataBytes = 0x80, BitmapOffset = 0x90;
    internal const uint AbiMagic = 0x524C4353;
    internal const ushort AbiVersion = 1;
    internal const byte ReadyPhase = 1;
    internal const int MagicOffset = 0x10, VersionOffset = 0x14, PhaseOffset = 0x16,
        OrdinarySlotOffset = 0x17, ControllerOffset = 0x18, DirtyOffset = 0x19,
        NativeFileOffset = 0x1A, StatusOffset = 0x1C, ContentIdOffset = 0x20,
        ManifestDigestOffset = 0x30, EnvelopeDigestOffset = 0x50, RevisionOffset = 0x70,
        LifetimeCountOffset = 0x74, BitmapBytesOffset = 0x76, ActiveCountOffset = 0x78,
        EarnedActiveCountOffset = 0x7A, MutationGenerationOffset = 0x7C,
        TransactionGenerationOffset = 0x80, OperationOffset = 0x84,
        FailureOffset = 0x88, SessionEpochOffset = 0x8C;
    private const uint RamEnd = 0x80200000, PeteNativeBytes = 0x39C48;
    private const string PeteHeaderSha256 = "b9a4de193b5cf0bd9592de15c57aa5daa3713c3575026154b45402f3aceef6f1";
    private readonly byte[] manifestBytes, activeMask;
    private readonly byte[]? originalExecutable, originalPeteHeader;

    internal sealed record LifetimeSizing(int LifetimeCount, int RawBitmapBytes, int AlignedBitmapBytes,
        int LiveFootprintBytes, int DetachedEnvelopeBytes, int NativeTransferBytes)
    {
        internal bool FitsProposedReservation => LiveFootprintBytes <= ReservationBytes;
    }

    internal sealed record SourceSpan(uint Pc, int ByteLength, string Sha256, string Role);

    // Pinned windows from Inspect-SpyroUsaPeteLifetime.py. These document specific
    // native readers/writers; a bounded source census is not computed-pointer closure.
    private static readonly SourceSpan[] SourceSpans =
    [
        new(0x8005B8E0, 0x94, "336d9b86228c313bd5e6d861117f6d51138a2e4c26278d34b6fbdb713a984900", "boot BSS, heap and separate stack setup"),
        new(0x800755A4, 8, "284620b7f621e7f47fb58e162ef4aa4db52946a05fb46cdf0dcdd83c0f0a11cb", "top reservation and unchanged stack origin"),
        new(0x80012C6C, 0x3C, "9bd86968507bb1300fa084f5a07d4d5c4924064547761b93d1a2ae3bff7a0f32", "boot ceiling reset and full PETE load"),
        new(0x8005B7D8, 0x108, "58fa8da2248f5804c79362dad219ccaedcb3214cff735afdb276a1fa04d93e23", "PETE staging, recopy and resident base"),
        new(0x8005B6F8, 0xE0, "d0b4cf5a7cf9ee407ff413f7261bd5f92f4880d7cee8467852f4d757e6b84295", "normal and alternate frame allocation"),
        new(0x8001441C, 0x34, "6840ddf7d901d954c7cbad4022d5f648fda51fd87c77131469b3257bec374b2b", "four-byte particle sentinel at ceiling requires guard"),
        new(0x80053570, 0x98, "2eefe927534faee54464ea9af9e53a9382ff3fafa0f90d50b6a68982dce61ddb", "particle records reach the native ceiling"),
        new(0x80033004, 0xD4, "abce71d91065d9945cdd40336e1e19f3e3cf97313fa0e3899c0cb1ab648dfdae", "card outer transition resets ceiling"),
        new(0x800331AC, 0x10C, "c819213b17027934dc5094e5a2655a8bae5340ada4d21928562b752d9972d7cd", "mode14 transition resets ceiling"),
        new(0x80012948, 0x30, "cd2a27c8728a6f1962facc9cdd4daefa768d1535118ecf08b7713464b939d302", "startup CD destination charged for reserve"),
        new(0x80016958, 0x54, "a3b8e30c7eb4c67ee12309f645026b8b9f522276e769b38295fc7e25d40667f5", "native forward-copy prefetch extent"),
        new(0x8002BAB8, 0x68, "a11d3e14d6373af261e89f3db30252199796aec811cddc0236637fcf5f785e14", "BIOS executable handoff remains outside lifetime proof"),
    ];

    private NativeCollectibleSelectedSlotResidence(DetachedCollectibleManifest manifest, int ordinarySlot,
        byte[]? executable = null, byte[]? peteHeader = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if ((uint)ordinarySlot >= DetachedCollectibleSave.OrdinarySlotCount)
            throw new ArgumentOutOfRangeException(nameof(ordinarySlot), "Only ordinary slots 0, 1 and 2 exist.");
        Sizing = MeasureLifetime(manifest.LifetimeCount);
        if (manifest.LifetimeCount == 0 || !Sizing.FitsProposedReservation)
            throw new InvalidDataException("This residence proposal requires 1..1408 lifetime identities; retired bits cannot be discarded.");
        ContentId = manifest.ContentId; ManifestRevision = manifest.Revision;
        ManifestSha256 = manifest.Sha256; ActiveCount = manifest.ActiveCount; OrdinarySlot = ordinarySlot;
        manifestBytes = manifest.SerializeCanonical();
        activeMask = new byte[Sizing.AlignedBitmapBytes];
        foreach (var entry in manifest.Entries)
            if (!entry.Retired) activeMask[entry.BitIndex / 8] |= (byte)(1 << (entry.BitIndex % 8));
        originalExecutable = executable?.ToArray(); originalPeteHeader = peteHeader?.ToArray();
        Evidence = Array.AsReadOnly(executable is null ? Array.Empty<SourceSpan>() : SourceSpans.ToArray());
    }

    internal Guid ContentId { get; }
    internal uint ManifestRevision { get; }
    internal string ManifestSha256 { get; }
    internal int ActiveCount { get; }
    internal int OrdinarySlot { get; }
    internal LifetimeSizing Sizing { get; }
    internal int GuardOffset => 0;
    internal int GuardBytes => MetadataOffset;
    internal int SelectedBitsOffset => BitmapOffset;
    internal int ActiveMaskOffset => BitmapOffset + Sizing.AlignedBitmapBytes;
    internal uint OriginalReserveValue => 0x800;
    internal uint ProposedReserveValue => OriginalReserveValue + ReservationBytes;
    internal int ReserveConstantOffset => 0x65DA4;
    internal uint ProposedBase => RamEnd - ProposedReserveValue;
    internal uint ProposedEnd => RamEnd - OriginalReserveValue;
    internal uint ProposedNormalFloor => ProposedBase - PeteNativeBytes - 0x6008 - 2 * 0x1C000;
    internal bool SourceEvidenceBound => originalExecutable is not null;
    internal IReadOnlyList<SourceSpan> Evidence { get; }
    internal bool NativeResidenceIssued => false;
    internal bool Installed => false;
    internal bool WholeEngineFitProven => false;
    internal bool LifetimeProven => false;
    internal bool CardIoApproved => false;
    internal bool PublicationApproved => false;

    internal static LifetimeSizing MeasureLifetime(int lifetimeCount)
    {
        if ((uint)lifetimeCount > DetachedCollectibleManifest.MaximumLifetimeInstances)
            throw new ArgumentOutOfRangeException(nameof(lifetimeCount), "Lifetime count exceeds the unchanged detached schema cap.");
        int raw = (lifetimeCount + 7) / 8, aligned = (raw + 15) & ~15;
        // Size the existing format; no competing serializer or native card wrapper.
        int envelope = DetachedCollectibleSaveCodec.HeaderBytes +
            DetachedCollectibleSave.OrdinarySlotCount * raw + SHA256.HashSizeInBytes;
        return new(lifetimeCount, raw, aligned, BitmapOffset + 2 * aligned, envelope, (envelope + 0x7F) & ~0x7F);
    }

    internal static NativeCollectibleSelectedSlotResidence Describe(DetachedCollectibleManifest manifest, int ordinarySlot) =>
        new(manifest, ordinarySlot);

    internal static NativeCollectibleSelectedSlotResidence Capture(NativeRegionalSourceSession usa,
        DetachedCollectibleManifest manifest, int ordinarySlot)
    {
        ArgumentNullException.ThrowIfNull(usa);
        _ = Describe(manifest, ordinarySlot);
        if (usa.Revision != NativeDiscRevision.UsaScus94228 || usa.Entries[8].Offset != 0x7B8800 || usa.Entries[8].ByteLength != 0x3A000)
            throw new InvalidDataException("Residence source evidence requires the exact original USA PETE owner.");
        try
        {
            byte[] executable = usa.ReadExecutableRange(0, usa.ExecutableByteLength);
            byte[] peteHeader = usa.ReadWadRange(usa.Entries[8].Offset, 0x800);
            ValidateSourceBeforeimages(executable, peteHeader);
            return new(manifest, ordinarySlot, executable, peteHeader);
        }
        finally { usa.VerifyUnchanged(); }
    }

    internal void RequireManifest(DetachedCollectibleManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.Sha256 != ManifestSha256 || !manifest.SerializeCanonical().AsSpan().SequenceEqual(manifestBytes))
            throw new InvalidDataException("Selected-slot residence requires its exact manifest, including revision and retired identities.");
    }

    internal byte[] CopyActiveMask() => activeMask.ToArray();

    internal byte[] CopySelectedSlotBits(DetachedCollectibleSave state)
    {
        ArgumentNullException.ThrowIfNull(state);
        RequireManifest(state.Manifest);
        byte[] copy = new byte[Sizing.AlignedBitmapBytes];
        state.CopySlotBits(OrdinarySlot).CopyTo(copy, 0);
        return copy;
    }

    internal byte[] CopyOriginalExecutable() => originalExecutable?.ToArray() ??
        throw new InvalidDataException("Neutral residence sizing has no original executable evidence.");

    internal byte[] CopyOriginalPeteHeader() => originalPeteHeader?.ToArray() ??
        throw new InvalidDataException("Neutral residence sizing has no original PETE header evidence.");

    internal void RequireExactOriginal(ReadOnlySpan<byte> executable, ReadOnlySpan<byte> peteHeader)
    {
        if (originalExecutable is null || originalPeteHeader is null ||
            !executable.SequenceEqual(originalExecutable) || !peteHeader.SequenceEqual(originalPeteHeader))
            throw new InvalidDataException("Complete original executable and PETE header must match the captured source beforeimages.");
    }

    internal static void ValidateSourceBeforeimages(ReadOnlySpan<byte> executable, ReadOnlySpan<byte> peteHeader)
    {
        NativeJapaneseEggMemoryReport.RequireSourceExecutable(executable);
        if (peteHeader.Length != 0x800 || Hash(peteHeader) != PeteHeaderSha256 ||
            BinaryPrimitives.ReadUInt32LittleEndian(peteHeader) != PeteNativeBytes ||
            BinaryPrimitives.ReadUInt32LittleEndian(executable[0x65DA4..]) != 0x800 ||
            BinaryPrimitives.ReadUInt32LittleEndian(executable[0x65DA8..]) != 0x200000)
            throw new InvalidDataException("Original PETE header, native top reserve or independent stack origin changed.");
        foreach (SourceSpan span in SourceSpans)
            if (Hash(executable.Slice(checked((int)(span.Pc - 0x8000F800)), span.ByteLength)) != span.Sha256)
                throw new InvalidDataException("Residence source beforeimage changed: " + span.Role);
    }

    internal void RequireNativeInstallation() => throw new InvalidDataException(
        "Selected-slot residence is unissued: whole-engine capacity, initialization and transition lifetime remain unproved.");

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

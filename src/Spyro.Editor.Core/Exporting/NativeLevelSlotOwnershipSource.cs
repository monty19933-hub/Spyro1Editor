using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeLevelSlotSourceCertificate(string Id, long ExecutableOffset, int ByteLength, string Sha256);

/// <summary>
/// Exact clean-USA source gate for the neutral ownership kernel. No caller
/// catalog, receipt, hash string or modified laboratory image grants authority.
/// This captures identity/capacity, not resource semantics, output bytes, CUE
/// freshness, runtime readiness or permission to publish.
/// </summary>
internal sealed class NativeLevelSlotOwnershipSource
{
    internal const string ProfileId = "native-retail-level-slot-ownership-clean-usa-v1";
    internal const string ExpectedSourceSha256 = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    internal const long ExpectedSourceLength = 661547040;
    internal const int WadLength = 0x6927000;
    internal const int ExecutableLength = 0x66000;
    private const string HeaderSha256 = "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd";
    private static readonly IReadOnlyList<NativeLevelSlotSourceCertificate> Certificates = Array.AsReadOnly<NativeLevelSlotSourceCertificate>(
    [
        new("native-decimal-index-and-pair-consumer", 0x5E10, 0x94, "1e7dff23571596585fa47fcf653e9b694413b2c92c1d8fb0d49f9013fd61ac20"),
        new("late-music-consumer", 0x1C594, 0x70, "646ab5899fbbee768024bbb6be81a72bafa8d0c2c43483dad5958aa97d184de2"),
        new("bounded-35-row-late-music-table", 0x5F85C, 0x1A4, "9ba86a52d7b508d9a376ec819bbcee27ba9f46553c50dae1ce3fd16432555a2c"),
        new("save-deserialize-capacities", 0x49D94, 0x2D0, "ff2bcfe124d934838348cb79807d36cdacc3d82f3cf6773d007c4aa0555e2ae6"),
        new("save-serialize-capacities", 0x4A064, 0x1E4, "b8674b556d8f4154886306731ee50c9812f2cac1d9a224756c110be693d19056"),
        new("portal-upper-exclusive-65", 0x47900, 0x24, "4aacc56f4a46459bf3d27c5ff16208618f6789cb10f683c8dc18f06b62fffd7a")
    ]);

    private readonly NativeLevelSlotOwnershipLayout _layout;
    internal string SourceImageSha256 => ExpectedSourceSha256;
    internal string WadHeaderSha256 => HeaderSha256;
    internal IReadOnlyList<NativeLevelSlotSourceCertificate> NativeCertificates => Certificates;
    internal NativeAddressSpace Wad => _layout.Wad;
    internal NativeAddressSpace Executable => _layout.Executable;
    internal IReadOnlyList<NativeLevelSlotBinding> Slots => _layout.Slots;
    internal IReadOnlyList<NativeObservedPortalBinding> ObservedPortals => _layout.ObservedPortals;

    private NativeLevelSlotOwnershipSource(NativeLevelSlotOwnershipLayout layout) => _layout = layout;

    internal static async Task<NativeLevelSlotOwnershipSource> CaptureAsync(string sourceImagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        string path = Path.GetFullPath(sourceImagePath);
        await using FileStream image = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        await RequireSourceAsync(image, cancellationToken);
        DiscLayout layout = DiscImage.DetectLayout(path);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("Native slot ownership requires exact MODE2/2352 source layout.");
        DiscFileRecord wad = DiscImage.FindRootFileRecord(image, layout,
            n => n.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord exe = DiscImage.FindRootFileRecord(image, layout,
            n => n.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        if (wad.Lba != 37 || wad.Size != WadLength || exe.Lba != 53875 || exe.Size != ExecutableLength)
            throw new InvalidDataException("Native slot ownership requires the exact retail WAD/EXE ISO extents.");
        byte[] header = DiscImage.ReadFileBytes(image, layout, wad.Lba, 0, 0x800);
        RequireHash(header, HeaderSha256, "retail WAD header");
        byte[] executable = DiscImage.ReadFileBytes(image, layout, exe.Lba, 0, exe.Size);
        foreach (var certificate in Certificates)
            RequireHash(executable.AsSpan((int)certificate.ExecutableOffset, certificate.ByteLength),
                certificate.Sha256, certificate.Id);
        if (!executable.AsSpan(0x5FA00, 12).SequenceEqual(Convert.FromHexString("60EA0000684A010028BB0100")))
            throw new InvalidDataException("The would-be late-music row35 no longer belongs to the exact PETEXA LBA table.");

        NativeAddressSpace wadSpace = new(NativeAddressKind.WadFile, ExpectedSourceSha256,
            NativeAddressRevision.Original, wad.Size);
        NativeAddressSpace exeSpace = new(NativeAddressKind.ExecutableFile, ExpectedSourceSha256,
            NativeAddressRevision.Original, exe.Size);
        List<NativeObservedPortalBinding> portals = [];
        for (int index = 0; index < NativeLevelSlotOwnershipLayout.RetailSlotCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int levelId = (index / 6 + 1) * 10 + index % 6;
            int entry = 10 + index * 2;
            long dataOffset = Word(header, entry * 8);
            int dataLength = checked((int)Word(header, entry * 8 + 4));
            byte[] dataHeader = ReadBounded(dataOffset, dataLength, 0, 0x20);
            int subOffset = checked((int)Word(dataHeader, 8));
            int subLength = checked((int)Word(dataHeader, 12));
            if (subOffset < 0x800 || subLength < 16 || subLength > 8 * 1024 * 1024 ||
                subOffset + (long)subLength > dataLength)
                throw new InvalidDataException($"Level{levelId} has no bounded native data subfile1.");
            int componentOffset = 0;
            for (int component = 0; component < 3; component++)
                componentOffset = checked(componentOffset + ReadComponentLength(componentOffset));
            int specialLength = ReadComponentLength(componentOffset);
            if (specialLength > 65536)
                throw new InvalidDataException("Native portal descriptor component exceeds its bounded capture budget.");
            byte[] special = ReadBounded(dataOffset + subOffset, subLength, componentOffset, specialLength);
            portals.AddRange(ParsePortalComponent(special, levelId,
                dataOffset + subOffset + componentOffset, wadSpace));

            int ReadComponentLength(int offset)
            {
                byte[] prefix = ReadBounded(dataOffset + subOffset, subLength, offset, 4);
                uint raw = Word(prefix, 0);
                if (raw < 4 || raw > subLength - (long)offset || raw % 4 != 0)
                    throw new InvalidDataException($"Level{levelId} has an invalid size-prefixed native component.");
                return checked((int)raw);
            }
        }
        if (portals.Count != 29 || portals.Any(p => p.DestinationLevelId == 65) ||
            portals.Select(p => p.DestinationLevelId).Distinct().Count() != 29)
            throw new InvalidDataException("The exact29 retail portal surface bindings changed.");
        NativeLevelSlotOwnershipLayout ownership = new(header, wadSpace, exeSpace, portals);
        // Hash the same open source again after sparse reads. No issued snapshot
        // can survive a changed/truncated source during this capture.
        await RequireSourceAsync(image, cancellationToken);
        return new(ownership);

        byte[] ReadBounded(long baseOffset, int ownerLength, int offset, int count)
        {
            if (baseOffset < 0 || ownerLength <= 0 || baseOffset > wad.Size - (long)ownerLength ||
                offset < 0 || count <= 0 || offset > ownerLength - (long)count)
                throw new InvalidDataException("A native portal read leaves its explicitly bounded source resource.");
            return DiscImage.ReadFileBytes(image, layout, wad.Lba, baseOffset + offset, count);
        }
    }

    internal NativeSourceBoundLevelSlotPlan Reserve(IReadOnlyList<NativeLevelSlotRequest> requests,
        IReadOnlyList<NativeLevelSlotDeclaredWrite>? writes = null,
        IReadOnlyList<NativeLevelSlotPortalIntent>? portalIntents = null) =>
        new(this, requests, writes, portalIntents);

    internal NativeLevelSlotOwnershipPlan CompileOwnership(IReadOnlyList<NativeLevelSlotRequest> requests,
        IReadOnlyList<NativeLevelSlotDeclaredWrite>? writes,
        IReadOnlyList<NativeLevelSlotPortalIntent>? portalIntents) => _layout.Reserve(requests, writes, portalIntents);

    internal static IReadOnlyList<NativeObservedPortalBinding> ParsePortalComponent(ReadOnlySpan<byte> component,
        int levelId, long wadOffset, NativeAddressSpace wad)
    {
        if (component.Length < 8 || component.Length > 65536 || Word(component, 0) != component.Length)
            throw new InvalidDataException("A portal component needs its complete native length preimage.");
        uint count = Word(component, 4);
        if (count > 63 || 8 + count * 4 > component.Length)
            throw new InvalidDataException("A portal component has an invalid descriptor count.");
        int[] offsets = new int[count];
        for (int i = 0; i < offsets.Length; i++)
        {
            uint raw = Word(component, 8 + i * 4);
            if (raw < 4 + count * 4 || raw > component.Length - 8 || raw % 4 != 0)
                throw new InvalidDataException("A native portal descriptor points into its directory or outside its component.");
            offsets[i] = checked(4 + (int)raw);
        }
        if (offsets.Distinct().Count() != offsets.Length)
            throw new InvalidDataException("Shared native surface descriptors require a separate explicit alias policy.");
        List<NativeObservedPortalBinding> result = [];
        for (int i = 0; i < offsets.Length; i++)
        {
            int start = offsets[i];
            int end = offsets.Where(o => o > start).DefaultIfEmpty(component.Length).Min();
            if (component[start] != 6) continue; // Native type is a byte, not a u32.
            if (end - start < 12)
                throw new InvalidDataException("A portal descriptor is missing its destination/portal-index fields.");
            int destination = BinaryPrimitives.ReadInt32LittleEndian(component[(start + 4)..]);
            result.Add(new(levelId, i, destination, new(wad, checked(wadOffset + start), end - start),
                Hash(component[start..end])));
        }
        return result.AsReadOnly();
    }

    private static async Task RequireSourceAsync(FileStream image, CancellationToken cancellationToken)
    {
        if (image.Length != ExpectedSourceLength)
            throw new InvalidDataException("Native slot ownership rejects non-retail source length, including candidate/laboratory inputs.");
        image.Position = 0;
        string hash = Convert.ToHexString(await SHA256.HashDataAsync(image, cancellationToken)).ToLowerInvariant();
        if (hash != ExpectedSourceSha256 || image.Length != ExpectedSourceLength || image.Position != ExpectedSourceLength)
            throw new InvalidDataException("Native slot ownership rejects stale or modified source identity.");
    }
    private static uint Word(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void RequireHash(ReadOnlySpan<byte> bytes, string expected, string id)
    {
        if (Hash(bytes) != expected) throw new InvalidDataException($"Native slot source certificate changed: {id}.");
    }
}

internal sealed class NativeSourceBoundLevelSlotPlan
{
    internal NativeLevelSlotOwnershipSource Source { get; }
    internal NativeLevelSlotOwnershipPlan Ownership { get; }
    internal string PlanSha256 { get; }
    internal bool PublishesCandidate => false;
    internal bool AddsIndependentLevel => false;
    internal bool NativePayloadSemanticsProven => false;
    internal bool PortalRetargetingProven => false;
    internal bool RuntimeAcceptance => false;

    internal NativeSourceBoundLevelSlotPlan(NativeLevelSlotOwnershipSource source,
        IReadOnlyList<NativeLevelSlotRequest> requests, IReadOnlyList<NativeLevelSlotDeclaredWrite>? writes,
        IReadOnlyList<NativeLevelSlotPortalIntent>? portalIntents)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source; Ownership = source.CompileOwnership(requests, writes, portalIntents);
        PlanSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            NativeLevelSlotOwnershipSource.ProfileId + "\n" + source.SourceImageSha256 + "\n" +
            source.WadHeaderSha256 + "\n" + Ownership.GeometrySha256))).ToLowerInvariant();
    }
}

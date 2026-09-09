using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Analysis;

/// <summary>Neutral caller bytes, never a source-issued capability.</summary>
public sealed record NativeStaticTerrainTextureInput(int MaterialId, string SourceIdentity,
    string AddressDomainIdentity, string RelocationPolicy, byte[] Record, byte[] SourceVram);

public sealed record NativeStaticTerrainTextureAllocationRegion(int MaterialId, string SourceIdentity,
    string AddressDomainIdentity, string RelocationPolicy, string Kind, int BitsPerPixel,
    int SourceOffset, int TargetOffset, int RowByteLength, int RowCount, string Sha256);

public sealed record NativeStaticTerrainTextureContainedAlias(int MaterialId, string SourceIdentity,
    string AddressDomainIdentity, string RelocationPolicy, string Kind, int BitsPerPixel,
    int SourceOffset, int TargetOffset, int ParentSourceOffset, int ParentTargetOffset,
    int RowByteLength, int RowCount);

/// <summary>
/// Detached two-record allocator/reencoder. A complete declared upload partition
/// is required even for zero bytes. Public neutral ownership and source labels
/// do not establish native source authority. No staging, body or runtime approval.
/// </summary>
public sealed class NativeStaticTerrainTextureAllocation
{
    public const string RelocationPolicyId = "native-canonical-static-terrain-right-half-vram-v1";
    private const int GpuLength = 0x80000, UploadLength = 0xDE000, MaximumOwners = 65536;
    private readonly byte[] _upload;
    private NativeStaticTerrainTextureAllocation(byte[] upload, NativeStaticTerrainTextureLayout[] records,
        NativeStaticTerrainTextureAllocationRegion[] regions, NativeStaticTerrainTextureContainedAlias[] aliases,
        int freeBytes)
    {
        _upload = upload; Records = Array.AsReadOnly(records);
        Allocations = Array.AsReadOnly(regions); ContainedAliases = Array.AsReadOnly(aliases);
        AllocatedByteCount = regions.Sum(r => r.RowByteLength * r.RowCount); FreeByteCount = freeBytes;
    }
    public bool SourceCertified => false;
    public bool RuntimeVerified => false;
    public bool PublicationApproved => false;
    public IReadOnlyList<NativeStaticTerrainTextureLayout> Records { get; }
    public IReadOnlyList<NativeStaticTerrainTextureAllocationRegion> Allocations { get; }
    public IReadOnlyList<NativeStaticTerrainTextureContainedAlias> ContainedAliases { get; }
    public int AllocatedByteCount { get; }
    public int FreeByteCount { get; }
    public byte[] CopyUploadPayload() => _upload.ToArray();

    public static NativeStaticTerrainTextureAllocation Compile(ReadOnlySpan<byte> upload,
        IEnumerable<NativeId65NormalResourceOwner> uploadOwners,
        IEnumerable<NativeStaticTerrainTextureInput> inputs, int nativeClip)
    {
        if (upload.Length != UploadLength || nativeClip != 0x8000)
            throw new InvalidDataException("Two-record allocation requires the completeDE000 upload and exact admitted native clip8000.");
        // No payload clone, occupancy array or tentative region exists before
        // BOTH enumerations and EVERY source/owner shape bound have passed.
        var selected = Snapshot(inputs, 2);
        var owners = Snapshot(uploadOwners, MaximumOwners);
        if (selected.Length != 2) throw new InvalidDataException("The detached profile requires exactly two complete material records.");
        foreach (var input in selected)
        {
            if (input.MaterialId is < 0 or > 1 || !Label(input.SourceIdentity) || !Label(input.AddressDomainIdentity) ||
                input.RelocationPolicy != RelocationPolicyId || input.Record is null || input.Record.Length != 184 ||
                input.SourceVram is null || input.SourceVram.Length != GpuLength)
                throw new InvalidDataException("Material/source-domain/policy or complete record/GPU byte budget is invalid.");
        }
        if (selected[0].MaterialId == selected[1].MaterialId)
            throw new InvalidDataException("Two material owners must have distinct IDs0 and1.");
        Array.Sort(selected, (a, b) => a.MaterialId.CompareTo(b.MaterialId));
        Array.Sort(owners, (a, b) => a.Offset.CompareTo(b.Offset));
        int cursor = 0;
        foreach (var owner in owners)
        {
            if (!Label(owner.Id) || owner.Domain != NativeId65ResourceDomain.UploadPayload ||
                owner.Offset != cursor || owner.ByteLength <= 0 || owner.Offset + (long)owner.ByteLength > UploadLength ||
                owner.Sha256 is null || owner.Sha256.Length != 64 || owner.Sha256.Any(c => !Uri.IsHexDigit(c)))
                throw new InvalidDataException("Upload ownership must be a bounded exhaustive, disjoint, hashed partition.");
            if (owner.Offset < GpuLength)
            {
                if (owner.Offset + owner.ByteLength > GpuLength)
                    throw new InvalidDataException("GPU owner cannot cross into the separate SPU domain.");
                bool addressMatches = owner.Provenance switch
                {
                    "fresh-zero-upload-background" or "issued-detached-companion-VRAM" => owner.SourceWadOffset is null,
                    "retained-fixed-Town-presentation-VRAM" => owner.SourceWadOffset == 0x119E800L + owner.Offset,
                    _ => false
                };
                if (!addressMatches) throw new InvalidDataException("Unknown GPU provenance/address cannot grant free or shared storage.");
            }
            else if (owner.Offset != GpuLength || owner.ByteLength != UploadLength - GpuLength ||
                owner.Provenance != "explicit-retained-audio-baseline" || owner.SourceWadOffset != 0x121E800)
                throw new InvalidDataException("The complete source SPU bank is one protected non-GPU owner.");
            cursor += owner.ByteLength;
        }
        if (cursor != UploadLength) throw new InvalidDataException("Missing upload byte owner.");

        byte[] before = upload.ToArray(), occupancy = new byte[GpuLength];
        foreach (var owner in owners)
        {
            ReadOnlySpan<byte> bytes = before.AsSpan(owner.Offset, owner.ByteLength);
            if (!string.Equals(Hash(bytes), owner.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("An upload owner's captured preimage changed.");
            if (owner.Offset >= GpuLength) continue;
            if (owner.Provenance == "fresh-zero-upload-background")
            {
                if (bytes.ContainsAnyExcept((byte)0)) throw new InvalidDataException("Fresh-free ownership requires actual zero background.");
            }
            else occupancy.AsSpan(owner.Offset, owner.ByteLength).Fill(1);
        }
        var allocation = NativeStaticTerrainGpuAllocation.Compile(before[..GpuLength], occupancy, selected);
        byte[] output = before.ToArray();
        allocation.CopyGpuPayload().CopyTo(output, 0);
        for (int i = 0; i < output.Length; i++)
            if ((i >= GpuLength || !allocation.IsAllocated(i)) && output[i] != before[i])
                throw new InvalidDataException("Texture composition changed its complete unallocated/protected/SPU complement.");
        return new(output, allocation.Records.ToArray(), allocation.Allocations.ToArray(),
            allocation.ContainedAliases.ToArray(), allocation.FreeByteCount);
    }

    /// <summary>
    /// Neutral current two-face byte grammar, not source authority for the tail.
    /// The private adapter supplies its actual immutable fresh component chain.
    /// </summary>
    public byte[] ComposeTwoFaceData(ReadOnlySpan<byte> originalData)
    {
        if (originalData.Length != 0x8C8 || Word(originalData, 0) != 8 || Word(originalData, 4) != 0 ||
            Word(originalData, 8) != 172 || Word(originalData, 12) != 1 || Word(originalData, 16) != 8 ||
            !originalData.Slice(0x24, 8).SequenceEqual(new byte[] { 4, 4, 2, 0, 4, 4, 2, 12 }) ||
            Word(originalData, 0x98) != 255 || Word(originalData, 0x9C) != 128 ||
            Word(originalData, 0xA8) != 255 || Word(originalData, 0xAC) != 128)
            throw new InvalidDataException("The bounded fresh untextured two-direct-triangle metadata/face preimages changed.");
        return NativeStaticTerrainGpuAllocation.ComposeTwoFaceMetadata(originalData, Records);
    }

    private static T[] Snapshot<T>(IEnumerable<T> values, int maximum) where T : class
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = new List<T>();
        foreach (T value in values)
        {
            if (result.Count == maximum || value is null) throw new InvalidDataException("Bounded non-null request enumeration exceeded its limit.");
            result.Add(value);
        }
        return result.ToArray();
    }
    private static bool Label(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 1024 && !value.Any(char.IsControl);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static uint Word(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static void Put(Span<byte> bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..], value);
}

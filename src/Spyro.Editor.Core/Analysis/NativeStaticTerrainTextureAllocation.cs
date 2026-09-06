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
        var captured = selected.Select(i => i with { Record = i.Record.ToArray(), SourceVram = i.SourceVram.ToArray() }).ToArray();
        var decoded = captured.Select(i => NativeStaticTerrainTextureLayout.Decode(i.Record)).ToArray();
        var fields = decoded.Select((layout, material) => layout.Descriptors.Select(d => new[]
        {
            R(material, "pixel", d.BitsPerPixel, d.PixelByteOffset, d.PixelRowByteLength, d.PixelRowCount),
            R(material, "palette", d.BitsPerPixel, d.PaletteByteOffset, d.PaletteRowByteLength, d.PaletteRowCount)
        }).ToArray()).ToArray();
        var unique = fields.SelectMany(a => a).SelectMany(a => a).Distinct().ToArray();
        // At most2*23*2 resources. Containment is source-coordinate/interpretation
        // identity, never content-hash or cross-material deduplication.
        for (int i = 0; i < unique.Length; i++)
            for (int j = 0; j < i; j++)
                if (SameDomain(unique[i], unique[j]) && Overlaps(unique[i], unique[j]) &&
                    !Contains(unique[i], unique[j]) && !Contains(unique[j], unique[i]))
                    throw new InvalidDataException("Partially overlapping source rectangles lack an admitted alias policy.");
        var requests = new List<Resource>(); var parents = new Dictionary<Resource, Resource>();
        foreach (var resource in unique.OrderByDescending(r => r.Width * r.Rows).ThenBy(r => r.Material)
            .ThenBy(r => r.Kind, StringComparer.Ordinal).ThenBy(r => r.Y).ThenBy(r => r.X).ThenBy(r => r.Bpp))
        {
            Resource? parent = requests.FirstOrDefault(r => Contains(r, resource));
            if (parent is null) requests.Add(resource); else parents.Add(resource, parent);
        }
        byte[] output = before.ToArray();
        var targets = new Dictionary<Resource, int>();
        var regions = new List<NativeStaticTerrainTextureAllocationRegion>();
        foreach (var r in requests.OrderBy(r => r.Kind != "palette").ThenByDescending(r => r.Width * r.Rows)
            .ThenBy(r => r.Material).ThenBy(r => r.Y).ThenBy(r => r.X).ThenBy(r => r.Bpp))
        {
            int target = Find(occupancy, r);
            if (target < 0) throw new InvalidDataException("No complete free texture/palette rectangle remains; partial reservations are discarded.");
            byte[] payload = new byte[r.Width * r.Rows];
            for (int row = 0; row < r.Rows; row++)
            {
                captured[r.Material].SourceVram.AsSpan(r.Offset + row * 1024, r.Width).CopyTo(payload.AsSpan(row * r.Width));
                payload.AsSpan(row * r.Width, r.Width).CopyTo(output.AsSpan(target + row * 1024));
                occupancy.AsSpan(target + row * 1024, r.Width).Fill(3);
            }
            targets.Add(r, target);
            var source = captured[r.Material];
            regions.Add(new(r.Material, source.SourceIdentity, source.AddressDomainIdentity, source.RelocationPolicy,
                r.Kind, r.Bpp, r.Offset, target, r.Width, r.Rows, Hash(payload)));
        }
        var aliases = new List<NativeStaticTerrainTextureContainedAlias>();
        foreach (var (child, parent) in parents.OrderBy(p => p.Key.Material).ThenBy(p => p.Key.Kind, StringComparer.Ordinal)
            .ThenBy(p => p.Key.Y).ThenBy(p => p.Key.X).ThenBy(p => p.Key.Bpp))
        {
            int target = targets[parent] + child.Offset - parent.Offset;
            targets.Add(child, target);
            var source = captured[child.Material];
            aliases.Add(new(child.Material, source.SourceIdentity, source.AddressDomainIdentity, source.RelocationPolicy,
                child.Kind, child.Bpp, child.Offset, target, parent.Offset, targets[parent], child.Width, child.Rows));
        }
        var records = new NativeStaticTerrainTextureLayout[2];
        for (int material = 0; material < 2; material++)
        {
            byte[] record = captured[material].Record.ToArray();
            for (int index = 0; index < 23; index++)
            {
                var pixel = fields[material][index][0]; var palette = fields[material][index][1];
                Rebase(record.AsSpan(index * 8, 8), index < 3, pixel.Rows, targets[pixel], targets[palette]);
            }
            records[material] = NativeStaticTerrainTextureLayout.Decode(record);
            for (int i = 0; i < 23; i++)
            {
                var source = decoded[material].Descriptors[i]; var final = records[material].Descriptors[i];
                if (source.BitsPerPixel != final.BitsPerPixel || source.TileSide != final.TileSide ||
                    !source.CopyPixels(captured[material].SourceVram).AsSpan().SequenceEqual(final.CopyPixels(output.AsSpan(0, GpuLength))) ||
                    !source.CopyPalette(captured[material].SourceVram).AsSpan().SequenceEqual(final.CopyPalette(output.AsSpan(0, GpuLength))))
                    throw new InvalidDataException("Reencoded descriptor did not preserve the complete source pixel/palette footprint.");
            }
        }
        for (int i = 0; i < output.Length; i++)
            if ((i >= GpuLength || occupancy[i] != 3) && output[i] != before[i])
                throw new InvalidDataException("Texture composition changed its complete unallocated/protected/SPU complement.");
        return new(output, records, regions.ToArray(), aliases.ToArray(), occupancy.Count(b => b == 0));
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
        byte[] output = new byte[0xA38];
        Put(output, 0, 0x178); Put(output, 4, 2);
        for (int material = 0; material < 2; material++)
        {
            byte[] record = Records[material].CopyRecord();
            record.AsSpan(0, 16).CopyTo(output.AsSpan(8 + material * 16));
            record.AsSpan(16, 168).CopyTo(output.AsSpan(40 + material * 168));
        }
        originalData[8..].CopyTo(output.AsSpan(0x178));
        Put(output, 0x208, 0); Put(output, 0x218, 1);
        for (int i = 8; i < originalData.Length; i++)
            if (i is not (>= 0x98 and < 0x9C) and not (>= 0xA8 and < 0xAC) && originalData[i] != output[i + 0x170])
                throw new InvalidDataException("Data metadata growth changed an unowned translated tail byte.");
        return output;
    }

    private sealed record Resource(int Material, string Kind, int Bpp, int X, int Y, int Width, int Rows)
    { internal int Offset => Y * 1024 + X; }
    private static Resource R(int material, string kind, int bpp, int offset, int width, int rows) =>
        new(material, kind, bpp, offset % 1024, offset / 1024, width, rows);
    private static bool SameDomain(Resource a, Resource b) => a.Material == b.Material && a.Kind == b.Kind && a.Bpp == b.Bpp;
    private static bool Contains(Resource a, Resource b) => SameDomain(a, b) && a.X <= b.X && a.Y <= b.Y &&
        a.X + a.Width >= b.X + b.Width && a.Y + a.Rows >= b.Y + b.Rows;
    private static bool Overlaps(Resource a, Resource b) => a.X < b.X + b.Width && b.X < a.X + a.Width &&
        a.Y < b.Y + b.Rows && b.Y < a.Y + a.Rows;
    private static int Find(byte[] occupied, Resource r)
    {
        int xStep = r.Kind == "palette" ? 32 : 16, yStep = r.Kind == "palette" && r.Bpp == 4 ? 4 : 1;
        for (int y = 0; y + r.Rows <= 512; y += yStep)
        {
            if (r.Kind == "pixel" && y % 256 + r.Rows > 256) continue;
            for (int x = 0; x + r.Width <= 1024; x += xStep)
            {
                bool free = true;
                for (int row = 0; row < r.Rows; row++)
                    if (occupied.AsSpan((y + row) * 1024 + x, r.Width).ContainsAnyExcept((byte)0)) { free = false; break; }
                if (free) return y * 1024 + x;
            }
        }
        return -1;
    }
    private static void Rebase(Span<byte> raw, bool low, int side, int pixel, int palette)
    {
        int x = pixel % 1024, y = pixel / 1024, px = palette % 1024, py = palette / 1024;
        int u = x % 128 * (low ? 2 : 1);
        raw[0] = checked((byte)u); raw[1] = checked((byte)(y % 256));
        raw[4] = checked((byte)(u + side - 1)); raw[5] = raw[1];
        raw[6] = checked((byte)((raw[6] & 0xE0) | (low ? 8 : 0x88) | (x / 128) | (y >= 256 ? 16 : 0)));
        if (low) { raw[2] = checked((byte)((px + 1024) / 32)); raw[3] = checked((byte)(py / 4)); }
        else BinaryPrimitives.WriteUInt16LittleEndian(raw[2..], checked((ushort)(py * 64 + (px + 1024) / 32)));
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

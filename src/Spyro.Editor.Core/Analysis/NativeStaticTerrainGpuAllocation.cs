using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// Internal neutral GPU-only mechanics. A binary occupied map is a caller
/// declaration, not source ownership or native/hardware admission. No SPU domain.
/// The old public adapter retains all source-label/partition/clip/hash checks.
/// </summary>
internal sealed class NativeStaticTerrainGpuAllocation
{
    private const int GpuLength = 0x80000;
    private readonly byte[] _gpu, _occupancy;
    private NativeStaticTerrainGpuAllocation(byte[] gpu, byte[] occupancy,
        NativeStaticTerrainTextureLayout[] records, NativeStaticTerrainTextureAllocationRegion[] regions,
        NativeStaticTerrainTextureContainedAlias[] aliases)
    {
        _gpu = gpu; _occupancy = occupancy;
        Records = Array.AsReadOnly(records); Allocations = Array.AsReadOnly(regions);
        ContainedAliases = Array.AsReadOnly(aliases);
        AllocatedByteCount = regions.Sum(r => r.RowByteLength * r.RowCount);
        FreeByteCount = occupancy.Count(b => b == 0);
    }
    internal IReadOnlyList<NativeStaticTerrainTextureLayout> Records { get; }
    internal IReadOnlyList<NativeStaticTerrainTextureAllocationRegion> Allocations { get; }
    internal IReadOnlyList<NativeStaticTerrainTextureContainedAlias> ContainedAliases { get; }
    internal int AllocatedByteCount { get; }
    internal int FreeByteCount { get; }
    internal byte[] CopyGpuPayload() => _gpu.ToArray();
    internal bool IsAllocated(int offset) => _occupancy[offset] == 3;

    internal static NativeStaticTerrainGpuAllocation Compile(byte[] gpu, byte[] occupied,
        NativeStaticTerrainTextureInput[] selected)
    {
        ArgumentNullException.ThrowIfNull(gpu);
        ArgumentNullException.ThrowIfNull(occupied);
        ArgumentNullException.ThrowIfNull(selected);
        if (gpu.Length != GpuLength || occupied.Length != GpuLength ||
            occupied.Any(b => b is not (0 or 1)))
            throw new InvalidDataException("GPU allocation requires one exact80000 plane and binary occupied map.");
        if (selected.Length != 2 || selected.Where((input, index) => input is null ||
            input.MaterialId != index || !Label(input.SourceIdentity) || !Label(input.AddressDomainIdentity) ||
            input.RelocationPolicy != NativeStaticTerrainTextureAllocation.RelocationPolicyId ||
            input.Record is null || input.Record.Length != 184 ||
            input.SourceVram is null || input.SourceVram.Length != GpuLength).Any())
            throw new InvalidDataException("GPU allocation requires two ordered, bounded neutral material inputs.");
        byte[] before = gpu.ToArray(), occupancy = occupied.ToArray();
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
            if (occupancy[i] != 3 && output[i] != before[i])
                throw new InvalidDataException("GPU allocation changed its complete unallocated/protected complement.");
        return new(output, occupancy, records, regions.ToArray(), aliases.ToArray());
    }

    // Neutral bounded metadata writer only. Each source adapter validates its
    // own full input grammar/ancestry before this call; this helper issues none.
    internal static byte[] ComposeTwoFaceMetadata(ReadOnlySpan<byte> originalData,
        IReadOnlyList<NativeStaticTerrainTextureLayout> records)
    {
        if (originalData.Length is not (0x8C8 or 0x8FC))
            throw new InvalidDataException("Metadata composition requires the bounded8C8 or8FC input extent.");
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count != 2 || records[0] is null || records[1] is null)
            throw new InvalidDataException("Metadata composition requires exactly two complete decoded records.");
        byte[] output = new byte[originalData.Length + 0x170];
        Put(output, 0, 0x178); Put(output, 4, 2);
        for (int material = 0; material < 2; material++)
        {
            byte[] record = records[material].CopyRecord();
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

    private static void Put(Span<byte> bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..], value);

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
    private static bool Label(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 1024 && !value.Any(char.IsControl);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

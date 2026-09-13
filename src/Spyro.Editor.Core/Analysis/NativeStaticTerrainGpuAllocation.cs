using System.Buffers.Binary;
using System.Security.Cryptography;
using Resource = Spyro.Editor.Core.Analysis.NativeStaticTerrainTextureResourceGraph.Resource;

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
        const int consumersPerRecord = NativeStaticTerrainTextureResourceGraph.ConsumersPerRecord;
        if (selected.Length == 0 || selected.Length > int.MaxValue / consumersPerRecord)
            throw new InvalidDataException("GPU allocation requires a nonempty ordered array of bounded neutral material inputs.");
        // Safety/work budgets grow with the complete input, not a material cap.
        // Per record at most46 nodes require at most two N-choose-2 scans:
        // overlap validation and first-containing-owner candidate search.
        var inputReferences = selected.ToArray();
        var graph = NativeStaticTerrainTextureResourceGraph.Compile(inputReferences, selected.Length * consumersPerRecord,
            (long)selected.Length * consumersPerRecord * (consumersPerRecord - 1));
        byte[] before = gpu.ToArray(), occupancy = occupied.ToArray();
        var captured = inputReferences.Select((i, material) => i with
            { Record = graph.Records[material].CopyRecord(), SourceVram = i.SourceVram.ToArray() }).ToArray();
        var decoded = graph.Records;
        byte[] output = before.ToArray();
        var targets = new Dictionary<Resource, int>();
        var regions = new List<NativeStaticTerrainTextureAllocationRegion>();
        foreach (var r in graph.Owners)
        {
            int target = Find(occupancy, r);
            if (target < 0) throw new InvalidDataException("No complete free texture/palette rectangle remains; partial reservations are discarded.");
            byte[] payload = new byte[r.RowByteLength * r.RowCount];
            for (int row = 0; row < r.RowCount; row++)
            {
                captured[r.MaterialId].SourceVram.AsSpan(r.SourceOffset + row * 1024, r.RowByteLength).CopyTo(payload.AsSpan(row * r.RowByteLength));
                payload.AsSpan(row * r.RowByteLength, r.RowByteLength).CopyTo(output.AsSpan(target + row * 1024));
                occupancy.AsSpan(target + row * 1024, r.RowByteLength).Fill(3);
            }
            targets.Add(r, target);
            regions.Add(new(r.MaterialId, r.SourceIdentity, r.AddressDomainIdentity, r.RelocationPolicy,
                r.Kind, r.BitsPerPixel, r.SourceOffset, target, r.RowByteLength, r.RowCount, Hash(payload)));
        }
        var aliases = new List<NativeStaticTerrainTextureContainedAlias>();
        foreach (var alias in graph.ContainedAliases)
        {
            var child = alias.Child; var parent = alias.Owner;
            int target = targets[parent] + alias.RelativeByteOffset;
            targets.Add(child, target);
            aliases.Add(new(child.MaterialId, child.SourceIdentity, child.AddressDomainIdentity, child.RelocationPolicy,
                child.Kind, child.BitsPerPixel, child.SourceOffset, target, parent.SourceOffset, targets[parent], child.RowByteLength, child.RowCount));
        }
        var records = new NativeStaticTerrainTextureLayout[captured.Length];
        for (int material = 0; material < captured.Length; material++)
        {
            byte[] record = captured[material].Record.ToArray();
            for (int index = 0; index < 23; index++)
            {
                int consumer = material * consumersPerRecord + index * 2;
                var pixel = graph.Consumers[consumer].Resource; var palette = graph.Consumers[consumer + 1].Resource;
                Rebase(record.AsSpan(index * 8, 8), index < 3, pixel.RowCount, targets[pixel], targets[palette]);
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
        NativeTerrainMaterialTableCodec.Encode([records[0], records[1]]).CopyTo(output, 0);
        originalData[8..].CopyTo(output.AsSpan(0x178));
        Put(output, 0x208, 0); Put(output, 0x218, 1);
        for (int i = 8; i < originalData.Length; i++)
            if (i is not (>= 0x98 and < 0x9C) and not (>= 0xA8 and < 0xAC) && originalData[i] != output[i + 0x170])
                throw new InvalidDataException("Data metadata growth changed an unowned translated tail byte.");
        return output;
    }

    private static void Put(Span<byte> bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..], value);

    private static int Find(byte[] occupied, Resource r)
    {
        int xStep = r.Kind == "palette" ? 32 : 16, yStep = r.Kind == "palette" && r.BitsPerPixel == 4 ? 4 : 1;
        for (int y = 0; y + r.RowCount <= 512; y += yStep)
        {
            if (r.Kind == "pixel" && y % 256 + r.RowCount > 256) continue;
            for (int x = 0; x + r.RowByteLength <= 1024; x += xStep)
            {
                bool free = true;
                for (int row = 0; row < r.RowCount; row++)
                    if (occupied.AsSpan((y + row) * 1024 + x, r.RowByteLength).ContainsAnyExcept((byte)0)) { free = false; break; }
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
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

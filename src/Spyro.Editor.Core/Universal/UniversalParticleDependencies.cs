using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Universal;

public sealed record UniversalParticleDependency(string SourceLevelKey, int TypeId, string Status, string Detail);

/// <summary>Particle IDs live in a level-local spawn/update dispatch and texture-pointer table. Reporting
/// these independently prevents Moby/model import success from being mistaken for particle closure.</summary>
public static class UniversalParticleDependencies
{
    public static UniversalParticleDependency Inspect(string sourceKey, int type, UniversalLevelData source,
        UniversalLevelData destination, bool sameLevel)
    {
        if (sameLevel) return new(sourceKey, type, "native", "Uses the destination's own particle type.");
        var sourceRecords = Records(source);
        var destRecords = Records(destination);
        if (sourceRecords.TryGetValue(type, out var src) && !destRecords.ContainsKey(type))
            return new(sourceKey, type, "texture-import-required", "Missing source-required texture record will be imported; per-ID spawn/update semantics remain unverified.");
        if (!sourceRecords.ContainsKey(type))
            return new(sourceKey, type, "dependency-unverified", "No source texture record; source/destination particle spawn/update dispatch equivalence has not been established.");
        if (!EquivalentTexture(src!, source.Vram(), destRecords[type], destination.Vram()))
            throw new InvalidDataException($"Particle dependency 0x{type:X2} from {sourceKey} conflicts with the destination texture/palette; per-source particle ID remapping is required.");
        return new(sourceKey, type, "dependency-unverified", src!.AsSpan().SequenceEqual(destRecords[type])
            ? "Particle texture descriptor matches; referenced pixels/palette and per-ID spawn/update behavior still need equivalence or import proof."
            : "Source and destination particle texture descriptors differ; per-ID texture and spawn/update behavior require separate verification.");
    }

    public static bool EquivalentTexture(byte[] a, ReadOnlySpan<byte> av, byte[] b, ReadOnlySpan<byte> bv)
    {
        if (a.Length != 8 || b.Length != 8) return false;
        int ap = BinaryPrimitives.ReadUInt16LittleEndian(a.AsSpan(6)), bp = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(6));
        int depth = (ap >> 7) & 3;
        if (depth == 3 || (ap & ~0x1F) != (bp & ~0x1F) || a[4] - a[0] != b[4] - b[0] || a[5] - a[1] != b[5] - b[1]) return false;
        int au = Math.Min(a[0], a[4]), bu = Math.Min(b[0], b[4]);
        if (depth == 0 && au % 2 != bu % 2) return false;
        int ax = depth == 0 ? au / 2 : au << (depth - 1), bx = depth == 0 ? bu / 2 : bu << (depth - 1);
        int maxA = Math.Max(a[0], a[4]), width = (depth == 0 ? maxA / 2 : (maxA << (depth - 1)) + (depth == 2 ? 1 : 0)) - ax + 1;
        int ay = Math.Min(a[1], a[5]), by = Math.Min(b[1], b[5]), height = Math.Abs(a[5] - a[1]) + 1;
        for (int row = 0; row < height; row++)
        {
            int ao = (((ap & 16) != 0 ? 256 : 0) + ay + row) * 1024 + (ap & 15) * 128 - 1024 + ax;
            int bo = (((bp & 16) != 0 ? 256 : 0) + by + row) * 1024 + (bp & 15) * 128 - 1024 + bx;
            if (ao < 0 || bo < 0 || ao + width > av.Length || bo + width > bv.Length || !av.Slice(ao, width).SequenceEqual(bv.Slice(bo, width))) return false;
        }
        if (depth == 2) return true;
        int ac = BinaryPrimitives.ReadUInt16LittleEndian(a.AsSpan(2)), bc = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(2));
        int acOff = (ac >> 6) * 1024 + (ac & 63) * 32 - 1024, bcOff = (bc >> 6) * 1024 + (bc & 63) * 32 - 1024;
        int length = depth == 0 ? 32 : 512;
        return acOff >= 0 && bcOff >= 0 && acOff + length <= av.Length && bcOff + length <= bv.Length && av.Slice(acOff, length).SequenceEqual(bv.Slice(bcOff, length));
    }

    public static byte[] TexturePackage(byte[] tile)
    {
        if (tile.Length != 8) throw new InvalidDataException("Particle texture payload must be exactly eight bytes for relocation.");
        byte[] pkg = new byte[0x80];
        BinaryPrimitives.WriteInt32LittleEndian(pkg, 1);
        Array.Fill(pkg, (byte)0xFF, 4, 16);
        BinaryPrimitives.WriteInt32LittleEndian(pkg.AsSpan(0x34), 0x64);
        BinaryPrimitives.WriteInt32LittleEndian(pkg.AsSpan(0x38), 0x40);
        BinaryPrimitives.WriteInt32LittleEndian(pkg.AsSpan(0x64), 20);
        BinaryPrimitives.WriteInt32LittleEndian(pkg.AsSpan(0x68), 2);
        tile.CopyTo(pkg, 0x70);
        pkg[0x78] = tile[0]; pkg[0x79] = tile[5]; pkg[0x7A] = tile[4]; pkg[0x7B] = tile[1];
        return pkg;
    }

    public static void AppendTexture(UniversalLevelData data, int type, byte[] relocatedPackage)
    {
        var existing = Records(data);
        if (existing.ContainsKey(type)) throw new InvalidDataException($"Particle dependency 0x{type:X2} already has a destination texture; it cannot be overwritten.");
        var layout = Locate(data);
        byte[] record = new byte[12];
        BinaryPrimitives.WriteUInt16LittleEndian(record, checked((ushort)type));
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(2), 8);
        relocatedPackage.AsSpan(0x70, 8).CopyTo(record.AsSpan(4));
        int sfOffset = data.Subfiles()[1].Offset;
        // The final sound descriptor may end in zero bytes that are still owned data.
        int usedByteLength = new NativeLevelSoundLayout(data.Bytes).SoundEnd - sfOffset;
        data.InsertWithinSubfile(1, layout.Offset + 8 + existing.Values.Sum(v => 4 + v.Length) - sfOffset, record, usedByteLength);
        BinaryPrimitives.WriteInt32LittleEndian(data.Bytes.AsSpan(layout.Offset), layout.Length + 12);
        BinaryPrimitives.WriteInt32LittleEndian(data.Bytes.AsSpan(layout.Offset + 4), layout.Count + 1);
    }

    public static IReadOnlyDictionary<int, byte[]> Records(UniversalLevelData data)
    {
        var layout = Locate(data);
        int cursor = layout.Offset + 8, end = layout.Offset + layout.Length;
        Dictionary<int, byte[]> records = [];
        for (int i = 0; i < layout.Count; i++)
        {
            if (cursor + 4 > end) throw new InvalidDataException("Particle texture record is truncated.");
            int type = BinaryPrimitives.ReadUInt16LittleEndian(data.Bytes.AsSpan(cursor));
            int payload = BinaryPrimitives.ReadUInt16LittleEndian(data.Bytes.AsSpan(cursor + 2));
            if (payload < 8 || (long)cursor + 4 + payload > end) throw new InvalidDataException("Particle texture payload is invalid.");
            if (!records.TryAdd(type, data.Bytes.AsSpan(cursor + 4, payload).ToArray()))
                throw new InvalidDataException($"Particle type 0x{type:X2} has ambiguous duplicate texture records.");
            cursor += 4 + payload;
        }
        return records;
    }

    private static (int Offset, int Length, int Count) Locate(UniversalLevelData data)
    {
        var (offset, length) = data.Subfiles()[1];
        byte[] bytes = data.Bytes.AsSpan(offset, length).ToArray();
        int U(int p)
        {
            if (p < 0 || p + 4 > bytes.Length) throw new InvalidDataException("Particle component header is outside level data.");
            return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(p));
        }
        void Advance(ref int p)
        {
            int n = U(p);
            if (n < 4 || (long)p + n > bytes.Length) throw new InvalidDataException("Particle component chain is invalid.");
            p += n;
        }
        int cursor = 0;
        for (int i = 0; i < 6; i++) Advance(ref cursor);
        int portals = U(cursor); cursor += 4;
        if (portals is < 0 or > 64) throw new InvalidDataException("Particle component portal count is invalid.");
        for (int i = 0; i < portals; i++)
        {
            int points = U(cursor + 4);
            if (points is < 1 or > 4096) throw new InvalidDataException("Particle component portal geometry is invalid.");
            cursor = checked(cursor + 0x40 + (points - 1) * 12);
            Advance(ref cursor);
        }
        int size = U(cursor), end = checked(cursor + size), count = U(cursor + 4);
        if (size < 8 || end > bytes.Length || count is < 0 or > 4096) throw new InvalidDataException("Particle texture component is invalid.");
        return (offset + cursor, size, count);
    }
}

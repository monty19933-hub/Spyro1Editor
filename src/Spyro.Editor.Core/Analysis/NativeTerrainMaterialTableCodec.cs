using System.Buffers.Binary;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// Neutral exact-component codec for ordered canonical static terrain records.
/// The native loader uses an8-byte length/count header, all16-byte LQ rows,
/// then all168-byte HQ rows. Record grammar remains owned by the existing decoder.
/// No face addressing, tail relocation, allocation, source or export authority.
/// Native destination/count restrictions belong to the source-bound adapters.
/// </summary>
internal static class NativeTerrainMaterialTableCodec
{
    private const int HeaderBytes = 8, LowBytes = 16, HighBytes = 168;

    internal static byte[] Encode(NativeStaticTerrainTextureLayout[] records)
    {
        ArgumentNullException.ThrowIfNull(records);
        long byteLength = HeaderBytes + (long)NativeStaticTerrainTextureLayout.RecordByteLength * records.Length;
        if (byteLength > Array.MaxLength)
            throw new InvalidDataException("Material table exceeds the representable managed byte-buffer length.");
        var captured = records.ToArray();
        if (captured.Any(record => record is null))
            throw new InvalidDataException("Material table requires every ordered complete decoded record.");
        byte[] output = new byte[(int)byteLength];
        BinaryPrimitives.WriteUInt32LittleEndian(output, (uint)output.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(4), (uint)captured.Length);
        int highStart = checked(HeaderBytes + LowBytes * captured.Length);
        for (int index = 0; index < captured.Length; index++)
        {
            byte[] record = captured[index].CopyRecord();
            record.AsSpan(0, LowBytes).CopyTo(output.AsSpan(HeaderBytes + index * LowBytes));
            record.AsSpan(LowBytes, HighBytes).CopyTo(output.AsSpan(highStart + index * HighBytes));
        }
        return output;
    }

    internal static IReadOnlyList<NativeStaticTerrainTextureLayout> Decode(ReadOnlySpan<byte> table)
    {
        if (table.Length < HeaderBytes)
            throw new InvalidDataException("Material table is shorter than its complete native header.");
        uint byteLength = BinaryPrimitives.ReadUInt32LittleEndian(table);
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(table[4..]);
        long expected = HeaderBytes + (long)NativeStaticTerrainTextureLayout.RecordByteLength * count;
        if (byteLength != table.Length || expected != table.Length)
            throw new InvalidDataException("Material table length/count must describe the exact complete component with no suffix.");
        // Exact input length proves these bounds before any record allocation;
        // no caller count can overflow, skip a row or absorb a following component.
        int materialCount = checked((int)count);
        int highStart = checked(HeaderBytes + LowBytes * materialCount);
        var records = new NativeStaticTerrainTextureLayout[materialCount];
        for (int index = 0; index < materialCount; index++)
        {
            byte[] record = new byte[NativeStaticTerrainTextureLayout.RecordByteLength];
            table.Slice(HeaderBytes + index * LowBytes, LowBytes).CopyTo(record);
            table.Slice(highStart + index * HighBytes, HighBytes).CopyTo(record.AsSpan(LowBytes));
            records[index] = NativeStaticTerrainTextureLayout.Decode(record);
        }
        return Array.AsReadOnly(records);
    }
}

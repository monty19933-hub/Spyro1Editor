using System.Buffers.Binary;

namespace Spyro.Editor.Core.Exporting;

internal sealed class NativeParticleDescriptorTable
{
    private readonly byte[] _bytes;
    internal NativeParticleDescriptorTable(ReadOnlySpan<byte> bytes, int[] ids)
    { _bytes = bytes.ToArray(); Ids = Array.AsReadOnly(ids.ToArray()); }
    internal int Count => Ids.Count;
    internal int ByteLength => _bytes.Length;
    internal IReadOnlyList<int> Ids { get; }
    internal byte[] CopyBytes() => _bytes.ToArray();
}

internal sealed class NativeParticleDescriptorAppend
{
    private readonly byte[] _component, _record;
    private NativeParticleDescriptorAppend(NativeParticleDescriptorTable original, ReadOnlySpan<byte> record)
    {
        ArgumentNullException.ThrowIfNull(original);
        int id = NativeParticleDescriptorCodec.RequireRecord(record);
        if (original.Count >= 64 || original.Ids.Contains(id))
            throw new InvalidDataException("Particle descriptor append cannot overwrite a resident selector or exceed its 64-entry nonaliasing namespace.");
        OriginalCount = original.Count; FinalCount = OriginalCount + 1;
        _record = record.ToArray(); _component = new byte[original.ByteLength + 12];
        original.CopyBytes().CopyTo(_component, 0); _record.CopyTo(_component, original.ByteLength);
        BinaryPrimitives.WriteUInt32LittleEndian(_component, (uint)_component.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(_component.AsSpan(4), (uint)FinalCount);
        _ = NativeParticleDescriptorCodec.Decode(_component);
    }
    internal int OriginalCount { get; }
    internal int FinalCount { get; }
    internal byte[] CopyComponent() => _component.ToArray();
    internal byte[] CopyInsertion() => _record.ToArray();
    internal static NativeParticleDescriptorAppend Build(NativeParticleDescriptorTable original, ReadOnlySpan<byte> record) => new(original, record);
}

internal static class NativeParticleDescriptorCodec
{
    // This is the known indexed-descriptor region 76278..76378, not the
    // 0x51 effect callback selectors or the 512-entry Moby class table.
    internal static NativeParticleDescriptorTable Decode(ReadOnlySpan<byte> component)
    {
        if (component.Length < 8 || component.Length > 8 + 64 * 12 || component.Length % 4 != 0 ||
            BinaryPrimitives.ReadUInt32LittleEndian(component) != component.Length)
            throw new InvalidDataException("Particle component length is not an exact bounded owner.");
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(component[4..]);
        if (count > 64 || component.Length != 8 + count * 12)
            throw new InvalidDataException("Particle count does not own its exact bounded native descriptor list.");
        int[] ids = new int[count]; HashSet<int> seen = [];
        for (int i = 0; i < ids.Length; i++)
        {
            ids[i] = RequireRecord(component.Slice(8 + i * 12, 12));
            if (!seen.Add(ids[i])) throw new InvalidDataException("Duplicate particle descriptors would overwrite the same native pointer-table slot.");
        }
        return new(component, ids);
    }
    internal static int RequireRecord(ReadOnlySpan<byte> record)
    {
        if (record.Length != 12) throw new InvalidDataException("Particle descriptor requires its complete 4-byte header plus supported 8-byte payload.");
        int id = BinaryPrimitives.ReadInt16LittleEndian(record), width = BinaryPrimitives.ReadInt16LittleEndian(record[2..]);
        if (id is < 0 or >= 64 || width != 8)
            throw new InvalidDataException("Particle descriptor signed ID/width is unsupported; ID64 aliases the player-model pointer, not another particle slot.");
        return id;
    }
}

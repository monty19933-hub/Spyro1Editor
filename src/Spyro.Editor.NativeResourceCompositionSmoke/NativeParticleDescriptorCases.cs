using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

internal static class NativeParticleDescriptorCases
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        byte[] component = Component([1, 3]);
        var table = NativeParticleDescriptorCodec.Decode(component);
        check(table.Count == 2 && table.ByteLength == 32 && table.Ids.SequenceEqual(new[] { 1, 3 }), "native particle size/count/ID grammar");
        check(table.CopyBytes().AsSpan().SequenceEqual(component), "exact particle roundtrip");
        var append = NativeParticleDescriptorAppend.Build(table, Record(7)); byte[] final = append.CopyComponent();
        check(append.OriginalCount == 2 && append.FinalCount == 3 && U(final, 0) == 44 && U(final, 4) == 3, "append owns size and count exactly once");
        check(final.AsSpan(8, 24).SequenceEqual(component.AsSpan(8)) && final.AsSpan(32).SequenceEqual(Record(7)), "old records retained verbatim and incoming record appended");
        check(NativeParticleDescriptorCodec.Decode(final).Ids.SequenceEqual(new[] { 1, 3, 7 }), "final descriptor keyed by actual selector, not ordinal");
        final[^1] ^= 1; component[^1] ^= 1;
        check(append.CopyComponent()[^1] != final[^1] && table.CopyBytes()[^1] != component[^1], "particle source and output copies detached");
        reject(() => NativeParticleDescriptorAppend.Build(table, Record(3)), "resident descriptor cannot be overwritten");
        var full = NativeParticleDescriptorCodec.Decode(Component(Enumerable.Range(0, 64).ToArray()));
        check(full.Count == 64 && full.Ids[^1] == 63, "bounded descriptor IDs end before player pointer at64");
        reject(() => NativeParticleDescriptorAppend.Build(full, Record(7)), "full descriptor namespace refuses append");
        reject(() => NativeParticleDescriptorCodec.Decode(Component([64])), "ID64 aliases global model root0 and is refused");
        reject(() => NativeParticleDescriptorCodec.Decode(Component([65535])), "signed negative descriptor ID refused");
        reject(() => NativeParticleDescriptorCodec.Decode(Component([1, 1])), "duplicate native table writes refused");
        foreach (int length in new[] { 0, 4, 12, 65535 })
        {
            byte[] malformed = Component([7]); W16(malformed, 10, length);
            reject(() => NativeParticleDescriptorCodec.Decode(malformed), "unknown/signed/truncated record width refused");
        }
        foreach (uint count in new uint[] { 0, 2, 65, uint.MaxValue })
        {
            byte[] malformed = Component([7]); W(malformed, 4, count);
            reject(() => NativeParticleDescriptorCodec.Decode(malformed), "count must own exact bounded record list");
        }
        byte[] shortOwner = Component([7]); W(shortOwner, 0, 16);
        reject(() => NativeParticleDescriptorCodec.Decode(shortOwner), "component size must equal complete owner");
        reject(() => NativeParticleDescriptorCodec.Decode(Component([7])[..^1]), "partial record refused");
        reject(() => NativeParticleDescriptorCodec.Decode(Component([7]).Concat(new byte[4]).ToArray()), "unowned trailing component bytes refused");
        reject(() => NativeParticleDescriptorAppend.Build(table, new byte[11]), "partial incoming descriptor refused");
        reject(() => NativeParticleDescriptorAppend.Build(table, Record(64)), "incoming descriptor cannot alias model namespace");
        check(NativeParticleDescriptorCodec.Decode(Component([])).Count == 0, "empty exact table remains representable");
    }

    internal static byte[] Component(int[] ids)
    {
        byte[] bytes = new byte[8 + ids.Length * 12]; W(bytes, 0, (uint)bytes.Length); W(bytes, 4, (uint)ids.Length);
        for (int i = 0; i < ids.Length; i++) Record(ids[i]).CopyTo(bytes, 8 + i * 12);
        return bytes;
    }
    internal static byte[] Record(int id)
    { byte[] b = [0, 0, 8, 0, 1, 2, 3, 4, 5, 6, 7, 8]; W16(b, 0, id); return b; }
    private static uint U(byte[] b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o));
    private static void W(byte[] b, int o, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o), v);
    private static void W16(byte[] b, int o, int v) => BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(o), (ushort)v);
}

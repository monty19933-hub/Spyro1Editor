using System.Buffers.Binary;
using System.Reflection;
using Spyro.Editor.Core.Analysis;
using static Program;

internal static class MaterialTableCases
{
    private delegate IReadOnlyList<NativeStaticTerrainTextureLayout> TableDecoder(ReadOnlySpan<byte> table);
    private delegate byte[] TwoFaceComposer(ReadOnlySpan<byte> data, IReadOnlyList<NativeStaticTerrainTextureLayout> records);

    internal static void Run()
    {
        // Run the complete old-byte oracle before the missing-codec RED.
        LegacyTwoFaceCases();
        Type? codec = typeof(NativeStaticTerrainTextureLayout).Assembly.GetType(
            "Spyro.Editor.Core.Analysis.NativeTerrainMaterialTableCodec");
        Check(codec is { IsPublic: false, IsAbstract: true, IsSealed: true },
            "Missing internal native material-table codec; encoding neutral rows cannot issue a native capability.");
        Check(codec!.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 0,
            "The table codec exposes no public factory or certificate.");
        var encode = codec.GetMethod("Encode", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<Func<NativeStaticTerrainTextureLayout[], byte[]>>();
        var decode = codec.GetMethod("Decode", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<TableDecoder>();

        foreach (int count in new[] { 0, 1, 3, 7 })
        {
            byte[][] raw = Enumerable.Range(0, count).Select(Record).ToArray();
            var records = raw.Select(b => NativeStaticTerrainTextureLayout.Decode(b)).ToArray();
            byte[] expected = TableOracle(raw);
            byte[] encoded = encode(records);
            Check(encoded.SequenceEqual(expected),
                "Complete ordered table matches the independent8+184*N, LQ16*N then HQ168*N layout.");
            Check(BinaryPrimitives.ReadUInt32LittleEndian(encoded) == encoded.Length &&
                BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(4)) == count,
                "Header length links exactly to the next component, and count selects the correct HQ base.");
            var decoded = decode(expected);
            Check(decoded.Count == count && Enumerable.Range(0, count).All(i =>
                decoded[i].CopyRecord().SequenceEqual(raw[i]) && decoded[i].Descriptors.Count == 23),
                "Decoding reassembles every complete record in table order, preserving all descriptor bytes.");
            Check(encode(decoded.ToArray()).SequenceEqual(expected), "An exact table roundtrip has no discarded suffix or row.");
            byte[] saved = expected.ToArray();
            if (count > 0)
            {
                expected[8] ^= 2;
                byte[] copy = decoded[^1].CopyRecord(); copy[0] ^= 2;
                records[^1] = NativeStaticTerrainTextureLayout.Decode(Record(0));
            }
            encoded[0] ^= 1;
            Check(encode(decoded.ToArray()).SequenceEqual(saved),
                "Caller bytes, input-array replacement and returned record copies cannot mutate the decoded result.");
            try
            {
                ((IList<NativeStaticTerrainTextureLayout>)decoded).Add(NativeStaticTerrainTextureLayout.Decode(Record(0)));
                throw new InvalidDataException("Expected immutable decoded collection.");
            }
            catch (NotSupportedException) { Check(true, "Decoded table order cannot be changed through its collection."); }
        }

        var same = NativeStaticTerrainTextureLayout.Decode(Record(0));
        Check(decode(encode([same, same, same])).Count == 3,
            "Repeated equal records remain separate ordered table slots; the codec never deduplicates.");
        // This is a byte-codec check only. Native face addressing and destination
        // resource capacity remain separate adapter restrictions.
        byte[] neutralLarge = encode(Enumerable.Repeat(same, 129).ToArray());
        Check(neutralLarge.Length == 8 + 184 * 129 && decode(neutralLarge).Count == 129,
            "Neutral table encoding adds no arbitrary replacement material-count cap or export eligibility.");
        bool nullRefused = false;
        try { encode(null!); }
        catch (ArgumentNullException) { nullRefused = true; }
        Check(nullRefused, "Null finite input array refuses.");
        Reject(() => encode([same, null!]), "A null row cannot be skipped or truncated.");
        byte[] valid = TableOracle([Record(0), Record(1), Record(2)]);
        foreach (int length in new[] { 0, 1, 7, 8, valid.Length - 1 })
            Reject(() => decode(valid.AsSpan(0, length)), "Missing header or incomplete declared table refuses before record extraction.");
        Reject(() => decode(valid.Concat(new byte[] { 0 }).ToArray()), "A table codec cannot silently discard another component's suffix.");
        foreach (var (offset, value) in new[] { (0, 0u), (0, 8u), (0, uint.MaxValue),
            (4, 0u), (4, 2u), (4, 4u), (4, 0x80000000u), (4, uint.MaxValue) })
        {
            byte[] bad = valid.ToArray(); BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(offset), value);
            string before = Hash(bad);
            Reject(() => decode(bad), "Inconsistent or overflowing length/count cannot allocate or lose table rows.");
            Check(Hash(bad) == before, "A rejected table's complete input remains unchanged.");
        }
        byte[] alias = valid.ToArray(); alias[8 + 3 * 16] ^= 2;
        Reject(() => decode(alias), "LQ/HQ-leading alias corruption remains a complete-record grammar refusal.");
        byte[] orientation = valid.ToArray(); orientation[8 + 3 * 16 + 8 + 7] = 1;
        Reject(() => decode(orientation), "Unsupported native descriptor orientation is not admitted by table decoding.");
        byte[] crossedRows = valid.ToArray();
        valid.AsSpan(8 + 3 * 16 + 168, 168).CopyTo(crossedRows.AsSpan(8 + 3 * 16));
        Reject(() => decode(crossedRows), "An HQ row from another ordered slot cannot silently bind to the wrong LQ aliases.");
    }

    private static void LegacyTwoFaceCases()
    {
        Type kernel = typeof(NativeStaticTerrainTextureLayout).Assembly.GetType(
            "Spyro.Editor.Core.Analysis.NativeStaticTerrainGpuAllocation")!;
        var compose = kernel.GetMethod("ComposeTwoFaceMetadata", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<TwoFaceComposer>();
        byte[][] originals = [Record(0), Record(1)];
        foreach (byte[][] rows in new[] { originals, originals.Reverse().ToArray(), new[] { originals[0], originals[0] } })
        foreach (int length in new[] { 0x8C8, 0x8FC })
        {
            byte[] before = Enumerable.Range(0, length).Select(i => (byte)(i * 29 + i / 17)).ToArray();
            byte[] captured = before.ToArray(), expected = new byte[length + 0x170];
            TableOracle(rows).CopyTo(expected, 0);
            captured.AsSpan(8).CopyTo(expected.AsSpan(0x178));
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(0x208), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(0x218), 1);
            var layouts = rows.Select(r => NativeStaticTerrainTextureLayout.Decode(r)).ToArray();
            byte[] actual = compose(before, layouts);
            Check(actual.SequenceEqual(expected) && before.SequenceEqual(captured),
                "Both old extents retain exact170 growth, all translated tail bytes and fixed face0/1 links for ordered and duplicate pairs.");
        }
        var record = NativeStaticTerrainTextureLayout.Decode(Record(0));
        foreach (var rows in new[] { Array.Empty<NativeStaticTerrainTextureLayout>(), new[] { record },
            new[] { record, record, record }, new[] { record, null! } })
            ExactRefusal(() => compose(new byte[0x8FC], rows), "Metadata composition requires exactly two complete decoded records.");
        ExactRefusal(() => compose(new byte[0x8FD], null!), "Metadata composition requires the bounded8C8 or8FC input extent.");
    }

    private static byte[] Record(int index)
    {
        byte[] bytes = new byte[184];
        for (int descriptor = 0; descriptor < 23; descriptor++)
        {
            bool low = descriptor < 3;
            byte[] raw = Convert.FromHexString(low ? "000020201f000880" : "004020281f408880");
            raw[0] = checked((byte)(index * (low ? 2 : 8)));
            raw[4] = checked((byte)(raw[0] + 31));
            raw.CopyTo(bytes, descriptor * 8);
        }
        return bytes;
    }

    private static byte[] TableOracle(byte[][] records)
    {
        byte[] result = new byte[8 + 184 * records.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(result, (uint)result.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), (uint)records.Length);
        for (int i = 0; i < records.Length; i++)
        {
            records[i].AsSpan(0, 16).CopyTo(result.AsSpan(8 + 16 * i));
            records[i].AsSpan(16, 168).CopyTo(result.AsSpan(8 + 16 * records.Length + 168 * i));
        }
        return result;
    }

    private static void ExactRefusal(Action action, string message)
    {
        try { action(); }
        catch (InvalidDataException error) { Check(error.Message == message, "The two-face adapter preserves its exact refusal order."); return; }
        throw new InvalidDataException("Expected adapter refusal.");
    }
}

using System.Buffers.Binary;
using System.Collections;
using System.Reflection;
using Spyro.Editor.Core.Analysis;
using static Program;

// Pure detached data only. No source issuer, GPU allocation, saved document,
// scene/body/candidate or publication gate is added by this test.
internal static class TiledMaterialDataCases
{
    private delegate byte[] Composer(ReadOnlySpan<byte> data, IEnumerable<NativeStaticTerrainTextureLayout> records,
        IEnumerable<int> faces, int clip);
    private static Composer _compose = null!;

    internal static void Run()
    {
        byte[] input = Data();
        Check(input.Length == 0xA58 && Hash(input.AsSpan(8, 0x2D4)) ==
            "809c2442de920c36fe2bb97db36abd82b2e5888322c95bee51e661c1408ec3d1",
            "Independent three-tile fixture retains the frozen canonical surface; its patterned tail has no source authority.");
        Check(Hash(input) == "541d82249eca0cb69c5a31e51fee294e9c5739e1ff9f50d5e1838f34f71e8949",
            "Complete neutral input matches the Python oracle frozen before production execution.");
        Type? type = typeof(NativeStaticTerrainTextureLayout).Assembly.GetType(
            "Spyro.Editor.Core.Exporting.NativeId65TiledMaterialDataLayout");
        Check(type is { IsPublic: false, IsAbstract: true, IsSealed: true },
            "Missing internal neutral tiled material-data composer; no public native capability is implied.");
        Check(type!.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 0,
            "Neutral tiled composition exposes no public factory or approval flags.");
        var method = type.GetMethod("Compose", BindingFlags.NonPublic | BindingFlags.Static);
        Check(method != null && method.ReturnType == typeof(byte[]) && method.GetParameters().Select(p => p.ParameterType)
            .SequenceEqual(new[] { typeof(ReadOnlySpan<byte>), typeof(IEnumerable<NativeStaticTerrainTextureLayout>),
                typeof(IEnumerable<int>), typeof(int) }), "Exact data/records/ordered-face/clip method, not arbitrary output offsets.");
        _compose = method!.CreateDelegate<Composer>();
        var records = new[] { Record(0), Record(1) };
        string[] goldens = ["d879542a2349aa43090ca12d20962d19d79271d0dcbf17fd99b73d36ce7fee2f",
            "6d4bd727ac9fbe9e45a773557d7d101fb1dc066e1045ba6b99cc9421f0bb8c6e",
            "758204003ead368f31140991d990c15d716a8232fac32cf01637de04dacb5b16",
            "c319099d04f864274fc55e2796fcdd274956e261b2e616a3a1830df1b8ce04d5"];
        int fixtureIndex = 0;
        foreach (int[] faces in new[] { new[] { 0, 1, 0, 1, 0, 1 }, new[] { 1, 0, 1, 0, 1, 0 },
            new[] { 255, 1, 0, 255, 1, 0 }, Enumerable.Repeat(255, 6).ToArray() })
            Exact(input, records, faces, goldens[fixtureIndex++]);
        Exact(input, records.Reverse().ToArray(), [0, 1, 1, 0, 0, 1], "ff4ca301eb98af2b8594d6a6fc9c4d3ea88a9e61cfbdba49378e74fa10dc4ce4");
        Exact(input, [Record(0), Record(0)], [0, 1, 0, 1, 0, 1], "cc80a320ebbc877d8c958c91e387b48ae285af867ebd8713fa8ca086ff2d6d5e");
        Exact(input, [], Enumerable.Repeat(255, 6).ToArray(), "541d82249eca0cb69c5a31e51fee294e9c5739e1ff9f50d5e1838f34f71e8949");
        foreach (int count in new[] { 1, 3, 7 })
            Exact(input, Enumerable.Range(0, count).Select(Record).ToArray(),
                Enumerable.Range(0, 6).Select(i => i == 5 ? count - 1 : i % count).ToArray(), count switch
                {
                    1 => "dbd8a944da1f9fee91a820c3f21826f238f4d4ad0410d0b5dbf4cfe6fd4cc5e3",
                    3 => "85c56b9a0d219f98e01e37f747581a46834a468a26157b4551f35ae1d5471a6e",
                    _ => "858138c128eb0e8f57c8449e9eb72c715631c031ce5701e44d771357cbeb3b04"
                });
        Invalid(() => _compose(input, Enumerable.Range(0, 8).Select(i => NativeStaticTerrainTextureLayout.Decode(Record(i))),
            [0, 1, 2, 3, 4, 7], 0x8000), "Eight complete records produce1018 despite only six faces; no unused metadata row is silently dropped.");
        Refusals(input, records);
        Boundaries(records);
        Enumeration(input, records);
        Console.WriteLine("NATIVE_TILED_MATERIAL_DATA_FREE_OK sourceRead=false nativeOutput=false sourceAuthority=false");
    }

    private static void Exact(byte[] input, byte[][] rawRecords, int[] faces, string? golden = null)
    {
        byte[] before = input.ToArray(); int[] faceCopy = faces.ToArray();
        var records = rawRecords.Select(r => NativeStaticTerrainTextureLayout.Decode(r)).ToArray();
        byte[] expected = Oracle(before, rawRecords, faces), output = _compose(input, records, faces, 0x8000);
        int growth = rawRecords.Length * 184;
        if (golden != null) Check(Hash(expected) == golden && Hash(output) == golden,
            "Full independent Python output hash, not a production-learned digest.");
        Check(output.SequenceEqual(expected) && output.Length == before.Length + growth &&
            U(output, 0) == 8 + growth && U(output, 4) == rawRecords.Length,
            "Complete data equals independent separated LQ/HQ metadata plus ordered six-face translation.");
        Check(input.SequenceEqual(before) && faces.SequenceEqual(faceCopy), "Successful composition preserves every caller input byte and binding.");
        int[] offsets = FaceOffsets(before);
        if (before.Length == 0xA58)
            Check(offsets.SequenceEqual(new[] { 0xA0, 0xB0, 0x140, 0x150, 0x1E0, 0x1F0 }),
                "All six material owners derive from the validated sector directory, not old98/A8 offsets.");
        var owned = offsets.SelectMany(at => Enumerable.Range(at, 4)).ToHashSet();
        Check(Enumerable.Range(8, before.Length - 8).Where(at => !owned.Contains(at))
            .All(at => before[at] == output[at + growth]),
            "Every translated non-material byte is exact: environment directories/vertices/colors/flags, visibility, collision, sky, portal, particles and sound.");
        for (int i = 0; i < offsets.Length; i++)
            Check(U(output, offsets[i] + growth) == faces[i] && U(output, offsets[i] + growth + 4) == 0x80,
                "Each ordered face retains exact direct-LQ flag80 and its unmasked requested material or FF sentinel.");
        for (int i = 0; i < rawRecords.Length; i++)
        {
            byte[] restored = output.AsSpan(8 + i * 16, 16).ToArray()
                .Concat(output.AsSpan(8 + rawRecords.Length * 16 + i * 168, 168).ToArray()).ToArray();
            Check(restored.SequenceEqual(rawRecords[i]) && NativeStaticTerrainTextureLayout.Decode(restored).Descriptors.Count == 23,
                "Every ordered complete184-byte record reconstructs with all23 descriptor owners; repeated rows are not deduplicated.");
        }
        byte[] inverse = new byte[before.Length]; W(inverse, 0, 8);
        output.AsSpan(8 + growth).CopyTo(inverse.AsSpan(8));
        foreach (int at in offsets) W(inverse, at, 255);
        Check(inverse.SequenceEqual(before), "Removing only emitted metadata and restoring exact material owners restores the complete neutral input.");
        if (rawRecords.Length == 0) Check(output.SequenceEqual(before), "Zero records/allFF is an exact byte roundtrip.");
        byte[] repeat = _compose(input, records, faces, 0x8000);
        output.AsSpan().Fill(0xCC); input[8] ^= 1; faces[0] ^= 1;
        if (records.Length > 0) records[0] = NativeStaticTerrainTextureLayout.Decode(Record(3));
        Check(repeat.SequenceEqual(expected), "Result arrays, later caller bytes and record-array replacement cannot mutate a prior detached result.");
        before.CopyTo(input, 0); faceCopy.CopyTo(faces, 0);
        Check(_compose(input, rawRecords.Select(r => NativeStaticTerrainTextureLayout.Decode(r)), faces, 0x8000).SequenceEqual(expected),
            "A fresh repeated call is independent of all earlier caller/result mutations.");
    }

    private static void Refusals(byte[] input, byte[][] raw)
    {
        var records = raw.Select(r => NativeStaticTerrainTextureLayout.Decode(r)).ToArray(); int[] faces = [0, 1, 0, 1, 0, 1];
        foreach (int clip in new[] { 0, 1, 129, 0x7FFF, 0x8001, -1, int.MaxValue })
            Invalid(() => _compose(input, records, faces, clip), "Only source-default clip8000 has the existing direct-LQ palette-row contract.");
        foreach (int index in new[] { 0, 5 })
            foreach (int id in new[] { -1, -129, 2, 127, 128, 254, 256, 257, int.MinValue, int.MaxValue })
            {
                int[] bad = faces.ToArray(); bad[index] = id;
                Invalid(() => _compose(input, records, bad, 0x8000), "First/last face ID is validated, never masked, clamped or dropped.");
            }
        foreach (int count in new[] { 0, 1, 5, 7 })
            Invalid(() => _compose(input, records, faces.Concat(new[] { 0 }).Take(count), 0x8000), "Exactly two bindings per actual tile are required.");
        Invalid(() => _compose(input, [], faces, 0x8000), "A material binding cannot address an empty table.");
        Invalid(() => _compose(input, [records[0], null!], faces, 0x8000), "Null table rows cannot disappear.");
        Null(() => _compose(input, null!, faces, 0x8000)); Null(() => _compose(input, records, null!, 0x8000));
        foreach (int length in new[] { 0, 7, input.Length - 1, input.Length - 4 })
            Invalid(() => _compose(input.AsSpan(0, length), records, faces, 0x8000), "Truncated metadata/components/tail refuse before unsafe slicing.");
        Invalid(() => _compose(input.Concat(new byte[4]).ToArray(), records, faces, 0x8000), "No trailing padding or extra component may be discarded.");
        int[] faceOffsets = FaceOffsets(input);
        foreach (int at in new[] { 0, 4, 8, 12, 16, 20, 24, 28, 28 + 16, 28 + 156,
            348 + 8, 348 + 76, 508, 524, 548, 580, 716, 732, 736, 740, 864, 1124 }
            .Concat(faceOffsets).Concat(faceOffsets.Select(at => at + 4)))
        {
            byte[] bad = input.ToArray(); bad[at] ^= 1; byte[] before = bad.ToArray();
            Invalid(() => _compose(bad, records, faces, 0x8000), "Malformed complete input, including the unselected third tile, refuses.");
            Check(bad.SequenceEqual(before), "Refusal preserves the complete malformed caller input.");
        }
        foreach (int flag in new[] { 0, 1, 0x81, 0x180 })
        {
            byte[] bad = input.ToArray(); W(bad, faceOffsets[^1] + 4, flag);
            Invalid(() => _compose(bad, records, faces, 0x8000), "No normal/HQ/close or added fade flag is silently admitted.");
        }
        byte[] alreadyTextured = Oracle(input, raw, faces);
        Invalid(() => _compose(alreadyTextured, records, faces, 0x8000), "Already-textured metadata is not silently overwritten or recast as untextured ancestry.");
    }

    private static void Boundaries(byte[][] records)
    {
        foreach (var (cell, used) in new[] { (56, 0xFFC), (58, 0x1000), (60, 0x1004) })
        {
            byte[] data = Data(tinyZ: cell); int[] faces = Enumerable.Range(0, 16).Select(i => i % 2).ToArray();
            Check(data.Length + 0x170 == used, "Eight-tile actual tree/component lengths independently establish the metadata-growth boundary.");
            if (used <= 0x1000) Exact(data, records, faces, used == 0xFFC
                ? "7049763cda5a3083d8de5a7b43bbc71246e463a4db1f537b389e0ed19cc13fcf"
                : "ec75ec96ca1c76003f36e0a8e12d5f7c1254792893949a8c1c98a020dc1dcf67");
            else Invalid(() => _compose(data, records.Select(r => NativeStaticTerrainTextureLayout.Decode(r)), faces, 0x8000),
                "Complete valid geometry above1000 refuses without truncation, padding substitution or smaller projected floor.");
        }
    }

    private static void Enumeration(byte[] input, byte[][] raw)
    {
        var records = raw.Select(r => NativeStaticTerrainTextureLayout.Decode(r)).ToArray(); int[] faces = [0, 1, 0, 1, 0, 1];
        byte[] expected = Oracle(input, raw, faces);
        Check(_compose(input, new HostileCollection<NativeStaticTerrainTextureLayout>(() => records),
            new HostileCollection<int>(() => faces), 0x8000).SequenceEqual(expected),
            "Caller Count, indexer and CopyTo are not trusted; both inputs are captured by bounded enumeration.");
        int recordMoves = 0, faceMoves = 0, disposed = 0;
        IEnumerable<NativeStaticTerrainTextureLayout> ExcessRecords()
        {
            try { for (int i = 0; i < 129; i++) { recordMoves++; yield return records[0]; } throw new Exception("Record budget overread."); }
            finally { disposed++; }
        }
        Invalid(() => _compose(input, ExcessRecords(), faces, 0x8000), "Independent native128-record enumeration bound.");
        Check(recordMoves <= 129 && disposed == 1, "Excess record iteration terminates and disposes within its own finite budget.");
        IEnumerable<int> ExcessFaces()
        {
            try { for (int i = 0; i < 7; i++) { faceMoves++; yield return i % 2; } throw new Exception("Face budget overread."); }
            finally { disposed++; }
        }
        Invalid(() => _compose(input, records, ExcessFaces(), 0x8000), "Face enumeration stops at actual face count plus one rejected element.");
        Check(faceMoves == 7 && disposed == 2, "Excess face iteration does not trust declared collection size or continue indefinitely.");
        byte[] mutableData = input.ToArray();
        IEnumerable<NativeStaticTerrainTextureLayout> MutatingRecords()
        { mutableData[8] ^= 1; foreach (var record in records) yield return record; }
        Check(_compose(mutableData, MutatingRecords(), faces, 0x8000).SequenceEqual(expected) && mutableData[8] != input[8],
            "Complete strict data is captured before invoking a caller-owned record enumerator; later caller mutation is neither adopted nor repaired.");
        mutableData = input.ToArray(); var mutableRecords = records.ToArray();
        IEnumerable<int> MutatingFaces()
        {
            mutableData[348 + 8] ^= 1; mutableRecords[0] = records[1];
            foreach (int face in faces) yield return face;
        }
        Check(_compose(mutableData, mutableRecords, MutatingFaces(), 0x8000).SequenceEqual(expected),
            "Face enumeration cannot swap the already captured complete data or ordered immutable record references.");
        Check(_compose(input, records, faces, 0x8000).SequenceEqual(expected), "Failed/hostile requests leave no shared composition state.");
    }

    private static byte[] Oracle(byte[] input, byte[][] records, int[] faces)
    {
        int growth = 184 * records.Length; byte[] output = new byte[input.Length + growth];
        W(output, 0, 8 + growth); W(output, 4, records.Length);
        for (int i = 0; i < records.Length; i++)
        { records[i].AsSpan(0, 16).CopyTo(output.AsSpan(8 + i * 16)); records[i].AsSpan(16).CopyTo(output.AsSpan(8 + records.Length * 16 + i * 168)); }
        input.AsSpan(8).CopyTo(output.AsSpan(8 + growth));
        int[] offsets = FaceOffsets(input);
        for (int i = 0; i < offsets.Length; i++) W(output, offsets[i] + growth, faces[i]);
        return output;
    }

    private static int[] FaceOffsets(byte[] data)
    {
        int environment = U(data, 0), count = U(data, environment + 4);
        return Enumerable.Range(0, count).SelectMany(i =>
        {
            int sector = environment + 4 + U(data, environment + 8 + i * 4);
            Check(data.AsSpan(sector + 16, 8).SequenceEqual(Convert.FromHexString("040402000404020c")),
                "Independent fixture sector contract has4/4/2 tables and exact12 header-extra bytes.");
            return new[] { sector + 132, sector + 148 };
        }).ToArray();
    }

    private static byte[] Record(int index)
    {
        byte[] record = new byte[184];
        for (int i = 0; i < 23; i++)
        {
            byte[] descriptor = Convert.FromHexString(i < 3 ? "000020201f000880" : "004020281f408880");
            descriptor[0] = (byte)(index * (i < 3 ? 2 : 8)); descriptor[4] = (byte)(descriptor[0] + 31);
            descriptor.CopyTo(record, i * 8);
        }
        return record;
    }

    // Independent fixture bytes, not a call into the producer being tested.
    private static byte[] Data(int? tinyZ = null)
    {
        int count = tinyZ == null ? 3 : 8, width = tinyZ == null ? 128 : 1;
        int y = tinyZ == null ? 528 : 0, z = tinyZ == null ? 544 : tinyZ.Value * 256;
        byte[] environment = new byte[8 + count * 164]; W(environment, 0, environment.Length); W(environment, 4, count);
        List<byte[]> triangles = [];
        for (int i = 0; i < count; i++)
        {
            int x = tinyZ == null ? 528 + i * 128 : i, sector = 8 + count * 4 + i * 160;
            W(environment, 8 + i * 4, sector - 4);
            W(environment, sector, (x + width / 2) << 16 | (y + width / 2));
            W(environment, sector + 4, z << 16 | width * 2); W(environment, sector + 8, x << 16 | y); W(environment, sector + 12, z << 16);
            Convert.FromHexString("040402000404020c").CopyTo(environment, sector + 16); W(environment, sector + 24, -1);
            int[] vertices = [0, width << 21, width << 10, width << 21 | width << 10];
            for (int v = 0; v < 4; v++)
            {
                W(environment, sector + 28 + v * 4, vertices[v]); W(environment, sector + 76 + v * 4, vertices[v]);
                foreach (int colors in new[] { 44, 92, 108 }) W(environment, sector + colors + v * 4, 0x708050);
            }
            for (int face = 0; face < 2; face++)
            {
                int a = face, b = face == 0 ? 1 : 3, c = 2;
                int low = a << 26 | b << 20 | c << 14 | c << 8, high = a | a << 8 | b << 16 | c << 24;
                W(environment, sector + 60 + face * 8, low); W(environment, sector + 64 + face * 8, low);
                W(environment, sector + 124 + face * 16, high); W(environment, sector + 128 + face * 16, high);
                W(environment, sector + 132 + face * 16, 255); W(environment, sector + 136 + face * 16, 128);
            }
            triangles.Add(Words(x | width << 23, y | width << 14, z,
                x + width | (-width & 511) << 14, y | width << 14 | width << 23, z));
        }
        int list = Align(count + 1), skyVisibility = 16 + list;
        byte[] visibility = new byte[skyVisibility + 12]; W(visibility, 0, visibility.Length); W(visibility, 4, skyVisibility - 4);
        W(visibility, 8, 1); W(visibility, 12, 12); visibility.AsSpan(16, list).Fill(255);
        for (int i = 0; i < count; i++) visibility[16 + i] = (byte)i;
        W(visibility, skyVisibility, 1); W(visibility, skyVisibility + 4, 8); visibility.AsSpan(skyVisibility + 8, 4).Fill(255);
        byte[] tree, blocks;
        if (tinyZ == null)
        { tree = Shorts(3, 65535, 65535, 8, 3, 65535, 65535, 16, 4, 65535, 65535, 0, 4, 0); blocks = Shorts(0x8003, 2, 1, 0, 0x8005, 4, 3, 2, 0x8000, 0); }
        else
        {
            int zLength = (tinyZ.Value + 2) * 2; tree = new byte[Align(zLength + 8)];
            H(tree, 0, tinyZ.Value + 1);
            for (int i = 0; i < tinyZ.Value; i++) H(tree, 2 + i * 2, 65535);
            H(tree, 2 + tinyZ.Value * 2, zLength); H(tree, zLength, 1); H(tree, zLength + 2, zLength + 4); H(tree, zLength + 4, 1);
            blocks = new byte[Align(count * 4 + 2)];
            for (int i = 0; i < count * 2; i++) H(blocks, i * 2, count * 2 - 1 - i | (i == 0 ? 0x8000 : 0));
            H(blocks, count * 4, 0x8000);
        }
        int triangleAt = 32 + tree.Length + blocks.Length, assignmentAt = triangleAt + count * 24, flagsAt = assignmentAt + Align(count * 2);
        byte[] collision = new byte[flagsAt + Align(count * 2)];
        int[] header = [collision.Length, count * 2, count * 2, 28, 28 + tree.Length, triangleAt - 4, assignmentAt - 4, flagsAt - 4];
        for (int i = 0; i < header.Length; i++) W(collision, i * 4, header[i]);
        tree.CopyTo(collision, 32); blocks.CopyTo(collision, 32 + tree.Length);
        for (int i = 0; i < count; i++) triangles[i].CopyTo(collision, triangleAt + i * 24);
        collision.AsSpan(flagsAt, count * 2).Fill(63);
        byte[] surface = environment.Concat(visibility).Concat(Words(8, 0)).Concat(collision).Concat(Words(16, 0x406080, 0, 0)).ToArray();
        byte[] tail = Enumerable.Range(0, 0x77C).Select(i => (byte)(i * 29 + i / 17)).ToArray();
        W(tail, 0, 0); W(tail, 4, 0x80); W(tail, 8, 10); W(tail, 0x84, 0x6F8); W(tail, 0x188, 76);
        return Words(8, 0).Concat(surface).Concat(tail).ToArray();
    }

    private static int Align(int value) => (value + 3) & ~3;
    private static int U(byte[] data, int at) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at));
    private static void W(byte[] data, int at, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(at), value);
    private static void H(byte[] data, int at, int value) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(at), checked((ushort)value));
    private static byte[] Words(params int[] values) { byte[] bytes = new byte[values.Length * 4]; for (int i = 0; i < values.Length; i++) W(bytes, i * 4, values[i]); return bytes; }
    private static byte[] Shorts(params int[] values) { byte[] bytes = new byte[values.Length * 2]; for (int i = 0; i < values.Length; i++) H(bytes, i * 2, values[i]); return bytes; }
    private static void Invalid(Action action, string label)
    { try { action(); } catch (InvalidDataException) { Check(true, label); return; } throw new InvalidDataException("Expected InvalidDataException: " + label); }
    private static void Null(Action action)
    { try { action(); } catch (ArgumentNullException) { Check(true, "Missing neutral enumeration refuses explicitly."); return; } throw new InvalidDataException("Expected ArgumentNullException."); }
    private sealed class HostileCollection<T>(Func<IEnumerable<T>> values) : IReadOnlyList<T>, ICollection<T>
    {
        public int Count => throw new Exception("Caller Count cannot grant a budget.");
        public T this[int index] => throw new Exception("Caller indexer cannot replace bounded enumeration.");
        public bool IsReadOnly => true;
        public IEnumerator<T> GetEnumerator() => values().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void CopyTo(T[] array, int index) => throw new Exception("Caller CopyTo is not a snapshot.");
        public void Add(T item) => throw new NotSupportedException(); public void Clear() => throw new NotSupportedException();
        public bool Contains(T item) => throw new NotSupportedException(); public bool Remove(T item) => throw new NotSupportedException();
    }
}

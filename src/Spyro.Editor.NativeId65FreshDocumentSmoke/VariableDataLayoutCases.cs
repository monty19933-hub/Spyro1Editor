using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

// Opt-in only. These tests do not publish or widen a source-neutral intent's authority.
internal static class VariableDataLayoutCases
{
    private const string OriginalHash = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const long OriginalLength = 661547040;
    private static int _checks;

    internal static void SourceFree()
    {
        _checks = 0;
        var lengths = new HashSet<int>();
        for (int sum = 0; sum <= 189; sum++)
        {
            int x = Math.Min(63, sum), y = Math.Min(63, sum - x), z = sum - x - y;
            lengths.Add(Surface(x, y, z));
        }
        Check(lengths.Count == 96 && lengths.Min() == 0x8BC && lengths.Max() == 0xA38,
            "All190 representable cell sums encode96 distinct four-byte-aligned data lengths.");
        foreach (var (x, y, z) in new[] { (0, 0, 1), (0, 1, 0), (1, 0, 0),
            (1, 1, 3), (1, 3, 1), (3, 1, 1), (2, 2, 2), (2, 2, 3), (2, 3, 2), (3, 2, 2) })
            Surface(x, y, z);
        var baseline = NativeId65AuthoredSurfaceLayout.Compile(new(8448, 8448, 8704, 1024, 1024, 0x708050, 0x406080));
        Check(Hash(baseline.CopyBundle()) == "ee82c4d3bb2589eb821028337499d062762c47d3f6af2267f6b0fabee8df58b9",
            "Complete retained starter surface golden stays exact.");
        Check(Surface(63, 63, 63) == 0xA38,
            "Valid untextured geometry can have the SAME used length as the fixed textured profile; length is not identity.");
        foreach (var (x, y, z) in new[] { (4080, 0, 0), (0, 4080, 0), (262128, 0, 0), (0, 0, 261632) })
            Reject(() => Intent(x, y, z), "Cross-cell closed edge or out-of-range derived spawn remains refused.");
        Check(Intent(4064, 4064, 261616).SpawnZ == 262128,
            "Last valid closed-cell extent and maximum aligned floorZ preserve native spawn bounds.");
        foreach (int length in new[] { 0, 0x8B8, 0x8BD, 0xA3C, int.MaxValue })
            Reject(() => NativeId65NormalDataLayout.RequireBoundedUsedLength(length), "Malformed used length refuses without allocation.");
        byte[] canonical = NeutralData(baseline.CopyBundle());
        foreach (int offset in new[] { 0, 4, 8, 180, 212, 220, 244, 332, 336, 340, 464, 724 })
        {
            byte[] bad = canonical.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(offset), int.MaxValue);
            Reject(() => NativeId65NormalDataLayout.DecodeUntextured(bad), "Corrupted component length/count/pointer cannot set the live end.");
        }
        byte[] texturedShape = canonical.Concat(new byte[0x170]).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(texturedShape, 0x178);
        BinaryPrimitives.WriteInt32LittleEndian(texturedShape.AsSpan(4), 2);
        Reject(() => NativeId65NormalDataLayout.DecodeUntextured(texturedShape),
            "A38 alone cannot turn texture metadata into the canonical untextured profile.");
        Console.WriteLine($"NATIVE_ID65_VARIABLE_DATA_FREE_OK checks={_checks} cellSums=190 distinctUsedLengths=96 nativeCpuExecuted=false");
    }

    internal static async Task RequireOriginCompilation(string original)
    {
        SourceFree();
        original = Path.GetFullPath(original);
        RequireOriginal(original, "before");
        try
        {
            var starter = NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 1024, 1024,
                8960, 8960, 64, new(80, 128, 112), new(128, 96, 64), 19);
            var retained = await NativeId65FreshDocumentCompilation.CaptureAsync(original, starter);
            Check(Hash(retained.DiscPlan.CopyBody()) == "b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c",
                "Actual complete retained starter body remains byte-exact before the new case.");
            Check(retained.DiscPlan.BodyPlanSha256 == "48f1f0b4f7740c8a6277a4b440990e1fd26cb609db2153c3c24fc61d70dbbb94",
                "Retained source-bound body plan identity is unchanged, not only its emitted bytes.");
            Console.WriteLine("VARIABLE_DATA_RETAINED_STARTER_BODY_OK");
            var origin = Intent(0, 0, 0);
            Console.WriteLine($"VARIABLE_DATA_CAPTURE_BEGIN floor=0,0,0 extent=16,16 spawn=1,1,512 canonical={Hash(NativeId65FreshDocumentCodec.SerializeCanonical(origin))}");
            // This must really succeed after implementation. Do not convert the
            // present production refusal into an expected-pass assertion.
            var compiled = await NativeId65FreshDocumentCompilation.CaptureAsync(original, origin);
            Check(ReferenceEquals(compiled.Intent, origin), "Actual issued compilation keeps the authored origin intent.");
            Check(Hash(compiled.DiscPlan.CopyBody()) == "055a138680475d075299fd1d722cdfa6d26d25dbf1ece1c3ba2ca9e5303c0134",
                "Complete origin-zero body matches the independent preimplementation oracle.");
            var maximum = await NativeId65FreshDocumentCompilation.CaptureAsync(original, Intent(63 * 4096, 63 * 4096, 63 * 4096));
            Check(Hash(maximum.DiscPlan.CopyBody()) == "3c69036d98cc9734a936f9904ad7ffdd453f5376fe602bec3106a722f0cfdee9",
                "Complete maximum-cell body matches the independent preimplementation oracle.");
            FullPhysical(original, compiled.DiscPlan, "0c3821141e50c74aee91d4ff4eeb7ef8632c3cd63daab624dd4d504d9ed83db2", retained.DiscPlan);
            FullPhysical(original, maximum.DiscPlan, "40d8cf3fb4168bdd870c8d6f73c2805e357160fa37486205f1b813b743855c2d", retained.DiscPlan);
            foreach (var (cx, cy, cz) in new[] { (0, 0, 1), (1, 1, 3), (2, 2, 2), (2, 2, 3) })
            {
                var alternative = await NativeId65FreshDocumentCompilation.CaptureAsync(original, Intent(cx * 4096, cy * 4096, cz * 4096));
                byte[] body = alternative.DiscPlan.CopyBody();
                int used = 0x8B0 + ((2 * (cx + cy + cz + 6) + 3) & ~3);
                Check(NativeId65NormalDataLayout.DecodeUntextured(body.AsSpan(0xDE800, used)).UsedByteLength == used,
                    "Actual intermediate cell-sum compilation retains its full canonical data chain.");
            }
        }
        catch (Exception error)
        {
            Console.WriteLine("VARIABLE_DATA_ACTUAL_FAILURE " + error);
            throw;
        }
        finally { RequireOriginal(original, "after"); }
        Console.WriteLine($"NATIVE_ID65_VARIABLE_DATA_SOURCE_OK checks={_checks} fullImages=2 sourceWritten=false");
    }

    private static int Surface(int cx, int cy, int cz)
    {
        var intent = Intent(cx * 4096, cy * 4096, cz * 4096);
        var floor = NativeId65AuthoredFloor.FromDisplayColors(intent.FloorX, intent.FloorY, intent.FloorZ,
            intent.FloorWidth, intent.FloorDepth, intent.FloorColor, intent.SkyColor);
        var bundle = NativeId65AuthoredSurfaceLayout.Compile(floor);
        byte[] bytes = bundle.CopyBundle(), collision = bundle.CopyComponent(NativeId65SurfaceComponentKind.Collision);
        Check(!bundle.SourceCertified && NativeId65AuthoredSurfaceLayout.DecodeAndValidate(bytes) == floor,
            "Canonical roundtrip is neutral and preserves the actual authored coordinates.");
        int tree = (2 * (cx + cy + cz + 6) + 3) & ~3;
        Check(bytes.Length == 300 + tree && collision.Length == 72 + tree, "Variable tree determines complete component length.");
        Check(Word(collision, 12) == 28 && Word(collision, 16) == 28 + tree &&
            Word(collision, 20) == 36 + tree && Word(collision, 24) == 60 + tree && Word(collision, 28) == 64 + tree,
            "All five native pointer fields retain their collision+4 basis and exact target owners.");
        int cursor = 0;
        foreach (int cell in new[] { cz, cy, cx })
        {
            Check(Half(collision, 32 + cursor) == cell + 1, "Actual selected axis is inside the declared native count.");
            for (int skipped = 0; skipped < cell; skipped++)
                Check(Half(collision, 34 + cursor + 2 * skipped) == 0xFFFF, "Every preceding cell remains an absent sentinel.");
            int pointer = Half(collision, 34 + cursor + 2 * cell);
            int next = cursor + 2 * (cell + 2);
            Check(pointer == (next == 2 * (cx + cy + cz + 6) ? 0 : next),
                "Native axis pointers reach the next owned segment or group0, never a synthetic zero/null interpretation.");
            cursor = next;
        }
        Check(Half(collision, 32 + tree) == 0x8001 && Half(collision, 34 + tree) == 0 &&
            Half(collision, 36 + tree) == 0x8000 && Half(collision, 38 + tree) == 0,
            "Reachable group0 retains both triangle indices and the independent following guard group.");
        int used = bytes.Length + 8 + 4 + 0x80 + 0x6F8;
        Check(used == 0x8B0 + tree && used is >= 0x8BC and <= 0xA38 && (used & 3) == 0 &&
            ((used + 0x7FF) & ~0x7FF) == 0x1000, "Complete metadata/surface/portal/particle/sound chain stays within one fixed CD allocation.");
        var layout = NativeId65NormalDataLayout.DecodeUntextured(NeutralData(bytes));
        Check(layout.UsedByteLength == used && layout.SurfaceByteLength == bytes.Length &&
            layout.CollisionTreeByteLength == tree && layout.SectorByteLength == 0x1000,
            "Neutral production layout derives actual canonical component lengths without source authority.");
        return used;
    }

    private static byte[] NeutralData(byte[] surface)
    {
        // Synthetic tail headers test only the neutral walk, never sound assets
        // or private source issuance. Real-source cases use actual captures.
        byte[] data = new byte[surface.Length + 0x784];
        BinaryPrimitives.WriteInt32LittleEndian(data, 8);
        surface.CopyTo(data, 8);
        int particles = surface.Length + 12, sound = particles + 0x80;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(particles), 0x80);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(particles + 4), 10);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(sound), 0x6F8);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(sound + 0x104), 76);
        return data;
    }

    private static void FullPhysical(string original, NativeId65FreshDiscPlan disc, string expected,
        NativeId65FreshDiscPlan starter)
    {
        using var source = File.OpenRead(original);
        using var forward = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var inverse = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var legacy = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] before = new byte[2352];
        int changed = 0;
        for (int lba = 0; lba < 281270; lba++)
        {
            source.ReadExactly(before);
            byte[] after = disc.CreateExpectedRawSector(lba, before), restored = disc.CreateOriginalRawSector(lba, after);
            Check(before.SequenceEqual(restored), "Actual full inverse preserves overwritten EXE/Form2 and untouched sectors.");
            forward.AppendData(after); inverse.AppendData(restored);
            legacy.AppendData(starter.CreateExpectedRawSector(lba, before));
            if (!before.SequenceEqual(after)) changed++;
        }
        Check(source.ReadByte() == -1 && source.Position == OriginalLength && source.Length == OriginalLength,
            "Actual full-image source EOF remains exact.");
        Check(Convert.ToHexStringLower(forward.GetHashAndReset()) == expected && changed == 1253,
            "Complete variable-tree afterimage matches independent raw oracle.");
        Check(Convert.ToHexStringLower(inverse.GetHashAndReset()) == OriginalHash &&
            Convert.ToHexStringLower(legacy.GetHashAndReset()) == "1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188",
            "Actual original inverse and complete retained starter afterimage remain byte-exact.");
        Check(!disc.RuntimeVerified && !disc.PublicationApproved && !disc.NormalCreateBinEnabled &&
            !disc.CompleteNativeDependencyClosureProven, "New data geometry supplies no runtime or publisher approval.");
        Console.WriteLine($"VARIABLE_DATA_PHYSICAL_OK sha256={expected} changed={changed} actualInverse=true physicalOutputWritten=false");
    }

    private static NativeId65FreshDocumentIntent Intent(int x, int y, int z) =>
        NativeId65FreshDocumentIntent.Create(x, y, z, 16, 16, x + 1, y + 1, 0,
            new ColorRgba(80, 128, 112), new ColorRgba(128, 96, 64), 2);
    private static int Word(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
    private static int Half(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); _checks++; }
    private static void Reject(Action action, string label)
    {
        try { action(); }
        catch (Exception error) when (error is InvalidDataException or ArgumentException) { _checks++; return; }
        throw new InvalidDataException("Expected refusal: " + label);
    }
    private static void RequireOriginal(string path, string phase)
    {
        using var input = File.OpenRead(path);
        Check(input.Length == OriginalLength, "Exact original length.");
        string hash = Convert.ToHexStringLower(SHA256.HashData(input));
        Check(hash == OriginalHash && input.Position == OriginalLength && input.Length == OriginalLength &&
            input.ReadByte() == -1 && input.Position == OriginalLength && input.Length == OriginalLength,
            "Whole original hash and real EOF before/after source capture.");
        Console.WriteLine($"VARIABLE_DATA_ORIGINAL_{phase.ToUpperInvariant()} sha256={hash} eof=true length={input.Length}");
    }
}

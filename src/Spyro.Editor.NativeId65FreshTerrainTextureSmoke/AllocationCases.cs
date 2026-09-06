using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using static Program;

internal static class AllocationCases
{
    internal const string Policy = "native-canonical-static-terrain-right-half-vram-v1";
    internal const string Free = "fresh-zero-upload-background";
    internal const string Protected = "retained-fixed-Town-presentation-VRAM";
    internal const string Detached = "issued-detached-companion-VRAM";
    internal const string Audio = "explicit-retained-audio-baseline";

    internal static void Run()
    {
        Check(typeof(NativeId65FreshTerrainTexturePlan).IsSealed &&
            typeof(NativeId65FreshTerrainTexturePlan).GetConstructors().Length == 0,
            "Actual fresh/source composition cannot be manufactured by a public certificate constructor.");
        byte[] upload = new byte[0xDE000];
        for (int i = 0x80000; i < upload.Length; i++) upload[i] = (byte)(i * 13);
        byte[] mask = new byte[0x80000];
        // The all-zero protected rectangle is earlier than valid free storage.
        Hole(mask, 0, 0, 16, 32, 1);
        Hole(mask, 32, 0, 32, 16, 2);
        var inputs = Inputs();
        var owners = Owners(upload, mask);
        byte[] originalUpload = upload.ToArray(), originalMask = mask.ToArray();
        var plan = NativeStaticTerrainTextureAllocation.Compile(upload, owners, inputs, 0x8000);
        Check(!plan.SourceCertified && !plan.RuntimeVerified && !plan.PublicationApproved,
            "Public neutral bytes/ownership do not grant native source, runtime or publication authority.");
        Check(plan.Records.Count == 2 && plan.Records.All(r => r.Descriptors.Count == 23),
            "Every test goes through both complete184-byte/23-descriptor records.");
        Check(plan.Allocations.Count == 8 && plan.AllocatedByteCount == 5120,
            "Two distinct synthetic source domains retain four physical owners each, even with equal source bytes.");
        VerifyRecords(inputs, plan);
        byte[] emitted = plan.CopyUploadPayload();
        Check(upload.SequenceEqual(originalUpload) && mask.SequenceEqual(originalMask), "Compilation is non-mutating.");
        Check(emitted.AsSpan(0x80000).SequenceEqual(upload.AsSpan(0x80000)), "Entire SPU domain is excluded, not merely nonzero samples.");
        VerifyComplement(upload, emitted, mask, plan.Allocations);
        byte[] copied = plan.CopyUploadPayload(); copied[0] ^= 1;
        Check(plan.CopyUploadPayload().SequenceEqual(emitted), "Returned complete upload is detached.");
        byte[] recordCopy = plan.Records[0].CopyRecord(); recordCopy[0] ^= 1;
        Check(!recordCopy.SequenceEqual(plan.Records[0].CopyRecord()), "Returned complete record is detached.");
        inputs[0].Record[0] ^= 1; inputs[0].SourceVram[0] ^= 1;
        Check(plan.CopyUploadPayload().SequenceEqual(emitted), "Input byte arrays cannot mutate an accepted plan.");
        inputs = Inputs();
        var retry = NativeStaticTerrainTextureAllocation.Compile(upload, owners.Reverse(), inputs.Reverse(), 0x8000);
        Check(retry.CopyUploadPayload().SequenceEqual(emitted) && retry.Records.Select(r => r.RecordSha256)
            .SequenceEqual(plan.Records.Select(r => r.RecordSha256)), "Ordering and a fresh retry produce the same material0/1 result.");

        foreach (int clip in new[] { 0, 1, 129, 0x7FFF, 0x8001, -1, int.MaxValue })
            Reject(() => NativeStaticTerrainTextureAllocation.Compile(upload, owners, inputs, clip),
                "Sixteen-row LQ palette requires exact admitted native clip0x8000, not arbitrary clip arithmetic.");
        foreach (int length in new[] { 0, 0x80000, 0xDDFFF, 0xDE001 })
            Reject(() => NativeStaticTerrainTextureAllocation.Compile(new byte[length], owners, inputs, 0x8000), "Complete bounded GPU plus SPU owner length before copy.");
        foreach (int length in new[] { 0, 183, 185 })
            Reject(() => NativeStaticTerrainTextureAllocation.Compile(upload, owners,
                new[] { inputs[0] with { Record = new byte[length] }, inputs[1] }, 0x8000), "Exact184-byte source record before clone.");
        foreach (int length in new[] { 0, 0x7FFFF, 0x80001 })
            Reject(() => NativeStaticTerrainTextureAllocation.Compile(upload, owners,
                new[] { inputs[0] with { SourceVram = new byte[length] }, inputs[1] }, 0x8000), "Source GPU cannot include SPU or omit last byte.");
        Reject(() => NativeStaticTerrainTextureAllocation.Compile(upload, owners, inputs.Take(1), 0x8000), "Exactly two material inputs required.");
        Reject(() => NativeStaticTerrainTextureAllocation.Compile(upload, owners,
            new NativeStaticTerrainTextureInput[] { inputs[0], null! }, 0x8000), "Null input is rejected before material sorting or payload cloning.");
        Reject(() => NativeStaticTerrainTextureAllocation.Compile(upload,
            new NativeId65NormalResourceOwner[] { null! }, inputs, 0x8000), "Null owner is rejected before owner sorting or payload cloning.");
        Reject(() => NativeStaticTerrainTextureAllocation.Compile(upload, owners, new LyingCountCollection<NativeStaticTerrainTextureInput>(TooManyInputs(inputs[0])), 0x8000), "Bounded enumeration independent of reported collection Count.");
        Reject(() => NativeStaticTerrainTextureAllocation.Compile(upload, owners,
            new[] { inputs[0], inputs[1] with { MaterialId = 0 } }, 0x8000), "Material IDs exactly0 and1, no duplicate or missing face.");
        foreach (var bad in new[] { inputs[0] with { MaterialId = 2 }, inputs[0] with { SourceIdentity = "" },
            inputs[0] with { AddressDomainIdentity = "" }, inputs[0] with { RelocationPolicy = "different-policy" },
            inputs[0] with { SourceIdentity = new string('x', 1025) } })
            Reject(() => NativeStaticTerrainTextureAllocation.Compile(upload, owners, new[] { bad, inputs[1] }, 0x8000), "Invalid identity/domain/policy is not silently content-deduplicated.");
        var sameHashDifferentDomain = NativeStaticTerrainTextureAllocation.Compile(upload, owners,
            new[] { inputs[0], inputs[1] with { SourceIdentity = inputs[0].SourceIdentity } }, 0x8000);
        Check(sameHashDifferentDomain.Allocations.Count == 8, "Same source label and payload under different address domains remains independently allocated.");
        byte[] partial = inputs[0].Record.ToArray(); partial[32] = 16; partial[36] = 47;
        Reject(() => NativeStaticTerrainTextureAllocation.Compile(upload, owners,
            new[] { inputs[0] with { Record = partial }, inputs[1] }, 0x8000),
            "Two same-source32px records overlapping by16px without full containment cannot acquire independent aliases.");

        foreach (var badOwners in new[]
        {
            owners.Skip(1).ToArray(), owners.Append(owners[0]).ToArray(),
            owners.Select((o, i) => i == 0 ? o with { Sha256 = new string('0', 64) } : o).ToArray(),
            owners.Select((o, i) => i == 0 ? o with { Provenance = "unknown-free-owner" } : o).ToArray(),
            owners.Select((o, i) => i == 0 ? o with { Domain = NativeId65ResourceDomain.ModelPayload } : o).ToArray(),
            owners.Select((o, i) => i == 0 ? o with { Offset = int.MaxValue } : o).ToArray(),
            owners.Select((o, i) => i == 0 ? o with { ByteLength = int.MaxValue } : o).ToArray(),
            owners.Select((o, i) => i == 0 ? o with { ByteLength = 0 } : o).ToArray()
        }) Reject(() => NativeStaticTerrainTextureAllocation.Compile(upload, badOwners, inputs, 0x8000), "Gaps, overlap, stale hashes, unknown provenance and domain overflow refuse.");
        byte[] dirtyFree = upload.ToArray(); dirtyFree[0x10000] = 1;
        Reject(() => NativeStaticTerrainTextureAllocation.Compile(dirtyFree, Owners(dirtyFree, mask), inputs, 0x8000), "Caller cannot label nonzero bytes fresh-zero storage.");
        Reject(() => NativeStaticTerrainTextureAllocation.Compile(upload, TooManyOwners(owners[0]), inputs, 0x8000), "Owner enumeration has an independent finite budget.");
        foreach (var badOwners in new[]
        {
            owners.Select(o => o.Provenance == Free ? o with { SourceWadOffset = 0x119E800L + o.Offset } : o).ToArray(),
            owners.Select(o => o.Provenance == Protected ? o with { SourceWadOffset = null } : o).ToArray(),
            owners.Select(o => o.Provenance == Audio ? o with { Provenance = Free, SourceWadOffset = null } : o).ToArray(),
            owners.Select(o => o.Provenance == Detached ? o with { SourceWadOffset = 0x119E800L + o.Offset } : o).ToArray()
        }) Reject(() => NativeStaticTerrainTextureAllocation.Compile(upload, badOwners, inputs, 0x8000),
            "Unknown source-address/provenance combinations or fresh-free SPU declarations do not grant storage.");
        var straddling = owners.ToArray();
        var lastGpu = straddling[^2]; var bank = straddling[^1];
        straddling[^2] = lastGpu with { ByteLength = lastGpu.ByteLength + 1,
            Sha256 = Hash(upload.AsSpan(lastGpu.Offset, lastGpu.ByteLength + 1)) };
        straddling[^1] = bank with { Offset = bank.Offset + 1, ByteLength = bank.ByteLength - 1,
            SourceWadOffset = bank.SourceWadOffset + 1, Sha256 = Hash(upload.AsSpan(bank.Offset + 1)) };
        Reject(() => NativeStaticTerrainTextureAllocation.Compile(upload, straddling, inputs, 0x8000),
            "Even exhaustive byte-hashed owners cannot straddle GPU and SPU address domains.");

        CapacityCases(upload);
        DataCases(plan);
    }

    private static void CapacityCases(byte[] upload)
    {
        // Every mask is reserved by default. Eight disjoint exact holes service
        // the SAME two complete-record path as the source-issued factory.
        byte[] ExactMask()
        {
            byte[] m = Enumerable.Repeat((byte)1, 0x80000).ToArray();
            foreach (int x in new[] { 0, 32 }) Hole(m, x, 0, 32, 16, 0);
            foreach (int y in new[] { 40, 41 }) Hole(m, 0, y, 512, 1, 0);
            foreach (int x in new[] { 0, 32 }) Hole(m, x, 64, 32, 32, 0);
            foreach (int x in new[] { 0, 16 }) Hole(m, x, 128, 16, 32, 0);
            return m;
        }
        var inputs = Inputs();
        byte[] m = ExactMask();
        var fit = NativeStaticTerrainTextureAllocation.Compile(upload, Owners(upload, m), inputs, 0x8000);
        Check(fit.AllocatedByteCount == 5120, "Exact eight-hole geometry fits complete two-record resources.");
        foreach (var cut in new[] { (0, 15, 32, 1), (511, 40, 1, 1), (0, 159, 16, 1) })
        {
            byte[] bad = ExactMask(); Hole(bad, cut.Item1, cut.Item2, cut.Item3, cut.Item4, 1);
            // Add many isolated free bytes: aggregate free count cannot repair a missing rectangle.
            for (int y = 300; y < 500; y += 2) Hole(bad, 800, y, 16, 1, 0);
            Reject(() => NativeStaticTerrainTextureAllocation.Compile(upload, Owners(upload, bad), inputs, 0x8000),
                "15/16 palette rows,511/512 CLUT bytes or31/32 pixel rows refuse despite aggregate slack.");
            Check(upload.AsSpan(0, 0x80000).ToArray().All(b => b == 0), "Late failure cannot publish earlier tentative writes into caller upload.");
        }
        byte[] crossing = ExactMask(); Hole(crossing, 0, 128, 32, 32, 1);
        Hole(crossing, 0, 240, 32, 32, 0);
        Reject(() => NativeStaticTerrainTextureAllocation.Compile(upload, Owners(upload, crossing), inputs, 0x8000), "Both free LQ rectangles crossing256-row page refuse.");
        byte[] edge = ExactMask(); Hole(edge, 0, 128, 32, 32, 1);
        Hole(edge, 992, 480, 32, 32, 0);
        var edgePlan = NativeStaticTerrainTextureAllocation.Compile(upload, Owners(upload, edge), inputs, 0x8000);
        Check(edgePlan.Allocations.Any(a => a.TargetOffset == 480 * 1024 + 1008), "Last legal16-byte column and final32 rows are usable.");
        var retry = NativeStaticTerrainTextureAllocation.Compile(upload, Owners(upload, m), inputs, 0x8000);
        Check(retry.CopyUploadPayload().SequenceEqual(fit.CopyUploadPayload()), "Retry after failures equals fresh success with no reservation leakage.");
    }

    private static void DataCases(NativeStaticTerrainTextureAllocation plan)
    {
        byte[] data = DataFixture();
        byte[] before = data.ToArray(), output = plan.ComposeTwoFaceData(data);
        Check(data.SequenceEqual(before) && output.Length == 0xA38, "Eight-byte header becomes178-byte metadata, shifting complete data by170.");
        Check(Word(output, 0) == 0x178 && Word(output, 4) == 2, "Native metadata length/count, not184-byte interleaving.");
        for (int i = 0; i < 2; i++)
        {
            byte[] record = plan.Records[i].CopyRecord();
            Check(output.AsSpan(8 + i * 16, 16).SequenceEqual(record.AsSpan(0, 16)) &&
                  output.AsSpan(40 + i * 168, 168).SequenceEqual(record.AsSpan(16)), "All LQ then all HQ records preserve184 bytes each.");
        }
        Check(Word(output, 0x208) == 0 && Word(output, 0x218) == 1, "Actual HP face0/1 receive distinct material0/1.");
        for (int i = 8; i < data.Length; i++)
            if (i is not (>= 0x98 and < 0x9C) and not (>= 0xA8 and < 0xAC))
                Check(data[i] == output[i + 0x170], "Every other translated data byte is exact, including zero owners.");
        foreach (int offset in new[] { 0, 4, 8, 12, 16, 0x24, 0x26, 0x28, 0x2A, 0x2B, 0x98, 0x9C, 0xA8, 0xAC })
        {
            byte[] bad = data.ToArray(); bad[offset] ^= 1;
            Reject(() => plan.ComposeTwoFaceData(bad), "Changed metadata/environment/face material/direct-triangle flag refuses.");
        }
        Reject(() => plan.ComposeTwoFaceData(data[..^1]), "Exact detached current fresh data length, not arbitrary body bytes.");
    }

    internal static byte[] DataFixture()
    {
        // Neutral shape fixture: caller bytes have no source authority. Patterned
        // tail makes whole-complement checks meaningful, not zero-only equality.
        byte[] result = Enumerable.Range(0, 0x8C8).Select(i => (byte)(i * 29 + i / 17)).ToArray();
        foreach (var (offset, value) in new[] { (0, 8), (4, 0), (8, 172), (12, 1), (16, 8),
            (0x98, 255), (0x9C, 128), (0xA8, 255), (0xAC, 128) }) Put(result, offset, value);
        Convert.FromHexString("040402000404020c").CopyTo(result, 0x24);
        return result;
    }
    internal static NativeStaticTerrainTextureInput[] Inputs() => Enumerable.Range(0, 2).Select(i =>
        new NativeStaticTerrainTextureInput(i, "fixture-source-" + i, "fixture-GPU-domain-" + i,
            Policy, Record(), Enumerable.Range(0, 0x80000).Select(p => (byte)(p * 17 + p / 1024)).ToArray())).ToArray();
    private static byte[] Record()
    {
        byte[] result = new byte[184];
        // Pixel4bpp(0,0) and CLUT(0,128); pixel8bpp(0,64), CLUT(0,160).
        for (int i = 0; i < 23; i++) Convert.FromHexString(i < 3 ? "000020201f000880" : "004020281f408880").CopyTo(result, i * 8);
        return result;
    }
    internal static NativeId65NormalResourceOwner[] Owners(byte[] upload, byte[] mask)
    {
        var owners = new List<NativeId65NormalResourceOwner>();
        for (int start = 0; start < mask.Length;)
        {
            int end = start + 1; while (end < mask.Length && mask[end] == mask[start]) end++;
            string provenance = mask[start] switch { 0 => Free, 1 => Protected, 2 => Detached, _ => throw new InvalidDataException() };
            owners.Add(new("fixture-" + start, NativeId65ResourceDomain.UploadPayload, start, end - start,
                Hash(upload.AsSpan(start, end - start)), provenance, mask[start] == 1 ? 0x119E800L + start : null));
            start = end;
        }
        owners.Add(new("fixture-SPU", NativeId65ResourceDomain.UploadPayload, 0x80000, 0x5E000,
            Hash(upload.AsSpan(0x80000)), Audio, 0x121E800));
        return owners.ToArray();
    }
    internal static void Hole(byte[] mask, int x, int y, int width, int rows, byte value)
    { for (int i = 0; i < rows; i++) mask.AsSpan((y + i) * 1024 + x, width).Fill(value); }
    internal static void VerifyRecords(NativeStaticTerrainTextureInput[] inputs, NativeStaticTerrainTextureAllocation plan)
    {
        byte[] output = plan.CopyUploadPayload()[..0x80000];
        for (int material = 0; material < 2; material++)
        {
            var before = NativeStaticTerrainTextureLayout.Decode(inputs.Single(i => i.MaterialId == material).Record);
            var after = NativeStaticTerrainTextureLayout.Decode(plan.Records[material].CopyRecord());
            byte[] source = inputs.Single(i => i.MaterialId == material).SourceVram;
            for (int descriptor = 0; descriptor < 23; descriptor++)
            {
                var a = before.Descriptors[descriptor]; var b = after.Descriptors[descriptor];
                Check(a.CopyPixels(source).SequenceEqual(b.CopyPixels(output)), "Every re-decoded descriptor selects exact complete source pixel rows.");
                Check(a.CopyPalette(source).SequenceEqual(b.CopyPalette(output)), "Every descriptor selects exact full CLUT bank, not one row.");
                byte[] x = a.CopyDescriptor(), y = b.CopyDescriptor();
                Check((x[6] & 0xE0) == (y[6] & 0xE0) && x[7] == y[7], "Rebasing retains material/mode bits, not just visual payload.");
            }
        }
    }
    internal static void VerifyComplement(byte[] before, byte[] after, byte[] mask,
        IReadOnlyList<NativeStaticTerrainTextureAllocationRegion> allocations)
    {
        byte[] owned = new byte[0x80000];
        foreach (var r in allocations)
            for (int row = 0; row < r.RowCount; row++)
                for (int x = 0; x < r.RowByteLength; x++)
                {
                    int at = r.TargetOffset + row * 1024 + x;
                    Check(at < 0x80000 && mask[at] == 0 && owned[at] == 0, "Typed allocation does not overlap protected, SPU or another allocation.");
                    owned[at] = 1;
                }
        Check(before.Where((b, i) => i >= 0x80000 || owned[i] == 0)
            .SequenceEqual(after.Where((b, i) => i >= 0x80000 || owned[i] == 0)), "Entire unallocated upload complement stays exact.");
    }
    private static IEnumerable<NativeStaticTerrainTextureInput> TooManyInputs(NativeStaticTerrainTextureInput input)
    {
        for (int i = 0; i < 3; i++) yield return input;
        throw new Exception("Input reader exceeded its independent two-request limit.");
    }
    private static IEnumerable<NativeId65NormalResourceOwner> TooManyOwners(NativeId65NormalResourceOwner owner)
    {
        for (int i = 0; i < 65537; i++) yield return owner;
        throw new Exception("Owner reader exceeded its independent finite limit.");
    }
    private sealed class LyingCountCollection<T>(IEnumerable<T> items) : IReadOnlyCollection<T>
    {
        public int Count => 1;
        public IEnumerator<T> GetEnumerator() => items.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
    internal static int Word(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
    internal static void Put(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
}

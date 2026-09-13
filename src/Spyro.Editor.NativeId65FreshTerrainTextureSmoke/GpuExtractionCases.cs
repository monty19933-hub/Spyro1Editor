using System.Reflection;
using System.Runtime.ExceptionServices;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using static Program;

internal static class GpuExtractionCases
{
    private const string Complete = "Two-record allocation requires the completeDE000 upload and exact admitted native clip8000.";
    private const string InputShape = "Material/source-domain/policy or complete record/GPU byte budget is invalid.";
    private const string OwnerHash = "An upload owner's captured preimage changed.";
    private const string DirtyFree = "Fresh-free ownership requires actual zero background.";
    private const string Capacity = "No complete free texture/palette rectangle remains; partial reservations are discarded.";
    private const string KernelInputShape = "GPU allocation requires a nonempty ordered array of bounded neutral material inputs.";
    private static Type? _kernel;

    internal static void Run()
    {
        // Executes through the preserved public method even in the missing-kernel RED.
        // Literal values follow its existing palette/area/material/Y/X/Bpp ordering.
        PublicOrderGolden();
        PublicRefusalOrder();
        _kernel = typeof(NativeStaticTerrainTextureAllocation).Assembly.GetType(
            "Spyro.Editor.Core.Analysis.NativeStaticTerrainGpuAllocation");
        Check(_kernel is { IsPublic: false, IsSealed: true },
            "Missing internal GPU-only extraction type; neutral bytes are not a public source capability.");
        Check(_kernel!.GetConstructors().Length == 0 &&
            _kernel.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Length == 0,
            "The extracted kernel exposes no public constructor, result API or certificate flags.");
        var compile = _kernel.GetMethod("Compile", BindingFlags.Static | BindingFlags.NonPublic);
        Check(compile is not null && compile.ReturnType == _kernel &&
            compile.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[]
            { typeof(byte[]), typeof(byte[]), typeof(NativeStaticTerrainTextureInput[]) }),
            "One internal exact GPU/map/ordered-input method, not an owner/clip/SPU/source factory.");
        VariableCountCases();
        KernelCases();
        MaterialTableCases.Run();
    }

    private static void PublicOrderGolden()
    {
        byte[] upload = Upload(), mask = new byte[0x80000];
        var inputs = AllocationCases.Inputs();
        var plan = NativeStaticTerrainTextureAllocation.Compile(upload, AllocationCases.Owners(upload, mask), inputs, 0x8000);
        var actual = plan.Allocations.Select(r => (r.MaterialId, r.Kind, r.BitsPerPixel,
            r.SourceOffset, r.TargetOffset, r.RowByteLength, r.RowCount)).ToArray();
        var expected = new[]
        {
            (0, "palette", 4, 128 * 1024, 0, 32, 16),
            (0, "palette", 8, 160 * 1024, 32, 512, 1),
            (1, "palette", 4, 128 * 1024, 544, 32, 16),
            (1, "palette", 8, 160 * 1024, 1056, 512, 1),
            (0, "pixel", 8, 64 * 1024, 576, 32, 32),
            (1, "pixel", 8, 64 * 1024, 608, 32, 32),
            (0, "pixel", 4, 0, 640, 16, 32),
            (1, "pixel", 4, 0, 656, 16, 32)
        };
        Check(actual.SequenceEqual(expected), "Literal old complete request/comparator order and targets remain exact.");
        Check(plan.ContainedAliases.Count == 0 && plan.AllocatedByteCount == 5120 &&
            plan.FreeByteCount == 0x80000 - 5120, "Neutral full-record count/free-space golden, without source authority.");
        Check(plan.CopyUploadPayload().AsSpan(0x80000).SequenceEqual(upload.AsSpan(0x80000)),
            "Literal nonzero entire old5E000 SPU bank remains unchanged.");
    }

    private static void PublicRefusalOrder()
    {
        byte[] upload = Upload(), mask = new byte[0x80000];
        var inputs = AllocationCases.Inputs();
        var owners = AllocationCases.Owners(upload, mask);
        var trace = new List<string>();
        IEnumerable<T> Traced<T>(string name, IEnumerable<T> values)
        {
            trace.Add(name + ":open");
            try { foreach (var value in values) { trace.Add(name + ":next"); yield return value; } }
            finally { trace.Add(name + ":close"); }
        }
        Exact(() => NativeStaticTerrainTextureAllocation.Compile(new byte[0xE5800],
            Traced("owners", owners), Traced("inputs", inputs), 0x8000), Complete);
        Check(trace.Count == 0, "The old complete-domain gate still precedes either enumeration.");
        var bad = inputs.ToArray(); bad[0] = bad[0] with { SourceIdentity = "" };
        Exact(() => NativeStaticTerrainTextureAllocation.Compile(upload,
            Traced("owners", owners), Traced("inputs", bad), 0x8000), InputShape);
        Check(trace.SequenceEqual(new[] { "inputs:open", "inputs:next", "inputs:next", "inputs:close",
            "owners:open", "owners:next", "owners:next", "owners:close" }),
            "Both bounded enumerations and disposal still occur input-first before any input shape validation.");

        var enumeratorFailure = new IOException("actual owner enumeration failure");
        IEnumerable<NativeId65NormalResourceOwner> BrokenOwners()
        { yield return owners[0]; throw enumeratorFailure; }
        try
        {
            NativeStaticTerrainTextureAllocation.Compile(upload, BrokenOwners(), bad, 0x8000);
            throw new InvalidDataException("Expected exact enumeration error.");
        }
        catch (IOException e) { Check(ReferenceEquals(e, enumeratorFailure), "Enumeration failure precedes malformed input and retains identity."); }

        // A complete but invalid descriptor must not jump ahead of existing owner checks.
        byte[] record = inputs[0].Record.ToArray(); record[8] ^= 2;
        var badDescriptor = new[] { inputs[0] with { Record = record }, inputs[1] };
        var badHash = owners.ToArray(); badHash[0] = badHash[0] with { Sha256 = new string('f', 64) };
        Exact(() => NativeStaticTerrainTextureAllocation.Compile(upload, badHash, badDescriptor, 0x8000), OwnerHash);
        byte[] dirty = upload.ToArray(); dirty[0] = 1;
        Exact(() => NativeStaticTerrainTextureAllocation.Compile(dirty,
            AllocationCases.Owners(dirty, mask), badDescriptor, 0x8000), DirtyFree);
        var unknown = badHash.ToArray(); unknown[0] = unknown[0] with { Provenance = "different-owner" };
        Exact(() => NativeStaticTerrainTextureAllocation.Compile(upload, unknown, badDescriptor, 0x8000),
            "Unknown GPU provenance/address cannot grant free or shared storage.");
    }

    private static void KernelCases()
    {
        byte[] upload = Upload(), gpu = upload[..0x80000], occupied = new byte[0x80000];
        AllocationCases.Hole(occupied, 0, 0, 16, 32, 1); // actual zero bytes still reserved.
        var inputs = AllocationCases.Inputs();
        byte[] gpuBefore = gpu.ToArray(), maskBefore = occupied.ToArray();
        string[] inputHashes = inputs.SelectMany(i => new[] { Hash(i.Record), Hash(i.SourceVram) }).ToArray();
        var publicPlan = NativeStaticTerrainTextureAllocation.Compile(upload,
            AllocationCases.Owners(upload, occupied), inputs, 0x8000);
        object result = Compile(gpu, occupied, inputs);
        byte[] emitted = Copy(result);
        Check(emitted.Length == 0x80000 &&
            emitted.SequenceEqual(publicPlan.CopyUploadPayload()[..0x80000]),
            "Same neutral full GPU inputs produce the same public-adapter bytes, not an independent source oracle.");
        var regions = Property<IReadOnlyList<NativeStaticTerrainTextureAllocationRegion>>(result, "Allocations");
        var aliases = Property<IReadOnlyList<NativeStaticTerrainTextureContainedAlias>>(result, "ContainedAliases");
        var records = Property<IReadOnlyList<NativeStaticTerrainTextureLayout>>(result, "Records");
        Check(regions.SequenceEqual(publicPlan.Allocations) && aliases.SequenceEqual(publicPlan.ContainedAliases) &&
            records.Select(r => r.RecordSha256).SequenceEqual(publicPlan.Records.Select(r => r.RecordSha256)),
            "Complete metadata, region order and aliases match the preserved wrapper.");
        Check(Property<int>(result, "AllocatedByteCount") == publicPlan.AllocatedByteCount &&
            Property<int>(result, "FreeByteCount") == publicPlan.FreeByteCount, "Counts agree without any new authority flag.");
        Check(gpu.SequenceEqual(gpuBefore) && occupied.SequenceEqual(maskBefore) &&
            inputs.SelectMany(i => new[] { Hash(i.Record), Hash(i.SourceVram) }).SequenceEqual(inputHashes),
            "Successful GPU allocation does not mutate caller GPU, mask, records or source VRAM.");
        byte[] returned = Copy(result); returned[0] ^= 1;
        gpu[0] ^= 1; occupied[0] = 0; inputs[0].Record[0] ^= 1; inputs[0].SourceVram[0] ^= 1;
        Check(Copy(result).SequenceEqual(emitted), "Returned bytes and every input buffer cannot mutate retained result.");
        byte[] recordCopy = records[0].CopyRecord(); recordCopy[0] ^= 1;
        Check(records[0].RecordSha256 == publicPlan.Records[0].RecordSha256 &&
            Hash(records[0].CopyRecord()) == records[0].RecordSha256, "Returned decoded records remain immutable.");
        try
        {
            ((IList<NativeStaticTerrainTextureAllocationRegion>)regions).Add(regions[0]);
            throw new InvalidDataException("Expected immutable region collection.");
        }
        catch (NotSupportedException) { Check(true, "Region collection rejects writes."); }
        KernelRefusals();
    }

    private static void VariableCountCases()
    {
        foreach (int count in new[] { 1, 3, 7 })
        {
            byte[] gpu = new byte[0x80000], occupied = new byte[0x80000];
            // Same-upload zero residue is occupied too; include the DMA-only
            // final16 bytes and nonzero sentinels in this neutral fixture.
            occupied.AsSpan(0x65800, 0x7F000 - 0x65800).Fill(1);
            gpu[0x65800] = 0xA5;
            gpu.AsSpan(0x7EFF0, 16).Fill(0x5A);
            AllocationCases.Hole(occupied, 0, 0, 16, 32, 1);
            var inputs = VariableInputs(count);
            string gpuHash = Hash(gpu), maskHash = Hash(occupied);
            string[] inputHashes = InputHashes(inputs);
            object result = Compile(gpu, occupied, inputs);
            byte[] output = Copy(result);
            var records = Property<IReadOnlyList<NativeStaticTerrainTextureLayout>>(result, "Records");
            var regions = Property<IReadOnlyList<NativeStaticTerrainTextureAllocationRegion>>(result, "Allocations");
            Check(records.Count == count && records.All(r => r.Descriptors.Count == 23),
                "Every requested material retains its complete184-byte/23-descriptor record without truncation.");
            Check(regions.Count == count * 4 && Property<int>(result, "AllocatedByteCount") == count * 2560 &&
                Property<int>(result, "FreeByteCount") == occupied.Count(b => b == 0) - count * 2560,
                "Variable material counts consume every independent physical owner and report exact remaining bytes.");
            var expectedOrder = Enumerable.Range(0, count).SelectMany(m => new[] { (m, "palette", 4), (m, "palette", 8) })
                .Concat(Enumerable.Range(0, count).Select(m => (m, "pixel", 8)))
                .Concat(Enumerable.Range(0, count).Select(m => (m, "pixel", 4)));
            Check(regions.Select(r => (r.MaterialId, r.Kind, r.BitsPerPixel)).SequenceEqual(expectedOrder),
                "All materials retain the established palette/area/material request order.");
            for (int material = 0; material < count; material++)
            {
                var source = NativeStaticTerrainTextureLayout.Decode(inputs[material].Record);
                for (int descriptor = 0; descriptor < 23; descriptor++)
                {
                    var before = source.Descriptors[descriptor]; var after = records[material].Descriptors[descriptor];
                    Check(before.CopyPixels(inputs[material].SourceVram).SequenceEqual(after.CopyPixels(output)) &&
                        before.CopyPalette(inputs[material].SourceVram).SequenceEqual(after.CopyPalette(output)),
                        "Every complete emitted descriptor retains the exact selected material's pixels and full palette.");
                    byte[] a = before.CopyDescriptor(), b = after.CopyDescriptor();
                    Check((a[6] & 0xE0) == (b[6] & 0xE0) && a[7] == b[7],
                        "Variable-count rebasing preserves descriptor mode/material bits.");
                }
                Check(regions.Where(r => r.MaterialId == material).All(r =>
                    r.SourceIdentity == inputs[material].SourceIdentity &&
                    r.AddressDomainIdentity == inputs[material].AddressDomainIdentity &&
                    r.RelocationPolicy == inputs[material].RelocationPolicy),
                    "Ordered records and allocation regions retain their actual neutral source labels.");
            }
            var written = new byte[gpu.Length];
            bool disjoint = true;
            foreach (var region in regions)
                for (int row = 0; row < region.RowCount; row++)
                    for (int x = 0; x < region.RowByteLength; x++)
                    {
                        int at = region.TargetOffset + row * 1024 + x;
                        disjoint &= occupied[at] == 0 && written[at] == 0;
                        written[at] = 1;
                    }
            Check(disjoint && gpu.Where((b, i) => written[i] == 0).SequenceEqual(output.Where((b, i) => written[i] == 0)),
                "All complete regions are disjoint and preserve every unallocated or occupied byte, including zero residue.");
            Check(output.AsSpan(0x65800, 0x7F000 - 0x65800).SequenceEqual(gpu.AsSpan(0x65800, 0x7F000 - 0x65800)),
                "The entire declared fallback residue and final DMA-only16 bytes remain exact.");
            Check(Hash(gpu) == gpuHash && Hash(occupied) == maskHash && InputHashes(inputs).SequenceEqual(inputHashes),
                "Variable-count success cannot mutate any caller array.");
            object retry = Compile(gpu, occupied, inputs);
            Check(Copy(retry).SequenceEqual(output) &&
                Property<IReadOnlyList<NativeStaticTerrainTextureAllocationRegion>>(retry, "Allocations").SequenceEqual(regions),
                "A fresh variable-count retry preserves complete output and allocation order.");
            byte[] detached = Copy(result); detached[0] ^= 1;
            gpu[0] ^= 1; occupied[0] = 0;
            inputs[^1].Record[0] ^= 1; inputs[^1].SourceVram[0] ^= 1;
            Check(Copy(result).SequenceEqual(output), "The final material and every exposed input/output buffer remain detached.");
        }

        var three = VariableInputs(3);
        byte[] plane = new byte[0x80000], available = new byte[0x80000];
        foreach (var bad in new[] { three.Reverse().ToArray(),
            new[] { three[0], three[1], three[2] with { MaterialId = 3 } },
            new[] { three[0], three[1], three[2] with { MaterialId = 1 } } })
        {
            string[] before = InputHashes(bad);
            Exact(() => Compile(plane, available, bad), KernelInputShape);
            Check(InputHashes(bad).SequenceEqual(before) && plane.All(b => b == 0) && available.All(b => b == 0),
                "Reordered, gapped and duplicate material IDs fail without mutating caller buffers.");
        }

        // The otherwise-fitting32-row window overlaps protected zero residue.
        // Palette reservations fit first; complete pixels then cannot fit.
        byte[] unprotected = Enumerable.Repeat((byte)1, 0x80000).ToArray();
        AllocationCases.Hole(unprotected, 0, 384, 1024, 32, 0);
        byte[] protectedMask = unprotected.ToArray();
        protectedMask.AsSpan(0x65800, 0x7F000 - 0x65800).Fill(1);
        string planeHash = Hash(plane), protectedHash = Hash(protectedMask);
        string[] threeHashes = InputHashes(three);
        Exact(() => Compile(plane, protectedMask, three), Capacity);
        Check(Hash(plane) == planeHash && Hash(protectedMask) == protectedHash && InputHashes(three).SequenceEqual(threeHashes),
            "Late variable-count capacity failure discards tentative reservations and preserves every caller buffer.");
        object fitting = Compile(plane, unprotected, three);
        Check(Property<int>(fitting, "AllocatedByteCount") == 7680 &&
            Property<IReadOnlyList<NativeStaticTerrainTextureAllocationRegion>>(fitting, "Allocations")
                .Any(r => Enumerable.Range(0, r.RowCount).Any(row =>
                    r.TargetOffset + row * 1024 < 0x7F000 && r.TargetOffset + row * 1024 + r.RowByteLength > 0x65800)),
            "The same full request fits only when its neutral mask releases residue; the failed attempt leaks no reservations.");
    }

    private static NativeStaticTerrainTextureInput[] VariableInputs(int count)
    {
        byte[] record = new byte[184];
        for (int i = 0; i < 23; i++)
            Convert.FromHexString(i < 3 ? "000020201f000880" : "004020281f408880").CopyTo(record, i * 8);
        return Enumerable.Range(0, count).Select(material => new NativeStaticTerrainTextureInput(material,
            "variable-source-" + material, "variable-GPU-domain-" + material, AllocationCases.Policy, record.ToArray(),
            Enumerable.Range(0, 0x80000).Select(p => (byte)(p * 17 + p / 1024 + material * 31)).ToArray())).ToArray();
    }

    private static string[] InputHashes(NativeStaticTerrainTextureInput[] inputs) =>
        inputs.SelectMany(i => new[] { Hash(i.Record), Hash(i.SourceVram) }).ToArray();

    private static void KernelRefusals()
    {
        byte[] gpu = new byte[0x80000], mask = new byte[0x80000];
        var inputs = AllocationCases.Inputs();
        foreach (int length in new[] { 0, 0x7FFFF, 0x80001, 0xDE000, 0xE5800 })
        {
            Exact(() => Compile(new byte[length], mask, inputs), "GPU allocation requires one exact80000 plane and binary occupied map.");
            Exact(() => Compile(gpu, new byte[length], inputs), "GPU allocation requires one exact80000 plane and binary occupied map.");
        }
        foreach (byte value in new byte[] { 2, 3, 255 })
        {
            byte[] bad = mask.ToArray(); bad[^1] = value;
            Exact(() => Compile(gpu, bad, inputs), "GPU allocation requires one exact80000 plane and binary occupied map.");
        }
        foreach (var bad in new[] { inputs.Reverse().ToArray(), Array.Empty<NativeStaticTerrainTextureInput>(),
            new[] { inputs[0], null! }, new[] { inputs[0] with { Record = new byte[183] }, inputs[1] },
            new[] { inputs[0] with { SourceVram = new byte[0x80001] }, inputs[1] },
            new[] { inputs[0] with { SourceIdentity = "" }, inputs[1] },
            new[] { inputs[0] with { RelocationPolicy = "different-policy" }, inputs[1] } })
            Exact(() => Compile(gpu, mask, bad), KernelInputShape);
        foreach (int parameter in new[] { 0, 1, 2 })
        {
            try { Compile(parameter == 0 ? null! : gpu, parameter == 1 ? null! : mask, parameter == 2 ? null! : inputs); }
            catch (ArgumentNullException) { Check(true, "Null internal argument refuses before cloning."); continue; }
            throw new InvalidDataException("Expected null refusal.");
        }
        byte[] partial = inputs[0].Record.ToArray(); partial[32] = 16; partial[36] = 47;
        Exact(() => Compile(gpu, mask, new[] { inputs[0] with { Record = partial }, inputs[1] }),
            "Partially overlapping source rectangles lack an admitted alias policy.");
        byte[] occupied = Enumerable.Repeat((byte)1, 0x80000).ToArray();
        // Room for complete palette requests, but no32-row pixel request: a late failure.
        AllocationCases.Hole(occupied, 0, 0, 32, 16, 0);
        AllocationCases.Hole(occupied, 32, 0, 32, 16, 0);
        AllocationCases.Hole(occupied, 0, 40, 512, 1, 0);
        AllocationCases.Hole(occupied, 0, 41, 512, 1, 0);
        string gpuHash = Hash(gpu), maskHash = Hash(occupied);
        string[] inputHashes = inputs.SelectMany(i => new[] { Hash(i.Record), Hash(i.SourceVram) }).ToArray();
        Exact(() => Compile(gpu, occupied, inputs), Capacity);
        Check(Hash(gpu) == gpuHash && Hash(occupied) == maskHash &&
            inputs.SelectMany(i => new[] { Hash(i.Record), Hash(i.SourceVram) }).SequenceEqual(inputHashes),
            "Late failed allocation discards every tentative output/reservation without mutating any caller buffer.");
        object retry = Compile(gpu, mask, inputs);
        Check(Property<int>(retry, "AllocatedByteCount") == 5120, "Fresh retry after actual late refusal has no leaked reservations.");
    }

    private static byte[] Upload()
    {
        byte[] result = new byte[0xDE000];
        for (int i = 0x80000; i < result.Length; i++) result[i] = (byte)(i * 13);
        return result;
    }
    private static object Compile(byte[] gpu, byte[] mask, NativeStaticTerrainTextureInput[] inputs)
    {
        try { return _kernel!.GetMethod("Compile", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { gpu, mask, inputs })!; }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
    }
    private static byte[] Copy(object result) => (byte[])_kernel!.GetMethod("CopyGpuPayload",
        BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(result, null)!;
    private static T Property<T>(object result, string name) => (T)_kernel!.GetProperty(name,
        BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(result)!;
    private static void Exact(Action action, string message)
    {
        try { action(); }
        catch (InvalidDataException e)
        { Check(e.Message == message, "Exact expected refusal/order: " + message); return; }
        throw new InvalidDataException("Expected exact refusal: " + message);
    }
}

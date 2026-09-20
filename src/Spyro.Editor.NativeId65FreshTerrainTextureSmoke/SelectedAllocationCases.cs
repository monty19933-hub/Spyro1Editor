using System.Collections;
using System.Reflection;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using static Program;

// Neutral ownership fixtures only. Neither count nor these caller labels
// establish original-source, document, scene, body or publication authority.
internal static class SelectedAllocationCases
{
    private delegate NativeStaticTerrainTextureAllocation Compiler(ReadOnlySpan<byte> upload,
        IEnumerable<NativeId65NormalResourceOwner> owners,
        IEnumerable<NativeStaticTerrainTextureInput> inputs, int clip);
    private static Compiler _compile = null!;

    internal static void Run()
    {
        var method = typeof(NativeStaticTerrainTextureAllocation).GetMethod("CompileSelected",
            BindingFlags.Static | BindingFlags.NonPublic);
        Check(method is { IsAssembly: true } && method.ReturnType == typeof(NativeStaticTerrainTextureAllocation) &&
            method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[]
            {
                typeof(ReadOnlySpan<byte>), typeof(IEnumerable<NativeId65NormalResourceOwner>),
                typeof(IEnumerable<NativeStaticTerrainTextureInput>), typeof(int)
            }), "Missing internal bounded-N selected allocation over the complete upload and declared owners.");
        Check(typeof(NativeStaticTerrainTextureAllocation).GetMethod("CompileSelected",
            BindingFlags.Static | BindingFlags.Public) == null, "No public selected-source admission is added.");
        _compile = method!.CreateDelegate<Compiler>();
        foreach (int count in new[] { 1, 3 }) Exact(count);
        Compatibility();
        Refusals();
        EnumerationAndCapture();
        CapacityAndDomains();
        Console.WriteLine("NATIVE_SELECTED_TERRAIN_ALLOCATION_FREE_OK sourceRead=false sourceAuthority=false nativeOutput=false");
    }

    private static void Exact(int count)
    {
        var (upload, mask, owners, inputs) = Fixture(count);
        byte[] before = upload.ToArray();
        var inputRecords = inputs.Select(i => i.Record.ToArray()).ToArray();
        var inputVram = inputs.Select(i => i.SourceVram.ToArray()).ToArray();
        var expected = Oracle(before, inputs);
        var result = _compile(upload, owners, inputs, 0x8000);
        byte[] output = result.CopyUploadPayload();
        Check(output.SequenceEqual(expected.Upload) && result.Records.Select(r => r.CopyRecord())
            .Zip(expected.Records).All(pair => pair.First.SequenceEqual(pair.Second)),
            "N1/N3 complete output and every184-byte record match independently specified exact-hole owners.");
        Check(result.Records.Count == count && result.Allocations.Count == 4 * count &&
            result.AllocatedByteCount == 2560 * count && result.FreeByteCount == 0 && result.ContainedAliases.Count == 0,
            "Every distinct slot retains all four physical resource owners without cross-material aliasing.");
        Check(!result.SourceCertified && !result.RuntimeVerified && !result.PublicationApproved,
            "Selected neutral allocation grants no source, runtime or publication authority.");
        VerifyDescriptors(inputs, result);
        AllocationCases.VerifyComplement(before, output, mask, result.Allocations);
        Check(upload.SequenceEqual(before) && inputs.Select((input, i) =>
            input.Record.SequenceEqual(inputRecords[i]) && input.SourceVram.SequenceEqual(inputVram[i])).All(x => x),
            "Successful selected allocation does not mutate any caller upload or source bytes.");
        Check(Inverse(output, before, expected.Upload, count).SequenceEqual(before),
            "Complete independent neutral inverse restores allocated preimages and every protected/unallocated/SPU byte.");
        foreach (int at in new[] { 0, 300 * 1024, 200 * 1024, 0x80000, output.Length - 1 })
        {
            byte[] changed = output.ToArray(); changed[at] ^= 1;
            Invalid(() => Inverse(changed, before, expected.Upload, count),
                "Changed allocated, protected-zero/nonzero or SPU afterimages cannot satisfy exact inversion.");
        }
        var repeat = _compile(upload, owners.Reverse(), inputs.Reverse(), 0x8000);
        Check(repeat.CopyUploadPayload().SequenceEqual(output) &&
            repeat.Allocations.SequenceEqual(result.Allocations) && repeat.ContainedAliases.SequenceEqual(result.ContainedAliases),
            "Sorting complete contiguous IDs and owners preserves deterministic retry and physical owner order.");
        byte[] copy = result.CopyUploadPayload(); copy.AsSpan().Fill(0xCC);
        byte[] record = result.Records[^1].CopyRecord(); record.AsSpan().Fill(0xDD);
        upload.AsSpan().Fill(0xEE); inputs[^1].Record.AsSpan().Clear(); inputs[^1].SourceVram.AsSpan().Clear();
        inputs[0] = inputs[0] with { SourceIdentity = "later replacement" };
        Check(result.CopyUploadPayload().SequenceEqual(expected.Upload) && result.Records[^1].CopyRecord()
            .SequenceEqual(expected.Records[^1]), "Returned arrays and later caller bytes/collection replacement cannot mutate a detached result.");
    }

    private static void Compatibility()
    {
        var (upload, _, owners, inputs) = Fixture(2);
        var legacy = NativeStaticTerrainTextureAllocation.Compile(upload, owners, inputs, 0x8000);
        var selected = _compile(upload, owners, inputs, 0x8000);
        Check(legacy.CopyUploadPayload().SequenceEqual(selected.CopyUploadPayload()) &&
            legacy.Records.Select(r => r.RecordSha256).SequenceEqual(selected.Records.Select(r => r.RecordSha256)) &&
            legacy.Allocations.SequenceEqual(selected.Allocations) && legacy.ContainedAliases.SequenceEqual(selected.ContainedAliases) &&
            legacy.FreeByteCount == selected.FreeByteCount && legacy.AllocatedByteCount == selected.AllocatedByteCount,
            "The public exact-two adapter and selected adapter share all byte/record/owner/alias accounting.");
        Check(legacy.ComposeTwoFaceData(AllocationCases.DataFixture()).SequenceEqual(
            selected.ComposeTwoFaceData(AllocationCases.DataFixture())), "Existing two-face metadata bytes remain exactly compatible.");
        InvalidMessage(() => NativeStaticTerrainTextureAllocation.Compile(upload, owners, inputs.Take(1), 0x8000),
            "The detached profile requires exactly two complete material records.");
        InvalidMessage(() => NativeStaticTerrainTextureAllocation.Compile(upload, owners, inputs, 0),
            "Two-record allocation requires the completeDE000 upload and exact admitted native clip8000.");
        var ownerProbe = new HostileCollection<NativeId65NormalResourceOwner>(ThrowingOwners(owners[0]), 0);
        try { _ = NativeStaticTerrainTextureAllocation.Compile(upload, ownerProbe, [], 0x8000); throw new InvalidDataException("Expected legacy owner iterator exception."); }
        catch (IteratorFailure) { Check(ownerProbe.Disposed && ownerProbe.Yielded == 1,
            "Legacy owner enumeration still precedes its exact-two count refusal, preserving public validation order."); }
        foreach (int count in new[] { 1, 3 })
        {
            var fixture = Fixture(count);
            Invalid(() => NativeStaticTerrainTextureAllocation.Compile(fixture.Upload, fixture.Owners, fixture.Inputs, 0x8000),
                "The old public Compile contract still refuses one and three inputs.");
            var result = _compile(fixture.Upload, fixture.Owners, fixture.Inputs, 0x8000);
            Invalid(() => result.ComposeTwoFaceData(AllocationCases.DataFixture()),
                "A neutral selected result does not widen the old exact-two-face metadata writer.");
        }
    }

    private static void Refusals()
    {
        var (upload, _, owners, inputs) = Fixture(3);
        byte[] before = upload.ToArray();
        var records = inputs.Select(i => i.Record.ToArray()).ToArray();
        var domains = inputs.Select(i => i.SourceVram.ToArray()).ToArray();
        foreach (int clip in new[] { -1, 0, 129, 0x7FFF, 0x8001, int.MaxValue })
            Invalid(() => _compile(upload, owners, inputs, clip), "Selected resources still require exact clip8000.");
        foreach (int length in new[] { 0, 0x80000, 0xDDFFF, 0xDE001 })
            Invalid(() => _compile(new byte[length], owners, inputs, 0x8000), "Complete fixed GPU+SPU domain precedes enumeration.");
        Invalid(() => _compile(upload, owners, [], 0x8000), "Zero records is not a selected allocation.");
        Invalid(() => _compile(upload, owners, [null!], 0x8000), "Null source input refuses without dereferencing fields.");
        foreach (int id in new[] { -1, 1, 127, 128, 255, int.MaxValue })
            Invalid(() => _compile(upload, owners, [inputs[0] with { MaterialId = id }], 0x8000),
                "Selected IDs are contiguous0..N-1 and never masked into a native row.");
        foreach (var selected in new[]
        {
            new[] { inputs[0], inputs[1] with { MaterialId = 0 }, inputs[2] },
            new[] { inputs[0], inputs[1] with { MaterialId = 3 }, inputs[2] },
            new[] { inputs[0], inputs[2] }
        }) Invalid(() => _compile(upload, owners, selected, 0x8000), "Duplicate or missing IDs cannot reorder or discard a slot.");
        foreach (var invalid in new[]
        {
            inputs[0] with { SourceIdentity = "" }, inputs[0] with { SourceIdentity = new string('a', 1025) },
            inputs[0] with { AddressDomainIdentity = "bad\naddress" }, inputs[0] with { RelocationPolicy = "other-policy" },
            inputs[0] with { Record = null! }, inputs[0] with { Record = new byte[183] },
            inputs[0] with { Record = new byte[185] }, inputs[0] with { SourceVram = null! },
            inputs[0] with { SourceVram = new byte[0x7FFFF] }, inputs[0] with { SourceVram = new byte[0x80001] }
        }) Invalid(() => _compile(upload, owners, [invalid], 0x8000), "Every selected source has bounded identity, policy and complete byte domains before cloning.");
        foreach (var invalidOwners in new[]
        {
            Array.Empty<NativeId65NormalResourceOwner>(), new NativeId65NormalResourceOwner[] { null! },
            owners.Skip(1).ToArray(), owners.Append(owners[0]).ToArray(),
            owners.Select((o, i) => i == 0 ? o with { Sha256 = new string('0', 64) } : o).ToArray(),
            owners.Select((o, i) => i == 0 ? o with { Domain = NativeId65ResourceDomain.DataComponentTail } : o).ToArray(),
            owners.Select((o, i) => i == 0 ? o with { Offset = int.MaxValue } : o).ToArray(),
            owners.Select((o, i) => i == 0 ? o with { ByteLength = int.MaxValue } : o).ToArray(),
            owners.Select(o => o.Provenance == AllocationCases.Free ? o with { SourceWadOffset = 0x119E800L + o.Offset } : o).ToArray(),
            owners.Select(o => o.Provenance == AllocationCases.Protected ? o with { SourceWadOffset = null } : o).ToArray(),
            owners.Select(o => o.Provenance == AllocationCases.Detached ? o with { SourceWadOffset = 0 } : o).ToArray(),
            owners.Select(o => o.Provenance == AllocationCases.Audio ? o with { Provenance = AllocationCases.Free, SourceWadOffset = null } : o).ToArray()
        }) Invalid(() => _compile(upload, invalidOwners, inputs, 0x8000), "Selected inputs use the same exhaustive owner/domain/hash/provenance refusal kernel.");
        Check(upload.SequenceEqual(before) && inputs.Select((i, n) => i.Record.SequenceEqual(records[n]) &&
            i.SourceVram.SequenceEqual(domains[n])).All(x => x), "Shape/ownership failures leave every caller byte unchanged.");
        Null(() => _compile(upload, owners, null!, 0x8000), "inputs");
        Null(() => _compile(upload, null!, inputs, 0x8000), "values");
    }

    private static void EnumerationAndCapture()
    {
        var (upload, _, owners, inputs) = Fixture(3);
        var expected = Oracle(upload, inputs);
        var inputProbe = new HostileCollection<NativeStaticTerrainTextureInput>(MutatingInputs(), 0);
        var ownerProbe = new HostileCollection<NativeId65NormalResourceOwner>(MutatingOwners(), int.MaxValue);
        var result = _compile(upload, ownerProbe, inputProbe, 0x8000);
        Check(inputProbe.Yielded == 3 && ownerProbe.Yielded == owners.Length && inputProbe.Disposed && ownerProbe.Disposed &&
            inputProbe.CountReads == 0 && ownerProbe.CountReads == 0,
            "Bounded selected capture uses only enumerators, never reported Count, indexers or CopyTo; both iterators dispose.");
        Check(result.CopyUploadPayload().SequenceEqual(expected.Upload) && result.Records.Select(r => r.CopyRecord())
            .Zip(expected.Records).All(pair => pair.First.SequenceEqual(pair.Second)),
            "Upload is captured before the first input MoveNext; each yielded source is captured before the next MoveNext and before owner enumeration.");
        Check(upload.All(b => b == 0xEE) && inputs.All(i => i.Record.All(b => b == 0xDD)),
            "Hostile callbacks really mutated caller storage; success is not an unexercised timing assertion.");

        var fixture = Fixture(1);
        var excessive = new HostileCollection<NativeStaticTerrainTextureInput>(TooManyInputs(fixture.Inputs[0]), 1);
        Invalid(() => _compile(fixture.Upload, fixture.Owners, excessive, 0x8000), "Independent128-record limit rejects the129th yield.");
        Check(excessive.Yielded == 129 && excessive.Disposed && excessive.CountReads == 0,
            "Excess input enumeration stops immediately at its bounded sentinel and disposes the source.");
        var complete128 = new HostileCollection<NativeStaticTerrainTextureInput>(TooManyInputs(fixture.Inputs[0]).Take(128), 0);
        var protectedOwners = AllocationCases.Owners(fixture.Upload, Enumerable.Repeat((byte)1, 0x80000).ToArray());
        InvalidMessage(() => _compile(fixture.Upload, protectedOwners, complete128, 0x8000),
            "No complete free texture/palette rectangle remains; partial reservations are discarded.");
        Check(complete128.Yielded == 128 && complete128.Disposed && complete128.CountReads == 0,
            "All128 complete contiguous slots reach the shared real-capacity check; there is no hidden smaller record cap.");
        var excessiveOwners = new HostileCollection<NativeId65NormalResourceOwner>(TooManyOwners(fixture.Owners[0]), 1);
        Invalid(() => _compile(fixture.Upload, excessiveOwners, fixture.Inputs, 0x8000), "Owner enumeration retains its independent65536 bound.");
        Check(excessiveOwners.Yielded == 65537 && excessiveOwners.Disposed && excessiveOwners.CountReads == 0,
            "The65537th owner stops enumeration independently of material count or caller Count.");
        var badInput = new HostileCollection<NativeStaticTerrainTextureInput>([fixture.Inputs[0] with { Record = new byte[183] }], 0);
        var untouchedOwners = new HostileCollection<NativeId65NormalResourceOwner>(fixture.Owners, 0);
        Invalid(() => _compile(fixture.Upload, untouchedOwners, badInput, 0x8000), "Malformed source shape fails at its yield boundary.");
        Check(badInput.Disposed && untouchedOwners.Yielded == 0, "Malformed sources cannot invoke a later owner iterator.");
        var throwing = new HostileCollection<NativeStaticTerrainTextureInput>(ThrowAfterOne(fixture.Inputs[0]), 0);
        try { _ = _compile(fixture.Upload, fixture.Owners, throwing, 0x8000); throw new InvalidDataException("Expected iterator exception."); }
        catch (IteratorFailure) { Check(throwing.Disposed && throwing.Yielded == 1, "Caller iterator failure disposes capture and cannot publish a partial result."); }

        IEnumerable<NativeStaticTerrainTextureInput> MutatingInputs()
        {
            upload.AsSpan().Fill(0xCC);
            for (int i = 0; i < inputs.Length; i++)
            {
                yield return inputs[i];
                inputs[i].Record.AsSpan().Fill(0xDD);
                inputs[i].SourceVram.AsSpan().Clear();
            }
        }
        IEnumerable<NativeId65NormalResourceOwner> MutatingOwners()
        {
            upload.AsSpan().Fill(0xEE);
            for (int i = 0; i < owners.Length; i++)
            {
                yield return owners[i];
                owners[i] = owners[i] with { Sha256 = new string('0', 64) };
            }
        }
    }

    private static void CapacityAndDomains()
    {
        var (upload, mask, owners, inputs) = Fixture(3);
        byte[] before = upload.ToArray();
        var expected = Oracle(upload, inputs);
        // Removing the third slot's final LQ row forces a late failure after
        // the first two materials' tentative reservations have already fit.
        byte[] missing = mask.ToArray(); AllocationCases.Hole(missing, 32, 159, 16, 1, 1);
        for (int y = 300; y < 500; y += 2) AllocationCases.Hole(missing, 800, y, 16, 1, 0);
        Invalid(() => _compile(upload, AllocationCases.Owners(upload, missing), inputs, 0x8000),
            "Aggregate free bytes cannot replace the third complete rectangle; failure discards all tentative writes.");
        byte[] crossing = mask.ToArray(); AllocationCases.Hole(crossing, 0, 128, 48, 32, 1);
        AllocationCases.Hole(crossing, 0, 240, 48, 32, 0);
        Invalid(() => _compile(upload, AllocationCases.Owners(upload, crossing), inputs, 0x8000),
            "Selected pixel owners cannot cross a native256-row page even with sufficient total free bytes.");
        byte[] dirty = upload.ToArray(); dirty[0] = 1;
        Invalid(() => _compile(dirty, AllocationCases.Owners(dirty, mask), inputs, 0x8000),
            "A caller cannot turn nonzero payload into available storage with a fresh-zero owner label.");
        byte[] partial = inputs[2].Record.ToArray(); partial[32] = 16; partial[36] = 47;
        Invalid(() => _compile(upload, owners, [inputs[0], inputs[1], inputs[2] with { Record = partial }], 0x8000),
            "Unsupported partial source overlap in the last material is not cross-slot alias authority.");
        var same = inputs.Select(i => i with
        {
            SourceIdentity = inputs[0].SourceIdentity, AddressDomainIdentity = inputs[0].AddressDomainIdentity,
            Record = inputs[0].Record, SourceVram = inputs[0].SourceVram
        }).ToArray();
        var separate = _compile(upload, owners, same, 0x8000);
        Check(separate.Allocations.Count == 12 && separate.Allocations.Select(a => a.MaterialId).Distinct().Count() == 3 &&
            separate.AllocatedByteCount == 7680, "Equal labels/records/source bytes still retain three distinct material-slot owners.");
        Check(upload.SequenceEqual(before) && _compile(upload, owners, inputs, 0x8000).CopyUploadPayload().SequenceEqual(expected.Upload),
            "Late failures and equal-byte domains cannot leak reservations into caller input or a fresh retry.");
    }

    private static IEnumerable<NativeStaticTerrainTextureInput> TooManyInputs(NativeStaticTerrainTextureInput input)
    {
        for (int i = 0; i < 129; i++) yield return input with { MaterialId = Math.Min(i, 127) };
        throw new IteratorFailure();
    }
    private static IEnumerable<NativeId65NormalResourceOwner> TooManyOwners(NativeId65NormalResourceOwner owner)
    {
        for (int i = 0; i < 65537; i++) yield return owner;
        throw new IteratorFailure();
    }
    private static IEnumerable<NativeStaticTerrainTextureInput> ThrowAfterOne(NativeStaticTerrainTextureInput input)
    { yield return input; throw new IteratorFailure(); }
    private static IEnumerable<NativeId65NormalResourceOwner> ThrowingOwners(NativeId65NormalResourceOwner owner)
    { yield return owner; throw new IteratorFailure(); }
    private sealed class IteratorFailure : Exception { }

    private sealed class HostileCollection<T>(IEnumerable<T> items, int reportedCount) : IList<T>, IReadOnlyList<T>
    {
        internal int CountReads, Yielded;
        internal bool Disposed;
        public int Count { get { CountReads++; return reportedCount; } }
        public bool IsReadOnly => true;
        public T this[int index] { get => throw new IteratorFailure(); set => throw new IteratorFailure(); }
        public IEnumerator<T> GetEnumerator()
        {
            try { foreach (var item in items) { Yielded++; yield return item; } }
            finally { Disposed = true; }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void CopyTo(T[] array, int index) => throw new IteratorFailure();
        public int IndexOf(T item) => throw new IteratorFailure();
        public bool Contains(T item) => throw new IteratorFailure();
        public void Add(T item) => throw new IteratorFailure();
        public void Clear() => throw new IteratorFailure();
        public void Insert(int index, T item) => throw new IteratorFailure();
        public bool Remove(T item) => throw new IteratorFailure();
        public void RemoveAt(int index) => throw new IteratorFailure();
    }

    private static void Null(Action action, string parameter)
    {
        try { action(); }
        catch (ArgumentNullException e) { Check(e.ParamName == parameter, "Exact null input is rejected without deferred dereference."); return; }
        throw new InvalidDataException("Expected null selected-allocation input.");
    }

    private static (byte[] Upload, byte[] Mask, NativeId65NormalResourceOwner[] Owners,
        NativeStaticTerrainTextureInput[] Inputs) Fixture(int count)
    {
        byte[] upload = new byte[0xDE000], mask = Enumerable.Repeat((byte)1, 0x80000).ToArray();
        for (int i = 0x80000; i < upload.Length; i++) upload[i] = (byte)(i * 13 + i / 1024);
        AllocationCases.Hole(mask, 0, 200, 32, 16, 2);
        for (int row = 200; row < 216; row++) upload.AsSpan(row * 1024, 32).Fill(0x5A);
        foreach (var hole in Holes(count)) AllocationCases.Hole(mask, hole.X, hole.Y, hole.Width, hole.Rows, 0);
        var prototype = AllocationCases.Inputs()[0];
        var inputs = Enumerable.Range(0, count).Select(i => new NativeStaticTerrainTextureInput(i,
            "selected-neutral-source-" + i, "selected-neutral-domain-" + i, AllocationCases.Policy,
            prototype.Record.ToArray(), Enumerable.Range(0, 0x80000).Select(p => (byte)(p * 17 + p / 1024 + i * 29)).ToArray())).ToArray();
        return (upload, mask, AllocationCases.Owners(upload, mask), inputs);
    }

    private static IEnumerable<(int Material, int X, int Y, int Width, int Rows, int SourceX, int SourceY)> Holes(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return (i, i * 32, 0, 32, 16, 0, 128); // Full4bpp CLUT bank.
            yield return (i, 0, 40 + i, 512, 1, 0, 160); // Full8bpp CLUT row.
            yield return (i, i * 32, 64, 32, 32, 0, 64);
            yield return (i, i * 16, 128, 16, 32, 0, 0);
        }
    }

    private static (byte[] Upload, byte[][] Records) Oracle(byte[] before, NativeStaticTerrainTextureInput[] inputs)
    {
        byte[] output = before.ToArray();
        foreach (var hole in Holes(inputs.Length))
            for (int row = 0; row < hole.Rows; row++)
                inputs[hole.Material].SourceVram.AsSpan((hole.SourceY + row) * 1024 + hole.SourceX, hole.Width)
                    .CopyTo(output.AsSpan((hole.Y + row) * 1024 + hole.X, hole.Width));
        byte[][] records = new byte[inputs.Length][];
        for (int material = 0; material < inputs.Length; material++)
        {
            byte u = checked((byte)(32 * material));
            byte[] low = [u, 128, checked((byte)(32 + material)), 0, checked((byte)(u + 31)), 128, 8, 128];
            ushort clut = checked((ushort)((40 + material) * 64 + 32));
            byte[] high = [u, 64, (byte)clut, (byte)(clut >> 8), checked((byte)(u + 31)), 64, 0x88, 128];
            records[material] = Enumerable.Range(0, 23).SelectMany(i => i < 3 ? low : high).ToArray();
        }
        return (output, records);
    }

    private static byte[] Inverse(byte[] after, byte[] before, byte[] expected, int count)
    {
        if (!after.AsSpan().SequenceEqual(expected)) throw new InvalidDataException("Complete neutral afterimage changed.");
        byte[] restored = after.ToArray();
        foreach (var hole in Holes(count))
            for (int row = 0; row < hole.Rows; row++)
                before.AsSpan((hole.Y + row) * 1024 + hole.X, hole.Width)
                    .CopyTo(restored.AsSpan((hole.Y + row) * 1024 + hole.X, hole.Width));
        return restored;
    }

    private static void VerifyDescriptors(NativeStaticTerrainTextureInput[] inputs, NativeStaticTerrainTextureAllocation result)
    {
        byte[] output = result.CopyUploadPayload()[..0x80000];
        for (int material = 0; material < inputs.Length; material++)
        {
            var before = NativeStaticTerrainTextureLayout.Decode(inputs[material].Record);
            var after = NativeStaticTerrainTextureLayout.Decode(result.Records[material].CopyRecord());
            Check(after.Descriptors.Count == 23, "Every complete selected record retains23 descriptors.");
            for (int descriptor = 0; descriptor < 23; descriptor++)
            {
                var a = before.Descriptors[descriptor]; var b = after.Descriptors[descriptor];
                Check(a.CopyPixels(inputs[material].SourceVram).SequenceEqual(b.CopyPixels(output)) &&
                    a.CopyPalette(inputs[material].SourceVram).SequenceEqual(b.CopyPalette(output)),
                    "Every selected slot/descriptor re-decodes to its complete original pixel and palette owners.");
                byte[] x = a.CopyDescriptor(), y = b.CopyDescriptor();
                Check((x[6] & 0xE0) == (y[6] & 0xE0) && x[7] == y[7], "All interpretation/direct-LQ flag bits survive rebasing.");
            }
        }
    }

    private static void Invalid(Action action, string label)
    {
        try { action(); }
        catch (InvalidDataException) { Check(true, label); return; }
        throw new InvalidDataException("Expected selected-allocation refusal: " + label);
    }

    private static void InvalidMessage(Action action, string message)
    {
        try { action(); }
        catch (InvalidDataException e) { Check(e.Message == message, "Exact existing refusal stage/message: " + message); return; }
        throw new InvalidDataException("Expected exact allocator refusal: " + message);
    }
}

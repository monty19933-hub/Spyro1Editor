using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

internal static class NativeLockedChestBehaviorLayoutCases
{
    private const string PayloadHash = "08f4372710b7162982397e247b75ed9b00cc5290ef9630d4e2fdd320611012b9";
    private static int _checks;

    internal static void Free()
    {
        // Literal instructions and expected afterimage are independent of the
        // production encoder: source segments are separated, destinations adjacent.
        byte[] first = Words(0x1022000F, 0x24630001, 0x08004000, 0,
            0x0C004010, 0x24840002, 0x0C00C000, 0x34420055,
            0x14220017, 0x24A50003, 0x08004021, 0);
        byte[] second = Words(0x24060004, 0);
        byte[] firstBefore = first.ToArray(), secondBefore = second.ToArray();
        var segments = new[]
        {
            new NativeLockedChestCodeSegment("first", 0x80010000, 0x80020000, first),
            new NativeLockedChestCodeSegment("second", 0x80010040, 0x80020030, second)
        };
        var targets = new[]
        {
            new NativeLockedChestExternalTarget(0x80010080, 0x80020100),
            new NativeLockedChestExternalTarget(0x80010084, 0x80020104)
        };
        var linked = NativeLockedChestCodeRelocation.Compile(segments, targets);
        byte[] expected = Words(0x1022000B, 0x24630001, 0x08008000, 0,
            0x0C00800C, 0x24840002, 0x0C00C000, 0x34420055,
            0x14220037, 0x24A50003, 0x08008041, 0, 0x24060004, 0);
        Check(linked.CopyBytes().SequenceEqual(expected), "Complete independent branch/jump/call literal afterimage.");
        Check(linked.Transfers.Count == 6 && linked.Transfers.Select(t => t.SourcePc).SequenceEqual(
            new uint[] { 0x80010000, 0x80010008, 0x80010010, 0x80010018, 0x80010020, 0x80010028 }),
            "Every direct transfer has its actual source site, not an arbitrary declaration.");
        Check(linked.Transfers.Select(t => t.TargetKind).SequenceEqual(new[]
        {
            NativeLockedChestTargetKind.InternalSegment, NativeLockedChestTargetKind.InternalSegment,
            NativeLockedChestTargetKind.InternalSegment, NativeLockedChestTargetKind.RetainedUnprovenCall,
            NativeLockedChestTargetKind.ExplicitExternal, NativeLockedChestTargetKind.ExplicitExternal
        }), "An unchanged external JAL is explicit unresolved ownership, not an executable certificate.");
        foreach (var transfer in linked.Transfers)
        {
            int offset = checked((int)(transfer.SourcePc - 0x80010000));
            Check(transfer.BeforeWord == Word(firstBefore, offset) && transfer.AfterWord == Word(expected, offset) &&
                transfer.FinalPc == 0x80020000 + offset && transfer.DelayWord == Word(firstBefore, offset + 4),
                "Direct-transfer preimage, emitted word, runtime address and unchanged delay slot are exact.");
        }
        Check(linked.Transfers[3].SourceTarget == 0x80030000 && linked.Transfers[3].FinalTarget == 0x80030000 &&
            !linked.CompleteExternalOwnersProven, "Retained call identity does not prove its source or final routine owner.");
        var inverse = linked.RestoreSources(expected);
        Check(inverse.Count == 2 && inverse[0].SequenceEqual(firstBefore) && inverse[1].SequenceEqual(secondBefore),
            "Complete inverse recovers all source words, including every non-transfer and delay byte.");
        first[0] ^= 1; second[^1] ^= 1; segments[0] = segments[1]; targets[0] = targets[1]; inverse[0][0] ^= 1;
        Check(linked.CopyBytes().SequenceEqual(expected) && linked.RestoreSources(expected)[0].SequenceEqual(firstBefore),
            "Caller input arrays, collection edits and returned inverse buffers cannot change retained bytes.");
        byte[] copy = linked.CopyBytes(); copy[^1] ^= 1;
        Check(linked.CopyBytes().SequenceEqual(expected), "Output copies are detached.");
        for (int at = 0; at < expected.Length; at++)
        {
            byte[] changed = expected.ToArray(); changed[at] ^= 1;
            Reject<InvalidDataException>(() => linked.RestoreSources(changed));
        }
        Reject<InvalidDataException>(() => linked.RestoreSources(expected[..^1]));
        Reject<InvalidDataException>(() => linked.RestoreSources([.. expected, 0]));
        Reject<ArgumentNullException>(() => NativeLockedChestCodeRelocation.Compile(null!, []));
        Reject<ArgumentNullException>(() => NativeLockedChestCodeRelocation.Compile([], null!));
        Reject<InvalidDataException>(() => NativeLockedChestCodeRelocation.Compile([], []));
        var plain = new NativeLockedChestCodeSegment("plain", 0x80010000, 0x80020000, Words(0x24020001, 0));
        foreach (var invalid in new[]
        {
            plain with { Id = "" }, plain with { SourcePc = 0x80010001 }, plain with { FinalPc = 0x80020001 },
            plain with { Bytes = [0] }, plain with { Bytes = [] }, plain with { FinalPc = 0x801FFFFC }
        }) Reject<InvalidDataException>(() => NativeLockedChestCodeRelocation.Compile([invalid], []));
        Reject<InvalidDataException>(() => NativeLockedChestCodeRelocation.Compile([plain, plain], []));
        Reject<InvalidDataException>(() => NativeLockedChestCodeRelocation.Compile([
            plain, plain with { Id = "alias", FinalPc = 0x80020008 }], []));
        Reject<InvalidDataException>(() => NativeLockedChestCodeRelocation.Compile([
            plain, plain with { Id = "final-alias", SourcePc = 0x80010008 }], []));
        Reject<InvalidDataException>(() => NativeLockedChestCodeRelocation.Compile([
            plain, plain with { Id = "final-hole", SourcePc = 0x80010008, FinalPc = 0x80020010 }], []));
        var externalJump = plain with { Bytes = Words(0x08004020, 0) };
        Reject<InvalidDataException>(() => NativeLockedChestCodeRelocation.Compile([externalJump], []));
        Reject<InvalidDataException>(() => NativeLockedChestCodeRelocation.Compile([externalJump],
            [new(0x80010084, 0x80020100)]));
        Reject<InvalidDataException>(() => NativeLockedChestCodeRelocation.Compile([externalJump],
            [new(0x80010080, 0x80020100), new(0x80010080, 0x80020100)]));
        Reject<InvalidDataException>(() => NativeLockedChestCodeRelocation.Compile([externalJump],
            [new(0x80010080, 0x80020101)]));
        Reject<InvalidDataException>(() => NativeLockedChestCodeRelocation.Compile([plain], [new(0x80010080, 0x80020100)]));
        Reject<InvalidDataException>(() => NativeLockedChestCodeRelocation.Compile([
            plain with { Bytes = Words(0x1000001F, 0) }], [new(0x80010080, 0x80040004)]));
        var minimumBranch = NativeLockedChestCodeRelocation.Compile([
            plain with { Bytes = Words(0x1000001F, 0) }], [new(0x80010080, 0x80000004)]);
        var maximumBranch = NativeLockedChestCodeRelocation.Compile([
            plain with { Bytes = Words(0x1000001F, 0) }], [new(0x80010080, 0x80040000)]);
        Check(Word(minimumBranch.CopyBytes(), 0) == 0x10008000 && Word(maximumBranch.CopyBytes(), 0) == 0x10007FFF,
            "Signed branch endpoints remain reachable without an invented veneer.");
        Reject<InvalidDataException>(() => NativeLockedChestCodeRelocation.Compile([
            plain with { Bytes = Words(0x1000001F, 0) }], [new(0x80010080, 0x80000000)]));
        byte[] mutable = Words(0x24020001, 0);
        IEnumerable<NativeLockedChestCodeSegment> MutatingSegments()
        {
            yield return plain with { Bytes = mutable };
            mutable[0] ^= 1;
        }
        Check(NativeLockedChestCodeRelocation.Compile(MutatingSegments(), []).CopyBytes().SequenceEqual(plain.Bytes),
            "Source bytes are snapshotted before the next caller MoveNext.");
        int segmentMoves = 0; bool segmentDisposed = false;
        IEnumerable<NativeLockedChestCodeSegment> UnboundedSegments()
        {
            try
            {
                while (true)
                {
                    int i = segmentMoves++;
                    yield return plain with { Id = "s" + i, SourcePc = 0x80010000 + (uint)(i * 8), FinalPc = 0x80020000 + (uint)(i * 8) };
                }
            }
            finally { segmentDisposed = true; }
        }
        Reject<InvalidDataException>(() => NativeLockedChestCodeRelocation.Compile(UnboundedSegments(), []));
        Check(segmentMoves == 17 && segmentDisposed, "Segment enumeration stops at its sixteen-segment bound and disposes the hostile enumerator.");
        int targetMoves = 0; bool targetDisposed = false;
        IEnumerable<NativeLockedChestExternalTarget> UnboundedTargets()
        {
            try
            {
                while (true)
                {
                    int i = targetMoves++;
                    yield return new(0x80030000 + (uint)(i * 4), 0x80040000 + (uint)(i * 4));
                }
            }
            finally { targetDisposed = true; }
        }
        Reject<InvalidDataException>(() => NativeLockedChestCodeRelocation.Compile([plain], UnboundedTargets()));
        Check(targetMoves == 129 && targetDisposed, "External target enumeration stops at its bound and disposes before relocation.");
        var missingDelay = NativeLockedChestCodeRelocation.Compile([
            plain with { Bytes = Words(0x0C00C000) }], []);
        Check(missingDelay.Transfers.Single().DelayWord is null && !missingDelay.CompleteExternalOwnersProven,
            "Historical partial segments never invent a delay-slot read or claim complete control-flow ownership.");

        List<(long, int)> calls = [];
        var error = Reject<InvalidDataException>(() => NativeLockedChestBehaviorSource.Capture((at, length) =>
        { calls.Add((at, length)); return new byte[length]; }));
        Check(calls.SequenceEqual(new[] { (0x188719CL, 0x30), (0x1887330L, 0x70C), (0x1881B64L, 0x434) }) &&
            error.Message.StartsWith("Peace Keepers Locked Chest setup SHA-256 changed:", StringComparison.Ordinal),
            "All three historical reads precede the first setup hash guard, in their unchanged order.");
        var earlyGuard = Reject<InvalidDataException>(() => NativeLockedChestBehaviorSource.Capture((at, length) =>
            at == 0x188719C ? new byte[length] : null!));
        Check(earlyGuard.Message.StartsWith("Peace Keepers Locked Chest setup SHA-256 changed:", StringComparison.Ordinal),
            "A null later read cannot overtake the historical first setup hash guard.");
        calls.Clear(); var sentinel = new IOException("state-reader-failure-before-setup-hash");
        var observed = Reject<IOException>(() => NativeLockedChestBehaviorSource.Capture((at, length) =>
        { calls.Add((at, length)); if (calls.Count == 2) throw sentinel; return new byte[length]; }));
        Check(ReferenceEquals(observed, sentinel) && calls.Count == 2,
            "Historical read failure ordering is not replaced by an early setup validation.");
        Check(typeof(NativeLockedChestBehaviorLayout).IsSealed && !typeof(NativeLockedChestBehaviorLayout).IsPublic &&
            typeof(NativeLockedChestBehaviorLayout).GetConstructors().Length == 0,
            "Source-bound Town layout has no caller constructor or public admission API.");
        Check(!typeof(NativeLockedChestBehaviorLayout).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(m => m.Name.Contains("Fresh", StringComparison.Ordinal)), "No Fresh host admission is introduced.");
        Console.WriteLine($"LOCKED_CHEST_BEHAVIOR_FREE_OK checks={_checks} originalOpened=false runtime=false");
    }

    internal static void Source(string image)
    {
        // Existing exact source factory is the only source reader. This mode is
        // read-only and never invokes an exporter or creates a native candidate.
        var closure = NativeTownSquareLockedChestClosureCompiler.Capture(image);
        var captured = closure.FamilySource;
        byte[] Read(long at, int length)
        {
            var read = captured.HandlerReads.Single(r => r.Extent.Offset + 0x187E000 == at && r.Extent.ByteLength == length);
            return captured.ReadSource(read.Extent);
        }
        var source = NativeLockedChestBehaviorSource.Capture(Read);
        var layout = NativeLockedChestBehaviorLayout.CompileTown(source);
        byte[] payload = layout.CopyPayload();
        Check(payload.Length == 0xBC0 && Hash(payload) == PayloadHash &&
            payload.SequenceEqual(TownSquareNativeLockedChestCandidateExporter.BuildHandlerPayload(Read)),
            "Shared Town producer equals the frozen complete pre-extraction payload and existing API.");
        Check(Hash(closure.Overlay) == "fd859bff13985e18c3aff74cc889006c48b2743bf57a2b0f9d80662d25a09c2d" &&
            Hash(closure.Executable) == "4268f9943233b9f8c24d1d4b0924cba8747547b44b82eddd13f0a2ac10ac17de" &&
            closure.Graph.Fingerprint == "d484fc13504ac0afc0b7a63e81dd4a6a3a3a7672dd7959654b02fd6103a3edcc",
            "Complete old Town overlay/executable/dependency graph identities remain unchanged.");
        Check(layout.Owners.Select(o => (o.Id, o.PayloadOffset, o.ByteLength)).SequenceEqual(new[]
        {
            ("chest", 0, 0x740), ("reward", 0x740, 0x438), ("reward-dispatch", 0xB78, 0x14),
            ("chest-dispatch", 0xB8C, 0x14), ("delete-thunk", 0xBA0, 0x10), ("sound-thunk", 0xBB0, 0x10)
        }), "Exact six block owners cover the original BC0 payload without holes or overlap.");
        Check(layout.Entries.Select(e => e.RuntimePc).SequenceEqual(new uint[]
        {
            0x80089ECC, 0x80089ED0, 0x80089F00, 0x8008A60C, 0x8008A610,
            0x8008AA44, 0x8008AA58, 0x8008AA6C, 0x8008AA7C
        }), "Known adapters, source roots, dispatches and thunks are explicit entries, not every interior instruction.");
        Check(payload.AsSpan(0xB78).SequenceEqual(Words(
            0x2402000D, 0x1082FEF0, 0, 0x08021C96, 0,
            0x240200AE, 0x1082FD1B, 0, 0x08021C96, 0,
            0x0C01495A, 0x02602021, 0x08021C96, 0,
            0x0C01569E, 0x24A70054, 0x08021C96, 0)),
            "Literal historical dispatch/delete/sound words preserve call arguments and delay words.");
        Check(Word(payload, 0) == 0x0260A021 && Word(payload, 0x740) == 0x0260A021 &&
            layout.PrivateAdapterFrameByteLength == 0 && layout.LoopContinuationPc == 0x80087258,
            "Chest moves s4=s3 without allocating Wizard's private2B0 frame; its continuation must not use Wizard leave.");
        var originals = layout.RestoreSources(payload);
        Check(originals.Count == 3 && originals.Select(Hash).SequenceEqual(new[]
        {
            "9343243adf185b81f638f37a41be77c61d27508c45158fed1ebe0b9fc6dd97ad",
            "83c507315fcf747b0c94c6bd47c7622e79f38c68ff746421fd9b2021314a479f",
            "129d8511c63925af80ab73a02def446efc39e23974255fd4b8d38f51411c2056"
        }) && originals.Select((bytes, i) => bytes.SequenceEqual(Read(new long[] { 0x188719C, 0x1887330, 0x1881B64 }[i], bytes.Length))).All(x => x),
            "Full inverse restores the three exact retained source blocks and all complements.");
        // Owner edges, each relocation target category, a donor delay slot and
        // one unchanged complement are distinct obligations; complete equality
        // is already the inverse's whole-buffer guard, not thousands of cases.
        HashSet<int> probes = layout.Owners.SelectMany(o => new[] { o.PayloadOffset, o.PayloadOffset + o.ByteLength - 1 }).ToHashSet();
        foreach (var transfer in layout.Transfers.GroupBy(t => t.TargetKind).Select(g => g.First()))
            probes.Add(checked((int)(transfer.FinalPc - 0x80089ECC)));
        var delayTransfer = layout.Transfers.First(t => t.DelayWord is not null);
        probes.Add(checked((int)(delayTransfer.FinalPc - 0x80089ECC)) + 4);
        int complement = Enumerable.Range(1, 0x73C / 4 - 1).Select(i => i * 4)
            .First(at => !layout.Transfers.Any(t => t.FinalPc == 0x80089ECC + at || t.FinalPc + 4 == 0x80089ECC + at));
        probes.Add(complement);
        foreach (int at in probes)
        { byte[] changed = payload.ToArray(); changed[at] ^= 1; Reject<InvalidDataException>(() => layout.RestoreSources(changed)); }
        Reject<InvalidDataException>(() => layout.RestoreSources(payload[..^1]));
        Reject<InvalidDataException>(() => layout.RestoreSources([.. payload, 0]));
        Check(!layout.CompleteExternalOwnersProven && !layout.RuntimeVerified && !layout.PublicationApproved &&
            layout.Transfers.Any(t => t.TargetKind == NativeLockedChestTargetKind.RetainedUnprovenCall),
            "Relocation and old output goldens do not certify unresolved global calls or runtime/publication.");
        byte[] detached = layout.CopyPayload(); detached[0] ^= 1; originals[0][0] ^= 1;
        Check(Hash(layout.CopyPayload()) == PayloadHash && Hash(layout.RestoreSources(payload)[0]) ==
            "9343243adf185b81f638f37a41be77c61d27508c45158fed1ebe0b9fc6dd97ad", "Layout byte owners remain immutable.");
        Console.WriteLine($"LOCKED_CHEST_BEHAVIOR_SOURCE_OK checks={_checks} payload={Hash(payload)} nativeOutputWritten=false runtime=false");
    }

    private static T Reject<T>(Action action) where T : Exception
    { try { action(); } catch (T error) { _checks++; return error; } throw new InvalidDataException("Expected " + typeof(T).Name); }
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidDataException(message); _checks++; }
    private static byte[] Words(params uint[] words)
    { byte[] bytes = new byte[words.Length * 4]; for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), words[i]); return bytes; }
    private static uint Word(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

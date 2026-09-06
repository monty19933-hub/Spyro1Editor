using Spyro.Editor.Core.Exporting;

internal static class CompositionCases
{
    public static int Run()
    {
        int checks = 0;
        byte[] original = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        byte[] first = [0, 1, 20, 21, 22, 23, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
        byte[] second = [0, 1, 2, 21, 30, 31, 32, 33, 8, 9, 10, 11, 12, 13, 14, 15];
        byte[] expected = [0, 1, 2, 21, 30, 31, 32, 33, 8, 9, 10, 11, 40, 41, 42, 43];
        NativeExactByteStage a = Stage("first", original, first, (2, 4));
        NativeExactByteStage b = Stage("second", first, second, (2, 1), (4, 4));
        NativeExactByteStage c = Stage("third", second, expected, (12, 4));
        NativeExactByteComposition result = NativeExactByteComposition.Compile(original, [a, b, c]);
        Check(result.CopyOutput().SequenceEqual(expected), "sequential overlap and restoration literal afterimage");
        Check(result.ApplyExact(original).SequenceEqual(expected), "source-bound direct forward");
        Check(result.ApplyExact(expected, reverse: true).SequenceEqual(original), "whole-file direct inverse");
        Check(result.OwnedRanges.All(r => r.DataRelativeOffset > 2), "restored byte leaves final ownership");
        Check(result.OwnedRanges.SelectMany(r => Enumerable.Range(r.DataRelativeOffset, r.ByteLength))
            .SequenceEqual(new[] { 3, 4, 5, 6, 7, 12, 13, 14, 15 }), "literal complete final changed-byte ownership");
        Check(result.DeterministicSha256 == NativeExactByteComposition.Compile(original, [a, b, c]).DeterministicSha256,
            "deterministic repeated compilation");
        Check(result.StageCount == 3, "all stage evidence retained");
        byte[] escaped = result.CopyOutput(); escaped[3] ^= 1;
        Check(result.CopyOutput().SequenceEqual(expected), "result does not expose mutable output");
        byte[] tampered = original.ToArray(); tampered[0] ^= 1;
        Reject(() => result.ApplyExact(tampered), "complete protected preimage");
        tampered = expected.ToArray(); tampered[0] ^= 1;
        Reject(() => result.ApplyExact(tampered, true), "complete inverse preimage");
        Reject(() => NativeExactByteComposition.Compile(original, [b]), "out-of-order stale stage");
        Reject(() => NativeExactByteComposition.Compile(original, [a, a]), "duplicate or stale stage");
        Reject(() => NativeExactByteComposition.Compile(original, []), "empty sequence");
        Reject(() => NativeExactByteComposition.Compile(original, [Stage("missing", original, first, (2, 2))]),
            "undeclared output change");
        Reject(() => NativeExactByteComposition.Compile(original,
            [new("resizing", original, first[..^1], a.Ranges)]), "stage resize");
        Reject(() => NativeExactByteComposition.Compile(original,
            [new("wrong-window", original, first, [new("range", "owner", 2, [99, 99, 99, 99], [20, 21, 22, 23])])]),
            "stale owned window");
        Reject(() => NativeExactByteComposition.Compile(original,
            [Stage("overlap", original, first, (2, 3), (4, 2))]), "intrastage overlap");
        Reject(() => NativeExactByteComposition.Compile(original,
            [new("bad-bounds", original, first, [new("bad", "owner", 15, [15, 16], [20, 21])])]), "unbounded range");
        Reject(() => NativeExactByteComposition.Compile(original,
            [new("duplicate", original, first, [a.Ranges[0], a.Ranges[0]])]), "duplicate range identity");
        NativeExactByteComposition restored = NativeExactByteComposition.Compile(original,
            [a, Stage("restore", first, original, (2, 4))]);
        Check(restored.OwnedRanges.Count == 0 && restored.CopyOutput().SequenceEqual(original) &&
            restored.ApplyExact(original, true).SequenceEqual(original), "complete stage cancellation retains exact inverse");

        // Same numeric offsets in independent domains are not aliases. The
        // existing typed mapper must preserve identity by object, not labels.
        NativeAddressSpace wad = new(NativeAddressKind.WadFile, "equal-label", NativeAddressRevision.Original, 16);
        NativeAddressSpace exe = new(NativeAddressKind.ExecutableFile, "equal-label", NativeAddressRevision.Original, 16);
        NativeAddressSpace disc = new(NativeAddressKind.DiscLogical, "disc", NativeAddressRevision.Final, 64);
        NativeRelocationMap wadMap = new(wad, disc, [new("wad", new(wad, 0, 16), new(disc, 0, 16))]);
        NativeRelocationMap exeMap = new(exe, disc, [new("exe", new(exe, 0, 16), new(disc, 32, 16))]);
        Check(wadMap.Map(new(wad, 4, 4)).Final.Offset == 4 && exeMap.Map(new(exe, 4, 4)).Final.Offset == 36,
            "equal numeric file offsets remain distinct native domains");
        Reject(() => wadMap.Map(new(exe, 4, 4)), "foreign file domain");
        NativeRelocationMap.RequireDisjoint([wadMap.Map(new(wad, 4, 4)).Final, exeMap.Map(new(exe, 4, 4)).Final]);
        checks++;
        return checks;

        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("Composition failed: " + label);
            checks++;
        }
        void Reject(Action action, string label)
        {
            try { action(); }
            catch (InvalidDataException) { checks++; return; }
            throw new InvalidOperationException("Composition accepted: " + label);
        }
    }

    private static NativeExactByteStage Stage(string id, byte[] before, byte[] after, params (int Offset, int Length)[] ranges) =>
        new(id, before, after, ranges.Select((r, i) => new Id65AuthoringCompositeOwnedRange(
            id + ":" + i, id, r.Offset, before.AsSpan(r.Offset, r.Length).ToArray(), after.AsSpan(r.Offset, r.Length).ToArray())).ToArray());
}

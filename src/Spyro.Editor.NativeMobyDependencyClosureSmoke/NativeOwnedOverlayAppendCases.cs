using Spyro.Editor.Core.Exporting;

internal static class NativeOwnedOverlayAppendCases
{
    private const uint Base = 0x8007AA38;

    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        int observed = 0; bool disposed = false;
        byte[] prefix = [1, 2, 3, 4, 5, 6, 7, 8];
        NativeOwnedOverlayBlockSize[] sizes = [new("code", 8), new("leave", 4)];
        var layout = NativeOwnedOverlayAppendLayout.Create("neutral-overlay", prefix, Base, 0x800, sizes, "padding");
        check(!layout.SourceCertified && layout.RetainedByteLength == 8 && layout.UsedByteLength == 20 &&
            layout.FinalByteLength == 0x800 && layout.CopyAnchor == Base + 0x800 &&
            layout.Domain.Kind == NativeAddressKind.LevelData && layout.Domain.Revision == NativeAddressRevision.Final &&
            layout.Domain.ByteLength == 0x800, "Neutral layout derives exact extent and runtime geometry without source authority.");
        check(layout.Blocks.Select(b => (b.Id, b.Owner.Offset, b.Owner.ByteLength, b.IsPadding)).SequenceEqual(new[]
            { ("code", 8L, 8L, false), ("leave", 16L, 4L, false), ("padding", 20L, 2028L, true) }),
            "Ordered real blocks and explicit sector padding exactly partition the appended owner.");
        sizes[0] = new("changed", 4); prefix[0] = 99;
        check(layout.Blocks[0].Id == "code" && layout.CopyRetainedPrefix()[0] == 1,
            "Layout snapshots caller sizes and the complete retained prefix.");
        var copy = layout.CopyRetainedPrefix(); copy[0] = 88;
        check(layout.CopyRetainedPrefix()[0] == 1, "Retained prefix copies are detached.");
        byte[] lazyPrefix = [1, 2, 3, 4];
        var lazy = NativeOwnedOverlayAppendLayout.Create("lazy-prefix", lazyPrefix, Base, 0x800, MutatingSizes(), "padding");
        check(lazyPrefix[0] == 99 && lazy.CopyRetainedPrefix()[0] == 1,
            "Complete retained prefix is frozen before the first call into the custom size enumerator.");
        var code = layout.Blocks[0].Owner; var leave = layout.Blocks[1].Owner;
        byte[] codeBytes = [11, 12, 13, 14, 15, 16, 17, 18], leaveBytes = [21, 22, 23, 24];
        NativeOwnedOverlayBlock[] blocks = [new("code", code, codeBytes), new("leave", leave, leaveBytes)];
        NativeOwnedOverlayEntry[] entries = [new("leave-entry", "leave", leave)];
        byte[] image = layout.Materialize(blocks, entries, [leave]);
        check(image.Length == 0x800 && image.AsSpan(0, 8).SequenceEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }) &&
            image.AsSpan(8, 8).SequenceEqual(codeBytes) && image.AsSpan(16, 4).SequenceEqual(leaveBytes) &&
            !image.AsSpan(20).ContainsAnyExcept((byte)0), "Shared materializer preserves prefix, writes owners and zeroes only its padding.");
        check(layout.Materialize(blocks.Reverse(), entries, [leave]).SequenceEqual(image), "Payload enumeration order does not change assigned placement.");
        byte[] expected = image.ToArray(); image[0] ^= 1; image[8] ^= 1;
        check(layout.Materialize(blocks, entries, [leave]).SequenceEqual(expected), "Returned materialization never aliases retained layout state.");
        byte[] temporal = layout.Materialize(MutatingPayloads(), MutatingEntries(), [leave]);
        check(temporal.SequenceEqual(expected), "Each payload is copied before caller enumeration resumes and before later entry enumeration.");
        codeBytes[0] = 11; leaveBytes[0] = 21;

        var other = new NativeAddressSpace(layout.Domain.Kind, layout.Domain.OwnerIdentity, layout.Domain.Revision, layout.Domain.ByteLength);
        reject(() => layout.Materialize([blocks[0] with { Owner = new(other, 8, 8) }, blocks[1]], entries, [leave]), "domain");
        reject(() => layout.Materialize(blocks, [new("leave-entry", "leave", new(other, 16, 4))], [leave]), "domain");
        reject(() => layout.Materialize(blocks, entries, [new(other, 16, 4)]), "domain");
        reject(() => layout.Materialize(blocks, [], [leave]), "entry");
        reject(() => layout.Materialize(blocks, [new("code-entry", "code", new(layout.Domain, 8, 8))], [new(layout.Domain, 12, 4)]), "entry");
        reject(() => layout.Materialize(blocks, [new("wrong-block", "leave", new(layout.Domain, 8, 4))], []), "entry");
        reject(() => layout.Materialize(blocks, [entries[0], entries[0]], []), "duplicate");
        reject(() => layout.Materialize(blocks, [new("padding-entry", "padding", new(layout.Domain, 20, 4))], []), "padding");
        reject(() => layout.Materialize(blocks, [], [new(layout.Domain, 0, 4)]), "arena");
        reject(() => layout.Materialize([blocks[0]], [], []), "coverage");
        reject(() => layout.Materialize([blocks[0], blocks[0]], [], []), "duplicate");
        reject(() => layout.Materialize([blocks[0] with { Bytes = new byte[4] }, blocks[1]], [], []), "length");
        reject(() => layout.Materialize([blocks[0] with { Id = "unknown" }, blocks[1]], [], []), "owner");
        reject(() => layout.Materialize([blocks[0] with { Owner = new(layout.Domain, 12, 8) }, blocks[1]], [], []), "owner");
        reject(() => layout.Materialize([blocks[0], blocks[1], new("padding", layout.Blocks[2].Owner, new byte[2028])], [], []), "padding");

        var exact = NativeOwnedOverlayAppendLayout.Create("exact-sector", new byte[0x7F8], Base, 0x800, [new("code", 8)], "padding");
        check(exact.Blocks.Count == 1 && exact.UsedByteLength == exact.FinalByteLength &&
            exact.Materialize([new("code", exact.Blocks[0].Owner, new byte[8])], [], []).Length == 0x800,
            "An exact sector needs no invalid zero-length padding owner.");
        var edge = NativeOwnedOverlayAppendLayout.Create("last-ram-sector", new byte[0x7FC], 0x801FF800, 0x800, [new("code", 4)], "padding");
        check(edge.CopyAnchor == 0x80200000, "End-exclusive native RAM boundary is checked without overflowing runtime arithmetic.");
        foreach (int retained in new[] { 0xB000, 0xF800, 0x11000 })
        {
            byte[] actualPrefix = Enumerable.Range(0, retained).Select(i => (byte)(i * 17)).ToArray();
            var next = NativeOwnedOverlayAppendLayout.Create("chained-overlay", actualPrefix, Base, 0x12000, [new("next-family", 0xBC0)], "padding");
            check(next.Blocks[0].Owner.Offset == retained && next.RetainedByteLength == actualPrefix.Length,
                "Append begins after the complete actual input allocation, independent of prior used length.");
            byte[] composed = next.Materialize([new("next-family", next.Blocks[0].Owner, new byte[0xBC0])], [], []);
            check(composed.AsSpan(0, retained).SequenceEqual(actualPrefix), "Every retained byte, including prior padding, survives chaining.");
            if (retained == 0x11000)
                check(next.UsedByteLength == 0x11BC0 && next.FinalByteLength == 0x12000 && next.CopyAnchor == 0x8008CA38 &&
                    next.Blocks[0].Owner.Offset != 0x10DB4, "Current Host chaining never reclaims 0x10DB4..0x11000 or historical 0xF494.");
        }

        reject(() => Create([], [new("code", 4)]), "prefix");
        reject(() => Create(new byte[7], [new("code", 4)]), "align");
        reject(() => Create(new byte[8], []), "block");
        reject(() => Create(new byte[8], [new("code", 4), new("code", 4)]), "duplicate");
        reject(() => Create(new byte[8], [new("padding", 4)]), "padding");
        reject(() => Create(new byte[8], [new(" ", 4)]), "identity");
        foreach (int length in new[] { int.MinValue, -4, 0, 2, 6, int.MaxValue })
            reject(() => Create(new byte[8], [new("code", length)]), "length");
        reject(() => Create(new byte[8], [new("code", 0x20004)]), "budget");
        reject(() => NativeOwnedOverlayAppendLayout.Create("small", new byte[8], Base, 0x7FF, [new("code", 4)], "padding"), "capacity");
        reject(() => NativeOwnedOverlayAppendLayout.Create("growth", new byte[0x800], Base, 0x800, [new("code", 4)], "padding"), "capacity");
        foreach (uint badBase in new[] { 0u, 0x7FFFFFFCu, Base + 1, 0x801FF804u, 0xFFFFF800u })
            reject(() => NativeOwnedOverlayAppendLayout.Create("bad-base", new byte[8], badBase, 0x800, [new("code", 4)], "padding"), "runtime");
        foreach (int ceiling in new[] { int.MinValue, -1, 0, int.MaxValue })
            reject(() => NativeOwnedOverlayAppendLayout.Create("bad-ceiling", new byte[8], Base, ceiling, [new("code", 4)], "padding"), "capacity");
        var maximum = NativeOwnedOverlayAppendLayout.Create("64-blocks", new byte[0x700], Base, 0x800,
            Enumerable.Range(0, 64).Select(i => new NativeOwnedOverlayBlockSize("b" + i, 4)), "padding");
        check(maximum.Blocks.Count == 64 && maximum.UsedByteLength == 0x800, "The exact 64-block budget is usable without padding.");
        reject(() => Create(new byte[8], Enumerable.Range(0, 64).Select(i => new NativeOwnedOverlayBlockSize("b" + i, 4))), "budget");
        RequireBounded(64, n => Create(new byte[8], Watch(new NativeOwnedOverlayBlockSize("code", 4), n)));
        RequireBounded(64, n => layout.Materialize(Watch(blocks[0], n), [], []));
        RequireBounded(256, n => layout.Materialize(blocks, Watch(entries[0], n), []));
        RequireBounded(1024, n => layout.Materialize(blocks, entries, Watch(leave, n)));
        check(layout.CopyRetainedPrefix()[0] == 1 && layout.Materialize(blocks, entries, [leave]).SequenceEqual(expected),
            "All failed requests leave existing immutable layout and payload result unchanged.");
        Console.WriteLine("NATIVE_OWNED_OVERLAY_APPEND_LAYOUT_AND_MATERIALIZATION_OK");

        NativeOwnedOverlayAppendLayout Create(byte[] retained, IEnumerable<NativeOwnedOverlayBlockSize> requests) =>
            NativeOwnedOverlayAppendLayout.Create("neutral-fixture", retained, Base, 0x30000, requests, "padding");
        IEnumerable<NativeOwnedOverlayBlockSize> MutatingSizes()
        { lazyPrefix[0] = 99; yield return new("code", 4); }
        IEnumerable<NativeOwnedOverlayBlock> MutatingPayloads()
        {
            yield return blocks[0]; codeBytes[0] = 99;
            yield return blocks[1]; leaveBytes[0] = 98;
        }
        IEnumerable<NativeOwnedOverlayEntry> MutatingEntries()
        { codeBytes[1] = 97; yield return entries[0]; codeBytes[1] = 12; }
        IEnumerable<T> Watch<T>(T item, int limit)
        {
            try { for (int i = 0; i < limit + 2; i++) { observed++; yield return item; } }
            finally { disposed = true; }
        }
        void RequireBounded(int maximumCount, Action<int> run)
        {
            observed = 0; disposed = false;
            reject(() => run(maximumCount), "budget");
            check(observed == maximumCount + 1 && disposed, "Bounded caller enumeration stops at the first excess item and disposes its iterator.");
        }
    }
}

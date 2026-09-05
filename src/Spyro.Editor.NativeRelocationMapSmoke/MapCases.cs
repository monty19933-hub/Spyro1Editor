using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

internal static class MapCases
{
    internal static void Run(Action<bool, string> check)
    {
        NativeAddressSpace source = Space(NativeAddressRevision.Original, 256);
        NativeAddressSpace final = Space(NativeAddressRevision.Final, 320);
        var declarations = new List<NativeRelocationRegion>
        {
            new("header", Extent(source, 0, 64), Extent(final, 0, 64), 4),
            new("inserted", null, Extent(final, 64, 64), 4),
            new("body", Extent(source, 64, 192), Extent(final, 128, 192), 4)
        };
        var map = new NativeRelocationMap(source, final, declarations);
        declarations.Clear();
        check(map.Regions.Count == 3 && map.Map(Extent(source, 64, 4)).Final.Offset == 128,
            "The map retained mutable declaration storage or mapped the insertion boundary incorrectly.");
        Reject(() => map.Map(Extent(final, 64, 4)));
        Reject(() => map.Map(Extent(Space(NativeAddressRevision.Original, 256), 64, 4)));
        Reject(() => map.Map(Extent(source, 63, 2)));
        Reject(() => Extent(source, -1, 4));
        Reject(() => Extent(source, 254, 4));
        Reject(() => Extent(source, 0, 0));
        Reject(() => Extent(source, long.MaxValue - 1, 4));
        Reject(() => new NativeAddressSpace((NativeAddressKind)99, "scene", NativeAddressRevision.Original, 4));
        Reject(() => new NativeAddressSpace(NativeAddressKind.Scene, "", NativeAddressRevision.Original, 4));
        Reject(() => new NativeRelocationMap(final, source, map.Regions));
        Reject(() => new NativeRelocationMap(source, final, []));
        Reject(() => new NativeRelocationMap(source, final, [map.Regions[0], map.Regions[0]]));
        Reject(() => new NativeRelocationMap(source, final, [new("bad", Extent(source, 0, 4), Extent(final, 0, 8))]));
        Reject(() => new NativeRelocationMap(source, final, [new("bad", Extent(source, 1, 4), Extent(final, 0, 4), 4)]));
        Reject(() => new NativeRelocationMap(source, final, [new("bad", Extent(source, 0, 4), Extent(final, 0, 4), 3)]));
        Reject(() => new NativeRelocationMap(source, final,
            [new("a", Extent(source, 0, 8), Extent(final, 0, 8)), new("b", Extent(source, 4, 8), Extent(final, 20, 8))]));
        Reject(() => new NativeRelocationMap(source, final,
            [new("a", Extent(source, 0, 8), Extent(final, 0, 8)), new("b", Extent(source, 20, 8), Extent(final, 4, 8))]));
        Reject(() => NativeRelocationMap.RequireDisjoint([Extent(final, 4, 8), Extent(final, 8, 4)]));
        NativeRelocationMap.RequireDisjoint([Extent(final, 4, 4), Extent(final, 8, 4)]);
        check(true, "Adjacent writes were rejected.");

        NativeResolvedReference reference = map.ResolveReference(Extent(source, 16, 4), Extent(source, 64, 8), Word(64), new());
        check(reference.Field.Final.Offset == 16 && reference.Target.Final.Offset == 128 && reference.Before == 64 && reference.After == 128,
            "A scene reference did not preserve its field and relocate its declared target.");
        reference = map.ResolveReference(Extent(source, 16, 4), Extent(source, 64, 8), Word(0x80000040), new(0x80000000, 0x80000000));
        check(reference.After == 0x80000080, "Runtime-base pointer encoding changed.");
        Reject(() => map.ResolveReference(Extent(source, 16, 4), Extent(source, 64, 8), Word(65), new()));
        Reject(() => map.ResolveReference(Extent(source, 17, 4), Extent(source, 64, 8), Word(64), new()));
        Reject(() => map.ResolveReference(Extent(source, 16, 4), Extent(source, 63, 2), Word(63), new()));
        Reject(() => map.ResolveReference(Extent(source, 16, 4), Extent(source, 0, 4), Word(0), new()));
        check(map.ResolveReference(Extent(source, 16, 4), Extent(source, 0, 4), Word(0), new(ZeroIsValidAddress: true)).After == 0,
            "An explicitly non-null zero address could not be represented.");
        Reject(() => map.ResolveReference(Extent(source, 16, 4), Extent(source, 65, 4), Word(0), new(AddressUnitBytes: 4)));
        Reject(() => map.ResolveReference(Extent(source, 16, 4), Extent(source, 64, 4), Word(64), new(FinalBase: uint.MaxValue)));

        NativeAddressSpace largeOriginal = Space(NativeAddressRevision.Original, long.MaxValue);
        NativeAddressSpace largeFinal = Space(NativeAddressRevision.Final, long.MaxValue);
        var high = new NativeRelocationMap(largeOriginal, largeFinal,
            [new("high", Extent(largeOriginal, long.MaxValue - 16, 8), Extent(largeFinal, long.MaxValue - 12, 8))]);
        check(high.Map(Extent(largeOriginal, long.MaxValue - 12, 4)).Final.Offset == long.MaxValue - 8,
            "A valid high-offset mapping overflowed before subtracting its original base.");

        NativeAddressSpace file = new(NativeAddressKind.ExecutableFile, "synthetic-exe-proof", NativeAddressRevision.Original, 0x66000);
        NativeAddressSpace disc = new(NativeAddressKind.DiscLogical, "synthetic-disc-proof", NativeAddressRevision.Final, 60000L * 2048);
        var placement = new NativeRelocationMap(file, disc,
            [new("executable", Extent(file, 0, file.ByteLength), Extent(disc, 55382L * 2048, file.ByteLength), 2048)]);
        check(placement.Map(Extent(file, 0x5F828, 4)).Final.Offset == 55382L * 2048 + 0x5F828,
            "EXE-file placement was confused with a WAD-relative address.");
        NativeRelocationMap.RequireDisjoint([Extent(disc, 55381L * 2048, 2048), placement.Map(Extent(file, 0, 4)).Final]);
        Reject(() => NativeRelocationMap.RequireDisjoint([Extent(disc, 55382L * 2048, 4), placement.Map(Extent(file, 0, 4)).Final]));
        check(true, "Distinct file extents could not be composed in one final disc domain.");

        NativeAddressSpace sectorOriginal = Space(NativeAddressRevision.Original, 0x2000);
        NativeAddressSpace sectorFinal = Space(NativeAddressRevision.Final, 0x3000);
        var sectorMap = new NativeRelocationMap(sectorOriginal, sectorFinal,
            [new("file", Extent(sectorOriginal, 0, 0x2000), Extent(sectorFinal, 0x800, 0x2000), 2048)]);
        reference = sectorMap.ResolveReference(Extent(sectorOriginal, 16, 4), Extent(sectorOriginal, 0x800, 4), Word(1), new(AddressUnitBytes: 2048));
        check(reference.After == 2 && reference.Field.Final.Offset == 0x810, "Sector-index references used byte-offset encoding.");

        NativeGrowingExtent table = NativeRelocationMap.Table(new("table", Extent(source, 16, 32), 32, 16, 2, 3, 4));
        NativeRelocationMap tableMap = NativeRelocationMap.ForGrowth(source, final, [table]);
        check(tableMap.Regions.Count == 2 && tableMap.Map(Extent(source, 16, 32)).Final.Offset == 32 &&
              tableMap.Regions.Single(item => item.Original == null).Final == Extent(final, 64, 16),
            "Table append did not preserve complete old rows and identify newly owned capacity.");
        Reject(() => NativeRelocationMap.Table(new("bad", Extent(source, 16, 31), 32, 16, 2, 3)));
        Reject(() => NativeRelocationMap.Table(new("bad", Extent(source, 16, 32), 32, 16, 2, 1)));
        Reject(() => NativeRelocationMap.ForGrowth(source, final, [new("bad", Extent(source, 16, 32), 32, [new(8, 4), new(8, 4)])]));
        Reject(() => NativeRelocationMap.ForGrowth(source, final, [new("bad", Extent(source, 16, 32), 32, [new(33, 4)])]));
        Reject(() => NativeRelocationMap.ForGrowth(source, final, [new("bad", Extent(source, 16, 32), 32, [new(8, -4)])]));
        Reject(() => NativeRelocationMap.ForGrowth(source, final, [new("bad", Extent(source, 16, 32), 32, [new(8, 3, 4)])]));
        Reject(() => NativeRelocationMap.ForGrowth(source, final, [new("bad", Extent(source, 16, 32), 300, [])]));
        Reject(() => NativeRelocationMap.ForGrowth(source, final, [null!]));
        Reject(() => NativeRelocationMap.RequireDisjoint([null!]));

        // Independent byte-splice oracle: List.InsertRange constructs the final
        // content, and unique original byte identities locate mapped bytes.
        for (int seed = 1; seed <= 16; seed++)
        {
            int firstGrowth = seed % 7 + 1, secondGrowth = seed % 5 + 1;
            NativeAddressSpace beforeSpace = Space(NativeAddressRevision.Original, 128);
            NativeAddressSpace afterSpace = Space(NativeAddressRevision.Final, 128 + firstGrowth + secondGrowth);
            byte[] before = Enumerable.Range(1, 128).Select(item => (byte)item).ToArray();
            List<byte> after = before.ToList();
            after.InsertRange(64, Enumerable.Repeat((byte)255, secondGrowth));
            after.InsertRange(16, Enumerable.Repeat((byte)255, firstGrowth));
            NativeRelocationMap splice = NativeRelocationMap.ForGrowth(beforeSpace, afterSpace,
                [new("body", Extent(beforeSpace, 0, 128), 0, [new(64, secondGrowth), new(16, firstGrowth)])]);
            for (int start = 0; start < 128; start++)
                for (int length = 1; length <= Math.Min(5, 128 - start); length++)
                {
                    bool crosses = start < 16 && start + length > 16 || start < 64 && start + length > 64;
                    if (crosses) Reject(() => splice.Map(Extent(beforeSpace, start, length)));
                    else
                    {
                        NativeMappedExtent mapped = splice.Map(Extent(beforeSpace, start, length));
                        check(mapped.Final.Offset == after.IndexOf(before[start]) &&
                              after.Skip((int)mapped.Final.Offset).Take(length).SequenceEqual(before.Skip(start).Take(length)),
                            "Typed mapping disagrees with independent byte-splice reconstruction.");
                    }
                }
        }

        void Reject(Action action)
        {
            bool rejected = false;
            try { action(); }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException or OverflowException) { rejected = true; }
            check(rejected, "An invalid or conflicting typed relocation/reference was accepted.");
        }
    }
    private static NativeAddressSpace Space(NativeAddressRevision revision, long length) => new(NativeAddressKind.Scene, "synthetic-scene", revision, length);
    private static NativeByteExtent Extent(NativeAddressSpace space, long offset, long length) => new(space, offset, length);
    private static byte[] Word(uint value) { byte[] bytes = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, value); return bytes; }
}

using System.Buffers.Binary;
using System.Collections;
using Spyro.Editor.Core.Scene;

internal static class CreationCases
{
    internal static void Run(Action<bool, string> require, Action<Action, string> reject)
    {
        NativeCollisionCell first = new(0, 0, 0), neighbor = new(1, 0, 0);
        var oneCell = new Dictionary<NativeCollisionCell, IReadOnlyList<int>>
        { [first] = new[] { 1, 0 } };
        NativeCollisionIndexSnapshot one = NativeCollisionIndexCodec.Create(oneCell, 2);
        require(one.CopyTree().SequenceEqual(Words(1, 4, 1, 8, 1, 0)) &&
            one.CopyBlocks().SequenceEqual(Words(0x8001, 0, 0x8000, 0)) &&
            one.UsedBlockBytes == 6, "creation preserves exact single-floor native bytes");
        AssertCells(one, oneCell, require);

        var twoCells = new Dictionary<NativeCollisionCell, IReadOnlyList<int>>
        { [first] = new[] { 3, 2 }, [neighbor] = new[] { 1, 0 } };
        NativeCollisionIndexSnapshot two = NativeCollisionIndexCodec.Create(twoCells, 4);
        require(two.CopyTree().SequenceEqual(Words(1, 4, 1, 8, 2, 0, 2, 0)) &&
            two.CopyBlocks().SequenceEqual(Words(0x8003, 2, 0x8001, 0, 0x8000, 0)) &&
            two.UsedBlockBytes == 10 && two.LeafPointerOffsets.Count == 2,
            "creation allocates two actual adjacent cell leaves with word-based groups");
        AssertCells(two, twoCells, require);
        reject(() => NativeCollisionIndexCodec.Rebuild(one, twoCells),
            "creation does not widen the existing fixed-capacity rebuild");

        // One triangle crosses the cell seam: both real cell leaves must retain it.
        var seamCells = new Dictionary<NativeCollisionCell, IReadOnlyList<int>>
        { [first] = new[] { 2, 0 }, [neighbor] = new[] { 1, 0 } };
        NativeCollisionIndexSnapshot seam = NativeCollisionIndexCodec.Create(seamCells, 3);
        require(seam.Cells[first].OrderedTriangleIndexes.Contains(0) &&
            seam.Cells[neighbor].OrderedTriangleIndexes.Contains(0),
            "seam triangle remains indexed in both neighboring cells");
        AssertCells(seam, seamCells, require);

        // Canonical physical order is Z root, ascending-Z Y segments, then
        // ascending-(Z,Y) X segments. Absent branches and X holes are 0xFFFF.
        var multiZCells = new Dictionary<NativeCollisionCell, IReadOnlyList<int>>
        {
            [new(2, 1, 3)] = new[] { 1, 0 },
            [new(0, 2, 1)] = new[] { 3 },
            [new(1, 0, 0)] = new[] { 4, 2 }
        };
        NativeCollisionIndexSnapshot multiZ = NativeCollisionIndexCodec.Create(multiZCells, 5);
        require(multiZ.CopyTree().SequenceEqual(Words(
                4, 10, 14, 0xFFFF, 22,
                1, 28,
                3, 0xFFFF, 0xFFFF, 34,
                2, 0xFFFF, 38,
                2, 0xFFFF, 0,
                1, 2,
                3, 0xFFFF, 0xFFFF, 3,
                0)) &&
            multiZ.CopyBlocks().SequenceEqual(Words(0x8004, 2, 0x8003, 0x8001, 0, 0x8000)) &&
            multiZ.LeafPointerOffsets.Count == 6,
            "creation writes actual sparse multi-Z and multi-Y segments with zero padding");
        AssertCells(multiZ, multiZCells, require);
        NativeCollisionIndexSnapshot reordered = NativeCollisionIndexCodec.Create(
            multiZCells.Reverse().ToDictionary(p => p.Key, p => p.Value), 5);
        require(reordered.CopyTree().SequenceEqual(multiZ.CopyTree()) &&
            reordered.CopyBlocks().SequenceEqual(multiZ.CopyBlocks()),
            "creation is independent of caller cell insertion order");

        NativeCollisionIndexSnapshot empty = NativeCollisionIndexCodec.Create(
            Array.Empty<KeyValuePair<NativeCollisionCell, IReadOnlyList<int>>>(), 0);
        require(empty.CopyTree().SequenceEqual(Words(0, 0)) &&
            empty.CopyBlocks().SequenceEqual(Words(0x8000, 0)) && empty.UsedBlockBytes == 2 &&
            empty.Cells.Count == 0 && empty.LeafPointerOffsets.Count == 0 && empty.GroupStartCount == 1,
            "empty creation has minimal aligned zero tree and exact lone terminal");

        int[] sharedSequence = [3, 1];
        var sharedCells = new Dictionary<NativeCollisionCell, IReadOnlyList<int>>
        { [first] = sharedSequence, [new(0, 1, 0)] = new[] { 3, 1 } };
        NativeCollisionIndexSnapshot shared = NativeCollisionIndexCodec.Create(sharedCells, 4);
        NativeCollisionCell otherRow = new(0, 1, 0);
        require(shared.Cells[first].SourceBlockWordOffset == shared.Cells[otherRow].SourceBlockWordOffset &&
            shared.Cells[first].TreePointerByteOffset != shared.Cells[otherRow].TreePointerByteOffset &&
            shared.UsedBlockBytes == 6 && shared.GroupStartCount == 2,
            "equal sequences share one block group without aliasing independently editable leaves");
        byte[] sharedTree = shared.CopyTree(), sharedBlocks = shared.CopyBlocks();
        sharedSequence[0] = 0;
        sharedCells.Clear();
        byte[] escapedTree = shared.CopyTree(), escapedBlocks = shared.CopyBlocks();
        escapedTree[0] = 0; escapedBlocks[0] = 0;
        require(shared.CopyTree().SequenceEqual(sharedTree) && shared.CopyBlocks().SequenceEqual(sharedBlocks) &&
            shared.Cells[first].OrderedTriangleIndexes.SequenceEqual(new[] { 3, 1 }) &&
            shared.Cells is IDictionary map && map.IsReadOnly &&
            shared.Cells[first].OrderedTriangleIndexes is IList list && list.IsReadOnly,
            "created snapshot owns immutable cells, sequences, and native byte buffers");
        NativeCollisionIndexSnapshot unshared = NativeCollisionIndexCodec.Rebuild(shared,
            new Dictionary<NativeCollisionCell, IReadOnlyList<int>> { [otherRow] = new[] { 3, 1 } });
        require(unshared.Cells.Count == 1 && unshared.Cells.ContainsKey(otherRow) &&
            shared.CopyTree().SequenceEqual(sharedTree) && shared.CopyBlocks().SequenceEqual(sharedBlocks),
            "retiring one generated shared-group leaf preserves its neighbor and original snapshot");

        var unorderedCells = new Dictionary<NativeCollisionCell, IReadOnlyList<int>>
        { [first] = new[] { 0, 2, 1, 2 } };
        NativeCollisionIndexSnapshot unordered = NativeCollisionIndexCodec.Create(unorderedCells, 3, false);
        require(!unordered.RequiresDescendingOrder &&
            unordered.Cells[first].OrderedTriangleIndexes.SequenceEqual(new[] { 0, 2, 1, 2 }) &&
            unordered.CopyBlocks().SequenceEqual(Words(0x8000, 2, 1, 2, 0x8000, 0)),
            "explicit unordered creation preserves original triangle order and duplicates");
        AssertCells(unordered, unorderedCells, require);
        reject(() => NativeCollisionIndexCodec.Create(unorderedCells, 3),
            "descending creation never silently sorts caller triangle order");

        var maximumCell = new Dictionary<NativeCollisionCell, IReadOnlyList<int>>
        { [new(255, 255, 255)] = new[] { 0x7FFF, 0 } };
        NativeCollisionIndexSnapshot maximum = NativeCollisionIndexCodec.Create(maximumCell, 0x8000);
        require(maximum.CopyTree().Length == 1544 && Word(maximum.CopyTree(), 0) == 256 &&
            Word(maximum.CopyTree(), 512) == 514 && Word(maximum.CopyTree(), 1026) == 1028 &&
            Word(maximum.CopyTree(), 1540) == 0 && Word(maximum.CopyTree(), 1542) == 0 &&
            maximum.CopyBlocks().SequenceEqual(Words(0xFFFF, 0, 0x8000, 0)),
            "maximum native coordinates and triangle use distinct byte links, leaf words, and marker context");
        AssertCells(maximum, maximumCell, require);
        foreach (NativeCollisionCell invalidCell in new NativeCollisionCell[]
                 { new(-1, 0, 0), new(0, -1, 0), new(0, 0, -1),
                   new(256, 0, 0), new(0, 256, 0), new(0, 0, 256), new(int.MaxValue, 0, 0) })
            reject(() => NativeCollisionIndexCodec.Create(
                new Dictionary<NativeCollisionCell, IReadOnlyList<int>> { [invalidCell] = new[] { 0 } }, 1),
                "creation refuses non-native cell coordinate");
        foreach (int count in new[] { -1, 0x8001, int.MaxValue })
            reject(() => NativeCollisionIndexCodec.Create(oneCell, count), "creation triangle-count range");
        foreach (IReadOnlyList<int> sequence in new IReadOnlyList<int>[]
                 { Array.Empty<int>(), new[] { -1 }, new[] { 2 }, new[] { 0, 1 }, new[] { 1, 1 }, null! })
            reject(() => NativeCollisionIndexCodec.Create(
                new Dictionary<NativeCollisionCell, IReadOnlyList<int>> { [first] = sequence }, 2),
                "creation refuses empty, null, invalid, or non-descending sequence");
        reject(() => NativeCollisionIndexCodec.Create(oneCell, 0), "no reference in zero-triangle pool");
        reject(() => NativeCollisionIndexCodec.Create(new[]
            { KeyValuePair.Create(first, (IReadOnlyList<int>)new[] { 0 }),
              KeyValuePair.Create(first, (IReadOnlyList<int>)new[] { 0 }) }, 1),
            "duplicate enumerable cells are rejected even when equal");

        // Exactly fill the native 64 KiB tree, including the final leaf at 0xFFFE.
        var fullTreeCells = Enumerable.Range(0, 127).ToDictionary(
            y => new NativeCollisionCell(y == 126 ? 254 : 255, y, 0),
            _ => (IReadOnlyList<int>)new[] { 0 });
        NativeCollisionIndexSnapshot fullTree = NativeCollisionIndexCodec.Create(fullTreeCells, 1);
        require(fullTree.CopyTree().Length == 0x10000 && Word(fullTree.CopyTree(), 0xFFFE) == 0 &&
            fullTree.Cells.Count == 127 && fullTree.LeafPointerOffsets.Count == 32511,
            "creation accepts exact native tree capacity with final representable leaf field");
        AssertCells(fullTree, fullTreeCells, require);
        fullTreeCells.Add(new(255, 126, 0), new[] { 0 });
        reject(() => NativeCollisionIndexCodec.Create(fullTreeCells, 1),
            "one extra tree word is refused before aligned allocation or pointer truncation");

        int[] large = Enumerable.Range(0, 0x8000).Reverse().ToArray();
        int[] almostLarge = Enumerable.Range(0, 0x7FFE).Reverse().ToArray();
        var pointerCells = new Dictionary<NativeCollisionCell, IReadOnlyList<int>>
        { [first] = large, [neighbor] = almostLarge, [new(2, 0, 0)] = new[] { 0 } };
        NativeCollisionIndexSnapshot lastPointer = NativeCollisionIndexCodec.Create(pointerCells, 0x8000);
        require(lastPointer.Cells[new(2, 0, 0)].SourceBlockWordOffset == 0xFFFE &&
            lastPointer.TerminalSentinelWordOffset == 0xFFFF && lastPointer.UsedBlockBytes == 0x20000,
            "last representable block-group pointer differs from terminal position");
        pointerCells[new(2, 0, 0)] = new[] { 1, 0 };
        NativeCollisionIndexSnapshot extendedLastGroup = NativeCollisionIndexCodec.Create(pointerCells, 0x8000);
        require(extendedLastGroup.Cells[new(2, 0, 0)].SourceBlockWordOffset == 0xFFFE &&
            extendedLastGroup.TerminalSentinelWordOffset == 0x10000 &&
            extendedLastGroup.UsedBlockBytes == 0x20002 && extendedLastGroup.CopyBlocks().Length == 0x20004,
            "final group may extend beyond ushort word offsets without truncating its representable start");
        AssertCells(extendedLastGroup, pointerCells, require);
        pointerCells[neighbor] = Enumerable.Range(0, 0x7FFF).Reverse().ToArray();
        reject(() => NativeCollisionIndexCodec.Create(pointerCells, 0x8000),
            "group start at 0xFFFF cannot alias the empty-leaf sentinel");
        require(large.SequenceEqual(Enumerable.Range(0, 0x8000).Reverse()) &&
            almostLarge.SequenceEqual(Enumerable.Range(0, 0x7FFE).Reverse()) && pointerCells.Count == 3,
            "capacity failures preserve caller collections and nested arrays");

        // Adversarial Count/indexers cannot cause preallocation or bypass the
        // actual outer/nested enumerators. Each guard fails loudly if overrun.
        foreach (int count in new[] { 0, -1, int.MaxValue })
        {
            var nested = new ReportedList(new[] { 1, 0 }, count, 3);
            var outer = new ReportedCells(new[] { KeyValuePair.Create(first, (IReadOnlyList<int>)nested) }, count, 2);
            NativeCollisionIndexSnapshot counted = NativeCollisionIndexCodec.Create(outer, 2);
            require(counted.CopyTree().SequenceEqual(one.CopyTree()) &&
                counted.CopyBlocks().SequenceEqual(one.CopyBlocks()) && outer.Steps == 1 && nested.Steps == 2,
                "creation uses actual enumeration independent of deceptive caller Count and indexers");
        }
        var excessiveOuter = new ReportedCells(AllCells(), 0, 0x8001);
        reject(() => NativeCollisionIndexCodec.Create(excessiveOuter, 1),
            "actual outer enumeration cannot grow past representable canonical tree capacity");
        require(excessiveOuter.Steps <= 0x8001, "outer enumeration has an independent finite bound");
        var endless = new ReportedList(EndlessZeroes(), 0, 1_048_577);
        reject(() => NativeCollisionIndexCodec.Create(
            new[] { KeyValuePair.Create(first, (IReadOnlyList<int>)endless) }, 1, false),
            "nonterminating nested sequence is independently bounded even in unordered mode");
        require(endless.Steps == 1_048_577, "nested reference budget rejects before unbounded enumeration");
        int[] manyReferences = new int[600_000];
        var repeatedInput = new Dictionary<NativeCollisionCell, IReadOnlyList<int>>
        { [first] = manyReferences, [neighbor] = manyReferences };
        reject(() => NativeCollisionIndexCodec.Create(repeatedInput, 1, false),
            "global nested enumeration budget also counts aliased input collections");
        require(manyReferences.All(i => i == 0) && repeatedInput.Count == 2 &&
            ReferenceEquals(repeatedInput[first], repeatedInput[neighbor]),
            "bounded enumeration refusal leaves aliased caller inputs untouched");
    }

    private static IEnumerable<KeyValuePair<NativeCollisionCell, IReadOnlyList<int>>> AllCells()
    {
        IReadOnlyList<int> sequence = new[] { 0 };
        for (int z = 0; z < 256; z++)
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                    yield return KeyValuePair.Create(new NativeCollisionCell(x, y, z), sequence);
    }

    private static IEnumerable<int> EndlessZeroes()
    {
        while (true) yield return 0;
    }

    private sealed class ReportedCells(
        IEnumerable<KeyValuePair<NativeCollisionCell, IReadOnlyList<int>>> values,
        int reportedCount, int maximumSteps) : IReadOnlyCollection<KeyValuePair<NativeCollisionCell, IReadOnlyList<int>>>
    {
        public int Count => reportedCount;
        public int Steps { get; private set; }
        public IEnumerator<KeyValuePair<NativeCollisionCell, IReadOnlyList<int>>> GetEnumerator()
        {
            foreach (var value in values)
            {
                if (++Steps > maximumSteps) throw new InvalidOperationException("Outer enumeration was not bounded.");
                yield return value;
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ReportedList(IEnumerable<int> values, int reportedCount, int maximumSteps) : IReadOnlyList<int>
    {
        public int Count => reportedCount;
        public int this[int index] => throw new InvalidOperationException("Caller indexer must not be trusted.");
        public int Steps { get; private set; }
        public IEnumerator<int> GetEnumerator()
        {
            foreach (int value in values)
            {
                if (++Steps > maximumSteps) throw new InvalidOperationException("Nested enumeration was not bounded.");
                yield return value;
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static void AssertCells(NativeCollisionIndexSnapshot generated,
        IReadOnlyDictionary<NativeCollisionCell, IReadOnlyList<int>> expected,
        Action<bool, string> require)
    {
        NativeCollisionIndexSnapshot decoded = NativeCollisionIndexCodec.Decode(
            generated.CopyTree(), generated.CopyBlocks(), generated.UsedBlockBytes,
            generated.TriangleCount, generated.RequiresDescendingOrder);
        require(decoded.Cells.Count == expected.Count && expected.All(p =>
            decoded.Cells.TryGetValue(p.Key, out NativeCollisionCellBinding? binding) &&
            binding.OrderedTriangleIndexes.SequenceEqual(p.Value)),
            "generated native bytes independently decode to every requested cell sequence");
    }

    private static byte[] Words(params ushort[] words)
    {
        byte[] bytes = new byte[words.Length * 2];
        for (int i = 0; i < words.Length; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2), words[i]);
        return bytes;
    }

    private static ushort Word(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
}

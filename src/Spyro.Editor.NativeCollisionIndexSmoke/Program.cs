using System.Buffers.Binary;
using System.Collections;
using System.Reflection;
using Spyro.Editor.Core.Scene;

int checks = 0;
byte[] tree = Words(1, 4, 1, 8, 3, 0, 2, 0xFFFF);
byte[] blocks = Words(0x8003, 2, 0x8001, 0x8000, 0, 0, 0, 0);
NativeCollisionCell a = new(0, 0, 0), b = new(1, 0, 0), vacant = new(2, 0, 0);
NativeCollisionIndexSnapshot source = NativeCollisionIndexCodec.Decode(tree, blocks, 8, 4);
Require(source.Cells.Count == 2 && source.LeafPointerOffsets.Count == 3 &&
    source.Cells[a].OrderedTriangleIndexes.SequenceEqual(new[] { 3, 2 }) &&
    source.Cells[b].OrderedTriangleIndexes.SequenceEqual(new[] { 1 }), "native fixture decoded");
Require(source.GroupStartCount == 3 && source.TerminalSentinelWordOffset == 3,
    "group count and word sentinel offset");
byte[] savedTree = tree.ToArray(), savedBlocks = blocks.ToArray();
tree[0] = 0; blocks[0] = 0;
Require(source.CopyTree().SequenceEqual(savedTree) && source.CopyBlocks().SequenceEqual(savedBlocks),
    "caller arrays cannot mutate snapshot");
byte[] escaped = source.CopyTree(); escaped[0] = 0;
Require(source.CopyTree().SequenceEqual(savedTree), "output copies cannot mutate snapshot");
Require(source.Cells is IDictionary dictionary && dictionary.IsReadOnly,
    "cell map is read only");
Require(source.Cells[a].OrderedTriangleIndexes is IList list && list.IsReadOnly,
    "triangle sequence is read only");

// Exercise the production terrain-set adapter too. This fixture was checked
// against its old decoder before extraction; real-source goldens remain the
// independent byte-level compatibility gate.
Type legacy = typeof(NativeCollisionIndexCodec).Assembly.GetType(
    "Spyro.Editor.Core.Exporting.Id65AuthoringTerrainSetCompiler", true)!;
object legacyResult = legacy.GetMethod("DecodeCollisionIndex", BindingFlags.NonPublic | BindingFlags.Static)!
    .Invoke(null, [savedTree, savedBlocks, 8, 4])!;
var oldCells = (IEnumerable)legacyResult.GetType().GetProperty("Cells")!.GetValue(legacyResult)!;
int oldCount = 0;
foreach (object item in oldCells)
{
    object binding = item.GetType().GetProperty("Value")!.GetValue(item)!;
    int field = (int)binding.GetType().GetProperty("TreePointerByteOffset")!.GetValue(binding)!;
    int[] sequence = (int[])binding.GetType().GetProperty("OrderedTriangleIndexes")!.GetValue(binding)!;
    Require(source.Cells.Values.Single(c => c.TreePointerByteOffset == field)
        .OrderedTriangleIndexes.SequenceEqual(sequence), "real legacy decoder equivalence");
    oldCount++;
}
Require(oldCount == 2, "real legacy decoder cell count");

Dictionary<NativeCollisionCell, IReadOnlyList<int>> unchanged = source.Cells
    .ToDictionary(p => p.Key, p => p.Value.OrderedTriangleIndexes);
NativeCollisionIndexSnapshot roundtrip = NativeCollisionIndexCodec.Rebuild(source, unchanged);
Require(roundtrip.CopyTree().SequenceEqual(savedTree) && roundtrip.CopyBlocks().SequenceEqual(savedBlocks),
    "exact fixed-capacity native roundtrip");
NativeCollisionIndexSnapshot one = NativeCollisionIndexCodec.RetainTriangles(source, new HashSet<int> { 2 });
Require(one.Cells.Count == 1 && one.Cells[a].OrderedTriangleIndexes.SequenceEqual(new[] { 2 }) &&
    one.UsedBlockBytes == 4 && Word(one.CopyTree(), 12) == 0xFFFF,
    "retirement leaves only requested reachable triangle");
Require(one.CopyBlocks().SequenceEqual(Words(0x8002, 0x8000, 0, 0, 0, 0, 0, 0)),
    "retired block bytes are zeroed with exact sentinel");
HashSet<int> leafBytes = source.LeafPointerOffsets.Values.SelectMany(o => new[] { o, o + 1 }).ToHashSet();
Require(savedTree.Where((v, i) => !leafBytes.Contains(i))
    .SequenceEqual(one.CopyTree().Where((v, i) => !leafBytes.Contains(i))), "all tree segment metadata preserved");
NativeCollisionIndexSnapshot empty = NativeCollisionIndexCodec.RetainTriangles(source, new HashSet<int>());
Require(empty.Cells.Count == 0 && empty.UsedBlockBytes == 2 && empty.GroupStartCount == 1,
    "empty index has only terminal sentinel");
NativeCollisionIndexSnapshot repeated = NativeCollisionIndexCodec.RetainTriangles(one, new HashSet<int> { 2 });
Require(repeated.CopyTree().SequenceEqual(one.CopyTree()) && repeated.CopyBlocks().SequenceEqual(one.CopyBlocks()),
    "retirement repeats deterministically");
NativeCollisionIndexSnapshot restored = NativeCollisionIndexCodec.Rebuild(empty, unchanged);
Require(restored.CopyTree().SequenceEqual(savedTree) && restored.CopyBlocks().SequenceEqual(savedBlocks),
    "explicit source sequences restore exact fixture");
var fill = new Dictionary<NativeCollisionCell, IReadOnlyList<int>>(unchanged) { [vacant] = new[] { 3, 2 } };
NativeCollisionIndexSnapshot shared = NativeCollisionIndexCodec.Rebuild(source, fill);
Require(shared.Cells.Count == 3 && shared.UsedBlockBytes == 8 &&
    shared.Cells[a].SourceBlockWordOffset == shared.Cells[vacant].SourceBlockWordOffset,
    "identical immutable block sequences deduplicate");

// Two logical rows share an exact X segment. Equal edits are valid; different
// edits to the same leaf word cannot be silently applied in iteration order.
byte[] aliasedTree = Words(1, 4, 2, 10, 10, 2, 0, 2);
NativeCollisionIndexSnapshot alias = NativeCollisionIndexCodec.Decode(aliasedTree, savedBlocks, 8, 4);
Require(alias.Cells.Count == 4, "native exact same-depth segment alias accepted");
Reject(() => NativeCollisionIndexCodec.Rebuild(alias,
    new Dictionary<NativeCollisionCell, IReadOnlyList<int>> { [a] = new[] { 2 } }),
    "shared leaf with conflicting logical retirement");
NativeCollisionIndexSnapshot aliasFiltered = NativeCollisionIndexCodec.RetainTriangles(alias, new HashSet<int> { 2 });
Require(aliasFiltered.Cells.Count == 2 && aliasFiltered.Cells.Values.All(c =>
    c.OrderedTriangleIndexes.SequenceEqual(new[] { 2 })), "consistent alias filtering");

Reject(() => NativeCollisionIndexCodec.Decode([], savedBlocks, 8, 4), "empty tree");
Reject(() => NativeCollisionIndexCodec.Decode(savedTree[..^1], savedBlocks, 8, 4), "odd tree capacity");
Reject(() => NativeCollisionIndexCodec.Decode(savedTree, savedBlocks[..^1], 8, 4), "odd block capacity");
foreach (int length in new[] { -1, 0, 1, 7, 18, int.MaxValue })
    Reject(() => NativeCollisionIndexCodec.Decode(savedTree, savedBlocks, length, 4), "invalid used length");
foreach (int count in new[] { -1, 0x8001, int.MaxValue })
    Reject(() => NativeCollisionIndexCodec.Decode(savedTree, savedBlocks, 8, count), "triangle count range");
RejectMutation(savedTree, savedBlocks, 0, 0, false, "missing leading group marker");
RejectMutation(savedTree, savedBlocks, 6, 0x8001, false, "bad terminal sentinel");
RejectMutation(savedTree, savedBlocks, 8, 1, false, "nonzero unused capacity");
RejectMutation(savedTree, savedBlocks, 0, 0x8004, false, "triangle index exceeds count");
RejectMutation(savedTree, savedBlocks, 2, 3, false, "duplicate non-descending triangle");
RejectMutation(savedTree, savedBlocks, 10, 1, true, "leaf into group interior");
RejectMutation(savedTree, savedBlocks, 10, 3, true, "leaf into terminal sentinel");
RejectMutation(savedTree, savedBlocks, 12, 0xFFFF, true, "unreferenced group");
RejectMutation(savedTree, savedBlocks, 2, 5, true, "unaligned Y pointer");
RejectMutation(savedTree, savedBlocks, 2, 0xFFFE, true, "out of range Y pointer");
RejectMutation(savedTree, savedBlocks, 2, 0, true, "segment interpreted at two depths");
RejectMutation(savedTree, savedBlocks, 6, 6, true, "partially overlapping segment");
RejectMutation(savedTree, savedBlocks, 0, 257, true, "excessive dimension length");
Reject(() => NativeCollisionIndexCodec.RetainTriangles(source, new HashSet<int> { 0 }), "unreachable retained triangle");
Reject(() => NativeCollisionIndexCodec.RetainTriangles(source, new HashSet<int> { -1 }), "negative retained triangle");
Reject(() => NativeCollisionIndexCodec.Rebuild(source,
    new Dictionary<NativeCollisionCell, IReadOnlyList<int>> { [new(3, 0, 0)] = new[] { 1 } }), "missing leaf allocation");
foreach (int[] sequence in new[] { Array.Empty<int>(), new[] { -1 }, new[] { 4 }, new[] { 1, 2 } })
    Reject(() => NativeCollisionIndexCodec.Rebuild(source,
        new Dictionary<NativeCollisionCell, IReadOnlyList<int>> { [a] = sequence }), "invalid rebuilt sequence");
byte[] shortBlocks = Words(0x8003, 2, 0x8001, 0x8000);
NativeCollisionIndexSnapshot full = NativeCollisionIndexCodec.Decode(savedTree, shortBlocks, 8, 4);
Reject(() => NativeCollisionIndexCodec.Rebuild(full,
    new Dictionary<NativeCollisionCell, IReadOnlyList<int>>
    { [a] = new[] { 3, 2, 1, 0 }, [b] = new[] { 2, 1, 0 } }), "capacity exhaustion");
Require(source.CopyTree().SequenceEqual(savedTree) && source.CopyBlocks().SequenceEqual(savedBlocks),
    "all failures leave source unchanged");
CreationCases.Run(Require, Reject);
Console.WriteLine($"NATIVE_COLLISION_INDEX_SMOKE_OK checks={checks}; native roundtrip, alias conflicts, deterministic retirement; no game image");

void Require(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException(label);
    checks++;
}
void Reject(Action action, string label)
{
    try { action(); } catch (InvalidDataException) { checks++; return; }
    throw new InvalidOperationException("Expected rejection: " + label);
}
void RejectMutation(byte[] t, byte[] bts, int offset, ushort value, bool mutateTree, string label)
{
    t = t.ToArray(); bts = bts.ToArray();
    BinaryPrimitives.WriteUInt16LittleEndian((mutateTree ? t : bts).AsSpan(offset), value);
    Reject(() => NativeCollisionIndexCodec.Decode(t, bts, 8, 4), label);
}
static byte[] Words(params ushort[] words)
{
    byte[] bytes = new byte[words.Length * 2];
    for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2), words[i]);
    return bytes;
}
static ushort Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));

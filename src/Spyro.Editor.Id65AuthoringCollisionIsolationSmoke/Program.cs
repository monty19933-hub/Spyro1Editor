using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Scene;

string repo = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? ".");
string locked = Path.Combine(repo, "_local", "id65-authoring-aggregate-workspace", "_research",
    "unusedlevel65blank", "v1", "locked-base", "unusedlevel65blank-locked-base-v1.bin");
Require(File.Exists(locked), "Exact existing locked fixture is required; this test does not create it.");
string originalHash = FileHash(locked);
var source = Id65AuthoringEditorAggregateBoundSource.Capture(locked).LockedSource;
var v3 = UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.BuildStaticPlan(locked);
var textures = Id65V2NativeTextureCompositionCompiler.DerivePinnedT66WitnessFromLockedBase(locked);
var grass = Id65AuthoringMobyDependencyBundleCompiler.CreateArtisansGrassBundleFromLockedBase(locked);
string[][] terrainStates = [[], [Id65AuthoringTerrainSetCatalog.Tile1Id],
    [Id65AuthoringTerrainSetCatalog.Tile2Id], [Id65AuthoringTerrainSetCatalog.Tile1Id, Id65AuthoringTerrainSetCatalog.Tile2Id]];
bool allStates = args.Contains("--all-states", StringComparer.Ordinal);
var positions = allStates ? Id65RemotePadGrassT107Catalog.Positions.ToArray() :
    [Id65RemotePadGrassT107Catalog.GetRequiredPosition(Id65RemotePadGrassT107Catalog.CenterPositionId)];
var kinds = allStates ? Enum.GetValues<Id65RemotePadMobyKind>() : [Id65RemotePadMobyKind.GrassT107];
int states = 0, checks = 0, queries = 0;
HashSet<string> outputHashes = [];
HashSet<string> planHashes = [];
foreach (string[] terrain in terrainStates)
foreach (Id65RemotePadMobyKind kind in kinds)
foreach (var position in positions)
{
    var document = Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
        kind, position.PositionId, terrain);
    var request = new Id65AuthoringSingleSectorSubstrateCompileRequest(document, source, v3, textures, grass);
    Id65CompiledAuthoringCollisionIsolation result = Id65AuthoringCollisionIsolationCompiler.Compile(request);
    Check(result, terrain.Length);
    outputHashes.Add(result.OutputRow80Sha256);
    Require(planHashes.Add(result.DeterministicPlanSha256), "Different authored intents must retain distinct plan identities.");
    if (!allStates || terrain.Length == 1 && terrain[0] == Id65AuthoringTerrainSetCatalog.Tile1Id &&
        kind == Id65RemotePadMobyKind.GrassT107 && position.PositionId == Id65RemotePadGrassT107Catalog.CenterPositionId)
    {
        var repeat = Id65AuthoringCollisionIsolationCompiler.Compile(request);
        Require(repeat.DeterministicPlanSha256 == result.DeterministicPlanSha256 &&
            repeat.CopyOutputRow80().SequenceEqual(result.CopyOutputRow80()), "exact deterministic compiler repeat");
    }
    states++;
}

// None has no physical placement: its nine position labels intentionally share
// bytes, while retaining distinct authored-document/plan identities.
Require(outputHashes.Count == (allStates ? 76 : 4), "Physical state equivalence remains unchanged.");

var collapsedDocument = Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
    Id65RemotePadMobyKind.GrassT107, Id65RemotePadGrassT107Catalog.CenterPositionId,
    [Id65AuthoringTerrainSetCatalog.Tile1Id]);
var collapsedResult = Id65AuthoringCollisionIsolationCompiler.Compile(
    new(collapsedDocument, source, v3, textures, grass), collapsedCyclorama: true);
Check(collapsedResult, 1);
Require(collapsedResult.CollapsedCyclorama && collapsedResult.BaselineRow80Sha256 ==
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.ExpectedOutputRow80Sha256,
    "collapsed probe uses its existing exact baseline golden");
Require(FileHash(locked) == originalHash, "Locked source image remains byte-identical.");
Console.WriteLine($"ID65_COLLISION_ISOLATION_SMOKE_OK states={states} collapsed=1 checkedStates={checks} " +
    $"independentCellQueries={queries} retiredCells={collapsedResult.Readback.RetiredOccupiedCells} " +
    $"retainedTriangles={string.Join(',', collapsedResult.RetainedTriangleIndexes)} " +
    $"collapsedOutput={collapsedResult.OutputRow80Sha256} nativeTriangleTableRetained=1 runtime=false noImageWriter=true");

void Check(Id65CompiledAuthoringCollisionIsolation result, int extraTriangles)
{
    byte[] before = result.CopyBaselineRow80(), after = result.CopyOutputRow80();
    var readback = result.Readback;
    Require(readback.SourceOccupiedCells == 4253 && readback.OutputOccupiedCells == 1 &&
        readback.RetiredOccupiedCells == 4252 && readback.OutputReachableTriangles == extraTriangles + 1 &&
        readback.OutputBlockUsedBytes == (extraTriangles + 2) * 2,
        "exact authored support island reachability census");
    Require(!result.RuntimeProven && !result.WritesDiscImage && !result.NormalCreateBinEnabled,
        "offline collision stage does not grant game/image authority");
    Require(result.ApplyExact(before).SequenceEqual(after) && result.ApplyExact(after, true).SequenceEqual(before),
        "exact forward and reverse transactions");
    byte[] tampered = before.ToArray(); tampered[0] ^= 1;
    Reject(() => result.ApplyExact(tampered), "changed protected input");
    tampered = before.ToArray(); tampered[result.OwnedRanges[0].DataRelativeOffset] ^= 1;
    Reject(() => result.ApplyExact(tampered), "changed owned input");
    tampered = after.ToArray(); tampered[0] ^= 1;
    Reject(() => result.ApplyExact(tampered, true), "changed inverse input");
    int baseOffset = readback.CollisionRow80Offset;
    int tree = baseOffset + 4 + Int(after, baseOffset + 12);
    int blocks = baseOffset + 4 + Int(after, baseOffset + 16);
    int triangles = baseOffset + 4 + Int(after, baseOffset + 20);
    int assignments = baseOffset + 4 + Int(after, baseOffset + 24);
    var sourceIndex = NativeCollisionIndexCodec.Decode(before.AsSpan(tree, blocks - tree),
        before.AsSpan(blocks, triangles - blocks), readback.SourceBlockUsedBytes, readback.TriangleCount);
    HashSet<int> writableTreeBytes = sourceIndex.LeafPointerOffsets.Values
        .SelectMany(o => new[] { tree + o, tree + o + 1 }).ToHashSet();
    for (int i = 0; i < before.Length; i++)
        if (before[i] != after[i])
            Require(writableTreeBytes.Contains(i) || i >= blocks && i < triangles,
                "only declared leaf words and block bytes can change");
    Require(before.AsSpan(triangles, readback.TriangleCount * 12)
        .SequenceEqual(after.AsSpan(triangles, readback.TriangleCount * 12)),
        "all native and authored triangle rows preserved");
    foreach (var cell in sourceIndex.LeafPointerOffsets.Keys)
    {
        int[] nativeHits = Lookup(after, tree, blocks, cell.X, cell.Y, cell.Z);
        int[] expected = cell == new NativeCollisionCell(1, 1, 2)
            ? result.RetainedTriangleIndexes.OrderDescending().ToArray() : [];
        Require(nativeHits.SequenceEqual(expected), "independent native tree lookup at every addressable cell");
        queries++;
    }
    foreach (int triangle in result.RetainedTriangleIndexes)
    {
        Require(after[assignments + triangle] == 0, "authored collision assignment retained");
        var p = Points(after.AsSpan(triangles + triangle * 12, 12));
        Require(p.All(v => v.X >> 8 == 1 && v.Y >> 8 == 1 && v.Z >> 8 == 2),
            "retained triangle bounds lie in their unique support cell");
        long normal = (long)(p[1].X - p[0].X) * (p[2].Y - p[0].Y) -
            (long)(p[1].Y - p[0].Y) * (p[2].X - p[0].X);
        Require(normal < 0, "known-good native floor winding retained");
    }
    Require(Hash(after) == result.OutputRow80Sha256, "complete output hash");
    byte[] escaped = result.CopyOutputRow80(); escaped[0] ^= 1;
    Require(Hash(result.CopyOutputRow80()) == result.OutputRow80Sha256, "immutable result bytes");
    Console.WriteLine($"Collision state {states + 1}: {extraTriangles + 1} reachable authored triangles; " +
        $"{readback.SourceOccupiedCells}->{readback.OutputOccupiedCells} cells; " +
        $"{readback.SourceBlockUsedBytes}->{readback.OutputBlockUsedBytes} used block bytes.");
    checks++;
}

// Independent literal interpretation of the established native tree grammar.
// It deliberately does not call the production codec or reuse its cell map.
static int[] Lookup(byte[] data, int tree, int blocks, int x, int y, int z)
{
    if (z < 0 || z >= U16(data, tree)) return [];
    int ySegment = U16(data, tree + 2 + z * 2);
    if (ySegment == 0xFFFF || y < 0 || y >= U16(data, tree + ySegment)) return [];
    int xSegment = U16(data, tree + ySegment + 2 + y * 2);
    if (xSegment == 0xFFFF || x < 0 || x >= U16(data, tree + xSegment)) return [];
    int group = U16(data, tree + xSegment + 2 + x * 2);
    if (group == 0xFFFF) return [];
    List<int> result = [U16(data, blocks + group * 2) & 0x7FFF];
    for (int i = group + 1; (U16(data, blocks + i * 2) & 0x8000) == 0; i++)
        result.Add(U16(data, blocks + i * 2));
    return result.ToArray();
}
static (int X, int Y, int Z)[] Points(ReadOnlySpan<byte> bytes)
{
    uint x = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    uint y = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
    uint z = BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]);
    int bx = (int)(x & 0x3FFF), by = (int)(y & 0x3FFF), bz = (int)(z & 0x3FFF);
    return [(bx, by, bz), (bx + Signed(x >> 14), by + Signed(y >> 14), bz + (int)((z >> 16) & 255)),
        (bx + Signed(x >> 23), by + Signed(y >> 23), bz + (int)(z >> 24))];
}
static int Signed(uint value) { int bits = (int)(value & 511); return bits >= 256 ? bits - 512 : bits; }
static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
static int Int(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
static string FileHash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(stream)); }
static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
static void Reject(Action action, string message)
{
    try { action(); } catch (InvalidDataException) { return; }
    throw new InvalidOperationException("Expected rejection: " + message);
}

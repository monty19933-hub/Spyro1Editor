using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

internal sealed record Id65CollisionIsolationReadback(
    int CollisionRow80Offset, int CollisionByteLength, int TriangleCount,
    int SourceOccupiedCells, int OutputOccupiedCells, int RetiredOccupiedCells,
    int SourceReachableTriangles, int OutputReachableTriangles,
    int SourceBlockUsedBytes, int OutputBlockUsedBytes,
    string SourceCollisionSha256, string OutputCollisionSha256);

/// <summary>
/// Reversible, in-memory lookup retirement. Triangle storage, assignments,
/// flags, source object dependencies and component capacities remain intact.
/// No assertion is made about non-lookup collision consumers or gameplay.
/// </summary>
internal sealed class Id65CompiledAuthoringCollisionIsolation
{
    private readonly byte[] _before, _after;

    internal Id65CompiledAuthoringCollisionIsolation(Id65AuthoringEditorDocument document,
        byte[] before, byte[] after, IEnumerable<Id65AuthoringCompositeOwnedRange> ranges,
        IEnumerable<int> retained, Id65CollisionIsolationReadback readback,
        string baselinePlan, bool collapsedCyclorama)
    {
        Document = Id65AuthoringEditorDocument.Parse(document.SerializeUtf8());
        _before = before.ToArray(); _after = after.ToArray();
        OwnedRanges = Array.AsReadOnly(ranges.Select(r => r.DeepCopy()).ToArray());
        RetainedTriangleIndexes = Array.AsReadOnly(retained.Order().ToArray());
        Readback = readback;
        BaselineRow80Sha256 = Id65AuthoringCollisionIsolationCompiler.Hash(_before);
        OutputRow80Sha256 = Id65AuthoringCollisionIsolationCompiler.Hash(_after);
        BaselineDeterministicPlanSha256 = baselinePlan;
        CollapsedCyclorama = collapsedCyclorama;
        ProtectedComplementSha256 = Id65AuthoringCollisionIsolationCompiler.ProtectedHash(_before, OwnedRanges);
        if (ProtectedComplementSha256 != Id65AuthoringCollisionIsolationCompiler.ProtectedHash(_after, OwnedRanges))
            throw new InvalidDataException("Collision retirement changed the protected complement.");
        DeterministicPlanSha256 = Id65AuthoringCollisionIsolationCompiler.Hash(Encoding.UTF8.GetBytes(
            string.Join('\n', Id65AuthoringCollisionIsolationCompiler.ProfileId, Document.CanonicalSha256,
                baselinePlan, BaselineRow80Sha256, OutputRow80Sha256,
                string.Join(',', RetainedTriangleIndexes), ProtectedComplementSha256,
                collapsedCyclorama ? "collapsed" : "single-sector")));
    }

    public Id65AuthoringEditorDocument Document { get; }
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> OwnedRanges { get; }
    public IReadOnlyList<int> RetainedTriangleIndexes { get; }
    public Id65CollisionIsolationReadback Readback { get; }
    public string BaselineRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string BaselineDeterministicPlanSha256 { get; }
    public string DeterministicPlanSha256 { get; }
    public string ProtectedComplementSha256 { get; }
    public bool CollapsedCyclorama { get; }
    public bool RuntimeProven => false;
    public bool WritesDiscImage => false;
    public bool NormalCreateBinEnabled => false;
    public byte[] CopyBaselineRow80() => _before.ToArray();
    public byte[] CopyOutputRow80() => _after.ToArray();

    public byte[] ApplyExact(ReadOnlySpan<byte> input, bool reverse = false)
    {
        byte[] expected = reverse ? _after : _before;
        if (!input.SequenceEqual(expected))
            throw new InvalidDataException("Collision transaction requires its complete exact input, not only matching patch bytes.");
        byte[] output = input.ToArray();
        foreach (Id65AuthoringCompositeOwnedRange range in OwnedRanges)
        {
            byte[] before = reverse ? range.CopyAfter() : range.CopyBefore();
            byte[] after = reverse ? range.CopyBefore() : range.CopyAfter();
            if (!output.AsSpan(range.DataRelativeOffset, before.Length).SequenceEqual(before))
                throw new InvalidDataException("Collision transaction preimage changed.");
            after.CopyTo(output, range.DataRelativeOffset);
        }
        if (!output.AsSpan().SequenceEqual(reverse ? _before : _after))
            throw new InvalidDataException("Collision transaction failed exact complete readback.");
        return output;
    }
}

internal static class Id65AuthoringCollisionIsolationCompiler
{
    public const string ProfileId = "id65-authored-support-collision-lookup-isolation-v1";
    private const int BaseSupportTriangle = 13_995;
    private static readonly NativeCollisionCell SupportCell = new(1, 1, 2);

    public static Id65CompiledAuthoringCollisionIsolation Compile(
        Id65AuthoringSingleSectorSubstrateCompileRequest request, bool collapsedCyclorama = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Rebuild from source-bound providers. No arbitrary row80 afterimage or
        // user-provided list of "safe" triangles is accepted by this stage.
        Id65CompiledAuthoringSingleSectorSubstrate baseline =
            Id65AuthoringSingleSectorSubstrateCompiler.Compile(request);
        Id65CompiledAuthoringTerrainSet terrain = Id65AuthoringTerrainSetCompiler.CompileTarget(
            Id65AuthoringTerrainSetCatalog.Create(request.Document.ActiveTerrainUnitIds),
            request.V3Template, request.TerrainLimits);
        byte[] before = baseline.CopyOutputRow80();
        string baselinePlan = baseline.DeterministicPlanSha256;
        if (collapsedCyclorama)
        {
            Id65CompiledAuthoringCollapsedCycloramaGeometryProbe collapsed =
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.CompileFromSingleSector(baseline);
            before = collapsed.CopyOutputRow80();
            baselinePlan = collapsed.DeterministicPlanSha256;
        }
        Id65ModelComponentRelocation collision = baseline.ComponentRelocations
            .Single(c => c.StableId == "component.collision");
        int offset = checked(baseline.ModelRow80Offset + collision.OutputRelativeOffset);
        byte[] component = before.AsSpan(offset, collision.OutputByteLength).ToArray();
        if (Hash(component) != baseline.CollisionSha256 || Hash(component) != terrain.CollisionSha256 ||
            Read(component, 0) != component.Length || Read(component, 4) != terrain.Capacity.CollisionTriangleCount)
            throw new InvalidDataException("Collision isolation lost the exact compiled terrain/component binding.");
        int triangles = Read(component, 4);
        int treeOffset = checked(4 + Read(component, 12));
        int blocksOffset = checked(4 + Read(component, 16));
        int triangleOffset = checked(4 + Read(component, 20));
        int assignmentsOffset = checked(4 + Read(component, 24));
        int flagsOffset = checked(4 + Read(component, 28));
        if (treeOffset != 0x20 || blocksOffset <= treeOffset || triangleOffset <= blocksOffset ||
            (long)triangleOffset + triangles * 12L != assignmentsOffset ||
            (long)assignmentsOffset + triangles > flagsOffset ||
            (long)flagsOffset + Read(component, 8) != component.Length)
            throw new InvalidDataException("Collision component partitions are not the exact native layout.");
        NativeCollisionIndexSnapshot native = NativeCollisionIndexCodec.Decode(
            component.AsSpan(treeOffset, blocksOffset - treeOffset),
            component.AsSpan(blocksOffset, triangleOffset - blocksOffset),
            terrain.Capacity.CollisionBlocksUsedBytes, triangles);
        HashSet<int> retained = terrain.Units.Select(u => u.CollisionTriangleIndex).Append(BaseSupportTriangle).ToHashSet();
        if (retained.Count != request.Document.ActiveTerrainUnitIds.Count + 1 ||
            !native.Cells.TryGetValue(SupportCell, out NativeCollisionCellBinding? support) ||
            !support.OrderedTriangleIndexes.SequenceEqual(terrain.TargetCellSequence) ||
            !support.OrderedTriangleIndexes.ToHashSet().SetEquals(retained))
            throw new InvalidDataException("The authored support island does not own exactly its declared native triangles.");
        foreach (int triangle in retained)
        {
            if (native.Cells.Values.Count(c => c.OrderedTriangleIndexes.Contains(triangle)) != 1 ||
                component[assignmentsOffset + triangle] != 0)
                throw new InvalidDataException("An authored support triangle has foreign cell ownership or assignment.");
        }
        NativeCollisionIndexSnapshot isolated = NativeCollisionIndexCodec.RetainTriangles(native, retained);
        if (isolated.Cells.Count != 1 || !isolated.Cells.ContainsKey(SupportCell) ||
            !isolated.Cells[SupportCell].OrderedTriangleIndexes.SequenceEqual(support.OrderedTriangleIndexes))
            throw new InvalidDataException("Collision retirement failed complete reachable-cell readback.");

        byte[] after = before.ToArray();
        isolated.CopyTree().CopyTo(after, offset + treeOffset);
        isolated.CopyBlocks().CopyTo(after, offset + blocksOffset);
        List<Id65AuthoringCompositeOwnedRange> owned = [];
        foreach (int field in native.LeafPointerOffsets.Values.Distinct().Order())
        {
            int absolute = offset + treeOffset + field;
            if (!before.AsSpan(absolute, 2).SequenceEqual(after.AsSpan(absolute, 2)))
                owned.Add(new($"collision.leaf.{field:X}", ProfileId, absolute,
                    before.AsSpan(absolute, 2).ToArray(), after.AsSpan(absolute, 2).ToArray()));
        }
        owned.Add(new("collision.blocks", ProfileId, offset + blocksOffset,
            before.AsSpan(offset + blocksOffset, triangleOffset - blocksOffset).ToArray(),
            after.AsSpan(offset + blocksOffset, triangleOffset - blocksOffset).ToArray()));
        var readback = new Id65CollisionIsolationReadback(offset, component.Length, triangles,
            native.Cells.Count, isolated.Cells.Count, native.Cells.Count - isolated.Cells.Count,
            native.Cells.Values.SelectMany(c => c.OrderedTriangleIndexes).Distinct().Count(), retained.Count,
            native.UsedBlockBytes, isolated.UsedBlockBytes, Hash(component),
            Hash(after.AsSpan(offset, component.Length)));
        Id65CompiledAuthoringCollisionIsolation result = new(request.Document, before, after, owned,
            retained, readback, baselinePlan, collapsedCyclorama);
        if (!result.ApplyExact(before).AsSpan().SequenceEqual(after) ||
            !result.ApplyExact(after, reverse: true).AsSpan().SequenceEqual(before))
            throw new InvalidDataException("Collision isolation did not preserve its reversible transaction.");
        return result;
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    internal static string ProtectedHash(byte[] bytes, IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int cursor = 0;
        foreach (var range in ranges.OrderBy(r => r.DataRelativeOffset))
        {
            int length = range.CopyBefore().Length;
            if (range.DataRelativeOffset < cursor || range.DataRelativeOffset > bytes.Length - length ||
                length <= 0 || range.CopyAfter().Length != length)
                throw new InvalidDataException("Collision transaction has overlapping, resizing or unbounded ownership.");
            hash.AppendData(bytes.AsSpan(cursor, range.DataRelativeOffset - cursor));
            cursor = range.DataRelativeOffset + length;
        }
        hash.AppendData(bytes.AsSpan(cursor));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static int Read(byte[] bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - 4)
            throw new InvalidDataException("Collision header is truncated.");
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
    }
}

using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Analysis;

/// <summary>An explicit association with an already persistent authored GUID; never an ID allocator.</summary>
internal sealed record NativeJapaneseEggCollectibleIdentity(string RowId, Guid InstanceId);

/// <summary>
/// Immutable join of the existing final authored-row owners and existing manifest.
/// This is neither another serialized identity schema nor a native scene-load owner.
/// Moving/reordering rows requires a fresh join with the same persistent GUIDs.
/// </summary>
internal sealed class NativeJapaneseEggCollectibleBinding
{
    internal sealed record Row(string RowId, Guid InstanceId, uint BitIndex, ushort AuthoredClass,
        uint RowAddress, uint PrivateAddress, NativeByteExtent RowOwner, NativeByteExtent PrivateOwner);
    private readonly byte[] data, overlay, manifestBytes;

    private NativeJapaneseEggCollectibleBinding(NativeJapaneseEggAuthoredClassesPlan authored,
        DetachedCollectibleManifest manifest, NativeJapaneseEggCollectibleIdentity[] mappings)
    {
        if (mappings.Any(m => m is null || string.IsNullOrWhiteSpace(m.RowId)) || mappings.Length != authored.Bindings.Count ||
            mappings.Select(m => m.RowId).Distinct(StringComparer.Ordinal).Count() != mappings.Length ||
            !mappings.Select(m => m.RowId).ToHashSet(StringComparer.Ordinal).SetEquals(authored.Bindings.Select(b => b.RowId)))
            throw new InvalidDataException("Every exact authored row requires one explicit mapping and no extra row mapping.");
        if (mappings.Any(m => m.InstanceId == Guid.Empty) || mappings.Select(m => m.InstanceId).Distinct().Count() != mappings.Length)
            throw new InvalidDataException("Each authored row requires a distinct nonempty persistent instance identity.");
        if (authored.SourceCertified != authored.Reservation.SourceCertified || authored.SourceCertified != authored.Projection.SourceCertified ||
            authored.SourceCertified != authored.Integration.SourceCertified)
            throw new InvalidDataException("Authored row source ancestry is inconsistent.");

        data = authored.CopyLevelData(); overlay = authored.CopyOverlay(); manifestBytes = manifest.SerializeCanonical();
        byte[] scene = authored.CopyScene();
        int sceneOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x18)));
        SceneAddress = authored.Projection.ProjectedSceneRamStart; SceneByteLength = scene.Length;
        ModelAddress = authored.Projection.ProjectedModelRamStart;
        ModelByteLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x14)));
        SourceCertified = authored.SourceCertified; ManifestSha256 = manifest.Sha256;
        AuthoredPlanSha256 = authored.PlanSha256; FinalDataSha256 = authored.FinalDataSha256; FinalOverlaySha256 = authored.FinalOverlaySha256;
        List<Row> rows = [];
        foreach (var b in authored.Bindings)
        {
            var identity = mappings.Single(m => m.RowId == b.RowId);
            var entry = manifest.GetEntry(identity.InstanceId);
            if (entry.Retired) throw new InvalidDataException("An authored live row cannot be bound to a retired collectible identity.");
            if (b.RowSceneOffset < 0 || b.RowSceneOffset > scene.Length - 0x58)
                throw new InvalidDataException("Authored row leaves its final scene owner.");
            uint privateOffset = BinaryPrimitives.ReadUInt32LittleEndian(scene.AsSpan(b.RowSceneOffset));
            if ((ulong)privateOffset + 12 > (ulong)scene.Length ||
                (ulong)SceneAddress + (uint)b.RowSceneOffset != b.ProjectedRowAddress ||
                (ulong)SceneAddress + privateOffset != b.ProjectedPrivateAddress ||
                BinaryPrimitives.ReadUInt16LittleEndian(scene.AsSpan(b.RowSceneOffset + 0x36)) != b.AuthoredDestinationClass ||
                scene[b.RowSceneOffset + 0x48] != 0)
                throw new InvalidDataException("Exact final authored row/class/private/live preimage changed.");
            rows.Add(new(b.RowId, identity.InstanceId, checked((uint)entry.BitIndex), b.AuthoredDestinationClass,
                b.ProjectedRowAddress, b.ProjectedPrivateAddress,
                new(authored.FinalDomain, sceneOffset + b.RowSceneOffset, 0x58),
                new(authored.FinalDomain, sceneOffset + privateOffset, 12)));
        }
        if (rows.Count != 2 || rows.Select(r => r.RowAddress).Distinct().Count() != rows.Count ||
            rows.Select(r => r.PrivateAddress).Distinct().Count() != rows.Count || rows.Select(r => r.BitIndex).Distinct().Count() != rows.Count)
            throw new InvalidDataException("Exact two-row collectible mapping lost independent native or manifest owners.");
        var extents = rows.SelectMany(r => new[] { r.RowOwner, r.PrivateOwner }).OrderBy(e => e.Offset).ToArray();
        for (int i = 1; i < extents.Length; i++)
            if (extents[i - 1].EndExclusive > extents[i].Offset)
                throw new InvalidDataException("Authored row and private owners overlap.");
        Rows = rows.AsReadOnly();
    }

    internal IReadOnlyList<Row> Rows { get; }
    internal string AuthoredPlanSha256 { get; }
    internal string FinalDataSha256 { get; }
    internal string FinalOverlaySha256 { get; }
    internal string ManifestSha256 { get; }
    internal bool SourceCertified { get; }
    internal uint SceneAddress { get; }
    internal int SceneByteLength { get; }
    internal uint ModelAddress { get; }
    internal int ModelByteLength { get; }

    internal static NativeJapaneseEggCollectibleBinding Create(NativeJapaneseEggAuthoredClassesPlan authored,
        DetachedCollectibleManifest manifest, IEnumerable<NativeJapaneseEggCollectibleIdentity> mappings)
    {
        ArgumentNullException.ThrowIfNull(authored); ArgumentNullException.ThrowIfNull(manifest); ArgumentNullException.ThrowIfNull(mappings);
        return new(authored, manifest, mappings.Take(authored.Bindings.Count + 1).ToArray());
    }

    internal void RequireCurrent(NativeJapaneseEggAuthoredClassesPlan authored, DetachedCollectibleManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(authored); ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.Sha256 != ManifestSha256 || !manifest.SerializeCanonical().AsSpan().SequenceEqual(manifestBytes))
            throw new InvalidDataException("Row binding requires its exact manifest; successor rebinding is explicit.");
        if (authored.SourceCertified != SourceCertified || authored.PlanSha256 != AuthoredPlanSha256 ||
            authored.FinalDataSha256 != FinalDataSha256 || authored.FinalOverlaySha256 != FinalOverlaySha256 ||
            !authored.CopyLevelData().AsSpan().SequenceEqual(data) || !authored.CopyOverlay().AsSpan().SequenceEqual(overlay))
            throw new InvalidDataException("Row binding is stale for this exact authored scene/model/overlay composition.");
    }
}

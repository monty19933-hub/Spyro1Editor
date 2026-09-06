using System.Buffers.Binary;
using System.Text;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativePlayerAnimationPreservation(string Id, int OriginalOffset, int FinalOffset,
    int ByteLength, string Sha256);
public sealed record NativePlayerAnimationHeaderChange(int FieldOffset, uint Before, uint After);

/// <summary>
/// Exact-source level-entry bytes only. This does not patch a WAD, allocate runtime
/// RAM/SPU/VRAM, authorize publication, or establish complete animation consumers.
/// </summary>
public sealed class NativePlayerAnimationMergePlan
{
    private readonly NativePlayerAnimationMergeLayout _layout;
    internal NativePlayerAnimationMergePlan(NativePlayerAnimationSource source, int donor, int target,
        NativePlayerAnimationMergeLayout layout)
    {
        _layout = layout; SourceImageSha256 = source.SourceImageSha256;
        SharedPlayerPackageSha256 = source.SharedPlayerPackageSha256;
        DonorLevelId = donor; TargetLevelId = target;
        string identity = $"{SourceImageSha256}|{SharedPlayerPackageSha256}|{donor}|{target}|{layout.OriginalSha256}|" +
            $"{layout.FinalSha256}|{string.Join(',', layout.RequestedIds)}";
        PlanSha256 = NativePlayerAnimationStreamCodec.Hash(Encoding.UTF8.GetBytes(identity));
    }
    public string SourceImageSha256 { get; }
    public string SharedPlayerPackageSha256 { get; }
    public int DonorLevelId { get; }
    public int TargetLevelId { get; }
    public string PlanSha256 { get; }
    public string OriginalDataSha256 => _layout.OriginalSha256;
    public string FinalDataSha256 => _layout.FinalSha256;
    public int OriginalDataByteLength => _layout.OriginalByteLength;
    public int FinalDataByteLength => _layout.FinalByteLength;
    public int ExactInsertedByteLength => _layout.ExactGrowth;
    public int SectorRoundedGrowth => _layout.FinalByteLength - _layout.OriginalByteLength;
    public IReadOnlyList<int> RequestedAnimationIds => _layout.RequestedIds;
    public IReadOnlyList<int> AddedAnimationIds => _layout.AddedIds;
    public IReadOnlyList<int> ReusedLocalAnimationIds => _layout.ReusedLocalIds;
    public IReadOnlyList<int> ReusedSharedAnimationIds => _layout.ReusedSharedIds;
    public IReadOnlyList<NativePlayerAnimationPreservation> Preservations => _layout.Preservations;
    public IReadOnlyList<NativePlayerAnimationHeaderChange> HeaderChanges => _layout.HeaderChanges;
    public bool RuntimeFitProven => false;
    public bool CompleteAnimationConsumersProven => false;
    public bool PublicationApproved => false;
    public byte[] CopyFinalLevelData() => _layout.CopyFinalBytes();
    internal NativeSkyRelocationPayload? CopyInsertionPayload() => _layout.CopyInsertionPayload();
}

/// <summary>Neutral byte composer. Only NativePlayerAnimationSource can issue the public source-bound plan.</summary>
internal sealed class NativePlayerAnimationMergeLayout
{
    private readonly byte[] _final;
    private readonly NativeSkyRelocationPayload? _payload;
    internal string OriginalSha256 { get; }
    internal string FinalSha256 { get; }
    internal int OriginalByteLength { get; }
    internal int FinalByteLength => _final.Length;
    internal int ExactGrowth => _payload?.Bytes.Length ?? 0;
    internal IReadOnlyList<int> RequestedIds { get; }
    internal IReadOnlyList<int> AddedIds { get; }
    internal IReadOnlyList<int> ReusedLocalIds { get; }
    internal IReadOnlyList<int> ReusedSharedIds { get; }
    internal IReadOnlyList<NativePlayerAnimationPreservation> Preservations { get; }
    internal IReadOnlyList<NativePlayerAnimationHeaderChange> HeaderChanges { get; }

    internal NativePlayerAnimationMergeLayout(ReadOnlySpan<byte> targetData, NativePlayerAnimationStream donor,
        ReadOnlySpan<byte> sharedGeometry, IReadOnlyList<int> sharedAnimationIds, IReadOnlyList<int> animationIds,
        int dataWadEntry = 0, long dataWadOffset = 0)
    {
        ArgumentNullException.ThrowIfNull(donor);
        ArgumentNullException.ThrowIfNull(sharedAnimationIds);
        ArgumentNullException.ThrowIfNull(animationIds);
        if (targetData.Length > 0x800000 || targetData.Length < 0x2800 || dataWadEntry < 0 || dataWadOffset < 0 ||
            dataWadOffset > long.MaxValue - targetData.Length ||
            animationIds.Count == 0 || animationIds.Count > 46 || animationIds.Any(i => i < 0 || i >= 46) ||
            animationIds.Distinct().Count() != animationIds.Count || sharedAnimationIds.Count > 46 ||
            sharedAnimationIds.Any(i => i < 0 || i >= 46) || sharedAnimationIds.Distinct().Count() != sharedAnimationIds.Count)
            throw new InvalidDataException("Animation merge needs bounded native owners and distinct requested/shared IDs 0..45.");
        byte[] original = targetData.ToArray();
        int modelOffset = Integer(original, 0x10), modelLength = Integer(original, 0x14);
        // The shared outer schema validates every descriptor/root before this producer
        // reads the special root-0 extent. Zero-length no-op is an explicit payload.
        var validation = new NativeLevelDataGrowthLayout(original.AsSpan(0, 0x800), original.Length,
            [new(0, 37, checked(dataWadOffset + modelOffset + modelLength), dataWadEntry, modelLength, 0, [], 2, false)]);
        int streamOffset = Integer(original, 0x50), nextRoot = Integer(original, 0x54);
        if (BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(0x150)) != 0 ||
            nextRoot <= streamOffset || streamOffset < modelOffset || nextRoot > modelOffset + (long)modelLength)
            throw new InvalidDataException("Animation merge requires a bounded special class-0 stream ending at the next native model root.");
        var target = NativePlayerAnimationStreamCodec.Decode(original.AsSpan(streamOffset, nextRoot - streamOffset), sharedGeometry);
        if (target.SharedGeometrySha256 != donor.SharedGeometrySha256 ||
            target.SharedGeometryByteLength != donor.SharedGeometryByteLength)
            throw new InvalidDataException("Donor and destination player animations use different shared-geometry address domains.");
        int[] requested = animationIds.Order().ToArray();
        HashSet<int> shared = sharedAnimationIds.ToHashSet();
        if (target.Records.Any(r => shared.Contains(r.AnimationId)) || donor.Records.Any(r => shared.Contains(r.AnimationId)))
            throw new InvalidDataException("A local animation overrides an owned shared-player slot; equivalence requires a separate native profile.");
        List<int> added = [], reusedLocal = [], reusedShared = [];
        List<byte[]> records = [];
        foreach (int id in requested)
        {
            var incoming = donor.Records.SingleOrDefault(r => r.AnimationId == id);
            var existing = target.Records.SingleOrDefault(r => r.AnimationId == id);
            if (incoming == null)
            {
                if (!shared.Contains(id))
                    throw new InvalidDataException($"Requested animation {id} is unavailable in the donor and shared player package.");
                reusedShared.Add(id); continue;
            }
            if (existing != null)
            {
                if (!existing.CopyRecord().AsSpan().SequenceEqual(incoming.CopyRecord()))
                    throw new InvalidDataException($"Destination animation {id} conflicts with the exact donor record; replacement is not implicit.");
                reusedLocal.Add(id); continue;
            }
            added.Add(id); records.Add(incoming.CopyRecord());
        }
        int insertion = checked(streamOffset + target.TerminatorOffset);
        byte[] inserted = new byte[records.Sum(r => r.Length)];
        int cursor = 0;
        foreach (byte[] record in records) { record.CopyTo(inserted, cursor); cursor += record.Length; }
        NativeLevelDataGrowthLayout growth = validation;
        if (inserted.Length > 0)
        {
            _payload = new(0, 37, checked(dataWadOffset + insertion), dataWadEntry, insertion - modelOffset, 0, inserted, 2, false);
            growth = new(original.AsSpan(0, 0x800), original.Length, [_payload]);
            if (dataWadOffset > long.MaxValue - growth.FinalLength)
                throw new InvalidDataException("Expanded player-animation level exceeds its representable WAD owner.");
            _final = growth.Expand(original);
        }
        else _final = original.ToArray();
        OriginalByteLength = original.Length;
        OriginalSha256 = NativePlayerAnimationStreamCodec.Hash(original);
        FinalSha256 = NativePlayerAnimationStreamCodec.Hash(_final);
        RequestedIds = Array.AsReadOnly(requested); AddedIds = added.AsReadOnly();
        ReusedLocalIds = reusedLocal.AsReadOnly(); ReusedSharedIds = reusedShared.AsReadOnly();
        List<NativePlayerAnimationPreservation> preserved = [];
        foreach (var subfile in growth.Subfiles)
        {
            if (subfile.Index == 2 && inserted.Length > 0)
            {
                int prefix = insertion - subfile.OriginalOffset;
                Preserve("models-before-terminator", subfile.OriginalOffset, subfile.FinalOffset, prefix);
                Preserve("terminator-and-all-existing-models", insertion, subfile.FinalOffset + prefix + inserted.Length,
                    subfile.OriginalLength - prefix);
            }
            else Preserve($"subfile-{subfile.Index}", subfile.OriginalOffset, subfile.FinalOffset, subfile.OriginalLength);
        }
        List<NativePlayerAnimationHeaderChange> changes = [];
        var allowed = growth.Subfiles.SelectMany(s => new[] { s.Index * 8, s.Index * 8 + 4 })
            .Concat(Enumerable.Range(0, 64).Select(i => 0x50 + i * 4)).ToHashSet();
        for (int field = 0; field < 0x800; field += 4)
        {
            uint before = Word(original, field), after = Word(_final, field);
            if (before == after) continue;
            if (!allowed.Contains(field)) throw new InvalidDataException("Animation merge modified an unowned native header field.");
            changes.Add(new(field, before, after));
        }
        Preservations = preserved.AsReadOnly(); HeaderChanges = changes.AsReadOnly();
        int finalStart = Integer(_final, 0x50), finalEnd = Integer(_final, 0x54);
        var readback = NativePlayerAnimationStreamCodec.Decode(_final.AsSpan(finalStart, finalEnd - finalStart), sharedGeometry);
        if (!readback.Records.Select(r => r.AnimationId).SequenceEqual(target.Records.Select(r => r.AnimationId).Concat(added)))
            throw new InvalidDataException("Final player-animation stream failed native record-order readback.");
        foreach (var record in target.Records.Concat(donor.Records.Where(r => added.Contains(r.AnimationId))))
            if (readback.Records.Single(r => r.AnimationId == record.AnimationId).Sha256 != record.Sha256)
                throw new InvalidDataException("Final player-animation bytes differ from the owned source record.");
        // Replay the complete nested schema against the expanded header, not just
        // the initial expected delta. No old Moby package or scene byte is changed.
        _ = new NativeLevelDataGrowthLayout(_final.AsSpan(0, 0x800), _final.Length,
            [new(0, 37, checked(dataWadOffset + Integer(_final, 0x10) + Integer(_final, 0x14)), dataWadEntry,
                Integer(_final, 0x14), 0, [], 2, false)]);

        void Preserve(string id, int oldOffset, int newOffset, int length)
        {
            if (length <= 0) return;
            if (!original.AsSpan(oldOffset, length).SequenceEqual(_final.AsSpan(newOffset, length)))
                throw new InvalidDataException($"Animation merge changed protected {id} bytes.");
            preserved.Add(new(id, oldOffset, newOffset, length,
                NativePlayerAnimationStreamCodec.Hash(original.AsSpan(oldOffset, length))));
        }
    }
    internal byte[] CopyFinalBytes() => _final.ToArray();
    internal NativeSkyRelocationPayload? CopyInsertionPayload() => _payload is null ? null : _payload with { Bytes = _payload.Bytes.ToArray() };
    private static uint Word(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b[o..]);
    private static int Integer(ReadOnlySpan<byte> b, int o)
    {
        uint value = Word(b, o);
        if (value > int.MaxValue) throw new InvalidDataException("Native animation owner exceeds signed bounded byte geometry.");
        return (int)value;
    }
}

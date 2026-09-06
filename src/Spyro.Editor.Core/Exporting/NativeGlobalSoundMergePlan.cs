using System.Buffers.Binary;
using System.Text;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

public enum NativeGlobalSoundMappingPolicy { RequireEquivalent, ExplicitRebindUnresolvedConsumers }
public sealed record NativeGlobalSoundRequest(int GlobalSelector, NativeGlobalSoundMappingPolicy MappingPolicy = NativeGlobalSoundMappingPolicy.RequireEquivalent);
public sealed record NativeGlobalSoundBinding(int GlobalSelector, int OriginalDescriptor, int FinalDescriptor, bool MappingChanged,
    bool ConsumerOwnershipResolved, string DonorSampleSha256);
public sealed record NativeSoundSamplePlacement(int BankRelativeOffset, int ByteLength, string Sha256)
{
    public uint SpuUploadAddress => checked((uint)(NativeSoundBankCodec.SpuUploadBase + BankRelativeOffset));
}
public sealed record NativeSoundProtectedExtent(string Id, int OriginalDataOffset, int FinalDataOffset, int ByteLength, string Sha256);
public sealed record NativeSoundFieldChange(string Id, int OriginalDataOffset, int FinalDataOffset, int ByteLength, uint Before, uint After);
internal sealed record NativeSoundOriginalFieldMutation(string Id, int DataOffset, int ByteLength, uint Before, uint After);
internal sealed record NativeSoundDescriptorAllocation(int SourceDescriptorIndex, int TargetDescriptorIndex, string SampleSha256);

/// <summary>
/// Retail-derived whole-level bytes, not a patch over an already edited level.
/// A changed global mapping remains an explicit unresolved consumer obligation.
/// SPU upload-domain fit is not transient RAM, voice/reverb, playback or publication authority.
/// </summary>
public sealed class NativeGlobalSoundMergePlan
{
    private readonly NativeGlobalSoundMergeLayout _layout;
    internal NativeGlobalSoundMergePlan(NativeGlobalSoundSource source, int donor, int target, NativeGlobalSoundMergeLayout layout)
    {
        _layout = layout; SourceImageSha256 = source.SourceImageSha256; DonorLevelId = donor; TargetLevelId = target;
        PlanSha256 = NativeSoundBankCodec.Hash(Encoding.UTF8.GetBytes($"sound-v1|{SourceImageSha256}|{donor}|{target}|{layout.OriginalSha256}|{layout.FinalSha256}|" +
            string.Join(';', layout.Requests.Select(r => $"{r.GlobalSelector}:{(int)r.MappingPolicy}"))));
    }
    public string SourceImageSha256 { get; }
    public int DonorLevelId { get; }
    public int TargetLevelId { get; }
    public string PlanSha256 { get; }
    public string OriginalDataSha256 => _layout.OriginalSha256;
    public string FinalDataSha256 => _layout.FinalSha256;
    public int OriginalDataByteLength => _layout.OriginalLength;
    public int FinalDataByteLength => _layout.FinalLength;
    public int ExactAppendedSampleBytes => _layout.AppendedSampleBytes;
    public int AppendedDescriptorCount => _layout.AppendedDescriptorCount;
    public int SectorRoundedGrowth => FinalDataByteLength - OriginalDataByteLength;
    public int FinalSpuBankByteLength => _layout.FinalBankLength;
    public IReadOnlyList<NativeGlobalSoundRequest> Requests => _layout.Requests;
    public IReadOnlyList<NativeGlobalSoundBinding> Bindings => _layout.Bindings;
    public IReadOnlyList<NativeSoundSamplePlacement> AppendedSamples => _layout.Samples;
    public IReadOnlyList<NativeSoundProtectedExtent> Preservations => _layout.Preservations;
    public IReadOnlyList<NativeSoundFieldChange> FieldChanges => _layout.FieldChanges;
    public bool SpuUploadDomainFitProven => FinalSpuBankByteLength <= NativeSoundBankCodec.SpuUploadByteLength;
    public bool CompleteConsumerOwnershipProven => false;
    public bool RuntimeFitProven => false;
    public bool RuntimePlaybackProven => false;
    public bool PublicationApproved => false;
    public byte[] CopyFinalLevelData() => _layout.CopyFinalBytes();
    internal IReadOnlyList<NativeSkyRelocationPayload> CopyInsertionPayloads() => _layout.CopyInsertionPayloads();
    internal IReadOnlyList<NativeSoundOriginalFieldMutation> CopyNonHeaderMutations() => _layout.CopyNonHeaderMutations();
}

internal sealed class NativeGlobalSoundMergeLayout
{
    private readonly byte[] _final;
    private readonly NativeSkyRelocationPayload[] _insertionPayloads;
    internal string OriginalSha256 { get; }
    internal string FinalSha256 { get; }
    internal int OriginalLength { get; }
    internal int FinalLength => _final.Length;
    internal int AppendedSampleBytes { get; }
    internal int AppendedDescriptorCount { get; }
    internal int FinalBankLength { get; }
    internal IReadOnlyList<NativeGlobalSoundRequest> Requests { get; }
    internal IReadOnlyList<NativeGlobalSoundBinding> Bindings { get; }
    internal IReadOnlyList<NativeSoundSamplePlacement> Samples { get; }
    internal IReadOnlyList<NativeSoundProtectedExtent> Preservations { get; }
    internal IReadOnlyList<NativeSoundFieldChange> FieldChanges { get; }
    internal IReadOnlyList<NativeSoundDescriptorAllocation> DescriptorAllocations { get; } = Array.Empty<NativeSoundDescriptorAllocation>();

    internal NativeGlobalSoundMergeLayout(ReadOnlySpan<byte> targetData, NativeSoundBank donor,
        IReadOnlyList<NativeGlobalSoundRequest> requests, IReadOnlyList<int> localDescriptorRequests,
        int dataWadEntry = 0, long dataWadOffset = 0)
        : this(targetData, donor, requests, localDescriptorRequests, true, dataWadEntry, dataWadOffset) { }

    internal static NativeGlobalSoundRequest[] Snapshot(IReadOnlyList<NativeGlobalSoundRequest> requests, bool allowEmpty = false)
    {
        NativeGlobalSoundRequest[] copy = BoundedSnapshot(requests, allowEmpty ? 0 : 1, 256, "global sound selector");
        if (copy.Any(r => r is null || r.GlobalSelector is < 0 or > 255 || !Enum.IsDefined(r.MappingPolicy)) ||
            copy.Select(r => r.GlobalSelector).Distinct().Count() != copy.Length)
            throw new InvalidDataException("Sound requests require distinct selectors 0..255 and explicit known mapping policies.");
        return copy.OrderBy(r => r.GlobalSelector).ToArray();
    }

    internal static T[] BoundedSnapshot<T>(IReadOnlyList<T> requests, int minimum, int maximum, string label)
    {
        ArgumentNullException.ThrowIfNull(requests);
        int declared = requests.Count;
        if (declared < minimum || declared > maximum)
            throw new InvalidDataException($"Native {label} request count is outside {minimum}..{maximum}.");
        // IReadOnlyList can be user-defined: Count is not an allocation budget
        // for its enumerator. Read at most max+1, then require exact consistency.
        T[] copy = requests.Take(maximum + 1).ToArray();
        if (copy.Length != declared || copy.Length > maximum)
            throw new InvalidDataException($"Native {label} request enumeration contradicts its bounded count.");
        return copy;
    }

    internal NativeGlobalSoundMergeLayout(ReadOnlySpan<byte> targetData, NativeSoundBank donor,
        IReadOnlyList<NativeGlobalSoundRequest> requests, int dataWadEntry = 0, long dataWadOffset = 0)
        : this(targetData, donor, requests, [], false, dataWadEntry, dataWadOffset) { }

    private NativeGlobalSoundMergeLayout(ReadOnlySpan<byte> targetData, NativeSoundBank donor,
        IReadOnlyList<NativeGlobalSoundRequest> requests, IReadOnlyList<int> localDescriptorRequests,
        bool localMode, int dataWadEntry, long dataWadOffset)
    {
        ArgumentNullException.ThrowIfNull(donor);
        int[] locals = BoundedSnapshot(localDescriptorRequests, 0, 256, "local sound descriptor");
        if (locals.Any(i => i < 0 || i >= donor.Descriptors.Count) || locals.Distinct().Count() != locals.Length)
            throw new InvalidDataException("Local sound resources require distinct, owned donor descriptor indices.");
        var snapshot = Snapshot(requests, localMode && locals.Length > 0); Requests = Array.AsReadOnly(snapshot);
        if (targetData.Length < 0x2800 || targetData.Length > 0x800000 || dataWadEntry < 0 || dataWadOffset < 0 || dataWadOffset > long.MaxValue - 0x1000000)
            throw new InvalidDataException("Sound level WAD ownership is outside its bounded domain.");
        byte[] original = targetData.ToArray(); var target = new NativeLevelSoundLayout(original);
        List<byte[]> descriptors = target.Sound.Descriptors.Select(d => d.CopyDescriptor()).ToList();
        List<(int Offset, byte[] Bytes)> sampleOwners = target.Sound.Descriptors.Select(d => d.Sample).DistinctBy(s => s.BankOffset)
            .Select(s => (s.BankOffset, s.CopyBytes())).ToList();
        List<byte> appendedBank = []; List<NativeSoundSamplePlacement> samples = [];
        List<NativeGlobalSoundBinding> bindings = [];
        Dictionary<int, int> allocated = [];
        // Validate global ownership before allocating anything. Equivalent mapped
        // resources retain their original target index, including opaque resources.
        foreach (var request in snapshot)
        {
            NativeSoundDescriptor incoming = donor.Descriptors[donor.Map[request.GlobalSelector]];
            int oldIndex = target.Sound.Map[request.GlobalSelector];
            NativeSoundDescriptor mapped = target.Sound.Descriptors[oldIndex];
            if (Equivalent(mapped, incoming))
                continue;
            if (request.MappingPolicy != NativeGlobalSoundMappingPolicy.ExplicitRebindUnresolvedConsumers)
                throw new InvalidDataException($"Global selector {request.GlobalSelector:X2} already owns a different native resource (descriptor {oldIndex}); explicit rebind with unresolved consumer ownership is required.");
        }
        // Preserve the v1 global-only ordering/goldens. A local-resource transaction
        // has one explicit source-descriptor ordering across both reference kinds.
        var neededGlobals = snapshot.Where(r => !Equivalent(target.Sound.Descriptors[target.Sound.Map[r.GlobalSelector]], donor.Descriptors[donor.Map[r.GlobalSelector]]))
            .Select(r => (int)donor.Map[r.GlobalSelector]);
        int[] resources = (localMode ? neededGlobals.Concat(locals).Distinct().Order() : neededGlobals.Distinct()).ToArray();
        foreach (int index in resources) allocated.Add(index, Allocate(donor.Descriptors[index], localMode));
        foreach (var request in snapshot)
        {
            NativeSoundDescriptor incoming = donor.Descriptors[donor.Map[request.GlobalSelector]];
            int oldIndex = target.Sound.Map[request.GlobalSelector];
            int finalIndex = Equivalent(target.Sound.Descriptors[oldIndex], incoming) ? oldIndex : allocated[incoming.Index];
            bindings.Add(new(request.GlobalSelector, oldIndex, finalIndex, oldIndex != finalIndex, false, incoming.Sample.Sha256));
        }
        DescriptorAllocations = Array.AsReadOnly(locals.Order().Select(index =>
            new NativeSoundDescriptorAllocation(index, allocated[index], donor.Descriptors[index].Sample.Sha256)).ToArray());

        int Allocate(NativeSoundDescriptor incoming, bool allowExactOpaqueReuse)
        {
            // Local references may select an already-resident exact opaque resource.
            // This grants no new loop/unknown-parameter import semantics.
            if (allowExactOpaqueReuse)
            {
                int equivalent = target.Sound.Descriptors.Where(d => Equivalent(d, incoming)).Select(d => d.Index).DefaultIfEmpty(-1).Min();
                if (equivalent >= 0) return equivalent;
            }
            if (!incoming.IsSupportedOneShot)
                throw new InvalidDataException("Import of looping/unknown sound descriptor or ADPCM semantics requires a separate native profile.");
            byte[] sampleBytes = incoming.Sample.CopyBytes(), descriptor = incoming.CopyDescriptor();
            int sampleOffset = sampleOwners.Where(s => s.Bytes.AsSpan().SequenceEqual(sampleBytes)).Select(s => s.Offset).DefaultIfEmpty(-1).Min();
            if (sampleOffset < 0)
            {
                sampleOffset = checked(target.Sound.BankByteLength + appendedBank.Count);
                int rawLength = checked(sampleOffset + sampleBytes.Length);
                int roundedLength = checked((rawLength + 0x7FF) / 0x800 * 0x800);
                if (roundedLength > NativeSoundBankCodec.SpuUploadByteLength)
                    throw new InvalidDataException("Sector-rounded appended SPU bank exceeds the native fixed upload domain.");
                appendedBank.AddRange(sampleBytes); sampleOwners.Add((sampleOffset, sampleBytes));
                samples.Add(new(sampleOffset, sampleBytes.Length, incoming.Sample.Sha256));
            }
            BinaryPrimitives.WriteUInt32LittleEndian(descriptor, (uint)sampleOffset);
            int finalIndex = descriptors.FindIndex(d => d.AsSpan().SequenceEqual(descriptor));
            if (finalIndex < 0)
            {
                if (descriptors.Count == 256) throw new InvalidDataException("Native sound descriptor byte-index capacity is exhausted.");
                finalIndex = descriptors.Count; descriptors.Add(descriptor);
            }
            return finalIndex;
        }
        AppendedSampleBytes = appendedBank.Count; AppendedDescriptorCount = descriptors.Count - target.Sound.Descriptors.Count;
        byte[] appendedDescriptors = descriptors.Skip(target.Sound.Descriptors.Count).SelectMany(d => d).ToArray();
        List<NativeSkyRelocationPayload> payloads = [];
        if (appendedBank.Count > 0) payloads.Add(new(0, 37, dataWadOffset + target.VramOffset + target.VramLength, dataWadEntry,
            target.VramLength, 0, appendedBank.ToArray(), 0, false));
        if (appendedDescriptors.Length > 0) payloads.Add(new(1, 37, dataWadOffset + target.SoundEnd, dataWadEntry,
            target.SoundEnd - target.DataOffset, 0, appendedDescriptors, 1, false));
        _insertionPayloads = payloads.Select(p => p with { Bytes = p.Bytes.ToArray() }).ToArray();
        if (payloads.Count == 0) payloads.Add(new(0, 37, dataWadOffset + target.VramOffset + target.VramLength, dataWadEntry,
            target.VramLength, 0, [], 0, false));
        var growth = new NativeLevelDataGrowthLayout(original.AsSpan(0, 0x800), original.Length, payloads);
        _final = growth.Expand(original);
        int soundDelta = growth.Subfiles[1].FinalOffset - target.DataOffset;
        int finalSoundOffset = target.SoundOffset + soundDelta;
        List<NativeSoundFieldChange> fields = [];
        for (int offset = 0; offset < 0x800; offset += 4)
        {
            uint before = NativeLevelSoundLayout.Word(original, offset), after = NativeLevelSoundLayout.Word(_final, offset);
            if (before != after) fields.Add(new("typed-nested-header", offset, offset, 4, before, after));
        }
        Patch("sound-component-length", target.SoundOffset, finalSoundOffset, 4, (uint)(target.Sound.ComponentByteLength + appendedDescriptors.Length));
        Patch("descriptor-count", target.SoundOffset + 0x104, finalSoundOffset + 0x104, 4, (uint)descriptors.Count);
        foreach (var binding in bindings)
            Patch($"global-selector-{binding.GlobalSelector:X2}", target.SoundOffset + 4 + binding.GlobalSelector,
                finalSoundOffset + 4 + binding.GlobalSelector, 1, (uint)binding.FinalDescriptor);

        List<NativeSoundProtectedExtent> protectedExtents = [];
        foreach (var subfile in growth.Subfiles)
        {
            // Split only at the known append point and typed field owners. Every
            // other original byte, including original sector padding, is compared.
            int cut = subfile.Index == 1 ? target.SoundEnd : subfile.OriginalOffset + subfile.OriginalLength;
            var changed = fields.Where(f => f.OriginalDataOffset >= subfile.OriginalOffset && f.OriginalDataOffset < cut).OrderBy(f => f.OriginalDataOffset).ToArray();
            int cursor = subfile.OriginalOffset;
            foreach (var field in changed)
            {
                Preserve(cursor, field.OriginalDataOffset, subfile.FinalOffset - subfile.OriginalOffset);
                cursor = field.OriginalDataOffset + field.ByteLength;
            }
            Preserve(cursor, cut, subfile.FinalOffset - subfile.OriginalOffset);
            if (cut < subfile.OriginalOffset + subfile.OriginalLength)
                Preserve(cut, subfile.OriginalOffset + subfile.OriginalLength, subfile.FinalOffset - subfile.OriginalOffset + appendedDescriptors.Length);
        }
        var finalLayout = new NativeLevelSoundLayout(_final);
        FinalBankLength = finalLayout.Sound.BankByteLength;
        if (FinalBankLength > NativeSoundBankCodec.SpuUploadByteLength || finalLayout.Sound.Descriptors.Count != descriptors.Count)
            throw new InvalidDataException("Sound merge failed final descriptor/upload-domain readback.");
        foreach (var binding in bindings)
            if (finalLayout.Sound.Map[binding.GlobalSelector] != binding.FinalDescriptor ||
                !Equivalent(finalLayout.Sound.Descriptors[binding.FinalDescriptor], donor.Descriptors[donor.Map[binding.GlobalSelector]]))
                throw new InvalidDataException("Sound merge failed exact mapped-resource readback.");
        foreach (var allocation in DescriptorAllocations)
            if (!Equivalent(finalLayout.Sound.Descriptors[allocation.TargetDescriptorIndex], donor.Descriptors[allocation.SourceDescriptorIndex]))
                throw new InvalidDataException("Sound merge failed exact local descriptor-resource readback.");
        OriginalLength = original.Length; OriginalSha256 = NativeSoundBankCodec.Hash(original); FinalSha256 = NativeSoundBankCodec.Hash(_final);
        Bindings = bindings.AsReadOnly(); Samples = samples.AsReadOnly(); Preservations = protectedExtents.AsReadOnly(); FieldChanges = fields.OrderBy(f => f.OriginalDataOffset).ToList().AsReadOnly();

        void Patch(string id, int oldOffset, int newOffset, int width, uint value)
        {
            uint before = width == 1 ? original[oldOffset] : NativeLevelSoundLayout.Word(original, oldOffset);
            if (before == value) return;
            if (width == 1) _final[newOffset] = checked((byte)value);
            else BinaryPrimitives.WriteUInt32LittleEndian(_final.AsSpan(newOffset), value);
            fields.Add(new(id, oldOffset, newOffset, width, before, value));
        }
        void Preserve(int start, int end, int delta)
        {
            if (end <= start) return;
            if (!original.AsSpan(start, end - start).SequenceEqual(_final.AsSpan(start + delta, end - start)))
                throw new InvalidDataException("Sound merge changed an unowned original level byte.");
            protectedExtents.Add(new($"protected-{start:X}", start, start + delta, end - start, NativeSoundBankCodec.Hash(original.AsSpan(start, end - start))));
        }
    }
    internal byte[] CopyFinalBytes() => _final.ToArray();
    internal IReadOnlyList<NativeSkyRelocationPayload> CopyInsertionPayloads() =>
        Array.AsReadOnly(_insertionPayloads.Select(p => p with { Bytes = p.Bytes.ToArray() }).ToArray());
    internal IReadOnlyList<NativeSoundOriginalFieldMutation> CopyNonHeaderMutations() => Array.AsReadOnly(FieldChanges
        .Where(f => f.OriginalDataOffset >= 0x800).Select(f => new NativeSoundOriginalFieldMutation(f.Id, f.OriginalDataOffset, f.ByteLength, f.Before, f.After)).ToArray());
    internal static bool Equivalent(NativeSoundDescriptor a, NativeSoundDescriptor b) =>
        a.CopyDescriptor().AsSpan(4).SequenceEqual(b.CopyDescriptor().AsSpan(4)) && a.Sample.CopyBytes().AsSpan().SequenceEqual(b.Sample.CopyBytes());
}

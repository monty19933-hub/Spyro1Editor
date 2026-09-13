using System.Buffers.Binary;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Registration into one already computed common layout. The old append-only
/// registration producer remains unchanged. This binds resources, not behavior.
/// </summary>
internal sealed class NativeComposedMobyModelRegistrationPlan
{
    private readonly byte[] _header, _unresolvedHeader;
    private readonly NativeMobyModelPackageBinding[] _bindings;
    private readonly NativeLevelResourceCompositionLayout _layout;
    private NativeComposedMobyModelRegistrationPlan(NativeMobyModelRegistrationSource source,
        NativeMobyModelLoaderCertificate loader, NativeLevelResourceCompositionLayout layout,
        int appendPatchIndex, IEnumerable<NativeMobyModelPackageBinding> requests, Func<NativeByteExtent, byte[]> read)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(layout); ArgumentNullException.ThrowIfNull(requests);
        if (layout.Source != source) throw new InvalidDataException("Composed registration requires the common layout's canonical original model source.");
        source.RequireSource(read); _layout = layout; Loader = loader;
        var list = requests.Take(NativeMobyModelRegistrationPlan.MaximumRequestDeclarations + 1).ToArray();
        if (list.Length is 0 or > NativeMobyModelRegistrationPlan.MaximumRequestDeclarations || list.Any(p => p == null))
            throw new InvalidDataException("Composed model registration request batch is empty, missing or exceeds its bounded declaration budget.");
        List<NativeMobyModelPackageBinding> unique = [];
        foreach (var group in list.GroupBy(p => p.NodeId, StringComparer.Ordinal))
        {
            var first = group.First();
            if (group.Any(p => p.Identity != first.Identity || p.Source.Original != first.Source.Original || p.Destination.Space != first.Destination.Space))
                throw new InvalidDataException("Repeated composed package identity has contradictory source, final domain or payload ownership.");
            unique.Add(first);
        }
        _bindings = unique.OrderBy(p => p.ActorId).ThenBy(p => p.NodeId, StringComparer.Ordinal).ToArray();
        GenericCountBefore = source.Registrations.Count - 1; GenericCountAfter = GenericCountBefore + _bindings.Length;
        if (GenericCountAfter > 63 || _bindings.Select(p => p.ActorId).Distinct().Count() != _bindings.Length)
            throw new InvalidDataException("Composed model registration exceeds63 generic slots or aliases a requested class.");
        if (source.Registrations.Skip(1).Any(r => loader.SharedClasses.Contains(r.ActorId)) ||
            _bindings.Any(p => loader.SharedClasses.Contains(p.ActorId) || source.Registrations.Any(r => r.ActorId == p.ActorId)))
            throw new InvalidDataException("Composed local/shared or existing class ownership collides; no resident registration may be reclaimed.");
        var append = layout.Subfiles[2].Payloads.SingleOrDefault(p => p.PatchIndex == appendPatchIndex)
            ?? throw new InvalidDataException("Composed registrations have no owned model-tail payload.");
        if (append.ModelBlockOffset != source.ModelLength || append.OriginalLength != 0 || append.Bytes.Length == 0)
            throw new InvalidDataException("Composed generic packages must append after the complete shifted original model owner.");
        var owner = layout.PayloadExtent(appendPatchIndex);
        if (_bindings.Sum(p => p.Destination.ByteLength) != owner.ByteLength)
            throw new InvalidDataException("Composed model packages do not exactly partition their single append payload.");
        _header = layout.CopyHeader(); _unresolvedHeader = _header.ToArray();
        int slot = source.Registrations.Count; long cursor = owner.Offset;
        List<NativeMobyModelRegistrationAppend> packages = [];
        foreach (var p in _bindings)
        {
            p.Source.RequireSource(read);
            if (p.Destination.Space != layout.Final || p.Destination.Offset != cursor || p.Destination.EndExclusive > owner.EndExclusive)
                throw new InvalidDataException("Composed package belongs to another final domain or disagrees with deterministic class-sorted placement.");
            int rootField = 0x50 + slot * 4, classField = 0x150 + slot * 2;
            if (BinaryPrimitives.ReadUInt32LittleEndian(_header.AsSpan(rootField)) != 0 ||
                BinaryPrimitives.ReadUInt16LittleEndian(_header.AsSpan(classField)) != 0 || cursor > uint.MaxValue)
                throw new InvalidDataException("Composed registration root/class preimage is occupied or unencodable.");
            _header=NativeMobyRegistrationTableLayout.Decode(_header,layout.Subfiles[2].FinalOffset,layout.Subfiles[2].FinalLength)
                .Append(p.ActorId,(uint)cursor).CopyHeader();
            BinaryPrimitives.WriteUInt16LittleEndian(_unresolvedHeader.AsSpan(classField), p.ActorId);
            packages.Add(new(p.NodeId, p.ActorId, slot, new(layout.Final, rootField, 4), new(layout.Final, classField, 2), p.Destination,
                new("root-" + slot, rootField, p.NodeId, 0, 4, 0)));
            cursor = p.Destination.EndExclusive; slot++;
        }
        Packages = packages.AsReadOnly();
    }
    internal NativeMobyModelLoaderCertificate Loader { get; }
    internal int GenericCountBefore { get; }
    internal int GenericCountAfter { get; }
    internal int FreeSlotsAfter => 63 - GenericCountAfter;
    internal IReadOnlyList<NativeMobyModelRegistrationAppend> Packages { get; }
    internal byte[] CopyHeader() => _header.ToArray();
    internal static NativeComposedMobyModelRegistrationPlan Build(NativeMobyModelRegistrationSource source,
        NativeMobyModelLoaderCertificate loader, NativeLevelResourceCompositionLayout layout, int appendPatchIndex,
        IEnumerable<NativeMobyModelPackageBinding> requests, Func<NativeByteExtent, byte[]> read) =>
        new(source, loader, layout, appendPatchIndex, requests, read);
    internal NativeMobyDependencyNode HeaderNode()
    {
        var source = _layout.Source;
        var preimage = new NativeMobyDependencyRead("original", new(source.Original, 0, 0x800), NativeMobyModelLoaderCertificate.Hash(source.CopyHeader()));
        return new("resource.header", NativeMobyDependencyKind.ActorRegistration, NativeMobyDependencyResolution.ImportFromSource,
            NativeMobyDependencySharing.Immutable, preimage.ExpectedSha256, _bindings.Select(p => p.NodeId).ToArray(), [preimage],
            c => { _ = c.Read("original"); return new(_unresolvedHeader, [new("final-header", new(_layout.Final, 0, 0x800))], Packages.Select(p => p.Reference)); });
    }
    internal void RequireFinal(ReadOnlySpan<byte> data, NativeMobyDependencyClosurePlan graph)
    {
        _layout.RequireFinalBody(data);
        if (!data[..0x800].SequenceEqual(_header) || !graph.CopyPayload("resource.header").AsSpan().SequenceEqual(_header))
            throw new InvalidDataException("Composed final header differs from its single typed registration owner.");
        foreach (var p in _bindings)
        {
            p.RequireClosure(graph);
            if (!data.Slice((int)p.Destination.Offset, (int)p.Destination.ByteLength).SequenceEqual(p.CopyPayload()))
                throw new InvalidDataException("Composed registered package differs from its source-bound resource graph.");
        }
        int end = checked((int)Packages.Max(p => p.Package.EndExclusive));
        var model = _layout.Subfiles[2];
        if (data.Slice(end, model.FinalOffset + model.FinalLength - end).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Combined model alignment padding has unexplained nonzero bytes.");
    }
}

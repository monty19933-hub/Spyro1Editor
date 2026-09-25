using System.Buffers.Binary;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeJapaneseEggModelPointerPlacement(int PackageFieldOffset, long OriginalFieldOffset,
    long FinalFieldOffset, long OriginalTargetOffset, long FinalTargetOffset, uint SourceWord,
    NativeJapaneseEggPointerEncoding Encoding);

/// <summary>
/// Detached model-data composition, not a native actor/import/export capability.
/// The existing tail transaction owns every actual write; the existing dependency
/// compiler independently resolves the registration and proves retained owners.
/// Internal model words stay in native source-relative disk form.
/// </summary>
public sealed class NativeJapaneseEggModelAssemblyPlan
{
    private readonly NativeModelTailBatch _batch;
    private readonly byte[] _final;
    private NativeJapaneseEggModelAssemblyPlan(NativeJapaneseEggModelAssetPlan asset, byte[] current,
        int sourceWadEntry, bool certified)
    {
        asset.RequireCurrentAncestry(current, sourceWadEntry, certified);
        SourceCertified = certified; SourceWadEntry = sourceWadEntry;
        OriginalResourceSha256 = NativeJapaneseEggModelAssetPlan.Hash(current);
        _batch = NativeModelTailBatch.Compile(current, [asset.ModelContribution], asset.SharedClasses);
        var entry = _batch.Packages.Single();
        if (!ReferenceEquals(entry.Input, asset.ModelContribution))
            throw new InvalidDataException("Model append lost its actual source issuer identity.");
        PackageOffset = entry.PackageOffset; PackageByteLength = entry.ByteLength;
        RegistrationSlot = entry.Slot; ModelGrowthBytes = _batch.ModelGrowthBytes;
        _final = _batch.CopyFinalBody();
        FinalResourceSha256 = NativeJapaneseEggModelAssetPlan.Hash(_final);
        OriginalDomain = new(NativeAddressKind.LevelData,
            (certified ? "usa-source:" : "fixture:") + sourceWadEntry + ":" + OriginalResourceSha256,
            NativeAddressRevision.Original, current.Length);
        FinalDomain = new(NativeAddressKind.LevelData, "detached-jp-f5:" + FinalResourceSha256,
            NativeAddressRevision.Final, _final.Length);
        var destination = new NativeByteExtent(FinalDomain, entry.PackageOffset, entry.ByteLength);
        var map = new NativeRelocationMap(asset.OriginalDataDomain, FinalDomain,
            [new("jp-complete-model", new(asset.OriginalDataDomain, asset.SourceRootDataOffset, entry.ByteLength), destination, 4)]);
        PointerPlacements = Array.AsReadOnly(asset.PointerFields.Select(field =>
        {
            var mappedField = map.Map(new(asset.OriginalDataDomain, asset.SourceRootDataOffset + field.PackageFieldOffset, 4));
            var mappedTarget = map.Map(new(asset.OriginalDataDomain, asset.SourceRootDataOffset + field.PackageTargetOffset, 4));
            if (NativeJapaneseEggModelAssetPlan.Word(_final, checked((int)mappedField.Final.Offset)) != field.SourceWord)
                throw new InvalidDataException("A model-owned relative field was incorrectly pre-rebased in disk form.");
            return new NativeJapaneseEggModelPointerPlacement(field.PackageFieldOffset, mappedField.Original.Offset,
                mappedField.Final.Offset, mappedTarget.Original.Offset, mappedTarget.Final.Offset, field.SourceWord, field.Encoding);
        }).ToArray());

        List<NativeMobyDependencyNode> nodes = asset.PackageNodes(destination).ToList();
        const string registration = "jp.f5.detached-registration-root";
        var oldRoot = new NativeMobyDependencyRead("empty-root", new(OriginalDomain, entry.RootFieldOffset, 4),
            NativeJapaneseEggModelAssetPlan.Hash(current.AsSpan(entry.RootFieldOffset, 4)));
        nodes.Add(new(registration, NativeMobyDependencyKind.ActorRegistration, NativeMobyDependencyResolution.ImportFromSource,
            NativeMobyDependencySharing.Immutable, oldRoot.ExpectedSha256, [NativeJapaneseEggModelAssetPlan.PackageNodeId], [oldRoot],
            context => new(context.Read(oldRoot.Id), [new("new-root", new(FinalDomain, entry.RootFieldOffset, 4))],
                [new("actor-root", 0, NativeJapaneseEggModelAssetPlan.PackageNodeId, 0, 4, ExpectedBefore: 0)])));
        List<string> roots = [registration];
        // Preserve every descriptor-owned original subfile, including the whole
        // old model allocation and scene. The tail kernel separately owns the
        // complete header and inserted/alignment complements and exact inverse.
        for (int i = 0; i < 10; i++)
        {
            int originalOffset = checked((int)NativeJapaneseEggModelAssetPlan.Word(current, i * 8));
            int length = checked((int)NativeJapaneseEggModelAssetPlan.Word(current, i * 8 + 4));
            if (length == 0) continue;
            int finalOffset = checked((int)NativeJapaneseEggModelAssetPlan.Word(_final, i * 8));
            string id = "usa.retained-subfile." + i;
            var read = new NativeMobyDependencyRead("original-subfile", new(OriginalDomain, originalOffset, length),
                NativeJapaneseEggModelAssetPlan.Hash(current.AsSpan(originalOffset, length)));
            nodes.Add(new(id, NativeMobyDependencyKind.RuntimeBuffer, NativeMobyDependencyResolution.DestinationResident,
                NativeMobyDependencySharing.Immutable, read.ExpectedSha256, [], [read],
                Preserved: [new(read.Id, 0, new(FinalDomain, finalOffset, length))]));
            roots.Add(id);
        }
        byte[] ReadSource(NativeByteExtent extent) => ReferenceEquals(extent.Space, OriginalDomain)
            ? current.AsSpan(checked((int)extent.Offset), checked((int)extent.ByteLength)).ToArray() : asset.ReadSource(extent);
        Graph = NativeMobyDependencyClosureCompiler.Compile(nodes, roots, ReadSource);
        Graph.RequirePreservedFinalBytes(FinalDomain, _final, ReadSource);
        foreach (var install in Graph.Installs)
            if (!ReferenceEquals(install.Destination.Space, FinalDomain) ||
                !_final.AsSpan(checked((int)install.Destination.Offset), checked((int)install.Destination.ByteLength))
                    .SequenceEqual(Graph.CopyPayload(install.NodeId)))
                throw new InvalidDataException("Dependency materialization disagrees with the existing tail transaction afterimage.");
        if (Graph.References.Count != 1 || Graph.References[0].Before != 0 || Graph.References[0].After != entry.PackageOffset ||
            BinaryPrimitives.ReadUInt16LittleEndian(_final.AsSpan(entry.ClassFieldOffset)) != 0xF5 ||
            !_batch.RevertExact(_final).AsSpan().SequenceEqual(current))
            throw new InvalidDataException("Detached model registration or complete-resource inverse differs from its typed proof.");
    }
    public bool SourceCertified { get; }
    public int SourceWadEntry { get; }
    public string OriginalResourceSha256 { get; }
    public string FinalResourceSha256 { get; }
    public string DependencyGraphSha256 => Graph.Fingerprint;
    public int PackageOffset { get; }
    public int PackageByteLength { get; }
    public int RegistrationSlot { get; }
    public int ModelGrowthBytes { get; }
    public IReadOnlyList<NativeJapaneseEggModelPointerPlacement> PointerPlacements { get; }
    public bool NativeImportApproved => false;
    public bool RuntimeVerified => false;
    public bool PublicationApproved => false;
    public byte[] CopyLevelData() => _final.ToArray();
    public byte[] CopyModelPackage() => _final.AsSpan(PackageOffset, PackageByteLength).ToArray();
    public byte[] RevertExact(ReadOnlySpan<byte> afterimage) => _batch.RevertExact(afterimage);
    internal NativeAddressSpace OriginalDomain { get; }
    internal NativeAddressSpace FinalDomain { get; }
    internal NativeMobyDependencyClosurePlan Graph { get; }
    internal static NativeJapaneseEggModelAssemblyPlan Compile(NativeJapaneseEggModelAssetPlan asset,
        ReadOnlySpan<byte> current, int sourceWadEntry, bool certified)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return new(asset, current.ToArray(), sourceWadEntry, certified);
    }
}

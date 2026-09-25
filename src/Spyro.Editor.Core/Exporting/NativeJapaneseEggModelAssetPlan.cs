using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeJapaneseEggModelAssemblySmoke")]

namespace Spyro.Editor.Core.Exporting;

public enum NativeJapaneseEggPointerEncoding { RootRelative, ModelDataRelative, PackedFrameModelDataRelative }
public enum NativeJapaneseEggUsaResource { Artisans, TownSquare }
public sealed record NativeJapaneseEggPointerField(int PackageFieldOffset, int PackageTargetOffset,
    uint SourceWord, NativeJapaneseEggPointerEncoding Encoding);
public sealed record NativeJapaneseEggLoaderAddress(int PackageFieldOffset, uint SourceWord,
    uint ProjectedLoadedWord, uint RuntimeTargetAddress);
public sealed record NativeJapaneseEggRegionalModelCompatibility(string JapaneseLoaderSha256, string UsaLoaderSha256,
    string JapaneseVertexConsumerSha256, string UsaVertexConsumerSha256, int RelocatedLoaderJumpWords,
    int RelocatedVertexJumpWords, int ScratchAddressWords, int ClippingConstantWords);

/// <summary>
/// Explicit JP F5 source issuer for detached USA model-data composition only.
/// Source-relative package bytes are immutable. USA native loader rebasing is
/// projected for verification, never applied to the disk-form package.
/// </summary>
public sealed class NativeJapaneseEggModelAssetPlan
{
    private readonly byte[] _donor, _jpExe, _usaExe;
    private readonly NativeAddressSpace _jpExeDomain, _usaExeDomain;
    private readonly NativeMobyModelLoaderCertificate _loader;
    internal const string PackageNodeId = "jp.f5.source-form-model";
    private NativeJapaneseEggModelAssetPlan(NativeJapaneseEggModel model, byte[] donor, int root,
        byte[] jpExe, byte[] usaExe, NativeMobyModelLoaderCertificate loader, bool certified)
    {
        Model = model; _donor = donor.ToArray(); SourceRootDataOffset = root;
        _jpExe = jpExe.ToArray(); _usaExe = usaExe.ToArray(); _loader = loader;
        SourceCertified = certified;
        if (root < 0 || root > donor.Length - model.PackageByteLength ||
            !donor.AsSpan(root, model.PackageByteLength).SequenceEqual(model.CopyPackage()) ||
            certified && (!model.SourceGeometryClosureProven || !loader.IsRetailCertified))
            throw new InvalidDataException("Regional model asset lost its exact source/loader ancestry.");
        OriginalDataDomain = new(NativeAddressKind.LevelData,
            "jp-model-source:" + (model.SourceImageSha256 ?? "fixture") + ":" + Hash(donor), NativeAddressRevision.Original, donor.Length);
        _jpExeDomain = new(NativeAddressKind.ExecutableFile, "jp-model-loader:" + Hash(jpExe), NativeAddressRevision.Original, Math.Max(1, jpExe.Length));
        _usaExeDomain = new(NativeAddressKind.ExecutableFile, "usa-model-loader:" + Hash(usaExe), NativeAddressRevision.Original, Math.Max(1, usaExe.Length));
        ModelContribution = new(0xF5, PackageNodeId, model.CopyPackage());
        byte[] package = model.CopyPackage();
        List<NativeJapaneseEggPointerField> fields = [];
        Add(0x34, 0, NativeJapaneseEggPointerEncoding.RootRelative);
        Add(0x38, 0, NativeJapaneseEggPointerEncoding.RootRelative);
        foreach (int p in new[] { 0x50, 0x54, 0x58, 0x5C }) Add(p, 0x70, NativeJapaneseEggPointerEncoding.ModelDataRelative);
        foreach (int p in new[] { 0x60, 0x68 }) Add(p, 0x70, NativeJapaneseEggPointerEncoding.PackedFrameModelDataRelative);
        PointerFields = fields.AsReadOnly();
        if (certified) Compatibility = RequireCompatibility(jpExe, usaExe);
        void Add(int field, int basis, NativeJapaneseEggPointerEncoding encoding)
        {
            uint word = Word(package, field);
            int target = checked(basis + (int)(encoding == NativeJapaneseEggPointerEncoding.PackedFrameModelDataRelative ? word & 0x1FFFFF : word));
            if (target % 4 != 0 || target < 0 || target > package.Length - 4)
                throw new InvalidDataException("Model pointer leaves its authenticated complete package.");
            fields.Add(new(field, target, word, encoding));
        }
    }
    public NativeJapaneseEggModel Model { get; }
    public bool SourceCertified { get; }
    public string? JapaneseSourceImageSha256 => Model.SourceImageSha256;
    public string? UsaSourceImageSha256 => SourceCertified ? NativeMobySourceInventory.OriginalImageSha256 : null;
    public NativeJapaneseEggRegionalModelCompatibility? Compatibility { get; }
    public IReadOnlyList<NativeJapaneseEggPointerField> PointerFields { get; }
    public bool NativeImportApproved => false;
    public bool RuntimeVerified => false;
    public bool PublicationApproved => false;
    internal NativeAddressSpace OriginalDataDomain { get; }
    internal int SourceRootDataOffset { get; }
    internal NativeModelTailPackage ModelContribution { get; }
    internal IReadOnlyList<ushort> SharedClasses => _loader.SharedClasses;

    public static NativeJapaneseEggModelAssetPlan Capture(NativeRegionalSourceSession japan, NativeRegionalSourceSession usa)
    {
        ArgumentNullException.ThrowIfNull(japan); ArgumentNullException.ThrowIfNull(usa);
        if (japan.Revision != NativeDiscRevision.JapanScps10083 || usa.Revision != NativeDiscRevision.UsaScus94228)
            throw new InvalidDataException("Regional model assembly requires separate authenticated JP donor and USA loader sources.");
        var model = NativeJapaneseEggModel.Capture(japan);
        byte[] donor = japan.ReadWadEntry(NativeJapaneseEggModel.DataWadEntry);
        byte[] jpExe = japan.ReadExecutableRange(0, japan.ExecutableByteLength), usaExe = usa.ReadExecutableRange(0, usa.ExecutableByteLength);
        var loader = NativeMobyModelLoaderCertificate.CaptureRetail(usaExe,
            usa.ReadWadRange(0x7B8800, 0x800), usa.ReadWadRange(0x7B9000, 0x39C48));
        if (loader.SharedClasses.Contains((ushort)0xF5)) throw new InvalidDataException("Japanese F5 collides with a USA shared model.");
        var result = new NativeJapaneseEggModelAssetPlan(model, donor, NativeJapaneseEggModel.SourceRootDataOffset, jpExe, usaExe, loader, true);
        japan.VerifyUnchanged(); usa.VerifyUnchanged(); return result;
    }

    /// <summary>Detached original-resource proof cases, not a generalized destination/export capability.</summary>
    public NativeJapaneseEggModelAssemblyPlan AssembleOriginalUsaResource(NativeRegionalSourceSession usa, NativeJapaneseEggUsaResource resource)
    {
        ArgumentNullException.ThrowIfNull(usa);
        if (!SourceCertified || usa.Revision != NativeDiscRevision.UsaScus94228 || usa.SourceImageSha256 != UsaSourceImageSha256)
            throw new InvalidDataException("Only the actual regional issuer may compose its authenticated USA resource.");
        var spec = SourceSpecification(resource switch
        {
            NativeJapaneseEggUsaResource.Artisans => 10,
            NativeJapaneseEggUsaResource.TownSquare => 16,
            _ => throw new InvalidDataException("This detached source-resource proof has no such destination.")
        });
        byte[] current = usa.ReadWadEntry(spec.Entry);
        if (current.Length != spec.Length || usa.Entries[spec.Entry].Offset != spec.Wad || Hash(current) != spec.Hash)
            throw new InvalidDataException("Original USA resource ownership changed.");
        var result = NativeJapaneseEggModelAssemblyPlan.Compile(this, current, spec.Entry, true);
        usa.VerifyUnchanged(); return result;
    }

    internal void RequireCurrentAncestry(ReadOnlySpan<byte> current, int entry, bool certified)
    {
        if (certified != SourceCertified)
            throw new InvalidDataException("A fixture cannot acquire authenticated regional model ancestry.");
        if (!certified) return;
        var spec = SourceSpecification(entry);
        if (current.Length != spec.Length || Hash(current) != spec.Hash)
            throw new InvalidDataException("Certified composition requires its complete exact original USA resource, not an edited or arbitrary current body.");
    }
    private static (int Entry, int Wad, int Length, string Hash) SourceSpecification(int entry) => entry switch
    {
        10 => (10, 0x800800, 0x383000, "86cde4f886d97821a3a4b869100b586842dbd4b9fb696558bead274bb547af4f"),
        16 => (16, 0x119E000, 0x2E2000, "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0"),
        _ => throw new InvalidDataException("This detached source-resource proof has no such destination.")
    };

    /// <summary>Pure address projection of the pinned native loader; no bytes are patched.</summary>
    public IReadOnlyList<NativeJapaneseEggLoaderAddress> ProjectLoaderAddresses(uint runtimeRoot)
    {
        if (runtimeRoot % 4 != 0 || runtimeRoot < 0x80000000 ||
            (ulong)runtimeRoot + (uint)Model.PackageByteLength > 0x80200000)
            throw new InvalidDataException("Projected root must fit one aligned package in PS1 KSEG0 main RAM; this is not a memory-fit certificate.");
        List<NativeJapaneseEggLoaderAddress> result = [];
        foreach (var field in PointerFields)
        {
            uint target = checked(runtimeRoot + (uint)field.PackageTargetOffset), after;
            if (field.Encoding == NativeJapaneseEggPointerEncoding.PackedFrameModelDataRelative)
            {
                // Encoding-zero loader adds low21(model-data address), retaining
                // the source frame flags. Root bounds prove no low21 carry.
                after = checked(field.SourceWord + ((runtimeRoot + 0x70) & 0x1FFFFF));
                if ((after & 0xFFE00000) != (field.SourceWord & 0xFFE00000) || (after & 0x1FFFFF) != (target & 0x1FFFFF))
                    throw new InvalidDataException("Projected frame relocation changes flags or leaves the RAM window.");
            }
            else after = target;
            result.Add(new(field.PackageFieldOffset, field.SourceWord, after, target));
        }
        return result.AsReadOnly();
    }

    internal IEnumerable<NativeMobyDependencyNode> PackageNodes(NativeByteExtent destination)
    {
        if (destination.ByteLength != Model.PackageByteLength || destination.Space.Revision != NativeAddressRevision.Final)
            throw new InvalidDataException("Model graph needs its complete actual Final package allocation.");
        string[] dependencies = SourceCertified ? ["jp.f5.usa-loader-grammar"] : [];
        if (SourceCertified)
        {
            NativeMobyDependencyRead[] reads =
            [
                new("jp-loader", new(_jpExeDomain, 0x30D8, 0x26C), Compatibility!.JapaneseLoaderSha256),
                new("usa-loader", new(_usaExeDomain, 0x3BE0, 0x26C), Compatibility.UsaLoaderSha256),
                new("jp-vertices", new(_jpExeDomain, 0x106C4, 0x474), Compatibility.JapaneseVertexConsumerSha256),
                new("usa-vertices", new(_usaExeDomain, 0x11938, 0x474), Compatibility.UsaVertexConsumerSha256)
            ];
            yield return new("jp.f5.usa-loader-grammar", NativeMobyDependencyKind.NativeRoutine, NativeMobyDependencyResolution.Aggregate,
                NativeMobyDependencySharing.Immutable, Hash(Encoding.UTF8.GetBytes(string.Join('|', reads.Select(r => r.ExpectedSha256)))), [], reads);
        }
        var read = new NativeMobyDependencyRead("jp-complete-model", new(OriginalDataDomain, SourceRootDataOffset, Model.PackageByteLength), Model.PackageSha256);
        yield return new(PackageNodeId, NativeMobyDependencyKind.ActorPackage, NativeMobyDependencyResolution.ImportFromSource,
            NativeMobyDependencySharing.Immutable, Model.PackageSha256, dependencies, [read], context =>
            {
                byte[] bytes = context.Read(read.Id);
                if (!bytes.AsSpan().SequenceEqual(ModelContribution.CopyBytes())) throw new InvalidDataException("JP model graph lost its exact source-form package.");
                return new(bytes, [new("detached-usa-source-form-model", destination)], []);
            });
    }
    internal byte[] ReadSource(NativeByteExtent extent)
    {
        ReadOnlySpan<byte> bytes = ReferenceEquals(extent.Space, OriginalDataDomain) ? _donor :
            ReferenceEquals(extent.Space, _jpExeDomain) ? _jpExe : ReferenceEquals(extent.Space, _usaExeDomain) ? _usaExe :
            throw new InvalidDataException("Model read belongs to another source domain, not merely an equal source label.");
        return bytes.Slice(checked((int)extent.Offset), checked((int)extent.ByteLength)).ToArray();
    }
    internal static NativeJapaneseEggModelAssetPlan Fixture(ReadOnlySpan<byte> package, IEnumerable<ushort>? shared = null) =>
        new(NativeJapaneseEggModel.DecodeFixture(package), package.ToArray(), 0, [], [], NativeMobyModelLoaderCertificate.DecodeFixture(shared ?? []), false);

    private static NativeJapaneseEggRegionalModelCompatibility RequireCompatibility(byte[] jp, byte[] usa)
    {
        const string jl = "67eb37fcb000ba1634438c7d45311481c766e171114dae214429731cb8d3f7a4", ul = "fede3c414aea2dce0372fce574549d961123cf6456a7a291acef48b9c4af8598";
        const string jv = "cf1bf8f5b7852586b9d63f49d9ccf759fb426f3515f62880d617bee3887a6db6", uv = "11c407e46e92faa2fa65b8b1f9689a99d00eb56aaafcbb25ceefd3fd2a9ebc22";
        if (Hash(jp.AsSpan(0x30D8, 0x26C)) != jl || Hash(usa.AsSpan(0x3BE0, 0x26C)) != ul ||
            Hash(jp.AsSpan(0x106C4, 0x474)) != jv || Hash(usa.AsSpan(0x11938, 0x474)) != uv)
            throw new InvalidDataException("Regional model consumers differ from the proved source windows.");
        Compare(0x30D8, 0x3BE0, 0x26C, [0x44, 0x1C0]);
        Compare(0x106C4, 0x11938, 0x474, [0x50, 0x54, 0x68, 0x6C, 0xE8, 0x124, 0x1A8, 0x1E8, 0x1FC, 0x2B0, 0x2F0, 0x348, 0x390, 0x418]);
        return new(jl, ul, jv, uv, 2, 10, 2, 2);
        void Compare(int j, int u, int length, int[] expected)
        {
            int[] differences = Enumerable.Range(0, length / 4).Select(i => i * 4).Where(i => Word(jp, j + i) != Word(usa, u + i)).ToArray();
            if (!differences.SequenceEqual(expected)) throw new InvalidDataException("Regional model grammar differs beyond its documented relocated addresses/clipping constants.");
        }
    }
    internal static uint Word(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

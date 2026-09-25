using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Analysis;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeJapaneseEggRgbEditSmoke")]

namespace Spyro.Editor.Core.Exporting;

public enum NativeJapaneseEggPaletteOrigin { NativeOriginal, Authored }
public sealed record NativeJapaneseEggRgbField(bool Far,int Index,int PackageOffset,uint OriginalRgb,uint FinalRgb);

/// <summary>
/// Narrow source issuer for the F5 model's two packed RGB arrays. The actual
/// source asset and native RGB consumer, not caller package/hash metadata, own
/// the editable fields. It confers no changed-model import or instance binding.
/// </summary>
public sealed class NativeJapaneseEggRgbEditSource
{
    public const int ColorsPerTable=70;
    private static readonly NativeJapaneseEggModelConsumer Consumer=new(0x800215C8,0x11DC8,0x174,
        "4cd236569c340e49360938ad8397aed87f2a7518d13134c935fd7f32acb856a5");
    private readonly byte[] _original;
    private readonly uint[] _near,_far;
    private readonly NativeByteExtent[] _rgbOwners;
    private NativeJapaneseEggRgbEditSource(NativeJapaneseEggModelAssetPlan asset,bool certified)
    {
        AssetAncestor=asset;SourceAncestryCertified=certified;
        if(asset.SourceCertified!=certified||certified&&(!asset.Model.SourceGeometryClosureProven||
            asset.Model.PackageSha256!=NativeJapaneseEggModel.SourcePackageSha256||asset.Compatibility is null))
            throw new InvalidDataException("RGB source requires the exact actual F5 asset ancestry.");
        _original=asset.Model.CopyPackage();
        NearRange=asset.Model.Ranges.Single(r=>r.Role=="near-packed-shading-words");
        FarRange=asset.Model.Ranges.Single(r=>r.Role=="far-packed-shading-words");
        if(asset.Model.NearPackedShadingWords.Count!=ColorsPerTable||asset.Model.FarPackedShadingWords.Count!=ColorsPerTable||
            NearRange.ByteLength!=ColorsPerTable*4||FarRange.ByteLength!=ColorsPerTable*4||
            NearRange.PackageOffset%4!=0||FarRange.PackageOffset%4!=0||
            NearRange.PackageOffset+NearRange.ByteLength>FarRange.PackageOffset)
            throw new InvalidDataException("RGB edits support only two ordered, disjoint 70-word F5 shading owners.");
        _near=new uint[ColorsPerTable];_far=new uint[ColorsPerTable];
        List<NativeByteExtent> fields=[];
        foreach(var range in new[]{NearRange,FarRange})
        for(int i=0;i<ColorsPerTable;i++)
        {
            int at=range.PackageOffset+i*4;bool far=ReferenceEquals(range,FarRange);
            uint word=BinaryPrimitives.ReadUInt32LittleEndian(_original.AsSpan(at));
            if((byte)(word>>24)!=(far?0:0xFF))
                throw new InvalidDataException("Only source near CODE=FF and far CODE=00 are supported; CODE is not RGB/alpha.");
            (far?_far:_near)[i]=word&0xFFFFFF;
            // Original extents retain the actual asset's original-data identity.
            fields.Add(new(asset.OriginalDataDomain,asset.SourceRootDataOffset+at,3));
        }
        _rgbOwners=fields.ToArray();
        NativeNearRgb=Array.AsReadOnly(_near);NativeFarRgb=Array.AsReadOnly(_far);
    }
    public NativeJapaneseEggModelAssetPlan AssetAncestor{get;}
    public bool SourceAncestryCertified{get;}
    public NativeJapaneseEggModelRange NearRange{get;}
    public NativeJapaneseEggModelRange FarRange{get;}
    public IReadOnlyList<uint> NativeNearRgb{get;}
    public IReadOnlyList<uint> NativeFarRgb{get;}
    public NativeJapaneseEggModelConsumer RgbConsumer=>Consumer;
    public string OriginalPackageSha256=>AssetAncestor.Model.PackageSha256;
    public bool NativeImportApproved=>false;
    public bool DistinctPerInstanceRootBindingProven=>false;
    public bool PublicationApproved=>false;
    internal IReadOnlyList<NativeByteExtent> RgbOwners=>Array.AsReadOnly(_rgbOwners);
    internal byte[] CopyOriginal()=>_original.ToArray();

    public static NativeJapaneseEggRgbEditSource Capture(NativeJapaneseEggModelAssetPlan asset,NativeRegionalSourceSession usa)
    {
        ArgumentNullException.ThrowIfNull(asset);ArgumentNullException.ThrowIfNull(usa);
        if(!asset.SourceCertified||usa.Revision!=NativeDiscRevision.UsaScus94228||
            usa.SourceImageSha256!=asset.UsaSourceImageSha256||
            Hash(usa.ReadExecutableRange(Consumer.ExecutableOffset,Consumer.ByteLength))!=Consumer.Sha256)
            throw new InvalidDataException("RGB source requires the actual regional asset and exact-USA animated RGB consumer.");
        var result=new NativeJapaneseEggRgbEditSource(asset,true);usa.VerifyUnchanged();return result;
    }
    public NativeJapaneseEggRgbEditPlan NativeOriginal()=>new(this,NativeJapaneseEggPaletteOrigin.NativeOriginal,_near,_far);

    /// <summary>
    /// Authored values are 0x00BBGGRR (R in low byte), not native preset IDs.
    /// Exactly 70 values per table are required; high-byte/CODE input is refused.
    /// No tint algorithm, destination bytes or per-instance root are inferred.
    /// </summary>
    public NativeJapaneseEggRgbEditPlan CreateAuthored(ReadOnlySpan<uint> nearRgb,ReadOnlySpan<uint> farRgb)=>
        new(this,NativeJapaneseEggPaletteOrigin.Authored,nearRgb,farRgb);
    internal static NativeJapaneseEggRgbEditSource Fixture(NativeJapaneseEggModelAssetPlan asset)=>new(asset,false);
    internal static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}

/// <summary>
/// Immutable detached RGB-only afterimage. Its identity describes content, not
/// native authenticity, final pixels, a model registry slot or publication.
/// An authored palette equal to native bytes keeps the same content identity
/// while retaining Authored provenance explicitly.
/// </summary>
public sealed class NativeJapaneseEggRgbEditPlan
{
    private readonly byte[] _original,_after;
    private readonly uint[] _near,_far;
    internal NativeJapaneseEggRgbEditPlan(NativeJapaneseEggRgbEditSource source,NativeJapaneseEggPaletteOrigin origin,
        ReadOnlySpan<uint> nearRgb,ReadOnlySpan<uint> farRgb)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(!Enum.IsDefined(origin)||nearRgb.Length!=NativeJapaneseEggRgbEditSource.ColorsPerTable||
            farRgb.Length!=NativeJapaneseEggRgbEditSource.ColorsPerTable)
            throw new InvalidDataException("An F5 palette requires exactly 70 near and 70 far RGB values.");
        _near=nearRgb.ToArray();_far=farRgb.ToArray();
        if(_near.Concat(_far).Any(w=>(w&0xFF000000)!=0))
            throw new InvalidDataException("Authored RGB values must not contain CODE/high-byte bits.");
        if(origin==NativeJapaneseEggPaletteOrigin.NativeOriginal&&
            (!_near.SequenceEqual(source.NativeNearRgb)||!_far.SequenceEqual(source.NativeFarRgb)))
            throw new InvalidDataException("Native-original provenance cannot describe authored RGB values.");
        Source=source;Origin=origin;_original=source.CopyOriginal();_after=_original.ToArray();
        List<NativeJapaneseEggRgbField> fields=[];
        var owners=source.RgbOwners;
        for(int table=0;table<2;table++)for(int i=0;i<NativeJapaneseEggRgbEditSource.ColorsPerTable;i++)
        {
            int ownerIndex=table*NativeJapaneseEggRgbEditSource.ColorsPerTable+i;var owner=owners[ownerIndex];
            if(!ReferenceEquals(owner.Space,source.AssetAncestor.OriginalDataDomain)||owner.ByteLength!=3)
                throw new InvalidDataException("RGB field lost exact three-byte source ownership.");
            int at=checked((int)(owner.Offset-source.AssetAncestor.SourceRootDataOffset));uint value=(table==0?_near:_far)[i];
            _after[at]=(byte)value;_after[at+1]=(byte)(value>>8);_after[at+2]=(byte)(value>>16);
            fields.Add(new(table==1,i,at,BinaryPrimitives.ReadUInt32LittleEndian(_original.AsSpan(at))&0xFFFFFF,value));
        }
        Fields=fields.AsReadOnly();NearRgb=Array.AsReadOnly(_near);FarRgb=Array.AsReadOnly(_far);
        var owned=fields.SelectMany(f=>Enumerable.Range(f.PackageOffset,3)).ToHashSet();
        for(int i=0;i<_original.Length;i++)
        {
            if(_original[i]!=_after[i]){ChangedByteCount++;if(!owned.Contains(i))throw new InvalidDataException("RGB edit changed an unowned model byte.");}
        }
        // Existing geometry parser is reused for readback, not a second grammar
        // or a native-source certificate for the authored output.
        if(!NativeJapaneseEggModel.DecodeFixture(_after).CopyPackage().AsSpan().SequenceEqual(_after))
            throw new InvalidDataException("RGB afterimage changed complete model package closure.");
        FinalPackageSha256=NativeJapaneseEggRgbEditSource.Hash(_after);
        VariantIdentity=NativeJapaneseEggRgbEditSource.Hash(Encoding.UTF8.GetBytes(
            "jp-f5-rgb-v1\n"+source.OriginalPackageSha256+"\n"+FinalPackageSha256+"\n"));
        FinalPackageDomain=new(NativeAddressKind.LevelData,"detached-f5-rgb:"+VariantIdentity,NativeAddressRevision.Final,_after.Length);
        FinalRgbOwners=Array.AsReadOnly(fields.Select(f=>new NativeByteExtent(FinalPackageDomain,f.PackageOffset,3)).ToArray());
    }
    public NativeJapaneseEggRgbEditSource Source{get;}
    public NativeJapaneseEggPaletteOrigin Origin{get;}
    public IReadOnlyList<uint> NearRgb{get;}
    public IReadOnlyList<uint> FarRgb{get;}
    public IReadOnlyList<NativeJapaneseEggRgbField> Fields{get;}
    public string OriginalPackageSha256=>Source.OriginalPackageSha256;
    public string FinalPackageSha256{get;}
    public string VariantIdentity{get;}
    public int ChangedByteCount{get;}
    public int ChangedWordCount=>Fields.Count(f=>f.OriginalRgb!=f.FinalRgb);
    public bool NativePalettePreserved=>ChangedByteCount==0;
    public bool SourceAncestryCertified=>Source.SourceAncestryCertified;
    public bool DistinctPerInstanceRootBindingProven=>false;
    public bool NativeImportApproved=>false;
    public bool FinalPixelsVerified=>false;
    public bool PublicationApproved=>false;
    internal NativeAddressSpace FinalPackageDomain{get;}
    internal IReadOnlyList<NativeByteExtent> FinalRgbOwners{get;}
    public byte[] CopyOriginalPackage()=>_original.ToArray();
    public byte[] CopyPackage()=>_after.ToArray();
    public byte[] ApplyExact(ReadOnlySpan<byte> original)
    {if(!original.SequenceEqual(_original))throw new InvalidDataException("RGB apply requires the complete exact original package.");return CopyPackage();}
    public byte[] RevertExact(ReadOnlySpan<byte> after)
    {RequireAfterimage(after);return CopyOriginalPackage();}
    public void RequireAfterimage(ReadOnlySpan<byte> after)
    {if(!after.SequenceEqual(_after))throw new InvalidDataException("RGB inverse/readback requires the complete exact afterimage.");}
    public void RequireDistinctInstanceBinding()=>throw new InvalidDataException("RGB-only detached variants have no independently bound native model roots or per-instance renderer hook.");
}

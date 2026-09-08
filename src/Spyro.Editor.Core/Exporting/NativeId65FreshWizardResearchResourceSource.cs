using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65FreshWizardResourceSmoke")]
namespace Spyro.Editor.Core.Exporting;

/// <summary>Private fixed A/N16 research capture. No body, scene or target-runtime issuer.</summary>
internal sealed class NativeId65FreshWizardResearchResourceSource
{
    private NativeId65FreshWizardResearchResourceSource(NativeId65FreshDocumentCompilation compilation,
        NativeWizardFamilyAssetPlan family, NativeWizardFamilyAssetSource donorSource, NativeId65NormalResourceSource normal,
        NativeFreshWizardResourceProfile profile)
    {
        NativeFreshWizardAuthoringShape.RequireCompilation(compilation, profile);
        Compilation = compilation; Family = family; Profile = profile;
        Fresh = compilation.FreshResourceAncestor;
        var chain = Fresh.ResourceChainAncestor;
        Upload = Fresh.CopyUploadPayload(); Data = chain.CopyDataComponents(); Models = chain.CopyModelPayload();
        Geometry = normal.CopyPlayerPackage().AsSpan(0xAA8,0x32374).ToArray();
        RequireHash(Upload,0xDE000,"b769a9a298285aa402956fdbd5721132736388c6f3f1dc1bfcd7e15a9afeb66d");
        if(profile==NativeFreshWizardResourceProfile.FixedA)
            RequireHash(Data,0x8C8,"821d25d4c012aa180d369f755d82a780dbcf244b4e53c6c4bcf349da533dce65");
        else NativeFreshWizardAuthoringShape.RequireSurfaceComplement(Data,0x8C8,"821d25d4c012aa180d369f755d82a780dbcf244b4e53c6c4bcf349da533dce65");
        RequireHash(Models,0xF800,"88c78343c283256de6ce8d3151b43fa72ba4d96b046d869bd0a7209cbff4c23d");
        if(profile==NativeFreshWizardResourceProfile.FixedA)
        {
            RequireHash(Fresh.CopyScene(),0x3000,"4a6e7b5c6c53199b8a7aa6ae0d24ac9bce32634aa259d5f49535282080641433");
            RequireHash(compilation.DiscPlan.CopyBody(),0x1FB800,"b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c");
        }
        if(Fresh.SceneLayout.NativeNominalCapacity!=16 || chain.SourceImageSha256!=NativeMobySourceInventory.OriginalImageSha256 ||
            !chain.Resources.Surface.CopySourceExecutable().AsSpan().SequenceEqual(normal.CopyExecutable()) ||
            family.SourceVariantId!=donorSource.Row.SourceVariantId || family.SharedGeometrySha256!=Hash(Geometry))
            throw new InvalidDataException("Actual fresh/family source ancestors or fixed capacity changed.");
        chain.RequireExactPairedBytes(Data,Fresh.CopyScene().AsSpan(0x88,0x44));
        _=NativeId65NormalDataLayout.DecodeUntextured(Data);
        var roots=chain.Resources.ModelRootOwners;
        if(roots.Count!=2 || roots[0].ClassId!=0 || roots[0].ModelRelativeOffset!=0 || roots[0].RegistrationAllocationByteLength!=0xCC30 ||
            roots[1].ClassId!=0x78 || roots[1].ModelRelativeOffset!=0xCC30 || roots[1].PayloadByteLength!=0x2930 || roots[1].RegistrationAllocationByteLength!=0x2BD0)
            throw new InvalidDataException("Actual player/companion complete registration owners changed.");
        byte[] block=family.CopySourceAssetBlock(), donor=donorSource.CopyDonor();
        RequireHash(block,0x13274,"af13490b7dbb22fc32ab070e9662ca85faf112c9068816f750ac9b8627a2a147");
        if(family.Owners.Count!=26 || family.SourceSpans.Count>1024 || family.Textures.Count!=5 || family.TextureReferences.Count!=61 ||
            family.SoundRequirements.Count!=5 || family.SoundReferences.Count!=8 || family.AnimationReferences.Count!=37)
            throw new InvalidDataException("Fixed source asset/reference budgets changed.");
        Assets=new(StringComparer.Ordinal); int cursor=0;
        foreach(var owner in family.Owners)
        {
            if(owner.BlockOffset!=cursor || owner.ByteLength<=0 || owner.ByteLength>block.Length-cursor || Assets.ContainsKey(owner.Id))
                throw new InvalidDataException("Source asset partition is not unique/exhaustive/bounded.");
            var bytes=block.AsSpan(cursor,owner.ByteLength);
            if(Hash(bytes)!=owner.Sha256)throw new InvalidDataException("Source asset owner digest changed.");
            int readCursor=0;
            foreach(var read in family.SourceSpans.Where(r=>r.OwnerId==owner.Id))
            {
                long at=read.SourceWadOffset-NativeWizardFamilyAssetSource.DonorWadOffset;
                if(read.OwnerOffset!=readCursor || read.ByteLength<=0 || read.ByteLength>bytes.Length-readCursor ||
                    at<0 || at>donor.Length-(long)read.ByteLength || Hash(donor.AsSpan((int)at,read.ByteLength))!=read.Sha256 ||
                    !bytes.Slice(readCursor,read.ByteLength).SequenceEqual(donor.AsSpan((int)at,read.ByteLength)))
                    throw new InvalidDataException("Asset span lost complete actual original source binding.");
                readCursor+=read.ByteLength;
            }
            if(readCursor!=bytes.Length)throw new InvalidDataException("Source asset has an unowned complement.");
            Assets.Add(owner.Id,bytes.ToArray());cursor+=bytes.Length;
        }
        if(cursor!=block.Length || !Assets["player.animation8"].AsSpan().SequenceEqual(donorSource.Animation.CopyRecord()))
            throw new InvalidDataException("Source asset block/animation is incomplete.");
        var oldStream=NativePlayerAnimationStreamCodec.Decode(Models.AsSpan(0,0xCC30),Geometry);
        if(!oldStream.Records.Select(r=>r.AnimationId).SequenceEqual(new[]{18,19,29,40}) ||
            !Assets["player.animation8"].AsSpan(0xA,0x22).SequenceEqual(oldStream.Records[0].CopyRecord().AsSpan(0xA,0x22)))
            throw new InvalidDataException("Animation8 lacks its exact retained Town texture-header owner.");
        Ownership=new byte[0x80000];cursor=0;
        foreach(var owner in Fresh.UploadOwners)
        {
            if(owner.Offset!=cursor || owner.ByteLength<=0 || owner.ByteLength>Upload.Length-cursor ||
                Hash(Upload.AsSpan(cursor,owner.ByteLength))!=owner.Sha256)
                throw new InvalidDataException("Fresh upload owner partition is not complete and bound.");
            if(cursor<0x80000)
            {
                if(owner.ByteLength>0x80000-cursor)throw new InvalidDataException("VRAM owner crosses audio domain.");
                byte tag=owner.Provenance switch {
                    "fresh-zero-upload-background"=>0,"retained-fixed-Town-presentation-VRAM"=>1,"issued-detached-companion-VRAM"=>2,
                    _=>throw new InvalidDataException("Unknown actual fresh VRAM owner.") };
                if(tag==0 && Upload.AsSpan(cursor,owner.ByteLength).ContainsAnyExcept((byte)0))
                    throw new InvalidDataException("Unowned background must retain its exact generated zero bytes.");
                if(tag==1 && owner.SourceWadOffset!=0x119E800L+cursor)
                    throw new InvalidDataException("Original presentation texture source address changed.");
                Array.Fill(Ownership,tag,cursor,owner.ByteLength);
            }
            cursor+=owner.ByteLength;
        }
        if(cursor!=Upload.Length)throw new InvalidDataException("Fresh upload has an unowned suffix.");
        foreach(var tile in family.Textures)
        {
            RequireRectangle(tile.SourcePixelOffset%1024,tile.SourcePixelOffset/1024,tile.RowBytes,tile.Rows,false);
            RequireRectangle(tile.SourcePaletteOffset%1024,tile.SourcePaletteOffset/1024,32,1,true);
            if(Assets[tile.PixelOwnerId].Length!=tile.RowBytes*tile.Rows || Assets[tile.PaletteOwnerId].Length!=32)
                throw new InvalidDataException("Source texture pair dimensions/owner length changed.");
        }
        TownSound=NativeSoundBankCodec.Decode(Data.AsSpan(0x1D0),Upload.AsSpan(0x80000));
        if(TownSound.Descriptors.Count!=76 || TownSound.Map[0x1F]!=0 || TownSound.Map[0x26]!=22)
            throw new InvalidDataException("Actual local/global sound namespace changed.");
    }

    internal NativeId65FreshDocumentCompilation Compilation{get;}
    internal NativeFreshWizardResourceProfile Profile{get;}
    internal NativeWizardFamilyAssetPlan Family{get;}
    internal NativeId65FreshSceneBundle Fresh{get;}
    internal byte[] Upload{get;}
    internal byte[] Data{get;}
    internal byte[] Models{get;}
    internal byte[] Geometry{get;}
    internal byte[] Ownership{get;}
    internal Dictionary<string,byte[]> Assets{get;}
    internal NativeSoundBank TownSound{get;}
    internal static async Task<NativeId65FreshWizardResearchResourceSource> CaptureAsync(string path,
        NativeId65FreshDocumentCompilation compilation,NativeWizardFamilyAssetPlan family,CancellationToken token,
        NativeFreshWizardResourceProfile profile=NativeFreshWizardResourceProfile.FixedA)
    {
        var donor=await NativeWizardFamilyAssetSource.CaptureAsync(path,token);
        var normal=await NativeId65NormalResourceSource.CaptureAsync(path,token);
        token.ThrowIfCancellationRequested();
        return new(compilation,family,donor,normal,profile);
    }

    internal static void RequireFixedIntent(NativeId65FreshDocumentIntent i)
    {
        ArgumentNullException.ThrowIfNull(i);
        if(i.FloorX!=8448 || i.FloorY!=8448 || i.FloorZ!=8704 || i.FloorWidth!=1024 || i.FloorDepth!=1024 ||
            i.SpawnX!=8960 || i.SpawnY!=8960 || i.SpawnZ!=9216 || i.Heading!=64 || i.MusicTrackId!=19 ||
            i.FloorColor!=new ColorRgba(80,128,112) || i.SkyColor!=new ColorRgba(128,96,64))
            throw new InvalidDataException("Wizard research resources require the exact A/N16/music19 actual compilation.");
    }

    // Neutral shared checker, never an issuer. Production supplies only the
    // original constants; the friend fixture supplies small hostile streams.
    internal static async Task RequireSnapshotIdentityAsync(Stream image,long length,string expected,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(!image.CanRead || !image.CanSeek || length<=0 || length>NativeMobySourceInventory.OriginalImageByteLength ||
            image.Length!=length || image.Position!=0)
            throw new InvalidDataException("Original snapshot length/initial position/readability changed before read.");
        using var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer=new byte[65536];long read=0;
        while(read<length)
        {
            token.ThrowIfCancellationRequested();
            int requested=(int)Math.Min(buffer.Length,length-read);
            int n=await image.ReadAsync(buffer.AsMemory(0,requested),token);
            token.ThrowIfCancellationRequested();
            if(n<=0 || n>requested)throw new InvalidDataException("Original snapshot was short or escaped its requested bounded read.");
            digest.AppendData(buffer.AsSpan(0,n));read+=n;
        }
        if(image.Position!=length)throw new InvalidDataException("Original snapshot final position changed.");
        int eof=image.ReadByte();token.ThrowIfCancellationRequested();
        if(eof!=-1 || image.Position!=length || image.Length!=length || Convert.ToHexStringLower(digest.GetHashAndReset())!=expected)
            throw new InvalidDataException("Original snapshot SHA/actual EOF/final position/length changed.");
        token.ThrowIfCancellationRequested();
    }

    internal static void RequireBackground(ReadOnlySpan<byte> ownership,int x,int y,int width,int rows,bool palette)
    {
        if(ownership.Length!=0x80000)throw new InvalidDataException("Incomplete VRAM ownership mask.");
        RequireRectangle(x,y,width,rows,palette);
        for(int r=0;r<rows;r++) if(ownership.Slice((y+r)*1024+x,width).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Texture reservation overlaps protected/background allocation ownership.");
    }
    internal static (int X,int Y) FindBackground(ReadOnlySpan<byte> ownership,int width,int rows,bool palette)
    {
        RequireRectangle(0,0,width,rows,palette);
        if(ownership.Length!=0x80000)throw new InvalidDataException("Incomplete VRAM ownership mask.");
        for(int y=0;y<=512-rows;y++)
        {
            if(!palette && y%256+rows>256)continue;
            for(int x=0;x<=1024-width;x+=palette?32:16)
            {
                if(!palette && x%128+width>128)continue;
                bool free=true;
                for(int row=0;row<rows && free;row++)free=!ownership.Slice((y+row)*1024+x,width).ContainsAnyExcept((byte)0);
                if(free)return(x,y);
            }
        }
        throw new InvalidDataException("Bounded fresh VRAM background is exhausted.");
    }
    internal static void RequireRectangle(int x,int y,int width,int rows,bool palette)
    {
        if(width<=0 || rows<=0 || x<0 || y<0 || width>1024 || rows>512 || x>1024-width || y>512-rows)
            throw new InvalidDataException("Complete texture rectangle leaves bounded VRAM.");
        if(palette ? width!=32 || rows!=1 || x%32!=0 : width>128 || x%128+width>128 || y%256+rows>256)
            throw new InvalidDataException("Texture rectangle violates native CLUT/page encoding.");
    }
    internal static void RequireInstalledGrammar(ReadOnlySpan<byte> data,ReadOnlySpan<byte> models)
    {
        if(data.Length!=0x8FC || models.Length!=0x17800 || Word(data,0)!=8 || Word(data,4)!=0)
            throw new InvalidDataException("Exact research resource lengths/metadata required; old profile is not widened.");
        _=NativeId65AuthoredSurfaceLayout.DecodeAndValidate(data.Slice(8,0x144));
        if(Word(data,0x14C)!=0)throw new InvalidDataException("The preserved portal count must remain zero.");
        var particles=NativeParticleDescriptorCodec.Decode(data.Slice(0x150,0x8C));
        if(!particles.Ids.SequenceEqual(new[]{0x15,0x18,2,0x21,0xC,0xB,0xA,9,1,0,7}) || Word(data,0x1DC)!=0x720 || Word(data,0x2E0)!=78)
            throw new InvalidDataException("Exact appended particle/sound grammar changed.");
        foreach(byte index in data.Slice(0x1E0,256)) if(index>=78)throw new InvalidDataException("Global map escapes descriptor namespace.");
        for(int at=0x2E4;at<data.Length;at+=20) if(Word(data,at)%16!=0)throw new InvalidDataException("Sample offset alignment changed.");
        int cursor=0;List<uint> ids=[];
        while(cursor<0x10A48)
        {
            if(cursor>0x10A48-8)throw new InvalidDataException("Truncated root0 record.");
            uint id=Word(models,cursor),length=Word(models,cursor+4);
            if(id>=46 || ids.Contains(id) || length<0x28 || length%4!=0 || length>0x10A48-cursor-8L)
                throw new InvalidDataException("Root0 record exceeds its complete source-owned stream.");
            ids.Add(id);cursor+=8+(int)length;
        }
        if(cursor!=0x10A48 || Word(models,cursor)!=uint.MaxValue || !ids.SequenceEqual(new uint[]{18,19,29,40,8}) ||
            models.Slice(0x17134).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Root0 terminator/order or generated model padding changed.");
        _=NativeMobyModelTextureLayout.Decode(models.Slice(0x1361C,0x564));
        _=NativeMobyModelTextureLayout.Decode(models.Slice(0x13B80,0x35B4));
    }
    internal static uint Word(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
    internal static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void RequireHash(ReadOnlySpan<byte> b,int length,string hash)
    {if(b.Length!=length || Hash(b)!=hash)throw new InvalidDataException("Complete fixed source/resource ancestor changed.");}
}

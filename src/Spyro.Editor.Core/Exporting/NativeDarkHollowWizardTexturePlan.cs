using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public sealed class NativeMobyTextureBytePatch
{
    private readonly byte[] _before,_after;
    internal NativeMobyTextureBytePatch(string owner,int offset,ReadOnlySpan<byte> before,ReadOnlySpan<byte> after)
    {Owner=owner;Offset=offset;_before=before.ToArray();_after=after.ToArray();}
    public string Owner { get; }
    public int Offset { get; }
    public int ByteLength=>_before.Length;
    public byte[] CopyBefore()=>_before.ToArray();
    public byte[] CopyAfter()=>_after.ToArray();
}

public sealed record NativeMobyTextureAllocationEvidence(string Id,int SourcePixelOffset,int TargetPixelOffset,
    int RowBytes,int Rows,int SourcePaletteOffset,int TargetPaletteOffset,string PixelsSha256,string PaletteSha256);

/// <summary>
/// Exact-source Dark Hollow Wizard/lightning texture resources only. Preserves all
/// target level bytes except ownership-proven new VRAM pixels/CLUTs; emits detached
/// model packages and a particle descriptor for later dependency-graph composition.
/// Does not install actors, native behavior, local sounds or grant publication.
/// </summary>
public sealed class NativeDarkHollowWizardTexturePlan
{
    private readonly byte[] _wizard,_lightning,_particle,_target;
    private NativeDarkHollowWizardTexturePlan(byte[] dark,byte[] donor,NativeTexturePageOwnershipReport ownership,string playerTextureHeaderHash)
    {
        const int wizardOffset=0x19B518,lightningOffset=0x1CAC88,particleOffset=0x173518;
        _wizard=donor.AsSpan(wizardOffset,0x35AC).ToArray();_lightning=donor.AsSpan(lightningOffset,0x564).ToArray();
        _particle=donor.AsSpan(particleOffset,12).ToArray();_target=dark.ToArray();
        Require(_wizard,"24df9ae279f32ba10a8e3bd25880d6989991b9225acf45755e6aaf985d66f529");
        Require(_lightning,"9ea5c72ba675e9c6a81433f7408695399e19bc188b4b2a923069d71de601789a");
        Require(_particle,"4baed93eb98ac71d27f59e3ddf8b5382dda7e74319de188749dce3831397f7b3");
        if(ownership.WadEntry!=14||ownership.AssetWadOffset!=0xF01000||ownership.TexturePagesWadOffset!=0xF01800||
            ownership.AddressableTexturePageByteLength!=0x80000||!ownership.AllConsumerClosureComplete||
            !ownership.ProvablyPrivatePixelAndClutSpace||!string.Equals(ownership.AddressableTexturePagesSha256,Hash(dark.AsSpan(0x800,0x80000)),StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Dark native all-consumer VRAM ownership is incomplete or stale.");
        List<NativeMobyTextureBytePatch> patches=[];List<NativeMobyTextureAllocationEvidence> allocations=[];
        var wizardLayout=NativeMobyModelTextureLayout.Decode(_wizard);var boltLayout=NativeMobyModelTextureLayout.Decode(_lightning);
        if(wizardLayout.References.Count!=51||boltLayout.References.Count!=9||
            !wizardLayout.FaceTableOffsets.SequenceEqual(new[]{0x238,0x700})||!boltLayout.FaceTableOffsets.SequenceEqual(new[]{0x1A8,0x260}))
            throw new InvalidDataException("Native Wizard/lightning renderer texture schemas changed.");
        if(BinaryPrimitives.ReadUInt16LittleEndian(_particle)!=7||BinaryPrimitives.ReadUInt16LittleEndian(_particle.AsSpan(2))!=8)
            throw new InvalidDataException("Native particle-7 descriptor schema changed.");
        int minU=Math.Min(_particle[4],_particle[8]),maxU=Math.Max(_particle[4],_particle[8]);
        int minV=Math.Min(_particle[5],_particle[9]),maxV=Math.Max(_particle[5],_particle[9]);
        ushort palette=BinaryPrimitives.ReadUInt16LittleEndian(_particle.AsSpan(6)),page=BinaryPrimitives.ReadUInt16LittleEndian(_particle.AsSpan(10));
        var wizardTiles=DescribeModel("wizard",wizardLayout);var boltTiles=DescribeModel("lightning",boltLayout);
        var particleRequest=new NativeMobyTextureTileRequest("particle7",palette,page,minU,minV,maxU,maxV);
        var requests=wizardTiles.Select(t=>t.Request).Concat(boltTiles.Select(t=>t.Request)).Append(particleRequest).ToArray();
        var materialization=NativeMobyTextureMaterialization.Compile(donor.AsSpan(0x800,0x80000),
            dark.AsSpan(0x800,0x80000),ownership.ProtectedRanges,requests);
        int nextTile=0,nextPatch=0;
        RebaseModel("wizard",_wizard,wizardTiles);RebaseModel("lightning",_lightning,boltTiles);
        var tile=Install(particleRequest);
        byte[] beforeParticle=_particle.ToArray();
        foreach(int field in new[]{4,8}){_particle[field]=checked((byte)(_particle[field]+tile.U-minU));_particle[field+1]=checked((byte)(_particle[field+1]+tile.V-minV));}
        BinaryPrimitives.WriteUInt16LittleEndian(_particle.AsSpan(6),tile.Palette);
        BinaryPrimitives.WriteUInt16LittleEndian(_particle.AsSpan(10),(ushort)((page&~0x1F)|tile.Page));
        patches.Add(new("particle7",0,beforeParticle,_particle));
        if(allocations.Count!=5||allocations.Sum(a=>a.RowBytes*a.Rows)!=2056||allocations.Count*32!=160||
            nextTile!=requests.Length||nextPatch!=materialization.Patches.Count)
            throw new InvalidDataException("Native five-tile Wizard texture dependency accounting changed.");
        // Every target-native byte, including all 128 rows/properties/model data,
        // survives unless it belongs to an explicit new texture install.
        bool[] changed=new bool[dark.Length];
        foreach(var p in patches.Where(p=>p.Owner=="target-vram"))
        {
            int offset=checked(0x800+p.Offset);
            if(!dark.AsSpan(offset,p.ByteLength).SequenceEqual(p.CopyBefore())||
                ownership.ProtectedRanges.Any(r=>r.Offset<p.Offset+(long)p.ByteLength&&p.Offset<r.Offset+r.Length))
                throw new InvalidDataException("Native texture patch conflicts with a protected consumer/source preimage.");
            for(int i=0;i<p.ByteLength;i++)if(changed[offset+i])throw new InvalidDataException("Native texture allocations alias.");else changed[offset+i]=true;
            p.CopyAfter().CopyTo(_target,offset);
            if(!_target.AsSpan(offset,p.ByteLength).SequenceEqual(p.CopyAfter()))throw new InvalidDataException("Native texture install readback failed.");
        }
        for(int i=0;i<dark.Length;i++)if(!changed[i]&&dark[i]!=_target[i])throw new InvalidDataException("Native target complement changed during texture relocation.");
        if(!_target.AsSpan(0x800,0x80000).SequenceEqual(materialization.CopyUpload()))
            throw new InvalidDataException("Native Wizard texture transaction differs from the shared complete upload.");
        VerifyModel("wizard",donor.AsSpan(wizardOffset,0x35AC),_wizard);
        VerifyModel("lightning",donor.AsSpan(lightningOffset,0x564),_lightning);
        SourceDataSha256=Hash(dark);DonorDataSha256=Hash(donor);FinalTextureDataSha256=Hash(_target);Player8ResidentTextureHeaderSha256=playerTextureHeaderHash;
        Patches=Array.AsReadOnly(patches.ToArray());Allocations=Array.AsReadOnly(allocations.ToArray());
        WizardTextureReferences=wizardLayout.References.Count;LightningTextureReferences=boltLayout.References.Count;

        static (NativeMobyTextureTileRequest Request,NativeMobyModelTextureReference[] References)[] DescribeModel(
            string id,NativeMobyModelTextureLayout model)
        {
            return model.References.GroupBy(r=>(r.Palette,r.Page)).OrderBy(g=>g.Key.Palette).ThenBy(g=>g.Key.Page).Select(group=>
            {
                var bytes=group.Select(r=>r.CopyBytes()).ToArray();
                int u0=bytes.Min(b=>new[]{b[0],b[4],b[8],b[10]}.Min()),u1=bytes.Max(b=>new[]{b[0],b[4],b[8],b[10]}.Max());
                int v0=bytes.Min(b=>new[]{b[1],b[5],b[9],b[11]}.Min()),v1=bytes.Max(b=>new[]{b[1],b[5],b[9],b[11]}.Max());
                return (new NativeMobyTextureTileRequest($"{id}:{group.Key.Palette:X4}:{group.Key.Page:X4}",
                    group.Key.Palette,group.Key.Page,u0,v0,u1,v1),group.ToArray());
            }).ToArray();
        }
        void RebaseModel(string id,byte[] package,
            (NativeMobyTextureTileRequest Request,NativeMobyModelTextureReference[] References)[] tiles)
        {
            foreach(var entry in tiles)
            {
                var target=Install(entry.Request);
                foreach(var reference in entry.References)
                {
                    byte[] original=reference.CopyBytes();
                    byte[] after=NativeMobyModelTextureLayout.Rebase(reference,target.U-entry.Request.MinU,target.V-entry.Request.MinV,
                        target.Palette,(ushort)((reference.Page&~0x1F)|target.Page));
                    after.CopyTo(package,reference.PackageOffset);patches.Add(new(id,reference.PackageOffset,original,after));
                }
            }
        }
        NativeMobyIndexedTextureLocation Install(NativeMobyTextureTileRequest request)
        {
            if(nextTile>=requests.Length||requests[nextTile]!=request)
                throw new InvalidDataException("Native Wizard texture request order changed.");
            var allocation=materialization.Allocations[nextTile];
            // Preserve the existing public patch order: tile rows/CLUT followed
            // by that tile's model consumers, then the next dependency.
            patches.AddRange(materialization.Patches.Skip(nextPatch).Take(allocation.Rows+1));
            nextPatch+=allocation.Rows+1;allocations.Add(allocation);
            return materialization.Locations[nextTile++];
        }
        void VerifyModel(string id,ReadOnlySpan<byte> before,byte[] after)
        {
            byte[] inverse=after.ToArray();
            foreach(var p in patches.Where(p=>p.Owner==id))
            {if(!before.Slice(p.Offset,p.ByteLength).SequenceEqual(p.CopyBefore())||!after.AsSpan(p.Offset,p.ByteLength).SequenceEqual(p.CopyAfter()))throw new InvalidDataException("Native model texture reference preimage/readback changed.");p.CopyBefore().CopyTo(inverse,p.Offset);}
            if(!inverse.AsSpan().SequenceEqual(before))throw new InvalidDataException("Model relocation modified an untyped byte.");
            _=NativeMobyModelTextureLayout.Decode(after);
        }
    }
    public string SourceImageSha256=>NativeMobySourceInventory.OriginalImageSha256;
    public string SourceDataSha256 { get; }
    public string DonorDataSha256 { get; }
    public string FinalTextureDataSha256 { get; }
    public string Player8ResidentTextureHeaderSha256 { get; }
    public int WizardTextureReferences { get; }
    public int LightningTextureReferences { get; }
    public IReadOnlyList<NativeMobyTextureBytePatch> Patches { get; }
    public IReadOnlyList<NativeMobyTextureAllocationEvidence> Allocations { get; }
    public bool CompleteNativeClosureProven=>false;
    public bool RuntimeFitProven=>false;
    public bool PublicationApproved=>false;
    public byte[] CopyWizardPackage()=>_wizard.ToArray();
    public byte[] CopyLightningPackage()=>_lightning.ToArray();
    public byte[] CopyParticle7Descriptor()=>_particle.ToArray();
    public byte[] CopyTexturePatchedTargetLevel()=>_target.ToArray();

    public static NativeDarkHollowWizardTexturePlan Capture(string originalImagePath)
    {
        using var image=new FileStream(originalImagePath,FileMode.Open,FileAccess.Read,FileShare.Read);
        RequireOriginal(image);var layout=DiscImage.DetectLayout(originalImagePath);
        if(layout.SectorSize!=2352||layout.UserOffset!=24)throw new InvalidDataException("Native texture capture requires original MODE2/2352 source geometry.");
        byte[] header=DiscImage.ReadFileBytes(image,layout,37,0,0x800);
        if(Word(header,14*8)!=0xF01000||Word(header,14*8+4)!=0x28D800||Word(header,40*8)!=0x2F6B000||Word(header,40*8+4)!=0x28A000)
            throw new InvalidDataException("Native Dark/Wizard data owners changed.");
        byte[] dark=DiscImage.ReadFileBytes(image,layout,37,0xF01000,0x28D800),donor=DiscImage.ReadFileBytes(image,layout,37,0x2F6B000,0x28A000);
        Require(dark,"a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97");
        Require(donor,"9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8");
        // Incoming electric-hurt animation 8 must not introduce an unenumerated
        // shared-player texture consumer after the target ownership scan. Its
        // complete header excluding only frame count matches resident Dark 18.
        var player=NativePlayerAnimationSource.CaptureAsync(originalImagePath).GetAwaiter().GetResult();
        var incoming=player.Levels.Single(l=>l.LevelId==33);var resident=player.Levels.Single(l=>l.LevelId==12);
        if(incoming.DataSha256!=Hash(donor)||resident.DataSha256!=Hash(dark))throw new InvalidDataException("Incoming/resident player texture source identity changed.");
        byte[] incomingRecord=incoming.Stream.Records.Single(r=>r.AnimationId==8).CopyRecord();
        byte[] residentRecord=resident.Stream.Records.Single(r=>r.AnimationId==18).CopyRecord();
        if(!incomingRecord.AsSpan(10,0x22).SequenceEqual(residentRecord.AsSpan(10,0x22)))
            throw new InvalidDataException("Incoming player animation introduces unclosed shared texture/geometry header consumers.");
        string playerHeaderHash=Hash(incomingRecord.AsSpan(10,0x22));
        var ownership=NativeTexturePageOwnershipScanner.Scan(originalImagePath,new LevelDefinition{Key="darkhollow",DisplayName="Dark Hollow",LevelId=12,SourceWadEntry=14});
        var result=new NativeDarkHollowWizardTexturePlan(dark,donor,ownership,playerHeaderHash);RequireOriginal(image);return result;
    }
    private static void RequireOriginal(FileStream image)
    {
        image.Position=0;
        if(image.Length!=NativeMobySourceInventory.OriginalImageByteLength||
            Convert.ToHexStringLower(SHA256.HashData(image))!=NativeMobySourceInventory.OriginalImageSha256||
            image.Length!=NativeMobySourceInventory.OriginalImageByteLength||image.Position!=NativeMobySourceInventory.OriginalImageByteLength)
            throw new InvalidDataException("Native Wizard texture producer requires the exact original image with stable length and complete readback.");
    }
    private static void Require(ReadOnlySpan<byte>b,string expected)
    {if(Hash(b)!=expected)throw new InvalidDataException("Native Wizard texture source certificate changed.");}
    private static string Hash(ReadOnlySpan<byte>b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static uint Word(ReadOnlySpan<byte>b,int o)=>BinaryPrimitives.ReadUInt32LittleEndian(b[o..]);
}

using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

public enum NativeFreshWizardResourceDomain { Upload, Data, Models }
public sealed record NativeFreshWizardResourceOwner(string Id,NativeFreshWizardResourceDomain Domain,int Offset,int ByteLength,
    string Sha256,string Provenance,string? SourceOwnerId,int? SourceOffset);
public sealed record NativeFreshWizardModelRoot(ushort ClassId,int Offset,int ByteLength,int RegistrationExtentByteLength);
public sealed record NativeFreshWizardTexturePlacement(string SourceOwnerId,int X,int Y,int RowBytes,int Rows);
public sealed record NativeFreshWizardSoundBinding(NativeWizardFamilySoundNamespace Namespace,int Selector,
    int SourceDescriptorIndex,int TargetDescriptorIndex,int? PreviousTargetDescriptorIndex);

public sealed class NativeFreshWizardResourcePatch
{
    private readonly byte[] _before,_after;
    internal NativeFreshWizardResourcePatch(string sourceId,NativeByteExtent original,NativeFreshWizardResourceDomain domain,
        NativeByteExtent final,ReadOnlySpan<byte> before,ReadOnlySpan<byte> after)
    {
        if(original.ByteLength!=final.ByteLength || before.Length!=original.ByteLength || after.Length!=before.Length)
            throw new InvalidDataException("Resource patch lost complete original/final field ownership.");
        SourceOwnerId=sourceId;Original=original;Final=final;Domain=domain;_before=before.ToArray();_after=after.ToArray();
    }
    internal NativeByteExtent Original{get;}
    internal NativeByteExtent Final{get;}
    public string SourceOwnerId{get;}
    public int SourceOffset=>(int)Original.Offset;
    public NativeFreshWizardResourceDomain Domain{get;}
    public int Offset=>(int)Final.Offset;
    public int ByteLength=>_after.Length;
    public byte[] CopyBefore()=>_before.ToArray();
    public byte[] CopyAfter()=>_after.ToArray();
}

/// <summary>
/// Exact A/N16 + Wizard33/T6 detached research resources. The global1F map
/// replacement is unresolved across Town consumers. No existing body/staging
/// grammar accepts this type; no scene, runtime, save or publication is issued.
/// </summary>
public sealed class NativeId65FreshWizardResearchResourcePlan
{
    private readonly NativeId65FreshWizardResearchResourceSource _source;
    private readonly byte[] _upload,_data,_models;
    private NativeId65FreshWizardResearchResourcePlan(NativeId65FreshWizardResearchResourceSource source)
    {
        _source=source;
        var family=source.Family;
        var inputs=new Dictionary<string,byte[]>(source.Assets,StringComparer.Ordinal) {
            ["fresh.upload"]=source.Upload,["fresh.data"]=source.Data,["fresh.models"]=source.Models
        };
        var originals=inputs.ToDictionary(p=>p.Key,p=>new NativeAddressSpace(NativeAddressKind.LevelData,
            "fresh-wizard-original:"+p.Key+":"+Hash(p.Value),NativeAddressRevision.Original,p.Value.Length),StringComparer.Ordinal);
        int[] lengths=[0xE5800,0x8FC,0x17800];
        var finals=lengths.Select((n,i)=>new NativeAddressSpace(NativeAddressKind.LevelData,
            "fresh-wizard-research:"+(NativeFreshWizardResourceDomain)i,NativeAddressRevision.Final,n)).ToArray();
        var tags=lengths.Select(n=>Enumerable.Repeat(-1,n).ToArray()).ToArray();
        var regions=new List<(NativeFreshWizardResourceDomain Domain,int Offset,int Length,string Provenance,string? Source,int SourceOffset)>();
        var patches=new List<NativeFreshWizardResourcePatch>();
        var placements=new List<NativeFreshWizardTexturePlacement>();
        var sounds=new List<NativeFreshWizardSoundBinding>();
        byte[] occupied=source.Ownership.ToArray(),vram=source.Upload.AsSpan(0,0x80000).ToArray();
        var allocation=new Dictionary<(string Id,bool Palette),(int X,int Y)>();
        foreach(var old in source.Fresh.UploadOwners)
            Region(NativeFreshWizardResourceDomain.Upload,old.Offset,old.ByteLength,old.Provenance,"fresh.upload",old.Offset);
        foreach(bool palette in new[]{true,false})
        foreach(var tile in family.Textures.OrderBy(t=>t.Id,StringComparer.Ordinal))
        {
            int width=palette?32:tile.RowBytes,rows=palette?1:tile.Rows;
            var point=NativeId65FreshWizardResearchResourceSource.FindBackground(occupied,width,rows,palette);
            NativeId65FreshWizardResearchResourceSource.RequireBackground(occupied,point.X,point.Y,width,rows,palette);
            string id=palette?tile.PaletteOwnerId:tile.PixelOwnerId;
            byte[] bytes=inputs[id];
            for(int row=0;row<rows;row++)
            {
                int at=(point.Y+row)*1024+point.X;
                bytes.AsSpan(row*width,width).CopyTo(vram.AsSpan(at,width));
                Array.Fill(occupied,(byte)3,at,width);
                Region(NativeFreshWizardResourceDomain.Upload,at,width,"source-texture-insertion",id,row*width,true);
            }
            placements.Add(new(id,point.X,point.Y,width,rows));allocation.Add((tile.Id,palette),point);
        }
        var rewritten=new Dictionary<string,byte[]> {
            ["wizard.model"]=inputs["wizard.model"].ToArray(),["bolt.model"]=inputs["bolt.model"].ToArray(),["particle7"]=inputs["particle7"].ToArray()
        };
        foreach(var field in family.TextureReferences)
        {
            byte[] before=field.CopySourceBytes();var tile=family.Textures.Single(t=>t.Id==field.TextureId);
            if(!inputs[field.OwnerId].AsSpan(field.FieldOffset,field.ByteLength).SequenceEqual(before))
                throw new InvalidDataException("Source texture field preimage changed.");
            var pixel=allocation[(tile.Id,false)];var palette=allocation[(tile.Id,true)];
            ushort page=Half(before,6);
            int sourceU=2*(tile.SourcePixelOffset%1024-((page&15)*128-1024));
            int sourceV=tile.SourcePixelOffset/1024-((page&16)!=0?256:0);
            ushort clut=(ushort)(palette.Y*64+(palette.X+1024)/32);
            ushort targetPage=(ushort)((page&~31)|(8+pixel.X/128)|(pixel.Y>=256?16:0));
            byte[] expanded=new byte[12];before.CopyTo(expanded,0);
            if(before.Length==8){expanded[8]=before[0];expanded[9]=before[5];expanded[10]=before[4];expanded[11]=before[1];}
            byte[] after=NativeMobyModelTextureLayout.Rebase(new(field.FieldOffset,expanded),
                2*(pixel.X%128)-sourceU,pixel.Y%256-sourceV,clut,targetPage).AsSpan(0,before.Length).ToArray();
            after.CopyTo(rewritten[field.OwnerId],field.FieldOffset);
            int[] coordinates=before.Length==8?[0,4]:[0,4,8,10];
            foreach(int c in coordinates)
            {
                int oldAt=TextureByte(page,before[c],before[c+1]),newAt=TextureByte(targetPage,after[c],after[c+1]);
                int sourceRow=oldAt/1024-tile.SourcePixelOffset/1024,sourceCol=oldAt%1024-tile.SourcePixelOffset%1024;
                if(sourceRow<0 || sourceRow>=tile.Rows || sourceCol<0 || sourceCol>=tile.RowBytes ||
                    vram[newAt]!=inputs[tile.PixelOwnerId][sourceRow*tile.RowBytes+sourceCol] || before[c]%2!=after[c]%2)
                    throw new InvalidDataException("Final texture consumer lost its exact indexed source byte/nibble.");
            }
            int paletteAt=(clut>>6)*1024+(clut&63)*32-1024;
            if(!vram.AsSpan(paletteAt,32).SequenceEqual(inputs[tile.PaletteOwnerId]))
                throw new InvalidDataException("Final texture descriptor does not resolve its full CLUT owner.");
            var (domain,basis)=field.OwnerId switch {
                "wizard.model"=>(NativeFreshWizardResourceDomain.Models,0x13B80),
                "bolt.model"=>(NativeFreshWizardResourceDomain.Models,0x1361C),
                "particle7"=>(NativeFreshWizardResourceDomain.Data,0x1D0),
                _=>throw new InvalidDataException("Unknown source texture package.") };
            Patch(field.OwnerId,field.FieldOffset,domain,basis+field.FieldOffset,before,after);
        }

        byte[] bank=new byte[0x65800],sound=new byte[0x720];
        source.Upload.AsSpan(0x80000).CopyTo(bank);source.Data.AsSpan(0x1D0).CopyTo(sound);
        int bankCursor=0x5E000;
        foreach(var requirement in family.SoundRequirements)
        {
            byte[] descriptor=inputs[requirement.DescriptorOwnerId],sample=inputs[requirement.SampleOwnerId];
            var equivalents=source.TownSound.Descriptors.Where(d=>descriptor.AsSpan(4).SequenceEqual(d.CopyDescriptor().AsSpan(4)) &&
                sample.AsSpan().SequenceEqual(d.Sample.CopyBytes())).Select(d=>d.Index).ToArray();
            int[] expected=requirement.ExactTownDescriptorIndex is int e?[e]:[];
            if(!equivalents.SequenceEqual(expected))throw new InvalidDataException("Complete descriptor/sample equivalence changed.");
            int target=requirement.Id switch {"global1F"=>76,"local0"=>77,"local1"=>66,"local2"=>67,"global26"=>22,
                _=>throw new InvalidDataException("Unknown source sound requirement.")};
            sounds.Add(new(requirement.Namespace,requirement.Selector,requirement.SourceDescriptorIndex,target,
                requirement.Id=="global1F"?0:requirement.Id=="global26"?22:null));
        }
        foreach(var entry in new[]{(Id:"global1F",Target:76),(Id:"local0",Target:77)})
        {
            byte[] descriptor=inputs[entry.Id+".descriptor"].ToArray(),sample=inputs[entry.Id+".sample"];
            if(Word(descriptor,4)!=0 || Word(descriptor,12)!=0 || Word(descriptor,16)!=0 || bankCursor%16!=0 || sample.Length>0x65170-bankCursor)
                throw new InvalidDataException("Appended sound controls/complete sample budget changed.");
            int at=0x1DC+0x108+entry.Target*20;
            Put(descriptor,0,(uint)bankCursor);descriptor.CopyTo(sound,0x108+entry.Target*20);sample.CopyTo(bank,bankCursor);
            Patch(entry.Id+".descriptor",0,NativeFreshWizardResourceDomain.Data,at,inputs[entry.Id+".descriptor"].AsSpan(0,4),descriptor.AsSpan(0,4));
            Region(NativeFreshWizardResourceDomain.Upload,0x80000+bankCursor,sample.Length,"source-sample-insertion",entry.Id+".sample",0);
            bankCursor+=sample.Length;
        }
        if(bankCursor!=0x65170)throw new InvalidDataException("Exact source audio used end changed.");
        Region(NativeFreshWizardResourceDomain.Upload,0xE5170,0x690,"generated-bank-padding",null,0);
        Put(sound,0,0x720);Put(sound,0x104,78);sound[4+0x1F]=76;
        byte[] wizard=rewritten["wizard.model"];
        for(int i=0;i<3;i++)
        {
            byte next=new byte[]{77,66,67}[i];
            Patch("wizard.model",4+i,NativeFreshWizardResourceDomain.Models,0x13B84+i,wizard.AsSpan(4+i,1),new[]{next});wizard[4+i]=next;
        }
        var particleAppend=NativeParticleDescriptorAppend.Build(NativeParticleDescriptorCodec.Decode(source.Data.AsSpan(0x150,0x80)),rewritten["particle7"]);
        byte[] particle=particleAppend.CopyComponent();
        _upload=new byte[0xE5800];vram.CopyTo(_upload,0);bank.CopyTo(_upload,0x80000);
        _data=new byte[0x8FC];source.Data.AsSpan(0,0x150).CopyTo(_data);particle.CopyTo(_data,0x150);sound.CopyTo(_data,0x1DC);
        _models=new byte[0x17800];source.Models.AsSpan(0,0xCC2C).CopyTo(_models);
        inputs["player.animation8"].CopyTo(_models,0xCC2C);source.Models.AsSpan(0xCC2C,4).CopyTo(_models.AsSpan(0x10A48));
        source.Models.AsSpan(0xCC30).CopyTo(_models.AsSpan(0x10A4C));rewritten["bolt.model"].CopyTo(_models,0x1361C);wizard.CopyTo(_models,0x13B80);
        Region(NativeFreshWizardResourceDomain.Data,0,0x1D0,"retained-data-prefix-and-particles","fresh.data",0);
        Region(NativeFreshWizardResourceDomain.Data,0x1D0,12,"source-particle-insertion","particle7",0);
        Region(NativeFreshWizardResourceDomain.Data,0x1DC,0x6F8,"retained-sound-component","fresh.data",0x1D0);
        Region(NativeFreshWizardResourceDomain.Data,0x8D4,20,"source-descriptor-insertion","global1F.descriptor",0);
        Region(NativeFreshWizardResourceDomain.Data,0x8E8,20,"source-descriptor-insertion","local0.descriptor",0);
        Region(NativeFreshWizardResourceDomain.Models,0,0xCC2C,"retained-root0-records","fresh.models",0);
        Region(NativeFreshWizardResourceDomain.Models,0xCC2C,0x3E1C,"source-animation8-insertion","player.animation8",0);
        Region(NativeFreshWizardResourceDomain.Models,0x10A48,4,"retained-root0-terminator","fresh.models",0xCC2C);
        Region(NativeFreshWizardResourceDomain.Models,0x10A4C,0x2BD0,"retained-complete-companion-allocation","fresh.models",0xCC30);
        Region(NativeFreshWizardResourceDomain.Models,0x1361C,0x564,"source-complete-bolt-allocation","bolt.model",0);
        Region(NativeFreshWizardResourceDomain.Models,0x13B80,0x35B4,"source-complete-wizard-allocation","wizard.model",0);
        Region(NativeFreshWizardResourceDomain.Models,0x17134,0x6CC,"generated-model-padding",null,0);
        foreach(var field in new[]{(Old:0x150,New:0x150),(Old:0x154,New:0x154),(Old:0x1D0,New:0x1DC),(Old:0x2D4,New:0x2E0)})
            Patch("fresh.data",field.Old,NativeFreshWizardResourceDomain.Data,field.New,source.Data.AsSpan(field.Old,4),_data.AsSpan(field.New,4));
        Patch("fresh.data",0x1F3,NativeFreshWizardResourceDomain.Data,0x1FF,source.Data.AsSpan(0x1F3,1),_data.AsSpan(0x1FF,1));
        byte[][] outputs=[_upload,_data,_models];
        foreach(var patch in patches)
        {
            if(patch.Original.Space!=originals[patch.SourceOwnerId] || patch.Final.Space!=finals[(int)patch.Domain] ||
                !inputs[patch.SourceOwnerId].AsSpan(patch.SourceOffset,patch.ByteLength).SequenceEqual(patch.CopyBefore()) ||
                !outputs[(int)patch.Domain].AsSpan(patch.Offset,patch.ByteLength).SequenceEqual(patch.CopyAfter()))
                throw new InvalidDataException("Patch preimage/afterimage or canonical domain binding failed.");
            Region(patch.Domain,patch.Offset,patch.ByteLength,"declared-source-field-reencoding",patch.SourceOwnerId,patch.SourceOffset,true);
        }
        var owners=new List<NativeFreshWizardResourceOwner>();
        for(int d=0;d<outputs.Length;d++)
        {
            byte[] output=outputs[d];int at=0;
            while(at<output.Length)
            {
                int tag=tags[d][at];if(tag<0)throw new InvalidDataException("Final resource has an unowned byte.");
                int end=at+1;while(end<output.Length && tags[d][end]==tag)end++;
                var region=regions[tag];int sourceAt=region.SourceOffset+at-region.Offset;
                ReadOnlySpan<byte> bytes=output.AsSpan(at,end-at);
                if(region.Source is null)
                {if(bytes.ContainsAnyExcept((byte)0))throw new InvalidDataException("Generated padding is not zero.");}
                else if(region.Provenance!="declared-source-field-reencoding" && !bytes.SequenceEqual(inputs[region.Source].AsSpan(sourceAt,end-at)))
                    throw new InvalidDataException("Resource copy changed an undeclared complete source complement.");
                owners.Add(new($"{(NativeFreshWizardResourceDomain)d}:{at:X}",(NativeFreshWizardResourceDomain)d,at,end-at,Hash(bytes),region.Provenance,region.Source,region.Source is null?null:sourceAt));
                at=end;
            }
        }
        NativeId65FreshWizardResearchResourceSource.RequireInstalledGrammar(_data,_models);
        var player=NativePlayerAnimationStreamCodec.Decode(_models.AsSpan(0,0x10A4C),source.Geometry);
        var finalSound=NativeSoundBankCodec.Decode(_data.AsSpan(0x1DC),_upload.AsSpan(0x80000));
        if(!player.Records.Select(r=>r.AnimationId).SequenceEqual(new[]{18,19,29,40,8}) || patches.Count!=71 ||
            finalSound.Descriptors[76].Sample.ByteLength!=0x1D40 || finalSound.Descriptors[77].Sample.ByteLength!=0x5430 ||
            !finalSound.Descriptors[76].IsSupportedOneShot || !finalSound.Descriptors[77].IsSupportedOneShot ||
            Word(finalSound.Descriptors[22].CopyDescriptor(),12)!=0x20028 || placements.Sum(p=>p.RowBytes*p.Rows)!=2216)
            throw new InvalidDataException("Final resource/native source schema accounting changed.");
        Owners=owners.AsReadOnly();Patches=patches.AsReadOnly();TexturePlacements=placements.AsReadOnly();SoundBindings=sounds.AsReadOnly();
        ModelRoots=Array.AsReadOnly(new[]{new NativeFreshWizardModelRoot(0,0,0x10A4C,0x10A4C),new(0x78,0x10A4C,0x2BD0,0x2BD0),
            new(0x26,0x1361C,0x564,0x564),new(0x11B,0x13B80,0x35B4,0x3C80)});
        PendingOwners=Array.AsReadOnly(new[]{"global1F occupied mapping0-to76: Town all-consumer compatibility unresolved",
            "Town distinct role adapters, effect07/41 callbacks and source code closure not installed",
            "new body/header/root table/copy anchor and SPU DMA staging not issued",
            "scene P0/N16 unchanged: actor/private/fixups, capacity/peak, save/reward not issued",
            "GTE/renderer/transient/SPU hardware/listening/native gameplay and publication unverified"});
        UploadSha256=Hash(_upload);DataSha256=Hash(_data);ModelSha256=Hash(_models);
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new {Profile=BindingProfileId,SourceImageSha256,
            CanonicalDocumentSha256,FamilyPlanSha256=family.PlanSha256,FreshPlanSha256=source.Fresh.PlanSha256,
            UploadSha256,DataSha256,ModelSha256,Owners,ModelRoots,TexturePlacements,SoundBindings,PendingOwners,
            Patches=Patches.Select(p=>new{p.SourceOwnerId,p.SourceOffset,p.Domain,p.Offset,p.ByteLength,Before=Hash(p.CopyBefore()),After=Hash(p.CopyAfter())})}));
        return;

        void Patch(string id,int sourceAt,NativeFreshWizardResourceDomain domain,int at,ReadOnlySpan<byte> before,ReadOnlySpan<byte> after)
        {
            var original=new NativeByteExtent(originals[id],sourceAt,before.Length);
            var final=new NativeByteExtent(finals[(int)domain],at,after.Length);
            if(patches.Any(p=>p.Domain==domain && p.Offset<final.EndExclusive && at<p.Offset+p.ByteLength))
                throw new InvalidDataException("Two source field patches overlap.");
            patches.Add(new(id,original,domain,final,before,after));
        }
        void Region(NativeFreshWizardResourceDomain domain,int at,int length,string provenance,string? id,int sourceAt,bool overwrite=false)
        {
            _=new NativeByteExtent(finals[(int)domain],at,length);
            if(id is not null)_=new NativeByteExtent(originals[id],sourceAt,length);
            int[] map=tags[(int)domain];
            if(!overwrite && map.AsSpan(at,length).ContainsAnyExcept(-1))throw new InvalidDataException("Resource source regions overlap.");
            int index=regions.Count;regions.Add((domain,at,length,provenance,id,sourceAt));Array.Fill(map,index,at,length);
        }
    }

    internal NativeId65FreshDocumentCompilation CompilationAncestor=>_source.Compilation;
    internal NativeWizardFamilyAssetPlan FamilyAncestor=>_source.Family;
    internal NativeFreshWizardResourceProfile ResourceProfile=>_source.Profile;
    internal void RequireCompilationProfile()=>NativeFreshWizardAuthoringShape.RequireCompilation(CompilationAncestor,ResourceProfile);
    internal void RequireAncestry(NativeId65FreshDocumentCompilation compilation,NativeWizardFamilyAssetPlan family)
    {if(!ReferenceEquals(compilation,CompilationAncestor)||!ReferenceEquals(family,FamilyAncestor))throw new InvalidDataException("Resource result belongs to another actual issued pair.");}
    public string SourceImageSha256=>NativeMobySourceInventory.OriginalImageSha256;
    public string BindingProfileId=>ResourceProfile==NativeFreshWizardResourceProfile.FixedA
        ?"fresh-A16-wizard33-T6-research-resources-v1":NativeFreshWizardAuthoringShape.ProfileId;
    public string CanonicalDocumentSha256=>_source.Compilation.CanonicalSha256;
    public string UploadSha256{get;}
    public string DataSha256{get;}
    public string ModelSha256{get;}
    public string PlanSha256{get;}
    public IReadOnlyList<NativeFreshWizardResourceOwner> Owners{get;}
    public IReadOnlyList<NativeFreshWizardResourcePatch> Patches{get;}
    public IReadOnlyList<NativeFreshWizardModelRoot> ModelRoots{get;}
    public IReadOnlyList<NativeFreshWizardTexturePlacement> TexturePlacements{get;}
    public IReadOnlyList<NativeFreshWizardSoundBinding> SoundBindings{get;}
    public IReadOnlyList<string> PendingOwners{get;}
    public bool GlobalAudioCompatibilityProven=>false;
    public bool RuntimeVerified=>false;
    public bool CompleteNativeDependencyClosureProven=>false;
    public bool PublicationApproved=>false;
    public byte[] CopyUploadPayload()=>_upload.ToArray();
    public byte[] CopyDataComponents()=>_data.ToArray();
    public byte[] CopyModelPayload()=>_models.ToArray();
    public void RequireExactOutputs(ReadOnlySpan<byte> upload,ReadOnlySpan<byte> data,ReadOnlySpan<byte> models)
    {if(!upload.SequenceEqual(_upload)||!data.SequenceEqual(_data)||!models.SequenceEqual(_models))throw new InvalidDataException("Complete resource owner/patch/padding changed.");}
    public static async Task<NativeId65FreshWizardResearchResourcePlan> CaptureAsync(string originalBin,
        NativeId65FreshDocumentCompilation compilation,NativeWizardFamilyAssetPlan family,CancellationToken token=default)
        =>await CaptureCoreAsync(originalBin,compilation,family,NativeFreshWizardResourceProfile.FixedA,token);

    /// <summary>Actual authored untextured surface in the same fixed native layout; not clearance or publication authority.</summary>
    public static async Task<NativeId65FreshWizardResearchResourcePlan> CaptureAuthoredAsync(string originalBin,
        NativeId65FreshDocumentCompilation compilation,NativeWizardFamilyAssetPlan family,CancellationToken token=default)
        =>await CaptureCoreAsync(originalBin,compilation,family,NativeFreshWizardResourceProfile.AuthoredSameLayout,token);

    private static async Task<NativeId65FreshWizardResearchResourcePlan> CaptureCoreAsync(string originalBin,
        NativeId65FreshDocumentCompilation compilation,NativeWizardFamilyAssetPlan family,NativeFreshWizardResourceProfile profile,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(compilation);ArgumentNullException.ThrowIfNull(family);
        NativeFreshWizardAuthoringShape.RequireCompilation(compilation,profile);
        if(!compilation.CopyCanonicalDocument().AsSpan().SequenceEqual(NativeId65FreshDocumentCodec.SerializeCanonical(compilation.Intent)))
            throw new InvalidDataException("Actual canonical compilation bytes changed.");
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);string path=Path.GetFullPath(originalBin);
        await using FileStream image=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        await NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,NativeMobySourceInventory.OriginalImageByteLength,NativeMobySourceInventory.OriginalImageSha256,token);
        try
        {
            var source=await NativeId65FreshWizardResearchResourceSource.CaptureAsync(path,compilation,family,token,profile);
            var result=new NativeId65FreshWizardResearchResourcePlan(source);token.ThrowIfCancellationRequested();return result;
        }
        finally
        {
            // Complete original readback even on refusal/cancellation. This is
            // source-only cleanup, not a publication/commit boundary.
            image.Position=0;
            await NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,NativeMobySourceInventory.OriginalImageByteLength,
                NativeMobySourceInventory.OriginalImageSha256,CancellationToken.None);
            token.ThrowIfCancellationRequested();
        }
    }
    private static int TextureByte(ushort page,int u,int v)
    {
        int x=(page&15)*128-1024+u/2,y=((page&16)!=0?256:0)+v;
        if(x<0||x>=1024||y<0||y>=512)throw new InvalidDataException("Actual descriptor read escaped VRAM.");return y*1024+x;
    }
    private static ushort Half(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(b[at..]);
    private static uint Word(ReadOnlySpan<byte> b,int at)=>NativeId65FreshWizardResearchResourceSource.Word(b,at);
    private static void Put(Span<byte> b,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(b[at..],value);
    private static string Hash(ReadOnlySpan<byte> b)=>NativeId65FreshWizardResearchResourceSource.Hash(b);
}

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeJapaneseEggArtisansIntegrationSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>ProfileIndex is the compatibility name for opaque row4F, not an F5 tint selector.</summary>
public sealed record NativeJapaneseEggVisualPlacement(string Id,int X,int Y,int Z,int Yaw,int ProfileIndex);
/// <summary>ProfileDescriptor is only a numerical static-table projection; animated F5 does not select that table.</summary>
public sealed record NativeJapaneseEggArtisansRow(int NativeIndex,int RowSceneOffset,int PrivateSceneOffset,
    NativeJapaneseEggVisualPlacement Placement,uint ProfileDescriptor,string SourceRowSha256,string FinalRowSha256);

/// <summary>
/// Authenticated detached two-F5 Artisans composition. The existing model-tail,
/// scene-append, level-growth and overlay materializers own all structural writes.
/// Normal-load accounting is not peak RAM, native rendering or activation approval.
/// No disc writer, gameplay collector or persistent-save hook is exposed.
/// </summary>
public sealed class NativeJapaneseEggArtisansIntegrationPlan
{
    internal const int CopyBufferPatchOffset=0x4AF18,DispatchPatchOffset=0x319C;
    private readonly byte[] _data,_scene,_overlay,_exe,_originalData,_originalExe,_peteHeader,_peteCopy;
    private readonly NativeJapaneseEggModelAssetPlan _asset;
    private NativeJapaneseEggArtisansIntegrationPlan(NativeJapaneseEggModelAssetPlan asset,
        NativeJapaneseEggVisualBehaviorPlan visual,byte[] original,byte[] donorScene,
        NativeJapaneseObjectAppearance appearance,NativeJapaneseEggVisualPlacement[] placements,
        byte[] peteHeader,byte[] peteCopy,bool certified)
    {
        if(asset.SourceCertified!=certified||visual.SourceCertified!=certified||
            !visual.ModelAssembly.CopyModelPackage().AsSpan().SequenceEqual(asset.Model.CopyPackage())||
            !visual.ModelAssembly.RevertExact(visual.ModelAssembly.CopyLevelData()).AsSpan().SequenceEqual(original))
            throw new InvalidDataException("Artisans integration lost the actual complete model/source ancestry.");
        if(placements.Length!=2||placements.Any(p=>p is null||string.IsNullOrWhiteSpace(p.Id)||p.Id.Length>64||
            p.Id.Any(c=>c is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '-')||
            p.X is <0 or >0x3FFFF||p.Y is <0 or >0x3FFFF||p.Z is <0 or >0xFFFF||p.Yaw is <0 or >255||p.ProfileIndex is <0 or >=32)||
            placements.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()!=2)
            throw new InvalidDataException("Detached Artisans requires exactly two distinct bounded visual placements.");
        placements=placements.OrderBy(p=>p.Id,StringComparer.Ordinal).ToArray();
        SourceCertified=certified;_asset=asset;VisualAncestor=visual;_originalData=original.ToArray();
        _originalExe=visual.CopyUsaExecutable();_peteHeader=peteHeader.ToArray();_peteCopy=peteCopy.ToArray();
        if(certified)
        {
            RequireHash(original,"86cde4f886d97821a3a4b869100b586842dbd4b9fb696558bead274bb547af4f");
            RequireHash(donorScene,"aa597ffebcb77ca97c62cf67616c1d52ae6c0e81c009a622f02d6dcc97b5413c");
            if(appearance.Revision!=NativeDiscRevision.UsaScus94228||appearance.SourceImageSha256!=asset.UsaSourceImageSha256)
                throw new InvalidDataException("Appearance must be issued from the exact destination USA source.");
        }
        byte[] baseline=visual.ModelAssembly.CopyLevelData();
        int oldSceneOffset=checked((int)Word(original,0x18)),sceneLength=checked((int)Word(original,0x1C));
        byte[] sourceScene=original.AsSpan(oldSceneOffset,sceneLength).ToArray();
        var usScene=Domain(NativeAddressKind.Scene,"usa-artisans",sourceScene);
        var jpScene=Domain(NativeAddressKind.Scene,"jp-artisans-f5",donorScene);
        var usExe=Domain(NativeAddressKind.ExecutableFile,"usa-artisans-exe",_originalExe);
        byte[] Read(NativeByteExtent extent)=>ReferenceEquals(extent.Space,usScene)?Slice(sourceScene,extent):
            ReferenceEquals(extent.Space,jpScene)?Slice(donorScene,extent):ReferenceEquals(extent.Space,usExe)?Slice(_originalExe,extent):asset.ReadSource(extent);
        OriginalSceneSource=NativeMobySceneAppendSource.Capture(usScene,Hash(sourceScene),Read);
        // Parse the actual source row table; native certification additionally
        // pins the complete JP scene and exact independently identified row193.
        int moby=0x88;
        for(int i=0;i<7;i++)moby=ComponentEnd(donorScene,moby,8);
        int properties=ComponentEnd(donorScene,moby,8),propertiesEnd=ComponentEnd(donorScene,properties,4);
        int count=checked((int)Word(donorScene,moby+4));
        if(count is <1 or >256||moby+8L+count*0x58>properties)throw new InvalidDataException("Donor row table exceeds its actual source owner.");
        int[] selected=Enumerable.Range(0,count).Where(i=>Half(donorScene,moby+8+i*0x58+0x36)==0xF5).ToArray();
        if(selected.Length!=1)throw new InvalidDataException("Expected one exact Artisans source F5 row.");
        int sourceIndex=selected[0],rowOffset=moby+8+sourceIndex*0x58,privateOffset=checked((int)Word(donorScene,rowOffset));
        if(privateOffset<properties+4||privateOffset>propertiesEnd-12||donorScene.AsSpan(privateOffset,12).ContainsAnyExcept((byte)0)||
            donorScene[rowOffset+0x49]!=0||donorScene[rowOffset+0x3C]!=0||donorScene[rowOffset+0x3D]!=0||donorScene[rowOffset+0x4F]!=1||
            Enumerable.Range(0,count).Any(i=>i!=sourceIndex&&Word(donorScene,moby+8+i*0x58)>=privateOffset&&Word(donorScene,moby+8+i*0x58)<privateOffset+12))
            throw new InvalidDataException("F5 private/state/animation source is not the independent zeroed12-byte owner.");
        string rowHash=Hash(donorScene.AsSpan(rowOffset,0x58));
        if(certified&&(sourceIndex!=193||rowOffset!=0xE418||privateOffset!=0x133A8||
            rowHash!="1d2b9428be365e08b951218f7daf73530b433e28d40b9455b1b6f60e857cc083"))
            throw new InvalidDataException("Exact Japanese Artisans native row binding changed.");
        FinalBodyDomain=new(NativeAddressKind.LevelData,"two-f5-body:"+Hash(original),NativeAddressRevision.Final,baseline.Length);
        FinalSceneDomain=new(NativeAddressKind.Scene,"two-f5-scene:"+Hash(sourceScene),NativeAddressRevision.Final,sceneLength);
        ModelGraph=NativeMobyDependencyClosureCompiler.Compile(asset.PackageNodes(new(FinalBodyDomain,
            visual.ModelAssembly.PackageOffset,asset.Model.PackageByteLength)),[NativeJapaneseEggModelAssetPlan.PackageNodeId],asset.ReadSource);
        var schema=new NativeMobyDependencyPrivateSchema("egg",sourceIndex,0xF5,
            new("native-row",new(jpScene,rowOffset,0x58),rowHash),
            new("native-private",new(jpScene,privateOffset,12),Hash(donorScene.AsSpan(privateOffset,12))),0,[],false);
        // There is no actor-exclusive mutable global: USA RNG is an explicitly
        // shared engine service, already pinned by the visual helper issuer.
        // This does NOT claim isolated random sequences between live instances.
        var template=NativeMobyDependencyInstanceTemplate.Capture("jp-f5-artisans-visual-two-v1",[schema],[],ModelGraph,Read);
        SceneAppend=NativeMobySceneAppendPlan.Compile(OriginalSceneSource,FinalSceneDomain,[FinalBodyDomain,FinalSceneDomain],usExe,
            placements.Select(p=>new NativeMobySceneAppendRequest(new(p.Id,template),[])),Read);
        _scene=SceneAppend.CopyScene();List<NativeJapaneseEggArtisansRow> rows=[];
        foreach(var family in SceneAppend.Allocation.Families)
        {
            var request=placements.Single(p=>p.Id==family.Id);var allocated=family.Rows.Single();int at=(int)allocated.Row.Offset;
            byte[] before=_scene.AsSpan(at,0x58).ToArray(),after=appearance.WithProfile(before,request.ProfileIndex);
            Put(after,12,(uint)request.X);Put(after,16,(uint)request.Y);Put(after,20,(uint)request.Z);
            Moby.YawByteToMatrixBytes(request.Yaw).CopyTo(after,0x20);after[0x46]=(byte)request.Yaw;
            for(int i=0;i<0x58;i++)if(before[i]!=after[i]&&!(i>=12&&i<24||i>=0x20&&i<0x32||i==0x46||i==0x4F))
                throw new InvalidDataException("Authored visual row revision changed an unowned source field.");
            after.CopyTo(_scene,at);
            rows.Add(new(allocated.Index,at,(int)allocated.Properties.Offset,request,
                appearance.DescribeRow(after).ShadingProfile.Descriptor,rowHash,Hash(after)));
        }
        Rows=rows.AsReadOnly();
        _data=ReplaceScene(baseline,_scene);
        if(_data.Length!=baseline.Length||!_data.AsSpan(0,checked((int)Word(_data,0x18))).SequenceEqual(baseline.AsSpan(0,checked((int)Word(baseline,0x18)))))
            throw new InvalidDataException("Exact-size scene composition changed model/header ownership.");
        foreach(var install in ModelGraph.Installs)
            if(!ReferenceEquals(install.Destination.Space,FinalBodyDomain)||!_data.AsSpan((int)install.Destination.Offset,(int)install.Destination.ByteLength).SequenceEqual(ModelGraph.CopyPayload(install.NodeId)))
                throw new InvalidDataException("The single shared model is not installed in its exact final owner.");
        var registrations=NativeMobyRegistrationTableLayout.Decode(_data.AsSpan(0,0x800),(int)Word(_data,0x10),(int)Word(_data,0x14));
        if(registrations.Registrations.Count(r=>r.ActorId==0xF5)!=1)throw new InvalidDataException("Two eggs require exactly one F5 model registration.");

        byte[] originalOverlay=visual.CopyOriginalOverlay(),prior=visual.CopyDetachedOverlay();
        if(originalOverlay.Length!=0xE000||Word(originalOverlay,DispatchPatchOffset)!=0x080215E0||Word(originalOverlay,DispatchPatchOffset+4)!=0)
            throw new InvalidDataException("The exact USA default leaf is not j80085780/nop.");
        var oldBlocks=visual.Layout.Blocks.Where(b=>!b.IsPadding).ToArray();
        Layout=NativeOwnedOverlayAppendLayout.Create("two-f5-artisans-overlay:"+Hash(originalOverlay),originalOverlay,0x8007AA38,0xE800,
            oldBlocks.Select(b=>new NativeOwnedOverlayBlockSize(b.Id,(int)b.Owner.ByteLength)).Append(new("f5-only-dispatch",0x1C)),"alignment");
        var blocks=Layout.Blocks.Where(b=>!b.IsPadding).ToDictionary(b=>b.Id,b=>b.Owner);List<NativeOwnedOverlayBlock> payloads=[];
        foreach(var old in oldBlocks)
        {
            var owned=blocks[old.Id];
            if(owned.Offset!=old.Owner.Offset||owned.ByteLength!=old.Owner.ByteLength)throw new InvalidDataException("Existing linked visual PCs cannot move without relinking.");
            payloads.Add(new(old.Id,owned,prior.AsSpan((int)old.Owner.Offset,(int)old.Owner.ByteLength).ToArray()));
        }
        var guard=blocks["f5-only-dispatch"];GuardPc=checked(0x8007AA38+(uint)guard.Offset);
        // Branch delay restores v1 for BOTH paths. No stack, HI/LO or scratch
        // register changes occur before the existing full-register adapter.
        payloads.Add(new("f5-only-dispatch",guard,Words(0x2463FF0B,0x10600003,0x246300F5,
            Jump(0x80085780),0,Jump(visual.EntryPc),0)));
        _overlay=Layout.Materialize(payloads,blocks.Select(b=>new NativeOwnedOverlayEntry(b.Key,b.Key,new(Layout.Domain,b.Value.Offset,4))),[]);
        Put(_overlay,DispatchPatchOffset,Jump(GuardPc));Put(_overlay,DispatchPatchOffset+4,0);
        _exe=_originalExe.ToArray();
        if(Word(_exe,CopyBufferPatchOffset)!=0x3C028009||Word(_exe,CopyBufferPatchOffset+4)!=0x24428620||
            Word(_exe,CopyBufferPatchOffset+8)!=0x3C018008||Word(_exe,CopyBufferPatchOffset+12)!=0xAC2285D8)
            throw new InvalidDataException("Exact Artisans non-end-aligned copy-buffer initializer changed.");
        short low=unchecked((short)FinalCopyBufferAddress);uint high=(uint)(((long)FinalCopyBufferAddress-low)>>16);
        Put(_exe,CopyBufferPatchOffset,0x3C020000|high);Put(_exe,CopyBufferPatchOffset+4,0x24420000u|(ushort)low);
        LevelDataSha256=Hash(_data);SceneSha256=Hash(_scene);OverlaySha256=Hash(_overlay);ExecutableSha256=Hash(_exe);
        RequireFinal(_overlay,_exe,_data);
        if(certified)
        {
            OriginalRuntimeMemory=NativeLevelRuntimeMemoryReport.AnalyzeJapaneseEggArtisans(this,true);
            RuntimeMemory=NativeLevelRuntimeMemoryReport.AnalyzeJapaneseEggArtisans(this,false);
        }
        else
        {
            OriginalRuntimeMemory=NativeLevelRuntimeMemoryReport.DecodeFixture(10,original,OriginalCopyBufferAddress);
            RuntimeMemory=NativeLevelRuntimeMemoryReport.DecodeFixture(10,_data,FinalCopyBufferAddress);
        }
        if(!RuntimeMemory.KnownPostLoadExtentsDisjoint||RuntimeMemory.PlacedMobyCount!=SceneAppend.PlacedCountAfter||
            RuntimeMemory.DynamicMobyCapacity!=SceneAppend.DynamicCapacityAfter)
            throw new InvalidDataException("Composed resources disagree with native normal-load/scene capacity accounting.");
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{profile="detached-two-f5-artisans-v1",SourceCertified,
            LevelDataSha256,SceneSha256,OverlaySha256,ExecutableSha256,placements,model=ModelGraph.Fingerprint,
            scene=SceneAppend.Graph.Fingerprint,OriginalCopyBufferAddress,FinalCopyBufferAddress,activation=false}));
        if(!RevertDataExact(_data).AsSpan().SequenceEqual(original))throw new InvalidDataException("Composed complete-data inverse failed.");
    }
    public bool SourceCertified{get;}
    public NativeJapaneseEggVisualBehaviorPlan VisualAncestor{get;}
    public IReadOnlyList<NativeJapaneseEggArtisansRow> Rows{get;}
    public NativeLevelRuntimeMemoryReport OriginalRuntimeMemory{get;}
    public NativeLevelRuntimeMemoryReport RuntimeMemory{get;}
    public uint OriginalCopyBufferAddress=>0x80088620;
    public uint FinalCopyBufferAddress=>Layout.CopyAnchor;
    public uint GuardPc{get;}
    public string LevelDataSha256{get;}
    public string SceneSha256{get;}
    public string OverlaySha256{get;}
    public string ExecutableSha256{get;}
    public string PlanSha256{get;}
    public bool ActivationApproved=>false;
    public bool NativeImportApproved=>false;
    public bool PublicationApproved=>false;
    public bool RenderVerified=>false;
    public void RequireNativeActivation()=>throw new InvalidDataException("Detached plan is not activatable: peak/transient RAM, actual stack, native draw-list/animation entry and rendered appearance remain unproved.");
    public byte[] CopyLevelData()=>_data.ToArray();
    public byte[] CopyScene()=>_scene.ToArray();
    public byte[] CopyOverlay()=>_overlay.ToArray();
    public byte[] CopyExecutable()=>_exe.ToArray();
    public byte[] CopyOriginalLevelData()=>_originalData.ToArray();
    public byte[] CopyOriginalScene()=>SceneAppend.CopyOriginalScene();
    public byte[] CopyOriginalOverlay()=>VisualAncestor.CopyOriginalOverlay();
    public byte[] CopyOriginalExecutable()=>_originalExe.ToArray();
    public byte[] RevertDataExact(ReadOnlySpan<byte> afterimage)
    {
        if(!afterimage.SequenceEqual(_data))throw new InvalidDataException("Data inverse requires the complete exact two-F5 afterimage.");
        byte[] modelOnly=ReplaceScene(afterimage.ToArray(),CopyOriginalScene());
        return VisualAncestor.ModelAssembly.RevertExact(modelOnly);
    }
    public byte[] RevertOverlayExact(ReadOnlySpan<byte> afterimage)
    {if(!afterimage.SequenceEqual(_overlay))throw new InvalidDataException("Overlay inverse requires the complete exact two-F5 afterimage.");return CopyOriginalOverlay();}
    public byte[] RevertExecutableExact(ReadOnlySpan<byte> afterimage)
    {if(!afterimage.SequenceEqual(_exe))throw new InvalidDataException("EXE inverse requires the complete exact two-F5 afterimage.");return CopyOriginalExecutable();}
    public void RequireFinal(ReadOnlySpan<byte> overlay,ReadOnlySpan<byte> executable,ReadOnlySpan<byte> data)
    {if(!overlay.SequenceEqual(_overlay)||!executable.SequenceEqual(_exe)||!data.SequenceEqual(_data))throw new InvalidDataException("Two-F5 source-issued complete afterimage changed.");}
    internal NativeMobySceneAppendSource OriginalSceneSource{get;}
    internal NativeMobySceneAppendPlan SceneAppend{get;}
    internal NativeMobyDependencyClosurePlan ModelGraph{get;}
    internal NativeOwnedOverlayAppendLayout Layout{get;}
    internal NativeAddressSpace FinalBodyDomain{get;}
    internal NativeAddressSpace FinalSceneDomain{get;}
    internal byte[] CopyPeteHeader()=>_peteHeader.ToArray();
    internal byte[] CopyPeteRuntime()=>_peteCopy.ToArray();
    public static NativeJapaneseEggArtisansIntegrationPlan Capture(NativeRegionalSourceSession japan,NativeRegionalSourceSession usa,
        IReadOnlyList<NativeJapaneseEggVisualPlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(japan);ArgumentNullException.ThrowIfNull(usa);ArgumentNullException.ThrowIfNull(placements);
        if(placements.Count!=2)throw new InvalidDataException("This research integration admits exactly two instances.");
        var asset=NativeJapaneseEggModelAssetPlan.Capture(japan,usa);
        var visual=NativeJapaneseEggVisualBehaviorPlan.Capture(japan,usa,asset);
        byte[] donor=japan.ReadWadEntry(23);int scene=(int)Word(donor,0x18),length=(int)Word(donor,0x1C);
        var result=new NativeJapaneseEggArtisansIntegrationPlan(asset,visual,usa.ReadWadEntry(10),donor.AsSpan(scene,length).ToArray(),
            NativeJapaneseObjectAppearance.Capture(usa),placements.ToArray(),usa.ReadWadRange(0x7B8800,0x800),usa.ReadWadRange(0x7B9000,0x39C48),true);
        japan.VerifyUnchanged();usa.VerifyUnchanged();return result;
    }
    internal static NativeJapaneseEggArtisansIntegrationPlan Fixture(NativeJapaneseEggModelAssetPlan asset,NativeJapaneseEggVisualBehaviorPlan visual,
        byte[] original,byte[] donorScene,NativeJapaneseObjectAppearance appearance,NativeJapaneseEggVisualPlacement[] placements)=>
        new(asset,visual,original,donorScene,appearance,placements,[],[],false);
    private static byte[] ReplaceScene(byte[] body,byte[] scene)
    {
        int length=checked((int)Word(body,0x1C));if(scene.Length!=length)throw new InvalidDataException("Only an exact-size owned scene replacement is admitted.");
        // This is the existing neutral outer geometry kernel, not a second writer.
        var growth=new NativeLevelDataGrowthLayout(body.AsSpan(0,0x800),body.Length,[new(0,0,0,0,0,length,scene,3,false)]);
        if(growth.Growth!=0)throw new InvalidDataException("Same-size scene replacement must not grow an outer owner.");
        return growth.Expand(body);
    }
    private static int ComponentEnd(byte[] bytes,int at,int minimum)
    {
        if(at<0||at>bytes.Length-4)throw new InvalidDataException("Scene component header is out of bounds.");uint n=Word(bytes,at);
        if(n<minimum||n%4!=0||n>bytes.Length-at)throw new InvalidDataException("Scene component leaves its owner.");return checked(at+(int)n);
    }
    private NativeAddressSpace Domain(NativeAddressKind kind,string id,byte[] b)=>new(kind,(SourceCertified?"source:":"fixture:")+id+":"+Hash(b),NativeAddressRevision.Original,b.Length);
    private static byte[] Slice(byte[] b,NativeByteExtent e)=>b.AsSpan((int)e.Offset,(int)e.ByteLength).ToArray();
    private static ushort Half(byte[] b,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(at));
    private static uint Word(ReadOnlySpan<byte>b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
    private static void Put(byte[] b,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at),value);
    private static uint Jump(uint pc)=>0x08000000|(pc>>2&0x3FFFFFF);
    private static byte[] Words(params uint[] words){byte[] b=new byte[4*words.Length];for(int i=0;i<words.Length;i++)Put(b,4*i,words[i]);return b;}
    private static string Hash(ReadOnlySpan<byte>b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void RequireHash(byte[] b,string hash){if(Hash(b)!=hash)throw new InvalidDataException("Exact Artisans source owner changed.");}
}

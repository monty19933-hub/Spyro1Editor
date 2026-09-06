using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Research-only source factory; no caller-authored graph, ownership or native schema is accepted.</summary>
internal static class NativeTownSquareLockedChestClosureCompiler
{
    internal const string SourceSha256 = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    internal const string Profile = "native-closure.peace-ae.townsquare.properties-and-sound.v2";
    internal const long TownDataWad = 0x119E000, PeaceDataWad = 0x1890800, TownOverlayWad = 0x118E800;
    internal const int OriginalScene = 0x1D0000, FinalScene = 0x1D1800, PropertiesEnd = 0x5F0C;
    internal const int PropertiesGrowth = 0xE4, OriginalFixup = 0x7F28, FinalFixup = 0x800C;
    private const string TownDataHash = "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";
    private const string PeaceDataHash = "c140bd422471c66cd57bd85b29912819f798f5b080779153674714889306c9d3";
    private const string TownOverlayHash = "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5";
    private const string ExeHash = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";

    internal static NativeTownSquareLockedChestClosure Capture(string sourcePath,
        NativeTownSquareClosureRecipe recipe=NativeTownSquareClosureRecipe.PropertiesAndSoundV2)
    {
        _=NativeTownSquareClosurePlacement.ProfileFor(recipe);
        sourcePath = Path.GetFullPath(sourcePath);
        using FileStream source = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (source.Length != 661547040 || Hash(SHA256.HashData(source), alreadyDigest: true) != SourceSha256)
            throw new InvalidDataException("Portable native closure requires the exact immutable USA source image.");
        DiscLayout layout = DiscImage.DetectLayout(sourcePath);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("Native closure requires MODE2/2352 source geometry.");
        DiscFileRecord wad = DiscImage.FindRootFileRecord(source, layout, n => n.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord exe = DiscImage.FindRootFileRecord(source, layout, n => n.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        if (wad.Lba != 37 || wad.Size != 0x6927000 || exe.Lba != 53875 || exe.Size != 0x66000)
            throw new InvalidDataException("Native closure ISO ownership changed.");
        byte[] header = DiscImage.ReadFileBytes(source, layout, 37, 0, 2048);
        byte[] ReadEntry(int id, long expectedOffset, int expectedLength)
        {
            if (U32(header, id * 8) != expectedOffset || U32(header, id * 8 + 4) != expectedLength)
                throw new InvalidDataException("Native WAD entry ownership changed.");
            return DiscImage.ReadFileBytes(source, layout, 37, expectedOffset, expectedLength);
        }
        byte[] data = ReadEntry(16, TownDataWad, 0x2E2000), peace = ReadEntry(22, PeaceDataWad, 0x29E800);
        byte[] overlay = ReadEntry(15, TownOverlayWad, 0xF800), donorOverlay = ReadEntry(21, 0x187E000, 0x12800);
        byte[] executable = DiscImage.ReadFileBytes(source, layout, exe.Lba, 0, exe.Size);
        RequireHash(data, TownDataHash); RequireHash(peace, PeaceDataHash); RequireHash(overlay, TownOverlayHash); RequireHash(executable, ExeHash);
        RequireHash(donorOverlay, "299f4822489c48736c9f46bb9556e82952fc1693f5f9f42bc2e6415c1908aebc");
        NativeTexturePageOwnershipReport textureOwnership = NativeTexturePageOwnershipScanner.Scan(sourcePath,
            new LevelDefinition { Key="townsquare", DisplayName="Town Square", LevelId=13, SourceWadEntry=16 });
        if (!textureOwnership.AllConsumerClosureComplete || !textureOwnership.ProvablyPrivatePixelAndClutSpace ||
            textureOwnership.TexturePagesWadOffset != TownDataWad + 0x800)
            throw new InvalidDataException("Native destination texture ownership is incomplete.");
        return Compile(sourcePath, layout, data, peace, overlay, donorOverlay, executable, header, textureOwnership,recipe);
    }

    private static NativeTownSquareLockedChestClosure Compile(string sourcePath, DiscLayout layout, byte[] data, byte[] peace,
        byte[] overlay, byte[] donorOverlay, byte[] executable, byte[] header, NativeTexturePageOwnershipReport textureOwnership,
        NativeTownSquareClosureRecipe recipe)
    {
        string profile=NativeTownSquareClosurePlacement.ProfileFor(recipe);
        // Native component grammar: Moby rows, properties, pod, collision, then fixups.
        byte[] scene = data.AsSpan(OriginalScene, 0x8800).ToArray();
        RequireWord(data, 16, 0x173000); RequireWord(data, 20, 0x5D000); RequireWord(data, 24, OriginalScene);
        RequireWord(scene, 0x168, 0x4CD0); RequireWord(scene, 0x16C, 107);
        RequireWord(scene, 0x4E38, 0x10D4); RequireWord(scene, PropertiesEnd, 0x18);
        RequireWord(scene, 0x5F24, 0x2004); RequireWord(scene, OriginalFixup, 129);
        if (scene.AsSpan(0x8130).ContainsAnyExcept((byte)0)) throw new InvalidDataException("Native scene growth tail is not padding.");
        for (int i = 0; i < 129; i++)
        {
            int field = checked((int)U32(scene, OriginalFixup + 4 + i * 4));
            if (field < 0 || field >= PropertiesEnd || U32(scene, field) >= PropertiesEnd)
                throw new InvalidDataException("Native properties append would move an existing pointer field or target.");
        }
        // Do not infer absence from labels or missing placed instances. Enumerate actual registrations,
        // then verify the native dispatch paths' default exits in the exact hashed overlay.
        ushort[] ids = Enumerable.Range(0,64).Where(i=>U32(data,0x50+i*4)!=0)
            .Select(i=>BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0x150+i*2,2))).ToArray();
        if (ids.Length != 37 || ids.Contains((ushort)0xAE) || !new ushort[]{0x135,0x136,0x137}.All(ids.Contains))
            throw new InvalidDataException("Native absent chest / resident debris registration contract changed.");
        foreach (int off in new[]{0x8007DB80-0x8007AA38,0x8007DC4C-0x8007AA38})
        { RequireWord(overlay,off,0x08021C96); RequireWord(overlay,off+4,0); }
        RequireWord(overlay,(int)(0x8007DBD8-0x8007AA38),0x240200AD);
        RequireWord(overlay,(int)(0x8007DBDC-0x8007AA38),0x108217DB);
        for(int i=0;i<107;i++) if (new uint[]{0xAD,0xAE,0xD}.Contains(U16(scene,0x170+i*0x58+0x36)))
            throw new InvalidDataException("Destination unexpectedly contains a native family instance.");

        NativeAddressSpace dataOriginal = new(NativeAddressKind.LevelData,"town:"+TownDataHash,NativeAddressRevision.Original,data.Length);
        NativeAddressSpace peaceOriginal = new(NativeAddressKind.LevelData,"peace:"+PeaceDataHash,NativeAddressRevision.Original,peace.Length);
        NativeAddressSpace sceneOriginal = new(NativeAddressKind.Scene,"town-scene:"+Hash(scene),NativeAddressRevision.Original,scene.Length);
        NativeAddressSpace overlayOriginal = new(NativeAddressKind.LevelData,"town-overlay:"+TownOverlayHash,NativeAddressRevision.Original,overlay.Length);
        NativeAddressSpace donorOverlayOriginal = new(NativeAddressKind.LevelData,"peace-overlay:"+Hash(donorOverlay),NativeAddressRevision.Original,donorOverlay.Length);
        NativeAddressSpace exeOriginal = new(NativeAddressKind.ExecutableFile,ExeHash,NativeAddressRevision.Original,executable.Length);
        NativeAddressSpace dataFinal = new(NativeAddressKind.LevelData,profile+":data",NativeAddressRevision.Final,0x2E3800);
        NativeAddressSpace sceneFinal = new(NativeAddressKind.Scene,profile+":scene",NativeAddressRevision.Final,0x8800);
        NativeAddressSpace overlayFinal = new(NativeAddressKind.LevelData,profile+":overlay",NativeAddressRevision.Final,0x10800);
        NativeAddressSpace exeFinal = new(NativeAddressKind.ExecutableFile,profile+":exe",NativeAddressRevision.Final,executable.Length);
        Dictionary<NativeAddressSpace,byte[]> originals = new(){[dataOriginal]=data,[peaceOriginal]=peace,[sceneOriginal]=scene,
            [overlayOriginal]=overlay,[donorOverlayOriginal]=donorOverlay,[exeOriginal]=executable};
        List<NativeMobyDependencyNode> nodes=[];
        List<(NativeAddressSpace Space,int Offset,int Length)> reservations=[];
        NativeMobyDependencyRead Read(NativeAddressSpace space,int off,int len,string id="native") =>
            new(id,new(space,off,len),Hash(originals[space].AsSpan(off,len)));
        NativeMobyDependencyNode Add(string id,NativeMobyDependencyKind kind,string[] deps,NativeMobyDependencyRead[] reads,
            Func<NativeMobyDependencyBuildContext,byte[]> build,NativeAddressSpace? destination=null,int offset=0,int length=0,
            string? owner=null,NativeMobyDependencyReference[]? references=null)
        {
            if(destination!=null) reservations.Add((destination,offset,length));
            NativeMobyDependencyNode node=new(id,kind,NativeMobyDependencyResolution.ImportFromSource,
                owner==null?NativeMobyDependencySharing.Immutable:NativeMobyDependencySharing.PrivateInstance,SourceSha256,deps,reads,
                c=>new(build(c),destination==null?[]:[new("install",new(destination,offset,length))],references??[]),owner);
            nodes.Add(node);return node;
        }
        void Aggregate(string id,NativeMobyDependencyKind kind,params string[] deps)=>nodes.Add(new(id,kind,
            NativeMobyDependencyResolution.Aggregate,NativeMobyDependencySharing.Immutable,SourceSha256,deps,[]));
        void Resident(string id,NativeMobyDependencyKind kind,NativeAddressSpace from,int offset,int length,NativeAddressSpace to,int finalOffset,
            bool global=false,string[]? deps=null)=>nodes.Add(new(id,kind,global?NativeMobyDependencyResolution.GlobalShared:NativeMobyDependencyResolution.DestinationResident,
                NativeMobyDependencySharing.Immutable,SourceSha256,deps??[],[Read(from,offset,length)],Preserved:[new("native",0,new(to,finalOffset,length))]));

        if(recipe==NativeTownSquareClosureRecipe.SpatiallySeparatedV3)
            Add("placement.site",NativeMobyDependencyKind.ObjectIndex,[],[Read(dataOriginal,0,data.Length,"data"),Read(exeOriginal,0,executable.Length,"executable")],
                c=>NativeTownSquareClosurePlacement.BuildPayload(c.Read("data"),c.Read("executable")));

        // Sound domains are deliberately separate: mapped global IDs 0/0x37, versus the model's
        // local descriptor byte 0x2F. Compare both playback metadata and uploaded ADPCM ownership.
        void Sound(string id,int donorDescriptor,int targetDescriptor,int donorSample,int targetSample,int sampleBytes,string sampleHash)
        {
            RequireHash(peace.AsSpan(donorSample,sampleBytes),sampleHash); RequireHash(data.AsSpan(targetSample,sampleBytes),sampleHash);
            if(!peace.AsSpan(donorDescriptor+4,16).SequenceEqual(data.AsSpan(targetDescriptor+4,16)) ||
                U32(peace,donorDescriptor)+0x80800!=donorSample || U32(data,targetDescriptor)+0x80800!=targetSample)
                throw new InvalidDataException("Native sound descriptor does not own an equivalent resident sample.");
            Resident(id+".descriptor",NativeMobyDependencyKind.Sound,dataOriginal,targetDescriptor,20,dataFinal,targetDescriptor);
            Resident(id+".sample",NativeMobyDependencyKind.Sound,dataOriginal,targetSample,sampleBytes,dataFinal,targetSample);
            Add(id+".equivalence",NativeMobyDependencyKind.Sound,[],[Read(peaceOriginal,donorDescriptor,20,"donor-descriptor"),
                Read(dataOriginal,targetDescriptor,20,"target-descriptor"),Read(peaceOriginal,donorSample,sampleBytes,"donor-sample"),
                Read(dataOriginal,targetSample,sampleBytes,"target-sample")],c=>{
                    if(!c.Read("donor-descriptor").AsSpan(4).SequenceEqual(c.Read("target-descriptor").AsSpan(4))||
                        !c.Read("donor-sample").SequenceEqual(c.Read("target-sample")))throw new InvalidDataException("Extracted native sound dependencies are not equivalent.");
                    return c.Read("target-descriptor");});
            Aggregate(id,NativeMobyDependencyKind.Sound,id+".descriptor",id+".sample",id+".equivalence","sound.native-routines");
        }
        foreach(var cert in new[]{(0x28D1C,0xA0),(0x3BE0,0x26C),(0x34F0,0x68),(0x5FC0,0x98),(0x46278,0x82C)})
            Resident("sound.native-routine."+cert.Item1,NativeMobyDependencyKind.NativeRoutine,exeOriginal,cert.Item1,cert.Item2,exeFinal,cert.Item1,true);
        Aggregate("sound.native-routines",NativeMobyDependencyKind.NativeRoutine,nodes.Where(n=>n.Id.StartsWith("sound.native-routine.",StringComparison.Ordinal)).Select(n=>n.Id).ToArray());
        Sound("sound.key",0x160BC8,0x17286C,0x8E100,0x8E100,0x3C40,"57a13ba3c0348ecd5212ea7fca7c23d211c9e298e1a3a8ae06f571cd01459610");
        Sound("sound.reward",0x160D30,0x1729D4,0xAF2E0,0xAF2E0,0xB60,"dfb35ed7a3c560113f0e8530bf1efe423ea1306f90f432ec991f4949a000ee31");
        Sound("sound.chest-local54",0x160E20,0x172B50,0xC1B00,0xC5420,0x1430,"f10bef3c98281716bf79cb09c77ff6017c2959d10f11ba4f0b9cda1868c977c3");
        foreach(var mapped in new[]{(0,17),(0x37,35)})
        {
            if(peace[0x160970+mapped.Item1]!=mapped.Item2||data[0x172614+mapped.Item1]!=mapped.Item2)
                throw new InvalidDataException("Native mapped global sound index changed.");
            Resident("sound.map."+mapped.Item1,NativeMobyDependencyKind.Sound,dataOriginal,0x172614+mapped.Item1,1,dataFinal,0x172614+mapped.Item1);
        }
        Resident("key.native-handler",NativeMobyDependencyKind.Behavior,overlayOriginal,(int)(0x80083B4C-0x8007AA38),0x160,overlayFinal,(int)(0x80083B4C-0x8007AA38));
        Resident("key.special-renderer-and-global-routines",NativeMobyDependencyKind.SpecialRenderer,exeOriginal,0,0x1E034,exeFinal,0,true);
        // Full immutable global code remains preserved below; rootless keys/controllers do not invent model registrations.
        Aggregate("key.rootless-rendering",NativeMobyDependencyKind.SpecialRenderer,"key.native-handler","key.special-renderer-and-global-routines","sound.key","sound.map.0","effects.native-debris-and-particles");
        Resident("effects.native-registration",NativeMobyDependencyKind.Effect,dataOriginal,0x50,0x94,dataFinal,0x50);
        foreach(var debris in new[]{(0x135,0x1BF8E0,0x1CC804,0x224,20),(0x136,0x1BFB04,0x1CCA28,0x1D4,16),(0x137,0x1BFCD8,0x1CCBFC,0x2D8,28)})
        {
            string id="effects.native-debris."+debris.Item1;
            Resident(id,NativeMobyDependencyKind.ActorPackage,dataOriginal,debris.Item3,debris.Item4,dataFinal,debris.Item3);
            Add(id+".equivalence",NativeMobyDependencyKind.ActorPackage,[],[Read(peaceOriginal,debris.Item2,debris.Item4,"donor"),Read(dataOriginal,debris.Item3,debris.Item4,"target")],c=>{
                byte[] donor=c.Read("donor"),target=c.Read("target");
                for(int f=0;f<debris.Item5;f++){donor.AsSpan(0x74+f*0x14,12).Clear();target.AsSpan(0x74+f*0x14,12).Clear();}
                if(!donor.SequenceEqual(target))throw new InvalidDataException("Native debris differs outside repacked texture descriptors.");
                return c.Read("target");});
        }
        for(int y=0;y<32;y++)
        {
            if(!peace.AsSpan(0x28B50+y*0x400,16).SequenceEqual(data.AsSpan(0x58A70+y*0x400,16)))throw new InvalidDataException("Native debris particle pixels differ.");
            Resident("effects.native-debris-pixels."+y,NativeMobyDependencyKind.Texture,dataOriginal,0x58A70+y*0x400,16,dataFinal,0x58A70+y*0x400);
        }
        if(!peace.AsSpan(0x54920,32).SequenceEqual(data.AsSpan(0x78AA0,32)))throw new InvalidDataException("Native debris CLUT differs.");
        Resident("effects.native-debris-clut",NativeMobyDependencyKind.Texture,dataOriginal,0x78AA0,32,dataFinal,0x78AA0);
        foreach(var cert in new[]{(0xC86C,0xB7C),(0xE530,0xF64),(0x38C,0x144)})
            Resident("effects.native-overlay."+cert.Item1,NativeMobyDependencyKind.Effect,overlayOriginal,cert.Item1,cert.Item2,overlayFinal,cert.Item1);
        foreach(var cert in new[]{(0x4AC70,0x30),(0x4B0A0,0x90),(0x1BD8,4),(0x5ECE0,0x48)})
            Resident("effects.native-executable."+cert.Item1,NativeMobyDependencyKind.NativeRoutine,exeOriginal,cert.Item1,cert.Item2,exeFinal,cert.Item1,true);
        RequireWord(executable,0x1BD8,0x8005A898); // Level 13's actual global callback initializer.
        RequireWord(overlay,0x38C+0x46*4,0x8008978C);
        RequireWord(overlay,0x38C+0xC*4,0x800892C0);
        Resident("key.particle-descriptor",NativeMobyDependencyKind.Texture,dataOriginal,0x1725C8,12,dataFinal,0x1725C8);
        for(int y=0;y<32;y++)
        {
            if(!peace.AsSpan(0x50910+y*0x400,16).SequenceEqual(data.AsSpan(0x78A80+y*0x400,16)))throw new InvalidDataException("Native key particle pixels differ.");
            Resident("key.particle-pixels."+y,NativeMobyDependencyKind.Texture,dataOriginal,0x78A80+y*0x400,16,dataFinal,0x78A80+y*0x400);
        }
        if(!peace.AsSpan(0x68C60,32).SequenceEqual(data.AsSpan(0x7BB40,32)))throw new InvalidDataException("Native key particle CLUT differs.");
        Resident("key.particle-clut",NativeMobyDependencyKind.Texture,dataOriginal,0x7BB40,32,dataFinal,0x7BB40);
        Aggregate("effects.native-debris-and-particles",NativeMobyDependencyKind.Effect,
            nodes.Where(n=>n.Id.StartsWith("effects.native-",StringComparison.Ordinal)||n.Id.StartsWith("key.particle-",StringComparison.Ordinal)).Select(n=>n.Id).ToArray());

        NativeMobyDependencyRead[] codeReads=[Read(donorOverlayOriginal,0x188719C-0x187E000,0x30,"setup"),
            Read(donorOverlayOriginal,0x1887330-0x187E000,0x70C,"state"),Read(donorOverlayOriginal,0x1881B64-0x187E000,0x434,"reward")];
        Add("behavior.bundle",NativeMobyDependencyKind.Behavior,["effects.native-debris-and-particles","sound.reward","sound.map.55"],codeReads,
            c=>TownSquareNativeLockedChestCandidateExporter.BuildHandlerPayload((o,n)=>o switch {
                0x188719C when n==0x30=>c.Read("setup"),0x1887330 when n==0x70C=>c.Read("state"),
                0x1881B64 when n==0x434=>c.Read("reward"),_=>throw new InvalidDataException("Undeclared native handler extraction.")}),overlayFinal,0xF494,0xBC0);
        foreach(var hook in new[]{((int)(0x8007DB80-0x8007AA38),0x08022A91u),((int)(0x8007DC4C-0x8007AA38),0x08022A96u)})
            Add("dispatch."+hook.Item1,NativeMobyDependencyKind.Behavior,["behavior.bundle"],[Read(overlayOriginal,hook.Item1,8)],
                c=>{byte[] b=c.Read("native");Put(b,0,hook.Item2);return b;},overlayFinal,hook.Item1,8);

        List<string> textureIds=[];
        var textureRegions=new[]{(0xE,192,192,0x4A2A,8,0,0,0x1028),(0xE,192,224,0x4C2A,8,0,32,0x1029),
            (0xF,0,160,0x5C2A,0x1D,96,192,0x102A),(0xF,0,192,0x5E2A,0x1F,128,128,0x102B)};
        for(int t=0;t<textureRegions.Length;t++)
        {
            var r=textureRegions[t];
            int[] from=TownSquareNativeLockedChestCandidateExporter.BuildTileOffsets((ushort)r.Item1,r.Item2,r.Item3);
            int[] to=TownSquareNativeLockedChestCandidateExporter.BuildTileOffsets((ushort)r.Item5,r.Item6,r.Item7);
            var slices=Enumerable.Range(0,32).Select(y=>(from[y*16],to[y*16],16)).Append((
                TownSquareNativeLockedChestCandidateExporter.BuildPaletteOffsets((ushort)r.Item4)[0],
                TownSquareNativeLockedChestCandidateExporter.BuildPaletteOffsets((ushort)r.Item8)[0],32));
            int part=0;
            foreach(var slice in slices)
            {
                int targetOffset=0x800+slice.Item2;
                if(data.AsSpan(targetOffset,slice.Item3).ContainsAnyExcept((byte)0)||textureOwnership.ProtectedRanges.Any(p=>p.Offset<slice.Item2+slice.Item3&&slice.Item2<p.Offset+p.Length))
                    throw new InvalidDataException("Native texture slice overlaps a live consumer or nonzero target.");
                string id=$"texture.{t}.{part++}";textureIds.Add(id);
                Add(id,NativeMobyDependencyKind.Texture,[],[Read(peaceOriginal,0x800+slice.Item1,slice.Item3)],c=>c.Read("native"),dataFinal,targetOffset,slice.Item3);
            }
        }
        Add("actor.ae",NativeMobyDependencyKind.ActorPackage,[..textureIds,"sound.chest-local54"],[Read(peaceOriginal,0x1B3978,0x1008)],c=>{
            byte[] b=c.Read("native");RequireWord(b,4,0xFFFFFF2F);
            if(TownSquareNativeLockedChestCandidateExporter.RebaseLockedChestDescriptors(b)!=146)throw new InvalidDataException("Actor face dependency grammar changed.");
            b[4]=0x36;return b;},dataFinal,0x1D0000,0x1008);
        Add("actor.registration",NativeMobyDependencyKind.ActorRegistration,["actor.ae"],[Read(dataOriginal,0xE4,4)],c=>c.Read("native"),dataFinal,0xE4,4,
            references:[new("actor-root",0,"actor.ae",0,4,0)]);
        Add("actor.class",NativeMobyDependencyKind.ActorRegistration,["actor.ae"],[Read(dataOriginal,0x19A,2)],c=>{byte[] b=c.Read("native");if(U16(b,0)!=0)throw new InvalidDataException("Actor class slot occupied.");BinaryPrimitives.WriteUInt16LittleEndian(b,0xAE);return b;},dataFinal,0x19A,2);
        Add("data.header",NativeMobyDependencyKind.RuntimeBuffer,[],[Read(dataOriginal,0,0x50)],c=>{
            byte[] b=c.Read("native");Put(b,20,0x5E800);for(int i=3;i<10;i++)if(U32(b,i*8)!=0)Put(b,i*8,U32(b,i*8)+0x1800);return b;},dataFinal,0,0x50);

        byte[] ReadNative(NativeByteExtent e)=>originals[e.Space].AsSpan((int)e.Offset,(int)e.ByteLength).ToArray();
        var sharedInstances=NativeMobyDependencyClosureCompiler.Compile(nodes,
            ["actor.registration","actor.class","behavior.bundle","key.rootless-rendering"],ReadNative);
        var instances=NativeTownSquareLockedChestInstantiation.Capture(peaceOriginal,sceneOriginal,sceneFinal,exeOriginal,
            [dataFinal,sceneFinal,overlayFinal,exeFinal],sharedInstances,ReadNative);
        var allocatedRows=instances.Allocated.Rows;
        NativeTownSquarePlacementPoint[] locations=NativeTownSquareClosurePlacement.Locations(recipe);
        NativeTownSquarePlacementPoint Location(NativeMobyDependencyBuildContext c,int instance)=>recipe==NativeTownSquareClosureRecipe.PropertiesAndSoundV2
            ?locations[instance]:NativeTownSquareClosurePlacement.Decode(c.DependencyPayload("placement.site"),instance);
        for(int i=0;i<allocatedRows.Count;i++)
        {
            int instance=i;var allocated=allocatedRows[i];
            string owner=allocated.OwnerId, props=owner+".props", row=owner+".row";
            // These native key/chest/reward blocks have no registered internal pointers.
            // Their integer owner fields are true-instance indices, not scene addresses.
            Add(props,NativeMobyDependencyKind.InstanceProperties,recipe==NativeTownSquareClosureRecipe.SpatiallySeparatedV3?["placement.site"]:[],[allocated.Source.Properties],c=>{
                byte[] b=allocated.RemapPrivateOwnerIndices(c.Read("native"));if(instance==0){if(b.Any(x=>x!=0))throw new InvalidDataException("Key initial props changed.");}
                if(instance>=2){var location=Location(c,instance);Put(b,4,(uint)location.X);Put(b,8,(uint)location.Y);Put(b,12,(uint)location.Z);}return b;
            },sceneFinal,(int)allocated.Properties.Offset,(int)allocated.Properties.ByteLength,owner);
            Add(row,NativeMobyDependencyKind.ObjectIndex,recipe==NativeTownSquareClosureRecipe.SpatiallySeparatedV3?[props,"placement.site"]:[props],[allocated.Source.Row],c=>{
                var location=Location(c,instance);byte[] b=c.Read("native");Put(b,0x0C,(uint)location.X);Put(b,0x10,(uint)location.Y);Put(b,0x14,(uint)location.Z);return b;
            },sceneFinal,(int)allocated.Row.Offset,(int)allocated.Row.ByteLength,owner,[allocated.PropertiesReference]);
            Aggregate(owner,NativeMobyDependencyKind.Instance,instance==0?[row,props,"key.rootless-rendering"]:
                instance==1?[row,props,"actor.registration","actor.class","behavior.bundle",allocatedRows[0].OwnerId]:[row,props,"behavior.bundle",allocatedRows[1].OwnerId]);
        }
        Add("scene.count",NativeMobyDependencyKind.ObjectIndex,[],[Read(sceneOriginal,0x16C,4)],c=>Word((uint)instances.Allocated.FinalRowCount),sceneFinal,0x16C,4);
        Add("scene.properties-length",NativeMobyDependencyKind.InstanceProperties,[],[Read(sceneOriginal,0x4E38,4)],c=>Word(checked(U32(c.Read("native"),0)+(uint)instances.Allocated.PropertyBytesAllocated)),sceneFinal,0x4E38,4);
        int finalFixupBytes=checked(0x208+allocatedRows.Count*4);
        Add("scene.fixups",NativeMobyDependencyKind.SceneFixup,allocatedRows.Select(r=>r.OwnerId).ToArray(),[Read(sceneOriginal,OriginalFixup,0x208)],c=>{
            byte[] before=c.Read("native"),b=new byte[finalFixupBytes];before.CopyTo(b,0);Put(b,0,checked(U32(before,0)+(uint)allocatedRows.Count));
            for(int i=0;i<allocatedRows.Count;i++)Put(b,0x208+i*4,(uint)allocatedRows[i].Row.Offset);return b;
        },sceneFinal,FinalFixup,finalFixupBytes);
        Add("executable.copy-buffer",NativeMobyDependencyKind.RuntimeBuffer,["behavior.bundle"],[Read(exeOriginal,0x4B098,8)],c=>{byte[] b=c.Read("native");RequireWord(b,4,0x24429ECC);Put(b,4,0x2442B238);return b;},exeFinal,0x4B098,8);
        Add("executable.treasure",NativeMobyDependencyKind.ObjectIndex,[],[Read(exeOriginal,0x5FC3E,2)],c=>new byte[]{210,0},exeFinal,0x5FC3E,2);
        Add("executable.navigation",NativeMobyDependencyKind.NativeRoutine,[],[Read(exeOriginal,0x1E034,8)],c=>new byte[]{0x1C,0x06,0x84,0xAF,0x88,0x06,0x80,0xAF},exeFinal,0x1E034,8);

        // Source-to-final preservation maps cover every untouched byte, not guessed zero ownership.
        void PreserveComplement(string id,NativeAddressSpace from,NativeAddressSpace to,params (int From,int To,int Length)[] maps)
        {
            List<NativeMobyDependencyRead> rs=[];List<NativeMobyDependencyPreservation> ps=[];
            foreach(var map in maps)
            {
                int cursor=map.To,end=map.To+map.Length;
                foreach(var cut in reservations.Where(r=>r.Space==to&&r.Offset<end&&cursor<r.Offset+r.Length).OrderBy(r=>r.Offset))
                {
                    if(cut.Offset>cursor){string name="span."+rs.Count;rs.Add(Read(from,map.From+cursor-map.To,cut.Offset-cursor,name));ps.Add(new(name,0,new(to,cursor,cut.Offset-cursor)));}
                    cursor=Math.Max(cursor,cut.Offset+cut.Length);
                }
                if(cursor<end){string name="span."+rs.Count;rs.Add(Read(from,map.From+cursor-map.To,end-cursor,name));ps.Add(new(name,0,new(to,cursor,end-cursor)));}
            }
            nodes.Add(new(id,NativeMobyDependencyKind.NativeRoutine,NativeMobyDependencyResolution.DestinationResident,
                NativeMobyDependencySharing.Immutable,SourceSha256,[],rs,Preserved:ps));
        }
        PreserveComplement("scene.preserved",sceneOriginal,sceneFinal,(0,0,PropertiesEnd),(PropertiesEnd,PropertiesEnd+PropertiesGrowth,OriginalFixup-PropertiesEnd));
        PreserveComplement("data.preserved",dataOriginal,dataFinal,(0,0,OriginalScene),(OriginalScene+0x8800,FinalScene+0x8800,data.Length-OriginalScene-0x8800));
        PreserveComplement("overlay.preserved",overlayOriginal,overlayFinal,(0,0,0xF494));
        PreserveComplement("executable.preserved",exeOriginal,exeFinal,(0,0,executable.Length));
        Aggregate("bundle",NativeMobyDependencyKind.Bundle,nodes.Select(n=>n.Id).ToArray());
        NativeMobyDependencyClosurePlan graph=NativeMobyDependencyClosureCompiler.Compile(nodes,["bundle"],e=>originals[e.Space].AsSpan((int)e.Offset,(int)e.ByteLength).ToArray());
        instances.Allocated.RequireSharedBinding(graph);
        byte[] Compose(NativeAddressSpace space)
        {
            byte[] result=new byte[space.ByteLength];
            foreach(var n in graph.Nodes)foreach(var p in n.Preserved.Where(p=>p.Final.Space==space))
            {var r=n.Reads.Single(r=>r.Id==p.ReadId);originals[r.Extent.Space].AsSpan((int)r.Extent.Offset+p.ReadOffset,(int)p.Final.ByteLength).CopyTo(result.AsSpan((int)p.Final.Offset));}
            foreach(var install in graph.Installs.Where(i=>i.Destination.Space==space))graph.CopyPayload(install.NodeId).CopyTo(result,(int)install.Destination.Offset);
            return result;
        }
        byte[] finalData=Compose(dataFinal),finalScene=Compose(sceneFinal);finalScene.CopyTo(finalData,FinalScene);
        byte[] finalOverlay=Compose(overlayFinal),finalExe=Compose(exeFinal);
        // Independent old-recipe comparison is a regression oracle, never the implementation input.
        byte[] ReadWad(long offset,int length)
        {
            foreach(var item in new[]{(TownDataWad,data),(PeaceDataWad,peace),(TownOverlayWad,overlay),(0x187E000L,donorOverlay)})
                if(offset>=item.Item1&&offset+length<=item.Item1+item.Item2.Length)return item.Item2.AsSpan((int)(offset-item.Item1),length).ToArray();
            throw new InvalidDataException("Legacy oracle requested an undeclared source domain.");
        }
        var legacy=TownSquareNativeLockedChestCandidateExporter.BuildFinalDataEntry(ReadWad,[]);
        RequireHash(legacy.Entry,"1c187183be611138b1058750ab099de71d8e4f9beb48c39a604068f0ef11bd48");
        RequireHash(legacy.RebasedPackage,"077d08948f51220f31796ded65463b0e07dc372b9bb76f1bde491095dfc93c89");
        if(!finalOverlay.SequenceEqual(TownSquareNativeLockedChestCandidateExporter.BuildFinalOverlay(overlay,graph.CopyPayload("behavior.bundle")))||
            !finalExe.SequenceEqual(TownSquareNativeLockedChestCandidateExporter.BuildFinalExecutable(executable,true)))
            throw new InvalidDataException("Graph-native overlay/executable diverged from the independent native relocation oracle.");
        for(int i=0;i<finalData.Length;i++)
            if(finalData[i]!=legacy.Entry[i]&&!(i>=FinalScene&&i<FinalScene+0x8800)&&i!=0x1D0004)
                throw new InvalidDataException($"Graph-native data diverged outside corrected properties/sound at 0x{i:X}.");
        if(!scene.AsSpan(PropertiesEnd,OriginalFixup-PropertiesEnd).SequenceEqual(finalScene.AsSpan(PropertiesEnd+PropertiesGrowth,OriginalFixup-PropertiesEnd)))
            throw new InvalidDataException("Properties growth altered native pod/collision components.");
        var placement=recipe==NativeTownSquareClosureRecipe.SpatiallySeparatedV3?NativeTownSquareClosurePlacement.CaptureCertificate(data,executable,recipe):null;
        if(placement!=null&&!graph.CopyPayload("placement.site").SequenceEqual(NativeTownSquareClosurePlacement.Encode(placement.Locations)))
            throw new InvalidDataException("Graph-driven placement does not match independently recaptured native site certificate.");
        return new(sourcePath,layout,header,graph,finalData,finalOverlay,finalExe,Hash(legacy.Entry),recipe,placement,instances);
    }
    internal static string Hash(ReadOnlySpan<byte> bytes,bool alreadyDigest=false)=>Convert.ToHexStringLower(alreadyDigest?bytes:SHA256.HashData(bytes));
    private static uint U32(byte[] bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset,4));
    private static ushort U16(byte[] bytes,int offset)=>BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset,2));
    private static void Put(byte[] bytes,int offset,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset,4),value);
    private static byte[] Word(uint value){byte[] b=new byte[4];Put(b,0,value);return b;}
    private static void RequireWord(byte[] bytes,int offset,uint value){if(U32(bytes,offset)!=value)throw new InvalidDataException($"Native word changed at 0x{offset:X}.");}
    private static void RequireHash(ReadOnlySpan<byte> bytes,string expected){if(Hash(bytes)!=expected)throw new InvalidDataException("Native source/component hash changed.");}
}

internal sealed record NativeTownSquareLockedChestClosure(string SourcePath,DiscLayout Layout,byte[] OriginalWadHeader,
    NativeMobyDependencyClosurePlan Graph,byte[] Data,byte[] Overlay,byte[] Executable,string LegacyDataSha256,
    NativeTownSquareClosureRecipe Recipe,NativeTownSquarePlacementCertificate? Placement,NativeTownSquareLockedChestInstanceCapture Instances);

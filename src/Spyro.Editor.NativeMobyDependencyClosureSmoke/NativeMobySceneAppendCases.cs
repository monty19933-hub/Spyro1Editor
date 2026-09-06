using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

internal static class NativeMobySceneAppendCases
{
    internal static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        var f=new Fixture();var plan=f.Compile();byte[] output=plan.CopyScene();
        check(plan.PlacedCountBefore==1&&plan.PlacedCountAfter==2&&plan.FixupCountAfter==4,"Scene append lost placed/fixup accounting.");
        check(plan.DynamicCapacityBefore==4&&plan.DynamicCapacityAfter==3&&output.Length==f.Scene.Length,"Scene append hid its shared dynamic-arena cost or grew a sector.");
        check(!plan.BehaviorClosureProven&&!plan.RuntimeFitProven&&!plan.PublicationApproved,"Private scene ownership became native behavior/publication authority.");
        int row=f.Source.RowTableOffset+0x58,props=f.Source.PropertiesEnd;
        check(Word(output,row)==props&&Word(output,props)==props+0x28&&plan.Graph.References.Count==2,"Private self/row pointer binding did not resolve in the canonical Scene domain.");
        check(output.AsSpan(f.Source.RowTableOffset,0x58).SequenceEqual(f.Scene.AsSpan(f.Source.RowTableOffset,0x58)),"Original native row changed.");
        check(output.AsSpan(f.Source.PropertiesOffset+4,f.Source.PropertiesEnd-f.Source.PropertiesOffset-4).SequenceEqual(f.Scene.AsSpan(f.Source.PropertiesOffset+4,f.Source.PropertiesEnd-f.Source.PropertiesOffset-4)),"Original native properties changed.");
        check(output.AsSpan(props+0x50,f.Source.FixupOffset-props).SequenceEqual(f.Scene.AsSpan(props,f.Source.FixupOffset-props)),"Native pod/collision suffix was not preserved at its typed shifted extent.");
        plan.Verify(f.Scene,output);check(true,"Exact scene inverse/readback failed.");
        byte[] copy=plan.CopyScene();copy[0]^=1;check(plan.CopyScene()[0]!=copy[0],"Scene output aliases detached caller bytes.");
        var retry=plan.Allocation.Allocate([f.Request.Instances]);
        check(retry.Rows.Single().Row==plan.Allocation.Rows.Single().Row&&retry.PropertyBytesAllocated==0x50,"Identical scene request retry consumed another private owner.");
        var two=f.Compile(requests:[f.Request,f.Request with { Instances=f.Request.Instances with { Id="second" } }]);
        var rows=two.Allocation.Rows.OrderBy(r=>r.Index).ToArray();byte[] twoBytes=two.CopyScene();
        check(rows.Length==2&&rows[0].Properties.EndExclusive==rows[1].Properties.Offset&&two.Graph.Nodes.Count(n=>n.Id=="allocator-code")==1,
            "Two scene instances did not share immutable evidence while isolating mutable properties.");
        foreach(var owned in rows)check(Word(twoBytes,(int)owned.Row.Offset)==owned.Properties.Offset&&Word(twoBytes,(int)owned.Properties.Offset)==owned.Properties.Offset+0x28,
            "Two scene instances alias an internal or row pointer.");
        check(two.FixupCountAfter==6&&two.PlacedCountAfter==3,"Two-scene private fixup accounting changed.");
        var sharedScene=f.WithSharedSceneInstall();reject(()=>f.Compile(requests:[sharedScene]),"preserved native scene");
        reject(()=>f.Compile(requests:[f.WithWrongSourceKind()]),"original Scene");
        check(f.Compile().FinalSha256==plan.FinalSha256&&f.Compile().Graph.Fingerprint==plan.Graph.Fingerprint,"Same scene/source request is not deterministic.");
        byte[] corrupt=output.ToArray();corrupt[0]^=1;reject(()=>plan.Verify(f.Scene,corrupt),"readback");
        corrupt=f.Scene.ToArray();corrupt[0]^=1;reject(()=>plan.Verify(corrupt,output),"source");
        reject(()=>NativeMobySceneAppendSource.Capture(f.Original,Hash([0]),f.Read),"preimage");
        reject(()=>f.Compile(final:new(NativeAddressKind.LevelData,"wrong-kind",NativeAddressRevision.Final,f.Scene.Length)),"Scene");
        reject(()=>f.Compile(domains:[]),"canonical");
        var bad=f.Request with { InternalReferences=[new("wizard",0,0x50)] };
        reject(()=>f.Compile(requests:[bad]),"internal pointer");
        bad=f.Request with { InternalReferences=[new("wizard",0,0x28),new("wizard",0,0x28)] };
        reject(()=>f.Compile(requests:[bad]),"internal pointer");
        bad=f.Request with { InternalReferences=[new("unknown",0,0x28)] };
        reject(()=>f.Compile(requests:[bad]),"role");
        byte[] stale=f.Donor.ToArray();f.Donor[0x84]^=1;
        reject(()=>f.Compile(),"preimage");stale.CopyTo(f.Donor,0);
        var occupied=new Fixture();occupied.Scene[occupied.Source.RowTableOffset+0x58]=1;
        occupied.Recapture();reject(()=>occupied.Compile(),"dynamic row");
        var tail=new Fixture();tail.Scene[^1]=1;
        reject(()=>tail.Recapture(),"tail");
        var moving=new Fixture();Put(moving.Scene,moving.Source.RowTableOffset,(uint)moving.Source.PropertiesEnd);
        reject(()=>moving.Recapture(),"target");
        var duplicate=new Fixture();Put(duplicate.Scene,duplicate.Source.FixupOffset+8,(uint)duplicate.Source.RowTableOffset);
        reject(()=>duplicate.Recapture(),"duplicate");
        var unknown=new Fixture();Put(unknown.Scene,unknown.Source.FixupOffset+8,(uint)(unknown.Source.RowTableOffset+4));
        reject(()=>unknown.Recapture(),"field");
        var tiny=new Fixture(arenaBytes:0x58);reject(()=>tiny.Compile(requests:[tiny.Request,tiny.Request with { Instances=tiny.Request.Instances with { Id="second" } }]),"capacity");
        check(tiny.Compile().DynamicCapacityAfter==0,"Failed batch consumed a subsequent valid row reservation.");
        var last=new Fixture(placed:255);check(last.Compile().PlacedCountAfter==256,"Last actual byte-sized owner was refused.");
        var overflow=new Fixture(placed:256);reject(()=>overflow.Compile(),"byte");
        IEnumerable<NativeMobySceneAppendRequest> Infinite(){while(true)yield return f.Request;}
        reject(()=>f.Compile(requests:Infinite()),"request budget");
        Console.WriteLine("NATIVE_MOBY_SCENE_APPEND_PRIVATE_POINTERS_POOL_AND_REFUSALS_OK");
    }

    internal static void Source(string image,Action<bool,string> check,Action<Action,string> reject)
    {
        var native=NativeDarkHollowWizardScenePlan.Capture(image);byte[] before=native.CopyOriginalScene(),after=native.CopyScene();
        check(native.PlacedCountBefore==128&&native.PlacedCountAfter==129&&native.FixupCountAfter==149,"Exact Dark native count/fixup obligation changed.");
        check(native.DynamicCapacityBefore==91&&native.DynamicCapacityAfter==90&&native.SceneByteLength==0x11800,"Exact Dark scene/pool native accounting changed.");
        check(native.FinalSceneSha256=="9ac99fdd150133a75aac8298da4b738dcbcdd12a1ab649088f53c300bd1a37c4"&&
            native.FinalLevelSha256=="6fd296b8f56f70f1d993596fe262076e3b2674865c24a1070543979334e56e38",
            "Scene/level output differs from the independent source-native Node splice oracle.");
        check(!native.BehaviorClosureProven&&!native.RuntimeFitProven&&!native.PublicationApproved&&!native.PlacementApproved,"Diagnostic source-owned scene became an approved enemy or placement.");
        check(Word(after,0xB838)==0xEED4&&Word(after,0xEED4)==0xEEFC&&Word(after,0xE038)==0xEEC,"Exact Dark private pointers/component length changed.");
        for(int i=0;i<128;i++)check(before.AsSpan(0x8C38+i*0x58,0x58).SequenceEqual(after.AsSpan(0x8C38+i*0x58,0x58)),"Dark native row was replaced or altered.");
        check(before.AsSpan(0xE03C,0xE98).SequenceEqual(after.AsSpan(0xE03C,0xE98)),"Dark original mutable properties changed.");
        check(before.AsSpan(0xEED4,0x201C).SequenceEqual(after.AsSpan(0xEF24,0x201C)),"Dark original pod/collision components changed.");
        check(before.AsSpan(0x10EF4,147*4).SequenceEqual(after.AsSpan(0x10F44,147*4)),"Dark old fixup order/values changed.");
        var fields=Enumerable.Range(0,149).Select(i=>(int)Word(after,0x10F44+i*4)).ToArray();
        check(fields.Distinct().Count()==149&&fields[^2]==0xB838&&fields[^1]==0xEED4,"Dark new fixup field order or uniqueness changed.");
        foreach(int field in fields)check(field%4==0&&field>=0&&field+4<=0xEF24&&Word(after,field)>=0xE03C&&Word(after,field)<0xEF24,"Dark native fixup targets unowned/padding bytes.");
        native.VerifyOriginalAndFinal(before,after);check(true,"Dark source/final readback failed.");
        byte[] stale=before.ToArray();stale[0]^=1;reject(()=>native.VerifyOriginalAndFinal(stale,after),"source");
        byte[] corrupt=after.ToArray();corrupt[0xB838]^=1;reject(()=>native.VerifyOriginalAndFinal(before,corrupt),"readback");
        byte[] originalLevel=native.CopyOriginalLevel(),finalLevel=native.CopyScenePatchedLevel();
        check(originalLevel.AsSpan(0,0x194000).SequenceEqual(finalLevel.AsSpan(0,0x194000))&&originalLevel.AsSpan(0x1A5800).SequenceEqual(finalLevel.AsSpan(0x1A5800)),"Scene stage changed non-scene level bytes.");
        var payload=native.CopyOriginalAddressPayload(51);check(payload.SubfileIndex==3&&payload.OriginalWadOffset==0x1095000&&payload.ModelBlockOffset==0&&payload.OriginalLength==0x11800,"Scene payload used an entry anchor or final address instead of its actual original source owner.");
        int modelOffset=(int)Word(originalLevel,16),modelLength=(int)Word(originalLevel,20);
        NativeSkyRelocationPayload modelInsert=new(52,37,0xF01000+modelOffset+modelLength,14,modelLength,0,new byte[0x804],2,false);
        var growth=new NativeLevelDataGrowthLayout(originalLevel.AsSpan(0,0x800),originalLevel.Length,[modelInsert,payload]);
        byte[] combined=growth.Expand(originalLevel);int moved=(int)Word(combined,24);
        check(moved==0x195000&&combined.AsSpan(moved,0x11800).SequenceEqual(after),"Original-address scene replacement did not compose after sector-rounded model growth.");
        payload.Bytes[0]^=1;check(native.CopyOriginalAddressPayload(51).Bytes[0]==after[0],"Scene payload exposes mutable plan storage.");
        var sounds=NativeGlobalSoundSource.CaptureAsync(image).GetAwaiter().GetResult();
        var sound=sounds.CompileMergeAsync(image,33,12,[new(0x1F,NativeGlobalSoundMappingPolicy.ExplicitRebindUnresolvedConsumers)]).GetAwaiter().GetResult();
        var players=NativePlayerAnimationSource.CaptureAsync(image).GetAwaiter().GetResult();
        var player=players.CompileMergeAsync(image,33,12,[8]).GetAwaiter().GetResult();
        check(sound.OriginalDataSha256==native.SourceDataSha256&&player.OriginalDataSha256==native.SourceDataSha256,
            "Source-issued sound/player/scene plans do not share the exact original data owner.");
        NativeSkyRelocationPayload[] nativePayloads=[..sound.CopyInsertionPayloads(),player.CopyInsertionPayload()! with { PatchIndex=10 }];
        byte[] baseline=Compose(nativePayloads);
        check(Hash(baseline)=="3965dd80a3e3492348798554e1658921330f2e32ffdc76d455adc4425791a10d",
            "Source sound/player composition differs from its independently certified baseline.");
        byte[] full=Compose([..nativePayloads,native.CopyOriginalAddressPayload(51)]);int actualScene=(int)Word(full,0x18);
        check(actualScene==0x19A800&&full.AsSpan(actualScene,0x11800).SequenceEqual(after)&&
            full.AsSpan(0,actualScene).SequenceEqual(baseline.AsSpan(0,actualScene))&&
            full.AsSpan(actualScene+0x11800).SequenceEqual(baseline.AsSpan(actualScene+0x11800)),
            "Scene append did not preserve the actual native bank/data/player composition complement.");
        check(Compose([native.CopyOriginalAddressPayload(51),..nativePayloads.Reverse()]).AsSpan().SequenceEqual(full),
            "Four-owner source-native composition depends on payload declaration order.");
        check(!sound.CompleteConsumerOwnershipProven&&!player.CompleteAnimationConsumersProven&&!native.BehaviorClosureProven,
            "Combined detached resources acquired missing consumer/behavior approval.");
        Console.WriteLine($"NATIVE_DARK_WIZARD_SCENE_SOUND_PLAYER_COMPOSITION_OK: {Hash(full)}; consumers/runtime/publication=false");
        Console.WriteLine($"NATIVE_DARK_WIZARD_SCENE_SOURCE_OK: scene {native.FinalSceneSha256}; level {native.FinalLevelSha256}; graph {native.GraphFingerprint}; pool 91->90; publication=false");
        return;
        byte[] Compose(IReadOnlyList<NativeSkyRelocationPayload> payloads)
        {
            var layout=new NativeLevelDataGrowthLayout(originalLevel.AsSpan(0,0x800),originalLevel.Length,payloads);
            byte[] final=layout.Expand(originalLevel);
            foreach(var field in sound.CopyNonHeaderMutations())
            {
                var owner=layout.Subfiles.Single(s=>field.DataOffset>=s.OriginalOffset&&field.DataOffset+(long)field.ByteLength<=s.OriginalOffset+s.OriginalLength);
                int relative=field.DataOffset-owner.OriginalOffset;
                if(owner.Payloads.Any(p=>p.OriginalLength!=0||p.ModelBlockOffset>relative&&p.ModelBlockOffset<relative+field.ByteLength))
                    throw new InvalidDataException("Source-native composition mutation enters an unsupported replacement or split field.");
                int mapped=owner.FinalOffset+relative+owner.Payloads.Where(p=>p.ModelBlockOffset<=relative).Sum(p=>p.Bytes.Length);
                uint beforeValue=field.ByteLength==1?final[mapped]:Word(final,mapped);
                if(beforeValue!=field.Before)throw new InvalidDataException("Source-native composition mutation preimage changed.");
                if(field.ByteLength==1)final[mapped]=checked((byte)field.After);else Put(final,mapped,field.After);
            }
            return final;
        }
    }

    private sealed class Fixture
    {
        internal byte[] Scene,Donor=new byte[0x100],Executable=new byte[64];
        internal NativeAddressSpace Original,Final,DonorSpace,Exe;
        internal NativeMobySceneAppendSource Source;
        internal NativeMobySceneAppendRequest Request;
        internal Fixture(int placed=1,int arenaBytes=0x200)
        {
            int moby=0xC0,mobyLength=8+placed*0x58+arenaBytes,props=moby+mobyLength,fix=props+0x44+16;
            Scene=new byte[(fix+4+(placed+1)*4+0x100+0x7FF)&~0x7FF];
            for(int p=0x88;p<moby;p+=8)Put(Scene,p,8);
            Put(Scene,moby,(uint)mobyLength);Put(Scene,moby+4,(uint)placed);Put(Scene,props,0x44);
            Put(Scene,props+4,(uint)(props+8));Put(Scene,props+0x44,8);Put(Scene,props+0x4C,8);
            Put(Scene,fix,(uint)(placed+1));
            for(int i=0;i<placed;i++){Put(Scene,moby+8+i*0x58,(uint)(props+4));Put(Scene,fix+4+i*4,(uint)(moby+8+i*0x58));}
            Put(Scene,fix+4+placed*4,(uint)(props+4));
            Original=new(NativeAddressKind.Scene,"fixture-scene",NativeAddressRevision.Original,Scene.Length);
            Final=new(NativeAddressKind.Scene,"fixture-final",NativeAddressRevision.Final,Scene.Length);
            DonorSpace=new(NativeAddressKind.Scene,"fixture-donor",NativeAddressRevision.Original,Donor.Length);
            Exe=new(NativeAddressKind.ExecutableFile,"fixture-exe",NativeAddressRevision.Original,Executable.Length);
            Put(Donor,0,0x80);BinaryPrimitives.WriteUInt16LittleEndian(Donor.AsSpan(0x36),0x11B);Put(Donor,0x80,0xA8);
            Source=NativeMobySceneAppendSource.Capture(Original,Hash(Scene),Read);
            NativeMobyDependencyRead R(NativeAddressSpace space,int offset,int length)=>new("native",new(space,offset,length),Hash(Read(new(space,offset,length))));
            var code=new NativeMobyDependencyNode("allocator-code",NativeMobyDependencyKind.NativeRoutine,NativeMobyDependencyResolution.ImportFromSource,NativeMobyDependencySharing.Immutable,Hash(Executable),[],[R(Exe,0,64)],c=>new(c.Read("native"),[],[]));
            var shared=NativeMobyDependencyClosureCompiler.Compile([code],[code.Id],Read);
            var template=NativeMobyDependencyInstanceTemplate.Capture("fixture-code-owned",[new("wizard",6,0x11B,R(DonorSpace,0,0x58),R(DonorSpace,0x80,0x50),0,[],true)],[],shared,Read);
            Request=new(new("wizard",template),[new("wizard",0,0x28)]);
        }
        internal byte[] Read(NativeByteExtent extent)
        {var b=extent.Space==Original?Scene:extent.Space==DonorSpace?Donor:extent.Space==Exe?Executable:throw new InvalidDataException("Unbound fixture domain.");return b.AsSpan((int)extent.Offset,(int)extent.ByteLength).ToArray();}
        internal void Recapture()=>Source=NativeMobySceneAppendSource.Capture(Original,Hash(Scene),Read);
        internal NativeMobySceneAppendRequest WithSharedSceneInstall()
        {
            var read=new NativeMobyDependencyRead("native",new(Exe,0,4),Hash(Executable.AsSpan(0,4)));
            var node=new NativeMobyDependencyNode("hostile-scene-shared",NativeMobyDependencyKind.NativeRoutine,
                NativeMobyDependencyResolution.ImportFromSource,NativeMobyDependencySharing.Immutable,read.ExpectedSha256,[],[read],
                c=>{var bytes=c.Read("native");bytes[0]=1;return new(bytes,[new("overwrite",new(Final,Source.RowTableOffset+0x34,4))],[]);});
            var graph=NativeMobyDependencyClosureCompiler.Compile([node],[node.Id],Read);
            var template=NativeMobyDependencyInstanceTemplate.Capture("fixture-shared-scene-write",Request.Instances.Template.Rows,[],graph,Read);
            return Request with { Instances=new("hostile",template) };
        }
        internal NativeMobySceneAppendRequest WithWrongSourceKind()
        {
            var foreign=new NativeAddressSpace(NativeAddressKind.LevelData,"same-bytes-wrong-source-domain",NativeAddressRevision.Original,Donor.Length);
            var old=Request.Instances.Template;var row=old.Rows.Single();
            var schema=row with { Row=row.Row with { Extent=new(foreign,0,0x58) },Properties=row.Properties with { Extent=new(foreign,0x80,0x50) } };
            var template=NativeMobyDependencyInstanceTemplate.Capture("wrong-source-kind",[schema],[],old.Shared,
                e=>e.Space==foreign?Donor.AsSpan((int)e.Offset,(int)e.ByteLength).ToArray():Read(e));
            return Request with { Instances=new("foreign",template) };
        }
        internal NativeMobySceneAppendPlan Compile(NativeAddressSpace? final=null,IReadOnlyList<NativeAddressSpace>? domains=null,IEnumerable<NativeMobySceneAppendRequest>? requests=null)=>
            NativeMobySceneAppendPlan.Compile(Source,final??Final,domains??[Final],Exe,requests??[Request],Read);
    }
    private static string Hash(ReadOnlySpan<byte>b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static uint Word(ReadOnlySpan<byte>b,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(b[offset..]);
    private static void Put(Span<byte>b,int offset,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(b[offset..],value);
}

using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

internal static class NativeMobyDependencyInstantiationCases
{
    internal static void Run(Action<bool,string> check, Action<Action,string> reject)
    {
        byte[] bytes = new byte[1024], exeBytes = Enumerable.Range(0,256).Select(i=>(byte)i).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64),512);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(64+0x36),0x135);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(160),528);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(160+0x36),0xD);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(528),7);
        for(int i=0;i<32;i++)bytes[800+i]=(byte)(i+1);
        var original = new NativeAddressSpace(NativeAddressKind.LevelData,"fixture-source",NativeAddressRevision.Original,bytes.Length);
        var exe = new NativeAddressSpace(NativeAddressKind.ExecutableFile,"fixture-exe",NativeAddressRevision.Original,exeBytes.Length);
        var scene = new NativeAddressSpace(NativeAddressKind.Scene,"fixture-scene",NativeAddressRevision.Final,32768);
        var assets = new NativeAddressSpace(NativeAddressKind.LevelData,"fixture-assets",NativeAddressRevision.Final,1024);
        byte[] Read(NativeByteExtent e)
        {
            var b=e.Space==original?bytes:e.Space==exe?exeBytes:throw new InvalidDataException("Unbound native source address domain.");
            return b.AsSpan((int)e.Offset,(int)e.ByteLength).ToArray();
        }
        NativeMobyDependencyRead R(NativeAddressSpace s,int o,int n,string id="native") => new(id,new(s,o,n),Hash(Read(new(s,o,n))));
        NativeMobyDependencyPrivateSchema[] roles = [
            new("actor",7,0x135,R(original,64,0x58),R(original,512,16),0,[],true),
            new("reward",8,0xD,R(original,160,0x58),R(original,528,16),0,[new(0,0)],true)
        ];
        NativeMobyDependencyNode SharedNode(string id="shared.actor",int source=800,int offset=128,NativeAddressSpace? destination=null) =>
            new(id,NativeMobyDependencyKind.ActorPackage,NativeMobyDependencyResolution.ImportFromSource,NativeMobyDependencySharing.Immutable,
                Hash(bytes),[],[R(original,source,8)],c=>new(c.Read("native"),[new("install",new(destination??assets,offset,8))],[]));
        NativeMobyDependencyClosurePlan CloseShared(NativeMobyDependencyNode node) => NativeMobyDependencyClosureCompiler.Compile([node],[node.Id],Read);
        var sharedNode=SharedNode();var shared=CloseShared(sharedNode);
        NativeMobyDependencyInstanceTemplate Template(string policy="fixture-relocation-v1",NativeMobyDependencyClosurePlan? resources=null,
            NativeMobyDependencyMutableGlobal[]? globals=null,NativeMobyDependencyPrivateSchema[]? rows=null) =>
            NativeMobyDependencyInstanceTemplate.Capture(policy,rows??roles,globals??[],resources??shared,Read);
        var template=Template();
        NativeMobyDependencyInstantiationPlan Empty(int first=10,int count=8,int propsBytes=256) =>
            NativeMobyDependencyInstantiationPlan.Create(new(scene,0x100+first*0x58,count*0x58),new(scene,30000,propsBytes),
                0x100,first,[scene,assets],exe);
        var empty=Empty();var plan=empty.Allocate([new("b",template),new("a",template)]);
        check(empty.Rows.Count==0&&plan.Rows.Count==4&&plan.FinalRowCount==14&&plan.PropertyBytesAllocated==64,
            "Transactional native allocation mutated its seed or allocated wrong counts.");
        check(plan.Families.Select(f=>f.Id).SequenceEqual(new[]{"a","b"})&&plan.Rows.Select(r=>r.Index).SequenceEqual(new[]{10,11,12,13}),
            "Native allocation request ordering is not reproducible.");
        check(plan.SharedNodeCount==1,"Two independent families duplicated their immutable native binding.");
        check(plan.Rows.Select(r=>r.Properties.Offset).SequenceEqual(new long[]{30000,30016,30032,30048}),"Private native properties alias or contain an allocation hole.");
        byte[] firstProps=plan.Rows[1].RemapPrivateOwnerIndices(Read(roles[1].Properties.Extent));
        byte[] secondProps=plan.Rows[3].RemapPrivateOwnerIndices(Read(roles[1].Properties.Extent));
        check(BinaryPrimitives.ReadUInt32LittleEndian(firstProps)==10&&BinaryPrimitives.ReadUInt32LittleEndian(secondProps)==12,
            "Native owner-index remap crossed family boundaries.");
        firstProps[0]=255;
        check(secondProps[0]==12&&plan.Rows[1].RemapPrivateOwnerIndices(Read(roles[1].Properties.Extent))[0]==10,
            "Private remap results alias one another or cached source storage.");
        reject(()=>plan.Rows[1].RemapPrivateOwnerIndices(new byte[16]),"source preimage");

        NativeMobyDependencyClosurePlan Materialize(NativeMobyDependencyInstantiationPlan allocation)
        {
            List<NativeMobyDependencyNode> nodes=[sharedNode];
            foreach(var row in allocation.Rows)
            {
                string owner=row.OwnerId,props=owner+".props",record=owner+".row";
                nodes.Add(new(props,NativeMobyDependencyKind.InstanceProperties,NativeMobyDependencyResolution.ImportFromSource,
                    NativeMobyDependencySharing.PrivateInstance,Hash(bytes),[],[row.Source.Properties],
                    c=>new(row.RemapPrivateOwnerIndices(c.Read("native")),[new("install",row.Properties)],[]),owner));
                nodes.Add(new(record,NativeMobyDependencyKind.ObjectIndex,NativeMobyDependencyResolution.ImportFromSource,
                    NativeMobyDependencySharing.PrivateInstance,Hash(bytes),[props],[row.Source.Row],
                    c=>new(c.Read("native"),[new("install",row.Row)],[row.PropertiesReference]),owner));
                nodes.Add(new(owner,NativeMobyDependencyKind.Instance,NativeMobyDependencyResolution.Aggregate,
                    NativeMobyDependencySharing.Immutable,Hash(bytes),[record,props,sharedNode.Id],[]));
            }
            var graph=NativeMobyDependencyClosureCompiler.Compile(nodes,allocation.Rows.Select(r=>r.OwnerId),Read);
            allocation.RequireSharedBinding(graph);return graph;
        }
        var graph=Materialize(plan);
        check(graph.Installs.Count==9&&graph.References.Count==4&&graph.Nodes.Count(n=>n.Id=="shared.actor")==1,
            "Actual graph did not share one model across four independently installed native rows/properties.");
        foreach(var row in plan.Rows)
        {
            check(BinaryPrimitives.ReadUInt32LittleEndian(graph.CopyPayload(row.OwnerId+".row"))==row.Properties.Offset,
                "Actual graph property pointer does not use allocated Scene address.");
            check(graph.Installs.Single(i=>i.NodeId==row.OwnerId+".props").Destination==row.Properties,
                "Actual private graph install diverged from allocation.");
        }
        var repeated=plan.Allocate([new("a",Template()),new("b",template)]);
        check(Materialize(repeated).Fingerprint==graph.Fingerprint&&repeated.Rows.Count==4,"Identical retry allocated duplicate instances/resources or changed bytes.");
        check(Materialize(empty.Allocate([new("a",template),new("b",template)])).Fingerprint==graph.Fingerprint,"Batch order changed graph bytes/identity.");
        var first=Empty(count:4).Allocate([new("a",template)]);
        reject(()=>first.Allocate([new("b",template),new("c",template)]),"capacity");
        var retried=first.Allocate([new("b",template)]);
        check(first.Rows.Count==2&&retried.Rows.Select(r=>r.Index).SequenceEqual(new[]{10,11,12,13}),
            "Failed later batch item consumed earlier private reservations or owner indices.");
        check(retried.SharedNodeCount==1,"Failed batch polluted shared native-resource reuse.");
        reject(()=>plan.Allocate([new("a",Template("another-relocation-policy"))]),"Repeated native identity");
        reject(()=>empty.Allocate([new("a",template),new("a",template)]),"unique");
        reject(()=>empty.Allocate([new("a",template,NativeMobyDependencyActivation.SequentialUnproved)]),"Sequential");
        reject(()=>empty.Allocate([new("a",template,(NativeMobyDependencyActivation)99)]),"Sequential");

        var claim=new NativeMobyDependencyMutableGlobal("native-key",exe,0x80075830,4,[R(exe,16,32,"consumer")]);
        var singleton=Template(globals:[claim]);
        reject(()=>empty.Allocate([new("a",singleton),new("b",singleton)]),"Mutable native global conflict");
        check(empty.Rows.Count==0,"Mutable global conflict mutated the allocation seed.");
        var one=empty.Allocate([new("a",singleton)]);
        reject(()=>one.Allocate([new("b",singleton)]),"Mutable native global conflict");
        check(one.Allocate([new("a",singleton)]).Rows.Count==2,"An exact retry falsely consumed the mutable global twice.");
        var aliasClaim=claim with {Role="different-label",RuntimeAddress=0x80075832,ByteLength=2};
        reject(()=>one.Allocate([new("b",Template(globals:[aliasClaim]))]),"Mutable native global conflict");
        var foreignExe=new NativeAddressSpace(NativeAddressKind.ExecutableFile,exe.OwnerIdentity,NativeAddressRevision.Original,exe.ByteLength);
        reject(()=>empty.Allocate([new("bad",Template(globals:[claim with {ExecutableDomain=foreignExe}]))]),"exact executable address domain");
        reject(()=>Template(globals:[claim with {Consumers=[]}]),"consumer domains");
        reject(()=>Template(globals:[claim with {Consumers=[R(exe,16,32) with {ExpectedSha256=Hash([9])}]}]),"preimage");

        reject(()=>plan.Allocate([new("c",Template("another-relocation-policy"))]),"relocation policy");
        reject(()=>plan.Allocate([new("c",Template(resources:CloseShared(SharedNode(source:801))))]),"payload");
        reject(()=>plan.Allocate([new("c",Template(resources:CloseShared(SharedNode(offset:136))))]),"address domain");
        reject(()=>plan.Allocate([new("c",Template(resources:CloseShared(SharedNode(id:"different-shared-label"))))]),"overlapping resource");
        var foreignAssets=new NativeAddressSpace(NativeAddressKind.LevelData,assets.OwnerIdentity,NativeAddressRevision.Final,assets.ByteLength);
        reject(()=>empty.Allocate([new("bad",Template(resources:CloseShared(SharedNode(destination:foreignAssets))))]),"unbound final address domain");
        reject(()=>empty.Allocate([new("bad",Template(resources:CloseShared(SharedNode(destination:scene,offset:0x100+10*0x58))))]),"private instance arenas");
        var unknownPreserved = new NativeMobyDependencyNode("unknown-resident",NativeMobyDependencyKind.NativeRoutine,
            NativeMobyDependencyResolution.DestinationResident,NativeMobyDependencySharing.Immutable,Hash(bytes),[],[R(original,800,8)],
            Preserved:[new("native",0,new(foreignAssets,128,8))]);
        reject(()=>empty.Allocate([new("bad",Template(resources:CloseShared(unknownPreserved)))]),"unbound final address domain");
        reject(()=>plan.RequireSharedBinding(CloseShared(SharedNode(source:801))),"Final graph");
        reject(()=>NativeMobyDependencyInstantiationPlan.Create(new(scene,1136,176),new(scene,1200,32),0x100,10,[scene,assets],exe),"disjoint");
        reject(()=>NativeMobyDependencyInstantiationPlan.Create(new(scene,1136,176),new(scene,30000,32),0x100,11,[scene,assets],exe),"address domains");
        reject(()=>NativeMobyDependencyInstantiationPlan.Create(new(scene,1136,176),new(scene,30000,32),0x100,10,[assets],exe),"address domains");
        reject(()=>NativeMobyDependencyInstantiationPlan.Create(new(scene,1136,176),new(scene,30000,32),0x100,10,[scene,scene,assets],exe),"address domains");
        var single=Template(rows:[roles[0]]);
        check(Empty(255,1,16).Allocate([new("last-byte-owner",single)]).Rows.Single().Index==255,"Last representable native persistent owner was rejected.");
        reject(()=>Empty(256,1,16).Allocate([new("overflow-byte-owner",single)]),"<256");
        reject(()=>Empty(255,2,32).Allocate([new("crosses-byte-owner",template)]),"<256");
        reject(()=>Empty(propsBytes:16).Allocate([new("small-properties",template)]),"capacity");
        reject(()=>Template(rows:[roles[0] with {NativeClass=0xAE}]),"class/properties");
        reject(()=>Template(rows:[roles[0] with {PropertiesPointerBaseOffset=4}]),"class/properties");
        reject(()=>Template(rows:[roles[0] with {SourceInstanceIndex=256}]),"class/properties");
        reject(()=>Template(rows:[roles[0],roles[1] with {OwnerFields=[new(0,1)]}]),"owner-index");
        reject(()=>Template(rows:[roles[0],roles[1] with {OwnerFields=[new(16,0)]}]),"owner-index");
        reject(()=>Template(rows:[roles[0],roles[1] with {OwnerFields=[new(0,2)]}]),"owner-index");
        reject(()=>Template(rows:[roles[0] with {Row=roles[0].Row with {ExpectedSha256=Hash([1])}}]),"preimage");
        var alienOriginal=new NativeAddressSpace(NativeAddressKind.LevelData,original.OwnerIdentity,NativeAddressRevision.Original,original.ByteLength);
        reject(()=>Template(rows:[roles[0] with {Row=roles[0].Row with {Extent=new(alienOriginal,64,0x58)}}]),"Unbound native source");
        // Donor aliases are allowed when the code-owned schema explicitly permits them. They
        // never imply that two newly allocated mutable copies may share final storage.
        byte[] aliasedBytes=bytes.ToArray();BinaryPrimitives.WriteUInt32LittleEndian(aliasedBytes.AsSpan(160),512);
        var aliasSource=new NativeAddressSpace(NativeAddressKind.LevelData,"alias-source",NativeAddressRevision.Original,aliasedBytes.Length);
        byte[] AliasRead(NativeByteExtent e)=>e.Space==aliasSource?aliasedBytes.AsSpan((int)e.Offset,(int)e.ByteLength).ToArray():Read(e);
        NativeMobyDependencyRead AliasR(int offset,int length)=>new("native",new(aliasSource,offset,length),Hash(aliasedBytes.AsSpan(offset,length)));
        var aliasedTemplate=NativeMobyDependencyInstanceTemplate.Capture("fixture-relocation-v1",[
            roles[0] with {Row=AliasR(64,0x58),Properties=AliasR(512,16)},
            roles[1] with {Row=AliasR(160,0x58),Properties=AliasR(512,16),OwnerFields=[]}],[],shared,AliasRead);
        var aliasAllocation=empty.Allocate([new("aliased-donor",aliasedTemplate)]);
        check(aliasAllocation.Rows[0].Source.Properties.Extent==aliasAllocation.Rows[1].Source.Properties.Extent&&
            aliasAllocation.Rows[0].Properties!=aliasAllocation.Rows[1].Properties,"Legitimate donor alias leaked into private final allocation.");
        byte[] destinationRows=new byte[0x170+107*0x58];BinaryPrimitives.WriteUInt32LittleEndian(destinationRows.AsSpan(0x16C),107);
        NativeTownSquareLockedChestInstantiation.RequireNoPlacedKeyFamily(destinationRows);check(true,"Empty native key-family scope refused.");
        reject(()=>NativeTownSquareLockedChestInstantiation.RequireExactNativeScene(new byte[0x8800]),"complete exact original Town scene");
        foreach(ushort nativeClass in new ushort[]{0xAD,0xAE,0xD})
        {
            BinaryPrimitives.WriteUInt16LittleEndian(destinationRows.AsSpan(0x170+19*0x58+0x36),nativeClass);
            reject(()=>NativeTownSquareLockedChestInstantiation.RequireNoPlacedKeyFamily(destinationRows),"Destination-native");
        }
        check(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(528))==7,"Native allocation changed immutable donor owner bytes.");
        Console.WriteLine("NATIVE_MOBY_INSTANTIATION_TRANSACTIONAL_PRIVATE_SHARED_AND_GLOBAL_REFUSALS_OK");
    }

    internal static void Source(NativeTownSquareLockedChestClosure native,Action<bool,string> check,Action<Action,string> reject)
    {
        var capture=native.Instances;var plan=capture.Allocated;
        using(var original=File.OpenRead(native.SourcePath))
        {
            byte[] originalScene=DiscImage.ReadFileBytes(original,native.Layout,37,0x119E000+0x1D0000,0x8800);
            NativeTownSquareLockedChestInstantiation.RequireExactNativeScene(originalScene);check(true,"Exact original Town scene rejected.");
            originalScene[0x200]^=1;
            reject(()=>NativeTownSquareLockedChestInstantiation.RequireExactNativeScene(originalScene),"complete exact original Town scene");
        }
        check(plan.FinalRowCount==114&&plan.Rows.Count==7&&plan.PropertyBytesAllocated==0xE4,
            "Native factory did not use generic instance allocation.");
        check(plan.Rows.Select(r=>r.Index).SequenceEqual(Enumerable.Range(107,7)),"Native destination instance identities changed.");
        check(capture.Template.Globals.Select(g=>g.RuntimeAddress).Order().SequenceEqual(new uint[]{0x80075758,0x80075830}),
            "Native key/chest mutable-global obligations disappeared.");
        reject(()=>plan.Allocate([new("second-native-chest",capture.Template)]),"Mutable native global conflict");
        reject(()=>capture.Empty.Allocate([new("a",capture.Template),new("b",capture.Template)]),"Mutable native global conflict");
        reject(()=>plan.Allocate([new("second-sequential-chest",capture.Template,NativeMobyDependencyActivation.SequentialUnproved)]),"Sequential");
        var replay=plan.Allocate([new(NativeTownSquareLockedChestInstantiation.FamilyId,capture.Template)]);
        check(replay.Rows.Select(r=>r.Row).SequenceEqual(plan.Rows.Select(r=>r.Row))&&replay.PropertyBytesAllocated==0xE4,
            "Native identical instantiation retry changed private reservations.");
        foreach(var row in plan.Rows)
        {
            check(native.Graph.Installs.Single(i=>i.NodeId==row.OwnerId+".row").Destination==row.Row&&
                native.Graph.Installs.Single(i=>i.NodeId==row.OwnerId+".props").Destination==row.Properties,
                "Native graph writes do not consume allocator ownership.");
            check(native.Graph.References.Single(r=>r.NodeId==row.OwnerId+".row").After==row.Properties.Offset,
                "Native graph pointer does not use the allocated private target.");
        }
        plan.RequireSharedBinding(native.Graph);
    }
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}

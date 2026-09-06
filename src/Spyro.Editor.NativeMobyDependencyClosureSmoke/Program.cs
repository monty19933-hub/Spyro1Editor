using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

int checks = 0;
byte[] source = Enumerable.Range(0, 128).Select(i => (byte)i).ToArray();
NativeAddressSpace donor = new(NativeAddressKind.LevelData, Hash(source), NativeAddressRevision.Original, source.Length);
NativeAddressSpace target = new(NativeAddressKind.LevelData, "fixture-target", NativeAddressRevision.Final, 512);
NativeMobyDependencyRead Read(string id, int offset, int length) =>
    new(id, new NativeByteExtent(donor, offset, length), Hash(source.AsSpan(offset, length)));
NativeMobyDependencyNode Node(string id, NativeMobyDependencyResolution resolution,
    string[]? dependencies = null, NativeMobyDependencyRead[]? reads = null,
    Func<NativeMobyDependencyBuildContext, NativeMobyDependencyOutput>? build = null,
    NativeMobyDependencySharing sharing = NativeMobyDependencySharing.Immutable,
    string? owner = null, NativeMobyDependencyKind kind = NativeMobyDependencyKind.ActorPackage) =>
    new(id, kind, resolution, sharing, Hash(System.Text.Encoding.UTF8.GetBytes(id)),
        dependencies ?? [], reads ?? [], build, owner);
NativeMobyDependencyNode Import(string id, string[] deps, int sourceOffset, int targetOffset,
    NativeMobyDependencySharing sharing = NativeMobyDependencySharing.Immutable, string? owner = null) =>
    Node(id, NativeMobyDependencyResolution.ImportFromSource, deps, [Read("payload", sourceOffset, 8)],
        c => new(c.Read("payload"), [new(id, new NativeByteExtent(target, targetOffset, 8))], []), sharing, owner);
byte[] Reader(NativeByteExtent extent)
{
    if (extent.Space != donor) throw new InvalidDataException("Unbound source address space.");
    return source.AsSpan(checked((int)extent.Offset), checked((int)extent.ByteLength)).ToArray();
}
NativeMobyDependencyClosurePlan Compile(IEnumerable<NativeMobyDependencyNode> nodes, params string[] roots) =>
    NativeMobyDependencyClosureCompiler.Compile(nodes, roots, Reader);
void Check(bool condition, string reason) { checks++; if (!condition) throw new Exception(reason); }
void Reject(Action action, string needle)
{
    checks++;
    try { action(); } catch (InvalidDataException e) when (e.Message.Contains(needle, StringComparison.OrdinalIgnoreCase)) { return; }
    throw new Exception("Expected precise rejection: " + needle);
}

NativeMobyDependencyNode global = Node("global", NativeMobyDependencyResolution.GlobalShared,
    reads: [Read("code", 0, 16)], kind: NativeMobyDependencyKind.NativeRoutine) with
    { Preserved=[new("code",0,new(target,0,16))] };
NativeMobyDependencyNode model = Import("model", ["global"], 16, 128);
NativeMobyDependencyNode a = Import("a.props", ["model"], 32, 144, NativeMobyDependencySharing.PrivateInstance, "a");
NativeMobyDependencyNode b = Import("b.props", ["model"], 32, 152, NativeMobyDependencySharing.PrivateInstance, "b");
NativeMobyDependencyNode instanceA = Node("a",NativeMobyDependencyResolution.Aggregate,["a.props"],kind:NativeMobyDependencyKind.Instance);
NativeMobyDependencyNode instanceB = Node("b",NativeMobyDependencyResolution.Aggregate,["b.props"],kind:NativeMobyDependencyKind.Instance);
NativeMobyDependencyNode root = Node("bundle", NativeMobyDependencyResolution.Aggregate, ["a", "b"]);
NativeMobyDependencyNode unreachable = Node("unused", NativeMobyDependencyResolution.Unrelocatable);
NativeMobyDependencyClosurePlan plan = Compile([unreachable, b, global, root, a, model,instanceA,instanceB], "bundle");
Check(plan.Nodes.Count == 7 && plan.Installs.Count == 3, "Closure did not share one model and retain two private copies.");
Check(plan.Nodes.All(n => n.Id != "unused"), "Unreachable dependency was extracted.");
Check(plan.CopyPayload("a.props").SequenceEqual(plan.CopyPayload("b.props")), "Equal donor bytes did not create independent payloads.");
byte[] detached = plan.CopyPayload("a.props"); detached[0] ^= 0xFF;
Check(plan.CopyPayload("b.props")[0] == 32 && plan.CopyPayload("a.props")[0] == 32, "Returned buffers alias plan storage.");
Check(plan.Fingerprint == Compile([root, model, a, b, global,instanceA,instanceB], "bundle", "bundle").Fingerprint,
    "Closure ordering or repeated roots changed the plan.");
Check(plan.Nodes.Single(n => n.Id == "global").Resolution == NativeMobyDependencyResolution.GlobalShared,
    "Global native code became a copied level dependency.");
Reject(() => Compile([root, model, a, global,instanceA,instanceB], "bundle"), "b.props");
Reject(() => Compile([global, global with { EvidenceSha256 = Hash([1]) }], "global"), "duplicate");
Reject(() => Compile([model], "model"), "global");
Reject(() => Compile([Node("blocked", NativeMobyDependencyResolution.Conflict)], "blocked"), "conflict");
Reject(() => Compile([unreachable], "unused"), "unrelocatable");
Reject(() => Compile([global with { Kind = (NativeMobyDependencyKind)999 }], "global"), "kind");
Reject(() => Compile([global with { Reads = [Read("code", 0, 16) with { ExpectedSha256 = Hash([1]) }] }], "global"), "preimage");
Reject(() => Compile([global with { Reads = [Read("code", 0, 16) with {
    Extent = new NativeByteExtent(new(NativeAddressKind.LevelData, donor.OwnerIdentity, NativeAddressRevision.Original, 128), 0, 16) }] }], "global"), "address space");
Reject(() => Compile([global with { Build = _ => new([1], [], []) }], "global"), "resident");
Reject(() => Compile([a with { OwnerInstanceId = null }, model, global], "a.props"), "private");
Reject(() => Compile([a, b with { Build = c => new(c.Read("payload"), [new("b.props", new(target, 144, 8))], []) }, model, global,instanceA,instanceB], "a", "b"), "overlap");
Reject(() => Compile([a,model,global,instanceA,instanceB with { Dependencies=["a.props"] }], "a", "b"), "another instance");
NativeMobyDependencyNode indirect = Node("indirect",NativeMobyDependencyResolution.Aggregate,["a.props"]);
Reject(() => Compile([a,model,global,instanceA,instanceB with { Dependencies=["indirect"] },indirect], "a", "b"), "another instance");
Reject(() => Compile([a,model,global],"a.props"),"instance owner");
Reject(() => Compile([a with { Build=c=>new(c.Read("payload"),[],[]) },model,global,instanceA],"a"),"exactly one");
Reject(() => Compile([model with { Build = c => new(c.Read("payload"), [new("bad", new(target, 128, 7))], []) }, global], "model"), "length");

NativeMobyDependencyNode rootless = Node("reward.handler", NativeMobyDependencyResolution.DestinationResident,
    reads: [Read("dispatch", 4, 8)], kind: NativeMobyDependencyKind.RootlessController) with
    { Preserved=[new("dispatch",0,new(target,4,8))] };
Check(Compile([rootless], rootless.Id).Installs.Count == 0, "Rootless controller invented a model root.");
NativeMobyDependencyNode cycleA = Node("cycle-a", NativeMobyDependencyResolution.Aggregate, ["cycle-b"]);
NativeMobyDependencyNode cycleB = Node("cycle-b", NativeMobyDependencyResolution.Aggregate, ["cycle-a", "global"]);
Check(Compile([cycleA, cycleB, global], cycleA.Id).Nodes.Count == 3, "A closed shared dependency cycle was rejected.");
NativeMobyDependencyNode lazyA = Import("lazy-a", ["lazy-b"], 40, 176) with
{ Build = c => new(c.DependencyPayload("lazy-b"), [new("lazy-a", new(target, 176, 8))], []) };
NativeMobyDependencyNode lazyB = Import("lazy-b", ["lazy-a"], 48, 184) with
{ Build = c => new(c.DependencyPayload("lazy-a"), [new("lazy-b", new(target, 184, 8))], []) };
Reject(() => Compile([lazyA, lazyB], "lazy-a"), "recursive materialization");
Reject(() => Compile([model with { Build = c => new(c.DependencyPayload("not-declared"), [], []) }, global], "model"), "undeclared");
Reject(() => Compile([model with { Build = c => new(c.Read("not-declared"), [], []) }, global], "model"), "undeclared");
Reject(() => Compile([model with { Build = c => new(c.Read("payload"), [], [new("pointer", 0, "not-declared", 0, 4)]) }, global], "model"), "reference");
NativeMobyDependencyNode pointer = Import("pointer", ["model"], 64, 208) with
{ Build = c => new(c.Read("payload"), [new("pointer", new(target,208,8))],
    [new("model-root",0,"model",4,4,System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(64,4)))]) };
NativeMobyDependencyClosurePlan pointerPlan = Compile([pointer,model,global], "pointer");
Check(pointerPlan.References.Single().After == 132 &&
    System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(pointerPlan.CopyPayload("pointer")) == 132,
    "Graph reference did not relocate its actual installed bytes.");
NativeMobyDependencyNode consumer = Node("pointer-consumer",NativeMobyDependencyResolution.ImportFromSource,["pointer"],
    build:c=>new(c.DependencyPayload("pointer"),[],[]));
Check(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(Compile([consumer,pointer,model,global],consumer.Id).CopyPayload(consumer.Id))==132,
    "A dependent compositor consumed pre-relocation payload bytes.");
Reject(()=>Compile([pointer with { Build=c=>new(c.Read("payload"),[new("pointer",new(target,208,8))],
    [new("model-root",0,"model",4,4,0)]) },model,global],"pointer"),"preimage");
NativeAddressSpace otherTarget = new(NativeAddressKind.LevelData,"fixture-target",NativeAddressRevision.Final,512);
Reject(()=>Compile([pointer with { Build=c=>new(c.Read("payload"),[new("pointer",new(otherTarget,208,8))],
    [new("model-root",0,"model",4,4,0)]) },model,global],"pointer"),"address space");
NativeMobyDependencyNode residentConsumer = Node("resident-consumer",NativeMobyDependencyResolution.ImportFromSource,["global"],
    build:c=>new(c.DependencyRead("global","code"),[],[]));
Check(Compile([residentConsumer,global],residentConsumer.Id).CopyPayload(residentConsumer.Id).SequenceEqual(source[..16]),
    "A direct dependent could not consume checked resident bytes.");
Reject(()=>Compile([residentConsumer with { Build=c=>new(c.DependencyRead("model","payload"),[],[]) },global],residentConsumer.Id),"undeclared");
Reject(()=>Compile([model with { Build=c=>new(c.Read("payload"),[new("model",new(target,8,8))],[]) },global],"model"),"resident");
Reject(()=>Compile([global with { Preserved=[] }],"global"),"preserved final");
Reject(()=>Compile([global with { Preserved=[new("code",12,new(target,0,8))] }],"global"),"preserved source");
Check(Compile([global,rootless],"global","reward.handler").Nodes.Count==2,"Equal overlapping native residency was rejected.");
int buildsBeforeResidentConflict=0;
Reject(()=>Compile([global,rootless with { Preserved=[new("dispatch",0,new(target,0,8))] },
    model with { Build=c=>{buildsBeforeResidentConflict++;return new(c.Read("payload"),[],[]);} }],"model","reward.handler"),"conflicting preserved");
Check(buildsBeforeResidentConflict==0,"Conflicting final residency was discovered after materialization.");
NativeMobyDependencyNode splitResident=global with { Preserved=[new("code",0,new(target,0,8)),new("code",0,new(target,24,8))] };
Check(Compile([splitResident],"global").Fingerprint==Compile([splitResident with { Preserved=splitResident.Preserved!.Reverse().ToArray() }],"global").Fingerprint,
    "Preservation ordering changed a reproducible closure.");
NativeMobyDependencyNode residentPointer=pointer with { Dependencies=["global"],Build=c=>new(c.Read("payload"),[new("pointer",new(target,208,8))],
    [new("resident-root",0,"global",4,4,System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(64,4)))]) };
Check(Compile([residentPointer,global],"pointer").References.Single().After==4,"Reference to final resident binding failed.");
Reject(()=>Compile([residentPointer,splitResident],"pointer"),"one exact preserved");
Reject(()=>Compile([pointer with { Build=c=>new(c.Read("payload"),[new("pointer",new(target,208,8))],
    [new("bad-unit",0,"model",0,4,0,AddressUnitBytes:3)]) },model,global],"pointer"),"encoding");
NativeMobyDependencyNode self = Import("self",["self"],72,240) with
{ Build=c=>new(c.Read("payload"),[new("self",new(target,240,8))],
    [new("self-pointer",0,"self",4,4,System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(72,4)))]) };
Check(Compile([self],"self").References.Single().After==244,"Self address reference required recursive payload bytes.");
NativeMobyDependencyNode refA = Import("ref-a",["ref-b"],80,256) with
{ Build=c=>new(c.Read("payload"),[new("ref-a",new(target,256,8))],
    [new("to-b",0,"ref-b",0,4,System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(80,4)))]) };
NativeMobyDependencyNode refB = Import("ref-b",["ref-a"],88,264) with
{ Build=c=>new(c.Read("payload"),[new("ref-b",new(target,264,8))],
    [new("to-a",0,"ref-a",0,4,System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(88,4)))]) };
NativeMobyDependencyClosurePlan addressCycle=Compile([refB,refA],"ref-a");
Check(addressCycle.References.Count==2&&addressCycle.References.Single(r=>r.NodeId=="ref-a").After==264&&
    addressCycle.References.Single(r=>r.NodeId=="ref-b").After==256,"Mutual address-only references were not closed.");

int readsBeforeFailure = 0;
Reject(() => NativeMobyDependencyClosureCompiler.Compile([root, a, b, model,instanceA,instanceB,
    global with { Resolution = NativeMobyDependencyResolution.Conflict }], ["bundle"], extent =>
    { readsBeforeFailure++; return Reader(extent); }), "conflict");
Check(readsBeforeFailure == 0, "Conflicting closure began extraction before graph validation finished.");
Check(source.SequenceEqual(Enumerable.Range(0,128).Select(i => (byte)i)), "Pure closure modified its source.");
byte[] FixtureSector(int lba)
{
    byte[] raw=new byte[2352];Array.Fill(raw,(byte)0xFF,1,10);raw[13]=2;raw[14]=(byte)lba;raw[15]=2;raw[18]=raw[22]=8;
    for(int i=0;i<2048;i++)raw[24+i]=(byte)(i+lba);
    return RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(raw,lba);
}
byte[] physicalSource=[..FixtureSector(0),..FixtureSector(1),..FixtureSector(2)];
byte[] ExpectedFixtureSector(int lba,byte[] before)
{
    if(lba==1){before[24+42]^=0x40;return RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(before,lba);}
    return before;
}
byte[] physicalExpected=Enumerable.Range(0,3).SelectMany(i=>ExpectedFixtureSector(i,physicalSource.AsSpan(i*2352,2352).ToArray())).ToArray();
(string Hash,int Changed) VerifyFixture(byte[] original,byte[] final,string? sourceDigest=null)=>NativeTownSquareClosurePhysicalReadback.Verify(
    new MemoryStream(original,writable:false),new MemoryStream(final,writable:false),physicalSource.Length,sourceDigest??Hash(physicalSource),ExpectedFixtureSector);
var physicalResult=VerifyFixture(physicalSource,physicalExpected);
Check(physicalResult.Changed==1&&physicalResult.Hash==Hash(physicalExpected),"Physical source-free readback did not certify exact expected bytes.");
foreach(int corruptOffset in new[]{24+9,2352+24+42,2352+2072,2352+2076,2352+2248,2352+18,2*2352+2250})
{
    byte[] corrupt=physicalExpected.ToArray();corrupt[corruptOffset]^=1;
    Reject(()=>VerifyFixture(physicalSource,corrupt),"raw sector");
}
Reject(()=>VerifyFixture(physicalSource,physicalExpected,Hash([9])),"source image identity");
Reject(()=>VerifyFixture(physicalSource,physicalExpected[..^1]),"length");
Reject(()=>NativeTownSquareClosurePhysicalReadback.Verify(new LateLengthStream(physicalSource),new MemoryStream(physicalExpected),physicalSource.Length,Hash(physicalSource),ExpectedFixtureSector),"length or position changed during");
Reject(()=>NativeTownSquareClosurePhysicalReadback.Verify(new MemoryStream(physicalSource),new LateLengthStream(physicalExpected),physicalSource.Length,Hash(physicalSource),ExpectedFixtureSector),"length or position changed during");
Reject(()=>NativeTownSquareClosurePhysicalReadback.Verify(new MemoryStream(physicalSource),new MemoryStream(physicalExpected),physicalSource.Length,Hash(physicalSource),(_,_)=>[]),"expected raw-sector length");
byte[] frozenCue=System.Text.Encoding.UTF8.GetBytes("FILE exact.bin BINARY\n"),frozenReceipt=System.Text.Encoding.UTF8.GetBytes("{\"image\":\"exact\"}");
NativeTownSquareClosurePhysicalReadback.RequireCompanions(frozenCue,frozenCue,frozenReceipt,frozenReceipt);checks++;
Reject(()=>NativeTownSquareClosurePhysicalReadback.RequireCompanions([0],frozenCue,frozenReceipt,frozenReceipt),"CUE");
Reject(()=>NativeTownSquareClosurePhysicalReadback.RequireCompanions(frozenCue,frozenCue,[0],frozenReceipt),"receipt");
string occupied=Directory.CreateTempSubdirectory("spyro-native-closure-occupied-").FullName;
string sentinel=Path.Combine(occupied,"existing-receipt.json");File.WriteAllBytes(sentinel,frozenReceipt);
try
{
    Reject(()=>NativeTownSquareLockedChestClosureCompiler.Capture(sentinel),"exact immutable USA source");
    bool refused=false;
    try{NativeTownSquareLockedChestClosurePublisher.Publish(Path.Combine(Path.GetTempPath(),"missing-original.bin"),Path.Combine(Path.GetTempPath(),"missing-original.cue"),occupied);}
    catch(IOException e) when(e.Message.Contains("existing or partial",StringComparison.Ordinal)){refused=true;}
    Check(refused&&File.ReadAllBytes(sentinel).SequenceEqual(frozenReceipt)&&Directory.EnumerateFileSystemEntries(occupied).Count()==1,"Occupied native output was not refused before source extraction or altered its existing artifact.");
}
finally{File.Delete(sentinel);Directory.Delete(occupied);}
Check(physicalSource.SequenceEqual(Enumerable.Range(0,3).SelectMany(FixtureSector)),"Physical streaming readback mutated its read-only source.");
if(args.Length>0)
{
    string nativeSource=args[0] is "--publish" or "--faults" or "--verify"?args[1]:args[0];
    var native=NativeTownSquareLockedChestClosureCompiler.Capture(nativeSource);
    Check(native.Graph.Nodes.Count>100,"Source-bound graph did not enumerate native imports.");
    Check(native.Data[0x1D0004]==0x36,"Local model sound descriptor was not remapped to equivalent resident sample.");
    Check(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(native.Data.AsSpan(0x1D1800+0x4E38))==0x11B8,"Genuine properties component did not grow.");
    Check(native.Graph.Nodes.Count(n=>n.Sharing==NativeMobyDependencySharing.PrivateInstance)==14,"Seven native instances did not receive private rows/properties.");
    var privateProps=native.Graph.Installs.Where(i=>i.NodeId.EndsWith(".props",StringComparison.Ordinal)).ToArray();
    Check(privateProps.Length==7&&privateProps.All(p=>p.Destination.Offset>=0x5F0C&&p.Destination.EndExclusive<=0x5FF0),"Native private properties were placed outside the grown genuine component.");
    using(var retail=File.OpenRead(nativeSource))
    {
        byte[] oldScene=DiscImage.ReadFileBytes(retail,native.Layout,37,0x119E000+0x1D0000,0x8800);
        ReadOnlySpan<byte> newScene=native.Data.AsSpan(0x1D1800,0x8800);
        Check(oldScene.AsSpan(0x170,107*0x58).SequenceEqual(newScene.Slice(0x170,107*0x58)),"An existing native source row changed.");
        Check(oldScene.AsSpan(0x7F2C,129*4).SequenceEqual(newScene.Slice(0x8010,129*4)),"An existing native pointer-fixup registration changed.");
        Check(oldScene.AsSpan(0x5F0C,0x201C).SequenceEqual(newScene.Slice(0x5FF0,0x201C)),"Native pod/collision suffix changed during properties growth.");
        HashSet<uint> fields=[];
        for(int i=0;i<136;i++)
        {
            uint field=System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(newScene.Slice(0x8010+i*4,4));
            Check(fields.Add(field)&&field<0x5FF0,"Duplicate or unowned final scene fixup field.");
            uint to=System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(newScene.Slice((int)field,4));
            Check(to>=0x4E3C&&to<0x5FF0,"Final fixup target escaped genuine properties ownership.");
        }
    }
    Console.WriteLine($"NATIVE_TOWNSQUARE_CLOSURE_SOURCE_OK: graph {native.Graph.Fingerprint}; nodes {native.Graph.Nodes.Count}; installs {native.Graph.Installs.Count}; references {native.Graph.References.Count}; data {Hash(native.Data)}; overlay {Hash(native.Overlay)}; exe {Hash(native.Executable)}");
    if(args[0]=="--publish")
    {
        if(args.Length!=4)throw new ArgumentException("--publish <original.bin> <original.cue> <new-output-directory>");
        var published=NativeTownSquareLockedChestClosurePublisher.Publish(nativeSource,args[2],args[3]);
        Console.WriteLine("NATIVE_TOWNSQUARE_CLOSURE_PUBLISHED: "+System.Text.Json.JsonSerializer.Serialize(published));
    }
    if(args[0]=="--faults")
    {
        if(args.Length!=4)throw new ArgumentException("--faults <original.bin> <original.cue> <new-output-root>");
        foreach(var fault in new[]{NativeTownSquareClosureFault.AfterCopy,NativeTownSquareClosureFault.AfterWrite,NativeTownSquareClosureFault.BeforePublish})
        {
            string destination=Path.Combine(args[3],fault.ToString());
            bool failed=false;
            try{NativeTownSquareLockedChestClosurePublisher.Publish(nativeSource,args[2],destination,fault);}
            catch(IOException e) when(e.Message.Contains("Deliberate native closure publication fault",StringComparison.Ordinal)){failed=true;}
            Check(failed,"Native publication fault was not reached.");
            Check(Directory.Exists(destination)&&!Directory.EnumerateFileSystemEntries(destination).Any(),"Failed publication retained a bootable or partial artifact.");
        }
        Console.WriteLine("NATIVE_TOWNSQUARE_CLOSURE_FAULTS_OK: 3 publication stages roll back before new-only publication.");
    }
    if(args[0]=="--verify")
    {
        if(args.Length!=4)throw new ArgumentException("--verify <original.bin> <original.cue> <published-output-directory>");
        var verified=NativeTownSquareLockedChestClosurePublisher.VerifyPublished(nativeSource,args[2],args[3]);
        bool refused=false;
        try{NativeTownSquareLockedChestClosurePublisher.Publish(nativeSource,args[2],args[3]);}
        catch(IOException e) when(e.Message.Contains("existing or partial",StringComparison.Ordinal)){refused=true;}
        Check(refused,"An immutable published candidate was accepted as overwrite output.");
        Check(verified.ImageSha256=="d94d90b51c15b82328b6dbeb67474e563ddefc7d7f6c81bd4b61c526c58083fe"&&verified.ChangedRawSectors==44671,
            "Native v2 physical regression golden changed.");
        Console.WriteLine("NATIVE_TOWNSQUARE_CLOSURE_RETAINED_OK: "+System.Text.Json.JsonSerializer.Serialize(verified));
    }
}
Console.WriteLine($"NATIVE_MOBY_DEPENDENCY_CLOSURE_SMOKE_OK: {checks} checks");
static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

sealed class LateLengthStream(byte[] bytes):MemoryStream(bytes,writable:false)
{
    private bool _read;
    public override long Length=>base.Length+(_read?1:0);
    public override int Read(Span<byte> buffer){int count=base.Read(buffer);_read=true;return count;}
}

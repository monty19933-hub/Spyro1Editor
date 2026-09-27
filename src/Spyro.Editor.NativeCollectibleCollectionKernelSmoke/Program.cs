using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

// Entirely source-free. Manual ABI bytes below are independent of production
// emission. No initialized RAM allocation, native file or card is issued here.
int checks=0;
void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
void Refuse(Action action,string needle)
{try{action();}catch(InvalidDataException e)when(e.Message.Contains(needle,StringComparison.Ordinal)){checks++;return;}throw new Exception("Expected refusal: "+needle);}
const uint Code=0x800E0000,Ledger=0x801F0000,Stop=0x80010000;
var space=new NativeAddressSpace(NativeAddressKind.LevelData,"test-only-detached-collection-code",NativeAddressRevision.Final,0x4000);
var capacity=new NativeByteExtent(space,0x40,0x2000);
static Guid Id(int value)=>Guid.Parse(value.ToString("x32"));
static DetachedCollectibleManifest Manifest(int count)=>DetachedCollectibleManifest.CreateNew(
    Guid.Parse("99999999-1111-2222-3333-444444444444"),Enumerable.Range(1,count).Select(Id));
static void U16(byte[] b,int at,int value)=>BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(at),(ushort)value);
static void U32(byte[] b,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at),value);
static byte[] Initial(DetachedCollectibleSave save,int slot,uint generation=0,byte flags=0)
{
    var m=save.Manifest;int raw=(m.LifetimeCount+7)/8,aligned=(raw+15)&~15;
    byte[] b=Enumerable.Repeat((byte)0xA5,0x200).ToArray();b.AsSpan(0x10,0x80+2*aligned).Clear();
    U32(b,0x10,0x524C4353);U16(b,0x14,1);b[0x16]=1;b[0x17]=(byte)slot;b[0x18]=1;b[0x19]=flags;U16(b,0x1A,0x2345);
    m.SerializeCanonical().AsSpan(0x10,16).CopyTo(b.AsSpan(0x20));Convert.FromHexString(m.Sha256).CopyTo(b,0x30);
    b.AsSpan(0x50,0x20).Fill(0x5A);U32(b,0x70,m.Revision);U16(b,0x74,m.LifetimeCount);U16(b,0x76,raw);
    U16(b,0x78,m.ActiveCount);U16(b,0x7A,save.EarnedActiveInstances(slot).Count);U32(b,0x7C,generation);
    U32(b,0x80,123);U32(b,0x8C,456);save.CopySlotBits(slot).CopyTo(b,0x90);
    foreach(var entry in m.Entries.Where(e=>!e.Retired))b[0x90+aligned+entry.BitIndex/8]|=(byte)(1<<(entry.BitIndex%8));
    return b;
}
var queries=new Dictionary<NativeCollectibleCollectionKernel,NativeCollectibleStateQueryKernel>();
int queryCases=0;
Check((uint)NativeCollectibleQueryResult.Available==0&&(uint)NativeCollectibleQueryResult.AlreadyEarned==1&&
    (uint)NativeCollectibleQueryResult.Retired==2&&(uint)NativeCollectibleQueryResult.OutOfRange==3&&
    (uint)NativeCollectibleQueryResult.InvalidLedger==4,"Query result ABI explicitly distinguishes available from collected.");
Check(typeof(NativeCollectibleStateQueryKernel).GetConstructors(System.Reflection.BindingFlags.Instance|
    System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).All(c=>c.IsPrivate),
    "Query type cannot wrap caller-supplied collecting bytes through a nonprivate constructor.");
NativeCollectibleCollectionKernel Kernel(DetachedCollectibleManifest m,int slot=0)
{
    var result=NativeCollectibleCollectionKernel.Compile(m,slot,capacity,Code,Ledger);
    queries.Add(result,NativeCollectibleStateQueryKernel.Compile(m,slot,capacity,Code,Ledger));
    return result;
}
void VerifyQuery(NativeCollectibleCollectionKernel collector,byte[] ledger,uint bit,
    NativeCollectibleCollectionResult collectionResult,uint pointer)
{
    var query=queries[collector];var cpu=new RenderProbeCpu{StrictSignedArithmetic=true};
    cpu.Copy(Code,query.CopyBytes());cpu.Copy(Ledger,ledger);
    for(int i=1;i<32;i++)cpu.Regs[i]=0xB0000000u+(uint)i;
    cpu.Regs[4]=pointer;cpu.Regs[5]=bit;cpu.Regs[29]=0x801D0000;cpu.Regs[31]=Stop;
    cpu.Hi=0x10203040;cpu.Lo=0x50607080;uint[] before=cpu.Regs.ToArray();
    cpu.Run(query.EntryPc,Stop);
    var expected=collectionResult is NativeCollectibleCollectionResult.Collected or NativeCollectibleCollectionResult.GenerationOverflow
        ? NativeCollectibleQueryResult.Available : (NativeCollectibleQueryResult)(uint)collectionResult;
    Check(cpu.Regs[2]==(uint)expected,"Read-only state query outcome independent of proximity and mutation overflow.");
    Check(cpu.Writes.Count==0&&Enumerable.Range(0,0x200).All(i=>cpu.Get(Ledger+(uint)i,1)==ledger[i]),
        "Every query preserves the full ledger without even an idempotent write.");
    Check(cpu.Reads.All(p=>p>=Ledger+0x10&&p<Ledger+(uint)query.LiveFootprintBytes),
        "Query never reads player proximity, row state, native file globals, guard or unowned tail.");
    Check(Enumerable.Range(1,31).Where(r=>r!=2&&r is not(>=8 and <=15)&&r is not(24 or 25)).All(r=>before[r]==cpu.Regs[r])&&
        cpu.Hi==0x10203040&&cpu.Lo==0x50607080,"Query preserves input/saved registers, stack and HI/LO.");
    Check(cpu.Calls.Count==0&&query.StackByteLength==0,"Query has no callbacks or nested stack.");
    Check(query.ManifestSha256==collector.ManifestSha256&&query.OrdinarySlot==collector.OrdinarySlot&&
        query.CallerLedgerAddress==Ledger&&query.Owner.ByteLength==query.CopyBytes().Length,
        "Query owns exact emitted bytes and identical manifest/slot/pointer binding.");
    Check(!query.NativeResidenceIssued&&!query.DispatchActivated&&!query.PublicationApproved&&!query.PersistentSaveChanges&&
        !query.ConcurrentMutationSafe,"Read-only query grants no live admission, retirement, concurrency or save authority.");
    if(pointer!=Ledger||collectionResult==NativeCollectibleCollectionResult.OutOfRange)
        Check(cpu.Reads.Count==0,"Invalid query pointer or unsigned ID refuses before memory access.");
    queryCases++;
}
RenderProbeCpu Execute(NativeCollectibleCollectionKernel kernel,byte[] ledger,uint bit,
    NativeCollectibleCollectionResult expected,uint pointer=Ledger)
{
    var cpu=new RenderProbeCpu{StrictSignedArithmetic=true};cpu.Copy(Code,kernel.CopyBytes());cpu.Copy(Ledger,ledger);
    cpu.Put(Ledger-4,0x12345678);cpu.Put(Ledger+0x200,0x87654321);
    for(int i=1;i<32;i++)cpu.Regs[i]=0xB0000000u+(uint)i;
    cpu.Regs[4]=pointer;cpu.Regs[5]=bit;cpu.Regs[29]=0x801D0000;cpu.Regs[31]=Stop;cpu.Hi=0x10203040;cpu.Lo=0x50607080;
    uint[] before=cpu.Regs.ToArray();cpu.Run(kernel.EntryPc,Stop);
    Check(cpu.Regs[2]==(uint)expected,"Exact collection result for bit "+bit+": "+cpu.Regs[2]+" expected "+expected);
    Check(Enumerable.Range(1,31).Where(r=>r!=2&&r is not(>=8 and <=15)&&r is not(24 or 25)).All(r=>before[r]==cpu.Regs[r])&&
        cpu.Hi==0x10203040&&cpu.Lo==0x50607080,"Leaf preserves arguments/callee registers/SP/RA/HI/LO.");
    Check(cpu.Calls.Count==0&&kernel.StackByteLength==0,"No native kill/helper/callback or stack frame.");
    Check(cpu.Reads.All(p=>p>=Ledger+0x10&&p<Ledger+(uint)kernel.LiveFootprintBytes),"All reads remain in exact non-guard live ledger extent.");
    Check(cpu.Get(Ledger-4)==0x12345678&&cpu.Get(Ledger+0x200)==0x87654321,"Outer guard words retained.");
    if(expected!=NativeCollectibleCollectionResult.Collected)
    {
        Check(cpu.Writes.Count==0,"Every refusal and duplicate writes nothing.");
        Check(Enumerable.Range(0,0x200).All(i=>cpu.Get(Ledger+(uint)i,1)==ledger[i]),"Every refused ledger byte preserved.");
    }
    else
    {
        HashSet<uint> writes=[Ledger+0x19,Ledger+0x7A,Ledger+0x7B,Ledger+0x7C,Ledger+0x7D,Ledger+0x7E,Ledger+0x7F,Ledger+0x90+bit/8];
        Check(cpu.Writes.Count==8&&cpu.Writes.ToHashSet().SetEquals(writes),"Only selected byte/count/generation/dirty stores.");
        Check(Enumerable.Range(0,0x200).Where(i=>!writes.Contains(Ledger+(uint)i)).All(i=>cpu.Get(Ledger+(uint)i,1)==ledger[i]),
            "Guard, active mask, manifest, other slots/opaque controls and unused tail unchanged.");
        Check(cpu.Get(Ledger+0x7A,2)==BinaryPrimitives.ReadUInt16LittleEndian(ledger.AsSpan(0x7A))+1&&
            cpu.Get(Ledger+0x7C)==BinaryPrimitives.ReadUInt32LittleEndian(ledger.AsSpan(0x7C))+1&&cpu.Get(Ledger+0x19,1)==1,
            "Count/dirty/generation advance exactly once.");
        Check(cpu.Get(Ledger+0x90+bit/8,1)==(uint)(ledger[0x90+bit/8]|1<<((int)bit%8)),
            "The exact stable ID bit is set without altering its byte neighbours or narrowing high IDs.");
    }
    VerifyQuery(kernel,ledger,bit,expected,pointer);
    return cpu;
}

var m=Manifest(9);var empty=DetachedCollectibleSave.CreateNew(m);var kernel=Kernel(m);
Check(kernel.ManifestSha256==m.Sha256&&kernel.CallerLedgerAddress==Ledger&&kernel.LedgerByteLength==0x200&&kernel.LiveFootprintBytes==0xB0,
    "Exact manifest-bound detached ABI geometry.");
Check(!kernel.NativeResidenceIssued&&!kernel.DispatchActivated&&!kernel.PublicationApproved&&!kernel.PersistentSaveChanges&&!kernel.ConcurrentMutationSafe,
    "No allocation, runtime, concurrency, save or hook authority.");
Check(kernel.Owner.ByteLength==kernel.CopyBytes().Length&&kernel.CopyBytes().Length<=capacity.ByteLength,"Exact emitted code owner within capacity.");
foreach(uint bit in new uint[]{0,1,7,8})
{
    var cpu=Execute(kernel,Initial(empty,0),bit,NativeCollectibleCollectionResult.Collected);
    byte[] after=Enumerable.Range(0,0x200).Select(i=>(byte)cpu.Get(Ledger+(uint)i,1)).ToArray();
    Execute(kernel,after,bit,NativeCollectibleCollectionResult.AlreadyEarned);
    Check(after.AsSpan(0x90,2).SequenceEqual(empty.WithCollected(0,m.Entries[(int)bit].InstanceId).CopySlotBits(0)),"Detached managed earned-state agrees.");
}
foreach(uint bit in new uint[]{9,10,1408,0x80000000,uint.MaxValue})
    Check(Execute(kernel,Initial(empty,0),bit,NativeCollectibleCollectionResult.OutOfRange).Reads.Count==0,"Unsigned range refusal before dereference.");
foreach(uint pointer in new uint[]{0,Ledger+1,Ledger+0x10,0x801FF600,uint.MaxValue})
    Check(Execute(kernel,Initial(empty,0),0,NativeCollectibleCollectionResult.InvalidLedger,pointer).Reads.Count==0,"Only compiled exact base may be dereferenced.");
byte[] otherController=Initial(empty,0);otherController[0x18]=0;
Execute(kernel,otherController,0,NativeCollectibleCollectionResult.Collected);
var next=m.Advance([Id(99)],[m.Entries[1].InstanceId]);var migrated=empty.WithCollected(0,m.Entries[1].InstanceId).MigrateTo(next);
Execute(Kernel(next),Initial(migrated,0),1,NativeCollectibleCollectionResult.Retired);
var collected=Execute(Kernel(next),Initial(migrated,0),9,NativeCollectibleCollectionResult.Collected);
Check((collected.Get(Ledger+0x90,1)&2)==2,"Retired earned bit retained; new identity appends without recycling.");
foreach(int at in new[]{0x10,0x11,0x12,0x13,0x14,0x15,0x16,0x17,0x18,0x19,0x1C,0x1D,0x1E,0x1F,
    0x70,0x71,0x72,0x73,0x74,0x75,0x76,0x77,0x78,0x79,0x7A,0x7B,0x84,0x85,0x86,0x87,0x88,0x89,0x8A,0x8B}
    .Concat(Enumerable.Range(0x20,0x30)))
{
    byte[] b=Initial(empty,0);b[at]^=at is 0x18 or 0x19?(byte)2:(byte)1;
    Execute(kernel,b,0,NativeCollectibleCollectionResult.InvalidLedger);
}
foreach(int at in Enumerable.Range(0x92,14).Concat(Enumerable.Range(0xA0,16)))
{byte[] b=Initial(empty,0);b[at]^=1;Execute(kernel,b,0,NativeCollectibleCollectionResult.InvalidLedger);}
foreach(byte padding in new byte[]{2,4,8,16,32,64,128})
{byte[] b=Initial(empty,0);b[0x91]=padding;Execute(kernel,b,0,NativeCollectibleCollectionResult.InvalidLedger);}
Execute(kernel,Initial(empty,0,uint.MaxValue),0,NativeCollectibleCollectionResult.GenerationOverflow);
Execute(kernel,Initial(empty.WithCollected(0,m.Entries[0].InstanceId),0,uint.MaxValue,1),0,NativeCollectibleCollectionResult.AlreadyEarned);
var last=Execute(kernel,Initial(empty,0,uint.MaxValue-1,1),0,NativeCollectibleCollectionResult.Collected);
byte[] lastBytes=Enumerable.Range(0,0x200).Select(i=>(byte)last.Get(Ledger+(uint)i,1)).ToArray();
Execute(kernel,lastBytes,1,NativeCollectibleCollectionResult.GenerationOverflow);
for(int slot=0;slot<3;slot++)
{
    var slotState=empty.WithCollected((slot+1)%3,m.Entries[0].InstanceId);
    Execute(Kernel(m,slot),Initial(slotState,slot),0,NativeCollectibleCollectionResult.Collected);
    Execute(Kernel(m,slot),Initial(slotState,(slot+1)%3),0,NativeCollectibleCollectionResult.InvalidLedger);
}
var zero=Manifest(0);Check(Execute(Kernel(zero),Initial(DetachedCollectibleSave.CreateNew(zero),0),0,NativeCollectibleCollectionResult.OutOfRange).Reads.Count==0,
    "Empty lifetime has no valid bitmap dereference.");
var maximum=Manifest(1408);var maximumState=DetachedCollectibleSave.CreateNew(maximum);var maximumKernel=Kernel(maximum);
foreach(uint bit in new uint[]{0,7,8,15,16,127,128,255,256,511,512,1023,1024,1406,1407})
    Execute(maximumKernel,Initial(maximumState,0),bit,NativeCollectibleCollectionResult.Collected);
Check(maximumKernel.LiveFootprintBytes==0x1F0,"Native1408 ceiling is separate from detached4096 schema.");
var dense=maximumState;
foreach(var entry in maximum.Entries.Take(1407))dense=dense.WithCollected(0,entry.InstanceId);
var denseCpu=Execute(maximumKernel,Initial(dense,0),1407,NativeCollectibleCollectionResult.Collected);
Check(denseCpu.Get(Ledger+0x7A,2)==1408,"Dense retirement-aware count reaches the physical lifetime ceiling without narrowing.");
byte[] corruptDense=Initial(dense,0);U16(corruptDense,0x7A,1406);
Execute(maximumKernel,corruptDense,1407,NativeCollectibleCollectionResult.InvalidLedger);
var allRetired=m.Advance([],m.Entries.Select(e=>e.InstanceId));
Execute(Kernel(allRetired),Initial(empty.MigrateTo(allRetired),0),0,NativeCollectibleCollectionResult.Retired);
var random=new Random(0x43534C52);var varied=Manifest(73).Advance([],new[]{Id(2),Id(10),Id(18),Id(73)});
for(int test=0;test<64;test++)
{
    int slot=test%3;var state=DetachedCollectibleSave.CreateNew(varied);
    foreach(var e in varied.Entries.Where(e=>!e.Retired))if(random.Next(4)==0)state=state.WithCollected(slot,e.InstanceId);
    int bit=random.Next(varied.LifetimeCount);var entry=varied.Entries[bit];
    var expected=entry.Retired?NativeCollectibleCollectionResult.Retired:state.IsEarned(slot,entry.InstanceId)?NativeCollectibleCollectionResult.AlreadyEarned:NativeCollectibleCollectionResult.Collected;
    Execute(Kernel(varied,slot),Initial(state,slot,(uint)test,(byte)(test%2)),(uint)bit,expected);
}
byte[] copy=kernel.CopyBytes();copy[0]^=1;Check(!copy.SequenceEqual(kernel.CopyBytes()),"Kernel byte copies cannot mutate code.");
Refuse(()=>Kernel(Manifest(1409)),"residence");Refuse(()=>Kernel(m,-1),"slot");Refuse(()=>Kernel(m,3),"slot");
Refuse(()=>NativeCollectibleCollectionKernel.Compile(m,0,capacity,Code+1,Ledger),"aligned");
Refuse(()=>NativeCollectibleCollectionKernel.Compile(m,0,capacity,Code,Ledger+1),"aligned");
Refuse(()=>NativeCollectibleCollectionKernel.Compile(m,0,capacity,Code,Ledger+4),"aligned");
Refuse(()=>NativeCollectibleCollectionKernel.Compile(m,0,capacity,0x801FFF00,Ledger),"RAM");
Refuse(()=>NativeCollectibleCollectionKernel.Compile(m,0,capacity,0x7FFFF000,Ledger),"RAM");
Refuse(()=>NativeCollectibleCollectionKernel.Compile(m,0,capacity,Code,0xFFFFFF00),"RAM");
Refuse(()=>NativeCollectibleCollectionKernel.Compile(m,0,capacity,Code,0x801FFF00),"RAM");
Refuse(()=>NativeCollectibleCollectionKernel.Compile(m,0,capacity,Code,Code),"overlap");
Refuse(()=>NativeCollectibleCollectionKernel.Compile(m,0,capacity,Ledger+0x1FC,Ledger),"overlap");
Refuse(()=>NativeCollectibleCollectionKernel.Compile(m,0,new(space,1,0x2000),Code,Ledger),"aligned");
Refuse(()=>NativeCollectibleCollectionKernel.Compile(m,0,new(space,0,4),Code,Ledger),"capacity");
Refuse(()=>NativeCollectibleCollectionKernel.Compile(m,0,new(new(NativeAddressKind.Scene,"wrong",NativeAddressRevision.Original,0x2000),0,0x2000),Code,Ledger),"final code");
Console.WriteLine($"NATIVE_COLLECTIBLE_COLLECTION_KERNEL_OK checks={checks} codeBytes={kernel.CopyBytes().Length:X} maximumCodeBytes={maximumKernel.CopyBytes().Length:X} sourceIo=0 runtime=false save=false");
byte[] queryCopy=queries[kernel].CopyBytes();queryCopy[0]^=1;
Check(!queryCopy.SequenceEqual(queries[kernel].CopyBytes()),"Query bytes remain immutable.");
Refuse(()=>NativeCollectibleStateQueryKernel.Compile(m,3,capacity,Code,Ledger),"slot");
Refuse(()=>NativeCollectibleStateQueryKernel.Compile(m,0,capacity,Code,Code),"overlap");
Refuse(()=>NativeCollectibleStateQueryKernel.Compile(Manifest(1409),0,capacity,Code,Ledger),"residence");
Refuse(()=>NativeCollectibleStateQueryKernel.Compile(m,0,new(space,0,4),Code,Ledger),"capacity");
Console.WriteLine($"NATIVE_COLLECTIBLE_STATE_QUERY_OK cases={queryCases} checks={checks} codeBytes={queries[kernel].CopyBytes().Length:X} maximumCodeBytes={queries[maximumKernel].CopyBytes().Length:X} writes=0 nativeAuthority=false");

using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using F=AttemptFixtures;

int checks=0;
void Check(bool value,string why){if(!value)throw new Exception(why);checks++;}
void Refuse(Action action,string needle)
{try{action();}catch(InvalidDataException e)when(e.Message.Contains(needle,StringComparison.Ordinal)){checks++;return;}throw new Exception("Expected refusal: "+needle);}
const uint Code=0x800E0000,PredicateCode=0x800F0000,CollectorCode=0x80100000,Ledger=0x801E0000,Sp=0x801F0000,Stop=0x80010000;
var domain=new NativeAddressSpace(NativeAddressKind.LevelData,"detached-row-attempt-fixture",NativeAddressRevision.Final,0x10000);
NativeJapaneseEggCollectibleIdentity[] Mappings()=>[new("egg-a",F.Id(257)),new("egg-b",F.Id(256))];
var manifest=F.Manifest(257);

void Verify(NativeJapaneseEggAuthoredClassesPlan authored,NativeJapaneseEggPickupPredicateSource predicateSource,bool source)
{
    var bindings=NativeJapaneseEggCollectibleBinding.Create(authored,manifest,Mappings());
    var predicate=predicateSource.Compile(new(domain,0x1000,0x154),PredicateCode);
    var collector=NativeCollectibleCollectionKernel.Compile(manifest,1,new(domain,0x2000,0x2000),CollectorCode,Ledger);
    NativeJapaneseEggCollectionAttempt Compile(NativeJapaneseEggCollectibleBinding? rows=null,
        NativeJapaneseEggAuthoredClassesPlan? current=null,DetachedCollectibleManifest? currentManifest=null,
        NativeJapaneseEggPickupPredicateKernel? p=null,NativeCollectibleCollectionKernel? k=null,
        NativeByteExtent? capacity=null,uint address=Code)=>NativeJapaneseEggCollectionAttempt.Compile(rows??bindings,current??authored,
            currentManifest??manifest,p??predicate,k??collector,capacity??new(domain,0,0x1000),address);
    var attempt=Compile();
    Check(attempt.SourceBound==source&&bindings.SourceCertified==source,"Source ancestry retained without fixture promotion.");
    Check(!attempt.DispatchActivated&&!attempt.RowRetirementApproved&&!attempt.NativeResidenceIssued&&!attempt.PersistentSaveChanges&&
        !attempt.ConcurrentMutationSafe&&!attempt.PublicationApproved,"No installation, lifecycle, save, concurrency or publication authority.");
    Check(attempt.StackByteLength==0x20&&attempt.MaximumStackByteLength==0x40&&attempt.Owner.ByteLength==attempt.CopyBytes().Length,
        "Exact caller code and nested predicate stack declarations.");
    Check(bindings.Rows.Single(r=>r.RowId=="egg-a").BitIndex==256&&bindings.Rows.Single(r=>r.RowId=="egg-b").BitIndex==255,
        "Explicit GUID mapping, not placement order, gives full-width boundary IDs.");
    Check(bindings.ManifestSha256==manifest.Sha256&&bindings.AuthoredPlanSha256==authored.PlanSha256&&
        bindings.Rows.All(r=>r.RowAddress==authored.Bindings.Single(b=>b.RowId==r.RowId).ProjectedRowAddress),"Exact final authored identity and addresses.");
    bindings.RequireCurrent(authored,DetachedCollectibleManifest.Parse(manifest.SerializeCanonical()));checks++;
    Refuse(()=>NativeJapaneseEggCollectibleBinding.Create(authored,manifest,[]),"mapping");
    Refuse(()=>NativeJapaneseEggCollectibleBinding.Create(authored,manifest,[new("egg-a",F.Id(256)),new("egg-a",F.Id(257))]),"mapping");
    Refuse(()=>NativeJapaneseEggCollectibleBinding.Create(authored,manifest,[new("egg-a",F.Id(256)),new("egg-b",F.Id(256))]),"identity");
    Refuse(()=>NativeJapaneseEggCollectibleBinding.Create(authored,manifest,[new("egg-a",F.Id(256)),new("unknown",F.Id(257))]),"mapping");
    Refuse(()=>NativeJapaneseEggCollectibleBinding.Create(authored,manifest,[new("egg-a",F.Id(256)),new("egg-b",F.Id(999))]),"manifest");
    Refuse(()=>NativeJapaneseEggCollectibleBinding.Create(authored,manifest.Advance([],[F.Id(257)]),Mappings()),"retired");
    Refuse(()=>Compile(currentManifest:manifest.Advance([F.Id(999)],[])),"manifest");
    Refuse(()=>Compile(k:NativeCollectibleCollectionKernel.Compile(F.Manifest(256),1,new(domain,0x2000,0x2000),CollectorCode,Ledger)),"manifest");

    RenderProbeCpu Cpu(byte[] ledger)
    {
        var cpu=new RenderProbeCpu{StrictSignedArithmetic=true};cpu.Copy(Code,attempt.CopyBytes());cpu.Copy(PredicateCode,predicate.CopyBytes());
        cpu.Copy(CollectorCode,collector.CopyBytes());cpu.Copy(Ledger,ledger);
        byte[] scene=authored.CopyScene();uint sceneBase=authored.Projection.ProjectedSceneRamStart;cpu.Copy(sceneBase,scene);
        if(source)
        {
            cpu.Copy(0x8000F800,authored.Integration.CopyOriginalExecutable());
            int moby=authored.Integration.OriginalSceneSource.RowTableOffset-8;
            cpu.Regs[16]=sceneBase+(uint)moby;cpu.Regs[5]=cpu.Regs[16];cpu.Run(0x80013F24,0x80013FC4);
            int fix=authored.Integration.OriginalSceneSource.FixupOffset+24;
            cpu.Regs[16]=sceneBase+(uint)fix;cpu.Regs[17]=0;cpu.Regs[18]=sceneBase;cpu.Run(0x80014040,0x80014084);
            foreach(var row in bindings.Rows)Check(cpu.Get(row.RowAddress)==row.PrivateAddress,"Real source scene fixup agrees with bound final private pointer.");
        }
        else foreach(var row in bindings.Rows)cpu.Put(row.RowAddress,row.PrivateAddress);
        foreach(var row in bindings.Rows){cpu.Put(row.RowAddress+0xC,0);cpu.Put(row.RowAddress+0x10,0);cpu.Put(row.RowAddress+0x14,0);cpu.Put(row.RowAddress+0x38,0,2);}
        cpu.Put(0x80078A58,0);cpu.Put(0x80078A5C,0);cpu.Put(0x80078A60,0);cpu.Put(0x80078C4C,0);cpu.Put(0x80078AD0,0);cpu.Put(0x80078AD4,0);
        cpu.Copy(Sp-0x50,Enumerable.Repeat((byte)0xCC,0x60).ToArray());cpu.Put(Ledger-4,0x12345678);cpu.Put(Ledger+0x200,0x87654321);
        cpu.Reads.Clear();cpu.Writes.Clear();cpu.Calls.Clear();cpu.Visited.Clear();return cpu;
    }
    RenderProbeCpu Execute(string id,byte[] ledger,NativeJapaneseEggCollectionAttemptResult expected,bool calls,
        bool collectCall,Action<RenderProbeCpu>? change=null,uint? pointer=null,uint ledgerPointer=Ledger)
    {
        var row=bindings.Rows.Single(r=>r.RowId==id);var cpu=Cpu(ledger);
        for(int i=1;i<32;i++)cpu.Regs[i]=0xBA000000u+(uint)i;
        cpu.Regs[4]=pointer??row.RowAddress;cpu.Regs[5]=ledgerPointer;cpu.Regs[29]=Sp;cpu.Regs[31]=Stop;
        cpu.Hi=0x11223344;cpu.Lo=0x55667788;change?.Invoke(cpu);uint[] before=cpu.Regs.ToArray();
        byte[][] rowsBefore=bindings.Rows.Select(r=>Enumerable.Range(0,0x58).Select(i=>(byte)cpu.Get(r.RowAddress+(uint)i,1)).ToArray()).ToArray();
        byte[][] privateBefore=bindings.Rows.Select(r=>Enumerable.Range(0,12).Select(i=>(byte)cpu.Get(r.PrivateAddress+(uint)i,1)).ToArray()).ToArray();
        cpu.Run(attempt.EntryPc,Stop);
        Check(cpu.Regs[2]==(uint)expected,"Attempt result "+id+" is "+cpu.Regs[2]+", expected "+expected);
        Check(new[]{4,5,16,17,18,19,20,21,22,23,26,27,28,29,30,31}.All(r=>cpu.Regs[r]==before[r])&&cpu.Hi==0x11223344&&cpu.Lo==0x55667788,
            "Arguments, saved/kernel registers, GP/SP/RA/HI/LO preserved on normal returns.");
        uint helper=predicate.SourceToFinalPc[0x80016F74];
        uint[] expectedCalls=calls?(collectCall?[predicate.EntryPc,helper,collector.EntryPc]:[predicate.EntryPc,helper]):[];
        Check(cpu.Calls.SequenceEqual(expectedCalls),"Only the declared callable predicate, internal XY helper and optional collector may be called.");
        HashSet<uint> stores=[];
        if(calls){foreach(uint at in new[]{Sp-0x10,Sp-0xC,Sp-8,Sp-4,Sp-0x30,Sp-0x2C,Sp-0x28})for(uint i=0;i<4;i++)stores.Add(at+i);}
        if(expected==NativeJapaneseEggCollectionAttemptResult.Collected)
        {
            stores.UnionWith([Ledger+0x19,Ledger+0x7A,Ledger+0x7B,Ledger+0x7C,Ledger+0x7D,Ledger+0x7E,Ledger+0x7F,Ledger+0x90+row.BitIndex/8]);
            Check(cpu.Get(Ledger+0x90+row.BitIndex/8,1)==(uint)(ledger[0x90+row.BitIndex/8]|1<<(int)(row.BitIndex%8)),"Exact full-width stable bit set.");
            Check(cpu.Get(Ledger+0x7A,2)==BinaryPrimitives.ReadUInt16LittleEndian(ledger.AsSpan(0x7A))+1&&
                cpu.Get(Ledger+0x7C)==BinaryPrimitives.ReadUInt32LittleEndian(ledger.AsSpan(0x7C))+1&&cpu.Get(Ledger+0x19,1)==1,"Exactly one count/generation/dirty transition.");
        }
        Check(cpu.Writes.Count==stores.Count&&cpu.Writes.ToHashSet().SetEquals(stores),"Complete exact byte write set, including both declared stack frames.");
        Check(Enumerable.Range(0,0x200).Where(i=>!stores.Contains(Ledger+(uint)i)).All(i=>cpu.Get(Ledger+(uint)i,1)==ledger[i]),"Entire unowned ledger complement retained.");
        Check(cpu.Get(Ledger-4)==0x12345678&&cpu.Get(Ledger+0x200)==0x87654321&&cpu.Get(Sp-0x44)==0xCCCCCCCC&&cpu.Get(Sp)==0xCCCCCCCC,
            "Outer ledger and maximum-stack guards retained.");
        for(int n=0;n<bindings.Rows.Count;n++)
        {
            var r=bindings.Rows[n];Check(Enumerable.Range(0,0x58).All(i=>cpu.Get(r.RowAddress+(uint)i,1)==rowsBefore[n][i])&&
                Enumerable.Range(0,12).All(i=>cpu.Get(r.PrivateAddress+(uint)i,1)==privateBefore[n][i]),"No invented row retirement or private-state mutation.");
        }
        Check(!cpu.Reads.Any(p=>p>=Ledger&&p<Ledger+0x10),"Ledger guard never read.");
        HashSet<uint> fixedReads=[0x80078A58,0x80078A5C,0x80078A60,0x80078C4C,0x80078AD0,0x80078AD4,
            Sp-0x10,Sp-0xC,Sp-8,Sp-4,Sp-0x30,Sp-0x2C,Sp-0x28];
        foreach(var r in bindings.Rows)foreach(uint offset in new uint[]{0,0x36,0x48,0xC,0x10,0x14,0x38})fixedReads.Add(r.RowAddress+offset);
        Check(cpu.Reads.All(p=>fixedReads.Contains(p)||p>=Ledger+0x10&&p<Ledger+(uint)collector.LiveFootprintBytes),
            "All data reads belong to the exact bound row fields, player fields, ledger live extent or owned saved registers.");
        if(!calls)Check(!cpu.Reads.Any(p=>p>=Ledger&&p<Ledger+0x200||p>=0x80078A58&&p<=0x80078C4C),
            "Pre-call refusal reads no ledger or player state.");
        return cpu;
    }
    var empty=DetachedCollectibleSave.CreateNew(manifest);byte[] initial=F.Ledger(empty,1);
    var first=Execute("egg-a",initial,NativeJapaneseEggCollectionAttemptResult.Collected,true,true);
    byte[] after=Enumerable.Range(0,0x200).Select(i=>(byte)first.Get(Ledger+(uint)i,1)).ToArray();
    Execute("egg-a",after,NativeJapaneseEggCollectionAttemptResult.AlreadyEarned,true,true);
    var second=Execute("egg-b",after,NativeJapaneseEggCollectionAttemptResult.Collected,true,true);
    Check(second.Get(Ledger+0xAF,1)==0x80&&second.Get(Ledger+0xB0,1)==1&&second.Get(Ledger+0x7A,2)==2,"Bits255 and256 coexist without low-byte alias or cross-instance suppression.");
    foreach(uint ptr in new[]{0u,uint.MaxValue,bindings.Rows[0].RowAddress+1,bindings.Rows[0].RowAddress+4,0x801B0000})
        Check(Execute("egg-a",initial,NativeJapaneseEggCollectionAttemptResult.UnboundRow,false,false,pointer:ptr).Reads.Count==0,"Unbound pointer refused before any dereference.");
    foreach(uint ptr in new[]{0u,Ledger+1,Ledger+0x10,uint.MaxValue})
        Check(Execute("egg-a",initial,NativeJapaneseEggCollectionAttemptResult.InvalidLedger,false,false,ledgerPointer:ptr).Reads.Count==0,"Wrong ledger pointer refused before any dereference.");
    uint a=bindings.Rows.Single(r=>r.RowId=="egg-a").RowAddress;
    foreach(uint cls in new uint[]{0xF5,0xE9,0x1F2,0,0xFFFF})Execute("egg-a",initial,NativeJapaneseEggCollectionAttemptResult.InvalidRow,false,false,c=>c.Put(a+0x36,cls,2));
    Execute("egg-a",initial,NativeJapaneseEggCollectionAttemptResult.InvalidRow,false,false,c=>c.Put(a,bindings.Rows[1].PrivateAddress));
    foreach(uint live in new uint[]{1,0x7F,0x80,0xFF})Execute("egg-a",initial,NativeJapaneseEggCollectionAttemptResult.NotLive,false,false,c=>c.Put(a+0x48,live,1));
    foreach(Action<RenderProbeCpu> reject in new Action<RenderProbeCpu>[] {
        c=>c.Put(a+0xC,500),c=>c.Put(a+0x14,500),c=>c.Put(0x80078C4C,0x2000),
        c=>{c.Put(0x80078AD0,15);c.Put(0x80078AD4,1);} })
    {
        var cpu=Execute("egg-a",initial,NativeJapaneseEggCollectionAttemptResult.NotEligible,true,false,reject);
        Check(!cpu.Reads.Any(p=>p>=Ledger&&p<Ledger+0x200),"Predicate rejection does not even read the ledger.");
    }
    byte[] bad=initial.ToArray();bad[0x17]=0;Execute("egg-a",bad,NativeJapaneseEggCollectionAttemptResult.InvalidLedger,true,true);
    bad=initial.ToArray();bad[0x30]^=1;Execute("egg-a",bad,NativeJapaneseEggCollectionAttemptResult.InvalidLedger,true,true);
    bad=initial.ToArray();F.U16(bad,0x7A,1);Execute("egg-a",bad,NativeJapaneseEggCollectionAttemptResult.InvalidLedger,true,true);
    Execute("egg-a",F.Ledger(empty,1,uint.MaxValue),NativeJapaneseEggCollectionAttemptResult.GenerationOverflow,true,true);
    Execute("egg-a",F.Ledger(empty.WithCollected(1,F.Id(257)),1,uint.MaxValue),NativeJapaneseEggCollectionAttemptResult.AlreadyEarned,true,true);
    // Existing native trapping arithmetic must prevent collector entry; no claim
    // of stack unwinding or PS1 exception-handler execution follows this test.
    var trap=Cpu(initial);trap.Regs[4]=a;trap.Regs[5]=Ledger;trap.Regs[29]=Sp;trap.Regs[31]=Stop;trap.Put(a+0xC,0x80000000);
    try{trap.Run(attempt.EntryPc,Stop);throw new Exception("Expected native predicate arithmetic trap");}
    catch(RenderProbeArithmeticOverflow e)
    {
        Check(e.Pc==predicate.SourceToFinalPc[0x80016F84]&&!trap.Visited.Contains(collector.EntryPc),"Native predicate trap propagates before collector entry.");
        Check(trap.Writes.Count==28&&trap.Writes.All(p=>p>=Sp-0x40&&p<Sp)&&Enumerable.Range(0,0x200).All(i=>trap.Get(Ledger+(uint)i,1)==initial[i]),
            "Trapping path changes only declared caller/predicate saves; ledger remains exact.");
    }
    Refuse(()=>Compile(address:Code+1),"aligned");Refuse(()=>Compile(capacity:new(domain,0,4)),"capacity");
    Refuse(()=>Compile(address:0x801FFF00),"RAM");Refuse(()=>Compile(address:PredicateCode),"overlap");
    Refuse(()=>Compile(address:CollectorCode),"overlap");Refuse(()=>Compile(address:Ledger),"overlap");
    Refuse(()=>Compile(address:authored.Projection.ProjectedSceneRamStart),"overlap");
    Refuse(()=>Compile(address:authored.Bindings[0].ProjectedRoot),"overlap");
    Refuse(()=>Compile(address:0x80078A58),"overlap");
    Refuse(()=>Compile(capacity:new(new(NativeAddressKind.Scene,"wrong",NativeAddressRevision.Final,0x1000),0,0x1000)),"final code");
    var conflictingCollector=NativeCollectibleCollectionKernel.Compile(manifest,1,new(domain,0x2000,0x2000),PredicateCode,Ledger);
    Refuse(()=>Compile(k:conflictingCollector),"overlap");
    byte[] escaped=attempt.CopyBytes();escaped[0]^=1;Check(!escaped.SequenceEqual(attempt.CopyBytes()),"Caller machine code is immutable.");
    if(!source)
    {
        var moved=F.Authored([new("alpha",90000,70000,6000,3,1),new("zeta",80000,60000,5000,4,1)]);
        NativeJapaneseEggCollectibleIdentity[] movedMap=[new("zeta",F.Id(257)),new("alpha",F.Id(256))];
        var movedBinding=NativeJapaneseEggCollectibleBinding.Create(moved,manifest,movedMap);
        Check(movedBinding.Rows.Single(r=>r.InstanceId==F.Id(257)).BitIndex==256&&
            movedBinding.Rows.Single(r=>r.InstanceId==F.Id(257)).RowAddress!=bindings.Rows.Single(r=>r.InstanceId==F.Id(257)).RowAddress,
            "Explicit GUID survives moved/reordered/relabelled layout while native row address changes.");
        Refuse(()=>bindings.RequireCurrent(moved,manifest),"authored");Refuse(()=>Compile(current:moved),"authored");
        var reversed=NativeJapaneseEggCollectibleBinding.Create(authored,manifest,Mappings().Reverse().ToArray());
        Check(Compile(rows:reversed).CopyBytes().SequenceEqual(attempt.CopyBytes()),"Mapping input order does not select stable IDs.");
        var movedAttempt=NativeJapaneseEggCollectionAttempt.Compile(movedBinding,moved,manifest,predicate,collector,new(domain,0,0x1000),Code);
        Check(movedAttempt.ManifestSha256==attempt.ManifestSha256,"Recomposition retains exact existing manifest authority.");
        foreach(var movedRow in movedBinding.Rows)
        {
            var cpu=new RenderProbeCpu{StrictSignedArithmetic=true};cpu.Copy(Code,movedAttempt.CopyBytes());
            cpu.Copy(PredicateCode,predicate.CopyBytes());cpu.Copy(CollectorCode,collector.CopyBytes());cpu.Copy(Ledger,initial);
            cpu.Copy(moved.Projection.ProjectedSceneRamStart,moved.CopyScene());
            foreach(var r in movedBinding.Rows)cpu.Put(r.RowAddress,r.PrivateAddress);
            cpu.Put(0x80078A58,cpu.Get(movedRow.RowAddress+0xC));cpu.Put(0x80078A5C,cpu.Get(movedRow.RowAddress+0x10));
            cpu.Put(0x80078A60,cpu.Get(movedRow.RowAddress+0x14));cpu.Put(movedRow.RowAddress+0x38,0,2);
            cpu.Regs[4]=movedRow.RowAddress;cpu.Regs[5]=Ledger;cpu.Regs[29]=Sp;cpu.Regs[31]=Stop;
            cpu.Run(movedAttempt.EntryPc,Stop);
            Check(cpu.Regs[2]==(uint)NativeJapaneseEggCollectionAttemptResult.Collected&&
                cpu.Get(Ledger+0x90+movedRow.BitIndex/8,1)==1u<<(int)(movedRow.BitIndex%8)&&cpu.Get(Ledger+0x7A,2)==1,
                "Actual relocated/reordered caller collects the same persistent GUID bit at its new native row address.");
            var other=movedBinding.Rows.Single(r=>r.InstanceId!=movedRow.InstanceId);
            Check(cpu.Get(Ledger+0x90+other.BitIndex/8,1)==0&&cpu.Writes.Count(p=>p>=Ledger&&p<Ledger+0x200)==8,
                "Reordered row collection leaves the other identity independent with exactly eight ledger stores.");
        }
    }
    else
    {
        Refuse(()=>Compile(p:F.Predicate().Compile(new(domain,0x1000,0x154),PredicateCode)),"ancestry");
        var fixture=F.Authored();var fixtureBinding=NativeJapaneseEggCollectibleBinding.Create(fixture,manifest,Mappings());
        Refuse(()=>NativeJapaneseEggCollectionAttempt.Compile(fixtureBinding,fixture,manifest,predicate,collector,new(domain,0,0x1000),Code),"ancestry");
    }
    Console.WriteLine($"JP_EGG_COLLECTION_ATTEMPT_{(source?"SOURCE":"SOURCE_FREE")}_OK checks={checks} codeBytes={attempt.CopyBytes().Length:X} stack=40 nativeRowWrites=0 dispatch=false save=false");
}
if(args.Length==0)Verify(F.Authored(),F.Predicate(),false);
else if(args.Length==3&&args[0]=="--source")
{
    using var jp=NativeRegionalSourceSession.Open(args[1],NativeDiscRevision.JapanScps10083);
    using var usa=NativeRegionalSourceSession.Open(args[2],NativeDiscRevision.UsaScus94228);
    try{Verify(F.Authored(jp,usa),NativeJapaneseEggPickupPredicateSource.Capture(jp,usa),true);}
    finally{jp.VerifyUnchanged();usa.VerifyUnchanged();}
}
else throw new ArgumentException("Usage: no arguments, or --source <exact-JP-BIN> <exact-USA-BIN>.");

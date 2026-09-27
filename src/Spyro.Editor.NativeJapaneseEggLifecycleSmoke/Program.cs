using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using F=AttemptFixtures;

int checks=0;
void Check(bool value,string why){if(!value)throw new Exception(why);checks++;}
void Refuse(Action action){try{action();}catch(InvalidDataException){checks++;return;}throw new Exception("Expected refusal");}
const uint Code=0x800D0000,AttemptPc=0x800E0000,PredicatePc=0x800F0000,CollectorPc=0x80100000,QueryPc=0x80110000,
    Ledger=0x801FF600,Sp=0x801FFC00,Stop=0x80010000;
byte[] retirement=F.Words(0x84850034,0x3C018007,0x24215778,0x04A0000C,0x8C210000,0x00052880,0x00A12820,
    0x20A2FFFC,0x20450004,0x8CA20000,0,0x1444FFFC,0,0x8C820004,0,0xACA20000,0x3C018007,
    0x24215890,0x8C210000,0,0x00240822,0x18200003,0x2002FFFD,0x03E00008,0xA0820048);
var manifest=F.Manifest(257);var space=new NativeAddressSpace(NativeAddressKind.LevelData,"lifecycle-test",NativeAddressRevision.Final,0x10000);
void Verify(NativeJapaneseEggAuthoredClassesPlan authored,NativeJapaneseEggPickupPredicateSource predicateSource,bool source)
{
    var binding=NativeJapaneseEggCollectibleBinding.Create(authored,manifest,[new("egg-a",F.Id(257)),new("egg-b",F.Id(256))]);
    var predicate=predicateSource.Compile(new(space,0x2000,0x154),PredicatePc);
    var collector=NativeCollectibleCollectionKernel.Compile(manifest,1,new(space,0x4000,0x2000),CollectorPc,Ledger);
    var query=NativeCollectibleStateQueryKernel.Compile(manifest,1,new(space,0x6000,0x2000),QueryPc,Ledger);
    var attempt=NativeJapaneseEggCollectionAttempt.Compile(binding,authored,manifest,predicate,collector,new(space,0x1000,0x1000),AttemptPc);
    NativeJapaneseEggLifecycleCallables Compile(NativeByteExtent? capacity=null,uint pc=Code,NativeCollectibleStateQueryKernel? q=null)=>source
        ? NativeJapaneseEggLifecycleCallables.Compile(authored,manifest,attempt,predicate,collector,q??query,capacity??new(space,0,0x1000),pc)
        : NativeJapaneseEggLifecycleCallables.CompileFixture(authored,manifest,attempt,predicate,collector,q??query,capacity??new(space,0,0x1000),pc);
    var plan=Compile();var empty=DetachedCollectibleSave.CreateNew(manifest);
    Check(plan.SourceBound==source&&!plan.DispatchActivated&&!plan.NativeResidenceIssued&&!plan.PersistentSaveChanges&&!plan.PublicationApproved,
        "Detached callable is not native installation or saving.");
    Check(plan.StackByteLength==0x20&&plan.MaximumStackByteLength==0x60,"Lifecycle frame plus sequential attempt/predicate stack.");
    uint dynamicBase=binding.Rows.Max(r=>r.RowAddress)+0x58;
    RenderProbeCpu Execute(bool pre,DetachedCollectibleSave state,NativeJapaneseEggLifecycleResult expected,string rowId="egg-a",
        Action<RenderProbeCpu>? change=null,uint? pointer=null,uint ledgerPointer=Ledger,uint generation=0,RenderProbeCpu? reuse=null)
    {
        var row=binding.Rows.Single(r=>r.RowId==rowId);var cpu=reuse??new RenderProbeCpu{StrictSignedArithmetic=true};
        if(reuse==null)
        {
            cpu.Copy(Code,plan.CopyBytes());cpu.Copy(AttemptPc,attempt.CopyBytes());cpu.Copy(PredicatePc,predicate.CopyBytes());
            cpu.Copy(CollectorPc,collector.CopyBytes());cpu.Copy(QueryPc,query.CopyBytes());cpu.Copy(0x80052568,retirement);
            ReloadRows(cpu);cpu.Copy(Ledger,F.Ledger(state,1,generation));
            cpu.Put(0x80075890,dynamicBase);cpu.Put(0x80075778,0);
            foreach(uint address in new uint[]{0x80078A58,0x80078A5C,0x80078A60,0x80078C4C,0x80078AD0,0x80078AD4})cpu.Put(address,0);
        }
        cpu.Copy(Sp-0x70,Enumerable.Repeat((byte)0xCC,0x80).ToArray());
        for(int i=1;i<32;i++)cpu.Regs[i]=0xBA000000+(uint)i;
        cpu.Regs[4]=pointer??row.RowAddress;cpu.Regs[5]=ledgerPointer;cpu.Regs[29]=Sp;cpu.Regs[31]=Stop;
        cpu.Hi=0x12345678;cpu.Lo=0x87654321;change?.Invoke(cpu);
        byte[] initial=Enumerable.Range(0,0x200).Select(i=>(byte)cpu.Get(Ledger+(uint)i,1)).ToArray();
        byte[][] rows=binding.Rows.Select(r=>Enumerable.Range(0,0x58).Select(i=>(byte)cpu.Get(r.RowAddress+(uint)i,1)).ToArray()).ToArray();
        uint[] registers=cpu.Regs.ToArray();cpu.Reads.Clear();cpu.Writes.Clear();cpu.Calls.Clear();
        cpu.Run(pre?plan.BeforeVisualEntryPc:plan.AfterVisualEntryPc,Stop);
        Check(cpu.Regs[2]==(uint)expected,"Lifecycle result "+expected+" got "+cpu.Regs[2]);
        Check(new[]{4,5,16,17,18,19,20,21,22,23,26,27,28,29,30,31}.All(r=>cpu.Regs[r]==registers[r])&&cpu.Hi==0x12345678&&cpu.Lo==0x87654321,
            "Both lifecycle entries preserve arguments/saved/kernel/SP/RA/HI/LO.");
        bool collected=expected==NativeJapaneseEggLifecycleResult.Collected;
        bool retired=collected||expected is NativeJapaneseEggLifecycleResult.Suppressed or NativeJapaneseEggLifecycleResult.AlreadyEarned;
        Check(cpu.Calls.Contains(0x80052568)==retired&&!cpu.Calls.Contains(0x8003B9D4),"Only admitted results call placed delete, never generic kill.");
        Check(cpu.Writes.All(p=>p>=Sp-0x60&&p<Sp||retired&&p==row.RowAddress+0x48||collected&&
            (p==Ledger+0x19||p>=Ledger+0x7A&&p<Ledger+0x80||p==Ledger+0x90+row.BitIndex/8)),"Every written byte has an explicit stack/ledger/row owner.");
        for(int n=0;n<binding.Rows.Count;n++)Check(Enumerable.Range(0,0x58).All(i=>cpu.Get(binding.Rows[n].RowAddress+(uint)i,1)==
            (retired&&binding.Rows[n]==row&&i==0x48?0xFD:rows[n][i])),"Only selected row live-state changes; other fields/instance retained.");
        if(!collected)Check(Enumerable.Range(0,0x200).All(i=>cpu.Get(Ledger+(uint)i,1)==initial[i]),"Refusal/suppression never changes collection state.");
        else Check(cpu.Get(Ledger+0x90+row.BitIndex/8,1)==(uint)(initial[0x90+row.BitIndex/8]|1<<((int)row.BitIndex%8))&&
            cpu.Get(Ledger+0x7A,2)==state.EarnedActiveInstances(1).Count+1,"Full-width chosen identity counted exactly once.");
        Check(cpu.Get(Sp-0x64)==0xCCCCCCCC&&cpu.Get(Sp)==0xCCCCCCCC,"Full nested stack outer canaries retained.");
        if(pre)Check(!cpu.Calls.Contains(predicate.EntryPc)&&!cpu.Calls.Contains(collector.EntryPc)&&
            !cpu.Reads.Any(p=>p>=0x80078A58&&p<=0x80078C4C),"Previsual decision never asks proximity or collects.");
        if(expected is NativeJapaneseEggLifecycleResult.UnboundRow or NativeJapaneseEggLifecycleResult.InvalidRow or NativeJapaneseEggLifecycleResult.NotLive || ledgerPointer!=Ledger)
        {
            Check(cpu.Calls.Count==0,"Structural refusal precedes every child callable and helper.");
            Check(cpu.Reads.All(p=>p>=row.RowAddress&&p<row.RowAddress+0x58||p>=0x80075890&&p<0x80075894),
                "Structural refusal reads only a bound row and the dynamic boundary, never invalid pointers or a ledger.");
        }
        return cpu;
    }
    void ReloadRows(RenderProbeCpu cpu)
    {
        cpu.Copy(authored.Projection.ProjectedSceneRamStart,authored.CopyScene());
        foreach(var r in binding.Rows){cpu.Put(r.RowAddress,r.PrivateAddress);cpu.Put(r.RowAddress+0x34,0xFFFF,2);
            cpu.Put(r.RowAddress+0xC,0);cpu.Put(r.RowAddress+0x10,0);cpu.Put(r.RowAddress+0x14,0);cpu.Put(r.RowAddress+0x38,0,2);}
    }
    Execute(true,empty,NativeJapaneseEggLifecycleResult.Available);
    var earned=empty.WithCollected(1,F.Id(257));
    Execute(true,earned,NativeJapaneseEggLifecycleResult.Suppressed,change:c=>c.Put(0x80078A58,0x100000));
    Execute(false,empty,NativeJapaneseEggLifecycleResult.Collected);
    Execute(false,empty,NativeJapaneseEggLifecycleResult.Collected,"egg-b");
    Execute(false,earned,NativeJapaneseEggLifecycleResult.AlreadyEarned);
    Execute(false,empty,NativeJapaneseEggLifecycleResult.NotEligible,change:c=>c.Put(0x80078A58,0x100000));
    Execute(false,empty,NativeJapaneseEggLifecycleResult.GenerationOverflow,generation:uint.MaxValue);
    Execute(true,empty,NativeJapaneseEggLifecycleResult.Available,"egg-b");
    Execute(true,empty.WithCollected(1,F.Id(256)),NativeJapaneseEggLifecycleResult.Suppressed,"egg-b");
    var sequence=Execute(false,empty,NativeJapaneseEggLifecycleResult.Collected);
    Execute(false,earned,NativeJapaneseEggLifecycleResult.NotLive,reuse:sequence);
    Execute(true,earned,NativeJapaneseEggLifecycleResult.Available,"egg-b",reuse:sequence);
    Execute(false,earned,NativeJapaneseEggLifecycleResult.Collected,"egg-b",reuse:sequence);
    var both=earned.WithCollected(1,F.Id(256));
    ReloadRows(sequence);sequence.Put(0x80078A58,0x100000);
    Execute(true,both,NativeJapaneseEggLifecycleResult.Suppressed,reuse:sequence);
    Execute(true,both,NativeJapaneseEggLifecycleResult.Suppressed,"egg-b",reuse:sequence);
    Execute(false,both,NativeJapaneseEggLifecycleResult.NotLive,"egg-b",reuse:sequence);
    foreach(bool pre in new[]{true,false})
    {
        Execute(pre,empty,NativeJapaneseEggLifecycleResult.UnboundRow,pointer:0);
        Execute(pre,empty,NativeJapaneseEggLifecycleResult.InvalidLedger,ledgerPointer:Ledger+0x10);
        Execute(pre,empty,NativeJapaneseEggLifecycleResult.InvalidRow,change:c=>c.Put(binding.Rows[0].RowAddress,0));
        Execute(pre,empty,NativeJapaneseEggLifecycleResult.InvalidRow,change:c=>c.Put(binding.Rows[0].RowAddress+0x36,0xF5,2));
        Execute(pre,empty,NativeJapaneseEggLifecycleResult.NotLive,change:c=>c.Put(binding.Rows[0].RowAddress+0x48,0xFD,1));
        Execute(pre,empty,NativeJapaneseEggLifecycleResult.InvalidRow,change:c=>c.Put(binding.Rows[0].RowAddress+0x34,0,2));
        Execute(pre,empty,NativeJapaneseEggLifecycleResult.InvalidRow,change:c=>c.Put(binding.Rows[0].RowAddress+0x34,0xFFFE,2));
        Execute(pre,empty,NativeJapaneseEggLifecycleResult.InvalidRow,change:c=>c.Put(0x80075890,dynamicBase-0x58));
        Execute(pre,empty,NativeJapaneseEggLifecycleResult.InvalidRow,change:c=>c.Put(0x80075890,dynamicBase+0x58));
        Execute(pre,empty,NativeJapaneseEggLifecycleResult.InvalidLedger,change:c=>c.Put(Ledger+0x16,0,1));
    }
    Refuse(()=>Compile(pc:Code+1));Refuse(()=>Compile(new(space,0,4)));
    foreach(uint address in new[]{QueryPc,Ledger,AttemptPc,PredicatePc,CollectorPc,binding.SceneAddress,binding.ModelAddress,0x80052568u,0x80075890u,0x80078A58u,0x801FFFFCu})
        Refuse(()=>Compile(pc:address));
    Refuse(()=>Compile(q:NativeCollectibleStateQueryKernel.Compile(manifest,0,new(space,0x6000,0x2000),QueryPc,Ledger)));
    Refuse(()=>Compile(q:NativeCollectibleStateQueryKernel.Compile(manifest,1,new(space,0x6000,0x2000),QueryPc,Ledger-0x200)));
    Refuse(()=>Compile(q:NativeCollectibleStateQueryKernel.Compile(F.Manifest(2),1,new(space,0x6000,0x2000),QueryPc,Ledger)));
    Refuse(()=>Compile(q:NativeCollectibleStateQueryKernel.Compile(manifest,1,new(space,0x6000,0x2000),AttemptPc,Ledger)));
    Refuse(()=>Compile(new(new(NativeAddressKind.LevelData,"original",NativeAddressRevision.Original,0x1000),0,0x1000)));
    if(source) Refuse(()=>NativeJapaneseEggLifecycleCallables.CompileFixture(authored,manifest,attempt,predicate,collector,query,new(space,0,0x1000),Code));
    else Refuse(()=>NativeJapaneseEggLifecycleCallables.Compile(authored,manifest,attempt,predicate,collector,query,new(space,0,0x1000),Code));
    byte[] bytes=plan.CopyBytes();bytes[0]^=1;Check(!bytes.SequenceEqual(plan.CopyBytes()),"Immutable emitted code.");
    Console.WriteLine($"LIFECYCLE_CODE source={source} bytes={plan.CopyBytes().Length:X} before={plan.BeforeVisualEntryPc:X8} after={plan.AfterVisualEntryPc:X8} dynamic={plan.DynamicBase:X8}");
}
Verify(F.Authored(),F.Predicate(),false);
Console.WriteLine($"JP_EGG_LIFECYCLE_SOURCE_FREE_OK checks={checks} dispatch=false save=false");
if(args.Length==0)return;
if(args.Length!=3||args[0]!="--source")throw new ArgumentException("--source JP USA");
using var jp=NativeRegionalSourceSession.Open(args[1],NativeDiscRevision.JapanScps10083);
using var usa=NativeRegionalSourceSession.Open(args[2],NativeDiscRevision.UsaScus94228);
try{Verify(F.Authored(jp,usa),NativeJapaneseEggPickupPredicateSource.Capture(jp,usa),true);
    Console.WriteLine($"JP_EGG_LIFECYCLE_SOURCE_OK checks={checks} dispatch=false save=false");}
finally{try{jp.VerifyUnchanged();}finally{usa.VerifyUnchanged();}}

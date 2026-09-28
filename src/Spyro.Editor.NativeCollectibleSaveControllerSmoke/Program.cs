using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks=0;
void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
MergeMemoryCases.Run(Check);
int mergeMemoryChecks=checks;checks=0;
void Refuse(Action action,string why)
{try{action();}catch(Exception e)when(e is InvalidDataException or ArgumentException){checks++;return;}throw new Exception("Expected refusal: "+why);}
var k=NativeCollectibleSaveControllerKernel.CreateFixture(0x800C0000);
Check(!k.SourceCertified&&!k.NativeIoExecuted&&!k.RuntimeVerified&&!k.HooksInstalled&&!k.NativeResidenceIssued&&
    !k.NativeSessionIdentityAuthenticated&&!k.CancellationApproved&&!k.PublicationApproved,"Detached code never issues runtime/session/I/O/lease authority");
Refuse(k.RequireSourceEvidence,"Fixture cannot claim source pins");Refuse(k.RequireNativeInstallation,"Detached controller cannot install itself");
foreach(uint at in new uint[]{0,0x80080000,0x800C0001,0x801EF000,0x801FF600,0xFFFFFFF0})
    Refuse(()=>NativeCollectibleSaveControllerKernel.CreateFixture(at),"Complete checked detached layout");
Check(k.Regions.First().Address==k.ArenaAddress&&k.Regions.Zip(k.Regions.Skip(1)).All(p=>p.First.Address+p.First.ByteLength==p.Second.Address)&&
    k.Regions[^1].Address+k.Regions[^1].ByteLength==k.ArenaAddress+NativeCollectibleSaveControllerKernel.ArenaBytes,"Every code/input/state/buffer byte has one disjoint owner");
void VerifySuccess(ControllerFixture f,byte[]? newer=null)
{
    Check(f.Stage==NativeCollectibleSaveControllerStage.Committed,"Actual emitted controller reaches detached commit");
    var prepared=NativeCollectiblePreparedSaveTail.Inspect(f.Kernel.Preparation,f.OriginalCard.AsSpan(0x1400,0x100),
        f.Bytes(f.Kernel.Preparation.OutputAddress,0x340),f.InitialLedger);
    var s=new NativeCollectibleSaveSelection(0xA00,1,0,0,0xABCD);
    var reference=NativeCollectibleSaveTransaction.Begin(f.Kernel.Preparation,f.Ordinary,f.InitialLedger,s);
    while(reference.Stage!=NativeCollectibleSaveStage.Verified)
    {
        var r=reference.Request(f.InitialLedger,s,0,0);reference.Accepted(1,0,0);
        reference.Poll(1,r.Operation,0,0,0,r.Operation==4?[]:r.FileOffset==0x200?f.Ordinary:
            reference.Stage==NativeCollectibleSaveStage.ReadingOldTail?f.OriginalCard.AsSpan(0x1400,0x100).ToArray():prepared.CopyTail(),f.InitialLedger,s);
        if(reference.Stage==NativeCollectibleSaveStage.NeedPreparation)reference.Prepared(prepared);
    }
    byte[] expected=reference.Commit(newer??f.InitialLedger,s);
    Check(f.Bytes(f.Kernel.ProposalAddress,512).SequenceEqual(expected),"Actual emitted proposal equals independently replayed reference model");
    Check(f.Game.Ledger().SequenceEqual(newer??f.InitialLedger),"No live ledger or native success7 publication");
    Check(f.Cpu.Get(0x80078D0C)==0&&f.Cpu.Get(f.Kernel.StateAddress+0x1C)==NativeCollectibleSaveControllerKernel.ProposalMagic,"Only detached proposal marker published");
    Check(f.Cpu.Writes.TakeLast(8).SequenceEqual(Enumerable.Range(0,4).Select(i=>f.Kernel.StateAddress+0x1Cu+(uint)i)
        .Concat(Enumerable.Range(0,4).Select(i=>f.Kernel.StateAddress+(uint)i))),"Complete proposal precedes marker, and committed stage publishes last");
    Check(f.Card.AsSpan(0x200,0x600).SequenceEqual(f.Ordinary)&&f.Card.AsSpan(0x1400,0x100).SequenceEqual(prepared.CopyTail()),"Both complete expected images reached simulated card");
    Check(Enumerable.Range(0,0x2000).All(i=>i is >=0x200 and <0x800 or >=0x1400 and <0x1483||f.Card[i]==f.OriginalCard[i]),"Every other slot/header/opaque suffix byte preserved");
    Check(f.Requests.Select(r=>(r.Op,r.Offset,r.Count)).SequenceEqual(new[]{(3u,0x1400,0x100),(4u,0x200,0x600),(3u,0x200,0x600),(4u,0x1400,0x100),(3u,0x1400,0x100)}),"Exact five-request source ABI sequence");
}
var good=new ControllerFixture(k);good.Invoke(k.BeginPc);
for(int step=0;step<5;step++)
{
    var state=good.Stage;good.PendingPoll();good.Invoke(k.PumpPc);
    Check(good.Stage==state&&good.Cpu.Calls.Count(pc=>pc==NativeCollectibleSaveControllerKernel.PollPc)==1,"Pending is one nonblocking poll without duplicate request");
    good.Complete();good.Invoke(k.PumpPc);
}
VerifySuccess(good);
foreach(var state in new[]{NativeCollectibleSaveControllerStage.ReadingOldTail,NativeCollectibleSaveControllerStage.Committed,
    NativeCollectibleSaveControllerStage.Failed,NativeCollectibleSaveControllerStage.Revoked})
{
    var f=new ControllerFixture(k);
    if(state==NativeCollectibleSaveControllerStage.Committed)f.Reach(state);
    else{f.Invoke(k.BeginPc);if(state==NativeCollectibleSaveControllerStage.Failed){f.ConfigurePoll(-1,3,0,0,0);f.Invoke(k.PumpPc);}
        if(state==NativeCollectibleSaveControllerStage.Revoked)f.Invoke(k.RevokePc);}
    int requests=f.Requests.Count;byte[] before=f.Bytes(k.StateAddress,0x30);
    f.Invoke(k.BeginPc);Check(f.Stage==state&&f.Requests.Count==requests&&f.Bytes(k.StateAddress,0x30).SequenceEqual(before),"Repeated Begin never resets/reissues pending or terminal state");
}
foreach(int rejected in new[]{0,-1,2})
{var f=new ControllerFixture(k){RequestReturn=rejected};f.Invoke(k.BeginPc);Check(f.Stage==NativeCollectibleSaveControllerStage.Failed&&f.Pending is null,"Rejected request never becomes owned pending");}
foreach(var(ret,op,result,busy,latch)in new(int,uint,uint,uint,uint)[]{(-1,3,0,0,0),(2,3,0,0,0),(1,4,0,0,0),(1,3,5,0,0),(1,3,0,3,0),(1,3,0,0,1),(0,3,0,4,0),(0,3,0,3,1)})
{var f=new ControllerFixture(k);f.Invoke(k.BeginPc);f.ConfigurePoll(ret,op,result,busy,latch);f.Invoke(k.PumpPc);Check(f.Stage==NativeCollectibleSaveControllerStage.Failed,"Idle/stale/wrong-op/result/ownership fails closed");}
foreach(var(stage,count)in new[]{(NativeCollectibleSaveControllerStage.ReadingOrdinary,0x600),(NativeCollectibleSaveControllerStage.ReadingTail,0x100)})
{
    // Each known expected byte is poisoned with its complement before native readback.
    for(int at=0;at<count;at++)
    {
        var f=new ControllerFixture(k);f.Reach(stage);var pending=f.Pending!.Value;
        byte[] expected=f.Card.AsSpan(pending.Offset,count).ToArray();
        Check(f.Bytes(pending.Buffer,count).Zip(expected).All(p=>p.First==(byte)~p.Second),"Full readback complement prefill");
        f.Complete(mutate:b=>b[at]^=1);f.Invoke(k.PumpPc);
        Check(f.Stage==NativeCollectibleSaveControllerStage.Failed&&f.Cpu.Get(k.StateAddress+0x1C)==0,"Every ordinary/tail readback byte is exact before proposal");
    }
    var shortRead=new ControllerFixture(k);shortRead.Reach(stage);shortRead.Complete(copyBytes:count-1);shortRead.Invoke(k.PumpPc);
    Check(shortRead.Stage==NativeCollectibleSaveControllerStage.Failed,"Complement catches skipped final byte even when native receipt says success");
}
{var f=new ControllerFixture(k);f.Reach(NativeCollectibleSaveControllerStage.ReadingOrdinary);f.Complete(mutate:b=>(b[0],b[1])=(b[1],b[0]));f.Invoke(k.PumpPc);
 Check(f.Stage==NativeCollectibleSaveControllerStage.Failed,"Equal-checksum ordinary difference refuses");}
{var f=new ControllerFixture(k);f.Invoke(k.BeginPc);f.Game.Pickup(1);f.Complete();f.Invoke(k.PumpPc);
 Check(f.Stage==NativeCollectibleSaveControllerStage.Failed&&f.Requests.Count==1&&f.Card.SequenceEqual(f.OriginalCard),"Pre-preparation pickup conservatively refuses before any write");}
{var f=new ControllerFixture(k);f.Reach(NativeCollectibleSaveControllerStage.WritingOrdinary);f.Game.Pickup(1);byte[] newer=f.Game.Ledger();
 f.PendingPoll();f.Invoke(k.PumpPc);f.Reach(NativeCollectibleSaveControllerStage.Committed);VerifySuccess(f,newer);
 Check(f.Bytes(k.ProposalAddress,512)[0x19]==1,"Actual later pickup stays dirty");}
foreach(int checkpoint in Enumerable.Range(0,7))
{
    var f=new ControllerFixture(k);if(checkpoint>0)f.Reach((NativeCollectibleSaveControllerStage)checkpoint);
    f.Invoke(k.RevokePc);int requests=f.Requests.Count;f.Invoke(k.RevokePc);
    // Recreate identical admission bytes, not a claimed unique native session.
    var again=new SaveCpuFixture(0);again.Pickup(0);Check(again.Ledger().SequenceEqual(f.InitialLedger),"Opaque epoch/ledger can repeat after admission");
    f.Cpu.Copy(SaveCpuFixture.Live,again.Ledger());f.Invoke(k.BeginPc);f.Invoke(k.PumpPc);
    Check(f.Stage==NativeCollectibleSaveControllerStage.Revoked&&f.Requests.Count==requests&&f.Cpu.Calls.Count==0&&f.Cpu.Get(k.StateAddress+0x1C)==0,"Revocation is terminal and never drains/cancels or releases native success");
}
foreach(uint sp in new uint[]{0,1,k.ArenaAddress+0x100,k.OrdinaryInputAddress,k.StateAddress,0x801FF8D8,0x801FFFC0,0x80200000})
{var f=new ControllerFixture(k);f.Invoke(k.BeginPc,c=>c.Regs[29]=sp);Check(f.Cpu.Writes.Count==0&&f.Cpu.Calls.Count==0,"Unsafe full nested/caller stack refuses without writes or calls");}
foreach(uint sp in new uint[]{0x801FF8E0,0x801FFFB8})
{var f=new ControllerFixture(k);f.Invoke(k.BeginPc,c=>c.Regs[29]=sp);Check(f.Stage==NativeCollectibleSaveControllerStage.ReadingOldTail,"Exact stack endpoints accepted");}
{var f=new ControllerFixture(k);f.Invoke(k.BeginPc);byte[] state=f.Bytes(k.StateAddress,0x30);f.PendingPoll();
 Check(f.Invoke(k.PumpPc,c=>c.Regs[29]=k.StateAddress)==9&&f.Cpu.Writes.Count==0&&f.Cpu.Calls.Count==0&&f.Bytes(k.StateAddress,0x30).SequenceEqual(state),
     "Hostile-stack Pump refusal preserves the accepted pending owner; it is not cancellation");}
foreach(int reg in new[]{4,5,6,7})foreach(uint entry in new[]{k.BeginPc,k.PumpPc,k.RevokePc})
{var f=new ControllerFixture(k);uint result=f.Invoke(entry,c=>c.Regs[reg]++);Check(result==9&&f.Cpu.Writes.Count==0&&f.Cpu.Calls.Count==0,"Every entry rejects wrong exact ABI before writes/calls");}
foreach(var(address,value,width)in new(uint,uint,int)[]{(0x800755A4,0x800,4),(0x80078D14,0,4),(0x80078D18,16,4),(0x80078D1C,1,4),(0x80078D20,0x1ABCD,4),
    (0x80075B50,3,4),(0x80075B58,1,4),(SaveCpuFixture.Live+0x16,0,1),(SaveCpuFixture.Live+0x19,0,1),
    (SaveCpuFixture.Live+0x7C,0,4),(SaveCpuFixture.Live+0x80,2,4),(SaveCpuFixture.Live+0x8C,0,4),(SaveCpuFixture.Live+0xB0,1,4)})
{var f=new ControllerFixture(k);f.Cpu.Put(address,value,width);f.Invoke(k.BeginPc);
 Check(f.Stage==NativeCollectibleSaveControllerStage.Failed&&f.Requests.Count==0,"Invalid native/live ownership refuses before requesting");}
foreach(int at in new[]{0,0x10,0x30,0x60,0x62,0x63,0x82})
{var f=new ControllerFixture(k);f.Invoke(k.BeginPc);f.Complete(mutate:b=>b[at]^=1);f.Invoke(k.PumpPc);
 Check(f.Stage==NativeCollectibleSaveControllerStage.Failed&&f.Requests.Count==1,"Unknown/corrupt old envelope never manufactures empty progress or writes");}
foreach(var(stage,operation)in new[]{(NativeCollectibleSaveControllerStage.ReadingOldTail,3u),(NativeCollectibleSaveControllerStage.WritingOrdinary,4u),
    (NativeCollectibleSaveControllerStage.ReadingOrdinary,3u),(NativeCollectibleSaveControllerStage.WritingTail,4u),(NativeCollectibleSaveControllerStage.ReadingTail,3u)})
{
    foreach(int refused in new[]{0,-1,2})
    {
        var f=new ControllerFixture(k);f.Reach(stage);f.RequestReturn=refused;f.Complete();f.Invoke(k.PumpPc);
        Check(stage==NativeCollectibleSaveControllerStage.ReadingTail?f.Stage==NativeCollectibleSaveControllerStage.Committed:
            f.Stage==NativeCollectibleSaveControllerStage.Failed,"Every subsequent request requires exact accepted1; final readback has no new request");
    }
    foreach(uint wrongOp in new[]{0u,operation==3?4u:3u})
    {var f=new ControllerFixture(k);f.Reach(stage);f.Complete(op:wrongOp);int requested=f.Requests.Count;f.Invoke(k.PumpPc);
     Check(f.Stage==NativeCollectibleSaveControllerStage.Failed&&f.Requests.Count==requested,"Every completion checks exact owned operation before progression");}
}
// Per-observation high-water is required even when the complete old Begin
// snapshot and every scalar identity field remain otherwise valid.
foreach(bool complete in new[]{false,true})
{
    var f=new ControllerFixture(k);f.Reach(NativeCollectibleSaveControllerStage.WritingOrdinary);f.Game.Pickup(1);
    f.PendingPoll();f.Invoke(k.PumpPc);f.Cpu.Copy(SaveCpuFixture.Live,f.InitialLedger);
    if(complete)f.Complete();else f.PendingPoll();f.Invoke(k.PumpPc);
    Check(f.Stage==NativeCollectibleSaveControllerStage.Failed&&f.Cpu.Calls.All(pc=>pc!=NativeCollectibleSaveControllerKernel.PollPc),"Observed generation2→1 fails before consuming another completion");
}
foreach(var(generation,bits,earned)in new(uint,byte,ushort)[]{(1,3,2),(2,1,1),(3,1,1)})
{
    var f=new ControllerFixture(k);f.Reach(NativeCollectibleSaveControllerStage.WritingOrdinary);f.Game.Pickup(1);f.PendingPoll();f.Invoke(k.PumpPc);
    f.Cpu.Put(SaveCpuFixture.Live+0x7C,generation);f.Cpu.Put(SaveCpuFixture.Live+0x90,bits,1);f.Cpu.Put(SaveCpuFixture.Live+0x7A,earned,2);
    f.PendingPoll();f.Invoke(k.PumpPc);Check(f.Stage==NativeCollectibleSaveControllerStage.Failed,"No generation rollback, same-generation bit change, or later-generation loss of observed bits");
}
foreach(uint address in new[]{SaveCpuFixture.Live+0x16,SaveCpuFixture.Live+0x50,SaveCpuFixture.Live+0x80,SaveCpuFixture.Live+0x8C,0x80078D20})
{
    var f=new ControllerFixture(k);f.Reach(NativeCollectibleSaveControllerStage.ReadingTail);f.Complete();
    f.BeforeServiceReturn=x=>x.Cpu.Put(address,x.Cpu.Get(address)^1);f.Invoke(k.PumpPc);
    Check(f.Stage==NativeCollectibleSaveControllerStage.Failed&&f.Cpu.Get(k.StateAddress+0x1C)==0,"Post-callback tuple/phase/digest/transaction-generation/epoch changes cannot publish a proposal");
}
foreach(bool begin in new[]{true,false})
{
    var f=new ControllerFixture(k);if(!begin){f.Invoke(k.BeginPc);f.Complete();}
    // Direct signal models a resident external owner; no such native owner is
    // installed. Its in-flight native request may still complete afterward.
    f.BeforeServiceReturn=x=>{x.Cpu.Put(k.StateAddress+0x14,1);x.Cpu.Put(k.StateAddress,8);};
    f.Invoke(begin?k.BeginPc:k.PumpPc);
    Check(f.Stage==NativeCollectibleSaveControllerStage.Revoked&&f.Cpu.Get(k.StateAddress+0x1C)==0&&f.Requests.Count<=1,"Native return cannot overwrite external revocation or start a later request");
}
{var f=new ControllerFixture(k);f.Invoke(k.RevokePc,c=>c.Regs[29]=k.StateAddress);
 Check(f.Cpu.Calls.Count==0&&f.Cpu.Writes.SequenceEqual(new[]{k.StateAddress+0x14,k.StateAddress+0x15,k.StateAddress+0x16,k.StateAddress+0x17,
     k.StateAddress+0x1C,k.StateAddress+0x1D,k.StateAddress+0x1E,k.StateAddress+0x1F,k.StateAddress,k.StateAddress+1,k.StateAddress+2,k.StateAddress+3}),
     "Unframed revoke intentionally writes only its three owned words, even if SP aliases state; no cancellation");}
// Exact original queue helper: source-pinned words, no services/diagnostics run.
// The separate initializer explicitly sets the last-enqueued index to -1.
uint[] queueWords=[0x3C028007,0x8C4251B0,0x27BDFFE8,0x24460001,0x28C20004,0x14400007,0xAFBF0010,
    0x3C048001,0x248421C8,0x0C0189E7,0,0x0801A3ED,0,0x24050003,0x00061100,0x3C038007,
    0x24635C14,0x00431821,0x00061080,0x3C018007,0xAC2651B0,0x3C018007,0x00220821,0xAC245C48,
    0xAC600000,0x24A5FFFF,0x04A1FFFD,0x2463FFFC,0x8FBF0010,0x27BD0018,0x03E00008,0];
byte[] queueCode=new byte[queueWords.Length*4];for(int i=0;i<queueWords.Length;i++)BinaryPrimitives.WriteUInt32LittleEndian(queueCode.AsSpan(i*4),queueWords[i]);
Check(Convert.ToHexStringLower(SHA256.HashData(queueCode))=="7b25bd5394d6eb44fbe836882eb7bb866ec7c917813ebdd6e17b7037cc9e06ce","Exact original queue-body literal pin");
foreach(uint index in new uint[]{0xFFFFFFFF,0,1,2,3})
{
    var cpu=new RenderProbeCpu();cpu.Copy(0x80068F44,queueCode);cpu.Put(0x800751B0,index);
    cpu.Regs[4]=0x80066F34;cpu.Regs[29]=ControllerFixture.Stack;cpu.Regs[31]=ControllerFixture.Stop;
    cpu.Run(0x80068F44,index==3?0x8006279C:ControllerFixture.Stop);
    if(index==3)Check(cpu.Get(0x800751B0)==3&&!cpu.Writes.Contains(0x80075C58),"Full source helper reaches diagnostic call before enqueue; diagnostic code is not executed");
    else {uint next=unchecked(index+1);Check(cpu.Get(0x800751B0)==next&&cpu.Get(0x80075C48+next*4)==0x80066F34,
        "Native helper proves empty -1 and indices0..2 are valid pre-enqueue states");}
}
// Exact native helper can return through an accepted API without enqueueing
// when its signed index is full. The controller precheck is not atomic.
foreach(uint occupied in new uint[]{3,4,0x7FFFFFFF,0x80000000,0xFFFFFFFE})
foreach(var stage in new[]{NativeCollectibleSaveControllerStage.Idle,NativeCollectibleSaveControllerStage.ReadingOldTail,
    NativeCollectibleSaveControllerStage.WritingOrdinary,NativeCollectibleSaveControllerStage.ReadingOrdinary,NativeCollectibleSaveControllerStage.WritingTail})
{
    var f=new ControllerFixture(k);if(stage!=NativeCollectibleSaveControllerStage.Idle){f.Reach(stage);f.Complete();}
    int requests=f.Requests.Count;byte[] input=f.Bytes(k.OrdinaryInputAddress,0x600),proposal=f.Bytes(k.ProposalAddress,512),ledger=f.Game.Ledger(),card=f.Card.ToArray();
    f.Cpu.Put(0x800751B0,occupied);f.Invoke(stage==NativeCollectibleSaveControllerStage.Idle?k.BeginPc:k.PumpPc);
    Check(f.Stage==NativeCollectibleSaveControllerStage.Failed&&f.Requests.Count==requests&&
        f.Cpu.Calls.All(pc=>pc!=NativeCollectibleSaveControllerKernel.ReadPc&&pc!=NativeCollectibleSaveControllerKernel.WritePc),
        "Full/negative/overflow queue counter refuses every new read/write before a native request");
    Check(f.Cpu.Get(0x800751B0)==occupied&&f.Bytes(k.OrdinaryInputAddress,0x600).SequenceEqual(input)&&f.Bytes(k.ProposalAddress,512).SequenceEqual(proposal)&&
        f.Game.Ledger().SequenceEqual(ledger)&&f.Card.SequenceEqual(card)&&f.Cpu.Get(k.StateAddress+0x1C)==0,"Queue refusal preserves input/proposal/live/card/queue owner complements and never publishes success");
}
foreach(uint occupied in new uint[]{0xFFFFFFFF,0,1,2})
{var f=new ControllerFixture(k);f.Cpu.Put(0x800751B0,occupied);f.Reach(NativeCollectibleSaveControllerStage.Committed);
 Check(f.Stage==NativeCollectibleSaveControllerStage.Committed,"Only source empty -1 and bounded indices0..2 pass all request stages");}
{var f=new ControllerFixture(k);f.Invoke(k.BeginPc);f.Cpu.Put(0x800751B0,3);f.PendingPoll();f.Invoke(k.PumpPc);
 Check(f.Stage==NativeCollectibleSaveControllerStage.ReadingOldTail&&f.Requests.Count==1,"Full queue during owned pending request is not cancellation or premature owner release");}
foreach(int at in Enumerable.Range(0,18))
foreach(var stage in new[]{NativeCollectibleSaveControllerStage.Idle,NativeCollectibleSaveControllerStage.ReadingOldTail,
    NativeCollectibleSaveControllerStage.WritingOrdinary,NativeCollectibleSaveControllerStage.ReadingOrdinary,NativeCollectibleSaveControllerStage.WritingTail})
{
    var f=new ControllerFixture(k);if(stage!=NativeCollectibleSaveControllerStage.Idle){f.Reach(stage);f.Complete();}
    int requests=f.Requests.Count;f.Cpu.Put(k.FilenameAddress+(uint)at,f.Cpu.Get(k.FilenameAddress+(uint)at,1)^0x80,1);
    f.Invoke(stage==NativeCollectibleSaveControllerStage.Idle?k.BeginPc:k.PumpPc);
    Check(f.Stage==NativeCollectibleSaveControllerStage.Failed&&f.Requests.Count==requests&&f.Cpu.Get(k.StateAddress+0x1C)==0&&
        f.Cpu.Calls.All(pc=>pc!=NativeCollectibleSaveControllerKernel.ReadPc&&pc!=NativeCollectibleSaveControllerKernel.WritePc),
        "Every filename byte, including NUL, is source-fixed before all five requests");
}
int previousChecks=checks;PendingReceiptCases.Run(k,Check);
int receiptChecks=checks-previousChecks;checks+=mergeMemoryChecks;
Console.WriteLine($"NATIVE_COLLECTIBLE_SAVE_CONTROLLER_OK checks={checks} previousChecks={previousChecks} receiptChecks={receiptChecks} mergeMemoryChecks={mergeMemoryChecks} codeBytes={k.CodeByteLength} arenaBytes={NativeCollectibleSaveControllerKernel.ArenaBytes} stateBytes={NativeCollectibleSaveControllerKernel.StateBytes} stackBytes={NativeCollectibleSaveControllerKernel.MaximumStackBytes} emittedControllerExecuted=true nativeIoExecuted=false cardWrites=0 runtime=false");
Console.WriteLine("SAVE_CONTROLLER_LAYOUT "+JsonSerializer.Serialize(new{
    beginPc=k.BeginPc.ToString("X8"),pumpPc=k.PumpPc.ToString("X8"),revokePc=k.RevokePc.ToString("X8"),drainPc=k.DrainPc.ToString("X8"),
    codeSha256=Convert.ToHexStringLower(SHA256.HashData(k.CopyBytes())),codeBytes=k.CodeByteLength,
    codeCapacity=NativeCollectibleSaveControllerKernel.CodeCapacityBytes,frameBytes=NativeCollectibleSaveControllerKernel.FrameBytes,
    nestedStackBytes=NativeCollectibleSaveControllerKernel.MaximumStackBytes,excludedCallerBytes=NativeCollectibleSaveControllerKernel.NativeCallerBytes,
    regions=k.Regions.Select(r=>new{r.Role,address=r.Address.ToString("X8"),r.ByteLength})}));
InstructionCountCases.Run(k); // Separate measurement checks preserve established public smoke totals.
if(args.Length==0)return;
if(args.Length!=2||args[0]!="--source")throw new ArgumentException("Only --source original-USA-BIN; no publication/I/O mode.");
using var source=NativeRegionalSourceSession.Open(args[1],NativeDiscRevision.UsaScus94228);
var certified=NativeCollectibleSaveControllerKernel.Capture(source,k.ArenaAddress);certified.RequireSourceEvidence();
Check(certified.SourceCertified&&certified.CopyBytes().SequenceEqual(k.CopyBytes()),"Source certification pins existing native ABI, never different code or installation");
var sourceFixture=new ControllerFixture(certified);sourceFixture.Reach(NativeCollectibleSaveControllerStage.Committed);VerifySuccess(sourceFixture);source.VerifyUnchanged();
SerializerSnapshotCases.Run(source,certified,Check);
PendingReceiptCases.Source(source,Check);
int beforeAcceptedWrappers=checks;
AcceptedRequestWrapperCases.Run(source,Check);
NativeRequestTransactionCases.Run(source,certified);
Console.WriteLine($"NATIVE_COLLECTIBLE_SAVE_CONTROLLER_SOURCE_OK checks={checks} acceptedWrapperChecks={checks-beforeAcceptedWrappers} sourceUnchanged=true acceptedWrappersExecuted=true asyncCallbacksExecuted=false nativeIoExecuted=false cardWrites=0 runtime=false");

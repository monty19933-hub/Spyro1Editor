using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

int checks=0;
void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
void Refuse(Action action,string why)
{try{action();}catch(Exception e)when(e is InvalidDataException or ArgumentException){checks++;return;}throw new Exception("Expected refusal: "+why);}
var kernel=NativeCollectibleSaveTailKernel.CreateFixture(0x800C0000);
var selection=new NativeCollectibleSaveSelection(0xA00,1,0,0,0xABCD);
Check(!kernel.SourceCertified&&!kernel.CardWrites&&!kernel.RuntimeVerified&&!kernel.NativeResidenceIssued&&!kernel.PublicationApproved&&!kernel.NormalCreateBinApproved,
    "Detached preparation issues no native ownership or I/O.");
Refuse(kernel.RequireSourceEvidence,"Fixture cannot issue source seams");
foreach(uint at in new uint[]{0,0x80080000,0x800C0001,0x801FF600,0xFFFFFFF0})Refuse(()=>NativeCollectibleSaveTailKernel.CreateFixture(at),"Unsafe detached arena");
byte[] Tail(SaveCpuFixture fixture)
{byte[] result=Enumerable.Range(0,256).Select(i=>(byte)(i*71+19)).ToArray();fixture.Envelope.CopyTo(result,0);return result;}
byte[] Ordinary()
{byte[] b=new byte[0x600];new Random(701).NextBytes(b);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(0x58C),(uint)b.AsSpan(0,0x58C).ToArray().Sum(x=>x));return b;}
byte[] Run(SaveCpuFixture fixture,byte[] tail,bool accepted=true,Action<RenderProbeCpu>? mutate=null,
    NativeCollectibleSaveTailKernel? actual=null,Action<RenderProbeCpu>? during=null)
{
    var k=actual??kernel;var c=fixture.Cpu;
    c.Copy(k.ArenaAddress,Enumerable.Repeat((byte)0xA5,NativeCollectibleSaveTailKernel.ArenaBytes).ToArray());
    c.Copy(k.EntryPc,k.CopyBytes());c.Copy(k.Sha256.EntryPc,k.Sha256.CopyBytes());c.Copy(k.Loader.EntryPc,k.Loader.CopyBytes());c.Copy(k.Query.EntryPc,k.Query.CopyBytes());
    c.Copy(k.OldTailAddress,tail);for(int i=1;i<32;i++)c.Regs[i]=0xB1000000u+(uint)i;
    c.Regs[4]=k.OldTailAddress;c.Regs[5]=0x100;c.Regs[6]=SaveCpuFixture.Live;c.Regs[7]=0;c.Regs[29]=SaveCpuFixture.Stack;c.Regs[31]=SaveCpuFixture.Stop;
    c.Hi=0x12345678;c.Lo=0xABCDEF01;mutate?.Invoke(c);
    byte[] beforeLive=fixture.Ledger(),beforeInput=SaveCpuFixture.Bytes(c,k.OldTailAddress,256),beforeOutput=SaveCpuFixture.Bytes(c,k.OutputAddress,0x340);
    var registers=c.Regs.ToArray();c.Reads.Clear();c.Writes.Clear();c.Calls.Clear();c.Visited.Clear();
    if(during is null)c.Run(k.EntryPc,SaveCpuFixture.Stop);
    else {c.Run(k.EntryPc,k.Loader.EntryPc);during(c);beforeLive=fixture.Ledger();beforeInput=SaveCpuFixture.Bytes(c,k.OldTailAddress,256);c.Run(k.Loader.EntryPc,SaveCpuFixture.Stop);}
    Check((c.Regs[2]==0)==accepted,"Actual emitted preparation result");
    Check(fixture.Ledger().SequenceEqual(beforeLive)&&SaveCpuFixture.Bytes(c,k.OldTailAddress,256).SequenceEqual(beforeInput),"Full live and old transfer remain immutable");
    Check(new[]{4,5,6,7,16,17,18,19,20,21,22,23,26,27,28,29,30,31}.All(r=>c.Regs[r]==registers[r])&&c.Hi==0x12345678&&c.Lo==0xABCDEF01,
        "Complete callee/argument/HI-LO contract");
    bool In(uint a,uint first,int n)=>(ulong)a>=first&&(ulong)a<first+(ulong)n;
    Check(c.Writes.All(a=>In(a,k.StageAddress,0xA90)||In(a,registers[29]-NativeCollectibleSaveTailKernel.MaximumStackBytes,NativeCollectibleSaveTailKernel.MaximumStackBytes)),
        "Every emitted store stays in detached scratch/output or declared nested stack, never native caller frame");
    Check(c.Calls.All(a=>a==k.Query.EntryPc||a==k.Loader.EntryPc||a==k.Sha256.EntryPc),"No emitted native card services or external caller");
    byte[] output=SaveCpuFixture.Bytes(c,k.OutputAddress,0x340);
    if(accepted)
    {
        Check(c.Writes.First(a=>In(a,k.OutputAddress,0x340))==k.OutputAddress+0x320,"Invalidate a prior prepared marker before changing its payload");
        Check(c.Writes.TakeLast(4).SequenceEqual(Enumerable.Range(0,4).Select(i=>k.OutputAddress+0x320u+(uint)i)),"Prepared marker published last");
        var p=NativeCollectiblePreparedSaveTail.Inspect(k,tail,output,beforeLive);
        Check(p.CopyTail().AsSpan(0x83).SequenceEqual(tail.AsSpan(0x83))&&p.CopyTail()[0x61]==tail[0x61]&&p.CopyTail()[0x62]==tail[0x62],
            "Every opaque suffix byte and both other ordinary-slot bitmaps preserved");
        Check(p.CopyLedger().SequenceEqual(beforeLive)&&p.CopyTail()[0x60]==beforeLive[0x90],"Prepared snapshot is the actual admitted-and-picked ledger");
    }
    else Check(output.SequenceEqual(beforeOutput),"Refusal preserves previous complete candidate output");
    return output;
}
var fixture=new SaveCpuFixture();fixture.Pickup(1);byte[] tail=Tail(fixture),live=fixture.Ledger(),ordinary=Ordinary();
Check(live[0x90]==3&&live[0x7A]==2&&live[0x19]==1&&NativeCollectiblePreparedSaveTail.Word(live,0x7C)==1,"Actual admission then guarded pickup, not manually seeded Ready");
byte[] record=Run(fixture,tail);var prepared=NativeCollectiblePreparedSaveTail.Inspect(kernel,tail,record,live);
Check(!prepared.NativeExecutionProven&&!prepared.NativeIoApproved,"Host inspection is not an execution or I/O certificate");
for(int at=0;at<0x83;at++)
{byte[] bad=tail.ToArray();bad[at]^=0x80;Run(fixture,bad,false);}
for(int at=0x83;at<0x100;at++)
{byte[] varied=tail.ToArray();varied[at]^=0xA9;Run(fixture,varied);}
foreach(byte a in new byte[]{0,1,2,3})foreach(byte b in new byte[]{0,1,2,3})
{var f=new SaveCpuFixture(1,a,b);f.Pickup(1);Run(f,Tail(f));}
foreach(var(a,v,n)in new(uint,uint,int)[]{(SaveCpuFixture.Live+0x16,0,1),(SaveCpuFixture.Live+0x17,1,1),(SaveCpuFixture.Live+0x18,2,1),
    (SaveCpuFixture.Live+0x19,0,1),(SaveCpuFixture.Live+0x1A,0xABCE,2),(SaveCpuFixture.Live+0x50,0,4),(SaveCpuFixture.Live+0x7A,1,2),(SaveCpuFixture.Live+0x7C,0,4),
    (SaveCpuFixture.Live+0x80,2,4),(SaveCpuFixture.Live+0x84,1,4),(SaveCpuFixture.Live+0x88,1,4),(SaveCpuFixture.Live+0x8C,0,4),
    (SaveCpuFixture.Live+0xB0,1,4),(SaveCpuFixture.Live+0x1FC,1,4),(0x800755A4,0x800,4),(0x80078D14,0,4),(0x80078D18,16,4),
    (0x80078D1C,1,4),(0x80078D20,0x1ABCD,4),(0x80075B50,4,4),(0x80075B58,1,4)})
{Run(fixture,tail,false,c=>c.Put(a,v,n));fixture.Cpu.Copy(SaveCpuFixture.Live,live);fixture.Cpu.Put(0x800755A4,0xA00);fixture.Cpu.Put(0x80078D14,1);
 fixture.Cpu.Put(0x80078D18,0);fixture.Cpu.Put(0x80078D1C,0);fixture.Cpu.Put(0x80078D20,0xABCD);fixture.Cpu.Put(0x80075B50,0);fixture.Cpu.Put(0x80075B58,0);}
foreach(uint sp in new uint[]{0,1,kernel.OutputAddress+0x100,SaveCpuFixture.Live+0x80,0x801FF898,0x801FFFC0,0x80200000})
{Run(fixture,tail,false,c=>c.Regs[29]=sp);Check(fixture.Cpu.Writes.Count==0,"Bad whole nested/native-caller stack rejected without writes");}
foreach(uint sp in new uint[]{0x801FF8A0,0x801FFFB8})Run(fixture,tail,true,c=>c.Regs[29]=sp);
foreach(int reg in new[]{4,5,6,7})Run(fixture,tail,false,c=>c.Regs[reg]++);
foreach(var(a,v,n)in new(uint,uint,int)[]{(SaveCpuFixture.Live+0x7C,2,4),(SaveCpuFixture.Live+0x8C,2,4),
    (SaveCpuFixture.Live+0x16,0,1),(0x80078D20,0xABCE,4),(kernel.OldTailAddress+0xFF,0,1)})
{Run(fixture,tail,false,during:c=>c.Put(a,v,n));fixture.Cpu.Copy(SaveCpuFixture.Live,live);fixture.Cpu.Put(0x80078D20,0xABCD);}
{byte[] changed=tail.ToArray();changed[0x61]^=1;DetachedCollectibleBinary.WriteChecksum(changed.AsSpan(0,0x83));Run(fixture,changed,false);}
Run(fixture,tail,false,c=>{c.Put(SaveCpuFixture.Live+0x90,2,1);c.Put(SaveCpuFixture.Live+0x7A,1,2);});fixture.Cpu.Copy(SaveCpuFixture.Live,live);
foreach(int at in new[]{0x10,0x30,0x50,0x8C,0x90,0x200,0x261,0x282,0x283,0x2FF,0x300,0x320,0x324,0x33F})
{var bad=record.ToArray();bad[at]^=0x80;Refuse(()=>NativeCollectiblePreparedSaveTail.Inspect(kernel,tail,bad,live),"Prepared owner/digest/metadata tamper");}
Refuse(()=>prepared.RequireOwner(NativeCollectibleSaveTailKernel.CreateFixture(0x800C0000)),"Same bytes are not the same compiler owner");

NativeCollectibleSaveTransaction Begin(byte[]? ledger=null)=>NativeCollectibleSaveTransaction.Begin(kernel,ordinary,ledger??live,selection);
void Accept(NativeCollectibleSaveTransaction t,byte[]? ledger=null)
{_=t.Request(ledger??live,selection,0,0);t.Accepted(1,0,0);}
void OldRead(NativeCollectibleSaveTransaction t,byte[]? ledger=null)
{Accept(t,ledger);t.Poll(1,3,0,0,0,tail,ledger??live,selection);t.Prepared(prepared);}
void VerifyAll(NativeCollectibleSaveTransaction t,NativeCollectiblePreparedSaveTail p,byte[] old,byte[] ledger,byte[]? card=null,byte[]? finalObservation=null)
{
    Accept(t,ledger);t.Poll(0,0xFFFF,0xFFFF,3,0,[],ledger,selection);t.Poll(1,3,0,0,0,old,ledger,selection);t.Prepared(p);
    while(t.Stage!=NativeCollectibleSaveStage.Verified)
    {
        var r=t.Request(ledger,selection,0,0);Check((r.FileOffset,r.ByteLength) is (0x200,0x600) or (0x1400,0x100),"Only exact selected ordinary/tail owners requested");
        t.Accepted(1,0,0);t.Poll(0,0xFFFF,0xFFFF,r.Operation,0,[],ledger,selection);Check(!t.WouldReleaseNativeSuccess,"Pending never releases success");
        if(card is not null&&r.Operation==4)r.WriteBytes!.CopyTo(card,r.FileOffset);
        byte[] bytes=r.Operation==4?[]:card is not null?card.AsSpan(r.FileOffset,r.ByteLength).ToArray():r.FileOffset==0x200?ordinary:p.CopyTail();
        t.Poll(1,r.Operation,0,0,0,bytes,r.Operation==3&&r.FileOffset==0x1400?finalObservation??ledger:ledger,selection);
        Check(!t.WouldReleaseNativeSuccess,"Readback alone does not commit live dirty");
    }
}
byte[] cardImage=new byte[0x2000];new Random(91).NextBytes(cardImage);tail.CopyTo(cardImage,0x1400);byte[] cardBefore=cardImage.ToArray();
var transaction=Begin();VerifyAll(transaction,prepared,tail,live,cardImage);byte[] committed=transaction.Commit(live,selection);
Check(transaction.ReferenceModelOnly&&!transaction.NativeControllerExecuted&&!transaction.CardWrites&&!transaction.RuntimeVerified&&!transaction.NativeResidenceIssued&&
    !transaction.PublicationApproved&&!transaction.CrashAtomic&&transaction.WouldReleaseNativeSuccess,"Reference model alone reaches commit; no native-controller/runtime/I/O promotion");
Check(committed[0x19]==0&&NativeCollectiblePreparedSaveTail.Word(committed,0x7C)==1&&NativeCollectiblePreparedSaveTail.Word(committed,0x80)==1&&
    committed.AsSpan(0x50,32).SequenceEqual(prepared.CopyDigest()),"Both exact readbacks commit baseline digest and clear only matching generation");
byte[] expectedLive=live.ToArray();expectedLive[0x19]=0;prepared.CopyDigest().CopyTo(expectedLive,0x50);BinaryPrimitives.WriteUInt32LittleEndian(expectedLive.AsSpan(0x80),1);
Check(committed.SequenceEqual(expectedLive)&&fixture.Ledger().SequenceEqual(live),"Exact detached commit write set, original CPU live unchanged");
Check(Enumerable.Range(0,cardImage.Length).All(i=>i is >=0x200 and <0x800 or >=0x1400 and <0x1500||cardImage[i]==cardBefore[i]),"Simulated complete file protects header, other ordinary slots and remaining tail");
Check(cardImage.AsSpan(0x1483,125).SequenceEqual(cardBefore.AsSpan(0x1483,125)),"Simulated transfer protects all125 padding bytes");
Refuse(()=>transaction.Commit(live,selection),"One-shot commit");
foreach(var(ret,busy,latch)in new(int,uint,uint)[]{(0,0,0),(-1,0,0),(1,3,0),(1,0,1)})
{var t=Begin();_=t.Request(live,selection,0,0);Refuse(()=>t.Accepted(ret,busy,latch),"Rejected/busy/unconsumed request");Check(t.Stage==NativeCollectibleSaveStage.Failed&&!t.WouldReleaseNativeSuccess,"Rejected request fail closed");}
foreach(var(ret,op,result,busy,latch)in new(int,uint,uint,uint,uint)[]{(-1,3,0,0,0),(1,4,0,0,0),(1,3,5,0,0),(1,3,0,3,0),(1,3,0,0,1),(2,3,0,0,0),(0,3,0,4,0)})
{var t=Begin();Accept(t);Refuse(()=>t.Poll(ret,op,result,busy,latch,[],live,selection),"Idle/stale/wrong completion");Check(!t.WouldReleaseNativeSuccess,"Bad completion never releases success");}
{var t=Begin();Refuse(()=>t.Poll(1,3,0,0,0,tail,live,selection),"Completion without accepted operation");}
{var t=Begin();Accept(t);Refuse(()=>t.Poll(1,3,0,0,0,tail[..255],live,selection),"Partial old tail read");}
{var t=Begin();Refuse(()=>t.Prepared(prepared),"Prepared marker without owned fresh read");}
{var t=Begin();OldRead(t);Refuse(()=>t.Commit(live,selection),"Premature dirty clearing");}
{var t=Begin();Refuse(()=>t.Accepted(1,0,0),"Accepted return without an issued request");}
{var t=Begin();_=t.Request(live,selection,0,0);Refuse(()=>t.Request(live,selection,0,0),"Duplicate unaccepted request");}
foreach(var(busy,latch)in new(uint,uint)[]{(3,0),(4,0),(0,1)})
{var t=Begin();Refuse(()=>t.Request(live,selection,busy,latch),"Busy or unconsumed completion before request intent");}
{var t=Begin();OldRead(t);Accept(t);Refuse(()=>t.Poll(1,3,0,0,0,[],live,selection),"Read completion cannot satisfy accepted write");}
{var t=Begin();OldRead(t);Accept(t);t.Poll(1,4,0,0,0,[],live,selection);Accept(t);
 Refuse(()=>t.Poll(1,3,0,0,0,ordinary[..^1],live,selection),"Partial ordinary readback");}
{var t=Begin();OldRead(t);Accept(t);t.Poll(1,4,0,0,0,[],live,selection);Accept(t);t.Poll(1,3,0,0,0,ordinary,live,selection);
 Accept(t);Refuse(()=>t.Poll(1,4,5,0,0,[],live,selection),"Tail failure after ordinary success cannot release complete success");}
{var t=Begin();OldRead(t);Accept(t);t.Poll(1,4,0,0,0,[],live,selection);Accept(t);t.Poll(1,3,0,0,0,ordinary,live,selection);
 Accept(t);t.Poll(1,4,0,0,0,[],live,selection);Accept(t);var shortTail=prepared.CopyTail()[..255];
 Refuse(()=>t.Poll(1,3,0,0,0,shortTail,live,selection),"Partial tail readback");}
for(int at=0;at<0x600;at++)
{var t=Begin();OldRead(t);Accept(t);t.Poll(1,4,0,0,0,[],live,selection);Accept(t);byte[] bad=ordinary.ToArray();bad[at]^=1;
 Refuse(()=>t.Poll(1,3,0,0,0,bad,live,selection),"Every ordinary byte, including native unchecked suffix");}
{var t=Begin();OldRead(t);Accept(t);t.Poll(1,4,0,0,0,[],live,selection);Accept(t);byte[] bad=ordinary.ToArray();(bad[0],bad[1])=(bad[1],bad[0]);
 Check(bad.AsSpan(0,0x58C).ToArray().Sum(x=>x)==ordinary.AsSpan(0,0x58C).ToArray().Sum(x=>x)&&!bad.SequenceEqual(ordinary),"Equal native checksum counterexample");
 Refuse(()=>t.Poll(1,3,0,0,0,bad,live,selection),"Equal checksum is not exact readback");}
for(int at=0;at<256;at++)
{var t=Begin();OldRead(t);Accept(t);t.Poll(1,4,0,0,0,[],live,selection);Accept(t);t.Poll(1,3,0,0,0,ordinary,live,selection);
 Accept(t);t.Poll(1,4,0,0,0,[],live,selection);Accept(t);var bad=prepared.CopyTail();bad[at]^=1;
 Refuse(()=>t.Poll(1,3,0,0,0,bad,live,selection),"Every expected tail readback byte");}
foreach(int at in new[]{0x16,0x17,0x18,0x1A,0x50,0x7C,0x80,0x8C,0x90})
{var t=Begin();Accept(t);var bad=live.ToArray();bad[at]^=1;Refuse(()=>t.Poll(1,3,0,0,0,tail,bad,selection),"Changed session/ancestry before completion");}
foreach(var bad in new[]{selection with{Reserve=0x800},selection with{Bound=0},selection with{Controller=16},selection with{Slot=1},selection with{Marker=0xABCE}})
{var t=Begin();Refuse(()=>t.Request(live,bad,0,0),"Changed tuple refused before issuing any request intent");}
foreach(var bad in new[]{selection with{Reserve=0x800},selection with{Bound=0},selection with{Controller=16},selection with{Slot=1},selection with{Marker=0x1ABCD}})
{var t=Begin();VerifyAll(t,prepared,tail,live);Refuse(()=>t.Commit(live,bad),"Changed native selection at final commit");}
foreach(int at in new[]{0x16,0x50,0x7C,0x80,0x8C})
{var t=Begin();VerifyAll(t,prepared,tail,live);var changed=live.ToArray();changed[at]^=1;Refuse(()=>t.Commit(changed,selection),"Final commit rechecks reset, ancestry and generation");}
// Actual later pickup, not manually invented generation, stays dirty.
var later=new SaveCpuFixture(0);later.Pickup(0);byte[] initial=later.Ledger(),oldTail=Tail(later);var p2=NativeCollectiblePreparedSaveTail.Inspect(kernel,oldTail,Run(later,oldTail),initial);
var t2=Begin(initial);VerifyAll(t2,p2,oldTail,initial);later.Pickup(1);byte[] newer=later.Ledger(),kept=t2.Commit(newer,selection);
Check(kept[0x90]==3&&kept[0x19]==1&&NativeCollectiblePreparedSaveTail.Word(kept,0x7C)==2&&NativeCollectiblePreparedSaveTail.Word(kept,0x80)==1&&
    kept.AsSpan(0x50,32).SequenceEqual(p2.CopyDigest()),"New actual mutation retained while committed envelope baseline advances");
// Observe a real second pickup while pending, then reject a replay of the earlier valid ledger.
foreach(int returned in new[]{0,1})
{
    var t=Begin(initial);Accept(t,initial);t.Poll(0,0xFFFF,0xFFFF,3,0,[],newer,selection);
    Refuse(()=>t.Poll(returned,3,0,returned==0?3u:0,0,returned==0?[]:oldTail,initial,selection),
        "Previously observed generation2 cannot roll back to generation1 during pending or completed poll");
    Check(t.Stage==NativeCollectibleSaveStage.Failed&&!t.WouldReleaseNativeSuccess,"Observed rollback is terminal and cannot clear dirty");
}
foreach(var(generation,bits,earned)in new(uint,byte,ushort)[]{(1,3,2),(2,1,1),(3,1,1)})
{
    var t=Begin(initial);Accept(t,initial);t.Poll(0,0,0,3,0,[],newer,selection);
    byte[] bad=newer.ToArray();BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(0x7C),generation);
    bad[0x90]=bits;BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(0x7A),earned);
    NativeCollectiblePreparedSaveTail.ValidateLedger(bad,true);
    Refuse(()=>t.Poll(0,0,0,3,0,[],bad,selection),"High-water rejects generation rollback, same-generation bitmap change or loss of observed earned bits");
}
{
    var t=Begin(initial);Accept(t,initial);byte[] gen2WithoutPickup=initial.ToArray();
    BinaryPrimitives.WriteUInt32LittleEndian(gen2WithoutPickup.AsSpan(0x7C),2);
    t.Poll(0,0,0,3,0,[],gen2WithoutPickup,selection);
    Refuse(()=>t.Poll(0,0,0,3,0,[],newer,selection),"Even newly added bits require a generation change after the last observation");
}
{
    var t=Begin(initial);_=t.Request(newer,selection,0,0);t.Accepted(1,0,0);
    Refuse(()=>t.Poll(1,3,0,0,0,oldTail,initial,selection),"Request observations also advance the generation high-water");
}
{
    var t=Begin(initial);Accept(t,initial);t.Poll(1,3,0,0,0,oldTail,newer,selection);t.Prepared(p2);
    Refuse(()=>t.Request(initial,selection,0,0),"Request refuses a rollback previously seen by Poll");
}
{
    var t=Begin(initial);VerifyAll(t,p2,oldTail,initial,finalObservation:newer);
    Refuse(()=>t.Commit(initial,selection),"Final commit cannot clear dirty by replaying the initial ledger after a later observed pickup");
    Check(initial[0x19]==1&&newer[0x19]==1&&!t.WouldReleaseNativeSuccess,"Rollback refusal leaves both caller snapshots dirty and immutable");
}
// A real fresh admission can reproduce every byte, including its opaque epoch.
// Only the external caller's explicit revocation separates these model lifetimes.
var readmission=new SaveCpuFixture(0);readmission.Pickup(0);byte[] readmitted=readmission.Ledger();
Check(readmitted.SequenceEqual(initial),"Real re-admission plus same pickup can repeat the complete ledger and opaque epoch");
foreach(int checkpoint in Enumerable.Range(0,7))
{
    var t=Begin(initial);
    if(checkpoint>=5){VerifyAll(t,p2,oldTail,initial);if(checkpoint==6)_=t.Commit(initial,selection);}
    else
    {
        if(checkpoint>=1)_=t.Request(initial,selection,0,0);
        if(checkpoint>=2)t.Accepted(1,0,0);
        if(checkpoint>=3)t.Poll(1,3,0,0,0,oldTail,initial,selection);
        if(checkpoint>=4)t.Prepared(p2);
    }
    t.InvalidateSession();t.InvalidateSession();
    Check(t.Stage==NativeCollectibleSaveStage.Invalidated&&!t.WouldReleaseNativeSuccess&&t.RequiresExternalSessionInvalidation&&
        !t.NativeSessionIdentityAuthenticated&&!t.NativeCancellationApproved,
        "External invalidation is idempotent and terminal, not native identity or cancellation authority");
    foreach(Action action in new Action[]{()=>t.Request(readmitted,selection,0,0),()=>t.Accepted(1,0,0),
        ()=>t.Poll(1,3,0,0,0,oldTail,readmitted,selection),()=>t.Prepared(p2),()=>t.Commit(readmitted,selection)})
    {
        Refuse(action,"No old transaction operation resumes after identical-byte re-admission");
        Check(t.Stage==NativeCollectibleSaveStage.Invalidated&&!t.WouldReleaseNativeSuccess,"Every refused API retains terminal invalidation");
    }
    Check(readmission.Ledger().SequenceEqual(readmitted),"Reference invalidation never edits the newly admitted CPU ledger");
}
var freshModel=Begin(readmitted);
Check(freshModel.Stage==NativeCollectibleSaveStage.NeedOldTailRead&&!freshModel.NativeSessionIdentityAuthenticated,
    "A new externally scoped model is distinct, but still does not authenticate a native session");
Console.WriteLine($"NATIVE_COLLECTIBLE_SAVE_TAIL_OK checks={checks} codeBytes={kernel.CodeByteLength} arenaBytes={NativeCollectibleSaveTailKernel.ArenaBytes} nestedStackBytes={NativeCollectibleSaveTailKernel.MaximumStackBytes} nativeControllerExecuted=false cardWrites=0 imageWrites=0 runtime=false");
if(args.Length==0)return;
if(args.Length!=2||args[0]!="--source")throw new ArgumentException("Only --source original-USA-BIN; no publication/card mode.");
using var source=NativeRegionalSourceSession.Open(args[1],NativeDiscRevision.UsaScus94228);
try
{
    var certified=NativeCollectibleSaveTailKernel.Capture(source,0x800C0000);certified.RequireSourceEvidence();
    Check(certified.SourceCertified&&certified.CopyBytes().SequenceEqual(kernel.CopyBytes()),"Source pins add evidence, not different algorithms or installation");
    Refuse(()=>prepared.RequireOwner(certified),"Source certification cannot reissue a fixture's prepared owner");
    Run(fixture,tail,true,actual:certified);source.VerifyUnchanged();
    Console.WriteLine($"NATIVE_COLLECTIBLE_SAVE_TAIL_SOURCE_OK checks={checks} pins={NativeCollectibleSaveTailKernel.SourceSpans.Count} sourceUnchanged=true nativeControllerExecuted=false cardWrites=0 imageWrites=0 runtime=false");
}
finally{source.VerifyUnchanged();}

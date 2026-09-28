using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

internal static class PendingReceiptCases
{
    private const uint Busy=0x80075B50,Latch=0x80075B58;
    private static NativeCollectibleSavePendingReceipt Receipt(ControllerFixture f)=>
        (NativeCollectibleSavePendingReceipt)f.Cpu.Get(f.Kernel.StateAddress+0x20);
    private static uint Kind(ControllerFixture f)=>f.Cpu.Get(f.Kernel.StateAddress+0x24);
    private static uint Op(int kind)=>(kind&1)==0?4u:3u;
    // Complete frozen retail poll owner; mode1 only is executed, no BIOS/IRQ.
    private static readonly uint[] PollWords=[
        0x3C028007,0x24425B50,0x8C420000,0,0x14400006,0,0x3C028007,0x8C425B58,0,0x10400030,0x2402FFFF,
        0x3C088007,0x25085B50,0x8D070000,0x3C038007,0x8C635B54,0x1480000C,0,0x3C028007,0x8C425B58,0,
        0x14400013,0x25030008,0x8C620000,0,0x1040FFFD,0,0x08019DB3,0,0x3C028007,0x8C425B58,0,0x14400008,0,
        0x10C00002,0,0xACC30000,0x10A00014,0x00001021,0x08019DC4,0xACA70000,0x10C00006,0,0x3C028007,
        0x24425B98,0x8C420000,0,0xACC20000,0x10A00006,0,0x3C028007,0x24425B94,0x8C420000,0,0xACA20000,
        0x3C018007,0xAC205B58,0x24020001,0x03E00008,0];
    private static byte[] PollBytes()
    {byte[] bytes=new byte[PollWords.Length*4];for(int i=0;i<PollWords.Length;i++)BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*4),PollWords[i]);return bytes;}
    internal static void Run(NativeCollectibleSaveControllerKernel k,Action<bool,string> check)
    {
        check(NativeCollectibleSaveControllerKernel.StateBytes==0x30&&NativeCollectibleSaveControllerKernel.ArenaBytes==0x7300&&k.CodeByteLength<=0x2000,"Receipt/Drain keep full existing capacities");
        for(int kind=1;kind<=5;kind++)foreach(bool revoke in new[]{false,true})
        {
            var f=new ControllerFixture(k);if(kind>1){f.Reach((NativeCollectibleSaveControllerStage)(kind-1));f.Complete();}
            f.BeforeServicePc=Op(kind)==3?NativeCollectibleSaveControllerKernel.ReadPc:NativeCollectibleSaveControllerKernel.WritePc;
            f.BeforeServiceReturn=x=>{
                check(Receipt(x)==NativeCollectibleSavePendingReceipt.Intent&&Kind(x)==kind&&x.Cpu.Get(k.StateAddress+0x28)==uint.MaxValue,"Exact kind and Intent precede native request");
                if(revoke){x.Cpu.Put(k.StateAddress+0x14,1);x.Cpu.Put(k.StateAddress,8);}else x.Cpu.Put(SaveCpuFixture.Live+0x16,0,1);
            };
            f.Invoke(kind==1?k.BeginPc:k.PumpPc);
            check(f.Stage==(revoke?NativeCollectibleSaveControllerStage.Revoked:NativeCollectibleSaveControllerStage.Failed)&&Receipt(f)==NativeCollectibleSavePendingReceipt.Accepted&&Kind(f)==kind&&f.Cpu.Get(k.StateAddress+0x28)==1&&f.Pending is not null,"Accepted receipt survives failure/revocation before stage publication");
            byte[] receipt=f.Bytes(k.StateAddress+0x20,0x10);f.Invoke(k.RevokePc);
            check(f.Bytes(k.StateAddress+0x20,0x10).SequenceEqual(receipt),"Revoke preserves pending receipt exactly");
            f.PendingPoll();Drain(f,NativeCollectibleSaveDrainResult.Pending,check);
            f.Complete(result:revoke?5u:0);Drain(f,NativeCollectibleSaveDrainResult.Consumed,check);
            check(Receipt(f)==NativeCollectibleSavePendingReceipt.Consumed&&f.Cpu.Get(k.StateAddress+0x2C)==1,"Drain consumes success/error without Saved authority");
            Drain(f,NativeCollectibleSaveDrainResult.Consumed,check);check(f.Cpu.Calls.Count==0,"Repeated consumed Drain does not repoll/reissue");
        }
        for(int kind=1;kind<=5;kind++)foreach(bool revoke in new[]{false,true})
        {
            var f=new ControllerFixture(k);f.Reach((NativeCollectibleSaveControllerStage)kind);f.Complete();
            f.BeforeServiceReturn=x=>{if(revoke){x.Cpu.Put(k.StateAddress+0x14,1);x.Cpu.Put(k.StateAddress,8);}else x.Cpu.Put(SaveCpuFixture.Live+0x16,0,1);};
            f.Invoke(k.PumpPc);
            check(Receipt(f)==NativeCollectibleSavePendingReceipt.Consumed&&Kind(f)==kind&&f.Stage==(revoke?NativeCollectibleSaveControllerStage.Revoked:NativeCollectibleSaveControllerStage.Failed),"Consumed receipt precedes post-poll revocation/live validation");
            Drain(f,NativeCollectibleSaveDrainResult.Consumed,check);
        }
        foreach(int returned in new[]{0,-1,2})
        {
            var f=new ControllerFixture(k){RequestReturn=returned};f.Invoke(k.BeginPc);
            check(Receipt(f)==(returned==0?NativeCollectibleSavePendingReceipt.Rejected:NativeCollectibleSavePendingReceipt.Indeterminate)&&f.Cpu.Get(k.StateAddress+0x28)==unchecked((uint)returned),"Exact zero rejected; unknown raw request return pinned");
            Drain(f,returned==0?NativeCollectibleSaveDrainResult.NoOwnedTransfer:NativeCollectibleSaveDrainResult.Indeterminate,check);
        }
        foreach(uint busy in new uint[]{0,3})foreach(uint latch in new uint[]{0,1})foreach(int clean in new[]{0,1,2})
        {
            var f=new ControllerFixture(k);if(clean==1){f.RequestReturn=0;f.Invoke(k.BeginPc);}if(clean==2)f.Reach(NativeCollectibleSaveControllerStage.Committed);
            f.Cpu.Put(Busy,busy);f.Cpu.Put(Latch,latch);
            Drain(f,busy!=0||latch!=0?NativeCollectibleSaveDrainResult.Indeterminate:clean==2?NativeCollectibleSaveDrainResult.Consumed:NativeCollectibleSaveDrainResult.NoOwnedTransfer,check);
            check(f.Cpu.Calls.Count==0&&f.Cpu.Get(Busy)==busy&&f.Cpu.Get(Latch)==latch,"Clean receipt cannot consume foreign completion or grant release");
        }
        foreach(var(ret,op,busy,latch)in new(int,uint,uint,uint)[]{(-1,3,0,0),(2,3,0,0),(1,4,0,0),(1,3,3,0),(1,3,0,1),(0,4,3,0)})
        {
            var f=new ControllerFixture(k);f.Invoke(k.BeginPc);f.Invoke(k.RevokePc);f.ConfigurePoll(ret,op,0,busy,latch);
            Drain(f,NativeCollectibleSaveDrainResult.Indeterminate,check);
            check(Receipt(f)==NativeCollectibleSavePendingReceipt.Indeterminate,"Contradictory poll keeps accepted owner pinned");
            f.ConfigurePoll(1,3,0,0,0);Drain(f,NativeCollectibleSaveDrainResult.Indeterminate,check);check(f.Cpu.Calls.Count==0,"Later unrelated completion cannot heal indeterminate receipt");
        }
        foreach(var(offset,value)in new(uint,uint)[]{(0x20,1),(0x20,99),(0x24,0),(0x24,6),(0x28,0),(0x2C,1)})
        {
            var f=new ControllerFixture(k);f.Invoke(k.BeginPc);f.Invoke(k.RevokePc);f.Cpu.Put(k.StateAddress+offset,value);
            Drain(f,NativeCollectibleSaveDrainResult.Indeterminate,check);check(f.Cpu.Calls.Count==0,"Intent/invalid receipt is pinned without poll");
        }
        for(uint stage=1;stage<=5;stage++)
        {var f=new ControllerFixture(k);f.Reach((NativeCollectibleSaveControllerStage)stage);check(f.Invoke(k.DrainPc)==9&&f.Cpu.Writes.Count==0&&f.Cpu.Calls.Count==0,"Active Drain refuses before mutations");}
        foreach(int register in new[]{4,5,6,7,29})
        {var f=new ControllerFixture(k);f.Invoke(k.BeginPc);f.Invoke(k.RevokePc);check(f.Invoke(k.DrainPc,c=>c.Regs[register]++)==9&&f.Cpu.Writes.Count==0&&f.Cpu.Calls.Count==0,"Drain ABI/stack preflight has no mutations");}
        NativePoll(k,check);
    }
    private static void Drain(ControllerFixture f,NativeCollectibleSaveDrainResult expected,Action<bool,string> check)
    {
        var k=f.Kernel;byte[] arena=f.Bytes(k.ArenaAddress,NativeCollectibleSaveControllerKernel.ArenaBytes),live=f.Game.Ledger(),card=f.Card.ToArray();
        int requests=f.Requests.Count;var stage=f.Stage;uint marker=f.Cpu.Get(k.StateAddress+0x1C);uint result=f.Invoke(k.DrainPc);
        check(result==(uint)expected&&f.Stage==stage&&f.Cpu.Get(k.StateAddress+0x1C)==marker,"Drain result preserves terminal stage/proposal marker");
        check(f.Requests.Count==requests&&f.Cpu.Calls.Count(p=>p==NativeCollectibleSaveControllerKernel.PollPc)<=1&&f.Cpu.Calls.All(p=>p!=NativeCollectibleSaveControllerKernel.ReadPc&&p!=NativeCollectibleSaveControllerKernel.WritePc),"Drain at most one poll, zero requests");
        byte[] after=f.Bytes(k.ArenaAddress,arena.Length);
        check(Enumerable.Range(0,arena.Length).All(i=>k.ArenaAddress+i>=k.StateAddress&&k.ArenaAddress+i<k.StateAddress+0x30||arena[i]==after[i])&&f.Game.Ledger().SequenceEqual(live)&&f.Card.SequenceEqual(card),"Drain preserves code/filename/input/payload/live/card");
        check(f.Cpu.Reads.All(a=>a<SaveCpuFixture.Live||a>=SaveCpuFixture.Live+512)&&f.Cpu.Reads.All(a=>a is not(0x80078D14 or 0x80078D18 or 0x80078D1C or 0x80078D20)),"Drain bypasses invalidated live/session identity");
    }
    private static void NativePoll(NativeCollectibleSaveControllerKernel k,Action<bool,string> check)
    {
        byte[] code=PollBytes();check(Convert.ToHexStringLower(SHA256.HashData(code))=="19553c82e1ac2ff3a3089e3c8b101ade63a08c9620216785f79df4a2d2da3e21","Exact complete retail poll literal pin");
        for(int kind=1;kind<=5;kind++)foreach(uint result in new uint[]{0,5})
        {
            var f=new ControllerFixture(k);f.Reach((NativeCollectibleSaveControllerStage)kind);f.Invoke(k.RevokePc);f.Cpu.Copy(NativeCollectibleSaveControllerKernel.PollPc,code);f.Cpu.Put(0x80075B54,0);
            Drain(f,NativeCollectibleSaveDrainResult.Pending,check);check(f.Cpu.Get(k.StateAddress+4)==Op(kind)&&f.Cpu.Get(k.StateAddress+0x2C)==0,"Actual source pending output/return recorded");
            f.Cpu.Put(Busy,0);f.Cpu.Put(Latch,1);f.Cpu.Put(0x80075B94,Op(kind));f.Cpu.Put(0x80075B98,result);
            Drain(f,NativeCollectibleSaveDrainResult.Consumed,check);check(f.Cpu.Get(Latch)==0&&f.Cpu.Get(k.StateAddress+8)==result&&f.Cpu.Visited.Contains(0x80067708),"Actual source completion consumed including error");
        }
        foreach(bool foreign in new[]{false,true})
        {var f=new ControllerFixture(k);f.Invoke(k.BeginPc);f.Invoke(k.RevokePc);f.Cpu.Copy(NativeCollectibleSaveControllerKernel.PollPc,code);f.Cpu.Put(Busy,0);f.Cpu.Put(Latch,foreign?1u:0);f.Cpu.Put(0x80075B94,4);
         Drain(f,NativeCollectibleSaveDrainResult.Indeterminate,check);check(f.Cpu.Calls.Count==0&&f.Cpu.Get(Latch)==(foreign?1u:0),"Source idle/foreign completion remains unconsumed");
         f.Cpu.Put(Latch,1);f.Cpu.Put(0x80075B94,3);f.Cpu.Put(0x80075B98,0);
         Drain(f,NativeCollectibleSaveDrainResult.Indeterminate,check);
         check(Receipt(f)==NativeCollectibleSavePendingReceipt.Indeterminate&&f.Cpu.Calls.Count==0&&f.Cpu.Get(Latch)==1,
             "Pre-poll ownership contradiction permanently pins receipt; later matching foreign completion cannot heal it");}
    }
    internal static void Source(NativeRegionalSourceSession source,Action<bool,string> check)=>
        check(source.ReadExecutableRange((int)(NativeCollectibleSaveControllerKernel.PollPc-0x8000F800),0xF0).SequenceEqual(PollBytes()),"Executed full poll literal equals authenticated source owner");
}

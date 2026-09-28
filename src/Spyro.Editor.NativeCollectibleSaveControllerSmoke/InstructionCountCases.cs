using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

// Deterministic instruction-dispatch observations with emitted mock services.
// Includes mock instructions, excludes host-simulated transfer/setup work.
// This is neither cycles/latency nor an IRQ-masking budget or worst-case bound.
internal static class InstructionCountCases
{
    private sealed record Sample(string Case,uint Entry,uint StageBefore,uint StageAfter,uint Returned,
        uint ReceiptBefore,uint ReceiptAfter,string StateBeforeSha256,string StateAfterSha256,
        ulong Instructions,int ReadCalls,int WriteCalls,int PollCalls,int PrepareCalls,int ShaCalls);
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static void Run(NativeCollectibleSaveControllerKernel k)
    {
        int checks=0;
        void Check(bool value,string why){checks++;if(!value)throw new Exception("Instruction measurement: "+why);}
        CounterCases(Check);
        List<Sample> Capture()
        {
            var result=new List<Sample>();
            Sample Add(string name,ControllerFixture f,uint entry,uint stage,uint returned,uint receipt,
                int reads=0,int writes=0,int polls=0,Action<RenderProbeCpu>? before=null)
            {
                byte[] state=f.Bytes(k.StateAddress,0x30),live=f.Game.Ledger(),card=f.Card.ToArray();
                uint stageBefore=(uint)f.Stage,receiptBefore=f.Cpu.Get(k.StateAddress+0x20);
                ulong start=f.Cpu.ExecutedInstructionCount;uint actual=f.Invoke(entry,before);
                ulong instructions=f.Cpu.ExecutedInstructionCount-start;
                int Calls(uint pc)=>f.Cpu.Calls.Count(c=>c==pc);
                var sample=new Sample(name,entry,stageBefore,(uint)f.Stage,actual,receiptBefore,f.Cpu.Get(k.StateAddress+0x20),
                    Hash(state),Hash(f.Bytes(k.StateAddress,0x30)),instructions,Calls(NativeCollectibleSaveControllerKernel.ReadPc),
                    Calls(NativeCollectibleSaveControllerKernel.WritePc),Calls(NativeCollectibleSaveControllerKernel.PollPc),
                    Calls(k.Preparation.EntryPc),Calls(k.Preparation.Sha256.EntryPc));
                Check(instructions>0&&sample.StageAfter==stage&&actual==returned&&sample.ReceiptAfter==receipt,name+" exact fixture outcome");
                Check(sample.ReadCalls==reads&&sample.WriteCalls==writes&&sample.PollCalls==polls,name+" exact mocked service sequence");
                Check(f.Game.Ledger().SequenceEqual(live)&&f.Card.SequenceEqual(card),name+" instruction interval never performs host/card transfer or live commit");
                result.Add(sample);return sample;
            }
            Add("begin-idle",new(k),k.BeginPc,1,1,2,reads:1);
            var split=new ControllerFixture(k){BeforeServiceReturn=_=>{}};
            var splitSample=Add("begin-idle-split-run",split,k.BeginPc,1,1,2,reads:1);
            Check(splitSample.Instructions==result[0].Instructions&&splitSample.StateAfterSha256==result[0].StateAfterSha256,
                "Monotonic delta covers both Run segments without changing execution or count");
            var duplicate=new ControllerFixture(k);duplicate.Invoke(k.BeginPc);
            Add("begin-duplicate",duplicate,k.BeginPc,1,1,2);
            var revoked=new ControllerFixture(k);revoked.Invoke(k.BeginPc);revoked.Invoke(k.RevokePc);
            Add("begin-revoked",revoked,k.BeginPc,8,8,2);
            Add("pump-idle",new(k),k.PumpPc,0,0,0);
            for(uint stage=1;stage<=5;stage++)
            {
                var pending=new ControllerFixture(k);pending.Reach((NativeCollectibleSaveControllerStage)stage);pending.PendingPoll();
                Add("pump-pending-"+stage,pending,k.PumpPc,stage,stage,2,polls:1);
                var complete=new ControllerFixture(k);complete.Reach((NativeCollectibleSaveControllerStage)stage);complete.Complete();
                var sample=Add("pump-complete-"+stage,complete,k.PumpPc,stage+1,stage+1,stage==5?4u:2u,
                    reads:stage is 2 or 4?1:0,writes:stage is 1 or 3?1:0,polls:1);
                Check(sample.PrepareCalls==(stage==1?1:0)&&sample.ShaCalls==(stage==1?4:0),"Only completion1 enters measured preparation/four SHA calls");
                if(stage==5)Check(complete.Cpu.Get(k.ProposalAddress+0x19,1)==0&&complete.Cpu.Get(k.StateAddress+0x1C)==NativeCollectibleSaveControllerKernel.ProposalMagic,
                    "Unchanged generation publishes clean detached proposal, never native Saved");
            }
            var later=new ControllerFixture(k);later.Reach(NativeCollectibleSaveControllerStage.ReadingTail);later.Game.Pickup(1);later.Complete();
            Add("pump-complete-5-later-pickup",later,k.PumpPc,6,6,4,polls:1);
            Check(later.Cpu.Get(k.ProposalAddress+0x19,1)==1&&later.Cpu.Get(k.ProposalAddress+0x90,1)==3,"Later pickup remains in dirty detached proposal");
            foreach(int corrupt in new[]{-1,0,0x82})
            {
                var f=new ControllerFixture(k);f.Invoke(k.BeginPc);f.Complete(mutate:b=>b[corrupt<0?0xFF:corrupt]^=1);
                Add(corrupt<0?"pump-complete-1-valid-opaque-suffix":"pump-complete-1-corrupt-"+corrupt.ToString("X2"),f,k.PumpPc,
                    corrupt<0?2u:7u,corrupt<0?2u:7u,corrupt<0?2u:4u,writes:corrupt<0?1:0,polls:1);
            }
            var committed=new ControllerFixture(k);committed.Reach(NativeCollectibleSaveControllerStage.Committed);
            Add("pump-committed",committed,k.PumpPc,6,6,4);
            var rejected=new ControllerFixture(k){RequestReturn=0};rejected.Invoke(k.BeginPc);
            Add("pump-failed",rejected,k.PumpPc,7,7,3);
            Add("pump-revoked",revoked,k.PumpPc,8,8,2);
            Add("pump-preflight-refused",new(k),k.PumpPc,0,9,0,before:c=>c.Regs[4]++);
            Add("drain-idle",new(k),k.DrainPc,0,0,0);
            Add("drain-rejected",rejected,k.DrainPc,7,0,3);
            var drain=new ControllerFixture(k);drain.Invoke(k.BeginPc);drain.Invoke(k.RevokePc);drain.PendingPoll();
            Add("drain-pending",drain,k.DrainPc,8,1,2,polls:1);
            drain.Complete();Add("drain-success",drain,k.DrainPc,8,2,4,polls:1);
            Add("drain-already-consumed",drain,k.DrainPc,8,2,4);
            var error=new ControllerFixture(k);error.Invoke(k.BeginPc);error.Invoke(k.RevokePc);error.Complete(result:5);
            Add("drain-error-completion",error,k.DrainPc,8,2,4,polls:1);
            Check(error.Cpu.Get(k.StateAddress+8)==5&&error.Cpu.Get(k.StateAddress+0x1C)==0,"Error drained without success proposal");
            var foreign=new ControllerFixture(k);foreign.Invoke(k.BeginPc);foreign.Invoke(k.RevokePc);foreign.Cpu.Put(0x80075B50,4);
            Add("drain-foreign-before-poll",foreign,k.DrainPc,8,3,5);
            var wrong=new ControllerFixture(k);wrong.Invoke(k.BeginPc);wrong.Invoke(k.RevokePc);wrong.ConfigurePoll(1,4,0,0,0);
            Add("drain-wrong-completion-after-poll",wrong,k.DrainPc,8,3,5,polls:1);
            Add("drain-preflight-refused",new(k),k.DrainPc,0,9,0,before:c=>c.Regs[4]++);
            return result;
        }
        var first=Capture();var second=Capture();
        Check(first.SequenceEqual(second),"Every count, state identity and fixture outcome replays exactly");
        var identity=new ControllerFixture(k);
        Console.WriteLine("SAVE_CONTROLLER_INSTRUCTION_COUNTS_OK "+JsonSerializer.Serialize(new{
            checks,samples=first.Count,controllerCodeSha256=Hash(k.CopyBytes()),arena=k.ArenaAddress,
            initialLedgerSha256=Hash(identity.InitialLedger),ordinarySha256=Hash(identity.Ordinary),originalCardSha256=Hash(identity.OriginalCard),
            nativeServicesMocked=true,hostTransferInstructionsCounted=false,instructionCountsOnly=true,cyclesMeasured=false,
            latencyMeasured=false,irqWindowApproved=false,runtimeVerified=false,measurements=first}));
    }
    private static void CounterCases(Action<bool,string> check)
    {
        const uint pc=0x80010000;var cpu=new RenderProbeCpu();
        uint[] words=[0x24080003,0x2508FFFF,0x1500FFFE,0,0];
        for(int i=0;i<words.Length;i++)cpu.Put(pc+(uint)i*4,words[i]);
        check(cpu.ExecutedInstructionCount==0,"Fresh CPU counter starts at zero");cpu.Run(pc,pc);
        check(cpu.ExecutedInstructionCount==0,"Stop boundary does not count or fetch an instruction");
        cpu.Run(pc,pc+16);check(cpu.ExecutedInstructionCount==10&&cpu.Regs[8]==0&&cpu.Visited.Count==4,"Loop and branch delay count executions rather than unique PCs");
        cpu.Run(pc+16,pc+20);check(cpu.ExecutedInstructionCount==11,"Counter stays monotonic across Run calls");
        cpu.Put(pc,0x8C090000);cpu.Put(0,0x12345678);cpu.Put(pc+4,0);ulong before=cpu.ExecutedInstructionCount;
        cpu.Run(pc,pc+8);check(cpu.ExecutedInstructionCount-before==2&&cpu.Regs[9]==0x12345678,"Load and its delay each count once; semantics unchanged");
        cpu.Put(pc,0xFFFFFFFF);before=cpu.ExecutedInstructionCount;bool refused=false;
        try{cpu.Run(pc,pc+4);}catch(Exception e)when(e.Message.StartsWith("Unknown test opcode ")){refused=true;}
        check(refused&&cpu.ExecutedInstructionCount-before==1,"Decoded unsupported word is counted before unchanged refusal");
    }
}

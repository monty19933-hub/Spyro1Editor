using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

// Enter/Exit and asynchronous completion are modeled, not BIOS/IRQ execution.
// The emitted controller, preparation, receipts and ABI run in the bounded CPU.
internal static class IrqWindowCases
{
    internal static void Run(NativeCollectibleSaveControllerKernel? sourceKernel=null)
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception("IRQ windows: "+why);}
        var legacy=NativeCollectibleSaveControllerKernel.CreateFixture(0x800C0000);
        var k=sourceKernel??NativeCollectibleSaveControllerKernel.CreateIrqWindowedFixture(legacy.ArenaAddress);
        Check(k.IrqWindowsEmitted&&!legacy.IrqWindowsEmitted&&k.Regions.SequenceEqual(legacy.Regions),"Opt-in profile preserves every existing code/state/buffer owner");
        Check(k.CodeByteLength<=0x2000&&k.FrameByteLength==0x50&&k.MaximumStackByteLength==0xF0&&legacy.FrameByteLength==0x40&&legacy.MaximumStackByteLength==0xE0,"Measured hard capacity and private frame slots");
        Check(legacy.CodeByteLength==0x1FB4&&Convert.ToHexStringLower(SHA256.HashData(legacy.CopyBytes()))=="9a523808f739324211d576def64a81f9932440196abaa941da105b220a378a52","Legacy code length and bytes stay exact");
        Check(Convert.ToHexStringLower(SHA256.HashData(k.CopyBytes()))=="2c3c9e158efd6f0ecb03041eba32bafd4c7266ef7b4fee8ea20385c5dc367046","Reviewed compact successor golden");
        var observer=new RenderProbeCpu();List<(uint Pc,ulong Count)> observations=[];
        observer.Put(0x80010000,0x24080001);observer.Put(0x80010004,0);
        observer.BeforeInstruction=pc=>{observations.Add((pc,observer.ExecutedInstructionCount));observer.Put(pc,0x24080002);};
        observer.Run(0x80010000,0x80010000);Check(observations.Count==0,"Stop boundary never invokes optional observer");
        observer.Run(0x80010000,0x80010008);
        Check(observations.SequenceEqual(new[]{(0x80010000u,1UL),(0x80010004u,2UL)})&&observer.Regs[8]==1,"Observer runs after fetch/count; fetched words and delay behavior remain unchanged");
        List<ulong> windows=[];int nativeAccesses=0,drainedPreparationReads=0,receiptStores=0,bulkStores=0;
        ControllerFixture New(uint prior)
        {
            var f=new ControllerFixture(k);f.Cpu.Put(ControllerFixture.Mock+0x80,prior);ulong? start=null;
            f.Cpu.BeforeInstruction=pc=>
            {
                var cpu=f.Cpu;uint word=cpu.Get(pc),op=word>>26;
                if(pc==k.IrqEnterHelperPc){if(start is not null)throw new Exception("Nested measured IRQ window");start=cpu.ExecutedInstructionCount;}
                if(pc==k.IrqLeaveReturnPc&&start is{} began){windows.Add(cpu.ExecutedInstructionCount-began+1);start=null;}
                if(op is not(0x20 or 0x21 or 0x23 or 0x24 or 0x25 or 0x28 or 0x29 or 0x2B))return;
                uint address=unchecked(cpu.Regs[(word>>21)&31]+(uint)(short)word);bool store=op is 0x28 or 0x29 or 0x2B;
                bool masked=cpu.Get(ControllerFixture.Mock+0x80)==0;
                if(address is >=0x80075B50 and <=0x80075B98||address==0x800751B0)
                {
                    // The unchanged preparer rechecks these two zero values
                    // before/after SHA, after the old-tail transfer is fully
                    // consumed and before any next request. Under the explicit
                    // exclusive-service prerequisite there is no outstanding
                    // completion to race. No queue/controller/store exception.
                    bool drained=!store&&address is 0x80075B50 or 0x80075B58&&
                        pc>=k.Preparation.EntryPc&&pc<k.Preparation.EntryPc+k.Preparation.CodeByteLength&&
                        cpu.Get(k.StateAddress)==1&&cpu.Get(k.StateAddress+0x24)==1&&cpu.Get(k.StateAddress+0x20)==4&&
                        cpu.Get(k.StateAddress+0x28)==1&&cpu.Get(k.StateAddress+0x2C)==1&&
                        cpu.Get(k.StateAddress+4)==3&&cpu.Get(k.StateAddress+8)==0&&
                        cpu.Get(0x80075B50)==0&&cpu.Get(0x80075B58)==0&&f.Pending is null;
                    if(drained)drainedPreparationReads++;
                    else{nativeAccesses++;if(!masked)throw new Exception("Unmasked native IO global access at "+pc.ToString("X8"));}
                }
                if(store&&(address>=k.StateAddress+0x20&&address<k.StateAddress+0x30||
                    address==k.StateAddress&&cpu.Regs[(word>>16)&31] is >=1 and <=5))
                {receiptStores++;if(!masked)throw new Exception("Unmasked receipt/active-stage publication");}
                bool In(uint lo,uint n)=>(ulong)address>=lo&&(ulong)address<(ulong)lo+n;
                if(store&&(In(k.Preparation.OldTailAddress,0xB90)||In(k.ExpectedOrdinaryAddress,0x1100)))
                {bulkStores++;if(cpu.Get(ControllerFixture.Stack-(uint)k.FrameByteLength+0x44)!=0)throw new Exception("Bulk copy/preparation store inside owned IRQ window");}
            };
            return f;
        }
        void Trace(ControllerFixture f,uint prior)
        {
            bool held=false,exitExpected=false;int nativeEnter=0,nativeExit=0;
            foreach(uint pc in f.Cpu.Calls)
            {
                if(pc==k.IrqEnterHelperPc){Check(!held&&!exitExpected,"No nested controller-owned mask");held=true;}
                if(pc==NativeCollectibleSaveControllerKernel.EnterCriticalPc){Check(held,"Native Enter belongs to explicit window");nativeEnter++;}
                if(pc is NativeCollectibleSaveControllerKernel.ReadPc or NativeCollectibleSaveControllerKernel.WritePc or NativeCollectibleSaveControllerKernel.PollPc)
                    Check(held,"Every request/poll executes within its short owned window");
                if(pc==k.Preparation.EntryPc||pc==k.Preparation.Sha256.EntryPc||pc==k.Preparation.Query.EntryPc)
                    Check(!held,"Preparation/SHA/ledger validation is outside controller-owned masks");
                if(pc==NativeCollectibleSaveControllerKernel.ExitCriticalPc){Check(exitExpected&&prior==1,"Exit only for saved enabled token1");nativeExit++;exitExpected=false;}
                if(pc==k.IrqLeaveHelperPc){exitExpected=held&&prior==1;held=false;}
            }
            Check(!held&&!exitExpected&&f.Cpu.Get(ControllerFixture.Mock+0x80)==prior,"Every return restores caller IRQ state");
            Check(nativeExit==(prior==1?nativeEnter:0),"Each Enter token1 has one Exit; token0 has none");
        }
        foreach(uint prior in new uint[]{0,1})
        {
            var idle=New(prior);Check(idle.Invoke(k.PumpPc)==0,"Idle Pump returns without acquiring a window");Trace(idle,prior);
            Check(idle.Invoke(k.DrainPc)==0,"Idle no-owned Drain has a bounded clean snapshot");Trace(idle,prior);
            idle.Invoke(k.BeginPc,c=>c.Regs[4]++);Trace(idle,prior);Check(idle.Cpu.Calls.Count==0&&idle.Cpu.Writes.Count==0,"ABI refusal has no cleanup obligation or native access");
            var stale=New(prior);stale.Cpu.Put(k.StateAddress+4,1);stale.Invoke(k.BeginPc);Trace(stale,prior);
            Check(stale.Stage==NativeCollectibleSaveControllerStage.Failed,"Initial dirty controller state refuses before any window/request");
            var f=New(prior);
            f.Invoke(k.BeginPc);Trace(f,prior);
            for(int stage=1;stage<=5;stage++)
            {
                f.PendingPoll();f.Invoke(k.PumpPc);Trace(f,prior);
                Check((uint)f.Stage==(uint)stage,"Pending remains in its accepted stage");
                f.Complete();f.Invoke(k.PumpPc);Trace(f,prior);
            }
            Check(f.Stage==NativeCollectibleSaveControllerStage.Committed&&f.Requests.Count==5&&
                f.Game.Ledger().SequenceEqual(f.InitialLedger),"Full emitted transaction ends only in detached proposal");
            Check(f.Card.AsSpan(0x200,0x600).SequenceEqual(f.Ordinary)&&
                Enumerable.Range(0,0x2000).All(i=>i is >=0x200 and <0x800 or >=0x1400 and <0x1483||f.Card[i]==f.OriginalCard[i]),"Full ordinary image and all other slots/opaque padding preserved");
            f.Invoke(k.DrainPc);Trace(f,prior);
            f.Invoke(k.BeginPc);Trace(f,prior);Check(f.Requests.Count==5,"Repeated terminal Begin cannot rearm");
        }
        foreach(uint prior in new uint[]{0,1})foreach(uint stage in Enumerable.Range(0,5).Select(i=>(uint)i))
        foreach(int failure in Enumerable.Range(0,5))
        {
            var f=New(prior);
            if(stage!=0){f.Reach((NativeCollectibleSaveControllerStage)stage);f.Complete();}
            switch(failure)
            {
                case 0:f.Cpu.Put(0x800751B0,3);break;
                case 1:f.Cpu.Put(k.FilenameAddress,0,1);break;
                case 2:f.RequestReturn=0;break;
                case 3:f.RequestReturn=-1;break;
                case 4:f.BeforeServicePc=stage==0?NativeCollectibleSaveControllerKernel.ReadPc:NativeCollectibleSaveControllerKernel.PollPc;
                    f.BeforeServiceReturn=x=>x.Cpu.Put(k.StateAddress+0x14,1);break;
            }
            int before=f.Requests.Count;f.Invoke(stage==0?k.BeginPc:k.PumpPc);Trace(f,prior);
            Check(f.Stage is NativeCollectibleSaveControllerStage.Failed or NativeCollectibleSaveControllerStage.Revoked,"All request-stage early exits remain terminal");
            Check(f.Cpu.Get(k.StateAddress+0x1C)==0&&f.Game.Ledger().SequenceEqual(f.InitialLedger),"Failure cannot publish or clear live progress");
            if(failure<2)Check(f.Requests.Count==before,"Guard failure makes no native request");
        }
        foreach(uint prior in new uint[]{0,1})foreach(int mode in Enumerable.Range(0,6))
        {
            var f=New(prior);f.Invoke(k.BeginPc);f.Invoke(k.RevokePc);
            if(mode==0)f.PendingPoll();else if(mode==1)f.Complete();else if(mode==2)f.Complete(result:5);
            else if(mode==3)f.Cpu.Put(0x80075B50,4);else if(mode==4)f.ConfigurePoll(-1,3,0,0,0);
            else f.ConfigurePoll(1,4,0,0,0);
            f.Invoke(k.DrainPc);Trace(f,prior);Check(f.Stage==NativeCollectibleSaveControllerStage.Revoked,"Drain preserves terminal stage on every branch");
        }
        foreach(uint stack in new uint[]{0x801FF8E8,0x801FF8F1,0x801FFFC0,0x80200000})
        {
            var f=new ControllerFixture(k);f.Invoke(k.BeginPc,c=>c.Regs[29]=stack);
            Check(f.Cpu.Calls.Count==0&&f.Cpu.Writes.Count==0&&!f.Cpu.Reads.Any(a=>a is >=0x80075B50 and <=0x80075B98),"Hostile stack refuses before IRQ/native-global access");
        }
        foreach(uint stack in new uint[]{0x801FF8F0,0x801FFFB8})
        {var f=new ControllerFixture(k);f.Invoke(k.BeginPc,c=>c.Regs[29]=stack);Check(f.Stage==NativeCollectibleSaveControllerStage.ReadingOldTail,"Exact successor lower/upper stack endpoints accepted");}
        foreach(uint prior in new uint[]{0,1})
        {
            var later=New(prior);later.Reach(NativeCollectibleSaveControllerStage.ReadingTail);later.Game.Pickup(1);byte[] earned=later.Game.Ledger();
            later.Complete();later.Invoke(k.PumpPc);Trace(later,prior);
            Check(later.Cpu.Get(k.ProposalAddress+0x19,1)==1&&later.Cpu.Get(k.ProposalAddress+0x90,1)==3&&later.Game.Ledger().SequenceEqual(earned),"Real later pickup retains dirty progress in proposal without live commit");
            var different=New(prior);different.Reach(NativeCollectibleSaveControllerStage.ReadingOrdinary);
            different.Complete(mutate:b=>(b[0],b[1])=(b[1],b[0]));different.Invoke(k.PumpPc);Trace(different,prior);
            Check(different.Stage==NativeCollectibleSaveControllerStage.Failed&&different.Requests.Count==3,"Equal-checksum full600 difference refuses before tail write");
            var suffix=New(prior);suffix.Reach(NativeCollectibleSaveControllerStage.ReadingTail);suffix.Complete(mutate:b=>b[0xFF]^=1);suffix.Invoke(k.PumpPc);Trace(suffix,prior);
            Check(suffix.Stage==NativeCollectibleSaveControllerStage.Failed,"Full100 readback includes the untouched opaque suffix");
            foreach(var(ret,op,result,busy,latch)in new(int,uint,uint,uint,uint)[]{(-1,3,0,0,0),(1,4,0,0,0),(1,3,7,0,0),(0,3,0,3,1),(1,3,0,3,0)})
            {
                var f=New(prior);f.Invoke(k.BeginPc);f.ConfigurePoll(ret,op,result,busy,latch);f.Invoke(k.PumpPc);Trace(f,prior);
                Check(f.Stage==NativeCollectibleSaveControllerStage.Failed,"Bad poll/operation/result/native-state postconditions clean up every mask");
            }
        }
        foreach(var profile in new[]{legacy,k})
        {
            var f=new ControllerFixture(profile);f.Cpu.Put(ControllerFixture.Mock+0x80,1);f.Invoke(profile.BeginPc);f.PendingPoll();
            bool queued=false,delivered=false;int attempts=0,deferred=0;
            f.Cpu.BeforeInstruction=pc=>
            {
                if(queued&&!delivered)
                {
                    attempts++;
                    if(f.Cpu.Get(ControllerFixture.Mock+0x80)!=0)
                    {
                        // Deliberately separate transfer/retirement from CPU work.
                        f.Complete();f.Cpu.Put(0x80075B50,0);f.Cpu.Put(0x80075B58,1);
                        f.Cpu.Put(0x80075B94,3);f.Cpu.Put(0x80075B98,0);delivered=true;
                    }
                    else deferred++;
                }
                uint word=f.Cpu.Get(pc);
                if(!queued&&word>>26==0x23&&unchecked(f.Cpu.Regs[(word>>21)&31]+(uint)(short)word)==0x80075B50)queued=true;
            };
            f.Invoke(profile.PumpPc);f.Cpu.BeforeInstruction=null;
            Check(queued&&delivered&&attempts>0,"Deterministic ISR attempt occurs after the first Busy load");
            if(profile.IrqWindowsEmitted)
            {
                Check(deferred>0&&f.Stage==NativeCollectibleSaveControllerStage.ReadingOldTail&&f.Cpu.Get(profile.StateAddress+0x20)==2,"Pending completion defers through all receipt/postchecks without poisoning ownership");
                f.Invoke(profile.PumpPc);Check(f.Stage==NativeCollectibleSaveControllerStage.WritingOrdinary,"Deferred completion is consumed by next entry");
            }
            else Check(deferred==0&&f.Stage==NativeCollectibleSaveControllerStage.Failed,"Unmasked legacy control reproduces Busy/Latch race; it still requires external exclusion");
        }
        Check(windows.Count>0&&windows.Max()<2000&&nativeAccesses>0&&drainedPreparationReads>0&&receiptStores>0&&bulkStores>0,"Measured finite masks exclude bulk work; instruction counts are not cycles/latency bounds");
        Console.WriteLine($"SAVE_CONTROLLER_IRQ_WINDOWS_OK checks={checks} source={k.SourceCertified} codeBytes={k.CodeByteLength:X} frame={k.FrameByteLength:X} stack={k.MaximumStackByteLength:X} windows={windows.Count} maxWindowInstructions={windows.Max()} maskedNativeAccesses={nativeAccesses} drainedPreparationReads={drainedPreparationReads} receiptStores={receiptStores} bulkStores={bulkStores} codeSha256={Convert.ToHexStringLower(SHA256.HashData(k.CopyBytes()))} irqServicesMocked=true nativeIo=false runtime=false");
    }
}

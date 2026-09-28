using Spyro.Editor.Core.Analysis;

internal static class SchedulerCases
{
    private const uint Live=0x801FF600,State=NativeCollectibleSaveSchedulerKernel.StateAddress;
    internal static void Run(NativeCollectibleSaveSchedulerKernel kernel,NativeRegionalSourceSession? source,Action<bool,string> check)
    {
        SchedulerFixture New(bool dirty=true)=>new(kernel,dirty,source);
        bool NoTail(SchedulerFixture f)=>!f.Cpu.Reads.Any(a=>a>=f.Controller.ArenaAddress&&a<f.Controller.ArenaAddress+0x7300);
        void NoServices(SchedulerFixture f,string why)=>check(!f.Cpu.Calls.Any(a=>a is 0x800662BC or 0x800665B8 or 0x80066E28 or 0x800670E4 or 0x80067628 or 0x80059864),why);
        var done=New();done.ReachHeld();
        check(done.Requests[0].Op==2&&done.Requests.Skip(1).Select(r=>(r.Op,r.Offset,r.Count)).SequenceEqual(new(uint,uint,uint)[]{
            (3,0,0x80),(3,0x1400,0x100),(4,0x200,0x600),(3,0x200,0x600),(4,0x1400,0x100),(3,0x1400,0x100)}),"Fresh op2→owned header3→five exact controller requests");
        check(done.Card.AsSpan(0x200,0x600).SequenceEqual(done.Ordinary)&&done.Bytes(kernel.Controller.ExpectedOrdinaryAddress,0x600).SequenceEqual(done.Ordinary),"Complete serializer600 captured and verified");
        check(done.Card.AsSpan(0x1400+0x83,0x7D).SequenceEqual(done.OriginalCard.AsSpan(0x1483,0x7D)),"Every unknown transfer padding byte preserved");
        check(done.Card.AsSpan(0,0x200).SequenceEqual(done.OriginalCard.AsSpan(0,0x200))&&done.Card.AsSpan(0x800,0xC00).SequenceEqual(done.OriginalCard.AsSpan(0x800,0xC00))&&
            done.Card.AsSpan(0x1500).SequenceEqual(done.OriginalCard.AsSpan(0x1500)),"Native header, other ordinary slots and file complement exact");
        check(done.Card[0x1460]==3&&done.Card[0x1461]==2&&done.Card[0x1462]==1,"Prepared bits3 retain other custom slots2/1");
        check(done.Game.Ledger().SequenceEqual(done.InitialLedger)&&done.Cpu.Get(0x80078D0C)==2&&done.Cpu.Get(0x80078D08)==0,"No live commit, native success7, retail state advance or rearm");
        int requests=done.Requests.Count;done.Frame();check(done.Flow==4&&done.Stage==6&&done.Cpu.Get(State+0x34)==2&&done.Requests.Count==requests,"Terminal HELD invokes bounded Drain without requests or rearm");
        var clean=New(false);clean.Frame(false);check(clean.Flow==0&&clean.Stage==0&&clean.Requests.Count==0&&NoTail(clean),"Seeded Ready dirty0/gen0 ordinary save passes through without custom tail access");
        NoServices(clean,"Clean save calls no custom-owned native service");
        foreach(uint state in new uint[]{1,2,3,uint.MaxValue})
        {var f=New();f.Cpu.Put(0x80078D08,state);f.Frame(false);check(f.Flow==0&&f.Requests.Count==0,"Never adopt unobserved retail state1/2 or arbitrary state");NoServices(f,"Unarmed retail state retains original driver");}
        foreach(var(at,value)in kernel.ExpectedGlobals.Concat(new(uint,uint)[]{(0x800757D8,11),(0x80078D0C,2)}))
        {
            var f=New();f.Cpu.Put(at,value^1);f.Frame(false);check(f.Flow==0&&f.Cpu.Writes.Count==0&&NoTail(f),"Unarmed nonapplicable source global returns native: "+at.ToString("X8"));
            var owned=New();owned.Frame();owned.Cpu.Put(at,value^1);owned.Frame();check(owned.Flow==1&&owned.Receipt==2&&owned.Cpu.Writes.Count==0&&NoTail(owned),"Owned source mismatch holds retained precheck without tail touches: "+at.ToString("X8"));
        }
        foreach(uint stack in new uint[]{0x801FF910,0x801FFF78})
        {var f=New();f.Frame(stack:stack);check(f.Flow==1&&f.Requests.Count==1,"Inclusive complete-frame stack boundary accepts "+stack.ToString("X8"));}
        foreach(uint stack in new uint[]{0x801FF908,0x801FFF80,0x801FFF74,0x80200000,0x801FF600,0x8016F000})
        {var f=New();f.Frame(false,stack:stack);check(f.Flow==0&&f.Cpu.Writes.Count==0&&NoTail(f),"Hostile stack rejected before custom frame/tail access");var owned=New();owned.Frame();owned.Frame(stack:stack);check(owned.Cpu.Writes.Count==0&&NoTail(owned),"Owned hostile stack retains owners, cannot run retail");}
        {var f=New();f.Frame(false,ra:0x80010000);check(f.Cpu.Reads.Count==0&&f.Cpu.Writes.Count==0,"Wrong native RA cannot claim save frame or read tail");}
        foreach(var(at,value,width)in new(uint,uint,int)[]{(Live+0x16,0,1),(Live+0x17,1,1),(Live+0x18,2,1),(Live+0x19,0,1),
            (Live+0x8C,0,4),(Live+0x7C,0,4),(0x80078D18,16,4),(0x80078D20,0x1000ABCD,4)})
        {
            var f=New();f.Cpu.Put(at,value,width);f.Frame(false);check(f.Flow==0&&f.Requests.Count==0,"Fresh invalid ledger/tuple never arms");
            var owned=New();owned.Frame();byte[] buffer=owned.Bytes(owned.Controller.OrdinaryInputAddress,0x600);owned.Cpu.Put(at,value,width);owned.Frame();
            check(owned.Flow==5&&owned.Receipt==2&&owned.Requests.Count==1&&owned.Bytes(owned.Controller.OrdinaryInputAddress,0x600).SequenceEqual(buffer),"Owned mismatch is sticky HELD, retains header buffer; not cancellation/drain");
            owned.ConfigureGlobals();owned.Frame();NoServices(owned,"Blocked scheduler does not reissue or manufacture empty state");
        }
        foreach(bool header in new[]{false,true})foreach(int returned in new[]{0,-1,2})
        {var f=New();if(header){f.Frame();f.Complete();}f.RequestReturn=returned;f.Frame();check(f.Flow==5&&f.Receipt==(returned==0?3u:5u)&&f.Cpu.Get(State+8)==unchecked((uint)returned),"Precheck/header raw acceptance receipt precedes rejection");int n=f.Requests.Count;f.Frame();check(f.Requests.Count==n,"No repeated fresh arm after rejected/indeterminate request");}
        foreach(uint index in new uint[]{0,1,2,3,0xFFFFFFFE,0x7FFFFFFF})
        {var f=New();f.Cpu.Put(0x800751B0,index);f.Frame(false);check(f.Requests.Count==0&&f.Flow==0,"Fresh arm cannot reset a nonempty/invalid queue");}
        foreach(uint index in new uint[]{uint.MaxValue,0,1,2,3,0xFFFFFFFE,0x7FFFFFFF})
        {var f=New();f.Frame();f.Complete();f.Cpu.Put(0x800751B0,index);f.Frame();bool accepted=index==uint.MaxValue||index<=2;check(accepted?f.Flow==2&&f.Requests.Count==2:f.Flow==5&&f.Requests.Count==1,"Every header request uses unsigned bounded queue guard");}
        foreach(uint index in new uint[]{uint.MaxValue,0,1,2,3,0xFFFFFFFE,0x7FFFFFFF})
        {var f=New();f.InterleavePc=0x800662BC;f.AfterService=x=>x.Cpu.Put(0x800751B0,index);f.Frame();bool accepted=index==uint.MaxValue||index<=2;
            check(accepted?f.Flow==1&&f.Requests.Count==1:f.Flow==5&&f.Requests.Count==0,"Precheck request rechecks queue after separate SDK initialization");}
        {var f=New();f.Cpu.Put(Live+0x18,1,1);f.Cpu.Put(0x80078D18,16);f.ReachHeld();check(f.Stage==6&&f.Cpu.Get(State+0x18)==16,"Both exact source native controller encodings execute through full transaction");}
        foreach(uint result in new uint[]{0,3,1,2,uint.MaxValue})
        {var f=New();f.Frame();f.Complete(result);f.Frame();check(result is 0 or 3?f.Flow==2:f.Flow==5&&f.Receipt==4,"Precheck accepts only source result0-or3 after consumed operation2");}
        foreach(bool header in new[]{false,true})
        {
            var f=New();if(header)f.ReachHeader();else f.Frame();f.Frame();check(f.Flow==(header?2u:1u)&&f.Receipt==2,"Pending expected busy/latch remains armed");
            f.Complete(operation:4);f.Cpu.Put(0x80075B60,header?3u:2u);f.Frame();check(f.Flow==5&&f.Receipt==2&&f.Cpu.Get(0x80075B58)==1&&!f.Cpu.Calls.Contains(0x80067628),"Completed operation uses75B94 (+44), not75B60, and foreign latch is unconsumed");
            f.Cpu.Put(0x80075B94,header?3u:2u);f.Frame();check(f.Cpu.Get(0x80075B58)==1,"Later matching operation cannot heal blocked receipt");
            var idle=New();if(header)idle.ReachHeader();else idle.Frame();idle.Cpu.Put(0x80075B50,0);idle.Cpu.Put(0x80075B58,0);idle.Frame();check(idle.Flow==5&&idle.Receipt==2,"Lost completion is held, never inferred as success");
            foreach(int returned in new[]{0,-1,2}){var bad=New();if(header)bad.ReachHeader();else bad.Frame();bad.Complete(returned:returned);bad.Frame();check(bad.Flow==5&&bad.Receipt==2&&bad.Cpu.Get(State+0xC)==unchecked((uint)returned),"Raw non-completion poll cannot produce Consumed/next request");}
        }
        foreach(uint result in new uint[]{1,3,uint.MaxValue})
        {var f=New();f.ReachHeader();f.Complete(result);f.Frame();check(f.Flow==5&&f.Receipt==4&&!f.Cpu.Calls.Contains(0x80059864),"Header result must be exactly0 after consumed op3");}
        {var f=New();f.ReachHeader();f.Complete(corrupt:b=>b[0x7E]^=1);f.Frame();check(f.Flow==5&&f.Receipt==4&&!f.Cpu.Calls.Contains(0x80059864),"Header marker exact and no serializer on mismatch");}
        foreach(uint service in new uint[]{0x800665B8,0x80066E28,0x80059864})
        {var f=New();if(service!=0x800665B8){f.Frame();f.Complete();if(service==0x80059864){f.Frame();f.Complete();}}f.InterleavePc=service;f.AfterService=x=>x.Cpu.Put(Live+0x7C,2);f.Frame();
            check(f.Flow==5&&f.Stage==0,"Mutation across precheck/header/serializer return refuses before controller Begin");}
        {var f=New();f.ReachTransaction();f.Cpu.Regs[4]=f.Controller.StateAddress;f.Cpu.Regs[5]=f.Cpu.Regs[6]=f.Cpu.Regs[7]=0;f.Cpu.Regs[31]=SchedulerFixture.Return;f.Cpu.Run(f.Controller.RevokePc,SchedulerFixture.Return);
            f.Frame();check(f.Flow==4&&f.Stage==8,"External controller revoke enters terminal HELD");f.Frame();check(f.Cpu.Get(State+0x34)==1&&f.Stage==8,"Held revoked transfer drains pending without rearm");
            f.Complete();f.Frame();check(f.Cpu.Get(State+0x34)==2&&f.Stage==8&&f.Requests.Count==3,"Matching drain consumes only outstanding controller transfer");}
        {var f=new SchedulerFixture(kernel,true,source,0,0);f.ReachTransaction();f.Complete();f.Frame();f.PickupNext();for(int i=0;i<4;i++){f.Complete();f.Frame();}
            check(f.Flow==4&&f.Stage==6&&f.Cpu.Get(Live+0x90,1)==3&&f.Cpu.Get(Live+0x7C)==2&&f.Cpu.Get(Live+0x19,1)==1,"New actual pickup while transaction proceeds remains live and dirty");}
        {var f=New();f.ReachTransaction();f.Complete();f.Frame();f.Complete();f.Frame();f.Complete(corrupt:b=>{
            int up=Array.FindIndex(b,0,0x58C,x=>x<255),down=Array.FindIndex(b,up+1,0x58C-up-1,x=>x>0);b[up]++;b[down]--;});f.Frame();
            check(f.Flow==4&&f.Stage==7&&!f.Requests.Any(r=>r.Op==4&&r.Offset==0x1400),"Equal-checksum but different full600 readback fails before any custom-tail write");}
    }
}

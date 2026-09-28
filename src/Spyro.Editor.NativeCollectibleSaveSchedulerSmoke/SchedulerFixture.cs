using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;

// Emitted scheduler/controller/preparer plus emitted mock native service seams.
// Card transfer is a host byte-array simulation, not BIOS/card/runtime evidence.
internal sealed class SchedulerFixture
{
    internal const uint Stack=0x801FFF70,Return=0x80031E08,Held=0x800324B8,Mock=0x801D0000;
    internal SaveCpuFixture Game{get;}
    internal RenderProbeCpu Cpu=>Game.Cpu;
    internal NativeCollectibleSaveSchedulerKernel Kernel{get;}
    internal NativeCollectibleSaveControllerKernel Controller=>Kernel.Controller;
    internal byte[] Card{get;}=new byte[0x2000];
    internal byte[] OriginalCard{get;}
    internal byte[] Ordinary{get;}=new byte[0x600];
    internal byte[] InitialLedger{get;}
    internal List<(uint Op,uint Buffer,uint Offset,uint Count)> Requests{get;}=[];
    internal (uint Op,uint Buffer,uint Offset,uint Count)? Pending{get;private set;}
    internal int RequestReturn{get;set;}=1;
    internal uint? InterleavePc{get;set;}
    internal Action<SchedulerFixture>? AfterService{get;set;}
    internal uint Flow=>Cpu.Get(Kernel.StateBase);
    internal uint Receipt=>Cpu.Get(Kernel.StateBase+4);
    internal uint Stage=>Cpu.Get(Controller.StateAddress);
    internal SchedulerFixture(NativeCollectibleSaveSchedulerKernel? kernel=null,bool dirty=true,NativeRegionalSourceSession? source=null,byte firstBits=1,int pickup=1)
    {
        Game=new(firstBits);if(dirty)Game.Pickup(pickup);InitialLedger=Game.Ledger();Kernel=kernel??NativeCollectibleSaveSchedulerKernel.CreateFixture();
        Cpu.Copy(Controller.ArenaAddress,new byte[NativeCollectibleSaveControllerKernel.ArenaBytes]);
        Cpu.Copy(Controller.BeginPc,Controller.CopyBytes());var p=Controller.Preparation;
        Cpu.Copy(p.EntryPc,p.CopyBytes());Cpu.Copy(p.Sha256.EntryPc,p.Sha256.CopyBytes());Cpu.Copy(p.Loader.EntryPc,p.Loader.CopyBytes());Cpu.Copy(p.Query.EntryPc,p.Query.CopyBytes());
        Cpu.Copy(Controller.FilenameAddress,Controller.CopyFilename());
        Cpu.Copy(NativeCollectibleSaveSchedulerKernel.EntryPc,Kernel.CopyMainBytes());if(Kernel.IsResident)Cpu.Copy(Kernel.ResidentAddress,Kernel.CopyResidentBytes());else Cpu.Copy(NativeCollectibleSaveSchedulerKernel.FinalPc,Kernel.CopyFinalBytes());
        Cpu.Copy(0x800873D8,Words(0x08000000|((NativeCollectibleSaveSchedulerKernel.EntryPc>>2)&0x3FFFFFF),0));
        ConfigureGlobals();Cpu.Put(0x80078D08,0);Cpu.Put(0x800751B0,uint.MaxValue);Cpu.Put(0x80075B50,0);Cpu.Put(0x80075B58,0);
        new Random(276).NextBytes(Card);Game.Envelope.CopyTo(Card,0x1400);Card[0x7E]=0xCD;Card[0x7F]=0xAB;OriginalCard=Card.ToArray();
        new Random(712).NextBytes(Ordinary);Cpu.Copy(Mock+0x100,Ordinary);
        Cpu.Copy(0x800662BC,Words(0x3C088007,0xAD005B50,0xAD005B58,0x2409FFFF,0xAD0951B0,0x03E00008,0));
        Cpu.Copy(0x800665B8,RequestStub(2));Cpu.Copy(0x80066E28,RequestStub(3));Cpu.Copy(0x800670E4,RequestStub(4));
        Cpu.Copy(0x80067628,PollStub());Cpu.Copy(0x80059864,SerializerStub());
        if(source is not null)
        {
            foreach(var(pc,length)in new(uint,int)[]{(0x80059864,0x1E4),(0x80016914,0x98),(0x8005956C,0x28)})
                Cpu.Copy(pc,source.ReadExecutableRange((int)(pc-0x8000F800),length));
            ConfigureOrdinarySource();
        }
    }
    internal byte[] Bytes(uint at,int n)=>SaveCpuFixture.Bytes(Cpu,at,n);
    internal void ConfigureGlobals()
    {foreach(var(at,value)in Kernel.ExpectedGlobals)Cpu.Put(at,value);Cpu.Put(0x800757D8,11);Cpu.Put(0x80078D0C,2);}
    internal void Frame(bool held=true,Action<RenderProbeCpu>? setup=null,uint stack=Stack,uint ra=Return)
    {
        for(int r=1;r<32;r++)Cpu.Regs[r]=0xB7000000u+(uint)r;
        Cpu.Regs[29]=stack;Cpu.Regs[31]=ra;Cpu.Hi=0x12345678;Cpu.Lo=0x87654321;
        Cpu.Put(Mock,unchecked((uint)RequestReturn));Cpu.Put(Mock+4,0);setup?.Invoke(Cpu);
        var saved=Cpu.Regs.ToArray();byte[] live=Game.Ledger();Cpu.Reads.Clear();Cpu.Writes.Clear();Cpu.Calls.Clear();Cpu.Visited.Clear();
        if(InterleavePc is{} pc&&AfterService is{} action)
        {Cpu.Run(0x800873D8,pc);uint continuation=Cpu.Regs[31];Cpu.Run(pc,continuation);action(this);live=Game.Ledger();InterleavePc=null;AfterService=null;Cpu.Run(continuation,held?Held:ra);}
        else Cpu.Run(0x800873D8,held?Held:ra);
        if(new[]{16,17,18,19,20,21,22,23,26,27,28,29,30,31}.Any(r=>Cpu.Regs[r]!=saved[r])||Cpu.Hi!=0x12345678||Cpu.Lo!=0x87654321)
            throw new Exception("Scheduler disturbed native callback callee state/frame/HI/LO.");
        bool In(uint a,uint lo,uint n)=>(ulong)a>=lo&&(ulong)a<(ulong)lo+n;
        bool Owned(uint a)=>In(a,Kernel.StateBase,0x40)||In(a,unchecked(stack-0x110),0x110)||
            In(a,Mock,0x800)||In(a,0x80075B50,0x50)||In(a,0x800751B0,4)||
            In(a,Controller.Preparation.OldTailAddress,0xBA0)||In(a,Controller.OrdinaryInputAddress,0x1740);
        if(Cpu.Writes.Any(a=>!Owned(a)))throw new Exception("Scheduler store escaped full write complement: "+Cpu.Writes.First(a=>!Owned(a)).ToString("X8"));
        if(!Game.Ledger().SequenceEqual(live))throw new Exception("Detached scheduler modified live progress.");
        if(Cpu.Get(Mock+4)!=0)
        {
            var request=(Cpu.Get(Mock+8),Cpu.Get(Mock+16),Cpu.Get(Mock+20),Cpu.Get(Mock+24));
            if(Cpu.Get(Mock+28)!=Cpu.Get(Kernel.StateBase+0x18))throw new Exception("Captured controller differs from armed native tuple.");
            if(request.Item1!=2)
            {
                uint file=Cpu.Get(Mock+12);
                if(file!=(request.Item3==0?Kernel.FilenameAddress:Controller.FilenameAddress)||
                    !Bytes(file,18).SequenceEqual("BASCUS-94228SPYRO\0"u8.ToArray()))throw new Exception("Wrong exact file owner.");
            }
            Requests.Add(request);if(RequestReturn==1)Pending=request;
        }
    }
    internal void Complete(uint result=0,uint? operation=null,int returned=1,Action<byte[]>? corrupt=null)
    {
        var p=Pending??throw new Exception("No accepted mocked operation.");
        if(result==0&&p.Op!=2)
        {
            if(p.Op==3){byte[] bytes=Card.AsSpan((int)p.Offset,(int)p.Count).ToArray();corrupt?.Invoke(bytes);Cpu.Copy(p.Buffer,bytes);}
            else Bytes(p.Buffer,(int)p.Count).CopyTo(Card,(int)p.Offset);
        }
        Cpu.Put(0x80075B50,0);Cpu.Put(0x80075B58,1);Cpu.Put(0x80075B94,operation??p.Op);Cpu.Put(0x80075B98,result);Cpu.Put(Mock+0x30,unchecked((uint)returned));
    }
    internal void ReachHeader(){Frame();Complete();Frame();if(Flow!=2||Pending?.Offset!=0)throw new Exception("Header not armed from exact fresh precheck.");}
    internal void ReachTransaction(){ReachHeader();Complete();Frame();if(Flow!=3||Stage!=1)throw new Exception("Actual serializer/Begin did not enter controller.");}
    internal void ReachHeld()
    {ReachTransaction();for(int i=0;i<5;i++){Complete();Frame();}if(Flow!=4||Stage!=6)throw new Exception("Actual detached transaction did not reach held proposal.");}
    internal void PickupNext()
    {
        foreach(var(at,value)in Game.Lifecycle.LiveGuard!.ExpectedSceneGlobals)Cpu.Put(at,value);
        Game.Pickup(1);ConfigureGlobals();
    }
    internal void LoadCompleteWorkspace(NativeCollectibleSaveSchedulerPlan plan,bool reloadOverlay=true)
    {
        byte[] data=plan.CopyLevelData();
        uint W(int at)=>BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at));
        Cpu.Copy(plan.Lifecycle.Projection.ModelAddress,data.AsSpan((int)W(0x10),(int)W(0x14)).ToArray());
        Cpu.Copy(plan.Lifecycle.Projection.SceneAddress,data.AsSpan((int)W(0x18),(int)W(0x1C)).ToArray());
        if(reloadOverlay)Cpu.Copy(NativeCollectibleSaveSchedulerPlan.OverlayBase,plan.CopyOverlay());
        ConfigureGlobals();
    }
    private void ConfigureOrdinarySource()
    {
        Array.Clear(Ordinary);
        foreach(var(at,value,output)in new(uint,uint,int)[]{(0x8007596C,10,0),(0x80075754,3,1),(0x80075748,1,2),(0x800757A4,17,3),
            (0x80076240,1,8),(0x80075838,0,9),(0x8007583C,1,10),(0x8007582C,1,11)}){Cpu.Put(at,value);Ordinary[output]=(byte)value;}
        Cpu.Put(0x80075914,0x12345678);BinaryPrimitives.WriteUInt32LittleEndian(Ordinary.AsSpan(4),0x12345678);
        var random=new Random(176);
        void Copy(uint at,int output,int n){byte[] b=new byte[n];random.NextBytes(b);Cpu.Copy(at,b);b.CopyTo(Ordinary,output);}
        Copy(0x80078618,0x10,0x14);Copy(0x80078680,0x24,0x19);Copy(0x80078E78,0x40,0x24);Copy(0x8007A6A8,0x64,0x24);
        for(int level=0;level<36;level++)
        {uint d=(uint)random.Next(6),g=(uint)random.Next(501),egg=(uint)random.Next(3);Cpu.Put(0x800772D8+(uint)level*4,d);Cpu.Put(0x80077420+(uint)level*4,g);Cpu.Put(0x80076FE8+(uint)level*4,egg);
            Ordinary[0x88+level]=(byte)d;BinaryPrimitives.WriteUInt16LittleEndian(Ordinary.AsSpan(0xAC+2*level),(ushort)g);Ordinary[0xF4+level]=(byte)egg;}
        Copy(0x800758D0,0x106,6);Copy(0x80077908,0x10C,0x480);
        BinaryPrimitives.WriteUInt32LittleEndian(Ordinary.AsSpan(0x58C),(uint)Ordinary.AsSpan(0,0x58C).ToArray().Sum(b=>b));
    }
    private static byte[] RequestStub(uint op)
    {
        // t0 mock,t1 literal op,t2 source busy; arguments captured before return.
        List<uint> w=[0x3C08801D,0x24090001,0xAD090004,0x24090000|op,0xAD090008,0xAD05000C,0xAD060010,0xAD070014,
            0x8FA90010,0,0xAD090018,0xAD04001C,0x8D020000,0,0x24090001,0x14490004,0,0x3C0A8007,0x24090000|op,0xAD495B50,0x03E00008,0];
        return Words(w.ToArray());
    }
    private static byte[] PollStub()=>Words(0x3C088007,0x8D095B50,0,0x11200005,0xACA90000,0xACC00000,0x24020000,0x03E00008,0,
        0x3C088007,0x8D095B94,0,0xACA90000,0x8D095B98,0,0xACC90000,
        0x3C09801D,0x8D220030,0,0x240A0001,0x144A0002,0,0xAD005B58,0x03E00008,0);
    private static byte[] SerializerStub()=>Words(0x3C08801D,0x25080100,0x24090600,0x910A0000,0,0xA08A0000,0x25080001,
        0x24840001,0x2529FFFF,0x1520FFF9,0,0x03E00008,0);
    internal static byte[] Words(params uint[] words)
    {byte[] b=new byte[words.Length*4];for(int i=0;i<words.Length;i++)BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(i*4),words[i]);return b;}
}

using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;

// Service seams below are emitted mock routines, not BIOS/native card execution.
// Their asynchronous data transfer is an explicit host byte-array simulation.
internal sealed class ControllerFixture
{
    internal const uint Mock=0x801E0000, Stack=0x801FFCA0, Stop=0x80010000;
    internal NativeCollectibleSaveControllerKernel Kernel { get; }
    internal SaveCpuFixture Game { get; }=new(0);
    internal RenderProbeCpu Cpu=>Game.Cpu;
    internal byte[] InitialLedger { get; }
    internal byte[] Ordinary { get; }=new byte[0x600];
    internal byte[] Card { get; }=new byte[0x2000];
    internal byte[] OriginalCard { get; }
    internal List<(uint Op,uint Buffer,int Offset,int Count)> Requests { get; }=[];
    internal (uint Op,uint Buffer,int Offset,int Count)? Pending { get; private set; }
    internal int RequestReturn { get; set; }=1;
    internal Action<ControllerFixture>? BeforeServiceReturn { get; set; }
    internal uint? BeforeServicePc { get; set; }
    internal NativeCollectibleSaveControllerStage Stage=>(NativeCollectibleSaveControllerStage)Cpu.Get(Kernel.StateAddress);
    internal ControllerFixture(NativeCollectibleSaveControllerKernel? kernel=null)
    {
        Kernel=kernel??NativeCollectibleSaveControllerKernel.CreateFixture(0x800C0000);
        Game.Pickup(0);InitialLedger=Game.Ledger();new Random(931).NextBytes(Ordinary);
        BinaryPrimitives.WriteUInt32LittleEndian(Ordinary.AsSpan(0x58C),(uint)Ordinary.AsSpan(0,0x58C).ToArray().Sum(b=>b));
        new Random(923).NextBytes(Card);Game.Envelope.CopyTo(Card,0x1400);OriginalCard=Card.ToArray();
        Cpu.Copy(Kernel.ArenaAddress,new byte[NativeCollectibleSaveControllerKernel.ArenaBytes]);
        Cpu.Copy(Kernel.BeginPc,Kernel.CopyBytes());
        var p=Kernel.Preparation;Cpu.Copy(p.EntryPc,p.CopyBytes());Cpu.Copy(p.Sha256.EntryPc,p.Sha256.CopyBytes());
        Cpu.Copy(p.Loader.EntryPc,p.Loader.CopyBytes());Cpu.Copy(p.Query.EntryPc,p.Query.CopyBytes());
        Cpu.Copy(Kernel.OrdinaryInputAddress,Ordinary);Cpu.Copy(Kernel.FilenameAddress,Kernel.CopyFilename());
        Cpu.Copy(NativeCollectibleSaveControllerKernel.ReadPc,RequestStub(3));
        Cpu.Copy(NativeCollectibleSaveControllerKernel.WritePc,RequestStub(4));
        Cpu.Copy(NativeCollectibleSaveControllerKernel.PollPc,PollStub());
        Cpu.Put(0x800751B0,uint.MaxValue); // Exact source initializer: empty index.
    }
    internal byte[] Bytes(uint at,int count)=>SaveCpuFixture.Bytes(Cpu,at,count);
    internal uint Invoke(uint entry,Action<RenderProbeCpu>? before=null)
    {
        for(int r=1;r<32;r++)Cpu.Regs[r]=0xB7000000u+(uint)r;
        Cpu.Regs[4]=entry==Kernel.BeginPc?Kernel.OrdinaryInputAddress:Kernel.StateAddress;
        Cpu.Regs[5]=entry==Kernel.BeginPc?0x600u:0;Cpu.Regs[6]=entry==Kernel.BeginPc?SaveCpuFixture.Live:0;Cpu.Regs[7]=0;
        Cpu.Regs[29]=Stack;Cpu.Regs[31]=Stop;Cpu.Hi=0x12345678;Cpu.Lo=0x87654321;
        Cpu.Put(Mock,unchecked((uint)RequestReturn));Cpu.Put(Mock+4,0);
        before?.Invoke(Cpu);var regs=Cpu.Regs.ToArray();Cpu.Reads.Clear();Cpu.Writes.Clear();Cpu.Calls.Clear();Cpu.Visited.Clear();
        // Optional interleaving at the native-service boundary. It is a crafted
        // test mutation, not a concurrent runtime/session ownership proof.
        if(BeforeServiceReturn is {} action)
        {
            uint target=BeforeServicePc??(entry==Kernel.BeginPc?NativeCollectibleSaveControllerKernel.ReadPc:NativeCollectibleSaveControllerKernel.PollPc);
            Cpu.Run(entry,target);action(this);BeforeServiceReturn=null;BeforeServicePc=null;Cpu.Run(target,Stop);
        }
        else Cpu.Run(entry,Stop);
        bool In(uint at,uint first,int length)=>(ulong)at>=first&&(ulong)at<(ulong)first+(uint)length;
        bool Owned(uint at)=>In(at,Kernel.Preparation.OldTailAddress,0xB90)||
            In(at,Kernel.ExpectedOrdinaryAddress,0x1100)||In(at,Kernel.StateAddress,NativeCollectibleSaveControllerKernel.StateBytes)||
            In(at,unchecked(regs[29]-NativeCollectibleSaveControllerKernel.MaximumStackBytes),NativeCollectibleSaveControllerKernel.MaximumStackBytes)||
            In(at,Mock+4,0x1C)||In(at,0x80075B50,4)||In(at,0x80075B58,4);
        if(Cpu.Writes.Any(a=>!Owned(a)))throw new Exception("Controller/mock store escaped the full declared write complement: "+Cpu.Writes.First(a=>!Owned(a)).ToString("X8"));
        if(Cpu.Calls.Any(a=>!In(a,Kernel.BeginPc,Kernel.CodeByteLength)&&a!=Kernel.Preparation.EntryPc&&a!=Kernel.Preparation.Query.EntryPc&&
            a!=Kernel.Preparation.Loader.EntryPc&&a!=Kernel.Preparation.Sha256.EntryPc&&a!=NativeCollectibleSaveControllerKernel.ReadPc&&
            a!=NativeCollectibleSaveControllerKernel.WritePc&&a!=NativeCollectibleSaveControllerKernel.PollPc))throw new Exception("Undeclared emitted call.");
        if(new[]{4,5,6,7,16,17,18,19,20,21,22,23,26,27,28,29,30,31}.Any(r=>Cpu.Regs[r]!=regs[r])||
            Cpu.Hi!=0x12345678||Cpu.Lo!=0x87654321)throw new Exception("Controller ABI was not preserved.");
        if(Cpu.Get(Mock+4)!=0)
        {
            var request=(Cpu.Get(Mock+8),Cpu.Get(Mock+16),(int)Cpu.Get(Mock+20),(int)Cpu.Get(Mock+24));
            if(Cpu.Get(Mock+12)!=Kernel.FilenameAddress||Cpu.Get(Mock+28)!=0||
                !Bytes(Kernel.FilenameAddress,18).SequenceEqual("BASCUS-94228SPYRO\0"u8.ToArray()))throw new Exception("Wrong fixed filename/controller ABI.");
            Requests.Add(request);
            if(RequestReturn==1)Pending=request;
        }
        return Cpu.Regs[2];
    }
    internal void PendingPoll(uint? busy=null,uint latch=0)
    {uint op=busy??Pending!.Value.Op;ConfigurePoll(0,op,0,op,latch);}
    internal void Complete(int? copyBytes=null,Action<byte[]>? mutate=null,uint? op=null,uint result=0,int returned=1,uint busy=0,uint latch=0)
    {
        var request=Pending??throw new Exception("No simulated accepted request.");
        int count=copyBytes??request.Count;
        if(request.Op==4)Bytes(request.Buffer,count).CopyTo(Card,request.Offset);
        else
        {
            byte[] input=Card.AsSpan(request.Offset,request.Count).ToArray();mutate?.Invoke(input);
            Cpu.Copy(request.Buffer,input[..count]);
        }
        ConfigurePoll(returned,op??request.Op,result,busy,latch);Pending=null;
    }
    internal void ConfigurePoll(int returned,uint op,uint result,uint busy,uint latch)
    {Cpu.Put(Mock+0x40,unchecked((uint)returned));Cpu.Put(Mock+0x44,op);Cpu.Put(Mock+0x48,result);Cpu.Put(Mock+0x4C,busy);Cpu.Put(Mock+0x50,latch);}
    internal void Reach(NativeCollectibleSaveControllerStage goal)
    {
        if(Stage==NativeCollectibleSaveControllerStage.Idle)Invoke(Kernel.BeginPc);
        for(int n=0;Stage!=goal&&n<6;n++){Complete();Invoke(Kernel.PumpPc);}
        if(Stage!=goal)throw new Exception("Did not reach "+goal+"; actual "+Stage);
    }
    private static uint I(int op,int rs,int rt,int value)=>(uint)(op<<26|rs<<21|rt<<16)|(ushort)value;
    private static byte[] Words(IEnumerable<uint> words)
    {var a=words.ToArray();byte[] b=new byte[a.Length*4];for(int i=0;i<a.Length;i++)BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(i*4),a[i]);return b;}
    private static byte[] RequestStub(uint operation)
    {
        List<uint>w=[I(15,0,8,0x801E),I(0x23,8,2,0),0,I(9,0,9,1),I(0x2B,8,9,4),I(9,0,10,(int)operation),I(0x2B,8,10,8),
            I(0x2B,8,5,12),I(0x2B,8,6,16),I(0x2B,8,7,20),I(0x23,29,9,0x10),0,I(0x2B,8,9,24),I(0x2B,8,4,28),
            I(9,0,9,1),I(5,2,9,5),0,I(15,0,9,0x8007),I(0x2B,9,10,0x5B50),I(0x2B,9,0,0x5B58),0,
            0x01000011,0x01000013,0x03E00008,0];
        return Words(w);
    }
    private static byte[] PollStub()=>Words(new uint[]{I(15,0,8,0x801E),I(0x23,8,2,0x40),0,I(0x23,8,9,0x44),0,I(0x2B,5,9,0),
        I(0x23,8,9,0x48),0,I(0x2B,6,9,0),I(15,0,10,0x8007),I(0x23,8,9,0x4C),0,I(0x2B,10,9,0x5B50),
        I(0x23,8,9,0x50),0,I(0x2B,10,9,0x5B58),0x01000011,0x01000013,0x03E00008,0});
}

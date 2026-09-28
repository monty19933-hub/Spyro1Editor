using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Analysis;

// Real synchronous source request/poll code around emitted controller stages.
// Transfers and interrupt/dispatcher completion are HOST simulations. No BIOS,
// asynchronous callback, file, emulator or real card is executed by this test.
internal static class NativeRequestTransactionCases
{
    private static readonly (uint Pc,int Size,string Hash)[] Sources=[
        (0x80066E28,0x10C,"87ba1be6dead52b5b297e0587aad7c37f99b5676e497988c0846859e802f9b6d"),
        (0x800670E4,0x10C,"791708cc4c2bb40cdef08c7fdd0a4c8981abd9ede32105b305f25074c1bc868a"),
        (0x80067628,0xF0,"19553c82e1ac2ff3a3089e3c8b101ade63a08c9620216785f79df4a2d2da3e21"),
        (0x80067D74,0x5C,"ca057f2642941761ca53f90f4c7032e166e443f607406a745291292890d65ed1"),
        (0x800687D4,0xA8,"1f1aa65c0b63c8f8cd31b7ab2caf5dcf0c7a64e9035201b07a35dda0dd52b056"),
        (0x8006276C,0x30,"67eba1fda7d9cdea44c7e0341d218d166b95f8baf32341890895a84f25308943"),
        (0x80068F44,0x80,"7b25bd5394d6eb44fbe836882eb7bb866ec7c917813ebdd6e17b7037cc9e06ce"),
        (0x800121C0,6,"34dbbc1cb64c4fac89bc48fe166c3f1caf2d6b41871e529e5beab49472fc8aa9")];

    internal static void Run(NativeRegionalSourceSession source,NativeCollectibleSaveControllerKernel kernel)
    {
        int checks=0;
        void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
        byte[][] original=Sources.Select(s=>source.ReadExecutableRange((int)(s.Pc-0x8000F800),s.Size)).ToArray();
        for(int i=0;i<Sources.Length;i++)Check(Convert.ToHexStringLower(SHA256.HashData(original[i]))==Sources[i].Hash,"Actual request/poll closure source pin");
        foreach(uint port in new uint[]{0,16})foreach(bool laterPickup in new[]{false,true})
        {
            var f=new ControllerFixture(kernel);var cpu=f.Cpu;
            for(int i=0;i<Sources.Length;i++)cpu.Copy(Sources[i].Pc,original[i]);
            cpu.Put(SaveCpuFixture.Live+0x18,port>>4,1);cpu.Put(0x80078D18,port);
            byte[] live=f.Game.Ledger();int issued=0;
            bool In(uint at,uint start,int length)=>(ulong)at>=start&&(ulong)at<(ulong)start+(uint)length;
            uint Invoke(uint pc)
            {
                for(int r=1;r<32;r++)cpu.Regs[r]=0xB7000000u+(uint)r;
                cpu.Regs[4]=pc==kernel.BeginPc?kernel.OrdinaryInputAddress:kernel.StateAddress;
                cpu.Regs[5]=pc==kernel.BeginPc?0x600u:0;cpu.Regs[6]=pc==kernel.BeginPc?SaveCpuFixture.Live:0;cpu.Regs[7]=0;
                cpu.Regs[29]=ControllerFixture.Stack;cpu.Regs[31]=ControllerFixture.Stop;
                cpu.Hi=0x12345678;cpu.Lo=0x87654321;uint[] saved=cpu.Regs.ToArray();
                cpu.Reads.Clear();cpu.Writes.Clear();cpu.Calls.Clear();cpu.Visited.Clear();
                cpu.Run(pc,ControllerFixture.Stop);
                Check(new[]{4,5,6,7,16,17,18,19,20,21,22,23,26,27,28,29,30,31}.All(r=>cpu.Regs[r]==saved[r])&&
                    cpu.Hi==0x12345678&&cpu.Lo==0x87654321,"Complete real request/poll plus controller ABI preserved");
                Check(cpu.Writes.All(a=>In(a,kernel.Preparation.OldTailAddress,0xB90)||In(a,kernel.ExpectedOrdinaryAddress,0x1100)||
                    In(a,kernel.StateAddress,0x30)||In(a,ControllerFixture.Stack-(uint)kernel.MaximumStackByteLength,kernel.MaximumStackByteLength)||
                    kernel.IrqWindowsEmitted&&In(a,ControllerFixture.Mock+0x80,4)||In(a,0x80075B50,0x110)||a>=0x800751B0&&a<0x800751B4),
                    "Combined controller/native closure stores only declared buffers, globals, queue and bounded stack");
                Check(cpu.Visited.All(a=>In(a,kernel.BeginPc,kernel.CodeByteLength)||
                    In(a,kernel.Preparation.EntryPc,NativeCollectibleSaveTailKernel.CodeCapacityBytes)||Sources.Take(7).Any(s=>In(a,s.Pc,s.Size))||
                    kernel.IrqWindowsEmitted&&(In(a,NativeCollectibleSaveControllerKernel.EnterCriticalPc,8)||In(a,NativeCollectibleSaveControllerKernel.ExitCriticalPc,8)||
                    In(a,ControllerFixture.Mock+0x100,0x1C)||In(a,ControllerFixture.Mock+0x140,0x1C))),
                    "Only declared source/controller code and opt-in modeled IRQ veneers; no asynchronous callback, dispatcher, diagnostic or BIOS");
                Check(f.Game.Ledger().SequenceEqual(live),"Controller never commits over actual live progress");
                issued+=cpu.Calls.Count(a=>a==0x80066E28||a==0x800670E4);
                return cpu.Regs[2];
            }
            Check(Invoke(kernel.BeginPc)==1,"Actual initial Read accepted");
            var requests=new(uint Op,uint Buffer,uint Offset,uint Count)[]{
                (3,kernel.Preparation.OldTailAddress,0x1400,0x100),
                (4,kernel.ExpectedOrdinaryAddress,0x200,0x600),
                (3,kernel.OrdinaryReadbackAddress,0x200,0x600),
                (4,kernel.Preparation.OutputAddress+0x200,0x1400,0x100),
                (3,kernel.TailReadbackAddress,0x1400,0x100)};
            for(int i=0;i<requests.Length;i++)
            {
                var r=requests[i];
                Check(cpu.Get(0x80075B50)==r.Op&&cpu.Get(0x80075B58)==0&&cpu.Get(0x80075B5C)==port&&
                    cpu.Get(0x80075B64)==r.Offset&&cpu.Get(0x80075B68)==r.Count&&cpu.Get(0x80075B6C)==r.Buffer,
                    "Native request globals agree with independently specified stage transfer");
                Check(cpu.Get(0x800751B0)==0&&cpu.Get(0x80075C48)==(r.Op==3?0x80066F34u:0x800671F0u)&&
                    f.Bytes(0x80075C08,16).All(b=>b==0)&&f.Bytes(0x80075B70,23).SequenceEqual(Encoding.ASCII.GetBytes(port==0?"bu00:BASCUS-94228SPYRO\0":"bu10:BASCUS-94228SPYRO\0")),
                    "Actual callback queue record and controller-prefixed fixed filename match");
                Check(Invoke(kernel.PumpPc)==i+1&&cpu.Get(kernel.StateAddress+0x20)==2&&cpu.Get(kernel.StateAddress+0x2C)==0,
                    "Actual nonblocking poll remains Accepted while native Busy is pending");
                // Explicit host completion, NOT execution of callback or I/O.
                if(r.Op==3)cpu.Copy(r.Buffer,f.Card.AsSpan((int)r.Offset,(int)r.Count).ToArray());
                else f.Bytes(r.Buffer,(int)r.Count).CopyTo(f.Card,(int)r.Offset);
                cpu.Put(0x800751B0,uint.MaxValue);cpu.Put(0x80075B50,0);cpu.Put(0x80075B58,1);
                cpu.Put(0x80075B94,r.Op);cpu.Put(0x80075B98,0);
                Check(Invoke(kernel.PumpPc)==i+2,"Actual completion poll advances one exact controller stage");
                if(i==0&&laterPickup){f.Game.Pickup(1);live=f.Game.Ledger();}
            }
            Check(issued==5&&cpu.Get(kernel.StateAddress+0x20)==4&&cpu.Get(kernel.StateAddress+0x1C)==0x53564331,
                "Exactly five accepted native requests end in consumed held proposal");
            Check(f.Card.AsSpan(0x200,0x600).SequenceEqual(f.Ordinary)&&f.Card.AsSpan(0x1400,0x100).SequenceEqual(f.Bytes(kernel.Preparation.OutputAddress+0x200,0x100)),
                "Host card receives complete exact ordinary/custom transfer images");
            Check(Enumerable.Range(0,f.Card.Length).All(i=>i>=0x200&&i<0x800||i>=0x1400&&i<0x1500||f.Card[i]==f.OriginalCard[i])&&
                f.Card.AsSpan(0x1483,0x7D).SequenceEqual(f.OriginalCard.AsSpan(0x1483,0x7D)),"Complete card complement and unknown custom suffix retained");
            Check(cpu.Get(kernel.ProposalAddress+0x19,1)==(laterPickup?1u:0)&&cpu.Get(kernel.ProposalAddress+0x90,1)==(laterPickup?3u:1),
                "Later actual pickup remains in proposal and dirty; unchanged generation clears only proposal dirty flag");
            Check(Invoke(kernel.DrainPc)==2&&issued==5&&cpu.Get(0x80075B58)==0,"Consumed actual poll leaves terminal Drain clean without new service request");
            Check(Sources.Select((s,i)=>f.Bytes(s.Pc,s.Size).SequenceEqual(original[i])).All(same=>same),"Complete pinned source closure remains unchanged");
        }
        source.VerifyUnchanged();
        string marker=kernel.IrqWindowsEmitted?"NATIVE_REQUEST_TRANSACTION_IRQ_SOURCE_OK":"NATIVE_REQUEST_TRANSACTION_SOURCE_OK";
        Console.WriteLine($"{marker} checks={checks} synchronousSourceWrappers=true emittedController=true irqServicesMocked={kernel.IrqWindowsEmitted} hostTransfers=true asyncCallbacks=false nativeIo=false cardWrites=0 runtime=false");
    }
}

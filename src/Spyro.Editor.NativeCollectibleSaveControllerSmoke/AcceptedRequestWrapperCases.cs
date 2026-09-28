using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Analysis;

// Only the synchronous accepted wrapper closure is copied/executed. The queued
// callbacks, queue dispatcher and BIOS services are deliberately absent. These
// private CPU buffers/stack are test fixtures, not native lease allocations.
internal static class AcceptedRequestWrapperCases
{
    private const uint Stack=0x801F0000,Buffer=0x800D0000,Stop=0x80010000;
    private static readonly (uint Pc,int Size,string Hash)[] Spans=[
        (0x80066E28,0x10C,"87ba1be6dead52b5b297e0587aad7c37f99b5676e497988c0846859e802f9b6d"),
        (0x800670E4,0x10C,"791708cc4c2bb40cdef08c7fdd0a4c8981abd9ede32105b305f25074c1bc868a"),
        (0x80067D74,0x5C,"ca057f2642941761ca53f90f4c7032e166e443f607406a745291292890d65ed1"),
        (0x800687D4,0xA8,"1f1aa65c0b63c8f8cd31b7ab2caf5dcf0c7a64e9035201b07a35dda0dd52b056"),
        (0x8006276C,0x30,"67eba1fda7d9cdea44c7e0341d218d166b95f8baf32341890895a84f25308943"),
        (0x80068F44,0x80,"7b25bd5394d6eb44fbe836882eb7bb866ec7c917813ebdd6e17b7037cc9e06ce"),
        (0x800121C0,6,"34dbbc1cb64c4fac89bc48fe166c3f1caf2d6b41871e529e5beab49472fc8aa9"),
        (0x80010DB8,18,"3accd2edff094699543c0038659c03c8bd088393b7c87accacfb7c5a6f9d65c3")];
    private static byte[] Bytes(RenderProbeCpu c,uint address,int count)=>
        Enumerable.Range(0,count).Select(i=>(byte)c.Get(address+(uint)i,1)).ToArray();
    internal static void Run(NativeRegionalSourceSession source,Action<bool,string> check)
    {
        try
        {
            byte[][] owners=Spans.Select(s=>source.ReadExecutableRange((int)(s.Pc-0x8000F800),s.Size)).ToArray();
            for(int i=0;i<Spans.Length;i++)check(Convert.ToHexStringLower(SHA256.HashData(owners[i]))==Spans[i].Hash,"Exact original accepted request closure/data source pin");
            foreach(bool write in new[]{false,true})foreach(uint controller in new uint[]{0,16})foreach(int previous in new[]{-1,0,1,2})
            {
                var cpu=new RenderProbeCpu{StrictSignedArithmetic=true};
                for(int i=0;i<Spans.Length;i++)cpu.Copy(Spans[i].Pc,owners[i]);
                int count=write?0x600:0x100;uint offset=write?0x200u:0x1400u,operation=write?4u:3u;
                uint entry=write?0x800670E4u:0x80066E28u,callback=write?0x800671F0u:0x80066F34u;
                byte[] payload=Enumerable.Range(0,0x620).Select(i=>(byte)(i*31+9)).ToArray();cpu.Copy(Buffer-16,payload);
                byte[] globals=Enumerable.Repeat((byte)0xA5,0x140).ToArray();cpu.Copy(0x80075B40,globals);
                cpu.Put(0x80075B50,0);cpu.Put(0x800751B0,unchecked((uint)previous));
                byte[] stack=Enumerable.Range(0,0x90).Select(i=>(byte)(i*13+3)).ToArray();cpu.Copy(Stack-0x60,stack);
                cpu.Put(Stack+0x10,(uint)count);byte[] caller=Bytes(cpu,Stack,0x30);
                for(int r=1;r<32;r++)cpu.Regs[r]=0xB7000000u+(uint)r;
                cpu.Regs[4]=controller;cpu.Regs[5]=0x80010DB8;cpu.Regs[6]=Buffer;cpu.Regs[7]=offset;
                cpu.Regs[29]=Stack;cpu.Regs[31]=Stop;cpu.Hi=0x12345678;cpu.Lo=0x89ABCDEF;uint[] saved=cpu.Regs.ToArray();
                // Stop only at retired pipeline boundaries. This proves actual
                // executed SP low-water, rather than inferring it from stores.
                cpu.Run(entry,0x800687D4);check(cpu.Regs[29]==Stack-0x30,"Original request owns30-byte frame before concat");
                cpu.Run(0x800687D4,0x800687D8);check(cpu.Regs[29]==Stack-0x50,"Actual concat prologue reaches synchronous50-byte nested stack");
                cpu.Run(0x800687D8,Stop);
                check(cpu.Regs[2]==1,"Exact native wrapper returns accepted1 after enqueue");
                check(new[]{16,17,18,19,20,21,22,23,28,29,30,31}.All(r=>cpu.Regs[r]==saved[r])&&cpu.Hi==0x12345678&&cpu.Lo==0x89ABCDEF,
                    "Actual request closure restores callee-saved registers/SP/RA and preserves HI/LO");
                check(cpu.Reads.Contains(Stack+0x10)&&cpu.Get(0x80075B68)==count&&Bytes(cpu,Stack,0x30).SequenceEqual(caller),
                    "Fifth argument comes from caller SP+10; caller frame is unchanged");
                void ExpectedWord(uint address,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(globals.AsSpan((int)(address-0x80075B40)),value);
                ExpectedWord(0x80075B50,operation);ExpectedWord(0x80075B54,0);ExpectedWord(0x80075B58,0);
                ExpectedWord(0x80075B5C,controller);ExpectedWord(0x80075B64,offset);ExpectedWord(0x80075B68,(uint)count);ExpectedWord(0x80075B6C,Buffer);
                byte[] filename=Encoding.ASCII.GetBytes(controller==0?"bu00:BASCUS-94228SPYRO\0":"bu10:BASCUS-94228SPYRO\0");
                filename.CopyTo(globals,0x30);
                uint slot=(uint)(previous+1),record=0x80075C08+slot*16,pointer=0x80075C48+slot*4;
                for(int i=0;i<4;i++)ExpectedWord(record+(uint)i*4,0);ExpectedWord(pointer,callback);
                check(Bytes(cpu,0x80075B40,globals.Length).SequenceEqual(globals),"Complete request globals/filename/queue owner equals independent expected bytes, including complement");
                check(cpu.Get(0x800751B0)==slot&&cpu.Get(pointer)==callback&&Bytes(cpu,record,16).All(b=>b==0),
                    "Only bounded new queue slot publishes exact callback and zero record");
                var allowed=new HashSet<uint>();
                void Permit(uint address,int bytes){for(int i=0;i<bytes;i++)allowed.Add(address+(uint)i);}
                foreach(uint address in new uint[]{0x80075B50,0x80075B54,0x80075B58,0x80075B5C,0x80075B64,0x80075B68,0x80075B6C,0x800751B0,pointer})Permit(address,4);
                Permit(0x80075B70,filename.Length);Permit(record,16);Permit(Stack-0x50,0x50);
                check(cpu.Writes.All(allowed.Contains)&&Bytes(cpu,Buffer-16,payload.Length).SequenceEqual(payload)&&Bytes(cpu,Stack-0x60,0x10).SequenceEqual(stack[..0x10]),
                    "Exact accepted closure stores only declared globals/queue/filename/own frames; transfer payload and boundaries unchanged");
                check(cpu.Calls.SequenceEqual(new uint[]{0x80067D74,0x800687D4,0x8006276C,0x8006276C,0x80068F44})&&
                    cpu.Visited.All(pc=>Spans.Take(6).Any(s=>pc>=s.Pc&&pc<s.Pc+s.Size)),
                    "Only synchronous source closure executes: no callback, dispatcher, diagnostic, BIOS or card transfer");
                check(Spans.Select((s,i)=>Bytes(cpu,s.Pc,s.Size).SequenceEqual(owners[i])).All(same=>same),"Complete pinned code/prefix/filename owners remain unchanged");
            }
        }
        finally{source.VerifyUnchanged();}
    }
}

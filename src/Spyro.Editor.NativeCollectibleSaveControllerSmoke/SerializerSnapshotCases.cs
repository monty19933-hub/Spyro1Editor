using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

// Exact original serializer/helper execution, followed by the detached controller
// with mocked card services. Neither the host copy nor the fixture address below
// is a native hook, asynchronous lease or proof of the real frame-scratch owner.
internal static class SerializerSnapshotCases
{
    internal static void Run(NativeRegionalSourceSession source,
        NativeCollectibleSaveControllerKernel kernel, Action<bool,string> check)
    {
        var spans = new (uint Pc,int Size,string Hash)[] {
            (0x80059864,0x1E4,"b8674b556d8f4154886306731ee50c9812f2cac1d9a224756c110be693d19056"),
            (0x80016914,0x98,"1c16f5a238ffa3b6160e60fd849b1ff89f7a409cb22a39bf5ae31fb0d5f137ca"),
            (0x8005956C,0x28,"d83b069d73f991a0ea4b1a318759f6ea66c5606de361242bab29d02c840824db") };
        var code = spans.Select(s => source.ReadExecutableRange((int)(s.Pc-0x8000F800),s.Size)).ToArray();
        for(int i=0;i<spans.Length;i++)
            check(Convert.ToHexStringLower(SHA256.HashData(code[i]))==spans[i].Hash,"Exact original serializer/helper source pin");
        const uint scratch=0x801D0000;
        foreach(int seed in new[]{0,1,17,913})
        {
            var f=new ControllerFixture(kernel);var cpu=f.Cpu;var random=new Random(seed);
            byte[] expected=new byte[0x600];
            for(int i=0;i<spans.Length;i++)cpu.Copy(spans[i].Pc,code[i]);
            void Scalar(uint address,int output,uint value,bool word=false)
            {
                cpu.Put(address,value);
                if(word)BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(output),value);
                else expected[output]=(byte)value;
            }
            Scalar(0x8007596C,0,10);Scalar(0x80075754,1,3);Scalar(0x80075748,2,1);
            Scalar(0x800757A4,3,(uint)seed);Scalar(0x80075914,4,0x12345678u+(uint)seed,true);
            Scalar(0x80076240,8,1);Scalar(0x80075838,9,0);Scalar(0x8007583C,10,1);Scalar(0x8007582C,11,1);
            void Block(uint address,int output,int length)
            {byte[] b=new byte[length];random.NextBytes(b);cpu.Copy(address,b);b.CopyTo(expected,output);}
            Block(0x80078618,0x10,0x14);Block(0x80078680,0x24,0x19);
            Block(0x80078E78,0x40,0x24);Block(0x8007A6A8,0x64,0x24);
            for(int level=0;level<36;level++)
            {
                uint dragons=(uint)random.Next(0,6),gems=(uint)random.Next(0,501),eggs=(uint)random.Next(0,3);
                cpu.Put(0x800772D8+(uint)level*4,dragons);cpu.Put(0x80077420+(uint)level*4,gems);
                cpu.Put(0x80076FE8+(uint)level*4,eggs);
                expected[0x88+level]=(byte)dragons;
                BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0xAC+level*2),(ushort)gems);
                expected[0xF4+level]=(byte)eggs;
            }
            // Retail serializer writes 36 egg bytes first, then overwrites the
            // overlapping last18 with these fields. Preserve that exact format.
            Block(0x800758D0,0x106,6);Block(0x80077908,0x10C,0x480);
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(0x58C),(uint)expected.AsSpan(0,0x58C).ToArray().Sum(b=>b));
            cpu.Copy(scratch-16,Enumerable.Repeat((byte)0xA5,0x620).ToArray());
            for(int r=1;r<32;r++)cpu.Regs[r]=0xB7000000u+(uint)r;
            cpu.Regs[4]=scratch;cpu.Regs[29]=ControllerFixture.Stack;cpu.Regs[31]=ControllerFixture.Stop;
            var saved=cpu.Regs.ToArray();cpu.Reads.Clear();cpu.Writes.Clear();cpu.Calls.Clear();cpu.Visited.Clear();
            cpu.StrictSignedArithmetic=true;cpu.Run(0x80059864,ControllerFixture.Stop);
            byte[] actual=f.Bytes(scratch,0x600);
            check(actual.SequenceEqual(expected),"Full original serializer image equals independent ordinary-slot model, including zero padding");
            check(cpu.Writes.All(a=>a>=scratch&&a<scratch+0x600||a>=ControllerFixture.Stack-0x20&&a<ControllerFixture.Stack),
                "Original serializer stores only its full ordinary buffer and20-byte frame");
            check(new[]{16,17,18,19,20,21,22,23,28,29,30,31}.All(r=>cpu.Regs[r]==saved[r]),"Original serializer restores its callee-saved registers and caller frame");
            check(cpu.Calls.SequenceEqual(new uint[]{0x80016914,0x80016958,0x80016958,0x80016958,0x80016958,0x8005956C}),
                "Exact original clear/copy/checksum helper closure executes, without BIOS or card calls");
            check(f.Bytes(scratch-16,16).Concat(f.Bytes(scratch+0x600,16)).All(b=>b==0xA5),"Both serializer output boundary sentinels survive");
            // Explicit host capture models the future adapter's post-serializer
            // copy. It does not claim the native callback performs this copy.
            actual.CopyTo(f.Ordinary,0);cpu.Copy(kernel.OrdinaryInputAddress,actual);
            f.Reach(NativeCollectibleSaveControllerStage.Committed);
            check(f.Card.AsSpan(0x200,0x600).SequenceEqual(actual)&&f.Bytes(kernel.ExpectedOrdinaryAddress,0x600).SequenceEqual(actual),
                "Actual serializer output survives Begin snapshot and complete simulated write/readback");
            check(f.Bytes(scratch,0x600).SequenceEqual(actual)&&f.Game.Ledger().SequenceEqual(f.InitialLedger),
                "Controller leaves original ordinary scratch and live ledger unchanged");
        }
        source.VerifyUnchanged();
    }
}

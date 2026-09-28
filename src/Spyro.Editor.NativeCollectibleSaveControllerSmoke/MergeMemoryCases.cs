using System.Buffers.Binary;

// Byte-by-byte oracle, separate from the probe's word/shift implementation.
// ISA cross-check: DuckStation cpu_core.cpp at d78fd23db934cabc38d66e9a3dd8106f27a00772,
// LWL/LWR/SWL/SWR and WriteRegDelayed. No emulator/runtime certification.
// https://github.com/stenzek/duckstation/blob/d78fd23db934cabc38d66e9a3dd8106f27a00772/src/core/cpu_core.cpp
internal static class MergeMemoryCases
{
    private const uint Code=0x80010000,Data=0x80018000,Old=0xD3B59779;
    private static uint Mem(int op,int rt,int offset=0)=>
        (uint)op<<26|4u<<21|(uint)rt<<16|(ushort)offset;
    private static uint Move(int dst,int src)=>(uint)src<<21|(uint)dst<<11|0x21;
    private static RenderProbeCpu Cpu(params uint[] words)
    {
        var cpu=new RenderProbeCpu();for(int i=0;i<words.Length;i++)cpu.Put(Code+(uint)i*4,words[i]);
        cpu.Regs[2]=Old;cpu.Regs[4]=Data;return cpu;
    }
    private static byte[] Bytes(RenderProbeCpu c,uint address,int count)=>
        Enumerable.Range(0,count).Select(i=>(byte)c.Get(address+(uint)i,1)).ToArray();
    private static byte[] Word(uint value)
    {byte[] b=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(b,value);return b;}
    private static uint Merge(int op,int lane,byte[] memory,byte[] prior)
    {
        byte[] result=prior.ToArray();
        if(op==0x22)for(int i=0;i<=lane;i++)result[3-lane+i]=memory[i];
        else for(int i=lane;i<4;i++)result[i-lane]=memory[i];
        return BinaryPrimitives.ReadUInt32LittleEndian(result);
    }
    private static uint[] StoreOracle(int op,int lane,uint aligned,byte[] memory,int start,uint value)
    {
        byte[] register=Word(value);var writes=new List<uint>();
        if(op==0x2A)for(int i=0;i<=lane;i++){memory[start+i]=register[3-lane+i];writes.Add(aligned+(uint)i);}
        else for(int i=lane;i<4;i++){memory[start+i]=register[i-lane];writes.Add(aligned+(uint)i);}
        return writes.ToArray();
    }
    internal static void Run(Action<bool,string> check)
    {
        byte[] pattern=Enumerable.Range(0,32).Select(i=>(byte)(17+i*7)).ToArray();
        var missing=new List<Exception>();
        foreach(int op in new[]{0x22,0x26,0x2A,0x2E})
        {
            var c=Cpu(Mem(op,2,1),0);c.Copy(Data,pattern);
            try{c.Run(Code,Code+8);}
            catch(Exception e){missing.Add(new Exception($"Required merge opcode {op:X2}",e));}
        }
        if(missing.Count!=0)throw new AggregateException("Merge instruction regression",missing);
        check(true,"All four merge opcodes execute before detailed independent oracles");
        foreach(int op in new[]{0x22,0x26})for(int lane=0;lane<4;lane++)
        {
            var c=Cpu(Mem(op,2,lane),Move(9,2),Move(10,2),0);c.Copy(Data,pattern);
            c.Run(Code,Code+16);
            check(c.Regs[2]==Merge(op,lane,pattern[..4],Word(Old)),"Each merge-load lane equals independent byte oracle");
            check(c.Regs[9]==Old&&c.Regs[10]==c.Regs[2],"Single merge retains exactly one ordinary load delay");
            check(c.Reads.SequenceEqual(new[]{Data})&&c.Writes.Count==0&&Bytes(c,Data,32).SequenceEqual(pattern),"Merge load reads aligned owner and never writes memory");
        }
        foreach(int op in new[]{0x2A,0x2E})for(int lane=0;lane<4;lane++)
        {
            var c=Cpu(Mem(op,2,lane),0);c.Copy(Data,pattern);byte[] expected=pattern.ToArray();
            uint[] writes=StoreOracle(op,lane,Data,expected,0,Old);c.Run(Code,Code+8);
            check(Bytes(c,Data,32).SequenceEqual(expected),"Each merge-store lane and complete byte complement match independent oracle");
            check(c.Writes.SequenceEqual(writes)&&c.Reads.Count==0&&c.Regs[2]==Old,"Only selected merge-store bytes are tracked as writes, without synthetic reads");
        }
        for(int lane=0;lane<4;lane++)foreach(bool reverse in new[]{false,true})
        {
            uint left=Mem(0x22,2,lane+3),right=Mem(0x26,2,lane);
            var c=Cpu(reverse?right:left,reverse?left:right,Move(9,2),Move(10,2),0);c.Copy(Data,pattern);
            c.Run(Code,Code+20);
            check(c.Regs[2]==BinaryPrimitives.ReadUInt32LittleEndian(pattern.AsSpan(lane,4)),"Adjacent merge pair reconstructs unaligned word in both orders");
            check(c.Regs[9]==Old&&c.Regs[10]==c.Regs[2],"Pair forwards pending value but cancels first retirement until final load delay");
            check(c.Writes.Count==0&&Bytes(c,Data,32).SequenceEqual(pattern),"Both merge-load orders preserve all memory");
            uint sl=Mem(0x2A,2,lane+3),sr=Mem(0x2E,2,lane);
            var s=Cpu(reverse?sr:sl,reverse?sl:sr,0);s.Copy(Data,pattern);byte[] expected=pattern.ToArray();
            byte[] register=Word(Old);register.CopyTo(expected,lane);s.Run(Code,Code+12);
            check(Bytes(s,Data,32).SequenceEqual(expected),"Both merge-store orders touch exactly the unaligned four-byte destination");
            var scratch=pattern.ToArray();var expectedWrites=new List<uint>();
            foreach(var (op,offset) in reverse?new[]{(0x2E,lane),(0x2A,lane+3)}:new[]{(0x2A,lane+3),(0x2E,lane)})
                expectedWrites.AddRange(StoreOracle(op,offset&3,Data+(uint)(offset&~3),scratch,offset&~3,Old));
            check(s.Writes.SequenceEqual(expectedWrites),"Paired stores retain exact per-instruction byte writes, including aligned overlap");
        }
        foreach(int op in new[]{0x22,0x26})
        {
            var c=Cpu(Mem(0x23,2,4),Mem(op,2,1),Move(9,2),Move(10,2),0);c.Copy(Data,pattern);c.Run(Code,Code+20);
            check(c.Regs[9]==Old&&c.Regs[10]==Merge(op,1,pattern[..4],pattern[4..8]),"Merge forwards any matching pending load, not only another merge");
            var s=Cpu(Mem(0x23,2,4),Mem(op==0x22?0x2A:0x2E,2,1),Move(9,2),0);s.Copy(Data,pattern);
            byte[] expected=pattern.ToArray();StoreOracle(op==0x22?0x2A:0x2E,1,Data,expected,0,Old);s.Run(Code,Code+16);
            check(Bytes(s,Data,32).SequenceEqual(expected)&&s.Regs[9]==BinaryPrimitives.ReadUInt32LittleEndian(pattern.AsSpan(4,4)),"Merge store uses visible register, not pending-load bypass");
            var distinct=Cpu(Mem(0x23,3,4),Mem(op,2,1),Move(9,2),Move(10,2),0);distinct.Copy(Data,pattern);distinct.Run(Code,Code+20);
            check(distinct.Regs[9]==Old&&distinct.Regs[10]==Merge(op,1,pattern[..4],Word(Old))&&distinct.Regs[3]==BinaryPrimitives.ReadUInt32LittleEndian(pattern.AsSpan(4,4)),"Different pending destination retires without forwarding into merge");
            var overwrite=Cpu(Mem(op,2,1),0x24021234,Move(9,2),0);overwrite.Copy(Data,pattern);overwrite.Run(Code,Code+16);
            check(overwrite.Regs[2]==0x1234&&overwrite.Regs[9]==0x1234,"Immediate register overwrite cancels merge load destination");
        }
        {
            var c=Cpu(Mem(0x23,2),Mem(0x23,2,4),Move(9,2),Move(10,2),0);c.Copy(Data,pattern);c.Run(Code,Code+20);
            check(c.Regs[9]==Old&&c.Regs[10]==BinaryPrimitives.ReadUInt32LittleEndian(pattern.AsSpan(4,4)),"Ordinary adjacent loads to same register also cancel first retirement");
            var c2=Cpu(Mem(0x23,2),Move(9,2),Move(10,2),0);c2.Copy(Data,pattern);c2.Run(Code,Code+16);
            check(c2.Regs[9]==Old&&c2.Regs[10]==BinaryPrimitives.ReadUInt32LittleEndian(pattern),"Ordinary single-load delay unchanged");
        }
        foreach(int op in new[]{0x22,0x26,0x23})foreach(int lane in op==0x23?new[]{0}:new[]{0,1,2,3})
        {
            var c=Cpu(Mem(op,0,lane));c.Copy(Data,pattern);c.Run(Code,Code+4);
            check(c.Regs[0]==0&&c.BoundaryLoad is null&&c.Reads.Count==1,"Zero-register loads still access memory but never leave pending state");
        }
        foreach(int op in new[]{0x2A,0x2E})for(int lane=0;lane<4;lane++)
        {
            var c=Cpu(Mem(op,0,lane),0);c.Copy(Data,pattern);byte[] expected=pattern.ToArray();
            StoreOracle(op,lane,Data,expected,0,0);c.Run(Code,Code+8);
            check(Bytes(c,Data,32).SequenceEqual(expected)&&c.Regs[0]==0,"Zero-register merge stores affect only chosen lanes");
        }
        // Every owner edge is a complete aligned word, including the final RAM
        // and scratch words. Effective addresses outside those owners refuse.
        foreach(uint aligned in new uint[]{0,0x1FFFFC,0x80000000,0x801FFFFC,0x1F800000,0x1F8003FC})
        foreach(int op in new[]{0x22,0x26,0x2A,0x2E})for(int lane=0;lane<4;lane++)
        {
            var c=Cpu(Mem(op,2,lane),0);c.Regs[4]=aligned;c.Copy(aligned,pattern[..4]);c.Run(Code,Code+8);
            if(op is 0x22 or 0x26)check(c.Regs[2]==Merge(op,lane,pattern[..4],Word(Old)),"Merge load owns exact boundary word without reading outside it");
            else{byte[] expected=pattern[..4];StoreOracle(op,lane,aligned,expected,0,Old);check(Bytes(c,aligned,4).SequenceEqual(expected),"Merge store owns exact boundary word without writing outside it");}
        }
        foreach(uint invalid in new uint[]{0x200000,0x7FFFFFFF,0x80200000,0x1F800400})foreach(int op in new[]{0x22,0x26,0x2A,0x2E})foreach(int rt in new[]{0,2})
        {
            var c=Cpu(Mem(op,rt),0);c.Regs[4]=invalid;bool refused=false;
            try{c.Run(Code,Code+8);}catch(Exception e)when(e.Message.StartsWith("Unowned test address ")){refused=true;}
            check(refused&&c.Writes.Count==0,"Unowned aligned merge access refuses before writes");
        }
        // Address sign-extension and rt==rs both use pre-instruction registers.
        foreach(int op in new[]{0x22,0x26})
        {
            var c=Cpu(Mem(op,4,-3),0);c.Copy(Data,pattern);c.Regs[4]=Data+4;c.Run(Code,Code+8);
            check(c.Regs[4]==Merge(op,1,pattern[..4],Word(Data+4)),"Signed offset and base/destination alias use old base");
        }
    }
}

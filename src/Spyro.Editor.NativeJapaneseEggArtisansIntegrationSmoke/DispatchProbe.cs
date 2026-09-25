using System.Buffers.Binary;

// Independent interpreter for the source comparison tree and new leaf guard.
// Stops at the first handler, before any actor behavior. Delay slots execute.
internal static class DispatchProbe
{
    internal static (uint Target, uint[] Registers) Trace(byte[] overlay, ushort actor, uint guard, int guardLength)
    {
        const uint basis=0x8007AA38, start=0x8007DA9C, end=0x8007DD04;
        uint[] r=Enumerable.Range(0,32).Select(i=>(uint)(0x12340000+i)).ToArray();r[0]=0;r[3]=unchecked((uint)(short)actor);r[30]=1;
        uint pc=start;uint? pending=null;
        for(int step=0;step<256;step++)
        {
            bool owned=pc>=start&&pc<end||pc>=guard&&pc<guard+(uint)guardLength;
            if(!owned&&pending==null)return(pc,r);
            uint w=BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(checked((int)(pc-basis))));
            int op=(int)(w>>26),rs=(int)(w>>21)&31,rt=(int)(w>>16)&31;int imm=(short)w;
            uint? old=pending;pending=null;uint next=pc+4;
            switch(op)
            {
                case 0:if(w!=0)throw new Exception("Unexpected tree SPECIAL "+w.ToString("X8"));break;
                case 2:pending=((pc+4)&0xF0000000)|((w&0x3FFFFFF)<<2);break;
                case 4:pending=r[rs]==r[rt]?unchecked(pc+4+(uint)(imm*4)):pc+8;break;
                case 5:pending=r[rs]!=r[rt]?unchecked(pc+4+(uint)(imm*4)):pc+8;break;
                case 9:r[rt]=unchecked(r[rs]+(uint)imm);break;
                case 10:r[rt]=(int)r[rs]<imm?1u:0;break;
                case 11:r[rt]=r[rs]<unchecked((uint)imm)?1u:0;break;
                case 15:r[rt]=w<<16;break;
                default:throw new Exception($"Unexpected tree opcode {w:X8} at {pc:X8}");
            }
            r[0]=0;if(old is uint target){if(pending!=null)throw new Exception("Tree transfer in delay slot");next=target;}pc=next;
        }
        throw new Exception("Bounded tree probe exhausted");
    }
}

using System.Buffers.Binary;

// Independent bounded test interpreter: branch delay AND one-instruction load
// delay. It runs only this package's generated code and exact helper windows;
// it is not a PS1 emulator or renderer/runtime acceptance.
internal sealed class TinyR3000
{
    private readonly byte[] _ram=new byte[0x200000];
    internal uint[] Regs { get; }=new uint[32];
    internal uint Hi,Lo;
    internal List<uint> Writes { get; }=[];
    internal List<uint> Calls { get; }=[];
    internal HashSet<uint> Visited { get; }=[];
    private static int At(uint a,int n)
    {if(a<0x80000000||(ulong)a+(uint)n>0x80200000)throw new Exception("Unowned test address "+a.ToString("X8"));return (int)(a-0x80000000);}
    internal void Copy(uint a,byte[] bytes)=>bytes.CopyTo(_ram,At(a,bytes.Length));
    internal uint Get(uint a,int n=4)=>n switch{1=>_ram[At(a,1)],2=>BinaryPrimitives.ReadUInt16LittleEndian(_ram.AsSpan(At(a,2))),_=>BinaryPrimitives.ReadUInt32LittleEndian(_ram.AsSpan(At(a,4)))};
    internal void Put(uint a,uint v,int n=4)
    {int at=At(a,n);if(n==1)_ram[at]=(byte)v;else if(n==2)BinaryPrimitives.WriteUInt16LittleEndian(_ram.AsSpan(at),(ushort)v);else BinaryPrimitives.WriteUInt32LittleEndian(_ram.AsSpan(at),v);}
    internal void Run(uint start,uint stop)
    {
        uint pc=start;uint? pendingJump=null;(int Reg,uint Value)? load=null;
        for(int steps=0;steps<10000;steps++)
        {
            if(pc==stop){if(pendingJump!=null||load!=null)throw new Exception("Unretired test delay");return;}
            Visited.Add(pc);if(pc is 0x80016C58 or 0x8006272C or 0x80038EE0)Calls.Add(pc);
            uint w=Get(pc),op=w>>26;int rs=(int)(w>>21)&31,rt=(int)(w>>16)&31,rd=(int)(w>>11)&31,shift=(int)(w>>6)&31;
            int imm=(short)w;uint a=Regs[rs],b=Regs[rt],next=pc+4;var oldLoad=load;load=null;var oldJump=pendingJump;pendingJump=null;
            (int Reg,uint Value)? write=null;
            void Set(int r,uint v)=>write=(r,v);
            void Branch(bool take){pendingJump=take?unchecked(pc+4+(uint)(imm*4)):pc+8;}
            switch(op)
            {
                case 0:switch(w&63)
                {
                    case 0:Set(rd,b<<shift);break;case 2:Set(rd,b>>shift);break;case 3:Set(rd,unchecked((uint)((int)b>>shift)));break;
                    case 8:pendingJump=a;break;case 16:Set(rd,Hi);break;case 18:Set(rd,Lo);break;case 17:Hi=a;break;case 19:Lo=a;break;
                    case 24:long product=(long)(int)a*(int)b;Lo=(uint)product;Hi=(uint)(product>>32);break;
                    case 32:case 33:Set(rd,unchecked(a+b));break;case 34:case 35:Set(rd,unchecked(a-b));break;
                    case 36:Set(rd,a&b);break;case 37:Set(rd,a|b);break;case 38:Set(rd,a^b);break;
                    case 42:Set(rd,(int)a<(int)b?1u:0);break;case 43:Set(rd,a<b?1u:0);break;
                    default:throw new Exception("Unknown SPECIAL "+w.ToString("X8"));
                }break;
                case 1:if(rt is not 0 and not 1)throw new Exception("Unsupported REGIMM");Branch(rt==0?(int)a<0:(int)a>=0);break;
                case 2:case 3:pendingJump=((pc+4)&0xF0000000)|((w&0x3FFFFFF)<<2);if(op==3)Set(31,pc+8);break;
                case 4:Branch(a==b);break;case 5:Branch(a!=b);break;case 6:Branch((int)a<=0);break;case 7:Branch((int)a>0);break;
                case 8:case 9:Set(rt,unchecked(a+(uint)imm));break;case 10:Set(rt,(int)a<imm?1u:0);break;
                case 11:Set(rt,a<unchecked((uint)imm)?1u:0);break;case 12:Set(rt,a&(w&65535));break;case 13:Set(rt,a|(w&65535));break;case 15:Set(rt,w<<16);break;
                case 0x20:case 0x21:case 0x23:case 0x24:case 0x25:
                    int size=op is 0x20 or 0x24?1:op is 0x21 or 0x25?2:4;uint value=Get(unchecked(a+(uint)imm),size);
                    if(op==0x20)value=unchecked((uint)(sbyte)value);if(op==0x21)value=unchecked((uint)(short)value);load=(rt,value);break;
                case 0x28:case 0x29:case 0x2B:
                    int width=op==0x28?1:op==0x29?2:4;uint address=unchecked(a+(uint)imm);Put(address,b,width);
                    for(int i=0;i<width;i++)Writes.Add(address+(uint)i);break;
                default:throw new Exception("Unknown test opcode "+w.ToString("X8")+" at "+pc.ToString("X8"));
            }
            if(oldLoad is {} l)Regs[l.Reg]=l.Value;if(write is {} r)Regs[r.Reg]=r.Value;Regs[0]=0;
            if(oldJump is uint destination){if(pendingJump!=null)throw new Exception("Transfer in delay slot");next=destination;}
            pc=next;
        }
        throw new Exception("Test CPU budget exceeded.");
    }
}

using System.Buffers.Binary;

// Test-bound arithmetic stop, not execution of the PS1 exception handler.
internal sealed class RenderProbeArithmeticOverflow(uint pc,bool branchDelaySlot) : ArithmeticException("R3000 signed arithmetic overflow at "+pc.ToString("X8"))
{
    internal uint Pc { get; }=pc;
    internal bool BranchDelaySlot { get; }=branchDelaySlot;
}

// Independent bounded test interpreter: branch delay AND one-instruction load
// delay. It runs only this package's generated code and exact helper windows;
// it is not a PS1 emulator or renderer/runtime acceptance.
internal sealed class RenderProbeCpu
{
    private readonly byte[] _ram=new byte[0x200400];
    internal uint[] Regs { get; }=new uint[32];
    internal uint Hi,Lo;
    // Off by default to preserve all prior bounded probes. The pickup tests
    // opt in because the source XY leaf uses trapping ADD/SUB, not ADDU/SUBU.
    internal bool StrictSignedArithmetic { get; set; }
    internal List<uint> Reads { get; }=[];
    internal List<uint> Writes { get; }=[];
    internal List<uint> Calls { get; }=[];
    internal HashSet<uint> Visited { get; }=[];
    private static int At(uint a,int n)
    {if((ulong)a+(uint)n<=0x200000)return (int)a; // Native null-list branch delay speculatively reads physical RAM+40.
     if(a>=0x1F800000 && (ulong)a+(uint)n<=0x1F800400)return (int)(a-0x1F800000)+0x200000;if(a<0x80000000||(ulong)a+(uint)n>0x80200000)throw new Exception("Unowned test address "+a.ToString("X8"));return (int)(a-0x80000000);}
    internal void Copy(uint a,byte[] bytes)=>bytes.CopyTo(_ram,At(a,bytes.Length));
    internal uint Get(uint a,int n=4)=>n switch{1=>_ram[At(a,1)],2=>BinaryPrimitives.ReadUInt16LittleEndian(_ram.AsSpan(At(a,2))),_=>BinaryPrimitives.ReadUInt32LittleEndian(_ram.AsSpan(At(a,4)))};
    internal void Put(uint a,uint v,int n=4)
    {int at=At(a,n);if(n==1)_ram[at]=(byte)v;else if(n==2)BinaryPrimitives.WriteUInt16LittleEndian(_ram.AsSpan(at),(ushort)v);else BinaryPrimitives.WriteUInt32LittleEndian(_ram.AsSpan(at),v);}
    internal (int Reg,uint Value)? BoundaryLoad { get; private set; }
    internal void Run(uint start,uint stop,bool allowPendingLoad=false)
    {
        uint pc=start;uint? pendingJump=null;(int Reg,uint Value)? load=null;
        for(int steps=0;steps<200000;steps++)
        {
            if(pc==stop){if(pendingJump!=null||load!=null&&!allowPendingLoad)throw new Exception("Unretired test delay at "+pc.ToString("X8"));BoundaryLoad=load;return;}
            Visited.Add(pc);if(pc is 0x80016C58 or 0x8006272C or 0x80038EE0)Calls.Add(pc);
            uint w=Get(pc),op=w>>26;int rs=(int)(w>>21)&31,rt=(int)(w>>16)&31,rd=(int)(w>>11)&31,shift=(int)(w>>6)&31;
            int imm=(short)w;uint a=Regs[rs],b=Regs[rt],next=pc+4;var oldLoad=load;load=null;var oldJump=pendingJump;pendingJump=null;
            (int Reg,uint Value)? write=null;
            void Set(int r,uint v)=>write=(r,v);
            void Load(int r,uint v){if(r!=0)load=(r,v);}
            void Branch(bool take){pendingJump=take?unchecked(pc+4+(uint)(imm*4)):pc+8;}
            uint Signed(uint left,uint right,bool subtract)
            {
                long result=subtract?(long)(int)left-(int)right:(long)(int)left+(int)right;
                if(StrictSignedArithmetic&&(result<int.MinValue||result>int.MaxValue))
                {
                    // The preceding load retires; this trapping instruction
                    // does not write its destination or execute a later word.
                    if(oldLoad is {} prior)Regs[prior.Reg]=prior.Value;
                    Regs[0]=0;throw new RenderProbeArithmeticOverflow(pc,oldJump!=null);
                }
                return unchecked((uint)result);
            }
            switch(op)
            {
                case 0:switch(w&63)
                {
                    case 0:Set(rd,b<<shift);break;case 2:Set(rd,b>>shift);break;case 3:Set(rd,unchecked((uint)((int)b>>shift)));break;
                    case 4:Set(rd,b<<(int)(a&31));break;case 6:Set(rd,b>>(int)(a&31));break;case 7:Set(rd,unchecked((uint)((int)b>>(int)(a&31))));break;
                    case 8:pendingJump=a;break;case 16:Set(rd,Hi);break;case 18:Set(rd,Lo);break;case 17:Hi=a;break;case 19:Lo=a;break;
                    case 24:long product=(long)(int)a*(int)b;Lo=(uint)product;Hi=(uint)(product>>32);break;
                    case 25:ulong unsignedProduct=(ulong)a*b;Lo=(uint)unsignedProduct;Hi=(uint)(unsignedProduct>>32);break;
                    case 32:Set(rd,Signed(a,b,false));break;case 33:Set(rd,unchecked(a+b));break;
                    case 34:Set(rd,Signed(a,b,true));break;case 35:Set(rd,unchecked(a-b));break;
                    case 36:Set(rd,a&b);break;case 37:Set(rd,a|b);break;case 38:Set(rd,a^b);break;
                    case 42:Set(rd,(int)a<(int)b?1u:0);break;case 43:Set(rd,a<b?1u:0);break;
                    default:throw new Exception("Unknown SPECIAL "+w.ToString("X8"));
                }break;
                case 1:if(rt is not 0 and not 1)throw new Exception("Unsupported REGIMM");Branch(rt==0?(int)a<0:(int)a>=0);break;
                case 2:case 3:pendingJump=((pc+4)&0xF0000000)|((w&0x3FFFFFF)<<2);if(op==3){Set(31,pc+8);Calls.Add(pendingJump.Value);}break;
                case 4:Branch(a==b);break;case 5:Branch(a!=b);break;case 6:Branch((int)a<=0);break;case 7:Branch((int)a>0);break;
                case 8:Set(rt,Signed(a,unchecked((uint)imm),false));break;case 9:Set(rt,unchecked(a+(uint)imm));break;case 10:Set(rt,(int)a<imm?1u:0);break;
                case 11:Set(rt,a<unchecked((uint)imm)?1u:0);break;case 12:Set(rt,a&(w&65535));break;case 13:Set(rt,a|(w&65535));break;case 15:Set(rt,w<<16);break;
                case 0x20:case 0x21:case 0x23:case 0x24:case 0x25:
                    int size=op is 0x20 or 0x24?1:op is 0x21 or 0x25?2:4;uint readAddress=unchecked(a+(uint)imm);Reads.Add(readAddress);uint value=Get(readAddress,size);
                    if(op==0x20)value=unchecked((uint)(sbyte)value);if(op==0x21)value=unchecked((uint)(short)value);Load(rt,value);break;
                case 0x22:case 0x26:
                {
                    uint effective=unchecked(a+(uint)imm),aligned=effective&~3u;int lane=(int)(effective&3);
                    Reads.Add(aligned);uint memory=Get(aligned);
                    // Merge operands alone forward a matching pending load.
                    uint prior=oldLoad is {} pending&&pending.Reg==rt?pending.Value:b;
                    int bits=(op==0x22?3-lane:lane)*8;
                    uint mask=op==0x22?uint.MaxValue<<bits:uint.MaxValue>>bits;
                    Load(rt,(prior&~mask)|(op==0x22?memory<<bits:memory>>bits));break;
                }
                case 0x28:case 0x29:case 0x2B:
                    int width=op==0x28?1:op==0x29?2:4;uint address=unchecked(a+(uint)imm);Put(address,b,width);
                    for(int i=0;i<width;i++)Writes.Add(address+(uint)i);break;
                case 0x2A:case 0x2E:
                {
                    uint effective=unchecked(a+(uint)imm),aligned=effective&~3u;int lane=(int)(effective&3);
                    _=At(aligned,4); // Validate complete owner without inventing a semantic read.
                    uint selected=op==0x2A?b>>((3-lane)*8):b<<(lane*8);
                    int first=op==0x2A?0:lane,last=op==0x2A?lane:3;
                    for(int i=first;i<=last;i++){Put(aligned+(uint)i,selected>>(i*8),1);Writes.Add(aligned+(uint)i);}break;
                }
                default:throw new Exception("Unknown test opcode "+w.ToString("X8")+" at "+pc.ToString("X8"));
            }
            // A second delayed load to the same register cancels the first
            // retirement (ordinary loads too); its own one-slot delay remains.
            if(oldLoad is {} l&&load?.Reg!=l.Reg)Regs[l.Reg]=l.Value;if(write is {} r)Regs[r.Reg]=r.Value;Regs[0]=0;
            if(oldJump is uint destination){if(pendingJump!=null)throw new Exception("Transfer in delay slot");next=destination;}
            pc=next;
        }
        throw new Exception("Test CPU budget exceeded.");
    }
}

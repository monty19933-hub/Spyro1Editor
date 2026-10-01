using System.Buffers.Binary;

// Independent delayed R3000 subset. Data loads commit after the next instruction;
// branch targets take effect only after executing their delay slot.
internal sealed class ScalarMips
{
    internal readonly uint[] R = new uint[32];
    internal readonly Dictionary<uint,uint> Memory = [];
    internal readonly List<(uint Address,uint Value)> Writes = [];
    internal readonly List<(uint Address,uint Value)> Reads = [];
    internal readonly List<uint> Calls = [];
    private readonly List<(uint Address,byte[] Bytes)> segments = [];
    private uint hi,lo;
    internal uint Pc { get; private set; }
    internal uint? PendingLoad { get; private set; }
    private int pendingRegister;
    private uint? pendingJump;
    internal ScalarMips()
    {
        for(int r=1;r<32;r++)R[r]=0xB0000000+(uint)r*0x10101;
        R[29]=0x801FFE00;R[31]=0x90000000;
    }
    internal void Segment(uint address,byte[] bytes)=>segments.Add((address,bytes));
    internal void Product(int first,int second){long product=(long)first*second;hi=(uint)((ulong)product>>32);lo=(uint)product;}
    internal uint Word(uint address)
    {
        if(Memory.TryGetValue(address,out uint value))return value;
        foreach(var segment in segments)
            if(address>=segment.Address&&(long)address+4<=segment.Address+segment.Bytes.LongLength)
                return BinaryPrimitives.ReadUInt32LittleEndian(segment.Bytes.AsSpan((int)(address-segment.Address)));
        throw new InvalidDataException($"Scalar read {address:X8} has no fixture owner.");
    }
    internal void Run(uint start,uint stop,int budget=256)
    {
        Pc=start;
        while(Pc!=stop)
        {
            if(--budget<0)throw new InvalidOperationException($"Scalar budget exhausted at {Pc:X8}.");
            uint pc=Pc,w=Word(pc),value=0;int op=(int)(w>>26),rs=(int)(w>>21)&31,rt=(int)(w>>16)&31,rd=(int)(w>>11)&31;
            int shift=(int)(w>>6)&31,fn=(int)w&63,imm=(short)w;
            bool write=false,load=false;uint? jump=null;
            switch(op)
            {
                case 0:
                    switch(fn)
                    {
                        case 0:value=R[rt]<<shift;rt=rd;write=w!=0;break;
                        case 3:value=unchecked((uint)((int)R[rt]>>shift));rt=rd;write=true;break;
                        case 8:jump=R[rs];break;
                        case 16:value=hi;rt=rd;write=true;break;
                        case 18:value=lo;rt=rd;write=true;break;
                        case 24:long product=(long)(int)R[rs]*(int)R[rt];hi=(uint)((ulong)product>>32);lo=(uint)product;break;
                        case 33:value=unchecked(R[rs]+R[rt]);rt=rd;write=true;break;
                        case 35:value=unchecked(R[rs]-R[rt]);rt=rd;write=true;break;
                        default:throw new InvalidDataException($"Unsupported scalar R function {fn} at {pc:X8}.");
                    }
                    break;
                case 2:jump=((pc+4)&0xF0000000)|((w&0x03FFFFFF)<<2);break;
                case 3:jump=((pc+4)&0xF0000000)|((w&0x03FFFFFF)<<2);rt=31;value=pc+8;write=true;Calls.Add(jump.Value);break;
                case 4:if(R[rs]==R[rt])jump=unchecked(pc+4+(uint)(imm*4));break;
                case 5:if(R[rs]!=R[rt])jump=unchecked(pc+4+(uint)(imm*4));break;
                case 9:value=unchecked(R[rs]+(uint)imm);write=true;break;
                case 11:value=R[rs]<unchecked((uint)imm)?1u:0;write=true;break;
                case 13:value=R[rs]|(w&0xFFFF);write=true;break;
                case 15:value=(w&0xFFFF)<<16;write=true;break;
                case 33:
                    uint halfAddress=unchecked(R[rs]+(uint)imm),packed=Word(halfAddress&~3u);
                    value=unchecked((uint)(short)(packed>>((int)(halfAddress&2)*8)));load=true;Reads.Add((halfAddress,value));break;
                case 35:uint address=unchecked(R[rs]+(uint)imm);value=Word(address);load=true;Reads.Add((address,value));break;
                case 43:uint destination=unchecked(R[rs]+(uint)imm);Memory[destination]=R[rt];Writes.Add((destination,R[rt]));break;
                default:throw new InvalidDataException($"Unsupported scalar opcode {op} at {pc:X8}.");
            }
            if(PendingLoad is uint pending)R[pendingRegister]=pending;
            PendingLoad=null;
            if(load){PendingLoad=value;pendingRegister=rt;}else if(write&&rt!=0)R[rt]=value;
            uint next=pendingJump??pc+4;pendingJump=jump;Pc=next;R[0]=0;
        }
    }
}

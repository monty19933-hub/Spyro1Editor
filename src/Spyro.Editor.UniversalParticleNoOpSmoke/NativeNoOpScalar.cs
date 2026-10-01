using Spyro.Editor.Core.Universal;

// Independent bounded MIPS-I execution of the actual native particle callback.
// Only its own stack/overlay reads and stack writes are admitted. Engine calls,
// allocation/global writes and unresolved instructions are negative controls.
internal static class NativeNoOpScalar
{
    internal static uint[] Execute(UniversalOverlay owner, uint argument3, uint type=64)
    {
        uint[] r=Enumerable.Range(0,32).Select(i=>(uint)(0x123000+i*16)).ToArray();r[0]=0;r[4]=8;r[5]=type;r[6]=0x123456;r[7]=argument3;r[29]=0x10000000;r[31]=0x70000000;
        Dictionary<uint,uint> stack=[];uint pc=owner.Bindings[UniversalExeBindings.ParticleSpawnGlobal].Value;
        (int Reg,uint Value)? pending=null;
        uint Load(uint address)
        {
            if(address>=0x0FFFF000&&address<0x10001000)return stack.GetValueOrDefault(address);
            if((address&3)==0&&owner.Contains(address))return owner.Word(address);
            throw new InvalidDataException("Native no-op read left its owned overlay/stack.");
        }
        uint? One(uint w,uint at,bool delay=false)
        {
            int op=(int)(w>>26),rs=(int)(w>>21&31),rt=(int)(w>>16&31),rd=(int)(w>>11&31),fn=(int)(w&63),sh=(int)(w>>6&31),imm=(short)(w&65535);
            int written=-1;(int Reg,uint Value)? loaded=null;uint? target=null;
            void Set(int reg,uint v){if(reg==0)return;r[reg]=v;written=reg;}
            switch(op)
            {
                case 0:
                    switch(fn)
                    {
                        case 0:Set(rd,r[rt]<<sh);break;
                        case 2:Set(rd,r[rt]>>sh);break;
                        case 3:Set(rd,unchecked((uint)((int)r[rt]>>sh)));break;
                        case 8:target=r[rs];break;
                        case 0x21:Set(rd,unchecked(r[rs]+r[rt]));break;
                        case 0x23:Set(rd,unchecked(r[rs]-r[rt]));break;
                        case 0x24:Set(rd,r[rs]&r[rt]);break;
                        case 0x25:Set(rd,r[rs]|r[rt]);break;
                        case 0x2A:Set(rd,(int)r[rs]<(int)r[rt]?1u:0);break;
                        case 0x2B:Set(rd,r[rs]<r[rt]?1u:0);break;
                        default:throw new InvalidDataException("Native no-op encountered unsupported R instruction/call.");
                    }break;
                case 2:target=((at+4)&0xF0000000)|((w&0x3FFFFFF)<<2);break;
                case 4:target=r[rs]==r[rt]?unchecked((uint)((long)at+4+imm*4)):at+8;break;
                case 5:target=r[rs]!=r[rt]?unchecked((uint)((long)at+4+imm*4)):at+8;break;
                case 6:target=(int)r[rs]<=0?unchecked((uint)((long)at+4+imm*4)):at+8;break;
                case 7:target=(int)r[rs]>0?unchecked((uint)((long)at+4+imm*4)):at+8;break;
                case 9:Set(rt,unchecked(r[rs]+(uint)imm));break;
                case 10:Set(rt,(int)r[rs]<imm?1u:0);break;
                case 11:Set(rt,r[rs]<unchecked((uint)imm)?1u:0);break;
                case 12:Set(rt,r[rs]&(w&65535));break;
                case 13:Set(rt,r[rs]|(w&65535));break;
                case 15:Set(rt,(w&65535)<<16);break;
                case 35:loaded=(rt,Load(unchecked(r[rs]+(uint)imm)));break;
                case 43:
                    uint address=unchecked(r[rs]+(uint)imm);
                    if(address<0x0FFFF000||address>=0x10001000)throw new InvalidDataException("Native no-op wrote outside its stack.");
                    stack[address]=r[rt];break;
                default:throw new InvalidDataException("Native no-op encountered an engine call/unsupported opcode.");
            }
            if(delay&&target!=null)throw new InvalidDataException("Native no-op branch has a control delay slot.");
            if(pending is{} previous&&previous.Reg!=0&&previous.Reg!=written)r[previous.Reg]=previous.Value;
            pending=loaded;r[0]=0;return target;
        }
        for(int steps=0;steps<4096;steps++)
        {
            if(!owner.Contains(pc))throw new InvalidDataException("Native no-op control left its callback.");
            uint? target=One(owner.Word(pc),pc);
            if(target==null){pc+=4;continue;}
            One(owner.Word(pc+4),pc+4,true);
            if(target==0x70000000)return r;
            pc=target.Value;
        }
        throw new InvalidDataException("Native no-op failed to return within its bounded native loop.");
    }
}

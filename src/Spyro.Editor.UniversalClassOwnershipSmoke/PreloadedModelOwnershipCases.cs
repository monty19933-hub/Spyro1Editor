using System.Buffers.Binary;
using Spyro.Editor.Core.Universal;

internal static class PreloadedModelOwnershipCases
{
    private static uint I(int op, int rs, int rt, int imm) => UniversalMips.EncodeI(op, rs, rt, imm);
    private static uint R(int rs, int rt, int rd, int shift, int fn) => (uint)((rs << 21) | (rt << 16) | (rd << 11) | (shift << 6) | fn);
    private static readonly uint[] Words = [I(15,0,9,0x8007), I(9,9,9,0x6378), I(33,19,2,0x36),
        I(15,0,4,0x8007), I(35,4,4,0x61D0), R(0,2,2,2,0), R(2,9,2,0,33), I(35,2,2,0), UniversalMips.JrRa, 0];
    private static UniversalOverlay Image(uint[] words, Action<byte[]>? mutate = null)
    {
        byte[] bytes = new byte[0x400];
        for (int n=0;n<words.Length;n++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x100+n*4),words[n]);
        mutate?.Invoke(bytes);
        var bindings = new Dictionary<uint,UniversalExeBinding>();
        foreach (uint global in new[]{UniversalExeBindings.InitGlobal,UniversalExeBindings.UpdateGlobal,UniversalExeBindings.CopyBufferGlobal})
            bindings[global] = new(global,0,0,UniversalOverlay.Base+0x100);
        return new(bytes,bindings);
    }
    public static void Free(Action<bool,string> check)
    {
        uint pc=UniversalOverlay.Base+0x108;
        bool Proved(uint[] words, Action<byte[]>? mutate=null)=>UniversalClassRemapping.FeedsModelTable(Image(words,mutate),pc);
        check(Proved(Words),"adjacent source literal before class read owns exact model-table lookup");
        foreach(var (name,at,word) in new (string,int,uint)[]{
            ("wrong high half",0,I(15,0,9,0x8008)),("wrong low half",1,I(9,9,9,0x637C)),
            ("different low-half input",1,I(9,8,9,0x6378)),("class register overwritten by base",0,I(15,0,2,0x8007)),
            ("prefix call",0,UniversalMips.EncodeJ(3,0x800529E4)),("mutable base",0,I(35,4,9,0)),
            ("base clobber",3,I(9,0,9,0)),("class consumed in load delay",3,R(0,2,2,2,0)),
            ("class passed to engine",3,I(9,2,4,0)),("root does not overwrite class",7,I(35,2,3,0))})
        {var words=Words.ToArray();words[at]=word;check(!Proved(words),"preloaded base refuses "+name);}
        foreach(uint entry in new[]{pc-4,pc})
        {
            check(!Proved(Words,b=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(0x200),UniversalMips.EncodeJ(2,entry))),"direct edge cannot skip literal pair "+entry.ToString("X8"));
            check(!Proved(Words,b=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(0x40),entry)),"dispatch table cannot skip literal pair "+entry.ToString("X8"));
            check(!Proved(Words,b=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(0x200),I(4,4,0,(int)((entry-(UniversalOverlay.Base+0x204))/4)))),"conditional edge cannot skip literal pair "+entry.ToString("X8"));
        }
        check(Proved(Words,b=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(0x40),pc-8)),"entry at the complete literal pair is allowed");
        check(!Proved(Words,b=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(0xFC),UniversalMips.EncodeJ(3,0x800529E4))),"LUI in a call delay slot cannot establish a unique producer");
        check(!Proved(Words,b=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(0x200),R(8,0,0,0,8))),"unresolved computed entry is refused");
        var cached=Image(Words);byte[] mutable=cached.Bytes.ToArray();var mutableImage=new UniversalOverlay(mutable,cached.Bindings);
        check(UniversalClassRemapping.FeedsModelTable(mutableImage,pc),"unchanged source establishes a cached entry proof");
        BinaryPrimitives.WriteUInt32LittleEndian(mutable.AsSpan(0x40),pc);
        check(!UniversalClassRemapping.FeedsModelTable(mutableImage,pc),"changed source bytes cannot reuse an earlier entry proof");
        for (uint cls=0;cls<0x200;cls++)
        {
            // Independent R3000 scalar: load values become visible after the next
            // instruction. All table entries must retain the destination class ID.
            uint[] registers=new uint[32]; uint? pending=null; int pendingRegister=0;
            uint expectedRoot=0x80100000+cls*8;
            foreach(uint word in Words.Take(8))
            {
                int op=UniversalMips.Op(word), rs=UniversalMips.Rs(word), rt=UniversalMips.Rt(word);
                uint value; bool load=false;
                switch(op)
                {
                    case 15:value=(uint)UniversalMips.Imm(word)<<16;break;
                    case 9:value=unchecked(registers[rs]+(uint)UniversalMips.SImm(word));break;
                    case 33:value=cls;load=true;break;
                    case 0:
                        value=UniversalMips.Fn(word)==0?registers[rt]<<UniversalMips.Sh(word):unchecked(registers[rs]+registers[rt]);
                        rt=UniversalMips.Rd(word);break;
                    case 35:
                        uint address=unchecked(registers[rs]+(uint)UniversalMips.SImm(word));
                        if(rs==4){check(address==0x800761D0,"unrelated delayed global source "+cls);value=0x12345678;}
                        else {check(address==0x80076378+cls*4,"preloaded exact destination table index "+cls);value=expectedRoot;}
                        load=true;break;
                    default:throw new InvalidOperationException("unexpected preloaded scalar instruction");
                }
                if(pending is uint delayed)registers[pendingRegister]=delayed;
                pending=null;
                if(load){pending=value;pendingRegister=rt;}else if(rt!=0)registers[rt]=value;
            }
            check(pending==expectedRoot,"preloaded exact model pointer "+cls);
        }
    }
    public static void Source(UniversalSourceLibrary library, Action<bool,string> check)
    {
        foreach(var (key,pc) in new(string,uint)[]{("treetops",0x800852F0),("drycanyon",0x800892D4),("loftycastle",0x80083650),
            ("highcaves",0x80087D14),("beastmakers",0x80085CE8),("terracevillage",0x800815D0),("hauntedtowers",0x80084524),("wizardpeak",0x80085BCC)})
        {
            var source=library.Get(key).Overlay;
            check(UniversalClassRemapping.FeedsModelTable(source,pc),"exact native adjacent preloaded model-table owner "+key);
            foreach(bool incoming in new[]{false,true})
            {
                byte[] bytes=source.Bytes.ToArray();
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(incoming?0:(int)(pc-4-UniversalOverlay.Base)),
                    incoming?pc:I(9,UniversalMips.Rt(source.Word(pc-4)),UniversalMips.Rt(source.Word(pc-4)),0x637C));
                check(!UniversalClassRemapping.FeedsModelTable(new(bytes,source.Bindings),pc),"source preloaded owner mutation refused "+key+(incoming?" entry":" table"));
            }
        }
        foreach(var (key,pc) in new(string,uint)[]{("alpineridge",0x80088F14),("mistybog",0x8008167C),("magiccrafters",0x80089BDC),("townsquare",0x80085ADC),("gnastysloot",0x800822CC)})
            check(!UniversalClassRemapping.FeedsModelTable(library.Get(key).Overlay,pc),"broader native preloaded ownership remains refused "+key);
    }
}

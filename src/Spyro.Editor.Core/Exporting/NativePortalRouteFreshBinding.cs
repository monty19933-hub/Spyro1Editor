using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>A compact, source-owned Town Square initializer keeps its nine retail
/// callbacks while current ID65 selects only the three authored callback overrides.</summary>
public static class NativePortalRouteFreshBinding
{
    public const uint Address=0x8005A898;
    public const int Length=152;
    public const uint CommonTail=0x8005B6E0;
    public const string SourceSha256="fe4669229aedbbc8ff698d57a237327af36025aaeda0ff5cc2e1c806fd23e50b";
    private const uint GlobalBase=0x80078000;
    private static readonly (uint Address,int Length,string Sha256)[] CallerOwners=
    [
        (0x8005A470,48,"7dba88624efe091d8b34a22b8874a2d12ab577858f7583a324d82f11d14d5c4f"),
        (0x800155F4,192,"345b607a394bb1c3a7870d1099278196dc524c2f6334667089aaf472044854c1")
    ];
    private static readonly (uint Global,uint Value)[] Retail =
    [
        (0x800785D8,0x80089ECC),(0x800758CC,0x800872A4),(0x80075734,0x8007DA54),
        (0x800758E4,0x80088F68),(0x800756BC,0x80087E20),(0x800757A0,0x8007AF2C),
        (0x800758A8,0x8007B068),(0x8007574C,0x8007B0AC),(0x800758D8,0x8007D04C)
    ];
    private static readonly uint[] Overridden=[UniversalExeBindings.CopyBufferGlobal,UniversalExeBindings.InitGlobal,UniversalExeBindings.UpdateGlobal];
    private static bool Pointer(uint value)=>value>=UniversalOverlay.Base&&value<0x80200000&&(value&3)==0;
    public const int OrdinaryPortalHeightOffset=0x5F640;

    /// <summary>Ordinary portal arrival uses an indexed height in addition to the scene
    /// spawn. The unused65 slot retains a flight height; give only that slot Town's
    /// normal arrival height. The separate balloon/state12 classifier remains intact.</summary>
    public static void PatchOrdinaryPortalArrival(byte[] originalExecutable,byte[] stagedExecutable)
    {
        if(originalExecutable.Length!=0x66000||stagedExecutable.Length!=originalExecutable.Length||
            Convert.ToHexStringLower(SHA256.HashData(originalExecutable))!="a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998")
            throw new InvalidDataException("Fresh portal arrival requires the complete original USA executable.");
        foreach(var (offset,length,digest) in new (int,int,string)[]{
            (0x419C,168,"7ebc26c894040d85ff4ed3143a41c78f85bbce7e6ee9610a2b7894ce51918d43"),
            (0x5DFC,104,"aa329be57b96475b5c97242a7220b7b25fbf70b057bb97dae183651f949046c3"),
            (0x5F5B4,144,"4726dfc8e49f12b07a5a39cd0209a3d155b66a5c9566f4d198ab0de0bcda14f4")})
        {
            var source=originalExecutable.AsSpan(offset,length);
            if(Convert.ToHexStringLower(SHA256.HashData(source))!=digest)
                throw new InvalidDataException("Fresh portal arrival source owner changed.");
            if(offset==0x5F5B4)continue;
            if(!stagedExecutable.AsSpan(offset,length).SequenceEqual(source))
                throw new InvalidDataException("Fresh portal arrival consumer or level index changed.");
        }
        byte[] expected=originalExecutable.AsSpan(0x5F5B4,144).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(35*4),8192);
        var table=stagedExecutable.AsSpan(0x5F5B4,144);
        if(!table.SequenceEqual(originalExecutable.AsSpan(0x5F5B4,144))&&!table.SequenceEqual(expected))
            throw new InvalidDataException("Fresh portal arrival conflicts with another level's entry height.");
        // The normal classifier must already be issued by the fresh bootstrap;
        // authenticate its complete local owner while retaining the state12 bypass.
        byte[] classifier=originalExecutable.AsSpan(0x409C,100).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(classifier.AsSpan(0x40C0-0x409C),0x3C025E00);
        if(!stagedExecutable.AsSpan(0x409C,100).SequenceEqual(classifier)||
            UniversalExeBindings.Word(stagedExecutable,UniversalExeBindings.LevelInitJumpTable+65*4)!=Address)
            throw new InvalidDataException("Fresh portal arrival requires its canonical normal65 bootstrap.");
        BinaryPrimitives.WriteInt32LittleEndian(stagedExecutable.AsSpan(OrdinaryPortalHeightOffset),8192);
    }
    private static byte[] Build(uint copy,uint init,uint update,uint townCopy=0x80089ECC,uint townUpdate=0x8007DA54)
    {
        if(!Pointer(copy)||!Pointer(init)||!Pointer(update)||!Pointer(townCopy)||!Pointer(townUpdate))throw new InvalidDataException("Fresh callbacks must remain aligned resident PS1 addresses.");
        var p=new UniversalAsmPart();void I(int op,int rs,int rt,int imm)=>p.Emit(UniversalMips.EncodeI(op,rs,rt,imm));
        I(15,0,1,0x8008);I(9,1,1,-32768);I(15,0,3,0x8008);
        foreach(var (global,retailValue) in Retail)
        {
            uint value=global==UniversalExeBindings.CopyBufferGlobal?townCopy:global==UniversalExeBindings.UpdateGlobal?townUpdate:retailValue;
            long delta=(long)value-0x80080000;
            if(delta>=short.MinValue&&delta<=short.MaxValue)I(9,3,2,(int)delta);
            else if((value>>16)==0x8008)I(13,3,2,(int)(value&0xFFFF));
            else if(global is UniversalExeBindings.CopyBufferGlobal or UniversalExeBindings.UpdateGlobal)
            {I(15,0,2,UniversalMips.Hi(value));I(9,2,2,UniversalMips.Lo(value));}
            else throw new InvalidDataException("Retail initializer callback escaped its proved literal base.");
            I(43,1,2,checked((short)((long)global-GlobalBase)));
        }
        I(35,1,2,checked((short)((long)0x8007596C-GlobalBase)));I(9,0,3,65);
        p.Emit(UniversalMips.EncodeI(5,2,3,0),new(UniversalFixKind.BranchLabel,Label:"tail"));p.Emit(0);
        foreach(var (global,value) in Overridden.Zip(new[]{copy,init,update}))
        {I(15,0,2,UniversalMips.Hi(value));I(9,2,2,UniversalMips.Lo(value));I(43,1,2,checked((short)((long)global-GlobalBase)));}
        p.Label("tail");p.Emit(UniversalMips.EncodeJ(2,CommonTail));p.Emit(0);
        byte[] code=UniversalBehaviorBundleBuilder.Assemble([p],Address).Code;
        if(code.Length>Length)throw new InvalidDataException("Fresh split initializer exceeded the original Town Square owner.");
        byte[] result=new byte[Length];code.CopyTo(result,0);return result;
    }

    private static void RequireOwners(byte[] originalExecutable,byte[] stagedExecutable)
    {
        ArgumentNullException.ThrowIfNull(originalExecutable);ArgumentNullException.ThrowIfNull(stagedExecutable);
        if(originalExecutable.Length!=0x66000||stagedExecutable.Length!=originalExecutable.Length)
            throw new InvalidDataException("Fresh callback splitting requires the complete USA executable.");
        ReadOnlySpan<byte> Owner(byte[] image,uint address,int length)=>image.AsSpan(checked((int)(address-UniversalExeBindings.ExeBase)),length);
        if(Convert.ToHexStringLower(SHA256.HashData(Owner(originalExecutable,Address,Length)))!=SourceSha256||
            Convert.ToHexStringLower(SHA256.HashData(Owner(originalExecutable,CommonTail,24)))!="19566edf84d04523d6cc943feb5107817297413669d3838b10ef868db744151d"||
            UniversalExeBindings.Word(originalExecutable,UniversalExeBindings.LevelInitJumpTable+13*4)!=Address||
            UniversalExeBindings.Word(originalExecutable,UniversalExeBindings.LevelInitJumpTable+65*4)!=CommonTail)
            throw new InvalidDataException("The retail Town Square/fresh initializer source owner changed.");
        if(!Owner(stagedExecutable,CommonTail,24).SequenceEqual(Owner(originalExecutable,CommonTail,24))||
            UniversalExeBindings.Word(stagedExecutable,UniversalExeBindings.LevelInitJumpTable+13*4)!=Address||
            UniversalExeBindings.Word(stagedExecutable,UniversalExeBindings.LevelInitJumpTable+65*4)!=Address)
            throw new InvalidDataException("Fresh callback splitting requires the captured ID65 Town Square bootstrap alias.");
        foreach(var caller in CallerOwners)
        {
            var originalCaller=Owner(originalExecutable,caller.Address,caller.Length);
            if(Convert.ToHexStringLower(SHA256.HashData(originalCaller))!=caller.Sha256||!Owner(stagedExecutable,caller.Address,caller.Length).SequenceEqual(originalCaller))
                throw new InvalidDataException("The fresh initializer dispatcher/caller volatile-register ownership changed.");
        }
    }

    public static void Rebind(byte[] originalExecutable,byte[] stagedExecutable,uint newUpdate,uint newCopyBuffer)
    {
        RequireOwners(originalExecutable,stagedExecutable);
        ReadOnlySpan<byte> Owner(byte[] image,uint address,int length)=>image.AsSpan(checked((int)(address-UniversalExeBindings.ExeBase)),length);
        var current=UniversalExeBindings.Read(stagedExecutable,65);
        if(current.Count!=Retail.Length||Retail.Where(p=>!Overridden.Contains(p.Global)).Any(p=>!current.TryGetValue(p.Global,out var value)||value.Value!=p.Value))
            throw new InvalidDataException("Fresh callback splitting supports only the proved copy/init/update overrides.");
        bool compact=TryRead(stagedExecutable,65,out _);
        if(!compact)
        {
            // The legacy initializer may differ only in these three standalone pointer pairs.
            byte[] expected=Owner(originalExecutable,Address,Length).ToArray();var source=UniversalExeBindings.Read(originalExecutable,13);
            foreach(uint global in Overridden)
            {
                var binding=source[global];uint value=current[global].Value;
                if(!Pointer(value))throw new InvalidDataException("The captured fresh initializer contains an invalid resident callback.");
                int hi=(int)(binding.LuiAddress-Address),lo=(int)(binding.AddiuAddress-Address);
                BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(hi),UniversalMips.WithImm(BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(hi)),UniversalMips.Hi(value)));
                BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(lo),UniversalMips.WithImm(BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(lo)),UniversalMips.Lo(value)));
            }
            if(!Owner(stagedExecutable,Address,Length).SequenceEqual(expected))throw new InvalidDataException("The staged legacy Town Square initializer has an unowned change.");
        }
        var town=compact?UniversalExeBindings.Read(stagedExecutable,13):UniversalExeBindings.Read(originalExecutable,13);
        byte[] code=Build(newCopyBuffer,current[UniversalExeBindings.InitGlobal].Value,newUpdate,town[UniversalExeBindings.CopyBufferGlobal].Value,town[UniversalExeBindings.UpdateGlobal].Value);
        code.CopyTo(stagedExecutable.AsSpan(checked((int)(Address-UniversalExeBindings.ExeBase)),Length));
    }

    /// <summary>Change Town Square's independent copy/update route after installing the ID65 split.</summary>
    public static void RebindTown(byte[] originalExecutable,byte[] stagedExecutable,uint newUpdate,uint newCopyBuffer)
    {
        RequireOwners(originalExecutable,stagedExecutable);
        if(!TryRead(stagedExecutable,65,out var fresh))throw new InvalidDataException("Town routing requires the complete canonical ID65 split initializer.");
        byte[] code=Build(fresh[UniversalExeBindings.CopyBufferGlobal].Value,fresh[UniversalExeBindings.InitGlobal].Value,
            fresh[UniversalExeBindings.UpdateGlobal].Value,newCopyBuffer,newUpdate);
        code.CopyTo(stagedExecutable.AsSpan(checked((int)(Address-UniversalExeBindings.ExeBase)),Length));
    }

    /// <summary>Recognize the complete canonical compact owner before exposing virtual
    /// per-level bindings. No shared literal instruction may be patched through these bindings.</summary>
    public static bool TryRead(ReadOnlySpan<byte> executable,int levelId,out IReadOnlyDictionary<uint,UniversalExeBinding> bindings)
    {
        bindings=new Dictionary<uint,UniversalExeBinding>();
        if(executable.Length!=0x66000||UniversalExeBindings.Word(executable,UniversalExeBindings.LevelInitJumpTable+(uint)levelId*4)!=Address)return false;
        int offset=checked((int)(Address-UniversalExeBindings.ExeBase));var owner=executable.Slice(offset,Length);
        uint first=BinaryPrimitives.ReadUInt32LittleEndian(owner);
        if(UniversalMips.Op(first)==15&&UniversalMips.Rt(first)==2)return false; // legacy source pointer pair
        static uint WordAt(ReadOnlySpan<byte> bytes,int at)
        {
            if(at<0||at+4>bytes.Length)throw new InvalidDataException("Compact initializer decode escaped its owner.");
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(at,4));
        }
        static uint PointerAt(ReadOnlySpan<byte> bytes,int at)=>unchecked((uint)((UniversalMips.Imm(WordAt(bytes,at))<<16)+UniversalMips.SImm(WordAt(bytes,at+4))));
        int cursor=12;var values=new Dictionary<uint,uint>();
        foreach(var (global,_) in Retail)
        {
            uint materializer=WordAt(owner,cursor),value;
            if(UniversalMips.Op(materializer)==15&&UniversalMips.Rt(materializer)==2){value=PointerAt(owner,cursor);cursor+=8;}
            else if(UniversalMips.Op(materializer)==9&&UniversalMips.Rs(materializer)==3&&UniversalMips.Rt(materializer)==2){value=unchecked((uint)(0x80080000L+UniversalMips.SImm(materializer)));cursor+=4;}
            else if(UniversalMips.Op(materializer)==13&&UniversalMips.Rs(materializer)==3&&UniversalMips.Rt(materializer)==2){value=0x80080000u|(uint)UniversalMips.Imm(materializer);cursor+=4;}
            else throw new InvalidDataException("Compact initializer callback materializer changed.");
            uint store=WordAt(owner,cursor);cursor+=4;
            if(store!=UniversalMips.EncodeI(43,1,2,checked((short)((long)global-GlobalBase))))throw new InvalidDataException("Compact initializer callback store changed.");
            values.Add(global,value);
        }
        cursor+=16; // current-level load, delayed selector and branch slot
        uint copy=PointerAt(owner,cursor),init=PointerAt(owner,cursor+12),update=PointerAt(owner,cursor+24);
        if(!owner.SequenceEqual(Build(copy,init,update,values[UniversalExeBindings.CopyBufferGlobal],values[UniversalExeBindings.UpdateGlobal])))
            throw new InvalidDataException("The compact fresh initializer differs from its canonical owner.");
        if(levelId==65){values[Overridden[0]]=copy;values[Overridden[1]]=init;values[Overridden[2]]=update;}
        bindings=values.ToDictionary(p=>p.Key,p=>new UniversalExeBinding(p.Key,0,0,p.Value));return true;
    }
}

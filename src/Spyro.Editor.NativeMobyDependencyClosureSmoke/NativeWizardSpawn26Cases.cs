using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

internal static class NativeWizardSpawn26Cases
{
    internal static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        byte[] original=Convert.FromHexString("33004010560063a2");
        byte[] rewritten=NativeDarkHollowWizardSpawn26Specialization.RewriteNativeDispatch(original);
        check(BinaryPrimitives.ReadUInt32LittleEndian(rewritten)==0x08021C3C&&rewritten.AsSpan(4).SequenceEqual(original.AsSpan(4)),"Class26 dispatcher specialization changed its owner-byte delay instruction.");
        rewritten[4]^=1;check(original[4]==0x56,"Specialized code aliases original instruction input.");
        reject(()=>NativeDarkHollowWizardSpawn26Specialization.RewriteNativeDispatch(new byte[8]),"preimage");
        reject(()=>NativeDarkHollowWizardSpawn26Specialization.RewriteNativeDispatch(new byte[12]),"length");
        byte[] caller=Convert.FromHexString("26000424212860020780023ccc58428c0000000009f8400001003126");
        NativeDarkHollowWizardSpawn26Specialization.ValidateCallerProof(0x8007EBDC,0x26,caller);
        check(true,"Exact source lightning-parent class26 caller refused.");
        reject(()=>NativeDarkHollowWizardSpawn26Specialization.ValidateCallerProof(0x8007EBE0,0x26,caller),"caller");
        reject(()=>NativeDarkHollowWizardSpawn26Specialization.ValidateCallerProof(0x8007EBDC,0x11B,caller),"parent class");
        byte[] bad=caller.ToArray();bad[0]=0x25;
        reject(()=>NativeDarkHollowWizardSpawn26Specialization.ValidateCallerProof(0x8007EBDC,0x26,bad),"class26");
        bad=caller.ToArray();bad[6]^=1;
        reject(()=>NativeDarkHollowWizardSpawn26Specialization.ValidateCallerProof(0x8007EBDC,0x26,bad),"parent");
        ComparePc(CompareChain(),check);
        Console.WriteLine("NATIVE_WIZARD_CLASS26_PRIVATE_CALLER_AND_DISPATCH_SPECIALIZATION_OK");
    }
    internal static void Source(NativeDarkHollowWizardBehaviorSource source,Action<bool,string> check)
    {
        var specialized=NativeDarkHollowWizardSpawn26Specialization.Create(source);
        check(specialized.Callers.Select(c=>c.CallPc).SequenceEqual(new uint[]{0x8007EBDC,0x80082DB4,0x80082DF0,0x80082E38}),"Source specialization caller set changed.");
        check(specialized.Rewrite.SourcePc==0x80086E64&&specialized.Rewrite.BeforeWord==0x10400033&&specialized.Rewrite.AfterWord==0x08021C3C&&specialized.Rewrite.DelayWord==0xA2630056,"Native single-instruction specialization certificate changed.");
        check(specialized.CopyOriginalCompareChain().AsSpan().SequenceEqual(CompareChain()),"Independent source compare-chain fixture changed.");
        ComparePc(specialized.CopyOriginalCompareChain(),check);
        check(specialized.DeadV0Overwrite.RuntimePc==0x8003A720,"Specialization lost its shared-callee v0 overwrite proof.");
        foreach(var original in source.SpawnPackage.Fragments)
        {
            byte[] before=original.CopyBytes(),after=specialized.Package.Fragments.Single(f=>f.Source.Id==original.Source.Id).CopyBytes();
            for(int at=0;at<before.Length;at++)
                if(original.Source.SourcePc+(uint)at is <0x80086E64 or >=0x80086E68)check(before[at]==after[at],"Specialization changed an undeclared native allocator/owner/initializer/finalizer byte.");
        }
        check(!specialized.GlobalSpawnCallbackReplaced&&!specialized.RuntimeFitProven&&!specialized.PublicationApproved,"Private callable specialization became global/runtime/publication approval.");
        check(specialized.Package.Transfers.All(t=>t.SourcePc!=0x80086E6C)&&specialized.Package.Transfers.Single(t=>t.SourcePc==0x80086E64).TargetPc==0x800870F0,"Broad dispatcher fallthrough remains unresolved after class26 specialization.");
        Console.WriteLine($"NATIVE_WIZARD_CLASS26_SOURCE_SPECIALIZATION_OK original={specialized.OriginalPackageSha256} specialized={specialized.SpecializedPackageSha256} runtime/publication=false");
    }
    private static byte[] CompareChain()=>Convert.FromHexString("33004010560063a20401022a6e0240104600022a140040104300022a8f01401010000224770002121100022a070040100e000224210102120f0002244a00021200000000b71e0208000000002200022480000212260002248c00021200000000");
    private static void ComparePc(byte[] native,Action<bool,string> check)
    {
        var original=Execute(native);var specialized=Execute(NativeDarkHollowWizardSpawn26Specialization.RewriteNativeDispatch(native.AsSpan(0,8)));
        check(original.Pc==0x800870F0&&specialized.Pc==original.Pc&&original.Address==0x80100056&&specialized.Address==original.Address&&original.Owner==128&&specialized.Owner==128,
            "Native/specialized compare-chain PC or byte-owner delay semantics differ.");
        check(original.Registers[2]==0x26&&specialized.Registers[2]==1,"Expected dead v0 difference at specialized initializer entry changed.");
        // 870F0..F8 does not read v0; its native callee3A720 begins
        // ADDIU v0,zero,10. Source issuer independently pins that entry.
        original.Registers[2]=specialized.Registers[2]=0x10;
        check(original.Registers.SequenceEqual(specialized.Registers),"Class26 specialization changed a live caller register.");
        static (uint Pc,uint Address,byte Owner,uint[] Registers) Execute(byte[] code)
        {
            uint[] r=new uint[32];r[16]=0x26;r[2]=1;r[3]=128;r[19]=0x80100000;
            uint pc=0x80086E64,address=0;byte owner=0;uint? delayed=null;
            for(int steps=0;steps<64;steps++)
            {
                if(pc<0x80086E64||pc>=0x80086E64+code.Length)return(pc,address,owner,r);
                uint w=BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan((int)(pc-0x80086E64)));int rs=(int)(w>>21)&31,rt=(int)(w>>16)&31;short imm=(short)w;
                uint? next=delayed;delayed=null;
                switch(w>>26)
                {
                    case 0:if(w!=0)throw new InvalidDataException("Unknown compare-chain SPECIAL.");break;
                    case 2:delayed=((pc+4)&0xF0000000)|((w&0x3FFFFFF)*4);break;
                    case 4:delayed=r[rs]==r[rt]?unchecked(pc+4+(uint)(imm*4)):pc+8;break;
                    case 9:r[rt]=unchecked(r[rs]+(uint)imm);break;
                    case 10:r[rt]=(int)r[rs]<imm?1u:0u;break;
                    case 0x28:address=unchecked(r[rs]+(uint)imm);owner=(byte)r[rt];break;
                    default:throw new InvalidDataException("Unknown compare-chain opcode.");
                }
                r[0]=0;pc=next??pc+4;
            }
            throw new InvalidDataException("Compare-chain exceeded bounded instruction steps.");
        }
    }
}

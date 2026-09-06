using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeSpawn26CallerEvidence(uint CallPc,ushort ParentClassId,uint WindowPc,int ByteLength,string Sha256);
public sealed record NativeSpawn26InstructionSpecialization(uint SourcePc,uint BeforeWord,uint AfterWord,uint DelayWord);

/// <summary>A source-certified private class26 callable; no global callback or native dispatch installation.</summary>
public sealed class NativeDarkHollowWizardSpawn26Specialization
{
    private static readonly (uint Pc,ushort Class,uint Window,int Length,int ClassOffset,int ParentOffset,string Sha)[] Profiles=
    [
        (0x8007EBDC,0x26,0x8007EBC8,0x1C,0,4,"f56082709e50b18beed4f35a0a671d48264d0a66616eb091525e0190659fad09"),
        (0x80082DB4,0x11B,0x80082DA4,0x18,0,12,"6d6f20f213a16f81a36efab52637b73c1733333bca18c6f1b0bca1b514ec2c6d"),
        (0x80082DF0,0x11B,0x80082DDC,0x1C,0,4,"bf424f63b2978765ca47c98848dfeff815db1470e8bb77d180d3da1fa7a29b7a"),
        (0x80082E38,0x11B,0x80082E24,0x1C,4,0,"7ef76ba88a73520babc28f0638abc65389f01678763483179ff376da846a0b7f")
    ];
    private readonly byte[] _dispatcher;
    private NativeDarkHollowWizardSpawn26Specialization(NativeDarkHollowWizardBehaviorSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.PackageSha256!="a9a8227c74a9c479f7f94c82314813809bd5caa0a5a137b0c18d614b80b3e3df")
            throw new InvalidDataException("Class26 specialization requires the exact source-issued native package.");
        var original=source.SpawnPackage;var nativeDomain=original.Fragments[0].Source.Read.Extent.Space;
        foreach(var p in Profiles)
        {
            byte[] window=source.ReadSourceExtent(new(nativeDomain,p.Window-0x8007AA38,p.Length));
            ValidateCallerProof(p.Pc,p.Class,window);
            if(!source.MainPackage.Transfers.Any(t=>t.SourcePc==p.Pc&&t.Kind==NativeMipsBehaviorTransferKind.CallbackRegister))
                throw new InvalidDataException("Class26 caller is not an owned native main callback.");
        }
        _dispatcher=source.ReadSourceExtent(new(nativeDomain,0x80086E64-0x8007AA38,0x60));
        if(Hash(_dispatcher)!="53350a2947162a226dc7cdf3477dcb8d72af307891686b09cc738ea513c5c924")
            throw new InvalidDataException("Native class26 compare-chain complete source preimage changed.");
        DeadV0Overwrite=source.DirectExeCalls.Single(c=>c.RuntimePc==0x8003A720);
        if(DeadV0Overwrite.EntryInstructionSha256!=Hash(Convert.FromHexString("10000224"))||
            !source.CopySourceFragment("spawn26-initializer").AsSpan(0,12).SequenceEqual(Convert.FromHexString("0000728ec8e9000c21206002")))
            throw new InvalidDataException("Class26 specialization's dead-v0 continuation/first-overwrite proof changed.");
        Callers=Array.AsReadOnly(Profiles.Select(p=>new NativeSpawn26CallerEvidence(p.Pc,p.Class,p.Window,p.Length,p.Sha)).ToArray());
        Rewrite=new(0x80086E64,0x10400033,0x08021C3C,0xA2630056);
        var fragments=original.Fragments.ToArray();var first=fragments.Single(f=>f.Source.Id=="spawn26-native-preamble");
        byte[] specialized=first.CopyBytes();int offset=(int)(Rewrite.SourcePc-first.Source.SourcePc);
        RewriteNativeDispatch(specialized.AsSpan(offset,8)).CopyTo(specialized,offset);
        fragments[Array.IndexOf(fragments,first)]=new(first.Source,specialized,false);
        var transfers=original.Transfers.Where(t=>t.SourcePc!=Rewrite.SourcePc&&t.SourcePc!=0x80086E6C)
            .Append(new(Rewrite.SourcePc,0x800870F0,true,NativeMipsBehaviorTransferKind.Jump26,Rewrite.AfterWord,Rewrite.DelayWord)).OrderBy(t=>t.SourcePc).ToArray();
        // Keep the exact Original extraction read and its hash. Only the copied
        // instruction payload changes, under the explicit before/after ledger.
        Package=new(fragments,transfers,original.StackAccesses,[0x80086DD8],[],source.ReadSourceExtent);
        OriginalPackageSha256=Hash(original.Fragments.SelectMany(f=>f.CopyBytes()).ToArray());
        SpecializedPackageSha256=Hash(fragments.SelectMany(f=>f.CopyBytes()).ToArray());
    }
    public static NativeDarkHollowWizardSpawn26Specialization Create(NativeDarkHollowWizardBehaviorSource source)=>new(source);
    public IReadOnlyList<NativeSpawn26CallerEvidence> Callers { get; }
    public NativeSpawn26InstructionSpecialization Rewrite { get; }
    public string OriginalPackageSha256 { get; }
    public string SpecializedPackageSha256 { get; }
    public NativeMobyBehaviorCallEvidence DeadV0Overwrite { get; }
    public bool GlobalSpawnCallbackReplaced=>false;
    public bool RuntimeFitProven=>false;
    public bool PublicationApproved=>false;
    internal NativeMipsBehaviorFragmentPackage Package { get; }
    internal byte[] CopyOriginalCompareChain()=>_dispatcher.ToArray();
    internal static byte[] RewriteNativeDispatch(ReadOnlySpan<byte> branchAndDelay)
    {
        if(branchAndDelay.Length!=8)throw new InvalidDataException("Native class26 branch/delay length must be exactly eight bytes.");
        if(Word(branchAndDelay,0)!=0x10400033||Word(branchAndDelay,4)!=0xA2630056)
            throw new InvalidDataException("Native class26 dispatcher instruction preimage changed.");
        byte[] result=branchAndDelay.ToArray();BinaryPrimitives.WriteUInt32LittleEndian(result,0x08021C3C);return result;
    }
    internal static void ValidateCallerProof(uint callPc,ushort parentClass,ReadOnlySpan<byte> window)
    {
        var matches=Profiles.Where(p=>p.Pc==callPc).ToArray();
        if(matches.Length!=1)throw new InvalidDataException("Unknown native class26 caller; no generic spawn authority.");
        var p=matches[0];
        if(parentClass!=p.Class)throw new InvalidDataException("Native class26 caller parent class differs from its source-owned handler.");
        if(window.Length!=p.Length)throw new InvalidDataException("Native class26 caller source window length changed.");
        if(Word(window,p.ClassOffset)!=0x24040026)throw new InvalidDataException("Native caller does not request exact class26.");
        if(Word(window,p.ParentOffset)!=0x02602821)throw new InvalidDataException("Native caller does not pass its current actor as parent.");
        if(Hash(window)!=p.Sha)throw new InvalidDataException("Native class26 caller complete preimage changed.");
    }
    private static uint Word(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>NativeMipsBehaviorFragmentCodec.Hash(bytes);
}

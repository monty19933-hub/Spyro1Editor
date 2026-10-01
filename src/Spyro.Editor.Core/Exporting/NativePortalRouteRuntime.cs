using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using Spyro.Editor.Core.Universal;

[assembly: InternalsVisibleTo("Spyro.Editor.NativePortalRouteRuntimeSmoke")]

namespace Spyro.Editor.Core.Exporting;

public sealed record NativePortalRouteOverlay(byte[] Overlay, uint UpdatePointer, uint CopyBuffer, uint ReturnStubPointer);

/// <summary>Return routing uses an authenticated rescheduling of the original pause-exit
/// function, plus metadata appended to each edited resident overlay. No executable cave is used.</summary>
public static class NativePortalRouteRuntime
{
    public const uint PauseExitAddress = 0x8002C664;
    public const int PauseExitLength = 0xB0;
    public const uint RouteHelperAddress = PauseExitAddress + 92;
    public const uint TransitionAddress = 0x8004AC24;
    public const uint ResetAddress = 0x80058C7C;
    public const uint PauseCallerAddress = 0x8002E0E8;
    public const uint FlightReturnAddress = 0x8002F050;
    public const int FlightReturnLength = 0x60;
    public const string FlightReturnSourceSha256 = "979b688bed32f9573278105c903870d7fd0cc3de1e5124c88db52e5be89b4a36";
    public const uint Tag = 0x54524F50; // PORT
    public const string PauseExitSourceSha256 = "17c22be3523fbd4e6730d25d24d5ad80530185d67d2fc4fc9ccceabd5385f423";
    public const string ResetSourceSha256 = "92849a178ac26ad8b11b1adcc44e6ab39f8aee419ccfedc94939729069d9c1e0";
    public const string PauseCallerSourceSha256 = "6c754995813f610b577873ea932ab3691d680b5a8da162820cd4d1142aeec383";

    private sealed class Asm
    {
        public UniversalAsmPart Part { get; } = new();
        public void I(int op,int rs,int rt,int imm)=>Part.Emit(UniversalMips.EncodeI(op,rs,rt,imm));
        public void R(int rs,int rt,int rd,int shift,int function)=>Part.Emit((uint)(rs<<21|rt<<16|rd<<11|shift<<6|function));
        public void J(uint target)=>Part.Emit(UniversalMips.EncodeJ(2,target));
        public void Jal(uint target)=>Part.Emit(UniversalMips.EncodeJ(3,target));
        public void N()=>Part.Emit(0);
        public void B(int op,int rs,int rt,string label)=>Part.Emit(UniversalMips.EncodeI(op,rs,rt,0),new(UniversalFixKind.BranchLabel,Label:label));
        public void Label(string label)=>Part.Label(label);
        public byte[] Bytes(uint address)=>UniversalBehaviorBundleBuilder.Assemble([Part],address).Code;
    }

    public static byte[] BuildRouteHelper()
    {
        var a=new Asm();
        a.I(15,0,8,0x8007);a.I(35,8,3,0x596C);a.I(15,0,2,0x6666);a.I(13,2,2,0x6667);
        a.R(3,2,0,0,24);a.I(35,8,9,0x5734);a.I(15,0,10,(int)(Tag>>16));a.I(13,10,10,(int)(Tag&0xFFFF));
        a.I(35,9,11,16);a.R(0,0,2,0,16);a.R(0,2,2,2,3);a.R(0,2,12,2,0);a.R(12,2,2,0,33);
        a.B(5,11,10,"writes");a.R(0,2,2,1,0);
        a.I(35,9,2,20);a.I(35,9,3,24);a.N();
        a.Label("writes");a.I(43,8,3,0x58AC);a.R(31,0,0,0,8);a.I(43,8,2,0x58B4);
        byte[] code=a.Bytes(RouteHelperAddress);
        if(code.Length!=84)throw new InvalidDataException("Native portal route helper exceeded its authenticated owner.");
        return code;
    }

    public static byte[] BuildPauseExit()
    {
        var a=new Asm();
        a.I(9,29,29,-24);a.I(43,29,31,16);a.Jal(ResetAddress);a.N();
        a.I(15,0,8,0x8007);a.I(15,0,3,0x8000);a.I(13,3,3,0x12);
        a.I(43,8,0,0x5864);a.I(43,8,0,0x56AC);a.I(43,8,3,0x6E90);
        a.I(9,0,2,-1);a.I(43,8,2,0x576C);a.I(9,0,2,1);
        foreach(int offset in new[]{0x57D8,0x56D0,0x56B0,0x579C})a.I(43,8,2,offset);
        a.Jal(RouteHelperAddress);a.N();a.I(35,29,31,16);a.I(9,29,29,24);a.R(31,0,0,0,8);a.N();
        byte[] main=a.Bytes(PauseExitAddress),leaf=BuildRouteHelper();
        if(main.Length!=92||main.Length+leaf.Length!=PauseExitLength)
            throw new InvalidDataException("Native portal pause exit exceeded its original function.");
        return main.Concat(leaf).ToArray();
    }

    /// <summary>Validate every owner before changing the staged executable. The original
    /// pause caller immediately restores its frame and consumes none of the helper's t registers.</summary>
    public static void PatchPauseExit(byte[] originalExecutable, byte[] stagedExecutable)
    {
        ArgumentNullException.ThrowIfNull(originalExecutable);ArgumentNullException.ThrowIfNull(stagedExecutable);
        if(originalExecutable.Length!=0x66000||stagedExecutable.Length!=originalExecutable.Length)
            throw new InvalidDataException("Native portal routing requires the complete USA executable image.");
        static ReadOnlySpan<byte> Window(byte[] image,uint address,int count)=>image.AsSpan(checked((int)(address-UniversalExeBindings.ExeBase)),count);
        static void Original(byte[] image,uint address,int count,string digest)
        {
            if(Convert.ToHexStringLower(SHA256.HashData(Window(image,address,count)))!=digest)
                throw new InvalidDataException($"Native portal route owner {address:X8} differs from its original source.");
        }
        Original(originalExecutable,PauseExitAddress,PauseExitLength,PauseExitSourceSha256);
        Original(originalExecutable,ResetAddress,0x44,ResetSourceSha256);
        Original(originalExecutable,PauseCallerAddress,0x44,PauseCallerSourceSha256);
        Original(stagedExecutable,ResetAddress,0x44,ResetSourceSha256);
        Original(stagedExecutable,PauseCallerAddress,0x44,PauseCallerSourceSha256);
        byte[] patch=BuildPauseExit();var staged=Window(stagedExecutable,PauseExitAddress,PauseExitLength);
        if(!staged.SequenceEqual(Window(originalExecutable,PauseExitAddress,PauseExitLength))&&!staged.SequenceEqual(patch))
            throw new InvalidDataException("Native portal pause exit is neither its original owner nor the canonical route helper.");
        patch.CopyTo(stagedExecutable.AsSpan(checked((int)(PauseExitAddress-UniversalExeBindings.ExeBase)),PauseExitLength));
    }

    public static byte[] BuildFlightReturn()
    {
        var a=new Asm();
        // t5 is unused by the helper. Retain the old target for the original
        // homeworld branch, which changes transition kind without writing 58B4.
        a.I(15,0,8,0x8007);a.I(35,8,13,0x58B4);a.Jal(RouteHelperAddress);a.N();
        a.B(4,11,10,"return");a.N();
        a.B(5,3,2,"return");a.I(43,8,0,0x58AC);
        a.I(43,8,13,0x58B4);a.J(0x8002F0B0);a.N();
        a.Label("return");a.I(9,0,2,2);a.I(43,8,2,0x5864);a.I(9,0,2,-1);a.I(43,8,2,0x576C);
        a.J(0x8002F350);a.I(9,0,2,1);
        byte[] code=a.Bytes(FlightReturnAddress);
        if(code.Length>FlightReturnLength)throw new InvalidDataException("Native flight return exceeded its original owner.");
        byte[] result=new byte[FlightReturnLength];code.CopyTo(result,0);return result;
    }

    public static void PatchFlightExit(byte[] originalExecutable,byte[] stagedExecutable) => PatchFlightReturn(originalExecutable,stagedExecutable);

    public static void PatchFlightReturn(byte[] originalExecutable,byte[] stagedExecutable)
    {
        ArgumentNullException.ThrowIfNull(originalExecutable);ArgumentNullException.ThrowIfNull(stagedExecutable);
        if(originalExecutable.Length!=0x66000||stagedExecutable.Length!=originalExecutable.Length)
            throw new InvalidDataException("Native flight routing requires the complete USA executable image.");
        static ReadOnlySpan<byte> Window(byte[] image,uint address,int count)=>image.AsSpan(checked((int)(address-UniversalExeBindings.ExeBase)),count);
        foreach(var (address,count,digest) in new (uint,int,string)[]{
            (FlightReturnAddress,FlightReturnLength,FlightReturnSourceSha256),
            (0x8002F0B0,0x14,"ca8a4fae0ac766bc5ac5f8b8c56024ce365766619ca879161f21a106a6a75203"),
            (0x8002F350,0x18,"dfb09d26d9153c7a1a3e81f4c7d71e802685750ef2516e7c08428828c2922bf1"),
            (0x8002F3A4,0x20,"c7ca137f4e6375496c0eb3a4a38a63fb471945f662fd827f292ed84087147609")})
        {
            if(Convert.ToHexStringLower(SHA256.HashData(Window(originalExecutable,address,count)))!=digest)
                throw new InvalidDataException($"Native flight route owner {address:X8} differs from its original source.");
            if(address!=FlightReturnAddress&&!Window(stagedExecutable,address,count).SequenceEqual(Window(originalExecutable,address,count)))
                throw new InvalidDataException($"Native flight route continuation {address:X8} changed before routing.");
        }
        if(!Window(stagedExecutable,PauseExitAddress,PauseExitLength).SequenceEqual(BuildPauseExit()))
            throw new InvalidDataException("Native flight return requires the installed authenticated portal route helper.");
        byte[] patch=BuildFlightReturn();var staged=Window(stagedExecutable,FlightReturnAddress,FlightReturnLength);
        if(!staged.SequenceEqual(Window(originalExecutable,FlightReturnAddress,FlightReturnLength))&&!staged.SequenceEqual(patch))
            throw new InvalidDataException("Native flight return is neither its original owner nor the canonical route image.");
        patch.CopyTo(stagedExecutable.AsSpan(checked((int)(FlightReturnAddress-UniversalExeBindings.ExeBase)),FlightReturnLength));
    }

    private static readonly (string Key,uint CallPc,string Digest)[] ReturnOwners =
    [
        ("stonehill",0x8007E090,"b33acc6724140e3c8c6e1421540a0aec5ead1647fb40dc4b353441540f832db1"),
        ("darkhollow",0x8007B424,"b33acc6724140e3c8c6e1421540a0aec5ead1647fb40dc4b353441540f832db1"),
        ("townsquare",0x8007E07C,"b33acc6724140e3c8c6e1421540a0aec5ead1647fb40dc4b353441540f832db1"),
        ("toasty",0x8007B528,"b33acc6724140e3c8c6e1421540a0aec5ead1647fb40dc4b353441540f832db1"),
        ("drycanyon",0x8007E634,"b33acc6724140e3c8c6e1421540a0aec5ead1647fb40dc4b353441540f832db1"),
        ("clifftown",0x8007E62C,"b33acc6724140e3c8c6e1421540a0aec5ead1647fb40dc4b353441540f832db1"),
        ("icecavern",0x8007B8B8,"b33acc6724140e3c8c6e1421540a0aec5ead1647fb40dc4b353441540f832db1"),
        ("doctorshemp",0x8007B49C,"b33acc6724140e3c8c6e1421540a0aec5ead1647fb40dc4b353441540f832db1"),
        ("alpineridge",0x8007BEF4,"b33acc6724140e3c8c6e1421540a0aec5ead1647fb40dc4b353441540f832db1"),
        ("highcaves",0x8007BA28,"c1c77c7ad910254efc643eed8e932ed57a1aab316a030eda677ad4b6e15c434a"),
        ("wizardpeak",0x8007BB94,"b33acc6724140e3c8c6e1421540a0aec5ead1647fb40dc4b353441540f832db1"),
        ("blowhard",0x8007B52C,"b33acc6724140e3c8c6e1421540a0aec5ead1647fb40dc4b353441540f832db1"),
        ("terracevillage",0x8007B9C8,"85560a0abdb27dc22cb20a4d2e145986090496ec73963588547d04ef180b68fa"),
        ("mistybog",0x8007B5B8,"b33acc6724140e3c8c6e1421540a0aec5ead1647fb40dc4b353441540f832db1"),
        ("treetops",0x8007BA8C,"e30015637a59befe35f9f04230471d5205608403ac086e619c1737854ce0daab"),
        ("metalhead",0x8007BB58,"e30015637a59befe35f9f04230471d5205608403ac086e619c1737854ce0daab"),
        ("darkpassage",0x8007B8D4,"b33acc6724140e3c8c6e1421540a0aec5ead1647fb40dc4b353441540f832db1"),
        ("loftycastle",0x8007B8D0,"b33acc6724140e3c8c6e1421540a0aec5ead1647fb40dc4b353441540f832db1"),
        ("hauntedtowers",0x8007B8EC,"8fe83a0dff6ddd5c89cb2cac710638fad61dd7d906723a6ce440b96ac65e048c"),
        ("jacques",0x8007B548,"b33acc6724140e3c8c6e1421540a0aec5ead1647fb40dc4b353441540f832db1"),
        ("gnorccove",0x8007B914,"b33acc6724140e3c8c6e1421540a0aec5ead1647fb40dc4b353441540f832db1"),
        ("twilightharbor",0x8007B48C,"b33acc6724140e3c8c6e1421540a0aec5ead1647fb40dc4b353441540f832db1"),
        ("gnastysloot",0x8007B35C,"b33acc6724140e3c8c6e1421540a0aec5ead1647fb40dc4b353441540f832db1"),
    ];

    /// <summary>Only complete copies of the original class-009 return packets may
    /// change their common-transition call. Imported copies use the same source certificate.</summary>
    public static int PatchReturnHomeCalls(UniversalSourceLibrary donors,byte[] finalOverlay,uint returnStub)
    {
        ArgumentNullException.ThrowIfNull(donors);ArgumentNullException.ThrowIfNull(finalOverlay);
        long stubOffset=(long)returnStub-UniversalOverlay.Base;
        if((returnStub&3)!=0||stubOffset<0||stubOffset+32>finalOverlay.Length||
            !finalOverlay.AsSpan((int)stubOffset,32).SequenceEqual(BuildReturnStub(returnStub)))
            throw new InvalidDataException("Return Home calls require the exact appended resident route stub.");
        List<byte[]> packets=[];
        foreach(var certificate in ReturnOwners)
        {
            var owner=donors.Get(certificate.Key).Overlay;uint start=certificate.CallPc-44;
            byte[] packet=owner.Bytes.Slice(checked((int)(start-UniversalOverlay.Base)),52).ToArray();
            if(Convert.ToHexStringLower(SHA256.HashData(packet))!=certificate.Digest)
                throw new InvalidDataException($"Native Return Home owner {certificate.Key}:{certificate.CallPc:X8} differs from its source certificate.");
            // Verify ownership in the actual class-009 closure, including the packet's
            // uninterrupted entry. An identical packet elsewhere is not a source owner.
            var dispatch=UniversalDispatchAnalysis.FindUpdateSwitch(owner);
            var trace=UniversalDispatchAnalysis.Trace(owner,dispatch.TreeStart,9,dispatch.Seeds,dispatch.ClassRegister);
            var (code,tables)=UniversalDispatchAnalysis.Closure(owner,dispatch.Function,[trace.Stop],new HashSet<uint>{dispatch.TreeStart});
            if(Enumerable.Range(0,13).Any(n=>!code.Contains(start+(uint)n*4))||
                UniversalMips.IsBranch(owner.Word(start-4)))
                throw new InvalidDataException("Native Return Home packet lacks its complete source class-009 owner.");
            foreach(uint pc in code)
            {
                uint word=owner.Word(pc);
                if(UniversalMips.IsPcRelative(word)||UniversalMips.Op(word) is 2 or 3)
                {
                    uint target=UniversalMips.BranchTarget(word,pc);
                    if(target>start&&target<start+52)
                        throw new InvalidDataException("Native Return Home packet has an alternate interior entry.");
                }
            }
            if(tables.SelectMany(t=>t.Targets).Any(target=>target>start&&target<start+52))
                throw new InvalidDataException("Native Return Home packet has an interior dispatch-table entry.");
            if(!packets.Any(existing=>existing.SequenceEqual(packet)))packets.Add(packet);
        }
        // Validate first, then mutate. Existing canonical calls can be recognized on
        // repeat composition without accepting arbitrary calls to this new stub.
        List<int> calls=[];uint expected=UniversalMips.EncodeJ(3,TransitionAddress),replacement=UniversalMips.EncodeJ(3,returnStub);
        for(int at=0;at+52<=finalOverlay.Length;at+=4)
        {
            var candidate=finalOverlay.AsSpan(at,52);uint call=BinaryPrimitives.ReadUInt32LittleEndian(candidate.Slice(44,4));
            if(call!=expected&&call!=replacement)continue;
            if(!packets.Any(packet=>finalOverlay.AsSpan(at,44).SequenceEqual(packet.AsSpan(0,44))&&
                finalOverlay.AsSpan(at+48,4).SequenceEqual(packet.AsSpan(48,4))))continue;
            if(at>=4&&UniversalMips.IsBranch(BinaryPrimitives.ReadUInt32LittleEndian(finalOverlay.AsSpan(at-4,4))))
                throw new InvalidDataException("A copied Return Home packet became a control-transfer delay slot.");
            calls.Add(at+44);
        }
        foreach(int offset in calls)BinaryPrimitives.WriteUInt32LittleEndian(finalOverlay.AsSpan(offset,4),replacement);
        return calls.Count;
    }

    public static byte[] BuildReturnStub(uint address)
    {
        var a=new Asm();a.I(9,29,29,-24);a.I(43,29,31,16);a.Jal(RouteHelperAddress);a.N();
        a.I(35,29,31,16);a.I(9,29,29,24);a.J(TransitionAddress);a.N();return a.Bytes(address);
    }

    /// <summary>Append after all behavior/save-map wrappers. The update tail still chains
    /// to the current resident update; SMAP at +8/+12 and SIGN at -12 retain their existing owners.</summary>
    public static NativePortalRouteOverlay AppendReturnWrapper(byte[] currentOverlay,uint currentUpdate,uint currentCopyBuffer,int homeId,int portalToken)
    {
        ArgumentNullException.ThrowIfNull(currentOverlay);
        if(homeId is <10 or >60||homeId%10!=0||portalToken/10*10!=homeId||portalToken%10 is <1 or >5||portalToken>64)
            throw new InvalidDataException("Return Home requires an original homeworld portal token.");
        long used=(long)currentCopyBuffer-UniversalOverlay.Base,offset=(long)currentUpdate-UniversalOverlay.Base;
        if((currentUpdate&3)!=0||(currentCopyBuffer&3)!=0||offset<0||offset+16>used||used>currentOverlay.Length)
            throw new InvalidDataException("Portal routes must append to the actual bound resident overlay.");
        int updateOffset=checked((int)offset);uint U(int at)=>BinaryPrimitives.ReadUInt32LittleEndian(currentOverlay.AsSpan(at,4));
        bool signs=updateOffset>=12&&U(updateOffset-12)==UniversalSpecialBehaviors.SignHookMarker;
        if(signs)
        {
            uint jump=U(updateOffset-8),delay=U(updateOffset-4),target=UniversalMips.BranchTarget(jump,currentUpdate-8);
            if(UniversalMips.Op(jump)!=2||delay!=0||target<UniversalOverlay.Base||target>=currentCopyBuffer||(target&3)!=0)
                throw new InvalidDataException("Portal routes require the intact resident native sign prefix.");
        }
        uint mapTag=0,mapLeaf=0;
        if(U(updateOffset+8)==NativeMobySaveMap.Tag)
        {
            mapTag=NativeMobySaveMap.Tag;mapLeaf=U(updateOffset+12);
            if(mapLeaf<UniversalOverlay.Base||(mapLeaf&3)!=0||(long)mapLeaf+48>currentCopyBuffer)
                throw new InvalidDataException("Portal routes require the existing resident save-map leaf.");
        }
        uint prefix=checked((currentCopyBuffer+7)&~7u),start=checked(prefix+12),stub=checked(start+28);
        var a=new Asm();a.J(currentUpdate);a.N();a.Part.Emit(mapTag);a.Part.Emit(mapLeaf);
        a.Part.Emit(Tag);a.Part.Emit((uint)homeId);a.Part.Emit((uint)portalToken);
        a.I(9,29,29,-24);a.I(43,29,31,16);a.Jal(RouteHelperAddress);a.N();
        a.I(35,29,31,16);a.I(9,29,29,24);a.J(TransitionAddress);a.N();
        byte[] code=a.Bytes(start);if(code.Length!=60)throw new InvalidDataException("Native portal wrapper layout changed.");
        uint copy=checked((stub+32+15)&~15u);
        byte[] result=new byte[UniversalLevelData.Align(checked((int)(copy-UniversalOverlay.Base)),0x800)];
        currentOverlay.AsSpan(0,checked((int)used)).CopyTo(result);
        if(signs)currentOverlay.AsSpan(updateOffset-12,12).CopyTo(result.AsSpan(checked((int)(prefix-UniversalOverlay.Base)),12));
        code.CopyTo(result,checked((int)(start-UniversalOverlay.Base)));
        return new(result,start,copy,stub);
    }
}

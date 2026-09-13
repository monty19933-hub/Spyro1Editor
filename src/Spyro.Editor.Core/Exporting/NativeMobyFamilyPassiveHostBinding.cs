using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Exact Artisans Grass dispatch evidence for one actual detached Wizard Host.
/// This proves only the class-01F5 scalar path through the rewritten dispatch;
/// it does not install a model/row, compose a destination, or certify complete
/// native behavior, installed dispatch, publication or runtime execution.
/// </summary>
internal sealed class NativeMobyFamilyPassiveHostBinding
{
    private const uint Base=0x8007AA38,ClassLoad=0x8007DB0C,ClassWord=0x86640036,
        Start=0x8007DB34,Wrapper=0x8008B7A8,Decision=0x8007DC70,Exit=0x80087258;
    private const string WrapperHash="f61a910589ba3dbdf971e588c30ea18c91493b47f7bd76283bd3cf943bb5bc80";
    private const string DecisionHash="fc27e64af0f02e3617fd9b791759ea4b2663b9bfc6aeb19abb885f68d41e5b44";
    private const string ExitHash="edb2f325f44eafb8149e0f2b93912a9009e7aea08c77d17bdeb71bb6af20e372";
    private readonly NativeId65FreshWizardResearchHostPlan _host;
    private readonly NativeMobyFamilyAssetPlan _family;
    private readonly NativeMobyFamilyPassiveDispatchTrace _trace;

    private NativeMobyFamilyPassiveHostBinding(NativeId65FreshWizardResearchHostPlan host,NativeMobyFamilyAssetPlan family,
        NativeMobyFamilyPassiveDispatchTrace trace)
    {
        _host=host;_family=family;_trace=trace;
        HostPlanSha256=host.PlanSha256;FamilyAssetPlanSha256=family.PlanSha256;
        OverlaySha256=host.OverlaySha256;SourceImageSha256=family.SourceImageSha256;
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            ProfileId,SourceImageSha256,HostPlanSha256,FamilyAssetPlanSha256,OverlaySha256,DonorLevelKey,DonorTrueIndex,NativeClass,
            ClassLoadAddress,ClassLoadWord,DispatchStartAddress,WrapperAddress,WrapperByteLength,WrapperSha256,
            RetainedDecisionAddress,RetainedDecisionByteLength,RetainedDecisionSha256,DefaultLoopExitAddress,DefaultLoopExitSha256,
            ExecutedInstructionAddresses,ExecutedInstructionWords,ExecutedTraceSha256,CallInstructionCount,PropertiesReadCount,
            RuntimeActorWriteCount,SourceCertified,PassiveDispatchCompatible,DestinationModelBound,DestinationSceneBound,
            NativeImportApproved,CompleteNativeBehaviorProven,InstalledDispatchProven,RuntimeVerified,PublicationApproved}));
    }

    public const string ProfileId="native-artisans-t121-01f5-actual-wizard-host-passive-dispatch-v1";
    internal NativeId65FreshWizardResearchHostPlan HostAncestor=>_host;
    internal NativeMobyFamilyAssetPlan FamilyAncestor=>_family;
    public string SourceImageSha256{get;}
    public string HostPlanSha256{get;}
    public string FamilyAssetPlanSha256{get;}
    public string OverlaySha256{get;}
    public string PlanSha256{get;}
    public string DonorLevelKey=>"artisans";
    public int DonorTrueIndex=>121;
    public ushort NativeClass=>0x01F5;
    public uint ClassLoadAddress=>ClassLoad;
    public uint ClassLoadWord=>ClassWord;
    public uint DispatchStartAddress=>Start;
    public uint WrapperAddress=>Wrapper;
    public int WrapperByteLength=>0x44;
    public string WrapperSha256=>WrapperHash;
    public uint RetainedDecisionAddress=>Decision;
    public int RetainedDecisionByteLength=>0x128;
    public string RetainedDecisionSha256=>DecisionHash;
    public uint DefaultLoopExitAddress=>Exit;
    public string DefaultLoopExitSha256=>ExitHash;
    public IReadOnlyList<uint> ExecutedInstructionAddresses=>_trace.ExecutedInstructionAddresses;
    public IReadOnlyList<uint> ExecutedInstructionWords=>_trace.ExecutedInstructionWords;
    public string ExecutedTraceSha256=>_trace.ExecutedTraceSha256;
    public int CallInstructionCount=>_trace.CallInstructionCount;
    public int PropertiesReadCount=>_trace.PropertiesReadCount;
    public int RuntimeActorWriteCount=>_trace.RuntimeActorWriteCount;
    public bool SourceCertified=>true;
    public bool PassiveDispatchCompatible=>true;
    public bool DestinationModelBound=>false;
    public bool DestinationSceneBound=>false;
    public bool NativeImportApproved=>false;
    public bool CompleteNativeBehaviorProven=>false;
    public bool InstalledDispatchProven=>false;
    public bool RuntimeVerified=>false;
    public bool PublicationApproved=>false;

    public static NativeMobyFamilyPassiveHostBinding Bind(NativeId65FreshWizardResearchHostPlan host,NativeMobyFamilyAssetPlan family)
    {
        ArgumentNullException.ThrowIfNull(host);ArgumentNullException.ThrowIfNull(family);
        return new(host,family,Inspect(host,family));
    }

    internal void RequireAncestry(NativeId65FreshWizardResearchHostPlan host,NativeMobyFamilyAssetPlan family)
    {
        if(!ReferenceEquals(host,_host)||!ReferenceEquals(family,_family))
            throw new InvalidDataException("Passive Grass dispatch belongs to another actual Host/family pair even if bytes/hash match.");
        RequireRetainedOwners();
    }

    internal void RequireRetainedOwners()
    {
        if(_host.PlanSha256!=HostPlanSha256||_family.PlanSha256!=FamilyAssetPlanSha256||
            _host.OverlaySha256!=OverlaySha256||_family.SourceImageSha256!=SourceImageSha256)
            throw new InvalidDataException("Passive Grass dispatch retained ancestor identities changed.");
        var actual=Inspect(_host,_family);
        if(actual.ExecutedTraceSha256!=ExecutedTraceSha256||
            !actual.ExecutedInstructionAddresses.SequenceEqual(ExecutedInstructionAddresses)||
            !actual.ExecutedInstructionWords.SequenceEqual(ExecutedInstructionWords)||
            actual.CallInstructionCount!=CallInstructionCount||actual.PropertiesReadCount!=PropertiesReadCount||
            actual.RuntimeActorWriteCount!=RuntimeActorWriteCount)
            throw new InvalidDataException("Passive Grass dispatch retained actual code path changed.");
    }

    private static NativeMobyFamilyPassiveDispatchTrace Inspect(NativeId65FreshWizardResearchHostPlan host,NativeMobyFamilyAssetPlan family)
    {
        host.RequireAncestry(host.SceneAncestor,host.FallbackAncestor);
        family.RequireRetainedOwners();
        if(!host.SourceCertified||!family.SourceCertified||host.SourceImageSha256!=family.SourceImageSha256||
            family.DonorLevelKey!="artisans"||family.DonorWadEntry!=10||family.DonorTrueIndex!=121||family.NativeClass!=0x01F5)
            throw new InvalidDataException("Passive Host dispatch requires the actual original-source Artisans T121 Grass family.");
        byte[] overlay=host.CopyOverlay();host.RequireExactOutputs(overlay,host.CopyInitializerPatch());
        if(overlay.Length!=0x11000||Hash(overlay)!=host.OverlaySha256)
            throw new InvalidDataException("Passive Grass dispatch requires the complete actual composed Host overlay.");
        var wrappers=host.AppendedOwners.Where(x=>x.Id=="dispatch").ToArray();
        if(wrappers.Length!=1||wrappers[0].OverlayOffset!=0x10D70||wrappers[0].ByteLength!=0x44||
            wrappers[0].RuntimePc!=Wrapper||wrappers[0].Sha256!=WrapperHash||Hash(Slice(Wrapper,0x44))!=WrapperHash)
            throw new InvalidDataException("Passive Grass dispatch requires the exact actual appended dispatch owner.");
        var patches=host.TownPatches.Where(x=>x.Id=="class-dispatch").ToArray();
        if(patches.Length!=1||patches[0].Offset!=0x30FC||patches[0].RuntimePc!=Start||patches[0].ByteLength!=8||
            !patches[0].CopyAfterBytes().AsSpan().SequenceEqual(Slice(Start,8))||Word(Start)!=0x08022DEA||Word(Start+4)!=0)
            throw new InvalidDataException("Passive Grass dispatch requires the actual rewritten Town entry and its delay slot.");
        if(Word(ClassLoad)!=ClassWord||Hash(Slice(Decision,0x128))!=DecisionHash||Hash(Slice(Exit,0x18))!=ExitHash)
            throw new InvalidDataException("Passive Grass dispatch retained class load, high-class decision tree or default exit changed.");
        var trace=NativeGrassFamilySourceDecoder.InspectPassiveDispatch(overlay,4,Start,Exit,"actual Wizard Host Grass");
        uint[] prefix=[Start,Start+4,Wrapper,Wrapper+4,Wrapper+8,Wrapper+20,Wrapper+24,Wrapper+28,
            Wrapper+40,Wrapper+44,Wrapper+48,Wrapper+52,Wrapper+56,Decision];
        if(!trace.ExecutedInstructionAddresses.Take(prefix.Length).SequenceEqual(prefix)||
            trace.ExecutedInstructionAddresses.Any(pc=>!(pc>=Start&&pc<Start+8||pc>=Wrapper&&pc<Wrapper+0x44||
                pc>=Decision&&pc<Decision+0x128))||
            trace.ExecutedInstructionAddresses.Contains(0x8008B3F0)||trace.ExecutedInstructionAddresses.Contains(0x8008B48C))
            throw new InvalidDataException("Passive Grass dispatch escaped its exact scalar owners or entered a specialized Wizard role.");
        return trace;

        ReadOnlySpan<byte> Slice(uint pc,int length)=>overlay.AsSpan(checked((int)(pc-Base)),length);
        uint Word(uint pc)=>BinaryPrimitives.ReadUInt32LittleEndian(Slice(pc,4));
    }

    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}

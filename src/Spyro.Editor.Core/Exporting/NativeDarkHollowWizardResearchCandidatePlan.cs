using System.Runtime.CompilerServices;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeWizardResearchSmoke")]

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeWizardResearchReplacement(int Index,int OriginalOffset,int OriginalLength,string OriginalSha256,byte[] Bytes);
public sealed record NativeWizardResearchEntry(int Index,int OriginalOffset,int FinalOffset,int OriginalLength,int FinalLength,string OriginalSha256,string FinalSha256);
internal sealed record NativeWizardResearchPackedWad(byte[] Bytes,IReadOnlyList<NativeWizardResearchEntry> Entries,string OriginalSha256);

internal static class NativeWizardResearchPackedEntryComposer
{
    internal static NativeWizardResearchPackedWad Compose(ReadOnlySpan<byte> header,int originalLength,
        IEnumerable<NativeWizardResearchReplacement> replacements,Func<int,int,byte[]> read)=>
        NativePackedResearchEntryComposer.ComposeDark(header,originalLength,replacements,read);
}

/// <summary>One explicit source-bound research profile; not ordinary export or gameplay approval.</summary>
public sealed class NativeDarkHollowWizardResearchCandidatePlan
{
    private readonly NativeWizardResearchArtifactPlan _artifacts;
    private NativeDarkHollowWizardResearchCandidatePlan(string source,NativeFreeFlightResearchSourceCue cue,
        NativeDarkHollowWizardPlacementPlan placement,NativeDarkHollowWizardResourcePlan resources,
        NativeDarkHollowWizardOverlayPlan overlay,NativeWizardResearchArtifactPlan artifacts)
    {
        SourceImagePath=source;_artifacts=artifacts;Layout=artifacts.Layout;Placement=placement;Resources=resources;Overlay=overlay;Reward=artifacts.Reward;
        Entries=artifacts.Entries;OriginalWadSha256=artifacts.OriginalWadSha256;WadSha256=artifacts.WadSha256;RootSha256=artifacts.RootSha256;
        var reward=Reward;
        RequireStageReversion();
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{Profile,SourceImagePath,sourceImageSha256=NativeMobySourceInventory.OriginalImageSha256,
            sourceCuePath=cue.CuePath,sourceCueSha256=cue.SourceSha256,placement=placement.PlanSha256,resources=resources.GraphFingerprint,
            resources.FinalDataSha256,overlay.OverlaySha256,reward.ExecutableSha256,WadSha256,RootSha256,Entries,
            GrowthBytes,RelocatedExecutableLba,Memory,RuntimeVerified,NormalCreateBinApproved}));
    }
    public const string Profile="native-dark-wizard-dry-corridor-research-v1";
    public const int TargetLevelId=12;
    internal const string ImageName="dark-hollow-native-wizard-r01.bin",CueName="dark-hollow-native-wizard-r01.cue",ReceiptName="native-wizard.receipt.json";
    internal static string[] OutputNames=>[ImageName,ReceiptName,CueName];
    public string SourceImagePath {get;}
    public string PlanSha256 {get;}
    public string OriginalWadSha256 {get;}
    public string WadSha256 {get;}
    public string RootSha256 {get;}
    public int GrowthBytes=>_artifacts.GrowthBytes;
    public int RelocatedExecutableLba=>_artifacts.RelocatedExecutableLba;
    public int FinalExecutableEndLba=>_artifacts.FinalExecutableEndLba;
    public NativeDarkHollowWizardPlacementPlan Placement {get;}
    public NativeDarkHollowWizardResourcePlan Resources {get;}
    public NativeDarkHollowWizardOverlayPlan Overlay {get;}
    public NativeDarkHollowWizardDeathRewardPlan Reward {get;}
    public NativeLevelRuntimeMemoryReport Memory=>Overlay.RuntimeMemory;
    public IReadOnlyList<NativeWizardResearchEntry> Entries {get;}
    public bool RuntimeVerified=>false;
    public bool NormalCreateBinApproved=>false;
    public bool CompleteNativeClosureProven=>false;
    public bool TestLevelSelectorEnabled=>false;
    internal DiscLayout Layout {get;}
    internal string SourceCuePath=>_artifacts.SourceCuePath;
    internal byte[] CopyCueBytes()=>_artifacts.CopyCueBytes();
    internal byte[] CopyWad()=>_artifacts.CopyWad();
    internal byte[] CopyExecutable()=>_artifacts.CopyExecutable();
    internal byte[] CopyRoot()=>_artifacts.CopyRoot();
    public static async Task<NativeDarkHollowWizardResearchCandidatePlan> CaptureAsync(string originalImagePath,string originalCuePath,
        LevelCatalog catalog,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(catalog);string path=Path.GetFullPath(originalImagePath);
        var cue=NativeFreeFlightResearchSourceCue.Capture(originalCuePath,path,ImageName);
        var dependencies=await NativeWizardResearchArtifactPlan.CaptureDependenciesAsync(path,catalog,token);
        var scene=NativeDarkHollowWizardScenePlan.Capture(path);
        var placement=NativeDarkHollowWizardPlacementSource.Capture(path,scene).Compile(new(92832,25248,10445,91808,25248));
        var resources=await NativeDarkHollowWizardResourcePlan.CompileWithPlacementAsync(path,dependencies.Inventory,dependencies.Player,
            dependencies.Sound,dependencies.Texture,dependencies.Descriptor,scene,placement,token);
        var overlay=NativeDarkHollowWizardOverlayPlan.Compile(dependencies.Behavior,resources,dependencies.Particles);
        if(!resources.IncludesAuthoredPlacement||resources.PlacementPlanSha256!=placement.PlanSha256||
            !overlay.AllCodeTargetsMaterialized||!overlay.ActiveDispatchEncoded||!overlay.RuntimeMemory.KnownPostLoadExtentsDisjoint||
            overlay.RuntimeMemory.DynamicMobyCapacity!=90||overlay.RuntimeMemory.NormalReservedBufferGapBytes!=0x441CC)
            throw new InvalidDataException("Research canonical stage composition lost required source/placement/code/native extent conditions.");
        var artifacts=await NativeWizardResearchArtifactPlan.CaptureAsync(path,cue,resources,overlay,token);
        return new(path,cue,placement,resources,overlay,artifacts);
    }
    public Task RequireFreshAsync(CancellationToken token=default)=>_artifacts.RequireFreshAsync(token);
    internal void RequireStageReversion()
    {
        Placement.RequireOriginalAndFinal(Placement.CopyBaseScene(),Placement.CopyScene());
        NativeDarkHollowWizardDeathRewardPlan.RequireSingleRewardWizard(Resources.CopyFinalLevelData());
        _artifacts.RequireStageReversion();
    }
    internal void WriteNewImage(string outputPath,CancellationToken token=default)=>_artifacts.WriteNewImage(outputPath,token);
    internal (string Hash,int Changed) VerifyImage(string path)=>_artifacts.VerifyImage(path);
    internal byte[] ExpectedSector(int lba,byte[] before)=>_artifacts.ExpectedSector(lba,before);
    internal byte[] BuildReceipt((string Hash,int Changed) verified)=>JsonSerializer.SerializeToUtf8Bytes(new
    {
        profile=Profile,PlanSha256,SourceImagePath,sourceImageSha256=NativeMobySourceInventory.OriginalImageSha256,
        sourceCuePath=SourceCuePath,sourceCueSha256=_artifacts.SourceCueSha256,outputImageSha256=verified.Hash,changedRawSectors=verified.Changed,
        outputCueSha256=Hash(CopyCueBytes()),OriginalWadSha256,WadSha256,RootSha256,GrowthBytes,RelocatedExecutableLba,
        FinalExecutableEndLba,Entries,placement=Placement,resourcePlan=Resources.GraphFingerprint,Resources.FinalDataSha256,
        overlaySha256=Overlay.OverlaySha256,executableSha256=Reward.ExecutableSha256,rewardSelector=Reward.RewardSelector,
        nativeRewardValue=Reward.NativeGemValue,treasureTarget=Reward.TreasureTargetAfter,Memory,
        wholeImagePhysicalReadback=true,completeOriginalWadInverseVerified=true,original128RowsPreserved=true,
        researchOnly=true,TestLevelSelectorEnabled,CompleteNativeClosureProven,RuntimeVerified,NormalCreateBinApproved,
        exceptionRollbackPublication=true,crashAtomicPublication=false,hostileFilesystemRaceSafe=false,
        pending=new[]{"full main-helper global/GTE and transient stack behavior","native body/route/bolt trajectory",
            "cold boot and normal portal entry","native patrol and intact lightning/hurt/effects/sound","native death, one10-value gem, +10 collection and persistence",
            "nearby native actors/collision/exit/death/re-entry","ordinary editor export approval"}
    },new JsonSerializerOptions{WriteIndented=true});
    internal static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Both(byte[] bytes,int offset,int value)
    {BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset),value);BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset+4),value);}
}

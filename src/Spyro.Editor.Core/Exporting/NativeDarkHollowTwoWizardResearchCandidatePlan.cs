using System.Runtime.CompilerServices;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Fixed source-issued two-owner research profile; no ordinary export or native runtime approval.</summary>
public sealed class NativeDarkHollowTwoWizardResearchCandidatePlan
{
    private readonly NativeWizardResearchArtifactPlan _artifacts;
    private readonly byte[] _owners;
    private NativeDarkHollowTwoWizardResearchCandidatePlan(NativeDarkHollowWizardAppendPlan append,
        NativeDarkHollowWizardAppendPlacementPlan placement,NativeDarkHollowWizardResourcePlan resources,
        NativeDarkHollowWizardOverlayPlan overlay,NativeWizardResearchArtifactPlan artifacts)
    {
        Appended=append;Placement=placement;Resources=resources;Overlay=overlay;_artifacts=artifacts;
        _owners=append.ProposedOwners.Serialize();
        if(!ReferenceEquals(placement.Appended,append)||!ReferenceEquals(placement.ProposedOwners,append.ProposedOwners)||
            !append.Families.Select(f=>(f.FamilyId,f.NativeIndex)).SequenceEqual(new[]{("z",128),("a",129)})||
            append.FixupCountAfter!=151||Memory.PlacedMobyCount!=130||Memory.DynamicMobyCapacity!=89||
            !Memory.KnownPostLoadExtentsDisjoint||Memory.NormalReservedBufferGapBytes!=0x441CC||
            resources.PlacementPlanSha256!=placement.PlanSha256||!overlay.AllCodeTargetsMaterialized||!overlay.ActiveDispatchEncoded||
            Reward.TreasureTargetAfter!=120)
            throw new InvalidDataException("Two-Wizard research requires its fixed actual source-issued temporal owners, placement and native capacities.");
        RequireStageReversion();
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{Profile,SourceImagePath,sourceImageSha256=NativeMobySourceInventory.OriginalImageSha256,
            sourceCuePath=SourceCuePath,sourceCueSha256=artifacts.SourceCueSha256,proposedOwnersSha256=Hash(_owners),
            append=append.GraphFingerprint,placement=placement.PlanSha256,resources=resources.GraphFingerprint,
            resources.FinalDataSha256,overlay.OverlaySha256,Reward.ExecutableSha256,WadSha256,RootSha256,Entries,
            GrowthBytes,RelocatedExecutableLba,Memory,RuntimeVerified,NormalCreateBinApproved}));
    }
    public const string Profile="native-dark-two-wizards-dry-corridors-research-v1";
    public const int TargetLevelId=12;
    internal const string ImageName="dark-hollow-native-two-wizards-r01.bin",CueName="dark-hollow-native-two-wizards-r01.cue",ReceiptName="native-two-wizards.receipt.json";
    internal static string[] OutputNames=>[ImageName,ReceiptName,CueName];
    public string SourceImagePath=>_artifacts.SourceImagePath;
    public string PlanSha256 {get;}
    public string OriginalWadSha256=>_artifacts.OriginalWadSha256;
    public string WadSha256=>_artifacts.WadSha256;
    public string RootSha256=>_artifacts.RootSha256;
    public int GrowthBytes=>_artifacts.GrowthBytes;
    public int RelocatedExecutableLba=>_artifacts.RelocatedExecutableLba;
    public int FinalExecutableEndLba=>_artifacts.FinalExecutableEndLba;
    public NativeDarkHollowWizardAppendPlan Appended {get;}
    public NativeDarkHollowWizardAppendPlacementPlan Placement {get;}
    public NativeDarkHollowWizardResourcePlan Resources {get;}
    public NativeDarkHollowWizardOverlayPlan Overlay {get;}
    public NativeDarkHollowWizardDeathRewardPlan Reward=>_artifacts.Reward;
    public NativeLevelRuntimeMemoryReport Memory=>Overlay.RuntimeMemory;
    public IReadOnlyList<NativeWizardResearchEntry> Entries=>_artifacts.Entries;
    public bool RuntimeVerified=>false;
    public bool NormalCreateBinApproved=>false;
    public bool CompleteNativeClosureProven=>false;
    public bool TestLevelSelectorEnabled=>false;
    internal DiscLayout Layout=>_artifacts.Layout;
    internal string SourceCuePath=>_artifacts.SourceCuePath;
    internal byte[] CopyCueBytes()=>_artifacts.CopyCueBytes();
    internal byte[] CopyWad()=>_artifacts.CopyWad();
    internal byte[] CopyExecutable()=>_artifacts.CopyExecutable();
    internal byte[] CopyRoot()=>_artifacts.CopyRoot();
    public static async Task<NativeDarkHollowTwoWizardResearchCandidatePlan> CaptureAsync(string originalImagePath,string originalCuePath,
        LevelCatalog catalog,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalImagePath);ArgumentException.ThrowIfNullOrWhiteSpace(originalCuePath);
        string path=Path.GetFullPath(originalImagePath);
        var cue=NativeFreeFlightResearchSourceCue.Capture(originalCuePath,path,ImageName);
        var dependencies=await NativeWizardResearchArtifactPlan.CaptureDependenciesAsync(path,catalog,token);
        var firstSource=NativeDarkHollowWizardAppendSource.Capture(path,catalog);
        var first=firstSource.CompileActive(firstSource.EmptyOwners(),[new("z")]);
        var reopened=NativeDarkHollowWizardAppendSource.Capture(path,catalog);
        var append=reopened.CompileActive(reopened.RestoreOwners(first.ProposedOwners.Serialize()),[new("a"),new("z")]);
        var placement=NativeDarkHollowWizardAppendPlacementSource.Capture(path,append).Compile([
            new("a",new(86688,25248,10445,87712,25248)),new("z",new(92832,25248,10445,91808,25248))]);
        var resources=await NativeDarkHollowWizardResourcePlan.CompileWithAppendPlacementAsync(path,dependencies.Inventory,dependencies.Player,
            dependencies.Sound,dependencies.Texture,dependencies.Descriptor,append,placement,token);
        var overlay=NativeDarkHollowWizardOverlayPlan.Compile(dependencies.Behavior,resources,dependencies.Particles);
        var artifacts=await NativeWizardResearchArtifactPlan.CaptureAsync(path,cue,resources,overlay,token);
        return new(append,placement,resources,overlay,artifacts);
    }
    public Task RequireFreshAsync(CancellationToken token=default)=>_artifacts.RequireFreshAsync(token);
    internal void RequireStageReversion()
    {
        if(!Appended.ProposedOwners.Serialize().AsSpan().SequenceEqual(_owners))throw new InvalidDataException("Two-Wizard proposed owner identities changed.");
        Appended.VerifyOriginalAndFinal(Appended.CopyOriginalScene(),Appended.CopyScene());
        Placement.RequireOriginalAndFinal(Appended.CopyScene(),Placement.CopyScene());
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
        FinalExecutableEndLba,Entries,proposedOwners=JsonSerializer.Deserialize<JsonElement>(_owners),proposedOwnersSha256=Hash(_owners),
        acceptedProjectHistory=false,appendGraph=Appended.GraphFingerprint,placement=Placement,
        resourcePlan=Resources.GraphFingerprint,Resources.FinalDataSha256,overlaySha256=Overlay.OverlaySha256,
        executableSha256=Reward.ExecutableSha256,rewardSelector=Reward.RewardSelector,nativeRewardValue=Reward.NativeGemValue,
        treasureTarget=Reward.TreasureTargetAfter,Memory,wholeImagePhysicalReadback=true,completeOriginalWadInverseVerified=true,
        original128RowsPreserved=true,researchOnly=true,TestLevelSelectorEnabled,CompleteNativeClosureProven,RuntimeVerified,NormalCreateBinApproved,
        exceptionRollbackPublication=true,crashAtomicPublication=false,hostileFilesystemRaceSafe=false,
        pending=new[]{"full main-helper global/GTE and transient stack behavior","both native body/route/bolt trajectories",
            "cold boot and normal portal entry","simultaneous intact native lightning/hurt/effects/sound",
            "both native deaths, independent10-value gems, +20 collection and checkpoint/durable persistence",
            "native postcollection RNG/companion branch","nearby native actors/collision/exit/death/re-entry","accepted project history and ordinary editor export"}
    },new JsonSerializerOptions{WriteIndented=true});
    private static string Hash(ReadOnlySpan<byte> bytes)=>NativeDarkHollowWizardResearchCandidatePlan.Hash(bytes);
}

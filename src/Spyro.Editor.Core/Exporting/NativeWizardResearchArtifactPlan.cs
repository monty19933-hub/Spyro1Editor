using System.Runtime.CompilerServices;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeTwoWizardResearchSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>Actual private source-issued dependencies, not metadata or publication authority.</summary>
internal sealed record NativeWizardResearchDependencies(NativeMobySourceInventory Inventory,
    NativePlayerAnimationMergePlan Player,NativeModelSoundMergePlan Sound,NativeDarkHollowWizardTexturePlan Texture,
    NativeDarkHollowWizardParticlePlan Descriptor,NativeDarkHollowWizardBehaviorSource Behavior,
    NativeDarkHollowWizardParticleBehaviorSource Particles);

/// <summary>
/// Legacy one/two-Wizard source/profile wrapper. Shared packed-image mechanics
/// cannot select its resource, reward, CUE or publication ancestry.
/// </summary>
internal sealed class NativeWizardResearchArtifactPlan
{
    private readonly NativePackedResearchImageOwner _physical;
    private readonly NativeFreeFlightResearchSourceCue _cue;
    private readonly NativeDarkHollowWizardResourcePlan _resources;
    private readonly NativeDarkHollowWizardOverlayPlan _overlay;
    private NativeWizardResearchArtifactPlan(NativeFreeFlightResearchSourceCue cue,NativeDarkHollowWizardResourcePlan resources,
        NativeDarkHollowWizardOverlayPlan overlay,NativeDarkHollowWizardDeathRewardPlan reward,NativePackedResearchImageOwner physical)
    {_cue=cue;_resources=resources;_overlay=overlay;Reward=reward;_physical=physical;RequireStageReversion();}
    internal string SourceImagePath=>_physical.SourceImagePath;
    internal string SourceCuePath=>_cue.CuePath;
    internal string SourceCueSha256=>_cue.SourceSha256;
    internal DiscLayout Layout=>_physical.Layout;
    internal string OriginalWadSha256=>_physical.OriginalWadSha256;
    internal string WadSha256=>_physical.WadSha256;
    internal string RootSha256=>_physical.RootSha256;
    internal int GrowthBytes=>_physical.GrowthBytes;
    internal int RelocatedExecutableLba=>_physical.RelocatedExecutableLba;
    internal int FinalExecutableEndLba=>_physical.FinalExecutableEndLba;
    internal IReadOnlyList<NativeWizardResearchEntry> Entries=>_physical.Entries;
    internal NativeDarkHollowWizardDeathRewardPlan Reward{get;}
    internal byte[] CopyCueBytes()=>_cue.CopyOutputBytes();
    internal byte[] CopyWad()=>_physical.CopyWad();
    internal byte[] CopyExecutable()=>_physical.CopyExecutable();
    internal byte[] CopyRoot()=>_physical.CopyRoot();
    internal static async Task<NativeWizardResearchDependencies> CaptureDependenciesAsync(string path,LevelCatalog catalog,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(catalog);
        var inventory=NativeMobySourceInventory.Capture(path,catalog);
        var playerSource=await NativePlayerAnimationSource.CaptureAsync(path,token);
        var player=await playerSource.CompileMergeAsync(path,33,12,[8],token);
        var soundSource=await NativeGlobalSoundSource.CaptureAsync(path,token);
        var sound=await soundSource.CompileModelSlotMergeAsync(path,inventory,33,12,0x11B,[0,1,2],
            [new(0x1F,NativeGlobalSoundMappingPolicy.ExplicitRebindUnresolvedConsumers),new(0x26)],token);
        var texture=NativeDarkHollowWizardTexturePlan.Capture(path);
        var descriptor=await NativeDarkHollowWizardParticlePlan.CaptureAsync(path,texture,token);
        return new(inventory,player,sound,texture,descriptor,NativeDarkHollowWizardBehaviorSource.Capture(path),
            NativeDarkHollowWizardParticleBehaviorSource.Capture(path));
    }

    internal static async Task<NativeWizardResearchArtifactPlan> CaptureAsync(string path,NativeFreeFlightResearchSourceCue cue,
        NativeDarkHollowWizardResourcePlan resources,NativeDarkHollowWizardOverlayPlan overlay,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(cue);
        ArgumentNullException.ThrowIfNull(resources);ArgumentNullException.ThrowIfNull(overlay);
        if(resources.SceneRevision==null||!ReferenceEquals(resources.SceneRevision,overlay.SceneRevision)||
            !resources.CopyFinalLevelData().AsSpan().SequenceEqual(overlay.CopyFinalLevelData()))
            throw new InvalidDataException("Physical research owners must retain the actual same resource/scene ancestor.");
        var reward=NativeDarkHollowWizardDeathRewardPlan.Capture(path,overlay);
        var physical=NativePackedResearchImageOwner.CaptureWizard(path,resources,overlay,reward);
        var plan=new NativeWizardResearchArtifactPlan(cue,resources,overlay,reward,physical);
        await plan.RequireFreshAsync(token);return plan;
    }
    internal async Task RequireFreshAsync(CancellationToken token=default)
    {_cue.RequireFresh();await _physical.RequireFreshAsync(token);_cue.RequireFresh();}
    internal void RequireStageReversion()
    {
        var data=_resources.CopyFinalLevelData();_resources.RequireFinal(data);
        _overlay.RequireFinal(_overlay.CopyOverlay(),_overlay.CopyExecutable(),data);
        byte[] executable=_physical.CopyExecutable();Reward.RequireExecutable(executable);
        if(!Reward.RevertToOverlayExecutable(executable).AsSpan().SequenceEqual(_overlay.CopyExecutable()))
            throw new InvalidDataException("Research reward did not revert exactly to source-issued overlay EXE.");
        _physical.RequireOriginalWadReversion();
    }
    internal void WriteNewImage(string outputPath,CancellationToken token=default)=>_physical.WriteNewImage(outputPath,token);
    internal (string Hash,int Changed) VerifyImage(string path)=>_physical.VerifyImage(path);
    internal byte[] ExpectedSector(int lba,byte[] before)=>_physical.ExpectedSector(lba,before);
}

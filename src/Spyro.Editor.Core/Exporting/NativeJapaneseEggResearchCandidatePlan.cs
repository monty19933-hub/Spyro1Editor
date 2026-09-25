using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeJapaneseEggResearchSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Fixed original-mode1 two-F5 visual RESEARCH candidate. Only the source-issued
/// placement factory can select rows. This is not an ordinary export, collector,
/// persistent inventory, independently coloured variant or runtime certificate.
/// </summary>
public sealed class NativeJapaneseEggResearchCandidatePlan
{
    private readonly NativePackedResearchImageOwner _physical;
    private readonly NativeFreeFlightResearchSourceCue _cue;
    private NativeJapaneseEggResearchCandidatePlan(string japan,NativeFreeFlightResearchSourceCue cue,
        NativeJapaneseEggArtisansPlacementPlan placement,NativeJapaneseEggArtisansIntegrationPlan integration,
        NativeJapaneseEggRenderAdmissionPlan render,NativeJapaneseEggMemoryReport memory,NativePackedResearchImageOwner physical)
    {
        JapaneseSourceImagePath=japan;_cue=cue;Placement=placement;Integration=integration;RenderAdmission=render;Memory=memory;_physical=physical;
        RequireStageReversion();
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{Profile,JapaneseSourceImagePath,SourceImagePath,
            sourceUsaSha256=NativeMobySourceInventory.OriginalImageSha256,
            sourceJapanSha256="1bcf4353d6a4d4d0db152d2ebd60879ef85516feaf170f364ff419c54f599ea7",
            sourceCuePath=SourceCuePath,sourceCueSha256=cue.SourceSha256,placement=placement.PlanSha256,
            integration=integration.PlanSha256,render=render.Consumers,memory=memory.Values,WadSha256,RootSha256,
            GrowthBytes,RelocatedExecutableLba,Entries,RuntimeVerified,NormalCreateBinApproved}));
    }
    public const string Profile="native-artisans-two-japanese-eggs-visual-research-v1";
    internal const string ImageName="artisans-native-japanese-eggs-visual-r01.bin";
    internal const string CueName="artisans-native-japanese-eggs-visual-r01.cue";
    internal const string ReceiptName="native-japanese-eggs-visual.receipt.json";
    internal static string[] OutputNames=>[ImageName,ReceiptName,CueName];
    public string SourceImagePath=>_physical.SourceImagePath;
    public string JapaneseSourceImagePath{get;}
    public string PlanSha256{get;}
    public string OriginalWadSha256=>_physical.OriginalWadSha256;
    public string WadSha256=>_physical.WadSha256;
    public string RootSha256=>_physical.RootSha256;
    public int GrowthBytes=>_physical.GrowthBytes;
    public int RelocatedExecutableLba=>_physical.RelocatedExecutableLba;
    public int FinalExecutableEndLba=>_physical.FinalExecutableEndLba;
    public IReadOnlyList<NativeWizardResearchEntry> Entries=>_physical.Entries;
    public NativeJapaneseEggArtisansPlacementPlan Placement{get;}
    public NativeJapaneseEggArtisansIntegrationPlan Integration{get;}
    public NativeJapaneseEggRenderAdmissionPlan RenderAdmission{get;}
    internal NativeJapaneseEggMemoryReport Memory{get;}
    public bool ResearchOnly=>true;
    public bool RuntimeVerified=>false;
    public bool NormalCreateBinApproved=>false;
    public bool CompleteNativeClosureProven=>false;
    public bool TestLevelSelectorEnabled=>false;
    public bool PickupEnabled=>false;
    public bool PersistentSaveChanges=>false;
    public bool IndependentColorVariants=>false;
    internal DiscLayout Layout=>_physical.Layout;
    internal string SourceCuePath=>_cue.CuePath;
    internal byte[] CopyCueBytes()=>_cue.CopyOutputBytes();
    internal byte[] CopyWad()=>_physical.CopyWad();
    internal byte[] CopyExecutable()=>_physical.CopyExecutable();
    internal byte[] CopyRoot()=>_physical.CopyRoot();

    public static async Task<NativeJapaneseEggResearchCandidatePlan> CaptureAsync(string japaneseImagePath,string originalImagePath,
        string originalCuePath,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentException.ThrowIfNullOrWhiteSpace(japaneseImagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalImagePath);ArgumentException.ThrowIfNullOrWhiteSpace(originalCuePath);
        string japanPath=Path.GetFullPath(japaneseImagePath),usaPath=Path.GetFullPath(originalImagePath);
        var cue=NativeFreeFlightResearchSourceCue.Capture(originalCuePath,usaPath,ImageName);
        using var japan=NativeRegionalSourceSession.Open(japanPath,NativeDiscRevision.JapanScps10083);
        using var usa=NativeRegionalSourceSession.Open(usaPath,NativeDiscRevision.UsaScus94228);
        var placement=NativeJapaneseEggArtisansPlacementPlan.Capture(usa);
        var integration=NativeJapaneseEggArtisansIntegrationPlan.Capture(japan,usa,placement.Placements);
        placement.RequireIntegration(integration);
        var render=NativeJapaneseEggRenderAdmissionPlan.Capture(usa,integration);
        var memory=NativeJapaneseEggMemoryReport.Capture(usa,integration);
        var physical=NativePackedResearchImageOwner.CaptureEgg(usaPath,integration,render,memory);
        var plan=new NativeJapaneseEggResearchCandidatePlan(japanPath,cue,placement,integration,render,memory,physical);
        japan.VerifyUnchanged();usa.VerifyUnchanged();await plan.RequireFreshAsync(token);return plan;
    }
    public async Task RequireFreshAsync(CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();_cue.RequireFresh();await _physical.RequireFreshAsync(token);
        using var jp=NativeRegionalSourceSession.Open(JapaneseSourceImagePath,NativeDiscRevision.JapanScps10083);
        jp.VerifyUnchanged();_cue.RequireFresh();token.ThrowIfCancellationRequested();
    }
    internal void RequireStageReversion()
    {
        Placement.RequireIntegration(Integration);
        if(!ReferenceEquals(RenderAdmission.Integration,Integration)||!RenderAdmission.SourceCertified||
            Memory.IntegrationPlanSha256!=Integration.PlanSha256||!Memory.ModeledLoadExtentsFitProven||
            !Memory.SynchronousVisualStackFitProven||Integration.Rows.Count!=2||
            Integration.Rows.Any(r=>r.Placement.ProfileIndex!=1)||GrowthBytes!=0x1800||RelocatedExecutableLba!=53878||FinalExecutableEndLba!=54082)
            throw new InvalidDataException("Fixed visual research placement/render/memory/physical ancestry changed.");
        Integration.RequireFinal(Integration.CopyOverlay(),CopyExecutable(),Integration.CopyLevelData());
        _physical.RequireOriginalWadReversion();
    }
    internal void WriteNewImage(string path,CancellationToken token=default)=>_physical.WriteNewImage(path,token);
    internal (string Hash,int Changed) VerifyImage(string path)=>_physical.VerifyImage(path);
    internal byte[] ExpectedSector(int lba,byte[] before)=>_physical.ExpectedSector(lba,before);
    internal byte[] BuildReceipt((string Hash,int Changed) verified)=>JsonSerializer.SerializeToUtf8Bytes(new
    {
        profile=Profile,PlanSha256,SourceImagePath,JapaneseSourceImagePath,
        sourceUsaSha256=NativeMobySourceInventory.OriginalImageSha256,
        sourceJapanSha256="1bcf4353d6a4d4d0db152d2ebd60879ef85516feaf170f364ff419c54f599ea7",
        sourceCuePath=SourceCuePath,sourceCueSha256=_cue.SourceSha256,outputImageSha256=verified.Hash,
        changedRawSectors=verified.Changed,outputCueSha256=Hash(CopyCueBytes()),OriginalWadSha256,WadSha256,RootSha256,
        GrowthBytes,RelocatedExecutableLba,FinalExecutableEndLba,Entries,placement=Placement,
        integration=Integration.PlanSha256,Integration.Rows,Integration.LevelDataSha256,Integration.OverlaySha256,Integration.ExecutableSha256,
        renderConsumers=RenderAdmission.Consumers,opaqueRenderMode=1,sharedModelColors=true,
        memory=Memory.Values,Memory.ModeledLoadExtentsFitProven,Memory.SynchronousVisualStackFitProven,
        Memory.FullRuntimePeakProven,wholeImagePhysicalReadback=true,completeOriginalWadInverseVerified=true,
        original174RowsPreserved=true,acceptedProjectHistory=false,ResearchOnly,TestLevelSelectorEnabled,
        CompleteNativeClosureProven,RuntimeVerified,NormalCreateBinApproved,PickupEnabled,PersistentSaveChanges,IndependentColorVariants,
        exceptionRollbackPublication=true,crashAtomicPublication=false,hostileFilesystemRaceSafe=false,
        pending=new[]{"native lit pixels and appearance","both visible bob/turn instances and nearby actor behavior",
            "camera/frustum/GTE and full renderer/IRQ peak behavior","cold boot, ordinary entry, death/re-entry and reset",
            "independent model colour variants","pickup, required100-percent inventory and durable save integration","normal editor/export approval"}
    },new JsonSerializerOptions{WriteIndented=true});
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}

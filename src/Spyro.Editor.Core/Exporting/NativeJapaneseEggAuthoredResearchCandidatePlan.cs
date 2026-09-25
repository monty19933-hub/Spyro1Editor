using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeJapaneseEggAuthoredResearchSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Closed two-color diagnostic Artisans research profile. Palette values and
/// original-ground placements are fixed by source-owning code, not caller
/// metadata. This is not a corrected native palette, normal export, collector,
/// persistence/inventory integration or native pixel/gameplay certificate.
/// </summary>
public sealed class NativeJapaneseEggAuthoredResearchCandidatePlan
{
    private readonly NativePackedResearchImageOwner _physical;
    private readonly NativeFreeFlightResearchSourceCue _cue;
    private NativeJapaneseEggAuthoredResearchCandidatePlan(string japan,NativeFreeFlightResearchSourceCue cue,
        NativeJapaneseEggArtisansPlacementPlan placement,NativeJapaneseEggAuthoredClassesPlan composition,
        NativeJapaneseEggRenderAdmissionPlan render,NativeJapaneseEggAuthoredMemoryReport memory,NativePackedResearchImageOwner physical)
    {
        JapaneseSourceImagePath=japan;_cue=cue;Placement=placement;Composition=composition;RenderAdmission=render;Memory=memory;_physical=physical;
        RequireStageReversion();
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{Profile,JapaneseSourceImagePath,SourceImagePath,
            sourceUsaSha256=NativeMobySourceInventory.OriginalImageSha256,sourceJapanSha256=JapanHash,
            sourceCuePath=SourceCuePath,sourceCueSha256=cue.SourceSha256,placement=placement.PlanSha256,
            composition=composition.PlanSha256,reservation=composition.Reservation.PlanSha256,render=render.Consumers,memory=memory.Values,
            WadSha256,RootSha256,GrowthBytes,RelocatedExecutableLba,Entries,IndependentColorVariants,RuntimeVerified,NormalCreateBinApproved}));
    }
    public const string Profile="native-artisans-two-japanese-eggs-authored-colors-research-v1";
    internal const string ImageName="artisans-native-japanese-eggs-authored-colors-r01.bin";
    internal const string CueName="artisans-native-japanese-eggs-authored-colors-r01.cue";
    internal const string ReceiptName="native-japanese-eggs-authored-colors.receipt.json";
    private const string JapanHash="1bcf4353d6a4d4d0db152d2ebd60879ef85516feaf170f364ff419c54f599ea7";
    internal static string[] OutputNames=>[ImageName,ReceiptName,CueName];
    private static uint Near(int index)=>index==0?0x332211u:0xCCBBAAu;
    private static uint Far(int index)=>index==0?0x665544u:0xFFEEDDu;
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
    public NativeJapaneseEggAuthoredClassesPlan Composition{get;}
    public NativeJapaneseEggRenderAdmissionPlan RenderAdmission{get;}
    internal NativeJapaneseEggAuthoredMemoryReport Memory{get;}
    public bool ResearchOnly=>true;
    public bool IndependentColorVariants=>true;
    public bool LitPixelsVerified=>false;
    public bool RuntimeVerified=>false;
    public bool NormalCreateBinApproved=>false;
    public bool CompleteNativeClosureProven=>false;
    public bool TestLevelSelectorEnabled=>false;
    public bool PickupEnabled=>false;
    public bool PersistentSaveChanges=>false;
    internal DiscLayout Layout=>_physical.Layout;
    internal string SourceCuePath=>_cue.CuePath;
    internal byte[] CopyCueBytes()=>_cue.CopyOutputBytes();
    internal byte[] CopyWad()=>_physical.CopyWad();
    internal byte[] CopyExecutable()=>_physical.CopyExecutable();
    internal byte[] CopyRoot()=>_physical.CopyRoot();

    public static async Task<NativeJapaneseEggAuthoredResearchCandidatePlan> CaptureAsync(string japaneseImagePath,string originalImagePath,
        string originalCuePath,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentException.ThrowIfNullOrWhiteSpace(japaneseImagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalImagePath);ArgumentException.ThrowIfNullOrWhiteSpace(originalCuePath);
        string japanPath=Path.GetFullPath(japaneseImagePath),usaPath=Path.GetFullPath(originalImagePath);
        var cue=NativeFreeFlightResearchSourceCue.Capture(originalCuePath,usaPath,ImageName);
        using var japan=NativeRegionalSourceSession.Open(japanPath,NativeDiscRevision.JapanScps10083);
        using var usa=NativeRegionalSourceSession.Open(usaPath,NativeDiscRevision.UsaScus94228);
        try
        {
            var placement=NativeJapaneseEggArtisansPlacementPlan.Capture(usa);
            var integration=NativeJapaneseEggArtisansIntegrationPlan.Capture(japan,usa,placement.Placements);
            placement.RequireIntegration(integration);
            var render=NativeJapaneseEggRenderAdmissionPlan.Capture(usa,integration);
            var asset=NativeJapaneseEggModelAssetPlan.Capture(japan,usa);var colors=NativeJapaneseEggRgbEditSource.Capture(asset,usa);
            NativeJapaneseEggRgbEditPlan Edit(int index)=>colors.CreateAuthored(Enumerable.Repeat(Near(index),70).ToArray(),Enumerable.Repeat(Far(index),70).ToArray());
            var reservation=NativeJapaneseEggAuthoredClassReservation.Capture(usa);
            var composition=NativeJapaneseEggAuthoredClassesPlan.Capture(usa,reservation,integration,Edit(0),Edit(1));
            var memory=NativeJapaneseEggAuthoredMemoryReport.Capture(usa,composition);
            var physical=NativePackedResearchImageOwner.CaptureAuthoredEgg(usaPath,composition,render,memory);
            var plan=new NativeJapaneseEggAuthoredResearchCandidatePlan(japanPath,cue,placement,composition,render,memory,physical);
            await plan.RequireFreshAsync(token);return plan;
        }
        finally { japan.VerifyUnchanged();usa.VerifyUnchanged(); }
    }
    public async Task RequireFreshAsync(CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();_cue.RequireFresh();await _physical.RequireFreshAsync(token);
        using var jp=NativeRegionalSourceSession.Open(JapaneseSourceImagePath,NativeDiscRevision.JapanScps10083);
        jp.VerifyUnchanged();_cue.RequireFresh();token.ThrowIfCancellationRequested();
    }
    internal void RequireStageReversion()
    {
        var integration=Composition.Integration;Placement.RequireIntegration(integration);
        if(!Composition.SourceCertified||!ReferenceEquals(RenderAdmission.Integration,integration)||!RenderAdmission.SourceCertified||
            Memory.PlanSha256!=Composition.PlanSha256||Memory.FinalDataSha256!=Composition.FinalDataSha256||
            Memory.FinalOverlaySha256!=Composition.FinalOverlaySha256||!Memory.ModeledLoadExtentsFitProven||!Memory.SynchronousVisualStackFitProven||
            Memory.Values.SceneEnd!=Composition.Projection.ProjectedSceneReadEnd||Memory.Values.NormalGap!=Composition.Projection.ProjectedNormalGap||
            integration.Rows.Count!=2||integration.Rows.Any(r=>r.Placement.ProfileIndex!=1)||
            GrowthBytes!=0x3000||RelocatedExecutableLba!=53881||FinalExecutableEndLba!=54085||
            !Composition.Bindings.Select(b=>b.AuthoredDestinationClass).SequenceEqual(new ushort[]{0x1E9,0x1F2}))
            throw new InvalidDataException("Fixed authored-color placement/class/render/final-memory/physical ancestry changed.");
        for(int i=0;i<2;i++)
        {
            var edit=Composition.Bindings[i].Edit;
            if(edit.Origin!=NativeJapaneseEggPaletteOrigin.Authored||edit.Fields.Count!=140||
                edit.Fields.Any(f=>f.FinalRgb!=(f.Far?Far(i):Near(i))))
                throw new InvalidDataException("Research palette differs from the fixed explicit diagnostic authored RGB choices.");
        }
        if(!integration.RevertDataExact(Composition.RevertDataExact(Composition.CopyLevelData())).AsSpan().SequenceEqual(integration.CopyOriginalLevelData())||
            !integration.RevertOverlayExact(Composition.RevertOverlayExact(Composition.CopyOverlay())).AsSpan().SequenceEqual(integration.CopyOriginalOverlay())||
            !integration.RevertExecutableExact(CopyExecutable()).AsSpan().SequenceEqual(integration.CopyOriginalExecutable()))
            throw new InvalidDataException("Authored research inverse lost complete original level/overlay/EXE owners.");
        _physical.RequireOriginalWadReversion();
    }
    internal void WriteNewImage(string path,CancellationToken token=default)=>_physical.WriteNewImage(path,token);
    internal (string Hash,int Changed) VerifyImage(string path)=>_physical.VerifyImage(path);
    internal byte[] ExpectedSector(int lba,byte[] before)=>_physical.ExpectedSector(lba,before);
    internal byte[] BuildReceipt((string Hash,int Changed) verified)=>JsonSerializer.SerializeToUtf8Bytes(new
    {
        profile=Profile,PlanSha256,SourceImagePath,JapaneseSourceImagePath,sourceUsaSha256=NativeMobySourceInventory.OriginalImageSha256,
        sourceJapanSha256=JapanHash,sourceCuePath=SourceCuePath,sourceCueSha256=_cue.SourceSha256,outputImageSha256=verified.Hash,
        changedRawSectors=verified.Changed,outputCueSha256=Hash(CopyCueBytes()),OriginalWadSha256,WadSha256,RootSha256,
        GrowthBytes,RelocatedExecutableLba,FinalExecutableEndLba,Entries,placement=Placement,
        integration=Composition.Integration.PlanSha256,composition=Composition.PlanSha256,reservation=Composition.Reservation.PlanSha256,
        LevelDataSha256=Composition.FinalDataSha256,OverlaySha256=Composition.FinalOverlaySha256,ExecutableSha256=Hash(CopyExecutable()),
        Rows=Composition.Integration.Rows.Select(r=>new{r.NativeIndex,r.RowSceneOffset,r.PrivateSceneOffset,r.Placement,
            AuthoredDestinationClass=Composition.Bindings.Single(b=>b.RowId==r.Placement.Id).AuthoredDestinationClass}),
        bindings=Composition.Bindings.Select((b,i)=>new{b.RowId,b.SourceActorId,b.AuthoredDestinationClass,b.RegistrationSlot,b.RootFieldOffset,
            b.ClassFieldOffset,b.PackageDataOffset,b.ByteLength,b.ProjectedRoot,b.RowSceneOffset,b.ProjectedRowAddress,b.ProjectedPrivateAddress,
            b.PaletteOrigin,VariantIdentity=b.Edit.VariantIdentity,PackageSha256=b.Edit.FinalPackageSha256,nearRgb=Near(i),farRgb=Far(i)}),
        renderConsumers=RenderAdmission.Consumers,opaqueRenderMode=1,sharedModelColors=false,paletteMeaning="explicit diagnostic authored RGB; not recovered or corrected native presets",
        nativeRendererUnchanged=Composition.NativeRendererUnchanged,memory=Memory.Values,Memory.ModeledLoadExtentsFitProven,Memory.SynchronousVisualStackFitProven,
        Memory.FullRuntimePeakProven,wholeImagePhysicalReadback=true,completeOriginalWadInverseVerified=true,
        original174RowsPreserved=true,acceptedProjectHistory=false,ResearchOnly,TestLevelSelectorEnabled,IndependentColorVariants,LitPixelsVerified,
        CompleteNativeClosureProven,RuntimeVerified,NormalCreateBinApproved,PickupEnabled,PersistentSaveChanges,
        Composition.GenericEffectsApproved,Composition.DynamicSpawnApproved,Composition.GlobalUnusedNamespaceProven,
        exceptionRollbackPublication=true,crashAtomicPublication=false,hostileFilesystemRaceSafe=false,
        pending=new[]{"native GPU/pixel appearance of both explicit diagnostic palettes","both animated instances and nearby actor behavior",
            "camera/frustum/GTE and full renderer/IRQ peak behavior","cold boot, ordinary entry, death/re-entry and reset",
            "generic retirement/effect class truncation and class-indexed callback semantics remain unapproved",
            "pickup, required100-percent inventory and durable save integration","normal editor/export approval"}
    },new JsonSerializerOptions{WriteIndented=true});
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}

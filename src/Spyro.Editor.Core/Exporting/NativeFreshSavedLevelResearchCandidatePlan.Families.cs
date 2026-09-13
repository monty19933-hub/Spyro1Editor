using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

public sealed partial class NativeFreshSavedLevelResearchCandidatePlan
{
    private const string FamilyVariant="schema3-two-retail-materials-wizard-grass-p2-n32";
    private readonly NativeFreshSavedMobyFamilyCompilation? _familyCompilation;
    internal NativeFreshSavedMobyFamilyCompilation? FamilyCompilation=>_familyCompilation;

    private NativeFreshSavedLevelResearchCandidatePlan(NativeFreshSavedMobyFamilyCompilation family,
        NativeFreshResearchSourceOwners owners,NativeId65FreshDiscVerification measured)
    {
        Saved=family.Saved;_familyCompilation=family;_mixedDisc=family.Disc;
        SourceImagePath=owners.ImagePath;SourceCuePath=owners.CuePath;
        _imageIdentity=owners.ImageIdentity!;_cueIdentity=owners.CueIdentity!;
        _sourceCue=owners.CueSnapshot!.ToArray();SourceCueSha256=Hash(_sourceCue);
        _outputCue=Encoding.UTF8.GetBytes("FILE \""+ImageName+"\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        _expected=measured;_canonical=NativeFreshLevelDocumentCodec.SerializeCanonical(Saved.Document);
        RequireFamilyAncestry(Saved,family);
        var disc=family.Disc;var exe=disc.ExecutableAncestor;var body=family.BaseBody;var resource=body.MixedResourceAncestor!;
        using var document=JsonDocument.Parse(_canonical);var snapshot=Saved.Snapshot;
        var revision=new{projectId=snapshot.ProjectId,root=snapshot.RootPath,path=snapshot.Path,
            rawSha256=snapshot.ContentSha256,manifestSha256=snapshot.ManifestSha256,
            canonicalSha256=Hash(_canonical),canonical=document.RootElement};
        var lineage=new{
            discPlan=disc.PlanSha256,executablePlan=exe.PlanSha256,familyCompositionPlan=family.Composition.PlanSha256,
            baseBodyPlan=body.PlanSha256,baseSubdocumentSha256=Hash(NativeFreshLevelDocumentCodec.SerializeCanonical(family.BaseDocument)),
            hostPlan=body.HostAncestor.PlanSha256,scenePlan=body.HostAncestor.SceneAncestor.PlanSha256,
            mixedResourcePlan=resource.PlanSha256,wizardInitialClearancePlan=family.WizardClearance.PlanSha256,
            clearanceScope="retained-wizard-initial-pose-only; no generic-family collision certificate",
            floor=Saved.Document.Base,placedCount=Saved.Document.Placements.Count,nativeNominalCapacity=32,
            families=family.Composition.Placements.Select((p,i)=>new{p.EditorId,
                sourceFamilyPlan=p.WizardAsset?.PlanSha256??p.PassiveAsset!.PlanSha256,passiveHostPlan=p.Dispatch?.PlanSha256,
                authoredPlacement=document.RootElement.GetProperty("placements")[i]}).ToArray(),
            materials=resource.MaterialSources.Select((s,i)=>new{slot=i,s.Handle,s.SourceImageSha256,s.SourceWadEntry,s.SourceTextureId,
                s.SourceDataSha256,sourceRecordSha256=s.Layout.RecordSha256}).ToArray(),
            resource.UploadSha256,resource.DataSha256,bodySha256=disc.BodySha256,sceneSha256=family.Composition.SceneSha256,
            overlaySha256=disc.OverlaySha256,executableSha256=disc.ExecutableSha256,wadDirectorySha256=disc.WadDirectorySha256,
            isoRootSha256=disc.IsoRootSha256,disc.FinalExecutableLba,disc.FinalWadByteLength,
            staticFamilyCensus=exe.FamilyCensus,upstreamPendingOwners=disc.PendingOwners};
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            profile=ProfileId,variant=FamilyVariant,saved=revision,sourceImageSha256=NativeId65FreshDiscSource.OriginalSha256,
            SourceImagePath,SourceCuePath,SourceCueSha256,imageIdentity=_imageIdentity,cueIdentity=_cueIdentity,
            lineage,expectedImageSha256=measured.OutputSha256,expectedImageByteLength=ExpectedImageByteLength,
            changedRawSectors=measured.ChangedSectors,cueSha256=Hash(_outputCue),
            savedRevisionBound=true,nativePersistentSaveOwnerProven=false,runtimeVerified=false,
            normalCreateBinApproved=false,completeNativeDependencyClosureProven=false}));
        _receipt=JsonSerializer.SerializeToUtf8Bytes(new{
            schema=1,profile=ProfileId,variant=FamilyVariant,planSha256=PlanSha256,saved=revision,
            SourceImagePath,SourceCuePath,SourceCueSha256,sourceImageSha256=NativeId65FreshDiscSource.OriginalSha256,lineage,
            imageFile=ImageName,imageSha256=measured.OutputSha256,imageByteLength=ExpectedImageByteLength,
            changedRawSectors=measured.ChangedSectors,cueFile=CueName,cueSha256=Hash(_outputCue),receiptFile=ReceiptName,
            savedRevisionBound=true,nativePersistentSaveOwnerProven=false,runtimeVerified=false,
            normalCreateBinApproved=false,completeNativeDependencyClosureProven=false,
            hqRuntimeProven=false,audioRuntimeProven=false,peakRuntimeMemoryProven=false,checkpointOrForeignStateSupported=false
        },new JsonSerializerOptions{WriteIndented=true});
        if(_receipt.Length is <1 or >65536)throw new InvalidDataException("Complete saved-family receipt exceeds companion bound; no evidence is truncated.");
    }

    private static async Task<NativeFreshSavedLevelResearchCandidatePlan> CaptureFamilyAsync(string originalBin,string sourceCue,
        NativeFreshSavedLevelDocumentReadContext saved,CancellationToken token)
    {
        _=NativeFreshSavedMobyFamilyCompilation.ProjectBaseDocument(saved.Document);
        NativeFreshDocumentStorage.RequireAggregateCurrent(saved.Snapshot,token);
        var owners=new NativeFreshResearchSourceOwners(Path.GetFullPath(originalBin),Path.GetFullPath(sourceCue));
        NativeFreshSavedLevelResearchCandidatePlan? result=null;Exception? primary=null;
        try
        {
            owners.Open(token);await NativeId65FreshDiscSource.RequireWholeOriginalAsync(owners.Bin!,token);
            var family=await NativeFreshSavedMobyFamilyCompilation.CaptureAsync(owners.ImagePath,saved,token);
            owners.Bin!.Position=0;var measured=NativeId65FreshDiscStreaming.Measure(family.Disc,owners.Bin,token);
            result=new(family,owners,measured);token.ThrowIfCancellationRequested();
        }
        catch(Exception error){primary=error;}
        CompleteOperation(owners,saved,primary);token.ThrowIfCancellationRequested();return result!;
    }
    internal void RequireFamilyAncestry(NativeFreshSavedLevelDocumentReadContext saved,NativeFreshSavedMobyFamilyCompilation family)
    {
        if(!ReferenceEquals(saved,Saved)||family is null||!ReferenceEquals(family,_familyCompilation)||
            _material!=null||_mixedClearance!=null||!ReferenceEquals(_mixedDisc,family.Disc))
            throw new InvalidDataException("Saved-family Candidate requires its actual saved context and full family compilation, not an equal or projected substitute.");
        family.RequireAncestry(saved);
        if(family.SourceImagePath!=SourceImagePath||!_canonical.AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(saved.Document)))
            throw new InvalidDataException("Saved-family Candidate source path or complete canonical document changed.");
    }
}

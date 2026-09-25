using System.Globalization;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

public sealed partial class NativeFreshSavedLevelResearchCandidatePlan
{
    private const string TiledFamilyVariant="schema5-four1536-tile-r03-wizard-grass-locked-chest-p9-n32";
    // Independent complete physical child reference, not a caller hash or a
    // retained parent runtime certificate. The closed exact recipe is still
    // validated in full, including editor identity, before source I/O.
    private const string TiledFamilyImageSha256="689fcf296e26d635d9c5ce6fc3fc687c2b47decce10ade2a0903f0c6bd877192";
    private readonly NativeFreshSavedTiledFamilyCompilation? _tiledFamilyCompilation;
    internal NativeFreshSavedTiledFamilyCompilation? TiledFamilyCompilation=>_tiledFamilyCompilation;

    private NativeFreshSavedLevelResearchCandidatePlan(NativeFreshSavedTiledFamilyCompilation family,
        NativeFreshResearchSourceOwners owners,NativeId65FreshDiscVerification measured)
    {
        if(measured.OutputSha256!=TiledFamilyImageSha256||measured.ChangedSectors!=1297)
            throw new InvalidDataException("Exact saved tiled-family complete physical image differs from the independent closed-profile reference.");
        Saved=family.Saved;_tiledFamilyCompilation=family;
        SourceImagePath=owners.ImagePath;SourceCuePath=owners.CuePath;
        _imageIdentity=owners.ImageIdentity!;_cueIdentity=owners.CueIdentity!;
        _sourceCue=owners.CueSnapshot!.ToArray();SourceCueSha256=Hash(_sourceCue);
        _outputCue=Encoding.UTF8.GetBytes("FILE \""+ImageName+"\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        _expected=measured;_canonical=family.CopyCanonicalDocument();
        RequireTiledFamilyAncestry(Saved,family);
        var disc=family.Disc;var parent=family.ParentExecutable;var placement=family.ParentPlacement;
        var body=family.Body;var terrain=family.Terrain;var material=family.BaseBody.MixedResourceAncestor!;
        using var document=JsonDocument.Parse(_canonical);
        var snapshot=Saved.Snapshot;
        var revision=new{
            projectId=snapshot.ProjectId,root=snapshot.RootPath,path=snapshot.Path,
            rawSha256=snapshot.ContentSha256,manifestSha256=snapshot.ManifestSha256,
            canonicalSha256=family.CanonicalDocumentSha256,canonical=document.RootElement};
        var lineage=new{
            compilationPlan=family.PlanSha256,discPlan=disc.PlanSha256,executablePlan=family.Executable.PlanSha256,
            bodyPlan=body.PlanSha256,terrainPlan=terrain.PlanSha256,
            parentDiscPlan=family.ParentDisc.PlanSha256,parentExecutablePlan=parent.PlanSha256,
            parentPlacementPlan=placement.PlanSha256,parentClearancePlan=family.ParentClearance.PlanSha256,
            tiledUnionClearancePlan=family.Clearance.PlanSha256,wizardInitialClearancePlan=family.WizardClearance.PlanSha256,
            baseSubdocumentSha256=Hash(NativeFreshLevelDocumentCodec.SerializeCanonical(family.BaseDocument)),
            baseBodyPlan=family.BaseBody.PlanSha256,baseHostPlan=family.BaseBody.HostAncestor.PlanSha256,
            baseScenePlan=family.BaseBody.HostAncestor.SceneAncestor.PlanSha256,
            mixedResourcePlan=material.PlanSha256,familyCompositionPlan=family.Composition.PlanSha256,
            chestHostPlan=placement.BodyAncestor.HostAncestor.PlanSha256,chestResourcePlan=placement.BodyAncestor.ResourceAncestor.PlanSha256,
            chestScenePlan=placement.BodyAncestor.SceneAncestor.PlanSha256,chestBodyPlan=placement.BodyAncestor.PlanSha256,
            tiledContent=document.RootElement.GetProperty("content"),tileCount=Saved.Document.TiledContent!.Tiles.Count,
            orderedFaceMaterials=family.Intent.OrderedFaceMaterials,materialSelections=family.Intent.MaterialSelections,
            neutralSurfaceSha256=family.Intent.SurfaceSha256,boundSurfaceSha256=family.Intent.BoundSurfaceSha256,
            clearanceScope="exact-four-tile-union-initial-and-fixed-xy-landing-only",
            clearance=new{family.Clearance.UnionX,family.Clearance.UnionY,family.Clearance.UnionWidth,family.Clearance.UnionDepth,
                family.Clearance.FloorZ,family.Clearance.OuterUnionEquivalentProven,family.Clearance.StaticLandingClearanceProven,
                ChestMargins=new{
                    Left=family.Clearance.ChestMargins.Left.ToString(CultureInfo.InvariantCulture),
                    Right=family.Clearance.ChestMargins.Right.ToString(CultureInfo.InvariantCulture),
                    Top=family.Clearance.ChestMargins.Top.ToString(CultureInfo.InvariantCulture),
                    Bottom=family.Clearance.ChestMargins.Bottom.ToString(CultureInfo.InvariantCulture)},
                PlayerMargins=new{
                    Left=family.Clearance.PlayerMargins.Left.ToString(CultureInfo.InvariantCulture),
                    Right=family.Clearance.PlayerMargins.Right.ToString(CultureInfo.InvariantCulture),
                    Top=family.Clearance.PlayerMargins.Top.ToString(CultureInfo.InvariantCulture),
                    Bottom=family.Clearance.PlayerMargins.Bottom.ToString(CultureInfo.InvariantCulture)}},
            authoredPlacementCount=Saved.Document.Placements.Count,
            nativePlacedCount=body.SceneLayout.PlacedCount,nativeNominalCapacity=body.SceneLayout.DynamicCapacity,
            families=body.RowSpans.Placements.Select((span,i)=>new{
                span.EditorId,span.FirstRowIndex,span.RowCount,authoredPlacement=document.RootElement.GetProperty("placements")[i]}).ToArray(),
            nativeRowOwners=body.Allocation.Rows.Select((row,i)=>new{
                nativeIndex=i,actorClass=parent.Rows[i].ActorClassId,rowOffset=row.Row.Offset,rowLength=row.Row.ByteLength,
                privateOffset=row.Properties.Offset,privateLength=row.Properties.ByteLength,
                finalRowSha256=parent.Rows[i].SceneRowSha256,sourceRowSha256=parent.Rows[i].SourceRowSha256}).ToArray(),
            rewardDestinationOverrides=placement.RewardDestinationsAncestor!.Values,coordinateWrites=placement.CoordinateWrites,
            materials=terrain.MaterialSources.Select((source,i)=>new{
                slot=i,source.Handle,source.SourceImageSha256,source.SourceWadEntry,source.SourceTextureId,
                source.SourceDataSha256,sourceRecordSha256=source.Layout.RecordSha256}).ToArray(),
            uploadSha256=terrain.UploadSha256,dataSha256=terrain.DataSha256,modelSha256=terrain.ModelSha256,sceneSha256=body.SceneSha256,
            terrain.RetainedComplementSha256,terrain.PriorDataAllocationSha256,
            bodySha256=disc.BodySha256,overlaySha256=disc.OverlaySha256,executableSha256=disc.ExecutableSha256,
            wadDirectorySha256=disc.WadDirectorySha256,isoRootSha256=disc.IsoRootSha256,
            disc.BodyLba,disc.FinalExecutableLba,disc.FinalWadByteLength,
            body.RuntimeMemory.UsedLevelDataBytes,body.RuntimeMemory.ModelRamStart,body.RuntimeMemory.SceneRamStart,
            body.RuntimeMemory.SceneReadEnd,sourceSelectorRows=parent.Rows,
            parent.KnownSelectorCount,parent.KnownDeterministicSelectorSubtotal,parent.ExperimentalTreasureTarget,
            experimentalKeyZeroAssumption=true,keyZeroContributionProven=false,completeStaticCensusProven=false,
            runtimeDropGuarantee=false,footprintAndTrajectoryProven=false,
            upstreamPendingOwnerScope="exact saved tiled-family issuance; no generic loader, route, trajectory, peak-memory or publication certificate",
            upstreamPendingOwners=family.ParentDisc.PendingOwners};
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            profile=ProfileId,variant=TiledFamilyVariant,saved=revision,
            sourceImageSha256=NativeId65FreshDiscSource.OriginalSha256,
            SourceImagePath,SourceCuePath,SourceCueSha256,imageIdentity=_imageIdentity,cueIdentity=_cueIdentity,
            lineage,expectedImageSha256=measured.OutputSha256,expectedImageByteLength=ExpectedImageByteLength,
            changedRawSectors=measured.ChangedSectors,cueSha256=Hash(_outputCue),wholeImageInverseRequired=true,
            savedRevisionBound=true,nativePersistentSaveOwnerProven=false,runtimeVerified=false,
            normalCreateBinApproved=false,completeNativeDependencyClosureProven=false}));
        _receipt=JsonSerializer.SerializeToUtf8Bytes(new{
            schema=1,profile=ProfileId,variant=TiledFamilyVariant,planSha256=PlanSha256,saved=revision,
            SourceImagePath,SourceCuePath,SourceCueSha256,sourceImageSha256=NativeId65FreshDiscSource.OriginalSha256,lineage,
            imageFile=ImageName,imageSha256=measured.OutputSha256,imageByteLength=ExpectedImageByteLength,
            changedRawSectors=measured.ChangedSectors,cueFile=CueName,cueSha256=Hash(_outputCue),receiptFile=ReceiptName,
            wholeImageInverseRequired=true,savedRevisionBound=true,nativePersistentSaveOwnerProven=false,
            runtimeVerified=false,normalCreateBinApproved=false,completeNativeDependencyClosureProven=false,
            hqRuntimeProven=false,audioRuntimeProven=false,peakRuntimeMemoryProven=false,
            checkpointOrForeignStateSupported=false,publicationApproved=false,keyZeroContributionProven=false,
            completeStaticCensusProven=false,runtimeDropGuarantee=false,experimentalKeyZeroAssumption=true},
            new JsonSerializerOptions{WriteIndented=true});
        if(_receipt.Length is <1 or >65536)
            throw new InvalidDataException("Complete saved tiled-family receipt exceeds companion bound; no evidence is truncated.");
    }

    private static async Task<NativeFreshSavedLevelResearchCandidatePlan> CaptureTiledFamilyAsync(string originalBin,
        string sourceCue,NativeFreshSavedLevelDocumentReadContext saved,CancellationToken token)
    {
        _=NativeFreshSavedTiledFamilyCompilation.ProjectBaseDocument(saved.Document);
        NativeFreshDocumentStorage.RequireAggregateCurrent(saved.Snapshot,token);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);ArgumentException.ThrowIfNullOrWhiteSpace(sourceCue);
        var owners=new NativeFreshResearchSourceOwners(Path.GetFullPath(originalBin),Path.GetFullPath(sourceCue));
        NativeFreshSavedLevelResearchCandidatePlan? result=null;Exception? primary=null;
        try
        {
            owners.Open(token);
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(owners.Bin!,token);
            var family=await NativeFreshSavedTiledFamilyCompilation.CaptureAsync(owners.ImagePath,saved,token);
            family.Clearance.RequireStaticLandingClearance(family.Executable,family.ParentClearance);
            owners.Bin!.Position=0;
            var measured=NativeId65FreshDiscStreaming.Measure(family.Disc,owners.Bin,token);
            result=new(family,owners,measured);
            token.ThrowIfCancellationRequested();
        }
        catch(Exception error){primary=error;}
        CompleteOperation(owners,saved,primary);
        token.ThrowIfCancellationRequested();return result!;
    }

    internal void RequireTiledFamilyAncestry(NativeFreshSavedLevelDocumentReadContext saved,
        NativeFreshSavedTiledFamilyCompilation family)
    {
        if(!ReferenceEquals(saved,Saved)||family is null||!ReferenceEquals(family,_tiledFamilyCompilation)||
            _material is not null||_mixedDisc is not null||_mixedClearance is not null||
            _familyCompilation is not null||_tiledCompilation is not null||_lockedChestCompilation is not null)
            throw new InvalidDataException("Saved tiled-family Candidate requires its actual complete saved compilation, not an equal or projected arm.");
        family.RequireAncestry(saved);
        if(family.SourceImagePath!=SourceImagePath||
            !_canonical.AsSpan().SequenceEqual(family.CopyCanonicalDocument())||
            !_canonical.AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(saved.Document)))
            throw new InvalidDataException("Saved tiled-family Candidate source path or complete canonical document changed.");
    }
}

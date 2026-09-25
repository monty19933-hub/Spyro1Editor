using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Shared, closed source construction for the two exact saved R03 recipes.
/// The complete document remains authoritative; BaseDocument is only the
/// actual subordinate same-layout resource input. No saved context is issued
/// or substituted here, and no general family or output authority is granted.
/// </summary>
internal sealed class NativeFreshSavedR03FamilyAssembly
{
    private readonly byte[] _canonical;
    private NativeFreshSavedR03FamilyAssembly(NativeFreshLevelDocumentIntent document,
        NativeFreshLevelDocumentIntent baseDocument,NativeId65FreshWizardResearchBodyPlan baseBody,
        NativeFreshInitialSpawnClearancePlan wizardClearance,NativeFreshMobyFamilyCompositionPlan composition,
        NativeFreshLockedChestPlacementPlan placement,NativeFreshLockedChestSpawnClearancePlan clearance,
        NativeFreshLockedChestResearchDiscPlan disc,string image)
    {
        Document=document;BaseDocument=baseDocument;BaseBody=baseBody;WizardClearance=wizardClearance;
        Composition=composition;Placement=placement;Clearance=clearance;Disc=disc;SourceImagePath=image;
        _canonical=NativeFreshLevelDocumentCodec.SerializeCanonical(document);
        RequireAncestry(document,baseDocument);
    }

    internal NativeFreshLevelDocumentIntent Document{get;}
    internal NativeFreshLevelDocumentIntent BaseDocument{get;}
    internal NativeId65FreshWizardResearchBodyPlan BaseBody{get;}
    internal NativeFreshInitialSpawnClearancePlan WizardClearance{get;}
    internal NativeFreshMobyFamilyCompositionPlan Composition{get;}
    internal NativeLockedChestFamilySource ChestSource=>Host.FamilySourceAncestor;
    internal NativeFreshLockedChestResearchHostExtension Host=>Body.HostAncestor;
    internal NativeFreshMobyFamilyCompositionPlan Resources=>Body.ResourceAncestor;
    internal NativeFreshLockedChestSceneExtension Scene=>Body.SceneAncestor;
    internal NativeFreshLockedChestResearchBody Body=>Placement.BodyAncestor;
    internal NativeFreshLockedChestPlacementPlan Placement{get;}
    internal NativeFreshLockedChestSpawnClearancePlan Clearance{get;}
    internal NativeFreshLockedChestExperimentalExecutable Executable=>Disc.ExecutableAncestor;
    internal NativeFreshLockedChestResearchDiscPlan Disc{get;}
    internal string SourceImagePath{get;}

    private static void RequireProjection(NativeFreshLevelDocumentIntent document,NativeFreshLevelDocumentIntent projected)
    {
        ArgumentNullException.ThrowIfNull(document);ArgumentNullException.ThrowIfNull(projected);
        // Both branches re-run their complete closed preflight before source I/O.
        var expected=document.TiledContent is null?
            NativeFreshSavedLockedChestFamilyCompilation.ProjectBaseDocument(document):
            NativeFreshSavedTiledFamilyCompilation.ProjectBaseDocument(document);
        if(!NativeFreshLevelDocumentCodec.SerializeCanonical(expected).AsSpan().SequenceEqual(
                NativeFreshLevelDocumentCodec.SerializeCanonical(projected))||
            projected.Placements.Count!=1||!ReferenceEquals(projected.Placements[0],document.Placements[0])||
            projected.FloorMaterials is not { } materials||
            !ReferenceEquals(materials.Triangle0,expected.FloorMaterials!.Triangle0)||
            !ReferenceEquals(materials.Triangle1,expected.FloorMaterials.Triangle1)||
            document.LegacyContent is { } legacy&&
                (!ReferenceEquals(projected.Base,legacy.Base)||!ReferenceEquals(materials,legacy.FloorMaterials)))
            throw new InvalidDataException("Shared saved R03 construction requires its exact subordinate projection and actual retained Wizard/material owners.");
    }

    internal void RequireAncestry(NativeFreshLevelDocumentIntent document,NativeFreshLevelDocumentIntent baseDocument)
    {
        if(!ReferenceEquals(document,Document)||!ReferenceEquals(baseDocument,BaseDocument)||
            !_canonical.AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(Document)))
            throw new InvalidDataException("Shared saved R03 construction retains its actual complete document and subordinate projection, not equal reissued owners.");
        RequireProjection(document,baseDocument);
        var baseHost=BaseBody.HostAncestor;var baseScene=baseHost.SceneAncestor;
        var mixed=BaseBody.MixedResourceAncestor??throw new InvalidDataException("Saved chest requires its actual mixed material resource owner.");
        if(baseScene.Producer!=NativeFreshWizardSceneProducer.SameLayoutMaterialAuthored||
            !ReferenceEquals(baseScene.AuthoringDocumentAncestor,BaseDocument)||mixed.MaterialSources.Count!=2)
            throw new InvalidDataException("Saved chest requires the separately named actual same-layout Scene and material producer.");
        _=NativeFreshWizardAuthoringShape.RequireSameLayoutMaterialDocument(BaseDocument,baseScene.CompilationAncestor);
        BaseBody.RequireAncestry(baseHost);
        mixed.RequireAncestry(baseHost,BaseDocument,mixed.MaterialSources[0],mixed.MaterialSources[1]);
        _=WizardClearance.RequireInitialFloorFootprint(BaseDocument,baseScene);
        Composition.RequireAncestry(BaseBody);
        if(Composition.Placements.Count!=2||!Composition.Placements.Select(p=>p.EditorId)
                .SequenceEqual(Document.Placements.Take(2).Select(p=>p.EditorId))||
            !ReferenceEquals(Composition.Placements[0].WizardAsset,baseScene.FamilyAncestor))
            throw new InvalidDataException("Saved chest base composition lost actual ordered Wizard/Grass identities.");
        var grass=(NativeFreshMobyPlacementIntent)Document.Placements[1];var placed=Composition.Placements[1];
        var asset=placed.PassiveAsset??throw new InvalidDataException("Saved Grass did not resolve to an actual retained asset.");
        asset.RequireRetainedOwners();placed.Dispatch!.RequireAncestry(baseHost,asset);
        if(asset.SourceImageSha256!=NativeId65FreshDiscSource.OriginalSha256||asset.DonorWadEntry!=grass.Source.SourceWadEntry||
            asset.DonorTrueIndex!=grass.Source.SourceTrueIndex||asset.NativeClass!=grass.Source.ExpectedNativeClass||
            placed.X!=grass.X||placed.Y!=grass.Y||placed.Z!=grass.Z||placed.NativeYawByte!=grass.NativeYawByte)
            throw new InvalidDataException("Saved Grass source/transform differs from its actual installed owner.");
        ChestSource.RequireWizardOriginalOwner(baseScene.FamilyAncestor);
        Host.CurrentExecutableAncestor.RequireFamilyAncestry(Composition);
        Host.RequireAncestry(Host.CurrentExecutableAncestor,ChestSource);
        Resources.RequireLockedChestResourceAncestry(Composition,ChestSource);
        Scene.RequireAncestry(Resources,Host);
        Body.RequireAncestry(Resources,Host,Scene);
        if(!ReferenceEquals(Resources.BaseBodyAncestor,BaseBody)||!ReferenceEquals(Scene.SourceAncestor,ChestSource)||
            !Scene.RowSpans.Placements.Select(p=>p.EditorId).SequenceEqual(Document.Placements.Select(p=>p.EditorId))||
            !Scene.RowSpans.Placements.Select(p=>p.FirstRowIndex).SequenceEqual(new[]{0,1,2})||
            !Scene.RowSpans.Placements.Select(p=>p.RowCount).SequenceEqual(new[]{1,1,7})||
            Scene.Layout.PlacedCount!=9||Scene.Layout.DynamicCapacity!=32||Scene.Allocation.Rows.Count!=9)
            throw new InvalidDataException("Saved family identities must expand exactly to their actual ordered1+1+7 P9/N32 allocation.");
        var chest=(NativeFreshLockedChestFamilyPlacementIntent)Document.Placements[2];var anchors=Placement.Intent;
        Placement.RequireAncestry(Body,anchors);
        Placement.RequireRewardDestinationsAncestry(chest.RewardDestinations!);
        if(anchors.KeyX!=chest.KeyX||anchors.KeyY!=chest.KeyY||anchors.KeyZ!=chest.KeyZ||
            anchors.ChestX!=chest.ChestX||anchors.ChestY!=chest.ChestY||anchors.ChestZ!=chest.ChestZ)
            throw new InvalidDataException("Saved family anchors differ from the actual placed source-owned rows.");
        Clearance.RequireStaticLandingClearance(Placement);
        Executable.RequireAncestry(Placement,Host);
        Disc.RequireAncestry(Executable);
    }

    internal static async Task<NativeFreshSavedR03FamilyAssembly> CaptureAsync(string image,
        NativeFreshLevelDocumentIntent document,NativeFreshLevelDocumentIntent projected,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();RequireProjection(document,projected);
        var (baseBody,wizardClearance)=await NativeFreshSavedLevelResearchCandidatePlan.CaptureSameLayoutMixedBodyAsync(image,projected,token);
        var baseHost=baseBody.HostAncestor;var wizard=baseHost.SceneAncestor.FamilyAncestor;
        var grass=await NativeMobyFamilyAssetPlan.CaptureArtisansGrassAsync(image,token);
        var native=(NativeFreshMobyPlacementIntent)document.Placements[1];
        var composition=NativeFreshMobyFamilyCompositionPlan.Compose(baseBody,[
            NativeFreshMobyFamilyPlacement.RetainedWizard(document.Placements[0].EditorId,wizard),
            NativeFreshMobyFamilyPlacement.Passive(native.EditorId,grass,NativeMobyFamilyPassiveHostBinding.Bind(baseHost,grass),
                native.X,native.Y,native.Z,native.NativeYawByte)]);
        var current=await NativeId65FreshWizardResearchExecutablePlan.CaptureFamilyComposedAsync(image,composition,token);
        var source=await NativeLockedChestFamilySource.CaptureOriginalAsync(image,wizard,token);
        var host=NativeFreshLockedChestResearchHostExtension.Compose(current,source);
        var sound=NativeFreshMobySoundResidencyBinding.BindLockedChest(source,composition);
        var fallback=await NativeId65FreshWizardResearchFallbackResourceSource.CaptureAsync(image,baseHost.SceneAncestor.ResourceAncestor,token);
        var resources=NativeFreshMobyFamilyCompositionPlan.ComposeWithLockedChestResources(composition,sound,fallback);
        var chest=(NativeFreshLockedChestFamilyPlacementIntent)document.Placements[2];
        var scene=NativeFreshLockedChestSceneExtension.Compose(resources,host,chest.EditorId);
        var body=NativeFreshLockedChestResearchBody.Compose(resources,host,scene);
        var placement=NativeFreshLockedChestPlacementPlan.ComposeWithRewardDestinations(body,
            new(chest.KeyX,chest.KeyY,chest.KeyZ,chest.ChestX,chest.ChestY,chest.ChestZ),chest.RewardDestinations!);
        var clearance=NativeFreshLockedChestSpawnClearancePlan.Compose(placement);
        clearance.RequireStaticLandingClearance(placement);
        var executable=NativeFreshLockedChestExperimentalExecutable.Compose(placement,host);
        var disc=await NativeFreshLockedChestResearchDiscPlan.CaptureAsync(image,executable,token);
        token.ThrowIfCancellationRequested();
        return new(document,projected,baseBody,wizardClearance,composition,placement,clearance,disc,image);
    }
}

using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// The exact saved schema5 R03 research recipe, resolved through existing
/// Wizard/Grass/chest producers. Its schema2 subdocument is only a subordinate
/// resource input; the actual complete saved document and all three authored
/// identities remain authoritative. No generic family or runtime approval.
/// </summary>
internal sealed class NativeFreshSavedLockedChestFamilyCompilation
{
    private readonly byte[] _canonical;
    private NativeFreshSavedLockedChestFamilyCompilation(NativeFreshSavedLevelDocumentReadContext saved,
        NativeFreshLevelDocumentIntent baseDocument,NativeId65FreshWizardResearchBodyPlan baseBody,
        NativeFreshInitialSpawnClearancePlan wizardClearance,NativeFreshMobyFamilyCompositionPlan composition,
        NativeFreshLockedChestPlacementPlan placement,NativeFreshLockedChestSpawnClearancePlan clearance,
        NativeFreshLockedChestResearchDiscPlan disc,string image)
    {
        Saved=saved;BaseDocument=baseDocument;BaseBody=baseBody;WizardClearance=wizardClearance;
        Composition=composition;Placement=placement;Clearance=clearance;Disc=disc;SourceImagePath=image;
        _canonical=NativeFreshLevelDocumentCodec.SerializeCanonical(saved.Document);
        CanonicalDocumentSha256=Convert.ToHexStringLower(SHA256.HashData(_canonical));
        RequireAncestry(saved);
    }

    internal NativeFreshSavedLevelDocumentReadContext Saved{get;}
    internal NativeFreshLevelDocumentIntent Document=>Saved.Document;
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
    internal string CanonicalDocumentSha256{get;}
    internal bool RuntimeVerified=>false;
    internal bool PublicationApproved=>false;
    internal bool CompleteNativeDependencyClosureProven=>false;
    internal bool KeyZeroContributionProven=>false;
    internal byte[] CopyCanonicalDocument()=>_canonical.ToArray();

    internal static NativeFreshLevelDocumentIntent ProjectBaseDocument(NativeFreshLevelDocumentIntent document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if(document.SchemaVersion!=5||document.LegacyContent is not { } legacy||document.Placements.Count!=3||
            document.Placements[0] is not NativeFreshWizardRouteIntent wizard||
            document.Placements[1] is not NativeFreshMobyPlacementIntent grass||
            document.Placements[2] is not NativeFreshLockedChestFamilyPlacementIntent chest||
            legacy.FloorMaterials is not { } materials||
            materials.Triangle0 is not NativeFreshRetailTerrainMaterialIntent{SourceWadEntry:14,TextureId:1} first||
            materials.Triangle1 is not NativeFreshRetailTerrainMaterialIntent{SourceWadEntry:70,TextureId:1} second||
            first.SourceVariant!=NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant||
            second.SourceVariant!=NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant||
            grass.Source.SourceVariant!=NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant||
            grass.Source.SourceWadEntry!=10||grass.Source.SourceTrueIndex!=121||grass.Source.ExpectedNativeClass!=0x1F5||
            grass.X!=9152||grass.Y!=9152||grass.Z!=8704||grass.NativeYawByte!=64||
            chest.Source.SourceVariant!=NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant||
            chest.Source.DefinitionId!=NativeFreshLockedChestFamilyReference.SupportedDefinitionId||
            chest.KeyX!=9728||chest.KeyY!=9120||chest.KeyZ!=8704||
            chest.ChestX!=10864||chest.ChestY!=9888||chest.ChestZ!=8704||
            chest.RewardDestinations is not {Values.Count:1} destinations||
            destinations.Values[0] is not {Role:NativeFreshChestRewardRole.Reward2,X:10454,Y:10503,Z:9216})
            throw new InvalidDataException("Saved locked-chest research requires the complete ordered schema5 R03 Wizard/Artisans Grass/Peace chest family, ordered14/70 T1 materials and the one explicit Reward2 WORLD destination. Other authored families remain unsupported.");
        var b=legacy.Base;
        if(b.FloorX!=8448||b.FloorY!=8448||b.FloorZ!=8704||b.FloorWidth!=3072||b.FloorDepth!=3072||
            b.SpawnX!=8960||b.SpawnY!=8960||b.SpawnZ!=9216||b.Heading!=64||b.MusicTrackId!=19||
            b.FloorColor!=new ColorRgba(80,128,112)||b.SkyColor!=new ColorRgba(128,96,64)||
            wizard.OriginX!=9984||wizard.OriginY!=9984||wizard.OriginZ!=8704||wizard.EndX!=9472||wizard.EndY!=9984)
            throw new InvalidDataException("Saved locked-chest research requires the exact3072 R03 floor, spawn, display, track19 and retained Wizard route.");
        // No source I/O or owner replacement occurred before complete validation.
        var projected=NativeFreshLevelDocumentIntent.Create(b,[wizard]).WithFloorMaterials(materials);
        _=NativeFreshWizardAuthoringShape.RequireSameLayoutMaterialDocumentShape(projected);
        return projected;
    }

    internal void RequireAncestry(NativeFreshSavedLevelDocumentReadContext saved)
    {
        if(!ReferenceEquals(saved,Saved))
            throw new InvalidDataException("Saved locked-chest compilation requires its actual complete saved-document context, not an equal reissued owner.");
        saved.RequireDocument(Document);
        var projected=ProjectBaseDocument(Document);
        if(!ReferenceEquals(BaseDocument.Base,Document.Base)||
            !ReferenceEquals(BaseDocument.Placements[0],Document.Placements[0])||
            !ReferenceEquals(BaseDocument.FloorMaterials,Document.FloorMaterials)||
            !NativeFreshLevelDocumentCodec.SerializeCanonical(projected).AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(BaseDocument))||
            !_canonical.AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(Document)))
            throw new InvalidDataException("Saved locked-chest subordinate projection lost an actual owner or complete canonical authorship.");
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

    internal static async Task<NativeFreshSavedLockedChestFamilyCompilation> CaptureAsync(string image,
        NativeFreshSavedLevelDocumentReadContext saved,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(saved);
        var document=saved.Document;var projected=ProjectBaseDocument(document);
        NativeFreshDocumentStorage.RequireAggregateCurrent(saved.Snapshot,token);
        ArgumentException.ThrowIfNullOrWhiteSpace(image);string path=Path.GetFullPath(image);
        NativeFreshSavedLockedChestFamilyCompilation? result=null;List<Exception> failures=[];
        try
        {
            var (baseBody,wizardClearance)=await NativeFreshSavedLevelResearchCandidatePlan.CaptureSameLayoutMixedBodyAsync(path,projected,token);
            var baseHost=baseBody.HostAncestor;var wizard=baseHost.SceneAncestor.FamilyAncestor;
            var grass=await NativeMobyFamilyAssetPlan.CaptureArtisansGrassAsync(path,token);
            var native=(NativeFreshMobyPlacementIntent)document.Placements[1];
            var composition=NativeFreshMobyFamilyCompositionPlan.Compose(baseBody,[
                NativeFreshMobyFamilyPlacement.RetainedWizard(document.Placements[0].EditorId,wizard),
                NativeFreshMobyFamilyPlacement.Passive(native.EditorId,grass,NativeMobyFamilyPassiveHostBinding.Bind(baseHost,grass),
                    native.X,native.Y,native.Z,native.NativeYawByte)]);
            var current=await NativeId65FreshWizardResearchExecutablePlan.CaptureFamilyComposedAsync(path,composition,token);
            var source=await NativeLockedChestFamilySource.CaptureOriginalAsync(path,wizard,token);
            var host=NativeFreshLockedChestResearchHostExtension.Compose(current,source);
            var sound=NativeFreshMobySoundResidencyBinding.BindLockedChest(source,composition);
            var fallback=await NativeId65FreshWizardResearchFallbackResourceSource.CaptureAsync(path,baseHost.SceneAncestor.ResourceAncestor,token);
            var resources=NativeFreshMobyFamilyCompositionPlan.ComposeWithLockedChestResources(composition,sound,fallback);
            var chest=(NativeFreshLockedChestFamilyPlacementIntent)document.Placements[2];
            var scene=NativeFreshLockedChestSceneExtension.Compose(resources,host,chest.EditorId);
            var body=NativeFreshLockedChestResearchBody.Compose(resources,host,scene);
            var placement=NativeFreshLockedChestPlacementPlan.ComposeWithRewardDestinations(body,
                new(chest.KeyX,chest.KeyY,chest.KeyZ,chest.ChestX,chest.ChestY,chest.ChestZ),chest.RewardDestinations!);
            var clearance=NativeFreshLockedChestSpawnClearancePlan.Compose(placement);
            clearance.RequireStaticLandingClearance(placement);
            var executable=NativeFreshLockedChestExperimentalExecutable.Compose(placement,host);
            var disc=await NativeFreshLockedChestResearchDiscPlan.CaptureAsync(path,executable,token);
            result=new(saved,projected,baseBody,wizardClearance,composition,placement,clearance,disc,path);
            token.ThrowIfCancellationRequested();
        }
        catch(Exception error){failures.Add(error);}
        finally
        {
            // The existing typed captures own Original checks; the outer saved
            // Candidate holds the complete BIN/CUE pair. This compilation also
            // refuses a saved revision changed during either success or failure.
            try{NativeFreshDocumentStorage.RequireAggregateCurrent(saved.Snapshot,CancellationToken.None);}
            catch(Exception error){failures.Add(error);}
        }
        NativeFreshResearchSourceOwners.ThrowFailures(failures);token.ThrowIfCancellationRequested();
        return result??throw new InvalidDataException("Saved locked-chest source compilation returned no result.");
    }
}

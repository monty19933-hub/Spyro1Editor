using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Binds the complete saved schema3 document to resolved family instances. The
/// explicit base subdocument is only an input to the existing Wizard resource
/// producer; it never replaces the saved document or discards its native rows.
/// This first original-only research profile does not grant generic import.
/// </summary>
internal sealed class NativeFreshSavedMobyFamilyCompilation
{
    private readonly byte[] _canonical;
    private NativeFreshSavedMobyFamilyCompilation(NativeFreshSavedLevelDocumentReadContext saved,
        NativeFreshLevelDocumentIntent baseDocument,NativeId65FreshWizardResearchBodyPlan body,
        NativeFreshInitialSpawnClearancePlan clearance,NativeFreshMobyFamilyCompositionPlan composition,
        NativeId65FreshWizardResearchDiscPlan disc,string image)
    {
        Saved=saved;BaseDocument=baseDocument;BaseBody=body;WizardClearance=clearance;Composition=composition;Disc=disc;
        SourceImagePath=image;_canonical=NativeFreshLevelDocumentCodec.SerializeCanonical(saved.Document);
        RequireAncestry(saved);
    }
    internal NativeFreshSavedLevelDocumentReadContext Saved{get;}
    internal NativeFreshLevelDocumentIntent Document=>Saved.Document;
    internal NativeFreshLevelDocumentIntent BaseDocument{get;}
    internal NativeId65FreshWizardResearchBodyPlan BaseBody{get;}
    internal NativeFreshInitialSpawnClearancePlan WizardClearance{get;}
    internal NativeFreshMobyFamilyCompositionPlan Composition{get;}
    internal NativeId65FreshWizardResearchDiscPlan Disc{get;}
    internal string SourceImagePath{get;}

    internal static NativeFreshLevelDocumentIntent ProjectBaseDocument(NativeFreshLevelDocumentIntent document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if(document.SchemaVersion!=3||document.Placements.Count!=2||document.Placements[0] is not NativeFreshWizardRouteIntent wizard||
            document.Placements[1] is not NativeFreshMobyPlacementIntent moby||document.FloorMaterials is null||
            document.FloorMaterials.Triangle0 is not NativeFreshRetailTerrainMaterialIntent{SourceWadEntry:14,TextureId:1}||
            document.FloorMaterials.Triangle1 is not NativeFreshRetailTerrainMaterialIntent{SourceWadEntry:70,TextureId:1}||
            moby.Source.SourceVariant!=NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant||moby.Source.SourceWadEntry!=10||
            moby.Source.SourceTrueIndex!=121||moby.Source.ExpectedNativeClass!=0x1F5||
            moby.X!=9152||moby.Y!=9152||moby.Z!=8704||moby.NativeYawByte!=64)
            throw new InvalidDataException("Saved native-family research requires the complete schema3 separatedB Wizard plus Artisans T121 Grass at9152/9152/8704 native yaw64, and ordered14/70 T1 materials. Other native dependencies remain authored-only.");
        var projected=NativeFreshLevelDocumentIntent.Create(document.Base,[wizard]).WithFloorMaterials(document.FloorMaterials);
        _=NativeFreshWizardAuthoringShape.RequireMaterialDocumentShape(projected);
        return projected;
    }

    internal void RequireAncestry(NativeFreshSavedLevelDocumentReadContext saved)
    {
        if(!ReferenceEquals(saved,Saved))throw new InvalidDataException("Native-family compilation requires its actual complete saved-document owner.");
        saved.RequireDocument(Document);
        var projected=ProjectBaseDocument(Document);
        if(!ReferenceEquals(BaseDocument.Base,Document.Base)||!ReferenceEquals(BaseDocument.Placements[0],Document.Placements[0])||
            !ReferenceEquals(BaseDocument.FloorMaterials,Document.FloorMaterials)||
            !NativeFreshLevelDocumentCodec.SerializeCanonical(projected).AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(BaseDocument))||
            !_canonical.AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(Document)))
            throw new InvalidDataException("Native-family base projection changed an owner or lost complete saved intent.");
        var host=BaseBody.HostAncestor;var scene=host.SceneAncestor;var resources=BaseBody.MixedResourceAncestor!;
        BaseBody.RequireAncestry(host);Composition.RequireAncestry(BaseBody);
        resources.RequireAncestry(host,BaseDocument,resources.MaterialSources[0],resources.MaterialSources[1]);
        _=WizardClearance.RequireInitialFloorFootprint(BaseDocument,scene);
        var exe=Disc.ExecutableAncestor;exe.RequireFamilyAncestry(Composition);Disc.RequireAncestry(exe);
        if(!ReferenceEquals(Disc.FamilyCompositionAncestor,Composition)||Composition.Placements.Count!=Document.Placements.Count||
            !Composition.Placements.Select(p=>p.EditorId).SequenceEqual(Document.Placements.Select(p=>p.EditorId))||
            !ReferenceEquals(Composition.Placements[0].WizardAsset,scene.FamilyAncestor))
            throw new InvalidDataException("Native-family saved composition lost exact ordered placement or executable/disc ancestry.");
        var intent=(NativeFreshMobyPlacementIntent)Document.Placements[1];var placed=Composition.Placements[1];
        var actual=placed.PassiveAsset??throw new InvalidDataException("Saved native source was not resolved to a retained actual asset.");
        actual.RequireRetainedOwners();placed.Dispatch!.RequireAncestry(host,actual);
        if(actual.SourceImageSha256!=NativeId65FreshDiscSource.OriginalSha256||actual.DonorWadEntry!=intent.Source.SourceWadEntry||
            actual.DonorTrueIndex!=intent.Source.SourceTrueIndex||actual.NativeClass!=intent.Source.ExpectedNativeClass||
            placed.X!=intent.X||placed.Y!=intent.Y||placed.Z!=intent.Z||placed.NativeYawByte!=intent.NativeYawByte)
            throw new InvalidDataException("Saved neutral source/transform does not match its actual captured and installed family.");
    }

    internal static async Task<NativeFreshSavedMobyFamilyCompilation> CaptureAsync(string image,
        NativeFreshSavedLevelDocumentReadContext saved,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(saved);
        var document=saved.Document;var projected=ProjectBaseDocument(document);
        var (body,clearance)=await NativeFreshSavedLevelResearchCandidatePlan.CaptureMixedBodyAsync(image,projected,token);
        var asset=await NativeMobyFamilyAssetPlan.CaptureArtisansGrassAsync(image,token);
        var binding=NativeMobyFamilyPassiveHostBinding.Bind(body.HostAncestor,asset);
        var native=(NativeFreshMobyPlacementIntent)document.Placements[1];
        var composition=NativeFreshMobyFamilyCompositionPlan.Compose(body,[
            NativeFreshMobyFamilyPlacement.RetainedWizard(document.Placements[0].EditorId,body.HostAncestor.SceneAncestor.FamilyAncestor),
            NativeFreshMobyFamilyPlacement.Passive(native.EditorId,asset,binding,native.X,native.Y,native.Z,native.NativeYawByte)]);
        var exe=await NativeId65FreshWizardResearchExecutablePlan.CaptureFamilyComposedAsync(image,composition,token);
        var disc=await NativeId65FreshWizardResearchDiscPlan.CaptureFamilyComposedAsync(image,exe,token);
        token.ThrowIfCancellationRequested();
        return new(saved,projected,body,clearance,composition,disc,Path.GetFullPath(image));
    }
}

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
    private readonly NativeFreshSavedR03FamilyAssembly _assembly;
    private NativeFreshSavedLockedChestFamilyCompilation(NativeFreshSavedLevelDocumentReadContext saved,
        NativeFreshSavedR03FamilyAssembly assembly)
    {
        Saved=saved;_assembly=assembly;
        _canonical=NativeFreshLevelDocumentCodec.SerializeCanonical(saved.Document);
        CanonicalDocumentSha256=Convert.ToHexStringLower(SHA256.HashData(_canonical));
        RequireAncestry(saved);
    }

    internal NativeFreshSavedLevelDocumentReadContext Saved{get;}
    internal NativeFreshLevelDocumentIntent Document=>Saved.Document;
    internal NativeFreshLevelDocumentIntent BaseDocument=>_assembly.BaseDocument;
    internal NativeId65FreshWizardResearchBodyPlan BaseBody=>_assembly.BaseBody;
    internal NativeFreshInitialSpawnClearancePlan WizardClearance=>_assembly.WizardClearance;
    internal NativeFreshMobyFamilyCompositionPlan Composition=>_assembly.Composition;
    internal NativeLockedChestFamilySource ChestSource=>Host.FamilySourceAncestor;
    internal NativeFreshLockedChestResearchHostExtension Host=>Body.HostAncestor;
    internal NativeFreshMobyFamilyCompositionPlan Resources=>Body.ResourceAncestor;
    internal NativeFreshLockedChestSceneExtension Scene=>Body.SceneAncestor;
    internal NativeFreshLockedChestResearchBody Body=>Placement.BodyAncestor;
    internal NativeFreshLockedChestPlacementPlan Placement=>_assembly.Placement;
    internal NativeFreshLockedChestSpawnClearancePlan Clearance=>_assembly.Clearance;
    internal NativeFreshLockedChestExperimentalExecutable Executable=>Disc.ExecutableAncestor;
    internal NativeFreshLockedChestResearchDiscPlan Disc=>_assembly.Disc;
    internal string SourceImagePath=>_assembly.SourceImagePath;
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
        _assembly.RequireAncestry(Document,BaseDocument);
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
            var assembly=await NativeFreshSavedR03FamilyAssembly.CaptureAsync(path,document,projected,token);
            result=new(saved,assembly);
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

using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Exact saved schema5 four-tile R03 compilation. The complete actual saved
/// aggregate owns every tile, material face and actor identity. A subordinate
/// legacy rectangle supplies only the existing R03 resources/placement; the
/// actual tiled Body/executable/clearance/disc are newly composed from those
/// same parents. This is not Candidate, publication, UI or runtime admission.
/// </summary>
internal sealed class NativeFreshSavedTiledFamilyCompilation
{
    private readonly byte[] _canonical;
    private readonly NativeFreshSavedR03FamilyAssembly _assembly;
    private NativeFreshSavedTiledFamilyCompilation(NativeFreshSavedLevelDocumentReadContext saved,
        NativeFreshTiledFamilyTerrainIntent intent,NativeFreshSavedR03FamilyAssembly assembly)
    {
        Saved=saved;Intent=intent;_assembly=assembly;
        _canonical=NativeFreshLevelDocumentCodec.SerializeCanonical(saved.Document);
        CanonicalDocumentSha256=Hash(_canonical);
        Terrain=NativeFreshTiledFamilyTerrainRevision.Compose(intent,ParentPlacement);
        Body=NativeFreshTiledFamilyBodyPlan.Compose(Terrain);
        Executable=NativeFreshTiledFamilyExecutablePlan.Compose(Body,ParentExecutable);
        Clearance=NativeFreshTiledFamilySpawnClearancePlan.Compose(Executable,ParentClearance);
        Clearance.RequireStaticLandingClearance(Executable,ParentClearance);
        Disc=NativeFreshTiledFamilyDiscPlan.Compose(Executable,ParentDisc);
        RequireAncestry(saved);
        var snapshot=saved.Snapshot;
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            Profile="saved-exact-four-tile-r03-family-compilation-v1",CanonicalDocumentSha256,
            snapshot.ProjectId,snapshot.RootPath,snapshot.Path,snapshot.ContentSha256,snapshot.ManifestSha256,
            SourceImagePath,BaseDocumentSha256=Hash(NativeFreshLevelDocumentCodec.SerializeCanonical(BaseDocument)),
            BaseBody=BaseBody.PlanSha256,Composition=Composition.PlanSha256,
            ParentPlacement=ParentPlacement.PlanSha256,ParentClearance=ParentClearance.PlanSha256,
            ParentExecutable=ParentExecutable.PlanSha256,ParentDisc=ParentDisc.PlanSha256,
            Terrain=Terrain.PlanSha256,Body=Body.PlanSha256,Executable=Executable.PlanSha256,
            Clearance=Clearance.PlanSha256,Disc=Disc.PlanSha256,
            RuntimeVerified,PublicationApproved,CompleteNativeDependencyClosureProven,KeyZeroContributionProven}));
    }

    internal NativeFreshSavedLevelDocumentReadContext Saved{get;}
    internal NativeFreshLevelDocumentIntent Document=>Saved.Document;
    internal NativeFreshLevelDocumentIntent BaseDocument=>_assembly.BaseDocument;
    internal NativeId65FreshWizardResearchBodyPlan BaseBody=>_assembly.BaseBody;
    internal NativeFreshInitialSpawnClearancePlan WizardClearance=>_assembly.WizardClearance;
    internal NativeFreshMobyFamilyCompositionPlan Composition=>_assembly.Composition;
    internal NativeFreshLockedChestPlacementPlan ParentPlacement=>_assembly.Placement;
    internal NativeFreshLockedChestSpawnClearancePlan ParentClearance=>_assembly.Clearance;
    internal NativeFreshLockedChestExperimentalExecutable ParentExecutable=>_assembly.Executable;
    internal NativeFreshLockedChestResearchDiscPlan ParentDisc=>_assembly.Disc;
    internal NativeFreshTiledFamilyTerrainIntent Intent{get;}
    internal NativeFreshTiledFamilyTerrainRevision Terrain{get;}
    internal NativeFreshTiledFamilyBodyPlan Body{get;}
    internal NativeFreshTiledFamilyExecutablePlan Executable{get;}
    internal NativeFreshTiledFamilySpawnClearancePlan Clearance{get;}
    internal NativeFreshTiledFamilyDiscPlan Disc{get;}
    internal string SourceImagePath=>_assembly.SourceImagePath;
    internal string CanonicalDocumentSha256{get;}
    internal string PlanSha256{get;}
    internal bool RuntimeVerified=>false;
    internal bool PublicationApproved=>false;
    internal bool CompleteNativeDependencyClosureProven=>false;
    internal bool KeyZeroContributionProven=>false;
    internal byte[] CopyCanonicalDocument()=>_canonical.ToArray();

    internal static NativeFreshLevelDocumentIntent ProjectBaseDocument(NativeFreshLevelDocumentIntent document)=>
        ProjectBaseDocument(NativeFreshTiledFamilyTerrainIntent.Create(document));

    private static NativeFreshLevelDocumentIntent ProjectBaseDocument(NativeFreshTiledFamilyTerrainIntent intent)
    {
        var document=intent.DocumentAncestor;intent.RequireAncestry(document);
        var content=document.TiledContent!;
        // Create() has validated the complete exact ordered union, floor Z,
        // spawn, settings, eight face descriptors and every actor/anchor. Derive
        // the resource rectangle from that union, not a fixture or replacement
        // saved document. Its Wizard and first descriptor occurrences are actual.
        int x=content.Tiles.Min(t=>t.X),y=content.Tiles.Min(t=>t.Y);
        int right=content.Tiles.Max(t=>checked(t.X+t.Width)),bottom=content.Tiles.Max(t=>checked(t.Y+t.Depth));
        var first=content.Tiles[0];
        var basis=NativeId65FreshDocumentIntent.Create(x,y,first.Z,checked(right-x),checked(bottom-y),
            content.SpawnX,content.SpawnY,content.Heading,first.FloorColor,content.SkyColor,content.MusicTrackId);
        if(basis.SpawnZ!=content.SpawnZ)
            throw new InvalidDataException("Saved tiled R03 resource projection lost the actual complete spawn height.");
        var materials=NativeFreshFloorMaterialsIntent.Create(intent.MaterialSelections[0],intent.MaterialSelections[1]);
        var projected=NativeFreshLevelDocumentIntent.Create(basis,[document.Placements[0]]).WithFloorMaterials(materials);
        _=NativeFreshWizardAuthoringShape.RequireSameLayoutMaterialDocumentShape(projected);
        return projected;
    }

    internal void RequireAncestry(NativeFreshSavedLevelDocumentReadContext saved)
    {
        if(!ReferenceEquals(saved,Saved))
            throw new InvalidDataException("Saved tiled family compilation requires its actual complete saved-document context, not an equal reissued owner.");
        saved.RequireDocument(Document);Intent.RequireAncestry(Document);
        var projected=ProjectBaseDocument(Intent);
        if(!ReferenceEquals(BaseDocument.Placements[0],Document.Placements[0])||
            !ReferenceEquals(BaseDocument.FloorMaterials!.Triangle0,Intent.MaterialSelections[0])||
            !ReferenceEquals(BaseDocument.FloorMaterials.Triangle1,Intent.MaterialSelections[1])||
            !NativeFreshLevelDocumentCodec.SerializeCanonical(projected).AsSpan().SequenceEqual(
                NativeFreshLevelDocumentCodec.SerializeCanonical(BaseDocument))||
            !_canonical.AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(Document))||
            !_canonical.AsSpan().SequenceEqual(Intent.CopyCanonicalDocument())||
            CanonicalDocumentSha256!=Intent.CanonicalDocumentSha256)
            throw new InvalidDataException("Saved tiled family compilation lost actual complete tiled authorship or subordinate resource ownership.");
        _assembly.RequireAncestry(Document,BaseDocument);
        Terrain.RequireAncestry(Intent,ParentPlacement);
        Body.RequireAncestry(Terrain);
        Executable.RequireAncestry(Body,ParentExecutable);
        Clearance.RequireStaticLandingClearance(Executable,ParentClearance);
        Disc.RequireAncestry(Executable,ParentDisc);
    }

    internal static async Task<NativeFreshSavedTiledFamilyCompilation> CaptureAsync(string image,
        NativeFreshSavedLevelDocumentReadContext saved,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(saved);
        // An already stale revision is refused before beginning any operation.
        NativeFreshDocumentStorage.RequireAggregateCurrent(saved.Snapshot,token);
        NativeFreshSavedTiledFamilyCompilation? result=null;List<Exception> failures=[];
        try
        {
            var document=saved.Document;saved.RequireDocument(document);
            var intent=NativeFreshTiledFamilyTerrainIntent.Create(document);
            var projected=ProjectBaseDocument(intent);
            ArgumentException.ThrowIfNullOrWhiteSpace(image);string path=Path.GetFullPath(image);
            var assembly=await NativeFreshSavedR03FamilyAssembly.CaptureAsync(path,document,projected,token);
            result=new(saved,intent,assembly);
            token.ThrowIfCancellationRequested();
        }
        catch(Exception error){failures.Add(error);}
        finally
        {
            // Typed captures own Original checks; a future outer Candidate must
            // hold the complete BIN/CUE pair. Never adopt a saved replacement,
            // including after projection, path, source or cancellation failure.
            try{NativeFreshDocumentStorage.RequireAggregateCurrent(saved.Snapshot,CancellationToken.None);}
            catch(Exception error){failures.Add(error);}
        }
        NativeFreshResearchSourceOwners.ThrowFailures(failures);token.ThrowIfCancellationRequested();
        return result??throw new InvalidDataException("Saved tiled family source compilation returned no result.");
    }

    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Scene;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65FreshWizardSceneSmoke")]

namespace Spyro.Editor.Core.Exporting;

// Set only by private construction/named factories; never a caller policy flag.
internal enum NativeFreshWizardSceneProducer { FixedA, Authored, MaterialAuthored, SameLayoutMaterialAuthored }

public sealed record NativeFreshWizardSceneOwner(string Id,int Offset,int ByteLength,string Sha256,
    string Provenance,string? SourceOwnerId,int? SourceOffset);
public sealed record NativeFreshWizardScenePointerFixup(string SourceOwnerId,int SourceOffset,int SceneOffset,int Before,int After);

/// <summary>Neutral fixed P1/N32 byte grammar. Neither caller bytes nor this internal layout issue source authority.</summary>
internal sealed class NativeId65FreshWizardSceneLayout
{
    private readonly byte[] _scene;
    private NativeId65FreshWizardSceneLayout(ReadOnlySpan<byte> p0,ReadOnlySpan<byte> sourceRow,
        ReadOnlySpan<byte> sourcePrivate,NativeWizardPlacementIntent intent,NativeId65FreshDocumentIntent? authoredBase=null)
    {
        if(authoredBase is null)ValidatePlacement(intent);
        else NativeFreshWizardAuthoringShape.RequirePlacement(intent,authoredBase);
        // Bound every input before decoding, hashing, cloning or allocating.
        if(p0.Length!=0x3000 || sourceRow.Length!=0x58 || sourcePrivate.Length!=0x50)
            throw new InvalidDataException("Fresh Wizard scene needs complete fixed P0/row/private owners.");
        var original=NativeId65FreshSceneLayout.DecodeCanonical(p0);
        var expectedSpawn=authoredBase is null?new NativeId65SceneSpawn(8960,8960,9216,64):
            new NativeId65SceneSpawn(authoredBase.SpawnX,authoredBase.SpawnY,authoredBase.SpawnZ,authoredBase.Heading);
        if(original.NativeNominalCapacity!=16 || original.Spawn!=expectedSpawn ||
            Hash(sourceRow)!="59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82" ||
            Hash(sourcePrivate)!="5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b")
            throw new InvalidDataException("Fixed A/N16 and complete Wizard33/T6 source-form bytes required.");
        if(I32(sourceRow,0)!=0xC350 || I32(sourcePrivate,0)!=0xC378 ||
            I32(sourcePrivate,0x28)!=2 || I32(sourcePrivate,0x2C)!=-65536 ||
            I32(sourcePrivate,0x38)-I32(sourceRow,20)!=287 || I32(sourcePrivate,0x48)!=I32(sourcePrivate,0x38))
            throw new InvalidDataException("Exact source route/fixup scalar schema changed.");
        // The neutral storage kernel owns byte geometry only. Keep this exact
        // old source/placement validator, mutation ledger and public lineage.
        _scene=NativeFreshMobySceneStorageLayout.Create([0x50],32,2).Compile(p0[..0xCC],
            [new("wizard",sourceRow,sourcePrivate)],[
                new(0,NativeFreshMobySceneOwnerKind.Row,0,0,NativeFreshMobySceneOwnerKind.Private,0,0xC350),
                new(0,NativeFreshMobySceneOwnerKind.Private,0,0,NativeFreshMobySceneOwnerKind.Private,0x28,0xC378)]).CopyScene();
        PointerFixups=Array.AsReadOnly(new[]{
            new NativeFreshWizardScenePointerFixup("wizard.row",0,0xD4,0xC350,0xF88),
            new NativeFreshWizardScenePointerFixup("wizard.private",0,0xF88,0xC378,0xFB0)});
        foreach(var field in PointerFixups)Put(_scene,field.SceneOffset,field.After);
        var mutations=new List<NativeWizardPlacementCoordinateMutation>();
        Coordinate("instance.x",0xD4+12,intent.OriginX);Coordinate("instance.y",0xD4+16,intent.OriginY);
        Coordinate("instance.z",0xD4+20,intent.OriginZ);
        Coordinate("route0.x",0xF88+0x30,intent.OriginX);Coordinate("route0.y",0xF88+0x34,intent.OriginY);
        Coordinate("route0.z",0xF88+0x38,intent.OriginZ+287);
        Coordinate("route1.x",0xF88+0x40,intent.EndX);Coordinate("route1.y",0xF88+0x44,intent.EndY);
        Coordinate("route1.z",0xF88+0x48,intent.OriginZ+287);
        Mutations=mutations.AsReadOnly();
        var owners=new List<NativeFreshWizardSceneOwner>();
        SourceRange("fresh.scene",0,0,0xCC);
        Generated("Moby-component-length-and-one-placed-row",0xCC,8,false);
        SourceRange("wizard.row",0xD4,0,0x58);
        Generated("32-complete-upward-dynamic-rows",0x12C,32*0x58,true);
        Generated("complete-next-row-lookahead-reserve",NextRowReserveOffset,NextRowReserveByteLength,true);
        Generated("32-complete-downward-dynamic-properties",DynamicPropertiesOffset,DynamicPropertiesByteLength,true);
        Generated("placed-properties-component-length",0xF84,4,false);
        SourceRange("wizard.private",0xF88,0,0x50);
        Generated("empty-numeric-pod-component",0xFD8,8,false);
        Generated("spatial-bucket-component-length",0xFE0,4,false);
        Generated("two-complete-zero-spatial-bucket-banks",0xFE4,0x2000,true);
        Generated("two-scene-pointer-fixups",0x2FE4,12,false);
        Generated("sector-padding",0x2FF0,0x10,true);
        Owners=owners.AsReadOnly();
        int cursor=0;
        foreach(var o in Owners)
        {if(o.Offset!=cursor || o.ByteLength<=0)throw new InvalidDataException("Scene lost its contiguous byte partition.");cursor+=o.ByteLength;}
        if(cursor!=_scene.Length || 0xF84-DynamicArenaOffset!=32*0x70+0x58 ||
            NextRowReserveOffset!=DynamicArenaOffset+32*0x58 || DynamicPropertiesOffset!=NextRowReserveOffset+0x58 ||
            DynamicPropertiesOffset+32*0x18!=0xF84)
            throw new InvalidDataException("P1/N32 requires all32 independent row/property pairs AND the full next-row reserve.");
        RuntimeAnchors=Array.AsReadOnly(new[]{
            new NativeId65SceneRuntimeAnchor("placed-row-base",0x80075828,0xD4,4,false),
            new NativeId65SceneRuntimeAnchor("dynamic-row-base",0x80075890,0x12C,4,false),
            new NativeId65SceneRuntimeAnchor("dynamic-row-cursor",0x8007573C,0x12C,4,false),
            new NativeId65SceneRuntimeAnchor("dynamic-properties-end",0x800756E8,0xF84,4,true),
            new NativeId65SceneRuntimeAnchor("dynamic-properties-cursor",0x80075930,0xF84,4,true),
            new NativeId65SceneRuntimeAnchor("two-spatial-bucket-banks",0x80075778,0xFE4,4,false)});
        return;

        void Coordinate(string id,int at,int after)
        {mutations.Add(new(id,at,I32(_scene,at),after));Put(_scene,at,after);}
        void Add(string id,int at,int length,string provenance,string? source,int? from)=>(owners).Add(
            new(id,at,length,Hash(_scene.AsSpan(at,length)),provenance,source,from));
        void Generated(string id,int at,int length,bool zero)
        {
            if(zero && _scene.AsSpan(at,length).ContainsAnyExcept((byte)0))throw new InvalidDataException("Fresh zero owner changed.");
            Add(id,at,length,zero?"generated-zero":"generated-native-fields",null,null);
        }
        void SourceRange(string id,int at,int from,int length)
        {
            var changes=mutations.Select(m=>(Offset:m.SceneOffset,Kind:"authored-coordinate")).Concat(
                PointerFixups.Select(p=>(Offset:p.SceneOffset,Kind:"source-pointer-reencoding")))
                .Where(m=>m.Offset>=at&&m.Offset<at+length).OrderBy(m=>m.Offset);
            int next=at;
            foreach(var change in changes)
            {
                if(change.Offset<next || change.Offset+4>at+length)throw new InvalidDataException("Source patch owners overlap or escape.");
                if(next<change.Offset)Add(id+":"+next.ToString("X"),next,change.Offset-next,"retained-source",id,from+next-at);
                Add(id+":"+change.Offset.ToString("X"),change.Offset,4,change.Kind,id,from+change.Offset-at);next=change.Offset+4;
            }
            if(next<at+length)Add(id+":"+next.ToString("X"),next,at+length-next,"retained-source",id,from+next-at);
        }
    }
    internal int DynamicArenaOffset=>0x12C;
    internal int DynamicArenaByteLength=>0xE58;
    internal int NativeNominalCapacity=>32;
    internal int NextRowReserveOffset=>0xC2C;
    internal int NextRowReserveByteLength=>0x58;
    internal int DynamicPropertiesOffset=>0xC84;
    internal int DynamicPropertiesByteLength=>0x300;
    internal bool SourceCertified=>false;
    internal IReadOnlyList<NativeFreshWizardSceneOwner> Owners{get;}
    internal IReadOnlyList<NativeWizardPlacementCoordinateMutation> Mutations{get;}
    internal IReadOnlyList<NativeFreshWizardScenePointerFixup> PointerFixups{get;}
    internal IReadOnlyList<NativeId65SceneRuntimeAnchor> RuntimeAnchors{get;}
    internal byte[] CopyScene()=>_scene.ToArray();
    internal void RequireExactScene(ReadOnlySpan<byte> bytes)
    {if(!bytes.SequenceEqual(_scene))throw new InvalidDataException("Complete fresh Wizard scene owner or complement changed.");}
    internal static NativeId65FreshWizardSceneLayout Compile(ReadOnlySpan<byte> p0,ReadOnlySpan<byte> row,
        ReadOnlySpan<byte> properties,NativeWizardPlacementIntent intent)=>new(p0,row,properties,intent);
    internal static NativeId65FreshWizardSceneLayout CompileAuthored(ReadOnlySpan<byte> p0,ReadOnlySpan<byte> row,
        ReadOnlySpan<byte> properties,NativeWizardPlacementIntent intent,NativeId65FreshDocumentIntent authoredBase)
    {ArgumentNullException.ThrowIfNull(authoredBase);return new(p0,row,properties,intent,authoredBase);}
    internal static void ValidatePlacement(NativeWizardPlacementIntent intent)
    {
        NativeWizardPlacementGeometry.Validate(intent);
        if(intent.OriginX<=8448 || intent.OriginX>=9472 || intent.OriginY<=8448 || intent.OriginY>=9472 ||
            intent.EndX<=8448 || intent.EndX>=9472 || intent.EndY<=8448 || intent.EndY>=9472 ||
            intent.OriginZ<8704 || intent.OriginZ>9216)
            throw new InvalidDataException("Geometric Wizard endpoints must be strictly inside fixed A floor; origin Z must be floor..floor+512.");
        // No body radius, trajectory, corridor, spawn separation or future bucket-index certificate.
    }
    private static int I32(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
    private static void Put(Span<byte> b,int at,int value)=>BinaryPrimitives.WriteInt32LittleEndian(b[at..],value);
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}

/// <summary>
/// Detached P1/N32 research scene bound to the actual fixed A/N16 compilation,
/// Wizard33/T6 family and their issued resource result. No existing P0/body API
/// accepts this type. Ordinal0 is scene-only, not a persistent/card owner.
/// Placement is geometric intent, not the old Dark corridor or native safety.
/// </summary>
public sealed class NativeId65FreshWizardResearchScenePlan
{
    private readonly byte[]? _materialCanonical;
    private NativeId65FreshWizardResearchScenePlan(NativeId65FreshDocumentCompilation compilation,
        NativeWizardFamilyAssetPlan family,NativeId65FreshWizardResearchResourcePlan resources,NativeWizardPlacementIntent placement,
        NativeFreshLevelDocumentIntent? authoredDocument=null,NativeFreshWizardSceneProducer? producer=null)
    {
        Producer=producer??(authoredDocument is null?NativeFreshWizardSceneProducer.FixedA:NativeFreshWizardSceneProducer.Authored);
        resources.RequireAncestry(compilation,family);
        RequireProducerInputs(compilation,family,resources,placement,authoredDocument,Producer);
        if(!compilation.CopyCanonicalDocument().AsSpan().SequenceEqual(NativeId65FreshDocumentCodec.SerializeCanonical(compilation.Intent)))
            throw new InvalidDataException("Actual canonical source compilation changed.");
        var fresh=compilation.FreshResourceAncestor;
        NativeId65FreshSceneSource.ValidateSourceExecutable(fresh.ResourceChainAncestor.Resources.Surface.CopySourceExecutable());
        byte[] scene=fresh.CopyScene();
        if((authoredDocument is null && Hash(scene)!="4a6e7b5c6c53199b8a7aa6ae0d24ac9bce32634aa259d5f49535282080641433") ||
            fresh.SourceImageSha256!=SourceImageSha256 || family.SourceImageSha256!=SourceImageSha256 || resources.SourceImageSha256!=SourceImageSha256 ||
            !family.SceneReferences.SequenceEqual(new[]{
                new NativeWizardFamilySceneReference("wizard.row",0,"wizard.private",0,0xC350),
                new NativeWizardFamilySceneReference("wizard.private",0,"wizard.private",0x28,0xC378)}))
            throw new InvalidDataException("Fixed scene/source or complete two-field family fixup schema changed.");
        CompilationAncestor=compilation;FamilyAncestor=family;ResourceAncestor=resources;Placement=placement;AuthoringDocumentAncestor=authoredDocument;
        Layout=authoredDocument is null
            ?NativeId65FreshWizardSceneLayout.Compile(scene,family.CopyOwner("wizard.row"),family.CopyOwner("wizard.private"),placement)
            :NativeId65FreshWizardSceneLayout.CompileAuthored(scene,family.CopyOwner("wizard.row"),family.CopyOwner("wizard.private"),placement,authoredDocument.Base);
        SceneSha256=Hash(Layout.CopyScene());
        _materialCanonical=Producer is NativeFreshWizardSceneProducer.MaterialAuthored or NativeFreshWizardSceneProducer.SameLayoutMaterialAuthored
            ?NativeFreshLevelDocumentCodec.SerializeCanonical(authoredDocument!):null;
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            ProfileId=BindingProfileId,SourceImageSha256,CanonicalDocumentSha256,FamilyPlanSha256=family.PlanSha256,ResourcePlanSha256=resources.PlanSha256,
            BaseSceneSha256=fresh.SceneSha256,SceneSha256,Placement,PlacedCount,SceneOrdinal,NativeNominalCapacity,DynamicArenaByteLength,
            Owners,Mutations,PointerFixups,RuntimeAnchors,PendingOwners}));
        // New arm only: the complete old-shaped scene evidence is a hash child.
        // Existing schema1/fixed JSON statement and its bytes above are unchanged.
        if(_materialCanonical is not null)
            PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{ScenePlanSha256=PlanSha256,MaterialCanonicalSha256}));
    }
    internal NativeFreshWizardSceneProducer Producer{get;}
    internal string? MaterialCanonicalSha256=>_materialCanonical is null?null:Hash(_materialCanonical);
    internal byte[] CopyMaterialCanonicalDocument()=>_materialCanonical?.ToArray()??
        throw new InvalidDataException("Scene has no material-authored aggregate.");
    internal NativeId65FreshDocumentCompilation CompilationAncestor{get;}
    internal NativeWizardFamilyAssetPlan FamilyAncestor{get;}
    internal NativeId65FreshWizardResearchResourcePlan ResourceAncestor{get;}
    internal NativeFreshLevelDocumentIntent? AuthoringDocumentAncestor{get;}
    internal NativeId65FreshWizardSceneLayout Layout{get;}
    internal void RequireAncestry(NativeId65FreshDocumentCompilation c,NativeWizardFamilyAssetPlan f,NativeId65FreshWizardResearchResourcePlan r)
    {if(!ReferenceEquals(c,CompilationAncestor)||!ReferenceEquals(f,FamilyAncestor)||!ReferenceEquals(r,ResourceAncestor))throw new InvalidDataException("Scene belongs to another actual issued triple.");r.RequireAncestry(c,f);RequireProducerInputs(c,f,r,Placement,AuthoringDocumentAncestor,Producer);
        if(_materialCanonical is not null&&!_materialCanonical.AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(AuthoringDocumentAncestor!)))
            throw new InvalidDataException("Actual material-authored aggregate canonical ownership changed.");}
    public const string ProfileId="fresh-A-wizard33-T6-P1-N32-detached-scene-research-v1";
    public string BindingProfileId=>Producer==NativeFreshWizardSceneProducer.SameLayoutMaterialAuthored
        ?"fresh-schema2-same-layout-material-authored-wizard33-T6-P1-N32-detached-scene-v1"
        :Producer==NativeFreshWizardSceneProducer.MaterialAuthored
        ?"fresh-schema2-material-authored-wizard33-T6-P1-N32-detached-scene-v1"
        :AuthoringDocumentAncestor is null?ProfileId:"fresh-authored-same-layout-wizard33-T6-P1-N32-detached-scene-v1";
    public string SourceImageSha256=>NativeMobySourceInventory.OriginalImageSha256;
    public string CanonicalDocumentSha256=>CompilationAncestor.CanonicalSha256;
    public string SceneSha256{get;}
    public string PlanSha256{get;}
    public NativeWizardPlacementIntent Placement{get;}
    public int PlacedCount=>1;
    public int SceneOrdinal=>0;
    public int NativeNominalCapacity=>32;
    public int DynamicArenaByteLength=>0xE58;
    public IReadOnlyList<NativeFreshWizardSceneOwner> Owners=>Layout.Owners;
    public IReadOnlyList<NativeWizardPlacementCoordinateMutation> Mutations=>Layout.Mutations;
    public IReadOnlyList<NativeFreshWizardScenePointerFixup> PointerFixups=>Layout.PointerFixups;
    public IReadOnlyList<NativeId65SceneRuntimeAnchor> RuntimeAnchors=>Layout.RuntimeAnchors;
    public IReadOnlyList<string> PendingOwners{get;}=Array.AsReadOnly(new[]{
        "Distinct Town role callbacks/effect routing and complete reachable dependency graph are not installed.",
        "Global1F occupied selector compatibility, audio, reward/collected bitmap/ID65 durable save ownership remain unissued.",
        "N32 authored storage is not measured peak demand; native allocation33 can corrupt live owners and is not safely refused.",
        "Geometric route endpoints are not body/trajectory/clearance or the old Dark512 corridor; all later bucket coordinates require separate closure.",
        "No resource body/header/overlay/SPU staging/disc/publication or native runtime is issued by this scene stage."});
    public bool RuntimeVerified=>false;
    public bool PublicationApproved=>false;
    public bool GlobalAudioCompatibilityProven=>false;
    public bool CompleteNativeDependencyClosureProven=>false;
    public bool NativeArenaExhaustionSafe=>false;
    public bool BodyIssued=>false;
    public bool PersistentOwnerIssued=>false;
    public bool AuthoredDryCorridorAccepted=>false;
    public bool NativeBodyAndTrajectoryProven=>false;
    public byte[] CopyScene()=>Layout.CopyScene();
    public byte[] CopyCanonicalDocument()=>CompilationAncestor.CopyCanonicalDocument();
    public void RequireExactScene(ReadOnlySpan<byte> bytes)=>Layout.RequireExactScene(bytes);

    public static async Task<NativeId65FreshWizardResearchScenePlan> CaptureAsync(string originalBin,
        NativeId65FreshDocumentCompilation compilation,NativeWizardFamilyAssetPlan family,
        NativeId65FreshWizardResearchResourcePlan resources,NativeWizardPlacementIntent placement,CancellationToken token=default)
        =>await CaptureCoreAsync(originalBin,compilation,family,resources,placement,null,token);

    /// <summary>One actual aggregate-owned route and its compiled Base; no safety or output permission is inferred.</summary>
    public static async Task<NativeId65FreshWizardResearchScenePlan> CaptureAuthoredAsync(string originalBin,
        NativeFreshLevelDocumentIntent document,NativeId65FreshDocumentCompilation compilation,NativeWizardFamilyAssetPlan family,
        NativeId65FreshWizardResearchResourcePlan resources,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(compilation);ArgumentNullException.ThrowIfNull(family);ArgumentNullException.ThrowIfNull(resources);
        var placement=NativeFreshWizardAuthoringShape.RequireDocument(document,compilation);
        return await CaptureCoreAsync(originalBin,compilation,family,resources,placement,document,token);
    }

    // Actual schema2 ownership only: same detached scene grammar, not a body.
    // This separate arm has independent all-owner finalization; old paths below
    // retain their existing validation/JSON/finally behavior byte-for-byte.
    internal static Task<NativeId65FreshWizardResearchScenePlan> CaptureMaterialAuthoredAsync(string originalBin,
        NativeFreshLevelDocumentIntent document,NativeId65FreshDocumentCompilation compilation,NativeWizardFamilyAssetPlan family,
        NativeId65FreshWizardResearchResourcePlan resources,CancellationToken token=default)=>
        CaptureMaterialCoreAsync(originalBin,document,compilation,family,resources,NativeFreshWizardSceneProducer.MaterialAuthored,token);

    internal static Task<NativeId65FreshWizardResearchScenePlan> CaptureSameLayoutMaterialAuthoredAsync(string originalBin,
        NativeFreshLevelDocumentIntent document,NativeId65FreshDocumentCompilation compilation,NativeWizardFamilyAssetPlan family,
        NativeId65FreshWizardResearchResourcePlan resources,CancellationToken token=default)=>
        CaptureMaterialCoreAsync(originalBin,document,compilation,family,resources,NativeFreshWizardSceneProducer.SameLayoutMaterialAuthored,token);

    // Share acquisition/finalization without broadening either named producer.
    private static async Task<NativeId65FreshWizardResearchScenePlan> CaptureMaterialCoreAsync(string originalBin,
        NativeFreshLevelDocumentIntent document,NativeId65FreshDocumentCompilation compilation,NativeWizardFamilyAssetPlan family,
        NativeId65FreshWizardResearchResourcePlan resources,NativeFreshWizardSceneProducer producer,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(compilation);ArgumentNullException.ThrowIfNull(family);ArgumentNullException.ThrowIfNull(resources);
        var placement=RequireMaterialDocument(document,compilation,producer);
        RequireProducerInputs(compilation,family,resources,placement,document,producer);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);string path=Path.GetFullPath(originalBin);
        FileStream? held=null;NativeId65FreshWizardResearchScenePlan? result=null;List<Exception> failures=[];
        try
        {
            held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            await Original(held,token);
            token.ThrowIfCancellationRequested();
            result=new(compilation,family,resources,placement,document,producer);
            token.ThrowIfCancellationRequested();
        }
        catch(Exception error){failures.Add(error);}
        finally
        {
            if(held is not null)await Observe(()=>Original(held,CancellationToken.None));
            FileStream? current=null;
            try
            {
                current=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
                await Original(current,CancellationToken.None);
            }
            catch(Exception error){failures.Add(error);}
            finally{if(current is not null)Close(current);}
            if(held is not null)Close(held);
        }
        NativeFreshResearchSourceOwners.ThrowFailures(failures);
        token.ThrowIfCancellationRequested();
        return result??throw new InvalidDataException("Material-authored Scene returned no actual result.");
        static Task Original(FileStream file,CancellationToken cancellation)
        {
            file.Position=0;
            return NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(file,
                NativeMobySourceInventory.OriginalImageByteLength,NativeMobySourceInventory.OriginalImageSha256,cancellation);
        }
        async Task Observe(Func<Task> action){try{await action();}catch(Exception error){failures.Add(error);}}
        void Close(FileStream file){try{file.Dispose();}catch(Exception error){failures.Add(error);}}
    }

    private static void RequireProducerInputs(NativeId65FreshDocumentCompilation compilation,NativeWizardFamilyAssetPlan family,
        NativeId65FreshWizardResearchResourcePlan resources,NativeWizardPlacementIntent placement,
        NativeFreshLevelDocumentIntent? document,NativeFreshWizardSceneProducer producer)
    {
        if(producer is NativeFreshWizardSceneProducer.MaterialAuthored or NativeFreshWizardSceneProducer.SameLayoutMaterialAuthored)
        {
            resources.RequireAncestry(compilation,family);resources.RequireCompilationProfile();
            if(resources.ResourceProfile!=NativeFreshWizardResourceProfile.AuthoredSameLayout||
                RequireMaterialDocument(document!,compilation,producer)!=placement)
                throw new InvalidDataException("Material-authored Scene requires its actual authored resource and aggregate route.");
            return;
        }
        if(producer!=(document is null?NativeFreshWizardSceneProducer.FixedA:NativeFreshWizardSceneProducer.Authored))
            throw new InvalidDataException("Unknown or mismatched closed Wizard Scene producer.");
        RequireCaptureInputs(compilation,family,resources,placement,document);
    }

    private static NativeWizardPlacementIntent RequireMaterialDocument(NativeFreshLevelDocumentIntent document,
        NativeId65FreshDocumentCompilation compilation,NativeFreshWizardSceneProducer producer)=>producer switch
    {
        NativeFreshWizardSceneProducer.MaterialAuthored=>NativeFreshWizardAuthoringShape.RequireMaterialDocument(document,compilation),
        NativeFreshWizardSceneProducer.SameLayoutMaterialAuthored=>NativeFreshWizardAuthoringShape.RequireSameLayoutMaterialDocument(document,compilation),
        _=>throw new InvalidDataException("Unknown closed material Scene producer.")
    };

    private static void RequireCaptureInputs(NativeId65FreshDocumentCompilation compilation,NativeWizardFamilyAssetPlan family,
        NativeId65FreshWizardResearchResourcePlan resources,NativeWizardPlacementIntent placement,NativeFreshLevelDocumentIntent? document)
    {
        resources.RequireAncestry(compilation,family);resources.RequireCompilationProfile();
        if(document is null)
        {
            if(resources.ResourceProfile!=NativeFreshWizardResourceProfile.FixedA)
                throw new InvalidDataException("Legacy fixed scene cannot consume an authored-profile resource.");
            NativeId65FreshWizardResearchResourceSource.RequireFixedIntent(compilation.Intent);
            NativeId65FreshWizardSceneLayout.ValidatePlacement(placement);
        }
        else if(resources.ResourceProfile!=NativeFreshWizardResourceProfile.AuthoredSameLayout ||
            NativeFreshWizardAuthoringShape.RequireDocument(document,compilation)!=placement)
            throw new InvalidDataException("Authored scene requires its actual aggregate route and authored resource producer.");
    }

    private static async Task<NativeId65FreshWizardResearchScenePlan> CaptureCoreAsync(string originalBin,
        NativeId65FreshDocumentCompilation compilation,NativeWizardFamilyAssetPlan family,
        NativeId65FreshWizardResearchResourcePlan resources,NativeWizardPlacementIntent placement,
        NativeFreshLevelDocumentIntent? document,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(compilation);ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(resources);resources.RequireAncestry(compilation,family);
        RequireCaptureInputs(compilation,family,resources,placement,document);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);string path=Path.GetFullPath(originalBin);
        await using FileStream image=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        await NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,NativeMobySourceInventory.OriginalImageByteLength,
            NativeMobySourceInventory.OriginalImageSha256,token);
        try
        {
            token.ThrowIfCancellationRequested();
            var result=new NativeId65FreshWizardResearchScenePlan(compilation,family,resources,placement,document);
            token.ThrowIfCancellationRequested();return result;
        }
        finally
        {
            image.Position=0;
            await NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,NativeMobySourceInventory.OriginalImageByteLength,
                NativeMobySourceInventory.OriginalImageSha256,CancellationToken.None);
            token.ThrowIfCancellationRequested();
        }
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}

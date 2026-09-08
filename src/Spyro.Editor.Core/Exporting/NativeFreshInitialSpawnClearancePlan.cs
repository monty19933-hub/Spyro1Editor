using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

public enum NativeInitialSpawnClearanceStatus { Unknown, Overlap, Touching, InitialSeparation }
public enum NativeInitialSpawnUnknownReason
{ None, UnsupportedPose, UnsupportedAnimationEncoding, NullCollider, UnsupportedResponseSemantics, UnsupportedTransformRange }
public readonly record struct NativeSpawnClearancePoint(int X,int Y,int Z);
public sealed record NativeInitialSpawnClearanceEvidence(string Id,string Domain,int Offset,int ByteLength,string Sha256);

/// <summary>Immutable diagnostic facts, not a source certificate or whole-frame safety grant.</summary>
public sealed class NativeInitialSpawnClearanceFinding
{
    internal NativeInitialSpawnClearanceFinding(string editorId,NativeInitialSpawnClearanceStatus status,
        NativeInitialSpawnUnknownReason reason,NativeSpawnClearancePoint spawn,NativeSpawnClearancePoint? vertex=null,
        NativeSpawnClearancePoint? centre=null,int? radius=null,uint? flags=null,bool? response=null)
    {
        EditorId=editorId;Status=status;UnknownReason=reason;Spawn=spawn;Vertex=vertex;Centre=centre;
        ColliderRadius=radius;IncomingDamageFlags=flags;ResponsePermitted=response;
        if(centre is {} c && radius is {} r)
        {DistanceSquared=NativeInitialSpawnClearanceMath.SquaredDistance(c,spawn);CombinedRadiusSquared=BigInteger.Pow((BigInteger)r+PlayerRadius,2);}
    }
    public string EditorId{get;}
    public NativeInitialSpawnClearanceStatus Status{get;}
    public NativeInitialSpawnUnknownReason UnknownReason{get;}
    public NativeSpawnClearancePoint Spawn{get;}
    public NativeSpawnClearancePoint? Vertex{get;}
    public NativeSpawnClearancePoint? Centre{get;}
    public int PlayerRadius=>356;
    public int? ColliderRadius{get;}
    public uint? IncomingDamageFlags{get;}
    public bool? ResponsePermitted{get;}
    public BigInteger? DistanceSquared{get;}
    public BigInteger? CombinedRadiusSquared{get;}
}

/// <summary>
/// Required initial-pose assessment for future authored compilation admission.
/// The current source-owned Wizard factory deliberately covers exactly one
/// aggregate placement and its actual issued scene. It does not compile that
/// aggregate, alter legacy P1, or certify the first query, route, floor footprint,
/// mutable collision roster, gameplay, capacity or publication.
/// </summary>
public sealed class NativeFreshInitialSpawnClearancePlan
{
    private readonly NativeFreshLevelDocumentIntent _document;
    private readonly NativeId65FreshWizardResearchScenePlan _scene;
    private readonly byte[] _canonical;
    private NativeFreshInitialSpawnClearancePlan(NativeFreshLevelDocumentIntent document,
        NativeId65FreshWizardResearchScenePlan scene,NativeFreshInitialSpawnClearanceSource source)
    {
        _document=document;_scene=scene;_canonical=NativeFreshLevelDocumentCodec.SerializeCanonical(document);
        Finding=source.Finding;Evidence=source.Evidence;
        CanonicalDocumentSha256=NativeFreshInitialSpawnClearanceSource.Hash(_canonical);
        PlanSha256=NativeFreshInitialSpawnClearanceSource.Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            ProfileId,SourceImageSha256,CanonicalDocumentSha256,ScenePlanSha256,ResourcePlanSha256,FamilyPlanSha256,
            Finding.EditorId,Finding.Status,Finding.UnknownReason,Finding.Spawn,Finding.Vertex,Finding.Centre,
            Finding.PlayerRadius,Finding.ColliderRadius,Finding.IncomingDamageFlags,Finding.ResponsePermitted,
            distanceSquared=Finding.DistanceSquared?.ToString(CultureInfo.InvariantCulture),
            combinedRadiusSquared=Finding.CombinedRadiusSquared?.ToString(CultureInfo.InvariantCulture),Evidence,
            touchingPolicy="require-strict-separation",scope="authored-initial-identity-pose-only"}));
    }
    public const string ProfileId="native-fresh-initial-spawn-clearance-wizard33-T6-identity-v1";
    public string SourceImageSha256=>NativeMobySourceInventory.OriginalImageSha256;
    public string CanonicalDocumentSha256{get;}
    public string ScenePlanSha256=>_scene.PlanSha256;
    public string ResourcePlanSha256=>_scene.ResourceAncestor.PlanSha256;
    public string FamilyPlanSha256=>_scene.FamilyAncestor.PlanSha256;
    public string PlanSha256{get;}
    public NativeInitialSpawnClearanceFinding Finding{get;}
    public IReadOnlyList<NativeInitialSpawnClearanceEvidence> Evidence{get;}
    public bool SourceCertified=>true;
    public bool RuntimeVerified=>false;
    public bool FirstQueryProven=>false;
    public bool TrajectoryProven=>false;
    public bool PublicationApproved=>false;
    public byte[] CopyCanonicalDocument()=>_canonical.ToArray();

    internal void RequireAncestry(NativeFreshLevelDocumentIntent document,NativeId65FreshWizardResearchScenePlan scene,
        NativeId65FreshDocumentCompilation compilation,NativeWizardFamilyAssetPlan family,NativeId65FreshWizardResearchResourcePlan resources)
    {
        if(!ReferenceEquals(document,_document)||!ReferenceEquals(scene,_scene))
            throw new InvalidDataException("Initial clearance belongs to another actual document/scene, even when bytes agree.");
        scene.RequireAncestry(compilation,family,resources);
        NativeFreshInitialSpawnClearanceSource.RequireDocument(document,scene);
    }
    // A future aggregate compiler/new Candidate must call this with its ACTUAL
    // inputs. Merely receiving an assessment (including Unknown) is no admission.
    internal void RequireInitialSeparation(NativeFreshLevelDocumentIntent document,NativeId65FreshWizardResearchScenePlan scene)
    {
        RequireAncestry(document,scene,scene.CompilationAncestor,scene.FamilyAncestor,scene.ResourceAncestor);
        NativeInitialSpawnClearanceMath.RequireInitialSeparation(Finding);
    }
    public static async Task<NativeFreshInitialSpawnClearancePlan> CaptureWizardAsync(string originalBin,
        NativeFreshLevelDocumentIntent document,NativeId65FreshWizardResearchScenePlan scene,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(document);ArgumentNullException.ThrowIfNull(scene);
        NativeFreshInitialSpawnClearanceSource.RequireDocument(document,scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);
        await using FileStream image=new(Path.GetFullPath(originalBin),FileMode.Open,FileAccess.Read,FileShare.Read);
        return await NativeFreshInitialSpawnClearanceSource.WithOriginalAsync(image,()=>{
            token.ThrowIfCancellationRequested();
            var source=NativeFreshInitialSpawnClearanceSource.Capture(image,document,scene,token);
            var result=new NativeFreshInitialSpawnClearancePlan(document,scene,source);
            token.ThrowIfCancellationRequested();return result;
        },token);
    }
}

/// <summary>Exact reusable scalar geometry only: caller values do not issue source/pose authority.</summary>
internal static class NativeInitialSpawnClearanceMath
{
    internal static void RequireInitialSeparation(NativeInitialSpawnClearanceFinding finding)
    {
        if(finding.Status!=NativeInitialSpawnClearanceStatus.InitialSeparation)
            throw new InvalidDataException($"Initial spawn clearance refused for editor placement {finding.EditorId}: {finding.Status}/{finding.UnknownReason}.");
    }
    internal static BigInteger SquaredDistance(NativeSpawnClearancePoint a,NativeSpawnClearancePoint b)
    {BigInteger x=(BigInteger)a.X-b.X,y=(BigInteger)a.Y-b.Y,z=(BigInteger)a.Z-b.Z;return x*x+y*y+z*z;}
    internal static NativeInitialSpawnClearanceStatus Classify(NativeSpawnClearancePoint a,int radiusA,NativeSpawnClearancePoint b,int radiusB)
    {
        if(radiusA<0||radiusB<0)throw new InvalidDataException("An active sphere needs nonnegative radii; null shape is not radius zero.");
        int comparison=SquaredDistance(a,b).CompareTo(BigInteger.Pow((BigInteger)radiusA+radiusB,2));
        return comparison<0?NativeInitialSpawnClearanceStatus.Overlap:comparison==0?NativeInitialSpawnClearanceStatus.Touching:NativeInitialSpawnClearanceStatus.InitialSeparation;
    }
}

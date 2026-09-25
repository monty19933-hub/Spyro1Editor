using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeJapaneseEggPlacementSmoke")]

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeJapaneseEggPlacementNeighbor(int NativeIndex,ushort ActorClass,int X,int Y,int Z,
    byte NativeRadiusByte,long SquaredHorizontalDistance,string SourceRowSha256);

/// <summary>
/// Fixed developer visual-test sites on original Artisans lawn. Source-authenticated
/// flat dry support and center separation only; not collision volumes, camera,
/// safe traversal from spawn, flawless placement or native gameplay acceptance.
/// </summary>
public sealed class NativeJapaneseEggArtisansPlacementPlan
{
    public const int GroundZ=6144,HalfWidth=512,ClearHeight=2048,MinimumCenterSeparation=1536;
    private const string OriginalDataHash="86cde4f886d97821a3a4b869100b586842dbd4b9fb696558bead274bb547af4f";
    private NativeJapaneseEggArtisansPlacementPlan(NativeRouteSurfaceReport support,NativeJapaneseEggPlacementNeighbor[] neighbors)
    {
        Placements=Array.AsReadOnly(CopyFixedRecipe());Neighbors=Array.AsReadOnly(neighbors);
        SurfaceCertificateSha256=support.CertificateSha256;CollisionSha256=support.CollisionSha256;
        SupportTriangleCount=support.SupportBindings.Select(b=>b.Triangle).Distinct().Count();
        NativeCollisionBindingCount=support.SupportBindings.Count;
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{profile="original-artisans-two-f5-lawn-visual-v1",
            SourceDataSha256,SurfaceCertificateSha256,CollisionSha256,Placements,Neighbors,GroundZ,HalfWidth,ClearHeight,
            MinimumCenterSeparation,GameplayVerified}));
    }
    public string SourceDataSha256=>OriginalDataHash;
    public string PlanSha256 {get;}
    public string SurfaceCertificateSha256 {get;}
    public string CollisionSha256 {get;}
    public IReadOnlyList<NativeJapaneseEggVisualPlacement> Placements {get;}
    public IReadOnlyList<NativeJapaneseEggPlacementNeighbor> Neighbors {get;}
    public int SupportTriangleCount {get;}
    public int NativeCollisionBindingCount {get;}
    public bool SourceCertified=>true;
    public bool GameplayVerified=>false;
    public bool NormalExportApproved=>false;

    public static NativeJapaneseEggArtisansPlacementPlan Capture(NativeRegionalSourceSession usa)
    {
        ArgumentNullException.ThrowIfNull(usa);
        if(usa.Revision!=NativeDiscRevision.UsaScus94228)throw new InvalidDataException("Artisans placement requires actual USA source.");
        byte[] data=usa.ReadWadEntry(10),exe=usa.ReadExecutableRange(0,usa.ExecutableByteLength);
        if(Hash(data)!=OriginalDataHash)throw new InvalidDataException("Placement data is not the complete original Artisans.");
        NativeJapaneseEggMemoryReport.RequireSourceExecutable(exe);
        int scene=Int(data,0x18),environment=Int(data,8),length=Int(data,12);
        if(Int(data,scene)!=84992||Int(data,scene+4)!=47104||Int(data,scene+8)!=9564||data[scene+14]!=64)
            throw new InvalidDataException("Original entry spawn changed; research sites no longer have their inspected context.");
        var surface=NativeRouteSurfaceSnapshot.Capture(data.AsSpan(environment,length).ToArray(),usa.Entries[10].Offset+(long)environment);
        var positions=CopyFixedRecipe();
        foreach(var p in positions)
            if(NativeRouteSurfaceCertificate.FindUniqueFlatGround(surface,new(p.X,p.Y,p.Z))!=GroundZ)
                throw new InvalidDataException("A research site no longer has unique original flat ground.");
        var support=NativeRouteSurfaceCertificate.Evaluate(surface,positions.Select(p=>new NativeRoutePoint(p.X,p.Y,p.Z)).ToArray(),
            new(GroundZ,HalfWidth,ClearHeight));
        if(!support.Accepted||support.SupportBindings.Count==0)
            throw new InvalidDataException("The complete two-egg corridor lacks exact indexed dry support/clearance.");
        int moby=scene+0x88;for(int i=0;i<7;i++)moby=checked(moby+Int(data,moby));
        int count=Int(data,moby+4);if(count!=174)throw new InvalidDataException("Source row inventory changed.");
        var neighbors=new NativeJapaneseEggPlacementNeighbor[count];
        for(int i=0;i<count;i++)
        {
            int row=moby+8+i*0x58,x=Int(data,row+12),y=Int(data,row+16),z=Int(data,row+20);
            long distance=SquaredCorridorDistance(x,y);
            if(distance<MinimumCenterSeparation*(long)MinimumCenterSeparation)
                throw new InvalidDataException("An original actor center occupies the research separation envelope.");
            neighbors[i]=new(i,BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(row+0x36)),x,y,z,data[row+0x50],distance,Hash(data.AsSpan(row,0x58)));
        }
        usa.VerifyUnchanged();return new(support,neighbors);
    }
    public void RequireIntegration(NativeJapaneseEggArtisansIntegrationPlan integration)
    {
        ArgumentNullException.ThrowIfNull(integration);
        if(!integration.SourceCertified||Hash(integration.CopyOriginalLevelData())!=SourceDataSha256)
            throw new InvalidDataException("Placement requires actual original-source integration ancestry.");
        RequireFixedRecipe(integration.Rows.Select(r=>r.Placement).ToArray());
    }
    internal static NativeJapaneseEggVisualPlacement[] CopyFixedRecipe()=>
        [new("egg-a",72704,47104,6656,32,1),new("egg-b",72704,49152,6656,96,1)];
    internal static void RequireFixedRecipe(IReadOnlyList<NativeJapaneseEggVisualPlacement> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        if(positions.Count!=2||!positions.SequenceEqual(CopyFixedRecipe()))
            throw new InvalidDataException("Only the exact two original-mode1 source-inspected lawn sites are admitted.");
    }
    internal static long SquaredCorridorDistance(int x,int y)
    {
        long dx=x-72704L,dy=y<47104?47104L-y:y>49152?y-49152L:0;
        return checked(dx*dx+dy*dy);
    }
    private static int Int(byte[] bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at));
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}

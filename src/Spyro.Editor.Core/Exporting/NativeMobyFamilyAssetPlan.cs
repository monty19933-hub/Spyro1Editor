using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeMobyFamilyAssetOwner(string Id,long SourceWadOffset,int ByteLength,string Sha256);

/// <summary>
/// An actual original-source native family asset and row schema. Its graph
/// materializes a detached package; it has no retail/fresh destination binding,
/// registration, placement, output writer or import/runtime approval.
/// </summary>
public sealed class NativeMobyFamilyAssetPlan
{
    private readonly byte[] _data,_overlay,_row,_properties,_package;
    private readonly NativeAddressSpace _dataSource,_sceneSource,_overlaySource;
    private NativeMobyFamilyAssetPlan(byte[] data,byte[] overlay)
    {
        _data=data;_overlay=overlay;
        var donor=NativeGrassFamilySourceDecoder.DecodeDonor(data);
        var dispatch=NativeGrassFamilySourceDecoder.DecodeDonorDispatch(overlay);
        _row=donor.Row;_properties=donor.Properties;_package=donor.Package;
        _dataSource=new(NativeAddressKind.LevelData,NativeGrassFamilySourceDecoder.ArtisansDataSha256,
            NativeAddressRevision.Original,data.Length);
        _sceneSource=new(NativeAddressKind.Scene,Hash(data.AsSpan(0x1C9800,0x14800)),NativeAddressRevision.Original,0x14800);
        _overlaySource=new(NativeAddressKind.LevelData,NativeGrassFamilySourceDecoder.ArtisansOverlaySha256,
            NativeAddressRevision.Original,overlay.Length);
        DetachedModelDomain=new(NativeAddressKind.LevelData,"detached-artisans-t121-01f5-"+PackageSha256,
            NativeAddressRevision.Final,_package.Length);
        var packageRead=new NativeMobyDependencyRead("complete-registration",new(_dataSource,0x1C1520,0x174),PackageSha256);
        var codeRead=new NativeMobyDependencyRead("complete-donor-overlay",new(_overlaySource,0,overlay.Length),Hash(overlay));
        NativeMobyDependencyNode[] nodes=[
            new("grass.donor-dispatch",NativeMobyDependencyKind.NativeRoutine,NativeMobyDependencyResolution.Aggregate,
                NativeMobyDependencySharing.Immutable,dispatch.ExecutedTraceSha256,[],[codeRead]),
            new("grass.package",NativeMobyDependencyKind.ActorPackage,NativeMobyDependencyResolution.ImportFromSource,
                NativeMobyDependencySharing.Immutable,PackageSha256,["grass.donor-dispatch"],[packageRead],
                context=>new(context.Read("complete-registration"),[new("detached-package",new(DetachedModelDomain,0,0x174))],[]))];
        AssetGraph=NativeMobyDependencyClosureCompiler.Compile(nodes,["grass.package"],ReadSource);
        // Only this closed original-source issuer chooses model contributions.
        // Neutral package inputs themselves confer no family/import authority.
        ModelContributions=Array.AsReadOnly(new[]{new NativeModelTailPackage(NativeClass,"grass.package",_package)});
        InstanceTemplate=NativeMobyDependencyInstanceTemplate.Capture("source-assets-artisans-t121-01f5-private-copy-v1",
            [new("grass",121,0x01F5,new("row",new(_sceneSource,0xCC44,0x58),RowSha256),
                new("properties",new(_sceneSource,0x11ACC,8),PropertiesSha256),0,[],false)],[],AssetGraph,ReadSource);
        DonorDispatchSha256=dispatch.ExecutedTraceSha256;
        DonorPropertiesSharingIndices=Array.AsReadOnly(donor.SharingTrueIndices.ToArray());
        Owners=Array.AsReadOnly(new[]{
            new NativeMobyFamilyAssetOwner("row",0x800800L+0x1C9800+0xCC44,0x58,RowSha256),
            new("properties",0x800800L+0x1C9800+0x11ACC,8,PropertiesSha256),
            new("package",0x800800L+0x1C1520,0x174,PackageSha256)});
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            ProfileId,SourceImageSha256,DonorLevelKey,DonorWadEntry,DonorTrueIndex,NativeClass,
            Owners,DonorDispatchSha256,DonorPropertiesSharingIndices,AssetGraphSha256=AssetGraph.Fingerprint,
            SourceCertified,DestinationBound,NativeImportApproved,RuntimeVerified,PublicationApproved}));
    }

    public const string ProfileId="native-original-family-assets-artisans-t121-01f5-v1";
    public string SourceImageSha256=>NativeId65FreshDiscSource.OriginalSha256;
    public string DonorLevelKey=>"artisans";
    public int DonorWadEntry=>10;
    public int DonorTrueIndex=>121;
    public ushort NativeClass=>0x01F5;
    public string RowSha256=>Hash(_row);
    public string PropertiesSha256=>Hash(_properties);
    public string PackageSha256=>Hash(_package);
    public string DonorDispatchSha256{get;}
    public string PlanSha256{get;}
    public IReadOnlyList<NativeMobyFamilyAssetOwner> Owners{get;}
    public IReadOnlyList<int> DonorPropertiesSharingIndices{get;}
    public bool SourceCertified=>true;
    public bool DestinationBound=>false;
    public bool NativeImportApproved=>false;
    public bool RuntimeVerified=>false;
    public bool PublicationApproved=>false;
    public byte[] CopyRow()=>_row.ToArray();
    public byte[] CopyProperties()=>_properties.ToArray();
    public byte[] CopyPackage()=>_package.ToArray();
    internal NativeMobyDependencyClosurePlan AssetGraph{get;}
    internal IReadOnlyList<NativeModelTailPackage> ModelContributions{get;}
    internal NativeMobyDependencyInstanceTemplate InstanceTemplate{get;}
    internal NativeAddressSpace DetachedModelDomain{get;}
    internal byte[] ReadSource(NativeByteExtent extent)
    {
        ArgumentNullException.ThrowIfNull(extent);
        ReadOnlySpan<byte> bytes=ReferenceEquals(extent.Space,_dataSource)?_data:
            ReferenceEquals(extent.Space,_overlaySource)?_overlay:
            ReferenceEquals(extent.Space,_sceneSource)?_data.AsSpan(0x1C9800,0x14800):
            throw new InvalidDataException("Native family source belongs to another actual captured address domain.");
        return bytes.Slice(checked((int)extent.Offset),checked((int)extent.ByteLength)).ToArray();
    }
    internal void RequireRetainedOwners()
    {
        var donor=NativeGrassFamilySourceDecoder.DecodeDonor(_data);
        var dispatch=NativeGrassFamilySourceDecoder.DecodeDonorDispatch(_overlay);
        if(!donor.Row.SequenceEqual(_row)||!donor.Properties.SequenceEqual(_properties)||!donor.Package.SequenceEqual(_package)||
            dispatch.ExecutedTraceSha256!=DonorDispatchSha256||!AssetGraph.CopyPayload("grass.package").SequenceEqual(_package)||
            ModelContributions.Count!=1||ModelContributions[0].ActorId!=NativeClass||ModelContributions[0].Id!="grass.package"||
            !ModelContributions[0].CopyBytes().AsSpan().SequenceEqual(_package))
            throw new InvalidDataException("Native family retained donor or detached asset owners changed.");
        foreach(var read in AssetGraph.Nodes.SelectMany(n=>n.Reads).Concat(InstanceTemplate.Rows.SelectMany(r=>new[]{r.Row,r.Properties})))
            if(Hash(ReadSource(read.Extent))!=read.ExpectedSha256)
                throw new InvalidDataException("Native family retained graph/template preimage changed.");
    }

    /// <summary>
    /// Rebind this issuer's complete source-form model contributions into the
    /// actual composer's allocated Final domain. The current Grass source has
    /// only immutable source-copy packages and aggregate source evidence; this
    /// does not admit another family or silently relocate richer dependencies.
    /// </summary>
    internal NativeMobyDependencyClosurePlan BindCompositionModels(NativeAddressSpace final,
        IReadOnlyList<NativeModelTailBatchEntry> packages)
    {
        ArgumentNullException.ThrowIfNull(final);ArgumentNullException.ThrowIfNull(packages);RequireRetainedOwners();
        if(final.Kind!=NativeAddressKind.LevelData||final.Revision!=NativeAddressRevision.Final||
            packages.Count!=ModelContributions.Count||packages.Where((p,i)=>p is null||
                !ReferenceEquals(p.Input,ModelContributions[i])||p.ByteLength!=p.Input.ByteLength).Any()||AssetGraph.References.Count!=0)
            throw new InvalidDataException("Family models require their actual code-owned package inputs and a complete allocated Final domain.");
        List<NativeMobyDependencyNode> nodes=[];
        foreach(var node in AssetGraph.Nodes)
        {
            var entry=packages.SingleOrDefault(p=>p.Input.Id==node.Id);
            if(entry is null)
            {
                if(node.Resolution!=NativeMobyDependencyResolution.Aggregate||node.Preserved.Count!=0||node.PayloadSha256 is not null)
                    throw new InvalidDataException("This family source has an unresolved non-model destination dependency.");
                nodes.Add(new(node.Id,node.Kind,node.Resolution,node.Sharing,node.EvidenceSha256,node.Dependencies,node.Reads));
                continue;
            }
            if(node.Kind!=NativeMobyDependencyKind.ActorPackage||node.Resolution!=NativeMobyDependencyResolution.ImportFromSource||
                node.Sharing!=NativeMobyDependencySharing.Immutable||node.Reads.Count!=1||node.Preserved.Count!=0||
                !AssetGraph.CopyPayload(node.Id).AsSpan().SequenceEqual(entry.Input.CopyBytes()))
                throw new InvalidDataException("A code-owned family model lost its complete source-copy materialization policy.");
            var read=node.Reads[0];
            NativeByteExtent destination=new(final,entry.PackageOffset,entry.ByteLength);
            nodes.Add(new(node.Id,node.Kind,node.Resolution,node.Sharing,node.EvidenceSha256,node.Dependencies,node.Reads,
                context=>
                {
                    byte[] bytes=context.Read(read.Id);
                    if(!bytes.AsSpan().SequenceEqual(entry.Input.CopyBytes()))
                        throw new InvalidDataException("A model contribution differs from its exact complete Original source read.");
                    return new(bytes,[new("registered-passive-package",destination)],[]);
                }));
        }
        return NativeMobyDependencyClosureCompiler.Compile(nodes,ModelContributions.Select(p=>p.Id),ReadSource);
    }

    public static async Task<NativeMobyFamilyAssetPlan> CaptureArtisansGrassAsync(string originalBin,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);
        string path=Path.GetFullPath(originalBin);FileStream? held=null;NativeMobyFamilyAssetPlan? result=null;
        List<Exception> failures=[];
        try
        {
            held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(held,token);
            var layout=DiscImage.DetectLayout(held);
            var wad=DiscImage.FindRootFileRecord(held,layout,n=>n.Equals("WAD.WAD",StringComparison.OrdinalIgnoreCase));
            if(layout!=new DiscLayout(2352,24,22,2048)||wad.Lba!=37||wad.Size!=0x6927000)
                throw new InvalidDataException("Native family requires the complete original WAD physical owner.");
            RequireEntry(9,0x7F2800,0xE000);RequireEntry(10,0x800800,0x383000);
            byte[] overlay=Read(0x7F2800,0xE000),data=Read(0x800800,0x383000);
            result=new(data,overlay);token.ThrowIfCancellationRequested();

            byte[] Read(long at,int length)
            {
                token.ThrowIfCancellationRequested();byte[] bytes=DiscImage.ReadFileBytes(held,layout,37,at,length);
                if(bytes.Length!=length)throw new InvalidDataException("Native family source owner is incomplete.");
                token.ThrowIfCancellationRequested();return bytes;
            }
            void RequireEntry(int index,int offset,int length)
            {
                byte[] entry=Read(index*8L,8);
                if(BinaryPrimitives.ReadInt32LittleEndian(entry)!=offset||BinaryPrimitives.ReadInt32LittleEndian(entry.AsSpan(4))!=length)
                    throw new InvalidDataException("Native family original donor WAD entry changed.");
            }
        }
        catch(Exception error){failures.Add(error);}
        finally
        {
            if(held is not null)await Observe(()=>NativeId65FreshDiscSource.RequireWholeOriginalAsync(held,CancellationToken.None));
            FileStream? current=null;
            try
            {
                current=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
                await NativeId65FreshDiscSource.RequireWholeOriginalAsync(current,CancellationToken.None);
            }
            catch(Exception error){failures.Add(error);}
            finally{if(current is not null)Attempt(current.Dispose);}
            if(result is not null)Attempt(result.RequireRetainedOwners);
            if(held is not null)Attempt(held.Dispose);
        }
        NativeFreshResearchSourceOwners.ThrowFailures(failures);token.ThrowIfCancellationRequested();
        return result??throw new InvalidDataException("Native family original-source capture returned no result.");
        async Task Observe(Func<Task> action){try{await action();}catch(Exception error){failures.Add(error);}}
        void Attempt(Action action){try{action();}catch(Exception error){failures.Add(error);}}
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}

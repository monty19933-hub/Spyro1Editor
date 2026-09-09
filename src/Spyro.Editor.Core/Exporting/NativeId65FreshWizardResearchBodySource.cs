using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65FreshWizardBodySmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>Private actual Host ancestry plus unchanged exact-original auxiliary issuer.</summary>
internal sealed class NativeId65FreshWizardResearchBodySource
{
    private readonly NativeId65NormalBodySource _retail;
    private NativeId65FreshWizardResearchBodySource(NativeId65FreshWizardResearchHostPlan host,NativeId65NormalBodySource retail)
    {
        RequireHost(host);Host=host;_retail=retail;
        // The unchanged private retail capture validates the complete Town
        // allocation, four exact auxiliary descriptors/payloads, original EXE,
        // native54 shared PETE registrations and normal reserved-address inputs.
        // Its old body/layout/staging issuer is never invoked or widened.
        if(retail.ResidentPeteAddress!=0x801C5BB8||retail.NormalReservedLowerAddress!=0x80187BB0)
            throw new InvalidDataException("Original shared/native reserved owners changed.");
        byte[] executable=host.SceneAncestor.CompilationAncestor.FreshResourceAncestor.ResourceChainAncestor.Resources.Surface.CopySourceExecutable();
        NativeId65NormalUploadStagingSource.ValidateSourceExecutable(executable);
    }
    internal NativeId65FreshWizardResearchHostPlan Host{get;}
    internal uint ResidentPeteAddress=>_retail.ResidentPeteAddress;
    internal uint NormalReservedLowerAddress=>_retail.NormalReservedLowerAddress;
    internal IReadOnlyList<NativeId65EngineEvidence> Evidence=>_retail.Evidence;
    internal byte[][] CopyAuxiliaryAllocations()=>_retail.CopyAuxiliaryAllocations();
    internal static void RequireHost(NativeId65FreshWizardResearchHostPlan host)
    {
        ArgumentNullException.ThrowIfNull(host);
        host.RequireAncestry(host.SceneAncestor,host.FallbackAncestor);
        if(host.SceneAncestor.Producer==NativeFreshWizardSceneProducer.MaterialAuthored)
            throw new InvalidDataException("Material-authored Scene requires its mixed body; old untextured Body capture is forbidden.");
        host.RequireExactOutputs(host.CopyOverlay(),host.CopyInitializerPatch());
        if(host.CopyAnchor!=0x8008BA38||host.OverlayByteLength!=0x11000||host.SceneAncestor.PlacedCount!=1||
            host.SceneAncestor.NativeNominalCapacity!=32||host.SourceImageSha256!=NativeMobySourceInventory.OriginalImageSha256)
            throw new InvalidDataException("Body requires its actual fixed P1/N32 enlarged Town Host.");
    }
    internal static async Task<NativeId65FreshWizardResearchBodySource> CaptureAsync(string path,
        NativeId65FreshWizardResearchHostPlan host,CancellationToken token)
    {
        RequireHost(host);
        var retail=await NativeId65NormalBodySource.CaptureAsync(path,token);
        token.ThrowIfCancellationRequested();return new(host,retail);
    }
}

/// <summary>
/// Separate fixed packing/structure kernel, not a source issuer. Neutral scene
/// shape is not source identity; the public factory also retains/revalidates
/// the actual issued scene through Host. Old normal-body bounds stay unchanged.
/// </summary>
internal sealed class NativeId65FreshWizardBodyLayout
{
    private static readonly int[] PartLengths=[0xE5800,0x1000,0x19000,0x3000,0x49800,0x42800,0x5D000,0x20800];
    private static readonly (ushort Class,int At,int Payload,int Allocation)[] Roots=[
        (0,0,0x10A4C,0x10A4C),(0x78,0x10A4C,0x2BD0,0x2BD0),(0x26,0x1361C,0x564,0x564),
        (0x11B,0x13B80,0x35B4,0x3C80),(0xE,0x17800,0x7EC,0x7EC),(0xF,0x17FEC,0x78C,0x78C),(0x10,0x18778,0x330,0x888)];
    private readonly byte[] _body;
    private readonly NativeFreshWizardResourceProfile _profile;
    private NativeId65FreshWizardBodyLayout(byte[] upload,byte[] data,byte[] model,byte[] scene,byte[][] auxiliary,
        NativeFreshWizardResourceProfile profile)
    {
        _profile=profile;
        _body=new byte[0x20C800];MakeHeader().CopyTo(_body,0);Descriptors=DecodeHeader(_body.AsSpan(0,0x800));
        byte[] padded=new byte[0x1000];data.CopyTo(padded,0);
        byte[][] parts=[upload,padded,model,scene,..auxiliary];
        for(int i=0;i<parts.Length;i++)parts[i].CopyTo(_body,Descriptors[i].Offset);
        ValidateStructureCore(_body,_profile);
        ModelRoots=Array.AsReadOnly(Roots.Select((r,i)=>new NativeId65NormalBodyRoot(i,0xE7000+r.At,r.At,r.Payload,r.Allocation,r.Class)).ToArray());
        var owners=new List<NativeId65NormalBodyOwner>();
        Add("header",0,0x800,"generated-native-fields-and-zero-complement");
        Add("same-fallback-upload",0x800,0xE5800,"neutral-input-not-authority");
        Add("same-fallback-used-data",0xE6000,0x8FC,"neutral-input-not-authority");
        Add("data-CD-padding",0xE68FC,0x704,"generated-zero-CD-padding");
        Add("same-fallback-seven-model-roots",0xE7000,0x19000,"neutral-input-not-authority");
        Add("same-host-P1-N32-scene",0x100000,0x3000,"neutral-input-not-authority");
        var subowners=new List<NativeId65AuxiliaryBodyOwner>();
        foreach(var d in Descriptors.Where(d=>d.RetainedAuxiliary))
        {
            Add("complete-retained-auxiliary-"+(d.Index-4),d.Offset,d.ByteLength,"neutral-input-not-authority");
            foreach(var o in DecodeAuxiliaryOwners(_body.AsSpan(d.Offset,d.ByteLength)))
                subowners.Add(new(d.Index,o.Id,o.Offset,d.Offset+o.Offset,o.Length,Hash(_body.AsSpan(d.Offset+o.Offset,o.Length))));
        }
        int cursor=0;foreach(var o in owners){if(o.Offset!=cursor||o.ByteLength<=0)throw new InvalidDataException("Body ownership gap/overlap.");cursor+=o.ByteLength;}
        if(cursor!=_body.Length)throw new InvalidDataException("Body owner partition is incomplete.");
        Owners=owners.AsReadOnly();AuxiliaryOwners=subowners.AsReadOnly();
        void Add(string id,int at,int n,string provenance)=>owners.Add(new(id,at,n,Hash(_body.AsSpan(at,n)),provenance,null));
    }
    internal bool SourceCertified=>false;
    internal IReadOnlyList<NativeId65NormalBodyDescriptor> Descriptors{get;}
    internal IReadOnlyList<NativeId65NormalBodyRoot> ModelRoots{get;}
    internal IReadOnlyList<NativeId65NormalBodyOwner> Owners{get;}
    internal IReadOnlyList<NativeId65AuxiliaryBodyOwner> AuxiliaryOwners{get;}
    internal byte[] CopyLevelData()=>_body.ToArray();
    internal void RequireExactLevelData(ReadOnlySpan<byte> body)
    {ValidateStructureCore(body,_profile);if(!body.SequenceEqual(_body))throw new InvalidDataException("Body source owner/complement changed.");}
    internal static NativeId65FreshWizardBodyLayout Compile(ReadOnlySpan<byte> upload,ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> models,ReadOnlySpan<byte> scene,IReadOnlyList<byte[]> auxiliary)
        =>CompileCore(upload,data,models,scene,auxiliary,NativeFreshWizardResourceProfile.FixedA);

    internal static NativeId65FreshWizardBodyLayout CompileAuthored(NativeId65FreshWizardResearchHostPlan host,
        IReadOnlyList<byte[]> auxiliary)
    {
        NativeId65FreshWizardResearchBodySource.RequireHost(host);
        if(host.SceneAncestor.ResourceAncestor.ResourceProfile!=NativeFreshWizardResourceProfile.AuthoredSameLayout)
            throw new InvalidDataException("Authored body entry requires its actual authored-profile Host.");
        var fallback=host.FallbackAncestor;var scene=host.SceneAncestor;
        byte[] upload=fallback.CopyUploadPayload(),data=fallback.CopyDataComponents(),models=fallback.CopyModelPayload(),sceneBytes=scene.CopyScene();
        fallback.RequireExactOutputs(upload,data,models);scene.RequireExactScene(sceneBytes);
        return CompileCore(upload,data,models,sceneBytes,auxiliary,NativeFreshWizardResourceProfile.AuthoredSameLayout);
    }

    private static NativeId65FreshWizardBodyLayout CompileCore(ReadOnlySpan<byte> upload,ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> models,ReadOnlySpan<byte> scene,IReadOnlyList<byte[]> auxiliary,NativeFreshWizardResourceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(auxiliary);
        // Bound all actual enumeration and lengths before clones/output work.
        if(upload.Length!=0xE5800||data.Length!=0x8FC||models.Length!=0x19000||scene.Length!=0x3000||auxiliary.Count!=4)
            throw new InvalidDataException("Only complete closed fallback/P1 body input lengths are admitted.");
        byte[][] snapshot=auxiliary.Take(5).ToArray();
        if(snapshot.Length!=4||snapshot.Where((a,i)=>a is null||a.Length!=PartLengths[i+4]).Any())
            throw new InvalidDataException("Actual bounded auxiliary count/length changed.");
        foreach(var bytes in snapshot)_=DecodeAuxiliaryOwners(bytes);
        RequireResourceParts(upload,data,models,profile);RequireSceneStructure(scene);
        return new(upload.ToArray(),data.ToArray(),models.ToArray(),scene.ToArray(),snapshot.Select(b=>b.ToArray()).ToArray(),profile);
    }
    internal static IReadOnlyList<NativeId65NormalBodyDescriptor> DecodeHeader(ReadOnlySpan<byte> header)
    {
        if(header.Length!=0x800)throw new InvalidDataException("Body header requires all800 bytes.");
        var descriptors=new List<NativeId65NormalBodyDescriptor>();int cursor=0x800;
        for(int i=0;i<8;i++)
        {
            int at=I32(header,i*8),n=I32(header,i*8+4);
            if(at!=cursor||n!=PartLengths[i]||n>0x20C800-cursor||at%0x800!=0||n%0x800!=0)
                throw new InvalidDataException("Body descriptor order/length/bound differs from exact eight-owner grammar.");
            descriptors.Add(new(i,at,n,i>=4));cursor+=n;
        }
        if(cursor!=0x20C800||header.Slice(0x40,0x10).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Absent descriptors8/9 or exact full body extent changed.");
        for(int i=0;i<Roots.Length;i++)
            if(I32(header,0x50+i*4)!=0xE7000+Roots[i].At||BinaryPrimitives.ReadUInt16LittleEndian(header[(0x150+i*2)..])!=Roots[i].Class)
                throw new InvalidDataException("Seven actual root/class entries escaped the closed model grammar.");
        byte[] inverse=header.ToArray();inverse.AsSpan(0,0x40).Clear();inverse.AsSpan(0x50,7*4).Clear();inverse.AsSpan(0x150,7*2).Clear();
        if(inverse.AsSpan().ContainsAnyExcept((byte)0))throw new InvalidDataException("Header reserved/unused root/class complement changed.");
        return descriptors.AsReadOnly();
    }
    internal static void ValidateStructure(ReadOnlySpan<byte> body)
        =>ValidateStructureCore(body,NativeFreshWizardResourceProfile.FixedA);
    private static void ValidateStructureCore(ReadOnlySpan<byte> body,NativeFreshWizardResourceProfile profile)
    {
        if(body.Length!=0x20C800)throw new InvalidDataException("Separate closed body length must be20C800, not old200000.");
        var descriptors=DecodeHeader(body[..0x800]);
        if(body.Slice(0xE68FC,0x704).ContainsAnyExcept((byte)0))throw new InvalidDataException("Discarded data CD padding changed.");
        RequireResourceParts(body.Slice(0x800,0xE5800),body.Slice(0xE6000,0x8FC),body.Slice(0xE7000,0x19000),profile);
        RequireSceneStructure(body.Slice(0x100000,0x3000));
        foreach(var d in descriptors.Where(d=>d.RetainedAuxiliary))_=DecodeAuxiliaryOwners(body.Slice(d.Offset,d.ByteLength));
    }
    private static void RequireResourceParts(ReadOnlySpan<byte> upload,ReadOnlySpan<byte> data,ReadOnlySpan<byte> models,
        NativeFreshWizardResourceProfile profile)
    {
        if(Hash(upload)!="8c4438fc13eca3b3bd40e9c056094c308fb272aded227577fb812ec9b16d96dc"||
            Hash(models)!="84d00dbbce264bc08c61abfe2579fe3d7a7464ba2df6b189834664da10b82c84")
            throw new InvalidDataException("Neutral fixed-resource bytes changed; hashes never issue ancestry.");
        if(profile==NativeFreshWizardResourceProfile.FixedA)
        {
            if(Hash(data)!="bea93c58d6d568c7c6162816ddad1afff32a51886f9412a0809b22887e0b83d4")
                throw new InvalidDataException("Neutral fixed-resource data changed; authored geometry needs its actual Host entry.");
        }
        else if(profile==NativeFreshWizardResourceProfile.AuthoredSameLayout)
            NativeFreshWizardAuthoringShape.RequireSurfaceComplement(data,0x8FC,"bea93c58d6d568c7c6162816ddad1afff32a51886f9412a0809b22887e0b83d4");
        else throw new InvalidDataException("Unknown closed Wizard body profile.");
    }
    private static void RequireSceneStructure(ReadOnlySpan<byte> scene)
    {
        if(scene.Length!=0x3000||I32(scene,0xCC)!=0xEB8||I32(scene,0xD0)!=1||I32(scene,0xD4)!=0xF88||
            I32(scene,0xF84)!=0x54||I32(scene,0xF88)!=0xFB0||I32(scene,0xFD8)!=8||I32(scene,0xFDC)!=0||
            I32(scene,0xFE0)!=0x2004||I32(scene,0x2FE4)!=2||I32(scene,0x2FE8)!=0xD4||I32(scene,0x2FEC)!=0xF88||
            scene.Slice(0x12C,0xE58).ContainsAnyExcept((byte)0)||scene.Slice(0xFE4,0x2000).ContainsAnyExcept((byte)0)||
            scene[0x2FF0..].ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Detached P1/N32 structure, fixups, reserve or padding changed.");
    }
    internal static IReadOnlyList<(string Id,int Offset,int Length)> DecodeAuxiliaryOwners(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length<0x24||bytes.Length>0x5D000||I32(bytes,0)!=0)throw new InvalidDataException("Auxiliary header/length changed.");
        var owners=new List<(string,int,int)>{("auxiliary-header",0,0x24)};int cursor=0x24;
        string[] names=["audio","model-one","model-two","track"];
        for(int i=0;i<4;i++)
        {
            int at=I32(bytes,4+i*8),n=I32(bytes,8+i*8);
            if(at!=cursor||n<=0||n%4!=0||n>bytes.Length-cursor||i==3&&n%24!=0)
                throw new InvalidDataException("Auxiliary source subowners must be consecutive, bounded and track-stride aligned.");
            owners.Add((names[i],at,n));cursor+=n;
        }
        int padding=bytes.Length-cursor;
        if(padding<0||padding>=0x800)throw new InvalidDataException("Auxiliary final source padding escaped sector bound.");
        if(padding>0)owners.Add(("retained-sector-padding",cursor,padding));return owners.AsReadOnly();
    }
    private static byte[] MakeHeader()
    {
        byte[] b=new byte[0x800];int cursor=0x800;
        for(int i=0;i<8;i++){Put(b,i*8,cursor);Put(b,i*8+4,PartLengths[i]);cursor+=PartLengths[i];}
        for(int i=0;i<Roots.Length;i++){Put(b,0x50+i*4,0xE7000+Roots[i].At);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(0x150+i*2),Roots[i].Class);}return b;
    }
    internal static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static int I32(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
    private static void Put(Span<byte> b,int at,int n)=>BinaryPrimitives.WriteInt32LittleEndian(b[at..],n);
}

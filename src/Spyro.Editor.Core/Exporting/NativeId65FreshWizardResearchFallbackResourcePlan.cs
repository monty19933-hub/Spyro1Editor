using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeFreshWizardFallbackTextureSpan(int Offset,int ByteLength,long SourceWadOffset,string Sha256);

/// <summary>
/// New detached fixed A16 Wizard resource revision retaining three exact Town
/// fallback model/texture owners. All prior17800 models, sound/data and old
/// source types remain unchanged. Source certification is not callback/audio
/// compatibility, a body/scene/header, save identity, or runtime approval.
/// </summary>
public sealed class NativeId65FreshWizardResearchFallbackResourcePlan
{
    private readonly NativeId65FreshWizardResearchFallbackResourceSource _source;
    private readonly byte[] _upload,_data,_models;
    private NativeId65FreshWizardResearchFallbackResourcePlan(NativeId65FreshWizardResearchFallbackResourceSource source)
    {
        _source=source;_upload=source.Upload.ToArray();_data=source.Data.ToArray();_models=new byte[0x19000];
        source.Models.CopyTo(_models,0);int cursor=source.Models.Length;
        foreach(var spec in NativeId65FreshWizardResearchFallbackResourceSource.PackageSpecs)
        {source.Packages[spec.Class].CopyTo(_models,cursor);cursor+=spec.Length;}
        if(cursor!=0x18AA8)throw new InvalidDataException("Fallback packages changed the declared complete model end.");
        TextureSourceSpans=Array.AsReadOnly(source.TextureSourceSpans.ToArray());
        RetainedTextureAliases=Array.AsReadOnly(source.RetainedTextureAliases.ToArray());
        var changed=new bool[0x80000];
        foreach(var span in TextureSourceSpans)
        {
            source.Town.AsSpan(checked((int)(span.SourceWadOffset-NativeId65FreshWizardResearchFallbackResourceSource.TownWad)),span.ByteLength)
                .CopyTo(_upload.AsSpan(span.Offset,span.ByteLength));
            Array.Fill(changed,true,span.Offset,span.ByteLength);
        }
        for(int i=0;i<0x80000;i++)if(!changed[i]&&_upload[i]!=source.Upload[i])throw new InvalidDataException("Fallback changed the protected VRAM complement.");
        if(!_data.AsSpan().SequenceEqual(source.Data)||!_upload.AsSpan(0x80000).SequenceEqual(source.Upload.AsSpan(0x80000))||
            !_models.AsSpan(0,0x17800).SequenceEqual(source.Models)||_models.AsSpan(0x18AA8).ContainsAnyExcept((byte)0)||
            !_models.AsSpan(0x17FEC,8).SequenceEqual(source.Packages[0xF].AsSpan(0,8)))
            throw new InvalidDataException("Fallback changed retained data/audio/models, simple lookahead ownership, or padding.");
        ModelRoots=Array.AsReadOnly(source.Resource.ModelRoots.Concat(new[]{new NativeFreshWizardModelRoot(0xE,0x17800,0x7EC,0x7EC),
            new(0xF,0x17FEC,0x78C,0x78C),new(0x10,0x18778,0x330,0x888)}).ToArray());
        if(ModelRoots.Count!=7||ModelRoots.Select(r=>r.ClassId).Distinct().Count()!=7||ModelRoots.Any(r=>r.ClassId>=512))
            throw new InvalidDataException("Fallback roots exceed their exact64/512 registration limits.");
        var owners=new List<NativeFreshWizardResourceOwner>();
        // Split the old upload at actual new source span boundaries; aliases
        // keep their old owner. New spans are exact source bytes, not patches.
        int next=0;cursor=0;
        foreach(var old in source.Resource.Owners.Where(o=>o.Domain==NativeFreshWizardResourceDomain.Upload))
        {
            if(old.Offset!=cursor)throw new InvalidDataException("Old owner order changed.");int end=old.Offset+old.ByteLength;
            while(cursor<end)
            {
                var span=next<TextureSourceSpans.Count?TextureSourceSpans[next]:null;
                if(span is not null&&span.Offset==cursor)
                {
                    if(span.ByteLength>end-cursor)throw new InvalidDataException("New span crosses its single certified background owner.");
                    Add(NativeFreshWizardResourceDomain.Upload,cursor,span.ByteLength,"original-Town-fallback-texture","Town",checked((int)(span.SourceWadOffset-NativeId65FreshWizardResearchFallbackResourceSource.TownWad)));
                    cursor+=span.ByteLength;next++;
                }
                else
                {
                    int stop=Math.Min(end,span?.Offset??end);if(stop<=cursor)throw new InvalidDataException("New source span ordering changed.");
                    Add(NativeFreshWizardResourceDomain.Upload,cursor,stop-cursor,"retained-prior-resource","prior.upload",cursor);cursor=stop;
                }
            }
        }
        if(next!=TextureSourceSpans.Count||cursor!=_upload.Length)throw new InvalidDataException("Fallback upload source partition is incomplete.");
        Add(NativeFreshWizardResourceDomain.Data,0,_data.Length,"retained-prior-resource","prior.data",0);
        foreach(var old in source.Resource.Owners.Where(o=>o.Domain==NativeFreshWizardResourceDomain.Models))
            Add(old.Domain,old.Offset,old.ByteLength,old.Provenance=="generated-model-padding"?"retained-prior-generated-model-padding":"retained-prior-resource","prior.models",old.Offset);
        cursor=0x17800;
        foreach(var spec in NativeId65FreshWizardResearchFallbackResourceSource.PackageSpecs)
        {Add(NativeFreshWizardResourceDomain.Models,cursor,spec.Length,"original-Town-complete-model","Town",spec.At);cursor+=spec.Length;}
        Add(NativeFreshWizardResourceDomain.Models,cursor,_models.Length-cursor,"generated-fallback-model-padding",null,0);
        byte[][] outputs=[_upload,_data,_models];
        for(int d=0;d<3;d++)
        {
            cursor=0;foreach(var owner in owners.Where(o=>(int)o.Domain==d))
            {if(owner.Offset!=cursor||owner.ByteLength<=0||owner.ByteLength>outputs[d].Length-cursor)throw new InvalidDataException("Fallback owner partition is incomplete or overlapping.");cursor+=owner.ByteLength;}
            if(cursor!=outputs[d].Length)throw new InvalidDataException("Fallback output has an unowned suffix.");
        }
        Owners=owners.AsReadOnly();UploadSha256=Hash(_upload);DataSha256=Hash(_data);ModelSha256=Hash(_models);
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{ProfileId,SourceImageSha256,ResourcePlanSha256,CanonicalDocumentSha256,
            UploadSha256,DataSha256,ModelSha256,ModelRoots,Owners,TextureSourceSpans,RetainedTextureAliases,PendingOwners,
            SourceCertified,GlobalAudioCompatibilityProven,CompleteNativeDependencyClosureProven,RuntimeVerified,PublicationApproved,BodyIssued,PersistentOwnerIssued}));
        return;
        void Add(NativeFreshWizardResourceDomain domain,int at,int n,string provenance,string? id,int sourceAt)
        {
            byte[] output=domain switch{NativeFreshWizardResourceDomain.Upload=>_upload,NativeFreshWizardResourceDomain.Data=>_data,_=>_models};
            if(at<0||n<=0||n>output.Length-at)throw new InvalidDataException("Fallback owner escapes its output.");
            if(id is not null)
            {
                byte[] input=id switch{"prior.upload"=>source.Upload,"prior.data"=>source.Data,"prior.models"=>source.Models,"Town"=>source.Town,_=>throw new InvalidDataException("Unknown source domain.")};
                if(sourceAt<0||n>input.Length-sourceAt||!output.AsSpan(at,n).SequenceEqual(input.AsSpan(sourceAt,n)))
                    throw new InvalidDataException("Fallback owner does not reproduce its complete source.");
            }
            else if(output.AsSpan(at,n).ContainsAnyExcept((byte)0))throw new InvalidDataException("Generated padding is not zero.");
            owners.Add(new($"{domain}:{at:X}",domain,at,n,Hash(output.AsSpan(at,n)),provenance,id,id is null?null:sourceAt));
        }
    }
    internal NativeId65FreshWizardResearchResourcePlan ResourceAncestor=>_source.Resource;
    internal void RequireAncestry(NativeId65FreshWizardResearchResourcePlan r)
    {if(!ReferenceEquals(r,ResourceAncestor))throw new InvalidDataException("Fallback belongs to another actual resource even if its bytes/hash match.");r.RequireAncestry(r.CompilationAncestor,r.FamilyAncestor);}
    public const string ProfileId="fresh-A16-wizard33-T6-Town0E-0F-10-detached-resources-v1";
    public string SourceImageSha256=>NativeMobySourceInventory.OriginalImageSha256;
    public string ResourcePlanSha256=>ResourceAncestor.PlanSha256;
    public string CanonicalDocumentSha256=>ResourceAncestor.CanonicalDocumentSha256;
    public string UploadSha256{get;}
    public string DataSha256{get;}
    public string ModelSha256{get;}
    public string PlanSha256{get;}
    public IReadOnlyList<NativeFreshWizardModelRoot> ModelRoots{get;}
    public IReadOnlyList<NativeFreshWizardResourceOwner> Owners{get;}
    public IReadOnlyList<NativeFreshWizardFallbackTextureSpan> TextureSourceSpans{get;}
    public IReadOnlyList<NativeFreshWizardFallbackTextureSpan> RetainedTextureAliases{get;}
    public IReadOnlyList<string> PendingOwners{get;}=Array.AsReadOnly(new[]{
        "Only exact three Town fallback model/texture owners are added; number104/105 remain existing shared registrations, not newly closed callbacks.",
        "Global1F occupied replacement compatibility, all transitive callback/effect/render/collider lifetimes and peak pressure remain unresolved.",
        "The simple0E renderer uses original adjacent0F lookahead; full renderer/GTE/hardware/native gameplay is not executed or approved here.",
        "No existing body/scene/staging/header/overlay/disc grammar accepts this revision; no such output is issued.",
        "ID65 durable reward/save identity, target/card/reentry and publication remain unproven."});
    public bool SourceCertified=>true;
    public bool GlobalAudioCompatibilityProven=>false;
    public bool CompleteNativeDependencyClosureProven=>false;
    public bool RuntimeVerified=>false;
    public bool PublicationApproved=>false;
    public bool BodyIssued=>false;
    public bool PersistentOwnerIssued=>false;
    public byte[] CopyUploadPayload()=>_upload.ToArray();
    public byte[] CopyDataComponents()=>_data.ToArray();
    public byte[] CopyModelPayload()=>_models.ToArray();
    public void RequireExactOutputs(ReadOnlySpan<byte> upload,ReadOnlySpan<byte> data,ReadOnlySpan<byte> models)
    {if(!upload.SequenceEqual(_upload)||!data.SequenceEqual(_data)||!models.SequenceEqual(_models))throw new InvalidDataException("Complete fallback owner/padding/afterimage changed.");}
    public static async Task<NativeId65FreshWizardResearchFallbackResourcePlan> CaptureAsync(string originalBin,
        NativeId65FreshWizardResearchResourcePlan resource,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);string path=Path.GetFullPath(originalBin);
        await using FileStream image=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        await NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,NativeMobySourceInventory.OriginalImageByteLength,NativeMobySourceInventory.OriginalImageSha256,token);
        try
        {var source=await NativeId65FreshWizardResearchFallbackResourceSource.CaptureAsync(path,resource,token);token.ThrowIfCancellationRequested();var result=new NativeId65FreshWizardResearchFallbackResourcePlan(source);token.ThrowIfCancellationRequested();return result;}
        finally
        {image.Position=0;await NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,NativeMobySourceInventory.OriginalImageByteLength,NativeMobySourceInventory.OriginalImageSha256,CancellationToken.None);token.ThrowIfCancellationRequested();}
    }
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}

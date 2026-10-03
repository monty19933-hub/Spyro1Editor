using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Universal;

[assembly: InternalsVisibleTo("Spyro.Editor.GreenDruidSecondWorldSmoke")]
namespace Spyro.Editor.Core.Exporting;

/// <summary>Engine reward fallbacks for the actual neutral main-editor kernel.
/// Captured independently of the detached wizard experiment; no actors or behavior are added.</summary>
internal sealed class NativeFreshRewardFallbackResources
{
    private readonly Dictionary<int, byte[]> _packages = [];
    private readonly byte[] _town, _upload;
    private readonly List<(int At,int Length)> _textureSpans = [];
    internal IReadOnlyList<NativeId65NormalResourceOwner> UploadOwners { get; }
    internal byte[] CopyUploadPayload() => _upload.ToArray();
    private NativeFreshRewardFallbackResources(NativeId65FreshSceneBundle fresh, NativeId65EngineProfileContract engine)
    {
        _town=engine.CopyTownData(); byte[] exe=engine.CopyExecutable();
        Require(_town,0x2E2000,"7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0");
        Require(exe,0x66000,"a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998");
        if(!exe.AsSpan().SequenceEqual(fresh.ResourceChainAncestor.Resources.SurfaceRevision.CopySourceExecutable()))
            throw new InvalidDataException("Reward fallbacks lost their actual main-editor kernel ancestry.");
        var space=new NativeAddressSpace(NativeAddressKind.LevelData,"main-kernel-original-Town",NativeAddressRevision.Original,_town.Length);
        var registrations=NativeMobyModelRegistrationSource.CaptureExactTown(space,extent=>{
            if(extent.Space!=space||extent.Offset<0||extent.EndExclusive>_town.Length)throw new InvalidDataException("Reward model read escaped Town.");
            return _town.AsSpan(checked((int)extent.Offset),checked((int)extent.ByteLength)).ToArray();});
        foreach(var spec in NativeId65FreshWizardResearchFallbackResourceSource.PackageSpecs)
        {
            var registration=registrations.Registrations.Single(r=>r.ActorId==spec.Class);
            long end=registration.Slot+1<registrations.Registrations.Count?registrations.Registrations[registration.Slot+1].RootDataOffset:registrations.ModelEnd;
            if(registration.RootDataOffset!=spec.At||end-spec.At!=spec.Length||engine.SharedPeteClasses.Contains(spec.Class))
                throw new InvalidDataException("Reward fallback is not the complete original local registration.");
            var package=_town.AsSpan(spec.At,spec.Length).ToArray();Require(package,spec.Length,spec.Hash);_packages.Add(spec.Class,package);
        }
        NativeTownFallbackSimpleModel.RequireShape(_packages[0xE]);
        var regions=new List<(int At,int Width,int Rows,bool Alias)>();
        foreach(int cls in new[]{0xF,0x10})
        {
            var package=_packages[cls];var layout=NativeMobyModelTextureLayout.Decode(package);
            if(BinaryPrimitives.ReadUInt32LittleEndian(package)!=1||package.AsSpan(4,16).ContainsAnyExcept((byte)255)||
                package.AsSpan(20,32).ContainsAnyExcept((byte)0)||layout.References.Count!=(cls==0xF?72:8))
                throw new InvalidDataException("Reward fallback animation, sound or collider schema changed.");
            foreach(var group in layout.References.GroupBy(r=>(r.Page,r.Palette)))
            {
                var uv=group.SelectMany(r=>{var b=r.CopyBytes();return new (int U,int V)[]{(b[0],b[1]),(b[4],b[5]),(b[8],b[9]),(b[10],b[11])};}).ToArray();
                int u0=uv.Min(p=>p.U),u1=uv.Max(p=>p.U),v0=uv.Min(p=>p.V),v1=uv.Max(p=>p.V);
                int x=(group.Key.Page&15)*128-1024+u0/2,y=((group.Key.Page&16)!=0?256:0)+v0;
                int width=u1/2-u0/2+1,rows=v1-v0+1,cx=(group.Key.Palette&63)*32-1024,cy=group.Key.Palette>>6;
                NativeId65FreshWizardResearchResourceSource.RequireRectangle(x,y,width,rows,false);
                NativeId65FreshWizardResearchResourceSource.RequireRectangle(cx,cy,32,1,true);
                regions.Add((y*1024+x,width,rows,cls==0xF&&group.Key.Page==0x3F));regions.Add((cy*1024+cx,32,1,false));
            }
        }
        if(!regions.OrderBy(r=>r.At).SequenceEqual(new[]{(0x48320,16,32,false),(0x582B0,16,32,false),(0x6C3A0,2,4,true),
            (0x722E0,32,1,false),(0x7C2C0,32,1,false),(0x7E2C0,32,1,false)}))
            throw new InvalidDataException("Reward fallback texture descriptors changed their exact source extents.");
        _upload=fresh.CopyUploadPayload();var tags=new byte[0x80000];int cursor=0;
        foreach(var owner in fresh.UploadOwners)
        {
            if(owner.Domain!=NativeId65ResourceDomain.UploadPayload||owner.Offset!=cursor||owner.ByteLength<=0||owner.ByteLength>_upload.Length-cursor||Hash(_upload.AsSpan(cursor,owner.ByteLength))!=owner.Sha256)
                throw new InvalidDataException("Actual main-editor upload ownership is incomplete or changed.");
            if(cursor<0x80000)
            {
                if(owner.ByteLength>0x80000-cursor)throw new InvalidDataException("Upload owner crosses GPU/SPU domains.");
                byte tag=owner.Provenance switch{"fresh-zero-upload-background"=>0,"retained-fixed-Town-presentation-VRAM"=>1,"issued-detached-companion-VRAM"=>2,_=>throw new InvalidDataException("Unknown kernel GPU owner.")};
                if(tag==0&&_upload.AsSpan(cursor,owner.ByteLength).ContainsAnyExcept((byte)0))throw new InvalidDataException("Kernel free upload is not zero.");
                if(tag==1&&(owner.SourceWadOffset!=0x119E800L+cursor||!_upload.AsSpan(cursor,owner.ByteLength).SequenceEqual(_town.AsSpan(0x800+cursor,owner.ByteLength))))
                    throw new InvalidDataException("Kernel retained Town upload lost its original owner.");
                Array.Fill(tags,tag,cursor,owner.ByteLength);
            }
            cursor+=owner.ByteLength;
        }
        if(cursor!=0xDE000)throw new InvalidDataException("Actual kernel GPU/SPU partition changed.");
        var inserted=new bool[0x80000];
        foreach(var region in regions)for(int row=0;row<region.Rows;row++)
        {
            int at=region.At+row*1024;_textureSpans.Add((at,region.Width));
            for(int i=at;i<at+region.Width;i++)
            {
                if(inserted[i])throw new InvalidDataException("Reward texture owners overlap.");
                NativeId65FreshWizardResearchFallbackResourceSource.RequireTextureOwner(tags[i],_upload[i],_town[0x800+i],region.Alias);
                if(!region.Alias){inserted[i]=true;_upload[i]=_town[0x800+i];}
            }
        }
        if(inserted.Count(v=>v)!=1120||_textureSpans.Count!=71)throw new InvalidDataException("Reward texture ownership budget changed.");
        var owners=new List<NativeId65NormalResourceOwner>();
        foreach(var old in fresh.UploadOwners)
        {
            if(old.Offset>=0x80000){owners.Add(old);continue;}
            int start=old.Offset,end=start+old.ByteLength;
            while(start<end)
            {
                bool added=inserted[start];int next=start+1;while(next<end&&inserted[next]==added)next++;
                owners.Add(new("main-reward-owner-"+owners.Count,old.Domain,start,next-start,Hash(_upload.AsSpan(start,next-start)),
                    added?"retained-fixed-Town-presentation-VRAM":old.Provenance,added?0x119E800L+start:old.SourceWadOffset is {} source?source+start-old.Offset:null));start=next;
            }
        }
        UploadOwners=owners.AsReadOnly();
    }
    internal static async Task<NativeFreshRewardFallbackResources> CaptureAsync(string original,NativeId65FreshSceneBundle fresh,CancellationToken token)
        =>new(fresh,await NativeId65EngineProfileContract.CaptureAsync(original,token));
    internal void InstallModels(UniversalLevelData body)
    {
        if(body.Registrations().Any(r=>_packages.ContainsKey(r.ClassId)))throw new InvalidDataException("Neutral kernel already contains a conflicting reward fallback.");
        var registrations=body.Registrations();if(registrations.Count>60)throw new InvalidDataException("Kernel lacks three reward model registration slots.");
        var bank=body.Subfiles()[2];int start=bank.Offset+bank.Length;
        // Original0E root mod8=4 and its entire7EC allocation ends exactly at0F.
        // Generic independently aligned imports would insert four bytes into the renderer's read-ahead owner.
        body.GrowSubfile(2,0x2000);
        int[] classes=[0xE,0xF,0x10],relative=[4,0x7F0,0xF80];
        for(int i=0;i<classes.Length;i++)
        {
            int root=start+relative[i],slot=registrations.Count+i+1;_packages[classes[i]].CopyTo(body.Bytes,root);
            BinaryPrimitives.WriteInt32LittleEndian(body.Bytes.AsSpan(0x50+slot*4),root);
            BinaryPrimitives.WriteUInt16LittleEndian(body.Bytes.AsSpan(0x150+slot*2),(ushort)classes[i]);
        }
        // Complete330 source10 plus a conservative888 total reserved extent, as a conservative policy, not an authenticated native requirement.
        // Sector rounding owns the remaining zero suffix; no original payload is replaced or shortened.
        RequireInstalled(body.Bytes);
    }
    internal void RequireInstalled(byte[] bytes)
    {
        var body=new UniversalLevelData(bytes);var registrations=body.Registrations();var roots=new Dictionary<int,int>();
        foreach(var spec in NativeId65FreshWizardResearchFallbackResourceSource.PackageSpecs)
        {
            var matches=registrations.Where(r=>r.ClassId==spec.Class).ToArray();
            if(matches.Length!=1)throw new InvalidDataException("An enemy reward fallback model is missing or duplicated; retain its engine resources.");
            int root=matches[0].Root;roots[spec.Class]=root;
            if((root&7)!=(spec.At&7)||!body.Bytes.AsSpan(root,spec.Length).SequenceEqual(_packages[spec.Class]))
                throw new InvalidDataException("An enemy reward fallback changed its complete source model or native alignment.");
        }
        if(roots[0xE]+0x7EC!=roots[0xF]||roots[0xF]+0x78C>roots[0x10]||
            !body.Bytes.AsSpan(roots[0xE]+0x7EC,8).SequenceEqual(_packages[0xF].AsSpan(0,8))||
            registrations.Any(r=>r.Root>roots[0x10]&&r.Root<roots[0x10]+0x888)||body.Subfiles()[2].Offset+body.Subfiles()[2].Length<roots[0x10]+0x888)
            throw new InvalidDataException("Reward fallback registration order, native read-ahead or runtime reservation changed.");
        var gpu=body.Subfiles()[0];foreach(var span in _textureSpans)
            if(!body.Bytes.AsSpan(gpu.Offset+span.At,span.Length).SequenceEqual(_town.AsSpan(0x800+span.At,span.Length)))
                throw new InvalidDataException("An enemy reward fallback texture or palette was overwritten.");
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(ReadOnlySpan<byte> bytes,int length,string hash){if(bytes.Length!=length||Hash(bytes)!=hash)throw new InvalidDataException("Original reward fallback source changed.");}
}

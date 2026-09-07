using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65FreshWizardFallbackResourceSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>Neutral exact Town0E shape guard, not an animated codec or source issuer.</summary>
internal static class NativeTownFallbackSimpleModel
{
    internal static void RequireShape(ReadOnlySpan<byte> package)
    {
        if(package.Length!=0x7EC || Word(package,0)!=0xFF31A178 || Word(package,4)!=0x178 ||
            Word(package,8)!=0x2E0 || Word(package,12)!=0x2E4 || 0x2E4+package[1]*8!=package.Length)
            throw new InvalidDataException("Exact Town0E simple header/pointer/fixed8-face owner changed; this is not animated grammar.");
        // The renderer prefetches two more words from the following package.
        // Their classified source ownership is a separate capture/plan check.
    }
    private static uint Word(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
}

/// <summary>Closed original-source capture; no caller package/roster/owner certificate.</summary>
internal sealed class NativeId65FreshWizardResearchFallbackResourceSource
{
    internal const long TownWad=0x119E000;
    internal static IReadOnlyList<(ushort Class,int At,int Length,string Hash)> PackageSpecs { get; } = Array.AsReadOnly(new (ushort Class,int At,int Length,string Hash)[]{
        (0xE,0x1C8D04,0x7EC,"ca65a14da356546aa00e308319e637809c87c9de8f0901b353de9b377a4a4a62"),
        (0xF,0x1C94F0,0x78C,"4e1dbb926be5b833e1fb314a154de05f9dd104c5a2148713b2a9e9e9094613f1"),
        (0x10,0x1BED08,0x330,"947da1119d6159e0a496c91850e167706c6f57e0e8615646d2ae1516c2fa7257")});
    private NativeId65FreshWizardResearchFallbackResourceSource(NativeId65FreshWizardResearchResourcePlan resource,
        NativeId65EngineProfileContract engine)
    {
        Resource=resource;
        resource.RequireAncestry(resource.CompilationAncestor,resource.FamilyAncestor);
        NativeId65FreshWizardResearchResourceSource.RequireFixedIntent(resource.CompilationAncestor.Intent);
        Upload=resource.CopyUploadPayload();Data=resource.CopyDataComponents();Models=resource.CopyModelPayload();
        resource.RequireExactOutputs(Upload,Data,Models);
        Require(Upload,0xE5800,"d74c2571ed857865b2b989af3001117f44e0915f9c6d59927e30a49d90a79402");
        Require(Data,0x8FC,"bea93c58d6d568c7c6162816ddad1afff32a51886f9412a0809b22887e0b83d4");
        Require(Models,0x17800,"782c4500b3152f6051b45b8a0a56f6c99c4f703c753948f656318ccfa8618cda");
        NativeId65FreshWizardResearchResourceSource.RequireInstalledGrammar(Data,Models);
        if(!resource.ModelRoots.SequenceEqual(new[]{new NativeFreshWizardModelRoot(0,0,0x10A4C,0x10A4C),
            new(0x78,0x10A4C,0x2BD0,0x2BD0),new(0x26,0x1361C,0x564,0x564),new(0x11B,0x13B80,0x35B4,0x3C80)}) ||
            engine.SourceImageSha256!=resource.SourceImageSha256 || engine.SharedPeteClasses.Count!=54 ||
            !engine.SharedPeteClasses.Contains((ushort)0x104) || !engine.SharedPeteClasses.Contains((ushort)0x105) ||
            PackageSpecs.Any(p=>engine.SharedPeteClasses.Contains(p.Class)))
            throw new InvalidDataException("Actual fixed resource/shared/local registration identity changed.");
        Town=engine.CopyTownData();byte[] exe=engine.CopyExecutable();
        Require(Town,0x2E2000,"7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0");
        Require(exe,0x66000,"a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998");
        if(!exe.AsSpan().SequenceEqual(resource.CompilationAncestor.FreshResourceAncestor.ResourceChainAncestor.Resources.Surface.CopySourceExecutable()))
            throw new InvalidDataException("Fallback capture lost the actual resource's original executable ancestry.");
        foreach(var pin in new (uint Address,int Length,string Hash)[]{
            (0x800133E0,0x26C,"fede3c414aea2dce0372fce574549d961123cf6456a7a291acef48b9c4af8598"),
            (0x80022A2C,0x1098,"7467b9b2e8b2a319736e5d8c1ae2d482f863abc17fc52ff02be464fee0c9c812"),
            (0x800529CC,0x18,"4973eb40a058490002e752ee189564f94c173b9489f03b6f77bd25f2b2a3c072")})
            Require(exe.AsSpan(checked((int)(pin.Address-0x8000F800)),pin.Length),pin.Length,pin.Hash);
        var original=new NativeAddressSpace(NativeAddressKind.LevelData,"fresh-fallback-original-Town",NativeAddressRevision.Original,Town.Length);
        var registrations=NativeMobyModelRegistrationSource.CaptureExactTown(original,Read);
        Packages=new();
        foreach(var spec in PackageSpecs)
        {
            var root=registrations.Registrations.Single(r=>r.ActorId==spec.Class);
            long end=root.Slot+1<registrations.Registrations.Count ? registrations.Registrations[root.Slot+1].RootDataOffset : registrations.ModelEnd;
            if(root.RootDataOffset!=spec.At || end-root.RootDataOffset!=spec.Length)
                throw new InvalidDataException("Fallback model is not its complete native source registration allocation.");
            byte[] package=Town.AsSpan(spec.At,spec.Length).ToArray();Require(package,spec.Length,spec.Hash);Packages.Add(spec.Class,package);
        }
        NativeTownFallbackSimpleModel.RequireShape(Packages[0xE]);
        if(!Town.AsSpan(0x1C94F0,8).SequenceEqual(Convert.FromHexString("01000000ffffffff")) ||
            PackageSpecs[0].At+PackageSpecs[0].Length!=PackageSpecs[1].At)
            throw new InvalidDataException("Simple renderer's following eight-byte source owner is not original0F.");
        var regions=new List<(int At,int Width,int Rows,bool Alias)>();
        foreach(ushort cls in new ushort[]{0xF,0x10})
        {
            byte[] package=Packages[cls];var layout=NativeMobyModelTextureLayout.Decode(package);
            if(Word(package,0)!=1 || package.AsSpan(4,16).ContainsAnyExcept((byte)255) || package.AsSpan(20,32).ContainsAnyExcept((byte)0) ||
                layout.References.Count!=(cls==0xF?72:8))throw new InvalidDataException("Fallback animation/local audio/collider/texture schema changed.");
            foreach(var group in layout.References.GroupBy(r=>(r.Page,r.Palette)))
            {
                var uv=group.SelectMany(r=>{var b=r.CopyBytes();return new (int U,int V)[]{(b[0],b[1]),(b[4],b[5]),(b[8],b[9]),(b[10],b[11])};}).ToArray();
                int u0=uv.Min(p=>p.U),u1=uv.Max(p=>p.U),v0=uv.Min(p=>p.V),v1=uv.Max(p=>p.V);
                int x=(group.Key.Page&15)*128-1024+u0/2,y=((group.Key.Page&16)!=0?256:0)+v0;
                int width=u1/2-u0/2+1,rows=v1-v0+1,cx=(group.Key.Palette&63)*32-1024,cy=group.Key.Palette>>6;
                NativeId65FreshWizardResearchResourceSource.RequireRectangle(x,y,width,rows,false);
                NativeId65FreshWizardResearchResourceSource.RequireRectangle(cx,cy,32,1,true);
                bool alias=cls==0xF&&group.Key.Page==0x3F;
                regions.Add((y*1024+x,width,rows,alias));regions.Add((cy*1024+cx,32,1,false));
            }
        }
        if(!regions.OrderBy(r=>r.At).SequenceEqual(new[]{(0x48320,16,32,false),(0x582B0,16,32,false),(0x6C3A0,2,4,true),
            (0x722E0,32,1,false),(0x7C2C0,32,1,false),(0x7E2C0,32,1,false)}))
            throw new InvalidDataException("Actual fallback descriptors do not produce the exact three source texture pairs.");
        Mask=new byte[0x80000];int cursor=0;
        var oldFresh=resource.CompilationAncestor.FreshResourceAncestor;
        foreach(var owner in resource.Owners.Where(o=>o.Domain==NativeFreshWizardResourceDomain.Upload))
        {
            if(owner.Offset!=cursor || owner.ByteLength<=0 || owner.ByteLength>Upload.Length-cursor || Hash(Upload.AsSpan(cursor,owner.ByteLength))!=owner.Sha256)
                throw new InvalidDataException("Prior resource upload ownership is not complete/bound.");
            if(cursor<0x80000)
            {
                if(owner.ByteLength>0x80000-cursor)throw new InvalidDataException("Prior owner crosses VRAM/audio domains.");
                byte tag=owner.Provenance switch {"fresh-zero-upload-background"=>0,"retained-fixed-Town-presentation-VRAM"=>1,
                    "issued-detached-companion-VRAM"=>2,"source-texture-insertion"=>3,_=>throw new InvalidDataException("Unknown prior VRAM owner classification.")};
                if(tag==0 && Upload.AsSpan(cursor,owner.ByteLength).ContainsAnyExcept((byte)0))throw new InvalidDataException("Prior background is not generated zero.");
                if(tag==1)
                {
                    var freshOwner=oldFresh.UploadOwners.Single(o=>o.Offset<=cursor&&o.Offset+o.ByteLength>=cursor+owner.ByteLength);
                    if(freshOwner.Provenance!="retained-fixed-Town-presentation-VRAM" ||
                        freshOwner.SourceWadOffset+(cursor-freshOwner.Offset)!=TownWad+0x800+cursor ||
                        !Upload.AsSpan(cursor,owner.ByteLength).SequenceEqual(Town.AsSpan(0x800+cursor,owner.ByteLength)))
                        throw new InvalidDataException("Retained alias lost its actual original Town source owner.");
                }
                Array.Fill(Mask,tag,cursor,owner.ByteLength);
            }
            cursor+=owner.ByteLength;
        }
        if(cursor!=Upload.Length)throw new InvalidDataException("Prior upload owner suffix is missing.");
        var spans=new List<NativeFreshWizardFallbackTextureSpan>();var aliases=new List<NativeFreshWizardFallbackTextureSpan>();
        var assigned=new bool[0x80000];
        foreach(var r in regions)
        for(int row=0;row<r.Rows;row++)
        {
            int at=r.At+row*1024;
            for(int i=at;i<at+r.Width;i++)
            {if(assigned[i])throw new InvalidDataException("Fallback texture source extents partially overlap.");assigned[i]=true;RequireTextureOwner(Mask[i],Upload[i],Town[0x800+i],r.Alias);}
            var span=new NativeFreshWizardFallbackTextureSpan(at,r.Width,TownWad+0x800+at,Hash(Town.AsSpan(0x800+at,r.Width)));
            (r.Alias?aliases:spans).Add(span);
        }
        TextureSourceSpans=spans.OrderBy(s=>s.Offset).ToArray();RetainedTextureAliases=aliases.OrderBy(s=>s.Offset).ToArray();
        if(TextureSourceSpans.Length!=67 || TextureSourceSpans.Sum(s=>s.ByteLength)!=1120 || RetainedTextureAliases.Length!=4 ||
            RetainedTextureAliases.Sum(s=>s.ByteLength)!=8)throw new InvalidDataException("Exact new/aliased source ownership budget changed.");
        return;
        byte[] Read(NativeByteExtent e)
        {if(e.Space!=original || e.Offset<0 || e.EndExclusive>Town.Length)throw new InvalidDataException("Read escaped actual Town owner.");return Town.AsSpan(checked((int)e.Offset),checked((int)e.ByteLength)).ToArray();}
    }
    internal NativeId65FreshWizardResearchResourcePlan Resource{get;}
    internal byte[] Upload{get;}
    internal byte[] Data{get;}
    internal byte[] Models{get;}
    internal byte[] Town{get;}
    internal byte[] Mask{get;}
    internal Dictionary<ushort,byte[]> Packages{get;}
    internal NativeFreshWizardFallbackTextureSpan[] TextureSourceSpans{get;}
    internal NativeFreshWizardFallbackTextureSpan[] RetainedTextureAliases{get;}
    internal static async Task<NativeId65FreshWizardResearchFallbackResourceSource> CaptureAsync(string path,
        NativeId65FreshWizardResearchResourcePlan resource,CancellationToken token)
    {var engine=await NativeId65EngineProfileContract.CaptureAsync(path,token);token.ThrowIfCancellationRequested();return new(resource,engine);}
    internal static void RequireTextureOwner(byte tag,byte current,byte source,bool alias)
    {if(alias ? tag!=1||current!=source : tag!=0||current!=0)throw new InvalidDataException("Texture changes protected/nonbackground ownership or aliases a different source byte.");}
    internal static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    internal static void Require(ReadOnlySpan<byte> b,int length,string hash)
    {if(b.Length!=length||Hash(b)!=hash)throw new InvalidDataException("Complete original resource/package/source pin changed.");}
    private static uint Word(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
}

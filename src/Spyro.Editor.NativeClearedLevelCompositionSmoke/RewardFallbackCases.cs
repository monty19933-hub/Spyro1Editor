using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

internal static class RewardFallbackCases
{
    internal static void RequirePreserved(UniversalSourceLibrary donors,byte[] bytes,Action<bool,string> check,string scope)
    {
        var body=new UniversalLevelData(bytes);var town=donors.Get("townsquare").Data;
        foreach(int cls in new[]{0xE,0xF,0x10})
        {
            var source=town.Registrations().Single(r=>r.ClassId==cls);var registered=body.Registrations().Where(r=>r.ClassId==cls).ToArray();
            check(registered.Length==1,scope+" has one native reward fallback registration "+cls);
            byte[] complete=town.Package(cls);int root=registered.Single().Root;
            check(body.Bytes.AsSpan(root,complete.Length).SequenceEqual(complete),scope+" retains complete original reward model "+cls);
            check((root&7)==(source.Root&7),scope+" retains original reward model alignment "+cls);
        }
        int e=body.Registrations().Single(r=>r.ClassId==0xE).Root,f=body.Registrations().Single(r=>r.ClassId==0xF).Root;
        check(e+town.Package(0xE).Length==f&&body.Bytes.AsSpan(f,8).SequenceEqual(town.Package(0xF).AsSpan(0,8)),scope+" retains native simple-model following-eight-byte owner");
        var gpu=body.Vram();var originalGpu=town.Vram();
        foreach(var region in Regions)for(int row=0;row<region.Rows;row++)
        {
            int at=region.At+row*1024;check(gpu.Slice(at,region.Width).SequenceEqual(originalGpu.Slice(at,region.Width)),scope+" retains complete reward texture/palette row "+at);
        }
    }
    internal static void RequireProtected(string image,LevelDefinition level,Action<bool,string> check,string scope)
    {
        var ranges=UniversalMobyExport.DefaultProtectedVram(image,level).ToArray();
        foreach(var region in Regions)for(int row=0;row<region.Rows;row++)
        {
            int at=region.At+row*1024;
            check(Enumerable.Range(at,region.Width).All(i=>ranges.Any(r=>r.Offset<=i&&i<r.Offset+r.Length)),scope+" protects every reward texel/CLUT including zero bytes "+at);
        }
    }
    private static readonly (int At,int Width,int Rows)[] Regions=[(0x48320,16,32),(0x582B0,16,32),(0x6C3A0,2,4),(0x722E0,32,1),(0x7C2C0,32,1),(0x7E2C0,32,1)];
}

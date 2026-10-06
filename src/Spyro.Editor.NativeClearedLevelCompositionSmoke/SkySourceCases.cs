using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

/// <summary>A built level that borrows a retail sky carries the block whole and lists every part in its one view group.</summary>
internal static class SkySourceCases
{
    private static uint Word(byte[] b,int o)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o));
    private static void W(byte[] b,int o,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o),v);
    // Minimal native sky block: size, background color, part count, payload-relative part pointers, parts of
    // one vertex, one color and one polygon each (24-byte header ending in two 0xFFFF words).
    private static byte[] SkyBlock(int parts)
    {
        int first=8+parts*4,part=24+4+4+8;byte[] b=new byte[4+first+parts*part];
        W(b,0,(uint)b.Length);W(b,4,0x00C08040);W(b,8,(uint)parts);
        for(int i=0;i<parts;i++)
        {
            int p=first+i*part;W(b,12+i*4,(uint)p);int at=4+p;
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(at+12),1);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(at+16),1);
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(at+18),1);W(b,at+20,0xFFFFFFFF);
        }
        return b;
    }
    internal static void Run(Action<bool,string> check)
    {
        var type=typeof(NativeAuthoredSpawn).Assembly.GetType("Spyro.Editor.Core.Exporting.NativeAuthoredMesh")!;
        byte[] Compile(SceneDocument d,byte[]? sky)=>(byte[])type.GetMethod("CompileWithSky",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[d,Array.Empty<AuthoredMaterial>(),sky])!;
        var doc=SceneDocument.New("Sky Proof");
        check(Spyro1SkyBlockAnalyzer.IsValidStandaloneSkyBlock(SkyBlock(3)),"synthetic sky block parses as one native block");
        byte[] plain=Compile(doc,null);
        check(Word(plain,plain.Length-16)==16,"a scene without a sky source keeps its 16-byte color sky");
        byte[] sky=SkyBlock(3);byte[] mesh=Compile(doc,sky);
        check(mesh.AsSpan(mesh.Length-sky.Length).SequenceEqual(sky),"the borrowed sky block is the last component, byte for byte");
        int vis=(int)Word(mesh,0),skyAt=vis+4+(int)Word(mesh,vis+4);
        check(Word(mesh,skyAt)==1&&Word(mesh,skyAt+4)==8,"the built level keeps one sky view group");
        check(mesh.AsSpan(skyAt+8,4).SequenceEqual(new byte[]{0,1,2,255}),"the sky view group lists every borrowed part then 255");
        check(Word(mesh,vis)==skyAt-vis+12,"the visibility component length covers its sky group");
        try{_=Compile(doc,[..sky.AsSpan(0,sky.Length-4)]);check(false,"a truncated sky block is refused");}
        catch(TargetInvocationException e) when(e.InnerException is InvalidDataException){check(true,"a truncated sky block is refused");}
        check(!Encoding.UTF8.GetString(doc.Serialize()).Contains("SkySourceWadEntry"),"scenes without a sky source serialize as before");
        var withSky=doc with{SkySourceWadEntry=58};withSky.Validate();
        check(Encoding.UTF8.GetString(withSky.Serialize()).Contains("\"SkySourceWadEntry\": 58"),"the sky source is saved with the scene");
        foreach(int bad in new[]{9,59,80})
        {
            try{(doc with{SkySourceWadEntry=bad}).Validate();check(false,"sky source "+bad+" is refused");}
            catch(InvalidDataException){check(true,"sky source "+bad+" is refused");}
        }
    }
}

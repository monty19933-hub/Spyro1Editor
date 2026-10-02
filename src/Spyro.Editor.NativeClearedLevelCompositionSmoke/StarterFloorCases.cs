using System.Buffers.Binary;
using System.Reflection;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

internal static class StarterFloorCases
{
    internal static async Task Run(Action<bool,string> check)
    {
        var document=SceneDocument.New("STARTER TEST");
        NativeAuthoredSpawn.RequireSupported(document);
        check(document.Spawn==new Vector3f(560,560,576)&&document.Heading==64&&document.Surfaces.Count==2,
            "larger starter retains original spawn heading and editable face identities");
        NativeAuthoredSpawn.RequireSupported(document with {Spawn=new(560,6509/16f,576)});
        check(true,"native portal floor crossing has complete player footprint support");
        var kernel=typeof(NativeAuthoredSpawn).Assembly.GetType("Spyro.Editor.Core.Exporting.NativeAuthoredMesh")!;
        byte[] mesh=(byte[])kernel.GetMethod("Compile",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,
            [document,Array.Empty<AuthoredMaterial>()])!;
        int Word(int offset)=>BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan(offset));
        var native=new List<Vector3f[]>();
        for(int i=0;i<Word(4);i++)
        {
            int at=Word(8+i*4)+4,xy=Word(at+8),z=Word(at+12);
            native.Add(Enumerable.Range(0,3).Select(v=>
            {
                uint packed=BinaryPrimitives.ReadUInt32LittleEndian(mesh.AsSpan(at+60+v*4));
                return new Vector3f((xy>>16)+(packed>>21),(xy&65535)+((packed>>10)&2047),(z>>16)+(packed&1023));
            }).ToArray());
        }
        double area=native.Sum(v=>Math.Abs((v[1].X-v[0].X)*(v[2].Y-v[0].Y)-(v[1].Y-v[0].Y)*(v[2].X-v[0].X))/2.0);
        check(native.Count==72&&area==384*384&&native.SelectMany(v=>v).All(v=>v.Z==544)&&
            native.SelectMany(v=>v).Min(v=>v.X)==368&&native.SelectMany(v=>v).Max(v=>v.X)==752&&
            native.SelectMany(v=>v).Min(v=>v.Y)==368&&native.SelectMany(v=>v).Max(v=>v.Y)==752,
            "independent native decoder retains full384 starter area and floor bounds");
        string root=Directory.CreateTempSubdirectory("spyro-starter-preservation-").FullName;
        var legacy=document with {Surfaces=[document.Surfaces[0] with {Vertices=[new(528,528,544),new(656,528,544),new(528,656,544)]},
            document.Surfaces[1] with {Vertices=[new(656,528,544),new(656,656,544),new(528,656,544)]}]};
        var sculpted=legacy with {Spawn=new(560,560,600),Surfaces=[legacy.Surfaces[0] with
            {Vertices=[new(528,528,544),new(656,528,560),new(528,656,548)],Material=new(12,0),
             NearColors=[new(10,20,30),new(40,50,60),new(70,80,90)]}]};
        foreach(var (name,saved) in new[]{("legacy128",legacy),("sculpted-material",sculpted),("empty",document with {Surfaces=[]})})
        {
            string path=Path.Combine(root,name+".scene.json"),before=saved.Fingerprint();
            await saved.SaveAsync(path);var reopened=SceneDocument.Read(path);
            check(reopened.Fingerprint()==before,"saved "+name+" scene is never resized by the new factory");
        }
    }
}

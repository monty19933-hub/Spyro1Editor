using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

int checks=0;
void Check(bool ok,string name) { checks++; if(!ok) throw new InvalidOperationException(name); }
void Supported(SceneDocument document,string name)
{
    string original=document.Fingerprint();
    NativeAuthoredSpawn.RequireSupported(document);
    Check(document.Fingerprint()==original,name+" and document immutability");
}
void Refused(SceneDocument document,string name)
{
    string original=document.Fingerprint();
    try { NativeAuthoredSpawn.RequireSupported(document);throw new InvalidOperationException(name+" incorrectly accepted"); }
    catch(InvalidDataException) { Check(document.Fingerprint()==original,name+" atomic refusal"); }
}
AuthoredSurface Face(params Vector3f[] vertices)=>new(Guid.NewGuid().ToString("N"),vertices,new(128,96,64),null);
AuthoredSurface Rectangle(float x0,float y0,float x1,float y1,float z=544)=>Face(new(x0,y0,z),new(x1,y0,z),new(x1,y1,z),new(x0,y1,z));
var seed=SceneDocument.New();
Supported(seed,"default two native triangles");
Supported(seed with {Surfaces=seed.Surfaces.Reverse().ToArray()},"face-order independent seam coverage");
Supported(seed with {Surfaces=seed.Surfaces.Select(s=>s with {Vertices=s.Vertices.Reverse().ToArray()}).ToArray()},"both windings normalized by collision compiler");
Supported(seed with {Surfaces=[Rectangle(528,528,656,656)]},"exact authored quad split");
Refused(seed with {Surfaces=[]},"empty floor");
Refused(seed with {Surfaces=[Rectangle(558,528,562,656)]},"narrow strip under player centre");
Refused(seed with {Surfaces=[Rectangle(560,528,656,656)]},"centre on outside edge");
Refused(seed with {Spawn=new(540,540,576)},"square corner footprint leaves default platform");
Refused(seed with {Spawn=new(560,560,566)},"less than native radius clearance");
Supported(seed with {Spawn=new(560,560,567)},"first whole-unit radius clearance");
Supported(seed with {Spawn=new(560,560,672)},"maximum downward gap inclusive");
Refused(seed with {Spawn=new(560,560,673)},"beyond maximum downward gap");
Refused(seed with {Spawn=new(560,560,543)},"floor above player");
Refused(seed with {Surfaces=[Face(new(528,528,544),new(560,560,544),new(592,592,544))]},"degenerate XY floor");
// All nine points in the former centre/eight-direction sampler are supported,
// but the spaces between these isolated islands remain genuine holes.
List<AuthoredSurface> islands=[Rectangle(559,559,561,561)];
for(int i=0;i<8;i++)
{
    double angle=i*Math.PI/4;
    float x=560+NativeAuthoredSpawn.PlayerRadius*(float)Math.Cos(angle),y=560+NativeAuthoredSpawn.PlayerRadius*(float)Math.Sin(angle);
    islands.Add(Rectangle(MathF.Floor(x)-1,MathF.Floor(y)-1,MathF.Ceiling(x)+1,MathF.Ceiling(y)+1));
}
Refused(seed with {Surfaces=islands},"nine-sample disconnected islands negative control");
// A small hole between the original sample rays must also be rejected.
Refused(seed with {Surfaces=[Rectangle(528,528,570,656),Rectangle(572,528,656,656),Rectangle(570,528,572,565),Rectangle(570,567,572,656)]},"interior two-unit hole");
Supported(seed with {Surfaces=[Rectangle(528,528,570,656),Rectangle(570,528,656,656)]},"multiple disjoint triangles cover square without hole");
var slope=Face(new(528,528,512),new(592,528,576),new(592,592,576),new(528,592,512));
Supported(seed with {Spawn=new(560,560,608),Surfaces=[slope]},"45 degree slope full footprint and clipped height bounds");
Refused(seed with {Spawn=new(560,560,608),Surfaces=[slope with {Vertices=slope.Vertices.Select(v=>v with {Z=v.Z+(v.X-528)}).ToArray()}]},"steeper than45 degrees");
Refused(seed with {Spawn=new(560,560,576),Surfaces=[slope]},"sloped centre safe but edge lacks headroom");
Supported(seed with {Spawn=new(14000,14000,576),Surfaces=[Rectangle(13968,13968,14096,14096)]},"large-coordinate local precision");
Supported(seed with {Surfaces=[..seed.Surfaces,Rectangle(528,528,656,656)]},"overlapping floor does not manufacture gaps");
Supported(seed with {Spawn=new(560.249f,560,576),Surfaces=[Rectangle(538,528,620,656)]},"support uses exact native rounded spawn rather than uncompiled coordinates");

MeshFaceCacheCases.Run(Check);

if(args.Length==0) { Console.WriteLine("NATIVE_CLEARED_LEVEL_COMPOSITION_CHECKS_OK checks="+checks);return; }
if(args.Length is not 3 and not 4 || args.Length==4 && args[3]!="--replacement-only")
    throw new ArgumentException("Source fixture: ORIGINAL_USA CATALOG_WORKSPACE ARTIFACTS_DIRECTORY [--replacement-only]");
Directory.CreateDirectory(args[2]);
await SourceCases.Run(args[0],args[1],args[2],Check,args.Length==4);
File.WriteAllText(Path.Combine(args[2],"checks.json"),JsonSerializer.Serialize(new{checks,SourceSha256=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(args[0]))),Scope="source-bound static composition/written readback; no native runtime claim"},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("NATIVE_CLEARED_LEVEL_COMPOSITION_CHECKS_OK checks="+checks);

using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Levels;

if (args.Length is < 4 or > 5) throw new ArgumentException("source-BIN catalog-workspace source-overlay new-private-output [protected-cache-workspace-read-only]");
string directory=Path.GetFullPath(args[3]);
if(Directory.Exists(directory))throw new IOException("Choose a new private output directory.");
var level=LevelCatalog.Load(Path.GetFullPath(args[1])).FindByKey("artisans")!;
int checks=0;
void Check(bool value,string label){checks++;if(!value)throw new InvalidOperationException(label);}
string Digest(string path){using var stream=File.OpenRead(path);return Convert.ToHexStringLower(SHA256.HashData(stream));}
Dictionary<string,string> State(string root)=>Directory.Exists(root)?Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).ToDictionary(Path.GetFullPath,p=>Digest(p)+":"+File.GetLastWriteTimeUtc(p).Ticks):[];
bool Same(Dictionary<string,string> left,Dictionary<string,string> right)=>left.Count==right.Count&&left.All(p=>right.TryGetValue(p.Key,out var value)&&value==p.Value);
Directory.CreateDirectory(directory);
string sourceOverlayBefore=Digest(args[2]);
string project=Path.Combine(directory,"project");
Check(!PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCache(project,level.Key),"geometry-only project initially has no native materials");
var built=await NativeTerrainTextureCacheRecovery.EnsureAsync(project,Path.GetFullPath(args[0]),Path.GetFullPath(args[2]),level);
Check(built.Rebuilt&&built.TextureCount==68,"missing Artisans cache automatically rebuilds all68 source textures");
Check(PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCache(project,level.Key),"rebuilt PNGs and raw HQ/LQ sidecars pass independent validation");
var before=State(project);
var reused=await NativeTerrainTextureCacheRecovery.EnsureAsync(project,"","",level);
Check(!reused.Rebuilt&&reused.TextureCount==68,"valid native cache works without requiring the source disc again");
Check(Same(before,State(project)),"valid cache reuse preserves every byte and timestamp");
string normal=Path.Combine(project,"editor-cache","terrain-textures","artisans","artisans-texture-000-normal.png");
File.WriteAllBytes(normal,[0,1,2,3]);
Check(!PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCache(project,level.Key),"damaged PNG rejects the otherwise intact sidecars");
var repaired=await NativeTerrainTextureCacheRecovery.EnsureAsync(project,Path.GetFullPath(args[0]),Path.GetFullPath(args[2]),level);
Check(repaired.Rebuilt&&repaired.TextureCount==68&&PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCache(project,level.Key),"invalid material cache automatically repairs and validates completely");
string absent=Path.Combine(directory,"no-source");bool refused=false;string refusal="";
try{await NativeTerrainTextureCacheRecovery.EnsureAsync(absent,"","",level);}catch(InvalidDataException ex){refused=true;refusal=ex.Message;}
Check(refused&&refusal.Contains("Select the source Spyro BIN/CUE"),"missing source reports actionable refusal instead of flat material success");
Check(!Directory.Exists(absent),"missing-source refusal creates no cache or modifies an existing project");
Check(Digest(args[2])==sourceOverlayBefore,"material recovery preserves every source terrain geometry byte");
bool? protectedCacheValid=null;
if(args.Length==5)
{
    string protectedRoot=Path.GetFullPath(args[4]);string protectedDirectory=Path.Combine(protectedRoot,"editor-cache","terrain-textures","artisans");
    var protectedBefore=State(protectedDirectory);
    protectedCacheValid=PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCache(protectedRoot,level.Key);
    Check(protectedCacheValid.Value,"protected user project already contains validated original Artisans materials");
    Check(Same(protectedBefore,State(protectedDirectory)),"protected user cache audit is strictly read-only");
}
File.WriteAllText(Path.Combine(directory,"texture-cache-recovery.json"),JsonSerializer.Serialize(new{checks,Source=Path.GetFullPath(args[0]),SourceHash=Digest(args[0]),SourceOverlay=Path.GetFullPath(args[2]),SourceOverlayHash=Digest(args[2]),CoreMvid=typeof(NativeTerrainTextureCacheRecovery).Module.ModuleVersionId,CoreHash=Digest(typeof(NativeTerrainTextureCacheRecovery).Assembly.Location),MissingCache=built,ReusedCache=reused,RepairedCache=repaired,MissingSourceRefusal=refusal,ProtectedCacheValid=protectedCacheValid,RuntimeTested=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("TERRAIN_TEXTURE_CACHE_RECOVERY_OK checks="+checks);

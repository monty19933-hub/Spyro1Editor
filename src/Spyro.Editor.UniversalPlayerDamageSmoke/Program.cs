using System.Buffers.Binary;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

int checks=0;void Check(bool v,string m){checks++;if(!v)throw new InvalidOperationException(m);}
void Refuse(byte[] p,byte[] e,string m){try{UniversalPlayerDamageDependencies.Analyze(p,e);throw new InvalidOperationException("Accepted "+m);}catch(InvalidDataException){checks++;}}
Refuse(new byte[16],new byte[0x66000],"uncertified executable");
if(args.Length==0){Console.WriteLine($"UNIVERSAL_PLAYER_DAMAGE_SMOKE_OK {checks} source-free preimage checks; actual source fixture optional");return;}
byte[] exe=File.ReadAllBytes(args[0]);
Check(UniversalPlayerDamageDependencies.MapDamageMask(0x800,exe).SequenceEqual([16,18]),"direct overlay mask uses same certified state map");
Check(UniversalPlayerDamageDependencies.MapDamageMask(0x120,exe).SequenceEqual([8,23]),"independent colliders retain both hazard dependencies");
byte[] Model(int shape,uint flags,bool positive=false)
{
 int stride=new[]{8,12,20,12}[shape];byte[] p=new byte[0x44+stride];
 BinaryPrimitives.WriteUInt32LittleEndian(p,1);BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(0x14),(positive?0u:0x80000000)|0x40u);
 BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(0x34),0x3C);
 BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(0x44),(uint)(shape<<8)|1);
 BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(0x44+stride-4),flags);return p;
}
foreach(var (mask,ids) in new (uint,int[])[]{(0,[]),(1,[15]),(2,[13]),(4,[13]),(6,[13,15]),(0x10,[26,32]),(0x20,[8]),(0x40,[28]),(0x80,[29]),(0x100,[23]),(0x400,[30]),(0x800,[16,18]),(0x8000,[13]),(0x106,[13,15,23])})
 foreach(int shape in Enumerable.Range(0,4))
 {
  var r=UniversalPlayerDamageDependencies.Analyze(Model(shape,mask),exe);
  Check(r.DamageMask==mask && r.AnimationIds.SequenceEqual(ids),$"shape{shape}/mask{mask:X} exact minimal states");
  Check(r.Descriptors.Count==1 && r.Descriptors[0].Shape==shape,"native variable-stride evidence");
  Check(!r.CompleteNativeDependenciesVerified,"no false full dependency claim");
 }
var polygon=UniversalPlayerDamageDependencies.Analyze(Model(2,0x70608FF,true),exe);
Check(polygon.DamageMask==0&&polygon.AnimationIds.Count==0&&polygon.PolygonGroupSlots.SequenceEqual([0]),"secondary polygon bytes never become primary damage masks");
byte[] none=new byte[0x3C];BinaryPrimitives.WriteUInt32LittleEndian(none,1);
Check(UniversalPlayerDamageDependencies.Analyze(none,exe).AnimationIds.Count==0,"proven absent collider pointers");
byte[] bad=Model(0,0x100);bad[0x44]=0;Refuse(bad,exe,"missing terminal");
bad=Model(0,0x100);bad[0x45]=4;Refuse(bad,exe,"unknown shape");
bad=Model(0,0x100);bad[0x44]|=0x80;Refuse(bad,exe,"reserved command bits");
bad=Model(0,0x100);BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(0x14),0x80000014);Refuse(bad,exe,"header alias");
bad=Model(0,0x100);BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(0x18),0x40);Refuse(bad,exe,"mixed collision partitions");
bad=Model(0,0x100);BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(0x14),0x80000041);Refuse(bad,exe,"misaligned owner");
bad=Model(0,0x100);BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(0x14),0x8000FFFF);Refuse(bad,exe,"escaped owner");
bad=Model(0,0x100);BinaryPrimitives.WriteUInt32LittleEndian(bad,10000);Refuse(bad,exe,"unbounded animation roots");
bad=new byte[16];BinaryPrimitives.WriteUInt32LittleEndian(bad,0x80000000);Refuse(bad,exe,"unbounded simple grammar");
foreach(var c in UniversalPlayerDamageDependencies.Analyze(none,exe).Certificates)
{var changed=exe.ToArray();changed[c.ExecutableOffset]^=1;Refuse(none,changed,c.Id);}
if(args.Length>=2)
{
 foreach(var name in new[]{"alpineridge","magiccrafters","wizardpeak"})
 {
  var r=UniversalPlayerDamageDependencies.Analyze(File.ReadAllBytes(Path.Combine(args[1],name+"-model.bin")),exe);
  Check(name=="wizardpeak"?r.DamageMask==1:r.DamageMask==0x106,name+" exact actual model mask");
  Check(name=="wizardpeak"?r.AnimationIds.SequenceEqual([15]):r.AnimationIds.SequenceEqual([13,15,23]),name+" model driven animations");
 }
}
if(args.Length>=4)
{
 var catalog=LevelCatalog.Load(args[3]);Check(catalog.Levels.Count>=35,"complete original level catalog");var lib=new UniversalSourceLibrary(args[2],catalog);int models=0;
 foreach(var level in catalog.Levels.Where(l=>l.LevelId>=10&&l.LevelId<=64))
 {
  var data=lib.Get(level.Key).Data;
  foreach(var reg in data.Registrations())
  {var r=UniversalPlayerDamageDependencies.Analyze(data.Package(reg.ClassId),exe);Check(r.AnimationIds.All(i=>i>=0&&i<46),"bounded source result");models++;}
 }
 Console.WriteLine($"ACTUAL_NATIVE_MODEL_COLLIDER_INVENTORY_OK {models} packages");
}
Console.WriteLine($"UNIVERSAL_PLAYER_DAMAGE_SMOKE_OK {checks} checks");

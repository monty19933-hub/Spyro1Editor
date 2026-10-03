using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
namespace Spyro.Editor.Core.Universal;

/// <summary>Exact original Alpine11/44/53 actor/resource prerequisite. This does
/// not enroll rows, issue world ownership, allocate memory, or authorize export.
/// The installed six-sector world capability must own and delegate this instance.</summary>
internal sealed partial class UniversalGreenDruidGroup8Actors
{
 internal static readonly int[] Members=[11,44,53];
 internal static readonly int[] Classes=[0x10E,0x127,0x13E,0x146,0x148];
 internal static readonly int[] Modeled=[0x10E,0x127,0x13E,0x146];
 private readonly UniversalLevelData _source;
 private readonly UniversalOverlay _overlay;
 private readonly byte[] _exe;
 private Dictionary<int,int>? _map;
 private Dictionary<int,int>? _groups;
 private readonly Dictionary<int,byte[]> _packages=[];
 internal int OriginalDynamicCapacity=>UniversalMobyComposer.ArenaSlots(_source);
 private static int I(ReadOnlySpan<byte>b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
 private static string H(ReadOnlySpan<byte>b)=>Convert.ToHexStringLower(SHA256.HashData(b));
 private static void Require(bool v,string why){if(!v)throw new InvalidDataException("Alpine linked druid group: "+why);}
 private UniversalGreenDruidGroup8Actors(UniversalLevelData data,UniversalOverlay overlay,byte[]exe)
 {_source=new(data.Bytes.ToArray());_overlay=new(overlay.Bytes.ToArray(),overlay.Bindings.ToDictionary());_exe=exe.ToArray();foreach(int c in Modeled)_packages.Add(c,data.Package(c));}
 internal static UniversalGreenDruidGroup8Actors CaptureSource(UniversalLevelData data,UniversalOverlay overlay,byte[]exe)
 {
  UniversalGreenDruidWorldRows.RequireSource(data,overlay);Require(H(exe)==UniversalGreenDruidWorldPrerequisites.ExeSha,"complete original executable owner");
  Require(data.Scene().RowCount==198&&UniversalMobyComposer.ArenaSlots(data)==91,"complete original dynamic object reservation");
  foreach(var (c,root,bytes,sha) in new (int,int,int,string)[]{
   (0x10E,0x1B5A60,25040,"fe97589a40a11d48008d9a28f81dd5577172697825224528666f2c2ef69a1267"),
   (0x146,0x1BBDA4,31844,"c9c6755a5b12f2942b65592bad08f28148fad28d874ff7f0c30723ce60d774ea"),
   (0x127,0x1D73E4,272,"0e09765dfe418f955a90bc706ce6ca673ba3e256a2ab237acd8daa3275e1570f"),
   (0x13E,0x1D7310,212,"32c19de0bd4f681360e9fe87a1ef59e7207158081a2b36624c1abb8c8d056258")})
  {Require(data.Registrations().Single(r=>r.ClassId==c).Root==root&&data.Package(c).Length==bytes&&H(data.Package(c))==sha,"complete source model registration/allocation");}
  Require(!data.Registrations().Any(r=>r.ClassId==0x148),"native cache target has no local model");
  var d=data.CaptureRow("alpineridge",11);var w=data.CaptureRow("alpineridge",44);var t=data.CaptureRow("alpineridge",53);
  Require(d.ClassId==0x10E&&d.Properties.Length==164&&d.InternalPointers.SequenceEqual(new[]{(0,92)})&&d.Properties[92]==4&&I(d.Properties,4)==8&&I(d.Properties,8)==53&&I(d.Properties,12)==44&&I(d.Properties,16)==3,"complete original mode3 druid/path/reciprocal indices");
  Require(w.ClassId==0x146&&w.Properties.Length==204&&w.InternalPointers.SequenceEqual(new[]{(12,52)})&&w.Properties[52]==9&&I(w.Properties,8)==11&&I(w.Properties,16)==1&&I(w.Properties,32)==0,"complete original wizard/path/reciprocal index");
  Require(t.ClassId==0x148&&t.Properties.Length==4&&I(t.Properties,0)==-1&&new[]{d,w,t}.All(x=>x.ExternalRowPointers.Count==0&&x.DroppedExternalPointers==0),"original target cache and pointer topology");
  var sound=new NativeLevelSoundLayout(data.Bytes).Sound;Require(sound.Map[38]==22&&sound.Descriptors[22].Sample.Sha256=="b38aa1eabec75d1a313e06a5e7276a4a49b43cdd5be9ec35b0b847eadc9e3cf9"&&sound.Descriptors[22].Sample.ByteLength==3264,"complete common poof sample owner");
  Require(overlay.Word(0x8007B6C4+10*4)==0x80088344&&overlay.Word(0x80087E88)==0xAE550000&&overlay.Word(0x80088374)==0x24A5000C,"linked projectile follows druid; resetstate10 copies current druid XYZ");
  return new(data,overlay,exe);
 }
 internal UniversalRowTemplate CaptureRow(int row,string group)
 {
  Require(Members.Contains(row)&&!string.IsNullOrWhiteSpace(group),"three exact source members need a linked group");var t=_source.CaptureRow("alpineridge",row);
  var links=row switch{11=>new[]{new UniversalSourceRowIndex(8,53,0x148),new UniversalSourceRowIndex(12,44,0x146)},44=>new[]{new UniversalSourceRowIndex(8,11,0x10E)},_=>[]};
  return t with{LinkGroupId=group,SourceRowIndices=links,SourcePrerequisiteRows=Members.Where(r=>r!=row).Select(r=>new UniversalSourcePrerequisite(r,_source.CaptureRow("alpineridge",r).ClassId)).ToArray()};
 }
 internal void RequirePrerequisites(UniversalRowTemplate owner,IReadOnlyList<UniversalRowTemplate> rows)
 {
  var original=CaptureRow(owner.SourceRowIndex,owner.LinkGroupId??"");
  Require(owner.SourceLevelKey==original.SourceLevelKey&&owner.ClassId==original.ClassId&&owner.Row.SequenceEqual(original.Row)&&owner.SourceRowIndices.SequenceEqual(original.SourceRowIndices)&&owner.SourcePrerequisiteRows.SequenceEqual(original.SourcePrerequisiteRows)&&owner.InternalPointers.SequenceEqual(original.InternalPointers)&&owner.ExternalRowPointers.Count==0&&owner.DroppedExternalPointers==0,"exact original link/class/path metadata");
  foreach(int row in Members)Require(rows.Count(t=>t.SourceLevelKey=="alpineridge"&&t.SourceRowIndex==row&&t.ClassId==_source.CaptureRow("alpineridge",row).ClassId&&t.LinkGroupId==owner.LinkGroupId)==1,"each original member exactly once in its issued linked group");
 }
 // Green's world certificate performs translation. Authenticate the same raw
 // delta here, including every node and every unchanged input/cache/state byte.
 internal void BindGroups(IReadOnlyDictionary<int,int> groups)
 {
  Require(groups.Count==2&&groups.Keys.Order().SequenceEqual(new[]{8,9})&&groups.Values.Distinct().Count()==2&&groups.Values.All(g=>g is >=0 and <255),"two issued group byte owners");
  if(_groups!=null)Require(_groups.All(p=>groups.TryGetValue(p.Key,out int g)&&g==p.Value),"issued animation groups cannot be rebound");_groups=groups.ToDictionary();
 }
 internal void RequireTranslatedRows(IReadOnlyList<(byte[]Row,UniversalRowTemplate Template)> rows,IReadOnlyList<int>delta,IReadOnlyList<int>physical)
 {
  Require(delta.Count==3&&delta.All(d=>d%16==0)&&rows.Count==3&&physical.Count==3&&physical.Distinct().Count()==3&&physical.All(r=>r is >=0 and <=255),"whole-unit common translation and three physical byte-index owners");
  Require(rows.Select(r=>r.Template.SourceRowIndex).Order().SequenceEqual(Members)&&rows.Select(r=>r.Template.LinkGroupId).Distinct().Count()==1&&!string.IsNullOrWhiteSpace(rows[0].Template.LinkGroupId),"one complete linked group");
  var templates=rows.Select(r=>r.Template).ToArray();
  foreach(var (row,t) in rows)
  {
   var original=CaptureRow(t.SourceRowIndex,t.LinkGroupId!);RequirePrerequisites(t,templates);Require(t.SourceLevelKey==original.SourceLevelKey&&t.ClassId==original.ClassId&&t.Row.SequenceEqual(original.Row)&&t.InternalPointers.SequenceEqual(original.InternalPointers)&&t.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers)&&t.DroppedExternalPointers==0&&t.SourceRowIndices.SequenceEqual(original.SourceRowIndices)&&t.SourcePrerequisiteRows.SequenceEqual(original.SourcePrerequisiteRows),"source actor/index/path enrollment changed");
   byte[] expected=original.Properties.ToArray();if(t.SourceRowIndex==11){Require(_groups!=null,"issued group map precedes actor preparation");BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4),_groups![8]);}int path=t.SourceRowIndex==11?92:t.SourceRowIndex==44?52:-1;
   if(path>=0)for(int n=0;n<expected[path];n++)for(int a=0;a<3;a++){int at=path+8+n*16+a*4;long v=(long)I(expected,at)+delta[a];Require(v is >=0 and <262144,"every complete path node remains in native coordinates");BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(at),(int)v);}
   Require(t.Properties.SequenceEqual(expected)&&row.Length==88,"complete translated props/path/reset inputs changed");
   var rr=original.Row.ToArray();for(int a=0;a<3;a++){long v=(long)I(rr,12+4*a)+delta[a];Require(v is >=0 and <262144,"actor coordinate bounds");BinaryPrimitives.WriteInt32LittleEndian(rr.AsSpan(12+4*a),(int)v);}
   // Actual world installer owns the unrestricted native activation/pod policy;
   // all remaining native yaw/state/reward/render/model inputs stay authentic.
   Require(row[67]==255&&row[74]==255,"installed projected visibility/activation owner");rr[67]=row[67];rr[74]=row[74];
   int mapped=BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(54));Require(_map==null?mapped==original.ClassId:mapped==_map[original.ClassId],"issued physical class namespace");BinaryPrimitives.WriteUInt16LittleEndian(rr.AsSpan(54),(ushort)mapped);Require(rr.SequenceEqual(row),"native state/yaw/animation/reward row changed");
   var expectedLinks=original.SourceRowIndices.Select(l=>new UniversalImportedRowIndex(l.Offset,Array.FindIndex(templates,x=>x.SourceRowIndex==l.SourceRow),l.SourceRow,l.SourceClass)).ToArray();Require(t.ImportedRowIndices.SequenceEqual(expectedLinks)&&t.ImportedRowPointers.Count==0,"complete three reciprocal integer link fixups");
  }
 }
 internal void RequireFinalRows(UniversalLevelData final,IReadOnlyList<(byte[]Row,UniversalRowTemplate Template)> rows,IReadOnlyList<int>delta,IReadOnlyList<int>physical)
 {
  RequireTranslatedRows(rows,delta,physical);
  for(int n=0;n<rows.Count;n++)
  {
   var (row,t)=rows[n];var actual=final.CaptureRow("written",physical[n]);Require(row.AsSpan(4).SequenceEqual(actual.Row.AsSpan(4)),"final complete native row/state/class/reward/pose");
   var expected=t.Properties.ToArray();foreach(var link in t.ImportedRowIndices){int target=Array.FindIndex(rows.Select(r=>r.Template).ToArray(),r=>r.SourceRowIndex==link.SourceRow);Require(target>=0&&link.PlacementIndex==target,"final reciprocal target identity");BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(link.Offset),physical[target]);}
   var found=actual.Properties.ToArray();foreach(var ptr in t.InternalPointers)expected.AsSpan(ptr.Offset,4).Clear();foreach(var ptr in actual.InternalPointers)found.AsSpan(ptr.Offset,4).Clear();
   Require(expected.SequenceEqual(found)&&actual.InternalPointers.SequenceEqual(t.InternalPointers)&&actual.ExternalRowPointers.Count==0&&actual.DroppedExternalPointers==0,"final full paths/pointer topology/cache/group/three physical scalar links");
  }
 }
 internal void BindBundle(UniversalOverlay source,UniversalOverlay destination,IReadOnlyList<int>classes,IReadOnlyDictionary<int,int>map,byte[]currentExe)
 {
  Require(source.Bytes.SequenceEqual(_overlay.Bytes)&&source.Bindings.SequenceEqual(_overlay.Bindings),"complete source callback owner");Require(classes.SequenceEqual(Classes)&&map.Count==Classes.Length&&map.Keys.Order().SequenceEqual(Classes)&&map.Values.Distinct().Count()==Classes.Length&&map.Values.All(v=>v is >0 and <512),"complete actor/child/cache class namespace");RequireNative(currentExe);RequireDestination(destination);
  if(_map!=null)Require(_map.Count==map.Count&&_map.All(p=>map.TryGetValue(p.Key,out int v)&&p.Value==v),"issued physical namespace cannot be rebound");_map=map.ToDictionary();
 }
 internal void RequireNative(byte[]current,bool allowSaveMap=false)
 {
  UniversalGreenDruidWorldInstallation.RequireNative(current,_exe,allowSaveMap);
  var native=NativeDragonStatueColors.OriginalRendererView(current).ToArray();if(allowSaveMap)foreach(var w in NativeMobySaveMap.Windows())_exe.AsSpan((int)(w.Address-UniversalExeBindings.ExeBase),w.Code.Length).CopyTo(native.AsSpan((int)(w.Address-UniversalExeBindings.ExeBase),w.Code.Length));
  foreach(var (start,end) in new (uint,uint)[]{(0x8003ABC0,0x8003C044),(0x8004E2E8,0x8004F384),(0x8006CBF8,0x8006CD54),(0x8005C000,0x80060000),(0x80057AB0,0x80057B68)})
  {int at=(int)(start-UniversalExeBindings.ExeBase),len=(int)(end-start);Require(native.AsSpan(at,len).SequenceEqual(_exe.AsSpan(at,len)),"retained collision/player/model/reward/renderer consumer changed at"+start.ToString("X8"));}
 }
 internal void RequireDestination(UniversalOverlay destination)
 {
  UniversalGreenDruidWorldInstallation.RequireDestination(destination,null);
  // Whole exact Town particle callbacks/tables are authenticated by RequireDestination.
  // Thus retained11/72 runs once; the copied function supplies only5+8.
  var p=UniversalDarkPassageParticleHosts.Resolve(destination);Require(p.Key=="townsquare"&&destination.Word(p.SpawnTable+5*4)==0x80089E74&&destination.Word(p.UpdateTable+5*4)==0x80088F20,"exact Town no-op5 continuation");
 }
 internal void RequireInitializer(UniversalAsmPart part,UniversalOverlay source,IReadOnlyList<int>classes)
 {
  Require(_map!=null&&source.Bytes.SequenceEqual(_overlay.Bytes)&&source.Bindings.SequenceEqual(_overlay.Bindings)&&classes.Count>0&&classes.All(Classes.Contains),"issued constructor source/namespace owner");string label=part.Items.First().Label??throw new InvalidDataException("Missing constructor label.");var expected=UniversalBehaviorBundleBuilder.BuildSpecializedInit(_overlay,classes,label).Part;RequireSamePart(part,expected,"complete constructor, allocation, physical parent byte, RNG, native model/frame inputs");
 }
 private static void RequireSamePart(UniversalAsmPart a,UniversalAsmPart b,string why)=>Require(a.LenientTables==b.LenientTables&&a.Items.SequenceEqual(b.Items)&&a.Tables.Count==b.Tables.Count&&b.Tables.All(p=>a.Tables.TryGetValue(p.Key,out var t)&&t.SequenceEqual(p.Value)),why);
}

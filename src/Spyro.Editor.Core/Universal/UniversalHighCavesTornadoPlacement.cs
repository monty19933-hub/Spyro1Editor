using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Original High Caves tornado/druid groups and their mutable world paths.
/// The druid-to-environment group through row15 is deliberately not admitted.</summary>
public static class UniversalHighCavesTornadoPlacement
{
 public const string Key="highcaves";
 internal const string DataSha="5f8f3189f0695e8710f71cc4056fc5dd677758c0cf26fd291953a650a6ada16c";
 internal const string OverlaySha="6c153366d9b3dd6d25a7aa8e8d1ea6708bdf97b04ee35912b11df0f8b9913140";
 public static bool Applies(UniversalRowTemplate t)=>string.Equals(t.SourceLevelKey,Key,StringComparison.OrdinalIgnoreCase)&&(t.ClassId is 0x12D or 0x12E or 0x12F||t.SourceRowIndex is >=0 and <=6 or 15 or 16 or 18 or 19);
 private static string Sha(ReadOnlySpan<byte>b)=>Convert.ToHexStringLower(SHA256.HashData(b));
 public static void RequireSource(UniversalLevelData data,UniversalOverlay overlay)
 {
  if(Sha(data.Bytes)!=DataSha)throw new InvalidDataException("High Caves placement lost its original data owner.");RequireOverlay(overlay);
 }
 private static void RequireOverlay(UniversalOverlay o)
 {
  byte[] bindings=new byte[o.Bindings.Count*16];int at=0;foreach(var b in o.Bindings.OrderBy(x=>x.Key).Select(x=>x.Value))foreach(uint v in new[]{b.Global,b.LuiAddress,b.AddiuAddress,b.Value}){BinaryPrimitives.WriteUInt32LittleEndian(bindings.AsSpan(at),v);at+=4;}
  if(o.Bindings.Any(x=>x.Key!=x.Value.Global)||Sha(bindings)!="0719106e3771779b366e785551f692c4e998e1aae61ec853aaca13ab3a7908eb"||Sha(o.Bytes)!=OverlaySha||o.Update!=0x8007B64C||o.Init!=0x8008883C||o.CopyBuffer!=0x8008C73C)
   throw new InvalidDataException("High Caves placement lost its source callback owner.");
 }
 private static void Original(UniversalLevelData d,UniversalRowTemplate t)
 {
  if(t.SourceRowIndex<0||t.SourceRowIndex>=d.Scene().RowCount)throw new InvalidDataException("High Caves source row is outside its original owner.");
  var b=d.CaptureRow(Key,t.SourceRowIndex);
  if(!t.Row.SequenceEqual(b.Row)||!t.Properties.SequenceEqual(b.Properties)||!t.InternalPointers.SequenceEqual(b.InternalPointers)||!t.ExternalRowPointers.SequenceEqual(b.ExternalRowPointers)||t.DroppedExternalPointers!=b.DroppedExternalPointers)
   throw new InvalidDataException("High Caves original row, property or path owner changed.");
 }
 private static int I(byte[]b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
 private static int[] Members(int row)=>row switch{0 or 1 or 2=>[0,1,2],3 or 4=>[3,4],_=>throw new InvalidDataException("This High Caves object needs its original moving terrain and linked scenery. Placement is not ready for this variant yet.")};
 public static UniversalRowTemplate Capture(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate t)
 {
  if(!Applies(t))return t;RequireSource(data,overlay);Original(data,t);var members=Members(t.SourceRowIndex);
  int offset=t.ClassId==0x12F?16:0;int target=I(t.Properties,offset),expected=t.ClassId==0x12F?0x12D:0x12F;
  if(!members.Contains(target)||BinaryPrimitives.ReadUInt16LittleEndian(data.Row(target).AsSpan(0x36))!=expected||t.InternalPointers.Any(p=>p.Offset==offset)||t.ExternalRowPointers.Any(p=>p.Offset==offset))
   throw new InvalidDataException("High Caves target ordinal lost its original class/physical owner.");
  var prerequisites=members.Where(r=>r!=t.SourceRowIndex&&r!=target).Select(r=>new UniversalSourcePrerequisite(r,BinaryPrimitives.ReadUInt16LittleEndian(data.Row(r).AsSpan(0x36)))).ToArray();
  return t with{SourceRowIndices=[new(offset,target,expected)],ImportedRowIndices=[],SourcePrerequisiteRows=prerequisites};
 }
 public static void RequirePrerequisites(UniversalRowTemplate t,IReadOnlyList<UniversalRowTemplate> active)
 {
  if(!Applies(t))return;var members=Members(t.SourceRowIndex);int offset=t.ClassId==0x12F?16:0,target=I(t.Properties,offset),targetClass=t.ClassId==0x12F?0x12D:0x12F;
  if(!t.SourceRowIndices.SequenceEqual(new[]{new UniversalSourceRowIndex(offset,target,targetClass)}))throw new InvalidDataException("High Caves target index enrollment changed.");
  var expected=members.Where(r=>r!=t.SourceRowIndex&&r!=target).Select(r=>new UniversalSourcePrerequisite(r,r is 2 or 4?0x12F:0x12D)).ToArray();
  if(!t.SourcePrerequisiteRows.SequenceEqual(expected))throw new InvalidDataException("High Caves group prerequisite ownership changed.");
  foreach(int member in members){var matches=active.Where(a=>string.Equals(a.SourceLevelKey,Key,StringComparison.OrdinalIgnoreCase)&&a.SourceRowIndex==member&&(a.LinkGroupId??"")==(t.LinkGroupId??"")).ToArray();if(matches.Length!=1||matches[0].ClassId!=(member is 2 or 4?0x12F:0x12D))throw new InvalidDataException("This High Caves object needs all of its linked creatures together.");}
 }
 public static UniversalRowTemplate Rebase(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate t,ReadOnlySpan<byte> row)
 {
  if(!Applies(t))return t;RequireSource(data,overlay);Original(data,t);Members(t.SourceRowIndex);RequireInitialRow(t,row);byte[]p=t.Properties.ToArray();
  if(t.ClassId==0x12D){if(p.Length!=28||t.InternalPointers.Count!=0||t.ExternalRowPointers.Count!=0)throw new InvalidDataException("Druid scalar target/state layout changed.");return t;}
  if(t.ClassId!=0x12F||t.InternalPointers.Count!=1||t.InternalPointers[0]!=(0,20)||t.ExternalRowPointers.Count!=0||p.Length!=28+16*p[20]||p[20]!=(t.SourceRowIndex==2?7:6)||!p.AsSpan(21,7).SequenceEqual(new byte[]{0,0,0,0,0,255,255}))throw new InvalidDataException("Tornado mutable path node/header owner changed.");
  for(int node=0;node<p[20];node++)for(int axis=0;axis<3;axis++){int at=28+16*node+4*axis;int delta=checked(I(row.ToArray(),12+4*axis)-I(t.Row,12+4*axis));BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(at),checked(I(p,at)+delta));}
  return t with{Properties=p};
 }
 public static void RequireInitialRow(UniversalRowTemplate t,ReadOnlySpan<byte>r)
 {
  if(r.Length!=88)throw new InvalidDataException("High Caves actor row is truncated.");
  for(int i=4;i<88;i++)if(i is not(>=12 and <24)&&i is not(0x43 or 0x46 or 0x4A)&&r[i]!=t.Row[i])throw new InvalidDataException("This High Caves object needs its original initial state, animation and collision settings.");
  if(r[0x43]!=255||r[0x4A]!=255)throw new InvalidDataException("Imported High Caves actors require native unrestricted activation and visibility.");
 }
 public static void RequireGroups(IReadOnlyList<(byte[]Row,UniversalRowTemplate Template)> rows)
 {
  var templates=rows.Select(x=>x.Template).ToArray();foreach(var (row,t) in rows.Where(x=>Applies(x.Template))){RequirePrerequisites(t,templates);var group=rows.Where(x=>Applies(x.Template)&&(x.Template.LinkGroupId??"")==(t.LinkGroupId??"")&&Members(t.SourceRowIndex).Contains(x.Template.SourceRowIndex)).ToArray();for(int axis=0;axis<3;axis++){int delta=checked(I(row,12+4*axis)-I(t.Row,12+4*axis));if(group.Any(x=>checked(I(x.Row,12+4*axis)-I(x.Template.Row,12+4*axis))!=delta))throw new InvalidDataException("Move the linked High Caves group together so its native path and creature targets remain aligned.");}}
 }
 public static void RequireDestination(UniversalLevelData sourceData,UniversalOverlay sourceOverlay,UniversalLevelData destination,IReadOnlyList<(byte[]Row,UniversalRowTemplate Template)> rows,IReadOnlyList<int> physicalRows)
 {
  if(physicalRows.Count!=rows.Count||physicalRows.Distinct().Count()!=physicalRows.Count||physicalRows.Any(r=>r<0||r>=destination.Scene().RowCount))throw new InvalidDataException("High Caves final rows lack unique physical owners.");
  for(int i=0;i<rows.Count;i++){
   var (row,t)=rows[i];if(!Applies(t))continue;RequireSource(sourceData,sourceOverlay);var original=Capture(sourceData,sourceOverlay,sourceData.CaptureRow(Key,t.SourceRowIndex));
   if(!t.Row.SequenceEqual(original.Row)||!t.SourceRowIndices.SequenceEqual(original.SourceRowIndices)||!t.SourcePrerequisiteRows.SequenceEqual(original.SourcePrerequisiteRows)||!t.InternalPointers.SequenceEqual(original.InternalPointers)||!t.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers))throw new InvalidDataException("High Caves final row lost its original enrollment.");
   var authored=row.ToArray();authored[0x36]=original.Row[0x36];authored[0x37]=original.Row[0x37];var translated=Rebase(sourceData,sourceOverlay,original,authored);
   if(!t.Properties.SequenceEqual(translated.Properties))throw new InvalidDataException("High Caves final path differs from its authenticated source translation.");
   var resolved=UniversalIndexedRows.Resolve(original with{LinkGroupId=t.LinkGroupId},rows.Select(x=>x.Template).ToArray());if(!resolved.ImportedRowIndices.SequenceEqual(t.ImportedRowIndices))throw new InvalidDataException("High Caves final ordinal resolves to another source identity.");
   var actual=destination.CaptureRow("final",physicalRows[i]);var expected=t.Properties.ToArray();
   foreach(var index in t.ImportedRowIndices){if(index.PlacementIndex<0||index.PlacementIndex>=rows.Count)throw new InvalidDataException("High Caves final target escaped allocation.");BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(index.Offset),physicalRows[index.PlacementIndex]);}
   var observed=actual.Properties.ToArray();foreach(var ptr in t.InternalPointers)expected.AsSpan(ptr.Offset,4).Clear();foreach(var ptr in actual.InternalPointers)observed.AsSpan(ptr.Offset,4).Clear();
   if(!actual.Row.AsSpan(4).SequenceEqual(row.AsSpan(4))||!observed.SequenceEqual(expected)||!actual.InternalPointers.SequenceEqual(t.InternalPointers)||actual.ExternalRowPointers.Count!=0||actual.DroppedExternalPointers!=0||t.ImportedRowIndices.Count!=1)
    throw new InvalidDataException("High Caves final row, target ordinal or translated mutable path changed.");
  }
 }
 public static void RequireNativeRemoval(string key,UniversalLevelData current,UniversalLevelData original,UniversalOverlay overlay,IReadOnlySet<int>removed)
 {
  if(removed.Count==0||!string.Equals(key,Key,StringComparison.OrdinalIgnoreCase))return;RequireSource(original,overlay);
  for(int i=0;i<original.Scene().RowCount;i++){var t=original.CaptureRow(Key,i);if(!Applies(t)||removed.Contains(i))continue;int offset=t.ClassId==0x12F?16:t.ClassId==0x12D?0:12;int target=I(t.Properties,offset);var now=current.CaptureRow(Key,i);if(now.ClassId!=t.ClassId||now.Properties.Length!=t.Properties.Length||I(now.Properties,offset)!=target)throw new InvalidDataException("A surviving High Caves actor changed or hid its original linked target.");if(target>=0&&removed.Contains(target))throw new InvalidDataException("Remove the referencing High Caves creature together with its required target.");}
 }
 internal static void RequireCopied(UniversalAsmPart part,string key,UniversalOverlay source,IReadOnlyList<int>classes,IReadOnlyDictionary<int,int>map,byte[]exe)
 {
  bool family=classes.Any(c=>c is 0x12D or 0x12E or 0x12F or 0x127 or 0x130);
  if(!family)return;bool owner=Sha(source.Bytes)==OverlaySha||source.Update==0x8007B64C&&source.Init==0x8008883C&&source.CopyBuffer==0x8008C73C;
  if(!string.Equals(key,Key,StringComparison.OrdinalIgnoreCase)){if(owner)throw new InvalidDataException("High Caves behavior was relabeled.");return;}RequireOverlay(source);RequireNative(exe);if(classes.Contains(0x12E))throw new InvalidDataException("Copied High Caves moving terrain has no imported environment animation owner.");
  if(classes.Contains(0x12F)&&(!classes.Contains(0x12D)||!classes.Contains(0x130))||classes.Contains(0x12D)&&(!classes.Contains(0x12F)||!classes.Contains(0x127)||!classes.Contains(0x13E)))throw new InvalidDataException("Copied High Caves group needs both root classes and their complete child dependencies.");
  string tag=part.Items.FirstOrDefault()?.Label??throw new InvalidDataException("Missing High Caves update entry.");var expected=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,classes,tag,map).Part;
  if(!part.Items.SequenceEqual(expected.Items)||part.Tables.Count!=expected.Tables.Count||expected.Tables.Any(t=>!part.Tables.TryGetValue(t.Key,out var a)||!a.SequenceEqual(t.Value)))throw new InvalidDataException("High Caves copied target/path producer or consumer flow changed.");
  // Druid death/reset jumps to its shared model-table tail. This is the current
  // allocated actor's destination class, not a comparison to source literals.
  // The full emitted extraction above owns all entries/operands/delay slots.
  // 84D48..58 establishes gModels; 87B9C..BC indexes current class *4,
  // loads its animation0 pointer and frame count, then 87BC0 kills V0.
  if(classes.Contains(0x12D)){
   if(source.Word(0x80084D48)!=0x86620036||source.Word(0x80084D4C)!=0x3C0A8007||source.Word(0x80084D50)!=0x254A6378||source.Word(0x80084D54)!=0x08021EE7||source.Word(0x80084D58)!=0xAE600018||source.Word(0x80087BA4)!=0x00021080||source.Word(0x80087BA8)!=0x004A1021||source.Word(0x80087BAC)!=0x8C420000||source.Word(0x80087BB4)!=0x8C420038||source.Word(0x80087BBC)!=0x9043000C||source.Word(0x80087BC0)!=0x24020001)throw new InvalidDataException("High Caves current-model tail owner changed.");
   int at=part.Items.FindIndex(x=>x.Orig==0x80084D48);if(at<0||part.Items.Count(x=>x.Orig==0x80084D48)!=1)throw new InvalidDataException("High Caves current model read is missing or duplicated.");part.Items[at]=part.Items[at] with{ClassReadOwned=true};
  }
 }
 internal static void RequireInitializer(UniversalAsmPart part,UniversalOverlay source,IReadOnlyList<int>classes,byte[]exe)
 {
  if(!classes.Any(c=>c is 0x12D or 0x12F or 0x127 or 0x130 or 0x13E))return;RequireOverlay(source);RequireNative(exe);string tag=part.Items.FirstOrDefault()?.Label??throw new InvalidDataException("Missing High Caves initializer.");var expected=UniversalBehaviorBundleBuilder.BuildSpecializedInit(source,classes,tag).Part;
  if(!part.Items.SequenceEqual(expected.Items)||part.Tables.Count!=expected.Tables.Count||expected.Tables.Any(t=>!part.Tables.TryGetValue(t.Key,out var a)||!a.SequenceEqual(t.Value)))throw new InvalidDataException("High Caves copied child/parent/path constructor changed.");
 }
 internal static void RequireNative(byte[]exe)
 {
  int at=(int)(0x80037F90-UniversalExeBindings.ExeBase);if(exe.Length<at+10252||Sha(exe.AsSpan(at,10252))!="2b99d1663f1c06a121a49ed3083cb5576e312ca8ce1fa637117bbc4133464d57")throw new InvalidDataException("High Caves path/timer/native helper owner changed.");
  int vectors=(int)(0x800176C8-UniversalExeBindings.ExeBase);if(exe.Length<vectors+1208||Sha(exe.AsSpan(vectors,1208))!="205cd04d3d922588f989f7c3759a0dd85825ade9d020cb17fc778c8c72ac57e9")throw new InvalidDataException("High Caves native vector/distance consumer changed.");
 }
}

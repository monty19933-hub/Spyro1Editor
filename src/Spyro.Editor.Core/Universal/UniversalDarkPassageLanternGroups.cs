using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Issued original Dark Passage lantern actor context. Foreign copies use
/// the native negative environment ordinal, retaining actor glow and linked changes.</summary>
public sealed class UniversalDarkPassageLanternContext
{
 internal UniversalDarkPassageLanternContext(UniversalLevelData source,UniversalOverlay overlay,UniversalOverlay destination,bool terrain)
 {Source=new(source.Bytes.ToArray());Overlay=overlay;DestinationHash=Fingerprint(destination);UsesSourceTerrainLighting=terrain;}
 internal UniversalLevelData Source {get;}
 internal UniversalOverlay Overlay {get;}
 private string DestinationHash {get;}
 internal void RequireDestination(UniversalOverlay destination,byte[] exe)
 { if(Fingerprint(destination)!=DestinationHash)throw new InvalidDataException("The lantern context belongs to a different destination overlay or callback owner.");UniversalDarkPassageLanternGroups.RequireNativeOwners(exe); }
 internal void RequireDestinationData(UniversalLevelData destination)
 { if(UsesSourceTerrainLighting&&!UniversalDarkPassageLanternGroups.HasSourceTerrain(destination,Source))throw new InvalidDataException("The original lantern terrain lighting changed after context issuance."); }
 private static string Fingerprint(UniversalOverlay overlay)=>Convert.ToHexStringLower(SHA256.HashData(overlay.Bytes))+string.Join(";",overlay.Bindings.OrderBy(b=>b.Key).Select(b=>$"{b.Key:X8}:{b.Value.Global:X8}:{b.Value.LuiAddress:X8}:{b.Value.AddiuAddress:X8}:{b.Value.Value:X8}"));
 public bool UsesSourceTerrainLighting {get;}
 public string BehaviorNote=>UsesSourceTerrainLighting?"Uses the original Dark Passage terrain lighting.":"Lights the lantern and changes linked creatures; terrain lighting is unchanged.";
 internal void RequireCopied(UniversalAsmPart part,UniversalOverlay source,IReadOnlyList<int> classes,IReadOnlyDictionary<int,int> map)
 {
  UniversalDarkPassageLanternGroups.ProveSource(Source,source);
  if(!classes.Contains(0x7A)||!classes.Contains(0x7C)||!map.ContainsKey(0x7A)||!map.ContainsKey(0x7C)||map[0x7A]==map[0x7C])
   throw new InvalidDataException("Copied lantern behavior needs its original child animation owner.");
  string tag=part.Items.FirstOrDefault()?.Label??throw new InvalidDataException("Missing lantern entry.");
  var expected=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,classes,tag,map).Part;
  if(!part.Items.SequenceEqual(expected.Items)||part.Tables.Count!=expected.Tables.Count||expected.Tables.Any(t=>!part.Tables.TryGetValue(t.Key,out var a)||!a.SequenceEqual(t.Value)))
   throw new InvalidDataException("Copied lantern behavior changed its source branches, entries, delays or consumers.");
 }
 internal void RequireCopiedInit(UniversalAsmPart part,UniversalOverlay source,IReadOnlyList<int> classes)
 {
  UniversalDarkPassageLanternGroups.ProveSource(Source,source);string tag=part.Items.FirstOrDefault()?.Label??throw new InvalidDataException("Missing lantern initializer.");
  var expected=UniversalBehaviorBundleBuilder.BuildSpecializedInit(source,classes,tag).Part;
  if(!part.Items.SequenceEqual(expected.Items)||part.Tables.Count!=expected.Tables.Count||expected.Tables.Any(t=>!part.Tables.TryGetValue(t.Key,out var a)||!a.SequenceEqual(t.Value)))throw new InvalidDataException("Lantern initialization changed its original properties or parent ownership.");
 }
 internal UniversalRowTemplate Prepare(UniversalRowTemplate t,byte[] row)
 {
  if(!UniversalDarkPassageLanternGroups.Applies(t))return t;
  UniversalDarkPassageLanternGroups.RequireOriginal(Source,Overlay,t);
  var original=Source.CaptureRow(t.SourceLevelKey,t.SourceRowIndex);
  for(int i=4;i<0x58;i++)if(i is not(>=12 and <24)&&i is not(0x43 or 0x46 or 0x4A)&&row[i]!=original.Row[i])
   throw new InvalidDataException("The lantern group needs its original initial animation, light and collision inputs.");
  byte[] properties=t.Properties.ToArray();
  if(t.ClassId==0x7A&&!UsesSourceTerrainLighting)BinaryPrimitives.WriteInt32LittleEndian(properties.AsSpan(0x18),-1);
  return t with{Properties=properties,DarkLanternContext=this};
 }
 internal void RequireGroups(IReadOnlyList<(byte[] Row,UniversalRowTemplate Template)> rows)
 {
  foreach(var (row,t) in rows.Where(p=>p.Template.DarkLanternContext==this))
  {
   var original=Source.CaptureRow(t.SourceLevelKey,t.SourceRowIndex);
   byte[] expected=original.Properties.ToArray();if(t.ClassId==0x7A&&!UsesSourceTerrainLighting)BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(0x18),-1);
   if(!t.Properties.SequenceEqual(expected))throw new InvalidDataException("The imported lantern context changed its certified properties.");
   var owned=UniversalDarkPassageLanternGroups.Capture(Source,Overlay,original);
   if(!t.SourceRowIndices.SequenceEqual(owned.SourceRowIndices)||!t.SourcePrerequisiteRows.SequenceEqual(owned.SourcePrerequisiteRows))
    throw new InvalidDataException("The imported lantern group changed its exact indexed or reverse owners.");
   int[] delta=Enumerable.Range(0,3).Select(a=>checked(I(row,12+4*a)-I(original.Row,12+4*a))).ToArray();
   foreach(int sourceRow in UniversalDarkPassageLanternGroups.Members(Source,t))
   {
    var matches=rows.Where(p=>string.Equals(p.Template.SourceLevelKey,t.SourceLevelKey,StringComparison.OrdinalIgnoreCase)&&p.Template.SourceRowIndex==sourceRow&&SameGroup(t,p.Template)).ToArray();
    if(matches.Length!=1||matches[0].Template.DarkLanternContext!=this)throw new InvalidDataException("Every linked lantern and creature must remain once in its own group.");
    var target=Source.CaptureRow(t.SourceLevelKey,sourceRow);
    if(!Enumerable.Range(0,3).All(a=>checked(I(matches[0].Row,12+4*a)-I(target.Row,12+4*a))==delta[a]))
     throw new InvalidDataException("Move the linked lantern and creatures together so their native relative positions stay intact.");
   }
  }
 }
 internal static bool SameGroup(UniversalRowTemplate a,UniversalRowTemplate b)=>string.Equals(a.LinkGroupId??"",b.LinkGroupId??"",StringComparison.Ordinal);
 private static int I(byte[] b,int o)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(o));
}

internal static class UniversalDarkPassageLanternGroups
{
 private const string Key="darkpassage";
 private static bool IsKey(string key)=>string.Equals(key,Key,StringComparison.OrdinalIgnoreCase);
 internal static bool Applies(UniversalRowTemplate t)=>IsKey(t.SourceLevelKey)&&(t.ClassId==0x7A||t.ClassId==0x35&&t.Properties.Length>=16&&I(t.Properties,12)>=0);
 internal static void ProveSource(UniversalLevelData data,UniversalOverlay overlay)
 {
  UniversalDarkPassageShapeshifterOwnership.RequireSource(overlay);
  if(Sha(data.Bytes)!=UniversalDarkPassageShapeshifterOwnership.DataSha)throw new InvalidDataException("Lantern groups need the original Dark Passage data owner.");
  var sw=UniversalDispatchAnalysis.FindUpdateSwitch(overlay);var trace=UniversalDispatchAnalysis.Trace(overlay,sw.TreeStart,0x7A,sw.Seeds,sw.ClassRegister);
  var update=UniversalDispatchAnalysis.Closure(overlay,sw.Function,[trace.Stop],new HashSet<uint>{sw.TreeStart}).Instructions;
  uint[] expectedReads=[0x800808E4,0x8008092C,0x8008097C,0x800809A4,0x80080AB0,0x80080BA4];
  var touches=update.Where(pc=>(UniversalMips.IsLoad(overlay.Word(pc))||UniversalMips.IsStore(overlay.Word(pc)))&&UniversalMips.Rs(overlay.Word(pc))==20&&UniversalMips.SImm(overlay.Word(pc))==0x18).ToArray();
  if(!touches.SequenceEqual(expectedReads)||touches.Any(pc=>overlay.Word(pc)!=0x8E840018))throw new InvalidDataException("Lantern environment ordinal gained an unowned read or writer.");
  foreach(var (pc,target) in new[]{(0x800808ECu,0x800809D4u),(0x80080984u,0x800809B0u),(0x80080AB8u,0x80080AC8u),(0x80080BACu,0x80080BBCu)})
   if(!update.Contains(pc)||UniversalMips.Op(overlay.Word(pc))!=1||UniversalMips.Rs(overlay.Word(pc))!=4||UniversalMips.Rt(overlay.Word(pc))!=0||UniversalMips.BranchTarget(overlay.Word(pc),pc)!=target)
    throw new InvalidDataException("Lantern negative environment guard changed.");
  var init=UniversalSpecializer.Specialize(overlay,overlay.Init,4,[0x7A]).Instructions;
  if(init.Any(pc=>UniversalMips.IsStore(overlay.Word(pc))&&UniversalMips.Rs(overlay.Word(pc))==20&&UniversalMips.SImm(overlay.Word(pc))==0x18))
   throw new InvalidDataException("Lantern initialization changed its ordinal producer.");
 }
 internal static void RequireOriginal(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate t)
 {
  ProveSource(data,overlay);var original=data.CaptureRow(Key,t.SourceRowIndex);
  if(!Applies(original)||!t.Row.SequenceEqual(original.Row)||!t.Properties.SequenceEqual(original.Properties)||!t.InternalPointers.SequenceEqual(original.InternalPointers)||!t.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers)||t.DroppedExternalPointers!=original.DroppedExternalPointers)
   throw new InvalidDataException("The lantern group differs from its original row and private properties.");
  if(t.ClassId==0x7A&&(t.Properties.Length!=28||t.InternalPointers.Count!=0||t.ExternalRowPointers.Count!=0||I(t.Properties,0x18) is <0 or >8))
   throw new InvalidDataException("The lantern has no original scalar environment ordinal.");
 }
 internal static UniversalRowTemplate Capture(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate t)
 {
  RequireLabel(t.SourceLevelKey,overlay,[t.ClassId]);if(!Applies(t))return t;RequireOriginal(data,overlay,t);
  if(t.ClassId==0x35)
  {
   int target=I(t.Properties,12);if(target>=data.Scene().RowCount||BinaryPrimitives.ReadUInt16LittleEndian(data.Row(target).AsSpan(0x36))!=0x7A||t.InternalPointers.Any(p=>p.Offset==12)||t.ExternalRowPointers.Any(p=>p.Offset==12))throw new InvalidDataException("The creature lost its native lantern index owner.");
   return t with{SourceRowIndices=[new(12,target,0x7A)],ImportedRowIndices=[]};
  }
  return t with{SourcePrerequisiteRows=Dogs(data,t.SourceRowIndex).Select(r=>new UniversalSourcePrerequisite(r,0x35)).ToArray()};
 }
 private static int[] Dogs(UniversalLevelData data,int lamp)=>Enumerable.Range(0,data.Scene().RowCount).Where(r=>BinaryPrimitives.ReadUInt16LittleEndian(data.Row(r).AsSpan(0x36))==0x35&&I(data.CaptureRow(Key,r).Properties,12)==lamp).ToArray();
 internal static int[] Members(UniversalLevelData data,UniversalRowTemplate t){int lamp=t.ClassId==0x7A?t.SourceRowIndex:I(data.CaptureRow(Key,t.SourceRowIndex).Properties,12);return [lamp,..Dogs(data,lamp)];}
 internal static void RequirePrerequisites(UniversalRowTemplate t,IReadOnlyList<UniversalRowTemplate> active)
 {
  if(!IsKey(t.SourceLevelKey)||t.ClassId!=0x7A)throw new InvalidDataException("Unowned lantern reverse prerequisites.");
  // Source identity was proved on capture; exact original group memberships are fixed below.
  int[] expected=t.SourceRowIndex switch{19=>[26],20=>[27],23=>[29],25=>[30],37=>[38],68=>[66,67],21 or 22 or 24=>[],_=>throw new InvalidDataException("Unowned original lantern row.")};
  if(!t.SourcePrerequisiteRows.SequenceEqual(expected.Select(r=>new UniversalSourcePrerequisite(r,0x35))))throw new InvalidDataException("The lantern lost its complete source-owned creature group.");
  foreach(int r in expected){var matches=active.Where(a=>string.Equals(a.SourceLevelKey,t.SourceLevelKey,StringComparison.OrdinalIgnoreCase)&&a.SourceRowIndex==r&&UniversalDarkPassageLanternContext.SameGroup(t,a)).ToArray();if(matches.Length!=1||matches[0].ClassId!=0x35)throw new InvalidDataException("The lantern needs each linked creature once in the same group.");}
 }
 internal static UniversalDarkPassageLanternContext? Issue(string destinationKey,UniversalLevelData destination,UniversalOverlay destinationOverlay,UniversalLevelData source,UniversalOverlay overlay,IReadOnlyList<UniversalMobyPlacement> placements,byte[] exe)
 {
  if(!placements.Any(p=>!p.Retired&&p.SpecialId==null&&IsKey(p.SourceLevelKey)&&Applies(source.CaptureRow(Key,p.SourceRowIndex))))return null;
  ProveSource(source,overlay);RequireNativeOwners(exe);
  bool native=IsKey(destinationKey)&&HasSourceTerrain(destination,source);return new(source,overlay,destinationOverlay,native);
 }
 internal static void RequireNativeOwners(byte[] exe)
 {
RequireNative(exe,0x8002B390,180,"446105fc3a8670a6b726943b4bf6bb94add06e9d86d89d1c8bb130bbaf44743d");RequireNative(exe,0x80058AE8,232,"acc98087e5829f4d6c4773a14ffc22a144f196808ab9f2a46e148a466af85dba");
  RequireNative(exe,0x8003A720,124,"e2d690c6097745e4fb354355436805a619c7f31b9d96fa088b90c0518bc6b056");RequireNative(exe,0x800526A8,804,"5483d5e337730567fd89f6118036d31f8c3a93fd281ff444b5d8149b4e2f03d5");RequireNative(exe,0x80017700,28,"c8e9d6ff8966aaa5fbc6e7ecbab1f9d4b379d15f22e3091881361a770fd433b7");RequireNative(exe,0x80017758,52,"a80b10978f8b9cf51fdb19dd9d67b776c3285c209aaf023ccfbccf71913d7398");RequireNative(exe,0x800176C8,40,"16b15a8106693cc86ec0ec04ccb9701a1c110ca55ac47bf8347c96697b572fa6");
 }
 internal static bool HasSourceTerrain(UniversalLevelData destination,UniversalLevelData source)
 {
  var a=source.Subfiles()[1];var b=destination.Subfiles()[1];int ca=a.Offset,cb=b.Offset;
  for(int i=0;i<6;i++){int la=source.U32(ca),lb=destination.U32(cb);if(la!=lb||!source.Bytes.AsSpan(ca,la).SequenceEqual(destination.Bytes.AsSpan(cb,lb)))return false;ca+=la;cb+=lb;}
  int sa=source.Subfiles()[3].Offset,da=destination.Subfiles()[3].Offset;int length=source.Scene().MobyComponent;
  return length==destination.Scene().MobyComponent&&source.Bytes.AsSpan(sa,length).SequenceEqual(destination.Bytes.AsSpan(da,length));
 }
 internal static void RequireNativeRemoval(string key,UniversalLevelData destination,UniversalLevelData original,UniversalOverlay overlay,IReadOnlySet<int> removed)
 {
  if(!IsKey(key)||removed.Count==0)return;ProveSource(original,overlay);
  for(int r=0;r<original.Scene().RowCount;r++)if(!removed.Contains(r)&&BinaryPrimitives.ReadUInt16LittleEndian(original.Row(r).AsSpan(0x36))==0x7A){var current=destination.CaptureRow(Key,r);var source=original.CaptureRow(Key,r);if(current.ClassId!=0x7A||current.Properties.Length<28||I(current.Properties,24)!=I(source.Properties,24))throw new InvalidDataException("A native lantern changed its original terrain-light owner.");}
 }
 internal static void RequireCopied(UniversalAsmPart part,string key,UniversalOverlay source,IReadOnlyList<int> classes,IReadOnlyDictionary<int,int> map,IReadOnlyDictionary<string,UniversalDarkPassageLanternContext>? contexts)
 {
  RequireLabel(key,source,classes);if(!IsKey(key)||!classes.Contains(0x7A))return;
  if(contexts==null||!contexts.TryGetValue(key,out var context))throw new InvalidDataException("This lantern needs its linked creatures and lighting context before it can be copied.");
  context.RequireCopied(part,source,classes,map);
 }
 private static void RequireLabel(string key,UniversalOverlay overlay,IReadOnlyList<int> classes)
 { if(!IsKey(key)&&classes.Contains(0x7A)&&(Sha(overlay.Bytes)==UniversalDarkPassageShapeshifterOwnership.OverlaySha||overlay.Update==0x8007B4F8&&overlay.Init==0x80084B94&&overlay.CopyBuffer==0x800880D4))throw new InvalidDataException("The original lantern source was mislabeled."); }
 private static void RequireNative(byte[] exe,uint pc,int length,string sha){int at=checked((int)(pc-UniversalExeBindings.ExeBase));if(at<0||at+length>exe.Length||Sha(exe.AsSpan(at,length))!=sha)throw new InvalidDataException("Lantern context lost a native environment or glow owner.");}
 private static int I(byte[] b,int o)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(o));
 private static string Sha(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}

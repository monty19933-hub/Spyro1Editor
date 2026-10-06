using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Exact original Dark Passage dog35/36 transitions and lantern selection.
/// Foreign placement is limited to original -1 variants; lantern environment banks
/// are destination context and have not been imported by this adapter.</summary>
internal static class UniversalDarkPassageShapeshifterOwnership
{
 internal const string Key="darkpassage";
 internal const string DataSha="217d4e525f10b63f4ae4a413d807989241b7f2f91963b543b421cbd3954c364a";
 internal const string OverlaySha="a8b99270c37a37363824cf1fd2081c092968abef62df9e74d70ae0017b984727";
 private const string BindingSha="55a205e94483da3ff00ec094e190790a37b855f779077b7422f3ff4db87a304d";
 private static bool IsSource(string key)=>string.Equals(key,Key,StringComparison.OrdinalIgnoreCase);
 private static bool IsDog(int c)=>c is 0x35 or 0x36;
 internal static bool Applies(UniversalRowTemplate t)=>IsSource(t.SourceLevelKey)&&IsDog(t.ClassId);
 internal static void RequireSource(UniversalOverlay source)
 {
  byte[] bindings=new byte[source.Bindings.Count*16];int n=0;
  foreach(var b in source.Bindings.OrderBy(x=>x.Key).Select(x=>x.Value))foreach(uint v in new[]{b.Global,b.LuiAddress,b.AddiuAddress,b.Value}){BinaryPrimitives.WriteUInt32LittleEndian(bindings.AsSpan(n),v);n+=4;}
  if(source.Bindings.Any(p=>p.Key!=p.Value.Global)||Sha(source.Bytes)!=OverlaySha||Sha(bindings)!=BindingSha)
   throw new InvalidDataException("Dark Passage dog transitions lost their original source/callback owners.");
 }
 internal static void AddDependencies(string key,UniversalOverlay source,int c,ISet<int> dependencies)
 {
  RequireLabel(key,source,[c]);if(!IsSource(key)||!IsDog(c))return;RequireSource(source);dependencies.Add(c==0x35?0x36:0x35);
 }
 internal static IReadOnlyList<uint> Patch(UniversalAsmPart part,string key,UniversalOverlay source,IReadOnlyList<int> classes,IReadOnlyDictionary<int,int> map,byte[] exe)
 {
  RequireLabel(key,source,classes);if(!IsSource(key)||!classes.Any(IsDog))return [];
  RequireSource(source);
  if(!classes.Contains(0x35)||!classes.Contains(0x36)||!classes.Contains(0x9B)||!map.ContainsKey(0x35)||!map.ContainsKey(0x36)||!map.ContainsKey(0x9B)||map[0x35]==map[0x36]||map[0x35] is <=0 or >=0x200||map[0x36] is <=0 or >=0x200||map[0x9B] is <=0 or >=0x200||new[]{map[0x35],map[0x36],map[0x9B]}.Distinct().Count()!=3)
   throw new InvalidDataException("Copied Dark Passage dog behavior requires both source-owned transition classes and the child attack class, models and handlers.");
  // An installed save map keeps these routines' native behaviour; prove the original code beneath it.
  exe=Spyro.Editor.Core.Exporting.NativeMobySaveMap.OriginalView(exe);
  RequireNative(exe,0x80051FEC,724,"70dc8c577dc54c205c8891cae9375af93a242f95ef37ab77bf0318a01e319d96");
  RequireNative(exe,0x8003AA84,316,"fd5a362fc588edfa15548b62c3b50fda15fd1f2e58b9dedd7b793082e8fde72d");
  RequireNative(exe,0x80037F90,10128,"5b7e67a79df04ef66ee63fda7d084b73989a87a7886cce5de46db1ad6e93b222");
  RequireNative(exe,0x8003ABC0,1308,"979e567dd4bebbdd9cadaf8f884e1110be16f293c6590ba6214941be5be7a4a8");
  RequireNative(exe,0x8003B7C0,148,"a8378b5d829c8a0661affbb6e50eb64b606c3114e116a38ec1b7137ba8a30ec4");
  // This complete extraction binds current root S3, source35 props S1/source36
  // props S0, signed index gates, physical target substate, both animation-event
  // gates, native model reset tails and all incoming edges/tables/duplicates.
  // Run immediately after extraction, before any other certified adapter.
  string tag=part.Items.FirstOrDefault()?.Label??throw new InvalidDataException("Missing dog entry.");
  var expected=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,classes,tag,map).Part;
  if(!part.Items.SequenceEqual(expected.Items)||part.Tables.Count!=expected.Tables.Count||expected.Tables.Any(t=>!part.Tables.TryGetValue(t.Key,out var a)||!a.SequenceEqual(t.Value)))
   throw new InvalidDataException("Dark Passage dog emitted owner changed operands, flow, delay slots, entries or duplicate consumers.");
  List<uint> stores=[];
  foreach(var (producer,store,c) in new[]{(0x8007DB10u,0x8007DB18u,0x36),(0x8007DD8Cu,0x8007DD94u,0x36),(0x8007DEF8u,0x8007DEFCu,0x35)})
  {
   int p=part.Items.FindIndex(x=>x.Orig==producer),s=part.Items.FindIndex(x=>x.Orig==store);
   if(p<0||s<0||part.Items[p].Word!=UniversalMips.EncodeI(9,0,2,c)||part.Items[s].Word!=0xA6620036||part.Items.Count(x=>x.Orig==producer)!=1||part.Items.Count(x=>x.Orig==store)!=1)
    throw new InvalidDataException("Dark Passage dog transition lost its literal/current-actor store owner.");
   part.Items[p]=part.Items[p] with{Word=UniversalMips.EncodeI(9,0,2,map[c])};
   part.Items[s]=part.Items[s] with{ClassReadOwned=true};stores.Add(store);
  }
  return stores;
 }
 internal static void RequirePlacement(string destinationKey,UniversalLevelData sourceData,UniversalOverlay source,UniversalRowTemplate t,byte[] row,UniversalLevelData? destinationData=null)
 {
  if(!IsSource(t.SourceLevelKey)||!IsDog(t.ClassId))return;
  RequireSource(source);
  if(Sha(sourceData.Bytes)!=DataSha)throw new InvalidDataException("Dark Passage dog placement lost its original data owner.");
  var original=sourceData.CaptureRow(Key,t.SourceRowIndex);
  if(t.ClassId!=0x35||!t.Row.SequenceEqual(original.Row)||!t.Properties.SequenceEqual(original.Properties)||!t.InternalPointers.SequenceEqual(original.InternalPointers)||!t.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers)||t.DroppedExternalPointers!=original.DroppedExternalPointers||t.Properties.Length!=20||t.InternalPointers.Count!=0||t.ExternalRowPointers.Count!=0||t.SourceRowIndices.Count!=0||t.ImportedRowIndices.Count!=0)
   throw new InvalidDataException("Dark Passage dog placement changed its original scalar properties or row identity.");
  for(int i=4;i<0x58;i++)if(i is not(>=12 and <24)&&i is not(0x43 or 0x46 or 0x4A)&&row[i]!=original.Row[i])
   throw new InvalidDataException("Dark Passage dog placement needs its original initial state, animation and collision inputs.");
  int lamp=BinaryPrimitives.ReadInt32LittleEndian(t.Properties.AsSpan(12));
  if(!IsSource(destinationKey)&&lamp!=-1)
   throw new InvalidDataException("This Dark Passage object needs its lantern and environment lighting. Cross-level placement is not ready for this variant yet.");
  if(lamp>=0&&IsSource(destinationKey)){
   var target=sourceData.CaptureRow(Key,lamp);var current=(destinationData??sourceData).CaptureRow(Key,lamp);
   if(target.ClassId!=0x7A||current.ClassId!=0x7A||current.Properties.Length!=target.Properties.Length||BinaryPrimitives.ReadInt32LittleEndian(current.Properties.AsSpan(0x18))!=BinaryPrimitives.ReadInt32LittleEndian(target.Properties.AsSpan(0x18)))
    throw new InvalidDataException("Copied native Dark Passage dog needs its original lantern environment ordinal.");
  }
  if(lamp< -1)throw new InvalidDataException("Dark Passage dog has an unowned negative lantern selector.");
 }
 internal static void RequireNativeRemoval(string destinationKey,UniversalLevelData destination,UniversalLevelData original,UniversalOverlay overlay,IReadOnlySet<int> removed)
 {
  if(!IsSource(destinationKey)||removed.Count==0)return;
  RequireSource(overlay);if(Sha(original.Bytes)!=DataSha)throw new InvalidDataException("Dark Passage native lantern removal lost its source owner.");
  for(int i=0;i<original.Scene().RowCount;i++){
   var t=original.CaptureRow(Key,i);if(!IsDog(t.ClassId)||removed.Contains(i))continue;
   var current=destination.CaptureRow(Key,i);
   if(current.ClassId!=t.ClassId||current.Properties.Length<16||BinaryPrimitives.ReadInt32LittleEndian(current.Properties.AsSpan(12))!=BinaryPrimitives.ReadInt32LittleEndian(t.Properties.AsSpan(12)))
    throw new InvalidDataException("A surviving Dark Passage dog changed its original lantern selector.");
   int lamp=BinaryPrimitives.ReadInt32LittleEndian(t.Properties.AsSpan(12));
   if(lamp>=0&&(removed.Contains(lamp)||BinaryPrimitives.ReadUInt16LittleEndian(destination.Row(lamp).AsSpan(0x36))!=0x7A))
    throw new InvalidDataException("Remove the dogs linked to this Dark Passage lantern before removing or replacing their lantern.");
  }
 }
 private static void RequireLabel(string key,UniversalOverlay source,IReadOnlyList<int> classes){
  bool callbackOwner=source.Update==0x8007B4F8&&source.Init==0x80084B94&&source.CopyBuffer==0x800880D4;
  bool storeOwner=new uint[]{0x8007DB18,0x8007DD94,0x8007DEFC}.All(pc=>source.Contains(pc)&&source.Word(pc)==0xA6620036);
  if(!IsSource(key)&&classes.Any(IsDog)&&(Sha(source.Bytes)==OverlaySha||callbackOwner||storeOwner))
   throw new InvalidDataException("Dark Passage dog source was relabeled and lost its transition owner.");
 }
 private static void RequireNative(byte[] exe,uint pc,int length,string sha){int at=checked((int)(pc-UniversalExeBindings.ExeBase));if(at<0||at+length>exe.Length||Sha(exe.AsSpan(at,length))!=sha)throw new InvalidDataException("Dark Passage dog lost a native collision/reward/checkpoint owner.");}
 private static string Sha(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}

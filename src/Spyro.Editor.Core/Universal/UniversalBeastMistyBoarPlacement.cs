using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Issued original placement and shared charge-state owner. Callers cannot manufacture one.</summary>
public sealed class UniversalBoarPlacementCertificate
{
 internal string Key {get;} internal int Row {get;} internal int Class {get;} internal int? ChargeAddress {get;}
 internal UniversalSourceRowIndex[] Indices {get;} internal int[] Delta {get;}
 internal string RowSha {get;} internal string PropertiesSha {get;} internal (int Offset,int Target)[] Pointers {get;}
 internal UniversalBoarPlacementCertificate(UniversalRowTemplate t,int? charge,int[]? delta=null)
 {Key=t.SourceLevelKey;Row=t.SourceRowIndex;Class=t.ClassId;ChargeAddress=charge;RowSha=UniversalBeastMistyBoarPlacement.Hash(t.Row);PropertiesSha=UniversalBeastMistyBoarPlacement.Hash(t.Properties);Pointers=t.InternalPointers.ToArray();Indices=t.SourceRowIndices.ToArray();Delta=delta?.ToArray()??[0,0,0];}
}

/// <summary>Original boar saved positions, path nodes, reciprocal chicken indices and group charge state.</summary>
public static class UniversalBeastMistyBoarPlacement
{
 private sealed record Profile(string Key,string DataSha,string OverlaySha,string BindingsSha,int[] Rows);
 private static readonly Profile[] Profiles=[
  new("beastmakers","41264e1b9ab2eb877fa9890c32456f3636090159cdff4486bca47571c85117bf","6bed996a20df44803ec4eefe434294082c3f5814ce56080e0594bab95eeb287f","c6df31a5dd1905b9f8abe221af6412b98aecc2470aa6249e768a0a8291345478",[32,33,34,79]),
  new("mistybog","b8019233e082408247c0a5a7b5c60f95d36147d3029041aec60fa5decce44541","3b37eb8b4b0f5fb74e7d263670705e20c807f2d8f1bbf03f34a461eb1c3e2ff6","32c34f835328c9b494d2dba27a3d385b86d11d402b93f36ef6e524dfe8a8c397",[8,9,14,15,28])];
 internal static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
 private static Profile? Owner(string key)=>Profiles.SingleOrDefault(p=>string.Equals(p.Key,key,StringComparison.OrdinalIgnoreCase));
 public static bool Applies(UniversalRowTemplate t)=>Owner(t.SourceLevelKey) is { } p && (t.ClassId==0x89 || p.Key=="mistybog"&&t.SourceRowIndex==27&&t.ClassId==0x1C);
 private static void RequireOverlay(Profile p,UniversalOverlay overlay)
 {
  byte[] b=new byte[16*overlay.Bindings.Count];int at=0;
  foreach(var item in overlay.Bindings.OrderBy(b=>b.Key))
   foreach(uint word in new[]{item.Key,item.Value.LuiAddress,item.Value.AddiuAddress,item.Value.Value}){BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at),word);at+=4;}
  if(Hash(overlay.Bytes)!=p.OverlaySha||Hash(b)!=p.BindingsSha)throw new InvalidDataException("Boar placement lost its original overlay or callback owners.");
 }
 private static UniversalRowTemplate Original(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate t)
 {
  var p=Owner(t.SourceLevelKey)??throw new InvalidDataException("Unknown boar placement source.");RequireOverlay(p,overlay);
  if(Hash(data.Bytes)!=p.DataSha)throw new InvalidDataException("Boar placement lost its original level data owner.");
  var original=data.CaptureRow(t.SourceLevelKey,t.SourceRowIndex);
  if(!Applies(original)|| !t.Row.SequenceEqual(original.Row)||!t.Properties.SequenceEqual(original.Properties)||!t.InternalPointers.SequenceEqual(original.InternalPointers)||!t.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers)||t.DroppedExternalPointers!=original.DroppedExternalPointers)
   throw new InvalidDataException("Boar placement differs from its original row, properties or pointers.");
  if(t.ClassId==0x89&&!p.Rows.Contains(t.SourceRowIndex))throw new InvalidDataException("Boar placement is not an original placed variant.");
  return original;
 }
 public static UniversalRowTemplate Capture(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate t)
 {
  if(!Applies(t))return t;_=Original(data,overlay,t);
  int? charge=t.ClassId==0x89?BinaryPrimitives.ReadInt32LittleEndian(t.Properties.AsSpan(0x40)):null;
  if(t.ClassId==0x89&&(t.InternalPointers.Count!=3||!t.InternalPointers.Contains((0x40,0x60))||BinaryPrimitives.ReadInt32LittleEndian(t.Properties.AsSpan(0x60))!=0))throw new InvalidDataException("Boar charge state has no original four-byte zero owner.");
  IReadOnlyList<UniversalSourceRowIndex> links=t.ClassId==0x89&&t.SourceLevelKey.Equals("mistybog",StringComparison.OrdinalIgnoreCase)&&t.SourceRowIndex==28?[new(0x48,27,0x1C)]:t.ClassId==0x1C?[new(4,28,0x89)]:[];
  if(links.Any(l=>BinaryPrimitives.ReadInt32LittleEndian(t.Properties.AsSpan(l.Offset))!=l.SourceRow||BinaryPrimitives.ReadUInt16LittleEndian(data.Row(l.SourceRow).AsSpan(0x36))!=l.SourceClass||t.InternalPointers.Any(p=>p.Offset==l.Offset)))throw new InvalidDataException("Boar and chicken reciprocal indices lost their placed owners.");
  var result=t with{SourceRowIndices=links,ImportedRowIndices=[]};return result with{BoarPlacementCertificate=new(result,charge)};
 }
 public static UniversalRowTemplate Rebase(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate t,ReadOnlySpan<byte> row)
 {
  if(!Applies(t))return t;_=Original(data,overlay,t);RequireCertificate(t);RequireInitialRow(t,row);
  byte[] props=t.Properties.ToArray();int[] delta=new int[3];for(int axis=0;axis<3;axis++)delta[axis]=checked(BinaryPrimitives.ReadInt32LittleEndian(row[(12+4*axis)..])-BinaryPrimitives.ReadInt32LittleEndian(t.Row.AsSpan(12+4*axis)));
  HashSet<int> coordinates=[];
  void Vector(int offset){for(int axis=0;axis<3;axis++){int at=offset+4*axis;if(!coordinates.Add(at)||t.InternalPointers.Any(p=>p.Offset==at))throw new InvalidDataException("Boar world vector overlaps a pointer or coordinate.");BinaryPrimitives.WriteInt32LittleEndian(props.AsSpan(at),checked(BinaryPrimitives.ReadInt32LittleEndian(props.AsSpan(at))+delta[axis]));}}
  if(t.ClassId==0x89)Vector(4);
  var paths=t.InternalPointers.Where(p=>t.ClassId==0x89?p.Offset is 0x18 or 0x1C:p.Offset==0).OrderBy(p=>p.Target).ToArray();
  if(paths.Length!=(t.ClassId==0x89?2:1))throw new InvalidDataException("Boar path membership changed.");
  foreach(var path in paths){int start=path.Target,end=t.InternalPointers.Where(p=>p.Target>start).Select(p=>p.Target).DefaultIfEmpty(props.Length).Min();if(start<0||start+8>end||props[start]==0||props[start+1]>=props[start]||start+8+16*props[start]!=end)throw new InvalidDataException("Boar path node count or cursor escaped its exact source block.");for(int n=0;n<props[start];n++)Vector(start+8+16*n);}
  var result=t with{Properties=props};return result with{BoarPlacementCertificate=new(result,t.BoarPlacementCertificate!.ChargeAddress,delta)};
 }
 public static void RequireInitialRow(UniversalRowTemplate t,ReadOnlySpan<byte> row)
 {
  if(!Applies(t))return;if(row.Length!=UniversalLevelData.RowStride)throw new InvalidDataException("Boar row is truncated.");
  for(int i=4;i<row.Length;i++)if(i is not (>=12 and <24) && i is not(0x43 or 0x46 or 0x4A)&&row[i]!=t.Row[i])throw new InvalidDataException("This boar needs its original state, animation and collision settings.");
  if(row[0x43]!=255||row[0x4A]!=255)throw new InvalidDataException("Imported boar needs unrestricted activation and visibility.");
 }
 private static void RequireCertificate(UniversalRowTemplate t)
 {
  var c=t.BoarPlacementCertificate??throw new InvalidDataException("Boar placement has no issued source owner.");
  if(c.Key!=t.SourceLevelKey||c.Row!=t.SourceRowIndex||c.Class!=t.ClassId||c.RowSha!=Hash(t.Row)||c.PropertiesSha!=Hash(t.Properties)||!c.Pointers.SequenceEqual(t.InternalPointers)||!c.Indices.SequenceEqual(t.SourceRowIndices))throw new InvalidDataException("Boar issued placement inputs changed after certification.");
 }
 internal static void ApplyChargePointer(UniversalRowTemplate t,IReadOnlyList<(byte[] Row,UniversalRowTemplate Template)> rows,IReadOnlyList<int> starts,Span<byte> properties)
 {
  if(!Applies(t)){if(t.BoarPlacementCertificate!=null)throw new InvalidDataException("Issued boar placement was relabeled.");return;}RequireCertificate(t);if(starts.Count!=rows.Count||starts.Where(s=>s!=0).Distinct().Count()!=starts.Count(s=>s!=0)||starts.Any(s=>s<0))throw new InvalidDataException("Boar property allocations are incomplete or duplicate.");
  var c=t.BoarPlacementCertificate!;
  foreach(var own in rows.Where(r=>ReferenceEquals(r.Template.BoarPlacementCertificate,c)))
  {
   if(own.Row.Length!=UniversalLevelData.RowStride)throw new InvalidDataException("Allocated boar row is truncated.");
   for(int offset=4;offset<own.Row.Length;offset++)if(offset is not(>=12 and<24) && offset is not(0x36 or 0x37 or 0x43 or 0x46 or 0x4A)&&own.Row[offset]!=t.Row[offset])throw new InvalidDataException("Allocated boar state, animation or collision inputs changed.");
   for(int axis=0;axis<3;axis++)if(BinaryPrimitives.ReadInt32LittleEndian(own.Row.AsSpan(12+4*axis))!=checked(BinaryPrimitives.ReadInt32LittleEndian(t.Row.AsSpan(12+4*axis))+c.Delta[axis]))throw new InvalidDataException("Allocated boar pose no longer matches its translated property owners.");
   if(own.Row[0x43]!=255||own.Row[0x4A]!=255)throw new InvalidDataException("Allocated boar activation or visibility changed.");
  }
  foreach(var link in t.ImportedRowIndices){var targetTemplate=rows[link.PlacementIndex].Template;RequireCertificate(targetTemplate);if(!c.Delta.SequenceEqual(targetTemplate.BoarPlacementCertificate!.Delta))throw new InvalidDataException("Linked boar and chicken need their original relative positions.");}
  if(c.ChargeAddress==null)return;
  int target=-1;
  for(int i=0;i<rows.Count;i++){var other=rows[i].Template;if(!Applies(other))continue;RequireCertificate(other);var cert=other.BoarPlacementCertificate!;if(cert.Key==c.Key&&cert.ChargeAddress==c.ChargeAddress&&string.Equals(t.LinkGroupId??"",other.LinkGroupId??"",StringComparison.Ordinal)){target=i;break;}}
  if(target<0||!t.InternalPointers.Contains((0x40,0x60))||!rows[target].Template.InternalPointers.Contains((0x40,0x60)))throw new InvalidDataException("Boar shared charge state escaped its certified group.");
  BinaryPrimitives.WriteInt32LittleEndian(properties[0x40..],checked(starts[target]+0x60));
 }
 public static void RequireNativeRemoval(string key,UniversalLevelData current,UniversalLevelData original,UniversalOverlay overlay,IReadOnlySet<int> removed)
 {
  if(!key.Equals("mistybog",StringComparison.OrdinalIgnoreCase)||removed.Count==0)return;
  foreach(int row in new[]{27,28}){if(removed.Contains(row))continue;var before=Capture(original,overlay,original.CaptureRow(key,row));var now=current.CaptureRow(key,row);if(now.ClassId!=before.ClassId||before.SourceRowIndices.Any(l=>now.Properties.Length<l.Offset+4||BinaryPrimitives.ReadInt32LittleEndian(now.Properties.AsSpan(l.Offset))!=l.SourceRow))throw new InvalidDataException("Edited native boar or chicken cannot hide its original link before removal.");if(before.SourceRowIndices.Any(l=>removed.Contains(l.SourceRow)))throw new InvalidDataException("Remove the linked boar and chicken together.");}
 }
 internal static void RequireCopiedInputs(UniversalAsmPart part,string key,UniversalOverlay source,IReadOnlyList<int> classes,IReadOnlyDictionary<int,int> map,byte[] exe)
 {
  var p=Owner(key);if(p==null||!classes.Contains(0x89)&&!(key=="mistybog"&&classes.Contains(0x1C)))return;RequireOverlay(p,source);
  const uint start=0x80037F90;const int length=10128;
  if(exe.Length<(int)(start-UniversalExeBindings.ExeBase)+length||Hash(exe.AsSpan((int)(start-UniversalExeBindings.ExeBase),length))!="5b7e67a79df04ef66ee63fda7d084b73989a87a7886cce5de46db1ad6e93b222")throw new InvalidDataException("Boar lost its native movement, path and collision helper owner.");
  foreach(var native in new[]{(Pc:0x800169ACu,Length:5288,Sha:"cd7816e798e34cc9b4a015bca681d845f526823c50ca29e2db94cf8b1ced0426"),(Pc:0x8003ABC0u,Length:1308,Sha:"979e567dd4bebbdd9cadaf8f884e1110be16f293c6590ba6214941be5be7a4a8"),(Pc:0x8003B7C0u,Length:148,Sha:"a8378b5d829c8a0661affbb6e50eb64b606c3114e116a38ec1b7137ba8a30ec4")})
   if(exe.Length<(int)(native.Pc-UniversalExeBindings.ExeBase)+native.Length||Hash(exe.AsSpan((int)(native.Pc-UniversalExeBindings.ExeBase),native.Length))!=native.Sha)throw new InvalidDataException("Boar lost its native geometry, reward or checkpoint helper owner.");
  var expected=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,classes,part.Items.FirstOrDefault()?.Label??throw new InvalidDataException("Missing boar copied entry."),map).Part;
  if(!part.Items.SequenceEqual(expected.Items)||part.Tables.Count!=expected.Tables.Count||expected.Tables.Any(t=>!part.Tables.TryGetValue(t.Key,out var target)||!target.SequenceEqual(t.Value)))throw new InvalidDataException("Boar world, path, charge state or reciprocal index consumer changed.");
 }
}

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
namespace Spyro.Editor.Core.Universal;
/// <summary>Explicit original Artisans removal profile. Native survivors never move;
/// removed assets/property owners are reclaimed and source0/1 reuses rows0/1.
/// Balloonist and Balloon removal removes travel. This never deletes project objects.</summary>
internal static class UniversalAlpineArtisansHost
{
 internal const string DataHash="86cde4f886d97821a3a4b869100b586842dbd4b9fb696558bead274bb547af4f",OverlayHash="7df6cf2f3ed4a71a123f03621019470bd6fe0cd243f75b1e8c3a115e6b1fcb55";
 internal const int CacheBytes=1192;
 internal static readonly int[] RemovedRows=[0,1,2,3,4,5,6,7,29,30,31,32,33,45,46,95,96,97,98,99,100,101,102,103,104,105,106,107,108,118,119,120,121,122,123,124,125,126,127,128,129,130,131,132,139,140];
 private static void Require(bool value,string reason){if(!value)throw new InvalidDataException("Complete Alpine group in Artisans: "+reason);}
 internal static string Hash(ReadOnlySpan<byte> value)=>Convert.ToHexStringLower(SHA256.HashData(value));
 internal static void RequireOriginal(UniversalLevelData data,UniversalOverlay overlay,byte[] exe,IReadOnlyCollection<int>? removed)
 {
  Require(Hash(data.Bytes)==DataHash&&Hash(overlay.Bytes)==OverlayHash,"start from original Artisans objects, assets and terrain");UniversalAlpineArmNative.RequireNative(exe);
  Require(removed!=null&&removed.Order().SequenceEqual(RemovedRows),"explicitly remove all six Gnorcs (T7,29,30,31,32,33), Balloonist T45 and Balloon T46 (removes travel), Tower Flags T0..6,95,96, all20 Tulip flowers, all four tree rows T102..105, and Grass T121,122,128..130; partial or additional families are not supported");
  Require(data.Scene().RowCount==174&&UniversalMobyComposer.ArenaSlots(data)==91,"original static/dynamic arena changed");
  Require(Enumerable.Range(0,174).All(row=>(data.Row(row)[58]&127)!=3),"new original source0/1 pending-drop group3 collides with a native owner");
  // Complete immutable host images bind incoming code. Surviving native indices
  // retain their original physical identity; no logical save index rewrites them.
  foreach(int row in new[]{133,134,135,136,137})Link(row,0x12,4,138);
  Link(160,0x31,8,91);
  foreach(var pair in new[]{(88,89),(91,90),(93,92),(143,142)}){Link(pair.Item1,0xFA,0x20,pair.Item2);Require(I(data.CaptureRow("artisans",pair.Item1).Properties,0x24)==-1,"negative dragon companion index changed");}
  foreach(var pair in new[]{(161,88,89),(162,91,90),(163,93,92),(164,143,142)}){Link(pair.Item1,0x6E,0,pair.Item2);Link(pair.Item1,0x6E,4,pair.Item3);}
  var whirlwind=data.CaptureRow("artisans",42);Require(whirlwind.ClassId==0x12C&&I(whirlwind.Properties,0x10)==-1&&(I(whirlwind.Properties,8)&8)==0,"inactive native whirlwind index domain changed");
  var sheep=data.CaptureRow("artisans",115);Require(sheep.ClassId==0x143&&I(sheep.Properties,0)==10,"sheep class filter changed");
  void Link(int row,int cls,int field,int target){var owner=data.CaptureRow("artisans",row);Require(owner.ClassId==cls&&I(owner.Properties,field)==target&&!RemovedRows.Contains(row)&&!RemovedRows.Contains(target),"surviving native indexed owner changed");}
 }
 private static int I(byte[] bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at));
 internal static (byte[] Data,byte[] Exe) Restore(byte[] staged,byte[] overlay,byte[] exe,UniversalLevelData original,bool accounted)
 {
  Require(staged.Length==original.Bytes.Length,"saved native deletion layout changed");byte[] restored=staged.ToArray();int offset=original.Subfiles()[3].Offset+original.Scene().RowsOffset;
  foreach(int row in RemovedRows)for(int axis=0;axis<3;axis++){int at=offset+88*row+12+4*axis;Require(I(restored,at)==-480000,"save every explicit profile deletion before export");original.Bytes.AsSpan(at,4).CopyTo(restored.AsSpan(at));}
  byte[] restoredExe=exe.ToArray();if(accounted)UniversalTreasureAccounting.ApplyDelta(restoredExe,10,RemovedRows.Sum(r=>UniversalTreasureAccounting.KnownValue(original.Row(r))));
  Require(Hash(restored)==DataHash&&Hash(overlay)==OverlayHash,"saved edits include fields outside the explicit native deletions");UniversalAlpineArmNative.RequireNative(restoredExe);return(restored,restoredExe);
 }
 internal static void Reserve(UniversalLevelData dest)
 {
  var scene=dest.Scene();var sf=dest.Subfiles()[3];var bytes=dest.Bytes.AsSpan(sf.Offset,sf.Length).ToArray();const int growth=32*112;int insert=scene.ArenaEnd;int Move(int n)=>n>=insert?n+growth:n;byte[] expanded=new byte[scene.UsedEnd+growth];bytes.AsSpan(0,insert).CopyTo(expanded);bytes.AsSpan(insert,scene.UsedEnd-insert).CopyTo(expanded.AsSpan(insert+growth));BinaryPrimitives.WriteInt32LittleEndian(expanded.AsSpan(scene.MobyComponent),scene.MobyComponentSize+growth);
  foreach(int f in scene.Fixups)BinaryPrimitives.WriteInt32LittleEndian(expanded.AsSpan(Move(f)),Move(I(bytes,f)));int fs=Move(scene.FixupsOffset);for(int n=0;n<scene.Fixups.Count;n++)BinaryPrimitives.WriteInt32LittleEndian(expanded.AsSpan(fs+4+4*n),Move(scene.Fixups[n]));dest.ReplaceScene(expanded);
  Require(dest.Scene().RowCount==174&&UniversalMobyComposer.ArenaSlots(dest)==123,"complete added32 paired dynamic lifetimes were not allocated");
 }
 internal static IEnumerable<(long Offset,int Length)> Protected(UniversalLevelData original)
 {
  using var stream=typeof(UniversalAlpineArtisansHost).Assembly.GetManifestResourceStream("Spyro.Editor.Core.Universal.ArtisansAlpineTextureOwners.json")??throw new InvalidDataException("Artisans native texture owner is missing");using var bytes=new MemoryStream();stream.CopyTo(bytes);Require(Hash(bytes.ToArray())=="62782dc41f8440dbc58153af245c61786bfdf9401a18f8dc6b6bac835f2a5a97","complete immutable native texture ledger changed");using var doc=JsonDocument.Parse(bytes.ToArray());var root=doc.RootElement;Require(root.GetProperty("OriginalHostSha256").GetString()==DataHash&&root.GetProperty("NativeVramSha256").GetString()==Hash(original.Vram()[..UniversalVramAllocator.ImageBytes]),"texture ledger differs from original Artisans assets");return root.GetProperty("ProtectedRanges").EnumerateArray().Select(r=>(r[0].GetInt64(),r[1].GetInt32())).ToArray();
 }
 internal static void RequireArena(UniversalLevelData source,UniversalOverlay sourceOverlay,UniversalLevelData original,UniversalOverlay host,UniversalLevelData written,byte[] exe,IReadOnlyList<int> parents)
 {
  RequireOriginal(original,host,exe,RemovedRows);UniversalAlpineArmResources.ProveSource(source,sourceOverlay);
  int words=written.Scene().RowCount+UniversalMobyComposer.ArenaSlots(written)+1;int usable=(written.Scene().ArenaEnd-written.Scene().RowsOffset-174*88-73)/112;
  Require(words*4==CacheBytes&&usable==122&&0x400+CacheBytes<=0x1C00,"complete native actor/dynamic/sentinel cache or SonyImage scratch owner changed");Require(parents.SequenceEqual(new[]{0,1}),"new arm/reward parents must be the two static source group rows");
  // Native parent-index byte has one direct consumer, collected-item marking.
  // Its source parents are static0/1; native16->120 can carry a dynamic parent,
  // but120 is Sparx, not a collected reward, and has no byte-parent read. Digit
  // constructors receive null parents. Dynamic physical indices are not saves.
  uint[] Reads(ReadOnlySpan<byte> bytes,uint start){List<uint> at=[];for(int n=0;n+4<=bytes.Length;n+=4){uint w=BinaryPrimitives.ReadUInt32LittleEndian(bytes[n..]);if(UniversalMips.Op(w)is 32 or 33 or 36 or 37&&UniversalMips.SImm(w)==86)at.Add(start+(uint)n);}return at.ToArray();}
  Require(Reads(exe,UniversalExeBindings.ExeBase).SequenceEqual(new uint[]{0x8003B95C})&&Reads(host.Bytes,UniversalOverlay.Base).Length==0&&Reads(sourceOverlay.Bytes,UniversalOverlay.Base).Length==0,"native typed byte-parent consumer inventory changed");
  var sw=UniversalDispatchAnalysis.FindUpdateSwitch(host);var trace=UniversalDispatchAnalysis.Trace(host,sw.TreeStart,120,sw.Seeds,sw.ClassRegister);var closure=UniversalDispatchAnalysis.Closure(host,sw.Function,new[]{trace.Stop},new HashSet<uint>{sw.DefaultStop});
  Require(trace.Stop==0x800819F8&&closure.Instructions.Count==743,"complete retained120 lifetime owner changed");
  foreach(uint pc in closure.Instructions){uint word=host.Word(pc);Require(!(UniversalMips.Op(word)is 32 or 33 or 36 or 37&&UniversalMips.SImm(word)==86),"Sparx acquires a byte-parent consumer");if(UniversalMips.IsJal(word))Require(UniversalMips.BranchTarget(word,pc)is not(0x8003B854 or 0x8003B9D4),"Sparx acquires a collected-object mark edge");if(UniversalMips.IsJalr(word))Require(word==0x0040F809&&host.Word(pc-12)==0x3C028007&&host.Word(pc-8)==0x8C4258E4&&host.Word(pc-4)==0,"Sparx indirect edge changed from original758E4 callback");}
  uint[] Incoming(byte[] bytes,uint start,uint target){List<uint> at=[];for(int n=0;n+4<=bytes.Length;n+=4){uint word=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(n));Require(word!=target,"stored mark pointer enables an unowned indirect parent consumer");if(UniversalMips.Op(word)is 2 or 3&&UniversalMips.BranchTarget(word,start+(uint)n)==target)at.Add(start+(uint)n);}return at.ToArray();}
  Require(Incoming(exe,UniversalExeBindings.ExeBase,0x8003B854).SequenceEqual(new uint[]{0x8003BAB4})&&Incoming(host.Bytes.ToArray(),UniversalOverlay.Base,0x8003B854).SequenceEqual(new uint[]{0x8007F374,0x80080498,0x80082EB4}),"complete original mark incoming owners changed");
  Require(Incoming(exe,UniversalExeBindings.ExeBase,0x8003B9D4).Length==0&&Incoming(host.Bytes.ToArray(),UniversalOverlay.Base,0x8003B9D4).SequenceEqual(new uint[]{0x8007FDE0})&&!closure.Instructions.Contains(0x8007FDE0),"native marked-reward helper became reachable by120");

 }
}

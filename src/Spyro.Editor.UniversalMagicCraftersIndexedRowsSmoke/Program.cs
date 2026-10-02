using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;
int checks=0;
void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
void Refused(Action a,string name){bool ok=false;try{a();}catch(InvalidDataException){ok=true;}Check(ok,name);}
UniversalSourceLibrary.RequireOriginal(args[0]);
var lib=new UniversalSourceLibrary(args[0],LevelCatalog.Load(args[1]));var s=lib.Get("magiccrafters");
var rows=Enumerable.Range(0,s.Data.Scene().RowCount).Where(r=>BinaryPrimitives.ReadUInt16LittleEndian(s.Data.Row(r).AsSpan(0x36))==0x10F).ToArray();
Check(rows.Length==14,"Original14 owners");
UniversalRowTemplate Raw(int row)=>s.Data.CaptureRow("magiccrafters",row);
UniversalRowTemplate Capture(int row)=>UniversalIndexedRows.Capture(s.Data,s.Overlay,Raw(row));
UniversalMagicCraftersDruidIndexedRows.ProveSource("magiccrafters",s.Data,s.Overlay);
foreach(int row in rows){if(row is 2 or 3 or 4){var t=Capture(row);Check(t.SourceRowIndices.Single()==new UniversalSourceRowIndex(0x24,row+1,0x10F),"Positive exact index");}else Refused(()=>Capture(row),"Unproved negative refuses");}
var owner=Capture(2);
foreach(int offset in new[]{0,0x14,0x24,0x28}){var props=owner.Properties.ToArray();props[offset]^=1;Refused(()=>UniversalIndexedRows.Capture(s.Data,s.Overlay,owner with{Properties=props}),"Changed template property refuses");}
Refused(()=>UniversalIndexedRows.Capture(s.Data,s.Overlay,owner with{SourceRowIndex=3}),"Changed source row refuses");
foreach(uint start in new uint[]{0x80086BB8,0x80086E18,0x80086E94,0x80087278,0x80087298,0x80087D60,0x80087F40,0x800880BC})
for(uint pc=start-4;pc<start+48;pc+=4){var bytes=s.Overlay.Bytes.ToArray();bytes[(int)(pc-UniversalOverlay.Base)]^=1;Refused(()=>UniversalIndexedRows.Capture(s.Data,new UniversalOverlay(bytes,s.Overlay.Bindings),owner),"Source packet or incoming mutation refuses");}
foreach(uint global in new[]{UniversalExeBindings.UpdateGlobal,UniversalExeBindings.InitGlobal,UniversalExeBindings.CopyBufferGlobal}){var b=s.Overlay.Bindings.ToDictionary(x=>x.Key,x=>x.Value);b[global]=b[global] with{Value=b[global].Value+4};Refused(()=>UniversalIndexedRows.Capture(s.Data,new UniversalOverlay(s.Overlay.Bytes.ToArray(),b),owner),"Binding mutation refuses");}
var changedData=s.Data.Bytes.ToArray();changedData[0x200]^=1;Refused(()=>UniversalIndexedRows.Capture(new(changedData),s.Overlay,owner),"Full data mutation refuses");
var target=Raw(3); // Allocation-only target: full group remains refused at terminal row5.
Refused(()=>UniversalIndexedRows.Resolve(owner,[owner]),"Missing target");Refused(()=>UniversalIndexedRows.Resolve(owner,[owner,target,target]),"Duplicate target");
Refused(()=>UniversalIndexedRows.Resolve(owner,[owner,target with{SourceLevelKey="stonehill"}]),"Other source identity");
var badClass=target.Row.ToArray();BinaryPrimitives.WriteUInt16LittleEndian(badClass.AsSpan(0x36),0x18);
Refused(()=>UniversalIndexedRows.Resolve(owner,[owner,target with{Row=badClass}]),"Wrong source class");
Refused(()=>UniversalIndexedRows.Resolve(owner,[owner,target with{LinkGroupId="other"}]),"Grouped target cannot bind blank owner");
var groupOwner=owner with{LinkGroupId="one"};var groupTarget=target with{LinkGroupId="one"};
var secondOwner=owner with{LinkGroupId="two"};var secondTarget=target with{LinkGroupId="two"};
var both=new[]{groupOwner,secondTarget,groupTarget,secondOwner};
Check(UniversalIndexedRows.Resolve(groupOwner,both).ImportedRowIndices.Single().PlacementIndex==2,"First group exact target");
Check(UniversalIndexedRows.Resolve(secondOwner,both).ImportedRowIndices.Single().PlacementIndex==1,"Second group exact target");
UniversalLevelData Fixture()
{
    const int so = 0x2000, mob = 0xA4, mobSize = 0x1000, count = 4, prop = mob + mobSize;
    byte[] data = new byte[0x4000]; void Put(int at, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(at), value);
    for (int i = 0; i < 4; i++) { Put(i * 8, 0x800 + i * 0x800); Put(i * 8 + 4, i == 3 ? 0x2000 : 0x800); }
    int cursor = so + 0x88; for (int i = 0; i < 7; i++) { Put(cursor, 4); cursor += 4; }
    Put(so + mob, mobSize); Put(so + mob + 4, count); Put(so + prop, 4 + count * 24);
    for (int i = 0; i < count; i++) { int row = so + mob + 8 + i * UniversalLevelData.RowStride; Put(row, prop + 4 + i * 24); BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(row + 0x36), 0x18); }
    int pod = prop + 4 + count * 24; Put(so + pod, 4); Put(so + pod + 4, 4); Put(so + pod + 8, count);
    for (int i = 0; i < count; i++) Put(so + pod + 12 + i * 4, mob + 8 + i * UniversalLevelData.RowStride);
    return new(data);
}

List<object> readbacks=[];
foreach(int row in new[]{2,3,4})foreach(bool reverse in new[]{false,true})foreach(int[] removed in new[]{Array.Empty<int>(),new[]{1,3},new[]{0,1,2,3}}){
var o=Capture(row);var t=Raw(row+1);var ts=reverse?new[]{t,o}:new[]{o,t};var resolved=ts.Select(x=>UniversalIndexedRows.Resolve(x,ts)).ToArray();
var dest=Fixture();UniversalRowRemoval.RemoveOwnedPropertiesAndClearRows(dest,removed);
var result=dest.AppendRows(resolved.Select((x,i)=>{var rr=x.Row.ToArray();BinaryPrimitives.WriteUInt16LittleEndian(rr.AsSpan(0x36),(ushort)(0x1E0+i));return (rr,x);}).ToArray(),removed);
int oi=reverse?1:0,ti=1-oi;var rb=dest.CaptureRow("fixture",result.NewRowIndices[oi]);
Check(BinaryPrimitives.ReadInt32LittleEndian(rb.Properties.AsSpan(0x24))==result.NewRowIndices[ti],"Final physical index readback");
Check(BinaryPrimitives.ReadUInt16LittleEndian(dest.Row(result.NewRowIndices[ti]).AsSpan(0x36))==0x1E0+ti,"Destination model class separate");
int props=BinaryPrimitives.ReadInt32LittleEndian(dest.Row(result.NewRowIndices[oi]));Check(!dest.Scene().Fixups.Contains(props+0x24),"Integer not pointer fixup");
readbacks.Add(new{row,reverse,removed,physicalOwner=result.NewRowIndices[oi],physicalTarget=result.NewRowIndices[ti]});}
var removeExcept2=rows.Where(r=>r!=2).ToHashSet();
Refused(()=>UniversalMagicCraftersDruidIndexedRows.RequireNativeRemoval("magiccrafters",s.Data,s.Data,s.Overlay,removeExcept2),"Surviving positive owner cannot lose target");
foreach(int offset in new[]{0x14,0x24,0x28}){var cur=new UniversalLevelData(s.Data.Bytes.ToArray());var scene=cur.Scene();int prop=BinaryPrimitives.ReadInt32LittleEndian(cur.Row(2));cur.Bytes[cur.Subfiles()[3].Offset+prop+offset]^=1;Refused(()=>UniversalMagicCraftersDruidIndexedRows.RequireNativeRemoval("magiccrafters",cur,s.Data,s.Overlay,removeExcept2),"Changed native selector/index");}
var hidden=new UniversalLevelData(s.Data.Bytes.ToArray());var hr=hidden.Row(2);BinaryPrimitives.WriteUInt16LittleEndian(hr.AsSpan(0x36),0x18);
// Row() returns a copy; write through its physical scene address.
var hs=hidden.Scene();int rowAt=hidden.Subfiles()[3].Offset+hs.RowsOffset+2*UniversalLevelData.RowStride;BinaryPrimitives.WriteUInt16LittleEndian(hidden.Bytes.AsSpan(rowAt+0x36),0x18);
Refused(()=>UniversalMagicCraftersDruidIndexedRows.RequireNativeRemoval("magiccrafters",hidden,s.Data,s.Overlay,removeExcept2),"Hidden original native owner");
UniversalIndexedRows.RequireNativeRemoval("magiccrafters",s.Data,s.Data,s.Overlay,new HashSet<int>{0});Check(true,"Unchanged negative source indices do not block unrelated native removal");
foreach(int offset in new[]{0x14,0x24,0x28}){var cur=new UniversalLevelData(s.Data.Bytes.ToArray());int prop=BinaryPrimitives.ReadInt32LittleEndian(cur.Row(1));cur.Bytes[cur.Subfiles()[3].Offset+prop+offset]^=1;Refused(()=>UniversalIndexedRows.RequireNativeRemoval("magiccrafters",cur,s.Data,s.Overlay,new HashSet<int>{0}),"Changed surviving negative native selector/index refuses");}
var negativeHidden=new UniversalLevelData(s.Data.Bytes.ToArray());BinaryPrimitives.WriteUInt16LittleEndian(negativeHidden.Bytes.AsSpan(negativeHidden.Subfiles()[3].Offset+negativeHidden.Scene().RowsOffset+UniversalLevelData.RowStride+0x36),0x18);Refused(()=>UniversalIndexedRows.RequireNativeRemoval("magiccrafters",negativeHidden,s.Data,s.Overlay,new HashSet<int>{0}),"Hidden negative original owner refuses");
foreach(int[] pod in new[]{new[]{2,3,4,5},new[]{12,13},new[]{32,33}}){
foreach(int member in pod){Refused(()=>UniversalIndexedRows.RequireNativeRemoval("magiccrafters",s.Data,s.Data,s.Overlay,new HashSet<int>{member}),"Native positive pod reference forbids partial member removal");
var reused=new UniversalLevelData(s.Data.Bytes.ToArray());reused.SetRowClass(member,0x1FD);Refused(()=>UniversalIndexedRows.RequireNativeRemoval("magiccrafters",reused,s.Data,s.Overlay,new HashSet<int>{member}),"Native pod cannot activate replacement class in reused member slot");}
UniversalIndexedRows.RequireNativeRemoval("magiccrafters",s.Data,s.Data,s.Overlay,pod.ToHashSet());Check(true,"Whole native pod removal allowed");}
for(int offset=0;offset<0x24;offset+=2){var cur=new UniversalLevelData(s.Data.Bytes.ToArray());cur.Bytes[cur.Subfiles()[3].Offset+cur.Scene().PodComponent+offset]^=1;Refused(()=>UniversalIndexedRows.RequireNativeRemoval("magiccrafters",cur,s.Data,s.Overlay,new HashSet<int>{0}),"Native pod table/header/member mutation refuses");}
var podHidden=new UniversalLevelData(s.Data.Bytes.ToArray());podHidden.Bytes[podHidden.Subfiles()[3].Offset+podHidden.Scene().RowsOffset+13*UniversalLevelData.RowStride+0x43]=0xFF;Refused(()=>UniversalIndexedRows.RequireNativeRemoval("magiccrafters",podHidden,s.Data,s.Overlay,new HashSet<int>{0}),"Native pod membership cannot be hidden before unrelated removal");
UniversalMagicCraftersDruidIndexedRows.RequireNativeRemoval("magiccrafters",s.Data,s.Data,s.Overlay,rows.ToHashSet());Check(true,"Removing every original owner permits native check");
Refused(()=>UniversalPlacementGroups.Expand(lib.Get,new("magiccrafters",2,0,0,0)),"Full linked group still refuses terminal negative row5");
var sw=UniversalDispatchAnalysis.FindUpdateSwitch(s.Overlay);var trace=UniversalDispatchAnalysis.Trace(s.Overlay,sw.TreeStart,0x10F,sw.Seeds,sw.ClassRegister);var closure=UniversalDispatchAnalysis.Closure(s.Overlay,sw.Function,[trace.Stop],new HashSet<uint>{sw.TreeStart});
Check(UniversalClassRemapping.ConstantSpawns(s.Overlay,closure.Instructions).Any(x=>x.ClassId==0x13B),"Generic13B dependency remains in source closure");
Check(!s.Data.Registrations().Any(r=>r.ClassId==0x13B),"Missing13B model remains absent");
Check(UniversalIndexedRows.Capture(s.Data,s.Overlay,Raw(0)).SourceRowIndices.Count==0,"Other source class unchanged");
UniversalSourceLibrary.RequireOriginal(args[0]);
File.WriteAllText(args[2],JsonSerializer.Serialize(new{checks,positiveRows=new[]{2,3,4},refusedNegativeRows=rows.Except(new[]{2,3,4}),readbacks,FullCompose="still blocked by missing13B and terminal negative index",Runtime="not tested"},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {checks} Magic Crafters indexed-row checks");

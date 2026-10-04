using System.Text.Json;using System.Security.Cryptography;using Spyro.Editor.Core.Levels;using Spyro.Editor.Core.Universal;using Spyro.Editor.Core.Exporting;
if(args.Length is not(3 or 4))throw new ArgumentException("USA BIN, catalog root, NEW report path; optional prior runtime BIN as fourth argument");
string reportPath=Path.GetFullPath(args[2]);if(File.Exists(reportPath))throw new IOException("Preserve the existing report");Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
int checks=0;void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}void Refuse(Action a,string s){try{a();}catch(InvalidDataException){checks++;return;}throw new Exception("Failed refusal: "+s);}string H(byte[] b)=>Convert.ToHexStringLower(SHA256.HashData(b));
UniversalSourceLibrary.RequireOriginal(args[0]);var lib=new UniversalSourceLibrary(args[0],LevelCatalog.Load(args[1]));var src=lib.Get("magiccrafters");var host=lib.Get("darkhollow");var root=new UniversalMobyPlacement("magiccrafters",12,14500,63750,17740,EditorId:"magic12",PersistentSlot:0);var placements=UniversalPlacementGroups.Expand(lib.Get,root);Check(placements.Count==2&&placements[1].SourceRowIndex==13,"authentic recursive pod companion expansion");
var cert=UniversalMagicPairCertificate.Capture(host.Level.Key,host.Data.Bytes,host.Overlay.Bytes.ToArray(),lib.Disc.Exe,src.Data,src.Overlay,placements,null);
foreach(var p in placements){var raw=src.Data.CaptureRow("magiccrafters",p.SourceRowIndex);var t=UniversalIndexedRows.Capture(src.Data,src.Overlay,raw);Check(t.SourcePrerequisiteRows.Count==1&&t.SourcePrerequisiteRows[0].SourceClass==271,"exact source companion metadata");foreach(int at in new[]{0x14,0x24,0x28}){var bad=raw.Properties.ToArray();bad[at]^=1;Refuse(()=>UniversalMagicPairCertificate.CaptureSource(src.Data,src.Overlay,raw with{Properties=bad}),"changed protected source field");}}
foreach(var bad in new[]{placements.Take(1).ToArray(),placements.Concat(placements).ToArray(),placements.Select((p,i)=>i==0?p with{X=p.X+1}:p).ToArray(),placements.Select(p=>p with{Yaw=99}).ToArray(),placements.Select(p=>p with{RowByteOverrides=new Dictionary<int,byte>{{0x48,90}}}).ToArray()})Refuse(()=>UniversalMagicPairCertificate.Capture(host.Level.Key,host.Data.Bytes,host.Overlay.Bytes.ToArray(),lib.Disc.Exe,src.Data,src.Overlay,bad,null),"invalid/edited authored scope");
Refuse(()=>UniversalMagicPairCertificate.Capture("artisans",host.Data.Bytes,host.Overlay.Bytes.ToArray(),lib.Disc.Exe,src.Data,src.Overlay,placements,null),"unproved destination");Refuse(()=>UniversalMagicPairCertificate.Capture(host.Level.Key,host.Data.Bytes,host.Overlay.Bytes.ToArray(),lib.Disc.Exe,src.Data,src.Overlay,placements,new[]{1}),"native row removal/reuse");
var result=UniversalMobyComposer.Compose(host.Level,host.Data.Bytes,host.Overlay.Bytes.ToArray(),lib.Disc.Exe,lib.Get,lib.SharedClasses,UniversalMobyExport.DefaultProtectedVram(args[0],host.Level),placements,sharedModelPackage:lib.SharedModelPackage,playerAnimationSource:lib.PlayerAnimations);
File.WriteAllBytes(reportPath+".data",result.LevelData);
Check(result.NewRowIndices.SequenceEqual(new[]{128,129}),"final physical lifetime identity");var final=new UniversalLevelData(result.LevelData);cert.RequireFinalRows(final,result.NewRowIndices);checks++;var proof=result.Behavior.Single(b=>b.SourceLevelKey=="magiccrafters").PrivateMagicPairShadow??throw new Exception("Missing source/copy/native proof");UniversalMagicPairCollisionShadow.RequireReadback(result.Overlay,proof);checks++;
var sourceParticleTextures=UniversalParticleDependencies.Records(src.Data);var finalParticleTextures=UniversalParticleDependencies.Records(final);foreach(int type in new[]{0}){sourceParticleTextures.TryGetValue(type,out var a);finalParticleTextures.TryGetValue(type,out var b);Check(a!=null&&b!=null&&UniversalParticleDependencies.EquivalentTexture(a,src.Data.Vram(),b,final.Vram()),"actual deathpoof texture0 pixels/palette equivalent");}
Check(H(result.LevelData)=="b737012df4b565c3e2f76fd4d419267f87092c045afe4b1e5582bfa31b1df08b","current normal composition preserves source-aligned models; previous native-r5 data identity is superseded");Check(H(result.Overlay)=="55c3e4cde5ef4944909cf0990fbd2024c5daa51238907d36a8882f3b1b52aa82","current normal machine bytes exact native-r5 overlay hash");
foreach(var reg in final.Registrations())
{
 var sourceReg=(reg.ClassId==271?src.Data:host.Data).Registrations().Single(r=>r.ClassId==reg.ClassId);
 Check((reg.Root&7)==(sourceReg.Root&7),"complete native/imported model retains its original root alignment "+reg.ClassId);
}
int firstModel=final.Registrations().Min(r=>r.Root);
Check(BitConverter.ToInt32(final.Bytes,firstModel-8)==-1&&final.Bytes.AsSpan(firstModel-4,4).IndexOfAnyExcept((byte)0)<0,"complete player stream terminator and exact alignment gap");
foreach(int at in new[]{firstModel-8,firstModel-4}){var bad=final.Bytes.ToArray();bad[at]^=1;Refuse(()=>cert.RequireFinalRows(new(bad),result.NewRowIndices),"player terminator/alignment-gap mutation");}
if(args.Length==4){var prior=UniversalDiscImage.Read(args[3]);Check(prior.EntryBytes(host.Level.SourceWadEntry).SequenceEqual(result.LevelData)&&prior.EntryBytes(host.Level.SourceWadEntry-1).SequenceEqual(result.Overlay),"independent runtime-disc extraction equality");}
foreach(int offset in Enumerable.Range(0,88)){var bad=result.LevelData.ToArray();var sc=final.Scene();int at=final.Subfiles()[3].Offset+sc.RowsOffset+128*88+offset;bad[at]^=1;Refuse(()=>cert.RequireFinalRows(new UniversalLevelData(bad),result.NewRowIndices),"final row guard");}
foreach(var p in placements){var raw=src.Data.CaptureRow("magiccrafters",p.SourceRowIndex);for(int at=0;at<raw.Row.Length;at++){var bad=raw.Row.ToArray();bad[at]^=1;Refuse(()=>UniversalMagicPairCertificate.CaptureSource(src.Data,src.Overlay,raw with{Row=bad}),"every original row descriptor byte");}for(int at=0;at<raw.Properties.Length;at++){var bad=raw.Properties.ToArray();bad[at]^=1;Refuse(()=>UniversalMagicPairCertificate.CaptureSource(src.Data,src.Overlay,raw with{Properties=bad}),"every original property/resource byte");}}
foreach(var blob in new[]{"sourceData","sourceOverlay","hostData","hostOverlay","EXE"}){var data=src.Data.Bytes.ToArray();var ov=src.Overlay.Bytes.ToArray();var hd=host.Data.Bytes.ToArray();var ho=host.Overlay.Bytes.ToArray();var exe=lib.Disc.Exe.ToArray();var chosen=blob=="sourceData"?data:blob=="sourceOverlay"?ov:blob=="hostData"?hd:blob=="hostOverlay"?ho:exe;chosen[chosen.Length/2]^=1;Refuse(()=>UniversalMagicPairCertificate.Capture(host.Level.Key,hd,ho,exe,new UniversalLevelData(data),new UniversalOverlay(ov,src.Overlay.Bindings),placements,null),"complete original owner mutation");}
foreach(var binding in src.Overlay.Bindings){var bad=src.Overlay.Bindings.ToDictionary();bad[binding.Key]=binding.Value with{Value=binding.Value.Value+4};Refuse(()=>UniversalMagicPairCertificate.Capture(host.Level.Key,host.Data.Bytes,host.Overlay.Bytes.ToArray(),lib.Disc.Exe,src.Data,new UniversalOverlay(src.Overlay.Bytes.ToArray(),bad),placements,null),"every native source callback binding");}
foreach(int field in new[]{0x14,0x24,0x28}){var bad=result.LevelData.ToArray();int property=BitConverter.ToInt32(final.Row(128));bad[final.Subfiles()[3].Offset+property+field]^=1;Refuse(()=>cert.RequireFinalRows(new UniversalLevelData(bad),result.NewRowIndices),"final selector/index state field");}
{var bad=result.LevelData.ToArray();var sc=final.Scene();int so=final.Subfiles()[3].Offset;int member=BitConverter.ToInt32(bad,so+sc.PodComponent+16);bad[so+sc.PodComponent+member+2]^=1;Refuse(()=>cert.RequireFinalRows(new UniversalLevelData(bad),result.NewRowIndices),"final native pod physical member");}
var code=result.Overlay.AsSpan((int)(proof.Base-UniversalOverlay.Base),proof.CodeBytes).ToArray();cert.RequireCompiled(code,proof.Base);checks++;for(int i=0;i<code.Length;i++){var bad=code.ToArray();bad[i]^=1;Refuse(()=>cert.RequireCompiled(bad,proof.Base),"every compiled update/init/replay/callback/native-copy/data byte");}
foreach(int at in new[]{final.Subfiles()[2].Offset+56, final.Subfiles()[2].Offset+100,0,final.Bytes.Length-1}){var bad=result.LevelData.ToArray();bad[at]^=1;Refuse(()=>cert.RequireFinalRows(new UniversalLevelData(bad),result.NewRowIndices),"final complete model/header/padding owner");}
cert.RequireFinalOverlay(result.Overlay);checks++;foreach(int at in new[]{0,100,4096,result.Overlay.Length-1}){var bad=result.Overlay.ToArray();bad[at]^=1;Refuse(()=>cert.RequireFinalOverlay(bad),"whole final callback/table/incoming overlay");}
cert.RequireFinalExecutable(result.Executable);checks++;foreach(int at in new[]{0,(int)(0x8003B47C-0x8000F800),(int)(0x8004E3C8-0x8000F800),result.Executable.Length-1}){var bad=result.Executable.ToArray();bad[at]^=1;Refuse(()=>cert.RequireFinalExecutable(bad),"complete final native/callback/executable owner");}
var inactive=src.Data.CaptureRow("magiccrafters",11);Refuse(()=>UniversalMagicPairCertificate.CaptureSource(src.Data,src.Overlay,inactive),"new singleton cannot inherit the original pair certificate");


{
    using var resource=typeof(UniversalMobyComposer).Assembly.GetManifestResourceStream("Spyro.Editor.Core.Universal.MagicPairAssemblyOwner.json")!;
    var fixtures=JsonSerializer.Deserialize<PartFixture[]>(resource)!;
    List<UniversalAsmPart> Parts()=>fixtures.Select(f=>{var p=new UniversalAsmPart{LenientTables=f.LenientTables};p.Items.AddRange(f.Items);foreach(var t in f.Tables)p.Tables.Add(t.Key,t.Value);return p;}).ToList();
    var originalParts=Parts();cert.RequireCompiled(code,proof.Base,originalParts);checks++;
    foreach(int part in Enumerable.Range(0,originalParts.Count))
    {
        var wrong=Parts();int index=wrong[part].Items.FindIndex(i=>i.Word!=null);wrong[part].Items[index]=wrong[part].Items[index] with{SourcePc=0x80000000};Refuse(()=>cert.RequireCompiled(code,proof.Base,wrong),"compiler producer/source ownership schema mutation");
        wrong=Parts();index=wrong[part].Items.FindIndex(i=>i.Label!=null);if(index>=0){wrong[part].Items[index]=wrong[part].Items[index] with{Label="unowned_incoming"};Refuse(()=>cert.RequireCompiled(code,proof.Base,wrong),"compiler labels/incoming schema mutation");}
    }
}
foreach(int at in new[]{final.Subfiles()[3].Offset+16,final.Subfiles()[3].Offset+final.Scene().RowsOffset+4,final.Subfiles()[3].Offset+final.Scene().CollisionComponent+16,final.Subfiles()[3].Offset+final.Scene().FixupsOffset+4})
{var bad=result.LevelData.ToArray();bad[at]^=1;Refuse(()=>cert.RequireFinalRows(new(bad),result.NewRowIndices),"derived native scene/row/collision/fixup ownership mutation");}
var parameterCases=new List<object>();
foreach(var pose in new[]{(14500,63750,17740),(16500,65000,18240),(25000,55000,17408),(100000,100000,18000),(8192,9881,17740),(253316,253951,17740)})
foreach(int rootRow in new[]{12,13})
{
    var offset=rootRow==12?(0,0,0):(625,-1689,308);
    var paramRoot=new UniversalMobyPlacement("magiccrafters",rootRow,pose.Item1+offset.Item1,pose.Item2+offset.Item2,pose.Item3+offset.Item3,Yaw:src.Data.Row(rootRow)[0x46],EditorId:"parameter-root",PersistentSlot:0);
    var group=UniversalPlacementGroups.Expand(lib.Get,paramRoot);
    var owner=UniversalMagicPairCertificate.Capture(host.Level.Key,host.Data.Bytes,host.Overlay.Bytes.ToArray(),lib.Disc.Exe,src.Data,src.Overlay,group,null);
    var composed=UniversalMobyComposer.Compose(host.Level,host.Data.Bytes,host.Overlay.Bytes.ToArray(),lib.Disc.Exe,lib.Get,lib.SharedClasses,UniversalMobyExport.DefaultProtectedVram(args[0],host.Level),group,sharedModelPackage:lib.SharedModelPackage,playerAnimationSource:lib.PlayerAnimations);
    owner.RequireFinalRows(new(composed.LevelData),composed.NewRowIndices);checks++;
    var packet=composed.Behavior.Single().PrivateMagicPairShadow!;owner.RequireCompiled(composed.Overlay.AsSpan((int)(packet.Base-UniversalOverlay.Base),packet.CodeBytes).ToArray(),packet.Base);owner.RequireFinalOverlay(composed.Overlay);owner.RequireFinalExecutable(composed.Executable);checks+=3;
    Check(composed.NewRowIndices.SequenceEqual(new[]{128,129})&&new UniversalLevelData(composed.LevelData).CaptureRow("darkhollow",128).Row[0x53]==src.Data.Row(rootRow)[0x53],"physical row allocation follows authenticated root/companion order");
    parameterCases.Add(new{pose,rootRow,DataSHA=H(composed.LevelData),packet.Base});
}

{
    var guidRoot=root with{EditorId=Guid.NewGuid().ToString(),PersistentSlot=null,LinkGroupId=Guid.NewGuid().ToString()};var guidGroup=UniversalPlacementGroups.Expand(lib.Get,guidRoot);
    var direct=UniversalMobyComposer.Compose(host.Level,host.Data.Bytes,host.Overlay.Bytes.ToArray(),lib.Disc.Exe,lib.Get,lib.SharedClasses,UniversalMobyExport.DefaultProtectedVram(args[0],host.Level),guidGroup,sharedModelPackage:lib.SharedModelPackage,playerAnimationSource:lib.PlayerAnimations);
    Check(direct.LevelData.SequenceEqual(result.LevelData)&&direct.Overlay.SequenceEqual(result.Overlay),"ordinary nonfixture GUIDs and direct no-ledger slots preserve scene/code owners");
}
foreach(int padding in new[]{64,4096,65536})
{
    var relocated=UniversalMobyComposer.Compose(host.Level,host.Data.Bytes,host.Overlay.Bytes.ToArray(),lib.Disc.Exe,lib.Get,lib.SharedClasses,UniversalMobyExport.DefaultProtectedVram(args[0],host.Level),placements,sharedModelPackage:lib.SharedModelPackage,playerAnimationSource:lib.PlayerAnimations,magicPairBundlePaddingBytes:padding);
    var owner=UniversalMagicPairCertificate.Capture(host.Level.Key,host.Data.Bytes,host.Overlay.Bytes.ToArray(),lib.Disc.Exe,src.Data,src.Overlay,placements,null);var packet=relocated.Behavior.Single().PrivateMagicPairShadow!;
    owner.RequireCompiled(relocated.Overlay.AsSpan((int)(packet.Base-UniversalOverlay.Base),packet.CodeBytes).ToArray(),packet.Base);owner.RequireFinalOverlay(relocated.Overlay);owner.RequireFinalExecutable(relocated.Executable);owner.RequireFinalRows(new(relocated.LevelData),relocated.NewRowIndices);checks+=4;
    Check(packet.Base==proof.Base+padding,"independent allocated base");Check(relocated.LevelData.SequenceEqual(result.LevelData),"base relocation preserves independently checked serialized assets/scene");
    var changed=relocated.Overlay.ToArray();changed[(int)(proof.Base-UniversalOverlay.Base)]^=1;Refuse(()=>owner.RequireFinalOverlay(changed),"unowned allocated padding mutation");
    parameterCases.Add(new{padding,packet.Base,OverlaySHA=H(relocated.Overlay),relocated.Ram});
}
foreach(var pose in new[]{(8191,63750),(14500,9880),(253317,63750),(14500,253952)})
{ var badGroup=UniversalPlacementGroups.Expand(lib.Get,root with{X=pose.Item1,Y=pose.Item2});Refuse(()=>UniversalMagicPairCertificate.Capture(host.Level.Key,host.Data.Bytes,host.Overlay.Bytes.ToArray(),lib.Disc.Exe,src.Data,src.Overlay,badGroup,null),"actor/home/path neighbor grid bounds"); }
var lifetimeCases=new List<object>();
foreach(int firstSlot in new[]{1,2,4,31,32,63,64,126})
foreach(int rootRow in new[]{12,13})
{
    var offset=rootRow==12?(0,0,0):(625,-1689,308);
    var freshRoot=new UniversalMobyPlacement("magiccrafters",rootRow,root.X+offset.Item1,root.Y+offset.Item2,root.Z+offset.Item3,EditorId:Guid.NewGuid().ToString(),PersistentSlot:firstSlot){LinkGroupId=Guid.NewGuid().ToString()};
    var freshGroup=UniversalPlacementGroups.Expand(lib.Get,freshRoot);
    // Deleted ledger metadata is deliberately unrelated: no source lookup, actor,
    // code or group is imported for inactive identities. Partial retired groups
    // have the same slot-reservation semantics as whole retired groups.
    var retired=Enumerable.Range(0,firstSlot).Select(slot=>new UniversalMobyPlacement("artisans",999,0,0,0,EditorId:Guid.NewGuid().ToString(),PersistentSlot:slot,Retired:true){LinkGroupId="retired-"+(slot/2)}).ToArray();
    var ledger=retired.Concat(freshGroup).Reverse().ToArray();
    var owner=UniversalMagicPairCertificate.Capture(host.Level.Key,host.Data.Bytes,host.Overlay.Bytes.ToArray(),lib.Disc.Exe,src.Data,src.Overlay,ledger,null);
    var composed=UniversalMobyComposer.Compose(host.Level,host.Data.Bytes,host.Overlay.Bytes.ToArray(),lib.Disc.Exe,lib.Get,lib.SharedClasses,UniversalMobyExport.DefaultProtectedVram(args[0],host.Level),ledger,sharedModelPackage:lib.SharedModelPackage,playerAnimationSource:lib.PlayerAnimations,magicPairBundlePaddingBytes:firstSlot==32?4096:0);
    var packet=composed.Behavior.Single().PrivateMagicPairShadow!;owner.RequireCompiled(composed.Overlay.AsSpan((int)(packet.Base-UniversalOverlay.Base),packet.CodeBytes).ToArray(),packet.Base);owner.RequireFinalOverlay(composed.Overlay);owner.RequireFinalExecutable(composed.Executable);owner.RequireFinalRows(new(composed.LevelData),composed.NewRowIndices.Where(n=>n>=0).Order().ToArray());checks+=4;
    Check(composed.NewRowIndices.Take(2).SequenceEqual(new[]{129,128})&&composed.NewRowIndices.Skip(2).All(n=>n==-1),"deleted lifetime entries allocate no physical rows; report follows input order");
    Check(composed.PhysicalToLogicalSaveRows.SequenceEqual(Enumerable.Range(0,128).Concat(new[]{128+firstSlot,129+firstSlot})),"native prefix self-maps and new actors bind only fresh lifetime bits");
    Check(composed.PlacementSaveRows.Count==ledger.Length&&composed.PlacementSaveRows.Take(2).SequenceEqual(new[]{129+firstSlot,128+firstSlot})&&composed.PlacementSaveRows.Skip(2).All(n=>n==-1),"logical save report preserves incoming ledger order and retired sentinels");
    Check(new UniversalLevelData(composed.LevelData).Row(128)[0x53]==src.Data.Row(rootRow)[0x53],"either root retains allocation after retired prefixes");
    foreach(var window in NativeMobySaveMap.Windows())
    {
        Check(composed.Executable.AsSpan((int)(window.Address-UniversalExeBindings.ExeBase),window.Code.Length).SequenceEqual(window.Code),"all six canonical native lifetime owner windows independently read back");
        var bad=composed.Executable.ToArray();bad[(int)(window.Address-UniversalExeBindings.ExeBase)+window.Code.Length/2]^=1;Refuse(()=>owner.RequireFinalExecutable(bad),"native lifetime owner instruction mutation");
    }
    uint update=UniversalExeBindings.Read(composed.Executable,12)[UniversalExeBindings.UpdateGlobal].Value;
    uint leaf=BitConverter.ToUInt32(composed.Overlay,(int)(update-UniversalOverlay.Base)+12);uint table=leaf+48;
    Check(BitConverter.ToUInt32(composed.Overlay,(int)(update-UniversalOverlay.Base)+8)==NativeMobySaveMap.Tag,"native lifetime leaf gateway marker");
    Check(BitConverter.ToInt16(composed.Overlay,(int)(table-UniversalOverlay.Base)+128*2)==128+firstSlot&&BitConverter.ToInt16(composed.Overlay,(int)(table-UniversalOverlay.Base)+129*2)==129+firstSlot,"actual signed16 map table binds allocated source identity");
    foreach(uint address in new[]{update,update+8,leaf,leaf+4,leaf+16,leaf+44,table,table+256,table+258})
    {var bad=composed.Overlay.ToArray();bad[(int)(address-UniversalOverlay.Base)]^=1;Refuse(()=>owner.RequireFinalOverlay(bad),"gateway/argument/branch/leaf/table mutation");}
    string caseName="lifetime-"+firstSlot+"-root"+rootRow;
    File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(reportPath)!,caseName+".overlay"),composed.Overlay);File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(reportPath)!,caseName+".exe"),composed.Executable);
    lifetimeCases.Add(new{caseName,firstSlot,rootRow,update,leaf,table,map=composed.PhysicalToLogicalSaveRows,core=H(File.ReadAllBytes(typeof(UniversalMobyComposer).Assembly.Location)),DataSHA=H(composed.LevelData),OverlaySHA=H(composed.Overlay),ExeSHA=H(composed.Executable)});
}
var twice=placements.Select(p=>p with{Retired=true}).Concat(UniversalPlacementGroups.Expand(lib.Get,root with{EditorId=Guid.NewGuid().ToString(),LinkGroupId=Guid.NewGuid().ToString(),PersistentSlot=2})).ToArray();
foreach(var bad in new[]{twice.Select((p,i)=>i==2?p with{PersistentSlot=0}:p).ToArray(),twice.Select((p,i)=>i==0?p with{PersistentSlot=7}:p).ToArray(),twice.Select((p,i)=>i==2?p with{EditorId=twice[0].EditorId}:p).ToArray(),twice.Select(p=>p with{PersistentSlot=null}).ToArray(),placements.Select(p=>p with{PersistentSlot=p.PersistentSlot+127}).ToArray()})
Refuse(()=>UniversalMagicPairCertificate.Capture(host.Level.Key,host.Data.Bytes,host.Overlay.Bytes.ToArray(),lib.Disc.Exe,src.Data,src.Overlay,bad,null),"malformed/reused/overflow lifetime ledger refuses");
File.WriteAllText(Path.Combine(Path.GetDirectoryName(reportPath)!,"lifetime-cases.json"),JsonSerializer.Serialize(lifetimeCases,new JsonSerializerOptions{WriteIndented=true}));
File.WriteAllText(Path.Combine(Path.GetDirectoryName(reportPath)!,"parameter-cases.json"),JsonSerializer.Serialize(parameterCases,new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));

File.WriteAllText(reportPath,JsonSerializer.Serialize(new{Status="MAGIC_PAIR_SOURCE_CERTIFICATE_INTEGRATED",Checks=checks,OriginalSHA=UniversalSourceLibrary.OriginalUsaSha256,CoreSHA=H(File.ReadAllBytes(typeof(UniversalMobyComposer).Assembly.Location)),DataSHA=H(result.LevelData),OverlaySHA=H(result.Overlay),ExecutableSHA=H(result.Executable),proof,result.Classes,result.Ram,placements,result.NewRowIndices,Runtime="parent-owned r5 evidence separate",Scope="Original selector2 pair12/13, either authored root, common XYZ translations within retained neighbor grid, original native yaw, actual derived scene/pod and independent aligned bundle allocation; unchanged host/assets, no removals/mixed active imports; validated retired lifetime ledgers supported"},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"MAGIC_PAIR_CERTIFICATE_SOURCE_OK checks={checks}");

record PartFixture(IReadOnlyList<UniversalAsmItem> Items,IReadOnlyDictionary<string,IReadOnlyList<uint>> Tables,bool LenientTables);

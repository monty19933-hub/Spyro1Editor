using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;
if(args.Length!=3 || File.Exists(args[2]))throw new ArgumentException("Original USA BIN, catalog ROOT, NEW report");
UniversalSourceLibrary.RequireOriginal(args[0]);var lib=new UniversalSourceLibrary(args[0],LevelCatalog.Load(args[1]));var d=lib.Get("darkhollow");int checks=0;List<object> cases=[];
void Check(bool b,string m){if(!b)throw new Exception(m);checks++;}
void Reject(Action a,string m){try{a();}catch(InvalidDataException){checks++;return;}throw new Exception("Accepted: "+m);}
foreach(string key in new[]{"beastmakers","mistybog"})
{
 var s=lib.Get(key);Dictionary<int,int> map=new(){{0x89,509},{0x193,508},{0x1C,507}};
 UniversalAsmPart Part()=>UniversalBehaviorBundleBuilder.BuildPrunedUpdate(s.Overlay,[0x89],"test",map).Part;
 var original=Part();uint read=key=="beastmakers"?0x80081D84u:0x8007F41Cu;
 var sites=UniversalBeastMistyBoarClassOwnership.Translate(original,key,s.Overlay,[0x89],map,lib.Disc.Exe);
 Check(sites.SequenceEqual(new[]{read}),"specific scanner site");Check(original.Items.Single(i=>i.Orig==read).ClassReadOwned,"classified owned load");
 // Execute the emitted inverse conversion for every native class ID; branches
 // exercise remapped and resident targets while all other live registers stay.
 int loadAt=original.Items.FindIndex(i=>i.Orig==read);int end=original.Items.FindIndex(loadAt+1,i=>i.Orig==read+4);
 var conversion=original.Items.Skip(loadAt+1).Take(end-loadAt-1).ToArray();
 var labels=conversion.Select((i,n)=>(i,n)).Where(p=>p.i.Label!=null).ToDictionary(p=>p.i.Label!,p=>p.n);
 for(uint value=0;value<512;value++)
 {uint[] regs=Enumerable.Range(0,32).Select(n=>(uint)(0x12340000+n)).ToArray();regs[0]=0;regs[3]=value;var before=regs.ToArray();int pc=0,steps=0;
  while(pc<conversion.Length)
  {if(++steps>100)throw new Exception("conversion loop");var item=conversion[pc];if(item.Word is not uint w){pc++;continue;}
   if(w==0){pc++;continue;}
   int op=UniversalMips.Op(w),rs=UniversalMips.Rs(w),rt=UniversalMips.Rt(w);
   if(op==9){regs[rt]=unchecked((uint)((int)regs[rs]+UniversalMips.SImm(w)));pc++;}
   else if(op==5){bool taken=regs[rs]!=regs[rt];Check(conversion[pc+1].Word==0,"branch delay NOP");pc=taken?labels[item.Fix.Label!]:pc+2;}
   else if(op==2){Check(conversion[pc+1].Word==0,"jump delay NOP");pc=labels[item.Fix.Label!];}
   else throw new Exception("unexpected conversion opcode");
  }
  uint expected=map.FirstOrDefault(m=>m.Value==value).Key is int sourceId && map.ContainsKey(sourceId) && map[sourceId]==value?(uint)sourceId:value;
  Check(regs[3]==expected,"all native class inverse mappings");Check(regs.Where((v,n)=>n!=3 && v!=before[n]).Count()==0,"all other live registers preserved");
 }
 UniversalClassRemapping.PatchOwnedClassReads(original,s.Overlay,map,[0x89]);UniversalClassRemapping.RequireNoUnownedClassMemory(original,false,s.Overlay);checks++;
 foreach(var item in Part().Items.Where(i=>i.Word!=null).ToArray())
 {var p=Part();int at=p.Items.FindIndex(i=>i==item);p.Items[at]=p.Items[at] with{Word=item.Word^1};Reject(()=>UniversalBeastMistyBoarClassOwnership.Translate(p,key,s.Overlay,[0x89],map,lib.Disc.Exe),"changed emitted word");}
 foreach(string kind in new[]{"insert-use","reorder","target","duplicate","table"})
 {var p=Part();int at=p.Items.FindIndex(i=>i.Orig==read);switch(kind){case "insert-use":p.Items.Insert(at+1,new(UniversalMips.EncodeI(43,16,3,0x36),default,null,null));break;case "reorder":(p.Items[at],p.Items[at+1])=(p.Items[at+1],p.Items[at]);break;case "target":p.Items[at+2]=p.Items[at+2] with{Fix=new(UniversalFixKind.Branch,read+8)};break;case "duplicate":p.Items.Add(p.Items[at]);break;case "table":p.Tables["forged_entry"]=[read+4];break;}Reject(()=>UniversalBeastMistyBoarClassOwnership.Translate(p,key,s.Overlay,[0x89],map,lib.Disc.Exe),kind);}
 {var b=s.Overlay.Bytes.ToArray();b[(int)(read-UniversalOverlay.Base)]^=1;Reject(()=>UniversalBeastMistyBoarClassOwnership.Translate(Part(),key,new(b,s.Overlay.Bindings),[0x89],map,lib.Disc.Exe),"changed source");}
 foreach(var binding in s.Overlay.Bindings.Values){var bindings=s.Overlay.Bindings.ToDictionary(p=>p.Key,p=>p.Key==binding.Global?p.Value with{AddiuAddress=p.Value.AddiuAddress+4}:p.Value);Reject(()=>UniversalBeastMistyBoarClassOwnership.Translate(Part(),key,new(s.Overlay.Bytes.ToArray(),bindings),[0x89],map,lib.Disc.Exe),"changed binding");}
 {var b=lib.Disc.Exe.ToArray();b[0x80052058-UniversalExeBindings.ExeBase]^=1;Reject(()=>UniversalBeastMistyBoarClassOwnership.Translate(Part(),key,s.Overlay,[0x89],map,b),"changed active list producer");}
 Reject(()=>UniversalBeastMistyBoarClassOwnership.Translate(Part(),key,s.Overlay,[],map,lib.Disc.Exe),"wrong class owner");
 var raw=UniversalBehaviorBundleBuilder.Build(d.Overlay,[(key,s.Overlay,(IReadOnlyList<int>)new[]{0x89})],0x80090000,classMaps:new Dictionary<string,IReadOnlyDictionary<int,int>>{{key,map}},nativeExecutable:lib.Disc.Exe);
 Check(raw.Sources.Single().LinkedClassReadSites.Contains(read),"raw alias Bundle reports owned scan");
 Reject(()=>UniversalBehaviorBundleBuilder.Build(d.Overlay,[(key=="beastmakers"?"mistybog":"beastmakers",s.Overlay,(IReadOnlyList<int>)new[]{0x89})],0x80090000,classMaps:new Dictionary<string,IReadOnlyDictionary<int,int>>{{key=="beastmakers"?"mistybog":"beastmakers",map}},nativeExecutable:lib.Disc.Exe),"mislabeled source alias Bundle");
 Reject(()=>UniversalBeastMistyBoarClassOwnership.Translate(Part(),key,s.Overlay,[0x89],map,[]),"missing native producer owner");
 List<object> compositions=[];
 foreach(int row in Enumerable.Range(0,s.Data.Scene().RowCount).Where(i=>BinaryPrimitives.ReadUInt16LittleEndian(s.Data.Row(i).AsSpan(0x36))==0x89))
 {
  var r=s.Data.Row(row);var p=new UniversalMobyPlacement(key,row,BitConverter.ToInt32(r,12),BitConverter.ToInt32(r,16),BitConverter.ToInt32(r,20),EditorId:key+"-boar-"+row,PersistentSlot:0);
  var host=new UniversalLevelData(d.Data.Bytes.ToArray());host.AppendModel(0x89,s.Data.Package(0x89));
  var composed=UniversalMobyComposer.Compose(d.Level,host.Bytes,d.Overlay.Bytes.ToArray(),lib.Disc.Exe,lib.Get,lib.SharedClasses,UniversalMobyExport.DefaultProtectedVram(args[0],d.Level),[p],sharedModelPackage:lib.SharedModelPackage,playerAnimationSource:lib.PlayerAnimations);
  Check(composed.Classes.Single(c=>c.ClassId==0x89).DestinationClassId!=0x89,"actual occupied namespace alias");Check(composed.Behavior.Single(b=>b.SourceLevelKey==key).LinkedClassReadSites.Contains(read),"ordinary Compose scanner ownership");
  var native=UniversalMobyComposer.Compose(s.Level,s.Data.Bytes.ToArray(),s.Overlay.Bytes.ToArray(),lib.Disc.Exe,lib.Get,lib.SharedClasses,UniversalMobyExport.DefaultProtectedVram(args[0],s.Level),[p],sharedModelPackage:lib.SharedModelPackage,playerAnimationSource:lib.PlayerAnimations);
  Check(native.Classes.Single(c=>c.ClassId==0x89).DestinationClassId==0x89 && native.Classes.Single(c=>c.ClassId==0x89).BehaviorImported==false,"same-level native89 control");
  compositions.Add(new{Row=row,Classes=composed.Classes,Ram=composed.Ram,Scope="Static namespace handling only; source paths not translated or gameplay verified."});
 }
 cases.Add(new{Source=key,Read=read,Rows=compositions});
}
File.WriteAllText(args[2],JsonSerializer.Serialize(new{Marker="BEAST_MISTY89_CLASS_SCAN_OWNERSHIP_OK",Checks=checks,Cases=cases,Runtime="not-tested",Scope="Exact original89 pointer-list comparison ownership. World-path translation remains separate."},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("BEAST_MISTY89_CLASS_SCAN_OWNERSHIP_OK checks="+checks);

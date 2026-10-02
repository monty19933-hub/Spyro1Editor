using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;
if(args.Length!=3 || File.Exists(args[2]))throw new ArgumentException("Usage: UniversalFlightGateSmoke <original USA.bin> <catalog root> <NEW report.json>");
UniversalSourceLibrary.RequireOriginal(args[0]);
var lib=new UniversalSourceLibrary(args[0],LevelCatalog.Load(args[1]));string[] keys=["sunnyflight","nightflight","crystalflight","wildflight"];
int checks=0;var cells=new List<object>();
void Check(bool x,string why){if(!x)throw new Exception(why);checks++;}
void Reject(Action a,string why){try{a();}catch(Exception ex) when(ex is InvalidDataException or UniversalFlightDependencyException){checks++;return;}throw new Exception("Missing refusal:"+why);}
int Gate(string k)=>Enumerable.Range(0,lib.Get(k).Data.Scene().RowCount).First(i=>BitConverter.ToUInt16(lib.Get(k).Data.Row(i),0x36)==0x161);
int Cat(string k)=>BitConverter.ToInt32(lib.Get(k).Data.CaptureRow(k,Gate(k)).Properties,0x18);
foreach(string sk in keys)foreach(string dk in keys){
 if(Cat(sk)!=Cat(dk))continue;
 var src=lib.Get(sk);var dst=lib.Get(dk);int sr=Gate(sk),dr=Gate(dk);var tr=dst.Data.Row(dr);
 var p=new UniversalMobyPlacement(sk,sr,BitConverter.ToInt32(tr,12),BitConverter.ToInt32(tr,16),BitConverter.ToInt32(tr,20),Yaw:tr[0x46]);var removed=new HashSet<int>{dr};
 var cert=UniversalFlightGateOwnership.Capture(dk,dst.Data,dst.Overlay,dst.Data,dst.Overlay,[p],removed,lib.Get,lib.Disc.Exe)??throw new Exception("no cert");
 Check(cert.Classes.Count==15,"fifteen classes");
 var t=cert.PrepareTemplate(src.Data.CaptureRow(sk,sr));cert.RequireRow(t.Row,t.Properties,0x161);
 Check(t.Properties.AsSpan(0,16).SequenceEqual(dst.Data.CaptureRow(dk,dr).Properties.AsSpan(0,16)),"target plane");
 foreach(int o in new[]{12,16,20,0x46,0x50,0x51}){var row=t.Row.ToArray();row[o]^=1;Reject(()=>cert.RequireRow(row,t.Properties,0x161),"changed target pose/state");}
 foreach(int o in Enumerable.Range(0,t.Properties.Length)){var props=t.Properties.ToArray();props[o]^=1;Reject(()=>cert.RequireRow(t.Row,props,0x161),"changed plane/selector/category");}
 Reject(()=>UniversalFlightGateOwnership.Capture(dk,dst.Data,dst.Overlay,dst.Data,dst.Overlay,[p with{X=p.X+1}],removed,lib.Get,lib.Disc.Exe),"moved root");
 var map=cert.Classes.Select((c,i)=>(c,m:0x1FD-i)).ToDictionary(x=>x.c,x=>x.m);var maps=new Dictionary<string,IReadOnlyDictionary<int,int>>{{sk,map}};var certs=new Dictionary<string,UniversalFlightGateCertificate>{{sk,cert}};
 UniversalAsmPart Fresh()=>UniversalBehaviorBundleBuilder.BuildPrunedUpdate(src.Overlay,cert.Classes,"gate",map).Part;
 var original=Fresh();var spIndex=original.Items.FindIndex(x=>x.Word!=null&&cert.SpawnCalls.Contains(x.Orig??x.SourcePc??0)&&(x.Orig??x.SourcePc)==cert.SpawnCalls.Max());
 uint call=original.Items[spIndex].Orig??original.Items[spIndex].SourcePc??0;
 foreach(int delta in new[]{-24,-20,-16,-12,-8,-4,0,4}){var bad=Fresh();int at=bad.Items.FindIndex(x=>(x.Orig??x.SourcePc)==call+delta);bad.Items[at]=bad.Items[at] with{Word=bad.Items[at].Word^1};Reject(()=>cert.Patch(bad,src.Overlay,map),"fragment complete word witness");}
 var insertion=Fresh();int put=insertion.Items.FindIndex(x=>x.Orig==call+4);insertion.Items.Insert(put+1,new(UniversalMips.EncodeI(9,20,20,7),default,null,null));Reject(()=>cert.Patch(insertion,src.Overlay,map),"inserted loop counter consumer");
 var bound=Fresh();int boundAt=bound.Items.FindIndex(x=>x.Word==0x2A82000D);bound.Items[boundAt]=bound.Items[boundAt] with{Word=0x2A82000E};Reject(()=>cert.Patch(bound,src.Overlay,map),"loop domain14 rejected");
 var entry=Fresh();entry.Items.Add(new(UniversalMips.EncodeJ(2,0),new(UniversalFixKind.Jump,call-20),null,null));Reject(()=>cert.Patch(entry,src.Overlay,map),"new direct fragment producer entry");
 var relocated=Fresh();int branchAt=relocated.Items.FindIndex(x=>x.Orig==call-24);relocated.Items[branchAt]=relocated.Items[branchAt] with{Fix=relocated.Items[branchAt].Fix with{Orig=relocated.Items[branchAt].Fix.Orig+4}};Reject(()=>cert.Patch(relocated,src.Overlay,map),"wrong relocated fragment branch target");
 foreach(bool identity in new[]{false,true})Reject(()=>UniversalBehaviorBundleBuilder.Build(dst.Overlay,[(sk,src.Overlay,cert.Classes)],0x80190000,classMaps:new Dictionary<string,IReadOnlyDictionary<int,int>>{{sk,cert.Classes.ToDictionary(c=>c,c=>identity?c:map[c])}},nativeExecutable:lib.Disc.Exe),"raw Bundle without source authority including identity");
 foreach(var binding in src.Overlay.Bindings){var cb=src.Overlay.Bindings.ToDictionary();cb[binding.Key]=binding.Value with{Value=binding.Value.Value+4};Reject(()=>cert.RequireSource(new(src.Overlay.Bytes.ToArray(),cb)),"mutated callback owner");}
 var part=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(src.Overlay,cert.Classes,"gate",map).Part;cert.Patch(part,src.Overlay,map);Check(part.Items.Count(x=>x.Label?.StartsWith("flight_gate_fragment_")==true)==14,"issued13 branch selectors and done");
 foreach(var testedMap in new[]{map,cert.Classes.ToDictionary(c=>c,c=>c),cert.Classes.Select((c,i)=>(c,m:0x1C0+(i*7)%31)).ToDictionary(x=>x.c,x=>x.m)})
 {
  var emission=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(src.Overlay,cert.Classes,"gate-map",testedMap).Part;cert.Patch(emission,src.Overlay,testedMap);
  byte[] code=UniversalBehaviorBundleBuilder.Assemble([emission],0x80190000).Code;
  int at=emission.Items.FindIndex(x=>x.Orig==call-20);uint begin=0x80190000u+(uint)emission.Items.Take(at).Count(x=>x.Word!=null)*4;
  foreach(int i in Enumerable.Range(0,13)){var result=RunSpawn(code,begin,i);Check(result.Class==testedMap[0x176+i]&&result.Counter==i+1&&result.Parent==0x12340000,"actual emitted branch/load delay maps every fragment in arbitrary namespace");}
 }
 var bundle=UniversalBehaviorBundleBuilder.Build(dst.Overlay,[(sk,src.Overlay,cert.Classes)],0x80190000,classMaps:maps,nativeExecutable:lib.Disc.Exe,flightGates:certs);Check(bundle.Code.Length>0,"raw bundle");
 foreach(int type in new[]{1,2,70}){var issued=UniversalFlightGateParticles.Inspect(cert,type,src.Overlay,dst.Overlay,src.Data,dst.Data,lib.Disc.Exe);Check(issued.Dependency.Status.StartsWith("source-certified"),"source status");}
 bool expectsCapacity = dk=="sunnyflight" && sk!=dk;
 try{
 var composed=UniversalMobyComposer.Compose(dst.Level,dst.Data.Bytes,dst.Overlay.Bytes.ToArray(),lib.Disc.Exe,lib.Get,lib.SharedClasses,UniversalMobyExport.DefaultProtectedVram(args[0],dst.Level).ToArray(),[p],removedRows:[dr],sharedModelPackage:lib.SharedModelPackage,playerAnimationSource:lib.PlayerAnimations);
 if(expectsCapacity)throw new InvalidOperationException("Expected original Sunny memory guard did not fire.");
 cells.Add(new{sk,dk,Status="static-composed",Rows=composed.NativeRowsAfter,Classes=composed.Classes,Particle=composed.FlightGateParticleProofs,OverlayBytes=composed.Overlay.Length});Console.WriteLine(sk+"→"+dk+" composed");
 }catch(UniversalBudgetException e) when(expectsCapacity && e.Budget.FinalEnd>e.Budget.Limit && e.Budget.FinalHeadroom<0){cells.Add(new{sk,dk,Status="capacity-refused",Error=e.Message,Budget=e.Budget});Console.WriteLine(sk+"→"+dk+" "+e.Message);}
}
File.WriteAllText(args[2],JsonSerializer.Serialize(new{Evidence="source/static composition and exact emitted guards; gameplay acceptance is separate",Checks=checks,Cells=cells},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("FLIGHT_GATE_OWNERSHIP_GUARDS_OK checks="+checks);

static (uint Class,uint Counter,uint Parent) RunSpawn(byte[] code,uint start,int iteration)
{
 uint[] r=Enumerable.Repeat(0xDEADBEEFu,32).ToArray();r[0]=0;r[19]=0x12340000;r[20]=(uint)iteration;
 uint pc=start;uint? redirect=null; (int Reg,uint Value)? pending=null;bool called=false;
 for(int step=0;step<200;step++)
 {
  uint w=BitConverter.ToUInt32(code,checked((int)(pc-0x80190000)));int op=(int)(w>>26),rs=(int)(w>>21&31),rt=(int)(w>>16&31),rd=(int)(w>>11&31),fn=(int)(w&63);int si=(short)(w&65535);
  uint[] old=r.ToArray();(int Reg,uint Value)? write=null,load=null;uint? jump=null;bool call=false;
  if(w==0){}
  else if(op==9)write=(rt,unchecked(old[rs]+(uint)si));
  else if(op==15)write=(rt,(w&65535)<<16);
  else if(op==35){if(unchecked(old[rs]+(uint)si)!=0x800758CC)throw new Exception("unowned load");load=(rt,0x87654320);}
  else if(op==0&&fn==33)write=(rd,unchecked(old[rs]+old[rt]));
  else if(op==5)jump=old[rs]!=old[rt]?unchecked(pc+4+(uint)(si*4)):pc+8;
  else if(op==2)jump=((pc+4)&0xF0000000)|((w&0x03FFFFFF)<<2);
  else if(op==0&&fn==9){if(old[rs]!=0x87654320)throw new Exception("uncommitted callback load");write=(31,pc+8);call=true;}
  else throw new Exception("unsupported emitted spawn instruction:"+w.ToString("X8"));
  if(pending is {} p)r[p.Reg]=p.Value;if(write is {} wr)r[wr.Reg]=wr.Value;pending=load;r[0]=0;
  if(called)return(r[4],r[20],r[5]);
  uint next=redirect??pc+4;redirect=jump;pc=next;called=call;
 }
 throw new Exception("emitted spawn interpreter did not reach callback");
}

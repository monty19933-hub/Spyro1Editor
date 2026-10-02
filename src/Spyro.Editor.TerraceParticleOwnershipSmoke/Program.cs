using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;
if(args.Length!=3 || File.Exists(args[2]))throw new ArgumentException("Original USA BIN, catalog root, NEW report JSON");
UniversalSourceLibrary.RequireOriginal(args[0]);
var catalog=LevelCatalog.Load(args[1]);var lib=new UniversalSourceLibrary(args[0],catalog);var s=lib.Get("terracevillage");var d=lib.Get("darkhollow");byte[] exe=lib.Disc.Exe;
int checks=0;List<string> cases=[];
void Check(bool b,string m){if(!b)throw new Exception(m);checks++;}
void Reject(Action a,string m){try{a();}catch(InvalidDataException){checks++;return;}throw new Exception("Accepted: "+m);}
UniversalOverlay Mutate(UniversalOverlay o,uint pc){byte[] b=o.Bytes.ToArray();b[(int)(pc-UniversalOverlay.Base)]^=1;return new(b,o.Bindings);}
UniversalOverlay Bind(UniversalOverlay o,uint global,uint target)=>new(o.Bytes.ToArray(),o.Bindings.ToDictionary(p=>p.Key,p=>p.Key==global?p.Value with{Value=target}:p.Value));
UniversalAsmPart Part()=>UniversalBehaviorBundleBuilder.BuildPrunedUpdate(s.Overlay,[0x20,0x157,0x1D3,0x1EE],"proof").Part;
var certificate=UniversalTerraceLineParticle77.Capture(s.Level.Key,d.Level.Key,s.Overlay,d.Overlay,exe);
Check(certificate.Proof.RequestingClass==0x20 && certificate.Proof.RequestState==5,"77 egg state5");
Check(!UniversalParticleDependencies.Records(s.Data).ContainsKey(77) && !UniversalParticleDependencies.Records(d.Data).ContainsKey(77),"77 owns no texture record");
var sw=UniversalDispatchAnalysis.FindUpdateSwitch(s.Overlay);
Check(UniversalDispatchAnalysis.Trace(s.Overlay,sw.TreeStart,0x20,sw.Seeds,sw.ClassRegister).Stop==0x8007C320,"actual class20 leaf");
Check(Part().Items.Count(i=>i.Orig==0x8007C8B8)==1,"one original77 call");
foreach(int c in new[]{0x157,0x1D3,0x1EE})Check(!UniversalBehaviorBundleBuilder.BuildPrunedUpdate(s.Overlay,[c],"single").Part.Items.Any(i=>i.Orig==0x8007C8B8),"77 is not the attack path class"+c);
UniversalTerraceLineParticle77.RequireCopiedCall(Part(),s.Overlay);checks++;
cases.Add("Exact source/destination ownership, no texture77, class20 state5 distinct from all three enemy/dependency handlers.");
foreach(var owner in new[]{(s.Overlay,0x800866D8u,4716),(s.Overlay,0x800853ACu,4908),(s.Overlay,0x8007B454u,324),(s.Overlay,0x8007B310u,324),(d.Overlay,0x800844A0u,4340),(d.Overlay,0x80083274u,4652),(d.Overlay,0x8007ACB8u,324),(d.Overlay,0x8007AB74u,324)})
 for(uint pc=owner.Item2;pc<owner.Item2+owner.Item3;pc+=4)
  Reject(()=>UniversalTerraceLineParticle77.Capture("terracevillage","darkhollow",owner.Item1==s.Overlay?Mutate(s.Overlay,pc):s.Overlay,owner.Item1==d.Overlay?Mutate(d.Overlay,pc):d.Overlay,exe),"function/table word "+pc.ToString("X"));
cases.Add("Every source/destination constructor/update/primary-table word mutation refuses.");
foreach(uint global in new[]{UniversalExeBindings.UpdateGlobal,UniversalExeBindings.InitGlobal,UniversalExeBindings.CopyBufferGlobal,UniversalExeBindings.ParticleSpawnGlobal,UniversalExeBindings.ParticleUpdateGlobal})
 Reject(()=>UniversalTerraceLineParticle77.Capture("terracevillage","darkhollow",Bind(s.Overlay,global,s.Overlay.Bindings[global].Value+4),d.Overlay,exe),"source callback");
foreach(uint global in new[]{UniversalExeBindings.ParticleSpawnGlobal,UniversalExeBindings.ParticleUpdateGlobal})
 Reject(()=>UniversalTerraceLineParticle77.Capture("terracevillage","darkhollow",s.Overlay,Bind(d.Overlay,global,d.Overlay.Bindings[global].Value+4),exe),"destination callback");
Reject(()=>UniversalTerraceLineParticle77.Capture("stonehill","darkhollow",s.Overlay,d.Overlay,exe),"mislabeled source");Reject(()=>UniversalTerraceLineParticle77.Capture("terracevillage","stonehill",s.Overlay,d.Overlay,exe),"mislabeled destination");
foreach(uint pc in new[]{0x800169ACu,0x80017110,0x8001778C,0x80017BFC,0x80053570,0x80053608,0x800573C8,0x80058000,0x8006272C,0x8006CBF8}){var b=exe.ToArray();b[(int)(pc-UniversalExeBindings.ExeBase)]^=1;Reject(()=>UniversalTerraceLineParticle77.Capture("terracevillage","darkhollow",s.Overlay,d.Overlay,b),"native callee/renderer");}
foreach(var sourceOwner in new[]{s.Overlay,d.Overlay})foreach(var binding in sourceOwner.Bindings.Values.Where(b=>sourceOwner==s.Overlay || b.Global is UniversalExeBindings.ParticleSpawnGlobal or UniversalExeBindings.ParticleUpdateGlobal))
 foreach(string field in new[]{"Global","LuiAddress","AddiuAddress"})
 {var bindings=sourceOwner.Bindings.ToDictionary(p=>p.Key,p=>p.Key!=binding.Global?p.Value:field=="Global"?p.Value with{Global=p.Value.Global+4}:field=="LuiAddress"?p.Value with{LuiAddress=p.Value.LuiAddress+4}:p.Value with{AddiuAddress=p.Value.AddiuAddress+4});var changed=new UniversalOverlay(sourceOwner.Bytes.ToArray(),bindings);Reject(()=>UniversalTerraceLineParticle77.Capture("terracevillage","darkhollow",sourceOwner==s.Overlay?changed:s.Overlay,sourceOwner==d.Overlay?changed:d.Overlay,exe),"callback metadata");}
{var b=exe.ToArray();b[0x8005A87C-UniversalExeBindings.ExeBase]^=1;Reject(()=>UniversalTerraceLineParticle77.Capture("terracevillage","darkhollow",s.Overlay,d.Overlay,b),"actual executable particle binding");}
cases.Add("Callback identity, mislabeled levels and native allocator/vector/trig/RNG/renderer mutations refuse.");
foreach(uint pc in Enumerable.Range(0,(int)((0x8007C928u-0x8007C87Cu)/4)+1).Select(n=>0x8007C87Cu+(uint)n*4).Concat(Enumerable.Range(0,12).Select(n=>0x8007C3CCu+(uint)n*4)))
 {var p=Part();int at=p.Items.FindIndex(i=>i.Orig==pc);p.Items[at]=p.Items[at] with{Word=p.Items[at].Word^1};Reject(()=>UniversalTerraceLineParticle77.RequireCopiedCall(p,s.Overlay),"caller word");}
foreach(string kind in new[]{"reorder","insert-use","insert-nop","wrong-target","partial-duplicate","state-table","incoming","label-incoming","table-incoming","first-incoming"})
 {var p=Part();int at=p.Items.FindIndex(i=>i.Orig==0x8007C8B8);
  switch(kind){
   case "reorder":(p.Items[at-2],p.Items[at-1])=(p.Items[at-1],p.Items[at-2]);break;
   case "insert-use":p.Items.Insert(at,new(UniversalMips.EncodeI(9,0,5,12),default,null,null));break;
   case "insert-nop":p.Items.Insert(at,new(0,default,null,null));break;
   case "wrong-target":int end=p.Items.FindIndex(i=>i.Orig==0x8007C924);p.Items[end]=p.Items[end] with{Fix=new(UniversalFixKind.Jump,0x80084610)};break;
   case "partial-duplicate":p.Items.Add(p.Items[at]);break;
   case "state-table":string key=p.Tables.Keys.Single(k=>k.EndsWith("8007C3F4"));var t=p.Tables[key].ToArray();t[4]+=4;p.Tables[key]=t;break;
   case "incoming":p.Items.Add(new(UniversalMips.EncodeJ(2,0),new(UniversalFixKind.Jump,0x8007C8A4),0x8007C100,null));break;
   case "table-incoming":p.Tables["forged_table"]=[0x8007C8A4];break;
   case "first-incoming":p.Items.Add(new(UniversalMips.EncodeJ(2,0),new(UniversalFixKind.Jump,0x8007C87C),0x8007C100,null));break;
   case "label-incoming":p.Items.Insert(at,new(null,default,null,"bad_interior"));p.Items.Add(new(UniversalMips.EncodeJ(2,0),new(UniversalFixKind.JumpLabel,Label:"bad_interior"),null,null));break;
  }Reject(()=>UniversalTerraceLineParticle77.RequireCopiedCall(p,s.Overlay),kind);
 }
// Full duplicates are independently checked, not rejected just for being duplicated.
{var p=Part();var packet=p.Items.Where(i=>i.Orig is >=0x8007C87C and <=0x8007C928).ToArray();p.Items.AddRange(packet);UniversalTerraceLineParticle77.RequireCopiedCall(p,s.Overlay);checks++;p.Items[^2]=p.Items[^2] with{Word=0};Reject(()=>UniversalTerraceLineParticle77.RequireCopiedCall(p,s.Overlay),"second duplicate changed");}
cases.Add("Complete emitted packet, state table, duplicate, order, argument, delayed-call, fix-target and interior-entry guards.");
var raw=UniversalBehaviorBundleBuilder.Build(d.Overlay,[("terracevillage",s.Overlay,(IReadOnlyList<int>)new[]{0x20,0x157,0x1D3,0x1EE})],0x80090000,nativeExecutable:exe);
Check(raw.OwnedParticleUpdateWrapper==null,"raw Bundle retains native particle updater");
Reject(()=>UniversalBehaviorBundleBuilder.Build(d.Overlay,[("terracevillage",Mutate(s.Overlay,0x8007C8B8),(IReadOnlyList<int>)new[]{0x20})],0x80090000,nativeExecutable:exe),"raw copied Bundle changed caller");
Reject(()=>UniversalTerraceLineParticle77.Capture("terracevillage","darkhollow",s.Overlay,d.Overlay,[]),"missing EXE owner");
Check(UniversalParticleDependencies.Inspect("terracevillage",77,s.Data,s.Data,true).Status=="native","same-level reporting unchanged");
cases.Add("Direct raw copied Bundle validates caller/native ownership without Composer; same-level native control remains unchanged.");
var placement=new UniversalMobyPlacement("terracevillage",0,16945,60751,17157,Yaw:158,EditorId:"terrace77-source-proof",PersistentSlot:0);
var result=UniversalMobyComposer.Compose(d.Level,d.Data.Bytes.ToArray(),d.Overlay.Bytes.ToArray(),exe,lib.Get,lib.SharedClasses,UniversalMobyExport.DefaultProtectedVram(args[0],d.Level),[placement],sharedModelPackage:lib.SharedModelPackage,playerAnimationSource:lib.PlayerAnimations);
Check(result.NewRowIndices.SequenceEqual(new[]{128}) && result.NativeRowsBefore==128 && result.NativeRowsAfter==129,"native128 and append128");
Check(result.NativeLineParticleProofs.Count==1 && result.ParticleDependencies.Single(p=>p.TypeId==77).Status=="source-certified-native-line","ordinary compositor issues77");
Check(result.ParticleDependencies.Single(p=>p.TypeId==12).Status=="dependency-unverified","type12 remains separate unverified");
var finalOverlay=new UniversalOverlay(result.Overlay,UniversalExeBindings.Read(result.Executable,12));certificate.RequireDestination(finalOverlay,result.Executable);checks++;
Check(finalOverlay.Bindings[UniversalExeBindings.ParticleSpawnGlobal].Value==0x800844A0 && finalOverlay.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value==0x80083274,"no added dispatcher or doubletick");
for(int n=0;n<128;n++)Check(new UniversalLevelData(result.LevelData).Row(n).SequenceEqual(d.Data.Row(n)),"original native row "+n);
cases.Add("Ordinary compositor and final callbacks:128 native rows preserved, physical128, existing77 certified,12 unverified, no update chain.");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
File.WriteAllText(args[2],JsonSerializer.Serialize(new{Marker="TERRACE_NATIVE_LINE77_OWNERSHIP_OK",Checks=checks,Proof=certificate.Proof,Cases=cases,Dependencies=result.ParticleDependencies,Classes=result.Classes,Runtime="not-tested;77 is egg completion, not an enemy attack",CoreSha256=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(UniversalMobyComposer).Assembly.Location)))},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("TERRACE_NATIVE_LINE77_OWNERSHIP_OK checks="+checks);

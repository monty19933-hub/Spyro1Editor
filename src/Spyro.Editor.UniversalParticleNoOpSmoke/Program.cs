using System.Buffers.Binary;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Levels;
int checks=0;void Check(bool ok,string label){checks++;if(!ok)throw new InvalidOperationException(label);}
void Refuse(Action a,string label){try{a();throw new InvalidOperationException(label+" accepted");}catch(InvalidDataException){checks++;}}
uint read=0x80080100;var words=Enumerable.Range(-19,29).ToDictionary(i=>(uint)((long)read+i*4),i=>0u);var included=words.Keys.ToHashSet();
void Put(int d,uint w)=>words[(uint)((long)read+d)]=w;
Put(-76,0x0C00EAF0);Put(-72,0xA2620052);Put(-68,0xAE220000);Put(-32,0x8E220000);Put(-28,0x24040008);Put(-16,0x24050040);Put(-8,0x8E220000);Put(-4,0x2666000C);Put(0,0x84470036);Put(4,0x3C028007);Put(8,0x8C4258E4);Put(16,0x0040F809);Put(20,0x24E7FFAD);Put(24,0x02602021);Put(28,0x00002821);Put(32,0x0C00E147);Put(36,0x00003021);
bool Prove(IReadOnlySet<uint>? ins=null,IReadOnlyList<uint>? targets=null)=>UniversalNativeParticleNoOpOwnership.ProveCaller(pc=>words[pc],ins??included,targets??[],read);
Check(Prove(),"complete native-shaped linked actor packet");
foreach(uint pc in included){var fewer=included.Where(p=>p!=pc).ToHashSet();Check(!Prove(fewer),"every native producer/delay/consumer is included");}
foreach(uint pc in included){Check(!Prove(targets:[pc]),"alternate basic-block entry refused");}
foreach(int d in new[]{-76,-72,-68,-28,-16,-8,-4,0,4,8,12,16,20,24,28,32,36}){uint old=words[(uint)((long)read+d)];Put(d,old^1);Check(!Prove(),"changed native producer/consumer refused");Put(d,old);}
Put(-40,0x24010001);Check(Prove(),"unrelated temporary preserves exact owner");Put(-40,0x24110001);Check(!Prove(),"linked property pointer clobber refused");Put(-40,0);
if(args.Length==0){Console.WriteLine("UNIVERSAL_PARTICLE_NOOP_SOURCE_FREE_OK checks="+checks);return;}
if(args.Length is not (3 or 4))throw new ArgumentException("original-USA catalog-workspace artifacts [mixed-export-bin]");
Directory.CreateDirectory(args[2]);UniversalSourceLibrary.RequireOriginal(args[0]);var catalog=LevelCatalog.Load(args[1]);var lib=new UniversalSourceLibrary(args[0],catalog);var art=lib.Get("artisans");int sites=0;
foreach(var level in catalog.Levels.Where(l=>l.LevelId>=10&&l.LevelId<=64))
{
 var o=lib.Get(level.Key).Overlay;var sw=UniversalDispatchAnalysis.FindUpdateSwitch(o);if(!UniversalDispatchAnalysis.Handles(o,sw,0x149))continue;
 var (part,_,_)=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(o,[0x149],"probe");var before=part.Items.ToArray();
 var proof=UniversalNativeParticleNoOpOwnership.MarkOwned(part,o,art.Overlay,lib.Disc.Exe);Check(proof.Count==1,"exact native source packet owned "+level.Key);sites++;
 Check(part.Items.Select(i=>i.Word).SequenceEqual(before.Select(i=>i.Word)) && part.Items.Count==before.Length,"zero-byte marker preserves all native words "+level.Key);
 UniversalNativeParticleNoOpOwnership.RequireFinalDestination(proof,art.Overlay,lib.Disc.Exe);
 var p=proof.Single();var altered=art.Overlay.Bytes.ToArray();BinaryPrimitives.WriteUInt32LittleEndian(altered.AsSpan((int)(p.TableAddress+64*4-UniversalOverlay.Base)),art.Overlay.Word(p.TableAddress));
 Refuse(()=>UniversalNativeParticleNoOpOwnership.RequireFinalDestination(proof,new UniversalOverlay(altered,art.Overlay.Bindings),lib.Disc.Exe),"changed final type64 table owner");
 altered=art.Overlay.Bytes.ToArray();altered[(int)(p.CallbackPc-UniversalOverlay.Base)+4]^=1;
 Refuse(()=>UniversalNativeParticleNoOpOwnership.RequireFinalDestination(proof,new UniversalOverlay(altered,art.Overlay.Bindings),lib.Disc.Exe),"changed final callback code");
 var bindings=art.Overlay.Bindings.ToDictionary();bindings[UniversalExeBindings.ParticleSpawnGlobal]=bindings[UniversalExeBindings.ParticleSpawnGlobal] with{Value=p.CallbackPc+4};
 Refuse(()=>UniversalNativeParticleNoOpOwnership.RequireFinalDestination(proof,new UniversalOverlay(art.Overlay.Bytes.ToArray(),bindings),lib.Disc.Exe),"changed final particle binding");
 var badExe=lib.Disc.Exe.ToArray();badExe[(int)(0x80038530-UniversalExeBindings.ExeBase)]^=1;
 Refuse(()=>UniversalNativeParticleNoOpOwnership.RequireFinalDestination(proof,art.Overlay,badExe),"changed next-callee kill");
 var (joined,_,_)=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(o,[0x149],"joined");joined.Tables["alternate"]=[p.SourceReadPc+4];
 Refuse(()=>UniversalNativeParticleNoOpOwnership.MarkOwned(joined,o,art.Overlay,lib.Disc.Exe),"incoming jump-table entry at load delay");
 var (duplicate,_,_)=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(o,[0x149],"duplicate");
 foreach(uint witness in new[]{p.SourceReadPc-16,p.SourceReadPc+8})
 {
  var native=duplicate.Items.First(i=>(i.SourcePc??i.Orig)==witness);duplicate.Items.Add(native with{Word=native.Word^1});
  Refuse(()=>UniversalNativeParticleNoOpOwnership.MarkOwned(duplicate,o,art.Overlay,lib.Disc.Exe),"wrong duplicate witness cannot borrow correct copy");duplicate.Items.RemoveAt(duplicate.Items.Count-1);
 }
 var nativeRead=duplicate.Items.First(i=>(i.SourcePc??i.Orig)==p.SourceReadPc);duplicate.Items.Add(nativeRead with{Orig=null,SourcePc=p.SourceReadPc});
 var duplicateProof=UniversalNativeParticleNoOpOwnership.MarkOwned(duplicate,o,art.Overlay,lib.Disc.Exe);
 Check(duplicateProof.Count==1 && duplicate.Items.Where(i=>(i.SourcePc??i.Orig)==p.SourceReadPc).All(i=>i.ClassReadOwned),"all unchanged read copies are owned consistently");

}
Check(sites==24,"all24 original source families");
foreach(var level in catalog.Levels.Where(l=>l.LevelId>=10&&l.LevelId<=64))
{
 var owner=lib.Get(level.Key).Overlay;var baseline=NativeNoOpScalar.Execute(owner,unchecked((uint)-83));
 for(int actualClass=0;actualClass<512;actualClass++)
 {
  var output=NativeNoOpScalar.Execute(owner,unchecked((uint)(actualClass-83)));
  Check(output.Where((_,i)=>i!=7).SequenceEqual(baseline.Where((_,i)=>i!=7)),"native type64 cannot propagate class argument to outputs "+level.Key);
 }
}
Refuse(()=>NativeNoOpScalar.Execute(art.Overlay,0,0),"actual allocating particle type is a native negative control");

if(args.Length==4){var actual=UniversalDiscImage.Read(args[3]);foreach(int id in new[]{10,65}){int entry=id==65?79:catalog.Levels.Single(l=>l.LevelId==id).SourceWadEntry-1;var o=new UniversalOverlay(actual.EntryBytes(entry),UniversalExeBindings.Read(actual.Exe,id));var s=lib.Get("wizardpeak").Overlay;var(part,_,_)=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(s,[0x149],"mixed");var proof=UniversalNativeParticleNoOpOwnership.MarkOwned(part,s,o,actual.Exe);Check(proof.Count==1,"actual mixed image final callback proof "+id);UniversalNativeParticleNoOpOwnership.RequireFinalDestination(proof,o,actual.Exe);}}
File.WriteAllText(Path.Combine(args[2],"result.json"),System.Text.Json.JsonSerializer.Serialize(new{checks,sites,Scope="native ownership/readback only",RuntimeClaim=false}));Console.WriteLine("UNIVERSAL_PARTICLE_NOOP_SOURCE_OK checks="+checks);

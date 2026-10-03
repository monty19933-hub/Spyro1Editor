using System.Buffers.Binary;using System.Security.Cryptography;using System.Text.Json;using Spyro.Editor.Core.Levels;using Spyro.Editor.Core.Universal;
var outPath=Path.GetFullPath(args[0]);Directory.CreateDirectory(outPath);UniversalSourceLibrary.RequireOriginal(args[1]);var lib=new UniversalSourceLibrary(args[1],LevelCatalog.Load(args[2]));var source=lib.Get("alpineridge");var host=lib.Get("darkhollow");int checks=0;
void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}void Refuse(Action a,string why){checks++;try{a();}catch(InvalidDataException){return;}throw new Exception("Accepted "+why);}
UniversalAlpineArmWorldCalls.RequireHost(host.Level.Key,host.Data,host.Overlay,lib.Disc.Exe);Check(true,"original retained host");
foreach(int at in new[]{0,host.Data.Bytes.Length/2,host.Data.Bytes.Length-1}){var b=host.Data.Bytes.ToArray();b[at]^=1;Refuse(()=>UniversalAlpineArmWorldCalls.RequireHost(host.Level.Key,new(b),host.Overlay,lib.Disc.Exe),"edited host geometry/objects/assets");}
var blowhard=lib.Get("blowhard");
UniversalAlpineArmDeathSound.Require(source.Data,source.Overlay,lib.Disc.Exe,blowhard.Data,blowhard.Data);checks++;
UniversalAlpineArmPoofOwners.Require(source.Data,source.Overlay,lib.Disc.Exe,blowhard.Data,blowhard.Overlay,blowhard.Data);checks++;
var poofTile=UniversalParticleDependencies.Records(blowhard.Data)[0];int page=BinaryPrimitives.ReadUInt16LittleEndian(poofTile.AsSpan(6)),clut=BinaryPrimitives.ReadUInt16LittleEndian(poofTile.AsSpan(2));int pixel=(((page&16)!=0?256:0)+Math.Min(poofTile[1],poofTile[5]))*1024+(page&15)*128-1024+Math.Min(poofTile[0],poofTile[4])/2,palette=(clut>>6)*1024+(clut&63)*32-1024;
foreach(int at in new[]{pixel,palette}){var edited=new UniversalLevelData(blowhard.Data.Bytes.ToArray());edited.Vram()[at]^=1;Refuse(()=>UniversalAlpineArmPoofOwners.Require(source.Data,source.Overlay,lib.Disc.Exe,blowhard.Data,blowhard.Overlay,edited),"written poof quad texture0 pixels/palette");}
var descriptorEdit=blowhard.Data.Bytes.ToArray();int particleCursor=blowhard.Data.Subfiles()[1].Offset;for(int i=0;i<6;i++)particleCursor+=blowhard.Data.U32(particleCursor);int portals=blowhard.Data.U32(particleCursor);particleCursor+=4;for(int i=0;i<portals;i++){int points=blowhard.Data.U32(particleCursor+4);particleCursor+=0x40+(points-1)*12;particleCursor+=blowhard.Data.U32(particleCursor);}int records=blowhard.Data.U32(particleCursor+4);particleCursor+=8;for(int i=0;i<records;i++){int type=BinaryPrimitives.ReadUInt16LittleEndian(descriptorEdit.AsSpan(particleCursor));if(type==0)break;particleCursor+=4+BinaryPrimitives.ReadUInt16LittleEndian(descriptorEdit.AsSpan(particleCursor+2));}Check(descriptorEdit.AsSpan(particleCursor+4,8).SequenceEqual(poofTile),"retained actual particle record0 payload located independently of repeated portal tiles");descriptorEdit[particleCursor+8]^=1;Refuse(()=>UniversalAlpineArmPoofOwners.Require(source.Data,source.Overlay,lib.Disc.Exe,blowhard.Data,blowhard.Overlay,new(descriptorEdit)),"written poof quad texture0 descriptor");
foreach(uint pc in new uint[]{0x8007AC5C+44,0x8007AC5C+288,0x80084F58,0x8008590C,0x80085EE8}){var mutated=blowhard.Overlay.Bytes.ToArray();mutated[(int)(pc-UniversalOverlay.Base)]^=1;Refuse(()=>UniversalAlpineArmPoofOwners.Require(source.Data,source.Overlay,lib.Disc.Exe,blowhard.Data,new(mutated,blowhard.Overlay.Bindings),blowhard.Data),"retained particle byte0 table/body/free-tail changed");}

var sounds=new Spyro.Editor.Core.Exporting.NativeLevelSoundLayout(blowhard.Data.Bytes);var death=sounds.Sound.Descriptors[sounds.Sound.Map[0x26]];
foreach(int at in new[]{sounds.SoundOffset+4+0x26,sounds.SoundOffset+0x108+death.Index*20+10,sounds.BankOffset+death.Sample.BankOffset+2}){var changed=blowhard.Data.Bytes.ToArray();changed[at]^=1;Refuse(()=>UniversalAlpineArmDeathSound.Require(source.Data,source.Overlay,lib.Disc.Exe,blowhard.Data,new(changed)),"written death sound selector/control/ADPCM owner mutation");}
foreach(int slots in new[]{1,16,32}){
 var grown=UniversalAlpineArmArenaStorage.Reserve(blowhard.Data,source.Data,source.Overlay,lib.Disc.Exe,slots,3);
 Check(grown.Scene().RowCount==87&&UniversalMobyComposer.ArenaSlots(grown)==91+slots,"additional dynamic storage retains original reserve and native row identities");
 foreach(int row in Enumerable.Range(0,87)){Check(BinaryPrimitives.ReadUInt16LittleEndian(grown.Row(row).AsSpan(54))==BinaryPrimitives.ReadUInt16LittleEndian(blowhard.Data.Row(row).AsSpan(54)),"all native classes retained");}
 UniversalAlpineArmArenaStorage.RequireWritten(blowhard.Data,source.Data,source.Overlay,lib.Disc.Exe,slots,3,grown);checks++;
 foreach(int at in new[]{0,grown.Subfiles()[3].Offset+grown.Scene().RowsOffset,grown.Subfiles()[3].Offset+grown.Scene().PropertiesComponent,grown.Bytes.Length-1}){var forged=grown.Bytes.ToArray();forged[at]^=1;Refuse(()=>UniversalAlpineArmArenaStorage.RequireWritten(blowhard.Data,source.Data,source.Overlay,lib.Disc.Exe,slots,3,new(forged)),"written native identity/property/fixup/geometry region mutation");}
 File.WriteAllBytes(Path.Combine(outPath,$"blowhard-reserve-{slots}.bin"),grown.Bytes);
}
foreach(int slots in new[]{0,-1,75,100})Refuse(()=>UniversalAlpineArmArenaStorage.Reserve(blowhard.Data,source.Data,source.Overlay,lib.Disc.Exe,slots,3),"invalid/inadequate cache namespace arena size");
foreach(int at in new[]{0,blowhard.Data.Subfiles()[3].Offset+blowhard.Data.Scene().RowsOffset,blowhard.Data.Bytes.Length-1}){var changed=blowhard.Data.Bytes.ToArray();changed[at]^=1;Refuse(()=>UniversalAlpineArmArenaStorage.Reserve(new(changed),source.Data,source.Overlay,lib.Disc.Exe,16,3),"modified native prefix/geometry/source assets cannot inherit reserve authority");}
var cases=new List<object>();foreach(bool aliases in new[]{false,true})
{
 var map=new Dictionary<int,int>{{0xFD,aliases?501:0xFD},{0x10F,aliases?502:0x10F},{0x13B,aliases?503:0x13B}};
 var parts=UniversalAlpineArmWorldCalls.Build(source.Data,source.Overlay,lib.Disc.Exe,map);UniversalAlpineArmPoofOwners.RequireCalls(parts.Update,source.Data,source.Overlay,lib.Disc.Exe,map);checks++;
 foreach(uint poofPc in new uint[]{0x8007D914,0x80082B20}){foreach(uint pc in new[]{poofPc-4,poofPc,poofPc+4}){int i=parts.Update.Items.FindIndex(x=>(x.SourcePc??x.Orig)==pc);var old=parts.Update.Items[i];parts.Update.Items[i]=old with{Word=old.Word!.Value^1};Refuse(()=>UniversalAlpineArmPoofOwners.RequireCalls(parts.Update,source.Data,source.Overlay,lib.Disc.Exe,map),"copied poof caller/actor/second argument/delay");parts.Update.Items[i]=old;}parts.Update.Emit(UniversalMips.EncodeJ(2,poofPc+4));Refuse(()=>UniversalAlpineArmPoofOwners.RequireCalls(parts.Update,source.Data,source.Overlay,lib.Disc.Exe,map),"outside incoming jump into poof argument delay");parts.Update.Items.RemoveAt(parts.Update.Items.Count-1);}
 UniversalAlpineArmWorldCalls.RequireCopied(source.Data,source.Overlay,lib.Disc.Exe,map,parts.Update,parts.Constructors,parts.World);checks++;
 foreach(var call in UniversalAlpineArmWorldCalls.Calls)
 {
  int callAt=parts.Update.Items.FindIndex(i=>(i.SourcePc??i.Orig)==call.Pc);Check(parts.Update.Items[callAt].Fix.Label==UniversalAlpineArmWorldCalls.Wrapper(call.Target),"all17 actual callers bound to exact stack ABI");
  for(int at=Math.Max(0,callAt-8);at<=Math.Min(parts.Update.Items.Count-1,callAt+2);at++)
  {
   var old=parts.Update.Items[at];if(old.Word is uint word){parts.Update.Items[at]=old with{Word=word^1};Refuse(()=>UniversalAlpineArmWorldCalls.RequireCopied(source.Data,source.Overlay,lib.Disc.Exe,map,parts.Update,parts.Constructors,parts.World),"caller argument/predecessor/delay byte");parts.Update.Items[at]=old;}
  }
  var original=parts.Update.Items[callAt];parts.Update.Items[callAt]=original with{SourcePc=call.Pc+4};Refuse(()=>UniversalAlpineArmWorldCalls.RequireCopied(source.Data,source.Overlay,lib.Disc.Exe,map,parts.Update,parts.Constructors,parts.World),"unchanged instruction with forged source identity");parts.Update.Items[callAt]=original;
  parts.Update.Emit(UniversalMips.EncodeJ(2,call.Pc+4));Refuse(()=>UniversalAlpineArmWorldCalls.RequireCopied(source.Data,source.Overlay,lib.Disc.Exe,map,parts.Update,parts.Constructors,parts.World),"new outside branch into source-owned delay/argument interior");parts.Update.Items.RemoveAt(parts.Update.Items.Count-1);
 }
 foreach(uint pc in UniversalAlpineArmBoneBoundary.Calls)
 {
  int at=parts.Update.Items.FindIndex(i=>(i.SourcePc??i.Orig)==pc);Check(parts.Update.Items[at].Fix.Label==UniversalAlpineArmBoneBoundary.Label,"only direct actorXYZ bone call is guarded");
  foreach(uint q in new[]{pc-8,pc-4,pc,pc+4})
  {int k=parts.Update.Items.FindIndex(i=>(i.SourcePc??i.Orig)==q);var old=parts.Update.Items[k];parts.Update.Items[k]=old with{Word=old.Word!.Value^1};Refuse(()=>UniversalAlpineArmWorldCalls.RequireCopied(source.Data,source.Overlay,lib.Disc.Exe,map,parts.Update,parts.Constructors,parts.World),"linked bone parent/bone0/output/delay packet mutation");parts.Update.Items[k]=old;}
  var badSource=source.Overlay.Bytes.ToArray();badSource[checked((int)(pc+4-UniversalOverlay.Base))]^=1;Refuse(()=>UniversalAlpineArmWorldCalls.Build(source.Data,new(badSource,source.Overlay.Bindings),lib.Disc.Exe,map),"original linked bone output mutation");
  parts.Update.Emit(UniversalMips.EncodeJ(2,pc+4));Refuse(()=>UniversalAlpineArmWorldCalls.RequireCopied(source.Data,source.Overlay,lib.Disc.Exe,map,parts.Update,parts.Constructors,parts.World),"foreign branch into linked bone argument packet");parts.Update.Items.RemoveAt(parts.Update.Items.Count-1);
 }
 int temp=parts.Update.Items.FindIndex(i=>(i.SourcePc??i.Orig)==0x800860E4);Check(parts.Update.Items[temp].Fix.Kind==UniversalFixKind.None,"native steering vector output remains native");
 bool inBone=false;for(int at=0;at<parts.World.Items.Count;at++){var old=parts.World.Items[at];if(old.Label==UniversalAlpineArmBoneBoundary.Label)inBone=true;if(inBone&&old.Label==UniversalAlpineArmMovementBoundary.EntryLabel(0x80039398))inBone=false;if(inBone&&old.Word is uint w){parts.World.Items[at]=old with{Word=w^1};Refuse(()=>UniversalAlpineArmWorldCalls.RequireCopied(source.Data,source.Overlay,lib.Disc.Exe,map,parts.Update,parts.Constructors,parts.World),"every linked bone backstop opcode");parts.World.Items[at]=old;}}
 foreach(uint pc in new uint[]{0x800831B0,0x800831EC,0x800831F4,0x80083204,0x80083234,0x80083240,0x80083250,0x80083254,0x80083264,0x8008326C,0x80083278,0x80083284,0x80083290,0x80083298,0x8008329C})
 {
  int at=parts.Update.Items.FindIndex(i=>(i.SourcePc??i.Orig)==pc);Check(at>=0,"all actual FD forced-player-response packet words retained");var old=parts.Update.Items[at];parts.Update.Items[at]=old with{Word=old.Word!.Value^1};Refuse(()=>UniversalAlpineArmWorldCalls.RequireCopied(source.Data,source.Overlay,lib.Disc.Exe,map,parts.Update,parts.Constructors,parts.World),"FD vector/control/vertical/cooldown player effect producer mutation");parts.Update.Items[at]=old;
  parts.Update.Emit(UniversalMips.EncodeJ(2,pc+4));Refuse(()=>UniversalAlpineArmWorldCalls.RequireCopied(source.Data,source.Overlay,lib.Disc.Exe,map,parts.Update,parts.Constructors,parts.World),"foreign FD response interior branch");parts.Update.Items.RemoveAt(parts.Update.Items.Count-1);
 }
 foreach(uint native in new uint[]{0x80012CB0,0x800171FC,0x80017218,0x800175BC,0x800175E8,0x8003FF20,0x8003FF60,0x80040118,0x80052758,0x800529C0,0x800522C0,0x80052D64,0x80052F34})
 {var bad=lib.Disc.Exe.ToArray();bad[checked((int)(native-0x8000F800))]^=1;Refuse(()=>UniversalAlpineArmWorldCalls.Build(source.Data,source.Overlay,bad,map),"whole actual native divisor/GTE/response/matrix/frame/bone owner mutation");}
 // Every wrapper instruction and guard insertion is part of the exact issuer.
 for(int at=0;at<parts.World.Items.Count;at++)
 {
  var old=parts.World.Items[at];if(old.Word is uint word&&(at<260||old.Fix.Kind!=UniversalFixKind.None))
  {parts.World.Items[at]=old with{Word=word^1};Refuse(()=>UniversalAlpineArmWorldCalls.RequireCopied(source.Data,source.Overlay,lib.Disc.Exe,map,parts.Update,parts.Constructors,parts.World),"wrapper/guard/path/cache allocation machine mutation");parts.World.Items[at]=old;}
 }
 foreach(uint baseAddress in new uint[]{0x800A0000,0x800B8000})
 {
  var (code,labels)=UniversalBehaviorBundleBuilder.Assemble(new[]{parts.Update}.Concat(parts.Constructors).Append(parts.World).ToList(),baseAddress);UniversalAlpineArmWorldCalls.RequireWritten(source.Data,source.Overlay,lib.Disc.Exe,map,code,labels,baseAddress);checks++;
  foreach(var c in UniversalAlpineArmWorldCalls.Calls){int at=checked((int)(labels[UniversalAlpineArmWorldCalls.Caller(c.Pc)]-baseAddress));var bad=code.ToArray();bad[at]^=1;Refuse(()=>UniversalAlpineArmWorldCalls.RequireWritten(source.Data,source.Overlay,lib.Disc.Exe,map,bad,labels,baseAddress),"actual emitted caller relocation");}
  foreach(string label in labels.Keys.Where(k=>k.Contains("linked_bone")||k.Contains("world_wrap")||k.Contains("shadow")||k.Contains("abort")||k.EndsWith("8003A068")))
  {var changed=labels.ToDictionary(p=>p.Key,p=>p.Value);changed[label]+=4;Refuse(()=>UniversalAlpineArmWorldCalls.RequireWritten(source.Data,source.Overlay,lib.Disc.Exe,map,code,changed,baseAddress),"entry/shadow/guard/native inner call label relocation");}
  var extended=code.Concat(BitConverter.GetBytes(labels[UniversalAlpineArmWorldCalls.Wrapper(0x80039398)]+4)).ToArray();Refuse(()=>UniversalAlpineArmWorldCalls.RequireWritten(source.Data,source.Overlay,lib.Disc.Exe,map,extended,labels,baseAddress),"outside table pointer to wrapper interior");
  string name=$"owned-world-{aliases}-{baseAddress:X8}";File.WriteAllBytes(Path.Combine(outPath,name+".bin"),code);File.WriteAllText(Path.Combine(outPath,name+".json"),JsonSerializer.Serialize(new{Base=baseAddress,Labels=labels,Items=new[]{parts.Update}.Concat(parts.Constructors).Append(parts.World).Select(p=>p.Items).ToArray(),Calls=UniversalAlpineArmWorldCalls.Calls.Select(c=>new{c.Pc,c.Target,Caller=labels[UniversalAlpineArmWorldCalls.Caller(c.Pc)],Wrapper=labels[UniversalAlpineArmWorldCalls.Wrapper(c.Target)]}).ToArray()},new JsonSerializerOptions{WriteIndented=true}));cases.Add(new{aliases,baseAddress,Bytes=code.Length,SHA256=Convert.ToHexStringLower(SHA256.HashData(code))});
 }
}
File.WriteAllText(Path.Combine(outPath,"world-call-tests.json"),JsonSerializer.Serialize(new{Status="PRIVATE_COPIED_WORLD_CALL_STORAGE_PREREQUISITE_NOT_GAMEPLAY",Checks=checks,Cases=cases,Pending=new[]{"Joined reachable source-state/frame/matrix/FDresponse issuer", "Actual terrain/floor/neighbor-chain query owner", "Factory frequency and complete aggregate resource/lifetime/capacity issuer"}},new JsonSerializerOptions{WriteIndented=true})+"\n");Console.WriteLine($"PASS {checks} Alpine source/copy/world-call mutation and independent relocation/readback checks");

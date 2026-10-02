using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;
if(args.Length != 3) throw new ArgumentException("Original USA BIN, catalog root, new report path required.");
int checks=0;void Check(bool b,string l){checks++;if(!b)throw new InvalidOperationException(l);}void Refused(Action f,string l){bool b=false;try{f();}catch(InvalidDataException){b=true;}Check(b,l);}
UniversalSourceLibrary.RequireOriginal(args[0]);var lib=new UniversalSourceLibrary(args[0],LevelCatalog.Load(args[1]));var source=lib.Get("gnorccove").Overlay;
var map=new Dictionary<int,int>{{0xC9,501},{0xCF,502}};
var part=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,[0xC9,0xCF],"model_read",map).Part;
UniversalAsmPart Clone(){var a=new UniversalAsmPart();a.Items.AddRange(part.Items.Select(i=>i with{ClassReadOwned=false}));return a;}
var pristine=Clone();Check(UniversalGnorcCurrentModelRead.MarkOwned(pristine,source).SequenceEqual(new[]{UniversalGnorcCurrentModelRead.Read}),"Exact mapped C9 current actor lookup enrolled");
Check(pristine.Items.Single(i=>(i.SourcePc??i.Orig)==UniversalGnorcCurrentModelRead.Read).ClassReadOwned,"Ownership marker set");
Check(pristine.Items.Select(i=>i.Word).SequenceEqual(part.Items.Select(i=>i.Word)),"Physical destination class and emitted words unchanged");
var owner=part.Items.Single(i=>(i.SourcePc??i.Orig)==UniversalGnorcCurrentModelRead.Read);
Check(owner.Word==0x86620036,"Current actor LH remains destination gModels selector");
foreach(uint physical in new uint[]{0xC9,501,511}){
 var registers=new UniversalMipsState();registers[2]=physical;registers[9]=0x80076378;
 registers.Apply(source.Word(0x80080E44));registers.Apply(source.Word(0x80080E48));
 Check(registers[2]==0x80076378+physical*4,"Physical class selects exact destination model slot");
 registers.Apply(source.Word(0x80080E4C));Check(registers[2]==null,"Model root load kills original class value");
}
for(int n=0;n<UniversalGnorcCurrentModelRead.Packet.Length;n++){
 uint pc=UniversalGnorcCurrentModelRead.Start+(uint)n*4;int i=part.Items.FindIndex(x=>(x.SourcePc??x.Orig)==pc);
 var changed=Clone();changed.Items[i]=changed.Items[i] with{Word=changed.Items[i].Word^1};Refused(()=>UniversalGnorcCurrentModelRead.MarkOwned(changed,source),"Copied model argument/consumer/delay byte mutation refuses");
 var original=source.Bytes.ToArray();original[(int)(pc-UniversalOverlay.Base)]^=1;Refused(()=>UniversalGnorcCurrentModelRead.MarkOwned(Clone(),new(original,source.Bindings)),"Every native source packet word authenticated");
 var removed=Clone();removed.Items.RemoveAt(i);if(pc==UniversalGnorcCurrentModelRead.Read)Check(UniversalGnorcCurrentModelRead.MarkOwned(removed,source).Count==0,"Absent read adds no ownership");else Refused(()=>UniversalGnorcCurrentModelRead.MarkOwned(removed,source),"Missing copied packet word refuses");
 var other=Clone();other.Items[i]=other.Items[i] with{SourcePc=pc+0x100};if(pc==UniversalGnorcCurrentModelRead.Read)Check(UniversalGnorcCurrentModelRead.MarkOwned(other,source).Count==0,"Foreign read not enrolled");else Refused(()=>UniversalGnorcCurrentModelRead.MarkOwned(other,source),"Copied source identity mutation refuses");
 if(n>0){foreach(var kind in new[]{UniversalFixKind.Branch,UniversalFixKind.Jump}){var a=Clone();a.Emit(UniversalMips.EncodeJ(2,pc),new(kind,pc));Refused(()=>UniversalGnorcCurrentModelRead.MarkOwned(a,source),"New branch/jump into model packet refuses");}
 var raw=Clone();raw.Emit(UniversalMips.EncodeJ(2,pc));Refused(()=>UniversalGnorcCurrentModelRead.MarkOwned(raw,source),"Raw model packet incoming jump refuses");
 var table=Clone();table.Tables["bad_table"]=[pc];Refused(()=>UniversalGnorcCurrentModelRead.MarkOwned(table,source),"Jump table cannot skip model base producer");
 var label=Clone();label.Items.Insert(i,new(null,default,null,"bad_label"));label.Emit(UniversalMips.EncodeJ(2,0),new(UniversalFixKind.JumpLabel,Label:"bad_label"));Refused(()=>UniversalGnorcCurrentModelRead.MarkOwned(label,source),"Copied model packet incoming label refuses");}
}
var packet=part.Items.Where(i=>(i.SourcePc??i.Orig)>=UniversalGnorcCurrentModelRead.Start&&(i.SourcePc??i.Orig)<UniversalGnorcCurrentModelRead.Start+UniversalGnorcCurrentModelRead.Packet.Length*4).ToArray();
var duplicate=Clone();duplicate.Items.AddRange(packet);Check(UniversalGnorcCurrentModelRead.MarkOwned(duplicate,source).Count==1&&duplicate.Items.Count(i=>i.ClassReadOwned)==2,"All intact copies owned without code changes");
var incomplete=Clone();incomplete.Items.AddRange(packet.Skip(1));Refused(()=>UniversalGnorcCurrentModelRead.MarkOwned(incomplete,source),"Valid first copy cannot bless partial second copy");
foreach(uint global in new uint[]{UniversalExeBindings.UpdateGlobal,UniversalExeBindings.InitGlobal,UniversalExeBindings.CopyBufferGlobal}){var bindings=source.Bindings.ToDictionary();bindings[global]=bindings[global] with{Value=bindings[global].Value+4};Refused(()=>UniversalGnorcCurrentModelRead.MarkOwned(Clone(),new(source.Bytes.ToArray(),bindings)),"Changed callback ownership refuses");}
Check(UniversalGnorcCurrentModelRead.MarkOwned(new(),source).Count==0,"No native model read adds no ownership");
UniversalSourceLibrary.RequireOriginal(args[0]);File.WriteAllText(args[2],JsonSerializer.Serialize(new{checks,read=$"{UniversalGnorcCurrentModelRead.Read:X8}",mappedC9=501,unchangedPhysicalClass=true,sourceSha256=Convert.ToHexStringLower(SHA256.HashData(source.Bytes))},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"GNORC_CURRENT_MODEL_READ_OK checks={checks}");

using System.Buffers.Binary;
using Spyro.Editor.Core.Universal;

internal static class InitializerOwnershipCases
{
    static uint I(int op,int rs,int rt,int imm)=>UniversalMips.EncodeI(op,rs,rt,imm);
    static uint Move(int from,int to)=>(uint)((from<<21)|(to<<11)|33);
    static (UniversalOverlay Overlay,UniversalAsmPart Part) Build(uint[] words)
    {
        var bytes=new byte[0x500];for(int n=0;n<words.Length;n++)BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x100+4*n),words[n]);
        var bindings=new Dictionary<uint,UniversalExeBinding>();foreach(uint global in new[]{UniversalExeBindings.InitGlobal,UniversalExeBindings.UpdateGlobal,UniversalExeBindings.CopyBufferGlobal})bindings[global]=new(global,0,0,UniversalOverlay.Base+0x100);
        var overlay=new UniversalOverlay(bytes,bindings);var part=new UniversalAsmPart();for(int n=0;n<words.Length;n++)part.Emit(words[n],orig:overlay.Init+(uint)n*4);return(overlay,part);
    }
    internal static void Run(Action<bool,string> check)
    {
        uint[] native=[I(9,29,29,-80),I(43,29,16,48),Move(4,16),Move(5,21),UniversalMips.EncodeJ(3,0x800524C4),I(43,29,17,52),Move(2,20),I(4,21,0,1),I(41,20,16,0x36),
            I(33,20,2,0x36),0,I(9,0,9,13),I(4,2,9,1),0,I(9,0,2,0),I(33,21,2,0x36),0,I(9,0,9,13),I(4,2,9,1),0,I(9,0,2,0),UniversalMips.JrRa,0];
        var(s,p)=Build(native);var roles=UniversalClassRemapping.ProveInitializerRoles(p,s);
        check(roles.ClassRegister==16&&roles.ActorRegister==20&&roles.ParentRegister==21,"s4 actor and s5 parent ownership are distinct");
        UniversalClassRemapping.PatchInitClassStore(p,s,13,509);
        check(p.Items.Any(i=>i.Word==I(41,20,25,0x36))&&p.Items.Any(i=>i.Word==I(9,0,25,509)),"mapped class stores into proved actor s4");
        var map=new Dictionary<int,int>{{13,509}};
        var actors=UniversalClassRemapping.PatchInitAllocatedClassReads(p,s,map,13);
        var parents=UniversalClassRemapping.PatchInitParentClassReads(p,s,map);
        check(actors.SequenceEqual(new[]{s.Init+36}),"allocated s4 class comparison is translated");
        check(parents.SequenceEqual(new[]{s.Init+60}),"parent s5 class comparison is separately translated");
        var legacy=native.ToArray();legacy[6]=Move(2,19);legacy[8]=I(41,19,16,0x36);legacy[9]=I(33,19,2,0x36);
        var(l,lp)=Build(legacy);check(UniversalClassRemapping.ProveInitializerRoles(lp,l).ActorRegister==19,"existing s3 ownership retained");
        foreach(int actor in new[]{19,20})for(int mapped=0;mapped<512;mapped++)
        {
            var words=native.ToArray();words[6]=Move(2,actor);words[8]=I(41,actor,16,0x36);var(o,part)=Build(words);
            UniversalClassRemapping.PatchInitClassStore(part,o,13,mapped);
            var registers=new uint[32];registers[4]=13;registers[5]=0x800A1234;registers[29]=0x801F0000;
            uint writtenAddress=0,writtenClass=0;
            foreach(var item in part.Items)
            {
                uint w=item.Word??0;int op=UniversalMips.Op(w),rs=UniversalMips.Rs(w),rt=UniversalMips.Rt(w);
                if(w==UniversalMips.EncodeJ(3,0x800524C4)){registers[2]=0x800B0000;continue;}
                if(op==0&&UniversalMips.Fn(w)==33)registers[UniversalMips.Rd(w)]=registers[rs]+registers[rt];
                else if(op==9)registers[rt]=unchecked(registers[rs]+(uint)UniversalMips.SImm(w));
                else if(op==41&&UniversalMips.SImm(w)==0x36){writtenAddress=registers[rs]+0x36;writtenClass=registers[rt];break;}
            }
            check(writtenAddress==0x800B0036&&writtenClass==(uint)mapped&&registers[16]==13&&registers[actor]==0x800B0000&&registers[21]==0x800A1234,
                "independent native delay-store scalar preserves class/actor/parent roles "+actor+"/"+mapped);
        }
        var unowned=Build(native);bool storeRefused=false;try{UniversalClassRemapping.RequireNoUnownedClassMemory(unowned.Part,true);}catch(InvalidDataException){storeRefused=true;}check(storeRefused,"raw class store needs explicit proved ownership");
        var mismatched=Build(native);mismatched.Part.Items[2]=mismatched.Part.Items[2] with{Word=Move(5,16)};bool changed=false;try{UniversalClassRemapping.ProveInitializerRoles(mismatched.Part,mismatched.Overlay);}catch(InvalidDataException){changed=true;}check(changed,"emitted class alias must match source owner");
        foreach(var change in new (string Label,int At,uint Word)[]{("wrong returned register",6,Move(3,20)),("class alias wrong",2,Move(5,16)),("actor copy disagrees with store",6,Move(2,19)),("caller-saved actor",8,I(41,8,16,0x36)),("unknown allocator",4,UniversalMips.EncodeJ(3,0x800524C8)),("intervening call",7,UniversalMips.EncodeJ(3,0x80012340)),("load-delay actor clobber",7,I(35,29,20,0)),("load-delay class clobber",7,I(35,29,16,0)),("class overwritten",3,Move(5,16)),("class input overwritten",1,I(9,0,4,13)),("pending load into class save",1,I(35,29,16,0)),("actor alias overwritten",7,Move(21,20))})
        {
            var corrupt=native.ToArray();corrupt[change.At]=change.Word;var(c,cp)=Build(corrupt);bool refused=false;try{UniversalClassRemapping.ProveInitializerRoles(cp,c);}catch(InvalidDataException){refused=true;}check(refused,"refuse "+change.Label);
        }
        var delayed=native.Take(6).Concat(new[]{I(35,29,20,0)}).Concat(native.Skip(6)).ToArray();var(doverlay,dpart)=Build(delayed);bool pendingActor=false;try{UniversalClassRemapping.ProveInitializerRoles(dpart,doverlay);}catch(InvalidDataException){pendingActor=true;}check(pendingActor,"pending actor load cannot alias allocator-result copy");
        var parentDelay=native.Take(3).Concat(new[]{I(35,29,21,0)}).Concat(native.Skip(3)).ToArray();var(po,pp)=Build(parentDelay);
        check(UniversalClassRemapping.ProveInitializerRoles(pp,po).ParentRegister==null,"pending parent load declines optional parent ownership");
        UniversalClassRemapping.PatchInitClassStore(pp,po,13,509);UniversalClassRemapping.PatchInitAllocatedClassReads(pp,po,map,13);UniversalClassRemapping.PatchInitParentClassReads(pp,po,map);
        bool parentRefused=false;try{UniversalClassRemapping.RequireNoUnownedClassMemory(pp,true);}catch(InvalidDataException){parentRefused=true;}check(parentRefused,"unproved parent class read retains final refusal");
        var missing=Build(native);missing.Part.Items.RemoveAt(3);bool gap=false;try{UniversalClassRemapping.ProveInitializerRoles(missing.Part,missing.Overlay);}catch(InvalidDataException){gap=true;}check(gap,"missing native prefix instruction refuses ownership");
        var parentAlias=native.ToArray();parentAlias[3]=Move(5,20);var(a,ap)=Build(parentAlias);check(UniversalClassRemapping.ProveInitializerRoles(ap,a).ParentRegister==null,"allocated register cannot also be the optional parent");
    }
    internal static void Written(UniversalSourceLibrary library,Spyro.Editor.Core.Levels.LevelCatalog catalog,string output,Action<bool,string> check)
    {
        if(File.Exists(output))throw new InvalidOperationException("Refuse to replace an existing fixture.");
        var source=library.Get("gnastysloot");var destination=library.Get("artisans");var row=source.Data.Row(15);
        int sourceClass=BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(0x36));
        var placement=new UniversalMobyPlacement("gnastysloot",15,84992,47850,8960,EditorId:"initializer-loot15",PersistentSlot:0);
        var result=UniversalMobyExport.Apply(library,library.ImagePath,output,catalog,new Dictionary<string,IReadOnlyList<UniversalMobyPlacement>>{{"artisans",new[]{placement}}});
        var compose=result.Levels.Single().Compose;var imported=compose.Classes.Single(c=>!c.DependencyOnly&&c.SourceLevelKey=="gnastysloot"&&c.ClassId==sourceClass);
        var disc=UniversalDiscImage.Read(output);var data=new UniversalLevelData(disc.EntryBytes(destination.Level.SourceWadEntry));
        check(imported.DestinationClassId!=sourceClass,"written initializer fixture has genuine source collision");
        check(data.Scene().RowCount==175&&BinaryPrimitives.ReadUInt16LittleEndian(data.Row(compose.NewRowIndices[0]).AsSpan(0x36))==imported.DestinationClassId,"written actor has mapped class and one new physical row");
        check(data.Registrations().Any(r=>r.ClassId==imported.DestinationClassId),"written model registration uses mapped class");
        var overlay=disc.EntryBytes(destination.Level.SourceWadEntry-1);check(overlay.SequenceEqual(compose.Overlay),"complete written constructor bundle equals assembled source-owned bundle");
        var tail=overlay.AsSpan(destination.Overlay.Bytes.Length).ToArray();
        uint store=I(41,20,25,0x36),literal=I(9,0,25,imported.DestinationClassId);
        bool pair=false;for(int at=0;at+12<=tail.Length;at+=4)if(BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(at))==literal&&BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(at+8))==store)pair=true;
        check(pair,"written native conditional branch delay slot stores mapped class into proved s4 actor");
        check(disc.EntryBytes(8).SequenceEqual(library.Disc.EntryBytes(8))&&compose.Ram.FinalHeadroom>=UniversalMobyComposer.RamSafetyMargin,"PETE unchanged and actual written RAM guard retained");
        string original;using(var f=File.OpenRead(library.ImagePath))original=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(f));check(original==UniversalSourceLibrary.OriginalUsaSha256,"original USA unchanged after written constructor fixture");
        File.WriteAllText(Path.ChangeExtension(output,".readback.json"),System.Text.Json.JsonSerializer.Serialize(new{Source="gnastysloot",SourceRow=15,SourceClassId=sourceClass,imported.DestinationClassId,imported.ModelOrigin,NativePosition=new{X=84992,Y=47850,Z=8960},compose.NewRowIndices,compose.PlacementSaveRows,compose.Ram,compose.ParticleDependencies,result.OutputImageSha256,SourceImageSha256=original,CoreModuleVersionId=typeof(UniversalMobyComposer).Module.ModuleVersionId,CoreModuleSha256=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(UniversalMobyComposer).Assembly.Location))),RuntimeStatus="not-tested"},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    }
    internal static void Source(UniversalSourceLibrary library,Spyro.Editor.Core.Levels.LevelCatalog catalog,Action<bool,string> check)
    {
        int proved=0;
        foreach(var level in catalog.Levels.Where(l=>l.SourceWadEntry>0))
        {
            var s=library.Get(level.Key).Overlay;var p=new UniversalAsmPart();
            for(uint pc=s.Init;pc<s.Init+0x80;pc+=4){uint w=s.Word(pc);p.Emit(w,orig:pc);if(UniversalMips.Op(w)==41&&UniversalMips.SImm(w)==0x36)break;}
            var r=UniversalClassRemapping.ProveInitializerRoles(p,s);proved++;
            check(r.ActorRegister is 19 or 20 && r.ClassRegister==16,"exact native initializer allocation "+level.Key);
        }
        check(proved==35,"all35 native constructor prefixes retain a proved ownership chain");
        var source=library.Get("gnastysloot").Overlay;
        var(init,_,_)=UniversalBehaviorBundleBuilder.BuildSpecializedInit(source,[13],"loot_init_proof");
        UniversalClassRemapping.PatchInitClassStore(init,source,13,509);
        var roles=UniversalClassRemapping.ProveInitializerRoles(init,source);
        check(roles.ActorRegister==20&&roles.ParentRegister==21,"actual pruned GnastyLoot constructor owns s4/s5 roles after rewrite");
        UniversalClassRemapping.PatchInitAllocatedClassReads(init,source,new Dictionary<int,int>{{13,509}},13);
        UniversalClassRemapping.PatchInitParentClassReads(init,source,new Dictionary<int,int>{{13,509}});
        UniversalClassRemapping.RequireNoUnownedClassMemory(init,true);
        check(init.Items.Any(i=>i.Word==I(41,20,25,0x36)),"actual pruned native mapped class store is source-owned s4");
    }
}

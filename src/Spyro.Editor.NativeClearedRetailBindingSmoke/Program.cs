using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

int checks=0;
void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
void Refuse(Action action,string message){try{action();}catch(InvalidDataException){checks++;return;}throw new Exception(message);}
uint Word(byte[] b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
void Put(byte[] b,int at,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at),v);
LevelDefinition Target(int id)=>new(){Key="test"+id,LevelId=id,SourceWadEntry=10+2*((id/10-1)*6+id%10)};

if(args.Length==0)
{
    Refuse(()=>NativeClearedRetailBinding.Census(new byte[0x66000]),"Synthetic source cannot certify a bootstrap");
    Console.WriteLine($"NATIVE_CLEARED_RETAIL_BINDING_SMOKE_OK {checks} source-free checks; actual-source census requires original USA BIN.");
    return;
}
var exe=UniversalDiscImage.Read(args[0]).Exe;
var census=NativeClearedRetailBinding.Census(exe);
Check(census.Count==30,"All30normal slots retained");
Check(census.Where(p=>p.NineSettersSupported).Select(p=>p.LevelId).SequenceEqual(new[]{10,11,13,20,21,22,30,40,50,60}),"Exact full setter family");
Check(census.Select(p=>p.InitializerAddress).Distinct().Count()==30,"Unique initializers");
var town=UniversalExeBindings.Read(exe,13);
{
    var aggregate=exe.ToArray();uint townEntry=Word(exe,0x1BA4+13*4);
    Put(aggregate,0x1BA4+65*4,townEntry);
    var artisan=NativeClearedRetailBinding.Prepare(exe,aggregate,Target(10));
    Check(Word(artisan,0x1BA4+65*4)==townEntry,"Authored65 aggregate jumpentry preserved");
    var neutralTown=NativeClearedRetailBinding.Prepare(exe,aggregate,Target(13));
    Check(neutralTown.AsSpan(0x4B098,0x98).SequenceEqual(aggregate.AsSpan(0x4B098,0x98)),"AlreadyneutralTown alias no-op");
    foreach(uint address in new[]{Word(exe,0x1BA4+10*4),Word(exe,0x1BA4+10*4)+4,townEntry+4})
    {
        var mutation=exe.ToArray();Put(mutation,0x1BA4+65*4,address);
        Refuse(()=>NativeClearedRetailBinding.Prepare(exe,mutation,Target(address>=townEntry?13:10)),"Stagedwhole/interior alias cannot change owner");
    }
    var changedTown=aggregate.ToArray();changedTown[0x4B09C]^=1;
    Refuse(()=>NativeClearedRetailBinding.Prepare(exe,changedTown,Target(13)),"EditedaliasedTown cannotbe silentlyreset");
}
foreach(var profile in census)
{
    var target=Target(profile.LevelId);
    if(!profile.NineSettersSupported)
    {
        // Short owners get a neutral Town copy in a free relocation slot; their own initializer stays untouched.
        var moved=NativeClearedRetailBinding.Prepare(exe,exe,target);
        uint entry=Word(moved,0x1BA4+target.LevelId*4);
        Check(NativeClearedRetailBinding.RelocationSlots.Contains(entry),"Short owner moves to a relocation slot");
        int slotAt=(int)(entry-UniversalExeBindings.ExeBase),shortAt=(int)(profile.InitializerAddress-UniversalExeBindings.ExeBase),idx=profile.ContinuousIndex;
        Check(moved.AsSpan(slotAt,0x98).SequenceEqual(exe.AsSpan(0x4B098,0x98)),"Relocated neutral Town copy");
        Check(moved.AsSpan(shortAt,profile.InitializerByteLength).SequenceEqual(exe.AsSpan(shortAt,profile.InitializerByteLength)),"Short initializer untouched");
        for(int at=0;at<moved.Length;at++)
        {
            bool owned=at>=slotAt&&at<slotAt+0x98 || at>=0x1BA4+target.LevelId*4&&at<0x1BA8+target.LevelId*4 ||
                at==0x5FC14+idx || at>=0x5FC38+idx*2&&at<0x5FC3A+idx*2 || idx<18&&at==0x5FC80+idx;
            if(!owned)Check(moved[at]==exe[at],"Relocation complement exact");
        }
        Check(UniversalExeBindings.Read(moved,target.LevelId).Count==9,"Relocated nine native bindings");
        Check(NativeClearedRetailBinding.Prepare(exe,moved,target).SequenceEqual(moved),"Relocated state idempotent");
        continue;
    }
    byte[] staged=exe.ToArray();staged[0x100]=0xA5;
    byte[] after=NativeClearedRetailBinding.Prepare(exe,staged,target);
    Check(staged[0x100]==0xA5 && exe[0x100]!=0xA5,"Caller/source preserved");
    var bindings=UniversalExeBindings.Read(after,target.LevelId);
    Check(bindings.Count==9,"Nine native bindings");
    foreach(var pair in town)Check(bindings[pair.Key].Value==pair.Value.Value,"Actual callback equals originalTown");
    foreach(var other in census.Where(p=>p.LevelId!=target.LevelId))
    {
        int at=(int)(other.InitializerAddress-UniversalExeBindings.ExeBase);
        Check(after.AsSpan(at,other.InitializerByteLength).SequenceEqual(staged.AsSpan(at,other.InitializerByteLength)),"Unrelated initializer immutable");
    }
    int index=profile.ContinuousIndex;
    int start=(int)(profile.InitializerAddress-UniversalExeBindings.ExeBase);
    for(int at=0;at<after.Length;at++)
    {
        bool owned=at>=start&&at<start+0x98 || at==0x5FC14+index || at>=0x5FC38+index*2&&at<0x5FC3A+index*2 || profile.HasRetailEggGoal&&at==0x5FC80+index;
        if(!owned)Check(after[at]==staged[at],"Unowned executable complement exact");
    }
    Check(after[0x5FC14+index]==0 && Word(after,0x5FC38+index*2)%65536==0,"Empty target goals");
    if(profile.HasRetailEggGoal)Check(after[0x5FC80+index]==0,"Bounded emptyegggoal");
    Check(NativeClearedRetailBinding.Prepare(exe,after,target).SequenceEqual(after),"Neutral state idempotent");
    if(index>=18)Check(after.AsSpan(0x5FC80,36).SequenceEqual(staged.AsSpan(0x5FC80,36)),"Later nonexistenteggslot leaves strings intact");
    for(int at=0;at<0x98;at+=4)
    {
        var mutation=staged.ToArray();mutation[start+at]^=1;
        Refuse(()=>NativeClearedRetailBinding.Prepare(exe,mutation,target),"Every original owner word guarded");
    }
    foreach(int at in new[]{0x1BA4+target.LevelId*4,0x4BED8,0x4BEE0,0xC934,0xC8E8,0xCB4C,0xCCBC,0x45468,0x5FC14+index,0x5FC38+index*2})
    {
        var mutation=staged.ToArray();mutation[at]=0x7F;
        Refuse(()=>NativeClearedRetailBinding.Prepare(exe,mutation,target),"Staged ownership conflict guarded");
    }
    var invalidOriginal=exe.ToArray();invalidOriginal[start]^=1;
    Refuse(()=>NativeClearedRetailBinding.Prepare(invalidOriginal,staged,target),"Original sourceidentity guarded");
    Refuse(()=>NativeClearedRetailBinding.Prepare(exe,staged,target with{SourceWadEntry=2}),"RetailWADbinding guarded");
    var summary=NativeClearedRetailBinding.Summarize(exe,staged,target);
    Check(summary.Target==profile && summary.ProfileId==NativeClearedRetailBinding.ProfileId,"Code-owned summary");
}
{
    // Two short levels take the two slots; a third is refused, and no slot is ever shared.
    var shorts=census.Where(p=>!p.NineSettersSupported).Take(3).ToArray();
    var first=NativeClearedRetailBinding.Prepare(exe,exe,Target(shorts[0].LevelId));
    var second=NativeClearedRetailBinding.Prepare(exe,first,Target(shorts[1].LevelId));
    Check(Word(second,0x1BA4+shorts[0].LevelId*4)!=Word(second,0x1BA4+shorts[1].LevelId*4),"Distinct relocation slots");
    Refuse(()=>NativeClearedRetailBinding.Prepare(exe,second,Target(shorts[2].LevelId)),"Third short level refused when both slots are used");
    var shared=first.ToArray();Put(shared,0x1BA4+shorts[1].LevelId*4,Word(first,0x1BA4+shorts[0].LevelId*4));
    Refuse(()=>NativeClearedRetailBinding.Prepare(exe,shared,Target(shorts[1].LevelId)),"Shared relocation slot refused");
}
foreach(int id in census.Select(p=>p.LevelId).Append(65))
{
    var staged=exe.ToArray();NativeClearedRetailBinding.ApplyNewGameStart(exe,staged,id);
    Check(Word(staged,0x2F80)==(0x24020000u|(uint)id)&&Word(staged,0x1DCF0)==(0x24020000u|(uint)id),"BothNewGameowners");
    NativeClearedRetailBinding.ApplyNewGameStart(exe,staged,10);
    Check(staged.SequenceEqual(exe),"Owned selection reverses exactly");
}
foreach(int id in new[]{-1,0,15,25,35,66})Refuse(()=>NativeClearedRetailBinding.ApplyNewGameStart(exe,exe.ToArray(),id),"InvalidNewGameclass refused");
{
    var staged=exe.ToArray();Put(staged,0x2F80,0x24020041);var before=staged.ToArray();
    Refuse(()=>NativeClearedRetailBinding.ApplyNewGameStart(exe,staged,10),"Mixed owned selections refused");
    Check(staged.SequenceEqual(before),"Mixed-selection failure atomic");
}
foreach(int at in new[]{0x2F7C,0x2F80,0x1DC40,0x1DCF0,0x23308,0x49D94})
{
    var staged=exe.ToArray();Put(staged,at,0x2402000F);var before=staged.ToArray();
    Refuse(()=>NativeClearedRetailBinding.ApplyNewGameStart(exe,staged,10),"NewGameowner conflict");
    Check(staged.SequenceEqual(before),"FailedNewGamemutation atomic");
}
if(args.Length>1)
{
    Directory.CreateDirectory(args[1]);
    File.WriteAllText(Path.Combine(args[1],"binding-census.json"),JsonSerializer.Serialize(new{checks,profiles=census,summary=NativeClearedRetailBinding.Summarize(exe,exe,Target(10)),runtime="not-tested"},new JsonSerializerOptions{WriteIndented=true}));
}
Console.WriteLine($"NATIVE_CLEARED_RETAIL_BINDING_SOURCE_OK {checks} checks;10full slots supported,20short slots relocated (two at a time); Dark Hollow runtime tested in the emulator.");

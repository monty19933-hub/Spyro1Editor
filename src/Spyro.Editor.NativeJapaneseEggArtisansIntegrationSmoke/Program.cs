using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks=0;
void Check(bool condition,string why){if(!condition)throw new Exception(why);checks++;}
void Refuse(Action action,string why){try{action();}catch(InvalidDataException){checks++;return;}throw new Exception("Expected refusal: "+why);}
static uint Word(byte[] b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
NativeJapaneseEggVisualPlacement[] placements=[new("egg-a",82000,75000,6500,32,1),new("egg-b",83024,75000,6500,96,9)];
void Verify(NativeJapaneseEggArtisansIntegrationPlan plan)
{
    byte[] data=plan.CopyLevelData(),overlay=plan.CopyOverlay(),exe=plan.CopyExecutable(),scene=plan.CopyScene();
    Check(plan.Rows.Count==2&&plan.Rows[1].NativeIndex==plan.Rows[0].NativeIndex+1,"Two actual scene ordinals are append-only and independent.");
    Check(plan.Rows[1].PrivateSceneOffset-plan.Rows[0].PrivateSceneOffset==12,"Two disjoint private states, not aliases of one native level slot.");
    Check(plan.SceneAppend.Allocation.SharedNodeCount==plan.ModelGraph.Nodes.Count&&plan.ModelGraph.Installs.Count==1,"One shared immutable model installation serves both rows.");
    foreach(var row in plan.Rows)
    {
        int at=row.RowSceneOffset;Check(Word(scene,at)==row.PrivateSceneOffset,"Disk row pointer remains scene-relative.");
        Check(!scene.AsSpan(row.PrivateSceneOffset,12).ContainsAnyExcept((byte)0)&&scene[at+0x49]==0,"Native visual initializer starts from private/state zero.");
        Check(Word(scene,at+12)==row.Placement.X&&Word(scene,at+16)==row.Placement.Y&&Word(scene,at+20)==row.Placement.Z,"Explicit authored coordinates are installed only in new rows.");
        Check(scene[at+0x46]==row.Placement.Yaw&&scene[at+0x4F]==row.Placement.ProfileIndex,"Independent yaw and opaque native render-mode bytes.");
        Check(BinaryPrimitives.ReadUInt16LittleEndian(scene.AsSpan(at+0x36))==0xF5&&scene[at+0x3C]==0&&scene[at+0x3D]==0,"Native class and admitted animation are preserved.");
    }
    Check(plan.Rows.Select(r=>r.ProfileDescriptor).Distinct().Count()==2,"Two numerical static-table projections differ; F5 does not select this table.");
    var source=plan.OriginalSceneSource;
    Check(scene.AsSpan(source.RowTableOffset,source.PlacedCount*0x58).SequenceEqual(plan.CopyOriginalScene().AsSpan(source.RowTableOffset,source.PlacedCount*0x58)),"Every original complete row is unchanged.");
    Check(scene.AsSpan(source.PropertiesOffset+4,source.PropertiesEnd-source.PropertiesOffset-4).SequenceEqual(plan.CopyOriginalScene().AsSpan(source.PropertiesOffset+4,source.PropertiesEnd-source.PropertiesOffset-4)),"Every original private byte is unchanged.");
    Check(scene.AsSpan(source.PropertiesEnd+24,source.FixupOffset-source.PropertiesEnd).SequenceEqual(plan.CopyOriginalScene().AsSpan(source.PropertiesEnd,source.FixupOffset-source.PropertiesEnd)),"Complete original pod/collision components move exactly once.");
    Check(scene.AsSpan(source.FixupOffset+28,source.FixupCount*4).SequenceEqual(plan.CopyOriginalScene().AsSpan(source.FixupOffset+4,source.FixupCount*4)),"Every original relocation entry is preserved.");
    Check(plan.SceneAppend.FixupCountAfter==source.FixupCount+2&&Word(scene,source.FixupOffset+24)==source.FixupCount+2,"Exactly two new private-pointer fixups.");
    Check(overlay.Length==0xE800&&plan.FinalCopyBufferAddress==0x80089238&&plan.OriginalCopyBufferAddress==0x80088620,"Copy buffer owns full padded overlay, including original non-end-aligned anchor.");
    Check(plan.FinalCopyBufferAddress-plan.OriginalCopyBufferAddress==0xC18,"Copy shift is C18, never incorrectly simplified to sector growth800.");
    byte[] original=plan.CopyOriginalOverlay();
    Check(overlay.AsSpan(0,0x319C).SequenceEqual(original.AsSpan(0,0x319C))&&overlay.AsSpan(0x31A4,original.Length-0x31A4).SequenceEqual(original.AsSpan(0x31A4)),"Only the exact original8-byte compare leaf changes.");
    foreach(var owner in plan.VisualAncestor.CodeOwners.Where(o=>o.Id!="alignment"))
        Check(overlay.AsSpan(owner.OverlayOffset,owner.ByteLength).SequenceEqual(plan.VisualAncestor.CopyDetachedOverlay().AsSpan(owner.OverlayOffset,owner.ByteLength)),"All existing linked visual code remains at its original proved PC.");
    for(int actor=0;actor<=ushort.MaxValue;actor++)
    {
        var before=DispatchProbe.Trace(original,(ushort)actor,0,0);var after=DispatchProbe.Trace(overlay,(ushort)actor,plan.GuardPc,0x1C);
        Check(before.Registers.SequenceEqual(after.Registers),"Comparison plus guard preserves every original dispatch result register: "+actor);
        Check(after.Target==(actor==0xF5?plan.VisualAncestor.EntryPc:before.Target),"Only F5 handler target changes: "+actor);
    }
    Check(plan.RevertDataExact(data).SequenceEqual(plan.CopyOriginalLevelData()),"Existing growth plus model inverse recovers complete original data.");
    Check(plan.RevertOverlayExact(overlay).SequenceEqual(original)&&plan.RevertExecutableExact(exe).SequenceEqual(plan.CopyOriginalExecutable()),"Complete overlay/EXE inverse checks exact afterimages.");
    foreach(int at in new[]{0,0x1000,plan.VisualAncestor.ModelAssembly.PackageOffset,(int)Word(data,0x18)+plan.Rows[0].RowSceneOffset,data.Length-1})
    {byte[] bad=data.ToArray();bad[at]^=1;Refuse(()=>plan.RevertDataExact(bad),"changed data owner "+at);}
    foreach(int at in new[]{0,0x319C,0xE000,overlay.Length-1})
    {byte[] bad=overlay.ToArray();bad[at]^=1;Refuse(()=>plan.RevertOverlayExact(bad),"changed overlay owner "+at);}
    foreach(int at in new[]{0,0x4AF18,0x4AF1C,exe.Length-1})
    {byte[] bad=exe.ToArray();bad[at]^=1;Refuse(()=>plan.RevertExecutableExact(bad),"changed EXE owner "+at);}
    Check(plan.RuntimeMemory.PlacedMobyCount==source.PlacedCount+2&&plan.RuntimeMemory.DynamicMobyCapacity==plan.SceneAppend.DynamicCapacityAfter,"Existing runtime accounting agrees with actual private allocator.");
    Check(plan.RuntimeMemory.SceneBytesRead==scene.Length&&plan.RuntimeMemory.CopyBufferAddress==plan.FinalCopyBufferAddress,"RAM accounting charges complete CD reads, not inferred used model/scene lengths.");
    Check(!plan.ActivationApproved&&!plan.NativeImportApproved&&!plan.PublicationApproved&&!plan.RuntimeMemory.RuntimeFitProven&&!plan.RuntimeMemory.PeakTransientFitProven&&!plan.RenderVerified,"Normal resident disjointness is never peak, native draw or activation approval.");
    Refuse(plan.RequireNativeActivation,"unresolved peak stack and rendering owners");
    ExecuteBoth(plan);
}
void ExecuteBoth(NativeJapaneseEggArtisansIntegrationPlan plan)
{
    var cpu=new TinyR3000();cpu.Copy(0x8000F800,plan.CopyExecutable());cpu.Copy(0x8007AA38,plan.CopyOverlay());
    byte[] scene=plan.CopyScene();uint start=plan.RuntimeMemory.SceneRamStart;cpu.Copy(start,scene);
    int fix=plan.OriginalSceneSource.FixupOffset+24;
    for(int i=0;i<Word(scene,fix);i++){uint field=Word(scene,fix+4+4*i);cpu.Put(start+field,start+Word(scene,(int)field));}
    const uint stack=0x801F0000;cpu.Put(0x80075AC0,0x12345678);cpu.Put(0x800756C4,1);
    for(int tick=0;tick<8;tick++)
    foreach(var row in plan.Rows)
    {
        var other=plan.Rows.Single(r=>r.NativeIndex!=row.NativeIndex);uint otherAddress=start+(uint)other.PrivateSceneOffset;
        uint[] otherWords=[cpu.Get(otherAddress),cpu.Get(otherAddress+4),cpu.Get(otherAddress+8)];
        for(int i=1;i<32;i++)cpu.Regs[i]=0x12340000u+(uint)i;
        cpu.Regs[3]=0xF5;cpu.Regs[19]=start+(uint)row.RowSceneOffset;cpu.Regs[29]=stack;uint[] registers=cpu.Regs.ToArray();
        cpu.Writes.Clear();cpu.Run(plan.GuardPc,0x80085780);
        Check(cpu.Regs.SequenceEqual(registers),"Guard and actual visual helper restore all original registers.");
        uint p=start+(uint)row.PrivateSceneOffset;
        Check(cpu.Get(p)==row.Placement.Z&&cpu.Get(p+4)==(uint)((tick+1)*128),"Each egg retains its own baseline and phase.");
        Check(otherWords.SequenceEqual(new[]{cpu.Get(otherAddress),cpu.Get(otherAddress+4),cpu.Get(otherAddress+8)}),"One egg cannot change the other egg's private state.");
        uint r=start+(uint)row.RowSceneOffset;
        Check(cpu.Writes.All(a=>a>=p&&a<p+12||a>=r+20&&a<r+24||a==r+0x46||a==r+0x49||a>=0x80075AC0&&a<0x80075AC4||a>=stack-0x2D8&&a<stack),"Tick stores remain inside current row/private, shared USA RNG and declared test stack.");
    }
}
if(args.Length==3&&args[0]=="--source")
{
    using var jp=NativeRegionalSourceSession.Open(args[1],NativeDiscRevision.JapanScps10083);
    using var usa=NativeRegionalSourceSession.Open(args[2],NativeDiscRevision.UsaScus94228);
    var plan=NativeJapaneseEggArtisansIntegrationPlan.Capture(jp,usa,placements);
    Check(plan.SourceCertified&&plan.RuntimeMemory.NativeSourceCertified&&plan.OriginalRuntimeMemory.NativeSourceCertified,"Actual JP/USA source issuer owns the final normal-load report.");
    Check(plan.RuntimeMemory.SceneReadEnd==0x80179554&&plan.RuntimeMemory.NormalReservedBufferGapBytes==0xE65C,"Exact-source normal RAM footprint includes C18 overlay anchor shift plus1000 model growth.");
    Check(plan.SceneAppend.PlacedCountBefore==174&&plan.SceneAppend.PlacedCountAfter==176&&plan.SceneAppend.DynamicCapacityBefore==91&&plan.SceneAppend.DynamicCapacityAfter==89,"Exact Artisans two-egg arena debit.");
    Verify(plan);jp.VerifyUnchanged();usa.VerifyUnchanged();
    Console.WriteLine($"JP_EGG_ARTISANS_INTEGRATION_SOURCE_OK checks={checks} data={plan.LevelDataSha256} overlay={plan.OverlaySha256} exe={plan.ExecutableSha256} activationApproved=false");
}
else if(args.Length==0)
{
    var asset=NativeJapaneseEggModelAssetPlan.Fixture(Fixtures.Model());byte[] original=Fixtures.Resource(),donor=Fixtures.Scene(0xF5);
    var visual=NativeJapaneseEggVisualBehaviorPlan.Fixture(asset,original,Fixtures.JapaneseOverlay(),Fixtures.UsaOverlay(),Fixtures.Executable());
    var appearance=NativeJapaneseObjectAppearance.DecodeFixture(Fixtures.Profiles());
    NativeJapaneseEggArtisansIntegrationPlan Build(NativeJapaneseEggVisualPlacement[] ps,byte[]? source=null)=>NativeJapaneseEggArtisansIntegrationPlan.Fixture(asset,visual,source??original,donor,appearance,ps);
    var plan=Build(placements);Check(!plan.SourceCertified&&!plan.RuntimeMemory.NativeSourceCertified,"Neutral fixtures never issue native source certificates.");Verify(plan);
    Refuse(()=>Build([placements[0]]),"not exactly two instances");Refuse(()=>Build([placements[0],placements[0]]),"duplicate instance identity");
    Refuse(()=>Build([placements[0] with{ProfileIndex=32},placements[1]]),"uncaptured shader profile");
    Refuse(()=>Build([placements[0] with{Yaw=256},placements[1]]),"invalid native heading");
    Refuse(()=>Build([placements[0] with{Z=-1},placements[1]]),"negative coordinate");
    byte[] bad=original.ToArray();bad[0x1000]^=1;Refuse(()=>Build(placements,bad),"wrong complete model-source ancestry");
    var repeat=Build(placements.Reverse().ToArray());Check(plan.LevelDataSha256==repeat.LevelDataSha256&&plan.PlanSha256==repeat.PlanSha256,"Canonical instance order gives deterministic complete bytes and plan.");
    Console.WriteLine($"JP_EGG_ARTISANS_INTEGRATION_SOURCE_FREE_OK checks={checks}");
}
else throw new ArgumentException("Usage: no arguments, or --source <exact-JP-BIN> <exact-USA-BIN>.");

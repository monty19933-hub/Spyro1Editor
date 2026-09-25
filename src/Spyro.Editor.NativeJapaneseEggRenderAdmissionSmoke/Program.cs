using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks=0;
void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
void Refuse(Action action,string message){try{action();}catch(InvalidDataException){checks++;return;}throw new Exception("Expected refusal: "+message);}
static uint W(byte[] b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
static string Hash(byte[] b)=>Convert.ToHexStringLower(SHA256.HashData(b));
NativeJapaneseEggVisualPlacement[] placements=[new("egg-a",82000,75000,6500,32,1),new("egg-b",83024,75000,6500,96,9)];
void Verify(NativeJapaneseEggRenderAdmissionPlan plan)
{
    Check(plan.Rows.Count==2&&plan.Rows.Select(r=>r.PrivateAddress).Distinct().Count()==2,"Independent private owners remain distinct.");
    Check(plan.Rows.Select(r=>r.ModelRoot).Distinct().Count()==1&&plan.Rows.All(r=>r.AnimationHeader==plan.ModelRoot+0x3C),"One model and one animation serve both rows.");
    Check(plan.Rows.Select(r=>r.NativeRenderMode).SequenceEqual(new byte[]{1,9}),"Opaque native modes remain exact authored bytes; not tints.");
    byte[] package=plan.Integration.VisualAncestor.ModelAssembly.CopyModelPackage();
    foreach(var colors in new[]{plan.NearColors,plan.FarColors})foreach(var c in colors)
        Check(((uint)c.Code<<24|(uint)c.Blue<<16|(uint)c.Green<<8|c.Red)==c.PackedWord,"Packed RGB/CODE roundtrip.");
    for(int i=0;i<plan.NearColors.Count;i++)
    {
        Check(W(package,(int)(plan.NearColorAddress-plan.ModelRoot)+4*i)==plan.NearColors[i].PackedWord,"Exact original near RGB owner.");
        Check(W(package,(int)(plan.FarColorAddress-plan.ModelRoot)+4*i)==plan.FarColors[i].PackedWord,"Exact original far RGB owner.");
    }
    Check(!plan.StaticDescriptorTableApplies&&!plan.IndependentColorVariantsProven&&!plan.LitPixelsVerified&&!plan.ActivationApproved&&!plan.PublicationApproved,"Admission is not rendering, independent colors or activation.");
    Refuse(plan.RequireIndependentColors,"same shared model cannot assert independent colors");Refuse(plan.RequireActivation,"source admission cannot approve activation");
    if(plan.SourceCertified)foreach(var c in plan.Consumers)
    {
        byte[] b=plan.CopyConsumer(c.Role);Check(Hash(b)==c.Sha256,"Exact consumer owner "+c.Role);b[0]^=1;
        Check(Hash(plan.CopyConsumer(c.Role))==c.Sha256,"Consumer copy does not mutate evidence.");
    }
    else Refuse(()=>plan.CopyConsumer("render-list"),"synthetic fixtures cannot issue native consumers");
}
void ExecuteNativeConsumers(NativeJapaneseEggRenderAdmissionPlan p)
{
    var cpu=new RenderProbeCpu();var integration=p.Integration;
    byte[] data=integration.CopyLevelData(),scene=integration.CopyScene(),package=integration.VisualAncestor.ModelAssembly.CopyModelPackage();
    cpu.Copy(0x8000F800,integration.CopyOriginalExecutable());cpu.Copy(p.ModelRoot,package);
    cpu.Copy(0x80076C00,data.AsSpan(0,0x800).ToArray());
    cpu.Put(0x800785E0,integration.RuntimeMemory.ModelRamStart);
    // Run the real registration loop starting at the single appended slot.
    int slot=Enumerable.Range(1,63).Single(i=>BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0x150+2*i))==0xF5);
    cpu.Regs[16]=(uint)slot;cpu.Regs[17]=0x80076C50+(uint)slot*4;cpu.Regs[18]=0x80076D50+(uint)slot*2;
    cpu.Regs[19]=0x80076C50;cpu.Regs[20]=0x80076378;cpu.Run(0x80015AE8,0x80015B34);
    Check(cpu.Get(0x80076378+0xF5*4)==p.ModelRoot,"Actual registration loop publishes the animated KSEG root.");
    int[] pointerFields=[0x34,0x38,0x50,0x54,0x58,0x5C,0x60,0x68];
    for(int i=0;i<package.Length;i++)if(!pointerFields.Any(f=>i>=f&&i<f+4))
        Check(cpu.Get(p.ModelRoot+(uint)i,1)==package[i],"Native loader preserves every non-pointer package byte.");
    Check(cpu.Writes.All(a=>a>=p.ModelRoot&&a<p.ModelRoot+package.Length||a>=0x80076378+0xF5*4&&a<0x80076378+0xF5*4+4),"Only package relocation and the F5 registration are written.");
    foreach(int field in pointerFields)
    {
        uint source=W(package,field),expected=field is 0x34 or 0x38?p.ModelRoot+source:
            field is 0x60 or 0x68?(source&0xFFE00000)|((p.ModelRoot+0x70+(source&0x1FFFFF))&0x1FFFFF):p.ModelRoot+0x70+source;
        Check(cpu.Get(p.ModelRoot+(uint)field)==expected,"Actual loader relocation field "+field.ToString("X"));
    }
    uint sceneBase=integration.RuntimeMemory.SceneRamStart;cpu.Copy(sceneBase,scene);
    int moby=integration.OriginalSceneSource.RowTableOffset-8;
    cpu.Regs[16]=sceneBase+(uint)moby;cpu.Regs[5]=cpu.Regs[16];cpu.Run(0x80013F24,0x80013FC4);
    Check(cpu.Get(0x80075828)==sceneBase+(uint)moby+8,"Native scene loader publishes actual row base.");
    Check(cpu.Get(0x80075890)==sceneBase+(uint)moby+8+(uint)integration.Rows[^1].NativeIndex*0x58+0x58,"Native sentinel follows both appended rows.");
    Check(cpu.Get(cpu.Get(0x80075890)+0x48,1)==0xFF,"Actual native row sentinel.");
    int fix=integration.OriginalSceneSource.FixupOffset+24;
    cpu.Regs[16]=sceneBase+(uint)fix;cpu.Regs[17]=0;cpu.Regs[18]=sceneBase;cpu.Writes.Clear();cpu.Run(0x80014040,0x80014084);
    var fixFields=Enumerable.Range(0,(int)W(scene,fix)).Select(i=>W(scene,fix+4+4*i)).ToArray();
    foreach(uint f in fixFields)Check(cpu.Get(sceneBase+f)==sceneBase+W(scene,(int)f),"Actual scene-relative fixup "+f.ToString("X"));
    Check(cpu.Writes.Count==fixFields.Length*4&&cpu.Writes.All(a=>fixFields.Any(f=>a>=sceneBase+f&&a<sceneBase+f+4)),"Real fixup loop changes only declared fields.");
    foreach(var row in p.Rows)
    {
        cpu.Regs[16]=0;cpu.Regs[4]=row.RowAddress;cpu.Writes.Clear();cpu.Calls.Clear();cpu.Run(0x8001417C,0x80052758);
        Check(cpu.Calls.Contains(0x800526A8)&&!cpu.Calls.Contains(0x800529CC),"Actual negative-root route selects animated initialization.");
        Check(cpu.Get(row.RowAddress+8)==0&&cpu.Get(row.RowAddress+0x34,2)==0xFFFF&&cpu.Get(row.RowAddress+0x41,1)==0x20,"Native animation speed/auxiliary fields initialized.");
        Check(cpu.Writes.All(a=>a>=row.RowAddress+8&&a<row.RowAddress+12||a>=row.RowAddress+0x34&&a<row.RowAddress+0x36||a==row.RowAddress+0x41),"Zero native auxiliary root bypasses spatial-grid insertion; no other row/private writes.");
    }
    const uint stop=0x80010000,scratch=0x8006FCF4,animated=scratch+0x2200;
    foreach(byte pod in new byte[]{0,1})
    {
        cpu.Copy(0x800771C8,Enumerable.Repeat(pod,256).ToArray());cpu.Regs[31]=stop;cpu.Reads.Clear();cpu.Run(0x800521C0,stop);
        List<uint> list=[];for(uint at=animated;cpu.Get(at)!=0;at+=4){list.Add(cpu.Get(at));if(list.Count>256)throw new Exception("Unterminated native list");}
        foreach(var row in p.Rows)Check(list.Count(a=>a==row.RowAddress)==1,"Both appended rows admitted exactly once regardless pod-active bytes.");
        Check(!cpu.Reads.Any(a=>p.Rows.Any(r=>a==r.RowAddress+0x4F||a==r.RowAddress+0x4C)),"List routing does not consume opaque row4F.");
    }
    // Only these two valid animated rows are passed to the animation test;
    // source model tables for the other174 actors are not synthetic fixtures.
    const uint listAddress=0x801E0000;
    cpu.Put(listAddress,p.Rows[0].RowAddress);cpu.Put(listAddress+4,p.Rows[1].RowAddress);cpu.Put(listAddress+8,0);
    for(int tick=0;tick<32;tick++)
    {
        cpu.Regs[4]=listAddress;cpu.Regs[5]=0;cpu.Regs[31]=stop;cpu.Writes.Clear();cpu.Calls.Clear();cpu.Run(0x800522C0,stop);
        foreach(var row in p.Rows)
        {
            Check(cpu.Get(row.RowAddress+0x3C,2)==0&&cpu.Get(row.RowAddress+0x3E,1)<2&&cpu.Get(row.RowAddress+0x3F,1)<2,"Native animation remains inside the two owned frames.");
            Check(cpu.Get(row.RowAddress+0x40,1)==(uint)((tick+1)%2*32),"Native32-unit blend advances and wraps.");
            Check(cpu.Get(row.PrivateAddress)==0&&cpu.Get(row.PrivateAddress+4)==0&&cpu.Get(row.PrivateAddress+8)==0,"Animation never changes independently allocated private state.");
        }
        Check(!cpu.Calls.Contains(0x80055A78),"Native FF frame event tags do not call the event dispatcher.");
        Check(cpu.Writes.All(a=>a>=0x1F800000&&a<0x1F800008||a>=scratch+4&&a<scratch+8||p.Rows.Any(r=>a>=r.RowAddress+0x3C&&a<r.RowAddress+0x44)),"Animation writes only8 row bytes,8 scratchpad bytes and event staging pointer.");
    }
    foreach(var row in p.Rows)foreach(byte blend in new byte[]{0,32})
    {
        cpu.Put(row.RowAddress+0x40,blend,1);cpu.Regs[29]=row.RowAddress;cpu.Regs[28]=0x80076378;
        cpu.Run(0x80020A14,0x80020A38,true);
        Check(cpu.Regs[15]==p.ModelRoot&&cpu.BoundaryLoad==(13,p.AnimationHeader),"Real selection reads class F5 and animation0; final LW remains explicitly delayed before GTE.");
        // Finish selection's pending load for a separate draw-command probe.
        // Camera/frustum/GTE outputs are prescribed inputs, NOT simulated visibility.
        cpu.Regs[13]=p.AnimationHeader;cpu.Regs[30]=scratch+0x1600;cpu.Regs[12]=100;
        cpu.Regs[1]=(uint)placements.Single(r=>r.Id==row.Id).X;cpu.Regs[2]=75000;cpu.Regs[3]=6500;cpu.Regs[4]=0;
        cpu.Writes.Clear();cpu.Reads.Clear();cpu.Run(0x80020BB0,0x80020CAC);
        uint command=scratch+0x1600;
        Check(cpu.Get(command+4)==p.AnimationHeader&&cpu.Get(command+8)==p.AnimationHeader+0x24+cpu.Get(row.RowAddress+0x3E,1)*8,"Draw command references owned current frame pair.");
        Check(cpu.Get(command+12)==(blend==0?0:p.AnimationHeader+0x24+cpu.Get(row.RowAddress+0x3F,1)*8),"Blend references only the owned next frame pair.");
        Check(cpu.Get(command+0x30)==cpu.Get(row.RowAddress+0x4C)&&cpu.Get(command+0x34)==row.RowAddress,"Opaque mode and row identity propagate without redirecting model owners.");
        Check(cpu.Get(command+0x10)==(uint)placements.Single(r=>r.Id==row.Id).X,"Prescribed per-row position remains a command value, not a pointer.");
        Check(cpu.Writes.All(a=>a>=command&&a<command+0x38||a==row.RowAddress+0x51),"Command emission touches only command owner and current row visible flag.");
        Check(!cpu.Reads.Any(a=>a>=0x8006E3D8&&a<0x8006E458),"Animated command path never selects the static descriptor table.");
    }
    foreach(var c in p.NearColors)Check(cpu.Get(p.NearColorAddress+(uint)c.Index*4)==c.PackedWord,"Original near model RGB preserved through every native probe.");
    foreach(var c in p.FarColors)Check(cpu.Get(p.FarColorAddress+(uint)c.Index*4)==c.PackedWord,"Original far model RGB preserved through every native probe.");
    for(int i=0;i<p.NearColors.Count;i++)
    {
        cpu.Regs[1]=p.NearColorAddress+(uint)i*4;cpu.Regs[2]=p.FarColorAddress+(uint)i*4;cpu.Regs[31]=p.AnimationHeader;
        cpu.Writes.Clear();cpu.Run(0x800215E4,0x80021610);
        var far=p.FarColors[i];
        Check(cpu.Regs[5]==p.NearColors[i].PackedWord,"Actual shading prefix loads near word into RGBC input.");
        Check(cpu.Regs[6]==far.Red*16u&&cpu.Regs[7]==far.Green*16u&&cpu.Regs[8]==far.Blue*16u,"Actual instructions decode far R/G/B low24 into12-bit FC inputs.");
        Check(cpu.Writes.Count==0,"RGB decode prefix changes no source arrays or unrelated owners.");
    }
    byte[] shading=p.CopyConsumer("animated-shading");
    Check(W(shading,0)==0x8FE10018&&W(shading,4)==0x8FE20020&&W(shading,0x64)==0x4A780010,"Actual near/far RGB owners feed the pinned native DPCS path.");
    Check(p.NearColors.Count==70&&p.FarColors.Count==70&&p.NearColors.All(c=>c.Code==0xFF)&&p.FarColors.All(c=>c.Code==0),"Exact source packed RGB CODE bytes, not alpha or normals.");
}
if(args.Length==3&&args[0]=="--source")
{
    using var jp=NativeRegionalSourceSession.Open(args[1],NativeDiscRevision.JapanScps10083);
    using var usa=NativeRegionalSourceSession.Open(args[2],NativeDiscRevision.UsaScus94228);
    var integration=NativeJapaneseEggArtisansIntegrationPlan.Capture(jp,usa,placements);
    var plan=NativeJapaneseEggRenderAdmissionPlan.Capture(usa,integration);Verify(plan);ExecuteNativeConsumers(plan);
    Refuse(()=>NativeJapaneseEggRenderAdmissionPlan.Capture(jp,integration),"wrong regional issuer");
    jp.VerifyUnchanged();usa.VerifyUnchanged();Console.WriteLine($"JP_EGG_RENDER_ADMISSION_SOURCE_OK checks={checks} consumers={plan.Consumers.Count} litPixelsVerified=false independentColors=false activationApproved=false");
}
else if(args.Length==0)
{
    byte[] model=Fixtures.Model();Fixtures.Put(model,0x70,8);Fixtures.Put(model,0x7C,8); // stream lengths, not face flags
    var asset=NativeJapaneseEggModelAssetPlan.Fixture(model);byte[] original=Fixtures.Resource(),donor=Fixtures.Scene(0xF5);
    int row=0x88+7*8+8;donor[row+0x4A]=0xFF;donor[row+0x4B]=0x94;donor[row+0x47]=4;donor[row+0x50]=0x28;
    var visual=NativeJapaneseEggVisualBehaviorPlan.Fixture(asset,original,Fixtures.JapaneseOverlay(),Fixtures.UsaOverlay(),Fixtures.Executable());
    NativeJapaneseEggArtisansIntegrationPlan Build()=>NativeJapaneseEggArtisansIntegrationPlan.Fixture(asset,visual,original,donor,NativeJapaneseObjectAppearance.DecodeFixture(Fixtures.Profiles()),placements);
    var plan=NativeJapaneseEggRenderAdmissionPlan.Fixture(Build());Verify(plan);
    foreach(int at in new[]{0x3C,0x3D,0x3E,0x3F,0x40,0x47,0x48,0x4A,0x4B,0x50})
    {
        byte before=donor[row+at];donor[row+at]=at is 0x3E or 0x3F? (byte)2:at==0x40?(byte)64:(byte)(before^1);
        Refuse(()=>NativeJapaneseEggRenderAdmissionPlan.Fixture(Build()),"unadmitted source row field "+at.ToString("X"));donor[row+at]=before;
    }
    Check(!plan.SourceCertified&&!plan.AnimatedListAndModelOwnersProven,"Fixture cannot issue native admission.");
    Console.WriteLine($"JP_EGG_RENDER_ADMISSION_SOURCE_FREE_OK checks={checks}");
}
else throw new ArgumentException("Usage: no arguments, or --source <exact-JP-BIN> <exact-USA-BIN>.");

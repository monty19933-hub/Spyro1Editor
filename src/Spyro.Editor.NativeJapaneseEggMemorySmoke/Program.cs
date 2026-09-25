using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks=0;
void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
void Refuse(Action action,string why){try{action();}catch(InvalidDataException){checks++;return;}throw new Exception("Expected refusal: "+why);}
NativeJapaneseEggMemoryReport.Metrics Measure(uint copy=0x80089238,uint used=0x8D31C,uint dataRead=0x8D800,
    uint model=0x4E800,uint scene=0x14800,int adapter=0x2B0)=>
    NativeJapaneseEggMemoryReport.Measure(copy,used,dataRead,model,scene,0x3A000,0x39C48,0x200000,0x800,adapter);
var expected=Measure();
Check(expected.SceneEnd==0x80179554&&expected.NormalReservedStart==0x80187BB0,"independent final resident golden");
Check(expected.TransferPeakEnd==expected.SceneEnd&&expected.NormalGap==0xE65C,"complete CD extents and staging fit");
Check(expected.PeteTemporaryStart==0x8018B800&&expected.PeteReadEnd==0x801C5800,"PETE placement uses directory length, not runtime copy length");
Check(expected.PeteCopySourceEnd==0x801C5C48&&expected.PeteReadOverhang==0x448,"native PETE overread is retained explicitly, not zero-filled");
Check(expected.PeteActualCopyReadEnd==0x801C5C50&&expected.PeteActualReadOverlap==0x98,"actual forward-copy prefetch includes final8 lookahead and source/destination overlap");
Check(expected.PeteResidentStart==0x801C5BB8,"native resident length differs from CD length");
Check(expected.BootStackPointer==0x801FFFF8&&expected.DispatchStackPointer==0x801FFDF8,"real main/update/dispatch stack chain");
Check(expected.VisualTickStackLow==0x801FFB20&&expected.StackMargin==0x320,"synchronous visual call stack fits reserved top800");
Check(expected.ModeledExtentsFit,"geometric result is positive without certification");
Check(Measure(adapter:0x5D0).StackMargin==0,"exact stack boundary is distinct from overflow");
Check(!Measure(adapter:0x5D4).ModeledExtentsFit,"one word past top-stack reservation refuses fit");
Check(!Measure(copy:0x800A0000).ModeledExtentsFit,"growing below code until data hits normal buffers refuses fit");
Refuse(()=>Measure(copy:0x80089239),"unaligned base");
Refuse(()=>Measure(dataRead:0x8D000),"used data leaves full CD read");
Refuse(()=>Measure(model:0x4E801),"unaligned full CD read");
Refuse(()=>Measure(adapter:-4),"negative adapter frame");
Refuse(()=>Measure(adapter:int.MaxValue),"malformed/overflowing frame");
Refuse(()=>Measure(model:0xFFFFFFFF),"overflowing CD length");
if(args.Length==0)
{
    Console.WriteLine($"JP_EGG_MEMORY_SOURCE_FREE_OK checks={checks} sourceReads=0 nativeCertified=false");
    return;
}
if(args.Length!=3||args[0]!="--source")throw new ArgumentException("Usage: no arguments or --source <JP-BIN> <USA-BIN>");
using var jp=NativeRegionalSourceSession.Open(args[1],NativeDiscRevision.JapanScps10083);
using var usa=NativeRegionalSourceSession.Open(args[2],NativeDiscRevision.UsaScus94228);
var plan=NativeJapaneseEggArtisansIntegrationPlan.Capture(jp,usa,
    [new("egg-a",82000,75000,6500,32,1),new("egg-b",83024,75000,6500,96,1)]);
var report=NativeJapaneseEggMemoryReport.Capture(usa,plan);
Check(report.Values==expected,"actual source and exact integrated data agree with independent arithmetic");
Check(report.SourceCertified&&report.ModeledLoadExtentsFitProven&&report.SynchronousVisualStackFitProven,"only source-proved bounded properties are admitted");
Check(!report.FullRuntimePeakProven&&!report.PublicationApproved,"modeled static phases never certify interrupts or gameplay");
Check(report.IntegrationPlanSha256==plan.PlanSha256,"report binds actual authored plan identity");
byte[] exe=plan.CopyExecutable(),overlay=plan.CopyOverlay();
var cpu=new TinyR3000();cpu.Copy(0x8000F800,exe);cpu.Copy(0x8007AA38,overlay);
cpu.Run(0x8005B904,0x8005B91C);
Check(cpu.Regs[29]==expected.BootStackPointer,"execute native boot stack binding");
cpu.Run(0x80012204,0x80012214);cpu.Run(0x80012230,0x8003385C);
Check(cpu.Regs[31]==0x80012238,"native main update JAL has no hidden intermediate frame");
cpu.Run(0x8003385C,0x8003386C);cpu.Run(0x8007D9C8,0x8007D9F0);
Check(cpu.Regs[29]==expected.DispatchStackPointer,"execute actual nested synchronous prologues without skipped hidden frames");
// Execute real PETE argument construction, stopping BEFORE the hardware call.
cpu.Put(0x8007A714,0x3A000);cpu.Put(0x8007A710,0x7B8800);cpu.Put(0x80076B90,37);
cpu.Regs[29]=expected.BootStackPointer-0x40;cpu.Run(0x8005B7D8,0x8005B83C);
Check(cpu.Regs[16]==expected.PeteTemporaryStart&&cpu.Regs[6]==0x3A000,"native temporary CD argument uses directory size");
cpu.Put(expected.PeteTemporaryStart,0x39C48);cpu.Run(0x8005B844,0x80016958);
Check(cpu.Regs[4]==expected.PeteResidentStart&&cpu.Regs[5]==expected.PeteTemporaryStart+0x800&&cpu.Regs[6]==0x39C48,
    "native copy preserves source overhang and exact resident argument, not an invented bulk-copy size");
// The real short final copy group loads16 before conditionally storing8.
for(int i=0;i<4;i++)cpu.Put(0x80100000+(uint)(4*i),0xABC00000u+(uint)i);
cpu.Regs[4]=0x80101000;cpu.Regs[5]=0x80100000;cpu.Regs[6]=8;cpu.Regs[31]=0x80012340;
cpu.Writes.Clear();cpu.Run(0x80016958,0x80012340);
Check(cpu.Regs[12]==0xABC00002&&cpu.Regs[13]==0xABC00003&&cpu.Regs[5]==0x80100010,
    "native8-byte final store fetches two additional words");
Check(cpu.Writes.Count==8&&cpu.Writes.All(a=>a>=0x80101000&&a<0x80101008),"copy lookahead is a read, not an extra output write");
// Use each relocated scene owner with the real caller SP and source helper instructions.
byte[] sceneBytes=plan.CopyScene();uint sceneBase=plan.RuntimeMemory.SceneRamStart;cpu.Copy(sceneBase,sceneBytes);
foreach(var row in plan.Rows)
{
    uint rowAddress=sceneBase+(uint)row.RowSceneOffset,privateAddress=sceneBase+(uint)row.PrivateSceneOffset;
    cpu.Put(rowAddress,privateAddress);cpu.Put(0x800756C4,1);cpu.Put(0x80075AC0,0x12345678);
    for(int tick=0;tick<32;tick++)
    {
        cpu.Regs[3]=0xF5;cpu.Regs[19]=rowAddress;cpu.Regs[29]=expected.DispatchStackPointer;
        cpu.Writes.Clear();cpu.Run(plan.GuardPc,0x80085780);
        Check(cpu.Regs[29]==expected.DispatchStackPointer,"actual visual entry/exit preserves real caller SP");
        uint[] stackWrites=cpu.Writes.Where(a=>a>=0x801FF000).ToArray();
        Check(stackWrites.Length>0&&stackWrites.All(a=>a>=expected.VisualTickStackLow&&a<expected.DispatchStackPointer),"all observed adapter/helper stack stores fit declared source chain");
    }
}
byte[] before=plan.CopyOriginalExecutable();before[0x4BFDC]^=1;
Refuse(()=>NativeJapaneseEggMemoryReport.RequireSourceExecutable(before),"changed native memory/stack instruction owner");
jp.VerifyUnchanged();usa.VerifyUnchanged();
Console.WriteLine($"JP_EGG_MEMORY_SOURCE_OK checks={checks} normalGap={expected.NormalGap:X} synchronousStackMargin={expected.StackMargin:X} publicationApproved=false");

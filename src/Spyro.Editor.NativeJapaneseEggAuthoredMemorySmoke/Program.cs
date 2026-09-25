using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks=0;
void Check(bool yes,string why){if(!yes)throw new Exception(why);checks++;}
var expected=NativeJapaneseEggMemoryReport.Measure(0x80089238,0x8D31C,0x8D800,
    0x50000,0x14800,0x3A000,0x39C48,0x200000,0x800,0x2B0);
Check(expected.SceneEnd==0x8017AD54 && expected.NormalReservedStart==0x80187BB0,
    "Final larger resource is charged against the unchanged normal frame floor.");
Check(expected.NormalGap==0xCE5C && expected.TransferPeakEnd==expected.SceneEnd && expected.ModeledExtentsFit,
    "Extra1800 reduces the old gap rather than inheriting it.");
Check(expected.VisualTickStackLow==0x801FFB20 && expected.StackMargin==0x320,
    "Class comparisons do not add a visual stack frame.");
Check(expected.PeteActualCopyReadEnd==0x801C5C50 && expected.PeteActualReadOverlap==0x98,
    "Existing PETE overlap/lookahead remains explicit.");
var overflow=NativeJapaneseEggMemoryReport.Measure(0x80089238,0x8D31C,0x8D800,
    0x5D000,0x14800,0x3A000,0x39C48,0x200000,0x800,0x2B0);
Check(!overflow.ModeledExtentsFit && overflow.NormalGap<0,"Larger model footprint cannot reuse the ancestor pass.");
Check(typeof(NativeJapaneseEggAuthoredMemoryReport).GetConstructors().Length==0,
    "Caller geometry or metadata cannot issue source-bound memory authority.");
if(args.Length==0)
{
    Console.WriteLine($"JP_EGG_AUTHORED_MEMORY_SOURCE_FREE_OK checks={checks} sourceReads=0");
    return;
}
if(args.Length!=3 || args[0]!="--source")
    throw new ArgumentException("Usage: no arguments or --source JP.bin USA.bin.");
using var jp=NativeRegionalSourceSession.Open(args[1],NativeDiscRevision.JapanScps10083);
using var usa=NativeRegionalSourceSession.Open(args[2],NativeDiscRevision.UsaScus94228);
try
{
    var placement=NativeJapaneseEggArtisansPlacementPlan.Capture(usa);
    var integration=NativeJapaneseEggArtisansIntegrationPlan.Capture(jp,usa,placement.Placements);
    placement.RequireIntegration(integration);
    var asset=NativeJapaneseEggModelAssetPlan.Capture(jp,usa);
    var rgb=NativeJapaneseEggRgbEditSource.Capture(asset,usa);
    var a=rgb.CreateAuthored(Enumerable.Repeat(0x332211u,70).ToArray(),Enumerable.Repeat(0x665544u,70).ToArray());
    var b=rgb.CreateAuthored(Enumerable.Repeat(0xCCBBAAu,70).ToArray(),Enumerable.Repeat(0xFFEEDDu,70).ToArray());
    var plan=NativeJapaneseEggAuthoredClassesPlan.Capture(usa,NativeJapaneseEggAuthoredClassReservation.Capture(usa),integration,a,b);
    var report=NativeJapaneseEggAuthoredMemoryReport.Capture(usa,plan);
    Check(report.Values==expected && report.PlanSha256==plan.PlanSha256 &&
        report.FinalDataSha256==plan.FinalDataSha256 && report.FinalOverlaySha256==plan.FinalOverlaySha256,
        "Source report binds this actual complete registered composition, not its smaller ancestor.");
    Check(report.ModeledLoadExtentsFitProven && report.SynchronousVisualStackFitProven &&
        !report.FullRuntimePeakProven && !report.PublicationApproved,"Bounded evidence is not full gameplay or publication approval.");
    Check(report.Ancestor.Values.NormalGap-report.Values.NormalGap==0x1800,"Exact extra memory charged.");
    var cpu=new TinyR3000();
    cpu.Copy(0x8000F800,plan.CopyExecutable());cpu.Copy(0x8007AA38,plan.CopyOverlay());
    cpu.Copy(plan.Projection.ProjectedSceneRamStart,plan.CopyScene());
    foreach(var row in plan.Bindings)
    {
        cpu.Put(row.ProjectedRowAddress,row.ProjectedPrivateAddress);
        cpu.Put(0x800756C4,1);cpu.Put(0x80075AC0,0x12345678);
        for(int tick=0;tick<32;tick++)
        {
            cpu.Regs[3]=row.AuthoredDestinationClass;cpu.Regs[19]=row.ProjectedRowAddress;
            cpu.Regs[29]=expected.DispatchStackPointer;cpu.Writes.Clear();
            cpu.Run(plan.GuardPc,0x80085780);
            Check(cpu.Regs[29]==expected.DispatchStackPointer,"Both authored visual routes restore real caller SP.");
            uint[] stack=cpu.Writes.Where(p=>p>=0x801FF000).ToArray();
            Check(stack.Length>0&&stack.All(p=>p>=expected.VisualTickStackLow&&p<expected.DispatchStackPointer),
                "All observed adapter/helper stack stores stay within the source-bound synchronous chain.");
            Check(cpu.Writes.All(p=>p>=expected.VisualTickStackLow&&p<expected.DispatchStackPointer ||
                p>=row.ProjectedRowAddress&&p<row.ProjectedRowAddress+0x58 ||
                p>=row.ProjectedPrivateAddress&&p<row.ProjectedPrivateAddress+12 ||
                p>=0x80075AC0&&p<0x80075AC4),"Visual tick stores have explicit row/private/stack/RNG owners.");
        }
    }
}
finally { jp.VerifyUnchanged();usa.VerifyUnchanged(); }
Console.WriteLine($"JP_EGG_AUTHORED_MEMORY_SOURCE_OK checks={checks} normalGap={expected.NormalGap:X} stackMargin={expected.StackMargin:X} runtime=false");

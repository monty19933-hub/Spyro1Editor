using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks=0;
void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
void Refuse(Action action,string why){try{action();}catch(InvalidDataException){checks++;return;}throw new Exception("Expected refusal: "+why);}
var kernel=NativeCollectibleSaveSchedulerKernel.CreateFixture();
Check(kernel.MainCodeByteLength<=0x5C0&&kernel.FinalCodeByteLength+0x40<=0x208,"Both actual owner capacities enforced");
Check(kernel.ExternalExclusionRequired&&!kernel.CacheCoherenceProven&&!kernel.NativeIoExecuted&&!kernel.RuntimeVerified&&
    !kernel.PublicationApproved&&!kernel.LiveCommitEmitted&&!kernel.RearmEmitted,"No runtime/cache/IRQ/commit/rearm authority");
Refuse(()=>NativeCollectibleSaveSchedulerPlan.Capture(null!,null!,null!),"No arbitrary fixture publication authority");
SchedulerCases.Run(kernel,null,Check);
if(args.Length!=0)SourceCases.Run(args,Check,Refuse);
Console.WriteLine($"SCHEDULER_LAYOUT main={kernel.MainCodeByteLength:X} final={kernel.FinalCodeByteLength:X} state=40");
Console.WriteLine($"NATIVE_COLLECTIBLE_SAVE_SCHEDULER_OK checks={checks} source={args.Length!=0} nativeIo=false imageWrites=0 cardWrites=0 runtimeVerified=false");

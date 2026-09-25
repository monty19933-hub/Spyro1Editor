using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks=0;
void Check(bool condition,string reason){if(!condition)throw new Exception(reason);checks++;}
void Refuse(Action action,string reason){try{action();}catch(InvalidDataException){checks++;return;}catch(ArgumentException){checks++;return;}throw new Exception("Expected refusal: "+reason);}
const uint table=0x80076378,slot=table+0xF5*4,stop=0x80010000;
// These are disjoint synthetic probe owners, NOT an actual level RAM allocation.
const uint firstRoot=0x80110000,secondRoot=0x80111000,rowA=0x80120000,rowB=0x80120100,list=0x80121000,command=0x80122000;
static uint W(byte[] b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
void Execute(byte[] exe,NativeJapaneseEggModelAssetPlan asset,NativeJapaneseEggRgbEditPlan first,NativeJapaneseEggRgbEditPlan second)
{
    var cpu=new RenderProbeCpu();cpu.Copy(0x8000F800,exe);
    byte[] a=first.CopyPackage(),b=second.CopyPackage();
    foreach(var (root,bytes) in new[]{(firstRoot,a),(secondRoot,b)})
    {
        cpu.Copy(root,bytes);cpu.Regs[4]=root;cpu.Regs[31]=stop;cpu.Writes.Clear();cpu.Run(0x800133E0,stop);
        Check(cpu.Regs[2]==root,"Actual loader returns this unregistered animated root.");
        foreach(var pointer in asset.ProjectLoaderAddresses(root))
            Check(cpu.Get(root+(uint)pointer.PackageFieldOffset)==pointer.ProjectedLoadedWord,"Actual loader rebases each preserved pointer into its own model owner.");
        var owned=asset.PointerFields.SelectMany(p=>Enumerable.Range(p.PackageFieldOffset,4)).ToHashSet();
        for(int i=0;i<bytes.Length;i++)if(!owned.Contains(i))Check(cpu.Get(root+(uint)i,1)==bytes[i],"Native loader preserves RGB and all non-pointer bytes.");
        Check(cpu.Writes.All(p=>p>=root&&p<root+bytes.Length),"Loading an unregistered root never writes the global class table.");
    }
    // The actual registration store has one address per class. A second F5
    // declaration replaces the first; it does not associate roots with rows.
    cpu.Put(list,0xF5,2);cpu.Regs[18]=list;cpu.Regs[20]=table;
    foreach(uint root in new[]{firstRoot,secondRoot})
    {
        cpu.Regs[2]=root;cpu.Writes.Clear();cpu.Run(0x80015B14,0x80015B28);
        Check(cpu.Get(slot)==root&&cpu.Writes.SequenceEqual(Enumerable.Range(0,4).Select(i=>slot+(uint)i)),"Duplicate F5 registration is last-writer-wins in the shared slot.");
    }
    cpu.Put(slot,firstRoot);
    foreach(uint row in new[]{rowA,rowB})
    {
        cpu.Put(row+0x36,0xF5,2);cpu.Put(row+0x3C,0);cpu.Put(row+0x40,0x2000);
        cpu.Put(row,secondRoot);cpu.Put(row+4,secondRoot);cpu.Put(row+8,secondRoot);cpu.Put(row+0x30,secondRoot);
        cpu.Regs[4]=row;cpu.Reads.Clear();cpu.Writes.Clear();cpu.Run(0x800526A8,0x80052758);
        Check(cpu.Reads.Contains(slot)&&cpu.Get(row+8)==0,"Initializer re-resolves the class root and overwrites row+08 with the model's zero auxiliary pointer.");
        Check(!cpu.Reads.Contains(row)&&!cpu.Reads.Contains(row+4)&&!cpu.Reads.Contains(row+0x30),"Private/list fields are not initializer model selectors.");
        Check(cpu.Get(row+0x41,1)==32&&cpu.Get(row+0x34,2)==0xFFFF,"RGB-only models keep canonical animation initialization.");
        cpu.Regs[29]=row;cpu.Regs[28]=table;cpu.Reads.Clear();cpu.Writes.Clear();cpu.Run(0x80020A14,0x80020A38,true);
        Check(cpu.Regs[15]==firstRoot&&cpu.BoundaryLoad==(13,firstRoot+0x3C),"Both rows select the shared root despite planted independent pointers.");
        Check(cpu.Reads.SequenceEqual(new[]{row+0x36,row+0x3C,slot,firstRoot+0x38})&&cpu.Writes.Count==0,"Entire native root/animation selector reads only class, animation and shared model owners.");
    }
    // Force both native animation branches. The RGB-only contract preserves
    // every header/frame byte, so each branch deliberately keeps canonical data.
    cpu.Put(list,rowA);cpu.Put(list+4,rowB);cpu.Put(list+8,0);
    foreach(bool transition in new[]{false,true})
    {
        foreach(uint row in new[]{rowA,rowB})
        {cpu.Put(row+0x3C,transition?1u:0u);cpu.Put(row+0x40,0x203F);}
        cpu.Regs[4]=list;cpu.Regs[5]=0;cpu.Regs[31]=stop;cpu.Reads.Clear();cpu.Writes.Clear();cpu.Visited.Clear();cpu.Run(0x800522C0,stop);
        Check(cpu.Visited.Contains(transition?0x800523ACu:0x8005235Cu),"Execute actual "+(transition?"animation transition":"frame wrap")+" root lookup.");
        Check(cpu.Reads.Count(p=>p==slot)==2&&!cpu.Reads.Any(p=>p>=secondRoot&&p<secondRoot+b.Length),"Both native animation paths remain canonical for both rows.");
        Check(!cpu.Writes.Any(p=>p>=table&&p<table+512*4),"Animation does not overwrite class roots.");
    }
    // This is a HYPOTHETICAL hook ABI probe, not installed code: inject only
    // t7 at the documented join, then execute the unchanged native suffix.
    foreach(var (row,root,bytes) in new[]{(rowA,firstRoot,a),(rowB,secondRoot,b)})
    foreach(byte blend in new byte[]{0,32})
    {
        cpu.Put(row+0x3C,0);cpu.Put(row+0x40,blend,1);cpu.Put(row+0x3F,1,1);
        cpu.Put(row+0x4C,0x01000000);cpu.Regs[29]=row;cpu.Regs[28]=table;
        cpu.Run(0x80020A14,0x80020A28,true);
        Check(cpu.BoundaryLoad==(15,firstRoot),"Native prefix's class lookup has an explicit pending load at the proposed join.");
        cpu.Regs[15]=root; // prescribed future selector result; no native hook claimed
        cpu.Reads.Clear();cpu.Writes.Clear();cpu.Run(0x80020A28,0x80020A38,true);
        Check(cpu.BoundaryLoad==(13,root+0x3C),"Prescribed per-row root selects its independent animation header in the unchanged suffix.");
        cpu.Regs[13]=root+0x3C;cpu.Regs[30]=command;cpu.Regs[12]=100;
        cpu.Regs[1]=72704;cpu.Regs[2]=row==rowA?47104u:49152u;cpu.Regs[3]=6656;cpu.Regs[4]=0;
        cpu.Run(0x80020BB0,0x80020CAC);
        Check(cpu.Get(command+4)==root+0x3C&&cpu.Get(command+8)==root+0x60,"Native command captures this selected root's current frame ownership.");
        Check(cpu.Get(command+12)==(blend==0?0:root+0x68)&&cpu.Get(command+0x34)==row,"Next frame and row identity remain independent.");
        Check(cpu.Get(slot)==firstRoot&&!cpu.Writes.Any(p=>p>=table&&p<table+512*4),"Proposed join result needs no class-table mutation.");
        uint near=root+(uint)first.Source.NearRange.PackageOffset,far=root+(uint)first.Source.FarRange.PackageOffset;
        for(int i=0;i<70;i++)
        {
            cpu.Regs[1]=near+(uint)i*4;cpu.Regs[2]=far+(uint)i*4;cpu.Regs[31]=root+0x3C;cpu.Run(0x800215E4,0x80021610);
            uint nw=W(bytes,first.Source.NearRange.PackageOffset+i*4),fw=W(bytes,first.Source.FarRange.PackageOffset+i*4);
            Check(cpu.Regs[5]==nw&&cpu.Regs[6]==(fw&255)*16&&cpu.Regs[7]==((fw>>8)&255)*16&&cpu.Regs[8]==((fw>>16)&255)*16,"Native RGB prefix consumes selected model colors after the prescribed join.");
        }
    }
    Check(a.SequenceEqual(first.CopyPackage())&&b.SequenceEqual(second.CopyPackage()),"Source-form detached variants survive probes unchanged.");
}

if(args.Length==0)
{
    Check(typeof(NativeJapaneseEggInstanceRootResolution).GetConstructors(BindingFlags.Public|BindingFlags.Instance).Length==0,"No public constructor can forge consumer authority.");
    Refuse(()=>NativeJapaneseEggInstanceRootResolution.Capture(null!,null!,null!),"null source ancestry");
    Check(slot==0x8007674C,"F5 has exactly one native class-table slot.");
    Check(NativeJapaneseEggInstanceRootResolution.F5ClassSlotAddress==slot,"Typed F5 slot agrees with independent address arithmetic.");
    Check(NativeJapaneseEggInstanceRootResolution.RequiredRenderInterceptPc+NativeJapaneseEggInstanceRootResolution.RequiredDisplacedByteLength==NativeJapaneseEggInstanceRootResolution.RequiredRenderJoinPc,"Proposed hook resumes immediately after the displaced class resolution.");
    Check(firstRoot+0x808<=secondRoot&&secondRoot+0x808<=rowA,"Probe package owners are disjoint; this is not a level allocation.");
    Console.WriteLine($"JP_EGG_INSTANCE_ROOT_SOURCE_FREE_OK checks={checks} sourceReads=0 nativeBinding=false");
}
else if(args.Length==3&&args[0]=="--source")
{
    using var jp=NativeRegionalSourceSession.Open(args[1],NativeDiscRevision.JapanScps10083);
    using var usa=NativeRegionalSourceSession.Open(args[2],NativeDiscRevision.UsaScus94228);
    var asset=NativeJapaneseEggModelAssetPlan.Capture(jp,usa);var source=NativeJapaneseEggRgbEditSource.Capture(asset,usa);
    var first=source.NativeOriginal();var second=source.CreateAuthored(Enumerable.Repeat(0x332211u,70).ToArray(),Enumerable.Repeat(0x665544u,70).ToArray());
    var plan=NativeJapaneseEggInstanceRootResolution.Capture(usa,first,second);
    Check(plan.SourceConsumersCertified&&plan.RgbOnlyTopologyPreserved&&plan.CoveredNativeConsumersUseSharedClassRoot&&plan.DistinctRequestedContents==2,"Actual pair is distinct RGB data but has only the shared native root resolution.");
    Check(!plan.NativePerInstanceRootBindingProven&&!plan.VariantResidencyOwned&&!plan.SelectorHookImplemented&&!plan.FinalPixelsVerified&&!plan.PublicationApproved,"Negative native binding is not relabeled as a detached hook/allocation implementation.");
    Check(plan.RootLookups.Count==5&&plan.Consumers.Count==9&&plan.RequiredOwners.Count==6,"Exact scoped consumers and remaining owner obligations.");
    Refuse(plan.RequireNativePerInstanceBinding,"uninstalled selector");Refuse(()=>plan.CopyConsumer("absent"),"unknown consumer");
    Refuse(()=>NativeJapaneseEggInstanceRootResolution.Capture(jp,first,second),"wrong-region runtime");
    Refuse(()=>NativeJapaneseEggInstanceRootResolution.Capture(usa,first,NativeJapaneseEggRgbEditSource.Capture(asset,usa).NativeOriginal()),"separately reissued RGB ancestry");
    Refuse(()=>NativeJapaneseEggInstanceRootResolution.Capture(usa,null!,second),"missing first variant");
    Refuse(()=>NativeJapaneseEggInstanceRootResolution.Capture(usa,first,null!),"missing second variant");
    foreach(var owner in plan.Consumers)
    {
        byte[] bytes=plan.CopyConsumer(owner.Role);Check(Convert.ToHexStringLower(SHA256.HashData(bytes))==owner.Sha256,"Exact source instruction window "+owner.Role);
        bytes[0]^=1;Check(Convert.ToHexStringLower(SHA256.HashData(plan.CopyConsumer(owner.Role)))==owner.Sha256,"Copied instruction evidence cannot mutate the report.");
    }
    Check(plan.PlanSha256==NativeJapaneseEggInstanceRootResolution.Capture(usa,first,second).PlanSha256,"Resolution identity is deterministic.");
    Check(NativeJapaneseEggInstanceRootResolution.Capture(usa,first,source.NativeOriginal()).DistinctRequestedContents==1,"Identical requests deduplicate content without asserting two native roots.");
    Execute(usa.ReadExecutableRange(0,usa.ExecutableByteLength),asset,first,second);
    jp.VerifyUnchanged();usa.VerifyUnchanged();
    Console.WriteLine($"JP_EGG_INSTANCE_ROOT_SOURCE_OK checks={checks} nativeBinding=false selectorOutputOnly=true sourceOrCandidateWrites=0");
}
else throw new ArgumentException("Usage: no arguments, or --source <exact-JP-BIN> <exact-USA-BIN>.");

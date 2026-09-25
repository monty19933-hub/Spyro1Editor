using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks = 0;
void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
void Refuse(Action action, string reason)
{ try { action(); } catch (InvalidDataException) { checks++; return; } throw new Exception("Expected refusal: " + reason); }
static uint Word(byte[] b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
static void Put(byte[] b, int at, uint w) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at), w);
static byte[] Words(params uint[] words)
{ byte[] b = new byte[words.Length * 4]; for (int i = 0; i < words.Length; i++) Put(b, i * 4, words[i]); return b; }
static byte[] ModelFixture()
{
    byte[] b = new byte[0xA0]; Put(b, 0, 1);
    for (int i = 4; i < 0x14; i += 4) Put(b, i, uint.MaxValue);
    Put(b, 0x34, 0x70); Put(b, 0x38, 0x3C); Put(b, 0x3C, 0x10002); b[0x42] = 2; b[0x44] = b[0x45] = 3;
    Put(b, 0x54, 0x18); Put(b, 0x58, 0xC); Put(b, 0x5C, 0x1C);
    Put(b, 0x60, 0xFF000020); Put(b, 0x64, 0x1000000); Put(b, 0x68, 0xFF000028); Put(b, 0x6C, 0x1000000);
    Put(b, 0x70, 8); Put(b, 0x74, (1u << 13) | (2u << 4)); Put(b, 0x7C, 8); Put(b, 0x80, (1u << 13) | (2u << 4));
    Put(b, 0x88, 0xFF010203); Put(b, 0x8C, 0x10203); Put(b, 0x90, 1); Put(b, 0x94, 0x20003); Put(b, 0x98, 1); Put(b, 0x9C, 0x20003); return b;
}
static byte[] ResourceFixture()
{
    byte[] b = new byte[0x3000];
    for (int i = 0; i < 5; i++) { Put(b, i * 8, (uint)(0x800 + i * 0x800)); Put(b, i * 8 + 4, 0x800); }
    Put(b, 0x50, 0x1800); return b;
}
static byte[] OverlayFixture()
{
    byte[] b = new byte[0xF800];
    Words(0x92620049, 0x8E700000, 0x14400031, 0).CopyTo(b, 0x93F4);
    Words(0x8E620014, 0, 0xAE020000).CopyTo(b, 0x949C);
    Words(0x24020001,0xA2620049,0x8E040004,0x0C00588F,0,0x8E030000,0x00021183,0x00621821,
        0xAE630014,0x8E020004,0,0x24420080,0xAE020004,0x92630049,0x240A0001,0x246200FF,
        0x146A0009,0xA2620049,0x0C019D6A,0,0x3042001F,0x24420010,0x0C019D6A,0xA2620049,
        0x304200FF,0xAE020008,0x02602021,0x24060008,0xAFA00010,0x8E050008,0x0C00F12D,0x24070008).CopyTo(b,0x94BC);
    return b;
}
static byte[] ExecutableFixture()
{
    byte[] b = new byte[0x66000];
    // Pure fixture helpers, deliberately not issued as native source evidence.
    Words(0x24020180,0x03E00008,0).CopyTo(b,0x7458);
    Words(0x3C0341C6,0x3C028007,0x8C425AC0,0x34634E6D,0x00430018,0x00002012,
        0x24823039,0x3C018007,0xAC225AC0,0x00021402,0x03E00008,0x30427FFF).CopyTo(b,0x52F2C);
    Words(0xA0850046,0x03E00008,0).CopyTo(b,0x296E0); return b;
}
void Verify(NativeJapaneseEggVisualBehaviorPlan plan)
{
    Check(plan.Package.InstructionCount == 39 && plan.Package.Fragments.Count == 3 && plan.Linked.SourceToFinalPc.Count == 39,
        "Exactly 39 native visual instructions have owned relocation mappings.");
    Check(plan.Package.InterceptedExits.SequenceEqual(new uint[] { 0x8008DE2C,0x8008DED0,0x8008DF64 }), "Device gate, level11 override, pickup continuation are intercepted.");
    Check(plan.Package.Fragments.All(f => !f.CopyBytes().Chunk(4).Any(w => (Word(w,0) >> 26) == 15)),
        "Visual fragments contain no direct global-address LUI instructions.");
    Check(plan.Calls.Count == 4 && plan.Calls.Select(c => c.UsaTargetPc).Distinct().Order().SequenceEqual(new uint[] {0x80016C58,0x80038EE0,0x8006272C}),
        "Only the three proved USA helpers are called, not Japanese pickup/effect APIs.");
    byte[] before = plan.CopyOriginalOverlay(), after = plan.CopyDetachedOverlay();
    Check(after.Length == before.Length + 0x800 && after.AsSpan(0,before.Length).SequenceEqual(before), "Whole original overlay including dispatch is untouched.");
    Check(plan.CodeOwners.Sum(o => o.ByteLength) == 0x800 && plan.CodeOwners.First().Id == "visual-main" &&
        plan.CodeOwners.First().ByteLength == 0xB4 && plan.Linked.UnmaterializedExternalTargets.Count == 6,
        "Existing materializer covers code, six bridge/exit targets and padding completely.");
    foreach (var f in plan.Package.Fragments)
    for (int i = 0; i < f.ByteLength; i += 4)
    {
        uint pc = f.Source.SourcePc + (uint)i, mapped = plan.Linked.SourceToFinalPc[pc];
        if (plan.Package.Transfers.Any(t => t.SourcePc == pc)) continue;
        Check(Word(after, (int)(mapped-NativeJapaneseEggVisualBehaviorPlan.UsaOverlayBase)) == Word(f.CopyBytes(),i),
            "Ordinary/delay instructions retain exact source bytes.");
    }
    foreach (var binding in plan.Bindings)
        Check(plan.CodeOwners.Any(o => o.RuntimePc == binding.TargetPc && o.ByteLength >= binding.Target.ByteLength), "Every formerly unresolved external target is a materialized named owner.");
    foreach (var call in plan.Calls.DistinctBy(c => c.Id))
    {
        var owner = plan.CodeOwners.Single(o => o.Id == "usa-" + call.Id);
        Check(Word(after, owner.OverlayOffset) == (0x08000000 | (call.UsaTargetPc >> 2 & 0x3FFFFFF)) && Word(after, owner.OverlayOffset+4) == 0,
            "Privately issued regional bridge jumps to its exact native USA helper.");
    }
    Check(plan.Graph.CopyPayload(NativeJapaneseEggModelAssetPlan.PackageNodeId).SequenceEqual(plan.ModelAssembly.CopyModelPackage()), "Dependency graph includes the actual already-authenticated model contribution.");
    Check(plan.RevertOverlayExact(after).SequenceEqual(before), "Exact appended overlay inverse recovers whole native prefix.");
    foreach (int offset in new[] {0, before.Length-1, before.Length, after.Length-1})
    { byte[] bad=after.ToArray();bad[offset]^=1;Refuse(()=>plan.RevertOverlayExact(bad),"changed full afterimage"); }
    byte[] copy=plan.CopyDetachedOverlay();copy[0]^=1;Check(plan.CopyDetachedOverlay().SequenceEqual(after),"Detached result cannot be mutated through returned arrays.");
    Check(plan.DetachedCodeOwnersComplete && !plan.DispatchActivated && !plan.NativeImportApproved && !plan.RuntimeMemoryFitProven &&
        !plan.RuntimeVerified && !plan.PublicationApproved && plan.UnresolvedObligations.Count == 6, "Closed detached bytes do not grant actor, RAM, render or gameplay approval.");
    VerifyExecution(plan);
}
void VerifyExecution(NativeJapaneseEggVisualBehaviorPlan plan)
{
    var cpu = new TinyR3000();
    cpu.Copy(0x8000F800,plan.CopyUsaExecutable()); cpu.Copy(NativeJapaneseEggVisualBehaviorPlan.UsaOverlayBase,plan.CopyDetachedOverlay());
    const uint row=0x801A0000,priv=0x801A0100,stack=0x801F0000,seed=0x80075AC0;
    for(int i=1;i<32;i++)cpu.Regs[i]=0x12340000u+(uint)i;
    cpu.Regs[19]=row;cpu.Regs[29]=stack;cpu.Hi=0xABCDEF01;cpu.Lo=0x12345678;
    uint[] original=cpu.Regs.ToArray();cpu.Put(row,priv);cpu.Put(row+0x14,10000);cpu.Put(row+0x36,0xF5,2);
    cpu.Put(row+0x46,32,1);cpu.Put(seed,0x12345678);cpu.Put(0x800756C4,1);cpu.Put(stack+0x188,0xCAFEBABE);
    uint currentSeed=0x12345678;
    for(int tick=0;tick<64;tick++)
    {
        uint oldPhase=cpu.Get(priv+4),oldState=cpu.Get(row+0x49,1);
        uint expectedSine=plan.SourceCertified?InterpolatedSine(plan.CopyUsaExecutable(),oldPhase):384;
        cpu.Writes.Clear();cpu.Calls.Clear();cpu.Run(plan.EntryPc,NativeJapaneseEggVisualBehaviorPlan.UsaContinuation);
        Check(cpu.Regs.SequenceEqual(original)&&cpu.Hi==0xABCDEF01&&cpu.Lo==0x12345678,"Real instruction execution restores all GPRs, HI/LO/SP across native visual calls.");
        Check(cpu.Get(stack+0x188)==0xCAFEBABE,"Original caller's cursor owner survives exact adapter frame restoration.");
        Check(cpu.Get(priv)==10000&&cpu.Get(priv+4)==oldPhase+128&&cpu.Get(row+0x14)==unchecked((uint)(10000+((int)expectedSine>>6))),
            "Initializer captures authored Z once; subsequent bobbing never drifts the baseline.");
        if(tick==0||oldState==1)
        {
            currentSeed=unchecked(currentSeed*0x41C64E6D+0x3039);uint countdown=16+(currentSeed>>16&31);
            currentSeed=unchecked(currentSeed*0x41C64E6D+0x3039);uint yaw=currentSeed>>16&255;
            Check(cpu.Get(row+0x49,1)==countdown&&cpu.Get(priv+8)==yaw&&cpu.Get(seed)==currentSeed&&cpu.Calls.Count(pc=>pc==0x8006272C)==2,
                "Native random timer/target uses only the USA RNG owner and exact two-call transition.");
        }
        else Check(cpu.Get(row+0x49,1)==(oldState-1&255)&&cpu.Get(seed)==currentSeed&&!cpu.Calls.Contains(0x8006272C),"Initialized countdown does not repeatedly initialize or consume random state.");
        Check(cpu.Calls.Contains(0x80016C58)&&cpu.Calls.Contains(0x80038EE0),"Relocated tick reaches exact USA sine and turn helpers.");
        Check(cpu.Writes.All(w=>w>=priv&&w<priv+12||w>=row+0x14&&w<row+0x18||w==row+0x46||w==row+0x49||
            w>=seed&&w<seed+4||w>=stack-0x2D8&&w<stack),"Instruction writes stay inside declared row/private/USA RNG/owned dynamic stack; no Japanese collection memory.");
        Check(cpu.Visited.All(pc=>pc<0x8008DE1C||pc>=0x8008E0E8),"Execution never enters original Japanese gate/pickup addresses.");
    }
    static uint InterpolatedSine(byte[] exe,uint phase)
    {
        int p=(int)(phase&4095),at=0x5D3F8+(p>>4)*2;
        int a=BinaryPrimitives.ReadInt16LittleEndian(exe.AsSpan(at));
        if((p&15)==0)return unchecked((uint)a);
        int b=BinaryPrimitives.ReadInt16LittleEndian(exe.AsSpan(at+2));return unchecked((uint)(a+((p&15)*(b-a)>>4)));
    }
}
if(args.Length==3&&args[0]=="--source")
{
    using var jp=NativeRegionalSourceSession.Open(args[1],NativeDiscRevision.JapanScps10083);
    using var usa=NativeRegionalSourceSession.Open(args[2],NativeDiscRevision.UsaScus94228);
    var asset=NativeJapaneseEggModelAssetPlan.Capture(jp,usa);
    Refuse(()=>NativeJapaneseEggVisualBehaviorPlan.Capture(usa,usa,asset),"wrong donor revision");
    var plan=NativeJapaneseEggVisualBehaviorPlan.Capture(jp,usa,asset);
    Check(plan.SourceCertified&&plan.Evidence.Count==23,"Exact source issuer binds both overlays, three helpers, tables, angle leaves and dispatch boundaries.");
    var dispatch=new TinyR3000();dispatch.Copy(NativeJapaneseEggVisualBehaviorPlan.UsaOverlayBase,plan.CopyOriginalOverlay());
    dispatch.Regs[3]=0xF5;dispatch.Regs[30]=1;dispatch.Run(0x8007DA9C,0x80085780);
    Check(dispatch.Visited.Contains(0x8007DBC0)&&dispatch.Visited.Contains(0x8007DBD4)&&!dispatch.Visited.Contains(plan.EntryPc),
        "Exact USA F5 update follows compare-chain default; appended code is not activated by model registration.");
    var ordinary=new TinyR3000();ordinary.Copy(NativeJapaneseEggVisualBehaviorPlan.UsaOverlayBase,plan.CopyOriginalOverlay());
    ordinary.Regs[3]=0x22;ordinary.Regs[30]=1;ordinary.Run(0x8007DA9C,0x8007ED6C);
    Check(ordinary.Visited.Contains(0x8007DB2C),"Independent ordinary-egg comparison reaches its real native handler, not the default.");
    var initialize=new TinyR3000();initialize.Copy(NativeJapaneseEggVisualBehaviorPlan.UsaOverlayBase,plan.CopyOriginalOverlay());
    initialize.Regs[16]=0xF5;initialize.Regs[19]=0x801A0000;initialize.Run(0x80085854,0x800860D4);
    Check(initialize.Visited.Contains(0x800858E4)&&initialize.Visited.Contains(0x800858F0),"Exact USA dynamic F5 constructor falls to generic initialization, not a dedicated egg initializer.");
    Verify(plan);jp.VerifyUnchanged();usa.VerifyUnchanged();
    Console.WriteLine($"JP_EGG_VISUAL_BEHAVIOR_SOURCE_OK checks={checks} overlay={plan.OverlaySha256} graph={plan.DependencyGraphSha256} entry={plan.EntryPc:X8} dispatchActivated=false");
}
else if(args.Length==0)
{
    var asset=NativeJapaneseEggModelAssetPlan.Fixture(ModelFixture());byte[] overlay=OverlayFixture(),prefix=new byte[0xE000];
    var plan=NativeJapaneseEggVisualBehaviorPlan.Fixture(asset,ResourceFixture(),overlay,prefix,ExecutableFixture());
    Check(!plan.SourceCertified&&plan.Evidence.Count==3,"Fixture owns structural inputs, never native helper/source identity.");Verify(plan);
    var repeat=NativeJapaneseEggVisualBehaviorPlan.Fixture(asset,ResourceFixture(),overlay,prefix,ExecutableFixture());
    Check(plan.OverlaySha256==repeat.OverlaySha256&&plan.DependencyGraphSha256==repeat.DependencyGraphSha256&&!ReferenceEquals(plan.Layout.Domain,repeat.Layout.Domain),"Deterministic bytes never alias independently issued code owners.");
    foreach(int offset in new[]{0x93F4,0x949C,0x94BC,0x94BC+0x7C})
    {byte[] bad=overlay.ToArray();bad[offset]^=1;Refuse(()=>NativeJapaneseEggVisualBehaviorPlan.Fixture(asset,ResourceFixture(),bad,prefix,ExecutableFixture()),"changed captured fragment");}
    Refuse(()=>plan.Package.Link(new(plan.Layout.Domain,0xE000,0x800),0x80088A38,[]),"missing external bindings");
    Refuse(()=>plan.Package.Link(new(plan.Layout.Domain,0xE000,0x800),0x80088A38,plan.Bindings.Select(b=>b.SourcePc==0x8008DEF0?b with{ExpectedTargetPc=0x80016C58}:b).ToArray()),"Japanese target preimage cannot be silently replaced with USA address");
    Console.WriteLine($"JP_EGG_VISUAL_BEHAVIOR_SOURCE_FREE_OK checks={checks}");
}
else throw new ArgumentException("Usage: no arguments, or --source <exact-JP-BIN> <exact-USA-BIN>.");

using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class SourceCases
{
    internal static void Run(string[] args,Action<bool,string> check,Action<Action,string> refuse)
    {
        if(args.Length!=5||args[0]!="--source-only"||args[1]!="--jp-bin"||args[3]!="--usa-bin")
            throw new ArgumentException("Only --source-only --jp-bin JP --usa-bin USA; no installation/publication mode.");
        using var japan=NativeRegionalSourceSession.Open(args[2],NativeDiscRevision.JapanScps10083);
        using var usa=NativeRegionalSourceSession.Open(args[4],NativeDiscRevision.UsaScus94228);
        try
        {
            var ancestry=NativeJapaneseEggAdmittedLifecyclePlan.Capture(japan,usa);
            var workspace=NativeCollectibleSaveWorkspacePlan.CaptureController(usa,ancestry);
            var lifecycle=NativeJapaneseEggLifecycleOverlayPlan.CreateForWorkspace(workspace);
            var plan=NativeCollectibleSaveSchedulerPlan.Capture(usa,workspace,lifecycle);var k=plan.Kernel;
            plan.RequireCurrent(workspace,lifecycle);check(true,"Exact source issuer accepted");
            var other=NativeCollectibleSaveWorkspacePlan.CaptureController(usa,ancestry);
            refuse(()=>plan.RequireCurrent(other,lifecycle),"Equal bytes cannot substitute another workspace issuer");
            refuse(()=>plan.RequireCurrent(workspace,NativeJapaneseEggLifecycleOverlayPlan.CreateForWorkspace(workspace)),"Equal lifecycle bytes cannot substitute another exact issuer instance");
            refuse(()=>NativeCollectibleSaveSchedulerPlan.Capture(usa,other,lifecycle),"Closed source capture rejects mismatched issuer");
            refuse(()=>NativeCollectibleSaveSchedulerPlan.Capture(usa,workspace,ancestry.Lifecycle),"Old scene lifecycle is not final workspace authority");
            refuse(()=>NativeCollectibleSaveSchedulerPlan.Capture(japan,workspace,lifecycle),"Wrong regional complete original source");
            check(plan.MainOwner.Offset==0xE240&&plan.MainOwner.ByteLength==0x5C0&&plan.FinalOwner.Offset==0xFDF8&&
                plan.FinalOwner.ByteLength==0x208&&plan.StateOwner.Offset==0xFFC0&&plan.StateOwner.ByteLength==0x40,"Actual typed padding reassigned without growth");
            check(k.MainCodeByteLength<=0x5C0&&k.FinalCodeByteLength+0x40<=0x208&&plan.CopyOverlay().Length==0x10000,"Both independent measured capacities remain hard bounds");
            check(k.ExpectedGlobals.Single(g=>g.Address==0x80075828).Value==workspace.Memory.FinalMobyTable&&
                k.ExpectedGlobals.Single(g=>g.Address==0x800785E4).Value==workspace.Memory.FinalSceneStart,"Guard derives actual current final scene globals");
            foreach(var model in lifecycle.Projection.Models)
                check(k.ExpectedGlobals.Single(g=>g.Address==0x80076378+model.AuthoredClass*4u).Value==model.RootAddress,"Exact projected class registration, not fixture/source-old literal");
            byte[] after=plan.CopyOverlay(),before=lifecycle.CopyOverlay();
            check(Enumerable.Range(0,after.Length).All(i=>i>=0xC9A0&&i<0xC9A8||i>=0xE240&&i<0xE800||i>=0xFDF8||after[i]==before[i]),"Full retained byte complement including prior lifecycle hooks/callables");
            check(plan.RevertOverlayExact(after).SequenceEqual(before),"Inverse restores both former padding owners and exact original epilogue");
            foreach(int at in new[]{0,0xC9A0,0xC9A4,0xE240,0xE7FF,0xFDF8,0xFFBF,0xFFC0,0xFFFF})
            {byte[] bad=after.ToArray();bad[at]^=1;refuse(()=>plan.RevertOverlayExact(bad),"Full afterimage inverse rejects stale or changed owner");}
            check(plan.CopyExecutable().SequenceEqual(ancestry.CopyExecutable())&&plan.CopyLevelData().SequenceEqual(workspace.CopyLevelData()),"Complete original-derived EXE/card-overlay ancestry and final data unchanged");
            check(plan.SourceCertified&&!plan.HooksInstalled&&!plan.RuntimeVerified&&!plan.CardWrites&&!plan.NormalCreateBinApproved&&!plan.PublicationApproved&&
                !plan.CacheCoherenceProven&&!plan.IrqExclusionProven&&!plan.NativeSessionAuthenticated&&!plan.NativeAsyncRetentionProven&&!plan.NativeFullStackProven,"Source/CPU afterimage does not promote any installation gate");
            SchedulerCases.Run(k,usa,check);
            // Execute from the exact detached patched epilogue/overlay, not just
            // equivalent fixture words. Native I/O remains explicitly mocked.
            var exact=new SchedulerFixture(k,true,usa);exact.Cpu.Copy(NativeCollectibleSaveSchedulerPlan.OverlayBase,after);exact.ReachHeld();
            check(exact.Stage==6&&exact.Flow==4&&exact.Card.AsSpan(0x200,0x600).SequenceEqual(exact.Ordinary),"Exact source-composed overlay reaches held proposal through original serializer600");
            Console.WriteLine("SCHEDULER_SOURCE_LAYOUT "+JsonSerializer.Serialize(new{plan.OverlaySha256,plan.OriginalOverlaySha256,workspace.FinalDataSha256,
                k.MainCodeByteLength,k.FinalCodeByteLength,state=NativeCollectibleSaveSchedulerKernel.StateAddress,globals=k.ExpectedGlobals.Select(g=>new{g.Address,g.Value}),
                mainSha256=Hash(k.CopyMainBytes()),finalSha256=Hash(k.CopyFinalBytes()),workspace.Memory.FinalSceneStart,workspace.Memory.FinalNormalGap}));
            ResidentCases.Source(usa,ancestry,check,refuse);
        }
        finally{try{japan.VerifyUnchanged();}finally{usa.VerifyUnchanged();}}
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}

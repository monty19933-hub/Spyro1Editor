using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using F=AttemptFixtures;

int checks=0;
void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
void Refuse(Action action,string why)
{try{action();}catch(Exception e)when(e is InvalidDataException or ArgumentException){checks++;return;}throw new Exception("Expected refusal: "+why);}
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
var manifest=NativeCollectibleCardLoadResearchComposition.FixedManifest();
var maps=new[]{new NativeJapaneseEggCollectibleIdentity("egg-a",Guid.Parse("11111111-1111-1111-1111-111111111111")),
    new NativeJapaneseEggCollectibleIdentity("egg-b",Guid.Parse("22222222-2222-2222-2222-222222222222"))};
var fixture=F.Authored();
var legacy=NativeJapaneseEggLifecycleOverlayPlan.Fixture(fixture,manifest,0,maps,F.Predicate());
var guarded=NativeJapaneseEggLifecycleOverlayPlan.FixtureGuarded(fixture,manifest,0,maps,F.Predicate());
Check(Hash(legacy.CopyOverlay())=="6712fbc9f88088b79be897d9300d0c8ae7406da7a8c278ab93352b7eabc1271b"&&
    Hash(legacy.CopyExecutable())=="bc00912493d03f4c627abfc8975e22fb6c7faf841dd206506802cd6d5b43cb41"&&
    Hash(legacy.CopyLevelData())=="04232ed7d53edf79fdf72cd2a8bcc59314ff901ab5082cc81c4cc7d1de1dc157","Old fixture exact afterimage goldens unchanged");
foreach(var old in new[]{legacy,guarded})
{
    Check(old.Projection.Delta==old.Projection.ModelDelta&&old.Projection.ModelDelta==old.Projection.SceneDelta,"Legacy uniform Delta unchanged");
    Check(ReferenceEquals(old.Projection.FinalDomain,fixture.FinalDomain)&&old.Projection.FinalDataSha256==fixture.FinalDataSha256&&
        old.Projection.CopyLevelData().SequenceEqual(fixture.CopyLevelData()),"Legacy final data authority remains authored");
    var projected=old.Projection;
    Check(projected.PlanSha256==Hash(JsonSerializer.SerializeToUtf8Bytes(new
    {
        profile="authored-egg-successor-overlay-projection-v1",authoredPlan=fixture.PlanSha256,projected.SourceCertified,
        projected.CopyAnchor,projected.Delta,projected.ModelAddress,projected.SceneAddress,projected.SceneEnd,projected.NormalGap,
        blocks=old.Layout.Blocks.Select(b=>new{b.Id,b.Owner.Offset,b.Owner.ByteLength,b.IsPadding})
    })),"Legacy projection hash recipe is unchanged by new final-data properties");
    Refuse(()=>old.RequireWorkspaceCurrent(null!),"Legacy plan cannot impersonate a workspace route");
}
Refuse(()=>NativeJapaneseEggOverlayProjection.CreateForWorkspace(null!),"No missing/caller-supplied projection authority");
Refuse(()=>NativeJapaneseEggLifecycleOverlayPlan.CreateForWorkspace(null!),"No missing/caller-supplied lifecycle authority");
AdmissionCpuProbe.Verify(NativeCollectibleCardAdmissionPlan.CreateFixture(),fixture,guarded,null,Check);
bool source=args.Length!=0;
if(source)
{
    if(args.Length!=5||args[0]!="--source-only"||args[1]!="--jp-bin"||args[3]!="--usa-bin")
        throw new ArgumentException("Only --source-only --jp-bin JP-BIN --usa-bin USA-BIN; no publication or installation mode.");
    using var japan=NativeRegionalSourceSession.Open(args[2],NativeDiscRevision.JapanScps10083);
    using var usa=NativeRegionalSourceSession.Open(args[4],NativeDiscRevision.UsaScus94228);
    try
    {
        var ancestry=NativeJapaneseEggAdmittedLifecyclePlan.Capture(japan,usa);var parent=ancestry.Lifecycle;
        var workspace=NativeCollectibleSaveWorkspacePlan.CaptureController(usa,ancestry);
        var preparation=NativeCollectibleSaveWorkspacePlan.Capture(usa,ancestry);
        Refuse(()=>NativeJapaneseEggOverlayProjection.CreateForWorkspace(preparation),"Preparation profile is not the closed controller route");
        Refuse(()=>NativeJapaneseEggLifecycleOverlayPlan.CreateForWorkspace(preparation),"No preparation lifecycle shortcut");
        var plan=NativeJapaneseEggLifecycleOverlayPlan.CreateForWorkspace(workspace);var p=plan.Projection;
        var guard=plan.LiveGuard??throw new Exception("Workspace guard absent");
        var parentGuard=parent.LiveGuard??throw new Exception("Parent guard absent");
        plan.RequireWorkspaceCurrent(workspace);p.RequireWorkspaceCurrent(workspace);checks+=2;
        var other=NativeCollectibleSaveWorkspacePlan.CaptureController(usa,ancestry);
        Refuse(()=>plan.RequireWorkspaceCurrent(other),"Same bytes do not substitute another workspace issuer");
        Refuse(()=>p.RequireWorkspaceCurrent(other),"Projection requires exact workspace instance");
        Refuse(()=>p.RequireCurrent(fixture),"Stale authored source");
        Refuse(()=>{_=p.Delta;},"Legacy uniform Delta cannot silently misrepresent nonuniform geometry");
        Refuse(()=>plan.Binding.RequireCurrent(ancestry.Authored,manifest.Advance([Guid.Parse("33333333-3333-3333-3333-333333333333")],[])),"Changed manifest");
        Refuse(()=>NativeJapaneseEggCollectibleBinding.CreateProjected(ancestry.Authored,manifest,maps.Reverse().Select((m,i)=>new NativeJapaneseEggCollectibleIdentity(maps[i].RowId,m.InstanceId)),p),"Workspace fixed row identities cannot be exchanged");
        Check(ReferenceEquals(p.Layout,parent.Layout)&&ReferenceEquals(p.FinalDomain,workspace.FinalDomain)&&p.FinalDataSha256==workspace.FinalDataSha256&&
            p.CopyLevelData().SequenceEqual(workspace.CopyLevelData()),"Projection derives exact final workspace bytes/domain/hash and unchanged overlay layout");
        Check(p.ModelDelta==0x1800&&p.SceneDelta==0x9000&&p.CopyAnchor==0x8008AA38&&p.ModelAddress==0x80117D54&&
            p.SceneAddress==0x8016F554&&p.SceneEnd==0x80183D54&&p.NormalGap==0x3E5C&&workspace.Memory.FinalNormalGap==0x3C5C,
            "Independent split-delta and reserve800 versus reserveA00 geometry goldens");
        Check(plan.Binding.ModelByteLength==0x57800&&plan.Binding.SceneByteLength==parent.Binding.SceneByteLength&&
            plan.Binding.FinalDataSha256==workspace.FinalDataSha256,"Binding charges complete grown model owner and unchanged scene extent");
        foreach(var model in p.Models)
        {
            var old=parent.Projection.Models.Single(m=>m.RowId==model.RowId);var row=plan.Binding.Rows.Single(r=>r.RowId==model.RowId);
            var oldRow=parent.Binding.Rows.Single(r=>r.RowId==model.RowId);
            Check(model.RootAddress==old.RootAddress&&model.LoaderFields.SequenceEqual(old.LoaderFields),"Every model root and loaded target remains fixed");
            Check(model.RowAddress==old.RowAddress+0x7800&&model.PrivateAddress==old.PrivateAddress+0x7800&&
                row.RowAddress==model.RowAddress&&row.PrivateAddress==model.PrivateAddress,"Rows and private addresses shift only by the model insertion");
            Check(ReferenceEquals(row.RowOwner.Space,workspace.FinalDomain)&&ReferenceEquals(row.PrivateOwner.Space,workspace.FinalDomain)&&
                row.RowOwner.Offset==oldRow.RowOwner.Offset+0x7800&&row.PrivateOwner.Offset==oldRow.PrivateOwner.Offset+0x7800,"Final disk extents use the current workspace domain");
        }
        Check(plan.Lifecycle.DynamicBase==parent.Lifecycle.DynamicBase+0x7800&&guard.ExpectedSceneGlobals.All(g=>
            workspace.RequiredGlobalRebindings.SingleOrDefault(r=>r.Address==g.Address) is{} r?g.Value==r.NewValue:
            g.Value==parentGuard.ExpectedSceneGlobals.Single(o=>o.Address==g.Address).Value),"Dynamic rows and all guarded globals use final geometry");
        Check(plan.Layout.FinalByteLength==0x10000&&plan.CopyExecutable().SequenceEqual(parent.CopyExecutable())&&
            plan.CopyLevelData().SequenceEqual(workspace.CopyLevelData()),"Recompilation grows neither overlay nor EXE and preserves complete workspace bytes");
        Check(plan.Predicate.EntryPc==parent.Predicate.EntryPc&&plan.Collector.EntryPc==parent.Collector.EntryPc&&plan.Query.EntryPc==parent.Query.EntryPc&&
            plan.Attempt.EntryPc==parent.Attempt.EntryPc&&plan.Lifecycle.BeforeVisualEntryPc==parent.Lifecycle.BeforeVisualEntryPc&&
            plan.Lifecycle.AfterVisualEntryPc==parent.Lifecycle.AfterVisualEntryPc&&plan.PreBridgePc==parent.PreBridgePc&&plan.PostBridgePc==parent.PostBridgePc&&
            guard.BeforeEntryPc==parentGuard.BeforeEntryPc&&guard.AfterEntryPc==parentGuard.AfterEntryPc&&guard.ValidatePc==parentGuard.ValidatePc,
            "Every callable, internal guard entry and bridge remains at the same owned PC");
        byte[] after=plan.CopyOverlay(),before=parent.CopyOverlay();
        var changedOwners=new[]{"pickup-attempt","egg-lifecycle","live-consumer-guard"};
        foreach(var owner in plan.Layout.Blocks)
        {
            int at=(int)owner.Owner.Offset,n=(int)owner.Owner.ByteLength;
            Check(changedOwners.Contains(owner.Id)?!after.AsSpan(at,n).SequenceEqual(before.AsSpan(at,n)):
                after.AsSpan(at,n).SequenceEqual(before.AsSpan(at,n)),"Only necessary recompiled owners change: "+owner.Id);
        }
        Check(after.AsSpan(0,plan.Layout.RetainedByteLength).SequenceEqual(before.AsSpan(0,plan.Layout.RetainedByteLength)),"Complete visual prefix including both hook edges unchanged");
        Check(plan.RevertOverlayExact(after).SequenceEqual(before)&&plan.RevertExecutableExact(plan.CopyExecutable()).SequenceEqual(parent.CopyExecutable()),"Exact immediate-parent inverse");
        foreach(int at in new[]{0,after.Length-1}.Concat(plan.Layout.Blocks.Select(b=>(int)b.Owner.Offset)))
        {var bad=after.ToArray();bad[at]^=1;Refuse(()=>plan.RevertOverlayExact(bad),"Complete overlay afterimage required");}
        var altered=plan.CopyExecutable();altered[0]^=1;Refuse(()=>plan.RevertExecutableExact(altered),"Complete executable inverse");
        byte[] dataCopy=p.CopyLevelData();dataCopy[0]^=1;Check(p.CopyLevelData().SequenceEqual(workspace.CopyLevelData()),"Projection bytes are defensive");
        Refuse(()=>NativeJapaneseEggAdmittedLifecyclePlan.Compose(ancestry.Admission,ancestry.Authored,plan),"Old startup/admitted publication route remains closed to workspace output");
        Check(plan.SourceCertified&&!plan.DispatchActivated&&!plan.NativeResidenceIssued&&!plan.PersistentSaveChanges&&!plan.PublicationApproved&&
            !p.NativeResidenceIssued&&!p.RuntimeFitProven&&!p.PublicationApproved,"Source recompilation does not issue runtime, lifetime, save or publication authority");
        NativeLoaderCases.Run(workspace,Check);
        // Reuse the existing actual admission/poll/SHA/envelope CPU path, not a
        // seeded Ready image. It exercises callbacks, reset, re-entry, stack,
        // post-hook rejection, row/ledger complement and canonical unbound F5.
        AdmissionCpuProbe.Verify(ancestry.Admission,ancestry.Authored,plan,ancestry.CopyExecutable(),Check);
        WorkspaceCpuCases.Run(workspace,plan,Check);
        Console.WriteLine("WORKSPACE_LIFECYCLE_LAYOUT "+JsonSerializer.Serialize(new{plan.PlanSha256,plan.FinalOverlaySha256,plan.FinalExecutableSha256,
            p.FinalDataSha256,p.ModelDelta,p.SceneDelta,p.ModelAddress,p.SceneAddress,p.SceneEnd,p.NormalGap,
            workspace.Memory.FinalNormalGap,plan.Lifecycle.DynamicBase,plan.Binding.Rows,
            guard=new{guard.BeforeEntryPc,guard.AfterEntryPc,guard.ValidatePc,sha256=Hash(guard.CopyBytes())}}));
    }
    finally{try{japan.VerifyUnchanged();}finally{usa.VerifyUnchanged();}}
}
Console.WriteLine($"NATIVE_JAPANESE_EGG_WORKSPACE_LIFECYCLE_OK checks={checks} source={source} imageWrites=0 cardWrites=0 runtimeVerified=false");

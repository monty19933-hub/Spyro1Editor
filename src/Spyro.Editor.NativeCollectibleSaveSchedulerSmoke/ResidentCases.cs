using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class ResidentCases
{
    internal static void Free(Action<bool,string> check)
    {
        var g=NativeCollectibleSaveWorkspacePlan.MeasureControllerScheduler(0x80167D54,0x801879B0);
        check(g.GrowthBytes==0x8800&&g.SemanticBytes==0x8300&&g.LeadingBytes==0xC&&g.TrailingBytes==0x4F4&&g.EndAddress==0x80170554,"Closed named scheduler geometry");
        check(NativeCollectibleSaveWorkspacePlan.MeasureControllerScheduler(g.TailAddress,g.EndAddress).TailGapToFloor==0,"New complete owner exactly abuts explicit floor");
        bool Refused(uint tail,uint floor){try{_=NativeCollectibleSaveWorkspacePlan.MeasureControllerScheduler(tail,floor);return false;}catch(InvalidDataException){return true;}}
        check(Refused(g.TailAddress,g.EndAddress-4),"New complete owner refuses one aligned word below exact floor");
        check(Refused(0x801E8000,0x801FF600),"New semantic owner refuses bounded cached-RAM overflow");
        var legacy=NativeCollectibleSaveSchedulerKernel.CreateFixture();
        var resident=NativeCollectibleSaveSchedulerKernel.CreateResidentFixture();
        check(resident.ResidentAddress==0x8016F060&&resident.StateBase==0x800891F8&&resident.CopyResidentBytes().Length==0x1000,"Named immutable resident owner and retained overlay state");
        check(resident.MainCodeByteLength+0x40<=0x5C0&&resident.ResidentCodeByteLength<=0x1000,"Independent measured guard/body hard caps");
        check(resident.Controller.CopyBytes().SequenceEqual(legacy.Controller.CopyBytes()),"Controller code/entries/buffers do not move");
        SchedulerCases.Run(resident,null,check);
    }
    internal static void Source(NativeRegionalSourceSession usa,NativeJapaneseEggAdmittedLifecyclePlan ancestry,Action<bool,string> check,Action<Action,string> refuse)
    {
        var old=NativeCollectibleSaveWorkspacePlan.CaptureController(usa,ancestry);
        check(old.FinalDataSha256=="700e0f54859f0a2759595d2b69e7f91ebeea720ef096de1910a7b6ba3196dd3a","Existing controller workspace bytes stay exact");
        check(NativeCollectibleSaveWorkspacePlan.Capture(usa,ancestry).FinalDataSha256=="9e50753a043722117f7843acf1a3d249191c847351fa518c4fada6cc870c7bc4","Existing preparation workspace bytes stay exact");
        check(NativeCollectibleSaveSchedulerPlan.Capture(usa,old,NativeJapaneseEggLifecycleOverlayPlan.CreateForWorkspace(old)).OverlaySha256==
            "b79a2fac3bd08fa0bf80e726b198084ec3d7b3ee967e569fb1f7e1afad86a57d","Existing overlay-only scheduler bytes stay exact");
        var workspace=NativeCollectibleSaveWorkspacePlan.CaptureControllerScheduler(usa,ancestry);
        var lifecycle=NativeJapaneseEggLifecycleOverlayPlan.CreateForWorkspace(workspace);
        var plan=NativeCollectibleSaveSchedulerPlan.CaptureResident(usa,workspace,lifecycle);var k=plan.Kernel;
        check(plan.OverlaySha256=="dcec8e77395614fa63b056f52760813fb95cbb9d3ee64abe83cdb1091c52c02b"&&
            workspace.FinalDataSha256=="e10ecf779e248dfa30980af7acca46227162f1fa21a7de2964f5a6874d4c3f5a"&&
            Convert.ToHexStringLower(SHA256.HashData(k.CopyResidentBytes()))=="979a6492876e55784cf5d1e1f0bdbe86b1ecb665fa91c1fe53118f1b4cde4554","Final fully materialized resident profile goldens");
        check(workspace.Profile==NativeCollectibleSaveWorkspacePlan.WorkspaceProfile.ControllerScheduler&&ReferenceEquals(workspace.Scheduler,k),"One closed final workspace owns exact generated scheduler");
        check(workspace.Memory.FinalSceneStart==0x80170554&&workspace.Memory.FinalSceneEnd==0x80184D54&&workspace.Memory.FinalNormalGap==0x2C5C&&
            workspace.Memory.FinalModelBytes==0x58800&&workspace.CopyLevelData().Length==0x38E000,"Complete new descriptor geometry, A00 gap and unchanged copy anchor");
        check(workspace.CopyOwnerBytes("resident-scheduler-code").SequenceEqual(k.CopyResidentBytes()),"Final hashed data contain the exact generated resident owner");
        check(plan.CopyOverlay().AsSpan(0xFDF8,0x208).SequenceEqual(lifecycle.CopyOverlay().AsSpan(0xFDF8,0x208)),"New route leaves entire former second padding owner untouched");
        check(plan.RevertOverlayExact(plan.CopyOverlay()).SequenceEqual(lifecycle.CopyOverlay())&&workspace.RevertExact(workspace.CopyLevelData()).SequenceEqual(ancestry.CopyLevelData()),"Complete independent overlay/data inverses");
        refuse(()=>NativeCollectibleSaveSchedulerPlan.Capture(usa,workspace,lifecycle),"Legacy source route cannot adopt resident profile");
        refuse(()=>NativeCollectibleSaveSchedulerPlan.CaptureResident(usa,old,NativeJapaneseEggLifecycleOverlayPlan.CreateForWorkspace(old)),"Resident source route cannot adopt legacy profile");
        var equivalent=NativeCollectibleSaveWorkspacePlan.CaptureControllerScheduler(usa,ancestry);
        check(equivalent.CopyLevelData().SequenceEqual(workspace.CopyLevelData()),"Independently captured source workspace is byte-equivalent");
        refuse(()=>plan.RequireCurrent(equivalent,lifecycle),"Byte-equivalent workspace cannot substitute exact issuer identity");
        foreach(int at in new[]{0xC9A0,0xE240,0xE7C0,0xFDF8,0xFFFF})
        {byte[] changed=plan.CopyOverlay();changed[at]^=1;refuse(()=>plan.RevertOverlayExact(changed),"Resident inverse checks complete overlay including retained second owner");}
        byte[] changedData=workspace.CopyLevelData();changedData[(int)workspace.Owners.Single(o=>o.Role=="resident-scheduler-code").Extent.Offset]^=1;
        refuse(()=>workspace.RevertExact(changedData),"Resident data inverse authenticates generated code owner");
        NativeLoaderCases.Run(workspace,check);WorkspaceCpuCases.Run(workspace,lifecycle,check);
        SchedulerCases.Run(k,usa,check);
        var exact=new SchedulerFixture(k,true,usa);exact.LoadCompleteWorkspace(plan);exact.ReachHeld();
        check(exact.Flow==4&&exact.Stage==6,"Fully materialized resident scheduler executes through source serializer and controller");
        check(exact.Bytes(k.ResidentAddress,0x1000).SequenceEqual(workspace.CopyOwnerBytes("resident-scheduler-code")),"No controller/native mock store changes any loaded scheduler code/table/padding byte");
        // First held entry records the bounded Drain result at state+34.
        // Subsequent retained state is compared only after that expected write.
        exact.Frame();byte[] state=exact.Bytes(k.StateBase,0x40),data=workspace.CopyLevelData();
        uint Word(int at)=>System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at));
        exact.Cpu.Copy(workspace.Memory.FinalSceneStart,data.AsSpan((int)Word(0x18),(int)Word(0x1C)).ToArray());exact.Frame();
        check(exact.Flow==4&&exact.Stage==6&&exact.Bytes(k.StateBase,0x40).SequenceEqual(state),"Scene transfer retains overlay state and terminal controller; no fresh arm");
        byte[] exe=ancestry.CopyExecutable();exact.Cpu.Copy(0x80012700,exe.AsSpan(0x2F00,0x50).ToArray());exact.Cpu.Regs[2]=uint.MaxValue;
        exact.Cpu.Run(0x80012700,0x80012750);check(exact.Cpu.Get(0x801FF616,1)==0&&exact.Flow==4,"Actual source-owned native reset invalidates Ready without inventing scheduler rearm");
        int requests=exact.Requests.Count;exact.Frame();check(exact.Flow==4&&exact.Requests.Count==requests,"Reset cannot make held scheduler issue another request");
        // Synthetic complete model reload after an already consumed terminal
        // transaction: no pending transfer is reclaimed in this fixture. This
        // is state-retention evidence, not a native unload/reload lease.
        exact.LoadCompleteWorkspace(plan,false);exact.Frame();
        check(exact.Flow==4&&exact.Stage==0&&exact.Requests.Count==requests,"Fresh model scratch does not reset retained overlay one-shot state");
        Console.WriteLine($"RESIDENT_SCHEDULER_LAYOUT overlay={plan.OverlaySha256} data={workspace.FinalDataSha256} guard={k.MainCodeByteLength:X} body={k.ResidentCodeByteLength:X} state={k.StateBase:X8} owner={k.ResidentAddress:X8} codeSha={Convert.ToHexStringLower(SHA256.HashData(k.CopyResidentBytes()))}");
    }
}

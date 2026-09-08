using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

internal static class SeparatedCandidateCases
{
    private const string Image="e57fedebb59ffc9020de00562baccbc5a6e09bffd5f24f665d34d5f03163cbdb";
    private const string PrimaryImage="c4a70f826b1a9a3d4ad8e36512be354b6205d27ea2c7a314776a51067f35b5a6";
    private const string Name="native-id65-fresh-wizard-separated-p1-n32-r01";
    private const string Profile="native-id65-fresh-wizard-separated-p1-n32-research-r01";
    internal static async Task Free(Action<bool,string> check)
    {
        var methods=typeof(NativeId65FreshWizardResearchCandidatePlan).GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly);
        check(methods.Length==2&&methods.Single(m=>m.Name=="CaptureSeparatedAsync").GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{
            typeof(string),typeof(string),typeof(NativeId65FreshWizardResearchDiscPlan),typeof(NativeFreshInitialSpawnClearancePlan),typeof(CancellationToken)}),
            "Only two closed candidate factories, no caller profile/hash/approval argument.");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        await Refuse<OperationCanceledException>(()=>NativeId65FreshWizardResearchCandidatePlan.CaptureSeparatedAsync(null!,null!,null!,null!,cancelled.Token),check);
        Task<NativeId65FreshWizardResearchCandidatePlan> task;
        try{task=NativeId65FreshWizardResearchCandidatePlan.CaptureSeparatedAsync("must-not-open","must-not-open",null!,null!);}
        catch(Exception e){throw new Exception("New async factory must return a faulted Task, not throw synchronously.",e);}
        check(task.IsFaulted&&!task.IsCanceled,"Null actual Disc faults returned Task before filesystem work.");
        await Refuse<ArgumentNullException>(()=>task,check);
        check(NativeId65FreshWizardResearchCandidatePlan.OutputNames.SequenceEqual(new[]{
            "native-id65-fresh-wizard-p1-n32-r01.bin","native-id65-fresh-wizard-p1-n32-r01.receipt.json","native-id65-fresh-wizard-p1-n32-r01.cue"}),
            "Old static output names and CUE-last ordering remain exact.");
    }

    internal static async Task<NativeId65FreshWizardResearchCandidatePlan> Source(string bin,string cue,
        NativeId65FreshWizardResearchDiscPlan disc,NativeFreshInitialSpawnClearancePlan clearance,Action<bool,string> check)
    {
        var scene=disc.ExecutableAncestor.BodyAncestor.HostAncestor.SceneAncestor;
        var document=scene.AuthoringDocumentAncestor!;
        byte[] canonical=NativeFreshLevelDocumentCodec.SerializeCanonical(document);
        await Refuse<ArgumentNullException>(()=>NativeId65FreshWizardResearchCandidatePlan.CaptureSeparatedAsync("must-not-open","must-not-open",disc,null!),check);
        using(var cancel=new CancellationTokenSource())
        {cancel.Cancel();await Refuse<OperationCanceledException>(()=>NativeId65FreshWizardResearchCandidatePlan.CaptureSeparatedAsync(null!,null!,disc,clearance,cancel.Token),check);}
        var plan=await NativeId65FreshWizardResearchCandidatePlan.CaptureSeparatedAsync(bin,cue,disc,clearance);
        plan.RequireAncestry(disc);
        check(ReferenceEquals(plan.DiscAncestor,disc)&&plan.BindingProfileId==Profile&&
            plan.ExpectedImageSha256==Image&&plan.ExpectedImageByteLength==661547040&&plan.ExpectedChangedRawSectors==1290,
            "Actual wider source chain binds the independent separated whole-image expectation.");
        check(plan.BoundImageName==Name+".bin"&&plan.BoundCueName==Name+".cue"&&plan.BoundReceiptName==Name+".receipt.json"&&
            plan.BoundOutputNames.SequenceEqual(new[]{Name+".bin",Name+".receipt.json",Name+".cue"}),"Instance output owners are separate and CUE last.");
        var names=plan.BoundOutputNames;names[0]="caller.bin";
        check(plan.BoundImageName==Name+".bin"&&plan.BoundOutputNames[0]==Name+".bin","Caller cannot change retained output names.");
        check(Encoding.UTF8.GetString(plan.CopyCueBytes())==$"FILE \"{Name}.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n",
            "Exact separated CUE uses actual branch name, not old unsafe image name.");
        check(plan.CopyCanonicalDocument().SequenceEqual(scene.CompilationAncestor.CopyCanonicalDocument())&&
            plan.CanonicalDocumentSha256==scene.CompilationAncestor.CanonicalSha256,"Existing canonical properties retain Base-document meaning.");
        var expected=new NativeId65FreshDiscVerification(Image,1290);
        byte[] receipt=plan.BuildReceipt(expected);
        using(var json=JsonDocument.Parse(receipt))
        {
            var r=json.RootElement;
            check(r.GetProperty("profile").GetString()==Profile&&r.GetProperty("planSha256").GetString()==plan.PlanSha256&&
                r.GetProperty("clearancePlanSha256").GetString()==clearance.PlanSha256&&
                r.GetProperty("aggregateCanonicalDocumentJson").GetString()==Encoding.UTF8.GetString(canonical)&&
                r.GetProperty("aggregateCanonicalDocumentSha256").GetString()==Hash(canonical)&&
                Hash(canonical)==clearance.CanonicalDocumentSha256,"New receipt separately binds actual aggregate bytes and source-issued clearance.");
            check(r.GetProperty("bodySha256").GetString()=="503916389b85b655f5e1db5728e01fc2ea95daa7d3b635a3df7f5f4cc6452d29"&&
                r.GetProperty("discPlanSha256").GetString()=="20393f8a891412b7a36f341b895e344953b9fd43b107bb0a69b67a9dbba29c7c"&&
                r.GetProperty("imageSha256").GetString()==Image,"Independent wider body/disc/image pins are not replaced by caller values.");
            foreach(string name in new[]{"projectSaveBindingClaimed","runtimeVerified","normalCreateBinApproved","persistentOwnerIssued",
                "cardOrHistoryCompatibilityProven","wholePickupOrDeathProven","globalAudioCompatibilityProven","peakRuntimeMemoryProven",
                "hardwareTransfersProven","completeIncomingEdgeClosureProven","completeNativeDependencyClosureProven","checkpointOrForeignStateSupported"})
                check(r.GetProperty(name).ValueKind==JsonValueKind.False,"Separated receipt preserves false boundary: "+name);
        }
        string receiptHash=Hash(receipt),canonicalHash=Hash(canonical);receipt[0]^=1;canonical[0]^=1;
        check(Hash(plan.BuildReceipt(expected))==receiptHash&&Hash(NativeFreshLevelDocumentCodec.SerializeCanonical(document))==canonicalHash,
            "Prospective receipt and aggregate copies cannot mutate actual retained ancestry.");
        RefuseNow<InvalidDataException>(()=>plan.BuildReceipt(expected with{OutputSha256=PrimaryImage}),check);
        RefuseNow<InvalidDataException>(()=>plan.BuildReceipt(expected with{ChangedSectors=1289}),check);
        await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchCandidatePlan.CaptureAsync("must-not-open","must-not-open",disc),check,"exact primary");
        var equalDisc=await NativeId65FreshWizardResearchDiscPlan.CaptureAsync(bin,disc.ExecutableAncestor);
        check(equalDisc.PlanSha256==disc.PlanSha256&&!ReferenceEquals(equalDisc,disc),"Real reissued equal Disc fixture.");
        RefuseNow<InvalidDataException>(()=>plan.RequireAncestry(equalDisc),check);
        var reissued=await NativeId65FreshWizardResearchCandidatePlan.CaptureSeparatedAsync(bin,cue,equalDisc,clearance);
        check(reissued.PlanSha256==plan.PlanSha256&&ReferenceEquals(reissued.DiscAncestor,equalDisc),"New capture may issue its own equal Disc binding; old ancestry cannot be swapped.");

        // Exact same native bytes but a genuinely distinct authored scene and EditorId.
        var route=(NativeFreshWizardRouteIntent)document.Placements.Single();
        var retagged=NativeFreshLevelDocumentIntent.Create(document.Base,[NativeFreshWizardRouteIntent.Create(
            "22222222222222222222222222222222",route.OriginX,route.OriginY,route.OriginZ,route.EndX,route.EndY)]);
        var secondScene=await NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync(bin,retagged,scene.CompilationAncestor,scene.FamilyAncestor,scene.ResourceAncestor);
        var secondDisc=await DiscForScene(bin,secondScene,disc.ExecutableAncestor.BodyAncestor.HostAncestor.FallbackAncestor);
        check(secondDisc.PlanSha256==disc.PlanSha256&&secondScene.CopyScene().SequenceEqual(scene.CopyScene()),"Different actual authored ledger preserves exact native image bytes.");
        await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchCandidatePlan.CaptureSeparatedAsync("must-not-open","must-not-open",secondDisc,clearance),check,"another actual document/scene");
        var secondClearance=await NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync(bin,retagged,secondScene);
        await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchCandidatePlan.CaptureSeparatedAsync("must-not-open","must-not-open",disc,secondClearance),check,"another actual document/scene");
        var second=await NativeId65FreshWizardResearchCandidatePlan.CaptureSeparatedAsync(bin,cue,secondDisc,secondClearance);
        check(second.PlanSha256!=plan.PlanSha256&&second.ExpectedImageSha256==Image&&secondClearance.PlanSha256!=clearance.PlanSha256,
            "Actual changed EditorId is retained in aggregate/clearance candidate identity, not silently replaced.");

        foreach(var placement in new[]{new NativeWizardPlacementIntent(9984,9984,8909,9472,9984),new(8704,8960,8704,9216,8960)})
        {
            var d=NativeFreshLevelDocumentIntent.Create(document.Base,[NativeFreshWizardRouteIntent.Create(route.EditorId,
                placement.OriginX,placement.OriginY,placement.OriginZ,placement.EndX,placement.EndY)]);
            var s=await NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync(bin,d,scene.CompilationAncestor,scene.FamilyAncestor,scene.ResourceAncestor);
            var c=await NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync(bin,d,s);
            var alternate=await DiscForScene(bin,s,disc.ExecutableAncestor.BodyAncestor.HostAncestor.FallbackAncestor);
            check(c.Finding.Status==(placement.OriginX==8704?NativeInitialSpawnClearanceStatus.Overlap:NativeInitialSpawnClearanceStatus.InitialSeparation),
                "Real alternate separated and unsafe overlap fixtures are independently assessed.");
            await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchCandidatePlan.CaptureSeparatedAsync("must-not-open","must-not-open",alternate,c),check,
                placement.OriginX==8704?"Initial spawn clearance refused":"exact B/P1");
        }
        // Real shared production writer, no physical output or publication. Sink independently hashes every supplied byte.
        using(var source=File.OpenRead(bin))
        using(var sink=new HashSink())
        {
            int first=0;var actual=plan.WriteImageForSmoke(source,sink,CancellationToken.None,()=>first++);
            check(actual==expected&&sink.Length==661547040&&sink.Digest()==Image&&first==1,"Full existing writer agrees with independent image golden/EOF extent; no output file.");
        }
        await Legacy(bin,cue,scene.FamilyAncestor,clearance,check);
        Console.WriteLine($"FRESH_WIZARD_SEPARATED_CANDIDATE_SOURCE_OK plan={plan.PlanSha256} cue={Hash(plan.CopyCueBytes())} receipt={receiptHash} receiptBytes={plan.BuildReceipt(expected).Length} fullImage={Image} outputsWritten=0 runtimeVerified=false");
        return plan;
    }
    private static async Task Legacy(string bin,string cue,NativeWizardFamilyAssetPlan family,NativeFreshInitialSpawnClearancePlan clearance,Action<bool,string> check)
    {
        var a=NativeId65FreshDocumentIntent.Create(8448,8448,8704,1024,1024,8960,8960,64,new(80,128,112),new(128,96,64),19);
        var compilation=await NativeId65FreshDocumentCompilation.CaptureAsync(bin,a);
        var resource=await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(bin,compilation,family);
        var scene=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(bin,compilation,family,resource,new(8704,8960,8704,9216,8960));
        var fallback=await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(bin,resource);
        var disc=await DiscForScene(bin,scene,fallback);
        await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchCandidatePlan.CaptureSeparatedAsync("must-not-open","must-not-open",disc,clearance),check,"actual authored document/scene");
        var legacy=await NativeId65FreshWizardResearchCandidatePlan.CaptureAsync(bin,cue,disc);
        byte[] receipt=legacy.BuildReceipt(new(PrimaryImage,1290));
        check(legacy.BindingProfileId==NativeId65FreshWizardResearchCandidatePlan.ProfileId&&legacy.BoundOutputNames.SequenceEqual(NativeId65FreshWizardResearchCandidatePlan.OutputNames),
            "Legacy actual issuance retains exact original binding/name profile.");
        check(legacy.PlanSha256=="3278bedaf4d24a564bfc6b3fc4554a959bf520bc51608bd53b73c8a112a1ac8b"&&
            Hash(legacy.CopyCueBytes())=="78c40516cbfe54286b839453d8ccf5786ac685d23a69623e6e51d82106238a6e"&&
            receipt.Length==4877&&Hash(receipt)=="af41aa8b0af10504c626f76cbaa2446e2207486551ce6ba8766794261a63b63e",
            "Complete original path-bound primary Plan/CUE/receipt goldens remain byte-exact.");
        using var json=JsonDocument.Parse(receipt);
        check(!json.RootElement.TryGetProperty("clearancePlanSha256",out _)&&!json.RootElement.TryGetProperty("aggregateCanonicalDocumentJson",out _),
            "Old receipt schema did not gain new separated fields.");
    }
    private static async Task<NativeId65FreshWizardResearchDiscPlan> DiscForScene(string bin,NativeId65FreshWizardResearchScenePlan scene,NativeId65FreshWizardResearchFallbackResourcePlan fallback)
    {
        var host=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(bin,scene,fallback);
        var body=await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(bin,host);
        var exe=await NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(bin,body);
        return await NativeId65FreshWizardResearchDiscPlan.CaptureAsync(bin,exe);
    }
    private sealed class HashSink:Stream
    {
        private readonly IncrementalHash hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private long length;
        internal string Digest()=>Convert.ToHexStringLower(hash.GetHashAndReset());
        public override bool CanRead=>false;public override bool CanSeek=>true;public override bool CanWrite=>true;
        public override long Length=>length;public override long Position{get=>length;set=>throw new NotSupportedException();}
        public override void Write(byte[] b,int o,int n)=>Write(b.AsSpan(o,n));
        public override void Write(ReadOnlySpan<byte> b){hash.AppendData(b);length=checked(length+b.Length);}
        public override void Flush(){}public override void SetLength(long n)=>throw new NotSupportedException();
        public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override int Read(byte[] b,int o,int n)=>throw new NotSupportedException();
        protected override void Dispose(bool d){if(d)hash.Dispose();base.Dispose(d);}
    }
    private static async Task Refuse<T>(Func<Task> action,Action<bool,string> check,string contains="")where T:Exception
    {try{await action();}catch(T error)when(error.Message.Contains(contains,StringComparison.OrdinalIgnoreCase)){check(true,"Expected "+typeof(T).Name+" "+contains);return;}throw new Exception("Expected "+typeof(T).Name+" "+contains);}
    private static void RefuseNow<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Expected "+typeof(T).Name);return;}throw new Exception("Expected "+typeof(T).Name);}
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}

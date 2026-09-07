using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

internal static class SourceCases
{
    private static int checks,refusals;
    internal static async Task Run(string bin,string cue)
    {
        string sourceCueHash=FileHash(cue);Original(bin);
        try
        {
            var intent=NativeId65FreshDocumentIntent.Create(8448,8448,8704,1024,1024,8960,8960,64,new(80,128,112),new(128,96,64),19);
            var compilation=await NativeId65FreshDocumentCompilation.CaptureAsync(bin,intent);
            var family=await NativeWizardFamilyAssetPlan.CaptureAsync(bin);
            var resource=await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(bin,compilation,family);
            var scene=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(bin,compilation,family,resource,new(8704,8960,8704,9216,8960));
            var fallback=await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(bin,resource);
            var host=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(bin,scene,fallback);
            var body=await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(bin,host);
            var exe=await NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(bin,body);
            var disc=await NativeId65FreshWizardResearchDiscPlan.CaptureAsync(bin,exe);
            var plan=await NativeId65FreshWizardResearchCandidatePlan.CaptureAsync(bin,cue,disc);
            var saved=JsonSerializer.Serialize(new{compilation,family,resource,scene,fallback,host,body,exe,disc,plan});
            plan.RequireAncestry(disc);
            Check(ReferenceEquals(plan.DiscAncestor,disc)&&plan.DiscPlanSha256==disc.PlanSha256&&plan.BodyPlanSha256==body.PlanSha256&&
                plan.HostPlanSha256==host.PlanSha256&&plan.ScenePlanSha256==scene.PlanSha256&&plan.FallbackPlanSha256==fallback.PlanSha256&&
                plan.CanonicalDocumentSha256==compilation.CanonicalSha256,"Complete immutable actual chain, not caller hashes.");
            Check(plan.SourceCertified&&!plan.RuntimeVerified&&!plan.NormalCreateBinApproved&&!plan.CompleteNativeDependencyClosureProven&&
                !plan.PersistentOwnerIssued&&!plan.GlobalAudioCompatibilityProven&&!plan.PeakRuntimeMemoryProven,"Source research does not promote pending native/runtime owners.");
            Check(plan.ExpectedImageSha256=="c4a70f826b1a9a3d4ad8e36512be354b6205d27ea2c7a314776a51067f35b5a6"&&
                plan.ExpectedImageByteLength==661547040&&plan.ExpectedChangedRawSectors==1290,"Independent exact primary physical golden, not a claimed readback.");
            byte[] canonical=plan.CopyCanonicalDocument(),outputCue=plan.CopyCueBytes();
            Check(canonical.SequenceEqual(compilation.CopyCanonicalDocument())&&Encoding.UTF8.GetString(outputCue)==
                "FILE \"native-id65-fresh-wizard-p1-n32-r01.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n","Actual canonical and separately generated Wizard CUE; no old starter name.");
            string canonicalHash=Hash(canonical),cueHash=Hash(outputCue);canonical[0]^=1;outputCue[0]^=1;
            Check(Hash(plan.CopyCanonicalDocument())==canonicalHash&&Hash(plan.CopyCueBytes())==cueHash,"Returned canonical/CUE copies cannot mutate plan.");
            var known=new NativeId65FreshDiscVerification(plan.ExpectedImageSha256,1290);
            byte[] receipt=plan.BuildReceipt(known);
            Check(receipt.Length is >0 and <=65536&&receipt.SequenceEqual(plan.BuildReceipt(known)),"Bounded deterministic prospective receipt, no publication performed.");
            using(var json=JsonDocument.Parse(receipt))
            {
                var r=json.RootElement;
                Check(r.GetProperty("planSha256").GetString()==plan.PlanSha256&&r.GetProperty("imageSha256").GetString()==known.OutputSha256&&
                    r.GetProperty("canonicalDocumentJson").GetString()==Encoding.UTF8.GetString(plan.CopyCanonicalDocument())&&
                    r.GetProperty("initializerSha256").GetString()==host.InitializerSha256&&r.GetProperty("headerSha256").GetString()==body.HeaderSha256&&
                    r.GetProperty("transferSha256").GetString()==body.TransferSourceSha256&&r.GetProperty("changedRawSectors").GetInt32()==1290&&
                    r.GetProperty("executableLba").GetInt32()==54958&&r.GetProperty("executableEndExclusive").GetInt32()==55162,
                    "Receipt binds actual compiled/native/physical owners.");
                foreach(string name in new[]{"projectSaveBindingClaimed","runtimeVerified","normalCreateBinApproved","persistentOwnerIssued",
                    "cardOrHistoryCompatibilityProven","wholePickupOrDeathProven","globalAudioCompatibilityProven","peakRuntimeMemoryProven",
                    "hardwareTransfersProven","completeIncomingEdgeClosureProven","completeNativeDependencyClosureProven","checkpointOrForeignStateSupported"})
                    Check(r.GetProperty(name).ValueKind==JsonValueKind.False,"Explicit false receipt boundary: "+name);
                Check(r.GetProperty("discPlanLimitations").GetArrayLength()>0&&r.GetProperty("candidatePublicationScope").GetString()!.Contains("research")&&
                    !r.TryGetProperty("pendingNativeOwners",out _),"Detached Disc limitations remain contextualized, not false claims that wrapper has no writer.");
            }
            receipt[0]^=1;Check(plan.BuildReceipt(known)[0]==(byte)'{'&&plan.BuildReceipt(known)[0]!=receipt[0],"Receipt bytes are detached.");
            Reject(()=>plan.BuildReceipt(known with{ChangedSectors=1253}));
            Reject(()=>plan.BuildReceipt(known with{OutputSha256=new string('0',64)}));
            try{plan.BuildReceipt(null!);throw new Exception("Null verification must refuse.");}
            catch(ArgumentNullException){refusals++;}
            var equalDisc=await NativeId65FreshWizardResearchDiscPlan.CaptureAsync(bin,exe);
            Check(!ReferenceEquals(equalDisc,disc)&&equalDisc.PlanSha256==disc.PlanSha256,"Genuinely reissued equal primary Disc.");
            Reject(()=>plan.RequireAncestry(equalDisc));
            var equalCandidate=await NativeId65FreshWizardResearchCandidatePlan.CaptureAsync(bin,cue,equalDisc);
            Check(equalCandidate.PlanSha256==plan.PlanSha256&&ReferenceEquals(equalCandidate.DiscAncestor,equalDisc),
                "Normal re-capture issues its own valid primary candidate; old object's exact ancestry still cannot swap.");
            var alternateScene=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(bin,compilation,family,resource,scene.Placement with{OriginZ=8909});
            var alternateHost=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(bin,alternateScene,fallback);
            var alternateBody=await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(bin,alternateHost);
            var alternateExe=await NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(bin,alternateBody);
            var alternateDisc=await NativeId65FreshWizardResearchDiscPlan.CaptureAsync(bin,alternateExe);
            Check(alternateDisc.CopyExecutable().SequenceEqual(disc.CopyExecutable())&&!alternateDisc.CopyBody().SequenceEqual(disc.CopyBody()),
                "Actual alternate scene has equal EXE but different retained body.");
            await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchCandidatePlan.CaptureAsync("must-not-open", "must-not-open",alternateDisc),"exact primary");
            var binding=NativeId65FreshResearchPublicationBinding.FromWizard(plan);
            Check(ReferenceEquals(binding.WizardPlan,plan)&&binding.SavedContext==null&&binding.TracksCommittedFailure&&
                binding.OutputNames.SequenceEqual(new[]{"native-id65-fresh-wizard-p1-n32-r01.bin","native-id65-fresh-wizard-p1-n32-r01.receipt.json","native-id65-fresh-wizard-p1-n32-r01.cue"}),
                "Closed Wizard arm has no invented saved-project context and commits CUE last.");
            using(var cancel=new CancellationTokenSource())
            {
                cancel.Cancel();Check(!binding.PostCommitToken(cancel.Token).CanBeCanceled,"Wizard final token is noncancellable; this is policy, not postcommit execution proof.");
                await Refuse<OperationCanceledException>(()=>NativeId65FreshWizardResearchCandidatePlan.CaptureAsync("must-not-open","must-not-open",disc,cancel.Token));
                await Refuse<OperationCanceledException>(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishAsync(plan,"must-not-create",cancel.Token));
                await Refuse<OperationCanceledException>(()=>NativeId65FreshWizardResearchCandidatePublisher.VerifyAsync(plan,"must-not-create",cancel.Token));
            }
            foreach(var bad in new[]{(NativeId65FreshWizardResearchFault)(-1),(NativeId65FreshWizardResearchFault)999})
                await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,"must-not-create",bad),"Unknown");
            await Refuse<ArgumentNullException>(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishPostcommitForSmokeAsync(plan,"must-not-create",null!));
            await Refuse<InvalidDataException>(()=>NativeId65FreshResearchPublicationEngine.PublishDocumentPostcommitForSmokeAsync(binding,"must-not-create",(_,_)=>{},default),"saved-document");
            // Real old issuance: null is NOT a wrong-kind certificate.
            var starter=await NativeId65FreshDiscResearchCandidatePlan.CaptureAsync(bin,cue,compilation.DiscPlan);
            var oldBinding=NativeId65FreshResearchPublicationBinding.FromStarter(starter);
            await Refuse<InvalidDataException>(()=>NativeId65FreshResearchPublicationEngine.PublishWizardPostcommitForSmokeAsync(oldBinding,"must-not-create",(_,_)=>{},default),"actual fresh Wizard");
            Check(oldBinding.WizardPlan==null&&!oldBinding.TracksCommittedFailure&&oldBinding.CopyCueBytes().SequenceEqual(starter.CopyCueBytes()),
                "Genuine old binding keeps its original bytes and kind.");
            await CueControls(bin,cue,disc,plan);
            CandidateStreamCases.Source(bin,plan,Check);
            Check(saved==JsonSerializer.Serialize(new{compilation,family,resource,scene,fallback,host,body,exe,disc,plan}),"Every fixture preserves all immutable ancestors.");
            Console.WriteLine($"candidatePlanSha256={plan.PlanSha256} prospectiveReceiptSha256={Hash(plan.BuildReceipt(known))} receiptBytes={plan.BuildReceipt(known).Length} cueSha256={cueHash}");
        }
        finally{Original(bin);Check(FileHash(cue)==sourceCueHash,"Original CUE byte hash and actual EOF preserved.");}
        Console.WriteLine($"FRESH_WIZARD_RESEARCH_SOURCE_OK checks={checks} refusals={refusals} physicalOutputFiles=0 tinyCueAliasFixturesRemoved=true");
    }
    private static async Task CueControls(string bin,string cue,NativeId65FreshWizardResearchDiscPlan disc,NativeId65FreshWizardResearchCandidatePlan plan)
    {
        string root=Path.Combine(Path.GetTempPath(),"spyro-fresh-wizard-source-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        string alias=Path.Combine(root,"source-alias.bin"),replacement=Path.Combine(root,"tiny-other.bin"),privateCue=Path.Combine(root,"private.cue"),suppliedCue=Path.Combine(root,"supplied.cue");
        try
        {
            File.CreateSymbolicLink(alias,Path.GetFullPath(bin));File.WriteAllBytes(replacement,[1]);
            foreach(string newline in new[]{"\n","\r\n","\r"})
            foreach(bool bom in new[]{false,true})
            {
                byte[] bytes=Encoding.UTF8.GetBytes($"FILE \"{alias}\" BINARY{newline}  TRACK 01 MODE2/2352{newline}    INDEX 01 00:00:00{newline}");
                if(bom)bytes=[0xEF,0xBB,0xBF,..bytes];
                NativeId65FreshWizardResearchCandidatePlan.ValidateSourceCue(bytes,privateCue,bin);Check(true,"Same-snapshot CR/LF/BOM source FILE alias parity.");
            }
            byte[] owner=Encoding.UTF8.GetBytes($"FILE \"{alias}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");File.WriteAllBytes(privateCue,owner);
            var aliasPlan=await NativeId65FreshWizardResearchCandidatePlan.CaptureAsync(bin,privateCue,disc);
            File.Delete(alias);File.CreateSymbolicLink(alias,replacement);
            await Refuse<InvalidDataException>(()=>aliasPlan.RequireFreshAsync(),"CUE");
            File.Delete(alias);File.CreateSymbolicLink(alias,Path.GetFullPath(bin));await aliasPlan.RequireFreshAsync();
            File.WriteAllBytes(privateCue,[..owner,10]);await Refuse<InvalidDataException>(()=>aliasPlan.RequireFreshAsync(),"snapshot");
            File.WriteAllBytes(privateCue,owner);await aliasPlan.RequireFreshAsync();
            File.CreateSymbolicLink(suppliedCue,privateCue);
            var supplied=await NativeId65FreshWizardResearchCandidatePlan.CaptureAsync(bin,suppliedCue,disc);
            File.Delete(suppliedCue);File.WriteAllBytes(suppliedCue,owner);
            await Refuse<InvalidDataException>(()=>supplied.RequireFreshAsync(),"path binding");
            // Supplied BIN alias changes independently of the CUE FILE alias.
            string inputAlias=Path.Combine(root,"supplied.bin");File.CreateSymbolicLink(inputAlias,Path.GetFullPath(bin));
            try
            {
                var suppliedBin=await NativeId65FreshWizardResearchCandidatePlan.CaptureAsync(inputAlias,privateCue,disc);
                File.Delete(inputAlias);File.CreateSymbolicLink(inputAlias,replacement);
                await Refuse<InvalidDataException>(()=>suppliedBin.RequireFreshAsync(),"path binding");
            }
            finally{File.Delete(inputAlias);}
            await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchCandidatePlan.CaptureAsync(replacement,privateCue,disc),"CUE");
            await Refuse<ArgumentException>(()=>NativeId65FreshWizardResearchCandidatePlan.CaptureAsync("",cue,disc));
            await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchCandidatePublisher.VerifyAsync(plan,Path.Combine(root,"absent")),"missing");
            foreach(string name in NativeId65FreshWizardResearchCandidatePlan.OutputNames)Check(!File.Exists(Path.Combine(root,name)),"No candidate companions created by source fixtures.");
        }
        finally
        {
            // Only exact tiny entries created above; preserve any unexpected owner.
            foreach(string path in new[]{alias,replacement,privateCue,suppliedCue})if(File.Exists(path)||new FileInfo(path).LinkTarget!=null)File.Delete(path);
            if(!Directory.EnumerateFileSystemEntries(root).Any())Directory.Delete(root);
        }
        Check(!Directory.Exists(root),"Tiny source fixture cleanup complete.");
    }
    private static void Check(bool value,string message){if(!value)throw new InvalidDataException(message);checks++;}
    private static void Reject(Action action){try{action();}catch(Exception error)when(error is InvalidDataException or ArgumentNullException){refusals++;return;}throw new Exception("Expected source-bound refusal.");}
    private static async Task Refuse<T>(Func<Task> action,string contains="")where T:Exception
    {try{await action();}catch(T error)when(error.Message.Contains(contains,StringComparison.OrdinalIgnoreCase)){refusals++;return;}throw new Exception("Expected "+typeof(T).Name+" containing "+contains);}
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string FileHash(string path){using var file=File.OpenRead(path);long length=file.Length;string sha=Convert.ToHexStringLower(SHA256.HashData(file));Check(file.Length==length&&file.Position==length&&file.ReadByte()==-1&&file.Length==length&&file.Position==length,"Complete file actual EOF/length/position.");return sha;}
    private static void Original(string bin)=>Check(FileHash(bin)==NativeId65FreshDiscSource.OriginalSha256&&new FileInfo(bin).Length==661547040,"Original full image SHA and length before/finally.");
}

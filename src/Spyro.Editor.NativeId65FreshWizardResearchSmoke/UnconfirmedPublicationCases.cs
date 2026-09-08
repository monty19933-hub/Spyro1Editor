using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

// Explicit reviewed physical exercise only. The default/source routes below
// test arguments and genuine binding refusals, never stage an image.
internal static class UnconfirmedPublicationCases
{
    private const string Mode="--unconfirmed-reviewed";
    private const string SentinelName="uncertainty-stage-sentinel.txt";
    private static readonly byte[] Sentinel=Encoding.UTF8.GetBytes("foreign stage owner: preserve complete unconfirmed evidence\n");
    private static readonly byte[] ForeignLock=Encoding.UTF8.GetBytes("foreign publication lock: actual PublishNew must refuse\n");
    internal static bool IsMode(string[] args)=>args.Length>0&&args[0]==Mode;
    internal static void RequireArguments(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if(args.Length!=4||!IsMode(args)||args.Skip(1).Any(value=>string.IsNullOrWhiteSpace(value)||value.Contains('\0')||value.StartsWith("--",StringComparison.Ordinal))||!Path.IsPathFullyQualified(args[3]))
            throw new ArgumentException("Usage: --unconfirmed-reviewed ORIGINAL_BIN ORIGINAL_CUE ABSENT_ABSOLUTE_WORKSPACE");
    }
    internal static async Task RunFreeAsync()
    {
        int checks=0;string absolute=Path.Combine(Path.GetTempPath(),"must-not-create-unconfirmed");
        RequireArguments([Mode,"bin","cue",absolute]);checks++;
        foreach(string[] bad in new[]{Array.Empty<string>(),new[]{Mode},new[]{Mode,"bin"},new[]{Mode,"bin","cue"},new[]{Mode,"bin","cue",absolute,"extra"}})Reject(bad);
        Reject(null!);
        foreach(string mode in new[]{"--source","--publish-reviewed","--verify-reviewed","--stage-reviewed","--postcommit-reviewed","--Unconfirmed-reviewed",Mode+" "})
        {Check(!IsMode([mode]),"No other mode activates the uncertainty exercise.");checks++;Reject([mode,"bin","cue",absolute]);}
        for(int field=1;field<4;field++)foreach(string? blank in new string?[]{null,""," \t\r\n","\0","a\0b","--unknown","--source","--publish-reviewed",Mode})
        {string[] bad=[Mode,"bin","cue",absolute];bad[field]=blank!;Reject(bad);}
        foreach(string relative in new[]{"output","./output","../output"})Reject([Mode,"bin","cue",relative]);
        Check(!IsMode([])&&!ReviewedPublicationCases.IsReviewedMode([Mode])&&!PublicationBoundaryModes.IsMode([Mode]),"Distinct opt-in dispatch only.");checks++;
        await Refuse<ArgumentNullException>(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishUnconfirmedForSmokeAsync(null!,"must-not-create",_=>{}));checks++;
        await Refuse<ArgumentNullException>(()=>NativeId65FreshResearchPublicationEngine.PublishWizardUnconfirmedForSmokeAsync(null!,"must-not-create",_=>{},default));checks++;
        Console.WriteLine($"FRESH_WIZARD_UNCONFIRMED_ARGUMENTS_OK checks={checks} sourceOrOutputIo=0 physicalExerciseInvoked=false");
        void Reject(string[] bad){try{RequireArguments(bad);}catch(ArgumentException){checks++;return;}throw new InvalidDataException("Malformed uncertainty arguments accepted.");}
    }
    internal static async Task SourceApiAsync(NativeId65FreshWizardResearchCandidatePlan plan,NativeId65FreshResearchPublicationBinding oldBinding)
    {
        await Refuse<ArgumentNullException>(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishUnconfirmedForSmokeAsync(plan,"must-not-create",null!));
        await Refuse<InvalidDataException>(()=>NativeId65FreshResearchPublicationEngine.PublishWizardUnconfirmedForSmokeAsync(oldBinding,"must-not-create",_=>{},default),"actual fresh Wizard");
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();bool called=false;
        await Refuse<OperationCanceledException>(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishUnconfirmedForSmokeAsync(plan,"must-not-create",_=>called=true,cancellation.Token));
        Check(!called,"Pre-cancel must not call the new boundary or touch a destination.");
        Console.WriteLine("FRESH_WIZARD_UNCONFIRMED_SOURCE_API_OK checks=4 physicalOutputFiles=0");
    }
    internal static async Task RunAsync(string[] args)
    {
        RequireArguments(args);
        string image=Path.GetFullPath(args[1]),cue=Path.GetFullPath(args[2]);
        string root=ReviewedPublicationCases.RequireOutputPath(args[3],false);
        ReviewedPublicationCases.RequireSourceSeparation(image,cue,root);
        var before=ReviewedPublicationCases.ReadOriginalIdentities(image,cue);
        try
        {
            string report=await PublicationFailureCases.RunWithFinalCheck(async()=>
            {
                var plan=await ReviewedPublicationCases.CapturePrimary(image,cue);
                var binding=NativeId65FreshResearchPublicationBinding.FromWizard(plan);
                byte[] expectedCue=plan.CopyCueBytes(),expectedReceipt=plan.BuildReceipt(new(plan.ExpectedImageSha256,1290));
                string stage="",lease=Path.Combine(root,binding.LeaseName),transactionLock=Path.Combine(root,".export-publication.lock");
                int callbacks=0;NativeId65FreshResearchPublicationUnconfirmedException? observed=null;
                try
                {
                    _=await NativeId65FreshWizardResearchCandidatePublisher.PublishUnconfirmedForSmokeAsync(plan,root,path=>
                    {
                        callbacks++;Check(callbacks==1&&path==transactionLock,"Exactly one final-boundary callback supplies the exact transaction lock path.");
                        string[] stages=Directory.EnumerateDirectories(root).ToArray();
                        Check(stages.Length==1&&Path.GetFileName(stages[0]).StartsWith(binding.StagePrefix,StringComparison.Ordinal),"One actual engine-created stage.");
                        stage=stages[0];RequireDirectory(stage);RequireRegular(lease);
                        RequireNames(root,[Path.GetFileName(stage),binding.LeaseName]);
                        VerifyStage(plan,stage,expectedCue,expectedReceipt,false);
                        // Real new owners after all final engine guards; no thrown
                        // fixture error or substitute transaction result.
                        WriteNew(Path.Combine(stage,SentinelName),Sentinel);
                        WriteNew(path,ForeignLock);
                    });
                }
                catch(NativeId65FreshResearchPublicationUnconfirmedException error){observed=error;}
                Check(observed!=null&&callbacks==1,"Actual engine call escaped with the distinct Unconfirmed outcome, never success.");
                var failure=observed!;
                Check(failure.InnerException is IOException and not NativeId65FreshResearchPublicationUnconfirmedException and not ExportArtifactRollbackException&&
                    !ReviewedPublicationCases.ContainsCommittedFailure(failure),"The actual early transaction IOException remains primary; no committed or rollback classification is inferred.");
                Check(failure.OutputDirectory==root&&failure.ImagePath==Path.Combine(root,binding.ImageName)&&failure.CuePath==Path.Combine(root,binding.CueName)&&
                    failure.ReceiptPath==Path.Combine(root,binding.ReceiptName)&&failure.PlanSha256==plan.PlanSha256&&failure.RetainedRecoveryDirectory==root&&
                    failure.RetainedStageDirectory==stage&&failure.RetainedOutputLeasePath==lease&&failure.RetainedProjectLeasePath==""&&failure.PublicationLockPath==transactionLock,
                    "Every reported inspection path and plan identity comes from the actual closed binding.");
                RequireDirectory(root);RequireDirectory(stage);
                RequireNames(root,[Path.GetFileName(stage),binding.LeaseName,".export-publication.lock"]);
                foreach(string name in binding.OutputNames)
                    Check(!File.Exists(Path.Combine(root,name))&&!Directory.Exists(Path.Combine(root,name))&&new FileInfo(Path.Combine(root,name)).LinkTarget==null,
                        "This specific foreign-lock refusal left its final destination observed absent; not a generic abort inference.");
                VerifyStage(plan,stage,expectedCue,expectedReceipt,true);
                RequireBytes(transactionLock,ForeignLock);
                RequireRegular(lease);
                // Opening with exclusive sharing AFTER return proves the engine
                // closed its handle; this does not adopt the marker as ours.
                using(var input=new FileStream(lease,FileMode.Open,FileAccess.Read,FileShare.None))
                {Check(input.Length==32,"Retained output marker has its actual 32-byte owner.");byte[] owner=new byte[32];input.ReadExactly(owner);Check(input.Position==32&&input.ReadByte()==-1&&input.Position==32&&input.Length==32,"Retained marker exact EOF and closed engine handle.");}
                var recaptured=await ReviewedPublicationCases.CapturePrimary(image,cue);
                Check(!ReferenceEquals(plan.DiscAncestor,recaptured.DiscAncestor)&&plan.PlanSha256==recaptured.PlanSha256&&
                    plan.CopyCanonicalDocument().SequenceEqual(recaptured.CopyCanonicalDocument())&&plan.DiscPlanSha256==recaptured.DiscPlanSha256&&
                    plan.ExecutablePlanSha256==recaptured.ExecutablePlanSha256&&plan.BodyPlanSha256==recaptured.BodyPlanSha256&&
                    plan.HostPlanSha256==recaptured.HostPlanSha256&&plan.ScenePlanSha256==recaptured.ScenePlanSha256&&plan.FallbackPlanSha256==recaptured.FallbackPlanSha256&&
                    recaptured.CopyCueBytes().SequenceEqual(expectedCue)&&recaptured.BuildReceipt(new(plan.ExpectedImageSha256,1290)).SequenceEqual(expectedReceipt),
                    "Fresh actual source-chain recapture agrees, without turning a receipt or matching artifact bytes into transaction authority.");
                VerifyStage(recaptured,stage,expectedCue,expectedReceipt,true);
                RequireBytes(transactionLock,ForeignLock);
                RequireNames(root,[Path.GetFileName(stage),binding.LeaseName,".export-publication.lock"]);
                return "FRESH_WIZARD_UNCONFIRMED_REVIEWED_OK "+JsonSerializer.Serialize(new{
                    actualTransactionInvoked=true,outcome="Unconfirmed",finalDestinationsObservedAbsent=true,completeStageRetained=true,
                    independentPublicChainRecapture=true,originalsPreserved=true,nativeRuntime=false,disposeAfterRenameExercised=false,
                    sourceImageSha256=before.Image,sourceCueSha256=before.Cue,plan.PlanSha256,plan.ExpectedImageSha256,
                    changedRawSectors=1290,root,stage,outputLease=lease,transactionLock,primaryExceptionType=failure.InnerException!.GetType().FullName});
            },()=>ReviewedPublicationCases.RequireOriginalAsync(image,cue,before.Image,before.Cue));
            Console.WriteLine(report); // All observations and BOTH final originals completed.
        }
        catch(Exception error)
        {throw new IOException("Explicit uncertainty exercise failed; preserve every existing owner and do not retry "+root,error);}
    }
    private static void VerifyStage(NativeId65FreshWizardResearchCandidatePlan plan,string stage,byte[] cue,byte[] receipt,bool sentinel)
    {
        RequireDirectory(stage);
        RequireNames(stage,sentinel?[..plan.BoundOutputNames,SentinelName]:plan.BoundOutputNames);
        foreach(string name in plan.BoundOutputNames)RequireRegular(Path.Combine(stage,name));
        var verified=plan.VerifyImage(Path.Combine(stage,plan.BoundImageName),CancellationToken.None);
        Check(verified.OutputSha256=="c4a70f826b1a9a3d4ad8e36512be354b6205d27ea2c7a314776a51067f35b5a6"&&verified.ChangedSectors==1290,
            "Retained actual staged full image has independent primary SHA/sector-count/true EOF; no returned publication is manufactured.");
        RequireBytes(Path.Combine(stage,plan.BoundCueName),cue);RequireBytes(Path.Combine(stage,plan.BoundReceiptName),receipt);
        if(sentinel)RequireBytes(Path.Combine(stage,SentinelName),Sentinel);
    }
    private static void RequireDirectory(string path)=>Check(Directory.Exists(path)&&new DirectoryInfo(path).LinkTarget==null,"Expected unlinked directory: "+path);
    private static void RequireRegular(string path)=>Check(File.Exists(path)&&!Directory.Exists(path)&&new FileInfo(path).LinkTarget==null,"Expected regular unlinked owner: "+path);
    private static void RequireNames(string root,string[] names)=>Check(Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName).Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal)),"Exact complete retained owner set: "+root);
    private static void RequireBytes(string path,byte[] expected)
    {RequireRegular(path);using var input=File.OpenRead(path);NativeId65FreshResearchPublicationEngine.RequireCompanion(input,expected,CancellationToken.None);}
    private static void WriteNew(string path,byte[] bytes)
    {using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);output.Write(bytes);output.Flush(flushToDisk:true);}
    private static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    private static async Task Refuse<T>(Func<Task> action,string text="")where T:Exception
    {try{await action();}catch(T error)when(error.Message.Contains(text,StringComparison.OrdinalIgnoreCase)){return;}throw new InvalidDataException("Expected "+typeof(T).Name+" "+text);}
}

using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

// Explicit output runner after reviewed materializer and argument RED.
// Explicit reviewed CLI dispatch only: no default/source/staging activation.
internal static class ReviewedPublicationCases
{
    private const string ImageHash="c4a70f826b1a9a3d4ad8e36512be354b6205d27ea2c7a314776a51067f35b5a6";
    private const string OriginalHash="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const long ImageLength=661547040;
    internal static bool IsReviewedMode(string[] args)=>args.Length>0&&args[0] is "--publish-reviewed" or "--verify-reviewed";
    internal static void RequireArguments(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if(args.Length!=4||!IsReviewedMode(args)||args.Skip(1).Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Usage: --publish-reviewed BIN CUE ABSENT-output-directory | --verify-reviewed BIN CUE EXISTING-output-directory");
    }
    internal static void RunArgumentTests()
    {
        int checks=0;
        foreach(string mode in new[]{"--publish-reviewed","--verify-reviewed"})
        {
            RequireArguments([mode,"source.bin","source.cue","output"]);checks++;
            foreach(string[] bad in new[]{new[]{mode},new[]{mode,"bin"},new[]{mode,"bin","cue"},new[]{mode,"bin","cue","output","extra"}})Reject(bad);
            for(int field=1;field<4;field++)foreach(string? blank in new string?[]{null,""," \t\r\n"})
            {string[] bad=[mode,"source.bin","source.cue","output"];bad[field]=blank!;Reject(bad);}
        }
        foreach(string mode in new[]{"--publish","--verify","--source","--stage-faults","--Publish-reviewed","--verify-reviewed "})
        {Check(!IsReviewedMode([mode]),"Another mode cannot dispatch reviewed publication.");checks++;Reject([mode,"bin","cue","output"]);}
        Reject([]);Reject(null!);Check(!IsReviewedMode([]),"Default cannot dispatch publication.");checks++;
        Console.WriteLine($"FRESH_WIZARD_REVIEWED_ARGUMENTS_OK checks={checks} sourceOrOutputIo=0");
        void Reject(string[] bad){try{RequireArguments(bad);}catch(ArgumentException){checks++;return;}throw new InvalidDataException("Malformed reviewed arguments were accepted.");}
    }
    internal static async Task RunAsync(string[] args)
    {
        RequireArguments(args);bool publish=args[0]=="--publish-reviewed";
        string image=Path.GetFullPath(args[1]),cue=Path.GetFullPath(args[2]),output=RequireOutputPath(args[3],!publish);
        RequireSourceSeparation(image,cue,output);
        var (sourceBefore,cueBefore)=ReadOriginalIdentities(image,cue);
        bool committed=false;
        try
        {
            string report=await PublicationFailureCases.RunWithFinalCheck(async()=>
            {
                var plan=await CapturePrimary(image,cue);
                byte[] expectedCue=plan.CopyCueBytes(),expectedReceipt=plan.BuildReceipt(new(ImageHash,1290));
                string[]? before=publish?null:ReadCompleteOwners(output,expectedCue,expectedReceipt);
                var result=publish
                    ?await NativeId65FreshWizardResearchCandidatePublisher.PublishAsync(plan,output)
                    :await NativeId65FreshWizardResearchCandidatePublisher.VerifyAsync(plan,output);
                committed=publish;
                Check(result.ImageSha256==ImageHash&&result.PlanSha256==plan.PlanSha256&&result.ChangedRawSectors==1290&&
                    result.WholeImageReadback&&result.ResearchOnly&&!result.RuntimeVerified&&!result.NormalCreateBinApproved&&!result.CompleteNativeDependencyClosureProven,
                    "Actual physical publication result matches exact primary without runtime approval.");
                if(result.RetainedRecoveryDirectory.Length!=0)throw new IOException("Published owners retain recovery evidence; preserve "+result.RetainedRecoveryDirectory);
                string[] after=ReadCompleteOwners(output,expectedCue,expectedReceipt);
                if(before!=null)Check(before.SequenceEqual(after),"Read-only verification preserves all three complete owners.");
                var recaptured=await CapturePrimary(image,cue);
                Check(!ReferenceEquals(plan.DiscAncestor,recaptured.DiscAncestor)&&plan.PlanSha256==recaptured.PlanSha256&&
                    plan.CopyCanonicalDocument().SequenceEqual(recaptured.CopyCanonicalDocument())&&plan.DiscPlanSha256==recaptured.DiscPlanSha256&&
                    plan.ExecutablePlanSha256==recaptured.ExecutablePlanSha256&&plan.BodyPlanSha256==recaptured.BodyPlanSha256&&
                    plan.HostPlanSha256==recaptured.HostPlanSha256&&plan.ScenePlanSha256==recaptured.ScenePlanSha256&&plan.FallbackPlanSha256==recaptured.FallbackPlanSha256,
                    "Independent actual public chain recapture preserves canonical and complete ancestor identities.");
                Check(recaptured.CopyCueBytes().SequenceEqual(expectedCue)&&recaptured.BuildReceipt(new(ImageHash,1290)).SequenceEqual(expectedReceipt),
                    "Actual recapture reproduces exact code-owned CUE/receipt, not receipt-deserialized authority.");
                var verified=await NativeId65FreshWizardResearchCandidatePublisher.VerifyAsync(recaptured,output);
                Check(verified==result&&ReadCompleteOwners(output,expectedCue,expectedReceipt).SequenceEqual(after),
                    "Fresh recapture reopens and verifies every physical owner without modification.");
                return (publish?"FRESH_WIZARD_RESEARCH_PUBLISHED_REVIEWED ":"FRESH_WIZARD_RESEARCH_VERIFIED_REVIEWED ")+JsonSerializer.Serialize(new{
                    result,sourceImageSha256=sourceBefore,sourceCueSha256=cueBefore,
                    canonicalDocumentSha256=plan.CanonicalDocumentSha256,discPlanSha256=plan.DiscPlanSha256,
                    executablePlanSha256=plan.ExecutablePlanSha256,bodyPlanSha256=plan.BodyPlanSha256,hostPlanSha256=plan.HostPlanSha256,
                    scenePlanSha256=plan.ScenePlanSha256,fallbackPlanSha256=plan.FallbackPlanSha256,
                    outputImageSha256=after[0],outputCueSha256=after[1],outputReceiptSha256=after[2],
                    independentPublicChainRecapture=true,originalUnchanged=true,postcommitCancellationExercised=false,
                    runtime=false,normalExport=false,projectSaveBindingClaimed=false});
            },()=>RequireOriginalAsync(image,cue,sourceBefore,cueBefore));
            Console.WriteLine(report); // Only after both complete original checks.
        }
        catch(Exception error)when(committed||ContainsCommittedFailure(error))
        {throw new IOException("Fresh Wizard research artifacts were already committed; preserve "+output,error);}
    }
    internal static bool ContainsCommittedFailure(Exception error)=>
        error is NativeId65FreshWizardResearchCommittedException||
        (error is AggregateException aggregate?aggregate.InnerExceptions.Any(ContainsCommittedFailure):
            error.InnerException!=null&&ContainsCommittedFailure(error.InnerException));
    internal static string RequireOutputPath(string path,bool existing)
    {
        if(!Path.IsPathFullyQualified(path))throw new ArgumentException("Reviewed output/workspace must be explicitly absolute.");
        string output=Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        for(string? owner=output;owner!=null;owner=Directory.GetParent(owner)?.FullName)
            if(new FileInfo(owner).LinkTarget!=null||new DirectoryInfo(owner).LinkTarget!=null)
                throw new InvalidDataException("Reviewed output requires unlinked ancestors.");
        if(File.Exists(output)||(existing?!Directory.Exists(output):Directory.Exists(output)))
            throw new InvalidDataException(existing?"Reviewed verification requires an existing directory.":"Reviewed publication requires a new absent directory.");
        if(Directory.GetParent(output) is not {Exists:true})throw new InvalidDataException("Reviewed output parent must already exist.");
        return output;
    }
    internal static void RequireSourceSeparation(string image,string cue,string output)
    {
        Spyro.Editor.Core.Persistence.PersistencePathSafety.EnsureTreesDoNotOverlap(image,output,"Reviewed output overlaps original BIN.");
        Spyro.Editor.Core.Persistence.PersistencePathSafety.EnsureTreesDoNotOverlap(cue,output,"Reviewed output overlaps original CUE.");
    }
    internal static (string Image,string Cue) ReadOriginalIdentities(string image,string cue)
    {
        string source=HashFile(image,ImageLength),companion=HashFile(cue);
        Check(source==OriginalHash,"Whole original identity before source capture.");
        return(source,companion);
    }
    internal static async Task RequireOriginalAsync(string image,string cue,string sourceBefore,string cueBefore)
    {
        _=await PublicationFailureCases.RunWithFinalCheck(()=>
        {
            Check(HashFile(image,ImageLength)==sourceBefore,"Complete original BIN SHA, length/position and EOF preserved.");
            return Task.FromResult(true);
        },()=>
        {
            Check(HashFile(cue)==cueBefore,"Complete original CUE SHA, length/position and EOF preserved.");
            return Task.CompletedTask;
        });
    }
    internal static async Task<NativeId65FreshWizardResearchCandidatePlan> CapturePrimary(string image,string cue)
    {
        var intent=NativeId65FreshDocumentIntent.Create(8448,8448,8704,1024,1024,8960,8960,64,new(80,128,112),new(128,96,64),19);
        var compilation=await NativeId65FreshDocumentCompilation.CaptureAsync(image,intent);
        var family=await NativeWizardFamilyAssetPlan.CaptureAsync(image);
        var resource=await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(image,compilation,family);
        var scene=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(image,compilation,family,resource,new(8704,8960,8704,9216,8960));
        var fallback=await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(image,resource);
        var host=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(image,scene,fallback);
        var body=await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(image,host);
        var exe=await NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(image,body);
        var disc=await NativeId65FreshWizardResearchDiscPlan.CaptureAsync(image,exe);
        var plan=await NativeId65FreshWizardResearchCandidatePlan.CaptureAsync(image,cue,disc);
        Check(plan.ExpectedImageSha256==ImageHash&&plan.ExpectedImageByteLength==ImageLength&&plan.ExpectedChangedRawSectors==1290,
            "Actual primary source chain enters only the closed independently expected physical recipe.");
        return plan;
    }
    private static string[] ReadCompleteOwners(string directory,byte[] cue,byte[] receipt)
    {
        string[] names=[NativeId65FreshWizardResearchCandidatePlan.ImageName,NativeId65FreshWizardResearchCandidatePlan.CueName,NativeId65FreshWizardResearchCandidatePlan.ReceiptName];
        Check(Directory.EnumerateFileSystemEntries(directory).Select(Path.GetFileName).Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal)),
            "Exact three-artifact set, no extra/recovery/unknown owner silently accepted.");
        foreach(string name in names)
        {string path=Path.Combine(directory,name);Check(File.Exists(path)&&!Directory.Exists(path)&&new FileInfo(path).LinkTarget==null,"Only existing non-linked regular artifact files.");}
        string hash=HashFile(Path.Combine(directory,names[0]),ImageLength);Check(hash==ImageHash,"Complete independent primary raw image hash.");
        using(var input=File.OpenRead(Path.Combine(directory,names[1])))NativeId65FreshResearchPublicationEngine.RequireCompanion(input,cue,CancellationToken.None);
        using(var input=File.OpenRead(Path.Combine(directory,names[2])))NativeId65FreshResearchPublicationEngine.RequireCompanion(input,receipt,CancellationToken.None);
        return [hash,HashFile(Path.Combine(directory,names[1]),cue.Length),HashFile(Path.Combine(directory,names[2]),receipt.Length)];
    }
    private static string HashFile(string path,long? exactLength=null)
    {
        using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);long length=input.Length;
        Check(exactLength.HasValue?length==exactLength.Value:length is >=1 and <=65536,"Exact image/companion or bounded source CUE length.");
        string hash=Convert.ToHexStringLower(SHA256.HashData(input));
        Check(input.Length==length&&input.Position==length&&input.ReadByte()==-1&&input.Position==length&&input.Length==length,
            "Whole hash includes actual EOF and final length/position guards.");
        return hash;
    }
    private static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
}

using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

// Explicit reviewed CLI only. Default/source cases cannot invoke this workflow.
// No receipt-deserialized authority, output cleanup, overwrite or retry path.
internal static class SeparatedPublicationCases
{
    private const long ImageLength=661547040;
    private const string ImageHash="e57fedebb59ffc9020de00562baccbc5a6e09bffd5f24f665d34d5f03163cbdb";
    private const string OriginalHash="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const string OriginalCueHash="05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766";
    private const string Name="native-id65-fresh-wizard-separated-p1-n32-r01";
    private static string[] Names=>[Name+".bin",Name+".receipt.json",Name+".cue"];
    internal sealed record Request(bool Publish,string Image,string Cue,string Output);
    internal static bool IsReviewedMode(string[] args)=>args.Length>0&&args[0] is "--publish-separated-reviewed" or "--verify-separated-reviewed";
    internal static Request Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if(args.Length!=4||!IsReviewedMode(args)||args.Skip(1).Any(value=>string.IsNullOrWhiteSpace(value)||value.Contains('\0')||value.StartsWith("--",StringComparison.Ordinal))||
            !Path.IsPathFullyQualified(args[3]))
            throw new ArgumentException("Usage: --publish-separated-reviewed BIN CUE ABSENT-absolute-directory | --verify-separated-reviewed BIN CUE EXISTING-absolute-directory");
        return new(args[0]=="--publish-separated-reviewed",args[1],args[2],args[3]);
    }

    internal static async Task RunAsync(string[] args)
    {
        var request=Parse(args);
        string image=Path.GetFullPath(request.Image),cue=Path.GetFullPath(request.Cue),root=RequireLocation(request,image,cue);
        FileStream? original=null,originalCue=null;bool publishReturned=false;
        try
        {
            string report=await FinishAsync(async()=>
            {
                original=OpenOwned(image,ImageLength);originalCue=OpenOwned(cue,88);
                Authenticate(original,ImageLength,OriginalHash);Authenticate(originalCue,88,OriginalCueHash);
                var plan=await Capture(image,cue);
                byte[] expectedCue=plan.CopyCueBytes(),expectedReceipt=plan.BuildReceipt(new(ImageHash,1290));
                Check(plan.BoundOutputNames.SequenceEqual(Names),"Only separated code-owned names, CUE last.");
                string[]? before=request.Publish?null:ReadCompleteOwners(root,expectedCue,expectedReceipt);
                var result=request.Publish
                    ?await NativeId65FreshWizardResearchCandidatePublisher.PublishAsync(plan,root)
                    :await NativeId65FreshWizardResearchCandidatePublisher.VerifyAsync(plan,root);
                publishReturned=request.Publish; // Only the actual publishing API's return acknowledges commit here.
                Check(result.ImageSha256==ImageHash&&result.PlanSha256==plan.PlanSha256&&result.ChangedRawSectors==1290&&
                    result.WholeImageReadback&&result.ResearchOnly&&!result.RuntimeVerified&&!result.NormalCreateBinApproved&&!result.CompleteNativeDependencyClosureProven&&
                    result.OutputDirectory==root&&result.ImagePath==Path.Combine(root,Names[0])&&result.ReceiptPath==Path.Combine(root,Names[1])&&result.CuePath==Path.Combine(root,Names[2]),
                    "Actual result must bind separated whole-image/readback/paths without promoting runtime.");
                Check(result.RetainedRecoveryDirectory.Length==0,"Retained recovery is not reviewed clean success; preserve all output.");
                string[] after=ReadCompleteOwners(root,expectedCue,expectedReceipt);
                if(before!=null)Check(before.SequenceEqual(after),"Read-only Verify preserves every complete artifact.");
                return (request.Publish?"FRESH_WIZARD_SEPARATED_PUBLISHED_REVIEWED ":"FRESH_WIZARD_SEPARATED_VERIFIED_REVIEWED ")+JsonSerializer.Serialize(new{
                    result,bindingProfileId=plan.BindingProfileId,sourceImageSha256=OriginalHash,sourceCueSha256=OriginalCueHash,
                    outputImageSha256=after[0],outputReceiptSha256=after[1],outputCueSha256=after[2],
                    sourceAndCueFinalChecks=true,sourceAndCueClosed=true,actualPublicChainCapture=true,
                    projectSaveBindingClaimed=false,runtimeVerified=false,normalExport=false});
            },[
                ()=>{if(original!=null)Authenticate(original,ImageLength,OriginalHash);},
                ()=>{if(originalCue!=null)Authenticate(originalCue,88,OriginalCueHash);},
                ()=>original?.Dispose(),()=>originalCue?.Dispose()]);
            Console.WriteLine(report); // Never before BOTH final SHA/EOF checks and BOTH terminal close attempts.
        }
        catch(Exception error)
        {
            Console.Error.WriteLine("FRESH_WIZARD_SEPARATED_FAILED "+JsonSerializer.Serialize(new{
                mode=request.Publish?"publish":"verify",outcome=FailureOutcome(error,publishReturned,request.Publish),outputDirectory=root,
                outputsRetained=true,automaticRetry=false,success=false,errors=Leaves(error).Select(e=>new{type=e.GetType().FullName,e.Message})}));
            throw;
        }
    }

    // CLI-only neutral accounting; callers cannot manufacture a Core plan/result.
    internal static async Task<string> FinishAsync(Func<Task<string>> action,IReadOnlyList<Action> finalActions)
    {
        List<Exception> errors=[];string? report=null;
        try{report=await action();}catch(Exception error){errors.Add(error);}
        foreach(var final in finalActions)try{final();}catch(Exception error){errors.Add(error);}
        if(errors.Count==1)ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if(errors.Count>1)throw new IOException("Separated reviewed operation has primary/final/close failures; preserve outputs and do not retry.",new AggregateException(errors));
        return report!;
    }
    internal static string FailureOutcome(Exception error,bool publishReturned,bool publish)
    {
        // Exception type comes from actual production invocation, never a receipt or observed file state.
        if(Contains<NativeId65FreshResearchPublicationUnconfirmedException>(error))return "unconfirmed";
        if(publishReturned||Contains<NativeId65FreshWizardResearchCommittedException>(error))return "committed-with-failure";
        return publish?"not-confirmed":"verification-failed";
    }
    private static bool Contains<T>(Exception error)where T:Exception=>error is T||
        (error is AggregateException a?a.InnerExceptions.Any(Contains<T>):error.InnerException!=null&&Contains<T>(error.InnerException));
    private static IEnumerable<Exception> Leaves(Exception error)
    {
        yield return error;
        if(error is AggregateException a){foreach(var e in a.InnerExceptions)foreach(var item in Leaves(e))yield return item;}
        else if(error.InnerException!=null){foreach(var item in Leaves(error.InnerException))yield return item;}
    }
    private static string RequireLocation(Request request,string image,string cue)
    {
        string output=Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.Output));
        PersistencePathSafety.EnsureTreesDoNotOverlap(image,output,"Separated output overlaps original BIN.");
        PersistencePathSafety.EnsureTreesDoNotOverlap(cue,output,"Separated output overlaps original CUE.");
        for(string? owner=output;owner!=null;owner=Directory.GetParent(owner)?.FullName)
            Check(new FileInfo(owner).LinkTarget==null&&new DirectoryInfo(owner).LinkTarget==null,"Reviewed output requires unlinked ancestors.");
        Check(!File.Exists(output)&&(request.Publish?!Directory.Exists(output):Directory.Exists(output)),
            request.Publish?"Publication requires a new absent output directory.":"Verification requires an existing output directory.");
        Check(Directory.GetParent(output) is {Exists:true},"Explicit output parent must already exist.");
        if(!request.Publish)RequireNames(output);
        return output;
    }
    private static async Task<NativeId65FreshWizardResearchCandidatePlan> Capture(string image,string cue)
    {
        var floor=NativeId65FreshDocumentIntent.Create(8448,8448,8704,2048,2048,8960,8960,64,new(80,128,112),new(128,96,64),19);
        var document=NativeFreshLevelDocumentIntent.Create(floor,[NativeFreshWizardRouteIntent.Create("11111111111111111111111111111111",9984,9984,8704,9472,9984)]);
        var compilation=await NativeId65FreshDocumentCompilation.CaptureAsync(image,floor);
        var family=await NativeWizardFamilyAssetPlan.CaptureAsync(image);
        var resource=await NativeId65FreshWizardResearchResourcePlan.CaptureAuthoredAsync(image,compilation,family);
        var scene=await NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync(image,document,compilation,family,resource);
        var clearance=await NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync(image,document,scene);
        var fallback=await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(image,resource);
        var host=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(image,scene,fallback);
        var body=await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(image,host);
        var exe=await NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(image,body);
        var disc=await NativeId65FreshWizardResearchDiscPlan.CaptureAsync(image,exe);
        var plan=await NativeId65FreshWizardResearchCandidatePlan.CaptureSeparatedAsync(image,cue,disc,clearance);
        Check(plan.ExpectedImageSha256==ImageHash&&plan.ExpectedImageByteLength==ImageLength&&plan.ExpectedChangedRawSectors==1290,
            "Only actual separated public chain with independent full-image expectation.");
        return plan;
    }
    private static void RequireNames(string root)=>Check(Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName).Order(StringComparer.Ordinal)
        .SequenceEqual(Names.Order(StringComparer.Ordinal)),"Exact three separated owners only; no unknown/recovery entries.");
    private static string[] ReadCompleteOwners(string root,byte[] cue,byte[] receipt)
    {
        RequireNames(root);
        using(var image=OpenOwned(Path.Combine(root,Names[0]),ImageLength))Authenticate(image,ImageLength,ImageHash);
        CheckCompanion(Path.Combine(root,Names[1]),receipt);CheckCompanion(Path.Combine(root,Names[2]),cue);
        RequireNames(root);return [ImageHash,Hash(receipt),Hash(cue)];
    }
    private static void CheckCompanion(string path,byte[] expected)
    {
        Check(expected.Length is >0 and <=65536,"Bounded exact companion.");
        using var input=OpenOwned(path,expected.Length);
        NativeId65FreshResearchPublicationEngine.RequireCompanion(input,expected,CancellationToken.None);
        Authenticate(input,expected.Length,Hash(expected));
    }
    private static FileStream OpenOwned(string path,long length)
    {
        var info=new FileInfo(path);
        Check(info.Exists&&!Directory.Exists(path)&&info.LinkTarget==null&&info.Length==length,"Exact non-linked file length before open.");
        return new(path,FileMode.Open,FileAccess.Read,FileShare.Read);
    }
    private static void Authenticate(Stream input,long length,string expected)
    {
        Check(input.CanRead&&input.CanSeek&&input.Length==length,"Held original/output exact readable length.");
        input.Position=0;using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] block=new byte[2352*256];
        for(long at=0;at<length;)
        {
            int n=(int)Math.Min(block.Length,length-at);input.ReadExactly(block.AsSpan(0,n));at+=n;hash.AppendData(block.AsSpan(0,n));
            Check(input.Length==length&&input.Position==at,"Held hash read lost length/position.");
        }
        Check(input.ReadByte()==-1&&input.Length==length&&input.Position==length&&Convert.ToHexStringLower(hash.GetHashAndReset())==expected,
            "Whole exact SHA/true EOF/final length and position.");
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
}

using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

// Explicit, separately authorized CLI modes only. Neither default nor --source
// can reach this output-producing branch. No source receipt issues authority.
internal static class ReviewedPublicationCases
{
    private const string ExpectedImage="612e68e1a8642d42f2417fd6153368c9549094ac391a3835c9cf6dfee7808cba";
    private const string OfficialImage="/Users/john/Documents/Codex/2026-04-24/could-you-create-a-level-editor/Spyro the Dragon (USA).bin";
    private const string OfficialCue="/Users/john/Documents/Codex/2026-04-24/could-you-create-a-level-editor/Spyro the Dragon (USA).cue";

    internal static bool IsReviewedMode(string[] args)=>args.Length>0&&args[0] is "--publish-reviewed" or "--verify-reviewed";
    internal static void RequireArguments(string[] args)
    {
        if(args.Length!=5||!IsReviewedMode(args)||args.Skip(1).Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Usage: --publish-reviewed BIN CUE workspace ABSENT-output-directory | --verify-reviewed BIN CUE workspace EXISTING-output-directory");
    }

    internal static void RunArgumentTests()
    {
        int checks=0;
        foreach(string mode in new[]{"--publish-reviewed","--verify-reviewed"})
        {
            RequireArguments([mode,"source.bin","source.cue","workspace","output"]);checks++;
            foreach(string[] bad in new[]{new[]{mode},new[]{mode,"source.bin","source.cue","workspace"},
                new[]{mode,"source.bin","source.cue","workspace","output","extra"},
                new[]{mode,"source.bin","source.cue","workspace"," "}})
                Reject(bad);
        }
        foreach(string mode in new[]{"--publish","--verify","--source","--exercise-staging","--Publish-reviewed"})
        {if(IsReviewedMode([mode]))throw new InvalidDataException("Implicit publication mode accepted.");checks++;Reject([mode,"bin","cue","workspace","output"]);}
        Reject([]);
        Console.WriteLine($"NATIVE_TWO_WIZARD_REVIEWED_ARGUMENTS_OK checks={checks} sourceOrOutputIo=0");
        void Reject(string[] args)
        {try{RequireArguments(args);}catch(ArgumentException){checks++;return;}throw new InvalidDataException("Reviewed mode accepted malformed arguments.");}
    }

    internal static async Task RunAsync(string[] args)
    {
        RequireArguments(args);bool publish=args[0]=="--publish-reviewed";
        string image=Path.GetFullPath(args[1]),cue=Path.GetFullPath(args[2]),output=Path.GetFullPath(args[4]);
        if(new FileInfo(output).LinkTarget!=null||new DirectoryInfo(output).LinkTarget!=null||File.Exists(output)||
            (publish?Directory.Exists(output):!Directory.Exists(output)))
            throw new InvalidDataException(publish?"Reviewed publication requires an absent new output directory.":"Reviewed verification requires an existing non-linked directory.");
        string originalBefore=HashFile(image,NativeMobySourceInventory.OriginalImageByteLength),cueBefore=HashFile(cue);
        Check(originalBefore==NativeMobySourceInventory.OriginalImageSha256,"complete original source identity before capture");
        var catalog=LevelCatalog.Load(args[3]);
        var plan=await NativeDarkHollowTwoWizardResearchCandidatePlan.CaptureAsync(image,cue,catalog);
        Check(plan.WadSha256=="4e7c47911822087eb30e531b8491b7548cfdf18d069e0a24a12e8711be971a94"&&
            plan.RootSha256=="7a881ab875392b331f65dc294368ebc6963998b5c24632acf2f63f3bff0cc6a0","independent complete WAD/root owners");
        if(image==OfficialImage&&cue==OfficialCue)
            Check(plan.PlanSha256=="efed20f6d745d33190bec4b6d2cd038c4916bcda70d5b170c180f5e8de92eae1","reviewed exact-path Plan golden");
        // Plan/receipt identity includes original paths. Other exact copies are
        // freshly source-issued and must match independent recapture below.
        byte[] expectedCue=plan.CopyCueBytes(),expectedReceipt=plan.BuildReceipt((ExpectedImage,46341));
        string[]? before=publish?null:ReadCompleteOwners(output,expectedCue,expectedReceipt);
        NativeWizardResearchPublication result=publish
            ?await NativeDarkHollowTwoWizardResearchCandidatePublisher.PublishAsync(plan,output)
            :await NativeDarkHollowTwoWizardResearchCandidatePublisher.VerifyAsync(plan,output);
        Check(result.ImageSha256==ExpectedImage&&result.PlanSha256==plan.PlanSha256&&result.ChangedRawSectors==46341&&
            result.WholeImageReadback&&result.ResearchOnly&&!result.RuntimeVerified&&!result.NormalCreateBinApproved&&
            result.RetainedRecoveryDirectory=="","actual independently pinned research-only result");
        string[] after=ReadCompleteOwners(output,expectedCue,expectedReceipt);
        if(before!=null)Check(after.SequenceEqual(before),"read-only verification preserves all three full artifact owners");
        var recaptured=await NativeDarkHollowTwoWizardResearchCandidatePlan.CaptureAsync(image,cue,catalog);
        Check(recaptured.PlanSha256==plan.PlanSha256&&recaptured.Appended.ProposedOwners.Serialize().SequenceEqual(plan.Appended.ProposedOwners.Serialize()),"independent private factory recapture retains exact Plan and temporal ledger");
        Check(recaptured.CopyCueBytes().SequenceEqual(expectedCue)&&recaptured.BuildReceipt((ExpectedImage,46341)).SequenceEqual(expectedReceipt),"full recaptured CUE and receipt bytes match first source-issued plan");
        var verified=await NativeDarkHollowTwoWizardResearchCandidatePublisher.VerifyAsync(recaptured,output);
        Check(verified==result&&ReadCompleteOwners(output,expectedCue,expectedReceipt).SequenceEqual(after),"independent recapture verifies complete image/companions without mutation");
        Check(HashFile(image,NativeMobySourceInventory.OriginalImageByteLength)==originalBefore&&HashFile(cue)==cueBefore,"original BIN and CUE exact before/after reviewed action");
        Console.WriteLine((publish?"NATIVE_TWO_WIZARD_RESEARCH_PUBLISHED_REVIEWED ":"NATIVE_TWO_WIZARD_RESEARCH_VERIFIED_REVIEWED ")+JsonSerializer.Serialize(new
        {
            result,sourceImageSha256=originalBefore,sourceCueSha256=cueBefore,outputImageSha256=after[0],
            outputCueSha256=after[1],outputReceiptSha256=after[2],independentFactoryRecapture=true,
            originalUnchanged=true,runtime=false,normalExport=false
        }));
    }

    private static string[] ReadCompleteOwners(string directory,byte[] cue,byte[] receipt)
    {
        string[] names=[NativeDarkHollowTwoWizardResearchCandidatePlan.ImageName,NativeDarkHollowTwoWizardResearchCandidatePlan.CueName,NativeDarkHollowTwoWizardResearchCandidatePlan.ReceiptName];
        Check(Directory.EnumerateFileSystemEntries(directory).Select(Path.GetFileName).Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal)),"exact three-artifact set, no hidden residue");
        foreach(string name in names)
        {
            string path=Path.Combine(directory,name);
            Check(new FileInfo(path).LinkTarget==null&&!Directory.Exists(path),"complete owner is a regular non-linked file");
        }
        string image=HashFile(Path.Combine(directory,names[0]),NativeMobySourceInventory.OriginalImageByteLength);
        Check(image==ExpectedImage,"independent full physical BIN golden");
        using(var input=File.OpenRead(Path.Combine(directory,names[1])))NativeDarkHollowWizardResearchCandidatePublisher.RequireCompanion(input,cue);
        using(var input=File.OpenRead(Path.Combine(directory,names[2])))NativeDarkHollowWizardResearchCandidatePublisher.RequireCompanion(input,receipt);
        return[image,HashFile(Path.Combine(directory,names[1]),cue.Length),HashFile(Path.Combine(directory,names[2]),receipt.Length)];
    }
    private static string HashFile(string path,long? requiredLength=null)
    {
        using FileStream input=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);long length=input.Length;
        Check(!requiredLength.HasValue||length==requiredLength.Value,"full owner has exact bounded length");
        string hash=Convert.ToHexStringLower(SHA256.HashData(input));
        Check(input.Length==length&&input.Position==length,"full owner length stable across read");return hash;
    }
    private static void Check(bool value,string reason){if(!value)throw new InvalidDataException(reason);}
}

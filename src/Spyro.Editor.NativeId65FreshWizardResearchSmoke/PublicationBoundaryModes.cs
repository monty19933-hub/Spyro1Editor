// Explicit physical test modes only. Neither defaults nor --source enters here.
// Stage mode forces every precommit refusal; postcommit retains nine real sets.
internal static class PublicationBoundaryModes
{
    internal static bool IsMode(string[] args)=>args.Length>0&&args[0] is "--stage-reviewed" or "--postcommit-reviewed";
    internal static void RequireArguments(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if(args.Length!=4||!IsMode(args)||args.Skip(1).Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Usage: --stage-reviewed BIN CUE ABSENT-absolute-workspace | --postcommit-reviewed BIN CUE ABSENT-absolute-workspace");
    }
    internal static void RunArgumentTests()
    {
        int checks=0;
        foreach(string mode in new[]{"--stage-reviewed","--postcommit-reviewed"})
        {
            RequireArguments([mode,"source.bin","source.cue","workspace"]);checks++;
            foreach(string[] bad in new[]{new[]{mode},new[]{mode,"bin"},new[]{mode,"bin","cue"},new[]{mode,"bin","cue","workspace","extra"}})Reject(bad);
            for(int field=1;field<4;field++)foreach(string? blank in new string?[]{null,""," \t\r\n"})
            {string[] bad=[mode,"bin","cue","workspace"];bad[field]=blank!;Reject(bad);}
            if(ReviewedPublicationCases.IsReviewedMode([mode]))throw new InvalidDataException("Fault tests cannot dispatch the reviewed candidate modes.");
            checks++;
        }
        foreach(string mode in new[]{"--source","--publish-reviewed","--verify-reviewed","--stage-faults","--Stage-reviewed","--postcommit-reviewed "})
        {if(IsMode([mode]))throw new InvalidDataException("Unrelated mode dispatches boundary writes.");checks++;Reject([mode,"bin","cue","workspace"]);}
        Reject([]);Reject(null!);if(IsMode([]))throw new InvalidDataException("Defaults dispatch physical tests.");checks++;
        // Relative output refusal occurs before any directory/file operation.
        foreach(string path in new[]{"output","./output","../output"})
            try{ReviewedPublicationCases.RequireOutputPath(path,false);throw new InvalidDataException("Relative output accepted.");}
            catch(ArgumentException){checks++;}
        Console.WriteLine($"FRESH_WIZARD_BOUNDARY_ARGUMENTS_OK checks={checks} sourceOrOutputIo=0");
        void Reject(string[] bad){try{RequireArguments(bad);}catch(ArgumentException){checks++;return;}throw new InvalidDataException("Malformed boundary arguments accepted.");}
    }
    internal static async Task RunAsync(string[] args)
    {
        RequireArguments(args);
        string image=Path.GetFullPath(args[1]),cue=Path.GetFullPath(args[2]);
        string workspace=ReviewedPublicationCases.RequireOutputPath(args[3],false);
        ReviewedPublicationCases.RequireSourceSeparation(image,cue,workspace);
        var before=ReviewedPublicationCases.ReadOriginalIdentities(image,cue);
        try
        {
            _=await PublicationFailureCases.RunWithFinalCheck(async()=>
            {
                var plan=await ReviewedPublicationCases.CapturePrimary(image,cue);
                if(args[0]=="--stage-reviewed")await StagingCases.Run(plan,workspace);
                else await PostcommitCases.Run(plan,workspace);
                return true;
            },()=>ReviewedPublicationCases.RequireOriginalAsync(image,cue,before.Image,before.Cue));
        }
        catch(Exception error)
        {throw new IOException("Physical boundary test failed; preserve all existing evidence at "+workspace,error);}
        Console.WriteLine("FRESH_WIZARD_BOUNDARY_ORIGINALS_PRESERVED mode="+args[0]+" workspace="+workspace);
    }
}

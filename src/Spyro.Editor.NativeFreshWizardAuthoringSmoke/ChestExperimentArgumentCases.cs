// Offline harness controls only. No original BIN/CUE, native compilation,
// directory creation or runtime state is needed to retire a failed recipe.
internal static class ChestExperimentArgumentCases
{
    internal static async Task Run(Action<bool,string> check)
    {
        foreach(string mode in new[]{"--chest-source","--chest-experiment","--chest-wide-source","--chest-wide-experiment",
            "--chest-r03-source","--chest-r03-experiment"})
            check(ChestExperimentCases.Matches([mode]),"Existing chest mode retains explicit dispatch: "+mode);
        foreach(string mode in new[]{"--chest-r04-source","--chest-r04-experiment","--chest-wide-experiment=r03","--chest-wide-experiment "})
            check(!ChestExperimentCases.Matches([mode]),"No future or approximate recipe selector is implicitly admitted.");
        check(!ChestExperimentCases.Matches([]),"Default invocation does not select a chest experiment.");
        check(ChestExperimentCases.Matches(["--tiled-family-experiment"]),"Tiled family experiment has one explicit opt-in writer selector.");
        foreach(string mode in new[]{"--tiled-family-experiment=1","--tiled-family-experiment ","--tiled-family-r02-experiment"})
            check(!ChestExperimentCases.Matches([mode]),"No approximate or future tiled experiment is admitted.");

        string output=Path.Combine(Path.GetTempPath(),"spyro-retired-chest-r02-must-not-create-"+Guid.NewGuid().ToString("N"));
        check(!Directory.Exists(output)&&!File.Exists(output),"Retired-command destination begins absent without creating a fixture directory.");
        foreach((string bin,string cue) in new[]{("\0","\0"),(output+".bin",output+".cue")})
        {
            InvalidDataException? refusal=null;
            try{await ChestExperimentCases.RunAsync(["--chest-wide-experiment",bin,cue,output]);}
            catch(InvalidDataException error){refusal=error;}
            check(refusal is { } known&&known.Message.Contains("R02",StringComparison.Ordinal)&&
                known.Message.Contains("escaped reward",StringComparison.Ordinal)&&
                known.Message.Contains("--chest-wide-source",StringComparison.Ordinal),
                "Known failed R02 write refuses before invalid/missing original paths with an explicit diagnostic and preserved source-only route.");
            check(!Directory.Exists(output)&&!File.Exists(output)&&!File.Exists(output+".bin")&&!File.Exists(output+".cue"),
                "Retired recipe creates neither output directory nor source placeholders.");
        }
        foreach(string mode in new[]{"--chest-r04-source","--chest-r04-experiment"})
        {
            try{await ChestExperimentCases.RunAsync([mode,"\0","\0",output]);}
            catch(ArgumentException){check(true,"Unknown future recipe refuses before source/path access.");continue;}
            throw new InvalidDataException("Unknown future chest recipe was accepted.");
        }
        check(!Directory.Exists(output)&&!File.Exists(output),"Unknown future recipes do not create the never-existing destination.");
        foreach(string[] malformed in new string[][]{
            ["--tiled-family-experiment"], ["--tiled-family-experiment","\0","\0"],
            ["--tiled-family-experiment","\0","\0",output,"extra"],
            ["--tiled-family-experiment","\0","\0","--output"]})
        {
            try{await ChestExperimentCases.RunAsync(malformed);}
            catch(ArgumentException){check(true,"Malformed tiled writer refuses before source access.");continue;}
            throw new InvalidDataException("Malformed tiled experiment was accepted.");
        }
        check(!Directory.Exists(output)&&!File.Exists(output),"Malformed tiled commands do not create their destination.");
        try{await ChestExperimentCases.RunAsync(["--tiled-family-experiment",output+".bin",output+".cue",Path.GetTempPath()]);}
        catch(InvalidDataException)
        {
            check(!File.Exists(output+".bin")&&!File.Exists(output+".cue"),"Existing tiled destination refuses before opening missing sources.");
            Console.WriteLine("FRESH_CHEST_EXPERIMENT_ARGUMENTS_OK retiredR02=true sourceIo=0 outputWrites=0");
            return;
        }
        throw new InvalidDataException("Tiled writer accepted an existing directory.");
    }
}

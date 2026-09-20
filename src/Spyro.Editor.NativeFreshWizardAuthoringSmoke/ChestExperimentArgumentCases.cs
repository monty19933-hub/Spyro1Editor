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
        Console.WriteLine("FRESH_CHEST_EXPERIMENT_ARGUMENTS_OK retiredR02=true sourceIo=0 outputWrites=0");
    }
}

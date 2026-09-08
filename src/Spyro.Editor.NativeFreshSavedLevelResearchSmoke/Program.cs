using Spyro.Editor.Core.Exporting;
using static Cases;

if (SavedLevelPublicationCases.IsMode(args)) { await SavedLevelPublicationCases.RunAsync(args); return; }
if (args.Length != 0 && (args.Length != 3 || args[0] != "--source" || args.Skip(1).Any(v =>
    string.IsNullOrWhiteSpace(v) || v.StartsWith("--", StringComparison.Ordinal) || v.Contains('\0'))))
    throw new ArgumentException("Usage: NativeFreshSavedLevelResearchSmoke [--source original.bin original.cue], or an explicit reviewed saved-level mode with BIN CUE ABSOLUTE_PROJECT_ROOT PROJECT_ID ABSOLUTE_OUTPUT (six tokens including mode).");
Root = Directory.CreateTempSubdirectory("spyro-saved-level-research-smoke-").FullName;
try
{
    await CandidateCases.Free(); await SourceOwnerCases.Free();
    await SavedLevelPhysicalApiCases.Free(); await SavedLevelPublicationArgumentCases.Free();
    await LeaseOutcomeCases.Free(); await ActualAttemptCases.Free();
    Console.WriteLine($"NATIVE_FRESH_SAVED_LEVEL_RESEARCH_FREE_OK checks={Checks} refusals={Refusals} originalOpened=false nativeImageWritten=false tinyTransactionInvoked=true");
    if (args.Length != 0)
    {
        var owners = new NativeFreshResearchSourceOwners(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
        Exception? primary = null;
        try
        {
            owners.Open(CancellationToken.None);
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(owners.Bin!, CancellationToken.None);
            await CandidateCases.Source(owners.ImagePath, owners.CuePath);
        }
        catch (Exception error) { primary = error; }
        owners.Complete(primary); // Both held/current BIN+CUE, EOF and all acquired closes.
        Console.WriteLine($"NATIVE_FRESH_SAVED_LEVEL_RESEARCH_SOURCE_OK checks={Checks} refusals={Refusals} originalFinal=true nativeImageWritten=false");
    }
}
finally { Console.Error.WriteLine("Retained new test-owned tiny fixtures: " + Root); }

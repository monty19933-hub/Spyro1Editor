using static Cases;

if (args.Length != 0 && (args.Length != 2 || args[0] != "--source" ||
    string.IsNullOrWhiteSpace(args[1]) || args[1].StartsWith("--", StringComparison.Ordinal) || args[1].Contains('\0')))
    throw new ArgumentException("Usage: NativeFreshSavedMaterialSmoke [--source original.bin]; no output modes.");
Root = Directory.CreateTempSubdirectory("spyro-saved-material-smoke-").FullName;
try
{
    await FreeCases.Run();
    Console.WriteLine($"NATIVE_FRESH_SAVED_MATERIAL_FREE_OK checks={Checks} refusals={Refusals} originalOpened=false artifactPublicationInvoked=false");
    if (args.Length != 0)
    {
        await SourceCases.Run(Path.GetFullPath(args[1]));
        Console.WriteLine($"NATIVE_FRESH_SAVED_MATERIAL_SOURCE_OK checks={Checks} refusals={Refusals} originalFinal=true physicalOutputWritten=false");
    }
}
finally
{
    // Tiny fixtures are retained even after success. Never recursively delete
    // a tree that could have acquired an unexpected owner during a test.
    Console.Error.WriteLine("Retained saved-material fixture (success or failure): " + Root);
}

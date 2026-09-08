if (TexturedPublicationCases.IsMode(args))
{
    await TexturedPublicationCases.RunAsync(args);
    return;
}
if (args.Length != 0 && (args.Length != 3 || args[0] != "--source" ||
    args.Skip(1).Any(s => string.IsNullOrWhiteSpace(s) || s.Contains('\0') || s.StartsWith("--", StringComparison.Ordinal))))
    throw new ArgumentException("Use no arguments, exactly --source ORIGINAL.bin ORIGINAL.cue, or an explicit reviewed textured mode.");
LineageCases.Free();
BindingCases.Free();
ExtractionCases.Free();
await CandidateApiCases.Free();
await TexturedPublicationArgumentCases.Free();
await TexturedFaultApiCases.Free();
CandidateSourceFailureCases.Free();
Console.WriteLine($"NATIVE_ID65_TEXTURED_RESEARCH_BASELINE_FREE_OK checks={Inspection.Checks} sourceIo=0 outputWrites=0");
if (args.Length != 0)
{
    await SourceCases.Run(args[1], args[2]);
    Console.WriteLine($"NATIVE_ID65_TEXTURED_RESEARCH_BASELINE_SOURCE_OK checks={Inspection.Checks} originalFinal=true physicalOutputWrites=0 publication=false");
}

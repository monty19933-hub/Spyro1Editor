// No output path, publication, workspace, UI or emulator mode exists here.
bool sourceMode = args.Length == 3 && args[0] == "--source";
if (args.Length != 0 && !sourceMode)
    throw new ArgumentException("Usage: NativeId65FreshDocumentResearchSmoke [--source original-BIN original-CUE]");
await PlanCases.SourceFree();
if (sourceMode)
{
    await PlanCases.Source(args[1], args[2]);
    Console.WriteLine($"NATIVE_ID65_FRESH_DOCUMENT_RESEARCH_SOURCE_OK checks={PlanCases.Checks}");
}
Console.WriteLine($"NATIVE_ID65_FRESH_DOCUMENT_RESEARCH_SMOKE_OK checks={PlanCases.Checks}");

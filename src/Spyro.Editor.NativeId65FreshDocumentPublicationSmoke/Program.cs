// Reviewed output modes are explicit and never entered by default/source/fault
// routes. They require separate approval of the exact saved project and target.
ReviewedPublicationCases.RunArgumentTests();
if (ReviewedPublicationCases.IsReviewedMode(args))
{
    await ReviewedPublicationCases.RunAsync(args);
    return;
}
bool source = args.Length == 3 && args[0] == "--source";
bool faults = args.Length == 4 && args[0] == "--stage-faults";
if (args.Length != 0 && !source && !faults)
    throw new ArgumentException("Usage: NativeId65FreshDocumentPublicationSmoke [--source BIN CUE | --stage-faults BIN CUE absent-workspace]");
await ApiTimingCases.Run();
await PostcommitCases.RunApiTests();
if (source)
{
    await SourceCases.Run(args[1], args[2]);
    await PostcommitCases.RunSourceGuards(args[1], args[2]);
}
if (faults) await StagingCases.Run(args[1], args[2], args[3]);
Console.WriteLine("NATIVE_ID65_FRESH_DOCUMENT_PUBLICATION_SMOKE_OK successfulPublication=false");

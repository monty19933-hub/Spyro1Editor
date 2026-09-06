// Reviewed output modes require separate explicit invocation. Default/source
// never call them; their argument-only checks preserve the existing check counts.
ReviewedPublicationCases.RunArgumentTests();
if (ReviewedPublicationCases.IsReviewedMode(args))
{
    await ReviewedPublicationCases.RunAsync(args);
    return;
}
// Fault mode remains explicit and always forces precommit refusal.
bool sourceMode = args.Length == 3 && args[0] == "--source";
bool stagingMode = args.Length == 4 && args[0] == "--stage-faults";
if (args.Length != 0 && !sourceMode && !stagingMode)
    throw new ArgumentException("Usage: NativeId65FreshEditedDocumentResearchSmoke [--source BIN CUE | --stage-faults BIN CUE absent-workspace | --publish-reviewed BIN CUE absent-output-directory | --verify-reviewed BIN CUE existing-output-directory]");

await EditedCases.SourceFree();
if (sourceMode)
{
    await EditedCases.Source(args[1], args[2]);
    Console.WriteLine($"NATIVE_ID65_FRESH_EDITED_DOCUMENT_RESEARCH_SOURCE_OK checks={EditedCases.Checks}");
}
if (stagingMode)
{
    var issued = await Spyro.Editor.Core.Exporting.NativeId65FreshDocumentCompilation.CaptureAsync(args[1], EditedCases.Recipe());
    // Also bind the supplied actual CUE before making the tiny private fault fixture.
    var originalBinding = await Spyro.Editor.Core.Exporting.NativeId65FreshEditedDocumentResearchPlan.CaptureAsync(args[1], args[2], issued);
    int stagingChecks = await StagingCases.Run(args[1], args[3], issued);
    await originalBinding.RequireFreshAsync();
    Console.WriteLine($"EDITED_DOCUMENT_STAGING_TOTAL checks={EditedCases.Checks + stagingChecks}");
}
Console.WriteLine($"NATIVE_ID65_FRESH_EDITED_DOCUMENT_RESEARCH_SMOKE_OK checks={EditedCases.Checks}");

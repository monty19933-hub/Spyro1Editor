using Spyro.Editor.Core.Exporting;

// Default and --source cannot publish. Both output-related modes require exact
// explicit arguments and are never registered in the ordinary regression gate.
bool sourceMode = args.Length == 3 && args[0] == "--source";
bool stagingMode = args.Length == 4 && args[0] == "--stage-faults";
bool publicationMode = args.Length == 4 && args[0] == "--exercise-publication";
bool verifyMode = args.Length == 4 && args[0] == "--verify";
if (args.Length != 0 && !sourceMode && !stagingMode && !publicationMode && !verifyMode)
    throw new ArgumentException("Usage: NativeId65FreshDiscResearchSmoke [--source BIN CUE | --stage-faults BIN CUE absent-workspace | --exercise-publication BIN CUE absent-workspace | --verify BIN CUE existing-candidate-directory]");

await PublicationApiTimingCases.Run();
int checks = ResearchCases.SourceFree() + await FactoryCases.SourceFree() + await CompilationCases.SourceFree();
if (sourceMode || stagingMode || publicationMode || verifyMode)
{
    var plan = await NativeId65FreshStarterResearchFactory.CaptureAsync(args[1], args[2]);
    if (sourceMode || stagingMode)
    {
        // Retain the pre-extraction recipe only as an independent source/fault
        // comparison control and the builder for deliberately refused variants.
        NativeId65FreshDiscPlan issued = await BuildDisc(args[1], 8960, 16, 19);
        var control = await NativeId65FreshDiscResearchCandidatePlan.CaptureAsync(args[1], args[2], issued);
        checks += FactoryCases.Source(control, plan);
        if (sourceMode)
            checks += await CompilationCases.Source(args[1], args[2], issued, control);
        checks += await ResearchCases.Source(plan, args[1], args[2], issued,
            (x, rows, track) => BuildDisc(args[1], x, rows, track));
        if (stagingMode)
            checks += await StagingCases.Run(args[1], args[3], issued);
    }
    if (publicationMode)
        checks += await SuccessCases.Run(plan, args[3], () =>
            NativeId65FreshStarterResearchFactory.CaptureAsync(args[1], args[2]));
    if (verifyMode)
    {
        var result = await NativeId65FreshDiscResearchCandidatePublisher.VerifyAsync(plan, args[3]);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));
        Console.WriteLine("NATIVE_ID65_FRESH_DISC_RESEARCH_VERIFY_OK");
    }
    if (sourceMode || stagingMode)
        Console.WriteLine($"NATIVE_ID65_FRESH_DISC_RESEARCH_SOURCE_OK checks={checks}");
}
Console.WriteLine($"NATIVE_ID65_FRESH_DISC_RESEARCH_SMOKE_OK checks={checks}");

static async Task<NativeId65FreshDiscPlan> BuildDisc(string original, int x, int rows, int track)
{
    var surface = await NativeId65AuthoredSurfaceBundle.CaptureAsync(original,
        new(8448, 8448, 8704, 1024, 1024, 0x708050, 0x406080));
    var companion = await NativeId65CompanionAssetPlan.CaptureAsync(original);
    var resources = await NativeId65NormalResourceBundle.CaptureAsync(original, surface, companion);
    var lists = await NativeId65ZeroSceneAnimationLists.CaptureAsync(original);
    var chain = NativeId65NormalResourceChain.Compile(resources, lists);
    var scene = await NativeId65FreshSceneBundle.CaptureAsync(original, chain, new(x, 8960, 9216, x == 8960 ? 64 : 128), rows);
    var staging = await NativeId65NormalUploadStaging.CaptureAsync(original, scene);
    var body = await NativeId65NormalBodyPlan.CaptureAsync(original, scene, staging);
    return await NativeId65FreshDiscPlan.CaptureAsync(original, body, track);
}

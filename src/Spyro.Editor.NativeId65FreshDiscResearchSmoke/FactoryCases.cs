using System.Reflection;
using Spyro.Editor.Core.Exporting;

internal static class FactoryCases
{
    internal static async Task<int> SourceFree()
    {
        int checks = 0;
        Type factory = typeof(NativeId65FreshStarterResearchFactory);
        Check(factory.IsAbstract && factory.IsSealed && factory.GetConstructors().Length == 0,
            "fixed starter factory is static and cannot carry caller-issued state");
        var members = factory.GetMembers(BindingFlags.Public | BindingFlags.Static |
            BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Check(members.Length == 1 && members[0] is MethodInfo { Name: "CaptureAsync" },
            "only public operation captures the fixed recipe; no publication or arbitrary profile API");
        var capture = factory.GetMethod("CaptureAsync")!;
        Check(capture.ReturnType == typeof(Task<NativeId65FreshDiscResearchCandidatePlan>) &&
            capture.GetParameters().Select(p => p.ParameterType).SequenceEqual(
                new[] { typeof(string), typeof(string), typeof(CancellationToken) }) &&
            capture.GetParameters()[2].IsOptional,
            "capture accepts only source BIN, CUE and optional cancellation and returns existing issued plan");

        string absent = Path.Combine(Path.GetTempPath(), "spyro-fresh-factory-no-io-" + Guid.NewGuid().ToString("N"));
        string bin = Path.Combine(absent, "never-created.bin"), cue = Path.Combine(absent, "never-created.cue");
        Check(!Directory.Exists(absent) && !File.Exists(absent), "no-I/O fixture starts absent");
        foreach (string? bad in new string?[] { null, "", " \t" })
        {
            await RejectArgument(() => NativeId65FreshStarterResearchFactory.CaptureAsync(bad!, cue),
                bad == null ? typeof(ArgumentNullException) : typeof(ArgumentException), "originalBin");
            await RejectArgument(() => NativeId65FreshStarterResearchFactory.CaptureAsync(bin, bad!),
                bad == null ? typeof(ArgumentNullException) : typeof(ArgumentException), "sourceCue");
        }
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        foreach (bool nullPaths in new[] { false, true })
        {
            bool refused = false;
            try
            {
                await NativeId65FreshStarterResearchFactory.CaptureAsync(
                    nullPaths ? null! : bin, nullPaths ? null! : cue, cancelled.Token);
            }
            catch (OperationCanceledException error) when (error.CancellationToken == cancelled.Token)
            { refused = true; }
            Check(refused, "pre-cancellation wins before path validation or source I/O and retains the supplied token");
        }
        Check(!Directory.Exists(absent) && !File.Exists(bin) && !File.Exists(cue),
            "invalid or cancelled capture creates no workspace or artifacts");
        return checks;

        void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); checks++; }
        async Task RejectArgument(Func<Task<NativeId65FreshDiscResearchCandidatePlan>> action, Type type, string parameter)
        {
            try { await action(); }
            catch (ArgumentException error) when (error.GetType() == type && error.ParamName == parameter)
            { checks++; return; }
            throw new InvalidDataException("Expected exact pre-I/O argument refusal for " + parameter);
        }
    }

    internal static int Source(NativeId65FreshDiscResearchCandidatePlan control,
        NativeId65FreshDiscResearchCandidatePlan captured)
    {
        int checks = 0;
        Check(control.PlanSha256 == captured.PlanSha256, "factory preserves the complete pre-extraction PlanSHA");
        Check(control.SourceImagePath == captured.SourceImagePath && control.SourceCuePath == captured.SourceCuePath &&
            control.SourceCueSha256 == captured.SourceCueSha256, "factory preserves exact source and CUE identities");
        Check(control.CopyCueBytes().SequenceEqual(captured.CopyCueBytes()), "factory preserves every output CUE byte");
        var verification = new NativeId65FreshDiscVerification(ResearchCases.ExpectedHash, 1253);
        Check(control.BuildReceipt(verification).SequenceEqual(captured.BuildReceipt(verification)),
            "factory preserves the complete prior receipt serialization and ownership evidence");
        Check(captured.ExpectedImageSha256 == ResearchCases.ExpectedHash &&
            captured.ExpectedImageSha256 == control.ExpectedImageSha256,
            "factory retains the independent full-image golden, not a new output expectation");
        Check(!captured.RuntimeVerified && !captured.NormalCreateBinApproved && !captured.CompleteNativeDependencyClosureProven,
            "factory extraction grants no runtime, normal export or complete dependency authority");
        return checks;
        void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); checks++; }
    }
}

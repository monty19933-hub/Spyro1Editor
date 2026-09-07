using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

// No receipt, caller digest or factory default can substitute for the actual
// saved project. These modes are never dispatched by source-free/source QA.
internal static class ReviewedPublicationCases
{
    internal static bool IsReviewedMode(string[] args) => args.Length > 0 && args[0] is
        "--publish-reviewed" or "--verify-reviewed" or "--exercise-postcommit";

    internal static void RequireArguments(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length != 6 || !IsReviewedMode(args) ||
            args.Skip(1).Any(value => string.IsNullOrWhiteSpace(value) || value.Contains('\0') || value.StartsWith("--", StringComparison.Ordinal)))
            throw new ArgumentException("Usage: --publish-reviewed|--verify-reviewed|--exercise-postcommit BIN CUE PROJECT_ROOT PROJECT_ID OUTPUT");
    }

    internal static void RunArgumentTests()
    {
        int checks = 0;
        foreach (string mode in new[] { "--publish-reviewed", "--verify-reviewed", "--exercise-postcommit" })
        {
            string[] valid = [mode, "source.bin", "source.cue", "project", "project-id", "output"];
            RequireArguments(valid); checks++;
            for (int length = 1; length < valid.Length; length++) Reject(valid.Take(length).ToArray());
            Reject(valid.Concat(["extra"]).ToArray());
            for (int field = 1; field < valid.Length; field++)
            {
                foreach (string? value in new string?[] { null, "", " \t\r\n", "\0", "bad\0path" })
                {
                    string[] bad = valid.ToArray(); bad[field] = value!; Reject(bad);
                }
                foreach (string option in new[] { "--publish-reviewed", "--verify-reviewed", "--exercise-postcommit",
                    "--source", "--stage-faults", "--unknown" })
                {
                    string[] bad = valid.ToArray(); bad[field] = option; Reject(bad);
                }
            }
        }
        foreach (string mode in new[] { "--publish", "--verify", "--source", "--stage-faults", "--Publish-reviewed", "--verify-reviewed " })
        {
            Check(!IsReviewedMode([mode]), "ordinary/near-match mode cannot dispatch reviewed output"); checks++;
            Reject([mode, "bin", "cue", "project", "id", "output"]);
        }
        Reject([]); Reject(null!);
        Check(!IsReviewedMode([]), "default never dispatches output"); checks++;
        Console.WriteLine($"FRESH_DOCUMENT_REVIEWED_ARGUMENTS_OK checks={checks} sourceOrOutputIo=0");

        void Reject(string[] bad)
        {
            try { RequireArguments(bad); }
            catch (ArgumentException) { checks++; return; }
            throw new InvalidDataException("Reviewed mode accepted malformed arguments.");
        }
    }

    internal static async Task RunAsync(string[] args)
    {
        RequireArguments(args);
        string output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[5]));
        bool publish = args[0] == "--publish-reviewed", postcommit = args[0] == "--exercise-postcommit";
        RequireOutput(output, absent: publish || postcommit);
        var context = await CaptureAsync(args[1], args[2], args[3], args[4]);
        string[] inputBefore = ReadInputs(context);
        if (postcommit)
        {
            await PostcommitCases.RunAsync(context, output);
            Check(ReadInputs(context).SequenceEqual(inputBefore), "postcommit exercise preserves original and actual saved inputs");
            return;
        }
        string[]? artifactBefore = publish ? null : ReadArtifacts(context, output);
        bool committed = false;
        try
        {
            var result = publish
                ? await NativeId65FreshDocumentResearchPublisher.PublishAsync(context, output)
                : await NativeId65FreshDocumentResearchPublisher.VerifyAsync(context, output);
            committed = publish;
            RequireResult(context, result);
            string[] artifactAfter = ReadArtifacts(context, output);
            if (artifactBefore != null) Check(artifactBefore.SequenceEqual(artifactAfter), "Verify preserves all three complete owners");
            // Fresh process uses this same path: actual Store/intent/source reissue,
            // never a historical Snapshot manufactured from receipt metadata.
            var fresh = await CaptureAsync(args[1], args[2], args[3], args[4]);
            Check(fresh.Plan.PlanSha256 == context.Plan.PlanSha256 &&
                fresh.Plan.CopyCanonicalDocument().SequenceEqual(context.Plan.CopyCanonicalDocument()) &&
                fresh.Snapshot.RootPath == context.Snapshot.RootPath && fresh.Snapshot.ProjectId == context.Snapshot.ProjectId &&
                fresh.Snapshot.ManifestSha256 == context.Snapshot.ManifestSha256 &&
                fresh.Snapshot.ContentSha256 == context.Snapshot.ContentSha256,
                "fresh actual saved/source ancestry reproduces the captured version");
            var verified = await NativeId65FreshDocumentResearchPublisher.VerifyAsync(fresh, output);
            Check(verified == result && ReadArtifacts(fresh, output).SequenceEqual(artifactAfter), "fresh recapture reopens exact image/CUE/receipt");
            Check(ReadInputs(context).SequenceEqual(inputBefore), "original BIN/CUE and raw saved manifest/document unchanged");
            Console.WriteLine((publish ? "FRESH_DOCUMENT_PUBLISHED_REVIEWED " : "FRESH_DOCUMENT_VERIFIED_REVIEWED ") +
                JsonSerializer.Serialize(new { result, canonicalDocumentSha256 = context.Plan.CanonicalDocumentSha256,
                    savedRoot = context.Snapshot.RootPath, projectId = context.Snapshot.ProjectId,
                    inputHashes = inputBefore, outputHashes = artifactAfter, independentRecapture = true,
                    originalAndProjectUnchanged = true, actualEof = true, postcommitCancellationExercised = false,
                    runtime = false, normalExport = false }));
        }
        catch (Exception error) when (committed)
        {
            throw new IOException("Research artifacts were already published; preserve " + output, error);
        }
    }

    internal static async Task<NativeId65FreshSavedDocumentExportContext> CaptureAsync(
        string original, string cue, string projectRoot, string projectId)
    {
        var snapshot = NativeId65FreshDocumentStore.Capture(new EditorProjectLayout(projectRoot, projectId));
        if (!snapshot.Exists || snapshot.Intent == null)
            throw new InvalidDataException("An actual existing saved fresh document is required; no starter fallback.");
        var compiled = await NativeId65FreshDocumentCompilation.CaptureAsync(original, snapshot.Intent);
        var plan = await NativeId65FreshDocumentResearchPlan.CaptureAsync(original, cue, compiled);
        return NativeId65FreshSavedDocumentExportContext.Capture(snapshot, plan);
    }

    internal static void RequireOutput(string output, bool absent)
    {
        if (new FileInfo(output).LinkTarget != null || new DirectoryInfo(output).LinkTarget != null ||
            File.Exists(output) || (absent ? Directory.Exists(output) : !Directory.Exists(output)))
            throw new InvalidDataException(absent ? "Reviewed output must be absent and not linked."
                : "Reviewed Verify requires an existing non-linked output directory.");
    }

    internal static string[] ReadInputs(NativeId65FreshSavedDocumentExportContext context)
    {
        var snapshot = context.Snapshot;
        string[] hashes = [HashFile(context.Plan.SourceImagePath, NativeId65FreshDiscSource.ImageByteLength),
            HashFile(context.Plan.SourceCuePath), HashFile(new EditorProjectLayout(snapshot.RootPath, snapshot.ProjectId).ManifestPath),
            HashFile(snapshot.Path)];
        Check(hashes[0] == NativeId65FreshDiscSource.OriginalSha256 && hashes[1] == context.Plan.SourceCueSha256 &&
            hashes[2] == snapshot.ManifestSha256 && hashes[3] == snapshot.ContentSha256, "actual bound source and raw saved identities");
        return hashes;
    }

    internal static string[] ReadArtifacts(NativeId65FreshSavedDocumentExportContext context, string root, bool exactSet = true)
    {
        string[] names = [NativeId65FreshDocumentResearchPlan.ImageName,
            NativeId65FreshDocumentResearchPublisher.CueName, NativeId65FreshDocumentResearchPublisher.ReceiptName];
        if (exactSet)
            Check(Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName).Order(StringComparer.Ordinal)
                .SequenceEqual(names.Order(StringComparer.Ordinal)), "exact three-artifact set without residue");
        foreach (string name in names)
            Check(File.Exists(Path.Combine(root, name)) && new FileInfo(Path.Combine(root, name)).LinkTarget == null,
                "actual regular non-linked artifact");
        var plan = context.Plan;
        byte[] cue = plan.CopyCueBytes();
        byte[] receipt = NativeId65FreshDocumentResearchPublisher.BuildReceiptForSmoke(context,
            new(plan.ExpectedImageSha256, plan.ExpectedChangedRawSectors));
        using (var file = File.OpenRead(Path.Combine(root, names[1])))
            NativeId65FreshResearchPublicationEngine.RequireCompanion(file, cue, CancellationToken.None);
        using (var file = File.OpenRead(Path.Combine(root, names[2])))
            NativeId65FreshResearchPublicationEngine.RequireCompanion(file, receipt, CancellationToken.None);
        string image = HashFile(Path.Combine(root, names[0]), plan.ExpectedImageByteLength);
        Check(image == plan.ExpectedImageSha256, "complete physical image matches source-derived plan, not caller hash");
        return [image, HashFile(Path.Combine(root, names[1]), cue.Length), HashFile(Path.Combine(root, names[2]), receipt.Length)];
    }

    internal static void RequireResult(NativeId65FreshSavedDocumentExportContext context, NativeId65FreshDiscResearchPublication result)
    {
        Check(result.PlanSha256 == context.Plan.PlanSha256 && result.ImageSha256 == context.Plan.ExpectedImageSha256 &&
            result.ChangedRawSectors == context.Plan.ExpectedChangedRawSectors && result.WholeImageReadback && result.ResearchOnly &&
            !result.RuntimeVerified && !result.NormalCreateBinApproved && !result.CompleteNativeDependencyClosureProven,
            "actual verified research result retains issued identities and limits");
        Check(result.RetainedRecoveryDirectory.Length == 0, "normal successful gate requires no retained recovery warning");
    }

    internal static string HashFile(string path, long? exactLength = null)
    {
        Check(new FileInfo(path).LinkTarget == null, "no linked input/output owner");
        using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        long length = input.Length;
        Check(exactLength.HasValue ? length == exactLength.Value : length is >= 1 and <= 65536, "exact or bounded owner length");
        string hash = Convert.ToHexStringLower(SHA256.HashData(input));
        Check(input.Length == length && input.Position == length && input.ReadByte() == -1 &&
            input.Length == length && input.Position == length, "stable complete owner and actual EOF");
        return hash;
    }

    internal static void Check(bool condition, string label)
    { if (!condition) throw new InvalidDataException(label); }
}

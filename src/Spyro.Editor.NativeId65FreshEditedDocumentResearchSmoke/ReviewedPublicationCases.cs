using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

// Only exact opt-in CLI modes reach this code. Default, source and forced-fault
// modes never call it. No existing receipt or caller digest can issue a plan.
internal static class ReviewedPublicationCases
{
    internal static bool IsReviewedMode(string[] args) =>
        args.Length > 0 && args[0] is "--publish-reviewed" or "--verify-reviewed";

    internal static void RequireArguments(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length != 4 || !IsReviewedMode(args) || args.Skip(1).Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Usage: --publish-reviewed BIN CUE ABSENT-output-directory | --verify-reviewed BIN CUE EXISTING-output-directory");
    }

    internal static void RunArgumentTests()
    {
        int checks = 0;
        foreach (string mode in new[] { "--publish-reviewed", "--verify-reviewed" })
        {
            RequireArguments([mode, "source.bin", "source.cue", "output"]);
            checks++;
            foreach (string[] bad in new[]
            {
                new[] { mode }, new[] { mode, "bin" }, new[] { mode, "bin", "cue" },
                new[] { mode, "bin", "cue", "output", "extra" }
            })
                Reject(bad);
            for (int field = 1; field < 4; field++)
                foreach (string? blank in new string?[] { null, "", " \t\r\n" })
                {
                    string[] bad = [mode, "source.bin", "source.cue", "output"];
                    bad[field] = blank!;
                    Reject(bad);
                }
        }
        foreach (string mode in new[] { "--publish", "--verify", "--source", "--stage-faults", "--Publish-reviewed", "--verify-reviewed " })
        {
            Check(!IsReviewedMode([mode]), "Another mode must not gain reviewed publication dispatch.");
            checks++;
            Reject([mode, "source.bin", "source.cue", "output"]);
        }
        Reject([]);
        Reject(null!);
        Check(!IsReviewedMode([]), "Default mode cannot reach publication.");
        checks++;
        Console.WriteLine($"EDITED_DOCUMENT_REVIEWED_ARGUMENTS_OK checks={checks} sourceOrOutputIo=0");

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
        bool publish = args[0] == "--publish-reviewed";
        string image = Path.GetFullPath(args[1]), cue = Path.GetFullPath(args[2]);
        string output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[3]));
        if (new FileInfo(output).LinkTarget != null || new DirectoryInfo(output).LinkTarget != null ||
            File.Exists(output) || (publish ? Directory.Exists(output) : !Directory.Exists(output)))
            throw new InvalidDataException(publish
                ? "Reviewed publication requires an absent new non-linked output directory."
                : "Reviewed verification requires an existing non-linked output directory.");

        string originalBefore = HashFile(image, NativeId65FreshDiscSource.ImageByteLength);
        string cueBefore = HashFile(cue);
        Check(originalBefore == NativeId65FreshDiscSource.OriginalSha256, "Whole original identity before capture.");
        var captured = await Capture(image, cue);
        var plan = captured.Plan;
        byte[] expectedCue = plan.CopyCueBytes();
        // Known expected companions are not readback evidence; actual Publish/Verify
        // below still authenticates every physical sector against the private source.
        byte[] expectedReceipt = plan.BuildReceipt(new(EditedCases.ImageHash, 1253));
        string[]? before = publish ? null : ReadCompleteOwners(output, expectedCue, expectedReceipt);

        bool committed = false;
        try
        {
            var result = publish
                ? await NativeId65FreshEditedDocumentResearchPublisher.PublishAsync(plan, output)
                : await NativeId65FreshEditedDocumentResearchPublisher.VerifyAsync(plan, output);
            committed = publish;
            Check(result.ImageSha256 == EditedCases.ImageHash && result.PlanSha256 == plan.PlanSha256 &&
                result.ChangedRawSectors == 1253 && result.WholeImageReadback && result.ResearchOnly &&
                !result.RuntimeVerified && !result.NormalCreateBinApproved && !result.CompleteNativeDependencyClosureProven,
                "Actual result matches exact independent research profile without native approval.");
            if (result.RetainedRecoveryDirectory.Length != 0)
                throw new IOException("Artifacts are present with retained recovery; preserve " + result.RetainedRecoveryDirectory);
            string[] after = ReadCompleteOwners(output, expectedCue, expectedReceipt);
            if (before != null)
                Check(before.SequenceEqual(after), "Read-only verification preserves all three complete owners.");

            // Fresh actual compilation, not a cached plan or receipt-deserialization path.
            var recaptured = await Capture(image, cue);
            Check(recaptured.Plan.PlanSha256 == plan.PlanSha256 &&
                recaptured.Compiled.CopyCanonicalDocument().SequenceEqual(captured.Compiled.CopyCanonicalDocument()) &&
                recaptured.Compiled.DiscPlan.PlanSha256 == captured.Compiled.DiscPlan.PlanSha256,
                "Independent source recapture retains exact edited document and native ancestry.");
            Check(recaptured.Plan.CopyCueBytes().SequenceEqual(expectedCue) &&
                recaptured.Plan.BuildReceipt(new(EditedCases.ImageHash, 1253)).SequenceEqual(expectedReceipt),
                "Fresh source issuance reproduces all CUE/receipt bytes.");
            var verified = await NativeId65FreshEditedDocumentResearchPublisher.VerifyAsync(recaptured.Plan, output);
            Check(verified == result && ReadCompleteOwners(output, expectedCue, expectedReceipt).SequenceEqual(after),
                "Independent recapture reopens the exact full artifact set without modification.");
            Check(HashFile(image, NativeId65FreshDiscSource.ImageByteLength) == originalBefore && HashFile(cue) == cueBefore,
                "Original BIN and CUE complete SHA/length/position/actual EOF unchanged.");
            Console.WriteLine((publish ? "EDITED_DOCUMENT_RESEARCH_PUBLISHED_REVIEWED " : "EDITED_DOCUMENT_RESEARCH_VERIFIED_REVIEWED ") +
                JsonSerializer.Serialize(new
                {
                    result, sourceImageSha256 = originalBefore, sourceCueSha256 = cueBefore,
                    canonicalDocumentSha256 = captured.Compiled.CanonicalSha256,
                    outputImageSha256 = after[0], outputCueSha256 = after[1], outputReceiptSha256 = after[2],
                    independentCompilationRecapture = true, originalUnchanged = true,
                    postcommitCancellationExercised = false, runtime = false, normalExport = false
                }));
        }
        catch (Exception error) when (committed)
        {
            // Do not disguise a real published candidate as an aborted write if
            // an additional CLI recapture/hash assertion fails afterwards.
            throw new IOException("Edited research artifacts were already published; preserve " + output, error);
        }
    }

    private static async Task<(NativeId65FreshDocumentCompilation Compiled, NativeId65FreshEditedDocumentResearchPlan Plan)>
        Capture(string image, string cue)
    {
        var compiled = await NativeId65FreshDocumentCompilation.CaptureAsync(image, EditedCases.Recipe());
        Check(compiled.CanonicalSha256 == EditedCases.CanonicalHash &&
            EditedCases.Hash(compiled.DiscPlan.CopyBody()) == EditedCases.BodyHash &&
            EditedCases.Hash(compiled.DiscPlan.CopyExecutable()) == EditedCases.ExeHash,
            "Exact sealed edited recipe reaches independently pinned complete native bytes.");
        var plan = await NativeId65FreshEditedDocumentResearchPlan.CaptureAsync(image, cue, compiled);
        Check(plan.ExpectedImageSha256 == EditedCases.ImageHash && plan.CanonicalDocumentSha256 == EditedCases.CanonicalHash,
            "Only actual issued compilation can select the closed edited publication profile.");
        return (compiled, plan);
    }

    private static string[] ReadCompleteOwners(string directory, byte[] cue, byte[] receipt)
    {
        string[] names = [NativeId65FreshEditedDocumentResearchPlan.ImageName,
            NativeId65FreshEditedDocumentResearchPlan.CueName, NativeId65FreshEditedDocumentResearchPlan.ReceiptName];
        Check(Directory.EnumerateFileSystemEntries(directory).Select(Path.GetFileName).Order(StringComparer.Ordinal)
            .SequenceEqual(names.Order(StringComparer.Ordinal)), "Exact three-artifact set, with no hidden residue.");
        foreach (string name in names)
        {
            string path = Path.Combine(directory, name);
            Check(new FileInfo(path).LinkTarget == null && File.Exists(path) && !Directory.Exists(path),
                "Artifact owner is an existing regular non-linked file.");
        }
        string image = HashFile(Path.Combine(directory, names[0]), NativeId65FreshDiscSource.ImageByteLength);
        Check(image == EditedCases.ImageHash, "Independent full physical image golden.");
        using (var input = File.OpenRead(Path.Combine(directory, names[1])))
            NativeId65FreshDiscResearchCandidatePublisher.RequireCompanion(input, cue, CancellationToken.None);
        using (var input = File.OpenRead(Path.Combine(directory, names[2])))
            NativeId65FreshDiscResearchCandidatePublisher.RequireCompanion(input, receipt, CancellationToken.None);
        return [image, HashFile(Path.Combine(directory, names[1]), cue.Length),
            HashFile(Path.Combine(directory, names[2]), receipt.Length)];
    }

    private static string HashFile(string path, long? exactLength = null)
    {
        using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        long length = input.Length;
        Check(exactLength.HasValue ? length == exactLength.Value : length is >= 1 and <= 65536,
            "Full owner has exact image/companion length or bounded source CUE length.");
        string hash = Convert.ToHexStringLower(SHA256.HashData(input));
        Check(input.Length == length && input.Position == length && input.ReadByte() == -1 &&
            input.Length == length && input.Position == length, "Hash includes actual EOF and stable final length/position.");
        return hash;
    }

    private static void Check(bool value, string reason)
    {
        if (!value) throw new InvalidDataException(reason);
    }
}

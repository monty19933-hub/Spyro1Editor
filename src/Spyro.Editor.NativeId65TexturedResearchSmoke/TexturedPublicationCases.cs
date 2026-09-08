using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

// Explicit physical modes only. No output cleanup, overwrite, retry, or
// receipt-deserialized authority. Default never calls RunAsync; source tests
// invoke it only with guaranteed invalid tiny originals, before any capture.
internal static class TexturedPublicationCases
{
    internal const long ImageLength = 661547040;
    internal const string ImageHash = "998a47930c5f20e36cf87d41bfb9ec3cab237d8d42c443ed706bd50c692611a3";
    private const string OriginalHash = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const string OriginalCueHash = "05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766";
    internal static string[] Names => NativeId65FreshTexturedResearchCandidatePlan.OutputNames;
    internal sealed record Request(string Mode, string Image, string Cue, string Output);
    internal static bool IsMode(string[] args) => args.Length > 0 && args[0] is
        "--publish-textured-reviewed" or "--verify-textured-reviewed" or "--stage-textured-reviewed" or
        "--unconfirmed-textured-reviewed" or "--postcommit-textured-reviewed";
    internal static Request Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length != 4 || !IsMode(args) || args.Skip(1).Any(value =>
            string.IsNullOrWhiteSpace(value) || value.Contains('\0') || value.StartsWith("--", StringComparison.Ordinal)) ||
            !Path.IsPathFullyQualified(args[3]))
            throw new ArgumentException("Use exactly --{publish|verify|stage|unconfirmed|postcommit}-textured-reviewed BIN CUE ABSOLUTE_DIRECTORY.");
        return new(args[0], args[1], args[2], args[3]);
    }

    internal static async Task RunAsync(string[] args)
    {
        var request = Parse(args);
        string image = Path.GetFullPath(request.Image), cue = Path.GetFullPath(request.Cue);
        bool verify = request.Mode == "--verify-textured-reviewed";
        string root = RequireLocation(request.Output, image, cue, verify);
        FileStream? original = null, originalCue = null; bool publishReturned = false;
        try
        {
            string report = await FinishAsync(async () =>
            {
                original = OpenOwned(image, ImageLength);
                originalCue = OpenOwned(cue, 88);
                Authenticate(original, ImageLength, OriginalHash); Authenticate(originalCue, 88, OriginalCueHash);
                var plan = await Capture(image, cue);
                object details;
                if (request.Mode is "--publish-textured-reviewed" or "--verify-textured-reviewed")
                {
                    string[]? before = verify ? ReadCompleteOwners(plan, root) : null;
                    var result = verify
                        ? await NativeId65FreshTexturedResearchCandidatePublisher.VerifyAsync(plan, root)
                        : await NativeId65FreshTexturedResearchCandidatePublisher.PublishAsync(plan, root);
                    publishReturned = !verify; // Only this actual public API return acknowledges a commit.
                    RequireResult(plan, root, result);
                    string[] after = ReadCompleteOwners(plan, root);
                    if (before != null) Check(before.SequenceEqual(after), "Read-only verification preserved every artifact.");
                    details = new { result, artifactSha256 = after };
                }
                else details = await TexturedFaultCases.Run(plan, root, request.Mode,
                    () => publishReturned = true); // Test bookkeeping only, called after an actual API return.
                return "FRESH_TEXTURED_REVIEWED_OK " + JsonSerializer.Serialize(new
                {
                    mode = request.Mode, root, details, plan.PlanSha256, plan.ExpectedImageSha256,
                    sourceImageSha256 = OriginalHash, sourceCueSha256 = OriginalCueHash,
                    sourceAndCueFinalChecks = true, sourceHeldAndNamedFinalChecks = true, sourceAndCueClosed = true,
                    actualPublicChainCapture = true, projectSaveBindingClaimed = false,
                    materialDocumentPersistenceBound = false, runtimeVerified = false, normalExport = false
                });
            }, [
                () => { if (original != null) Authenticate(original, ImageLength, OriginalHash); },
                () => { if (originalCue != null) Authenticate(originalCue, 88, OriginalCueHash); },
                () => AuthenticateNamed(image, ImageLength, OriginalHash),
                () => AuthenticateNamed(cue, 88, OriginalCueHash),
                () => original?.Dispose(), () => originalCue?.Dispose()
            ]);
            Console.WriteLine(report); // Only after all held/named final checks and every acquired close attempt.
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FRESH_TEXTURED_REVIEWED_FAILED " + JsonSerializer.Serialize(new
            {
                mode = request.Mode, root, outcome = FailureOutcome(error, publishReturned, !verify),
                success = false, outputsRetained = true, automaticRetry = false,
                errors = Errors(error).Select(e => new { type = e.GetType().FullName, e.Message })
            }));
            throw;
        }
    }

    internal static async Task<string> FinishAsync(Func<Task<string>> action, IReadOnlyList<Action> finals)
    {
        List<Exception> errors = []; string? report = null;
        try { report = await action(); } catch (Exception error) { errors.Add(error); }
        foreach (var final in finals) try { final(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new IOException("Textured reviewed primary/final/close failures; preserve outputs and do not retry.", new AggregateException(errors));
        return report!;
    }
    internal static string FailureOutcome(Exception error, bool publishReturned, bool publish)
    {
        if (Errors(error).Any(e => e is NativeId65FreshResearchPublicationUnconfirmedException)) return "unconfirmed";
        if (publishReturned || Errors(error).Any(e => e is NativeId65FreshTexturedResearchCommittedException)) return "committed-with-failure";
        return publish ? "not-confirmed" : "verification-failed";
    }
    internal static IEnumerable<Exception> Errors(Exception error)
    {
        yield return error;
        if (error is AggregateException aggregate)
        { foreach (var e in aggregate.InnerExceptions) foreach (var inner in Errors(e)) yield return inner; }
        else if (error.InnerException != null) foreach (var inner in Errors(error.InnerException)) yield return inner;
    }

    internal static string RequireLocation(string path, string image, string cue, bool existing)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        Check(Path.IsPathFullyQualified(path), "Explicit absolute output required.");
        PersistencePathSafety.EnsureTreesDoNotOverlap(image, root, "Textured output overlaps source BIN.");
        PersistencePathSafety.EnsureTreesDoNotOverlap(cue, root, "Textured output overlaps source CUE.");
        RequireUnlinked(root);
        Check(Directory.GetParent(root) is { Exists: true }, "Output parent must already exist.");
        if (existing) { Check(Directory.Exists(root) && !File.Exists(root), "Existing verification directory required."); RequireNames(root, Names); }
        else RequireAbsent(root);
        return root;
    }
    internal static void RequireAbsent(string path)
    {
        Check(!File.Exists(path) && !Directory.Exists(path) && new FileInfo(path).LinkTarget == null &&
            new DirectoryInfo(path).LinkTarget == null, "New absent output owner required: " + path);
    }
    internal static void RequireUnlinked(string path)
    {
        for (string? owner = path; owner != null; owner = Directory.GetParent(owner)?.FullName)
            Check(new FileInfo(owner).LinkTarget == null && new DirectoryInfo(owner).LinkTarget == null, "Unlinked output ancestors required.");
    }
    internal static void RequireNames(string root, IEnumerable<string> names)
    {
        RequireUnlinked(root);
        Check(Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName).Order(StringComparer.Ordinal)
            .SequenceEqual(names.Order(StringComparer.Ordinal)), "Exact complete output/evidence owner names.");
    }
    internal static void RequireResult(NativeId65FreshTexturedResearchCandidatePlan plan, string root,
        NativeId65FreshDiscResearchPublication result)
    {
        Check(result.ImageSha256 == ImageHash && result.PlanSha256 == plan.PlanSha256 && result.ChangedRawSectors == 1253 &&
            result.WholeImageReadback && result.ResearchOnly && !result.RuntimeVerified && !result.NormalCreateBinApproved &&
            !result.CompleteNativeDependencyClosureProven && result.OutputDirectory == root &&
            result.ImagePath == Path.Combine(root, Names[0]) && result.ReceiptPath == Path.Combine(root, Names[1]) &&
            result.CuePath == Path.Combine(root, Names[2]) && result.RetainedRecoveryDirectory.Length == 0,
            "Only actual complete fixed-a clean result, never retained recovery or runtime approval.");
    }
    internal static string[] ReadCompleteOwners(NativeId65FreshTexturedResearchCandidatePlan plan, string root)
    {
        RequireNames(root, Names);
        string[] hashes = ReadArtifacts(plan, root);
        RequireNames(root, Names);
        return hashes;
    }
    internal static string[] ReadArtifacts(NativeId65FreshTexturedResearchCandidatePlan plan, string root)
    {
        byte[] cue = plan.CopyCueBytes();
        using (var input = OpenOwned(Path.Combine(root, Names[0]), ImageLength)) Authenticate(input, ImageLength, ImageHash);
        var verification = plan.VerifyImage(Path.Combine(root, Names[0]), CancellationToken.None);
        Check(verification.OutputSha256 == ImageHash && verification.ChangedSectors == 1253, "Real full image readback.");
        byte[] receipt = plan.BuildReceipt(verification);
        RequireBytes(Path.Combine(root, Names[1]), receipt); RequireBytes(Path.Combine(root, Names[2]), cue);
        Check(DiscCueSheet.ReferencesDataImageSnapshot(cue, Path.Combine(root, Names[2]), Path.Combine(root, Names[0])), "Actual same-snapshot CUE relation.");
        return [ImageHash, Hash(receipt), Hash(cue)];
    }
    internal static void RequireBytes(string path, byte[] bytes)
    {
        Check(bytes.Length is > 0 and <= 65536, "Bounded evidence/companion owner.");
        using var input = OpenOwned(path, bytes.Length);
        NativeId65FreshResearchPublicationEngine.RequireCompanion(input, bytes, CancellationToken.None);
        Authenticate(input, bytes.Length, Hash(bytes));
    }
    internal static byte[] ReadMarker(string path)
    {
        var info = new FileInfo(path);
        Check(info.Exists && !Directory.Exists(path) && info.LinkTarget == null && info.Length == 32,
            "Actual retained marker is a regular nonlinked 32-byte file before open.");
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        Check(new FileInfo(path).LinkTarget == null && file.Length == 32, "Actual retained marker is a closed 32-byte owner.");
        byte[] bytes = new byte[32]; file.ReadExactly(bytes);
        Check(file.Position == 32 && file.ReadByte() == -1 && file.Position == 32 && file.Length == 32, "Marker exact EOF.");
        return bytes;
    }
    internal static void WriteTinyNew(string root, string path, byte[] bytes)
    {
        Check(bytes.Length is > 0 and <= 65536, "Only bounded new evidence bytes.");
        PersistencePathSafety.EnsureWritePathWithinRoot(root, path); RequireAbsent(path); RequireUnlinked(path);
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(bytes); file.Flush(true);
    }
    private static async Task<NativeId65FreshTexturedResearchCandidatePlan> Capture(string image, string cue)
    {
        var surface = await NativeId65AuthoredSurfaceBundle.CaptureAsync(image, new(8448,8448,8704,1024,1024,0x708050,0x406080));
        var companion = await NativeId65CompanionAssetPlan.CaptureAsync(image);
        var resources = await NativeId65NormalResourceBundle.CaptureAsync(image, surface, companion);
        var lists = await NativeId65ZeroSceneAnimationLists.CaptureAsync(image);
        var chain = NativeId65NormalResourceChain.Compile(resources, lists);
        var fresh = await NativeId65FreshSceneBundle.CaptureAsync(image, chain, new(8960,8960,9216,64), 16);
        var first = NativeStaticTerrainTextureSource.Capture(image,14,0);
        var second = NativeStaticTerrainTextureSource.Capture(image,70,0);
        var texture = await NativeId65FreshTerrainTexturePlan.CaptureAsync(image, fresh, first, second);
        var stage = await NativeId65NormalUploadStaging.CaptureTexturedAsync(image, texture);
        var body = await NativeId65NormalBodyPlan.CaptureTexturedAsync(image, texture, stage);
        var disc = await NativeId65FreshDiscPlan.CaptureTexturedAsync(image, body, 19);
        var plan = await NativeId65FreshTexturedResearchCandidatePlan.CaptureAsync(image, cue, disc);
        Check(plan.ExpectedImageSha256 == ImageHash && plan.ExpectedImageByteLength == ImageLength &&
            plan.ExpectedChangedRawSectors == 1253, "Actual public fixed-a chain, not caller hashes.");
        Check(Names.SequenceEqual(new[] { "native-id65-fresh-textured-fixed-a-track19-r01.bin",
            "native-id65-fresh-textured-fixed-a-track19-r01.receipt.json", "native-id65-fresh-textured-fixed-a-track19-r01.cue" }),
            "Exact new role names, CUE last.");
        return plan;
    }
    private static FileStream OpenOwned(string path, long length)
    {
        var info = new FileInfo(path);
        Check(info.Exists && !Directory.Exists(path) && info.LinkTarget == null && info.Length == length, "Exact nonlinked readable file.");
        return new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }
    private static void Authenticate(Stream input, long length, string expected)
    {
        Check(input.CanRead && input.CanSeek && input.Length == length, "Complete held source/artifact owner length.");
        input.Position = 0; string hash = Convert.ToHexStringLower(SHA256.HashData(input));
        Check(input.Position == length && input.Length == length && input.ReadByte() == -1 &&
            input.Position == length && input.Length == length && hash == expected, "Complete SHA/length/position/true EOF.");
    }
    private static void AuthenticateNamed(string path, long length, string expected)
    {
        FileStream? current = null; List<Exception> errors = [];
        try { current = OpenOwned(path, length); Authenticate(current, length, expected); }
        catch (Exception error) { errors.Add(error); }
        finally
        {
            try { current?.Dispose(); } catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new IOException("Current named original check/close failures.", new AggregateException(errors));
    }
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static void Check(bool ok, string why) { if (!ok) throw new InvalidDataException(why); }
}

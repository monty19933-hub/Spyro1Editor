using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

// Genuine saved JSON and issued original-source plans only. No candidate image,
// publication engine, forged Snapshot, or selected-row projection is exercised.
internal static class SavedWizardBindingCases
{
    private const string Marker = ".native-fresh-level.writer.lock";
    private static Action<bool, string> _check = null!;
    private static int _checks;
    internal static void Free(Action<bool, string> check)
    {
        _check = check; _checks = 0;
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Throws<OperationCanceledException>(() => NativeFreshSavedWizardResearchContext.Capture(null!, null!, canceled.Token));
        Throws<ArgumentNullException>(() => NativeFreshSavedWizardResearchContext.Capture(null!, null!));
        var type = typeof(NativeFreshSavedWizardResearchContext);
        Check(type.IsSealed && !type.IsPublic && type.GetConstructors().Length == 0 &&
            type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic).All(p => p.SetMethod == null),
            "Pair is internal, privately constructed and has no settable actual owners.");
        Check(type.GetProperties().Length == 0 && type.GetMethods(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static).Length == 0,
            "Pair exposes no public writer, factory, receipt or approval API.");
        Console.WriteLine($"FRESH_SAVED_WIZARD_BINDING_FREE_OK checks={_checks} artifactPublicationInvoked=false");
    }

    internal static async Task Source(string bin, string cue, NativeWizardFamilyAssetPlan family,
        NativeId65FreshDocumentCompilation fixedCompilation, NativeId65FreshWizardResearchResourcePlan fixedResource,
        NativeId65FreshWizardResearchCandidatePlan unsavedCandidate, Action<bool, string> check)
    {
        _check = check; _checks = 0;
        string root = Directory.CreateTempSubdirectory("spyro-saved-wizard-binding-").FullName;
        FileStream? original = null, cueOwner = null;
        string? originalHash = null, cueHash = null;
        long cueLength = 0;
        Exception? primary = null;
        var finalErrors = new List<Exception>();
        try
        {
            original = new FileStream(bin, FileMode.Open, FileAccess.Read, FileShare.Read);
            originalHash = FullHash(original, 661547040);
            cueOwner = new FileStream(cue, FileMode.Open, FileAccess.Read, FileShare.Read);
            cueLength = cueOwner.Length;
            if (cueLength is < 1 or > 65536) throw new InvalidDataException("Bounded original CUE length required.");
            cueHash = FullHash(cueOwner, cueLength);
            Check(originalHash == "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37", "Complete actual original source identity.");
            var project = Project("main");
            var requested = Document();
            var saved = NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project), requested);
            // Preserve a genuine noncanonical raw saved revision, not a canonical-only certificate.
            File.AppendAllText(saved.Path, " \n");
            saved = NativeFreshLevelDocumentStore.Capture(project);
            byte[] documentBytes = File.ReadAllBytes(saved.Path), manifestBytes = File.ReadAllBytes(project.ManifestPath);
            var context = NativeFreshSavedLevelDocumentReadContext.Capture(saved);
            Throws<ArgumentNullException>(() => NativeFreshSavedWizardResearchContext.Capture(context, null!));
            var actual = await Compile(context);
            var pair = NativeFreshSavedWizardResearchContext.Capture(context, actual.Candidate);
            Check(ReferenceEquals(pair.Saved, context) && ReferenceEquals(pair.Candidate, actual.Candidate) &&
                ReferenceEquals(pair.Saved.Snapshot, saved) && ReferenceEquals(pair.Saved.Document, saved.Intent) &&
                ReferenceEquals(actual.Scene.AuthoringDocumentAncestor, saved.Intent) &&
                ReferenceEquals(actual.Compilation.Intent, saved.Intent!.Base) && !ReferenceEquals(requested, saved.Intent),
                "Actual saved Snapshot.Intent, not the applied equal editor object, owns the entire issued chain.");
            Check(!File.Exists(Path.Combine(project.RootPath, Marker)) && File.ReadAllBytes(saved.Path).SequenceEqual(documentBytes),
                "Source compilation and pairing neither hold a long guard nor rewrite saved raw bytes.");
            pair.RequireCandidate(actual.Candidate);
            Throws<ArgumentNullException>(() => pair.RequireCandidate(null!));
            Throws<InvalidDataException>(() => context.RequireDocument(requested), "another actual saved aggregate");
            Throws<InvalidDataException>(() => NativeFreshSavedWizardResearchContext.Capture(context, unsavedCandidate), "another actual saved aggregate");
            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                Throws<OperationCanceledException>(() => NativeFreshSavedWizardResearchContext.Capture(context, actual.Candidate, canceled.Token));
                Throws<OperationCanceledException>(() => pair.AcquireFinalReadGuard(canceled.Token));
            }
            var reissued = await NativeId65FreshWizardResearchCandidatePlan.CaptureSeparatedAsync(bin, cue, actual.Candidate.DiscAncestor, actual.Clearance);
            Check(!ReferenceEquals(reissued, actual.Candidate) && reissued.PlanSha256 == actual.Candidate.PlanSha256, "Genuine equal candidate reissue.");
            Throws<InvalidDataException>(() => pair.RequireCandidate(reissued), "another actual Candidate");
            NativeFreshSavedWizardResearchContext.Capture(context, reissued).RequireCandidate(reissued);

            var recaptured = NativeFreshLevelDocumentStore.Capture(project);
            var context2 = NativeFreshSavedLevelDocumentReadContext.Capture(recaptured);
            Throws<InvalidDataException>(() => NativeFreshSavedWizardResearchContext.Capture(context2, actual.Candidate), "another actual saved aggregate");
            var actual2 = await Compile(context2);
            NativeFreshSavedWizardResearchContext.Capture(context2, actual2.Candidate).RequireCandidate(actual2.Candidate);
            Throws<InvalidDataException>(() => NativeFreshSavedWizardResearchContext.Capture(context, actual2.Candidate), "another actual saved aggregate");
            Check(actual2.Candidate.PlanSha256 == actual.Candidate.PlanSha256 &&
                NativeFreshLevelDocumentCodec.SerializeCanonical(context2.Document).SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(context.Document)),
                "Equal saved bytes/plans retain separate actual snapshot ancestry.");

            var oldScene = await NativeId65FreshWizardResearchScenePlan.CaptureAsync(bin, fixedCompilation, family, fixedResource,
                new(8704, 8960, 8704, 9216, 8960));
            var oldFallback = await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(bin, fixedResource);
            var oldHost = await NativeId65FreshWizardResearchHostPlan.CaptureAsync(bin, oldScene, oldFallback);
            var oldBody = await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(bin, oldHost);
            var oldExe = await NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(bin, oldBody);
            var oldDisc = await NativeId65FreshWizardResearchDiscPlan.CaptureAsync(bin, oldExe);
            var oldCandidate = await NativeId65FreshWizardResearchCandidatePlan.CaptureAsync(bin, cue, oldDisc);
            Throws<InvalidDataException>(() => NativeFreshSavedWizardResearchContext.Capture(context, oldCandidate), "authored aggregate");

            var other = Project("other");
            var otherSaved = NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(other), Document());
            var otherContext = NativeFreshSavedLevelDocumentReadContext.Capture(otherSaved);
            Throws<InvalidDataException>(() => NativeFreshSavedWizardResearchContext.Capture(otherContext, actual.Candidate), "another actual saved aggregate");
            foreach (string path in new[] { saved.Path, project.ManifestPath })
            {
                byte[] prior = File.ReadAllBytes(path); File.AppendAllText(path, " ");
                Throws<InvalidDataException>(() => NativeFreshSavedWizardResearchContext.Capture(context, actual.Candidate));
                Throws<InvalidDataException>(() => pair.AcquireFinalReadGuard());
                Check(!File.Exists(Path.Combine(project.RootPath, Marker)), "Raw-CAS refusal leaves no acquired marker.");
                File.WriteAllBytes(path, prior); // Only our new tiny fixture; restore exact raw version for next independent case.
            }
            string marker = Path.Combine(project.RootPath, Marker);
            File.WriteAllText(marker, "foreign");
            Throws<IOException>(() => pair.AcquireFinalReadGuard());
            Check(File.ReadAllText(marker) == "foreign", "Failed CreateNew never owns or removes a preexisting marker.");
            File.Delete(marker);
            var guard = pair.AcquireFinalReadGuard();
            Check(new FileInfo(marker).Length == 32, "Actual final aggregate marker is acquired only now.");
            Throws<IOException>(() => NativeFreshLevelDocumentStore.Save(saved, saved.Intent!));
            var operationFailure = new IOException("source-only simulated consumer failure");
            Check(ReferenceEquals(Throws<IOException>(() => guard.Complete(operationFailure)), operationFailure), "Primary-only completion keeps exact exception identity.");
            Check(ReferenceEquals(Throws<IOException>(() => guard.Complete(operationFailure)), operationFailure) &&
                ReferenceEquals(Throws<IOException>(() => guard.Complete()), operationFailure), "Same-primary and argument-free completion replay cached outcome.");
            Throws<InvalidOperationException>(() => guard.Complete(new IOException("different primary")));
            Check(!File.Exists(marker), "Primary-only completion releases its actual marker.");
            var guard2 = pair.AcquireFinalReadGuard();
            File.Delete(marker); File.WriteAllText(marker, "replacement");
            var failure2 = new OperationCanceledException("late consumer cancellation");
            var combined = Throws<IOException>(() => guard2.Complete(failure2));
            Check(combined.InnerException is AggregateException errors && ReferenceEquals(errors.InnerExceptions[0], failure2) &&
                errors.InnerExceptions.Skip(1).Any(e => e is InvalidDataException) && guard2.RetainedEvidencePath == marker,
                "Real terminal ownership failure retains ordered primary plus release evidence.");
            Check(ReferenceEquals(Throws<IOException>(() => guard2.Complete(failure2)), combined) && File.ReadAllText(marker) == "replacement",
                "Repeated completion cannot remove or retry a replacement owner.");
            File.Delete(marker);
            NativeFreshLevelDocumentStore.Save(saved, saved.Intent!);
            Check(File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifestBytes), "Guard tests preserve actual project identity.");

            foreach (int count in new[] { 0, 2, 128 })
            {
                var rows = Enumerable.Range(1, count).Select(i => NativeFreshWizardRouteIntent.Create(i.ToString("x32"), 9984, 9984, 8909, 9472, 9984));
                var neutral = NativeFreshLevelDocumentIntent.Create(context.Document.Base, rows);
                Check(neutral.Placements.Count == count, "Neutral collection is valid, not one-Wizard native permission.");
                await ThrowsAsync<InvalidDataException>(() => NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync("must-not-open", neutral,
                    actual.Compilation, family, actual.Resource), "exactly one authored Wizard route");
            }
            using var receipt = JsonDocument.Parse(actual.Candidate.BuildReceipt(new(actual.Candidate.ExpectedImageSha256, actual.Candidate.ExpectedChangedRawSectors)));
            Check(receipt.RootElement.GetProperty("projectSaveBindingClaimed").ValueKind == JsonValueKind.False &&
                !actual.Candidate.RuntimeVerified && !actual.Candidate.NormalCreateBinApproved,
                "Pairing does not change old Candidate receipt/publication/runtime authority.");
        }
        catch (Exception error) { primary = error; }
        finally
        {
            if (original != null)
                Final(() => Check(FullHash(original, 661547040) == (originalHash ?? "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"), "Original complete final hash/EOF."));
            if (cueOwner != null)
                Final(() =>
                {
                    if (cueLength is < 1 or > 65536) throw new InvalidDataException("Acquired CUE has no valid initial bounded extent for final verification.");
                    string finalCueHash = FullHash(cueOwner, cueLength); // Attempt even if the initial hash/EOF check failed.
                    Check(cueHash != null && finalCueHash == cueHash, "CUE complete final hash/EOF requires an established initial identity.");
                });
            if (original != null) Final(original.Dispose);
            if (cueOwner != null) Final(cueOwner.Dispose);
        }
        if (primary == null && finalErrors.Count == 0) Directory.Delete(root, recursive: true); // Only this new test-owned JSON tree.
        else
        {
            Console.Error.WriteLine("Retained saved Wizard binding fixture: " + root);
            if (finalErrors.Count == 0) ExceptionDispatchInfo.Capture(primary!).Throw();
            throw new AggregateException("Saved Wizard source test plus all final checks.", (primary == null ? [] : new[] { primary }).Concat(finalErrors));
        }
        Console.WriteLine($"FRESH_SAVED_WIZARD_BINDING_SOURCE_OK checks={_checks} originalAndCueFinal=true artifactPublicationInvoked=false outputImageWritten=false");

        void Final(Action action) { try { action(); } catch (Exception error) { finalErrors.Add(error); } }
        EditorProjectLayout Project(string name)
        {
            string path = Path.Combine(root, name); Directory.CreateDirectory(path);
            var project = new EditorProjectLayout(path, "saved-wizard");
            File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"saved-wizard\"}");
            return project;
        }
        async Task<(NativeId65FreshWizardResearchCandidatePlan Candidate, NativeId65FreshWizardResearchScenePlan Scene,
            NativeId65FreshDocumentCompilation Compilation, NativeId65FreshWizardResearchResourcePlan Resource, NativeFreshInitialSpawnClearancePlan Clearance)>
            Compile(NativeFreshSavedLevelDocumentReadContext savedContext)
        {
            var document = savedContext.Document;
            var compilation = await NativeId65FreshDocumentCompilation.CaptureAsync(bin, document.Base);
            var resource = await NativeId65FreshWizardResearchResourcePlan.CaptureAuthoredAsync(bin, compilation, family);
            var scene = await NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync(bin, document, compilation, family, resource);
            var clearance = await NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync(bin, document, scene);
            var fallback = await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(bin, resource);
            var host = await NativeId65FreshWizardResearchHostPlan.CaptureAsync(bin, scene, fallback);
            var body = await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(bin, host);
            var exe = await NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(bin, body);
            var disc = await NativeId65FreshWizardResearchDiscPlan.CaptureAsync(bin, exe);
            var candidate = await NativeId65FreshWizardResearchCandidatePlan.CaptureSeparatedAsync(bin, cue, disc, clearance);
            return (candidate, scene, compilation, resource, clearance);
        }
    }
    private static NativeFreshLevelDocumentIntent Document() => NativeFreshLevelDocumentIntent.Create(
        NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 2048, 2048, 8960, 8960, 64, new(80, 128, 112), new(128, 96, 64), 19),
        [NativeFreshWizardRouteIntent.Create("abcdef1234567890abcdef1234567890", 9984, 9984, 8704, 9472, 9984)]);
    private static string FullHash(Stream stream, long length)
    {
        if (!stream.CanRead || !stream.CanSeek || stream.Length != length || length <= 0) throw new InvalidDataException("Source owner shape changed.");
        stream.Position = 0; string hash = Convert.ToHexStringLower(SHA256.HashData(stream));
        if (stream.Length != length || stream.Position != length || stream.ReadByte() != -1 || stream.Position != length)
            throw new InvalidDataException("Source owner EOF/position changed.");
        return hash;
    }
    private static void Check(bool value, string message) { _check(value, message); _checks++; }
    private static T Throws<T>(Action action, string? message = null) where T : Exception
    {
        try { action(); } catch (T error) { Check(message == null || error.Message.Contains(message, StringComparison.Ordinal), "Expected refusal: " + error.Message); return error; }
        throw new InvalidDataException("Expected " + typeof(T).Name);
    }
    private static async Task ThrowsAsync<T>(Func<Task> action, string? message = null) where T : Exception
    {
        try { await action(); } catch (T error) { Check(message == null || error.Message.Contains(message, StringComparison.Ordinal), "Expected refusal: " + error.Message); return; }
        throw new InvalidDataException("Expected " + typeof(T).Name);
    }
}

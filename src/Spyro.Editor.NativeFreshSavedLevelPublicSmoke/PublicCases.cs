using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Primitives;

// NON-friend public boundary smoke. Never constructs private owners or publishes.
if (args.Length != 0 && (args.Length != 3 || args[0] != "--source" ||
    args.Skip(1).Any(v => string.IsNullOrWhiteSpace(v) || v.Contains('\0') || v.StartsWith("--", StringComparison.Ordinal))))
    throw new ArgumentException("Usage: NativeFreshSavedLevelPublicSmoke [--source original.bin original.cue]");
PublicCases.Root = Directory.CreateTempSubdirectory("saved-level-public-smoke-").FullName;
try
{
    SourcePairCases.Free();
    await PublicCases.Free();
    Console.WriteLine($"SAVED_LEVEL_PUBLIC_FREE_OK checks={PublicCases.Checks} refusals={PublicCases.Refusals} originalOpened=false nativeOutputWritten=false");
    if (args.Length != 0)
    {
        var sources = new SourcePair(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
        Exception? primary = null;
        try { sources.Open(); await PublicCases.Source(sources.BinPath, sources.CuePath); }
        catch (Exception error) { primary = error; }
        sources.Complete(primary);
        Console.WriteLine($"SAVED_LEVEL_PUBLIC_SOURCE_OK checks={PublicCases.Checks} refusals={PublicCases.Refusals} originalHeldAndNamedFinal=true nativeOutputWritten=false");
    }
}
finally { Console.Error.WriteLine("Retained new tiny public-boundary fixtures: " + PublicCases.Root); }

internal static class PublicCases
{
    internal static string Root = "";
    internal static int Checks, Refusals;
    internal static async Task Free()
    {
        var type = typeof(NativeFreshSavedLevelResearchCandidatePlan);
        Check(type.IsPublic && type.IsSealed && type.GetConstructors().Length == 0, "One public sealed, privately constructed Candidate.");
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Check(methods.Length == 1 && methods[0].Name == "CaptureSavedAsync" &&
            methods[0].GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] {
                typeof(string), typeof(string), typeof(NativeFreshLevelDocumentSnapshot), typeof(CancellationToken) }), "Exact one public Snapshot factory.");
        Check(type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(p => p.Name).Order().SequenceEqual(new[] { "SavedSnapshot", "SourceImagePath", "SourceCuePath", "SourceCueSha256",
                "PlanSha256", "ExpectedImageSha256", "ExpectedChangedRawSectors", "ExpectedImageByteLength", "RuntimeVerified" }.Order()), "Exact descriptive getter set.");
        Check(type.GetProperties().All(p => p.SetMethod == null), "No public mutations.");
        var publisher = typeof(NativeFreshSavedLevelResearchCandidatePublisher);
        Check(publisher.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name).Order().SequenceEqual(new[] { "PublishAsync", "VerifyAsync" }), "Only normal public publisher methods.");
        Check(typeof(NativeFreshSavedLevelResearchCommittedException).GetConstructors().Length == 0 &&
            typeof(NativeId65FreshResearchPublicationUnconfirmedException).GetConstructors().Length == 0, "No externally manufactured publication classification.");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Task<NativeFreshSavedLevelResearchCandidatePlan> capture;
        try { capture = NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync("\0", "\0", null!, canceled.Token); }
        catch { throw new InvalidDataException("Public async capture validation must fault the Task."); }
        var error = await Throws<OperationCanceledException>(() => capture);
        Check(error.CancellationToken == canceled.Token, "Exact cancellation before null/path/context.");
        await Throws<ArgumentNullException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync("\0", "\0", null!));
        foreach (bool verify in new[] { false, true })
        {
            Task<NativeId65FreshDiscResearchPublication> call;
            try { call = verify ? NativeFreshSavedLevelResearchCandidatePublisher.VerifyAsync(null!, "\0", canceled.Token) :
                NativeFreshSavedLevelResearchCandidatePublisher.PublishAsync(null!, "\0", canceled.Token); }
            catch { throw new InvalidDataException("Normal Publisher validation remains asynchronous."); }
            await Throws<ArgumentNullException>(() => call);
        }

        var absentProject = Project("absent");
        var absent = NativeFreshLevelDocumentStore.Capture(absentProject);
        Check(!absent.Exists && absent.Intent == null, "Genuine absent public Snapshot.");
        await PreIo(absent);
        foreach (var (label, document) in new[]
        {
            ("schema1", NativeFreshLevelDocumentIntent.Create(Document().Base, [])),
            ("both-color", Document().WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(
                NativeFreshFloorMaterialIntent.BaseFloorColor(), NativeFreshFloorMaterialIntent.BaseFloorColor()))),
            ("mixed-color", Document().WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(
                NativeFreshFloorMaterialIntent.BaseFloorColor(), Retail(70, 1)))),
            ("wizard", Document().WithContent(Document().Base, [NativeFreshWizardRouteIntent.Create(
                "11111111111111111111111111111111", 8704, 8960, 8909, 9216, 8960)]))
        }) await PreIo(Save(label, document).Snapshot);

        foreach (string kind in new[] { "raw", "manifest", "malformed" })
        {
            var (project, snapshot) = Save(kind, Document());
            string path = kind == "manifest" ? project.ManifestPath : snapshot.Path;
            if (kind == "malformed") File.WriteAllText(path, "{"); else File.AppendAllText(path, " ");
            byte[] expected = File.ReadAllBytes(path);
            await PreIo(snapshot);
            Check(File.ReadAllBytes(path).SequenceEqual(expected), "Public refusal leaves deliberate tiny mutation untouched.");
        }
        var (_, valid) = Save("valid", Document());
        await Throws<ArgumentException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(" ", "\0", valid));
        string missingBin = Path.Combine(Root, "absent-original.bin"), missingCue = Path.Combine(Root, "absent-original.cue");
        var missing = await Throws<IOException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(missingBin, missingCue, valid));
        var leaves = Leaves(missing);
        Check(leaves.Count == 3 && leaves[0] is InvalidDataException &&
            leaves[1] is FileNotFoundException b && b.FileName == missingBin &&
            leaves[2] is FileNotFoundException c && c.FileName == missingCue, "Real missing source primary and both named final checks; no fabricated success.");
        Check(!File.Exists(missingBin) && !File.Exists(missingCue), "Missing original owners never created.");
    }

    private static async Task PreIo(NativeFreshLevelDocumentSnapshot snapshot)
    {
        var error = await Throws<InvalidDataException>(() =>
            NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync("\0", "\0", snapshot));
        Check(!Leaves(error).Any(e => e is FileNotFoundException or DirectoryNotFoundException or ArgumentException),
            "Actual saved state/profile refusal before NUL path normalization/source IO.");
    }

    internal static async Task Source(string bin, string cue)
    {
        const string expectedImage = "2533ee961f13e43db03b08f221921cb60e35465e662e51bf221e6c096eb75b70";
        var (project, snapshot) = Save("actual-source", Document());
        byte[] raw = File.ReadAllBytes(snapshot.Path), manifest = File.ReadAllBytes(project.ManifestPath);
        Exception? primary = null;
        try
        {
            var plan = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, snapshot);
            Check(ReferenceEquals(plan.SavedSnapshot, snapshot) && ReferenceEquals(plan.SavedSnapshot.Intent, snapshot.Intent), "Retain exact actual public Snapshot/Intent.");
            Check(plan.ExpectedImageSha256 == expectedImage && plan.ExpectedChangedRawSectors == 1253 &&
                plan.ExpectedImageByteLength == 661547040 && !plan.RuntimeVerified, "Independent exact joint native image; runtime still false.");
            Check(plan.SourceImagePath == bin && plan.SourceCuePath == cue &&
                plan.SourceCueSha256 == SourcePair.CueHash, "Actual protected source paths and CUE digest.");
            var recaptured = NativeFreshLevelDocumentStore.Capture(project);
            Check(!ReferenceEquals(recaptured, snapshot) && recaptured.ContentSha256 == snapshot.ContentSha256, "Genuine equal-byte Snapshot is distinct.");
            var second = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, recaptured);
            Check(ReferenceEquals(second.SavedSnapshot, recaptured) && !ReferenceEquals(second.SavedSnapshot, plan.SavedSnapshot) &&
                second.ExpectedImageSha256 == plan.ExpectedImageSha256 && second.PlanSha256 == plan.PlanSha256,
                "Independent valid public issuance retains its own actual ancestor; equal hashes are descriptive.");
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            string output = Path.Combine(Root, "never-native-output");
            var pub = await Throws<OperationCanceledException>(() => NativeFreshSavedLevelResearchCandidatePublisher.PublishAsync(plan, output, canceled.Token));
            var ver = await Throws<OperationCanceledException>(() => NativeFreshSavedLevelResearchCandidatePublisher.VerifyAsync(plan, output, canceled.Token));
            Check(pub.CancellationToken == canceled.Token && ver.CancellationToken == canceled.Token, "Actual plan pre-cancellation preserves token.");
            var missing = await Throws<InvalidDataException>(() => NativeFreshSavedLevelResearchCandidatePublisher.VerifyAsync(plan, output));
            Check(missing.Message == "Saved whole-level LAB complete artifact set is missing.", "Exact existing complete-set refusal, not a source or publication error.");
            Check(!Leaves(missing).Any(e => e is NativeFreshSavedLevelResearchCommittedException or NativeId65FreshResearchPublicationUnconfirmedException), "Read-only Verify never manufactures commit classification.");
            Check(!Directory.Exists(output) && !File.Exists(output), "Public source gate never creates native output.");
        }
        catch (Exception error) { primary = error; }
        List<Exception> failures = []; if (primary != null) failures.Add(primary);
        Attempt(() => Check(File.ReadAllBytes(snapshot.Path).SequenceEqual(raw), "Actual source fixture raw unchanged."), failures);
        Attempt(() => Check(File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest), "Actual source fixture manifest unchanged."), failures);
        Attempt(() => Check(!File.Exists(Path.Combine(project.RootPath, ".native-fresh-level.writer.lock")), "No source-only publication marker."), failures);
        ThrowFailures(failures);
    }

    internal static EditorProjectLayout Project(string label)
    {
        string path = Path.Combine(Root, label + "-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path);
        var project = new EditorProjectLayout(path, "saved-public-smoke");
        File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"saved-public-smoke\",\"unrelated\":true}");
        return project;
    }
    internal static (EditorProjectLayout Project, NativeFreshLevelDocumentSnapshot Snapshot) Save(string label, NativeFreshLevelDocumentIntent document)
    { var project = Project(label); return (project, NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project), document)); }
    internal static NativeFreshRetailTerrainMaterialIntent Retail(int entry, int id) =>
        NativeFreshFloorMaterialIntent.RetailTerrain("spyro1-usa-retail-mode2-2352-v1", entry, id);
    internal static NativeFreshLevelDocumentIntent Document() => NativeFreshLevelDocumentIntent.Create(
        NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 1024, 1024, 8960, 8960, 128,
            new ColorRgba(80, 128, 112), new ColorRgba(128, 96, 64), 20), [])
        .WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(Retail(14, 1), Retail(70, 1)));
    internal static void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); Checks++; }
    internal static async Task<T> Throws<T>(Func<Task> call) where T : Exception
    { try { await call(); } catch (T error) { Refusals++; return error; } throw new InvalidDataException("Expected " + typeof(T).Name); }
    internal static List<Exception> Leaves(Exception error) => error is AggregateException a ? a.InnerExceptions.SelectMany(Leaves).ToList() :
        error is IOException && error.InnerException is AggregateException g ? Leaves(g) : [error];
    internal static void Attempt(Action action, List<Exception> failures) { try { action(); } catch (Exception e) { failures.Add(e); } }
    internal static void ThrowFailures(List<Exception> errors)
    { if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw(); if (errors.Count > 1) throw new IOException("Public source smoke primary/final errors.", new AggregateException(errors)); }
}

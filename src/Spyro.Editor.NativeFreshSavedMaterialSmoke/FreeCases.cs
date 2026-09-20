using System.Reflection;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static Cases;

internal static class FreeCases
{
    internal static async Task Run()
    {
        Type type = typeof(NativeFreshSavedMaterialCompilation);
        Check(type.IsSealed && !type.IsPublic && type.GetConstructors().Length == 0,
            "Bridge is an internal sealed issued capability, not a public constructor.");
        Check(type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .All(p => p.SetMethod == null), "Every bridge property is read-only.");
        Check(type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 0,
            "No public factory takes caller native claims.");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var canceledError = await Throws<OperationCanceledException>(() =>
            NativeFreshSavedMaterialCompilation.CaptureAsync(Cases.Missing, null!, canceled.Token));
        Check(canceledError.CancellationToken == canceled.Token, "Pre-cancel token is retained.");
        Task<NativeFreshSavedMaterialCompilation> nullTask;
        try { nullTask = NativeFreshSavedMaterialCompilation.CaptureAsync(Cases.Missing, null!); }
        catch { throw new InvalidDataException("Async validation must fault its Task."); }
        await Throws<ArgumentNullException>(() => nullTask);
        var (_, valid) = Save("valid");
        await Throws<ArgumentException>(() => NativeFreshSavedMaterialCompilation.CaptureAsync(" ", valid));
        foreach (var (width, depth) in new[] { (2048, 2048), (2064, 2048), (2048, 2064), (3072, 2048), (2048, 3072), (3072, 3072) })
        {
            var floor = NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, width, depth, 8960, 8960, 64,
                new(80, 128, 112), new(128, 96, 64), 19);
            var document = Document().WithContent(floor, []);
            var (project, saved) = Save($"public-floor-{width}-{depth}", document);
            byte[] before = File.ReadAllBytes(saved.Snapshot.Path);
            List<string> phases = [];
            bool supported = width == 2048 && depth == 2048;
            Check(NativeFreshSavedLevelResearchEligibility.IsSupportedMaterialProfile(document) == supported,
                "Neutral larger floors remain saveable without widening the historical material export profile.");
            if (supported)
                await Throws<ArgumentException>(() => NativeFreshSavedMaterialCompilation.CaptureForSmokeAsync("\0", saved, phases.Add));
            else
                await Throws<InvalidDataException>(() => NativeFreshSavedMaterialCompilation.CaptureForSmokeAsync("\0", saved, phases.Add), "2048");
            Check(phases.Count == 0 && File.ReadAllBytes(saved.Snapshot.Path).SequenceEqual(before) &&
                !File.Exists(Path.Combine(project.RootPath, LockName)),
                "Dimension refusal precedes invalid path normalization/source callbacks; saved bytes and lock remain untouched.");
        }

        var color = NativeFreshFloorMaterialIntent.BaseFloorColor();
        var legacy = NativeFreshLevelDocumentIntent.Create(Base(), []);
        var vectors = new[]
        {
            ("schema1", legacy, "schema"),
            ("both-color", legacy.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(color, color)), "retail"),
            ("first-color", legacy.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(color, Retail(70, 1))), "retail"),
            ("second-color", legacy.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(Retail(14, 1), color)), "retail")
        };
        foreach (var (name, document, message) in vectors)
        {
            var (project, saved) = Save(name, document);
            byte[] before = File.ReadAllBytes(saved.Snapshot.Path);
            await Throws<InvalidDataException>(() => NativeFreshSavedMaterialCompilation.CaptureAsync(Cases.Missing, saved), message);
            Check(File.ReadAllBytes(saved.Snapshot.Path).SequenceEqual(before) &&
                !File.Exists(Path.Combine(project.RootPath, LockName)), "Unsupported saved intent unchanged without source I/O or lock.");
        }
        foreach (int count in new[] { 1, 2, 128 })
        {
            var requested = Document().WithContent(Base(), Enumerable.Range(1, count).Select(i =>
                NativeFreshWizardRouteIntent.Create(i.ToString("x32"), 8704, 8960, 8909, 9216, 8960)));
            var (_, saved) = Save("wizard" + count, requested);
            await Throws<InvalidDataException>(() => NativeFreshSavedMaterialCompilation.CaptureAsync(Cases.Missing, saved), "placement");
            Check(saved.Document.Placements.Count == count && saved.Document.Placements.Cast<NativeFreshWizardRouteIntent>()
                .All(p => p.OriginZ == 8909), "No Wizard is silently omitted or modified.");
        }
        foreach (string mutation in new[] { "raw", "manifest", "material", "order" })
        {
            var (project, saved) = Save(mutation);
            if (mutation == "raw") File.AppendAllText(saved.Snapshot.Path, " \n");
            else if (mutation == "manifest") File.AppendAllText(project.ManifestPath, " ");
            else if (mutation == "material") NativeFreshLevelDocumentStore.Save(saved.Snapshot, Document(firstId: 0));
            else if (mutation == "order") NativeFreshLevelDocumentStore.Save(saved.Snapshot, Document(70, 1, 14, 1));
            List<string> phases = [];
            var error = await Throws<Exception>(() => NativeFreshSavedMaterialCompilation.CaptureForSmokeAsync(
                Cases.Missing, saved, phases.Add));
            Check(error is InvalidDataException or IOException &&
                Leaves(error).All(e => e is not (FileNotFoundException or DirectoryNotFoundException)) && phases.Count == 0,
                "Actual stale saved context refuses before source I/O; an aggregate wrapper cannot hide missing-source leaves.");
        }
        await ReadGuardAllowsObservation();
        await PartialSourceAccounting();
    }

    private static async Task ReadGuardAllowsObservation()
    {
        var (project, saved) = Save("read-guard-observation");
        var guard = saved.AcquireFinalReadGuard();
        try
        {
            NativeFreshDocumentStorage.RequireAggregateCurrent(saved.Snapshot, CancellationToken.None);
            Check(new FileInfo(guard.LeasePath) is { Length: 32, LinkTarget: null },
                "A real regular32 read-guard marker permits detached raw-Snapshot observation.");
            var missing = Path.Combine(project.RootPath, "known-absent-source.bin");
            var error = await Throws<IOException>(() => NativeFreshSavedMaterialCompilation.CaptureAsync(missing, saved));
            Check(Leaves(error).Count == 2 && Leaves(error).All(e => e is FileNotFoundException),
                "Read guard does not block bridge source entry; actual initial/current-name source misses stay explicit.");
            await Throws<IOException>(() =>
            { NativeFreshLevelDocumentStore.Save(saved.Snapshot, saved.Document); return Task.CompletedTask; });
            Check(new FileInfo(guard.LeasePath) is { Length: 32, LinkTarget: null },
                "Bridge observation and refused Save leave the held regular32 marker present.");
        }
        finally { guard.Complete(); }
        Check(!File.Exists(guard.LeasePath) && guard.TerminalFailure == null && guard.RetainedEvidencePath == "",
            "Actual Complete checked its exact32 owner token then released it; no external held-byte snapshot is claimed.");
    }

    private static async Task PartialSourceAccounting()
    {
        var (project, saved) = Save("partial-source");
        string tiny = Path.Combine(project.RootPath, "known-tiny-not-original.bin");
        File.WriteAllBytes(tiny, [1, 2, 3, 4]);
        var primary = new IOException("injected source-opened callback; not native/source success");
        var sourceHook = new InvalidOperationException("injected final-source checkpoint");
        var snapshotHook = new InvalidOperationException("injected final-snapshot checkpoint");
        var closeHook = new InvalidOperationException("injected before-close checkpoint");
        List<string> observed = [];
        var error = await Throws<IOException>(() => NativeFreshSavedMaterialCompilation.CaptureForSmokeAsync(tiny, saved, phase =>
        {
            observed.Add(phase);
            if (phase == "source-opened")
            { File.AppendAllText(saved.Snapshot.Path, " "); throw primary; }
            if (phase == "before-final-source") throw sourceHook;
            if (phase == "before-final-snapshot") throw snapshotHook;
            if (phase == "before-close") throw closeHook;
        }));
        var leaves = Leaves(error);
        Check(ReferenceEquals(leaves[0], primary), "Exact primary preserved before all final errors.");
        Check(leaves.IndexOf(sourceHook) > 0 && leaves.IndexOf(snapshotHook) > leaves.IndexOf(sourceHook) &&
            leaves.IndexOf(closeHook) > leaves.IndexOf(snapshotHook), "Every injected final hook was attempted in order.");
        var sourceFailures = leaves.Skip(leaves.IndexOf(sourceHook) + 1)
            .Take(leaves.IndexOf(snapshotHook) - leaves.IndexOf(sourceHook) - 1).ToArray();
        Check(sourceFailures.Length == 2 && sourceFailures.All(e => e is InvalidDataException) &&
            !ReferenceEquals(sourceFailures[0], sourceFailures[1]),
            "Both actual held and current named tiny-source checks run independently, in the final-source interval.");
        Check(
            leaves.Skip(leaves.IndexOf(snapshotHook) + 1).Take(leaves.IndexOf(closeHook) - leaves.IndexOf(snapshotHook) - 1)
                .Any(e => e is InvalidDataException), "Actual wrong-source and raw-Snapshot checks still run after hook failures.");
        Check(observed.SequenceEqual(new[] { "source-opened", "before-final-source", "before-final-snapshot", "before-close" }),
            "Partial owner does not imply a captured compilation, and every final category is attempted.");
        using var reopened = new FileStream(tiny, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Check(reopened.Length == 4 && !File.Exists(Path.Combine(project.RootPath, LockName)),
            "Only the actual tiny file was held; no bridge lock or native output was created.");
        Check(!Directory.EnumerateFiles(project.RootPath, "*.cue").Any() && !Directory.EnumerateDirectories(project.RootPath).Any(),
            "No stage, CUE or candidate publication occurred.");
    }
}

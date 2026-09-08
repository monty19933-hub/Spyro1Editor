using System.Reflection;
using Spyro.Editor.Core.Editing;
using static AggregateReadGuardCases;

internal static class AggregateReadContextCases
{
    internal static void Run()
    {
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Throws<OperationCanceledException>(() => NativeFreshSavedLevelDocumentReadContext.Capture(null!, canceled.Token));
        Throws<ArgumentNullException>(() => NativeFreshSavedLevelDocumentReadContext.Capture(null!));
        Type contextType = typeof(NativeFreshSavedLevelDocumentReadContext);
        Check(contextType.IsSealed && !contextType.IsPublic && contextType.GetConstructors().Length == 0 &&
            contextType.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance).All(p => p.SetMethod == null), "closed internal context has no public authority constructor/setter");
        foreach (int count in new[] { 0, 1, 2, 128 })
        {
            var requested = NativeFreshLevelDocumentIntent.Create(Cases.Base(20),
                Enumerable.Range(1, count).Select(i => Cases.Wizard(i.ToString("x32"), z: 8909)));
            var (project, saved) = Saved("count" + count, requested);
            File.AppendAllText(saved.Path, " \n");
            saved = NativeFreshLevelDocumentStore.Capture(project);
            byte[] bytes = File.ReadAllBytes(saved.Path);
            var context = NativeFreshSavedLevelDocumentReadContext.Capture(saved);
            Check(ReferenceEquals(context.Snapshot, saved) && ReferenceEquals(context.Document, saved.Intent) &&
                !ReferenceEquals(context.Document, requested) && context.Document.Placements.Count == count &&
                context.Document.Placements.Select(p => p.EditorId).SequenceEqual(requested.Placements.Select(p => p.EditorId)) &&
                context.Document.Placements.Cast<NativeFreshWizardRouteIntent>().All(p => p.OriginZ == 8909) &&
                context.Document.Base.MusicTrackId == 20, "all neutral placements/Z/base actual saved ancestry, not native eligibility");
            Check(context.Snapshot.ContentSha256 == Hash(bytes) && File.ReadAllBytes(saved.Path).SequenceEqual(bytes) &&
                !File.Exists(Path.Combine(project.RootPath, LockName)), "noncanonical raw version retained without normalization or persistent lock");
            context.RequireDocument(saved.Intent!);
            Throws<InvalidDataException>(() => context.RequireDocument(requested));
            var again = NativeFreshLevelDocumentStore.Capture(project);
            Throws<InvalidDataException>(() => context.RequireDocument(again.Intent!));
            NativeFreshSavedLevelDocumentReadContext.Capture(again).RequireDocument(again.Intent!);
            var guard = context.AcquireFinalReadGuard(); guard.Complete();
        }
        var absentProject = Project("absent");
        var absent = NativeFreshLevelDocumentStore.Capture(absentProject);
        NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(absentProject), Cases.Base());
        Throws<InvalidDataException>(() => NativeFreshSavedLevelDocumentReadContext.Capture(absent));
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.AcquireReadLease(absent));
        Check(!File.Exists(absent.Path) && !File.Exists(Path.Combine(absentProject.RootPath, LockName)), "legacy presence never supplies absent aggregate authority");

        foreach (string mutation in new[] { "document-whitespace", "manifest-whitespace", "document-missing", "malformed", "oversized", "manifest-id", "manifest-oversized", "root-moved", "document-link", "legacy-ignored" })
        {
            var (project, saved) = Saved(mutation);
            var context = NativeFreshSavedLevelDocumentReadContext.Capture(saved);
            string currentRoot = project.RootPath;
            if (mutation == "document-whitespace") File.AppendAllText(saved.Path, " ");
            else if (mutation == "manifest-whitespace") File.AppendAllText(project.ManifestPath, " ");
            else if (mutation == "document-missing") File.Delete(saved.Path);
            else if (mutation == "malformed") File.WriteAllText(saved.Path, "bad aggregate");
            else if (mutation == "oversized") File.WriteAllBytes(saved.Path, new byte[65537]);
            else if (mutation == "manifest-id") File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"elsewhere\"}");
            else if (mutation == "manifest-oversized") File.WriteAllBytes(project.ManifestPath, new byte[65537]);
            else if (mutation == "root-moved") { currentRoot += "-moved"; Directory.Move(project.RootPath, currentRoot); }
            else if (mutation == "document-link") { File.Move(saved.Path, saved.Path + ".held"); File.CreateSymbolicLink(saved.Path, saved.Path + ".held"); }
            else File.WriteAllText(Path.Combine(project.RootPath, NativeId65FreshDocumentStore.FileName), "unrelated malformed legacy");
            if (mutation == "legacy-ignored")
            { var valid = NativeFreshSavedLevelDocumentReadContext.Capture(saved); var lease = valid.AcquireFinalReadGuard(); lease.Complete(); }
            else
            {
                Throws<InvalidDataException>(() => NativeFreshSavedLevelDocumentReadContext.Capture(saved));
                Throws<InvalidDataException>(() => context.AcquireFinalReadGuard());
            }
            Check(!File.Exists(Path.Combine(currentRoot, LockName)), "stale/invalid current owner leaves no acquired guard: " + mutation);
        }
        var (recoveryProject, prior) = Saved("actual-recovery");
        string stage = "";
        var recovery = NativeFreshLevelDocumentStore.SaveForSmoke(prior, Cases.Document(Cases.Wizard(Cases.Id2)), phase =>
        {
            if (phase == "staged") stage = Directory.GetFiles(recoveryProject.RootPath, ".native-fresh-level-*.tmp").Single();
            if (phase == "published") Directory.CreateDirectory(stage);
        });
        Check(recovery.Exists && recovery.RetainedRecoveryDirectory == recoveryProject.RootPath, "real Store returned retained recovery, not forged snapshot");
        string recoveryMarker = Path.Combine(recoveryProject.RootPath, LockName);
        byte[] recoveryOwner = File.ReadAllBytes(recoveryMarker);
        Check(recoveryOwner.Length == 0 && Directory.Exists(stage), "actual existing Save retained its empty writer marker and unexpected stage owner");
        Throws<InvalidDataException>(() => NativeFreshSavedLevelDocumentReadContext.Capture(recovery));
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.AcquireReadLease(recovery));
        Check(File.ReadAllBytes(recoveryMarker).SequenceEqual(recoveryOwner) && Directory.Exists(stage), "unresolved recovery refusal preserves prior Save evidence without acquiring or deleting it");
    }
}

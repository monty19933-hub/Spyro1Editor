using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

internal static class SavedExportContextCases
{
    private static int _checks;

    internal static void SourceFree()
    {
        Type type = typeof(NativeId65FreshSavedDocumentExportContext);
        Check(type.IsSealed && type.GetConstructors().Length == 0, "context cannot be publicly forged or inherited");
        Check(type.GetProperties().All(p => p.SetMethod == null) && type.GetProperties().Select(p => p.Name).Order()
            .SequenceEqual(new[] { "Plan", "Snapshot" }), "context contains actual issued inputs, not mutable approval/hash metadata");
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Check(methods.Length == 1 && methods[0].Name == "Capture" && methods[0].GetParameters().Select(p => p.ParameterType)
            .SequenceEqual(new[] { typeof(NativeId65FreshDocumentSnapshot), typeof(NativeId65FreshDocumentResearchPlan), typeof(CancellationToken) }),
            "only actual saved snapshot and issued plan may bind; no callback or caller evidence");
        Check(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).All(m => m.IsSpecialName),
            "context has no output path, writer, publication or public lease API");
        Reject<ArgumentNullException>(() => NativeId65FreshSavedDocumentExportContext.Capture(null!, null!));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Reject<OperationCanceledException>(() => NativeId65FreshSavedDocumentExportContext.Capture(null!, null!, canceled.Token));
        Console.WriteLine($"NATIVE_ID65_SAVED_EXPORT_CONTEXT_FREE_OK checks={_checks} publication=false");
    }

    internal static async Task Source(string original, string cue)
    {
        string sourceBefore = HashFile(original), cueBefore = HashFile(cue);
        string temporary = Directory.CreateTempSubdirectory("spyro-fresh-saved-context-").FullName;
        try
        {
            var intent = PlanCases.GoldenIntent(1);
            var compiled = await NativeId65FreshDocumentCompilation.CaptureAsync(original, intent);
            var plan = await NativeId65FreshDocumentResearchPlan.CaptureAsync(original, cue, compiled);
            string originalPlan = plan.PlanSha256;
            byte[] canonical = plan.CopyCanonicalDocument();
            EditorProjectLayout Project(string id)
            {
                string root = Path.Combine(temporary, id); Directory.CreateDirectory(root);
                var project = new EditorProjectLayout(root, id);
                File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"" + id + "\"}");
                return project;
            }
            NativeId65FreshDocumentSnapshot Save(EditorProjectLayout p, NativeId65FreshDocumentIntent value) =>
                NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(p), value);

            var project = Project("saved");
            var absent = NativeId65FreshDocumentStore.Capture(project);
            Reject<InvalidDataException>(() => NativeId65FreshSavedDocumentExportContext.Capture(absent, plan));
            var saved = Save(project, intent);
            byte[] manifest = File.ReadAllBytes(project.ManifestPath), doc = File.ReadAllBytes(saved.Path);
            var context = NativeId65FreshSavedDocumentExportContext.Capture(saved, plan);
            Check(ReferenceEquals(context.Snapshot, saved) && ReferenceEquals(context.Plan, plan), "exact issued reference lineage retained");
            Check(Directory.GetFileSystemEntries(project.RootPath).Length == 2 &&
                File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest) && File.ReadAllBytes(saved.Path).SequenceEqual(doc),
                "context capture is read-only and leaves no lease");
            Reject<ArgumentNullException>(() => NativeId65FreshSavedDocumentExportContext.Capture(saved, null!));
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            Reject<OperationCanceledException>(() => context.AcquireFinalPrecommitGuard(canceled.Token));
            using (var guard = context.AcquireFinalPrecommitGuard())
            {
                Reject<IOException>(() => NativeId65FreshDocumentStore.Save(saved, intent));
                Check(guard.RetainedEvidencePath == "", "final guard owns Store's actual cooperating-writer lease");
            }
            Check(NativeId65FreshDocumentStore.Save(saved, intent).ContentSha256 == saved.ContentSha256, "old Save unchanged after guard release");
            // A byte-identical regular replacement is equivalent, not an inode promise.
            string replacement = Path.Combine(project.RootPath, "replacement");
            File.WriteAllBytes(replacement, doc); File.Move(replacement, saved.Path, overwrite: true);
            using (context.AcquireFinalPrecommitGuard()) Check(true, "same bytes/root/id regular replacement is equivalent");
            var otherProject = Project("other"); var otherSaved = Save(otherProject, intent);
            var otherContext = NativeId65FreshSavedDocumentExportContext.Capture(otherSaved, plan);
            Check(ReferenceEquals(otherContext.Snapshot, otherSaved), "source-neutral Plan can bind an independently saved equivalent intent without forging prior project ancestry");
            var mismatch = Save(Project("mismatch"), PlanCases.Recipe(field: 7));
            Reject<InvalidDataException>(() => NativeId65FreshSavedDocumentExportContext.Capture(mismatch, plan));

            foreach (string change in new[] { "document-whitespace", "document-missing", "manifest-whitespace", "project-id", "root-moved", "document-link", "manifest-link", "foreign-lock" })
            {
                var p = Project(change); var snapshot = Save(p, intent);
                var bound = NativeId65FreshSavedDocumentExportContext.Capture(snapshot, plan);
                string lockPath = Path.Combine(p.RootPath, ".native-id65-fresh-document.writer.lock");
                switch (change)
                {
                    case "document-whitespace": File.AppendAllText(snapshot.Path, " "); break;
                    case "document-missing": File.Delete(snapshot.Path); break;
                    case "manifest-whitespace": File.AppendAllText(p.ManifestPath, " "); break;
                    case "project-id": File.WriteAllText(p.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"other-id\"}"); break;
                    case "root-moved": Directory.Move(p.RootPath, p.RootPath + "-moved"); break;
                    case "document-link": File.Move(snapshot.Path, snapshot.Path + "-held"); File.CreateSymbolicLink(snapshot.Path, snapshot.Path + "-held"); break;
                    case "manifest-link": File.Move(p.ManifestPath, p.ManifestPath + "-held"); File.CreateSymbolicLink(p.ManifestPath, p.ManifestPath + "-held"); break;
                    case "foreign-lock": File.WriteAllText(lockPath, "foreign lease"); break;
                }
                if (change == "foreign-lock")
                {
                    Reject<IOException>(() => bound.AcquireFinalPrecommitGuard());
                    Check(File.ReadAllText(lockPath) == "foreign lease", "final guard preserves foreign lock");
                }
                else
                {
                    Reject<InvalidDataException>(() => bound.AcquireFinalPrecommitGuard());
                    Reject<InvalidDataException>(() => NativeId65FreshSavedDocumentExportContext.Capture(snapshot, plan));
                    Check(!File.Exists(lockPath), "stale final guard does not leak an owned lock: " + change);
                }
            }
            Check(plan.PlanSha256 == originalPlan && plan.CopyCanonicalDocument().SequenceEqual(canonical) &&
                plan.ExpectedImageSha256 == PlanCases.Images[1] && !plan.ProjectSaveBindingClaimed,
                "context does not mutate generic Plan bytes, derived native image or source-neutral claims");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
            Check(HashFile(original) == sourceBefore && HashFile(cue) == cueBefore, "original BIN/CUE exact before/after");
        }
        Console.WriteLine($"NATIVE_ID65_SAVED_EXPORT_CONTEXT_SOURCE_OK checks={_checks} publication=false");
    }

    private static string HashFile(string path) { using var stream = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(stream)); }
    private static void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); _checks++; }
    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { _checks++; return; }
        throw new InvalidDataException("Expected " + typeof(T).Name);
    }
}

using System.Reflection;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;

internal static class PublicationReadLeaseCases
{
    private const string LockName = ".native-id65-fresh-document.writer.lock";
    private static int _checks;

    internal static void Run()
    {
        // Only this newly created private fixture tree is written or removed.
        string temporary = Directory.CreateTempSubdirectory("spyro-fresh-read-lease-").FullName;
        try
        {
            NativeId65FreshDocumentIntent intent = NativeId65FreshDocumentIntent.Create(8448, 8448, 8704,
                1024, 1024, 8960, 8960, 64, new(80, 128, 112), new(128, 96, 64), 19);
            EditorProjectLayout Project(string id)
            {
                string root = Path.Combine(temporary, id);
                Directory.CreateDirectory(root);
                var project = new EditorProjectLayout(root, id);
                File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"" + id + "\"}");
                return project;
            }
            NativeId65FreshDocumentSnapshot Saved(EditorProjectLayout project) =>
                NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(project), intent);

            Check(typeof(NativeId65FreshDocumentStore).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(m => m.Name).Order().SequenceEqual(new[] { "Capture", "Save" }), "public Store API remains unchanged");
            Reject<ArgumentNullException>(() => NativeId65FreshDocumentStore.AcquireReadLease(null!));
            var project = Project("clean");
            var saved = Saved(project);
            string leasePath = Path.Combine(project.RootPath, LockName);
            byte[] doc = File.ReadAllBytes(saved.Path), manifest = File.ReadAllBytes(project.ManifestPath);
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            Reject<OperationCanceledException>(() => NativeId65FreshDocumentStore.AcquireReadLease(saved, canceled.Token));
            Check(!File.Exists(leasePath), "pre-cancellation creates no lease");
            var lease = NativeId65FreshDocumentStore.AcquireReadLease(saved);
            Check(lease.LeasePath == leasePath && lease.RetainedEvidencePath == "" && File.Exists(leasePath), "actual private lease is identified");
            Reject<IOException>(() => NativeId65FreshDocumentStore.AcquireReadLease(saved));
            Reject<IOException>(() => NativeId65FreshDocumentStore.Save(saved, intent));
            Check(File.ReadAllBytes(saved.Path).SequenceEqual(doc) && File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest),
                "reader contention does not mutate document or manifest");
            lease.Dispose();
            lease.Dispose();
            Check(!File.Exists(leasePath) && lease.RetainedEvidencePath == "", "clean release is idempotent");
            Check(NativeId65FreshDocumentStore.Save(saved, intent).ContentSha256 == saved.ContentSha256, "old Save succeeds after release");
            using (var afterAcquire = new CancellationTokenSource())
            {
                var held = NativeId65FreshDocumentStore.AcquireReadLease(saved, afterAcquire.Token);
                afterAcquire.Cancel();
                held.Dispose();
                Check(!File.Exists(leasePath) && held.RetainedEvidencePath == "", "release ignores subsequent cancellation");
            }

            foreach (string change in new[] { "document-whitespace", "document-missing", "manifest-whitespace", "project-id" })
            {
                var p = Project(change); var snapshot = Saved(p);
                switch (change)
                {
                    case "document-whitespace": File.AppendAllText(snapshot.Path, " "); break;
                    case "document-missing": File.Delete(snapshot.Path); break;
                    case "manifest-whitespace": File.AppendAllText(p.ManifestPath, " "); break;
                    case "project-id": File.WriteAllText(p.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"different\"}"); break;
                }
                byte[] currentManifest = File.ReadAllBytes(p.ManifestPath);
                byte[]? currentDoc = File.Exists(snapshot.Path) ? File.ReadAllBytes(snapshot.Path) : null;
                Reject<InvalidDataException>(() => NativeId65FreshDocumentStore.AcquireReadLease(snapshot));
                Check(!File.Exists(Path.Combine(p.RootPath, LockName)) && File.ReadAllBytes(p.ManifestPath).SequenceEqual(currentManifest) &&
                    (currentDoc == null ? !File.Exists(snapshot.Path) : File.ReadAllBytes(snapshot.Path).SequenceEqual(currentDoc)),
                    "stale check under owned lease refuses and cleans only its marker: " + change);
            }
            var foreignProject = Project("foreign-lock"); var foreignSaved = Saved(foreignProject);
            string foreignLock = Path.Combine(foreignProject.RootPath, LockName);
            File.WriteAllText(foreignLock, "foreign owner sentinel");
            Reject<IOException>(() => NativeId65FreshDocumentStore.AcquireReadLease(foreignSaved));
            Check(File.ReadAllText(foreignLock) == "foreign owner sentinel", "foreign preexisting lock is never removed");

            List<byte[]> privateTokens = [];
            foreach (string replacement in new[] { "regular", "oversized", "directory", "live-link", "dangling-link", "missing" })
            {
                var p = Project("replace-" + replacement); var snapshot = Saved(p);
                var held = NativeId65FreshDocumentStore.AcquireReadLease(snapshot);
                string originalLease = Path.Combine(p.RootPath, "owned-moved-lock");
                File.Move(held.LeasePath, originalLease); // All targets belong to this private fixture.
                string sentinel = Path.Combine(p.RootPath, "sentinel");
                File.WriteAllText(sentinel, "untouched target");
                switch (replacement)
                {
                    case "regular": File.WriteAllText(held.LeasePath, new string('x', 32)); break;
                    case "oversized": File.WriteAllBytes(held.LeasePath, new byte[65537]); break;
                    case "directory": Directory.CreateDirectory(held.LeasePath); break;
                    case "live-link": File.CreateSymbolicLink(held.LeasePath, sentinel); break;
                    case "dangling-link": File.CreateSymbolicLink(held.LeasePath, Path.Combine(p.RootPath, "absent-target")); break;
                }
                held.Dispose();
                Check(held.RetainedEvidencePath == (replacement == "missing" ? p.RootPath : held.LeasePath),
                    "unknown replacement/missing owner has bounded release evidence: " + replacement);
                byte[] token = File.ReadAllBytes(originalLease);
                Check(token.Length == 32, "owned token length is exactly bounded before release reads");
                privateTokens.Add(token);
                Check(File.ReadAllText(sentinel) == "untouched target", "release never follows replacement links");
                if (replacement is "regular" or "oversized")
                    Check(new FileInfo(held.LeasePath).Length == (replacement == "regular" ? 32 : 65537), "foreign bytes preserved");
                else if (replacement == "directory") Check(Directory.Exists(held.LeasePath), "foreign directory preserved");
                else if (replacement != "missing") Check(new FileInfo(held.LeasePath).LinkTarget != null, "foreign link preserved");
                // Put the exact old marker back only AFTER release: second Dispose
                // must not delete even formerly owned bytes at a reused path.
                if (replacement == "directory") Directory.Delete(held.LeasePath);
                else if (replacement != "missing") File.Delete(held.LeasePath);
                File.Move(originalLease, held.LeasePath);
                held.Dispose();
                Check(File.ReadAllBytes(held.LeasePath).SequenceEqual(token), "release is final, not a later delete capability");
            }
            Check(privateTokens.Select(Convert.ToHexString).Distinct().Count() == privateTokens.Count, "each private lease has a fresh random owner token");

            foreach (string target in new[] { "root", "manifest", "document", "lease" })
            {
                var p = Project("link-" + target); var snapshot = Saved(p);
                string path = target switch { "root" => p.RootPath, "manifest" => p.ManifestPath, "document" => snapshot.Path, _ => Path.Combine(p.RootPath, LockName) };
                string moved = path + "-held";
                if (target == "root") { Directory.Move(path, moved); Directory.CreateSymbolicLink(path, moved); }
                else
                {
                    if (target != "lease") File.Move(path, moved);
                    File.CreateSymbolicLink(path, moved);
                }
                Reject<InvalidDataException>(() => NativeId65FreshDocumentStore.AcquireReadLease(snapshot));
                Check(new FileInfo(path).LinkTarget != null, "link refusal preserves caller evidence: " + target);
            }
            Console.WriteLine($"NATIVE_ID65_PUBLICATION_READ_LEASE_OK checks={_checks} publication=false");
        }
        finally { Directory.Delete(temporary, recursive: true); }
    }

    private static void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); _checks++; }
    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { _checks++; return; }
        throw new InvalidDataException("Expected " + typeof(T).Name);
    }
}

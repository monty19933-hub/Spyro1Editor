using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;
using static Cases;

// Actual tiny authoring files only; no new Store profile.
internal static class MaterialStorageCases
{
    internal static void Run()
    {
        string root = Directory.CreateTempSubdirectory("spyro-material-storage-").FullName;
        bool passed = false;
        try
        {
            var project = Project(root, "materials");
            byte[] manifest = File.ReadAllBytes(project.ManifestPath);
            var legacy = NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(project), Base(20));
            byte[] legacyBytes = File.ReadAllBytes(legacy.Path);
            var absent = NativeFreshLevelDocumentStore.Capture(project);
            var value = MaterialCodecCases.Value();
            byte[] canonical = Encoding.UTF8.GetBytes(MaterialCodecCases.Golden);
            var saved = NativeFreshLevelDocumentStore.Save(absent, value);
            Check(saved.Path == Path.Combine(project.RootPath, "native-fresh-level.json") &&
                saved.ContentSha256 == Hash(canonical) && File.ReadAllBytes(saved.Path).SequenceEqual(canonical),
                "schema2 uses same filename and actual canonical Store output");
            Check(saved.Intent!.SchemaVersion == 2 && saved.Intent.FloorMaterials != null &&
                !ReferenceEquals(saved.Intent, value), "actual return is independently parsed schema2");
            Check(File.ReadAllBytes(legacy.Path).SequenceEqual(legacyBytes), "schema2 save leaves actual legacy bytes unchanged");
            var context = NativeFreshSavedLevelDocumentReadContext.Capture(saved);
            context.RequireDocument(saved.Intent);
            Check(ReferenceEquals(context.Document, saved.Intent), "neutral read context retains actual schema2 Snapshot only");
            var guard = context.AcquireFinalReadGuard();
            try { Throws<IOException>(() => NativeFreshLevelDocumentStore.Save(saved, value), "same cooperating lease excludes modern save"); }
            finally { guard.Complete(); }

            string transactionLock = Path.Combine(project.RootPath, ".export-publication.lock");
            File.WriteAllText(transactionLock, "foreign fixture lock");
            DateTime stamp = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(saved.Path, stamp);
            var noOp = NativeFreshLevelDocumentStore.SaveForSmoke(saved, value, _ => throw new Exception("No-op must not stage"));
            Check(noOp.ContentSha256 == saved.ContentSha256 && File.GetLastWriteTimeUtc(saved.Path) == stamp &&
                File.ReadAllText(transactionLock) == "foreign fixture lock", "schema2 canonical no-op retains old timing/raw contract");
            File.Delete(transactionLock); // This fixture created this exact tiny owner.
            string replacement = Path.Combine(project.RootPath, "same-bytes.tmp");
            File.WriteAllBytes(replacement, canonical); File.Move(replacement, saved.Path, overwrite: true);
            Check(NativeFreshLevelDocumentStore.Save(saved, value).ContentSha256 == saved.ContentSha256,
                "schema2 same-byte file replacement remains current by raw SHA");

            foreach (string external in new[]
            {
                MaterialCodecCases.Golden + " \n",
                MaterialCodecCases.Golden.Replace("\"sourceWadEntry\":14", "\"sourceWadEntry\":16"),
                MaterialCodecCases.Golden.Replace("\"textureId\":0", "\"textureId\":127"),
                Serialize(value.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(MaterialIntentCases.Color(), MaterialIntentCases.Retail()))),
                Serialize(value.WithContent(value.Base, [Wizard(Id2)]))
            })
            {
                File.WriteAllText(saved.Path, external);
                Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.Save(saved, value), "raw material/order/ID/whitespace conflict");
                Check(File.ReadAllText(saved.Path) == external, "stale save preserves actual external bytes");
                File.WriteAllBytes(saved.Path, canonical);
            }
            File.WriteAllText(saved.Path, MaterialCodecCases.Golden.Replace(MaterialIntentCases.Variant, "unknown"));
            Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.Capture(project), "unknown source variant cannot be adopted");
            File.WriteAllBytes(saved.Path, canonical);
            File.AppendAllText(saved.Path, " ");
            var raw = NativeFreshLevelDocumentStore.Capture(project);
            Check(raw.ContentSha256 != saved.ContentSha256 && NativeFreshLevelDocumentStore.Save(raw, value).ContentSha256 == saved.ContentSha256,
                "explicit save may canonicalize observed noncanonical schema2, not silently discard materials");
            File.AppendAllText(project.ManifestPath, " ");
            Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.Save(saved, value), "raw manifest remains CAS input");
            File.WriteAllBytes(project.ManifestPath, manifest);

            var all = value.WithContent(value.Base, Enumerable.Range(1,128).Select(i => Wizard(i.ToString("x32"))));
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                Throws<OperationCanceledException>(() => NativeFreshLevelDocumentStore.Save(saved, all, cancel.Token), "schema2 precancel");
                Check(File.ReadAllBytes(saved.Path).SequenceEqual(canonical), "precancel preserves exact disk bytes");
            }
            using (var cancel = new CancellationTokenSource())
            {
                Throws<OperationCanceledException>(() => NativeFreshLevelDocumentStore.SaveForSmoke(saved, all,
                    phase => { if (phase == "staged") cancel.Cancel(); }, cancel.Token), "actual schema2 precommit cancel");
                Check(File.ReadAllBytes(saved.Path).SequenceEqual(canonical), "staged cancellation preserves prior bytes");
            }
            using (var late = new CancellationTokenSource())
            {
                saved = NativeFreshLevelDocumentStore.SaveForSmoke(saved, all,
                    phase => { if (phase == "published") late.Cancel(); }, late.Token);
                Check(late.IsCancellationRequested && saved.Intent!.SchemaVersion == 2 &&
                    saved.Intent.Placements.Count == 128 && saved.Intent.Placements.Cast<NativeFreshWizardRouteIntent>().All(p => p.OriginZ == 8909) &&
                    Serialize(NativeFreshLevelDocumentStore.Capture(project).Intent!) == Serialize(all),
                    "actual schema2 committed save verifies all128 IDs/Z/materials despite late cancellation");
            }
            byte[] modern = File.ReadAllBytes(saved.Path);
            NativeId65FreshDocumentStore.Save(legacy, Base(21));
            Check(File.ReadAllBytes(saved.Path).SequenceEqual(modern), "legacy save does not touch schema2 aggregate");
            Check(File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest), "manifest raw bytes untouched");
            var forkProject = Project(root, "fork");
            var source = NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(forkProject), Base());
            var fork = NativeFreshLevelDocumentStore.ForkLegacyFloor(source, NativeFreshLevelDocumentStore.Capture(forkProject));
            Check(fork.Intent!.SchemaVersion == 1 && fork.Intent.FloorMaterials == null && fork.Intent.Placements.Count == 0 &&
                Serialize(fork.Intent) == "{\"kind\":\"spyro-native-fresh-level\",\"schema\":1,\"base\":" + BaseJson + ",\"placements\":[]}",
                "actual explicit legacy fork remains byte-exact schema1 with no materials");

            var failedProject = Project(root, "known-commit");
            var expected = NativeFreshLevelDocumentStore.Capture(failedProject);
            var primary = new IOException("actual post-publish callback fixture, not rollback/Dispose proof");
            var error = Throws<IOException>(() => NativeFreshLevelDocumentStore.SaveForSmoke(expected, value,
                phase => { if (phase == "published") throw primary; }), "actual schema2 known-commit callback failure");
            Check(error.Message.StartsWith("Document save committed but readback/cleanup did not complete.", StringComparison.Ordinal) &&
                ReferenceEquals(error.InnerException, primary) && File.ReadAllBytes(expected.Path).SequenceEqual(canonical) &&
                File.Exists(Path.Combine(failedProject.RootPath, ".native-fresh-level.writer.lock")),
                "unchanged known-commit wrapper retains actual published document and marker");
            passed = true;
        }
        finally
        {
            if (passed) Directory.Delete(root, recursive: true); // Only this fully owned tiny fixture tree.
            else Console.WriteLine("MATERIAL_STORAGE_FAILURE_RETAINED path=" + root);
        }
    }

    private static EditorProjectLayout Project(string root, string id)
    {
        string path = Path.Combine(root, id); Directory.CreateDirectory(path);
        var project = new EditorProjectLayout(path, id);
        File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"" + id + "\",\"unrelated\":\"preserve\"}\n");
        return project;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static T Throws<T>(Action action, string label) where T : Exception
    { try { action(); } catch (T error) { Check(true, label); return error; } throw new InvalidDataException("Expected " + typeof(T).Name + ": " + label); }
}

using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;
using static Cases;

internal static class TiledStorageCases
{
    internal static void Run()
    {
        string root = Directory.CreateTempSubdirectory("spyro-tiled-storage-").FullName;
        bool passed = false;
        try
        {
            var project = new EditorProjectLayout(root, "tiled-storage");
            File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"tiled-storage\",\"unrelated\":\"preserve\"}\n");
            byte[] manifest = File.ReadAllBytes(project.ManifestPath);
            var legacy = NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(project), Base(20));
            byte[] legacyBytes = File.ReadAllBytes(legacy.Path);
            var old = NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project), MobyIntentCases.Value(true));
            byte[] oldBytes = File.ReadAllBytes(old.Path);
            var value = TiledCodecCases.Value();
            var saved = NativeFreshLevelDocumentStore.Save(old, value);
            byte[] expected = Encoding.UTF8.GetBytes(TiledCodecCases.Golden);
            Check(saved.Path == old.Path && File.ReadAllBytes(saved.Path).SequenceEqual(expected) &&
                saved.Intent!.SchemaVersion == 4 && saved.Intent.TiledContent != null && !ReferenceEquals(saved.Intent, value),
                "schema4 uses actual unchanged aggregate Store and independently parsed return, not parallel tile persistence");
            Check(oldBytes.SequenceEqual(Encoding.UTF8.GetBytes(MobyCodecCases.MaterialGolden)) &&
                File.ReadAllBytes(legacy.Path).SequenceEqual(legacyBytes) && File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest),
                "explicit aggregate upgrade preserves legacy floor and manifest owners");
            var context = NativeFreshSavedLevelDocumentReadContext.Capture(saved);
            context.RequireDocument(saved.Intent!);
            Reject(() => context.RequireDocument(Parse(TiledCodecCases.Golden)), "equal schema4 canonical copy is not actual saved document lineage");
            var guard = context.AcquireFinalReadGuard();
            try { Throws<IOException>(() => NativeFreshLevelDocumentStore.Save(saved, value), "actual schema4 final read guard excludes Save"); }
            finally { guard.Complete(); }
            NativeFreshLevelDocumentStore.SaveForSmoke(saved, value, _ => throw new InvalidOperationException("No-op must not stage."));
            string external = TiledCodecCases.Golden + " \n";
            File.WriteAllText(saved.Path, external);
            Reject(() => NativeFreshLevelDocumentStore.Save(saved, value), "raw schema4 whitespace revision conflicts with saved expectation");
            Check(File.ReadAllText(saved.Path) == external, "stale schema4 save preserves exact external bytes");
            File.WriteAllBytes(saved.Path, expected);
            var changed = value.WithTiledContent(value.TiledContent!.WithSettings(8976, 8960, 9216, 65, new(1, 2, 3), 20));
            using (var cancel = new CancellationTokenSource())
            {
                Throws<OperationCanceledException>(() => NativeFreshLevelDocumentStore.SaveForSmoke(saved, changed,
                    phase => { if (phase == "staged") cancel.Cancel(); }, cancel.Token), "schema4 precommit cancellation");
                Check(File.ReadAllBytes(saved.Path).SequenceEqual(expected), "cancelled tile/settings save preserves previous complete aggregate");
            }
            saved = NativeFreshLevelDocumentStore.Save(saved, changed);
            var reopened = NativeFreshLevelDocumentStore.Capture(project);
            Check(reopened.Intent!.SchemaVersion == 4 && reopened.Intent.SpawnX == 8976 && reopened.Intent.Heading == 65 &&
                reopened.Intent.SkyColor == new Spyro.Editor.Core.Primitives.ColorRgba(1, 2, 3) && reopened.Intent.MusicTrackId == 20 &&
                reopened.Intent.TiledContent!.Tiles[0].EditorId == TiledIntentCases.TileId &&
                reopened.Intent.Placements.Select(p => p.EditorId).SequenceEqual(value.Placements.Select(p => p.EditorId)),
                "reopen retains actual tiled settings, stable tile IDs and mixed placement identities");
            Reject(() => context.RequireDocument(reopened.Intent), "old saved context cannot adopt a newer actual tile revision");
            File.WriteAllText(saved.Path, Serialize(changed).Replace("\"z\":9216", "\"z\":9217"));
            Reject(() => NativeFreshLevelDocumentStore.Capture(project), "malformed saved tile/spawn relation is not installed");
            Check(File.ReadAllBytes(legacy.Path).SequenceEqual(legacyBytes) && File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest),
                "all tiled Store operations leave unrelated legacy and manifest owners untouched");
            passed = true;
        }
        finally
        {
            if (passed) Directory.Delete(root, recursive: true); // Only this newly owned source-free fixture.
            else Console.WriteLine("TILED_STORAGE_FAILURE_RETAINED path=" + root);
        }
    }

    private static void Throws<T>(Action action, string label) where T : Exception
    {
        try { action(); } catch (T) { Check(true, label); return; }
        throw new InvalidDataException("Expected " + typeof(T).Name + ": " + label);
    }
}

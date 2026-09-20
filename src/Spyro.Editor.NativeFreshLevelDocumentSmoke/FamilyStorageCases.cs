using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;
using static Cases;

// Schema5 is neutral persisted authorship, not admission to a native publisher.
internal static class FamilyStorageCases
{
    internal static void Run()
    {
        string root = Directory.CreateTempSubdirectory("spyro-family-storage-").FullName;
        bool passed = false;
        try
        {
            foreach (bool tiled in new[] { false, true })
            foreach (bool explicitDestinations in new[] { false, true })
            {
                string id = $"family-{(tiled ? "tiled" : "legacy")}-{(explicitDestinations ? "explicit" : "default")}";
                string directory = Path.Combine(root, id);
                Directory.CreateDirectory(directory);
                var project = new EditorProjectLayout(directory, id);
                File.WriteAllText(project.ManifestPath,
                    "{\"schemaVersion\":1,\"projectId\":\"" + id + "\",\"unrelated\":\"preserve\"}\n");
                byte[] manifest = File.ReadAllBytes(project.ManifestPath);
                var legacy = NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(project), Base(20));
                byte[] legacyBytes = File.ReadAllBytes(legacy.Path);
                var old = NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project), Document(Wizard()));
                string golden = tiled ? FamilyCodecCases.TiledGolden : FamilyCodecCases.Golden;
                if (explicitDestinations)
                    golden = golden.Replace(FamilyCodecCases.FamilyJson, FamilyCodecCases.ExplicitFamilyJson);
                byte[] expected = Encoding.UTF8.GetBytes(golden);
                var value = Parse(golden);
                var family = Family(value);
                var saved = NativeFreshLevelDocumentStore.Save(old, value);
                Check(saved.Path == old.Path && saved.Intent!.SchemaVersion == 5 &&
                    File.ReadAllBytes(saved.Path).SequenceEqual(expected) && !ReferenceEquals(saved.Intent, value),
                    "family save reuses the aggregate file and returns independently parsed complete schema5 authorship");
                Check((Family(saved.Intent!).RewardDestinations != null) == explicitDestinations &&
                    (saved.Intent!.TiledContent != null) == tiled && Serialize(saved.Intent) == golden,
                    "save retains the active content arm and absent versus explicit WORLD destinations");
                var reopened = NativeFreshLevelDocumentStore.Capture(project);
                Check(Serialize(reopened.Intent!) == golden && !ReferenceEquals(reopened.Intent, saved.Intent),
                    "reopen recovers complete family intent without a source BIN or compiler");

                var context = NativeFreshSavedLevelDocumentReadContext.Capture(saved);
                context.RequireDocument(saved.Intent!);
                Check(ReferenceEquals(context.Document, saved.Intent), "existing read context retains actual schema5 snapshot ancestry");
                Reject(() => context.RequireDocument(Parse(golden)), "equal reissued family document is not the actual read owner");
                var guard = context.AcquireFinalReadGuard();
                try
                {
                    Throws<IOException>(() => NativeFreshLevelDocumentStore.Save(saved, value),
                        "actual family final read guard excludes cooperating Save");
                }
                finally { guard.Complete(); }
                DateTime stamp = new(2001, 2, 3, 0, 0, 0, DateTimeKind.Utc);
                File.SetLastWriteTimeUtc(saved.Path, stamp);
                NativeFreshLevelDocumentStore.SaveForSmoke(saved, value, _ => throw new Exception("No-op cannot stage"));
                Check(File.GetLastWriteTimeUtc(saved.Path) == stamp, "schema5 canonical no-op preserves file time");

                var changed = value.WithPlacements([family.WithAnchors(family.KeyX + 1, family.KeyY, family.KeyZ,
                    family.ChestX, family.ChestY, family.ChestZ)]);
                foreach (string external in new[] { golden + " \n", Serialize(changed) })
                {
                    File.WriteAllText(saved.Path, external);
                    Reject(() => NativeFreshLevelDocumentStore.Save(saved, value), "stale schema5 raw revision cannot overwrite external content");
                    Check(File.ReadAllText(saved.Path) == external, "conflict leaves exact external family bytes intact");
                    File.WriteAllBytes(saved.Path, expected);
                }
                string malformed = golden.Replace("\"rewardDestinations\":", "\"nativeApproval\":true,\"rewardDestinations\":");
                File.WriteAllText(saved.Path, malformed);
                Reject(() => NativeFreshLevelDocumentStore.Capture(project), "malformed or authority-injected family file cannot be adopted");
                Check(File.ReadAllText(saved.Path) == malformed, "failed capture neither normalizes nor discards unsupported family fields");
                File.WriteAllBytes(saved.Path, expected);

                using (var cancel = new CancellationTokenSource())
                {
                    Throws<OperationCanceledException>(() => NativeFreshLevelDocumentStore.SaveForSmoke(saved, changed,
                        phase => { if (phase == "staged") cancel.Cancel(); }, cancel.Token), "family precommit cancellation");
                    Check(File.ReadAllBytes(saved.Path).SequenceEqual(expected), "cancelled family save preserves prior complete content");
                }
                using (var late = new CancellationTokenSource())
                {
                    saved = NativeFreshLevelDocumentStore.SaveForSmoke(saved, changed,
                        phase => { if (phase == "published") late.Cancel(); }, late.Token);
                    Check(late.IsCancellationRequested && Serialize(saved.Intent!) == Serialize(changed),
                        "committed family save still verifies complete actual authorship after late cancellation");
                }
                var removed = saved.Intent!.WithPlacements([]);
                saved = NativeFreshLevelDocumentStore.Save(saved, removed);
                Check(saved.Intent!.SchemaVersion == 5 && saved.Intent.Placements.Count == 0 &&
                    NativeFreshLevelDocumentStore.Capture(project).Intent!.SchemaVersion == 5 &&
                    (saved.Intent.TiledContent != null) == tiled,
                    "removing the last family survives Save/Reopen without schema or content-arm downgrade");
                Check(File.ReadAllBytes(legacy.Path).SequenceEqual(legacyBytes) &&
                    File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest),
                    "family save never changes the legacy floor or manifest owners");
            }
            passed = true;
        }
        finally
        {
            if (passed) Directory.Delete(root, recursive: true); // Only this newly owned tiny fixture tree.
            else Console.WriteLine("FAMILY_STORAGE_FAILURE_RETAINED path=" + root);
        }
    }

    private static NativeFreshLockedChestFamilyPlacementIntent Family(NativeFreshLevelDocumentIntent document) =>
        document.Placements.OfType<NativeFreshLockedChestFamilyPlacementIntent>().Single();

    private static T Throws<T>(Action action, string label) where T : Exception
    {
        try { action(); }
        catch (T error) { Check(true, label); return error; }
        throw new InvalidDataException("Expected " + typeof(T).Name + ": " + label);
    }
}

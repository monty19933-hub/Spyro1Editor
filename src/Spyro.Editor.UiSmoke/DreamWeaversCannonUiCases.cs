using System.Buffers.Binary;
using System.Reflection;
using Avalonia;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

/// <summary>The editor side of Dream Weavers' Resize cannon, headless on a fresh scene in a temporary workspace: adding the
/// cannon brings its gunner as one group; a fool added with "Let the Resize cannon shoot it" joins that group but moves,
/// copies and is removed on its own; the export projection links it to the cannon. Composes (no BIN is written).</summary>
internal static class DreamWeaversCannonUiCases
{
    internal static void Run(string image, string catalogRoot)
    {
        image = Path.GetFullPath(image); catalogRoot = Path.GetFullPath(catalogRoot);
        int checks = 0;
        void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); checks++; Console.WriteLine($"CANNON_UI {checks}: {what}"); }
        UniversalSourceLibrary.RequireOriginal(image);
        var catalog = LevelCatalog.Load(catalogRoot);
        var library = new UniversalSourceLibrary(image, catalog);
        string root = Directory.CreateTempSubdirectory("spyro-cannon-ui-").FullName;
        string previous = Environment.CurrentDirectory;
        string? workspace = Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE"), release = Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
        File.WriteAllText(Path.Combine(root, "spyro-level-catalog.json"), "{\"levels\":[]}");
        MainWindow? window = null;
        try
        {
            Environment.CurrentDirectory = root; Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", root); Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
            window = new MainWindow(); Invoke(window, "ApplyFreshScene", SceneDocument.New("Cannon Test")); window.Show(); Pump();
            const string Key = UniversalDreamWeaversCannonRows.SourceKey;
            (UniversalMobyCatalogEntry, UniversalMobyVariant) Entry(int classId, int row, string name)
            {
                var v = new UniversalMobyVariant(classId, Key, row, 0, 0, 0, true, false, true);
                return (new UniversalMobyCatalogEntry(classId, name, "Enemies", "", [v], v, [Key], null), v);
            }
            var mobys = Get<List<Moby>>(window, "_currentMobys");
            var (cannonEntry, cannonVariant) = Entry(0x5B, 26, "Resize cannon");
            var (foolEntry, foolVariant) = Entry(0x82, 20, "Armored Fool");

            Check(Invoke(window, "UniversalCannonGroupForNewFool") == null, "no cannon to join yet");
            Complete((Task)Invoke(window, "AddUniversalMobyAsync", library, cannonEntry, cannonVariant, false, null)!);
            Check(mobys.Count == 2 && mobys.Select(m => m.CrossLevelSourceTrueIndex).Order().SequenceEqual([26, 27]), "the cannon comes with its gunner");
            var cannon = mobys.Single(m => m.CrossLevelSourceTrueIndex == 26); var gunner = mobys.Single(m => m.CrossLevelSourceTrueIndex == 27);
            string group = cannon.UniversalLinkGroupId;
            Check(group.Length > 0 && gunner.UniversalLinkGroupId == group, "one linked group");

            Set(window, "_selectedMoby", gunner);
            Check((string?)Invoke(window, "UniversalCannonGroupForNewFool") == group, "the selected gunner offers its cannon");
            Set(window, "_selectedMoby", cannon);
            Complete((Task)Invoke(window, "AddUniversalMobyIntoGroupAsync", library, foolEntry, foolVariant, false, null, group)!);
            var fool = mobys.Single(m => m.CrossLevelSourceTrueIndex == 20);
            Check(mobys.Count == 3 && fool.UniversalLinkGroupId == group, "the fool joins the cannon's group");
            Complete((Task)Invoke(window, "AddUniversalMobyIntoGroupAsync", library, cannonEntry, cannonVariant, false, null, group)!);
            Check(mobys.Count == 3, "only a fool can join a cannon");

            Set(window, "_selectedMoby", fool);
            Check(((IEnumerable<Moby>)Invoke(window, "GetLinkedMoveMobys", fool)!).Single() == fool, "the fool moves on its own");
            var cannonAt = cannon.Position; var foolAt = fool.Position;
            Invoke(window, "NudgeSelectedMoby", 40f, -30f, 0f);
            Check(cannon.Position == cannonAt && Math.Abs(fool.Position.X - foolAt.X - 40) < 0.5f, "nudging the fool leaves the cannon");
            Check(((IEnumerable<Moby>)Invoke(window, "GetLinkedMoveMobys", cannon)!).Select(m => m.CrossLevelSourceTrueIndex).Order().SequenceEqual([26, 27]),
                "the cannon moves with its gunner, not the fool");

            Set(window, "_selectedMoby", cannon); Invoke(window, "CopySelectedMoby");
            Complete((Task)Invoke(window, "PasteMobyClipboardAtViewportAsync", new Point(300, 300))!);
            var pasted = mobys.Skip(3).ToList();
            Check(pasted.Count == 2 && pasted.Select(m => m.CrossLevelSourceTrueIndex).Order().SequenceEqual([26, 27]) &&
                  pasted.All(m => m.UniversalLinkGroupId == pasted[0].UniversalLinkGroupId && m.UniversalLinkGroupId != group), "a copied cannon is a new pair, without the fool");

            Set(window, "_selectedMoby", fool); Invoke(window, "RemoveSelectedMoby");
            Check(fool.IsRemoved && !cannon.IsRemoved && !gunner.IsRemoved, "removing the fool keeps the cannon");
            Invoke(window, "UndoLastRemovedMoby");
            Check(!fool.IsRemoved, "undo restores the fool");
            Set(window, "_selectedMoby", pasted[0]); Invoke(window, "RemoveSelectedMoby");
            Check(pasted.All(m => m.IsRemoved) && !cannon.IsRemoved && !fool.IsRemoved, "removing a cannon removes its pair only");

            var placements = (List<UniversalMobyPlacement>)Invoke(window, "CurrentUniversalPlacements")!;
            var active = placements.Where(p => !p.Retired).ToList();
            Check(active.Count == 3 && active.All(p => p.LinkGroupId == group), "the export projection keeps the fool in the cannon's group");
            // Off the UI thread, as the editor's own export does.
            var compose = Task.Run(() =>
            {
                var host = UniversalCompositionHost.Capture(image, catalog.FindByKey("townsquare")!);
                return host.DryRun(library, placements, removedRows: Enumerable.Range(0, host.DestinationData.Scene().RowCount).ToArray());
            });
            Complete(compose, 600);
            var (result, error) = compose.Result;
            Check(result != null, "Town Square composes the projection: " + error);
            var data = new UniversalLevelData(result!.LevelData);
            int Row(int sourceRow) => result.NewRowIndices[placements.FindIndex(p => !p.Retired && p.SourceRowIndex == sourceRow)];
            Check(BinaryPrimitives.ReadInt32LittleEndian(data.CaptureRow("townsquare", Row(20)).Properties.AsSpan(0x40)) == Row(26), "the exported fool names its cannon's row");
            Console.WriteLine($"DREAMWEAVERS_CANNON_UI_OK checks={checks}");
        }
        finally
        {
            if (window != null) { Set(window, "_allowCloseWithUnsavedTerrain", true); window.Close(); }
            Pump(); Environment.CurrentDirectory = previous;
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", workspace); Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", release);
            try { Directory.Delete(root, true); } catch (IOException) { }
        }
    }

    private static T Get<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(o)!;
    private static void Set(object o, string n, object? v) => o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(o, v);
    private static object? Invoke(object o, string n, params object?[] a) =>
        o.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Single(m => m.Name == n && m.GetParameters().Length == a.Length).Invoke(o, a);
    private static void Pump() => Dispatcher.UIThread.RunJobs();
    private static void Complete(Task t, int seconds = 60)
    {
        DateTime until = DateTime.UtcNow.AddSeconds(seconds);
        while (!t.IsCompleted) { Pump(); if (DateTime.UtcNow > until) throw new TimeoutException(); Thread.Sleep(5); }
        t.GetAwaiter().GetResult(); Pump();
    }
}

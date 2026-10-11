using System.Text.Json;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

/// <summary>Object edits in a built level, as a user makes them: EDIT_LEVEL names the level, EDIT_OBJECTS a JSON list.
/// Each item is one of
///   {"remove": {"label", "x", "y"}}                         the live object with that label nearest (x, y) (within 40);
///   {"move": {"label", "x", "y"}, "x", "y", "z", "yaw"?}    that object moved (and turned: yaw byte);
///   {"turn": {"label", "x", "y"}, "face": [x, y]}           that object turned to face a point;
///   {"color": {"label", "x", "y"}, "rgb": [r, g, b]}         a dragon statue's colour;
///   {"route": {"label", "x", "y"}, "nodes": [[x, y, z], ...]} an added object's route (Edit Route…), points on the terrain;
///   {"source", "row" | "special" | "class", "x", "y", "z", "yaw"?, "snap"?}   an object added through the Add Object
///                                     browser (special: a special id such as crash-tnt; snap: dropped onto the terrain).
/// FREE_SAVE_ROWS=1 first gives back the save rows of deleted objects (File > Free Deleted Objects' Save Rows).</summary>
internal static partial class NewLevelBuildCases
{
    private static void EditObjects(MainWindow window)
    {
        string level = Environment.GetEnvironmentVariable("EDIT_LEVEL") ?? throw new InvalidOperationException("Set EDIT_LEVEL.");
        string path = Environment.GetEnvironmentVariable("EDIT_OBJECTS") ?? throw new InvalidOperationException("Set EDIT_OBJECTS.");
        Load(window, level);
        if (Environment.GetEnvironmentVariable("FREE_SAVE_ROWS") == "1")
        {
            if (window.FreshSceneForTesting == null) throw new InvalidOperationException($"{level} is not a built level");
            var free = (Task<int>)Invoke(window, "FreeRetiredSaveRowsConfirmedAsync")!;
            Wait(() => free.IsCompleted, "free save rows", 120);
            if (free.IsFaulted) throw free.Exception!;
            Console.WriteLine($"freed {free.Result} save rows; ledger {window.FreshSceneForTesting!.Ledger.Entries.Count}");
        }
        var (browser, entries) = OpenBrowserReporting(window); browser.Close(); Pump(10);
        var lib = Library(window);
        var japanese = (UniversalJapaneseSource?)Invoke(window, "ResolveUniversalJapaneseSource");
        var variants = entries.SelectMany(e => e.Variants.Select(v => (Entry: e, Variant: v))).ToArray();
        Moby Find(JsonElement f)
        {
            string label = f.GetProperty("label").GetString()!;
            float fx = f.GetProperty("x").GetSingle(), fy = f.GetProperty("y").GetSingle();
            var live = Mobys(window).Where(m => !m.IsRemoved).ToList();
            var named = live.Any(m => m.DisplayLabel == label) ? live.Where(m => m.DisplayLabel == label) : live.Where(m => m.DisplayLabel.StartsWith(label, StringComparison.Ordinal));
            var hit = named.OrderBy(m => Math.Abs(m.Position.X - fx) + Math.Abs(m.Position.Y - fy)).FirstOrDefault();
            if (hit == null || Math.Abs(hit.Position.X - fx) + Math.Abs(hit.Position.Y - fy) > 40)
                throw new InvalidOperationException($"no live '{label}' near ({fx},{fy})");
            return hit;
        }
        // Group members the editor adds with their root (a statue's dragon), skipped once each when listed.
        List<(string Source, int Row)> linked = [];
        void Turn(Moby m, int yaw) => Invoke(window, "RotateMobyFromViewport", new MobyRotateRequestedEventArgs(m, yaw & 0xFF));
        foreach (var o in JsonDocument.Parse(File.ReadAllText(path)).RootElement.EnumerateArray())
        {
            if (o.TryGetProperty("remove", out var r))
            {
                var m = Find(r);
                SetField(window, "_selectedMoby", m); Invoke(window, "RemoveSelectedMoby");
                Console.WriteLine($"  removed {m.DisplayLabel} ({m.Position.X:0},{m.Position.Y:0},{m.Position.Z:0})");
                continue;
            }
            if (o.TryGetProperty("color", out var cl))
            {
                // A dragon statue's colour, as the object dialog's "Customize dragon color" sets it.
                var m = Find(cl);
                if (!DragonStatueColor.IsStatue(m)) throw new InvalidOperationException($"{m.DisplayLabel} is not a dragon statue");
                var rgb = o.GetProperty("rgb");
                m.DragonColor = new Spyro.Editor.Core.Primitives.ColorRgba((byte)rgb[0].GetInt32(), (byte)rgb[1].GetInt32(), (byte)rgb[2].GetInt32());
                Console.WriteLine($"  coloured {m.DisplayLabel} ({m.Position.X:0},{m.Position.Y:0}) {m.DragonColor}");
                continue;
            }
            if (o.TryGetProperty("route", out var rt))
            {
                // An added object's route, as Edit Route… sets it: each point moved (and dropped onto the terrain).
                var m = Find(rt);
                SetField(window, "_selectedMoby", m);
                Invoke(window, "RefreshPlacedObjectRoutes");
                var route = (Spyro.Editor.Core.Scene.NativeMobyPath?)Invoke(window, "SelectedNativeMobyPath")
                    ?? throw new InvalidOperationException($"{m.DisplayLabel} has no route");
                var nodes = o.GetProperty("nodes").EnumerateArray().Select(n => (X: n[0].GetSingle(), Y: n[1].GetSingle(), Z: n[2].GetSingle())).ToList();
                if (nodes.Count != route.Nodes.Count) throw new InvalidOperationException($"{m.DisplayLabel}'s route has {route.Nodes.Count} points, the list {nodes.Count}");
                for (int i = 0; i < nodes.Count; i++)
                {
                    var (nx, ny, nz) = nodes[i];
                    object?[] args = [nx, ny, nz, 0f, -1, true];
                    if ((bool)typeof(MainWindow).GetMethod("TryFindTerrainZAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, args)!)
                        nz = (float)args[3]!;
                    route.Nodes[i].SetPosition(new Spyro.Editor.Core.Primitives.Vector3f(nx, ny, nz));
                }
                Invoke(window, "StorePlacedObjectRoute", route);
                Console.WriteLine($"  route of {m.DisplayLabel}: {route.Nodes.Count} points, saved {m.UniversalPathNodes?.Count}");
                continue;
            }
            if (o.TryGetProperty("turn", out var t))
            {
                var m = Find(t);
                var face = o.GetProperty("face");
                double angle = Math.Atan2(face[1].GetSingle() - m.Position.Y, face[0].GetSingle() - m.Position.X);
                int yaw = (int)Math.Round(angle / (2 * Math.PI) * 256) & 0xFF;
                Turn(m, yaw);
                Console.WriteLine($"  turned {m.DisplayLabel} to yaw {m.YawByte}");
                continue;
            }
            float x = o.GetProperty("x").GetSingle(), y = o.GetProperty("y").GetSingle(), z = o.GetProperty("z").GetSingle();
            if (o.TryGetProperty("move", out var mv))
            {
                var m = Find(mv);
                SetField(window, "_selectedMoby", m);
                Invoke(window, "NudgeSelectedMoby", x - m.Position.X, y - m.Position.Y, z - m.Position.Z);
                if (o.TryGetProperty("yaw", out var my)) Turn(m, my.GetInt32());
                Console.WriteLine($"  moved {m.DisplayLabel} to ({m.Position.X:0},{m.Position.Y:0},{m.Position.Z:0}) yaw {m.YawByte}");
                continue;
            }
            (UniversalMobyCatalogEntry Entry, UniversalMobyVariant Variant) pick;
            if (o.TryGetProperty("special", out var sp))
            {
                string id = sp.GetString()!;
                pick = variants.FirstOrDefault(p => UniversalSpecialCatalog.SpecialIdForClass(p.Variant.ClassId) == id);
            }
            else if (o.TryGetProperty("class", out var cls))
            {
                int c = cls.GetInt32();
                pick = variants.Where(p => p.Variant.ClassId == c && p.Entry.ClassId == c).OrderBy(p => p.Variant.SourceLevelKey == level ? 0 : 1).FirstOrDefault();
            }
            else
            {
                string source = o.GetProperty("source").GetString()!; int row = o.GetProperty("row").GetInt32();
                if (linked.Remove((source, row))) { Console.WriteLine($"  {source}:{row} came with its group"); continue; }
                pick = variants.FirstOrDefault(p => p.Variant.SourceLevelKey == source && p.Variant.SourceRow == row);
            }
            if (pick.Entry == null) throw new InvalidOperationException($"not in the Add Object browser: {o}");
            var before = Mobys(window).ToHashSet();
            SetField(window, "_selectedMoby", null);
            var add = (Task)Invoke(window, "AddUniversalMobyAsync", lib, pick.Entry, pick.Variant, false, japanese)!;
            Wait(() => add.IsCompleted, $"add {o}", 120);
            if (add.IsFaulted) throw add.Exception!;
            var added = Mobys(window).Where(m => !before.Contains(m)).ToList();
            if (added.Count == 0) throw new InvalidOperationException($"not added: {o}: {Status(window)}");
            Moby moby = added[0];
            linked.AddRange(added.Skip(1).Select(m => (m.CrossLevelSourceLevelKey, m.CrossLevelSourceTrueIndex)));
            SetField(window, "_selectedMoby", moby);
            Invoke(window, "NudgeSelectedMoby", x - moby.Position.X, y - moby.Position.Y, z - moby.Position.Z);
            if (o.TryGetProperty("yaw", out var ay)) Turn(moby, ay.GetInt32());
            if (o.TryGetProperty("snap", out var snap) && snap.GetBoolean() && !(bool)Invoke(window, "SnapSelectedMobyToCurrentTerrain", moby, 0)!)
                throw new InvalidOperationException($"no terrain under {o}");
            foreach (var m in added)
                Console.WriteLine($"  added {m.DisplayLabel,-34} class 0x{pick.Variant.ClassId:X3} ({m.Position.X:0},{m.Position.Y:0},{m.Position.Z:0}) yaw {m.YawByte}");
        }
        Console.WriteLine($"objects {Mobys(window).Count(m => m.IsAdded && !m.IsRemoved)}, treasure {Mobys(window).Where(m => m.IsAdded && !m.IsRemoved).Sum(m => m.TreasureValue)}");
        Save(window);
        if (window.FreshSceneForTesting is { } scene)
            Console.WriteLine($"ledger {scene.Ledger.Entries.Count} + prefix {scene.TargetBinding?.NativePrefixRows} = {scene.Ledger.Entries.Count + (scene.TargetBinding?.NativePrefixRows ?? 0)} of 256");
        Console.WriteLine("NEW_LEVEL_EDIT_OBJECTS_OK");
    }
}

internal static partial class NewLevelBuildCases
{
    /// <summary>The Create BIN dialog's "To finish the game 100%" lines for the project as saved.</summary>
    private static void CompletionCheck(MainWindow window)
    {
        var catalog = Get<Spyro.Editor.Core.Levels.LevelCatalog>(window, "_retailCatalog");
        var saved = Spyro.Editor.Core.Editing.SceneDocumentStore.ReadAll(Workspace(window).RootPath, catalog);
        var targets = Invoke(window, "FindEditedLevelExportTargets");
        var lines = (List<string>)Invoke(window, "CompletionGapLines", saved, targets)!;
        Console.WriteLine($"completion gaps: {lines.Count}");
        foreach (string line in lines) Console.WriteLine("  " + line);
        // COMPLETION_LEVELS (comma list): open each level and read the line under its treasure, as the user sees it.
        foreach (string level in (Environment.GetEnvironmentVariable("COMPLETION_LEVELS") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            Load(window, level);
            var line = Get<Avalonia.Controls.TextBlock>(window, "_completionText");
            Wait(() => line.IsVisible, $"{level}: the 100% line", 180);
            Console.WriteLine($"  {level}: {line.Text!.Replace("\n", " | ")}");
        }
        Console.WriteLine("NEW_LEVEL_COMPLETION_CHECK_OK");
    }
}

using Avalonia.Controls;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Generation;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

/// <summary>Fix It For Me: when Create BIN stops on something the editor can put right by itself
/// (a refusal with an <see cref="ExportRepair"/>), the status bar and the Status Details window show a button that does it
/// and says what it did; Create BIN is then pressed again. The first such repair places the way into the project's new
/// world in the homeworld it is entered from, on open ground the editor finds (<see cref="WorldEntranceSpots"/>).</summary>
public sealed partial class MainWindow
{
    private Button? _fixItButton;
    private ExportRepair? _pendingRepair;
    private string _pendingRepairWorkspace = "";
    private bool _repairBusy;

    /// <summary>The repair on offer (null: none).</summary>
    internal ExportRepair? PendingRepairForTesting => _pendingRepair;
    internal bool FixItVisibleForTesting => _fixItButton?.IsVisible == true;
    internal Task ApplyPendingRepairForTestingAsync() => ApplyPendingRepairAsync();

    private Button BuildFixItButton()
    {
        _fixItButton = NewAsyncButton("Fix It For Me", ApplyPendingRepairAsync);
        _fixItButton.Name = "FixItForMe";
        _fixItButton.IsVisible = false;
        StyleModernPrimaryButton(_fixItButton, ModernBlue);
        ToolTip.SetTip(_fixItButton, "Let the editor put right what stopped Create BIN. It says what it did; then press Create BIN again.");
        return _fixItButton;
    }

    /// <summary>Remembers the repair of a Create BIN failure, if it has one, and returns the words that point to the button.</summary>
    private string? OfferRepair(Exception failure)
    {
        _pendingRepair = ExportRepair.Find(failure);
        _pendingRepairWorkspace = _workspace.RootPath;
        if (_fixItButton != null) _fixItButton.IsVisible = _pendingRepair != null;
        return _pendingRepair == null ? null : "Or press Fix It For Me (beside this message) and the editor does it for you.";
    }

    private void ForgetRepair()
    {
        _pendingRepair = null;
        if (_fixItButton != null) _fixItButton.IsVisible = false;
    }

    private async Task ApplyPendingRepairAsync()
    {
        if (_repairBusy || _pendingRepair is not { } repair) return;
        if (_createBinBusy) { _statusText.Text = "Create BIN is running. Fix It For Me works once it has stopped."; return; }
        if (!string.Equals(_pendingRepairWorkspace, _workspace.RootPath, StringComparison.Ordinal)) { ForgetRepair(); _statusText.Text = "That problem was in another project."; return; }
        _repairBusy = true;
        if (_fixItButton != null) _fixItButton.IsEnabled = false;
        try
        {
            _statusText.Text = "Fixing it…";
            var (done, message) = repair switch
            {
                ExportRepair.PlaceWorldEntrance place => await PlaceWorldEntranceForMeAsync(place.HomeworldKey),
                ExportRepair.FitHomeworldObjects fit => await FitHomeworldObjectsForMeAsync(fit.Room),
                _ => (false, "The editor has no fix for this."),
            };
            // Done or not, the button has had its turn: what is left to do is in the message.
            ForgetRepair();
            _statusText.Text = message;
            Spyro.Editor.Core.Diagnostics.EditorDiagnostics.RecordAction("Fix It For Me", $"{repair}: {(done ? "done" : "not done")}. {message}");
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        {
            ForgetRepair();
            _statusText.Text = "The editor could not fix it by itself: " + e.Message;
        }
        finally
        {
            _repairBusy = false;
            if (_fixItButton != null) _fixItButton.IsEnabled = true;
        }
    }

    /// <summary>Makes the new world's homeworld fit its save rows (<see cref="SceneDocument.FitSaveRows"/>): the level as
    /// it was is kept in the project's backups folder, the fitted one saved in its place and, if it is the open level,
    /// shown.</summary>
    private async Task<(bool Done, string Message)> FitHomeworldObjectsForMeAsync(int room)
    {
        string root = _workspace.RootPath, key = NewWorldDocument.KeyFor(NewWorldDocument.Homeworld), path = SceneDocumentStore.PathForTarget(root, key);
        bool open = _freshSceneDocument?.WorldLevelId == NewWorldDocument.Homeworld;
        if (open && !await SaveFreshSceneAsync()) return (false, "The homeworld could not be saved first: " + _statusText.Text);
        var scene = open ? CaptureFreshScene() : SceneDocumentStore.ReadForTarget(root, NewWorldDocument.Target(NewWorldDocument.Homeworld, ""), _retailCatalog);
        if (scene == null) return (false, "This project's new world has no homeworld saved.");
        int before = scene.Ledger.Entries.Count;
        if (before <= room) return (true, $"{scene.Name} already fits ({before} of {room} objects). Press Create BIN again.");
        var fit = scene.FitSaveRows(room);
        if (fit.FreedRows == 0 && fit.RemovedGems == 0)
            return (false, $"{scene.Name} has {before} objects and room for {room}, and no loose gems the editor could take out by itself. Delete {before - room} objects (select one on the Objects tab, then Delete).");
        string kept = Path.Combine(root, "backups", "fix-it", $"{DateTime.Now:yyyyMMdd-HHmmss}-{Path.GetFileName(path)}");
        Directory.CreateDirectory(Path.GetDirectoryName(kept)!);
        File.Copy(path, kept, overwrite: true);
        await fit.Scene.SaveAsync(path);
        if (open) ApplyFreshScene(fit.Scene);
        var did = new List<string>();
        if (fit.FreedRows > 0) did.Add($"gave back {Count(fit.FreedRows, "save row", "save rows")} of deleted objects");
        if (fit.RemovedGems > 0) did.Add($"took out {Count(fit.RemovedGems, "loose gem", "loose gems")} worth {fit.RemovedTreasure} treasure (the least valuable ones)");
        string what = $"in {scene.Name} the editor {string.Join(" and ", did)}: it now has {fit.Scene.Ledger.Entries.Count} objects of the {room} a new world's homeworld holds";
        string backup = " The level as it was is kept in the project's backups folder (fix-it).";
        return fit.StillOver > 0
            ? (false, $"Partly fixed: {what}, still {fit.StillOver} too many. Delete {Count(fit.StillOver, "more object", "more objects")} (select one on the Objects tab, then Delete), then press Create BIN again.{backup}")
            : (true, $"Fixed: {what}. Press Create BIN again.{backup}");
    }

    /// <summary>Places the way into the project's new world in <paramref name="homeworldKey"/> (the homeworld the world is
    /// entered from): the best place <see cref="WorldEntranceSpots"/> finds that the homeworld also has the memory for,
    /// in the homeworld's own kind of doorway, at normal size or (where that has no room) the smallest.</summary>
    private async Task<(bool Done, string Message)> PlaceWorldEntranceForMeAsync(string homeworldKey)
    {
        string root = _workspace.RootPath;
        var (world, entrance) = WorldEntranceProject();
        var level = _retailCatalog.FindByKey(homeworldKey);
        if (world == null || level == null) return (false, "This project has no new world to place a way into.");
        if (!string.Equals(world.EntryHomeworldKey, homeworldKey, StringComparison.OrdinalIgnoreCase))
            return (false, $"{world.Name} is entered from another homeworld now. Press Create BIN again.");
        if (entrance != null && string.Equals(entrance.HomeworldKey, homeworldKey, StringComparison.OrdinalIgnoreCase))
            return (true, $"The way into {world.Name} already stands in {level.DisplayName}. Press Create BIN again.");
        string by = $"open {level.DisplayName}, then Terrain › World Entrance › Place Entrance…";
        if (File.Exists(SceneDocumentStore.PathForTarget(root, level.Key)))
            return (false, $"{level.DisplayName} is a level you built in the homeworld's place, and the way into a new world stands in one of the game's own homeworlds. " +
                           "Open one of those, then Terrain › World Entrance › Use This Homeworld and Place Entrance….");
        string disc = ResolveUniversalSourceDisc();
        if (string.IsNullOrEmpty(disc) || !File.Exists(disc))
            return (false, "Choose your original Spyro the Dragon (USA) disc first (Open Game…), then press Create BIN again.");
        var shape = LevelThemes.DoorwayShape(homeworldKey);
        var mode = GameRam.Mode;
        var placed = await Task.Run(() =>
        {
            UniversalSourceLibrary.RequireOriginal(disc);
            var originals = _worldEntranceOriginals is { } cached && cached.ImagePath == disc ? cached : _worldEntranceOriginals = new UniversalSourceLibrary(disc, _retailCatalog);
            string scenePath = SceneDocumentStore.PathForTarget(root, NewWorldDocument.KeyFor(NewWorldDocument.Homeworld));
            byte[] sky = File.Exists(scenePath) ? NativeWorldEntrance.SceneSky(disc, SceneDocument.Read(scenePath)) : NativeWorldEntrance.PlainSky(new Spyro.Editor.Core.Primitives.ColorRgba(120, 170, 230));
            var image = originals.Disc;
            using var scope = GameRam.Use(mode);
            foreach (float scale in new[] { 1f, (float)DoorwayShapes.MinScale })
                foreach (var spot in WorldEntranceSpots.Find(originals, _retailCatalog, homeworldKey, shape, scale))
                {
                    var document = new WorldEntranceDocument(1, level.Key, spot.Position, spot.Heading, shape.Id, scale);
                    try
                    {
                        NativeWorldEntrance.Apply(originals, _retailCatalog, document.Spec(), image.EntryBytes(level.SourceWadEntry), image.EntryBytes(level.SourceWadEntry - 1), image.Exe, sky);
                        return ((WorldEntranceDocument Document, WorldEntranceSpots.Spot Spot)?)(document, spot);
                    }
                    catch (InvalidDataException) { /* the homeworld cannot hold a doorway here: the next place */ }
                }
            return null;
        });
        if (placed is not { } found)
            return (false, $"The editor found no free place for a doorway in {level.DisplayName} by itself. Place it where you like: {by}");
        await found.Document.SaveAsync(root);
        RefreshWorldEntranceCard();
        string where = found.Spot.FromArrival < 900 ? "near where Spyro arrives" : $"about {Math.Round(found.Spot.FromArrival / 100) * 100:N0} units from where Spyro arrives";
        string size = found.Document.Scale < 0.99f ? " (the small size: the only one with room there)" : "";
        return (true, $"Fixed: placed the way into {world.Name} in {level.DisplayName} for you, a {shape.Name.ToLowerInvariant()}{size} on open ground {where}. " +
                      $"Press Create BIN again. To see or move it, {by.Replace(" › Place Entrance…", "")}.");
    }
}

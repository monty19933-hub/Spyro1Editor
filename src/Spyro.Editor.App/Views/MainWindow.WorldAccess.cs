using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

/// <summary>How players reach the project's own world and come back (<see cref="WorldAccess"/>), as the editor keeps it
/// right: by balloon, like the game's own worlds — no doorway is put into the game's homeworlds and the world's homeworld
/// has no doorway back out, but it has a balloonist to fly Spyro back. A doorway as well is the user's own choice
/// (Terrain › World Entrance). The world is called by its homeworld's name.</summary>
public sealed partial class MainWindow
{
    /// <summary>One of the game's homeworlds by level id (10-60), under the game's name.</summary>
    private string GameHomeworldName(int levelId) => _retailCatalog.Levels.FirstOrDefault(l => l.LevelId == levelId)?.DisplayName ?? $"level {levelId}";

    /// <summary>"Players reach GLOOMY FEN by balloon: Artisans' balloonist flies Spyro there once he has found 50 treasure, …".</summary>
    private string WorldAccessSentence(NewWorldDocument world, WorldBalloonPlace place) => WorldAccess.Describe(world, place, GameHomeworldName);

    /// <summary>The world's homeworld as it is now: the open level if it is the one, else as saved (null: not made).</summary>
    private SceneDocument? WorldHomeworldNow()
    {
        if (_freshSceneDocument?.WorldLevelId == NewWorldDocument.Homeworld && _freshSceneDocument.TargetBinding == null && _currentGeometry != null) return CaptureFreshScene();
        string path = SceneDocumentStore.PathForTarget(_workspace.RootPath, NewWorldDocument.KeyFor(NewWorldDocument.Homeworld));
        return File.Exists(path) ? SceneDocument.Read(path) : null;
    }

    /// <summary>The world under its homeworld's name, saved when that changes it (a world made before may still have
    /// the name it was made with while its homeworld is called something else). Returns the world and a sentence for
    /// the status line ("" when nothing changed).</summary>
    /// <param name="calledBefore">The world's name before (Create BIN's own save of the open homeworld may have renamed it
    /// already); null: <paramref name="world"/>'s.</param>
    private async Task<(NewWorldDocument World, string Note)> NameWorldAfterHomeworldAsync(NewWorldDocument world, string? calledBefore = null)
    {
        var named = world.NamedAfter(WorldHomeworldNow()?.Name);
        if (named != world)
        {
            await named.SaveAsync(_workspace.RootPath);
            Spyro.Editor.Core.Diagnostics.EditorDiagnostics.RecordAction("World named after its homeworld", $"{world.Name} -> {named.Name}");
        }
        string before = calledBefore ?? world.Name;
        return (named, named.Name == before ? "" : $" Your world is called {named.Name} now, after its homeworld (the balloonists' menus and the inventory show that name; it was {before}).");
    }

    /// <summary>The world's homeworld without its doorways back out to the game (made when the world was
    /// entered through a doorway): the world is reached by balloon only, so they would lead nowhere. The level as it was
    /// is kept in the project's backups/fix-it folder; the open level is shown without them. Returns a sentence for the
    /// status line ("" when there was none).</summary>
    private async Task<string> RemoveWorldWayBackAsync()
    {
        string root = _workspace.RootPath, path = SceneDocumentStore.PathForTarget(root, NewWorldDocument.KeyFor(NewWorldDocument.Homeworld));
        bool open = _freshSceneDocument?.WorldLevelId == NewWorldDocument.Homeworld && _freshSceneDocument.TargetBinding == null;
        if (open && !await SaveFreshSceneAsync()) throw new InvalidDataException("Your world's homeworld could not be saved first: " + _statusText.Text);
        if (!File.Exists(path)) return "";
        var scene = SceneDocument.Read(path);
        var backs = WorldAccess.WaysBack(scene);
        if (backs.Count == 0) return "";
        string kept = Path.Combine(root, "backups", "fix-it", $"{DateTime.Now:yyyyMMdd-HHmmss}-{Path.GetFileName(path)}");
        Directory.CreateDirectory(Path.GetDirectoryName(kept)!);
        File.Copy(path, kept, overwrite: true);
        var without = scene;
        foreach (var back in backs) without = SceneDoorways.Remove(without, back.DestinationLevelId);
        await without.SaveAsync(path);
        // The open homeworld is shown without them, and saved as the editor holds it.
        if (open)
        {
            ApplyFreshScene(without);
            if (!await SaveFreshSceneAsync()) throw new InvalidDataException("Your world's homeworld could not be saved without its doorway back: " + _statusText.Text);
        }
        string to = string.Join(" and ", backs.Select(b => GameHomeworldName(b.DestinationLevelId)).Distinct());
        Spyro.Editor.Core.Diagnostics.EditorDiagnostics.RecordAction("World's doorway back out taken out", $"{scene.Name}: doorway back to {to}; the level as it was: {kept}");
        return $" Took {Owned(scene.Name)} doorway back out to {to} (and its frame) out of the level: your world is reached by balloon, and its balloonist flies Spyro back. " +
               "The level as it was is kept in the project's backups folder (fix-it); for a doorway as well, use Links › World Entrance.";
    }

    /// <summary>Stands the world's balloonist with his balloon in its homeworld, which must be the open level: Artisans'
    /// balloonist (the world is built on Artisans' place; in the game he takes the name the balloon route gives him), on
    /// open floor near where Spyro arrives (<see cref="WorldAccess.BalloonistSpot"/>), facing there; the level is saved.</summary>
    private async Task<(bool Done, string Message)> PlaceWorldBalloonistHereAsync()
    {
        if (_freshSceneDocument is not { WorldLevelId: NewWorldDocument.Homeworld, TargetBinding: null } || _currentGeometry == null)
            return (false, "Open your world's homeworld first.");
        if (WorldAccess.HasBalloonist(_currentMobys)) return (true, "");
        string disc = ResolveUniversalSourceDisc();
        if (string.IsNullOrEmpty(disc) || !File.Exists(disc))
            return (false, "Choose your original Spyro the Dragon (USA) disc first (Open Game…), then press Create BIN again.");
        UniversalSourceLibrary lib; IReadOnlyList<UniversalMobyCatalogEntry> entries;
        try { (_, lib, entries) = await EnsureUniversalCatalogAsync(); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        { return (false, "The balloonist is copied from your original Spyro the Dragon (USA) disc: " + e.Message); }
        var pilot = entries.FirstOrDefault(e => e.ClassId == UniversalBalloonistRows.BalloonistClass && e.Placeable);
        var marco = pilot?.Variants.FirstOrDefault(v => v.RestrictionReason == null && v.SourceLevelKey == NativeNewWorldExport.TemplateKey);
        var scene = CaptureFreshScene();
        if (pilot == null || marco == null) return (false, $"The balloonist could not be copied from Artisans. Add him yourself in {scene.Name}: Add Object… › Balloonist.");
        if (WorldAccess.BalloonistSpot(scene) is not { } spot)
            return (false, $"The editor found no open floor near where Spyro arrives in {scene.Name} for the balloonist and his balloon. Place him yourself: " +
                           "Objects › Random objects (\"The way out\"), or Add Object… › Balloonist.");
        var added = await AddUniversalMobyAtAsync(lib, pilot, marco, spot.Position, spot.Yaw);
        if (added.Count == 0) return (false, "The balloonist could not be added: " + _statusText.Text);
        if (!await SaveFreshSceneAsync()) return (false, "The balloonist was added, but the level could not be saved: " + _statusText.Text);
        string who = WorldBalloonistName();
        int away = (int)Math.Round(Math.Sqrt(Math.Pow(spot.Position.X - scene.Spawn.X, 2) + Math.Pow(spot.Position.Y - scene.Spawn.Y, 2)) / 10) * 10;
        return (true, $"Stood {Owned(scene.Name)} balloonist {who} with his balloon {away} units from where Spyro arrives, to fly him back out of your world " +
                      "(move them with the Objects tab if you like).");
    }

    /// <summary>Fix It For Me: opens the world's homeworld if it is not open, then <see cref="PlaceWorldBalloonistHereAsync"/>.</summary>
    private async Task<(bool Done, string Message)> PlaceWorldBalloonistForMeAsync()
    {
        if (_freshSceneDocument is not { WorldLevelId: NewWorldDocument.Homeworld, TargetBinding: null })
        {
            string path = SceneDocumentStore.PathForTarget(_workspace.RootPath, NewWorldDocument.KeyFor(NewWorldDocument.Homeworld));
            if (!File.Exists(path)) return (false, "This project's new world has no homeworld saved.");
            await OpenWorldLevelAsync(NewWorldDocument.Homeworld);
            if (_freshSceneDocument is not { WorldLevelId: NewWorldDocument.Homeworld }) return (false, "Your world's homeworld could not be opened: " + _statusText.Text);
        }
        return await PlaceWorldBalloonistHereAsync();
    }

    /// <summary>The name the world's balloonist has in the game (World's Balloon Route…).</summary>
    private string WorldBalloonistName()
    {
        try { return NewWorldDocument.Read(_workspace.RootPath)?.Balloon?.Balloonist ?? WorldBalloonPlace.DefaultBalloonist; }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException) { return WorldBalloonPlace.DefaultBalloonist; }
    }

    private (string Key, string? Name) _savedWorldHomeName = ("", null);

    /// <summary>The name the world's homeworld is saved with (null: not made, or unreadable); read again only when its
    /// file changes (cards refresh often).</summary>
    private string? SavedWorldHomeworldName()
    {
        if (string.IsNullOrEmpty(_workspace.RootPath)) return null;
        string path = SceneDocumentStore.PathForTarget(_workspace.RootPath, NewWorldDocument.KeyFor(NewWorldDocument.Homeworld));
        if (!File.Exists(path)) return null;
        string key = path + "@" + File.GetLastWriteTimeUtc(path).Ticks + ":" + new FileInfo(path).Length;
        if (_savedWorldHomeName.Key != key)
        {
            try { _savedWorldHomeName = (key, SceneDocument.Read(path).Name); }
            catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException) { _savedWorldHomeName = (key, null); }
        }
        return _savedWorldHomeName.Name;
    }

    /// <summary>The world's homeworld's name, or the world's when it has none saved.</summary>
    private string WorldHomeworldNameOr(NewWorldDocument world) => SavedWorldHomeworldName() ?? world.Name;

    /// <summary>The world as saved, or null (none, or unreadable).</summary>
    private NewWorldDocument? ProjectWorldOrNull()
    {
        if (string.IsNullOrEmpty(_workspace.RootPath)) return null;
        try { return NewWorldDocument.Read(_workspace.RootPath); }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException) { return null; }
    }
}

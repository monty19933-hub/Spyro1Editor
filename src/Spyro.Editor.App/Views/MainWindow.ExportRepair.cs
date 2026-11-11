using Avalonia.Controls;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Generation;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

/// <summary>Fix It For Me: whenever Create BIN stops on something in the project, the status bar and the Status Details
/// window show a button that gets the game made. It works through one stop after another, running Create BIN itself:
/// a stop the editor knows a repair for (a refusal with an <see cref="ExportRepair"/>) is repaired, for instance the way
/// into the project's new world is placed on open ground the editor finds (<see cref="WorldEntranceSpots"/>); any other
/// stop is got past by making the game without the part of the project that causes it (found by trying), which stays in
/// the project and is remembered as left out (<see cref="Spyro.Editor.Core.Persistence.ExportLeftOutSettings"/>). At the
/// end it says everything it did.</summary>
public sealed partial class MainWindow
{
    /// <summary>What stopped the last Create BIN: its words and, when the editor knows one, its repair.</summary>
    private sealed record CreateStop(string Message, ExportRepair? Repair);
    /// <summary>How one Create BIN run by Fix It For Me ended.</summary>
    private sealed record CreateOutcome(bool Created, CreateStop? Stop, string Status);

    private const string FixItOffer = "Press Fix It For Me (beside this message): the editor puts right what it can and makes the game, and tells you about anything it had to leave out.";
    /// <summary>The most Create BIN runs one press of Fix It For Me makes.</summary>
    private const int MaxFixItRuns = 40;

    private Button? _fixItButton;
    private CreateStop? _pendingStop;
    private string _pendingRepairWorkspace = "";
    private bool _repairBusy;
    /// <summary>Fix It For Me is running Create BIN: Create BIN's questions are answered as they were last time.</summary>
    private bool _quietCreate;
    /// <summary>Which try Fix It For Me is on (shown in front of Create BIN's own progress).</summary>
    private string _fixItProgress = "";
    /// <summary>The level New Game starts in, as chosen in the last Create BIN window.</summary>
    private int? _lastExportStartLevel;
    /// <summary>Set by a Create BIN run that wrote its game (the CUE's path); null while one runs or after one that stopped.</summary>
    private string? _createdGameCue;

    /// <summary>The repair on offer (null: none, or a stop with no repair of its own).</summary>
    internal ExportRepair? PendingRepairForTesting => _pendingStop?.Repair;
    /// <summary>Fix It For Me is on offer for the last Create BIN's stop.</summary>
    internal bool FixItOnOfferForTesting => _pendingStop != null;
    internal bool FixItVisibleForTesting => _fixItButton?.IsVisible == true;
    internal Task ApplyPendingRepairForTestingAsync() => ApplyPendingRepairAsync();
    /// <summary>Lets a test make Create BIN stop: called at the start of every Create BIN.</summary>
    internal Action? CreateBinFaultForTesting { get; set; }

    private Button BuildFixItButton()
    {
        _fixItButton = NewAsyncButton("Fix It For Me", ApplyPendingRepairAsync);
        _fixItButton.Name = "FixItForMe";
        _fixItButton.IsVisible = false;
        StyleModernPrimaryButton(_fixItButton, ModernBlue);
        ToolTip.SetTip(_fixItButton, "Let the editor get past what stopped Create BIN: it repairs what it can, makes the game, and says what it did and anything it had to leave out.");
        return _fixItButton;
    }

    /// <summary>Whether a Create BIN failure is about the project (something Fix It For Me can work on) and not about
    /// the computer: a missing disc image or folder, no permission, a full or failing disk, a cancelled run.</summary>
    private static bool IsProjectStop(Exception failure)
    {
        for (Exception? e = failure; e != null; e = e.InnerException)
        {
            if (e is OperationCanceledException or FileNotFoundException or DirectoryNotFoundException or DriveNotFoundException or
                UnauthorizedAccessException or PathTooLongException or OutOfMemoryException)
                return false;
            if (e is IOException) return false;
        }
        return true;
    }

    /// <summary>Remembers what stopped Create BIN and returns the words that point to the button (null: the failure is
    /// not one Fix It For Me can work on).</summary>
    private string? OfferRepair(Exception failure)
    {
        if (!IsProjectStop(failure)) { ForgetRepair(); return null; }
        return OfferFixFor(failure.Message, ExportRepair.Find(failure));
    }

    /// <summary>The same for a stop that is not an exception (Create BIN returned with its reason in the status bar).</summary>
    private string OfferFixFor(string message, ExportRepair? repair = null)
    {
        _pendingStop = new(message, repair);
        _pendingRepairWorkspace = _workspace.RootPath;
        if (_fixItButton != null) _fixItButton.IsVisible = true;
        return FixItOffer;
    }

    /// <summary>Create BIN stops here with <paramref name="message"/> in the status bar, and Fix It For Me on offer.</summary>
    private void StopCreateBin(string message)
    {
        _statusText.Text = message + " " + OfferFixFor(message);
    }

    private void ForgetRepair()
    {
        _pendingStop = null;
        if (_fixItButton != null) _fixItButton.IsVisible = false;
    }

    private async Task ApplyPendingRepairAsync()
    {
        if (_repairBusy || _pendingStop is not { } stop) return;
        if (_createBinBusy) { _statusText.Text = "Create BIN is running. Fix It For Me works once it has stopped."; return; }
        if (!string.Equals(_pendingRepairWorkspace, _workspace.RootPath, StringComparison.Ordinal)) { ForgetRepair(); _statusText.Text = "That problem was in another project."; return; }
        _repairBusy = true;
        if (_fixItButton != null) _fixItButton.IsEnabled = false;
        List<string> did = [];
        HashSet<string> outBefore = [.. LeftOutItems().Select(i => i.Part)];
        try
        {
            _statusText.Text = "Fix It For Me: working on it…";
            string result = await FixItAsync(stop, did);
            // Made or not, the button has had its turn: what is left to do is in the words.
            ForgetRepair();
            _statusText.Text = result;
            Spyro.Editor.Core.Diagnostics.EditorDiagnostics.RecordAction("Fix It For Me", result);
            // Something of the user's is not in the game: that is said in a window of its own, not only in the line
            // at the bottom (it opens by itself and is not waited for).
            if (ShowLeftOutAfterFixIt && LeftOutItems().Any(i => !outBefore.Contains(i.Part)))
                _ = ShowLeftOutCoreAsync(afterFixIt: true);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        {
            ForgetRepair();
            _statusText.Text = "Fix It For Me could not finish: " + e.Message + DidSoFar(did);
        }
        finally
        {
            _quietCreate = false;
            _repairBusy = false;
            if (_fixItButton != null) _fixItButton.IsEnabled = true;
        }
    }

    private static string DidSoFar(IReadOnlyList<string> did) =>
        did.Count == 0 ? "" : " What it did so far: " + Numbered(did);

    private static string Numbered(IReadOnlyList<string> did) =>
        did.Count == 1 ? did[0] : string.Join(" ", did.Select((d, i) => $"({i + 1}) {d}"));

    /// <summary>Works through Create BIN's stops, the first being <paramref name="stop"/>, until the game is made or
    /// something is left that only the user can do. Returns the words for the status bar.</summary>
    private async Task<string> FixItAsync(CreateStop stop, List<string> did)
    {
        int runs = 0;
        HashSet<string> repaired = new(StringComparer.Ordinal);
        string Finished(CreateOutcome made) => made.Status + (did.Count == 0 ? "" : " Fix It For Me got it made: " + Numbered(did));
        string GaveUp(string why) => "Fix It For Me could not get the game made. " + why + DidSoFar(did);
        while (true)
        {
            CreateOutcome next;
            if (stop.Repair is { } repair && repaired.Add(repair.ToString() + "|" + stop.Message))
            {
                // A stop the editor has a repair for.
                var (done, message) = await ApplyRepairAsync(repair, stop);
                if (!done) { ForgetRepair(); return message + DidSoFar(did); }
                if (message.Length > 0) did.Add(message);
                if (++runs > MaxFixItRuns) return GaveUp("It kept finding something else.");
                next = await CreateBinQuietlyAsync($"Fix It For Me: making the game (run {runs})…");
            }
            else
            {
                // No repair known (or the repair did not get past it): the same Create BIN once more, in case the stop was
                // a passing one; then the game without the part of the project that stops it.
                if (++runs > MaxFixItRuns) return GaveUp("It kept finding something else.");
                next = await CreateBinQuietlyAsync($"Fix It For Me: trying Create BIN again (run {runs})…");
                if (!next.Created && next.Stop is { } same && same.Message == stop.Message)
                {
                    var search = await LeaveOutWhatStopsAsync(stop, did, () => ++runs <= MaxFixItRuns, () => runs);
                    if (search.GaveUp is { } why) return GaveUp(why);
                    next = search.Outcome!;
                }
            }
            if (next.Created)
            {
                ForgetRepair();
                // Create BIN for the game's own levels shows the output folder when it is done; Fix It For Me's runs
                // kept it shut until this last one.
                if (next.Status.Contains(" Output folder: ", StringComparison.Ordinal) && _createdGameCue is { } cue && Path.GetDirectoryName(cue) is { Length: > 0 } folder)
                    try { OpenPath(folder); } catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or IOException) { }
                return Finished(next);
            }
            if (next.Stop is not { } another)
            {
                // Create BIN ended without making the game and without a stop Fix It For Me can work on.
                ForgetRepair();
                return "Fix It For Me stopped here: " + next.Status + DidSoFar(did);
            }
            stop = another;
        }
    }

    /// <summary>Runs Create BIN without its questions (they are answered as last time) and reports how it ended.</summary>
    private async Task<CreateOutcome> CreateBinQuietlyAsync(string progress)
    {
        _quietCreate = true;
        _fixItProgress = progress;
        try
        {
            _statusText.Text = progress;
            await RunCreateBinCommandWithVisibleFeedbackAsync(CreateObjectTestBinCoreAsync);
        }
        finally { _quietCreate = false; }
        string status = _statusText.Text ?? "";
        return _createdGameCue != null ? new(true, null, status) : new(false, _pendingStop, status);
    }

    private async Task<(bool Done, string Message)> ApplyRepairAsync(ExportRepair repair, CreateStop stop)
    {
        switch (repair)
        {
            case ExportRepair.PlaceWorldEntrance place: return await PlaceWorldEntranceForMeAsync(place.HomeworldKey);
            case ExportRepair.FitHomeworldObjects fit: return await FitHomeworldObjectsForMeAsync(fit.Room);
            case ExportRepair.LeaveOutSkippedChanges skipped:
                LeaveOut(Spyro.Editor.Core.Persistence.ExportParts.SkippedChanges, stop.Message);
                return (true, $"Left out {Count(skipped.Details.Count, "saved change", "saved changes")} the game cannot take ({ShortList(skipped.Details)}); " +
                              "they stay in your project and Create BIN names them each time.");
            default: return (false, "The editor has no fix for this.");
        }
    }

    private static string ShortList(IReadOnlyList<string> details)
    {
        static string Short(string text) => text.Length <= 140 ? text : text[..137].TrimEnd() + "…";
        return string.Join("; ", details.Take(2).Select(d => Short(PlainSkippedChange(d)))) + (details.Count > 2 ? $"; and {details.Count - 2} more" : "");
    }

    /// <summary>A skipped change in plain words where the editor knows them (the stage's own words stay in the refusal
    /// and the diagnostic report).</summary>
    private static string PlainSkippedChange(string detail)
    {
        var thief = System.Text.RegularExpressions.Regex.Match(detail, @"^(?<level>[^:]+): Egg thief T\d+: egg-thief path export blocked");
        return thief.Success ? $"{thief.Groups["level"].Value}: the egg thief's new run (a point of it is off the ground he runs on, so he keeps the run he had)" : detail;
    }

    /// <summary>The last resort for a stop the editor has no repair for: finds the part of the project that causes it by
    /// making the game without one part at a time (a part the refusal names first), and leaves that part out. If no single
    /// part is it, everything is left out and then put back part by part, keeping out only what stops the game.</summary>
    private async Task<(CreateOutcome? Outcome, string? GaveUp)> LeaveOutWhatStopsAsync(CreateStop stop, List<string> did, Func<bool> mayRun, Func<int> run)
    {
        var parts = ProjectParts();
        if (parts.Count == 0)
            return (null, "Nothing of yours is in the game any more and Create BIN still stops: " + stop.Message +
                          " That is not something in your project: check your original disc image (Open Game…) and the space on your disk.");
        static bool Names(ExportPart part, string message) => part.Names.Any(n => n.Length > 2 && ContainsWord(message, n));
        // Parts the refusal names come first, the one with the least work in it before the others (when it names two
        // that cannot be in one game, the smaller one goes); then the rest in the project's order.
        var ordered = parts.Where(p => Names(p, stop.Message)).OrderBy(p => p.Work).Concat(parts.Where(p => !Names(p, stop.Message))).ToList();
        foreach (var part in ordered)
        {
            if (!mayRun()) return (null, "It tried many times and Create BIN still stops: " + stop.Message);
            LeaveOut(part.Id, stop.Message);
            var outcome = await CreateBinQuietlyAsync($"Fix It For Me: trying the game without {part.Label} (run {run()})…");
            if (outcome.Created)
            {
                var (made, what) = await LeaveOutNoMoreThanNeededAsync(part, stop, outcome, mayRun, run);
                did.Add($"Left out {what} because Create BIN stopped on it (“{Shortened(stop.Message)}”); it is still in your project (More › Left Out of the Game… puts it back).");
                return (made, null);
            }
            if (outcome.Stop is { } another && another.Message != stop.Message)
            {
                // Past this stop and on to another: the part stays out and the next stop gets its own turn.
                did.Add($"Left out {part.Label} because Create BIN stopped on it (“{Shortened(stop.Message)}”); it is still in your project (More › Left Out of the Game… puts it back).");
                return (outcome, null);
            }
            if (outcome.Stop == null) { PutBack(part.Id); return (outcome, null); }
            PutBack(part.Id); // it made no difference
        }
        // No single part: without everything, then each part back in turn.
        foreach (var part in ordered) LeaveOut(part.Id, stop.Message);
        if (!mayRun()) { foreach (var part in ordered) PutBack(part.Id); return (null, "It tried many times and Create BIN still stops: " + stop.Message); }
        var bare = await CreateBinQuietlyAsync($"Fix It For Me: trying the game with nothing of yours in it (run {run()})…");
        if (!bare.Created && bare.Stop != null)
        {
            foreach (var part in ordered) PutBack(part.Id);
            return (null, "Even with nothing of yours in the game, Create BIN stops: " + bare.Stop.Message +
                          " That is not something in your project: check your original disc image (Open Game…) and the space on your disk. Nothing was left out.");
        }
        // The stop is gone (with nothing of yours left there may be no game to make at all): each part back in turn,
        // keeping out only those that stop the game.
        CreateOutcome? last = bare.Created ? bare : null;
        List<ExportPart> kept = [];
        foreach (var part in Enumerable.Reverse(ordered))
        {
            if (!mayRun()) { kept.Add(part); continue; }
            PutBack(part.Id);
            var outcome = await CreateBinQuietlyAsync($"Fix It For Me: putting {part.Label} back (run {run()})…");
            if (outcome.Created) last = outcome;
            else { LeaveOut(part.Id, outcome.Stop?.Message ?? stop.Message); kept.Add(part); }
        }
        if (last == null)
        {
            foreach (var part in ordered) PutBack(part.Id);
            return (null, "Every part of your project stops Create BIN, one way or another: " + stop.Message + " Nothing was left out.");
        }
        did.Add($"Left out {string.Join("; ", kept.Select(p => p.Label))} because Create BIN stopped with {(kept.Count == 1 ? "it" : "them")} in the game (“{Shortened(stop.Message)}”); " +
                $"{(kept.Count == 1 ? "it is" : "they are")} still in your project (More › Left Out of the Game… puts {(kept.Count == 1 ? "it" : "them")} back).");
        return (last, null);
    }

    /// <summary>The game was made without all of a level's saved changes: tries to leave out only one kind of them
    /// (its objects, its ground, its sky, its music or its name). Returns the outcome that stands and what was left out.</summary>
    private async Task<(CreateOutcome Outcome, string What)> LeaveOutNoMoreThanNeededAsync(ExportPart part, CreateStop stop, CreateOutcome made, Func<bool> mayRun, Func<int> run)
    {
        if (part.Kinds.Count == 0 || !part.Id.StartsWith("level:", StringComparison.Ordinal)) return (made, part.Label);
        string key = part.Id["level:".Length..];
        string objects = Spyro.Editor.Core.Persistence.ExportParts.Level(key, Spyro.Editor.Core.Persistence.ExportParts.Objects);
        string outNow = part.Id;
        if (part.Kinds.Count == 1)
        {
            // The level has one kind of change, so the whole level and that kind are the same thing (no run needed).
            outNow = Spyro.Editor.Core.Persistence.ExportParts.Level(key, part.Kinds[0]);
            PutBack(part.Id); LeaveOut(outNow, stop.Message);
        }
        else
            foreach (string kind in part.Kinds)
            {
                if (!mayRun()) break;
                string only = Spyro.Editor.Core.Persistence.ExportParts.Level(key, kind);
                PutBack(part.Id);
                LeaveOut(only, stop.Message);
                var outcome = await CreateBinQuietlyAsync($"Fix It For Me: trying with only {PartLabel(only)} left out (run {run()})…");
                if (outcome.Created) { made = outcome; outNow = only; break; }
                PutBack(only);
                LeaveOut(part.Id, stop.Message);
            }
        if (outNow != objects) return (made, outNow == part.Id ? part.Label : PartLabel(outNow));
        return await LeaveOutOnlyTheObjectsThatStopAsync(key, stop, made, mayRun, run);
    }

    /// <summary>The most extra Create BIN runs spent on finding which added objects stop the game.</summary>
    private const int MaxObjectSearchRuns = 10;

    /// <summary>The game was made without a level's object changes: finds which KINDS of object added there stop it
    /// (every copy of a Crash frog, say) and leaves out only those, putting the rest of the level's object changes back.
    /// First the level's changes to its own objects go back in with every added kind out; then the added kinds are
    /// tested in halves. Returns the outcome that stands (its game is without exactly what is left out) and what that is.</summary>
    private async Task<(CreateOutcome Outcome, string What)> LeaveOutOnlyTheObjectsThatStopAsync(string key, CreateStop stop, CreateOutcome made, Func<bool> mayRun, Func<int> run)
    {
        string objects = Spyro.Editor.Core.Persistence.ExportParts.Level(key, Spyro.Editor.Core.Persistence.ExportParts.Objects);
        string name = _retailCatalog.FindByKey(key)?.DisplayName ?? key;
        List<(string Kind, string Label, int Count)> kinds;
        try { kinds = UniversalPlacementReader.ReadAddedKinds(Path.Combine(_workspace.RootPath, key + "-native-edits.json")); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException) { kinds = []; }
        if (kinds.Count == 0) return (made, PartLabel(objects));
        string Part(string kind) => Spyro.Editor.Core.Persistence.ExportParts.Added(key, kind);
        string Config() => string.Join("|", LeftOutItems().Select(i => i.Part).OrderBy(p => p, StringComparer.Ordinal));
        int spent = 0;
        bool More() => spent < MaxObjectSearchRuns && mayRun();
        async Task<CreateOutcome> Try(string what)
        {
            spent++;
            return await CreateBinQuietlyAsync($"Fix It For Me: finding which objects added to {name} stop the game: {what} (run {run()})…");
        }
        void AllOut() { foreach (var k in kinds) LeaveOut(Part(k.Kind), stop.Message); }
        void WholeLevelOut() { foreach (var k in kinds) PutBack(Part(k.Kind)); LeaveOut(objects, stop.Message); }

        // The changes to the level's own objects back in, everything added out.
        if (!More()) return (made, PartLabel(objects));
        PutBack(objects); AllOut();
        var standing = await Try("without any of them");
        if (!standing.Created) { WholeLevelOut(); return (made, PartLabel(objects)); }
        string standingConfig = Config();

        // Halves: a half put back that still makes the game is clean and stays in.
        async Task<List<(string Kind, string Label, int Count)>> Culprits(List<(string Kind, string Label, int Count)> suspects)
        {
            if (suspects.Count <= 1 || !More()) return suspects;
            var a = suspects.Take(suspects.Count / 2).ToList(); var b = suspects.Skip(suspects.Count / 2).ToList();
            foreach (var k in a) PutBack(Part(k.Kind));
            var withA = await Try($"with {Count(a.Sum(k => k.Count), "of them")} back");
            if (withA.Created) { standing = withA; standingConfig = Config(); return await Culprits(b); }
            foreach (var k in a) LeaveOut(Part(k.Kind), stop.Message);
            if (!More()) return suspects;
            foreach (var k in b) PutBack(Part(k.Kind));
            var withB = await Try($"with {Count(b.Sum(k => k.Count), "other")} back");
            if (withB.Created) { standing = withB; standingConfig = Config(); return await Culprits(a); }
            foreach (var k in b) LeaveOut(Part(k.Kind), stop.Message);
            // Each half holds something that stops the game.
            var inA = await Culprits(a);
            var inB = await Culprits(b);
            return [.. inA, .. inB];
        }
        var culprits = await Culprits(kinds);
        foreach (var k in kinds)
            if (culprits.Any(c => c.Kind == k.Kind)) LeaveOut(Part(k.Kind), stop.Message); else PutBack(Part(k.Kind));
        if (Config() != standingConfig)
        {
            // The game on disk is from another try: make it once more as it now stands.
            var final = mayRun() ? await Try("making the game without the ones that stop it") : null;
            if (final is { Created: true }) standing = final;
            else
            {
                // (The kinds do not stop the game one by one but together some other way: all of them stay out.)
                AllOut(); culprits = kinds;
                var again = mayRun() ? await Try("without any of them") : null;
                if (again is { Created: true }) standing = again;
                else { WholeLevelOut(); return (made, PartLabel(objects)); }
            }
        }
        int copies = culprits.Sum(c => c.Count), all = kinds.Sum(k => k.Count);
        string list = string.Join(", ", culprits.Select(c => c.Count > 1 ? $"{c.Count} × {c.Label}" : c.Label));
        return (standing, $"{list} ({(copies == all ? $"everything you added to {name}" : $"{copies} of the {all} objects you added to {name}")}; the rest of your changes there are in the game)");
    }

    private static string Shortened(string message)
    {
        string text = message.ReplaceLineEndings(" ").Trim();
        return text.Length <= 160 ? text : text[..157].TrimEnd() + "…";
    }

    /// <summary>Whether <paramref name="text"/> has <paramref name="name"/> as whole words (a level's name in a refusal).</summary>
    private static bool ContainsWord(string text, string name)
    {
        for (int at = text.IndexOf(name, StringComparison.Ordinal); at >= 0; at = text.IndexOf(name, at + 1, StringComparison.Ordinal))
        {
            bool before = at == 0 || !char.IsLetterOrDigit(text[at - 1]), after = at + name.Length >= text.Length || !char.IsLetterOrDigit(text[at + name.Length]);
            if (before && after) return true;
        }
        return false;
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
        if (before <= room) return (true, "");
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
            : (true, $"{Capitalized(what)}.{backup}");
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
            return (true, "");
        if (entrance != null && string.Equals(entrance.HomeworldKey, homeworldKey, StringComparison.OrdinalIgnoreCase))
            return (true, "");
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
        return (true, $"Placed the way into {world.Name} in {level.DisplayName} for you, a {shape.Name.ToLowerInvariant()}{size} on open ground {where} " +
                      $"(to see or move it, {by.Replace(" › Place Entrance…", "")}).");
    }
}

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private const string UniversalOriginalSha256 = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private (string Path, UniversalSourceLibrary Library, IReadOnlyList<UniversalMobyCatalogEntry> Entries)? _universalCatalog;
    private Task<(string, UniversalSourceLibrary, IReadOnlyList<UniversalMobyCatalogEntry>)>? _universalCatalogLoad;

    private static readonly (string Category, string Sub)[] UniversalCategoryOrder =
    [
        ("All Mobys", ""),
        ("Enemies", ""),
        ("NPCs", ""),
        ("NPCs", "Fodder & Animals"),
        ("Collectibles", ""),
        ("Collectibles", "Gems"),
        ("Collectibles", "Dragons"),
        ("Collectibles", "Dragonfly Eggs"),
        ("Collectibles", "Eggs"),
        ("Interactive", ""),
        ("Interactive", "Signs"),
        ("Interactive", "Chests & Containers"),
        ("Interactive", "Keys"),
        ("Interactive", "Doors, Platforms & Triggers"),
        ("Props", ""),
        ("Effects", ""),
        ("Other / Unknown", ""),
    ];

    private string ResolveUniversalSourceDisc() => FirstExistingDiscImagePath(_discImagePathBox.Text,
        _skyboxDiscImagePathBox.Text, DiscImageLocator.FindImage(_workspace));

    private Task<(string, UniversalSourceLibrary, IReadOnlyList<UniversalMobyCatalogEntry>)> EnsureUniversalCatalogAsync()
    {
        string path = ResolveUniversalSourceDisc();
        if (_universalCatalog is { } ready && PathsEqual(ready.Path, path))
            return Task.FromResult((ready.Path, ready.Library, ready.Entries));
        if (_universalCatalogLoad != null && !_universalCatalogLoad.IsFaulted) return _universalCatalogLoad;
        LevelCatalog catalog = _retailCatalog;
        string workspace = _workspace.RootPath;
        _universalCatalogLoad = Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("Select your original Spyro the Dragon (USA) disc image first.");
            using (var fs = File.OpenRead(path))
                if (Convert.ToHexStringLower(SHA256.HashData(fs)) != UniversalOriginalSha256)
                    throw new InvalidDataException("The universal Moby library reads objects from the unchanged original USA disc. The selected image is not that disc.");
            var lib = new UniversalSourceLibrary(path, catalog);
            var names = LoadUniversalNameIndex(workspace, catalog);
            var entries = UniversalMobyCatalogBuilder.Build(lib, catalog,
                (level, row) => names.TryGetValue((level, row), out var n) ? n : null);
            return (path, lib, entries);
        });
        _universalCatalogLoad.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully) _universalCatalog = t.Result;
        }, TaskScheduler.Default);
        return _universalCatalogLoad;
    }

    private static Dictionary<(string, int), (string, string)> LoadUniversalNameIndex(string workspaceRoot, LevelCatalog catalog)
    {
        Dictionary<(string, int), (string, string)> index = [];
        var workspace = new Spyro.Editor.Core.Workspace.EditorWorkspace(workspaceRoot);
        foreach (var level in catalog.Levels)
        {
            string path = Path.Combine(workspaceRoot, "editor-cache", $"{level.Key}-mobys.json");
            if (!File.Exists(path)) continue;
            try
            {
                List<Moby> mobys = MobyLoader.LoadCached(path).ToList();
                MobyMetadataEnricher.Apply(workspace, level.Key, mobys);
                foreach (var m in mobys.Where(m => m.TrueIndex >= 0))
                    index[(level.Key, m.TrueIndex)] = (m.DisplayLabel, m.CandidateKind);
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException) { }
        }
        return index;
    }

    private Button BuildUniversalMobyLibraryButton(string name, string label = "Add object…", string? category = null, string? sub = null, Color? color = null)
    {
        Button button = NewButton(label);
        button.Name = name;
        StyleModernPrimaryButton(button, color ?? ModernGreen);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        ToolTip.SetTip(button, sub switch
        {
            "Dragonfly Eggs" => "Place a collectible dragonfly egg. Choose its colour in the next window.",
            "Signs" => "Place a sign. You can type its message afterwards with Edit.",
            _ => "Pick any object from any Spyro level (enemies, NPCs, gems, eggs, signs, props…) and place it here."
        });
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            try { await ShowUniversalMobyLibraryAsync(this, category, sub); }
            finally { button.IsEnabled = true; }
        };
        return button;
    }

    /// <summary>The Objects panel's add buttons: the object browser plus egg/sign shortcuts.</summary>
    private Control BuildUniversalAddButtons()
    {
        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(BuildUniversalMobyLibraryButton("UniversalMobyLibraryButton"));
        Grid shortcuts = NewEqualButtonGrid(2);
        AddModernGridButton(shortcuts, BuildUniversalMobyLibraryButton("UniversalAddEggButton", "Add dragonfly egg", "Collectibles", "Dragonfly Eggs", Color.FromRgb(176, 122, 26)), 0);
        AddModernGridButton(shortcuts, BuildUniversalMobyLibraryButton("UniversalAddSignButton", "Add sign", "Interactive", "Signs", Color.FromRgb(124, 77, 181)), 1);
        panel.Children.Add(shortcuts);
        return panel;
    }

    private List<UniversalMobyPlacement> CurrentUniversalPlacements() => _currentMobys
        .Where(m => m.IsAdded && !m.IsRemoved && m.CrossLevelRequiredExporterFeature == UniversalMobyExport.RequiredExporterFeature)
        .Select(m => new UniversalMobyPlacement(m.CrossLevelSourceLevelKey, m.CrossLevelSourceTrueIndex,
            (int)MathF.Round(m.Position.X * 16), (int)MathF.Round(m.Position.Y * 16), (int)MathF.Round(m.Position.Z * 16),
            SpecialId: UniversalSpecialIdOf(m), Text: IsUniversalSign(m) ? UniversalPlacementReader.SplitSignText(m.SignText) : null))
        .ToList();

    /// <summary>This level's own objects the user removed; removing every copy of a type frees its model.</summary>
    private List<int> CurrentRemovedNativeRows() => _currentMobys
        .Where(m => m.IsRemoved && !m.IsAdded && m.TrueIndex >= 0).Select(m => m.TrueIndex).ToList();

    private static string? UniversalSpecialIdOf(Moby m) =>
        m.CrossLevelTemplateId.StartsWith(UniversalSpecialCatalog.TemplatePrefix, StringComparison.Ordinal)
            ? m.CrossLevelTemplateId[UniversalSpecialCatalog.TemplatePrefix.Length..] : null;

    private static bool IsUniversalSign(Moby m) => UniversalSpecialIdOf(m) == UniversalDragonflyEggs.SignId;

    private static bool IsUniversalEgg(Moby m) => UniversalSpecialIdOf(m) is { } id && UniversalDragonflyEggs.Variants.Any(v => v.Id == id);

    private sealed record UniversalEggColourChoice(string Id, string Name)
    {
        public override string ToString() => Name;
    }

    private static ComboBox BuildUniversalEggColourBox(Moby moby)
    {
        var choices = UniversalDragonflyEggs.Variants.Select(v => new UniversalEggColourChoice(v.Id, v.Name)).ToList();
        return new ComboBox
        {
            Name = "UniversalEggColourBox",
            ItemsSource = choices,
            SelectedItem = choices.FirstOrDefault(c => c.Id == UniversalSpecialIdOf(moby)),
            MinWidth = 260
        };
    }

    /// <summary>Switches a placed egg to another colour variant (a different imported class).</summary>
    internal static void SetUniversalEggColour(Moby moby, string variantId)
    {
        var v = UniversalDragonflyEggs.Variants.FirstOrDefault(x => x.Id == variantId);
        if (v.Id == null || UniversalSpecialIdOf(moby) == variantId) return;
        string oldName = UniversalDragonflyEggs.Variants.FirstOrDefault(x => x.Id == UniversalSpecialIdOf(moby)).Name ?? "";
        moby.CrossLevelTemplateId = UniversalSpecialCatalog.TemplatePrefix + variantId;
        if (moby.Label == oldName) moby.Label = v.Name;
    }

    private static TextBox BuildUniversalSignTextBox(Moby moby) => new()
    {
        Name = "UniversalSignTextBox",
        Text = moby.SignText,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        MinHeight = 76,
        PlaceholderText = "WELCOME TO\nMY LEVEL!"
    };

    /// <summary>Normalises to what the game's HUD letters can show: three capitalised lines.</summary>
    internal static void SetUniversalSignText(Moby moby, string? text) =>
        moby.SignText = string.Join('\n', UniversalPlacementReader.SplitSignText(text).Select(UniversalSpecialBehaviors.NormalizeSignLine)).TrimEnd('\n');

    private (string Path, UniversalJapaneseSource Source)? _universalJapanese;

    /// <summary>The verified Japanese disc used for recovered objects, or null if none is selected.</summary>
    private UniversalJapaneseSource? ResolveUniversalJapaneseSource()
    {
        string path = UniversalJapaneseDiscLocator.Find(_workspace);
        if (_universalJapanese is { } hit && PathsEqual(hit.Path, path)) return hit.Source;
        if (!UniversalJapaneseDiscLocator.IsOriginal(path)) return null;
        var src = new UniversalJapaneseSource(path);
        _universalJapanese = (path, src);
        return src;
    }

    private int PlannedUniversalBytes(UniversalSourceLibrary lib)
    {
        if (_currentLevel == null) return 0;
        int total = 0;
        HashSet<(int, string)> counted = [];
        foreach (var m in _currentMobys.Where(m => m.IsAdded && !m.IsRemoved &&
            m.CrossLevelRequiredExporterFeature == UniversalMobyExport.RequiredExporterFeature))
        {
            var v = UniversalSpecialIdOf(m) is { } sid
                ? UniversalSpecialCatalog.Entries(true).SelectMany(e => e.Variants).FirstOrDefault(x => UniversalSpecialCatalog.SpecialIdForClass(x.ClassId) == sid)
                : UniversalMobyCatalogBuilder.AllVariants(lib, _retailCatalog)
                .FirstOrDefault(x => x.SourceLevelKey == m.CrossLevelSourceLevelKey && x.SourceRow == m.CrossLevelSourceTrueIndex);
            if (v == null) continue;
            var est = UniversalMobyCatalogBuilder.Estimate(lib, _currentLevel, v);
            total += counted.Add((v.ClassId, v.SourceLevelKey)) ? est.EstimatedRamBytes : UniversalLevelData.Align(v.PropertiesBytes, 4) + 8;
        }
        return total;
    }

    private static string EggColourName(string name)
    {
        string c = name.Replace("Dragonfly Egg (", "").TrimEnd(')');
        return c == "native colours" ? "Original colours" : c.Length > 0 ? char.ToUpperInvariant(c[0]) + c[1..] : c;
    }

    private static string FriendlyCategory(string c) => c switch { "Other / Unknown" => "Other", "All Mobys" => "All objects", _ => c };

    private async Task ShowUniversalMobyLibraryAsync(Window owner, string? startCategory = null, string? startSub = null)
    {
        if (TryBlockId65ObjectMutation("Add Moby"))
            return;
        if (_currentLevel == null) { _statusText.Text = "Open a level before adding objects."; return; }
        LevelDefinition destination = _currentLevel;
        Window dialog = new()
        {
            Title = $"Add object — {destination.DisplayName}",
            Width = 1220, Height = 800, MinWidth = 960, MinHeight = 640,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = true
        };
        TextBlock summary = NewSmallNote("Loading every object in the game from your disc… (first time takes a few seconds)");
        summary.Name = "UniversalMobySummary";
        TextBox search = new() { Name = "UniversalMobySearch", PlaceholderText = "Search (for example: sheep, gnorc, egg, sign, Toasty)", MinHeight = 34 };
        ComboBox fitFilter = new() { Name = "UniversalMobyFit", ItemsSource = new[] { "Show everything", "Only what fits here", "Only this level's own objects" }, SelectedIndex = 0, MinWidth = 170 };
        ComboBox sourceFilter = new() { Name = "UniversalMobySourceLevel", MinWidth = 180 };
        ListBox categories = new() { Name = "UniversalMobyCategories", MinWidth = 220 };
        ListBox list = new() { Name = "UniversalMobyList" };
        ComboBox variants = new() { Name = "UniversalMobyVariant", HorizontalAlignment = HorizontalAlignment.Stretch };
        TextBlock title = new() { FontSize = 18, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
        TextBlock details = new() { Name = "UniversalMobyDetails", TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 18 };
        TextBlock variantLabel = new() { Text = "Version", FontSize = 11, Foreground = new SolidColorBrush(ModernMutedInk), TextWrapping = TextWrapping.Wrap };
        TextBlock technical = new() { Name = "UniversalMobyTechnical", TextWrapping = TextWrapping.Wrap, FontSize = 11, LineHeight = 16, Foreground = new SolidColorBrush(ModernMutedInk) };
        TextBlock fit = new() { Name = "UniversalMobyFitText", TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 18, FontWeight = FontWeight.SemiBold };
        Button place = NewButton("Place it (click in the level)"); place.Name = "UniversalMobyPlace"; place.IsEnabled = false;
        StyleModernPrimaryButton(place, ModernGreen);
        Button addHere = NewButton("Add next to selected object"); addHere.Name = "UniversalMobyAddHere"; addHere.IsEnabled = false;
        StyleModernSecondaryButton(addHere);
        Button close = NewButton("Close"); StyleModernSecondaryButton(close);
        Button japaneseDisc = NewButton("Choose Japanese disc…"); japaneseDisc.Name = "UniversalMobyJapaneseDisc"; StyleModernSecondaryButton(japaneseDisc);
        ToolTip.SetTip(japaneseDisc, "Dragonfly eggs and signs are copied from your unchanged original Spyro the Dragon (Japan) disc. It is only read, never modified.");
        UniversalJapaneseSource? japanese = null;
        bool checking = false;
        StackPanel jpRow = new() { Orientation = Orientation.Horizontal, Spacing = 12 };
        jpRow.Children.Add(new TextBlock
        {
            Text = "Dragonfly eggs and signs come from the Japanese version of Spyro. Choose your copy of that disc once to unlock them (it is only read, never changed).",
            TextWrapping = TextWrapping.Wrap, MaxWidth = 760, VerticalAlignment = VerticalAlignment.Center
        });
        jpRow.Children.Add(japaneseDisc);
        Border jpBanner = new()
        {
            Name = "UniversalMobyJapaneseBanner", IsVisible = false, Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromRgb(255, 244, 214)), Child = jpRow
        };
        Func<string, UniversalSpecialAsset?>? special = null;
        close.Click += (_, _) => dialog.Close();

        UniversalSourceLibrary? lib = null;
        IReadOnlyList<UniversalMobyCatalogEntry> entries = [];
        int planned = 0;
        Dictionary<UniversalMobyVariant, UniversalDestinationEstimate> estimates = [];
        System.Collections.Concurrent.ConcurrentDictionary<UniversalMobyVariant, (UniversalComposeResult? Result, string? Error)> dryRuns = new();
        List<UniversalMobyPlacement> existing = CurrentUniversalPlacements();
        List<int> removedRows = CurrentRemovedNativeRows();
        System.Collections.Concurrent.ConcurrentDictionary<UniversalMobyVariant, (UniversalComposeResult? Result, string? Error)> listDry = new();
        UniversalDestinationEstimate Est(UniversalMobyVariant v)
        {
            if (!estimates.TryGetValue(v, out var e)) estimates[v] = e = UniversalMobyCatalogBuilder.Estimate(lib!, destination, v, planned);
            return e;
        }

        UniversalMobyPlacement TrialFor(UniversalMobyVariant v)
        {
            if (UniversalSpecialCatalog.IsSpecial(v))
                return new UniversalMobyPlacement(UniversalSpecialCatalog.SourceKey, v.SourceRow, 0, 0, 0,
                    SpecialId: UniversalSpecialCatalog.SpecialIdForClass(v.ClassId), Text: ["SIGN"]);
            var row = lib!.Get(v.SourceLevelKey).Data.Row(v.SourceRow);
            return new UniversalMobyPlacement(v.SourceLevelKey, v.SourceRow,
                BitConverter.ToInt32(row, 0x0C), BitConverter.ToInt32(row, 0x10), BitConverter.ToInt32(row, 0x14));
        }

        list.ItemTemplate = new FuncDataTemplate<UniversalMobyCatalogEntry>((entry, _) =>
        {
            if (entry == null) return new TextBlock();
            var e = Est(entry.Default);
            bool known = listDry.TryGetValue(entry.Default, out var dr);
            bool fitsNow = e.Native || (known ? dr.Error == null : e.Fits);
            string status = entry.RestrictionReason != null ? (UniversalSpecialCatalog.IsSpecial(entry.Default) ? "Needs your Japanese disc" : "Can't be placed")
                : e.Native ? "Already in this level" : !known ? "Checking whether it fits…" : fitsNow ? "Can be added"
                : known && dr.Error?.Contains("texture", StringComparison.OrdinalIgnoreCase) == true ? "Won't fit: not enough texture space here" : "Won't fit: not enough memory left in this level";
            Color statusColor = entry.RestrictionReason == null && !e.Native && !known ? ModernMutedInk
                : entry.RestrictionReason != null || !fitsNow ? ModernRed : e.Native ? ModernBlue : ModernGreen;
            return new StackPanel
            {
                Margin = new Thickness(6, 4), Spacing = 2,
                Children =
                {
                    new TextBlock { Text = entry.Name, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = $"{FriendlyCategory(entry.Category)}{(entry.SubCategory.Length > 0 ? " › " + entry.SubCategory : "")} · {(UniversalSpecialCatalog.IsSpecial(entry.Default) ? "Japanese version of the game" : "found in " + string.Join(", ", entry.SourceLevels.Take(3).Select(LevelName)) + (entry.SourceLevels.Count > 3 ? $" and {entry.SourceLevels.Count - 3} more" : ""))}",
                        FontSize = 11, Foreground = new SolidColorBrush(ModernMutedInk), TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = status, FontSize = 11, Foreground = new SolidColorBrush(statusColor) }
                }
            };
        });
        categories.ItemTemplate = new FuncDataTemplate<(string, string, int)>((c, _) => new TextBlock
        {
            Text = (c.Item2.Length > 0 ? "    " + c.Item2 : FriendlyCategory(c.Item1)) + $"  ({c.Item3})",
            FontWeight = c.Item2.Length > 0 ? FontWeight.Normal : FontWeight.SemiBold, Margin = new Thickness(4, 3)
        });
        variants.ItemTemplate = new FuncDataTemplate<UniversalMobyVariant>((v, _) => new TextBlock
        {
            Text = v == null ? "" : UniversalSpecialCatalog.IsSpecial(v) ? EggColourName(UniversalSpecialCatalog.DisplayName(v.ClassId)) : VersionName(v)
        });

        string VersionName(UniversalMobyVariant v)
        {
            var same = list.SelectedItem is UniversalMobyCatalogEntry en ? en.Variants.Where(x => x.SourceLevelKey == v.SourceLevelKey).ToList() : [v];
            int n = same.IndexOf(v);
            return same.Count > 1 ? $"From {LevelName(v.SourceLevelKey)} (#{n + 1} of {same.Count})" : $"From {LevelName(v.SourceLevelKey)}";
        }

        string LevelName(string key) => key == UniversalSpecialCatalog.SourceKey ? "Spyro the Dragon (Japan)" : _retailCatalog.FindByKey(key)?.DisplayName ?? key;

        bool MatchesCategory(UniversalMobyCatalogEntry e)
        {
            if (categories.SelectedItem is not (string cat, string sub, int)) return true;
            if (cat == "All Mobys") return true;
            return e.Category == cat && (sub.Length == 0 || e.SubCategory == sub);
        }

        void RefreshList()
        {
            if (lib == null) return;
            string q = (search.Text ?? "").Trim().ToLowerInvariant();
            string? src = sourceFilter.SelectedItem is LevelDefinition ld ? ld.Key : null;
            var shown = entries.Where(e => MatchesCategory(e) &&
                (q.Length == 0 || q.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(e.SearchText.Contains)) &&
                (src == null || e.SourceLevels.Contains(src)) &&
                (fitFilter.SelectedIndex switch { 1 => Est(e.Default).Native || (listDry.TryGetValue(e.Default, out var d) ? d.Error == null : Est(e.Default).Fits), 2 => Est(e.Default).Native, _ => true }))
                .OrderBy(e => UniversalSpecialCatalog.IsSpecial(e.Default) ? 0 : 1).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var selected = list.SelectedItem;
            list.ItemsSource = shown;
            if (selected != null && shown.Contains(selected)) list.SelectedItem = selected;
            summary.Text = $"Showing {shown.Count} of {entries.Count} objects. Pick one, then press Place it." + (checking ? " (Checking which objects fit this level…)" : "");
        }

        void RefreshDetails()
        {
            place.IsEnabled = addHere.IsEnabled = false;
            if (lib == null || list.SelectedItem is not UniversalMobyCatalogEntry entry)
            {
                title.Text = ""; details.Text = "Choose an object from the list to see what it is and whether it fits here."; fit.Text = ""; technical.Text = "";
                variants.ItemsSource = null; variants.Tag = null; variants.IsVisible = variantLabel.IsVisible = false; return;
            }
            title.Text = entry.Name;
            variants.IsVisible = variantLabel.IsVisible = entry.Variants.Count > 1;
            if (!ReferenceEquals(variants.Tag, entry))
            {
                variants.Tag = entry;
                variants.ItemsSource = entry.Variants.Take(200).ToList();
                variants.SelectedItem = entry.Default;
            }
            var v = variants.SelectedItem as UniversalMobyVariant ?? entry.Default;
            var e = Est(v);
            bool isSpecial = UniversalSpecialCatalog.IsSpecial(v);
            variantLabel.Text = entry.SubCategory == "Dragonfly Eggs" ? "Colour" : entry.Variants.Count > 1 ? "Version (copies of this object in the game can differ slightly)" : "Version";
            details.Text = (isSpecial
                    ? "From the Japanese version of Spyro the Dragon.\n"
                    : $"Found in {string.Join(", ", entry.SourceLevels.Select(LevelName))}.\n") +
                (entry.Notes is { Length: > 0 } friendly ? friendly + "\n" : "") +
                (e.Native ? "" : "When you Create BIN, everything it needs (look, sounds and how it behaves) is copied into this level for you.");
            technical.Text =
                $"Object type {entry.ClassHex} · {FriendlyCategory(entry.Category)}{(entry.SubCategory.Length > 0 ? " › " + entry.SubCategory : "")}\n" +
                (UniversalSpecialCatalog.IsSpecial(v)
                    ? $"Source: {LevelName(v.SourceLevelKey)} (model copied from the Japanese disc; behavior written natively for the USA game)\n"
                    : $"Source: {LevelName(v.SourceLevelKey)} row T{v.SourceRow} ({entry.Variants.Count} native placement(s) across {entry.SourceLevels.Count} level(s))\n") +
                $"Model: {(v.SharedModel ? "shared game-wide model" : v.HasLocalModel ? $"{v.ModelBytes / 1024.0:0.0} KB level model with textures and sounds" : "no visible model (controller/trigger)")}\n" +
                $"Behavior: {(UniversalSpecialCatalog.IsSpecial(v) ? $"native USA-engine routine added by the editor ({v.BehaviorBytes / 1024.0:0.0} KB)" : v.HasBehavior ? $"native code from {LevelName(v.SourceLevelKey)} ({v.BehaviorBytes / 1024.0:0.0} KB)" : "engine default")}\n" +
                $"Variants: {entry.Variants.Count} placement(s) across {entry.SourceLevels.Count} level(s)";
            if (entry.RestrictionReason != null) { fit.Text = entry.RestrictionReason; fit.Foreground = new SolidColorBrush(ModernRed); return; }
            var trial = existing.Append(TrialFor(v)).ToList();
            if (!dryRuns.TryGetValue(v, out var dry)) dryRuns[v] = dry = UniversalMobyCatalogBuilder.DryRun(lib, destination, trial, special, removedRows);
            if (dry.Error != null)
            {
                fit.Text = (dry.Error.Contains("texture", StringComparison.OrdinalIgnoreCase)
                    ? $"This won't fit in {destination.DisplayName}: the level has no room left for its textures."
                    : dry.Error.Contains("RAM", StringComparison.OrdinalIgnoreCase) || dry.Error.Contains("memory", StringComparison.OrdinalIgnoreCase)
                    ? $"This won't fit in {destination.DisplayName}: the level doesn't have enough memory left."
                    : $"This can't be added to {destination.DisplayName} right now.") +
                    "\nTip: removing every copy of one of this level's own objects (for example all of one enemy type) frees the memory its model uses." +
                    "\n" + dry.Error;
                fit.Foreground = new SolidColorBrush(ModernRed);
                return;
            }
            var ram = dry.Result!.Ram;
            fit.Text = e.Native ? $"✓ Already part of {destination.DisplayName} — ready to place." : $"✓ Fits in {destination.DisplayName} — ready to place." +
                (dry.Result.Classes.Count(c => c.DependencyOnly) is int deps and > 0 ? $" It brings {deps} small helper object(s) it needs along with it." : "");
            technical.Text += $"\nGame RAM left after import: {ram.FinalHeadroom / 1024.0:0.0} KB";
            fit.Foreground = new SolidColorBrush(ModernGreen);
            place.IsEnabled = addHere.IsEnabled = true;
        }

        async Task AddAsync(bool clickToPlace)
        {
            if (lib == null || list.SelectedItem is not UniversalMobyCatalogEntry entry) return;
            var v = variants.SelectedItem as UniversalMobyVariant ?? entry.Default;
            dialog.Close();
            await AddUniversalMobyAsync(lib, entry, v, clickToPlace, japanese);
        }
        place.Click += async (_, _) => await AddAsync(true);
        addHere.Click += async (_, _) => await AddAsync(false);
        list.DoubleTapped += async (_, _) => { if (place.IsEnabled) await AddAsync(true); };
        list.SelectionChanged += (_, _) => RefreshDetails();
        variants.SelectionChanged += (_, _) => RefreshDetails();
        categories.SelectionChanged += (_, _) => RefreshList();
        search.TextChanged += (_, _) => RefreshList();
        fitFilter.SelectionChanged += (_, _) => RefreshList();
        sourceFilter.SelectionChanged += (_, _) => RefreshList();

        Grid top = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 8 };
        top.Children.Add(search);
        Grid.SetColumn(fitFilter, 1); top.Children.Add(fitFilter);
        Grid.SetColumn(sourceFilter, 2); top.Children.Add(sourceFilter);
        StackPanel right = new() { Spacing = 10, Margin = new Thickness(12, 0, 0, 0) };
        right.Children.Add(title);
        right.Children.Add(variantLabel);
        right.Children.Add(variants);
        right.Children.Add(details);
        right.Children.Add(fit);
        right.Children.Add(new Expander { Header = "Technical details", IsExpanded = false, Content = technical, HorizontalAlignment = HorizontalAlignment.Stretch });
        WrapPanel buttons = new() { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 8 };
        buttons.Children.Add(place); buttons.Children.Add(addHere); buttons.Children.Add(close);
        right.Children.Add(buttons);
        Grid body = new() { ColumnDefinitions = new ColumnDefinitions("240,*,380"), ColumnSpacing = 10 };
        body.Children.Add(new ScrollViewer { Content = categories });
        ScrollViewer listScroll = new() { Content = list }; Grid.SetColumn(listScroll, 1); body.Children.Add(listScroll);
        ScrollViewer rightScroll = new() { Content = right }; Grid.SetColumn(rightScroll, 2); body.Children.Add(rightScroll);
        DockPanel root = new() { Margin = new Thickness(14) };
        StackPanel header = new() { Spacing = 8, Margin = new Thickness(0, 0, 0, 10) };
        header.Children.Add(new TextBlock { Text = $"Add any Spyro 1 object to {destination.DisplayName}", FontSize = 20, FontWeight = FontWeight.SemiBold });
        header.Children.Add(top); header.Children.Add(summary); header.Children.Add(jpBanner);
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header); root.Children.Add(body);
        dialog.Content = root;
        dialog.Background = new SolidColorBrush(ModernSurface);

        japaneseDisc.Click += async (_, _) =>
        {
            var files = await dialog.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select the original Spyro the Dragon (Japan) disc image (.bin)",
                AllowMultiple = false
            });
            string? picked = files.FirstOrDefault()?.Path.LocalPath;
            if (picked == null) return;
            try
            {
                string bin = DiscImageLocator.ResolveImagePath(picked);
                await Task.Run(() => UniversalJapaneseDiscLocator.Configure(_workspace, bin));
                japanese = await Task.Run(ResolveUniversalJapaneseSource);
                special = UniversalMobyExport.SpecialProvider(japanese);
                var specials = UniversalSpecialCatalog.Entries(japanese != null);
                entries = [.. entries.Where(e => e.Default.SourceLevelKey != UniversalSpecialCatalog.SourceKey), .. specials];
                dryRuns.Clear();
                foreach (var e in specials) listDry.TryRemove(e.Default, out _);
                if (lib != null)
                    foreach (var e in specials.Where(e => e.RestrictionReason == null))
                        listDry[e.Default] = (null, UniversalMobyCatalogBuilder.DryRun(lib, destination, existing.Append(TrialFor(e.Default)).ToList(), special, removedRows).Error);
                RefreshList(); RefreshDetails();
                summary.Text = "Japanese disc found — dragonfly eggs and signs are ready to use.";
                jpBanner.IsVisible = false;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                summary.Text = ex.Message;
                summary.Foreground = new SolidColorBrush(ModernRed);
            }
        };

        dialog.Opened += async (_, _) =>
        {
            try
            {
                var (_, l, usa) = await EnsureUniversalCatalogAsync();
                japanese = await Task.Run(ResolveUniversalJapaneseSource);
                special = UniversalMobyExport.SpecialProvider(japanese);
                var es = entries = [.. usa, .. UniversalSpecialCatalog.Entries(japanese != null)];
                lib = l;
                planned = await Task.Run(() => PlannedUniversalBytes(l));
                await Task.Run(() => { foreach (var e in es) Est(e.Default); });
                var cats = UniversalCategoryOrder.Select(c => (c.Category, c.Sub,
                    c.Category == "All Mobys" ? es.Count : es.Count(e => e.Category == c.Category && (c.Sub.Length == 0 || e.SubCategory == c.Sub))))
                    .Where(c => c.Item3 > 0 || c.Category == "All Mobys").ToList();
                categories.ItemsSource = cats;
                int startIndex = startCategory == null ? 0 : cats.FindIndex(c => c.Category == startCategory && c.Sub == (startSub ?? ""));
                categories.SelectedIndex = Math.Max(0, startIndex);
                jpBanner.IsVisible = japanese == null;
                sourceFilter.ItemsSource = new object[] { "Found in any level" }.Concat(_retailCatalog.Levels.Where(x => x.SourceWadEntry > 0)).ToList();
                sourceFilter.SelectedIndex = 0;
                RefreshList();
                if (startSub != null && list.ItemsSource is IEnumerable<UniversalMobyCatalogEntry> firstShown && firstShown.FirstOrDefault() is { } first)
                    list.SelectedItem = first;
                RefreshDetails();
                checking = true;
                RefreshList();
                await Task.Run(() => Parallel.ForEach(es.Where(x => x.RestrictionReason == null && !Est(x.Default).Native),
                    new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2) }, x =>
                    {
                        var trial = existing.Append(TrialFor(x.Default)).ToList();
                        var r = UniversalMobyCatalogBuilder.DryRun(l, destination, trial, special, removedRows);
                        listDry[x.Default] = (null, r.Error);
                        if (r.Error == null) dryRuns[x.Default] = r;
                    }));
                checking = false;
                RefreshList();
            }
            catch (Exception ex)
            {
                summary.Text = ex.Message;
                summary.Foreground = new SolidColorBrush(ModernRed);
            }
        };
        await dialog.ShowDialog(owner);
    }

    private async Task AddUniversalMobyAsync(UniversalSourceLibrary lib, UniversalMobyCatalogEntry entry, UniversalMobyVariant v, bool clickToPlace,
        UniversalJapaneseSource? japanese = null)
    {
        if (_currentLevel == null) return;
        string? specialId = UniversalSpecialCatalog.IsSpecial(v) ? UniversalSpecialCatalog.SpecialIdForClass(v.ClassId) : null;
        UniversalSpecialAsset? asset = specialId != null ? japanese?.Find(specialId) : null;
        if (specialId != null && asset == null) { _statusText.Text = "Select the original Japanese disc first (Japanese disc… in the Add Moby browser)."; return; }
        byte[] row = asset?.RowTemplate ?? lib.Get(v.SourceLevelKey).Data.Row(v.SourceRow);
        string sourceName = asset != null ? "Spyro the Dragon (Japan)" : _retailCatalog.FindByKey(v.SourceLevelKey)?.DisplayName ?? v.SourceLevelKey;
        string name = asset?.Name ?? entry.Name;
        var template = new AddMobyTemplate(
            Name: name,
            Type: row[0x50], State: row[0x51],
            SourceByte36: row[0x36], SourceByte37: row[0x37], SourceByte4F: row[0x4F],
            Flag4A: row[0x52], Flag4B: row[0x53],
            UsesGem: false,
            DefaultLabel: name,
            FromCrossLevelTemplate: true,
            DefaultColor: Moby.ColorForType(row[0x50]),
            TemplateId: specialId != null ? UniversalSpecialCatalog.TemplatePrefix + specialId : $"universal:{v.SourceLevelKey}:{v.SourceRow}",
            Family: "universal",
            SourceLevelKey: v.SourceLevelKey,
            SourceLevelName: sourceName,
            SourceTrueIndex: v.SourceRow,
            AddSupportStatus: UniversalMobyExport.RequiredExporterFeature,
            RequiredExporterFeature: UniversalMobyExport.RequiredExporterFeature,
            TemplateNote: $"Universal import of {name} from {sourceName}.",
            CandidateKind: entry.Category,
            CurrentLevelSupportLabel: "importable",
            CurrentLevelReady: true,
            CurrentLevelPlaceable: true,
            YawByte: row[0x46],
            SignText: specialId == UniversalDragonflyEggs.SignId ? "HELLO!" : "");
        Vector3f fallback = BuildAddMobyPlacementOptions()[0].Position;
        MobyPlacementOption option = clickToPlace
            ? new("Click in the 3D view", fallback, "Click the terrain to place the object.", SnapOnAdd: true, PlaceAfterDialog: true)
            : new("Near selection", fallback, "Placed next to the current selection.", SnapOnAdd: true, PlaceAfterDialog: false);
        PendingMobyAdd pending = new(template, name, template.Type, template.State, template.YawByte,
            template.SourceByte36, template.SourceByte4F, template.Flag4B, GemValue.Red, option, [template]);
        if (clickToPlace) { BeginPendingMobyPlacement(pending, fallback); return; }
        Vector3f position = ApplyTerrainPlacementLift(SnapOrNearestTerrain(fallback), pending);
        await AddPendingMobyAtPositionAsync(pending, position);
    }
}

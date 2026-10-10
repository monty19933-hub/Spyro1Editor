using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Crash;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Editing;
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
    private string? _universalCatalogLoadPath;

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

    private Dictionary<(string, int), (string, string)> _universalSourceNames = [];

    private string UniversalSourceObjectName(UniversalMobyCatalogEntry entry, UniversalMobyVariant variant)
    {
        if (UniversalSpecialCatalog.IsSpecial(variant)) return entry.Name;
        if (variant.SourceLevelKey == "alpineridge" && variant.ClassId == 0x10F) return "Alpine Ridge source object (identity unverified)";
        if (_universalSourceNames.TryGetValue((variant.SourceLevelKey, variant.SourceRow), out var identity)) return identity.Item1;
        if (variant.SourceLevelKey == "wizardpeak" && variant.ClassId == 0x11C) return "Armored Druid (Wizard Peak; roster identity)";
        if (variant.SourceLevelKey == "magiccrafters" && variant.ClassId == 0x10F) return "Armored Druid (Magic Crafters; roster identity)";
        return $"Source object 0x{variant.ClassId:X3}";
    }

    private string ResolveUniversalSourceDisc() => FirstExistingDiscImagePath(_discImagePathBox.Text,
        _skyboxDiscImagePathBox.Text, DiscImageLocator.FindImage(_workspace));

    private Task<(string, UniversalSourceLibrary, IReadOnlyList<UniversalMobyCatalogEntry>)> EnsureUniversalCatalogAsync()
    {
        string path = ResolveUniversalSourceDisc();
        if (_universalCatalog is { } ready && PathsEqual(ready.Path, path))
            return Task.FromResult((ready.Path, ready.Library, ready.Entries));
        if (_universalCatalogLoad != null && !_universalCatalogLoad.IsFaulted && PathsEqual(_universalCatalogLoadPath ?? "", path)) return _universalCatalogLoad;
        _universalCatalogLoadPath = path;
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
            _universalSourceNames = names;
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
            "Dragonfly Eggs" => "Place a collectible dragonfly egg. Choose its color in the next window.",
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

    private List<UniversalMobyPlacement> CurrentUniversalPlacements()
    {
        if (_currentLevel == null) return [];
        string path = Path.Combine(_workspace.RootPath, $"{_currentLevel.Key}-native-edits.json");
        var ledger = _freshSceneDocument?.ReconcileLedger(_currentMobys) ?? UniversalPlacementLedger.Reconcile(path, _currentMobys);
        return PlacementsFor(ledger,_currentMobys);
    }

    private static List<UniversalMobyPlacement> PlacementsFor(UniversalPlacementLedger ledger,IReadOnlyList<Moby> mobys)
    {
        var active = mobys.Where(m => m.IsAdded && !m.IsRemoved &&
                m.CrossLevelRequiredExporterFeature == UniversalMobyExport.RequiredExporterFeature)
            .ToDictionary(m => m.UniversalPlacementId, StringComparer.Ordinal);
        return ledger.Entries.Select(identity => active.TryGetValue(identity.Id, out var m)
            ? new UniversalMobyPlacement(m.CrossLevelSourceLevelKey, m.CrossLevelSourceTrueIndex,
                (int)MathF.Round(m.Position.X * 16), (int)MathF.Round(m.Position.Y * 16), (int)MathF.Round(m.Position.Z * 16),
                Yaw:m.YawByte>=0?m.YawByte:null,
                RowByteOverrides:new Dictionary<int,byte>{{0x50,checked((byte)m.Type)},{0x51,checked((byte)m.State)}},
                EditorId: identity.Id, PersistentSlot: identity.Slot,
                SpecialId: UniversalSpecialIdOf(m), Text: IsUniversalSign(m) ? UniversalPlacementReader.SplitSignText(m.SignText) : null) { LinkGroupId = m.UniversalLinkGroupId, DragonColor = m.DragonColor }
            : new UniversalMobyPlacement("", -1, 0, 0, 0, EditorId: identity.Id, PersistentSlot: identity.Slot, Retired: true)).ToList();
    }

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
            MinWidth = 260,
            ItemTemplate = new FuncDataTemplate<UniversalEggColourChoice>((choice, _) =>
                BuildEggColourChoice(choice?.Id ?? "", choice?.Name ?? ""))
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

    private (string Path, CrashWarpedDisc Disc)? _universalCrash;

    /// <summary>The user's Crash Bandicoot: Warped disc (box art and sounds), or null if none is selected.</summary>
    private CrashWarpedDisc? ResolveUniversalCrashDisc()
    {
        string path = CrashWarpedDiscLocator.Find(_workspace);
        if (_universalCrash is { } hit && PathsEqual(hit.Path, path)) return hit.Disc;
        if (!CrashWarpedDiscLocator.IsUsable(path)) return null;
        var disc = new CrashWarpedDisc(path);
        _universalCrash = (path, disc);
        return disc;
    }

    /// <summary>Objects that do not come from a USA row: Japanese eggs and signs, editor gems and Crash boxes.</summary>
    private Func<string, UniversalSpecialAsset?>? UniversalSpecials(UniversalJapaneseSource? japanese, UniversalSourceLibrary? library) =>
        UniversalMobyExport.SpecialProvider(japanese, library, ResolveUniversalCrashDisc(), ResolveYearOfTheDragonDisc());

    private static bool IsCrashVariant(UniversalMobyVariant v) => v.SourceLevelKey == UniversalCrashBoxes.SourceKey;
    private static bool IsYotdVariant(UniversalMobyVariant v) => v.SourceLevelKey == UniversalCrashEnemies.YotdSourceKey;

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
            var estimatedBytes = _freshSceneDocument == null
                ? UniversalMobyCatalogBuilder.Estimate(lib, _currentLevel, v).EstimatedRamBytes
                : v.ModelBytes + v.BehaviorBytes + UniversalLevelData.Align(v.PropertiesBytes,4) + 112;
            total += counted.Add((v.ClassId, v.SourceLevelKey)) ? estimatedBytes : UniversalLevelData.Align(v.PropertiesBytes, 4) + 8;
        }
        return total;
    }

    private static string EggColourName(string name)
    {
        string c = name.Replace("Dragonfly Egg (", "").TrimEnd(')');
        return c == "native colours" ? "Original colors" : c.Length > 0 ? char.ToUpperInvariant(c[0]) + c[1..] : c;
    }

    private static string UniversalFitFailureSummary(string error, string levelName, bool compact)
    {
        bool textureCapacity = error.Contains("texture", StringComparison.OrdinalIgnoreCase) &&
            (error.Contains("space", StringComparison.OrdinalIgnoreCase) || error.Contains("memory", StringComparison.OrdinalIgnoreCase) || error.Contains("allocate", StringComparison.OrdinalIgnoreCase) || error.Contains("fit", StringComparison.OrdinalIgnoreCase));
        bool memory = error.Contains("RAM", StringComparison.OrdinalIgnoreCase) || error.Contains("memory", StringComparison.OrdinalIgnoreCase);
        if (textureCapacity) return compact ? "Won't fit: not enough texture space" : $"This won't fit in {levelName}: there is not enough texture space.";
        if (memory) return compact ? "Won't fit: not enough game memory" : $"This won't fit in {levelName}: there is not enough game memory.";
        if (error.Contains("collid", StringComparison.OrdinalIgnoreCase) || error.Contains("conflict", StringComparison.OrdinalIgnoreCase))
            return compact ? "Can't be added: source variant conflict" : $"This source version conflicts with an object already in {levelName}.";
        return compact ? "Can't be added here: see details" : $"This object can't be added to {levelName} with the current placements. See Technical details.";
    }

    private static string FriendlyCategory(string c) => c switch { "Other / Unknown" => "Other", "All Mobys" => "All objects", _ => c };

    private async Task<(UniversalCompositionHost Host,NativeTerrainTexturePrivateRecordBatchRequest? PrivateRequest)> CaptureUniversalRetailHostAsync(UniversalSourceLibrary library, LevelDefinition level,
        IReadOnlyList<UniversalMobyPlacement> placements, IReadOnlyCollection<int> removed, Func<string,UniversalSpecialAsset?>? special,
        bool ignoreTerrainShapeEdits = false)
    {
        // The modal prevents scene edits while this snapshot is captured. All derived files stay
        // in a disposable directory; fit checks never save/overwrite the user's edit manifests.
        string scratch = Path.Combine(Path.GetTempPath(), "spyro-universal-fit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            string edits = Path.Combine(scratch,"terrain-edits.json");
            var geometry = _currentGeometry;
            string savedEdits = Path.Combine(_workspace.RootPath,$"{level.Key}-terrain-edits.json");
            // When a saved terrain change cannot be exported, the browser still has to work: space is then
            // measured against the level without terrain shape changes and the caller explains that.
            if (ignoreTerrainShapeEdits) await TerrainEditStore.SaveAsync(edits,Array.Empty<TerrainPolygon>(),level.DisplayName);
            else if (geometry != null) await TerrainEditStore.SaveAsync(edits,geometry.Polygons,level.DisplayName);
            else if (File.Exists(savedEdits)) File.Copy(savedEdits,edits);
            string custom = "", customSource = CustomTerrainTextureStore.ManifestPath(_workspace.RootPath,level.Key);
            var imports = _customTerrainTextures.Count > 0 ? _customTerrainTextures.ToArray()
                : File.Exists(customSource) ? CustomTerrainTextureStore.LoadManifest(customSource).ToArray() : [];
            if (geometry != null)
            {
                var live = geometry.Polygons.Where(p => !p.IsTerrainRemoved).Select(p => p.TextureId).ToHashSet();
                imports = imports.Where(i => live.Contains(i.TextureId)).ToArray();
            }
            if (imports.Length > 0)
            {
                string directory = Path.GetDirectoryName(customSource) ?? _workspace.RootPath;
                imports = imports.Select(i => i with { SourceImagePath = Path.IsPathRooted(i.SourceImagePath)
                    ? i.SourceImagePath : Path.GetFullPath(Path.Combine(directory,i.SourceImagePath)) }).ToArray();
                custom = Path.Combine(scratch,"custom-textures.json");
                await CustomTerrainTextureStore.SaveManifestAsync(custom,level.Key,level.DisplayName,imports);
            }
            string relocation = NativeTerrainTextureRelocationEditStore.ManifestPath(_workspace.RootPath,level.Key);
            string ram = TerrainPatchDataLocator.FindRamDump(_workspace,level.Key);
            string search = TerrainPatchDataLocator.FindSourceSearch(_workspace,level.Key);
            if (geometry != null)
            {
                string generated = Path.Combine(scratch,"source-search.json");
                if (File.Exists(ram)) search = (await TerrainSourceSearchBuilder.BuildAsync(new(library.ImagePath,generated,level,geometry,ram))).OutputPath;
                else if (IsSourceDerivedGeometry(geometry)) search = (await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(new(library.ImagePath,generated,level,geometry))).OutputPath;
            }
            var saved = NativeTerrainTextureRelocationEditStore.Load(_workspace.RootPath,level.Key);
            bool appended = saved.Any(e => e.UsesAppendedPrivateRecord);
            var plan = await Task.Run(() => TerrainPatchExporter.BuildPlan(library.ImagePath,DiscImageLocator.FindCueForImage(library.ImagePath),
                "","",level,ram,search,edits,custom,appended ? "" : relocation));
            if (!appended) return (await Task.Run(() => UniversalCompositionHost.FromTerrainPlan(library.ImagePath,level,plan)),null);
            plan = TerrainPatchExporter.AddPrivateTextureAppearanceDependencies(plan,level,saved);
            var binding = await Task.Run(() => NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(library.ImagePath,level));
            var reservations = await Task.Run(() => UniversalTerrainReservations.Capture(library,level,placements,special,removed));
            var request = new NativeTerrainTexturePrivateRecordBatchRequest(library.ImagePath,DiscImageLocator.FindCueForImage(library.ImagePath),
                Path.Combine(scratch,"private-host"),WadAnalysisLocator.Find(_workspace),level,saved,
                plan.NativeLevelReplacement!=null ? [] : NativeTerrainTexturePrivateRecordBatchCompiler.ConvertOrdinaryTerrainPlan(plan),
                HasCustomImageTexturePatches:plan.CustomTextureBytePatchCount>0,
                StructuralGrowthPolicy:AppendedPrivateTerrainTextureResearchGate.ResolveStructuralGrowthPolicy(binding,saved.Count(e=>e.UsesAppendedPrivateRecord)))
                { AdditionalProtectedRanges = reservations, TerrainPreparation=plan.NativeLevelReplacement!=null ? plan : null };
            return (await BuildUniversalPrivateFitHostAsync(request),request);
        }
        finally { Directory.Delete(scratch,recursive:true); }
    }

    private static async Task<UniversalCompositionHost> BuildUniversalPrivateFitHostAsync(NativeTerrainTexturePrivateRecordBatchRequest request)
    {
        string scratch = Path.Combine(Path.GetTempPath(),"spyro-universal-fit-growth-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            var provenance=await NativeTerrainTexturePreparedSourceCompiler.PrepareAsync(request,Path.Combine(scratch,"terrain"));
            var prepared=provenance.Request;
            var plan=await Task.Run(()=>NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(prepared));
            if(!AppendedPrivateTerrainTextureResearchGate.TryAllow(provenance.AuthorizationBinding??plan.SourceBinding,plan.WriterKind,plan.AppendedPrivateEdits.Count,plan.StructuralGrowthPolicy,out var reason))
                throw new InvalidDataException(reason);
            if(plan.FixedTailPlan!=null)
                return UniversalCompositionHost.FromPrivateTerrainPlan(request.SourceImagePath,request.TargetLevel,plan,
                    prepared.SourceImagePath==request.SourceImagePath ? null : prepared.SourceImagePath);
            var built = await NativeTerrainTexturePrivateRecordBatchCompiler.ExportAsync(plan with { OutputPrefix = Path.Combine(scratch,"host") });
            return UniversalCompositionHost.FromPreparedImage(request.SourceImagePath,built.OutputImagePath,request.TargetLevel);
        }
        finally { Directory.Delete(scratch,recursive:true); }
    }

    private async Task<IReadOnlyList<NativeTexturePageOwnedRange>> CapturePrivateTerrainActorReservationsAsync(LevelDefinition level)
    {
        string path = Path.Combine(_workspace.RootPath,$"{level.Key}-native-edits.json");
        var placements = level == _currentLevel ? CurrentUniversalPlacements() : UniversalPlacementReader.Read(path);
        if (!placements.Any(p=>!p.Retired)) return [];
        var removed = level == _currentLevel ? CurrentRemovedNativeRows() : UniversalPlacementReader.ReadRemovedNativeRows(path);
        var (_,library,_) = await EnsureUniversalCatalogAsync();
        var special = UniversalSpecials(ResolveUniversalJapaneseSource(), library);
        return await Task.Run(() => UniversalTerrainReservations.Capture(library,level,placements,special,removed));
    }

    private sealed record UniversalFitChoice(UniversalMobyVariant Variant, UniversalRemovalPlan? Plan);

    private void ShowUniversalRemovalPlan(UniversalFitChoice choice, string objectName)
    {
        if (choice.Plan == null) return;
        var rows = choice.Plan.AdditionalRows.ToHashSet();
        var objects = _currentMobys.Where(m => !m.IsAdded && !m.IsRemoved && rows.Contains(m.TrueIndex)).OrderBy(m => m.TrueIndex).ToList();
        Window review = new() { Title = $"Inspect removal plan — {objectName}", Width = 640, Height = 460,
            WindowStartupLocation = WindowStartupLocation.CenterOwner };
        ListBox objectsList = new() { Name = "UniversalRemovalObjects", ItemsSource = objects,
            ItemTemplate = new FuncDataTemplate<Moby>((m, _) => new TextBlock { Text = m == null ? "" : $"T{m.TrueIndex}: {m.DisplayLabel}", Margin = new Thickness(6) }) };
        Button locate = NewButton("Go to object"); locate.Name = "UniversalRemovalLocate";
        Button remove = NewButton("Remove object"); remove.Name = "UniversalRemovalRemove";
        StyleModernSecondaryButton(locate); StyleModernSecondaryButton(remove);
        TextBlock status = NewSmallNote(""); status.Name = "UniversalRemovalStatus";
        LevelDefinition? inspectedLevel = _currentLevel;
        bool CurrentScene() => ReferenceEquals(_currentLevel, inspectedLevel) && objects.All(_currentMobys.Contains);
        bool CanSelect() => CurrentScene() && objectsList.SelectedItem is Moby { IsRemoved: false } m && objects.Contains(m);
        void UpdateButtons()
        {
            locate.IsEnabled = remove.IsEnabled = CanSelect();
        }
        void RefreshObjects()
        {
            var selected = objectsList.SelectedItem as Moby;
            objectsList.ItemsSource = CurrentScene() ? objects.Where(m => !m.IsRemoved).ToList() : [];
            objectsList.SelectedItem = selected is { IsRemoved: false } && CurrentScene() ? selected : null;
            UpdateButtons();
            if (!CurrentScene()) status.Text = "The level has changed. Reopen Add object to check this scene.";
            else if (objects.All(m => m.IsRemoved)) status.Text = "All suggested objects are removed. Reopen Add object to check the available space.";
        }
        void SelectObject(bool focus)
        {
            if (!CanSelect()) { RefreshObjects(); return; }
            _mobySearchBox.Text = "";
            SelectMobyCategory("All objects");
            _viewport.SelectMoby((Moby)objectsList.SelectedItem!, focus);
        }
        locate.Click += (_, _) => SelectObject(focus: true);
        objectsList.DoubleTapped += (_, args) =>
        {
            args.Handled = true;
            SelectObject(focus: true);
        };
        objectsList.SelectionChanged += (_, _) => UpdateButtons();
        remove.Click += (_, _) =>
        {
            if (!CanSelect()) { RefreshObjects(); return; }
            var selected = (Moby)objectsList.SelectedItem!;
            SelectObject(focus: false);
            if (!ReferenceEquals(_selectedMoby, selected)) return;
            RemoveSelectedMoby();
            status.Text = selected.IsRemoved
                ? $"Removed {selected.DisplayLabel}. Use Undo last removal in the editor's Recovery section to restore it."
                : _statusText.Text;
            RefreshObjects();
        };
        // This inspector is modeless: reflect removals/undo in the editor and reject stale scenes.
        review.Activated += (_, _) => RefreshObjects();
        DockPanel panel = new() { Margin = new Thickness(16) };
        var actions = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 8 };
        actions.Children.Add(locate); actions.Children.Add(remove);
        var header = new StackPanel { Spacing = 8, Children =
        {
            NewSmallNote($"This checked plan lets the {_retailCatalog.FindByKey(choice.Variant.SourceLevelKey)?.DisplayName ?? choice.Variant.SourceLevelKey} version fit. Double-click an object to go to it, or select it and choose Remove object. Removals can be undone in the editor's Recovery section.\nReopen Add object afterwards to recheck the current scene."),
            NewSmallNote(string.Join("\n", choice.Plan.Groups.Select(g => $"{g.Name}: {g.Effect}"))), actions, status
        }};
        DockPanel.SetDock(header, Dock.Top); panel.Children.Add(header); panel.Children.Add(objectsList);
        review.Content = panel; review.Show(this);
        objectsList.SelectedIndex = objects.Count > 0 ? 0 : -1;
        UpdateButtons();
    }

    private async Task ShowUniversalMobyLibraryAsync(Window owner, string? startCategory = null, string? startSub = null)
    {
        if (TryBlockId65ObjectMutation("Add Moby"))
            return;
        if (_currentLevel == null) { _statusText.Text = "Open a level before adding objects."; return; }
        LevelDefinition destination = _currentLevel;
        Window dialog = new()
        {
            Title = $"Add object — {destination.DisplayName}",
            Width = 1080, Height = 760, MinWidth = 900, MinHeight = 620,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = true
        };
        TextBlock summary = NewSmallNote("Loading the game-wide object catalog from your disc… (first time takes a few seconds)");
        summary.Name = "UniversalMobySummary";
        TextBlock terrainNotice = new()
        {
            Name = "UniversalMobyTerrainNotice", IsVisible = false, TextWrapping = TextWrapping.Wrap, FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(110, 32, 32)), Margin = new Thickness(0, 2, 0, 2)
        };
        TextBox search = new() { Name = "UniversalMobySearch", PlaceholderText = "Search by object name, level or type…", MinHeight = 34 };
        ComboBox fitFilter = new() { Name = "UniversalMobyFit", ItemsSource = new[] { "Show everything", "Only what fits here", "Only this level's own objects" }, SelectedIndex = 0, MinWidth = 170 };
        ComboBox sourceFilter = new() { Name = "UniversalMobySourceLevel", MinWidth = 180 };
        ListBox categories = new() { Name = "UniversalMobyCategories", MinWidth = 150 };
        ListBox list = new() { Name = "UniversalMobyList" };
        ComboBox variants = new() { Name = "UniversalMobyVariant", HorizontalAlignment = HorizontalAlignment.Stretch };
        TextBlock empty = new() { Name = "UniversalMobyEmpty", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16), IsVisible = false, VerticalAlignment = VerticalAlignment.Center };
        Button clearSearch = NewButton("Clear search"); clearSearch.Name = "UniversalMobyClearSearch";
        StyleModernSecondaryButton(clearSearch); clearSearch.IsVisible = false;
        clearSearch.Click += (_, _) => search.Text = "";
        Border modelPreview = new() { Name = "UniversalMobyPreviewHost" };
        UniversalMobyVariant? previewVariant = null;
        TextBlock title = new() { FontSize = 18, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
        TextBlock details = new() { Name = "UniversalMobyDetails", TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 18 };
        TextBlock variantLabel = new() { Text = "Source version", FontSize = 11, Foreground = new SolidColorBrush(ModernMutedInk), TextWrapping = TextWrapping.Wrap };
        TextBlock technical = new() { Name = "UniversalMobyTechnical", TextWrapping = TextWrapping.Wrap, FontSize = 11, LineHeight = 16, Foreground = new SolidColorBrush(ModernMutedInk) };
        TextBlock fit = new() { Name = "UniversalMobyFitText", TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 18, FontWeight = FontWeight.SemiBold };
        // A Dream Weavers fool can join a Resize cannon's group: the cannon (once Spyro has defeated its gunner) can then shoot it.
        string? cannonGroup = UniversalCannonGroupForNewFool();
        bool levelHasCannon = UniversalCannonGroups().Count > 0;
        TextBlock joinCannonText = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        CheckBox joinCannon = new() { Name = "UniversalMobyJoinCannon", IsVisible = false, Content = joinCannonText,
            IsChecked = cannonGroup != null && _selectedMoby?.UniversalLinkGroupId == cannonGroup };
        ToolTip.SetTip(joinCannon, "The fool joins the Resize cannon's linked group. It still moves and is removed on its own. A fool that is not in a cannon's group never calls a cannon.");
        Button place = NewButton("Place in level"); place.Name = "UniversalMobyPlace"; place.IsEnabled = false;
        StyleModernPrimaryButton(place, ModernGreen);
        Button addHere = NewButton("Add near selection"); addHere.Name = "UniversalMobyAddHere"; addHere.IsEnabled = false;
        StyleModernSecondaryButton(addHere);
        Button findRoom = NewButton("Find space for this object…"); findRoom.Name = "UniversalMobyFindRoom";
        StyleModernSecondaryButton(findRoom); findRoom.IsEnabled = false;
        TextBlock roomExplanation = new() { Name = "UniversalMobyRoomExplanation", FontSize = 12, TextWrapping = TextWrapping.Wrap };
        ListBox roomPlans = new() { Name = "UniversalMobyRemovalPlans", MaxHeight = 190, IsVisible = false };
        Button inspectPlan = NewButton("Inspect suggested objects…"); inspectPlan.Name = "UniversalMobyInspectPlan";
        StyleModernSecondaryButton(inspectPlan); inspectPlan.IsVisible = false;
        TextBlock magicRoom = new() { Name = "UniversalMagicArtisansAdvice", IsVisible = false, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        Button magicPair = NewButton("Use the supported druid pair"); magicPair.Name = "UniversalMagicArtisansPair";
        StyleModernSecondaryButton(magicPair); magicPair.IsVisible = false;
        magicPair.Click += (_, _) =>
        {
            if (list.SelectedItem is UniversalMobyCatalogEntry entry)
                variants.SelectedItem = entry.Variants.FirstOrDefault(v => v.SourceLevelKey == "magiccrafters" && v.SourceRow == 12)
                    ?? entry.Variants.FirstOrDefault(v => v.SourceLevelKey == "magiccrafters" && v.SourceRow == 13);
        };
        using var roomCancellation = new CancellationTokenSource();
        CancellationToken browserCancellation = roomCancellation.Token;
        dialog.Closed += (_, _) => roomCancellation.Cancel();
        bool findingRoom = false;
        // Pressing the button again while a search runs stops the search and keeps what it found so far.
        CancellationTokenSource? activeSearchStop = null;
        string spaceSearchNote = "";
        Button close = NewButton("Close"); close.Name = "UniversalMobyClose"; StyleModernSecondaryButton(close);
        ToolTip.SetTip(place, "Close this browser, then click the terrain in the 3D view to place the object.");
        ToolTip.SetTip(addHere, "Place the object beside the current selection, or at the default position when nothing is selected.");
        Button japaneseDisc = NewButton("Choose Japanese disc…"); japaneseDisc.Name = "UniversalMobyJapaneseDisc"; StyleModernSecondaryButton(japaneseDisc);
        ToolTip.SetTip(japaneseDisc, "Dragonfly eggs and signs are copied from your unchanged original Spyro the Dragon (Japan) disc. It is only read, never modified.");
        UniversalJapaneseSource? japanese = null;
        bool checking = false;
        bool refreshingList = false;
        Grid jpRow = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        jpRow.Children.Add(new TextBlock
        {
            Text = "Dragonfly eggs and signs come from the Japanese version of Spyro. Choose your copy of that disc once to unlock them (it is only read, never changed).",
            TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(japaneseDisc, 1); jpRow.Children.Add(japaneseDisc);
        Border jpBanner = new()
        {
            Name = "UniversalMobyJapaneseBanner", IsVisible = false, Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromRgb(255, 244, 214)), Child = jpRow
        };
        Button crashDisc = NewButton("Choose Crash disc…"); crashDisc.Name = "UniversalMobyCrashDisc"; StyleModernSecondaryButton(crashDisc);
        ToolTip.SetTip(crashDisc, "Crash crates, TNT, Nitro and the Crash enemies use art, models and sounds from your Crash Bandicoot: Warped (USA) disc. It is only read, never modified.");
        CrashWarpedDisc? crash = null;
        Grid crashRow = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        crashRow.Children.Add(new TextBlock
        {
            Text = "Crash crates, TNT, Nitro and the Crash enemies (frog, knight, wizard) use art, models and sounds from Crash Bandicoot: Warped. Choose your copy of that disc once to unlock them (it is only read, never changed).",
            TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(crashDisc, 1); crashRow.Children.Add(crashDisc);
        Border crashBanner = new()
        {
            Name = "UniversalMobyCrashBanner", IsVisible = false, Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromRgb(255, 233, 214)), Child = crashRow
        };
        Func<string, UniversalSpecialAsset?>? special = null;
        close.Click += (_, _) => dialog.Close();

        UniversalSourceLibrary? lib = null;
        IReadOnlyList<UniversalMobyCatalogEntry> entries = [];
        INativeAuthoredCompilation? freshCompilation = null;
        UniversalCompositionHost? retailHost = null;
        NativeTerrainTexturePrivateRecordBatchRequest? retailPrivateRequest = null;
        System.Collections.Concurrent.ConcurrentDictionary<string,Lazy<UniversalCompositionHost>> jointHosts = new();
        int planned = 0;
        System.Collections.Concurrent.ConcurrentDictionary<UniversalMobyCatalogEntry,UniversalMobyVariant> fittingVariants = new();
        System.Collections.Concurrent.ConcurrentDictionary<UniversalMobyVariant, UniversalDestinationEstimate> estimates = [];
        System.Collections.Concurrent.ConcurrentDictionary<UniversalMobyVariant, (UniversalComposeResult? Result, string? Error)> dryRuns = new();
        // Share bounded background work between the catalog and selected-object panel.
        // Never synchronously wait for a composition from an Avalonia event handler:
        // private-terrain preparation awaits I/O and must not capture a blocked UI thread.
        var fitWorkers = new SemaphoreSlim(2);
        var fitTasks = new System.Collections.Concurrent.ConcurrentDictionary<UniversalMobyVariant,
            Lazy<Task<(UniversalComposeResult? Result, string? Error)>>>();
        HashSet<UniversalMobyVariant> pendingDetails = [];
        bool refreshingDetails = false;
        List<UniversalMobyPlacement> existing = CurrentUniversalPlacements();
        List<int> removedRows = CurrentRemovedNativeRows();
        System.Collections.Concurrent.ConcurrentDictionary<UniversalMobyVariant, (UniversalComposeResult? Result, string? Error)> listDry = new();
        UniversalDestinationEstimate Est(UniversalMobyVariant v)
        {
            if (!estimates.TryGetValue(v, out var e)) estimates[v] = e = freshCompilation == null ? UniversalMobyCatalogBuilder.Estimate(lib!, destination, v, planned) : new(false, v.HasLocalModel, v.HasBehavior, v.ModelBytes + v.BehaviorBytes, 0, true, "Exact authored-scene composition is checked below.");
            return e;
        }

        (UniversalComposeResult? Result,string? Error) ExactRetailCheck(UniversalSourceLibrary library,IReadOnlyList<UniversalMobyPlacement> trial,IReadOnlyCollection<int> gone)
        {
            var initial=(retailHost??throw new InvalidDataException("Prepared terrain fit host is not ready.")).DryRun(library,trial,special,gone);
            if(retailPrivateRequest==null||initial.Error!=null&&!UniversalCapacityPlanner.IsCapacityFailure(initial.Error))return initial;
            try
            {
                // Prospective checks and saved-project exports must select the same reservation
                // policy. A successful existing-import host alone does not prove the next build.
                var ranges=UniversalTerrainReservations.Capture(library,destination,trial,special,gone);
                string key=string.Join(';',ranges.Select(r=>$"{r.Offset}:{r.Length}"));
                string current=string.Join(';',retailPrivateRequest.AdditionalProtectedRanges.Select(r=>$"{r.Offset}:{r.Length}"));
                var host=key==current?retailHost:jointHosts.GetOrAdd(key,_=>new Lazy<UniversalCompositionHost>(()=>
                    BuildUniversalPrivateFitHostAsync(retailPrivateRequest with{AdditionalProtectedRanges=ranges}).GetAwaiter().GetResult())).Value;
                return host!.DryRun(library,trial,special,gone);
            }
            catch(Exception e) when(e is IOException or InvalidDataException or InvalidOperationException){return(null,e.Message);}
        }

        (UniversalComposeResult? Result, string? Error) CheckPlacement(UniversalSourceLibrary library, IReadOnlyList<UniversalMobyPlacement> trial) =>
            freshCompilation == null ? ExactRetailCheck(library,trial,removedRows) : DryRunFresh(freshCompilation, library, trial, special);

        IReadOnlyList<UniversalMobyPlacement> TrialFor(UniversalMobyVariant v)
        {
            if (UniversalSpecialCatalog.IsSpecial(v))
                return [new UniversalMobyPlacement(v.SourceLevelKey, v.SourceRow, 0, 0, 0,
                    SpecialId: UniversalSpecialCatalog.SpecialIdForClass(v.ClassId), Text: ["SIGN"],
                    EditorId: Guid.NewGuid().ToString("N"), PersistentSlot: existing.Count)];
            var row = lib!.Get(v.SourceLevelKey).Data.Row(v.SourceRow);
            var root = new UniversalMobyPlacement(v.SourceLevelKey, v.SourceRow,
                BitConverter.ToInt32(row, 0x0C), BitConverter.ToInt32(row, 0x10), BitConverter.ToInt32(row, 0x14),
                EditorId: Guid.NewGuid().ToString("N"), PersistentSlot: existing.Count);
            try { return UniversalPlacementGroups.Expand(lib.Get, root); }
            catch (InvalidDataException)
            {
                // The normal composition check returns the precise source-ownership
                // failure to this row instead of cancelling the entire object browser.
                return [root];
            }
        }

        Task<(UniversalComposeResult? Result, string? Error)> CheckVariantAsync(UniversalMobyVariant variant) =>
            fitTasks.GetOrAdd(variant, v => new(() => Task.Run(async () =>
            {
                await fitWorkers.WaitAsync(browserCancellation).ConfigureAwait(false);
                try
                {
                    browserCancellation.ThrowIfCancellationRequested();
                    Est(v);
                    return CheckPlacement(lib!, existing.Concat(TrialFor(v)).ToList());
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { return (null, ex.Message); }
                finally { fitWorkers.Release(); }
            }, browserCancellation))).Value;

        async Task UpdateSelectedFitAsync(UniversalMobyCatalogEntry entry, UniversalMobyVariant variant)
        {
            // Even a cached task must resume after the current selection refresh completes.
            await Task.Yield();
            try
            {
                var result = await CheckVariantAsync(variant);
                if (browserCancellation.IsCancellationRequested) return;
                dryRuns[variant] = result;
                listDry[variant] = (null, result.Error);
                if (result.Error == null) fittingVariants[entry] = variant;
            }
            catch (OperationCanceledException) when (browserCancellation.IsCancellationRequested) { }
            finally { pendingDetails.Remove(variant); }
            // A late result must not enable placement or change another object's details.
            if (!browserCancellation.IsCancellationRequested && ReferenceEquals(list.SelectedItem, entry) &&
                Equals(variants.SelectedItem as UniversalMobyVariant ?? entry.Default, variant)) RefreshDetails();
        }

        list.ItemTemplate = new FuncDataTemplate<UniversalMobyCatalogEntry>((entry, _) =>
        {
            if (entry == null) return new TextBlock();
            var statusVariant = fittingVariants.TryGetValue(entry,out var fitVersion) ? fitVersion : entry.Default;
            var e = Est(statusVariant);
            bool known = listDry.TryGetValue(statusVariant, out var dr);
            bool fitsNow = known ? dr.Error == null : e.Fits;
            string status = entry.RestrictionReason != null ? (IsCrashVariant(entry.Default) ? "Needs your Crash disc" : IsYotdVariant(entry.Default) ? "Needs your Year of the Dragon disc" : UniversalSpecialCatalog.IsSpecial(entry.Default) ? "Needs your Japanese disc" : "See placement details")
                : !known ? "Checking whether it fits…" : fitsNow ? e.Native ? "Already in this level" : "Can be added"
                : UniversalFitFailureSummary(dr.Error!, destination.DisplayName, compact: true);
            Color statusColor = entry.RestrictionReason == null && !e.Native && !known ? ModernMutedInk
                : entry.RestrictionReason != null || !fitsNow ? ModernRed : e.Native ? ModernBlue : ModernGreen;
            StackPanel words = new()
            {
                Margin = new Thickness(0, 4), Spacing = 3,
                Children =
                {
                    new TextBlock { Text = entry.Name, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = $"{FriendlyCategory(entry.Category)}{(entry.SubCategory.Length > 0 ? " › " + entry.SubCategory : "")} · {(entry.Default.SourceLevelKey == UniversalHighValueGems.SourceKey ? "USA gem variant" : IsCrashVariant(entry.Default) ? "Crash Bandicoot: Warped" : IsYotdVariant(entry.Default) ? "Spyro: Year of the Dragon (Lost Fleet)" : UniversalSpecialCatalog.IsSpecial(entry.Default) ? "Japanese version of the game" : "found in " + string.Join(", ", entry.SourceLevels.Take(3).Select(LevelName)) + (entry.SourceLevels.Count > 3 ? $" and {entry.SourceLevels.Count - 3} more" : ""))}",
                        FontSize = 11, Foreground = new SolidColorBrush(ModernMutedInk), TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = status, FontSize = 11, Foreground = new SolidColorBrush(statusColor) }
                }
            };
            Grid row = new() { ColumnDefinitions = new ColumnDefinitions("72,*"), Margin = new Thickness(4), ColumnSpacing = 6 };
            row.Children.Add(UniversalMobyModelPreview.Build(() => UniversalMobyModelPreview.LoadAsync(lib!, entry.Default, japanese, crash, ResolveYearOfTheDragonDisc())));
            Grid.SetColumn(words, 1); row.Children.Add(words);
            return row;
        });
        categories.ItemTemplate = new FuncDataTemplate<(string, string, int)>((c, _) => new TextBlock
        {
            // Sub-groups are indented with a margin, so a name that wraps stays lined up under itself.
            Text = (c.Item2.Length > 0 ? c.Item2 : FriendlyCategory(c.Item1)) + $"\u00A0\u00A0({c.Item3})",
            FontWeight = c.Item2.Length > 0 ? FontWeight.Normal : FontWeight.SemiBold,
            Margin = new Thickness(c.Item2.Length > 0 ? 20 : 4, 3, 4, 3),
            TextWrapping = TextWrapping.Wrap, MaxWidth = 200
        });
        variants.ItemTemplate = new FuncDataTemplate<UniversalMobyVariant>((v, _) =>
            v != null && UniversalDragonflyEggs.IsEggClass(v.ClassId)
                ? BuildEggColourChoice(UniversalSpecialCatalog.SpecialIdForClass(v.ClassId)!, UniversalSpecialCatalog.DisplayName(v.ClassId))
                : new TextBlock { Text = v == null ? "" : v.SourceLevelKey == UniversalHighValueGems.SourceKey || IsCrashVariant(v) || IsYotdVariant(v) ? UniversalSpecialCatalog.DisplayName(v.ClassId) : UniversalSpecialCatalog.IsSpecial(v) ? "Japanese sign" : VersionName(v), TextWrapping = TextWrapping.Wrap });

        string VersionName(UniversalMobyVariant v)
        {
            var same = list.SelectedItem is UniversalMobyCatalogEntry en ? en.Variants.Where(x => x.SourceLevelKey == v.SourceLevelKey).ToList() : [v];
            int n = same.IndexOf(v);
            string identity = list.SelectedItem is UniversalMobyCatalogEntry selected ? DisplaySourceName(selected,v) : "Source object";
            return same.Count > 1 ? $"{identity} — {LevelName(v.SourceLevelKey)} (#{n + 1} of {same.Count})" : $"{identity} — {LevelName(v.SourceLevelKey)}";
        }

        string DisplaySourceName(UniversalMobyCatalogEntry entry, UniversalMobyVariant v) => UniversalSourceObjectName(entry,v)
            .Replace("; roster identity", "", StringComparison.Ordinal)
            .Replace("(identity unverified)", "(name unknown)", StringComparison.Ordinal);

        string LevelName(string key) => key == UniversalHighValueGems.SourceKey ? "USA gem variants" : key == UniversalCrashBoxes.SourceKey ? "Crash Bandicoot: Warped" : key == UniversalCrashEnemies.YotdSourceKey ? "Spyro: Year of the Dragon" : key == UniversalSpecialCatalog.SourceKey ? "Spyro the Dragon (Japan)" : _retailCatalog.FindByKey(key)?.DisplayName ?? key;

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
            var shown = entries.Where(e => (q.Length > 0 || MatchesCategory(e)) &&
                (q.Length == 0 || q.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(e.SearchText.Contains)) &&
                (src == null || e.SourceLevels.Contains(src)) &&
                (fitFilter.SelectedIndex switch { 1 => e.RestrictionReason == null && (fittingVariants.ContainsKey(e) || (listDry.TryGetValue(e.Default, out var d) && d.Error == null)), 2 => Est(e.Default).Native, _ => true }))
                .OrderBy(e => UniversalSpecialCatalog.IsSpecial(e.Default) ? 0 : 1).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var selected = list.SelectedItem;
            // Rebuild status rows without treating their temporary removal as a new
            // selection. Keep the user's chosen source version while fit checks finish.
            refreshingList = true;
            try
            {
                list.ItemsSource = null;
                list.ItemsSource = shown;
                if (selected != null && shown.Contains(selected)) list.SelectedItem = selected;
            }
            finally { refreshingList = false; }
            clearSearch.IsVisible = q.Length > 0;
            empty.IsVisible = shown.Count == 0;
            empty.Text = checking && fitFilter.SelectedIndex == 1
                ? "Checking which objects fit. Results will appear here as the checks finish."
                : q.Length > 0 ? "No objects match this search. Try a shorter name or clear the search. You can also change the level or fit filters."
                : "No objects match these filters. Choose another category, source level or fit filter.";
            summary.Text = $"Showing {shown.Count} of {entries.Count} objects{(q.Length > 0 ? " · searching all categories" : "")}." +
                (checking ? " Checking which objects fit this level…" : " Choose an object to see its preview and placement options.");
            RefreshDetails();
        }

        void RefreshDetails()
        {
            if (refreshingDetails || browserCancellation.IsCancellationRequested) return;
            refreshingDetails = true;
            try { RefreshDetailsCore(); }
            finally { refreshingDetails = false; }
        }

        void RefreshDetailsCore()
        {
            place.IsEnabled = addHere.IsEnabled = false;
            magicRoom.IsVisible = magicPair.IsVisible = false;
            joinCannon.IsVisible = false;
            // While a search runs the button is its Stop button; a refresh must not take that away.
            findRoom.Content = findingRoom ? "Stop searching" : "Find space for this object…";
            findRoom.IsEnabled = findingRoom || (lib != null && freshCompilation == null && list.SelectedItem is UniversalMobyCatalogEntry);
            if (lib == null || list.SelectedItem is not UniversalMobyCatalogEntry entry)
            {
                title.Text = "Choose an object"; details.Text = "Select an object from the list to see its preview and check whether it fits this level."; fit.Text = ""; technical.Text = "";
                roomPlans.ItemsSource = null; roomPlans.IsVisible = inspectPlan.IsVisible = false; roomExplanation.Text = "";
                modelPreview.Child = null; previewVariant = null;
                variants.ItemsSource = null; variants.Tag = null; variants.IsVisible = variantLabel.IsVisible = false; return;
            }
            title.Text = entry.Name;
            variants.IsVisible = variantLabel.IsVisible = entry.Variants.Count > 1;
            if (!ReferenceEquals(variants.Tag, entry))
            {
                variants.Tag = entry;
                variants.ItemsSource = entry.Variants;
                variants.SelectedItem = fittingVariants.TryGetValue(entry,out var preferred) ? preferred : entry.Default;
            }
            var v = variants.SelectedItem as UniversalMobyVariant ?? entry.Default;
            if (previewVariant != v)
            {
                previewVariant = v;
                modelPreview.Child = UniversalMobyModelPreview.Build(() => UniversalMobyModelPreview.LoadAsync(lib, v, japanese, crash, ResolveYearOfTheDragonDisc()), detailed: true);
            }
            bool magicArtisans = freshCompilation == null && IsMagicArtisansRoomChoice(destination.Key, v);
            if (magicArtisans)
            {
                magicRoom.Text = MagicArtisansRoomText(v); magicRoom.IsVisible = true;
                magicPair.IsVisible = !IsMagicArtisansPairChoice(v) && entry.Variants.Any(p => p.SourceLevelKey == "magiccrafters" && p.SourceRow is 12 or 13);
                findRoom.Content = findingRoom ? "Stop searching" : "Review the six Gnorcs to make room…";
                findRoom.IsEnabled = findingRoom || IsMagicArtisansPairChoice(v);
            }
            bool alpineArtisans = freshCompilation == null && UniversalAlpineArtisansRemovalAdvice.IsChoice(destination.Key,v.SourceLevelKey,v.SourceRow);
            if (alpineArtisans)
            {
                magicRoom.Text = UniversalAlpineArtisansRemovalAdvice.Explanation; magicRoom.IsVisible = true;
                findRoom.Content = findingRoom ? "Stop searching" : "Review the objects needed to make room…";
            }
            // Source-version estimates can also decode models; populate those on the worker.
            var e = estimates.TryGetValue(v, out var estimate) ? estimate :
                new UniversalDestinationEstimate(false, v.HasLocalModel, v.HasBehavior, 0, 0, false, "Checking…");
            bool isSpecial = UniversalSpecialCatalog.IsSpecial(v);
            variantLabel.Text = entry.SubCategory == "Dragonfly Eggs" ? "Color" : "Source version";
            // The heading already names the object; repeat the source's own name only when it differs.
            string sourceName = DisplaySourceName(entry, v);
            details.Text = (string.Equals(sourceName, title.Text, StringComparison.OrdinalIgnoreCase) ? "" : sourceName + "\n") +
                $"From {LevelName(v.SourceLevelKey)}.\n" +
                (isSpecial && entry.Notes is { Length: > 0 } friendly ? friendly :
                    e.Native ? "This object already belongs to this level." : "Its appearance and game behavior are copied when you create the BIN. Playtest your exported level to check it in its new surroundings.");
            if (UniversalDreamWeaversCannonRows.IsCannonRow(v.SourceLevelKey, v.SourceRow))
                details.Text += "\nComes with its gunner, a Winged Fool that keeps the cannon pointed at Spyro. Once Spyro defeats the gunner, the barrel stays where it was left: stand behind the cannon facing it and flame it to fire. A hit makes a big Armored Fool small (and a small one big). To give it targets, select the cannon, then add Armored Fools with \u201cLet the Resize cannon shoot it\u201d ticked (Dream Weavers T20 and T21 start big and never call it).";
            if (UniversalDreamWeaversCannonRows.IsFoolRow(v.SourceLevelKey, v.SourceRow))
            {
                joinCannon.IsVisible = levelHasCannon;
                joinCannon.IsEnabled = cannonGroup != null;
                joinCannonText.Text = cannonGroup != null ? "Let the Resize cannon shoot it (joins the cannon\u2019s group)" : "Select the Resize cannon in the level first to let it shoot this fool";
                if (!levelHasCannon) details.Text += "\nOn its own a fool never calls a Resize cannon. Add a Resize cannon first to let one shoot it.";
                else details.Text += "\n" + UniversalDreamWeaversCannonRows.Describe(lib.Get(v.SourceLevelKey).Data, v.SourceRow);
            }
            technical.Text =
                $"Source identity: {UniversalSourceObjectName(entry,v)}\n" +
                $"Object type {entry.ClassHex} · {FriendlyCategory(entry.Category)}{(entry.SubCategory.Length > 0 ? " › " + entry.SubCategory : "")}\n" +
                (UniversalSpecialCatalog.IsSpecial(v)
                    ? $"Source: {LevelName(v.SourceLevelKey)} (local source model; native USA behavior)\n"
                    : $"Source: {LevelName(v.SourceLevelKey)} row T{v.SourceRow} ({entry.Variants.Count} native placement(s) across {entry.SourceLevels.Count} level(s))\n") +
                $"Model: {(v.SharedModel ? "shared game-wide model" : v.HasLocalModel ? $"{v.ModelBytes / 1024.0:0.0} KB level model with textures and sounds" : "no visible model (controller/trigger)")}\n" +
                $"Behavior: {(UniversalSpecialCatalog.IsSpecial(v) ? $"native USA-engine routine added by the editor ({v.BehaviorBytes / 1024.0:0.0} KB)" : v.HasBehavior ? $"native code from {LevelName(v.SourceLevelKey)} ({v.BehaviorBytes / 1024.0:0.0} KB)" : "engine default")}\n" +
                $"Variants: {entry.Variants.Count} placement(s) across {entry.SourceLevels.Count} level(s)\n" +
                $"Found in: {string.Join(", ", entry.SourceLevels.Select(LevelName))}" +
                (!isSpecial && entry.Notes is { Length: > 0 } notes ? "\n" + notes : "");
            if (magicArtisans && !IsMagicArtisansPairChoice(v))
            {
                fit.Text = "Use the supported pair above to check this Artisans option.";
                fit.Foreground = new SolidColorBrush(ModernRed); return;
            }
            if (magicArtisans && MagicArtisansRoomRestriction(existing, removedRows) is string magicRestriction)
            {
                fit.Text = magicRestriction; fit.Foreground = new SolidColorBrush(ModernRed); return;
            }
            string? restriction = v.RestrictionReason ?? entry.RestrictionReason;
            if (v.DroppedExternalPointers > 0)
            {
                details.Text += "\nObjects linked to this source copy are not automatically added. Check how it behaves after exporting.";
                technical.Text += $"\nImport clears {v.DroppedExternalPointers} external relationship pointer(s).";
            }
            if (restriction != null)
            {
                fit.Text = isSpecial ? (IsCrashVariant(v) ? "Choose your Crash Bandicoot: Warped disc above to add this object." : IsYotdVariant(v) ? "Choose your Spyro: Year of the Dragon disc (Game Settings → Music → Year of the Dragon disc…) to add this creature." : "Choose your Japanese disc above to add this object.")
                    : "This source version cannot be placed here. Try another version if available; see Technical details for the reason.";
                technical.Text += "\nPlacement restriction: " + restriction;
                fit.Foreground = new SolidColorBrush(ModernRed); return;
            }
            var trial = existing.Concat(TrialFor(v)).ToList();
            if (trial.Count > existing.Count + 1)
                details.Text += $"\nIncludes {Count(trial.Count - existing.Count - 1, "required linked object")}. They move, copy and remove together.";
            if (!dryRuns.TryGetValue(v, out var dry))
            {
                fit.Text = "Checking whether this object fits… You can keep browsing.";
                fit.Foreground = new SolidColorBrush(ModernMutedInk);
                if (pendingDetails.Add(v)) _ = UpdateSelectedFitAsync(entry, v);
                return;
            }
            listDry[v] = (null, dry.Error);
            if (dry.Error == null) fittingVariants[entry] = v;
            if (dry.Error != null)
            {
                fit.Text = UniversalFitFailureSummary(dry.Error, destination.DisplayName, compact: false) +
                    (UniversalCapacityPlanner.IsCapacityFailure(dry.Error)
                        ? freshCompilation == null
                            ? "\nUse Find space for this object to compare versions and removal plans."
                            : "\nRemove other objects or choose a different source version to make room."
                        : "");
                if (alpineArtisans)
                    fit.Text = removedRows.Any(r => !UniversalAlpineArtisansRemovalAdvice.RequiredRows.Contains(r))
                        ? "Undo other original-object removals first. This option needs exactly the listed 46 objects removed."
                        : UniversalAlpineArtisansRemovalAdvice.RequiredRows.Except(removedRows).Any()
                            ? "Review the named objects below, then use the normal Remove button and save your changes. Reopen Add object to recheck. Removing the balloonist and balloon also removes balloon travel."
                            : "The removal list is complete, but this group did not pass the export check. Keep the terrain and remaining original objects unchanged, and use this group on its own. See Technical details for the reason.";
                if (magicArtisans)
                    fit.Text = MagicArtisansRoomRestriction(existing, removedRows) ??
                        (MagicArtisansRoomRows.Except(removedRows).Any()
                            ? "Remove all six listed Gnorcs, then reopen Add object to check the pair. Review them below before removing."
                            : "This pair could not pass the export check. It needs unchanged Artisans terrain and remaining original objects; see Technical details for the reason.");
                technical.Text += "\nExport check: " + dry.Error;
                fit.Foreground = new SolidColorBrush(ModernRed);
                return;
            }
            var ram = dry.Result!.Ram;
            const string lanternNote = "Lights the lantern and changes linked creatures; terrain lighting is unchanged.";
            if (dry.Result.Warnings.Contains(lanternNote) && trial.Skip(existing.Count).Any(p =>
                    p.SpecialId == null && string.Equals(p.SourceLevelKey, "darkpassage", StringComparison.OrdinalIgnoreCase) &&
                    BitConverter.ToUInt16(lib.Get(p.SourceLevelKey).Data.Row(p.SourceRowIndex), 0x36) == 0x7A))
                details.Text += "\n" + lanternNote;
            fit.Text = e.Native ? $"✓ Already part of {destination.DisplayName} — ready to place." : $"✓ Fits in {destination.DisplayName} — ready to place." +
                (dry.Result.Classes.Count(c => c.DependencyOnly) is int deps and > 0 ? $" It brings {Count(deps, "small helper object")} it needs along with it." : "");
            if (dry.Result.ParticleDependencies.Any(p => p.Status == "dependency-unverified"))
                fit.Text += "\nIts special effects still need a playtest in this level.";
            if (dry.Result.ParticleDependencies.Any(p => p.Status == "dependency-unverified"))
                technical.Text += "\nParticle behavior dependencies are not yet verified.";
            technical.Text += $"\nGame RAM left after import: {ram.FinalHeadroom / 1024.0:0.0} KB";
            int reactionBytes = dry.Result.PlayerAnimationImports.Sum(p => p.ModelGrowthBytes);
            if (reactionBytes > 0)
                technical.Text += $"\nIncludes Spyro's reaction animations ({reactionBytes / 1024.0:0.0} KB).";
            fit.Foreground = new SolidColorBrush(ModernGreen);
            place.IsEnabled = addHere.IsEnabled = true;
        }

        roomPlans.ItemTemplate = new FuncDataTemplate<UniversalFitChoice>((choice, _) => new TextBlock
        {
            Text = choice == null ? "" : choice.Plan == null
                ? $"{VersionName(choice.Variant)} — fits without further removals"
                : $"{VersionName(choice.Variant)}: remove {Count(choice.Plan.AdditionalRows.Count, "object")}\n" +
                  string.Join(" + ", choice.Plan.Groups.Select(g => $"{g.Name} ({g.AdditionalRows.Count})")) +
                  $"\nSpace after placement: {choice.Plan.FinalRamHeadroom / 1024.0:0.0} KB game memory left; {choice.Plan.ReclaimedVramBytes / 1024.0:0.0} KB texture space freed" +
                  (choice.Plan.ParticleBehaviorUnverified ? "\nParticle behavior still needs a playtest." : "\nGameplay still needs a playtest."),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4)
        });
        roomPlans.SelectionChanged += (_, _) =>
        {
            inspectPlan.IsVisible = roomPlans.SelectedItem is UniversalFitChoice { Plan: not null };
            if (roomPlans.SelectedItem is UniversalFitChoice choice) variants.SelectedItem = choice.Variant;
        };
        inspectPlan.Click += (_, _) =>
        {
            if (roomPlans.SelectedItem is not UniversalFitChoice choice || list.SelectedItem is not UniversalMobyCatalogEntry entry) return;
            dialog.Close(); ShowUniversalRemovalPlan(choice, UniversalSourceObjectName(entry,choice.Variant));
        };
        findRoom.Click += async (_, _) =>
        {
            if (findingRoom)
            {
                if (activeSearchStop == null) return; // the short Magic Crafters pair check cannot be stopped
                activeSearchStop.Cancel(); findRoom.IsEnabled = false;
                roomExplanation.Text = "Stopping… what has been found so far is kept.";
                return;
            }
            if (lib == null || freshCompilation != null || list.SelectedItem is not UniversalMobyCatalogEntry selectedEntry) return;
            var magicSelected = variants.SelectedItem as UniversalMobyVariant ?? selectedEntry.Default;
            if (IsMagicArtisansRoomChoice(destination.Key, magicSelected))
            {
                roomPlans.ItemsSource = null; roomPlans.IsVisible = inspectPlan.IsVisible = false;
                if (!IsMagicArtisansPairChoice(magicSelected)) { roomExplanation.Text = "Choose the supported druid pair first."; return; }
                string? blocked = MagicArtisansRoomRestriction(existing, removedRows);
                if (blocked != null) { roomExplanation.Text = blocked; return; }
                findingRoom = true; findRoom.IsEnabled = false;
                roomExplanation.Text = "Checking the pair with all six Gnorcs removed…";
                var magicTrial = existing.Concat(TrialFor(magicSelected)).ToList();
                try
                {
                    var check = await Task.Run(() => ExactRetailCheck(lib, magicTrial, MagicArtisansRoomRows), browserCancellation);
                    if (browserCancellation.IsCancellationRequested) return;
                    if (!ReferenceEquals(list.SelectedItem, selectedEntry) || !Equals(variants.SelectedItem as UniversalMobyVariant ?? selectedEntry.Default, magicSelected))
                    { roomExplanation.Text = "Selection changed. Use Review again to check the new choice."; return; }
                    if (check.Error != null || check.Result == null)
                    {
                        roomExplanation.Text = "This removal option did not pass the export check. Keep the Artisans terrain and remaining original objects unchanged. See Technical details for the exact reason.";
                        technical.Text += "\nArtisans removal check: " + check.Error; return;
                    }
                    string Name(int row) => _currentMobys.Single(m => !m.IsAdded && m.TrueIndex == row).DisplayLabel;
                    var plan = MagicArtisansCheckedPlan(check.Result, removedRows, Name);
                    roomPlans.ItemsSource = new[] { new UniversalFitChoice(magicSelected, plan.AdditionalRows.Count > 0 ? plan : null) };
                    roomPlans.IsVisible = true; roomPlans.SelectedIndex = 0;
                    roomExplanation.Text = $"Checked: removing these six enemies frees {plan.DroppedModelBytes / 1024.0:0.0} KB of model memory and {plan.ReclaimedVramBytes / 1024.0:0.0} KB of texture space. The pair leaves {plan.FinalRamHeadroom / 1024.0:0.0} KB of game memory.\n" +
                        (plan.AdditionalRows.Count > 0 ? $"{plan.AdditionalRows.Count} of the six still need removal. Inspect them below and use the normal Remove button. Reopen Add object afterwards to recheck." : "All six are already removed. The pair fits without further removals.");
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { roomExplanation.Text = "The removal option could not be checked: " + ex.Message; }
                finally { findingRoom = false; if (!browserCancellation.IsCancellationRequested) RefreshDetails(); }
                return;
            }
            findingRoom = true; roomPlans.IsVisible = inspectPlan.IsVisible = false;
            using var searchStop = CancellationTokenSource.CreateLinkedTokenSource(browserCancellation);
            activeSearchStop = searchStop;
            findRoom.Content = "Stop searching"; findRoom.IsEnabled = true;
            var selected = variants.SelectedItem as UniversalMobyVariant ?? selectedEntry.Default;
            // Six versions and 48 exact checks each: the exhaustive 12 x 128 search ran for minutes with a
            // static message and rarely found more than the first few options do.
            const int maximumChecks = 48;
            var candidates = selectedEntry.Variants.Where(v => v.Extractable)
                .GroupBy(v => (v.SourceLevelKey, v.PropertiesBytes)).Select(g => g.First())
                .OrderBy(v => v == selected ? 0 : 1).Take(6).ToList();
            if (!candidates.Contains(selected)) candidates.Insert(0, selected);
            var labels = _currentMobys.Where(m => !m.IsAdded && m.TrueIndex >= 0)
                .GroupBy(m => m.TrueIndex).ToDictionary(g => g.Key, g => (g.First().DisplayLabel, g.First().CandidateKind));
            var trials = candidates.Select(v => (Variant: v, Trial: existing.Concat(TrialFor(v)).ToList())).ToList();
            var liveChoices = new System.Collections.ObjectModel.ObservableCollection<UniversalFitChoice>();
            roomPlans.ItemsSource = liveChoices;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            int currentCopies = existing.Count(p => !p.Retired);
            string Elapsed() => clock.Elapsed.TotalSeconds < 60 ? $"{clock.Elapsed.TotalSeconds:0} s" : $"{(int)clock.Elapsed.TotalMinutes}:{clock.Elapsed.Seconds:00} min";
            void ShowProgress(int version, int versions, string versionName, UniversalSearchProgress? p)
            {
                string where = versions > 1 ? $"version {version} of {versions} ({versionName}), " : "";
                string count = p == null ? "preparing…" : $"option {Math.Max(1, p.Checks - 1)} of up to {Math.Min(p.MaximumChecks - 1, p.Options)}";
                roomExplanation.Text = $"Looking for a way to make room: {where}{count}, {Elapsed()}. " +
                    (liveChoices.Count > 0 ? $"{Count(liveChoices.Count, "way")} found so far. " : "") +
                    "Each option is a complete export check, so this can take a while. Stop searching keeps what has been found.";
            }
            ShowProgress(1, trials.Count, LevelName(trials[0].Variant.SourceLevelKey), null);
            try
            {
                var reports = await Task.Run(() =>
                {
                    var found = new List<(UniversalMobyVariant Variant, UniversalCapacityAdvice Advice)>();
                    int version = 0;
                    foreach (var item in trials)
                    {
                        browserCancellation.ThrowIfCancellationRequested();
                        if (searchStop.IsCancellationRequested) break;
                        version++;
                        int versionNow = version;
                        (UniversalComposeResult? Result,string? Error) ExactCheck(IReadOnlyCollection<int> gone)
                        {
                            var result = ExactRetailCheck(lib,item.Trial,gone);
                            if (gone.ToHashSet().SetEquals(removedRows) && result.Error == null) dryRuns[item.Variant] = result;
                            return result;
                        }
                        var advice = UniversalCapacityPlanner.Analyze(lib, destination, item.Trial, removedRows,
                            row => labels.GetValueOrDefault(row, ($"Object T{row}", "unknown")), special,
                            maximumChecks: maximumChecks, cancellationToken: roomCancellation.Token, host: retailHost, compositionCheck:ExactCheck,
                            progress: p => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                // A report that arrives after the search ended must not overwrite its result.
                                if (browserCancellation.IsCancellationRequested || !ReferenceEquals(activeSearchStop, searchStop)) return;
                                if (p.Found != null) { liveChoices.Add(new UniversalFitChoice(item.Variant, p.Found)); roomPlans.IsVisible = true; if (roomPlans.SelectedIndex < 0) roomPlans.SelectedIndex = 0; }
                                ShowProgress(versionNow, trials.Count, LevelName(item.Variant.SourceLevelKey), p);
                            }), stop: searchStop.Token);
                        found.Add((item.Variant, advice));
                        if (advice.CurrentError == null || advice.Plans.Count > 0 || advice.Stopped) break;
                    }
                    return found;
                }, browserCancellation);
                if (browserCancellation.IsCancellationRequested) return;
                if (!ReferenceEquals(list.SelectedItem, selectedEntry) || !Equals(variants.SelectedItem as UniversalMobyVariant ?? selectedEntry.Default, selected))
                { roomExplanation.Text = "Selection changed. Use Find space again to check the new choice."; return; }
                var choices = reports.SelectMany(r => r.Advice.CurrentError == null
                    ? new[] { new UniversalFitChoice(r.Variant, null) }
                    : r.Advice.Plans.Select(p => new UniversalFitChoice(r.Variant, p))).ToList();
                roomPlans.ItemsSource = choices; roomPlans.IsVisible = choices.Count > 0;
                int checks = reports.Sum(r => r.Advice.Checks);
                bool stopped = reports.Any(r => r.Advice.Stopped);
                string situation = $"This level already has {Count(currentCopies, "object")} added by you and {Count(removedRows.Count, "original object")} removed.";
                roomExplanation.Text = choices.Count > 0
                    ? $"{Count(choices.Count, "way")} to make room {(choices.Count == 1 ? "was" : "were")} found ({Count(checks, "export check")}, {Elapsed()}{(stopped ? ", stopped early" : "")}). " +
                      "Choose one below, then inspect the named objects. Removing an object frees its own slot; its appearance and texture space are freed only when every copy is removed. You decide which objects to remove."
                    : stopped
                        ? $"Stopped after {Count(checks, "export check")} ({Elapsed()}) without finding a way to make room. {situation}"
                        : $"No way to make room was found: {Count(checks, "export check")} across {Count(reports.Count, "source version")} in {Elapsed()}. {situation} " +
                          "Removing objects you added, choosing a smaller object, or trying another version of this one is more likely to work.";
                spaceSearchNote = $"Space search: {checks} complete export checks across {reports.Count} version(s) in {Elapsed()}; up to {maximumChecks - 1} removal options of one to three native type groups per version" + (stopped ? "; stopped by the user." : ".");
                technical.Text += "\n" + spaceSearchNote;
                foreach (var report in reports.Where(r => r.Advice.Plans.Count == 0 && r.Advice.CurrentError != null && !UniversalCapacityPlanner.IsCapacityFailure(r.Advice.CurrentError)))
                {
                    if (UniversalAlpineArtisansRemovalAdvice.IsChoice(destination.Key,report.Variant.SourceLevelKey,report.Variant.SourceRow))
                    {
                        roomExplanation.Text += "\nThis Alpine group needs exactly the listed 46 removals, unchanged Artisans terrain and remaining objects, and no other added objects. This includes losing balloon travel. The removal option did not pass the export check.";
                        technical.Text += $"\nAlpine Ridge T{report.Variant.SourceRow}: {report.Advice.CurrentError}";
                        continue;
                    }
                    roomExplanation.Text += $"\nThe {LevelName(report.Variant.SourceLevelKey)} version needs another resource from its original level. Removing objects here cannot fix that. Try another source version; see Technical details.";
                    technical.Text += $"\n{LevelName(report.Variant.SourceLevelKey)} T{report.Variant.SourceRow}: {report.Advice.CurrentError}";
                }
                roomPlans.SelectedIndex = choices.Count > 0 ? 0 : -1;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { roomExplanation.Text = ex.Message; }
            finally
            {
                findingRoom = false; activeSearchStop = null; findRoom.Content = "Find space for this object…";
                if (!browserCancellation.IsCancellationRequested)
                {
                    string keep = roomExplanation.Text ?? ""; var keepPlans = roomPlans.ItemsSource; bool keepVisible = roomPlans.IsVisible;
                    RefreshDetails();
                    // RefreshDetails rebuilds the texts for the selection; the search result must survive it.
                    roomExplanation.Text = keep; roomPlans.ItemsSource = keepPlans; roomPlans.IsVisible = keepVisible;
                    inspectPlan.IsVisible = roomPlans.SelectedItem is UniversalFitChoice { Plan: not null };
                    if (spaceSearchNote.Length > 0 && !(technical.Text ?? "").Contains(spaceSearchNote, StringComparison.Ordinal)) technical.Text += "\n" + spaceSearchNote;
                }
            }
        };

        async Task AddAsync(bool clickToPlace)
        {
            if (lib == null || list.SelectedItem is not UniversalMobyCatalogEntry entry) return;
            var v = variants.SelectedItem as UniversalMobyVariant ?? entry.Default;
            dialog.Close();
            if (joinCannon.IsVisible && joinCannon.IsChecked == true && cannonGroup != null)
                await AddUniversalMobyIntoGroupAsync(lib, entry, v, clickToPlace, japanese, cannonGroup);
            else await AddUniversalMobyAsync(lib, entry, v, clickToPlace, japanese);
        }
        place.Click += async (_, _) => await AddAsync(true);
        addHere.Click += async (_, _) => await AddAsync(false);
        list.DoubleTapped += async (_, _) => { if (place.IsEnabled) await AddAsync(true); };
        list.SelectionChanged += (_, _) =>
        {
            if (refreshingList) return;
            roomPlans.ItemsSource = null; roomPlans.IsVisible = inspectPlan.IsVisible = false; roomExplanation.Text = "";
            RefreshDetails();
        };
        variants.SelectionChanged += (_, _) =>
        {
            if (roomPlans.SelectedItem is UniversalFitChoice oldPlan && !Equals(oldPlan.Variant, variants.SelectedItem))
            {
                roomPlans.ItemsSource = null; roomPlans.IsVisible = inspectPlan.IsVisible = false; roomExplanation.Text = "";
            }
            RefreshDetails();
        };
        categories.SelectionChanged += (_, _) => RefreshList();
        search.TextChanged += (_, _) => RefreshList();
        fitFilter.SelectionChanged += (_, _) => RefreshList();
        sourceFilter.SelectionChanged += (_, _) => RefreshList();

        Grid top = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 8, RowSpacing = 8 };
        top.Children.Add(search);
        Grid.SetColumn(clearSearch, 1); top.Children.Add(clearSearch);
        WrapPanel filters = new() { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 8 };
        filters.Children.Add(fitFilter); filters.Children.Add(sourceFilter);
        Grid.SetRow(filters, 1); Grid.SetColumnSpan(filters, 2); top.Children.Add(filters);
        StackPanel right = new() { Spacing = 10, Margin = new Thickness(12, 0, 16, 0) };
        right.Children.Add(title);
        right.Children.Add(modelPreview);
        right.Children.Add(variantLabel);
        right.Children.Add(variants);
        right.Children.Add(details);
        right.Children.Add(fit);
        right.Children.Add(joinCannon);
        right.Children.Add(magicRoom); right.Children.Add(magicPair);
        right.Children.Add(findRoom); right.Children.Add(roomExplanation); right.Children.Add(roomPlans); right.Children.Add(inspectPlan);
        right.Children.Add(new Expander { Header = "Technical details", IsExpanded = false, Content = technical, HorizontalAlignment = HorizontalAlignment.Stretch });
        WrapPanel buttons = new() { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 8 };
        buttons.Children.Add(place); buttons.Children.Add(addHere); buttons.Children.Add(close);
        // Wide enough for "Fodder & Animals (17)" on one line; the right column leaves room for its scroll bar.
        Grid body = new() { ColumnDefinitions = new ColumnDefinitions("200,*,320"), ColumnSpacing = 8 };
        body.Children.Add(new ScrollViewer { Content = categories });
        Grid listHost = new(); listHost.Children.Add(list); listHost.Children.Add(empty);
        Grid.SetColumn(listHost, 1); body.Children.Add(listHost);
        ScrollViewer rightScroll = new() { Content = right }; Grid.SetColumn(rightScroll, 2); body.Children.Add(rightScroll);
        DockPanel root = new() { Margin = new Thickness(14) };
        StackPanel header = new() { Spacing = 8, Margin = new Thickness(0, 0, 0, 10) };
        header.Children.Add(new TextBlock { Text = $"Add an object to {destination.DisplayName}", TextWrapping = TextWrapping.Wrap, FontSize = 20, FontWeight = FontWeight.SemiBold });
        header.Children.Add(top); header.Children.Add(summary); header.Children.Add(terrainNotice); header.Children.Add(jpBanner); header.Children.Add(crashBanner);
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        Border footer = new() { Name = "UniversalMobyFooter", Padding = new Thickness(0, 10, 0, 0), Child = buttons };
        buttons.HorizontalAlignment = HorizontalAlignment.Right;
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer); root.Children.Add(body);
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
                special = UniversalSpecials(japanese, lib);
                var specials = UniversalSpecialCatalog.Entries(japanese != null, crash != null, ResolveYearOfTheDragonDisc() != null);
                entries = [.. entries.Where(e => !UniversalSpecialCatalog.IsSpecial(e.Default)), .. specials];
                dryRuns.Clear();
                fitTasks.Clear();
                foreach (var e in specials) listDry.TryRemove(e.Default, out _);
                if (lib != null)
                    foreach (var e in specials.Where(e => e.RestrictionReason == null))
                        listDry[e.Default] = (null, (await CheckVariantAsync(e.Default)).Error);
                previewVariant = null;
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

        crashDisc.Click += async (_, _) =>
        {
            var files = await dialog.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select your Crash Bandicoot: Warped (USA) disc image (.bin)",
                AllowMultiple = false
            });
            string? picked = files.FirstOrDefault()?.Path.LocalPath;
            if (picked == null) return;
            try
            {
                string bin = DiscImageLocator.ResolveImagePath(picked);
                await Task.Run(() => CrashWarpedDiscLocator.Configure(_workspace, bin));
                crash = await Task.Run(ResolveUniversalCrashDisc);
                special = UniversalSpecials(japanese, lib);
                var specials = UniversalSpecialCatalog.Entries(japanese != null, crash != null, ResolveYearOfTheDragonDisc() != null);
                entries = [.. entries.Where(e => !UniversalSpecialCatalog.IsSpecial(e.Default)), .. specials];
                dryRuns.Clear();
                fitTasks.Clear();
                foreach (var e in specials) listDry.TryRemove(e.Default, out _);
                if (lib != null)
                    foreach (var e in specials.Where(e => e.RestrictionReason == null))
                        listDry[e.Default] = (null, (await CheckVariantAsync(e.Default)).Error);
                previewVariant = null;
                RefreshList(); RefreshDetails();
                summary.Text = "Crash disc found — Crash crates, TNT, Nitro and the Crash enemies are ready to use.";
                crashBanner.IsVisible = false;
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
                special = UniversalSpecials(japanese, l);
                crash = await Task.Run(ResolveUniversalCrashDisc);
                var es = entries = [.. usa, .. UniversalSpecialCatalog.Entries(japanese != null, crash != null, ResolveYearOfTheDragonDisc() != null)];
                lib = l;
                if (_freshSceneDocument != null) freshCompilation = await CaptureFreshNativeAsync();
                else
                {
                    try { (retailHost,retailPrivateRequest) = await CaptureUniversalRetailHostAsync(l,destination,existing,removedRows,special); }
                    catch (Exception terrainRefusal) when (LooksLikeTerrainExportRefusal(terrainRefusal))
                    {
                        // One unexportable terrain change must not hide the whole catalogue.
                        (retailHost,retailPrivateRequest) = await CaptureUniversalRetailHostAsync(l,destination,existing,removedRows,special,ignoreTerrainShapeEdits:true);
                        ReportTerrainExportProblem(destination,terrainRefusal.Message,"object browser");
                        terrainNotice.Text = "Space checks here ignore this level's terrain changes, because one of them can't be exported yet. " +
                            "You can still add objects. Close this window and use the red bar to fix the terrain.";
                        terrainNotice.IsVisible = true;
                    }
                }
                if (browserCancellation.IsCancellationRequested) return;
                planned = await Task.Run(() => PlannedUniversalBytes(l));
                await Task.Run(() => { foreach (var e in es) Est(e.Default); });
                var cats = UniversalCategoryOrder.Select(c => (c.Category, c.Sub,
                    c.Category == "All Mobys" ? es.Count : es.Count(e => e.Category == c.Category && (c.Sub.Length == 0 || e.SubCategory == c.Sub))))
                    .Where(c => c.Item3 > 0 || c.Category == "All Mobys").ToList();
                categories.ItemsSource = cats;
                int startIndex = startCategory == null ? 0 : cats.FindIndex(c => c.Category == startCategory && c.Sub == (startSub ?? ""));
                categories.SelectedIndex = Math.Max(0, startIndex);
                jpBanner.IsVisible = japanese == null;
                crashBanner.IsVisible = crash == null;
                sourceFilter.ItemsSource = new object[] { "Found in any level" }.Concat(_retailCatalog.Levels.Where(x => x.SourceWadEntry > 0)).ToList();
                sourceFilter.SelectedIndex = 0;
                RefreshList();
                if (startSub != null && list.ItemsSource is IEnumerable<UniversalMobyCatalogEntry> firstShown && firstShown.FirstOrDefault() is { } first)
                    list.SelectedItem = first;
                RefreshDetails();
                checking = true;
                RefreshList();
                await Parallel.ForEachAsync(es.Where(x => x.RestrictionReason == null),
                    new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = browserCancellation }, async (x, _) =>
                    {
                        var r = await CheckVariantAsync(x.Default);
                        listDry[x.Default] = (null, r.Error);
                        if (r.Error == null) { dryRuns[x.Default] = r; fittingVariants[x] = x.Default; }
                        else
                            foreach (var alternative in x.Variants.Where(v => v.Extractable && v.SourceLevelKey != x.Default.SourceLevelKey)
                                .GroupBy(v => v.SourceLevelKey).Select(g => g.First()))
                            {
                                var alternate = await CheckVariantAsync(alternative);
                                listDry[alternative] = (null,alternate.Error);
                                if (alternate.Error != null) continue;
                                dryRuns[alternative] = alternate; fittingVariants[x] = alternative; break;
                            }
                    });
                if (browserCancellation.IsCancellationRequested) return;
                checking = false;
                RefreshList();
            }
            catch (OperationCanceledException) when (browserCancellation.IsCancellationRequested) { }
            catch (Exception ex)
            {
                if (browserCancellation.IsCancellationRequested) return;
                summary.Text = ex.Message;
                summary.Foreground = new SolidColorBrush(ModernRed);
            }
        };
        await dialog.ShowDialog(owner);
    }

    private Task AddUniversalMobyAsync(UniversalSourceLibrary lib, UniversalMobyCatalogEntry entry, UniversalMobyVariant v, bool clickToPlace,
        UniversalJapaneseSource? japanese = null) => AddUniversalMobyCoreAsync(lib, entry, v, clickToPlace, japanese, null);

    /// <summary>Adds a Dream Weavers fool into a Resize cannon's linked group, so that cannon can shoot it
    /// (<see cref="UniversalDreamWeaversCannonRows"/>).</summary>
    private Task AddUniversalMobyIntoGroupAsync(UniversalSourceLibrary lib, UniversalMobyCatalogEntry entry, UniversalMobyVariant v, bool clickToPlace,
        UniversalJapaneseSource? japanese, string cannonGroup) => AddUniversalMobyCoreAsync(lib, entry, v, clickToPlace, japanese, cannonGroup);

    private async Task AddUniversalMobyCoreAsync(UniversalSourceLibrary lib, UniversalMobyCatalogEntry entry, UniversalMobyVariant v, bool clickToPlace,
        UniversalJapaneseSource? japanese, string? cannonGroup)
    {
        if (_currentLevel == null) return;
        string? specialId = UniversalSpecialCatalog.IsSpecial(v) ? UniversalSpecialCatalog.SpecialIdForClass(v.ClassId) : null;
        UniversalSpecialAsset? asset = specialId != null ? UniversalSpecials(japanese, lib)?.Invoke(specialId) : null;
        if (specialId != null && asset == null && IsCrashVariant(v)) { _statusText.Text = "Choose your Crash Bandicoot: Warped disc first (Choose Crash disc… in the Add Object browser)."; return; }
        if (specialId != null && asset == null && IsYotdVariant(v)) { _statusText.Text = "Choose your Spyro: Year of the Dragon disc first (Game Settings → Music → Year of the Dragon disc…)."; return; }
        if (specialId != null && asset == null) { _statusText.Text = "Select the original Japanese disc first (Choose Japanese disc… in the Add Object browser)."; return; }
        byte[] row = asset?.RowTemplate ?? lib.Get(v.SourceLevelKey).Data.Row(v.SourceRow);
        string sourceName = v.SourceLevelKey == UniversalHighValueGems.SourceKey ? "USA gem variant" : IsCrashVariant(v) ? "Crash Bandicoot: Warped" : IsYotdVariant(v) ? "Spyro: Year of the Dragon" : asset != null ? "Spyro the Dragon (Japan)" : _retailCatalog.FindByKey(v.SourceLevelKey)?.DisplayName ?? v.SourceLevelKey;
        string name = asset?.Name ?? UniversalSourceObjectName(entry,v);
        var template = new AddMobyTemplate(
            Name: name,
            Type: row[0x50], State: row[0x51],
            SourceByte36: row[0x36], SourceByte37: row[0x37], SourceByte4F: row[0x4F],
            Flag4A: row[0x52], Flag4B: row[0x53],
            UsesGem: false,
            DefaultLabel: name,
            FromCrossLevelTemplate: true,
            DefaultColor: v.ClassId == UniversalHighValueGems.TealClass ? GemValue.Teal.Color : v.ClassId == UniversalHighValueGems.RainbowClass ? GemValue.Rainbow.Color : Moby.ColorForType(row[0x50]),
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
        pending = WithUniversalCompanions(pending, lib, row);
        if (cannonGroup != null)
        {
            if (pending.UniversalCompanions.Count != 0 || !UniversalDreamWeaversCannonRows.IsFoolRow(v.SourceLevelKey, v.SourceRow) ||
                !UniversalCannonGroups().Contains(cannonGroup))
            { _statusText.Text = "Only a Dream Weavers fool can join a Resize cannon in this level."; return; }
            pending = pending with { UniversalLinkGroupId = cannonGroup };
        }
        if (clickToPlace) { BeginPendingMobyPlacement(pending, fallback); return; }
        Vector3f position = ApplyTerrainPlacementLift(SnapOrNearestTerrain(fallback), pending);
        await AddPendingMobyAtPositionAsync(pending, position);
    }
}

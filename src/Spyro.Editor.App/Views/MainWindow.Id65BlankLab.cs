using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private const string Id65BlankLabDisclosureTitle = "ID65 Blank-Level Lab";
    private const string Id65BlankLabTerrainCapability =
        "Existing HP Z: research-only through the topology-safe solid-terrain path. True Add, LP authoring, and XY movement: unavailable.";
    private const string Id65BlankLabTextureCapability =
        "Resident bytes: bound to the locked payload. Portable/custom art: research-only and requires a proved destination profile before a test CUE.";
    private const string Id65BlankLabMobyCapability =
        "Resident records: inspection only. Mutation, portable imports, arbitrary actors, and linked families: unavailable until a destination profile exists.";

    private readonly TextBlock _id65BlankLabStatusText = new();
    private readonly TextBlock _id65BlankLabLockedBaseText = new();
    private readonly TextBlock _id65BlankLabAuthoredLayerText = new();
    private Button? _id65BlankLabBuildButton;
    private Button? _id65BlankLabLoadButton;
    private Button? _id65BlankLabSaveButton;
    private Button? _id65BlankLabCreateCueButton;
    private Button? _id65BlankLabRevealButton;
    private bool _id65BlankLabBusy;
    private string _id65BlankLabRevealPath = "";
    private UnusedLevel65BlankLevelLabWorkspacePaths? _id65BlankLabPaths;
    private UnusedLevel65BlankLevelLabManifest? _id65BlankLabManifest;
    private string _id65BlankLabValidationFailure = "";

    private Control BuildId65BlankLevelLabDisclosure()
    {
        StackPanel panel = new() { Spacing = 10 };
        panel.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(255, 247, 229)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(225, 174, 76)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = new TextBlock
            {
                Text =
                    "Research-only, evidence-bound ID65 workspace. It starts from a locked " +
                    "Town Square-derived physical payload; it is not an empty map and does not " +
                    "make arbitrary terrain, textures, or Mobys safe.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(107, 70, 17)),
                FontSize = 12,
                LineHeight = 18
            }
        });

        _id65BlankLabStatusText.TextWrapping = TextWrapping.Wrap;
        _id65BlankLabStatusText.Foreground = new SolidColorBrush(ModernInk);
        _id65BlankLabStatusText.FontSize = 12;
        _id65BlankLabStatusText.LineHeight = 18;
        panel.Children.Add(_id65BlankLabStatusText);

        Grid layers = NewEqualButtonGrid(2);
        AddModernGridButton(
            layers,
            BuildId65BlankLabLayerCard(
                "Locked base",
                _id65BlankLabLockedBaseText,
                Color.FromRgb(42, 104, 155)),
            0);
        AddModernGridButton(
            layers,
            BuildId65BlankLabLayerCard(
                "Authored layer",
                _id65BlankLabAuthoredLayerText,
                Color.FromRgb(117, 80, 158)),
            1);
        panel.Children.Add(layers);

        panel.Children.Add(BuildId65BlankLabCapabilities());

        _id65BlankLabBuildButton = NewAsyncButton(
            "Build / Refresh Locked Base",
            BuildOrRefreshId65BlankLabBaseAsync);
        _id65BlankLabLoadButton = NewAsyncButton("Load Lab", LoadId65BlankLabAsync);
        _id65BlankLabSaveButton = NewAsyncButton(
            "Save Research Workspace",
            SaveId65BlankLabWorkspaceAsync);
        _id65BlankLabCreateCueButton = NewAsyncButton(
            "Create Disposable Test CUE",
            CreateId65BlankLabDisposableCueAsync);
        _id65BlankLabRevealButton = NewButton(
            "Reveal Test CUE",
            RevealId65BlankLabOutput);

        StyleModernPrimaryButton(_id65BlankLabBuildButton, Color.FromRgb(173, 104, 27));
        StyleModernPrimaryButton(_id65BlankLabLoadButton, ModernTeal);
        StyleModernSecondaryButton(_id65BlankLabSaveButton);
        StyleModernPrimaryButton(_id65BlankLabCreateCueButton, Color.FromRgb(117, 80, 158));
        StyleModernSecondaryButton(_id65BlankLabRevealButton);

        Grid firstActions = NewEqualButtonGrid(2);
        AddModernGridButton(firstActions, _id65BlankLabBuildButton, 0);
        AddModernGridButton(firstActions, _id65BlankLabLoadButton, 1);
        panel.Children.Add(firstActions);

        Grid secondActions = NewEqualButtonGrid(2);
        AddModernGridButton(secondActions, _id65BlankLabSaveButton, 0);
        AddModernGridButton(secondActions, _id65BlankLabCreateCueButton, 1);
        panel.Children.Add(secondActions);
        panel.Children.Add(_id65BlankLabRevealButton);

        RefreshId65BlankLabUi();
        Control disclosure = BuildModernDisclosure(Id65BlankLabDisclosureTitle, panel, expanded: true);
        disclosure.Name = "Id65BlankLevelLabDisclosure";
        return disclosure;
    }

    private static Button BuildId65BlankLabLayerCard(
        string title,
        TextBlock details,
        Color accent)
    {
        details.TextWrapping = TextWrapping.Wrap;
        details.Foreground = new SolidColorBrush(ModernMutedInk);
        details.FontSize = 11;
        details.LineHeight = 16;

        StackPanel content = new() { Spacing = 4 };
        content.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(accent)
        });
        content.Children.Add(details);

        // Reuse the equal-column helper without introducing another layout-only
        // control API; the card is disabled and never acts like a command.
        return new Button
        {
            IsEnabled = false,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
            Padding = new Thickness(9),
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(212, 219, 227)),
            BorderThickness = new Thickness(1),
            Content = content
        };
    }

    private static Control BuildId65BlankLabCapabilities()
    {
        StackPanel panel = new() { Spacing = 5 };
        panel.Children.Add(new TextBlock
        {
            Text = "Current capability boundary",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ModernInk)
        });
        panel.Children.Add(Id65BlankLabCapabilityRow(
            "Terrain",
            Id65BlankLabTerrainCapability));
        panel.Children.Add(Id65BlankLabCapabilityRow(
            "Textures",
            Id65BlankLabTextureCapability));
        panel.Children.Add(Id65BlankLabCapabilityRow(
            "Mobys",
            Id65BlankLabMobyCapability));
        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(243, 247, 251)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(204, 216, 228)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = panel
        };
    }

    private static TextBlock Id65BlankLabCapabilityRow(string label, string text) => new()
    {
        Text = $"{label}: {text}",
        TextWrapping = TextWrapping.Wrap,
        Foreground = new SolidColorBrush(ModernMutedInk),
        FontSize = 11,
        LineHeight = 16
    };

    private LevelCatalog BuildCatalogWithValidatedId65BlankLab(LevelCatalog retailCatalog)
    {
        ArgumentNullException.ThrowIfNull(retailCatalog);
        _id65BlankLabPaths =
            UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(_workspace.RootPath);
        _id65BlankLabManifest = null;
        _id65BlankLabValidationFailure = "";
        try
        {
            _id65BlankLabManifest = UnusedLevel65BlankLevelLabBootstrapper
                .ValidatePublishedWorkspaceAsync(_id65BlankLabPaths)
                .GetAwaiter()
                .GetResult();
            return UnusedLevel65BlankLevelLabProfileRegistry.AugmentCatalog(retailCatalog);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            _id65BlankLabValidationFailure = ex.Message;
            return retailCatalog;
        }
    }

    private async Task BuildOrRefreshId65BlankLabBaseAsync()
    {
        if (_id65BlankLabBusy)
            return;

        string sourceImage = FirstExistingDiscImagePath(
            _discImagePathBox.Text,
            _skyboxDiscImagePathBox.Text,
            DiscImageLocator.FindImage(_workspace));
        string sourceCue = DiscImageLocator.FindCueForImage(sourceImage);
        if (!File.Exists(sourceImage) || !File.Exists(sourceCue))
        {
            _statusText.Text =
                "Choose the exact clean Spyro USA CUE/BIN first. The ID65 lab never falls back to another retail or cached source.";
            return;
        }

        SetId65BlankLabBusy(true);
        _statusText.Text = "ID65 Lab: validating the clean disc and building the evidence-bound locked base...";
        try
        {
            UnusedLevel65BlankLevelLabWorkspacePaths requestedPaths =
                UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(_workspace.RootPath);
            Id65BlankLabAuthoredLayerSnapshot authoredBefore =
                await CaptureId65BlankLabAuthoredLayerAsync(requestedPaths.AuthoredEditsDirectoryPath);
            bool repairingExistingWorkspace =
                File.Exists(requestedPaths.ManifestPath) ||
                File.Exists(requestedPaths.LockedBaseImagePath) ||
                File.Exists(requestedPaths.LockedBaseCuePath) ||
                Directory.Exists(requestedPaths.LockedBaseDirectoryPath);
            UnusedLevel65BlankLevelLabBootstrapResult result =
                await UnusedLevel65BlankLevelLabBootstrapper.BootstrapAsync(
                    new UnusedLevel65BlankLevelLabBootstrapRequest(
                        sourceImage,
                        sourceCue,
                        _workspace.RootPath,
                        ReplaceInvalidExistingWorkspace: true));
            Id65BlankLabAuthoredLayerSnapshot authoredAfter =
                await CaptureId65BlankLabAuthoredLayerAsync(result.Paths.AuthoredEditsDirectoryPath);
            EnsureId65BlankLabAuthoredLayerPreserved(authoredBefore, authoredAfter);
            await BuildId65BlankLabCacheAsync(result.Paths, overwrite: true);
            _id65BlankLabPaths = result.Paths;
            _id65BlankLabManifest = result.Manifest;
            _id65BlankLabValidationFailure = "";
            AdmitValidatedId65BlankLabToCatalog();
            _statusText.Text = result.ReusedExistingLockedBase
                ? "ID65 Lab: exact locked base was already valid; its isolated editor cache was refreshed."
                : repairingExistingWorkspace
                    ? "ID65 Lab: invalid/partial locked-base files were repaired transactionally; the authored-edits layer and clean source BIN were preserved."
                    : "ID65 Lab: exact locked base was built and validated; the clean source BIN was preserved.";
        }
        catch (Exception ex)
        {
            _id65BlankLabValidationFailure = ex.Message;
            _statusText.Text = $"Could not build the ID65 locked base: {ex.Message}";
        }
        finally
        {
            SetId65BlankLabBusy(false);
        }
    }

    private async Task LoadId65BlankLabAsync()
    {
        if (_id65BlankLabBusy)
            return;

        SetId65BlankLabBusy(true);
        try
        {
            if (!await TryValidateId65BlankLabAsync())
            {
                _statusText.Text =
                    $"Build and validate the ID65 locked base before loading the lab. {_id65BlankLabValidationFailure}".Trim();
                return;
            }

            await BuildId65BlankLabCacheAsync(_id65BlankLabPaths!, overwrite: false);
            AdmitValidatedId65BlankLabToCatalog();
            LevelDefinition lab = _catalog.FindByKey(UnusedLevel65BlankLevelLabProfileRegistry.Key)
                ?? throw new InvalidOperationException("The validated ID65 lab was not admitted to the in-memory catalog.");
            LevelDefinition? previousLevel = _currentLevel;
            await SelectLevelFromPickerAsync(lab);
            if (!IsCurrentId65BlankLab())
            {
                if (ReferenceEquals(_currentLevel, previousLevel) ||
                    string.Equals(_currentLevel?.Key, previousLevel?.Key, StringComparison.OrdinalIgnoreCase))
                {
                    _statusText.Text =
                        "ID65 Lab load canceled; the current level, camera, selection, and unsaved edits were preserved.";
                }
                return;
            }
            _statusText.Text =
                $"Loaded {lab.DisplayName} from its locked base: {_currentGeometry?.Polygons.Count ?? 0} terrain faces and {_currentMobys.Count} resident Mobys for inspection.";
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Could not load the ID65 lab: {ex.Message}";
        }
        finally
        {
            SetId65BlankLabBusy(false);
        }
    }

    private async Task SaveId65BlankLabWorkspaceAsync()
    {
        _ = await SaveId65BlankLabWorkspaceCoreAsync(announce: true);
    }

    private async Task CreateId65BlankLabDisposableCueAsync()
    {
        if (_id65BlankLabBusy)
            return;
        if (!IsCurrentId65BlankLab())
        {
            _statusText.Text = "Load the validated ID65 lab before creating its disposable test CUE.";
            return;
        }

        SetId65BlankLabBusy(true);
        try
        {
            if (!await TryValidateId65BlankLabAsync())
                throw new InvalidDataException(_id65BlankLabValidationFailure);
            if (_currentGeometry == null)
                throw new InvalidOperationException("The ID65 lab terrain is not loaded.");

            if (!await SaveId65BlankLabWorkspaceCoreAsync(announce: false))
            {
                throw new InvalidOperationException(
                    "The ID65 research workspace contains an unsupported edit and was not saved.");
            }
            if (!TryValidateId65BlankLabTerrainForDisposableCue(
                    _currentGeometry,
                    out string terrainBlocker))
            {
                throw new InvalidOperationException(terrainBlocker);
            }
            if (TryGetId65BlankLabUnsupportedArtifact(out string unsupportedArtifact))
            {
                throw new InvalidOperationException(
                    $"This first disposable writer accepts only existing HP Z terrain edits. {unsupportedArtifact}");
            }

            string terrainEditsPath = Id65BlankLabTerrainEditsPath();
            if (!TerrainEditFileHasEdits(terrainEditsPath))
                throw new InvalidOperationException("Make and save at least one existing HP Z terrain edit first.");

            string testKey = CreateId65BlankLabDisposableTestKey();
            UnusedLevel65BlankLevelLabTerrainTestResult result =
                await UnusedLevel65BlankLevelLabTerrainTestExporter.CreateDisposableTerrainTestAsync(
                    new UnusedLevel65BlankLevelLabTerrainTestRequest(
                        WorkspaceContainerPath: _workspace.RootPath,
                        TerrainEditsPath: terrainEditsPath,
                        TestKey: testKey,
                        TestDisplayName: "ID65 Blank-Level Lab HP-Z terrain test",
                        ReplaceExistingTest: false,
                        RequestFinderReveal: OperatingSystem.IsMacOS()));
            _id65BlankLabRevealPath = result.Terrain.OutputCuePath;
            RefreshId65BlankLabUi();
            RevealId65BlankLabOutput();
            _statusText.Text =
                $"Created atomic disposable ID65 test CUE {Path.GetFileName(result.Terrain.OutputCuePath)} with {result.Terrain.Plan.PatchCount} exact HP-Z/collision patch(es). " +
                $"Checklist (all four load codes): {Path.GetFileName(result.Paths.RuntimeChecklistPath)}. " +
                $"Readback verified across {result.VerifiedRawSectorCount} raw sector(s); BIN SHA-256 {result.OutputImageSha256}.";
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Could not create an ID65 disposable test CUE: {ex.Message}";
        }
        finally
        {
            SetId65BlankLabBusy(false);
        }
    }

    private void RevealId65BlankLabOutput()
    {
        if (string.IsNullOrWhiteSpace(_id65BlankLabRevealPath) ||
            !File.Exists(_id65BlankLabRevealPath))
        {
            _statusText.Text = "Create a safely routeable disposable ID65 test CUE before revealing it.";
            return;
        }

        try
        {
            RevealFileInPlatformFileManager(_id65BlankLabRevealPath);
            string fileManager = OperatingSystem.IsMacOS()
                ? "Finder"
                : OperatingSystem.IsWindows() ? "Explorer" : "the file manager";
            _statusText.Text = $"Revealed the exact ID65 test CUE in {fileManager}: {_id65BlankLabRevealPath}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _statusText.Text = $"Could not reveal the ID65 test CUE: {ex.Message}";
        }
    }

    private void RefreshId65BlankLabUi()
    {
        bool valid = _id65BlankLabManifest != null && _id65BlankLabPaths != null;
        bool active = IsCurrentId65BlankLab();
        bool hasTerrain = TerrainEditFileHasEdits(Id65BlankLabTerrainEditsPath());
        bool hasUnsupported = TryGetId65BlankLabUnsupportedArtifact(out string unsupported);
        _id65BlankLabStatusText.Text = valid
            ? "Validated evidence-bound lab. ID65 is admitted only in memory; the retail catalog file remains exactly 35 levels. Normal Create BIN never includes this lab."
            : "Setup required: choose the clean Spyro CUE/BIN, then build the versioned locked base. " +
              "ID65 stays out of the level picker until its manifest and base pass exact validation." +
              (string.IsNullOrWhiteSpace(_id65BlankLabValidationFailure)
                  ? ""
                  : $" Validation: {_id65BlankLabValidationFailure}");
        _id65BlankLabLockedBaseText.Text = valid
            ? $"Profile v{_id65BlankLabManifest!.ProfileVersion}; exact BIN {_id65BlankLabManifest.LockedBaseImageSha256[..12]}...; {Path.GetFileName(_id65BlankLabPaths!.LockedBaseCuePath)}."
            : "Not built or not validated.";
        _id65BlankLabAuthoredLayerText.Text = valid
            ? $"Isolated by versioned manifest + unique key '{UnusedLevel65BlankLevelLabProfileRegistry.Key}'. " +
              $"Saved HP-Z terrain: {(hasTerrain ? "present" : "none")}." +
              (hasUnsupported ? $" Disposable writer blocked: {unsupported}" : "")
            : "No admitted lab workspace. Normal Create BIN remains separate.";

        if (_id65BlankLabBuildButton != null)
            _id65BlankLabBuildButton.IsEnabled = !_id65BlankLabBusy;
        if (_id65BlankLabLoadButton != null)
            _id65BlankLabLoadButton.IsEnabled = valid && !_id65BlankLabBusy;
        if (_id65BlankLabSaveButton != null)
            _id65BlankLabSaveButton.IsEnabled = valid && active && !_id65BlankLabBusy;
        if (_id65BlankLabCreateCueButton != null)
            _id65BlankLabCreateCueButton.IsEnabled =
                valid && active && !_id65BlankLabBusy && !hasUnsupported &&
                (hasTerrain || HasUnsavedTerrainEdits());
        if (_id65BlankLabRevealButton != null)
            _id65BlankLabRevealButton.IsEnabled =
                !_id65BlankLabBusy && File.Exists(_id65BlankLabRevealPath);
    }

    private async Task<bool> SaveId65BlankLabWorkspaceCoreAsync(bool announce)
    {
        if (!IsCurrentId65BlankLab() || _currentGeometry == null)
        {
            if (announce)
                _statusText.Text = "Load the validated ID65 lab before saving its research workspace.";
            return false;
        }
        if (HasUnsavedMobyEdits() ||
            _currentMobys.Any(moby => moby.HasAnyEdit) ||
            HasUnsavedNativeMovementEdits() ||
            HasUnsavedDragonRunToEdits())
        {
            _statusText.Text =
                "ID65 Lab save refused: resident Mobys and native movement records are inspection-only. Undo the object/native movement mutation or reload the Lab; the editor did not mark it saved.";
            return false;
        }
        if (!TryValidateId65BlankLabTerrainEdits(
                _currentGeometry,
                requireAtLeastOneEdit: false,
                out string terrainBlocker))
        {
            _statusText.Text = $"ID65 Lab save refused: {terrainBlocker}";
            return false;
        }
        if (!await TryValidateId65BlankLabAsync())
        {
            if (announce)
                _statusText.Text = $"Could not validate the ID65 research workspace: {_id65BlankLabValidationFailure}";
            return false;
        }

        int terrainCount = await PersistCurrentTerrainEditsAsync();
        RefreshCurrentLevelDetails();
        RefreshId65BlankLabUi();
        if (announce)
        {
            _statusText.Text =
                $"Saved {terrainCount} ID65 terrain edit(s) under the unique versioned lab identity. " +
                "Resident Mobys remain inspection-only, and this authored layer is excluded from normal Create BIN.";
        }
        return true;
    }

    private async Task<bool> TryValidateId65BlankLabAsync()
    {
        if (Id65BlankLabValidationOverrideForTesting != null)
            return await Id65BlankLabValidationOverrideForTesting();

        _id65BlankLabPaths ??=
            UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(_workspace.RootPath);
        try
        {
            _id65BlankLabManifest = await UnusedLevel65BlankLevelLabBootstrapper
                .ValidatePublishedWorkspaceAsync(_id65BlankLabPaths);
            _id65BlankLabValidationFailure = "";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            _id65BlankLabManifest = null;
            _id65BlankLabValidationFailure = ex.Message;
            return false;
        }
    }

    private async Task BuildId65BlankLabCacheAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        bool overwrite)
    {
        await UnusedLevel65BlankLevelLabBootstrapper.ValidatePublishedWorkspaceAsync(paths);
        string cacheDirectory = Id65BlankLabCacheDirectory(paths);
        string wadAnalysisPath = Id65BlankLabWadAnalysisPath(paths);
        string overlayPath = Id65BlankLabOverlayPath(paths);
        string mobyPath = Id65BlankLabMobyCachePath(paths);
        string sourceSearchPath = Id65BlankLabSourceSearchPath(paths);
        Directory.CreateDirectory(cacheDirectory);

        if (overwrite || !File.Exists(wadAnalysisPath))
            await WadAnalysisBuilder.BuildAsync(paths.LockedBaseImagePath, wadAnalysisPath);
        if (overwrite || !File.Exists(overlayPath))
        {
            await SourceSceneOverlayExporter.ExportAsync(
                paths.LockedBaseImagePath,
                wadAnalysisPath,
                UnusedLevel65BlankLevelLabProfileRegistry.Definition,
                overlayPath);
        }
        GeometryCacheHealthIssue? issue = GeometryCacheHealth.InspectOverlay(
            UnusedLevel65BlankLevelLabProfileRegistry.Key,
            overlayPath);
        if (issue?.BlocksLoading == true)
            throw new InvalidDataException(issue.Message);
        GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
        if (overwrite || !File.Exists(mobyPath))
        {
            await SourceMobyCacheBuilder.BuildAsync(
                paths.LockedBaseImagePath,
                UnusedLevel65BlankLevelLabProfileRegistry.Definition,
                mobyPath);
        }
        _ = MobyLoader.LoadCached(mobyPath).Count;
        if (overwrite || !File.Exists(sourceSearchPath))
        {
            TerrainSourceSearchResult search = await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(
                new SourceDerivedTerrainSourceSearchRequest(
                    paths.LockedBaseImagePath,
                    sourceSearchPath,
                    UnusedLevel65BlankLevelLabProfileRegistry.Definition,
                    geometry));
            if (search.Report.SectorCount <= 0 ||
                search.Report.MatchedSectorCount != search.Report.SectorCount ||
                search.Report.AmbiguousSectorCount != 0 ||
                search.Report.MissingSectorCount != 0)
            {
                throw new InvalidDataException("The ID65 locked-base terrain source-search map is incomplete.");
            }
        }
    }

    private TerrainGeometryLoadData LoadId65BlankLabGeometryData(bool includeAuthoredTerrainEdits = true)
    {
        if (!TryGetValidatedId65BlankLabForLoad(out UnusedLevel65BlankLevelLabWorkspacePaths? paths, out string error) ||
            paths == null)
        {
            return new TerrainGeometryLoadData(null, 0, error);
        }

        try
        {
            string overlayPath = Id65BlankLabOverlayPath(paths);
            if (!File.Exists(overlayPath))
                return new TerrainGeometryLoadData(null, 0, "ID65 locked-base editor cache is missing; use Load Lab to rebuild it.");
            GeometryCacheHealthIssue? issue = GeometryCacheHealth.InspectOverlay(
                UnusedLevel65BlankLevelLabProfileRegistry.Key,
                overlayPath);
            if (issue?.BlocksLoading == true)
                throw new InvalidDataException(issue.Message);
            GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
            if (includeAuthoredTerrainEdits)
            {
                TerrainMaterialClassifier.Apply(
                    UnusedLevel65BlankLevelLabProfileRegistry.Key,
                    _workspace.RootPath,
                    geometry);
            }
            int edits = includeAuthoredTerrainEdits
                ? TerrainEditStore.Load(Id65BlankLabTerrainEditsPath(), geometry.Polygons)
                : 0;
            return new TerrainGeometryLoadData(
                geometry,
                edits,
                "Loaded only from the validated ID65 locked base; retail source fallback is disabled.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or JsonException)
        {
            return new TerrainGeometryLoadData(null, 0, $"Could not load the ID65 locked-base terrain: {ex.Message}");
        }
    }

    private MobyLoadData LoadId65BlankLabMobyData()
    {
        if (!TryGetValidatedId65BlankLabForLoad(out UnusedLevel65BlankLevelLabWorkspacePaths? paths, out _) ||
            paths == null)
        {
            return new MobyLoadData([], 0, default);
        }

        try
        {
            string mobyPath = Id65BlankLabMobyCachePath(paths);
            if (!File.Exists(mobyPath))
                return new MobyLoadData([], 0, default);
            List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
            MobyMetadataResult metadata = MobyMetadataEnricher.Apply(
                _workspace,
                UnusedLevel65BlankLevelLabProfileRegistry.Key,
                mobys);
            MobyRelationshipRepair.RepairChestContentLinks(
                UnusedLevel65BlankLevelLabProfileRegistry.Key,
                mobys);
            return new MobyLoadData(mobys, 0, metadata);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or JsonException)
        {
            Debug.WriteLine(ex);
            return new MobyLoadData([], 0, default);
        }
    }

    private bool TryGetValidatedId65BlankLabForLoad(
        out UnusedLevel65BlankLevelLabWorkspacePaths? paths,
        out string error)
    {
        paths = _id65BlankLabPaths ??
            UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(_workspace.RootPath);
        error = "";
        try
        {
            _id65BlankLabManifest = UnusedLevel65BlankLevelLabBootstrapper
                .ValidatePublishedWorkspaceAsync(paths)
                .GetAwaiter()
                .GetResult();
            _id65BlankLabPaths = paths;
            _id65BlankLabValidationFailure = "";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            _id65BlankLabManifest = null;
            _id65BlankLabValidationFailure = ex.Message;
            error = $"The ID65 locked base failed validation: {ex.Message}";
            return false;
        }
    }

    private void AdmitValidatedId65BlankLabToCatalog()
    {
        _catalog = UnusedLevel65BlankLevelLabProfileRegistry.AugmentCatalog(_retailCatalog);
        LevelDefinition? selected = _currentLevel == null
            ? null
            : _catalog.FindByKey(_currentLevel.Key);
        _syncingLevelSelection = true;
        try
        {
            _levelList.ItemsSource = null;
            _levelJumpBox.ItemsSource = null;
            _levelList.ItemsSource = _catalog.Levels;
            _levelJumpBox.ItemsSource = _catalog.Levels;
        }
        finally
        {
            _syncingLevelSelection = false;
        }
        SyncLevelPickers(selected);
        RefreshId65BlankLabUi();
    }

    private void SetId65BlankLabBusy(bool busy)
    {
        _id65BlankLabBusy = busy;
        RefreshId65BlankLabUi();
    }

    private bool IsCurrentId65BlankLab() =>
        UnusedLevel65BlankLevelLabProfileRegistry.IsLabLevel(_currentLevel);

    private static bool IsId65BlankLabKey(string levelKey) =>
        LevelCatalog.NormalizeKey(levelKey) ==
        LevelCatalog.NormalizeKey(UnusedLevel65BlankLevelLabProfileRegistry.Key);

    private bool IsId65BlankLabObjectInspectionOnly() =>
        IsCurrentId65BlankLab() &&
        UnusedLevel65BlankLevelLabProfileRegistry.Capabilities.ObjectMutation ==
        UnusedLevel65BlankLevelLabCapabilityState.Unavailable;

    private string Id65BlankLabTerrainEditsPath() =>
        Path.Combine(
            (_id65BlankLabPaths ??
             UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(_workspace.RootPath))
                .AuthoredEditsDirectoryPath,
            $"{UnusedLevel65BlankLevelLabProfileRegistry.Key}-terrain-edits.json");

    private static string Id65BlankLabCacheDirectory(UnusedLevel65BlankLevelLabWorkspacePaths paths) =>
        Path.Combine(paths.RootPath, "editor-cache");

    private static string Id65BlankLabWadAnalysisPath(UnusedLevel65BlankLevelLabWorkspacePaths paths) =>
        Path.Combine(paths.RootPath, "locked-base-wad-analysis.json");

    private static string Id65BlankLabOverlayPath(UnusedLevel65BlankLevelLabWorkspacePaths paths) =>
        Path.Combine(
            Id65BlankLabCacheDirectory(paths),
            $"{UnusedLevel65BlankLevelLabProfileRegistry.Key}-runtime-scene-editor-overlay.json");

    private static string Id65BlankLabMobyCachePath(UnusedLevel65BlankLevelLabWorkspacePaths paths) =>
        Path.Combine(
            Id65BlankLabCacheDirectory(paths),
            $"{UnusedLevel65BlankLevelLabProfileRegistry.Key}-mobys.json");

    private static string Id65BlankLabSourceSearchPath(UnusedLevel65BlankLevelLabWorkspacePaths paths) =>
        Path.Combine(
            Id65BlankLabCacheDirectory(paths),
            $"{UnusedLevel65BlankLevelLabProfileRegistry.Key}-runtime-terrain-source-search-native.json");

    private bool TryGetId65BlankLabUnsupportedArtifact(out string reason)
    {
        string key = UnusedLevel65BlankLevelLabProfileRegistry.Key;
        string objectEditsPath = Path.Combine(_workspace.RootPath, $"{key}-native-edits.json");
        if (NativeEditFileHasEdits(objectEditsPath) || NativeEditFileHasEdits(NativeMobyPathEditPath(key)))
        {
            reason = "Resident Moby/path mutation is present but this profile permits inspection only.";
            return true;
        }
        if (CustomTerrainTextureFileHasTextures(CustomTerrainTextureStore.ManifestPath(_workspace.RootPath, key)))
        {
            reason = "Custom/imported terrain art needs a proved ID65 destination profile.";
            return true;
        }
        if (File.Exists(NativeTerrainTextureRelocationEditStore.ManifestPath(_workspace.RootPath, key)))
        {
            reason = "Portable cross-level terrain textures need a proved ID65 destination profile.";
            return true;
        }

        reason = "";
        return false;
    }

    private bool Id65BlankLabHasAnyAuthoredArtifacts()
    {
        string key = UnusedLevel65BlankLevelLabProfileRegistry.Key;
        string terrainEditsPath = Id65BlankLabTerrainEditsPath();
        return TerrainEditFileHasEdits(terrainEditsPath) ||
            NativeEditFileHasEdits(Path.Combine(_workspace.RootPath, $"{key}-native-edits.json")) ||
            NativeEditFileHasEdits(NativeMobyPathEditPath(key)) ||
            CustomTerrainTextureFileHasTextures(CustomTerrainTextureStore.ManifestPath(_workspace.RootPath, key)) ||
            File.Exists(NativeTerrainTextureRelocationEditStore.ManifestPath(_workspace.RootPath, key)) ||
            (_id65BlankLabPaths != null && Directory.Exists(_id65BlankLabPaths.AuthoredEditsDirectoryPath) &&
             Directory.EnumerateFiles(_id65BlankLabPaths.AuthoredEditsDirectoryPath, "*", SearchOption.AllDirectories)
                 .Any(path => !string.Equals(
                     Path.GetFullPath(path),
                     Path.GetFullPath(terrainEditsPath),
                     StringComparison.OrdinalIgnoreCase)));
    }

    private bool TryGetId65BlankLabNormalCreateBinBlockReason(out string reason)
    {
        if (IsCurrentId65BlankLab())
        {
            reason =
                "Normal Create BIN is unavailable while ID65 Blank-Level Lab is loaded. Use Create Disposable Test CUE in the ID65 Lab disclosure.";
            return true;
        }
        if (Id65BlankLabHasAnyAuthoredArtifacts())
        {
            reason =
                "Normal Create BIN is blocked because the isolated ID65 lab has authored research edits that the retail all-saved-edits writer must neither omit nor include. Load ID65 and use its disposable test writer, or restore its research layer first.";
            return true;
        }

        reason = "";
        return false;
    }

    private static bool TryValidateId65BlankLabTerrainForDisposableCue(
        GeometryCandidate geometry,
        out string reason) =>
        TryValidateId65BlankLabTerrainEdits(
            geometry,
            requireAtLeastOneEdit: true,
            out reason);

    private static bool TryValidateId65BlankLabTerrainEdits(
        GeometryCandidate geometry,
        bool requireAtLeastOneEdit,
        out string reason)
    {
        TerrainPolygon[] edits = geometry.Polygons.Where(polygon => polygon.IsTerrainEdited).ToArray();
        if (requireAtLeastOneEdit && edits.Length == 0)
        {
            reason = "Make at least one terrain edit before creating a disposable test CUE.";
            return false;
        }
        TerrainPolygon? unsupported = edits.FirstOrDefault(polygon =>
            !string.Equals(polygon.Detail, "hp", StringComparison.OrdinalIgnoreCase) ||
            !polygon.HasHeightEdit ||
            polygon.HasPositionEdit ||
            polygon.HasTextureEdit ||
            polygon.HasTextureVisualEdit ||
            polygon.HasStructureEdit ||
            polygon.HasSurfaceBehaviorEdit);
        if (unsupported != null)
        {
            reason =
                $"Face {unsupported.RuntimeKey} is outside the first safe gate. Disposable CUEs currently accept existing high-detail (HP), Z-only edits; Add/remove, LP, XY, surface, and texture edits stay in the research workspace.";
            return false;
        }

        reason = "";
        return true;
    }

    private static string CreateId65BlankLabDisposableTestKey()
    {
        string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        return $"id65-hp-z-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{nonce}";
    }

    private bool TryBlockId65ObjectMutation(string action)
    {
        if (!IsId65BlankLabObjectInspectionOnly())
            return false;

        _statusText.Text =
            $"ID65 Lab blocked {action}: resident Mobys and native movement records are inspection-only in this profile.";
        return true;
    }

    private bool TryBlockId65UnsupportedTerrainMutation(
        TerrainPolygon? terrain,
        string action,
        bool movesXy = false,
        bool structural = false,
        bool textureOrSurface = false)
    {
        if (!IsCurrentId65BlankLab())
            return false;

        if (structural || movesXy || textureOrSurface)
        {
            _statusText.Text =
                $"ID65 Lab blocked {action}: this first public writer accepts existing high-detail (HP), Z-only edits. Add/remove, LP, XY, surface, and texture mutations remain unavailable.";
            return true;
        }
        if (terrain == null ||
            !string.Equals(terrain.Detail, "hp", StringComparison.OrdinalIgnoreCase) ||
            terrain.IsTerrainRemoved ||
            terrain.IsTerrainAddClone)
        {
            _statusText.Text =
                $"ID65 Lab blocked {action}: only existing high-detail (HP) faces may receive Z-height edits.";
            return true;
        }

        return false;
    }

    private void ResetId65BlankLabIncompatibleModes()
    {
        if (!IsCurrentId65BlankLab())
            return;

        _pendingMobyAdd = null;
        _viewport.ObjectPlacementMode = false;
        StopTerrainTexturePaintMode(announce: false);
        _viewport.TerrainTexturePaintMode = false;
        _terrainLookClipboard = null;
        _terrainJoinSeamsBox.IsChecked = false;
        _terrainPlayableOnlyBox.IsChecked = true;
        _activeFlattenTerrainZ = null;
        _terrainBrushModeBox.SelectedItem = TerrainBrushModeOption.All[0];
        _viewport.TerrainBrushAction = TerrainBrushAction.Off;
        ClearTerrainBrushUndoHistory();
    }

    private static async Task<Id65BlankLabAuthoredLayerSnapshot> CaptureId65BlankLabAuthoredLayerAsync(
        string directoryPath)
    {
        string fullDirectory = Path.GetFullPath(directoryPath);
        if (!Directory.Exists(fullDirectory))
            return new Id65BlankLabAuthoredLayerSnapshot(DirectoryExisted: false, Files: []);

        List<string> files = [];
        foreach (string path in Directory.EnumerateFiles(fullDirectory, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            string relative = Path.GetRelativePath(fullDirectory, path).Replace('\\', '/');
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            string hash = Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
            files.Add($"{relative}|{stream.Length}|{hash}");
        }
        return new Id65BlankLabAuthoredLayerSnapshot(DirectoryExisted: true, Files: files);
    }

    private static void EnsureId65BlankLabAuthoredLayerPreserved(
        Id65BlankLabAuthoredLayerSnapshot before,
        Id65BlankLabAuthoredLayerSnapshot after)
    {
        if (!before.DirectoryExisted)
            return;
        if (!after.DirectoryExisted || !before.Files.SequenceEqual(after.Files, StringComparer.Ordinal))
        {
            throw new IOException(
                "The ID65 locked-base repair did not preserve the authored-edits layer exactly.");
        }
    }

    private static void RevealFileInPlatformFileManager(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The test CUE was not found.", fullPath);
        ProcessStartInfo startInfo;
        if (OperatingSystem.IsMacOS())
        {
            startInfo = new ProcessStartInfo("/usr/bin/open") { UseShellExecute = false };
            startInfo.ArgumentList.Add("-R");
            startInfo.ArgumentList.Add(fullPath);
        }
        else if (OperatingSystem.IsWindows())
        {
            startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
            startInfo.ArgumentList.Add($"/select,{fullPath}");
        }
        else
        {
            startInfo = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
            startInfo.ArgumentList.Add(Path.GetDirectoryName(fullPath) ?? fullPath);
        }
        Process.Start(startInfo);
    }

    internal Id65BlankLabUiSnapshot CaptureId65BlankLabUiSnapshotForTesting() => new(
        DisclosureTitle: Id65BlankLabDisclosureTitle,
        StatusText: _id65BlankLabStatusText.Text ?? "",
        LockedBaseText: _id65BlankLabLockedBaseText.Text ?? "",
        AuthoredLayerText: _id65BlankLabAuthoredLayerText.Text ?? "",
        CapabilityText: $"Terrain: {Id65BlankLabTerrainCapability}\nTextures: {Id65BlankLabTextureCapability}\nMobys: {Id65BlankLabMobyCapability}",
        RetailCatalogCount: _catalog.Levels.Count(level => level.LevelId != 65),
        HasId65CatalogLevel: _catalog.Levels.Any(level => level.LevelId == 65),
        BuildEnabled: _id65BlankLabBuildButton?.IsEnabled == true,
        LoadEnabled: _id65BlankLabLoadButton?.IsEnabled == true,
        SaveEnabled: _id65BlankLabSaveButton?.IsEnabled == true,
        CreateCueEnabled: _id65BlankLabCreateCueButton?.IsEnabled == true,
        RevealEnabled: _id65BlankLabRevealButton?.IsEnabled == true,
        RevealButtonText: _id65BlankLabRevealButton?.Content?.ToString() ?? "",
        RepairsInvalidWorkspace: true,
        DisposableExporterRoute: nameof(UnusedLevel65BlankLevelLabTerrainTestExporter));

    internal static string CreateId65BlankLabDisposableTestKeyForTesting() =>
        CreateId65BlankLabDisposableTestKey();

    internal async Task<string> AssertId65BlankLabMutationGuardsForTestingAsync()
    {
        if (_currentLevel == null || _currentGeometry == null || _currentMobys.Count == 0)
            throw new InvalidOperationException("The ID65 mutation-boundary probe requires one loaded retail level.");

        LevelDefinition originalLevel = _currentLevel;
        Moby originalSelectedMoby = _selectedMoby ?? _currentMobys.First(moby => !moby.IsEditorControl);
        TerrainPolygon? originalSelectedTerrain = _selectedTerrain;
        MobyClipboard? originalClipboard = _mobyClipboard;
        PendingMobyAdd? originalPending = _pendingMobyAdd;
        object? originalBrushMode = _terrainBrushModeBox.SelectedItem;
        TerrainBrushAction originalBrushAction = _viewport.TerrainBrushAction;
        bool originalTexturePaintMode = _viewport.TerrainTexturePaintMode;
        bool originalPlacementMode = _viewport.ObjectPlacementMode;
        bool? originalJoinSeams = _terrainJoinSeamsBox.IsChecked;
        bool? originalPlayableOnly = _terrainPlayableOnlyBox.IsChecked;
        string savedMobySignature = _savedMobyEditSignature;
        string savedNativeMovementSignature = _savedNativeMovementEditSignature;
        string savedDragonRunToSignature = _savedDragonRunToEditSignature;

        Moby moby = _currentMobys.First(candidate =>
            !candidate.IsEditorControl && !candidate.IsRemoved && !IsReleaseProtectedControlMoby(candidate));
        int hpIndex = _currentGeometry.Polygons.FindIndex(polygon =>
            !polygon.IsTerrainRemoved &&
            string.Equals(polygon.Detail, "hp", StringComparison.OrdinalIgnoreCase));
        if (hpIndex < 0)
            throw new InvalidOperationException("The ID65 mutation-boundary probe found no existing HP face.");
        TerrainPolygon hp = _currentGeometry.Polygons[hpIndex];
        int lpIndex = _currentGeometry.Polygons.FindIndex(polygon =>
            !polygon.IsTerrainRemoved &&
            string.Equals(polygon.Detail, "lp", StringComparison.OrdinalIgnoreCase));
        TerrainPolygon? lp = lpIndex >= 0 ? _currentGeometry.Polygons[lpIndex] : null;
        Vector3f originalMobyPosition = moby.Position;
        int originalMobyYaw = moby.YawByte;
        int originalMobyCount = _currentMobys.Count;
        string originalMobySignature = BuildMobyEditSignature(_currentMobys);
        NativePathNode? nativePathNode = _currentNativeMobyPaths
            .SelectMany(path => path.Nodes)
            .FirstOrDefault();
        NativeMobyPath? nativePath = nativePathNode == null
            ? null
            : _currentNativeMobyPaths.First(path => path.Nodes.Contains(nativePathNode));
        Vector3f? originalNativePathNodePosition = nativePathNode?.Position;
        NativeDragonRunToEdit? dragonRunTo = _currentDragonRunToEdits.FirstOrDefault();
        int? originalDragonRunToRawX = dragonRunTo?.RawX;
        int? originalDragonRunToRawY = dragonRunTo?.RawY;
        float[] originalHpZ = hp.TerrainVertexDeltas().ToArray();
        Vector2f[] originalHpXy = hp.TerrainVertexXYDeltas().ToArray();
        float[]? originalLpZ = lp?.TerrainVertexDeltas().ToArray();
        string labKey = UnusedLevel65BlankLevelLabProfileRegistry.Key;
        string objectEditsPath = Path.Combine(_workspace.RootPath, $"{labKey}-native-edits.json");
        string materialOverridesPath = Path.Combine(_workspace.RootPath, $"{labKey}-terrain-material-overrides.json");
        string customTexturesPath = CustomTerrainTextureStore.ManifestPath(_workspace.RootPath, labKey);
        string nativeRelocationsPath = NativeTerrainTextureRelocationEditStore.ManifestPath(_workspace.RootPath, labKey);
        string musicPlanPath = LevelMusicEditStore.PlanPath(
            _workspace.RootPath,
            UnusedLevel65BlankLevelLabProfileRegistry.Definition);
        string[] guardedMutationPaths =
        [
            objectEditsPath,
            Id65BlankLabTerrainEditsPath(),
            materialOverridesPath,
            customTexturesPath,
            nativeRelocationsPath,
            musicPlanPath
        ];
        Dictionary<string, byte[]?> guardedMutationFilesBefore = guardedMutationPaths.ToDictionary(
            path => path,
            path => File.Exists(path) ? File.ReadAllBytes(path) : null,
            StringComparer.OrdinalIgnoreCase);
        bool originalMobySnapDisabled = _mobyTerrainSnapDisabledKeys.Contains(MobyTerrainSnapKey(moby));
        string originalCustomTextureState = JsonSerializer.Serialize(_customTerrainTextures);
        string originalNativeRelocationState = JsonSerializer.Serialize(_nativeTerrainTextureRelocations);
        TerrainTexturePaintStageSnapshot originalHpPaintState = TerrainTexturePaintStageSnapshot.Capture(hp);

        try
        {
            _mobyClipboard = MobyClipboard.From(moby, originalLevel, terrainGroundOffset: null);
            _selectedMoby = moby;
            _selectedTerrain = hp;
            _selectedTerrainIndex = hpIndex;
            _currentLevel = UnusedLevel65BlankLevelLabProfileRegistry.Definition;

            _viewport.ObjectPlacementMode = true;
            _viewport.TerrainTexturePaintMode = true;
            _viewport.TerrainBrushAction = TerrainBrushAction.Raise;
            _terrainJoinSeamsBox.IsChecked = true;
            ResetId65BlankLabIncompatibleModes();
            if (_viewport.ObjectPlacementMode ||
                _viewport.TerrainTexturePaintMode ||
                _viewport.TerrainBrushAction != TerrainBrushAction.Off ||
                _terrainJoinSeamsBox.IsChecked != false)
            {
                throw new InvalidOperationException("Loading ID65 did not reset every incompatible placement/paint/seam mode.");
            }

            RefreshSelectedMobyZControls(moby);
            RefreshActionAvailability();
            if (_selectedMobyZSlider.IsEnabled ||
                _selectedMobySnapZBox.IsEnabled ||
                _terrainSelectedLinkedPaintButton == null ||
                _terrainSelectedLinkedPaintButton.IsEnabled)
            {
                throw new InvalidOperationException(
                    "ID65 left an object Z/snap or Paint Selected / Linked Sections control enabled.");
            }
            _selectedMobyZSlider.Value = Math.Clamp(
                moby.Position.Z + 1,
                _selectedMobyZSlider.Minimum,
                _selectedMobyZSlider.Maximum);
            ApplySelectedMobyZSliderValue();
            _selectedMobySnapZBox.IsChecked = _selectedMobySnapZBox.IsChecked != true;
            ApplySelectedMobyTerrainSnapChoice();
            _ = SnapSelectedMobyToCurrentTerrain(moby, out _);
            if (BuildMobyEditSignature(_currentMobys) != originalMobySignature ||
                _mobyTerrainSnapDisabledKeys.Contains(MobyTerrainSnapKey(moby)) != originalMobySnapDisabled ||
                _savedMobyEditSignature != savedMobySignature)
            {
                throw new InvalidOperationException(
                    "An ID65 selected-object Z/snap action mutated the selected or linked Mobys, snap state, or saved signature.");
            }

            string terrainSignatureBeforePaint = BuildTerrainEditSignature(_currentGeometry);
            await PaintSelectedTerrainFaceAsync();
            await ChooseTerrainTexturePaintBrushAsync();
            if (BuildTerrainEditSignature(_currentGeometry) != terrainSignatureBeforePaint ||
                TerrainTexturePaintStageSnapshot.Capture(hp) != originalHpPaintState ||
                JsonSerializer.Serialize(_customTerrainTextures) != originalCustomTextureState ||
                JsonSerializer.Serialize(_nativeTerrainTextureRelocations) != originalNativeRelocationState)
            {
                throw new InvalidOperationException(
                    "An ID65 selected/linked or adjacent direct terrain-paint route staged resident, cross-level, surface, or texture state.");
            }

            RefreshLevelMusicEditor(_currentLevel);
            if (_levelMusicTrackBox.IsEnabled ||
                _levelMusicSaveButton == null ||
                _levelMusicSaveButton.IsEnabled ||
                _levelMusicResetButton == null ||
                _levelMusicResetButton.IsEnabled ||
                !(_levelMusicDetails.Text ?? "").Contains("unavailable", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("ID65 did not disable and truthfully reset every Level Music control.");
            }
            MusicTrackEntry alternateTrack = MusicTrackCatalog.SelectableTracks.First(track =>
                track.TrackId != MusicTrackCatalog.GetNativeTrackId(_currentLevel));
            _levelMusicTrackBox.SelectedItem = alternateTrack;
            await SaveLevelMusicPlanAsync();
            ResetLevelMusicPlan();
            if (File.Exists(musicPlanPath) != (guardedMutationFilesBefore[musicPlanPath] != null) ||
                (File.Exists(musicPlanPath) &&
                 !File.ReadAllBytes(musicPlanPath).SequenceEqual(guardedMutationFilesBefore[musicPlanPath]!)) ||
                _levelMusicTrackBox.IsEnabled ||
                _levelMusicSaveButton == null ||
                _levelMusicSaveButton.IsEnabled ||
                _levelMusicResetButton == null ||
                _levelMusicResetButton.IsEnabled ||
                !(_statusText.Text ?? "").Contains("unavailable", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "An ID65 Level Music save/reset boundary changed its retail-root plan or exposed a false Create BIN claim.");
            }

            foreach ((string path, byte[]? contents) in guardedMutationFilesBefore)
            {
                bool matches = contents == null
                    ? !File.Exists(path)
                    : File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(contents);
                if (!matches)
                    throw new InvalidOperationException($"ID65 guarded mutation changed {Path.GetFileName(path)}.");
            }

            await EditMobyAsync(moby);
            await PasteMobyClipboardAtViewportAsync(_viewport.LastPointerPosition);
            MoveMobyFromViewport(new MobyMoveRequestedEventArgs(moby, 16, 0, 0));
            RotateMobyFromViewport(new MobyRotateRequestedEventArgs(moby, moby.YawByte + 1));
            RemoveSelectedMoby();
            if (_currentMobys.Count != originalMobyCount ||
                moby.Position != originalMobyPosition ||
                moby.YawByte != originalMobyYaw ||
                BuildMobyEditSignature(_currentMobys) != originalMobySignature)
            {
                throw new InvalidOperationException("An ID65 double-click/paste/viewport object boundary mutated an inspection-only Moby.");
            }

            if (nativePath != null && nativePathNode != null && originalNativePathNodePosition != null)
            {
                MoveNativePathNodeFromViewport(new ViewportNativePathNodeMoveRequestedEventArgs(
                    nativePath,
                    nativePathNode,
                    16,
                    0));
                if (nativePathNode.Position != originalNativePathNodePosition.Value)
                    throw new InvalidOperationException("An ID65 viewport gesture mutated an inspection-only native path node.");
            }
            if (dragonRunTo != null &&
                originalDragonRunToRawX != null &&
                originalDragonRunToRawY != null &&
                _currentDragonRunToViewportTargets.TryGetValue(
                    dragonRunTo.OwnerTrueIndex,
                    out ViewportDragonRunToTarget? dragonTarget))
            {
                MoveDragonRunToFromViewport(new ViewportDragonRunToMoveRequestedEventArgs(dragonTarget, 16, 0));
                if (dragonRunTo.RawX != originalDragonRunToRawX.Value ||
                    dragonRunTo.RawY != originalDragonRunToRawY.Value)
                {
                    throw new InvalidOperationException("An ID65 viewport gesture mutated an inspection-only dragon run-to record.");
                }
            }

            MoveTerrainFromViewport(new ViewportTerrainMoveRequestedEventArgs(hpIndex, hp, 16, 0, 0, false));
            StageTerrainRemovalFromViewport(hpIndex, hp);
            if (!hp.TerrainVertexDeltas().SequenceEqual(originalHpZ) ||
                !hp.TerrainVertexXYDeltas().SequenceEqual(originalHpXy) ||
                hp.IsTerrainRemoved || hp.IsTerrainAddClone)
            {
                throw new InvalidOperationException("An ID65 viewport XY/structural terrain boundary mutated the HP face.");
            }
            if (lp != null && originalLpZ != null)
            {
                MoveTerrainFromViewport(new ViewportTerrainMoveRequestedEventArgs(lpIndex, lp, 0, 0, 8, false));
                if (!lp.TerrainVertexDeltas().SequenceEqual(originalLpZ))
                    throw new InvalidOperationException("An ID65 viewport gesture mutated low-detail terrain.");
            }

            MoveTerrainFromViewport(new ViewportTerrainMoveRequestedEventArgs(hpIndex, hp, 0, 0, 8, false));
            if (hp.TerrainVertexDeltas().SequenceEqual(originalHpZ))
                throw new InvalidOperationException("The ID65 guard incorrectly blocked an existing HP Z-only viewport edit.");
            hp.ApplyTerrainVertexDeltas(originalHpZ);
            hp.ApplyTerrainVertexXYDeltas(originalHpXy);

            moby.Position = new Vector3f(originalMobyPosition.X + 1, originalMobyPosition.Y, originalMobyPosition.Z);
            bool saved = await SaveId65BlankLabWorkspaceCoreAsync(announce: true);
            if (saved || _savedMobyEditSignature != savedMobySignature || !HasUnsavedMobyEdits())
            {
                throw new InvalidOperationException(
                    "ID65 Lab Save marked an inspection-only Moby mutation as saved instead of failing closed.");
            }
            moby.Position = originalMobyPosition;

            if (nativePathNode != null && originalNativePathNodePosition != null)
            {
                nativePathNode.SetPosition(new Vector3f(
                    originalNativePathNodePosition.Value.X + 1,
                    originalNativePathNodePosition.Value.Y,
                    originalNativePathNodePosition.Value.Z));
                saved = await SaveId65BlankLabWorkspaceCoreAsync(announce: true);
                if (saved ||
                    _savedNativeMovementEditSignature != savedNativeMovementSignature ||
                    !HasUnsavedNativeMovementEdits())
                {
                    throw new InvalidOperationException(
                        "ID65 Lab Save marked an inspection-only native movement mutation as saved instead of failing closed.");
                }
                nativePathNode.SetPosition(originalNativePathNodePosition.Value);
            }

            return "disabled/direct object Z and terrain-snap controls preserved selected+linked Mobys, snap state, signatures, and files; selected/linked plus adjacent direct paint routes preserved resident/cross-level texture/surface state and files; Level Music controls/save/reset stayed unavailable and write-free; double-click, paste, object/native-movement viewport mutations, object move/rotate/remove, LP/XY/structural terrain gestures, mode reset, and inspection-only save refusal passed; existing HP Z remained editable";
        }
        finally
        {
            moby.Position = originalMobyPosition;
            moby.YawByte = originalMobyYaw;
            if (nativePathNode != null && originalNativePathNodePosition != null)
                nativePathNode.SetPosition(originalNativePathNodePosition.Value);
            if (dragonRunTo != null && originalDragonRunToRawX != null && originalDragonRunToRawY != null)
                dragonRunTo.SetRawEndpoint(originalDragonRunToRawX.Value, originalDragonRunToRawY.Value);
            hp.ApplyTerrainVertexDeltas(originalHpZ);
            hp.ApplyTerrainVertexXYDeltas(originalHpXy);
            if (lp != null && originalLpZ != null)
                lp.ApplyTerrainVertexDeltas(originalLpZ);
            _currentLevel = originalLevel;
            _selectedMoby = originalSelectedMoby;
            _selectedTerrain = originalSelectedTerrain;
            _mobyClipboard = originalClipboard;
            _pendingMobyAdd = originalPending;
            _terrainBrushModeBox.SelectedItem = originalBrushMode;
            _viewport.TerrainBrushAction = originalBrushAction;
            _viewport.TerrainTexturePaintMode = originalTexturePaintMode;
            _viewport.ObjectPlacementMode = originalPlacementMode;
            _terrainJoinSeamsBox.IsChecked = originalJoinSeams;
            _terrainPlayableOnlyBox.IsChecked = originalPlayableOnly;
            _savedMobyEditSignature = savedMobySignature;
            _savedNativeMovementEditSignature = savedNativeMovementSignature;
            _savedDragonRunToEditSignature = savedDragonRunToSignature;
            foreach ((string path, byte[]? contents) in guardedMutationFilesBefore)
            {
                if (contents == null)
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _workspace.RootPath);
                    File.WriteAllBytes(path, contents);
                }
            }
            RefreshLevelMusicEditor(originalLevel);
        }
    }

    internal async Task<string> AssertId65BlankLabGuardedTransitionForTestingAsync()
    {
        if (_currentLevel == null || _currentGeometry == null || _currentMobys.Count == 0)
            throw new InvalidOperationException("The ID65 transition probe requires one loaded retail level.");
        LevelDefinition retail = _currentLevel;
        LevelDefinition lab = _catalog.FindByKey(UnusedLevel65BlankLevelLabProfileRegistry.Key)
            ?? throw new InvalidOperationException("The ID65 transition probe requires the admitted Lab row.");
        Moby moby = _currentMobys.First(candidate => !candidate.IsEditorControl && !candidate.IsRemoved);
        TerrainPolygon terrain = _currentGeometry.Polygons.First(candidate =>
            !candidate.IsTerrainRemoved &&
            string.Equals(candidate.Detail, "hp", StringComparison.OrdinalIgnoreCase));
        Vector3f originalMobyPosition = moby.Position;
        float[] originalTerrainDeltas = terrain.TerrainVertexDeltas().ToArray();
        string terrainEditsPath = Path.Combine(_workspace.RootPath, $"{retail.Key}-terrain-edits.json");
        string mobyEditsPath = Path.Combine(_workspace.RootPath, $"{retail.Key}-native-edits.json");
        string labKey = UnusedLevel65BlankLevelLabProfileRegistry.Key;
        string labAuthoredTerrainPath = Id65BlankLabTerrainEditsPath();
        string labRetailTerrainPath = Path.Combine(_workspace.RootPath, $"{labKey}-terrain-edits.json");
        string labRetailMobyPath = Path.Combine(_workspace.RootPath, $"{labKey}-native-edits.json");
        string labRetailNativePath = NativeMobyPathEditPath(labKey);
        string[] labPathsToPreserve =
        [
            labAuthoredTerrainPath,
            labRetailTerrainPath,
            labRetailMobyPath,
            labRetailNativePath
        ];
        Dictionary<string, byte[]?> preservedLabPaths = labPathsToPreserve.ToDictionary(
            path => path,
            path => File.Exists(path) ? File.ReadAllBytes(path) : null,
            StringComparer.OrdinalIgnoreCase);

        LevelLoadData CreateCleanLockedCacheFixture()
        {
            string[] retailEditPaths =
            [
                terrainEditsPath,
                mobyEditsPath,
                NativeMobyPathEditPath(retail.Key)
            ];
            Dictionary<string, byte[]?> snapshots = retailEditPaths.ToDictionary(
                path => path,
                path => File.Exists(path) ? File.ReadAllBytes(path) : null,
                StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string path in retailEditPaths)
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                return LoadLevelData(retail.Key);
            }
            finally
            {
                foreach ((string path, byte[]? contents) in snapshots)
                {
                    if (contents != null)
                        File.WriteAllBytes(path, contents);
                }
            }
        }

        try
        {
            moby.Position = new Vector3f(originalMobyPosition.X + 5, originalMobyPosition.Y, originalMobyPosition.Z);
            float[] changedTerrain = originalTerrainDeltas.ToArray();
            changedTerrain[0] += 7;
            terrain.ApplyTerrainVertexDeltas(changedTerrain);
            _selectedMoby = moby;
            _selectedTerrain = null;
            _selectedTerrainIndex = -1;
            _viewport.SelectMoby(moby, focus: false);
            _viewport.SetFlyGameCameraForTesting(413, 829, 127, 0.42, -0.18);
            EditorShellSessionSnapshot beforeCancel = CaptureEditorShellSessionSnapshotForTesting();
            if (!beforeCancel.HasUnsavedMobyEdits || !beforeCancel.HasUnsavedTerrainEdits)
                throw new InvalidOperationException("The ID65 transition probe did not stage both retail edit types.");

            UnsavedTerrainDecisionOverrideForTesting = (_, _) => Task.FromResult("Cancel");
            await SelectLevelFromPickerAsync(lab);
            EditorShellSessionSnapshot afterCancel = CaptureEditorShellSessionSnapshotForTesting();
            if (!ReferenceEquals(afterCancel.CurrentLevel, beforeCancel.CurrentLevel) ||
                !ReferenceEquals(afterCancel.CurrentGeometry, beforeCancel.CurrentGeometry) ||
                !ReferenceEquals(afterCancel.SelectedMoby, beforeCancel.SelectedMoby) ||
                afterCancel.Navigation != beforeCancel.Navigation ||
                afterCancel.CurrentMobyEditSignature != beforeCancel.CurrentMobyEditSignature ||
                afterCancel.CurrentTerrainEditSignature != beforeCancel.CurrentTerrainEditSignature ||
                !afterCancel.HasUnsavedMobyEdits || !afterCancel.HasUnsavedTerrainEdits)
            {
                throw new InvalidOperationException(
                    "Canceling ID65 load did not preserve the retail level, camera, selection, and both unsaved edit layers.");
            }

            UnsavedTerrainDecisionOverrideForTesting = (_, _) => Task.FromResult("Save");
            await SelectLevelFromPickerAsync(lab);
            if (!IsCurrentId65BlankLab() ||
                !TerrainEditFileHasEdits(terrainEditsPath) ||
                !NativeEditFileHasEdits(mobyEditsPath))
            {
                throw new InvalidOperationException(
                    "The guarded ID65 transition did not persist retail terrain and Moby edits before loading the Lab.");
            }

            ApplyLoadedLevel(lab, CreateCleanLockedCacheFixture());
            Id65BlankLabReloadOverrideForTesting = _ =>
                Task.FromResult(CreateCleanLockedCacheFixture());
            _id65BlankLabManifest = UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(
                _id65BlankLabPaths ??
                UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(_workspace.RootPath));
            _id65BlankLabValidationFailure = "";
            Id65BlankLabValidationOverrideForTesting = () => Task.FromResult(true);
            if (_currentGeometry == null || _currentMobys.Count == 0)
                throw new InvalidOperationException("The ID65 test fixture could not seed a locked-cache-equivalent scene.");
            if (File.Exists(labAuthoredTerrainPath))
                throw new InvalidOperationException("The isolated UI smoke unexpectedly began with saved ID65 terrain edits.");

            byte[] retailSentinel = JsonSerializer.SerializeToUtf8Bytes(new
            {
                editCount = 0,
                edits = Array.Empty<object>(),
                marker = "ID65 generic retail path must remain untouched"
            });
            foreach (string path in new[] { labRetailTerrainPath, labRetailMobyPath, labRetailNativePath })
                File.WriteAllBytes(path, retailSentinel);

            TerrainPolygon labTerrain = _currentGeometry.Polygons.First(candidate =>
                !candidate.IsTerrainRemoved &&
                string.Equals(candidate.Detail, "hp", StringComparison.OrdinalIgnoreCase));
            Moby labMoby = _currentMobys.First(candidate => !candidate.IsEditorControl && !candidate.IsRemoved);
            float[] lockedLabTerrainDeltas = labTerrain.TerrainVertexDeltas().ToArray();
            Vector3f lockedLabMobyPosition = labMoby.Position;
            string savedLabTerrainSignature = _savedTerrainEditSignature;
            string savedLabMobySignature = _savedMobyEditSignature;
            float[] unsupportedTerrainDeltas = lockedLabTerrainDeltas.ToArray();
            unsupportedTerrainDeltas[0] += 9;
            labTerrain.ApplyTerrainVertexDeltas(unsupportedTerrainDeltas);
            labMoby.Position = new Vector3f(
                lockedLabMobyPosition.X + 1,
                lockedLabMobyPosition.Y,
                lockedLabMobyPosition.Z);
            string stagedLabTerrainSignature = BuildTerrainEditSignature(_currentGeometry);
            string stagedLabMobySignature = BuildMobyEditSignature(_currentMobys);

            UnsavedTerrainDecisionOverrideForTesting = (_, _) => Task.FromResult("Save");
            await SelectLevelFromPickerAsync(retail);
            if (!IsCurrentId65BlankLab() ||
                BuildTerrainEditSignature(_currentGeometry) != stagedLabTerrainSignature ||
                BuildMobyEditSignature(_currentMobys) != stagedLabMobySignature ||
                _savedTerrainEditSignature != savedLabTerrainSignature ||
                _savedMobyEditSignature != savedLabMobySignature ||
                File.Exists(labAuthoredTerrainPath) ||
                !new[] { labRetailTerrainPath, labRetailMobyPath, labRetailNativePath }
                    .All(path => File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(retailSentinel)))
            {
                throw new InvalidOperationException(
                    "Refused ID65 Save-to-retail changed the level, current/saved signatures, isolated authored file, or retail-root sentinels.");
            }

            bool lowLevelMobySaveBlocked = false;
            try
            {
                _ = await PersistCurrentMobyEditsAsync();
            }
            catch (InvalidOperationException)
            {
                lowLevelMobySaveBlocked = true;
            }
            if (!lowLevelMobySaveBlocked ||
                _savedMobyEditSignature != savedLabMobySignature ||
                !File.ReadAllBytes(labRetailMobyPath).SequenceEqual(retailSentinel))
            {
                throw new InvalidOperationException(
                    "Generic low-level Moby persistence did not fail closed for ID65.");
            }

            labMoby.Position = lockedLabMobyPosition;
            labTerrain.ApplyTerrainVertexDeltas(lockedLabTerrainDeltas);
            float[] savedHpZ = lockedLabTerrainDeltas.ToArray();
            savedHpZ[0] += 11;
            labTerrain.ApplyTerrainVertexDeltas(savedHpZ);
            bool genericTerrainSaved = await TrySaveCurrentTerrainEditsAsync();
            bool savedTerrainFileHasEdits = TerrainEditFileHasEdits(labAuthoredTerrainPath);
            bool terrainStillUnsaved = HasUnsavedTerrainEdits();
            if (!genericTerrainSaved || !savedTerrainFileHasEdits || terrainStillUnsaved)
            {
                throw new InvalidOperationException(
                    "Generic Save Terrain did not persist the supported ID65 HP-Z edit through the isolated Lab save route. " +
                    $"Returned={genericTerrainSaved}; file={savedTerrainFileHasEdits}; unsaved={terrainStillUnsaved}; status={_statusText.Text}");
            }

            string restoredRuntimeKey = labTerrain.RuntimeKey;
            byte[] authoredBeforeFailedRestore = File.ReadAllBytes(labAuthoredTerrainPath);
            LevelDefinition levelBeforeFailedRestore = _currentLevel!;
            GeometryCandidate geometryBeforeFailedRestore = _currentGeometry;
            List<Moby> mobysBeforeFailedRestore = _currentMobys;
            Moby? selectedMobyBeforeFailedRestore = _selectedMoby;
            TerrainPolygon? selectedTerrainBeforeFailedRestore = _selectedTerrain;
            int selectedTerrainIndexBeforeFailedRestore = _selectedTerrainIndex;
            string terrainSignatureBeforeFailedRestore = BuildTerrainEditSignature(_currentGeometry);
            string savedTerrainSignatureBeforeFailedRestore = _savedTerrainEditSignature;
            string mobySignatureBeforeFailedRestore = BuildMobyEditSignature(_currentMobys);
            string savedMobySignatureBeforeFailedRestore = _savedMobyEditSignature;
            Id65BlankLabReloadOverrideForTesting = _ =>
            {
                if (!File.Exists(labAuthoredTerrainPath) ||
                    !File.ReadAllBytes(labAuthoredTerrainPath).SequenceEqual(authoredBeforeFailedRestore))
                {
                    return Task.FromException<LevelLoadData>(
                        new InvalidDataException(
                            "The authored ID65 file disappeared before injected reload validation."));
                }
                return Task.FromException<LevelLoadData>(
                    new InvalidDataException("Injected ID65 locked-cache reload failure."));
            };
            bool failedRestoreResult = await RestoreId65BlankLabTerrainAsync("terrain");
            bool failedRestoreFileExact = File.Exists(labAuthoredTerrainPath) &&
                File.ReadAllBytes(labAuthoredTerrainPath).SequenceEqual(authoredBeforeFailedRestore);
            bool failedRestoreLevelExact = ReferenceEquals(_currentLevel, levelBeforeFailedRestore);
            bool failedRestoreGeometryExact = ReferenceEquals(_currentGeometry, geometryBeforeFailedRestore);
            bool failedRestoreMobysExact = ReferenceEquals(_currentMobys, mobysBeforeFailedRestore);
            bool failedRestoreMobySelectionExact = ReferenceEquals(_selectedMoby, selectedMobyBeforeFailedRestore);
            bool failedRestoreTerrainSelectionExact = ReferenceEquals(_selectedTerrain, selectedTerrainBeforeFailedRestore) &&
                _selectedTerrainIndex == selectedTerrainIndexBeforeFailedRestore;
            bool failedRestoreTerrainSignaturesExact =
                BuildTerrainEditSignature(_currentGeometry) == terrainSignatureBeforeFailedRestore &&
                _savedTerrainEditSignature == savedTerrainSignatureBeforeFailedRestore;
            bool failedRestoreMobySignaturesExact =
                BuildMobyEditSignature(_currentMobys) == mobySignatureBeforeFailedRestore &&
                _savedMobyEditSignature == savedMobySignatureBeforeFailedRestore;
            bool failedRestoreStatusTruthful = (_statusText.Text ?? "")
                .Contains("refused", StringComparison.OrdinalIgnoreCase);
            if (failedRestoreResult ||
                !failedRestoreFileExact ||
                !failedRestoreLevelExact ||
                !failedRestoreGeometryExact ||
                !failedRestoreMobysExact ||
                !failedRestoreMobySelectionExact ||
                !failedRestoreTerrainSelectionExact ||
                !failedRestoreTerrainSignaturesExact ||
                !failedRestoreMobySignaturesExact ||
                !failedRestoreStatusTruthful)
            {
                throw new InvalidOperationException(
                    "Injected ID65 restore failure did not preserve the exact authored file and in-memory/signature state. " +
                    $"result={failedRestoreResult}; file={failedRestoreFileExact}; level={failedRestoreLevelExact}; " +
                    $"geometry={failedRestoreGeometryExact}; mobys={failedRestoreMobysExact}; " +
                    $"mobySelection={failedRestoreMobySelectionExact}; terrainSelection={failedRestoreTerrainSelectionExact}; " +
                    $"terrainSignatures={failedRestoreTerrainSignaturesExact}; mobySignatures={failedRestoreMobySignaturesExact}; " +
                    $"status={failedRestoreStatusTruthful} ({_statusText.Text})");
            }

            Id65BlankLabReloadOverrideForTesting = _ =>
            {
                if (!File.Exists(labAuthoredTerrainPath) ||
                    !File.ReadAllBytes(labAuthoredTerrainPath).SequenceEqual(authoredBeforeFailedRestore))
                {
                    return Task.FromException<LevelLoadData>(
                        new InvalidDataException(
                            "The authored ID65 file disappeared before successful reload validation."));
                }
                return Task.FromResult(CreateCleanLockedCacheFixture());
            };
            if (!await RestoreId65BlankLabTerrainAsync("terrain") ||
                File.Exists(labAuthoredTerrainPath) ||
                !IsCurrentId65BlankLab() ||
                _currentGeometry == null ||
                HasUnsavedTerrainEdits())
            {
                throw new InvalidOperationException(
                    "ID65 Restore Terrain did not remove the isolated authored layer and reload the locked cache.");
            }
            TerrainPolygon restoredTerrain = _currentGeometry.Polygons.Single(candidate =>
                string.Equals(candidate.RuntimeKey, restoredRuntimeKey, StringComparison.Ordinal));
            if (!restoredTerrain.TerrainVertexDeltas().SequenceEqual(lockedLabTerrainDeltas) ||
                !new[] { labRetailTerrainPath, labRetailMobyPath, labRetailNativePath }
                    .All(path => File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(retailSentinel)))
            {
                throw new InvalidOperationException(
                    "ID65 restore did not return the saved HP-Z face to locked-cache coordinates or touched a retail/object path.");
            }

            return "Cancel preserved retail level/camera/selection/unsaved edits; Save persisted retail terrain and Mobys before the guarded ID65 transition; unsupported ID65 Save-to-retail stayed put without file/signature writes; generic Moby persistence failed closed; generic Terrain Save used the isolated Lab route; authored bytes stayed durable through injected Restore reload failure and exact in-memory/signature state remained; successful candidate validation/application committed removal of the actual saved HP-Z layer, reloaded locked cache, and left retail/object paths untouched";
        }
        finally
        {
            UnsavedTerrainDecisionOverrideForTesting = null;
            Id65BlankLabReloadOverrideForTesting = null;
            Id65BlankLabValidationOverrideForTesting = null;
            foreach ((string path, byte[]? contents) in preservedLabPaths)
            {
                if (contents == null)
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _workspace.RootPath);
                    File.WriteAllBytes(path, contents);
                }
            }
        }
    }

    internal void AdmitId65BlankLabManifestForTesting(
        UnusedLevel65BlankLevelLabManifest manifest)
    {
        UnusedLevel65BlankLevelLabWorkspacePaths paths =
            UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(_workspace.RootPath);
        UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(manifest, paths);
        _id65BlankLabPaths = paths;
        _id65BlankLabManifest = manifest;
        _id65BlankLabValidationFailure = "";
        AdmitValidatedId65BlankLabToCatalog();
    }

    internal sealed record Id65BlankLabUiSnapshot(
        string DisclosureTitle,
        string StatusText,
        string LockedBaseText,
        string AuthoredLayerText,
        string CapabilityText,
        int RetailCatalogCount,
        bool HasId65CatalogLevel,
        bool BuildEnabled,
        bool LoadEnabled,
        bool SaveEnabled,
        bool CreateCueEnabled,
        bool RevealEnabled,
        string RevealButtonText,
        bool RepairsInvalidWorkspace,
        string DisposableExporterRoute);

    private sealed record Id65BlankLabAuthoredLayerSnapshot(
        bool DirectoryExisted,
        IReadOnlyList<string> Files);
}

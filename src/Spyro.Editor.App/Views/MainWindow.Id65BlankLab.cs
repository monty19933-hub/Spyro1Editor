using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Skyboxes;
using Spyro.Editor.Core.Text;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private const string Id65BlankLabDisclosureTitle = "ID65 Blank-Level Lab";
    private const string Id65BlankLabTerrainCapability =
        "Existing HP Z: research-only through the topology-safe solid-terrain path. True Add, LP authoring, and XY movement: unavailable.";
    private const string Id65BlankLabTextureCapability =
        "Same-level resident textures: editor preview and isolated authored-layer saving. Cross-level/custom art and runtime export remain unavailable until a destination profile is proved.";
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
    private CancellationTokenSource? _id65BlankLabManualOperationCancellation;
    private int _id65BlankLabManualOperationGeneration;
    private Id65BlankLabManualOperation? _id65BlankLabManualOperation;
    private string _id65BlankLabRevealPath = "";
    private UnusedLevel65BlankLevelLabWorkspacePaths? _id65BlankLabPaths;
    private UnusedLevel65BlankLevelLabManifest? _id65BlankLabManifest;
    private string _id65BlankLabTextureOverlaySha256 = "";
    private string _id65BlankLabValidationFailure = "";
    private CancellationTokenSource? _id65BlankLabCatalogValidationCancellation;
    private int _id65BlankLabCatalogValidationGeneration;
    private bool _id65BlankLabCatalogValidationBusy;
    private bool _id65BlankLabCatalogValidationLifetimeEnded;
    internal Func<
        UnusedLevel65BlankLevelLabWorkspacePaths,
        CancellationToken,
        Task<UnusedLevel65BlankLevelLabManifest>>?
        Id65BlankLabCatalogValidationOverrideForTesting { get; set; }
    internal Func<string, CancellationToken, Task>?
        Id65BlankLabManualOperationDelayOverrideForTesting { get; set; }
    internal Action<string>? Id65BlankLabTerrainPersistenceFaultForTesting { get; set; }

    private sealed record Id65BlankLabManualOperation(
        int Generation,
        string Name,
        string WorkspaceRoot,
        LevelDefinition? LevelIdentity,
        GeometryCandidate? GeometryIdentity,
        CancellationTokenSource Cancellation)
    {
        public LevelDefinition? ExpectedLevelIdentity { get; set; } = LevelIdentity;
        public GeometryCandidate? ExpectedGeometryIdentity { get; set; } = GeometryIdentity;
    }

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

    private LevelCatalog ResetCatalogForDeferredId65BlankLabValidation(LevelCatalog retailCatalog)
    {
        ArgumentNullException.ThrowIfNull(retailCatalog);
        CancelId65BlankLabCatalogValidation();
        _id65BlankLabPaths =
            UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(_workspace.RootPath);
        _id65BlankLabManifest = null;
        _id65BlankLabTextureOverlaySha256 = "";
        _id65BlankLabValidationFailure = "";
        _id65BlankLabCatalogValidationBusy = !_id65BlankLabCatalogValidationLifetimeEnded;
        return retailCatalog;
    }

    private async Task ValidateAndAdmitId65BlankLabCatalogAsync()
    {
        if (_id65BlankLabCatalogValidationLifetimeEnded)
            return;

        if (_id65BlankLabManifest != null &&
            _catalog.FindByKey(UnusedLevel65BlankLevelLabProfileRegistry.Key) != null)
        {
            return;
        }

        UnusedLevel65BlankLevelLabWorkspacePaths paths = _id65BlankLabPaths ??
            UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(_workspace.RootPath);
        int generation = ++_id65BlankLabCatalogValidationGeneration;
        _id65BlankLabCatalogValidationCancellation?.Cancel();
        CancellationTokenSource cancellation = new();
        _id65BlankLabCatalogValidationCancellation = cancellation;
        _id65BlankLabCatalogValidationBusy = true;
        RefreshId65BlankLabUi();
        try
        {
            Func<
                UnusedLevel65BlankLevelLabWorkspacePaths,
                CancellationToken,
                Task<UnusedLevel65BlankLevelLabManifest>> validator =
                Id65BlankLabCatalogValidationOverrideForTesting ??
                UnusedLevel65BlankLevelLabBootstrapper.ValidatePublishedWorkspaceAsync;
            UnusedLevel65BlankLevelLabManifest manifest =
                await Task.Run(
                    async () => await validator(paths, cancellation.Token).ConfigureAwait(false),
                    cancellation.Token);
            if (_id65BlankLabCatalogValidationLifetimeEnded ||
                generation != _id65BlankLabCatalogValidationGeneration ||
                cancellation.IsCancellationRequested)
                return;

            _id65BlankLabPaths = paths;
            _id65BlankLabManifest = manifest;
            _id65BlankLabValidationFailure = "";
            AdmitValidatedId65BlankLabToCatalog();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is
            IOException or
            InvalidDataException or
            JsonException or
            UnauthorizedAccessException)
        {
            if (generation != _id65BlankLabCatalogValidationGeneration)
                return;
            _id65BlankLabValidationFailure = ex.Message;
            _id65BlankLabManifest = null;
        }
        catch (Exception ex)
        {
            if (generation != _id65BlankLabCatalogValidationGeneration)
                return;
            _id65BlankLabValidationFailure = ex.Message;
            _id65BlankLabManifest = null;
            EditorDiagnostics.RecordException("validating the deferred ID65 lab catalog", ex);
        }
        finally
        {
            if (generation == _id65BlankLabCatalogValidationGeneration)
            {
                _id65BlankLabCatalogValidationBusy = false;
                if (ReferenceEquals(_id65BlankLabCatalogValidationCancellation, cancellation))
                    _id65BlankLabCatalogValidationCancellation = null;
                RefreshId65BlankLabUi();
            }
            cancellation.Dispose();
        }
    }

    private void CancelId65BlankLabCatalogValidation()
    {
        ++_id65BlankLabCatalogValidationGeneration;
        _id65BlankLabCatalogValidationCancellation?.Cancel();
        _id65BlankLabCatalogValidationCancellation = null;
        _id65BlankLabCatalogValidationBusy = false;
    }

    private void EndId65BlankLabCatalogValidationLifetime()
    {
        _id65BlankLabCatalogValidationLifetimeEnded = true;
        CancelId65BlankLabCatalogValidation();
    }

    internal Task ValidateAndAdmitId65BlankLabCatalogForTestingAsync() =>
        ValidateAndAdmitId65BlankLabCatalogAsync();

    internal void EndId65BlankLabCatalogValidationLifetimeForTesting() =>
        EndId65BlankLabCatalogValidationLifetime();

    private async Task BuildOrRefreshId65BlankLabBaseAsync()
    {
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

        if (!TryBeginId65BlankLabManualOperation(
                "locked-base build/refresh",
                out Id65BlankLabManualOperation? operation) ||
            operation == null)
        {
            return;
        }

        _statusText.Text = "ID65 Lab: validating the clean disc and building the evidence-bound locked base...";
        try
        {
            await AwaitId65BlankLabManualOperationDelayForTestingAsync(
                operation,
                "build-started");
            RequireCurrentId65BlankLabManualOperation(operation);
            UnusedLevel65BlankLevelLabWorkspacePaths requestedPaths =
                UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(operation.WorkspaceRoot);
            Id65BlankLabAuthoredLayerSnapshot authoredBefore =
                await CaptureId65BlankLabAuthoredLayerAsync(
                    requestedPaths.AuthoredEditsDirectoryPath,
                    operation.Cancellation.Token);
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
                        operation.WorkspaceRoot,
                        ReplaceInvalidExistingWorkspace: true),
                    operation.Cancellation.Token);
            RequireCurrentId65BlankLabManualOperation(operation);
            Id65BlankLabAuthoredLayerSnapshot authoredAfter =
                await CaptureId65BlankLabAuthoredLayerAsync(
                    result.Paths.AuthoredEditsDirectoryPath,
                    operation.Cancellation.Token);
            EnsureId65BlankLabAuthoredLayerPreserved(authoredBefore, authoredAfter);
            UnusedLevel65BlankLevelLabManifest cacheManifest =
                await BuildId65BlankLabCacheAsync(
                    result.Paths,
                    overwrite: true,
                    operation.Cancellation.Token,
                    operation);
            RequireCurrentId65BlankLabManualOperation(operation);
            _id65BlankLabPaths = result.Paths;
            _id65BlankLabManifest = cacheManifest;
            _id65BlankLabValidationFailure = "";
            AdmitValidatedId65BlankLabToCatalog();
            _statusText.Text = result.ReusedExistingLockedBase
                ? "ID65 Lab: exact locked base was already valid; its isolated editor cache was refreshed."
                : repairingExistingWorkspace
                    ? "ID65 Lab: invalid/partial locked-base files were repaired transactionally; the authored-edits layer and clean source BIN were preserved."
                    : "ID65 Lab: exact locked base was built and validated; the clean source BIN was preserved.";
        }
        catch (OperationCanceledException) when (operation.Cancellation.IsCancellationRequested ||
                                                 !IsCurrentId65BlankLabManualOperation(
                                                     operation,
                                                     requireSceneIdentity: true))
        {
            // A workspace/level/generation switch owns the visible state now.
            // Never publish this older operation's status or manifest there.
        }
        catch (Exception ex)
        {
            if (IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: true))
            {
                _id65BlankLabValidationFailure = ex.Message;
                _statusText.Text = $"Could not build the ID65 locked base: {ex.Message}";
            }
        }
        finally
        {
            CompleteId65BlankLabManualOperation(operation);
        }
    }

    private async Task LoadId65BlankLabAsync()
    {
        if (!TryBeginId65BlankLabManualOperation(
                "load",
                out Id65BlankLabManualOperation? operation) ||
            operation == null)
        {
            return;
        }

        try
        {
            await AwaitId65BlankLabManualOperationDelayForTestingAsync(
                operation,
                "load-started");
            if (!await TryValidateId65BlankLabAsync(operation))
            {
                RequireCurrentId65BlankLabManualOperation(operation);
                _statusText.Text =
                    $"Build and validate the ID65 locked base before loading the lab. {_id65BlankLabValidationFailure}".Trim();
                return;
            }

            UnusedLevel65BlankLevelLabWorkspacePaths paths = _id65BlankLabPaths!;
            UnusedLevel65BlankLevelLabManifest manifest = await BuildId65BlankLabCacheAsync(
                paths,
                overwrite: false,
                operation.Cancellation.Token,
                operation);
            RequireCurrentId65BlankLabManualOperation(operation);
            AdmitValidatedId65BlankLabToCatalog();
            LevelDefinition lab = _catalog.FindByKey(UnusedLevel65BlankLevelLabProfileRegistry.Key)
                ?? throw new InvalidOperationException("The validated ID65 lab was not admitted to the in-memory catalog.");
            LevelDefinition? previousLevel = _currentLevel;
            if (IsCurrentId65BlankLab())
            {
                if (HasUnsavedTerrainEdits())
                {
                    _statusText.Text =
                        "ID65 Lab reload refused because the current terrain has unsaved edits. Save or undo those edits before reloading the locked workspace.";
                    return;
                }
                await SelectLevelAsync(lab, operation);
            }
            else
            {
                await SelectLevelFromPickerAsync(lab, operation);
            }
            if (!IsCurrentId65BlankLabManualOperation(operation))
                return;
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
            _id65BlankLabManifest = manifest;
            _statusText.Text =
                $"Loaded {lab.DisplayName} from its locked base: {_currentGeometry?.Polygons.Count ?? 0} terrain faces and {_currentMobys.Count} resident Mobys for inspection.";
        }
        catch (OperationCanceledException) when (operation.Cancellation.IsCancellationRequested ||
                                                 !IsCurrentId65BlankLabManualOperation(
                                                     operation,
                                                     requireSceneIdentity: true))
        {
        }
        catch (Exception ex)
        {
            if (IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: true))
                _statusText.Text = $"Could not load the ID65 lab: {ex.Message}";
        }
        finally
        {
            CompleteId65BlankLabManualOperation(operation);
        }
    }

    private async Task SaveId65BlankLabWorkspaceAsync()
    {
        _ = await SaveId65BlankLabWorkspaceCoreAsync(announce: true);
    }

    private async Task CreateId65BlankLabDisposableCueAsync()
    {
        if (!IsCurrentId65BlankLab())
        {
            _statusText.Text = "Load the validated ID65 lab before creating its disposable test CUE.";
            return;
        }
        if (!TryBeginId65BlankLabManualOperation(
                "disposable test CUE creation",
                out Id65BlankLabManualOperation? operation) ||
            operation == null)
        {
            return;
        }

        try
        {
            await AwaitId65BlankLabManualOperationDelayForTestingAsync(
                operation,
                "create-started");
            if (!await TryValidateId65BlankLabAsync(operation))
                throw new InvalidDataException(_id65BlankLabValidationFailure);
            RequireCurrentId65BlankLabManualOperation(operation);
            GeometryCandidate geometry = operation.GeometryIdentity
                ?? throw new InvalidOperationException("The ID65 lab terrain is not loaded.");

            if (!await SaveId65BlankLabWorkspaceCoreAsync(
                    announce: false,
                    existingOperation: operation))
            {
                throw new InvalidOperationException(
                    "The ID65 research workspace contains an unsupported edit and was not saved.");
            }
            if (!TryValidateId65BlankLabTerrainForDisposableCue(
                    geometry,
                    out string terrainBlocker))
            {
                throw new InvalidOperationException(terrainBlocker);
            }
            if (TryGetId65BlankLabUnsupportedArtifact(out string unsupportedArtifact))
            {
                throw new InvalidOperationException(
                    $"This first disposable writer accepts only existing HP Z terrain edits. {unsupportedArtifact}");
            }

            UnusedLevel65BlankLevelLabWorkspacePaths paths =
                UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
                    operation.WorkspaceRoot);
            string terrainEditsPath = Path.Combine(
                paths.AuthoredEditsDirectoryPath,
                $"{UnusedLevel65BlankLevelLabProfileRegistry.Key}-terrain-edits.json");
            if (!TerrainEditFileHasEdits(terrainEditsPath))
                throw new InvalidOperationException("Make and save at least one existing HP Z terrain edit first.");

            RequireCurrentId65BlankLabManualOperation(operation);
            string testKey = CreateId65BlankLabDisposableTestKey();
            UnusedLevel65BlankLevelLabTerrainTestResult result =
                await UnusedLevel65BlankLevelLabTerrainTestExporter.CreateDisposableTerrainTestAsync(
                    new UnusedLevel65BlankLevelLabTerrainTestRequest(
                        WorkspaceContainerPath: operation.WorkspaceRoot,
                        TerrainEditsPath: terrainEditsPath,
                        TestKey: testKey,
                        TestDisplayName: "ID65 Blank-Level Lab HP-Z terrain test",
                        ReplaceExistingTest: false,
                        RequestFinderReveal: OperatingSystem.IsMacOS()),
                    operation.Cancellation.Token);
            RequireCurrentId65BlankLabManualOperation(operation);
            _id65BlankLabRevealPath = result.Terrain.OutputCuePath;
            RefreshId65BlankLabUi();
            RevealId65BlankLabOutput();
            _statusText.Text =
                $"Created atomic disposable ID65 test CUE {Path.GetFileName(result.Terrain.OutputCuePath)} with {result.Terrain.Plan.PatchCount} exact HP-Z/collision patch(es). " +
                $"Checklist (all four load codes): {Path.GetFileName(result.Paths.RuntimeChecklistPath)}. " +
                $"Readback verified across {result.VerifiedRawSectorCount} raw sector(s); BIN SHA-256 {result.OutputImageSha256}.";
        }
        catch (OperationCanceledException) when (operation.Cancellation.IsCancellationRequested ||
                                                 !IsCurrentId65BlankLabManualOperation(
                                                     operation,
                                                     requireSceneIdentity: true))
        {
        }
        catch (Exception ex)
        {
            if (IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: true))
                _statusText.Text = $"Could not create an ID65 disposable test CUE: {ex.Message}";
        }
        finally
        {
            CompleteId65BlankLabManualOperation(operation);
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
        bool disposableTerrainReady =
            active &&
            _currentGeometry != null &&
            TryValidateId65BlankLabTerrainForDisposableCue(_currentGeometry, out _);
        _id65BlankLabStatusText.Text = _id65BlankLabCatalogValidationBusy
            ? "Checking the existing ID65 locked base in the background. The editor remains available while its exact manifest and BIN identity are validated."
            : valid
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
              $"Saved HP-Z/resident-texture terrain: {(hasTerrain ? "present" : "none")}." +
              (hasUnsupported ? $" Disposable writer blocked: {unsupported}" : "")
            : "No admitted lab workspace. Normal Create BIN remains separate.";

        bool busy = _id65BlankLabBusy ||
            _id65BlankLabCatalogValidationBusy ||
            HasConflictingId65BlankLabManualOperationState();
        if (_id65BlankLabBuildButton != null)
            _id65BlankLabBuildButton.IsEnabled = !busy;
        if (_id65BlankLabLoadButton != null)
            _id65BlankLabLoadButton.IsEnabled = valid && !busy;
        if (_id65BlankLabSaveButton != null)
            _id65BlankLabSaveButton.IsEnabled = valid && active && !busy;
        if (_id65BlankLabCreateCueButton != null)
            _id65BlankLabCreateCueButton.IsEnabled =
                valid && active && !busy && !hasUnsupported &&
                disposableTerrainReady;
        if (_id65BlankLabRevealButton != null)
            _id65BlankLabRevealButton.IsEnabled =
                !busy && File.Exists(_id65BlankLabRevealPath);
    }

    private async Task<bool> SaveId65BlankLabWorkspaceCoreAsync(
        bool announce,
        Id65BlankLabManualOperation? existingOperation = null,
        bool automaticTerrainPersistence = false)
    {
        if (!IsCurrentId65BlankLab() || _currentGeometry == null)
        {
            if (announce)
                _statusText.Text = "Load the validated ID65 lab before saving its research workspace.";
            return false;
        }
        bool ownsOperation = existingOperation == null;
        Id65BlankLabManualOperation? operation = existingOperation;
        if (ownsOperation &&
            (!TryBeginId65BlankLabManualOperation(
                 automaticTerrainPersistence ? "automatic terrain persistence" : "save",
                 out operation,
                 allowActiveTerrainPersistence: automaticTerrainPersistence) ||
             operation == null))
        {
            return false;
        }
        if (operation == null)
            throw new InvalidOperationException("The ID65 Lab save operation context is missing.");

        UnusedLevel65BlankLevelLabManifest? expectedManifest = _id65BlankLabManifest;
        string? stagedTerrainEditsPath = null;
        try
        {
            if (ownsOperation)
            {
                await AwaitId65BlankLabManualOperationDelayForTestingAsync(
                    operation,
                    "save-started");
            }
            RequireCurrentId65BlankLabManualOperation(operation);
            if (HasUnsavedMobyEdits() ||
                _currentMobys.Any(moby => moby.HasAnyEdit) ||
                HasUnsavedNativeMovementEdits() ||
                HasUnsavedDragonRunToEdits())
            {
                _statusText.Text =
                    "ID65 Lab save refused: resident Mobys and native movement records are inspection-only. Undo the object/native movement mutation or reload the Lab; the editor did not mark it saved.";
                return false;
            }
            GeometryCandidate geometry = operation.GeometryIdentity
                ?? throw new InvalidOperationException("The ID65 Lab save lost its captured terrain identity.");
            if (!TryValidateId65BlankLabTerrainEdits(
                    geometry,
                    requireAtLeastOneEdit: false,
                    allowResidentTextureEdits: true,
                    out string terrainBlocker))
            {
                _statusText.Text = $"ID65 Lab save refused: {terrainBlocker}";
                return false;
            }
            if (!await TryValidateId65BlankLabAsync(operation))
            {
                RequireCurrentId65BlankLabManualOperation(operation);
                _statusText.Text =
                    $"Could not validate the ID65 research workspace: {_id65BlankLabValidationFailure}";
                return false;
            }

            RequireCurrentId65BlankLabManualOperation(operation);
            UnusedLevel65BlankLevelLabWorkspacePaths paths =
                UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
                    operation.WorkspaceRoot);
            if (expectedManifest == null)
                throw new InvalidDataException("The ID65 save has no admitted locked-workspace manifest identity.");
            UnusedLevel65BlankLevelLabManifest validatedCacheManifest =
                await BuildId65BlankLabCacheAsync(
                    paths,
                    overwrite: false,
                    operation.Cancellation.Token,
                    operation);
            RequireCurrentId65BlankLabManualOperation(operation);
            if (!Equals(validatedCacheManifest, expectedManifest))
            {
                throw new InvalidDataException(
                    "The published ID65 workspace changed after this Lab scene was admitted. Reload the Lab before saving terrain.");
            }

            string terrainEditsPath = Path.Combine(
                paths.AuthoredEditsDirectoryPath,
                $"{UnusedLevel65BlankLevelLabProfileRegistry.Key}-terrain-edits.json");
            Directory.CreateDirectory(paths.AuthoredEditsDirectoryPath);
            string persistedTerrainSignature = BuildTerrainEditSignature(geometry);
            stagedTerrainEditsPath = Path.Combine(
                paths.AuthoredEditsDirectoryPath,
                $".{UnusedLevel65BlankLevelLabProfileRegistry.Key}-terrain-edits-save-{operation.Generation}-{Guid.NewGuid():N}.tmp");
            int terrainCount = await TerrainEditStore.SaveAsync(
                stagedTerrainEditsPath,
                geometry.Polygons,
                operation.LevelIdentity?.DisplayName ??
                    UnusedLevel65BlankLevelLabProfileRegistry.Definition.DisplayName,
                operation.Cancellation.Token);
            Id65BlankLabTerrainPersistenceFaultForTesting?.Invoke(stagedTerrainEditsPath);
            await AwaitId65BlankLabManualOperationDelayForTestingAsync(
                operation,
                "save-work-completed");
            RequireCurrentId65BlankLabManualOperation(operation);
            if (!string.Equals(
                    BuildTerrainEditSignature(geometry),
                    persistedTerrainSignature,
                    StringComparison.Ordinal))
            {
                throw new OperationCanceledException(
                    "The ID65 terrain changed while its staged save was being serialized; the prior authored file remains unchanged.",
                    operation.Cancellation.Token);
            }

            UnusedLevel65BlankLevelLabManifest publishManifest =
                await UnusedLevel65BlankLevelLabBootstrapper.ValidatePublishedWorkspaceAsync(
                    paths,
                    operation.Cancellation.Token);
            RequireCurrentId65BlankLabManualOperation(operation);
            if (!Equals(publishManifest, expectedManifest) ||
                !await IsCurrentId65BlankLabDerivedCacheAsync(
                    paths,
                    publishManifest,
                    operation.Cancellation.Token))
            {
                throw new InvalidDataException(
                    "The ID65 locked source, published manifest, or derived binding changed while the staged terrain save was being prepared.");
            }
            RequireCurrentId65BlankLabManualOperation(operation);

            File.Move(stagedTerrainEditsPath, terrainEditsPath, overwrite: true);
            stagedTerrainEditsPath = null;
            _loadedTerrainEdits = terrainCount;
            _savedTerrainEditSignature = persistedTerrainSignature;
            InvalidateBuildSafetySummary();
            RefreshTerrainReadinessHint();
            RefreshCurrentLevelDetails();
            RefreshId65BlankLabUi();
            if (announce)
            {
                _statusText.Text =
                    $"Saved {terrainCount} ID65 terrain edit(s) under the unique versioned lab identity. " +
                    "Resident texture-ID paints are editor preview only; resident Mobys remain inspection-only, " +
                    "and this authored layer is excluded from normal Create BIN.";
            }
            return true;
        }
        catch (OperationCanceledException) when (operation.Cancellation.IsCancellationRequested ||
                                                 !IsCurrentId65BlankLabManualOperation(
                                                     operation,
                                                     requireSceneIdentity: true))
        {
            if (!ownsOperation)
                throw;
            return false;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(stagedTerrainEditsPath) &&
                File.Exists(stagedTerrainEditsPath))
            {
                try
                {
                    File.Delete(stagedTerrainEditsPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Debug.WriteLine(ex);
                }
            }
            if (ownsOperation)
                CompleteId65BlankLabManualOperation(operation);
        }
    }

    private async Task<bool> TryValidateId65BlankLabAsync(
        Id65BlankLabManualOperation? operation = null)
    {
        CancellationToken cancellationToken = operation?.Cancellation.Token ?? default;
        if (Id65BlankLabValidationOverrideForTesting != null)
        {
            bool overridden = await Id65BlankLabValidationOverrideForTesting()
                .WaitAsync(cancellationToken);
            if (operation != null)
                RequireCurrentId65BlankLabManualOperation(operation);
            return overridden;
        }

        UnusedLevel65BlankLevelLabWorkspacePaths paths = _id65BlankLabPaths ??
            UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
                operation?.WorkspaceRoot ?? _workspace.RootPath);
        try
        {
            UnusedLevel65BlankLevelLabManifest manifest =
                await UnusedLevel65BlankLevelLabBootstrapper
                    .ValidatePublishedWorkspaceAsync(paths, cancellationToken);
            if (operation != null)
                RequireCurrentId65BlankLabManualOperation(operation);
            else
            {
                _id65BlankLabPaths = paths;
                _id65BlankLabManifest = manifest;
            }
            _id65BlankLabValidationFailure = "";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            if (operation != null)
                RequireCurrentId65BlankLabManualOperation(operation);
            else
                _id65BlankLabManifest = null;
            _id65BlankLabValidationFailure = ex.Message;
            return false;
        }
    }

    private async Task<UnusedLevel65BlankLevelLabManifest> BuildId65BlankLabCacheAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        bool overwrite,
        CancellationToken cancellationToken,
        Id65BlankLabManualOperation operation)
    {
        UnusedLevel65BlankLevelLabManifest manifest =
            await UnusedLevel65BlankLevelLabBootstrapper.ValidatePublishedWorkspaceAsync(
                paths,
                cancellationToken);
        RequireCurrentId65BlankLabManualOperation(operation);
        string cacheDirectory = Id65BlankLabCacheDirectory(paths);
        string overlayPath = Id65BlankLabOverlayPath(paths);
        Directory.CreateDirectory(cacheDirectory);

        if (overwrite || !await IsCurrentId65BlankLabDerivedCacheAsync(
                paths,
                manifest,
                cancellationToken))
        {
            await RebuildId65BlankLabDerivedCacheAsync(
                paths,
                manifest,
                cancellationToken);
        }
        RequireCurrentId65BlankLabManualOperation(operation);

        GeometryCacheHealthIssue? issue = GeometryCacheHealth.InspectOverlay(
            UnusedLevel65BlankLevelLabProfileRegistry.Key,
            overlayPath);
        if (issue?.BlocksLoading == true)
            throw new InvalidDataException(issue.Message);
        _ = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
        if (MobyLoader.LoadCached(Id65BlankLabMobyCachePath(paths)).Count <= 0)
        {
            throw new InvalidDataException("The ID65 locked-base Moby cache is empty.");
        }

        string sourceBoundOverlaySha256 = await HashFileSha256Async(
            overlayPath,
            cancellationToken);
        int texturePreviewCount = await PortableEditorCacheBuilder
            .BuildTerrainTexturePreviewCacheFromSourceAsync(
                paths.LockedBaseImagePath,
                overlayPath,
                paths.RootPath,
                UnusedLevel65BlankLevelLabProfileRegistry.Definition,
                overwrite,
                expectedSourceImageSha256: manifest.LockedBaseImageSha256,
                expectedSourceOverlaySha256: sourceBoundOverlaySha256,
                cancellationToken: cancellationToken);
        if (texturePreviewCount != UnusedLevel65BlankLevelLabProfileRegistry.ResidentTextureCount)
        {
            throw new InvalidDataException(
                $"The ID65 locked base produced {texturePreviewCount} resident texture records; " +
                $"exactly {UnusedLevel65BlankLevelLabProfileRegistry.ResidentTextureCount} are required.");
        }
        RequireCurrentId65BlankLabManualOperation(operation);
        _id65BlankLabTextureOverlaySha256 = sourceBoundOverlaySha256;
        ClearTerrainTextureCatalogPreviewCache();
        _terrainTexturePreviewBundleCache.Clear();
        return manifest;
    }

    private static async Task<bool> IsCurrentId65BlankLabDerivedCacheAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest manifest,
        CancellationToken cancellationToken = default)
    {
        string bindingPath = Id65BlankLabDerivedCacheBindingPath(paths);
        string wadAnalysisPath = Id65BlankLabWadAnalysisPath(paths);
        string overlayPath = Id65BlankLabOverlayPath(paths);
        string mobyPath = Id65BlankLabMobyCachePath(paths);
        string sourceSearchPath = Id65BlankLabSourceSearchPath(paths);
        if (!File.Exists(bindingPath) ||
            !File.Exists(wadAnalysisPath) ||
            !File.Exists(overlayPath) ||
            !File.Exists(mobyPath) ||
            !File.Exists(sourceSearchPath))
        {
            return false;
        }

        try
        {
            Id65BlankLabDerivedCacheBinding binding = JsonSerializer.Deserialize<Id65BlankLabDerivedCacheBinding>(
                await File.ReadAllTextAsync(bindingPath, cancellationToken))
                ?? throw new InvalidDataException("The ID65 derived-cache binding is empty.");
            if (binding.SchemaVersion != 1 ||
                !string.Equals(binding.LevelKey, UnusedLevel65BlankLevelLabProfileRegistry.Key, StringComparison.Ordinal) ||
                !string.Equals(binding.LockedSourceImageSha256, manifest.LockedBaseImageSha256, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(await HashFileSha256Async(wadAnalysisPath, cancellationToken), binding.WadAnalysisSha256, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(await HashFileSha256Async(overlayPath, cancellationToken), binding.SceneOverlaySha256, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(await HashFileSha256Async(mobyPath, cancellationToken), binding.MobyCacheSha256, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(await HashFileSha256Async(sourceSearchPath, cancellationToken), binding.SourceSearchSha256, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            GeometryCacheHealthIssue? issue = GeometryCacheHealth.InspectOverlay(
                UnusedLevel65BlankLevelLabProfileRegistry.Key,
                overlayPath);
            return issue?.BlocksLoading != true &&
                GeometryOverlayLoader.LoadFirstCandidate(overlayPath).Polygons.Count > 0 &&
                MobyLoader.LoadCached(mobyPath).Count > 0;
        }
        catch (Exception ex) when (ex is
            IOException or
            UnauthorizedAccessException or
            JsonException or
            InvalidDataException or
            InvalidOperationException)
        {
            return false;
        }
    }

    private static async Task RebuildId65BlankLabDerivedCacheAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest manifest,
        CancellationToken cancellationToken)
    {
        string operationRoot = Path.Combine(
            paths.RootPath,
            ".derived-cache-operations",
            Guid.NewGuid().ToString("N"));
        string stagedWadAnalysisPath = Path.Combine(operationRoot, "locked-base-wad-analysis.json");
        string stagedOverlayPath = Path.Combine(operationRoot, "locked-base-scene-overlay.json");
        string stagedMobyPath = Path.Combine(operationRoot, "locked-base-mobys.json");
        string stagedSourceSearchPath = Path.Combine(operationRoot, "locked-base-source-search.json");
        string stagedBindingPath = Path.Combine(operationRoot, "derived-cache-source-binding.json");
        Directory.CreateDirectory(operationRoot);
        try
        {
            await WadAnalysisBuilder.BuildAsync(
                paths.LockedBaseImagePath,
                stagedWadAnalysisPath,
                cancellationToken: cancellationToken);
            await SourceSceneOverlayExporter.ExportAsync(
                paths.LockedBaseImagePath,
                stagedWadAnalysisPath,
                UnusedLevel65BlankLevelLabProfileRegistry.Definition,
                stagedOverlayPath,
                cancellationToken: cancellationToken);

            JsonObject overlayRoot = JsonNode.Parse(
                await File.ReadAllTextAsync(stagedOverlayPath, cancellationToken))?.AsObject()
                ?? throw new InvalidDataException("The regenerated ID65 source overlay is empty.");
            overlayRoot["sourceImage"] = paths.LockedBaseImagePath;
            overlayRoot["sourceWadAnalysis"] = Id65BlankLabWadAnalysisPath(paths);
            await File.WriteAllTextAsync(
                stagedOverlayPath,
                overlayRoot.ToJsonString(),
                cancellationToken);

            GeometryCacheHealthIssue? issue = GeometryCacheHealth.InspectOverlay(
                UnusedLevel65BlankLevelLabProfileRegistry.Key,
                stagedOverlayPath);
            if (issue?.BlocksLoading == true)
                throw new InvalidDataException(issue.Message);
            GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(stagedOverlayPath);

            await SourceMobyCacheBuilder.BuildAsync(
                paths.LockedBaseImagePath,
                UnusedLevel65BlankLevelLabProfileRegistry.Definition,
                stagedMobyPath,
                cancellationToken);
            if (MobyLoader.LoadCached(stagedMobyPath).Count <= 0)
                throw new InvalidDataException("The regenerated ID65 Moby cache is empty.");

            TerrainSourceSearchResult search = await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(
                new SourceDerivedTerrainSourceSearchRequest(
                    paths.LockedBaseImagePath,
                    stagedSourceSearchPath,
                    UnusedLevel65BlankLevelLabProfileRegistry.Definition,
                    geometry),
                cancellationToken);
            if (search.Report.SectorCount <= 0 ||
                search.Report.MatchedSectorCount != search.Report.SectorCount ||
                search.Report.AmbiguousSectorCount != 0 ||
                search.Report.MissingSectorCount != 0)
            {
                throw new InvalidDataException("The regenerated ID65 locked-base terrain source-search map is incomplete.");
            }

            string sourceAfterSha256 = await HashFileSha256Async(
                paths.LockedBaseImagePath,
                cancellationToken);
            if (!string.Equals(sourceAfterSha256, manifest.LockedBaseImageSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("The ID65 locked source changed while its derived cache was being rebuilt.");
            }

            Id65BlankLabDerivedCacheBinding binding = new(
                SchemaVersion: 1,
                LevelKey: UnusedLevel65BlankLevelLabProfileRegistry.Key,
                LockedSourceImageSha256: sourceAfterSha256,
                WadAnalysisSha256: await HashFileSha256Async(stagedWadAnalysisPath, cancellationToken),
                SceneOverlaySha256: await HashFileSha256Async(stagedOverlayPath, cancellationToken),
                MobyCacheSha256: await HashFileSha256Async(stagedMobyPath, cancellationToken),
                SourceSearchSha256: await HashFileSha256Async(stagedSourceSearchPath, cancellationToken));
            await File.WriteAllTextAsync(
                stagedBindingPath,
                JsonSerializer.Serialize(binding, new JsonSerializerOptions { WriteIndented = true }) + "\n",
                cancellationToken);

            Directory.CreateDirectory(Id65BlankLabCacheDirectory(paths));
            File.Move(stagedWadAnalysisPath, Id65BlankLabWadAnalysisPath(paths), overwrite: true);
            File.Move(stagedOverlayPath, Id65BlankLabOverlayPath(paths), overwrite: true);
            File.Move(stagedMobyPath, Id65BlankLabMobyCachePath(paths), overwrite: true);
            File.Move(stagedSourceSearchPath, Id65BlankLabSourceSearchPath(paths), overwrite: true);
            // The binding is the commit marker and is published last. Any interrupted
            // mixed generation remains untrusted and is rebuilt on the next load.
            File.Move(stagedBindingPath, Id65BlankLabDerivedCacheBindingPath(paths), overwrite: true);

            if (!await IsCurrentId65BlankLabDerivedCacheAsync(
                    paths,
                    manifest,
                    cancellationToken))
                throw new IOException("The published ID65 derived cache failed exact source-bound readback.");
        }
        finally
        {
            if (Directory.Exists(operationRoot))
            {
                try
                {
                    Directory.Delete(operationRoot, recursive: true);
                }
                catch (IOException)
                {
                    // Derived-cache debris contains no user edits. A later build can
                    // safely replace it; never mask the primary build/readback result.
                }
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
            using (FileStream overlayStream = File.OpenRead(overlayPath))
            {
                _id65BlankLabTextureOverlaySha256 = Convert.ToHexString(
                    SHA256.HashData(overlayStream)).ToLowerInvariant();
            }
            int edits = includeAuthoredTerrainEdits
                ? TerrainEditStore.LoadStrict(Id65BlankLabTerrainEditsPath(), geometry.Polygons)
                : 0;
            if (includeAuthoredTerrainEdits &&
                !TryValidateId65BlankLabTerrainEdits(
                    geometry,
                    requireAtLeastOneEdit: false,
                    allowResidentTextureEdits: true,
                    out string authoredBlocker))
            {
                throw new InvalidDataException(
                    $"The isolated ID65 authored terrain layer is invalid: {authoredBlocker}");
            }
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
        paths = _id65BlankLabPaths;
        error = "";
        if (paths == null || _id65BlankLabManifest == null)
        {
            error = "Validate and prepare the ID65 locked workspace before loading the Lab.";
            return false;
        }
        UnusedLevel65BlankLevelLabWorkspacePaths validatedPaths = paths;
        UnusedLevel65BlankLevelLabManifest validatedManifest = _id65BlankLabManifest;

        try
        {
            UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(
                validatedManifest,
                validatedPaths);
            bool derivedCacheCurrent = Task.Run(() =>
                    IsCurrentId65BlankLabDerivedCacheAsync(validatedPaths, validatedManifest))
                .GetAwaiter()
                .GetResult();
            if (!derivedCacheCurrent)
            {
                error =
                    "The ID65 source-bound derived cache is missing or changed; select Load Lab again to rebuild it.";
                return false;
            }

            _id65BlankLabValidationFailure = "";
            return true;
        }
        catch (Exception ex) when (ex is
            IOException or
            InvalidDataException or
            InvalidOperationException or
            JsonException or
            UnauthorizedAccessException)
        {
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

    private bool TryBeginId65BlankLabManualOperation(
        string name,
        out Id65BlankLabManualOperation? operation,
        bool allowActiveLevelSelection = false,
        bool allowActiveTerrainPersistence = false)
    {
        if (_id65BlankLabBusy || _id65BlankLabManualOperation != null)
        {
            operation = null;
            _statusText.Text =
                $"ID65 Lab {name} did not start because another Lab operation is still active.";
            return false;
        }
        if (HasConflictingId65BlankLabManualOperationState(
                allowActiveLevelSelection,
                allowActiveTerrainPersistence))
        {
            operation = null;
            _statusText.Text =
                $"ID65 Lab {name} did not start because a save, Build Safety, level load, cache build, texture operation, Create BIN, or other navigation operation is still active.";
            return false;
        }

        CancellationTokenSource cancellation = new();
        operation = new Id65BlankLabManualOperation(
            Generation: ++_id65BlankLabManualOperationGeneration,
            Name: name,
            WorkspaceRoot: Path.GetFullPath(_workspace.RootPath),
            LevelIdentity: _currentLevel,
            GeometryIdentity: _currentGeometry,
            Cancellation: cancellation);
        _id65BlankLabManualOperationCancellation = cancellation;
        _id65BlankLabManualOperation = operation;
        _id65BlankLabBusy = true;
        RefreshId65BlankLabUi();
        RefreshLevelSelectionAvailability();
        RefreshActionAvailability();
        return true;
    }

    private bool HasConflictingId65BlankLabManualOperationState(
        bool allowActiveLevelSelection = false,
        bool allowActiveTerrainPersistence = false) =>
        _buildingPortableCache ||
        _projectDataImportBusy ||
        _workspaceTransitionBusy ||
        _regularEditorPersistenceBusy ||
        _buildSafetyBusy ||
        _loadingLevel ||
        (!allowActiveLevelSelection && _handlingLevelSelection) ||
        _levelSelectionAsyncInFlight > 0 ||
        (!allowActiveTerrainPersistence && _terrainTexturePaintBusy) ||
        (!allowActiveTerrainPersistence && _terrainTextureRelocationBusy) ||
        (!allowActiveTerrainPersistence && _terrainTexturePaintHistoryRestoreBusy) ||
        _createBinBusy ||
        _nativeLevelReplacementBusy ||
        _id65BlankLabCatalogValidationBusy;

    private bool TryBlockTerrainMutationDuringId65BlankLabManualOperation(
        string action,
        WorkspaceTransitionOperation? owningWorkspaceTransition = null)
    {
        if (_workspaceTransitionBusy &&
            (owningWorkspaceTransition == null ||
             !IsCurrentWorkspaceTransition(
                 owningWorkspaceTransition,
                 allowOwnedRegularPersistence: true)))
        {
            _statusText.Text =
                $"Wait for the active workspace transition to finish before {action}. No terrain or saved edit was changed.";
            return true;
        }
        if (!_id65BlankLabBusy || _id65BlankLabManualOperation == null)
            return false;

        _statusText.Text =
            $"Wait for the active ID65 Lab {_id65BlankLabManualOperation.Name} operation to finish before {action}. No terrain or saved edit was changed.";
        return true;
    }

    private async Task AwaitId65BlankLabManualOperationDelayForTestingAsync(
        Id65BlankLabManualOperation operation,
        string stage)
    {
        RequireCurrentId65BlankLabManualOperation(operation);
        if (Id65BlankLabManualOperationDelayOverrideForTesting != null)
        {
            await Id65BlankLabManualOperationDelayOverrideForTesting(
                stage,
                operation.Cancellation.Token);
        }
        RequireCurrentId65BlankLabManualOperation(operation);
    }

    private void RequireCurrentId65BlankLabManualOperation(
        Id65BlankLabManualOperation operation,
        bool requireSceneIdentity = true)
    {
        ArgumentNullException.ThrowIfNull(operation);
        operation.Cancellation.Token.ThrowIfCancellationRequested();
        bool sameOperation =
            operation.Generation == _id65BlankLabManualOperationGeneration &&
            ReferenceEquals(_id65BlankLabManualOperation, operation) &&
            ReferenceEquals(_id65BlankLabManualOperationCancellation, operation.Cancellation);
        bool sameWorkspace = PathsEqual(operation.WorkspaceRoot, _workspace.RootPath);
        bool sameScene = !requireSceneIdentity ||
            (ReferenceEquals(operation.ExpectedLevelIdentity, _currentLevel) &&
             ReferenceEquals(operation.ExpectedGeometryIdentity, _currentGeometry));
        if (!sameOperation || !sameWorkspace || !sameScene)
        {
            throw new OperationCanceledException(
                $"The ID65 Lab {operation.Name} result belongs to an older workspace, level, or geometry generation.",
                operation.Cancellation.Token);
        }
    }

    private bool IsCurrentId65BlankLabManualOperation(
        Id65BlankLabManualOperation operation,
        bool requireSceneIdentity = false) =>
        operation.Generation == _id65BlankLabManualOperationGeneration &&
        ReferenceEquals(_id65BlankLabManualOperation, operation) &&
        ReferenceEquals(_id65BlankLabManualOperationCancellation, operation.Cancellation) &&
        !operation.Cancellation.IsCancellationRequested &&
        PathsEqual(operation.WorkspaceRoot, _workspace.RootPath) &&
        (!requireSceneIdentity ||
         (ReferenceEquals(operation.ExpectedLevelIdentity, _currentLevel) &&
          ReferenceEquals(operation.ExpectedGeometryIdentity, _currentGeometry)));

    private void TransitionId65BlankLabManualOperationSceneIdentity(
        Id65BlankLabManualOperation operation,
        LevelDefinition? expectedPreviousLevel,
        GeometryCandidate? expectedPreviousGeometry,
        LevelDefinition? nextLevel,
        GeometryCandidate? nextGeometry)
    {
        RequireCurrentId65BlankLabManualOperation(operation, requireSceneIdentity: false);
        if (!ReferenceEquals(operation.ExpectedLevelIdentity, expectedPreviousLevel) ||
            !ReferenceEquals(operation.ExpectedGeometryIdentity, expectedPreviousGeometry) ||
            !ReferenceEquals(_currentLevel, nextLevel) ||
            !ReferenceEquals(_currentGeometry, nextGeometry))
        {
            throw new OperationCanceledException(
                $"The ID65 Lab {operation.Name} scene changed outside its guarded identity transition.",
                operation.Cancellation.Token);
        }

        operation.ExpectedLevelIdentity = nextLevel;
        operation.ExpectedGeometryIdentity = nextGeometry;
        RequireCurrentId65BlankLabManualOperation(operation);
    }

    private void ResetId65BlankLabManualOperationSceneIdentityAfterRollback(
        Id65BlankLabManualOperation operation,
        LevelDefinition? level,
        GeometryCandidate? geometry)
    {
        RequireCurrentId65BlankLabManualOperation(operation, requireSceneIdentity: false);
        if (!ReferenceEquals(_currentLevel, level) || !ReferenceEquals(_currentGeometry, geometry))
        {
            throw new InvalidDataException(
                $"The ID65 Lab {operation.Name} rollback did not restore its captured scene identity.");
        }
        operation.ExpectedLevelIdentity = level;
        operation.ExpectedGeometryIdentity = geometry;
        RequireCurrentId65BlankLabManualOperation(operation);
    }

    private void CompleteId65BlankLabManualOperation(Id65BlankLabManualOperation operation)
    {
        bool current =
            operation.Generation == _id65BlankLabManualOperationGeneration &&
            ReferenceEquals(_id65BlankLabManualOperation, operation) &&
            ReferenceEquals(_id65BlankLabManualOperationCancellation, operation.Cancellation);
        if (current)
        {
            _id65BlankLabManualOperation = null;
            _id65BlankLabManualOperationCancellation = null;
            _id65BlankLabBusy = false;
            RefreshId65BlankLabUi();
            RefreshLevelSelectionAvailability();
            RefreshActionAvailability();
        }
        operation.Cancellation.Dispose();
    }

    private void CancelId65BlankLabManualOperationForWorkspaceChange()
    {
        ++_id65BlankLabManualOperationGeneration;
        _id65BlankLabManualOperationCancellation?.Cancel();
        _id65BlankLabManualOperationCancellation = null;
        _id65BlankLabManualOperation = null;
        _id65BlankLabBusy = false;
        _loadingLevel = false;
        RefreshId65BlankLabUi();
        RefreshLevelSelectionAvailability();
        RefreshActionAvailability();
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

    private static string Id65BlankLabDerivedCacheBindingPath(UnusedLevel65BlankLevelLabWorkspacePaths paths) =>
        Path.Combine(
            Id65BlankLabCacheDirectory(paths),
            $"{UnusedLevel65BlankLevelLabProfileRegistry.Key}-locked-source-derived-cache-binding.json");

    private bool TryGetId65BlankLabUnsupportedArtifact(out string reason)
    {
        string key = UnusedLevel65BlankLevelLabProfileRegistry.Key;
        string objectEditsPath = Path.Combine(_workspace.RootPath, $"{key}-native-edits.json");
        string movementEditsPath = NativeMobyPathEditPath(key);
        string customTexturesPath = CustomTerrainTextureStore.ManifestPath(_workspace.RootPath, key);
        string relocationPath = NativeTerrainTextureRelocationEditStore.ManifestPath(_workspace.RootPath, key);
        string materialOverridesPath = Path.Combine(
            _workspace.RootPath,
            $"{key}-terrain-material-overrides.json");
        string musicPlanPath = LevelMusicEditStore.PlanPath(
            _workspace.RootPath,
            UnusedLevel65BlankLevelLabProfileRegistry.Definition);
        if (File.Exists(objectEditsPath) || File.Exists(movementEditsPath))
        {
            reason = "Resident Moby/path mutation is present but this profile permits inspection only.";
            return true;
        }
        if (File.Exists(customTexturesPath))
        {
            reason = "Custom/imported terrain art needs a proved ID65 destination profile.";
            return true;
        }
        if (File.Exists(relocationPath))
        {
            reason = "Portable cross-level terrain textures need a proved ID65 destination profile.";
            return true;
        }
        if (File.Exists(materialOverridesPath))
        {
            reason = "Retail terrain material overrides cannot be applied to the locked ID65 presentation; remove the unsupported override file.";
            return true;
        }
        if (File.Exists(NativeSkyEditStore.PlanPath(_workspace.RootPath, key)))
        {
            reason = "Skybox editing is unavailable for this profile; remove the unsupported ID65 skybox plan.";
            return true;
        }
        if (File.Exists(musicPlanPath))
        {
            reason = "Music editing is unavailable for this profile; remove the unsupported ID65 music plan.";
            return true;
        }

        reason = "";
        return false;
    }

    private bool Id65BlankLabHasAnyAuthoredArtifacts()
    {
        string key = UnusedLevel65BlankLevelLabProfileRegistry.Key;
        string terrainEditsPath = Id65BlankLabTerrainEditsPath();
        string objectEditsPath = Path.Combine(_workspace.RootPath, $"{key}-native-edits.json");
        string movementEditsPath = NativeMobyPathEditPath(key);
        string customTexturesPath = CustomTerrainTextureStore.ManifestPath(_workspace.RootPath, key);
        string relocationPath = NativeTerrainTextureRelocationEditStore.ManifestPath(_workspace.RootPath, key);
        string materialOverridesPath = Path.Combine(
            _workspace.RootPath,
            $"{key}-terrain-material-overrides.json");
        string musicPlanPath = LevelMusicEditStore.PlanPath(
            _workspace.RootPath,
            UnusedLevel65BlankLevelLabProfileRegistry.Definition);
        return Id65TerrainEditFileBlocksNormalCreateBin(terrainEditsPath) ||
            File.Exists(objectEditsPath) ||
            File.Exists(movementEditsPath) ||
            File.Exists(customTexturesPath) ||
            File.Exists(relocationPath) ||
            File.Exists(materialOverridesPath) ||
            File.Exists(NativeSkyEditStore.PlanPath(_workspace.RootPath, key)) ||
            File.Exists(musicPlanPath) ||
            (_id65BlankLabPaths != null && Directory.Exists(_id65BlankLabPaths.AuthoredEditsDirectoryPath) &&
             Directory.EnumerateFiles(_id65BlankLabPaths.AuthoredEditsDirectoryPath, "*", SearchOption.AllDirectories)
                 .Any(path => !string.Equals(
                     Path.GetFullPath(path),
                     Path.GetFullPath(terrainEditsPath),
                     StringComparison.OrdinalIgnoreCase)));
    }

    private static bool Id65TerrainEditFileBlocksNormalCreateBin(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("editCount", out JsonElement countElement) ||
                countElement.ValueKind != JsonValueKind.Number ||
                !countElement.TryGetInt32(out int count) ||
                count < 0 ||
                !root.TryGetProperty("edits", out JsonElement editsElement) ||
                editsElement.ValueKind != JsonValueKind.Array ||
                editsElement.GetArrayLength() != count)
            {
                return true;
            }

            return count > 0;
        }
        catch (Exception ex) when (ex is
            IOException or
            UnauthorizedAccessException or
            JsonException or
            InvalidOperationException)
        {
            // A present but unreadable/malformed authored layer must block normal
            // export; treating it as empty would silently omit the user's data.
            return true;
        }
    }

    private bool TryGetId65BlankLabNormalCreateBinBlockReason(out string reason)
    {
        if (IsCurrentId65BlankLab())
        {
            reason =
                "Normal Create BIN is unavailable while ID65 Blank-Level Lab is loaded. Use Create Disposable Test CUE in the ID65 Lab disclosure.";
            return true;
        }
        if (TryGetInvalidRetailTerrainTextureRelocationDonorBlockReason(out reason))
            return true;
        if (Id65BlankLabHasAnyAuthoredArtifacts())
        {
            reason =
                "Normal Create BIN is blocked because the isolated ID65 lab has authored research edits that the retail all-saved-edits writer must neither omit nor include. Load ID65 and use its disposable test writer, or restore its research layer first.";
            return true;
        }

        reason = "";
        return false;
    }

    private bool TryGetInvalidRetailTerrainTextureRelocationDonorBlockReason(
        out string reason)
    {
        foreach (LevelDefinition level in _retailCatalog.Levels)
        {
            string manifestPath = NativeTerrainTextureRelocationEditStore.ManifestPath(
                _workspace.RootPath,
                level.Key);
            if (!File.Exists(manifestPath))
                continue;

            if (TryGetPersistedRetailTerrainTextureRelocationDonorIdentityError(
                    manifestPath,
                    level,
                    out reason))
            {
                return true;
            }

            IReadOnlyList<NativeTerrainTextureRelocationEdit> relocations;
            try
            {
                relocations = NativeTerrainTextureRelocationEditStore.Load(
                    _workspace.RootPath,
                    level.Key);
            }
            catch (InvalidDataException)
            {
                // The general saved-edit validation reports malformed v3
                // manifests. This boundary is specifically about a valid row
                // trying to name the isolated Lab as a retail donor.
                continue;
            }

            if (TryGetRetailTerrainTextureRelocationDonorIdentityError(
                    level,
                    relocations,
                    out reason))
            {
                return true;
            }
        }

        reason = "";
        return false;
    }

    private bool TryGetPersistedRetailTerrainTextureRelocationDonorIdentityError(
        string manifestPath,
        LevelDefinition destination,
        out string reason)
    {
        if (!File.Exists(manifestPath))
        {
            reason = "";
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("relocations", out JsonElement rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                reason = "";
                return false;
            }

            foreach (JsonElement row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object ||
                    !row.TryGetProperty("targetTextureId", out JsonElement targetElement) ||
                    !targetElement.TryGetInt32(out int targetTextureId) ||
                    !row.TryGetProperty("donorLevelKey", out JsonElement keyElement) ||
                    keyElement.ValueKind != JsonValueKind.String ||
                    !row.TryGetProperty("donorLevelName", out JsonElement nameElement) ||
                    nameElement.ValueKind != JsonValueKind.String ||
                    !row.TryGetProperty("donorWadEntry", out JsonElement wadElement) ||
                    !wadElement.TryGetInt32(out int donorWadEntry))
                {
                    // The existing manifest-envelope/strict-v3 loader owns
                    // malformed-row reporting. This gate handles well-shaped
                    // rows whose donor identity is nevertheless forged.
                    continue;
                }

                string persistedKey = keyElement.GetString() ?? "";
                string persistedName = nameElement.GetString() ?? "";
                string normalizedKey = LevelCatalog.NormalizeKey(persistedKey);
                if (IsId65BlankLabKey(normalizedKey) ||
                    donorWadEntry == UnusedLevel65BlankLevelLabProfileRegistry.DataWadEntry)
                {
                    reason =
                        $"Saved native terrain texture relocation for {destination.DisplayName} T{targetTextureId} is blocked because its persisted donor identity names the source-bound ID65 Lab/WAD {UnusedLevel65BlankLevelLabProfileRegistry.DataWadEntry}.";
                    return true;
                }

                LevelDefinition? donor = _retailCatalog.FindByKey(persistedKey);
                if (donor == null ||
                    !string.Equals(persistedKey, donor.Key, StringComparison.Ordinal) ||
                    !string.Equals(persistedName, donor.DisplayName, StringComparison.Ordinal) ||
                    donorWadEntry != donor.SourceWadEntry)
                {
                    string expected = donor == null
                        ? "one exact retail catalog key/name/WAD identity"
                        : $"'{donor.Key}'/'{donor.DisplayName}'/WAD {donor.SourceWadEntry}";
                    reason =
                        $"Saved native terrain texture relocation for {destination.DisplayName} T{targetTextureId} is blocked because persisted donor '{persistedKey}'/'{persistedName}'/WAD {donorWadEntry} does not exactly match {expected}.";
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // General manifest validation reports unreadable/malformed files.
        }

        reason = "";
        return false;
    }

    private bool TryGetRetailTerrainTextureRelocationDonorIdentityError(
        LevelDefinition destination,
        IReadOnlyList<NativeTerrainTextureRelocationEdit> relocations,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(relocations);
        foreach (NativeTerrainTextureRelocationEdit edit in relocations)
        {
            string donorKey = LevelCatalog.NormalizeKey(edit.DonorLevelKey);
            if (IsId65BlankLabKey(donorKey) ||
                edit.DonorWadEntry == UnusedLevel65BlankLevelLabProfileRegistry.DataWadEntry)
            {
                reason =
                    $"Saved native terrain texture relocation for {destination.DisplayName} T{edit.TargetTextureId} is blocked because its donor identity names the source-bound ID65 Lab/WAD {UnusedLevel65BlankLevelLabProfileRegistry.DataWadEntry}. " +
                    "ID65 resident textures are available only inside the Lab and cannot enter retail Build Safety or Create BIN.";
                return true;
            }

            LevelDefinition? donor = _retailCatalog.FindByKey(donorKey);
            if (donor == null ||
                UnusedLevel65BlankLevelLabProfileRegistry.IsLabLevel(donor))
            {
                reason =
                    $"Saved native terrain texture relocation for {destination.DisplayName} T{edit.TargetTextureId} is blocked because donor key '{edit.DonorLevelKey}' is not one of the exact retail catalog levels.";
                return true;
            }

            if (!string.Equals(donorKey, LevelCatalog.NormalizeKey(donor.Key), StringComparison.Ordinal) ||
                !string.Equals(edit.DonorLevelName, donor.DisplayName, StringComparison.Ordinal) ||
                edit.DonorWadEntry != donor.SourceWadEntry)
            {
                reason =
                    $"Saved native terrain texture relocation for {destination.DisplayName} T{edit.TargetTextureId} is blocked because donor '{edit.DonorLevelKey}'/'{edit.DonorLevelName}'/WAD {edit.DonorWadEntry} does not exactly match retail catalog donor '{donor.Key}'/'{donor.DisplayName}'/WAD {donor.SourceWadEntry}.";
                return true;
            }
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
            allowResidentTextureEdits: false,
            out reason);

    private static bool TryValidateId65BlankLabTerrainEdits(
        GeometryCandidate geometry,
        bool requireAtLeastOneEdit,
        bool allowResidentTextureEdits,
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
            (!polygon.HasHeightEdit &&
             !(allowResidentTextureEdits && polygon.HasTextureEdit)) ||
            polygon.HasPositionEdit ||
            (!allowResidentTextureEdits && polygon.HasTextureEdit) ||
            (polygon.HasTextureEdit &&
             (polygon.TextureId is < 0 or >= UnusedLevel65BlankLevelLabProfileRegistry.ResidentTextureCount ||
              polygon.OriginalTextureId is < 0 or >= UnusedLevel65BlankLevelLabProfileRegistry.ResidentTextureCount)) ||
            polygon.HasTextureVisualEdit ||
            polygon.HasStructureEdit ||
            polygon.HasSurfaceBehaviorEdit);
        if (unsupported != null)
        {
            reason = allowResidentTextureEdits
                ? $"Face {unsupported.RuntimeKey} is outside the editor-safe ID65 authored layer. " +
                  $"Only existing high-detail (HP) Z edits and resident texture IDs 0-{UnusedLevel65BlankLevelLabProfileRegistry.ResidentTextureCount - 1} are saved; " +
                  "Add/remove, LP, XY, custom/cross-level textures, visual/material transfer, and surface edits remain unavailable."
                : $"Face {unsupported.RuntimeKey} is outside the first runtime-safe gate. " +
                  "Disposable CUEs currently accept existing high-detail (HP), Z-only edits; " +
                  "resident texture previews and every other texture/surface/structure edit remain editor-only or unavailable.";
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

    private bool TryBlockEditorMutationDuringId65BlankLabManualOperation(string action)
    {
        if (_workspaceTransitionBusy)
        {
            _statusText.Text =
                $"Wait for the active workspace transition to finish before {action}. No editor state or saved file was changed.";
            return true;
        }
        if (!_id65BlankLabBusy || _id65BlankLabManualOperation == null)
            return false;

        _statusText.Text =
            $"Wait for the active ID65 Lab {_id65BlankLabManualOperation.Name} operation to finish before {action}. No editor state or saved file was changed.";
        return true;
    }

    private EditorMutationSceneIdentity CaptureEditorMutationSceneIdentity(LevelDefinition level) => new(
        Workspace: _workspace,
        WorkspaceRoot: Path.GetFullPath(_workspace.RootPath),
        Level: level,
        Mobys: _currentMobys,
        LevelLoadRequestId: _levelLoadRequestId,
        Id65OperationGeneration: _id65BlankLabManualOperationGeneration,
        WorkspaceTransitionGeneration: _workspaceTransitionGeneration);

    private bool IsCurrentEditorMutationSceneIdentity(EditorMutationSceneIdentity captured) =>
        !_id65BlankLabBusy &&
        _id65BlankLabManualOperation == null &&
        !_workspaceTransitionBusy &&
        !_regularEditorPersistenceBusy &&
        !_buildSafetyBusy &&
        captured.Id65OperationGeneration == _id65BlankLabManualOperationGeneration &&
        captured.WorkspaceTransitionGeneration == _workspaceTransitionGeneration &&
        captured.LevelLoadRequestId == _levelLoadRequestId &&
        ReferenceEquals(captured.Workspace, _workspace) &&
        PathsEqual(captured.WorkspaceRoot, _workspace.RootPath) &&
        ReferenceEquals(captured.Level, _currentLevel) &&
        ReferenceEquals(captured.Mobys, _currentMobys);

    private ObjectDialogSceneIdentity CaptureObjectDialogSceneIdentity(
        LevelDefinition level,
        Moby target) => new(
            Scene: CaptureEditorMutationSceneIdentity(level),
            Target: target,
            TargetIndex: target.Index,
            TargetTrueIndex: target.TrueIndex,
            TargetWasRemoved: target.IsRemoved,
            MobyEditSignature: BuildMobyEditSignature(_currentMobys));

    private bool IsCurrentObjectDialogSceneIdentity(
        ObjectDialogSceneIdentity captured,
        bool requireOriginalMobySignature = true) =>
        IsCurrentEditorMutationSceneIdentity(captured.Scene) &&
        captured.Target.Index == captured.TargetIndex &&
        captured.Target.TrueIndex == captured.TargetTrueIndex &&
        captured.Target.IsRemoved == captured.TargetWasRemoved &&
        _currentMobys.Any(candidate => ReferenceEquals(candidate, captured.Target)) &&
        (!requireOriginalMobySignature ||
         string.Equals(
             BuildMobyEditSignature(_currentMobys),
             captured.MobyEditSignature,
             StringComparison.Ordinal));

    private SourceDiscOperationIdentity CaptureSourceDiscOperationIdentity() => new(
        Workspace: _workspace,
        WorkspaceRoot: Path.GetFullPath(_workspace.RootPath),
        Level: _currentLevel,
        Geometry: _currentGeometry,
        Mobys: _currentMobys,
        LevelLoadRequestId: _levelLoadRequestId,
        Id65OperationGeneration: _id65BlankLabManualOperationGeneration,
        PortableCacheBuildGeneration: _portableCacheBuildGeneration,
        ProjectDataImportGeneration: _projectDataImportGeneration,
        RegularPersistenceGeneration: _regularEditorPersistenceGeneration,
        BuildSafetyGeneration: _buildSafetyGeneration,
        WorkspaceTransitionGeneration: _workspaceTransitionGeneration);

    private bool IsCurrentSourceDiscOperationIdentity(
        SourceDiscOperationIdentity captured,
        bool requireSceneIdentity = true,
        bool allowPortableCacheBuild = false,
        bool allowProjectDataImport = false) =>
        !_id65BlankLabBusy &&
        _id65BlankLabManualOperation == null &&
        !_workspaceTransitionBusy &&
        captured.Id65OperationGeneration == _id65BlankLabManualOperationGeneration &&
        captured.WorkspaceTransitionGeneration == _workspaceTransitionGeneration &&
        captured.PortableCacheBuildGeneration == _portableCacheBuildGeneration &&
        captured.ProjectDataImportGeneration == _projectDataImportGeneration &&
        captured.RegularPersistenceGeneration == _regularEditorPersistenceGeneration &&
        captured.BuildSafetyGeneration == _buildSafetyGeneration &&
        (allowPortableCacheBuild || !_buildingPortableCache) &&
        (allowProjectDataImport || !_projectDataImportBusy) &&
        !_regularEditorPersistenceBusy &&
        !_buildSafetyBusy &&
        ReferenceEquals(captured.Workspace, _workspace) &&
        PathsEqual(captured.WorkspaceRoot, _workspace.RootPath) &&
        (!requireSceneIdentity ||
         captured.LevelLoadRequestId == _levelLoadRequestId &&
         ReferenceEquals(captured.Level, _currentLevel) &&
         ReferenceEquals(captured.Geometry, _currentGeometry) &&
         ReferenceEquals(captured.Mobys, _currentMobys));

    private bool TryBlockId65ObjectMutation(string action)
    {
        if (TryBlockEditorMutationDuringId65BlankLabManualOperation(action))
            return true;

        if (!IsId65BlankLabObjectInspectionOnly())
            return false;

        _statusText.Text =
            $"ID65 Lab blocked {action}: resident Mobys and native movement records are inspection-only in this profile.";
        return true;
    }

    private bool TryBlockId65SkyMutation(string action)
    {
        if (TryBlockEditorMutationDuringId65BlankLabManualOperation($"sky {action}"))
            return true;

        if (!IsCurrentId65BlankLab())
            return false;

        _statusText.Text =
            $"ID65 Lab sky {action} refused: sky editing is unavailable in this profile. " +
            "The locked-base sky remains unchanged; no ID65 sky plan or test CUE was changed, and normal Create BIN does not include ID65.";
        return true;
    }

    private bool TryBlockId65LevelNameMutation(string action)
    {
        if (TryBlockEditorMutationDuringId65BlankLabManualOperation($"level-name {action}"))
            return true;

        if (!IsCurrentId65BlankLab())
            return false;

        _statusText.Text =
            $"ID65 Lab level-name {action} refused: level-name editing is unavailable in this profile. " +
            "The locked TOWN SQUARE Inventory identity and retail Town Square name plan remain unchanged; normal Create BIN does not include ID65.";
        return true;
    }

    private bool TryBlockId65UnsupportedTerrainMutation(
        TerrainPolygon? terrain,
        string action,
        bool movesXy = false,
        bool structural = false,
        bool textureOrSurface = false,
        bool releaseViewportGesture = false)
    {
        if (TryBlockTerrainMutationDuringId65BlankLabManualOperation(action))
            return true;

        if (!IsCurrentId65BlankLab())
        {
            if (!_releaseMode)
                return false;

            if (textureOrSurface && !releaseViewportGesture)
                return false;

            if (releaseViewportGesture || structural || movesXy)
            {
                _statusText.Text =
                    $"Beta V5 blocked {action}: release terrain editing uses the visible HP Raise/Lower and guarded texture-paint controls. " +
                    "XY movement, drag sculpting, full edit dialogs, add/remove, and hidden viewport shortcuts remain research-only.";
                return true;
            }

            if (terrain == null ||
                !string.Equals(terrain.Detail, "hp", StringComparison.OrdinalIgnoreCase) ||
                terrain.IsTerrainRemoved ||
                terrain.IsTerrainAddClone)
            {
                _statusText.Text =
                    $"Beta V5 blocked {action}: only existing high-detail (HP) faces may receive Z-height edits.";
                return true;
            }

            return false;
        }

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
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        string fullDirectory = Path.GetFullPath(directoryPath);
        if (!Directory.Exists(fullDirectory))
            return new Id65BlankLabAuthoredLayerSnapshot(DirectoryExisted: false, Files: []);

        List<string> files = [];
        foreach (string path in Directory.EnumerateFiles(fullDirectory, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relative = Path.GetRelativePath(fullDirectory, path).Replace('\\', '/');
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            string hash = Convert.ToHexString(
                await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
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
        string skyPlanPath = NativeSkyEditStore.PlanPath(_workspace.RootPath, labKey);
        TextTargetEntry townSquareTextTarget = _textTargets.Find("townsquare")
            ?? throw new InvalidOperationException("The ID65 mutation-boundary probe found no Town Square name target.");
        string townSquareNamePlanPath = LevelTextEditStore.PlanPath(_workspace.RootPath, townSquareTextTarget);
        string[] guardedMutationPaths =
        [
            objectEditsPath,
            Id65BlankLabTerrainEditsPath(),
            materialOverridesPath,
            customTexturesPath,
            nativeRelocationsPath,
            musicPlanPath,
            skyPlanPath,
            townSquareNamePlanPath
        ];
        Dictionary<string, byte[]?> guardedMutationFilesBefore = guardedMutationPaths.ToDictionary(
            path => path,
            path => File.Exists(path) ? File.ReadAllBytes(path) : null,
            StringComparer.OrdinalIgnoreCase);
        bool originalMobySnapDisabled = _mobyTerrainSnapDisabledKeys.Contains(MobyTerrainSnapKey(moby));
        string originalCustomTextureState = JsonSerializer.Serialize(_customTerrainTextures);
        string originalNativeRelocationState = JsonSerializer.Serialize(_nativeTerrainTextureRelocations);
        TerrainTexturePaintStageSnapshot originalHpPaintState = TerrainTexturePaintStageSnapshot.Capture(hp);

        void RestoreGuardedMutationFile(string path)
        {
            byte[]? contents = guardedMutationFilesBefore[path];
            if (contents == null)
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _workspace.RootPath);
            File.WriteAllBytes(path, contents);
        }

        string CaptureSkyAndNameState() => JsonSerializer.Serialize(new
        {
            Mode = (_skyboxModeBox.SelectedItem as SkyboxEditModeOption)?.Id,
            Preset = (_skyboxPresetBox.SelectedItem as SkyboxPreset)?.Id,
            OriginalPreset = (_skyboxOriginalPresetBox.SelectedItem as OriginalSkyboxPreset)?.Id,
            CustomPalette = _skyboxCustomPaletteBox.Text,
            Donor = (_skyboxDonorBox.SelectedItem as LevelDefinition)?.Key,
            ImportPath = _skyboxImportPathBox.Text,
            EnvironmentEnabled = _skyboxEnvironmentEnabledBox.IsChecked,
            EnvironmentStrength = _skyboxEnvironmentStrengthSlider.Value,
            EnvironmentBrightness = _skyboxEnvironmentBrightnessSlider.Value,
            EnvironmentSaturation = _skyboxEnvironmentSaturationSlider.Value,
            EnvironmentTint = _skyboxEnvironmentTintBox.Text,
            EnvironmentTintStrength = _skyboxEnvironmentTintStrengthSlider.Value,
            EnvironmentScene = _skyboxEnvironmentSceneBox.IsChecked,
            EnvironmentTextures = _skyboxEnvironmentTextureBox.IsChecked,
            EnvironmentMobys = _skyboxEnvironmentMobyBox.IsChecked,
            NameTarget = (_levelTextTargetBox.SelectedItem as TextTargetEntry)?.LevelKey,
            NameReplacement = _levelTextReplacementBox.Text,
            SkyDetails = _skyboxDetails.Text,
            NameDetails = _levelTextDetails.Text
        });

        void AssertRefusalStatus(string operation, params string[] required)
        {
            string status = _statusText.Text ?? "";
            if (required.Any(term => !status.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"The ID65 {operation} refusal status was not truthful: {status}");
            }
        }

        try
        {
            await NativeSkyEditStore.SaveAsync(_workspace.RootPath, new NativeSkyEditPlan(
                Version: 2,
                SavedAt: DateTimeOffset.UtcNow,
                LevelKey: labKey,
                LevelName: UnusedLevel65BlankLevelLabProfileRegistry.Definition.DisplayName,
                Mode: NativeSkyEditPlan.SwapMode,
                PalettePreset: SkyboxPresetCatalog.NativeDefault.Id,
                CustomPaletteHex: "",
                DonorLevelKey: originalLevel.Key,
                ImportedSkyPath: "",
                ImportedSkySha256: ""));
            await LevelTextEditStore.SaveAsync(_workspace.RootPath, townSquareTextTarget, "TOWN PLAZA");
            byte[] skyFixtureBytes = File.ReadAllBytes(skyPlanPath);
            byte[] nameFixtureBytes = File.ReadAllBytes(townSquareNamePlanPath);
            Id65BlankLabAuthoredLayerSnapshot outputBefore =
                await CaptureId65BlankLabAuthoredLayerAsync(Path.Combine(_workspace.RootPath, "output"));

            _mobyClipboard = MobyClipboard.From(moby, originalLevel, terrainGroundOffset: null);
            _selectedMoby = moby;
            _selectedTerrain = hp;
            _selectedTerrainIndex = hpIndex;
            _currentLevel = UnusedLevel65BlankLevelLabProfileRegistry.Definition;
            UpdateLevelToolPanels(_currentLevel);

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

            if (_skyboxModeBox.IsEnabled ||
                _skyboxPresetBox.IsEnabled ||
                _skyboxOriginalPresetBox.IsEnabled ||
                _skyboxCustomPaletteBox.IsEnabled ||
                _skyboxDonorBox.IsEnabled ||
                _skyboxImportPathBox.IsEnabled ||
                _skyboxPalettePanel.IsEnabled ||
                _skyboxSwapPanel.IsEnabled ||
                _skyboxOriginalPresetPanel.IsEnabled ||
                _skyboxImportPanel.IsEnabled ||
                _skyboxEnvironmentEnabledBox.IsEnabled ||
                _skyboxEnvironmentStrengthSlider.IsEnabled ||
                _skyboxEnvironmentBrightnessSlider.IsEnabled ||
                _skyboxEnvironmentSaturationSlider.IsEnabled ||
                _skyboxEnvironmentTintStrengthSlider.IsEnabled ||
                _skyboxEnvironmentTintBox.IsEnabled ||
                _skyboxEnvironmentSceneBox.IsEnabled ||
                _skyboxEnvironmentTextureBox.IsEnabled ||
                _skyboxEnvironmentMobyBox.IsEnabled ||
                _skyboxEnvironmentActorBox.IsEnabled ||
                _skyboxEnvironmentChestBox.IsEnabled ||
                _skyboxEnvironmentSceneryBox.IsEnabled ||
                _skyboxEnvironmentDragonBox.IsEnabled ||
                _skyboxPaletteImportButton == null ||
                _skyboxPaletteImportButton.IsEnabled ||
                _skyboxCustomImportButton == null ||
                _skyboxCustomImportButton.IsEnabled ||
                _skyboxMatchEnvironmentButton == null ||
                _skyboxMatchEnvironmentButton.IsEnabled ||
                _skyboxSaveButton == null ||
                _skyboxSaveButton.IsEnabled ||
                _skyboxCreateTestButton?.IsEnabled == true ||
                _skyboxResetButton == null ||
                _skyboxResetButton.IsEnabled ||
                _levelTextTargetBox.IsEnabled ||
                _levelTextReplacementBox.IsEnabled ||
                _levelTextSaveButton == null ||
                _levelTextSaveButton.IsEnabled ||
                _levelTextResetButton == null ||
                _levelTextResetButton.IsEnabled)
            {
                throw new InvalidOperationException("ID65 left a Skybox or Level Name editing control enabled.");
            }
            if ((_skyboxModeBox.SelectedItem as SkyboxEditModeOption)?.Id != NativeSkyEditPlan.PaletteMode ||
                !string.IsNullOrWhiteSpace(_skyboxCustomPaletteBox.Text) ||
                _levelTextTargetBox.SelectedItem != null ||
                !string.IsNullOrEmpty(_levelTextReplacementBox.Text) ||
                !(_skyboxDetails.Text ?? "").Contains("unavailable", StringComparison.OrdinalIgnoreCase) ||
                !(_levelTextDetails.Text ?? "").Contains("unavailable", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "ID65 loaded an unsupported saved sky/name target instead of presenting the locked unavailable state.");
            }
            if (!TryGetId65BlankLabUnsupportedArtifact(out string skyArtifactReason) ||
                !skyArtifactReason.Contains("skybox", StringComparison.OrdinalIgnoreCase) ||
                !Id65BlankLabHasAnyAuthoredArtifacts())
            {
                throw new InvalidOperationException(
                    "The ID65 unsupported-artifact boundary did not recognize its skybox edit plan.");
            }

            _skyboxModeBox.SelectedItem = SkyboxEditModeOptions.First(option =>
                option.Id == NativeSkyEditPlan.SwapMode);
            _skyboxPresetBox.SelectedItem = SkyboxPresetCatalog.NativePresets.Last();
            _skyboxOriginalPresetBox.SelectedItem = SkyboxPresetCatalog.OriginalPresets.Last();
            _skyboxCustomPaletteBox.Text = "#010203 #A0B0C0";
            _skyboxDonorBox.SelectedItem = originalLevel;
            _skyboxImportPathBox.Text = "blocked-id65-test.sky";
            _skyboxEnvironmentEnabledBox.IsChecked = true;
            _skyboxEnvironmentStrengthSlider.Value = 37;
            _skyboxEnvironmentBrightnessSlider.Value = 83;
            _skyboxEnvironmentSaturationSlider.Value = 71;
            _skyboxEnvironmentTintBox.Text = "#112233";
            _skyboxEnvironmentTintStrengthSlider.Value = 29;
            _skyboxEnvironmentSceneBox.IsChecked = false;
            _skyboxEnvironmentTextureBox.IsChecked = false;
            _skyboxEnvironmentMobyBox.IsChecked = true;
            _levelTextTargetBox.SelectedItem = townSquareTextTarget;
            _levelTextReplacementBox.Text = "TOWN PLAZA";
            string stagedSkyAndNameState = CaptureSkyAndNameState();

            bool skySaved = await SaveSkyboxPlanAsync();
            if (skySaved)
                throw new InvalidOperationException("ID65 sky Save reported success for an unavailable capability.");
            AssertRefusalStatus("sky save", "sky save refused", "unavailable", "locked-base", "normal Create BIN does not include ID65");
            ResetSkyboxPlan();
            AssertRefusalStatus("sky reset", "sky reset refused", "unavailable", "locked-base", "normal Create BIN does not include ID65");
            await CreateSkyboxCueAsync();
            AssertRefusalStatus("sky test CUE creation", "test CUE creation refused", "unavailable", "locked-base", "normal Create BIN does not include ID65");
            await MatchLevelTerrainPaletteAsync();
            AssertRefusalStatus("sky environment match", "environment matching refused", "unavailable", "locked-base");
            await ChooseSkyPaletteImageAsync();
            AssertRefusalStatus("sky palette import", "palette import refused", "unavailable", "locked-base");
            await ChooseCustomSkyAsync();
            AssertRefusalStatus("custom sky import", "custom sky import refused", "unavailable", "locked-base");
            await SaveLevelTextPlanAsync();
            AssertRefusalStatus("level-name save", "level-name save refused", "unavailable", "TOWN SQUARE", "normal Create BIN does not include ID65");
            ResetLevelTextPlan();
            AssertRefusalStatus("level-name reset", "level-name reset refused", "unavailable", "TOWN SQUARE", "normal Create BIN does not include ID65");

            Id65BlankLabAuthoredLayerSnapshot outputAfter =
                await CaptureId65BlankLabAuthoredLayerAsync(Path.Combine(_workspace.RootPath, "output"));
            if (CaptureSkyAndNameState() != stagedSkyAndNameState ||
                !File.ReadAllBytes(skyPlanPath).SequenceEqual(skyFixtureBytes) ||
                !File.ReadAllBytes(townSquareNamePlanPath).SequenceEqual(nameFixtureBytes) ||
                outputBefore.DirectoryExisted != outputAfter.DirectoryExisted ||
                !outputBefore.Files.SequenceEqual(outputAfter.Files, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "An ID65 sky/name method changed staged control state, a saved plan, or test-CUE output despite refusing the operation.");
            }

            RestoreGuardedMutationFile(skyPlanPath);
            RestoreGuardedMutationFile(townSquareNamePlanPath);

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
            if (BuildTerrainEditSignature(_currentGeometry) != terrainSignatureBeforePaint ||
                TerrainTexturePaintStageSnapshot.Capture(hp) != originalHpPaintState ||
                JsonSerializer.Serialize(_customTerrainTextures) != originalCustomTextureState ||
                JsonSerializer.Serialize(_nativeTerrainTextureRelocations) != originalNativeRelocationState)
            {
                throw new InvalidOperationException(
                    "The blocked ID65 selected/linked terrain-look route staged texture or surface state.");
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

            return "Skybox and Level Name controls, saved-plan loading, Save/Reset/Create/import/match methods, unsupported-artifact detection, state, plans, and test-CUE output stayed unavailable and write-free; disabled/direct object Z and terrain-snap controls preserved selected+linked Mobys, snap state, signatures, and files; selected/linked full terrain-look transfer remained blocked; Level Music controls/save/reset stayed unavailable and write-free; double-click, paste, object/native-movement viewport mutations, object move/rotate/remove, LP/XY/structural terrain gestures, mode reset, and inspection-only save refusal passed; existing HP Z remained editable";
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
            UpdateLevelToolPanels(originalLevel);
        }
    }

    internal async Task<string> AssertId65ResidentTexturePaintingForTestingAsync()
    {
        if (!IsCurrentId65BlankLab() || _currentLevel == null || _currentGeometry == null ||
            _id65BlankLabManifest == null || _id65BlankLabPaths == null)
        {
            throw new InvalidOperationException(
                "The resident-texture probe requires the validated, loaded ID65 Lab.");
        }

        string labKey = UnusedLevel65BlankLevelLabProfileRegistry.Key;
        string isolatedEditsPath = Id65BlankLabTerrainEditsPath();
        string retailEditsPath = Path.Combine(_workspace.RootPath, $"{labKey}-terrain-edits.json");
        string overlayPath = Id65BlankLabOverlayPath(_id65BlankLabPaths);
        string derivedBindingPath = Id65BlankLabDerivedCacheBindingPath(_id65BlankLabPaths);
        string materialOverridesPath = Path.Combine(
            _workspace.RootPath,
            $"{labKey}-terrain-material-overrides.json");
        LevelDefinition retailDonorGuardTarget = _retailCatalog.Levels.First();
        string retailRelocationPath = NativeTerrainTextureRelocationEditStore.ManifestPath(
            _workspace.RootPath,
            retailDonorGuardTarget.Key);
        byte[]? isolatedBefore = File.Exists(isolatedEditsPath)
            ? File.ReadAllBytes(isolatedEditsPath)
            : null;
        byte[]? retailBefore = File.Exists(retailEditsPath)
            ? File.ReadAllBytes(retailEditsPath)
            : null;
        byte[]? materialOverridesBefore = File.Exists(materialOverridesPath)
            ? File.ReadAllBytes(materialOverridesPath)
            : null;
        byte[]? retailRelocationBefore = File.Exists(retailRelocationPath)
            ? File.ReadAllBytes(retailRelocationPath)
            : null;
        byte[] derivedBindingBefore = File.ReadAllBytes(derivedBindingPath);
        long overlayLengthBefore = new FileInfo(overlayPath).Length;
        int loadedEditsBefore = _loadedTerrainEdits;
        string savedSignatureBefore = _savedTerrainEditSignature;
        TerrainPolygon? selectedBefore = _selectedTerrain;
        int selectedIndexBefore = _selectedTerrainIndex;
        int selectedPointBefore = _selectedTerrainPointIndex;
        TerrainTexturePaintBrush? activeBrushBefore = _activeTerrainTexturePaintBrush;

        TerrainPolygon target = _currentGeometry.Polygons
            .Where(face =>
                !face.IsTerrainRemoved &&
                !face.IsTerrainAddClone &&
                string.Equals(face.Detail, "hp", StringComparison.OrdinalIgnoreCase) &&
                face.OriginalTextureId is >= 0 and < UnusedLevel65BlankLevelLabProfileRegistry.ResidentTextureCount &&
                !face.IsTerrainEdited)
            .OrderBy(face => face.RuntimeKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "The loaded ID65 Lab has no clean resident-textured HP face.");
        int targetIndex = _currentGeometry.Polygons.IndexOf(target);
        float[] targetZBefore = target.TerrainVertexDeltas().ToArray();
        Vector2f[] targetXyBefore = target.TerrainVertexXYDeltas().ToArray();
        TerrainTexturePaintStageSnapshot targetPaintBefore =
            TerrainTexturePaintStageSnapshot.Capture(target);

        void RestoreFile(string path, byte[]? contents)
        {
            if (contents == null)
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _workspace.RootPath);
            File.WriteAllBytes(path, contents);
        }

        async Task AssertPersistedRetailDonorIdentityRefusedAsync(
            string donorLevelKey,
            string donorLevelName,
            int donorWadEntry,
            string fixtureName,
            string? rawPersistedDonorKey = null)
        {
            RestoreFile(retailRelocationPath, retailRelocationBefore);
            await NativeTerrainTextureRelocationEditStore.AddOrReplaceArtOnlyAsync(
                _workspace.RootPath,
                retailDonorGuardTarget.Key,
                retailDonorGuardTarget.DisplayName,
                targetTextureId: 0,
                donorLevelKey,
                donorLevelName,
                donorWadEntry,
                donorTextureId: 0);
            if (!string.IsNullOrWhiteSpace(rawPersistedDonorKey))
            {
                JsonObject manifestRoot = JsonNode.Parse(
                    await File.ReadAllTextAsync(retailRelocationPath))?.AsObject()
                    ?? throw new InvalidOperationException(
                        $"The {fixtureName} donor fixture manifest is empty.");
                JsonObject firstRow = manifestRoot["relocations"]?.AsArray()[0]?.AsObject()
                    ?? throw new InvalidOperationException(
                        $"The {fixtureName} donor fixture manifest has no first row.");
                firstRow["donorLevelKey"] = rawPersistedDonorKey;
                await File.WriteAllTextAsync(
                    retailRelocationPath,
                    manifestRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
            if (!TryGetInvalidRetailTerrainTextureRelocationDonorBlockReason(
                    out string donorBlockReason))
            {
                throw new InvalidOperationException(
                    $"The {fixtureName} persisted donor fixture did not block Build Safety/Create BIN.");
            }

            bool retailLoadRefused = false;
            try
            {
                _ = LoadLevelData(retailDonorGuardTarget.Key);
            }
            catch (InvalidDataException ex) when (
                ex.Message.Contains("Saved native terrain texture relocation", StringComparison.Ordinal) &&
                ex.Message.Contains("blocked", StringComparison.Ordinal))
            {
                retailLoadRefused = true;
            }
            if (!retailLoadRefused)
            {
                throw new InvalidOperationException(
                    $"The {fixtureName} persisted donor fixture loaded into a retail level.");
            }
        }

        try
        {
            LevelDefinition canonicalRetailDonor = _retailCatalog.Levels
                .First(level => !string.Equals(
                    LevelCatalog.NormalizeKey(level.Key),
                    LevelCatalog.NormalizeKey(retailDonorGuardTarget.Key),
                    StringComparison.Ordinal));
            LevelDefinition alternateRetailDonor = _retailCatalog.Levels
                .First(level =>
                    level.SourceWadEntry != canonicalRetailDonor.SourceWadEntry &&
                    level.SourceWadEntry != UnusedLevel65BlankLevelLabProfileRegistry.DataWadEntry);
            await AssertPersistedRetailDonorIdentityRefusedAsync(
                labKey,
                _currentLevel.DisplayName,
                canonicalRetailDonor.SourceWadEntry,
                "ID65-key");
            await AssertPersistedRetailDonorIdentityRefusedAsync(
                canonicalRetailDonor.Key,
                canonicalRetailDonor.DisplayName,
                UnusedLevel65BlankLevelLabProfileRegistry.DataWadEntry,
                "ID65-WAD80");
            await AssertPersistedRetailDonorIdentityRefusedAsync(
                "unknown-retail-donor",
                "Unknown Retail Donor",
                canonicalRetailDonor.SourceWadEntry,
                "unknown-key");
            await AssertPersistedRetailDonorIdentityRefusedAsync(
                canonicalRetailDonor.Key,
                $"{canonicalRetailDonor.DisplayName} forged",
                canonicalRetailDonor.SourceWadEntry,
                "mismatched-name");
            await AssertPersistedRetailDonorIdentityRefusedAsync(
                canonicalRetailDonor.Key,
                canonicalRetailDonor.DisplayName,
                alternateRetailDonor.SourceWadEntry,
                "mismatched-WAD");
            await AssertPersistedRetailDonorIdentityRefusedAsync(
                canonicalRetailDonor.Key,
                canonicalRetailDonor.DisplayName,
                canonicalRetailDonor.SourceWadEntry,
                "noncanonical-key-spelling",
                $"{canonicalRetailDonor.Key[0]}_{canonicalRetailDonor.Key[1..]}");
            RestoreFile(retailRelocationPath, retailRelocationBefore);

            if (!string.Equals(
                    ResolveTerrainTextureSourceImage(_currentLevel),
                    _id65BlankLabPaths.LockedBaseImagePath,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    FindCachedTerrainOverlayPath(labKey),
                    overlayPath,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    ResolveTerrainTextureCacheWorkspaceRoot(labKey),
                    _id65BlankLabPaths.RootPath,
                    StringComparison.OrdinalIgnoreCase) ||
                !await IsCurrentId65BlankLabDerivedCacheAsync(
                    _id65BlankLabPaths,
                    _id65BlankLabManifest) ||
                !IsCurrentTerrainTexturePreviewCacheForLevel(
                    _id65BlankLabPaths.RootPath,
                    labKey))
            {
                throw new InvalidOperationException(
                    "ID65 did not resolve its texture catalog, overlay, and preview cache from the exact locked Lab workspace.");
            }

            Id65BlankLabDerivedCacheBinding forgedBinding = JsonSerializer.Deserialize<Id65BlankLabDerivedCacheBinding>(
                derivedBindingBefore)
                ?? throw new InvalidOperationException("The ID65 derived-cache binding is empty.");
            File.WriteAllText(
                derivedBindingPath,
                JsonSerializer.Serialize(forgedBinding with { SceneOverlaySha256 = new string('0', 64) }));
            if (await IsCurrentId65BlankLabDerivedCacheAsync(_id65BlankLabPaths, _id65BlankLabManifest))
                throw new InvalidOperationException("ID65 admitted a forged derived-cache binding.");

            GeometryCandidate sceneBeforeSynchronousReload = _currentGeometry;
            string editsBeforeSynchronousReload = BuildTerrainEditSignature(_currentGeometry);
            if (SelectLevel(_currentLevel) ||
                !ReferenceEquals(_currentGeometry, sceneBeforeSynchronousReload) ||
                BuildTerrainEditSignature(_currentGeometry) != editsBeforeSynchronousReload ||
                !(_statusText.Text ?? "").Contains("awaited source-bound loader", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The legacy synchronous level route reloaded ID65 or changed its scene while the derived binding was forged.");
            }
            File.WriteAllBytes(derivedBindingPath, derivedBindingBefore);

            await using (FileStream overlayAppend = new(
                overlayPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.None))
            {
                overlayAppend.WriteByte((byte)' ');
                await overlayAppend.FlushAsync();
            }
            if (await IsCurrentId65BlankLabDerivedCacheAsync(_id65BlankLabPaths, _id65BlankLabManifest))
                throw new InvalidOperationException("ID65 admitted a validly shaped but byte-divergent source overlay.");
            using (FileStream overlayRestore = new(overlayPath, FileMode.Open, FileAccess.Write, FileShare.None))
                overlayRestore.SetLength(overlayLengthBefore);
            if (!await IsCurrentId65BlankLabDerivedCacheAsync(_id65BlankLabPaths, _id65BlankLabManifest))
                throw new InvalidOperationException("The exact restored ID65 derived cache did not validate again.");

            _selectedTerrain = target;
            _selectedTerrainIndex = targetIndex;
            _selectedTerrainPointIndex = -1;
            TerrainTextureSwapChoice[] residentPalette = BuildTerrainTextureSwapChoices()
                .Where(choice =>
                    choice.CanUseArt &&
                    !choice.RequiresPrivateCopy &&
                    choice.TextureId is >= 0 and < UnusedLevel65BlankLevelLabProfileRegistry.ResidentTextureCount)
                .GroupBy(choice => choice.TextureId)
                .Select(group => group.First())
                .OrderBy(choice => choice.TextureId)
                .ToArray();
            if (!residentPalette.Select(choice => choice.TextureId).SequenceEqual(
                    Enumerable.Range(0, UnusedLevel65BlankLevelLabProfileRegistry.ResidentTextureCount)))
            {
                throw new InvalidOperationException(
                    "The locked ID65 palette did not expose every resident texture ID exactly once.");
            }
            TerrainTextureSwapChoice source = residentPalette
                .FirstOrDefault(choice => choice.TextureId != target.OriginalTextureId)
                ?? throw new InvalidOperationException(
                    "The locked ID65 payload exposed no second source-verified resident texture.");

            float[] raisedZ = targetZBefore.ToArray();
            raisedZ[0] += 1;
            target.ApplyTerrainVertexDeltas(raisedZ);
            await PersistCurrentTerrainEditsAsync();

            string preservedSurface = target.Surface;
            string preservedSurfaceSource = target.SurfaceSource;
            ColorRgba preservedSurfaceColor = target.SurfaceColor;
            string preservedBehavior = target.Behavior;
            string preservedBehaviorSource = target.BehaviorSource;
            TerrainTexturePaintBrush brush = TerrainTexturePaintBrush.FromSameLevel(
                labKey,
                _currentLevel.DisplayName,
                source,
                preserveTargetNativeSurface: true);
            StartTerrainTexturePaintMode(brush);
            await ApplyTerrainTexturePaintBrushAsync(targetIndex, target);
            RefreshId65BlankLabUi();

            if (target.TextureId != source.TextureId ||
                !target.HasTextureEdit ||
                !target.TerrainVertexDeltas().SequenceEqual(raisedZ) ||
                target.HasTextureVisualEdit ||
                target.HasSurfaceBehaviorEdit ||
                !string.Equals(target.Surface, preservedSurface, StringComparison.Ordinal) ||
                !string.Equals(target.SurfaceSource, preservedSurfaceSource, StringComparison.Ordinal) ||
                target.SurfaceColor != preservedSurfaceColor ||
                !string.Equals(target.Behavior, preservedBehavior, StringComparison.Ordinal) ||
                !string.Equals(target.BehaviorSource, preservedBehaviorSource, StringComparison.Ordinal) ||
                !File.Exists(isolatedEditsPath) ||
                File.Exists(retailEditsPath) != (retailBefore != null) ||
                (retailBefore != null && !File.ReadAllBytes(retailEditsPath).SequenceEqual(retailBefore)) ||
                _customTerrainTextures.Count != 0 ||
                _nativeTerrainTextureRelocations.Count != 0 ||
                _id65BlankLabCreateCueButton?.IsEnabled == true ||
                TryValidateId65BlankLabTerrainForDisposableCue(_currentGeometry, out _))
            {
                throw new InvalidOperationException(
                    "ID65 resident paint did not preserve HP height/material/tint/collision state, use only the isolated file, or block runtime CUE export.");
            }

            GeometryCandidate reloaded = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
            int reloadedCount = TerrainEditStore.LoadStrict(isolatedEditsPath, reloaded.Polygons);
            TerrainPolygon reloadedTarget = reloaded.Polygons.Single(face =>
                string.Equals(face.RuntimeKey, target.RuntimeKey, StringComparison.OrdinalIgnoreCase));
            if (reloadedCount <= 0 ||
                reloadedTarget.TextureId != source.TextureId ||
                !reloadedTarget.TerrainVertexDeltas().SequenceEqual(raisedZ) ||
                reloadedTarget.HasTextureVisualEdit ||
                reloadedTarget.HasSurfaceBehaviorEdit)
            {
                throw new InvalidOperationException(
                    "The isolated ID65 resident texture and HP height did not reload exactly.");
            }

            _selectedTerrain = target;
            _selectedTerrainIndex = targetIndex;
            await UndoSelectedTerrainTexturePaintAsync();
            RefreshId65BlankLabUi();
            if (target.TextureId != target.OriginalTextureId ||
                target.HasTextureEdit ||
                !target.TerrainVertexDeltas().SequenceEqual(raisedZ) ||
                _id65BlankLabCreateCueButton?.IsEnabled != true)
            {
                throw new InvalidOperationException(
                    "ID65 texture-only Undo did not preserve the HP height or restore disposable-CUE eligibility.");
            }

            byte[] beforeInjectedFailure = File.ReadAllBytes(isolatedEditsPath);
            TerrainTexturePaintStageSnapshot beforeInjectedState =
                TerrainTexturePaintStageSnapshot.Capture(target);
            TerrainTexturePaintPersistenceFaultForTesting = _ =>
                throw new IOException("Injected ID65 resident texture persistence failure.");
            bool injectedApplied = await ApplySelectedTerrainResidentArtOnlyAsync(source, target);
            TerrainTexturePaintPersistenceFaultForTesting = null;
            if (injectedApplied ||
                TerrainTexturePaintStageSnapshot.Capture(target) != beforeInjectedState ||
                !File.ReadAllBytes(isolatedEditsPath).SequenceEqual(beforeInjectedFailure))
            {
                throw new InvalidOperationException(
                    "Injected ID65 resident-texture persistence failure did not restore memory and isolated bytes exactly.");
            }

            TerrainTextureSwapChoice privateChoice = new()
            {
                TextureId = source.TextureId,
                CanUseArt = true,
                RequiresPrivateCopy = true
            };
            TerrainTextureSwapChoice outOfRangeChoice = new()
            {
                TextureId = UnusedLevel65BlankLevelLabProfileRegistry.ResidentTextureCount,
                CanUseArt = true,
                RequiresPrivateCopy = false
            };
            if (await ApplySelectedTerrainResidentArtOnlyAsync(privateChoice, target) ||
                await ApplySelectedTerrainResidentArtOnlyAsync(outOfRangeChoice, target) ||
                TerrainTexturePaintStageSnapshot.Capture(target) != beforeInjectedState ||
                !File.ReadAllBytes(isolatedEditsPath).SequenceEqual(beforeInjectedFailure))
            {
                throw new InvalidOperationException(
                    "ID65 admitted a private/cross-level or out-of-range texture into its resident-only authored layer.");
            }

            string surfaceBeforeRegularUndo = target.Surface;
            string behaviorBeforeRegularUndo = target.Behavior;
            string hostileSurface = string.Equals(
                TerrainMaterialClassifier.NormalizeSurfaceName(surfaceBeforeRegularUndo),
                "water",
                StringComparison.Ordinal)
                ? "lava"
                : "water";
            Directory.CreateDirectory(Path.GetDirectoryName(materialOverridesPath) ?? _workspace.RootPath);
            File.WriteAllBytes(
                materialOverridesPath,
                JsonSerializer.SerializeToUtf8Bytes(new
                {
                    generatedAt = "2000-01-01T00:00:00",
                    editor = "ID65 resident-texture smoke hostile fixture",
                    levelKey = labKey,
                    overrides = new[]
                    {
                        new { textureId = target.TextureId, surface = hostileSurface }
                    }
                }));
            await UndoSelectedTerrainAsync();
            if (!string.Equals(target.Surface, surfaceBeforeRegularUndo, StringComparison.Ordinal) ||
                !string.Equals(target.Behavior, behaviorBeforeRegularUndo, StringComparison.Ordinal) ||
                target.IsTerrainEdited)
            {
                throw new InvalidOperationException(
                    "Regular ID65 Undo admitted a retail-root material override or failed to clear the selected edit.");
            }

            void AssertStrictAuthoredLayerRefusal(byte[] bytes, string label)
            {
                File.WriteAllBytes(isolatedEditsPath, bytes);
                GeometryCandidate strictFixture = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
                bool refused = false;
                try
                {
                    _ = TerrainEditStore.LoadStrict(isolatedEditsPath, strictFixture.Polygons);
                }
                catch (Exception ex) when (ex is InvalidDataException or JsonException)
                {
                    refused = true;
                }
                if (!refused || strictFixture.Polygons.Any(face => face.IsTerrainEdited) ||
                    !Id65TerrainEditFileBlocksNormalCreateBin(isolatedEditsPath) ||
                    !TryGetId65BlankLabNormalCreateBinBlockReason(out _))
                {
                    throw new InvalidOperationException(
                        $"The {label} ID65 authored layer was silently accepted, partially applied, or omitted by normal Create BIN.");
                }
            }

            AssertStrictAuthoredLayerRefusal("["u8.ToArray(), "malformed");

            JsonObject duplicateRoot = JsonNode.Parse(beforeInjectedFailure)?.AsObject()
                ?? throw new InvalidOperationException("Could not clone the valid ID65 authored fixture.");
            JsonArray duplicateEdits = duplicateRoot["edits"]?.AsArray()
                ?? throw new InvalidOperationException("The valid ID65 authored fixture has no edits array.");
            duplicateEdits.Add(JsonNode.Parse(duplicateEdits[0]?.ToJsonString() ?? "null"));
            duplicateRoot["editCount"] = duplicateEdits.Count;
            AssertStrictAuthoredLayerRefusal(
                JsonSerializer.SerializeToUtf8Bytes(duplicateRoot),
                "duplicate-key");

            JsonObject unknownRoot = JsonNode.Parse(beforeInjectedFailure)?.AsObject()
                ?? throw new InvalidOperationException("Could not clone the valid ID65 authored fixture.");
            JsonObject unknownEdit = unknownRoot["edits"]?.AsArray()[0]?.AsObject()
                ?? throw new InvalidOperationException("The valid ID65 authored fixture has no first edit.");
            unknownEdit["runtimeKey"] = "999999:999999:hp";
            unknownEdit["sectorIndex"] = 999999;
            unknownEdit["faceIndex"] = 999999;
            AssertStrictAuthoredLayerRefusal(
                JsonSerializer.SerializeToUtf8Bytes(unknownRoot),
                "unknown-key");

            return
                $"locked-source cache and palette exposed {UnusedLevel65BlankLevelLabProfileRegistry.ResidentTextureCount} resident slots; " +
                "one HP face painted and reloaded from the isolated authored layer while preserving height/material/tint/collision; " +
                "texture-only Undo preserved height and regular Undo ignored hostile retail overrides; persistence rollback was exact; " +
                "private/out-of-range sources, ID65 key/WAD80/unknown/noncanonical-name/noncanonical-key/mismatched-WAD retail donors, and disposable runtime export stayed blocked; the synchronous reload route refused ID65; " +
                "malformed, duplicate, and unknown authored rows stayed unconsumable";
        }
        finally
        {
            TerrainTexturePaintPersistenceFaultForTesting = null;
            StopTerrainTexturePaintMode(announce: false);
            targetPaintBefore.Restore();
            target.ApplyTerrainVertexDeltas(targetZBefore);
            target.ApplyTerrainVertexXYDeltas(targetXyBefore);
            RestoreFile(isolatedEditsPath, isolatedBefore);
            RestoreFile(retailEditsPath, retailBefore);
            RestoreFile(materialOverridesPath, materialOverridesBefore);
            RestoreFile(retailRelocationPath, retailRelocationBefore);
            RestoreFile(derivedBindingPath, derivedBindingBefore);
            if (File.Exists(overlayPath) && new FileInfo(overlayPath).Length != overlayLengthBefore)
            {
                using FileStream overlayRestore = new(overlayPath, FileMode.Open, FileAccess.Write, FileShare.None);
                overlayRestore.SetLength(overlayLengthBefore);
            }
            _loadedTerrainEdits = loadedEditsBefore;
            _savedTerrainEditSignature = savedSignatureBefore;
            _selectedTerrain = selectedBefore;
            _selectedTerrainIndex = selectedIndexBefore;
            _selectedTerrainPointIndex = selectedPointBefore;
            _activeTerrainTexturePaintBrush = activeBrushBefore;
            RefreshCurrentLevelDetails();
            RefreshId65BlankLabUi();
        }
    }

    internal async Task<string> AssertId65BlankLabStaleEditorContinuationsForTestingAsync()
    {
        if (_currentLevel == null || _currentGeometry == null || _currentMobys.Count == 0 ||
            IsCurrentId65BlankLab())
        {
            throw new InvalidOperationException(
                "The stale editor-continuation probe requires one loaded retail level.");
        }

        LevelDefinition retailLevel = _currentLevel;
        GeometryCandidate retailGeometry = _currentGeometry;
        List<Moby> retailMobys = _currentMobys;
        Moby? retailSelection = _selectedMoby;
        string retailMobySignature = BuildMobyEditSignature(retailMobys);
        string retailTerrainSignature = BuildTerrainEditSignature(retailGeometry);
        string retailSavedMobySignature = _savedMobyEditSignature;
        string retailSavedTerrainSignature = _savedTerrainEditSignature;
        Func<string, string, Task<string>>? originalUnsavedOverride =
            UnsavedTerrainDecisionOverrideForTesting;
        TaskCompletionSource? manualLoadRelease = null;
        string projectImportFixtureRoot = "";

        static bool IsEnabled(Control? control) => control?.IsEnabled == true;
        static byte[]? ReadOptionalFile(string path) =>
            File.Exists(path) ? File.ReadAllBytes(path) : null;
        static bool OptionalFileIsExact(string path, byte[]? expected) =>
            expected == null
                ? !File.Exists(path)
                : File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(expected);

        try
        {
            UnsavedTerrainDecisionOverrideForTesting = (_, _) => Task.FromResult("Discard");
            if (_discImageChooseButton == null)
                _ = BuildDiscImagePanel();

            TaskCompletionSource manualLoadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            manualLoadRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Id65BlankLabManualOperationDelayOverrideForTesting = (stage, cancellationToken) =>
            {
                if (!string.Equals(stage, "load-started", StringComparison.Ordinal))
                    return Task.CompletedTask;
                manualLoadStarted.TrySetResult();
                return manualLoadRelease.Task.WaitAsync(cancellationToken);
            };
            Task delayedLoad = LoadId65BlankLabAsync();
            await manualLoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            RefreshActionAvailability();
            RefreshId65SkyAndNameAvailability(_currentLevel);
            RefreshLevelMusicEditor(_currentLevel);
            RefreshNativeMovementActionButton();
            RefreshSelectedMobyZControls(_selectedMoby);
            if (!_id65BlankLabBusy || _id65BlankLabManualOperation == null ||
                IsEnabled(_viewport) ||
                IsEnabled(_objectManagerWorkspaceButton) ||
                IsEnabled(_levelBuildingWorkspaceButton) ||
                IsEnabled(_modernWorkspaceTabs) ||
                IsEnabled(_modernObjectWorkspaceTab) ||
                IsEnabled(_modernTerrainWorkspaceTab) ||
                IsEnabled(_modernLevelWorkspaceTab) ||
                IsEnabled(_modernEnvironmentWorkspaceTab) ||
                IsEnabled(_modernResearchWorkspaceTab) ||
                IsEnabled(_toolbarOpenDiscImageButton) ||
                IsEnabled(_discImageChooseButton) ||
                IsEnabled(_toolbarMoreControl) ||
                IsEnabled(_previousBetaProjectReminder) ||
                IsEnabled(_previousBetaProjectImportButton) ||
                IsEnabled(_updateNotificationBanner) ||
                IsEnabled(_inspectBuildSafetyButton) ||
                IsEnabled(_modernMapViewButton) ||
                IsEnabled(_modernFlyViewButton) ||
                IsEnabled(_modernViewportActionButton) ||
                IsEnabled(_toolbarCreateBinButton) ||
                IsEnabled(_objectAddButton) ||
                IsEnabled(_objectRemoveButton) ||
                IsEnabled(_objectEditButton) ||
                IsEnabled(_objectNativeMovementButton) ||
                IsEnabled(_objectSwapCatalogButton) ||
                IsEnabled(_objectCopyButton) ||
                IsEnabled(_objectPasteButton) ||
                IsEnabled(_objectUndoButton) ||
                IsEnabled(_selectedMobyZSlider) ||
                IsEnabled(_selectedMobySnapZBox) ||
                _skyboxModeBox.IsEnabled ||
                IsEnabled(_skyboxCustomImportButton) ||
                IsEnabled(_skyboxSaveButton) ||
                _levelTextTargetBox.IsEnabled ||
                IsEnabled(_levelTextSaveButton) ||
                _levelMusicTrackBox.IsEnabled ||
                IsEnabled(_levelMusicSaveButton))
            {
                throw new InvalidOperationException(
                    "A manual ID65 Load left an editing workspace, Create command, object action, or nested Level/Environment control enabled.");
            }
            string directSourceConfigPath = Path.Combine(
                _workspace.RootPath,
                "_local",
                "settings",
                "source-disc.json");
            byte[]? directSourceConfigBefore = ReadOptionalFile(directSourceConfigPath);
            Id65BlankLabAuthoredLayerSnapshot directCacheBefore =
                await CaptureId65BlankLabAuthoredLayerAsync(
                    Path.Combine(_workspace.RootPath, "editor-cache"));
            int portableGenerationBeforeDirectRefusal = _portableCacheBuildGeneration;
            int projectGenerationBeforeDirectRefusal = _projectDataImportGeneration;
            int regularPersistenceGenerationBeforeDirectRefusal =
                _regularEditorPersistenceGeneration;
            int buildSafetyGenerationBeforeDirectRefusal = _buildSafetyGeneration;
            await BuildPortableCacheAsync();
            await ImportEditorCacheAsync();
            await ChooseSourceDiscImageAsync();
            await ShowProjectDataAsync();
            bool buildSafetyDuringManual =
                await RunBuildSafetyInspectorForTestingAsync();
            if (ReleaseProjectBootstrap.Current != null &&
                await ImportPreviousBetaProjectForTestingAsync())
            {
                throw new InvalidOperationException(
                    "Direct Project Data import succeeded during a manual ID65 Load.");
            }
            RunContextualViewportAction();
            Id65BlankLabAuthoredLayerSnapshot directCacheAfter =
                await CaptureId65BlankLabAuthoredLayerAsync(
                    Path.Combine(_workspace.RootPath, "editor-cache"));
            if (_buildingPortableCache ||
                _projectDataImportBusy ||
                _regularEditorPersistenceBusy ||
                _buildSafetyBusy ||
                buildSafetyDuringManual ||
                portableGenerationBeforeDirectRefusal != _portableCacheBuildGeneration ||
                projectGenerationBeforeDirectRefusal != _projectDataImportGeneration ||
                regularPersistenceGenerationBeforeDirectRefusal !=
                    _regularEditorPersistenceGeneration ||
                buildSafetyGenerationBeforeDirectRefusal != _buildSafetyGeneration ||
                !OptionalFileIsExact(directSourceConfigPath, directSourceConfigBefore) ||
                directCacheBefore.DirectoryExisted != directCacheAfter.DirectoryExisted ||
                !directCacheBefore.Files.SequenceEqual(directCacheAfter.Files, StringComparer.Ordinal) ||
                !ReferenceEquals(_currentLevel, retailLevel) ||
                !ReferenceEquals(_currentGeometry, retailGeometry) ||
                !ReferenceEquals(_currentMobys, retailMobys) ||
                BuildMobyEditSignature(retailMobys) != retailMobySignature ||
                BuildTerrainEditSignature(retailGeometry) != retailTerrainSignature)
            {
                throw new InvalidOperationException(
                    "A direct source/cache/Project Data command mutated state during the delayed manual ID65 Load.");
            }
            CancelId65BlankLabManualOperationForWorkspaceChange();
            manualLoadRelease.TrySetResult();
            await delayedLoad;
            Id65BlankLabManualOperationDelayOverrideForTesting = null;
            if (!ReferenceEquals(_currentLevel, retailLevel) ||
                !ReferenceEquals(_currentGeometry, retailGeometry) ||
                !ReferenceEquals(_currentMobys, retailMobys) ||
                !ReferenceEquals(_selectedMoby, retailSelection) ||
                BuildMobyEditSignature(retailMobys) != retailMobySignature ||
                BuildTerrainEditSignature(retailGeometry) != retailTerrainSignature ||
                _savedMobyEditSignature != retailSavedMobySignature ||
                _savedTerrainEditSignature != retailSavedTerrainSignature)
            {
                throw new InvalidOperationException(
                    "Canceling the delayed manual ID65 Load changed the retail scene or signatures.");
            }

            string sourceConfigPath = Path.Combine(
                _workspace.RootPath,
                "_local",
                "settings",
                "source-disc.json");
            byte[]? sourceConfigBefore = ReadOptionalFile(sourceConfigPath);
            Id65BlankLabAuthoredLayerSnapshot retailCacheBeforeSourcePicker =
                await CaptureId65BlankLabAuthoredLayerAsync(
                    Path.Combine(_workspace.RootPath, "editor-cache"));
            string sourcePickerReturnPath = FirstExistingDiscImagePath(
                _discImagePathBox.Text,
                _skyboxDiscImagePathBox.Text,
                DiscImageLocator.FindImage(_workspace));
            LevelDefinition? sourceLabLevel = null;
            GeometryCandidate? sourceLabGeometry = null;
            List<Moby>? sourceLabMobys = null;
            Moby? sourceLabSelection = null;
            string sourceLabMobySignature = "";
            string sourceLabTerrainSignature = "";
            string sourceLabSavedMobySignature = "";
            string sourceLabSavedTerrainSignature = "";
            string sourceLabDiscPath = "";
            string sourceLabSkyDiscPath = "";
            string sourceLabStatus = "";
            Spyro1SkyBlockReport? sourceLabSkyReport = null;
            string sourceLabSkyReportPath = "";
            long sourceLabPrivateTextureGeneration = 0;
            Dictionary<string, TerrainTexturePreviewBundleCacheEntry>? sourceLabPreviewCache = null;
            SourceDiscFilePickerOverrideForTesting = async () =>
            {
                await LoadId65BlankLabAsync();
                if (!IsCurrentId65BlankLab() || _currentLevel == null || _currentGeometry == null)
                    throw new InvalidOperationException("The stale source-picker fixture could not load ID65.");
                sourceLabLevel = _currentLevel;
                sourceLabGeometry = _currentGeometry;
                sourceLabMobys = _currentMobys;
                sourceLabSelection = _selectedMoby;
                sourceLabMobySignature = BuildMobyEditSignature(_currentMobys);
                sourceLabTerrainSignature = BuildTerrainEditSignature(_currentGeometry);
                sourceLabSavedMobySignature = _savedMobyEditSignature;
                sourceLabSavedTerrainSignature = _savedTerrainEditSignature;
                sourceLabDiscPath = _discImagePathBox.Text ?? "";
                sourceLabSkyDiscPath = _skyboxDiscImagePathBox.Text ?? "";
                sourceLabStatus = _statusText.Text ?? "";
                sourceLabSkyReport = _nativeSkyReport;
                sourceLabSkyReportPath = _nativeSkyReportSourcePath;
                sourceLabPrivateTextureGeneration = _privateTexturePreflightOperationGeneration;
                sourceLabPreviewCache = new Dictionary<string, TerrainTexturePreviewBundleCacheEntry>(
                    _terrainTexturePreviewBundleCache,
                    StringComparer.OrdinalIgnoreCase);
                return sourcePickerReturnPath;
            };
            await ChooseSourceDiscImageAsync();
            SourceDiscFilePickerOverrideForTesting = null;
            Id65BlankLabAuthoredLayerSnapshot retailCacheAfterSourcePicker =
                await CaptureId65BlankLabAuthoredLayerAsync(
                    Path.Combine(_workspace.RootPath, "editor-cache"));
            bool previewCacheExact = sourceLabPreviewCache != null &&
                sourceLabPreviewCache.Count == _terrainTexturePreviewBundleCache.Count &&
                sourceLabPreviewCache.All(entry =>
                    _terrainTexturePreviewBundleCache.TryGetValue(
                        entry.Key,
                        out TerrainTexturePreviewBundleCacheEntry? current) &&
                    ReferenceEquals(current, entry.Value));
            if (sourceLabLevel == null || sourceLabGeometry == null || sourceLabMobys == null ||
                !ReferenceEquals(_currentLevel, sourceLabLevel) ||
                !ReferenceEquals(_currentGeometry, sourceLabGeometry) ||
                !ReferenceEquals(_currentMobys, sourceLabMobys) ||
                !ReferenceEquals(_selectedMoby, sourceLabSelection) ||
                BuildMobyEditSignature(sourceLabMobys) != sourceLabMobySignature ||
                BuildTerrainEditSignature(sourceLabGeometry) != sourceLabTerrainSignature ||
                _savedMobyEditSignature != sourceLabSavedMobySignature ||
                _savedTerrainEditSignature != sourceLabSavedTerrainSignature ||
                !string.Equals(_discImagePathBox.Text ?? "", sourceLabDiscPath, StringComparison.Ordinal) ||
                !string.Equals(_skyboxDiscImagePathBox.Text ?? "", sourceLabSkyDiscPath, StringComparison.Ordinal) ||
                !string.Equals(_statusText.Text, sourceLabStatus, StringComparison.Ordinal) ||
                !ReferenceEquals(_nativeSkyReport, sourceLabSkyReport) ||
                !string.Equals(_nativeSkyReportSourcePath, sourceLabSkyReportPath, StringComparison.Ordinal) ||
                _privateTexturePreflightOperationGeneration != sourceLabPrivateTextureGeneration ||
                !previewCacheExact ||
                !OptionalFileIsExact(sourceConfigPath, sourceConfigBefore) ||
                retailCacheBeforeSourcePicker.DirectoryExisted != retailCacheAfterSourcePicker.DirectoryExisted ||
                !retailCacheBeforeSourcePicker.Files.SequenceEqual(
                    retailCacheAfterSourcePicker.Files,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "A stale retail source picker changed source settings, caches, ID65 scene, controls, selection, or signatures.");
            }

            await SelectLevelAsync(retailLevel);
            if (!ReferenceEquals(_currentLevel, retailLevel))
                throw new InvalidOperationException("The stale Project Data fixture could not reload its retail scene.");

            var projectContext = ReleaseProjectBootstrap.Current ?? new ReleaseProjectContext(
                InstallRoot: _workspace.RootPath,
                UserData: new EditorUserDataLayout(
                    Path.Combine(_workspace.RootPath, "_local", "ui-smoke-user-data")),
                Project: new EditorProjectLayout(_workspace.RootPath, "ui-smoke-project"),
                AppVersion: "ui-smoke",
                AutomaticMigration: null);
            string projectCanaryName =
                $"stale-id65-{Guid.NewGuid():N}-terrain-edits.json";
            string projectCanaryTarget = Path.Combine(
                projectContext.Project.RootPath,
                projectCanaryName);
            if (File.Exists(projectCanaryTarget))
                throw new InvalidOperationException("The stale Project Data fixture target already exists.");
            projectImportFixtureRoot = Path.Combine(
                Path.GetTempPath(),
                $"spyro-editor-stale-project-{Guid.NewGuid():N}");
            string projectFixtureSupport = Path.Combine(projectImportFixtureRoot, "support");
            Directory.CreateDirectory(projectFixtureSupport);
            File.Copy(
                Path.Combine(_workspace.RootPath, "spyro-level-catalog.json"),
                Path.Combine(projectFixtureSupport, "spyro-level-catalog.json"));
            File.Copy(
                Path.Combine(_workspace.RootPath, "spyro-object-templates.json"),
                Path.Combine(projectFixtureSupport, "spyro-object-templates.json"));
            File.WriteAllText(Path.Combine(projectImportFixtureRoot, "Launch Spyro Editor.command"), "");
            File.WriteAllText(Path.Combine(projectImportFixtureRoot, projectCanaryName), "{}\n");
            LevelDefinition? projectLabLevel = null;
            GeometryCandidate? projectLabGeometry = null;
            List<Moby>? projectLabMobys = null;
            Moby? projectLabSelection = null;
            string projectLabMobySignature = "";
            string projectLabTerrainSignature = "";
            string projectLabSavedMobySignature = "";
            string projectLabSavedTerrainSignature = "";
            string projectLabDiscPath = "";
            string projectLabSkyDiscPath = "";
            string projectLabStatus = "";
            ProjectDataFolderPickerOverrideForTesting = async () =>
            {
                await LoadId65BlankLabAsync();
                if (!IsCurrentId65BlankLab() || _currentLevel == null || _currentGeometry == null)
                    throw new InvalidOperationException("The stale Project Data fixture could not load ID65.");
                projectLabLevel = _currentLevel;
                projectLabGeometry = _currentGeometry;
                projectLabMobys = _currentMobys;
                projectLabSelection = _selectedMoby;
                projectLabMobySignature = BuildMobyEditSignature(_currentMobys);
                projectLabTerrainSignature = BuildTerrainEditSignature(_currentGeometry);
                projectLabSavedMobySignature = _savedMobyEditSignature;
                projectLabSavedTerrainSignature = _savedTerrainEditSignature;
                projectLabDiscPath = _discImagePathBox.Text ?? "";
                projectLabSkyDiscPath = _skyboxDiscImagePathBox.Text ?? "";
                projectLabStatus = _statusText.Text ?? "";
                return projectImportFixtureRoot;
            };
            bool projectImportStarted = false;
            ProjectDataImportStartedForTesting = () => projectImportStarted = true;
            bool staleProjectImported = await ImportPreviousBetaProjectForTestingAsync();
            ProjectDataImportStartedForTesting = null;
            ProjectDataFolderPickerOverrideForTesting = null;
            if (staleProjectImported ||
                projectImportStarted ||
                File.Exists(projectCanaryTarget) ||
                _projectDataImportBusy ||
                projectLabLevel == null || projectLabGeometry == null || projectLabMobys == null ||
                !ReferenceEquals(_currentLevel, projectLabLevel) ||
                !ReferenceEquals(_currentGeometry, projectLabGeometry) ||
                !ReferenceEquals(_currentMobys, projectLabMobys) ||
                !ReferenceEquals(_selectedMoby, projectLabSelection) ||
                BuildMobyEditSignature(projectLabMobys) != projectLabMobySignature ||
                BuildTerrainEditSignature(projectLabGeometry) != projectLabTerrainSignature ||
                _savedMobyEditSignature != projectLabSavedMobySignature ||
                _savedTerrainEditSignature != projectLabSavedTerrainSignature ||
                !string.Equals(_discImagePathBox.Text ?? "", projectLabDiscPath, StringComparison.Ordinal) ||
                !string.Equals(_skyboxDiscImagePathBox.Text ?? "", projectLabSkyDiscPath, StringComparison.Ordinal) ||
                !string.Equals(_statusText.Text, projectLabStatus, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A stale Project Data folder picker imported files or changed the ID65 scene, controls, selection, or signatures.");
            }
            Directory.Delete(projectImportFixtureRoot, recursive: true);
            projectImportFixtureRoot = "";

            await SelectLevelAsync(retailLevel);
            if (!ReferenceEquals(_currentLevel, retailLevel))
                throw new InvalidOperationException("The stale object fixture could not reload its retail scene.");

            Moby retailTarget = retailMobys.First(candidate =>
                !candidate.IsEditorControl &&
                !candidate.IsRemoved &&
                !candidate.IsFlyInLandingControl &&
                !IsMappedPortalControlMoby(candidate));
            string retailTargetSignature = BuildMobyEditSignature([retailTarget]);
            LevelDefinition? objectLabLevel = null;
            GeometryCandidate? objectLabGeometry = null;
            List<Moby>? objectLabMobys = null;
            Moby? objectLabSelection = null;
            string objectLabMobySignature = "";
            string objectLabTerrainSignature = "";
            string objectLabSavedMobySignature = "";
            string objectLabSavedTerrainSignature = "";
            string objectLabStatus = "";
            MobyEditDialogOverrideForTesting = async (target, nameBox) =>
            {
                if (!ReferenceEquals(target, retailTarget))
                    throw new InvalidOperationException("The stale object fixture received the wrong retail target.");
                nameBox.Text = "stale dialog must not commit";
                await LoadId65BlankLabAsync();
                if (!IsCurrentId65BlankLab() || _currentLevel == null || _currentGeometry == null)
                    throw new InvalidOperationException("The stale object fixture could not load ID65.");
                objectLabLevel = _currentLevel;
                objectLabGeometry = _currentGeometry;
                objectLabMobys = _currentMobys;
                objectLabSelection = _selectedMoby;
                objectLabMobySignature = BuildMobyEditSignature(_currentMobys);
                objectLabTerrainSignature = BuildTerrainEditSignature(_currentGeometry);
                objectLabSavedMobySignature = _savedMobyEditSignature;
                objectLabSavedTerrainSignature = _savedTerrainEditSignature;
                objectLabStatus = _statusText.Text ?? "";
                return true;
            };
            await EditMobyAsync(retailTarget);
            MobyEditDialogOverrideForTesting = null;
            if (objectLabLevel == null || objectLabGeometry == null || objectLabMobys == null ||
                !ReferenceEquals(_currentLevel, objectLabLevel) ||
                !ReferenceEquals(_currentGeometry, objectLabGeometry) ||
                !ReferenceEquals(_currentMobys, objectLabMobys) ||
                !ReferenceEquals(_selectedMoby, objectLabSelection) ||
                BuildMobyEditSignature(objectLabMobys) != objectLabMobySignature ||
                BuildTerrainEditSignature(objectLabGeometry) != objectLabTerrainSignature ||
                _savedMobyEditSignature != objectLabSavedMobySignature ||
                _savedTerrainEditSignature != objectLabSavedTerrainSignature ||
                !string.Equals(_statusText.Text, objectLabStatus, StringComparison.Ordinal) ||
                BuildMobyEditSignature([retailTarget]) != retailTargetSignature)
            {
                throw new InvalidOperationException(
                    "An accepted stale retail object dialog mutated its detached target, ID65 scene, selection, or signatures.");
            }

            await SelectLevelAsync(retailLevel);
            if (!ReferenceEquals(_currentLevel, retailLevel))
                throw new InvalidOperationException("The stale sky fixture could not reload its retail scene.");

            LevelDefinition labLevel = _catalog.FindByKey(UnusedLevel65BlankLevelLabProfileRegistry.Key)
                ?? throw new InvalidOperationException("The stale sky fixture requires the admitted ID65 row.");
            string id65SkyDirectory = Path.Combine(
                _workspace.RootPath,
                "custom-skyboxes",
                LevelCatalog.NormalizeKey(labLevel.Key));
            Id65BlankLabAuthoredLayerSnapshot id65SkyBefore =
                await CaptureId65BlankLabAuthoredLayerAsync(id65SkyDirectory);
            LevelDefinition? skyLabLevel = null;
            GeometryCandidate? skyLabGeometry = null;
            List<Moby>? skyLabMobys = null;
            Moby? skyLabSelection = null;
            string skyLabMobySignature = "";
            string skyLabTerrainSignature = "";
            string skyLabSavedMobySignature = "";
            string skyLabSavedTerrainSignature = "";
            object? skyLabMode = null;
            string skyLabImportPath = "";
            NativeEnvironmentGradeMatch? skyLabEnvironment = null;
            string skyLabStatus = "";
            CustomSkyFilePickerOverrideForTesting = async () =>
            {
                await LoadId65BlankLabAsync();
                if (!IsCurrentId65BlankLab() || _currentLevel == null || _currentGeometry == null)
                    throw new InvalidOperationException("The stale sky fixture could not load ID65.");
                skyLabLevel = _currentLevel;
                skyLabGeometry = _currentGeometry;
                skyLabMobys = _currentMobys;
                skyLabSelection = _selectedMoby;
                skyLabMobySignature = BuildMobyEditSignature(_currentMobys);
                skyLabTerrainSignature = BuildTerrainEditSignature(_currentGeometry);
                skyLabSavedMobySignature = _savedMobyEditSignature;
                skyLabSavedTerrainSignature = _savedTerrainEditSignature;
                skyLabMode = _skyboxModeBox.SelectedItem;
                skyLabImportPath = _skyboxImportPathBox.Text ?? "";
                skyLabEnvironment = _activeEnvironmentGradeMatch;
                skyLabStatus = _statusText.Text ?? "";
                return Path.Combine(_workspace.RootPath, "injected-stale-id65.sky");
            };
            await ChooseCustomSkyAsync();
            CustomSkyFilePickerOverrideForTesting = null;
            Id65BlankLabAuthoredLayerSnapshot id65SkyAfter =
                await CaptureId65BlankLabAuthoredLayerAsync(id65SkyDirectory);
            if (skyLabLevel == null || skyLabGeometry == null || skyLabMobys == null ||
                !ReferenceEquals(_currentLevel, skyLabLevel) ||
                !ReferenceEquals(_currentGeometry, skyLabGeometry) ||
                !ReferenceEquals(_currentMobys, skyLabMobys) ||
                !ReferenceEquals(_selectedMoby, skyLabSelection) ||
                BuildMobyEditSignature(skyLabMobys) != skyLabMobySignature ||
                BuildTerrainEditSignature(skyLabGeometry) != skyLabTerrainSignature ||
                _savedMobyEditSignature != skyLabSavedMobySignature ||
                _savedTerrainEditSignature != skyLabSavedTerrainSignature ||
                !ReferenceEquals(_skyboxModeBox.SelectedItem, skyLabMode) ||
                !string.Equals(_skyboxImportPathBox.Text ?? "", skyLabImportPath, StringComparison.Ordinal) ||
                !ReferenceEquals(_activeEnvironmentGradeMatch, skyLabEnvironment) ||
                !string.Equals(_statusText.Text, skyLabStatus, StringComparison.Ordinal) ||
                id65SkyBefore.DirectoryExisted != id65SkyAfter.DirectoryExisted ||
                !id65SkyBefore.Files.SequenceEqual(id65SkyAfter.Files, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "A stale retail sky picker wrote an ID65 artifact or changed its scene, controls, selection, or signatures.");
            }

            return
                "manual Load disabled the full editor/source/cache/More command surface; direct source, cache, Project Data, and viewport commands refused; canceled Load preserved retail state; stale source-disc and previous-project pickers stayed settings/cache/file write-free; accepted stale object and sky-picker continuations could not mutate detached retail data, ID65 files, scene, selection, controls, or signatures";
        }
        finally
        {
            manualLoadRelease?.TrySetResult();
            Id65BlankLabManualOperationDelayOverrideForTesting = null;
            MobyEditDialogOverrideForTesting = null;
            CustomSkyFilePickerOverrideForTesting = null;
            SourceDiscFilePickerOverrideForTesting = null;
            EditorCacheFolderPickerOverrideForTesting = null;
            ProjectDataFolderPickerOverrideForTesting = null;
            ProjectDataImportStartedForTesting = null;
            UnsavedTerrainDecisionOverrideForTesting = originalUnsavedOverride;
            if (_id65BlankLabManualOperation != null || _id65BlankLabBusy)
                CancelId65BlankLabManualOperationForWorkspaceChange();
            if (!ReferenceEquals(_currentLevel, retailLevel))
                await SelectLevelAsync(retailLevel);
            if (!string.IsNullOrWhiteSpace(projectImportFixtureRoot) &&
                Directory.Exists(projectImportFixtureRoot))
            {
                Directory.Delete(projectImportFixtureRoot, recursive: true);
            }
            RefreshActionAvailability();
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
                    "The guarded ID65 transition did not persist retail terrain and Moby edits before loading the Lab. " +
                    $"current={_currentLevel?.Key ?? "<none>"}; terrainSaved={TerrainEditFileHasEdits(terrainEditsPath)}; " +
                    $"mobySaved={NativeEditFileHasEdits(mobyEditsPath)}; status={_statusText.Text}");
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

            _selectedTerrain = labTerrain;
            _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(labTerrain);
            _selectedTerrainPointIndex = 0;
            byte[] automaticPersistFileBefore = File.ReadAllBytes(labAuthoredTerrainPath);
            string automaticPersistTerrainSignatureBefore = BuildTerrainEditSignature(_currentGeometry);
            string automaticPersistSavedSignatureBefore = _savedTerrainEditSignature;
            float[] automaticPersistHeightBefore = labTerrain.TerrainVertexDeltas().ToArray();

            void AssertAutomaticHeightUndoRolledBack(string fixture)
            {
                bool exact =
                    labTerrain.TerrainVertexDeltas().SequenceEqual(automaticPersistHeightBefore) &&
                    BuildTerrainEditSignature(_currentGeometry) == automaticPersistTerrainSignatureBefore &&
                    _savedTerrainEditSignature == automaticPersistSavedSignatureBefore &&
                    File.Exists(labAuthoredTerrainPath) &&
                    File.ReadAllBytes(labAuthoredTerrainPath).SequenceEqual(automaticPersistFileBefore) &&
                    (_statusText.Text ?? "").Contains(
                        "Could not undo terrain height",
                        StringComparison.OrdinalIgnoreCase);
                if (!exact)
                {
                    throw new InvalidOperationException(
                        $"The {fixture} automatic ID65 persistence failure changed the face, signature, or authored file. Status: {_statusText.Text}");
                }
            }

            Id65BlankLabTerrainPersistenceFaultForTesting = _ =>
                throw new IOException("Injected staged automatic ID65 persistence failure.");
            try
            {
                await UndoSelectedTerrainHeightAsync();
            }
            finally
            {
                Id65BlankLabTerrainPersistenceFaultForTesting = null;
            }
            AssertAutomaticHeightUndoRolledBack("injected staged-write");

            UnusedLevel65BlankLevelLabWorkspacePaths automaticPersistPaths = _id65BlankLabPaths ??
                throw new InvalidOperationException("The ID65 paths disappeared before automatic persistence testing.");
            string automaticPersistLockedImagePath = automaticPersistPaths.LockedBaseImagePath;
            long automaticPersistTamperOffset;
            byte automaticPersistLockedByteBefore;
            using (FileStream lockedStream = new(
                       automaticPersistLockedImagePath,
                       FileMode.Open,
                       FileAccess.ReadWrite,
                       FileShare.Read))
            {
                automaticPersistTamperOffset = Math.Min(8192, lockedStream.Length - 1);
                lockedStream.Position = automaticPersistTamperOffset;
                int originalByte = lockedStream.ReadByte();
                if (originalByte < 0)
                    throw new InvalidDataException("The ID65 locked BIN is empty.");
                automaticPersistLockedByteBefore = (byte)originalByte;
                lockedStream.Position = automaticPersistTamperOffset;
                lockedStream.WriteByte((byte)(automaticPersistLockedByteBefore ^ 0x3C));
                lockedStream.Flush(flushToDisk: true);
            }
            try
            {
                await UndoSelectedTerrainHeightAsync();
            }
            finally
            {
                using FileStream lockedStream = new(
                    automaticPersistLockedImagePath,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.Read);
                lockedStream.Position = automaticPersistTamperOffset;
                lockedStream.WriteByte(automaticPersistLockedByteBefore);
                lockedStream.Flush(flushToDisk: true);
            }
            AssertAutomaticHeightUndoRolledBack("locked-BIN tamper");

            string automaticPersistManifestPath = automaticPersistPaths.ManifestPath;
            byte[] automaticPersistManifestBefore = File.ReadAllBytes(automaticPersistManifestPath);
            JsonObject automaticPersistTamperedManifest = JsonNode.Parse(
                    File.ReadAllText(automaticPersistManifestPath))?.AsObject()
                ?? throw new InvalidDataException("The ID65 published manifest fixture is empty.");
            string automaticPersistProfileVersionProperty = automaticPersistTamperedManifest
                .Select(property => property.Key)
                .FirstOrDefault(key => string.Equals(
                    key,
                    "profileVersion",
                    StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("The ID65 published manifest has no profileVersion field.");
            automaticPersistTamperedManifest[automaticPersistProfileVersionProperty] = -1;
            File.WriteAllText(
                automaticPersistManifestPath,
                automaticPersistTamperedManifest.ToJsonString());
            try
            {
                await UndoSelectedTerrainHeightAsync();
            }
            finally
            {
                File.WriteAllBytes(
                    automaticPersistManifestPath,
                    automaticPersistManifestBefore);
            }
            AssertAutomaticHeightUndoRolledBack("published-manifest tamper");

            string restoredRuntimeKey = labTerrain.RuntimeKey;
            byte[] authoredBeforeFailedRestore = File.ReadAllBytes(labAuthoredTerrainPath);
            LevelDefinition levelBeforeFailedRestore = _currentLevel!;
            GeometryCandidate geometryBeforeFailedRestore = _currentGeometry;
            List<Moby> mobysBeforeFailedRestore = _currentMobys;
            Moby? selectedMobyBeforeFailedRestore = _selectedMoby;
            TerrainPolygon? selectedTerrainBeforeFailedRestore = _selectedTerrain;
            int selectedTerrainIndexBeforeFailedRestore = _selectedTerrainIndex;
            int selectedTerrainPointIndexBeforeFailedRestore = _selectedTerrainPointIndex;
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

            void AssertFailedRestoreRemainedExact(string fixture, bool result)
            {
                bool fileExact = File.Exists(labAuthoredTerrainPath) &&
                    File.ReadAllBytes(labAuthoredTerrainPath).SequenceEqual(authoredBeforeFailedRestore);
                bool sceneExact =
                    ReferenceEquals(_currentLevel, levelBeforeFailedRestore) &&
                    ReferenceEquals(_currentGeometry, geometryBeforeFailedRestore) &&
                    ReferenceEquals(_currentMobys, mobysBeforeFailedRestore) &&
                    ReferenceEquals(_selectedMoby, selectedMobyBeforeFailedRestore) &&
                    ReferenceEquals(_selectedTerrain, selectedTerrainBeforeFailedRestore) &&
                    _selectedTerrainIndex == selectedTerrainIndexBeforeFailedRestore &&
                    _selectedTerrainPointIndex == selectedTerrainPointIndexBeforeFailedRestore;
                bool signaturesExact =
                    BuildTerrainEditSignature(_currentGeometry) == terrainSignatureBeforeFailedRestore &&
                    _savedTerrainEditSignature == savedTerrainSignatureBeforeFailedRestore &&
                    BuildMobyEditSignature(_currentMobys) == mobySignatureBeforeFailedRestore &&
                    _savedMobyEditSignature == savedMobySignatureBeforeFailedRestore;
                bool statusTruthful = (_statusText.Text ?? "")
                    .Contains("refused", StringComparison.OrdinalIgnoreCase);
                if (result || !fileExact || !sceneExact || !signaturesExact || !statusTruthful)
                {
                    throw new InvalidOperationException(
                        $"The {fixture} ID65 Restore fixture was not atomic. " +
                        $"result={result}; file={fileExact}; scene={sceneExact}; signatures={signaturesExact}; " +
                        $"status={statusTruthful} ({_statusText.Text})");
                }
            }

            async Task AssertSnapshotReadFailureReleasedOperationAsync(
                string fixture,
                Exception injectedFailure)
            {
                Id65BlankLabRestoreSnapshotReadFaultForTesting = _ => throw injectedFailure;
                bool result;
                try
                {
                    result = await RestoreId65BlankLabTerrainAsync("terrain");
                }
                finally
                {
                    Id65BlankLabRestoreSnapshotReadFaultForTesting = null;
                }

                AssertFailedRestoreRemainedExact(fixture, result);
                Id65BlankLabUiSnapshot availableUi = CaptureId65BlankLabUiSnapshotForTesting();
                if (_id65BlankLabBusy ||
                    _id65BlankLabManualOperation != null ||
                    _loadingLevel ||
                    !_levelJumpBox.IsEnabled ||
                    !_levelList.IsEnabled ||
                    !_viewport.IsEnabled ||
                    !availableUi.LoadEnabled ||
                    !availableUi.SaveEnabled)
                {
                    throw new InvalidOperationException(
                        $"The {fixture} ID65 Restore fixture leaked its operation fence or left the editor unavailable. " +
                        $"busy={_id65BlankLabBusy}; operation={_id65BlankLabManualOperation != null}; " +
                        $"loading={_loadingLevel}; jump={_levelJumpBox.IsEnabled}; list={_levelList.IsEnabled}; " +
                        $"viewport={_viewport.IsEnabled}; load={availableUi.LoadEnabled}; save={availableUi.SaveEnabled}.");
                }
            }

            await AssertSnapshotReadFailureReleasedOperationAsync(
                "authored snapshot-read I/O failure",
                new IOException("Injected authored snapshot-read failure."));
            await AssertSnapshotReadFailureReleasedOperationAsync(
                "authored snapshot-read permission failure",
                new UnauthorizedAccessException("Injected authored snapshot-read permission failure."));

            Id65BlankLabRestorePostApplyFaultForTesting = () =>
                throw new InvalidOperationException("Injected post-apply ID65 restore failure.");
            bool postApplyFailureResult;
            try
            {
                postApplyFailureResult = await RestoreId65BlankLabTerrainAsync("terrain");
            }
            finally
            {
                Id65BlankLabRestorePostApplyFaultForTesting = null;
            }
            AssertFailedRestoreRemainedExact("post-apply failure", postApplyFailureResult);

            Id65BlankLabRestoreDeleteFaultForTesting = _ =>
                throw new IOException("Injected authored-file delete failure.");
            bool deleteFailureResult;
            try
            {
                deleteFailureResult = await RestoreId65BlankLabTerrainAsync("terrain");
            }
            finally
            {
                Id65BlankLabRestoreDeleteFaultForTesting = null;
            }
            AssertFailedRestoreRemainedExact("authored-file delete failure", deleteFailureResult);

            Id65BlankLabRestoreDeleteFaultForTesting = _ =>
                throw new UnauthorizedAccessException("Injected authored-file delete permission failure.");
            bool unauthorizedDeleteFailureResult;
            try
            {
                unauthorizedDeleteFailureResult = await RestoreId65BlankLabTerrainAsync("terrain");
            }
            finally
            {
                Id65BlankLabRestoreDeleteFaultForTesting = null;
            }
            AssertFailedRestoreRemainedExact(
                "authored-file unauthorized delete failure",
                unauthorizedDeleteFailureResult);

            UnusedLevel65BlankLevelLabWorkspacePaths activeLabPaths = _id65BlankLabPaths ??
                throw new InvalidOperationException("The ID65 locked paths disappeared during Restore testing.");
            string lockedImagePath = activeLabPaths.LockedBaseImagePath;
            long lockedTamperOffset;
            byte lockedByteBefore;
            using (FileStream lockedStream = new(
                       lockedImagePath,
                       FileMode.Open,
                       FileAccess.ReadWrite,
                       FileShare.Read))
            {
                lockedTamperOffset = Math.Min(4096, lockedStream.Length - 1);
                lockedStream.Position = lockedTamperOffset;
                int originalByte = lockedStream.ReadByte();
                if (originalByte < 0)
                    throw new InvalidDataException("The ID65 locked BIN is empty.");
                lockedByteBefore = (byte)originalByte;
                lockedStream.Position = lockedTamperOffset;
                lockedStream.WriteByte((byte)(lockedByteBefore ^ 0x5A));
                lockedStream.Flush(flushToDisk: true);
            }
            bool lockedTamperResult;
            try
            {
                lockedTamperResult = await RestoreId65BlankLabTerrainAsync("terrain");
            }
            finally
            {
                using FileStream lockedStream = new(
                    lockedImagePath,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.Read);
                lockedStream.Position = lockedTamperOffset;
                lockedStream.WriteByte(lockedByteBefore);
                lockedStream.Flush(flushToDisk: true);
            }
            AssertFailedRestoreRemainedExact("locked BIN tamper", lockedTamperResult);

            string publishedManifestPath = activeLabPaths.ManifestPath;
            byte[] publishedManifestBefore = File.ReadAllBytes(publishedManifestPath);
            JsonObject tamperedManifest = JsonNode.Parse(
                    File.ReadAllText(publishedManifestPath))?.AsObject()
                ?? throw new InvalidDataException("The ID65 published manifest fixture is empty.");
            string profileVersionProperty = tamperedManifest
                .Select(property => property.Key)
                .FirstOrDefault(key => string.Equals(
                    key,
                    "profileVersion",
                    StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("The ID65 published manifest has no profileVersion field.");
            tamperedManifest[profileVersionProperty] = -1;
            File.WriteAllText(publishedManifestPath, tamperedManifest.ToJsonString());
            bool manifestTamperResult;
            try
            {
                manifestTamperResult = await RestoreId65BlankLabTerrainAsync("terrain");
            }
            finally
            {
                File.WriteAllBytes(publishedManifestPath, publishedManifestBefore);
            }
            AssertFailedRestoreRemainedExact("published-manifest tamper", manifestTamperResult);

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

            // Finish on the actual locked-source geometry rather than the compact
            // transition fixture so the resident-texture probe exercises the real Lab.
            Id65BlankLabReloadOverrideForTesting = null;
            await SelectLevelAsync(lab);
            if (!IsCurrentId65BlankLab() || _currentGeometry == null || _currentMobys.Count == 0)
            {
                throw new InvalidOperationException(
                    "The guarded transition could not reload the actual source-bound ID65 Lab.");
            }

            return "Cancel preserved retail level/camera/selection/unsaved edits; Save persisted retail terrain and Mobys before the guarded ID65 transition; unsupported ID65 Save-to-retail stayed put without file/signature writes; generic Moby persistence failed closed; generic Terrain Save and supported autosaves used the isolated staged/atomic Lab route; automatic Undo Height rolled back memory/signatures/files under injected write, locked-BIN, and published-manifest failures; Restore preserved authored bytes and exact in-memory/signature state under injected reload, post-apply, delete, locked-BIN-tamper, and published-manifest-tamper failures; successful validated candidate application committed authored-layer removal and left resident objects plus retail paths untouched";
        }
        finally
        {
            UnsavedTerrainDecisionOverrideForTesting = null;
            Id65BlankLabReloadOverrideForTesting = null;
            Id65BlankLabValidationOverrideForTesting = null;
            Id65BlankLabRestoreSnapshotReadFaultForTesting = null;
            Id65BlankLabRestorePostApplyFaultForTesting = null;
            Id65BlankLabRestoreDeleteFaultForTesting = null;
            Id65BlankLabTerrainPersistenceFaultForTesting = null;
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

    internal async Task<string> AssertId65BlankLabReverseRegularPersistenceFencingForTestingAsync()
    {
        if (!IsCurrentId65BlankLab() || _currentLevel == null || _currentGeometry == null ||
            _id65BlankLabPaths == null || _id65BlankLabManifest == null)
        {
            throw new InvalidOperationException(
                "The reverse regular-persistence probe requires the validated, loaded ID65 Lab.");
        }

        LevelDefinition lab = _currentLevel;
        LevelDefinition retail = _retailCatalog.FindByKey("stonehill")
            ?? throw new InvalidOperationException(
                "The reverse regular-persistence probe requires the Stone Hill retail catalog level.");
        await SelectLevelAsync(retail);
        if (!ReferenceEquals(_currentLevel, retail) || _currentGeometry == null ||
            _currentMobys.Count == 0 || _currentNativeMobyPaths.Count == 0 ||
            _currentDragonRunToEdits.Count == 0)
        {
            throw new InvalidOperationException(
                "The reverse regular-persistence probe could not load Stone Hill Mobys, native paths, and dragon run-to controls.");
        }

        GeometryCandidate retailGeometry = _currentGeometry;
        List<Moby> retailMobys = _currentMobys;
        Moby objectTarget = retailMobys.First(candidate =>
            !candidate.IsAdded &&
            !candidate.IsRemoved &&
            !candidate.IsEditorControl &&
            candidate.TrueIndex >= 0);
        NativeMobyPath nativePathTarget = _currentNativeMobyPaths[0];
        NativePathNode nativePathNodeTarget = nativePathTarget.Nodes[0];
        NativeDragonRunToEdit dragonRunToTarget = _currentDragonRunToEdits[0];
        Vector3f objectPositionBefore = objectTarget.Position;
        (int X, int Y, int Z) nativePathPositionBefore = (
            nativePathNodeTarget.RawX,
            nativePathNodeTarget.RawY,
            nativePathNodeTarget.RawZ);
        (int X, int Y) dragonRunToPositionBefore = (
            dragonRunToTarget.RawX,
            dragonRunToTarget.RawY);
        string mobyPath = Path.Combine(
            _workspace.RootPath,
            $"{retail.Key}-native-edits.json");
        string nativeMovementPath = Path.Combine(
            _workspace.RootPath,
            NativeMobyPathEditStore.DefaultFileName(retail.Key));
        string terrainPath = Path.Combine(
            _workspace.RootPath,
            $"{retail.Key}-terrain-edits.json");
        string[] persistedPaths = [mobyPath, nativeMovementPath, terrainPath];
        Dictionary<string, byte[]?> persistedBefore = persistedPaths.ToDictionary(
            path => path,
            path => File.Exists(path) ? File.ReadAllBytes(path) : null,
            StringComparer.OrdinalIgnoreCase);
        string savedMobySignatureBefore = _savedMobyEditSignature;
        string savedTerrainSignatureBefore = _savedTerrainEditSignature;
        string savedNativeMovementSignatureBefore = _savedNativeMovementEditSignature;
        string savedDragonRunToSignatureBefore = _savedDragonRunToEditSignature;
        int loadedMobyEditsBefore = _loadedMobyEdits;
        int loadedTerrainEditsBefore = _loadedTerrainEdits;
        TerrainPolygon terrain = retailGeometry.Polygons.First(candidate =>
            !candidate.IsTerrainRemoved &&
            !candidate.IsTerrainAddClone &&
            string.Equals(candidate.Detail, "hp", StringComparison.OrdinalIgnoreCase));
        float[] terrainDeltasBefore = terrain.TerrainVertexDeltas().ToArray();

        static bool FileStateIsExact(string path, byte[]? expected) =>
            expected == null
                ? !File.Exists(path)
                : File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(expected);
        static bool IsEnabled(Control? control) => control?.IsEnabled == true;

        static bool ManifestContainsRow(
            string path,
            Func<JsonElement, bool> predicate)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
            return document.RootElement.TryGetProperty("edits", out JsonElement edits) &&
                edits.ValueKind == JsonValueKind.Array &&
                edits.EnumerateArray().Any(predicate);
        }

        bool updateSnapshotStarted = false;
        UpdateSnapshotStartedForTesting = () => updateSnapshotStarted = true;

        void AssertReloadedObjectLayers(
            Vector3f expectedMobyPosition,
            (int X, int Y, int Z) expectedNativePathPosition,
            (int X, int Y) expectedDragonRunToPosition,
            string label)
        {
            LevelLoadData reloaded = LoadLevelData(retail.Key);
            Moby reloadedMoby = reloaded.Mobys.Single(candidate =>
                candidate.TrueIndex == objectTarget.TrueIndex);
            NativeMobyPath reloadedPath = reloaded.NativeMovement.Paths.Single(candidate =>
                candidate.OwnerTrueIndex == nativePathTarget.OwnerTrueIndex);
            NativePathNode reloadedNode = reloadedPath.Nodes.Single(candidate =>
                candidate.Index == nativePathNodeTarget.Index);
            NativeDragonRunToEdit reloadedDragon =
                reloaded.NativeMovement.DragonRunToEdits.Single(candidate =>
                    candidate.OwnerTrueIndex == dragonRunToTarget.OwnerTrueIndex);
            if (reloadedMoby.Position != expectedMobyPosition ||
                reloadedNode.RawX != expectedNativePathPosition.X ||
                reloadedNode.RawY != expectedNativePathPosition.Y ||
                reloadedNode.RawZ != expectedNativePathPosition.Z ||
                reloadedDragon.RawX != expectedDragonRunToPosition.X ||
                reloadedDragon.RawY != expectedDragonRunToPosition.Y)
            {
                throw new InvalidOperationException(
                    $"{label} did not reload the exact Moby, native path, and dragon run-to edits.");
            }
        }

        async Task AssertRegularPersistenceBlocksLabAsync(
            string stage,
            string label,
            Func<Task<bool>> start)
        {
            TaskCompletionSource started =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource release =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            Dictionary<string, byte[]?> beforeDelay = persistedPaths.ToDictionary(
                path => path,
                path => File.Exists(path) ? File.ReadAllBytes(path) : null,
                StringComparer.OrdinalIgnoreCase);
            int id65GenerationBefore = _id65BlankLabManualOperationGeneration;
            RegularEditorPersistenceDelayOverrideForTesting = observedStage =>
            {
                if (!string.Equals(observedStage, stage, StringComparison.Ordinal))
                    return Task.CompletedTask;
                started.TrySetResult();
                return release.Task;
            };

            Task<bool>? pending = null;
            try
            {
                pending = start();
                await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
                RefreshActionAvailability();
                Id65BlankLabUiSnapshot labUi = CaptureId65BlankLabUiSnapshotForTesting();
                if (!_regularEditorPersistenceBusy || _buildSafetyBusy ||
                    _regularEditorPersistenceOperation == null ||
                    _levelJumpBox.IsEnabled || _levelList.IsEnabled ||
                    (_openWorkspaceButton != null && _openWorkspaceButton.IsEnabled) ||
                    _viewport.IsEnabled ||
                    (_inspectBuildSafetyButton != null && _inspectBuildSafetyButton.IsEnabled) ||
                    IsEnabled(_updateNotificationBanner) ||
                    labUi.BuildEnabled || labUi.LoadEnabled || labUi.SaveEnabled ||
                    labUi.CreateCueEnabled)
                {
                    throw new InvalidOperationException(
                        $"Delayed {label} did not disable level/workspace/editor and ID65 Lab admission controls.");
                }
                if (beforeDelay.Any(pair => !FileStateIsExact(pair.Key, pair.Value)))
                {
                    throw new InvalidOperationException(
                        $"Delayed {label} wrote a retail edit file before its guarded persistence continuation was released.");
                }
                if (TryBeginUpdateSnapshotForTesting() || updateSnapshotStarted)
                {
                    throw new InvalidOperationException(
                        $"Delayed {label} admitted an update safety snapshot action.");
                }

                await LoadId65BlankLabAsync();
                if (!ReferenceEquals(_currentLevel, retail) ||
                    !ReferenceEquals(_currentGeometry, retailGeometry) ||
                    !ReferenceEquals(_currentMobys, retailMobys) ||
                    _id65BlankLabBusy || _id65BlankLabManualOperation != null ||
                    id65GenerationBefore != _id65BlankLabManualOperationGeneration ||
                    beforeDelay.Any(pair => !FileStateIsExact(pair.Key, pair.Value)))
                {
                    throw new InvalidOperationException(
                        $"ID65 Lab admission overlapped or changed delayed {label} state.");
                }

                release.TrySetResult();
                bool saved = await pending;
                if (!saved || _regularEditorPersistenceBusy || _buildSafetyBusy ||
                    !ReferenceEquals(_currentLevel, retail) ||
                    !ReferenceEquals(_currentGeometry, retailGeometry) ||
                    !ReferenceEquals(_currentMobys, retailMobys) ||
                    !string.Equals(
                        _savedMobyEditSignature,
                        BuildMobyEditSignature(retailMobys),
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        _savedTerrainEditSignature,
                        BuildTerrainEditSignature(retailGeometry),
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        _savedNativeMovementEditSignature,
                        BuildNativeMovementEditSignature(),
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        _savedDragonRunToEditSignature,
                        BuildDragonRunToEditSignature(),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Delayed {label} did not complete from its exact captured retail snapshot.");
                }
            }
            finally
            {
                release.TrySetResult();
                if (pending != null && !pending.IsCompleted)
                {
                    try
                    {
                        await pending;
                    }
                    catch
                    {
                        // Preserve the fixture's primary assertion failure.
                    }
                }
                RegularEditorPersistenceDelayOverrideForTesting = null;
            }
        }

        try
        {
            await AssertRegularPersistenceBlocksLabAsync(
                "moby-save-started",
                "full Save",
                () => SaveCurrentEditsAsync());
            await AssertRegularPersistenceBlocksLabAsync(
                "moby-save-started",
                "immediate object persistence",
                async () =>
                {
                    _ = await PersistCurrentMobyEditsAsync();
                    return true;
                });

            nativePathNodeTarget.TranslateRaw(1, 2, 0);
            dragonRunToTarget.TranslateRaw(1, -1);
            (int X, int Y, int Z) persistedNativePathPosition = (
                nativePathNodeTarget.RawX,
                nativePathNodeTarget.RawY,
                nativePathNodeTarget.RawZ);
            (int X, int Y) persistedDragonRunToPosition = (
                dragonRunToTarget.RawX,
                dragonRunToTarget.RawY);
            _ = await PersistCurrentDragonRunToEditsAsync();
            if (!ManifestContainsRow(
                    mobyPath,
                    row => row.TryGetProperty(
                            "editorControlKind",
                            out JsonElement controlKind) &&
                        string.Equals(
                            controlKind.GetString(),
                            dragonRunToTarget.StableId,
                            StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "The initial dragon run-to save did not publish its synthetic manifest row.");
            }

            objectTarget.Position = new Vector3f(
                objectTarget.Position.X + 0.25f,
                objectTarget.Position.Y,
                objectTarget.Position.Z);
            Vector3f persistedMobyPosition = objectTarget.Position;
            _ = await PersistCurrentMobyEditsAsync();
            if (!ManifestContainsRow(
                    mobyPath,
                    row => row.TryGetProperty("trueIndex", out JsonElement trueIndex) &&
                        trueIndex.ValueKind == JsonValueKind.Number &&
                        trueIndex.GetInt32() == objectTarget.TrueIndex) ||
                !ManifestContainsRow(
                    mobyPath,
                    row => row.TryGetProperty(
                            "editorControlKind",
                            out JsonElement controlKind) &&
                        string.Equals(
                            controlKind.GetString(),
                            dragonRunToTarget.StableId,
                            StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "Immediate Moby persistence dropped either the ordinary Moby row or the saved dragon synthetic row.");
            }
            AssertReloadedObjectLayers(
                persistedMobyPosition,
                persistedNativePathPosition,
                persistedDragonRunToPosition,
                "Immediate compound object persistence");

            byte[] manifestBeforeInjectedFailure = File.ReadAllBytes(mobyPath);
            byte[] nativePathBeforeInjectedFailure = File.ReadAllBytes(nativeMovementPath);
            string savedMobyBeforeInjectedFailure = _savedMobyEditSignature;
            string savedNativeBeforeInjectedFailure = _savedNativeMovementEditSignature;
            string savedDragonBeforeInjectedFailure = _savedDragonRunToEditSignature;
            int loadedMobyBeforeInjectedFailure = _loadedMobyEdits;
            objectTarget.Position = new Vector3f(
                objectTarget.Position.X,
                objectTarget.Position.Y + 0.25f,
                objectTarget.Position.Z);
            bool injectedFailureObserved = false;
            RegularObjectPersistenceFaultForTesting = stage =>
            {
                if (string.Equals(
                        stage,
                        "after-native-manifest-publish",
                        StringComparison.Ordinal))
                {
                    throw new IOException(
                        "Injected compound object persistence failure after manifest publication.");
                }
            };
            try
            {
                _ = await PersistCurrentMobyEditsAsync();
            }
            catch (IOException exception) when (exception.Message.Contains(
                "Injected compound object persistence failure",
                StringComparison.Ordinal))
            {
                injectedFailureObserved = true;
            }
            finally
            {
                RegularObjectPersistenceFaultForTesting = null;
            }
            if (!injectedFailureObserved ||
                _regularEditorPersistenceBusy ||
                _regularEditorPersistenceOperation != null ||
                !File.ReadAllBytes(mobyPath).SequenceEqual(
                    manifestBeforeInjectedFailure) ||
                !File.ReadAllBytes(nativeMovementPath).SequenceEqual(
                    nativePathBeforeInjectedFailure) ||
                !string.Equals(
                    _savedMobyEditSignature,
                    savedMobyBeforeInjectedFailure,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    _savedNativeMovementEditSignature,
                    savedNativeBeforeInjectedFailure,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    _savedDragonRunToEditSignature,
                    savedDragonBeforeInjectedFailure,
                    StringComparison.Ordinal) ||
                _loadedMobyEdits != loadedMobyBeforeInjectedFailure)
            {
                throw new InvalidOperationException(
                    "Injected compound object persistence failure did not restore both files, all saved signatures, and the shared operation fence exactly.");
            }

            async Task AssertStageCleanupFailureAsync(
                string label,
                Func<string, Exception> createFailure)
            {
                objectTarget.Position = new Vector3f(
                    objectTarget.Position.X + 0.125f,
                    objectTarget.Position.Y,
                    objectTarget.Position.Z);
                Vector3f expectedMobyPosition = objectTarget.Position;
                (int X, int Y, int Z) expectedNativePathPosition = (
                    nativePathNodeTarget.RawX,
                    nativePathNodeTarget.RawY,
                    nativePathNodeTarget.RawZ);
                (int X, int Y) expectedDragonRunToPosition = (
                    dragonRunToTarget.RawX,
                    dragonRunToTarget.RawY);
                List<string> cleanupAttempts = [];
                Exception? observedFailure = null;
                RegularObjectPersistenceStageCleanupFaultForTesting = stage =>
                {
                    cleanupAttempts.Add(stage);
                    throw createFailure(stage);
                };
                try
                {
                    _ = await PersistCurrentMobyEditsAsync();
                }
                catch (Exception failure)
                {
                    observedFailure = failure;
                }
                finally
                {
                    RegularObjectPersistenceStageCleanupFaultForTesting = null;
                }

                Type expectedFailureType = createFailure("expected-type").GetType();
                if (observedFailure?.GetType() != expectedFailureType ||
                    cleanupAttempts.Count != 2 ||
                    !string.Equals(
                        cleanupAttempts[0],
                        "native-manifest-stage",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        cleanupAttempts[1],
                        "native-path-stage",
                        StringComparison.Ordinal) ||
                    _regularEditorPersistenceBusy ||
                    _regularEditorPersistenceOperation != null ||
                    !string.Equals(
                        _savedMobyEditSignature,
                        BuildMobyEditSignature(retailMobys),
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        _savedNativeMovementEditSignature,
                        BuildNativeMovementEditSignature(),
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        _savedDragonRunToEditSignature,
                        BuildDragonRunToEditSignature(),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Injected {label} stage cleanup failure did not attempt both stage deletions, preserve the exact published signatures, and release the persistence fence.");
                }
                AssertReloadedObjectLayers(
                    expectedMobyPosition,
                    expectedNativePathPosition,
                    expectedDragonRunToPosition,
                    $"Injected {label} stage cleanup failure");
            }

            await AssertStageCleanupFailureAsync(
                "IOException",
                stage => new IOException(
                    $"Injected IOException while cleaning {stage}."));
            await AssertStageCleanupFailureAsync(
                "UnauthorizedAccessException",
                stage => new UnauthorizedAccessException(
                    $"Injected UnauthorizedAccessException while cleaning {stage}."));

            nativePathNodeTarget.TranslateRaw(2, -1, 1);
            dragonRunToTarget.TranslateRaw(-1, 2);
            Vector3f beforeLeaveMobyPosition = objectTarget.Position;
            (int X, int Y, int Z) beforeLeaveNativePathPosition = (
                nativePathNodeTarget.RawX,
                nativePathNodeTarget.RawY,
                nativePathNodeTarget.RawZ);
            (int X, int Y) beforeLeaveDragonRunToPosition = (
                dragonRunToTarget.RawX,
                dragonRunToTarget.RawY);
            if (!await PersistCurrentMobyEditsBeforeLeavingLevelAsync(lab))
            {
                throw new InvalidOperationException(
                    "The guarded level-leave path refused the compound object/native-movement save.");
            }
            AssertReloadedObjectLayers(
                beforeLeaveMobyPosition,
                beforeLeaveNativePathPosition,
                beforeLeaveDragonRunToPosition,
                "Guarded level-leave persistence");
            if (!string.Equals(
                    _savedMobyEditSignature,
                    BuildMobyEditSignature(retailMobys),
                    StringComparison.Ordinal) ||
                !string.Equals(
                    _savedNativeMovementEditSignature,
                    BuildNativeMovementEditSignature(),
                    StringComparison.Ordinal) ||
                !string.Equals(
                    _savedDragonRunToEditSignature,
                    BuildDragonRunToEditSignature(),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The guarded level-leave save did not publish all three exact saved signatures.");
            }

            float[] changedTerrain = terrainDeltasBefore.ToArray();
            changedTerrain[0] += 1;
            terrain.ApplyTerrainVertexDeltas(changedTerrain);
            RefreshActionAvailability();
            await AssertRegularPersistenceBlocksLabAsync(
                "terrain-save-started",
                "Save Terrain Changes",
                () => TrySaveCurrentTerrainEditsAsync());
            if (!TerrainEditFileHasEdits(terrainPath) ||
                !string.Equals(
                    _savedTerrainEditSignature,
                    BuildTerrainEditSignature(retailGeometry),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Save Terrain Changes did not publish the exact captured retail terrain snapshot.");
            }

            TaskCompletionSource buildSafetyStarted =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource buildSafetyRelease =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            BuildSafetyInspectionDelayOverrideForTesting = stage =>
            {
                if (!string.Equals(stage, "build-safety-scan-started", StringComparison.Ordinal))
                    return Task.CompletedTask;
                buildSafetyStarted.TrySetResult();
                return buildSafetyRelease.Task;
            };
            Task<bool> buildSafety = RunBuildSafetyInspectorForTestingAsync();
            try
            {
                await buildSafetyStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
                RefreshActionAvailability();
                Id65BlankLabUiSnapshot labUi = CaptureId65BlankLabUiSnapshotForTesting();
                int id65GenerationBefore = _id65BlankLabManualOperationGeneration;
                if (!_regularEditorPersistenceBusy || !_buildSafetyBusy ||
                    _regularEditorPersistenceOperation == null ||
                    _levelJumpBox.IsEnabled || _levelList.IsEnabled ||
                    (_openWorkspaceButton != null && _openWorkspaceButton.IsEnabled) ||
                    _viewport.IsEnabled ||
                    (_inspectBuildSafetyButton != null && _inspectBuildSafetyButton.IsEnabled) ||
                    IsEnabled(_updateNotificationBanner) ||
                    labUi.BuildEnabled || labUi.LoadEnabled || labUi.SaveEnabled ||
                    labUi.CreateCueEnabled)
                {
                    throw new InvalidOperationException(
                        "Delayed Build Safety did not own the shared persistence fence or disable ID65 admission.");
                }
                if (TryBeginUpdateSnapshotForTesting() || updateSnapshotStarted)
                {
                    throw new InvalidOperationException(
                        "Delayed Build Safety admitted an update safety snapshot action.");
                }

                await LoadId65BlankLabAsync();
                if (!ReferenceEquals(_currentLevel, retail) ||
                    !ReferenceEquals(_currentGeometry, retailGeometry) ||
                    !ReferenceEquals(_currentMobys, retailMobys) ||
                    _id65BlankLabBusy || _id65BlankLabManualOperation != null ||
                    id65GenerationBefore != _id65BlankLabManualOperationGeneration)
                {
                    throw new InvalidOperationException(
                        "ID65 Lab admission overlapped the delayed Build Safety scan.");
                }

                buildSafetyRelease.TrySetResult();
                if (!await buildSafety || _regularEditorPersistenceBusy || _buildSafetyBusy)
                {
                    throw new InvalidOperationException(
                        "Build Safety did not complete and release its shared persistence fence exactly once.");
                }
            }
            finally
            {
                buildSafetyRelease.TrySetResult();
                if (!buildSafety.IsCompleted)
                {
                    try
                    {
                        await buildSafety;
                    }
                    catch
                    {
                        // Preserve the fixture's primary assertion failure.
                    }
                }
                BuildSafetyInspectionDelayOverrideForTesting = null;
            }

            return
                "Full Save, immediate object/native/dragon persistence, guarded level-leave persistence, Save Terrain Changes, and Build Safety held one exact retail-scene persistence fence; Moby saves preserved dragon rows, compound publication failure rolled both files back exactly, IOException and UnauthorizedAccess cleanup faults attempted both stages and released the fence with exact published state, update snapshots and reverse ID65 admission stayed disabled/refused until each operation completed";
        }
        finally
        {
            RegularEditorPersistenceDelayOverrideForTesting = null;
            BuildSafetyInspectionDelayOverrideForTesting = null;
            RegularObjectPersistenceFaultForTesting = null;
            RegularObjectPersistenceStageCleanupFaultForTesting = null;
            UpdateSnapshotStartedForTesting = null;
            objectTarget.Position = objectPositionBefore;
            nativePathNodeTarget.SetRawPosition(
                nativePathPositionBefore.X,
                nativePathPositionBefore.Y,
                nativePathPositionBefore.Z);
            dragonRunToTarget.SetRawEndpoint(
                dragonRunToPositionBefore.X,
                dragonRunToPositionBefore.Y);
            terrain.ApplyTerrainVertexDeltas(terrainDeltasBefore);
            foreach ((string path, byte[]? contents) in persistedBefore)
            {
                if (contents == null)
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                else
                {
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(path) ?? _workspace.RootPath);
                    File.WriteAllBytes(path, contents);
                }
            }
            _savedMobyEditSignature = savedMobySignatureBefore;
            _savedTerrainEditSignature = savedTerrainSignatureBefore;
            _savedNativeMovementEditSignature = savedNativeMovementSignatureBefore;
            _savedDragonRunToEditSignature = savedDragonRunToSignatureBefore;
            _loadedMobyEdits = loadedMobyEditsBefore;
            _loadedTerrainEdits = loadedTerrainEditsBefore;
            InvalidateBuildSafetySummary();
            if (!ReferenceEquals(_currentLevel, lab))
                await SelectLevelAsync(lab);
            if (!IsCurrentId65BlankLab() || _currentGeometry == null)
            {
                throw new InvalidOperationException(
                    "The reverse regular-persistence probe did not restore the loaded ID65 Lab.");
            }
        }
    }

    internal async Task<string> AssertId65BlankLabManualOperationFencingForTestingAsync()
    {
        if (!IsCurrentId65BlankLab() || _currentLevel == null || _currentGeometry == null ||
            _id65BlankLabPaths == null || _id65BlankLabManifest == null)
        {
            throw new InvalidOperationException(
                "The ID65 manual-operation fence probe requires the validated, loaded Lab.");
        }

        EditorWorkspace originalWorkspace = _workspace;
        LevelDefinition originalLevel = _currentLevel;
        GeometryCandidate originalGeometry = _currentGeometry;
        UnusedLevel65BlankLevelLabWorkspacePaths originalPaths = _id65BlankLabPaths;
        UnusedLevel65BlankLevelLabManifest originalManifest = _id65BlankLabManifest;
        string originalSavedTerrainSignature = _savedTerrainEditSignature;
        string originalRevealPath = _id65BlankLabRevealPath;
        Moby? originalSelectedMoby = _selectedMoby;
        TerrainPolygon? originalSelectedTerrain = _selectedTerrain;
        int originalSelectedTerrainIndex = _selectedTerrainIndex;
        int originalSelectedTerrainPointIndex = _selectedTerrainPointIndex;
        int mutationTargetIndex = originalGeometry.Polygons.FindIndex(candidate =>
            !candidate.IsTerrainRemoved &&
            !candidate.IsTerrainAddClone &&
            string.Equals(candidate.Detail, "hp", StringComparison.OrdinalIgnoreCase));
        if (mutationTargetIndex < 0)
            throw new InvalidOperationException("The ID65 operation fence probe found no existing HP terrain face.");
        TerrainPolygon mutationTarget = originalGeometry.Polygons[mutationTargetIndex];
        _selectedMoby = null;
        _selectedTerrain = mutationTarget;
        _selectedTerrainIndex = mutationTargetIndex;
        _selectedTerrainPointIndex = 0;
        RefreshActionAvailability();
        string authoredPath = Id65BlankLabTerrainEditsPath();
        byte[]? authoredBefore = File.Exists(authoredPath)
            ? File.ReadAllBytes(authoredPath)
            : null;

        bool AuthoredFileIsExact() => authoredBefore == null
            ? !File.Exists(authoredPath)
            : File.Exists(authoredPath) &&
              File.ReadAllBytes(authoredPath).SequenceEqual(authoredBefore);

        async Task AssertCanceledBeforeMutationAsync(
            string stage,
            Func<Task> startOperation)
        {
            TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Id65BlankLabManualOperationDelayOverrideForTesting = (observedStage, cancellationToken) =>
            {
                if (!string.Equals(observedStage, stage, StringComparison.Ordinal))
                    return Task.CompletedTask;
                started.TrySetResult();
                return release.Task.WaitAsync(cancellationToken);
            };

            Task task = startOperation();
            await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Id65BlankLabUiSnapshot busySnapshot = CaptureId65BlankLabUiSnapshotForTesting();
            if (!_id65BlankLabBusy ||
                (_openWorkspaceButton != null && _openWorkspaceButton.IsEnabled) ||
                _levelJumpBox.IsEnabled ||
                _levelList.IsEnabled ||
                _viewport.IsEnabled ||
                (_modernTerrainWorkspaceTab != null && _modernTerrainWorkspaceTab.IsEnabled) ||
                (_releaseTerrainRaiseFaceButton != null && _releaseTerrainRaiseFaceButton.IsEnabled) ||
                (_releaseTerrainLowerFaceButton != null && _releaseTerrainLowerFaceButton.IsEnabled) ||
                (_releaseTerrainUndoHeightButton != null && _releaseTerrainUndoHeightButton.IsEnabled) ||
                (_terrainTaskSaveButton != null && _terrainTaskSaveButton.IsEnabled) ||
                busySnapshot.BuildEnabled ||
                busySnapshot.LoadEnabled ||
                busySnapshot.SaveEnabled ||
                busySnapshot.CreateCueEnabled)
            {
                throw new InvalidOperationException(
                    $"The delayed {stage} operation did not disable workspace, level, and peer Lab commands. " +
                    $"busy={_id65BlankLabBusy}; open={(_openWorkspaceButton == null ? "<missing>" : _openWorkspaceButton.IsEnabled.ToString())}; " +
                    $"jump={_levelJumpBox.IsEnabled}; list={_levelList.IsEnabled}; " +
                    $"build={busySnapshot.BuildEnabled}; load={busySnapshot.LoadEnabled}; " +
                    $"save={busySnapshot.SaveEnabled}; create={busySnapshot.CreateCueEnabled}.");
            }

            string mutationSignatureBefore = BuildTerrainEditSignature(originalGeometry);
            byte[]? mutationFileBefore = File.Exists(authoredPath)
                ? File.ReadAllBytes(authoredPath)
                : null;
            NudgeSelectedTerrain(32);
            MoveTerrainFromViewport(new ViewportTerrainMoveRequestedEventArgs(
                mutationTargetIndex,
                mutationTarget,
                0,
                0,
                32,
                stageAddCopy: false));
            ApplySelectedTerrainPointEdit(value => value + 32, null, "Raised");
            await UndoSelectedTerrainHeightAsync();
            await UndoSelectedTerrainAsync();
            await UndoSelectedTerrainTexturePaintAsync();
            await ApplyTerrainTexturePaintBrushAsync(mutationTargetIndex, mutationTarget);
            bool regularSaveSucceeded = await TrySaveCurrentTerrainEditsAsync();
            bool mutationFileExact = mutationFileBefore == null
                ? !File.Exists(authoredPath)
                : File.Exists(authoredPath) &&
                  File.ReadAllBytes(authoredPath).SequenceEqual(mutationFileBefore);
            if (regularSaveSucceeded ||
                !string.Equals(
                    BuildTerrainEditSignature(originalGeometry),
                    mutationSignatureBefore,
                    StringComparison.Ordinal) ||
                !mutationFileExact)
            {
                throw new InvalidOperationException(
                    $"A regular height, point, viewport, texture, Undo, or Save route escaped delayed {stage} fencing.");
            }
            if (SelectLevel(_retailCatalog.Levels.First()) ||
                !ReferenceEquals(_currentLevel, originalLevel) ||
                !ReferenceEquals(_currentGeometry, originalGeometry))
            {
                throw new InvalidOperationException(
                    $"A synchronous level route escaped delayed {stage} fencing.");
            }
            await OpenWorkspaceAsync();
            if (!ReferenceEquals(_workspace, originalWorkspace) ||
                !_id65BlankLabBusy ||
                !(_statusText.Text ?? "").Contains(
                    "active ID65 Lab operation",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Open Workspace did not fail closed during delayed {stage}.");
            }

            CancelId65BlankLabManualOperationForWorkspaceChange();
            const string staleStatusMarker = "new generation owns this status";
            _statusText.Text = staleStatusMarker;
            release.TrySetResult();
            await task;
            bool statusExact = string.Equals(
                _statusText.Text,
                staleStatusMarker,
                StringComparison.Ordinal);
            bool workspaceExact = ReferenceEquals(_workspace, originalWorkspace);
            bool levelExact = ReferenceEquals(_currentLevel, originalLevel);
            bool geometryExact = ReferenceEquals(_currentGeometry, originalGeometry);
            bool pathsExact = ReferenceEquals(_id65BlankLabPaths, originalPaths);
            bool manifestExact = ReferenceEquals(_id65BlankLabManifest, originalManifest);
            bool signatureExact = _savedTerrainEditSignature == originalSavedTerrainSignature;
            bool revealExact = _id65BlankLabRevealPath == originalRevealPath;
            bool authoredExact = AuthoredFileIsExact();
            if (!statusExact || !workspaceExact || !levelExact || !geometryExact ||
                !pathsExact || !manifestExact || !signatureExact || !revealExact ||
                !authoredExact)
            {
                throw new InvalidOperationException(
                    $"The canceled {stage} completion changed guarded state. " +
                    $"status={statusExact}; workspace={workspaceExact}; level={levelExact}; " +
                    $"geometry={geometryExact}; paths={pathsExact}; manifest={manifestExact}; " +
                    $"signature={signatureExact}; reveal={revealExact}; authored={authoredExact}.");
            }
            Id65BlankLabManualOperationDelayOverrideForTesting = null;
        }

        try
        {
            await AssertCanceledBeforeMutationAsync(
                "build-started",
                BuildOrRefreshId65BlankLabBaseAsync);
            await AssertCanceledBeforeMutationAsync(
                "load-started",
                LoadId65BlankLabAsync);
            await AssertCanceledBeforeMutationAsync(
                "load-work-completed",
                LoadId65BlankLabAsync);
            await AssertCanceledBeforeMutationAsync(
                "save-started",
                async () => _ = await SaveId65BlankLabWorkspaceCoreAsync(announce: true));
            await AssertCanceledBeforeMutationAsync(
                "save-work-completed",
                async () => _ = await SaveId65BlankLabWorkspaceCoreAsync(announce: true));
            await AssertCanceledBeforeMutationAsync(
                "save-work-completed",
                async () =>
                {
                    try
                    {
                        _ = await PersistCurrentTerrainEditsAsync();
                    }
                    catch (InvalidOperationException)
                    {
                        // Cancellation is surfaced to the mutation caller so it can
                        // restore its in-memory preimage; the fence assertions below
                        // verify that no staged file or shared signature committed.
                    }
                });
            await AssertCanceledBeforeMutationAsync(
                "create-started",
                CreateId65BlankLabDisposableCueAsync);
            await AssertCanceledBeforeMutationAsync(
                "restore-started",
                async () => _ = await RestoreId65BlankLabTerrainAsync("terrain"));
            await AssertCanceledBeforeMutationAsync(
                "restore-work-completed",
                async () => _ = await RestoreId65BlankLabTerrainAsync("terrain"));

            TaskCompletionSource overlapStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource overlapRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Id65BlankLabManualOperationDelayOverrideForTesting = (stage, cancellationToken) =>
            {
                if (!string.Equals(stage, "save-started", StringComparison.Ordinal))
                    return Task.CompletedTask;
                overlapStarted.TrySetResult();
                return overlapRelease.Task.WaitAsync(cancellationToken);
            };
            Task<bool> firstSave = SaveId65BlankLabWorkspaceCoreAsync(announce: true);
            await overlapStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Id65BlankLabManualOperation? firstOperation = _id65BlankLabManualOperation;
            int firstGeneration = _id65BlankLabManualOperationGeneration;
            bool overlappingSave = await SaveId65BlankLabWorkspaceCoreAsync(announce: true);
            if (overlappingSave || firstOperation == null ||
                !ReferenceEquals(firstOperation, _id65BlankLabManualOperation) ||
                firstGeneration != _id65BlankLabManualOperationGeneration)
            {
                throw new InvalidOperationException(
                    "A second ID65 Save overlapped or displaced the active single-flight operation.");
            }
            CancelId65BlankLabManualOperationForWorkspaceChange();
            overlapRelease.TrySetResult();
            if (await firstSave)
            {
                throw new InvalidOperationException(
                    "The canceled first ID65 Save reported success after an overlapping Save was refused.");
            }
            Id65BlankLabManualOperationDelayOverrideForTesting = null;

            TaskCompletionSource regularLoadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource regularLoadRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
            LevelSelectionDelayOverrideForTesting = stage =>
            {
                if (!string.Equals(stage, "level-load-work-completed", StringComparison.Ordinal))
                    return Task.CompletedTask;
                regularLoadStarted.TrySetResult();
                return regularLoadRelease.Task;
            };
            LevelDefinition pendingRetailLevel = _retailCatalog.Levels.First();
            Task pendingRetailLoad = SelectLevelAsync(pendingRetailLevel);
            await regularLoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Id65BlankLabUiSnapshot navigationBusySnapshot = CaptureId65BlankLabUiSnapshotForTesting();
            int operationGenerationBeforeRefusals = _id65BlankLabManualOperationGeneration;
            await OpenWorkspaceAsync();
            bool reverseSynchronousLevelChange = SelectLevel(_retailCatalog.Levels.Last());
            await SelectLevelAsync(_retailCatalog.Levels.Last());
            await BuildOrRefreshId65BlankLabBaseAsync();
            bool reverseSave = await SaveId65BlankLabWorkspaceCoreAsync(announce: true);
            bool reverseRestore = await RestoreId65BlankLabTerrainAsync("terrain");
            if (reverseSynchronousLevelChange || reverseSave || reverseRestore ||
                _id65BlankLabManualOperation != null ||
                _id65BlankLabBusy ||
                (_openWorkspaceButton != null && _openWorkspaceButton.IsEnabled) ||
                _levelJumpBox.IsEnabled ||
                _levelList.IsEnabled ||
                !ReferenceEquals(_currentLevel, originalLevel) ||
                !ReferenceEquals(_currentGeometry, originalGeometry) ||
                operationGenerationBeforeRefusals != _id65BlankLabManualOperationGeneration ||
                navigationBusySnapshot.BuildEnabled ||
                navigationBusySnapshot.LoadEnabled ||
                navigationBusySnapshot.SaveEnabled ||
                navigationBusySnapshot.CreateCueEnabled)
            {
                throw new InvalidOperationException(
                    "An ID65 Build, Save, or Restore started while a regular awaited level load was still in flight.");
            }
            _levelLoadRequestId++;
            regularLoadRelease.TrySetResult();
            await pendingRetailLoad;
            LevelSelectionDelayOverrideForTesting = null;
            if (!ReferenceEquals(_currentLevel, originalLevel) ||
                !ReferenceEquals(_currentGeometry, originalGeometry))
            {
                throw new InvalidOperationException(
                    "The delayed regular level load committed after its request generation was invalidated.");
            }

            TaskCompletionSource switchStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource switchRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Id65BlankLabManualOperationDelayOverrideForTesting = (stage, cancellationToken) =>
            {
                if (!string.Equals(stage, "save-started", StringComparison.Ordinal))
                    return Task.CompletedTask;
                switchStarted.TrySetResult();
                return switchRelease.Task.WaitAsync(cancellationToken);
            };
            Task<bool> staleSave = SaveId65BlankLabWorkspaceCoreAsync(announce: true);
            await switchStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            LevelDefinition switchedLevel = _retailCatalog.Levels.First();
            CancelId65BlankLabManualOperationForWorkspaceChange();
            _workspace = new EditorWorkspace(
                originalWorkspace.RootPath + "-id65-operation-fence-other");
            _currentLevel = switchedLevel;
            _currentGeometry = null;
            const string switchedStatus = "switched workspace and level own this status";
            _statusText.Text = switchedStatus;
            switchRelease.TrySetResult();
            bool staleSaveResult = await staleSave;
            if (staleSaveResult ||
                !string.Equals(_statusText.Text, switchedStatus, StringComparison.Ordinal) ||
                PathsEqual(_workspace.RootPath, originalWorkspace.RootPath) ||
                !ReferenceEquals(_currentLevel, switchedLevel) ||
                _currentGeometry != null ||
                !ReferenceEquals(_id65BlankLabPaths, originalPaths) ||
                !ReferenceEquals(_id65BlankLabManifest, originalManifest) ||
                !AuthoredFileIsExact())
            {
                throw new InvalidOperationException(
                    "A delayed ID65 Save completion crossed a forced workspace/level/geometry generation switch.");
            }

            return
                "Build, Load, manual/automatic Save, Create CUE, and Restore canceled at start and after load/save/restore worker completion; viewport, regular terrain mutation/Undo/Save routes, workspace and level controls, and peer Lab commands stayed fenced; overlapping Save was single-flight; reverse-order regular level loading refused Lab operations; delayed completions could not commit status, identity, signatures, reveal state, or authored bytes across generation switches";
        }
        finally
        {
            Id65BlankLabManualOperationDelayOverrideForTesting = null;
            LevelSelectionDelayOverrideForTesting = null;
            Id65BlankLabTerrainPersistenceFaultForTesting = null;
            if (_id65BlankLabManualOperation != null || _id65BlankLabBusy)
                CancelId65BlankLabManualOperationForWorkspaceChange();
            _workspace = originalWorkspace;
            _currentLevel = originalLevel;
            _currentGeometry = originalGeometry;
            _id65BlankLabPaths = originalPaths;
            _id65BlankLabManifest = originalManifest;
            _savedTerrainEditSignature = originalSavedTerrainSignature;
            _id65BlankLabRevealPath = originalRevealPath;
            _selectedMoby = originalSelectedMoby;
            _selectedTerrain = originalSelectedTerrain;
            _selectedTerrainIndex = originalSelectedTerrainIndex;
            _selectedTerrainPointIndex = originalSelectedTerrainPointIndex;
            if (authoredBefore == null)
            {
                if (File.Exists(authoredPath))
                    File.Delete(authoredPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(authoredPath) ?? originalWorkspace.RootPath);
                File.WriteAllBytes(authoredPath, authoredBefore);
            }
            _loadingLevel = false;
            SyncLevelPickers(originalLevel);
            _viewport.Geometry = originalGeometry;
            RefreshId65BlankLabUi();
            RefreshLevelSelectionAvailability();
        }
    }

    internal async Task<string> AssertWorkspaceTransitionFencingForTestingAsync()
    {
        ReleaseProjectContext originalReleaseContext = ReleaseProjectBootstrap.Current
            ?? throw new InvalidOperationException(
                "The workspace-transition probe requires initialized release project storage.");
        if (!PathsEqual(originalReleaseContext.Project.RootPath, _workspace.RootPath))
        {
            throw new InvalidOperationException(
                "The workspace-transition probe release context does not own the active editor workspace.");
        }
        if (_currentLevel == null || _currentGeometry == null)
        {
            throw new InvalidOperationException(
                "The workspace-transition probe requires a fully loaded source scene.");
        }

        string targetRoot = Path.Combine(
            Path.GetTempPath(),
            $"spyro-editor-workspace-transition-{Guid.NewGuid():N}");
        string currentProjectSettingsPath = Path.Combine(
            originalReleaseContext.UserData.SettingsPath,
            "current-project.json");
        byte[]? currentProjectSettingsBefore = File.Exists(currentProjectSettingsPath)
            ? File.ReadAllBytes(currentProjectSettingsPath)
            : null;
        string? workspaceEnvironmentBefore = Environment.GetEnvironmentVariable(
            ReleaseProjectBootstrap.WorkspaceEnvironmentVariable);
        EditorWorkspace originalWorkspace = _workspace;
        LevelCatalog originalRetailCatalog = _retailCatalog;
        LevelCatalog originalCatalog = _catalog;
        LevelDefinition originalLevel = _currentLevel;
        GeometryCandidate originalGeometry = _currentGeometry;
        List<Moby> originalMobys = _currentMobys;
        List<NativeMobyPath> originalNativePaths = _currentNativeMobyPaths;
        Moby? originalSelectedMoby = _selectedMoby;
        TerrainPolygon? originalSelectedTerrain = _selectedTerrain;
        int originalSelectedTerrainIndex = _selectedTerrainIndex;
        int originalSelectedTerrainPointIndex = _selectedTerrainPointIndex;
        UnusedLevel65BlankLevelLabWorkspacePaths? originalId65Paths =
            _id65BlankLabPaths;
        UnusedLevel65BlankLevelLabManifest? originalId65Manifest =
            _id65BlankLabManifest;
        int originalLevelLoadRequestId = _levelLoadRequestId;
        string originalTerrainSignature = BuildTerrainEditSignature(originalGeometry);
        string originalMobySignature = BuildMobyEditSignature(originalMobys);
        string originalNativeSignature = BuildNativeMovementEditSignature();
        string originalDragonSignature = BuildDragonRunToEditSignature();
        string originalSavedTerrainSignature = _savedTerrainEditSignature;
        string originalSavedMobySignature = _savedMobyEditSignature;
        string originalSavedNativeSignature = _savedNativeMovementEditSignature;
        string originalSavedDragonSignature = _savedDragonRunToEditSignature;
        int commitCountBefore = WorkspaceTransitionCommitCountForTesting;
        TerrainPolygon mutationTarget = originalGeometry.Polygons.First(candidate =>
            !candidate.IsTerrainRemoved &&
            !candidate.IsTerrainAddClone &&
            string.Equals(candidate.Detail, "hp", StringComparison.OrdinalIgnoreCase));
        _selectedMoby = null;
        _selectedTerrain = mutationTarget;
        _selectedTerrainIndex = originalGeometry.Polygons.IndexOf(mutationTarget);
        _selectedTerrainPointIndex = 0;
        float[] mutationDeltasBefore = mutationTarget.TerrainVertexDeltas().ToArray();

        bool OptionalFileIsExact(string path, byte[]? expected) =>
            expected == null
                ? !File.Exists(path)
                : File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(expected);

        bool OldReleaseStateIsExact() =>
            ReferenceEquals(ReleaseProjectBootstrap.Current, originalReleaseContext) &&
            string.Equals(
                Environment.GetEnvironmentVariable(
                    ReleaseProjectBootstrap.WorkspaceEnvironmentVariable),
                workspaceEnvironmentBefore,
                StringComparison.Ordinal) &&
            OptionalFileIsExact(
                currentProjectSettingsPath,
                currentProjectSettingsBefore);

        bool OldEditorStateIsExact() =>
            ReferenceEquals(_workspace, originalWorkspace) &&
            ReferenceEquals(_retailCatalog, originalRetailCatalog) &&
            ReferenceEquals(_catalog, originalCatalog) &&
            ReferenceEquals(_currentLevel, originalLevel) &&
            ReferenceEquals(_currentGeometry, originalGeometry) &&
            ReferenceEquals(_currentMobys, originalMobys) &&
            ReferenceEquals(_currentNativeMobyPaths, originalNativePaths) &&
            ReferenceEquals(_id65BlankLabPaths, originalId65Paths) &&
            ReferenceEquals(_id65BlankLabManifest, originalId65Manifest) &&
            ReferenceEquals(_selectedTerrain, mutationTarget) &&
            _selectedTerrainIndex == originalGeometry.Polygons.IndexOf(mutationTarget) &&
            _selectedTerrainPointIndex == 0 &&
            _levelLoadRequestId == originalLevelLoadRequestId &&
            mutationTarget.TerrainVertexDeltas().SequenceEqual(mutationDeltasBefore) &&
            string.Equals(
                BuildTerrainEditSignature(originalGeometry),
                originalTerrainSignature,
                StringComparison.Ordinal) &&
            string.Equals(
                BuildMobyEditSignature(originalMobys),
                originalMobySignature,
                StringComparison.Ordinal) &&
            string.Equals(
                BuildNativeMovementEditSignature(),
                originalNativeSignature,
                StringComparison.Ordinal) &&
            string.Equals(
                BuildDragonRunToEditSignature(),
                originalDragonSignature,
                StringComparison.Ordinal) &&
            string.Equals(
                _savedTerrainEditSignature,
                originalSavedTerrainSignature,
                StringComparison.Ordinal) &&
            string.Equals(
                _savedMobyEditSignature,
                originalSavedMobySignature,
                StringComparison.Ordinal) &&
            string.Equals(
                _savedNativeMovementEditSignature,
                originalSavedNativeSignature,
                StringComparison.Ordinal) &&
            string.Equals(
                _savedDragonRunToEditSignature,
                originalSavedDragonSignature,
                StringComparison.Ordinal);

        static bool IsEnabled(Control? control) => control?.IsEnabled == true;

        void AssertTransitionControlsDisabled(string label)
        {
            Id65BlankLabUiSnapshot labUi = CaptureId65BlankLabUiSnapshotForTesting();
            if (!_workspaceTransitionBusy || _workspaceTransitionOperation == null ||
                _levelJumpBox.IsEnabled || _levelList.IsEnabled ||
                IsEnabled(_openWorkspaceButton) || _viewport.IsEnabled ||
                IsEnabled(_modernWorkspaceTabs) ||
                IsEnabled(_modernObjectWorkspaceTab) ||
                IsEnabled(_modernTerrainWorkspaceTab) ||
                IsEnabled(_modernLevelWorkspaceTab) ||
                IsEnabled(_modernEnvironmentWorkspaceTab) ||
                IsEnabled(_toolbarCreateBinButton) ||
                IsEnabled(_toolbarOpenDiscImageButton) ||
                IsEnabled(_toolbarMoreControl) ||
                IsEnabled(_updateNotificationBanner) ||
                labUi.BuildEnabled || labUi.LoadEnabled || labUi.SaveEnabled ||
                labUi.CreateCueEnabled)
            {
                throw new InvalidOperationException(
                    $"Delayed {label} did not disable the complete navigation, editing, source, update, and ID65 command surface.");
            }
        }

        async Task AssertMutationAndNavigationRefusedAsync(string label)
        {
            LevelDefinition retailTarget = _retailCatalog.Levels.First();
            bool synchronousSelection = SelectLevel(retailTarget);
            await SelectLevelAsync(retailTarget);
            bool saved = await SaveCurrentEditsAsync();
            NudgeSelectedTerrain(32);
            await LoadId65BlankLabAsync();
            if (synchronousSelection || saved ||
                !OldEditorStateIsExact() || !OldReleaseStateIsExact())
            {
                throw new InvalidOperationException(
                    $"Delayed {label} admitted a level change, Save, terrain mutation, Lab operation, or release-context write.");
            }
        }

        WorkspaceFolderPickerOverrideForTesting = () =>
            Task.FromResult<string?>(targetRoot);
        UnsavedTerrainDecisionOverrideForTesting = (_, _) =>
            Task.FromResult("Discard");
        try
        {
            TaskCompletionSource prepareStarted =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource prepareRelease =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            int delayedPrepareCount = 0;
            WorkspaceTransitionDelayOverrideForTesting = (stage, cancellationToken) =>
            {
                if (!string.Equals(stage, "prepare-completed", StringComparison.Ordinal))
                    return Task.CompletedTask;
                delayedPrepareCount++;
                prepareStarted.TrySetResult();
                return prepareRelease.Task.WaitAsync(cancellationToken);
            };

            Task canceledTransition = OpenWorkspaceAsync();
            await prepareStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            RefreshActionAvailability();
            AssertTransitionControlsDisabled("workspace preparation");
            if (!OldEditorStateIsExact() || !OldReleaseStateIsExact() ||
                delayedPrepareCount != 1 ||
                WorkspaceTransitionCommitCountForTesting != commitCountBefore)
            {
                throw new InvalidOperationException(
                    "Prepared workspace files changed the active editor or global release context before commit.");
            }
            await AssertMutationAndNavigationRefusedAsync("workspace preparation");
            CancelWorkspaceTransitionForTesting();
            prepareRelease.TrySetResult();
            await canceledTransition;
            WorkspaceTransitionDelayOverrideForTesting = null;
            if (_workspaceTransitionBusy || _workspaceTransitionOperation != null ||
                !OldEditorStateIsExact() || !OldReleaseStateIsExact() ||
                WorkspaceTransitionCommitCountForTesting != commitCountBefore)
            {
                throw new InvalidOperationException(
                    "A canceled prepared workspace transition changed editor/global state or leaked its operation fence.");
            }

            WorkspaceTransitionFaultForTesting = stage =>
            {
                if (string.Equals(
                        stage,
                        "after-release-environment-publish",
                        StringComparison.Ordinal))
                {
                    throw new IOException(
                        "Injected workspace commit failure after environment publication.");
                }
            };
            await OpenWorkspaceAsync();
            WorkspaceTransitionFaultForTesting = null;
            if (_workspaceTransitionBusy || _workspaceTransitionOperation != null ||
                !OldEditorStateIsExact() || !OldReleaseStateIsExact() ||
                WorkspaceTransitionCommitCountForTesting != commitCountBefore)
            {
                throw new InvalidOperationException(
                    "An injected post-settings/environment workspace commit failure did not restore the exact editor and release context.");
            }

            int successfulPrepareCount = 0;
            WorkspaceTransitionDelayOverrideForTesting = (stage, _) =>
            {
                if (string.Equals(stage, "prepare-completed", StringComparison.Ordinal))
                    successfulPrepareCount++;
                return Task.CompletedTask;
            };
            await OpenWorkspaceAsync();
            WorkspaceTransitionDelayOverrideForTesting = null;

            ReleaseProjectContext committedContext = ReleaseProjectBootstrap.Current
                ?? throw new InvalidOperationException(
                    "The successful workspace transition cleared release project context.");
            CurrentProjectSettings committedSettings = JsonSerializer.Deserialize<CurrentProjectSettings>(
                File.ReadAllBytes(currentProjectSettingsPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException(
                    "The successful workspace transition wrote an empty current-project setting.");
            if (_workspaceTransitionBusy || _workspaceTransitionOperation != null ||
                successfulPrepareCount != 1 ||
                WorkspaceTransitionCommitCountForTesting != commitCountBefore + 1 ||
                ReferenceEquals(_workspace, originalWorkspace) ||
                !PathsEqual(_workspace.RootPath, targetRoot) ||
                ReferenceEquals(_retailCatalog, originalRetailCatalog) ||
                ReferenceEquals(_catalog, originalCatalog) ||
                ReferenceEquals(_currentGeometry, originalGeometry) ||
                ReferenceEquals(_currentMobys, originalMobys) ||
                _currentGeometry != null || _currentMobys.Count != 0 ||
                ReferenceEquals(committedContext, originalReleaseContext) ||
                !PathsEqual(committedContext.Project.RootPath, targetRoot) ||
                !PathsEqual(committedSettings.ProjectRoot, targetRoot) ||
                !PathsEqual(
                    Environment.GetEnvironmentVariable(
                        ReleaseProjectBootstrap.WorkspaceEnvironmentVariable) ?? "",
                    targetRoot))
            {
                throw new InvalidOperationException(
                    "The successful workspace transition did not commit exactly once or retained stale source-scene/global context.");
            }

            return
                "Open Workspace held one captured generation/CTS fence through confirmation, owned saves, target preparation, transactional release settings/environment/context publication, and app workspace/catalog commit; delayed mutation/navigation/Lab admission stayed refused, cancellation preserved the exact old state, injected post-publication failure rolled global state back, and success committed once without retaining the old scene";
        }
        finally
        {
            WorkspaceFolderPickerOverrideForTesting = null;
            WorkspaceTransitionDelayOverrideForTesting = null;
            WorkspaceTransitionFaultForTesting = null;
            UnsavedTerrainDecisionOverrideForTesting = null;
            if (_workspaceTransitionOperation != null)
            {
                CancelWorkspaceTransitionForTesting();
                CompleteWorkspaceTransition(_workspaceTransitionOperation);
            }
            if (PathsEqual(_workspace.RootPath, originalWorkspace.RootPath))
            {
                _selectedMoby = originalSelectedMoby;
                _selectedTerrain = originalSelectedTerrain;
                _selectedTerrainIndex = originalSelectedTerrainIndex;
                _selectedTerrainPointIndex = originalSelectedTerrainPointIndex;
            }
            try
            {
                if (Directory.Exists(targetRoot))
                    Directory.Delete(targetRoot, recursive: true);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine(exception);
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

    private sealed record EditorMutationSceneIdentity(
        EditorWorkspace Workspace,
        string WorkspaceRoot,
        LevelDefinition Level,
        List<Moby> Mobys,
        int LevelLoadRequestId,
        int Id65OperationGeneration,
        int WorkspaceTransitionGeneration);

    private sealed record ObjectDialogSceneIdentity(
        EditorMutationSceneIdentity Scene,
        Moby Target,
        int TargetIndex,
        int TargetTrueIndex,
        bool TargetWasRemoved,
        string MobyEditSignature);

    private sealed record SourceDiscOperationIdentity(
        EditorWorkspace Workspace,
        string WorkspaceRoot,
        LevelDefinition? Level,
        GeometryCandidate? Geometry,
        List<Moby> Mobys,
        int LevelLoadRequestId,
        int Id65OperationGeneration,
        int PortableCacheBuildGeneration,
        int ProjectDataImportGeneration,
        int RegularPersistenceGeneration,
        int BuildSafetyGeneration,
        int WorkspaceTransitionGeneration);

    private sealed record Id65BlankLabAuthoredLayerSnapshot(
        bool DirectoryExisted,
        IReadOnlyList<string> Files);

    private sealed record Id65BlankLabDerivedCacheBinding(
        int SchemaVersion,
        string LevelKey,
        string LockedSourceImageSha256,
        string WadAnalysisSha256,
        string SceneOverlaySha256,
        string MobyCacheSha256,
        string SourceSearchSha256);
}

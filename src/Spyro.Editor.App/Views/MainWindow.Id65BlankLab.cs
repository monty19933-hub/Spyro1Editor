using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
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
    private const string Id65BlankSlatePresetId = "blank-slate-v1";
    private const string Id65BlankSlateColorHex = "#263447";
    private const string Id65BlankLabTerrainCapability =
        "Schema-5 T66 documents expose two independently selectable structural units, Tile 1 and Tile 2, through exact canonical-document actions. Schema 4 retains its prior single optional-triangle semantics until explicitly migrated; tile 2 is never inferred. Generic HP-Z, texture, XY, add/remove, undo, and TerrainEditStore persistence remain unavailable on the fresh scene projection.";
    private const string Id65BlankLabTextureCapability =
        "Every Full Authoring Document requires exactly one base-face texture choice: Resident T25 or Proved private T66. T25 is offline-authoring/viewport-only and cannot create a candidate, BIN, runtime, or release artifact. Proved private T66 supports the frozen legacy, schema-3, and schema-5 offline candidates. Schema 4 is T66-only and currently authorizes only canonical document persistence plus static fresh-scene projection; physical materialization remains closed. Arbitrary cross-level/custom art remains unavailable.";
    private const string Id65BlankLabMobyCapability =
        "Fresh schema-4 and schema-5 documents make one mutually exclusive canonical choice: None, verified Grass T107, or resident Red Gem T21 at one of nine fixed positions on collision T13995; the position is retained while None is selected. Schema 3 must be migrated explicitly and maps only to None or Grass; schema 4 must be explicitly migrated before Tile 2 exists. Schema 5 can build its separately guarded exact offline candidate; schema-4 physical materialization, normal Create BIN, runtime acceptance, release promotion, arbitrary actors, and linked families remain unavailable.";
    private const string Id65AuthoringDocumentFileName =
        Id65AuthoringEditorDocumentWorkspaceInspector.DocumentFileName;
    private const int Id65LegacyT66DocumentSchemaVersion = 1;
    private const string Id65LegacyT66DocumentProfileId =
        "id65-authoring-editor-document-exact-composite-v1";

    private readonly TextBlock _id65BlankLabStatusText = new();
    private readonly TextBlock _id65BlankLabLockedBaseText = new();
    private readonly TextBlock _id65BlankLabAuthoredLayerText = new();
    private Button? _id65BlankLabBuildButton;
    private Button? _id65BlankLabLoadButton;
    private Button? _id65BlankLabAuthoringDocumentButton;
    private Button? _id65BlankLabAuthoringCandidateButton;
    private Button? _id65BlankLabSaveButton;
    private Button? _id65BlankLabCreateCueButton;
    private Button? _id65BlankLabRevealButton;
    private bool _id65BlankLabBusy;
    private CancellationTokenSource? _id65BlankLabManualOperationCancellation;
    private int _id65BlankLabManualOperationGeneration;
    private Id65BlankLabManualOperation? _id65BlankLabManualOperation;
    private string _id65BlankLabRevealPath = "";
    private string _id65BlankLabRevealDocumentEnvelopeSha256 = "";
    private string _id65BlankLabRevealDocumentCanonicalSha256 = "";
    private UnusedLevel65BlankLevelLabWorkspacePaths? _id65BlankLabPaths;
    private UnusedLevel65BlankLevelLabManifest? _id65BlankLabManifest;
    private string _id65BlankLabTextureOverlaySha256 = "";
    private Id65FreshLevelT66ViewportTextureProjection?
        _id65FreshLevelT66ViewportTextureProjection;
    private Id65AuthoringEditorDocumentReadModel?
        _id65FreshAuthoringDocument;
    private bool _id65FreshDocumentProjectionSynchronized;
    private Border? _id65FreshOptionalTerrainPanel;
    private Border? _id65FreshLegacyOptionalTerrainControlsPanel;
    private Border? _id65FreshTerrainSetControlsPanel;
    private Button? _id65FreshCreateOptionalTerrainButton;
    private Button? _id65FreshRemoveOptionalTerrainButton;
    private Button? _id65FreshAddTerrainTile1Button;
    private Button? _id65FreshRemoveTerrainTile1Button;
    private Button? _id65FreshAddTerrainTile2Button;
    private Button? _id65FreshRemoveTerrainTile2Button;
    private readonly TextBlock _id65FreshOptionalTerrainStatusText = new();
    private Button? _id65FreshObjectManagerDocumentButton;
    private Border? _id65FreshSchema3GrassControlsPanel;
    private ComboBox? _id65FreshGrassT107PositionSelector;
    private Button? _id65FreshAddOrMoveGrassT107Button;
    private Button? _id65FreshRemoveGrassT107Button;
    private bool _id65FreshGrassT107ControlsAvailable;
    private Border? _id65FreshMobyChoiceControlsPanel;
    private ComboBox? _id65FreshMobyChoiceSelector;
    private ComboBox? _id65FreshMobyChoicePositionSelector;
    private Button? _id65FreshApplyMobyChoiceButton;
    private bool _id65FreshMobyChoiceControlsAvailable;
    private readonly TextBlock _id65FreshObjectManagerStatusText = new();
    private string _id65BlankLabValidationFailure = "";
    private CancellationTokenSource? _id65BlankLabCatalogValidationCancellation;
    private int _id65BlankLabCatalogValidationGeneration;
    private bool _id65BlankLabCatalogValidationBusy;
    private bool _id65BlankLabCatalogValidationLifetimeEnded;
    private Task _id65BlankLabCatalogValidationTask = Task.CompletedTask;
    internal Func<
        UnusedLevel65BlankLevelLabWorkspacePaths,
        CancellationToken,
        Task<UnusedLevel65BlankLevelLabManifest>>?
        Id65BlankLabCatalogValidationOverrideForTesting
    { get; set; }
    internal Func<string, CancellationToken, Task>?
        Id65BlankLabManualOperationDelayOverrideForTesting
    { get; set; }
    internal Action<string>? Id65BlankLabTerrainPersistenceFaultForTesting { get; set; }
    internal Func<
        Id65AuthoringEditorControlState,
        bool,
        CancellationToken,
        Task<Id65AuthoringEditorControlState?>>?
        Id65AuthoringDocumentDialogOverrideForTesting
    { get; set; }
    internal Func<string, string>?
        Id65AuthoringTerrainExpectedEnvelopeOverrideForTesting
    { get; set; }
    internal Func<string, string>?
        Id65AuthoringMobyExpectedEnvelopeOverrideForTesting
    { get; set; }
    internal Action<string>?
        Id65FreshProjectionApplyFaultForTesting
    { get; set; }
    internal Func<
        UnusedLevel65BlankLevelLabWorkspacePaths,
        UnusedLevel65BlankLevelLabManifest,
        CancellationToken,
        Task<Id65BlankLabAuthoringCandidateAppResult>>?
        Id65AuthoringCandidateMaterializerOverrideForTesting
    { get; set; }

    internal sealed record Id65BlankLabAuthoringCandidateAppResult(
        string CandidateDirectoryPath,
        string CandidateImagePath,
        string CandidateCuePath,
        string CandidateReceiptPath,
        string DocumentEnvelopeSha256,
        string DocumentCanonicalSha256,
        bool UsesRemotePadT107Grid,
        string? T107RemotePadPositionId,
        bool StructuralTerrainPresent,
        bool GrassT107Present,
        string AggregatePlanSha256,
        string OutputImageSha256,
        string CandidateCueSha256,
        string ReceiptContentSha256,
        int ChangedRawSectorCount,
        long ChangedPhysicalImageBytes,
        bool ReusedExistingCandidate,
        bool CandidateArtifactsWrittenThisCall,
        bool FileSystemMutationPerformed,
        bool ExactReadbackVerified,
        bool Schema3CompilerPinsVerified,
        bool T107PositionReadbackVerified,
        bool T88ExcludedVerified,
        bool RepairedT13995SupportVerified,
        bool ProtectedComplementVerified,
        bool RetailTownSquarePreserved,
        bool TargetOverlayPreserved,
        bool ExecutablePreserved,
        bool AtomicNoReplacePublicationVerified,
        bool OwnedTemporaryResidueRemoved,
        bool CarriesAppOrUiAuthority,
        bool CarriesCreateBinAuthority,
        bool NormalRetailCreateBinComposed,
        bool RuntimeCandidateAuthorized,
        bool RuntimeProven,
        bool ReleaseIntegrated,
        bool PromotionAuthorized,
        bool Publishable,
        bool CurrentOnDiskFreshnessVerified,
        bool HostileFilesystemRaceSafe,
        bool RequiresTrustedExclusiveWorkspaceDirectory,
        bool CommonWorkspaceLeaseOwned,
        bool UsesRemotePadTerrainSet = false,
        string ActiveTerrainUnitIdsCanonical = "",
        Id65RemotePadMobyKind? RemotePadMobyKind = null,
        string? RemotePadMobyPositionId = null,
        bool Schema5CompilerPinsVerified = false,
        bool MobyChoicePositionReadbackVerified = false,
        bool FourTerrainTopologiesVerified = false,
        bool UsesBlankCyclorama = false,
        string BlankCycloramaPresetId = "",
        string BlankCycloramaColorHex = "",
        string OutputCycloramaSha256 = "",
        string CycloramaGeometrySha256 = "",
        string CycloramaCommandByteSha256 = "",
        string CycloramaNonPaletteSha256 = "",
        int CycloramaModelOffset = 0,
        int CycloramaRow80Offset = 0,
        long CycloramaWadOffset = 0,
        int CycloramaByteLength = 0,
        int PaletteWordCount = 0,
        int PaletteOwnedRangeCount = 0,
        bool ExactStoreReloadCasVerifiedThreeTimes = false,
        bool ExactEnvelopeCasVerifiedThreeTimes = false,
        bool ExactStaticAuthoringProjectionReloadVerifiedThreeTimes = false,
        bool ExactNativeInputGenerationRecapturedThreeTimes = false,
        bool ExactV3TemplateRecapturedThreeTimes = false,
        bool ExactLockedBaseRecapturedThreeTimes = false,
        bool ExactRow80ReadbackVerified = false,
        bool ExactInverseReadbackVerified = false,
        bool Schema6CompilerPinsVerified = false,
        bool BlankCycloramaIntentReadbackVerified = false,
        bool CycloramaLayoutReadbackVerified = false,
        bool EveryPaletteWordReadbackVerified = false,
        bool CycloramaGeometryReadbackVerified = false,
        bool CycloramaCommandBytesReadbackVerified = false,
        bool CycloramaNonPaletteReadbackVerified = false,
        bool Wad79Preserved = false,
        bool RetailTownSquareWad15Preserved = false,
        bool RetailTownSquareWad16Preserved = false,
        bool RetailArtisansWad9Preserved = false,
        bool RetailArtisansWad10Preserved = false,
        bool RetailTownSquareSkyBlockPreserved = false,
        bool RetailArtisansLinkedSkyBlockPreserved = false);

    private sealed record Id65AuthoringMandatoryBaseTextureChoice(
        int TextureId,
        string Label,
        string Description)
    {
        public override string ToString() => Label;
    }

    private static Id65AuthoringMandatoryBaseTextureChoice[]
        CreateId65AuthoringMandatoryBaseTextureChoices() =>
        [
            new(
                Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId,
                "Resident T25",
                "Uses the ordinary validated resident T25 close, normal, low-detail, and high-detail viewport caches. This choice is offline-authoring-only and cannot create a candidate, BIN, runtime, or release artifact."),
            new(
                Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId,
                "Proved private T66",
                "Uses the exact private T66 viewport projection. It supports the frozen legacy full-candidate state, every verified schema-3 terrain/Grass combination while T88 remains off, and the guarded schema-5 terrain-set/Moby candidate; normal Create BIN and runtime/release authority remain separate.")
        ];

    private static string Id65MandatoryBaseTextureLabel(int textureId) => textureId switch
    {
        Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId =>
            "Resident T25",
        Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId =>
            "Proved private T66",
        _ => throw new InvalidDataException(
            $"Unsupported ID65 mandatory base-face texture T{textureId}.")
    };

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
                    "Town Square-derived physical payload, but the Level Building Editor presents " +
                    "only the fresh remote starter pad, its exact selected Resident T25 or Proved private T66 base texture, matching collision/occlusion, " +
                    "and protected spawn. The physical substrate is not wiped, and this presentation " +
                    "does not make arbitrary terrain, textures, or Mobys safe.",
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
        _id65BlankLabAuthoringDocumentButton = NewAsyncButton(
            "Edit Full Authoring Document…",
            EditId65AuthoringDocumentAsync,
            RefreshId65BlankLabUi);
        _id65BlankLabAuthoringDocumentButton.Name =
            "Id65BlankLabAuthoringDocumentButton";
        _id65BlankLabAuthoringCandidateButton = NewAsyncButton(
            "Create Exact Full Candidate",
            CreateId65AuthoringAggregateCandidateAsync,
            RefreshId65BlankLabUi);
        _id65BlankLabAuthoringCandidateButton.Name =
            "Id65BlankLabAuthoringCandidateButton";
        _id65BlankLabSaveButton = NewAsyncButton(
            "Save Research Workspace",
            SaveId65BlankLabWorkspaceAsync);
        _id65BlankLabCreateCueButton = NewAsyncButton(
            "Create Disposable Test CUE",
            CreateId65BlankLabDisposableCueAsync);
        _id65BlankLabRevealButton = NewAsyncButton(
            "Reveal Test CUE",
            RevealId65BlankLabOutputAsync,
            RefreshId65BlankLabUi);

        StyleModernPrimaryButton(_id65BlankLabBuildButton, Color.FromRgb(173, 104, 27));
        StyleModernPrimaryButton(_id65BlankLabLoadButton, ModernTeal);
        StyleModernPrimaryButton(
            _id65BlankLabAuthoringDocumentButton,
            Color.FromRgb(117, 80, 158));
        StyleModernPrimaryButton(
            _id65BlankLabAuthoringCandidateButton,
            Color.FromRgb(42, 104, 155));
        StyleModernSecondaryButton(_id65BlankLabSaveButton);
        StyleModernPrimaryButton(_id65BlankLabCreateCueButton, Color.FromRgb(117, 80, 158));
        StyleModernSecondaryButton(_id65BlankLabRevealButton);

        Grid firstActions = NewEqualButtonGrid(2);
        AddModernGridButton(firstActions, _id65BlankLabBuildButton, 0);
        AddModernGridButton(firstActions, _id65BlankLabLoadButton, 1);
        panel.Children.Add(firstActions);
        panel.Children.Add(_id65BlankLabAuthoringDocumentButton);
        panel.Children.Add(_id65BlankLabAuthoringCandidateButton);

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

    private Control BuildId65FreshOptionalTerrainControls()
    {
        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = "Fresh-Level Structural Terrain",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(42, 61, 79))
        });
        panel.Children.Add(new TextBlock
        {
            Text =
                "The mandatory starter pad, matching collision/occlusion, selected Resident T25 or Proved private T66 base texture, and protected spawn always remain. " +
                "Schema 5 exposes Tile 1 and Tile 2 as independent canonical choices. Older documents retain their prior single optional-terrain control until explicitly migrated. Every action preserves the exact Moby choice and uses CAS save, readback, and full reprojection.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            LineHeight = 18
        });

        StackPanel legacyControls = new() { Spacing = 8 };
        legacyControls.Children.Add(new TextBlock
        {
            Text = "Schema 1–4 optional terrain",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(42, 61, 79))
        });
        _id65FreshCreateOptionalTerrainButton = NewAsyncButton(
            "Create optional terrain",
            () => SetId65FreshOptionalTerrainAsync(includeStructuralTerrain: true),
            RefreshId65BlankLabUi);
        _id65FreshCreateOptionalTerrainButton.Name =
            "Id65FreshCreateOptionalTerrainButton";
        _id65FreshRemoveOptionalTerrainButton = NewAsyncButton(
            "Remove optional terrain",
            () => SetId65FreshOptionalTerrainAsync(includeStructuralTerrain: false),
            RefreshId65BlankLabUi);
        _id65FreshRemoveOptionalTerrainButton.Name =
            "Id65FreshRemoveOptionalTerrainButton";
        StyleModernPrimaryButton(
            _id65FreshCreateOptionalTerrainButton,
            Color.FromRgb(42, 104, 155));
        StyleModernSecondaryButton(_id65FreshRemoveOptionalTerrainButton);

        Grid actions = NewEqualButtonGrid(2);
        AddModernGridButton(actions, _id65FreshCreateOptionalTerrainButton, 0);
        AddModernGridButton(actions, _id65FreshRemoveOptionalTerrainButton, 1);
        legacyControls.Children.Add(actions);
        _id65FreshLegacyOptionalTerrainControlsPanel = new Border
        {
            Name = "Id65FreshLegacyOptionalTerrainControlsPanel",
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(204, 216, 228)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(9),
            Child = legacyControls
        };
        panel.Children.Add(_id65FreshLegacyOptionalTerrainControlsPanel);

        StackPanel terrainSetControls = new() { Spacing = 10 };
        terrainSetControls.Children.Add(new TextBlock
        {
            Text = "Schema-5 terrain units",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(42, 61, 79))
        });
        terrainSetControls.Children.Add(new TextBlock
        {
            Text =
                "Each tile is independently present or absent. Add/Remove changes only that tile; selecting a Moby or retained position never changes terrain.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ModernMutedInk),
            FontSize = 11,
            LineHeight = 16
        });
        _id65FreshAddTerrainTile1Button = NewAsyncButton(
            "Add Tile 1",
            () => SetId65FreshTerrainTileAsync(tileNumber: 1, includeTile: true),
            RefreshId65BlankLabUi);
        _id65FreshAddTerrainTile1Button.Name = "Id65FreshAddTerrainTile1Button";
        _id65FreshRemoveTerrainTile1Button = NewAsyncButton(
            "Remove Tile 1",
            () => SetId65FreshTerrainTileAsync(tileNumber: 1, includeTile: false),
            RefreshId65BlankLabUi);
        _id65FreshRemoveTerrainTile1Button.Name =
            "Id65FreshRemoveTerrainTile1Button";
        _id65FreshAddTerrainTile2Button = NewAsyncButton(
            "Add Tile 2",
            () => SetId65FreshTerrainTileAsync(tileNumber: 2, includeTile: true),
            RefreshId65BlankLabUi);
        _id65FreshAddTerrainTile2Button.Name = "Id65FreshAddTerrainTile2Button";
        _id65FreshRemoveTerrainTile2Button = NewAsyncButton(
            "Remove Tile 2",
            () => SetId65FreshTerrainTileAsync(tileNumber: 2, includeTile: false),
            RefreshId65BlankLabUi);
        _id65FreshRemoveTerrainTile2Button.Name =
            "Id65FreshRemoveTerrainTile2Button";
        foreach (Button add in new[]
                 {
                     _id65FreshAddTerrainTile1Button,
                     _id65FreshAddTerrainTile2Button
                 })
        {
            StyleModernPrimaryButton(add, Color.FromRgb(42, 104, 155));
        }
        StyleModernSecondaryButton(_id65FreshRemoveTerrainTile1Button);
        StyleModernSecondaryButton(_id65FreshRemoveTerrainTile2Button);
        Grid tile1Actions = NewEqualButtonGrid(2);
        AddModernGridButton(tile1Actions, _id65FreshAddTerrainTile1Button, 0);
        AddModernGridButton(tile1Actions, _id65FreshRemoveTerrainTile1Button, 1);
        terrainSetControls.Children.Add(BuildModernDialogField(
            "Tile 1",
            tile1Actions));
        Grid tile2Actions = NewEqualButtonGrid(2);
        AddModernGridButton(tile2Actions, _id65FreshAddTerrainTile2Button, 0);
        AddModernGridButton(tile2Actions, _id65FreshRemoveTerrainTile2Button, 1);
        terrainSetControls.Children.Add(BuildModernDialogField(
            "Tile 2",
            tile2Actions));
        _id65FreshTerrainSetControlsPanel = new Border
        {
            Name = "Id65FreshTerrainSetControlsPanel",
            IsVisible = false,
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(204, 216, 228)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(9),
            Child = terrainSetControls
        };
        panel.Children.Add(_id65FreshTerrainSetControlsPanel);

        _id65FreshOptionalTerrainStatusText.Name =
            "Id65FreshOptionalTerrainStatusText";
        _id65FreshOptionalTerrainStatusText.TextWrapping = TextWrapping.Wrap;
        _id65FreshOptionalTerrainStatusText.Foreground =
            new SolidColorBrush(Color.FromRgb(34, 71, 106));
        _id65FreshOptionalTerrainStatusText.FontSize = 12;
        _id65FreshOptionalTerrainStatusText.LineHeight = 18;
        panel.Children.Add(_id65FreshOptionalTerrainStatusText);

        _id65FreshOptionalTerrainPanel = new Border
        {
            Name = "Id65FreshOptionalTerrainPanel",
            IsVisible = false,
            Background = new SolidColorBrush(Color.FromRgb(241, 246, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(185, 205, 224)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = panel
        };
        RefreshId65FreshOptionalTerrainUi();
        return _id65FreshOptionalTerrainPanel;
    }

    private Control BuildId65LevelBuildingObjectManager()
    {
        StackPanel panel = new() { Spacing = 12 };
        panel.Children.Add(BuildNativeMobyLibraryButton("Id65NativeMobyLibraryButton", allowExistingTemplateSelection: false));
        panel.Children.Add(ModernSectionHeading("Verified Moby bundles"));
        panel.Children.Add(new TextBlock
        {
            Text =
                "This Object Manager belongs only to fresh blank levels. It uses the canonical Full Authoring Document so terrain support, object identity, and placement remain one deterministic project state. Existing retail levels keep their normal Objects editor.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ModernMutedInk),
            FontSize = 12,
            LineHeight = 18
        });

        StackPanel mobyChoiceControls = new() { Spacing = 8 };
        mobyChoiceControls.Children.Add(new TextBlock
        {
            Text = "Schema-4/5 remote-pad Moby choice",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(42, 61, 79))
        });
        mobyChoiceControls.Children.Add(new TextBlock
        {
            Text =
                "Choose exactly None, Grass, or Red Gem and one retained remote-pad position, then Apply Choice. Canonical schema-4 and schema-5 documents permit at most one visible placement. Changing either selector alone never saves or changes the viewport or terrain.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            LineHeight = 18
        });
        _id65FreshMobyChoiceSelector = new ComboBox
        {
            Name = "Id65FreshMobyChoiceSelector",
            ItemsSource = Id65RemotePadMobyChoiceCatalog.Choices,
            SelectedItem = Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(
                Id65RemotePadMobyKind.None),
            DisplayMemberBinding = new Binding(
                nameof(Id65RemotePadMobyChoice.Label)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = false
        };
        _id65FreshMobyChoiceSelector.SelectionChanged += (_, _) =>
            RefreshId65FreshMobyChoiceActionAvailability();
        mobyChoiceControls.Children.Add(BuildModernDialogField(
            "Moby",
            _id65FreshMobyChoiceSelector));
        _id65FreshMobyChoicePositionSelector = new ComboBox
        {
            Name = "Id65FreshMobyChoicePositionSelector",
            ItemsSource = Id65RemotePadGrassT107Catalog.Positions,
            SelectedItem = Id65RemotePadGrassT107Catalog.DefaultPosition,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = false
        };
        _id65FreshMobyChoicePositionSelector.SelectionChanged += (_, _) =>
            RefreshId65FreshMobyChoiceActionAvailability();
        mobyChoiceControls.Children.Add(BuildModernDialogField(
            "Retained position",
            _id65FreshMobyChoicePositionSelector));
        _id65FreshApplyMobyChoiceButton = NewAsyncButton(
            "Apply Choice",
            SetId65FreshRemotePadMobyChoiceAsync,
            RefreshId65BlankLabUi);
        _id65FreshApplyMobyChoiceButton.Name =
            "Id65FreshApplyMobyChoiceButton";
        StyleModernPrimaryButton(
            _id65FreshApplyMobyChoiceButton,
            Color.FromRgb(117, 80, 158));
        mobyChoiceControls.Children.Add(_id65FreshApplyMobyChoiceButton);
        _id65FreshMobyChoiceControlsPanel = new Border
        {
            Name = "Id65FreshMobyChoiceControlsPanel",
            IsVisible = false,
            Background = new SolidColorBrush(Color.FromRgb(248, 245, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(207, 192, 220)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = mobyChoiceControls
        };
        panel.Children.Add(_id65FreshMobyChoiceControlsPanel);

        StackPanel grassControls = new() { Spacing = 8 };
        grassControls.Children.Add(new TextBlock
        {
            Text = "Grass T107 placement",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(42, 61, 79))
        });
        grassControls.Children.Add(new TextBlock
        {
            Text =
                "Choose one of the nine verified remote-pad positions, then add or move the one supported Grass bundle. Remove keeps the selected position dormant in the canonical document. Changing the selector alone never saves or moves anything.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            LineHeight = 18
        });
        _id65FreshGrassT107PositionSelector = new ComboBox
        {
            Name = "Id65FreshGrassT107PositionSelector",
            ItemsSource = Id65RemotePadGrassT107Catalog.Positions,
            SelectedItem = Id65RemotePadGrassT107Catalog.DefaultPosition,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = false
        };
        _id65FreshGrassT107PositionSelector.SelectionChanged += (_, _) =>
            RefreshId65FreshGrassT107ActionAvailability();
        grassControls.Children.Add(_id65FreshGrassT107PositionSelector);

        _id65FreshAddOrMoveGrassT107Button = NewAsyncButton(
            "Add / Move Grass",
            () => SetId65FreshRemotePadGrassAsync(includeGrassAtT107: true),
            RefreshId65BlankLabUi);
        _id65FreshAddOrMoveGrassT107Button.Name =
            "Id65FreshAddOrMoveGrassT107Button";
        _id65FreshRemoveGrassT107Button = NewAsyncButton(
            "Remove Grass",
            () => SetId65FreshRemotePadGrassAsync(includeGrassAtT107: false),
            RefreshId65BlankLabUi);
        _id65FreshRemoveGrassT107Button.Name =
            "Id65FreshRemoveGrassT107Button";
        StyleModernPrimaryButton(
            _id65FreshAddOrMoveGrassT107Button,
            Color.FromRgb(117, 80, 158));
        StyleModernSecondaryButton(_id65FreshRemoveGrassT107Button);
        Grid grassActions = NewEqualButtonGrid(2);
        AddModernGridButton(
            grassActions,
            _id65FreshAddOrMoveGrassT107Button,
            0);
        AddModernGridButton(
            grassActions,
            _id65FreshRemoveGrassT107Button,
            1);
        grassControls.Children.Add(grassActions);
        _id65FreshSchema3GrassControlsPanel = new Border
        {
            Name = "Id65FreshSchema3GrassControlsPanel",
            Background = new SolidColorBrush(Color.FromRgb(248, 245, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(207, 192, 220)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = grassControls
        };
        panel.Children.Add(_id65FreshSchema3GrassControlsPanel);

        _id65FreshObjectManagerStatusText.Name =
            "Id65FreshObjectManagerStatusText";
        _id65FreshObjectManagerStatusText.TextWrapping = TextWrapping.Wrap;
        _id65FreshObjectManagerStatusText.Foreground =
            new SolidColorBrush(Color.FromRgb(34, 71, 106));
        _id65FreshObjectManagerStatusText.FontSize = 12;
        _id65FreshObjectManagerStatusText.LineHeight = 18;
        panel.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(241, 246, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(185, 205, 224)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = _id65FreshObjectManagerStatusText
        });

        _id65FreshObjectManagerDocumentButton = NewAsyncButton(
            "Edit Verified Moby Bundles…",
            EditId65AuthoringDocumentAsync,
            RefreshId65BlankLabUi);
        _id65FreshObjectManagerDocumentButton.Name =
            "Id65FreshObjectManagerDocumentButton";
        StyleModernPrimaryButton(
            _id65FreshObjectManagerDocumentButton,
            Color.FromRgb(117, 80, 158));
        panel.Children.Add(_id65FreshObjectManagerDocumentButton);

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
                    "Current schema-4/5 boundary: one canonical None/Grass T107/Red Gem T21 choice at one of nine fixed positions on collision T13995, with T66 fixed and T88 omitted. Schema 5 additionally exposes two independent terrain tiles and can build a separately guarded exact offline candidate. Schema 4 remains canonical-document/static-presentation only. Arbitrary Mobys, drag placement, normal Create BIN, runtime proof, and release promotion remain locked. Older schema-3 Grass and schema-4 single-terrain documents retain their prior routes until explicitly migrated.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(107, 70, 17)),
                FontSize = 11,
                LineHeight = 16
            }
        });

        RefreshId65FreshObjectManagerUi();
        return panel;
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
        ClearId65BlankLabRevealBinding();
        ResetId65AuthoringCandidateButtonLabel();
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
        RefreshLevelSelectionAvailability();
        try
        {
            Func<
                UnusedLevel65BlankLevelLabWorkspacePaths,
                CancellationToken,
                Task<UnusedLevel65BlankLevelLabManifest>> validator =
                Id65BlankLabCatalogValidationOverrideForTesting ??
                UnusedLevel65BlankLevelLabBootstrapper
                    .ValidatePublishedWorkspaceReadOnlyAsync;
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
                RefreshLevelSelectionAvailability();
            }
            cancellation.Dispose();
        }
    }

    private void BeginId65BlankLabCatalogValidationInBackground()
    {
        if (_id65BlankLabCatalogValidationLifetimeEnded)
            return;

        _id65BlankLabCatalogValidationTask =
            ObserveId65BlankLabCatalogValidationAsync();
    }

    private async Task ObserveId65BlankLabCatalogValidationAsync()
    {
        try
        {
            await ValidateAndAdmitId65BlankLabCatalogAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            EditorDiagnostics.RecordException(
                "observing deferred ID65 lab validation",
                ex);
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

    internal Task CurrentId65BlankLabCatalogValidationTaskForTesting =>
        _id65BlankLabCatalogValidationTask;

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

        // A prior T66 candidate is bound to an older canonical document and must
        // never remain revealable across any load/reload or malformed-profile
        // reinspection attempt.
        ClearId65BlankLabRevealBinding();
        RefreshId65BlankLabUi();

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
            Id65AuthoringEditorControlState? loadedAuthoringState =
                _id65FreshAuthoringDocument == null
                    ? null
                    : Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                        _id65FreshAuthoringDocument);
            string loadedMobySummary = _currentMobys.Count == 0
                ? "no visible document-bound Mobys"
                : _currentMobys.Count == 1
                    ? $"one read-only document-bound Moby ({_currentMobys[0].DisplayLabel})"
                    : $"{_currentMobys.Count} document-bound Mobys";
            _statusText.Text =
                $"Loaded {lab.DisplayName} as a fresh starter scene: the mandatory HP/LP pad with matching collision/occlusion, " +
                $"{Id65MandatoryBaseTextureLabel(loadedAuthoringState?.MandatoryBaseFaceTextureId ?? Id65FreshLevelSceneProjector.StarterTextureId)}, protected spawn, and {loadedMobySummary}" +
                (loadedAuthoringState == null
                    ? ". No Full Authoring Document exists, so no defaults were persisted and optional terrain is absent"
                    : loadedAuthoringState.HasAnyStructuralTerrain
                        ? ", plus optional document-bound structural terrain"
                        : "; the Full Authoring Document currently omits optional structural terrain") +
                ". The Town Square-derived locked base remains evidence only.";
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

    private async Task EditId65AuthoringDocumentAsync()
    {
        if (_id65BlankLabPaths == null ||
            _id65BlankLabManifest == null)
        {
            _statusText.Text =
                "Build and validate the ID65 lab before editing its Full Authoring Document.";
            return;
        }
        if (IsCurrentId65BlankLab() &&
            (HasUnsavedTerrainEdits() || HasUnsavedMobyEdits() ||
             HasUnsavedNativeMovementEdits() || HasUnsavedDragonRunToEdits()))
        {
            _statusText.Text =
                "Save, undo, or reload the current scene edits before opening the ID65 Full Authoring Document. The document does not translate unsaved legacy scene mutations.";
            return;
        }
        if (!TryBeginId65BlankLabManualOperation(
                "full-authoring document edit",
                out Id65BlankLabManualOperation? operation) ||
            operation == null)
        {
            return;
        }

        UnusedLevel65BlankLevelLabWorkspacePaths? recoveryPaths = null;
        UnusedLevel65BlankLevelLabManifest? recoveryManifest = null;
        Id65AuthoringEditorDocumentWriteResult? durableWrite = null;
        bool noWriteProjectionApplyStarted = false;

        try
        {
            await AwaitId65BlankLabManualOperationDelayForTestingAsync(
                operation,
                "authoring-document-started");
            RequireCurrentId65BlankLabManualOperation(operation);
            UnusedLevel65BlankLevelLabWorkspacePaths paths =
                UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
                    operation.WorkspaceRoot);
            UnusedLevel65BlankLevelLabManifest manifest =
                _id65BlankLabManifest ?? throw new InvalidDataException(
                    "The ID65 Full Authoring Document has no admitted locked-workspace manifest.");
            UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(
                manifest,
                paths);
            recoveryPaths = paths;
            recoveryManifest = manifest;

            _statusText.Text =
                "ID65 Full Authoring Document: validating the existing canonical document…";
            Id65AuthoringEditorDocumentReadModel? existing =
                await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                    paths,
                    manifest,
                    operation.Cancellation.Token);
            RequireCurrentId65BlankLabManualOperation(operation);
            Id65AuthoringEditorControlState initial = existing == null
                ? Id65AuthoringEditorControlState.CreateRemotePad(
                    includeStructuralTerrain: false,
                    includeGrassAtT88: false,
                    includeGrassAtT107: false,
                    Id65RemotePadGrassT107Catalog.DefaultPositionId,
                    Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId)
                : Id65AuthoringEditorControlStateCompiler.RecoverControlState(existing);

            Id65AuthoringEditorControlState? selected =
                Id65AuthoringDocumentDialogOverrideForTesting != null
                    ? await Id65AuthoringDocumentDialogOverrideForTesting(
                            initial,
                            existing != null,
                            operation.Cancellation.Token)
                        .WaitAsync(operation.Cancellation.Token)
                    : existing == null
                        ? await ShowId65FreshStartDialogAsync()
                        : await ShowId65AuthoringDocumentDialogAsync(
                            initial,
                            replacingExistingDocument: true);
            RequireCurrentId65BlankLabManualOperation(operation);
            if (selected == null)
            {
                _statusText.Text =
                    "ID65 Full Authoring Document was closed without saving.";
                return;
            }
            if (existing != null && Id65AuthoringControlStatesEqual(initial, selected))
            {
                _statusText.Text =
                    "ID65 Full Authoring Document: confirming the canonical envelope is still current…";
                Id65AuthoringEditorDocumentReadModel? current =
                    await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                        paths,
                        manifest,
                        operation.Cancellation.Token);
                RequireCurrentId65BlankLabManualOperation(operation);
                if (current == null ||
                    current.EnvelopeCanonicalSha256 !=
                        existing.EnvelopeCanonicalSha256 ||
                    current.DocumentCanonicalSha256 !=
                        existing.DocumentCanonicalSha256 ||
                    !Id65AuthoringControlStatesEqual(
                        Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                            current),
                        initial))
                {
                    throw new InvalidDataException(
                        "The canonical ID65 authoring document changed while the dialog was open. Reopen it before saving or accepting an unchanged state.");
                }
                bool staleLoadedProjection =
                    IsCurrentId65BlankLab() &&
                    (!_id65FreshDocumentProjectionSynchronized ||
                     !Id65AuthoringDocumentSnapshotsEqual(
                         _id65FreshAuthoringDocument,
                         current));
                if (staleLoadedProjection)
                {
                    _statusText.Text =
                        "ID65 Full Authoring Document is unchanged on disk; refreshing the stale loaded projection…";
                    LevelLoadData? stalePreparedLevel =
                        await PrepareId65FreshProjectionAsync(
                            current,
                            initial,
                            operation,
                            requireSceneIdentity: true);
                    RequireCurrentId65BlankLabManualOperation(operation);
                    noWriteProjectionApplyStarted = true;
                    ApplyPreparedId65FreshProjection(
                        operation,
                        stalePreparedLevel,
                        invokePostApplyFaultSeam: false);
                    _statusText.Text =
                        $"ID65 Full Authoring Document already matches the selected controls ({current.DocumentCanonicalSha256[..12]}…). No file was changed; the stale viewport was re-inspected and replaced with this exact document projection.";
                }
                else
                {
                    _statusText.Text =
                        $"ID65 Full Authoring Document already matches the selected controls ({current.DocumentCanonicalSha256[..12]}…). No file was changed.";
                }
                return;
            }
            if (IsCurrentId65BlankLab() &&
                (HasUnsavedTerrainEdits() || HasUnsavedMobyEdits() ||
                 HasUnsavedNativeMovementEdits() || HasUnsavedDragonRunToEdits()))
            {
                throw new OperationCanceledException(
                    "The loaded scene changed while the ID65 authoring dialog was open.",
                    operation.Cancellation.Token);
            }

            bool explicitLegacyToRemotePadMigration =
                existing != null &&
                !initial.UsesRemotePadT107Grid &&
                selected.UsesRemotePadT107Grid;
            bool explicitRemotePadMobyChoiceMigration =
                existing != null &&
                initial.UsesRemotePadT107Grid &&
                selected.UsesRemotePadMobyChoice &&
                !selected.UsesRemotePadTerrainSet;
            bool explicitRemotePadTerrainSetMigration =
                existing != null &&
                initial.UsesRemotePadMobyChoice &&
                !initial.UsesRemotePadTerrainSet &&
                selected.UsesRemotePadTerrainSet;
            bool explicitBlankCycloramaMigration =
                existing != null &&
                initial.UsesRemotePadTerrainSet &&
                !initial.UsesBlankCyclorama &&
                selected.UsesBlankCyclorama;
            if ((initial.UsesBlankCyclorama && !selected.UsesBlankCyclorama) ||
                (selected.UsesBlankCyclorama &&
                    !initial.UsesBlankCyclorama &&
                    !explicitBlankCycloramaMigration))
            {
                throw new InvalidDataException(
                    "Blank Slate schema 6 is an explicit, one-way schema-5 migration. It is never inferred or downgraded by ordinary document edits.");
            }
            _statusText.Text = selected.UsesBlankCyclorama
                ? explicitBlankCycloramaMigration
                    ? "ID65 Full Authoring Document: revalidating the exact schema-5 document before explicit Blank Slate schema-6 migration (blank-slate-v1, #263447)…"
                    : "ID65 Full Authoring Document: validating the Blank Slate schema-6 projection (blank-slate-v1, #263447) and publishing with compare-and-swap…"
                : selected.UsesRemotePadTerrainSet
                ? explicitRemotePadTerrainSetMigration
                    ? "ID65 Full Authoring Document: revalidating the exact schema-4 document before explicit schema-5 two-tile terrain-set migration…"
                    : "ID65 Full Authoring Document: validating the schema-5 static terrain-set projection and publishing with compare-and-swap…"
                : selected.UsesRemotePadMobyChoice
                ? explicitRemotePadMobyChoiceMigration
                    ? "ID65 Full Authoring Document: revalidating the exact schema-3 T66/T88-off document before explicit schema-4 None/Grass migration…"
                    : "ID65 Full Authoring Document: validating the schema-4 static-authoring projection and publishing with compare-and-swap…"
                : selected.UsesRemotePadT107Grid
                ? explicitLegacyToRemotePadMigration
                    ? "ID65 Full Authoring Document: revalidating the exact prior legacy document and provider-owned native inputs before explicit remote-pad migration…"
                    : "ID65 Full Authoring Document: validating the schema-3 static-authoring projection and publishing with compare-and-swap…"
                : "ID65 Full Authoring Document: revalidating native inputs and publishing with compare-and-swap…";
            Id65AuthoringEditorControlStateCompilation selectedCompilation =
                Id65AuthoringEditorControlStateCompiler.Compile(selected);
            Id65AuthoringEditorDocumentWriteResult written =
                explicitBlankCycloramaMigration
                    ? await Id65AuthoringEditorDocumentWorkspaceWriter
                        .MigrateRemotePadMobyChoiceTerrainSetToBlankCycloramaExactAsync(
                            paths,
                            manifest,
                            selected,
                            existing!.EnvelopeCanonicalSha256,
                            operation.Cancellation.Token)
                    : explicitRemotePadTerrainSetMigration
                    ? await Id65AuthoringEditorDocumentWorkspaceWriter
                        .MigrateRemotePadMobyChoiceToTerrainSetExactAsync(
                            paths,
                            manifest,
                            selected,
                            existing!.EnvelopeCanonicalSha256,
                            operation.Cancellation.Token)
                    : explicitRemotePadMobyChoiceMigration
                    ? await Id65AuthoringEditorDocumentWorkspaceWriter
                        .MigrateRemotePadGridToMobyChoiceExactAsync(
                            paths,
                            manifest,
                            selected,
                            existing!.EnvelopeCanonicalSha256,
                            operation.Cancellation.Token)
                    : explicitLegacyToRemotePadMigration
                    ? await Id65AuthoringEditorDocumentWorkspaceWriter
                        .MigrateLegacyToRemotePadGridExactAsync(
                            paths,
                            manifest,
                            selected,
                            existing!.EnvelopeCanonicalSha256,
                            operation.Cancellation.Token)
                    : await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
                        paths,
                        manifest,
                        selected,
                        existing?.EnvelopeCanonicalSha256,
                        operation.Cancellation.Token);
            durableWrite = written;
            RequireCurrentId65BlankLabManualOperation(operation);
            RequireExactId65AuthoringDocumentWriteResult(
                written,
                selectedCompilation,
                expectedExplicitLegacyToRemotePadMigration:
                    explicitLegacyToRemotePadMigration,
                expectedExplicitRemotePadMobyChoiceMigration:
                    explicitRemotePadMobyChoiceMigration,
                expectedExplicitRemotePadTerrainSetMigration:
                    explicitRemotePadTerrainSetMigration,
                expectedExplicitBlankCycloramaMigration:
                    explicitBlankCycloramaMigration);
            ClearId65BlankLabRevealBinding();
            _statusText.Text =
                "ID65 Full Authoring Document: re-inspecting the durable document and preparing its exact fresh-scene projection…";
            (_, LevelLoadData? preparedLevel) =
                await ReinspectAndPrepareId65FreshProjectionAsync(
                    paths,
                    manifest,
                    written,
                    selected,
                    operation);
            RequireCurrentId65BlankLabManualOperation(operation);
            ApplyPreparedId65FreshProjection(operation, preparedLevel);

            ClearId65BlankLabRevealBinding();
            InvalidateBuildSafetySummary();
            RefreshId65BlankLabUi();
            RefreshActionAvailability();
            _statusText.Text =
                $"{(written.ExplicitLegacyToRemotePadMigrationPerformed ||
                    written.ExplicitRemotePadMobyChoiceMigrationPerformed ||
                    written.ExplicitRemotePadTerrainSetMigrationPerformed ||
                    written.ExplicitBlankCycloramaMigrationPerformed
                    ? "Migrated"
                    : written.CreatedNewDocument ? "Created" : "Updated")} the canonical ID65 Full Authoring Document " +
                $"({written.DocumentCanonicalSha256[..12]}…): " +
                (written.ControlState.UsesRemotePadTerrainSet
                    ? $"Terrain Tile 1 {(written.ControlState.IncludeTerrainTile1 ? "included" : "omitted")}; Terrain Tile 2 {(written.ControlState.IncludeTerrainTile2 ? "included" : "omitted")}; "
                    : $"structural terrain {(written.ControlState.IncludeStructuralTerrain ? "included" : "omitted")}; ") +
                (written.ControlState.UsesRemotePadMobyChoice
                    ? $"schema-{(written.ControlState.UsesBlankCyclorama ? "6" : written.ControlState.UsesRemotePadTerrainSet ? "5" : "4")} Moby {Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(written.ControlState.RemotePadMobyKind!.Value).Label}; "
                    : $"Grass T88 {(written.ControlState.IncludeGrassAtT88 ? "included" : "omitted")}; Grass T107 {(written.ControlState.IncludeGrassAtT107 ? "included" : "omitted")}; ") +
                $"mandatory base face {Id65MandatoryBaseTextureLabel(written.ControlState.MandatoryBaseFaceTextureId)}. " +
                (written.ControlState.UsesRemotePadMobyChoice
                    ? $"Schema-{(written.ControlState.UsesBlankCyclorama ? "6" : written.ControlState.UsesRemotePadTerrainSet ? "5" : "4")} retained position {Id65RemotePadGrassT107Catalog.GetRequiredPosition(written.ControlState.RemotePadMobyPositionId!).Label}. " +
                      (written.ControlState.UsesBlankCyclorama
                          ? $"Blank Slate preset {written.ControlState.BlankCycloramaPresetId} with exact color {written.ControlState.BlankCycloramaColorHex} is preserved. "
                          : "")
                    : written.ControlState.UsesRemotePadT107Grid
                    ? $"Remote-pad T107 position {Id65RemotePadGrassT107Catalog.GetRequiredPosition(written.ControlState.T107RemotePadPositionId!).Label} is retained by schema 3. "
                    : "Legacy native-apron T107 semantics remain unchanged. ") +
                "The baseline spawn/music/totals/exit/save handles and every unrelated serialized pin remain fixed. " +
                (preparedLevel == null
                    ? "Load the exact ID65 Lab to view this canonical document projection. "
                    : "The loaded fresh scene was replaced only after exact post-save readback and projection. ") +
                (written.ControlState.UsesBlankCyclorama
                    ? "Blank Slate schema 6 is saved, statically projected, and eligible for the separately guarded exact offline schema-6 candidate. Normal Create BIN, runtime use, release integration, promotion, and publication remain blocked."
                    : written.ControlState.UsesRemotePadTerrainSet
                    ? "Schema 5 is saved, statically projected, and eligible for the separately guarded exact offline candidate. Normal Create BIN, runtime use, release integration, promotion, and publication remain blocked."
                    : written.ControlState.UsesRemotePadMobyChoice
                    ? "Schema 4 is saved and statically projected only. Physical materialization, exact candidate creation, normal Create BIN, runtime use, release integration, promotion, and publication remain blocked."
                    : written.ControlState.UsesRemotePadT107Grid
                    ? written.ControlState.MandatoryBaseFaceTextureId ==
                        Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId &&
                      !written.ControlState.IncludeGrassAtT88
                        ? "This exact schema-3 terrain/Grass combination can build the separately guarded offline blank-level candidate. Normal Create BIN, runtime proof, and release paths remain blocked pending focused physical acceptance."
                        : "This schema-3 state is saved and projected, but the exact candidate requires Proved private T66 and Grass T88 OFF; structural terrain and Grass T107 may each be on or off."
                    : written.ControlState.MandatoryBaseFaceTextureId ==
                    Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId
                    ? "Resident T25 is offline-authoring/viewport-only; exact candidate, Create BIN, runtime, and release paths are refused."
                    : "The explicit exact candidate action currently accepts only structural terrain with both Grass placements; normal Create BIN remains blocked pending runtime and atomic retail-composition proof.");
        }
        catch (OperationCanceledException exception)
        {
            if (durableWrite != null && recoveryPaths != null &&
                recoveryManifest != null &&
                IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: false))
            {
                _statusText.Text =
                    await RecoverId65PostWriteProjectionFailureAsync(
                        operation,
                        recoveryPaths,
                        recoveryManifest,
                        durableWrite,
                        exception,
                        "Full Authoring Document");
            }
            else if (noWriteProjectionApplyStarted && recoveryPaths != null &&
                     recoveryManifest != null &&
                     IsCurrentId65BlankLabManualOperation(
                         operation,
                         requireSceneIdentity: false))
            {
                _statusText.Text =
                    await RecoverId65NoWriteProjectionFailureAsync(
                        operation,
                        recoveryPaths,
                        recoveryManifest,
                        exception,
                        "unchanged Full Authoring Document");
            }
        }
        catch (Exception ex)
        {
            if (durableWrite != null && recoveryPaths != null &&
                recoveryManifest != null &&
                IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: false))
            {
                _statusText.Text =
                    await RecoverId65PostWriteProjectionFailureAsync(
                        operation,
                        recoveryPaths,
                        recoveryManifest,
                        durableWrite,
                        ex,
                        "Full Authoring Document");
            }
            else if (noWriteProjectionApplyStarted && recoveryPaths != null &&
                     recoveryManifest != null &&
                     IsCurrentId65BlankLabManualOperation(
                         operation,
                         requireSceneIdentity: false))
            {
                _statusText.Text =
                    await RecoverId65NoWriteProjectionFailureAsync(
                        operation,
                        recoveryPaths,
                        recoveryManifest,
                        ex,
                        "unchanged Full Authoring Document");
            }
            else if (IsCurrentId65BlankLabManualOperation(
                         operation,
                         requireSceneIdentity: false))
            {
                _statusText.Text =
                    $"Could not save the ID65 Full Authoring Document: {ex.Message} " +
                    "The prior viewport remains active. Reload the Lab if the canonical document was changed by another operation, then reopen the Full Authoring Document before retrying.";
            }
        }
        finally
        {
            CompleteId65BlankLabManualOperation(operation);
        }
    }

    private async Task CreateId65AuthoringAggregateCandidateAsync()
    {
        if (!OperatingSystem.IsMacOS())
        {
            _statusText.Text =
                "The exact ID65 aggregate candidate is currently available only on macOS because its atomic directory publication is Darwin-anchored.";
            return;
        }
        if (!IsCurrentId65BlankLab() || _id65BlankLabPaths == null ||
            _id65BlankLabManifest == null)
        {
            _statusText.Text =
                "Load the validated ID65 Blank-Level Lab before creating its exact full candidate.";
            return;
        }
        if (!Id65BlankLabHasCanonicalAuthoringDocument())
        {
            _statusText.Text =
                "Create the ID65 Full Authoring Document before creating its exact full candidate.";
            return;
        }
        ClearId65BlankLabRevealBinding();
        RefreshId65BlankLabUi();
        if (HasUnsavedTerrainEdits() || HasUnsavedMobyEdits() ||
            HasUnsavedNativeMovementEdits() || HasUnsavedDragonRunToEdits())
        {
            _statusText.Text =
                "Save, undo, or reload the current scene edits before creating the exact ID65 candidate. The candidate consumes only the canonical Full Authoring Document.";
            return;
        }
        if (TryGetId65BlankLabNonCanonicalUnsupportedArtifact(out string unsupported))
        {
            _statusText.Text =
                $"Could not create the exact ID65 candidate: {unsupported}";
            return;
        }
        if (!TryBeginId65BlankLabManualOperation(
                "exact aggregate candidate materialization",
                out Id65BlankLabManualOperation? operation) ||
            operation == null)
        {
            return;
        }

        object? idleButtonContent = _id65BlankLabAuthoringCandidateButton?.Content;
        if (_id65BlankLabAuthoringCandidateButton != null)
            _id65BlankLabAuthoringCandidateButton.Content = "Checking exact document…";
        try
        {
            await AwaitId65BlankLabManualOperationDelayForTestingAsync(
                operation,
                "authoring-candidate-started");
            RequireCurrentId65BlankLabManualOperation(operation);

            UnusedLevel65BlankLevelLabWorkspacePaths paths =
                UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
                    operation.WorkspaceRoot);
            UnusedLevel65BlankLevelLabManifest manifest =
                _id65BlankLabManifest ?? throw new InvalidDataException(
                    "The exact ID65 candidate lost its admitted workspace manifest.");
            if (_id65BlankLabPaths != paths)
            {
                throw new InvalidDataException(
                    "The exact ID65 candidate workspace paths changed after the operation fence was acquired.");
            }
            UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(
                manifest,
                paths);

            Id65AuthoringEditorDocumentReadModel document =
                await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                    paths,
                    manifest,
                    operation.Cancellation.Token) ?? throw new InvalidDataException(
                    "Create the canonical ID65 Full Authoring Document before materializing an exact candidate.");
            RequireCurrentId65BlankLabManualOperation(operation);
            if (!_id65FreshDocumentProjectionSynchronized ||
                !Id65AuthoringDocumentSnapshotsEqual(
                    _id65FreshAuthoringDocument,
                    document) ||
                _currentLevel == null ||
                !Id65FreshLevelSceneProjector.IsExactProjectionGeometry(
                    _currentGeometry) ||
                _loadedTerrainEdits != 0)
            {
                throw new InvalidDataException(
                    "The loaded fresh-scene viewport is not synchronized to the current exact canonical document. Load the ID65 Lab again before creating a candidate.");
            }
            Id65AuthoringEditorControlState state =
                Id65AuthoringEditorControlStateCompiler.RecoverControlState(document);
            if (state.UsesRemotePadMobyChoice &&
                !state.UsesRemotePadTerrainSet)
            {
                throw new InvalidDataException(
                    "Schema-4 None/Grass/Red Gem documents currently support canonical persistence and static fresh-scene projection only. Physical materialization, exact candidate creation, normal Create BIN, runtime use, release integration, promotion, and publication are refused.");
            }
            if (state.MandatoryBaseFaceTextureId ==
                Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId)
            {
                throw new InvalidDataException(
                    "Resident T25 is an offline-authoring/viewport-only mandatory base texture. Exact candidate materialization, Create BIN, runtime use, release integration, promotion, and publication are refused. Apply and save Proved private T66 before using the exact full-candidate path.");
            }
            if (state.MandatoryBaseFaceTextureId !=
                Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId)
            {
                throw new InvalidDataException(
                    "The mandatory base-face texture is not an exact supported candidate identity.");
            }
            Id65FreshLevelSceneProjection expectedProjection =
                Id65FreshLevelSceneProjector.ProjectExact(
                    _currentLevel,
                    document);
            GeometryCandidate loadedGeometry = _currentGeometry ??
                throw new InvalidDataException(
                    "The loaded fresh-scene geometry disappeared before the candidate graph check.");
            TerrainPolygon loadedMandatoryBaseFace = loadedGeometry.Polygons.Single(
                polygon => polygon.FaceIndex ==
                    Id65FreshLevelSceneProjector.AuthoredFaceIndex);
            if (expectedProjection.StructuralTerrainPresent !=
                    state.HasAnyStructuralTerrain ||
                expectedProjection.RemotePadMobyKind !=
                    state.RemotePadMobyKind ||
                document.MandatoryBaseFaceTextureId !=
                    state.MandatoryBaseFaceTextureId ||
                loadedMandatoryBaseFace.TextureId !=
                    state.MandatoryBaseFaceTextureId ||
                loadedMandatoryBaseFace.OriginalTextureId !=
                    state.MandatoryBaseFaceTextureId ||
                loadedMandatoryBaseFace.NativeTextureId !=
                    state.MandatoryBaseFaceTextureId ||
                loadedMandatoryBaseFace.NativeMaterialByte !=
                    (byte)state.MandatoryBaseFaceTextureId ||
                loadedMandatoryBaseFace.NativeFaceWord2 != 0x01400242u ||
                loadedGeometry.Polygons.Count !=
                    expectedProjection.Geometry.Polygons.Count ||
                loadedGeometry.LowDetailPolygons.Count !=
                    expectedProjection.Geometry.LowDetailPolygons.Count ||
                BuildTerrainEditSignature(loadedGeometry) !=
                    BuildTerrainEditSignature(expectedProjection.Geometry) ||
                !Id65PreparedMobysMatchProjection(
                    _currentMobys,
                    expectedProjection))
            {
                throw new InvalidDataException(
                    "The loaded fresh-scene graph does not match the exact canonical document projection. Load the ID65 Lab again before creating a candidate.");
            }
            bool exactRemotePadCandidateState =
                state.UsesRemotePadT107Grid &&
                !state.IncludeGrassAtT88;
            bool exactTerrainSetCandidateState =
                state.UsesRemotePadTerrainSet &&
                state.UsesRemotePadMobyChoice &&
                !state.UsesRemotePadT107Grid &&
                !state.IncludeGrassAtT88;
            bool exactLegacyCandidateState =
                !state.UsesRemotePadT107Grid &&
                !state.UsesRemotePadMobyChoice &&
                state.IncludeStructuralTerrain &&
                state.IncludeGrassAtT88 &&
                state.IncludeGrassAtT107;
            if (!exactTerrainSetCandidateState &&
                !exactRemotePadCandidateState &&
                !exactLegacyCandidateState)
            {
                throw new InvalidDataException(
                    state.UsesRemotePadT107Grid
                        ? "The schema-3 exact materializer requires Grass T88 OFF and Proved private T66. Structural terrain and Grass T107 may each be ON or OFF; save a supported document state, then try again."
                        : "The legacy exact materializer supports only the full document: enable structural terrain plus both proved Grass placements at T88 and T107, save the document, then try again.");
            }

            if (_id65BlankLabAuthoringCandidateButton != null)
                _id65BlankLabAuthoringCandidateButton.Content =
                    state.UsesBlankCyclorama
                        ? "Building Blank Slate schema-6 candidate…"
                        : "Building exact candidate…";
            _statusText.Text = state.UsesBlankCyclorama
                ? "Blank Slate schema-6 candidate: revalidating exact document CAS, preset blank-slate-v1, color #263447, locked base, WAD80-only destination, and protected retail sky blocks before atomic materialization…"
                : "ID65 candidate: revalidating the locked base, native texture/Moby inputs, and canonical document before atomic materialization…";
            Id65BlankLabAuthoringCandidateAppResult result =
                Id65AuthoringCandidateMaterializerOverrideForTesting != null
                    ? await Id65AuthoringCandidateMaterializerOverrideForTesting(
                            paths,
                            manifest,
                            operation.Cancellation.Token)
                        .WaitAsync(operation.Cancellation.Token)
                    : await MaterializeId65AuthoringCandidateForAppAsync(
                        paths,
                        manifest,
                        state,
                        document.EnvelopeCanonicalSha256,
                        operation.Cancellation.Token);
            RequireCurrentId65BlankLabManualOperation(operation);
            RequireExactId65AuthoringCandidateAppResult(
                paths,
                document,
                result);
            Id65AuthoringEditorDocumentReadModel currentDocument =
                await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                    paths,
                    manifest,
                    operation.Cancellation.Token) ?? throw new InvalidDataException(
                    "The canonical ID65 Full Authoring Document disappeared after candidate materialization.");
            RequireCurrentId65BlankLabManualOperation(operation);
            if (currentDocument.EnvelopeCanonicalSha256 !=
                    result.DocumentEnvelopeSha256 ||
                currentDocument.DocumentCanonicalSha256 !=
                    result.DocumentCanonicalSha256)
            {
                throw new InvalidDataException(
                    "The canonical ID65 Full Authoring Document changed after candidate materialization. Run the exact candidate action again for the current document.");
            }

            _id65BlankLabRevealPath = result.CandidateCuePath;
            _id65BlankLabRevealDocumentEnvelopeSha256 =
                result.DocumentEnvelopeSha256;
            _id65BlankLabRevealDocumentCanonicalSha256 =
                result.DocumentCanonicalSha256;
            RefreshId65BlankLabUi();
            string verb = result.ReusedExistingCandidate
                ? "Reused"
                : "Created";
            string terrainSetDescription = state.UsesRemotePadTerrainSet
                ? state.IncludeTerrainTile1 && state.IncludeTerrainTile2
                    ? "Terrain Tile 1 and Terrain Tile 2"
                    : state.IncludeTerrainTile1
                        ? "Terrain Tile 1 only"
                        : state.IncludeTerrainTile2
                            ? "Terrain Tile 2 only"
                            : "empty terrain set"
                : "";
            string terrainSetMobyDescription = state.UsesRemotePadTerrainSet
                ? state.RemotePadMobyKind == Id65RemotePadMobyKind.None
                    ? $"no visible Moby with dormant {Id65RemotePadGrassT107Catalog.GetRequiredPosition(state.RemotePadMobyPositionId!).Label} position retained"
                    : $"{Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(state.RemotePadMobyKind!.Value).Label} at {Id65RemotePadGrassT107Catalog.GetRequiredPosition(state.RemotePadMobyPositionId!).Label}"
                : "";
            _statusText.Text =
                $"{verb} the exact offline " +
                (state.UsesBlankCyclorama
                    ? "Blank Slate schema-6 candidate "
                    : "ID65 full-authoring candidate ") +
                $"{Path.GetFileName(result.CandidateCuePath)} " +
                $"(image {result.OutputImageSha256[..12]}…, plan {result.AggregatePlanSha256[..12]}…; " +
                $"{result.ChangedRawSectorCount} changed raw sectors). " +
                (state.UsesBlankCyclorama
                    ? $"Preset {result.BlankCycloramaPresetId} with exact color {result.BlankCycloramaColorHex}, all {result.PaletteWordCount} palette words, cyclorama {result.OutputCycloramaSha256[..12]}…, WAD80 placement/readback, terrain/Moby state, and protected WAD79 plus Town Square/Artisans sky blocks passed exact receipt validation. "
                    : state.UsesRemotePadTerrainSet
                    ? $"Its {terrainSetDescription} with matching collision/occlusion, private T66 texture, {terrainSetMobyDescription}, repaired T13995 support, and protected retail complement passed deterministic readback. "
                    : state.UsesRemotePadT107Grid
                    ? $"Its {(state.IncludeStructuralTerrain ? "structural terrain/collision/occlusion" : "blank terrain state")}, private T66 texture, " +
                      $"{(state.IncludeGrassAtT107 ? $"Grass T107 at {Id65RemotePadGrassT107Catalog.GetRequiredPosition(state.T107RemotePadPositionId!).Label}" : $"Grass T107 omitted with dormant {Id65RemotePadGrassT107Catalog.GetRequiredPosition(state.T107RemotePadPositionId!).Label} position retained")}, " +
                      "repaired T13995 support, and protected retail complement passed deterministic readback. "
                    : "Its structural terrain, private T66 texture, both Grass placements, and protected retail complement passed deterministic readback. ") +
                (state.UsesBlankCyclorama
                    ? "This hash-bound workspace snapshot is not normal Create BIN, runtime proof, or a release artifact yet; use Reveal Blank Slate schema-6 Test CUE for the focused DuckStation gate."
                    : "This hash-bound workspace snapshot is not normal Create BIN, runtime proof, or a release artifact yet; use Reveal Test CUE for the focused DuckStation gate.");
        }
        catch (OperationCanceledException) when (
            operation.Cancellation.IsCancellationRequested ||
            !IsCurrentId65BlankLabManualOperation(
                operation,
                requireSceneIdentity: true))
        {
        }
        catch (Exception exception)
        {
            if (IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: true))
            {
                bool blankSlate = false;
                if (_id65FreshAuthoringDocument != null)
                {
                    try
                    {
                        blankSlate = Id65AuthoringEditorControlStateCompiler
                            .RecoverControlState(_id65FreshAuthoringDocument)
                            .UsesBlankCyclorama;
                    }
                    catch (Exception stateException) when (
                        stateException is InvalidDataException or
                            InvalidOperationException)
                    {
                        Debug.WriteLine(stateException);
                    }
                }
                _statusText.Text = blankSlate
                    ? $"Could not create the exact Blank Slate schema-6 candidate: {exception.Message}"
                    : $"Could not create the exact ID65 full-authoring candidate: {exception.Message}";
            }
        }
        finally
        {
            if (IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: false) ||
                (!_id65BlankLabBusy && _id65BlankLabManualOperation == null))
            {
                if (_id65BlankLabAuthoringCandidateButton != null)
                {
                    _id65BlankLabAuthoringCandidateButton.Content =
                        idleButtonContent ?? "Create Exact Full Candidate";
                }
            }
            CompleteId65BlankLabManualOperation(operation);
        }
    }

    private static async Task<Id65BlankLabAuthoringCandidateAppResult>
        MaterializeId65AuthoringCandidateForAppAsync(
            UnusedLevel65BlankLevelLabWorkspacePaths paths,
            UnusedLevel65BlankLevelLabManifest manifest,
            Id65AuthoringEditorControlState state,
            string expectedEnvelopeCanonicalSha256,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.UsesBlankCyclorama)
        {
            Id65AuthoringBlankCycloramaAggregateCandidateWorkspaceResult
                blankSlate = await
                    Id65AuthoringBlankCycloramaAggregateCandidateWorkspaceMaterializer
                        .MaterializeExactAsync(
                            paths,
                            manifest,
                            expectedEnvelopeCanonicalSha256,
                            cancellationToken);
            return new(
                blankSlate.CandidateDirectoryPath,
                blankSlate.CandidateImagePath,
                blankSlate.CandidateCuePath,
                blankSlate.CandidateReceiptPath,
                blankSlate.DocumentEnvelopeSha256,
                blankSlate.DocumentCanonicalSha256,
                UsesRemotePadT107Grid: false,
                T107RemotePadPositionId: null,
                StructuralTerrainPresent:
                    blankSlate.ActiveTerrainUnitIds.Count != 0,
                GrassT107Present:
                    blankSlate.MobyKind == Id65RemotePadMobyKind.GrassT107,
                blankSlate.AggregatePlanSha256,
                blankSlate.OutputImageSha256,
                blankSlate.CandidateCueSha256,
                blankSlate.ReceiptContentSha256,
                blankSlate.ChangedRawSectorCount,
                blankSlate.ChangedPhysicalImageBytes,
                blankSlate.ReusedExistingCandidate,
                blankSlate.CandidateArtifactsWrittenThisCall,
                blankSlate.FileSystemMutationPerformed,
                blankSlate.ExactStoreReloadCasVerifiedThreeTimes &&
                    blankSlate.ExactEnvelopeCasVerifiedThreeTimes &&
                    blankSlate.ExactStaticAuthoringProjectionReloadVerifiedThreeTimes &&
                    blankSlate.ExactNativeInputGenerationRecapturedThreeTimes &&
                    blankSlate.ExactV3TemplateRecapturedThreeTimes &&
                    blankSlate.ExactLockedBaseRecapturedThreeTimes &&
                    blankSlate.ExactRow80ReadbackVerified &&
                    blankSlate.ExactInverseReadbackVerified &&
                    blankSlate.RawSectorIntegrityVerified &&
                    blankSlate.PhysicalComplementVerified,
                Schema3CompilerPinsVerified: false,
                T107PositionReadbackVerified: false,
                T88ExcludedVerified: blankSlate.T88ExcludedVerified,
                RepairedT13995SupportVerified:
                    blankSlate.RepairedT13995SupportVerified,
                blankSlate.ProtectedComplementVerified,
                RetailTownSquarePreserved:
                    blankSlate.RetailTownSquareWad15Preserved &&
                    blankSlate.RetailTownSquareWad16Preserved &&
                    blankSlate.RetailTownSquareSkyBlockPreserved,
                TargetOverlayPreserved: blankSlate.Wad79Preserved,
                blankSlate.ExecutablePreserved,
                blankSlate.AtomicNoReplacePublicationVerified,
                blankSlate.OwnedTemporaryResidueRemoved,
                blankSlate.CarriesAppOrUiAuthority,
                blankSlate.CarriesCreateBinAuthority,
                blankSlate.NormalRetailCreateBinComposed,
                blankSlate.RuntimeCandidateAuthorized,
                blankSlate.RuntimeProven,
                blankSlate.ReleaseIntegrated,
                blankSlate.PromotionAuthorized,
                blankSlate.Publishable,
                blankSlate.CurrentOnDiskFreshnessVerified,
                blankSlate.HostileFilesystemRaceSafe,
                blankSlate.RequiresTrustedExclusiveWorkspaceDirectory,
                blankSlate.CommonWorkspaceLeaseOwned,
                UsesRemotePadTerrainSet: true,
                ActiveTerrainUnitIdsCanonical:
                    string.Join('\n', blankSlate.ActiveTerrainUnitIds),
                RemotePadMobyKind: blankSlate.MobyKind,
                RemotePadMobyPositionId: blankSlate.PositionId,
                Schema5CompilerPinsVerified: false,
                MobyChoicePositionReadbackVerified:
                    blankSlate.MobyChoicePositionReadbackVerified,
                FourTerrainTopologiesVerified:
                    blankSlate.FourTerrainTopologiesVerified,
                UsesBlankCyclorama: true,
                BlankCycloramaPresetId: blankSlate.PresetId,
                BlankCycloramaColorHex: blankSlate.ColorHex,
                OutputCycloramaSha256: blankSlate.OutputCycloramaSha256,
                CycloramaGeometrySha256: blankSlate.GeometrySha256,
                CycloramaCommandByteSha256: blankSlate.CommandByteSha256,
                CycloramaNonPaletteSha256: blankSlate.NonPaletteSha256,
                CycloramaModelOffset: blankSlate.CycloramaModelOffset,
                CycloramaRow80Offset: blankSlate.CycloramaRow80Offset,
                CycloramaWadOffset: blankSlate.CycloramaWadOffset,
                CycloramaByteLength: blankSlate.CycloramaByteLength,
                PaletteWordCount: blankSlate.PaletteWordCount,
                PaletteOwnedRangeCount: blankSlate.PaletteOwnedRangeCount,
                ExactStoreReloadCasVerifiedThreeTimes:
                    blankSlate.ExactStoreReloadCasVerifiedThreeTimes,
                ExactEnvelopeCasVerifiedThreeTimes:
                    blankSlate.ExactEnvelopeCasVerifiedThreeTimes,
                ExactStaticAuthoringProjectionReloadVerifiedThreeTimes:
                    blankSlate.ExactStaticAuthoringProjectionReloadVerifiedThreeTimes,
                ExactNativeInputGenerationRecapturedThreeTimes:
                    blankSlate.ExactNativeInputGenerationRecapturedThreeTimes,
                ExactV3TemplateRecapturedThreeTimes:
                    blankSlate.ExactV3TemplateRecapturedThreeTimes,
                ExactLockedBaseRecapturedThreeTimes:
                    blankSlate.ExactLockedBaseRecapturedThreeTimes,
                ExactRow80ReadbackVerified:
                    blankSlate.ExactRow80ReadbackVerified,
                ExactInverseReadbackVerified:
                    blankSlate.ExactInverseReadbackVerified,
                Schema6CompilerPinsVerified:
                    blankSlate.Schema6CompilerPinsVerified,
                BlankCycloramaIntentReadbackVerified:
                    blankSlate.BlankCycloramaIntentReadbackVerified,
                CycloramaLayoutReadbackVerified:
                    blankSlate.CycloramaLayoutReadbackVerified,
                EveryPaletteWordReadbackVerified:
                    blankSlate.EveryPaletteWordReadbackVerified,
                CycloramaGeometryReadbackVerified:
                    blankSlate.CycloramaGeometryReadbackVerified,
                CycloramaCommandBytesReadbackVerified:
                    blankSlate.CycloramaCommandBytesReadbackVerified,
                CycloramaNonPaletteReadbackVerified:
                    blankSlate.CycloramaNonPaletteReadbackVerified,
                Wad79Preserved: blankSlate.Wad79Preserved,
                RetailTownSquareWad15Preserved:
                    blankSlate.RetailTownSquareWad15Preserved,
                RetailTownSquareWad16Preserved:
                    blankSlate.RetailTownSquareWad16Preserved,
                RetailArtisansWad9Preserved:
                    blankSlate.RetailArtisansWad9Preserved,
                RetailArtisansWad10Preserved:
                    blankSlate.RetailArtisansWad10Preserved,
                RetailTownSquareSkyBlockPreserved:
                    blankSlate.RetailTownSquareSkyBlockPreserved,
                RetailArtisansLinkedSkyBlockPreserved:
                    blankSlate.RetailArtisansLinkedSkyBlockPreserved);
        }
        if (state.UsesRemotePadTerrainSet)
        {
            Id65AuthoringRemotePadTerrainSetAggregateCandidateWorkspaceResult
                terrainSet = await
                    Id65AuthoringRemotePadTerrainSetAggregateCandidateWorkspaceMaterializer
                        .MaterializeExactAsync(
                            paths,
                            manifest,
                            expectedEnvelopeCanonicalSha256,
                            cancellationToken);
            return new(
                terrainSet.CandidateDirectoryPath,
                terrainSet.CandidateImagePath,
                terrainSet.CandidateCuePath,
                terrainSet.CandidateReceiptPath,
                terrainSet.DocumentEnvelopeSha256,
                terrainSet.DocumentCanonicalSha256,
                UsesRemotePadT107Grid: false,
                T107RemotePadPositionId: null,
                StructuralTerrainPresent:
                    terrainSet.ActiveTerrainUnitIds.Count != 0,
                GrassT107Present:
                    terrainSet.MobyKind == Id65RemotePadMobyKind.GrassT107,
                terrainSet.AggregatePlanSha256,
                terrainSet.OutputImageSha256,
                terrainSet.CandidateCueSha256,
                terrainSet.ReceiptContentSha256,
                terrainSet.ChangedRawSectorCount,
                terrainSet.ChangedPhysicalImageBytes,
                terrainSet.ReusedExistingCandidate,
                terrainSet.CandidateArtifactsWrittenThisCall,
                terrainSet.FileSystemMutationPerformed,
                terrainSet.ExactStoreReloadCasVerifiedThreeTimes &&
                    terrainSet.ExactEnvelopeCasVerifiedThreeTimes &&
                    terrainSet.ExactStaticAuthoringProjectionReloadVerifiedThreeTimes &&
                    terrainSet.ExactNativeInputGenerationRecapturedThreeTimes &&
                    terrainSet.ExactV3TemplateRecapturedThreeTimes &&
                    terrainSet.ExactLockedBaseRecapturedThreeTimes &&
                    terrainSet.ExactRow80ReadbackVerified &&
                    terrainSet.ExactInverseReadbackVerified &&
                    terrainSet.RawSectorIntegrityVerified &&
                    terrainSet.PhysicalComplementVerified,
                Schema3CompilerPinsVerified: false,
                T107PositionReadbackVerified: false,
                T88ExcludedVerified: terrainSet.T88ExcludedVerified,
                RepairedT13995SupportVerified:
                    terrainSet.RepairedT13995SupportVerified,
                terrainSet.ProtectedComplementVerified,
                terrainSet.RetailTownSquarePreserved,
                terrainSet.TargetOverlayPreserved,
                terrainSet.ExecutablePreserved,
                terrainSet.AtomicNoReplacePublicationVerified,
                terrainSet.OwnedTemporaryResidueRemoved,
                terrainSet.CarriesAppOrUiAuthority,
                terrainSet.CarriesCreateBinAuthority,
                terrainSet.NormalRetailCreateBinComposed,
                terrainSet.RuntimeCandidateAuthorized,
                terrainSet.RuntimeProven,
                terrainSet.ReleaseIntegrated,
                terrainSet.PromotionAuthorized,
                terrainSet.Publishable,
                terrainSet.CurrentOnDiskFreshnessVerified,
                terrainSet.HostileFilesystemRaceSafe,
                terrainSet.RequiresTrustedExclusiveWorkspaceDirectory,
                terrainSet.CommonWorkspaceLeaseOwned,
                UsesRemotePadTerrainSet: true,
                ActiveTerrainUnitIdsCanonical:
                    string.Join('\n', terrainSet.ActiveTerrainUnitIds),
                RemotePadMobyKind: terrainSet.MobyKind,
                RemotePadMobyPositionId: terrainSet.PositionId,
                Schema5CompilerPinsVerified:
                    terrainSet.Schema5CompilerPinsVerified,
                MobyChoicePositionReadbackVerified:
                    terrainSet.MobyChoicePositionReadbackVerified,
                FourTerrainTopologiesVerified:
                    terrainSet.FourTerrainTopologiesVerified);
        }
        if (state.UsesRemotePadMobyChoice)
        {
            throw new InvalidDataException(
                "Schema-4 physical candidate materialization remains unavailable.");
        }
        if (state.UsesRemotePadT107Grid)
        {
            Id65AuthoringRemotePadAggregateCandidateWorkspaceResult remote =
                await Id65AuthoringRemotePadAggregateCandidateWorkspaceMaterializer
                    .MaterializeExactAsync(paths, manifest, cancellationToken);
            return new(
                remote.CandidateDirectoryPath,
                remote.CandidateImagePath,
                remote.CandidateCuePath,
                remote.CandidateReceiptPath,
                remote.DocumentEnvelopeSha256,
                remote.DocumentCanonicalSha256,
                UsesRemotePadT107Grid: true,
                remote.PositionId,
                remote.StructuralTerrainPresent,
                remote.GrassT107Present,
                remote.AggregatePlanSha256,
                remote.OutputImageSha256,
                remote.CandidateCueSha256,
                remote.ReceiptContentSha256,
                remote.ChangedRawSectorCount,
                remote.ChangedPhysicalImageBytes,
                remote.ReusedExistingCandidate,
                remote.CandidateArtifactsWrittenThisCall,
                remote.FileSystemMutationPerformed,
                remote.ExactStoreReloadVerified &&
                    remote.ExactStaticAuthoringProjectionReloadVerified &&
                    remote.ExactNativeInputGenerationRecaptured &&
                    remote.ExactLockedBaseRecaptured &&
                    remote.ExactRow80ReadbackVerified &&
                    remote.ExactInverseReadbackVerified &&
                    remote.RawSectorIntegrityVerified &&
                    remote.PhysicalComplementVerified,
                remote.Schema3CompilerPinsVerified,
                remote.T107PositionReadbackVerified,
                remote.T88ExcludedVerified,
                remote.RepairedT13995SupportVerified,
                remote.ProtectedComplementVerified,
                remote.RetailTownSquarePreserved,
                remote.TargetOverlayPreserved,
                remote.ExecutablePreserved,
                remote.AtomicNoReplacePublicationVerified,
                remote.OwnedTemporaryResidueRemoved,
                remote.CarriesAppOrUiAuthority,
                remote.CarriesCreateBinAuthority,
                remote.NormalRetailCreateBinComposed,
                remote.RuntimeCandidateAuthorized,
                remote.RuntimeProven,
                remote.ReleaseIntegrated,
                remote.PromotionAuthorized,
                remote.Publishable,
                remote.CurrentOnDiskFreshnessVerified,
                remote.HostileFilesystemRaceSafe,
                remote.RequiresTrustedExclusiveWorkspaceDirectory,
                remote.CommonWorkspaceLeaseOwned);
        }

        Id65AuthoringEditorAggregateCandidateWorkspaceResult result =
            await Id65AuthoringEditorAggregateCandidateWorkspaceMaterializer
                .MaterializeExactAsync(paths, manifest, cancellationToken);
        return new(
            result.CandidateDirectoryPath,
            result.CandidateImagePath,
            result.CandidateCuePath,
            result.CandidateReceiptPath,
            result.DocumentEnvelopeSha256,
            result.DocumentCanonicalSha256,
            UsesRemotePadT107Grid: false,
            T107RemotePadPositionId: null,
            StructuralTerrainPresent: true,
            GrassT107Present: true,
            result.AggregatePlanSha256,
            result.OutputImageSha256,
            result.CandidateCueSha256,
            result.ReceiptContentSha256,
            result.ChangedRawSectorCount,
            result.ChangedPhysicalImageBytes,
            result.ReusedExistingCandidate,
            result.CandidateArtifactsWrittenThisCall,
            result.FileSystemMutationPerformed,
            result.ExactStoreReloadVerified &&
                result.ExactLockedBaseRecaptured &&
                result.ExactRow80ReadbackVerified &&
                result.ExactInverseReadbackVerified &&
                result.RawSectorIntegrityVerified &&
                result.PhysicalComplementVerified,
            Schema3CompilerPinsVerified: false,
            T107PositionReadbackVerified: false,
            T88ExcludedVerified: false,
            RepairedT13995SupportVerified: false,
            result.ProtectedComplementVerified,
            result.RetailTownSquarePreserved,
            result.TargetOverlayPreserved,
            result.ExecutablePreserved,
            result.AtomicNoReplacePublicationVerified,
            result.OwnedTemporaryResidueRemoved,
            result.CarriesAppOrUiAuthority,
            result.CarriesCreateBinAuthority,
            result.NormalRetailCreateBinComposed,
            result.RuntimeCandidateAuthorized,
            result.RuntimeProven,
            result.ReleaseIntegrated,
            result.PromotionAuthorized,
            result.Publishable,
            result.CurrentOnDiskFreshnessVerified,
            result.HostileFilesystemRaceSafe,
            result.RequiresTrustedExclusiveWorkspaceDirectory,
            result.CommonWorkspaceLeaseOwned);
    }

    private static void RequireExactId65AuthoringCandidateAppResult(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        Id65AuthoringEditorDocumentReadModel expectedDocument,
        Id65BlankLabAuthoringCandidateAppResult result)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(expectedDocument);
        ArgumentNullException.ThrowIfNull(result);
        Id65AuthoringEditorControlState expectedState =
            Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                expectedDocument);
        bool blankSlate = expectedState.UsesBlankCyclorama;
        bool terrainSet = expectedState.UsesRemotePadTerrainSet;
        bool remotePad = expectedState.UsesRemotePadT107Grid;
        bool schema6FieldsAbsent =
            !result.UsesBlankCyclorama &&
            result.BlankCycloramaPresetId == "" &&
            result.BlankCycloramaColorHex == "" &&
            result.OutputCycloramaSha256 == "" &&
            result.CycloramaGeometrySha256 == "" &&
            result.CycloramaCommandByteSha256 == "" &&
            result.CycloramaNonPaletteSha256 == "" &&
            result.CycloramaModelOffset == 0 &&
            result.CycloramaRow80Offset == 0 &&
            result.CycloramaWadOffset == 0 &&
            result.CycloramaByteLength == 0 &&
            result.PaletteWordCount == 0 &&
            result.PaletteOwnedRangeCount == 0 &&
            !result.ExactStoreReloadCasVerifiedThreeTimes &&
            !result.ExactEnvelopeCasVerifiedThreeTimes &&
            !result.ExactStaticAuthoringProjectionReloadVerifiedThreeTimes &&
            !result.ExactNativeInputGenerationRecapturedThreeTimes &&
            !result.ExactV3TemplateRecapturedThreeTimes &&
            !result.ExactLockedBaseRecapturedThreeTimes &&
            !result.ExactRow80ReadbackVerified &&
            !result.ExactInverseReadbackVerified &&
            !result.Schema6CompilerPinsVerified &&
            !result.BlankCycloramaIntentReadbackVerified &&
            !result.CycloramaLayoutReadbackVerified &&
            !result.EveryPaletteWordReadbackVerified &&
            !result.CycloramaGeometryReadbackVerified &&
            !result.CycloramaCommandBytesReadbackVerified &&
            !result.CycloramaNonPaletteReadbackVerified &&
            !result.Wad79Preserved &&
            !result.RetailTownSquareWad15Preserved &&
            !result.RetailTownSquareWad16Preserved &&
            !result.RetailArtisansWad9Preserved &&
            !result.RetailArtisansWad10Preserved &&
            !result.RetailTownSquareSkyBlockPreserved &&
            !result.RetailArtisansLinkedSkyBlockPreserved;
        bool schema5FieldsAbsent =
            !result.UsesRemotePadTerrainSet &&
            result.ActiveTerrainUnitIdsCanonical == "" &&
            result.RemotePadMobyKind == null &&
            result.RemotePadMobyPositionId == null &&
            !result.Schema5CompilerPinsVerified &&
            !result.MobyChoicePositionReadbackVerified &&
            !result.FourTerrainTopologiesVerified &&
            schema6FieldsAbsent;
        int expectedCycloramaModelOffset =
            expectedDocument.ActiveTerrainUnitIds.Count switch
            {
                0 => 0x8B9D4,
                1 => 0x8BA04,
                2 => 0x8BA30,
                _ => -1
            };
        int expectedCycloramaRow80Offset =
            expectedCycloramaModelOffset < 0
                ? -1
                : expectedCycloramaModelOffset + 0xDE800;
        bool expectedDocumentShape = blankSlate
            ? expectedDocument.ReadModelSchemaVersion ==
                Id65AuthoringEditorDocumentReadModel
                    .RemotePadMobyChoiceTerrainSetBlankCycloramaReadModelSchemaVersion &&
              expectedState.UsesRemotePadTerrainSet &&
              expectedState.UsesRemotePadMobyChoice &&
              !expectedState.UsesRemotePadT107Grid &&
              expectedState.MandatoryBaseFaceTextureId ==
                Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId &&
              !expectedState.IncludeGrassAtT88 &&
              expectedState.BlankCycloramaPresetId ==
                Id65BlankSlatePresetId &&
              expectedState.BlankCycloramaColorHex ==
                Id65BlankSlateColorHex &&
              expectedDocument.BlankCycloramaPresetId ==
                Id65BlankSlatePresetId &&
              expectedDocument.BlankCycloramaColorHex ==
                Id65BlankSlateColorHex &&
              result.UsesBlankCyclorama &&
              result.UsesRemotePadTerrainSet &&
              !result.UsesRemotePadT107Grid &&
              result.T107RemotePadPositionId == null &&
              result.ActiveTerrainUnitIdsCanonical ==
                string.Join('\n', expectedDocument.ActiveTerrainUnitIds) &&
              result.RemotePadMobyKind == expectedState.RemotePadMobyKind &&
              result.RemotePadMobyPositionId ==
                expectedState.RemotePadMobyPositionId &&
              result.StructuralTerrainPresent ==
                expectedState.HasAnyStructuralTerrain &&
              result.GrassT107Present ==
                (expectedState.RemotePadMobyKind ==
                    Id65RemotePadMobyKind.GrassT107) &&
              !result.Schema3CompilerPinsVerified &&
              !result.Schema5CompilerPinsVerified &&
              !result.T107PositionReadbackVerified &&
              result.T88ExcludedVerified &&
              result.RepairedT13995SupportVerified &&
              result.MobyChoicePositionReadbackVerified &&
              result.FourTerrainTopologiesVerified &&
              result.BlankCycloramaPresetId ==
                Id65BlankSlatePresetId &&
              result.BlankCycloramaColorHex ==
                Id65BlankSlateColorHex &&
              result.CycloramaModelOffset == expectedCycloramaModelOffset &&
              result.CycloramaRow80Offset == expectedCycloramaRow80Offset &&
              result.CycloramaWadOffset ==
                0x6936800L + expectedCycloramaRow80Offset &&
              result.CycloramaByteLength == 0x84E4 &&
              result.PaletteWordCount == 727 &&
              result.PaletteOwnedRangeCount == 727 &&
              result.OutputCycloramaSha256 ==
                "A9DA1343EF6493CD46AB57D84AE78F27495AFFC564BCC9464396752914648AC2" &&
              result.CycloramaGeometrySha256 ==
                "405BACB0D5F058C8FF876E14C41FCD7C6C6A0C57535BD451FFA49C5FB82E4B9A" &&
              result.CycloramaCommandByteSha256 ==
                "27fb27c2dc1670d5cc3c0a05f406417d7ea033682b99ab5c4d343017b94a524d" &&
              IsSha256(result.CycloramaNonPaletteSha256) &&
              result.ExactStoreReloadCasVerifiedThreeTimes &&
              result.ExactEnvelopeCasVerifiedThreeTimes &&
              result.ExactStaticAuthoringProjectionReloadVerifiedThreeTimes &&
              result.ExactNativeInputGenerationRecapturedThreeTimes &&
              result.ExactV3TemplateRecapturedThreeTimes &&
              result.ExactLockedBaseRecapturedThreeTimes &&
              result.ExactRow80ReadbackVerified &&
              result.ExactInverseReadbackVerified &&
              result.Schema6CompilerPinsVerified &&
              result.BlankCycloramaIntentReadbackVerified &&
              result.CycloramaLayoutReadbackVerified &&
              result.EveryPaletteWordReadbackVerified &&
              result.CycloramaGeometryReadbackVerified &&
              result.CycloramaCommandBytesReadbackVerified &&
              result.CycloramaNonPaletteReadbackVerified &&
              result.Wad79Preserved &&
              result.RetailTownSquareWad15Preserved &&
              result.RetailTownSquareWad16Preserved &&
              result.RetailArtisansWad9Preserved &&
              result.RetailArtisansWad10Preserved &&
              result.RetailTownSquareSkyBlockPreserved &&
              result.RetailArtisansLinkedSkyBlockPreserved
            : terrainSet
            ? expectedDocument.ReadModelSchemaVersion ==
                Id65AuthoringEditorDocumentReadModel
                    .RemotePadMobyChoiceTerrainSetReadModelSchemaVersion &&
              expectedState.UsesRemotePadMobyChoice &&
              !expectedState.UsesRemotePadT107Grid &&
              expectedState.MandatoryBaseFaceTextureId ==
                Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId &&
              !expectedState.IncludeGrassAtT88 &&
              result.UsesRemotePadTerrainSet &&
              !result.UsesRemotePadT107Grid &&
              result.T107RemotePadPositionId == null &&
              result.ActiveTerrainUnitIdsCanonical ==
                string.Join('\n', expectedDocument.ActiveTerrainUnitIds) &&
              result.RemotePadMobyKind == expectedState.RemotePadMobyKind &&
              result.RemotePadMobyPositionId ==
                expectedState.RemotePadMobyPositionId &&
              result.StructuralTerrainPresent ==
                expectedState.HasAnyStructuralTerrain &&
              result.GrassT107Present ==
                (expectedState.RemotePadMobyKind ==
                    Id65RemotePadMobyKind.GrassT107) &&
              !result.Schema3CompilerPinsVerified &&
              !result.T107PositionReadbackVerified &&
              result.T88ExcludedVerified &&
              result.RepairedT13995SupportVerified &&
              result.Schema5CompilerPinsVerified &&
              result.MobyChoicePositionReadbackVerified &&
              result.FourTerrainTopologiesVerified &&
              schema6FieldsAbsent
            : remotePad
            ? expectedState.MandatoryBaseFaceTextureId ==
                Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId &&
              !expectedState.IncludeGrassAtT88 &&
              result.UsesRemotePadT107Grid &&
              result.T107RemotePadPositionId ==
                expectedState.T107RemotePadPositionId &&
              result.StructuralTerrainPresent ==
                expectedState.IncludeStructuralTerrain &&
              result.GrassT107Present == expectedState.IncludeGrassAtT107 &&
              result.Schema3CompilerPinsVerified &&
              result.T107PositionReadbackVerified &&
              result.T88ExcludedVerified &&
              result.RepairedT13995SupportVerified &&
              schema5FieldsAbsent
            : expectedDocument.SchemaVersion ==
                Id65LegacyT66DocumentSchemaVersion &&
              expectedDocument.ProfileId ==
                Id65LegacyT66DocumentProfileId &&
              expectedState.MandatoryBaseFaceTextureId ==
                Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId &&
              expectedState.IncludeStructuralTerrain &&
              expectedState.IncludeGrassAtT88 &&
              expectedState.IncludeGrassAtT107 &&
              !result.UsesRemotePadT107Grid &&
              result.T107RemotePadPositionId == null &&
              result.StructuralTerrainPresent &&
              result.GrassT107Present &&
              !result.Schema3CompilerPinsVerified &&
              !result.T107PositionReadbackVerified &&
              !result.T88ExcludedVerified &&
              !result.RepairedT13995SupportVerified &&
              schema5FieldsAbsent;
        string candidateRoot = Path.GetFullPath(Path.Combine(
            paths.RootPath,
            blankSlate
                ? "materialized-blank-cyclorama-schema6-candidates"
                : terrainSet
                ? "materialized-remote-pad-terrain-set-schema5-candidates"
                : remotePad
                ? "materialized-remote-pad-candidates"
                : "materialized-candidates"));
        string candidateDirectory = Path.GetFullPath(result.CandidateDirectoryPath);
        string expectedPrefix = candidateRoot.EndsWith(Path.DirectorySeparatorChar)
            ? candidateRoot
            : candidateRoot + Path.DirectorySeparatorChar;
        StringComparison pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        bool exactFiles =
            Path.GetFullPath(result.CandidateImagePath).StartsWith(
                candidateDirectory + Path.DirectorySeparatorChar,
                pathComparison) &&
            Path.GetFullPath(result.CandidateCuePath).StartsWith(
                candidateDirectory + Path.DirectorySeparatorChar,
                pathComparison) &&
            Path.GetFullPath(result.CandidateReceiptPath).StartsWith(
                candidateDirectory + Path.DirectorySeparatorChar,
                pathComparison) &&
            File.Exists(result.CandidateImagePath) &&
            File.Exists(result.CandidateCuePath) &&
            File.Exists(result.CandidateReceiptPath);
        if (!expectedDocumentShape ||
            !candidateDirectory.StartsWith(expectedPrefix, pathComparison) ||
            !exactFiles ||
            result.ReusedExistingCandidate ==
                result.CandidateArtifactsWrittenThisCall ||
            (result.CandidateArtifactsWrittenThisCall &&
                !result.FileSystemMutationPerformed) ||
            result.DocumentEnvelopeSha256 !=
                expectedDocument.EnvelopeCanonicalSha256 ||
            result.DocumentCanonicalSha256 !=
                expectedDocument.DocumentCanonicalSha256 ||
            !IsCanonicalLowerSha256(result.AggregatePlanSha256) ||
            !IsCanonicalLowerSha256(result.OutputImageSha256) ||
            !IsCanonicalLowerSha256(result.CandidateCueSha256) ||
            !IsCanonicalLowerSha256(result.ReceiptContentSha256) ||
            result.ChangedRawSectorCount <= 0 ||
            result.ChangedPhysicalImageBytes <= 0 ||
            !result.ExactReadbackVerified ||
            !result.ProtectedComplementVerified ||
            !result.RetailTownSquarePreserved ||
            !result.TargetOverlayPreserved ||
            !result.ExecutablePreserved ||
            !result.AtomicNoReplacePublicationVerified ||
            !result.OwnedTemporaryResidueRemoved ||
            result.CarriesAppOrUiAuthority ||
            result.CarriesCreateBinAuthority ||
            result.NormalRetailCreateBinComposed ||
            result.RuntimeCandidateAuthorized || result.RuntimeProven ||
            result.ReleaseIntegrated || result.PromotionAuthorized ||
            result.Publishable || result.CurrentOnDiskFreshnessVerified ||
            result.HostileFilesystemRaceSafe ||
            !result.RequiresTrustedExclusiveWorkspaceDirectory ||
            result.CommonWorkspaceLeaseOwned)
        {
            throw new InvalidDataException(
                "The ID65 candidate result lost an exact path, identity, readback, or offline safety boundary.");
        }
    }

    private static bool IsCanonicalLowerSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or
                >= 'a' and <= 'f' or
                >= 'A' and <= 'F');

    private async Task<Id65AuthoringEditorControlState?>
        ShowId65FreshStartDialogAsync()
    {
        Window dialog = new()
        {
            Title = "Start Blank-Level Editing",
            Width = 620,
            Height = 500,
            MinWidth = 520,
            MinHeight = 440,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Opened += (_, _) =>
        {
            dialog.Activate();
            dialog.Focus();
        };

        Id65AuthoringMandatoryBaseTextureChoice[] allTextureChoices =
            CreateId65AuthoringMandatoryBaseTextureChoices();
        Id65AuthoringMandatoryBaseTextureChoice[] startTextureChoices =
        [
            allTextureChoices.Single(choice =>
                choice.TextureId ==
                    Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId),
            allTextureChoices.Single(choice =>
                choice.TextureId ==
                    Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId)
        ];
        ComboBox mandatoryTexture = new()
        {
            Name = "Id65FreshStartMandatoryBaseTextureSelector",
            ItemsSource = startTextureChoices,
            SelectedIndex = -1,
            PlaceholderText = "Choose Proved private T66 or Resident T25",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        TextBlock summary = new()
        {
            Name = "Id65FreshStartSummaryText",
            Text =
                "The first document remains blank: Terrain Tile 1 OFF, Terrain Tile 2 OFF, Grass T88 OFF, no visible Moby, and retained position Center. Proved private T66 starts directly in schema 5 with the explicit None choice and empty terrain set; Resident T25 retains the schema-3 blank route because schema 5 is T66-only. " +
                "The mandatory starter pad, matching collision/occlusion, and protected spawn remain. Nothing is saved until Create is selected.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ModernMutedInk),
            FontSize = 12,
            LineHeight = 18
        };
        TextBlock preview = new()
        {
            Name = "Id65FreshStartPreviewText",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ModernMutedInk),
            FontSize = 11,
            LineHeight = 16
        };
        TextBlock validation = new()
        {
            Text = "Choose exactly one mandatory starter-pad texture.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ModernRed),
            IsVisible = false
        };

        StackPanel body = new()
        {
            Spacing = 12,
            Margin = new Thickness(18)
        };
        body.Children.Add(BuildModernDialogHeader(
            "Create the blank authoring document",
            "Choose the mandatory starter-pad texture explicitly. T66 creates the canonical schema-5 empty terrain set plus None Moby choice; T25 creates the canonical schema-3 blank choice. This action saves and synchronizes only the editor projection; the T66 document then enables the separate exact offline-candidate action, without claiming runtime proof."));
        body.Children.Add(BuildModernDialogField(
            "Mandatory starter-pad texture",
            mandatoryTexture));
        body.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(243, 247, 251)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(204, 216, 228)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = summary
        });
        body.Children.Add(preview);
        body.Children.Add(validation);

        Button cancel = NewButton("Cancel");
        Button create = NewButton("Create Blank-Level Document");
        create.Name = "Id65FreshStartCreateButton";
        create.IsEnabled = false;
        StyleModernSecondaryButton(cancel);
        StyleModernPrimaryButton(create, Color.FromRgb(117, 80, 158), 205);

        Id65AuthoringEditorControlState BuildSelectedState()
        {
            if (mandatoryTexture.SelectedItem is not
                Id65AuthoringMandatoryBaseTextureChoice selectedChoice ||
                selectedChoice.TextureId is not
                    (Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId or
                     Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId))
            {
                throw new InvalidDataException(
                    "Choose exactly Proved private T66 or Resident T25 for the mandatory starter pad.");
            }

            return selectedChoice.TextureId ==
                    Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId
                ? Id65AuthoringEditorControlState
                    .CreateRemotePadMobyChoiceTerrainSet(
                    includeTerrainTile1: false,
                    includeTerrainTile2: false,
                    Id65RemotePadMobyKind.None,
                    Id65RemotePadMobyChoiceCatalog.DefaultPositionId)
                : Id65AuthoringEditorControlState.CreateRemotePad(
                    includeStructuralTerrain: false,
                    includeGrassAtT88: false,
                    includeGrassAtT107: false,
                    Id65RemotePadGrassT107Catalog.DefaultPositionId,
                    selectedChoice.TextureId);
        }

        void RefreshPreview()
        {
            if (mandatoryTexture.SelectedItem is not
                Id65AuthoringMandatoryBaseTextureChoice selectedChoice)
            {
                preview.Text =
                    "No texture is preselected and no document or viewport state has changed. Choose one mandatory texture to enable Create.";
                validation.IsVisible = false;
                create.IsEnabled = false;
                return;
            }

            try
            {
                Id65AuthoringEditorControlState selected = BuildSelectedState();
                Id65AuthoringEditorControlStateCompilation compilation =
                    Id65AuthoringEditorControlStateCompiler.Compile(selected);
                bool privateT66 = selectedChoice.TextureId ==
                    Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId;
                preview.Text =
                    $"Exact blank document {compilation.DocumentCanonicalSha256[..12]}…; mandatory starter-pad texture {selectedChoice.Label}; " +
                    "Terrain Tile 1 OFF; Terrain Tile 2 OFF; Grass T88 OFF; no visible Moby; retained position Center. " +
                    (privateT66
                        ? "This creates schema-5 None with an empty terrain set and opens both two-tile terrain controls and the None/Grass/Red Gem Object Manager after exact save/readback. The synchronized document can then build the separately guarded exact offline candidate; normal Create BIN, runtime proof, and release remain unavailable."
                        : "This creates the schema-3 T25 blank route. Resident T25 is viewport-authoring-only; exact candidate, normal Create BIN, runtime, and release remain unavailable.");
                validation.IsVisible = false;
                create.IsEnabled = true;
            }
            catch (InvalidDataException exception)
            {
                preview.Text = "This transient choice cannot form an exact blank document.";
                validation.Text = exception.Message;
                validation.IsVisible = true;
                create.IsEnabled = false;
            }
        }

        mandatoryTexture.SelectionChanged += (_, _) => RefreshPreview();
        cancel.Click += (_, _) =>
            dialog.Close((Id65AuthoringEditorControlState?)null);
        create.Click += (_, _) =>
        {
            try
            {
                Id65AuthoringEditorControlState selected = BuildSelectedState();
                _ = Id65AuthoringEditorControlStateCompiler.Compile(selected);
                validation.IsVisible = false;
                dialog.Close(selected);
            }
            catch (InvalidDataException exception)
            {
                validation.Text = exception.Message;
                validation.IsVisible = true;
                create.IsEnabled = false;
            }
        };
        RefreshPreview();
        dialog.Content = BuildModernDialogFrame(body, cancel, create);
        return await dialog.ShowDialog<Id65AuthoringEditorControlState?>(this);
    }

    private async Task<Id65AuthoringEditorControlState?>
        ShowId65AuthoringDocumentDialogAsync(
            Id65AuthoringEditorControlState initial,
            bool replacingExistingDocument)
    {
        Window dialog = new()
        {
            Title = "ID65 Full Authoring Document",
            Width = 700,
            Height = 720,
            MinWidth = 560,
            MinHeight = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Opened += (_, _) =>
        {
            dialog.Activate();
            dialog.Focus();
        };

        CheckBox terrain = new()
        {
            Name = "Id65AuthoringStructuralTerrainBox",
            Content = initial.UsesRemotePadTerrainSet
                ? "Include exact structural Terrain Tile 1"
                : "Include exact structural terrain intent",
            IsChecked = initial.IncludeStructuralTerrain
        };
        CheckBox terrainTile2 = new()
        {
            Name = "Id65AuthoringTerrainTile2Box",
            Content = "Include exact structural Terrain Tile 2",
            IsChecked = initial.IncludeTerrainTile2
        };
        Id65AuthoringEditorTerrainSelectionSemantics terrainSelectionSemantics =
            initial.TerrainSelectionSemantics;
        bool canOfferTerrainSetMigration =
            initial.UsesRemotePadMobyChoice &&
            !initial.UsesRemotePadTerrainSet;
        Button migrateTerrainSet = NewButton(
            "Migrate Schema 4 to Two-Tile Terrain Set");
        migrateTerrainSet.Name =
            "Id65AuthoringTerrainSetMigrationButton";
        StyleModernSecondaryButton(migrateTerrainSet);
        migrateTerrainSet.IsVisible = canOfferTerrainSetMigration;
        bool blankCycloramaSelected = initial.UsesBlankCyclorama;
        bool canOfferBlankCycloramaMigration =
            initial.UsesRemotePadTerrainSet &&
            !initial.UsesBlankCyclorama;
        Button migrateBlankCyclorama = NewButton(
            "Migrate Schema 5 to Blank Slate Schema 6");
        migrateBlankCyclorama.Name =
            "Id65AuthoringBlankCycloramaMigrationButton";
        StyleModernSecondaryButton(migrateBlankCyclorama);
        migrateBlankCyclorama.IsVisible = canOfferBlankCycloramaMigration;
        TextBlock blankCycloramaNotice = new()
        {
            Name = "Id65AuthoringBlankCycloramaNotice",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            LineHeight = 16,
            Foreground = new SolidColorBrush(Color.FromRgb(150, 94, 12))
        };
        CheckBox t88 = new()
        {
            Name = "Id65AuthoringGrassT88Box",
            Content = "Include proved Grass placement at T88",
            IsChecked = initial.IncludeGrassAtT88
        };
        CheckBox t107 = new()
        {
            Name = "Id65AuthoringGrassT107Box",
            Content = initial.UsesRemotePadT107Grid
                ? "Include verified Grass T107 on the fresh remote pad"
                : "Include historical proved Grass placement at T107",
            IsChecked = initial.IncludeGrassAtT107
        };
        Id65AuthoringEditorT107PlacementSemantics t107PlacementSemantics =
            initial.T107PlacementSemantics;
        Id65RemotePadGrassT107Position initialT107Position =
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                initial.UsesRemotePadMobyChoice
                    ? initial.RemotePadMobyPositionId!
                    : initial.T107RemotePadPositionId ??
                Id65RemotePadGrassT107Catalog.DefaultPositionId);
        ComboBox t107Position = new()
        {
            Name = "Id65AuthoringGrassT107RemotePadPositionSelector",
            ItemsSource = Id65RemotePadGrassT107Catalog.Positions,
            SelectedItem = initialT107Position,
            IsEnabled = initial.UsesRemotePadT107Grid,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        TextBlock t107SemanticsNotice = new()
        {
            Name = "Id65AuthoringGrassT107SemanticsNotice",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            LineHeight = 16
        };
        Button migrateT107 = NewButton(
            "Migrate T107 to Remote-Pad 3x3 Center");
        migrateT107.Name = "Id65AuthoringGrassT107MigrationButton";
        StyleModernSecondaryButton(migrateT107);
        migrateT107.IsVisible =
            initial.T107PlacementSemantics ==
                Id65AuthoringEditorT107PlacementSemantics.LegacyNativeApronV1;
        bool canOfferMobyChoiceMigration = initial.UsesRemotePadT107Grid;
        Button migrateMobyChoice = NewButton(
            "Migrate Schema 3 to None / Grass / Red Gem");
        migrateMobyChoice.Name =
            "Id65AuthoringRemotePadMobyChoiceMigrationButton";
        StyleModernSecondaryButton(migrateMobyChoice);
        migrateMobyChoice.IsVisible = canOfferMobyChoiceMigration;
        Id65RemotePadMobyChoice initialMobyChoice =
            Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(
                initial.UsesRemotePadMobyChoice
                    ? initial.RemotePadMobyKind!.Value
                    : initial.IncludeGrassAtT107
                        ? Id65RemotePadMobyKind.GrassT107
                        : Id65RemotePadMobyKind.None);
        ComboBox mobyChoice = new()
        {
            Name = "Id65AuthoringRemotePadMobyChoiceSelector",
            ItemsSource = Id65RemotePadMobyChoiceCatalog.Choices,
            SelectedItem = initialMobyChoice,
            DisplayMemberBinding = new Binding(
                nameof(Id65RemotePadMobyChoice.Label)),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        ComboBox mobyChoicePosition = new()
        {
            Name = "Id65AuthoringRemotePadMobyPositionSelector",
            ItemsSource = Id65RemotePadGrassT107Catalog.Positions,
            SelectedItem = initialT107Position,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        TextBlock mobyChoiceNotice = new()
        {
            Name = "Id65AuthoringRemotePadMobyChoiceNotice",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            LineHeight = 16,
            Foreground = new SolidColorBrush(Color.FromRgb(150, 94, 12)),
            Text =
                "Schema 4 is canonical-document and static-presentation only. It fixes T66, excludes T88, permits at most one None/Grass/Red Gem choice, and does not authorize physical materialization, Create BIN, runtime use, or release promotion."
        };
        Id65AuthoringMandatoryBaseTextureChoice[] mandatoryTextureChoices =
            CreateId65AuthoringMandatoryBaseTextureChoices();
        Id65AuthoringMandatoryBaseTextureChoice initialMandatoryTexture =
            mandatoryTextureChoices.Single(choice =>
                choice.TextureId == initial.MandatoryBaseFaceTextureId);
        ComboBox mandatoryTexture = new()
        {
            Name = "Id65AuthoringMandatoryBaseTextureSelector",
            ItemsSource = mandatoryTextureChoices,
            SelectedItem = initialMandatoryTexture,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        TextBlock validation = new()
        {
            Text = "The selected choices cannot form an exact document.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ModernRed),
            IsVisible = false
        };
        TextBlock exactPreview = new()
        {
            Name = "Id65AuthoringExactPreviewText",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ModernMutedInk),
            FontSize = 11,
            LineHeight = 16
        };

        StackPanel body = new()
        {
            Spacing = 12,
            Margin = new Thickness(18)
        };
        body.Children.Add(BuildModernDialogHeader(
            replacingExistingDocument
                ? "Edit the canonical ID65 document"
                : "Create the canonical ID65 document",
            "These are exact, evidence-bound authoring choices. Saving publishes only the canonical project document; it does not create a BIN or claim runtime proof."));
        StackPanel mandatoryTextureField = new() { Spacing = 5 };
        mandatoryTextureField.Children.Add(mandatoryTexture);
        mandatoryTextureField.Children.Add(new TextBlock
        {
            Text = "This mandatory two-choice field changes the preview only. The canonical document and viewport change only after Apply and Save Exact Document completes CAS save, reinspection, projection, and apply/recovery.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ModernMutedInk),
            FontSize = 11,
            LineHeight = 16
        });
        body.Children.Add(BuildModernDialogField(
            "Mandatory base-face texture",
            mandatoryTextureField));
        StackPanel terrainField = new() { Spacing = 7 };
        terrainField.Children.Add(BuildId65AuthoringChoice(
            terrain,
            "Schema 1–4 changes the one proved optional triangle. Schema 5 changes Tile 1 independently while preserving Tile 2."));
        Control terrainTile2ChoiceControl = BuildId65AuthoringChoice(
            terrainTile2,
            "Schema-5 Tile 2 is independent and is never inferred by migration.");
        terrainField.Children.Add(terrainTile2ChoiceControl);
        terrainField.Children.Add(migrateTerrainSet);
        body.Children.Add(BuildModernDialogField(
            "Structural terrain",
            terrainField));
        StackPanel blankCycloramaField = new() { Spacing = 7 };
        blankCycloramaField.Children.Add(migrateBlankCyclorama);
        blankCycloramaField.Children.Add(blankCycloramaNotice);
        body.Children.Add(BuildModernDialogField(
            "Blank Slate background",
            blankCycloramaField));
        Control t88ChoiceControl = BuildId65AuthoringChoice(
            t88,
            "Retains the exact historical replacement-row choice at T88. It is not presented as a placed object in the fresh blank scene.");
        body.Children.Add(t88ChoiceControl);
        StackPanel t107Field = new() { Spacing = 7 };
        t107Field.Children.Add(BuildId65AuthoringChoice(
            t107,
            "Remote-pad documents use exactly one verified Grass bundle, fixed yaw, and the selected point on collision T13995. A dormant position is retained even while Grass is omitted."));
        t107Field.Children.Add(t107Position);
        t107Field.Children.Add(t107SemanticsNotice);
        t107Field.Children.Add(migrateT107);
        t107Field.Children.Add(migrateMobyChoice);
        Control t107FieldControl = BuildModernDialogField(
            "Grass T107 placement",
            t107Field);
        body.Children.Add(t107FieldControl);
        StackPanel mobyChoiceField = new() { Spacing = 7 };
        mobyChoiceField.Children.Add(BuildModernDialogField(
            "Mutually exclusive Moby",
            mobyChoice));
        mobyChoiceField.Children.Add(BuildModernDialogField(
            "Retained remote-pad position",
            mobyChoicePosition));
        mobyChoiceField.Children.Add(mobyChoiceNotice);
        Control mobyChoiceFieldControl = BuildModernDialogField(
            "Schema-4/5 Moby choice",
            mobyChoiceField);
        body.Children.Add(mobyChoiceFieldControl);
        body.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(243, 247, 251)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(204, 216, 228)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = new TextBlock
            {
                Text =
                    "Always fixed: the baseline spawn, music, totals, exit, save handles, and every serialized pin outside the exact mandatory base-face choice. Arbitrary terrain, textures, Mobys, and behavior are not accepted by this first exact contract.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(ModernMutedInk),
                LineHeight = 18
            }
        });
        body.Children.Add(exactPreview);
        body.Children.Add(validation);

        Button cancel = NewButton("Cancel");
        Button save = NewButton("Apply and Save Exact Document");
        save.Name = "Id65AuthoringSaveButton";
        StyleModernSecondaryButton(cancel);
        StyleModernPrimaryButton(save, Color.FromRgb(117, 80, 158), 205);
        int GetMandatoryTextureId()
        {
            if (mandatoryTexture.SelectedItem is not
                Id65AuthoringMandatoryBaseTextureChoice selectedChoice ||
                selectedChoice.TextureId is not
                    (Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId or
                     Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId))
            {
                throw new InvalidDataException(
                    "Choose exactly Resident T25 or Proved private T66 for the mandatory base face.");
            }

            return selectedChoice.TextureId;
        }
        Id65RemotePadGrassT107Position GetT107Position()
        {
            if (t107Position.SelectedItem is not
                Id65RemotePadGrassT107Position selectedPosition)
            {
                throw new InvalidDataException(
                    "Choose exactly one verified remote-pad T107 position.");
            }
            return Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                selectedPosition.PositionId);
        }
        Id65RemotePadMobyChoice GetMobyChoice()
        {
            if (mobyChoice.SelectedItem is not
                Id65RemotePadMobyChoice selectedChoice ||
                !ReferenceEquals(
                    selectedChoice,
                    Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(
                        selectedChoice.Kind)))
            {
                throw new InvalidDataException(
                    "Choose exactly None, Grass, or Red Gem from the verified remote-pad catalog.");
            }
            return selectedChoice;
        }
        Id65RemotePadGrassT107Position GetMobyChoicePosition()
        {
            if (mobyChoicePosition.SelectedItem is not
                Id65RemotePadGrassT107Position selectedPosition)
            {
                throw new InvalidDataException(
                    "Choose exactly one verified remote-pad position.");
            }
            string positionId = Id65RemotePadMobyChoiceCatalog
                .GetRequiredPositionId(selectedPosition.PositionId);
            return Id65RemotePadGrassT107Catalog.GetRequiredPosition(positionId);
        }
        Id65AuthoringEditorControlState BuildSelectedState()
        {
            return t107PlacementSemantics ==
                    Id65AuthoringEditorT107PlacementSemantics.RemotePadMobyChoiceV1
                ? terrainSelectionSemantics ==
                    Id65AuthoringEditorTerrainSelectionSemantics.RemotePadTerrainSetV1
                    ? blankCycloramaSelected
                        ? Id65AuthoringEditorControlState
                            .CreateRemotePadMobyChoiceTerrainSetBlankCyclorama(
                                terrain.IsChecked == true,
                                terrainTile2.IsChecked == true,
                                GetMobyChoice().Kind,
                                GetMobyChoicePosition().PositionId)
                        : Id65AuthoringEditorControlState
                            .CreateRemotePadMobyChoiceTerrainSet(
                                terrain.IsChecked == true,
                                terrainTile2.IsChecked == true,
                                GetMobyChoice().Kind,
                                GetMobyChoicePosition().PositionId)
                    : Id65AuthoringEditorControlState.CreateRemotePadMobyChoice(
                        terrain.IsChecked == true,
                        GetMobyChoice().Kind,
                        GetMobyChoicePosition().PositionId)
                : t107PlacementSemantics ==
                    Id65AuthoringEditorT107PlacementSemantics.RemotePadGridV1
                ? Id65AuthoringEditorControlState.CreateRemotePad(
                    terrain.IsChecked == true,
                    t88.IsChecked == true,
                    t107.IsChecked == true,
                    GetT107Position().PositionId,
                    GetMandatoryTextureId())
                : new Id65AuthoringEditorControlState(
                    terrain.IsChecked == true,
                    t88.IsChecked == true,
                    t107.IsChecked == true,
                    GetMandatoryTextureId());
        }
        void RefreshTerrainSemanticsControls()
        {
            bool terrainSet = terrainSelectionSemantics ==
                Id65AuthoringEditorTerrainSelectionSemantics.RemotePadTerrainSetV1;
            bool transientMigration =
                terrainSet && !initial.UsesRemotePadTerrainSet;
            bool transientBlankMigration =
                blankCycloramaSelected && !initial.UsesBlankCyclorama;
            terrain.Content = terrainSet
                ? "Include exact structural Terrain Tile 1"
                : "Include exact structural terrain intent";
            terrainTile2ChoiceControl.IsVisible = terrainSet;
            terrain.IsEnabled = !transientMigration && !transientBlankMigration;
            terrainTile2.IsEnabled =
                terrainSet && !transientMigration && !transientBlankMigration;
            migrateTerrainSet.IsVisible =
                !terrainSet && canOfferTerrainSetMigration;
            migrateBlankCyclorama.IsVisible =
                terrainSet && canOfferBlankCycloramaMigration &&
                !blankCycloramaSelected;
            blankCycloramaNotice.Text = blankCycloramaSelected
                ? initial.UsesBlankCyclorama
                    ? "Blank Slate schema 6 is active and one-way: preset blank-slate-v1 with exact color #263447. Terrain and Moby edits preserve it; this dialog never offers a downgrade."
                    : "Explicit schema-5 to schema-6 migration selected. The exact prior terrain and Moby choices are locked for this save; only Blank Slate preset blank-slate-v1 with exact color #263447 is added."
                : canOfferBlankCycloramaMigration
                    ? "Schema 5 remains unchanged until you explicitly select the migration above and then Apply and Save Exact Document. No preview, refresh, open, or ordinary terrain/Moby edit infers schema 6."
                    : "Blank Slate schema 6 becomes available only through the explicit one-way migration from an exact schema-5 terrain-set document.";
        }
        void RefreshT107SemanticsNotice()
        {
            bool remotePadGrid = t107PlacementSemantics ==
                Id65AuthoringEditorT107PlacementSemantics.RemotePadGridV1;
            bool remotePadMobyChoice = t107PlacementSemantics ==
                Id65AuthoringEditorT107PlacementSemantics.RemotePadMobyChoiceV1;
            t88ChoiceControl.IsVisible = !remotePadMobyChoice;
            t107FieldControl.IsVisible = !remotePadMobyChoice;
            mobyChoiceFieldControl.IsVisible = remotePadMobyChoice;
            mandatoryTexture.IsEnabled = !remotePadMobyChoice;
            mobyChoice.IsEnabled =
                remotePadMobyChoice && initial.UsesRemotePadMobyChoice &&
                !(terrainSelectionSemantics ==
                    Id65AuthoringEditorTerrainSelectionSemantics.RemotePadTerrainSetV1 &&
                  !initial.UsesRemotePadTerrainSet) &&
                !(blankCycloramaSelected && !initial.UsesBlankCyclorama);
            mobyChoicePosition.IsEnabled =
                remotePadMobyChoice && initial.UsesRemotePadMobyChoice &&
                !(terrainSelectionSemantics ==
                    Id65AuthoringEditorTerrainSelectionSemantics.RemotePadTerrainSetV1 &&
                  !initial.UsesRemotePadTerrainSet) &&
                !(blankCycloramaSelected && !initial.UsesBlankCyclorama);
            mobyChoiceNotice.Text = remotePadMobyChoice &&
                !initial.UsesRemotePadMobyChoice
                ? "This explicit schema-3 migration is locked to its exact mapped None/Grass choice and retained position. Save it first, then use the schema-4 Object Manager to switch None, Grass, or Red Gem. No selector preview performs the migration. Physical materialization and every runtime/release path remain closed."
                : terrainSelectionSemantics ==
                    Id65AuthoringEditorTerrainSelectionSemantics.RemotePadTerrainSetV1 &&
                  !initial.UsesRemotePadTerrainSet
                    ? "This explicit schema-4 migration is locked to the exact prior Tile 1 and Moby choices; Tile 2 remains off. Save first, then use the schema-5 terrain and Moby controls or its separately guarded exact offline candidate. Normal Create BIN and every runtime/release path remain closed."
                : terrainSelectionSemantics ==
                    Id65AuthoringEditorTerrainSelectionSemantics.RemotePadTerrainSetV1
                    ? "Schema 5 fixes T66, excludes T88, combines two independent terrain units with at most one None/Grass/Red Gem choice, and can build the separately guarded exact offline candidate. Normal Create BIN, runtime proof, and release promotion remain unavailable."
                    : "Schema 4 is canonical-document and static-presentation only. It fixes T66, excludes T88, permits at most one None/Grass/Red Gem choice, and does not authorize physical materialization, Create BIN, runtime use, or release promotion.";
            t107Position.IsEnabled = remotePadGrid;
            migrateT107.IsVisible =
                !remotePadGrid && !remotePadMobyChoice &&
                initial.T107PlacementSemantics ==
                    Id65AuthoringEditorT107PlacementSemantics.LegacyNativeApronV1;
            migrateMobyChoice.IsVisible =
                remotePadGrid && canOfferMobyChoiceMigration;
            t107.Content = remotePadGrid
                ? "Include verified Grass T107 on the fresh remote pad"
                : "Include historical proved Grass placement at T107";
            t107SemanticsNotice.Foreground = new SolidColorBrush(
                remotePadGrid ? ModernMutedInk : Color.FromRgb(150, 94, 12));
            t107SemanticsNotice.Text = remotePadGrid
                ? "Remote-pad 3x3 semantics are active. The selected point is document-bound and retained while disabled. With T88 off and Proved private T66, all four terrain/Grass combinations can build an exact offline candidate; runtime acceptance remains separate."
                : "Legacy native-apron semantics are preserved exactly. The historical coordinate is not the new pad center and is never shown on the blank pad. Use the explicit migration button to choose Center for a new schema-3 document; migration is applied only if you save.";
        }
        void RefreshExactPreview()
        {
            try
            {
                int mandatoryTextureId = GetMandatoryTextureId();
                Id65AuthoringEditorControlState selected = BuildSelectedState();
                Id65AuthoringEditorControlStateCompilation compilation =
                    Id65AuthoringEditorControlStateCompiler.Compile(selected);
                bool remoteCandidateSelected =
                    selected.UsesRemotePadT107Grid &&
                    !selected.IncludeGrassAtT88 &&
                    mandatoryTextureId ==
                        Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId;
                string textureRoute = mandatoryTextureId ==
                    Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId
                    ? "Resident T25; ordinary resident close/normal/LQ/HQ viewport caches; offline-authoring-only; exact candidate/Create BIN/runtime/release refused"
                    : selected.UsesBlankCyclorama
                        ? "Proved private T66 fixed by Blank Slate schema 6; preset blank-slate-v1 with exact color #263447; separately guarded exact offline schema-6 candidate eligible; normal Create BIN/runtime/release refused"
                    : selected.UsesRemotePadTerrainSet
                        ? "Proved private T66 fixed by schema 5; two-unit terrain set plus canonical Moby choice; separately guarded exact offline candidate eligible; normal Create BIN/runtime/release refused"
                        : selected.UsesRemotePadMobyChoice
                        ? "Proved private T66 fixed by schema 4; canonical document and static fresh-scene presentation only; physical materialization/Create BIN/runtime/release refused"
                        : selected.UsesRemotePadT107Grid
                        ? remoteCandidateSelected
                            ? "Proved private T66; exact private viewport projection; verified schema-3 offline-candidate state; normal Create BIN/runtime/release refused"
                            : "Proved private T66; exact private viewport projection; Grass T88 must remain off for a candidate; Create BIN/runtime/release refused"
                        : "Proved private T66; exact private viewport projection; separately guarded legacy full-candidate path retained";
                exactPreview.Text =
                    $"Exact document {compilation.DocumentCanonicalSha256[..12]}…; " +
                    $"mandatory base face {textureRoute}; " +
                    $"terrain intents {compilation.TerrainIntentCount}; Moby targets " +
                    $"{(compilation.MobyTargetTrueIndices.Count == 0 ? "none" : string.Join(", ", compilation.MobyTargetTrueIndices.Select(index => $"T{index}")))}; " +
                    (selected.UsesBlankCyclorama
                        ? $"Blank Slate schema-6 terrain Tile 1 {(selected.IncludeTerrainTile1 ? "ON" : "OFF")}, Tile 2 {(selected.IncludeTerrainTile2 ? "ON" : "OFF")}; {GetMobyChoice().Label} at retained {GetMobyChoicePosition().Label}; preset {selected.BlankCycloramaPresetId}, color {selected.BlankCycloramaColorHex}; separately guarded exact offline candidate eligible; no normal Create BIN/runtime/release authority."
                        : selected.UsesRemotePadTerrainSet
                        ? $"schema-5 terrain Tile 1 {(selected.IncludeTerrainTile1 ? "ON" : "OFF")}, Tile 2 {(selected.IncludeTerrainTile2 ? "ON" : "OFF")}; {GetMobyChoice().Label} at retained {GetMobyChoicePosition().Label}; separately guarded exact offline candidate eligible; no normal Create BIN/runtime/release authority."
                        : selected.UsesRemotePadMobyChoice
                        ? $"schema-4 {GetMobyChoice().Label} choice at retained {GetMobyChoicePosition().Label} " +
                          $"({GetMobyChoicePosition().EditorX}, {GetMobyChoicePosition().EditorY}, {GetMobyChoicePosition().EditorZ}); static presentation only; no physical candidate authority."
                        : selected.UsesRemotePadT107Grid
                        ? $"T107 remote-pad position {GetT107Position().Label} ({GetT107Position().EditorX}, {GetT107Position().EditorY}, {GetT107Position().EditorZ}); {(remoteCandidateSelected ? "exact offline candidate eligible" : "saved presentation only for this combination")}."
                        : "legacy native-apron T107 semantics; historical exact-candidate route only.");
                validation.IsVisible = false;
                save.IsEnabled = true;
            }
            catch (InvalidDataException ex)
            {
                exactPreview.Text = "This transient choice cannot form an exact document.";
                validation.Text = ex.Message;
                validation.IsVisible = true;
                save.IsEnabled = false;
            }
        }
        terrain.PropertyChanged += (_, e) =>
        {
            if (e.Property == CheckBox.IsCheckedProperty)
                RefreshExactPreview();
        };
        terrainTile2.PropertyChanged += (_, e) =>
        {
            if (e.Property == CheckBox.IsCheckedProperty)
                RefreshExactPreview();
        };
        t88.PropertyChanged += (_, e) =>
        {
            if (e.Property == CheckBox.IsCheckedProperty)
                RefreshExactPreview();
        };
        t107.PropertyChanged += (_, e) =>
        {
            if (e.Property == CheckBox.IsCheckedProperty)
                RefreshExactPreview();
        };
        mandatoryTexture.SelectionChanged += (_, _) => RefreshExactPreview();
        t107Position.SelectionChanged += (_, _) => RefreshExactPreview();
        mobyChoice.SelectionChanged += (_, _) => RefreshExactPreview();
        mobyChoicePosition.SelectionChanged += (_, _) => RefreshExactPreview();
        migrateT107.Click += (_, _) =>
        {
            try
            {
                Id65AuthoringEditorControlState legacyPreview = new(
                    terrain.IsChecked == true,
                    t88.IsChecked == true,
                    t107.IsChecked == true,
                    GetMandatoryTextureId());
                Id65AuthoringEditorControlState migrated =
                    Id65AuthoringEditorControlState.MigrateLegacyToRemotePadGrid(
                        legacyPreview,
                        Id65RemotePadGrassT107Catalog.DefaultPositionId);
                t107PlacementSemantics = migrated.T107PlacementSemantics;
                t107Position.SelectedItem =
                    Id65RemotePadGrassT107Catalog.DefaultPosition;
                RefreshT107SemanticsNotice();
                RefreshExactPreview();
            }
            catch (Exception ex) when (
                ex is InvalidDataException or InvalidOperationException)
            {
                validation.Text = ex.Message;
                validation.IsVisible = true;
            }
        };
        migrateTerrainSet.Click += (_, _) =>
        {
            try
            {
                Id65AuthoringEditorControlState schema4Preview =
                    Id65AuthoringEditorControlState.CreateRemotePadMobyChoice(
                        terrain.IsChecked == true,
                        GetMobyChoice().Kind,
                        GetMobyChoicePosition().PositionId);
                Id65AuthoringEditorControlState migrated =
                    Id65AuthoringEditorControlState
                        .MigrateRemotePadMobyChoiceToTerrainSet(schema4Preview);
                terrainSelectionSemantics = migrated.TerrainSelectionSemantics;
                terrain.IsChecked = migrated.IncludeTerrainTile1;
                terrainTile2.IsChecked = migrated.IncludeTerrainTile2;
                RefreshTerrainSemanticsControls();
                RefreshT107SemanticsNotice();
                RefreshExactPreview();
            }
            catch (Exception ex) when (
                ex is InvalidDataException or InvalidOperationException)
            {
                validation.Text = ex.Message;
                validation.IsVisible = true;
            }
        };
        migrateBlankCyclorama.Click += (_, _) =>
        {
            try
            {
                Id65AuthoringEditorControlState schema5Preview =
                    Id65AuthoringEditorControlState
                        .CreateRemotePadMobyChoiceTerrainSet(
                            terrain.IsChecked == true,
                            terrainTile2.IsChecked == true,
                            GetMobyChoice().Kind,
                            GetMobyChoicePosition().PositionId);
                Id65AuthoringEditorControlState migrated =
                    Id65AuthoringEditorControlState
                        .MigrateRemotePadMobyChoiceTerrainSetToBlankCyclorama(
                            schema5Preview);
                blankCycloramaSelected = migrated.UsesBlankCyclorama;
                terrain.IsChecked = migrated.IncludeTerrainTile1;
                terrainTile2.IsChecked = migrated.IncludeTerrainTile2;
                mobyChoice.SelectedItem =
                    Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(
                        migrated.RemotePadMobyKind!.Value);
                mobyChoicePosition.SelectedItem =
                    Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                        migrated.RemotePadMobyPositionId!);
                RefreshTerrainSemanticsControls();
                RefreshT107SemanticsNotice();
                RefreshExactPreview();
            }
            catch (Exception ex) when (
                ex is InvalidDataException or InvalidOperationException)
            {
                validation.Text = ex.Message;
                validation.IsVisible = true;
            }
        };
        migrateMobyChoice.Click += (_, _) =>
        {
            try
            {
                Id65AuthoringEditorControlState schema3Preview =
                    Id65AuthoringEditorControlState.CreateRemotePad(
                        terrain.IsChecked == true,
                        t88.IsChecked == true,
                        t107.IsChecked == true,
                        GetT107Position().PositionId,
                        GetMandatoryTextureId());
                Id65AuthoringEditorControlState migrated =
                    Id65AuthoringEditorControlState
                        .MigrateRemotePadGridToMobyChoice(schema3Preview);
                t107PlacementSemantics = migrated.T107PlacementSemantics;
                mobyChoice.SelectedItem =
                    Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(
                        migrated.RemotePadMobyKind!.Value);
                mobyChoicePosition.SelectedItem =
                    Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                        migrated.RemotePadMobyPositionId!);
                RefreshT107SemanticsNotice();
                RefreshExactPreview();
            }
            catch (Exception ex) when (
                ex is InvalidDataException or InvalidOperationException)
            {
                validation.Text = ex.Message;
                validation.IsVisible = true;
            }
        };
        cancel.Click += (_, _) =>
            dialog.Close((Id65AuthoringEditorControlState?)null);
        save.Click += (_, _) =>
        {
            try
            {
                Id65AuthoringEditorControlState selected = BuildSelectedState();
                _ = Id65AuthoringEditorControlStateCompiler.Compile(selected);
                validation.IsVisible = false;
                dialog.Close(selected);
            }
            catch (InvalidDataException ex)
            {
                validation.Text = ex.Message;
                validation.IsVisible = true;
            }
        };
        RefreshTerrainSemanticsControls();
        RefreshT107SemanticsNotice();
        RefreshExactPreview();
        dialog.Content = BuildModernDialogFrame(body, cancel, save);
        return await dialog.ShowDialog<Id65AuthoringEditorControlState?>(this);
    }

    private static Control BuildId65AuthoringChoice(
        CheckBox choice,
        string description)
    {
        StackPanel panel = new() { Spacing = 4 };
        panel.Children.Add(choice);
        panel.Children.Add(new TextBlock
        {
            Text = description,
            Margin = new Thickness(24, 0, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ModernMutedInk),
            FontSize = 11,
            LineHeight = 16
        });
        return panel;
    }

    private static bool Id65AuthoringControlStatesEqual(
        Id65AuthoringEditorControlState left,
        Id65AuthoringEditorControlState right) =>
        left.IncludeStructuralTerrain == right.IncludeStructuralTerrain &&
        left.IncludeGrassAtT88 == right.IncludeGrassAtT88 &&
        left.IncludeGrassAtT107 == right.IncludeGrassAtT107 &&
        left.MandatoryBaseFaceTextureId == right.MandatoryBaseFaceTextureId &&
        left.T107PlacementSemantics == right.T107PlacementSemantics &&
        string.Equals(
            left.T107RemotePadPositionId,
            right.T107RemotePadPositionId,
            StringComparison.Ordinal) &&
        left.RemotePadMobyKind == right.RemotePadMobyKind &&
        string.Equals(
            left.RemotePadMobyPositionId,
            right.RemotePadMobyPositionId,
            StringComparison.Ordinal) &&
        left.TerrainSelectionSemantics == right.TerrainSelectionSemantics &&
        left.IncludeTerrainTile2 == right.IncludeTerrainTile2 &&
        left.UsesBlankCyclorama == right.UsesBlankCyclorama &&
        string.Equals(
            left.BlankCycloramaPresetId,
            right.BlankCycloramaPresetId,
            StringComparison.Ordinal) &&
        string.Equals(
            left.BlankCycloramaColorHex,
            right.BlankCycloramaColorHex,
            StringComparison.Ordinal);

    private static Id65AuthoringEditorControlState
        CreateId65TerrainSetMutationPreservingBlankCyclorama(
            Id65AuthoringEditorControlState currentState,
            bool includeTerrainTile1,
            bool includeTerrainTile2,
            Id65RemotePadMobyKind mobyKind,
            string positionId)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        if (!currentState.UsesRemotePadTerrainSet)
        {
            throw new InvalidDataException(
                "Only an exact schema-5 or Blank Slate schema-6 terrain-set document can use this mutation route.");
        }
        return currentState.UsesBlankCyclorama
            ? Id65AuthoringEditorControlState
                .CreateRemotePadMobyChoiceTerrainSetBlankCyclorama(
                    includeTerrainTile1,
                    includeTerrainTile2,
                    mobyKind,
                    positionId)
            : Id65AuthoringEditorControlState
                .CreateRemotePadMobyChoiceTerrainSet(
                    includeTerrainTile1,
                    includeTerrainTile2,
                    mobyKind,
                    positionId);
    }

    private static bool Id65AuthoringDocumentSnapshotsEqual(
        Id65AuthoringEditorDocumentReadModel? left,
        Id65AuthoringEditorDocumentReadModel? right) =>
        left == null
            ? right == null
            : right != null &&
              string.Equals(
                  left.EnvelopeCanonicalSha256,
                  right.EnvelopeCanonicalSha256,
                  StringComparison.Ordinal) &&
              string.Equals(
                  left.DocumentCanonicalSha256,
                  right.DocumentCanonicalSha256,
                  StringComparison.Ordinal) &&
              string.Equals(
                  left.DocumentContentSha256,
                  right.DocumentContentSha256,
                  StringComparison.Ordinal) &&
              string.Equals(
                  left.ReadModelCanonicalSha256,
                  right.ReadModelCanonicalSha256,
                  StringComparison.Ordinal);

    private static void RequireExactId65AuthoringDocumentWriteResult(
        Id65AuthoringEditorDocumentWriteResult written,
        Id65AuthoringEditorControlStateCompilation expectedCompilation,
        bool expectedExplicitLegacyToRemotePadMigration = false,
        bool expectedExplicitRemotePadMobyChoiceMigration = false,
        bool expectedExplicitRemotePadTerrainSetMigration = false,
        bool expectedExplicitBlankCycloramaMigration = false)
    {
        if (written.DocumentCanonicalSha256 !=
                expectedCompilation.DocumentCanonicalSha256 ||
            written.DocumentContentSha256 !=
                expectedCompilation.DocumentContentSha256 ||
            written.Document.MandatoryBaseFaceTextureId !=
                expectedCompilation.State.MandatoryBaseFaceTextureId ||
            !Id65AuthoringControlStatesEqual(
                expectedCompilation.State,
                Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                    written.Document)) ||
            !written.ExplicitSaveInvocationRequired ||
            written.ExplicitLegacyToRemotePadMigrationPerformed !=
                expectedExplicitLegacyToRemotePadMigration ||
            written.ExplicitRemotePadMobyChoiceMigrationPerformed !=
                expectedExplicitRemotePadMobyChoiceMigration ||
            written.ExplicitRemotePadTerrainSetMigrationPerformed !=
                expectedExplicitRemotePadTerrainSetMigration ||
            written.ExplicitBlankCycloramaMigrationPerformed !=
                expectedExplicitBlankCycloramaMigration ||
            written.AutomaticMigrationPerformed ||
            !written.CompareAndSwapEnforced ||
            !written.AtomicPublicationCompleted ||
            !written.PersistencePerformed ||
            written.CarriesAppOrUiAuthority ||
            written.WritesDiscImage || written.WritesCue ||
            written.AggregateMaterializationEnabled ||
            written.NormalCreateBinEnabled ||
            written.RuntimeCandidateAuthorized || written.Publishable)
        {
            throw new InvalidDataException(
                "The ID65 Full Authoring Document receipt crossed an App, BIN, runtime, or publication boundary.");
        }
    }

    private async Task<(
        Id65AuthoringEditorDocumentReadModel Readback,
        LevelLoadData? PreparedLevel)>
        ReinspectAndPrepareId65FreshProjectionAsync(
            UnusedLevel65BlankLevelLabWorkspacePaths paths,
            UnusedLevel65BlankLevelLabManifest manifest,
            Id65AuthoringEditorDocumentWriteResult written,
            Id65AuthoringEditorControlState expectedState,
            Id65BlankLabManualOperation operation)
    {
        RequireCurrentId65BlankLabManualOperation(operation);
        Id65AuthoringEditorDocumentReadModel readback =
            await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                paths,
                manifest,
                operation.Cancellation.Token) ?? throw new InvalidDataException(
                "The canonical ID65 Full Authoring Document disappeared after its atomic save. The prior viewport was retained; reload the Lab after checking the authored-edits folder.");
        RequireCurrentId65BlankLabManualOperation(operation);
        if (!Id65AuthoringDocumentSnapshotsEqual(written.Document, readback) ||
            !Id65AuthoringControlStatesEqual(
                expectedState,
                Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                    readback)))
        {
            throw new InvalidDataException(
                "The canonical ID65 Full Authoring Document did not pass exact post-save readback. The prior viewport was retained; reopen the document before retrying.");
        }

        LevelLoadData? prepared =
            await PrepareId65FreshProjectionAsync(
                readback,
                expectedState,
                operation,
                requireSceneIdentity: true);
        return (readback, prepared);
    }

    private async Task<LevelLoadData?> PrepareId65FreshProjectionAsync(
        Id65AuthoringEditorDocumentReadModel readback,
        Id65AuthoringEditorControlState expectedState,
        Id65BlankLabManualOperation operation,
        bool requireSceneIdentity)
    {
        RequireCurrentId65BlankLabManualOperation(
            operation,
            requireSceneIdentity);

        if (!IsCurrentId65BlankLab())
            return null;

        LevelDefinition level = _currentLevel ?? throw new InvalidDataException(
            "The exact ID65 scene lost its admitted level identity before projection.");
        LevelLoadData prepared = await Task.Run(
            () => LoadLevelData(
                level.Key,
                id65AuthoringDocument: readback,
                id65AuthoringDocumentInspectionCompleted: true),
            operation.Cancellation.Token);
        RequireCurrentId65BlankLabManualOperation(
            operation,
            requireSceneIdentity);
        Id65FreshLevelSceneProjection projection =
            prepared.Id65FreshSceneProjection ?? throw new InvalidDataException(
                "The saved ID65 authoring document could not be projected. The prior viewport was retained; reopen the document and retry.");
        TerrainPolygon mandatoryBaseFace = prepared.Geometry?.Polygons.SingleOrDefault(
            polygon => polygon.FaceIndex == Id65FreshLevelSceneProjector.AuthoredFaceIndex) ??
            throw new InvalidDataException(
                "The saved ID65 document projection lost its mandatory base face.");
        uint expectedMandatoryFaceWord2 = expectedState.MandatoryBaseFaceTextureId ==
            Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId
            ? 0x01400242u
            : 0x01400219u;
        if (!ReferenceEquals(prepared.Id65AuthoringDocument, readback) ||
            !projection.CanonicalDocumentBound ||
            readback.MandatoryBaseFaceTextureId !=
                expectedState.MandatoryBaseFaceTextureId ||
            projection.StructuralTerrainPresent !=
                expectedState.HasAnyStructuralTerrain ||
            projection.DocumentCanonicalSha256 !=
                readback.DocumentCanonicalSha256 ||
            projection.DocumentContentSha256 !=
                readback.DocumentContentSha256 ||
            !Id65FreshLevelSceneProjector.IsExactProjectionGeometry(
                prepared.Geometry) ||
            mandatoryBaseFace.TextureId !=
                expectedState.MandatoryBaseFaceTextureId ||
            mandatoryBaseFace.OriginalTextureId !=
                expectedState.MandatoryBaseFaceTextureId ||
            mandatoryBaseFace.NativeTextureId !=
                expectedState.MandatoryBaseFaceTextureId ||
            mandatoryBaseFace.NativeMaterialByte !=
                (byte)expectedState.MandatoryBaseFaceTextureId ||
            mandatoryBaseFace.NativeFaceWord2 != expectedMandatoryFaceWord2 ||
            prepared.LoadedTerrainEdits != 0 ||
            !Id65PreparedMobysMatchProjection(
                prepared.Mobys,
                projection) ||
            prepared.CustomTerrainTextures.Count != 0 ||
            prepared.NativeTerrainTextureRelocations.Count != 0)
        {
            throw new InvalidDataException(
                "The saved ID65 document failed its exact fresh-scene projection boundary. The prior viewport was retained; no generic TerrainEditStore state was applied.");
        }

        return prepared;
    }

    private void ApplyPreparedId65FreshProjection(
        Id65BlankLabManualOperation operation,
        LevelLoadData? preparedLevel,
        bool invokePostApplyFaultSeam = true)
    {
        if (preparedLevel == null)
            return;

        RequireCurrentId65BlankLabManualOperation(operation);
        LevelDefinition previousLevel = _currentLevel ?? throw new InvalidDataException(
            "The exact ID65 projection commit lost its current level.");
        GeometryCandidate? previousGeometry = _currentGeometry;
        _levelLoadRequestId++;
        ApplyLoadedLevel(previousLevel, preparedLevel);
        _id65FreshDocumentProjectionSynchronized = false;
        if (invokePostApplyFaultSeam)
        {
            Id65FreshProjectionApplyFaultForTesting?.Invoke(
                "after-apply-before-operation-transition");
        }
        TransitionId65BlankLabManualOperationSceneIdentity(
            operation,
            previousLevel,
            previousGeometry,
            previousLevel,
            preparedLevel.Geometry);
        _id65FreshDocumentProjectionSynchronized = true;
        RefreshId65BlankLabUi();
        RefreshActionAvailability();
    }

    private void ClearId65SceneForQuarantine(
        Id65BlankLabManualOperation operation)
    {
        RequireCurrentId65BlankLabManualOperation(
            operation,
            requireSceneIdentity: false);
        _levelLoadRequestId++;
        ClearLoadedSceneForWorkspaceCommit();
        operation.ExpectedLevelIdentity = _currentLevel;
        operation.ExpectedGeometryIdentity = _currentGeometry;
        _id65FreshDocumentProjectionSynchronized = false;
        RefreshId65BlankLabUi();
        RefreshActionAvailability();
    }

    private void ApplyRecoveredId65FreshProjection(
        Id65BlankLabManualOperation operation,
        LevelLoadData preparedLevel)
    {
        RequireCurrentId65BlankLabManualOperation(
            operation,
            requireSceneIdentity: false);
        LevelDefinition? expectedPreviousLevel =
            operation.ExpectedLevelIdentity;
        GeometryCandidate? expectedPreviousGeometry =
            operation.ExpectedGeometryIdentity;
        LevelDefinition level = _currentLevel ?? throw new InvalidDataException(
            "The ID65 recovery lost its current exact level identity.");
        _levelLoadRequestId++;
        ApplyLoadedLevel(level, preparedLevel);
        _id65FreshDocumentProjectionSynchronized = false;
        TransitionId65BlankLabManualOperationSceneIdentity(
            operation,
            expectedPreviousLevel,
            expectedPreviousGeometry,
            level,
            preparedLevel.Geometry);
    }

    private async Task<string> RecoverId65PostWriteProjectionFailureAsync(
        Id65BlankLabManualOperation operation,
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest manifest,
        Id65AuthoringEditorDocumentWriteResult written,
        Exception applyFailure,
        string actionLabel)
    {
        _id65FreshDocumentProjectionSynchronized = false;
        if (!IsCurrentId65BlankLabManualOperation(
                operation,
                requireSceneIdentity: false))
        {
            return
                $"The {actionLabel} document save completed, but its viewport result belongs to an older editor generation. Load the ID65 Lab again before creating a candidate.";
        }

        if (!IsCurrentId65BlankLab())
        {
            return
                $"The {actionLabel} document save completed, but no ID65 Lab viewport is loaded. Load the Lab to inspect the saved exact document. Initial apply error: {applyFailure.Message}";
        }

        try
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                RequireCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: false);
                Id65AuthoringEditorDocumentReadModel readback =
                    await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                        paths,
                        manifest,
                        operation.Cancellation.Token) ??
                    throw new InvalidDataException(
                        "The canonical document disappeared while recovering the saved viewport.");
                bool currentEnvelopeIsSavedWrite =
                    Id65AuthoringDocumentSnapshotsEqual(
                    written.Document,
                    readback);
                Id65AuthoringEditorControlState currentState =
                    Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                        readback);
                LevelLoadData prepared =
                    await PrepareId65FreshProjectionAsync(
                        readback,
                        currentState,
                        operation,
                        requireSceneIdentity: false) ??
                    throw new InvalidDataException(
                        "The exact ID65 Lab stopped being current during viewport recovery.");
                ApplyRecoveredId65FreshProjection(operation, prepared);

                Id65AuthoringEditorDocumentReadModel? confirmation =
                    await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                        paths,
                        manifest,
                        operation.Cancellation.Token);
                RequireCurrentId65BlankLabManualOperation(operation);
                if (Id65AuthoringDocumentSnapshotsEqual(
                        readback,
                        confirmation))
                {
                    _id65FreshDocumentProjectionSynchronized = true;
                    RefreshId65BlankLabUi();
                    RefreshActionAvailability();
                    return currentEnvelopeIsSavedWrite
                        ? $"The {actionLabel} document save succeeded, but its first viewport apply failed: {applyFailure.Message} The editor recovered by re-inspecting and fully loading the saved exact document. Candidate creation remains gated by a fresh disk snapshot check."
                        : $"The {actionLabel} document save was followed by a concurrent canonical document update, and the first viewport apply failed: {applyFailure.Message} The editor loaded the concurrent winner exactly. Review its controls before retrying; candidate creation remains gated by a fresh disk snapshot check.";
                }
            }

            throw new InvalidDataException(
                "The canonical document changed repeatedly during three recovery attempts.");
        }
        catch (OperationCanceledException recoveryCancellation)
        {
            if (IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: false) &&
                IsCurrentId65BlankLab())
            {
                ClearId65SceneForQuarantine(operation);
                return
                    $"The {actionLabel} document save completed, but viewport synchronization was canceled after the apply failure ({applyFailure.Message}): {recoveryCancellation.Message} The partially applied scene was cleared; candidate materialization and optional terrain controls are quarantined. Use Load Lab to re-inspect the exact saved document before continuing.";
            }

            return
                $"The {actionLabel} document save completed, but its recovery was canceled after the editor moved to a newer workspace generation. Load the ID65 Lab again before creating a candidate.";
        }
        catch (Exception recoveryFailure)
        {
            if (IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: false) &&
                IsCurrentId65BlankLab())
            {
                ClearId65SceneForQuarantine(operation);
                return
                    $"The {actionLabel} document save completed, but the viewport could not be synchronized after the apply failure ({applyFailure.Message}): {recoveryFailure.Message} The hybrid scene was cleared; candidate materialization and optional terrain controls are quarantined. Use Load Lab to re-inspect the exact saved document before continuing.";
            }

            return
                $"The {actionLabel} document save completed, but viewport recovery failed after the editor moved to a newer workspace generation ({recoveryFailure.Message}). Load the ID65 Lab again before creating a candidate.";
        }
    }

    private async Task<string> RecoverId65NoWriteProjectionFailureAsync(
        Id65BlankLabManualOperation operation,
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest manifest,
        Exception applyFailure,
        string actionLabel)
    {
        _id65FreshDocumentProjectionSynchronized = false;
        if (!IsCurrentId65BlankLabManualOperation(
                operation,
                requireSceneIdentity: false))
        {
            return
                $"No canonical document file was changed by the {actionLabel}, but its viewport result belongs to an older editor generation. Load the ID65 Lab again before creating a candidate.";
        }

        if (!IsCurrentId65BlankLab())
        {
            return
                $"No canonical document file was changed by the {actionLabel}, and no ID65 Lab viewport is loaded. Load the Lab to inspect the exact disk document. Initial apply error: {applyFailure.Message}";
        }

        try
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                RequireCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: false);
                Id65AuthoringEditorDocumentReadModel readback =
                    await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                        paths,
                        manifest,
                        operation.Cancellation.Token) ??
                    throw new InvalidDataException(
                        "The canonical document disappeared while recovering the unchanged viewport refresh.");
                Id65AuthoringEditorControlState currentState =
                    Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                        readback);
                LevelLoadData prepared =
                    await PrepareId65FreshProjectionAsync(
                        readback,
                        currentState,
                        operation,
                        requireSceneIdentity: false) ??
                    throw new InvalidDataException(
                        "The exact ID65 Lab stopped being current during unchanged-viewport recovery.");
                ApplyRecoveredId65FreshProjection(operation, prepared);

                Id65AuthoringEditorDocumentReadModel? confirmation =
                    await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                        paths,
                        manifest,
                        operation.Cancellation.Token);
                RequireCurrentId65BlankLabManualOperation(operation);
                if (Id65AuthoringDocumentSnapshotsEqual(readback, confirmation))
                {
                    _id65FreshDocumentProjectionSynchronized = true;
                    RefreshId65BlankLabUi();
                    RefreshActionAvailability();
                    return
                        $"No canonical document file was changed by the {actionLabel}. Its first stale-viewport apply failed: {applyFailure.Message} The editor recovered by re-inspecting and fully loading the exact disk document; candidate creation remains gated by a fresh disk snapshot check.";
                }
            }

            throw new InvalidDataException(
                "The canonical document changed repeatedly during three unchanged-viewport recovery attempts.");
        }
        catch (OperationCanceledException recoveryCancellation)
        {
            if (IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: false) &&
                IsCurrentId65BlankLab())
            {
                ClearId65SceneForQuarantine(operation);
                return
                    $"No canonical document file was changed by the {actionLabel}, but viewport synchronization was canceled after the apply failure ({applyFailure.Message}): {recoveryCancellation.Message} The partially applied scene was cleared; candidate materialization and optional terrain controls are quarantined. Use Load Lab to re-inspect the exact disk document before continuing.";
            }

            return
                $"No canonical document file was changed by the {actionLabel}; its recovery was canceled after the editor moved to a newer workspace generation. Load the ID65 Lab again before creating a candidate.";
        }
        catch (Exception recoveryFailure)
        {
            if (IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: false) &&
                IsCurrentId65BlankLab())
            {
                ClearId65SceneForQuarantine(operation);
                return
                    $"No canonical document file was changed by the {actionLabel}, but the viewport could not be synchronized after the apply failure ({applyFailure.Message}): {recoveryFailure.Message} The hybrid scene was cleared; candidate materialization and optional terrain controls are quarantined. Use Load Lab to re-inspect the exact disk document before continuing.";
            }

            return
                $"No canonical document file was changed by the {actionLabel}, but viewport recovery failed after the editor moved to a newer workspace generation ({recoveryFailure.Message}). Load the ID65 Lab again before creating a candidate.";
        }
    }

    private async Task SetId65FreshOptionalTerrainAsync(
        bool includeStructuralTerrain)
    {
        string action = includeStructuralTerrain
            ? "create optional terrain"
            : "remove optional terrain";
        if (!IsCurrentId65BlankLab() || _currentLevel == null ||
            !Id65FreshLevelSceneProjector.IsProjectionGeometry(_currentGeometry) ||
            _id65BlankLabPaths == null || _id65BlankLabManifest == null)
        {
            _statusText.Text =
                $"Load the exact ID65 Blank-Level Lab before using {action}.";
            return;
        }
        if (_id65FreshAuthoringDocument == null)
        {
            _statusText.Text =
                $"Could not {action}: no canonical Full Authoring Document is loaded. Use Edit Full Authoring Document… to create it explicitly; no default was persisted.";
            return;
        }
        if (HasUnsavedTerrainEdits() || HasUnsavedMobyEdits() ||
            HasUnsavedNativeMovementEdits() || HasUnsavedDragonRunToEdits())
        {
            _statusText.Text =
                $"Could not {action}: save, undo, or reload current scene edits first. No document or viewport state was changed.";
            return;
        }
        if (!TryBeginId65BlankLabManualOperation(
                action,
                out Id65BlankLabManualOperation? operation) ||
            operation == null)
        {
            return;
        }

        UnusedLevel65BlankLevelLabWorkspacePaths? recoveryPaths = null;
        UnusedLevel65BlankLevelLabManifest? recoveryManifest = null;
        Id65AuthoringEditorDocumentWriteResult? durableWrite = null;

        try
        {
            UnusedLevel65BlankLevelLabWorkspacePaths paths =
                UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
                    operation.WorkspaceRoot);
            UnusedLevel65BlankLevelLabManifest manifest =
                _id65BlankLabManifest ?? throw new InvalidDataException(
                    "The optional terrain action lost its admitted ID65 manifest.");
            UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(
                manifest,
                paths);
            recoveryPaths = paths;
            recoveryManifest = manifest;
            RequireCurrentId65BlankLabManualOperation(operation);

            _statusText.Text =
                $"ID65 {action}: inspecting the current canonical document…";
            Id65AuthoringEditorDocumentReadModel current =
                await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                    paths,
                    manifest,
                    operation.Cancellation.Token) ?? throw new InvalidDataException(
                    "The canonical Full Authoring Document is missing. Use Edit Full Authoring Document… to create it explicitly.");
            RequireCurrentId65BlankLabManualOperation(operation);
            if (!Id65AuthoringDocumentSnapshotsEqual(
                    _id65FreshAuthoringDocument,
                    current))
            {
                throw new InvalidDataException(
                    "The canonical Full Authoring Document changed after this viewport was projected. Reload the Lab before changing optional terrain.");
            }

            Id65AuthoringEditorControlState currentState =
                Id65AuthoringEditorControlStateCompiler.RecoverControlState(current);
            if (currentState.IncludeStructuralTerrain == includeStructuralTerrain)
            {
                _statusText.Text = includeStructuralTerrain
                    ? "Optional structural terrain is already present; no document or viewport state was changed."
                    : "Optional structural terrain is already absent; no document or viewport state was changed.";
                return;
            }
            Id65AuthoringEditorControlState nextState =
                currentState.UsesRemotePadTerrainSet
                    ? CreateId65TerrainSetMutationPreservingBlankCyclorama(
                            currentState,
                            includeStructuralTerrain,
                            currentState.IncludeTerrainTile2,
                            currentState.RemotePadMobyKind!.Value,
                            currentState.RemotePadMobyPositionId!)
                : currentState.UsesRemotePadMobyChoice
                    ? Id65AuthoringEditorControlState.CreateRemotePadMobyChoice(
                        includeStructuralTerrain,
                        currentState.RemotePadMobyKind!.Value,
                        currentState.RemotePadMobyPositionId!)
                    : currentState.UsesRemotePadT107Grid
                    ? Id65AuthoringEditorControlState.CreateRemotePad(
                        includeStructuralTerrain,
                        currentState.IncludeGrassAtT88,
                        currentState.IncludeGrassAtT107,
                        currentState.T107RemotePadPositionId!,
                        currentState.MandatoryBaseFaceTextureId)
                    : new Id65AuthoringEditorControlState(
                        includeStructuralTerrain,
                        currentState.IncludeGrassAtT88,
                        currentState.IncludeGrassAtT107,
                        currentState.MandatoryBaseFaceTextureId);
            Id65AuthoringEditorControlStateCompilation compilation =
                Id65AuthoringEditorControlStateCompiler.Compile(nextState);
            string expectedEnvelope =
                Id65AuthoringTerrainExpectedEnvelopeOverrideForTesting?.Invoke(
                    current.EnvelopeCanonicalSha256) ??
                current.EnvelopeCanonicalSha256;

            _statusText.Text =
                $"ID65 {action}: publishing the canonical document with compare-and-swap…";
            Id65AuthoringEditorDocumentWriteResult written =
                await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
                    paths,
                    manifest,
                    nextState,
                    expectedEnvelope,
                    operation.Cancellation.Token);
            durableWrite = written;
            RequireCurrentId65BlankLabManualOperation(operation);
            RequireExactId65AuthoringDocumentWriteResult(written, compilation);
            ClearId65BlankLabRevealBinding();

            _statusText.Text =
                $"ID65 {action}: re-inspecting the durable document and preparing its exact fresh-scene projection…";
            (_, LevelLoadData? preparedLevel) =
                await ReinspectAndPrepareId65FreshProjectionAsync(
                    paths,
                    manifest,
                    written,
                    nextState,
                    operation);
            RequireCurrentId65BlankLabManualOperation(operation);
            ApplyPreparedId65FreshProjection(operation, preparedLevel);

            ClearId65BlankLabRevealBinding();
            InvalidateBuildSafetySummary();
            RefreshId65BlankLabUi();
            RefreshActionAvailability();
            _statusText.Text =
                $"{(includeStructuralTerrain ? "Created" : "Removed")} optional structural terrain through the canonical Full Authoring Document. " +
                (nextState.UsesRemotePadMobyChoice
                    ? $"Schema-{(nextState.UsesBlankCyclorama ? "6" : nextState.UsesRemotePadTerrainSet ? "5" : "4")} {Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(nextState.RemotePadMobyKind!.Value).Label} at retained {Id65RemotePadGrassT107Catalog.GetRequiredPosition(nextState.RemotePadMobyPositionId!).Label} was preserved. " +
                      (nextState.UsesBlankCyclorama
                          ? $"Blank Slate preset {nextState.BlankCycloramaPresetId} and exact color {nextState.BlankCycloramaColorHex} were preserved. "
                          : "")
                    : $"Grass T88 {(nextState.IncludeGrassAtT88 ? "included" : "omitted")} and Grass T107 {(nextState.IncludeGrassAtT107 ? "included" : "omitted")} were preserved. ") +
                $"Mandatory base face {Id65MandatoryBaseTextureLabel(nextState.MandatoryBaseFaceTextureId)} was preserved. " +
                "The viewport was replaced only after durable CAS save, exact readback, and projection succeeded. Generic HP-Z/paint/brush/add/remove/undo/save, normal Create BIN, runtime use, and release promotion remain unavailable.";
        }
        catch (OperationCanceledException exception)
        {
            if (durableWrite != null && recoveryPaths != null &&
                recoveryManifest != null &&
                IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: false))
            {
                _statusText.Text =
                    await RecoverId65PostWriteProjectionFailureAsync(
                        operation,
                        recoveryPaths,
                        recoveryManifest,
                        durableWrite,
                        exception,
                        action);
            }
        }
        catch (Exception exception)
        {
            if (durableWrite != null && recoveryPaths != null &&
                recoveryManifest != null &&
                IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: false))
            {
                _statusText.Text =
                    await RecoverId65PostWriteProjectionFailureAsync(
                        operation,
                        recoveryPaths,
                        recoveryManifest,
                        durableWrite,
                        exception,
                        action);
            }
            else if (IsCurrentId65BlankLabManualOperation(
                         operation,
                         requireSceneIdentity: false))
            {
                _statusText.Text =
                    $"Could not {action}: {exception.Message} The prior viewport remains active. Reload the Lab if the canonical document was changed by another operation.";
            }
        }
        finally
        {
            CompleteId65BlankLabManualOperation(operation);
        }
    }

    private async Task SetId65FreshTerrainTileAsync(
        int tileNumber,
        bool includeTile)
    {
        if (tileNumber is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(tileNumber));

        string action = $"{(includeTile ? "add" : "remove")} Terrain Tile {tileNumber}";
        if (!IsCurrentId65BlankLab() || _currentLevel == null ||
            !Id65FreshLevelSceneProjector.IsProjectionGeometry(_currentGeometry) ||
            _id65BlankLabPaths == null || _id65BlankLabManifest == null)
        {
            _statusText.Text =
                $"Load the exact ID65 Blank-Level Lab before using {action}.";
            return;
        }
        if (_id65FreshAuthoringDocument == null)
        {
            _statusText.Text =
                $"Could not {action}: no canonical Full Authoring Document is loaded. Use Start Blank-Level Editing… to create it explicitly; no default was persisted.";
            return;
        }
        if (!_id65FreshDocumentProjectionSynchronized)
        {
            _statusText.Text =
                $"Could not {action}: the canonical document and loaded fresh-scene projection are not synchronized. Reload the Lab first.";
            return;
        }
        if (HasUnsavedTerrainEdits() || HasUnsavedMobyEdits() ||
            HasUnsavedNativeMovementEdits() || HasUnsavedDragonRunToEdits())
        {
            _statusText.Text =
                $"Could not {action}: save, undo, or reload current scene edits first. No document or viewport state was changed.";
            return;
        }
        if (!TryBeginId65BlankLabManualOperation(
                action,
                out Id65BlankLabManualOperation? operation) ||
            operation == null)
        {
            return;
        }

        UnusedLevel65BlankLevelLabWorkspacePaths? recoveryPaths = null;
        UnusedLevel65BlankLevelLabManifest? recoveryManifest = null;
        Id65AuthoringEditorDocumentWriteResult? durableWrite = null;

        try
        {
            UnusedLevel65BlankLevelLabWorkspacePaths paths =
                UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
                    operation.WorkspaceRoot);
            UnusedLevel65BlankLevelLabManifest manifest =
                _id65BlankLabManifest ?? throw new InvalidDataException(
                    $"The {action} action lost its admitted ID65 manifest.");
            UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(
                manifest,
                paths);
            recoveryPaths = paths;
            recoveryManifest = manifest;
            RequireCurrentId65BlankLabManualOperation(operation);

            _statusText.Text =
                $"ID65 {action}: inspecting the current canonical schema-5/6 terrain-set document…";
            Id65AuthoringEditorDocumentReadModel current =
                await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                    paths,
                    manifest,
                    operation.Cancellation.Token) ?? throw new InvalidDataException(
                    "The canonical Full Authoring Document is missing. Use Start Blank-Level Editing… to create it explicitly.");
            RequireCurrentId65BlankLabManualOperation(operation);
            if (!Id65AuthoringDocumentSnapshotsEqual(
                    _id65FreshAuthoringDocument,
                    current))
            {
                throw new InvalidDataException(
                    "The canonical Full Authoring Document changed after this viewport was projected. Reload the Lab before changing a terrain tile.");
            }

            Id65AuthoringEditorControlState currentState =
                Id65AuthoringEditorControlStateCompiler.RecoverControlState(current);
            if (!currentState.UsesRemotePadTerrainSet)
            {
                throw new InvalidDataException(
                    "This document is not schema 5 or Blank Slate schema 6. Direct Tile 1/Tile 2 controls never migrate older documents. Use Edit Verified Moby Bundles… and explicitly migrate an eligible schema-4 document first.");
            }

            bool currentIncluded = tileNumber == 1
                ? currentState.IncludeTerrainTile1
                : currentState.IncludeTerrainTile2;
            if (currentIncluded == includeTile)
            {
                _statusText.Text = includeTile
                    ? $"Terrain Tile {tileNumber} is already present; no document or viewport state was changed."
                    : $"Terrain Tile {tileNumber} is already absent; no document or viewport state was changed.";
                return;
            }

            bool includeTile1 = tileNumber == 1
                ? includeTile
                : currentState.IncludeTerrainTile1;
            bool includeTile2 = tileNumber == 2
                ? includeTile
                : currentState.IncludeTerrainTile2;
            Id65AuthoringEditorControlState nextState =
                CreateId65TerrainSetMutationPreservingBlankCyclorama(
                        currentState,
                        includeTile1,
                        includeTile2,
                        currentState.RemotePadMobyKind!.Value,
                        currentState.RemotePadMobyPositionId!);
            Id65AuthoringEditorControlStateCompilation compilation =
                Id65AuthoringEditorControlStateCompiler.Compile(nextState);
            string expectedEnvelope =
                Id65AuthoringTerrainExpectedEnvelopeOverrideForTesting?.Invoke(
                    current.EnvelopeCanonicalSha256) ??
                current.EnvelopeCanonicalSha256;

            _statusText.Text =
                $"ID65 {action}: publishing the canonical schema-{(nextState.UsesBlankCyclorama ? "6" : "5")} document with compare-and-swap…";
            Id65AuthoringEditorDocumentWriteResult written =
                await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
                    paths,
                    manifest,
                    nextState,
                    expectedEnvelope,
                    operation.Cancellation.Token);
            durableWrite = written;
            RequireCurrentId65BlankLabManualOperation(operation);
            RequireExactId65AuthoringDocumentWriteResult(written, compilation);
            ClearId65BlankLabRevealBinding();

            _statusText.Text =
                $"ID65 {action}: re-inspecting the durable document and preparing its exact static fresh-scene projection…";
            (_, LevelLoadData? preparedLevel) =
                await ReinspectAndPrepareId65FreshProjectionAsync(
                    paths,
                    manifest,
                    written,
                    nextState,
                    operation);
            RequireCurrentId65BlankLabManualOperation(operation);
            ApplyPreparedId65FreshProjection(operation, preparedLevel);

            ClearId65BlankLabRevealBinding();
            InvalidateBuildSafetySummary();
            RefreshId65BlankLabUi();
            RefreshActionAvailability();
            Id65RemotePadMobyChoice preservedChoice =
                Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(
                    nextState.RemotePadMobyKind!.Value);
            Id65RemotePadGrassT107Position preservedPosition =
                Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                    nextState.RemotePadMobyPositionId!);
            _statusText.Text =
                $"{(includeTile ? "Added" : "Removed")} Terrain Tile {tileNumber} through the canonical schema-{(nextState.UsesBlankCyclorama ? "6" : "5")} Full Authoring Document. " +
                $"Tile 1 is {(nextState.IncludeTerrainTile1 ? "present" : "absent")}; Tile 2 is {(nextState.IncludeTerrainTile2 ? "present" : "absent")}. " +
                (nextState.UsesBlankCyclorama
                    ? $"Blank Slate preset {nextState.BlankCycloramaPresetId} and exact color {nextState.BlankCycloramaColorHex} were preserved. "
                    : "") +
                $"Moby {preservedChoice.Label} at retained {preservedPosition.Label} and mandatory T66 were preserved exactly. " +
                "The viewport was replaced only after durable CAS save, exact readback, and full static reprojection succeeded. The separate guarded offline candidate is eligible; normal Create BIN, runtime proof, and release remain locked.";
        }
        catch (OperationCanceledException exception)
        {
            if (durableWrite != null && recoveryPaths != null &&
                recoveryManifest != null &&
                IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: false))
            {
                _statusText.Text =
                    await RecoverId65PostWriteProjectionFailureAsync(
                        operation,
                        recoveryPaths,
                        recoveryManifest,
                        durableWrite,
                        exception,
                        action);
            }
        }
        catch (Exception exception)
        {
            if (durableWrite != null && recoveryPaths != null &&
                recoveryManifest != null &&
                IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: false))
            {
                _statusText.Text =
                    await RecoverId65PostWriteProjectionFailureAsync(
                        operation,
                        recoveryPaths,
                        recoveryManifest,
                        durableWrite,
                        exception,
                        action);
            }
            else if (IsCurrentId65BlankLabManualOperation(
                         operation,
                         requireSceneIdentity: false))
            {
                _statusText.Text =
                    $"Could not {action}: {exception.Message} The prior viewport remains active. Reload the Lab if the canonical document was changed by another operation.";
            }
        }
        finally
        {
            CompleteId65BlankLabManualOperation(operation);
        }
    }

    private async Task SetId65FreshRemotePadGrassAsync(
        bool includeGrassAtT107)
    {
        string action = includeGrassAtT107
            ? "add or move Grass T107"
            : "remove Grass T107";
        if (!IsCurrentId65BlankLab() || _currentLevel == null ||
            !Id65FreshLevelSceneProjector.IsProjectionGeometry(_currentGeometry) ||
            _id65BlankLabPaths == null || _id65BlankLabManifest == null)
        {
            _statusText.Text =
                $"Load the exact ID65 Blank-Level Lab before using {action}.";
            return;
        }
        if (_id65FreshAuthoringDocument == null)
        {
            _statusText.Text =
                $"Could not {action}: no canonical Full Authoring Document is loaded. Use Edit Verified Moby Bundles… to create it explicitly; no default was persisted.";
            return;
        }
        if (!_id65FreshDocumentProjectionSynchronized)
        {
            _statusText.Text =
                $"Could not {action}: the canonical document and loaded fresh-scene projection are not synchronized. Reload the Lab first.";
            return;
        }
        if (HasUnsavedTerrainEdits() || HasUnsavedMobyEdits() ||
            HasUnsavedNativeMovementEdits() || HasUnsavedDragonRunToEdits())
        {
            _statusText.Text =
                $"Could not {action}: save, undo, or reload current scene edits first. No document or viewport state was changed.";
            return;
        }

        Id65RemotePadGrassT107Position? requestedPosition = null;
        if (includeGrassAtT107)
        {
            requestedPosition =
                _id65FreshGrassT107PositionSelector?.SelectedItem as
                    Id65RemotePadGrassT107Position;
            if (requestedPosition == null ||
                !ReferenceEquals(
                    requestedPosition,
                    Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                        requestedPosition.PositionId)))
            {
                _statusText.Text =
                    "Could not add or move Grass T107: choose one exact verified remote-pad position first. No document or viewport state was changed.";
                return;
            }
        }

        if (!TryBeginId65BlankLabManualOperation(
                action,
                out Id65BlankLabManualOperation? operation) ||
            operation == null)
        {
            return;
        }

        UnusedLevel65BlankLevelLabWorkspacePaths? recoveryPaths = null;
        UnusedLevel65BlankLevelLabManifest? recoveryManifest = null;
        Id65AuthoringEditorDocumentWriteResult? durableWrite = null;

        try
        {
            UnusedLevel65BlankLevelLabWorkspacePaths paths =
                UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
                    operation.WorkspaceRoot);
            UnusedLevel65BlankLevelLabManifest manifest =
                _id65BlankLabManifest ?? throw new InvalidDataException(
                    "The Grass T107 action lost its admitted ID65 manifest.");
            UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(
                manifest,
                paths);
            recoveryPaths = paths;
            recoveryManifest = manifest;
            RequireCurrentId65BlankLabManualOperation(operation);

            _statusText.Text =
                $"ID65 {action}: inspecting the current canonical document…";
            Id65AuthoringEditorDocumentReadModel current =
                await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                    paths,
                    manifest,
                    operation.Cancellation.Token) ?? throw new InvalidDataException(
                    "The canonical Full Authoring Document is missing. Use Edit Verified Moby Bundles… to create it explicitly.");
            RequireCurrentId65BlankLabManualOperation(operation);
            if (!Id65AuthoringDocumentSnapshotsEqual(
                    _id65FreshAuthoringDocument,
                    current))
            {
                throw new InvalidDataException(
                    "The canonical Full Authoring Document changed after this viewport was projected. Reload the Lab before changing Grass T107.");
            }

            Id65AuthoringEditorControlState currentState =
                Id65AuthoringEditorControlStateCompiler.RecoverControlState(current);
            if (!currentState.UsesRemotePadT107Grid)
            {
                throw new InvalidDataException(
                    "This is a legacy native-apron T107 document. Direct Object Manager controls never migrate it. Use Edit Verified Moby Bundles… and explicitly choose the remote-pad migration first.");
            }

            string nextPositionId = includeGrassAtT107
                ? requestedPosition!.PositionId
                : currentState.T107RemotePadPositionId!;
            Id65RemotePadGrassT107Position nextPosition =
                Id65RemotePadGrassT107Catalog.GetRequiredPosition(nextPositionId);
            if (currentState.IncludeGrassAtT107 == includeGrassAtT107 &&
                string.Equals(
                    currentState.T107RemotePadPositionId,
                    nextPosition.PositionId,
                    StringComparison.Ordinal))
            {
                _statusText.Text = includeGrassAtT107
                    ? $"Grass T107 is already present at {nextPosition.Label}; no document or viewport state was changed."
                    : $"Grass T107 is already absent and dormant {nextPosition.Label} was retained; no document or viewport state was changed.";
                return;
            }

            Id65AuthoringEditorControlState nextState =
                Id65AuthoringEditorControlState.CreateRemotePad(
                    currentState.IncludeStructuralTerrain,
                    currentState.IncludeGrassAtT88,
                    includeGrassAtT107,
                    nextPosition.PositionId,
                    currentState.MandatoryBaseFaceTextureId);
            Id65AuthoringEditorControlStateCompilation compilation =
                Id65AuthoringEditorControlStateCompiler.Compile(nextState);

            _statusText.Text =
                $"ID65 {action}: publishing the canonical document with compare-and-swap…";
            Id65AuthoringEditorDocumentWriteResult written =
                await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
                    paths,
                    manifest,
                    nextState,
                    current.EnvelopeCanonicalSha256,
                    operation.Cancellation.Token);
            durableWrite = written;
            RequireCurrentId65BlankLabManualOperation(operation);
            RequireExactId65AuthoringDocumentWriteResult(written, compilation);
            ClearId65BlankLabRevealBinding();

            _statusText.Text =
                $"ID65 {action}: re-inspecting the durable document and preparing its exact fresh-scene projection…";
            (_, LevelLoadData? preparedLevel) =
                await ReinspectAndPrepareId65FreshProjectionAsync(
                    paths,
                    manifest,
                    written,
                    nextState,
                    operation);
            RequireCurrentId65BlankLabManualOperation(operation);
            ApplyPreparedId65FreshProjection(operation, preparedLevel);

            ClearId65BlankLabRevealBinding();
            InvalidateBuildSafetySummary();
            RefreshId65BlankLabUi();
            RefreshActionAvailability();
            _statusText.Text = includeGrassAtT107
                ? $"{(currentState.IncludeGrassAtT107 ? "Moved" : "Added")} the one verified Grass T107 bundle to {nextPosition.Label} through the canonical Full Authoring Document. " +
                  $"Structural terrain {(nextState.IncludeStructuralTerrain ? "included" : "omitted")}, Grass T88 {(nextState.IncludeGrassAtT88 ? "included" : "omitted")}, and mandatory base face {Id65MandatoryBaseTextureLabel(nextState.MandatoryBaseFaceTextureId)} were preserved. " +
                  "The viewport was replaced only after durable CAS save, exact readback, and projection succeeded. Arbitrary Mobys, drag placement, normal Create BIN, runtime use, and release promotion remain unavailable."
                : $"Removed the one verified Grass T107 bundle through the canonical Full Authoring Document. Dormant {nextPosition.Label} was retained. " +
                  $"Structural terrain {(nextState.IncludeStructuralTerrain ? "included" : "omitted")}, Grass T88 {(nextState.IncludeGrassAtT88 ? "included" : "omitted")}, and mandatory base face {Id65MandatoryBaseTextureLabel(nextState.MandatoryBaseFaceTextureId)} were preserved. " +
                  "The viewport was replaced only after durable CAS save, exact readback, and projection succeeded. Arbitrary Mobys, normal Create BIN, runtime use, and release promotion remain unavailable.";
        }
        catch (OperationCanceledException exception)
        {
            if (durableWrite != null && recoveryPaths != null &&
                recoveryManifest != null &&
                IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: false))
            {
                _statusText.Text =
                    await RecoverId65PostWriteProjectionFailureAsync(
                        operation,
                        recoveryPaths,
                        recoveryManifest,
                        durableWrite,
                        exception,
                        action);
            }
        }
        catch (Exception exception)
        {
            if (durableWrite != null && recoveryPaths != null &&
                recoveryManifest != null &&
                IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: false))
            {
                _statusText.Text =
                    await RecoverId65PostWriteProjectionFailureAsync(
                        operation,
                        recoveryPaths,
                        recoveryManifest,
                        durableWrite,
                        exception,
                        action);
            }
            else if (IsCurrentId65BlankLabManualOperation(
                         operation,
                         requireSceneIdentity: false))
            {
                _statusText.Text =
                    $"Could not {action}: {exception.Message} The prior viewport remains active. Reload the Lab if the canonical document was changed by another operation.";
            }
        }
        finally
        {
            CompleteId65BlankLabManualOperation(operation);
        }
    }

    private async Task SetId65FreshRemotePadMobyChoiceAsync()
    {
        const string action = "apply remote-pad Moby choice";
        if (!IsCurrentId65BlankLab() || _currentLevel == null ||
            !Id65FreshLevelSceneProjector.IsProjectionGeometry(_currentGeometry) ||
            _id65BlankLabPaths == null || _id65BlankLabManifest == null)
        {
            _statusText.Text =
                "Load the exact ID65 Blank-Level Lab before applying a Moby choice.";
            return;
        }
        if (_id65FreshAuthoringDocument == null)
        {
            _statusText.Text =
                "Could not apply the Moby choice: no canonical Full Authoring Document is loaded. Use Edit Verified Moby Bundles… to create or migrate it explicitly; no default was persisted.";
            return;
        }
        if (!_id65FreshDocumentProjectionSynchronized)
        {
            _statusText.Text =
                "Could not apply the Moby choice: the canonical document and loaded fresh-scene projection are not synchronized. Reload the Lab first.";
            return;
        }
        if (HasUnsavedTerrainEdits() || HasUnsavedMobyEdits() ||
            HasUnsavedNativeMovementEdits() || HasUnsavedDragonRunToEdits())
        {
            _statusText.Text =
                "Could not apply the Moby choice: save, undo, or reload current scene edits first. No document or viewport state was changed.";
            return;
        }

        Id65RemotePadMobyChoice? requestedChoice =
            _id65FreshMobyChoiceSelector?.SelectedItem as
                Id65RemotePadMobyChoice;
        Id65RemotePadGrassT107Position? requestedPosition =
            _id65FreshMobyChoicePositionSelector?.SelectedItem as
                Id65RemotePadGrassT107Position;
        if (requestedChoice == null ||
            !ReferenceEquals(
                requestedChoice,
                Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(
                    requestedChoice.Kind)) ||
            requestedPosition == null ||
            !ReferenceEquals(
                requestedPosition,
                Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                    requestedPosition.PositionId)) ||
            Id65RemotePadMobyChoiceCatalog.GetRequiredPositionId(
                requestedPosition.PositionId) != requestedPosition.PositionId)
        {
            _statusText.Text =
                "Could not apply the Moby choice: choose one exact None/Grass/Red Gem entry and one verified position first. No document or viewport state was changed.";
            return;
        }

        if (!TryBeginId65BlankLabManualOperation(
                action,
                out Id65BlankLabManualOperation? operation) ||
            operation == null)
        {
            return;
        }

        UnusedLevel65BlankLevelLabWorkspacePaths? recoveryPaths = null;
        UnusedLevel65BlankLevelLabManifest? recoveryManifest = null;
        Id65AuthoringEditorDocumentWriteResult? durableWrite = null;

        try
        {
            UnusedLevel65BlankLevelLabWorkspacePaths paths =
                UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
                    operation.WorkspaceRoot);
            UnusedLevel65BlankLevelLabManifest manifest =
                _id65BlankLabManifest ?? throw new InvalidDataException(
                    "The Moby choice action lost its admitted ID65 manifest.");
            UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(
                manifest,
                paths);
            recoveryPaths = paths;
            recoveryManifest = manifest;
            RequireCurrentId65BlankLabManualOperation(operation);

            _statusText.Text =
                "ID65 Moby choice: inspecting the current canonical document…";
            Id65AuthoringEditorDocumentReadModel current =
                await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                    paths,
                    manifest,
                    operation.Cancellation.Token) ?? throw new InvalidDataException(
                    "The canonical Full Authoring Document is missing. Use Edit Verified Moby Bundles… to create or migrate it explicitly.");
            RequireCurrentId65BlankLabManualOperation(operation);
            if (!Id65AuthoringDocumentSnapshotsEqual(
                    _id65FreshAuthoringDocument,
                    current))
            {
                throw new InvalidDataException(
                    "The canonical Full Authoring Document changed after this viewport was projected. Reload the Lab before applying a Moby choice.");
            }

            Id65AuthoringEditorControlState currentState =
                Id65AuthoringEditorControlStateCompiler.RecoverControlState(current);
            if (!currentState.UsesRemotePadMobyChoice)
            {
                throw new InvalidDataException(
                    "This document is not schema 4. Direct None/Grass/Red Gem controls never migrate it. Use Edit Verified Moby Bundles… and explicitly migrate the eligible schema-3 T66/T88-off document first.");
            }
            if (currentState.RemotePadMobyKind == requestedChoice.Kind &&
                string.Equals(
                    currentState.RemotePadMobyPositionId,
                    requestedPosition.PositionId,
                    StringComparison.Ordinal))
            {
                _statusText.Text =
                    $"Schema-{(currentState.UsesBlankCyclorama ? "6" : currentState.UsesRemotePadTerrainSet ? "5" : "4")} {requestedChoice.Label} at retained {requestedPosition.Label} is already the canonical choice; no document or viewport state was changed.";
                return;
            }

            Id65AuthoringEditorControlState nextState =
                currentState.UsesRemotePadTerrainSet
                    ? CreateId65TerrainSetMutationPreservingBlankCyclorama(
                            currentState,
                            currentState.IncludeTerrainTile1,
                            currentState.IncludeTerrainTile2,
                            requestedChoice.Kind,
                            requestedPosition.PositionId)
                    : Id65AuthoringEditorControlState.CreateRemotePadMobyChoice(
                        currentState.IncludeStructuralTerrain,
                        requestedChoice.Kind,
                        requestedPosition.PositionId);
            Id65AuthoringEditorControlStateCompilation compilation =
                Id65AuthoringEditorControlStateCompiler.Compile(nextState);
            string expectedEnvelope =
                Id65AuthoringMobyExpectedEnvelopeOverrideForTesting?.Invoke(
                    current.EnvelopeCanonicalSha256) ??
                current.EnvelopeCanonicalSha256;

            _statusText.Text =
                $"ID65 schema-{(nextState.UsesBlankCyclorama ? "6" : nextState.UsesRemotePadTerrainSet ? "5" : "4")} Moby choice: publishing the canonical document with compare-and-swap…";
            Id65AuthoringEditorDocumentWriteResult written =
                await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
                    paths,
                    manifest,
                    nextState,
                    expectedEnvelope,
                    operation.Cancellation.Token);
            durableWrite = written;
            RequireCurrentId65BlankLabManualOperation(operation);
            RequireExactId65AuthoringDocumentWriteResult(written, compilation);
            ClearId65BlankLabRevealBinding();

            _statusText.Text =
                $"ID65 schema-{(nextState.UsesBlankCyclorama ? "6" : nextState.UsesRemotePadTerrainSet ? "5" : "4")} Moby choice: re-inspecting the durable document and preparing its exact static fresh-scene projection…";
            (_, LevelLoadData? preparedLevel) =
                await ReinspectAndPrepareId65FreshProjectionAsync(
                    paths,
                    manifest,
                    written,
                    nextState,
                    operation);
            RequireCurrentId65BlankLabManualOperation(operation);
            ApplyPreparedId65FreshProjection(operation, preparedLevel);

            ClearId65BlankLabRevealBinding();
            InvalidateBuildSafetySummary();
            RefreshId65BlankLabUi();
            RefreshActionAvailability();
            _statusText.Text =
                $"Applied schema-{(nextState.UsesBlankCyclorama ? "6" : nextState.UsesRemotePadTerrainSet ? "5" : "4")} {requestedChoice.Label} at retained {requestedPosition.Label} through the canonical Full Authoring Document. " +
                (nextState.UsesRemotePadTerrainSet
                    ? $"Terrain Tile 1 {(nextState.IncludeTerrainTile1 ? "included" : "omitted")} and Terrain Tile 2 {(nextState.IncludeTerrainTile2 ? "included" : "omitted")} were preserved. "
                    : $"Structural terrain {(nextState.IncludeStructuralTerrain ? "included" : "omitted")} was preserved. ") +
                (nextState.UsesBlankCyclorama
                    ? $"Blank Slate preset {nextState.BlankCycloramaPresetId} and exact color {nextState.BlankCycloramaColorHex} were preserved. "
                    : "") +
                "Mandatory T66 and T88 exclusion were preserved. " +
                (nextState.UsesRemotePadTerrainSet
                    ? "The viewport was replaced only after durable CAS save, exact readback, and static projection succeeded. The separate guarded offline candidate is eligible; arbitrary Mobys and drag placement remain locked, and normal Create BIN, runtime proof, and release remain locked."
                    : "The viewport was replaced only after durable CAS save, exact readback, and static projection succeeded. Physical materialization, arbitrary Mobys, drag placement, normal Create BIN, runtime use, release promotion, and publication remain unavailable.");
        }
        catch (OperationCanceledException exception)
        {
            if (durableWrite != null && recoveryPaths != null &&
                recoveryManifest != null &&
                IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: false))
            {
                _statusText.Text =
                    await RecoverId65PostWriteProjectionFailureAsync(
                        operation,
                        recoveryPaths,
                        recoveryManifest,
                        durableWrite,
                        exception,
                        action);
            }
        }
        catch (Exception exception)
        {
            if (durableWrite != null && recoveryPaths != null &&
                recoveryManifest != null &&
                IsCurrentId65BlankLabManualOperation(
                    operation,
                    requireSceneIdentity: false))
            {
                _statusText.Text =
                    await RecoverId65PostWriteProjectionFailureAsync(
                        operation,
                        recoveryPaths,
                        recoveryManifest,
                        durableWrite,
                        exception,
                        action);
            }
            else if (IsCurrentId65BlankLabManualOperation(
                         operation,
                         requireSceneIdentity: false))
            {
                _statusText.Text =
                    $"Could not apply the Moby choice: {exception.Message} The prior viewport remains active. Reload the Lab if the canonical document was changed by another operation.";
            }
        }
        finally
        {
            CompleteId65BlankLabManualOperation(operation);
        }
    }

    private async Task CreateId65BlankLabDisposableCueAsync()
    {
        if (!IsCurrentId65BlankLab())
        {
            _statusText.Text = "Load the validated ID65 lab before creating its disposable test CUE.";
            return;
        }
        if (Id65BlankLabHasCanonicalAuthoringDocument())
        {
            _statusText.Text =
                "The legacy ID65 disposable test CUE is disabled because it does not consume the canonical Full Authoring Document. Use Edit Full Authoring Document… to inspect or update that exact layer, then Create Exact Full Candidate for the separately guarded offline artifact; normal Create BIN remains blocked pending runtime and atomic retail-composition proof.";
            return;
        }
        if (Id65FreshLevelSceneProjector.IsProjectionGeometry(_currentGeometry))
        {
            _statusText.Text =
                "The legacy ID65 disposable terrain-only CUE is unavailable for the fresh-level scene because it cannot apply a coupled HP/LP/collision/occlusion/spawn transaction. Use Edit Full Authoring Document… and Create Exact Full Candidate for the separately guarded offline artifact. This does not authorize normal Create BIN, runtime use, or release promotion.";
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
                Id65FreshLevelSceneProjector.TerrainEditFileName);
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
            _id65BlankLabRevealDocumentEnvelopeSha256 = "";
            _id65BlankLabRevealDocumentCanonicalSha256 = "";
            RefreshId65BlankLabUi();
            await RevealId65BlankLabOutputAsync();
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

    private void ClearId65BlankLabRevealBinding()
    {
        _id65BlankLabRevealPath = "";
        _id65BlankLabRevealDocumentEnvelopeSha256 = "";
        _id65BlankLabRevealDocumentCanonicalSha256 = "";
    }

    private async Task RevealId65BlankLabOutputAsync()
    {
        if (string.IsNullOrWhiteSpace(_id65BlankLabRevealPath) ||
            !File.Exists(_id65BlankLabRevealPath))
        {
            _statusText.Text =
                "Create an exact full-authoring candidate or a safely routeable disposable ID65 test CUE before revealing it.";
            return;
        }

        bool revealingBlankSlateCandidate = false;
        bool candidateBound =
            IsCanonicalLowerSha256(_id65BlankLabRevealDocumentEnvelopeSha256) &&
            IsCanonicalLowerSha256(_id65BlankLabRevealDocumentCanonicalSha256);
        if (candidateBound || Id65BlankLabHasCanonicalAuthoringDocument())
        {
            try
            {
                if (!candidateBound || !IsCurrentId65BlankLab() ||
                    !_id65FreshDocumentProjectionSynchronized ||
                    _id65FreshAuthoringDocument == null ||
                    _id65BlankLabPaths == null || _id65BlankLabManifest == null)
                {
                    throw new InvalidDataException(
                        "The reveal path is not bound to one current synchronized Proved private T66 document and viewport.");
                }

                Id65AuthoringEditorControlState state =
                    Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                        _id65FreshAuthoringDocument);
                bool revealableBlankSlate =
                    state.UsesBlankCyclorama &&
                    state.BlankCycloramaPresetId ==
                        Id65BlankSlatePresetId &&
                    state.BlankCycloramaColorHex ==
                        Id65BlankSlateColorHex &&
                    state.MandatoryBaseFaceTextureId ==
                        Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId &&
                    !state.IncludeGrassAtT88;
                bool revealableTerrainSet =
                    !state.UsesBlankCyclorama &&
                    state.UsesRemotePadTerrainSet &&
                    state.UsesRemotePadMobyChoice &&
                    state.MandatoryBaseFaceTextureId ==
                        Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId &&
                    !state.IncludeGrassAtT88;
                bool revealableRemotePad =
                    state.UsesRemotePadT107Grid &&
                    state.MandatoryBaseFaceTextureId ==
                        Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId &&
                    !state.IncludeGrassAtT88;
                bool revealableLegacy =
                    !state.UsesRemotePadT107Grid &&
                    !state.UsesRemotePadMobyChoice &&
                    _id65FreshAuthoringDocument.SchemaVersion ==
                        Id65LegacyT66DocumentSchemaVersion &&
                    _id65FreshAuthoringDocument.ProfileId ==
                        Id65LegacyT66DocumentProfileId &&
                    state.MandatoryBaseFaceTextureId ==
                        Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId &&
                    state.IncludeStructuralTerrain &&
                    state.IncludeGrassAtT88 &&
                    state.IncludeGrassAtT107;
                if (!revealableBlankSlate &&
                    !revealableTerrainSet &&
                    !revealableRemotePad &&
                    !revealableLegacy)
                {
                    throw new InvalidDataException(
                        "Only a verified Blank Slate schema-6 state, schema-5 terrain-set/Moby state, schema-3 T88-off/T66 terrain-and-Grass combination, or the full legacy T66 candidate state has a revealable candidate. Schema 4, Resident T25, and unsupported documents are refused.");
                }
                if (!_viewport.IsId65FreshLevelT66ViewportTextureProjectionBoundForTesting)
                {
                    throw new InvalidDataException(
                        "The Proved private T66 viewport projection is not bound.");
                }

                Id65AuthoringEditorDocumentReadModel current =
                    await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                        _id65BlankLabPaths,
                        _id65BlankLabManifest) ?? throw new InvalidDataException(
                        "The canonical Full Authoring Document is missing.");
                if (!Id65AuthoringDocumentSnapshotsEqual(
                        _id65FreshAuthoringDocument,
                        current) ||
                    current.EnvelopeCanonicalSha256 !=
                        _id65BlankLabRevealDocumentEnvelopeSha256 ||
                    current.DocumentCanonicalSha256 !=
                        _id65BlankLabRevealDocumentCanonicalSha256)
                {
                    throw new InvalidDataException(
                        "The canonical Full Authoring Document changed after candidate materialization.");
                }
                revealingBlankSlateCandidate = revealableBlankSlate;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or
                    InvalidDataException or InvalidOperationException or JsonException)
            {
                ClearId65BlankLabRevealBinding();
                RefreshId65BlankLabUi();
                _statusText.Text =
                    $"Could not reveal the exact ID65 candidate: {exception.Message} Recreate it from the current synchronized Proved private T66 document.";
                return;
            }
        }

        try
        {
            RevealFileInPlatformFileManager(_id65BlankLabRevealPath);
            string fileManager = OperatingSystem.IsMacOS()
                ? "Finder"
                : OperatingSystem.IsWindows() ? "Explorer" : "the file manager";
            _statusText.Text = revealingBlankSlateCandidate
                ? $"Revealed the exact Blank Slate schema-6 candidate CUE in {fileManager}: {_id65BlankLabRevealPath}"
                : $"Revealed the exact ID65 CUE in {fileManager}: {_id65BlankLabRevealPath}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _statusText.Text = $"Could not reveal the ID65 test CUE: {ex.Message}";
        }
    }

    private void RefreshId65BlankLabUi()
    {
        ApplyEditorExperienceForCurrentLevel();
        bool valid = _id65BlankLabManifest != null && _id65BlankLabPaths != null;
        bool active = IsCurrentId65BlankLab();
        bool freshProjectionActive =
            active &&
            Id65FreshLevelSceneProjector.IsProjectionGeometry(_currentGeometry);
        bool hasAuthoringDocument = Id65BlankLabHasCanonicalAuthoringDocument();
        Id65AuthoringEditorControlState? synchronizedAuthoringState = null;
        if (active && hasAuthoringDocument &&
            _id65FreshDocumentProjectionSynchronized &&
            _id65FreshAuthoringDocument != null)
        {
            try
            {
                synchronizedAuthoringState =
                    Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                        _id65FreshAuthoringDocument);
            }
            catch (Exception exception) when (
                exception is InvalidDataException or InvalidOperationException)
            {
                Debug.WriteLine(exception);
            }
        }
        bool residentT25Selected = synchronizedAuthoringState?.MandatoryBaseFaceTextureId ==
            Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId;
        bool privateT66Selected = synchronizedAuthoringState?.MandatoryBaseFaceTextureId ==
            Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId;
        bool remotePadT107Selected =
            synchronizedAuthoringState?.UsesRemotePadT107Grid == true;
        bool remotePadMobyChoiceSelected =
            synchronizedAuthoringState?.UsesRemotePadMobyChoice == true;
        bool remotePadTerrainSetSelected =
            synchronizedAuthoringState?.UsesRemotePadTerrainSet == true;
        bool blankSlateSelected =
            synchronizedAuthoringState?.UsesBlankCyclorama == true;
        bool legacyPrivateT66CandidateReady = privateT66Selected &&
            !remotePadT107Selected &&
            !remotePadMobyChoiceSelected &&
            synchronizedAuthoringState!.IncludeStructuralTerrain &&
            synchronizedAuthoringState.IncludeGrassAtT88 &&
            synchronizedAuthoringState.IncludeGrassAtT107;
        bool remotePadPrivateT66CandidateReady = privateT66Selected &&
            remotePadT107Selected &&
            !remotePadMobyChoiceSelected &&
            !synchronizedAuthoringState!.IncludeGrassAtT88;
        bool terrainSetPrivateT66CandidateReady = privateT66Selected &&
            remotePadMobyChoiceSelected &&
            remotePadTerrainSetSelected &&
            !synchronizedAuthoringState!.IncludeGrassAtT88;
        bool exactCandidateReady =
            legacyPrivateT66CandidateReady ||
            remotePadPrivateT66CandidateReady ||
            terrainSetPrivateT66CandidateReady;
        bool hasUnsupported = TryGetId65BlankLabUnsupportedArtifact(out string unsupported);
        bool disposableTerrainReady =
            active &&
            _currentGeometry != null &&
            TryValidateId65BlankLabTerrainForDisposableCue(_currentGeometry, out _);
        _id65BlankLabStatusText.Text = _id65BlankLabCatalogValidationBusy
            ? "Checking the existing ID65 locked base in the background. The editor remains available while its exact manifest and BIN identity are validated."
            : valid
            ? "Validated evidence-bound lab. ID65 is admitted only in memory; the retail catalog file remains exactly 35 levels. Exact full-document candidates use the separate offline materializer; normal Create BIN still does not include this lab."
            : "Setup required: choose the clean Spyro CUE/BIN, then build the versioned locked base. " +
              "ID65 stays out of the level picker until its manifest and base pass exact validation." +
              (string.IsNullOrWhiteSpace(_id65BlankLabValidationFailure)
                  ? ""
                  : $" Validation: {_id65BlankLabValidationFailure}");
        _id65BlankLabLockedBaseText.Text = valid
            ? $"Profile v{_id65BlankLabManifest!.ProfileVersion}; exact BIN {_id65BlankLabManifest.LockedBaseImageSha256[..12]}...; {Path.GetFileName(_id65BlankLabPaths!.LockedBaseCuePath)}."
            : "Not built or not validated.";
        _id65BlankLabAuthoredLayerText.Text = valid
            ? hasAuthoringDocument
                ? remotePadMobyChoiceSelected
                    ? remotePadTerrainSetSelected
                        ? blankSlateSelected
                            ? $"Canonical Blank Slate schema-6 document is synchronized with the fresh Level Building Editor: preset {synchronizedAuthoringState!.BlankCycloramaPresetId}, exact color {synchronizedAuthoringState.BlankCycloramaColorHex}; Terrain Tile 1 {(synchronizedAuthoringState.IncludeTerrainTile1 ? "present" : "absent")}; Terrain Tile 2 {(synchronizedAuthoringState.IncludeTerrainTile2 ? "present" : "absent")}; {Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(synchronizedAuthoringState.RemotePadMobyKind!.Value).Label} at retained {Id65RemotePadGrassT107Catalog.GetRequiredPosition(synchronizedAuthoringState.RemotePadMobyPositionId!).Label}. This exact state can build the separately guarded offline Blank Slate schema-6 candidate. Normal Create BIN, runtime proof, release integration, promotion, and publication remain unavailable."
                            : $"Canonical schema-5 terrain-set/Moby document is synchronized with the fresh Level Building Editor: Terrain Tile 1 {(synchronizedAuthoringState!.IncludeTerrainTile1 ? "present" : "absent")}; Terrain Tile 2 {(synchronizedAuthoringState.IncludeTerrainTile2 ? "present" : "absent")}; {Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(synchronizedAuthoringState.RemotePadMobyKind!.Value).Label} at retained {Id65RemotePadGrassT107Catalog.GetRequiredPosition(synchronizedAuthoringState.RemotePadMobyPositionId!).Label}; Proved private T66 is fixed and Grass T88 is excluded. This exact state can build the separately guarded offline schema-5 candidate. Normal Create BIN, runtime proof, release integration, promotion, and publication remain unavailable."
                        : $"Canonical schema-4 remote-pad Moby document is synchronized with the fresh Object Manager: {Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(synchronizedAuthoringState!.RemotePadMobyKind!.Value).Label} at retained {Id65RemotePadGrassT107Catalog.GetRequiredPosition(synchronizedAuthoringState.RemotePadMobyPositionId!).Label}; {(synchronizedAuthoringState.IncludeStructuralTerrain ? "structural terrain is present" : "the terrain state is blank")}; Proved private T66 is fixed; Grass T88 is excluded. This is a canonical-document and static-presentation route only. Physical materialization, exact candidate creation, normal Create BIN, runtime proof, release integration, promotion, and publication remain unavailable."
                    : remotePadT107Selected
                    ? remotePadPrivateT66CandidateReady
                        ? $"Canonical schema-3 remote-pad document is synchronized with the fresh Object Manager. {(synchronizedAuthoringState!.IncludeStructuralTerrain ? "Structural terrain is present" : "The terrain state is blank")}; Proved private T66 is selected; {(synchronizedAuthoringState.IncludeGrassAtT107 ? $"Grass T107 is present at {Id65RemotePadGrassT107Catalog.GetRequiredPosition(synchronizedAuthoringState.T107RemotePadPositionId!).Label}" : $"Grass T107 is omitted and its dormant {Id65RemotePadGrassT107Catalog.GetRequiredPosition(synchronizedAuthoringState.T107RemotePadPositionId!).Label} position is retained")}; T88 exclusion, repaired T13995 support, and protected complement are eligible for the separately guarded exact offline candidate. Normal Create BIN, runtime proof, release integration, promotion, and publication remain unavailable."
                        : "Canonical schema-3 remote-pad document is synchronized with the fresh Object Manager. Its exact offline candidate requires Proved private T66 and Grass T88 OFF; structural terrain and Grass T107 may each be on or off."
                    : residentT25Selected
                        ? "Canonical Full Authoring Document is synchronized with mandatory Resident T25. The viewport uses the ordinary resident close/normal/LQ/HQ caches. T25 is offline-authoring-only: exact candidate, normal Create BIN, runtime, release, promotion, and publication are refused. Edit and explicitly save Proved private T66 to reopen the separately guarded exact-candidate path."
                        : privateT66Selected
                            ? legacyPrivateT66CandidateReady
                                ? "Canonical Full Authoring Document is synchronized with mandatory Proved private T66 and the full structural-terrain/Grass T88/Grass T107 candidate state. The viewport uses the exact private T66 projection; Create Exact Full Candidate still revalidates every disk and graph identity. Legacy HP-Z save/test actions and normal Create BIN remain disabled."
                                : "Canonical Full Authoring Document is synchronized with mandatory Proved private T66, but the full exact candidate also requires structural terrain plus both Grass T88 and T107. The private viewport remains available; candidate action is disabled until those controls are explicitly saved. Normal Create BIN remains disabled."
                            : "Canonical Full Authoring Document filename detected, but no exact synchronized supported selector state is loaded. Candidate creation and reveal are quarantined until Load Lab re-inspects and projects the document."
                : $"Isolated by versioned manifest + unique key '{UnusedLevel65BlankLevelLabProfileRegistry.Key}'. " +
                  "The fresh scene is presentation-only: generic HP-Z/texture save and the legacy disposable writer are disabled because they would desynchronize HP, LP, collision, occlusion, and the protected spawn. Use Edit Full Authoring Document… for coupled terrain create/remove work." +
                  (hasUnsupported ? $" Disposable writer blocked: {unsupported}" : "")
            : "No admitted lab workspace. Normal Create BIN remains separate.";

        bool busy = _id65BlankLabBusy ||
            _id65BlankLabCatalogValidationBusy ||
            HasConflictingId65BlankLabManualOperationState();
        if (_id65BlankLabBuildButton != null)
            _id65BlankLabBuildButton.IsEnabled = !busy;
        if (_id65BlankLabLoadButton != null)
            _id65BlankLabLoadButton.IsEnabled = valid && !busy;
        if (_id65BlankLabAuthoringDocumentButton != null)
            _id65BlankLabAuthoringDocumentButton.IsEnabled = valid && !busy;
        if (_id65BlankLabAuthoringCandidateButton != null)
        {
            _id65BlankLabAuthoringCandidateButton.IsEnabled =
                valid && active && hasAuthoringDocument &&
                _id65FreshDocumentProjectionSynchronized &&
                exactCandidateReady && OperatingSystem.IsMacOS() && !busy;
            if (!busy)
            {
                _id65BlankLabAuthoringCandidateButton.Content =
                    blankSlateSelected
                    ? "Create Blank Slate schema-6 candidate"
                    : remotePadTerrainSetSelected
                    ? "Create Verified Schema-5 Candidate"
                    : remotePadMobyChoiceSelected
                    ? "Schema 4 Physical Candidate Not Implemented"
                    : remotePadPrivateT66CandidateReady
                    ? "Create Verified Blank-Level Candidate"
                    : residentT25Selected
                    ? "Exact Candidate Unavailable for Resident T25"
                    : remotePadT107Selected
                    ? "Exact Candidate Requires T88 Off + T66"
                    : privateT66Selected && !legacyPrivateT66CandidateReady
                        ? "Exact Candidate Requires Full T66 Document"
                    : "Create Exact Full Candidate";
            }
        }
        if (_id65BlankLabSaveButton != null)
            _id65BlankLabSaveButton.IsEnabled =
                valid && active && !busy && !hasAuthoringDocument &&
                !freshProjectionActive;
        if (_id65BlankLabCreateCueButton != null)
            _id65BlankLabCreateCueButton.IsEnabled =
                valid && active && !busy && !hasUnsupported &&
                disposableTerrainReady && !freshProjectionActive;
        if (_id65BlankLabRevealButton != null)
        {
            _id65BlankLabRevealButton.IsEnabled =
                !busy && File.Exists(_id65BlankLabRevealPath) &&
                (!hasAuthoringDocument ||
                    (privateT66Selected && exactCandidateReady));
            if (!busy)
            {
                _id65BlankLabRevealButton.Content = blankSlateSelected
                    ? "Reveal Blank Slate schema-6 Test CUE"
                    : "Reveal Test CUE";
            }
        }
        RefreshId65FreshObjectManagerUi();
        RefreshId65FreshOptionalTerrainUi();
    }

    private void RefreshId65FreshObjectManagerUi()
    {
        bool exactFreshRoute =
            IsCurrentId65BlankLab() &&
            Id65FreshLevelSceneProjector.IsProjectionGeometry(_currentGeometry);
        bool busy = _id65BlankLabBusy ||
            _id65BlankLabCatalogValidationBusy ||
            HasConflictingId65BlankLabManualOperationState();
        bool canOpenDocument =
            exactFreshRoute && _id65BlankLabManifest != null &&
            _id65BlankLabPaths != null && !busy;
        bool hasCanonicalDocumentEntry =
            Id65BlankLabHasCanonicalAuthoringDocument();
        _id65FreshGrassT107ControlsAvailable = false;
        _id65FreshMobyChoiceControlsAvailable = false;
        if (_id65FreshSchema3GrassControlsPanel != null)
            _id65FreshSchema3GrassControlsPanel.IsVisible = true;
        if (_id65FreshMobyChoiceControlsPanel != null)
            _id65FreshMobyChoiceControlsPanel.IsVisible = false;
        if (_id65FreshGrassT107PositionSelector != null)
            _id65FreshGrassT107PositionSelector.IsEnabled = false;
        if (_id65FreshMobyChoiceSelector != null)
            _id65FreshMobyChoiceSelector.IsEnabled = false;
        if (_id65FreshMobyChoicePositionSelector != null)
            _id65FreshMobyChoicePositionSelector.IsEnabled = false;
        RefreshId65FreshGrassT107ActionAvailability();
        RefreshId65FreshMobyChoiceActionAvailability();
        if (_id65FreshObjectManagerDocumentButton != null)
        {
            _id65FreshObjectManagerDocumentButton.Content =
                hasCanonicalDocumentEntry
                    ? "Edit Verified Moby Bundles…"
                    : "Start Blank-Level Editing…";
            _id65FreshObjectManagerDocumentButton.IsEnabled = false;
        }

        if (!exactFreshRoute)
        {
            _id65FreshObjectManagerStatusText.Text =
                "Load a validated fresh ID65 Blank-Level Lab to manage its verified Moby bundles.";
            return;
        }

        if (_id65FreshAuthoringDocument == null)
        {
            if (_id65FreshObjectManagerDocumentButton != null)
                _id65FreshObjectManagerDocumentButton.IsEnabled = canOpenDocument;
            _id65FreshObjectManagerStatusText.Text = hasCanonicalDocumentEntry
                ? "A canonical Full Authoring Document entry exists but is not synchronized with this blank presentation. Use Edit Verified Moby Bundles… to inspect it; direct object controls remain quarantined."
                : "Blank scene: 0 placed objects. No Full Authoring Document exists, so no object choice has been silently persisted. Choose Start Blank-Level Editing… to explicitly select the mandatory starter-pad texture and create the blank document.";
            return;
        }
        if (!_id65FreshDocumentProjectionSynchronized)
        {
            _id65FreshObjectManagerStatusText.Text =
                "The canonical document and loaded fresh-scene presentation are not synchronized. Reload the Lab before changing verified objects.";
            return;
        }

        Id65AuthoringEditorControlState state;
        try
        {
            state = Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                _id65FreshAuthoringDocument);
        }
        catch (Exception exception) when (
            exception is InvalidDataException or InvalidOperationException)
        {
            Debug.WriteLine(exception);
            _id65FreshObjectManagerStatusText.Text =
                "The canonical document is not synchronized with a supported Object Manager state. Reload the Lab before editing objects.";
            return;
        }

        if (state.UsesRemotePadMobyChoice)
        {
            if (_id65FreshSchema3GrassControlsPanel != null)
                _id65FreshSchema3GrassControlsPanel.IsVisible = false;
            if (_id65FreshMobyChoiceControlsPanel != null)
                _id65FreshMobyChoiceControlsPanel.IsVisible = true;
            Id65RemotePadMobyChoice choice =
                Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(
                    state.RemotePadMobyKind!.Value);
            Id65RemotePadGrassT107Position mobyChoicePosition =
                Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                    state.RemotePadMobyPositionId!);
            string mobySchema = state.UsesBlankCyclorama
                ? "6"
                : state.UsesRemotePadTerrainSet ? "5" : "4";
            if (_id65FreshMobyChoiceSelector != null)
                _id65FreshMobyChoiceSelector.SelectedItem = choice;
            if (_id65FreshMobyChoicePositionSelector != null)
                _id65FreshMobyChoicePositionSelector.SelectedItem =
                    mobyChoicePosition;

            bool loadedMobyChoicePresentationMatches;
            try
            {
                Id65FreshLevelSceneProjection projection =
                    Id65FreshLevelSceneProjector.ProjectExact(
                        _currentLevel ?? throw new InvalidDataException(
                            $"The schema-{mobySchema} Object Manager lost its fresh level identity."),
                        _id65FreshAuthoringDocument);
                loadedMobyChoicePresentationMatches =
                    projection.RemotePadMobyKind == state.RemotePadMobyKind &&
                    Id65PreparedMobysMatchProjection(_currentMobys, projection);
            }
            catch (Exception exception) when (
                exception is InvalidDataException or InvalidOperationException)
            {
                Debug.WriteLine(exception);
                loadedMobyChoicePresentationMatches = false;
            }
            if (!loadedMobyChoicePresentationMatches)
            {
                _id65FreshObjectManagerStatusText.Text =
                    $"The loaded schema-{mobySchema} Moby presentation does not match the synchronized canonical document. Object controls are quarantined; reload the Lab before continuing.";
                return;
            }
            if (_id65FreshObjectManagerDocumentButton != null)
                _id65FreshObjectManagerDocumentButton.IsEnabled = canOpenDocument;
            _id65FreshMobyChoiceControlsAvailable = canOpenDocument;
            if (_id65FreshMobyChoiceSelector != null)
                _id65FreshMobyChoiceSelector.IsEnabled = canOpenDocument;
            if (_id65FreshMobyChoicePositionSelector != null)
                _id65FreshMobyChoicePositionSelector.IsEnabled = canOpenDocument;
            RefreshId65FreshMobyChoiceActionAvailability();
            _id65FreshObjectManagerStatusText.Text =
                state.UsesBlankCyclorama &&
                choice.Kind == Id65RemotePadMobyKind.None
                    ? $"Blank Slate schema 6: preset {state.BlankCycloramaPresetId}, exact color {state.BlankCycloramaColorHex}; 0 placed objects. None is explicit and retained position {mobyChoicePosition.Label} ({mobyChoicePosition.EditorX}, {mobyChoicePosition.EditorY}, {mobyChoicePosition.EditorZ}) remains ready for a later choice. The separate guarded offline Blank Slate schema-6 candidate is eligible; normal Create BIN, runtime proof, and release remain locked."
                : state.UsesBlankCyclorama
                    ? $"Blank Slate schema 6: preset {state.BlankCycloramaPresetId}, exact color {state.BlankCycloramaColorHex}; 1 placed object: {choice.Label} at {mobyChoicePosition.Label} " +
                      $"({mobyChoicePosition.EditorX}, {mobyChoicePosition.EditorY}, {mobyChoicePosition.EditorZ}). " +
                      "The mutually exclusive choice, position, and Blank Slate intent are canonical-document-bound. The separate guarded offline Blank Slate schema-6 candidate is eligible; normal Create BIN, runtime proof, and release remain locked."
                : state.UsesRemotePadTerrainSet &&
                choice.Kind == Id65RemotePadMobyKind.None
                    ? $"Blank scene: 0 placed objects. Schema-5 None is explicit and retained position {mobyChoicePosition.Label} ({mobyChoicePosition.EditorX}, {mobyChoicePosition.EditorY}, {mobyChoicePosition.EditorZ}) remains ready for a later choice. The separate guarded offline candidate is eligible; normal Create BIN, runtime proof, and release remain locked."
                : state.UsesRemotePadTerrainSet
                    ? $"1 placed object: schema-5 {choice.Label} at {mobyChoicePosition.Label} " +
                      $"({mobyChoicePosition.EditorX}, {mobyChoicePosition.EditorY}, {mobyChoicePosition.EditorZ}). " +
                      "The mutually exclusive choice and position are canonical-document-bound. The separate guarded offline candidate is eligible; arbitrary Mobys and drag placement remain locked, and normal Create BIN, runtime proof, and release remain locked."
                : choice.Kind == Id65RemotePadMobyKind.None
                    ? $"Blank scene: 0 placed objects. Schema-{mobySchema} None is explicit and retained position {mobyChoicePosition.Label} ({mobyChoicePosition.EditorX}, {mobyChoicePosition.EditorY}, {mobyChoicePosition.EditorZ}) remains ready for a later choice. Physical materialization and all runtime/release paths remain locked."
                    : $"1 placed object: schema-{mobySchema} {choice.Label} at {mobyChoicePosition.Label} " +
                      $"({mobyChoicePosition.EditorX}, {mobyChoicePosition.EditorY}, {mobyChoicePosition.EditorZ}). " +
                      "The mutually exclusive choice and position are canonical-document-bound and presentation-only. Physical materialization, generic dragging, Create BIN, runtime, and release remain locked.";
            return;
        }

        if (!state.UsesRemotePadT107Grid)
        {
            if (_id65FreshObjectManagerDocumentButton != null)
                _id65FreshObjectManagerDocumentButton.IsEnabled = canOpenDocument;
            _id65FreshObjectManagerStatusText.Text =
                "Legacy document detected. Its historical T107 coordinate belongs to the native apron and is never reinterpreted on the blank pad. The fresh scene shows 0 placed objects until you explicitly migrate it to the remote-pad 3×3 grid.";
            return;
        }

        Id65RemotePadGrassT107Position position =
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                state.T107RemotePadPositionId!);
        if (_id65FreshGrassT107PositionSelector != null)
            _id65FreshGrassT107PositionSelector.SelectedItem = position;
        int expectedVisibleCount = state.IncludeGrassAtT107 ? 1 : 0;
        bool loadedPresentationMatches = _currentMobys.Count == expectedVisibleCount;
        if (loadedPresentationMatches && expectedVisibleCount == 1)
        {
            Moby loaded = _currentMobys[0];
            loadedPresentationMatches =
                loaded.TrueIndex == Id65RemotePadGrassT107Catalog.TargetTrueIndex &&
                loaded.Position == new Vector3f(
                    position.EditorX,
                    position.EditorY,
                    position.EditorZ) &&
                loaded.OriginalPosition == loaded.Position &&
                !loaded.HasAnyEdit;
        }
        if (!loadedPresentationMatches)
        {
            _id65FreshObjectManagerStatusText.Text =
                "The loaded Moby presentation does not match the synchronized canonical document. Object controls are quarantined; reload the Lab before continuing.";
            return;
        }
        if (_id65FreshObjectManagerDocumentButton != null)
            _id65FreshObjectManagerDocumentButton.IsEnabled = canOpenDocument;
        _id65FreshGrassT107ControlsAvailable = canOpenDocument;
        if (_id65FreshGrassT107PositionSelector != null)
            _id65FreshGrassT107PositionSelector.IsEnabled = canOpenDocument;
        RefreshId65FreshGrassT107ActionAvailability();
        _id65FreshObjectManagerStatusText.Text = state.IncludeGrassAtT107
            ? $"1 placed object: verified Grass T107 at {position.Label} " +
              $"({position.EditorX}, {position.EditorY}, {position.EditorZ}). " +
              "The selected position is retained by the canonical document. With structural terrain, Proved private T66, and T88 omitted, it is eligible for the verified offline blank-level candidate; generic viewport dragging remains locked."
            : $"Blank scene: 0 placed objects. The dormant Grass T107 position is " +
              $"{position.Label} ({position.EditorX}, {position.EditorY}, {position.EditorZ}); " +
              "it will be retained if Grass is enabled later.";
    }

    private void RefreshId65FreshGrassT107ActionAvailability()
    {
        Id65AuthoringEditorControlState? state = null;
        if (_id65FreshGrassT107ControlsAvailable &&
            _id65FreshAuthoringDocument != null)
        {
            try
            {
                state = Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                    _id65FreshAuthoringDocument);
            }
            catch (Exception exception) when (
                exception is InvalidDataException or InvalidOperationException)
            {
                Debug.WriteLine(exception);
            }
        }

        Id65RemotePadGrassT107Position? selected =
            _id65FreshGrassT107PositionSelector?.SelectedItem as
                Id65RemotePadGrassT107Position;
        bool exactSelection = selected != null &&
            ReferenceEquals(
                selected,
                Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                    selected.PositionId));
        if (_id65FreshAddOrMoveGrassT107Button != null)
        {
            _id65FreshAddOrMoveGrassT107Button.IsEnabled =
                state?.UsesRemotePadT107Grid == true && exactSelection &&
                (!state.IncludeGrassAtT107 ||
                 !string.Equals(
                     state.T107RemotePadPositionId,
                     selected!.PositionId,
                     StringComparison.Ordinal));
        }
        if (_id65FreshRemoveGrassT107Button != null)
        {
            _id65FreshRemoveGrassT107Button.IsEnabled =
                state?.UsesRemotePadT107Grid == true &&
                state.IncludeGrassAtT107;
        }
    }

    private void RefreshId65FreshMobyChoiceActionAvailability()
    {
        Id65AuthoringEditorControlState? state = null;
        if (_id65FreshMobyChoiceControlsAvailable &&
            _id65FreshAuthoringDocument != null)
        {
            try
            {
                state = Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                    _id65FreshAuthoringDocument);
            }
            catch (Exception exception) when (
                exception is InvalidDataException or InvalidOperationException)
            {
                Debug.WriteLine(exception);
            }
        }

        Id65RemotePadMobyChoice? selectedChoice =
            _id65FreshMobyChoiceSelector?.SelectedItem as
                Id65RemotePadMobyChoice;
        Id65RemotePadGrassT107Position? selectedPosition =
            _id65FreshMobyChoicePositionSelector?.SelectedItem as
                Id65RemotePadGrassT107Position;
        bool exactChoice = selectedChoice != null &&
            ReferenceEquals(
                selectedChoice,
                Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(
                    selectedChoice.Kind));
        bool exactPosition = selectedPosition != null &&
            ReferenceEquals(
                selectedPosition,
                Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                    selectedPosition.PositionId));
        if (_id65FreshApplyMobyChoiceButton != null)
        {
            _id65FreshApplyMobyChoiceButton.IsEnabled =
                state?.UsesRemotePadMobyChoice == true && exactChoice &&
                exactPosition &&
                (state.RemotePadMobyKind != selectedChoice!.Kind ||
                 !string.Equals(
                     state.RemotePadMobyPositionId,
                     selectedPosition!.PositionId,
                     StringComparison.Ordinal));
        }
    }

    private void RefreshId65FreshOptionalTerrainUi()
    {
        bool exactFreshRoute =
            IsCurrentId65BlankLab() &&
            Id65FreshLevelSceneProjector.IsProjectionGeometry(_currentGeometry);
        if (_id65FreshOptionalTerrainPanel != null)
            _id65FreshOptionalTerrainPanel.IsVisible = exactFreshRoute;

        Id65AuthoringEditorControlState? state = null;
        if (exactFreshRoute && _id65FreshAuthoringDocument != null)
        {
            try
            {
                state = Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                    _id65FreshAuthoringDocument);
            }
            catch (Exception exception) when (
                exception is InvalidDataException or InvalidOperationException)
            {
                Debug.WriteLine(exception);
            }
        }

        bool busy = _id65BlankLabBusy ||
            _id65BlankLabCatalogValidationBusy ||
            HasConflictingId65BlankLabManualOperationState();
        bool terrainSet = state?.UsesRemotePadTerrainSet == true;
        bool controlsAvailable =
            exactFreshRoute && state != null &&
            _id65FreshDocumentProjectionSynchronized && !busy;
        if (_id65FreshLegacyOptionalTerrainControlsPanel != null)
        {
            _id65FreshLegacyOptionalTerrainControlsPanel.IsVisible =
                !terrainSet;
        }
        if (_id65FreshTerrainSetControlsPanel != null)
        {
            _id65FreshTerrainSetControlsPanel.IsVisible = terrainSet;
        }
        if (_id65FreshCreateOptionalTerrainButton != null)
        {
            _id65FreshCreateOptionalTerrainButton.IsEnabled =
                controlsAvailable && !terrainSet &&
                _id65FreshDocumentProjectionSynchronized &&
                !state!.IncludeStructuralTerrain && !busy;
        }
        if (_id65FreshRemoveOptionalTerrainButton != null)
        {
            _id65FreshRemoveOptionalTerrainButton.IsEnabled =
                controlsAvailable && !terrainSet &&
                state!.IncludeStructuralTerrain &&
                _id65FreshDocumentProjectionSynchronized && !busy;
        }
        if (_id65FreshAddTerrainTile1Button != null)
        {
            _id65FreshAddTerrainTile1Button.IsEnabled =
                controlsAvailable && terrainSet && !state!.IncludeTerrainTile1;
        }
        if (_id65FreshRemoveTerrainTile1Button != null)
        {
            _id65FreshRemoveTerrainTile1Button.IsEnabled =
                controlsAvailable && terrainSet && state!.IncludeTerrainTile1;
        }
        if (_id65FreshAddTerrainTile2Button != null)
        {
            _id65FreshAddTerrainTile2Button.IsEnabled =
                controlsAvailable && terrainSet && !state!.IncludeTerrainTile2;
        }
        if (_id65FreshRemoveTerrainTile2Button != null)
        {
            _id65FreshRemoveTerrainTile2Button.IsEnabled =
                controlsAvailable && terrainSet && state!.IncludeTerrainTile2;
        }

        _id65FreshOptionalTerrainStatusText.Text = !exactFreshRoute
            ? "Load the exact ID65 Blank-Level Lab to use its document-bound terrain controls."
            : !_id65FreshDocumentProjectionSynchronized
                ? "The canonical document and loaded fresh-scene viewport are not synchronized. Optional terrain and exact candidate actions are quarantined. Use Load Lab to re-inspect and fully project the current exact document before continuing."
            : state == null
                ? "Mandatory starter pad only. No Full Authoring Document is loaded, so no default was silently persisted. Open Object Manager and choose Start Blank-Level Editing… to select the mandatory texture and create the blank document first."
                : state.UsesRemotePadTerrainSet
                    ? $"Schema-{(state.UsesBlankCyclorama ? "6 Blank Slate" : "5")} Terrain Tile 1 is {(state.IncludeTerrainTile1 ? "present" : "absent")}; Terrain Tile 2 is {(state.IncludeTerrainTile2 ? "present" : "absent")}. Each exact Add/Remove action changes only its named tile and preserves Moby {Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(state.RemotePadMobyKind!.Value).Label} at retained {Id65RemotePadGrassT107Catalog.GetRequiredPosition(state.RemotePadMobyPositionId!).Label}. " +
                      (state.UsesBlankCyclorama
                          ? $"Preset {state.BlankCycloramaPresetId} and exact color {state.BlankCycloramaColorHex} are preserved. "
                          : "") +
                      "The synchronized state is eligible for the separately guarded exact offline candidate; normal Create BIN, runtime, and release paths remain locked."
                : state.IncludeStructuralTerrain
                    ? $"Optional structural terrain is present. Remove optional terrain changes only IncludeStructuralTerrain; Grass T88 is {(state.IncludeGrassAtT88 ? "included" : "omitted")}, Grass T107 is {(state.IncludeGrassAtT107 ? "included" : "omitted")}, and mandatory base face {Id65MandatoryBaseTextureLabel(state.MandatoryBaseFaceTextureId)} will be preserved."
                    : $"Optional structural terrain is absent. Create optional terrain changes only IncludeStructuralTerrain; Grass T88 is {(state.IncludeGrassAtT88 ? "included" : "omitted")}, Grass T107 is {(state.IncludeGrassAtT107 ? "included" : "omitted")}, and mandatory base face {Id65MandatoryBaseTextureLabel(state.MandatoryBaseFaceTextureId)} will be preserved.";
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
        if (Id65BlankLabHasCanonicalAuthoringDocument())
        {
            _statusText.Text =
                "Legacy ID65 terrain save is disabled because it does not consume the canonical Full Authoring Document. Use Edit Full Authoring Document… for the exact terrain/private-texture/Moby layer instead of creating a conflicting HP-Z file.";
            return false;
        }
        if (Id65FreshLevelSceneProjector.IsProjectionGeometry(_currentGeometry))
        {
            _statusText.Text =
                "Generic ID65 terrain save is unavailable for the fresh-level scene. A standalone HP edit would desynchronize LP terrain, collision, occlusion, and the protected spawn, so no TerrainEditStore file was written. Use Edit Full Authoring Document… for coupled terrain create/remove work.";
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
                Id65FreshLevelSceneProjector.TerrainEditFileName);
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
        Id65FreshLevelT66ViewportTextureProjection t66Projection =
            !overwrite && _id65FreshLevelT66ViewportTextureProjection != null
                ? _id65FreshLevelT66ViewportTextureProjection
                : await Task.Run(
                    () => Id65FreshLevelT66ViewportTextureProjector.ProjectExact(
                        paths.LockedBaseImagePath),
                    cancellationToken);
        RequireCurrentId65BlankLabManualOperation(operation);
        if (t66Projection.TextureId != Id65FreshLevelSceneProjector.StarterTextureId ||
            !t66Projection.IsPresentationOnly ||
            t66Projection.WritesWorkspace ||
            t66Projection.WritesLockedBase ||
            t66Projection.WritesDiscImage ||
            t66Projection.AuthorizesCreateBin ||
            t66Projection.AuthorizesRuntimeCandidate ||
            t66Projection.AuthorizesReleasePromotion)
        {
            throw new InvalidDataException(
                "The exact ID65 T66 viewport projection changed identity or acquired write/runtime/release authority.");
        }
        _id65FreshLevelT66ViewportTextureProjection = t66Projection;
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

    private TerrainGeometryLoadData LoadId65BlankLabGeometryData(
        LevelDefinition level,
        bool includeAuthoredTerrainEdits = true,
        Id65AuthoringEditorDocumentReadModel? authoringDocument = null)
    {
        if (!UnusedLevel65BlankLevelLabProfileRegistry.IsLabLevel(level))
        {
            return new TerrainGeometryLoadData(
                null,
                0,
                "ID65 fresh-scene admission refused a forged or noncanonical level identity.");
        }
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
            using (FileStream overlayStream = File.OpenRead(overlayPath))
            {
                _id65BlankLabTextureOverlaySha256 = Convert.ToHexString(
                    SHA256.HashData(overlayStream)).ToLowerInvariant();
            }
            Id65FreshLevelSceneProjection projection =
                authoringDocument == null
                    ? Id65FreshLevelSceneProjector.ProjectExact(level)
                    : Id65FreshLevelSceneProjector.ProjectExact(
                        level,
                        authoringDocument);
            GeometryCandidate geometry = projection.Geometry;
            _ = includeAuthoredTerrainEdits;
            return new TerrainGeometryLoadData(
                geometry,
                0,
                $"Loaded {projection.ProfileId}: only authored sector {Id65FreshLevelSceneProjector.AuthoredSectorIndex}, its matching collision/occlusion, exact mandatory base face {Id65MandatoryBaseTextureLabel(authoringDocument?.MandatoryBaseFaceTextureId ?? Id65FreshLevelSceneProjector.StarterTextureId)}, and the protected spawn are visible. Generic terrain edit files are intentionally neither loaded nor saved because HP/LP/collision/spawn changes require one coupled transaction in the Full Authoring Document. The validated locked base remains source evidence only; Town Square scaffolding and resident Mobys are not projected.",
                projection);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or JsonException)
        {
            return new TerrainGeometryLoadData(null, 0, $"Could not load the ID65 fresh authoring scene: {ex.Message}");
        }
    }

    private sealed record Id65FreshMobyPresentationNativeShape(
        Id65RemotePadMobyKind Kind,
        string MobyTypeName,
        string PlacementId,
        int TargetTrueIndex,
        ushort ActorId,
        byte YawByte,
        byte? ExactYawByte,
        string SupportHandle,
        int SupportCollisionTrueIndex,
        string RowSha256,
        uint PropertiesPointer,
        int Type,
        int State,
        int SourceByte36,
        int SourceByte37,
        int SourceByte4F,
        int Flag4A,
        int Flag4B,
        string Label,
        string CandidateKind,
        ColorRgba Color);

    private static Id65FreshMobyPresentationNativeShape
        ResolveId65FreshMobyPresentationNativeShape(
            Id65FreshLevelMobyPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        return presentation.MobyKind switch
        {
            Id65RemotePadMobyKind.GrassT107 => new(
                presentation.MobyKind,
                "Grass",
                Id65RemotePadGrassT107Catalog.PlacementId,
                Id65RemotePadGrassT107Catalog.TargetTrueIndex,
                Id65RemotePadGrassT107Catalog.ActorId,
                Id65RemotePadGrassT107Catalog.YawByte,
                Id65RemotePadGrassT107Catalog.YawByte,
                Id65RemotePadGrassT107Catalog.SupportHandle,
                Id65RemotePadGrassT107Catalog.SupportCollisionTrueIndex,
                Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                    presentation.PositionId).RowSha256,
                0x00008138,
                0x20,
                0x00,
                0xF5,
                0x01,
                0x00,
                0x10,
                0xFF,
                $"Grass (remote pad: {presentation.PositionLabel})",
                "Verified remote-pad scenery presentation",
                Moby.ColorForType(0x20)),
            Id65RemotePadMobyKind.RedGemT21 => new(
                presentation.MobyKind,
                Id65RemotePadRedGemT21Catalog.MobyTypeName,
                Id65RemotePadRedGemT21Catalog.PlacementId,
                Id65RemotePadRedGemT21Catalog.TargetTrueIndex,
                Id65RemotePadRedGemT21Catalog.ActorClass,
                0,
                null,
                Id65RemotePadRedGemT21Catalog.SupportHandle,
                Id65RemotePadRedGemT21Catalog.SupportCollisionTrueIndex,
                Id65RemotePadRedGemT21Catalog.GetRequiredPosition(
                    presentation.PositionId).OutputRowSha256,
                0x00005560,
                0x18,
                0x00,
                GemValue.Red.IdByte,
                0x00,
                GemValue.Red.ValueByte,
                0x40,
                0xFF,
                $"Red Gem (remote pad: {presentation.PositionLabel})",
                "Verified remote-pad collectible presentation",
                GemValue.Red.Color),
            _ => throw new InvalidDataException(
                "A visible fresh-level Moby presentation must be Grass T107 or Red Gem T21.")
        };
    }

    private MobyLoadData LoadId65BlankLabMobyData(
        LevelDefinition level,
        Id65FreshLevelSceneProjection? projection = null)
    {
        if (!UnusedLevel65BlankLevelLabProfileRegistry.IsLabLevel(level))
            throw new InvalidDataException("ID65 Moby projection refused a forged or noncanonical level identity.");
        if (!TryGetValidatedId65BlankLabForLoad(out UnusedLevel65BlankLevelLabWorkspacePaths? paths, out _) ||
            paths == null)
        {
            return new MobyLoadData([], 0, default);
        }

        Id65FreshLevelSceneProjection exactProjection =
            projection ?? Id65FreshLevelSceneProjector.ProjectExact(level);
        if (exactProjection.VisibleMobyCount is < 0 or > 1 ||
            exactProjection.VisibleMobyPresentations.Count !=
                exactProjection.VisibleMobyCount ||
            exactProjection.VisibleMobyTrueIndexes.Count !=
                exactProjection.VisibleMobyCount)
        {
            throw new InvalidDataException(
                "The ID65 fresh-level scene projection lost its bounded zero-or-one Moby presentation contract.");
        }

        List<Moby> mobys = [];
        foreach (Id65FreshLevelMobyPresentation presentation in
                 exactProjection.VisibleMobyPresentations)
        {
            Id65FreshMobyPresentationNativeShape nativeShape;
            Id65RemotePadGrassT107Position expectedPosition;
            try
            {
                nativeShape =
                    ResolveId65FreshMobyPresentationNativeShape(presentation);
                expectedPosition = Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                    presentation.PositionId);
            }
            catch (Exception exception) when (
                exception is ArgumentException or KeyNotFoundException)
            {
                throw new InvalidDataException(
                    "The ID65 fresh-level Moby descriptor names an unknown remote-pad position.",
                    exception);
            }
            if (!presentation.IsPresentationOnly || presentation.IsEditable ||
                presentation.NativePersistenceEnabled ||
                presentation.RuntimeObserved ||
                presentation.RuntimeCandidateAuthorized ||
                presentation.AuthorizesCreateBin ||
                presentation.AuthorizesReleasePromotion ||
                presentation.MobyKind != nativeShape.Kind ||
                presentation.MobyTypeName != nativeShape.MobyTypeName ||
                presentation.PlacementId != nativeShape.PlacementId ||
                presentation.TargetTrueIndex != nativeShape.TargetTrueIndex ||
                presentation.ActorId != nativeShape.ActorId ||
                presentation.YawByte != nativeShape.YawByte ||
                presentation.ExactYawByte != nativeShape.ExactYawByte ||
                presentation.SupportHandle != nativeShape.SupportHandle ||
                presentation.SupportCollisionTrueIndex !=
                    nativeShape.SupportCollisionTrueIndex ||
                presentation.PositionLabel != expectedPosition.Label ||
                presentation.EditorX != expectedPosition.EditorX ||
                presentation.EditorY != expectedPosition.EditorY ||
                presentation.EditorZ != expectedPosition.EditorZ ||
                presentation.RawX != expectedPosition.RawX ||
                presentation.RawY != expectedPosition.RawY ||
                presentation.RawZ != expectedPosition.RawZ ||
                presentation.RowSha256 != nativeShape.RowSha256 ||
                presentation.DocumentCanonicalSha256 !=
                    exactProjection.DocumentCanonicalSha256 ||
                presentation.DocumentContentSha256 !=
                    exactProjection.DocumentContentSha256)
            {
                throw new InvalidDataException(
                    "The ID65 fresh-level Moby descriptor crossed its presentation-only identity boundary.");
            }

            Vector3f position = new(
                presentation.EditorX,
                presentation.EditorY,
                presentation.EditorZ);
            string evidence =
                $"Moby {Id65RemotePadMobyChoiceCatalog.GetKindId(presentation.MobyKind)}; position {presentation.PositionId}; editor {presentation.EditorX},{presentation.EditorY},{presentation.EditorZ}; " +
                $"raw {presentation.RawX},{presentation.RawY},{presentation.RawZ}; support {presentation.SupportHandle}; " +
                $"row {presentation.RowSha256}; document {presentation.DocumentCanonicalSha256}/{presentation.DocumentContentSha256}; " +
                $"presentation {presentation.CanonicalSha256}.";
            mobys.Add(new Moby
            {
                Index = 0,
                TrueIndex = presentation.TargetTrueIndex,
                LegacyIndex = MobyLoader.GetLegacyAliasIndex(
                    presentation.TargetTrueIndex),
                Position = position,
                OriginalPosition = position,
                Type = nativeShape.Type,
                OriginalType = nativeShape.Type,
                State = nativeShape.State,
                OriginalState = nativeShape.State,
                YawByte = nativeShape.YawByte,
                OriginalYawByte = nativeShape.YawByte,
                RuntimeAddress = 0,
                PropertiesPointer = nativeShape.PropertiesPointer,
                SpecialDataPointer = 0,
                SourceByte36 = nativeShape.SourceByte36,
                OriginalSourceByte36 = nativeShape.SourceByte36,
                SourceByte37 = nativeShape.SourceByte37,
                OriginalSourceByte37 = nativeShape.SourceByte37,
                SourceByte4F = nativeShape.SourceByte4F,
                OriginalSourceByte4F = nativeShape.SourceByte4F,
                Flag4A = nativeShape.Flag4A,
                OriginalFlag4A = nativeShape.Flag4A,
                Flag4B = nativeShape.Flag4B,
                OriginalFlag4B = nativeShape.Flag4B,
                Color = nativeShape.Color,
                OriginalColor = nativeShape.Color,
                Label = nativeShape.Label,
                OriginalLabel = nativeShape.Label,
                CandidateKind = nativeShape.CandidateKind,
                OriginalCandidateKind = nativeShape.CandidateKind,
                Confidence = "static-document-bound",
                OriginalConfidence = "static-document-bound",
                Evidence = evidence,
                OriginalEvidence = evidence,
                BehaviorNote =
                    "Read-only fresh-scene presentation; native persistence and runtime acceptance are not implemented.",
                OriginalBehaviorNote =
                    "Read-only fresh-scene presentation; native persistence and runtime acceptance are not implemented.",
                ZoneLabel = presentation.PositionLabel,
                OriginalZoneLabel = presentation.PositionLabel,
                PatchStatus = "presentation-only",
                OriginalPatchStatus = "presentation-only",
                PatchLead =
                    "Edit placement through the canonical Full Authoring Document. Generic object mutation and all native writers are blocked.",
                OriginalPatchLead =
                    "Edit placement through the canonical Full Authoring Document. Generic object mutation and all native writers are blocked.",
                IsAdded = false,
                IsRemoved = false
            });
        }

        return new MobyLoadData(mobys, 0, default);
    }

    private static bool Id65PreparedMobysMatchProjection(
        IReadOnlyList<Moby> mobys,
        Id65FreshLevelSceneProjection projection)
    {
        ArgumentNullException.ThrowIfNull(mobys);
        ArgumentNullException.ThrowIfNull(projection);
        if (mobys.Count != projection.VisibleMobyCount ||
            projection.VisibleMobyPresentations.Count != mobys.Count)
        {
            return false;
        }
        if (mobys.Count == 0)
            return true;

        Id65FreshLevelMobyPresentation presentation =
            projection.VisibleMobyPresentations.Single();
        Id65FreshMobyPresentationNativeShape nativeShape;
        Id65RemotePadGrassT107Position expectedCatalogPosition;
        try
        {
            nativeShape = ResolveId65FreshMobyPresentationNativeShape(
                presentation);
            expectedCatalogPosition =
                Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                    presentation.PositionId);
        }
        catch (Exception exception) when (
            exception is ArgumentException or KeyNotFoundException)
        {
            return false;
        }
        Moby moby = mobys.Single();
        Vector3f expectedPosition = new(
            presentation.EditorX,
            presentation.EditorY,
            presentation.EditorZ);
        string expectedEvidence =
            $"Moby {Id65RemotePadMobyChoiceCatalog.GetKindId(presentation.MobyKind)}; position {presentation.PositionId}; editor {presentation.EditorX},{presentation.EditorY},{presentation.EditorZ}; " +
            $"raw {presentation.RawX},{presentation.RawY},{presentation.RawZ}; support {presentation.SupportHandle}; " +
            $"row {presentation.RowSha256}; document {presentation.DocumentCanonicalSha256}/{presentation.DocumentContentSha256}; " +
            $"presentation {presentation.CanonicalSha256}.";
        return presentation.PositionLabel == expectedCatalogPosition.Label &&
            presentation.EditorX == expectedCatalogPosition.EditorX &&
            presentation.EditorY == expectedCatalogPosition.EditorY &&
            presentation.EditorZ == expectedCatalogPosition.EditorZ &&
            presentation.RawX == expectedCatalogPosition.RawX &&
            presentation.RawY == expectedCatalogPosition.RawY &&
            presentation.RawZ == expectedCatalogPosition.RawZ &&
            presentation.MobyKind == nativeShape.Kind &&
            presentation.MobyTypeName == nativeShape.MobyTypeName &&
            presentation.PlacementId == nativeShape.PlacementId &&
            presentation.TargetTrueIndex == nativeShape.TargetTrueIndex &&
            presentation.ActorId == nativeShape.ActorId &&
            presentation.YawByte == nativeShape.YawByte &&
            presentation.ExactYawByte == nativeShape.ExactYawByte &&
            presentation.SupportHandle == nativeShape.SupportHandle &&
            presentation.SupportCollisionTrueIndex ==
                nativeShape.SupportCollisionTrueIndex &&
            presentation.RowSha256 == nativeShape.RowSha256 &&
            presentation.DocumentCanonicalSha256 ==
                projection.DocumentCanonicalSha256 &&
            presentation.DocumentContentSha256 ==
                projection.DocumentContentSha256 &&
            moby.Index == 0 &&
            moby.TrueIndex == presentation.TargetTrueIndex &&
            moby.LegacyIndex == MobyLoader.GetLegacyAliasIndex(
                presentation.TargetTrueIndex) &&
            moby.Position == expectedPosition &&
            moby.OriginalPosition == expectedPosition &&
            moby.Type == nativeShape.Type &&
            moby.OriginalType == nativeShape.Type &&
            moby.State == nativeShape.State &&
            moby.OriginalState == nativeShape.State &&
            moby.YawByte == nativeShape.YawByte &&
            moby.OriginalYawByte == nativeShape.YawByte &&
            moby.RuntimeAddress == 0 &&
            moby.PropertiesPointer == nativeShape.PropertiesPointer &&
            moby.SpecialDataPointer == 0 &&
            moby.SourceByte36 == nativeShape.SourceByte36 &&
            moby.OriginalSourceByte36 == nativeShape.SourceByte36 &&
            moby.SourceByte37 == nativeShape.SourceByte37 &&
            moby.OriginalSourceByte37 == nativeShape.SourceByte37 &&
            moby.SourceByte4F == nativeShape.SourceByte4F &&
            moby.OriginalSourceByte4F == nativeShape.SourceByte4F &&
            moby.Flag4A == nativeShape.Flag4A &&
            moby.OriginalFlag4A == nativeShape.Flag4A &&
            moby.Flag4B == nativeShape.Flag4B &&
            moby.OriginalFlag4B == nativeShape.Flag4B &&
            moby.Label == nativeShape.Label &&
            moby.OriginalLabel == nativeShape.Label &&
            moby.CandidateKind == nativeShape.CandidateKind &&
            moby.OriginalCandidateKind == nativeShape.CandidateKind &&
            moby.Evidence == expectedEvidence &&
            moby.OriginalEvidence == expectedEvidence &&
            !moby.IsAdded && !moby.IsRemoved && !moby.HasAnyEdit;
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
        _freshNativeResearchBusy || _buildingPortableCache ||
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
        if (TryBlockFreshDocumentResearchMutation(action)) return true;
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
        ResetId65AuthoringCandidateButtonLabel();
        RefreshId65BlankLabUi();
        RefreshLevelSelectionAvailability();
        RefreshActionAvailability();
    }

    private void ResetId65AuthoringCandidateButtonLabel()
    {
        if (_id65BlankLabAuthoringCandidateButton != null)
        {
            _id65BlankLabAuthoringCandidateButton.Content =
                "Create Exact Full Candidate";
        }
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
            Id65FreshLevelSceneProjector.TerrainEditFileName);

    private string Id65BlankLabAuthoringDocumentPath()
    {
        UnusedLevel65BlankLevelLabWorkspacePaths paths =
            _id65BlankLabPaths ??
            UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
                _workspace.RootPath);
        return Path.Combine(
            paths.AuthoredEditsDirectoryPath,
            Id65AuthoringDocumentFileName);
    }

    private bool Id65BlankLabHasCanonicalAuthoringDocument() =>
        Id65BlankLabExactPathEntryExistsOrIsUninspectable(
            Id65BlankLabAuthoringDocumentPath());

    private static bool Id65BlankLabExactPathEntryExistsOrIsUninspectable(
        string path)
    {
        try
        {
            DirectoryInfo directory = new(path);
            if (directory.Exists || !string.IsNullOrWhiteSpace(directory.LinkTarget))
                return true;
            FileInfo file = new(path);
            return file.Exists || !string.IsNullOrWhiteSpace(file.LinkTarget);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // An unreadable reserved destination must fail closed. Treating it
            // as absent could let a legacy writer omit or collide with the
            // canonical authoring layer.
            return true;
        }
    }

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
        if (Id65BlankLabHasCanonicalAuthoringDocument())
        {
            reason =
                "The canonical Full Authoring Document filename was detected. The legacy terrain-only disposable writer does not consume that document and is disabled so it cannot silently omit structural terrain, private texture, or Moby intent; use Edit Full Authoring Document… to inspect or update it.";
            return true;
        }

        return TryGetId65BlankLabNonCanonicalUnsupportedArtifact(out reason);
    }

    private bool TryGetId65BlankLabNonCanonicalUnsupportedArtifact(
        out string reason)
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
        if (Id65BlankLabExactPathEntryExistsOrIsUninspectable(terrainEditsPath))
        {
            reason =
                "A legacy generic ID65 terrain edit file is present, but the fresh scene projection neither loads nor saves TerrainEditStore rows. Preserve it for inspection or move it out of the authored layer; coupled terrain changes belong in the Full Authoring Document.";
            return true;
        }
        if (Id65BlankLabExactPathEntryExistsOrIsUninspectable(objectEditsPath) ||
            Id65BlankLabExactPathEntryExistsOrIsUninspectable(movementEditsPath))
        {
            reason = "Resident Moby/path mutation is present but this profile permits inspection only.";
            return true;
        }
        if (Id65BlankLabExactPathEntryExistsOrIsUninspectable(customTexturesPath))
        {
            reason = "Custom/imported terrain art needs a proved ID65 destination profile.";
            return true;
        }
        if (Id65BlankLabExactPathEntryExistsOrIsUninspectable(relocationPath))
        {
            reason = "Portable cross-level terrain textures need a proved ID65 destination profile.";
            return true;
        }
        if (Id65BlankLabExactPathEntryExistsOrIsUninspectable(materialOverridesPath))
        {
            reason = "Retail terrain material overrides cannot be applied to the locked ID65 presentation; remove the unsupported override file.";
            return true;
        }
        if (Id65BlankLabExactPathEntryExistsOrIsUninspectable(
                NativeSkyEditStore.PlanPath(_workspace.RootPath, key)))
        {
            reason = "Skybox editing is unavailable for this profile; remove the unsupported ID65 skybox plan.";
            return true;
        }
        if (Id65BlankLabExactPathEntryExistsOrIsUninspectable(musicPlanPath))
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
        if (Id65BlankLabHasCanonicalAuthoringDocument())
        {
            if (_id65FreshAuthoringDocument != null)
            {
                try
                {
                    Id65AuthoringEditorControlState loadedState =
                        Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                            _id65FreshAuthoringDocument);
                    if (loadedState.MandatoryBaseFaceTextureId ==
                        Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId)
                    {
                        reason =
                            "Normal Create BIN is refused for the synchronized Resident T25 Full Authoring Document. T25 is offline-authoring/viewport-only and has no exact candidate, Create BIN, runtime, release, promotion, or publication authority. Apply and save Proved private T66 to use its separately guarded exact offline candidate path; normal retail composition still remains unavailable.";
                        return true;
                    }
                }
                catch (Exception exception) when (
                    exception is InvalidDataException or InvalidOperationException)
                {
                    Debug.WriteLine(exception);
                }
            }
            reason =
                "Normal Create BIN is unavailable because the exact ID65 aggregate materializer is intentionally a separate offline candidate action and is not authorized for normal retail composition yet. Focused runtime proof and an atomic cross-process composition coordinator are still required. The legacy terrain-only test writer also remains disabled because it does not consume the canonical structural terrain, private-texture, and Moby intent.";
            return true;
        }
        if (IsCurrentId65BlankLab())
        {
            reason =
                "Normal Create BIN is unavailable while ID65 Blank-Level Lab is loaded. Use Edit Full Authoring Document… and Create Exact Full Candidate for a separately guarded supported offline artifact. Runtime use and release promotion remain unauthorized.";
            return true;
        }
        if (TryGetInvalidRetailTerrainTextureRelocationDonorBlockReason(out reason))
            return true;
        if (Id65BlankLabHasAnyAuthoredArtifacts())
        {
            reason =
                "Normal Create BIN is blocked because the isolated ID65 lab has authored research artifacts that the retail all-saved-edits writer must neither omit nor include. Supported schema-5 terrain-set/Moby, schema-3 remote-pad, and frozen legacy T66 documents have separate exact offline candidate paths; none is authorized for normal retail composition, runtime use, or release promotion. Schema 4 remains static-presentation only.";
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
        out string reason)
    {
        if (Id65FreshLevelSceneProjector.IsProjectionGeometry(geometry))
        {
            reason =
                "The fresh ID65 starter scene is an authoring presentation, not the locked-base disposable writer substrate. Use the canonical Full Authoring Document and exact candidate path; no runtime, Create BIN, or release authority is implied.";
            return false;
        }

        return TryValidateId65BlankLabTerrainEdits(
            geometry,
            requireAtLeastOneEdit: true,
            allowResidentTextureEdits: false,
            out reason);
    }

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
        if (TryBlockFreshDocumentResearchMutation(action)) return true;
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
        WorkspaceTransitionGeneration: _workspaceTransitionGeneration,
        FreshResearchGeneration: _freshNativeResearchGeneration);

    private bool IsCurrentEditorMutationSceneIdentity(EditorMutationSceneIdentity captured) =>
        !_freshNativeResearchBusy &&
        captured.FreshResearchGeneration == _freshNativeResearchGeneration &&
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
        WorkspaceTransitionGeneration: _workspaceTransitionGeneration,
        FreshResearchGeneration: _freshNativeResearchGeneration);

    private bool IsCurrentSourceDiscOperationIdentity(
        SourceDiscOperationIdentity captured,
        bool requireSceneIdentity = true,
        bool allowPortableCacheBuild = false,
        bool allowProjectDataImport = false) =>
        !_freshNativeResearchBusy &&
        captured.FreshResearchGeneration == _freshNativeResearchGeneration &&
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
                    $"The editor blocked {action}: release terrain editing uses the visible HP Raise/Lower and guarded texture-paint controls. " +
                    "XY movement, drag sculpting, full edit dialogs, add/remove, and hidden viewport shortcuts remain research-only.";
                return true;
            }

            if (terrain == null ||
                !string.Equals(terrain.Detail, "hp", StringComparison.OrdinalIgnoreCase) ||
                terrain.IsTerrainRemoved ||
                terrain.IsTerrainAddClone)
            {
                _statusText.Text =
                    $"The editor blocked {action}: only existing high-detail (HP) faces may receive Z-height edits.";
                return true;
            }

            return false;
        }

        if (Id65FreshLevelSceneProjector.IsProjectionGeometry(_currentGeometry))
        {
            _statusText.Text =
                $"ID65 fresh-level scene blocked {action}: generic terrain mutation is intentionally unavailable because changing only the HP face would desynchronize its LP companion, collision, occlusion, and protected spawn. Use Edit Full Authoring Document… for coupled terrain create/remove work; no generic terrain edit file was loaded or saved.";
            return true;
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

    private static bool Id65BlankLabLayerSnapshotsEqual(
        Id65BlankLabAuthoredLayerSnapshot left,
        Id65BlankLabAuthoredLayerSnapshot right) =>
        left.DirectoryExisted == right.DirectoryExisted &&
        left.Files.SequenceEqual(right.Files, StringComparer.Ordinal);

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

    private int CaptureId65MandatoryBaseFaceTextureIdForTesting()
    {
        if (_id65FreshAuthoringDocument == null)
            return -1;
        try
        {
            return Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                _id65FreshAuthoringDocument).MandatoryBaseFaceTextureId;
        }
        catch (Exception exception) when (
            exception is InvalidDataException or InvalidOperationException)
        {
            Debug.WriteLine(exception);
            return -1;
        }
    }

    private string AssertId65MandatoryBaseTextureViewportRouteForTesting(
        int expectedTextureId)
    {
        if (!IsCurrentId65BlankLab() || _currentLevel == null ||
            _id65FreshAuthoringDocument == null ||
            !_id65FreshDocumentProjectionSynchronized)
        {
            throw new InvalidOperationException(
                "The viewport-route probe requires one synchronized canonical ID65 document.");
        }
        Id65AuthoringEditorControlState state =
            Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                _id65FreshAuthoringDocument);
        if (state.MandatoryBaseFaceTextureId != expectedTextureId)
        {
            throw new InvalidOperationException(
                $"The viewport-route probe expected T{expectedTextureId} but loaded T{state.MandatoryBaseFaceTextureId}.");
        }

        NativeTerrainLqFrameSnapshot close =
            _viewport.CaptureNativeTerrainLqFrameForTesting(expectedTextureId, 79.999);
        NativeTerrainLqFrameSnapshot normal =
            _viewport.CaptureNativeTerrainLqFrameForTesting(expectedTextureId, 511);
        NativeTerrainLqFrameSnapshot far =
            _viewport.CaptureNativeTerrainLqFrameForTesting(expectedTextureId, 1088);
        if (!close.HasIndexedPayload || !close.BitmapResolved ||
            close.HqTier != NativeTerrainTexturePreviewTier.Close ||
            close.BitmapWidth != 64 || close.BitmapHeight != 64 ||
            !normal.HasIndexedPayload || !normal.BitmapResolved ||
            normal.HqTier != NativeTerrainTexturePreviewTier.Normal ||
            normal.BitmapWidth != 64 || normal.BitmapHeight != 64 ||
            !far.HasIndexedPayload || !far.BitmapResolved ||
            far.HqTier != null || far.BitmapWidth != 32 || far.BitmapHeight != 32)
        {
            throw new InvalidOperationException(
                $"The T{expectedTextureId} viewport did not resolve exact close/normal/far tiers: close={close}; normal={normal}; far={far}.");
        }

        NativeTerrainLqTextureSet expectedLowDetail;
        NativeTerrainHqMaterialSet expectedHighDetail;
        if (expectedTextureId ==
            Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId)
        {
            string levelKey = LevelCatalog.NormalizeKey(_currentLevel.Key);
            if (!TryGetTerrainTexturePreviewBundle(
                    levelKey,
                    out TerrainTexturePreviewBundle? residentBundle) ||
                residentBundle == null ||
                !residentBundle.NormalFiles.TryGetValue(
                    expectedTextureId,
                    out string? expectedNormalPath) ||
                !residentBundle.CloseFiles.TryGetValue(
                    expectedTextureId,
                    out string? expectedClosePath) ||
                !string.Equals(
                    Path.GetFullPath(expectedNormalPath),
                    Path.GetFullPath(_viewport.ResolveTerrainTextureImagePathForTesting(
                        expectedTextureId,
                        511) ?? ""),
                    StringComparison.Ordinal) ||
                !string.Equals(
                    Path.GetFullPath(expectedClosePath),
                    Path.GetFullPath(_viewport.ResolveTerrainTextureImagePathForTesting(
                        expectedTextureId,
                        79.999) ?? ""),
                    StringComparison.Ordinal) ||
                _viewport.IsId65FreshLevelT66ViewportTextureProjectionBoundForTesting)
            {
                throw new InvalidOperationException(
                    "Resident T25 did not retain its exact ordinary resident normal/close files or incorrectly bound the private T66 projection.");
            }
            expectedLowDetail = residentBundle.LowDetailTextures;
            expectedHighDetail = residentBundle.HighDetailMaterials;
        }
        else if (expectedTextureId ==
            Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId)
        {
            Id65FreshLevelT66ViewportTextureProjection privateProjection =
                _id65FreshLevelT66ViewportTextureProjection ??
                throw new InvalidOperationException(
                    "The Proved private T66 test projection is unavailable.");
            if (!_viewport.IsId65FreshLevelT66ViewportTextureProjectionBoundForTesting ||
                !string.Equals(
                    close.BitmapSha256,
                    privateProjection.Close.RgbaSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    normal.BitmapSha256,
                    privateProjection.Normal.RgbaSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Proved private T66 did not bind its exact private close/normal frame identities.");
            }
            expectedLowDetail = privateProjection.LowDetailTextures;
            expectedHighDetail = privateProjection.HighDetailMaterials;
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported viewport-route probe texture T{expectedTextureId}.");
        }

        if (!expectedLowDetail.TryGetTexture(
                expectedTextureId,
                out NativeTerrainLqTextureRecordPayload expectedTexture))
        {
            throw new InvalidOperationException(
                $"The expected T{expectedTextureId} LQ payload is missing.");
        }
        foreach (NativeTerrainLqFrameSnapshot snapshot in new[] { close, normal, far })
        {
            NativeTerrainLqTextureDescriptorPayload descriptor =
                expectedTexture.GetDescriptor(snapshot.DescriptorIndex);
            if (snapshot.RawDescriptorSha256 != descriptor.RawDescriptorSha256 ||
                snapshot.PackedIndicesSha256 != descriptor.PackedIndicesSha256 ||
                snapshot.PaletteWordsSha256 != descriptor.PaletteWordsSha256)
            {
                throw new InvalidOperationException(
                    $"The T{expectedTextureId} {snapshot.HqTier?.ToString() ?? "far"} frame lost its exact resident/private LQ descriptor identity.");
            }
        }
        if (_viewport.CaptureNativeTerrainHqMaterialFingerprintForTesting() !=
            expectedHighDetail.ContentSha256)
        {
            throw new InvalidOperationException(
                $"The T{expectedTextureId} viewport lost its exact resident/private HQ material fingerprint.");
        }

        return expectedTextureId ==
            Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId
            ? "Resident T25 ordinary close/normal/LQ/HQ route exact; private projection unbound"
            : "Proved private T66 close/normal/LQ/HQ route exact; private projection bound";
    }

    internal Id65BlankLabUiSnapshot CaptureId65BlankLabUiSnapshotForTesting()
    {
        Id65AuthoringEditorControlState? loadedState = null;
        if (_id65FreshAuthoringDocument != null)
        {
            try
            {
                loadedState =
                    Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                        _id65FreshAuthoringDocument);
            }
            catch (Exception exception) when (
                exception is InvalidDataException or InvalidOperationException)
            {
                Debug.WriteLine(exception);
            }
        }

        return new Id65BlankLabUiSnapshot(
        DisclosureTitle: Id65BlankLabDisclosureTitle,
        StatusText: _id65BlankLabStatusText.Text ?? "",
        LockedBaseText: _id65BlankLabLockedBaseText.Text ?? "",
        AuthoredLayerText: _id65BlankLabAuthoredLayerText.Text ?? "",
        CapabilityText: $"Terrain: {Id65BlankLabTerrainCapability}\nTextures: {Id65BlankLabTextureCapability}\nMobys: {Id65BlankLabMobyCapability}",
        RetailCatalogCount: _catalog.Levels.Count(level => level.LevelId != 65),
        HasId65CatalogLevel: _catalog.Levels.Any(level => level.LevelId == 65),
        BuildEnabled: _id65BlankLabBuildButton?.IsEnabled == true,
        LoadEnabled: _id65BlankLabLoadButton?.IsEnabled == true,
        AuthoringDocumentEnabled:
            _id65BlankLabAuthoringDocumentButton?.IsEnabled == true,
        AuthoringDocumentButtonText:
            _id65BlankLabAuthoringDocumentButton?.Content?.ToString() ?? "",
        AuthoringCandidateEnabled:
            _id65BlankLabAuthoringCandidateButton?.IsEnabled == true,
        AuthoringCandidateButtonText:
            _id65BlankLabAuthoringCandidateButton?.Content?.ToString() ?? "",
        ObjectManagerDocumentEnabled:
            _id65FreshObjectManagerDocumentButton?.IsEnabled == true,
        ObjectManagerStatusText:
            _id65FreshObjectManagerStatusText.Text ?? "",
        ObjectManagerGrassPositionSelectorEnabled:
            _id65FreshGrassT107PositionSelector?.IsEnabled == true,
        ObjectManagerGrassSelectedPositionId:
            (_id65FreshGrassT107PositionSelector?.SelectedItem as
                Id65RemotePadGrassT107Position)?.PositionId ?? "",
        ObjectManagerAddOrMoveGrassEnabled:
            _id65FreshAddOrMoveGrassT107Button?.IsEnabled == true,
        ObjectManagerRemoveGrassEnabled:
            _id65FreshRemoveGrassT107Button?.IsEnabled == true,
        ObjectManagerMobyChoicePanelVisible:
            _id65FreshMobyChoiceControlsPanel?.IsVisible == true,
        ObjectManagerMobyChoiceSelectorEnabled:
            _id65FreshMobyChoiceSelector?.IsEnabled == true,
        ObjectManagerSelectedMobyKind:
            (_id65FreshMobyChoiceSelector?.SelectedItem as
                Id65RemotePadMobyChoice)?.Kind,
        ObjectManagerMobyPositionSelectorEnabled:
            _id65FreshMobyChoicePositionSelector?.IsEnabled == true,
        ObjectManagerSelectedMobyPositionId:
            (_id65FreshMobyChoicePositionSelector?.SelectedItem as
                Id65RemotePadGrassT107Position)?.PositionId ?? "",
        ObjectManagerApplyMobyChoiceEnabled:
            _id65FreshApplyMobyChoiceButton?.IsEnabled == true,
        SaveEnabled: _id65BlankLabSaveButton?.IsEnabled == true,
        CreateCueEnabled: _id65BlankLabCreateCueButton?.IsEnabled == true,
        RevealEnabled: _id65BlankLabRevealButton?.IsEnabled == true,
        RevealButtonText: _id65BlankLabRevealButton?.Content?.ToString() ?? "",
        OptionalTerrainVisible:
            _id65FreshOptionalTerrainPanel?.IsVisible == true,
        CreateOptionalTerrainEnabled:
            _id65FreshCreateOptionalTerrainButton?.IsEnabled == true,
        RemoveOptionalTerrainEnabled:
            _id65FreshRemoveOptionalTerrainButton?.IsEnabled == true,
        LegacyOptionalTerrainControlsVisible:
            _id65FreshLegacyOptionalTerrainControlsPanel?.IsVisible == true,
        TerrainSetControlsVisible:
            _id65FreshTerrainSetControlsPanel?.IsVisible == true,
        AddTerrainTile1Enabled:
            _id65FreshAddTerrainTile1Button?.IsEnabled == true,
        RemoveTerrainTile1Enabled:
            _id65FreshRemoveTerrainTile1Button?.IsEnabled == true,
        AddTerrainTile2Enabled:
            _id65FreshAddTerrainTile2Button?.IsEnabled == true,
        RemoveTerrainTile2Enabled:
            _id65FreshRemoveTerrainTile2Button?.IsEnabled == true,
        OptionalTerrainStatusText:
            _id65FreshOptionalTerrainStatusText.Text ?? "",
        DocumentProjectionSynchronized:
            _id65FreshDocumentProjectionSynchronized,
        LoadedMandatoryBaseFaceTextureId:
            CaptureId65MandatoryBaseFaceTextureIdForTesting(),
        LoadedUsesRemotePadT107Grid:
            _id65FreshAuthoringDocument?.UsesRemotePadT107Grid == true,
        LoadedT107RemotePadPositionId:
            _id65FreshAuthoringDocument?.T107RemotePadPositionId ?? "",
        LoadedUsesRemotePadMobyChoice:
            _id65FreshAuthoringDocument?.UsesRemotePadMobyChoice == true,
        LoadedUsesRemotePadTerrainSet:
            loadedState?.UsesRemotePadTerrainSet == true,
        LoadedTerrainTile1:
            loadedState?.IncludeTerrainTile1 == true,
        LoadedTerrainTile2:
            loadedState?.IncludeTerrainTile2 == true,
        LoadedRemotePadMobyKind:
            _id65FreshAuthoringDocument?.RemotePadMobyKind,
        LoadedRemotePadMobyPositionId:
            _id65FreshAuthoringDocument?.RemotePadMobyPositionId ?? "",
        LoadedUsesBlankCyclorama:
            loadedState?.UsesBlankCyclorama == true,
        LoadedBlankCycloramaPresetId:
            loadedState?.BlankCycloramaPresetId ?? "",
        LoadedBlankCycloramaColorHex:
            loadedState?.BlankCycloramaColorHex ?? "",
        VisibleMobyCount: _currentMobys.Count,
        PrivateT66ViewportProjectionBound:
            _viewport.IsId65FreshLevelT66ViewportTextureProjectionBoundForTesting,
        RepairsInvalidWorkspace: true,
        DisposableExporterRoute: nameof(UnusedLevel65BlankLevelLabTerrainTestExporter));
    }

    internal static string CreateId65BlankLabDisposableTestKeyForTesting() =>
        CreateId65BlankLabDisposableTestKey();

    internal async Task<string> AssertId65CanonicalAuthoringDocumentLegacyWriterGuardForTestingAsync()
    {
        if (!IsCurrentId65BlankLab() || _currentLevel == null || _currentGeometry == null ||
            _id65BlankLabManifest == null || _id65BlankLabPaths == null)
        {
            throw new InvalidOperationException(
                "The canonical-document writer guard probe requires the validated, loaded ID65 Lab.");
        }
        if (_id65BlankLabBusy || _id65BlankLabManualOperation != null ||
            _createBinBusy || _loadingLevel)
        {
            throw new InvalidOperationException(
                "The canonical-document writer guard probe requires an idle ID65 Lab.");
        }
        if (!string.Equals(
                Id65AuthoringDocumentFileName,
                "unusedlevel65blank-authoring-document-v1.json",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The App guard is not bound to the canonical Core inspector filename.");
        }

        UnusedLevel65BlankLevelLabWorkspacePaths paths = _id65BlankLabPaths;
        string documentPath = Id65BlankLabAuthoringDocumentPath();
        string nearMissPath = documentPath + ".bak";
        if (Id65BlankLabHasCanonicalAuthoringDocument() ||
            Id65BlankLabExactPathEntryExistsOrIsUninspectable(nearMissPath))
        {
            throw new InvalidOperationException(
                "The canonical-document writer guard probe refuses to replace a pre-existing exact or near-miss fixture.");
        }

        RefreshId65BlankLabUi();
        RefreshActionAvailability();
        Id65BlankLabUiSnapshot uiBefore = CaptureId65BlankLabUiSnapshotForTesting();
        if (uiBefore.SaveEnabled ||
            uiBefore.CreateCueEnabled ||
            !uiBefore.AuthoredLayerText.Contains(
                "presentation-only",
                StringComparison.OrdinalIgnoreCase) ||
            !uiBefore.AuthoredLayerText.Contains(
                "Full Authoring Document",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The fresh projection did not start with generic Save/disposable CUE disabled and Full Authoring Document guidance.");
        }
        string mainStatusBefore = _statusText.Text ?? "";
        Id65BlankLabAuthoredLayerSnapshot authoredBefore =
            await CaptureId65BlankLabAuthoredLayerAsync(paths.AuthoredEditsDirectoryPath);
        string[] protectedOutputDirectories =
        [
            paths.OperationsDirectoryPath,
            Path.Combine(paths.RootPath, ".terrain-test-operations"),
            Path.Combine(paths.RootPath, "runtime-tests"),
            Path.Combine(paths.RootPath, "materialized-candidates"),
            Path.Combine(_workspace.RootPath, "output")
        ];
        Dictionary<string, Id65BlankLabAuthoredLayerSnapshot> protectedBefore =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (string directory in protectedOutputDirectories)
        {
            protectedBefore[directory] =
                await CaptureId65BlankLabAuthoredLayerAsync(directory);
        }

        int operationGenerationBefore = _id65BlankLabManualOperationGeneration;
        CancellationTokenSource? operationCancellationBefore =
            _id65BlankLabManualOperationCancellation;
        string revealPathBefore = _id65BlankLabRevealPath;
        LevelDefinition levelBefore = _currentLevel;
        GeometryCandidate geometryBefore = _currentGeometry;
        List<Moby> mobysBefore = _currentMobys;
        string terrainSignatureBefore = BuildTerrainEditSignature(_currentGeometry);
        string mobySignatureBefore = BuildMobyEditSignature(_currentMobys);
        string savedTerrainSignatureBefore = _savedTerrainEditSignature;
        string savedMobySignatureBefore = _savedMobyEditSignature;
        object? toolbarCreateBinContentBefore = _toolbarCreateBinButton?.Content;

        static bool SnapshotIsExact(
            Id65BlankLabAuthoredLayerSnapshot expected,
            Id65BlankLabAuthoredLayerSnapshot actual) =>
            expected.DirectoryExisted == actual.DirectoryExisted &&
            expected.Files.SequenceEqual(actual.Files, StringComparer.Ordinal);

        async Task AssertWriterBoundariesUnchangedAsync(
            string label,
            Id65BlankLabAuthoredLayerSnapshot expectedAuthored)
        {
            Id65BlankLabAuthoredLayerSnapshot authoredNow =
                await CaptureId65BlankLabAuthoredLayerAsync(
                    paths.AuthoredEditsDirectoryPath);
            bool protectedOutputsExact = true;
            foreach ((string directory, Id65BlankLabAuthoredLayerSnapshot expected) in
                     protectedBefore)
            {
                Id65BlankLabAuthoredLayerSnapshot actual =
                    await CaptureId65BlankLabAuthoredLayerAsync(directory);
                protectedOutputsExact &= SnapshotIsExact(expected, actual);
            }

            if (!SnapshotIsExact(expectedAuthored, authoredNow) ||
                !protectedOutputsExact ||
                _id65BlankLabBusy || _id65BlankLabManualOperation != null ||
                _createBinBusy || _loadingLevel ||
                _id65BlankLabManualOperationGeneration != operationGenerationBefore ||
                !ReferenceEquals(
                    _id65BlankLabManualOperationCancellation,
                    operationCancellationBefore) ||
                !string.Equals(_id65BlankLabRevealPath, revealPathBefore, StringComparison.Ordinal) ||
                !ReferenceEquals(_currentLevel, levelBefore) ||
                !ReferenceEquals(_currentGeometry, geometryBefore) ||
                !ReferenceEquals(_currentMobys, mobysBefore) ||
                !Equals(_toolbarCreateBinButton?.Content, toolbarCreateBinContentBefore) ||
                !string.Equals(
                    BuildTerrainEditSignature(_currentGeometry),
                    terrainSignatureBefore,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    BuildMobyEditSignature(_currentMobys),
                    mobySignatureBefore,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    _savedTerrainEditSignature,
                    savedTerrainSignatureBefore,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    _savedMobyEditSignature,
                    savedMobySignatureBefore,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The {label} canonical-document refusal changed authored/output files, operation state, or the loaded scene.");
            }
        }

        try
        {
            Directory.CreateDirectory(paths.AuthoredEditsDirectoryPath);
            File.WriteAllBytes(nearMissPath, "near-miss-must-not-bind"u8.ToArray());
            RefreshId65BlankLabUi();
            RefreshActionAvailability();
            Id65BlankLabUiSnapshot nearMissUi =
                CaptureId65BlankLabUiSnapshotForTesting();
            if (Id65BlankLabHasCanonicalAuthoringDocument() || nearMissUi != uiBefore)
            {
                throw new InvalidOperationException(
                    "A near-miss canonical document filename changed the ID65 writer guards.");
            }
            File.Delete(nearMissPath);

            Directory.CreateDirectory(documentPath);
            RefreshId65BlankLabUi();
            RefreshActionAvailability();
            Id65BlankLabUiSnapshot reservedDirectoryUi =
                CaptureId65BlankLabUiSnapshotForTesting();
            Id65BlankLabAuthoredLayerSnapshot authoredWithReservedDirectory =
                await CaptureId65BlankLabAuthoredLayerAsync(
                    paths.AuthoredEditsDirectoryPath);
            if (!Id65BlankLabHasCanonicalAuthoringDocument() ||
                !Directory.Exists(documentPath) ||
                reservedDirectoryUi.SaveEnabled ||
                reservedDirectoryUi.CreateCueEnabled ||
                !reservedDirectoryUi.AuthoredLayerText.Contains(
                    "filename detected",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "A directory reserving the exact canonical document path did not fail closed.");
            }
            await SaveId65BlankLabWorkspaceAsync();
            if (!(_statusText.Text ?? "").Contains(
                    "terrain save is disabled",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Legacy Save did not refuse a directory at the canonical document path.");
            }
            await CreateId65BlankLabDisposableCueAsync();
            if (!Directory.Exists(documentPath) ||
                !(_statusText.Text ?? "").Contains(
                    "test CUE is disabled",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The disposable-CUE route did not preserve and refuse a directory at the canonical document path.");
            }
            await AssertWriterBoundariesUnchangedAsync(
                "reserved canonical-path directory",
                authoredWithReservedDirectory);
            Directory.Delete(documentPath, recursive: false);

            File.WriteAllBytes(
                documentPath,
                "canonical-filename-only-invalid-json-sentinel"u8.ToArray());
            Id65BlankLabAuthoredLayerSnapshot authoredWithSentinel =
                await CaptureId65BlankLabAuthoredLayerAsync(
                    paths.AuthoredEditsDirectoryPath);
            RefreshId65BlankLabUi();
            RefreshActionAvailability();
            Id65BlankLabUiSnapshot guardedUi =
                CaptureId65BlankLabUiSnapshotForTesting();
            string disclosure = guardedUi.AuthoredLayerText;
            if (!Id65BlankLabHasCanonicalAuthoringDocument() ||
                _id65BlankLabSaveButton == null ||
                _id65BlankLabCreateCueButton == null ||
                _toolbarCreateBinButton == null ||
                guardedUi.SaveEnabled || guardedUi.CreateCueEnabled ||
                !guardedUi.AuthoringDocumentEnabled ||
                guardedUi.AuthoringCandidateEnabled ||
                _toolbarCreateBinButton.IsEnabled ||
                !disclosure.Contains("filename detected", StringComparison.OrdinalIgnoreCase) ||
                !disclosure.Contains("quarantined", StringComparison.OrdinalIgnoreCase) ||
                !TryGetId65BlankLabUnsupportedArtifact(out string unsupportedReason) ||
                !unsupportedReason.Contains("does not consume", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The exact canonical filename did not truthfully disable the complete legacy ID65 writer surface.");
            }

            await SaveId65BlankLabWorkspaceAsync();
            if (!(_statusText.Text ?? "").Contains("terrain save is disabled", StringComparison.OrdinalIgnoreCase) ||
                !(_statusText.Text ?? "").Contains("does not consume", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The direct legacy ID65 Save route did not refuse the canonical filename truthfully.");
            }
            await AssertWriterBoundariesUnchangedAsync(
                "legacy Save",
                authoredWithSentinel);

            await CreateId65BlankLabDisposableCueAsync();
            if (!(_statusText.Text ?? "").Contains("test CUE is disabled", StringComparison.OrdinalIgnoreCase) ||
                !(_statusText.Text ?? "").Contains("does not consume", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The direct legacy ID65 disposable-CUE route masked the canonical filename refusal.");
            }
            await AssertWriterBoundariesUnchangedAsync(
                "legacy disposable CUE",
                authoredWithSentinel);

            await CreateObjectTestBinAsync();
            if (!(_statusText.Text ?? "").Contains("Normal Create BIN is unavailable", StringComparison.OrdinalIgnoreCase) ||
                !(_statusText.Text ?? "").Contains("aggregate materializer", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The direct normal Create BIN route did not refuse the canonical filename truthfully.");
            }
            await AssertWriterBoundariesUnchangedAsync(
                "normal Create BIN",
                authoredWithSentinel);

            LevelDefinition retailLevel = _retailCatalog.Levels.First();
            string retailCreateBinStatus = "";
            try
            {
                _currentLevel = retailLevel;
                RefreshActionAvailability();
                if (_toolbarCreateBinButton?.IsEnabled != true)
                {
                    throw new InvalidOperationException(
                        "The retail-current Create BIN handler was not user-reachable before its canonical filename refusal.");
                }
                await CreateObjectTestBinAsync();
                retailCreateBinStatus = _statusText.Text ?? "";
            }
            finally
            {
                _currentLevel = levelBefore;
                RefreshId65BlankLabUi();
                RefreshActionAvailability();
            }
            if (!retailCreateBinStatus.Contains(
                    "Normal Create BIN is unavailable",
                    StringComparison.OrdinalIgnoreCase) ||
                !retailCreateBinStatus.Contains(
                    "aggregate materializer",
                    StringComparison.OrdinalIgnoreCase) ||
                retailCreateBinStatus.Contains(
                    "use its disposable test writer",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "A retail-current normal Create BIN route did not use the canonical filename-specific refusal.");
            }
            await AssertWriterBoundariesUnchangedAsync(
                "retail-current normal Create BIN",
                authoredWithSentinel);

            int invalidCandidateMaterializerCalls = 0;
            Id65AuthoringCandidateMaterializerOverrideForTesting =
                (_, _, _) =>
                {
                    invalidCandidateMaterializerCalls++;
                    throw new InvalidOperationException(
                        "An invalid canonical envelope reached the candidate materializer seam.");
                };
            await CreateId65AuthoringAggregateCandidateAsync();
            Id65AuthoringCandidateMaterializerOverrideForTesting = null;
            if (invalidCandidateMaterializerCalls != 0 ||
                !(_statusText.Text ?? "").Contains(
                    "Could not create the exact ID65 full-authoring candidate",
                    StringComparison.OrdinalIgnoreCase) ||
                _id65BlankLabBusy || _id65BlankLabManualOperation != null)
            {
                throw new InvalidOperationException(
                    "The invalid canonical envelope did not fail before aggregate candidate materialization.");
            }
            operationGenerationBefore = _id65BlankLabManualOperationGeneration;
            await AssertWriterBoundariesUnchangedAsync(
                "invalid canonical candidate",
                authoredWithSentinel);

            return
                "fresh projection started with generic Save/disposable CUE disabled; exact-name guard preserved the more specific legacy Save, disposable CUE, and normal Create BIN refusals from both ID65 and retail-current routes; near-miss filename did not bind and an exact-path directory failed closed; invalid envelope failed before the explicit candidate materializer; filename-only detection directed inspection to the explicit Full Authoring Document action; handlers recovered toolbar/busy state and changed no authored/output/operation/scene state";
        }
        finally
        {
            Id65AuthoringCandidateMaterializerOverrideForTesting = null;
            _currentLevel = levelBefore;
            if (Directory.Exists(documentPath))
                Directory.Delete(documentPath, recursive: false);
            else if (Id65BlankLabExactPathEntryExistsOrIsUninspectable(documentPath))
                File.Delete(documentPath);
            if (Directory.Exists(nearMissPath))
                Directory.Delete(nearMissPath, recursive: false);
            else if (Id65BlankLabExactPathEntryExistsOrIsUninspectable(nearMissPath))
                File.Delete(nearMissPath);
            if (!authoredBefore.DirectoryExisted &&
                Directory.Exists(paths.AuthoredEditsDirectoryPath) &&
                !Directory.EnumerateFileSystemEntries(
                    paths.AuthoredEditsDirectoryPath).Any())
            {
                Directory.Delete(paths.AuthoredEditsDirectoryPath);
            }

            RefreshId65BlankLabUi();
            RefreshActionAvailability();
            _statusText.Text = mainStatusBefore;
            Id65BlankLabAuthoredLayerSnapshot authoredAfter =
                await CaptureId65BlankLabAuthoredLayerAsync(
                    paths.AuthoredEditsDirectoryPath);
            Id65BlankLabUiSnapshot uiAfter =
                CaptureId65BlankLabUiSnapshotForTesting();
            bool protectedOutputsRestored = true;
            foreach ((string directory, Id65BlankLabAuthoredLayerSnapshot expected) in
                     protectedBefore)
            {
                Id65BlankLabAuthoredLayerSnapshot actual =
                    await CaptureId65BlankLabAuthoredLayerAsync(directory);
                protectedOutputsRestored &= SnapshotIsExact(expected, actual);
            }
            if (!SnapshotIsExact(authoredBefore, authoredAfter) ||
                !protectedOutputsRestored || uiAfter != uiBefore ||
                _id65BlankLabBusy || _id65BlankLabManualOperation != null ||
                _createBinBusy || _loadingLevel ||
                _id65BlankLabManualOperationGeneration != operationGenerationBefore ||
                !ReferenceEquals(
                    _id65BlankLabManualOperationCancellation,
                    operationCancellationBefore) ||
                !ReferenceEquals(_currentLevel, levelBefore) ||
                !ReferenceEquals(_currentGeometry, geometryBefore) ||
                !ReferenceEquals(_currentMobys, mobysBefore) ||
                !Equals(_toolbarCreateBinButton?.Content, toolbarCreateBinContentBefore))
            {
                throw new InvalidOperationException(
                    "The canonical-document writer guard probe did not restore its exact pre-probe files, UI, or operation state.");
            }
        }
    }

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

        if (Id65FreshLevelSceneProjector.IsProjectionGeometry(_currentGeometry))
        {
            TerrainPolygon hp = _currentGeometry.Polygons.Single();
            LowDetailTerrainPolygon lp = _currentGeometry.LowDetailPolygons.Single();
            NativeTerrainOcclusionData occlusion = _currentGeometry.NativeTerrainOcclusion
                ?? throw new InvalidOperationException(
                    "The fresh ID65 starter-scene probe found no collision/occlusion data.");
            PortableLevelEntryPose entry = TryLoadLevelEntryPose(_currentLevel.Key)
                ?? throw new InvalidOperationException(
                    "The fresh ID65 starter-scene probe found no protected entry pose.");
            Id65FreshLevelT66ViewportTextureProjection t66 =
                _id65FreshLevelT66ViewportTextureProjection
                ?? throw new InvalidOperationException(
                    "The fresh ID65 starter-scene probe found no witnessed in-memory T66 projection.");
            Id65BlankLabAuthoredLayerSnapshot cacheBefore =
                await CaptureId65BlankLabAuthoredLayerAsync(
                    Id65BlankLabCacheDirectory(_id65BlankLabPaths));
            Id65BlankLabAuthoredLayerSnapshot authoredBefore =
                await CaptureId65BlankLabAuthoredLayerAsync(
                    _id65BlankLabPaths.AuthoredEditsDirectoryPath);
            bool closeBitmap = _viewport.LoadTerrainTextureImageForTesting(
                Id65FreshLevelSceneProjector.StarterTextureId,
                79.999);
            bool normalBitmap = _viewport.LoadTerrainTextureImageForTesting(
                Id65FreshLevelSceneProjector.StarterTextureId,
                511);
            NativeTerrainLqFrameSnapshot closeComposite =
                _viewport.CaptureNativeTerrainLqFrameForTesting(
                    Id65FreshLevelSceneProjector.StarterTextureId,
                    79.999);
            NativeTerrainLqFrameSnapshot normalComposite =
                _viewport.CaptureNativeTerrainLqFrameForTesting(
                    Id65FreshLevelSceneProjector.StarterTextureId,
                    511);
            NativeTerrainLqFrameSnapshot farLqComposite =
                _viewport.CaptureNativeTerrainLqFrameForTesting(
                    Id65FreshLevelSceneProjector.StarterTextureId,
                    1_088);
            if (_currentMobys.Count != 0 ||
                _currentGeometry.Polygons.Count != 1 ||
                _currentGeometry.LowDetailPolygons.Count != 1 ||
                _currentGeometry.SourceSectors.Count != 1 ||
                hp.RuntimeKey != "216:0:hp" ||
                hp.TextureId != Id65FreshLevelSceneProjector.StarterTextureId ||
                hp.NativeTextureId != Id65FreshLevelSceneProjector.StarterTextureId ||
                hp.NativeFaceWord2 != 0x01400242 ||
                lp.RuntimeKey != "216:0:lp" ||
                lp.RawWord0 != 0x00108200 ||
                lp.RawWord1 != 0x00108200 ||
                occlusion.CollisionTriangles.Count != 1 ||
                occlusion.CollisionTriangles[0].Index !=
                    Id65FreshLevelSceneProjector.CollisionTriangleIndex ||
                occlusion.VisibleSectorsForGroup(
                    Id65FreshLevelSceneProjector.OcclusionGroupIndex)?.SetEquals([216]) != true ||
                entry.RawX != 6_153 || entry.RawY != 6_154 || entry.RawZ != 8_550 ||
                entry.YawByte != 0x40 ||
                t66.TextureId != Id65FreshLevelSceneProjector.StarterTextureId ||
                t66.LowDetailTextures.TextureCount != 67 ||
                t66.HighDetailMaterials.TextureCount != 67 ||
                !t66.LowDetailTextures.TryGetTexture(66, out _) ||
                !t66.HighDetailMaterials.TryGetTexture(66, out _) ||
                !closeBitmap ||
                !normalBitmap ||
                !closeComposite.HasIndexedPayload ||
                !closeComposite.BitmapResolved ||
                !closeComposite.HqOverlayEligible ||
                closeComposite.HqTier != NativeTerrainTexturePreviewTier.Close ||
                closeComposite.BitmapWidth != 64 ||
                closeComposite.BitmapHeight != 64 ||
                !normalComposite.HasIndexedPayload ||
                !normalComposite.BitmapResolved ||
                !normalComposite.HqOverlayEligible ||
                normalComposite.HqTier != NativeTerrainTexturePreviewTier.Normal ||
                normalComposite.BitmapWidth != 64 ||
                normalComposite.BitmapHeight != 64 ||
                !farLqComposite.HighPolyVisible ||
                farLqComposite.HqOverlayEligible ||
                farLqComposite.HqTier != null ||
                !farLqComposite.HasIndexedPayload ||
                !farLqComposite.BitmapResolved ||
                farLqComposite.DescriptorIndex != 0 ||
                farLqComposite.PaletteRow != 1 ||
                farLqComposite.BitmapWidth != 32 ||
                farLqComposite.BitmapHeight != 32 ||
                TryValidateId65BlankLabTerrainForDisposableCue(_currentGeometry, out string reason) ||
                !reason.Contains("authoring presentation", StringComparison.OrdinalIgnoreCase) ||
                _id65BlankLabCreateCueButton?.IsEnabled == true)
            {
                throw new InvalidOperationException(
                    "The fresh ID65 starter scene lost its exact pad/T66/collision/protected-spawn/no-visible-Moby boundary.");
            }

            _selectedTerrain = hp;
            _selectedTerrainIndex = 0;
            float[] hpZBefore = hp.ZValues.ToArray();
            string terrainSignatureBefore = BuildTerrainEditSignature(_currentGeometry);
            NudgeSelectedTerrain(16);
            if (!hp.ZValues.SequenceEqual(hpZBefore) ||
                !string.Equals(
                    BuildTerrainEditSignature(_currentGeometry),
                    terrainSignatureBefore,
                    StringComparison.Ordinal) ||
                !(_statusText.Text ?? "").Contains(
                    "Full Authoring Document",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The fresh ID65 projection accepted or misreported a generic HP-Z mutation.");
            }
            _ = ApplyTerrainBrush(
                hp.Center,
                radius: 64,
                static (_, _, _) => 32);
            if (!hp.ZValues.SequenceEqual(hpZBefore) ||
                !(_statusText.Text ?? "").Contains(
                    "terrain brush sculpting",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The fresh ID65 projection accepted a direct terrain-brush mutation.");
            }
            bool saved = await SaveId65BlankLabWorkspaceCoreAsync(announce: true);
            if (saved ||
                !(_statusText.Text ?? "").Contains(
                    "no TerrainEditStore file was written",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The fresh ID65 projection accepted or misreported generic terrain persistence.");
            }
            await CreateId65BlankLabDisposableCueAsync();
            if (!(_statusText.Text ?? "").Contains(
                    "legacy ID65 disposable terrain-only CUE is unavailable",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The fresh ID65 projection did not refuse the stale disposable-CUE path.");
            }
            RefreshId65BlankLabUi();
            RefreshActionAvailability();
            if (_id65BlankLabSaveButton?.IsEnabled == true ||
                _id65BlankLabCreateCueButton?.IsEnabled == true ||
                _terrainUndoButton?.IsEnabled == true ||
                _terrainTaskPaintFaceButton?.IsEnabled == true ||
                _terrainTaskSaveButton?.IsEnabled == true ||
                _releaseTerrainRaiseFaceButton?.IsEnabled == true ||
                _releaseTerrainLowerFaceButton?.IsEnabled == true ||
                _terrainPointRaiseButton?.IsEnabled == true ||
                _terrainPointLowerButton?.IsEnabled == true)
            {
                throw new InvalidOperationException(
                    "The fresh ID65 projection left a generic terrain mutation or legacy writer control enabled.");
            }
            Id65BlankLabAuthoredLayerSnapshot cacheAfter =
                await CaptureId65BlankLabAuthoredLayerAsync(
                    Id65BlankLabCacheDirectory(_id65BlankLabPaths));
            Id65BlankLabAuthoredLayerSnapshot authoredAfter =
                await CaptureId65BlankLabAuthoredLayerAsync(
                    _id65BlankLabPaths.AuthoredEditsDirectoryPath);
            if (!Id65BlankLabLayerSnapshotsEqual(cacheBefore, cacheAfter) ||
                !Id65BlankLabLayerSnapshotsEqual(authoredBefore, authoredAfter) ||
                File.Exists(Id65BlankLabTerrainEditsPath()))
            {
                throw new InvalidOperationException(
                    "T66 preview or refused generic mutation changed the ID65 cache/authored layer or created a TerrainEditStore file.");
            }

            return
                $"fresh projection exposed one HP/LP pad with fixed private T{Id65FreshLevelSceneProjector.StarterTextureId}; real close/normal HQ and game-camera far-LQ composites resolved from complete 67-record in-memory sets; matching T{Id65FreshLevelSceneProjector.CollisionTriangleIndex}/group-0 collision and protected spawn remained coupled; generic HP-Z/brush/save and the legacy disposable writer refused without cache/authored writes, zero visible Mobys, Create BIN/runtime/release authority false";
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
                ApplicationSupportRoot: Path.Combine(_workspace.RootPath, "support"),
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
        string labAuthoredTerrainPath = Id65BlankLabTerrainEditsPath();
        byte[]? preservedLabAuthoredTerrain = File.Exists(labAuthoredTerrainPath)
            ? File.ReadAllBytes(labAuthoredTerrainPath)
            : null;

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

            if (!Id65FreshLevelSceneProjector.IsProjectionGeometry(
                    _currentGeometry) ||
                !Id65FreshLevelSceneProjector.IsExactProjectionGeometry(
                    _currentGeometry) ||
                _currentMobys.Count != 0 ||
                _loadedTerrainEdits != 0)
            {
                throw new InvalidOperationException(
                    "The guarded retail-to-ID65 transition did not finish on the exact mandatory fresh projection with zero visible Mobys and no TerrainEditStore rows.");
            }
            GeometryCandidate freshGeometry = _currentGeometry;
            string freshSignature = BuildTerrainEditSignature(freshGeometry);
            TerrainPolygon freshFace = freshGeometry.Polygons.Single();
            float[] attemptedFreshHeight = freshFace.TerrainVertexDeltas().ToArray();
            attemptedFreshHeight[0] += 9;
            freshFace.ApplyTerrainVertexDeltas(attemptedFreshHeight);
            bool genericFreshSave = await TrySaveCurrentTerrainEditsAsync();
            freshFace.ResetTerrainEdit();
            if (genericFreshSave ||
                File.Exists(labAuthoredTerrainPath) ||
                !ReferenceEquals(_currentGeometry, freshGeometry) ||
                BuildTerrainEditSignature(_currentGeometry) != freshSignature ||
                !(_statusText.Text ?? "").Contains(
                    "Generic ID65 terrain save is unavailable",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The fresh ID65 transition allowed generic HP-Z persistence or changed its exact projection while refusing it.");
            }

            return "Cancel preserved retail level/camera/selection/unsaved edits; Save persisted retail terrain and Mobys before the guarded exact-ID65 transition; the production route committed the mandatory fresh projection with zero visible Mobys and no TerrainEditStore rows; generic fresh-scene HP-Z persistence failed closed without replacing the viewport";

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
            if (preservedLabAuthoredTerrain == null)
            {
                if (File.Exists(labAuthoredTerrainPath))
                    File.Delete(labAuthoredTerrainPath);
            }
            else
            {
                Directory.CreateDirectory(
                    Path.GetDirectoryName(labAuthoredTerrainPath) ??
                    _workspace.RootPath);
                File.WriteAllBytes(
                    labAuthoredTerrainPath,
                    preservedLabAuthoredTerrain);
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
                    labUi.BuildEnabled || labUi.LoadEnabled ||
                    labUi.AuthoringDocumentEnabled || labUi.SaveEnabled ||
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
                    labUi.BuildEnabled || labUi.LoadEnabled ||
                    labUi.AuthoringDocumentEnabled || labUi.SaveEnabled ||
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
                busySnapshot.AuthoringDocumentEnabled ||
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
            bool freshProjection =
                Id65FreshLevelSceneProjector.IsProjectionGeometry(originalGeometry);
            await AssertCanceledBeforeMutationAsync(
                "build-started",
                BuildOrRefreshId65BlankLabBaseAsync);
            await AssertCanceledBeforeMutationAsync(
                "load-started",
                LoadId65BlankLabAsync);
            await AssertCanceledBeforeMutationAsync(
                "load-work-completed",
                LoadId65BlankLabAsync);
            if (!freshProjection)
            {
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
            }
            else
            {
                int immediateGeneration = _id65BlankLabManualOperationGeneration;
                bool immediateSave = await SaveId65BlankLabWorkspaceCoreAsync(announce: true);
                bool immediateRestore = await RestoreId65BlankLabTerrainAsync("terrain");
                await CreateId65BlankLabDisposableCueAsync();
                bool automaticPersistenceRefused = false;
                try
                {
                    _ = await PersistCurrentTerrainEditsAsync();
                }
                catch (InvalidOperationException)
                {
                    automaticPersistenceRefused = true;
                }
                if (immediateSave || immediateRestore || !automaticPersistenceRefused ||
                    _id65BlankLabBusy || _id65BlankLabManualOperation != null ||
                    _id65BlankLabManualOperationGeneration != immediateGeneration ||
                    !AuthoredFileIsExact())
                {
                    throw new InvalidOperationException(
                        "Fresh projection Save, automatic persistence, disposable CUE, or restore did not refuse before opening a manual operation or touching the generic terrain file.");
                }
            }

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
                navigationBusySnapshot.AuthoringDocumentEnabled ||
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

            if (!freshProjection)
            {
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
            }

            return
                freshProjection
                    ? "Build and Load canceled at start/after worker completion while viewport, regular mutation/Undo/Save, workspace/level controls, and peer Lab commands stayed fenced; fresh-projection Save, automatic persistence, disposable CUE, and restore refused before opening an operation or touching generic terrain bytes; reverse-order regular level loading refused Lab operations; delayed load completions could not commit across generation switches"
                    : "Build, Load, manual/automatic Save, Create CUE, and Restore canceled at start and after load/save/restore worker completion; viewport, regular terrain mutation/Undo/Save routes, workspace and level controls, and peer Lab commands stayed fenced; overlapping Save was single-flight; reverse-order regular level loading refused Lab operations; delayed completions could not commit status, identity, signatures, reveal state, or authored bytes across generation switches";
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
        UnusedLevel65BlankLevelLabWorkspacePaths originalId65Paths =
            _id65BlankLabPaths ?? throw new InvalidOperationException(
                "The workspace-transition probe requires admitted exact ID65 paths.");
        UnusedLevel65BlankLevelLabManifest originalId65Manifest =
            _id65BlankLabManifest ?? throw new InvalidOperationException(
                "The workspace-transition probe requires an admitted exact ID65 manifest.");
        if (_catalog.FindByKey(
                UnusedLevel65BlankLevelLabProfileRegistry.Key) == null)
        {
            throw new InvalidOperationException(
                "The workspace-transition probe requires ID65 to be admitted before its background-validation transition checks.");
        }

        string targetRoot = Path.Combine(
            Path.GetTempPath(),
            $"spyro-editor-workspace-transition-{Guid.NewGuid():N}");
        string secondTargetRoot = Path.Combine(
            Path.GetTempPath(),
            $"spyro-editor-workspace-transition-second-{Guid.NewGuid():N}");
        string selectedTargetRoot = targetRoot;
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
        string originalRevealPath = _id65BlankLabRevealPath;
        string workspaceBoundRevealFixture = originalId65Paths.LockedBaseCuePath;
        _id65BlankLabRevealPath = workspaceBoundRevealFixture;
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
            string.Equals(
                _id65BlankLabRevealPath,
                workspaceBoundRevealFixture,
                StringComparison.Ordinal) &&
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
                labUi.BuildEnabled || labUi.LoadEnabled ||
                labUi.AuthoringDocumentEnabled || labUi.SaveEnabled ||
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
            Task.FromResult<string?>(selectedTargetRoot);
        UnsavedTerrainDecisionOverrideForTesting = (_, _) =>
            Task.FromResult("Discard");
        TaskCompletionSource<UnusedLevel65BlankLevelLabManifest>?
            targetValidationGate = null;
        TaskCompletionSource<UnusedLevel65BlankLevelLabManifest>?
            rearmedValidationGate = null;
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

            TaskCompletionSource targetValidationStarted =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource rearmedValidationStarted =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            targetValidationGate = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            rearmedValidationGate = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int validationCallCount = 0;
            CancellationToken targetValidationToken = default;
            CancellationToken rearmedValidationToken = default;
            string targetValidationRoot = "";
            string rearmedValidationRoot = "";
            Id65BlankLabCatalogValidationOverrideForTesting =
                (paths, cancellationToken) =>
                {
                    int call = Interlocked.Increment(ref validationCallCount);
                    if (call == 1)
                    {
                        targetValidationRoot = paths.ContainerRootPath;
                        targetValidationToken = cancellationToken;
                        targetValidationStarted.TrySetResult();
                        return targetValidationGate.Task;
                    }
                    if (call == 2)
                    {
                        rearmedValidationRoot = paths.ContainerRootPath;
                        rearmedValidationToken = cancellationToken;
                        rearmedValidationStarted.TrySetResult();
                        return rearmedValidationGate.Task;
                    }
                    return Task.FromException<UnusedLevel65BlankLevelLabManifest>(
                        new InvalidOperationException(
                            $"Unexpected background ID65 validation call {call}."));
                };

            int successfulPrepareCount = 0;
            WorkspaceTransitionDelayOverrideForTesting = (stage, _) =>
            {
                if (string.Equals(stage, "prepare-completed", StringComparison.Ordinal))
                    successfulPrepareCount++;
                return Task.CompletedTask;
            };
            Task successfulTransition = OpenWorkspaceAsync();
            await targetValidationStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await successfulTransition.WaitAsync(TimeSpan.FromSeconds(5));
            WorkspaceTransitionDelayOverrideForTesting = null;
            Task targetValidationTask =
                CurrentId65BlankLabCatalogValidationTaskForTesting;

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
                    targetRoot) ||
                targetValidationTask.IsCompleted ||
                targetValidationGate.Task.IsCompleted ||
                !_id65BlankLabCatalogValidationBusy ||
                !PathsEqual(targetValidationRoot, targetRoot) ||
                !string.IsNullOrWhiteSpace(_id65BlankLabRevealPath))
            {
                throw new InvalidOperationException(
                    "The successful workspace transition did not commit exactly once, retained stale source-scene/global context, or waited for background ID65 validation.");
            }

            Id65BlankLabUiSnapshot targetValidationUi =
                CaptureId65BlankLabUiSnapshotForTesting();
            if (targetValidationUi.RetailCatalogCount != 35 ||
                targetValidationUi.HasId65CatalogLevel ||
                !targetValidationUi.StatusText.Contains(
                    "background",
                    StringComparison.Ordinal) ||
                targetValidationUi.BuildEnabled || targetValidationUi.LoadEnabled ||
                targetValidationUi.AuthoringDocumentEnabled ||
                targetValidationUi.SaveEnabled ||
                targetValidationUi.CreateCueEnabled ||
                !_levelJumpBox.IsEnabled || !_levelList.IsEnabled ||
                !_viewport.IsEnabled ||
                (_openWorkspaceButton != null &&
                 !_openWorkspaceButton.IsEnabled) ||
                _id65BlankLabManifest != null)
            {
                throw new InvalidOperationException(
                    $"A gated target ID65 validation did not leave the committed retail workspace available and fail-closed: {targetValidationUi}");
            }

            selectedTargetRoot = secondTargetRoot;
            TaskCompletionSource secondPrepareStarted =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource secondPrepareRelease =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            WorkspaceTransitionDelayOverrideForTesting = (stage, cancellationToken) =>
            {
                if (!string.Equals(stage, "prepare-completed", StringComparison.Ordinal))
                    return Task.CompletedTask;
                secondPrepareStarted.TrySetResult();
                return secondPrepareRelease.Task.WaitAsync(cancellationToken);
            };
            Task canceledSecondTransition = OpenWorkspaceAsync();
            await secondPrepareStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            if (!targetValidationToken.IsCancellationRequested ||
                targetValidationTask.IsCompleted ||
                !_workspaceTransitionBusy || _workspaceTransitionOperation == null)
            {
                throw new InvalidOperationException(
                    "A new Open Workspace action waited for or failed to cancel the old background ID65 validator before taking its transition fence.");
            }
            CancelWorkspaceTransitionForTesting();
            secondPrepareRelease.TrySetResult();
            await canceledSecondTransition;
            WorkspaceTransitionDelayOverrideForTesting = null;
            await rearmedValidationStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Task rearmedValidationTask =
                CurrentId65BlankLabCatalogValidationTaskForTesting;
            if (_workspaceTransitionBusy || _workspaceTransitionOperation != null ||
                ReferenceEquals(rearmedValidationTask, targetValidationTask) ||
                rearmedValidationTask.IsCompleted ||
                rearmedValidationGate.Task.IsCompleted ||
                rearmedValidationToken.IsCancellationRequested ||
                !PathsEqual(rearmedValidationRoot, targetRoot) ||
                !PathsEqual(_workspace.RootPath, targetRoot) ||
                !_id65BlankLabCatalogValidationBusy)
            {
                throw new InvalidOperationException(
                    "Canceling a workspace transition did not rearm a fresh validator for the unchanged committed workspace.");
            }

            targetValidationGate.TrySetResult(originalId65Manifest);
            await targetValidationTask.WaitAsync(TimeSpan.FromSeconds(30));
            if (!_id65BlankLabCatalogValidationBusy ||
                _id65BlankLabManifest != null ||
                _catalog.FindByKey(
                    UnusedLevel65BlankLevelLabProfileRegistry.Key) != null)
            {
                throw new InvalidOperationException(
                    "A canceled token-ignoring ID65 validator admitted stale manifest state after a newer validation generation began.");
            }

            rearmedValidationGate.TrySetException(
                new InvalidDataException(
                    "injected current-generation background validation fault"));
            await rearmedValidationTask.WaitAsync(TimeSpan.FromSeconds(30));
            Id65BlankLabUiSnapshot failedValidationUi =
                CaptureId65BlankLabUiSnapshotForTesting();
            if (validationCallCount != 2 ||
                _id65BlankLabCatalogValidationBusy ||
                _id65BlankLabManifest != null ||
                failedValidationUi.RetailCatalogCount != 35 ||
                failedValidationUi.HasId65CatalogLevel ||
                !failedValidationUi.BuildEnabled || failedValidationUi.LoadEnabled ||
                failedValidationUi.AuthoringDocumentEnabled ||
                failedValidationUi.SaveEnabled || failedValidationUi.CreateCueEnabled ||
                !failedValidationUi.StatusText.Contains(
                    "injected current-generation background validation fault",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The fresh current-generation background validation fault did not settle retail-only and fail-closed: {failedValidationUi}");
            }

            return
                "Open Workspace held one captured generation/CTS fence through confirmation, owned saves, target preparation, transactional release settings/environment/context publication, and app workspace/catalog commit; delayed mutation/navigation/Lab admission stayed refused, cancellation preserved the exact old state, injected post-publication failure rolled global state back, success committed once without retaining the old scene or waiting for full-BIN ID65 validation, another Open canceled the old validator and took the transition fence, canceled transition rearmed the unchanged workspace, stale token-ignoring completion stayed unadmitted, and a current-generation validation fault settled retail-only";
        }
        finally
        {
            WorkspaceFolderPickerOverrideForTesting = null;
            WorkspaceTransitionDelayOverrideForTesting = null;
            WorkspaceTransitionFaultForTesting = null;
            UnsavedTerrainDecisionOverrideForTesting = null;
            Id65BlankLabCatalogValidationOverrideForTesting = null;
            CancelId65BlankLabCatalogValidation();
            targetValidationGate?.TrySetCanceled();
            rearmedValidationGate?.TrySetCanceled();
            if (_workspaceTransitionOperation != null)
            {
                CancelWorkspaceTransitionForTesting();
                CompleteWorkspaceTransition(_workspaceTransitionOperation);
            }
            if (PathsEqual(_workspace.RootPath, originalWorkspace.RootPath))
            {
                _id65BlankLabRevealPath = originalRevealPath;
                _selectedMoby = originalSelectedMoby;
                _selectedTerrain = originalSelectedTerrain;
                _selectedTerrainIndex = originalSelectedTerrainIndex;
                _selectedTerrainPointIndex = originalSelectedTerrainPointIndex;
            }
            foreach (string temporaryRoot in new[] { targetRoot, secondTargetRoot })
            {
                try
                {
                    if (Directory.Exists(temporaryRoot))
                        Directory.Delete(temporaryRoot, recursive: true);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    Debug.WriteLine(exception);
                }
            }
        }
    }

    internal async Task<string>
        AssertId65CanonicalAuthoringDocumentEditorForTestingAsync()
    {
        if (_id65BlankLabManifest == null || _id65BlankLabPaths == null)
        {
            throw new InvalidOperationException(
                "The canonical authoring editor probe requires an admitted exact ID65 workspace.");
        }
        if (_id65BlankLabBusy || _id65BlankLabManualOperation != null ||
            _createBinBusy || _loadingLevel)
        {
            throw new InvalidOperationException(
                "The canonical authoring editor probe requires an idle editor.");
        }

        UnusedLevel65BlankLevelLabWorkspacePaths paths = _id65BlankLabPaths;
        UnusedLevel65BlankLevelLabManifest manifest = _id65BlankLabManifest;
        UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(manifest, paths);
        string documentPath = Id65BlankLabAuthoringDocumentPath();
        if (Id65BlankLabHasCanonicalAuthoringDocument())
        {
            throw new InvalidOperationException(
                "The canonical authoring editor probe refuses to replace a pre-existing fixture.");
        }
        string legacyPath = Id65BlankLabTerrainEditsPath();
        if (TerrainEditFileHasEdits(legacyPath))
        {
            throw new InvalidOperationException(
                "The canonical authoring editor probe requires no positive legacy terrain edits.");
        }

        LevelDefinition? levelBefore = _currentLevel;
        GeometryCandidate? geometryBefore = _currentGeometry;
        List<Moby> mobysBefore = _currentMobys;
        string terrainSignatureBefore = _currentGeometry == null
            ? ""
            : BuildTerrainEditSignature(_currentGeometry);
        string mobySignatureBefore = BuildMobyEditSignature(_currentMobys);
        FileInfo lockedImage = new(paths.LockedBaseImagePath);
        long lockedImageLengthBefore = lockedImage.Length;
        DateTime lockedImageWriteBefore = lockedImage.LastWriteTimeUtc;
        byte[] cueBefore = await File.ReadAllBytesAsync(paths.LockedBaseCuePath);
        byte[] manifestBefore = await File.ReadAllBytesAsync(paths.ManifestPath);
        string candidateDirectory = Path.Combine(
            paths.RootPath,
            "materialized-candidates",
            "ui-smoke-exact-full-candidate");
        string candidateImage = Path.Combine(candidateDirectory, "candidate.bin");
        string candidateCue = Path.Combine(candidateDirectory, "candidate.cue");
        string candidateReceipt = Path.Combine(candidateDirectory, "candidate.receipt.json");
        Directory.CreateDirectory(candidateDirectory);
        await File.WriteAllBytesAsync(candidateImage, "ui-smoke-image"u8.ToArray());
        await File.WriteAllBytesAsync(candidateCue, "ui-smoke-cue"u8.ToArray());
        await File.WriteAllBytesAsync(candidateReceipt, "ui-smoke-receipt"u8.ToArray());
        string[] protectedOutputDirectories =
        [
            paths.OperationsDirectoryPath,
            Path.Combine(paths.RootPath, ".terrain-test-operations"),
            Path.Combine(paths.RootPath, "runtime-tests"),
            Path.Combine(paths.RootPath, "materialized-candidates"),
            Path.Combine(_workspace.RootPath, "output")
        ];
        Dictionary<string, Id65BlankLabAuthoredLayerSnapshot> protectedBefore =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (string directory in protectedOutputDirectories)
        {
            protectedBefore[directory] =
                await CaptureId65BlankLabAuthoredLayerAsync(directory);
        }

        static bool Equivalent(
            Id65AuthoringEditorControlState left,
            Id65AuthoringEditorControlState right) =>
            left.IncludeStructuralTerrain == right.IncludeStructuralTerrain &&
            left.IncludeGrassAtT88 == right.IncludeGrassAtT88 &&
            left.IncludeGrassAtT107 == right.IncludeGrassAtT107 &&
            left.MandatoryBaseFaceTextureId == right.MandatoryBaseFaceTextureId &&
            left.T107PlacementSemantics == right.T107PlacementSemantics &&
            string.Equals(
                left.T107RemotePadPositionId,
                right.T107RemotePadPositionId,
                StringComparison.Ordinal);

        try
        {
            int dialogCount = 0;
            Id65AuthoringDocumentDialogOverrideForTesting =
                (initial, exists, _) =>
                {
                    dialogCount++;
                    if (exists || !Equivalent(
                            initial,
                            Id65AuthoringEditorControlState.CreateRemotePad(
                                includeStructuralTerrain: false,
                                includeGrassAtT88: false,
                                includeGrassAtT107: false,
                                Id65RemotePadGrassT107Catalog.DefaultPositionId,
                                Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId)))
                    {
                        throw new InvalidOperationException(
                            "The missing-document dialog did not receive the exact blank schema-3 starting state.");
                    }
                    return Task.FromResult<Id65AuthoringEditorControlState?>(null);
                };
            await EditId65AuthoringDocumentAsync();
            if (dialogCount != 1 || File.Exists(documentPath) ||
                !(_statusText.Text ?? "").Contains(
                    "closed without saving",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Cancelling the missing canonical document dialog changed a file or status boundary.");
            }

            Id65AuthoringDocumentDialogOverrideForTesting =
                (initial, exists, _) =>
                {
                    if (exists)
                    {
                        throw new InvalidOperationException(
                            "The first canonical save was not based on an inspected absence.");
                    }
                    return Task.FromResult<Id65AuthoringEditorControlState?>(
                        Id65AuthoringEditorControlState.CreateDefault());
                };
            await EditId65AuthoringDocumentAsync();
            Id65AuthoringEditorDocumentReadModel created =
                await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                    paths,
                    manifest)
                ?? throw new InvalidOperationException(
                    "The explicit App action did not create its canonical document.");
            Id65AuthoringEditorControlState createdState =
                Id65AuthoringEditorControlStateCompiler.RecoverControlState(created);
            if (!Equivalent(
                    createdState,
                    Id65AuthoringEditorControlState.CreateDefault()) ||
                !(_statusText.Text ?? "").Contains(
                    "Created the canonical ID65 Full Authoring Document",
                    StringComparison.Ordinal) ||
                !(_statusText.Text ?? "").Contains(
                    "normal Create BIN remains blocked",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The explicit App create action lost its exact state or fail-closed status.");
            }

            GeometryCandidate createdProjection = _currentGeometry ??
                throw new InvalidOperationException(
                    "The explicit App create action did not publish a replacement viewport projection.");
            Id65BlankLabUiSnapshot createdProjectionUi =
                CaptureId65BlankLabUiSnapshotForTesting();
            if (ReferenceEquals(createdProjection, geometryBefore) ||
                createdProjection.Polygons.Count != 2 ||
                createdProjection.LowDetailPolygons.Count != 2 ||
                !Id65FreshLevelSceneProjector.IsExactProjectionGeometry(
                    createdProjection) ||
                !Id65AuthoringDocumentSnapshotsEqual(
                    _id65FreshAuthoringDocument,
                    created) ||
                !createdProjectionUi.OptionalTerrainVisible ||
                createdProjectionUi.CreateOptionalTerrainEnabled ||
                !createdProjectionUi.RemoveOptionalTerrainEnabled)
            {
                throw new InvalidOperationException(
                    "The explicit App create action did not atomically apply its terrain-present document projection and controls.");
            }

            byte[] beforeInjectedCasFailure =
                await File.ReadAllBytesAsync(documentPath);
            GeometryCandidate viewportBeforeInjectedCasFailure =
                _currentGeometry;
            Id65AuthoringTerrainExpectedEnvelopeOverrideForTesting =
                _ => new string('0', 64);
            await SetId65FreshOptionalTerrainAsync(
                includeStructuralTerrain: false);
            Id65AuthoringTerrainExpectedEnvelopeOverrideForTesting = null;
            if (!(await File.ReadAllBytesAsync(documentPath))
                    .SequenceEqual(beforeInjectedCasFailure) ||
                !ReferenceEquals(
                    _currentGeometry,
                    viewportBeforeInjectedCasFailure) ||
                !ReferenceEquals(
                    _viewport.Geometry,
                    viewportBeforeInjectedCasFailure) ||
                !(_statusText.Text ?? "").Contains(
                    "prior viewport remains active",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The injected optional-terrain CAS failure changed the canonical document or prior viewport.");
            }

            string[] retailPersistencePaths = _retailCatalog.Levels
                .SelectMany(level => new[]
                {
                    Path.Combine(
                        _workspace.RootPath,
                        $"{level.Key}-terrain-edits.json"),
                    Path.Combine(
                        _workspace.RootPath,
                        $"{level.Key}-native-edits.json"),
                    NativeMobyPathEditPath(level.Key)
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            Dictionary<string, byte[]?> retailPersistenceBeforeApplyFault =
                retailPersistencePaths.ToDictionary(
                    path => path,
                    path => File.Exists(path) ? File.ReadAllBytes(path) : null,
                    StringComparer.OrdinalIgnoreCase);
            int partialApplyFaultCalls = 0;
            Id65FreshProjectionApplyFaultForTesting = stage =>
            {
                if (string.Equals(
                        stage,
                        "after-viewport-geometry-before-dependent-refresh",
                        StringComparison.Ordinal) &&
                    partialApplyFaultCalls++ == 0)
                {
                    throw new InvalidOperationException(
                        "Injected partial ID65 viewport apply failure.");
                }
            };
            try
            {
                await SetId65FreshOptionalTerrainAsync(
                    includeStructuralTerrain: false);
            }
            finally
            {
                Id65FreshProjectionApplyFaultForTesting = null;
            }
            Id65AuthoringEditorDocumentReadModel recoveredAfterApplyFault =
                await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                    paths,
                    manifest) ?? throw new InvalidOperationException(
                    "The partial-apply recovery lost the valid saved document.");
            Id65AuthoringEditorControlState recoveredAfterApplyFaultState =
                Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                    recoveredAfterApplyFault);
            Id65BlankLabUiSnapshot recoveredAfterApplyFaultUi =
                CaptureId65BlankLabUiSnapshotForTesting();
            bool retailPersistencePreserved = retailPersistenceBeforeApplyFault.All(
                pair => pair.Value == null
                    ? !File.Exists(pair.Key)
                    : File.Exists(pair.Key) &&
                      File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value));
            if (partialApplyFaultCalls < 1 ||
                recoveredAfterApplyFaultState.IncludeStructuralTerrain ||
                !recoveredAfterApplyFaultState.IncludeGrassAtT88 ||
                !recoveredAfterApplyFaultState.IncludeGrassAtT107 ||
                !Id65AuthoringDocumentSnapshotsEqual(
                    _id65FreshAuthoringDocument,
                    recoveredAfterApplyFault) ||
                !_id65FreshDocumentProjectionSynchronized ||
                _currentGeometry?.Polygons.Count != 1 ||
                _currentGeometry.LowDetailPolygons.Count != 1 ||
                !Id65FreshLevelSceneProjector.IsExactProjectionGeometry(
                    _currentGeometry) ||
                !ReferenceEquals(_viewport.Geometry, _currentGeometry) ||
                !recoveredAfterApplyFaultUi.CreateOptionalTerrainEnabled ||
                recoveredAfterApplyFaultUi.RemoveOptionalTerrainEnabled ||
                !(_statusText.Text ?? "").Contains(
                    "document save succeeded",
                    StringComparison.OrdinalIgnoreCase) ||
                !(_statusText.Text ?? "").Contains(
                    "recovered",
                    StringComparison.OrdinalIgnoreCase) ||
                File.Exists(legacyPath) ||
                !retailPersistencePreserved ||
                _id65BlankLabBusy || _id65BlankLabManualOperation != null)
            {
                throw new InvalidOperationException(
                    "A partial post-save viewport failure did not finish fully synchronized to the valid saved document with truthful status and no TerrainEditStore/retail writes.");
            }

            await SetId65FreshOptionalTerrainAsync(
                includeStructuralTerrain: true);

            await SetId65FreshOptionalTerrainAsync(
                includeStructuralTerrain: false);
            Id65AuthoringEditorDocumentReadModel terrainAbsent =
                await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                    paths,
                    manifest) ?? throw new InvalidOperationException(
                    "Removing optional terrain lost the canonical document.");
            Id65AuthoringEditorControlState terrainAbsentState =
                Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                    terrainAbsent);
            GeometryCandidate terrainAbsentProjection = _currentGeometry ??
                throw new InvalidOperationException(
                    "Removing optional terrain lost the current viewport geometry.");
            Id65BlankLabUiSnapshot terrainAbsentUi =
                CaptureId65BlankLabUiSnapshotForTesting();
            if (terrainAbsentState.IncludeStructuralTerrain ||
                !terrainAbsentState.IncludeGrassAtT88 ||
                !terrainAbsentState.IncludeGrassAtT107 ||
                terrainAbsentProjection.Polygons.Count != 1 ||
                terrainAbsentProjection.LowDetailPolygons.Count != 1 ||
                ReferenceEquals(
                    terrainAbsentProjection,
                    viewportBeforeInjectedCasFailure) ||
                !terrainAbsentUi.CreateOptionalTerrainEnabled ||
                terrainAbsentUi.RemoveOptionalTerrainEnabled ||
                File.Exists(legacyPath))
            {
                throw new InvalidOperationException(
                    "Remove optional terrain did not preserve both Grass choices, publish the terrain-absent projection, or avoid TerrainEditStore.");
            }

            await SetId65FreshOptionalTerrainAsync(
                includeStructuralTerrain: true);
            Id65AuthoringEditorDocumentReadModel terrainRestored =
                await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                    paths,
                    manifest) ?? throw new InvalidOperationException(
                    "Recreating optional terrain lost the canonical document.");
            Id65AuthoringEditorControlState terrainRestoredState =
                Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                    terrainRestored);
            Id65BlankLabUiSnapshot terrainRestoredUi =
                CaptureId65BlankLabUiSnapshotForTesting();
            if (!terrainRestoredState.IncludeStructuralTerrain ||
                !terrainRestoredState.IncludeGrassAtT88 ||
                !terrainRestoredState.IncludeGrassAtT107 ||
                _currentGeometry?.Polygons.Count != 2 ||
                _currentGeometry.LowDetailPolygons.Count != 2 ||
                terrainRestoredUi.CreateOptionalTerrainEnabled ||
                !terrainRestoredUi.RemoveOptionalTerrainEnabled ||
                File.Exists(legacyPath))
            {
                throw new InvalidOperationException(
                    "Create optional terrain did not complete the true-false-true projection sequence while preserving both Grass choices and avoiding TerrainEditStore.");
            }

            Id65AuthoringEditorControlState selectorOnlyResidentState = new(
                terrainRestoredState.IncludeStructuralTerrain,
                terrainRestoredState.IncludeGrassAtT88,
                terrainRestoredState.IncludeGrassAtT107,
                Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId);
            byte[] beforeSelectorOnlyResidentSave =
                await File.ReadAllBytesAsync(documentPath);
            GeometryCandidate beforeSelectorOnlyResidentViewport =
                _currentGeometry ?? throw new InvalidOperationException(
                    "The selector-only T66-to-T25 save lost its initial viewport.");
            Id65AuthoringDocumentDialogOverrideForTesting =
                (initial, exists, _) =>
                {
                    if (!exists || !Equivalent(initial, terrainRestoredState))
                    {
                        throw new InvalidOperationException(
                            "The selector-only T66-to-T25 save did not open the exact T66 state.");
                    }
                    return Task.FromResult<Id65AuthoringEditorControlState?>(
                        selectorOnlyResidentState);
                };
            await EditId65AuthoringDocumentAsync();
            Id65AuthoringEditorDocumentReadModel selectorOnlyResidentDocument =
                await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                    paths,
                    manifest) ?? throw new InvalidOperationException(
                    "The selector-only T66-to-T25 save lost the canonical document.");
            if ((await File.ReadAllBytesAsync(documentPath))
                    .SequenceEqual(beforeSelectorOnlyResidentSave) ||
                !Equivalent(
                    Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                        selectorOnlyResidentDocument),
                    selectorOnlyResidentState) ||
                ReferenceEquals(
                    _currentGeometry,
                    beforeSelectorOnlyResidentViewport) ||
                _currentGeometry?.Polygons.Count != 2 ||
                !_id65FreshDocumentProjectionSynchronized ||
                !(_statusText.Text ?? "").Contains(
                    "mandatory base face Resident T25",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A selector-only T66-to-T25 Apply/Save was treated as a no-op or failed exact CAS/reprojection.");
            }
            string residentT25ViewportProof =
                AssertId65MandatoryBaseTextureViewportRouteForTesting(
                    Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId);
            terrainRestored = selectorOnlyResidentDocument;

            Id65AuthoringEditorControlState externalAsymmetricState = new(
                includeStructuralTerrain: false,
                includeGrassAtT88: false,
                includeGrassAtT107: true,
                mandatoryBaseFaceTextureId:
                    Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId);
            Id65AuthoringEditorDocumentWriteResult externalWrite =
                await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
                    paths,
                    manifest,
                    externalAsymmetricState,
                    terrainRestored.EnvelopeCanonicalSha256);
            RequireExactId65AuthoringDocumentWriteResult(
                externalWrite,
                Id65AuthoringEditorControlStateCompiler.Compile(
                    externalAsymmetricState));
            byte[] externalDocumentBytesBeforeNoOp =
                await File.ReadAllBytesAsync(documentPath);
            GeometryCandidate staleViewportBeforeNoOp = _currentGeometry ??
                throw new InvalidOperationException(
                    "The external-document probe lost its previously projected viewport.");
            int staleCandidateMaterializerCalls = 0;
            Id65AuthoringCandidateMaterializerOverrideForTesting =
                (_, _, _) =>
                {
                    staleCandidateMaterializerCalls++;
                    throw new InvalidOperationException(
                        "The stale viewport must refuse before candidate materialization.");
                };
            await CreateId65AuthoringAggregateCandidateAsync();
            Id65AuthoringCandidateMaterializerOverrideForTesting = null;
            if (staleCandidateMaterializerCalls != 0 ||
                !ReferenceEquals(_currentGeometry, staleViewportBeforeNoOp) ||
                !(_statusText.Text ?? "").Contains(
                    "not synchronized",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Candidate creation did not refuse the externally changed canonical document before materialization.");
            }

            Id65AuthoringDocumentDialogOverrideForTesting =
                (initial, exists, _) =>
                {
                    if (!exists ||
                        !Equivalent(initial, externalAsymmetricState))
                    {
                        throw new InvalidOperationException(
                            "The unchanged external-document probe did not inspect its asymmetric disk winner.");
                    }
                    return Task.FromResult<Id65AuthoringEditorControlState?>(
                        new(
                            initial.IncludeStructuralTerrain,
                            initial.IncludeGrassAtT88,
                            initial.IncludeGrassAtT107,
                            initial.MandatoryBaseFaceTextureId));
                };
            int staleNoWriteApplyFaultCalls = 0;
            Id65FreshProjectionApplyFaultForTesting = stage =>
            {
                if (string.Equals(
                        stage,
                        "after-viewport-geometry-before-dependent-refresh",
                        StringComparison.Ordinal) &&
                    staleNoWriteApplyFaultCalls++ == 0)
                {
                    throw new InvalidOperationException(
                        "Injected unchanged stale-modal partial apply failure.");
                }
            };
            try
            {
                await EditId65AuthoringDocumentAsync();
            }
            finally
            {
                Id65FreshProjectionApplyFaultForTesting = null;
            }
            Id65AuthoringEditorDocumentReadModel refreshedExternalDocument =
                await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                    paths,
                    manifest) ?? throw new InvalidOperationException(
                    "The unchanged external-document repair lost its canonical document.");
            Id65AuthoringEditorControlState refreshedExternalState =
                Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                    refreshedExternalDocument);
            if (!(await File.ReadAllBytesAsync(documentPath))
                    .SequenceEqual(externalDocumentBytesBeforeNoOp) ||
                !Equivalent(refreshedExternalState, externalAsymmetricState) ||
                !Id65AuthoringDocumentSnapshotsEqual(
                    _id65FreshAuthoringDocument,
                    refreshedExternalDocument) ||
                staleNoWriteApplyFaultCalls < 1 ||
                !_id65FreshDocumentProjectionSynchronized ||
                ReferenceEquals(_currentGeometry, staleViewportBeforeNoOp) ||
                _currentGeometry?.Polygons.Count != 1 ||
                _currentGeometry.LowDetailPolygons.Count != 1 ||
                !(_statusText.Text ?? "").Contains(
                    "No canonical document file was changed",
                    StringComparison.OrdinalIgnoreCase) ||
                !(_statusText.Text ?? "").Contains(
                    "first stale-viewport apply failed",
                    StringComparison.OrdinalIgnoreCase) ||
                !(_statusText.Text ?? "").Contains(
                    "recovered",
                    StringComparison.OrdinalIgnoreCase) ||
                File.Exists(legacyPath))
            {
                throw new InvalidOperationException(
                    "An injected partial Apply failure during an unchanged stale-modal selection did not recover the exact disk projection with truthful no-write status.");
            }

            if (AssertId65MandatoryBaseTextureViewportRouteForTesting(
                    Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId) !=
                residentT25ViewportProof)
            {
                throw new InvalidOperationException(
                    "The no-write stale-document recovery changed the exact Resident T25 viewport route identity.");
            }
            RefreshId65BlankLabUi();
            Id65BlankLabUiSnapshot residentT25Ui =
                CaptureId65BlankLabUiSnapshotForTesting();
            if (residentT25Ui.LoadedMandatoryBaseFaceTextureId !=
                    Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId ||
                residentT25Ui.PrivateT66ViewportProjectionBound ||
                residentT25Ui.AuthoringCandidateEnabled ||
                !residentT25Ui.AuthoringCandidateButtonText.Contains(
                    "Resident T25",
                    StringComparison.Ordinal) ||
                !residentT25Ui.AuthoredLayerText.Contains(
                    "offline-authoring-only",
                    StringComparison.OrdinalIgnoreCase) ||
                !TryGetId65BlankLabNormalCreateBinBlockReason(
                    out string residentT25CreateBinReason) ||
                !residentT25CreateBinReason.Contains(
                    "Resident T25",
                    StringComparison.Ordinal) ||
                !residentT25CreateBinReason.Contains(
                    "offline-authoring/viewport-only",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The synchronized Resident T25 selector did not close candidate/Create-BIN authority or bind the truthful resident viewport state.");
            }

            _id65BlankLabRevealPath = candidateCue;
            _id65BlankLabRevealDocumentEnvelopeSha256 =
                created.EnvelopeCanonicalSha256;
            _id65BlankLabRevealDocumentCanonicalSha256 =
                created.DocumentCanonicalSha256;
            await RevealId65BlankLabOutputAsync();
            if (!string.IsNullOrWhiteSpace(_id65BlankLabRevealPath) ||
                !(_statusText.Text ?? "").Contains(
                    "Resident T25",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A stale T66 reveal binding remained usable under Resident T25.");
            }

            Id65BlankLabAuthoredLayerSnapshot residentCandidateOutputsBefore =
                await CaptureId65BlankLabAuthoredLayerAsync(
                    Path.Combine(paths.RootPath, "materialized-candidates"));
            int residentCandidateMaterializerCalls = 0;
            Id65AuthoringCandidateMaterializerOverrideForTesting =
                (_, _, _) =>
                {
                    residentCandidateMaterializerCalls++;
                    throw new InvalidOperationException(
                        "Resident T25 must refuse before the candidate materializer.");
                };
            await CreateId65AuthoringAggregateCandidateAsync();
            Id65AuthoringCandidateMaterializerOverrideForTesting = null;
            Id65BlankLabAuthoredLayerSnapshot residentCandidateOutputsAfter =
                await CaptureId65BlankLabAuthoredLayerAsync(
                    Path.Combine(paths.RootPath, "materialized-candidates"));
            if (residentCandidateMaterializerCalls != 0 ||
                !residentCandidateOutputsBefore.Files.SequenceEqual(
                    residentCandidateOutputsAfter.Files,
                    StringComparer.Ordinal) ||
                residentCandidateOutputsBefore.DirectoryExisted !=
                    residentCandidateOutputsAfter.DirectoryExisted ||
                !string.IsNullOrWhiteSpace(_id65BlankLabRevealPath) ||
                !(_statusText.Text ?? "").Contains(
                    "offline-authoring/viewport-only",
                    StringComparison.OrdinalIgnoreCase) ||
                _id65BlankLabBusy || _id65BlankLabManualOperation != null)
            {
                throw new InvalidOperationException(
                    "Resident T25 crossed the direct pre-materializer refusal or changed candidate/reveal/output state.");
            }

            await SetId65FreshOptionalTerrainAsync(
                includeStructuralTerrain: true);
            Id65AuthoringEditorControlState residentTerrainPresent =
                Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                    await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                        paths,
                        manifest) ?? throw new InvalidOperationException(
                        "Resident T25 terrain creation lost the canonical document."));
            if (!residentTerrainPresent.IncludeStructuralTerrain ||
                residentTerrainPresent.MandatoryBaseFaceTextureId !=
                    Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId ||
                !(_statusText.Text ?? "").Contains(
                    "Resident T25 was preserved",
                    StringComparison.Ordinal) ||
                AssertId65MandatoryBaseTextureViewportRouteForTesting(
                    Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId) !=
                    residentT25ViewportProof)
            {
                throw new InvalidOperationException(
                    "Creating optional terrain did not preserve the Resident T25 selector and viewport caches.");
            }
            await SetId65FreshOptionalTerrainAsync(
                includeStructuralTerrain: false);
            Id65AuthoringEditorControlState residentTerrainAbsent =
                Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                    await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                        paths,
                        manifest) ?? throw new InvalidOperationException(
                        "Resident T25 terrain removal lost the canonical document."));
            if (residentTerrainAbsent.IncludeStructuralTerrain ||
                residentTerrainAbsent.MandatoryBaseFaceTextureId !=
                    Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId ||
                AssertId65MandatoryBaseTextureViewportRouteForTesting(
                    Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId) !=
                    residentT25ViewportProof ||
                File.Exists(legacyPath))
            {
                throw new InvalidOperationException(
                    "Removing optional terrain did not preserve the Resident T25 selector/caches or avoid TerrainEditStore.");
            }

            Id65AuthoringEditorControlState selectorOnlyPrivateState = new(
                residentTerrainAbsent.IncludeStructuralTerrain,
                residentTerrainAbsent.IncludeGrassAtT88,
                residentTerrainAbsent.IncludeGrassAtT107,
                Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId);
            byte[] beforeSelectorOnlyPrivateSave =
                await File.ReadAllBytesAsync(documentPath);
            Id65AuthoringDocumentDialogOverrideForTesting =
                (initial, exists, _) =>
                {
                    if (!exists || !Equivalent(initial, residentTerrainAbsent))
                    {
                        throw new InvalidOperationException(
                            "The selector-only T25-to-T66 save did not open the exact T25 state.");
                    }
                    return Task.FromResult<Id65AuthoringEditorControlState?>(
                        selectorOnlyPrivateState);
                };
            await EditId65AuthoringDocumentAsync();
            Id65AuthoringEditorDocumentReadModel selectorOnlyPrivateDocument =
                await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                    paths,
                    manifest) ?? throw new InvalidOperationException(
                    "The selector-only T25-to-T66 save lost the canonical document.");
            RefreshId65BlankLabUi();
            Id65BlankLabUiSnapshot incompletePrivateUi =
                CaptureId65BlankLabUiSnapshotForTesting();
            if ((await File.ReadAllBytesAsync(documentPath))
                    .SequenceEqual(beforeSelectorOnlyPrivateSave) ||
                !Equivalent(
                    Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                        selectorOnlyPrivateDocument),
                    selectorOnlyPrivateState) ||
                incompletePrivateUi.LoadedMandatoryBaseFaceTextureId !=
                    Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId ||
                !incompletePrivateUi.PrivateT66ViewportProjectionBound ||
                incompletePrivateUi.AuthoringCandidateEnabled ||
                !incompletePrivateUi.AuthoringCandidateButtonText.Contains(
                    "Requires Full T66",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A selector-only T25-to-T66 Apply/Save lost exact CAS/reprojection or exposed an incomplete T66 candidate.");
            }
            string privateT66ViewportProof =
                AssertId65MandatoryBaseTextureViewportRouteForTesting(
                    Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId);

            Id65AuthoringDocumentDialogOverrideForTesting =
                (_, exists, _) =>
                {
                    if (!exists)
                    {
                        throw new InvalidOperationException(
                            "The default-state restore lost the external document.");
                    }
                    return Task.FromResult<Id65AuthoringEditorControlState?>(
                        Id65AuthoringEditorControlState.CreateDefault());
                };
            await EditId65AuthoringDocumentAsync();
            terrainRestored =
                await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                    paths,
                    manifest) ?? throw new InvalidOperationException(
                    "The external-document probe could not restore the full default state for candidate testing.");
            if (!Equivalent(
                    Id65AuthoringEditorControlStateCompiler.RecoverControlState(
                        terrainRestored),
                    Id65AuthoringEditorControlState.CreateDefault()) ||
                _currentGeometry?.Polygons.Count != 2 ||
                !_id65FreshDocumentProjectionSynchronized)
            {
                throw new InvalidOperationException(
                    "The external-document probe did not restore the full synchronized candidate fixture.");
            }
            if (AssertId65MandatoryBaseTextureViewportRouteForTesting(
                    Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId) !=
                privateT66ViewportProof)
            {
                throw new InvalidOperationException(
                    "The full T66 restore changed the exact private viewport route identity.");
            }

            int materializerCalls = 0;
            Id65BlankLabAuthoringCandidateAppResult? latestCandidateResult = null;
            Func<
                UnusedLevel65BlankLevelLabWorkspacePaths,
                UnusedLevel65BlankLevelLabManifest,
                CancellationToken,
                Task<Id65BlankLabAuthoringCandidateAppResult>>
                successfulCandidateMaterializer =
                (actualPaths, actualManifest, _) =>
                {
                    materializerCalls++;
                    if (actualPaths != paths || actualManifest != manifest)
                    {
                        throw new InvalidOperationException(
                            "The explicit candidate action lost its admitted path or manifest identity.");
                    }
                    latestCandidateResult = new Id65BlankLabAuthoringCandidateAppResult(
                        candidateDirectory,
                        candidateImage,
                        candidateCue,
                        candidateReceipt,
                        created.EnvelopeCanonicalSha256,
                        created.DocumentCanonicalSha256,
                        UsesRemotePadT107Grid: false,
                        T107RemotePadPositionId: null,
                        StructuralTerrainPresent: true,
                        GrassT107Present: true,
                        new string('a', 64),
                        new string('b', 64),
                        new string('c', 64),
                        new string('d', 64),
                        ChangedRawSectorCount: 558,
                        ChangedPhysicalImageBytes: 1_075_276,
                        ReusedExistingCandidate: materializerCalls > 1,
                        CandidateArtifactsWrittenThisCall: materializerCalls == 1,
                        FileSystemMutationPerformed: materializerCalls == 1,
                        ExactReadbackVerified: true,
                        Schema3CompilerPinsVerified: false,
                        T107PositionReadbackVerified: false,
                        T88ExcludedVerified: false,
                        RepairedT13995SupportVerified: false,
                        ProtectedComplementVerified: true,
                        RetailTownSquarePreserved: true,
                        TargetOverlayPreserved: true,
                        ExecutablePreserved: true,
                        AtomicNoReplacePublicationVerified: true,
                        OwnedTemporaryResidueRemoved: true,
                        CarriesAppOrUiAuthority: false,
                        CarriesCreateBinAuthority: false,
                        NormalRetailCreateBinComposed: false,
                        RuntimeCandidateAuthorized: false,
                        RuntimeProven: false,
                        ReleaseIntegrated: false,
                        PromotionAuthorized: false,
                        Publishable: false,
                        CurrentOnDiskFreshnessVerified: false,
                        HostileFilesystemRaceSafe: false,
                        RequiresTrustedExclusiveWorkspaceDirectory: true,
                        CommonWorkspaceLeaseOwned: false);
                    return Task.FromResult(latestCandidateResult);
                };
            Id65AuthoringCandidateMaterializerOverrideForTesting =
                successfulCandidateMaterializer;
            await CreateId65AuthoringAggregateCandidateAsync();
            string createdCandidateStatus = _statusText.Text ?? "";
            await CreateId65AuthoringAggregateCandidateAsync();
            string reusedCandidateStatus = _statusText.Text ?? "";
            if (materializerCalls != 2 ||
                !createdCandidateStatus.Contains(
                    "Created the exact offline ID65 full-authoring candidate",
                    StringComparison.Ordinal) ||
                !createdCandidateStatus.Contains(
                    "not normal Create BIN",
                    StringComparison.OrdinalIgnoreCase) ||
                !reusedCandidateStatus.Contains(
                    "Reused the exact offline ID65 full-authoring candidate",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    _id65BlankLabRevealPath,
                    candidateCue,
                    StringComparison.Ordinal) ||
                _id65BlankLabBusy || _id65BlankLabManualOperation != null)
            {
                throw new InvalidOperationException(
                    "The explicit ID65 candidate action lost exact create/reuse, offline truth, reveal, or operation-state behavior.");
            }

            Id65BlankLabAuthoringCandidateAppResult validCandidateResult =
                latestCandidateResult ?? throw new InvalidOperationException(
                    "The exact candidate seam returned no reusable test receipt.");
            Id65AuthoringCandidateMaterializerOverrideForTesting =
                (_, _, _) => Task.FromResult(validCandidateResult with
                {
                    ReusedExistingCandidate = true,
                    CandidateArtifactsWrittenThisCall = true,
                    FileSystemMutationPerformed = true
                });
            await CreateId65AuthoringAggregateCandidateAsync();
            if (materializerCalls != 2 ||
                !(_statusText.Text ?? "").Contains(
                    "lost an exact path, identity, readback, or offline safety boundary",
                    StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(_id65BlankLabRevealPath))
            {
                throw new InvalidOperationException(
                    "A contradictory exact-candidate create/reuse receipt was accepted or retained a stale reveal path.");
            }
            Id65AuthoringCandidateMaterializerOverrideForTesting =
                successfulCandidateMaterializer;

            byte[] exactDocumentBeforePostReturnMutation =
                await File.ReadAllBytesAsync(documentPath);
            Id65AuthoringCandidateMaterializerOverrideForTesting =
                async (_, _, _) =>
                {
                    await File.WriteAllTextAsync(documentPath, "{}\n");
                    return validCandidateResult;
                };
            await CreateId65AuthoringAggregateCandidateAsync();
            string postReturnMutationStatus = _statusText.Text ?? "";
            string postReturnMutationReveal = _id65BlankLabRevealPath;
            bool postReturnMutationOperationSettled =
                !_id65BlankLabBusy && _id65BlankLabManualOperation == null;
            await File.WriteAllBytesAsync(
                documentPath,
                exactDocumentBeforePostReturnMutation);
            Id65AuthoringCandidateMaterializerOverrideForTesting =
                successfulCandidateMaterializer;
            if (materializerCalls != 2 ||
                !postReturnMutationStatus.Contains(
                    "Could not create the exact ID65 full-authoring candidate",
                    StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(postReturnMutationReveal) ||
                !postReturnMutationOperationSettled)
            {
                throw new InvalidOperationException(
                    "A canonical document mutation at candidate return crossed the post-materializer reinspection boundary.");
            }

            TaskCompletionSource candidateDelayStarted =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource candidateDelayRelease =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            Id65BlankLabManualOperationDelayOverrideForTesting =
                (stage, cancellationToken) =>
                {
                    if (!string.Equals(
                            stage,
                            "authoring-candidate-started",
                            StringComparison.Ordinal))
                    {
                        return Task.CompletedTask;
                    }
                    candidateDelayStarted.TrySetResult();
                    return candidateDelayRelease.Task.WaitAsync(cancellationToken);
                };
            Task staleCandidate = CreateId65AuthoringAggregateCandidateAsync();
            await candidateDelayStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            LevelDefinition? delayedCandidateLevel = _currentLevel;
            string delayedCandidateTerrainSignature = _currentGeometry == null
                ? ""
                : BuildTerrainEditSignature(_currentGeometry);
            string delayedCandidateMobySignature =
                BuildMobyEditSignature(_currentMobys);
            int materializerCallsBeforeFencedDuplicate = materializerCalls;
            bool fencedLevelSelection = SelectLevel(_retailCatalog.Levels.First());
            await CreateId65AuthoringAggregateCandidateAsync();
            if (_id65BlankLabAuthoringCandidateButton?.IsEnabled != false ||
                fencedLevelSelection || _currentLevel != delayedCandidateLevel ||
                (_currentGeometry == null
                    ? ""
                    : BuildTerrainEditSignature(_currentGeometry)) !=
                    delayedCandidateTerrainSignature ||
                BuildMobyEditSignature(_currentMobys) !=
                    delayedCandidateMobySignature ||
                materializerCalls != materializerCallsBeforeFencedDuplicate)
            {
                throw new InvalidOperationException(
                    "The delayed exact-candidate operation did not fence duplicate creation, level navigation, terrain, or Moby state.");
            }
            CancelId65BlankLabManualOperationForWorkspaceChange();
            const string candidateGenerationStatus =
                "new candidate generation owns this status";
            _statusText.Text = candidateGenerationStatus;
            candidateDelayRelease.TrySetResult();
            await staleCandidate;
            Id65BlankLabManualOperationDelayOverrideForTesting = null;
            if (materializerCalls != 2 ||
                !string.Equals(
                    _statusText.Text,
                    candidateGenerationStatus,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    _id65BlankLabAuthoringCandidateButton?.Content?.ToString(),
                    "Create Exact Full Candidate",
                    StringComparison.Ordinal) ||
                !string.IsNullOrWhiteSpace(_id65BlankLabRevealPath) ||
                _id65BlankLabBusy || _id65BlankLabManualOperation != null)
            {
                throw new InvalidOperationException(
                    "A stale exact-candidate continuation changed the newer status, retained a stale reveal binding, changed the button label, or leaked operation state.");
            }

            byte[] createdBytes = await File.ReadAllBytesAsync(documentPath);
            Id65AuthoringDocumentDialogOverrideForTesting =
                (initial, exists, _) =>
                {
                    if (!exists || !Equivalent(initial, createdState))
                    {
                        throw new InvalidOperationException(
                            "The stale no-op probe did not reopen the exact created state.");
                    }
                    File.Delete(documentPath);
                    return Task.FromResult<Id65AuthoringEditorControlState?>(new(
                        initial.IncludeStructuralTerrain,
                        initial.IncludeGrassAtT88,
                        initial.IncludeGrassAtT107,
                        initial.MandatoryBaseFaceTextureId));
                };
            bool staleNoOpLeftDocumentAbsent;
            string staleNoOpStatus;
            try
            {
                await EditId65AuthoringDocumentAsync();
                staleNoOpLeftDocumentAbsent = !File.Exists(documentPath);
                staleNoOpStatus = _statusText.Text ?? "";
            }
            finally
            {
                await File.WriteAllBytesAsync(documentPath, createdBytes);
            }
            Id65AuthoringEditorDocumentReadModel restoredAfterStaleNoOp =
                await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                    paths,
                    manifest)
                ?? throw new InvalidOperationException(
                    "The stale no-op probe could not restore its exact fixture.");
            if (!staleNoOpLeftDocumentAbsent ||
                !staleNoOpStatus.Contains(
                    "changed while the dialog was open",
                    StringComparison.OrdinalIgnoreCase) ||
                restoredAfterStaleNoOp.EnvelopeCanonicalSha256 !=
                    created.EnvelopeCanonicalSha256)
            {
                throw new InvalidOperationException(
                    "An unchanged selection trusted a deleted or stale pre-dialog envelope.");
            }

            Id65AuthoringDocumentDialogOverrideForTesting =
                (initial, exists, _) =>
                {
                    if (!exists || !Equivalent(initial, createdState))
                    {
                        throw new InvalidOperationException(
                            "The existing canonical document did not reopen as its exact control state.");
                    }
                    return Task.FromResult<Id65AuthoringEditorControlState?>(new(
                        initial.IncludeStructuralTerrain,
                        initial.IncludeGrassAtT88,
                        initial.IncludeGrassAtT107,
                        initial.MandatoryBaseFaceTextureId));
                };
            await EditId65AuthoringDocumentAsync();
            if (!(await File.ReadAllBytesAsync(documentPath)).SequenceEqual(createdBytes) ||
                !(_statusText.Text ?? "").Contains(
                    "already matches",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "An unchanged canonical dialog selection rewrote its envelope.");
            }

            Id65AuthoringDocumentDialogOverrideForTesting =
                (_, exists, _) =>
                {
                    if (!exists)
                    {
                        throw new InvalidOperationException(
                            "The invalid-state probe lost its inspected existing document.");
                    }
                    return Task.FromResult<Id65AuthoringEditorControlState?>(new(
                        includeStructuralTerrain: true,
                        includeGrassAtT88: false,
                        includeGrassAtT107: false,
                        mandatoryBaseFaceTextureId:
                            Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId));
                };
            await EditId65AuthoringDocumentAsync();
            if (!(await File.ReadAllBytesAsync(documentPath)).SequenceEqual(createdBytes) ||
                !(_statusText.Text ?? "").Contains(
                    "Could not save",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "An invalid no-placement App state changed the canonical envelope.");
            }

            Id65AuthoringEditorControlState replacement = new(
                includeStructuralTerrain: false,
                includeGrassAtT88: true,
                includeGrassAtT107: false,
                mandatoryBaseFaceTextureId:
                    Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId);
            Id65AuthoringDocumentDialogOverrideForTesting =
                (_, exists, _) =>
                {
                    if (!exists)
                    {
                        throw new InvalidOperationException(
                            "The replacement probe lost its inspected existing document.");
                    }
                    return Task.FromResult<Id65AuthoringEditorControlState?>(replacement);
                };
            await EditId65AuthoringDocumentAsync();
            Id65AuthoringEditorDocumentReadModel replaced =
                await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                    paths,
                    manifest)
                ?? throw new InvalidOperationException(
                    "The explicit App replacement action removed its canonical document.");
            Id65AuthoringEditorControlState replacedState =
                Id65AuthoringEditorControlStateCompiler.RecoverControlState(replaced);
            Id65AuthoringEditorControlStateCompilation expectedReplacement =
                Id65AuthoringEditorControlStateCompiler.Compile(replacement);
            RefreshId65BlankLabUi();
            RefreshActionAvailability();
            Id65BlankLabUiSnapshot ui = CaptureId65BlankLabUiSnapshotForTesting();
            string replacementStatus = _statusText.Text ?? "";
            int callsBeforeUnsupportedVariant = materializerCalls;
            await CreateId65AuthoringAggregateCandidateAsync();
            string unsupportedVariantStatus = _statusText.Text ?? "";
            if (!Equivalent(replacedState, replacement) ||
                replaced.DocumentCanonicalSha256 !=
                    expectedReplacement.DocumentCanonicalSha256 ||
                replaced.EnvelopeCanonicalSha256 == created.EnvelopeCanonicalSha256 ||
                !replacementStatus.Contains(
                    "Updated the canonical ID65 Full Authoring Document",
                    StringComparison.Ordinal) ||
                !ui.AuthoringDocumentEnabled || ui.AuthoringCandidateEnabled ||
                !ui.AuthoringCandidateButtonText.Contains(
                    "Requires Full T66",
                    StringComparison.Ordinal) ||
                ui.SaveEnabled || ui.CreateCueEnabled ||
                callsBeforeUnsupportedVariant != materializerCalls ||
                !unsupportedVariantStatus.Contains(
                    "supports only the full document",
                    StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(_id65BlankLabRevealPath) ||
                !TryGetId65BlankLabNormalCreateBinBlockReason(
                    out string createBinReason) ||
                !createBinReason.Contains(
                    "aggregate materializer",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The explicit App replacement action lost exact CAS state or reopened a legacy/Create-BIN gate.");
            }

            LevelLoadData persistedReload = await Task.Run(
                () => LoadLevelData(levelBefore!.Key));
            if (persistedReload.Id65AuthoringDocument == null ||
                !Id65AuthoringDocumentSnapshotsEqual(
                    persistedReload.Id65AuthoringDocument,
                    replaced) ||
                persistedReload.Id65FreshSceneProjection == null ||
                !persistedReload.Id65FreshSceneProjection.CanonicalDocumentBound ||
                persistedReload.Id65FreshSceneProjection.StructuralTerrainPresent ||
                persistedReload.Geometry?.Polygons.Count != 1 ||
                persistedReload.Geometry.LowDetailPolygons.Count != 1 ||
                persistedReload.LoadedTerrainEdits != 0 ||
                persistedReload.Mobys.Count != 0 ||
                File.Exists(legacyPath))
            {
                throw new InvalidOperationException(
                    "The normal exact-ID65 load path did not inspect and project the persisted terrain-absent document without TerrainEditStore.");
            }

            FileInfo lockedImageAfter = new(paths.LockedBaseImagePath);
            bool protectedOutputsExact = true;
            foreach ((string directory, Id65BlankLabAuthoredLayerSnapshot before) in
                     protectedBefore)
            {
                Id65BlankLabAuthoredLayerSnapshot after =
                    await CaptureId65BlankLabAuthoredLayerAsync(directory);
                protectedOutputsExact &=
                    before.DirectoryExisted == after.DirectoryExisted &&
                    before.Files.SequenceEqual(after.Files, StringComparer.Ordinal);
            }
            if (lockedImageAfter.Length != lockedImageLengthBefore ||
                lockedImageAfter.LastWriteTimeUtc != lockedImageWriteBefore ||
                !(await File.ReadAllBytesAsync(paths.LockedBaseCuePath))
                    .SequenceEqual(cueBefore) ||
                !(await File.ReadAllBytesAsync(paths.ManifestPath))
                    .SequenceEqual(manifestBefore) ||
                !protectedOutputsExact ||
                !ReferenceEquals(_currentLevel, levelBefore) ||
                ReferenceEquals(_currentGeometry, geometryBefore) ||
                _currentGeometry?.Polygons.Count != 1 ||
                _currentGeometry.LowDetailPolygons.Count != 1 ||
                !Id65FreshLevelSceneProjector.IsExactProjectionGeometry(
                    _currentGeometry) ||
                !ReferenceEquals(_viewport.Geometry, _currentGeometry) ||
                _currentMobys.Count != 0 ||
                !Id65AuthoringDocumentSnapshotsEqual(
                    _id65FreshAuthoringDocument,
                    replaced) ||
                _loadedTerrainEdits != 0 ||
                (_currentGeometry != null && !string.Equals(
                    BuildTerrainEditSignature(_currentGeometry),
                    terrainSignatureBefore,
                    StringComparison.Ordinal)) ||
                !string.Equals(
                    BuildMobyEditSignature(_currentMobys),
                    mobySignatureBefore,
                    StringComparison.Ordinal) ||
                _id65BlankLabBusy || _id65BlankLabManualOperation != null)
            {
                throw new InvalidOperationException(
                    "The explicit App authoring action changed locked/output state, lost its exact document-bound viewport, loaded TerrainEditStore rows, or retained an operation fence.");
            }

            return
                $"explicit cancel preserved absence; create immediately reprojected; injected CAS failure preserved document bytes and viewport identity; partial Apply full-sync recovery replaced the hybrid viewport from the valid saved document with truthful status and no TerrainEditStore/retail writes; true-false-true optional-terrain toggle preserved both Grass choices and wrote no TerrainEditStore; external stale-document viewport refresh injected a no-write partial Apply failure, then re-inspected and fully recovered the unchanged exact disk projection with truthful status while refusing candidate materialization when stale; mandatory two-choice selector transition proved {residentT25ViewportProof}, selector-preserving Resident T25 terrain toggles, direct T25 pre-materializer/no-output/no-reveal/Create-BIN refusal, then {privateT66ViewportProof}; exact persisted reload, modal replacement reinspection/reprojection, exact offline candidate create/reuse, post-materializer document race refusal, stale-envelope no-op refusal, exact reopen/no-op, invalid-state refusal, and CAS replacement passed; unsupported T66 document variants failed before materialization; legacy Save/disposable CUE and normal Create BIN stayed closed; locked BIN/CUE/manifest and protected outputs stayed unchanged";
        }
        finally
        {
            Id65AuthoringDocumentDialogOverrideForTesting = null;
            Id65AuthoringCandidateMaterializerOverrideForTesting = null;
            Id65BlankLabManualOperationDelayOverrideForTesting = null;
            Id65AuthoringTerrainExpectedEnvelopeOverrideForTesting = null;
            Id65FreshProjectionApplyFaultForTesting = null;
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
        bool AuthoringDocumentEnabled,
        string AuthoringDocumentButtonText,
        bool AuthoringCandidateEnabled,
        string AuthoringCandidateButtonText,
        bool ObjectManagerDocumentEnabled,
        string ObjectManagerStatusText,
        bool ObjectManagerGrassPositionSelectorEnabled,
        string ObjectManagerGrassSelectedPositionId,
        bool ObjectManagerAddOrMoveGrassEnabled,
        bool ObjectManagerRemoveGrassEnabled,
        bool ObjectManagerMobyChoicePanelVisible,
        bool ObjectManagerMobyChoiceSelectorEnabled,
        Id65RemotePadMobyKind? ObjectManagerSelectedMobyKind,
        bool ObjectManagerMobyPositionSelectorEnabled,
        string ObjectManagerSelectedMobyPositionId,
        bool ObjectManagerApplyMobyChoiceEnabled,
        bool SaveEnabled,
        bool CreateCueEnabled,
        bool RevealEnabled,
        string RevealButtonText,
        bool OptionalTerrainVisible,
        bool CreateOptionalTerrainEnabled,
        bool RemoveOptionalTerrainEnabled,
        bool LegacyOptionalTerrainControlsVisible,
        bool TerrainSetControlsVisible,
        bool AddTerrainTile1Enabled,
        bool RemoveTerrainTile1Enabled,
        bool AddTerrainTile2Enabled,
        bool RemoveTerrainTile2Enabled,
        string OptionalTerrainStatusText,
        bool DocumentProjectionSynchronized,
        int LoadedMandatoryBaseFaceTextureId,
        bool LoadedUsesRemotePadT107Grid,
        string LoadedT107RemotePadPositionId,
        bool LoadedUsesRemotePadMobyChoice,
        bool LoadedUsesRemotePadTerrainSet,
        bool LoadedTerrainTile1,
        bool LoadedTerrainTile2,
        Id65RemotePadMobyKind? LoadedRemotePadMobyKind,
        string LoadedRemotePadMobyPositionId,
        bool LoadedUsesBlankCyclorama,
        string LoadedBlankCycloramaPresetId,
        string LoadedBlankCycloramaColorHex,
        int VisibleMobyCount,
        bool PrivateT66ViewportProjectionBound,
        bool RepairsInvalidWorkspace,
        string DisposableExporterRoute);

    private sealed record EditorMutationSceneIdentity(
        EditorWorkspace Workspace,
        string WorkspaceRoot,
        LevelDefinition Level,
        List<Moby> Mobys,
        int LevelLoadRequestId,
        int Id65OperationGeneration,
        int WorkspaceTransitionGeneration,
        int FreshResearchGeneration);

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
        int WorkspaceTransitionGeneration,
        int FreshResearchGeneration);

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

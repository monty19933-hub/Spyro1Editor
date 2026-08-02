using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private readonly ComboBox _nativeLevelReplacementDonorBox = new()
    {
        Name = "NativeLevelReplacementDonorBox",
        MinHeight = 34
    };
    private readonly TextBlock _nativeLevelReplacementStatusText = new()
    {
        Name = "NativeLevelReplacementStatus",
        TextWrapping = TextWrapping.Wrap,
        FontSize = 11,
        LineHeight = 16
    };
    private Expander? _nativeLevelReplacementDisclosure;
    private Button? _nativeLevelReplacementSaveIntentButton;
    private Button? _nativeLevelReplacementInspectButton;
    private Button? _nativeLevelReplacementCreateTestButton;
    private Button? _nativeLevelReplacementCreateEditedTestButton;
    private Button? _nativeLevelReplacementRestoreButton;
    private bool _nativeLevelReplacementBusy;
    private string _nativeLevelReplacementNotice = "";
    private bool _nativeLevelReplacementNoticeIsError;
    private string _nativeLevelReplacementNoticeLevelKey = "";

    private Control BuildNativeLevelReplacementDisclosure()
    {
        NativeLevelReplacementProfile profile = RequireTownSquareReplacementProfile();
        _nativeLevelReplacementDonorBox.ItemsSource =
        new NativeLevelReplacementDonorOption[]
        {
            new NativeLevelReplacementDonorOption(
                profile.Id,
                "Town Square — complete retail level")
        };
        _nativeLevelReplacementDonorBox.SelectedIndex = 0;
        _nativeLevelReplacementDonorBox.IsEnabled = false;
        ToolTip.SetTip(
            _nativeLevelReplacementDonorBox,
            "V5 currently exposes only the exact Town Square into Stone Hill recipe that passed DuckStation.");

        StackPanel content = new() { Spacing = 9 };
        content.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(255, 247, 230)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 174, 83)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(9, 7),
            Child = new TextBlock
            {
                Text = "V5 research only — this is separate from normal Create BIN. The proven replacement test copies Stone Hill's complete retail Town Square payload unchanged. The edited replacement test consumes only saved Town Square native object edits; normal Create BIN remains unchanged.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                LineHeight = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(126, 78, 11))
            }
        });
        content.Children.Add(new TextBlock
        {
            Text = "Replacement level",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ModernMutedInk)
        });
        content.Children.Add(_nativeLevelReplacementDonorBox);
        content.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(246, 248, 250)),
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(9, 7),
            Child = _nativeLevelReplacementStatusText
        });

        _nativeLevelReplacementSaveIntentButton = NewNativeLevelReplacementAsyncButton(
            "NativeLevelReplacementSaveIntentButton",
            "Save Replacement Intent",
            SaveNativeLevelReplacementIntentAsync);
        _nativeLevelReplacementInspectButton = NewNativeLevelReplacementAsyncButton(
            "NativeLevelReplacementInspectButton",
            "Inspect Replacement Safety",
            InspectNativeLevelReplacementSafetyAsync);
        _nativeLevelReplacementCreateTestButton = NewNativeLevelReplacementAsyncButton(
            "NativeLevelReplacementCreateTestButton",
            "Create Replacement Test CUE",
            CreateNativeLevelReplacementTestCueAsync);
        _nativeLevelReplacementCreateEditedTestButton = NewNativeLevelReplacementAsyncButton(
            "NativeLevelReplacementCreateEditedTestButton",
            "Create Edited Replacement Test CUE",
            CreateEditedNativeLevelReplacementTestCueAsync);
        _nativeLevelReplacementRestoreButton = NewNativeLevelReplacementAsyncButton(
            "NativeLevelReplacementRestoreButton",
            "Restore Original Stone Hill",
            RestoreNativeLevelReplacementIntentAsync);
        StyleModernPrimaryButton(_nativeLevelReplacementSaveIntentButton, Color.FromRgb(176, 103, 20));
        StyleModernPrimaryButton(_nativeLevelReplacementCreateTestButton, Color.FromRgb(111, 86, 174));
        StyleModernPrimaryButton(_nativeLevelReplacementCreateEditedTestButton, Color.FromRgb(176, 103, 20));
        StyleModernSecondaryButton(_nativeLevelReplacementInspectButton);
        StyleModernSecondaryButton(_nativeLevelReplacementRestoreButton, ModernRed);

        Grid firstRow = NewEqualButtonGrid(2);
        AddModernGridButton(firstRow, _nativeLevelReplacementSaveIntentButton, 0);
        AddModernGridButton(firstRow, _nativeLevelReplacementInspectButton, 1);
        content.Children.Add(firstRow);
        content.Children.Add(_nativeLevelReplacementCreateTestButton);
        content.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(255, 247, 230)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 174, 83)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(9, 7),
            Child = new TextBlock
            {
                Text = "Edited replacement test — pending DuckStation. This first X-only proof intentionally preserves T21's native placement/culling-sector byte; a later gate will validate the derived sector patch. It accepts exactly one saved Town Square T21 red-gem X move. Other objects, axes, additions, removals, paths, terrain, and textures remain blocked; normal Create BIN remains unchanged.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                LineHeight = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(126, 78, 11))
            }
        });
        content.Children.Add(_nativeLevelReplacementCreateEditedTestButton);
        content.Children.Add(_nativeLevelReplacementRestoreButton);

        _nativeLevelReplacementDisclosure = (Expander)BuildModernDisclosure(
            "Replace Stone Hill (V5 research)",
            content,
            false);
        _nativeLevelReplacementDisclosure.Name = "NativeLevelReplacementDisclosure";
        RefreshNativeLevelReplacementPanel();
        return _nativeLevelReplacementDisclosure;
    }

    private Button NewNativeLevelReplacementAsyncButton(
        string name,
        string text,
        Func<Task> command)
    {
        Button button = NewButton(text);
        button.Name = name;
        button.Click += async (_, _) =>
        {
            if (_nativeLevelReplacementBusy || !button.IsEnabled)
                return;

            _nativeLevelReplacementBusy = true;
            SetNativeLevelReplacementNotice($"{text} is running...", isError: false);
            RefreshNativeLevelReplacementPanel();
            try
            {
                EditorDiagnostics.RecordAction($"Button: {text}");
                await command();
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException or InvalidDataException or
                InvalidOperationException or ArgumentException)
            {
                EditorDiagnostics.RecordException($"running the '{text}' command", ex);
                SetNativeLevelReplacementNotice($"Could not {text.ToLowerInvariant()}: {ex.Message}", isError: true);
                _statusText.Text = _nativeLevelReplacementNotice;
            }
            finally
            {
                _nativeLevelReplacementBusy = false;
                RefreshNativeLevelReplacementPanel();
            }
        };
        return button;
    }

    private void RefreshNativeLevelReplacementPanel()
    {
        if (_nativeLevelReplacementDisclosure == null)
            return;

        string levelKey = LevelCatalog.NormalizeKey(_currentLevel?.Key ?? "");
        bool isStoneHill = string.Equals(
            levelKey,
            NativeLevelReplacementSupportCatalog.FirstTargetLevelKey,
            StringComparison.Ordinal);
        _nativeLevelReplacementDisclosure.IsVisible = isStoneHill;
        if (!isStoneHill)
            return;

        if (!string.Equals(_nativeLevelReplacementNoticeLevelKey, levelKey, StringComparison.Ordinal))
        {
            _nativeLevelReplacementNotice = "";
            _nativeLevelReplacementNoticeIsError = false;
            _nativeLevelReplacementNoticeLevelKey = levelKey;
        }

        NativeLevelReplacementUiState state = ReadNativeLevelReplacementUiState();
        bool hasTownSquareNativeObjectEdits = HasSavedTownSquareNativeObjectEdits();
        bool available = !_nativeLevelReplacementBusy;
        if (_nativeLevelReplacementSaveIntentButton != null)
            _nativeLevelReplacementSaveIntentButton.IsEnabled = available;
        if (_nativeLevelReplacementInspectButton != null)
            _nativeLevelReplacementInspectButton.IsEnabled = available && state.HasIntent;
        if (_nativeLevelReplacementCreateTestButton != null)
            _nativeLevelReplacementCreateTestButton.IsEnabled = available && state.ValidIntent;
        if (_nativeLevelReplacementCreateEditedTestButton != null)
        {
            _nativeLevelReplacementCreateEditedTestButton.IsEnabled =
                available && state.ValidIntent && hasTownSquareNativeObjectEdits;
            ToolTip.SetTip(
                _nativeLevelReplacementCreateEditedTestButton,
                hasTownSquareNativeObjectEdits
                    ? "Build the guarded one-move Town Square donor candidate for DuckStation validation."
                    : "Save a Town Square native object edit first. This gate currently accepts only T21 Red Gem moved on X.");
        }
        if (_nativeLevelReplacementRestoreButton != null)
            _nativeLevelReplacementRestoreButton.IsEnabled = available && state.HasIntent;

        string stateText = state.ValidIntent
            ? "Saved intent: replace Stone Hill with Town Square's complete retail level. The exact clean-USA recipe is runtime-proven. The base replacement test keeps Stone Hill's portal identity and music; the edited test layers the proven Town Square display identity while retaining Stone Hill music and save-slot ownership. Title demo slot 0 safely uses Doctor Shemp."
            : state.HasIntent
            ? $"Blocked saved intent: {state.Error} Restore it or save the checked intent again before creating a test CUE."
            : "Original Stone Hill is active — no whole-level replacement intent is saved.";
        string editedStateText = hasTownSquareNativeObjectEdits
            ? "Saved Town Square native object edits are present. This pending-DuckStation X-only proof preserves T21's native placement/culling-sector byte; a later gate will validate the derived sector patch."
            : "Edited replacement test unavailable: save a Town Square native object edit first; the initial gate accepts only T21 Red Gem moved on X.";
        _nativeLevelReplacementStatusText.Text = string.IsNullOrWhiteSpace(_nativeLevelReplacementNotice)
            ? $"{stateText}\n\n{editedStateText}"
            : $"{_nativeLevelReplacementNotice}\n\n{stateText}\n\n{editedStateText}";
        _nativeLevelReplacementStatusText.Foreground = new SolidColorBrush(
            _nativeLevelReplacementNoticeIsError || state.HasIntent && !state.ValidIntent
                ? ModernRed
                : state.ValidIntent
                    ? ModernGreen
                    : Color.FromRgb(126, 78, 11));
    }

    private NativeLevelReplacementUiState ReadNativeLevelReplacementUiState()
    {
        string targetKey = NativeLevelReplacementSupportCatalog.FirstTargetLevelKey;
        string manifestPath = NativeLevelReplacementStore.GetPath(_workspace.RootPath, targetKey);
        string intentPath = NativeLevelReplacementIntentStore.GetPath(_workspace.RootPath, targetKey);
        bool hasIntent = File.Exists(intentPath);
        if (!File.Exists(manifestPath))
        {
            return hasIntent
                ? new NativeLevelReplacementUiState(true, false, "Its source-bound retail baseline is missing.", null, null)
                : new NativeLevelReplacementUiState(false, false, "", null, null);
        }

        try
        {
            NativeLevelReplacementManifest manifest = NativeLevelReplacementStore.Load(
                _workspace.RootPath,
                targetKey,
                _catalog)
                ?? throw new InvalidDataException("The source-bound retail baseline is missing.");
            if (!hasIntent)
                return new NativeLevelReplacementUiState(false, false, "", manifest, null);
            NativeLevelReplacementIntent intent = NativeLevelReplacementIntentStore.Load(
                _workspace.RootPath,
                targetKey,
                manifest,
                _catalog)
                ?? throw new InvalidDataException("The saved replacement intent is missing.");
            _ = NativeLevelReplacementProfileRegistry.RequireRuntimeProven(
                intent.ProfileId,
                manifest,
                _catalog);
            return new NativeLevelReplacementUiState(true, true, "", manifest, intent);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or InvalidDataException or
            InvalidOperationException or ArgumentException)
        {
            return new NativeLevelReplacementUiState(hasIntent, false, ex.Message, null, null);
        }
    }

    private async Task SaveNativeLevelReplacementIntentAsync()
    {
        RequireCurrentStoneHill();
        DiscImageSelection source = RequireNativeLevelReplacementSource();
        string targetKey = NativeLevelReplacementSupportCatalog.FirstTargetLevelKey;
        NativeLevelReplacementManifest? manifest = NativeLevelReplacementStore.Load(
            _workspace.RootPath,
            targetKey,
            _catalog);
        manifest ??= await Task.Run(() => NativeLevelReplacementStore.StartStoneHillAsync(
            _workspace.RootPath,
            source.ImagePath,
            _catalog));
        _ = await Task.Run(() => NativeLevelReplacementStore.ValidateSourceAsync(
            manifest,
            source.ImagePath,
            _catalog));
        NativeLevelReplacementIntent intent = await NativeLevelReplacementIntentStore.StartAsync(
            _workspace.RootPath,
            NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillProfileId,
            manifest,
            _catalog);
        SetNativeLevelReplacementNotice(
            "Saved the guarded Town Square into Stone Hill intent. Normal Create BIN and ordinary saved level edits remain unchanged.",
            isError: false);
        _statusText.Text = $"Saved V5 replacement intent {intent.IntentId}. Use Inspect Replacement Safety, then create its separate test CUE.";
        EditorDiagnostics.RecordAction(
            "V5 Stone Hill replacement intent saved",
            $"Profile: {intent.ProfileId}; source: {source.ImagePath}");
    }

    private async Task InspectNativeLevelReplacementSafetyAsync()
    {
        RequireCurrentStoneHill();
        NativeLevelReplacementUiState state = RequireValidNativeLevelReplacementIntent();
        DiscImageSelection source = RequireNativeLevelReplacementSource();
        string outputPrefix = NativeLevelReplacementOutputPrefix();
        StoneHillTownSquareReplacementCandidateRequest request = new(
            state.Manifest!,
            state.Intent!,
            _catalog,
            source.ImagePath,
            source.CuePath,
            outputPrefix + ".bin",
            outputPrefix + ".cue");
        StoneHillTownSquareReplacementCandidatePlan plan = await Task.Run(
            async () => await StoneHillTownSquareReplacementCandidateComposer.BuildPlanAsync(request));
        SetNativeLevelReplacementNotice(
            $"Replacement Safety passed for checked profile {plan.ProfileId}; no BIN/CUE was written.",
            isError: false);
        _statusText.Text = "V5 replacement safety passed. Review the protected scopes before creating its separate test CUE.";
        await ShowNativeLevelReplacementSafetyDialogAsync(plan);
    }

    private async Task CreateNativeLevelReplacementTestCueAsync()
    {
        RequireCurrentStoneHill();
        NativeLevelReplacementUiState state = RequireValidNativeLevelReplacementIntent();
        DiscImageSelection source = RequireNativeLevelReplacementSource();
        string outputPrefix = NativeLevelReplacementOutputPrefix();
        StoneHillTownSquareReplacementCandidateRequest request = new(
            state.Manifest!,
            state.Intent!,
            _catalog,
            source.ImagePath,
            source.CuePath,
            outputPrefix + ".bin",
            outputPrefix + ".cue");
        SetNativeLevelReplacementNotice(
            "Creating the guarded replacement BIN/CUE and verifying its complete final image...",
            isError: false);
        RefreshNativeLevelReplacementPanel();
        StoneHillTownSquareReplacementArtifactResult result = await Task.Run(
            async () => await StoneHillTownSquareReplacementArtifactWriter.ExportAsync(request));
        string message =
            $"Created {Path.GetFileName(result.Candidate.OutputCuePath)} from the runtime-proven profile. " +
            $"Static proof: {Path.GetFileName(result.StaticProofPath)}. Checklist: {Path.GetFileName(result.RuntimeChecklistPath)}.";
        SetNativeLevelReplacementNotice(message, isError: false);
        _statusText.Text = message + OpenContainingFolderStatus(result.Candidate.OutputCuePath);
        EditorDiagnostics.RecordAction(
            "V5 Stone Hill replacement test created",
            $"CUE: {result.Candidate.OutputCuePath}; SHA-256: {result.Candidate.OutputImageSha256}; profile: {result.Candidate.Plan.ProfileId}");
    }

    private async Task CreateEditedNativeLevelReplacementTestCueAsync()
    {
        RequireCurrentStoneHill();
        NativeLevelReplacementUiState state = RequireValidNativeLevelReplacementIntent();
        DiscImageSelection source = RequireNativeLevelReplacementSource();
        string nativeEditsPath = TownSquareNativeObjectEditsPath();
        if (!HasSavedTownSquareNativeObjectEdits())
        {
            throw new InvalidOperationException(
                "Save a Town Square native object edit first. The initial edited-replacement gate accepts only T21 Red Gem moved on X.");
        }

        LevelDefinition townSquare = _catalog.FindByKey("townsquare")
            ?? throw new InvalidDataException("Town Square is missing from the retail level catalog.");
        string intermediateDirectory = NativeLevelReplacementPrivateIntermediateDirectory();
        Directory.CreateDirectory(intermediateDirectory);
        string planPrefix = Path.Combine(intermediateDirectory, "town-square-saved-object-plan");
        MobySourcePatchPlan genuineMobyPatchPlan = await Task.Run(() =>
            MobySourcePatchExporter.BuildPlan(
                source.ImagePath,
                source.CuePath,
                planPrefix + ".bin",
                planPrefix + ".cue",
                townSquare,
                nativeEditsPath));
        MobyBuildSafetyLevelReport buildSafety = await Task.Run(() =>
            MobyBuildSafetyInspector.InspectLevel(source.ImagePath, townSquare, genuineMobyPatchPlan));
        if (buildSafety.Status != MobyBuildSafetyStatus.Stable)
        {
            throw new InvalidOperationException(
                $"The saved Town Square object edit did not pass the focused native Build Safety gate ({buildSafety.StatusLabel}). " +
                string.Join(" ", buildSafety.Findings));
        }
        MobySourcePatchPlan mobyPatchPlan = NormalizeFirstEditedDonorProofPlan(genuineMobyPatchPlan);
        string savedEditSha256 = await HashFileSha256Async(nativeEditsPath);

        SetNativeLevelReplacementNotice(
            "Preparing the exact Town Square display-identity control and composing the saved T21 X move...",
            isError: false);
        RefreshNativeLevelReplacementPanel();
        (string identityImage, string identityCue) = await Task.Run(
            async () => await EnsureExactTownSquareDisplayIdentityControlAsync(
                state,
                source,
                intermediateDirectory));
        string outputPrefix = EditedNativeLevelReplacementOutputPrefix();
        StoneHillTownSquareEditedDonorCandidateRequest request = new(
            identityImage,
            identityCue,
            source.ImagePath,
            mobyPatchPlan,
            outputPrefix + ".bin",
            outputPrefix + ".cue");
        StoneHillTownSquareEditedDonorArtifactResult result = await Task.Run(
            async () => await StoneHillTownSquareEditedDonorArtifactWriter.ExportAsync(request));
        string message =
            $"Created {Path.GetFileName(result.Candidate.OutputCuePath)} for the pending DuckStation T21 X-move gate. " +
            $"Static proof: {Path.GetFileName(result.StaticProofPath)}. Checklist: {Path.GetFileName(result.RuntimeChecklistPath)}.";
        SetNativeLevelReplacementNotice(message, isError: false);
        _statusText.Text = message + OpenContainingFolderStatus(result.Candidate.OutputCuePath);
        EditorDiagnostics.RecordAction(
            "V5 edited Town Square donor replacement test created",
            $"CUE: {result.Candidate.OutputCuePath}; SHA-256: {result.Candidate.OutputImageSha256}; source edits: {nativeEditsPath}; saved edit SHA-256: {savedEditSha256}; native placement sector preserved");
    }

    private static MobySourcePatchPlan NormalizeFirstEditedDonorProofPlan(
        MobySourcePatchPlan genuinePlan)
    {
        MobySourcePatch[] xPatches = genuinePlan.Patches
            .Where(patch =>
                patch.TrueIndex == 21 &&
                string.Equals(patch.Kind, "moby-position-x", StringComparison.Ordinal) &&
                string.Equals(patch.WadRelativeOffset, "0x136E8B4", StringComparison.OrdinalIgnoreCase) &&
                patch.ByteLength == 4)
            .ToArray();
        MobySourcePatch[] placementPatches = genuinePlan.Patches
            .Where(patch =>
                patch.TrueIndex == 21 &&
                string.Equals(patch.Kind, "moby-placement-sector", StringComparison.Ordinal) &&
                string.Equals(patch.WadRelativeOffset, "0x136E8F2", StringComparison.OrdinalIgnoreCase) &&
                patch.ByteLength == 1)
            .ToArray();
        MobySourceEditOutcome[] outcomes = (genuinePlan.EditOutcomes ?? [])
            .ToArray();
        if (genuinePlan.PatchCount != 2 ||
            genuinePlan.TotalPatchedBytes != 5 ||
            genuinePlan.Patches.Count != 2 ||
            xPatches.Length != 1 ||
            placementPatches.Length != 1 ||
            genuinePlan.SkippedEdits.Count != 0 ||
            genuinePlan.PackageImportPreviews.Count != 0 ||
            outcomes.Length != 1 ||
            outcomes[0].EditorTrueIndex != 21 ||
            outcomes[0].SkippedReasons.Count != 0 ||
            outcomes[0].PackageOutcomes.Count != 0 ||
            !outcomes[0].PatchKinds.Order(StringComparer.Ordinal).SequenceEqual(
                new[] { "moby-placement-sector", "moby-position-x" },
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The saved Town Square edits are outside the first edited-replacement proof. " +
                "This gate requires exactly T21 X plus the editor's one derived placement-sector patch; no other edits or dependencies are accepted.");
        }

        MobySourceEditOutcome xOnlyOutcome = outcomes[0] with
        {
            PatchKinds = ["moby-position-x"]
        };
        return genuinePlan with
        {
            PatchCount = 1,
            TotalPatchedBytes = 4,
            Patches = [xPatches[0]],
            EditOutcomes = [xOnlyOutcome],
            Notes = genuinePlan.Notes
                .Concat([
                    "Focused edited-donor proof: the genuine editor plan also derived a placement/culling-sector byte, but this isolated first gate preserves T21's native sector and transplants X only."
                ])
                .ToArray()
        };
    }

    private async Task<(string ImagePath, string CuePath)> EnsureExactTownSquareDisplayIdentityControlAsync(
        NativeLevelReplacementUiState state,
        DiscImageSelection source,
        string intermediateDirectory)
    {
        string developmentIdentityPrefix = Path.Combine(
            _workspace.RootPath,
            "_local",
            "v5-stone-hill-level-replacement",
            "town-square-display-identity-candidate",
            "Stone-Hill-slot-Town-Square-complete-level-with-Town-Square-display-name-RUNTIME-CANDIDATE");
        string privateIdentityPrefix = Path.Combine(
            intermediateDirectory,
            "Stone-Hill-slot-Town-Square-complete-level-with-Town-Square-display-name-RUNTIME-CANDIDATE");
        foreach (string prefix in new[] { developmentIdentityPrefix, privateIdentityPrefix })
        {
            if (await IsExactBinCuePairAsync(
                    prefix + ".bin",
                    prefix + ".cue",
                    NativeLevelReplacementIdentityProfileRegistry.TownSquareDisplayIdentityOutputImageSha256))
            {
                return (prefix + ".bin", prefix + ".cue");
            }
        }

        string developmentBasePrefix = Path.Combine(
            _workspace.RootPath,
            "_local",
            "v5-stone-hill-level-replacement",
            "town-square-complete-pair-candidate",
            "Stone-Hill-slot-Town-Square-complete-level-RUNTIME-CANDIDATE");
        string privateBasePrefix = Path.Combine(
            intermediateDirectory,
            "Stone-Hill-slot-Town-Square-complete-level-RUNTIME-CANDIDATE");
        string basePrefix = "";
        foreach (string candidatePrefix in new[] { developmentBasePrefix, privateBasePrefix })
        {
            if (await IsExactBinCuePairAsync(
                    candidatePrefix + ".bin",
                    candidatePrefix + ".cue",
                    NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillOutputImageSha256))
            {
                basePrefix = candidatePrefix;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(basePrefix))
        {
            basePrefix = privateBasePrefix;
            StoneHillTownSquareReplacementCandidateRequest baseRequest = new(
                state.Manifest!,
                state.Intent!,
                _catalog,
                source.ImagePath,
                source.CuePath,
                basePrefix + ".bin",
                basePrefix + ".cue");
            _ = await StoneHillTownSquareReplacementArtifactWriter.ExportAsync(baseRequest);
        }

        StoneHillTownSquareIdentityCandidateRequest identityRequest = new(
            basePrefix + ".bin",
            basePrefix + ".cue",
            privateIdentityPrefix + ".bin",
            privateIdentityPrefix + ".cue");
        _ = await StoneHillTownSquareIdentityArtifactWriter.ExportAsync(identityRequest);
        if (!await IsExactBinCuePairAsync(
                privateIdentityPrefix + ".bin",
                privateIdentityPrefix + ".cue",
                NativeLevelReplacementIdentityProfileRegistry.TownSquareDisplayIdentityOutputImageSha256))
        {
            throw new InvalidDataException(
                "The private Town Square display-identity control did not match its exact runtime-proven hash after regeneration.");
        }

        return (privateIdentityPrefix + ".bin", privateIdentityPrefix + ".cue");
    }

    private static async Task<bool> IsExactBinCuePairAsync(
        string imagePath,
        string cuePath,
        string expectedImageSha256)
    {
        if (!File.Exists(imagePath) || !File.Exists(cuePath))
            return false;

        await using FileStream image = new(
            imagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            FileOptions.SequentialScan);
        string actualImageSha256 = Convert.ToHexString(await SHA256.HashDataAsync(image)).ToLowerInvariant();
        if (!string.Equals(actualImageSha256, expectedImageSha256, StringComparison.OrdinalIgnoreCase))
            return false;

        string cue = await File.ReadAllTextAsync(cuePath);
        return cue.Contains($"FILE \"{Path.GetFileName(imagePath)}\" BINARY", StringComparison.OrdinalIgnoreCase) &&
            cue.Contains("TRACK 01 MODE2/2352", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> HashFileSha256Async(string path)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
    }

    private string TownSquareNativeObjectEditsPath() =>
        Path.Combine(_workspace.RootPath, "townsquare-native-edits.json");

    private bool HasSavedTownSquareNativeObjectEdits()
    {
        string path = TownSquareNativeObjectEditsPath();
        if (!File.Exists(path))
            return false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("edits", out JsonElement edits) &&
                edits.ValueKind == JsonValueKind.Array &&
                edits.GetArrayLength() > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private async Task RestoreNativeLevelReplacementIntentAsync()
    {
        RequireCurrentStoneHill();
        if (!File.Exists(NativeLevelReplacementIntentStore.GetPath(
                _workspace.RootPath,
                NativeLevelReplacementSupportCatalog.FirstTargetLevelKey)))
        {
            SetNativeLevelReplacementNotice("Original Stone Hill is already active; no replacement intent was saved.", isError: false);
            return;
        }

        if (!await ConfirmRestoreNativeLevelReplacementIntentAsync())
        {
            SetNativeLevelReplacementNotice("Kept the saved Town Square replacement intent.", isError: false);
            return;
        }

        _ = NativeLevelReplacementIntentStore.Delete(
            _workspace.RootPath,
            NativeLevelReplacementSupportCatalog.FirstTargetLevelKey);
        SetNativeLevelReplacementNotice(
            "Restored the original Stone Hill intent. Normal terrain/object edits, the retail baseline manifest, and existing test CUEs were left untouched.",
            isError: false);
        _statusText.Text = "Original Stone Hill intent restored; normal project edits were not changed.";
        EditorDiagnostics.RecordAction("V5 Stone Hill replacement intent restored");
    }

    private async Task<bool> ConfirmRestoreNativeLevelReplacementIntentAsync()
    {
        Window dialog = new()
        {
            Title = "Restore Original Stone Hill Intent",
            Width = 600,
            MinWidth = 520,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };
        StackPanel panel = new() { Spacing = 12, Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock
        {
            Text = "Stop replacing Stone Hill with Town Square?",
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ModernInk)
        });
        panel.Children.Add(new TextBlock
        {
            Text = "This removes only the saved whole-level replacement intent. It does not delete normal terrain/object edits, the source-bound retail baseline, or previously created test BIN/CUE files.",
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 19,
            Foreground = new SolidColorBrush(ModernMutedInk)
        });
        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Keep Replacement");
        Button restore = NewButton("Restore Original Stone Hill");
        StyleModernSecondaryButton(restore, ModernRed);
        cancel.Click += (_, _) => dialog.Close(false);
        restore.Click += (_, _) => dialog.Close(true);
        actions.Children.Add(cancel);
        actions.Children.Add(restore);
        panel.Children.Add(actions);
        dialog.Content = panel;
        return await dialog.ShowDialog<bool>(this);
    }

    private async Task ShowNativeLevelReplacementSafetyDialogAsync(
        StoneHillTownSquareReplacementCandidatePlan plan)
    {
        Window dialog = new()
        {
            Title = "V5 Replacement Safety — Stone Hill",
            Width = 840,
            Height = 650,
            MinWidth = 680,
            MinHeight = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        StackPanel body = new() { Spacing = 10, Margin = new Thickness(18) };
        body.Children.Add(new TextBlock
        {
            Text = "Runtime-proven exact profile — separate test workflow",
            FontSize = 19,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ModernGreen)
        });
        body.Children.Add(SafetyText(
            $"Profile: {plan.ProfileId}\nRecipe: v{plan.ProfileRecipeVersion}\nRoute: Stone Hill <- Town Square\n" +
            $"Evidence: {plan.Evidence} ({plan.EvidenceId})\nExpected final BIN SHA-256: {plan.ExpectedOutputImageSha256}"));
        body.Children.Add(SafetyHeading("Writable scopes"));
        body.Children.Add(SafetyText(BulletList(plan.Safety.WritableScopes)));
        body.Children.Add(SafetyHeading("Protected scopes"));
        body.Children.Add(SafetyText(BulletList(plan.Safety.ProtectedScopes)));
        body.Children.Add(SafetyHeading("Regression checklist"));
        body.Children.Add(SafetyText(BulletList(plan.Safety.RuntimeChecks)));
        body.Children.Add(new TextBlock
        {
            Text = "Normal Create BIN is unchanged and cannot consume this intent. This recipe copies one unchanged retail Town Square pair; it does not compile ordinary saved edits or authorize arbitrary donors.",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeight.SemiBold,
            LineHeight = 18,
            Foreground = new SolidColorBrush(Color.FromRgb(126, 78, 11))
        });
        Button close = NewButton("Close");
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Click += (_, _) => dialog.Close();
        body.Children.Add(close);
        dialog.Content = new ScrollViewer
        {
            Content = body,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        await dialog.ShowDialog(this);

        static TextBlock SafetyHeading(string text) => new()
        {
            Text = text,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ModernInk)
        };
        static TextBlock SafetyText(string text) => new()
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            LineHeight = 17,
            Foreground = new SolidColorBrush(ModernMutedInk)
        };
        static string BulletList(IEnumerable<string> values) =>
            string.Join("\n", values.Select(value => $"• {value}"));
    }

    private NativeLevelReplacementUiState RequireValidNativeLevelReplacementIntent()
    {
        NativeLevelReplacementUiState state = ReadNativeLevelReplacementUiState();
        if (!state.ValidIntent || state.Manifest == null || state.Intent == null)
        {
            throw new InvalidOperationException(state.HasIntent
                ? $"The saved replacement intent is blocked. {state.Error}"
                : "Save the Town Square replacement intent first.");
        }
        return state;
    }

    private DiscImageSelection RequireNativeLevelReplacementSource()
    {
        string sourceImage = FirstExistingDiscImagePath(
            _discImagePathBox.Text,
            _skyboxDiscImagePathBox.Text,
            DiscImageLocator.FindImage(_workspace));
        DiscImageSelection selection = DiscImageLocator.ResolveSelection(sourceImage);
        if (!selection.ImageExists || !selection.CueExists)
        {
            throw new InvalidOperationException(
                "Choose the supported clean Spyro the Dragon USA BIN/CUE first; both matching files must be available.");
        }
        return selection;
    }

    private void RequireCurrentStoneHill()
    {
        if (_currentLevel == null || !string.Equals(
                LevelCatalog.NormalizeKey(_currentLevel.Key),
                NativeLevelReplacementSupportCatalog.FirstTargetLevelKey,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Load Stone Hill before using its V5 replacement workflow.");
        }
    }

    private string NativeLevelReplacementOutputPrefix() => Path.Combine(
        EnsureUserOutputDirectory(),
        "Spyro Editor V5 - Stone Hill replaced by Town Square - RESEARCH ONLY");

    private string EditedNativeLevelReplacementOutputPrefix() => Path.Combine(
        EnsureUserOutputDirectory(),
        "Spyro Editor V5 - Stone Hill replaced by edited Town Square - RESEARCH ONLY");

    private string NativeLevelReplacementPrivateIntermediateDirectory() => Path.Combine(
        EnsureUserOutputDirectory(),
        ".v5-edited-replacement-intermediate");

    private static NativeLevelReplacementProfile RequireTownSquareReplacementProfile() =>
        NativeLevelReplacementProfileRegistry.Find(
            NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillProfileId)
        ?? throw new InvalidDataException("The checked Town Square into Stone Hill profile is missing.");

    private void SetNativeLevelReplacementNotice(string message, bool isError)
    {
        _nativeLevelReplacementNotice = message;
        _nativeLevelReplacementNoticeIsError = isError;
        _nativeLevelReplacementNoticeLevelKey = LevelCatalog.NormalizeKey(_currentLevel?.Key ?? "");
    }

    private sealed record NativeLevelReplacementDonorOption(string ProfileId, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record NativeLevelReplacementUiState(
        bool HasIntent,
        bool ValidIntent,
        string Error,
        NativeLevelReplacementManifest? Manifest,
        NativeLevelReplacementIntent? Intent);
}

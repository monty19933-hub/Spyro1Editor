using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Updates;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private static readonly HttpClient UpdateHttpClient = new() { Timeout = TimeSpan.FromSeconds(45) };
    private bool _quietUpdateCheckStarted;
    private bool _updateDialogOpen;
    private Border? _updateNotificationBanner;
    private TextBlock? _updateNotificationText;
    private EditorUpdateInfo? _bannerUpdate;
    internal Action? UpdateSnapshotStartedForTesting { get; set; }

    private Control BuildAppNotificationArea()
    {
        StackPanel notifications = new();
        notifications.Children.Add(BuildPreviousBetaProjectReminder());
        notifications.Children.Add(BuildUpdateNotificationBanner());
        return notifications;
    }

    private Control BuildUpdateNotificationBanner()
    {
        _updateNotificationText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(25, 74, 51)),
            FontWeight = FontWeight.SemiBold
        };

        Button details = NewButton("What's New & Download");
        details.Click += async (_, _) =>
        {
            if (TryBlockUpdateActionDuringEditorPersistence(
                    "opening update download actions"))
                return;
            EditorUpdateInfo? update = _bannerUpdate;
            if (update == null)
                return;
            if (_updateNotificationBanner != null)
                _updateNotificationBanner.IsVisible = false;
            await ShowUpdateResultDialogAsync(update, "");
        };
        Button later = NewButton("Later", () =>
        {
            if (_updateNotificationBanner != null)
                _updateNotificationBanner.IsVisible = false;
            _statusText.Text = _bannerUpdate == null
                ? "Update reminder dismissed."
                : $"{_bannerUpdate.DisplayName} remains available under More > Check for Updates.";
        });

        Grid content = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };
        content.Children.Add(_updateNotificationText);
        Grid.SetColumn(details, 1);
        content.Children.Add(details);
        Grid.SetColumn(later, 2);
        content.Children.Add(later);

        _updateNotificationBanner = new Border
        {
            IsVisible = false,
            Background = new SolidColorBrush(Color.FromRgb(232, 248, 239)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(151, 207, 176)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 8),
            Child = content
        };
        return _updateNotificationBanner;
    }

    internal void ShowUpdateNotificationForTesting(EditorUpdateInfo update) =>
        ShowUpdateNotification(update);

    private void ShowUpdateNotification(EditorUpdateInfo update)
    {
        if (_updateNotificationBanner == null || _updateNotificationText == null)
            throw new InvalidOperationException("The update notification UI is not initialized.");
        _bannerUpdate = update;
        _updateNotificationText.Text =
            $"{update.DisplayName} is available. See what's new and download it without changing your project files.";
        _updateNotificationBanner.IsVisible = true;
        _updateNotificationBanner.IsEnabled =
            !UpdateActionsBlockedByEditorPersistence();
    }

    private void BeginQuietUpdateCheck()
    {
        if (_quietUpdateCheckStarted || !_releaseMode || ReleaseProjectBootstrap.Current == null)
            return;
        _quietUpdateCheckStarted = true;
        _ = CheckForUpdatesQuietlyAsync();
    }

    private async Task CheckForUpdatesQuietlyAsync()
    {
        ReleaseProjectContext? context = ReleaseProjectBootstrap.Current;
        if (context == null)
            return;
        string statePath = UpdateStatePath(context);
        try
        {
            UpdateCheckState? saved = ReadUpdateCheckState(statePath);
            EditorBetaReleaseVersion? savedAvailable = StateVersion(
                saved?.AvailablePublicVersion,
                saved?.AvailableBetaVersion ?? 0);
            EditorBetaReleaseVersion? savedLastNotified = StateVersion(
                saved?.LastAutoNotificationPublicVersion,
                saved?.LastAutoNotificationBetaVersion ?? 0);
            bool recent = saved?.SchemaVersion == 3 &&
                EditorUpdateNotificationPolicy.IsRecentCheck(
                    DateTimeOffset.UtcNow,
                    saved!.LastCheckedUtc,
                    TimeSpan.FromHours(24));
            if (recent)
            {
                if (savedAvailable?.CompareTo(AppReleaseIdentity.BetaVersion) > 0)
                    _statusText.Text = $"{saved!.AvailableDisplayName} is available. Use More > Check for Updates.";
                if (!EditorUpdateNotificationPolicy.ShouldShow(
                    AppReleaseIdentity.BetaVersion,
                    savedAvailable,
                    savedLastNotified))
                {
                    return;
                }
            }

            GitHubReleaseUpdateClient client = new(UpdateHttpClient);
            EditorUpdateInfo? update = await client.CheckAsync(AppReleaseIdentity.BetaVersion);
            UpdateCheckState next = BuildUpdateState(saved, update, DateTimeOffset.UtcNow);
            await WriteUpdateCheckStateAsync(statePath, next);
            if (update == null)
                return;

            _statusText.Text = $"{update.DisplayName} is available. Your project will remain untouched.";
            EditorBetaReleaseVersion updateVersion = GitHubReleaseUpdateClient.RequireUpdateVersion(update);
            if (EditorUpdateNotificationPolicy.ShouldShow(
                AppReleaseIdentity.BetaVersion,
                updateVersion,
                StateVersion(next.LastAutoNotificationPublicVersion, next.LastAutoNotificationBetaVersion)))
                await ShowUpdateNotificationWhenReadyAsync(update, statePath, next);
        }
        catch (Exception exception)
        {
            EditorDiagnostics.RecordWarning("Background update check failed", exception.Message);
        }
    }

    private async Task ShowUpdateNotificationWhenReadyAsync(
        EditorUpdateInfo update,
        string statePath,
        UpdateCheckState state)
    {
        for (int attempt = 0; attempt < 300 && (!IsVisible || _loadingLevel); attempt++)
            await Task.Delay(100);
        if (!IsVisible || _updateNotificationBanner == null || _updateNotificationText == null)
            return;

        ShowUpdateNotification(update);
        await Task.Yield();
        await WriteUpdateCheckStateAsync(
            statePath,
            state with
            {
                LastAutoNotificationPublicVersion = GitHubReleaseUpdateClient
                    .RequireUpdateVersion(update)
                    .CanonicalVersion
            });
    }

    private async Task ShowUpdatesAsync()
    {
        ReleaseProjectContext? context = ReleaseProjectBootstrap.Current;
        if (!_releaseMode || context == null)
        {
            _statusText.Text = "Update checks are available in packaged release builds.";
            return;
        }

        _statusText.Text = "Checking GitHub Releases for the next Spyro Editor beta...";
        EditorUpdateInfo? update;
        try
        {
            GitHubReleaseUpdateClient client = new(UpdateHttpClient);
            update = await client.CheckAsync(AppReleaseIdentity.BetaVersion);
            string statePath = UpdateStatePath(context);
            UpdateCheckState? saved = ReadUpdateCheckState(statePath);
            UpdateCheckState next = BuildUpdateState(saved, update, DateTimeOffset.UtcNow);
            if (update != null)
            {
                next = next with
                {
                    LastAutoNotificationPublicVersion = GitHubReleaseUpdateClient
                        .RequireUpdateVersion(update)
                        .CanonicalVersion
                };
            }
            await WriteUpdateCheckStateAsync(statePath, next);
        }
        catch (Exception exception)
        {
            EditorDiagnostics.RecordWarning("Could not check for a Spyro Editor update", exception.Message);
            await ShowUpdateResultDialogAsync(null, $"Could not check GitHub Releases: {exception.Message}");
            return;
        }

        if (update == null)
        {
            _statusText.Text = $"{AppReleaseIdentity.DisplayName} is up to date.";
            await ShowUpdateResultDialogAsync(null, $"You already have the newest beta release: {AppReleaseIdentity.DisplayName}.");
            return;
        }
        if (_updateNotificationBanner != null)
            _updateNotificationBanner.IsVisible = false;
        await ShowUpdateResultDialogAsync(update, "");
    }

    private async Task ShowUpdateResultDialogAsync(EditorUpdateInfo? update, string message)
    {
        if (TryBlockUpdateActionDuringEditorPersistence(
                "opening update download actions"))
            return;
        if (_updateDialogOpen)
            return;
        _updateDialogOpen = true;
        try
        {
            await ShowUpdateResultDialogCoreAsync(update, message);
        }
        finally
        {
            _updateDialogOpen = false;
        }
    }

    private async Task ShowUpdateResultDialogCoreAsync(EditorUpdateInfo? update, string message)
    {
        ReleaseProjectContext? context = ReleaseProjectBootstrap.Current;
        Window dialog = new()
        {
            Title = update == null ? "Spyro Editor Updates" : $"Update Available - {update.DisplayName}",
            Width = 700,
            Height = update == null ? 300 : 620,
            MinWidth = 560,
            MinHeight = update == null ? 260 : 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White
        };
        StackPanel body = new() { Margin = new Thickness(24), Spacing = 14 };
        body.Children.Add(new TextBlock
        {
            Text = update == null ? "Update status" : update.DisplayName,
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ModernInk)
        });
        if (update == null)
        {
            body.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(ModernMutedInk),
                LineHeight = 20
            });
            Button closeOnly = NewButton("Close", () => dialog.Close());
            closeOnly.HorizontalAlignment = HorizontalAlignment.Right;
            body.Children.Add(closeOnly);
            dialog.Content = body;
            await dialog.ShowDialog(this);
            return;
        }

        body.Children.Add(new TextBlock
        {
            Text = $"Current release: {AppReleaseIdentity.DisplayName}\nAvailable release: {update.DisplayName}\nPackage: {update.Asset.Name} ({update.Asset.Size / 1_048_576d:0.0} MB)",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ModernMutedInk),
            LineHeight = 20
        });
        body.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(237, 248, 242)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(174, 220, 193)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Child = new TextBlock
            {
                Text = context == null
                    ? "Project storage is unavailable."
                    : $"Project-safe update: your edits remain at {context.Project.RootPath}. Before downloading, the editor creates a verified snapshot outside the application folder.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(31, 101, 65)),
                LineHeight = 19
            }
        });
        body.Children.Add(new TextBlock
        {
            Text = $"What's new in {update.DisplayName}",
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ModernInk)
        });
        body.Children.Add(new Border
        {
            Height = 190,
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = new TextBlock
                {
                    Text = update.ReleaseNotes,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(ModernInk),
                    LineHeight = 20
                }
            }
        });
        ProgressBar progress = new() { Minimum = 0, Maximum = 100, Value = 0, IsVisible = false };
        TextBlock result = new()
        {
            Text = "The package is accepted only when its name, public beta identity, changelog, archive manifest, and SHA-256 digest all agree.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ModernMutedInk)
        };
        body.Children.Add(progress);
        body.Children.Add(result);
        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button viewRelease = NewButton("View Full Release Notes", () => OpenPath(update.ReleasePage.ToString()));
        Button download = NewButton("Download Update");
        download.Click += async (_, _) =>
        {
            if (context == null)
                return;
            if (!TryBeginUpdateSnapshotAction())
            {
                result.Text =
                    "Update download paused while the editor finishes its active save or Build Safety operation.";
                return;
            }
            if (HasUnsavedTerrainEdits() || HasUnsavedMobyEdits())
            {
                result.Text = "Save the current level before downloading so the safety snapshot contains every staged object and terrain edit.";
                _statusText.Text = "Update paused until the current level is saved.";
                return;
            }
            download.IsEnabled = false;
            progress.IsVisible = true;
            result.Text = "Creating and verifying the pre-update project snapshot...";
            try
            {
                ProjectSnapshotResult snapshot = await ProjectSnapshotService.CreateAsync(
                    context.Project,
                    context.UserData.BackupsPath,
                    $"pre-update-beta-v{GitHubReleaseUpdateClient.RequireUpdateVersion(update).CanonicalVersion}");
                result.Text = $"Snapshot verified ({snapshot.FileCount} project file(s)). Downloading update...";
                Progress<double> report = new(value => progress.Value = value * 100);
                GitHubReleaseUpdateClient client = new(UpdateHttpClient);
                string packagePath = await client.DownloadVerifiedAsync(update, context.UserData.UpdatesPath, report);
                string changelogPath = await GitHubReleaseUpdateClient.WriteChangelogAsync(update, context.UserData.UpdatesPath);
                await RememberDownloadedUpdateAsync(context, update);
                result.Text =
                    $"Downloaded and verified: {packagePath}\nChangelog: {changelogPath}\nSnapshot: {snapshot.SnapshotPath}\n\n" +
                    "Your project remains untouched. Automatic in-place replacement stays disabled until the Mac and Windows packages are production-signed; install this release normally.";
                _statusText.Text = $"Downloaded {update.DisplayName}. Your external project remains separate and backed up.";
                OpenPath(context.UserData.UpdatesPath);
            }
            catch (Exception exception)
            {
                result.Text = $"Update download stopped safely: {exception.Message}";
                EditorDiagnostics.RecordWarning("Update download or backup stopped", exception.Message);
            }
            finally
            {
                download.IsEnabled = true;
            }
        };
        actions.Children.Add(viewRelease);
        actions.Children.Add(download);
        actions.Children.Add(NewButton("Later", () => dialog.Close()));
        body.Children.Add(actions);
        dialog.Content = new ScrollViewer { Content = body };
        await dialog.ShowDialog(this);
    }

    private bool UpdateActionsBlockedByEditorPersistence() =>
        _workspaceTransitionBusy ||
        _id65BlankLabBusy && _id65BlankLabManualOperation != null ||
        _regularEditorPersistenceBusy ||
        _buildSafetyBusy;

    private bool TryBlockUpdateActionDuringEditorPersistence(string action)
    {
        if (TryBlockEditorMutationDuringId65BlankLabManualOperation(action))
            return true;
        if (!_regularEditorPersistenceBusy && !_buildSafetyBusy)
            return false;

        _statusText.Text =
            $"Wait for the active save or Build Safety operation before {action}.";
        return true;
    }

    private bool TryBeginUpdateSnapshotAction()
    {
        if (TryBlockUpdateActionDuringEditorPersistence(
                "creating an update safety snapshot"))
        {
            return false;
        }

        UpdateSnapshotStartedForTesting?.Invoke();
        return true;
    }

    internal bool TryBeginUpdateSnapshotForTesting() =>
        TryBeginUpdateSnapshotAction();

    private static string UpdateStatePath(ReleaseProjectContext context) =>
        Path.Combine(context.UserData.SettingsPath, "update-check.json");

    private static UpdateCheckState BuildUpdateState(
        UpdateCheckState? saved,
        EditorUpdateInfo? update,
        DateTimeOffset checkedAtUtc)
    {
        EditorBetaReleaseVersion? available = update == null
            ? null
            : GitHubReleaseUpdateClient.RequireUpdateVersion(update);
        return new(
            SchemaVersion: 3,
            LastCheckedUtc: checkedAtUtc,
            AvailablePublicVersion: available?.CanonicalVersion ?? "",
            AvailableDisplayName: update?.DisplayName ?? "",
            ReleaseUrl: update?.ReleasePage.ToString() ?? "",
            LastAutoNotificationPublicVersion: CanonicalStateVersion(
                saved?.LastAutoNotificationPublicVersion,
                saved?.LastAutoNotificationBetaVersion ?? 0),
            LastDownloadedPublicVersion: CanonicalStateVersion(
                saved?.LastDownloadedPublicVersion,
                saved?.LastDownloadedBetaVersion ?? 0),
            LastDownloadedAtUtc: saved?.LastDownloadedAtUtc);
    }

    private static async Task RememberDownloadedUpdateAsync(
        ReleaseProjectContext context,
        EditorUpdateInfo update)
    {
        string statePath = UpdateStatePath(context);
        UpdateCheckState? saved = ReadUpdateCheckState(statePath);
        EditorBetaReleaseVersion updateVersion = GitHubReleaseUpdateClient.RequireUpdateVersion(update);
        UpdateCheckState next = BuildUpdateState(saved, update, saved?.LastCheckedUtc ?? DateTimeOffset.UtcNow) with
        {
            LastAutoNotificationPublicVersion = MaxVersionText(
                CanonicalStateVersion(
                    saved?.LastAutoNotificationPublicVersion,
                    saved?.LastAutoNotificationBetaVersion ?? 0),
                updateVersion.CanonicalVersion),
            LastDownloadedPublicVersion = updateVersion.CanonicalVersion,
            LastDownloadedAtUtc = DateTimeOffset.UtcNow
        };
        await WriteUpdateCheckStateAsync(statePath, next);
    }

    internal static string MigrateUpdateStateJsonForTesting(string json)
    {
        UpdateCheckState state = JsonSerializer.Deserialize<UpdateCheckState>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new JsonException("Update state is empty.");
        UpdateCheckState migrated = UpgradePersistedState(state);
        return JsonSerializer.Serialize(migrated, StateJsonOptions());
    }

    private static UpdateCheckState? ReadUpdateCheckState(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<UpdateCheckState>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static async Task WriteUpdateCheckStateAsync(string path, UpdateCheckState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        string temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(state, StateJsonOptions()));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static UpdateCheckState UpgradePersistedState(UpdateCheckState state) =>
        new(
            SchemaVersion: 3,
            LastCheckedUtc: state.LastCheckedUtc,
            AvailablePublicVersion: CanonicalStateVersion(
                state.AvailablePublicVersion,
                state.AvailableBetaVersion),
            AvailableDisplayName: state.AvailableDisplayName,
            ReleaseUrl: state.ReleaseUrl,
            LastAutoNotificationPublicVersion: CanonicalStateVersion(
                state.LastAutoNotificationPublicVersion,
                state.LastAutoNotificationBetaVersion),
            LastDownloadedPublicVersion: CanonicalStateVersion(
                state.LastDownloadedPublicVersion,
                state.LastDownloadedBetaVersion),
            LastDownloadedAtUtc: state.LastDownloadedAtUtc);

    private static EditorBetaReleaseVersion? StateVersion(string? canonical, int legacyMajor)
    {
        if (!string.IsNullOrWhiteSpace(canonical))
            return EditorBetaReleaseVersion.TryParse(canonical, out EditorBetaReleaseVersion version) ? version : null;
        return legacyMajor > 0 ? new EditorBetaReleaseVersion(legacyMajor) : null;
    }

    private static string CanonicalStateVersion(string? canonical, int legacyMajor) =>
        StateVersion(canonical, legacyMajor)?.CanonicalVersion ?? "";

    private static string MaxVersionText(string left, string right)
    {
        EditorBetaReleaseVersion? leftVersion = StateVersion(left, 0);
        EditorBetaReleaseVersion? rightVersion = StateVersion(right, 0);
        if (leftVersion == null)
            return rightVersion?.CanonicalVersion ?? "";
        if (rightVersion == null)
            return leftVersion.CanonicalVersion;
        return leftVersion.CompareTo(rightVersion) >= 0
            ? leftVersion.CanonicalVersion
            : rightVersion.CanonicalVersion;
    }

    private static JsonSerializerOptions StateJsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed record UpdateCheckState(
        int SchemaVersion,
        DateTimeOffset LastCheckedUtc,
        string AvailablePublicVersion = "",
        string AvailableDisplayName = "",
        string ReleaseUrl = "",
        string LastAutoNotificationPublicVersion = "",
        string LastDownloadedPublicVersion = "",
        DateTimeOffset? LastDownloadedAtUtc = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int AvailableBetaVersion = 0,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int LastAutoNotificationBetaVersion = 0,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int LastDownloadedBetaVersion = 0);
}

namespace Spyro.Editor.Core.Persistence;

/// <summary>
/// A project-owned export decision, not a source or native-runtime certificate.
/// Explicit OFF still requires a complete rebuild from the original disc.
/// </summary>
public sealed class NativeFreeFlightProjectExportIntent
{
    public const int SupportedEnabledLevelId = 14;
    private readonly NativeFreeFlightSettingsSnapshot _snapshot;
    private readonly string _workspaceRoot;

    private NativeFreeFlightProjectExportIntent(string workspaceRoot, NativeFreeFlightSettingsSnapshot snapshot, int? enabledLevelId)
    { _workspaceRoot = workspaceRoot; _snapshot = snapshot; EnabledLevelId = enabledLevelId; }

    public bool RequiresRebuild => _snapshot.Preferences.Count != 0;
    public int? EnabledLevelId { get; }
    public bool HasExplicitSelection(int levelId) => _snapshot.HasExplicitSelection(levelId);

    public static NativeFreeFlightProjectExportIntent Capture(string workspaceRoot)
    {
        string root = Normalize(workspaceRoot);
        var snapshot = NativeFreeFlightSettingsStore.Capture(root);
        var enabled = snapshot.Preferences.Where(p => p.EnableFreeFlight).ToArray();
        if (enabled.Length > 1)
            throw new InvalidDataException("Fly Anywhere currently supports one enabled target per exported project; turn other targets OFF before exporting.");
        if (enabled.Length == 1 && enabled[0].LevelId != SupportedEnabledLevelId)
            throw new InvalidDataException("Fly Anywhere ON currently supports Toasty (level 14) only. This setting was not substituted with another level.");
        return new(root, snapshot, enabled.Length == 0 ? null : enabled[0].LevelId);
    }

    public void RequireCurrent(string workspaceRoot)
    {
        if (!string.Equals(Normalize(workspaceRoot), _workspaceRoot, StringComparison.Ordinal))
            throw new InvalidDataException("Fly Anywhere export belongs to a different project. Start Create BIN again in the intended project.");
        var current = NativeFreeFlightSettingsStore.Capture(_workspaceRoot);
        if (current.Path != _snapshot.Path || current.ContentSha256 != _snapshot.ContentSha256)
            throw new InvalidDataException("Fly Anywhere settings changed during export. Reload the saved choice and create the BIN again.");
    }

    public void RequireOrdinaryEditCompatibility(bool hasOrdinaryEdits)
    {
        if (EnabledLevelId != null && hasOrdinaryEdits)
            throw new InvalidDataException("Fly Anywhere ON currently requires a project without other saved edits; combined native behavior has not been validated. OFF can rebuild with ordinary saved edits.");
    }

    private static string Normalize(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspaceRoot));
    }
}

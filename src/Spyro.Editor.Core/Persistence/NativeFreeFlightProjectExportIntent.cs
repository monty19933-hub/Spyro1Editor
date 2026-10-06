using Spyro.Editor.Core.Exporting;

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
    // Legacy stage callers use this only as an ON marker. New combined
    // exports consume Enabled and the global composition contract.
    public int? EnabledLevelId { get; }
    public bool Enabled => _snapshot.GlobalEnabled;
    /// <summary>The saved choice, level by level: every normal level for the whole-game switch, or the
    /// chosen ones. The composition plan treats the complete set as the whole game.</summary>
    public IReadOnlyList<int> EnabledLevelIds => Array.AsReadOnly(_snapshot.Preferences
        .Where(p => p.EnableFreeFlight && NativeFreeFlightCompositionPlan.IsNormalLevel(p.LevelId))
        .Select(p => p.LevelId).Distinct().Order().ToArray());
    public bool WholeGame => Enabled &&
        Enumerable.Range(10, 55).Where(NativeFreeFlightCompositionPlan.IsNormalLevel).All(EnabledLevelIds.Contains);
    public bool HasExplicitSelection(int levelId) => _snapshot.HasExplicitSelection(levelId);

    public static NativeFreeFlightProjectExportIntent Capture(string workspaceRoot)
    {
        string root = Normalize(workspaceRoot);
        var snapshot = NativeFreeFlightSettingsStore.Capture(root);
        // A per-level ON is exported for exactly the saved levels (1.2); the
        // complete set of normal levels is the whole-game policy. Never silently OFF.
        return new(root, snapshot, snapshot.GlobalEnabled ? SupportedEnabledLevelId : null);
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
        // Flight player-stream validation joins the ordinary data composer before
        // final allocation. The final composer, not intent, proves byte owners
        // and capacity. This method grants no native/runtime certificate.
        _ = hasOrdinaryEdits;
    }

    private static string Normalize(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspaceRoot));
    }
}

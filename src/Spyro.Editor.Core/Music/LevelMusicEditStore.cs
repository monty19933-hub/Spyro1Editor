using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Music;

public static class LevelMusicEditStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string PlanPath(string workspaceRoot, LevelDefinition level) =>
        Path.Combine(workspaceRoot, $"{level.Key}-level-music-edit-plan.json");

    public static LevelMusicEditPlan? Load(string workspaceRoot, LevelDefinition level)
    {
        string path = PlanPath(workspaceRoot, level);
        if (!File.Exists(path))
            return null;

        try
        {
            LevelMusicEditPlan? plan = JsonSerializer.Deserialize<LevelMusicEditPlan>(File.ReadAllText(path), JsonOptions);
            int levelIndex = MusicTrackCatalog.GetLevelIndex(level);
            int nativeTrackId = MusicTrackCatalog.GetNativeTrackId(level);
            MusicTrackEntry? selected = plan == null ? null : MusicTrackCatalog.Find(plan.SelectedTrackId);
            if (plan == null ||
                !string.Equals(LevelCatalog.NormalizeKey(plan.LevelKey), LevelCatalog.NormalizeKey(level.Key), StringComparison.OrdinalIgnoreCase) ||
                plan.LevelIndex != levelIndex ||
                plan.NativeTrackId != nativeTrackId ||
                selected == null ||
                !selected.IsSelectable ||
                plan.SelectedTrackId == nativeTrackId ||
                !plan.LockLongPlayToSelectedTrack)
            {
                return null;
            }

            return plan with
            {
                LevelKey = level.Key,
                LevelName = level.DisplayName,
                SelectedTrackName = selected.DisplayName
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static async Task<string> SaveAsync(
        string workspaceRoot,
        LevelDefinition level,
        int selectedTrackId,
        CancellationToken cancellationToken = default)
    {
        int nativeTrackId = MusicTrackCatalog.GetNativeTrackId(level);
        string path = PlanPath(workspaceRoot, level);
        if (selectedTrackId == nativeTrackId)
        {
            Delete(workspaceRoot, level);
            return path;
        }

        MusicTrackEntry track = MusicTrackCatalog.Find(selectedTrackId)
            ?? throw new InvalidOperationException($"Music track {selectedTrackId} does not exist.");
        if (!track.IsSelectable)
            throw new InvalidOperationException($"{track.DisplayName} is reserved for future custom-audio support.");

        LevelMusicEditPlan plan = new(
            GeneratedAt: DateTimeOffset.UtcNow,
            Kind: "exe-level-music-edit",
            LevelKey: level.Key,
            LevelName: level.DisplayName,
            LevelIndex: MusicTrackCatalog.GetLevelIndex(level),
            NativeTrackId: nativeTrackId,
            SelectedTrackId: selectedTrackId,
            SelectedTrackName: track.DisplayName,
            LockLongPlayToSelectedTrack: true);

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? workspaceRoot);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(plan, JsonOptions), cancellationToken);
        return path;
    }

    public static void Delete(string workspaceRoot, LevelDefinition level)
    {
        string path = PlanPath(workspaceRoot, level);
        if (File.Exists(path))
            File.Delete(path);
    }
}

public sealed record LevelMusicEditPlan(
    DateTimeOffset GeneratedAt,
    string Kind,
    string LevelKey,
    string LevelName,
    int LevelIndex,
    int NativeTrackId,
    int SelectedTrackId,
    string SelectedTrackName,
    bool LockLongPlayToSelectedTrack);

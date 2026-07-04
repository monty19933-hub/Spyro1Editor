using System.Text.Json;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Editing;

public static class IdentityBatchOverlay
{
    public static IdentityBatchOverlayResult Apply(string path, IList<Moby> mobys, string loadedSummary = "identity test batch")
    {
        Dictionary<int, Moby> byTrueIndex = mobys
            .Where(moby => moby.TrueIndex >= 0)
            .ToDictionary(moby => moby.TrueIndex);
        Dictionary<int, IdentityBatchRestoreState> restore = CaptureRestoreState(path, byTrueIndex);
        List<Moby> changed = new();

        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("edits", out JsonElement edits) || edits.ValueKind != JsonValueKind.Array)
            return new IdentityBatchOverlayResult(changed, new IdentityBatchRestoreSet(restore));

        foreach (JsonElement edit in edits.EnumerateArray())
        {
            int trueIndex = JsonValue.GetInt32(edit, "trueIndex", -1);
            if (trueIndex < 0 || !byTrueIndex.TryGetValue(trueIndex, out Moby? moby))
                continue;

            if (edit.TryGetProperty("edited", out JsonElement edited) && edited.ValueKind == JsonValueKind.Object)
            {
                moby.Position = new Vector3f(
                    JsonValue.GetSingle(edited, "x", moby.Position.X),
                    JsonValue.GetSingle(edited, "y", moby.Position.Y),
                    JsonValue.GetSingle(edited, "z", moby.Position.Z));
            }

            string label = JsonValue.GetString(edit, "labelEdited", JsonValue.GetString(edit, "label", moby.Label));
            if (!string.IsNullOrWhiteSpace(label))
                moby.Label = label;

            moby.HasLoadedNativeEdit = true;
            moby.LoadedNativeEditSummary = loadedSummary;
            changed.Add(moby);
        }

        return new IdentityBatchOverlayResult(changed, new IdentityBatchRestoreSet(restore));
    }

    public static int Clear(IdentityBatchRestoreSet restoreSet, IList<Moby> mobys)
    {
        Dictionary<int, Moby> byTrueIndex = mobys
            .Where(moby => moby.TrueIndex >= 0)
            .ToDictionary(moby => moby.TrueIndex);
        int restored = 0;
        foreach ((int trueIndex, IdentityBatchRestoreState state) in restoreSet.ByTrueIndex)
        {
            if (!byTrueIndex.TryGetValue(trueIndex, out Moby? moby))
                continue;

            moby.Position = state.Position;
            moby.Label = state.Label;
            moby.HasLoadedNativeEdit = state.HasLoadedNativeEdit;
            moby.LoadedNativeEditSummary = state.LoadedNativeEditSummary;
            restored++;
        }

        return restored;
    }

    public static IReadOnlyList<int> ReadTrueIndexes(string path)
    {
        List<int> trueIndexes = new();
        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("edits", out JsonElement edits) || edits.ValueKind != JsonValueKind.Array)
            return trueIndexes;

        foreach (JsonElement edit in edits.EnumerateArray())
        {
            int trueIndex = JsonValue.GetInt32(edit, "trueIndex", -1);
            if (trueIndex >= 0)
                trueIndexes.Add(trueIndex);
        }

        return trueIndexes;
    }

    private static Dictionary<int, IdentityBatchRestoreState> CaptureRestoreState(string path, IReadOnlyDictionary<int, Moby> mobysByTrueIndex)
    {
        Dictionary<int, IdentityBatchRestoreState> restore = new();
        foreach (int trueIndex in ReadTrueIndexes(path))
        {
            if (!mobysByTrueIndex.TryGetValue(trueIndex, out Moby? moby))
                continue;

            restore[trueIndex] = new IdentityBatchRestoreState(
                moby.Position,
                moby.Label,
                moby.HasLoadedNativeEdit,
                moby.LoadedNativeEditSummary);
        }

        return restore;
    }
}

public sealed record IdentityBatchOverlayResult(
    IReadOnlyList<Moby> ChangedMobys,
    IdentityBatchRestoreSet RestoreSet);

public sealed record IdentityBatchRestoreSet(
    IReadOnlyDictionary<int, IdentityBatchRestoreState> ByTrueIndex)
{
    public int Count => ByTrueIndex.Count;
}

public sealed record IdentityBatchRestoreState(
    Vector3f Position,
    string Label,
    bool HasLoadedNativeEdit,
    string LoadedNativeEditSummary);

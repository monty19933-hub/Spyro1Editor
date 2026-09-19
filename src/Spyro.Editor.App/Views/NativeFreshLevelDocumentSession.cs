using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.App.Views;

/// <summary>
/// App-only applied aggregate, selection and history over an actual Store snapshot.
/// Raw form text and active-project/busy guards belong to the future window.
/// This session issues no source, compilation, export or runtime authority.
/// </summary>
internal sealed class NativeFreshLevelDocumentSession
{
    private const int MaximumHistory = 100;
    private readonly List<HistoryEntry> _undo = [];
    private readonly List<HistoryEntry> _redo = [];

    private sealed record HistoryEntry(NativeFreshLevelDocumentIntent? Intent, string? SelectedEditorId, string? SelectedTileId);

    internal NativeFreshLevelDocumentSession(NativeFreshLevelDocumentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Snapshot = snapshot;
        CurrentIntent = snapshot.Intent;
        SelectedTileId = CurrentIntent?.TiledContent?.Tiles.FirstOrDefault()?.EditorId;
    }

    internal NativeFreshLevelDocumentSnapshot Snapshot { get; private set; }
    internal NativeFreshLevelDocumentIntent? CurrentIntent { get; private set; }
    internal string? SelectedEditorId { get; private set; }
    internal string? SelectedTileId { get; private set; }
    internal bool HasUnsavedChanges => !Equal(CurrentIntent, Snapshot.Intent);
    internal bool CanUndo => _undo.Count != 0;
    internal bool CanRedo => _redo.Count != 0;

    internal void Select(string? editorId)
    {
        if (editorId != null && !Contains(CurrentIntent, editorId))
            throw new InvalidDataException("The selected editor ID is not present in the applied aggregate.");
        SelectedEditorId = editorId;
    }

    internal void Apply(NativeFreshLevelDocumentIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        // Factories already validated the entire immutable aggregate. A semantic
        // no-op must preserve actual references, selection and meaningful redo.
        if (Equal(CurrentIntent, intent)) return;
        Push(_undo, CaptureHistory());
        _redo.Clear();
        CurrentIntent = intent;
        if (!Contains(intent, SelectedEditorId)) SelectedEditorId = null;
        if (intent.TiledContent?.Tiles.Any(t => t.EditorId == SelectedTileId) != true)
            SelectedTileId = intent.TiledContent?.Tiles.FirstOrDefault()?.EditorId;
    }

    internal NativeFreshWizardRouteIntent AddWizardRoute(int originX, int originY, int originZ, int endX, int endY)
    {
        NativeFreshLevelDocumentIntent current = RequireDocument();
        var added = NativeFreshWizardRouteIntent.Create(Guid.NewGuid().ToString("N"),
            originX, originY, originZ, endX, endY);
        // Validate count and whole-floor containment before changing session state.
        var next = current.WithPlacements(current.Placements.Append(added));
        Apply(next);
        SelectedEditorId = added.EditorId;
        return added;
    }

    internal void ReplaceWizardRoute(string editorId, int originX, int originY, int originZ, int endX, int endY)
    {
        NativeFreshLevelDocumentIntent current = RequireDocument();
        RequirePlacement(current, editorId);
        if (current.Placements.Single(placement => placement.EditorId == editorId) is not NativeFreshWizardRouteIntent)
            throw new InvalidDataException("The selected editor ID is not an authored Wizard route; replacement cannot change its placement tag.");
        var replacement = NativeFreshWizardRouteIntent.Create(editorId, originX, originY, originZ, endX, endY);
        var next = current.WithPlacements(
            current.Placements.Select(placement => placement.EditorId == editorId ? replacement : placement));
        Apply(next);
    }

    internal NativeFreshMobyPlacementIntent AddNativeMoby(NativeFreshRetailMobyReference source, int x, int y, int z, int nativeYawByte)
    {
        NativeFreshLevelDocumentIntent current = RequireDocument();
        var added = NativeFreshMobyPlacementIntent.Create(Guid.NewGuid().ToString("N"), source, x, y, z, nativeYawByte);
        // Upgrade is an explicit Add action, installed only after the full current
        // floor/list validation succeeds. An invalid Add preserves old schema/history.
        var next = current.UpgradeForNativeMobys().WithPlacements(current.Placements.Append(added));
        Apply(next);
        SelectedEditorId = added.EditorId;
        return added;
    }

    internal void ReplaceNativeMoby(string editorId, NativeFreshRetailMobyReference source, int x, int y, int z, int nativeYawByte)
    {
        NativeFreshLevelDocumentIntent current = RequireDocument();
        RequirePlacement(current, editorId);
        if (current.Placements.Single(placement => placement.EditorId == editorId) is not NativeFreshMobyPlacementIntent)
            throw new InvalidDataException("The selected editor ID is not an authored native Moby; replacement cannot change its placement tag.");
        var replacement = NativeFreshMobyPlacementIntent.Create(editorId, source, x, y, z, nativeYawByte);
        var next = current.WithPlacements(
            current.Placements.Select(placement => placement.EditorId == editorId ? replacement : placement));
        Apply(next);
    }

    internal void RemovePlacement(string editorId)
    {
        NativeFreshLevelDocumentIntent current = RequireDocument();
        RequirePlacement(current, editorId);
        var next = current.WithPlacements(
            current.Placements.Where(placement => placement.EditorId != editorId));
        Apply(next);
    }

    internal bool Undo()
    {
        if (_undo.Count == 0) return false;
        Push(_redo, CaptureHistory());
        Restore(Pop(_undo));
        return true;
    }

    internal void SelectTile(string editorId)
    {
        if (CurrentIntent?.TiledContent?.Tiles.Any(t => t.EditorId == editorId) != true)
            throw new InvalidDataException("The tile editor ID is not in the applied tiled document.");
        SelectedTileId = editorId;
    }

    internal void UpgradeToTiles()
    {
        var current = RequireDocument();
        if (current.TiledContent != null) return;
        Apply(current.UpgradeToTiled(Guid.NewGuid().ToString("N")));
    }

    internal void AddAdjacentTile()
    {
        var current = RequireDocument();
        var content = current.TiledContent ?? throw new InvalidDataException("Upgrade to tiled geometry first.");
        var selected = content.Tiles.Single(t => t.EditorId == SelectedTileId);
        var added = NativeFreshFlatTileIntent.Create(Guid.NewGuid().ToString("N"),
            checked(selected.X + selected.Width), selected.Y, selected.Z, selected.Width, selected.Depth,
            selected.FloorColor, selected.Materials);
        Apply(current.WithTiledContent(content.WithTiles(content.Tiles.Append(added))));
        SelectedTileId = added.EditorId;
    }

    internal void RemoveSelectedTile()
    {
        var current = RequireDocument();
        var content = current.TiledContent ?? throw new InvalidDataException("No tiled geometry is active.");
        if (SelectedTileId == null) throw new InvalidDataException("Select a tile first.");
        // Whole-content and placement validation happens before any history/state mutation.
        Apply(current.WithTiledContent(content.WithTiles(content.Tiles.Where(t => t.EditorId != SelectedTileId))));
    }

    internal void MoveSelectedTile(int direction)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        var current = RequireDocument();
        var content = current.TiledContent ?? throw new InvalidDataException("No tiled geometry is active.");
        var tiles = content.Tiles.ToArray();
        int index = Array.FindIndex(tiles, t => t.EditorId == SelectedTileId), target = index + direction;
        if (index < 0) throw new InvalidDataException("Select a tile first.");
        if (target < 0 || target >= tiles.Length) return;
        (tiles[index], tiles[target]) = (tiles[target], tiles[index]);
        Apply(current.WithTiledContent(content.WithTiles(tiles)));
    }

    internal bool Redo()
    {
        if (_redo.Count == 0) return false;
        Push(_undo, CaptureHistory());
        Restore(Pop(_redo));
        return true;
    }

    internal NativeFreshLevelDocumentSnapshot Save(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        NativeFreshLevelDocumentIntent current = RequireDocument();
        NativeFreshLevelDocumentSnapshot saved = NativeFreshLevelDocumentStore.Save(Snapshot, current, token);
        // Assign only after an actual successful Store return. CurrentIntent stays
        // the applied object; saved.Intent is independently parsed. Future export
        // must compile the actual captured Snapshot.Intent, not assume equality
        // of those references merely because their canonical bytes agree.
        Snapshot = saved;
        _undo.RemoveAll(entry => entry.Intent == null);
        _redo.RemoveAll(entry => entry.Intent == null);
        return saved;
    }

    internal NativeFreshLevelDocumentSnapshot Reopen(CancellationToken token = default)
    {
        NativeFreshLevelDocumentSnapshot next = NativeFreshLevelDocumentStore.Capture(
            new EditorProjectLayout(Snapshot.RootPath, Snapshot.ProjectId), token);
        // A failed capture, including malformed bytes, installs nothing.
        Snapshot = next;
        CurrentIntent = next.Intent;
        _undo.Clear();
        _redo.Clear();
        if (!Contains(CurrentIntent, SelectedEditorId)) SelectedEditorId = null;
        if (CurrentIntent?.TiledContent?.Tiles.Any(t => t.EditorId == SelectedTileId) != true)
            SelectedTileId = CurrentIntent?.TiledContent?.Tiles.FirstOrDefault()?.EditorId;
        return next;
    }

    internal NativeFreshLevelDocumentSnapshot ForkLegacyFloor(NativeId65FreshDocumentSnapshot source,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        token.ThrowIfCancellationRequested();
        if (Snapshot.Exists || CurrentIntent != null || CanUndo || CanRedo || SelectedEditorId != null)
            throw new InvalidOperationException("Explicit legacy fork requires a pristine absent aggregate session.");
        NativeFreshLevelDocumentSnapshot forked = NativeFreshLevelDocumentStore.ForkLegacyFloor(source, Snapshot, token);
        // The Store alone owns new-file publication and its outcome. Exceptions
        // (including known commit or Unconfirmed) propagate with all state intact;
        // observed destination bytes never replace a missing successful return.
        Snapshot = forked;
        CurrentIntent = forked.Intent;
        SelectedEditorId = null;
        _undo.Clear();
        _redo.Clear();
        return forked;
    }

    private NativeFreshLevelDocumentIntent RequireDocument() => CurrentIntent ??
        throw new InvalidOperationException("Apply an explicit aggregate or fork an actual saved floor before editing or saving.");

    private static void RequirePlacement(NativeFreshLevelDocumentIntent current, string editorId)
    {
        ArgumentNullException.ThrowIfNull(editorId);
        if (!Contains(current, editorId))
            throw new InvalidDataException("The editor ID is not present in the applied aggregate.");
    }

    private static bool Contains(NativeFreshLevelDocumentIntent? intent, string? editorId) =>
        editorId != null && intent != null && intent.Placements.Any(placement => placement.EditorId == editorId);

    private static bool Equal(NativeFreshLevelDocumentIntent? left, NativeFreshLevelDocumentIntent? right) =>
        ReferenceEquals(left, right) || left != null && right != null &&
        NativeFreshLevelDocumentCodec.SerializeCanonical(left).AsSpan()
            .SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(right));

    private HistoryEntry CaptureHistory() => new(CurrentIntent, SelectedEditorId, SelectedTileId);

    private void Restore(HistoryEntry entry)
    {
        CurrentIntent = entry.Intent;
        SelectedEditorId = entry.SelectedEditorId;
        SelectedTileId = entry.SelectedTileId;
    }

    private static void Push(List<HistoryEntry> history, HistoryEntry entry)
    {
        if (history.Count == MaximumHistory) history.RemoveAt(0);
        history.Add(entry);
    }

    private static HistoryEntry Pop(List<HistoryEntry> history)
    {
        HistoryEntry entry = history[^1];
        history.RemoveAt(history.Count - 1);
        return entry;
    }
}

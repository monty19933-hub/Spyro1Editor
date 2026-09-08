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

    private sealed record HistoryEntry(NativeFreshLevelDocumentIntent? Intent, string? SelectedEditorId);

    internal NativeFreshLevelDocumentSession(NativeFreshLevelDocumentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Snapshot = snapshot;
        CurrentIntent = snapshot.Intent;
    }

    internal NativeFreshLevelDocumentSnapshot Snapshot { get; private set; }
    internal NativeFreshLevelDocumentIntent? CurrentIntent { get; private set; }
    internal string? SelectedEditorId { get; private set; }
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
    }

    internal NativeFreshWizardRouteIntent AddWizardRoute(int originX, int originY, int originZ, int endX, int endY)
    {
        NativeFreshLevelDocumentIntent current = RequireDocument();
        var added = NativeFreshWizardRouteIntent.Create(Guid.NewGuid().ToString("N"),
            originX, originY, originZ, endX, endY);
        // Validate count and whole-floor containment before changing session state.
        var next = current.WithContent(current.Base, current.Placements.Append(added));
        Apply(next);
        SelectedEditorId = added.EditorId;
        return added;
    }

    internal void ReplaceWizardRoute(string editorId, int originX, int originY, int originZ, int endX, int endY)
    {
        NativeFreshLevelDocumentIntent current = RequireDocument();
        RequirePlacement(current, editorId);
        var replacement = NativeFreshWizardRouteIntent.Create(editorId, originX, originY, originZ, endX, endY);
        var next = current.WithContent(current.Base,
            current.Placements.Select(placement => placement.EditorId == editorId ? replacement : placement));
        Apply(next);
    }

    internal void RemovePlacement(string editorId)
    {
        NativeFreshLevelDocumentIntent current = RequireDocument();
        RequirePlacement(current, editorId);
        var next = current.WithContent(current.Base,
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

    private HistoryEntry CaptureHistory() => new(CurrentIntent, SelectedEditorId);

    private void Restore(HistoryEntry entry)
    {
        CurrentIntent = entry.Intent;
        SelectedEditorId = entry.SelectedEditorId;
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

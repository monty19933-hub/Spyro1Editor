using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

/// <summary>
/// Wording for the shipping interface. The editor's working vocabulary ("live edits", "structural",
/// "geometry patches") is exact but reads like a log; these say the same things the way a player would.
/// </summary>
public sealed partial class MainWindow
{
    private static string Count(int value, string singular, string? plural = null) =>
        $"{value:N0} {(value == 1 ? singular : plural ?? singular + "s")}";

    /// <summary>"Level details" in Game Settings.</summary>
    private string BuildPlainLevelDetails(int objects, int entryMarkers)
    {
        int tiles = _currentGeometry?.Polygons.Count(face => !face.IsTerrainRemoved) ?? 0;
        int changedObjects = _currentMobys.Count(moby => moby.HasAnyEdit);
        int reshaped = 0, repainted = 0, addedOrRemoved = 0;
        if (_currentGeometry != null)
        {
            foreach (var face in _currentGeometry.Polygons)
            {
                if (face.HasStructureEdit) addedOrRemoved++;
                else if (face.HasHeightEdit || face.HasPositionEdit) reshaped++;
                else if (face.HasTextureEdit || face.HasTextureVisualEdit) repainted++;
            }
        }
        int changedTiles = reshaped + repainted + addedOrRemoved;
        List<string> kinds = [];
        if (reshaped > 0) kinds.Add($"{reshaped:N0} reshaped");
        if (repainted > 0) kinds.Add($"{repainted:N0} repainted");
        if (addedOrRemoved > 0) kinds.Add($"{addedOrRemoved:N0} added or removed");
        bool unsaved = HasUnsavedMobyEdits() || HasUnsavedTerrainEdits();
        string changes = changedObjects == 0 && changedTiles == 0
            ? "You have not changed this level."
            : $"Your changes: {Count(changedObjects, "object")}, {Count(changedTiles, "terrain tile")}" +
              (kinds.Count > 0 ? $" ({string.Join(", ", kinds)})" : "") + ".";
        return $"{Count(tiles, "terrain tile")}, {Count(objects, "object")}" +
               (entryMarkers > 0 ? ", plus the spot where Spyro arrives" : "") + ".\n" +
               changes +
               (changedObjects == 0 && changedTiles == 0 ? "" : unsaved ? "\nSome changes are not saved yet." : "\nAll changes are saved.");
    }

    /// <summary>The line under the level name.</summary>
    private string BuildPlainTreasureLine()
    {
        var treasure = _currentMobys.Where(moby => !moby.IsRemoved && moby.TreasureValue > 0).ToList();
        int value = treasure.Sum(moby => moby.TreasureValue);
        int added = treasure.Count(moby => moby.IsAdded);
        return $"Treasure in this level: {value:N0}" + (added > 0 ? $"  ·  {Count(added, "pickup")} added by you" : "");
    }

    /// <summary>The line above the object list.</summary>
    private static string BuildPlainObjectListLine(int objects, int shown, int total, int changed, bool filtered) =>
        (filtered ? $"Showing {shown:N0} of {total:N0}" : Count(objects, "object")) +
        (changed > 0 ? $"  ·  {changed:N0} changed" : "");
    /// <summary>The short note under the selected object's buttons, in everyday words.</summary>
    private string BuildPlainObjectActionHint(bool hasLevel, bool hasObject, bool objectHasEdits)
    {
        if (!hasLevel)
            return "Open a level to see its objects.";
        if (_pendingMobyAdd != null)
            return $"Click in the view where the new {_pendingMobyAdd.Label} should go. Press Esc to cancel.";
        if (!hasObject)
            return _lastRemovedMoby == null
                ? "Click an object in the view, or pick one from the list below."
                : $"Removed {PlainObjectName(_lastRemovedMoby)}. Undo last removal brings it back.";
        Moby moby = _selectedMoby!;
        string name = PlainObjectName(moby);
        string changed = objectHasEdits ? " Changed from the original; Undo puts it back." : "";
        if (moby.IsFlyInLandingControl)
            return "This is where Spyro lands when he flies into the level. His approach path moves with it." + changed;
        if (IsMappedPortalControlMoby(moby))
            return "Portal. Its lettering, travel path and walk-in surface move together with it." + changed;
        if (IsReleaseProtectedControlMoby(moby))
            return "This is a hidden helper the game needs. It can be moved, but not copied or removed." + changed;
        if (IsCrossLevelExistingSlotSwapEdit(moby))
            return $"{name} was swapped in with Replace. Undo puts the original back.";
        if (objectHasEdits)
            return $"{name}: changed from the original. Undo puts it back.";
        if (!moby.IsAdded && moby.TrueIndex >= 0 &&
            CrossLevelSwapCatalogBuilder.TryGetEligibleTargetCategory(moby, out CrossLevelSwapCategory category, out _))
            return $"{name} selected. Replace… can swap it for a different {(category == CrossLevelSwapCategory.Enemy ? "enemy" : "chest")}.";
        return $"{name} selected. Drag it in the view to move it.";
    }

    private static string PlainObjectName(Moby moby) =>
        string.IsNullOrWhiteSpace(moby.DisplayLabel) ? "the object" : moby.DisplayLabel;
    /// <summary>Export check results as an answer to "can I create the BIN?".</summary>
    private static string PlainExportStatus(Spyro.Editor.Core.Exporting.MobyBuildSafetyStatus status) => status switch
    {
        Spyro.Editor.Core.Exporting.MobyBuildSafetyStatus.Stable => "Ready to export",
        Spyro.Editor.Core.Exporting.MobyBuildSafetyStatus.Review => "Worth checking first",
        _ => "Can't export yet"
    };
}

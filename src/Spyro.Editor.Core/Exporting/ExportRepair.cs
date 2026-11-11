namespace Spyro.Editor.Core.Exporting;

/// <summary>Something Create BIN refused that the editor can put right by itself: the status bar's Fix It For Me. A
/// refusal carries its repair with it (<see cref="Refusal"/>) and still reads like any other: its message says what to do
/// by hand.</summary>
public abstract record ExportRepair
{
    /// <summary>The new world needs its way in standing in <paramref name="HomeworldKey"/> (none is placed, or it stands in
    /// another homeworld): the editor places one there on open ground (<see cref="WorldEntranceSpots"/>).</summary>
    public sealed record PlaceWorldEntrance(string HomeworldKey) : ExportRepair;

    /// <summary>The new world's homeworld has more objects than it has save rows for (<paramref name="Room"/>): the
    /// editor gives back the rows of deleted objects and takes out loose gems, the least valuable first
    /// (<see cref="Editing.SceneDocument.FitSaveRows"/>).</summary>
    public sealed record FitHomeworldObjects(int Room) : ExportRepair;

    private const string Key = "SpyroEditor.ExportRepair";

    /// <summary>The refusal to throw: an <see cref="InvalidDataException"/> like Create BIN's others, with its repair.</summary>
    public static InvalidDataException Refusal(string message, ExportRepair repair)
    {
        var refusal = new InvalidDataException(message);
        refusal.Data[Key] = repair;
        return refusal;
    }

    /// <summary>The repair of <paramref name="failure"/> or of what caused it (null: nothing the editor can do itself).</summary>
    public static ExportRepair? Find(Exception? failure)
    {
        for (var e = failure; e != null; e = e.InnerException)
        {
            if (e.Data[Key] is ExportRepair repair) return repair;
            if (e is AggregateException many)
                foreach (var inner in many.InnerExceptions)
                    if (Find(inner) is { } found) return found;
        }
        return null;
    }
}

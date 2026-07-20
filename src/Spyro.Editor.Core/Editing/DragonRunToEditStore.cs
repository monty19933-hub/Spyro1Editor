using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Editing;

public sealed class NativeDragonRunToEdit
{
    public NativeDragonRunToEdit(string levelKey, DragonRescueCameraData scene)
    {
        LevelKey = LevelCatalog.NormalizeKey(levelKey);
        Scene = scene;
        OriginalRawX = RawX = scene.RunToRawX;
        OriginalRawY = RawY = scene.RunToRawY;
    }

    public string LevelKey { get; }
    public DragonRescueCameraData Scene { get; }
    public int OwnerTrueIndex => Scene.DragonTrueIndex;
    public string StableId => $"dragon-run-to:T{OwnerTrueIndex}";
    public int OriginalRawX { get; }
    public int OriginalRawY { get; }
    public int RawX { get; private set; }
    public int RawY { get; private set; }
    public double EditorX => RawX / 16d;
    public double EditorY => RawY / 16d;
    public bool HasEdit => RawX != OriginalRawX || RawY != OriginalRawY;

    public void SetRawEndpoint(int rawX, int rawY)
    {
        RawX = rawX;
        RawY = rawY;
    }

    public void TranslateRaw(int deltaX, int deltaY) =>
        SetRawEndpoint(checked(RawX + deltaX), checked(RawY + deltaY));

    public void Reset() => SetRawEndpoint(OriginalRawX, OriginalRawY);
}

public sealed record DragonRunToEditLoadResult(
    IReadOnlyList<NativeDragonRunToEdit> Edits,
    int AppliedCount,
    IReadOnlyList<string> BlockedReasons);

/// <summary>
/// Persists native dragon run destinations as synthetic controls in the
/// ordinary per-level Moby manifest. Older manifests simply have no matching
/// rows and therefore retain retail behavior.
/// </summary>
public static class DragonRunToEditStore
{
    public const string ControlPrefix = "dragon-run-to:T";

    public static DragonRunToEditLoadResult LoadFromMobyManifest(
        string manifestPath,
        string levelKey,
        IReadOnlyDictionary<int, DragonRescueCameraData> scenes)
    {
        Dictionary<int, NativeDragonRunToEdit> byOwner = scenes.ToDictionary(
            pair => pair.Key,
            pair => new NativeDragonRunToEdit(levelKey, pair.Value));
        if (!File.Exists(manifestPath))
            return new DragonRunToEditLoadResult(byOwner.Values.OrderBy(edit => edit.OwnerTrueIndex).ToArray(), 0, []);

        using FileStream stream = File.OpenRead(manifestPath);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("edits", out JsonElement edits) || edits.ValueKind != JsonValueKind.Array)
            return new DragonRunToEditLoadResult(byOwner.Values.OrderBy(edit => edit.OwnerTrueIndex).ToArray(), 0, []);

        List<string> blocked = [];
        int applied = 0;
        foreach (JsonElement row in edits.EnumerateArray())
        {
            string controlKind = ReadString(row, "editorControlKind");
            if (!controlKind.StartsWith(ControlPrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            int ownerTrueIndex = ReadInt32(row, "ownerTrueIndex", ParseOwner(controlKind));
            if (!byOwner.TryGetValue(ownerTrueIndex, out NativeDragonRunToEdit? edit))
            {
                blocked.Add($"{controlKind}: the native dragon scene is not present in the selected disc.");
                continue;
            }

            string? mismatch = ValidatePreimage(row, edit);
            if (mismatch != null)
            {
                blocked.Add($"{controlKind}: {mismatch}");
                continue;
            }
            if (!TryReadRawPoint(row, "rawEdited", out int x, out int y))
                continue;
            edit.SetRawEndpoint(x, y);
            applied++;
        }

        return new DragonRunToEditLoadResult(
            byOwner.Values.OrderBy(edit => edit.OwnerTrueIndex).ToArray(),
            applied,
            blocked);
    }

    public static async Task<int> MergeIntoMobyManifestAsync(
        string manifestPath,
        IEnumerable<NativeDragonRunToEdit> runToEdits,
        CancellationToken cancellationToken = default)
    {
        JsonObject root;
        if (File.Exists(manifestPath))
        {
            root = JsonNode.Parse(await File.ReadAllTextAsync(manifestPath, cancellationToken)) as JsonObject
                ?? throw new InvalidOperationException("The native Moby edit manifest root is invalid.");
        }
        else
        {
            root = new JsonObject
            {
                ["generatedAt"] = DateTime.Now.ToString("s"),
                ["editor"] = "Spyro.Editor.Core",
                ["levelName"] = "Unknown",
                ["note"] = "Cross-platform editor native object and synthetic-control manifest."
            };
        }

        JsonArray rows = root["edits"] as JsonArray ?? new JsonArray();
        root["edits"] = rows;
        for (int index = rows.Count - 1; index >= 0; index--)
        {
            if (rows[index] is JsonObject row &&
                (row["editorControlKind"]?.GetValue<string>() ?? "")
                    .StartsWith(ControlPrefix, StringComparison.OrdinalIgnoreCase))
            {
                rows.RemoveAt(index);
            }
        }

        NativeDragonRunToEdit[] edited = runToEdits
            .Where(edit => edit.HasEdit)
            .OrderBy(edit => edit.OwnerTrueIndex)
            .ToArray();
        foreach (NativeDragonRunToEdit edit in edited)
            rows.Add(BuildRow(edit));

        root["editCount"] = rows.Count;
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath) ?? ".");
        await File.WriteAllTextAsync(
            manifestPath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        return edited.Length;
    }

    private static JsonObject BuildRow(NativeDragonRunToEdit edit) => new()
    {
        ["editKind"] = "synthetic-control",
        ["editorControlKind"] = edit.StableId,
        ["ownerTrueIndex"] = edit.OwnerTrueIndex,
        ["label"] = "Spyro runs here",
        ["levelKey"] = edit.LevelKey,
        ["cameraDataWadOffset"] = $"0x{edit.Scene.CameraDataWadOffset:X}",
        ["originalAngle"] = edit.Scene.RunToAngle,
        ["originalRadius"] = edit.Scene.RunToRadius,
        ["preservedAuxiliary"] = edit.Scene.RunToAuxiliary,
        ["rawOriginal"] = NewRawPoint(edit.OriginalRawX, edit.OriginalRawY),
        ["rawEdited"] = NewRawPoint(edit.RawX, edit.RawY)
    };

    private static JsonObject NewRawPoint(int x, int y) => new()
    {
        ["x"] = x,
        ["y"] = y
    };

    private static string? ValidatePreimage(JsonElement row, NativeDragonRunToEdit edit)
    {
        if (!string.Equals(LevelCatalog.NormalizeKey(ReadString(row, "levelKey")), edit.LevelKey, StringComparison.Ordinal))
            return "level identity no longer matches.";
        if (ReadInt64(row, "cameraDataWadOffset", -1) != edit.Scene.CameraDataWadOffset)
            return "packed scene address no longer matches.";
        if (ReadInt32(row, "originalAngle", int.MinValue) != edit.Scene.RunToAngle ||
            ReadInt32(row, "originalRadius", int.MinValue) != edit.Scene.RunToRadius ||
            ReadInt32(row, "preservedAuxiliary", int.MinValue) != edit.Scene.RunToAuxiliary)
        {
            return "packed angle/radius/auxiliary preimage no longer matches.";
        }
        if (!TryReadRawPoint(row, "rawOriginal", out int x, out int y) ||
            x != edit.OriginalRawX || y != edit.OriginalRawY)
        {
            return "original endpoint no longer matches.";
        }
        return null;
    }

    private static bool TryReadRawPoint(JsonElement row, string name, out int x, out int y)
    {
        x = y = 0;
        if (!row.TryGetProperty(name, out JsonElement point) || point.ValueKind != JsonValueKind.Object)
            return false;
        x = ReadInt32(point, "x", int.MinValue);
        y = ReadInt32(point, "y", int.MinValue);
        return x != int.MinValue && y != int.MinValue;
    }

    private static int ParseOwner(string controlKind) =>
        int.TryParse(controlKind.AsSpan(ControlPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : -1;

    private static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement property)
            ? property.ValueKind == JsonValueKind.String ? property.GetString() ?? "" : property.ToString()
            : "";

    private static int ReadInt32(JsonElement element, string name, int fallback)
    {
        if (!element.TryGetProperty(name, out JsonElement property))
            return fallback;
        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int number))
            return number;
        return int.TryParse(property.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;
    }

    private static long ReadInt64(JsonElement element, string name, long fallback)
    {
        string text = ReadString(element, name).Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hex))
        {
            return hex;
        }
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
            ? value
            : fallback;
    }
}

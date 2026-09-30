using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Universal;

/// <summary>Reads universal Moby placements saved in a level's native-edits.json.</summary>
public static class UniversalPlacementReader
{
    public static bool IsUniversalEdit(JsonElement edit)
    {
        if (!edit.TryGetProperty("crossLevelTemplate", out var t) || t.ValueKind != JsonValueKind.Object) return false;
        return t.TryGetProperty("requiredExporterFeature", out var f) && f.ValueKind == JsonValueKind.String &&
            f.GetString() == UniversalMobyExport.RequiredExporterFeature;
    }

    public static string[] SplitSignText(string? text) =>
        (text ?? "").Replace("\r", "").Split('\n').Take(UniversalSpecialBehaviors.SignLines).ToArray();

    /// <summary>Native rows (true indices) the user removed in this level.</summary>
    public static List<int> ReadRemovedNativeRows(string nativeEditsPath)
    {
        List<int> rows = [];
        if (!File.Exists(nativeEditsPath)) return rows;
        using var doc = JsonDocument.Parse(File.ReadAllBytes(nativeEditsPath));
        if (!doc.RootElement.TryGetProperty("edits", out var edits) || edits.ValueKind != JsonValueKind.Array) return rows;
        foreach (var edit in edits.EnumerateArray())
        {
            bool added = edit.TryGetProperty("added", out var a) && a.ValueKind == JsonValueKind.True;
            bool removed = edit.TryGetProperty("removed", out var r) && r.ValueKind == JsonValueKind.True;
            if (added || !removed) continue;
            if (edit.TryGetProperty("trueIndex", out var ti) && ti.ValueKind == JsonValueKind.Number) rows.Add(ti.GetInt32());
        }
        return rows;
    }

    public static List<UniversalMobyPlacement> Read(string nativeEditsPath)
    {
        List<UniversalMobyPlacement> list = [];
        if (!File.Exists(nativeEditsPath)) return list;
        using var doc = JsonDocument.Parse(File.ReadAllBytes(nativeEditsPath));
        if (!doc.RootElement.TryGetProperty("edits", out var edits) || edits.ValueKind != JsonValueKind.Array) return list;
        var ledger = UniversalPlacementLedger.Read(doc.RootElement);
        Dictionary<string, UniversalMobyPlacement> active = new(StringComparer.Ordinal);
        var slots = ledger.Entries.ToDictionary(e => e.Id, e => e.Slot, StringComparer.Ordinal);
        foreach (var edit in edits.EnumerateArray())
        {
            if (!IsUniversalEdit(edit)) continue;
            bool added = edit.TryGetProperty("added", out var a) && a.ValueKind == JsonValueKind.True;
            bool removed = edit.TryGetProperty("removed", out var r) && r.ValueKind == JsonValueKind.True;
            if (!added || removed) continue;
            var t = edit.GetProperty("crossLevelTemplate");
            string templateId = t.TryGetProperty("id", out var tid) && tid.ValueKind == JsonValueKind.String ? tid.GetString() ?? "" : "";
            string? special = templateId.StartsWith(UniversalSpecialCatalog.TemplatePrefix, StringComparison.Ordinal)
                ? templateId[UniversalSpecialCatalog.TemplatePrefix.Length..] : null;
            string src = special != null ? UniversalSpecialCatalog.SourceKey : LevelCatalog.NormalizeKey(t.GetProperty("sourceLevelKey").GetString() ?? "");
            int row = t.TryGetProperty("sourceTrueIndex", out var sti) && sti.ValueKind == JsonValueKind.Number ? sti.GetInt32() : -1;
            string[]? text = edit.TryGetProperty("signText", out var st2) && st2.ValueKind == JsonValueKind.String
                ? SplitSignText(st2.GetString()) : null;
            var raw = edit.GetProperty("rawEdited");
            int x = raw.GetProperty("x").GetInt32(), y = raw.GetProperty("y").GetInt32(), z = raw.GetProperty("z").GetInt32();
            int? yaw = null;
            if (edit.TryGetProperty("yawByteEditedHex", out var yh) && yh.ValueKind == JsonValueKind.String)
                yaw = Convert.ToInt32(yh.GetString(), 16);
            Dictionary<int, byte> overrides = [];
            if (edit.TryGetProperty("stateEditedHex", out var st) && st.ValueKind == JsonValueKind.String) overrides[0x51] = (byte)Convert.ToInt32(st.GetString(), 16);
            if (edit.TryGetProperty("typeEditedHex", out var ty) && ty.ValueKind == JsonValueKind.String) overrides[0x50] = (byte)Convert.ToInt32(ty.GetString(), 16);
            string id = UniversalPlacementLedger.IdentityOf(edit);
            if (!slots.TryGetValue(id, out int slot) || !active.TryAdd(id, new(src, row, x, y, z, yaw, overrides, id, special, text, PersistentSlot: slot)))
                throw new InvalidDataException("An imported object has a missing or duplicate placement identity; export stopped to preserve collected state.");
        }
        foreach (var entry in ledger.Entries)
            list.Add(active.TryGetValue(entry.Id, out var placement) ? placement :
                new("", -1, 0, 0, 0, EditorId: entry.Id, PersistentSlot: entry.Slot, Retired: true));
        return list;
    }
}

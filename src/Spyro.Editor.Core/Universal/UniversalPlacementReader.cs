using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

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

    /// <summary>An added object's saved route ("universalPathNodes": [[x, y, z], …] in raw units), or null.</summary>
    public static List<int[]>? ReadPathNodes(JsonElement edit)
    {
        if (!edit.TryGetProperty("universalPathNodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array) return null;
        List<int[]> list = [];
        foreach (var n in nodes.EnumerateArray())
        {
            if (n.ValueKind != JsonValueKind.Array || n.GetArrayLength() != 3) throw new InvalidDataException("A saved route node is not x, y, z.");
            list.Add([n[0].GetInt32(), n[1].GetInt32(), n[2].GetInt32()]);
        }
        return list.Count is > 0 and <= 64 ? list : throw new InvalidDataException("A saved route has no nodes or too many.");
    }

    /// <summary>What an added object gives when that was chosen (row +0x53; <see cref="Moby.UniversalRewardByte"/>):
    /// saved as "universalRewardByteHex" so Create BIN writes it into the object's row in the game's own levels too.</summary>
    public const string RewardByteField = "universalRewardByteHex";

    public static int? ReadRewardByte(JsonElement edit)
    {
        if (!edit.TryGetProperty(RewardByteField, out var saved) || saved.ValueKind != JsonValueKind.String) return null;
        int value = Convert.ToInt32(saved.GetString(), 16);
        return value is >= 0 and <= 255 ? value : throw new InvalidDataException("A saved reward is not one byte.");
    }

    /// <summary>What kind of added object an edit is, for leaving a kind out of the game (Fix It For Me): its
    /// template's id ("special:crash-frog", a level's own template id), or the source level and row when it has none.</summary>
    public static string KindOf(JsonElement edit)
    {
        var t = edit.GetProperty("crossLevelTemplate");
        string id = t.TryGetProperty("id", out var tid) && tid.ValueKind == JsonValueKind.String ? tid.GetString() ?? "" : "";
        if (id.Length > 0) return id;
        string source = t.TryGetProperty("sourceLevelKey", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() ?? "" : "";
        int row = t.TryGetProperty("sourceTrueIndex", out var r) && r.ValueKind == JsonValueKind.Number ? r.GetInt32() : -1;
        return $"{LevelCatalog.NormalizeKey(source)}#{row}";
    }

    /// <summary>The kinds of object added to the level (<see cref="KindOf"/>), each with the name the editor shows for it
    /// and how many were added, in the order they were first added.</summary>
    public static List<(string Kind, string Label, int Count)> ReadAddedKinds(string nativeEditsPath)
    {
        List<(string Kind, string Label, int Count)> kinds = [];
        if (!File.Exists(nativeEditsPath)) return kinds;
        using var doc = JsonDocument.Parse(File.ReadAllBytes(nativeEditsPath));
        if (!doc.RootElement.TryGetProperty("edits", out var edits) || edits.ValueKind != JsonValueKind.Array) return kinds;
        foreach (var edit in edits.EnumerateArray())
        {
            if (!IsUniversalEdit(edit)) continue;
            bool added = edit.TryGetProperty("added", out var a) && a.ValueKind == JsonValueKind.True;
            bool removed = edit.TryGetProperty("removed", out var r) && r.ValueKind == JsonValueKind.True;
            if (!added || removed) continue;
            string kind = KindOf(edit);
            string label = edit.TryGetProperty("labelEdited", out var le) && le.ValueKind == JsonValueKind.String && le.GetString() is { Length: > 0 } edited ? edited
                : edit.TryGetProperty("label", out var l) && l.ValueKind == JsonValueKind.String && l.GetString() is { Length: > 0 } plain ? plain : "object";
            int at = kinds.FindIndex(k => k.Kind == kind);
            if (at < 0) kinds.Add((kind, label, 1)); else kinds[at] = (kind, kinds[at].Label, kinds[at].Count + 1);
        }
        return kinds;
    }

    /// <param name="withoutKinds">Kinds of added object (<see cref="KindOf"/>) to read as if they had been deleted (left
    /// out of the game by Fix It For Me): their save rows stay kept for them, as a deleted object's do.</param>
    public static List<UniversalMobyPlacement> Read(string nativeEditsPath, IReadOnlySet<string>? withoutKinds = null)
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
            if (withoutKinds != null && withoutKinds.Contains(KindOf(edit))) continue;
            var t = edit.GetProperty("crossLevelTemplate");
            string templateId = t.TryGetProperty("id", out var tid) && tid.ValueKind == JsonValueKind.String ? tid.GetString() ?? "" : "";
            string? special = templateId.StartsWith(UniversalSpecialCatalog.TemplatePrefix, StringComparison.Ordinal)
                ? templateId[UniversalSpecialCatalog.TemplatePrefix.Length..] : null;
            string src = special != null ? UniversalHighValueGems.IsId(special) ? UniversalHighValueGems.SourceKey : UniversalSpecialCatalog.SourceKey
                : LevelCatalog.NormalizeKey(t.GetProperty("sourceLevelKey").GetString() ?? "");
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
            // A special object's row is its own (the composer sets what it gives).
            if (special == null && ReadRewardByte(edit) is int reward) overrides[UniversalRewardChoice.RowOffset] = (byte)reward;
            string id = UniversalPlacementLedger.IdentityOf(edit);
            string? group = edit.TryGetProperty("universalLinkGroupId", out var lg) && lg.ValueKind == JsonValueKind.String ? lg.GetString() : null;
            if (!slots.TryGetValue(id, out int slot) || !active.TryAdd(id, new(src, row, x, y, z, yaw, overrides, id, special, text, PersistentSlot: slot) { LinkGroupId = group, DragonColor = DragonStatueColor.Read(edit), PathNodes = ReadPathNodes(edit) }))
                throw new InvalidDataException("An imported object has a missing or duplicate placement identity; export stopped to preserve collected state.");
        }
        foreach (var entry in ledger.Entries)
            list.Add(active.TryGetValue(entry.Id, out var placement) ? placement :
                new("", -1, 0, 0, 0, EditorId: entry.Id, PersistentSlot: entry.Slot, Retired: true));
        return list;
    }
}

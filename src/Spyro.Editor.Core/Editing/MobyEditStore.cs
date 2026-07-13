using System.Text.Json;
using System.Globalization;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Editing;

public static class MobyEditStore
{
    public static int Load(string path, IList<Moby> mobys)
    {
        if (!File.Exists(path))
            return 0;

        foreach (Moby moby in mobys)
        {
            moby.Position = moby.OriginalPosition;
            if (moby.OriginalType >= 0)
                moby.SetType(moby.OriginalType);
            if (moby.OriginalState >= 0)
                moby.State = moby.OriginalState;
            if (moby.OriginalYawByte >= 0)
                moby.YawByte = moby.OriginalYawByte;
            if (moby.OriginalSourceByte36 >= 0)
                moby.SourceByte36 = moby.OriginalSourceByte36;
            if (moby.OriginalSourceByte37 >= 0)
                moby.SourceByte37 = moby.OriginalSourceByte37;
            if (moby.OriginalSourceByte4F >= 0)
                moby.SourceByte4F = moby.OriginalSourceByte4F;
            if (moby.OriginalFlag4A >= 0)
                moby.Flag4A = moby.OriginalFlag4A;
            if (moby.OriginalFlag4B >= 0)
                moby.Flag4B = moby.OriginalFlag4B;
            moby.Label = moby.OriginalLabel;
            moby.CrossLevelTemplateId = "";
            moby.CrossLevelFamily = "";
            moby.CrossLevelSourceLevelKey = "";
            moby.CrossLevelSourceLevelName = "";
            moby.CrossLevelSourceTrueIndex = -1;
            moby.CrossLevelRequiredExporterFeature = "";
            moby.SourceCloneLevelKey = "";
            moby.SourceCloneLevelName = "";
            moby.SourceCloneTrueIndex = -1;
            moby.IsRemoved = false;
            moby.HasLoadedNativeEdit = false;
            moby.LoadedNativeEditSummary = "";
        }

        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("edits", out JsonElement editsElement) || editsElement.ValueKind != JsonValueKind.Array)
            return 0;

        Dictionary<int, Moby> byIndex = mobys.ToDictionary(moby => moby.Index);
        Dictionary<int, Moby> byTrueIndex = mobys
            .Where(moby => moby.TrueIndex >= 0)
            .ToDictionary(moby => moby.TrueIndex);
        Dictionary<string, Moby> byEditorControlKind = mobys
            .Where(moby => moby.IsEditorControl)
            .ToDictionary(moby => moby.EditorControlKind, StringComparer.OrdinalIgnoreCase);

        int applied = 0;
        foreach (JsonElement edit in editsElement.EnumerateArray())
        {
            Moby? moby = ResolveMoby(edit, byIndex, byTrueIndex, byEditorControlKind);
            if (moby == null && IsAddEdit(edit))
            {
                moby = CreateAddedMoby(edit, mobys);
                mobys.Add(moby);
                byIndex[moby.Index] = moby;
                if (moby.TrueIndex >= 0)
                    byTrueIndex[moby.TrueIndex] = moby;
            }

            if (moby == null)
                continue;

            bool changed = ApplyPositionEdit(edit, moby);
            changed = ApplyMetadataEdit(edit, moby) || changed;
            if (JsonValue.GetBoolean(edit, "removed"))
            {
                moby.IsRemoved = true;
                changed = true;
            }
            string mutationMode = ReadMutationMode(edit);
            if (!string.IsNullOrWhiteSpace(mutationMode))
                changed = true;

            if (!changed)
                continue;

            if (IsAddEdit(edit))
                moby.IsAdded = true;
            moby.HasLoadedNativeEdit = true;
            moby.LoadedNativeEditSummary = BuildSummary(edit, mutationMode);
            applied++;
        }

        ApplySavedLinkedCompanionGroups(editsElement, byTrueIndex);
        return applied;
    }

    public static async Task<int> SaveAsync(string path, IEnumerable<Moby> mobys, string levelName, CancellationToken cancellationToken = default)
    {
        List<Moby> mobyList = mobys.ToList();
        Dictionary<int, Moby> byTrueIndex = mobyList
            .Where(moby => moby.TrueIndex >= 0)
            .ToDictionary(moby => moby.TrueIndex);
        List<object> edits = new();
        foreach (Moby moby in mobyList)
        {
            if (!moby.HasAnyEdit)
                continue;

            edits.Add(new Dictionary<string, object?>
            {
                ["editKind"] = moby.IsAdded ? "add" : moby.IsRemoved ? "remove" : "update",
                ["index"] = moby.Index,
                ["trueIndex"] = moby.TrueIndex >= 0 ? moby.TrueIndex : null,
                ["legacyIndex"] = moby.LegacyIndex >= 0 ? moby.LegacyIndex : null,
                ["label"] = moby.DisplayLabel,
                ["labelOriginal"] = moby.OriginalLabel,
                ["labelEdited"] = moby.Label,
                ["typeHex"] = $"0x{moby.Type:X2}",
                ["typeOriginalHex"] = moby.OriginalType >= 0 ? $"0x{moby.OriginalType:X2}" : null,
                ["typeEditedHex"] = $"0x{moby.Type:X2}",
                ["stateHex"] = $"0x{moby.State:X2}",
                ["stateOriginalHex"] = moby.OriginalState >= 0 ? $"0x{moby.OriginalState:X2}" : null,
                ["stateEditedHex"] = $"0x{moby.State:X2}",
                ["yawByteHex"] = moby.YawByte >= 0 ? $"0x{moby.YawByte:X2}" : null,
                ["yawByteOriginalHex"] = moby.OriginalYawByte >= 0 ? $"0x{moby.OriginalYawByte:X2}" : null,
                ["yawByteEditedHex"] = moby.YawByte >= 0 ? $"0x{moby.YawByte:X2}" : null,
                ["yawDegrees"] = moby.YawByte >= 0
                    ? Math.Round(
                        moby.IsFlyInLandingControl
                            ? FlyInLandingEditorControl.HeadingByteToDegrees(moby.YawByte)
                            : moby.YawDegrees,
                        4)
                    : null,
                ["removed"] = moby.IsRemoved,
                ["added"] = moby.IsAdded,
                ["runtimeAddress"] = $"0x{moby.RuntimeAddress:X8}",
                ["specialDataPointer"] = $"0x{moby.SpecialDataPointer:X8}",
                ["sourceByte36Hex"] = $"0x{moby.SourceByte36:X2}",
                ["sourceByte36OriginalHex"] = moby.OriginalSourceByte36 >= 0 ? $"0x{moby.OriginalSourceByte36:X2}" : null,
                ["sourceByte36EditedHex"] = $"0x{moby.SourceByte36:X2}",
                ["sourceByte37Hex"] = $"0x{moby.SourceByte37:X2}",
                ["sourceByte37OriginalHex"] = moby.OriginalSourceByte37 >= 0 ? $"0x{moby.OriginalSourceByte37:X2}" : null,
                ["sourceByte37EditedHex"] = $"0x{moby.SourceByte37:X2}",
                ["sourceByte4FHex"] = $"0x{moby.SourceByte4F:X2}",
                ["sourceByte4FOriginalHex"] = moby.OriginalSourceByte4F >= 0 ? $"0x{moby.OriginalSourceByte4F:X2}" : null,
                ["sourceByte4FEditedHex"] = $"0x{moby.SourceByte4F:X2}",
                ["flag4AHex"] = $"0x{moby.Flag4A:X2}",
                ["flag4AOriginalHex"] = moby.OriginalFlag4A >= 0 ? $"0x{moby.OriginalFlag4A:X2}" : null,
                ["flag4AEditedHex"] = $"0x{moby.Flag4A:X2}",
                ["flag4BHex"] = $"0x{moby.Flag4B:X2}",
                ["flag4BOriginalHex"] = moby.OriginalFlag4B >= 0 ? $"0x{moby.OriginalFlag4B:X2}" : null,
                ["flag4BEditedHex"] = $"0x{moby.Flag4B:X2}",
                ["patchStatus"] = moby.PatchStatus,
                ["patchLead"] = moby.PatchLead,
                ["editorControlKind"] = moby.IsEditorControl ? moby.EditorControlKind : null,
                ["crossLevelTemplate"] = NewCrossLevelTemplate(moby),
                ["recordMutation"] = NewRecordMutation(moby),
                ["gem"] = moby.IsGemLike ? NewGem(moby) : null,
                ["sourceByteEdits"] = NewSourceByteEdits(moby),
                ["linkedCompanionLinks"] = NewLinkedCompanionLinks(moby, byTrueIndex),
                ["chestContentLinkEdit"] = NewChestContentLinkEdit(moby, byTrueIndex),
                ["original"] = NewVector(moby.OriginalPosition),
                ["edited"] = NewVector(moby.Position),
                ["rawOriginal"] = NewRawVector(moby.OriginalPosition),
                ["rawEdited"] = NewRawVector(moby.Position),
                ["rawDelta"] = NewRawDelta(moby)
            });
        }

        var root = new
        {
            generatedAt = DateTime.Now.ToString("s"),
            editor = "Spyro.Editor.Core",
            levelName = string.IsNullOrWhiteSpace(levelName) ? "Unknown" : levelName,
            note = "Cross-platform editor moby records using the legacy NativeSpyroEditor edit manifest shape.",
            editCount = edits.Count,
            edits
        };

        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, root, cancellationToken: cancellationToken);
        return edits.Count;
    }

    private static Moby? ResolveMoby(
        JsonElement edit,
        IReadOnlyDictionary<int, Moby> byIndex,
        IReadOnlyDictionary<int, Moby> byTrueIndex,
        IReadOnlyDictionary<string, Moby> byEditorControlKind)
    {
        string editorControlKind = JsonValue.GetString(edit, "editorControlKind");
        if (!string.IsNullOrWhiteSpace(editorControlKind) &&
            byEditorControlKind.TryGetValue(editorControlKind, out Moby? editorControl))
        {
            return editorControl;
        }

        int trueIndex = JsonValue.GetInt32(edit, "trueIndex", -1);
        if (trueIndex >= 0 && byTrueIndex.TryGetValue(trueIndex, out Moby? byTrue))
            return byTrue;

        int legacyIndex = JsonValue.GetInt32(edit, "legacyIndex", -1);
        if (legacyIndex >= 0 && MobyLoader.TryGetLoaderTableTrueIndex(legacyIndex, out int mappedTrueIndex) && byTrueIndex.TryGetValue(mappedTrueIndex, out Moby? byLegacy))
            return byLegacy;

        int index = JsonValue.GetInt32(edit, "index", -1);
        if (index >= 0)
        {
            if (MobyLoader.TryGetLoaderTableTrueIndex(index, out int mappedIndex) && byTrueIndex.TryGetValue(mappedIndex, out Moby? mapped))
                return mapped;

            if (byIndex.TryGetValue(index, out Moby? byPlainIndex))
                return byPlainIndex;
        }

        return null;
    }

    private static bool IsAddEdit(JsonElement edit)
    {
        return JsonValue.GetBoolean(edit, "added")
            || string.Equals(JsonValue.GetString(edit, "editKind"), "add", StringComparison.OrdinalIgnoreCase);
    }

    private static Moby CreateAddedMoby(JsonElement edit, IList<Moby> mobys)
    {
        int index = JsonValue.GetInt32(edit, "index", NextIndex(mobys));
        int trueIndex = JsonValue.GetInt32(edit, "trueIndex", NextTrueIndex(mobys));
        int type = Math.Clamp(JsonValue.GetInt32(edit, "typeEditedHex", JsonValue.GetInt32(edit, "typeHex", 0x20)), 0, 255);
        int state = Math.Clamp(JsonValue.GetInt32(edit, "stateEditedHex", JsonValue.GetInt32(edit, "stateHex", 0)), 0, 255);
        int yawByte = ReadYawByte(edit, 0);
        int sourceByte36 = Math.Clamp(JsonValue.GetInt32(edit, "sourceByte36EditedHex", JsonValue.GetInt32(edit, "sourceByte36Hex", 0x53)), 0, 255);
        int sourceByte37 = Math.Clamp(JsonValue.GetInt32(edit, "sourceByte37EditedHex", JsonValue.GetInt32(edit, "sourceByte37Hex", 0)), 0, 255);
        int sourceByte4F = Math.Clamp(JsonValue.GetInt32(edit, "sourceByte4FEditedHex", JsonValue.GetInt32(edit, "sourceByte4FHex", 0x01)), 0, 255);
        int flag4A = Math.Clamp(JsonValue.GetInt32(edit, "flag4AHex", 0), 0, 255);
        int flag4B = Math.Clamp(JsonValue.GetInt32(edit, "flag4BEditedHex", JsonValue.GetInt32(edit, "flag4BHex", 0x53)), 0, 255);
        Vector3f original = ReadVector(edit, "original", ReadVector(edit, "edited", new Vector3f(0, 0, 0)));
        string label = JsonValue.GetString(edit, "labelEdited", JsonValue.GetString(edit, "label", Moby.FallbackLabel(type)));
        if (string.IsNullOrWhiteSpace(label))
            label = Moby.FallbackLabel(type);
        JsonElement? crossLevelTemplate = ReadCrossLevelTemplate(edit);

        Moby added = new()
        {
            Index = index,
            TrueIndex = trueIndex,
            LegacyIndex = JsonValue.GetInt32(edit, "legacyIndex", MobyLoader.GetLegacyAliasIndex(trueIndex)),
            Position = original,
            OriginalPosition = original,
            Type = type,
            OriginalType = type,
            State = state,
            OriginalState = state,
            YawByte = yawByte,
            OriginalYawByte = yawByte,
            RuntimeAddress = ReadUInt32(edit, "runtimeAddress"),
            SpecialDataPointer = ReadUInt32(edit, "specialDataPointer"),
            SourceByte36 = sourceByte36,
            OriginalSourceByte36 = sourceByte36,
            SourceByte37 = sourceByte37,
            OriginalSourceByte37 = sourceByte37,
            SourceByte4F = sourceByte4F,
            OriginalSourceByte4F = sourceByte4F,
            Flag4A = flag4A,
            OriginalFlag4A = flag4A,
            Flag4B = flag4B,
            OriginalFlag4B = flag4B,
            Color = Moby.ColorForType(type),
            Label = label,
            OriginalLabel = label,
            PatchStatus = JsonValue.GetString(edit, "patchStatus", "new-native-editor-object"),
            PatchLead = JsonValue.GetString(edit, "patchLead", "Added in the native editor."),
            CrossLevelTemplateId = crossLevelTemplate is JsonElement crossLevel ? JsonValue.GetString(crossLevel, "id") : "",
            CrossLevelFamily = crossLevelTemplate is JsonElement crossLevelFamily ? JsonValue.GetString(crossLevelFamily, "family") : "",
            CrossLevelSourceLevelKey = crossLevelTemplate is JsonElement crossLevelKey ? JsonValue.GetString(crossLevelKey, "sourceLevelKey") : "",
            CrossLevelSourceLevelName = crossLevelTemplate is JsonElement crossLevelName ? JsonValue.GetString(crossLevelName, "sourceLevelName") : "",
            CrossLevelSourceTrueIndex = crossLevelTemplate is JsonElement crossLevelIndex ? JsonValue.GetInt32(crossLevelIndex, "sourceTrueIndex", -1) : -1,
            CrossLevelRequiredExporterFeature = crossLevelTemplate is JsonElement crossLevelFeature ? JsonValue.GetString(crossLevelFeature, "requiredExporterFeature") : "",
            IsAdded = true
        };
        ApplyGemColor(added);
        return added;
    }

    private static int NextIndex(IEnumerable<Moby> mobys)
    {
        return mobys.Any() ? mobys.Max(moby => moby.Index) + 1 : 0;
    }

    private static int NextTrueIndex(IEnumerable<Moby> mobys)
    {
        return mobys.Any() ? mobys.Max(moby => moby.TrueIndex) + 1 : 0;
    }

    private static Vector3f ReadVector(JsonElement edit, string name, Vector3f fallback)
    {
        if (!edit.TryGetProperty(name, out JsonElement vector) || vector.ValueKind != JsonValueKind.Object)
            return fallback;

        return new Vector3f(
            JsonValue.GetSingle(vector, "x", fallback.X),
            JsonValue.GetSingle(vector, "y", fallback.Y),
            JsonValue.GetSingle(vector, "z", fallback.Z));
    }

    private static uint ReadUInt32(JsonElement edit, string name)
    {
        string text = JsonValue.GetString(edit, name, "0");
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && uint.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hex))
            return hex;

        return uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed) ? parsed : 0;
    }

    private static int ReadYawByte(JsonElement edit, int fallback)
    {
        int yawByte = JsonValue.GetInt32(
            edit,
            "yawByteEditedHex",
            JsonValue.GetInt32(
                edit,
                "yawByteHex",
                JsonValue.GetInt32(edit, "facingByteHex", -1)));
        if (yawByte >= 0)
            return Math.Clamp(yawByte, 0, 255);

        float degrees = JsonValue.GetSingle(
            edit,
            "yawDegreesEdited",
            JsonValue.GetSingle(edit, "yawDegrees", float.NaN));
        return float.IsNaN(degrees) ? fallback : Moby.DegreesToYawByte(degrees);
    }

    private static bool ApplyPositionEdit(JsonElement edit, Moby moby)
    {
        if (!edit.TryGetProperty("edited", out JsonElement edited) || edited.ValueKind != JsonValueKind.Object)
            return false;

        moby.Position = new Vector3f(
            JsonValue.GetSingle(edited, "x", moby.Position.X),
            JsonValue.GetSingle(edited, "y", moby.Position.Y),
            JsonValue.GetSingle(edited, "z", moby.Position.Z));
        return true;
    }

    private static bool ApplyMetadataEdit(JsonElement edit, Moby moby)
    {
        bool changed = false;
        int type = JsonValue.GetInt32(edit, "typeEditedHex", JsonValue.GetInt32(edit, "typeHex", -1));
        if (type >= 0)
        {
            moby.SetType(type);
            changed = true;
        }

        int state = JsonValue.GetInt32(edit, "stateEditedHex", JsonValue.GetInt32(edit, "stateHex", -1));
        if (state >= 0)
        {
            moby.State = Math.Clamp(state, 0, 255);
            changed = true;
        }

        int yawByte = ReadYawByte(edit, -1);
        if (yawByte >= 0)
        {
            moby.YawByte = yawByte;
            changed = true;
        }

        int sourceByte36 = JsonValue.GetInt32(edit, "sourceByte36EditedHex", -1);
        if (sourceByte36 >= 0)
        {
            moby.SourceByte36 = Math.Clamp(sourceByte36, 0, 255);
            changed = true;
        }

        int sourceByte37 = JsonValue.GetInt32(edit, "sourceByte37EditedHex", -1);
        if (sourceByte37 >= 0)
        {
            moby.SourceByte37 = Math.Clamp(sourceByte37, 0, 255);
            changed = true;
        }

        int sourceByte4F = JsonValue.GetInt32(edit, "sourceByte4FEditedHex", -1);
        if (sourceByte4F >= 0)
        {
            moby.SourceByte4F = Math.Clamp(sourceByte4F, 0, 255);
            changed = true;
        }

        int flag4A = JsonValue.GetInt32(edit, "flag4AEditedHex", -1);
        if (flag4A >= 0)
        {
            moby.Flag4A = Math.Clamp(flag4A, 0, 255);
            changed = true;
        }

        int flag4B = JsonValue.GetInt32(edit, "flag4BEditedHex", -1);
        if (flag4B >= 0)
        {
            moby.Flag4B = Math.Clamp(flag4B, 0, 255);
            changed = true;
        }

        string label = JsonValue.GetString(edit, "labelEdited", JsonValue.GetString(edit, "label"));
        if (!string.IsNullOrWhiteSpace(label))
        {
            moby.Label = label.Equals("Portal pad trigger marker", StringComparison.OrdinalIgnoreCase) &&
                moby.SourceByte36 == 0x8E && moby.Flag4A == 0x10 && moby.Flag4B == 0xFF
                ? "Portal travel path marker"
                : label;
            changed = true;
        }

        ApplyGemColor(moby);
        ApplyRecordMutation(edit, moby);
        if (ApplyCrossLevelTemplate(edit, moby))
            changed = true;
        if (moby.SourceCloneTrueIndex >= 0)
            changed = true;

        return changed;
    }

    private static void ApplyRecordMutation(JsonElement edit, Moby moby)
    {
        if (!edit.TryGetProperty("recordMutation", out JsonElement mutation) || mutation.ValueKind != JsonValueKind.Object)
            return;

        string mode = JsonValue.GetString(mutation, "mode");
        if (!string.Equals(mode, "cloneSourceRecordIntoSlot", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(mode, "appendSourceRecordClone", StringComparison.OrdinalIgnoreCase))
            return;

        moby.SourceCloneLevelKey = JsonValue.GetString(mutation, "sourceLevelKey");
        moby.SourceCloneLevelName = JsonValue.GetString(mutation, "sourceLevelName");
        moby.SourceCloneTrueIndex = JsonValue.GetInt32(mutation, "sourceTrueIndex", -1);
    }

    private static bool ApplyCrossLevelTemplate(JsonElement edit, Moby moby)
    {
        JsonElement? crossLevelTemplate = ReadCrossLevelTemplate(edit);
        if (crossLevelTemplate is not JsonElement crossLevel)
            return false;

        moby.CrossLevelTemplateId = JsonValue.GetString(crossLevel, "id");
        moby.CrossLevelFamily = JsonValue.GetString(crossLevel, "family");
        moby.CrossLevelSourceLevelKey = JsonValue.GetString(crossLevel, "sourceLevelKey");
        moby.CrossLevelSourceLevelName = JsonValue.GetString(crossLevel, "sourceLevelName");
        moby.CrossLevelSourceTrueIndex = JsonValue.GetInt32(crossLevel, "sourceTrueIndex", -1);
        moby.CrossLevelRequiredExporterFeature = JsonValue.GetString(crossLevel, "requiredExporterFeature");
        return true;
    }

    private static void ApplyGemColor(Moby moby)
    {
        GemValue gem = moby.Gem;
        if (gem != GemValue.Unknown)
            moby.Color = gem.Color;
    }

    private static string ReadMutationMode(JsonElement edit)
    {
        if (!edit.TryGetProperty("recordMutation", out JsonElement mutation) || mutation.ValueKind != JsonValueKind.Object)
            return "";

        return JsonValue.GetString(mutation, "mode");
    }

    private static string BuildSummary(JsonElement edit, string mutationMode)
    {
        List<string> parts = new();
        if (edit.TryGetProperty("edited", out JsonElement edited) && edited.ValueKind == JsonValueKind.Object)
        {
            parts.Add($"XYZ {JsonValue.GetSingle(edited, "x"):0.0}, {JsonValue.GetSingle(edited, "y"):0.0}, {JsonValue.GetSingle(edited, "z"):0.0}");
        }

        if (!string.IsNullOrWhiteSpace(mutationMode))
            parts.Add($"mutation {mutationMode}");

        int yawByte = ReadYawByte(edit, -1);
        if (yawByte >= 0)
        {
            bool isFlyInLanding = string.Equals(
                JsonValue.GetString(edit, "editorControlKind"),
                Moby.FlyInLandingControlKind,
                StringComparison.OrdinalIgnoreCase);
            parts.Add(isFlyInLanding
                ? $"fly-in heading {FlyInLandingEditorControl.HeadingByteToDegrees(yawByte):0.#} deg"
                : $"yaw {Moby.YawByteToDegrees(yawByte):0.#} deg");
        }

        string editKind = JsonValue.GetString(edit, "editKind");
        if (!string.IsNullOrWhiteSpace(editKind))
            parts.Add(editKind);

        if (edit.TryGetProperty("sourceByteEdits", out JsonElement sourceByteEdits) && sourceByteEdits.ValueKind == JsonValueKind.Array)
            parts.Add($"{sourceByteEdits.GetArrayLength()} source byte edit(s)");

        return parts.Count == 0 ? "Loaded legacy edit" : string.Join("; ", parts);
    }

    private static object NewVector(Vector3f vector)
    {
        return new
        {
            x = Math.Round(vector.X, 4),
            y = Math.Round(vector.Y, 4),
            z = Math.Round(vector.Z, 4)
        };
    }

    private static object NewRawVector(Vector3f vector)
    {
        return new
        {
            x = ToRawCoordinate(vector.X),
            y = ToRawCoordinate(vector.Y),
            z = ToRawCoordinate(vector.Z)
        };
    }

    private static object NewRawDelta(Moby moby)
    {
        return new
        {
            x = ToRawCoordinate(moby.Position.X) - ToRawCoordinate(moby.OriginalPosition.X),
            y = ToRawCoordinate(moby.Position.Y) - ToRawCoordinate(moby.OriginalPosition.Y),
            z = ToRawCoordinate(moby.Position.Z) - ToRawCoordinate(moby.OriginalPosition.Z)
        };
    }

    private static object NewGem(Moby moby)
    {
        GemValue gem = moby.Gem;
        return new
        {
            color = gem.Name,
            value = gem.Value,
            idByteHex = gem.IdByte >= 0 ? $"0x{gem.IdByte:X2}" : null,
            valueByteHex = gem.ValueByte >= 0 ? $"0x{gem.ValueByte:X2}" : null,
            mode = moby.IsChestContent ? "chest-content-marker" : "standalone-gem"
        };
    }

    private static object? NewCrossLevelTemplate(Moby moby)
    {
        if (string.IsNullOrWhiteSpace(moby.CrossLevelTemplateId))
            return null;

        return new
        {
            id = moby.CrossLevelTemplateId,
            family = moby.CrossLevelFamily,
            sourceLevelKey = moby.CrossLevelSourceLevelKey,
            sourceLevelName = moby.CrossLevelSourceLevelName,
            sourceTrueIndex = moby.CrossLevelSourceTrueIndex >= 0 ? moby.CrossLevelSourceTrueIndex : null as int?,
            addSupportStatus = moby.PatchStatus,
            requiredExporterFeature = moby.CrossLevelRequiredExporterFeature
        };
    }

    private static object? NewRecordMutation(Moby moby)
    {
        if (moby.SourceCloneTrueIndex < 0 || string.IsNullOrWhiteSpace(moby.SourceCloneLevelKey))
            return null;

        return new
        {
            mode = moby.IsAdded ? "appendSourceRecordClone" : "cloneSourceRecordIntoSlot",
            sourceLevelKey = moby.SourceCloneLevelKey,
            sourceLevelName = moby.SourceCloneLevelName,
            sourceTrueIndex = moby.SourceCloneTrueIndex,
            preserveTargetPosition = true
        };
    }

    private static List<object> NewSourceByteEdits(Moby moby)
    {
        List<object> edits = new();
        if (moby.IsAdded && moby.Flag4A != 0 ||
            !moby.IsAdded && moby.OriginalFlag4A >= 0 && moby.Flag4A != moby.OriginalFlag4A)
        {
            edits.Add(new
            {
                offset = 82,
                offsetHex = "0x52",
                value = moby.Flag4A,
                valueHex = $"0x{moby.Flag4A:X2}",
                field = "flag4A-identity-byte"
            });
        }

        if (moby.OriginalSourceByte36 >= 0 && moby.SourceByte36 != moby.OriginalSourceByte36)
        {
            edits.Add(new
            {
                offset = 54,
                offsetHex = "0x36",
                value = moby.SourceByte36,
                valueHex = $"0x{moby.SourceByte36:X2}",
                field = "gem-id-byte"
            });
        }

        if (moby.OriginalSourceByte37 >= 0 && moby.SourceByte37 != moby.OriginalSourceByte37)
        {
            edits.Add(new
            {
                offset = 55,
                offsetHex = "0x37",
                value = moby.SourceByte37,
                valueHex = $"0x{moby.SourceByte37:X2}",
                field = "actor-id-high-byte"
            });
        }

        if (moby.OriginalSourceByte4F >= 0 && moby.SourceByte4F != moby.OriginalSourceByte4F)
        {
            edits.Add(new
            {
                offset = 79,
                offsetHex = "0x4F",
                value = moby.SourceByte4F,
                valueHex = $"0x{moby.SourceByte4F:X2}",
                field = "gem-value-byte"
            });
        }

        if (moby.OriginalFlag4B >= 0 && moby.Flag4B != moby.OriginalFlag4B)
        {
            edits.Add(new
            {
                offset = 83,
                offsetHex = "0x53",
                value = moby.Flag4B,
                valueHex = $"0x{moby.Flag4B:X2}",
                field = moby.IsChestContent ? "contained-gem-id-byte" : "type20-reward-gem-id-byte"
            });
        }

        return edits;
    }

    private static JsonElement? ReadCrossLevelTemplate(JsonElement edit)
    {
        return edit.TryGetProperty("crossLevelTemplate", out JsonElement template) && template.ValueKind == JsonValueKind.Object
            ? template
            : null;
    }

    private static object? NewChestContentLinkEdit(Moby moby, IReadOnlyDictionary<int, Moby> byTrueIndex)
    {
        if (!moby.IsChestContent)
            return null;

        foreach (MobyLink link in moby.Links.Where(link => string.Equals(link.Kind, "chest contents", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (int trueIndex in link.TrueIndexes)
            {
                if (trueIndex == moby.TrueIndex)
                    continue;
                if (!byTrueIndex.TryGetValue(trueIndex, out Moby? chest) || !chest.IsChest)
                    continue;

                return new
                {
                    mode = "contained-gem-chest-link",
                    chestIndex = chest.Index,
                    chestTrueIndex = chest.TrueIndex,
                    chestLabel = chest.DisplayLabel,
                    rawOffset = new
                    {
                        x = ToRawCoordinate(moby.Position.X) - ToRawCoordinate(chest.Position.X),
                        y = ToRawCoordinate(moby.Position.Y) - ToRawCoordinate(chest.Position.Y),
                        z = ToRawCoordinate(moby.Position.Z) - ToRawCoordinate(chest.Position.Z)
                    },
                    note = "Patch contained-gem special data +0x00 to the owning chest true index and +0x04/+0x08/+0x0C to the gem explosion offset."
                };
            }
        }

        return null;
    }

    private static List<object>? NewLinkedCompanionLinks(Moby moby, IReadOnlyDictionary<int, Moby> byTrueIndex)
    {
        List<object> links = new();
        foreach (MobyLink link in moby.Links.Where(MobyCompanionClonePlanner.IsCompanionCloneLink))
        {
            if (!link.TrueIndexes.Contains(moby.TrueIndex))
                continue;

            List<Moby> members = link.TrueIndexes
                .Select(trueIndex => byTrueIndex.TryGetValue(trueIndex, out Moby? member) ? member : null)
                .Where(member => member != null)
                .Cast<Moby>()
                .ToList();
            if (members.Count < 2 || !members.Any(member => member.IsAdded))
                continue;

            links.Add(new
            {
                key = link.Key,
                name = link.Name,
                kind = link.Kind,
                linkedMove = link.LinkedMove,
                confidence = link.Confidence,
                reason = link.Reason,
                trueIndexes = members.Select(member => member.TrueIndex).ToArray()
            });
        }

        return links.Count == 0 ? null : links;
    }

    private static void ApplySavedLinkedCompanionGroups(JsonElement editsElement, IReadOnlyDictionary<int, Moby> byTrueIndex)
    {
        HashSet<string> appliedKeys = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement edit in editsElement.EnumerateArray())
        {
            if (!edit.TryGetProperty("linkedCompanionLinks", out JsonElement links) || links.ValueKind != JsonValueKind.Array)
                continue;

            foreach (JsonElement group in links.EnumerateArray())
            {
                List<int> trueIndexes = ReadTrueIndexes(group);
                if (trueIndexes.Count < 2)
                    continue;

                string key = JsonValue.GetString(group, "key");
                if (string.IsNullOrWhiteSpace(key))
                    key = $"native-editor:loaded-companion:{string.Join("-", trueIndexes)}";
                if (!appliedKeys.Add(key))
                    continue;

                List<Moby> members = trueIndexes
                    .Select(trueIndex => byTrueIndex.TryGetValue(trueIndex, out Moby? member) ? member : null)
                    .Where(member => member != null)
                    .Cast<Moby>()
                    .ToList();
                if (members.Count < 2)
                    continue;

                MobyLink link = new()
                {
                    Key = key,
                    Name = JsonValue.GetString(group, "name", "Linked companion objects"),
                    Kind = JsonValue.GetString(group, "kind", "linked group"),
                    LinkedMove = JsonValue.GetBoolean(group, "linkedMove", true),
                    Confidence = JsonValue.GetString(group, "confidence", "native-editor-companion"),
                    Reason = JsonValue.GetString(group, "reason", "Loaded from native editor linked companion metadata."),
                    TrueIndexes = trueIndexes
                };
                if (!MobyCompanionClonePlanner.IsCompanionCloneLink(link))
                    continue;

                foreach (Moby member in members)
                    AddUniqueLink(member, link);
            }
        }
    }

    private static List<int> ReadTrueIndexes(JsonElement group)
    {
        List<int> indexes = new();
        if (!group.TryGetProperty("trueIndexes", out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return indexes;

        foreach (JsonElement value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int trueIndex) && trueIndex >= 0)
                indexes.Add(trueIndex);
        }

        return indexes;
    }

    private static void AddUniqueLink(Moby moby, MobyLink link)
    {
        if (!moby.Links.Any(existing => string.Equals(existing.Key, link.Key, StringComparison.OrdinalIgnoreCase)))
            moby.Links.Add(link);
    }

    private static int ToRawCoordinate(float value)
    {
        return (int)Math.Round(value * 16f);
    }
}

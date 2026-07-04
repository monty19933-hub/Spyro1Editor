using System.Text.Json;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Scene;

public static class MobyMetadataEnricher
{
    public static MobyMetadataResult Apply(EditorWorkspace workspace, string levelKey, IList<Moby> mobys)
    {
        int labeled = 0;
        labeled += ApplyMobyMetadataFile(workspace, levelKey, $"{levelKey}-live-validation-overrides.json", mobys);
        labeled += ApplyMobyMetadataFile(workspace, levelKey, $"{levelKey}-moby-user-overrides.json", mobys);
        labeled += ApplyIdentityObservationFiles(workspace, levelKey, mobys);
        labeled += MobyIdentityClassifier.Apply(levelKey, mobys);
        labeled += ApplyGlobalSignatureLabels(workspace, mobys);
        labeled += ApplyLevelSignatureLabels(mobys);
        labeled += ApplyLevelModelFamilyLabels(mobys);
        int behaviorLinks = ApplyBehaviorLinks(workspace, levelKey, mobys);
        int inferredLinks = InferRelationshipLinks(levelKey, mobys);
        return new MobyMetadataResult(labeled, behaviorLinks, inferredLinks);
    }

    private static int ApplyMobyMetadataFile(EditorWorkspace workspace, string levelKey, string fileName, IList<Moby> mobys)
    {
        string path = ResolveMetadataPath(workspace, fileName);
        if (!File.Exists(path))
            return 0;

        Dictionary<int, Moby> byTrueIndex = mobys
            .Where(moby => moby.TrueIndex >= 0)
            .ToDictionary(moby => moby.TrueIndex);

        int applied = 0;
        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("mobys", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
                return 0;

            foreach (JsonElement item in items.EnumerateArray())
            {
                int trueIndex = JsonValue.GetInt32(item, "trueIndex", JsonValue.GetInt32(item, "index", -1));
                if (trueIndex < 0 || !byTrueIndex.TryGetValue(trueIndex, out Moby? moby))
                    continue;
                if (HasConflictingMetadataIdentity(item, moby))
                    continue;

                string label = JsonValue.GetString(item, "displayTargetLabel", JsonValue.GetString(item, "label"));
                if (!string.IsNullOrWhiteSpace(label))
                {
                    moby.Label = label;
                    moby.OriginalLabel = label;
                }

                moby.CandidateKind = FirstNonEmpty(JsonValue.GetString(item, "candidateKind"), moby.CandidateKind);
                moby.Confidence = FirstNonEmpty(JsonValue.GetString(item, "confidence"), moby.Confidence);
                moby.Evidence = FirstNonEmpty(JsonValue.GetString(item, "evidence"), moby.Evidence);
                moby.BehaviorNote = FirstNonEmpty(JsonValue.GetString(item, "behaviorNote"), moby.BehaviorNote);
                moby.ZoneLabel = FirstNonEmpty(JsonValue.GetString(item, "zoneLabel"), moby.ZoneLabel);
                int sourceByte36 = JsonValue.GetInt32(item, "sourceByte36Hex", -1);
                if (sourceByte36 >= 0)
                    moby.SourceByte36 = sourceByte36;
                int sourceByte4F = JsonValue.GetInt32(item, "sourceByte4FHex", -1);
                if (sourceByte4F >= 0)
                    moby.SourceByte4F = sourceByte4F;
                int flag4B = JsonValue.GetInt32(item, "flag4BHex", -1);
                if (flag4B >= 0)
                    moby.Flag4B = flag4B;
                if (ColorRgba.TryParseHex(JsonValue.GetString(item, "color"), out ColorRgba color))
                    moby.Color = color;

                applied++;
            }
        }
        catch
        {
            return applied;
        }

        return applied;
    }

    private static int ApplyIdentityObservationFiles(EditorWorkspace workspace, string levelKey, IList<Moby> mobys)
    {
        int applied = 0;
        foreach (string path in IdentityObservationPaths(workspace, levelKey))
        {
            if (!File.Exists(path))
                continue;

            try
            {
                using FileStream stream = File.OpenRead(path);
                using JsonDocument document = JsonDocument.Parse(stream);
                JsonElement root = document.RootElement;
                if (!TryGetObservationArray(root, out JsonElement observations))
                    continue;

                foreach (JsonElement observation in observations.EnumerateArray())
                {
                    if (!ObservationAppliesToLevel(observation, levelKey))
                        continue;

                    string label = JsonValue.GetString(observation, "displayTargetLabel", JsonValue.GetString(observation, "label")).Trim();
                    if (!IsUsableObservationLabel(label))
                        continue;

                    foreach (Moby moby in mobys)
                    {
                        if (!ShouldApplyIdentityObservation(moby) || !ObservationMatchesMoby(observation, moby))
                            continue;

                        moby.Label = label;
                        moby.OriginalLabel = label;
                        moby.CandidateKind = FirstNonEmpty(JsonValue.GetString(observation, "candidateKind"), moby.CandidateKind);
                        moby.Confidence = FirstNonEmpty(JsonValue.GetString(observation, "confidence", "live-observed-fingerprint"), moby.Confidence);
                        moby.Evidence = FirstNonEmpty(JsonValue.GetString(observation, "evidence"), moby.Evidence);
                        if (ColorRgba.TryParseHex(JsonValue.GetString(observation, "color"), out ColorRgba color))
                            moby.Color = color;
                        applied++;
                    }
                }
            }
            catch
            {
                continue;
            }
        }

        return applied;
    }

    private static IEnumerable<string> IdentityObservationPaths(EditorWorkspace workspace, string levelKey)
    {
        yield return Path.Combine(workspace.RootPath, "moby-identity-observations.json");
        yield return Path.Combine(workspace.RootPath, $"{levelKey}-moby-identity-observations.json");
        yield return Path.Combine(workspace.RootPath, "_local", "moby-identity-observations.json");
        yield return Path.Combine(workspace.RootPath, "_local", $"{levelKey}-moby-identity-observations.json");
        yield return Path.Combine(workspace.RootPath, "_local", "smoke", "moby-identity-observations.json");
        yield return Path.Combine(workspace.RootPath, "_local", "smoke", $"{levelKey}-moby-identity-observations.json");
        yield return Path.Combine(workspace.RootPath, "_local", "identity-observation-answers", "moby-identity-observations.json");
        yield return Path.Combine(workspace.RootPath, "_local", "identity-observation-answers", $"{levelKey}-moby-identity-observations.json");
    }

    private static bool TryGetObservationArray(JsonElement root, out JsonElement observations)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            observations = root;
            return true;
        }

        if (root.TryGetProperty("observations", out observations) && observations.ValueKind == JsonValueKind.Array)
            return true;

        if (root.TryGetProperty("mobys", out observations) && observations.ValueKind == JsonValueKind.Array)
            return true;

        observations = default;
        return false;
    }

    private static bool ObservationAppliesToLevel(JsonElement observation, string levelKey)
    {
        string directLevel = JsonValue.GetString(observation, "levelKey");
        if (!string.IsNullOrWhiteSpace(directLevel) && !string.Equals(directLevel, levelKey, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!observation.TryGetProperty("levels", out JsonElement levels) || levels.ValueKind != JsonValueKind.Array)
            return true;

        return levels.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : item.ToString())
            .Any(item => string.Equals(item, levelKey, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsUsableObservationLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return false;

        string text = label.ToLowerInvariant();
        return !text.Contains("?") &&
            !text.Contains("unknown") &&
            !text.Contains("candidate") &&
            !text.Contains("related") &&
            !IsVagueObservationLabel(text);
    }

    private static bool IsVagueObservationLabel(string text)
    {
        text = text.Trim();
        string[] vagueLabels =
        [
            "object",
            "moby",
            "prop",
            "scenery",
            "actor",
            "enemy",
            "chest",
            "control",
            "helper",
            "marker",
            "scenery/prop object",
            "actor/container object",
            "nonvisual control marker",
            "special object"
        ];

        return vagueLabels.Any(label => string.Equals(text, label, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ShouldApplyIdentityObservation(Moby moby)
    {
        string label = moby.Label.Trim();
        if (string.IsNullOrWhiteSpace(label) || label.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || label.Contains("?", StringComparison.Ordinal))
            return true;

        string proof = $"{moby.Confidence} {moby.Evidence}";
        return proof.Contains("pattern-inferred", StringComparison.OrdinalIgnoreCase) ||
            proof.Contains("needs live", StringComparison.OrdinalIgnoreCase) ||
            proof.Contains("until live-tested", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ObservationMatchesMoby(JsonElement observation, Moby moby)
    {
        string fingerprint = JsonValue.GetString(observation, "fingerprint");
        if (!string.IsNullOrWhiteSpace(fingerprint))
            return string.Equals(fingerprint, IdentityFingerprint(moby), StringComparison.OrdinalIgnoreCase) &&
                ObservationPointerMatchesMoby(observation, moby) &&
                ObservationTrueIndexMatchesMoby(observation, moby);

        int type = FirstJsonInt32(observation, -1, "typeHex", "type", "typeId");
        int sourceByte36 = FirstJsonInt32(observation, -1, "sourceByte36Hex", "sourceByte36", "b36Hex", "b36");
        int flag4A = FirstJsonInt32(observation, -1, "flag4AHex", "flag4A", "f4AHex", "f4A");
        int flag4B = FirstJsonInt32(observation, -1, "flag4BHex", "flag4B", "f4BHex", "f4B");
        int sourceByte4F = FirstJsonInt32(observation, -1, "sourceByte4FHex", "sourceByte4F", "b4FHex", "b4F");
        if (type < 0 || sourceByte36 < 0 || flag4A < 0 || flag4B < 0 || sourceByte4F < 0)
            return false;

        return type == moby.Type &&
            sourceByte36 == moby.SourceByte36 &&
            flag4A == moby.Flag4A &&
            flag4B == moby.Flag4B &&
            sourceByte4F == moby.SourceByte4F &&
            ObservationPointerMatchesMoby(observation, moby) &&
            ObservationTrueIndexMatchesMoby(observation, moby);
    }

    private static bool ObservationPointerMatchesMoby(JsonElement observation, Moby moby)
    {
        long specialDataPointer = FirstJsonInt64(observation, -1, "specialDataPointerHex", "specialDataPointer");
        return specialDataPointer < 0 || (uint)specialDataPointer == moby.SpecialDataPointer;
    }

    private static bool ObservationTrueIndexMatchesMoby(JsonElement observation, Moby moby)
    {
        int trueIndex = FirstJsonInt32(observation, -1, "matchTrueIndex", "trueIndex");
        return trueIndex < 0 || trueIndex == moby.TrueIndex;
    }

    private static int FirstJsonInt32(JsonElement element, int fallback, params string[] names)
    {
        foreach (string name in names)
        {
            int value = JsonValue.GetInt32(element, name, int.MinValue);
            if (value != int.MinValue)
                return value;
        }

        return fallback;
    }

    private static long FirstJsonInt64(JsonElement element, long fallback, params string[] names)
    {
        foreach (string name in names)
        {
            long value = JsonValue.GetInt64(element, name, long.MinValue);
            if (value != long.MinValue)
                return value;
        }

        return fallback;
    }

    private static string IdentityFingerprint(Moby moby)
    {
        return $"type=0x{moby.Type:X2} b36=0x{moby.SourceByte36:X2} f4A=0x{moby.Flag4A:X2} f4B=0x{moby.Flag4B:X2} b4F=0x{moby.SourceByte4F:X2}";
    }

    private static bool HasConflictingMetadataIdentity(JsonElement item, Moby moby)
    {
        int type = JsonValue.GetInt32(item, "typeHex", -1);
        if (type >= 0 && type != moby.Type)
            return true;

        int sourceByte36 = JsonValue.GetInt32(item, "sourceByte36Hex", -1);
        if (sourceByte36 >= 0 && sourceByte36 != moby.SourceByte36)
            return true;

        int sourceByte4F = JsonValue.GetInt32(item, "sourceByte4FHex", -1);
        if (sourceByte4F >= 0 && sourceByte4F != moby.SourceByte4F)
            return true;

        int flag4B = JsonValue.GetInt32(item, "flag4BHex", -1);
        return flag4B >= 0 && flag4B != moby.Flag4B;
    }

    private static int ApplyBehaviorLinks(EditorWorkspace workspace, string levelKey, IList<Moby> mobys)
    {
        string path = ResolveMetadataPath(workspace, $"{levelKey}-behavior-links.json");
        if (!File.Exists(path))
            return 0;

        Dictionary<int, Moby> byTrueIndex = mobys
            .Where(moby => moby.TrueIndex >= 0)
            .ToDictionary(moby => moby.TrueIndex);

        int applied = 0;
        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("linkGroups", out JsonElement groups) || groups.ValueKind != JsonValueKind.Array)
                return 0;

            foreach (JsonElement group in groups.EnumerateArray())
            {
                List<int> indexes = ReadTrueIndexes(group);
                if (indexes.Count < 2)
                    continue;

                MobyLink link = new()
                {
                    Key = JsonValue.GetString(group, "key", $"{levelKey}:link:{applied}"),
                    Name = JsonValue.GetString(group, "name", "Linked objects"),
                    Kind = InferLinkKind(JsonValue.GetString(group, "name"), JsonValue.GetString(group, "basis")),
                    LinkedMove = JsonValue.GetBoolean(group, "linkedMove", true) && !IsBroadEditorScaffoldGroup(group),
                    Confidence = JsonValue.GetString(group, "confidence"),
                    Reason = FirstNonEmpty(JsonValue.GetString(group, "reason"), JsonValue.GetString(group, "basis")),
                    TrueIndexes = indexes
                };

                foreach (int trueIndex in indexes)
                {
                    if (byTrueIndex.TryGetValue(trueIndex, out Moby? moby))
                        AddUniqueLink(moby, link);
                }

                applied++;
            }
        }
        catch
        {
            return applied;
        }

        return applied;
    }

    private static int ApplyGlobalSignatureLabels(EditorWorkspace workspace, IList<Moby> mobys)
    {
        Dictionary<string, GlobalIdentityMetadata> global = BuildGlobalSignatureLabels(workspace);
        if (global.Count == 0)
            return 0;

        int applied = 0;
        foreach (Moby moby in mobys)
        {
            string key = GlobalSignatureKey(moby);
            if (string.IsNullOrEmpty(key) || !global.TryGetValue(key, out GlobalIdentityMetadata? metadata))
                continue;

            if (!IsWeakOrQuestionableIdentity(moby))
                continue;

            moby.Label = metadata.Label;
            moby.OriginalLabel = metadata.Label;
            moby.CandidateKind = metadata.Kind;
            moby.Confidence = $"global signature: {metadata.Confidence}";
            moby.Evidence = FirstNonEmpty(
                $"Reused known identity from {metadata.SourceLevelName} T{metadata.SourceTrueIndex}, matching type/special-data/flag family.",
                metadata.Evidence);
            if (metadata.Color != default)
                moby.Color = metadata.Color;
            applied++;
        }

        return applied;
    }

    private static Dictionary<string, GlobalIdentityMetadata> BuildGlobalSignatureLabels(EditorWorkspace workspace)
    {
        Dictionary<string, GlobalIdentityMetadata> result = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> blocked = new(StringComparer.OrdinalIgnoreCase);
        string root = workspace.RootPath;
        if (!Directory.Exists(root))
            return result;

        foreach (string searchRoot in new[] { root, Path.Combine(root, "support") }.Where(Directory.Exists))
        {
            foreach (string path in Directory.GetFiles(searchRoot, "*-moby-user-overrides.json"))
                MergeGlobalSignatureFile(path, result, blocked);
            foreach (string path in Directory.GetFiles(searchRoot, "*-live-validation-overrides.json"))
                MergeGlobalSignatureFile(path, result, blocked);
        }

        foreach (string key in blocked)
            result.Remove(key);
        return result;
    }

    private static void MergeGlobalSignatureFile(string path, Dictionary<string, GlobalIdentityMetadata> result, HashSet<string> blocked)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            string levelKey = JsonValue.GetString(root, "levelKey");
            string levelName = string.IsNullOrWhiteSpace(levelKey)
                ? Path.GetFileNameWithoutExtension(path).Replace("-moby-user-overrides", "", StringComparison.OrdinalIgnoreCase)
                : levelKey;
            if (!root.TryGetProperty("mobys", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
                return;

            foreach (JsonElement item in items.EnumerateArray())
            {
                string key = GlobalSignatureKey(item);
                if (string.IsNullOrEmpty(key) || blocked.Contains(key))
                    continue;

                GlobalIdentityMetadata? incoming = BuildGlobalIdentityMetadata(item, levelName);
                if (incoming == null || !IsShareableGlobalIdentity(incoming))
                    continue;

                if (!result.TryGetValue(key, out GlobalIdentityMetadata? existing))
                {
                    result[key] = incoming;
                    continue;
                }

                if (SameGlobalIdentity(existing, incoming))
                    result[key] = existing.Merge(incoming);
                else
                    blocked.Add(key);
            }
        }
        catch
        {
        }
    }

    private static GlobalIdentityMetadata? BuildGlobalIdentityMetadata(JsonElement item, string levelName)
    {
        string label = FirstNonEmpty(JsonValue.GetString(item, "displayTargetLabel"), JsonValue.GetString(item, "label")).Trim();
        if (string.IsNullOrWhiteSpace(label))
            return null;

        ColorRgba.TryParseHex(JsonValue.GetString(item, "color"), out ColorRgba color);
        return new GlobalIdentityMetadata(
            label,
            JsonValue.GetString(item, "candidateKind"),
            JsonValue.GetString(item, "confidence", "user override"),
            JsonValue.GetString(item, "evidence"),
            color,
            levelName,
            JsonValue.GetInt32(item, "trueIndex", -1));
    }

    private static bool IsShareableGlobalIdentity(GlobalIdentityMetadata metadata)
    {
        string text = $"{metadata.Label} {metadata.Kind} {metadata.Confidence}".ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(metadata.Label) ||
            text.Contains("?") ||
            text.Contains("unknown") ||
            text.Contains("candidate") ||
            text.Contains("related to") ||
            text.Contains("crash"))
            return false;

        if (ContainsAnyText(text, "scenery", "tree", "grass", "flower", "lamp", "flag", "fountain", "camera", "nonvisual", "invisible", "helper", "control") &&
            !ContainsAnyText(text, "dragon", "pedestal", "portal", "return home", "locked chest", "spring chest"))
            return false;

        return ContainsAnyText(text,
            "chest", "box", "container", "enemy", "gnorc", "norc", "torro", "bull", "fodder", "sheep", "chicken", "ram", "shepherd", "shepard", "thief", "theif",
            "key", "dragon", "pedestal", "whirlwind", "fairy", "portal", "return home", "balloonist", "transport npc");
    }

    private static bool SameGlobalIdentity(GlobalIdentityMetadata left, GlobalIdentityMetadata right)
    {
        return string.Equals(NormalizeModelFamilyLabel(left.Label), NormalizeModelFamilyLabel(right.Label), StringComparison.OrdinalIgnoreCase);
    }

    private static string GlobalSignatureKey(Moby moby)
    {
        if (moby.SpecialDataPointer == 0 || moby.Type is 0x00 or 0x18)
            return "";

        return $"{moby.Type:X2}|{moby.SpecialDataPointer:X8}|{moby.Flag4A:X2}";
    }

    private static string GlobalSignatureKey(JsonElement item)
    {
        int type = JsonValue.GetInt32(item, "typeId", JsonValue.GetInt32(item, "typeHex", -1));
        long specialDataPointer = JsonValue.GetInt64(item, "specialDataPointer", -1);
        int flag4A = JsonValue.GetInt32(item, "flag4A", JsonValue.GetInt32(item, "flag4AHex", -1));
        if (type < 0 || specialDataPointer <= 0 || flag4A < 0 || type is 0x00 or 0x18)
            return "";

        return $"{type:X2}|{(uint)specialDataPointer:X8}|{flag4A:X2}";
    }

    private static int ApplyLevelSignatureLabels(IList<Moby> mobys)
    {
        Dictionary<string, Moby> strongBySignature = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> blocked = new(StringComparer.OrdinalIgnoreCase);

        foreach (Moby moby in mobys)
        {
            string key = LevelSignatureKey(moby);
            if (string.IsNullOrEmpty(key) || blocked.Contains(key) || !IsShareableStrongIdentity(moby))
                continue;

            if (!strongBySignature.TryGetValue(key, out Moby? existing))
            {
                strongBySignature[key] = moby;
                continue;
            }

            if (!SameIdentityLabel(existing, moby))
            {
                blocked.Add(key);
                strongBySignature.Remove(key);
            }
        }

        int applied = 0;
        foreach (Moby moby in mobys)
        {
            string key = LevelSignatureKey(moby);
            if (string.IsNullOrEmpty(key) || blocked.Contains(key) || !strongBySignature.TryGetValue(key, out Moby? source))
                continue;

            if (ReferenceEquals(source, moby) || !IsWeakOrQuestionableIdentity(moby) || !IsShareableStrongIdentity(source))
                continue;

            moby.Label = source.DisplayLabel;
            moby.OriginalLabel = source.DisplayLabel;
            moby.CandidateKind = source.CandidateKind;
            moby.Confidence = string.IsNullOrWhiteSpace(source.Confidence)
                ? "level signature"
                : $"level signature: {source.Confidence}";
            moby.Evidence = FirstNonEmpty(
                $"Reused known identity from matching type/special-data signature T{source.TrueIndex} in this level.",
                source.Evidence);
            moby.BehaviorNote = FirstNonEmpty(moby.BehaviorNote, source.BehaviorNote);
            moby.Color = source.Color;
            applied++;
        }

        return applied;
    }

    private static int ApplyLevelModelFamilyLabels(IList<Moby> mobys)
    {
        Dictionary<string, Moby> strongByFamily = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> blocked = new(StringComparer.OrdinalIgnoreCase);

        foreach (Moby moby in mobys)
        {
            string key = LevelModelFamilyKey(moby);
            if (string.IsNullOrEmpty(key) || blocked.Contains(key) || !IsShareableStrongIdentity(moby))
                continue;

            if (!strongByFamily.TryGetValue(key, out Moby? existing))
            {
                strongByFamily[key] = moby;
                continue;
            }

            if (!SameModelFamilyLabel(existing, moby))
            {
                blocked.Add(key);
                strongByFamily.Remove(key);
            }
        }

        int applied = 0;
        foreach (Moby moby in mobys)
        {
            string key = LevelModelFamilyKey(moby);
            if (string.IsNullOrEmpty(key) || blocked.Contains(key) || !strongByFamily.TryGetValue(key, out Moby? source))
                continue;

            if (ReferenceEquals(source, moby) || !IsWeakOrQuestionableIdentity(moby) || !IsShareableStrongIdentity(source))
                continue;

            string label = FamilyLabelForVariant(source.DisplayLabel, moby);
            moby.Label = label;
            moby.OriginalLabel = label;
            moby.CandidateKind = source.CandidateKind;
            moby.Confidence = string.IsNullOrWhiteSpace(source.Confidence)
                ? "level model family"
                : $"level model family: {source.Confidence}";
            moby.Evidence = FirstNonEmpty(
                $"Reused known identity from matching level-local model family T{source.TrueIndex}; reward/state bytes may differ.",
                source.Evidence);
            moby.BehaviorNote = FirstNonEmpty(moby.BehaviorNote, source.BehaviorNote);
            moby.Color = source.Color;
            applied++;
        }

        return applied;
    }

    private static string LevelSignatureKey(Moby moby)
    {
        if (moby.SpecialDataPointer == 0 || moby.Type is 0x00 or 0x18)
            return "";

        return $"{moby.Type:X2}|{moby.SpecialDataPointer:X8}|{moby.Flag4A:X2}|{moby.Flag4B:X2}";
    }

    private static string LevelModelFamilyKey(Moby moby)
    {
        if (moby.SpecialDataPointer == 0 || moby.Type is 0x00 or 0x18)
            return "";

        return $"{moby.Type:X2}|{moby.SourceByte36:X2}|{moby.SpecialDataPointer:X8}";
    }

    private static bool IsShareableStrongIdentity(Moby moby)
    {
        string label = moby.DisplayLabel;
        string text = $"{label} {moby.CandidateKind} {moby.Confidence}".ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(label) ||
            label.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(label, Moby.FallbackLabel(moby.Type), StringComparison.OrdinalIgnoreCase))
            return false;

        if (text.Contains("?") ||
            text.Contains("unknown") ||
            text.Contains("object?") ||
            text.Contains("candidate") ||
            text.Contains("nonvisual") ||
            text.Contains("invisible") ||
            text.Contains("helper") ||
            text.Contains("control"))
            return false;

        return ContainsAnyText(text,
            "chest", "box", "enemy", "gnorc", "norc", "bull", "fodder", "sheep", "chicken", "ram", "shepherd", "shepard", "thief",
            "dragon", "pedestal", "whirlwind", "portal", "return home", "balloonist", "tree", "cactus", "flower", "lamp", "flag");
    }

    private static bool IsWeakOrQuestionableIdentity(Moby moby)
    {
        string label = moby.DisplayLabel;
        string text = $"{label} {moby.CandidateKind} {moby.Confidence} {moby.Evidence}".ToLowerInvariant();
        return string.IsNullOrWhiteSpace(label) ||
            label.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(label, Moby.FallbackLabel(moby.Type), StringComparison.OrdinalIgnoreCase) ||
            text.Contains("?") ||
            text.Contains("candidate") ||
            text.Contains("needs live validation") ||
            text.Contains("until live-tested");
    }

    private static bool SameIdentityLabel(Moby left, Moby right)
    {
        return string.Equals(NormalizeIdentityLabel(left.DisplayLabel), NormalizeIdentityLabel(right.DisplayLabel), StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameModelFamilyLabel(Moby left, Moby right)
    {
        return string.Equals(NormalizeModelFamilyLabel(left.DisplayLabel), NormalizeModelFamilyLabel(right.DisplayLabel), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeIdentityLabel(string label)
    {
        string result = label.Trim().ToLowerInvariant();
        int parenthetical = result.IndexOf(" (", StringComparison.Ordinal);
        if (parenthetical >= 0)
            result = result[..parenthetical];
        return result
            .Replace("safe-ground observed", "", StringComparison.OrdinalIgnoreCase)
            .Replace("live-tested", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string NormalizeModelFamilyLabel(string label)
    {
        string result = NormalizeIdentityLabel(label);
        int reward = result.IndexOf(" reward", StringComparison.OrdinalIgnoreCase);
        if (reward >= 0)
            result = result[..reward].Trim();

        return result
            .Replace("red reward", "", StringComparison.OrdinalIgnoreCase)
            .Replace("green reward", "", StringComparison.OrdinalIgnoreCase)
            .Replace("blue reward", "", StringComparison.OrdinalIgnoreCase)
            .Replace("yellow reward", "", StringComparison.OrdinalIgnoreCase)
            .Replace("purple reward", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string FamilyLabelForVariant(string sourceLabel, Moby moby)
    {
        if (!GemValue.TryFromIdByte(moby.Flag4B, out GemValue gem) || !sourceLabel.Contains("reward", StringComparison.OrdinalIgnoreCase))
            return sourceLabel;

        int parenthetical = sourceLabel.IndexOf(" (", StringComparison.Ordinal);
        string baseLabel = parenthetical >= 0 ? sourceLabel[..parenthetical] : sourceLabel;
        string reward = gem.Name.Replace(" gem", "", StringComparison.OrdinalIgnoreCase);
        return $"{baseLabel} ({reward} reward)";
    }

    private static bool ContainsAnyText(string text, params string[] needles)
    {
        return needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsBroadEditorScaffoldGroup(JsonElement group)
    {
        string text = $"{JsonValue.GetString(group, "key")} {JsonValue.GetString(group, "name")} {JsonValue.GetString(group, "confidence")} {JsonValue.GetString(group, "reason")} {JsonValue.GetString(group, "basis")}";
        return text.Contains("editor-scaffold", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("broad fallback", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("cluster", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("all portal pad/trigger clusters", StringComparison.OrdinalIgnoreCase) ||
            IsBroadPortalClusterGroup(group, text);
    }

    private static bool IsBroadPortalClusterGroup(JsonElement group, string text)
    {
        return ReadTrueIndexes(group).Count > 6 &&
            text.Contains("portal", StringComparison.OrdinalIgnoreCase) &&
            (text.Contains("cluster", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("pad/arch", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("collision/warp", StringComparison.OrdinalIgnoreCase));
    }

    private static int InferRelationshipLinks(string levelKey, IList<Moby> mobys)
    {
        int count = 0;
        count += InferChestContentLinks(levelKey, mobys);
        count += InferDragonPedestalLinks(levelKey, mobys);
        return count;
    }

    private static int InferChestContentLinks(string levelKey, IList<Moby> mobys)
    {
        List<Moby> chests = mobys
            .Where(moby => IsChest(moby) && !IsChestContent(moby))
            .ToList();
        List<Moby> contents = mobys
            .Where(IsChestContent)
            .ToList();

        int count = 0;
        foreach (IGrouping<string, Moby> group in contents.GroupBy(content => PositionKey(content.Position)))
        {
            List<Moby> linked = group.ToList();
            Vector3f center = AveragePosition(linked);
            Moby? chest = chests
                .OrderBy(candidate => DistanceSquared(candidate.Position, center))
                .FirstOrDefault(candidate => DistanceSquared(candidate.Position, center) <= 512 * 512);

            if (chest == null)
                continue;

            linked = linked
                .OrderBy(content => content.TrueIndex)
                .Take(12)
                .ToList();

            if (linked.Count == 0)
                continue;

            List<int> indexes = new() { chest.TrueIndex };
            indexes.AddRange(linked.Select(content => content.TrueIndex));
            MobyLink link = new()
            {
                Key = $"{levelKey}:chest:{chest.TrueIndex}",
                Name = $"Chest contents for T{chest.TrueIndex}",
                Kind = "chest contents",
                LinkedMove = true,
                Confidence = linked.Any(content => content.Confidence.Contains("user", StringComparison.OrdinalIgnoreCase)) ? "metadata" : "nearby-inferred",
                Reason = "Chest and contained gem records are co-located or nearby in the imported editor metadata.",
                TrueIndexes = indexes
            };

            AddUniqueLink(chest, link);
            foreach (Moby content in linked)
                AddUniqueLink(content, link);
            count++;
        }

        return count;
    }

    private static string PositionKey(Vector3f position)
    {
        return $"{Math.Round(position.X / 32f) * 32:0}:{Math.Round(position.Y / 32f) * 32:0}:{Math.Round(position.Z / 32f) * 32:0}";
    }

    private static Vector3f AveragePosition(IReadOnlyList<Moby> mobys)
    {
        if (mobys.Count == 0)
            return new Vector3f();

        return new Vector3f(
            mobys.Sum(moby => moby.Position.X) / mobys.Count,
            mobys.Sum(moby => moby.Position.Y) / mobys.Count,
            mobys.Sum(moby => moby.Position.Z) / mobys.Count);
    }

    private static int InferDragonPedestalLinks(string levelKey, IList<Moby> mobys)
    {
        List<Moby> dragons = mobys
            .Where(IsDragonActor)
            .ToList();
        List<Moby> pedestals = mobys
            .Where(IsDragonPedestal)
            .ToList();
        List<Moby> supports = mobys
            .Where(IsDragonSupport)
            .ToList();

        HashSet<int> usedPedestals = new();
        int count = 0;
        foreach (Moby dragon in dragons)
        {
            Moby? pedestal = pedestals
                .Where(item => !usedPedestals.Contains(item.TrueIndex))
                .OrderBy(item => DistanceSquared(item.Position, dragon.Position))
                .FirstOrDefault(item => DistanceSquared(item.Position, dragon.Position) <= 384 * 384);
            if (pedestal == null)
                continue;

            usedPedestals.Add(pedestal.TrueIndex);
            List<int> indexes = new() { dragon.TrueIndex, pedestal.TrueIndex };
            indexes.AddRange(supports
                .Where(support => support.TrueIndex != dragon.TrueIndex && support.TrueIndex != pedestal.TrueIndex)
                .Where(support => IsNearbyDragonSupport(support, dragon, pedestal))
                .OrderBy(support => Math.Min(DistanceSquared(support.Position, dragon.Position), DistanceSquared(support.Position, pedestal.Position)))
                .Take(1)
                .Select(support => support.TrueIndex));

            MobyLink link = new()
            {
                Key = $"{levelKey}:dragon-scene:{dragon.TrueIndex}:{pedestal.TrueIndex}",
                Name = indexes.Count > 2
                    ? $"Dragon scene T{dragon.TrueIndex}/T{pedestal.TrueIndex}"
                    : $"Dragon/pedestal T{dragon.TrueIndex}/T{pedestal.TrueIndex}",
                Kind = indexes.Count > 2 ? "dragon scene" : "dragon pedestal",
                LinkedMove = true,
                Confidence = "nearby-inferred",
                Reason = indexes.Count > 2
                    ? "Dragon, pedestal, and nearby dragon camera/helper records are near each other in the imported editor metadata."
                    : "Dragon and pedestal labels are near each other in the imported editor metadata.",
                TrueIndexes = indexes
            };
            foreach (int trueIndex in indexes)
            {
                Moby? moby = mobys.FirstOrDefault(item => item.TrueIndex == trueIndex);
                if (moby != null)
                    AddUniqueLink(moby, link);
            }
            count++;
        }

        return count;
    }

    private static bool IsDragonActor(Moby moby)
    {
        return ContainsAny(moby, "dragon") &&
            !ContainsAny(moby, "pedestal") &&
            !ContainsAny(moby, "control", "marker") &&
            !ContainsAll(moby, "dragon", "helper") &&
            !ContainsAny(moby, "camera");
    }

    private static bool IsDragonPedestal(Moby moby)
    {
        return ContainsAny(moby, "dragon") && ContainsAny(moby, "pedestal");
    }

    private static bool IsDragonSupport(Moby moby)
    {
        return ContainsAll(moby, "dragon", "helper") ||
            ContainsAll(moby, "dragon", "camera") ||
            ContainsAll(moby, "dragon", "control") ||
            ContainsAll(moby, "rescue", "camera");
    }

    private static bool IsNearbyDragonSupport(Moby support, Moby dragon, Moby pedestal)
    {
        const float maxDragonSupportDistance = 256 * 256;
        return DistanceSquared(support.Position, dragon.Position) <= maxDragonSupportDistance &&
            DistanceSquared(support.Position, pedestal.Position) <= maxDragonSupportDistance;
    }

    private static string ResolveMetadataPath(EditorWorkspace workspace, string fileName)
    {
        string direct = Path.Combine(workspace.RootPath, fileName);
        if (File.Exists(direct))
            return direct;

        return workspace.ResolveFile(fileName, "generated-research", "game-and-capture-artifacts");
    }

    private static List<int> ReadTrueIndexes(JsonElement group)
    {
        List<int> indexes = new();
        if (!group.TryGetProperty("trueIndexes", out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return indexes;

        foreach (JsonElement value in values.EnumerateArray())
        {
            int trueIndex = value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int numeric)
                ? numeric
                : -1;
            if (trueIndex >= 0)
                indexes.Add(trueIndex);
        }

        return indexes;
    }

    private static void AddUniqueLink(Moby moby, MobyLink link)
    {
        if (moby.Links.Any(existing => string.Equals(existing.Key, link.Key, StringComparison.OrdinalIgnoreCase)))
            return;

        moby.Links.Add(link);
    }

    private static string InferLinkKind(string name, string basis)
    {
        string text = $"{name} {basis}";
        if (text.Contains("dragon", StringComparison.OrdinalIgnoreCase))
            return "dragon pedestal";
        if (text.Contains("portal", StringComparison.OrdinalIgnoreCase))
            return "portal group";
        if (text.Contains("chest", StringComparison.OrdinalIgnoreCase))
            return "chest contents";
        return "linked group";
    }

    private static bool IsChest(Moby moby)
    {
        return moby.IsChest;
    }

    private static bool IsChestContent(Moby moby)
    {
        return moby.IsChestContent;
    }

    private static bool ContainsAny(Moby moby, params string[] needles)
    {
        string text = $"{moby.DisplayLabel} {moby.CandidateKind} {moby.BehaviorNote}";
        return needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAll(Moby moby, params string[] needles)
    {
        string text = $"{moby.DisplayLabel} {moby.CandidateKind} {moby.BehaviorNote}";
        return needles.All(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
    }

    private static float DistanceSquared(Vector3f a, Vector3f b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }
}

internal sealed record GlobalIdentityMetadata(
    string Label,
    string Kind,
    string Confidence,
    string Evidence,
    ColorRgba Color,
    string SourceLevelName,
    int SourceTrueIndex)
{
    public GlobalIdentityMetadata Merge(GlobalIdentityMetadata other)
    {
        string evidence = string.IsNullOrWhiteSpace(Evidence) ? other.Evidence : Evidence;
        ColorRgba color = Color != default ? Color : other.Color;
        string sourceLevel = string.IsNullOrWhiteSpace(SourceLevelName) ? other.SourceLevelName : SourceLevelName;
        int sourceTrueIndex = SourceTrueIndex >= 0 ? SourceTrueIndex : other.SourceTrueIndex;
        return this with
        {
            Evidence = evidence,
            Color = color,
            SourceLevelName = sourceLevel,
            SourceTrueIndex = sourceTrueIndex
        };
    }
}

public readonly record struct MobyMetadataResult(int LabeledMobys, int BehaviorLinkGroups, int InferredLinkGroups);

using System.Globalization;
using System.Text.Json;
using Spyro.Editor.Core.Cache;
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
        foreach (Moby moby in mobys)
        {
            moby.OriginalColor = moby.Color;
            moby.OriginalPatchStatus = moby.PatchStatus;
            moby.OriginalPatchLead = moby.PatchLead;
            moby.OriginalCandidateKind = moby.CandidateKind;
            moby.OriginalConfidence = moby.Confidence;
            moby.OriginalEvidence = moby.Evidence;
            moby.OriginalBehaviorNote = moby.BehaviorNote;
            moby.OriginalZoneLabel = moby.ZoneLabel;
        }
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
                int sourceByte37 = JsonValue.GetInt32(item, "sourceByte37Hex", -1);
                if (sourceByte37 >= 0)
                    moby.SourceByte37 = sourceByte37;
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
        HashSet<Moby> observationApplied = new();
        foreach (string path in IdentityObservationPaths(workspace, levelKey))
        {
            if (!File.Exists(path))
                continue;

            bool implicitLevelScope = Path.GetFileName(path).StartsWith($"{levelKey}-", StringComparison.OrdinalIgnoreCase);

            try
            {
                using FileStream stream = File.OpenRead(path);
                using JsonDocument document = JsonDocument.Parse(stream);
                JsonElement root = document.RootElement;
                if (!TryGetObservationArray(root, out JsonElement observations))
                    continue;

                foreach (JsonElement observation in observations.EnumerateArray()
                    .OrderBy(IdentityObservationScopeRank))
                {
                    if (!ObservationAppliesToLevel(observation, levelKey))
                        continue;

                    string label = JsonValue.GetString(observation, "displayTargetLabel", JsonValue.GetString(observation, "label")).Trim();
                    if (!IsUsableObservationLabel(label))
                        continue;

                    foreach (Moby moby in mobys)
                    {
                        if ((!observationApplied.Contains(moby) && !ShouldApplyIdentityObservation(moby)) ||
                            !ObservationMatchesMoby(observation, moby, mobys, implicitLevelScope))
                        {
                            continue;
                        }

                        moby.Label = label;
                        moby.OriginalLabel = label;
                        moby.CandidateKind = FirstNonEmpty(JsonValue.GetString(observation, "candidateKind"), moby.CandidateKind);
                        moby.Confidence = FirstNonEmpty(JsonValue.GetString(observation, "confidence", "live-observed-fingerprint"), moby.Confidence);
                        moby.Evidence = FirstNonEmpty(JsonValue.GetString(observation, "evidence"), moby.Evidence);
                        if (ColorRgba.TryParseHex(JsonValue.GetString(observation, "color"), out ColorRgba color))
                            moby.Color = color;
                        observationApplied.Add(moby);
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

    private static int IdentityObservationScopeRank(JsonElement observation)
    {
        int rank = 0;
        if (FirstJsonInt64(observation, -1, "specialDataPointerHex", "specialDataPointer") > 0)
            rank++;
        if (FirstJsonInt32(observation, -1, "matchTrueIndex", "trueIndex") >= 0)
            rank += 2;
        return rank;
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
            !text.Contains("placeholder") &&
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

    private static bool ObservationMatchesMoby(
        JsonElement observation,
        Moby moby,
        IEnumerable<Moby> levelMobys,
        bool implicitLevelScope)
    {
        string fingerprint = JsonValue.GetString(observation, "fingerprint");
        if (!string.IsNullOrWhiteSpace(fingerprint))
        {
            bool pointerMatches = ObservationPointerMatchesMoby(observation, moby);
            bool trueIndexMatches = ObservationTrueIndexMatchesMoby(observation, moby);
            if (!pointerMatches || !trueIndexMatches)
                return false;

            int optionalSourceByte36 = FirstJsonInt32(observation, -1, "sourceByte36Hex", "sourceByte36", "b36Hex", "b36");
            int optionalSourceByte37 = FirstJsonInt32(observation, -1, "sourceByte37Hex", "sourceByte37", "b37Hex", "b37");
            int optionalNativeClass = FirstJsonInt32(observation, -1, "nativeClassHex", "nativeClass");
            if ((optionalSourceByte36 >= 0 && optionalSourceByte36 != moby.SourceByte36) ||
                (optionalSourceByte37 >= 0 && optionalSourceByte37 != moby.SourceByte37) ||
                (optionalNativeClass >= 0 && optionalNativeClass != ((moby.SourceByte37 << 8) | moby.SourceByte36)))
            {
                return false;
            }

            bool legacy = MobyIdentityFingerprint.IsLegacyV1(fingerprint);
            if (legacy && !LegacyFingerprintHasSafeScope(observation, fingerprint, levelMobys, implicitLevelScope))
                return false;

            return MobyIdentityFingerprint.Matches(fingerprint, moby, allowLegacyLowByte: legacy);
        }

        int type = FirstJsonInt32(observation, -1, "typeHex", "type", "typeId");
        int sourceByte36 = FirstJsonInt32(observation, -1, "sourceByte36Hex", "sourceByte36", "b36Hex", "b36");
        int sourceByte37 = FirstJsonInt32(observation, -1, "sourceByte37Hex", "sourceByte37", "b37Hex", "b37");
        int nativeClass = FirstJsonInt32(observation, -1, "nativeClassHex", "nativeClass");
        int flag4A = FirstJsonInt32(observation, -1, "flag4AHex", "flag4A", "f4AHex", "f4A");
        int flag4B = FirstJsonInt32(observation, -1, "flag4BHex", "flag4B", "f4BHex", "f4B");
        int sourceByte4F = FirstJsonInt32(observation, -1, "sourceByte4FHex", "sourceByte4F", "b4FHex", "b4F");
        if (nativeClass >= 0)
        {
            int nativeLow = nativeClass & 0xFF;
            int nativeHigh = (nativeClass >> 8) & 0xFF;
            if ((sourceByte36 >= 0 && sourceByte36 != nativeLow) ||
                (sourceByte37 >= 0 && sourceByte37 != nativeHigh))
            {
                return false;
            }

            sourceByte36 = nativeLow;
            sourceByte37 = nativeHigh;
        }
        if (type < 0 || sourceByte36 < 0 || flag4A < 0 || flag4B < 0 || sourceByte4F < 0)
            return false;

        if (sourceByte37 < 0)
        {
            string legacyFingerprint = $"type=0x{type:X2} b36=0x{sourceByte36:X2} f4A=0x{flag4A:X2} f4B=0x{flag4B:X2} b4F=0x{sourceByte4F:X2}";
            if (!LegacyFingerprintHasSafeScope(observation, legacyFingerprint, levelMobys, implicitLevelScope))
                return false;
        }

        return type == moby.Type &&
            sourceByte36 == moby.SourceByte36 &&
            (sourceByte37 < 0 || sourceByte37 == moby.SourceByte37) &&
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

    private static bool LegacyFingerprintHasSafeScope(
        JsonElement observation,
        string fingerprint,
        IEnumerable<Moby> levelMobys,
        bool implicitLevelScope)
    {
        int sourceByte37 = FirstJsonInt32(observation, -1, "sourceByte37Hex", "sourceByte37", "b37Hex", "b37");
        int nativeClass = FirstJsonInt32(observation, -1, "nativeClassHex", "nativeClass");
        if (sourceByte37 >= 0 || nativeClass >= 0)
            return true;

        string directLevel = JsonValue.GetString(observation, "levelKey");
        bool hasSingleLevelScope = implicitLevelScope || !string.IsNullOrWhiteSpace(directLevel);
        if (!hasSingleLevelScope &&
            observation.TryGetProperty("levels", out JsonElement levels) &&
            levels.ValueKind == JsonValueKind.Array)
        {
            hasSingleLevelScope = levels.GetArrayLength() == 1;
        }
        if (!hasSingleLevelScope)
            return false;

        int trueIndex = FirstJsonInt32(observation, -1, "matchTrueIndex", "trueIndex");
        long specialDataPointer = FirstJsonInt64(observation, -1, "specialDataPointerHex", "specialDataPointer");
        int distinctClasses = levelMobys
            .Where(candidate => MobyIdentityFingerprint.Matches(fingerprint, candidate, allowLegacyLowByte: true))
            .Where(candidate => trueIndex < 0 || candidate.TrueIndex == trueIndex)
            .Where(candidate => specialDataPointer <= 0 || candidate.SpecialDataPointer == (uint)specialDataPointer)
            .Select(candidate => (candidate.SourceByte37 << 8) | candidate.SourceByte36)
            .Distinct()
            .Take(2)
            .Count();
        return distinctClasses == 1;
    }

    private static bool HasConflictingMetadataIdentity(JsonElement item, Moby moby)
    {
        int type = JsonValue.GetInt32(item, "typeHex", -1);
        if (type >= 0 && type != moby.Type)
            return true;

        int sourceByte36 = JsonValue.GetInt32(item, "sourceByte36Hex", -1);
        if (sourceByte36 >= 0 && sourceByte36 != moby.SourceByte36)
            return true;

        int sourceByte37 = JsonValue.GetInt32(item, "sourceByte37Hex", -1);
        if (sourceByte37 >= 0 && sourceByte37 != moby.SourceByte37)
            return true;

        int sourceByte4F = JsonValue.GetInt32(item, "sourceByte4FHex", -1);
        if (sourceByte4F >= 0 && sourceByte4F != moby.SourceByte4F)
            return true;

        int flag4B = JsonValue.GetInt32(item, "flag4BHex", -1);
        return flag4B >= 0 && flag4B != moby.Flag4B;
    }

    private static int ApplyBehaviorLinks(EditorWorkspace workspace, string levelKey, IList<Moby> mobys)
    {
        Dictionary<int, Moby> byTrueIndex = mobys
            .Where(moby => moby.TrueIndex >= 0)
            .ToDictionary(moby => moby.TrueIndex);

        int applied = 0;
        HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (string path in BehaviorLinkMetadataPaths(workspace, levelKey))
        {
            if (!File.Exists(path) || !seenPaths.Add(path))
                continue;

            applied += ApplyBehaviorLinkFile(path, levelKey, byTrueIndex);
        }

        return applied;
    }

    private static int ApplyBehaviorLinkFile(string path, string levelKey, IReadOnlyDictionary<int, Moby> byTrueIndex)
    {
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
                    Kind = FirstNonEmpty(JsonValue.GetString(group, "kind"), InferLinkKind(JsonValue.GetString(group, "name"), JsonValue.GetString(group, "basis"))),
                    LinkedMove = JsonValue.GetBoolean(group, "linkedMove", true) && !IsBroadEditorScaffoldGroup(group),
                    Confidence = JsonValue.GetString(group, "confidence"),
                    Reason = FirstNonEmpty(JsonValue.GetString(group, "reason"), JsonValue.GetString(group, "basis")),
                    TrueIndexes = indexes
                };

                foreach (int trueIndex in indexes)
                {
                    if (byTrueIndex.TryGetValue(trueIndex, out Moby? moby))
                    {
                        AddUniqueLink(moby, link);
                        ApplyBehaviorLinkMemberMetadata(group, moby);
                    }
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

    private static void ApplyBehaviorLinkMemberMetadata(JsonElement group, Moby moby)
    {
        string label = ReadMemberString(group, "memberLabels", moby.TrueIndex);
        if (!string.IsNullOrWhiteSpace(label) && ShouldApplyBehaviorLinkMemberLabel(moby))
        {
            moby.Label = label;
            moby.OriginalLabel = label;
        }

        string kind = ReadMemberString(group, "memberKinds", moby.TrueIndex);
        if (string.IsNullOrWhiteSpace(kind) &&
            IsTreasureThiefRewardTriggerGroup(group) &&
            moby.Type == 0x00)
        {
            kind = "treasure thief reward trigger marker";
        }

        if (!string.IsNullOrWhiteSpace(kind))
            moby.CandidateKind = FirstNonEmpty(kind, moby.CandidateKind);

        string note = ReadMemberString(group, "memberNotes", moby.TrueIndex);
        if (string.IsNullOrWhiteSpace(note) && IsTreasureThiefRewardTriggerGroup(group))
        {
            note = moby.Type == 0x00
                ? "User-confirmed hidden reward trigger for the linked Treasure Gnorc; edit its reward gem byte through the Treasure Gnorc dialog."
                : "User-confirmed Treasure Gnorc reward root; linked hidden trigger rows control its spawned red gems.";
        }

        if (!string.IsNullOrWhiteSpace(note))
            moby.BehaviorNote = FirstNonEmpty(note, moby.BehaviorNote);
    }

    private static bool ShouldApplyBehaviorLinkMemberLabel(Moby moby)
    {
        string label = moby.Label.Trim();
        if (string.IsNullOrWhiteSpace(label) || label.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return true;

        string lowerLabel = label.ToLowerInvariant();
        if (lowerLabel.Contains("scene control", StringComparison.Ordinal) ||
            lowerLabel.Contains("scene/route", StringComparison.Ordinal) ||
            lowerLabel.Contains("class 0x1e passive control", StringComparison.Ordinal) ||
            lowerLabel.Contains("control marker", StringComparison.Ordinal) ||
            lowerLabel.Contains("system/trigger", StringComparison.Ordinal))
        {
            return true;
        }

        string proof = $"{moby.Confidence} {moby.Evidence} {moby.BehaviorNote}";
        return proof.Contains("pattern-inferred", StringComparison.OrdinalIgnoreCase) ||
            proof.Contains("needs live", StringComparison.OrdinalIgnoreCase) ||
            proof.Contains("until live-tested", StringComparison.OrdinalIgnoreCase) ||
            proof.Contains("scene/route", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadMemberString(JsonElement group, string propertyName, int trueIndex)
    {
        if (!group.TryGetProperty(propertyName, out JsonElement values) || values.ValueKind != JsonValueKind.Object)
            return "";

        string numericKey = trueIndex.ToString(CultureInfo.InvariantCulture);
        string prefixedKey = $"T{numericKey}";
        if (values.TryGetProperty(prefixedKey, out JsonElement prefixed) && prefixed.ValueKind == JsonValueKind.String)
            return prefixed.GetString() ?? "";
        if (values.TryGetProperty(numericKey, out JsonElement numeric) && numeric.ValueKind == JsonValueKind.String)
            return numeric.GetString() ?? "";
        return "";
    }

    private static bool IsTreasureThiefRewardTriggerGroup(JsonElement group)
    {
        string text = $"{JsonValue.GetString(group, "key")} {JsonValue.GetString(group, "name")} {JsonValue.GetString(group, "kind")} {JsonValue.GetString(group, "basis")} {JsonValue.GetString(group, "reason")}";
        return text.Contains("treasure", StringComparison.OrdinalIgnoreCase) &&
            text.Contains("reward", StringComparison.OrdinalIgnoreCase) &&
            text.Contains("trigger", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> BehaviorLinkMetadataPaths(EditorWorkspace workspace, string levelKey)
    {
        yield return ResolveMetadataPath(workspace, $"{levelKey}-behavior-links.json");
        yield return Path.Combine(
            workspace.RootPath,
            "_local",
            "control-role-proof-review",
            "promoted-behavior-links",
            $"{levelKey}-behavior-links.json");
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
                $"Reused known identity from {metadata.SourceLevelName} T{metadata.SourceTrueIndex}, matching full native class, render-radius, special-data, and update-distance family.",
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
                MergeGlobalSignatureFile(workspace, path, result, blocked);
            foreach (string path in Directory.GetFiles(searchRoot, "*-live-validation-overrides.json"))
                MergeGlobalSignatureFile(workspace, path, result, blocked);
        }

        foreach (string key in blocked)
            result.Remove(key);
        return result;
    }

    private static void MergeGlobalSignatureFile(EditorWorkspace workspace, string path, Dictionary<string, GlobalIdentityMetadata> result, HashSet<string> blocked)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            string levelKey = JsonValue.GetString(root, "levelKey");
            if (string.IsNullOrWhiteSpace(levelKey))
            {
                levelKey = Path.GetFileNameWithoutExtension(path)
                    .Replace("-moby-user-overrides", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("-live-validation-overrides", "", StringComparison.OrdinalIgnoreCase);
            }
            string levelName = levelKey;
            Dictionary<int, Moby> sourceMobys = LoadSourceMobysByTrueIndex(workspace, levelKey);
            if (!root.TryGetProperty("mobys", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
                return;

            foreach (JsonElement item in items.EnumerateArray())
            {
                string key = GlobalSignatureKey(item, sourceMobys);
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
        if (moby.SpecialDataPointer == 0 || moby.IsGemLike || moby.VisualKind == MobyVisualKind.Control)
            return "";

        return $"{moby.SourceByte37:X2}{moby.SourceByte36:X2}|{moby.Type:X2}|{moby.SpecialDataPointer:X8}|{moby.Flag4A:X2}";
    }

    private static string GlobalSignatureKey(JsonElement item, IReadOnlyDictionary<int, Moby> sourceMobys)
    {
        int trueIndex = JsonValue.GetInt32(item, "trueIndex", JsonValue.GetInt32(item, "index", -1));
        int type = JsonValue.GetInt32(item, "typeId", JsonValue.GetInt32(item, "typeHex", -1));
        long specialDataPointer = JsonValue.GetInt64(item, "specialDataPointer", -1);
        int flag4A = JsonValue.GetInt32(item, "flag4A", JsonValue.GetInt32(item, "flag4AHex", -1));
        int sourceByte36 = JsonValue.GetInt32(item, "sourceByte36Hex", -1);
        int sourceByte37 = JsonValue.GetInt32(item, "sourceByte37Hex", -1);
        if (sourceMobys.TryGetValue(trueIndex, out Moby? source))
        {
            if ((type >= 0 && type != source.Type) ||
                (specialDataPointer >= 0 && (uint)specialDataPointer != source.SpecialDataPointer) ||
                (flag4A >= 0 && flag4A != source.Flag4A) ||
                (sourceByte36 >= 0 && sourceByte36 != source.SourceByte36) ||
                (sourceByte37 >= 0 && sourceByte37 != source.SourceByte37))
            {
                return "";
            }

            return GlobalSignatureKey(source);
        }

        if (type < 0 || specialDataPointer <= 0 || flag4A < 0 || sourceByte36 < 0 || sourceByte37 < 0)
            return "";

        return $"{sourceByte37:X2}{sourceByte36:X2}|{type:X2}|{(uint)specialDataPointer:X8}|{flag4A:X2}";
    }

    private static Dictionary<int, Moby> LoadSourceMobysByTrueIndex(EditorWorkspace workspace, string levelKey)
    {
        string path = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-mobys.json");
        if (!File.Exists(path))
            return new Dictionary<int, Moby>();

        try
        {
            return MobyLoader.LoadCached(path)
                .Where(moby => moby.TrueIndex >= 0)
                .GroupBy(moby => moby.TrueIndex)
                .ToDictionary(group => group.Key, group => group.First());
        }
        catch
        {
            return new Dictionary<int, Moby>();
        }
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
                $"Reused known identity from matching full-class/render-radius/special-data signature T{source.TrueIndex} in this level.",
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
            string familyEvidence = moby.SpecialDataPointer != 0
                ? $"matching level-local model pointer 0x{moby.SpecialDataPointer:X8}"
                : $"matching native class 0x{moby.SourceByte37:X2}{moby.SourceByte36:X2} and source-record family";
            moby.Evidence = FirstNonEmpty(
                $"Reused known identity from T{source.TrueIndex} with {familyEvidence}; reward/state bytes may differ.",
                source.Evidence);
            moby.BehaviorNote = FirstNonEmpty(moby.BehaviorNote, source.BehaviorNote);
            moby.Color = source.Color;
            applied++;
        }

        return applied;
    }

    private static string LevelSignatureKey(Moby moby)
    {
        if (moby.SpecialDataPointer == 0 || moby.IsGemLike || moby.VisualKind == MobyVisualKind.Control)
            return "";

        return $"{moby.SourceByte37:X2}{moby.SourceByte36:X2}|{moby.Type:X2}|{moby.SpecialDataPointer:X8}|{moby.Flag4A:X2}|{moby.Flag4B:X2}";
    }

    private static string LevelModelFamilyKey(Moby moby)
    {
        if (moby.IsGemLike || moby.VisualKind == MobyVisualKind.Control)
            return "";

        if (moby.SpecialDataPointer != 0)
            return $"pointer|{moby.SourceByte37:X2}{moby.SourceByte36:X2}|{moby.SpecialDataPointer:X8}";

        if (moby.SourceByte36 == 0 && moby.SourceByte37 == 0)
            return "";

        // Portable source caches intentionally have no runtime model pointer. Within one level,
        // Full native class plus update-distance family is the stable equivalent of the pointer family.
        return $"native-class|{moby.SourceByte37:X2}{moby.SourceByte36:X2}|{moby.Flag4A:X2}";
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
        if (text.Contains("live-proof-control-role", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("control-role-proof", StringComparison.OrdinalIgnoreCase))
            return false;
        if (text.Contains("treasure", StringComparison.OrdinalIgnoreCase) &&
            text.Contains("reward", StringComparison.OrdinalIgnoreCase) &&
            text.Contains("trigger", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

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
        count += ApplySourceProvenPortalControlLinks(levelKey, mobys);
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
        List<Moby> sceneLinkControls = mobys
            .Where(IsDragonSceneLinkControl)
            .ToList();
        List<Moby> supports = mobys
            .Where(IsDragonSupport)
            .ToList();

        HashSet<int> usedPedestals = new();
        HashSet<int> usedSceneLinkControls = new();
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
            Moby? sceneLinkControl = sceneLinkControls
                .Where(item => !usedSceneLinkControls.Contains(item.TrueIndex))
                .OrderBy(item => DistanceSquared(item.Position, dragon.Position))
                .FirstOrDefault(item => IsNearbyDragonSceneLinkControl(item, dragon, pedestal));
            if (sceneLinkControl != null)
            {
                usedSceneLinkControls.Add(sceneLinkControl.TrueIndex);
                indexes.Add(sceneLinkControl.TrueIndex);
            }
            else
            {
                indexes.AddRange(supports
                    .Where(support => support.TrueIndex != dragon.TrueIndex && support.TrueIndex != pedestal.TrueIndex)
                    .Where(support => IsNearbyDragonSupport(support, dragon, pedestal))
                    .OrderBy(support => Math.Min(DistanceSquared(support.Position, dragon.Position), DistanceSquared(support.Position, pedestal.Position)))
                    .Take(2)
                    .Select(support => support.TrueIndex));
            }

            MobyLink link = new()
            {
                Key = $"{levelKey}:dragon-scene:{dragon.TrueIndex}:{pedestal.TrueIndex}",
                Name = indexes.Count > 2
                    ? $"Dragon scene T{dragon.TrueIndex}/T{pedestal.TrueIndex}"
                    : $"Dragon/pedestal T{dragon.TrueIndex}/T{pedestal.TrueIndex}",
                Kind = indexes.Count > 2 ? "dragon scene" : "dragon pedestal",
                LinkedMove = true,
                Confidence = sceneLinkControl != null ? "native-byte-family" : "nearby-inferred",
                Reason = sceneLinkControl != null
                    ? "Native dragon actor, pedestal, and 0x6E scene-link control form the repeated three-row dragon scene; linked data stores the approach camera and later cinematic camera track."
                    : indexes.Count > 2
                    ? "Dragon, pedestal, and nearby dragon camera/helper/trigger records are near each other in the imported editor metadata."
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
        if (IsNativeDragonActor(moby))
            return true;
        if (IsNativeDragonPedestal(moby) || IsDragonSceneLinkControl(moby))
            return false;

        return ContainsAny(moby, "dragon") &&
            !ContainsAny(moby, "pedestal") &&
            !ContainsAny(moby, "control", "marker") &&
            !ContainsAll(moby, "dragon", "helper") &&
            !ContainsAny(moby, "camera");
    }

    private static bool IsDragonPedestal(Moby moby)
    {
        if (IsNativeDragonPedestal(moby))
            return true;
        if (IsNativeDragonActor(moby) || IsDragonSceneLinkControl(moby))
            return false;

        return ContainsAny(moby, "dragon") && ContainsAny(moby, "pedestal");
    }

    private static bool IsDragonSupport(Moby moby)
    {
        return IsDragonSceneLinkControl(moby) ||
            ContainsAll(moby, "dragon", "helper") ||
            ContainsAll(moby, "dragon", "camera") ||
            ContainsAll(moby, "dragon", "control") ||
            ContainsAll(moby, "rescue", "camera");
    }

    private static bool IsNativeDragonActor(Moby moby)
    {
        return moby.Type is 0x20 or 0x3C &&
            moby.SourceByte36 == 0xFA &&
            moby.SourceByte37 == 0x00 &&
            moby.SourceByte4F == 0x00 &&
            moby.Flag4A == 0x10 &&
            moby.Flag4B == 0xFF;
    }

    private static bool IsNativeDragonPedestal(Moby moby)
    {
        return moby.Type == 0x20 &&
            moby.SourceByte36 is 0x4B or 0x4C or 0x4D &&
            moby.SourceByte37 == 0x01 &&
            moby.SourceByte4F == 0x00 &&
            moby.Flag4A == 0x10 &&
            moby.Flag4B == 0xFF;
    }

    private static bool IsDragonSceneLinkControl(Moby moby)
    {
        return moby.Type == 0x00 &&
            moby.SourceByte36 == 0x6E &&
            moby.SourceByte37 == 0x00 &&
            moby.SourceByte4F == 0x00 &&
            moby.Flag4A == 0x10 &&
            moby.Flag4B == 0xFF;
    }

    private static bool IsNearbyDragonSceneLinkControl(Moby control, Moby dragon, Moby pedestal)
    {
        const float MaxDistanceSquared = 96 * 96;
        return DistanceSquared(control.Position, dragon.Position) <= MaxDistanceSquared &&
            DistanceSquared(control.Position, pedestal.Position) <= MaxDistanceSquared;
    }

    private static bool IsNearbyDragonSupport(Moby support, Moby dragon, Moby pedestal)
    {
        const float maxDistance = 256 * 256;
        return DistanceSquared(support.Position, dragon.Position) <= maxDistance &&
            DistanceSquared(support.Position, pedestal.Position) <= maxDistance;
    }

    private static int ApplySourceProvenPortalControlLinks(string levelKey, IList<Moby> mobys)
    {
        IReadOnlyList<HomeworldPortalControlDefinition> definitions = HomeworldPortalControlCatalog.ForLevel(levelKey);
        if (definitions.Count == 0)
            return 0;

        Dictionary<int, Moby> byTrueIndex = mobys
            .Where(moby => !moby.IsRemoved && moby.TrueIndex >= 0)
            .GroupBy(moby => moby.TrueIndex)
            .ToDictionary(group => group.Key, group => group.First());
        int count = 0;
        foreach (HomeworldPortalControlDefinition definition in definitions)
        {
            if (!byTrueIndex.TryGetValue(definition.PathTrueIndex, out Moby? path) ||
                !byTrueIndex.TryGetValue(definition.LetteringTrueIndex, out Moby? lettering) ||
                !byTrueIndex.TryGetValue(definition.CompanionTrueIndex, out Moby? companion) ||
                !IsPortalPathControl(path) ||
                !IsPortalDestinationControl(lettering) ||
                !IsPortalRouteControl(companion))
            {
                continue;
            }

            List<int> indexes = definition.TrueIndexes.Distinct().OrderBy(index => index).ToList();

            MobyLink link = new()
            {
                Key = $"{levelKey}:portal-controls:level-{definition.DestinationLevelId}",
                Name = $"{definition.DestinationName} portal location",
                Kind = "portal controls",
                LinkedMove = true,
                Confidence = "source-proven",
                Reason = $"The native homeworld portal table maps destination {definition.DestinationLevelId} to path T{definition.PathTrueIndex}; its unique nearby class 0x01 lettering and class 0x011E ambient-sound companion rows are T{definition.LetteringTrueIndex}/T{definition.CompanionTrueIndex}. Create BIN also moves the dedicated portal plane and type-6 entry collision surface. Decorative arch terrain remains separate.",
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

    private static bool IsPortalPathControl(Moby moby)
    {
        return !moby.IsRemoved &&
            moby.Type == 0x00 &&
            moby.SourceByte36 == 0x8E &&
            moby.Flag4A == 0x10 &&
            moby.Flag4B == 0xFF;
    }

    private static bool IsPortalDestinationControl(Moby moby)
    {
        return !moby.IsRemoved &&
            moby.Type == 0x00 &&
            moby.SourceByte36 == 0x01 &&
            moby.Flag4A == 0x10 &&
            moby.Flag4B == 0xFF;
    }

    private static bool IsPortalRouteControl(Moby moby)
    {
        return !moby.IsRemoved &&
            moby.Type == 0x00 &&
            moby.SourceByte36 == 0x1E &&
            moby.Flag4A == 0x10 &&
            moby.Flag4B == 0xFF;
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

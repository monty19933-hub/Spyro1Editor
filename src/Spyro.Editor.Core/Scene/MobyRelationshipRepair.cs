using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Scene;

public static class MobyRelationshipRepair
{
    public static int RepairChestContentLinks(string levelKey, IList<Moby> mobys)
    {
        Dictionary<int, Moby> byTrueIndex = BuildTrueIndexMap(mobys);
        CaptureAuthoritativeGemRelationshipBaselines(levelKey, mobys, byTrueIndex);

        // A newly added contained-gem row can predate its first explicit link. Preserve
        // the old nearby repair for that one case, but never reconsider a member that
        // already belongs to dormant authoritative topology.
        if (InferUnownedChestContentBaselines(levelKey, mobys, byTrueIndex) > 0)
            byTrueIndex = BuildTrueIndexMap(mobys);

        return ProjectCompatibleGemRelationships(levelKey, mobys, byTrueIndex);
    }

    public static int PruneIncompatibleGemLinks(IList<Moby> mobys)
    {
        int before = mobys.Sum(moby => moby.Links.Count(IsGemRelationshipLink));
        RepairChestContentLinks("", mobys);
        int after = mobys.Sum(moby => moby.Links.Count(IsGemRelationshipLink));
        return Math.Max(0, before - after);
    }

    public static bool SupportsChestRelationships(Moby moby, IEnumerable<Moby> mobys) =>
        SupportsChestRelationships("", moby, mobys);

    public static bool SupportsChestRelationships(string levelKey, Moby moby, IEnumerable<Moby> mobys) =>
        SupportsChestRelationships(levelKey, moby, BuildTrueIndexMap(mobys));

    public static bool SupportsRewardTriggerRelationships(Moby moby, IEnumerable<Moby> mobys) =>
        SupportsRewardTriggerRelationships("", moby, mobys);

    public static bool SupportsRewardTriggerRelationships(string levelKey, Moby moby, IEnumerable<Moby> mobys) =>
        SupportsRewardTriggerRelationships(levelKey, moby, BuildTrueIndexMap(mobys));

    private static void CaptureAuthoritativeGemRelationshipBaselines(
        string levelKey,
        IList<Moby> mobys,
        IReadOnlyDictionary<int, Moby> byTrueIndex)
    {
        List<MobyLink> activeCandidates = mobys
            .SelectMany(moby => moby.Links)
            .Where(IsGemRelationshipLink)
            .GroupBy(ActiveLinkIdentity, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        foreach (MobyLink candidate in activeCandidates)
        {
            if (!IsValidAuthoritativeBaseline(levelKey, candidate, byTrueIndex))
                continue;

            StoreOrMergeBaseline(candidate, mobys, byTrueIndex);
        }
    }

    private static bool IsValidAuthoritativeBaseline(
        string levelKey,
        MobyLink link,
        IReadOnlyDictionary<int, Moby> byTrueIndex)
    {
        GemRelationshipKind kind = RelationshipKind(link);
        if (kind == GemRelationshipKind.None || link.TrueIndexes.Count < 2)
            return false;
        if (link.TrueIndexes.Any(trueIndex => trueIndex < 0) ||
            link.TrueIndexes.Distinct().Count() != link.TrueIndexes.Count)
        {
            return false;
        }

        // The first declared index is authoritative. Do not search the rest of the
        // group for something that merely resembles a root.
        if (!byTrueIndex.TryGetValue(link.TrueIndexes[0], out Moby? root))
            return false;
        bool validRoot = kind == GemRelationshipKind.ChestContents
            ? SupportsOriginalChestRoot(root) || root.IsAdded && SupportsChestRelationships(levelKey, root, byTrueIndex)
            : SupportsOriginalRewardRoot(root) || root.IsAdded && SupportsRewardTriggerRelationships(levelKey, root, byTrueIndex);
        if (!validRoot)
            return false;

        foreach (int childTrueIndex in link.TrueIndexes.Skip(1))
        {
            if (!byTrueIndex.TryGetValue(childTrueIndex, out Moby? child))
                return false;
            bool validChild = kind == GemRelationshipKind.ChestContents
                ? SupportsOriginalChestContent(child) || child.IsAdded && SupportsChestContentRole(levelKey, child, byTrueIndex)
                : SupportsOriginalRewardTrigger(child) || child.IsAdded && SupportsRewardTriggerRole(levelKey, child, byTrueIndex);
            if (!validChild)
                return false;
        }

        return true;
    }

    private static void StoreOrMergeBaseline(
        MobyLink candidate,
        IList<Moby> mobys,
        IReadOnlyDictionary<int, Moby> byTrueIndex)
    {
        string groupIdentity = BaselineGroupIdentity(candidate);
        List<MobyLink> existing = mobys
            .SelectMany(moby => moby.DormantGemRelationshipLinks)
            .Where(link => string.Equals(BaselineGroupIdentity(link), groupIdentity, StringComparison.OrdinalIgnoreCase))
            .GroupBy(ActiveLinkIdentity, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        MobyLink metadataSource = existing
            .OrderByDescending(link => link.TrueIndexes.Count)
            .FirstOrDefault() ?? candidate;
        List<int> fullTopology = new() { candidate.TrueIndexes[0] };
        foreach (MobyLink link in existing.Append(candidate))
        {
            if (link.TrueIndexes.Count == 0 || link.TrueIndexes[0] != fullTopology[0])
                continue;
            foreach (int childTrueIndex in link.TrueIndexes.Skip(1))
            {
                if (!fullTopology.Contains(childTrueIndex))
                    fullTopology.Add(childTrueIndex);
            }
        }

        MobyLink baseline = CloneLink(metadataSource, fullTopology);
        foreach (Moby moby in mobys)
        {
            moby.DormantGemRelationshipLinks.RemoveAll(link =>
                string.Equals(BaselineGroupIdentity(link), groupIdentity, StringComparison.OrdinalIgnoreCase));
        }

        foreach (int trueIndex in fullTopology)
        {
            if (byTrueIndex.TryGetValue(trueIndex, out Moby? member))
                member.DormantGemRelationshipLinks.Add(baseline);
        }
    }

    private static int InferUnownedChestContentBaselines(
        string levelKey,
        IList<Moby> mobys,
        IReadOnlyDictionary<int, Moby> byTrueIndex)
    {
        List<MobyLink> baselines = GetCanonicalBaselines(mobys);
        HashSet<int> dormantMembers = baselines
            .SelectMany(link => link.TrueIndexes)
            .ToHashSet();
        HashSet<int> dormantChildren = baselines
            .SelectMany(link => link.TrueIndexes.Skip(1))
            .ToHashSet();
        List<Moby> roots = mobys
            .Where(moby => !moby.IsRemoved && !dormantChildren.Contains(moby.TrueIndex))
            .Where(moby => !dormantMembers.Contains(moby.TrueIndex) || CarriesDormantRootTopology(moby))
            .Where(moby => SupportsChestRelationships(levelKey, moby, byTrueIndex))
            .ToList();
        if (roots.Count == 0)
            return 0;

        Dictionary<int, List<Moby>> inferredByRoot = new();
        foreach (Moby content in mobys
            .Where(moby => !moby.IsRemoved && !dormantMembers.Contains(moby.TrueIndex))
            .Where(moby => SupportsChestContentRole(levelKey, moby, byTrueIndex)))
        {
            Moby? root = roots
                .OrderBy(candidate => DistanceSquared(candidate.Position, content.Position))
                .FirstOrDefault(candidate => DistanceSquared(candidate.Position, content.Position) <= 64 * 64);
            if (root == null)
                continue;

            if (!inferredByRoot.TryGetValue(root.TrueIndex, out List<Moby>? inferred))
            {
                inferred = new List<Moby>();
                inferredByRoot[root.TrueIndex] = inferred;
            }
            inferred.Add(content);
        }

        int inferredLinks = 0;
        foreach ((int rootTrueIndex, List<Moby> contents) in inferredByRoot)
        {
            MobyLink? existing = baselines.FirstOrDefault(link =>
                RelationshipKind(link) == GemRelationshipKind.ChestContents &&
                link.TrueIndexes.Count > 0 &&
                link.TrueIndexes[0] == rootTrueIndex);
            List<int> indexes = existing?.TrueIndexes.ToList() ?? new List<int> { rootTrueIndex };
            foreach (int contentTrueIndex in contents.Select(content => content.TrueIndex).Order())
            {
                if (!indexes.Contains(contentTrueIndex))
                    indexes.Add(contentTrueIndex);
            }

            MobyLink inferredLink = existing == null
                ? new MobyLink
                {
                    Key = $"{levelKey}:chest:{rootTrueIndex}",
                    Name = $"Chest contents for T{rootTrueIndex}",
                    Kind = "chest contents",
                    LinkedMove = true,
                    Confidence = "native-editor",
                    Reason = "A new contained-gem row was co-located with its compatible chest root.",
                    TrueIndexes = indexes
                }
                : CloneLink(existing, indexes);
            StoreOrMergeBaseline(inferredLink, mobys, byTrueIndex);
            inferredLinks++;
        }

        return inferredLinks;
    }

    private static int ProjectCompatibleGemRelationships(
        string levelKey,
        IList<Moby> mobys,
        IReadOnlyDictionary<int, Moby> byTrueIndex)
    {
        foreach (Moby moby in mobys)
            moby.Links.RemoveAll(IsGemRelationshipLink);

        int activeChestLinks = 0;
        foreach (MobyLink baseline in GetCanonicalBaselines(mobys))
        {
            GemRelationshipKind kind = RelationshipKind(baseline);
            if (kind == GemRelationshipKind.None || baseline.TrueIndexes.Count < 2)
                continue;
            if (!byTrueIndex.TryGetValue(baseline.TrueIndexes[0], out Moby? root) ||
                root.IsRemoved ||
                !CarriesDormantTopology(root, baseline))
                continue;

            bool rootIsCompatible = kind == GemRelationshipKind.ChestContents
                ? SupportsChestRelationships(levelKey, root, byTrueIndex)
                : SupportsRewardTriggerRelationships(levelKey, root, byTrueIndex);
            if (!rootIsCompatible)
                continue;

            List<int> activeIndexes = new() { root.TrueIndex };
            foreach (int childTrueIndex in baseline.TrueIndexes.Skip(1))
            {
                if (!byTrueIndex.TryGetValue(childTrueIndex, out Moby? child) ||
                    child.IsRemoved ||
                    !CarriesDormantTopology(child, baseline))
                    continue;
                bool childIsCompatible = kind == GemRelationshipKind.ChestContents
                    ? SupportsChestContentRole(levelKey, child, byTrueIndex)
                    : SupportsRewardTriggerRole(levelKey, child, byTrueIndex);
                if (childIsCompatible)
                    activeIndexes.Add(child.TrueIndex);
            }

            if (activeIndexes.Count < 2)
                continue;

            MobyLink active = CloneLink(baseline, activeIndexes);
            foreach (int trueIndex in activeIndexes)
            {
                if (byTrueIndex.TryGetValue(trueIndex, out Moby? member))
                    member.Links.Add(active);
            }
            if (kind == GemRelationshipKind.ChestContents)
                activeChestLinks++;
        }

        return activeChestLinks;
    }

    private static List<MobyLink> GetCanonicalBaselines(IEnumerable<Moby> mobys) =>
        mobys.SelectMany(moby => moby.DormantGemRelationshipLinks)
            .Where(IsGemRelationshipLink)
            .GroupBy(BaselineGroupIdentity, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(link => link.TrueIndexes.Count).First())
            .OrderBy(link => link.TrueIndexes.Count == 0 ? int.MaxValue : link.TrueIndexes[0])
            .ThenBy(link => link.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool SupportsChestRelationships(
        string levelKey,
        Moby moby,
        IReadOnlyDictionary<int, Moby> byTrueIndex)
    {
        if (TryResolveSameLevelDonor(levelKey, moby, byTrueIndex, out Moby donor))
            return SupportsOriginalChestRoot(donor);
        if (moby.SourceCloneTrueIndex >= 0 || HasCrossLevelIdentity(moby))
            return SupportsCrossLevelChestRoot(moby);

        return SupportsOriginalChestRoot(moby) &&
            moby.Type == moby.OriginalType &&
            moby.SourceByte36 == moby.OriginalSourceByte36 &&
            moby.SourceByte37 == moby.OriginalSourceByte37 &&
            moby.SourceByte4F == moby.OriginalSourceByte4F &&
            moby.Flag4A == moby.OriginalFlag4A;
    }

    private static bool SupportsRewardTriggerRelationships(
        string levelKey,
        Moby moby,
        IReadOnlyDictionary<int, Moby> byTrueIndex)
    {
        if (TryResolveSameLevelDonor(levelKey, moby, byTrueIndex, out Moby donor))
            return SupportsOriginalRewardRoot(donor);
        if (moby.SourceCloneTrueIndex >= 0 || HasCrossLevelIdentity(moby))
            return SupportsCrossLevelRewardRoot(moby);

        return SupportsCurrentRewardRoot(moby);
    }

    private static bool SupportsChestContentRole(
        string levelKey,
        Moby moby,
        IReadOnlyDictionary<int, Moby> byTrueIndex)
    {
        if (TryResolveSameLevelDonor(levelKey, moby, byTrueIndex, out Moby donor))
            return SupportsOriginalChestContent(donor);
        if (moby.SourceCloneTrueIndex >= 0 || HasCrossLevelIdentity(moby))
            return SupportsCrossLevelChestContent(moby);

        return SupportsCurrentChestContent(moby);
    }

    private static bool SupportsRewardTriggerRole(
        string levelKey,
        Moby moby,
        IReadOnlyDictionary<int, Moby> byTrueIndex)
    {
        if (TryResolveSameLevelDonor(levelKey, moby, byTrueIndex, out Moby donor))
            return SupportsOriginalRewardTrigger(donor);
        if (moby.SourceCloneTrueIndex >= 0 || HasCrossLevelIdentity(moby))
            return SupportsCrossLevelRewardTrigger(moby);

        return SupportsCurrentRewardTrigger(moby);
    }

    private static bool TryResolveSameLevelDonor(
        string levelKey,
        Moby moby,
        IReadOnlyDictionary<int, Moby> byTrueIndex,
        out Moby donor)
    {
        donor = null!;
        if (moby.SourceCloneTrueIndex < 0 ||
            string.IsNullOrWhiteSpace(levelKey) ||
            !LevelKeysEqual(moby.SourceCloneLevelKey, levelKey))
        {
            return false;
        }

        if (!byTrueIndex.TryGetValue(moby.SourceCloneTrueIndex, out Moby? resolved) || resolved == null)
            return false;
        donor = resolved;
        return true;
    }

    private static bool SupportsOriginalChestRoot(Moby moby)
    {
        if (SupportsOriginalChestContent(moby))
            return false;

        string originalIdentity = NormalizeIdentity(moby.OriginalLabel);
        if (IsNonRootCompanionIdentity(originalIdentity))
        {
            return false;
        }
        return IsKnownChestNativeIdentity(
                moby.OriginalType,
                moby.OriginalSourceByte36,
                moby.OriginalSourceByte37) ||
            originalIdentity.Contains("chest", StringComparison.Ordinal) &&
            !originalIdentity.Contains("chestcontent", StringComparison.Ordinal);
    }

    private static bool SupportsOriginalChestContent(Moby moby) =>
        moby.OriginalType == 0x00 &&
        moby.OriginalFlag4A == 0xFF &&
        GemValue.TryFromIdByte(moby.OriginalFlag4B, out _);

    private static bool SupportsOriginalRewardRoot(Moby moby) =>
        moby.OriginalType == 0x20 &&
        moby.OriginalSourceByte36 == 0x53 &&
        moby.OriginalSourceByte37 == 0x01 &&
        GemValue.TryFromIdByte(moby.OriginalFlag4B, out _);

    private static bool SupportsOriginalRewardTrigger(Moby moby) =>
        moby.OriginalType == 0x00 &&
        moby.OriginalSourceByte36 == 0x5F &&
        moby.OriginalSourceByte37 == 0x01 &&
        moby.OriginalFlag4A == 0x10 &&
        GemValue.TryFromIdByte(moby.OriginalFlag4B, out _);

    private static bool SupportsCurrentChestContent(Moby moby) =>
        moby.Type == 0x00 &&
        moby.Flag4A == 0xFF &&
        GemValue.TryFromIdByte(moby.Flag4B, out _);

    private static bool SupportsCurrentRewardRoot(Moby moby) =>
        moby.Type == 0x20 &&
        moby.SourceByte36 == 0x53 &&
        moby.SourceByte37 == 0x01 &&
        GemValue.TryFromIdByte(moby.Flag4B, out _);

    private static bool SupportsCurrentRewardTrigger(Moby moby) =>
        moby.Type == 0x00 &&
        moby.SourceByte36 == 0x5F &&
        moby.SourceByte37 == 0x01 &&
        moby.Flag4A == 0x10 &&
        GemValue.TryFromIdByte(moby.Flag4B, out _);

    private static bool SupportsCrossLevelChestRoot(Moby moby)
    {
        string identity = CrossLevelIdentity(moby);
        if (IsChestContentIdentity(identity) || IsNonRootCompanionIdentity(identity))
            return false;
        return identity.Contains("chest", StringComparison.Ordinal) ||
            IsKnownChestNativeIdentity(moby.Type, moby.SourceByte36, moby.SourceByte37);
    }

    private static bool SupportsCrossLevelChestContent(Moby moby)
    {
        string identity = CrossLevelIdentity(moby);
        return IsChestContentIdentity(identity) || SupportsCurrentChestContent(moby);
    }

    private static bool SupportsCrossLevelRewardRoot(Moby moby)
    {
        string identity = CrossLevelIdentity(moby);
        if (IsChestContentIdentity(identity) || IsNonRootCompanionIdentity(identity))
            return false;
        return identity.Contains("treasurethief", StringComparison.Ordinal) ||
            identity.Contains("treasuregnorc", StringComparison.Ordinal) ||
            SupportsCurrentRewardRoot(moby);
    }

    private static bool SupportsCrossLevelRewardTrigger(Moby moby)
    {
        string identity = CrossLevelIdentity(moby);
        return identity.Contains("rewardtrigger", StringComparison.Ordinal) ||
            SupportsCurrentRewardTrigger(moby);
    }

    private static bool IsKnownChestNativeIdentity(int type, int sourceByte36, int sourceByte37) =>
        type is 0x18 or 0x20 &&
        (sourceByte36 == 0xAE && sourceByte37 == 0x00 ||
         sourceByte37 == 0x01 && sourceByte36 is 0x38 or 0x49 or 0x86 ||
         sourceByte36 == 0xC2 && sourceByte37 == 0x00);

    private static bool HasCrossLevelIdentity(Moby moby) =>
        !string.IsNullOrWhiteSpace(moby.CrossLevelTemplateId) ||
        !string.IsNullOrWhiteSpace(moby.CrossLevelFamily);

    private static string CrossLevelIdentity(Moby moby) =>
        NormalizeIdentity($"{moby.CrossLevelTemplateId} {moby.CrossLevelFamily}");

    private static bool IsChestContentIdentity(string normalizedIdentity) =>
        normalizedIdentity.Contains("chestcontent", StringComparison.Ordinal) ||
        normalizedIdentity.Contains("containedgem", StringComparison.Ordinal);

    private static bool IsNonRootCompanionIdentity(string normalizedIdentity) =>
        normalizedIdentity.Contains("controller", StringComparison.Ordinal) ||
        normalizedIdentity.Contains("controlmarker", StringComparison.Ordinal) ||
        normalizedIdentity.Contains("trigger", StringComparison.Ordinal) ||
        normalizedIdentity.Contains("helper", StringComparison.Ordinal);

    private static bool LevelKeysEqual(string left, string right) =>
        string.Equals(NormalizeIdentity(left), NormalizeIdentity(right), StringComparison.Ordinal);

    private static string NormalizeIdentity(string? value) =>
        new((value ?? "")
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static Dictionary<int, Moby> BuildTrueIndexMap(IEnumerable<Moby> mobys) =>
        mobys.Where(moby => moby.TrueIndex >= 0)
            .GroupBy(moby => moby.TrueIndex)
            .ToDictionary(group => group.Key, group => group.First());

    private static GemRelationshipKind RelationshipKind(MobyLink link)
    {
        if (string.Equals(link.Kind, "chest contents", StringComparison.OrdinalIgnoreCase))
            return GemRelationshipKind.ChestContents;

        string text = $"{link.Key} {link.Name} {link.Kind} {link.Confidence} {link.Reason}";
        return text.Contains("treasure", StringComparison.OrdinalIgnoreCase) &&
            text.Contains("reward", StringComparison.OrdinalIgnoreCase) &&
            text.Contains("trigger", StringComparison.OrdinalIgnoreCase)
                ? GemRelationshipKind.RewardTriggers
                : GemRelationshipKind.None;
    }

    private static bool IsGemRelationshipLink(MobyLink link) =>
        RelationshipKind(link) != GemRelationshipKind.None;

    private static string BaselineGroupIdentity(MobyLink link)
    {
        int rootTrueIndex = link.TrueIndexes.Count == 0 ? -1 : link.TrueIndexes[0];
        string key = string.IsNullOrWhiteSpace(link.Key) ? $"root:{rootTrueIndex}" : link.Key.Trim();
        return $"{RelationshipKind(link)}|{key}|root:{rootTrueIndex}";
    }

    private static string ActiveLinkIdentity(MobyLink link) =>
        $"{BaselineGroupIdentity(link)}|{string.Join(',', link.TrueIndexes)}";

    private static bool CarriesDormantTopology(Moby moby, MobyLink baseline) =>
        moby.DormantGemRelationshipLinks.Any(link =>
            string.Equals(BaselineGroupIdentity(link), BaselineGroupIdentity(baseline), StringComparison.OrdinalIgnoreCase) &&
            link.TrueIndexes.SequenceEqual(baseline.TrueIndexes));

    private static bool CarriesDormantRootTopology(Moby moby) =>
        moby.DormantGemRelationshipLinks.Any(link =>
            link.TrueIndexes.Count > 0 && link.TrueIndexes[0] == moby.TrueIndex);

    private static MobyLink CloneLink(MobyLink source, IReadOnlyList<int> trueIndexes) => new()
    {
        Key = source.Key,
        Name = source.Name,
        Kind = source.Kind,
        LinkedMove = source.LinkedMove,
        Confidence = source.Confidence,
        Reason = source.Reason,
        TrueIndexes = trueIndexes.ToArray()
    };

    private static float DistanceSquared(Vector3f a, Vector3f b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    private enum GemRelationshipKind
    {
        None,
        ChestContents,
        RewardTriggers
    }
}

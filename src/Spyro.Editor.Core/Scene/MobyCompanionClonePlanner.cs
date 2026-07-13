namespace Spyro.Editor.Core.Scene;

public static class MobyCompanionClonePlanner
{
    public static IReadOnlyList<Moby> GetCompanionDonors(Moby root, IEnumerable<Moby> mobys)
    {
        Dictionary<int, Moby> byTrueIndex = mobys
            .Where(moby => moby.TrueIndex >= 0 && !moby.IsRemoved)
            .ToDictionary(moby => moby.TrueIndex);
        List<Moby> companions = new();
        HashSet<int> seen = new();

        foreach (MobyLink link in root.Links.Where(IsCompanionCloneLink))
        {
            foreach (int trueIndex in link.TrueIndexes)
            {
                if (trueIndex == root.TrueIndex || !seen.Add(trueIndex))
                    continue;
                if (!byTrueIndex.TryGetValue(trueIndex, out Moby? candidate))
                    continue;
                if (IsCloneableCompanion(root, candidate, link))
                    companions.Add(candidate);
            }
        }

        return companions;
    }

    public static bool IsCompanionCloneLink(MobyLink link)
    {
        if (!MobyLinkTraversal.IsActiveMoveLink(link))
            return false;
        if (link.TrueIndexes.Count is < 2 or > 6)
            return false;

        string kind = link.Kind.ToLowerInvariant();
        string text = $"{link.Key} {link.Name} {link.Kind} {link.Confidence} {link.Reason}".ToLowerInvariant();
        if (kind is "chest contents" or "dragon scene" or "dragon pedestal")
            return true;
        if (kind.Contains("reward", StringComparison.Ordinal) &&
            text.Contains("trigger", StringComparison.Ordinal))
        {
            return true;
        }
        if (kind == "portal group")
            return text.Contains("entry trigger", StringComparison.Ordinal) ||
                text.Contains("portal entry", StringComparison.Ordinal);
        if (kind == "linked group")
            return text.Contains("helper", StringComparison.Ordinal) ||
                text.Contains("control", StringComparison.Ordinal) ||
                text.Contains("trigger", StringComparison.Ordinal);

        return false;
    }

    public static bool IsCloneableCompanion(Moby root, Moby companion, MobyLink link)
    {
        if (companion.IsRemoved || companion.TrueIndex == root.TrueIndex)
            return false;

        string kind = link.Kind.ToLowerInvariant();
        string linkText = $"{link.Key} {link.Name} {link.Kind} {link.Confidence} {link.Reason}".ToLowerInvariant();
        if (kind == "chest contents")
            return companion.IsChestContent;
        if (kind is "dragon scene" or "dragon pedestal")
            return companion.VisualKind is MobyVisualKind.Control or MobyVisualKind.Dragon;
        if (kind == "portal group")
            return companion.VisualKind is MobyVisualKind.Control or MobyVisualKind.Portal;
        if (kind.Contains("reward", StringComparison.Ordinal))
            return companion.VisualKind == MobyVisualKind.Control ||
                ContainsCompanionText(companion);
        if (kind == "linked group" &&
            linkText.Contains("3x flame", StringComparison.Ordinal) &&
            linkText.Contains("fan", StringComparison.Ordinal) &&
            companion.Type == 0x1A &&
            companion.SourceByte36 == 0x88)
        {
            return true;
        }

        return companion.VisualKind == MobyVisualKind.Control ||
            companion.IsChestContent ||
            ContainsCompanionText(companion);
    }

    public static bool IsLinkedCompanionCloneText(string text)
    {
        return text.Contains("linked companion", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("companion cloned", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsCompanionText(Moby moby)
    {
        string text = $"{moby.DisplayLabel} {moby.CandidateKind} {moby.BehaviorNote}".ToLowerInvariant();
        return text.Contains("helper", StringComparison.Ordinal) ||
            text.Contains("control", StringComparison.Ordinal) ||
            text.Contains("trigger", StringComparison.Ordinal) ||
            text.Contains("linked record", StringComparison.Ordinal) ||
            text.Contains("reward link", StringComparison.Ordinal);
    }
}

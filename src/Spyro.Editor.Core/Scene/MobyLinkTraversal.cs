namespace Spyro.Editor.Core.Scene;

public static class MobyLinkTraversal
{
    public static IEnumerable<Moby> GetLinkedMoveMobys(Moby selected, IEnumerable<Moby> mobys)
    {
        yield return selected;

        HashSet<int> linkedTrueIndexes = selected.Links
            .Where(IsActiveMoveLink)
            .SelectMany(link => link.TrueIndexes)
            .Where(trueIndex => trueIndex != selected.TrueIndex)
            .ToHashSet();

        foreach (Moby moby in mobys)
        {
            if (!moby.IsRemoved && linkedTrueIndexes.Contains(moby.TrueIndex))
                yield return moby;
        }
    }

    public static IEnumerable<MobyLink> GetVisibleLinks(Moby selected)
    {
        return selected.Links.Where(link => link.LinkedMove && IsVisibleLink(link));
    }

    public static HashSet<int> GetVisibleLinkedTrueIndexes(Moby selected)
    {
        return GetVisibleLinks(selected)
            .SelectMany(link => GetVisibleLinkTrueIndexes(selected, link))
            .ToHashSet();
    }

    public static IEnumerable<int> GetVisibleLinkTrueIndexes(Moby selected, MobyLink link)
    {
        return link.TrueIndexes.Where(trueIndex => trueIndex != selected.TrueIndex);
    }

    public static bool IsActiveMoveLink(MobyLink link)
    {
        return link.LinkedMove && IsVisibleLink(link);
    }

    public static bool IsVisibleLink(MobyLink link)
    {
        return !IsBroadEditorScaffold(link);
    }

    private static bool IsBroadEditorScaffold(MobyLink link)
    {
        string text = $"{link.Key} {link.Name} {link.Kind} {link.Confidence} {link.Reason}";
        if (text.Contains("live-proof-control-role", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("control-role-proof", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
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
            IsBroadPortalCluster(link, text);
    }

    private static bool IsBroadPortalCluster(MobyLink link, string text)
    {
        return link.TrueIndexes.Count > 6 &&
            text.Contains("portal", StringComparison.OrdinalIgnoreCase) &&
            (text.Contains("cluster", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("pad/arch", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("collision/warp", StringComparison.OrdinalIgnoreCase));
    }

}

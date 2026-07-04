using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Scene;

public static class MobyRelationshipRepair
{
    public static int RepairChestContentLinks(string levelKey, IList<Moby> mobys)
    {
        int repaired = 0;
        foreach (Moby chest in mobys.Where(moby => !moby.IsRemoved && moby.IsChest).ToList())
        {
            List<Moby> contents = mobys
                .Where(moby => !moby.IsRemoved && moby.IsChestContent)
                .Where(content => content.Links.Any(link =>
                    string.Equals(link.Kind, "chest contents", StringComparison.OrdinalIgnoreCase) &&
                    link.TrueIndexes.Contains(chest.TrueIndex)) ||
                    DistanceSquared(content.Position, chest.Position) <= 64 * 64)
                .OrderBy(content => content.TrueIndex)
                .ToList();

            string key = $"{levelKey}:chest:{chest.TrueIndex}";
            foreach (Moby moby in mobys)
            {
                moby.Links.RemoveAll(existing =>
                    string.Equals(existing.Key, key, StringComparison.OrdinalIgnoreCase) ||
                    (string.Equals(existing.Kind, "chest contents", StringComparison.OrdinalIgnoreCase) && existing.TrueIndexes.Contains(chest.TrueIndex)));
            }

            if (contents.Count == 0)
                continue;

            List<int> indexes = new() { chest.TrueIndex };
            indexes.AddRange(contents.Select(content => content.TrueIndex));
            MobyLink link = new()
            {
                Key = key,
                Name = $"Chest contents for T{chest.TrueIndex}",
                Kind = "chest contents",
                LinkedMove = true,
                Confidence = "native-editor",
                Reason = "Chest contents were loaded or repaired by the native editor.",
                TrueIndexes = indexes
            };

            chest.Links.Add(link);
            foreach (Moby content in contents)
                content.Links.Add(link);
            repaired++;
        }

        return repaired;
    }

    private static float DistanceSquared(Vector3f a, Vector3f b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }
}

using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Scene;

public static class MobyCompanionCloneBuilder
{
    public static int AddLinkedCompanionClones(
        Moby sourceRoot,
        Moby addedRoot,
        IEnumerable<Moby> sourceMobys,
        IList<Moby> addedMobys,
        ref int nextIndex,
        ref int nextTrueIndex,
        string sourceLevelKey,
        string sourceLevelName)
    {
        IReadOnlyList<Moby> companionDonors = MobyCompanionClonePlanner.GetCompanionDonors(sourceRoot, sourceMobys);
        if (companionDonors.Count == 0)
            return 0;

        Dictionary<int, int> sourceToCloneTrueIndex = new()
        {
            [sourceRoot.TrueIndex] = addedRoot.TrueIndex
        };

        int added = 0;
        foreach (Moby donor in companionDonors.OrderBy(moby => moby.TrueIndex))
        {
            Vector3f companionPosition = new(
                addedRoot.Position.X + (donor.Position.X - sourceRoot.Position.X),
                addedRoot.Position.Y + (donor.Position.Y - sourceRoot.Position.Y),
                addedRoot.Position.Z + (donor.Position.Z - sourceRoot.Position.Z));
            Moby clone = CreateLinkedCompanionMoby(donor, sourceRoot, addedRoot, companionPosition, nextIndex, nextTrueIndex, sourceLevelKey, sourceLevelName);
            addedMobys.Add(clone);
            sourceToCloneTrueIndex[donor.TrueIndex] = clone.TrueIndex;
            nextIndex++;
            nextTrueIndex++;
            added++;
        }

        ApplyClonedCompanionLinks(sourceRoot, addedMobys, sourceToCloneTrueIndex);
        return added;
    }

    private static Moby CreateLinkedCompanionMoby(
        Moby donor,
        Moby sourceRoot,
        Moby addedRoot,
        Vector3f position,
        int index,
        int trueIndex,
        string sourceLevelKey,
        string sourceLevelName)
    {
        string label = donor.IsChestContent
            ? donor.DisplayLabel
            : $"Linked companion for {addedRoot.DisplayLabel}: {donor.DisplayLabel}";
        return new Moby
        {
            Index = index,
            TrueIndex = trueIndex,
            LegacyIndex = MobyLoader.GetLegacyAliasIndex(trueIndex),
            Position = position,
            OriginalPosition = position,
            Type = donor.Type,
            OriginalType = donor.Type,
            State = donor.State,
            OriginalState = donor.State,
            YawByte = donor.YawByte >= 0 ? donor.YawByte : 0,
            OriginalYawByte = donor.YawByte >= 0 ? donor.YawByte : 0,
            SourceByte36 = donor.SourceByte36,
            OriginalSourceByte36 = donor.SourceByte36,
            SourceByte37 = donor.SourceByte37,
            OriginalSourceByte37 = donor.SourceByte37,
            SourceByte4F = donor.SourceByte4F,
            OriginalSourceByte4F = donor.SourceByte4F,
            Flag4A = donor.Flag4A,
            OriginalFlag4A = donor.Flag4A,
            Flag4B = donor.Flag4B,
            OriginalFlag4B = donor.Flag4B,
            Color = donor.Color,
            Label = label,
            OriginalLabel = label,
            PatchStatus = "native-clone",
            PatchLead = $"Linked companion cloned with {addedRoot.DisplayLabel} from same-level donor T{donor.TrueIndex}; source root was T{sourceRoot.TrueIndex}. Create BIN reuses a matching same-level source slot when one is available.",
            CandidateKind = donor.CandidateKind,
            Confidence = "same-level-linked-companion-clone",
            Evidence = $"Companion copied from same-level linked donor T{donor.TrueIndex}.",
            BehaviorNote = string.IsNullOrWhiteSpace(donor.BehaviorNote)
                ? $"Linked companion for cloned donor T{sourceRoot.TrueIndex}."
                : donor.BehaviorNote,
            ZoneLabel = donor.ZoneLabel,
            SourceCloneLevelKey = sourceLevelKey,
            SourceCloneLevelName = sourceLevelName,
            SourceCloneTrueIndex = donor.TrueIndex,
            IsAdded = true
        };
    }

    private static void ApplyClonedCompanionLinks(Moby sourceRoot, IEnumerable<Moby> addedMobys, IReadOnlyDictionary<int, int> sourceToCloneTrueIndex)
    {
        Dictionary<int, Moby> clonesByTrueIndex = addedMobys
            .Where(moby => moby.TrueIndex >= 0)
            .ToDictionary(moby => moby.TrueIndex);
        int groupIndex = 0;
        foreach (MobyLink sourceLink in sourceRoot.Links.Where(MobyCompanionClonePlanner.IsCompanionCloneLink))
        {
            List<int> clonedTrueIndexes = sourceLink.TrueIndexes
                .Where(sourceToCloneTrueIndex.ContainsKey)
                .Select(sourceTrueIndex => sourceToCloneTrueIndex[sourceTrueIndex])
                .Distinct()
                .ToList();
            if (clonedTrueIndexes.Count < 2)
                continue;

            MobyLink clonedLink = new()
            {
                Key = $"native-editor:companion:{sourceRoot.TrueIndex}:{clonedTrueIndexes[0]}:{groupIndex++}",
                Name = $"Copied {sourceLink.DisplayName}",
                Kind = sourceLink.Kind,
                LinkedMove = true,
                Confidence = "native-editor-companion",
                Reason = $"Copied from source link {sourceLink.Key}.",
                TrueIndexes = clonedTrueIndexes
            };

            foreach (int trueIndex in clonedTrueIndexes)
            {
                if (clonesByTrueIndex.TryGetValue(trueIndex, out Moby? clone) &&
                    !clone.Links.Any(existing => string.Equals(existing.Key, clonedLink.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    clone.Links.Add(clonedLink);
                }
            }
        }
    }
}

using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Universal;

public sealed record UniversalPlayerAnimationImport(string SourceLevelKey, IReadOnlyList<int> RequestedAnimationIds,
    IReadOnlyList<int> AddedAnimationIds, IReadOnlyList<int> ReusedLocalAnimationIds,
    IReadOnlyList<int> ReusedSharedAnimationIds, int InsertedBytes, int ModelGrowthBytes);

internal static class UniversalPlayerAnimationImporter
{
    internal static (UniversalLevelData Data, UniversalPlayerAnimationImport Report) Merge(
        UniversalLevelData destination, string sourceKey, int sourceLevelId, IReadOnlyList<int> animationIds,
        NativePlayerAnimationSource source)
    {
        var donor = source.Levels.Single(l => l.LevelId == sourceLevelId);
        var textureOwner = source.Levels.Single(l => l.LevelId == 10).Stream.Records.Single(r => r.AnimationId == 18).CopyRecord();
        foreach (int id in animationIds)
        {
            var record = donor.Stream.Records.SingleOrDefault(r => r.AnimationId == id);
            if (record == null && source.SharedAnimationIds.Contains(id)) continue;
            if (record == null)
                throw new InvalidDataException($"{sourceKey} requires Spyro animation {id}, which its original level does not supply.");
            // These source-owned records use the existing shared Spyro geometry and texture header.
            // A non-FF frame selector needs a separately imported global sound dependency.
            if (record.Frames.Any(f => f.UpperByte != 0xFF))
                throw new InvalidDataException($"Spyro animation {id} from {sourceKey} needs a global sound dependency that is not supported yet.");
            if (!record.CopyRecord().AsSpan(0x0A, 0x22).SequenceEqual(textureOwner.AsSpan(0x0A, 0x22)))
                throw new InvalidDataException($"Spyro animation {id} from {sourceKey} uses a different shared texture or geometry header.");
        }
        var merged = source.MergeCaptured(destination.Bytes, sourceLevelId, animationIds);
        var result = new UniversalLevelData(merged.CopyFinalBytes());
        return (result, new(sourceKey, merged.RequestedIds, merged.AddedIds, merged.ReusedLocalIds,
            merged.ReusedSharedIds, merged.ExactGrowth, result.Subfiles()[2].Length - destination.Subfiles()[2].Length));
    }
}

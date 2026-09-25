namespace Spyro.Editor.Core.Persistence;

/// <summary>
/// Pure required-for-100% policy. Does not bind native counters, hook a game/card consumer,
/// mutate saved state or grant publication authority. See detached-collectible-completion-policy.md.
/// </summary>
internal static class DetachedCollectibleCompletionPolicy
{
    internal const int PolicyVersion = 1;
    internal const int RequiredBaseGems = 12000;
    internal const int RequiredBaseDragons = 80;
    internal const int RequiredBaseOrdinaryEggs = 12;
    internal const int GemScore = 50;
    internal const int DragonScore = 6000;
    internal const int EggScore = 10000;
    internal const long OrdinaryRequiredScore = 1200000;

    /// <summary>
    /// Mathematical input domain, not a native corrupt-save arithmetic emulator.
    /// Dragons/ordinary eggs retain the base profile; gems may include bonus treasure.
    /// </summary>
    internal sealed class OrdinaryProgress
    {
        internal OrdinaryProgress(int gems, int dragons, int ordinaryEggs)
        {
            if (gems < 0) throw new ArgumentOutOfRangeException(nameof(gems), "Gem count must be a nonnegative Int32.");
            if (dragons < 0 || dragons > RequiredBaseDragons)
                throw new ArgumentOutOfRangeException(nameof(dragons), "This unchanged ordinary profile admits 0..80 dragons.");
            if (ordinaryEggs < 0 || ordinaryEggs > RequiredBaseOrdinaryEggs)
                throw new ArgumentOutOfRangeException(nameof(ordinaryEggs), "This unchanged ordinary profile admits 0..12 ordinary eggs.");
            Gems = gems; Dragons = dragons; OrdinaryEggs = ordinaryEggs;
        }
        internal int Gems { get; }
        internal int Dragons { get; }
        internal int OrdinaryEggs { get; }
    }

    internal sealed record InventoryEntry(Guid InstanceId, int BitIndex, bool Earned);

    internal sealed class Snapshot
    {
        internal Snapshot(DetachedCollectibleManifest manifest, int ordinarySlot, OrdinaryProgress ordinary,
            InventoryEntry[] activeInventory, long requiredScore, long earnedBaseScore, int percent, bool baseComplete)
        {
            ContentId = manifest.ContentId; ManifestRevision = manifest.Revision; ManifestSha256 = manifest.Sha256;
            OrdinarySlot = ordinarySlot; Ordinary = ordinary;
            ActiveInventory = Array.AsReadOnly(activeInventory.ToArray());
            RequiredDragonflyCount = activeInventory.Length;
            EarnedDragonflyCount = activeInventory.Count(entry => entry.Earned);
            RequiredScore = requiredScore; EarnedBaseScore = earnedBaseScore;
            Percent = percent; RequiredBaseComplete = baseComplete;
        }
        internal Guid ContentId { get; }
        internal uint ManifestRevision { get; }
        internal string ManifestSha256 { get; }
        internal int OrdinarySlot { get; }
        internal OrdinaryProgress Ordinary { get; }
        internal IReadOnlyList<InventoryEntry> ActiveInventory { get; }
        internal int RequiredDragonflyCount { get; }
        internal int EarnedDragonflyCount { get; }
        internal int RemainingDragonflyCount => RequiredDragonflyCount - EarnedDragonflyCount;
        internal bool UsesRequiredDragonflyPolicy => RequiredDragonflyCount != 0;
        internal long RequiredScore { get; }
        internal long EarnedBaseScore { get; }
        internal int Percent { get; }
        // Deliberately distinct from Percent >= 100: the untouched zero-custom native
        // bonus shortcut can report >=100 even with inconsistent ordinary counters.
        internal bool RequiredBaseComplete { get; }
    }

    internal static Snapshot Evaluate(DetachedCollectibleSave save, int ordinarySlot, OrdinaryProgress ordinary)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(ordinary);
        if (ordinarySlot < 0 || ordinarySlot >= DetachedCollectibleSave.OrdinarySlotCount)
            throw new ArgumentOutOfRangeException(nameof(ordinarySlot), "Only independent ordinary slots 0, 1 and 2 are admitted.");
        InventoryEntry[] inventory = save.Manifest.Entries.Where(entry => !entry.Retired)
            .Select(entry => new InventoryEntry(entry.InstanceId, entry.BitIndex, save.IsEarned(ordinarySlot, entry.InstanceId))).ToArray();
        int required = inventory.Length, earned = inventory.Count(entry => entry.Earned);
        bool baseComplete = ordinary.Gems >= RequiredBaseGems && ordinary.Dragons == RequiredBaseDragons &&
            ordinary.OrdinaryEggs == RequiredBaseOrdinaryEggs && earned == required;
        long requiredScore = checked(OrdinaryRequiredScore + (long)EggScore * required);
        long earnedBaseScore = checked((long)GemScore * Math.Min(ordinary.Gems, RequiredBaseGems) +
            (long)DragonScore * ordinary.Dragons + (long)EggScore * ordinary.OrdinaryEggs + (long)EggScore * earned);
        int percent;
        if (required == 0)
        {
            // Exact documented native mathematical formula on the admitted input domain.
            // Preserve its >12000-gem shortcut; do not retrofit a new completion gate.
            percent = ordinary.Gems > RequiredBaseGems
                ? BonusPercent(ordinary.Gems)
                : checked((int)(earnedBaseScore / 12000));
        }
        else if (baseComplete) percent = BonusPercent(ordinary.Gems);
        else percent = checked((int)(checked(100L * earnedBaseScore) / requiredScore));
        return new(save.Manifest, ordinarySlot, ordinary, inventory, requiredScore, earnedBaseScore, percent, baseComplete);
    }

    private static int BonusPercent(int gems) => checked(100 + (gems - RequiredBaseGems) / 100);
}

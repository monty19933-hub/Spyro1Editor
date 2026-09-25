using Spyro.Editor.Core.Persistence;

internal static class Program
{
    private static int checks;
    private static readonly Guid Content = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
    private static Guid Id(int index) => Guid.Parse($"10000000-0000-0000-0000-{index:x12}");
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static void Refuses(Action action, string message)
    {
        try { action(); }
        catch (ArgumentException) { Check(true, message); return; }
        catch (InvalidDataException) { Check(true, message); return; }
        catch (NotSupportedException) { Check(true, message); return; }
        throw new Exception("Expected refusal: " + message);
    }
    private static DetachedCollectibleSave Create(int required, int earned = 0, int slot = 0)
    {
        var manifest = DetachedCollectibleManifest.CreateNew(Content, Enumerable.Range(0, required).Select(Id));
        var state = DetachedCollectibleSave.CreateNew(manifest);
        for (int index = 0; index < earned; index++) state = state.WithCollected(slot, Id(index));
        return state;
    }
    private static DetachedCollectibleCompletionPolicy.Snapshot Evaluate(DetachedCollectibleSave save, int gems, int dragons, int eggs, int slot = 0) =>
        DetachedCollectibleCompletionPolicy.Evaluate(save, slot, new(gems, dragons, eggs));
    private static int NativeOracle(int gems, int dragons, int eggs) => gems > 12000
        ? 100 + (gems - 12000) / 100 : (int)(((long)gems * 50 + dragons * 6000L + eggs * 10000L) / 12000);

    private static void Main(string[] args)
    {
        if (args.Length != 0) throw new InvalidDataException("This source-free policy smoke accepts no paths or options.");
        LegacyExactness();
        RequiredEggGoldens();
        MonotonicProgress();
        SlotInventoryAndRetirement();
        BoundsAndCapacity();
        Console.WriteLine($"DETACHED_COLLECTIBLE_COMPLETION_SOURCE_FREE_OK checks={checks} sourceReads=0 candidateWrites=0 cardWrites=0 nativeHooks=false");
    }

    private static void LegacyExactness()
    {
        var empty = Create(0);
        foreach (int gems in new[] { 0, 1, 99, 100, 239, 240, 1000, 11999, 12000, 12001, 12099, 12100, 13999, 14000, int.MaxValue })
            for (int dragons = 0; dragons <= 80; dragons++)
                for (int eggs = 0; eggs <= 12; eggs++)
                {
                    var result = Evaluate(empty, gems, dragons, eggs);
                    Check(result.Percent == NativeOracle(gems, dragons, eggs), "zero-custom preserves native integer weighting and bonus shortcut");
                    Check(!result.UsesRequiredDragonflyPolicy && result.RequiredDragonflyCount == 0 && result.EarnedDragonflyCount == 0,
                        "zero-custom has no fabricated inventory or required-egg path");
                }
        Check(Evaluate(empty, 12000, 80, 12).Percent == 100, "native complete golden");
        Check(Evaluate(empty, 14000, 80, 12).Percent == 120, "native bonus golden");
        Check(Evaluate(empty, 12000, 0, 0).Percent == 50, "native gems-only golden");
        Check(Evaluate(empty, 0, 80, 0).Percent == 40, "native dragons-only golden");
        Check(Evaluate(empty, 0, 0, 12).Percent == 10, "native ordinary-eggs-only golden");
        var shortcut = Evaluate(empty, 12001, 0, 0);
        Check(shortcut.Percent == 100 && !shortcut.RequiredBaseComplete, "legacy bonus shortcut retained even for incomplete ordinary counters");
    }

    private static void RequiredEggGoldens()
    {
        var oneMissing = Create(1);
        var oneEarned = oneMissing.WithCollected(0, Id(0));
        Check(Evaluate(oneMissing, 12000, 80, 12).Percent == 99, "one missing required dragonfly prevents 100");
        Check(Evaluate(oneMissing, 14000, 80, 12).Percent == 99, "bonus gems cannot buy out missing dragonfly");
        Check(Evaluate(oneMissing, int.MaxValue, 80, 12).Percent == 99, "arbitrarily large accepted bonus cannot buy out missing dragonfly");
        Check(Evaluate(oneEarned, 12000, 80, 12).Percent == 100, "all required base complete reaches 100");
        Check(Evaluate(oneEarned, 12099, 80, 12).Percent == 100, "native bonus floors below 100 extra gems");
        Check(Evaluate(oneEarned, 12100, 80, 12).Percent == 101, "native bonus resumes only after required base complete");
        Check(Evaluate(oneEarned, 14000, 80, 12).Percent == 120, "completed custom content keeps native treasure bonus curve");
        Check(Evaluate(oneEarned, 0, 0, 0).Percent == 0, "one egg alone has less than one percent at N1");
        Check(Evaluate(oneEarned, 200, 0, 0).Percent == 1, "custom and ordinary weights combine before floor");
        Check(Evaluate(Create(2, 2), 0, 0, 0).Percent == 1, "two custom egg weight golden");
        Check(Evaluate(Create(30, 15), 6000, 40, 6).Percent == 50, "half of every required category is exactly half");
        Check(Evaluate(Create(30, 30), 0, 0, 0).Percent == 20, "30 dragonflies weight like 30 ordinary eggs");
        foreach (int count in new[] { 1, 2, 30, 128, 4096 })
        {
            var save = Create(count, count);
            foreach (var ordinary in new[] { (11999, 80, 12), (12000, 79, 12), (12000, 80, 11), (14000, 79, 12), (int.MaxValue, 80, 11) })
            {
                var result = Evaluate(save, ordinary.Item1, ordinary.Item2, ordinary.Item3);
                Check(result.Percent < 100 && !result.RequiredBaseComplete, "all ordinary base categories are required with custom eggs");
            }
            var missing = Create(count, count - 1);
            Check(Evaluate(missing, int.MaxValue, 80, 12).Percent < 100, "each last missing egg matters even at maximum capacity");
            var complete = Evaluate(save, 12000, 80, 12);
            Check(complete.Percent == 100 && complete.RequiredBaseComplete && complete.RemainingDragonflyCount == 0, "all required categories yield exactly 100");
            Check(complete.RequiredScore == 1200000L + 10000L * count && complete.EarnedBaseScore == complete.RequiredScore, "score units and denominator are exact");
            Check(Evaluate(save, int.MaxValue, 80, 12).Percent == NativeOracle(int.MaxValue, 80, 12), "complete base permits native bonus at maximum accepted counter");
        }
    }

    private static void MonotonicProgress()
    {
        foreach (int earned in new[] { 0, 2, 5 })
        {
            var save = Create(5, earned);
            foreach (var counters in new[] { (0, 0), (79, 11), (80, 12) })
            {
                int previous = -1;
                for (int gems = 0; gems <= 14000; gems++)
                {
                    int percent = Evaluate(save, gems, counters.Item1, counters.Item2).Percent;
                    Check(percent >= previous, "ordinary gem progress never lowers fixed-manifest completion");
                    previous = percent;
                }
            }
            foreach (int gems in new[] { 0, 6000, 12000, 12001, 14000, int.MaxValue })
            {
                int previous = -1;
                for (int dragons = 0; dragons <= 80; dragons++)
                {
                    int percent = Evaluate(save, gems, dragons, 12).Percent;
                    Check(percent >= previous, "ordinary dragon progress never lowers completion"); previous = percent;
                }
                previous = -1;
                for (int eggs = 0; eggs <= 12; eggs++)
                {
                    int percent = Evaluate(save, gems, 80, eggs).Percent;
                    Check(percent >= previous, "ordinary egg progress never lowers completion"); previous = percent;
                }
            }
        }
        var incremental = Create(30);
        int prior = -1;
        for (int earned = 0; earned <= 30; earned++)
        {
            if (earned != 0) incremental = incremental.WithCollected(0, Id(earned - 1));
            var result = Evaluate(incremental, 14000, 80, 12);
            Check(result.Percent >= prior, "custom collection never lowers fixed-manifest completion");
            Check(result.RequiredBaseComplete == (earned == 30), "required completion tracks all active identities");
            prior = result.Percent;
        }
    }

    private static void SlotInventoryAndRetirement()
    {
        var state = Create(3).WithCollected(0, Id(0)).WithCollected(1, Id(1)).WithCollected(2, Id(0)).WithCollected(2, Id(2));
        byte[] before = DetachedCollectibleSaveCodec.Serialize(state);
        for (int slot = 0; slot < 3; slot++)
        {
            var result = Evaluate(state, 12000, 80, 12, slot);
            Check(result.OrdinarySlot == slot && result.ContentId == Content && result.ManifestRevision == state.Manifest.Revision &&
                result.ManifestSha256 == state.Manifest.Sha256, "inventory snapshot identifies exact slot and manifest");
            Check(result.RequiredDragonflyCount == 3 && result.EarnedDragonflyCount == (slot == 2 ? 2 : 1), "three slot inventories remain independent");
            Check(result.ActiveInventory.Count == 3 && result.RemainingDragonflyCount == (slot == 2 ? 1 : 2), "remaining count and inventory agree");
            foreach (var entry in result.ActiveInventory)
                Check(entry.Earned == state.IsEarned(slot, entry.InstanceId) && entry.BitIndex == state.Manifest.GetEntry(entry.InstanceId).BitIndex, "inventory binds GUID and bit rather than row order");
            Refuses(() => ((IList<DetachedCollectibleCompletionPolicy.InventoryEntry>)result.ActiveInventory).Clear(), "inventory snapshot immutable");
        }
        Check(DetachedCollectibleSaveCodec.Serialize(state).SequenceEqual(before), "completion evaluation never mutates earned state");
        var retiredManifest = state.Manifest.Advance([], [Id(0)]);
        var retiredState = state.MigrateTo(retiredManifest);
        byte[] retiredBefore = DetachedCollectibleSaveCodec.Serialize(retiredState);
        var retiredResult = Evaluate(retiredState, 12000, 80, 12, 0);
        Check(retiredResult.RequiredDragonflyCount == 2 && retiredResult.EarnedDragonflyCount == 0 && retiredResult.ActiveInventory.All(e => e.InstanceId != Id(0)), "retired owner excluded from numerator denominator and inventory");
        Check(retiredState.IsEarned(0, Id(0)), "retired earned history remains exact");
        Check(DetachedCollectibleSaveCodec.Serialize(retiredState).SequenceEqual(retiredBefore), "retired evaluation does not clear history");
        var allRetired = retiredState.MigrateTo(retiredManifest.Advance([], [Id(1), Id(2)]));
        var legacyAgain = Evaluate(allRetired, 12001, 0, 0, 2);
        Check(!legacyAgain.UsesRequiredDragonflyPolicy && legacyAgain.Percent == 100 && legacyAgain.ActiveInventory.Count == 0, "zero active entries takes exact legacy path even with retained tombstones");
        Check(allRetired.IsEarned(2, Id(0)) && allRetired.IsEarned(2, Id(2)), "returning to legacy path does not discard tombstone history");
        var added = allRetired.MigrateTo(allRetired.Manifest.Advance([Id(4)], []));
        Check(Evaluate(added, 14000, 80, 12, 2).Percent == 99, "new identity cannot inherit earned retired bits for completion");
    }

    private static void BoundsAndCapacity()
    {
        foreach (int bad in new[] { -1, int.MinValue }) Refuses(() => new DetachedCollectibleCompletionPolicy.OrdinaryProgress(bad, 0, 0), "negative gem counter refused");
        foreach (int bad in new[] { -1, 81, int.MinValue, int.MaxValue }) Refuses(() => new DetachedCollectibleCompletionPolicy.OrdinaryProgress(0, bad, 0), "dragon counter outside original base profile refused");
        foreach (int bad in new[] { -1, 13, int.MinValue, int.MaxValue }) Refuses(() => new DetachedCollectibleCompletionPolicy.OrdinaryProgress(0, 0, bad), "ordinary egg counter outside original base profile refused");
        var empty = Create(0);
        foreach (int slot in new[] { -1, 3, int.MinValue, int.MaxValue })
            Refuses(() => Evaluate(empty, 0, 0, 0, slot), "invalid slot refused even with empty manifest");
        Refuses(() => DetachedCollectibleCompletionPolicy.Evaluate(null!, 0, new(0, 0, 0)), "null save refused");
        Refuses(() => DetachedCollectibleCompletionPolicy.Evaluate(empty, 0, null!), "null ordinary progress refused");
        var unknown = DetachedCollectibleSaveCodec.Inspect(new byte[3072], empty.Manifest);
        Refuses(() => Evaluate(unknown.RequireCompatible(), 12000, 80, 12), "unknown saved bytes cannot default to a zero-custom completion result");
        var mismatched = DetachedCollectibleSaveCodec.Inspect(DetachedCollectibleSaveCodec.Serialize(Create(1, 1)), empty.Manifest);
        Refuses(() => Evaluate(mismatched.RequireCompatible(), 12000, 80, 12), "manifest mismatch cannot reinterpret a save as legacy completed content");
        var maximum = Evaluate(Create(4096, 4096), 12000, 80, 12);
        Check(maximum.RequiredScore * 100 > int.MaxValue && maximum.Percent == 100, "weighted percent numerator cannot overflow signed 32-bit arithmetic");
        var hugeBonus = Evaluate(Create(1, 1), int.MaxValue, 80, 12);
        Check(hugeBonus.Percent == 21474816, "large accepted gem total has exact bounded percent without overflow");
        Check(Evaluate(Create(1), int.MaxValue, 0, 0).Percent == 49, "bonus cannot substitute for any required base category");
        var decoded = DetachedCollectibleSaveCodec.Inspect(DetachedCollectibleSaveCodec.Serialize(Create(2, 2, 1)), Create(2).Manifest).RequireCompatible();
        Check(Evaluate(decoded, 12000, 80, 12, 1).Percent == 100 && Evaluate(decoded, 12000, 80, 12, 0).Percent == 98, "decoded detached state preserves per-slot completion");
    }
}

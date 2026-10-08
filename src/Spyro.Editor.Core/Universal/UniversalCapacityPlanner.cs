using System.Buffers.Binary;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Universal;

public sealed record UniversalRemovalGroup(int ClassId, string Name, IReadOnlyList<int> AdditionalRows,
    int ModelBytes, int EffectPriority, string Effect);

/// <summary>A suggestion proved by composing all current imports together with the requested variant.
/// Rows are native source identities. Applying a suggestion is always a separate editor action.</summary>
public sealed record UniversalRemovalPlan(IReadOnlyList<UniversalRemovalGroup> Groups, IReadOnlyList<int> AdditionalRows,
    int DroppedModelBytes, int ReclaimedVramBytes, int FinalRamHeadroom, int ArenaSlotsRemaining,
    IReadOnlyList<int> DroppedModelClasses, bool ParticleBehaviorUnverified);

public sealed record UniversalCapacityAdvice(string? CurrentError, int NativeRows, int RemovedRows,
    int ActiveImports, int RetiredIdentities, int? CurrentRamHeadroom, IReadOnlyList<UniversalRemovalGroup> Candidates,
    IReadOnlyList<UniversalRemovalPlan> Plans, int Checks, bool SearchTruncated)
{
    /// <summary>True when the caller stopped the search before it ran out of options or checks.</summary>
    public bool Stopped { get; init; }
}

/// <summary>What the search is doing right now: how many exact checks ran, the most it will run, how many
/// removal options there are, and a plan the moment one is proved. Reported from the search thread.</summary>
public sealed record UniversalSearchProgress(int Checks, int MaximumChecks, int Options, UniversalRemovalPlan? Found);

public static class UniversalCapacityPlanner
{
    /// <summary>Suggests complete native model-owner groups, then proves bounded combinations with the
    /// real composer. A model estimate never authorizes removal or marks an import as fitting.</summary>
    public static UniversalCapacityAdvice Analyze(UniversalSourceLibrary library, LevelDefinition destination,
        IReadOnlyList<UniversalMobyPlacement> trial, IReadOnlyCollection<int> alreadyRemoved,
        Func<int, (string Name, string Kind)>? names = null, Func<string, UniversalSpecialAsset?>? special = null,
        int maximumChecks = 128, CancellationToken cancellationToken = default, UniversalCompositionHost? host = null,
        Func<IReadOnlyCollection<int>, (UniversalComposeResult? Result, string? Error)>? compositionCheck = null,
        Action<UniversalSearchProgress>? progress = null, CancellationToken stop = default)
    {
        if (maximumChecks < 1) throw new ArgumentOutOfRangeException(nameof(maximumChecks));
        var native = library.Get(destination.Key);
        var data = host?.DestinationData ?? native.Data;
        var overlay = host?.DestinationOverlay ?? native.Overlay;
        var exe = host?.Executable ?? library.Disc.Exe;
        (UniversalComposeResult? Result, string? Error) Compose(IReadOnlyCollection<int> rows) => compositionCheck != null ? compositionCheck(rows) : host == null
            ? UniversalMobyCatalogBuilder.DryRun(library,destination,trial,special,rows)
            : host.DryRun(library,trial,special,rows);
        var removed = alreadyRemoved.ToHashSet();
        var current = Compose(removed);
        int count = data.Scene().RowCount;
        if (current.Error == null)
            return new(null, count, removed.Count, trial.Count(p => !p.Retired), trial.Count(p => p.Retired),
                current.Result!.Ram.FinalHeadroom, [], [], 1, false);
        // This option needs more than the general three-family search can find.
        // Check exactly the existing explicit profile, including its travel loss;
        // never widen removals or treat the estimate as export permission.
        if (UniversalAlpineArtisansRemovalAdvice.Requested(destination.Key,trial))
        {
            var named = UniversalAlpineArtisansRemovalAdvice.Groups(native.Data,removed);
            var profilePlans = new List<UniversalRemovalPlan>();
            int profileChecks = 1;
            string profileError = current.Error;
            if (removed.IsSubsetOf(UniversalAlpineArtisansRemovalAdvice.RequiredRows.ToHashSet()) && profileChecks < maximumChecks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var checkedProfile = Compose(UniversalAlpineArtisansRemovalAdvice.RequiredRows);
                profileChecks++;
                if (checkedProfile.Error != null) profileError = checkedProfile.Error;
                if (checkedProfile.Error == null && checkedProfile.Result is { } r)
                    profilePlans.Add(new(named,UniversalAlpineArtisansRemovalAdvice.RequiredRows.Except(removed).ToArray(),
                        r.DroppedModelBytes,r.ReclaimedVramBytes,r.Ram.FinalHeadroom,r.ArenaSlotsRemaining,
                        r.DroppedModelClasses.ToArray(),r.ParticleDependencies.Any(p=>p.Status=="dependency-unverified")));
            }
            return new(profileError,count,removed.Count,trial.Count(p=>!p.Retired),trial.Count(p=>p.Retired),null,
                named,profilePlans,profileChecks,profilePlans.Count==0 && profileChecks>=maximumChecks);
        }
        var groups = Enumerable.Range(0, count).GroupBy(row => (int)BinaryPrimitives.ReadUInt16LittleEndian(data.Row(row).AsSpan(0x36)))
            .Select(group =>
            {
                int[] additional = group.Where(row => !removed.Contains(row)).ToArray();
                if (additional.Length == 0) return null;
                var eligible = UniversalMobyComposer.DroppableClasses(data, overlay, exe,
                    library.SharedClasses, removed.Concat(additional).ToArray(), new HashSet<int>());
                if (!eligible.Classes.Contains(group.Key)) return null;
                var label = names?.Invoke(group.First()) ?? ($"Object 0x{group.Key:X3}", "unknown");
                var (priority, effect) = Effect(label.Item1, label.Item2);
                return new UniversalRemovalGroup(group.Key, label.Item1, additional, data.Package(group.Key).Length, priority, effect);
            }).OfType<UniversalRemovalGroup>().OrderBy(g => g.EffectPriority).ThenBy(g => g.AdditionalRows.Count)
            .ThenByDescending(g => g.ModelBytes).ToList();
        // Missing behavior/format dependencies cannot be repaired by deleting destination actors.
        if (!IsCapacityFailure(current.Error))
            return new(current.Error, count, removed.Count, trial.Count(p => !p.Retired), trial.Count(p => p.Retired), null, groups, [], 1, false);
        // Keep transport, dragons and unknown controllers out of the default suggestion search.
        // Their ownership is shown, but a capacity hint must not casually remove progression.
        var preferred = groups.Where(g => g.EffectPriority <= 2).Take(12).ToArray();
        var combinations = Combinations(preferred).OrderBy(gs => gs.Sum(g => g.AdditionalRows.Count))
            .ThenBy(gs => gs.Max(g => g.EffectPriority)).ThenByDescending(gs => gs.Sum(g => g.ModelBytes)).ToList();
        var plans = new List<UniversalRemovalPlan>();
        int checks = 1;
        bool stopped = false;
        progress?.Invoke(new UniversalSearchProgress(checks, maximumChecks, combinations.Count, null));
        foreach (var combination in combinations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // The caller's Stop keeps what has been proved so far instead of throwing it away.
            if (stop.IsCancellationRequested) { stopped = true; break; }
            if (checks >= maximumChecks) break;
            int[] additional = combination.SelectMany(g => g.AdditionalRows).Order().ToArray();
            // A larger plan containing an already proved plan needlessly removes more objects.
            if (plans.Any(p => p.AdditionalRows.All(additional.Contains))) continue;
            var result = Compose(removed.Concat(additional).ToArray());
            checks++;
            if (result.Error != null)
            {
                progress?.Invoke(new UniversalSearchProgress(checks, maximumChecks, combinations.Count, null));
                continue;
            }
            var r = result.Result!;
            var plan = new UniversalRemovalPlan(combination, additional, r.DroppedModelBytes, r.ReclaimedVramBytes, r.Ram.FinalHeadroom,
                r.ArenaSlotsRemaining, r.DroppedModelClasses.ToArray(), r.ParticleDependencies.Any(p => p.Status == "dependency-unverified"));
            plans.Add(plan);
            progress?.Invoke(new UniversalSearchProgress(checks, maximumChecks, combinations.Count, plan));
            if (plans.Count >= 3) break;
        }
        return new(current.Error, count, removed.Count, trial.Count(p => !p.Retired), trial.Count(p => p.Retired), null,
            groups, plans, checks, stopped || checks >= maximumChecks || plans.Count >= 3) { Stopped = stopped };
    }

    public static bool IsCapacityFailure(string error) =>
        error.Contains("Not enough free texture memory", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("free CLUT", StringComparison.OrdinalIgnoreCase) ||
        System.Text.RegularExpressions.Regex.IsMatch(error,@"\bRAM\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant) ||
        error.Contains("dynamic slots", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("spare object slots", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<UniversalRemovalGroup[]> Combinations(UniversalRemovalGroup[] groups)
    {
        for (int i = 0; i < groups.Length; i++)
        {
            yield return [groups[i]];
            for (int j = i + 1; j < groups.Length; j++)
            {
                yield return [groups[i], groups[j]];
                for (int k = j + 1; k < groups.Length; k++) yield return [groups[i], groups[j], groups[k]];
            }
        }
    }

    private static (int Priority, string Effect) Effect(string name, string kind)
    {
        string text = (name + " " + kind).ToLowerInvariant();
        if (new[] { "dragon", "portal", "gate", "trigger", "controller", "helper", "anchor", "balloon", "flight", "unknown" }.Any(text.Contains))
            return (4, "May affect progression, transport or linked behavior; inspect before removing.");
        if (new[] { "gnorc", "enemy", "druid", "wizard", "knight", "bull" }.Any(text.Contains))
            return (0, "Removes these enemies and their native rewards.");
        if (new[] { "chest", "gem", "treasure", "basket", "life" }.Any(text.Contains))
            return (1, "Removes these pickups/containers and their rewards.");
        if (new[] { "tree", "flower", "grass", "flag", "scenery", "prop", "plant" }.Any(text.Contains))
            return (2, "Removes these scenery objects.");
        return (4, "Object effect is not classified; inspect before removing.");
    }
}

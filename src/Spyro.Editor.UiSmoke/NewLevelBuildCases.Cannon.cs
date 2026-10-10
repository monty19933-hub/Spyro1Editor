using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

/// <summary>Cloudspire Isles (Town Square, built): the island east of the start (flat at z 1600, x 4052-4768,
/// y 2640-3360) gets Dream Weavers' Resize cannon in place of its two Armored Fools, added through the Add Object
/// browser as a user would: the cannon (which brings its gunner), then two big Armored Fools that never call (Dream
/// Weavers rows 20/21) into the cannon's group. Spyro defeats the gunner, stands behind the cannon and flames it to
/// shrink a fool, then charges it.
///
/// Layout (editor units): the cannon at (4290,3000) faces +x (yaw 0), down the island's middle; the gunner starts 94
/// units behind it, the distance it keeps while circling to stay opposite Spyro. The fools start at (4140,2990) and
/// (4143,2888), Dream Weavers' own spacing, so their moved trigger area (centre 4541,3152) and path (4569,2811)-(4530,
/// 3279) - which they walk back and forth when Spyro is away - lie across the barrel's line 250 units ahead.
/// Save rows: 2 + 2 more for good (the removed fools keep theirs).</summary>
internal static partial class NewLevelBuildCases
{
    private static readonly Vector3f CannonSpot = new(4290, 3000, 1600), GunnerSpot = new(4196, 3000, 1600);
    private static readonly (int Row, Vector3f Spot)[] CannonFoolSpots = [(20, new(4140, 2990, 1600)), (21, new(4143, 2888, 1600))];
    private static readonly Vector3f[] ReplacedFoolSpots = [new(4330, 2840, 1600), new(4520, 3160, 1600)];

    private static void CloudspireCannon(MainWindow window)
    {
        const string Key = UniversalDreamWeaversCannonRows.SourceKey;
        Load(window, "townsquare");
        if (window.FreshSceneForTesting == null) throw new InvalidOperationException("Town Square is not the built Cloudspire Isles here.");
        static bool Near(Vector3f a, Vector3f b) => Math.Abs(a.X - b.X) < 2 && Math.Abs(a.Y - b.Y) < 2 && Math.Abs(a.Z - b.Z) < 2;
        int treasureBefore = Mobys(window).Where(m => m.IsAdded && !m.IsRemoved).Sum(m => m.TreasureValue);

        // 1. The island's two Armored Fools (Dream Weavers row 13 copies).
        var armored = Mobys(window).Where(m => m.IsAdded && !m.IsRemoved && m.CrossLevelSourceLevelKey == Key && m.CrossLevelSourceTrueIndex == 13).ToList();
        foreach (var spot in ReplacedFoolSpots)
        {
            var fool = armored.SingleOrDefault(m => Near(m.Position, spot)) ??
                throw new InvalidOperationException($"No Armored Fool at ({spot.X},{spot.Y},{spot.Z}): {string.Join(", ", armored.Select(m => m.Position))}");
            SetField(window, "_selectedMoby", fool);
            Invoke(window, "RemoveSelectedMoby");
            if (Mobys(window).Contains(fool) && !fool.IsRemoved) throw new InvalidOperationException("Not removed: " + fool.DisplayLabel + " — " + Status(window));
            Console.WriteLine($"  removed {fool.DisplayLabel} at ({spot.X:0},{spot.Y:0},{spot.Z:0})");
        }

        var (browser, entries) = OpenBrowserReporting(window); browser.Close(); Pump(10);
        var lib = Library(window);
        var japanese = (UniversalJapaneseSource?)Invoke(window, "ResolveUniversalJapaneseSource");
        (UniversalMobyCatalogEntry Entry, UniversalMobyVariant Variant) Pick(int row) =>
            entries.SelectMany(e => e.Variants.Select(v => (Entry: e, Variant: v))).FirstOrDefault(p => p.Variant.SourceLevelKey == Key && p.Variant.SourceRow == row) is { Entry: not null } pick
                ? pick : throw new InvalidOperationException($"Dream Weavers row {row} is not in the Add Object browser.");
        List<Moby> Add(Func<Task> add, string what)
        {
            var before = Mobys(window).ToHashSet();
            var task = add();
            Wait(() => task.IsCompleted, "add " + what, 180);
            if (task.IsFaulted) throw task.Exception!;
            var added = Mobys(window).Where(m => !before.Contains(m)).ToList();
            if (added.Count == 0) throw new InvalidOperationException($"Not added: {what}: {Status(window)}");
            return added;
        }

        // 2. The Resize cannon; the browser adds its gunner with it, in one linked group.
        var (cannonEntry, cannonVariant) = Pick(UniversalDreamWeaversCannonRows.CannonRow);
        if (cannonVariant.RestrictionReason != null) throw new InvalidOperationException("The Resize cannon is locked: " + cannonVariant.RestrictionReason);
        SetField(window, "_selectedMoby", null);
        var pair = Add(() => (Task)Invoke(window, "AddUniversalMobyAsync", lib, cannonEntry, cannonVariant, false, japanese)!, "Resize cannon");
        Moby cannon = pair.Single(m => m.CrossLevelSourceTrueIndex == UniversalDreamWeaversCannonRows.CannonRow);
        Moby gunner = pair.SingleOrDefault(m => m.CrossLevelSourceTrueIndex == UniversalDreamWeaversCannonRows.GunnerRow) ??
            throw new InvalidOperationException("The cannon came without its gunner.");
        string group = cannon.UniversalLinkGroupId;
        if (pair.Count != 2 || string.IsNullOrEmpty(group) || gunner.UniversalLinkGroupId != group) throw new InvalidOperationException("The cannon and its gunner are not one group.");
        SetField(window, "_selectedMoby", cannon);
        // A non-zero height change keeps the nudge from snapping to the ground (the island is flat at the spots' height).
        Invoke(window, "NudgeSelectedMoby", CannonSpot.X - cannon.Position.X, CannonSpot.Y - cannon.Position.Y, CannonSpot.Z - cannon.Position.Z + 0.001f);
        cannon.YawByte = 0;
        // The gunner moved with the cannon; it goes behind the cannon on its own (moving it in the editor moves the pair).
        gunner.Position = GunnerSpot;

        // 3. Two big Armored Fools that never call, into the cannon's group (the browser's "Let the Resize cannon shoot it").
        foreach (var (row, spot) in CannonFoolSpots)
        {
            var (entry, variant) = Pick(row);
            SetField(window, "_selectedMoby", cannon);
            if ((string?)Invoke(window, "UniversalCannonGroupForNewFool") != group) throw new InvalidOperationException("The selected cannon is not offered to the fool.");
            var added = Add(() => (Task)Invoke(window, "AddUniversalMobyIntoGroupAsync", lib, entry, variant, false, japanese, group)!, "Armored Fool " + row);
            Moby fool = added.Single();
            if (fool.UniversalLinkGroupId != group) throw new InvalidOperationException("The fool did not join the cannon.");
            var cannonBefore = cannon.Position;
            SetField(window, "_selectedMoby", fool);
            Invoke(window, "NudgeSelectedMoby", spot.X - fool.Position.X, spot.Y - fool.Position.Y, spot.Z - fool.Position.Z + 0.001f);
            if (!Near(fool.Position, spot) || !Near(cannon.Position, cannonBefore)) throw new InvalidOperationException("The fool did not move on its own.");
        }

        foreach (var m in Mobys(window).Where(m => m.IsAdded && !m.IsRemoved && m.UniversalLinkGroupId == group))
            Console.WriteLine($"  {m.DisplayLabel,-34} Dream Weavers row {m.CrossLevelSourceTrueIndex,2} ({m.Position.X:0},{m.Position.Y:0},{m.Position.Z:0}) yaw {m.YawByte} gem {m.TreasureValue}");
        Save(window);
        int ledger = window.FreshSceneForTesting?.Ledger.Entries.Count ?? -1;
        int prefix = lib.Get("townsquare").Data.Scene().RowCount;
        int treasureAfter = Mobys(window).Where(m => m.IsAdded && !m.IsRemoved).Sum(m => m.TreasureValue);
        Console.WriteLine($"save rows: {prefix} native + {ledger} ever added = {prefix + ledger} of 256 ({256 - prefix - ledger} left)");
        Console.WriteLine($"editor-known treasure {treasureBefore} -> {treasureAfter}");
        Console.WriteLine("NEW_LEVEL_CLOUDSPIRE_CANNON_OK");
    }
}

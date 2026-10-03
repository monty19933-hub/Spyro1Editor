using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

public partial class MainWindow
{
    // This is advice for the bounded Artisans route, not a new removal admission.
    // The full composer must still prove the prospective six-row plan.
    private static readonly int[] MagicArtisansRoomRows = [7, 29, 30, 31, 32, 33];
    private static bool IsMagicArtisansRoomChoice(string destination, UniversalMobyVariant variant) =>
        destination == "artisans" && variant.SourceLevelKey == "magiccrafters" && variant.ClassId == 0x10F;
    private static bool IsMagicArtisansPairChoice(UniversalMobyVariant variant) => variant.SourceRow is 12 or 13;

    private string MagicArtisansRoomText(UniversalMobyVariant variant)
    {
        string Rows(int[] rows) => string.Join(", ", rows.Select(row => $"T{row}" +
            (_currentMobys.Any(m => !m.IsAdded && m.TrueIndex == row && m.IsRemoved) ? " (removed)" : "")));
        string Name(int row, string fallback) => _currentMobys.FirstOrDefault(m => !m.IsAdded && m.TrueIndex == row)?.DisplayLabel ?? fallback;
        return (IsMagicArtisansPairChoice(variant)
            ? "Adds the linked Armored Druid pair from Magic Crafters (T12 and T13). They move, copy and remove together; keep their original facing."
            : "For Artisans, choose the linked Armored Druid pair from Magic Crafters (T12 and T13). Other source versions are not supported here.") +
            $"\nTo make room, remove all six original Gnorcs:\n{Name(29, "Regular Gnorc")}: {Rows([29,30,32,33])}\n{Name(7, "Treasure Gnorc")}: {Rows([7,31])}" +
            "\nRemoving these enemies also removes their rewards (13 gems). Use Review to check the space and locate each object, then use the normal Remove button." +
            "\nThis option supports one linked pair. Keep the terrain and all remaining original objects unchanged; other additions or removals are not supported with this pair yet.";
    }

    private string? MagicArtisansRoomRestriction(IReadOnlyList<UniversalMobyPlacement> existing, IReadOnlyCollection<int> removed)
    {
        if (removed.Any(row => !MagicArtisansRoomRows.Contains(row)))
            return "Undo other original-object removals first. This option needs exactly the six listed Gnorcs removed.";
        if (_currentMobys.Any(m => !m.IsAdded && !m.IsRemoved && m.HasAnyEdit))
            return "Undo changes to the remaining original objects first. This option keeps their positions and settings unchanged.";
        if (existing.Any(p => !p.Retired) || _currentMobys.Any(m => m.IsAdded && !m.IsRemoved))
            return "Remove the current added objects first. This option adds one linked Armored Druid pair.";
        foreach (int row in MagicArtisansRoomRows)
        {
            var native = _currentMobys.Where(m => !m.IsAdded && m.TrueIndex == row).ToArray();
            int expected = row is 7 or 31 ? 0x153 : 0x72;
            if (native.Length != 1 || (native[0].OriginalSourceByte36 | native[0].OriginalSourceByte37 << 8) != expected)
                return "This option needs the original Artisans objects. Reload the original level to review this removal plan.";
        }
        return null;
    }

    private static UniversalRemovalPlan MagicArtisansCheckedPlan(UniversalComposeResult result, IReadOnlyCollection<int> removed,
        Func<int, string> name)
    {
        int[] remaining = MagicArtisansRoomRows.Except(removed).ToArray();
        var groups = new[]
        {
            new UniversalRemovalGroup(0x72, name(29), remaining.Where(r => r is 29 or 30 or 32 or 33).ToArray(), 0, 0,
                "Remove all four Regular Gnorcs, including both linked pairs, and their rewards."),
            new UniversalRemovalGroup(0x153, name(7), remaining.Where(r => r is 7 or 31).ToArray(), 0, 0,
                "Remove both Treasure Gnorcs and their rewards.")
        }.Where(g => g.AdditionalRows.Count > 0).ToArray();
        return new(groups, remaining, result.DroppedModelBytes, result.ReclaimedVramBytes, result.Ram.FinalHeadroom,
            result.ArenaSlotsRemaining, result.DroppedModelClasses.ToArray(),
            result.ParticleDependencies.Any(p => p.Status == "dependency-unverified"));
    }
}

using System.Buffers.Binary;
namespace Spyro.Editor.Core.Universal;

/// <summary>Names the existing explicit Artisans option. Advice never admits a
/// composition: its complete removal set must pass the ordinary export check.</summary>
public static class UniversalAlpineArtisansRemovalAdvice
{
    public const string Explanation = "The Alpine Ridge armored druid and caster (T0 and T1) are added together. To make room in original Artisans, this option needs exactly 46 original objects removed: six Gnorcs, the balloonist and balloon, nine tower flags, twenty tulip flowers, four trees and five grass objects. Removing the balloonist and balloon makes balloon travel unavailable. Keep the terrain and remaining original objects unchanged; use this group on its own. Nothing is removed automatically.";
    public static bool IsChoice(string destination, string source, int row) =>
        destination == "artisans" && source == "alpineridge" && row is 0 or 1;
    internal static bool Requested(string destination, IReadOnlyList<UniversalMobyPlacement> trial) =>
        trial.Any(p => !p.Retired && IsChoice(destination,p.SourceLevelKey,p.SourceRowIndex));
    public static IReadOnlyList<int> RequiredRows { get; } = Array.AsReadOnly(UniversalAlpineArtisansHost.RemovedRows.ToArray());

    internal static IReadOnlyList<UniversalRemovalGroup> Groups(UniversalLevelData data, IReadOnlySet<int> removed)
    {
        var families = new (string Name, int[] Rows, int Priority, string Effect)[]
        {
            ("Gnorcs", [7,29,30,31,32,33], 0, "Removes all six Gnorcs and their rewards (13 gems)."),
            ("Balloonist and balloon", [45,46], 4, "Removes the balloonist and balloon. Balloon travel will be unavailable."),
            ("Tower flags", [0,1,2,3,4,5,6,95,96], 2, "Removes all nine tower flags."),
            ("Tulip flowers", [97,98,99,100,101,106,107,108,118,119,120,123,124,125,126,127,131,132,139,140], 2, "Removes all twenty tulip flowers."),
            ("Trees", [102,103,104,105], 2, "Removes all four trees."),
            ("Grass", [121,122,128,129,130], 2, "Removes all five grass objects.")
        };
        if (!families.SelectMany(f => f.Rows).Order().SequenceEqual(RequiredRows))
            throw new InvalidDataException("The named Artisans removal list differs from the export's required objects.");
        return families.Select(f =>
        {
            int[] additional = f.Rows.Except(removed).ToArray();
            int[] classes = f.Rows.Select(r => (int)BinaryPrimitives.ReadUInt16LittleEndian(data.Row(r).AsSpan(54))).Distinct().ToArray();
            return new UniversalRemovalGroup(classes[0],f.Name,additional,classes.Sum(c=>data.Package(c).Length),f.Priority,f.Effect);
        }).Where(g=>g.AdditionalRows.Count>0).ToArray();
    }
}

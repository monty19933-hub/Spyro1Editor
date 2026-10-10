using System.Buffers.Binary;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Universal;

/// <summary>Save identities of a rebuilt level's original objects that no save card can hold set: rows whose class the
/// level's code does not run, that the executable does not share, and that are worth no treasure (flags, trees,
/// grass, markers). A new object may take one of them; every existing card has its bit clear.</summary>
public static class UniversalSaveIdentities
{
    public static IReadOnlyList<int> Reusable((LevelDefinition Level, UniversalLevelData Data, UniversalOverlay Overlay) original, IReadOnlySet<int> shared)
    {
        var sw = UniversalDispatchAnalysis.FindUpdateSwitch(original.Overlay);
        var scene = original.Data.Scene();
        List<int> rows = [];
        for (int i = 0; i < scene.RowCount && i < 256; i++)
        {
            byte[] row = original.Data.Row(i);
            int c = BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(0x36));
            if (c != 0 && !UniversalDispatchAnalysis.Handles(original.Overlay, sw, c) && !shared.Contains(c) &&
                UniversalTreasureAccounting.KnownValue(row) == 0)
                rows.Add(i);
        }
        return rows;
    }
}

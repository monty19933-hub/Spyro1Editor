using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Universal;

/// <summary>The original Magic Crafters armored-druid chain uses signed g_Mobys row
/// integers. This proof enrolls its positive links; it does not prove the handler's
/// selector/state domain or authorize removal of the missing class13B dependency.</summary>
public static class UniversalMagicCraftersDruidIndexedRows
{
    public const string DataSha256 = "ad34af2e3401514e7db740691c80fd2bbf59057a55bdfedbaaa39b6939141fcb";
    public const string OverlaySha256 = "98e135e18876ea094ca9484b33cfb38438e2b99b72ee4d6d66e907722c744a1d";

    public static void ProveSource(string key, UniversalLevelData data, UniversalOverlay overlay)
    {
        if (!string.Equals(key, "magiccrafters", StringComparison.OrdinalIgnoreCase) ||
            Convert.ToHexStringLower(SHA256.HashData(data.Bytes)) != DataSha256 ||
            Convert.ToHexStringLower(SHA256.HashData(overlay.Bytes)) != OverlaySha256 ||
            overlay.Update != 0x8007E398 || overlay.Init != 0x8008B2C0 || overlay.CopyBuffer != 0x8008E608)
            throw new InvalidDataException("Magic Crafters druid row ownership requires the complete authenticated original source data and overlay.");
        var sw = UniversalDispatchAnalysis.FindUpdateSwitch(overlay);
        var trace = UniversalDispatchAnalysis.Trace(overlay, sw.TreeStart, 0x10F, sw.Seeds, sw.ClassRegister);
        var owned = UniversalDispatchAnalysis.Closure(overlay, sw.Function, [trace.Stop], new HashSet<uint> { sw.TreeStart }).Instructions;
        // Every native +24 consumer materializes g_Mobys + index*0x58. Full-source
        // authentication includes other handlers, callbacks, tables and incoming entries.
        Packet(0x80086BB8, [0x8E230024,0,0x00031040,0x00431021,0x00021080,0x00431023,0x3C038007,0x8C635828,0x000210C0,0x00621821,0xA0690048,0x8C630000]);
        Packet(0x80086E18, [0x8E230024,0,0x00031040,0x00431021,0x00021080,0x00431023,0x3C038007,0x8C635828,0x000210C0,0x00621821,0x2402000A,0xA0620048]);
        Packet(0x80086E94, [0x8E220024,0x24050004,0x00021840,0x00621821,0x00031880,0x00621823,0x3C028007,0x8C425828,0x000318C0,0x0C00DFE4,0x00439021,0x10400028]);
        Packet(0x80087278, [0x8E230024,0x3C048007,0x8C845828,0x00031040,0x00431021,0x00021080,0x00431023,0x000210C0,0x8E230028,0x00822821,0x00031040,0x00431021]);
        Packet(0x80087D60, [0x8E230024,0x3C048007,0x8C845794,0x00031040,0x00431021,0x00021080,0x00431023,0x3C038007,0x8C635828,0x000210C0,0x10800011,0x00628021]);
        Packet(0x80087F40, [0x8E230024,0x24050004,0x00031040,0x00431021,0x00021080,0x00431023,0x3C038007,0x8C635828,0x000210C0,0x00628021,0x02002021,0x8E030000]);
        Packet(0x800880BC, [0x8E230024,0,0x00031040,0x00431021,0x00021080,0x00431023,0x3C038007,0x8C635828,0x000210C0,0x00628021,0x8E030000,0x2402000A]);
        // The same handler's second index is not inferred inactive from -1.
        Packet(0x80087298, [0x8E230028,0x00822821,0x00031040,0x00431021,0x00021080,0x00431023,0x000210C0,0x90A3003D,0,0x10690005,0x00822021,0x9082003D]);
        if (owned.Any(pc => UniversalMips.IsStore(overlay.Word(pc)) && UniversalMips.Rs(overlay.Word(pc)) == 17 && UniversalMips.SImm(overlay.Word(pc)) is 0x24 or 0x28))
            throw new InvalidDataException("Magic Crafters druid index has an additional self producer.");
        void Packet(uint start, uint[] words)
        {
            for (int i = 0; i < words.Length; i++)
                if (!owned.Contains(start + (uint)i * 4) || overlay.Word(start + (uint)i * 4) != words[i])
                    throw new InvalidDataException("Magic Crafters druid index consumer is changed or outside its class owner.");
        }
    }

    public static UniversalRowTemplate Capture(UniversalLevelData data, UniversalOverlay overlay, UniversalRowTemplate template)
    {
        if (template.ClassId != 0x10F || !string.Equals(template.SourceLevelKey, "magiccrafters", StringComparison.OrdinalIgnoreCase)) return template;
        ProveSource(template.SourceLevelKey, data, overlay);
        var original = data.CaptureRow(template.SourceLevelKey, template.SourceRowIndex);
        if (!template.Row.SequenceEqual(original.Row) || !template.Properties.SequenceEqual(original.Properties) ||
            !template.InternalPointers.SequenceEqual(original.InternalPointers) || !template.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers) ||
            template.DroppedExternalPointers != original.DroppedExternalPointers || template.Properties.Length < 0x2C ||
            template.InternalPointers.Any(p => p.Offset is 0x24 or 0x28) || template.ExternalRowPointers.Any(p => p.Offset is 0x24 or 0x28))
            throw new InvalidDataException("Magic Crafters druid index template differs from its authenticated placed source owner.");
        int target = BinaryPrimitives.ReadInt32LittleEndian(template.Properties.AsSpan(0x24));
        if (template.SourceRowIndex is not (2 or 3 or 4) || target != template.SourceRowIndex + 1 ||
            BinaryPrimitives.ReadUInt16LittleEndian(data.Row(target).AsSpan(0x36)) != 0x10F)
            throw new InvalidDataException("Magic Crafters druid negative index has no proved inactive state domain; its source chain cannot yet be imported completely.");
        return template with { SourceRowIndices = [new(0x24, target, 0x10F)], ImportedRowIndices = [] };
    }

    public static void RequireNativeRemoval(string key, UniversalLevelData current, UniversalLevelData original,
        UniversalOverlay overlay, IReadOnlySet<int> removed)
    {
        if (removed.Count == 0 || !string.Equals(key, "magiccrafters", StringComparison.OrdinalIgnoreCase)) return;
        ProveSource(key, original, overlay);
        // These native pod tables are another positive g_Mobys index owner. The
        // original source state2 callers reach 3B47C, which walks every ushort
        // member (mask7FFF; high bit terminates). Clearing/reusing a member row
        // does not remove that integer from the unchanged native pod component.
        int[][] pods = [[2, 3, 4, 5], [12, 13], [32, 33]];
        uint[] podWords = [0x24, 3, 0x14, 0x1C, 0x20, 0x00030002, 0x80050004, 0x800D000C, 0x80210020];
        var layout = original.Scene(); int at = original.Subfiles()[3].Offset + layout.PodComponent;
        if (layout.PodComponent != 0xF808 || podWords.Where((word, i) =>
                BinaryPrimitives.ReadUInt32LittleEndian(original.Bytes.AsSpan(at + i * 4)) != word).Any())
            throw new InvalidDataException("Magic Crafters native pod membership differs from its authenticated source owner.");
        for (int pod = 0; pod < pods.Length; pod++)
        {
            if (pods[pod].Any(row => original.Row(row)[0x43] != pod ||
                    BinaryPrimitives.ReadUInt16LittleEndian(original.Row(row).AsSpan(0x36)) != 0x10F))
                throw new InvalidDataException("Magic Crafters native pod row identity differs from its authenticated source owner.");
            if (pods[pod].Any(row => !removed.Contains(row)))
            {
                UniversalSceneLayout currentLayout;
                try { currentLayout = current.Scene(); }
                catch (Exception error) when (error is ArgumentOutOfRangeException or IndexOutOfRangeException or OverflowException)
                { throw new InvalidDataException("A surviving native druid pod scene has invalid component ownership.", error); }
                int start = current.Subfiles()[3].Offset + currentLayout.PodComponent;
                int size = BinaryPrimitives.ReadInt32LittleEndian(current.Bytes.AsSpan(start));
                int count = BinaryPrimitives.ReadInt32LittleEndian(current.Bytes.AsSpan(start + 4));
                if (count < pods.Length || count > 255 || size < 8 + 4 * count || size % 4 != 0)
                    throw new InvalidDataException("A surviving native druid pod table was changed or hidden before removal.");
                int members = BinaryPrimitives.ReadInt32LittleEndian(current.Bytes.AsSpan(start + 8 + 4 * pod));
                if (members < 8 + 4 * count || members + 2 * pods[pod].Length > size || start + size > current.Bytes.Length)
                    throw new InvalidDataException("A surviving native druid pod has invalid member ownership before removal.");
                for (int i = 0; i < pods[pod].Length; i++)
                    if (BinaryPrimitives.ReadUInt16LittleEndian(current.Bytes.AsSpan(start + members + 2 * i)) !=
                        (pods[pod][i] | (i == pods[pod].Length - 1 ? 0x8000 : 0)))
                        throw new InvalidDataException("A surviving native druid pod membership changed before removal.");
            }
            if (pods[pod].Any(removed.Contains) && pods[pod].Any(row => !removed.Contains(row)))
                throw new InvalidDataException($"Magic Crafters native pod {pod} owns rows {string.Join(", ", pods[pod])}; remove the whole pod together so a surviving druid cannot activate a replacement actor in a deleted member's slot.");
        }
        for (int row = 0; row < original.Scene().RowCount; row++)
        {
            if (removed.Contains(row) || BinaryPrimitives.ReadUInt16LittleEndian(original.Row(row).AsSpan(0x36)) != 0x10F) continue;
            if (row >= current.Scene().RowCount || BinaryPrimitives.ReadUInt16LittleEndian(current.Row(row).AsSpan(0x36)) != 0x10F)
                throw new InvalidDataException("A surviving native druid indexed owner was changed or hidden before native removal.");
            var before = original.CaptureRow(key, row); var after = current.CaptureRow(key, row);
            if (current.Row(row)[0x43] != original.Row(row)[0x43] ||
                before.Properties.Length < 0x2C || after.Properties.Length != before.Properties.Length ||
                !before.Properties.AsSpan(0x14, 4).SequenceEqual(after.Properties.AsSpan(0x14, 4)) ||
                !before.Properties.AsSpan(0x24, 8).SequenceEqual(after.Properties.AsSpan(0x24, 8)))
                throw new InvalidDataException("A surviving native druid selector/index producer changed before native removal.");
            int target = BinaryPrimitives.ReadInt32LittleEndian(before.Properties.AsSpan(0x24));
            // This removal check prevents a removed nonnegative physical slot from
            // being rebound. An unchanged original negative integer cannot select
            // such a slot. This says nothing about whether its imported handler may
            // execute the negative-index path; Capture still refuses that domain.
            if (target < 0) continue;
            var witness = Capture(original, overlay, before);
            if (removed.Contains(witness.SourceRowIndices.Single().SourceRow))
                throw new InvalidDataException($"Magic Crafters row {row} depends on removed row {target}; remove the owned referencing druid too.");
        }
    }
}

using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Universal;

/// <summary>A signed physical-row index owned by an authenticated placed source row.</summary>
public sealed record UniversalSourceRowIndex(int Offset, int SourceRow, int SourceClass);

/// <summary>The unique active import owning an indexed source row, before physical allocation.</summary>
public sealed record UniversalImportedRowIndex(int Offset, int PlacementIndex, int SourceRow, int SourceClass);

/// <summary>Source-scoped actor indices are integers, never scene pointer fixups or save IDs.</summary>
public static class UniversalIndexedRows
{
    public const string GnorcDataSha256 = "0f4ad8c1644a823022ac03718f3b46167a9545b202a4ef4b47d1de085c4a0d7e";
    public const string GnorcOverlaySha256 = "1d6f62cc80f515c73efc50b9c8435a36cb51b5e2bcea8823005ce05ed856fad9";
    private static bool IsGnorcIndexedClass(int c) => c is 0x0C7 or 0x0C9 or 0x0CA or 0x0CF;

    /// <summary>Enrolls only original placed Gnorc Cove reciprocal actor indices. Its spawned constructor
    /// separately writes -1; no placed template is manufactured from that inactive sentinel.</summary>
    public static UniversalRowTemplate Capture(UniversalLevelData data, UniversalOverlay overlay, UniversalRowTemplate template)
    {
        template = UniversalHauntedWizardPlacement.Capture(data, overlay, template);
        var rescue = UniversalLoftyWhirlwindGroups.Capture(data, overlay, template);
        template = rescue.Template with { SourcePrerequisiteRows = rescue.Prerequisites };
        if (template.ClassId == 0x12C) return UniversalWhirlwindIndexedRows.Capture(data, overlay, template);
        template = UniversalMagicCraftersDruidIndexedRows.Capture(data, overlay, template);
        if (!string.Equals(template.SourceLevelKey, "gnorccove", StringComparison.OrdinalIgnoreCase) || !IsGnorcIndexedClass(template.ClassId))
            return template;
        // Bind the complete source, including incoming branches, tables and every later
        // producer/consumer, rather than treating one coincidentally matching load as proof.
        if (Convert.ToHexStringLower(SHA256.HashData(data.Bytes)) != GnorcDataSha256 ||
            Convert.ToHexStringLower(SHA256.HashData(overlay.Bytes)) != GnorcOverlaySha256 ||
            overlay.Update != 0x8007B528 || overlay.Init != 0x80085664 || overlay.CopyBuffer != 0x80088668)
            throw new InvalidDataException("Gnorc Cove indexed-row ownership requires the authenticated original source data and overlay.");
        int indexOffset = template.ClassId is 0x0C7 or 0x0C9 ? 0 : 0x10;
        var original = data.CaptureRow(template.SourceLevelKey, template.SourceRowIndex);
        if (!template.Row.SequenceEqual(original.Row) || !template.Properties.SequenceEqual(original.Properties) ||
            !template.InternalPointers.SequenceEqual(original.InternalPointers) ||
            !template.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers) ||
            template.DroppedExternalPointers != original.DroppedExternalPointers || template.Properties.Length < indexOffset + 4 ||
            template.InternalPointers.Any(p => p.Offset == indexOffset) || template.ExternalRowPointers.Any(p => p.Offset == indexOffset))
            throw new InvalidDataException("Gnorc Cove indexed-row template differs from its authenticated placed source owner.");
        var sw = UniversalDispatchAnalysis.FindUpdateSwitch(overlay);
        var trace = UniversalDispatchAnalysis.Trace(overlay, sw.TreeStart, template.ClassId, sw.Seeds, sw.ClassRegister);
        var update = UniversalDispatchAnalysis.Closure(overlay, sw.Function, [trace.Stop], new HashSet<uint> { sw.TreeStart }).Instructions;
        var init = UniversalSpecializer.Specialize(overlay, overlay.Init, 4, [template.ClassId]).Instructions;
        if (template.ClassId == 0x0CF)
        {
            RequirePacket(overlay, update, 0x80081738, [0x8EC30010, 0, 0x0460000C, 0x00031040, 0x00431021, 0x00021080, 0x00431023, 0x3C038007, 0x8C635828, 0x000210C0, 0x00431021, 0x84430036, 0x240200C9, 0x10620091, 0]);
            RequirePacket(overlay, update, 0x800819FC, [0x8EC30010, 0, 0x046001AD, 0x00031040, 0x00431021, 0x00021080, 0x00431023, 0x3C038007, 0x8C635828, 0x000210C0, 0x00431021, 0x90420048, 0, 0x2C420080, 0x10400E26, 0, 0x0802082F, 0]);
            RequirePacket(overlay, init, 0x80085C04, [0x8E700000, 0x0C00E9C8, 0x02602021, 0x2672000C, 0x02402021, 0x0C005DC0, 0x26A5000C, 0x02602021, 0x2402FFF4, 0x2411FFFF, 0xAE02000C, 0x240201CD, 0xAE150004, 0xA6000002, 0xA6000014, 0xAE110010]);
        }
        else if (template.ClassId == 0x0CA)
        {
            RequirePacket(overlay, update, 0x800810AC, [0x8EC30010, 0, 0x04600164, 0x00031040, 0x00431021, 0x00021080, 0x00431023, 0x3C038007, 0x8C635828, 0x000210C0, 0x00431021, 0x90420048, 0, 0x2C420080, 0x1040107A, 0, 0x08020592, 0]);
            RequirePacket(overlay, init, 0x80085B84, [0x8E700000, 0x0C00E9C8, 0x02602021, 0x2672000C, 0x02402021, 0x0C005DC0, 0x26A5000C, 0x02602021, 0x2402FFF4, 0x2411FFFF, 0xAE02000C, 0x240201CD, 0xAE150004, 0xA6000002, 0xAE110010]);
        }
        else if (template.ClassId == 0x0C9)
        {
            RequirePacket(overlay, update, 0x80080DDC, [0x8E230000, 0, 0x0460120C, 0x00031040, 0x00431021, 0x00021080, 0x00431023, 0x3C038007, 0x8C635828, 0x000210C0, 0x00431821, 0x90620048, 0, 0x2C420002, 0x14400019, 0x02602021, 0x2402FFFF, 0x8C630000, 0x3C098007, 0x25296378, 0xAC620010, 0xAE220000]);
            RequirePacket(overlay, update, 0x80080EC4, [0x8E260000, 0x00002821, 0x00061840, 0x00661821, 0x00031880, 0x00661823, 0x3C068007, 0x8CC65828, 0x000318C0, 0x00661821, 0x0C005C7F, 0xA0620046]);
            RequirePacket(overlay, update, 0x80080F08, [0x8E260000, 0x24050001, 0x00061840, 0x00661821, 0x00031880, 0x00661823, 0x3C068007, 0x8CC65828, 0x000318C0, 0x00661821, 0x0C005DB2, 0xA0620045]);
            RequirePacket(overlay, update, 0x80080F40, [0x8E230000, 0x3C048007, 0x8C845828, 0x00031040, 0x00431021, 0x00021080, 0x00431023, 0x000210C0, 0x00822021, 0x0C005DD6, 0x2484000C]);
            RequirePacket(overlay, update, 0x80080F78, [0x8E230000, 0x3C048007, 0x8C845828, 0x00031040, 0x00431021, 0x00021080, 0x00431023, 0x000210C0, 0x00822021, 0x0C0138BA, 0x2484000C]);
            RequirePacket(overlay, update, 0x80080FAC, [0x8E230000, 0x3C058007, 0x8CA55828, 0x00031040, 0x00431021, 0x00021080, 0x00431023, 0x000210C0, 0x00A22821, 0x0C005DD6, 0x24A5000C]);
        }
        else
        {
            RequirePacket(overlay, update, 0x8007FEC8, [0x8E030000, 0, 0x00031040, 0x00431021, 0x00021080, 0x00431023, 0x3C038007, 0x8C635828, 0x000210C0, 0x00431021, 0x8C430000, 0x2402FFFF, 0xAC620010, 0xAE020000]);
            RequirePacket(overlay, update, 0x8007FFB8, [0x8E030000, 0, 0x0460003F, 0x00031040, 0x00431021, 0x00021080, 0x00431023, 0x3C038007, 0x8C635828, 0x000210C0, 0x00431021, 0x90430048, 0x24090003, 0x1469001A, 0x24020004, 0x02602021, 0x24050003, 0x00003021, 0x00003821, 0x2402FFFF, 0xAE020000, 0x240200F0, 0xAE00000C, 0x0C00EAF0, 0xAE020010]);
            RequirePacket(overlay, update, 0x80080058, [0x14620019, 0x02602021, 0x24050003, 0x00003021, 0x00003821, 0x2402FFFF, 0xAE020000, 0x24020078, 0xAE00000C, 0x0C00EAF0, 0xAE020010]);
            RequirePacket(overlay, update, 0x800800D0, [0x8E020000, 0x3C048007, 0x8C845828, 0x00021840, 0x00621821, 0x00031880, 0x00621823, 0x000318C0, 0x00641821, 0x8C620014, 0, 0x244201CD, 0xAC620014, 0x8E030000, 0, 0x00031040, 0x00431021, 0x00021080, 0x00431023, 0x000210C0, 0x00441021, 0x24030040, 0xA0430045]);
        }
        // The only later producers in the reciprocal target handlers clear both
        // owned sides to -1. Other +10 stores address spawned coordinates/particles.
        int propertiesRegister = template.ClassId switch { 0x0C7 => 16, 0x0C9 => 17, _ => 22 };
        uint[] clearStores = template.ClassId switch
        {
            0x0C7 => [0x8007FEFC, 0x80080008, 0x80080070],
            0x0C9 => [0x80080E30],
            _ => []
        };
        var indexStores = update.Where(pc => UniversalMips.IsStore(overlay.Word(pc)) &&
                UniversalMips.Rs(overlay.Word(pc)) == propertiesRegister && UniversalMips.SImm(overlay.Word(pc)) == indexOffset).ToArray();
        if (!indexStores.SequenceEqual(clearStores))
            throw new InvalidDataException("Gnorc Cove placed actor index has an unauthenticated update producer.");
        int target = BinaryPrimitives.ReadInt32LittleEndian(template.Properties.AsSpan(indexOffset));
        if (target < 0 || target >= data.Scene().RowCount)
            throw new InvalidDataException("Gnorc Cove placed actor index is outside its authenticated source scene.");
        int targetClass = BinaryPrimitives.ReadUInt16LittleEndian(data.Row(target).AsSpan(0x36));
        return template with { SourceRowIndices = [new(indexOffset, target, targetClass)], ImportedRowIndices = [] };
    }

    /// <summary>A surviving native actor cannot inherit a replacement placed in a deleted
    /// indexed target slot. Native indices remain physical; only explicit imports relocate.</summary>
    public static void RequireNativeRemoval(string destinationKey, UniversalLevelData destination,
        UniversalLevelData original, UniversalOverlay originalOverlay, IReadOnlySet<int> removed)
    {
        UniversalHauntedWizardPlacement.RequireNativeRemoval(destinationKey, destination, original, originalOverlay, removed);
        UniversalLoftyWhirlwindGroups.RequireNativeRemoval(destinationKey, destination, original, originalOverlay, removed);
        UniversalWhirlwindIndexedRows.RequireNativeRemoval(destinationKey, destination, original, originalOverlay, removed);
        UniversalMagicCraftersDruidIndexedRows.RequireNativeRemoval(destinationKey, destination, original, originalOverlay, removed);
        if (removed.Count == 0 || !string.Equals(destinationKey, "gnorccove", StringComparison.OrdinalIgnoreCase)) return;
        for (int row = 0; row < destination.Scene().RowCount; row++)
        {
            if (removed.Contains(row)) continue;
            int currentClass = BinaryPrimitives.ReadUInt16LittleEndian(destination.Row(row).AsSpan(0x36));
            int originalClass = row < original.Scene().RowCount ? BinaryPrimitives.ReadUInt16LittleEndian(original.Row(row).AsSpan(0x36)) : -1;
            if (!IsGnorcIndexedClass(currentClass) && !IsGnorcIndexedClass(originalClass)) continue;
            if (currentClass != originalClass || !IsGnorcIndexedClass(originalClass))
                throw new InvalidDataException("Native indexed-row removal lacks the original placed actor identity.");
            var witness = Capture(original, originalOverlay, original.CaptureRow(destinationKey, row));
            var current = destination.CaptureRow(destinationKey, row);
            var link = witness.SourceRowIndices.Single();
            if (current.Properties.Length < link.Offset + 4 ||
                BinaryPrimitives.ReadInt32LittleEndian(current.Properties.AsSpan(link.Offset)) != link.SourceRow)
                throw new InvalidDataException("Native indexed-row removal lacks the original source index producer.");
            if (removed.Contains(link.SourceRow))
                throw new InvalidDataException($"Permanent removal of row {link.SourceRow} would leave the surviving indexed actor {destinationKey} row {row}; remove its owned referencing object too. A new actor cannot inherit that index.");
        }
    }

    private static void RequirePacket(UniversalOverlay overlay, IReadOnlySet<uint> owned, uint start, uint[] words)
    {
        for (int i = 0; i < words.Length; i++)
        {
            uint pc = start + (uint)i * 4;
            if (!owned.Contains(pc) || overlay.Word(pc) != words[i])
                throw new InvalidDataException($"Gnorc Cove indexed-row source witness is unowned or changed at 0x{pc:X8}.");
        }
    }

    /// <summary>Resolves explicit imported row identity; a native destination row with the same
    /// integer is never an owner. Missing, retired and repeated copies are refused.</summary>
    public static UniversalRowTemplate Resolve(UniversalRowTemplate template, IReadOnlyList<UniversalRowTemplate> activeTemplates)
    {
        UniversalLoftyWhirlwindGroups.RequirePrerequisites(template, template.SourcePrerequisiteRows, activeTemplates);
        List<UniversalImportedRowIndex> resolved = [];
        foreach (var link in template.SourceRowIndices)
        {
            var owners = Enumerable.Range(0, activeTemplates.Count).Where(i =>
                string.Equals(activeTemplates[i].SourceLevelKey, template.SourceLevelKey, StringComparison.OrdinalIgnoreCase) &&
                activeTemplates[i].SourceRowIndex == link.SourceRow && SameGroup(template, activeTemplates[i])).ToArray();
            if (owners.Length != 1)
                throw new InvalidDataException($"{template.SourceLevelKey} row {template.SourceRowIndex}: indexed target row {link.SourceRow} must be imported exactly once (found {owners.Length}); source integers cannot select native destination rows.");
            if (activeTemplates[owners[0]].ClassId != link.SourceClass)
                throw new InvalidDataException("An indexed import target no longer owns its authenticated source class.");
            resolved.Add(new(link.Offset, owners[0], link.SourceRow, link.SourceClass));
        }
        return template with { ImportedRowIndices = resolved };
    }

    private static bool SameGroup(UniversalRowTemplate owner, UniversalRowTemplate target) =>
        string.Equals(owner.LinkGroupId ?? "", target.LinkGroupId ?? "", StringComparison.Ordinal);

    internal static void Apply(UniversalRowTemplate template, IReadOnlyList<(byte[] Row, UniversalRowTemplate Template)> rows,
        UniversalRowAllocation allocation, Span<byte> properties)
    {
        UniversalLoftyWhirlwindGroups.RequirePrerequisites(template, template.SourcePrerequisiteRows, rows.Select(r => r.Template).ToArray());
        if (template.ImportedRowIndices.Count != template.SourceRowIndices.Count ||
            template.SourceRowIndices.Select(p => p.Offset).Distinct().Count() != template.SourceRowIndices.Count ||
            template.ImportedRowIndices.Select(p => p.Offset).Distinct().Count() != template.ImportedRowIndices.Count ||
            allocation.NewRows.Count != rows.Count || allocation.NewRows.Distinct().Count() != rows.Count ||
            allocation.NewRows.Any(r => r < 0 || r >= allocation.FinalRowCount))
            throw new InvalidDataException("Indexed row links lack a complete unique physical allocation.");
        foreach (var link in template.ImportedRowIndices)
        {
            if (link.Offset < 0 || link.Offset + 4 > template.Properties.Length || link.Offset % 4 != 0 ||
                link.PlacementIndex < 0 || link.PlacementIndex >= rows.Count ||
                !template.SourceRowIndices.Contains(new(link.Offset, link.SourceRow, link.SourceClass)) ||
                template.InternalPointers.Any(p => p.Offset == link.Offset) || template.ImportedRowPointers.Any(p => p.Offset == link.Offset) ||
                BinaryPrimitives.ReadInt32LittleEndian(template.Properties.AsSpan(link.Offset)) != link.SourceRow)
                throw new InvalidDataException("An indexed row link escaped its authenticated property/source ownership.");
            var target = rows[link.PlacementIndex].Template;
            if (!string.Equals(target.SourceLevelKey, template.SourceLevelKey, StringComparison.OrdinalIgnoreCase) ||
                target.SourceRowIndex != link.SourceRow || target.ClassId != link.SourceClass || !SameGroup(template, target) ||
                rows.Count(p => string.Equals(p.Template.SourceLevelKey, template.SourceLevelKey, StringComparison.OrdinalIgnoreCase) &&
                    p.Template.SourceRowIndex == link.SourceRow && SameGroup(template, p.Template)) != 1)
                throw new InvalidDataException("An indexed target changed or became ambiguous after physical allocation.");
            BinaryPrimitives.WriteInt32LittleEndian(properties[link.Offset..], allocation.NewRows[link.PlacementIndex]);
        }
    }
}

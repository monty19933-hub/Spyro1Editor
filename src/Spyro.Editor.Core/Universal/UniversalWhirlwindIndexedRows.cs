using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Original USA whirlwind trigger indices. These integers select physical actors,
/// not pointer fixups, logical collection rows or destination rows with the same number.</summary>
public static class UniversalWhirlwindIndexedRows
{
    private sealed record Certificate(string DataSha256, string OverlaySha256, uint ClassReadPc, uint Update, uint Init, uint CopyBuffer);
    private static readonly Dictionary<string, Certificate> Sources = new(StringComparer.OrdinalIgnoreCase)
    {
        ["artisans"] = new("86cde4f886d97821a3a4b869100b586842dbd4b9fb696558bead274bb547af4f", "7df6cf2f3ed4a71a123f03621019470bd6fe0cd243f75b1e8c3a115e6b1fcb55", 0x80083724, 0x8007D9C8, 0x800857CC, 0x80088620),
        ["beastmakers"] = new("41264e1b9ab2eb877fa9890c32456f3636090159cdff4486bca47571c85117bf", "6bed996a20df44803ec4eefe434294082c3f5814ce56080e0594bab95eeb287f", 0x80084380, 0x8007E18C, 0x80087EF0, 0x8008AB70),
        ["blowhard"] = new("3dfe32f742de381626afc133b7e750bb3b21a08ce51cae4ec4ef1eee296a8bf6", "1ee0030607139491b6d076c389837d5b6c0a38e87d324fec2e14b58b47f95aef", 0x800809CC, 0x8007AF28, 0x80083AB4, 0x8008749C),
        ["clifftown"] = new("8246261aa9d5fe9f83bc138051bbfc9734af4673928808b08d1b414674babdc0", "25f0fc287cc8b6b5b70d960630025e009e0e425f7af723dadf008605af5b0286", 0x80087178, 0x8007E240, 0x80088B88, 0x8008BAF8),
        ["darkpassage"] = new("217d4e525f10b63f4ae4a413d807989241b7f2f91963b543b421cbd3954c364a", "a8b99270c37a37363824cf1fd2081c092968abef62df9e74d70ae0017b984727", 0x8008207C, 0x8007B4F8, 0x80084B94, 0x800880D4),
        ["doctorshemp"] = new("2383ceb1257e8512c80cbd8f81bf7ceadd6a3e62d26c4a870ed9577e134f2a8d", "73c7fd7ba2083507edbeca38393c825072ec2bd92e0bbbe9c3df5f5010aa8de9", 0x80080424, 0x8007AEB8, 0x800845F0, 0x80087210),
        ["dreamweavers"] = new("cadd3a2366eae64995c0eb40427410c569b6cbc8faea1ba0872639f31230c559", "dcbe76c16c51393e784037af420c7fb7c00cecd526ed4d890ae5f1b257cdb352", 0x80086574, 0x8007E3C0, 0x80088E24, 0x8008BB38),
        ["gnorccove"] = new("0f4ad8c1644a823022ac03718f3b46167a9545b202a4ef4b47d1de085c4a0d7e", "1d6f62cc80f515c73efc50b9c8435a36cb51b5e2bcea8823005ce05ed856fad9", 0x800830CC, 0x8007B528, 0x80085664, 0x80088668),
        ["hauntedtowers"] = new("970bc9e977d679f01d3ad65c8f0d07dee746ea2ede5307351dadb1c8887fb79d", "e714a2c4aff4a7e8d4980fa091540f4fdcd320b4fe4c532fa8c6c3d36fd7fbfc", 0x80082A68, 0x8007B510, 0x8008590C, 0x80089820),
        ["jacques"] = new("3f7a1ea3dcd8e1f87055e2ae746ce45f44e99b9f7a0dd8142beefe8e94f2ecac", "e09f6cb0adeb6275b04549507ed5d8424c1f6045ecfde0f50424f6ba58713f49", 0x80081EDC, 0x8007AF50, 0x800836A8, 0x80086348),
        ["loftycastle"] = new("57a61a60d5f4b4e303287bcf95b7868f135d60d42d7118fd1d1ecfb6b58beacf", "e0f1f9e14726e4ca9d41e9e8bca112290263f00ce85413a7e389ab59c9e358c3", 0x80081BA8, 0x8007B4DC, 0x80084620, 0x8008771C),
        ["metalhead"] = new("987462f78e22d41438700100d57c918e4063c214903c71f5ed668dce2727963c", "97a960c4c4e9f6933d3dc4ab19a31987a105f33dfdfd98433021977e7778543f", 0x80084A08, 0x8007B770, 0x800874FC, 0x8008A69C),
        ["stonehill"] = new("c341a3a10a67590c69d23547f6ad147f05f0b6fb7d0fb1360d572793276e796b", "876c0145649bb5b26921858d4d677468463974901dcc416dcba5ccea25caa069", 0x8008652C, 0x8007DA78, 0x8008772C, 0x8008A3B8),
        ["terracevillage"] = new("26bd02231f93d780ee75b528aac8e5797fbb0b578acfb2ba21e692872e6b5c5e", "285fc63da6b536af2b41c19e41d1db03a980007226dc55e5d930a1998f39c50e", 0x80080424, 0x8007B5DC, 0x8008465C, 0x80087944),
        ["treetops"] = new("808552324fd3925f676bb3deeee719395b1f63612d6a0f502b67f469b2d6118e", "f7363f0f2a03e6ddcc906fd68c914905c3704e49e058220941ac9a0ece488405", 0x8008420C, 0x8007B698, 0x80086B38, 0x80089848),
        ["twilightharbor"] = new("a230505350794dcae8205b142a9d60621a4c20bf4c20d3c83818c5a4f7ca0b26", "55d0845c750c0fcbd13d69633d1481d288bb3b02ce33bf60e3b17866111739dd", 0x800813A8, 0x8007AE5C, 0x80083108, 0x80086004),
        ["wizardpeak"] = new("9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8", "5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1", 0x80084A9C, 0x8007B7A8, 0x80086DD8, 0x8008A8A0),
    };

    public static uint ProveSource(string key, UniversalLevelData data, UniversalOverlay overlay)
    {
        if (!Sources.TryGetValue(key, out var proof) ||
            Convert.ToHexStringLower(SHA256.HashData(data.Bytes)) != proof.DataSha256 ||
            Convert.ToHexStringLower(SHA256.HashData(overlay.Bytes)) != proof.OverlaySha256 ||
            overlay.Update != proof.Update || overlay.Init != proof.Init || overlay.CopyBuffer != proof.CopyBuffer)
            throw new InvalidDataException("Whirlwind indexed-row ownership requires the complete authenticated original source data and overlay.");
        var sw = UniversalDispatchAnalysis.FindUpdateSwitch(overlay);
        var trace = UniversalDispatchAnalysis.Trace(overlay, sw.TreeStart, 0x12C, sw.Seeds, sw.ClassRegister);
        var owned = UniversalDispatchAnalysis.Closure(overlay, sw.Function, [trace.Stop], new HashSet<uint> { sw.TreeStart }).Instructions;
        uint start = proof.ClassReadPc - 0x30;
        uint[] packet = [0x10400025, 0, 0x8E230010, 0, 0x00031040, 0x00431021,
            0x00021080, 0x00431023, 0x3C038007, 0x8C635828, 0x000210C0, 0x00432021,
            0x84830036, 0x240200AD, 0x1462000E];
        for (int i = 0; i < packet.Length; i++)
            if (!owned.Contains(start + (uint)i * 4) || overlay.Word(start + (uint)i * 4) != packet[i])
                throw new InvalidDataException("Whirlwind indexed-row consumer packet is changed or not owned by class12C.");
        if (overlay.Word(start - 12) != 0x8E220008 || overlay.Word(start - 8) != 0 ||
            overlay.Word(start - 4) != 0x30420008)
            throw new InvalidDataException("Whirlwind bit8 index activation producer changed.");
        // Complete authenticated source binds incoming entries and other actor handlers.
        // The whirlwind's own properties register is s1; its two stores update bits2/4.
        // No s1+10 update store changes this source-scoped index.
        if (owned.Any(pc => (overlay.Word(pc) >> 26) is 40 or 41 or 43 &&
            ((overlay.Word(pc) >> 21) & 31) == 17 && (short)overlay.Word(pc) == 0x10))
            throw new InvalidDataException("Whirlwind index has an additional unauthenticated self producer.");
        return proof.ClassReadPc;
    }

    public static UniversalRowTemplate Capture(UniversalLevelData data, UniversalOverlay overlay, UniversalRowTemplate template)
    {
        if (template.ClassId != 0x12C) return template;
        ProveSource(template.SourceLevelKey, data, overlay);
        var original = data.CaptureRow(template.SourceLevelKey, template.SourceRowIndex);
        if (!template.Row.SequenceEqual(original.Row) || !template.Properties.SequenceEqual(original.Properties) ||
            !template.InternalPointers.SequenceEqual(original.InternalPointers) ||
            !template.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers) ||
            template.DroppedExternalPointers != original.DroppedExternalPointers || template.Properties.Length != 20 ||
            template.InternalPointers.Any(p => p.Offset == 0x10) || template.ExternalRowPointers.Any(p => p.Offset == 0x10))
            throw new InvalidDataException("Whirlwind indexed-row template differs from its authenticated placed source owner.");
        int flags = BinaryPrimitives.ReadInt32LittleEndian(template.Properties.AsSpan(8));
        int target = BinaryPrimitives.ReadInt32LittleEndian(template.Properties.AsSpan(0x10));
        if ((flags & 8) == 0)
        {
            if (target != -1) throw new InvalidDataException("Whirlwind inactive index lacks its original -1 witness.");
            return template with { SourceRowIndices = [], ImportedRowIndices = [] };
        }
        // Lofty rescue leaders activate flags, never this original -1 index.
        // Reverse prerequisites are enrolled separately from integer actor indices.
        if (target == -1 && UniversalLoftyWhirlwindGroups.IsNegativeOneSource(data, overlay, template))
            return template with { SourceRowIndices = [], ImportedRowIndices = [] };
        if (target < 0 || target >= data.Scene().RowCount)
            throw new InvalidDataException("Whirlwind active indexed trigger has no authenticated source target; its external producer remains unproven.");
        int targetClass = BinaryPrimitives.ReadUInt16LittleEndian(data.Row(target).AsSpan(0x36));
        if (targetClass is not (0x0AD or 0x0FA or 0x053))
            throw new InvalidDataException("Whirlwind indexed target is outside the exact native key, dragon and red-gem consumer classes.");
        return template with { SourceRowIndices = [new(0x10, target, targetClass)], ImportedRowIndices = [] };
    }

    public static void RequireNativeRemoval(string destinationKey, UniversalLevelData current,
        UniversalLevelData original, UniversalOverlay overlay, IReadOnlySet<int> removed)
    {
        if (removed.Count == 0 || !Sources.ContainsKey(destinationKey)) return;
        for (int row = 0; row < original.Scene().RowCount; row++)
        {
            if (removed.Contains(row) || BinaryPrimitives.ReadUInt16LittleEndian(original.Row(row).AsSpan(0x36)) != 0x12C) continue;
            if (row >= current.Scene().RowCount || BinaryPrimitives.ReadUInt16LittleEndian(current.Row(row).AsSpan(0x36)) != 0x12C)
                throw new InvalidDataException("A surviving native whirlwind indexed owner was changed or hidden before the native removal check.");
            var before = original.CaptureRow(destinationKey, row);
            var after = current.CaptureRow(destinationKey, row);
            if (before.Properties.Length != 20 || after.Properties.Length != 20 ||
                !before.Properties.AsSpan(8, 4).SequenceEqual(after.Properties.AsSpan(8, 4)) ||
                !before.Properties.AsSpan(16, 4).SequenceEqual(after.Properties.AsSpan(16, 4)))
                throw new InvalidDataException("A surviving native whirlwind flag/index owner changed before native row removal.");
            if ((BinaryPrimitives.ReadInt32LittleEndian(before.Properties.AsSpan(8)) & 8) == 0) continue;
            int target = BinaryPrimitives.ReadInt32LittleEndian(before.Properties.AsSpan(0x10));
            if (target == -1 && UniversalLoftyWhirlwindGroups.IsNegativeOneSource(original, overlay, before))
            { UniversalLoftyWhirlwindGroups.RequireNativeRemoval(destinationKey, current, original, overlay, removed); continue; }
            if (target < 0)
                throw new InvalidDataException("A surviving active native whirlwind has an unproven external index producer; native removals cannot retarget its dependencies.");
            if (target >= 0 && removed.Contains(target))
            {
                ProveSource(destinationKey, original, overlay);
                throw new InvalidDataException($"{destinationKey} row {row}: surviving native whirlwind depends on removed row {target}; its physical slot cannot be reused by another actor.");
            }
        }
    }

    /// <summary>Inverse class classification after an authenticated indexed actor lookup.
    /// The actor keeps its actual model class; only this comparison value is translated.</summary>
    internal static IReadOnlyList<uint> TranslateOwnedReads(UniversalAsmPart part, UniversalOverlay source,
        IReadOnlyDictionary<int, int> map)
    {
        var certificate = Sources.Values.SingleOrDefault(p =>
            p.OverlaySha256 == Convert.ToHexStringLower(SHA256.HashData(source.Bytes)));
        if (certificate == null) return [];
        if (source.Update != certificate.Update || source.Init != certificate.Init || source.CopyBuffer != certificate.CopyBuffer)
            throw new InvalidDataException("Whirlwind source callback bindings differ from their authenticated owners.");
        uint read = certificate.ClassReadPc;
        var indices = Enumerable.Range(0, part.Items.Count).Where(i =>
            (part.Items[i].SourcePc ?? part.Items[i].Orig) == read && !part.Items[i].ClassReadOwned).ToArray();
        if (indices.Length == 0) return [];
        var included = part.Items.Where(i => i.Word != null && (i.SourcePc ?? i.Orig) != null)
            .Select(i => (i.SourcePc ?? i.Orig)!.Value).ToHashSet();
        // The traced prefix may replay the bit8 load/AND before the handler stop.
        // Authenticate every retained copy, including duplicates, before inserting code.
        for (uint pc = read - 0x3C; pc <= read + 0x5C; pc += 4)
        {
            var copies = part.Items.Where(i => (i.SourcePc ?? i.Orig) == pc).ToArray();
            if (copies.Length == 0 || copies.Any(i => i.Word != source.Word(pc)))
                throw new InvalidDataException($"Whirlwind copied index/class witness is missing or changed at 0x{pc:X8}.");
            foreach (var item in copies)
            {
                var expected = UniversalMips.IsPcRelative(source.Word(pc)) ? UniversalFixKind.Branch :
                    UniversalMips.Op(source.Word(pc)) == 2 ? UniversalFixKind.Jump : UniversalFixKind.None;
                if (item.Fix.Kind != expected)
                    throw new InvalidDataException("Whirlwind copied index/class relocation witness changed.");
                if (expected is UniversalFixKind.Branch or UniversalFixKind.Jump &&
                    (item.Fix.Orig != UniversalMips.BranchTarget(source.Word(pc), pc) || item.Fix.Label != null))
                    throw new InvalidDataException("Whirlwind copied control target differs from its native owner.");
            }
        }
        uint guard = read - 0x30, end = read + 0x5C;
        foreach (int index in indices)
        {
            for (int n = -12; n <= 23; n++)
            {
                int at = index + n;
                if (at < 0 || at >= part.Items.Count ||
                    (part.Items[at].SourcePc ?? part.Items[at].Orig) != read + n * 4L)
                    throw new InvalidDataException("Whirlwind emitted consumer packet is reordered or has an inserted operation.");
            }
        }
        // Replay retains the properties/flag loads and their native NOPs, with
        // optional replay retirement NOP after each load. Native copies remain
        // exactly adjacent to their guard. The replay sole outgoing J
        // must enter the authenticated native activation guard.
        var prefixes = Enumerable.Range(0, part.Items.Count).Where(i =>
            (part.Items[i].SourcePc ?? part.Items[i].Orig) == read - 0x44).ToArray();
        if (prefixes.Length == 0) throw new InvalidDataException("Whirlwind copied property-register producer is absent.");
        foreach (int index in prefixes)
        {
            int cursor = index;
            bool replay = index + 1 < part.Items.Count && part.Items[index + 1].SourcePc == null && part.Items[index + 1].Orig == null;
            foreach (uint pc in new[] { read - 0x44, read - 0x40, read - 0x3C, read - 0x38, read - 0x34 })
            {
                if (cursor >= part.Items.Count || (part.Items[cursor].SourcePc ?? part.Items[cursor].Orig) != pc ||
                    part.Items[cursor].Word != source.Word(pc) || part.Items[cursor].Fix.Kind != UniversalFixKind.None)
                    throw new InvalidDataException("Whirlwind replayed flag producer lost source adjacency.");
                cursor++;
                if (replay && UniversalMips.IsLoad(source.Word(pc)))
                {
                    if (cursor >= part.Items.Count || part.Items[cursor].Word != 0 ||
                        part.Items[cursor].SourcePc != null || part.Items[cursor].Orig != null ||
                        part.Items[cursor].Fix.Kind != UniversalFixKind.None)
                        throw new InvalidDataException("Whirlwind replayed flag load lacks its exact retirement NOP.");
                    cursor++;
                }
            }
            if (!replay)
            {
                if (cursor >= part.Items.Count || (part.Items[cursor].SourcePc ?? part.Items[cursor].Orig) != guard)
                    throw new InvalidDataException("Whirlwind native flag producer is not adjacent to its activation guard.");
                continue;
            }
            if (cursor + 1 >= part.Items.Count || part.Items[cursor].Word != UniversalMips.EncodeJ(2, 0) ||
                part.Items[cursor].Fix.Kind != UniversalFixKind.Jump || part.Items[cursor].Fix.Orig != guard ||
                part.Items[cursor + 1].Word != 0 || part.Items[cursor + 1].Fix.Kind != UniversalFixKind.None)
                throw new InvalidDataException("Whirlwind replayed flag path no longer enters the native activation guard.");
        }
        foreach (var item in part.Items)
        {
            if (item.Fix.Kind is not (UniversalFixKind.Branch or UniversalFixKind.Jump)) continue;
            uint target = item.Fix.Orig;
            if (target <= guard || target > end) continue;
            uint? pc = item.SourcePc ?? item.Orig;
            if (pc == null || pc < guard || pc > end ||
                item.Word != source.Word(pc.Value) || target != UniversalMips.BranchTarget(source.Word(pc.Value), pc.Value))
                throw new InvalidDataException("Whirlwind consumer interior has a new or modified incoming control edge.");
        }
        if (part.Tables.Values.SelectMany(x => x).Any(pc => pc > guard && pc <= end))
            throw new InvalidDataException("Whirlwind consumer interior has an unowned incoming table entry.");
        var interiorLabels = part.Items.Where(i => (i.SourcePc ?? i.Orig) > guard &&
            (i.SourcePc ?? i.Orig) <= end && i.Label != null).Select(i => i.Label).ToHashSet();
        if (part.Items.Any(i => i.Fix.Label != null && interiorLabels.Contains(i.Fix.Label)))
            throw new InvalidDataException("Whirlwind consumer interior has an unowned incoming label edge.");
        if (!UniversalClassRemapping.ComparisonOnly(source, included, read + 4, 3,
                Enumerable.Range(0, 512).ToArray()))
            throw new InvalidDataException("Whirlwind indexed class value escapes its comparison-only consumer.");
        foreach (int index in indices.OrderDescending())
            if (certificate.OverlaySha256 == Sources["loftycastle"].OverlaySha256)
                UniversalLoftyWhirlwindGroups.SubstituteNegativeOne(part, index, map);
            else UniversalClassRemapping.TranslateOwnedClassification(part, index, map, "whirlwind_indexed_class");
        return indices.Select(_ => read).Distinct().ToArray();
    }
}

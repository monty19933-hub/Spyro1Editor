using System.Buffers.Binary;
using Spyro.Editor.Core.Universal;

/// <summary>No disc: group binding of synthetic Dream Weavers rows and the own-row word written when rows are appended
/// to a synthetic scene (after removals that reuse slots), and the coordinate rebase leaving row words alone.</summary>
internal static class SourceFree
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool ok, string what) { checks++; if (!ok) throw new InvalidOperationException(what); }
        void Refused(Action action, string what)
        {
            checks++;
            try { action(); } catch (InvalidDataException) { return; }
            throw new InvalidOperationException("Not refused: " + what);
        }
        int I(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
        const string Key = UniversalDreamWeaversCannonRows.SourceKey;

        UniversalRowTemplate Template(int sourceRow, int classId, int mode, int call)
        {
            byte[] row = new byte[UniversalLevelData.RowStride];
            BinaryPrimitives.WriteUInt16LittleEndian(row.AsSpan(0x36), (ushort)classId);
            int[] at = [105728, 66736, 13296];
            for (int axis = 0; axis < 3; axis++) BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(0x0C + 4 * axis), at[axis]);
            byte[] props = new byte[0x84];
            void W(int o, int v) => BinaryPrimitives.WriteInt32LittleEndian(props.AsSpan(o), v);
            W(0x18, 111053); W(0x1C, 60867); W(0x20, 13312); // trigger centre near the fool
            W(0x34, mode); W(0x3C, call); W(0x40, 26); W(0x54, classId == 0x82 ? 18 : 0x1234); W(0x58, classId == 0x82 ? -1 : 127);
            return new UniversalRowTemplate(Key, sourceRow, row, props, [], 0);
        }
        UniversalLevelData Fixture()
        {
            const int so = 0x2000, mob = 0xA4, mobSize = 0x1000, count = 4, prop = mob + mobSize;
            byte[] data = new byte[0x4000]; void Put(int at, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(at), value);
            for (int i = 0; i < 4; i++) { Put(i * 8, 0x800 + i * 0x800); Put(i * 8 + 4, i == 3 ? 0x2000 : 0x800); }
            int cursor = so + 0x88; for (int i = 0; i < 7; i++) { Put(cursor, 4); cursor += 4; }
            Put(so + mob, mobSize); Put(so + mob + 4, count); Put(so + prop, 4 + count * 24);
            for (int i = 0; i < count; i++) { int r = so + mob + 8 + i * UniversalLevelData.RowStride; Put(r, prop + 4 + i * 24); BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(r + 0x36), 0x18); }
            int pod = prop + 4 + count * 24; Put(so + pod, 4); Put(so + pod + 4, 4); Put(so + pod + 8, count);
            for (int i = 0; i < count; i++) Put(so + pod + 12 + i * 4, mob + 8 + i * UniversalLevelData.RowStride);
            return new(data);
        }
        UniversalMobyPlacement P(int row, string? group, int slot) => new(Key, row, 105728, 66736, 13296, EditorId: "p" + slot, PersistentSlot: slot) { LinkGroupId = group };
        var empty = Fixture(); HashSet<int> none = [];

        var armored = Template(13, 0x82, 0, 11000); var winged = Template(12, 0x5C, 0, 12000);
        UniversalMobyPlacement[] alone = [P(13, null, 0), P(12, null, 1), P(13, null, 2)];
        var a = UniversalDreamWeaversCannonRows.Bind(armored, alone[0], alone, "townsquare", empty, none, empty);
        Check(I(a.Properties, 0x3C) == 0 && I(a.Properties, 0x54) == -1 && I(a.Properties, 0x58) == -1 && a.OwnRowIndexOffsets.SequenceEqual([0x40]) && a.SourceRowIndices.Count == 0,
            "an Armored Fool without a cannon never calls, forgets its partners and names itself");
        var w = UniversalDreamWeaversCannonRows.Bind(winged, alone[1], alone, "townsquare", empty, none, empty);
        Check(I(w.Properties, 0x3C) == 0 && I(w.Properties, 0x54) == 0x1234 && I(w.Properties, 0x58) == 127 && w.OwnRowIndexOffsets.SequenceEqual([0x40]),
            "a Winged Fool without a cannon never calls; its own timers stay");
        UniversalMobyPlacement[] grouped = [P(26, "g", 0), P(27, "g", 1), P(13, "g", 2)];
        var linked = UniversalDreamWeaversCannonRows.Bind(armored, grouped[2], grouped, "townsquare", empty, none, empty);
        Check(linked.SourceRowIndices.SequenceEqual([new UniversalSourceRowIndex(0x40, 26, 0x5B)]) && I(linked.Properties, 0x3C) == 11000 && I(linked.Properties, 0x54) == -1 && linked.OwnRowIndexOffsets.Count == 0,
            "an Armored Fool in the cannon's group names the cannon and keeps calling; its absent partner is cleared");
        Refused(() => UniversalDreamWeaversCannonRows.Bind(Template(27, 0x5C, 1, 0), P(27, null, 0), [P(27, null, 0)], "townsquare", empty, none, empty), "a gunner without its cannon");
        var cannon = new UniversalRowTemplate(Key, 26, Template(26, 0x5B, 0, 0).Row, new byte[0x40], [], 0);
        Check(ReferenceEquals(UniversalDreamWeaversCannonRows.Bind(cannon, grouped[0], grouped, "townsquare", empty, none, empty), cannon), "a paired cannon binds unchanged");
        var notDreamWeavers = armored with { SourceLevelKey = "darkhollow" };
        Check(ReferenceEquals(UniversalDreamWeaversCannonRows.Bind(notDreamWeavers, alone[0], alone, "townsquare", empty, none, empty), notDreamWeavers), "rows from other levels are left alone");

        // The coordinate rebase moves the trigger point but never the row words.
        var points = UniversalPropertyCoordinates.Points(a);
        Check(points.Contains(0x18) && !points.Any(o => o <= 0x40 && o + 12 > 0x40), "the own-row word is not a coordinate");
        byte[] moved = a.Row.ToArray(); BinaryPrimitives.WriteInt32LittleEndian(moved.AsSpan(0x0C), 105728 + 4096);
        var rebased = UniversalPropertyCoordinates.Rebase(a, moved);
        Check(I(rebased.Properties, 0x18) == 111053 + 4096 && I(rebased.Properties, 0x3C) == 0 && I(rebased.Properties, 0x40) == 0, "rebase moves the trigger only");

        // Appending writes each fool's own final row, after removals that hand out reused slots.
        var b = UniversalDreamWeaversCannonRows.Bind(armored, alone[2], alone, "townsquare", empty, none, empty);
        foreach (int[] removed in new[] { Array.Empty<int>(), new[] { 1, 3 }, new[] { 0, 1, 2, 3 } })
        {
            var dest = Fixture(); UniversalRowRemoval.RemoveOwnedPropertiesAndClearRows(dest, removed);
            var (_, _, rows) = dest.AppendRows([(a.Row, a), (w.Row, w), (b.Row, b)], removed);
            Check(rows.Distinct().Count() == 3, "three rows");
            for (int i = 0; i < 3; i++)
            {
                var placed = dest.CaptureRow("fixture", rows[i]).Properties;
                Check(I(placed, 0x40) == rows[i] && I(placed, 0x3C) == 0, $"appended fool {i} names its own row {rows[i]} (removed {string.Join(",", removed)})");
                Check(!dest.Scene().Fixups.Contains(BinaryPrimitives.ReadInt32LittleEndian(dest.Row(rows[i])) + 0x40), "the own-row word is not a pointer");
            }
        }
        var overlapping = a with { InternalPointers = [(0x40, 0x10)] };
        Refused(() => Fixture().AppendRows([(overlapping.Row, overlapping)]), "an own-row word overlapping a pointer");
        var outside = a with { OwnRowIndexOffsets = [0x84] };
        Refused(() => Fixture().AppendRows([(outside.Row, outside)]), "an own-row word outside the data");
        return checks;
    }
}

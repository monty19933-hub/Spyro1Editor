using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private sealed record UniversalCompanionTemplate(AddMobyTemplate Template, Vector3f Offset);
    private sealed record UniversalCompanionClipboard(MobyClipboard Clipboard, Vector3f Offset);

    private IEnumerable<Moby> GetUniversalLinkGroupMembers(Moby member) =>
        string.IsNullOrWhiteSpace(member.UniversalLinkGroupId) || IsLooseUniversalGroupMember(member) ? [] : _currentMobys.Where(m =>
            !m.IsRemoved && m.UniversalLinkGroupId == member.UniversalLinkGroupId && !IsLooseUniversalGroupMember(m));

    /// <summary>The members that move with <paramref name="member"/>. A locked chest and its key are one group (they are
    /// copied and removed together, and Create BIN makes the chest name its key's row), but each is moved on its own:
    /// the key belongs somewhere else in the level.</summary>
    private IEnumerable<Moby> GetUniversalMoveGroupMembers(Moby member) =>
        IsLockedChestPairMember(member) ? [] : GetUniversalLinkGroupMembers(member).Where(m => !IsLockedChestPairMember(m) || ReferenceEquals(m, member));

    /// <summary>An added locked chest (class 0xAE) or key (class 0xAD).</summary>
    private static bool IsLockedChestPairMember(Moby m) =>
        m.IsAdded && m.SourceByte37 == 0 && m.SourceByte36 is UniversalLockedChestRows.ChestClass or UniversalLockedChestRows.KeyClass &&
        m.CrossLevelRequiredExporterFeature == UniversalMobyExport.RequiredExporterFeature;

    /// <summary>Where a Key Chest's key is set down when the chest is added: a few steps from the chest on floor of about
    /// its height, floating as keys do (the user then moves it to where it should be found).</summary>
    private Vector3f LockedChestKeyPlace(Vector3f chest)
    {
        float lift = UniversalGroundOffsets.For(UniversalLockedChestRows.KeyClass) ?? 19f;
        foreach (float distance in new[] { 160f, 110f, 220f })
            for (int turn = 0; turn < 8; turn++)
            {
                double a = turn * Math.PI / 4;
                float x = chest.X + distance * (float)Math.Cos(a), y = chest.Y + distance * (float)Math.Sin(a);
                if (TryFindTerrainZAt(x, y, chest.Z + 16, out float z) && Math.Abs(z - chest.Z) <= 48) return new(x, y, z + lift);
            }
        return new(chest.X + 160, chest.Y, chest.Z + lift);
    }

    /// <summary>A key set down at <paramref name="spot"/>: on the floor there, floating as keys do.</summary>
    private Vector3f LockedChestKeyAt(Vector3f spot) =>
        ApplyTerrainGroundOffset(SnapOrNearestTerrain(spot with { Z = spot.Z + 16 }), UniversalGroundOffsets.For(UniversalLockedChestRows.KeyClass) ?? 19f);

    /// <summary>Whether adding this Key Chest adds its key with it (every locked chest of the game names one).</summary>
    private static bool LockedChestBringsKey(UniversalSourceLibrary library, UniversalMobyVariant chest)
    {
        if (UniversalSpecialCatalog.IsSpecial(chest)) return false;
        try { var data = library.Get(chest.SourceLevelKey).Data; return UniversalLockedChestRows.KeyRow(data, data.CaptureRow(chest.SourceLevelKey, chest.SourceRow)) >= 0; }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException) { return false; }
    }

    /// <summary>A Dream Weavers fool in a Resize cannon's group: the group only tells the export which cannon it calls
    /// and is shot by. It moves, copies and is removed on its own, and the cannon and its gunner leave it where it is.</summary>
    private static bool IsLooseUniversalGroupMember(Moby m) =>
        m.IsAdded && UniversalDreamWeaversCannonRows.IsFoolRow(m.CrossLevelSourceLevelKey, m.CrossLevelSourceTrueIndex);

    /// <summary>The level's Resize cannon groups (each a cannon and its gunner).</summary>
    private List<string> UniversalCannonGroups() => _currentMobys.Where(m => m.IsAdded && !m.IsRemoved &&
            !string.IsNullOrWhiteSpace(m.UniversalLinkGroupId) &&
            UniversalDreamWeaversCannonRows.IsCannonRow(m.CrossLevelSourceLevelKey, m.CrossLevelSourceTrueIndex))
        .Select(m => m.UniversalLinkGroupId).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>The Resize cannon a newly added Dream Weavers fool can join: the selected object's cannon, or the level's
    /// only one.</summary>
    private string? UniversalCannonGroupForNewFool()
    {
        var groups = UniversalCannonGroups();
        if (_selectedMoby is { } selected && groups.Contains(selected.UniversalLinkGroupId)) return selected.UniversalLinkGroupId;
        return groups.Count == 1 ? groups[0] : null;
    }

    private PendingMobyAdd WithUniversalCompanions(PendingMobyAdd pending, UniversalSourceLibrary library, byte[] sourceRow)
    {
        var template = pending.Template;
        if (template.TemplateId.StartsWith(UniversalSpecialCatalog.TemplatePrefix, StringComparison.Ordinal)) return pending;
        var group = UniversalPlacementGroups.Expand(library.Get, new UniversalMobyPlacement(template.SourceLevelKey,
            template.SourceTrueIndex, BitConverter.ToInt32(sourceRow, 0x0C), BitConverter.ToInt32(sourceRow, 0x10),
            BitConverter.ToInt32(sourceRow, 0x14), EditorId: Guid.NewGuid().ToString("N"), PersistentSlot: 0));
        if (group.Count == 1) return pending;
        var root = group[0];
        var companions = group.Skip(1).Select(p =>
        {
            byte[] row = library.Get(p.SourceLevelKey).Data.Row(p.SourceRowIndex);
            string name = _universalSourceNames.TryGetValue((p.SourceLevelKey, p.SourceRowIndex), out var identity)
                ? identity.Item1 : $"Linked object ({row[0x37]:X2}{row[0x36]:X2})";
            var companion = template with
            {
                Name = name, DefaultLabel = name, Type = row[0x50], State = row[0x51],
                SourceByte36 = row[0x36], SourceByte37 = row[0x37], SourceByte4F = row[0x4F],
                Flag4A = row[0x52], Flag4B = row[0x53], YawByte = row[0x46],
                TemplateId = $"universal:{p.SourceLevelKey}:{p.SourceRowIndex}", SourceTrueIndex = p.SourceRowIndex,
                DefaultColor = Moby.ColorForType(row[0x50]), CandidateKind = "Linked object",
                TemplateNote = $"Required linked object for {pending.Label}.", SignText = ""
            };
            return new UniversalCompanionTemplate(companion, new Vector3f((p.X - root.X) / 16f,
                (p.Y - root.Y) / 16f, (p.Z - root.Z) / 16f));
        }).ToArray();
        return pending with { UniversalLinkGroupId = root.LinkGroupId ?? "", UniversalCompanions = companions };
    }

    private void AppendPendingUniversalCompanions(PendingMobyAdd pending, Moby root, List<Moby> added,
        ref int index, ref int trueIndex)
    {
        root.UniversalLinkGroupId = pending.UniversalLinkGroupId;
        foreach (var companion in pending.UniversalCompanions)
        {
            var t = companion.Template;
            Vector3f p = new(root.Position.X + companion.Offset.X, root.Position.Y + companion.Offset.Y, root.Position.Z + companion.Offset.Z);
            // A locked chest's key lies far off in its own level: here it starts beside the chest.
            if (root.SourceByte36 == UniversalLockedChestRows.ChestClass && root.SourceByte37 == 0 && t.SourceByte36 == UniversalLockedChestRows.KeyClass && t.SourceByte37 == 0)
                p = LockedChestKeyPlace(root.Position);
            var member = CreateAddedMobyFromTemplate(t, t.DefaultLabel, p, index++, trueIndex++, t.Type, t.State,
                t.SourceByte36, t.SourceByte4F, t.Flag4B, t.YawByte, pending.SelectedGem);
            member.UniversalLinkGroupId = pending.UniversalLinkGroupId;
            added.Add(member);
        }
    }

    private MobyClipboard WithUniversalClipboardCompanions(MobyClipboard clipboard, Moby root) => clipboard with
    {
        UniversalCompanions = GetUniversalLinkGroupMembers(root).Where(m => !ReferenceEquals(m, root))
            .Select(m => new UniversalCompanionClipboard(MobyClipboard.From(m, _currentLevel, null),
                new(m.Position.X - root.Position.X, m.Position.Y - root.Position.Y, m.Position.Z - root.Position.Z))).ToArray()
    };

    private void AppendUniversalClipboardCompanions(MobyClipboard clipboard, Moby root, List<Moby> pasted,
        ref int index, ref int trueIndex)
    {
        if (clipboard.UniversalCompanions.Count == 0) return;
        string group = Guid.NewGuid().ToString("N"); root.UniversalLinkGroupId = group;
        foreach (var companion in clipboard.UniversalCompanions)
        {
            Vector3f p = new(root.Position.X + companion.Offset.X, root.Position.Y + companion.Offset.Y, root.Position.Z + companion.Offset.Z);
            var member = CreateMobyFromClipboard(companion.Clipboard, p, index++, trueIndex++);
            member.UniversalLinkGroupId = group; pasted.Add(member);
        }
    }
}

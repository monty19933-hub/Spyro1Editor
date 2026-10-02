using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private sealed record UniversalCompanionTemplate(AddMobyTemplate Template, Vector3f Offset);
    private sealed record UniversalCompanionClipboard(MobyClipboard Clipboard, Vector3f Offset);

    private IEnumerable<Moby> GetUniversalLinkGroupMembers(Moby member) =>
        string.IsNullOrWhiteSpace(member.UniversalLinkGroupId) ? [] : _currentMobys.Where(m =>
            !m.IsRemoved && m.UniversalLinkGroupId == member.UniversalLinkGroupId);

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

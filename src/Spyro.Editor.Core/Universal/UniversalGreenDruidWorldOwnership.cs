namespace Spyro.Editor.Core.Universal;

/// <summary>Original Magic/Alpine Green Druids retain native animation-group
/// IDs and physical terrain-target rows. Model/sound/code import cannot own them.</summary>
internal static class UniversalGreenDruidWorldOwnership
{
    internal const string Refusal = "Green Druid world dependencies are not relocated";

    internal static void Require(string destination, string sourceKey, int actor,
        UniversalLevelData current, UniversalLevelData original, IReadOnlySet<int> removed)
    {
        if (actor != 0x10E || !(sourceKey.Equals("magiccrafters", StringComparison.OrdinalIgnoreCase) ||
            sourceKey.Equals("alpineridge", StringComparison.OrdinalIgnoreCase))) return;
        if (!destination.Equals(sourceKey, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(Refusal + ": class 0x10E from " + sourceKey +
                " retains environment animation-group IDs and physical terrain-target rows. " +
                "Import requires the complete translated LP/HP/collision animation group and its indexed targets; sound capacity alone does not establish ownership.");
        // Same-level copying retains those native references only while the actual
        // world bank and original physical scene owners remain present unchanged.
        var a = current.Subfiles()[1]; var b = original.Subfiles()[1];
        var scene=current.Scene(); var nativeScene=original.Scene();
        int sceneAt=current.Subfiles()[3].Offset, nativeAt=original.Subfiles()[3].Offset;
        if (a.Length != b.Length || !current.Bytes.AsSpan(a.Offset, a.Length).SequenceEqual(original.Bytes.AsSpan(b.Offset, b.Length)) ||
            scene.MobyComponent!=nativeScene.MobyComponent ||
            !current.Bytes.AsSpan(sceneAt+0x88,scene.MobyComponent-0x88).SequenceEqual(original.Bytes.AsSpan(nativeAt+0x88,nativeScene.MobyComponent-0x88)) ||
            scene.RowCount < nativeScene.RowCount || removed.Count != 0)
            throw new InvalidDataException(Refusal + ": same-level copying requires the unchanged original world bank and physical scene owners.");
        for (int row = 0; row < original.Scene().RowCount; row++)
        {
            // Properties pointers may have moved during previous ordinary imports;
            // compare their contents and pointer topology through the native capture.
            var expected = original.CaptureRow(sourceKey, row); var actual = current.CaptureRow(sourceKey, row);
            byte[] Contents(UniversalRowTemplate t)
            {
                var p=t.Properties.ToArray();
                foreach(var pointer in t.InternalPointers)p.AsSpan(pointer.Offset,4).Clear();
                foreach(var pointer in t.ExternalRowPointers)p.AsSpan(pointer.Offset,4).Clear();
                return p;
            }
            if (!actual.Row.AsSpan(4).SequenceEqual(expected.Row.AsSpan(4)) ||
                !Contents(actual).AsSpan().SequenceEqual(Contents(expected)) ||
                !actual.InternalPointers.SequenceEqual(expected.InternalPointers) ||
                !actual.ExternalRowPointers.SequenceEqual(expected.ExternalRowPointers) ||
                actual.DroppedExternalPointers != expected.DroppedExternalPointers)
                throw new InvalidDataException(Refusal + ": original same-level physical scene owner " + row + " changed or was removed.");
        }
    }
}

using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Universal;

// Source-owned placement inputs. Particle dispatch and runtime acceptance are separate requirements.
public static class UniversalHauntedWizardPlacement
{
    public static bool Applies(UniversalRowTemplate t) =>
        string.Equals(t.SourceLevelKey, "hauntedtowers", StringComparison.OrdinalIgnoreCase) && t.ClassId is 0xCB or 0xCC;

    public static void RequireSource(UniversalLevelData data, UniversalOverlay overlay)
    {
        if (Convert.ToHexStringLower(SHA256.HashData(data.Bytes)) != "970bc9e977d679f01d3ad65c8f0d07dee746ea2ede5307351dadb1c8887fb79d" ||
            Convert.ToHexStringLower(SHA256.HashData(overlay.Bytes)) != "e714a2c4aff4a7e8d4980fa091540f4fdcd320b4fe4c532fa8c6c3d36fd7fbfc" ||
            overlay.Update != 0x8007B510 || overlay.Init != 0x8008590C || overlay.CopyBuffer != 0x80089820)
            throw new InvalidDataException("Haunted Towers source owner changed.");
    }

    private static void RequireTemplate(UniversalLevelData data, UniversalRowTemplate t)
    {
        var original = data.CaptureRow(t.SourceLevelKey, t.SourceRowIndex);
        if (!t.Row.SequenceEqual(original.Row) || !t.Properties.SequenceEqual(original.Properties) ||
            !t.InternalPointers.SequenceEqual(original.InternalPointers) ||
            !t.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers) ||
            t.DroppedExternalPointers != original.DroppedExternalPointers)
            throw new InvalidDataException("Haunted Towers original row or property owner changed.");
    }

    public static UniversalRowTemplate Capture(UniversalLevelData data, UniversalOverlay overlay, UniversalRowTemplate t)
    {
        if (!Applies(t)) return t;
        RequireSource(data, overlay);
        RequireTemplate(data, t);

        // Seven signed indices consumed by 81DA0..81E10 and 81E64..81EC4.
        // -1 is the native sentinel. Each live target is an original placed suit of armor.
        List<UniversalSourceRowIndex> links = [];
        foreach (int offset in t.ClassId == 0xCC ? Enumerable.Range(0,7).Select(i=>0x20+4*i) : new[]{0x18})
        {
            int row = BinaryPrimitives.ReadInt32LittleEndian(t.Properties.AsSpan(offset));
            if (row == -1) continue;
            if (row < 0 || row >= data.Scene().RowCount ||
                (t.ClassId == 0xCC && BinaryPrimitives.ReadUInt16LittleEndian(data.Row(row).AsSpan(0x36)) != 0xCB) ||
                t.InternalPointers.Any(p => p.Offset == offset))
                throw new InvalidDataException("Wizard armor index has no original target owner.");
            links.Add(new(offset, row, BinaryPrimitives.ReadUInt16LittleEndian(data.Row(row).AsSpan(0x36))));
        }
        return t with { SourceRowIndices = links, ImportedRowIndices = [] };
    }

    public static UniversalRowTemplate Rebase(UniversalLevelData data, UniversalOverlay overlay, UniversalRowTemplate t, byte[] placedRow)
    {
        if (!Applies(t)) return t;
        RequireSource(data, overlay);
        RequireTemplate(data, t);
        byte[] properties = t.Properties.ToArray();
        int[] delta = Enumerable.Range(0, 3).Select(axis => checked(
            BinaryPrimitives.ReadInt32LittleEndian(placedRow.AsSpan(12 + 4 * axis)) -
            BinaryPrimitives.ReadInt32LittleEndian(t.Row.AsSpan(12 + 4 * axis)))).ToArray();
        HashSet<int> coordinates = [];
        void Vector(int offset)
        {
            if (offset < 0 || offset + 12 > properties.Length || offset % 4 != 0)
                throw new InvalidDataException("World vector exceeds its source property owner.");
            for (int axis = 0; axis < 3; axis++)
            {
                int at = offset + axis * 4;
                if (!coordinates.Add(at) || t.InternalPointers.Any(p => p.Offset == at))
                    throw new InvalidDataException("World vector overlaps another field or pointer.");
                int value = checked(BinaryPrimitives.ReadInt32LittleEndian(properties.AsSpan(at)) + delta[axis]);
                BinaryPrimitives.WriteInt32LittleEndian(properties.AsSpan(at), value);
            }
        }
        var paths = t.InternalPointers.Where(p => t.ClassId == 0xCC ? p.Offset < 0x20 : p.Offset == 0xC).ToArray();
        if (paths.Length != (t.ClassId == 0xCC ? 8 : 1) || paths.Length != t.InternalPointers.Count)
            throw new InvalidDataException("Original path pointer membership changed.");
        foreach (var path in paths)
        {
            int start = path.Target;
            int end = t.InternalPointers.Where(p => p.Target > start).Select(p => p.Target).DefaultIfEmpty(properties.Length).Min();
            if (start < 0 || start + 8 > end || properties[start] == 0 || start + 8 + 16 * properties[start] != end)
                throw new InvalidDataException("Native path node count exceeds its exact block.");
            // Retain current-node, reversal and each node's fourth word (timing/flags).
            for (int node = 0; node < properties[start]; node++) Vector(start + 8 + 16 * node);
        }
        if (t.ClassId == 0xCC)
        {
            // Native 38C4C rectangle tests use XYZ followed by dimensions/yaw.
            // E8..100 are activation flags; 104..10C/128..130 are live state, never vectors.
            foreach (int offset in new[] { 0x40, 0x58, 0x70, 0x88, 0xA0, 0xB8, 0xD0, 0x110, 0x134, 0x14C }) Vector(offset);
        }
        return t with { Properties = properties };
    }

    public static void RequireInitialRow(UniversalRowTemplate template, ReadOnlySpan<byte> row)
    {
        if (!Applies(template)) return;
        if (row.Length != UniversalLevelData.RowStride) throw new InvalidDataException("Haunted actor row is truncated.");
        // Initial state, animation/frame domains, shader mode, dimensions, scale,
        // flags and collision inputs belong to the authenticated original variant.
        // Translation and yaw are authored; pods/sectors use native unrestricted sentinels.
        for (int i=4;i<row.Length;i++)
            if (i is not (>=12 and <24) && i is not (0x43 or 0x46 or 0x4A) && row[i]!=template.Row[i])
                throw new InvalidDataException("This Haunted actor needs its original state, animation and material settings.");
        if (row[0x43]!=0xFF || row[0x4A]!=0xFF)
            throw new InvalidDataException("Imported Haunted actors require unrestricted native activation and visibility.");
    }

    public static void RequireNativeRemoval(string destinationKey, UniversalLevelData destination,
        UniversalLevelData original, UniversalOverlay originalOverlay, IReadOnlySet<int> removed)
    {
        if (!string.Equals(destinationKey,"hauntedtowers",StringComparison.OrdinalIgnoreCase) || removed.Count==0) return;
        RequireSource(original,originalOverlay);
        for(int i=0;i<original.Scene().RowCount;i++)
        {
            var t=original.CaptureRow(destinationKey,i);
            if(!Applies(t)||removed.Contains(i))continue;
            var links=Capture(original,originalOverlay,t).SourceRowIndices;
            if(!links.Any(l=>removed.Contains(l.SourceRow)))continue;
            // A surviving original owner cannot hide its relationship through an edited
            // class or index and inherit a new actor allocated into the deleted slot.
            var current=destination.CaptureRow(destinationKey,i);
            if(current.ClassId!=t.ClassId || !current.Properties.SequenceEqual(t.Properties))
                throw new InvalidDataException("An edited Haunted owner cannot reuse a removed linked actor slot.");
            throw new InvalidDataException("Remove the linked Haunted owner together with its required actor.");
        }
    }

    internal static void RequireNative(byte[] exe)
    {
        // Entire native moby helper owner contains timer37F90, rotated rectangle
        // 38C4C and path39E94, including their helper bodies. Source path flags
        // retain waitFF/no pod wait and collision bounds0.
        if (exe.Length < 0x2AF20 || Convert.ToHexStringLower(SHA256.HashData(exe.AsSpan(0x28790,10128))) != "5b7e67a79df04ef66ee63fda7d084b73989a87a7886cce5de46db1ad6e93b222")
            throw new InvalidDataException("Haunted placement lost its native timer, rectangle or path helper owner.");
    }

    internal static void RequireCopiedPlacementInputs(UniversalAsmPart part, UniversalOverlay source, IReadOnlyList<int> classes)
    {
        if (!classes.Any(c=>c is 0xCB or 0xCC))return;
        if(Convert.ToHexStringLower(SHA256.HashData(source.Bytes))!="e714a2c4aff4a7e8d4980fa091540f4fdcd320b4fe4c532fa8c6c3d36fd7fbfc" ||
            source.Update!=0x8007B510 || source.Init!=0x8008590C || source.CopyBuffer!=0x80089820)
            throw new InvalidDataException("Haunted copied placement consumers lost their source callback owner.");
        // Compare the complete reachable actor body, including source state writes,
        // rectangle consumers and clamp operands, before any owned adapter runs.
        var expected = UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,classes,part.Items.First(x=>x.Label!=null).Label!).Part;
        foreach(var c in classes.Where(c=>c is 0xCB or 0xCC))
        {
            uint lo=c==0xCB?0x8008121Cu:0x80081AC0u, hi=c==0xCB?0x80081ABCu:0x8008223Cu;
            var body=expected.Items.Where(x=>x.Orig>=lo&&x.Orig<=hi).ToArray();
            var positions=part.Items.Select((x,i)=>(x,i)).Where(p=>p.x.Orig>=lo&&p.x.Orig<=hi).Select(p=>p.i).ToArray();
            if(body.Length==0 || positions.Length!=body.Length || positions[^1]-positions[0]+1!=body.Length ||
                !part.Items.Skip(positions[0]).Take(body.Length).SequenceEqual(body))
                throw new InvalidDataException($"A Haunted actor state/path body changed, reordered or gained an instruction. {c:X} expected={body.Length} actual={positions.Length} span={(positions.Length==0?0:positions[^1]-positions[0]+1)} first={(positions.Length==0?0:positions[0])}");
        }
        List<(uint Start,uint End)> packets=[];
        if(classes.Contains(0xCB))packets.AddRange([(0x80081428,0x80081480),(0x80081844,0x800818AC)]);
        if(classes.Contains(0xCC))packets.AddRange([(0x80081D94,0x80081E10),(0x80081E54,0x80081EC4),(0x800821F4,0x80082228),(0x80081FDC,0x80082000)]);
        foreach(var (start,end) in packets)
        {
            var starts=part.Items.Select((x,i)=>(x,i)).Where(x=>x.x.Word!=null&&(x.x.Orig??x.x.SourcePc)==start).Select(x=>x.i).ToArray();
            if(starts.Length==0)throw new InvalidDataException("A Haunted placement consumer entry is missing from the copied behavior.");
            foreach(int at in starts)
            {
                int cursor=at;
                for(uint pc=start;pc<=end;pc+=4)
                {
                    if(cursor>=part.Items.Count)throw new InvalidDataException("A Haunted placement consumer is truncated.");
                    var x=part.Items[cursor++];uint word=source.Word(pc);
                    var kind=UniversalMips.IsPcRelative(word)?UniversalFixKind.Branch:UniversalMips.Op(word)==2?UniversalFixKind.Jump:UniversalFixKind.None;
                    uint target=kind==UniversalFixKind.None?0:UniversalMips.BranchTarget(word,pc);
                    if(x.Word!=word || (x.Orig??x.SourcePc)!=pc || x.Fix.Kind!=kind || x.Fix.Orig!=target)
                        throw new InvalidDataException("A Haunted placement consumer changed, reordered or gained an unowned instruction.");
                }
            }
            for(uint pc=start;pc<=end;pc+=4)
                if(part.Items.Count(x=>x.Word!=null&&(x.Orig??x.SourcePc)==pc)!=starts.Length)
                    throw new InvalidDataException("A Haunted placement consumer has an unmatched duplicate.");
            foreach(var x in part.Items.Where(x=>x.Fix.Kind is UniversalFixKind.Branch or UniversalFixKind.Jump && x.Fix.Orig>start && x.Fix.Orig<=end))
                if((x.Orig??x.SourcePc) is not uint pc || source.Word(pc)!=x.Word || UniversalMips.BranchTarget(source.Word(pc),pc)!=x.Fix.Orig)
                    throw new InvalidDataException("A Haunted placement consumer gained an unauthenticated incoming entry.");
        }
    }
}

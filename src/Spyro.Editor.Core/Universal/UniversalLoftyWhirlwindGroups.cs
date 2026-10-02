using System.Buffers.Binary;
namespace Spyro.Editor.Core.Universal;

public sealed record UniversalSourcePrerequisite(int SourceRow,int SourceClass);
public sealed record UniversalLoftyRowCapture(UniversalRowTemplate Template,IReadOnlyList<UniversalSourcePrerequisite> Prerequisites);

/// <summary>Original Lofty rescue groups: three cages release three fairies; the leader
/// activates their whirlwind by changing its flags, never by filling its -1 index.</summary>
public static class UniversalLoftyWhirlwindGroups
{
    private static readonly Dictionary<int,int[]> Groups=new()
    {
        [1]=[22,24,26,23,25,27], [2]=[28,30,67,29,31,68],
        [5]=[37,39,42,38,40,43], [7]=[59,61,64,60,62,63],
        [12]=[44,46,69,45,47,70]
    };
    private static int I(UniversalRowTemplate t,int offset)=>BinaryPrimitives.ReadInt32LittleEndian(t.Properties.AsSpan(offset));
    private static void RequireOriginal(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate t)
    {
        _=UniversalWhirlwindIndexedRows.ProveSource("loftycastle",data,overlay);
        var original=data.CaptureRow("loftycastle",t.SourceRowIndex);
        if(!t.Row.SequenceEqual(original.Row)||!t.Properties.SequenceEqual(original.Properties)||
            !t.InternalPointers.SequenceEqual(original.InternalPointers)||!t.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers)||
            t.DroppedExternalPointers!=original.DroppedExternalPointers)
            throw new InvalidDataException("Lofty rescue-group row differs from its exact original owner.");
    }
    private static void RequirePacket(UniversalOverlay overlay,uint pc,uint[] words)
    {
        for(int n=0;n<words.Length;n++)if(!overlay.Contains(pc+(uint)n*4)||overlay.Word(pc+(uint)n*4)!=words[n])
            throw new InvalidDataException("Lofty fairy/cage producer or consumer packet differs from its original owner.");
    }
    public static void ProveSource(UniversalLevelData data,UniversalOverlay overlay)
    {
        _=UniversalWhirlwindIndexedRows.ProveSource("loftycastle",data,overlay);
        // Fairy+4 is a nullable leader actor integer, guarded before address formation.
        RequirePacket(overlay,0x8007E6B0,[0x8E030004,0,0x04600020,0x00031040,0x00431021,0x00021080,0x00431023,0x000210C0,0x3C038007,0x8C635828,0x24090001,0x00431021,0x8E03001C,0x90440049,0x00691804,0x00832025,0xA0440049]);
        // Fairy+0 selects its whirlwind, then changes that whirlwind's properties flags to0x10.
        RequirePacket(overlay,0x8007E788,[0x24090010,0x8E030000,0,0x00031040,0x00431021,0x00021080,0x00431023,0x3C038007,0x8C635828,0x000210C0,0x00431021,0x8C430000,0x24020004,0xA2620048,0x24020020,0xAC690008]);
        // Cage+0 selects its fairy actor; breaking the cage enters that actor's state1.
        RequirePacket(overlay,0x8007CC2C,[0x8E230000,0x24100001,0xAE200004,0x00031040,0x00431021,0x00021080,0x00431023,0x3C038007,0x8C635828,0x000210C0,0x00431021,0xA0500048]);
        var scene=data.Scene();int before=scene.RowsOffset-UniversalLevelData.RowStride+0x36;
        if(before<0||BinaryPrimitives.ReadUInt16LittleEndian(data.Bytes.AsSpan(data.Subfiles()[3].Offset+before))!=0)
            throw new InvalidDataException("Original Lofty -1 comparison lacks its exact pre-array zero witness.");
        foreach(int row in Enumerable.Range(0,scene.RowCount).Where(r=>BinaryPrimitives.ReadUInt16LittleEndian(data.Row(r).AsSpan(0x36))==0x12C))
            if(I(data.CaptureRow("loftycastle",row),16)!=-1)throw new InvalidDataException("Original Lofty whirlwind index domain changed.");
        foreach(var group in Groups)
        {
            var wind=data.CaptureRow("loftycastle",group.Key);
            if(wind.ClassId!=0x12C||I(wind,8)!=8||I(wind,16)!=-1)throw new InvalidDataException("Original Lofty rescue whirlwind differs from its sentinel/flag owner.");
            var fairies=group.Value.Take(3).Select(r=>data.CaptureRow("loftycastle",r)).ToArray();
            if(fairies.Any(t=>t.ClassId!=0x58||I(t,0)!=group.Key)||!fairies.Select(t=>I(t,0x1C)).Order().SequenceEqual(new[]{0,1,2}))
                throw new InvalidDataException("Original Lofty three-fairy ordinal group changed.");
            var leader=fairies.Single(t=>I(t,0x1C)==0);
            if(I(leader,4)!=-1||fairies.Where(t=>t!=leader).Any(t=>I(t,4)!=leader.SourceRowIndex))
                throw new InvalidDataException("Original Lofty fairy leader links changed.");
            if(!group.Value.Skip(3).Select(r=>data.CaptureRow("loftycastle",r)).All(t=>t.ClassId==0x3D)||
                !group.Value.Skip(3).Select(r=>I(data.CaptureRow("loftycastle",r),0)).Order().SequenceEqual(group.Value.Take(3).Order()))
                throw new InvalidDataException("Original Lofty cage-to-fairy matching changed.");
        }
    }
    public static UniversalLoftyRowCapture Capture(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate t)
    {
        if(!string.Equals(t.SourceLevelKey,"loftycastle",StringComparison.OrdinalIgnoreCase)||t.ClassId is not(0x12C or 0x58 or 0x3D))return new(t,[]);
        RequireOriginal(data,overlay,t);ProveSource(data,overlay);
        if(t.ClassId==0x12C)return new(t,Groups.TryGetValue(t.SourceRowIndex,out var members)?members.Select(r=>new UniversalSourcePrerequisite(r,BinaryPrimitives.ReadUInt16LittleEndian(data.Row(r).AsSpan(0x36)))).ToArray():[]);
        int[] offsets=t.ClassId==0x58?[0,4]:[0];List<UniversalSourceRowIndex> indices=[];
        foreach(int offset in offsets)
        {
            int target=I(t,offset);
            if(target==-1&&t.ClassId==0x58&&offset==4&&I(t,0x1C)==0)continue;
            int expected=t.ClassId==0x3D||offset==4?0x58:0x12C;
            if(target<0||target>=data.Scene().RowCount||BinaryPrimitives.ReadUInt16LittleEndian(data.Row(target).AsSpan(0x36))!=expected||
                t.InternalPointers.Any(p=>p.Offset==offset)||t.ExternalRowPointers.Any(p=>p.Offset==offset))
                throw new InvalidDataException("Lofty rescue index lacks its exact native target/class owner.");
            indices.Add(new(offset,target,expected));
        }
        return new(t with{SourceRowIndices=indices,ImportedRowIndices=[]},[]);
    }
    public static void RequirePrerequisites(UniversalRowTemplate owner,IReadOnlyList<UniversalSourcePrerequisite> prerequisites,IReadOnlyList<UniversalRowTemplate> active)
    {
        if(!string.Equals(owner.SourceLevelKey,"loftycastle",StringComparison.OrdinalIgnoreCase)||owner.ClassId!=0x12C||!Groups.TryGetValue(owner.SourceRowIndex,out var members))
        {if(prerequisites.Count!=0)throw new InvalidDataException("Unowned Lofty reverse prerequisites.");return;}
        var expected=members.Select((row,n)=>new UniversalSourcePrerequisite(row,n<3?0x58:0x3D)).ToArray();
        if(!prerequisites.SequenceEqual(expected))throw new InvalidDataException("Lofty whirlwind must retain all six exact fairy/cage prerequisites.");
        foreach(var required in expected)
        {
            var matches=active.Where(t=>string.Equals(t.SourceLevelKey,owner.SourceLevelKey,StringComparison.OrdinalIgnoreCase)&&t.SourceRowIndex==required.SourceRow&&t.LinkGroupId==owner.LinkGroupId).ToArray();
            if(matches.Length!=1||matches[0].ClassId!=required.SourceClass)throw new InvalidDataException("Lofty rescue prerequisite is missing, duplicated, changed or in a different instance.");
        }
    }
    public static bool IsNegativeOneSource(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate owner)
    {
        if(!string.Equals(owner.SourceLevelKey,"loftycastle",StringComparison.OrdinalIgnoreCase)||owner.ClassId!=0x12C||owner.Properties.Length!=20||I(owner,16)!=-1)return false;
        RequireOriginal(data,overlay,owner);ProveSource(data,overlay);return true;
    }
    public static void RequireNativeRemoval(string key,UniversalLevelData current,UniversalLevelData original,UniversalOverlay overlay,IReadOnlySet<int> removed)
    {
        if(removed.Count==0||!string.Equals(key,"loftycastle",StringComparison.OrdinalIgnoreCase))return;ProveSource(original,overlay);
        for(int row=0;row<original.Scene().RowCount;row++)
        {
            var before=original.CaptureRow(key,row);if(before.ClassId is not(0x12C or 0x58 or 0x3D)||removed.Contains(row))continue;
            if(row>=current.Scene().RowCount||BinaryPrimitives.ReadUInt16LittleEndian(current.Row(row).AsSpan(0x36))!=before.ClassId)throw new InvalidDataException("A native Lofty rescue owner was changed or hidden before removal.");
            var after=current.CaptureRow(key,row);int[] offsets=before.ClassId==0x58?[0,4]:before.ClassId==0x3D?[0]:[8,16];
            if(offsets.Any(offset=>!before.Properties.AsSpan(offset,4).SequenceEqual(after.Properties.AsSpan(offset,4))))throw new InvalidDataException("A native Lofty rescue link/flag changed before removal.");
            var captured=Capture(original,overlay,before);
            if(captured.Template.SourceRowIndices.Any(link=>removed.Contains(link.SourceRow))||captured.Prerequisites.Any(link=>removed.Contains(link.SourceRow)))throw new InvalidDataException("A surviving native Lofty rescue owner depends on a removed companion.");
        }
    }
    internal static void SubstituteNegativeOne(UniversalAsmPart part,int index,IReadOnlyDictionary<int,int> map)
    {
        var item=part.Items[index];if(item.Word!=0x84830036||item.Fix.Kind!=UniversalFixKind.None)throw new InvalidDataException("Lofty sentinel comparison lacks its exact copied class-load owner.");
        string tag=$"lofty_sentinel_{index}_{item.SourcePc??item.Orig:X8}";UniversalAsmPart branch=new();
        branch.Emit(0);branch.Emit(UniversalMips.EncodeI(9,3,3,1));branch.Emit(UniversalMips.EncodeI(5,3,0,0),new(UniversalFixKind.BranchLabel,Label:tag+"_native"));branch.Emit(0);
        branch.Emit(UniversalMips.EncodeI(9,0,3,0));branch.Emit(UniversalMips.EncodeJ(2,0),new(UniversalFixKind.JumpLabel,Label:tag+"_done"));branch.Emit(0);branch.Label(tag+"_native");
        int native=branch.Items.Count;branch.Emit(0x84830036);branch.Label(tag+"_done");
        // Exact -1 reuses the source's zero comparison result. Every other index
        // executes the unchanged actor load and the same owned inverse classification.
        part.Items[index]=item with{Word=0x8E230010,ClassReadOwned=true};part.Items.InsertRange(index+1,branch.Items);
        UniversalClassRemapping.TranslateOwnedClassification(part,index+1+native,map,"lofty_actual_index");
    }
}

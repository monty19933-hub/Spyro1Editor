using System.Buffers.Binary;
namespace Spyro.Editor.Core.Universal;

/// <summary>Private source-owned row/pod/shared-flag/path serializer prerequisite.
/// Capturing this metadata never authorizes dropping13B or importing a negative
/// integer without an independently issued selector/state/native certificate.</summary>
internal static class UniversalMagicDruidChainResources
{
    internal static bool IsChain(UniversalRowTemplate t)=>t.SourceLevelKey=="magiccrafters"&&t.SourceRowIndex is >=2 and <=5;
    private static readonly int[] Members=[2,3,4,5];
    private static int I(byte[] b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
    private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Magic four-druid chain: "+why);}
    internal static UniversalRowTemplate CaptureSource(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate template)
    {
        UniversalMagicCraftersDruidIndexedRows.ProveSource("magiccrafters",data,overlay);
        Require(IsChain(template),"row has no original selector1 chain owner");
        var original=data.CaptureRow("magiccrafters",template.SourceRowIndex);
        Require(template.ClassId==271&&template.Row.SequenceEqual(original.Row)&&template.Properties.SequenceEqual(original.Properties)&&
            template.InternalPointers.SequenceEqual(original.InternalPointers)&&template.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers)&&
            template.DroppedExternalPointers==original.DroppedExternalPointers,"source row/bytes/pointer ownership changed");
        Require(template.Row[0x43]==0&&template.Row[0x48]==0&&template.Row[0x49]==0&&I(template.Properties,20)==1&&I(template.Properties,40)==-1,
            "original placed pod/state/selector/second integer required");
        Require(I(template.Properties,36)==(template.SourceRowIndex<5?template.SourceRowIndex+1:-1),"source chain integer changed");
        Require(template.Properties.Length==120&&template.InternalPointers.Count==2&&template.InternalPointers.Contains((0,80))&&
            template.InternalPointers.Contains((60,76))&&template.Properties[80]==2&&template.Properties[81]==0&&
            BinaryPrimitives.ReadInt16LittleEndian(template.Properties.AsSpan(86))==-1&&I(template.Properties,76)==0,
            "exact two-node path/current/direction/shared-zero flag owner changed");
        var sc=data.Scene();int so=data.Subfiles()[3].Offset,pod=sc.PodComponent;
        Require(I(data.Bytes,so+pod)==36&&I(data.Bytes,so+pod+4)==3&&I(data.Bytes,so+pod+8)==20,
            "original pod header owner changed");
        for(int i=0;i<Members.Length;i++)Require(BinaryPrimitives.ReadUInt16LittleEndian(data.Bytes.AsSpan(so+pod+20+2*i))==(Members[i]|(i==3?0x8000:0)),"original pod member namespace changed");
        // The source +3C pointers really name ONE mutable word, not four coincident
        // zero values. The full original fixup/component owner authenticates it.
        int flag=I(data.Bytes,so+I(data.Row(2),0)+60);
        Require(flag==0xE0F0&&Members.All(row=>I(data.Bytes,so+I(data.Row(row),0)+60)==flag),"source shared flag identity changed");
        return template with{SourceRowIndices=template.SourceRowIndex<5?[new(36,template.SourceRowIndex+1,271)]:[],ImportedRowIndices=[],
            SourcePrerequisiteRows=Members.Where(row=>row!=template.SourceRowIndex).Select(row=>new UniversalSourcePrerequisite(row,271)).ToArray()};
    }
    internal static void RequireGroup(UniversalRowTemplate owner,IReadOnlyList<UniversalRowTemplate> active)
    {
        Require(IsChain(owner)&&owner.ClassId==271&&!string.IsNullOrWhiteSpace(owner.LinkGroupId),"native chain must have its own group identity");
        Require(owner.SourcePrerequisiteRows.SequenceEqual(Members.Where(row=>row!=owner.SourceRowIndex).Select(row=>new UniversalSourcePrerequisite(row,271))),"whole native pod prerequisite metadata changed");
        foreach(int row in Members)
            Require(active.Count(t=>IsChain(t)&&t.SourceRowIndex==row&&(t.LinkGroupId??"")==owner.LinkGroupId&&t.ClassId==271)==1,"all four original druids must occur exactly once in the same group");
    }
    internal static UniversalRowTemplate Rebase(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate raw,UniversalMobyPlacement placement)
    {
        var t=CaptureSource(data,overlay,raw);
        Require(placement.SourceLevelKey==t.SourceLevelKey&&placement.SourceRowIndex==t.SourceRowIndex&&
            (placement.Yaw==null||placement.Yaw==t.Row[70])&&(placement.RowByteOverrides==null||placement.RowByteOverrides.All(f=>f.Key is 0x50 or 0x51&&f.Value==t.Row[f.Key]))&&placement.Text==null&&placement.SpecialId==null&&placement.DragonColor==null,
            "only source yaw and common authored XYZ may change");
        long[] delta=[(long)placement.X-I(t.Row,12),(long)placement.Y-I(t.Row,16),(long)placement.Z-I(t.Row,20)];byte[] props=t.Properties.ToArray();
        foreach(int at in new[]{4,88,104})for(int axis=0;axis<3;axis++)
        {
            long value=I(props,at+4*axis)+delta[axis];Require(value>=int.MinValue&&value<=int.MaxValue,"source-derived home/path translation overflows signed32");
            if(axis<2)Require(value>=UniversalMagicPairMovementBoundary.Minimum&&value<UniversalMagicPairMovementBoundary.MaximumExclusive,"translated home/path leaves retained native grid");
            BinaryPrimitives.WriteInt32LittleEndian(props.AsSpan(at+4*axis),(int)value);
        }
        return t with{Properties=props,LinkGroupId=placement.LinkGroupId};
    }
    internal static void AppendOwnedPods(UniversalLevelData destination,IReadOnlyList<(byte[] Row,UniversalRowTemplate Template)> authored,IReadOnlyList<int> physical)
    {
        Require(authored.Count==physical.Count&&physical.Distinct().Count()==physical.Count&&physical.All(row=>row>=0&&row<destination.Scene().RowCount),"physical allocation is missing or ambiguous");
        var active=authored.Select(x=>x.Template).ToArray();foreach(var t in active)RequireGroup(t,active);
        foreach(var group in active.Select(t=>t.LinkGroupId!).Distinct(StringComparer.Ordinal))
        {
            int[] positions=Members.Select(row=>Array.FindIndex(active,t=>t.SourceRowIndex==row&&t.LinkGroupId==group)).ToArray();
            int[] rows=positions.Select(i=>physical[i]).ToArray();
            AppendPod(destination,rows);
            var sc=destination.Scene();int so=destination.Subfiles()[3].Offset;
            int firstProperties=I(destination.Row(rows[0]),0),flag=checked(firstProperties+76);
            Require(sc.Fixups.Contains(firstProperties+60)&&I(destination.Bytes,so+firstProperties+60)==flag&&I(destination.Bytes,so+flag)==0,"leader shared flag lacks its exact fixed-up allocation owner");
            foreach(int row in rows)
            {
                int properties=I(destination.Row(row),0);
                Require(sc.Fixups.Contains(properties+60)&&sc.Fixups.Contains(properties)&&properties>=sc.PropertiesComponent+4&&properties+120<=sc.PropertiesEnd,"complete druid properties/path/fixup owner missing");
                // Keep each original fixup LOCATION; replace its target with the
                // same group's allocated leader flag. Native loader fixes it once.
                BinaryPrimitives.WriteInt32LittleEndian(destination.Bytes.AsSpan(so+properties+60),flag);
            }
        }
    }
    private static void AppendPod(UniversalLevelData data,IReadOnlyList<int> rows)
    {
        var sc=data.Scene();var sf=data.Subfiles()[3];byte[] scene=data.Bytes.AsSpan(sf.Offset,sf.Length).ToArray();
        int oldSize=I(scene,sc.PodComponent),oldCount=I(scene,sc.PodComponent+4);Require(oldCount<255&&rows.Count==4&&rows.All(row=>row<256),"native pod/save namespace exhausted");
        const int growth=12;byte[] pod=new byte[oldSize+growth];BinaryPrimitives.WriteInt32LittleEndian(pod,pod.Length);BinaryPrimitives.WriteInt32LittleEndian(pod.AsSpan(4),oldCount+1);
        for(int i=0;i<oldCount;i++)BinaryPrimitives.WriteInt32LittleEndian(pod.AsSpan(8+4*i),I(scene,sc.PodComponent+8+4*i)+4);
        scene.AsSpan(sc.PodComponent+8+4*oldCount,oldSize-8-4*oldCount).CopyTo(pod.AsSpan(12+4*oldCount));BinaryPrimitives.WriteInt32LittleEndian(pod.AsSpan(8+4*oldCount),oldSize+4);
        for(int i=0;i<rows.Count;i++){BinaryPrimitives.WriteUInt16LittleEndian(pod.AsSpan(oldSize+4+2*i),(ushort)(rows[i]|(i==3?0x8000:0)));scene[sc.RowsOffset+88*rows[i]+67]=(byte)oldCount;}
        int oldEnd=sc.PodComponent+oldSize;int Move(int value)=>value>=oldEnd?checked(value+growth):value;
        byte[] rewritten=new byte[Math.Max(scene.Length,sc.UsedEnd+growth)];scene.AsSpan(0,sc.PodComponent).CopyTo(rewritten);pod.CopyTo(rewritten,sc.PodComponent);scene.AsSpan(oldEnd,sc.UsedEnd-oldEnd).CopyTo(rewritten.AsSpan(oldEnd+growth));
        foreach(int fixup in sc.Fixups)BinaryPrimitives.WriteInt32LittleEndian(rewritten.AsSpan(Move(fixup)),Move(I(scene,fixup)));
        int fixupsAt=Move(sc.FixupsOffset);for(int i=0;i<sc.Fixups.Count;i++)BinaryPrimitives.WriteInt32LittleEndian(rewritten.AsSpan(fixupsAt+4+4*i),Move(sc.Fixups[i]));
        if(rewritten.Length>sf.Length)data.GrowSubfile(3,UniversalLevelData.Align(rewritten.Length-sf.Length,2048));sf=data.Subfiles()[3];Array.Clear(data.Bytes,sf.Offset,sf.Length);rewritten.CopyTo(data.Bytes,sf.Offset);
    }
}

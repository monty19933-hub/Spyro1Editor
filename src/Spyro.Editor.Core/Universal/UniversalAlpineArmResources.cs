using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Universal;

/// <summary>Original Alpine cohort links, paths and allocation dependencies.
/// The installer separately validates the destination and retained resources.</summary>
internal static class UniversalAlpineArmResources
{
    internal const string DataHash = "507c7b327bdfe9e4c230bef7d93da5d8c935a9a9f033ad99de22a5af8dfd0ddf";
    internal const string OverlayHash = "f7c9a174f64959fb53b4058f9786df223a9c142b2289563ca31edc6a24c6d383";
    private static readonly int[][] Cohorts = [[0,1],[3,4],[24,25,26]];
    internal static void RequireSourceFamilyEnrollment(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate template)
    {
        if(template.ClassId is not(0xFD or 0x10F))return;
        bool sourceLabel=template.SourceLevelKey.Equals("alpineridge",StringComparison.OrdinalIgnoreCase);
        bool sourceOwner=Convert.ToHexStringLower(SHA256.HashData(overlay.Bytes))==OverlayHash;
        if(!sourceLabel&&!sourceOwner)return;
        Require(template.SourceLevelKey=="alpineridge","complete original Alpine source namespace changed");
        ProveSource(data,overlay);
        Require(template.SourceRowIndex>=0&&template.SourceRowIndex<data.Scene().RowCount&&
            BinaryPrimitives.ReadUInt16LittleEndian(data.Row(template.SourceRowIndex).AsSpan(54))==template.ClassId,"source family row/class identity changed");
        // The complete source hash owns the real row enumeration. Never turn an
        // unproved scalar companion index into a destination native row owner.
        Require(UniversalAlpineArmInstaller.Applies(template),"this original Alpine druid needs its complete native behavior and resources before placement; this variant is not supported yet");
    }
    private static int ClassForRow(int row)=>row is 1 or 4 or 26?0x10F:0xFD;
    private static int I(byte[] b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
    private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Alpine dynamic arms storage: "+why);}
    internal static void ProveSource(UniversalLevelData data,UniversalOverlay overlay)
    {
        Require(Convert.ToHexStringLower(SHA256.HashData(data.Bytes))==DataHash&&
            Convert.ToHexStringLower(SHA256.HashData(overlay.Bytes))==OverlayHash&&
            overlay.Update==0x8007BB00&&overlay.Init==0x8008A36C,"complete original data/overlay/callback owner changed");
        Require(Convert.ToHexStringLower(SHA256.HashData(data.Package(0x13B)))=="837e65a4f27076cf57bfab22e8a7e103983aef62eca2fd7b9668cacdfeb0bf38",
            "actual local animated13B model missing or changed");
        // The full original overlay owns incoming branches, tables, callbacks and
        // all producers. These witnesses specify integer semantics explicitly.
        Packet(0x80082344,[0x8E230020,0x9264003D,0x00031040,0x00431021,0x00021080,0x00431023,0x3C038007,0x8C635828,0x000210C0,0x1080000E,0x00628021]);
        Packet(0x800825E0,[0x8E230020,0,0x00031040,0x00431021,0x00021080,0x00431023,0x3C038007,0x8C635828,0x000210C0]);
        // Original selector5 uses props+36 as a signed physical row number,
        // writes peer state10 and initializes its own linked-activation timer210.
        Packet(0x80084F54,[0x8E230024,0,0x00031040,0x00431021,0x00021080,0x00431023,0x3C038007,0x8C635828,0x000210C0,0x00621821,0x2402000A,0xA0620048,0x240200D2,0xAE220030]);
        // Actual13B factories are reached through g_MobyInit with literal class,
        // and actor pointer in the jalr delay slot. No arbitrary constant scan.
        Packet(0x80084DA0,[0x2404013B,0x3C028007,0x8C4258CC,0,0x0040F809,0x02602821]);
        Packet(0x80085540,[0x2404013B,0x3C028007,0x8C4258CC,0,0x0040F809,0x02602821]);
        void Packet(uint pc,uint[] words){for(int i=0;i<words.Length;i++)Require(overlay.Word(pc+(uint)i*4)==words[i],"native integer/factory packet changed");}
    }
    internal static int[] Members(int row)=>Cohorts.SingleOrDefault(c=>c.Contains(row))?.ToArray()
        ??throw new InvalidDataException("Alpine dynamic arms storage: original row is outside the three proved storage cohorts.");
    internal static UniversalRowTemplate CaptureSource(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate template)
    {
        ProveSource(data,overlay);Require(template.SourceLevelKey=="alpineridge","wrong source namespace");
        int[] members=Members(template.SourceRowIndex);var original=data.CaptureRow("alpineridge",template.SourceRowIndex);
        Require(template.Row.SequenceEqual(original.Row)&&template.Properties.SequenceEqual(original.Properties)&&
            template.InternalPointers.SequenceEqual(original.InternalPointers)&&template.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers)&&
            template.DroppedExternalPointers==original.DroppedExternalPointers&&template.DroppedExternalPointers==0,"placed original row/properties/fixup ownership changed");
        int row=template.SourceRowIndex;Require(template.Row[72]==0&&template.Row[73]==0&&template.Row[67]==(row<=1?4:row<=4?5:2),"native initial state/pod changed");
        int selector=I(template.Properties,template.ClassId==0xFD?16:20);
        Require((row,template.ClassId,selector,template.Properties.Length) switch
            {(0,0xFD,0,104)or(1,0x10F,4,120)or(3,0xFD,2,104)or(4,0x10F,5,120)or(24,0xFD,4,136)or(25,0xFD,4,136)or(26,0x10F,10,280)=>true,_=>false},"row/class/selector/private-block owner changed");
        int path=template.ClassId==0xFD?64:80,count=template.ClassId==0xFD?(row is 0 or 3?2:4):(row is 1 or 4?2:12);
        Require(template.InternalPointers.Contains((template.ClassId==0xFD?12:0,path))&&
            template.InternalPointers.Count==(template.ClassId==0xFD?1:2)&&
            (template.ClassId==0xFD||template.InternalPointers.Contains((60,76)))&&
            template.Properties[path]==count&&path+8+16*count==template.Properties.Length,
            "complete native path/current/direction/flag block changed");
        var links=template.ClassId==0xFD?new[]{new UniversalSourceRowIndex(32,row==0?1:row==3?4:26,0x10F)}:
            row is 1 or 4?[new UniversalSourceRowIndex(36,row==1?0:3,0xFD)]:new[]{new UniversalSourceRowIndex(36,24,0xFD),new UniversalSourceRowIndex(40,25,0xFD)};
        Require(links.All(l=>I(template.Properties,l.Offset)==l.SourceRow&&BinaryPrimitives.ReadUInt16LittleEndian(data.Row(l.SourceRow).AsSpan(54))==l.SourceClass),"reciprocal source integer target changed");
        Require(!links.Any(l=>template.InternalPointers.Any(p=>p.Offset==l.Offset)||template.ExternalRowPointers.Any(p=>p.Offset==l.Offset)),"row integer was retyped as a pointer");
        RequirePod(data,template.Row[67],members);
        return template with{SourceRowIndices=links,ImportedRowIndices=[],SourcePrerequisiteRows=members.Where(r=>r!=row).Select(r=>new UniversalSourcePrerequisite(r,ClassForRow(r))).ToArray()};
    }
    internal static void RequireGroup(UniversalRowTemplate owner,IReadOnlyList<UniversalRowTemplate> active)
    {
        Require(owner.SourceLevelKey=="alpineridge"&&!string.IsNullOrWhiteSpace(owner.LinkGroupId),"cohort needs its own group identity");
        int[] members=Members(owner.SourceRowIndex);
        Require(owner.SourcePrerequisiteRows.SequenceEqual(members.Where(r=>r!=owner.SourceRowIndex).Select(r=>new UniversalSourcePrerequisite(r,ClassForRow(r)))),"whole pod prerequisite metadata changed");
        foreach(int row in members)Require(active.Count(t=>t.SourceLevelKey=="alpineridge"&&t.SourceRowIndex==row&&t.LinkGroupId==owner.LinkGroupId&&t.ClassId==(ClassForRow(row)))==1,"each actual pod member must occur exactly once in its own group");
    }
    internal static UniversalRowTemplate Rebase(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate raw,UniversalMobyPlacement placement)
    {
        var t=CaptureSource(data,overlay,raw);Require(placement.SourceLevelKey==t.SourceLevelKey&&placement.SourceRowIndex==t.SourceRowIndex&&
            (placement.Yaw==null||placement.Yaw==t.Row[70])&&(placement.RowByteOverrides==null||placement.RowByteOverrides.All(f=>f.Key is 0x50 or 0x51&&f.Value==t.Row[f.Key]))&&placement.SpecialId==null&&placement.Text==null&&placement.DragonColor==null,
            "only authored common translation with native yaw is proved");
        long[] delta=[(long)placement.X-I(t.Row,12),(long)placement.Y-I(t.Row,16),(long)placement.Z-I(t.Row,20)];
        var props=t.Properties.ToArray();int path=t.ClassId==0xFD?64:80,count=props[path];
        foreach(int at in new[]{t.ClassId==0xFD?0:4}.Concat(Enumerable.Range(0,count).Select(i=>path+8+16*i)))
            for(int axis=0;axis<3;axis++){long v=I(props,at+4*axis)+delta[axis];Require(v>=int.MinValue&&v<=int.MaxValue,"authored home/path signed32 overflow");if(axis<2)Require(v>=8192&&v<253952,"authored home/path leaves the bounded grid domain");BinaryPrimitives.WriteInt32LittleEndian(props.AsSpan(at+4*axis),(int)v);}
        return t with{Properties=props,LinkGroupId=placement.LinkGroupId};
    }
    internal static void RequirePod(UniversalLevelData data,int pod,IReadOnlyList<int> members)
    {
        var sc=data.Scene();int at=data.Subfiles()[3].Offset+sc.PodComponent,size=I(data.Bytes,at),count=I(data.Bytes,at+4);
        Require(pod>=0&&pod<count&&count<256&&size>=8+4*count&&at+size<=data.Bytes.Length,"pod header outside its owner");
        int off=I(data.Bytes,at+8+4*pod);Require(off>=8+4*count&&off+2*members.Count<=size,"pod members outside their owner");
        for(int i=0;i<members.Count;i++)Require(BinaryPrimitives.ReadUInt16LittleEndian(data.Bytes.AsSpan(at+off+2*i))==(members[i]|(i==members.Count-1?0x8000:0)),"exact final native pod member/terminator changed");
    }
    internal static void AppendPods(UniversalLevelData data,UniversalLevelData source,UniversalOverlay sourceOverlay,IReadOnlyList<(byte[] Row,UniversalRowTemplate Template)> authored,IReadOnlyList<int> physical)
    {
        ProveSource(source,sourceOverlay);
        foreach(var (row,t) in authored)
        {
            var placement=new UniversalMobyPlacement(t.SourceLevelKey,t.SourceRowIndex,I(row,12),I(row,16),I(row,20),Yaw:row[70]){LinkGroupId=t.LinkGroupId};
            var expected=Rebase(source,sourceOverlay,source.CaptureRow(t.SourceLevelKey,t.SourceRowIndex),placement);
            Require(t.Row.SequenceEqual(expected.Row)&&t.Properties.SequenceEqual(expected.Properties)&&t.InternalPointers.SequenceEqual(expected.InternalPointers)&&t.ExternalRowPointers.SequenceEqual(expected.ExternalRowPointers)&&t.DroppedExternalPointers==expected.DroppedExternalPointers&&t.SourceRowIndices.SequenceEqual(expected.SourceRowIndices)&&t.SourcePrerequisiteRows.SequenceEqual(expected.SourcePrerequisiteRows),"post-capture source/selector/index/path/pointer ownership changed");
            // Class aliases/model registrations and save lifetimes are deliberately
            // separate premises; storage accepts only the native namespace width.
            Require(BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(54))<512,"physical model namespace exceeds native table");
            var comparison=row.ToArray();expected.Row.AsSpan(0,4).CopyTo(comparison);expected.Row.AsSpan(12,12).CopyTo(comparison.AsSpan(12));expected.Row.AsSpan(54,2).CopyTo(comparison.AsSpan(54));comparison[74]=expected.Row[74];
            Require(comparison.SequenceEqual(expected.Row)&&(row[74]==expected.Row[74]||row[74]==255),"authored row changed unsupported native state/yaw/pod/resource bytes");
        }
        foreach(var group in authored.GroupBy(r=>r.Template.LinkGroupId,StringComparer.Ordinal))
        {
            var first=group.First();long[] delta=[(long)I(first.Row,12)-I(first.Template.Row,12),(long)I(first.Row,16)-I(first.Template.Row,16),(long)I(first.Row,20)-I(first.Template.Row,20)];
            Require(group.All(r=>Enumerable.Range(0,3).All(axis=>(long)I(r.Row,12+4*axis)-I(r.Template.Row,12+4*axis)==delta[axis])),"cohort lost its authored common XYZ translation");
        }
        Require(authored.Count==physical.Count&&physical.Distinct().Count()==physical.Count&&physical.All(r=>r>=0&&r<256&&r<data.Scene().RowCount),"physical row allocation invalid");
        var active=authored.Select(r=>r.Template).ToArray();foreach(var owner in active)RequireGroup(owner,active);
        foreach(string group in active.Select(t=>t.LinkGroupId!).Distinct(StringComparer.Ordinal))
        {
            var owned=active.Where(t=>t.LinkGroupId==group).ToArray();var members=Members(owned[0].SourceRowIndex);
            Require(owned.Length==members.Length&&owned.All(t=>members.Contains(t.SourceRowIndex)),"different native cohorts share one group identity");
            int[] rows=members.Select(row=>physical[Array.FindIndex(active,t=>t.SourceRowIndex==row&&t.LinkGroupId==group)]).ToArray();
            var sc=data.Scene();var sf=data.Subfiles()[3];byte[] scene=data.Bytes.AsSpan(sf.Offset,sf.Length).ToArray();int oldSize=I(scene,sc.PodComponent),oldCount=I(scene,sc.PodComponent+4);
            Require(oldCount<255,"pod namespace exhausted");int growth=4+UniversalLevelData.Align(2*rows.Length,4);byte[] pod=new byte[oldSize+growth];
            BinaryPrimitives.WriteInt32LittleEndian(pod,pod.Length);BinaryPrimitives.WriteInt32LittleEndian(pod.AsSpan(4),oldCount+1);
            for(int i=0;i<oldCount;i++)BinaryPrimitives.WriteInt32LittleEndian(pod.AsSpan(8+4*i),I(scene,sc.PodComponent+8+4*i)+4);
            scene.AsSpan(sc.PodComponent+8+4*oldCount,oldSize-8-4*oldCount).CopyTo(pod.AsSpan(12+4*oldCount));BinaryPrimitives.WriteInt32LittleEndian(pod.AsSpan(8+4*oldCount),oldSize+4);
            for(int i=0;i<rows.Length;i++){BinaryPrimitives.WriteUInt16LittleEndian(pod.AsSpan(oldSize+4+2*i),(ushort)(rows[i]|(i==rows.Length-1?0x8000:0)));scene[sc.RowsOffset+88*rows[i]+67]=(byte)oldCount;}
            int oldEnd=sc.PodComponent+oldSize;int Move(int v)=>v>=oldEnd?checked(v+growth):v;byte[] rewritten=new byte[Math.Max(scene.Length,sc.UsedEnd+growth)];scene.AsSpan(0,sc.PodComponent).CopyTo(rewritten);pod.CopyTo(rewritten,sc.PodComponent);scene.AsSpan(oldEnd,sc.UsedEnd-oldEnd).CopyTo(rewritten.AsSpan(oldEnd+growth));
            foreach(int fixup in sc.Fixups)BinaryPrimitives.WriteInt32LittleEndian(rewritten.AsSpan(Move(fixup)),Move(I(scene,fixup)));
            int fixups=Move(sc.FixupsOffset);for(int i=0;i<sc.Fixups.Count;i++)BinaryPrimitives.WriteInt32LittleEndian(rewritten.AsSpan(fixups+4+4*i),Move(sc.Fixups[i]));
            if(rewritten.Length>sf.Length)data.GrowSubfile(3,UniversalLevelData.Align(rewritten.Length-sf.Length,2048));sf=data.Subfiles()[3];Array.Clear(data.Bytes,sf.Offset,sf.Length);rewritten.CopyTo(data.Bytes,sf.Offset);RequirePod(data,oldCount,rows);
        }
    }
}

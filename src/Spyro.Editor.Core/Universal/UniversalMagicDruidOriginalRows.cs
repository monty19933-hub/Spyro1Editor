using System.Buffers.Binary;
namespace Spyro.Editor.Core.Universal;
/// <summary>Exact original selector0/3 singleton and second selector2 pair resource owner. Capture does not
/// issue source table pruning; only the aggregate source/native issuer may do so.</summary>
internal static class UniversalMagicDruidOriginalRows
{
    internal static bool IsSingle(UniversalRowTemplate t)=>t.SourceLevelKey=="magiccrafters"&&t.SourceRowIndex is 1 or 11 or 16 or 25 or 26 or 27;
    internal static bool IsSecondPair(UniversalRowTemplate t)=>t.SourceLevelKey=="magiccrafters"&&t.SourceRowIndex is 32 or 33;
    internal static bool IsSupported(UniversalRowTemplate t)=>IsSingle(t)||IsSecondPair(t);
    internal static void RequirePair(UniversalRowTemplate owner,IReadOnlyList<UniversalRowTemplate> active)
    {
        Require(IsSecondPair(owner)&&owner.ClassId==271&&!string.IsNullOrWhiteSpace(owner.LinkGroupId),"second original pair requires its own group identity");int target=owner.SourceRowIndex==32?33:32;
        Require(owner.SourcePrerequisiteRows.SequenceEqual(new[]{new UniversalSourcePrerequisite(target,271)}),"second pair metadata changed");
        Require(active.Count(t=>IsSecondPair(t)&&t.SourceRowIndex==target&&t.ClassId==271&&t.LinkGroupId==owner.LinkGroupId)==1,"exact second pair companion must occur once in the same group");
    }
    private static int I(byte[] b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
    private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Magic original druid: "+why);}
    internal static UniversalRowTemplate CaptureSource(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate template)
    {
        UniversalMagicCraftersDruidIndexedRows.ProveSource("magiccrafters",data,overlay);
        Require(IsSupported(template)&&template.ClassId==271,"unsupported original selector/row");var original=data.CaptureRow("magiccrafters",template.SourceRowIndex);
        Require(template.Row.SequenceEqual(original.Row)&&template.Properties.SequenceEqual(original.Properties)&&template.InternalPointers.SequenceEqual(original.InternalPointers)&&template.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers)&&template.DroppedExternalPointers==original.DroppedExternalPointers,"complete original descriptor/properties/resource owner changed");
        Require(template.Row[67]==(IsSecondPair(template)?2:255)&&template.Row[72]==0&&template.Row[73]==0&&I(template.Properties,20)==(IsSecondPair(template)?2:template.SourceRowIndex==11?3:0)&&I(template.Properties,36)==-1&&I(template.Properties,40)==-1&&I(template.Properties,52)==(IsSecondPair(template)?template.SourceRowIndex==32?2:1:0),"placed original pod/state/selector/index/role owner changed");
        Require(template.InternalPointers.Count==2&&template.InternalPointers.Contains((0,80))&&template.InternalPointers.Contains((60,76))&&I(template.Properties,76)==0&&template.Properties[80]>=1&&template.Properties[81]<template.Properties[80]&&template.Properties.Length==88+16*template.Properties[80],"native own flag and exact path extent changed");
        if(IsSecondPair(template))
        {
            int so=data.Subfiles()[3].Offset;int flag=I(data.Bytes,so+I(data.Row(32),0)+60);Require(flag==0xEBE4&&I(data.Bytes,so+I(data.Row(33),0)+60)==flag,"original second pair shared flag identity changed");
            int pod=data.Scene().PodComponent;int members=I(data.Bytes,so+pod+16);Require(BinaryPrimitives.ReadUInt16LittleEndian(data.Bytes.AsSpan(so+pod+members))==32&&BinaryPrimitives.ReadUInt16LittleEndian(data.Bytes.AsSpan(so+pod+members+2))==(33|0x8000),"original second pair pod membership changed");
        }
        return template with{SourceRowIndices=[],ImportedRowIndices=[],SourcePrerequisiteRows=IsSecondPair(template)?[new(template.SourceRowIndex==32?33:32,271)]:[]};
    }
    internal static UniversalRowTemplate Rebase(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate raw,UniversalMobyPlacement placement)
    {
        var t=CaptureSource(data,overlay,raw);Require(placement.SourceLevelKey==t.SourceLevelKey&&placement.SourceRowIndex==t.SourceRowIndex&&(placement.Yaw==null||placement.Yaw==t.Row[70])&&(placement.RowByteOverrides==null||placement.RowByteOverrides.All(f=>f.Key is 0x50 or 0x51&&f.Value==t.Row[f.Key]))&&placement.Text==null&&placement.SpecialId==null&&placement.DragonColor==null,"only source yaw and authored XYZ may change");
        long[] delta=[(long)placement.X-I(t.Row,12),(long)placement.Y-I(t.Row,16),(long)placement.Z-I(t.Row,20)];byte[] properties=t.Properties.ToArray();
        foreach(int at in new[]{4}.Concat(Enumerable.Range(0,t.Properties[80]).Select(node=>88+16*node)))for(int axis=0;axis<3;axis++)
        {long value=I(properties,at+4*axis)+delta[axis];Require(value>=int.MinValue&&value<=int.MaxValue,"translated source home/path overflows signed32");if(axis<2)Require(value>=UniversalMagicPairMovementBoundary.Minimum&&value<UniversalMagicPairMovementBoundary.MaximumExclusive,"translated source home/path leaves retained native grid");BinaryPrimitives.WriteInt32LittleEndian(properties.AsSpan(at+4*axis),(int)value);}
        return t with{Properties=properties,LinkGroupId=placement.LinkGroupId};
    }
}

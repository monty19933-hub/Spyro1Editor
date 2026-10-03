using System.Buffers.Binary;

namespace Spyro.Editor.Core.Universal;

/// <summary>Private native deletion prerequisite for the two authenticated arm
/// cohorts. Does not enroll imported rows or authorize other source families.</summary>
internal static class UniversalAlpineArmNativeRows
{
    internal static void RequireRemoval(string sourceKey,UniversalLevelData current,
        UniversalLevelData original,UniversalOverlay source,IReadOnlySet<int> removed)
    {
        if(sourceKey!="alpineridge"||removed.Count==0)return;
        UniversalAlpineArmResources.ProveSource(original,source);
        foreach(int[] cohort in new[]{new[]{0,1},new[]{24,25,26}})
        {
            int[] surviving=cohort.Where(row=>!removed.Contains(row)).ToArray();
            if(surviving.Length==0)continue;
            foreach(int row in surviving)
            {
                if(row>=current.Scene().RowCount)throw new InvalidDataException("Alpine dynamic arms: a surviving original companion row disappeared.");
                var expected=UniversalAlpineArmResources.CaptureSource(original,source,original.CaptureRow(sourceKey,row));
                byte[] native=current.Row(row);
                if(BinaryPrimitives.ReadUInt16LittleEndian(native.AsSpan(54))!=expected.ClassId)
                    throw new InvalidDataException("Alpine dynamic arms: the class of a surviving original companion changed.");
                var props=current.CaptureRow(sourceKey,row).Properties;
                int selector=expected.ClassId==0xFD?16:20;
                if(props.Length<=selector+3||BinaryPrimitives.ReadInt32LittleEndian(props.AsSpan(selector))!=BinaryPrimitives.ReadInt32LittleEndian(expected.Properties.AsSpan(selector)))
                    throw new InvalidDataException("Alpine dynamic arms: the behavior of a surviving original companion changed.");
                foreach(var index in expected.SourceRowIndices)
                {
                    if(props.Length<=index.Offset+3||BinaryPrimitives.ReadInt32LittleEndian(props.AsSpan(index.Offset))!=index.SourceRow)
                        throw new InvalidDataException("Alpine dynamic arms: a surviving original companion index changed.");
                    if(removed.Contains(index.SourceRow))
                        throw new InvalidDataException("Remove the whole linked Alpine druid group together; a surviving actor still uses this companion.");
                }
                UniversalAlpineArmResources.RequirePod(current,native[67],cohort);
            }
            if(cohort.Any(removed.Contains))
                throw new InvalidDataException("Remove the whole linked Alpine druid group together; its native pod still has surviving members.");
        }
    }
}

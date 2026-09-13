using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeFreshMobyFamilyRowSpanRequest(string EditorId,
    IReadOnlyList<NativeMobyDependencyPrivateSchema> Rows,IReadOnlyList<NativeFreshMobyScenePointerReference> InternalPointers);
internal sealed record NativeFreshMobyFamilyRowSpan(string EditorId,int FirstRowIndex,int RowCount);

/// <summary>
/// Neutral source-schema geometry before final domains or resource graphs exist.
/// Source factories own the supplied schemas and explicit internal pointers;
/// this preflight only snapshots and projects them. Actual source reads remain
/// checked by the later template capture and allocated storage compiler.
/// </summary>
internal sealed class NativeFreshMobyFamilyRowSpanPlan
{
    private readonly IReadOnlyList<NativeFreshMobyFamilyRowSpanRequest> _requests;
    private NativeFreshMobyFamilyRowSpanPlan(NativeFreshMobyFamilyRowSpanRequest[] requests,
        NativeFreshMobyFamilyRowSpan[] placements,NativeMobyDependencyPrivateSchema[] rows,
        NativeFreshMobyScenePointerReference[] pointers,int dynamicCapacity)
    {
        _requests=Array.AsReadOnly(requests);Placements=Array.AsReadOnly(placements);
        Rows=Array.AsReadOnly(rows);PointerReferences=Array.AsReadOnly(pointers);
        Layout=NativeFreshMobySceneStorageLayout.Create(rows.Select(r=>checked((int)r.Properties.Extent.ByteLength)).ToArray(),
            dynamicCapacity,pointers.Length);
    }
    internal IReadOnlyList<NativeFreshMobyFamilyRowSpan> Placements{get;}
    internal IReadOnlyList<NativeMobyDependencyPrivateSchema> Rows{get;}
    internal IReadOnlyList<NativeFreshMobyScenePointerReference> PointerReferences{get;}
    internal NativeFreshMobySceneStorageLayout Layout{get;}

    internal static NativeFreshMobyFamilyRowSpanPlan Create(IReadOnlyList<NativeFreshMobyFamilyRowSpanRequest> requests,
        int dynamicCapacity=32)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if(requests.Count is <1 or >128||dynamicCapacity is <0 or >4096)
            throw new InvalidDataException("Family row-span geometry exceeds bounded authored placement or dynamic counts.");
        var input=Snapshot(requests,128);int totalRows=0,totalPointers=0;
        int[] rowCounts=new int[input.Length],pointerCounts=new int[input.Length];
        HashSet<string> ids=new(StringComparer.Ordinal);
        // Count every family before indexing or allocating any row snapshots.
        // A small authored placement count may expand beyond native row limits.
        for(int i=0;i<input.Length;i++)
        {
            var request=input[i];
            if(request is null||request.Rows is null||request.InternalPointers is null||!ValidId(request.EditorId)||!ids.Add(request.EditorId))
                throw new InvalidDataException("Family row-span requests need unique editor identities and complete schema/pointer lists.");
            int count=request.Rows.Count,pointers=request.InternalPointers.Count;
            if(count<1||count>256-totalRows||pointers<0||pointers>4096-totalPointers-count)
                throw new InvalidDataException("Expanded native rows or explicit fixups exceed their bounded complete layout.");
            rowCounts[i]=count;pointerCounts[i]=pointers;
            totalRows+=count;totalPointers+=count+pointers;
        }
        List<NativeFreshMobyFamilyRowSpanRequest> retained=[];
        List<NativeFreshMobyFamilyRowSpan> spans=[];
        List<NativeMobyDependencyPrivateSchema> flattened=[];
        List<NativeFreshMobyScenePointerReference> lifted=[];
        for(int requestIndex=0;requestIndex<input.Length;requestIndex++)
        {
            var request=input[requestIndex];
            if(request.Rows.Count!=rowCounts[requestIndex]||request.InternalPointers.Count!=pointerCounts[requestIndex])
                throw new InvalidDataException("Family source row count changed during bounded preflight.");
            var rows=Snapshot(request.Rows,rowCounts[requestIndex]);var pointers=Snapshot(request.InternalPointers,pointerCounts[requestIndex]);
            if(rows.Length!=rowCounts[requestIndex]||pointers.Length!=pointerCounts[requestIndex])
                throw new InvalidDataException("Family source schema or pointer count changed during its snapshot.");
            HashSet<string> roles=new(StringComparer.Ordinal);HashSet<int> sourceIndices=[];
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows[i];
                if(row is null||string.IsNullOrWhiteSpace(row.Role)||!roles.Add(row.Role)||row.SourceInstanceIndex<0||
                    !sourceIndices.Add(row.SourceInstanceIndex)||row.RequiresBytePersistentOwner&&row.SourceInstanceIndex>=256||
                    row.OwnerFields is null||row.Row is null||row.Properties is null||
                    !ValidRead(row.Row)||!ValidRead(row.Properties)||
                    !ReferenceEquals(row.Row.Extent.Space,row.Properties.Extent.Space)||row.Row.Extent.ByteLength!=0x58||
                    row.Properties.Extent.ByteLength<4||row.Properties.Extent.ByteLength>0x100000||row.Properties.Extent.ByteLength%4!=0||
                    row.PropertiesPointerBaseOffset<0||row.Properties.Extent.Offset<row.PropertiesPointerBaseOffset||
                    row.Properties.Extent.Offset-row.PropertiesPointerBaseOffset>uint.MaxValue||
                    row.Row.Extent.Offset<row.Properties.Extent.EndExclusive&&row.Properties.Extent.Offset<row.Row.Extent.EndExclusive)
                    throw new InvalidDataException("Family preflight requires complete aligned Original row/private schemas.");
                var owners=Snapshot(row.OwnerFields,256);HashSet<int> fields=[];
                foreach(var owner in owners)
                    if(owner is null||owner.PropertyByteOffset<0||owner.PropertyByteOffset%4!=0||
                        owner.PropertyByteOffset>row.Properties.Extent.ByteLength-4||!fields.Add(owner.PropertyByteOffset)||
                        owner.OwnerRoleIndex<0||owner.OwnerRoleIndex>=rows.Length)
                        throw new InvalidDataException("Family integer owner fields escape their exact private block or local role indices.");
                rows[i]=row with{OwnerFields=Array.AsReadOnly(owners)};
            }
            HashSet<(int,NativeFreshMobySceneOwnerKind,int)> pointerFields=[];
            foreach(var pointer in pointers)
            {
                if(pointer is null||pointer.FieldRowIndex<0||pointer.FieldRowIndex>=rows.Length||
                    pointer.TargetRowIndex<0||pointer.TargetRowIndex>=rows.Length||
                    !Enum.IsDefined(pointer.FieldOwner)||!Enum.IsDefined(pointer.TargetOwner)||
                    !pointerFields.Add((pointer.FieldRowIndex,pointer.FieldOwner,pointer.FieldOffset)))
                    throw new InvalidDataException("Family internal pointers need unique fields and explicit local row owners.");
                var fieldRow=rows[pointer.FieldRowIndex];var targetRow=rows[pointer.TargetRowIndex];
                var field=pointer.FieldOwner==NativeFreshMobySceneOwnerKind.Row?fieldRow.Row.Extent:fieldRow.Properties.Extent;
                var target=pointer.TargetOwner==NativeFreshMobySceneOwnerKind.Row?targetRow.Row.Extent:targetRow.Properties.Extent;
                if(!ReferenceEquals(field.Space,target.Space)||pointer.FieldOffset<0||pointer.FieldOffset%4!=0||
                    pointer.FieldOffset>field.ByteLength-4||pointer.TargetOffset<0||pointer.TargetOffset%4!=0||pointer.TargetOffset>target.ByteLength-4||
                    pointer.FieldOwner==NativeFreshMobySceneOwnerKind.Row&&pointer.FieldOffset==0||
                    pointer.FieldOwner==NativeFreshMobySceneOwnerKind.Private&&fieldRow.OwnerFields.Any(f=>f.PropertyByteOffset==pointer.FieldOffset))
                    throw new InvalidDataException("Family internal pointer escapes an aligned owner or conflicts with a row pointer/integer owner field.");
                long encoded=target.Offset+pointer.TargetOffset-fieldRow.PropertiesPointerBaseOffset;
                if(encoded<0||encoded>uint.MaxValue||pointer.ExpectedSourceValue!=(uint)encoded)
                    throw new InvalidDataException("Family internal pointer source value disagrees with its explicit Original target and pointer base.");
            }
            int first=flattened.Count;
            for(int i=0;i<rows.Length;i++)
            {
                int ordinal=first+i;var row=rows[i];
                lifted.Add(new(ordinal,NativeFreshMobySceneOwnerKind.Row,0,ordinal,NativeFreshMobySceneOwnerKind.Private,0,
                    checked((uint)(row.Properties.Extent.Offset-row.PropertiesPointerBaseOffset))));
                foreach(var pointer in pointers.Where(p=>p.FieldRowIndex==i))
                    lifted.Add(pointer with{FieldRowIndex=first+pointer.FieldRowIndex,TargetRowIndex=first+pointer.TargetRowIndex});
            }
            spans.Add(new(request.EditorId,first,rows.Length));flattened.AddRange(rows);
            retained.Add(new(request.EditorId,Array.AsReadOnly(rows),Array.AsReadOnly(pointers)));
        }
        if(flattened.Count!=totalRows||lifted.Count!=totalPointers)
            throw new InvalidDataException("Family source schema or pointer counts changed during bounded preflight.");
        return new(retained.ToArray(),spans.ToArray(),flattened.ToArray(),lifted.ToArray(),dynamicCapacity);
    }

    internal void RequireTemplates(IReadOnlyList<NativeMobyDependencyInstantiationRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var templates=Snapshot(requests,128);
        if(templates.Length!=_requests.Count)
            throw new InvalidDataException("Final family templates do not cover the ordered source-schema placements.");
        for(int i=0;i<templates.Length;i++)
        {
            var request=templates[i];var expected=_requests[i];
            if(request is null||request.Template is null||request.Id!=expected.EditorId||
                request.Activation!=NativeMobyDependencyActivation.ConcurrentLevel||request.Template.Rows.Count!=expected.Rows.Count||
                !request.Template.Rows.Zip(expected.Rows).All(pair=>SameSchema(pair.First,pair.Second)))
                throw new InvalidDataException("Final family template changed ordered identity or an actual Original private schema binding.");
        }
    }

    internal void RequireAllocation(NativeMobyDependencyInstantiationPlan allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        RequireTemplates(allocation.Families.Select(f=>new NativeMobyDependencyInstantiationRequest(f.Id,f.Template)).ToArray());
        var rows=allocation.Rows;
        if(rows.Count!=Rows.Count||allocation.FinalRowCount!=Rows.Count||allocation.PropertyBytesAllocated!=Layout.PrivateByteLength)
            throw new InvalidDataException("Allocated family storage differs from the complete preflight geometry.");
        var scene=rows[0].Row.Space;
        if(scene.Kind!=NativeAddressKind.Scene||scene.Revision!=NativeAddressRevision.Final||scene.ByteLength!=Layout.SceneByteLength)
            throw new InvalidDataException("Allocated family storage requires its exact Final Scene geometry.");
        for(int i=0;i<Placements.Count;i++)
        {
            var family=allocation.Families[i];var span=Placements[i];
            if(family.Rows.Count!=span.RowCount)
                throw new InvalidDataException("Allocated family rows lost their authored placement span.");
            for(int j=0;j<span.RowCount;j++)
                if(!ReferenceEquals(family.Rows[j],rows[span.FirstRowIndex+j]))
                    throw new InvalidDataException("Allocated family rows differ from their retained contiguous span.");
        }
        for(int i=0;i<rows.Count;i++)
        {
            var row=rows[i];
            if(row.Index!=i||!SameSchema(row.Source,Rows[i])||!ReferenceEquals(row.Row.Space,scene)||!ReferenceEquals(row.Properties.Space,scene)||
                row.Row.Offset!=Layout.RowTableOffset+i*0x58||row.Row.ByteLength!=0x58||
                row.Properties.Offset!=Layout.PrivateOffsets[i]||row.Properties.ByteLength!=Layout.PrivateByteLengths[i])
                throw new InvalidDataException("Allocated family row/private owner differs from its exact source schema and final span.");
        }
    }

    private static bool SameSchema(NativeMobyDependencyPrivateSchema a,NativeMobyDependencyPrivateSchema b)=>
        a.Role==b.Role&&a.SourceInstanceIndex==b.SourceInstanceIndex&&a.NativeClass==b.NativeClass&&
        ReferenceEquals(a.Row.Extent.Space,b.Row.Extent.Space)&&ReferenceEquals(a.Properties.Extent.Space,b.Properties.Extent.Space)&&
        a.Row==b.Row&&a.Properties==b.Properties&&a.PropertiesPointerBaseOffset==b.PropertiesPointerBaseOffset&&
        a.RequiresBytePersistentOwner==b.RequiresBytePersistentOwner&&a.OwnerFields.SequenceEqual(b.OwnerFields);
    private static bool ValidRead(NativeMobyDependencyRead read)=>read.Extent is not null&&read.Extent.Space.Revision==NativeAddressRevision.Original&&
        !string.IsNullOrWhiteSpace(read.Id)&&read.ExpectedSha256 is {Length:64}&&read.ExpectedSha256.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool ValidId(string id)=>id is {Length:32}&&id.Any(c=>c!='0')&&id.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static T[] Snapshot<T>(IReadOnlyList<T> values,int maximum)
    {
        int count=values.Count;
        if(count<0||count>maximum)throw new InvalidDataException("Family schema list exceeds its bounded count.");
        T[] copy=new T[count];for(int i=0;i<count;i++)copy[i]=values[i];return copy;
    }
}

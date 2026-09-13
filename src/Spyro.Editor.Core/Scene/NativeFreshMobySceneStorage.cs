using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Scene;

internal enum NativeFreshMobySceneOwnerKind { Row, Private }
internal sealed record NativeFreshMobyScenePointerReference(int FieldRowIndex,NativeFreshMobySceneOwnerKind FieldOwner,
    int FieldOffset,int TargetRowIndex,NativeFreshMobySceneOwnerKind TargetOwner,int TargetOffset,uint ExpectedSourceValue);
internal sealed record NativeFreshMobyScenePointerWrite(NativeFreshMobyScenePointerReference Reference,
    int FieldSceneOffset,int TargetSceneOffset,uint Before,uint After);
internal sealed record NativeFreshMobySceneStorageOwner(string Id,int Offset,int ByteLength,bool IsZero);

/// <summary>Immutable neutral bytes. Neither a native source certificate nor a placed-family approval.</summary>
internal sealed class NativeFreshMobySceneStorageRow
{
    private readonly byte[] _row,_properties;
    internal NativeFreshMobySceneStorageRow(string id,ReadOnlySpan<byte> row,ReadOnlySpan<byte> properties)
    {
        if(string.IsNullOrWhiteSpace(id)||row.Length!=0x58||properties.Length<4||properties.Length%4!=0||properties.Length>0x100000)
            throw new InvalidDataException("Fresh storage needs a named complete row and bounded aligned exact private block.");
        Id=id;_row=row.ToArray();_properties=properties.ToArray();
    }
    internal string Id{get;}
    internal int PrivateByteLength=>_properties.Length;
    internal byte[] CopyRow()=>_row.ToArray();
    internal byte[] CopyProperties()=>_properties.ToArray();
}

/// <summary>
/// Growth-aware byte geometry, not a source/runtime/destination issuer. P placed
/// rows are additional to N complete dynamic rows, N private 0x18 blocks and a
/// full next-row lookahead. N is reserved storage, never measured safe demand.
/// </summary>
internal sealed class NativeFreshMobySceneStorageLayout
{
    private NativeFreshMobySceneStorageLayout(int[] lengths,int dynamicCapacity,int fixupCount)
    {
        PrivateByteLengths=Array.AsReadOnly(lengths);PlacedCount=lengths.Length;DynamicCapacity=dynamicCapacity;FixupCount=fixupCount;
        // Create bounds the complete input before any multiplication or allocation.
        DynamicRowOffset=RowTableOffset+PlacedCount*0x58;
        DynamicRowByteLength=DynamicCapacity*0x58;
        NextRowReserveOffset=DynamicRowOffset+DynamicRowByteLength;
        DynamicPropertiesOffset=NextRowReserveOffset+NextRowReserveByteLength;
        DynamicPropertiesByteLength=DynamicCapacity*0x18;
        PropertiesHeaderOffset=DynamicPropertiesOffset+DynamicPropertiesByteLength;
        int cursor=PropertiesHeaderOffset+4;int[] offsets=new int[lengths.Length];
        for(int i=0;i<lengths.Length;i++){offsets[i]=cursor;cursor+=lengths[i];}
        PrivateOffsets=Array.AsReadOnly(offsets);PrivateByteLength=cursor-PropertiesHeaderOffset-4;
        NumericPodOffset=cursor;BucketDataOffset=NumericPodOffset+8+4;
        FixupCountOffset=BucketDataOffset+0x2000;
        UsedByteLength=FixupCountOffset+4+FixupCount*4;
        SceneByteLength=(UsedByteLength+0x7FF)&~0x7FF;
    }
    internal int PlacedCount{get;}
    internal int DynamicCapacity{get;}
    internal int RowTableOffset=>0xD4;
    internal int DynamicRowOffset{get;}
    internal int DynamicRowByteLength{get;}
    internal int NextRowReserveOffset{get;}
    internal int NextRowReserveByteLength=>0x58;
    internal int DynamicPropertiesOffset{get;}
    internal int DynamicPropertiesByteLength{get;}
    internal int PropertiesHeaderOffset{get;}
    internal int PrivateByteLength{get;}
    internal IReadOnlyList<int> PrivateOffsets{get;}
    internal IReadOnlyList<int> PrivateByteLengths{get;}
    internal int NumericPodOffset{get;}
    internal int BucketDataOffset{get;}
    internal int FixupCountOffset{get;}
    internal int FixupCount{get;}
    internal int UsedByteLength{get;}
    internal int SceneByteLength{get;}

    internal static NativeFreshMobySceneStorageLayout Create(IReadOnlyList<int> privateByteLengths,int dynamicCapacity,int fixupCount)
    {
        ArgumentNullException.ThrowIfNull(privateByteLengths);
        if(dynamicCapacity<0||dynamicCapacity>4096||fixupCount<0||fixupCount>4096)
            throw new InvalidDataException("Fresh storage exceeds bounded placed/dynamic/fixup counts.");
        int[] lengths=Snapshot(privateByteLengths,256,"private lengths");long total=0;
        if(fixupCount<lengths.Length)throw new InvalidDataException("Fresh storage requires a private-pointer fixup for every placed row.");
        foreach(int length in lengths)
        {
            if(length<4||length%4!=0||length>0x100000||(total+=length)>0x100000)
                throw new InvalidDataException("Fresh storage exact private blocks exceed the aligned one-MiB budget.");
        }
        return new(lengths,dynamicCapacity,fixupCount);
    }

    /// <summary>
    /// Assign real template ownership using the existing transactional allocator.
    /// All address domains/templates come from the caller's retained producers;
    /// this method never manufactures an Original domain or certificate.
    /// Supplied family order is explicit and survives lexical ID differences.
    /// </summary>
    internal NativeMobyDependencyInstantiationPlan Allocate(NativeAddressSpace finalScene,
        IReadOnlyList<NativeMobyDependencyInstantiationRequest> orderedRequests,IReadOnlyList<NativeAddressSpace> finalDomains,
        NativeAddressSpace runtimeExecutableDomain)
    {
        ArgumentNullException.ThrowIfNull(finalScene);ArgumentNullException.ThrowIfNull(runtimeExecutableDomain);
        var requests=Snapshot(orderedRequests,256,"family requests");var domains=Snapshot(finalDomains,1024,"final domains");
        if(PlacedCount==0||finalScene.Kind!=NativeAddressKind.Scene||finalScene.Revision!=NativeAddressRevision.Final||
            finalScene.ByteLength!=SceneByteLength||requests.Any(r=>r==null||r.Template==null||string.IsNullOrWhiteSpace(r.Id))||
            requests.Select(r=>r.Id).Distinct(StringComparer.Ordinal).Count()!=requests.Length)
            throw new InvalidDataException("Fresh allocation needs its exact nonempty final Scene and unique ordered family identities.");
        int index=0;
        foreach(var request in requests)
        {
            if(request.Template.Rows.Count>PlacedCount-index)
                throw new InvalidDataException("Fresh family rows exceed their exact layout.");
            foreach(var row in request.Template.Rows)
                if(row.Properties.Extent.ByteLength!=PrivateByteLengths[index++])
                    throw new InvalidDataException("Fresh family private lengths differ from their exact layout order.");
        }
        if(index!=PlacedCount)throw new InvalidDataException("Fresh family row count differs from its complete layout.");
        var allocation=NativeMobyDependencyInstantiationPlan.Create(new(finalScene,RowTableOffset,PlacedCount*0x58),
            new(finalScene,PropertiesHeaderOffset+4,PrivateByteLength),RowTableOffset,0,domains,runtimeExecutableDomain);
        foreach(var request in requests)allocation=allocation.Allocate([request]);
        RequireAllocation(allocation);
        return allocation;
    }

    internal NativeFreshMobySceneStoragePlan Compile(ReadOnlySpan<byte> prefix,IReadOnlyList<NativeFreshMobySceneStorageRow> rows,
        IReadOnlyList<NativeFreshMobyScenePointerReference> references)=>CompileCore(prefix,rows,references,null);

    /// <summary>
    /// Materialize the retained allocator's private owners from complete checked
    /// source reads. Model/behavior installation and destination approval remain
    /// with the actual family composer, not this private-storage byte plan.
    /// </summary>
    internal NativeFreshMobySceneStoragePlan CompileAllocated(ReadOnlySpan<byte> prefix,NativeMobyDependencyInstantiationPlan allocation,
        IReadOnlyList<NativeFreshMobyScenePointerReference> references,Func<NativeByteExtent,byte[]> readSource)
    {
        ArgumentNullException.ThrowIfNull(readSource);RequireAllocation(allocation);
        // Refuse unbounded/wrong shape before reading any source extent.
        if(prefix.Length!=0xCC)throw new InvalidDataException("Fresh storage requires its complete pre-Moby prefix.");
        var refs=Snapshot(references,4096,"scene pointers");
        if(refs.Length!=FixupCount)throw new InvalidDataException("Fresh scene pointer count differs from its complete layout.");
        foreach(var reference in refs)
        {
            if(reference==null)throw new InvalidDataException("Fresh scene pointer is missing.");
            if(reference.FieldRowIndex>=0&&reference.FieldRowIndex<PlacedCount&&reference.FieldOwner==NativeFreshMobySceneOwnerKind.Private&&
                allocation.Rows[reference.FieldRowIndex].Source.OwnerFields.Any(f=>f.PropertyByteOffset==reference.FieldOffset))
                throw new InvalidDataException("A private field cannot be both an owner index and a scene pointer.");
        }
        List<NativeFreshMobySceneStorageRow> rows=[];
        foreach(var row in allocation.Rows)
        {
            byte[] record=Read(row.Source.Row),properties=Read(row.Source.Properties);
            rows.Add(new(row.OwnerId,record,row.RemapPrivateOwnerIndices(properties)));
        }
        return CompileCore(prefix,rows,refs,allocation);

        byte[] Read(NativeMobyDependencyRead source)
        {
            byte[] bytes=readSource(source.Extent)??throw new InvalidDataException("Fresh private source returned no bytes.");
            if(bytes.LongLength!=source.Extent.ByteLength||Convert.ToHexStringLower(SHA256.HashData(bytes))!=source.ExpectedSha256)
                throw new InvalidDataException("Fresh private complete source preimage changed.");
            return bytes.ToArray();
        }
    }

    private void RequireAllocation(NativeMobyDependencyInstantiationPlan allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        var rows=allocation.Rows;
        if(PlacedCount==0||rows.Count!=PlacedCount||allocation.FinalRowCount!=PlacedCount||allocation.PropertyBytesAllocated!=PrivateByteLength)
            throw new InvalidDataException("Fresh allocation does not own the complete placed/private storage.");
        var scene=rows[0].Row.Space;
        if(scene.Kind!=NativeAddressKind.Scene||scene.Revision!=NativeAddressRevision.Final||scene.ByteLength!=SceneByteLength)
            throw new InvalidDataException("Fresh allocation has another final Scene domain geometry.");
        for(int i=0;i<rows.Count;i++)
            if(rows[i].Index!=i||!ReferenceEquals(rows[i].Row.Space,scene)||!ReferenceEquals(rows[i].Properties.Space,scene)||
                rows[i].Row.Offset!=RowTableOffset+i*0x58||rows[i].Row.ByteLength!=0x58||
                rows[i].Properties.Offset!=PrivateOffsets[i]||rows[i].Properties.ByteLength!=PrivateByteLengths[i]||
                rows[i].Source.Properties.Extent.ByteLength!=PrivateByteLengths[i])
                throw new InvalidDataException("Fresh allocation row/private ordinal, extent or actual domain differs from its layout.");
    }

    private NativeFreshMobySceneStoragePlan CompileCore(ReadOnlySpan<byte> prefix,IReadOnlyList<NativeFreshMobySceneStorageRow> suppliedRows,
        IReadOnlyList<NativeFreshMobyScenePointerReference> suppliedReferences,NativeMobyDependencyInstantiationPlan? allocation)
    {
        if(prefix.Length!=0xCC)throw new InvalidDataException("Fresh storage requires its complete pre-Moby prefix.");
        var rows=Snapshot(suppliedRows,256,"placed rows");var references=Snapshot(suppliedReferences,4096,"scene pointers");
        if(rows.Length!=PlacedCount||references.Length!=FixupCount||rows.Any(r=>r==null)||
            rows.Select(r=>r.Id).Distinct(StringComparer.Ordinal).Count()!=rows.Length||
            rows.Where((r,i)=>r.PrivateByteLength!=PrivateByteLengths[i]).Any())
            throw new InvalidDataException("Fresh storage row identity, exact private lengths or pointer count changed.");
        byte[][] records=rows.Select(r=>r.CopyRow()).ToArray(),properties=rows.Select(r=>r.CopyProperties()).ToArray();
        List<NativeFreshMobyScenePointerWrite> pointers=[];HashSet<int> fields=[];bool[] rowPointers=new bool[PlacedCount];
        foreach(var reference in references)
        {
            if(reference==null)throw new InvalidDataException("Fresh scene pointer is missing.");
            int field=Address(reference.FieldRowIndex,reference.FieldOwner,reference.FieldOffset);
            int target=Address(reference.TargetRowIndex,reference.TargetOwner,reference.TargetOffset);
            if(!fields.Add(field))throw new InvalidDataException("Fresh scene pointer fields overlap or repeat.");
            byte[] bytes=reference.FieldOwner==NativeFreshMobySceneOwnerKind.Row?records[reference.FieldRowIndex]:properties[reference.FieldRowIndex];
            uint before=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(reference.FieldOffset));
            if(before!=reference.ExpectedSourceValue)throw new InvalidDataException("Fresh scene pointer lacks its exact field preimage.");
            if(reference.FieldOwner==NativeFreshMobySceneOwnerKind.Row&&reference.FieldOffset==0)
            {
                if(reference.TargetRowIndex!=reference.FieldRowIndex||reference.TargetOwner!=NativeFreshMobySceneOwnerKind.Private||reference.TargetOffset!=0)
                    throw new InvalidDataException("Every placed row must point to its own complete private block.");
                rowPointers[reference.FieldRowIndex]=true;
            }
            pointers.Add(new(reference,field,target,before,(uint)target));
        }
        if(rowPointers.Any(p=>!p))throw new InvalidDataException("A placed row is missing its explicit private-pointer fixup.");
        byte[] scene=new byte[SceneByteLength];prefix.CopyTo(scene);
        Put(0xCC,PropertiesHeaderOffset-0xCC);Put(0xD0,PlacedCount);
        for(int i=0;i<PlacedCount;i++){records[i].CopyTo(scene,RowTableOffset+i*0x58);properties[i].CopyTo(scene,PrivateOffsets[i]);}
        Put(PropertiesHeaderOffset,PrivateByteLength+4);Put(NumericPodOffset,8);Put(BucketDataOffset-4,0x2004);Put(FixupCountOffset,FixupCount);
        for(int i=0;i<pointers.Count;i++)
        {Put(pointers[i].FieldSceneOffset,pointers[i].TargetSceneOffset);Put(FixupCountOffset+4+i*4,pointers[i].FieldSceneOffset);}
        List<NativeFreshMobySceneStorageOwner> owners=[];
        Owner("retained-prefix",0,0xCC);Owner("placed-component-fields",0xCC,8);
        for(int i=0;i<PlacedCount;i++)Owner("row:"+rows[i].Id,RowTableOffset+i*0x58,0x58);
        Owner("complete-dynamic-rows",DynamicRowOffset,DynamicRowByteLength,true);
        Owner("complete-next-row-lookahead",NextRowReserveOffset,NextRowReserveByteLength,true);
        Owner("complete-dynamic-properties",DynamicPropertiesOffset,DynamicPropertiesByteLength,true);
        Owner("private-component-length",PropertiesHeaderOffset,4);
        for(int i=0;i<PlacedCount;i++)Owner("private:"+rows[i].Id,PrivateOffsets[i],PrivateByteLengths[i]);
        Owner("empty-numeric-pod",NumericPodOffset,8);Owner("bucket-component-length",BucketDataOffset-4,4);
        Owner("complete-two-bucket-banks",BucketDataOffset,0x2000,true);
        Owner("explicit-pointer-fixups",FixupCountOffset,4+FixupCount*4);Owner("sector-padding",UsedByteLength,SceneByteLength-UsedByteLength,true);
        int end=0;
        foreach(var owner in owners)
        {if(owner.Offset!=end)throw new InvalidDataException("Fresh storage lost its complete owner partition.");end+=owner.ByteLength;}
        if(end!=scene.Length)throw new InvalidDataException("Fresh storage lost its complete trailing owner.");
        return new(this,scene,owners,pointers,allocation);

        int Address(int index,NativeFreshMobySceneOwnerKind kind,int offset)
        {
            if(index<0||index>=PlacedCount||!Enum.IsDefined(kind)||offset<0||offset%4!=0)
                throw new InvalidDataException("Fresh scene pointer names an invalid aligned private/row owner.");
            int length=kind==NativeFreshMobySceneOwnerKind.Row?0x58:PrivateByteLengths[index];
            if(offset>length-4)throw new InvalidDataException("Fresh scene pointer field/target escapes its complete owner.");
            return (kind==NativeFreshMobySceneOwnerKind.Row?RowTableOffset+index*0x58:PrivateOffsets[index])+offset;
        }
        void Put(int offset,int value)=>BinaryPrimitives.WriteInt32LittleEndian(scene.AsSpan(offset),value);
        void Owner(string id,int offset,int length,bool zero=false)
        {
            if(length==0)return;
            if(length<0||offset<0||length>scene.Length-offset||zero&&scene.AsSpan(offset,length).ContainsAnyExcept((byte)0))
                throw new InvalidDataException("Fresh storage owner exceeds its exact bytes or zero complement.");
            owners.Add(new(id,offset,length,zero));
        }
    }

    private static T[] Snapshot<T>(IReadOnlyList<T> values,int maximum,string name)
    {
        ArgumentNullException.ThrowIfNull(values);int count=values.Count;
        if(count<0||count>maximum)throw new InvalidDataException("Fresh storage "+name+" exceed their bounded count.");
        T[] snapshot=new T[count];for(int i=0;i<count;i++)snapshot[i]=values[i];return snapshot;
    }
}

/// <summary>Exact private-storage bytes only; actual family/source/runtime approvals remain external.</summary>
internal sealed class NativeFreshMobySceneStoragePlan
{
    private readonly byte[] _scene;
    internal NativeFreshMobySceneStoragePlan(NativeFreshMobySceneStorageLayout layout,ReadOnlySpan<byte> scene,
        IReadOnlyList<NativeFreshMobySceneStorageOwner> owners,IReadOnlyList<NativeFreshMobyScenePointerWrite> pointers,
        NativeMobyDependencyInstantiationPlan? allocation)
    {Layout=layout;_scene=scene.ToArray();Owners=Array.AsReadOnly(owners.ToArray());PointerWrites=Array.AsReadOnly(pointers.ToArray());Allocation=allocation;}
    internal NativeFreshMobySceneStorageLayout Layout{get;}
    internal NativeMobyDependencyInstantiationPlan? Allocation{get;}
    internal IReadOnlyList<NativeFreshMobySceneStorageOwner> Owners{get;}
    internal IReadOnlyList<NativeFreshMobyScenePointerWrite> PointerWrites{get;}
    internal bool SourceCertified=>false;
    internal bool RuntimeVerified=>false;
    internal bool PublicationApproved=>false;
    internal byte[] CopyScene()=>_scene.ToArray();
    internal void RequireExactScene(ReadOnlySpan<byte> scene)
    {if(!scene.SequenceEqual(_scene))throw new InvalidDataException("Complete fresh storage scene or complement changed.");}
}

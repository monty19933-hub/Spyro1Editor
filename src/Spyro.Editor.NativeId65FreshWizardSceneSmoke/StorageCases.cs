using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

internal static class StorageCases
{
    private static int checks, refusals;
    private static NativeFreshMobyScenePointerReference RowPointer(int index,uint before=0x100) =>
        new(index,NativeFreshMobySceneOwnerKind.Row,0,index,NativeFreshMobySceneOwnerKind.Private,0,before);
    private static NativeFreshMobyScenePointerReference WizardPointer =>
        new(0,NativeFreshMobySceneOwnerKind.Private,0,0,NativeFreshMobySceneOwnerKind.Private,0x28,0xC378);

    internal static void RequireLegacy(byte[] p0,byte[] row,byte[] props,byte[] legacy)
    {
        byte[] placed=row.ToArray(),route=props.ToArray();
        Put(placed,12,8704);Put(placed,16,8960);Put(placed,20,8704);
        Put(route,0x30,8704);Put(route,0x34,8960);Put(route,0x38,8991);
        Put(route,0x40,9216);Put(route,0x44,8960);Put(route,0x48,8991);
        var shape=NativeFreshMobySceneStorageLayout.Create([0x50],32,2);
        var plan=shape.Compile(p0.AsSpan(0,0xCC),[new("wizard",placed,route)],[RowPointer(0,0xC350),WizardPointer]);
        Check(plan.CopyScene().SequenceEqual(legacy),"Storage kernel preserves every old P1/N32 scene byte, including source prefix.");
        RequireGeometry(shape,1,0x12C,0xC2C,0xC84,0xF84,0xF88,0xFD8,0xFE4,0x2FE4,0x2FF0,0x3000);
        RequirePartition(plan);
    }

    internal static void Run(byte[] p0,byte[] row,byte[] props)
    {
        var wizard=new NativeFreshMobySceneStorageRow("wizard",row,props);
        byte[] passiveRow=new byte[0x58],passiveProps=[1,2,3,4,5,6,7,8];
        Put(passiveRow,0,0x100);BinaryPrimitives.WriteUInt16LittleEndian(passiveRow.AsSpan(0x36),0x1F5);
        var grass=new NativeFreshMobySceneStorageRow("grass",passiveRow,passiveProps);
        var shape=NativeFreshMobySceneStorageLayout.Create([0x50,8],32,3);
        var refs=new[]{RowPointer(0,0xC350),WizardPointer,RowPointer(1)};
        var plan=shape.Compile(p0.AsSpan(0,0xCC),[wizard,grass],refs);
        RequireGeometry(shape,2,0x184,0xC84,0xCDC,0xFDC,0xFE0,0x1038,0x1044,0x3044,0x3054,0x3800);
        byte[] b=plan.CopyScene();
        Check(I32(b,0xCC)==0xF10&&I32(b,0xD0)==2&&I32(b,0xFDC)==0x5C&&
            I32(b,0xD4)==0xFE0&&I32(b,0xFE0)==0x1008&&I32(b,0x12C)==0x1030,
            "P2 component lengths and Wizard/Grass private pointers use growth, not N32 storage consumption.");
        Check(I32(b,0x3044)==3&&new[]{I32(b,0x3048),I32(b,0x304C),I32(b,0x3050)}.SequenceEqual(new[]{0xD4,0xFE0,0x12C}),
            "Explicit fixup order is retained, including private-before-second-row ordering.");
        Check(b.AsSpan(0x1030,8).SequenceEqual(passiveProps)&&b.AsSpan(0x1044,0x2000).ContainsAnyExcept((byte)0)==false,
            "Complete passive private block and complete zero bucket banks.");
        Check(!plan.SourceCertified&&!plan.RuntimeVerified&&!plan.PublicationApproved&&plan.Allocation is null,
            "Neutral byte mechanics do not mint original, destination, runtime or output authority.");
        RequirePartition(plan);
        refs[0]=refs[0] with{ExpectedSourceValue=0};passiveRow[1]^=1;passiveProps[0]^=1;
        Check(plan.CopyScene().SequenceEqual(b)&&grass.CopyRow()[1]!=passiveRow[1]&&grass.CopyProperties()[0]!=passiveProps[0],
            "Caller row/private/reference buffers do not alias retained storage.");
        byte[] copy=plan.CopyScene();copy[0]^=1;Reject(()=>plan.RequireExactScene(copy));
        Check(plan.CopyScene().SequenceEqual(b),"Readback failure is atomic.");
        foreach(var owner in plan.Owners)
        {copy=plan.CopyScene();copy[owner.Offset]^=1;Reject(()=>plan.RequireExactScene(copy));}

        var many=NativeFreshMobySceneStorageLayout.Create([8,8,8,8],32,4);
        var rows=Enumerable.Range(0,4).Select(i=>new NativeFreshMobySceneStorageRow("passive-"+i,grass.CopyRow(),grass.CopyProperties())).ToArray();
        var multiple=many.Compile(p0.AsSpan(0,0xCC),rows,Enumerable.Range(0,4).Select(i=>RowPointer(i)).ToArray());
        Check(many.PlacedCount==4&&many.DynamicRowOffset==0x234&&many.DynamicPropertiesByteLength==0x300&&
            many.PrivateOffsets.SequenceEqual(new[]{0x1090,0x1098,0x10A0,0x10A8})&&many.SceneByteLength==0x3800,
            "Multiple passive copies have disjoint exact private extents without consuming N32.");
        for(int i=0;i<4;i++)Check(I32(multiple.CopyScene(),0xD4+i*0x58)==many.PrivateOffsets[i],"Each repeated donor gets its own scene-relative pointer.");
        RequirePartition(multiple);
        foreach(int n in new[]{0,1,16,32,64})
        {
            var g=NativeFreshMobySceneStorageLayout.Create([4,12,20],n,3);
            Check(g.DynamicRowByteLength==n*0x58&&g.DynamicPropertiesByteLength==n*0x18&&g.NextRowReserveByteLength==0x58&&
                g.PropertiesHeaderOffset-g.DynamicRowOffset==n*0x70+0x58,"Arbitrary exact aligned blocks retain independent dynamic components and full lookahead.");
        }
        var empty=NativeFreshMobySceneStorageLayout.Create([],0,0).Compile(p0.AsSpan(0,0xCC),[],[]);
        Check(I32(empty.CopyScene(),0xD0)==0&&empty.Layout.NextRowReserveByteLength==0x58,"Zero placed rows are byte mechanics, not an import approval.");
        RequirePartition(empty);
        GeometryRefusals();
        FixupRefusals(p0,wizard,grass);
        AllocationCases(p0);
        Console.WriteLine($"FRESH_MOBY_SCENE_STORAGE_FREE_OK checks={checks} refusals={refusals} outputsWritten=0");
    }

    private static void GeometryRefusals()
    {
        foreach(int n in new[]{-1,int.MaxValue})Reject(()=>NativeFreshMobySceneStorageLayout.Create([8],n,1));
        foreach(int length in new[]{0,-4,1,6,int.MaxValue,0x100004})Reject(()=>NativeFreshMobySceneStorageLayout.Create([length],32,1));
        foreach(int count in new[]{-1,0,4097,int.MaxValue})Reject(()=>NativeFreshMobySceneStorageLayout.Create([8],32,count));
        Reject(()=>NativeFreshMobySceneStorageLayout.Create(Enumerable.Repeat(8,257).ToArray(),32,257));
        Reject(()=>NativeFreshMobySceneStorageLayout.Create([0x100000,4],32,2));
        Reject(()=>new NativeFreshMobySceneStorageRow("bad",new byte[0x57],new byte[8]));
        Reject(()=>new NativeFreshMobySceneStorageRow("bad",new byte[0x58],new byte[6]));
        Reject(()=>new NativeFreshMobySceneStorageRow(" ",new byte[0x58],new byte[8]));
        var input=new[]{8};var shape=NativeFreshMobySceneStorageLayout.Create(input,32,1);input[0]=80;
        Check(shape.PrivateByteLengths.SequenceEqual(new[]{8}),"Geometry takes immutable bounded lengths before allocation.");
    }

    private static void FixupRefusals(byte[] p0,NativeFreshMobySceneStorageRow wizard,NativeFreshMobySceneStorageRow grass)
    {
        var shape=NativeFreshMobySceneStorageLayout.Create([0x50,8],32,3);
        var valid=new[]{RowPointer(0,0xC350),WizardPointer,RowPointer(1)};
        void Bad(NativeFreshMobyScenePointerReference value,int slot=1)
        {var refs=valid.ToArray();refs[slot]=value;Reject(()=>shape.Compile(p0.AsSpan(0,0xCC),[wizard,grass],refs));}
        Bad(valid[0]);Bad(WizardPointer with{FieldOffset=1});Bad(WizardPointer with{FieldOffset=0x50});
        Bad(WizardPointer with{FieldOffset=int.MaxValue});Bad(WizardPointer with{FieldRowIndex=-1});
        Bad(WizardPointer with{FieldRowIndex=2});Bad(WizardPointer with{FieldOwner=(NativeFreshMobySceneOwnerKind)99});
        Bad(WizardPointer with{TargetRowIndex=2});Bad(WizardPointer with{TargetOffset=0x50});
        Bad(WizardPointer with{TargetOffset=1});Bad(WizardPointer with{TargetOffset=int.MaxValue});
        Bad(WizardPointer with{TargetOwner=(NativeFreshMobySceneOwnerKind)99});Bad(WizardPointer with{ExpectedSourceValue=0});
        Bad(valid[0] with{TargetRowIndex=1},0);Bad(valid[0] with{FieldOffset=4},0);
        Reject(()=>shape.Compile(p0.AsSpan(0,0xCB),[wizard,grass],valid));
        Reject(()=>shape.Compile(p0.AsSpan(0,0xCC),[wizard,grass],valid[..2]));
        Reject(()=>shape.Compile(p0.AsSpan(0,0xCC),[wizard],valid));
        Reject(()=>shape.Compile(p0.AsSpan(0,0xCC),[wizard,wizard],valid));
        Reject(()=>shape.Compile(p0.AsSpan(0,0xCC),[wizard,new("wizard",grass.CopyRow(),grass.CopyProperties())],valid));
        var reordered=shape.Compile(p0.AsSpan(0,0xCC),[wizard,grass],valid.Reverse().ToArray());
        Check(I32(reordered.CopyScene(),shape.FixupCountOffset+4)==0x12C,"Pointer list ordering is explicit, never accidental sorting.");
        var cross=shape.Compile(p0.AsSpan(0,0xCC),[wizard,grass],[valid[0],WizardPointer with{
            TargetRowIndex=1,TargetOwner=NativeFreshMobySceneOwnerKind.Row,TargetOffset=0x54},valid[2]]);
        Check(I32(cross.CopyScene(),shape.PrivateOffsets[0])==0x180,"Explicit references can name another complete row's final aligned field; semantics remain outside the kernel.");
    }

    private static void AllocationCases(byte[] p0)
    {
        byte[] bytes=new byte[0x300];Put(bytes,0x20,0x100);Put(bytes,0x78,0x110);Put(bytes,0x110,7);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x20+0x36),0x135);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x78+0x36),0xD);bytes[0x200]=73;
        var original=new NativeAddressSpace(NativeAddressKind.Scene,"neutral-storage-original",NativeAddressRevision.Original,bytes.Length);
        var exe=new NativeAddressSpace(NativeAddressKind.ExecutableFile,"neutral-storage-exe",NativeAddressRevision.Original,4);
        var models=new NativeAddressSpace(NativeAddressKind.LevelData,"neutral-storage-models",NativeAddressRevision.Final,4);
        byte[] Read(NativeByteExtent e)=>ReferenceEquals(e.Space,original)?bytes.AsSpan((int)e.Offset,(int)e.ByteLength).ToArray():throw new InvalidDataException("Unbound neutral source.");
        NativeMobyDependencyRead R(int at,int length,string id)=>new(id,new(original,at,length),Hash(bytes.AsSpan(at,length)));
        var shared=NativeMobyDependencyClosureCompiler.Compile([
            new("neutral-model",NativeMobyDependencyKind.ActorPackage,NativeMobyDependencyResolution.ImportFromSource,NativeMobyDependencySharing.Immutable,
                Hash(bytes),[],[R(0x200,4,"model")],c=>new(c.Read("model"),[new("model",new(models,0,4))],[]))],["neutral-model"],Read);
        var template=NativeMobyDependencyInstanceTemplate.Capture("neutral-storage-private-v1",[
            new("actor",7,0x135,R(0x20,0x58,"row-a"),R(0x100,16,"props-a"),0,[],true),
            new("child",8,0xD,R(0x78,0x58,"row-b"),R(0x110,16,"props-b"),0,[new(0,0)],true)],[],shared,Read);
        var shape=NativeFreshMobySceneStorageLayout.Create([16,16,16,16],32,4);
        var scene=new NativeAddressSpace(NativeAddressKind.Scene,"neutral-storage-final",NativeAddressRevision.Final,shape.SceneByteLength);
        NativeMobyDependencyInstantiationRequest[] requests=[new("z-existing",template),new("a-appended",template)];
        var allocation=shape.Allocate(scene,requests,[scene,models],exe);
        Check(allocation.Families.Select(f=>f.Id).SequenceEqual(new[]{"z-existing","a-appended"})&&allocation.Rows.Select(r=>r.Index).SequenceEqual(new[]{0,1,2,3}),
            "Explicit family sequence preserves existing ordinals even when appended IDs sort earlier.");
        Check(allocation.Rows.Select(r=>r.Properties.Offset).SequenceEqual(shape.PrivateOffsets.Select(i=>(long)i))&&allocation.SharedNodeCount==1,
            "Existing instantiation assigns exact private owners and shares only the actual shared binding.");
        var pointers=Enumerable.Range(0,4).Select(i=>RowPointer(i,i%2==0?0x100u:0x110u)).ToArray();
        var plan=shape.CompileAllocated(p0.AsSpan(0,0xCC),allocation,pointers,Read);
        Check(ReferenceEquals(plan.Allocation,allocation)&&I32(plan.CopyScene(),shape.PrivateOffsets[1])==0&&I32(plan.CopyScene(),shape.PrivateOffsets[3])==2&&I32(bytes,0x110)==7,
            "Actual allocation owns private owner-index remapping without mutating donor aliases.");
        RequirePartition(plan);
        Reject(()=>shape.Allocate(scene,[requests[0],requests[0]],[scene,models],exe));
        Reject(()=>shape.Allocate(scene,requests,[scene],exe));
        Reject(()=>shape.Allocate(new(NativeAddressKind.Scene,scene.OwnerIdentity,NativeAddressRevision.Final,scene.ByteLength),requests,[scene,models],exe));
        Reject(()=>shape.Allocate(new(NativeAddressKind.Scene,"foreign-size",NativeAddressRevision.Final,0x3000),requests,[scene,models],exe));
        Reject(()=>NativeFreshMobySceneStorageLayout.Create([8,24,16,16],32,4).CompileAllocated(p0.AsSpan(0,0xCC),allocation,pointers,Read));
        Reject(()=>NativeFreshMobySceneStorageLayout.Create([16,16,16,16],32,5).CompileAllocated(p0.AsSpan(0,0xCC),allocation,
            pointers.Append(new(1,NativeFreshMobySceneOwnerKind.Private,0,0,NativeFreshMobySceneOwnerKind.Row,0,0)).ToArray(),Read));
        byte[] before=plan.CopyScene();bytes[0x100]^=1;
        Reject(()=>shape.CompileAllocated(p0.AsSpan(0,0xCC),allocation,pointers,Read));
        Check(plan.CopyScene().SequenceEqual(before),"Changed complete source private preimage cannot mutate an earlier issued byte plan.");
    }

    private static void RequireGeometry(NativeFreshMobySceneStorageLayout g,int placed,int dynamic,int reserve,int dynamicProps,
        int propsHeader,int props,int numeric,int buckets,int fixups,int used,int length)
    {
        Check(g.PlacedCount==placed&&g.DynamicCapacity==32&&g.RowTableOffset==0xD4&&g.DynamicRowOffset==dynamic&&
            g.DynamicRowByteLength==32*0x58&&g.NextRowReserveOffset==reserve&&g.NextRowReserveByteLength==0x58&&
            g.DynamicPropertiesOffset==dynamicProps&&g.DynamicPropertiesByteLength==32*0x18&&g.PropertiesHeaderOffset==propsHeader&&
            g.PrivateOffsets[0]==props&&g.NumericPodOffset==numeric&&g.BucketDataOffset==buckets&&g.FixupCountOffset==fixups&&
            g.UsedByteLength==used&&g.SceneByteLength==length,"Independent exact growth geometry.");
    }
    private static void RequirePartition(NativeFreshMobySceneStoragePlan plan)
    {
        byte[] bytes=plan.CopyScene();int at=0;
        foreach(var owner in plan.Owners)
        {Check(owner.Offset==at&&owner.ByteLength>0&&owner.ByteLength<=bytes.Length-at,"Storage owner partition is contiguous and bounded.");
            if(owner.IsZero)Check(!bytes.AsSpan(at,owner.ByteLength).ContainsAnyExcept((byte)0),"Declared complete zero owner remains zero.");at+=owner.ByteLength;}
        Check(at==bytes.Length,"Storage owner partition covers all bytes, including alignment tail.");
    }
    private static int I32(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
    private static void Put(Span<byte> b,int at,int value)=>BinaryPrimitives.WriteInt32LittleEndian(b[at..],value);
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Check(bool yes,string why){if(!yes)throw new InvalidDataException(why);checks++;}
    private static void Reject(Action action){try{action();}catch(InvalidDataException){checks++;refusals++;return;}throw new InvalidDataException("Expected storage refusal.");}
}

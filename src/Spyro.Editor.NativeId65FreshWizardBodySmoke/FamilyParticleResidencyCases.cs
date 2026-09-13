using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

internal static class FamilyParticleResidencyCases
{
    internal static void Free(Action<bool,string> check)
    {
        var type=typeof(NativeFreshMobyParticleResidencyBinding);
        check(type.IsSealed&&!type.IsPublic&&type.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
            .All(c=>c.IsPrivate),
            "Key-particle residency has only private constructors, not caller-byte or caller-owner authority.");
        var factories=type.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)
            .Where(m=>m.ReturnType==type).ToArray();
        check(factories.Length==1&&factories[0].Name=="BindLockedChestKey"&&
            factories[0].GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{
                typeof(NativeLockedChestFamilySource),typeof(NativeFreshMobyFamilyCompositionPlan)}),
            "Only an actual extracted source/composition pair can issue the bounded key-particle residency binding.");
        check(type.GetProperties(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
            .All(p=>p.PropertyType!=typeof(bool)),
            "Resident particle bytes add no admission, callback, renderer, frame-lifetime, GPU or runtime capability flags.");
        Reject<ArgumentNullException>(()=>NativeFreshMobyParticleResidencyBinding.BindLockedChestKey(null!,null!),check);

        // Existing neutral codecs, not a synthetic source or destination issuer.
        byte[] record=Record(0x0C),component=Component(record);
        var table=NativeParticleDescriptorCodec.Decode(component);
        var footprint=Footprint(record);
        check(table.Ids.SequenceEqual(new[]{0x0C})&&table.ByteLength==20&&
            NativeParticleDescriptorCodec.RequireRecord(record)==0x0C&&
            footprint.PixelRows.Count==32&&footprint.PixelRows.All(r=>r.ByteLength==16)&&footprint.Palette.ByteLength==32,
            "The signed descriptor ID and eight-byte frame own a complete 4bpp rectangle and sixteen-entry CLUT.");
        byte[] other=Record(7);
        check(NativeParticleDescriptorCodec.Decode(Component(other,record)).Ids.SequenceEqual(new[]{7,0x0C}),
            "Particle IDs are decoded from records, not inferred from ordinal or effect callback selectors.");
        Reject<InvalidDataException>(()=>NativeParticleDescriptorCodec.Decode(Component(record,record)),check);
        foreach(short id in new short[]{-1,64,short.MaxValue})
        {byte[] bad=record.ToArray();Half(bad,0,id);Reject<InvalidDataException>(()=>NativeParticleDescriptorCodec.RequireRecord(bad),check);}
        foreach(short width in new short[]{0,4,12,-1})
        {byte[] bad=record.ToArray();Half(bad,2,width);Reject<InvalidDataException>(()=>NativeParticleDescriptorCodec.RequireRecord(bad),check);}
        Reject<InvalidDataException>(()=>NativeParticleDescriptorCodec.RequireRecord(record[..^1]),check);
        Reject<InvalidDataException>(()=>NativeParticleDescriptorCodec.Decode(component[..^1]),check);
        byte[] badCount=component.ToArray();Put(badCount,4,2);
        Reject<InvalidDataException>(()=>NativeParticleDescriptorCodec.Decode(badCount),check);
        foreach(ushort page in new ushort[]{0,7,0x9D,0xFFFF})
        {byte[] bad=record.ToArray();UHalf(bad,10,page);Reject<InvalidDataException>(()=>Footprint(bad),check);}
        byte[] badPalette=record.ToArray();UHalf(badPalette,6,0);
        Reject<InvalidDataException>(()=>Footprint(badPalette),check);

        // Zero is still referenced data. Same original-address overlap is valid;
        // another detached owner cannot reserve it merely because bytes match.
        var zeroVram=new NativeId65VramAssembly(new byte[0x80000]);
        var first=footprint.PixelRows[0];
        zeroVram.CopySourceRange(first.VramRelativeOffset,first.ByteLength);
        zeroVram.CopySourceRange(first.VramRelativeOffset+4,4);
        check(zeroVram.SourceOwnedByteCount==first.ByteLength,
            "Legitimate same-source aliases retain one zero-valued original pixel owner.");
        Reject<InvalidDataException>(()=>zeroVram.CopyDetachedRange("equal-zero-other-owner",first.VramRelativeOffset,new byte[first.ByteLength]),check);
        Console.WriteLine("NATIVE_FRESH_FAMILY_PARTICLE_RESIDENCY_FREE_OK nativeAdmission=false renderer=false");
    }

    // The sound harness already holds these exact five source domains. No new
    // ROM acquisition, inventory, Town compiler or candidate is needed here.
    internal static void Source(NativeLockedChestFamilySource source,NativeFreshMobyFamilyCompositionPlan composition,
        NativeFreshMobyFamilyCompositionPlan equalComposition,Action<bool,string> check)
    {
        byte[] body=composition.CopyLevelData(),scene=composition.CopyScene(),package=source.CopyPackage();
        var descriptorRead=source.ResourceInputs["key.particle-descriptor"].Single();
        var pixelReads=Enumerable.Range(0,32).Select(i=>source.ResourceInputs["key.particle-pixels."+i].Single()).ToArray();
        var clutRead=source.ResourceInputs["key.particle-clut"].Single();
        var reads=new[]{descriptorRead}.Concat(pixelReads).Append(clutRead).ToArray();
        var townDomain=descriptorRead.Extent.Space;
        byte[] town=source.ReadSource(new(townDomain,0,townDomain.ByteLength));
        var sourceLayout=new NativeLevelSoundLayout(town);
        var sourceTable=NativeParticleDescriptorCodec.Decode(town.AsSpan(sourceLayout.ParticleOffset,
            sourceLayout.SoundOffset-sourceLayout.ParticleOffset));
        int sourceOrdinal=sourceTable.Ids.ToList().IndexOf(0x0C);
        byte[] record=source.ReadSource(descriptorRead.Extent);
        var sourceFootprint=Footprint(record);
        check(sourceOrdinal==4&&sourceTable.Ids.Count(id=>id==0x0C)==1&&descriptorRead.Extent.ByteLength==12&&
            descriptorRead.Extent.Offset==sourceLayout.ParticleOffset+8+sourceOrdinal*12&&
            NativeParticleDescriptorCodec.RequireRecord(record)==0x0C&&
            record.SequenceEqual(town.AsSpan((int)descriptorRead.Extent.Offset,12).ToArray()),
            "The extracted requirement is the exact original Town support record0C, not a Peace-equivalence or callback claim.");
        check(sourceFootprint.PixelRows.Count==32&&sourceFootprint.PixelRows.All(r=>r.ByteLength==16)&&
            sourceFootprint.Palette.ByteLength==32,
            "The source descriptor owns all thirty-two pixel rows and its complete thirty-two-byte palette.");
        for(int i=0;i<pixelReads.Length;i++)
            check(ReferenceEquals(pixelReads[i].Extent.Space,townDomain)&&pixelReads[i].Extent.ByteLength==sourceFootprint.PixelRows[i].ByteLength&&
                pixelReads[i].Extent.Offset==sourceLayout.VramOffset+sourceFootprint.PixelRows[i].VramRelativeOffset,
                "Each named key pixel read matches the descriptor-derived original physical upload row.");
        check(ReferenceEquals(clutRead.Extent.Space,townDomain)&&clutRead.Extent.ByteLength==sourceFootprint.Palette.ByteLength&&
            clutRead.Extent.Offset==sourceLayout.VramOffset+sourceFootprint.Palette.VramRelativeOffset,
            "The named key CLUT read matches its descriptor-derived original physical palette owner.");

        var layout=new NativeLevelSoundLayout(body);
        var table=NativeParticleDescriptorCodec.Decode(body.AsSpan(layout.ParticleOffset,layout.SoundOffset-layout.ParticleOffset));
        int ordinal=table.Ids.ToList().IndexOf(0x0C);
        bool mixed=composition.BaseBodyAncestor.MixedResourceAncestor is not null;
        check(table.Ids.SequenceEqual(new[]{0x15,0x18,2,0x21,0x0C,0x0B,0x0A,9,1,0,7})&&ordinal==4&&
            layout.ParticleOffset-layout.DataOffset==(mixed?0x2C0:0x150)&&layout.SoundOffset-layout.ParticleOffset==0x8C,
            "Both actual data streams keep the unique retained0C record and appended Wizard7 after material prefix growth.");
        NativeByteExtent descriptor=new(composition.FinalBodyDomain,layout.ParticleOffset+8+ordinal*12,12);
        byte[] actualRecord=Slice(body,descriptor);
        var footprint=Footprint(actualRecord);
        var pixels=footprint.PixelRows.Select(r=>new NativeByteExtent(composition.FinalBodyDomain,
            layout.VramOffset+r.VramRelativeOffset,r.ByteLength)).ToArray();
        NativeByteExtent clut=new(composition.FinalBodyDomain,layout.VramOffset+footprint.Palette.VramRelativeOffset,footprint.Palette.ByteLength);
        var expected=new[]{descriptor}.Concat(pixels).Append(clut).ToArray();
        check(descriptor.Offset-layout.DataOffset==(mixed?0x2F8:0x188)&&actualRecord.SequenceEqual(record)&&
            footprint.PixelRows.SequenceEqual(sourceFootprint.PixelRows)&&footprint.Palette==sourceFootprint.Palette,
            "Actual final record and GPU footprint retain the exact source bytes at grammar-derived body offsets.");
        var normal=composition.BaseBodyAncestor.HostAncestor.SceneAncestor.CompilationAncestor.FreshResourceAncestor.ResourceChainAncestor.Resources;
        check(normal.CopyParticleFrame(0x0C,0).SequenceEqual(actualRecord.AsSpan(4,8).ToArray()),
            "The retained key support record exposes only the existing owned eight-byte frame0.");
        Reject<InvalidDataException>(()=>normal.CopyParticleFrame(0x0C,1),check);
        Reject<InvalidDataException>(()=>normal.CopyParticleFrame(0x0C,-1),check);

        var plan=NativeFreshMobyParticleResidencyBinding.BindLockedChestKey(source,composition);
        plan.RequireAncestry(source,composition);plan.RequireRetainedOwners();plan.RequireExactLevelData(body);
        check(ReferenceEquals(plan.SourceAncestor,source)&&ReferenceEquals(plan.CompositionAncestor,composition)&&plan.DescriptorId==0x0C&&
            Same(plan.Descriptor,descriptor)&&plan.PixelRows.Count==pixels.Length&&
            plan.PixelRows.Zip(pixels).All(p=>Same(p.First,p.Second))&&Same(plan.Clut,clut),
            "The closed binding retains its actual ancestors and independently decoded descriptor/pixel/CLUT final owners.");
        check(plan.Graph.Installs.Count==0&&plan.Graph.References.Count==0&&
            plan.Graph.Nodes.All(n=>n.Kind is NativeMobyDependencyKind.Texture or NativeMobyDependencyKind.Bundle)&&
            plan.Graph.Nodes.All(n=>n.Resolution is NativeMobyDependencyResolution.Aggregate or NativeMobyDependencyResolution.DestinationResident),
            "Particle residency introduces no packages, native code, global routines, allocations, rewrites or installed dispatch references.");
        var preserved=plan.Graph.Nodes.SelectMany(n=>n.Preserved.Select(p=>(Node:n,Preservation:p))).ToArray();
        check(preserved.Length==34&&preserved.Sum(p=>p.Preservation.Final.ByteLength)==556&&
            expected.All(e=>preserved.Count(p=>Same(p.Preservation.Final,e))==1),
            "Exactly one complete descriptor, thirty-two pixel rows and one CLUT receive the thirty-four narrow preservations.");
        for(int i=0;i<expected.Length;i++)
        {
            var item=preserved.Single(p=>Same(p.Preservation.Final,expected[i]));
            var read=item.Node.Reads.Single(r=>r.Id==item.Preservation.ReadId);
            check(Same(read.Extent,reads[i].Extent)&&read.ExpectedSha256==reads[i].ExpectedSha256&&item.Preservation.ReadOffset==0&&
                Slice(body,expected[i]).SequenceEqual(source.ReadSource(reads[i].Extent)),
                "Every final owner is checked against its full exact retained source read, including interior and final bytes.");
        }
        foreach(var node in plan.Graph.Nodes)
        {
            ReadOnly(node.Reads,check);ReadOnly(node.Dependencies,check);ReadOnly(node.Preserved,check);
            foreach(var read in node.Reads)
                check(read.Extent.Space.Revision==NativeAddressRevision.Original&&ReferenceEquals(read.Extent.Space,townDomain)&&
                    Hash(source.ReadSource(read.Extent))==read.ExpectedSha256,
                    "Graph reads remain actual Original Town support inputs, never generated final bytes or broad executable claims.");
        }

        // Check NEW write allocations only. Existing player/particle/fallback
        // same-source aliases are legitimate and need not be pairwise disjoint.
        var resource=composition.BaseBodyAncestor.HostAncestor.FallbackAncestor.ResourceAncestor;
        var newWizardWrites=resource.TexturePlacements.SelectMany(p=>Enumerable.Range(0,p.Rows)
            .Select(y=>(Offset:(p.Y+y)*1024+p.X,Length:p.RowBytes))).ToArray();
        var keyGpu=footprint.PixelRows.Append(footprint.Palette).ToArray();
        check(newWizardWrites.Length>0&&newWizardWrites.All(w=>keyGpu.All(k=>!Overlap(w.Offset,w.Length,k.VramRelativeOffset,k.ByteLength))),
            "The key's retained GPU bytes do not overlap any newly allocated Wizard texture or palette writes.");
        if(composition.BaseBodyAncestor.MixedResourceAncestor is { } material)
        {
            var newMaterialWrites=material.Allocation.Allocations.SelectMany(p=>Enumerable.Range(0,p.RowCount)
                .Select(y=>(Offset:p.TargetOffset+y*1024,Length:p.RowByteLength))).ToArray();
            check(newMaterialWrites.Length>0&&newMaterialWrites.All(w=>keyGpu.All(k=>!Overlap(w.Offset,w.Length,k.VramRelativeOffset,k.ByteLength)))&&
                keyGpu.All(k=>Enumerable.Range(k.VramRelativeOffset,k.ByteLength).All(at=>!material.Allocation.IsAllocated(at))),
                "Actual newly allocated material pixels and palettes leave every retained key GPU byte untouched.");
        }
        int wizardOrdinal=table.Ids.ToList().IndexOf(7);
        byte[] resourceData=resource.CopyDataComponents();
        var resourceTable=NativeParticleDescriptorCodec.Decode(resourceData.AsSpan(0x150,0x8C));
        check(wizardOrdinal==10&&Slice(body,new(composition.FinalBodyDomain,layout.ParticleOffset+8+wizardOrdinal*12,12))
            .SequenceEqual(resourceData.AsSpan(0x150+8+resourceTable.Ids.ToList().IndexOf(7)*12,12).ToArray()),
            "Binding the retained key record leaves the distinct already-appended Wizard7 descriptor unchanged.");
        var zeroOffsets=expected.Skip(1).SelectMany(e=>Enumerable.Range((int)e.Offset,(int)e.ByteLength)).Where(at=>body[at]==0).ToArray();
        check(zeroOffsets.Length>0&&zeroOffsets.All(at=>preserved.Any(p=>at>=p.Preservation.Final.Offset&&at<p.Preservation.Final.EndExclusive)),
            "Actual referenced zero-valued pixels and CLUT bytes remain explicit protected owners, not free GPU space.");

        var domains=Domains(source);var reissued=Decode(domains,source.ReadSource);
        check(!ReferenceEquals(source,reissued)&&reissued.CopyPackage().SequenceEqual(package)&&
            !ReferenceEquals(composition,equalComposition)&&composition.PlanSha256==equalComposition.PlanSha256,
            "Ancestry refusals use genuinely equal reissued source and composition objects.");
        Reject<InvalidDataException>(()=>plan.RequireAncestry(reissued,composition),check);
        Reject<InvalidDataException>(()=>plan.RequireAncestry(source,equalComposition),check);
        Reject<ArgumentNullException>(()=>NativeFreshMobyParticleResidencyBinding.BindLockedChestKey(source,null!),check);
        Reject<ArgumentNullException>(()=>NativeFreshMobyParticleResidencyBinding.BindLockedChestKey(null!,composition),check);
        var repeated=NativeFreshMobyParticleResidencyBinding.BindLockedChestKey(source,composition);
        check(repeated.PlanSha256==plan.PlanSha256&&repeated.Graph.Fingerprint==plan.Graph.Fingerprint,
            "Repeated binding is deterministic without changing its original or composed owners.");
        ReadOnly(plan.PixelRows,check);ReadOnly(plan.Graph.Nodes,check);ReadOnly(plan.Graph.Installs,check);ReadOnly(plan.Graph.References,check);
        var corruptOffsets=expected.SelectMany(e=>new[]{(int)e.Offset,(int)(e.Offset+e.ByteLength/2),(int)e.EndExclusive-1})
            .Concat(zeroOffsets.Take(1)).Concat(zeroOffsets.TakeLast(1)).Concat(new[]{
                0,layout.ParticleOffset,layout.ParticleOffset+4,layout.ParticleOffset+8+wizardOrdinal*12,
                layout.SoundOffset,layout.SoundOffset+4+0x1F,layout.BankOffset,layout.BankOffset+0x65800-1,
                layout.VramOffset+0x65800,layout.VramOffset+0x7F000-1,
                (int)Word(body,24)+(int)composition.Allocation.Rows[0].Row.Offset,body.Length-1}).Distinct().ToArray();
        foreach(int at in corruptOffsets)
        {byte[] bad=body.ToArray();bad[at]^=1;Reject<InvalidDataException>(()=>plan.RequireExactLevelData(bad),check);}
        Reject<InvalidDataException>(()=>plan.RequireExactLevelData(body[..^1]),check);
        Reject<InvalidDataException>(()=>plan.RequireExactLevelData([..body,(byte)0]),check);
        foreach(int at in new[]{(int)descriptorRead.Extent.Offset,(int)pixelReads[0].Extent.Offset,(int)clutRead.Extent.Offset})
        {
            byte[] bad=town.ToArray();bad[at]^=1;
            Reject<InvalidDataException>(()=>Decode(domains,e=>ReferenceEquals(e.Space,townDomain)?
                bad.AsSpan((int)e.Offset,(int)e.ByteLength).ToArray():source.ReadSource(e)),check);
        }
        body[0]^=1;scene[0]^=1;package[4]^=1;town[(int)descriptorRead.Extent.Offset]^=1;record[4]^=1;
        plan.RequireRetainedOwners();plan.RequireExactLevelData(composition.CopyLevelData());
        check(Hash(composition.CopyLevelData())==composition.LevelDataSha256&&Hash(composition.CopyScene())==composition.SceneSha256&&
            Hash(source.CopyPackage())==source.PackageRead.ExpectedSha256&&
            Hash(source.ReadSource(descriptorRead.Extent))==descriptorRead.ExpectedSha256&&
            composition.CopyInverseBaseBody().SequenceEqual(composition.BaseBodyAncestor.CopyLevelData()),
            "All copy mutations and refusals preserve source, full body/scene, bank/residue/complement and existing inverse.");
        Console.WriteLine($"NATIVE_FRESH_LOCKED_CHEST_PARTICLE_RESIDENCY_SOURCE_OK mixed={mixed} body={composition.LevelDataSha256} installs=0 nativeAdmission=false renderer=false");
    }

    private static NativeId65TextureFootprint Footprint(ReadOnlySpan<byte> record)
    {
        _=NativeParticleDescriptorCodec.RequireRecord(record);
        ReadOnlySpan<byte> frame=record[4..];
        byte[] quad=new byte[12];frame.CopyTo(quad);
        quad[8]=frame[0];quad[9]=frame[5];quad[10]=frame[4];quad[11]=frame[1];
        return NativeId65PlayerTextureOwners.DecodeFootprint(quad);
    }
    private static byte[] Record(short id)
    {
        byte[] record=new byte[12];Half(record,0,id);Half(record,2,8);
        record[8]=31;record[9]=31;UHalf(record,6,0x7B3A);UHalf(record,10,0x1D);return record;
    }
    private static byte[] Component(params byte[][] records)
    {
        byte[] bytes=new byte[8+records.Length*12];Put(bytes,0,(uint)bytes.Length);Put(bytes,4,(uint)records.Length);
        for(int i=0;i<records.Length;i++)records[i].CopyTo(bytes,8+i*12);return bytes;
    }
    private static NativeAddressSpace[] Domains(NativeLockedChestFamilySource source)=>[
        source.PrivateRows[0].Row.Extent.Space,source.HandlerReads[0].Extent.Space,
        source.ResourceInputs["key.particle-descriptor"].Single().Extent.Space,
        source.ResourceInputs["key.native-handler"].Single().Extent.Space,source.MutableGlobals[0].ExecutableDomain];
    private static NativeLockedChestFamilySource Decode(NativeAddressSpace[] domains,Func<NativeByteExtent,byte[]> reader)=>
        NativeLockedChestFamilySource.DecodeOriginal(domains[0],domains[1],domains[2],domains[3],domains[4],reader);
    private static bool Overlap(int a,int lengthA,int b,int lengthB)=>a<(long)b+lengthB&&b<(long)a+lengthA;
    private static bool Same(NativeByteExtent a,NativeByteExtent b)=>ReferenceEquals(a.Space,b.Space)&&a.Offset==b.Offset&&a.ByteLength==b.ByteLength;
    private static byte[] Slice(byte[] bytes,NativeByteExtent extent)=>bytes.AsSpan((int)extent.Offset,(int)extent.ByteLength).ToArray();
    private static uint Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static void Put(Span<byte> bytes,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..],value);
    private static void Half(Span<byte> bytes,int at,short value)=>BinaryPrimitives.WriteInt16LittleEndian(bytes[at..],value);
    private static void UHalf(Span<byte> bytes,int at,ushort value)=>BinaryPrimitives.WriteUInt16LittleEndian(bytes[at..],value);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void ReadOnly<T>(IReadOnlyList<T> values,Action<bool,string> check)
    {if(values is IList<T> list)Reject<NotSupportedException>(()=>list.Clear(),check);else check(true,"No mutable particle-residency list interface.");}
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Expected particle-residency refusal "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}

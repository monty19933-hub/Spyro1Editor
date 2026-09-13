using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

internal static class FamilySoundResidencyCases
{
    internal static void Free(Action<bool,string> check)
    {
        var type=typeof(NativeFreshMobySoundResidencyBinding);
        check(type.IsSealed&&!type.IsPublic&&type.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
            .All(c=>c.IsPrivate),
            "Fresh sound residency has a closed actual-source/actual-composition issuer, not a caller-byte constructor.");
        var factories=type.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)
            .Where(m=>m.ReturnType==type).ToArray();
        check(factories.Length==1&&factories[0].Name=="BindLockedChest"&&
            factories[0].GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{
                typeof(NativeLockedChestFamilySource),typeof(NativeFreshMobyFamilyCompositionPlan)}),
            "Only the retained source and actual composition can issue this bounded sound-residency binding.");
        check(type.GetProperties(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
            .All(p=>p.PropertyType!=typeof(bool)),
            "Sound-byte residency adds no source/native admission, behavior, playback, runtime or publication capability flags.");
        Reject<ArgumentNullException>(()=>NativeFreshMobySoundResidencyBinding.BindLockedChest(null!,null!),check);

        // Exercise the EXISTING neutral equivalence rule independently. These
        // synthetic codec inputs cannot issue a source or destination binding.
        var source=Bank(0);var relocated=Bank(16);
        check(source.Descriptors[0].SampleOffset!=relocated.Descriptors[0].SampleOffset&&
            NativeGlobalSoundMergeLayout.Equivalent(source.Descriptors[0],relocated.Descriptors[0]),
            "Equivalent sound resources compare all playback metadata and complete ADPCM bytes, not bank-relative offsets.");
        byte[] metadata=relocated.CopyComponent();metadata[0x108+10]^=1;
        var wrongMetadata=NativeSoundBankCodec.Decode(metadata,relocated.CopyBank());
        check(!NativeGlobalSoundMergeLayout.Equivalent(source.Descriptors[0],wrongMetadata.Descriptors[0]),
            "Equal sample bytes with different descriptor metadata are not equivalent sound ownership.");
        foreach(int at in new[]{2,15,30,31})
        {
            byte[] sample=relocated.CopyBank();sample[16+at]^=1;
            var wrongSample=NativeSoundBankCodec.Decode(relocated.CopyComponent(),sample);
            check(!NativeGlobalSoundMergeLayout.Equivalent(source.Descriptors[0],wrongSample.Descriptors[0]),
                "Complete ADPCM equivalence includes interior and final sample bytes.");
        }
        Console.WriteLine("NATIVE_FRESH_FAMILY_SOUND_RESIDENCY_FREE_OK nativeAdmission=false playback=false");
    }

    internal static async Task Source(string original,NativeFreshMobyFamilyCompositionPlan composition,
        NativeFreshMobyFamilyCompositionPlan equalComposition,Action<bool,string> check)
    {
        var source=await CaptureSource(original,composition.BaseBodyAncestor.HostAncestor.SceneAncestor.FamilyAncestor);
        byte[] body=composition.CopyLevelData(),scene=composition.CopyScene(),package=source.CopyPackage();
        var plan=NativeFreshMobySoundResidencyBinding.BindLockedChest(source,composition);
        plan.RequireAncestry(source,composition);plan.RequireRetainedOwners();plan.RequireExactLevelData(body);
        check(ReferenceEquals(plan.SourceAncestor,source)&&ReferenceEquals(plan.CompositionAncestor,composition),
            "Sound residency retains the actual extracted source and complete Fresh composition instances.");
        var layout=new NativeLevelSoundLayout(body);
        bool mixed=composition.BaseBodyAncestor.MixedResourceAncestor is not null;
        check(layout.SoundOffset-layout.DataOffset==(mixed?0x34C:0x1DC)&&layout.Sound.Descriptors.Count==78&&
            layout.Sound.BankByteLength==0x65800&&layout.Sound.Map[0x1F]==76,
            "Both existing Fresh layouts retain the real moved sound component, Wizard selector and complete bank owners.");
        check(plan.Selections.Select(s=>s.Id).SequenceEqual(new[]{"sound.key","sound.reward","sound.chest-local54"})&&
            plan.Selections.Select(s=>s.GlobalSelector).SequenceEqual(new int?[]{0,0x37,null})&&
            plan.Selections.Select(s=>s.SourceDescriptorIndex).SequenceEqual(new[]{17,35,47})&&
            plan.Selections.Select(s=>s.TargetDescriptorIndex).SequenceEqual(new[]{17,35,54}),
            "Global selectors0/37 remain distinct from source-local descriptor47 and resident local descriptor54.");
        var sourceData=source.PrivateRows[0].Row.Extent.Space;
        var donor=new NativeLevelSoundLayout(source.ReadSource(new(sourceData,0,sourceData.ByteLength)));
        foreach(var selection in plan.Selections)
        {
            var target=layout.Sound.Descriptors[selection.TargetDescriptorIndex];
            var incoming=donor.Sound.Descriptors[selection.SourceDescriptorIndex];
            check(ReferenceEquals(selection.Descriptor.Space,composition.FinalBodyDomain)&&
                ReferenceEquals(selection.Sample.Space,composition.FinalBodyDomain)&&
                selection.Descriptor.Offset==layout.SoundOffset+0x108+selection.TargetDescriptorIndex*20&&
                selection.Descriptor.ByteLength==20&&selection.Sample.Offset==layout.BankOffset+target.SampleOffset&&
                selection.Sample.ByteLength==target.Sample.ByteLength,
                "Resident descriptor and complete uploaded sample bind to grammar-derived actual FinalBody extents.");
            check(NativeGlobalSoundMergeLayout.Equivalent(incoming,target)&&
                Slice(body,selection.Descriptor).SequenceEqual(target.CopyDescriptor())&&
                Slice(body,selection.Sample).SequenceEqual(incoming.Sample.CopyBytes()),
                "Every resident selection compares donor controls and complete sample bytes against the actual composed body.");
            if(selection.GlobalSelector is int global)
                check(layout.Sound.Map[global]==selection.TargetDescriptorIndex&&donor.Sound.Map[global]==selection.SourceDescriptorIndex,
                    "A global binding retains the actual mapped source and destination descriptor indices.");
        }
        check(plan.Selections.Select(s=>s.Sample.ByteLength).SequenceEqual(new long[]{0x3C40,0xB60,0x1430}),
            "The three existing samples retain their complete native ADPCM extents.");

        var local=plan.LocalSelector;
        check(ReferenceEquals(local.SourceField.Extent.Space,source.PackageRead.Extent.Space)&&
            local.SourceField.Extent.Offset==source.PackageRead.Extent.Offset+4&&local.SourceField.Extent.ByteLength==4&&
            local.SourceField.ExpectedSha256==Hash(package.AsSpan(4,4))&&
            Word(source.ReadSource(local.SourceField.Extent),0)==0xFFFFFF2F&&
            local.SourceDescriptorIndex==47&&local.TargetDescriptorIndex==54,
            "Local selector is a checked Original FFFFFF2F word and future descriptor54 obligation, not an installed model field.");
        check(!local.GetType().GetProperties(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
            .Any(p=>p.PropertyType==typeof(NativeByteExtent)||p.Name.StartsWith("Final",StringComparison.Ordinal)),
            "An absent AE package cannot acquire a fabricated final selector extent.");
        check(!plan.Graph.Installs.Any()&&!plan.Graph.References.Any()&&
            plan.Graph.Nodes.All(n=>n.Kind is NativeMobyDependencyKind.Sound or NativeMobyDependencyKind.Bundle)&&
            plan.Graph.Nodes.All(n=>n.Resolution is NativeMobyDependencyResolution.Aggregate or NativeMobyDependencyResolution.DestinationResident),
            "This graph binds existing sound bytes only: no packages, code, global routines, generated payload installs or native fixups.");
        var preserved=plan.Graph.Nodes.SelectMany(n=>n.Preserved.Select(p=>(Node:n,Preservation:p))).ToArray();
        var expected=plan.Selections.SelectMany(s=>new[]{s.Descriptor,s.Sample}).Concat(new[]{
            new NativeByteExtent(composition.FinalBodyDomain,layout.SoundOffset+4,1),
            new NativeByteExtent(composition.FinalBodyDomain,layout.SoundOffset+4+0x37,1)}).ToArray();
        check(preserved.Length==8&&expected.All(e=>preserved.Count(p=>Same(p.Preservation.Final,e))==1),
            "Exactly three full descriptors, three full samples and two map bytes receive final sound preservations.");
        foreach(var node in plan.Graph.Nodes)
        {
            ReadOnly(node.Reads,check);ReadOnly(node.Dependencies,check);ReadOnly(node.Preserved,check);
            foreach(var read in node.Reads)
                check(read.Extent.Space.Revision==NativeAddressRevision.Original&&
                    Hash(source.ReadSource(read.Extent))==read.ExpectedSha256,
                    "Every graph input is a verified read from one of the retained Original source domains.");
        }
        foreach(var item in preserved)
        {
            var p=item.Preservation;var read=item.Node.Reads.Single(r=>r.Id==p.ReadId);
            check(ReferenceEquals(p.Final.Space,composition.FinalBodyDomain)&&
                Slice(body,p.Final).SequenceEqual(source.ReadSource(read.Extent).AsSpan(p.ReadOffset,(int)p.Final.ByteLength).ToArray()),
                "Each preservation is backed by its exact retained source bytes, not a matching resource label.");
        }

        // No new source inventory: replay already retained decoder owners to
        // distinguish equal evidence from the exact source object held by plan.
        var domains=Domains(source);var reissued=Decode(domains,source.ReadSource);
        check(!ReferenceEquals(reissued,source)&&reissued.CopyPackage().SequenceEqual(package)&&
            !ReferenceEquals(equalComposition,composition)&&equalComposition.PlanSha256==composition.PlanSha256,
            "Identity refusals use genuinely equal reissued source and composition objects.");
        Reject<InvalidDataException>(()=>plan.RequireAncestry(reissued,composition),check);
        Reject<InvalidDataException>(()=>plan.RequireAncestry(source,equalComposition),check);
        Reject<ArgumentNullException>(()=>NativeFreshMobySoundResidencyBinding.BindLockedChest(source,null!),check);
        Reject<ArgumentNullException>(()=>NativeFreshMobySoundResidencyBinding.BindLockedChest(null!,composition),check);
        var repeated=NativeFreshMobySoundResidencyBinding.BindLockedChest(source,composition);
        check(repeated.PlanSha256==plan.PlanSha256&&repeated.Graph.Fingerprint==plan.Graph.Fingerprint,
            "Repeated binding of the same actual source/composition is deterministic without changing either owner.");
        ReadOnly(plan.Selections,check);ReadOnly(plan.Graph.Nodes,check);ReadOnly(plan.Graph.Installs,check);ReadOnly(plan.Graph.References,check);

        // The issuer accepts no caller body/schema/map overrides. Its exact-byte
        // recheck must refuse both selected owner changes and untouched complement.
        var corruptOffsets=expected.SelectMany(e=>new[]{(int)e.Offset,(int)(e.Offset+e.ByteLength/2),(int)(e.EndExclusive-1)})
            .Concat(new[]{0,layout.SoundOffset,layout.SoundOffset+0x104,layout.SoundOffset+4+0x1F,
                layout.BankOffset-1,layout.BankOffset+layout.Sound.BankByteLength-1,
                (int)composition.Allocation.Rows[0].Row.Offset+(int)Word(body,24),body.Length-1}).Distinct().ToArray();
        foreach(int at in corruptOffsets)
        {byte[] bad=body.ToArray();bad[at]^=1;Reject<InvalidDataException>(()=>plan.RequireExactLevelData(bad),check);}
        Reject<InvalidDataException>(()=>plan.RequireExactLevelData(body[..^1]),check);
        Reject<InvalidDataException>(()=>plan.RequireExactLevelData([..body,(byte)0]),check);
        byte[] corruptSource=source.ReadSource(new(domains[0],0,domains[0].ByteLength));
        corruptSource[(int)source.PackageRead.Extent.Offset+4]^=1;
        Reject<InvalidDataException>(()=>Decode(domains,e=>ReferenceEquals(e.Space,domains[0])?
            corruptSource.AsSpan((int)e.Offset,(int)e.ByteLength).ToArray():source.ReadSource(e)),check);
        body[0]^=1;scene[0]^=1;package[4]^=1;
        plan.RequireRetainedOwners();plan.RequireExactLevelData(composition.CopyLevelData());
        check(Hash(composition.CopyLevelData())==composition.LevelDataSha256&&
            Hash(composition.CopyScene())==composition.SceneSha256&&Hash(source.CopyPackage())==source.PackageRead.ExpectedSha256&&
            composition.CopyInverseBaseBody().SequenceEqual(composition.BaseBodyAncestor.CopyLevelData()),
            "Copy mutations and every refusal preserve original source, complete Body/Scene and old inverse bytes.");
        Console.WriteLine($"NATIVE_FRESH_LOCKED_CHEST_SOUND_RESIDENCY_SOURCE_OK mixed={mixed} body={composition.LevelDataSha256} installs=0 nativeAdmission=false playback=false");
    }

    private static NativeSoundBank Bank(int offset)
    {
        byte[] component=new byte[0x11C],bank=new byte[64];
        Put(component,0,0x11C);Put(component,0x104,1);Put(component,0x108,(uint)offset);
        BinaryPrimitives.WriteUInt16LittleEndian(component.AsSpan(0x112),0x1000);
        bank[offset+17]=1; // Two complete legal ADPCM frames, ending on frame2.
        return NativeSoundBankCodec.Decode(component,bank);
    }

    // Test-only exact source acquisition. It never invokes the Town destination
    // compiler, scans inventory, writes a candidate or exposes a source issuer.
    private static async Task<NativeLockedChestFamilySource> CaptureSource(string path,NativeWizardFamilyAssetPlan wizard)
    {
        await using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        await NativeId65FreshDiscSource.RequireWholeOriginalAsync(file,CancellationToken.None);
        try
        {
            var layout=DiscImage.DetectLayout(file);
            if(layout!=new DiscLayout(2352,24,22,2048))throw new InvalidDataException("Sound smoke requires exact original raw geometry.");
            var wad=DiscImage.FindRootFileRecord(file,layout,n=>n.Equals("WAD.WAD",StringComparison.OrdinalIgnoreCase));
            var exe=DiscImage.FindRootFileRecord(file,layout,n=>n.Equals("SCUS_942.28",StringComparison.OrdinalIgnoreCase));
            if(wad.Lba!=37||wad.Size!=0x6927000||exe.Lba!=53875||exe.Size!=0x66000)
                throw new InvalidDataException("Sound smoke lost complete original WAD/EXE owners.");
            byte[] header=DiscImage.ReadFileBytes(file,layout,37,0,0x800);
            (int Id,int Offset,int Length)[] entries=[(22,0x1890800,0x29E800),(21,0x187E000,0x12800),
                (16,0x119E000,0x2E2000),(15,0x118E800,0xF800)];
            var domains=entries.Select(e=>new NativeAddressSpace(NativeAddressKind.LevelData,
                "sound-smoke-original-wad-"+e.Id,NativeAddressRevision.Original,e.Length)).Append(wizard.OriginalExecutableDomain).ToArray();
            Dictionary<NativeAddressSpace,byte[]> owners=[];
            for(int i=0;i<entries.Length;i++)
            {
                var entry=entries[i];
                if(Word(header,entry.Id*8)!=entry.Offset||Word(header,entry.Id*8+4)!=entry.Length)
                    throw new InvalidDataException("Sound smoke lost an exact original source entry.");
                owners.Add(domains[i],DiscImage.ReadFileBytes(file,layout,37,entry.Offset,entry.Length));
            }
            byte[] executable=DiscImage.ReadFileBytes(file,layout,exe.Lba,0,exe.Size);
            if(!executable.SequenceEqual(wizard.ReadPrivateSource(new(domains[4],0,domains[4].ByteLength))))
                throw new InvalidDataException("Sound smoke lost the actual retained Wizard Original executable ancestor.");
            owners.Add(domains[4],executable);
            return Decode(domains,e=>owners[e.Space].AsSpan((int)e.Offset,(int)e.ByteLength).ToArray());
        }
        finally {await NativeId65FreshDiscSource.RequireWholeOriginalAsync(file,CancellationToken.None);}
    }
    private static NativeAddressSpace[] Domains(NativeLockedChestFamilySource source)=>[
        source.PrivateRows[0].Row.Extent.Space,source.HandlerReads[0].Extent.Space,
        source.ResourceInputs["key.particle-descriptor"].Single().Extent.Space,
        source.ResourceInputs["key.native-handler"].Single().Extent.Space,source.MutableGlobals[0].ExecutableDomain];
    private static NativeLockedChestFamilySource Decode(NativeAddressSpace[] domains,Func<NativeByteExtent,byte[]> reader)=>
        NativeLockedChestFamilySource.DecodeOriginal(domains[0],domains[1],domains[2],domains[3],domains[4],reader);
    private static bool Same(NativeByteExtent a,NativeByteExtent b)=>ReferenceEquals(a.Space,b.Space)&&a.Offset==b.Offset&&a.ByteLength==b.ByteLength;
    private static byte[] Slice(byte[] bytes,NativeByteExtent extent)=>bytes.AsSpan((int)extent.Offset,(int)extent.ByteLength).ToArray();
    private static uint Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static void Put(Span<byte> bytes,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..],value);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void ReadOnly<T>(IReadOnlyList<T> values,Action<bool,string> check)
    {if(values is IList<T> list)Reject<NotSupportedException>(()=>list.Clear(),check);else check(true,"No mutable sound-residency list interface.");}
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Expected sound-residency refusal "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}

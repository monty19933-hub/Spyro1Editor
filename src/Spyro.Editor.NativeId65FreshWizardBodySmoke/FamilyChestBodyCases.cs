using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class FamilyChestBodyCases
{
    private const int BodyLength=0x20F000,SceneOffset=0x102000,SceneLength=0x3800;
    // Original-only scene reference460, frozen independently of this Body leaf.
    // There is deliberately no claimed original-only resource/Body golden:
    // the complete expected Body below is relative to its actual typed child.
    private const string OldSceneHash="70312f09b2441c602ac5e8a5edd10dd0be7edb3246928ad0bf9aba8450653cf0";
    private const string SceneHash="cb881f39cb6ea5bf6bd47cd23564474488dac15d565f403e3591e44ea720a83c";

    internal static void Free(Action<bool,string> check)
    {
        var type=typeof(NativeFreshLockedChestResearchBody);
        var constructors=type.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        check(type.IsSealed&&!type.IsPublic&&constructors.Length>0&&constructors.All(c=>c.IsPrivate)&&
            type.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance|BindingFlags.DeclaredOnly).Length==0,
            "Chest Body leaf is internal, sealed and privately issued, with no public candidate or export API.");
        foreach(var entry in new[]{(Name:"ResourceAncestor",Type:typeof(NativeFreshMobyFamilyCompositionPlan)),
            (Name:"HostAncestor",Type:typeof(NativeFreshLockedChestResearchHostExtension)),
            (Name:"SceneAncestor",Type:typeof(NativeFreshLockedChestSceneExtension)),
            (Name:"Layout",Type:typeof(NativeLevelDataGrowthLayout)),
            (Name:"RuntimeMemory",Type:typeof(NativeLevelRuntimeMemoryReport)),
            (Name:"FinalBodyDomain",Type:typeof(NativeAddressSpace)),
            (Name:"LevelDataSha256",Type:typeof(string)),(Name:"PlanSha256",Type:typeof(string))})
        {
            var property=type.GetProperty(entry.Name,BindingFlags.NonPublic|BindingFlags.Instance);
            check(property is not null&&property.PropertyType==entry.Type&&property.GetMethod?.IsAssembly==true&&
                property.GetSetMethod(true) is null,"Actual "+entry.Name+" is an internal read-only owner.");
        }
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestResearchBody.Compose(null!,null!,null!),check);
        Console.WriteLine("NATIVE_FRESH_CHEST_BODY_FREE_OK finalExecutableIssued=false nativeAdmission=false");
    }

    internal static void Source(NativeFreshMobyFamilyCompositionPlan resources,
        NativeFreshLockedChestResearchHostExtension host,NativeFreshLockedChestSceneExtension scene,Action<bool,string> check)
    {
        var parent=resources.ResourceAncestor??throw new InvalidDataException("Chest Body fixture needs its actual installed resource child.");
        check(resources.HasUnplacedModelResources&&resources.BaseBodyAncestor.MixedResourceAncestor is not null&&
            ReferenceEquals(host.CurrentExecutableAncestor.FamilyCompositionAncestor,parent)&&
            ReferenceEquals(scene.ResourceAncestor,resources)&&ReferenceEquals(scene.HostAncestor,host),
            "Child-relative fixture retains the actual mixed-B resource child and matching Host/Scene tuple.");
        byte[] before=resources.CopyLevelData(),oldScene=resources.CopyScene(),nextScene=scene.CopyScene();
        byte[] overlay=host.CopyOverlay(),initializer=host.CopyInitializer(),executable=host.CopyExecutablePrototype();
        resources.RequireExactLevelData(before);scene.RequireExactScene(nextScene);
        host.RequireExactOutputs(overlay,initializer,executable);
        check(before.Length==BodyLength&&oldScene.Length==SceneLength&&nextScene.Length==SceneLength&&
            Hash(oldScene)==OldSceneHash&&Hash(nextScene)==SceneHash&&
            Word(before,0x18)==SceneOffset&&Word(before,0x1C)==SceneLength&&
            before.AsSpan(SceneOffset,SceneLength).SequenceEqual(oldScene),
            "Actual child scene descriptor owns the complete P2 source, and replacement matches frozen460 separated-B P9 scene.");
        CheckDescriptors(before,check);
        // Independent leaf oracle: one direct descriptor-bounded copy, no
        // production growth/layout/compiler call to construct the expectation.
        byte[] expected=before.ToArray();nextScene.CopyTo(expected,SceneOffset);
        string childRelativeHash=Hash(expected);

        Reject<ArgumentNullException>(()=>NativeFreshLockedChestResearchBody.Compose(null!,host,scene),check);
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestResearchBody.Compose(resources,null!,scene),check);
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestResearchBody.Compose(resources,host,null!),check);
        Reject<InvalidDataException>(()=>NativeFreshLockedChestResearchBody.Compose(parent,host,scene),check);
        var body=NativeFreshLockedChestResearchBody.Compose(resources,host,scene);
        byte[] actual=body.CopyLevelData();
        check(ReferenceEquals(body.ResourceAncestor,resources)&&ReferenceEquals(body.HostAncestor,host)&&
            ReferenceEquals(body.SceneAncestor,scene),"Body retains all three actual immutable ancestors, not equal-byte stand-ins.");
        body.RequireAncestry(resources,host,scene);body.RequireExactLevelData(actual);
        check(body.SourceCertified&&!body.FinalExecutableIssued&&!body.RuntimeVerified&&!body.PublicationApproved&&
            !body.CompleteNativeDependencyClosureProven&&!body.PeakRuntimeMemoryProven,
            "Source-owned Body leaf grants no final executable, runtime, publication, dependency closure or peak-memory proof.");
        check(actual.Length==BodyLength&&actual.SequenceEqual(expected)&&body.LevelDataSha256==childRelativeHash&&
            IsHash(body.PlanSha256),"Complete Body equals the independent child-relative replacement, with a complete content hash and bounded plan identity.");
        check(body.FinalBodyDomain.Kind==NativeAddressKind.LevelData&&body.FinalBodyDomain.Revision==NativeAddressRevision.Final&&
            body.FinalBodyDomain.ByteLength==BodyLength&&!ReferenceEquals(body.FinalBodyDomain,resources.FinalBodyDomain)&&
            !ReferenceEquals(body.FinalBodyDomain,scene.FinalSceneDomain),
            "Final Body has a distinct complete Final LevelData domain, not relabelled Original, resource or scene storage.");
        CheckDescriptors(actual,check);
        check(actual.AsSpan(0,0x800).SequenceEqual(before.AsSpan(0,0x800))&&
            actual.AsSpan(0x50,0x180).SequenceEqual(before.AsSpan(0x50,0x180)),
            "Complete header, all descriptor fields, model root pointers, class IDs, terminators and reserved bytes are unchanged.");
        check(actual.AsSpan(0,SceneOffset).SequenceEqual(before.AsSpan(0,SceneOffset))&&
            actual.AsSpan(SceneOffset+SceneLength).SequenceEqual(before.AsSpan(SceneOffset+SceneLength))&&
            actual.AsSpan(SceneOffset,SceneLength).SequenceEqual(nextScene),
            "Complete outside-scene complement is unchanged, including GPU/audio/data/model/auxiliary allocations and padding.");
        var layout=body.Layout;
        check(layout.OriginalLength==BodyLength&&layout.FinalLength==BodyLength&&layout.Growth==0&&layout.Subfiles.Count==8&&
            layout.Subfiles.All(s=>s.OriginalOffset==s.FinalOffset&&s.OriginalLength==s.FinalLength&&s.Growth==0)&&
            layout.Subfiles.Where(s=>s.Index!=3).All(s=>s.Payloads.Count==0)&&layout.Subfiles[3].Payloads.Count==1,
            "Neutral growth layout contains exactly one same-size scene payload and no resource/header growth.");
        var payload=layout.Subfiles[3].Payloads.Single();
        check(payload.SubfileIndex==3&&payload.ModelBlockOffset==0&&payload.OriginalLength==SceneLength&&
            payload.Bytes.AsSpan().SequenceEqual(nextScene)&&layout.Expand(before).SequenceEqual(expected),
            "Only the complete scene is replaced; neutral expansion independently agrees with the direct-copy expectation.");
        for(int i=0;i<8;i++)
        {
            var subfile=layout.Subfiles[i];
            check(subfile.Index==i&&subfile.OriginalOffset==Word(before,i*8)&&subfile.OriginalLength==Word(before,i*8+4),
                "Every layout owner agrees with the actual complete descriptor "+i+".");
        }
        var mutableLayout=body.Layout;
        mutableLayout.Subfiles[3].Payloads[0].Bytes[0]^=1;
        body.RequireAncestry(resources,host,scene);body.RequireExactLevelData(expected);
        var detachedLayout=body.Layout;
        check(!ReferenceEquals(mutableLayout,detachedLayout)&&body.CopyLevelData().SequenceEqual(expected)&&
            detachedLayout.Subfiles[3].Payloads[0].Bytes.AsSpan().SequenceEqual(nextScene)&&
            detachedLayout.Expand(before).SequenceEqual(expected),
            "Mutating exposed neutral payload bytes cannot change the retained Body, ancestry or a freshly detached Layout.");
        CheckRuntime(body.RuntimeMemory,actual,check);
        byte[] inverse=body.RevertExactLevelData(actual);
        check(inverse.SequenceEqual(before),"Complete inverse recovers the immediate actual resource child, not retail or its earlier P2 parent.");
        byte[] directInverse=actual.ToArray();oldScene.CopyTo(directInverse,SceneOffset);
        check(directInverse.SequenceEqual(before),"Independent direct-copy inverse covers every Body byte.");
        Reject<InvalidDataException>(()=>resources.RequireExactLevelData(actual),check);
        Reject<InvalidDataException>(()=>body.RequireExactLevelData(before),check);
        Reject<InvalidDataException>(()=>body.RevertExactLevelData(before),check);

        string chestId=scene.RowSpans.Placements[2].EditorId;
        var repeatHost=NativeFreshLockedChestResearchHostExtension.Compose(host.CurrentExecutableAncestor,host.FamilySourceAncestor);
        var repeatScene=NativeFreshLockedChestSceneExtension.Compose(resources,host,chestId);
        check(!ReferenceEquals(repeatHost,host)&&repeatHost.PlanSha256==host.PlanSha256&&
            repeatHost.CopyOverlay().SequenceEqual(overlay)&&!ReferenceEquals(repeatScene,scene)&&
            repeatScene.PlanSha256==scene.PlanSha256&&repeatScene.CopyScene().SequenceEqual(nextScene),
            "Equal reissued Host/Scene plans are genuine distinct instances over the same source owners.");
        Reject<InvalidDataException>(()=>body.RequireAncestry(resources,repeatHost,scene),check);
        Reject<InvalidDataException>(()=>body.RequireAncestry(resources,host,repeatScene),check);
        Reject<InvalidDataException>(()=>body.RequireAncestry(parent,host,scene),check);
        Reject<InvalidDataException>(()=>NativeFreshLockedChestResearchBody.Compose(resources,repeatHost,scene),check);
        Reject<ArgumentNullException>(()=>body.RequireAncestry(null!,host,scene),check);
        Reject<ArgumentNullException>(()=>body.RequireAncestry(resources,null!,scene),check);
        Reject<ArgumentNullException>(()=>body.RequireAncestry(resources,host,null!),check);
        var repeat=NativeFreshLockedChestResearchBody.Compose(resources,host,scene);
        check(!ReferenceEquals(repeat,body)&&repeat.PlanSha256==body.PlanSha256&&repeat.LevelDataSha256==body.LevelDataSha256&&
            repeat.CopyLevelData().SequenceEqual(expected),"Repeated issuance from the exact tuple is deterministic and does not accumulate replacements.");

        var probes=new[]{0,0x18,0x1C,0x40,0x50,0x150,0x1CF,0x1D0,0x7FF,BodyLength-1}
            .Concat(Enumerable.Range(0,8).SelectMany(i=>new[]{checked((int)Word(before,i*8)),
                checked((int)(Word(before,i*8)+Word(before,i*8+4)-1))}))
            .Concat(new[]{0,0xCC,0xD0,0xD4,0x3EC,0xEEC,0xF44,0x1243,0x1244,0x1248,
                0x12A0,0x12A4,0x1383,0x1390,0x3390,0x33BB,0x33BC,0x37FF}.Select(at=>SceneOffset+at)).Distinct();
        foreach(int at in probes)
        {
            byte[] bad=actual.ToArray();bad[at]^=1;
            Reject<InvalidDataException>(()=>body.RequireExactLevelData(bad),check);
            Reject<InvalidDataException>(()=>body.RevertExactLevelData(bad),check);
        }
        foreach(byte[] bad in new byte[][]{Array.Empty<byte>(),actual[..^1],[..actual,(byte)0]})
        {Reject<InvalidDataException>(()=>body.RequireExactLevelData(bad),check);Reject<InvalidDataException>(()=>body.RevertExactLevelData(bad),check);}
        actual[0]^=1;inverse[0]^=1;nextScene[0]^=1;overlay[0]^=1;initializer[0]^=1;executable[0]^=1;
        check(body.CopyLevelData().SequenceEqual(expected)&&body.RevertExactLevelData(body.CopyLevelData()).SequenceEqual(before)&&
            resources.CopyLevelData().SequenceEqual(before)&&resources.CopyScene().SequenceEqual(oldScene)&&
            Hash(scene.CopyScene())==SceneHash&&host.CopyOverlay().SequenceEqual(repeatHost.CopyOverlay())&&
            host.CopyInitializer().SequenceEqual(repeatHost.CopyInitializer())&&
            host.CopyExecutablePrototype().SequenceEqual(repeatHost.CopyExecutablePrototype()),
            "Copy mutation and every refusal preserve complete Body, inverse, actual resource, scene, Host and prototype executable owners.");
        body.RequireAncestry(resources,host,scene);body.RequireExactLevelData(expected);
        resources.RequireExactLevelData(before);scene.RequireExactScene(scene.CopyScene());
        host.RequireExactOutputs(host.CopyOverlay(),host.CopyInitializer(),host.CopyExecutablePrototype());
        check(BinaryPrimitives.ReadUInt16LittleEndian(host.CopyExecutablePrototype().AsSpan(0x5FC7E))==10,
            "This Body leaf leaves the provisional target10 executable untouched; no census or final-executable claim.");
        Console.WriteLine($"NATIVE_FRESH_CHEST_BODY_SOURCE_OK childRelativeBody={childRelativeHash} scene={SceneHash} originalOnlyBodyProof=false finalExecutableIssued=false nativeAdmission=false");
    }

    private static void CheckDescriptors(byte[] bytes,Action<bool,string> check)
    {
        int cursor=0x800;
        for(int i=0;i<8;i++)
        {
            int at=checked((int)Word(bytes,i*8)),length=checked((int)Word(bytes,i*8+4));
            check(at==cursor&&length>0&&at%0x800==0&&length%0x800==0&&at<=bytes.Length-length,
                "Complete child-relative descriptor "+i+" is packed, aligned and bounded.");
            cursor=checked(at+length);
        }
        check(cursor==BodyLength&&bytes.AsSpan(0x40,0x10).SequenceEqual(new byte[0x10]),
            "Exactly eight subfiles exhaust the Body and both unused descriptors remain zero.");
    }

    private static void CheckRuntime(NativeLevelRuntimeMemoryReport memory,byte[] bytes,Action<bool,string> check)
    {
        check(memory.LevelId==65&&!memory.NativeSourceCertified&&memory.AnalyzedDataSha256==Hash(bytes)&&
            !memory.RuntimeFitProven&&!memory.PeakTransientFitProven&&!memory.PublicationApproved,
            "Neutral runtime address decoding stays non-source-certified and is not fit, peak-memory or publication authority.");
        check(memory.CopyBufferAddress==0x8008CA38u&&memory.UsedLevelDataBytes==0xA6C&&memory.LevelDataSectorPaddingBytes==0x594&&
            memory.ModelBytesRead==0x1B000&&memory.ModelRamStart==0x8008D4A4u&&
            memory.SceneBytesRead==SceneLength&&memory.SceneRamStart==0x800A84A4u&&memory.SceneReadEnd==0x800ABCA4u,
            "Host end plus actual used metadata, complete model allocation and complete scene produce the exact new normal-load addresses.");
        check(memory.PlacedMobyCount==9&&memory.DynamicMobyCapacity==32&&memory.DynamicArenaBytes==0xE58&&
            memory.MobyTableRamStart==0x800A8578u&&memory.NormalReservedBufferStart==0x80187BB0u&&
            memory.NormalReservedBufferGapBytes==0x80187BB0L-0x800ABCA4L&&memory.KnownPostLoadExtentsDisjoint&&
            (long)memory.CopyBufferAddress+0x80000<=memory.NormalReservedBufferStart,
            "P9/N32 and copy-buffer bounds are explicit normal-load accounting, not permission to consume the remaining gap.");
        int dynamic=0xD4+9*0x58,nextRow=dynamic+32*0x58,properties=nextRow+0x58+32*0x18;
        check(dynamic==0x3EC&&nextRow==0xEEC&&properties==0x1244&&
            Word(bytes,SceneOffset+0xCC)==properties-0xCC&&Word(bytes,SceneOffset+0xD0)==9&&
            !bytes.AsSpan(SceneOffset+dynamic,properties-dynamic).ContainsAnyExcept((byte)0)&&
            properties+4==0x1248&&0x33BC<=SceneLength,
            "Nine placed rows,32 dynamic rows,complete lookahead and backwards private arena remain disjoint from static private storage and scene padding.");
    }

    private static bool IsHash(string value)=>value.Length==64&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static uint Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Chest Body refused "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}

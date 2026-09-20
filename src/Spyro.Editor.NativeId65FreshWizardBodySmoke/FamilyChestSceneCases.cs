using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

internal static class FamilyChestSceneCases
{
    private const string ChestId="33333333333333333333333333333333";
    // Original-only source oracle460; neither value is learned from this producer.
    private const string OldSceneHash="70312f09b2441c602ac5e8a5edd10dd0be7edb3246928ad0bf9aba8450653cf0";
    private const string SceneHash="cb881f39cb6ea5bf6bd47cd23564474488dac15d565f403e3591e44ea720a83c";
    private static readonly int[] PrivateOffsets=[0x1248,0x1298,0x12A0,0x12A4,0x12BC,0x12E4,0x130C,0x1334,0x135C];
    private static readonly int[] PrivateLengths=[0x50,8,4,0x18,0x28,0x28,0x28,0x28,0x28];
    private static readonly int[] Fixups=[0xD4,0x1248,0x12C,0x184,0x1DC,0x234,0x28C,0x2E4,0x33C,0x394];

    internal static void Free(Action<bool,string> check)
    {
        var type=typeof(NativeFreshLockedChestSceneExtension);
        var constructors=type.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        check(type.IsSealed&&!type.IsPublic&&constructors.Length>0&&constructors.All(c=>c.IsPrivate)&&
            type.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance|BindingFlags.DeclaredOnly).Length==0,
            "Chest scene extension is internal, sealed and privately issued, without a public placement or Body API.");
        foreach(var entry in new[]{(Name:"ResourceAncestor",Type:typeof(NativeFreshMobyFamilyCompositionPlan)),
            (Name:"HostAncestor",Type:typeof(NativeFreshLockedChestResearchHostExtension)),
            (Name:"SourceAncestor",Type:typeof(NativeLockedChestFamilySource)),
            (Name:"RowSpans",Type:typeof(NativeFreshMobyFamilyRowSpanPlan)),
            (Name:"Layout",Type:typeof(NativeFreshMobySceneStorageLayout)),
            (Name:"Allocation",Type:typeof(NativeMobyDependencyInstantiationPlan)),
            (Name:"FinalSceneDomain",Type:typeof(NativeAddressSpace)),
            (Name:"SceneSha256",Type:typeof(string)),(Name:"PlanSha256",Type:typeof(string))})
        {
            var property=type.GetProperty(entry.Name,BindingFlags.NonPublic|BindingFlags.Instance);
            check(property is not null&&property.PropertyType==entry.Type&&property.GetMethod?.IsAssembly==true&&
                property.GetSetMethod(true) is null,"Actual "+entry.Name+" is an internal read-only owner.");
        }
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestSceneExtension.Compose(null!,null!,ChestId),check);
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestSceneStorageSource.Capture(null!,null!),check);
        Console.WriteLine("NATIVE_FRESH_CHEST_SCENE_EXTENSION_FREE_OK bodyIssued=false nativeAdmission=false");
    }

    internal static void Source(NativeFreshMobyFamilyCompositionPlan resources,
        NativeFreshLockedChestResearchHostExtension host,Action<bool,string> check)
    {
        var source=host.FamilySourceAncestor;
        var parent=resources.ResourceAncestor??throw new InvalidDataException("Chest scene fixture needs actual installed chest resources.");
        var current=host.CurrentExecutableAncestor;
        check(resources.HasUnplacedModelResources&&ReferenceEquals(current.FamilyCompositionAncestor,parent)&&
            ReferenceEquals(resources.BaseBodyAncestor,parent.BaseBodyAncestor)&&resources.BaseBodyAncestor.MixedResourceAncestor is not null,
            "Source fixture is the actual mixed-B Wizard+Grass resource composition and its matching detached Host.");
        byte[] oldScene=resources.CopyScene(),oldBody=resources.CopyLevelData();
        byte[] oldOverlay=host.CopyOverlay(),oldExecutable=host.CopyExecutablePrototype(),oldInitializer=host.CopyInitializer();
        check(oldScene.Length==0x3800&&Hash(oldScene)==OldSceneHash&&oldScene.SequenceEqual(parent.CopyScene())&&
            resources.Layout.PlacedCount==2&&resources.Allocation.Rows.Count==2&&resources.Placements.Count==2,
            "Independent460 P2 scene matches actual455 mixed-B bytes; the original-A control is not substituted.");
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestSceneExtension.Compose(null!,host,ChestId),check);
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestSceneExtension.Compose(resources,null!,ChestId),check);
        Reject<InvalidDataException>(()=>NativeFreshLockedChestSceneExtension.Compose(parent,host,ChestId),check);
        foreach(string bad in new[]{"",new string('0',32),new string('A',32),"not-an-editor-id",
            resources.Placements[0].EditorId,resources.Placements[1].EditorId})
            Reject<InvalidDataException>(()=>NativeFreshLockedChestSceneExtension.Compose(resources,host,bad),check);
        CheckStorageSource(resources,host,oldBody,check);

        var result=NativeFreshLockedChestSceneExtension.Compose(resources,host,ChestId);
        FamilyChestBodyCases.Source(resources,host,result,check);
        byte[] scene=result.CopyScene();
        check(ReferenceEquals(result.ResourceAncestor,resources)&&ReferenceEquals(result.HostAncestor,host)&&
            ReferenceEquals(result.SourceAncestor,source),"Scene binds all three actual resource/Host/source instances.");
        check(result.SourceCertified&&!result.BodyIssued&&!result.RuntimeVerified&&!result.PublicationApproved&&
            !result.AuthoredChestTransformProven&&!result.CompleteNativeDependencyClosureProven,
            "Original source/private storage is proven; authored chest coordinates, complete behavior, Body and publication remain closed.");
        check(result.SceneSha256==SceneHash&&Hash(scene)==SceneHash&&scene.Length==0x3800&&
            result.PlanSha256.Length==64&&result.PlanSha256.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f'),
            "Full P9 scene matches independent460, with a complete separate plan identity.");
        result.RequireAncestry(resources,host);result.RequireExactScene(scene);
        check(!ReferenceEquals(result.FinalSceneDomain,resources.FinalSceneDomain)&&
            result.FinalSceneDomain.Kind==NativeAddressKind.Scene&&result.FinalSceneDomain.Revision==NativeAddressRevision.Final&&
            result.FinalSceneDomain.ByteLength==0x3800&&!ReferenceEquals(result.Allocation,resources.Allocation)&&
            ReferenceEquals(result.RowSpans.Layout,result.Layout),
            "A new complete Final Scene and row allocation replace the old two-row ownership even without physical growth.");

        var layout=result.Layout;var allocation=result.Allocation;
        check(layout.PlacedCount==9&&layout.DynamicCapacity==32&&layout.RowTableOffset==0xD4&&
            layout.DynamicRowOffset==0x3EC&&layout.DynamicRowByteLength==0xB00&&
            layout.NextRowReserveOffset==0xEEC&&layout.NextRowReserveByteLength==0x58&&
            layout.DynamicPropertiesOffset==0xF44&&layout.DynamicPropertiesByteLength==0x300&&
            layout.PropertiesHeaderOffset==0x1244&&layout.PrivateByteLength==0x13C&&
            layout.PrivateOffsets.SequenceEqual(PrivateOffsets)&&layout.PrivateByteLengths.SequenceEqual(PrivateLengths)&&
            layout.NumericPodOffset==0x1384&&layout.BucketDataOffset==0x1390&&layout.FixupCountOffset==0x3390&&
            layout.FixupCount==10&&layout.UsedByteLength==0x33BC&&layout.SceneByteLength==0x3800,
            "Independent9-row/N32 geometry owns the full dynamic rows, lookahead, backwards private reservation and complete13C private region.");
        check(Word(scene,0xCC)==0x1178&&Word(scene,0xD0)==9&&Word(scene,0x1244)==0x140&&
            Word(scene,0x1384)==8&&Word(scene,0x1388)==0&&Word(scene,0x138C)==0x2004&&Word(scene,0x3390)==10,
            "Actual scene component fields encode nine placed native rows and complete private/bucket/fixup owners.");
        check(allocation.Rows.Count==9&&allocation.FinalRowCount==9&&allocation.PropertyBytesAllocated==0x13C&&
            allocation.Families.Count==3&&allocation.Rows.Select(r=>r.Index).SequenceEqual(Enumerable.Range(0,9))&&
            result.RowSpans.Placements.Select(s=>s.FirstRowIndex).SequenceEqual(new[]{0,1,2})&&
            result.RowSpans.Placements.Select(s=>s.RowCount).SequenceEqual(new[]{1,1,7})&&
            result.RowSpans.Placements.Select(s=>s.EditorId).SequenceEqual(resources.Placements.Select(p=>p.EditorId).Append(ChestId)),
            "Author order expands three families to9 contiguous native rows, rather than treating seven dependent rows as one.");
        result.RowSpans.RequireAllocation(allocation);
        result.RowSpans.RequireTemplates(allocation.Families.Select(f=>new NativeMobyDependencyInstantiationRequest(f.Id,f.Template)).ToArray());
        check(result.RowSpans.Placements.Take(2).SequenceEqual(resources.RowSpans.Placements)&&
            ReferenceEquals(resources.Placements[0].WizardAsset,parent.Placements[0].WizardAsset)&&
            ReferenceEquals(resources.Placements[1].PassiveAsset,parent.Placements[1].PassiveAsset),
            "Existing Wizard/Grass editor order and actual source assets are retained unchanged.");

        check(Enumerable.Range(0,10).Select(i=>checked((int)Word(scene,0x3394+i*4))).SequenceEqual(Fixups)&&
            result.RowSpans.PointerReferences.Count==10&&
            result.RowSpans.PointerReferences.Count(p=>p.FieldOwner==NativeFreshMobySceneOwnerKind.Private)==1,
            "Nine row pointers plus one Wizard route pointer form the exact native fixup list; integer owners are excluded.");
        for(int i=0;i<9;i++)
        {
            var row=allocation.Rows[i];int at=0xD4+i*0x58;
            check(ReferenceEquals(row.Row.Space,result.FinalSceneDomain)&&ReferenceEquals(row.Properties.Space,result.FinalSceneDomain)&&
                row.Row.Offset==at&&row.Row.ByteLength==0x58&&row.Properties.Offset==PrivateOffsets[i]&&
                row.Properties.ByteLength==PrivateLengths[i]&&Word(scene,at)==PrivateOffsets[i],
                "Actual allocated row/private owner and pointer "+i);
            byte[] expectedRow,expectedPrivate;
            if(i<2)
            {
                int oldPrivate=i==0?0xFE0:0x1030;
                expectedRow=oldScene.AsSpan(at,0x58).ToArray();expectedPrivate=oldScene.AsSpan(oldPrivate,PrivateLengths[i]).ToArray();
                if(i==0)Put(expectedPrivate,0,PrivateOffsets[0]+0x28);
                check(SameSchema(row.Source,resources.Allocation.Rows[i].Source),
                    "Retained authored row still binds its complete actual Original source schema "+i);
            }
            else
            {
                var schema=source.PrivateRows[i-2];
                expectedRow=source.ReadSource(schema.Row.Extent);expectedPrivate=source.ReadSource(schema.Properties.Extent);
                check(SameSchema(row.Source,schema)&&row.Source.SourceInstanceIndex==new[]{78,79,118,119,120,121,122}[i-2],
                    "Appended role binds its real Peace source schema/index "+i);
                if(i>=3)
                {
                    check(Word(expectedPrivate,0)==(i==3?78:79)&&
                        schema.OwnerFields.Single()==new NativeMobyDependencyOwnerField(0,i==3?0:1),
                        "Integer owner uses source78/79 and local family role, not a native scene address.");
                    Put(expectedPrivate,0,i==3?2:3);
                    check(Word(scene,PrivateOffsets[i])==(i==3?2:3)&&!Fixups.Contains(PrivateOffsets[i]),
                        "Actual full-word owner is rebased to key2/chest3 and excluded from native pointer fixups.");
                }
            }
            Put(expectedRow,0,PrivateOffsets[i]);
            check(scene.AsSpan(at,0x58).SequenceEqual(expectedRow)&&
                scene.AsSpan(PrivateOffsets[i],PrivateLengths[i]).SequenceEqual(expectedPrivate),
                "Complete row/private complement including authored Wizard/Grass and unmodified source chest coordinates "+i);
        }
        check(PrivateLengths.Skip(2).Sum()==0xE4&&PrivateOffsets[2]+0xE4==0x1384&&
            scene.AsSpan(0,0xCC).SequenceEqual(oldScene.AsSpan(0,0xCC))&&
            Word(scene,0xE0)==9984&&Word(scene,0xE4)==9984&&Word(scene,PrivateOffsets[0]+0x40)==9472&&
            Word(scene,PrivateOffsets[0]+0x44)==9984,
            "Complete E4 family append retains the actual separated-B spawn/prefix and Wizard route.");
        foreach(var span in new (int At,int Length)[]{(0x3EC,0xB00),(0xEEC,0x58),(0xF44,0x300),(0x1390,0x2000),(0x33BC,0x444)})
            check(!scene.AsSpan(span.At,span.Length).ContainsAnyExcept((byte)0),"Complete zero owner at"+span.At.ToString("X"));

        var chest=allocation.Families[2];
        check(chest.Id==ChestId&&chest.Rows.Select(r=>r.Index).SequenceEqual(Enumerable.Range(2,7))&&
            chest.Template.Globals.Count==2&&chest.Template.Globals.Select(g=>g.RuntimeAddress).SequenceEqual(new uint[]{0x80075830,0x80075758})&&
            chest.Template.Globals.All(g=>ReferenceEquals(g.ExecutableDomain,source.MutableGlobals[0].ExecutableDomain)),
            "One family retains both source-bound singleton globals on the actual executable domain.");
        Reject<InvalidDataException>(()=>allocation.Allocate([new("44444444444444444444444444444444",chest.Template)]),check);
        check(allocation.Rows.Count==9&&allocation.PropertyBytesAllocated==0x13C,"Refused second live family leaves its immutable allocation unchanged.");
        foreach(var family in allocation.Families)
        {
            var graph=family.Template.Shared;
            check(graph.Nodes.Count>0&&graph.Nodes.All(n=>n.Sharing==NativeMobyDependencySharing.Immutable&&
                n.Kind is not (NativeMobyDependencyKind.Instance or NativeMobyDependencyKind.InstanceProperties)),
                "Rows share immutable resource dependencies, never private row/property nodes.");
            foreach(var extent in graph.Installs.Select(i=>i.Destination).Concat(graph.Nodes.SelectMany(n=>n.Preserved).Select(p=>p.Final)))
                check(!ReferenceEquals(extent.Space,parent.FinalBodyDomain)&&!ReferenceEquals(extent.Space,parent.FinalSceneDomain),
                    "Storage cannot reuse stale pre-resource Body/Scene graph domains.");
        }
        ReadOnly(result.RowSpans.Placements,check);ReadOnly(result.RowSpans.Rows,check);
        ReadOnly(result.RowSpans.PointerReferences,check);ReadOnly(layout.PrivateOffsets,check);ReadOnly(allocation.Rows,check);

        var repeatHost=NativeFreshLockedChestResearchHostExtension.Compose(current,source);
        check(!ReferenceEquals(host,repeatHost)&&host.PlanSha256==repeatHost.PlanSha256,"Equal Host evidence is still a distinct actual instance.");
        Reject<InvalidDataException>(()=>result.RequireAncestry(resources,repeatHost),check);
        Reject<InvalidDataException>(()=>result.RequireAncestry(parent,host),check);
        var reissuedSource=NativeLockedChestFamilySource.DecodeOriginal(source.PrivateRows[0].Row.Extent.Space,
            source.HandlerReads[0].Extent.Space,source.ResourceInputs["sound.key.descriptor"].Single().Extent.Space,
            source.ResourceInputs["key.native-handler"].Single().Extent.Space,
            source.ResourceInputs["key.special-renderer-and-global-routines"].Single().Extent.Space,source.ReadSource);
        var otherHost=NativeFreshLockedChestResearchHostExtension.Compose(current,reissuedSource);
        check(!ReferenceEquals(source,reissuedSource)&&source.CopyPackage().SequenceEqual(reissuedSource.CopyPackage()),
            "Equal newly issued source bytes cannot substitute for the resource composition's actual source owner.");
        Reject<InvalidDataException>(()=>NativeFreshLockedChestSceneExtension.Compose(resources,otherHost,ChestId),check);
        check(result.RevertExactScene(scene).SequenceEqual(oldScene),"Complete inverse returns the actual immediate P2 scene.");
        foreach(byte[] bad in new byte[][]{Array.Empty<byte>(),oldScene,scene[..^1],[..scene,(byte)0]})
        {Reject<InvalidDataException>(()=>result.RequireExactScene(bad),check);Reject<InvalidDataException>(()=>result.RevertExactScene(bad),check);}
        var mutationOffsets=new[]{0,0xCB,0xCC,0xD0,0x3EC,0xEEC,0xF44,0x1243,0x1244,0x1384,0x138C,0x1390,0x338F,0x3390,0x33BB,0x33BC,0x37FF}
            .Concat(Enumerable.Range(0,9).SelectMany(i=>new[]{0xD4+i*0x58,0xD4+(i+1)*0x58-1,PrivateOffsets[i],PrivateOffsets[i]+PrivateLengths[i]-1}))
            .Concat(Enumerable.Range(0,10).Select(i=>0x3394+i*4)).Distinct();
        foreach(int at in mutationOffsets)
        {
            byte[] bad=scene.ToArray();bad[at]^=1;
            Reject<InvalidDataException>(()=>result.RequireExactScene(bad),check);
            Reject<InvalidDataException>(()=>result.RevertExactScene(bad),check);
        }
        var repeat=NativeFreshLockedChestSceneExtension.Compose(resources,host,ChestId);
        var otherId=NativeFreshLockedChestSceneExtension.Compose(resources,host,"44444444444444444444444444444444");
        check(!ReferenceEquals(repeat,result)&&repeat.PlanSha256==result.PlanSha256&&repeat.CopyScene().SequenceEqual(scene)&&
            otherId.SceneSha256==result.SceneSha256&&otherId.PlanSha256!=result.PlanSha256,
            "Repeated issue is deterministic; changing only stable editor identity preserves bytes but changes the full plan.");
        byte[] inverse=result.RevertExactScene(scene);scene[0]^=1;inverse[0]^=1;
        check(Hash(result.CopyScene())==SceneHash&&result.RevertExactScene(result.CopyScene()).SequenceEqual(oldScene)&&
            resources.CopyScene().SequenceEqual(oldScene)&&resources.CopyLevelData().SequenceEqual(oldBody)&&
            host.CopyOverlay().SequenceEqual(oldOverlay)&&host.CopyExecutablePrototype().SequenceEqual(oldExecutable),
            "Copy mutation/refusal leaves all outputs, complete inverse, resource Body and detached Host unchanged.");
        resources.RequireExactLevelData(oldBody);host.RequireExactOutputs(oldOverlay,oldInitializer,oldExecutable);source.RequireRetainedOwners();
        Console.WriteLine($"NATIVE_FRESH_CHEST_SCENE_EXTENSION_SOURCE_OK scene={SceneHash} oldScene={OldSceneHash} bodyIssued=false nativeAdmission=false");
    }

    private static void CheckStorageSource(NativeFreshMobyFamilyCompositionPlan resources,
        NativeFreshLockedChestResearchHostExtension host,byte[] body,Action<bool,string> check)
    {
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestSceneStorageSource.Capture(null!,host),check);
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestSceneStorageSource.Capture(resources,null!),check);
        var storage=NativeFreshLockedChestSceneStorageSource.Capture(resources,host);
        var graph=storage.Graph;
        var templates=new[]{storage.WizardTemplate,storage.PassiveTemplate,storage.ChestTemplate};
        check(ReferenceEquals(storage.Source,host.FamilySourceAncestor)&&
            templates.All(t=>ReferenceEquals(t.Shared,graph))&&templates.Select(t=>t.Policy).Distinct(StringComparer.Ordinal).Count()==1&&
            !string.IsNullOrWhiteSpace(templates[0].Policy)&&templates.Select(t=>t.Rows.Count).SequenceEqual(new[]{1,1,7}),
            "All three exact private templates share one actual graph and one explicit immutable-storage policy.");
        check(graph.Nodes.Count==8&&graph.Installs.Count==8&&graph.References.Count==0&&
            graph.Nodes.All(n=>n.Sharing==NativeMobyDependencySharing.Immutable&&n.Preserved.Count==0),
            "Storage has exactly eight immutable installed owners, not overlapping alias or hidden preservation claims.");
        var installs=graph.Installs.OrderBy(i=>i.Destination.Offset).ToArray();
        List<(long Offset,long Length)> expected=[(0,0x800)];
        for(int i=0;i<8;i++)if(i!=3)expected.Add((Word(body,i*8),Word(body,i*8+4)));
        check(installs.Select(i=>(i.Destination.Offset,i.Destination.ByteLength)).SequenceEqual(expected),
            "Eight installed spans are exactly the actual header and seven complete non-Scene subfiles.");
        int sceneAt=checked((int)Word(body,0x18)),sceneLength=checked((int)Word(body,0x1C));
        var partition=installs.Select(i=>(Offset:i.Destination.Offset,Length:i.Destination.ByteLength))
            .Append((Offset:(long)sceneAt,Length:(long)sceneLength)).OrderBy(p=>p.Offset).ToArray();
        long cursor=0;
        foreach(var span in partition)
        {
            check(span.Offset==cursor&&span.Length>0&&span.Offset<=body.LongLength-span.Length,
                "Installed owners plus the one excluded Scene form a gap-free, nonoverlapping partition.");
            cursor=checked(span.Offset+span.Length);
        }
        check(cursor==body.LongLength&&installs.Sum(i=>i.Destination.ByteLength)==body.LongLength-sceneLength,
            "Full Body coverage excludes exactly the complete old Scene, including all allocation padding elsewhere.");
        foreach(var install in installs)
        {
            var extent=install.Destination;byte[] payload=graph.CopyPayload(install.NodeId);
            check(ReferenceEquals(extent.Space,resources.FinalBodyDomain)&&extent.Space.Revision==NativeAddressRevision.Final&&
                !(extent.Offset<sceneAt+(long)sceneLength&&sceneAt<extent.EndExclusive)&&
                payload.LongLength==extent.ByteLength&&Hash(payload)==install.PayloadSha256&&
                payload.AsSpan().SequenceEqual(body.AsSpan(checked((int)extent.Offset),checked((int)extent.ByteLength))),
                "Every installed payload equals its complete actual Final resource Body owner.");
            payload[0]^=1;
            check(graph.CopyPayload(install.NodeId).AsSpan().SequenceEqual(body.AsSpan(checked((int)extent.Offset),checked((int)extent.ByteLength))),
                "Shared storage payload copies cannot mutate the immutable graph.");
            Reject<InvalidDataException>(()=>storage.ReadSource(extent),check);
        }
        var source=host.FamilySourceAncestor;var wizard=resources.Placements[0].WizardAsset!;var passive=resources.Placements[1].PassiveAsset!;
        var originalDomains=source.ResourceInputs.Values.SelectMany(v=>v).Concat(source.HandlerReads).Append(source.PackageRead)
            .Select(r=>r.Extent.Space).Concat(source.PrivateRows.SelectMany(r=>new[]{r.Row.Extent.Space,r.Properties.Extent.Space}))
            .Concat(new[]{wizard.OriginalDataDomain,wizard.OriginalExecutableDomain})
            .Concat(passive.AssetGraph.Nodes.SelectMany(n=>n.Reads).Select(r=>r.Extent.Space))
            .Concat(passive.InstanceTemplate.Rows.SelectMany(r=>new[]{r.Row.Extent.Space,r.Properties.Extent.Space})).ToArray();
        var reads=graph.Nodes.SelectMany(n=>n.Reads).ToArray();
        check(reads.Length>0&&reads.All(r=>r.Extent.Space.Revision==NativeAddressRevision.Original&&
            originalDomains.Any(d=>ReferenceEquals(d,r.Extent.Space))&&!ReferenceEquals(r.Extent.Space,resources.FinalBodyDomain)),
            "Every graph read names an actual retained Original source domain; generated Final bytes are payloads, never relabelled source reads.");
        foreach(var read in reads)
        {
            byte[] bytes=storage.ReadSource(read.Extent);
            check(bytes.LongLength==read.Extent.ByteLength&&Hash(bytes)==read.ExpectedSha256,
                "Declared graph read returns its complete authenticated Original bytes.");
        }
        var positive=reads.First(r=>r.Extent.ByteLength>=8).Extent;
        byte[] first=storage.ReadSource(positive);string sourceHash=Hash(first);first[0]^=1;
        check(Hash(storage.ReadSource(positive))==sourceHash,"Source read returns detached immutable copies.");
        Reject<ArgumentNullException>(()=>storage.ReadSource(null!),check);
        Reject<InvalidDataException>(()=>storage.ReadSource(new(positive.Space,positive.Offset,positive.ByteLength-1)),check);
        Reject<InvalidDataException>(()=>storage.ReadSource(new(positive.Space,positive.Offset+4,positive.ByteLength-4)),check);
        var equalOriginal=new NativeAddressSpace(positive.Space.Kind,positive.Space.OwnerIdentity,NativeAddressRevision.Original,positive.Space.ByteLength);
        Reject<InvalidDataException>(()=>storage.ReadSource(new(equalOriginal,positive.Offset,positive.ByteLength)),check);
        var equalFinal=new NativeAddressSpace(positive.Space.Kind,positive.Space.OwnerIdentity,NativeAddressRevision.Final,positive.Space.ByteLength);
        Reject<InvalidDataException>(()=>storage.ReadSource(new(equalFinal,positive.Offset,positive.ByteLength)),check);
        check(resources.CopyLevelData().SequenceEqual(body)&&Hash(storage.ReadSource(positive))==sourceHash,
            "Wrong length, subextent, equal-labelled Original and Final refusals preserve all actual storage/source owners.");
    }

    private static bool SameSchema(NativeMobyDependencyPrivateSchema a,NativeMobyDependencyPrivateSchema b)=>
        a.Role==b.Role&&a.SourceInstanceIndex==b.SourceInstanceIndex&&a.NativeClass==b.NativeClass&&
        a.Row==b.Row&&a.Properties==b.Properties&&a.PropertiesPointerBaseOffset==b.PropertiesPointerBaseOffset&&
        a.RequiresBytePersistentOwner==b.RequiresBytePersistentOwner&&a.OwnerFields.SequenceEqual(b.OwnerFields);
    private static void ReadOnly<T>(IReadOnlyList<T> values,Action<bool,string> check)
    {if(values is IList<T> list)Reject<NotSupportedException>(()=>list.Clear(),check);else check(true,"No mutable list interface.");}
    private static uint Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static void Put(Span<byte> bytes,int at,int value)=>BinaryPrimitives.WriteInt32LittleEndian(bytes[at..],value);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Chest scene refused "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}

using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

// Child-relative reference only: existing typed R03 owners plus independently
// frozen tiled data. This does not certify a new executable, image or gameplay.
internal static class TiledFamilyBodyCases
{
    // Independently frozen child oracle, root replay600; no C# Body existed.
    private const string ExpectedBodyHash="c8c85e71b633e29ce573dddccb04a4a0adcd6696d292658304328e435dfaa9ca";
    private static readonly string[] SubfileHashes=[
        "6e20736d79a86035119d264af1f8acb307f6a08e573295afcd89097b4330e014",
        "83baa79d7b1f9f27eadecb721f1026b0d0aea57cc65f0758b5353eaa0fc36fb6",
        "b3fdc6639588149657de506300930e5f96cb69dc3b02a4362fac52a7c3f764bf",
        "4f560dcbfa2d997a3f02554cf297d95c1778f67fc5d8fcf8ff47f64d609a89e8",
        "bdf27988f3d2b0fd4c4b905636b6980aff49ab1475d85edb4ce8aa2cc469351f",
        "23968146e46b3263997e16692a192bbdb049966b010673d17611f2ab6903cef0",
        "402bb40e21bf84922cbc35312b0e49b95292a2f96326636aba05685443686991",
        "eabadb4ea663043665bc4ba2d2e00c1c7c104688906b56bac2d7aba7c6cae8da"];
    private const int DataAt=0xE6000,DataLength=0x1000,SceneAt=0x102000,SceneLength=0x3800;
    private static readonly int[] PrivateAt=[0x1248,0x1298,0x12A0,0x12A4,0x12BC,0x12E4,0x130C,0x1334,0x135C];
    private static readonly int[] PrivateLength=[0x50,8,4,0x18,0x28,0x28,0x28,0x28,0x28];

    internal static void Free(Action<bool,string> check)
    {
        var type=typeof(NativeFreshTiledFamilyBodyPlan);
        check(type.IsSealed&&!type.IsPublic&&type.GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic).All(c=>c.IsPrivate)&&
            type.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance|BindingFlags.DeclaredOnly).Length==0,
            "Tiled family Body is privately constructed and has no public or arbitrary-byte ingress.");
        Reject<ArgumentNullException>(()=>NativeFreshTiledFamilyBodyPlan.Compose(null!),check);
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestSceneStorageSource.CaptureForTerrainRevision(null!),check);
        Console.WriteLine("TILED_FAMILY_BODY_FREE_OK sourceOpened=false finalExecutableIssued=false publication=false");
    }

    internal static void Source(NativeFreshTiledFamilyTerrainRevision revision,Action<bool,string> check)
    {
        var placement=revision.PlacementAncestor;var oldBody=revision.BodyAncestor;
        var resources=revision.ResourceAncestor;var host=revision.HostAncestor;var priorScene=oldBody.SceneAncestor;
        byte[] before=placement.CopyLevelData(),scene=placement.CopyScene(),used=revision.CopyUsedData();
        byte[] overlay=host.CopyOverlay(),initializer=host.CopyInitializer(),exe=host.CopyExecutablePrototype();
        var oldStorage=NativeFreshLockedChestSceneStorageSource.Capture(resources,host);
        string oldGraph=oldStorage.Graph.Fingerprint,oldScenePlan=priorScene.PlanSha256,oldBodyPlan=oldBody.PlanSha256;
        // Independent direct bounded replacement, never a production growth or
        // scene encoder: complete expected image is also externally hash-pinned.
        byte[] expected=before.ToArray();expected.AsSpan(DataAt,DataLength).Clear();used.CopyTo(expected,DataAt);
        check(before.Length==0x20F000&&Word(before,8)==DataAt&&Word(before,12)==DataLength&&
            Hash(before)=="2d8c1bf1af35c8e5522b239c564d36c79c828539d133278c461b6178b53bb504"&&
            Hash(expected)==ExpectedBodyHash,"Actual old child and complete direct-splice Body match independent references.");
        var body=NativeFreshTiledFamilyBodyPlan.Compose(revision);
        body.RequireAncestry(revision);
        check(ReferenceEquals(body.TerrainAncestor,revision)&&ReferenceEquals(body.PlacementAncestor,placement)&&
            ReferenceEquals(body.ResourceAncestor,resources)&&ReferenceEquals(body.HostAncestor,host),
            "Body retains the actual complete terrain/placement/resource/Host tuple.");
        byte[] actual=body.CopyLevelData();body.RequireExactLevelData(actual);body.RequireExactScene(scene);
        check(actual.SequenceEqual(expected)&&actual.Length==before.Length&&body.LevelDataSha256==ExpectedBodyHash&&
            body.CopyScene().SequenceEqual(scene)&&body.SceneSha256==Hash(scene),
            "Complete Body and freshly reconstructed placed Scene are byte-exact, including authored Reward2.");
        check(body.Layout.Growth==0&&body.Layout.Subfiles.Count==8&&body.Layout.Expand(before).SequenceEqual(expected)&&
            !ReferenceEquals(body.Layout,body.Layout),"Complete padded-data replacement has zero physical growth and detached layout snapshots.");
        check(actual.AsSpan(0,0x800).SequenceEqual(before.AsSpan(0,0x800))&&
            actual.AsSpan(0,DataAt).SequenceEqual(before.AsSpan(0,DataAt))&&
            actual.AsSpan(DataAt+DataLength).SequenceEqual(before.AsSpan(DataAt+DataLength))&&
            actual.AsSpan(DataAt+0xCB8,0x348).IndexOfAnyExcept((byte)0)<0,
            "Complete header, roots, all outside-data bytes and the entire348-byte padding are preserved.");
        check(Hash(actual.AsSpan(0,0x800))=="79a8467e3c33d6924bde2253e2f165cf8423cc1d719730c09aa01dbe042a467f"&&
            Hash(actual[..DataAt].Concat(actual[(DataAt+DataLength)..]).ToArray())=="1216590804168647145c2ee8ddbe8a1c38c2ec80dc86363b2f7edf0b40146a6a"&&
            Enumerable.Range(0,actual.Length).Count(i=>actual[i]!=before[i])==1413,
            "Independent complete header/non-data complement and changed-byte census match the child reference.");
        for(int i=0;i<8;i++)
        {
            int at=Word(before,i*8),length=Word(before,i*8+4);
            check(Word(actual,i*8)==at&&Word(actual,i*8+4)==length&&Hash(actual.AsSpan(at,length))==SubfileHashes[i]&&
                (i==1||actual.AsSpan(at,length).SequenceEqual(before.AsSpan(at,length))),
                "Exact original descriptor and complete non-data subfile "+i);
        }
        check(body.RevertExactLevelData(actual).SequenceEqual(before)&&
            revision.RevertExactDataAllocation(actual.AsSpan(DataAt,DataLength)).SequenceEqual(before.AsSpan(DataAt,DataLength).ToArray()),
            "Both immediate inverses restore the complete actual placed child/allocation.");

        CheckStorage(body,expected,oldStorage,check);
        var layout=body.SceneLayout;var allocation=body.Allocation;
        check(layout.PlacedCount==9&&layout.DynamicCapacity==32&&layout.PrivateByteLength==0x13C&&
            layout.SceneByteLength==SceneLength&&layout.FixupCount==10&&layout.UsedByteLength==0x33BC&&
            layout.PrivateOffsets.SequenceEqual(PrivateAt)&&layout.PrivateByteLengths.SequenceEqual(PrivateLength)&&
            body.RowSpans.Placements.SequenceEqual(priorScene.RowSpans.Placements)&&
            ReferenceEquals(body.RowSpans.Layout,layout)&&!ReferenceEquals(allocation,priorScene.Allocation)&&allocation.Rows.Count==9,
            "Fresh shared allocation retains the complete1+1+7 P9/N32/private/fixup geometry and actual authored IDs.");
        body.RowSpans.RequireAllocation(allocation);
        for(int i=0;i<9;i++)
        {
            var row=allocation.Rows[i];var prior=priorScene.Allocation.Rows[i];
            check(ReferenceEquals(row.Row.Space,body.FinalSceneDomain)&&ReferenceEquals(row.Properties.Space,body.FinalSceneDomain)&&
                row.Row.Offset==0xD4+i*0x58&&row.Properties.Offset==PrivateAt[i]&&row.Properties.ByteLength==PrivateLength[i]&&
                row.Source.Row==prior.Source.Row&&row.Source.Properties==prior.Source.Properties&&
                actual.AsSpan(SceneAt+(int)row.Row.Offset,0x58).SequenceEqual(scene.AsSpan((int)prior.Row.Offset,0x58))&&
                actual.AsSpan(SceneAt+PrivateAt[i],PrivateLength[i]).SequenceEqual(scene.AsSpan(PrivateAt[i],PrivateLength[i])),
                "Actual Original schema and complete authored row/private complement survive fresh allocation "+i);
        }
        foreach(var span in new[]{(0x3EC,0xB00),(0xEEC,0x58),(0xF44,0x300),(0x1390,0x2000),(0x33BC,0x444)})
            check(actual.AsSpan(SceneAt+span.Item1,span.Item2).IndexOfAnyExcept((byte)0)<0,"Complete reserve/padding remains zero.");
        check(Enumerable.Range(0,10).Select(i=>Word(actual,SceneAt+0x3394+4*i)).SequenceEqual(
            new[]{0xD4,0x1248,0x12C,0x184,0x1DC,0x234,0x28C,0x2E4,0x33C,0x394}),
            "All ten fixups remain exact; integer family owner indices are not pointers.");
        var memory=body.RuntimeMemory;
        // These report fields are Body-relative; the oracle tail offsets are data-relative.
        check(memory.AnalyzedDataSha256==ExpectedBodyHash&&memory.CopyBufferAddress==0x8008CA38u&&
            memory.UsedLevelDataBytes==0xCB8&&memory.LevelDataSectorPaddingBytes==0x348&&
            memory.ParticleComponentDataOffset==DataAt+0x50C&&memory.SoundComponentDataOffset==DataAt+0x598&&
            memory.ModelBytesRead==0x1B000&&memory.ModelRamStart==0x8008D6F0u&&memory.SceneBytesRead==SceneLength&&
            memory.SceneRamStart==0x800A86F0u&&memory.SceneReadEnd==0x800ABEF0u&&
            memory.MobyTableRamStart==0x800A87C4u&&memory.PlacedMobyCount==9&&memory.DynamicMobyCapacity==32&&
            memory.NormalReservedBufferStart==0x80187BB0u&&memory.KnownPostLoadExtentsDisjoint,
            "Independent parser endpoints and actual Body decode agree without retaining old A6C runtime addresses.");
        check(body.SourceCertified&&!body.FinalExecutableIssued&&!body.RuntimeVerified&&!body.PublicationApproved&&
            !body.CompleteNativeDependencyClosureProven&&!body.PeakRuntimeMemoryProven&&
            !memory.NativeSourceCertified&&!memory.RuntimeFitProven&&!memory.PeakTransientFitProven&&!memory.PublicationApproved,
            "Body source composition and neutral address accounting grant no native-load, fit, peak, trajectory or publication proof.");
        var equalRevision=NativeFreshTiledFamilyTerrainRevision.Compose(revision.IntentAncestor,placement);
        check(equalRevision.PlanSha256==revision.PlanSha256&&equalRevision.CopyUsedData().SequenceEqual(used),
            "Foreign revision is fully byte-equal before actual ancestry refusal.");
        Reject<InvalidDataException>(()=>body.RequireAncestry(equalRevision),check);
        Reject<ArgumentNullException>(()=>body.RequireAncestry(null!),check);
        var repeated=NativeFreshTiledFamilyBodyPlan.Compose(revision);
        check(repeated.PlanSha256==body.PlanSha256&&repeated.CopyLevelData().SequenceEqual(expected)&&
            !ReferenceEquals(repeated.FinalBodyDomain,body.FinalBodyDomain)&&!ReferenceEquals(repeated.FinalSceneDomain,body.FinalSceneDomain),
            "Repeat issuance is deterministic while Final address domains retain actual instance identity.");
        foreach(int at in new[]{0,8,12,0x50,0x7FF,DataAt-1,DataAt,DataAt+0x177,DataAt+0x178,DataAt+0x508,
            DataAt+0xCB7,DataAt+0xCB8,DataAt+0xFFF,DataAt+DataLength,SceneAt,SceneAt+0xD4,SceneAt+0x12E4,
            SceneAt+0x3390,SceneAt+SceneLength-1,expected.Length-1})
        {
            byte[] corrupt=expected.ToArray();corrupt[at]^=1;
            Reject<InvalidDataException>(()=>body.RequireExactLevelData(corrupt),check);
            Reject<InvalidDataException>(()=>body.RevertExactLevelData(corrupt),check);
        }
        Reject<InvalidDataException>(()=>body.RevertExactLevelData(expected[..^1]),check);
        Reject<InvalidDataException>(()=>body.RevertExactLevelData(expected.Concat(new byte[4]).ToArray()),check);
        byte[] badScene=scene.ToArray();badScene[0x12E8]^=1;
        Reject<InvalidDataException>(()=>body.RequireExactScene(badScene),check);
        body.Layout.Subfiles[1].Payloads[0].Bytes[0]^=1;
        actual[DataAt]^=1;body.CopyScene()[0]^=1;body.RevertExactLevelData(expected)[0]^=1;
        check(body.CopyLevelData().SequenceEqual(expected)&&body.Layout.Expand(before).SequenceEqual(expected)&&
            body.CopyScene().SequenceEqual(scene)&&body.RevertExactLevelData(expected).SequenceEqual(before)&&
            placement.CopyLevelData().SequenceEqual(before)&&host.CopyOverlay().SequenceEqual(overlay)&&
            host.CopyInitializer().SequenceEqual(initializer)&&host.CopyExecutablePrototype().SequenceEqual(exe)&&
            oldBody.PlanSha256==oldBodyPlan&&priorScene.PlanSha256==oldScenePlan&&oldBody.RuntimeMemory.UsedLevelDataBytes==0xA6C&&
            NativeFreshLockedChestSceneStorageSource.Capture(resources,host).Graph.Fingerprint==oldGraph,
            "Detached mutations/refusals leave the complete old Body certificate, resource graph, Scene, Host and executable unchanged.");
        Console.WriteLine($"TILED_FAMILY_BODY_SOURCE_OK body={body.LevelDataSha256} immediateChildInverse=true finalExecutableIssued=false runtime=false publication=false");
    }

    private static void CheckStorage(NativeFreshTiledFamilyBodyPlan body,byte[] expected,
        NativeFreshLockedChestSceneStorageSource oldStorage,Action<bool,string> check)
    {
        var storage=body.StorageSource;var graph=storage.Graph;
        check(body.FinalBodyDomain.Kind==NativeAddressKind.LevelData&&body.FinalBodyDomain.Revision==NativeAddressRevision.Final&&
            body.FinalBodyDomain.ByteLength==expected.Length&&body.FinalSceneDomain.Kind==NativeAddressKind.Scene&&
            body.FinalSceneDomain.Revision==NativeAddressRevision.Final&&body.FinalSceneDomain.ByteLength==SceneLength&&
            !ReferenceEquals(body.FinalBodyDomain,body.ResourceAncestor.FinalBodyDomain)&&
            !ReferenceEquals(body.FinalBodyDomain,body.PlacementAncestor.BodyAncestor.FinalBodyDomain)&&
            !ReferenceEquals(body.FinalSceneDomain,body.PlacementAncestor.BodyAncestor.SceneAncestor.FinalSceneDomain),
            "New Body and Scene use fresh complete Final domains, not old or Original owner labels.");
        var templates=new[]{storage.WizardTemplate,storage.PassiveTemplate,storage.ChestTemplate};
        check(templates.All(t=>ReferenceEquals(t.Shared,graph))&&templates.Select(t=>t.Policy).Distinct().Count()==1&&
            templates.Select(t=>t.Rows.Count).SequenceEqual(new[]{1,1,7})&&
            body.Allocation.Families.Select(f=>f.Template).Zip(templates).All(p=>ReferenceEquals(p.First,p.Second))&&
            !ReferenceEquals(graph,oldStorage.Graph)&&graph.Fingerprint!=oldStorage.Graph.Fingerprint&&
            oldStorage.WizardTemplate.Policy=="source-owned-mixed-family-chest-scene-storage-v1",
            "All three actual new templates/allocated families share one fresh graph/policy; old policy/fingerprint remain separate.");
        check(ReferenceEquals(storage.Source,body.HostAncestor.FamilySourceAncestor)&&storage.ChestTemplate.Globals.Count==2&&
            storage.ChestTemplate.Globals.All(g=>ReferenceEquals(g.ExecutableDomain,storage.Source.MutableGlobals[0].ExecutableDomain))&&
            storage.WizardTemplate.Globals.Count==0&&storage.PassiveTemplate.Globals.Count==0,
            "Source-owned singleton globals remain in the actual Original executable domain and are not dropped.");
        check(graph.Nodes.Count==8&&graph.Installs.Count==8&&graph.References.Count==0&&
            graph.Nodes.All(n=>n.Preserved.Count==0&&n.Sharing==NativeMobyDependencySharing.Immutable),
            "One header plus seven non-Scene owners have no overlapping broad preservation or invented imports.");
        var installs=graph.Installs.OrderBy(i=>i.Destination.Offset).ToArray();
        var spans=new List<(long At,long Length)>{(0,0x800)};
        for(int i=0;i<8;i++)if(i!=3)spans.Add((Word(expected,i*8),Word(expected,i*8+4)));
        check(installs.Select(i=>(i.Destination.Offset,i.Destination.ByteLength)).SequenceEqual(spans)&&
            installs.Sum(i=>i.Destination.ByteLength)==expected.Length-SceneLength,
            "Shared graph covers the complete revised Body except exactly its Scene.");
        foreach(var install in installs)
        {
            var extent=install.Destination;byte[] bytes=graph.CopyPayload(install.NodeId);
            check(ReferenceEquals(extent.Space,body.FinalBodyDomain)&&
                bytes.AsSpan().SequenceEqual(expected.AsSpan((int)extent.Offset,(int)extent.ByteLength)),
                "Every installed payload belongs to the fresh actual final Body, including revised data and unchanged GPU/model owners.");
            Reject<InvalidDataException>(()=>storage.ReadSource(extent),check);
            bytes[0]^=1;
            check(graph.CopyPayload(install.NodeId).AsSpan().SequenceEqual(expected.AsSpan((int)extent.Offset,(int)extent.ByteLength)),
                "Graph payload copies are detached.");
        }
        var reads=graph.Nodes.SelectMany(n=>n.Reads).ToArray();
        check(reads.Length>0&&reads.All(r=>r.Extent.Space.Revision==NativeAddressRevision.Original)&&
            reads.Select(r=>r.Extent).SequenceEqual(oldStorage.Graph.Nodes.SelectMany(n=>n.Reads).Select(r=>r.Extent)),
            "New storage shares the exact retained Original read provenance, not generated bytes labelled Original.");
        var read=reads.First();byte[] original=storage.ReadSource(read.Extent);original[0]^=1;
        check(Hash(storage.ReadSource(read.Extent))==read.ExpectedSha256,"Original reader retains complete immutable authenticated bytes.");
        var foreign=new NativeAddressSpace(read.Extent.Space.Kind,read.Extent.Space.OwnerIdentity,NativeAddressRevision.Original,read.Extent.Space.ByteLength);
        Reject<InvalidDataException>(()=>storage.ReadSource(new(foreign,read.Extent.Offset,read.Extent.ByteLength)),check);
        Reject<InvalidDataException>(()=>storage.ReadSource(new(read.Extent.Space,read.Extent.Offset,read.Extent.ByteLength-1)),check);
    }
    private static int Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Expected tiled family Body refusal: "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}

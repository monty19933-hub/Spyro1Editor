using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

// Expected bytes/hashes come from the separately executed child-relative Python
// reference, never this new issuer. Source supplies actual existing R03 owners;
// no new Body/Scene/domain/EXE/disc or output file is constructed here.
internal static class TiledFamilyTerrainRevisionCases
{
    private const string UsedHash="fe754f8e5e4686fdfe03d7e516dab8f1b52ad605b1541615e98d3f0511e4835e";
    private const string PaddedHash="83baa79d7b1f9f27eadecb721f1026b0d0aea57cc65f0758b5353eaa0fc36fb6";
    private const string BeforeHash="d1b1b48eb310699a7579d3e7b5dcd5d3aa079f28236a4ad3029252aa22e96235";
    private const string MetadataHash="d36546bd44b8816a4b285afe9782dda0d1e46f8adf63f272412cb50cf9c77067";
    private const string TailHash="279df31a0ce9b325f2066754f78eda88cdf07a6be00c9d6dc01b240915f14f2c";
    private static readonly int[] MaterialWords=[532,548,692,708,852,868,1012,1028];
    private static readonly int[] FaceSlots=[0,0,0,1,0,1,1,1];

    internal static void Free(Action<bool,string> check)
    {
        var type=typeof(NativeFreshTiledFamilyTerrainRevision);
        check(type.IsSealed&&!type.IsPublic&&type.GetConstructors(BindingFlags.Public|BindingFlags.Instance).Length==0&&
            type.GetConstructors(BindingFlags.NonPublic|BindingFlags.Instance).All(c=>c.IsPrivate),
            "Detached terrain source revision has only private construction and no new public issuer.");
        Reject<ArgumentNullException>(()=>NativeFreshTiledFamilyTerrainRevision.Compose(null!,null!),check);
        var intent=NativeFreshTiledFamilyTerrainIntent.Create(TiledFamilyTerrainIntentCases.Document());
        Reject<ArgumentNullException>(()=>NativeFreshTiledFamilyTerrainRevision.Compose(intent,null!),check);
        check(type.GetProperty("FinalBodyDomain",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)==null&&
            type.GetMethod("CopyLevelData",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)==null,
            "A data-only revision cannot pretend to return an issued Body or Final Body domain.");
        Console.WriteLine("TILED_FAMILY_TERRAIN_REVISION_FREE_OK sourceOpened=false bodyIssued=false");
    }

    internal static NativeFreshTiledFamilyTerrainRevision Source(NativeFreshLockedChestPlacementPlan placement,Action<bool,string> check)
    {
        var intent=ForPlacement(placement);
        var document=intent.DocumentAncestor;
        var body=placement.BodyAncestor;var resources=body.ResourceAncestor;var host=body.HostAncestor;
        var mixed=resources.BaseBodyAncestor.MixedResourceAncestor!;
        byte[] beforeBody=placement.CopyLevelData(),beforeScene=placement.CopyScene();
        byte[] overlay=host.CopyOverlay(),initializer=host.CopyInitializer(),exe=host.CopyExecutablePrototype();
        int dataAt=Word(beforeBody,8),dataLength=Word(beforeBody,12);
        check(dataAt==0xE6000&&dataLength==0x1000&&Hash(beforeBody)=="2d8c1bf1af35c8e5522b239c564d36c79c828539d133278c461b6178b53bb504",
            "Actual retained R03 child has the independent whole-Body/data-descriptor identity.");
        byte[] before=beforeBody.AsSpan(dataAt,dataLength).ToArray();
        check(Hash(before)==BeforeHash&&Hash(before[..0xA6C])=="cc8d9503d511c780ee6e583f0109f106b8f634eb8ff58f07283b8b8790a0e05b",
            "Complete actual retained data allocation and used preimage match the independent child reference.");
        // Direct independent descriptor-bounded splice, not the production composer.
        byte[] expected=new byte[0xCB8];
        before.AsSpan(0,0x178).CopyTo(expected);
        intent.CopyBoundSurface().CopyTo(expected,0x178);
        before.AsSpan(0x2BC,0x7B0).CopyTo(expected.AsSpan(0x508));
        check(Hash(expected)==UsedHash,"Frozen full used-data reference binds the neutral surface and complete actual installed suffix.");
        byte[] allocation=new byte[0x1000];expected.CopyTo(allocation,0);
        check(Hash(allocation)==PaddedHash,"Complete independently zero-padded afterimage reference.");
        var revision=NativeFreshTiledFamilyTerrainRevision.Compose(intent,placement);
        revision.RequireAncestry(intent,placement);
        byte[] actual=revision.CopyUsedData();
        check(ReferenceEquals(revision.IntentAncestor,intent)&&ReferenceEquals(revision.PlacementAncestor,placement)&&
            ReferenceEquals(revision.BodyAncestor,body)&&ReferenceEquals(revision.ResourceAncestor,resources)&&ReferenceEquals(revision.HostAncestor,host)&&
            revision.MaterialSources.Count==2&&ReferenceEquals(revision.MaterialSources[0],mixed.MaterialSources[0])&&
            ReferenceEquals(revision.MaterialSources[1],mixed.MaterialSources[1]),
            "Revision retains its actual entire intent/placement/Body/resource/Host and ordered source instances.");
        check(revision.CanonicalDocumentSha256==intent.CanonicalDocumentSha256&&
            document.Placements.Select(p=>p.EditorId).SequenceEqual(body.SceneAncestor.RowSpans.Placements.Select(p=>p.EditorId))&&
            ReferenceEquals(((NativeFreshLockedChestFamilyPlacementIntent)document.Placements[2]).RewardDestinations,placement.RewardDestinationsAncestor),
            "Actual CLI IDs and override owner remain bound without coercion to the neutral oracle's different fixture IDs.");
        check(actual.SequenceEqual(expected)&&revision.DataSha256==UsedHash&&revision.PriorDataAllocationSha256==BeforeHash&&
            actual.Length==0xCB8&&Hash(actual[..0x178])==MetadataHash&&Hash(actual[0x508..])==TailHash,
            "Every used byte, complete metadata and complete translated family tail match independent references.");
        var records=NativeTerrainMaterialTableCodec.Decode(actual.AsSpan(0,0x178));
        check(records.Select(r=>r.RecordSha256).SequenceEqual(new[]{
            "27e844b704fc16372b0006639b0ea57553ece23e89c0ffd56383cddd1377c065",
            "f25d25b4df2cb6b5a633fb7397fe0d563b4af4601ce446438934edf687167c66"}),
            "Both native split records match the corrected independently frozen record hashes.");
        check(records.Count==2&&records.Select(r=>r.RecordSha256).SequenceEqual(mixed.Allocation.Records.Select(r=>r.RecordSha256))&&
            records.Zip(mixed.Allocation.Records).All(p=>p.First.CopyRecord().SequenceEqual(p.Second.CopyRecord())),
            "Native split LQ/HQ metadata retains both exact allocated records, not two contiguous184-byte slices.");
        for(int i=0;i<8;i++)check(Word(actual,MaterialWords[i])==FaceSlots[i]&&Word(actual,MaterialWords[i]+4)==128,
            "Each actual data-relative material word and following flag has its independent expected value.");
        check(actual.AsSpan(0,0x178).SequenceEqual(before.AsSpan(0,0x178))&&
            actual.AsSpan(0x508).SequenceEqual(before.AsSpan(0x2BC,0x7B0))&&
            before.AsSpan(0xA6C).IndexOfAnyExcept((byte)0)<0,
            "Complete metadata/suffix complements and old padding are retained without rebuilding a P0 tail.");
        var inverse=revision.RevertExactDataAllocation(allocation);
        check(inverse.Length==0x1000&&inverse.SequenceEqual(before)&&revision.RevertExactDataAllocation(allocation).SequenceEqual(before),
            "Complete immediate inverse restores the actual1000-byte allocation including old padding.");
        byte[] complement=beforeBody[..dataAt].Concat(beforeBody[(dataAt+dataLength)..]).ToArray();
        check(revision.RetainedBodySha256==Hash(beforeBody)&&revision.HeaderSha256==Hash(beforeBody[..0x800])&&
            revision.UploadSha256==Hash(Part(beforeBody,0))&&revision.ModelSha256==Hash(Part(beforeBody,2))&&
            revision.SceneSha256==Hash(beforeScene)&&revision.RetainedComplementSha256==Hash(complement)&&
            Part(beforeBody,2).Length==0x1B000&&Part(beforeBody,0).Length==0xE5800&&beforeScene.Length==0x3800,
            "Retained hashes cover the final actual chest child, not older mixed upload/model/Scene owners.");
        check(revision.SourceCertified&&!revision.BodyIssued&&!revision.RuntimeVerified&&!revision.PublicationApproved&&
            !revision.CompleteNativeDependencyClosureProven&&!revision.PeakRuntimeMemoryProven,
            "Source-owned detached data does not issue a Body, runtime-layout, peak or publication certificate.");
        var repeated=NativeFreshTiledFamilyTerrainRevision.Compose(intent,placement);
        check(repeated.PlanSha256==revision.PlanSha256&&repeated.CopyUsedData().SequenceEqual(expected),"Same actual tuple is deterministic.");
        var equalIntent=NativeFreshTiledFamilyTerrainIntent.Create(document);
        Reject<InvalidDataException>(()=>revision.RequireAncestry(equalIntent,placement),check);
        var foreignDocument=NativeFreshLevelDocumentCodec.Parse(intent.CopyCanonicalDocument());
        Reject<InvalidDataException>(()=>revision.RequireAncestry(NativeFreshTiledFamilyTerrainIntent.Create(foreignDocument),placement),check);
        Reject<InvalidDataException>(()=>NativeFreshTiledFamilyTerrainRevision.Compose(NativeFreshTiledFamilyTerrainIntent.Create(foreignDocument),placement),check);
        var equalPlacement=NativeFreshLockedChestPlacementPlan.ComposeWithRewardDestinations(body,placement.Intent,placement.RewardDestinationsAncestor!);
        check(equalPlacement.CopyLevelData().SequenceEqual(beforeBody),"Foreign placement control is byte-equal before identity refusal.");
        Reject<InvalidDataException>(()=>revision.RequireAncestry(intent,equalPlacement),check);
        var equalBody=NativeFreshLockedChestResearchBody.Compose(resources,host,body.SceneAncestor);
        var foreignBodyPlacement=NativeFreshLockedChestPlacementPlan.ComposeWithRewardDestinations(equalBody,placement.Intent,placement.RewardDestinationsAncestor!);
        check(equalBody.CopyLevelData().SequenceEqual(body.CopyLevelData())&&foreignBodyPlacement.CopyLevelData().SequenceEqual(beforeBody),
            "Foreign Body control retains identical complete bytes before actual ancestry refusal.");
        Reject<InvalidDataException>(()=>revision.RequireAncestry(intent,foreignBodyPlacement),check);
        Reject<ArgumentNullException>(()=>revision.RequireAncestry(null!,placement),check);
        Reject<ArgumentNullException>(()=>revision.RequireAncestry(intent,null!),check);
        Reject<ArgumentNullException>(()=>NativeFreshTiledFamilyTerrainRevision.Compose(null!,placement),check);
        var wrongId=document.WithPlacements(document.Placements.Select((p,i)=>i==0?
            NativeFreshWizardRouteIntent.Create(new('9',32),9984,9984,8704,9472,9984):p));
        Reject<InvalidDataException>(()=>NativeFreshTiledFamilyTerrainRevision.Compose(NativeFreshTiledFamilyTerrainIntent.Create(wrongId),placement),check);
        var family=(NativeFreshLockedChestFamilyPlacementIntent)document.Placements[2];
        var equalOverride=NativeFreshChestRewardDestinationOverrides.Create(placement.RewardDestinationsAncestor!.Values);
        var wrongOverride=document.WithPlacements(document.Placements.Take(2).Append(family.WithRewardDestinations(equalOverride)));
        Reject<InvalidDataException>(()=>NativeFreshTiledFamilyTerrainRevision.Compose(NativeFreshTiledFamilyTerrainIntent.Create(wrongOverride),placement),check);
        Reject<InvalidDataException>(()=>NativeFreshTiledFamilyTerrainRevision.Compose(intent,NativeFreshLockedChestPlacementPlan.Compose(body,placement.Intent)),check);
        foreach(int at in MaterialWords.Concat(MaterialWords.Select(at=>at+4)).Concat(new[]{0,4,8,0x18,0x28,0xD0,0x177,
            0x178,0x40F,0x410,0x433,0x434,0x43B,0x43C,0x4F7,0x4F8,0x507,0x508,0x50C,0x510,0x597,0x598,0x69C,0xCB7}))
        {
            byte[] corrupt=expected.ToArray();corrupt[at]^=1;
            Reject<InvalidDataException>(()=>revision.RequireExactUsedData(corrupt),check);
            byte[] full=allocation.ToArray();corrupt.CopyTo(full,0);
            Reject<InvalidDataException>(()=>revision.RevertExactDataAllocation(full),check);
        }
        foreach(int at in new[]{0xCB8,0xFFF})
        {byte[] corrupt=allocation.ToArray();corrupt[at]=1;Reject<InvalidDataException>(()=>revision.RevertExactDataAllocation(corrupt),check);}
        Reject<InvalidDataException>(()=>revision.RequireExactUsedData(expected[..^1]),check);
        Reject<InvalidDataException>(()=>revision.RequireExactUsedData(allocation),check);
        Reject<InvalidDataException>(()=>revision.RevertExactDataAllocation(expected),check);
        Reject<InvalidDataException>(()=>revision.RevertExactDataAllocation(allocation.Concat(new byte[4]).ToArray()),check);
        actual[0]^=1;inverse[^1]^=1;beforeBody[0]^=1;
        if(revision.MaterialSources is IList<NativeStaticTerrainTextureSource> sources)
            Reject<NotSupportedException>(()=>sources[0]=sources[1],check);
        check(revision.CopyUsedData().SequenceEqual(expected)&&revision.RevertExactDataAllocation(allocation).SequenceEqual(before)&&
            placement.CopyLevelData().AsSpan(dataAt,dataLength).SequenceEqual(before)&&placement.CopyScene().SequenceEqual(beforeScene)&&
            host.CopyOverlay().SequenceEqual(overlay)&&host.CopyInitializer().SequenceEqual(initializer)&&host.CopyExecutablePrototype().SequenceEqual(exe),
            "Mutated detached copies and refusals leave all retained source-derived owners unchanged.");
        check(!NativeFreshSavedLevelResearchEligibility.IsSupportedLockedChestProfile(document)&&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(document),"New source data revision does not widen saved/UI admission.");
        Console.WriteLine($"TILED_FAMILY_TERRAIN_REVISION_SOURCE_OK used={revision.DataSha256} inverse1000=true bodyIssued=false runtime=false publication=false");
        return revision;
    }

    private static NativeFreshTiledFamilyTerrainIntent ForPlacement(NativeFreshLockedChestPlacementPlan placement)
    {
        var document=TiledFamilyTerrainIntentCases.Document();var ids=placement.BodyAncestor.SceneAncestor.RowSpans.Placements;
        var grass=(NativeFreshMobyPlacementIntent)document.Placements[1];var family=(NativeFreshLockedChestFamilyPlacementIntent)document.Placements[2];
        document=document.WithPlacements([
            NativeFreshWizardRouteIntent.Create(ids[0].EditorId,9984,9984,8704,9472,9984),
            NativeFreshMobyPlacementIntent.Create(ids[1].EditorId,grass.Source,9152,9152,8704,64),
            NativeFreshLockedChestFamilyPlacementIntent.Create(ids[2].EditorId,family.Source,9728,9120,8704,10864,9888,8704,
                placement.RewardDestinationsAncestor)]);
        return NativeFreshTiledFamilyTerrainIntent.Create(document);
    }
    private static byte[] Part(byte[] body,int index)=>body.AsSpan(Word(body,index*8),Word(body,index*8+4)).ToArray();
    private static int Word(byte[] bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at));
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Expected detached revision refusal: "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}

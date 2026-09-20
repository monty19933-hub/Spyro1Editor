using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

// Independent literal afterimages, not the production override transform.
// R03 changes only Reward2's authored destination, leaving the R02 anchors,
// finite floor, native code and every other actor/destination untouched.
internal static class AuthoredChestDestinationCases
{
    internal static NativeFreshLockedChestPlacementPlan Source(
        NativeFreshLockedChestResearchBody body,Action<bool,string> check)
    {
        ArgumentNullException.ThrowIfNull(body);ArgumentNullException.ThrowIfNull(check);
        var anchors=new NativeFreshLockedChestPlacementIntent(9728,9120,8704,10864,9888,8704);
        var old=NativeFreshLockedChestPlacementPlan.Compose(body,anchors);
        check(old.RewardDestinationsAncestor is null&&
            old.SceneSha256=="6cda54db346a3fc7a4ae4338c885828d3a4759b4767872038b43bae3ede50092"&&
            old.LevelDataSha256=="ada9d0894a3c50bac54533f9eed6c006af325964fef73697a61a437a4b4332a8",
            "No override preserves the frozen R02 scene/Body and its inherited source-relative pattern.");
        var values=new[]{new NativeFreshChestRewardDestinationOverride(NativeFreshChestRewardRole.Reward2,10454,10503,9216)};
        var authored=NativeFreshChestRewardDestinationOverrides.Create(values);
        var placement=NativeFreshLockedChestPlacementPlan.ComposeWithRewardDestinations(body,anchors,authored);
        placement.RequireAncestry(body,anchors);placement.RequireRewardDestinationsAncestry(authored);
        byte[] originalScene=body.SceneAncestor.CopyScene(),originalBody=body.CopyLevelData();
        // All seven final XYZ triples are independently literal. The duplicate
        // Reward2/Reward4 destination is intentional; their native owners,
        // selectors, delay/ordinal and spawn identities remain distinct.
        (int X,int Y,int Z)[] points=[(9728,9120,8704),(10864,9888,8704),(10454,11117,9216),
            (10454,10503,9216),(10249,10093,9216),(10454,10503,9216),(11068,10707,9216)];
        int[] privates=[0x12A0,0x12A4,0x12BC,0x12E4,0x130C,0x1334,0x135C];
        byte[] expected=originalScene.ToArray();List<int> allFields=[];
        for(int role=0;role<7;role++)
        {
            int[] xyz=[points[role].X,points[role].Y,points[role].Z];
            for(int axis=0;axis<3;axis++)
            {
                int at=0x184+role*0x58+12+axis*4;
                Put(expected,at,xyz[axis]);allFields.Add(at);
                if(role>=2){at=privates[role]+4+axis*4;Put(expected,at,xyz[axis]);allFields.Add(at);}
            }
        }
        byte[] actual=placement.CopyScene(),actualBody=placement.CopyLevelData(),expectedBody=originalBody.ToArray();
        expected.CopyTo(expectedBody,0x102000);
        check(actual.SequenceEqual(expected)&&actualBody.SequenceEqual(expectedBody)&&
            placement.SceneSha256==Hash(expected)&&placement.LevelDataSha256==Hash(expectedBody)&&
            placement.CoordinateWrites.Count==36&&allFields.Distinct().Count()==36&&
            placement.CoordinateWrites.Select(w=>w.SceneOffset).Order().SequenceEqual(allFields.Order())&&
            placement.CoordinateWrites.All(w=>w.Before==Word(originalScene,w.SceneOffset)&&w.After==Word(expected,w.SceneOffset)),
            "Authored placement is exactly36 independently predicted source-to-output coordinate words and complete protected complements.");
        byte[] r02=old.CopyScene(),relativeExpected=r02.ToArray();
        // Reward2 is native placed ordinal5, not role2/row2. Two representations
        // of its world destination change; Z remains9216 in both.
        int[] changedWords=[0x298,0x29C,0x12E8,0x12EC];
        foreach(int at in changedWords)Put(relativeExpected,at,at is 0x298 or 0x12E8?10454:10503);
        check(actual.SequenceEqual(relativeExpected)&&
            changedWords.All(at=>Word(actual,at)!=Word(r02,at))&&
            Word(actual,0x2A0)==9216&&Word(actual,0x12F0)==9216&&
            actual.AsSpan(0x28C+0x20,0x38).SequenceEqual(r02.AsSpan(0x28C+0x20,0x38))&&
            actual.AsSpan(0x12E4,4).SequenceEqual(r02.AsSpan(0x12E4,4))&&
            actual.AsSpan(0x12F4,0x18).SequenceEqual(r02.AsSpan(0x12F4,0x18)),
            "Relative to R02 only four words differ; Reward2 class/selector, parent index, states, delay and ordinal are untouched.");
        check(placement.RevertExactScene(actual).SequenceEqual(originalScene)&&
            placement.RevertExactLevelData(actualBody).SequenceEqual(originalBody)&&
            body.RevertExactLevelData(placement.RevertExactLevelData(actualBody)).SequenceEqual(body.ResourceAncestor.CopyLevelData()),
            "Authored destination retains exact scene/Body/full resource-parent inverses, not an R02 payload substituted as source.");
        var clear=NativeFreshLockedChestSpawnClearancePlan.Compose(placement);
        clear.RequireStaticLandingClearance(placement);
        var oldClear=NativeFreshLockedChestSpawnClearancePlan.Compose(old);
        check(clear.InitialFinding.Spawn==oldClear.InitialFinding.Spawn&&clear.InitialFinding.Centre==oldClear.InitialFinding.Centre&&
            clear.InitialFinding.DistanceSquared==oldClear.InitialFinding.DistanceSquared&&
            clear.InitialFinding.Status==oldClear.InitialFinding.Status&&clear.InitialFinding.UnknownReason==oldClear.InitialFinding.UnknownReason&&
            clear.HorizontalDistanceSquared==oldClear.HorizontalDistanceSquared&&
            clear.ChestMargins==oldClear.ChestMargins&&clear.PlayerMargins==oldClear.PlayerMargins&&
            placement.MinimumCentreBoundaryMargin==403&&
            ReferenceEquals(placement.RewardDestinationsAncestor,authored)&&placement.PlanSha256!=old.PlanSha256&&
            !placement.RuntimeVerified&&!placement.PublicationApproved&&!placement.FootprintClearanceProven&&
            !clear.TrajectoryProven&&!clear.WholeFamilyFootprintClearanceProven,
            "Same real anchors retain static spawn margins; explicit authored identity adds no trajectory, native lifecycle or publication claim.");
        var repeated=NativeFreshLockedChestPlacementPlan.ComposeWithRewardDestinations(body,anchors,authored);
        check(repeated.PlanSha256==placement.PlanSha256&&repeated.CopyLevelData().SequenceEqual(expectedBody),
            "Same retained authored inputs are deterministic.");
        var equal=NativeFreshChestRewardDestinationOverrides.Create(values);
        Reject<InvalidDataException>(()=>placement.RequireRewardDestinationsAncestry(equal),check);
        Reject<InvalidDataException>(()=>old.RequireRewardDestinationsAncestry(authored),check);
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestPlacementPlan.ComposeWithRewardDestinations(body,anchors,null!),check);
        foreach(var wrong in new[]{
            new NativeFreshChestRewardDestinationOverride(NativeFreshChestRewardRole.Reward2,8448,10093,9216),
            new NativeFreshChestRewardDestinationOverride(NativeFreshChestRewardRole.Reward2,10249,11520,9216),
            new NativeFreshChestRewardDestinationOverride(NativeFreshChestRewardRole.Reward2,10249,10093,8703),
            new NativeFreshChestRewardDestinationOverride(NativeFreshChestRewardRole.Reward2,10249,10093,9217)})
            Reject<InvalidDataException>(()=>NativeFreshLockedChestPlacementPlan.ComposeWithRewardDestinations(body,anchors,
                NativeFreshChestRewardDestinationOverrides.Create([wrong])),check);
        var moved=NativeFreshLockedChestPlacementPlan.ComposeWithRewardDestinations(body,anchors with{ChestX=10848},authored);
        check(Word(moved.CopyScene(),0x298)==10454&&Word(moved.CopyScene(),0x12E8)==10454&&
            Word(moved.CopyScene(),0x240)==10438&&Word(moved.CopyScene(),0x12C0)==10438,
            "Explicit world destination stays fixed when the chest moves; inherited Reward1 remains source-relative.");
        foreach(int at in changedWords.Concat(new[]{0x2DF,0x12E4,0x12F8,0x1300,0x3390}))
        {byte[] corrupt=actual.ToArray();corrupt[at]^=1;Reject<InvalidDataException>(()=>placement.RevertExactScene(corrupt),check);}
        values[0]=new(NativeFreshChestRewardRole.Reward2,0,0,0);actual[0]^=1;actualBody[^1]^=1;
        check(authored.Values.Single()==new NativeFreshChestRewardDestinationOverride(NativeFreshChestRewardRole.Reward2,10454,10503,9216)&&
            placement.CopyScene().SequenceEqual(expected)&&placement.CopyLevelData().SequenceEqual(expectedBody)&&
            old.CopyScene().SequenceEqual(r02)&&body.CopyLevelData().SequenceEqual(originalBody),
            "Mutable input/copy changes and refusals cannot rewrite authored owners or the preserved R02/source ancestors.");
        Console.WriteLine($"AUTHORED_CHEST_DESTINATION_SOURCE_OK scene={Hash(expected)} body={Hash(expectedBody)} changedFromR02=4Words runtime=false");
        return placement;
    }
    private static int Word(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
    private static void Put(Span<byte> b,int at,int value)=>BinaryPrimitives.WriteInt32LittleEndian(b[at..],value);
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Authored destination refused "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}

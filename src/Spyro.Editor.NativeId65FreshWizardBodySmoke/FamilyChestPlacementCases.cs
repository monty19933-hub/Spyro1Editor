using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

internal static class FamilyChestPlacementCases
{
    internal static void Free(Action<bool,string> check)
    {
        var type=typeof(NativeFreshLockedChestPlacementPlan);
        check(!type.IsPublic&&type.IsSealed&&type.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
            .All(c=>c.IsPrivate),"Chest placement is a closed internal scene/Body transaction, not a public export gate.");
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestPlacementPlan.Compose(null!,null!),check);
        RewardDestinationFree(check);
        FamilyChestSpawnClearanceCases.Free(check);
        FamilyChestExecutableCases.Free(check);
    }

    private static void RewardDestinationFree(Action<bool,string> check)
    {
        var type=typeof(NativeFreshChestRewardDestinationOverrides);
        check(!type.IsPublic&&type.IsSealed&&type.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
            .All(c=>c.IsPrivate)&&type.GetProperties(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
            .All(p=>p.SetMethod==null),"Reward overrides are a closed immutable internal neutral owner, not a public source/export capability.");
        check(Enum.GetValues<NativeFreshChestRewardRole>().Select(v=>(int)v).SequenceEqual(new[]{0,1,2,3,4})&&
            Enum.GetNames<NativeFreshChestRewardRole>().SequenceEqual(new[]{"Reward1","Reward2","Reward3","Reward4","Reward5"}),
            "Exactly five explicit semantic D-child roles, not raw scene ordinals or an open integer role.");
        for(int count=1;count<=5;count++)
        {
            var input=Enumerable.Range(0,count).Reverse().Select(i=>new NativeFreshChestRewardDestinationOverride(
                (NativeFreshChestRewardRole)i,0,262128,1)).ToArray();
            var captured=NativeFreshChestRewardDestinationOverrides.Create(input);
            check(captured.Values.Count==count&&captured.Values.Select(v=>(int)v.Role).SequenceEqual(Enumerable.Range(0,count))&&
                captured.Values.All(v=>v.X==0&&v.Y==262128&&v.Z==1),
                "One through five roles are sorted into detached WORLD coordinates; neutral capture adds no grid/floor/trajectory authority.");
            input[0]=new(NativeFreshChestRewardRole.Reward5,100,200,300);
            check(captured.Values.All(v=>v.X==0&&v.Y==262128&&v.Z==1),"Mutating caller slots cannot change captured values.");
            var writable=(IList<NativeFreshChestRewardDestinationOverride>)captured.Values;
            Reject<NotSupportedException>(()=>writable[0]=new(NativeFreshChestRewardRole.Reward1,5,6,7),check);
            var copy=captured.Values[0] with{X=123};
            check(copy.X==123&&captured.Values[0].X==0,"Record copies cannot mutate retained destinations.");
        }
        Reject<ArgumentNullException>(()=>NativeFreshChestRewardDestinationOverrides.Create(null!),check);
        Reject<InvalidDataException>(()=>NativeFreshChestRewardDestinationOverrides.Create([]),check);
        Reject<InvalidDataException>(()=>NativeFreshChestRewardDestinationOverrides.Create([null!]),check);
        Reject<InvalidDataException>(()=>NativeFreshChestRewardDestinationOverrides.Create(Enumerable.Range(0,6)
            .Select(i=>new NativeFreshChestRewardDestinationOverride((NativeFreshChestRewardRole)(i%5),0,0,0)).ToArray()),check);
        foreach(int role in new[]{-1,5,int.MinValue,int.MaxValue})
            Reject<InvalidDataException>(()=>NativeFreshChestRewardDestinationOverrides.Create([new((NativeFreshChestRewardRole)role,0,0,0)]),check);
        foreach(var second in new[]{new NativeFreshChestRewardDestinationOverride(NativeFreshChestRewardRole.Reward1,0,0,0),
            new NativeFreshChestRewardDestinationOverride(NativeFreshChestRewardRole.Reward1,1,2,3)})
            Reject<InvalidDataException>(()=>NativeFreshChestRewardDestinationOverrides.Create([new(NativeFreshChestRewardRole.Reward1,0,0,0),second]),check);
        foreach(var (x,y,z) in new[]{(-1,0,0),(262129,0,0),(0,-1,0),(0,262129,0),(0,0,-1),(0,0,262129),(int.MinValue,0,0),(0,int.MaxValue,0)})
            Reject<InvalidDataException>(()=>NativeFreshChestRewardDestinationOverrides.Create([new(NativeFreshChestRewardRole.Reward1,x,y,z)]),check);
        var valid=NativeFreshChestRewardDestinationOverrides.Create([new(NativeFreshChestRewardRole.Reward3,10240,10240,9216)]);
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestPlacementPlan.ComposeWithRewardDestinations(null!,null!,null!),check);
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestPlacementPlan.ComposeWithRewardDestinations(null!,
            new(8960,9728,8704,9472,8960,8704),valid),check);
    }

    internal static void Source(NativeFreshLockedChestResearchBody body,Action<bool,string> check)
    {
        var intent=new NativeFreshLockedChestPlacementIntent(8960,9728,8704,9472,8960,8704);
        byte[] priorBody=body.CopyLevelData(),priorScene=body.SceneAncestor.CopyScene();
        var placement=NativeFreshLockedChestPlacementPlan.Compose(body,intent);
        FamilyChestSpawnClearanceCases.Source(placement,check);
        FamilyChestExecutableCases.Source(placement,check);
        placement.RequireAncestry(body,intent);
        check(ReferenceEquals(body,placement.BodyAncestor)&&ReferenceEquals(intent,placement.Intent)&&
            placement.SourceCertified&&placement.AuthoredCoordinatesProven&&placement.CentreContainmentProven&&
            !placement.FootprintClearanceProven&&!placement.RuntimeVerified&&!placement.PublicationApproved&&
            !placement.FinalExecutableIssued&&!placement.CompleteNativeBehaviorProven,
            "Actual two-anchor authorship proves only source-owned transforms and centre containment, not physical safety or native behavior.");
        // Child-relative independent expectation: pinned460 source P9 plus
        // literal final coordinates, not the production relative transform.
        int[][] coordinates=[[8960,9728,8704],[9472,8960,8704],[9062,10189,9216],
            [9472,10189,9216],[8857,9165,9216],[9062,9575,9216],[9676,9779,9216]];
        int[] properties=[0x12A0,0x12A4,0x12BC,0x12E4,0x130C,0x1334,0x135C];
        byte[] expected=priorScene.ToArray();List<int> fields=[];
        for(int i=0;i<7;i++)for(int axis=0;axis<3;axis++)
        {
            int row=0x184+i*0x58+12+axis*4;
            Put(expected,row,coordinates[i][axis]);fields.Add(row);
            if(i>=2){int field=properties[i]+4+axis*4;Put(expected,field,coordinates[i][axis]);fields.Add(field);}
        }
        byte[] actual=placement.CopyScene(),actualBody=placement.CopyLevelData(),expectedBody=priorBody.ToArray();
        expected.CopyTo(expectedBody,0x102000);
        check(actual.SequenceEqual(expected)&&actualBody.SequenceEqual(expectedBody)&&
            placement.SceneSha256==Hash(expected)&&placement.LevelDataSha256==Hash(expectedBody),
            "All seven row and five private XYZ owners match independent literal positions in the complete scene and Body.");
        check(Hash(actual)=="075c84cfd5ec13dc245dfa8b888f349903056b07676fc1990c494875e90f20d3",
            "Complete placed scene equals frozen original-only reference480, not only the child-relative direct-copy expectation.");
        check(placement.CoordinateWrites.Count==36&&placement.CoordinateWrites.Select(w=>w.SceneOffset).Order().SequenceEqual(fields.Order())&&
            placement.CoordinateWrites.All(w=>w.Before==Word(priorScene,w.SceneOffset)&&w.After==Word(expected,w.SceneOffset)),
            "Exactly36 disjoint coordinate-word preimages and afterimages are retained, not a broad seven-row replacement policy.");
        foreach(int i in Enumerable.Range(0,actual.Length))
            if(!fields.Any(at=>i>=at&&i<at+4)&&actual[i]!=priorScene[i])
                throw new InvalidDataException("Unexpected non-coordinate byte mutation at "+i);
        check(actual.AsSpan(0,0x184).SequenceEqual(priorScene.AsSpan(0,0x184))&&
            actual.AsSpan(0x1248,0x58).SequenceEqual(priorScene.AsSpan(0x1248,0x58))&&
            actual.AsSpan(0x3390).SequenceEqual(priorScene.AsSpan(0x3390)),
            "Complete Wizard/Grass, ten fixups and trailing padding survive placement.");
        for(int i=0;i<7;i++)
        {
            int row=0x184+i*0x58;
            check(actual.AsSpan(row+0x20,0x38).SequenceEqual(priorScene.AsSpan(row+0x20,0x38))&&
                Word(actual,properties[i])==Word(priorScene,properties[i]),
                "Class, matrix, yaw, selector, private initialization and integer owner are not transformed as coordinates.");
        }
        check(actualBody.AsSpan(0,0x102000).SequenceEqual(priorBody.AsSpan(0,0x102000))&&
            actualBody.AsSpan(0x105800).SequenceEqual(priorBody.AsSpan(0x105800)),
            "Complete body header/resource/model/auxiliary complement is retained.");
        check(placement.MinimumCentreBoundaryMargin==307&&placement.RevertExactScene(actual).SequenceEqual(priorScene)&&
            placement.RevertExactLevelData(actualBody).SequenceEqual(priorBody),
            "All family centres lie inside the existing floor, minimum307; full immediate scene/Body inverses are exact.");
        check(body.RevertExactLevelData(placement.RevertExactLevelData(actualBody)).SequenceEqual(body.ResourceAncestor.CopyLevelData())&&
            body.SceneAncestor.RevertExactScene(placement.RevertExactScene(actual)).SequenceEqual(body.ResourceAncestor.CopyScene()),
            "Placement inverse composes through both existing actual resource-child and P2 scene ancestors.");
        var repeat=NativeFreshLockedChestPlacementPlan.Compose(body,intent);
        check(repeat.PlanSha256==placement.PlanSha256&&repeat.CopyLevelData().SequenceEqual(actualBody),
            "Same actual input is deterministic without accumulating coordinate edits.");
        var equalIntent=intent with{};
        Reject<InvalidDataException>(()=>placement.RequireAncestry(body,equalIntent),check);
        var equalBody=NativeFreshLockedChestResearchBody.Compose(body.ResourceAncestor,body.HostAncestor,body.SceneAncestor);
        Reject<InvalidDataException>(()=>placement.RequireAncestry(equalBody,intent),check);
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestPlacementPlan.Compose(null!,intent),check);
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestPlacementPlan.Compose(body,null!),check);
        foreach(var bad in new[]{intent with{KeyX=8448},intent with{KeyY=10496},intent with{KeyZ=8720},
            intent with{ChestX=8448+512},intent with{ChestY=9472},intent with{ChestZ=9216},
            intent with{KeyX=8961},intent with{ChestX=int.MaxValue},intent with{KeyY=int.MinValue},
            intent with{KeyX=9472,KeyY=8960}})
            Reject<InvalidDataException>(()=>NativeFreshLockedChestPlacementPlan.Compose(body,bad),check);
        foreach(int at in fields.Concat(new[]{0,0xD0,0xD4,0x3EC,0x12A0,0x12A4,0x3390,0x37FF}).Distinct())
        {
            byte[] bad=actual.ToArray();bad[at]^=1;
            Reject<InvalidDataException>(()=>placement.RequireExactScene(bad),check);
            Reject<InvalidDataException>(()=>placement.RevertExactScene(bad),check);
            byte[] badBody=actualBody.ToArray();bad.CopyTo(badBody,0x102000);
            Reject<InvalidDataException>(()=>placement.RequireExactLevelData(badBody),check);
        }
        foreach(int at in new[]{0,0x50,0xE6000,0xE7000,0x101FFF,0x105800,actualBody.Length-1})
        {byte[] bad=actualBody.ToArray();bad[at]^=1;Reject<InvalidDataException>(()=>placement.RevertExactLevelData(bad),check);}
        foreach(byte[] bad in new byte[][]{[],actual[..^1],[..actual,(byte)0],priorScene})
            Reject<InvalidDataException>(()=>placement.RequireExactScene(bad),check);
        foreach(byte[] bad in new byte[][]{[],actualBody[..^1],[..actualBody,(byte)0],priorBody})
            Reject<InvalidDataException>(()=>placement.RequireExactLevelData(bad),check);
        actual[0]^=1;actualBody[0]^=1;
        check(placement.CopyScene().SequenceEqual(expected)&&placement.CopyLevelData().SequenceEqual(expectedBody)&&
            body.CopyLevelData().SequenceEqual(priorBody)&&body.SceneAncestor.CopyScene().SequenceEqual(priorScene),
            "Mutated copies and refusals preserve all immediate immutable owners.");
        Console.WriteLine($"NATIVE_FRESH_CHEST_PLACEMENT_SOURCE_OK scene={Hash(expected)} body={Hash(expectedBody)} centreMargin=307 clearance=false runtime=false");
    }
    private static int Word(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
    private static void Put(Span<byte> b,int at,int value)=>BinaryPrimitives.WriteInt32LittleEndian(b[at..],value);
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Chest placement refused "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}

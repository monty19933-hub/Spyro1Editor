using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

internal static class WideChestPlacementCases
{
    internal static void Source(NativeFreshLockedChestResearchBody body,Action<bool,string> check)
    {
        ArgumentNullException.ThrowIfNull(body);ArgumentNullException.ThrowIfNull(check);
        var floor=body.ResourceAncestor.BaseBodyAncestor.HostAncestor.SceneAncestor.CompilationAncestor.Intent;
        var source=body.HostAncestor.FamilySourceAncestor;
        body.RequireAncestry(body.ResourceAncestor,body.HostAncestor,body.SceneAncestor);
        source.RequireRetainedOwners();
        byte[] priorBody=body.CopyLevelData(),priorScene=body.SceneAncestor.CopyScene(),package=source.CopyPackage();
        byte[][] sourceRows=source.PrivateRows.Select(p=>source.ReadSource(p.Row.Extent)).ToArray();
        byte[][] sourcePrivate=source.PrivateRows.Select(p=>source.ReadSource(p.Properties.Extent)).ToArray();
        check(floor.FloorX==8448&&floor.FloorY==8448&&floor.FloorZ==8704&&floor.FloorWidth==3072&&floor.FloorDepth==3072&&
            floor.SpawnX==8960&&floor.SpawnY==8960&&floor.SpawnZ==9216&&sourceRows.Length==7&&sourcePrivate.Length==7,
            "Wide placement consumes the actual3072 square and unchanged authored spawn, not an enlarged display rectangle or substitute Body.");
        check(priorScene.Length==0x3800&&priorBody.Length==0x20F000&&Word(priorBody,0x18)==0x102000&&
            Word(priorBody,0x1C)==0x3800&&priorBody.AsSpan(0x102000,0x3800).SequenceEqual(priorScene)&&
            Hash(priorScene)=="cb881f39cb6ea5bf6bd47cd23564474488dac15d565f403e3591e44ea720a83c",
            "The independently frozen source-coordinate P9 scene remains byte-identical despite the actual wider floor geometry.");
        check(Xyz(priorScene,0)==(8960,8960,9216)&&Xyz(priorScene,0xE0)==(9984,9984,8704)&&
            Word(priorScene,0x1288)==9472&&Word(priorScene,0x128C)==9984&&Xyz(priorScene,0x138)==(9152,9152,8704),
            "Actual source-coordinate P9 retains the spawn, Wizard9984/9984 to9472/9984 route and Grass9152/9152.");

        // Independent literals from frozen original-only placement oracle480:
        // compare all actual source row/private preimages before composing.
        (int X,int Y,int Z)[] deltas=[(-410,1229,512),(0,1229,512),(-615,205,512),(-410,615,512),(204,819,512)];
        (int X,int Y,int Z)[] positions=[(9728,9120,8704),(10864,9888,8704),(10454,11117,9216),
            (10864,11117,9216),(10249,10093,9216),(10454,10503,9216),(11068,10707,9216)];
        int[] properties=[0x12A0,0x12A4,0x12BC,0x12E4,0x130C,0x1334,0x135C];
        var oldChest=Xyz(sourceRows[1],12);
        for(int i=0;i<7;i++)
        {
            int row=0x184+i*0x58;
            check(Xyz(priorScene,row+12)==Xyz(sourceRows[i],12)&&Word(priorScene,row)==properties[i]&&
                BinaryPrimitives.ReadUInt16LittleEndian(priorScene.AsSpan(row+0x36))==(i==0?0xAD:i==1?0xAE:0xD),
                "Every wide-placement preimage is the corresponding actual source-coordinate class and private owner.");
            if(i<2)continue;
            var point=Xyz(sourceRows[i],12);var delta=deltas[i-2];
            check((point.X-oldChest.X,point.Y-oldChest.Y,point.Z-oldChest.Z)==delta&&
                Xyz(sourcePrivate[i],4)==point&&Xyz(priorScene,properties[i]+4)==point&&
                positions[i]==(10864+delta.X,9888+delta.Y,8704+delta.Z),
                "Literal final reward XYZ independently preserves its actual source row/private relative arrangement.");
        }

        var intent=new NativeFreshLockedChestPlacementIntent(9728,9120,8704,10864,9888,8704);
        // Use the existing actual placement producer, never a test-only
        // widened projector or a substitute source-coordinate document.
        var placement=NativeFreshLockedChestPlacementPlan.Compose(body,intent);
        placement.RequireAncestry(body,intent);
        byte[] expected=priorScene.ToArray();List<int> fields=[];
        for(int i=0;i<7;i++)
        {
            int[] point=[positions[i].X,positions[i].Y,positions[i].Z];
            for(int axis=0;axis<3;axis++)
            {
                int at=0x184+i*0x58+12+axis*4;Put(expected,at,point[axis]);fields.Add(at);
                if(i>=2){at=properties[i]+4+axis*4;Put(expected,at,point[axis]);fields.Add(at);}
            }
        }
        byte[] actual=placement.CopyScene(),actualBody=placement.CopyLevelData(),expectedBody=priorBody.ToArray();
        expected.CopyTo(expectedBody,0x102000);
        check(fields.Count==36&&fields.Distinct().Count()==36&&actual.SequenceEqual(expected)&&actualBody.SequenceEqual(expectedBody)&&
            placement.SceneSha256==Hash(expected)&&placement.LevelDataSha256==Hash(expectedBody)&&
            placement.CoordinateWrites.Count==36&&placement.CoordinateWrites.Select(w=>w.SceneOffset).Order().SequenceEqual(fields.Order())&&
            placement.CoordinateWrites.All(w=>w.Before==Word(priorScene,w.SceneOffset)&&w.After==Word(expected,w.SceneOffset)&&w.Before!=w.After),
            "Complete scene and Body equal exactly36 independent literal XYZ writes, including every explicit preimage/afterimage.");
        byte[] inverse=actual.ToArray();foreach(int at in fields)Put(inverse,at,Word(priorScene,at));
        check(inverse.SequenceEqual(priorScene)&&placement.RevertExactScene(actual).SequenceEqual(priorScene)&&
            placement.RevertExactLevelData(actualBody).SequenceEqual(priorBody)&&
            body.RevertExactLevelData(placement.RevertExactLevelData(actualBody)).SequenceEqual(body.ResourceAncestor.CopyLevelData())&&
            body.SceneAncestor.RevertExactScene(inverse).SequenceEqual(body.ResourceAncestor.CopyScene()),
            "Literal and production complete inverses preserve every non-coordinate byte and compose through the actual resource and P2 owners.");
        check(actual.AsSpan(0,0x184).SequenceEqual(priorScene.AsSpan(0,0x184))&&
            actual.AsSpan(0x1248,0x58).SequenceEqual(priorScene.AsSpan(0x1248,0x58))&&
            actual.AsSpan(0x3390).SequenceEqual(priorScene.AsSpan(0x3390))&&
            actualBody.AsSpan(0,0x102000).SequenceEqual(priorBody.AsSpan(0,0x102000))&&
            actualBody.AsSpan(0x105800).SequenceEqual(priorBody.AsSpan(0x105800)),
            "Actual Wizard/Grass rows and privates, pointer fixups, padding, geometry, models, upload and Body complement are untouched.");

        var clearance=NativeFreshLockedChestSpawnClearancePlan.Compose(placement);
        clearance.RequireAncestry(placement);clearance.RequireStaticLandingClearance(placement);
        check(ReferenceEquals(clearance.PlacementAncestor,placement)&&ReferenceEquals(clearance.BodyAncestor,body)&&
            ReferenceEquals(clearance.HostAncestor,body.HostAncestor)&&clearance.SourceCertified&&
            clearance.InitialFinding.Spawn==new NativeSpawnClearancePoint(8960,8960,9216)&&
            clearance.InitialFinding.Centre==new NativeSpawnClearancePoint(10864,9888,9040)&&
            clearance.InitialFinding.ColliderRadius==600&&clearance.InitialFinding.PlayerRadius==356&&
            clearance.InitialFinding.Vertex is null&&clearance.InitialFinding.IncomingDamageFlags==4&&clearance.InitialFinding.ResponsePermitted==true&&
            clearance.InitialFinding.UnknownReason==NativeInitialSpawnUnknownReason.None&&
            clearance.InitialFinding.Status==NativeInitialSpawnClearanceStatus.InitialSeparation&&
            clearance.InitialFinding.DistanceSquared==(BigInteger)4517376&&clearance.HorizontalDistanceSquared==(BigInteger)4486400&&
            clearance.CombinedRadiusSquared==(BigInteger)913936&&clearance.LandingColumnStatus==NativeInitialSpawnClearanceStatus.InitialSeparation,
            "Actual source-owned AE type1/default-player assessment strictly separates initial3D and fixed-XY landing column on the wider floor.");
        check(clearance.ChestMargins.Left==2416&&clearance.ChestMargins.Right==656&&clearance.ChestMargins.Top==1440&&
            clearance.ChestMargins.Bottom==1632&&clearance.ChestMargins.Right-600==56&&clearance.ChestMargins.StrictlyContains(600)&&
            clearance.PlayerMargins.Left==512&&clearance.PlayerMargins.Right==2560&&clearance.PlayerMargins.Top==512&&
            clearance.PlayerMargins.Bottom==2560&&clearance.PlayerMargins.StrictlyContains(356),
            "Chest right-edge disk clearance is56 and the initial player's minimum centre margin is512, using the actual finite3072 floor.");
        int rewardMargin=positions.Skip(2).Min(p=>new[]{p.X-8448,11520-p.X,p.Y-8448,11520-p.Y}.Min());
        check(positions.All(p=>p.X>8448&&p.X<11520&&p.Y>8448&&p.Y<11520&&p.Z>=8704&&p.Z<=9216)&&
            rewardMargin==403&&rewardMargin-356==47&&placement.MinimumCentreBoundaryMargin==403,
            "All seven authored centres fit; advisory356-radius XY disks around the five reward-controller centres have minimum47, not emitted-gem trajectory proof.");
        AdvisoryPath(actual,check);
        check(ReferenceEquals(placement.BodyAncestor,body)&&ReferenceEquals(placement.Intent,intent)&&placement.SourceCertified&&
            placement.AuthoredCoordinatesProven&&placement.CentreContainmentProven&&!placement.FootprintClearanceProven&&
            !placement.CompleteNativeBehaviorProven&&!placement.FinalExecutableIssued&&!placement.RuntimeVerified&&!placement.PublicationApproved&&
            !clearance.RuntimeVerified&&!clearance.FirstQueryProven&&!clearance.TrajectoryProven&&!clearance.WholeFamilyFootprintClearanceProven&&
            !clearance.PublicationApproved&&!body.FinalExecutableIssued&&!body.RuntimeVerified&&!body.PublicationApproved,
            "Static source-owned assessment and advisory navigation do not grant trajectory, first-query, native behavior, whole-family, EXE or publication authority.");

        var repeat=NativeFreshLockedChestPlacementPlan.Compose(body,intent);
        check(repeat.PlanSha256==placement.PlanSha256&&repeat.CopyLevelData().SequenceEqual(expectedBody)&&
            NativeFreshLockedChestSpawnClearancePlan.Compose(repeat).PlanSha256==clearance.PlanSha256,
            "The same actual wider input is deterministic without accumulating placement edits.");
        Reject<InvalidDataException>(()=>clearance.RequireAncestry(repeat),check);
        Reject<InvalidDataException>(()=>placement.RequireAncestry(body,intent with{}),check);
        foreach(var bad in new[]{intent with{ChestX=11520},intent with{ChestY=10304},intent with{KeyX=8448},
            intent with{KeyZ=8720},intent with{ChestX=10865},intent with{KeyX=int.MaxValue}})
            Reject<InvalidDataException>(()=>NativeFreshLockedChestPlacementPlan.Compose(body,bad),check);
        foreach(int at in new[]{fields[0],fields[^1],0x1DC+0x46,0x12A4,0x3390,actual.Length-1})
        {
            byte[] bad=actual.ToArray();bad[at]^=1;
            Reject<InvalidDataException>(()=>placement.RevertExactScene(bad),check);
        }
        byte[] brokenBody=actualBody.ToArray();brokenBody[0xE6000]^=1;
        Reject<InvalidDataException>(()=>placement.RevertExactLevelData(brokenBody),check);
        actual[0]^=1;actualBody[0]^=1;sourceRows[0][0]^=1;package[0]^=1;
        source.RequireRetainedOwners();
        check(placement.CopyScene().SequenceEqual(expected)&&placement.CopyLevelData().SequenceEqual(expectedBody)&&
            body.CopyLevelData().SequenceEqual(priorBody)&&body.SceneAncestor.CopyScene().SequenceEqual(priorScene)&&
            source.ReadSource(source.PrivateRows[0].Row.Extent)[0]!=sourceRows[0][0]&&source.CopyPackage()[0]!=package[0]&&
            source.PrivateRows.Select((p,i)=>source.ReadSource(p.Properties.Extent).SequenceEqual(sourcePrivate[i])).All(v=>v),
            "Copied payload mutation and refusals leave actual placement, source-coordinate P9, Body and authenticated donor owners immutable.");
        Console.WriteLine($"WIDE_CHEST_PLACEMENT_SOURCE_OK floor=3072 scene={Hash(expected)} body={Hash(expectedBody)} chestDiskMargin=56 rewardAdvisoryDiskMargin=47 approachAdvisoryOnly=true runtime=false");
    }

    private static void AdvisoryPath(byte[] scene,Action<bool,string> check)
    {
        // Conditional planar geometry only: fixed600-radius chest,356-radius
        // player and straight segments. Actual landing/movement, Wizard/Grass
        // collision, states, facing input, key ownership and response are not
        // assumed or established. No production admission uses this helper.
        (int X,int Y)[] path=[(8960,8960),(9728,9120),(9728,8864),(10864,8864)];
        (int X,int Y) chest=(10864,9888);
        BigInteger combined=956*956;
        int[] distances=[1880320,1880320,1048576];
        for(int i=0;i<3;i++)
        {
            var distance=SegmentDistanceSquared(path[i],path[i+1],chest);
            check(distance.Numerator==distances[i]*distance.Denominator&&distance.Numerator>combined*distance.Denominator,
                "Independent exact segment minimum strictly clears the static combined956-radius chest disk, not merely its endpoints.");
        }
        int pathMargin=path.Min(p=>new[]{p.X-8448,11520-p.X,p.Y-8448,11520-p.Y}.Min());
        check(pathMargin==416&&pathMargin-356==60&&path.All(p=>p.X>8804&&p.X<11164&&p.Y>8804&&p.Y<11164),
            "All path vertices lie strictly in the convex356-inset floor; every straight segment therefore has at least60 advisory edge clearance.");
        var crossing=SegmentDistanceSquared((-1024,0),(1024,0),(0,0));
        var tangent=SegmentDistanceSquared((-1024,956),(1024,956),(0,0));
        var separated=SegmentDistanceSquared((-1024,957),(1024,957),(0,0));
        var point=SegmentDistanceSquared((0,956),(0,956),(0,0));
        check(1024*1024>combined&&crossing.Numerator==0&&tangent.Numerator==combined*tangent.Denominator&&
            separated.Numerator>combined*separated.Denominator&&point.Numerator==combined*point.Denominator,
            "Advisory segment oracle detects interior collision despite clear endpoints and refuses exact tangency, including zero-length paths.");
        int chestYaw=scene[0x1DC+0x46],frontBearing=192,headingTowardChest=64;
        int yawDelta=Math.Abs(chestYaw-frontBearing);yawDelta=Math.Min(yawDelta,256-yawDelta);
        int dx=Math.Abs(path[^1].X-chest.X),dy=Math.Abs(path[^1].Y-chest.Y);
        int nativeHorizontal=Math.Max(dx,dy)+3*Math.Min(dx,dy)/8;
        check(chestYaw==0xDC&&frontBearing==0xC0&&yawDelta==28&&yawDelta<0x30&&headingTowardChest==0x40&&
            nativeHorizontal==1024&&nativeHorizontal<1536&&1024-956==68&&
            BinaryPrimitives.ReadInt16LittleEndian(scene.AsSpan(0x1DC+0x38))==0&&
            BinaryPrimitives.ReadInt16LittleEndian(scene.AsSpan(0x184+0x38))==0&&
            Xyz(scene,0x184+12)==(9728,9120,8704)&&Word(scene,8)-8704==512,
            "The advisory front waypoint matches native horizontal/front-angle limits and leaves68 radial gap; authored airborne Z is exactly the excluded512 boundary, so landing, facing, accepted state and acquired key still need runtime evidence.");
    }

    private static (BigInteger Numerator,BigInteger Denominator) SegmentDistanceSquared(
        (int X,int Y) a,(int X,int Y) b,(int X,int Y) centre)
    {
        BigInteger vx=(BigInteger)b.X-a.X,vy=(BigInteger)b.Y-a.Y,wx=(BigInteger)centre.X-a.X,wy=(BigInteger)centre.Y-a.Y;
        BigInteger length=vx*vx+vy*vy,dot=wx*vx+wy*vy;
        if(length==0||dot<=0)return(wx*wx+wy*wy,1);
        if(dot>=length){BigInteger x=(BigInteger)centre.X-b.X,y=(BigInteger)centre.Y-b.Y;return(x*x+y*y,1);}
        BigInteger cross=wx*vy-wy*vx;return(cross*cross,length);
    }
    private static (int X,int Y,int Z) Xyz(ReadOnlySpan<byte> bytes,int at)=>(Word(bytes,at),Word(bytes,at+4),Word(bytes,at+8));
    private static int Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]);
    private static void Put(Span<byte> bytes,int at,int value)=>BinaryPrimitives.WriteInt32LittleEndian(bytes[at..],value);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Wide chest placement refused "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}

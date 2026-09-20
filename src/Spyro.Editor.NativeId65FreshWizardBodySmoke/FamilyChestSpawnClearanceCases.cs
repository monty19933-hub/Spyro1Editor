using System.Numerics;
using System.Reflection;
using Spyro.Editor.Core.Exporting;

internal static class FamilyChestSpawnClearanceCases
{
    internal static void Free(Action<bool,string> check)
    {
        var type=typeof(NativeFreshLockedChestSpawnClearancePlan);
        var constructors=type.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        check(!type.IsPublic&&type.IsSealed&&constructors.Length>0&&constructors.All(c=>c.IsPrivate),
            "Chest clearance is a closed immutable assessment, not a caller-issued collider or public export admission.");
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestSpawnClearancePlan.Compose(null!),check);
    }

    internal static void Source(NativeFreshLockedChestPlacementPlan original,Action<bool,string> check)
    {
        Free(check);
        var body=original.BodyAncestor;
        byte[] originalScene=original.CopyScene(),originalBody=original.CopyLevelData();
        var r01=NativeFreshLockedChestSpawnClearancePlan.Compose(original);
        RequireOwners(r01,original,check);
        check(original.Intent==new NativeFreshLockedChestPlacementIntent(8960,9728,8704,9472,8960,8704)&&
            original.SceneSha256=="075c84cfd5ec13dc245dfa8b888f349903056b07676fc1990c494875e90f20d3",
            "The failed R01 source-coordinate regression remains a separate unchanged placement, not a silently corrected fixture.");
        // Independent scalar facts from the preserved native type1 descriptor:
        // radius600, signed Z offset336, incoming flags4; player radius356.
        // These are diagnostic source facts, not an observed earliest query.
        var finding=r01.InitialFinding;
        check(finding.Spawn==new NativeSpawnClearancePoint(8960,8960,9216)&&
            finding.Centre==new NativeSpawnClearancePoint(9472,8960,9040)&&finding.Vertex is null&&
            finding.ColliderRadius==600&&finding.PlayerRadius==356&&finding.IncomingDamageFlags==4&&
            finding.ResponsePermitted==true&&finding.UnknownReason==NativeInitialSpawnUnknownReason.None&&
            finding.Status==NativeInitialSpawnClearanceStatus.Overlap&&finding.DistanceSquared==(BigInteger)293120&&
            finding.CombinedRadiusSquared==(BigInteger)913936&&r01.HorizontalDistanceSquared==(BigInteger)262144&&
            r01.CombinedRadiusSquared==(BigInteger)913936&&r01.LandingColumnStatus==NativeInitialSpawnClearanceStatus.Overlap,
            "R01 has independently pinned initial3D overlap and a distinct overlapping landing column; zero Y delta is retained.");
        Margins(r01.ChestMargins,1024,1024,512,1536,check);
        Margins(r01.PlayerMargins,512,1536,512,1536,check);
        Reject<InvalidDataException>(()=>r01.RequireStaticLandingClearance(original),check);

        var corrected=NativeFreshLockedChestPlacementPlan.Compose(body,original.Intent with{ChestX=9888,ChestY=9216});
        var clear=NativeFreshLockedChestSpawnClearancePlan.Compose(corrected);
        RequireOwners(clear,corrected,check);
        check(clear.InitialFinding.Centre==new NativeSpawnClearancePoint(9888,9216,9040)&&
            clear.InitialFinding.Spawn==finding.Spawn&&clear.InitialFinding.DistanceSquared==(BigInteger)957696&&
            clear.InitialFinding.Status==NativeInitialSpawnClearanceStatus.InitialSeparation&&
            clear.HorizontalDistanceSquared==(BigInteger)926720&&clear.CombinedRadiusSquared==(BigInteger)913936&&
            clear.LandingColumnStatus==NativeInitialSpawnClearanceStatus.InitialSeparation,
            "The separately issued same-Body correction strictly separates both initial spheres and the vertical landing column.");
        Margins(clear.ChestMargins,1440,608,768,1280,check);
        Margins(clear.PlayerMargins,512,1536,512,1536,check);
        check(clear.ChestMargins.Right-600==8&&clear.ChestMargins.StrictlyContains(600)&&clear.PlayerMargins.StrictlyContains(356),
            "The corrected chest disk has only eight units of strict right-edge clearance; this is not a whole-family footprint guarantee.");
        clear.RequireStaticLandingClearance(corrected);
        check(corrected.CopyScene().AsSpan(0,0x184).SequenceEqual(originalScene.AsSpan(0,0x184))&&
            corrected.BodyAncestor==body&&body.CopyLevelData().SequenceEqual(original.RevertExactLevelData(originalBody)),
            "Clearance diagnosis leaves the actual spawn, Wizard, Grass and immediate Body ancestor unchanged.");

        // Same collider/floor/spawn: vertical separation alone must not grant
        // a conservative landing-column policy. Both XY disks still fit here.
        var columnPlacement=NativeFreshLockedChestPlacementPlan.Compose(body,original.Intent with{ChestX=9888,ChestY=9168});
        var column=NativeFreshLockedChestSpawnClearancePlan.Compose(columnPlacement);
        check(column.InitialFinding.DistanceSquared==(BigInteger)935424&&
            column.InitialFinding.Status==NativeInitialSpawnClearanceStatus.InitialSeparation&&
            column.HorizontalDistanceSquared==(BigInteger)904448&&
            column.LandingColumnStatus==NativeInitialSpawnClearanceStatus.Overlap&&
            column.ChestMargins.StrictlyContains(600)&&column.PlayerMargins.StrictlyContains(356),
            "Initial3D separation does not substitute for landing-column separation, even when both floor disks fit.");
        Reject<InvalidDataException>(()=>column.RequireStaticLandingClearance(columnPlacement),check);
        // The native16-unit grid cannot represent exact radius600 edge contact
        // on this floor: one grid step brackets the strict boundary by +/-8.
        var edgePlacement=NativeFreshLockedChestPlacementPlan.Compose(body,original.Intent with{ChestX=9904,ChestY=9216});
        var edge=NativeFreshLockedChestSpawnClearancePlan.Compose(edgePlacement);
        check(edge.InitialFinding.Status==NativeInitialSpawnClearanceStatus.InitialSeparation&&
            edge.LandingColumnStatus==NativeInitialSpawnClearanceStatus.InitialSeparation&&
            edge.ChestMargins.Right==592&&!edge.ChestMargins.StrictlyContains(600),
            "One native grid step preserves centre containment and separation but crosses the chest footprint boundary by eight units.");
        Reject<InvalidDataException>(()=>edge.RequireStaticLandingClearance(edgePlacement),check);

        var repeat=NativeFreshLockedChestSpawnClearancePlan.Compose(corrected);
        check(repeat.PlanSha256==clear.PlanSha256&&repeat.Evidence.SequenceEqual(clear.Evidence)&&
            clear.PlanSha256!=r01.PlanSha256&&clear.PlanSha256!=column.PlanSha256&&clear.PlanSha256!=edge.PlanSha256,
            "The exact same owner is deterministic; different authored coordinates remain different diagnostic identities.");
        var equalPlacement=NativeFreshLockedChestPlacementPlan.Compose(body,corrected.Intent);
        check(equalPlacement.PlanSha256==corrected.PlanSha256&&equalPlacement.CopyScene().SequenceEqual(corrected.CopyScene()),
            "An equal-hash separately issued placement is a genuine ancestry-negative fixture.");
        Reject<InvalidDataException>(()=>clear.RequireAncestry(equalPlacement),check);
        Reject<InvalidDataException>(()=>clear.RequireStaticLandingClearance(equalPlacement),check);
        var equalBody=NativeFreshLockedChestResearchBody.Compose(body.ResourceAncestor,body.HostAncestor,body.SceneAncestor);
        var foreignPlacement=NativeFreshLockedChestPlacementPlan.Compose(equalBody,corrected.Intent);
        check(equalBody.PlanSha256==body.PlanSha256&&foreignPlacement.PlanSha256==corrected.PlanSha256,
            "An equal-byte separately issued Body does not become the retained actual Body ancestor.");
        Reject<InvalidDataException>(()=>clear.RequireAncestry(foreignPlacement),check);
        Reject<InvalidDataException>(()=>clear.RequireStaticLandingClearance(original),check);
        Reject<ArgumentNullException>(()=>clear.RequireAncestry(null!),check);
        Reject<ArgumentNullException>(()=>clear.RequireStaticLandingClearance(null!),check);
        foreach(int extreme in new[]{int.MinValue,int.MaxValue,262128})
            Reject<InvalidDataException>(()=>NativeFreshLockedChestPlacementPlan.Compose(body,corrected.Intent with{ChestX=extreme}),check);

        // There is deliberately no mutable descriptor/collider decoder input:
        // callers only get copies from the actual typed placement owner.
        byte[] sceneCopy=corrected.CopyScene(),bodyCopy=corrected.CopyLevelData();
        sceneCopy[0]^=1;sceneCopy[0x1DC+12]^=1;bodyCopy[0xE7000+0x19800+0xFF8]^=1;
        Reject<InvalidDataException>(()=>corrected.RequireExactScene(sceneCopy),check);
        Reject<InvalidDataException>(()=>corrected.RequireExactLevelData(bodyCopy),check);
        var evidenceCopy=clear.Evidence.ToArray();
        var firstEvidence=clear.Evidence[0];evidenceCopy[0]=firstEvidence with{Sha256=new string('0',64)};
        if(clear.Evidence is IList<NativeInitialSpawnClearanceEvidence> list)
            Reject<NotSupportedException>(()=>list[0]=evidenceCopy[0],check);
        clear.RequireAncestry(corrected);clear.RequireStaticLandingClearance(corrected);
        check(clear.Evidence[0]==firstEvidence&&clear.PlanSha256==repeat.PlanSha256&&
            original.CopyScene().SequenceEqual(originalScene)&&original.CopyLevelData().SequenceEqual(originalBody),
            "Detached-copy corruption, evidence-copy replacement and all refusals leave the exact R01 and corrected owners unchanged.");
        Console.WriteLine("NATIVE_FRESH_CHEST_SPAWN_CLEARANCE_SOURCE_OK r01=overlap corrected=static-only chestEdgeMargin=8 runtime=false trajectory=false wholeFamily=false");
    }

    private static void RequireOwners(NativeFreshLockedChestSpawnClearancePlan plan,
        NativeFreshLockedChestPlacementPlan placement,Action<bool,string> check)
    {
        plan.RequireAncestry(placement);
        check(ReferenceEquals(plan.PlacementAncestor,placement)&&ReferenceEquals(plan.BodyAncestor,placement.BodyAncestor)&&
            ReferenceEquals(plan.HostAncestor,placement.BodyAncestor.HostAncestor)&&plan.SourceCertified&&
            !plan.RuntimeVerified&&!plan.FirstQueryProven&&!plan.TrajectoryProven&&!plan.WholeFamilyFootprintClearanceProven&&
            !plan.PublicationApproved&&plan.SourceImageSha256=="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"&&
            Digest(plan.PlanSha256),
            "Actual placement/Body/Host lineage and authenticated source identity do not grant runtime, trajectory, family-footprint or publication authority.");
        check(plan.Evidence.Count>0&&plan.Evidence.Select(e=>e.Id).Distinct().Count()==plan.Evidence.Count&&
            plan.Evidence.All(e=>!string.IsNullOrWhiteSpace(e.Id)&&!string.IsNullOrWhiteSpace(e.Domain)&&
                e.Offset>=0&&e.ByteLength>0&&(BigInteger)e.Offset+e.ByteLength<=int.MaxValue&&Digest(e.Sha256)),
            "The diagnostic retains distinct bounded immutable source-owner evidence, not bare expected geometry or hash-only ancestry.");
    }
    private static void Margins(NativeInitialFloorMargins value,int left,int right,int top,int bottom,Action<bool,string> check)=>
        check(value.Left==left&&value.Right==right&&value.Top==top&&value.Bottom==bottom,
            "Four independently derived signed floor margins use the actual2048-unit floor.");
    private static bool Digest(string value)=>value.Length==64&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Chest clearance refused "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}

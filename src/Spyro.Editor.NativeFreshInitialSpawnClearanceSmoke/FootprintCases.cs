// Tests first: neutral math does not manufacture an issued source assessment.
using System.Numerics;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static ClearanceCases;

internal static class FootprintCases
{
    private const string Policy = "strict-initial-XY-disks-v1";
    private static NativeId65FreshDocumentIntent B(int sx=8960,int sy=8960)=>
        NativeId65FreshDocumentIntent.Create(8448,8448,8704,2048,2048,sx,sy,64,
            new(80,128,112),new(128,96,64),19);
    // Exact source-shaped bounded neutral bytes only; this is NOT CaptureWizard.
    private static NativeInitialSpawnClearanceFinding Evaluate(NativeId65FreshDocumentIntent floor,
        int centreX=9828,int centreY=9974,byte[]? package=null)=>
        NativeFreshInitialSpawnClearanceSource.EvaluateWizard(package??Package(),
            Row(new(centreX+156,centreY+10,8704,9472,9984)),
            new(floor.SpawnX,floor.SpawnY,floor.SpawnZ),Id);

    internal static void Free()
    {
        int beforeChecks=Checks,beforeRefusals=Refusals;
        var floor=B();var known=Evaluate(floor);
        var result=NativeInitialSpawnClearanceMath.RequireInitialFloorFootprint(known,floor);
        Check(result.PolicyId==Policy && result.WizardMargins.Left==1380 &&
            result.WizardMargins.Right==668 && result.WizardMargins.Top==1526 &&
            result.WizardMargins.Bottom==522,"Separated B exact source-formula initial footprint.");
        Check(result.PlayerMargins.Left==512 && result.PlayerMargins.Right==1536 &&
            result.PlayerMargins.Top==512 && result.PlayerMargins.Bottom==1536,
            "Player-query radius is tested independently from Wizard radius.");
        Check(known.IncomingDamageFlags==0 && known.ResponsePermitted==true,
            "Zero incoming damage is still an active positional-response sphere.");
        var nonzeroFlags=Package();Put(nonzeroFlags,0x35B0,0x20);
        var sameGeometry=NativeInitialSpawnClearanceMath.RequireInitialFloorFootprint(Evaluate(floor,package:nonzeroFlags),floor);
        Check(sameGeometry.WizardMargins==result.WizardMargins && sameGeometry.PlayerMargins==result.PlayerMargins,
            "Neutral nonzero damage flags cannot change geometric disk containment; no source plan is issued.");

        for(int edge=0;edge<4;edge++)
        foreach(int offset in new[]{-1,0,1})
        {
            int value=400+offset;
            int x=edge==0?8448+value:edge==1?10496-value:10080;
            int y=edge==2?8448+value:edge==3?10496-value:10080;
            var finding=Evaluate(floor,x,y);
            Check(finding.Status==NativeInitialSpawnClearanceStatus.InitialSeparation,
                "Wizard edge fixture isolates floor footprint, not sphere overlap.");
            if(offset==1) _=NativeInitialSpawnClearanceMath.RequireInitialFloorFootprint(finding,floor);
            else Reject<InvalidDataException>(()=>NativeInitialSpawnClearanceMath.RequireInitialFloorFootprint(finding,floor));
        }
        for(int edge=0;edge<4;edge++)
        foreach(int offset in new[]{-1,0,1})
        {
            int value=356+offset;
            var f=B(edge==0?8448+value:edge==1?10496-value:8960,
                edge==2?8448+value:edge==3?10496-value:8960);
            var finding=Evaluate(f);
            Check(finding.Status==NativeInitialSpawnClearanceStatus.InitialSeparation,
                "Player edge fixture isolates footprint and preserves source-query radius.");
            if(offset==1) _=NativeInitialSpawnClearanceMath.RequireInitialFloorFootprint(finding,f);
            else Reject<InvalidDataException>(()=>NativeInitialSpawnClearanceMath.RequireInitialFloorFootprint(finding,f));
        }

        foreach(var status in new[]{NativeInitialSpawnClearanceStatus.Unknown,
            NativeInitialSpawnClearanceStatus.Overlap,NativeInitialSpawnClearanceStatus.Touching})
        {
            var finding=new NativeInitialSpawnClearanceFinding(Id,status,
                status==NativeInitialSpawnClearanceStatus.Unknown?NativeInitialSpawnUnknownReason.UnsupportedPose:
                    NativeInitialSpawnUnknownReason.None,known.Spawn,known.Vertex,known.Centre,400,0,true);
            Reject<InvalidDataException>(()=>NativeInitialSpawnClearanceMath.RequireInitialFloorFootprint(finding,floor));
        }
        foreach(byte[] unsupported in UnsupportedPackages())
        {
            var finding=Evaluate(floor,package:unsupported);
            Check(finding.Status==NativeInitialSpawnClearanceStatus.Unknown,
                "Null descriptor and unsupported response are unknown, not no collider.");
            Reject<InvalidDataException>(()=>NativeInitialSpawnClearanceMath.RequireInitialFloorFootprint(finding,floor));
        }
        foreach(var finding in new[]{
            new NativeInitialSpawnClearanceFinding(Id,NativeInitialSpawnClearanceStatus.InitialSeparation,
                NativeInitialSpawnUnknownReason.None,known.Spawn),
            new NativeInitialSpawnClearanceFinding(Id,NativeInitialSpawnClearanceStatus.InitialSeparation,
                NativeInitialSpawnUnknownReason.None,known.Spawn,known.Vertex,known.Centre,400,0,false)})
            Reject<InvalidDataException>(()=>NativeInitialSpawnClearanceMath.RequireInitialFloorFootprint(finding,floor));

        var extreme=NativeInitialSpawnClearanceMath.InitialFloorMargins(floor,new(int.MinValue,int.MaxValue,0));
        Check(extreme.Left==(BigInteger)int.MinValue-8448 && extreme.Right==(BigInteger)10496-int.MinValue &&
            extreme.Top==(BigInteger)int.MaxValue-8448 && extreme.Bottom==(BigInteger)10496-int.MaxValue,
            "Margins widen BEFORE subtraction; negative outside values cannot wrap.");
        var oldFloor=A();var old=NativeFreshInitialSpawnClearanceSource.EvaluateWizard(Package(),Row(Placement),
            new(oldFloor.SpawnX,oldFloor.SpawnY,oldFloor.SpawnZ),Id);
        Check(old.Status==NativeInitialSpawnClearanceStatus.Overlap && old.DistanceSquared==243828,
            "Historical unsafe P1 remains diagnosable with its exact original values.");
        Reject<InvalidDataException>(()=>NativeInitialSpawnClearanceMath.RequireInitialFloorFootprint(old,oldFloor));
        var corner=Evaluate(oldFloor,8308,8454);
        Check(corner.Status==NativeInitialSpawnClearanceStatus.InitialSeparation && corner.DistanceSquared==755124,
            "Known tiny-floor corner passes separation but is not footprint clearance.");
        Reject<InvalidDataException>(()=>NativeInitialSpawnClearanceMath.RequireInitialFloorFootprint(corner,oldFloor));
        Console.WriteLine($"INITIAL_FLOOR_FOOTPRINT_FREE_OK checks={Checks-beforeChecks} refusals={Refusals-beforeRefusals} sourceIssued=false outputWritten=false");
    }

    private static IEnumerable<byte[]> UnsupportedPackages()
    {
        var missing=Package();Put(missing,0x68,0xFF200D78);yield return missing;
        var response=Package();Put(response,0x35A8,0x01900303);yield return response;
    }

    // Call for actual rows from the later72-row source matrix. The expected
    // boolean is a literal test expectation, never a Candidate input.
    internal static void Source(NativeFreshLevelDocumentIntent document,
        NativeId65FreshWizardResearchScenePlan scene,NativeFreshInitialSpawnClearancePlan actual,bool expected)
    {
        string planHash=actual.PlanSha256;byte[] canonical=actual.CopyCanonicalDocument();
        if(expected)
        {
            var result=actual.RequireInitialFloorFootprint(document,scene);
            Check(result.PolicyId==Policy,"Actual method derives only the fixed initial footprint policy.");
        }
        else Reject<InvalidDataException>(()=>actual.RequireInitialFloorFootprint(document,scene));
        var equal=NativeFreshLevelDocumentIntent.Create(document.Base,document.Placements);
        Reject<InvalidDataException>(()=>actual.RequireInitialFloorFootprint(equal,scene));
        Check(actual.PlanSha256==planHash && actual.CopyCanonicalDocument().SequenceEqual(canonical) &&
            !actual.RuntimeVerified && !actual.FirstQueryProven && !actual.TrajectoryProven && !actual.PublicationApproved,
            "New admission does not mutate old diagnosis identity or promote pending gates.");
    }
}

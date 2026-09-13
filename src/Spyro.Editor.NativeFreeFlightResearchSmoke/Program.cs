using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using System.Text.Json;

int checks = AdversarialCases.Run();
checks += await ProductionStageCases.RunFreeAsync();
if (args.Length != 0 && args.Length != 3 && args.Length != 5)
    throw new ArgumentException("Usage: NativeFreeFlightResearchSmoke [original.bin original.cue workspace [--verify|--exercise-staging|--publish|--verify-fast-entry|--exercise-staging-fast-entry|--publish-fast-entry|--exercise-production-stage directory]]");

Require(NativeFreeFlightResearchCandidatePlan.TargetLevelId == 14, "one explicit source profile");
Require(NativeFreeFlightResearchCandidatePlan.RequiredNormalBufferGap == 0xA780, "known extents exact margin, not runtime fit");
if (args.Length == 0)
{
    Console.WriteLine($"NATIVE_FREE_FLIGHT_RESEARCH_SOURCE_FREE_OK checks={checks}");
    return;
}

var catalog = LevelCatalog.Load(args[2]);
var plan = await NativeFreeFlightResearchCandidatePlan.CaptureAsync(args[0], args[1], catalog);
var fastEntry = await FastEntryCases.RunAsync(args[0], args[1], catalog, plan);
checks += fastEntry.Checks;
bool selector = args.Length == 5 && args[3].EndsWith("-fast-entry", StringComparison.Ordinal);
if (selector) plan = fastEntry.Plan;
Require(plan.AnimationMerge.RequestedAnimationIds.SequenceEqual(new[] { 33, 34, 35 }), "native root0 dependencies");
Require(plan.AnimationMerge.SectorRoundedGrowth == 0x3800, "typed insertion sector growth");
Require(plan.Memory.NormalReservedBufferGapBytes == 0xA780 && plan.Memory.KnownPostLoadExtentsDisjoint,
    "source-bound known resident extents");
Require(!plan.RuntimeVerified && !plan.NormalCreateBinApproved && !plan.Memory.RuntimeFitProven,
    "research proof cannot promote runtime or ordinary export");
Require(plan.ExpectedExecutableSha256 != NativeFreeFlightPatchPlan.SourceExecutableSha256, "real native flight executable");
Require(plan.RelocatedExecutableLba == 53882, "live executable moves seven sectors");
await plan.RequireFreshAsync();
if (args.Length == 5)
{
    if (args[3] == "--exercise-production-stage")
    {
        checks += await ProductionStageCases.RunSourceAsync(args[0], args[1], plan, args[4]);
    }
    else if (args[3] is "--verify" or "--verify-fast-entry")
    {
        var verified = await NativeFreeFlightResearchCandidatePublisher.VerifyAsync(plan, args[4]);
        Require(verified.WholeImageReadback && !verified.RuntimeVerified, "published readback distinct from gameplay");
    }
    else if (args[3] is "--exercise-staging" or "--exercise-staging-fast-entry")
    {
        checks += await StagingCases.RunAsync(args[0], args[2], args[4], selector);
    }
    else if (args[3] is "--publish" or "--publish-fast-entry")
    {
        // Explicit research invocation only. Default and three-argument modes
        // never publish, and ordinary editor export approval remains false.
        var published = await NativeFreeFlightResearchCandidatePublisher.PublishAsync(plan, args[4]);
        Require(published.WholeImageReadback && !published.RuntimeVerified && !published.NormalCreateBinApproved,
            "research publication is not runtime or ordinary export approval");
        Console.WriteLine("NATIVE_FREE_FLIGHT_RESEARCH_PUBLICATION " + JsonSerializer.Serialize(published));
    }
    else throw new ArgumentException("Unknown research command.");
}
Console.WriteLine($"NATIVE_FREE_FLIGHT_RESEARCH_SOURCE_OK checks={checks} plan={plan.PlanSha256} gap={plan.Memory.NormalReservedBufferGapBytes:X}");

void Require(bool value, string message)
{
    if (!value) throw new InvalidDataException(message);
    checks++;
}

using System.Text.Json;
using Spyro.Editor.Core.Persistence;

if (args.Length == 5 && args[0] == "--publish")
{
    var result = Id65CollisionCandidatePublisher.Publish(args[1], args[2], args[3], args[4]);
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("ID65_COLLISION_CANDIDATE_PUBLISHED runtime=false normalCreateBin=false");
}
else if (args.Length == 4 && args[0] is "--verify" or "--verify-regressions")
{
    var compiled = Id65CollisionCandidatePublisher.Compile(args[1]);
    using var parent = File.OpenRead(args[2]);
    using var candidate = File.OpenRead(args[3]);
    var readback = Id65CollisionCandidatePublisher.Verify(compiled, parent, candidate);
    Console.WriteLine(JsonSerializer.Serialize(readback));
    Console.WriteLine("ID65_COLLISION_CANDIDATE_VERIFIED imageOnly=true companionsVerified=false runtime=false");
    if (args[0] == "--verify-regressions")
        CandidateReadbackCases.Run(compiled, args[2], args[3]);
}
else
{
    Console.Error.WriteLine("Usage: --publish <locked-fixture.bin> <exact-parent.bin> <parent.cue> <new-output-root>\n" +
        "       --verify or --verify-regressions <locked-fixture.bin> <exact-parent.bin> <candidate.bin>");
    return 2;
}
return 0;

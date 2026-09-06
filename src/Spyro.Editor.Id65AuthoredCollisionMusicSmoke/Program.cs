int compositionChecks = CompositionCases.Run();
Console.WriteLine($"ID65_AUTHORED_COLLISION_MUSIC_COMPOSITION_OK checks={compositionChecks}");
Console.WriteLine($"ID65_AUTHORED_COLLISION_MUSIC_INPUT_OK checks={InputCases.Run()}");
Console.WriteLine($"ID65_AUTHORED_COLLISION_MUSIC_CUE_SNAPSHOT_OK checks={CueSnapshotCases.Run()}");
if (args.Length != 0)
{
    if (args.Length != 2 || args[0] is not ("--candidates" or "--verify-retained" or "--cue-freshness"))
        throw new ArgumentException("Use --candidates <repository-root>, --verify-retained <existing-fixture-root>, or --cue-freshness <existing-fixture-root>.");
    if (args[0] == "--candidates") await CandidateCases.RunAsync(Path.GetFullPath(args[1]));
    else if (args[0] == "--cue-freshness") await CueFreshnessCases.RunAsync(Path.GetFullPath(args[1]));
    else await RetainedCases.RunAsync(Path.GetFullPath(args[1]));
}
Console.WriteLine("ID65_AUTHORED_COLLISION_MUSIC_SMOKE_OK sourceFree=1 runtime=false");

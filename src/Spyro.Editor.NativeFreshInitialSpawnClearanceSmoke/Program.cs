if(args.Length!=0 && (args.Length!=2 || args[0]!="--source" || string.IsNullOrWhiteSpace(args[1])))
    throw new ArgumentException("Usage: InitialSpawnClearanceSmoke [--source original.bin]");
await ClearanceCases.Run();
await ResourceStreamCases.Run();
if(args.Length==2)await SourceCases.Run(args[1]);

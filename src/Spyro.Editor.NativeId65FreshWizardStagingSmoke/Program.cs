if(args.Length!=0 && (args.Length!=2 || args[0]!="--source"))
    throw new ArgumentException("Usage: FreshWizardStagingSmoke [--source original.bin]");
await StagingCases.SourceFree();
// Exact production snapshot checker; hostile streams do not issue a stage.
await ResourceStreamCases.Run();
if(args.Length==2)await StagingCases.Source(args[1]);

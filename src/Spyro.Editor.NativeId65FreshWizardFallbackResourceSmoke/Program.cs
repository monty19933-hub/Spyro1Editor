if(args.Length!=0 && (args.Length!=2 || args[0]!="--source"))
    throw new ArgumentException("Usage: FreshWizardFallbackResourceSmoke [--source original.bin]");
await FallbackCases.SourceFree();
await ResourceStreamCases.Run();
if(args.Length==2)await FallbackCases.Source(args[1]);

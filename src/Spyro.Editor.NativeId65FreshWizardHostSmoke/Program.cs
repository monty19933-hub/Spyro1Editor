if(args.Length!=0 && (args.Length!=2 || args[0]!="--source"))
    throw new ArgumentException("Usage: FreshWizardHostSmoke [--source original.bin]");
await HostCases.SourceFree();
await ResourceStreamCases.Run();
if(args.Length==2)await HostCases.Source(args[1]);

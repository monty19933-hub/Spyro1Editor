if(args.Length!=0 && (args.Length!=2 || args[0]!="--source"))
    throw new ArgumentException("Usage: FreshWizardBodySmoke [--source original.bin]");
await BodyCases.SourceFree();
await ResourceStreamCases.Run();
if(args.Length==2)await BodyCases.Source(args[1]);

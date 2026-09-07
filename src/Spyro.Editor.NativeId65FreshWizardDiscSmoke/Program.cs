if(args.Length!=0&&(args.Length!=2||args[0]!="--source"))
    throw new ArgumentException("Usage: FreshWizardDiscSmoke [--source original.bin]");
await ApiCases.Run();
SourceCases.SourceFree();
await ResourceStreamCases.Run();
if(args.Length==2)await SourceCases.Source(args[1]);

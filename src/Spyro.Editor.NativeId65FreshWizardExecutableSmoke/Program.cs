if(args.Length!=0&&(args.Length!=2||args[0]!="--source"))
    throw new ArgumentException("Usage: FreshWizardExecutableSmoke [--source original.bin]");
await ApiCases.Run();
ExecutableCases.SourceFree();
await ResourceStreamCases.Run();
if(args.Length==2)await ExecutableCases.Source(args[1]);

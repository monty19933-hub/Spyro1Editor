if(args.Length!=0&&(args.Length!=3||args[0]!="--source"))
    throw new ArgumentException("Usage: FreshWizardResearchSmoke [--source original.bin original.cue]; no output mode yet.");
await ApiCases.Run();
await FreeCases.Run();
if(args.Length==3)await SourceCases.Run(args[1],args[2]);

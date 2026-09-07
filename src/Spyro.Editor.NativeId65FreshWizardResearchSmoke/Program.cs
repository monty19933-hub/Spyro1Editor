if(ReviewedPublicationCases.IsReviewedMode(args)){await ReviewedPublicationCases.RunAsync(args);return;}
if(PublicationBoundaryModes.IsMode(args)){await PublicationBoundaryModes.RunAsync(args);return;}
if(args.Length!=0&&(args.Length!=3||args[0]!="--source"))
    throw new ArgumentException("Usage: FreshWizardResearchSmoke [--source original.bin original.cue]; separate exact --publish-reviewed/--verify-reviewed/--stage-reviewed/--postcommit-reviewed BIN CUE DIRECTORY modes are opt-in only.");
await ApiCases.Run();
await FreeCases.Run();
ReviewedPublicationCases.RunArgumentTests();
PublicationBoundaryModes.RunArgumentTests();
await PublicationFailureCases.Run();
if(args.Length==3)await SourceCases.Run(args[1],args[2]);

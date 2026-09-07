if (args.Length != 0 && (args.Length != 2 || args[0] != "--source"))
    throw new ArgumentException("Usage: FreshWizardResourceSmoke [--source original.bin]");
await FreshWizardResourceCases.SourceFree();
await ResourceStreamCases.Run();
if (args.Length == 2) await FreshWizardResourceCases.Source(args[1]);

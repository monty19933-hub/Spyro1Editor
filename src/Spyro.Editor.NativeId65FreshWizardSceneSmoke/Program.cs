if (args.Length != 0 && (args.Length != 2 || args[0] != "--source"))
    throw new ArgumentException("Usage: FreshWizardSceneSmoke [--source original.bin]");
await SceneCases.SourceFree();
// The new issuer reuses this exact bounded snapshot checker, not a new stream authority.
await ResourceStreamCases.Run();
if (args.Length == 2) await SceneCases.Source(args[1]);

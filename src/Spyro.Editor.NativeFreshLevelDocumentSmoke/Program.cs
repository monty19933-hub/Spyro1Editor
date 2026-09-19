if (args.Length != 0)
    throw new ArgumentException("Usage: NativeFreshLevelDocumentSmoke (neutral source-free only)");
AggregateCases.Run();
CodecCases.Run();
AggregateStorageCases.Run();
AggregateForkCases.Run();
AggregateReadGuardCases.Run();
MaterialIntentCases.Run();
MaterialCodecCases.Run();
MaterialStorageCases.Run();
MobyIntentCases.Run();
MobyCodecCases.Run();
MobyStorageCases.Run();
TiledIntentCases.Run();
TiledCodecCases.Run();
TiledStorageCases.Run();
Console.WriteLine($"NATIVE_FRESH_LEVEL_DOCUMENT_SMOKE_OK checks={Cases.Checks} refusals={Cases.Refusals}");

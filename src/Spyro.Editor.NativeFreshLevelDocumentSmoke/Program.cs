if (args.Length != 0)
    throw new ArgumentException("Usage: NativeFreshLevelDocumentSmoke (neutral source-free only)");
AggregateCases.Run();
CodecCases.Run();
AggregateStorageCases.Run();
Console.WriteLine($"NATIVE_FRESH_LEVEL_DOCUMENT_SMOKE_OK checks={Cases.Checks} refusals={Cases.Refusals}");

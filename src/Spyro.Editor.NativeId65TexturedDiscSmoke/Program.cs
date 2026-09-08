if (args.Length != 0 && (args.Length != 2 || args[0] != "--source" ||
    string.IsNullOrWhiteSpace(args[1]) || args[1].Contains('\0') || args[1].StartsWith("--", StringComparison.Ordinal)))
    throw new ArgumentException("Usage: NativeId65TexturedDiscSmoke [--source original.bin]; no output/publication mode.");
int checks = 0;
void Check(bool ok, string text) { if (!ok) throw new InvalidDataException(text); checks++; }
ApiCases.Free(Check);
ShapeCases.Free(Check);
Console.WriteLine($"NATIVE_ID65_TEXTURED_DISC_FREE_OK checks={checks} sourceIo=0 outputWrites=0");
if (args.Length == 2)
{
    await SourceCases.Run(args[1], Check);
    Console.WriteLine($"NATIVE_ID65_TEXTURED_DISC_SOURCE_OK checks={checks} fullSourceFinal=true outputWrites=0 runtime=false publication=false");
}

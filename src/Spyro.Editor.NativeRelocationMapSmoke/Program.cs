int checks = 0;
ConsumerCases.Run(Check);
#if !FROZEN_CORE
MapCases.Run(Check);
#endif
Console.WriteLine($"NATIVE_RELOCATION_MAP_SMOKE_OK checks={checks}");
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new InvalidOperationException(message);
}

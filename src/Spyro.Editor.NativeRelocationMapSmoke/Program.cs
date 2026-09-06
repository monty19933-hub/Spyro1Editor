int checks = 0;
if (args.Length == 2 && args[0] == "--native-header-audit")
{
    NestedGrowthCases.RunSource(args[1], Check);
    Console.WriteLine($"NATIVE_LEVEL_HEADER_GROWTH_AUDIT_OK checks={checks}");
    return;
}
if (args.Length == 1 && args[0] == "--single-subfile-baseline")
{
    NestedGrowthCases.SingleSubfileBaseline();
    return;
}
ConsumerCases.Run(Check);
NestedGrowthCases.Run(Check);
NestedGrowthDiscCases.Run(Check);
#if !FROZEN_CORE
MapCases.Run(Check);
#endif
Console.WriteLine($"NATIVE_RELOCATION_MAP_SMOKE_OK checks={checks}");
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new InvalidOperationException(message);
}

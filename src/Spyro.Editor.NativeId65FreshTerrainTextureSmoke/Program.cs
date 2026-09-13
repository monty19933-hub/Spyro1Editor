using System.Security.Cryptography;

internal static class Program
{
    private static int _checks;
    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--source"))
            throw new ArgumentException("Usage: NativeId65FreshTerrainTextureSmoke [--source original.bin]");
        AllocationCases.Run();
        GpuExtractionCases.Run();
        ResourceGraphCases.Run();
        await SelectedBindingCases.Free();
        Console.WriteLine($"NATIVE_ID65_FRESH_TERRAIN_TEXTURE_FREE_OK checks={_checks}");
        if (args.Length == 2)
        {
            int before = _checks;
            await SourceCases.Run(args[1]);
            await SelectedBindingCases.Source(args[1]);
            Console.WriteLine($"NATIVE_ID65_FRESH_TERRAIN_TEXTURE_SOURCE_OK checks={_checks - before} total={_checks}");
        }
        return 0;
    }
    internal static void Check(bool value, string message)
    {
        if (!value) throw new InvalidDataException(message);
        _checks++;
    }
    internal static void Reject(Action action, string message)
    {
        try { action(); }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or InvalidOperationException)
        { _checks++; return; }
        throw new InvalidDataException("Expected bounded refusal: " + message);
    }
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

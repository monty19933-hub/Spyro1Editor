using System.Security.Cryptography;

internal static class Program
{
    private static int _checks;
    private static int Main(string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--source"))
            throw new ArgumentException("Usage: NativeId65StaticTerrainTextureSmoke [--source original.bin]");
        NativeScalarCases.Run(Check, Reject);
        StaticSourceCases.Free(Check, Reject);
        SourcePreviewCases.Free(Check, Reject);
        Console.WriteLine($"NATIVE_ID65_STATIC_TERRAIN_TEXTURE_FREE_OK checks={_checks}");
        if (args.Length == 2)
        {
            int before = _checks;
            StaticSourceCases.Source(args[1], Check, Reject);
            SourcePreviewCases.Source(args[1], Check, Reject);
            Console.WriteLine($"NATIVE_ID65_STATIC_TERRAIN_TEXTURE_SOURCE_OK checks={_checks - before} total={_checks}");
        }
        return 0;
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidDataException(message);
        _checks++;
    }
    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or InvalidOperationException or IOException)
        { _checks++; return; }
        throw new InvalidDataException("Expected refusal: " + message);
    }
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

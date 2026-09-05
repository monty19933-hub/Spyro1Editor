using System.Reflection;
using System.Runtime.ExceptionServices;
using Spyro.Editor.Core.Exporting;

string root = Directory.CreateTempSubdirectory("spyro-moby-wad-reader-").FullName;
List<string> failures = [];
int checks = 0;
try
{
    foreach ((int size, int user) in new[] { (2048, 0), (2352, 24), (2336, 8) })
    {
        DiscLayout layout = new(size, user, 0, 0);
        byte[] bytes = new byte[40 * size];
        byte[] payload = Enumerable.Range(0, 3 * 2048).Select(index => (byte)(index * 37)).ToArray();
        for (int sector = 0; sector < 3; sector++)
            payload.AsSpan(sector * 2048, 2048).CopyTo(bytes.AsSpan((37 + sector) * size + user));
        string path = Path.Combine(root, $"{size}.bin");
        File.WriteAllBytes(path, bytes);
        using FileStream stream = File.OpenRead(path);
        Run($"{size} cross-sector read", () => Require(((byte[])Invoke("ReadWadBytes", stream, layout, 2039L, 2069))
            .AsSpan().SequenceEqual(payload.AsSpan(2039, 2069)), "WAD payload changed across a raw-sector boundary."));
        Run($"{size} negative WAD offset", () => RequireThrows<ArgumentOutOfRangeException>(
            () => Invoke("ReadWadBytes", stream, layout, -2048L, 1)));
        Run($"{size} negative empty WAD offset", () => RequireThrows<ArgumentOutOfRangeException>(
            () => Invoke("ReadWadBytes", stream, layout, -1L, 0)));
        Run($"{size} offset integer precision", () =>
        {
            long offset = (1L << 54) + 2047;
            long exact = checked((37 + offset / 2048) * size + user + offset % 2048);
            Require((long)Invoke("ConvertWadOffsetToImageOffset", layout, offset) == exact,
                "WAD offset mapping rounded through floating point.");
        });
        Run($"{size} validate before allocation", () =>
        {
            // Warm the reflection/exception path, then measure a bounded 1 MiB
            // request. Never provoke a multi-gigabyte allocation in the red test.
            try { Invoke("ReadWadBytes", stream, layout, 0L, 8192); } catch (EndOfStreamException) { }
            long before = GC.GetAllocatedBytesForCurrentThread();
            RequireThrows<EndOfStreamException>(() => Invoke("ReadWadBytes", stream, layout, 0L, 1024 * 1024));
            Require(GC.GetAllocatedBytesForCurrentThread() - before < 64 * 1024,
                "Moby WAD reader allocated the requested output before rejecting its extent.");
        });
        Run($"{size} source unchanged", () => Require(File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes), "Read path mutated source."));
    }
    if (failures.Count != 0)
        throw new InvalidOperationException($"{failures.Count}/{checks} failed:\n" + string.Join('\n', failures));
    Console.WriteLine($"MOBY_WAD_READER_SMOKE_OK checks={checks}; bounded shared reads, exact offsets, three layouts, source unchanged");
}
finally { Directory.Delete(root, recursive: true); }

void Run(string name, Action action)
{
    checks++;
    try { action(); }
    catch (Exception error) { failures.Add($"{name}: {error.Message}"); }
}

static object Invoke(string name, params object[] arguments)
{
    MethodInfo method = typeof(MobySourcePatchExporter).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Missing exporter method {name}.");
    try { return method.Invoke(null, arguments)!; }
    catch (TargetInvocationException error) when (error.InnerException != null)
    {
        ExceptionDispatchInfo.Capture(error.InnerException).Throw();
        throw;
    }
}

static void RequireThrows<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

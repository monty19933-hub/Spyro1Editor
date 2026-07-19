namespace Spyro.Editor.Smoke;

internal sealed class SmokeTemporaryDirectory : IDisposable
{
    private bool _disposed;

    private SmokeTemporaryDirectory(string path)
    {
        Path = path;
        Directory.CreateDirectory(path);
    }

    public string Path { get; }

    public static SmokeTemporaryDirectory Create(string prefix)
    {
        string safePrefix = string.IsNullOrWhiteSpace(prefix) ? "spyro-editor-smoke" : prefix.Trim();
        return new SmokeTemporaryDirectory(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"{safePrefix}-{Guid.NewGuid():N}"));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);
    }
}

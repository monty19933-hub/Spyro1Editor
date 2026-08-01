namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Stages a disc image for an exporter without blocking the caller's UI
/// context. Combined builds may move a disposable predecessor instead of
/// materializing another 600+ MB copy; standalone exporters remain
/// non-destructive by default.
/// </summary>
internal static class DiscImageWorkingCopy
{
    public static async Task StageAsync(
        string sourceImagePath,
        string destinationImagePath,
        bool consumeDisposableSource,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationImagePath);
        cancellationToken.ThrowIfCancellationRequested();

        string source = Path.GetFullPath(sourceImagePath);
        string destination = Path.GetFullPath(destinationImagePath);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A disc-image working copy cannot replace its own source path.");

        Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? ".");
        if (consumeDisposableSource)
        {
            File.Move(source, destination, overwrite: true);
            return;
        }

        await Task.Run(
            () => File.Copy(source, destination, overwrite: true),
            cancellationToken);
    }
}

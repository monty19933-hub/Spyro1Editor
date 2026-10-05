namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Create BIN stages whole disc images (about 660 MB each) in <c>&lt;project&gt;/_local</c>. These are working
/// copies only: nothing reads them after the export returns, so they are removed on every outcome and any
/// left behind by an interrupted or older run are swept the next time a project opens.
/// </summary>
public static class ExportScratchSpace
{
    public static readonly string[] SweptFolders = ["authored-aggregate"];

    public static string AggregateRoot(string workspace) => Path.Combine(workspace, "_local", "authored-aggregate");

    /// <summary>Deletes one staging folder. Never throws: failing to clean up must not fail an export.</summary>
    public static void Delete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Removes staging folders that no running export can still own. Returns the bytes freed.</summary>
    public static long SweepStale(string workspace, TimeSpan olderThan)
    {
        long freed = 0;
        string root = AggregateRoot(workspace);
        if (!Directory.Exists(root))
            return 0;
        DateTime cutoff = DateTime.UtcNow - olderThan;
        foreach (string folder in Directory.GetDirectories(root))
        {
            try
            {
                DirectoryInfo info = new(folder);
                DateTime newest = info.EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
                    .Select(entry => entry.LastWriteTimeUtc)
                    .Append(info.LastWriteTimeUtc)
                    .Max();
                if (newest > cutoff)
                    continue;
                long size = info.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
                Directory.Delete(folder, recursive: true);
                freed += size;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
        return freed;
    }

    /// <summary>
    /// The ordinary export stages its disc images under <c>&lt;output&gt;/_combined-build/run-*</c>. A finished
    /// export has published or abandoned them, so any image still there is dead weight (about 660 MB each).
    /// The small reports beside them are kept. Call only when no export is running. Returns the bytes freed.
    /// </summary>
    public static long DeleteStagedImages(string outputDirectory, long smallestImageBytes = 1_000_000)
    {
        long freed = 0;
        try
        {
            string root = Path.Combine(outputDirectory, "_combined-build");
            if (!Directory.Exists(root))
                return 0;
            foreach (string file in Directory.EnumerateFiles(root, "*.bin", SearchOption.AllDirectories).ToArray())
            {
                try
                {
                    long size = new FileInfo(file).Length;
                    if (size < smallestImageBytes)
                        continue;
                    File.Delete(file);
                    freed += size;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        return freed;
    }

    /// <summary>Keeps only the newest cached baseline image for an authored level; older fingerprints are dead.</summary>
    public static long PruneAuthoredBaselines(string workspace, string keepPath)
    {
        long freed = 0;
        string root = Path.Combine(workspace, "_local", "authored-native");
        if (!Directory.Exists(root))
            return 0;
        foreach (string file in Directory.GetFiles(root, "*.bin"))
        {
            if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(keepPath), StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                long size = new FileInfo(file).Length;
                File.Delete(file);
                freed += size;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
        return freed;
    }
}

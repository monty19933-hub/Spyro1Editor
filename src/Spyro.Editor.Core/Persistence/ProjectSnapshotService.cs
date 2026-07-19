using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Persistence;

public sealed record ProjectSnapshotResult(
    string SnapshotPath,
    string Sha256Path,
    string Sha256,
    int FileCount,
    long SourceBytes);

public static class ProjectSnapshotService
{
    private static readonly HashSet<string> ExcludedUnknownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bin", ".cue", ".iso", ".img", ".chd", ".ecm", ".m3u", ".ccd", ".sub", ".toc",
        ".bios", ".rom", ".mcr", ".srm", ".sav", ".state", ".ram", ".dmp", ".dump", ".wad",
        ".exe", ".dll", ".dylib", ".so", ".pdb", ".dbg", ".tmp", ".download"
    };

    public static async Task<ProjectSnapshotResult> CreateAsync(
        EditorProjectLayout project,
        string backupRoot,
        string label,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(project.RootPath))
            throw new DirectoryNotFoundException($"Project folder not found: {project.RootPath}");
        string destinationRoot = Path.GetFullPath(backupRoot);
        PersistencePathSafety.EnsureTreesDoNotOverlap(
            project.RootPath,
            destinationRoot,
            "Project backup storage must be outside and must not contain the project tree.");
        Directory.CreateDirectory(destinationRoot);
        string safeLabel = Sanitize(label);
        string prefix = $"{project.ProjectId}-{safeLabel}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
        string destination = Path.Combine(destinationRoot, $"{prefix}.zip");
        string temporary = $"{destination}.{Guid.NewGuid():N}.tmp";
        IReadOnlyList<PortableProjectArtifact> artifacts = PortableProjectMigration.Inventory(project.RootPath)
            .Where(ShouldInclude)
            .ToArray();
        List<string> files = artifacts.Select(artifact => artifact.SourcePath).ToList();
        if (File.Exists(project.ManifestPath))
            files.Add(project.ManifestPath);
        files = files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        long sourceBytes = files.Sum(path => new FileInfo(path).Length);

        try
        {
            using (ZipArchive archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                foreach (string file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string relative = Path.GetRelativePath(project.RootPath, file).Replace('\\', '/');
                    if (relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                        throw new InvalidOperationException($"Project backup path escaped the project: {file}");
                    ZipArchiveEntry entry = archive.CreateEntry(relative, CompressionLevel.Optimal);
                    await using Stream entryStream = entry.Open();
                    await using FileStream input = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    await input.CopyToAsync(entryStream, cancellationToken);
                }

                ZipArchiveEntry note = archive.CreateEntry("_snapshot-info.txt", CompressionLevel.Optimal);
                await using StreamWriter writer = new(note.Open(), new UTF8Encoding(false));
                await writer.WriteLineAsync("Spyro Editor project safety snapshot");
                await writer.WriteLineAsync($"Created: {DateTimeOffset.UtcNow:O}");
                await writer.WriteLineAsync($"Project: {project.RootPath}");
                await writer.WriteLineAsync("Includes saved edits, imported project assets, settings, user research, preserved conflicts, and future project files.");
                await writer.WriteLineAsync("Generated BIN/CUE output, source game images, WAD analysis, application files, and rebuildable cache are intentionally excluded.");
            }

            using (ZipArchive verify = ZipFile.OpenRead(temporary))
            {
                if (verify.Entries.Count != files.Count + 1)
                    throw new InvalidDataException("The project snapshot did not contain the expected number of files.");
                foreach (string file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string relative = Path.GetRelativePath(project.RootPath, file).Replace('\\', '/');
                    ZipArchiveEntry entry = verify.GetEntry(relative)
                        ?? throw new InvalidDataException($"The project snapshot omitted {relative}.");
                    await using Stream archived = entry.Open();
                    byte[] archivedHash = await SHA256.HashDataAsync(archived, cancellationToken);
                    await using FileStream source = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    byte[] sourceHash = await SHA256.HashDataAsync(source, cancellationToken);
                    if (!archivedHash.AsSpan().SequenceEqual(sourceHash))
                        throw new InvalidDataException($"The project snapshot did not verify against the saved source file: {relative}");
                }

                ZipArchiveEntry note = verify.GetEntry("_snapshot-info.txt")
                    ?? throw new InvalidDataException("The project snapshot is missing its recovery note.");
                await using Stream noteStream = note.Open();
                await noteStream.CopyToAsync(Stream.Null, cancellationToken);
            }
            File.Move(temporary, destination, overwrite: false);
            await using FileStream snapshot = new(destination, FileMode.Open, FileAccess.Read, FileShare.Read);
            string sha256 = Convert.ToHexString(await SHA256.HashDataAsync(snapshot, cancellationToken)).ToLowerInvariant();
            string sha256Path = $"{destination}.sha256";
            await File.WriteAllTextAsync(sha256Path, $"{sha256}  {Path.GetFileName(destination)}\n", new UTF8Encoding(false), cancellationToken);
            return new ProjectSnapshotResult(destination, sha256Path, sha256, files.Count, sourceBytes);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static bool ShouldInclude(PortableProjectArtifact artifact)
    {
        if (artifact.RelativePath.StartsWith("_local/.app/", StringComparison.OrdinalIgnoreCase))
            return false;
        if (artifact.Kind is PortableProjectArtifactKind.ProjectEdit or
            PortableProjectArtifactKind.ProjectAsset or
            PortableProjectArtifactKind.ProjectSetting or
            PortableProjectArtifactKind.UserResearch)
        {
            return true;
        }

        if (artifact.Kind != PortableProjectArtifactKind.Unknown)
            return false;
        string fileName = Path.GetFileName(artifact.RelativePath);
        if (fileName.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("core", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return !ExcludedUnknownExtensions.Contains(Path.GetExtension(fileName));
    }

    private static string Sanitize(string value)
    {
        string cleaned = new((value ?? "snapshot")
            .ToLowerInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-')
            .ToArray());
        while (cleaned.Contains("--", StringComparison.Ordinal))
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
        cleaned = cleaned.Trim('-');
        return string.IsNullOrWhiteSpace(cleaned) ? "snapshot" : cleaned;
    }
}

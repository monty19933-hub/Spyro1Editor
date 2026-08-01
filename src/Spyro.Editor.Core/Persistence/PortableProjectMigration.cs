using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Spyro.Editor.Core.Persistence;

public enum PortableProjectArtifactKind
{
    ProjectEdit,
    ProjectAsset,
    ProjectSetting,
    UserResearch,
    GeneratedOutput,
    RebuildableCache,
    ApplicationFile,
    GameImage,
    Unknown
}

public sealed record PortableProjectArtifact(
    string SourcePath,
    string RelativePath,
    PortableProjectArtifactKind Kind,
    long Length);

public sealed record PortableProjectMigrationOptions(
    bool IncludeGeneratedOutputs = false,
    bool IncludeRebuildableCache = false,
    bool IncludeUserResearch = true);

public sealed record PortableProjectMigrationResult(
    string SourceRoot,
    string DestinationRoot,
    int CopiedCount,
    int ExistingCount,
    int ConflictCount,
    int SkippedCount,
    long CopiedBytes,
    IReadOnlyList<string> ConflictPaths,
    IReadOnlyList<PortableProjectArtifact> Inventory,
    string ReportPath);

public static class PortableProjectMigration
{
    private static readonly string[] ProjectEditSuffixes =
    [
        "-native-edits.json",
        "-native-moby-path-edits.json",
        "-native-level-replacement.json",
        "-terrain-edits.json",
        "-terrain-material-overrides.json",
        "-terrain-behavior-proofs.json",
        "-custom-terrain-textures.json",
        "-native-terrain-texture-relocations.json",
        "-skybox-edit-plan.json",
        "-level-text-edit-plan.json",
        "-level-music-edit-plan.json",
        "-moby-identity-observations.json"
    ];

    public static IReadOnlyList<PortableProjectArtifact> Inventory(string sourceRoot)
    {
        string root = RequireDirectory(sourceRoot);
        return Directory.EnumerateFiles(root, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                IgnoreInaccessible = false
            })
            .Select(path =>
            {
                string relative = NormalizeRelativePath(Path.GetRelativePath(root, path));
                return new PortableProjectArtifact(
                    Path.GetFullPath(path),
                    relative,
                    Classify(relative),
                    new FileInfo(path).Length);
            })
            .OrderBy(artifact => artifact.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static PortableProjectArtifactKind Classify(string relativePath)
    {
        string relative = NormalizeRelativePath(relativePath);
        string fileName = Path.GetFileName(relative);
        string extension = Path.GetExtension(relative);

        if (relative.StartsWith("support/", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("Spyro Editor.app/", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("Launch Spyro Editor", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("README.txt", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase))
        {
            return PortableProjectArtifactKind.ApplicationFile;
        }

        if (extension.Equals(".bin", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".cue", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".iso", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".chd", StringComparison.OrdinalIgnoreCase))
        {
            return relative.StartsWith("output/", StringComparison.OrdinalIgnoreCase)
                ? PortableProjectArtifactKind.GeneratedOutput
                : PortableProjectArtifactKind.GameImage;
        }

        if (relative.StartsWith("output/", StringComparison.OrdinalIgnoreCase))
            return PortableProjectArtifactKind.GeneratedOutput;

        if (relative.StartsWith("editor-cache/", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("spyro-wad-analysis.json", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("_local/terrain/source-derived-overlays/", StringComparison.OrdinalIgnoreCase))
        {
            return PortableProjectArtifactKind.RebuildableCache;
        }

        if (relative.StartsWith("custom-skyboxes/", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("_local/custom-textures/", StringComparison.OrdinalIgnoreCase))
        {
            return PortableProjectArtifactKind.ProjectAsset;
        }

        if (relative.StartsWith("_local/settings/", StringComparison.OrdinalIgnoreCase))
            return PortableProjectArtifactKind.ProjectSetting;

        if (relative.StartsWith("_local/identity-review/", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("_local/control-role-proof-review/", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("_local/identity-observation-answers/", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("_local/terrain-behavior/", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("_local/research/", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("_local/skybox-research/", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("_local/release-qa/", StringComparison.OrdinalIgnoreCase))
        {
            return PortableProjectArtifactKind.UserResearch;
        }

        if (fileName.Equals("ui-text-edit-plan.json", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("moby-identity-observations.json", StringComparison.OrdinalIgnoreCase) ||
            ProjectEditSuffixes.Any(suffix => fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
        {
            return PortableProjectArtifactKind.ProjectEdit;
        }

        if (relative.StartsWith("_local/smoke/", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("_local/terrain/", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("_local/objects/", StringComparison.OrdinalIgnoreCase))
        {
            return PortableProjectArtifactKind.UserResearch;
        }

        return PortableProjectArtifactKind.Unknown;
    }

    public static async Task<PortableProjectMigrationResult> MigrateAsync(
        string sourceRoot,
        EditorProjectLayout destination,
        PortableProjectMigrationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        string source = RequireDirectory(sourceRoot);
        string destinationRoot = Path.GetFullPath(destination.RootPath);
        PersistencePathSafety.EnsureTreesDoNotOverlap(
            source,
            destinationRoot,
            "Portable migration source and project destination must not overlap in either direction, including through symbolic links or junctions.");

        options ??= new PortableProjectMigrationOptions();
        IReadOnlyList<PortableProjectArtifact> inventory = Inventory(source);
        string migrationId = $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}";
        string conflictRoot = Path.Combine(destination.MigrationPath, "conflicts", migrationId);
        PersistencePathSafety.EnsureWritePathWithinRoot(destinationRoot, conflictRoot);
        int copied = 0;
        int existing = 0;
        int conflicts = 0;
        int skipped = 0;
        long copiedBytes = 0;
        List<string> conflictPaths = [];

        Directory.CreateDirectory(destinationRoot);
        HashSet<string> plansBlockedByReferencedAssets = await FindPlansBlockedByReferencedAssetsAsync(
            source,
            destinationRoot,
            inventory,
            options,
            cancellationToken);
        foreach (PortableProjectArtifact artifact in inventory)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ShouldMigrate(artifact.Kind, options))
            {
                skipped++;
                continue;
            }

            string targetPath = SafeDestinationPath(destinationRoot, artifact.RelativePath);
            if (plansBlockedByReferencedAssets.Contains(artifact.RelativePath))
            {
                string blockedPlanConflictPath = SafeDestinationPath(conflictRoot, artifact.RelativePath);
                await PreserveConflictAsync(artifact, source, conflictRoot, blockedPlanConflictPath, cancellationToken);
                conflictPaths.Add(blockedPlanConflictPath);
                conflicts++;
                continue;
            }
            if (!File.Exists(targetPath))
            {
                await CopyFileAsync(artifact.SourcePath, targetPath, cancellationToken);
                await RebaseProjectPathAsync(source, destinationRoot, targetPath, cancellationToken);
                copied++;
                copiedBytes += artifact.Length;
                continue;
            }

            if (await FilesEquivalentAsync(artifact, targetPath, source, destinationRoot, cancellationToken))
            {
                await RebaseProjectPathAsync(source, destinationRoot, targetPath, cancellationToken);
                existing++;
                continue;
            }

            string conflictPath = SafeDestinationPath(conflictRoot, artifact.RelativePath);
            await PreserveConflictAsync(artifact, source, conflictRoot, conflictPath, cancellationToken);
            conflictPaths.Add(conflictPath);
            conflicts++;
        }

        PersistencePathSafety.EnsureWritePathWithinRoot(destinationRoot, destination.MigrationPath);
        Directory.CreateDirectory(destination.MigrationPath);
        string reportPath = Path.Combine(destination.MigrationPath, $"portable-migration-{migrationId}.json");
        PersistencePathSafety.EnsureWritePathWithinRoot(destinationRoot, reportPath);
        PortableProjectMigrationResult result = new(
            SourceRoot: source,
            DestinationRoot: destinationRoot,
            CopiedCount: copied,
            ExistingCount: existing,
            ConflictCount: conflicts,
            SkippedCount: skipped,
            CopiedBytes: copiedBytes,
            ConflictPaths: conflictPaths,
            Inventory: inventory,
            ReportPath: reportPath);
        await EditorUserDataLayout.WriteJsonAtomicallyAsync(reportPath, result, cancellationToken);
        return result;
    }

    private static async Task<HashSet<string>> FindPlansBlockedByReferencedAssetsAsync(
        string sourceRoot,
        string destinationRoot,
        IReadOnlyList<PortableProjectArtifact> inventory,
        PortableProjectMigrationOptions options,
        CancellationToken cancellationToken)
    {
        Dictionary<string, PortableProjectArtifact> byRelativePath = inventory.ToDictionary(
            artifact => artifact.RelativePath,
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> blocked = new(StringComparer.OrdinalIgnoreCase);
        foreach (PortableProjectArtifact plan in inventory.Where(artifact =>
                     ShouldMigrate(artifact.Kind, options) &&
                     IsPathBearingProjectManifest(artifact.RelativePath)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonNode? root;
            try
            {
                root = JsonNode.Parse(await File.ReadAllTextAsync(plan.SourcePath, cancellationToken));
            }
            catch (JsonException)
            {
                continue;
            }

            if (root == null)
                continue;
            foreach (string referencedPath in EnumerateProjectAssetPaths(root))
            {
                if (!TryRelativePathWithinRoot(sourceRoot, referencedPath, out string relativePath))
                    continue;
                string sourceAssetPath = Path.GetFullPath(referencedPath);
                if (!File.Exists(sourceAssetPath) ||
                    !byRelativePath.TryGetValue(relativePath, out PortableProjectArtifact? asset) ||
                    !ShouldMigrate(asset.Kind, options))
                {
                    blocked.Add(plan.RelativePath);
                    break;
                }

                string targetAssetPath = SafeDestinationPath(destinationRoot, relativePath);
                if (File.Exists(targetAssetPath) &&
                    !await FilesRawEqualAsync(sourceAssetPath, targetAssetPath, cancellationToken))
                {
                    blocked.Add(plan.RelativePath);
                    break;
                }
            }
        }
        return blocked;
    }

    private static bool ShouldMigrate(PortableProjectArtifactKind kind, PortableProjectMigrationOptions options)
    {
        return kind switch
        {
            PortableProjectArtifactKind.ProjectEdit => true,
            PortableProjectArtifactKind.ProjectAsset => true,
            PortableProjectArtifactKind.ProjectSetting => true,
            PortableProjectArtifactKind.UserResearch => options.IncludeUserResearch,
            PortableProjectArtifactKind.GeneratedOutput => options.IncludeGeneratedOutputs,
            PortableProjectArtifactKind.RebuildableCache => options.IncludeRebuildableCache,
            _ => false
        };
    }

    private static async Task RebaseProjectPathAsync(
        string sourceRoot,
        string destinationRoot,
        string path,
        CancellationToken cancellationToken)
    {
        if (!IsPathBearingProjectManifest(path))
            return;

        cancellationToken.ThrowIfCancellationRequested();
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(await File.ReadAllTextAsync(path, cancellationToken));
        }
        catch (JsonException)
        {
            return;
        }

        if (root == null || !RebaseNode(root, sourceRoot, destinationRoot))
            return;
        await EditorUserDataLayout.WriteJsonAtomicallyAsync(path, root, cancellationToken);
    }

    private static bool IsPathBearingProjectManifest(string path)
    {
        string fileName = Path.GetFileName(path);
        return fileName.EndsWith("-skybox-edit-plan.json", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith("-custom-terrain-textures.json", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith("-native-terrain-texture-relocations.json", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task PreserveConflictAsync(
        PortableProjectArtifact artifact,
        string sourceRoot,
        string conflictRoot,
        string conflictPath,
        CancellationToken cancellationToken)
    {
        await CopyConflictFileAsync(artifact.SourcePath, conflictPath, cancellationToken);
        if (!IsPathBearingProjectManifest(artifact.RelativePath))
            return;

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(await File.ReadAllTextAsync(conflictPath, cancellationToken));
        }
        catch (JsonException)
        {
            return;
        }

        if (root == null)
            return;
        foreach (string referencedPath in EnumerateProjectAssetPaths(root))
        {
            if (!TryRelativePathWithinRoot(sourceRoot, referencedPath, out string relativePath))
                continue;
            string sourceAssetPath = Path.GetFullPath(referencedPath);
            if (!File.Exists(sourceAssetPath))
                continue;
            string conflictAssetPath = SafeDestinationPath(conflictRoot, relativePath);
            await CopyConflictFileAsync(sourceAssetPath, conflictAssetPath, cancellationToken);
        }

        if (RebaseNode(root, sourceRoot, conflictRoot))
            await EditorUserDataLayout.WriteJsonAtomicallyAsync(conflictPath, root, cancellationToken);
    }

    private static IEnumerable<string> EnumerateProjectAssetPaths(JsonNode node)
    {
        if (node is JsonObject jsonObject)
        {
            foreach ((string propertyName, JsonNode? value) in jsonObject)
            {
                if (value is System.Text.Json.Nodes.JsonValue jsonValue &&
                    (propertyName.Equals("importedSkyPath", StringComparison.OrdinalIgnoreCase) ||
                        propertyName.Equals("sourceImagePath", StringComparison.OrdinalIgnoreCase) ||
                        propertyName.Equals("previewImagePath", StringComparison.OrdinalIgnoreCase)) &&
                    jsonValue.TryGetValue(out string? path) &&
                    !string.IsNullOrWhiteSpace(path))
                {
                    yield return path;
                }
                else if (value != null)
                {
                    foreach (string nested in EnumerateProjectAssetPaths(value))
                        yield return nested;
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (JsonNode? value in jsonArray)
            {
                if (value == null)
                    continue;
                foreach (string nested in EnumerateProjectAssetPaths(value))
                    yield return nested;
            }
        }
    }

    private static bool RebaseNode(JsonNode node, string sourceRoot, string destinationRoot)
    {
        bool changed = false;
        if (node is JsonObject jsonObject)
        {
            foreach ((string propertyName, JsonNode? value) in jsonObject.ToArray())
            {
                if (value is System.Text.Json.Nodes.JsonValue jsonValue &&
                    (propertyName.Equals("importedSkyPath", StringComparison.OrdinalIgnoreCase) ||
                        propertyName.Equals("sourceImagePath", StringComparison.OrdinalIgnoreCase) ||
                        propertyName.Equals("previewImagePath", StringComparison.OrdinalIgnoreCase)) &&
                    jsonValue.TryGetValue(out string? path) &&
                    !string.IsNullOrWhiteSpace(path) &&
                    TryRelativePathWithinRoot(sourceRoot, path, out string relative))
                {
                    jsonObject[propertyName] = SafeDestinationPath(destinationRoot, relative);
                    changed = true;
                }
                else if (value != null)
                {
                    changed = RebaseNode(value, sourceRoot, destinationRoot) || changed;
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (JsonNode? value in jsonArray)
            {
                if (value != null)
                    changed = RebaseNode(value, sourceRoot, destinationRoot) || changed;
            }
        }

        return changed;
    }

    private static bool TryRelativePathWithinRoot(string root, string candidate, out string relative)
    {
        relative = "";
        if (!Path.IsPathRooted(candidate))
            return false;

        string fullRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
        string fullCandidate;
        try
        {
            fullCandidate = Path.GetFullPath(candidate);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }

        StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!fullCandidate.StartsWith(fullRoot, comparison))
            return false;

        relative = NormalizeRelativePath(Path.GetRelativePath(root, fullCandidate));
        return true;
    }

    private static async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");
        string temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using FileStream input = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using (FileStream output = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await input.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            File.Move(temporaryPath, destinationPath, overwrite: false);
            if (new FileInfo(destinationPath).Length != new FileInfo(sourcePath).Length)
            {
                File.Delete(destinationPath);
                throw new IOException($"Migrated file length does not match its source: {destinationPath}");
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static async Task CopyConflictFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(destinationPath))
        {
            await CopyFileAsync(sourcePath, destinationPath, cancellationToken);
            return;
        }

        if (!await FilesRawEqualAsync(sourcePath, destinationPath, cancellationToken))
            throw new IOException($"Migration conflict snapshot already contains different data: {destinationPath}");
    }

    private static async Task<bool> FilesEquivalentAsync(
        PortableProjectArtifact source,
        string destinationPath,
        string sourceRoot,
        string destinationRoot,
        CancellationToken cancellationToken)
    {
        if (await FilesRawEqualAsync(source.SourcePath, destinationPath, cancellationToken))
            return true;

        if (IsPathBearingProjectManifest(source.RelativePath))
        {
            try
            {
                JsonNode? sourceNode = JsonNode.Parse(await File.ReadAllTextAsync(source.SourcePath, cancellationToken));
                JsonNode? destinationNode = JsonNode.Parse(await File.ReadAllTextAsync(destinationPath, cancellationToken));
                if (sourceNode != null && destinationNode != null)
                {
                    RebaseNode(sourceNode, sourceRoot, destinationRoot);
                    return JsonNode.DeepEquals(sourceNode, destinationNode);
                }
            }
            catch (JsonException)
            {
            }
        }
        return false;
    }

    private static async Task<bool> FilesRawEqualAsync(string left, string right, CancellationToken cancellationToken)
    {
        FileInfo leftInfo = new(left);
        FileInfo rightInfo = new(right);
        if (leftInfo.Length != rightInfo.Length)
            return false;

        await using FileStream leftStream = new(left, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using FileStream rightStream = new(right, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] leftHash = await SHA256.HashDataAsync(leftStream, cancellationToken);
        byte[] rightHash = await SHA256.HashDataAsync(rightStream, cancellationToken);
        return leftHash.AsSpan().SequenceEqual(rightHash);
    }

    private static string SafeDestinationPath(string destinationRoot, string relativePath)
    {
        string root = EnsureTrailingSeparator(Path.GetFullPath(destinationRoot));
        string platformRelative = NormalizeRelativePath(relativePath).Replace('/', Path.DirectorySeparatorChar);
        string destination = Path.GetFullPath(Path.Combine(root, platformRelative));
        StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!destination.StartsWith(root, comparison))
            throw new InvalidOperationException($"Migration path escapes its destination root: {relativePath}");
        PersistencePathSafety.EnsureWritePathWithinRoot(
            destinationRoot,
            destination,
            $"Migration path follows a symbolic link or junction outside its destination root: {relativePath}");
        return destination;
    }

    private static string RequireDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A portable installation path is required.", nameof(path));
        string fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Portable installation not found: {fullPath}");
        return fullPath;
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;
    }
}

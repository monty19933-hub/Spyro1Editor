using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeLevelReplacementBaselineRequest(
    NativeLevelReplacementManifest Manifest,
    LevelCatalog Catalog,
    string SourceImagePath,
    string SourceCuePath,
    string OutputImagePath,
    string OutputCuePath);

public sealed record NativeLevelReplacementBaselineResult(
    string OutputImagePath,
    string OutputCuePath,
    string SourceSha256,
    string OutputSha256,
    bool ByteIdentical,
    bool WadHeaderIdentical,
    bool MetadataEntryIdentical,
    bool DataEntryIdentical,
    bool NestedHeaderIdentical,
    bool LevelDataIdentical,
    bool MobyTableIdentical,
    bool PortalAndReturnHomePreimagesIdentical,
    NativeLevelReplacementSafetyReport Safety);

public static class NativeLevelReplacementBaselineExporter
{
    private const int MaximumCueBytes = 64 * 1024;

    public static async Task<NativeLevelReplacementBaselineResult> ExportAsync(
        NativeLevelReplacementBaselineRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Manifest);
        ArgumentNullException.ThrowIfNull(request.Catalog);
        NativeLevelReplacementBaselinePlan plan =
            NativeLevelReplacementSafetyInspector.BuildBaselinePlan(request.Manifest, request.Catalog);
        if (plan.PlannedWrites.Count != 0)
            throw new InvalidOperationException("The first Stone Hill baseline cannot contain byte patches.");

        string sourceImage = RequireExistingFile(request.SourceImagePath, "source BIN");
        string sourceCue = RequireExistingFile(request.SourceCuePath, "source CUE");
        string outputImage = Path.GetFullPath(request.OutputImagePath);
        string outputCue = Path.GetFullPath(request.OutputCuePath);
        EnsureDistinctRoles(sourceImage, sourceCue, outputImage, outputCue);
        string outputDirectory = Path.GetDirectoryName(outputImage)
            ?? throw new InvalidOperationException("The output BIN needs a parent directory.");
        string outputCueDirectory = Path.GetDirectoryName(outputCue)
            ?? throw new InvalidOperationException("The output CUE needs a parent directory.");
        if (!PathsEqual(CanonicalizePath(outputDirectory), CanonicalizePath(outputCueDirectory)))
            throw new InvalidOperationException("The Stone Hill baseline BIN and CUE must be written to the same directory.");
        Directory.CreateDirectory(outputDirectory);

        ValidateCue(sourceCue, sourceImage, expectedMode: "MODE2/2352");
        NativeLevelReplacementSourceBinding sourceBinding =
            await NativeLevelReplacementStore.ValidateSourceAsync(
                request.Manifest,
                sourceImage,
                request.Catalog,
                cancellationToken);

        string operationId = Guid.NewGuid().ToString("N");
        string temporaryImage = Path.Combine(outputDirectory, $".{Path.GetFileName(outputImage)}.{operationId}.tmp");
        string temporaryCue = Path.Combine(outputDirectory, $".{Path.GetFileName(outputCue)}.{operationId}.tmp");
        string backupImage = Path.Combine(outputDirectory, $".{Path.GetFileName(outputImage)}.{operationId}.bak");
        string backupCue = Path.Combine(outputDirectory, $".{Path.GetFileName(outputCue)}.{operationId}.bak");
        bool imageBackedUp = false;
        bool cueBackedUp = false;
        bool imagePublished = false;
        bool cuePublished = false;
        try
        {
            await DiscImageWorkingCopy.StageAsync(
                sourceImage,
                temporaryImage,
                consumeDisposableSource: false,
                cancellationToken);
            NativeLevelReplacementSourceBinding stagedBinding =
                await NativeLevelReplacementStore.ValidateSourceAsync(
                    request.Manifest,
                    temporaryImage,
                    request.Catalog,
                    cancellationToken);

            string cueText = DiscImage.BuildCueText(sourceCue, Path.GetFileName(outputImage));
            await File.WriteAllTextAsync(temporaryCue, cueText, Encoding.ASCII, cancellationToken);
            ValidateCue(temporaryCue, outputImage, expectedMode: "MODE2/2352");

            if (File.Exists(outputImage))
            {
                File.Move(outputImage, backupImage);
                imageBackedUp = true;
            }
            if (File.Exists(outputCue))
            {
                File.Move(outputCue, backupCue);
                cueBackedUp = true;
            }
            File.Move(temporaryImage, outputImage);
            imagePublished = true;
            File.Move(temporaryCue, outputCue);
            cuePublished = true;

            string finalSha256 = await HashFileAsync(outputImage, cancellationToken);
            if (!string.Equals(finalSha256, sourceBinding.SourceImageSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The published no-edit Stone Hill baseline changed the source disc bytes.");
            ValidateCue(outputCue, outputImage, expectedMode: "MODE2/2352");

            TryDelete(backupImage);
            TryDelete(backupCue);
            return new NativeLevelReplacementBaselineResult(
                outputImage,
                outputCue,
                sourceBinding.SourceImageSha256,
                finalSha256,
                ByteIdentical: true,
                WadHeaderIdentical: sourceBinding.WadArchiveHeaderSha256 == stagedBinding.WadArchiveHeaderSha256,
                MetadataEntryIdentical: sourceBinding.LevelMetadataEntry == stagedBinding.LevelMetadataEntry,
                DataEntryIdentical: sourceBinding.LevelDataEntry == stagedBinding.LevelDataEntry,
                NestedHeaderIdentical: sourceBinding.NestedHeaderSha256 == stagedBinding.NestedHeaderSha256 &&
                    sourceBinding.NestedDescriptorTableSha256 == stagedBinding.NestedDescriptorTableSha256,
                LevelDataIdentical: sourceBinding.LevelDataSubfile == stagedBinding.LevelDataSubfile,
                MobyTableIdentical: sourceBinding.SourceMobyTableSha256 == stagedBinding.SourceMobyTableSha256,
                PortalAndReturnHomePreimagesIdentical:
                    sourceBinding.ArtisansPortalControlRows.SequenceEqual(stagedBinding.ArtisansPortalControlRows) &&
                    sourceBinding.StoneHillReturnHomeRows.SequenceEqual(stagedBinding.StoneHillReturnHomeRows),
                plan.Safety);
        }
        catch (Exception exportFailure)
        {
            if (cuePublished)
                TryDelete(outputCue);
            if (imagePublished)
                TryDelete(outputImage);

            List<Exception> recoveryFailures = [];
            Exception? imageRecoveryFailure = TryRestoreBackup(
                "BIN",
                backupImage,
                outputImage,
                ref imageBackedUp);
            if (imageRecoveryFailure != null)
                recoveryFailures.Add(imageRecoveryFailure);
            Exception? cueRecoveryFailure = TryRestoreBackup(
                "CUE",
                backupCue,
                outputCue,
                ref cueBackedUp);
            if (cueRecoveryFailure != null)
                recoveryFailures.Add(cueRecoveryFailure);
            if (recoveryFailures.Count > 0)
            {
                throw new IOException(
                    "The Stone Hill baseline failed and one or more previous outputs could not be restored. " +
                    "The recovery error names every retained backup path; do not delete those files.",
                    new AggregateException([exportFailure, .. recoveryFailures]));
            }
            throw;
        }
        finally
        {
            TryDelete(temporaryImage);
            TryDelete(temporaryCue);
            if (!imageBackedUp)
                TryDelete(backupImage);
            if (!cueBackedUp)
                TryDelete(backupCue);
        }
    }

    private static void EnsureDistinctRoles(
        string sourceImage,
        string sourceCue,
        string outputImage,
        string outputCue)
    {
        (string Label, string Path)[] roles =
        [
            ("source BIN", CanonicalizePath(sourceImage)),
            ("source CUE", CanonicalizePath(sourceCue)),
            ("output BIN", CanonicalizePath(outputImage)),
            ("output CUE", CanonicalizePath(outputCue))
        ];
        IGrouping<string, (string Label, string Path)>? collision = roles
            .GroupBy(role => role.Path, PathComparer)
            .FirstOrDefault(group => group.Count() > 1);
        if (collision != null)
            throw new InvalidOperationException($"Stone Hill baseline file roles alias each other: {string.Join(", ", collision.Select(role => role.Label))}.");
    }

    private static void ValidateCue(string cuePath, string expectedImagePath, string expectedMode)
    {
        FileInfo info = new(cuePath);
        if (!info.Exists || info.Length <= 0 || info.Length > MaximumCueBytes)
            throw new InvalidDataException("The baseline source/output CUE must be a small one-BIN text file.");
        string[] lines = File.ReadAllLines(cuePath, Encoding.ASCII);
        string[] fileLines = lines.Where(line => line.TrimStart().StartsWith("FILE ", StringComparison.OrdinalIgnoreCase)).ToArray();
        string[] trackLines = lines.Where(line => line.TrimStart().StartsWith("TRACK ", StringComparison.OrdinalIgnoreCase)).ToArray();
        string[] indexLines = lines.Where(line => line.TrimStart().StartsWith("INDEX ", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (fileLines.Length != 1 || trackLines.Length != 1 || indexLines.Length != 1 ||
            !string.Equals(NormalizeCueDirective(trackLines[0]), $"TRACK 01 {expectedMode}", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(NormalizeCueDirective(indexLines[0]), "INDEX 01 00:00:00", StringComparison.OrdinalIgnoreCase) ||
            !NormalizeCueDirective(fileLines[0]).EndsWith(" BINARY", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The Stone Hill baseline requires one {expectedMode} BIN track.");
        string fileName = ParseCueFileName(fileLines[0]);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidDataException("The CUE does not contain a readable BIN filename.");
        string referenced = Path.IsPathRooted(fileName)
            ? fileName
            : Path.Combine(Path.GetDirectoryName(cuePath) ?? ".", fileName);
        if (!PathsEqual(CanonicalizePath(referenced), CanonicalizePath(expectedImagePath)))
            throw new InvalidDataException("The CUE does not point to its expected BIN.");
    }

    private static string ParseCueFileName(string line)
    {
        string text = line.Trim();
        int firstQuote = text.IndexOf('"');
        if (firstQuote >= 0)
        {
            int secondQuote = text.IndexOf('"', firstQuote + 1);
            return secondQuote > firstQuote ? text[(firstQuote + 1)..secondQuote] : "";
        }
        string remainder = text.Length > 5 ? text[5..].Trim() : "";
        int binary = remainder.LastIndexOf(" BINARY", StringComparison.OrdinalIgnoreCase);
        return binary >= 0 ? remainder[..binary].Trim() : remainder;
    }

    private static string NormalizeCueDirective(string line) =>
        string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    internal static Exception? TryRestoreBackup(
        string label,
        string backupPath,
        string outputPath,
        ref bool backupPending)
    {
        if (!backupPending)
            return null;
        if (!File.Exists(backupPath))
        {
            return new FileNotFoundException(
                $"The previous {label} output backup is missing and cannot be restored: {backupPath}",
                backupPath);
        }

        try
        {
            File.Move(backupPath, outputPath, overwrite: true);
            backupPending = false;
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new IOException(
                $"The previous {label} output could not be restored. Its backup remains at: {backupPath}",
                exception);
        }
    }

    private static string RequireExistingFile(string path, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"The {label} does not exist.", fullPath);
        return fullPath;
    }

    private static string CanonicalizePath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath)
            ?? throw new InvalidOperationException($"A rooted path is required: {path}");
        string current = root;
        foreach (string segment in Path.GetRelativePath(root, fullPath).Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(current, segment);
            FileSystemInfo entry = Directory.Exists(candidate)
                ? new DirectoryInfo(candidate)
                : new FileInfo(candidate);
            if ((entry.Exists || !string.IsNullOrWhiteSpace(entry.LinkTarget)) &&
                !string.IsNullOrWhiteSpace(entry.LinkTarget))
            {
                current = entry.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                    ?? throw new IOException($"The path link cannot be resolved: {candidate}");
            }
            else
            {
                current = candidate;
            }
        }
        return Path.GetFullPath(current);
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), PathComparison);

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}

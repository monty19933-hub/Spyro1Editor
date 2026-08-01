using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeLockedChestResearchArtifactRequest(
    string DestinationLevelKey,
    string SourceImagePath,
    string SourceCuePath,
    string OutputPrefix,
    string WadAnalysisPath,
    string LevelCatalogRootPath,
    bool EnableFastEntry = true);

public sealed record NativeLockedChestResearchArtifactResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputPlanPath,
    string OutputChecklistPath,
    NativeLockedChestResearchCandidateResult Candidate,
    bool Verified);

/// <summary>
/// Writes the complete disposable artifact set for an already checked, destination-specific
/// Key + Locked Chest research candidate. This is deliberately separate from normal Create BIN
/// and does not make a candidate profile available to normal object placement/export.
/// </summary>
public static class NativeLockedChestResearchArtifactWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    public const string ResearchOnlyNotice =
        "RESEARCH ONLY - this disposable CUE is not part of normal Add/Create BIN and is not a project build.";

    public static bool CanWrite(string destinationLevelKey)
    {
        string key = LevelCatalog.NormalizeKey(destinationLevelKey);
        return key != "stonehill" && NativeLockedChestResearchCandidateExporter.CanExport(key);
    }

    public static async Task<NativeLockedChestResearchArtifactResult> WriteAsync(
        NativeLockedChestResearchArtifactRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        string key = LevelCatalog.NormalizeKey(request.DestinationLevelKey);
        string sourceImagePath = Path.GetFullPath(request.SourceImagePath);
        string sourceCuePath = Path.GetFullPath(request.SourceCuePath);
        string outputPrefix = Path.GetFullPath(request.OutputPrefix);
        string outputImagePath = outputPrefix + ".bin";
        string outputCuePath = outputPrefix + ".cue";
        string outputPlanPath = outputPrefix + ".plan.json";
        string outputChecklistPath = outputPrefix + ".runtime-checklist.md";
        string wadAnalysisPath = Path.GetFullPath(request.WadAnalysisPath);
        string levelCatalogRootPath = Path.GetFullPath(request.LevelCatalogRootPath);

        if (PathsEqual(sourceImagePath, outputImagePath) || PathsEqual(sourceCuePath, outputCuePath))
            throw new InvalidOperationException("The disposable test output cannot overwrite the selected retail BIN/CUE.");

        cancellationToken.ThrowIfCancellationRequested();
        string sourceSha256 = await ComputeSha256Async(sourceImagePath, cancellationToken);
        if (!string.Equals(
                sourceSha256,
                LockedChestRuntimeBundleProfileCatalog.CleanUsaImageSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Disposable Key + Locked Chest tests require the clean Spyro the Dragon USA retail BIN. " +
                $"Selected SHA-256: {sourceSha256}.");
        }

        string? outputDirectory = Path.GetDirectoryName(outputPrefix);
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new InvalidOperationException("The disposable test output folder could not be resolved.");
        Directory.CreateDirectory(outputDirectory);

        string[] artifacts = [outputImagePath, outputCuePath, outputPlanPath, outputChecklistPath];
        foreach (string artifact in artifacts)
            DeleteIfExists(artifact);

        try
        {
            NativeLockedChestResearchCandidateResult candidate = await Task.Run(
                () => NativeLockedChestResearchCandidateExporter.Export(
                    new NativeLockedChestResearchCandidateRequest(
                        DestinationLevelKey: key,
                        SourceImagePath: sourceImagePath,
                        OutputImagePath: outputImagePath,
                        WadAnalysisPath: wadAnalysisPath,
                        LevelCatalogRootPath: levelCatalogRootPath,
                        EnableFastEntry: request.EnableFastEntry)),
                cancellationToken);

            if (!candidate.Verified || !File.Exists(candidate.OutputImagePath))
                throw new InvalidDataException("The destination-specific candidate did not pass its guarded final-image readback.");
            if (!PathsEqual(candidate.OutputImagePath, outputImagePath))
                throw new InvalidDataException("The destination-specific candidate wrote an unexpected output path.");

            cancellationToken.ThrowIfCancellationRequested();
            string cueText = DiscImage.BuildCueText(sourceCuePath, Path.GetFileName(outputImagePath));
            await File.WriteAllTextAsync(outputCuePath, cueText, Encoding.ASCII, cancellationToken);
            await File.WriteAllTextAsync(
                outputPlanPath,
                JsonSerializer.Serialize(candidate.Plan, new JsonSerializerOptions { WriteIndented = true }),
                Utf8WithoutBom,
                cancellationToken);
            await File.WriteAllTextAsync(
                outputChecklistPath,
                BuildChecklist(candidate, outputCuePath, outputPlanPath),
                Utf8WithoutBom,
                cancellationToken);

            if (!File.Exists(outputCuePath) || !File.Exists(outputPlanPath) || !File.Exists(outputChecklistPath))
                throw new IOException("The disposable candidate artifact set was not written completely.");

            return new NativeLockedChestResearchArtifactResult(
                outputImagePath,
                outputCuePath,
                outputPlanPath,
                outputChecklistPath,
                candidate,
                Verified: true);
        }
        catch
        {
            foreach (string artifact in artifacts)
                DeleteIfExists(artifact);
            throw;
        }
    }

    private static void ValidateRequest(NativeLockedChestResearchArtifactRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationLevelKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceImagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceCuePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WadAnalysisPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.LevelCatalogRootPath);

        string key = LevelCatalog.NormalizeKey(request.DestinationLevelKey);
        if (key == "stonehill")
        {
            throw new NotSupportedException(
                "Stone Hill already has a native working Key + Locked Chest and is never a disposable transplant destination.");
        }
        if (!CanWrite(key))
        {
            throw new NotSupportedException(
                $"{request.DestinationLevelKey} does not have a checked disposable Key + Locked Chest exporter.");
        }
        if (!File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("The selected clean retail BIN was not found.", request.SourceImagePath);
        if (!File.Exists(request.SourceCuePath))
            throw new FileNotFoundException("The selected clean retail CUE was not found.", request.SourceCuePath);
        if (!File.Exists(request.WadAnalysisPath))
            throw new FileNotFoundException("The selected disc's WAD analysis was not found.", request.WadAnalysisPath);
        if (!File.Exists(Path.Combine(request.LevelCatalogRootPath, "spyro-level-catalog.json")))
            throw new FileNotFoundException(
                "The level catalog required by the guarded candidate writer was not found.",
                Path.Combine(request.LevelCatalogRootPath, "spyro-level-catalog.json"));
    }

    private static string BuildChecklist(
        NativeLockedChestResearchCandidateResult candidate,
        string outputCuePath,
        string outputPlanPath)
    {
        NativeLockedChestResearchCandidatePlan plan = candidate.Plan;
        string fastEntry = plan.FastEntryEnabled
            ? $"The disposable test includes the guarded fast-entry patch: `{plan.FastEntryInstructions}`"
            : $"Enter {plan.DestinationLevelName} through normal gameplay.";

        return $$"""
            # {{plan.DestinationLevelName}} Key + Locked Chest disposable runtime checklist

            > **{{ResearchOnlyNotice}}**

            Mount `{{Path.GetFileName(outputCuePath)}}` in DuckStation and cold boot it. Keep this test separate from project edits and normal Create BIN output.

            ## Enter the destination

            {{fastEntry}}

            ## Required runtime evidence

            - The disc cold boots and reaches {{plan.DestinationLevelName}} without a black screen, freeze, or GTE assertion.
            - The candidate Key is gold, collectible once, and remains collected after death/reload.
            - The Locked Chest uses the retail metal model and textures.
            - The chest cannot open before collecting the Key.
            - After collecting the Key, the chest opens once with native animation and sound.
            - Exactly six gems worth `+10` appear once; the level treasure target moves from {{plan.TreasureTargetBefore}} to {{plan.TreasureTargetAfter}}.
            - The chest and reward do not repeat after death, reload, or leave/re-enter.
            - Nearby native actors, the level objective, portals, and ordinary rewards remain normal.

            Do not treat a successful boot as completion. Record all checks above in DuckStation before this destination can be considered for editor promotion.

            ## Static artifact proof

            - Recipe: `{{plan.RecipeId}}`
            - BIN SHA-256: `{{plan.OutputImageSha256}}`
            - Handler SHA-256: `{{plan.HandlerPayloadSha256}}`
            - Rebased package SHA-256: `{{plan.RebasedLockedChestPackageSha256}}`
            - Guarded readback: {{candidate.Verification}}
            - Full structural plan: `{{Path.GetFileName(outputPlanPath)}}`
            """;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            useAsync: true);
        byte[] digest = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}

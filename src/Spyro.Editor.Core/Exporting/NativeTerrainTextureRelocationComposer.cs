using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeTerrainTextureRelocationExportRequest(
    string SourceImagePath,
    string SourceCuePath,
    string OutputPrefix,
    LevelDefinition TargetLevel,
    IReadOnlyList<NativeTerrainTextureRelocationImport> Imports,
    NativeTexturePageExternalOwnershipProof OwnershipProof,
    bool WriteImage = true);

public sealed record NativeTerrainTextureRelocationExportPlan(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    string SourceImageSha256,
    string OutputImagePath,
    string OutputCuePath,
    string OutputPlanPath,
    string TargetLevelKey,
    string TargetLevelName,
    int TargetWadEntry,
    NativeTerrainTextureRuntimeControlAudit RuntimeControlAudit,
    NativeTerrainTextureRelocationPlan Relocation,
    int PatchCount,
    int PatchedByteCount,
    bool RuntimeTargetsPersistent,
    bool ExactLogicalReadbackRequired,
    IReadOnlyList<string> Notes);

public sealed record NativeTerrainTextureRelocationExportResult(
    NativeTerrainTextureRelocationExportPlan Plan,
    bool WroteImage,
    string OutputImageSha256);

/// <summary>
/// Composes the complete native terrain-texture relocation allocator with the
/// level's runtime animation/scrolling audit and final BIN readback. Callers
/// must supply a source-bound all-consumer ownership proof; this class never
/// manufactures or weakens one.
/// </summary>
public static class NativeTerrainTextureRelocationComposer
{
    private const int WadLba = 37;

    public static NativeTerrainTextureRelocationExportPlan BuildPlan(
        NativeTerrainTextureRelocationExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.TargetLevel);
        ArgumentNullException.ThrowIfNull(request.OwnershipProof);
        if (!File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", request.SourceImagePath);
        if (request.Imports == null || request.Imports.Count == 0)
            throw new ArgumentException("At least one native terrain texture relocation import is required.", nameof(request));
        if (request.Imports.Any(importItem =>
                !string.Equals(importItem.DescriptorTier, "both", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(importItem.DescriptorTier, "all", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(importItem.DescriptorTier, "complete", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Native relocation must carry the complete 23-descriptor record. Partial HQ-only imports are runtime-incomplete.");
        }

        NativeTerrainTextureRuntimeControlAudit runtimeAudit =
            NativeTerrainTextureRuntimeControlScanner.Inspect(request.SourceImagePath, request.TargetLevel);
        if (!runtimeAudit.Complete)
        {
            throw new InvalidOperationException(
                $"Runtime texture-control audit is incomplete for {request.TargetLevel.DisplayName}: " +
                string.Join(" ", runtimeAudit.SafetyBlockers));
        }

        int[] controlledTargets = request.Imports
            .Select(importItem => importItem.TargetTextureId)
            .Distinct()
            .Where(textureId => !runtimeAudit.IsRuntimePersistentTarget(textureId))
            .Order()
            .ToArray();
        if (controlledTargets.Length > 0)
        {
            string details = string.Join(" ", controlledTargets.Select(runtimeAudit.TargetReadinessNote));
            throw new InvalidOperationException(
                $"Native terrain texture relocation cannot use runtime-controlled target record(s) {string.Join(", ", controlledTargets)}. {details}");
        }

        if (!NativeTerrainTextureRelocationAllocator.TryBuild(
                request.SourceImagePath,
                request.TargetLevel,
                request.Imports,
                request.OwnershipProof,
                out NativeTerrainTextureRelocationPlan? relocation,
                out string failureReason) ||
            relocation == null)
        {
            throw new InvalidOperationException(
                $"Native terrain texture relocation proof/build failed for {request.TargetLevel.DisplayName}: {failureReason}");
        }

        if (!relocation.ExactDonorIndexedPixelsVerified ||
            !relocation.ExactDonorPalettesVerified ||
            !relocation.LowDetailAliasPreserved ||
            !relocation.LogicalReadbackVerified ||
            !relocation.ProtectedStorageVerified ||
            !relocation.TargetDescriptorMaterialPolicyVerified)
        {
            throw new InvalidOperationException(
                "The relocation allocator returned a plan without every required indexed-pixel, palette, LQ-alias, logical-readback, protected-storage, and target descriptor-material policy proof.");
        }

        ValidatePatchSet(request.SourceImagePath, relocation.Patches);

        string outputPrefix = string.IsNullOrWhiteSpace(request.OutputPrefix)
            ? Path.Combine(
                Path.GetDirectoryName(request.SourceImagePath) ?? "",
                $"SpyroEditor-{request.TargetLevel.Key}-native-terrain-texture-relocation")
            : request.OutputPrefix;
        string outputImagePath = $"{outputPrefix}.bin";
        string outputCuePath = $"{outputPrefix}.cue";
        string outputPlanPath = $"{outputPrefix}.native-terrain-texture-relocation.json";
        return new NativeTerrainTextureRelocationExportPlan(
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceImagePath: request.SourceImagePath,
            SourceImageSha256: ComputeSha256(request.SourceImagePath),
            OutputImagePath: outputImagePath,
            OutputCuePath: outputCuePath,
            OutputPlanPath: outputPlanPath,
            TargetLevelKey: request.TargetLevel.Key,
            TargetLevelName: request.TargetLevel.DisplayName,
            TargetWadEntry: request.TargetLevel.SourceWadEntry,
            RuntimeControlAudit: runtimeAudit,
            Relocation: relocation,
            PatchCount: relocation.Patches.Count,
            PatchedByteCount: relocation.Patches.Sum(patch => patch.ByteLength),
            RuntimeTargetsPersistent: true,
            ExactLogicalReadbackRequired: true,
            Notes:
            [
                "The target terrain texture id is unchanged; every destination face sharing that texture record observes one atomic replacement.",
                "The composer rejects target records rewritten by the level's texture-animation or scrolling tables before any BIN is copied.",
                "Final output is reread through the detected disc layout and every WAD-relative patch must equal its allocator-produced after bytes."
            ]);
    }

    public static async Task<NativeTerrainTextureRelocationExportResult> ExportAsync(
        NativeTerrainTextureRelocationExportRequest request,
        CancellationToken cancellationToken = default)
    {
        NativeTerrainTextureRelocationExportPlan plan = BuildPlan(request);
        string? outputDirectory = Path.GetDirectoryName(plan.OutputPlanPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        string outputSha256 = "";
        if (request.WriteImage)
        {
            DeleteStale(plan.OutputImagePath);
            DeleteStale(plan.OutputCuePath);
            File.Copy(request.SourceImagePath, plan.OutputImagePath, true);
            DiscLayout layout = DiscImage.DetectLayout(plan.OutputImagePath);
            await using (FileStream output = File.Open(
                             plan.OutputImagePath,
                             FileMode.Open,
                             FileAccess.ReadWrite,
                             FileShare.Read))
            {
                foreach (NativeTerrainTextureRelocationPatch patch in plan.Relocation.Patches)
                {
                    byte[] before = DiscImage.ReadFileBytes(
                        output,
                        layout,
                        WadLba,
                        patch.WadOffset,
                        patch.ByteLength);
                    if (!before.SequenceEqual(patch.Before))
                    {
                        throw new InvalidDataException(
                            $"Output copy no longer matches relocation before-bytes at WAD offset 0x{patch.WadOffset:X}.");
                    }
                    DiscImage.WriteFileBytes(output, layout, WadLba, patch.WadOffset, patch.After);
                }

                output.Flush(flushToDisk: true);
                foreach (NativeTerrainTextureRelocationPatch patch in plan.Relocation.Patches)
                {
                    byte[] after = DiscImage.ReadFileBytes(
                        output,
                        layout,
                        WadLba,
                        patch.WadOffset,
                        patch.ByteLength);
                    if (!after.SequenceEqual(patch.After))
                    {
                        throw new InvalidDataException(
                            $"Final BIN relocation readback failed at WAD offset 0x{patch.WadOffset:X}.");
                    }
                }
            }

            string cueText = DiscImage.BuildCueText(request.SourceCuePath, Path.GetFileName(plan.OutputImagePath));
            await File.WriteAllTextAsync(plan.OutputCuePath, cueText, Encoding.ASCII, cancellationToken);
            outputSha256 = ComputeSha256(plan.OutputImagePath);
        }

        var report = new
        {
            plan,
            wroteImage = request.WriteImage,
            outputImageSha256 = outputSha256
        };
        await File.WriteAllTextAsync(
            plan.OutputPlanPath,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        return new NativeTerrainTextureRelocationExportResult(plan, request.WriteImage, outputSha256);
    }

    private static void ValidatePatchSet(
        string sourceImagePath,
        IReadOnlyList<NativeTerrainTextureRelocationPatch> patches)
    {
        if (patches.Count == 0)
            throw new InvalidOperationException("The relocation allocator emitted no patches.");

        NativeTerrainTextureRelocationPatch[] ordered = patches
            .OrderBy(patch => patch.WadOffset)
            .ToArray();
        for (int index = 0; index < ordered.Length; index++)
        {
            NativeTerrainTextureRelocationPatch patch = ordered[index];
            if (patch.WadOffset < 0 || patch.ByteLength <= 0 ||
                patch.Before.Length != patch.ByteLength ||
                patch.After.Length != patch.ByteLength ||
                patch.Before.SequenceEqual(patch.After))
            {
                throw new InvalidDataException(
                    $"Relocation patch {index} at WAD offset 0x{patch.WadOffset:X} is empty, malformed, or a no-op.");
            }
            if (index > 0)
            {
                NativeTerrainTextureRelocationPatch previous = ordered[index - 1];
                if (patch.WadOffset < previous.WadOffset + previous.ByteLength)
                {
                    throw new InvalidDataException(
                        $"Relocation patches overlap at WAD offsets 0x{previous.WadOffset:X} and 0x{patch.WadOffset:X}.");
                }
            }
        }

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream source = File.OpenRead(sourceImagePath);
        foreach (NativeTerrainTextureRelocationPatch patch in ordered)
        {
            byte[] actual = DiscImage.ReadFileBytes(source, layout, WadLba, patch.WadOffset, patch.ByteLength);
            if (!actual.SequenceEqual(patch.Before))
            {
                throw new InvalidDataException(
                    $"Relocation before-bytes do not match the selected source at WAD offset 0x{patch.WadOffset:X}.");
            }
        }
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void DeleteStale(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

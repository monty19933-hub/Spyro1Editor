using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

public sealed record StoneHillTownSquareIdentityArtifactResult(
    StoneHillTownSquareIdentityCandidateResult Candidate,
    string StaticProofPath,
    string RuntimeChecklistPath);

/// <summary>
/// Publishes the exact runtime-proven name-identity artifact and regenerable evidence
/// sidecars. The runtime claim is permitted only for the pinned recipe and output hash.
/// Each file is replaced safely, while the final verification below detects incomplete
/// or stale sidecars so the focused runner can regenerate them.
/// </summary>
public static class StoneHillTownSquareIdentityArtifactWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async Task<StoneHillTownSquareIdentityArtifactResult> ExportAsync(
        StoneHillTownSquareIdentityCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        StoneHillTownSquareIdentityCandidateResult result =
            await StoneHillTownSquareIdentityCandidateComposer.ExportAsync(
                request,
                cancellationToken);
        ValidateRuntimeProvenResult(result);
        string outputImage = Path.GetFullPath(result.OutputImagePath);
        string outputPrefix = Path.Combine(
            Path.GetDirectoryName(outputImage)!,
            Path.GetFileNameWithoutExtension(outputImage));
        string reportPath = outputPrefix + "-static-proof.json";
        string checklistPath = outputPrefix + "-runtime-checklist.md";

        string report = JsonSerializer.Serialize(new
        {
            status = "runtime-proven-identity-profile-guarded",
            runtimeClaim = true,
            requiresDuckStationRuntimeProof = false,
            evidencePublication = "derived-regenerable-sidecars",
            result.Plan.RecipeId,
            result.Plan.RecipeVersion,
            result.Plan.BaseProfileId,
            result.Plan.Evidence,
            result.Plan.EvidenceId,
            result.Plan.EvidenceSummary,
            result.Plan.BaseImageSha256,
            result.Plan.ExpectedOutputImageSha256,
            result.OutputImagePath,
            result.OutputCuePath,
            result.OutputImageSha256,
            result.ChangedLogicalExecutableBytes,
            result.ChangedPhysicalImageBytes,
            result.RebuiltRawSectorCount,
            result.ExactLogicalDiffBoundaryVerified,
            result.ExactPhysicalSectorBoundaryVerified,
            result.NamePointerReadbackVerified,
            result.CountTablesPreserved,
            result.BaseCandidatePreserved,
            result.BinCuePublishCompleted,
            result.Plan
        }, JsonOptions);
        string checklist =
            $$"""
            # V5 Town Square display identity — regression checklist

            Recipe: `{{result.Plan.RecipeId}}`
            Evidence: `{{result.Plan.EvidenceId}}`
            Base runtime-proven BIN SHA-256: `{{result.Plan.BaseImageSha256}}`
            Exact proven BIN SHA-256: `{{result.OutputImageSha256}}`

            This exact guarded V5 research artifact passed the complete interactive DuckStation
            checklist on 2026-08-01. Recheck after any recipe, source-disc, emulator, or surrounding
            edit change. The recipe changes only Stone Hill's indexed level-name pointer; it does not
            change level ID 11, save ownership, completion totals, music, the title-demo safety
            reroute, or the known transition-sky handoff.

            ## Start clean

            1. Use a fresh game or disposable memory card. Existing Stone Hill slot-11 progress can
               legitimately carry into this replacement and would make the initial counters ambiguous.
            2. Cold-boot `{{Path.GetFileName(result.OutputCuePath)}}`.
            3. In Artisans, inspect both the replaced Stone Hill portal and the original Town Square portal.

            ## Display identity

            - The replaced Stone Hill portal should now be lettered **Town Square**.
            - Its fly-in/transition name should display **Town Square**.
            - Inventory/guidebook should display **Town Square**, starting at 0/200 gems,
              0/4 dragons, and 0/1 egg on a fresh slot.
            - The original retail Town Square portal also remains labelled Town Square. Two visible
              Town Square entries are expected in this experiment, but they retain independent save slots.

            ## Gameplay and persistence

            - Enter the replaced portal and confirm Town Square still loads without a freeze or assertion.
            - Run, jump, glide, charge, flame, defeat enemies, and use pause/Inventory.
            - Collect at least one gem, rescue one dragon, and collect the egg; each slot-11 counter
              must advance exactly once.
            - Die/reload, Return Home, re-enter, save/reload, and revisit.
            - Enter the original Town Square portal and confirm its slot-13 progress is independent.

            ## Expected unchanged behavior

            - Music remains Stone Hill for this name-only experiment.
            - The brief Stone Hill/Town Square transition-sky handoff can still occur.
            - Title demo slot 0 remains safety-rerouted to Doctor Shemp, so Doctor Shemp can appear twice.
            """;

        await WriteTextAtomicAsync(reportPath, report, cancellationToken);
        await WriteTextAtomicAsync(checklistPath, checklist, cancellationToken);
        await VerifyPublishedEvidenceAsync(
            reportPath,
            checklistPath,
            result,
            cancellationToken);
        return new StoneHillTownSquareIdentityArtifactResult(
            result,
            reportPath,
            checklistPath);
    }

    private static void ValidateRuntimeProvenResult(
        StoneHillTownSquareIdentityCandidateResult result)
    {
        NativeLevelReplacementIdentityProfile profile =
            NativeLevelReplacementIdentityProfileRegistry.RequireRuntimeProven(
                result.Plan.RecipeId);
        if (result.Plan.RecipeVersion != profile.RecipeVersion ||
            result.Plan.BaseProfileId != profile.BaseProfileId ||
            result.Plan.Evidence != NativeLevelReplacementEvidenceStatus.RuntimeProven ||
            result.Plan.EvidenceId != profile.EvidenceId ||
            result.Plan.EvidenceSummary != profile.EvidenceSummary ||
            !NativeLevelReplacementProfile.ShaEquals(
                result.Plan.BaseImageSha256,
                profile.BaseOutputImageSha256) ||
            !NativeLevelReplacementProfile.ShaEquals(
                result.Plan.ExpectedOutputImageSha256,
                profile.OutputImageSha256) ||
            !NativeLevelReplacementProfile.ShaEquals(
                result.OutputImageSha256,
                profile.OutputImageSha256) ||
            result.ChangedLogicalExecutableBytes != profile.ExpectedLogicalChangedBytes ||
            result.ChangedPhysicalImageBytes != profile.ExpectedPhysicalChangedBytes ||
            result.RebuiltRawSectorCount != profile.ExpectedRebuiltRawSectorCount ||
            result.Plan.Safety.Status != "runtime-proven-identity-profile-guarded" ||
            result.Plan.Safety.RequiresDuckStationRuntimeProof ||
            !result.ExactLogicalDiffBoundaryVerified ||
            !result.ExactPhysicalSectorBoundaryVerified ||
            !result.NamePointerReadbackVerified ||
            !result.CountTablesPreserved ||
            !result.BaseCandidatePreserved ||
            !result.BinCuePublishCompleted)
        {
            throw new InvalidDataException(
                "A Town Square display-identity runtime claim requires the exact checked profile and output hash.");
        }
    }

    private static async Task VerifyPublishedEvidenceAsync(
        string reportPath,
        string checklistPath,
        StoneHillTownSquareIdentityCandidateResult result,
        CancellationToken cancellationToken)
    {
        string report = await File.ReadAllTextAsync(reportPath, cancellationToken);
        using JsonDocument document = JsonDocument.Parse(report);
        JsonElement root = document.RootElement;
        if (root.GetProperty("status").GetString() != "runtime-proven-identity-profile-guarded" ||
            !root.GetProperty("runtimeClaim").GetBoolean() ||
            root.GetProperty("requiresDuckStationRuntimeProof").GetBoolean() ||
            root.GetProperty("evidencePublication").GetString() != "derived-regenerable-sidecars" ||
            root.GetProperty("evidenceId").GetString() != result.Plan.EvidenceId ||
            root.GetProperty("evidenceSummary").GetString() != result.Plan.EvidenceSummary ||
            root.GetProperty("outputImageSha256").GetString() != result.OutputImageSha256 ||
            root.GetProperty("expectedOutputImageSha256").GetString() !=
                result.Plan.ExpectedOutputImageSha256)
        {
            throw new InvalidDataException(
                "The published identity proof is incomplete, stale, or not bound to the proven profile.");
        }

        string checklist = await File.ReadAllTextAsync(checklistPath, cancellationToken);
        if (!checklist.Contains(result.OutputImageSha256, StringComparison.Ordinal) ||
            !checklist.Contains(Path.GetFileName(result.OutputCuePath), StringComparison.Ordinal) ||
            !checklist.Contains(result.Plan.EvidenceId, StringComparison.Ordinal) ||
            !checklist.Contains("passed the complete interactive DuckStation", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The published identity regression checklist does not identify the exact proven artifact.");
        }
    }

    private static async Task WriteTextAtomicAsync(
        string path,
        string contents,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough);
            await using StreamWriter writer = new(stream, new UTF8Encoding(false));
            await writer.WriteAsync(contents.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
            stream.Flush(flushToDisk: true);
            writer.Close();
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}

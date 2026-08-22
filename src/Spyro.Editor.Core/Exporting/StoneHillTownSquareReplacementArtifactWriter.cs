using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spyro.Editor.Core.Exporting;

public sealed record StoneHillTownSquareReplacementArtifactResult(
    StoneHillTownSquareReplacementCandidateResult Candidate,
    string StaticProofPath,
    string RuntimeChecklistPath);

/// <summary>
/// Produces the guarded V5 research BIN/CUE and its human/machine-readable evidence files.
/// Normal V4 Create BIN does not call this writer.
/// </summary>
public static class StoneHillTownSquareReplacementArtifactWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async Task<StoneHillTownSquareReplacementArtifactResult> ExportAsync(
        StoneHillTownSquareReplacementCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        StoneHillTownSquareReplacementCandidateResult result =
            await StoneHillTownSquareReplacementCandidateComposer.ExportAsync(
                request,
                cancellationToken);
        string outputImage = Path.GetFullPath(result.OutputImagePath);
        string outputPrefix = Path.Combine(
            Path.GetDirectoryName(outputImage)!,
            Path.GetFileNameWithoutExtension(outputImage));
        string reportPath = outputPrefix + "-static-proof.json";
        string checklistPath = outputPrefix + "-runtime-checklist.md";

        string report = JsonSerializer.Serialize(new
        {
            status = "runtime-proven-profile-guarded",
            runtimeClaim = true,
            profileId = result.Plan.ProfileId,
            profileRecipeVersion = result.Plan.ProfileRecipeVersion,
            evidence = result.Plan.Evidence,
            evidenceId = result.Plan.EvidenceId,
            expectedOutputImageSha256 = result.Plan.ExpectedOutputImageSha256,
            result.OutputImagePath,
            result.OutputCuePath,
            result.OutputImageSha256,
            result.ChangedWadBytes,
            result.ChangedExecutableBytes,
            result.RebuiltRawSectorCount,
            result.ExactLogicalDiffBoundaryVerified,
            result.ArtisansPortalPreimagesVerified,
            result.DonorEntriesPreserved,
            result.TownSquareFlyInRelocatedExactly,
            result.TownSquareReturnHomeRelocatedExactly,
            result.ResidualTargetCapacityPreserved,
            result.SourceImagePreserved,
            result.AtomicRenameCompleted,
            result.Plan
        }, JsonOptions);
        string checklist =
            $$"""
            # V5 Stone Hill slot replacement — regression checklist

            Profile: `{{result.Plan.ProfileId}}`
            Evidence: `{{result.Plan.EvidenceId}}`
            Exact proven BIN SHA-256: `{{result.Plan.ExpectedOutputImageSha256}}`

            This guarded V5 research artifact is separate from Beta V4 and normal Create BIN.
            The exact output already passed the complete DuckStation checklist. Recheck after any recipe,
            source-disc, emulator, or surrounding edit change.

            ## Start

            1. Use a fresh game or disposable memory card if collection masks are not under test.
            2. Cold-boot `{{Path.GetFileName(result.OutputCuePath)}}`.
            3. Enter the Artisans portal still labelled **Stone Hill**.

            ## Expected replacement

            - Town Square geometry, collision, textures, sky, actors, camera, fly-in, and actor sounds load together.
            - Portal identity and music remain Stone Hill in this first slot-identity recipe.
            - Title demo slot 0 is intentionally safety-rerouted to native Doctor Shemp. The resulting
              cycle is Doctor Shemp, Doctor Shemp, Icy Flight, and Wizard Peak; the duplicate is expected.
            - A brief Stone Hill-to-Town Square sky handoff can remain during portal entry, with the
              inverse handoff on exit. This is known visual polish and not a destination-load failure.

            ## Regression exercise

            - Run, jump, glide, charge, flame, defeat enemies, and collect gems.
            - Rescue a dragon, collect the egg thief, pause, and open Inventory.
            - Die/reload, Return Home, re-enter, save/reload, and revisit.
            - Let title demo mode begin; Doctor Shemp is the expected safety reroute.
            """;

        await WriteTextAtomicAsync(reportPath, report, cancellationToken);
        await WriteTextAtomicAsync(checklistPath, checklist, cancellationToken);
        return new StoneHillTownSquareReplacementArtifactResult(
            result,
            reportPath,
            checklistPath);
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

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

public sealed record StoneHillTownSquareEditedDonorArtifactResult(
    StoneHillTownSquareEditedDonorCandidateResult Candidate,
    string StaticProofPath,
    string RuntimeChecklistPath);

/// <summary>
/// Publishes the isolated edited-donor candidate and static evidence sidecars.
/// The evidence deliberately carries runtimeClaim=false until the exact output
/// hash passes the DuckStation checklist and is promoted separately.
/// </summary>
public static class StoneHillTownSquareEditedDonorArtifactWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async Task<StoneHillTownSquareEditedDonorArtifactResult> ExportAsync(
        StoneHillTownSquareEditedDonorCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        StoneHillTownSquareEditedDonorCandidateResult result =
            await StoneHillTownSquareEditedDonorCandidateComposer.ExportAsync(
                request,
                cancellationToken);
        ValidatePendingRuntimeResult(result);

        string outputImage = Path.GetFullPath(result.OutputImagePath);
        string outputPrefix = Path.Combine(
            Path.GetDirectoryName(outputImage)!,
            Path.GetFileNameWithoutExtension(outputImage));
        string reportPath = outputPrefix + "-static-proof.json";
        string checklistPath = outputPrefix + "-runtime-checklist.md";

        bool placementMode = result.Plan.PlacementPatch != null;
        bool renderRadiusMode = result.Plan.RenderRadiusPatch != null;
        string report = JsonSerializer.Serialize(new
        {
            status = result.Plan.Safety.Status,
            runtimeClaim = false,
            requiresDuckStationRuntimeProof = true,
            evidencePublication = "derived-regenerable-sidecars",
            result.Plan.RecipeId,
            result.Plan.RecipeVersion,
            result.Plan.Evidence,
            result.Plan.EvidenceId,
            result.Plan.EvidenceSummary,
            result.Plan.BaseImageSha256,
            result.Plan.RetailSourceImageSha256,
            result.OutputImagePath,
            result.OutputCuePath,
            result.OutputImageSha256,
            result.ChangedLogicalWadBytes,
            result.ChangedPhysicalImageBytes,
            result.RebuiltRawSectorCount,
            result.ExactLogicalDiffBoundaryVerified,
            result.ExactPhysicalSectorBoundaryVerified,
            result.OriginalTownSquareDonorPreserved,
            result.DisplayIdentityExecutablePreserved,
            result.BaseImagePreserved,
            result.RetailSourcePreserved,
            result.BinCuePublishCompleted,
            result.PlacementSectorReadbackVerified,
            result.RenderRadiusReadbackVerified,
            result.Plan
        }, JsonOptions);
        string checklistTitle = renderRadiusMode
            ? "V5 Town Square edited-donor T21 X + isolated render radius"
            : placementMode
                ? "V5 Town Square edited-donor T21 X + placement sector"
                : "V5 Town Square edited-donor T21 X";
        string placementDisclosure = renderRadiusMode
            ? "This diagnostic preserves the native +0x4A FF visibility sentinel and changes only T21 X plus its +0x50 render radius from the native loose-gem value 18 to the maximum safe positive value 7F. Values 80-FF select the renderer's special negative-radius path and are deliberately blocked. This is not a loose-gem promotion rule. Both logical edits share one rebuilt raw WAD sector."
            : placementMode
                ? "This rejected historical gate overwrites T21's native +0x4A FF visibility sentinel with the editor-derived terrain-sector value D5. Both logical edits share one rebuilt raw WAD sector."
                : "This first gate preserves T21's native +0x4A FF visibility sentinel unchanged and relocates only the X word; the invalid derived terrain-sector write is intentionally outside this control.";
        string placementCheck = renderRadiusMode
            ? "From the same normal in-level viewpoint that reproduced the regression, confirm T21 remains continuously visible with +0x50 = 7F; compare adjacent native T22 if practical, then approach and collect T21 once. Do not treat visibility beyond the unchanged +0x52 update radius as part of this test."
            : placementMode
                ? "Confirm whether T21 remains visible and interactive after the rejected historical D5 visibility-byte overwrite; watch for pop-in, premature culling, or a missing gem."
                : "Confirm T21 remains visible and interactive at its edited position while its native +0x4A FF visibility sentinel remains unchanged; record any pop-in or culling limitation.";
        string checklist =
            $$"""
            # {{checklistTitle}} — DuckStation checklist

            Recipe: `{{result.Plan.RecipeId}}`
            Pending evidence: `{{result.Plan.EvidenceId}}`
            Exact candidate BIN SHA-256: `{{result.OutputImageSha256}}`
            Base display-identity BIN SHA-256: `{{result.Plan.BaseImageSha256}}`

            This is a disposable V5 research candidate. Static preimage, relocation, readback,
            source-preservation, SCUS-preservation, and raw-sector boundary checks passed, but
            this result is **not runtime proof**. Do not promote this recipe into normal Create BIN
            until this exact BIN hash passes the full checklist below in DuckStation.

            {{placementDisclosure}}

            ## Start clean

            1. Use a fresh game or disposable memory card so collection state cannot hide T21.
            2. Cold-boot `{{Path.GetFileName(result.OutputCuePath)}}`.
            3. Enter the Artisans portal displayed as **Town Square** in Stone Hill's retail slot.

            ## Edited payload

            - Locate Town Square actor T21 and confirm its X position changed as staged by the editor.
            - {{placementCheck}}
            - Confirm no second actor moved and no actor model, type, state, yaw, Y, or Z changed.
            - Collect/interact with T21 if applicable and confirm it behaves and persists normally.

            ## Full replacement regression

            - Run, jump, glide, charge, flame, defeat enemies, collect gems, rescue a dragon,
              collect the egg, pause, and open Inventory.
            - Die/reload, Return Home, re-enter, save/reload, and revisit.
            - Confirm the displayed Town Square identity, slot-11 totals, Stone Hill music,
              known transition-sky handoff, and Doctor Shemp demo reroute remain unchanged.
            - Enter the original retail Town Square portal and confirm its actors and T21 position
              are unchanged and its progression remains independent.

            ## Promotion record

            Record the emulator/version, exact BIN SHA-256 above, clean/fresh-save status, each
            checklist result, and any freeze, assertion, graphical corruption, or persistence issue.
            """;

        await WriteTextAtomicAsync(reportPath, report, cancellationToken);
        await WriteTextAtomicAsync(checklistPath, checklist, cancellationToken);
        await VerifyPublishedEvidenceAsync(reportPath, checklistPath, result, cancellationToken);
        return new StoneHillTownSquareEditedDonorArtifactResult(
            result,
            reportPath,
            checklistPath);
    }

    private static void ValidatePendingRuntimeResult(
        StoneHillTownSquareEditedDonorCandidateResult result)
    {
        bool placementMode = result.Plan.PlacementPatch != null;
        bool renderRadiusMode = result.Plan.RenderRadiusPatch != null;
        if (placementMode && renderRadiusMode)
            throw new InvalidDataException("The edited-donor candidate cannot combine placement and render-radius diagnostics.");
        string expectedRecipeId = renderRadiusMode
            ? StoneHillTownSquareEditedDonorCandidateComposer.RenderRadiusRecipeId
            : placementMode
                ? StoneHillTownSquareEditedDonorCandidateComposer.PlacementRecipeId
                : StoneHillTownSquareEditedDonorCandidateComposer.RecipeId;
        int expectedRecipeVersion = renderRadiusMode
            ? StoneHillTownSquareEditedDonorCandidateComposer.RenderRadiusRecipeVersion
            : placementMode
                ? StoneHillTownSquareEditedDonorCandidateComposer.PlacementRecipeVersion
                : StoneHillTownSquareEditedDonorCandidateComposer.RecipeVersion;
        string expectedEvidenceId = renderRadiusMode
            ? StoneHillTownSquareEditedDonorCandidateComposer.RenderRadiusEvidenceId
            : placementMode
                ? StoneHillTownSquareEditedDonorCandidateComposer.PlacementEvidenceId
                : StoneHillTownSquareEditedDonorCandidateComposer.EvidenceId;
        string expectedStatus = renderRadiusMode
            ? StoneHillTownSquareEditedDonorCandidateComposer.RenderRadiusStatus
            : placementMode
                ? StoneHillTownSquareEditedDonorCandidateComposer.PlacementStatus
                : StoneHillTownSquareEditedDonorCandidateComposer.Status;
        if (result.Plan.RecipeId != expectedRecipeId ||
            result.Plan.RecipeVersion != expectedRecipeVersion ||
            result.Plan.Evidence != NativeLevelReplacementEvidenceStatus.StaticBaselineOnly ||
            result.Plan.EvidenceId != expectedEvidenceId ||
            result.Plan.Safety.Status != expectedStatus ||
            !result.Plan.Safety.RequiresDuckStationRuntimeProof ||
            result.Plan.Patch.Kind != "moby-position-x" ||
            result.Plan.Patch.TrueIndex != 21 ||
            result.Plan.Patch.DonorWadOffset != 0x136E8B4 ||
            result.Plan.Patch.TargetWadOffset != 0xD640B4 ||
            result.Plan.Patch.ByteLength != 4 ||
            (placementMode &&
             (result.Plan.Patch.BeforeHex != "5CE80100" ||
              result.Plan.Patch.AfterHex != "5CE00100" ||
              result.Plan.PlacementPatch!.Kind != "moby-placement-sector" ||
              result.Plan.PlacementPatch.TrueIndex != 21 ||
              result.Plan.PlacementPatch.DonorWadOffset != 0x136E8F2 ||
              result.Plan.PlacementPatch.TargetWadOffset != 0xD640F2 ||
              result.Plan.PlacementPatch.ByteLength != 1 ||
              result.Plan.PlacementPatch.BeforeHex != "FF" ||
              result.Plan.PlacementPatch.AfterHex != "D5")) ||
            (renderRadiusMode &&
             (result.Plan.Patch.BeforeHex != "5CE80100" ||
              result.Plan.Patch.AfterHex != "5CE00100" ||
              result.Plan.RenderRadiusPatch!.Kind != "moby-render-radius" ||
              result.Plan.RenderRadiusPatch.TrueIndex != 21 ||
              result.Plan.RenderRadiusPatch.DonorWadOffset != 0x136E8F8 ||
              result.Plan.RenderRadiusPatch.TargetWadOffset != 0xD640F8 ||
              result.Plan.RenderRadiusPatch.ByteLength != 1 ||
              result.Plan.RenderRadiusPatch.BeforeHex != "18" ||
              result.Plan.RenderRadiusPatch.AfterHex != "7F")) ||
            !NativeLevelReplacementProfile.IsSha256(result.OutputImageSha256) ||
            NativeLevelReplacementProfile.ShaEquals(
                result.OutputImageSha256,
                NativeLevelReplacementIdentityProfileRegistry.TownSquareDisplayIdentityOutputImageSha256) ||
            (placementMode || renderRadiusMode
                ? result.ChangedLogicalWadBytes != 2
                : result.ChangedLogicalWadBytes is <= 0 or > 4) ||
            result.ChangedPhysicalImageBytes <= result.ChangedLogicalWadBytes ||
            result.RebuiltRawSectorCount != 1 ||
            !result.ExactLogicalDiffBoundaryVerified ||
            !result.ExactPhysicalSectorBoundaryVerified ||
            !result.OriginalTownSquareDonorPreserved ||
            !result.DisplayIdentityExecutablePreserved ||
            !result.BaseImagePreserved ||
            !result.RetailSourcePreserved ||
            !result.BinCuePublishCompleted ||
            result.PlacementSectorReadbackVerified != placementMode ||
            result.RenderRadiusReadbackVerified != renderRadiusMode)
        {
            throw new InvalidDataException(
                "The edited-donor static evidence is incomplete or incorrectly claims its checked gate boundary.");
        }
    }

    private static async Task VerifyPublishedEvidenceAsync(
        string reportPath,
        string checklistPath,
        StoneHillTownSquareEditedDonorCandidateResult result,
        CancellationToken cancellationToken)
    {
        string report = await File.ReadAllTextAsync(reportPath, cancellationToken);
        using JsonDocument document = JsonDocument.Parse(report);
        JsonElement root = document.RootElement;
        if (root.GetProperty("status").GetString() !=
                result.Plan.Safety.Status ||
            root.GetProperty("runtimeClaim").GetBoolean() ||
            !root.GetProperty("requiresDuckStationRuntimeProof").GetBoolean() ||
            root.GetProperty("evidencePublication").GetString() != "derived-regenerable-sidecars" ||
            root.GetProperty("evidenceId").GetString() != result.Plan.EvidenceId ||
            root.GetProperty("outputImageSha256").GetString() != result.OutputImageSha256)
        {
            throw new InvalidDataException(
                "The published edited-donor proof is incomplete, stale, or makes a runtime claim.");
        }

        string checklist = await File.ReadAllTextAsync(checklistPath, cancellationToken);
        if (!checklist.Contains(result.OutputImageSha256, StringComparison.Ordinal) ||
            !checklist.Contains(Path.GetFileName(result.OutputCuePath), StringComparison.Ordinal) ||
            !checklist.Contains(result.Plan.EvidenceId, StringComparison.Ordinal) ||
            !checklist.Contains("not runtime proof", StringComparison.Ordinal) ||
            !checklist.Contains("visibility sentinel", StringComparison.OrdinalIgnoreCase) ||
            !checklist.Contains("original retail Town Square", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The edited-donor runtime checklist does not identify the exact pending candidate.");
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

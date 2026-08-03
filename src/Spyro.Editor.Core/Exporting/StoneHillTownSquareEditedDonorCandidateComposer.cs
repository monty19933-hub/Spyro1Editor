using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

public sealed record StoneHillTownSquareEditedDonorCandidateRequest(
    string BaseImagePath,
    string BaseCuePath,
    string RetailSourceImagePath,
    MobySourcePatchPlan MobyPatchPlan,
    string OutputImagePath,
    string OutputCuePath);

public sealed record StoneHillTownSquareEditedDonorPatchSummary(
    string Label,
    string Kind,
    int TrueIndex,
    long DonorWadOffset,
    long TargetWadOffset,
    int ByteLength,
    string BeforeHex,
    string AfterHex,
    string BeforeSha256,
    string AfterSha256);

public sealed record StoneHillTownSquareEditedDonorCandidatePlan(
    DateTimeOffset GeneratedAtUtc,
    string RecipeId,
    int RecipeVersion,
    NativeLevelReplacementEvidenceStatus Evidence,
    string EvidenceId,
    string EvidenceSummary,
    string BaseImageSha256,
    string RetailSourceImageSha256,
    string BaseExecutableSha256,
    long DonorDataWadOffset,
    long TargetDataWadOffset,
    StoneHillTownSquareEditedDonorPatchSummary Patch,
    StoneHillTownSquareReplacementSafetyReport Safety,
    StoneHillTownSquareEditedDonorPatchSummary? PlacementPatch = null,
    StoneHillTownSquareEditedDonorPatchSummary? RenderRadiusPatch = null);

public sealed record StoneHillTownSquareEditedDonorCandidateResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    StoneHillTownSquareEditedDonorCandidatePlan Plan,
    long ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool OriginalTownSquareDonorPreserved,
    bool DisplayIdentityExecutablePreserved,
    bool BaseImagePreserved,
    bool RetailSourcePreserved,
    bool BinCuePublishCompleted,
    bool PlacementSectorReadbackVerified = false,
    bool RenderRadiusReadbackVerified = false);

/// <summary>
/// Isolated V5 research compiler for the first edited-donor runtime experiment.
/// It starts from the exact runtime-proven Town-Square display-identity BIN and
/// rebases one checked, in-place Town Square moby coordinate patch into the
/// transplanted payload in Stone Hill's slot. It is intentionally not reachable
/// from normal V4 Create BIN and makes no runtime claim until DuckStation passes.
/// </summary>
public static class StoneHillTownSquareEditedDonorCandidateComposer
{
    public const string RecipeId =
        "native-level-replacement-stonehill-townsquare-edited-donor-t21-x-clean-usa-v1";
    public const int RecipeVersion = 1;
    public const string EvidenceId =
        "stonehill-slot-townsquare-edited-donor-t21-x-pending-duckstation";
    public const string Status = "edited-donor-runtime-candidate-pending-duckstation";
    public const string PlacementRecipeId =
        "native-level-replacement-stonehill-townsquare-edited-donor-t21-x-placement-sector-clean-usa-v2";
    public const int PlacementRecipeVersion = 2;
    public const string PlacementEvidenceId =
        "stonehill-slot-townsquare-edited-donor-t21-x-placement-sector-pending-duckstation";
    public const string PlacementStatus =
        "edited-donor-placement-runtime-candidate-pending-duckstation";
    public const string RenderRadiusRecipeId =
        "native-level-replacement-stonehill-townsquare-edited-donor-t21-x-render-radius-7f-clean-usa-v4";
    public const int RenderRadiusRecipeVersion = 4;
    public const string RenderRadiusEvidenceId =
        "stonehill-slot-townsquare-edited-donor-t21-x-render-radius-7f-pending-duckstation";
    public const string RenderRadiusStatus =
        "edited-donor-render-radius-runtime-candidate-pending-duckstation";

    private const int WadLba = 37;
    private const int ExecutableLba = 53875;
    private const int ExecutableByteLength = 0x66000;
    private const int RawSectorByteLength = 2352;
    private const int UserSectorByteLength = 2048;
    private const int UserSectorOffset = 24;
    private const long DonorDataWadOffset = 0x119E000;
    private const int DonorDataByteLength = 0x2E2000;
    private const long TargetDataWadOffset = 0xB93800;
    private const long DonorPatchWadOffset = 0x136E8B4;
    private const long TargetPatchWadOffset = 0xD640B4;
    private const int PatchByteLength = 4;
    private const long DonorPlacementPatchWadOffset = 0x136E8F2;
    private const long TargetPlacementPatchWadOffset = 0xD640F2;
    private const int PlacementPatchByteLength = 1;
    private const long DonorRenderRadiusPatchWadOffset = 0x136E8F8;
    private const long TargetRenderRadiusPatchWadOffset = 0xD640F8;
    private const int RenderRadiusPatchByteLength = 1;
    private const int ExpectedTrueIndex = 21;
    private const int ExpectedSourceRecordCount = 107;
    private const int ExpectedRecordStride = 0x58;
    private const string ExpectedLevelKey = "townsquare";
    private const string ExpectedPatchKind = "moby-position-x";
    private const string ExpectedPlacementPatchKind = "moby-placement-sector";
    private const string ExpectedRenderRadiusPatchKind = "moby-render-radius";
    private const string ExpectedSourceTableWadOffset = "0x136E170";
    private const string IdentityBaseImageSha256 =
        "71808a4b5e0d0891e4f6f49be8b2712de018a393695b1b606b79e9ecbd3166c9";
    private const string IdentityExecutableSha256 =
        "b17fd7679d586562ef325813b7c469aff58430f5200633f64b8e6900409e3304";
    private static readonly byte[] ExpectedPlacementXBefore = Convert.FromHexString("5CE80100");
    private static readonly byte[] ExpectedPlacementXAfter = Convert.FromHexString("5CE00100");
    private static readonly byte[] ExpectedPlacementSectorBefore = [0xFF];
    private static readonly byte[] ExpectedPlacementSectorAfter = [0xD5];
    private static readonly byte[] ExpectedRenderRadiusBefore = [0x18];
    private static readonly byte[] ExpectedRenderRadiusAfter = [0x7F];

    public static async Task<StoneHillTownSquareEditedDonorCandidatePlan> BuildPlanAsync(
        StoneHillTownSquareEditedDonorCandidateRequest request,
        CancellationToken cancellationToken = default) =>
        (await PrepareAsync(request, cancellationToken)).Plan;

    public static async Task<StoneHillTownSquareEditedDonorCandidateResult> ExportAsync(
        StoneHillTownSquareEditedDonorCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        PreparedCandidate prepared = await PrepareAsync(request, cancellationToken);
        string baseImage = Path.GetFullPath(request.BaseImagePath);
        string baseCue = Path.GetFullPath(request.BaseCuePath);
        string retailSource = Path.GetFullPath(request.RetailSourceImagePath);
        string outputImage = Path.GetFullPath(request.OutputImagePath);
        string outputCue = Path.GetFullPath(request.OutputCuePath);
        EnsureDistinctPaths(baseImage, baseCue, retailSource, outputImage, outputCue);
        if (!PathEquals(Path.GetDirectoryName(outputImage), Path.GetDirectoryName(outputCue)))
            throw new InvalidOperationException("The edited-donor candidate BIN and CUE must share one directory.");

        Directory.CreateDirectory(Path.GetDirectoryName(outputImage)!);
        NativeLevelReplacementBaselineExporter.ValidateCue(baseCue, baseImage, "MODE2/2352");

        string operationId = Guid.NewGuid().ToString("N");
        string directory = Path.GetDirectoryName(outputImage)!;
        string temporaryImage = Path.Combine(directory, $".{Path.GetFileName(outputImage)}.{operationId}.tmp");
        string temporaryCue = Path.Combine(directory, $".{Path.GetFileName(outputCue)}.{operationId}.tmp");
        string backupImage = Path.Combine(directory, $".{Path.GetFileName(outputImage)}.{operationId}.bak");
        string backupCue = Path.Combine(directory, $".{Path.GetFileName(outputCue)}.{operationId}.bak");
        bool imageBackedUp = false;
        bool cueBackedUp = false;
        bool imagePublished = false;
        bool cuePublished = false;
        try
        {
            await DiscImageWorkingCopy.StageAsync(baseImage, temporaryImage, false, cancellationToken);
            DiscLayout layout = DiscImage.DetectLayout(temporaryImage);
            int rebuiltRawSectors;
            await using (FileStream output = new(
                temporaryImage, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                foreach (ValidatedPatch patch in prepared.Patches)
                {
                    byte[] current = DiscImage.ReadFileBytes(
                        output,
                        layout,
                        WadLba,
                        patch.TargetWadOffset,
                        patch.Before.Length);
                    VerifyBytesEqual(
                        patch.Before,
                        current,
                        $"The staged transplanted Town Square {patch.Source.Kind} preimage changed.");
                    DiscImage.WriteFileBytes(
                        output,
                        layout,
                        WadLba,
                        patch.TargetWadOffset,
                        patch.After);
                }
                rebuiltRawSectors = RawMode2Form1SectorIntegrity.RebuildFileRanges(
                    output,
                    layout,
                    WadLba,
                    prepared.Patches.Select(patch =>
                        (patch.TargetWadOffset, patch.After.Length)));
                output.Flush(flushToDisk: true);
            }

            if (rebuiltRawSectors != 1)
                throw new InvalidDataException($"Edited-donor export rebuilt {rebuiltRawSectors} raw sectors; expected one.");

            CandidateReadback readback = VerifyReadback(
                baseImage,
                retailSource,
                temporaryImage,
                prepared,
                cancellationToken);
            string cueText = DiscImage.BuildCueText(baseCue, Path.GetFileName(outputImage));
            await File.WriteAllTextAsync(temporaryCue, cueText, Encoding.ASCII, cancellationToken);
            NativeLevelReplacementBaselineExporter.ValidateCue(temporaryCue, outputImage, "MODE2/2352");

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

            NativeLevelReplacementBaselineExporter.ValidateCue(outputCue, outputImage, "MODE2/2352");
            string outputSha256 = await HashFileAsync(outputImage, cancellationToken);
            bool basePreserved = NativeLevelReplacementProfile.ShaEquals(
                await HashFileAsync(baseImage, cancellationToken),
                prepared.Plan.BaseImageSha256);
            bool retailPreserved = NativeLevelReplacementProfile.ShaEquals(
                await HashFileAsync(retailSource, cancellationToken),
                prepared.Plan.RetailSourceImageSha256);
            if (!basePreserved || !retailPreserved)
                throw new InvalidDataException("An edited-donor input BIN changed during export.");

            TryDelete(backupImage);
            TryDelete(backupCue);
            return new StoneHillTownSquareEditedDonorCandidateResult(
                outputImage,
                outputCue,
                outputSha256,
                prepared.Plan,
                readback.ChangedLogicalWadBytes,
                readback.ChangedPhysicalImageBytes,
                rebuiltRawSectors,
                ExactLogicalDiffBoundaryVerified: true,
                ExactPhysicalSectorBoundaryVerified: true,
                OriginalTownSquareDonorPreserved: true,
                DisplayIdentityExecutablePreserved: true,
                BaseImagePreserved: basePreserved,
                RetailSourcePreserved: retailPreserved,
                BinCuePublishCompleted: true,
                PlacementSectorReadbackVerified: prepared.PlacementPatch != null,
                RenderRadiusReadbackVerified: prepared.RenderRadiusPatch != null);
        }
        catch (Exception exportFailure)
        {
            if (cuePublished)
                TryDelete(outputCue);
            if (imagePublished)
                TryDelete(outputImage);
            List<Exception> recoveryFailures = [];
            Exception? imageRecovery = NativeLevelReplacementBaselineExporter.TryRestoreBackup(
                "BIN", backupImage, outputImage, ref imageBackedUp);
            Exception? cueRecovery = NativeLevelReplacementBaselineExporter.TryRestoreBackup(
                "CUE", backupCue, outputCue, ref cueBackedUp);
            if (imageRecovery != null)
                recoveryFailures.Add(imageRecovery);
            if (cueRecovery != null)
                recoveryFailures.Add(cueRecovery);
            if (recoveryFailures.Count > 0)
            {
                throw new IOException(
                    "The edited-donor candidate failed and prior output recovery was incomplete.",
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

    private static async Task<PreparedCandidate> PrepareAsync(
        StoneHillTownSquareEditedDonorCandidateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.MobyPatchPlan);
        string baseImage = RequireExistingFile(request.BaseImagePath, "exact display-identity base BIN");
        string baseCue = RequireExistingFile(request.BaseCuePath, "exact display-identity base CUE");
        string retailSource = RequireExistingFile(request.RetailSourceImagePath, "clean retail source BIN");
        NativeLevelReplacementBaselineExporter.ValidateCue(baseCue, baseImage, "MODE2/2352");

        string baseImageSha256 = await HashFileAsync(baseImage, cancellationToken);
        RequireHash(baseImageSha256, IdentityBaseImageSha256, "display-identity base BIN");
        string retailSourceSha256 = await HashFileAsync(retailSource, cancellationToken);
        RequireHash(
            retailSourceSha256,
            NativeLevelReplacementProfileRegistry.CleanUsaImageSha256,
            "clean USA retail source BIN");

        ValidatedPatchPlan validated = ValidatePatchPlan(request.MobyPatchPlan, retailSource);
        DiscLayout baseLayout = RequireMode2Layout(baseImage, "display-identity base BIN");
        DiscLayout retailLayout = RequireMode2Layout(retailSource, "clean retail source BIN");
        if (new FileInfo(baseImage).Length != new FileInfo(retailSource).Length)
            throw new InvalidDataException("The display-identity and retail BIN lengths differ.");

        byte[] retailDonorData;
        byte[] baseDonorData;
        byte[] executableBytes;
        await using (FileStream retail = File.OpenRead(retailSource))
        await using (FileStream identity = File.OpenRead(baseImage))
        {
            retailDonorData = DiscImage.ReadFileBytes(
                retail, retailLayout, WadLba, DonorDataWadOffset, DonorDataByteLength);
            baseDonorData = DiscImage.ReadFileBytes(
                identity, baseLayout, WadLba, DonorDataWadOffset, DonorDataByteLength);
            foreach (ValidatedPatch patch in validated.Patches)
            {
                byte[] retailDonorBefore = DiscImage.ReadFileBytes(
                    retail, retailLayout, WadLba, patch.DonorWadOffset, patch.Before.Length);
                byte[] baseDonorBefore = DiscImage.ReadFileBytes(
                    identity, baseLayout, WadLba, patch.DonorWadOffset, patch.Before.Length);
                byte[] baseTargetBefore = DiscImage.ReadFileBytes(
                    identity, baseLayout, WadLba, patch.TargetWadOffset, patch.Before.Length);
                VerifyBytesEqual(
                    patch.Before,
                    retailDonorBefore,
                    $"The {patch.Source.Kind} patch plan does not match the clean retail donor preimage.");
                VerifyBytesEqual(
                    patch.Before,
                    baseDonorBefore,
                    $"The display-identity BIN's native Town Square {patch.Source.Kind} preimage changed.");
                VerifyBytesEqual(
                    patch.Before,
                    baseTargetBefore,
                    $"The transplanted Town Square {patch.Source.Kind} preimage does not match retail.");
            }
            VerifyBytesEqual(retailDonorData, baseDonorData, "The display-identity BIN changed the original Town Square data entry.");

            DiscFileRecord executable = DiscImage.FindRootFileRecord(identity, baseLayout, name =>
                name.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
            if (executable.Lba != ExecutableLba || executable.Size != ExecutableByteLength)
                throw new InvalidDataException("The display-identity executable extent changed.");
            executableBytes = DiscImage.ReadFileBytes(
                identity, baseLayout, executable.Lba, 0, executable.Size);
            RequireHash(Hash(executableBytes), IdentityExecutableSha256, "display-identity executable");
        }

        StoneHillTownSquareEditedDonorPatchSummary xSummary = BuildSummary(validated.XPatch);
        StoneHillTownSquareEditedDonorPatchSummary? placementSummary =
            validated.PlacementPatch == null ? null : BuildSummary(validated.PlacementPatch);
        StoneHillTownSquareEditedDonorPatchSummary? renderRadiusSummary =
            validated.RenderRadiusPatch == null ? null : BuildSummary(validated.RenderRadiusPatch);
        bool renderRadiusMode = renderRadiusSummary != null;
        StoneHillTownSquareReplacementSafetyReport safety = new(
            validated.Status,
            WritableScopes: renderRadiusMode
                ?
                [
                    "Stone Hill target data entry: transplanted Town Square T21 position.x word",
                    "Stone Hill target data entry: transplanted Town Square T21 native render-radius byte (+0x50)",
                    "MODE2 EDC/ECC for their one shared raw WAD sector"
                ]
                : placementSummary == null
                ?
                [
                    "Stone Hill target data entry: transplanted Town Square T21 position.x word only",
                    "MODE2 EDC/ECC for the single raw WAD sector containing that word"
                ]
                :
                [
                    "Stone Hill target data entry: transplanted Town Square T21 position.x word",
                    "Stone Hill target data entry: transplanted Town Square T21 historical +0x4A visibility-byte diagnostic",
                    "MODE2 EDC/ECC for their one shared raw WAD sector"
                ],
            ProtectedScopes:
            [
                "The exact runtime-proven display-identity control BIN",
                "The clean retail source BIN and original Town Square data entry",
                renderRadiusMode
                    ? "Every transplanted Town Square byte except T21 X and its isolated render-radius diagnostic byte"
                    : placementSummary == null
                    ? "T21's native +0x4A visibility sentinel and every other transplanted Town Square byte"
                    : "Every transplanted Town Square byte except T21 X and its rejected historical +0x4A visibility-byte diagnostic",
                "Every other WAD entry and raw sector",
                "SCUS, slot identity, completion totals, music, title demo reroute, and save ownership",
                "Actor packages, appends, removals, state/type/yaw/Z/Y edits, paths, terrain, and textures",
                "Normal V4 Create BIN, projects, releases, and update channel"
            ],
            RuntimeChecks:
            [
                "Cold-boot the generated CUE in DuckStation; static proof is not runtime proof",
                renderRadiusMode
                    ? "Enter through Stone Hill's slot and verify moved T21 no longer flickers from the previously failing normal in-level sightline with render radius 0x7F"
                    : placementSummary == null
                    ? "Enter the replaced Town Square through Stone Hill's slot and locate moved T21 with its native +0x4A FF visibility sentinel preserved"
                    : "Enter the replaced Town Square through Stone Hill's slot and reproduce the rejected historical +0x4A D5 visibility-byte diagnostic",
                "Exercise gameplay, collection, dragon/egg progress, death/reload, Return Home, re-entry, and save/reload",
                "Confirm the original retail Town Square slot remains unchanged and independently playable"
            ],
            RequiresDuckStationRuntimeProof: true);
        StoneHillTownSquareEditedDonorCandidatePlan candidatePlan = new(
            DateTimeOffset.UtcNow,
            validated.RecipeId,
            validated.RecipeVersion,
            NativeLevelReplacementEvidenceStatus.StaticBaselineOnly,
            validated.EvidenceId,
            "Static preimage, relocation, readback, source-preservation, and raw-sector boundary checks passed; DuckStation runtime proof is pending.",
            baseImageSha256,
            retailSourceSha256,
            IdentityExecutableSha256,
            DonorDataWadOffset,
            TargetDataWadOffset,
            xSummary,
            safety,
            placementSummary,
            renderRadiusSummary);
        return new PreparedCandidate(
            validated.Patches,
            validated.PlacementPatch,
            validated.RenderRadiusPatch,
            retailDonorData,
            executableBytes,
            candidatePlan);
    }

    private static ValidatedPatchPlan ValidatePatchPlan(MobySourcePatchPlan plan, string retailSource)
    {
        if (!PathEquals(plan.SourceImagePath, retailSource))
            throw new InvalidDataException("The moby patch plan was not built from the supplied clean retail BIN.");
        if (!string.Equals(plan.LevelKey, ExpectedLevelKey, StringComparison.OrdinalIgnoreCase) ||
            plan.SourceRecordCount != ExpectedSourceRecordCount ||
            plan.RecordStride != ExpectedRecordStride ||
            !OffsetEquals(plan.SourceTableWadOffset, ExpectedSourceTableWadOffset) ||
            plan.SkippedEdits.Count != 0 ||
            plan.PackageImportPreviews.Count != 0 ||
            plan.LockedChestRuntimeBundle != null)
        {
            throw new InvalidDataException(
                "The edited-donor gate accepts only checked Town Square T21 in-place patches with no skips, packages, bundles, or structural work.");
        }

        bool placementMode =
            plan.PatchCount == 2 &&
            plan.TotalPatchedBytes == PatchByteLength + PlacementPatchByteLength &&
            plan.Patches.Count == 2 &&
            string.Equals(plan.Patches[1].Kind, ExpectedPlacementPatchKind, StringComparison.Ordinal);
        bool xOnlyMode =
            plan.PatchCount == 1 &&
            plan.TotalPatchedBytes == PatchByteLength &&
            plan.Patches.Count == 1;
        bool renderRadiusMode =
            plan.PatchCount == 2 &&
            plan.TotalPatchedBytes == PatchByteLength + RenderRadiusPatchByteLength &&
            plan.Patches.Count == 2 &&
            string.Equals(plan.Patches[1].Kind, ExpectedRenderRadiusPatchKind, StringComparison.Ordinal);
        if (!xOnlyMode && !placementMode && !renderRadiusMode)
        {
            throw new InvalidDataException(
                "The edited-donor gate requires the original X-only control, the rejected X-plus-placement diagnostic, or the isolated X-plus-render-radius diagnostic.");
        }

        MobySourcePatch patch = plan.Patches[0];
        if (!string.Equals(patch.LevelKey, ExpectedLevelKey, StringComparison.OrdinalIgnoreCase) ||
            patch.TrueIndex != ExpectedTrueIndex ||
            !string.Equals(patch.Kind, ExpectedPatchKind, StringComparison.Ordinal) ||
            patch.ByteLength != PatchByteLength ||
            ParseOffset(patch.WadRelativeOffset, "moby patch WAD offset") != DonorPatchWadOffset ||
            !string.Equals(patch.RecordOffset, "0xC", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(patch.MobyLabel))
        {
            throw new InvalidDataException(
                $"The first edited-donor gate requires Town Square T{ExpectedTrueIndex} {ExpectedPatchKind} at 0x{DonorPatchWadOffset:X}.");
        }

        if (plan.EditOutcomes is not { Count: 1 })
            throw new InvalidDataException("The edited-donor gate requires exactly one source edit outcome.");
        MobySourceEditOutcome outcome = plan.EditOutcomes[0];
        if (outcome.EditorTrueIndex != ExpectedTrueIndex ||
            outcome.SkippedReasons.Count != 0 ||
            outcome.PackageOutcomes.Count != 0)
        {
            throw new InvalidDataException("The edited-donor source outcome includes skipped or package work.");
        }

        byte[] before = ParseHexBytes(patch.BeforeHexPreview, PatchByteLength, "T21 X preimage");
        byte[] after = ParseHexBytes(patch.AfterHexPreview, PatchByteLength, "T21 X edited value");
        if (before.SequenceEqual(after))
            throw new InvalidDataException("The edited-donor T21 X patch is a no-op.");
        if (DonorPatchWadOffset < DonorDataWadOffset ||
            DonorPatchWadOffset + PatchByteLength > DonorDataWadOffset + DonorDataByteLength ||
            TargetPatchWadOffset != TargetDataWadOffset + (DonorPatchWadOffset - DonorDataWadOffset))
        {
            throw new InvalidDataException("The checked donor-to-target WAD relocation is malformed.");
        }
        ValidatedPatch xPatch = new(
            patch,
            DonorPatchWadOffset,
            TargetPatchWadOffset,
            before,
            after);
        if (xOnlyMode)
        {
            if (outcome.PatchKinds.Count != 1 ||
                !string.Equals(outcome.PatchKinds[0], ExpectedPatchKind, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The X-only control outcome includes work outside T21 position.x.");
            }
            return new ValidatedPatchPlan(
                [xPatch],
                xPatch,
                null,
                null,
                RecipeId,
                RecipeVersion,
                EvidenceId,
                Status);
        }

        if (!before.SequenceEqual(ExpectedPlacementXBefore) ||
            !after.SequenceEqual(ExpectedPlacementXAfter))
        {
            throw new InvalidDataException(
                "The placement-sector gate requires the exact checked T21 X move 5CE80100 -> 5CE00100.");
        }
        if (renderRadiusMode)
        {
            MobySourcePatch radius = plan.Patches[1];
            if (!string.Equals(radius.LevelKey, ExpectedLevelKey, StringComparison.OrdinalIgnoreCase) ||
                radius.TrueIndex != ExpectedTrueIndex ||
                !string.Equals(radius.Kind, ExpectedRenderRadiusPatchKind, StringComparison.Ordinal) ||
                radius.ByteLength != RenderRadiusPatchByteLength ||
                ParseOffset(radius.WadRelativeOffset, "render-radius patch WAD offset") !=
                    DonorRenderRadiusPatchWadOffset ||
                !string.Equals(radius.RecordOffset, "0x50", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(radius.MobyLabel))
            {
                throw new InvalidDataException(
                    $"The render-radius gate requires Town Square T{ExpectedTrueIndex} {ExpectedRenderRadiusPatchKind} at 0x{DonorRenderRadiusPatchWadOffset:X}.");
            }
            byte[] radiusBefore = ParseHexBytes(
                radius.BeforeHexPreview,
                RenderRadiusPatchByteLength,
                "T21 render-radius preimage");
            byte[] radiusAfter = ParseHexBytes(
                radius.AfterHexPreview,
                RenderRadiusPatchByteLength,
                "T21 render-radius diagnostic value");
            if (!radiusBefore.SequenceEqual(ExpectedRenderRadiusBefore) ||
                !radiusAfter.SequenceEqual(ExpectedRenderRadiusAfter))
            {
                throw new InvalidDataException(
                    "The render-radius gate requires the checked maximum-positive diagnostic 18 -> 7F.");
            }
            if (TargetRenderRadiusPatchWadOffset !=
                    TargetDataWadOffset + (DonorRenderRadiusPatchWadOffset - DonorDataWadOffset) ||
                DonorRenderRadiusPatchWadOffset < DonorDataWadOffset ||
                DonorRenderRadiusPatchWadOffset + RenderRadiusPatchByteLength >
                    DonorDataWadOffset + DonorDataByteLength ||
                TargetRenderRadiusPatchWadOffset / UserSectorByteLength !=
                    TargetPatchWadOffset / UserSectorByteLength)
            {
                throw new InvalidDataException(
                    "The checked render-radius relocation or shared raw-sector boundary is malformed.");
            }
            if (outcome.PatchKinds.Count != 2 ||
                !outcome.PatchKinds.Order(StringComparer.Ordinal).SequenceEqual(
                    new[] { ExpectedPatchKind, ExpectedRenderRadiusPatchKind }.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    "The render-radius gate outcome is not exactly T21 X plus its isolated +0x50 diagnostic.");
            }
            ValidatedPatch radiusPatch = new(
                radius,
                DonorRenderRadiusPatchWadOffset,
                TargetRenderRadiusPatchWadOffset,
                radiusBefore,
                radiusAfter);
            return new ValidatedPatchPlan(
                [xPatch, radiusPatch],
                xPatch,
                null,
                radiusPatch,
                RenderRadiusRecipeId,
                RenderRadiusRecipeVersion,
                RenderRadiusEvidenceId,
                RenderRadiusStatus);
        }

        MobySourcePatch placement = plan.Patches[1];
        if (!string.Equals(placement.LevelKey, ExpectedLevelKey, StringComparison.OrdinalIgnoreCase) ||
            placement.TrueIndex != ExpectedTrueIndex ||
            !string.Equals(placement.Kind, ExpectedPlacementPatchKind, StringComparison.Ordinal) ||
            placement.ByteLength != PlacementPatchByteLength ||
            ParseOffset(placement.WadRelativeOffset, "placement patch WAD offset") !=
                DonorPlacementPatchWadOffset ||
            !string.Equals(placement.RecordOffset, "0x4A", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(placement.MobyLabel))
        {
            throw new InvalidDataException(
                $"The placement-sector gate requires Town Square T{ExpectedTrueIndex} {ExpectedPlacementPatchKind} at 0x{DonorPlacementPatchWadOffset:X}.");
        }
        byte[] placementBefore = ParseHexBytes(
            placement.BeforeHexPreview,
            PlacementPatchByteLength,
            "T21 placement-sector preimage");
        byte[] placementAfter = ParseHexBytes(
            placement.AfterHexPreview,
            PlacementPatchByteLength,
            "T21 placement-sector edited value");
        if (!placementBefore.SequenceEqual(ExpectedPlacementSectorBefore) ||
            !placementAfter.SequenceEqual(ExpectedPlacementSectorAfter))
        {
            throw new InvalidDataException(
                "The placement-sector gate requires the genuine editor-derived byte FF -> D5.");
        }
        if (TargetPlacementPatchWadOffset !=
                TargetDataWadOffset + (DonorPlacementPatchWadOffset - DonorDataWadOffset) ||
            DonorPlacementPatchWadOffset < DonorDataWadOffset ||
            DonorPlacementPatchWadOffset + PlacementPatchByteLength >
                DonorDataWadOffset + DonorDataByteLength ||
            TargetPlacementPatchWadOffset / UserSectorByteLength !=
                TargetPatchWadOffset / UserSectorByteLength)
        {
            throw new InvalidDataException(
                "The checked placement-sector relocation or shared raw-sector boundary is malformed.");
        }
        if (outcome.PatchKinds.Count != 2 ||
            !outcome.PatchKinds.Order(StringComparer.Ordinal).SequenceEqual(
                new[] { ExpectedPlacementPatchKind, ExpectedPatchKind },
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "The placement-sector gate outcome is not exactly T21 X plus its derived placement sector.");
        }
        ValidatedPatch placementPatch = new(
            placement,
            DonorPlacementPatchWadOffset,
            TargetPlacementPatchWadOffset,
            placementBefore,
            placementAfter);
        return new ValidatedPatchPlan(
            [xPatch, placementPatch],
            xPatch,
            placementPatch,
            null,
            PlacementRecipeId,
            PlacementRecipeVersion,
            PlacementEvidenceId,
            PlacementStatus);
    }

    private static StoneHillTownSquareEditedDonorPatchSummary BuildSummary(
        ValidatedPatch patch) =>
        new(
            patch.Source.Label,
            patch.Source.Kind,
            patch.Source.TrueIndex,
            patch.DonorWadOffset,
            patch.TargetWadOffset,
            patch.Before.Length,
            Convert.ToHexString(patch.Before),
            Convert.ToHexString(patch.After),
            Hash(patch.Before),
            Hash(patch.After));

    private static CandidateReadback VerifyReadback(
        string baseImagePath,
        string retailSourcePath,
        string outputImagePath,
        PreparedCandidate prepared,
        CancellationToken cancellationToken)
    {
        DiscLayout baseLayout = RequireMode2Layout(baseImagePath, "display-identity base BIN");
        DiscLayout retailLayout = RequireMode2Layout(retailSourcePath, "clean retail source BIN");
        DiscLayout outputLayout = RequireMode2Layout(outputImagePath, "edited-donor output BIN");
        if (baseLayout.SectorSize != outputLayout.SectorSize ||
            baseLayout.UserOffset != outputLayout.UserOffset)
            throw new InvalidDataException("The edited-donor disc layout changed.");

        using FileStream baseline = File.OpenRead(baseImagePath);
        using FileStream retail = File.OpenRead(retailSourcePath);
        using FileStream output = File.OpenRead(outputImagePath);
        foreach (ValidatedPatch patch in prepared.Patches)
        {
            byte[] outputTarget = DiscImage.ReadFileBytes(
                output,
                outputLayout,
                WadLba,
                patch.TargetWadOffset,
                patch.After.Length);
            VerifyBytesEqual(
                patch.After,
                outputTarget,
                $"The edited-donor target readback does not match {patch.Source.Kind}.");
        }

        byte[] outputDonorData = DiscImage.ReadFileBytes(
            output, outputLayout, WadLba, DonorDataWadOffset, DonorDataByteLength);
        VerifyBytesEqual(prepared.RetailDonorData, outputDonorData, "The edited-donor candidate changed original Town Square.");
        byte[] retailDonorData = DiscImage.ReadFileBytes(
            retail, retailLayout, WadLba, DonorDataWadOffset, DonorDataByteLength);
        VerifyBytesEqual(prepared.RetailDonorData, retailDonorData, "The clean retail Town Square donor changed during readback.");

        DiscFileRecord outputExecutable = DiscImage.FindRootFileRecord(output, outputLayout, name =>
            name.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        if (outputExecutable.Lba != ExecutableLba || outputExecutable.Size != ExecutableByteLength)
            throw new InvalidDataException("The edited-donor candidate moved the executable extent.");
        byte[] outputExecutableBytes = DiscImage.ReadFileBytes(
            output, outputLayout, outputExecutable.Lba, 0, outputExecutable.Size);
        VerifyBytesEqual(prepared.IdentityExecutable, outputExecutableBytes, "The edited-donor candidate changed the display-identity executable.");
        RequireHash(Hash(outputExecutableBytes), IdentityExecutableSha256, "edited-donor output executable");

        long expectedLogicalChanges = prepared.Patches.Sum(patch =>
            CountChanged(patch.Before, patch.After));
        long allowedRawSector = checked(
            WadLba + (prepared.Patches[0].TargetWadOffset / UserSectorByteLength));
        UserPatchRange[] allowedRanges = prepared.Patches
            .Select(patch =>
            {
                long rawSector = checked(
                    WadLba + (patch.TargetWadOffset / UserSectorByteLength));
                if (rawSector != allowedRawSector)
                {
                    throw new InvalidDataException(
                        "The edited-donor first gates require every target patch to share one raw WAD sector.");
                }
                return new UserPatchRange(
                    checked((int)(patch.TargetWadOffset % UserSectorByteLength)),
                    patch.After.Length);
            })
            .ToArray();
        ImageDiff diff = CompareImages(
            baseline,
            output,
            allowedRawSector,
            allowedRanges,
            cancellationToken);
        if (diff.ChangedLogicalBytes != expectedLogicalChanges || diff.OutsideLogicalBytes != 0)
        {
            throw new InvalidDataException(
                $"Edited-donor logical diff failed: changed={diff.ChangedLogicalBytes}, outside={diff.OutsideLogicalBytes}; expected {expectedLogicalChanges} changed byte(s) only in the checked T21 patch range(s).");
        }
        if (diff.ChangedPhysicalBytes <= expectedLogicalChanges || diff.OutsidePhysicalSectorBytes != 0)
        {
            throw new InvalidDataException(
                $"Edited-donor physical diff failed: changed={diff.ChangedPhysicalBytes}, outside={diff.OutsidePhysicalSectorBytes}.");
        }
        return new CandidateReadback(diff.ChangedLogicalBytes, diff.ChangedPhysicalBytes);
    }

    private static ImageDiff CompareImages(
        FileStream baseline,
        FileStream output,
        long allowedRawSector,
        IReadOnlyList<UserPatchRange> allowedUserRanges,
        CancellationToken cancellationToken)
    {
        if (baseline.Length != output.Length || baseline.Length % RawSectorByteLength != 0)
            throw new InvalidDataException("The edited-donor candidate changed the physical BIN length or alignment.");
        byte[] before = new byte[RawSectorByteLength];
        byte[] after = new byte[RawSectorByteLength];
        long changedPhysical = 0;
        long outsidePhysical = 0;
        long changedLogical = 0;
        long outsideLogical = 0;
        long sectorCount = baseline.Length / RawSectorByteLength;
        baseline.Position = 0;
        output.Position = 0;
        for (long sector = 0; sector < sectorCount; sector++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            baseline.ReadExactly(before);
            output.ReadExactly(after);
            for (int index = 0; index < RawSectorByteLength; index++)
            {
                if (before[index] == after[index])
                    continue;
                changedPhysical++;
                if (sector != allowedRawSector)
                    outsidePhysical++;
                if (index < UserSectorOffset || index >= UserSectorOffset + UserSectorByteLength)
                    continue;
                changedLogical++;
                int userIndex = index - UserSectorOffset;
                if (sector != allowedRawSector ||
                    !allowedUserRanges.Any(range =>
                        userIndex >= range.Offset &&
                        userIndex < range.Offset + range.ByteLength))
                    outsideLogical++;
            }
        }
        return new ImageDiff(changedLogical, outsideLogical, changedPhysical, outsidePhysical);
    }

    private static long CountChanged(byte[] before, byte[] after)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException("The edited-donor before/after lengths differ.");
        long changed = 0;
        for (int index = 0; index < before.Length; index++)
        {
            if (before[index] != after[index])
                changed++;
        }
        return changed;
    }

    private static DiscLayout RequireMode2Layout(string path, string label)
    {
        DiscLayout layout = DiscImage.DetectLayout(path);
        if (layout.SectorSize != RawSectorByteLength || layout.UserOffset != UserSectorOffset)
            throw new InvalidDataException($"The {label} is not the checked MODE2/2352 layout.");
        return layout;
    }

    private static byte[] ParseHexBytes(string value, int requiredLength, string label)
    {
        string compact = new((value ?? "").Where(Uri.IsHexDigit).ToArray());
        if (compact.Length != requiredLength * 2)
            throw new InvalidDataException($"The {label} is not exactly {requiredLength} bytes.");
        try
        {
            return Convert.FromHexString(compact);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException($"The {label} is not valid hexadecimal.", exception);
        }
    }

    private static long ParseOffset(string value, string label)
    {
        string text = (value ?? "").Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            text = text[2..];
        if (!long.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long result))
            throw new InvalidDataException($"The {label} '{value}' is invalid.");
        return result;
    }

    private static bool OffsetEquals(string actual, string expected) =>
        ParseOffset(actual, "source table WAD offset") == ParseOffset(expected, "expected source table WAD offset");

    private static string RequireExistingFile(string path, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"The {label} does not exist.", fullPath);
        return fullPath;
    }

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!NativeLevelReplacementProfile.ShaEquals(actual, expected))
            throw new InvalidDataException($"The {label} SHA-256 is {actual}, expected {expected}.");
    }

    private static void VerifyBytesEqual(byte[] expected, byte[] actual, string message)
    {
        if (!expected.SequenceEqual(actual))
            throw new InvalidDataException(message);
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static void EnsureDistinctPaths(params string[] paths)
    {
        for (int left = 0; left < paths.Length; left++)
        {
            for (int right = left + 1; right < paths.Length; right++)
            {
                if (PathEquals(paths[left], paths[right]))
                    throw new InvalidOperationException("Edited-donor input and output roles must use distinct paths.");
            }
        }
    }

    private static bool PathEquals(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

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

    private sealed record ValidatedPatch(
        MobySourcePatch Source,
        long DonorWadOffset,
        long TargetWadOffset,
        byte[] Before,
        byte[] After);

    private sealed record ValidatedPatchPlan(
        IReadOnlyList<ValidatedPatch> Patches,
        ValidatedPatch XPatch,
        ValidatedPatch? PlacementPatch,
        ValidatedPatch? RenderRadiusPatch,
        string RecipeId,
        int RecipeVersion,
        string EvidenceId,
        string Status);

    private sealed record PreparedCandidate(
        IReadOnlyList<ValidatedPatch> Patches,
        ValidatedPatch? PlacementPatch,
        ValidatedPatch? RenderRadiusPatch,
        byte[] RetailDonorData,
        byte[] IdentityExecutable,
        StoneHillTownSquareEditedDonorCandidatePlan Plan);

    private readonly record struct UserPatchRange(
        int Offset,
        int ByteLength);

    private sealed record ImageDiff(
        long ChangedLogicalBytes,
        long OutsideLogicalBytes,
        long ChangedPhysicalBytes,
        long OutsidePhysicalSectorBytes);

    private sealed record CandidateReadback(
        long ChangedLogicalWadBytes,
        long ChangedPhysicalImageBytes);
}

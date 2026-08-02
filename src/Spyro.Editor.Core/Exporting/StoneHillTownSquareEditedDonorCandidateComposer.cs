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
    StoneHillTownSquareReplacementSafetyReport Safety);

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
    bool BinCuePublishCompleted);

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
    private const int ExpectedTrueIndex = 21;
    private const int ExpectedSourceRecordCount = 107;
    private const int ExpectedRecordStride = 0x58;
    private const string ExpectedLevelKey = "townsquare";
    private const string ExpectedPatchKind = "moby-position-x";
    private const string ExpectedSourceTableWadOffset = "0x136E170";
    private const string IdentityBaseImageSha256 =
        "71808a4b5e0d0891e4f6f49be8b2712de018a393695b1b606b79e9ecbd3166c9";
    private const string IdentityExecutableSha256 =
        "b17fd7679d586562ef325813b7c469aff58430f5200633f64b8e6900409e3304";

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
                byte[] current = DiscImage.ReadFileBytes(
                    output,
                    layout,
                    WadLba,
                    TargetPatchWadOffset,
                    PatchByteLength);
                VerifyBytesEqual(
                    prepared.Before,
                    current,
                    "The staged transplanted Town Square T21 X preimage changed.");
                DiscImage.WriteFileBytes(
                    output,
                    layout,
                    WadLba,
                    TargetPatchWadOffset,
                    prepared.After);
                rebuiltRawSectors = RawMode2Form1SectorIntegrity.RebuildFileRanges(
                    output,
                    layout,
                    WadLba,
                    [(TargetPatchWadOffset, PatchByteLength)]);
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
                BinCuePublishCompleted: true);
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

        ValidatedPatch patch = ValidatePatchPlan(request.MobyPatchPlan, retailSource);
        DiscLayout baseLayout = RequireMode2Layout(baseImage, "display-identity base BIN");
        DiscLayout retailLayout = RequireMode2Layout(retailSource, "clean retail source BIN");
        if (new FileInfo(baseImage).Length != new FileInfo(retailSource).Length)
            throw new InvalidDataException("The display-identity and retail BIN lengths differ.");

        byte[] retailDonorData;
        byte[] baseDonorData;
        byte[] baseTargetBefore;
        byte[] executableBytes;
        await using (FileStream retail = File.OpenRead(retailSource))
        await using (FileStream identity = File.OpenRead(baseImage))
        {
            retailDonorData = DiscImage.ReadFileBytes(
                retail, retailLayout, WadLba, DonorDataWadOffset, DonorDataByteLength);
            baseDonorData = DiscImage.ReadFileBytes(
                identity, baseLayout, WadLba, DonorDataWadOffset, DonorDataByteLength);
            byte[] retailDonorBefore = DiscImage.ReadFileBytes(
                retail, retailLayout, WadLba, DonorPatchWadOffset, PatchByteLength);
            byte[] baseDonorBefore = DiscImage.ReadFileBytes(
                identity, baseLayout, WadLba, DonorPatchWadOffset, PatchByteLength);
            baseTargetBefore = DiscImage.ReadFileBytes(
                identity, baseLayout, WadLba, TargetPatchWadOffset, PatchByteLength);
            VerifyBytesEqual(patch.Before, retailDonorBefore, "The patch plan does not match the clean retail donor preimage.");
            VerifyBytesEqual(patch.Before, baseDonorBefore, "The display-identity BIN's native Town Square donor preimage changed.");
            VerifyBytesEqual(patch.Before, baseTargetBefore, "The transplanted Town Square T21 X preimage does not match retail.");
            VerifyBytesEqual(retailDonorData, baseDonorData, "The display-identity BIN changed the original Town Square data entry.");

            DiscFileRecord executable = DiscImage.FindRootFileRecord(identity, baseLayout, name =>
                name.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
            if (executable.Lba != ExecutableLba || executable.Size != ExecutableByteLength)
                throw new InvalidDataException("The display-identity executable extent changed.");
            executableBytes = DiscImage.ReadFileBytes(
                identity, baseLayout, executable.Lba, 0, executable.Size);
            RequireHash(Hash(executableBytes), IdentityExecutableSha256, "display-identity executable");
        }

        StoneHillTownSquareEditedDonorPatchSummary summary = new(
            patch.Source.Label,
            patch.Source.Kind,
            patch.Source.TrueIndex,
            DonorPatchWadOffset,
            TargetPatchWadOffset,
            PatchByteLength,
            Convert.ToHexString(patch.Before),
            Convert.ToHexString(patch.After),
            Hash(patch.Before),
            Hash(patch.After));
        StoneHillTownSquareReplacementSafetyReport safety = new(
            Status,
            WritableScopes:
            [
                "Stone Hill target data entry: transplanted Town Square T21 position.x word only",
                "MODE2 EDC/ECC for the single raw WAD sector containing that word"
            ],
            ProtectedScopes:
            [
                "The exact runtime-proven display-identity control BIN",
                "The clean retail source BIN and original Town Square data entry",
                "Every other transplanted Town Square byte, WAD entry, and raw sector",
                "SCUS, slot identity, completion totals, music, title demo reroute, and save ownership",
                "Actor packages, appends, removals, state/type/yaw/Z/Y edits, paths, terrain, and textures",
                "Normal V4 Create BIN, projects, releases, and update channel"
            ],
            RuntimeChecks:
            [
                "Cold-boot the generated CUE in DuckStation; static proof is not runtime proof",
                "Enter the replaced Town Square through Stone Hill's slot and locate the moved T21 actor",
                "Exercise gameplay, collection, dragon/egg progress, death/reload, Return Home, re-entry, and save/reload",
                "Confirm the original retail Town Square slot remains unchanged and independently playable"
            ],
            RequiresDuckStationRuntimeProof: true);
        StoneHillTownSquareEditedDonorCandidatePlan candidatePlan = new(
            DateTimeOffset.UtcNow,
            RecipeId,
            RecipeVersion,
            NativeLevelReplacementEvidenceStatus.StaticBaselineOnly,
            EvidenceId,
            "Static preimage, relocation, readback, source-preservation, and raw-sector boundary checks passed; DuckStation runtime proof is pending.",
            baseImageSha256,
            retailSourceSha256,
            IdentityExecutableSha256,
            DonorDataWadOffset,
            TargetDataWadOffset,
            summary,
            safety);
        return new PreparedCandidate(
            patch.Before,
            patch.After,
            retailDonorData,
            executableBytes,
            candidatePlan);
    }

    private static ValidatedPatch ValidatePatchPlan(MobySourcePatchPlan plan, string retailSource)
    {
        if (!PathEquals(plan.SourceImagePath, retailSource))
            throw new InvalidDataException("The moby patch plan was not built from the supplied clean retail BIN.");
        if (!string.Equals(plan.LevelKey, ExpectedLevelKey, StringComparison.OrdinalIgnoreCase) ||
            plan.SourceRecordCount != ExpectedSourceRecordCount ||
            plan.RecordStride != ExpectedRecordStride ||
            !OffsetEquals(plan.SourceTableWadOffset, ExpectedSourceTableWadOffset) ||
            plan.PatchCount != 1 ||
            plan.TotalPatchedBytes != PatchByteLength ||
            plan.Patches.Count != 1 ||
            plan.SkippedEdits.Count != 0 ||
            plan.PackageImportPreviews.Count != 0 ||
            plan.LockedChestRuntimeBundle != null)
        {
            throw new InvalidDataException(
                "The edited-donor gate accepts exactly one plain Town Square T21 X-coordinate patch with no skips, packages, bundles, or structural work.");
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
            outcome.PackageOutcomes.Count != 0 ||
            outcome.PatchKinds.Count != 1 ||
            !string.Equals(outcome.PatchKinds[0], ExpectedPatchKind, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The edited-donor source outcome includes work outside T21 position.x.");
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
        return new ValidatedPatch(patch, before, after);
    }

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
        byte[] outputTarget = DiscImage.ReadFileBytes(
            output, outputLayout, WadLba, TargetPatchWadOffset, PatchByteLength);
        VerifyBytesEqual(prepared.After, outputTarget, "The edited-donor target readback does not match the requested T21 X value.");

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

        long expectedLogicalChanges = CountChanged(prepared.Before, prepared.After);
        ImageDiff diff = CompareImages(
            baseline,
            output,
            checked(WadLba + (TargetPatchWadOffset / UserSectorByteLength)),
            checked((int)(TargetPatchWadOffset % UserSectorByteLength)),
            PatchByteLength,
            cancellationToken);
        if (diff.ChangedLogicalBytes != expectedLogicalChanges || diff.OutsideLogicalBytes != 0)
        {
            throw new InvalidDataException(
                $"Edited-donor logical diff failed: changed={diff.ChangedLogicalBytes}, outside={diff.OutsideLogicalBytes}; expected {expectedLogicalChanges} changed byte(s) only at target T21 X.");
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
        int allowedUserOffset,
        int allowedUserLength,
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
                    userIndex < allowedUserOffset ||
                    userIndex >= allowedUserOffset + allowedUserLength)
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
        byte[] Before,
        byte[] After);

    private sealed record PreparedCandidate(
        byte[] Before,
        byte[] After,
        byte[] RetailDonorData,
        byte[] IdentityExecutable,
        StoneHillTownSquareEditedDonorCandidatePlan Plan);

    private sealed record ImageDiff(
        long ChangedLogicalBytes,
        long OutsideLogicalBytes,
        long ChangedPhysicalBytes,
        long OutsidePhysicalSectorBytes);

    private sealed record CandidateReadback(
        long ChangedLogicalWadBytes,
        long ChangedPhysicalImageBytes);
}

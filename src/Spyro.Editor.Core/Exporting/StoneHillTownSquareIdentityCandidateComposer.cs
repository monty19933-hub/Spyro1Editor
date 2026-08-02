using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

public sealed record StoneHillTownSquareIdentityCandidateRequest(
    string BaseImagePath,
    string BaseCuePath,
    string OutputImagePath,
    string OutputCuePath);

public sealed record StoneHillTownSquareIdentityTotals(
    int Gems,
    int Dragons,
    int Eggs);

public sealed record StoneHillTownSquareIdentityCandidatePlan(
    DateTimeOffset GeneratedAtUtc,
    string RecipeId,
    int RecipeVersion,
    string BaseProfileId,
    NativeLevelReplacementEvidenceStatus Evidence,
    string BaseImageSha256,
    string ExpectedOutputImageSha256,
    string BaseExecutableSha256,
    string OutputExecutableSha256,
    long NamePointerTableFileOffset,
    long StoneHillNamePointerFileOffset,
    long TownSquareNamePointerFileOffset,
    string StoneHillNamePointerBefore,
    string StoneHillNamePointerAfter,
    StoneHillTownSquareIdentityTotals StoneHillTotals,
    StoneHillTownSquareIdentityTotals TownSquareTotals,
    IReadOnlyList<StoneHillTownSquareReplacementPatchSummary> Patches,
    StoneHillTownSquareReplacementSafetyReport Safety);

public sealed record StoneHillTownSquareIdentityCandidateResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    StoneHillTownSquareIdentityCandidatePlan Plan,
    long ChangedLogicalExecutableBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool NamePointerReadbackVerified,
    bool CountTablesPreserved,
    bool BaseCandidatePreserved,
    bool BinCuePublishCompleted);

/// <summary>
/// Pending-runtime V5 identity experiment. It starts from the exact runtime-proven
/// Town-Square-in-Stone-Hill BIN and changes only Stone Hill's indexed level-name
/// pointer to the already-present Town Square string. Save/progression ownership
/// remains retail slot 11. Normal V4 Create BIN never calls this composer.
/// </summary>
public static class StoneHillTownSquareIdentityCandidateComposer
{
    public const string RecipeId =
        "native-level-replacement-stonehill-townsquare-display-identity-clean-usa-v1";
    public const int RecipeVersion = 1;

    private const int ExecutableLba = 53875;
    private const int ExecutableByteLength = 0x66000;
    private const long NamePointerTableFileOffset = 0x5FFF0;
    private const long StoneHillNamePointerFileOffset = 0x5FFF4;
    private const long TownSquareNamePointerFileOffset = 0x5FFFC;
    private const int StoneHillNameStringFileOffset = 0x9FC;
    private const int TownSquareNameStringFileOffset = 0x9E4;
    private const long DragonTotalsTableFileOffset = 0x5FC14;
    private const long TreasureTotalsTableFileOffset = 0x5FC38;
    private const long EggTotalsTableFileOffset = 0x5FC80;
    private const int StoneHillTableIndex = 1;
    private const int TownSquareTableIndex = 3;
    private const int RawSectorByteLength = 2352;
    private const int UserSectorByteLength = 2048;

    private const string BaseExecutableSha256 =
        "558d4f5f0f7dd482b035d5f5793bfc6cf886d9cdd218562f4cedd4b1effbfab9";
    private const string OutputExecutableSha256 =
        "b17fd7679d586562ef325813b7c469aff58430f5200633f64b8e6900409e3304";

    public const string ExpectedOutputImageSha256 =
        "71808a4b5e0d0891e4f6f49be8b2712de018a393695b1b606b79e9ecbd3166c9";

    private static readonly byte[] StoneHillNamePointer = Convert.FromHexString("FC010180");
    private static readonly byte[] TownSquareNamePointer = Convert.FromHexString("E4010180");
    private static readonly byte[] StoneHillName = Encoding.ASCII.GetBytes("STONE HILL\0");
    private static readonly byte[] TownSquareName = Encoding.ASCII.GetBytes("TOWN SQUARE\0");

    public static async Task<StoneHillTownSquareIdentityCandidatePlan> BuildPlanAsync(
        StoneHillTownSquareIdentityCandidateRequest request,
        CancellationToken cancellationToken = default) =>
        (await PrepareAsync(request, cancellationToken)).Plan;

    public static async Task<StoneHillTownSquareIdentityCandidateResult> ExportAsync(
        StoneHillTownSquareIdentityCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        PreparedIdentityCandidate prepared = await PrepareAsync(request, cancellationToken);
        string baseImage = Path.GetFullPath(request.BaseImagePath);
        string baseCue = Path.GetFullPath(request.BaseCuePath);
        string outputImage = Path.GetFullPath(request.OutputImagePath);
        string outputCue = Path.GetFullPath(request.OutputCuePath);
        NativeLevelReplacementBaselineExporter.EnsureDistinctRoles(
            baseImage,
            baseCue,
            outputImage,
            outputCue);
        if (!PathEquals(Path.GetDirectoryName(outputImage), Path.GetDirectoryName(outputCue)))
            throw new InvalidOperationException("The identity candidate BIN and CUE must share one directory.");

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
                temporaryImage,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None))
            {
                byte[] current = DiscImage.ReadFileBytes(
                    output,
                    layout,
                    prepared.Executable.Lba,
                    StoneHillNamePointerFileOffset,
                    StoneHillNamePointer.Length);
                VerifyBytesEqual(
                    StoneHillNamePointer,
                    current,
                    "The staged identity candidate no longer has Stone Hill's guarded name pointer.");
                DiscImage.WriteFileBytes(
                    output,
                    layout,
                    prepared.Executable.Lba,
                    StoneHillNamePointerFileOffset,
                    TownSquareNamePointer);
                rebuiltRawSectors = RawMode2Form1SectorIntegrity.RebuildFileRanges(
                    output,
                    layout,
                    prepared.Executable.Lba,
                    [(StoneHillNamePointerFileOffset, TownSquareNamePointer.Length)]);
                output.Flush(flushToDisk: true);
            }

            IdentityReadback readback = VerifyReadback(
                baseImage,
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
            if (!NativeLevelReplacementProfile.ShaEquals(
                    outputSha256,
                    ExpectedOutputImageSha256))
            {
                throw new InvalidDataException(
                    $"The identity candidate SHA-256 is {outputSha256}, expected " +
                    $"{ExpectedOutputImageSha256}.");
            }

            string baseSha256After = await HashFileAsync(baseImage, cancellationToken);
            if (!NativeLevelReplacementProfile.ShaEquals(
                    baseSha256After,
                    NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillOutputImageSha256))
            {
                throw new InvalidDataException("The runtime-proven base BIN changed during identity export.");
            }

            TryDelete(backupImage);
            TryDelete(backupCue);
            return new StoneHillTownSquareIdentityCandidateResult(
                outputImage,
                outputCue,
                outputSha256,
                prepared.Plan,
                readback.ChangedLogicalExecutableBytes,
                readback.ChangedPhysicalImageBytes,
                rebuiltRawSectors,
                ExactLogicalDiffBoundaryVerified: true,
                ExactPhysicalSectorBoundaryVerified: true,
                NamePointerReadbackVerified: true,
                CountTablesPreserved: true,
                BaseCandidatePreserved: true,
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
                    "The identity candidate failed and prior output recovery was incomplete.",
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

    private static async Task<PreparedIdentityCandidate> PrepareAsync(
        StoneHillTownSquareIdentityCandidateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string baseImage = RequireExistingFile(request.BaseImagePath, "runtime-proven base BIN");
        string baseCue = RequireExistingFile(request.BaseCuePath, "runtime-proven base CUE");
        NativeLevelReplacementBaselineExporter.ValidateCue(baseCue, baseImage, "MODE2/2352");
        string baseImageSha256 = await HashFileAsync(baseImage, cancellationToken);
        if (!NativeLevelReplacementProfile.ShaEquals(
                baseImageSha256,
                NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillOutputImageSha256))
        {
            throw new InvalidDataException(
                $"The identity experiment requires the exact runtime-proven base BIN " +
                $"{NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillOutputImageSha256}, " +
                $"not {baseImageSha256}.");
        }

        DiscLayout layout = DiscImage.DetectLayout(baseImage);
        if (layout.SectorSize != RawSectorByteLength || layout.UserOffset != 24)
            throw new InvalidDataException("The identity experiment requires the checked MODE2/2352 layout.");
        await using FileStream input = new(baseImage, FileMode.Open, FileAccess.Read, FileShare.Read);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(input, layout, name =>
            name.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        if (executable.Lba != ExecutableLba || executable.Size != ExecutableByteLength)
            throw new InvalidDataException("The exact runtime-proven SCUS extent changed.");

        byte[] executableBytes = DiscImage.ReadFileBytes(
            input,
            layout,
            executable.Lba,
            0,
            executable.Size);
        RequireHash(executableBytes, BaseExecutableSha256, "runtime-proven base executable");
        RequireSlice(
            executableBytes,
            StoneHillNamePointerFileOffset,
            StoneHillNamePointer,
            "Stone Hill name pointer");
        RequireSlice(
            executableBytes,
            TownSquareNamePointerFileOffset,
            TownSquareNamePointer,
            "Town Square name pointer");
        RequireSlice(executableBytes, StoneHillNameStringFileOffset, StoneHillName, "STONE HILL name string");
        RequireSlice(executableBytes, TownSquareNameStringFileOffset, TownSquareName, "TOWN SQUARE name string");

        StoneHillTownSquareIdentityTotals stoneHillTotals = ReadTotals(executableBytes, StoneHillTableIndex);
        StoneHillTownSquareIdentityTotals townSquareTotals = ReadTotals(executableBytes, TownSquareTableIndex);
        StoneHillTownSquareIdentityTotals expectedTotals = new(200, 4, 1);
        if (stoneHillTotals != expectedTotals || townSquareTotals != expectedTotals)
        {
            throw new InvalidDataException(
                $"The guarded retail totals changed: Stone Hill {stoneHillTotals}, " +
                $"Town Square {townSquareTotals}.");
        }

        byte[] patchedExecutable = executableBytes.ToArray();
        TownSquareNamePointer.CopyTo(patchedExecutable, (int)StoneHillNamePointerFileOffset);
        RequireHash(patchedExecutable, OutputExecutableSha256, "identity candidate executable");

        IReadOnlyList<StoneHillTownSquareReplacementPatchSummary> patches =
        [
            new(
                executable.Name,
                "alias-stone-hill-level-name-to-town-square",
                StoneHillNamePointerFileOffset,
                TownSquareNamePointer.Length,
                Hash(StoneHillNamePointer),
                Hash(TownSquareNamePointer))
        ];
        StoneHillTownSquareReplacementSafetyReport safety = new(
            "runtime-candidate-pending-duckstation",
            WritableScopes:
            [
                "SCUS indexed level-name pointer for retail slot 11 only"
            ],
            ProtectedScopes:
            [
                "The exact runtime-proven complete Town Square payload and level-11 dispatch",
                "Every WAD byte, actor package, terrain, collision, texture, and scene byte",
                "Stone Hill/Town Square gem, dragon, and egg total tables",
                "Level ID 11 and its independent save/progression ownership",
                "The original Town Square slot 13 payload and save/progression ownership",
                "Normal V4 Create BIN, projects, releases, and update channel"
            ],
            RuntimeChecks:
            [
                "Use a fresh game or disposable memory card; slot-11 progress remains independent",
                "Confirm the Stone Hill portal, fly-in text, guidebook, and Inventory now display Town Square",
                "Confirm slot 11 starts at 0/200 gems, 0/4 dragons, and 0/1 egg",
                "Collect one gem, rescue one dragon, and collect the egg; each counter must advance exactly once",
                "Die/reload, Return Home, re-enter, save/reload, and revisit",
                "Enter the original Town Square portal and confirm its separate slot-13 progress remains intact",
                "Stone Hill music, duplicate Doctor Shemp demo, and the brief sky handoff remain expected"
            ],
            RequiresDuckStationRuntimeProof: true);
        StoneHillTownSquareIdentityCandidatePlan plan = new(
            DateTimeOffset.UtcNow,
            RecipeId,
            RecipeVersion,
            NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillProfileId,
            NativeLevelReplacementEvidenceStatus.StaticBaselineOnly,
            baseImageSha256,
            ExpectedOutputImageSha256,
            BaseExecutableSha256,
            OutputExecutableSha256,
            NamePointerTableFileOffset,
            StoneHillNamePointerFileOffset,
            TownSquareNamePointerFileOffset,
            Convert.ToHexString(StoneHillNamePointer),
            Convert.ToHexString(TownSquareNamePointer),
            stoneHillTotals,
            townSquareTotals,
            patches,
            safety);
        return new PreparedIdentityCandidate(executable, executableBytes, plan);
    }

    private static IdentityReadback VerifyReadback(
        string baseImagePath,
        string outputImagePath,
        PreparedIdentityCandidate prepared,
        CancellationToken cancellationToken)
    {
        DiscLayout baseLayout = DiscImage.DetectLayout(baseImagePath);
        DiscLayout outputLayout = DiscImage.DetectLayout(outputImagePath);
        if (baseLayout.SectorSize != outputLayout.SectorSize ||
            baseLayout.UserOffset != outputLayout.UserOffset)
            throw new InvalidDataException("The identity candidate disc layout changed.");

        using FileStream baseline = File.OpenRead(baseImagePath);
        using FileStream output = File.OpenRead(outputImagePath);
        DiscFileRecord baseExecutable = DiscImage.FindRootFileRecord(baseline, baseLayout, name =>
            name.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord outputExecutable = DiscImage.FindRootFileRecord(output, outputLayout, name =>
            name.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        if (baseExecutable != outputExecutable || baseExecutable != prepared.Executable)
            throw new InvalidDataException("The identity candidate moved the executable extent.");

        byte[] baseExecutableBytes = DiscImage.ReadFileBytes(
            baseline,
            baseLayout,
            baseExecutable.Lba,
            0,
            baseExecutable.Size);
        byte[] outputExecutableBytes = DiscImage.ReadFileBytes(
            output,
            outputLayout,
            outputExecutable.Lba,
            0,
            outputExecutable.Size);
        VerifyBytesEqual(
            prepared.BaseExecutableBytes,
            baseExecutableBytes,
            "The runtime-proven base executable changed during identity readback.");
        RequireHash(outputExecutableBytes, OutputExecutableSha256, "identity candidate executable readback");
        RequireSlice(
            outputExecutableBytes,
            StoneHillNamePointerFileOffset,
            TownSquareNamePointer,
            "identity candidate Stone Hill name pointer");
        RequireSlice(
            outputExecutableBytes,
            TownSquareNamePointerFileOffset,
            TownSquareNamePointer,
            "identity candidate native Town Square name pointer");
        RequireSlice(outputExecutableBytes, StoneHillNameStringFileOffset, StoneHillName, "preserved STONE HILL string");
        RequireSlice(outputExecutableBytes, TownSquareNameStringFileOffset, TownSquareName, "preserved TOWN SQUARE string");

        if (ReadTotals(outputExecutableBytes, StoneHillTableIndex) != prepared.Plan.StoneHillTotals ||
            ReadTotals(outputExecutableBytes, TownSquareTableIndex) != prepared.Plan.TownSquareTotals)
            throw new InvalidDataException("The identity candidate changed a guarded completion-total table.");

        long changedLogical = 0;
        long outsideLogical = 0;
        for (int index = 0; index < baseExecutableBytes.Length; index++)
        {
            if (baseExecutableBytes[index] == outputExecutableBytes[index])
                continue;
            changedLogical++;
            if (index < StoneHillNamePointerFileOffset ||
                index >= StoneHillNamePointerFileOffset + StoneHillNamePointer.Length)
                outsideLogical++;
        }
        if (changedLogical != 1 || outsideLogical != 0)
        {
            throw new InvalidDataException(
                $"Identity logical diff failed: changed={changedLogical}, outside={outsideLogical}; expected one byte.");
        }

        long affectedSector = outputExecutable.Lba + (StoneHillNamePointerFileOffset / UserSectorByteLength);
        long allowedPhysicalStart = affectedSector * RawSectorByteLength;
        (long changedPhysical, long outsidePhysical) = ComparePhysicalImages(
            baseline,
            output,
            allowedPhysicalStart,
            RawSectorByteLength,
            cancellationToken);
        if (changedPhysical <= 1 || outsidePhysical != 0)
        {
            throw new InvalidDataException(
                $"Identity physical diff failed: changed={changedPhysical}, outside={outsidePhysical}; " +
                "only the rebuilt raw SCUS sector may differ.");
        }
        return new IdentityReadback(changedLogical, changedPhysical);
    }

    private static StoneHillTownSquareIdentityTotals ReadTotals(byte[] executable, int tableIndex)
    {
        int dragons = executable[checked((int)DragonTotalsTableFileOffset + tableIndex)];
        int gems = BinaryPrimitives.ReadUInt16LittleEndian(
            executable.AsSpan(checked((int)TreasureTotalsTableFileOffset + (tableIndex * 2)), 2));
        int eggs = executable[checked((int)EggTotalsTableFileOffset + tableIndex)];
        return new StoneHillTownSquareIdentityTotals(gems, dragons, eggs);
    }

    private static (long Changed, long Outside) ComparePhysicalImages(
        FileStream baseline,
        FileStream output,
        long allowedOffset,
        long allowedLength,
        CancellationToken cancellationToken)
    {
        if (baseline.Length != output.Length)
            throw new InvalidDataException("The identity candidate changed the physical BIN length.");
        const int chunkSize = 1 << 20;
        byte[] before = new byte[chunkSize];
        byte[] after = new byte[chunkSize];
        long changed = 0;
        long outside = 0;
        baseline.Position = 0;
        output.Position = 0;
        for (long offset = 0; offset < baseline.Length; offset += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int length = (int)Math.Min(chunkSize, baseline.Length - offset);
            baseline.ReadExactly(before.AsSpan(0, length));
            output.ReadExactly(after.AsSpan(0, length));
            for (int index = 0; index < length; index++)
            {
                if (before[index] == after[index])
                    continue;
                changed++;
                long physicalOffset = offset + index;
                if (physicalOffset < allowedOffset || physicalOffset >= allowedOffset + allowedLength)
                    outside++;
            }
        }
        return (changed, outside);
    }

    private static string RequireExistingFile(string path, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"The {label} does not exist.", fullPath);
        return fullPath;
    }

    private static void RequireHash(ReadOnlySpan<byte> bytes, string expected, string label)
    {
        string actual = Hash(bytes);
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {label} SHA-256 is {actual}, expected {expected}.");
    }

    private static void RequireSlice(byte[] source, long offset, byte[] expected, string label)
    {
        if (offset < 0 || offset + expected.Length > source.Length ||
            !source.AsSpan((int)offset, expected.Length).SequenceEqual(expected))
            throw new InvalidDataException($"The {label} preimage changed.");
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

    private sealed record PreparedIdentityCandidate(
        DiscFileRecord Executable,
        byte[] BaseExecutableBytes,
        StoneHillTownSquareIdentityCandidatePlan Plan);

    private sealed record IdentityReadback(
        long ChangedLogicalExecutableBytes,
        long ChangedPhysicalImageBytes);
}

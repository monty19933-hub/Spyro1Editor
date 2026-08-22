using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

public sealed record UnusedLevel65DisplayNameCandidateRequest(
    string BaseImagePath,
    string BaseCuePath,
    string OutputImagePath,
    string OutputCuePath);

public sealed record UnusedLevel65DisplayNameCandidatePlan(
    DateTimeOffset GeneratedAtUtc,
    string ProfileId,
    string BaseProfileId,
    string BaseImageSha256,
    string BaseExecutableSha256,
    string OutputExecutableSha256,
    int LevelId,
    int ContinuousLevelIndex,
    int ExecutableLba,
    int ExecutableByteLength,
    long NamePointerTableFileOffset,
    int NameTableIndex,
    long TargetNamePointerFileOffset,
    long DonorNamePointerFileOffset,
    string PlaceholderName,
    string AuthoredDisplayName,
    string TargetNamePointerBeforeHex,
    string TargetNamePointerAfterHex,
    IReadOnlyList<UnusedLevel65BootstrapPatch> LogicalPatches,
    IReadOnlyList<string> RuntimeChecklist,
    bool RequiresDuckStationRuntimeProof);

public sealed record UnusedLevel65DisplayNameCandidateResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    UnusedLevel65DisplayNameCandidatePlan Plan,
    long ChangedLogicalExecutableBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int ChangedRawSectorCount,
    bool ExactLogicalDiffBoundaryVerified,
    bool ExactPhysicalSectorBoundaryVerified,
    bool NamePointerReadbackVerified,
    bool DisplayNameReadbackVerified,
    bool ProtectedScopesPreserved,
    bool BaseCandidatePreserved,
    bool AtomicRenameCompleted);

/// <summary>
/// Disposable identity-only follow-up to the runtime-proven physical ID65 clone.
/// It aliases only level-name table slot 35 from the retail placeholder A string
/// to the already-present Town Square string. No WAD, totals, music, portal,
/// saving, Return Home, content, editor, or normal Create BIN path is changed.
/// </summary>
public static class UnusedLevel65DisplayNameCandidateExporter
{
    public const string ProfileId =
        "unused-level-65-town-square-display-name-clean-usa-disposable-v4";
    public const string ExpectedOutputImageSha256 =
        "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";

    private const string BaseImageSha256 =
        UnusedLevel65PhysicalCloneCandidateExporter.ExpectedOutputImageSha256;
    private const string BaseExecutableSha256 =
        "0f6996babd64e815529df648fd7136f478c42f8187567d71439462df12b931ec";
    private const string OutputExecutableSha256 =
        "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";

    private const int PsxExeHeaderSize = 0x800;
    private const int PsxExeLoadAddressOffset = 0x18;
    private const uint ExpectedLoadAddress = 0x80010000;
    private const int RawSectorBytes = 2352;
    private const int ExecutableLba = 55382;
    private const int ExecutableByteLength = 0x66000;
    private const int LevelId = 65;
    private const int ContinuousLevelIndex = 35;
    private const long NamePointerTableFileOffset = 0x5FFF0;
    private const int NameTableIndex = ContinuousLevelIndex;
    private const long TargetNamePointerFileOffset =
        NamePointerTableFileOffset + (NameTableIndex * sizeof(uint));
    private const int TownSquareNameTableIndex = 3;
    private const long DonorNamePointerFileOffset =
        NamePointerTableFileOffset + (TownSquareNameTableIndex * sizeof(uint));
    private const int PlaceholderStringFileOffset = 0x65D64;
    private const int TownSquareStringFileOffset = 0x9E4;
    private const int ExpectedChangedLogicalBytes = 3;
    private const int ExpectedChangedPhysicalBytes = 53;
    private const int ExpectedAffectedRawSector = 55574;
    private const byte XaDataSubmode = 0x08;

    private static readonly byte[] PlaceholderNamePointer = Convert.FromHexString("64550780");
    private static readonly byte[] TownSquareNamePointer = Convert.FromHexString("E4010180");
    private static readonly byte[] PlaceholderNameBytes = Encoding.ASCII.GetBytes("A\0");
    private static readonly byte[] TownSquareNameBytes = Encoding.ASCII.GetBytes("TOWN SQUARE\0");

    public static async Task<UnusedLevel65DisplayNameCandidatePlan> BuildPlanAsync(
        UnusedLevel65DisplayNameCandidateRequest request,
        CancellationToken cancellationToken = default) =>
        (await PrepareAsync(request, cancellationToken)).Plan;

    public static async Task<UnusedLevel65DisplayNameCandidateResult> ExportAsync(
        UnusedLevel65DisplayNameCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        Prepared prepared = await PrepareAsync(request, cancellationToken);
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
            throw new InvalidOperationException("The ID65 display-name BIN and CUE must share one directory.");

        Directory.CreateDirectory(Path.GetDirectoryName(outputImage)!);
        string operationId = Guid.NewGuid().ToString("N");
        string outputDirectory = Path.GetDirectoryName(outputImage)!;
        string temporaryImage = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(outputImage)}.{operationId}.tmp");
        string temporaryCue = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(outputCue)}.{operationId}.tmp");
        string backupImage = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(outputImage)}.{operationId}.bak");
        string backupCue = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(outputCue)}.{operationId}.bak");
        bool imageBackedUp = false;
        bool cueBackedUp = false;
        bool imagePublished = false;
        bool cuePublished = false;
        try
        {
            await DiscImageWorkingCopy.StageAsync(
                baseImage,
                temporaryImage,
                consumeDisposableSource: false,
                cancellationToken);
            DiscLayout layout = DiscImage.DetectLayout(temporaryImage);
            int rebuiltRawSectors;
            await using (FileStream output = new(
                             temporaryImage,
                             FileMode.Open,
                             FileAccess.ReadWrite,
                             FileShare.None))
            {
                RequireSlice(
                    DiscImage.ReadFileBytes(
                        output,
                        layout,
                        ExecutableLba,
                        TargetNamePointerFileOffset,
                        PlaceholderNamePointer.Length),
                    0,
                    PlaceholderNamePointer,
                    "staged ID65 placeholder name pointer");
                DiscImage.WriteFileBytes(
                    output,
                    layout,
                    ExecutableLba,
                    TargetNamePointerFileOffset,
                    TownSquareNamePointer);
                rebuiltRawSectors = RawMode2Form1SectorIntegrity.RebuildFileRanges(
                    output,
                    layout,
                    ExecutableLba,
                    [(TargetNamePointerFileOffset, TownSquareNamePointer.Length)]);
                int verifiedRawSectors = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
                    output,
                    layout,
                    [(ExpectedAffectedRawSector, 1)]);
                RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(
                    output,
                    layout,
                    ExpectedAffectedRawSector,
                    XaDataSubmode);
                if (rebuiltRawSectors != 1 || verifiedRawSectors != 1)
                    throw new InvalidDataException("The ID65 display-name patch did not rebuild exactly one raw sector.");
                output.Flush(flushToDisk: true);
            }

            Readback readback = VerifyReadback(
                baseImage,
                temporaryImage,
                prepared,
                cancellationToken);
            string outputImageSha256 = await HashFileAsync(temporaryImage, cancellationToken);
            if (!string.Equals(
                    outputImageSha256,
                    ExpectedOutputImageSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"ID65 display-name candidate SHA-256 is {outputImageSha256}, expected {ExpectedOutputImageSha256}.");
            }

            string cueText = DiscImage.BuildCueText(baseCue, Path.GetFileName(outputImage));
            await File.WriteAllTextAsync(temporaryCue, cueText, Encoding.ASCII, cancellationToken);
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
            NativeLevelReplacementBaselineExporter.ValidateCue(
                outputCue,
                outputImage,
                "MODE2/2352");

            string baseHashAfter = await HashFileAsync(baseImage, cancellationToken);
            if (!string.Equals(baseHashAfter, BaseImageSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The runtime-proven physical ID65 base changed during display-name export.");

            TryDelete(backupImage);
            imageBackedUp = false;
            TryDelete(backupCue);
            cueBackedUp = false;
            return new UnusedLevel65DisplayNameCandidateResult(
                outputImage,
                outputCue,
                outputImageSha256,
                prepared.Plan,
                readback.ChangedLogicalExecutableBytes,
                readback.ChangedPhysicalImageBytes,
                rebuiltRawSectors,
                readback.ChangedRawSectorCount,
                ExactLogicalDiffBoundaryVerified: true,
                ExactPhysicalSectorBoundaryVerified: true,
                NamePointerReadbackVerified: true,
                DisplayNameReadbackVerified: true,
                ProtectedScopesPreserved: true,
                BaseCandidatePreserved: true,
                AtomicRenameCompleted: true);
        }
        catch (Exception exportFailure)
        {
            if (cuePublished)
                TryDelete(outputCue);
            if (imagePublished)
                TryDelete(outputImage);
            List<Exception> recoveryFailures = [];
            Exception? imageRecovery = NativeLevelReplacementBaselineExporter.TryRestoreBackup(
                "BIN",
                backupImage,
                outputImage,
                ref imageBackedUp);
            Exception? cueRecovery = NativeLevelReplacementBaselineExporter.TryRestoreBackup(
                "CUE",
                backupCue,
                outputCue,
                ref cueBackedUp);
            if (imageRecovery != null)
                recoveryFailures.Add(imageRecovery);
            if (cueRecovery != null)
                recoveryFailures.Add(cueRecovery);
            if (recoveryFailures.Count > 0)
            {
                throw new IOException(
                    "ID65 display-name export failed and prior output recovery was incomplete.",
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

    private static async Task<Prepared> PrepareAsync(
        UnusedLevel65DisplayNameCandidateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string baseImage = RequireExistingFile(request.BaseImagePath, "runtime-proven physical ID65 base BIN");
        string baseCue = RequireExistingFile(request.BaseCuePath, "runtime-proven physical ID65 base CUE");
        NativeLevelReplacementBaselineExporter.ValidateCue(baseCue, baseImage, "MODE2/2352");
        string baseImageSha256 = await HashFileAsync(baseImage, cancellationToken);
        if (!string.Equals(baseImageSha256, BaseImageSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"The display-name experiment requires exact runtime-proven base BIN {BaseImageSha256}, not {baseImageSha256}.");
        }

        DiscLayout layout = DiscImage.DetectLayout(baseImage);
        if (layout.SectorSize != RawSectorBytes || layout.UserOffset != 24)
            throw new InvalidDataException("The ID65 display-name experiment requires the checked MODE2/2352 layout.");
        await using FileStream input = new(baseImage, FileMode.Open, FileAccess.Read, FileShare.Read);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(input, layout, IsExecutableName);
        if (executable.Lba != ExecutableLba || executable.Size != ExecutableByteLength)
            throw new InvalidDataException("The runtime-proven relocated SCUS extent changed.");
        byte[] executableBytes = DiscImage.ReadFileBytes(
            input,
            layout,
            executable.Lba,
            0,
            executable.Size);
        RequireHash(executableBytes, BaseExecutableSha256, "runtime-proven physical ID65 executable");
        RequireSlice(
            executableBytes,
            checked((int)TargetNamePointerFileOffset),
            PlaceholderNamePointer,
            "ID65 placeholder name pointer");
        RequireSlice(
            executableBytes,
            checked((int)DonorNamePointerFileOffset),
            TownSquareNamePointer,
            "retail Town Square name pointer");
        RequireSlice(
            executableBytes,
            PlaceholderStringFileOffset,
            PlaceholderNameBytes,
            "placeholder A string");
        RequireSlice(
            executableBytes,
            TownSquareStringFileOffset,
            TownSquareNameBytes,
            "retail TOWN SQUARE string");
        RequirePointerTarget(
            executableBytes,
            TargetNamePointerFileOffset,
            PlaceholderStringFileOffset,
            PlaceholderNameBytes,
            "ID65 placeholder display name");
        RequirePointerTarget(
            executableBytes,
            DonorNamePointerFileOffset,
            TownSquareStringFileOffset,
            TownSquareNameBytes,
            "retail Town Square display name");
        if (CountOccurrences(executableBytes, PlaceholderNamePointer) != 1)
            throw new InvalidDataException("The placeholder A name pointer is no longer uniquely owned by ID65 slot 35.");

        byte[] expectedOutputExecutable = executableBytes.ToArray();
        TownSquareNamePointer.CopyTo(
            expectedOutputExecutable,
            checked((int)TargetNamePointerFileOffset));
        RequireHash(expectedOutputExecutable, OutputExecutableSha256, "expected ID65 display-name executable");

        IReadOnlyList<UnusedLevel65BootstrapPatch> patches =
        [
            new(
                executable.Name,
                "alias-level-65-display-name-to-town-square",
                TargetNamePointerFileOffset,
                TownSquareNamePointer.Length,
                ToHex(PlaceholderNamePointer),
                ToHex(TownSquareNamePointer))
        ];
        UnusedLevel65DisplayNameCandidatePlan plan = new(
            DateTimeOffset.UtcNow,
            ProfileId,
            UnusedLevel65PhysicalCloneCandidateExporter.ProfileId,
            baseImageSha256,
            BaseExecutableSha256,
            OutputExecutableSha256,
            LevelId,
            ContinuousLevelIndex,
            ExecutableLba,
            ExecutableByteLength,
            NamePointerTableFileOffset,
            NameTableIndex,
            TargetNamePointerFileOffset,
            DonorNamePointerFileOffset,
            "A",
            "TOWN SQUARE",
            ToHex(PlaceholderNamePointer),
            ToHex(TownSquareNamePointer),
            patches,
            RuntimeChecklist(),
            RequiresDuckStationRuntimeProof: true);
        return new Prepared(executable, executableBytes, expectedOutputExecutable, plan);
    }

    private static Readback VerifyReadback(
        string baseImagePath,
        string outputImagePath,
        Prepared prepared,
        CancellationToken cancellationToken)
    {
        DiscLayout baseLayout = DiscImage.DetectLayout(baseImagePath);
        DiscLayout outputLayout = DiscImage.DetectLayout(outputImagePath);
        using FileStream baseline = File.OpenRead(baseImagePath);
        using FileStream output = File.OpenRead(outputImagePath);
        if (baseLayout != outputLayout || baseline.Length != output.Length)
            throw new InvalidDataException("The ID65 display-name candidate changed the disc layout or raw length.");

        DiscFileRecord baseExecutable = DiscImage.FindRootFileRecord(baseline, baseLayout, IsExecutableName);
        DiscFileRecord outputExecutable = DiscImage.FindRootFileRecord(output, outputLayout, IsExecutableName);
        if (baseExecutable != prepared.Executable || outputExecutable != prepared.Executable)
            throw new InvalidDataException("The ID65 display-name candidate moved or resized the relocated SCUS extent.");

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
        VerifyEqual(
            prepared.BaseExecutableBytes,
            baseExecutableBytes,
            "runtime-proven physical ID65 executable during readback");
        VerifyEqual(
            prepared.ExpectedOutputExecutableBytes,
            outputExecutableBytes,
            "ID65 display-name executable readback");
        RequireHash(outputExecutableBytes, OutputExecutableSha256, "ID65 display-name executable readback");
        RequireSlice(
            outputExecutableBytes,
            checked((int)TargetNamePointerFileOffset),
            TownSquareNamePointer,
            "authored ID65 display-name pointer readback");
        RequireSlice(
            outputExecutableBytes,
            checked((int)DonorNamePointerFileOffset),
            TownSquareNamePointer,
            "preserved retail Town Square name pointer");
        RequireSlice(
            outputExecutableBytes,
            PlaceholderStringFileOffset,
            PlaceholderNameBytes,
            "preserved placeholder A string");
        RequireSlice(
            outputExecutableBytes,
            TownSquareStringFileOffset,
            TownSquareNameBytes,
            "preserved retail TOWN SQUARE string");
        RequirePointerTarget(
            outputExecutableBytes,
            TargetNamePointerFileOffset,
            TownSquareStringFileOffset,
            TownSquareNameBytes,
            "authored ID65 Town Square display name readback");
        RequirePointerTarget(
            outputExecutableBytes,
            DonorNamePointerFileOffset,
            TownSquareStringFileOffset,
            TownSquareNameBytes,
            "preserved retail Town Square display name readback");

        long changedLogical = 0;
        long outsideLogical = 0;
        for (int index = 0; index < baseExecutableBytes.Length; index++)
        {
            if (baseExecutableBytes[index] == outputExecutableBytes[index])
                continue;
            changedLogical++;
            if (index < TargetNamePointerFileOffset ||
                index >= TargetNamePointerFileOffset + TownSquareNamePointer.Length)
            {
                outsideLogical++;
            }
        }
        if (changedLogical != ExpectedChangedLogicalBytes || outsideLogical != 0)
        {
            throw new InvalidDataException(
                $"ID65 display-name logical diff failed: changed={changedLogical}, outside={outsideLogical}; expected {ExpectedChangedLogicalBytes}/0.");
        }

        PhysicalDiff physical = ComparePhysicalImages(
            baseline,
            output,
            ExpectedAffectedRawSector,
            cancellationToken);
        if (physical.ChangedBytes != ExpectedChangedPhysicalBytes ||
            physical.OutsideAllowedSectorBytes != 0 ||
            physical.ChangedRawSectorCount != 1)
        {
            throw new InvalidDataException(
                $"ID65 display-name physical diff failed: changed={physical.ChangedBytes}, " +
                $"outside={physical.OutsideAllowedSectorBytes}, sectors={physical.ChangedRawSectorCount}; " +
                $"expected {ExpectedChangedPhysicalBytes}/0/1 at LBA {ExpectedAffectedRawSector}.");
        }
        return new Readback(changedLogical, physical.ChangedBytes, physical.ChangedRawSectorCount);
    }

    private static PhysicalDiff ComparePhysicalImages(
        FileStream baseline,
        FileStream output,
        int allowedRawSector,
        CancellationToken cancellationToken)
    {
        if (baseline.Length != output.Length || baseline.Length % RawSectorBytes != 0)
            throw new InvalidDataException("The ID65 display-name raw diff requires equal complete MODE2/2352 images.");
        const int chunkSize = 1 << 20;
        byte[] before = new byte[chunkSize];
        byte[] after = new byte[chunkSize];
        long allowedStart = checked((long)allowedRawSector * RawSectorBytes);
        long allowedEnd = allowedStart + RawSectorBytes;
        long changedBytes = 0;
        long outside = 0;
        HashSet<long> changedSectors = [];
        baseline.Position = 0;
        output.Position = 0;
        for (long offset = 0; offset < baseline.Length; offset += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int length = checked((int)Math.Min(chunkSize, baseline.Length - offset));
            baseline.ReadExactly(before.AsSpan(0, length));
            output.ReadExactly(after.AsSpan(0, length));
            for (int index = 0; index < length; index++)
            {
                if (before[index] == after[index])
                    continue;
                changedBytes++;
                long physicalOffset = offset + index;
                changedSectors.Add(physicalOffset / RawSectorBytes);
                if (physicalOffset < allowedStart || physicalOffset >= allowedEnd)
                    outside++;
            }
        }
        if (!changedSectors.SetEquals([allowedRawSector]))
            throw new InvalidDataException("The ID65 display-name candidate changed an unexpected raw sector.");
        return new PhysicalDiff(changedBytes, outside, changedSectors.Count);
    }

    private static IReadOnlyList<string> RuntimeChecklist() =>
    [
        "Disable every DuckStation cheat and memory-card insertion completely, then cold boot the candidate without resuming a save state.",
        $"From controllable gameplay, press Select to open Inventory; enter {TestLevelWarpPatch.ActivationSequence}; then press Left, then Down. Confirm the ID65 entry now reads TOWN SQUARE instead of A and loads normally.",
        "Move through several sectors, open pause/Inventory, and confirm geometry, textures, collision, camera, initial music, enemies, and the TOWN SQUARE display name remain stable.",
        $"Collect exactly one loose gem, reset DuckStation, cold boot, and reach controllable gameplay. Press Select to open Inventory; enter {TestLevelWarpPatch.ActivationSequence}; then press Left, then Down to re-enter ID65. Before recollecting anything, confirm Inventory reads 0/0, confirm the collected red gem is physically present again because the no-card reset discarded the session, and confirm the TOWN SQUARE identity still appears.",
        $"Reset and reach controllable gameplay. Press Select to open Inventory; enter {TestLevelWarpPatch.ActivationSequence}; then press Cross, then Triangle. Verify retail Town Square and confirm it remains independently loadable.",
        $"Reset and reach controllable gameplay. Press Select to open Inventory; enter {TestLevelWarpPatch.ActivationSequence}; then press Left, then Right. Verify Gnasty's Loot.",
        $"Reset and reach controllable gameplay. Press Select to open Inventory; enter {TestLevelWarpPatch.ActivationSequence}; then press Cross, then Down. Verify Sunny Flight and check flight controls/timer only.",
        $"For the Inventory page control, cold boot without a resumed save state and do not reset between entries. Load Gnasty's World ID60 with Select; {TestLevelWarpPatch.ActivationSequence}; Left, then Circle. Load Dream Weavers ID50 with Select; {TestLevelWarpPatch.ActivationSequence}; Down, then Circle. Load ID65 with Select; {TestLevelWarpPatch.ActivationSequence}; Left, then Down. Open Inventory, use D-pad Left, wait for the Dream Weavers page transition to finish, then use D-pad Right. Confirm Left reaches Dream Weavers and Right returns to the now-visited Gnasty page.",
        "Do not rescue dragons, touch the egg thief, attack or kill enemies, open or break chests, die, insert a memory card, save, select Exit Level or Quit Game, or use Return Home. Totals, alternate music, portal routing, save ownership, and content edits remain out of scope."
    ];

    private static int CountOccurrences(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        int count = 0;
        for (int index = 0; index <= haystack.Length - needle.Length; index++)
        {
            if (haystack.Slice(index, needle.Length).SequenceEqual(needle))
                count++;
        }
        return count;
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
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{label} SHA-256 is {actual}, expected {expected}.");
    }

    private static void RequireSlice(
        byte[] bytes,
        int offset,
        ReadOnlySpan<byte> expected,
        string label)
    {
        if (offset < 0 || offset + expected.Length > bytes.Length ||
            !bytes.AsSpan(offset, expected.Length).SequenceEqual(expected))
        {
            throw new InvalidDataException($"The guarded {label} changed.");
        }
    }

    private static void VerifyEqual(byte[] expected, byte[] actual, string label)
    {
        if (!expected.AsSpan().SequenceEqual(actual))
            throw new InvalidDataException($"The {label} failed byte-for-byte verification.");
    }

    private static void RequirePointerTarget(
        byte[] executable,
        long pointerFileOffset,
        int expectedStringFileOffset,
        ReadOnlySpan<byte> expectedString,
        string label)
    {
        if (executable.Length < PsxExeHeaderSize ||
            !executable.AsSpan(0, 8).SequenceEqual("PS-X EXE"u8))
        {
            throw new InvalidDataException("The guarded SCUS no longer has a valid PS-X EXE header.");
        }
        uint loadAddress = BinaryPrimitives.ReadUInt32LittleEndian(
            executable.AsSpan(PsxExeLoadAddressOffset, sizeof(uint)));
        if (loadAddress != ExpectedLoadAddress)
            throw new InvalidDataException($"The guarded SCUS load address is 0x{loadAddress:X8}, expected 0x{ExpectedLoadAddress:X8}.");
        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(
            executable.AsSpan(checked((int)pointerFileOffset), sizeof(uint)));
        if (pointer < loadAddress)
            throw new InvalidDataException($"The {label} pointer 0x{pointer:X8} precedes the executable load address.");
        int resolvedFileOffset = checked((int)(pointer - loadAddress) + PsxExeHeaderSize);
        if (resolvedFileOffset != expectedStringFileOffset)
        {
            throw new InvalidDataException(
                $"The {label} pointer resolves to SCUS 0x{resolvedFileOffset:X}, expected 0x{expectedStringFileOffset:X}.");
        }
        RequireSlice(executable, resolvedFileOffset, expectedString, label);
    }

    private static string ToHex(IEnumerable<byte> bytes) =>
        string.Join(' ', bytes.Select(value => $"{value:X2}"));

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static async Task<string> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(
            await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static bool IsExecutableName(string name) =>
        name.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase);

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

    private sealed record Prepared(
        DiscFileRecord Executable,
        byte[] BaseExecutableBytes,
        byte[] ExpectedOutputExecutableBytes,
        UnusedLevel65DisplayNameCandidatePlan Plan);

    private sealed record Readback(
        long ChangedLogicalExecutableBytes,
        long ChangedPhysicalImageBytes,
        int ChangedRawSectorCount);

    private sealed record PhysicalDiff(
        long ChangedBytes,
        long OutsideAllowedSectorBytes,
        int ChangedRawSectorCount);
}

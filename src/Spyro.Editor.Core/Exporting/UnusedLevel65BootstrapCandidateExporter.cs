using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

public sealed record UnusedLevel65BootstrapCandidateRequest(
    string SourceImagePath,
    string SourceCuePath,
    string OutputImagePath,
    string OutputCuePath);

public sealed record UnusedLevel65BootstrapPatch(
    string File,
    string Kind,
    long LogicalOffset,
    int ByteLength,
    string BeforeHex,
    string AfterHex);

public sealed record UnusedLevel65BootstrapCandidatePlan(
    DateTimeOffset GeneratedAtUtc,
    string ProfileId,
    string SourceImageSha256,
    int LevelId,
    int OverlayDirectoryIndex,
    int DataDirectoryIndex,
    long AliasedOverlayWadOffset,
    int AliasedOverlayByteLength,
    long AliasedDataWadOffset,
    int AliasedDataByteLength,
    string DonorOverlaySha256,
    string DonorDataSha256,
    IReadOnlyList<UnusedLevel65BootstrapPatch> Patches,
    IReadOnlyList<string> RuntimeChecklist,
    bool RequiresDuckStationRuntimeProof);

public sealed record UnusedLevel65BootstrapCandidateResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    UnusedLevel65BootstrapCandidatePlan Plan,
    long ChangedWadLogicalBytes,
    long ChangedExecutableLogicalBytes,
    int RebuiltRawSectorCount,
    bool ExactLogicalDiffBoundaryVerified,
    bool DonorPayloadsPreserved,
    bool RetailRootDirectoryPreserved,
    bool SourceImagePreserved);

/// <summary>
/// Disposable V5 discriminator for Spyro 1's unused sixth Gnasty-world slot.
/// It gives level ID 65 its already-reserved WAD directory row, aliases that row
/// to Town Square's byte-identical retail pair, registers Town Square's checked
/// callback dispatcher for ID 65, and widens only the hidden retail level-warp
/// range by one value. No retail level is replaced and no normal editor build
/// path consumes this exporter.
/// </summary>
public static class UnusedLevel65BootstrapCandidateExporter
{
    public const string ProfileId =
        "unused-level-65-town-square-alias-clean-usa-disposable-v1";
    public const string ExpectedOutputImageSha256 =
        "33534481a43a7655b570194d9f78292467a20ab480f46e2ed5528a59fe39b2ea";
    public const int LevelId = 65;
    public const string EntryInstructions =
        "Open Inventory; enter R1, R2, L1, L2, R1, L1, R2, L2; then press Left, then Down.";

    private const int SectorBytes = 0x800;
    private const int WadLba = 37;
    private const int WadSize = 0x6927000;
    private const int WadHeaderByteLength = SectorBytes;
    private const int ExecutableLba = 53875;
    private const int ExecutableByteLength = 0x66000;
    private const int NextFileLba = 60000;

    private const int DonorOverlayEntryIndex = 15;
    private const int DonorDataEntryIndex = 16;
    private const int TargetOverlayEntryIndex = 79;
    private const int TargetDataEntryIndex = 80;
    private const long DonorOverlayWadOffset = 0x118E800;
    private const int DonorOverlayByteLength = 0xF800;
    private const long DonorDataWadOffset = 0x119E000;
    private const int DonorDataByteLength = 0x2E2000;

    private const long Level65DispatchPointerFileOffset = 0x1CA8;
    private const uint DefaultDispatchPointer = 0x8005B6E0;
    private const uint TownSquareDispatchPointer = 0x8005A898;
    private const long WarpUpperBoundFileOffset = 0x1DF44;
    private const uint WarpUpperBoundBefore = 0x2C420037;
    private const uint WarpUpperBoundAfter = 0x2C420038;
    private const long WarpActivationFileOffset = 0x1E034;
    private static readonly byte[] WarpActivationBefore =
        [0x37, 0xB6, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] WarpActivationAfter =
        [0x1C, 0x06, 0x84, 0xAF, 0x88, 0x06, 0x80, 0xAF];

    private const string CleanUsaImageSha256 =
        "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const string RetailWadHeaderSha256 =
        "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd";
    private const string RetailExecutableSha256 =
        "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private const string TownSquareOverlaySha256 =
        "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5";
    private const string TownSquareDataSha256 =
        "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";

    public static async Task<UnusedLevel65BootstrapCandidateResult> ExportAsync(
        UnusedLevel65BootstrapCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string sourceImage = Path.GetFullPath(request.SourceImagePath);
        string sourceCue = Path.GetFullPath(request.SourceCuePath);
        string outputImage = Path.GetFullPath(request.OutputImagePath);
        string outputCue = Path.GetFullPath(request.OutputCuePath);
        NativeLevelReplacementBaselineExporter.EnsureDistinctRoles(
            sourceImage,
            sourceCue,
            outputImage,
            outputCue);
        NativeLevelReplacementBaselineExporter.ValidateCue(
            sourceCue,
            sourceImage,
            "MODE2/2352");
        if (!PathEquals(Path.GetDirectoryName(outputImage), Path.GetDirectoryName(outputCue)))
            throw new InvalidOperationException("The level-65 candidate BIN and CUE must share one directory.");

        string sourceImageSha256 = await HashFileAsync(sourceImage, cancellationToken);
        if (!string.Equals(sourceImageSha256, CleanUsaImageSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"Level-65 bootstrap accepts only the exact clean USA BIN ({CleanUsaImageSha256}); selected SHA-256 was {sourceImageSha256}.");

        Prepared prepared = Prepare(sourceImage);
        Directory.CreateDirectory(Path.GetDirectoryName(outputImage)!);
        string operationId = Guid.NewGuid().ToString("N");
        string temporaryImage = Path.Combine(
            Path.GetDirectoryName(outputImage)!,
            $".{Path.GetFileName(outputImage)}.{operationId}.tmp");
        string temporaryCue = Path.Combine(
            Path.GetDirectoryName(outputCue)!,
            $".{Path.GetFileName(outputCue)}.{operationId}.tmp");
        string backupImage = Path.Combine(
            Path.GetDirectoryName(outputImage)!,
            $".{Path.GetFileName(outputImage)}.{operationId}.bak");
        string backupCue = Path.Combine(
            Path.GetDirectoryName(outputCue)!,
            $".{Path.GetFileName(outputCue)}.{operationId}.bak");
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
            DiscLayout layout = DiscImage.DetectLayout(temporaryImage);
            int rebuiltRawSectors;
            await using (FileStream output = new(
                temporaryImage,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None))
            {
                DiscImage.WriteFileBytes(
                    output,
                    layout,
                    WadLba,
                    TargetOverlayEntryIndex * 8L,
                    prepared.TargetDirectoryRow);
                DiscImage.WriteFileBytes(
                    output,
                    layout,
                    ExecutableLba,
                    Level65DispatchPointerFileOffset,
                    UInt32Bytes(TownSquareDispatchPointer));
                DiscImage.WriteFileBytes(
                    output,
                    layout,
                    ExecutableLba,
                    WarpUpperBoundFileOffset,
                    UInt32Bytes(WarpUpperBoundAfter));
                DiscImage.WriteFileBytes(
                    output,
                    layout,
                    ExecutableLba,
                    WarpActivationFileOffset,
                    WarpActivationAfter);

                rebuiltRawSectors =
                    RawMode2Form1SectorIntegrity.RebuildFileRanges(
                        output,
                        layout,
                        WadLba,
                        [(TargetOverlayEntryIndex * 8L, 16)]) +
                    RawMode2Form1SectorIntegrity.RebuildFileRanges(
                        output,
                        layout,
                        ExecutableLba,
                        [
                            (Level65DispatchPointerFileOffset, 4),
                            (WarpUpperBoundFileOffset, 4),
                            (WarpActivationFileOffset, WarpActivationAfter.Length)
                        ]);
                output.Flush(flushToDisk: true);
            }

            Readback readback = VerifyReadback(sourceImage, temporaryImage, prepared, cancellationToken);
            string cueText = DiscImage.BuildCueText(sourceCue, Path.GetFileName(outputImage));
            await File.WriteAllTextAsync(temporaryCue, cueText, Encoding.ASCII, cancellationToken);
            string outputImageSha256 = await HashFileAsync(temporaryImage, cancellationToken);
            if (!string.Equals(
                    outputImageSha256,
                    ExpectedOutputImageSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Level-65 candidate SHA-256 is {outputImageSha256}, expected exact static output {ExpectedOutputImageSha256}.");
            }

            bool sourcePreserved = string.Equals(
                await HashFileAsync(sourceImage, cancellationToken),
                CleanUsaImageSha256,
                StringComparison.OrdinalIgnoreCase);
            if (!sourcePreserved)
                throw new InvalidDataException("The clean retail source changed during level-65 export.");

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
            TryDelete(backupImage);
            imageBackedUp = false;
            TryDelete(backupCue);
            cueBackedUp = false;

            UnusedLevel65BootstrapCandidatePlan plan = new(
                DateTimeOffset.UtcNow,
                ProfileId,
                sourceImageSha256,
                LevelId,
                TargetOverlayEntryIndex,
                TargetDataEntryIndex,
                DonorOverlayWadOffset,
                DonorOverlayByteLength,
                DonorDataWadOffset,
                DonorDataByteLength,
                TownSquareOverlaySha256,
                TownSquareDataSha256,
                prepared.Patches,
                RuntimeChecklist(),
                RequiresDuckStationRuntimeProof: true);
            return new UnusedLevel65BootstrapCandidateResult(
                outputImage,
                outputCue,
                outputImageSha256,
                plan,
                readback.ChangedWadBytes,
                readback.ChangedExecutableBytes,
                rebuiltRawSectors,
                ExactLogicalDiffBoundaryVerified: true,
                DonorPayloadsPreserved: true,
                RetailRootDirectoryPreserved: true,
                SourceImagePreserved: true);
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
                    "The level-65 candidate failed and one or more prior outputs could not be restored.",
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

    private static Prepared Prepare(string sourceImage)
    {
        DiscLayout layout = DiscImage.DetectLayout(sourceImage);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("Level-65 bootstrap requires the raw MODE2/2352 USA image.");
        using FileStream source = File.OpenRead(sourceImage);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(source, layout, IsWadName);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(source, layout, IsExecutableName);
        DiscFileRecord nextFile = DiscImage.FindRootFileRecord(source, layout, name =>
            name.StartsWith("PETEXA0", StringComparison.OrdinalIgnoreCase));
        if (wad.Lba != WadLba || wad.Size != WadSize ||
            executable.Lba != ExecutableLba || executable.Size != ExecutableByteLength ||
            nextFile.Lba != NextFileLba)
        {
            throw new InvalidDataException("The clean-USA WAD, executable, or next-file ISO extent changed.");
        }

        byte[] wadHeader = DiscImage.ReadFileBytes(
            source,
            layout,
            wad.Lba,
            0,
            WadHeaderByteLength);
        RequireHash(wadHeader, RetailWadHeaderSha256, "retail WAD header");
        byte[] blankTargetRow = wadHeader.AsSpan(TargetOverlayEntryIndex * 8, 16).ToArray();
        if (blankTargetRow.Any(value => value != 0))
            throw new InvalidDataException("Reserved level-65 WAD directory entries 79/80 are no longer blank.");
        RequireDirectoryEntry(
            wadHeader,
            DonorOverlayEntryIndex,
            DonorOverlayWadOffset,
            DonorOverlayByteLength,
            "Town Square overlay");
        RequireDirectoryEntry(
            wadHeader,
            DonorDataEntryIndex,
            DonorDataWadOffset,
            DonorDataByteLength,
            "Town Square data");
        RequireHash(
            DiscImage.ReadFileBytes(
                source,
                layout,
                wad.Lba,
                DonorOverlayWadOffset,
                DonorOverlayByteLength),
            TownSquareOverlaySha256,
            "Town Square overlay");
        RequireHash(
            DiscImage.ReadFileBytes(
                source,
                layout,
                wad.Lba,
                DonorDataWadOffset,
                DonorDataByteLength),
            TownSquareDataSha256,
            "Town Square data");

        byte[] executableBytes = DiscImage.ReadFileBytes(
            source,
            layout,
            executable.Lba,
            0,
            executable.Size);
        RequireHash(executableBytes, RetailExecutableSha256, "retail executable");
        RequireUInt32(
            executableBytes,
            Level65DispatchPointerFileOffset,
            DefaultDispatchPointer,
            "level-65 default dispatch pointer");
        RequireUInt32(
            executableBytes,
            WarpUpperBoundFileOffset,
            WarpUpperBoundBefore,
            "retail warp upper bound");
        RequireSlice(
            executableBytes,
            WarpActivationFileOffset,
            WarpActivationBefore,
            "retail warp activation");

        byte[] targetRow = new byte[16];
        WriteDirectoryEntry(
            targetRow,
            0,
            DonorOverlayWadOffset,
            DonorOverlayByteLength);
        WriteDirectoryEntry(
            targetRow,
            8,
            DonorDataWadOffset,
            DonorDataByteLength);
        IReadOnlyList<UnusedLevel65BootstrapPatch> patches =
        [
            Patch(
                "WAD.WAD",
                "reserved-level-65-overlay-and-data-directory-row",
                TargetOverlayEntryIndex * 8L,
                blankTargetRow,
                targetRow),
            Patch(
                executable.Name,
                "level-65-overlay-dispatch-pointer",
                Level65DispatchPointerFileOffset,
                UInt32Bytes(DefaultDispatchPointer),
                UInt32Bytes(TownSquareDispatchPointer)),
            Patch(
                executable.Name,
                "retail-level-warp-accept-id-65",
                WarpUpperBoundFileOffset,
                UInt32Bytes(WarpUpperBoundBefore),
                UInt32Bytes(WarpUpperBoundAfter)),
            Patch(
                executable.Name,
                "enable-hidden-retail-level-warp",
                WarpActivationFileOffset,
                WarpActivationBefore,
                WarpActivationAfter)
        ];
        return new Prepared(targetRow, patches);
    }

    private static Readback VerifyReadback(
        string sourceImage,
        string outputImage,
        Prepared prepared,
        CancellationToken cancellationToken)
    {
        DiscLayout sourceLayout = DiscImage.DetectLayout(sourceImage);
        DiscLayout outputLayout = DiscImage.DetectLayout(outputImage);
        using FileStream source = File.OpenRead(sourceImage);
        using FileStream output = File.OpenRead(outputImage);
        DiscFileRecord sourceWad = DiscImage.FindRootFileRecord(source, sourceLayout, IsWadName);
        DiscFileRecord outputWad = DiscImage.FindRootFileRecord(output, outputLayout, IsWadName);
        DiscFileRecord sourceExecutable = DiscImage.FindRootFileRecord(source, sourceLayout, IsExecutableName);
        DiscFileRecord outputExecutable = DiscImage.FindRootFileRecord(output, outputLayout, IsExecutableName);
        if (sourceLayout != outputLayout || sourceWad != outputWad || sourceExecutable != outputExecutable ||
            source.Length != output.Length)
        {
            throw new InvalidDataException(
                "The level-65 alias candidate changed the retail disc layout or file extents: " +
                $"layout {sourceLayout} -> {outputLayout}; WAD {sourceWad} -> {outputWad}; " +
                $"SCUS {sourceExecutable} -> {outputExecutable}; image bytes " +
                $"{source.Length} -> {output.Length}.");
        }
        VerifyBytesEqual(
            DiscImage.ReadFileBytes(
                source,
                sourceLayout,
                sourceLayout.RootExtent,
                0,
                sourceLayout.RootLength),
            DiscImage.ReadFileBytes(
                output,
                outputLayout,
                outputLayout.RootExtent,
                0,
                outputLayout.RootLength),
            "The level-65 alias candidate changed the ISO root directory.");

        VerifyBytesEqual(
            prepared.TargetDirectoryRow,
            DiscImage.ReadFileBytes(
                output,
                outputLayout,
                outputWad.Lba,
                TargetOverlayEntryIndex * 8L,
                16),
            "Level-65 WAD directory row failed readback.");
        RequireHash(
            DiscImage.ReadFileBytes(
                output,
                outputLayout,
                outputWad.Lba,
                DonorOverlayWadOffset,
                DonorOverlayByteLength),
            TownSquareOverlaySha256,
            "aliased Town Square overlay payload");
        RequireHash(
            DiscImage.ReadFileBytes(
                output,
                outputLayout,
                outputWad.Lba,
                DonorDataWadOffset,
                DonorDataByteLength),
            TownSquareDataSha256,
            "aliased Town Square data payload");
        RequireUInt32(
            DiscImage.ReadFileBytes(
                output,
                outputLayout,
                outputExecutable.Lba,
                0,
                outputExecutable.Size),
            Level65DispatchPointerFileOffset,
            TownSquareDispatchPointer,
            "level-65 Town Square dispatch pointer readback");
        VerifyBytesEqual(
            WarpActivationAfter,
            DiscImage.ReadFileBytes(
                output,
                outputLayout,
                outputExecutable.Lba,
                WarpActivationFileOffset,
                WarpActivationAfter.Length),
            "Warp activation failed readback.");
        byte[] outputExecutableBytes = DiscImage.ReadFileBytes(
            output,
            outputLayout,
            outputExecutable.Lba,
            0,
            outputExecutable.Size);
        RequireUInt32(
            outputExecutableBytes,
            WarpUpperBoundFileOffset,
            WarpUpperBoundAfter,
            "level-65 warp bound readback");

        (long changedWad, long outsideWad) = CompareLogicalFile(
            source,
            output,
            sourceLayout,
            sourceWad.Lba,
            sourceWad.Size,
            [new LogicalRange(TargetOverlayEntryIndex * 8L, 16)],
            cancellationToken);
        (long changedExecutable, long outsideExecutable) = CompareLogicalFile(
            source,
            output,
            sourceLayout,
            sourceExecutable.Lba,
            sourceExecutable.Size,
            [
                new LogicalRange(Level65DispatchPointerFileOffset, 4),
                new LogicalRange(WarpUpperBoundFileOffset, 4),
                new LogicalRange(WarpActivationFileOffset, WarpActivationAfter.Length)
            ],
            cancellationToken);
        if (changedWad == 0 || changedExecutable == 0 || outsideWad != 0 || outsideExecutable != 0)
        {
            throw new InvalidDataException(
                $"Level-65 diff boundary failed: WAD changed={changedWad}, outside={outsideWad}; " +
                $"SCUS changed={changedExecutable}, outside={outsideExecutable}.");
        }
        return new Readback(changedWad, changedExecutable);
    }

    private static IReadOnlyList<string> RuntimeChecklist() =>
    [
        "Disable memory-card insertion completely for this first identity/load probe. The retail serializer can record current level ID 65 and its slot-35 state; this candidate intentionally does not modify global save/load code.",
        "Cold boot the candidate and reach a normal controllable game state before opening Inventory.",
        EntryInstructions,
        "Expected: the game begins a normal level transition and loads Town Square from the separate level ID 65 path. The level name may still display the retail placeholder A; identity is deliberately not patched yet.",
        "Confirm geometry, textures, sky, the initial music track, camera, enemy animation, pause, and Inventory remain responsive during a short movement/sector-streaming check. Extended-session alternate music is unproven because its retail table has only 35 rows.",
        "Open and close pause/Inventory only; do not select Exit Level or Quit Game because those transitions are not proven for portal-to-exit 65.",
        "Do not attack or kill enemies, collect anything, rescue dragons, touch the egg thief, open or break chests, interact with gameplay objects, die/respawn, use a balloonist/save prompt, or save in this first loader/dispatch discriminator.",
        "Do not enter Town Square's Return Home portal in this first probe: it records portal-to-exit 65, and Gnasty's World has no matching portal-65 landing object yet.",
        "After the movement/streaming checks, reset DuckStation, boot again, and re-enter level 65 once through the Inventory sequence. Keep memory cards disabled and do not save from this static-only candidate.",
        "After that focused re-entry, reset and confirm the untouched retail Town Square and Gnasty's Loot still load normally."
    ];

    private static (long Changed, long Outside) CompareLogicalFile(
        FileStream source,
        FileStream output,
        DiscLayout layout,
        int fileLba,
        int byteLength,
        IReadOnlyList<LogicalRange> allowed,
        CancellationToken cancellationToken)
    {
        const int chunkSize = 1 << 20;
        long changed = 0;
        long outside = 0;
        for (long offset = 0; offset < byteLength; offset += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int length = (int)Math.Min(chunkSize, byteLength - offset);
            byte[] before = DiscImage.ReadFileBytes(source, layout, fileLba, offset, length);
            byte[] after = DiscImage.ReadFileBytes(output, layout, fileLba, offset, length);
            for (int index = 0; index < length; index++)
            {
                if (before[index] == after[index])
                    continue;
                changed++;
                long logicalOffset = offset + index;
                if (!allowed.Any(range => range.Contains(logicalOffset)))
                    outside++;
            }
        }
        return (changed, outside);
    }

    private static void RequireDirectoryEntry(
        byte[] header,
        int index,
        long expectedOffset,
        int expectedLength,
        string label)
    {
        long offset = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(index * 8, 4));
        int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(
            header.AsSpan((index * 8) + 4, 4)));
        if (offset != expectedOffset || length != expectedLength)
            throw new InvalidDataException($"The {label} WAD directory entry changed.");
    }

    private static void WriteDirectoryEntry(
        byte[] bytes,
        int offset,
        long wadOffset,
        int byteLength)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(
            bytes.AsSpan(offset, 4),
            checked((uint)wadOffset));
        BinaryPrimitives.WriteUInt32LittleEndian(
            bytes.AsSpan(offset + 4, 4),
            checked((uint)byteLength));
    }

    private static void RequireUInt32(byte[] bytes, long offset, uint expected, string label)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new InvalidDataException($"{label} is outside its guarded buffer.");
        uint actual = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan((int)offset, 4));
        if (actual != expected)
            throw new InvalidDataException($"{label} changed: expected 0x{expected:X8}, found 0x{actual:X8}.");
    }

    private static void RequireSlice(byte[] bytes, long offset, byte[] expected, string label)
    {
        if (offset < 0 || offset + expected.Length > bytes.Length ||
            !bytes.AsSpan((int)offset, expected.Length).SequenceEqual(expected))
            throw new InvalidDataException($"{label} preimage changed.");
    }

    private static void RequireHash(ReadOnlySpan<byte> bytes, string expected, string label)
    {
        string actual = Hash(bytes);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{label} SHA-256 is {actual}, expected {expected}.");
    }

    private static void VerifyBytesEqual(byte[] expected, byte[] actual, string message)
    {
        if (!expected.AsSpan().SequenceEqual(actual))
            throw new InvalidDataException(message);
    }

    private static UnusedLevel65BootstrapPatch Patch(
        string file,
        string kind,
        long offset,
        byte[] before,
        byte[] after) =>
        new(file, kind, offset, after.Length, ToHex(before), ToHex(after));

    private static byte[] UInt32Bytes(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
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

    private static bool IsWadName(string name) =>
        name.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase);

    private static bool IsExecutableName(string name) =>
        name.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase);

    private static bool PathEquals(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
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
        byte[] TargetDirectoryRow,
        IReadOnlyList<UnusedLevel65BootstrapPatch> Patches);

    private sealed record Readback(long ChangedWadBytes, long ChangedExecutableBytes);

    private sealed record LogicalRange(long Offset, long ByteLength)
    {
        public bool Contains(long value) => value >= Offset && value < Offset + ByteLength;
    }
}

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

public sealed record StoneHillTownSquareReplacementCandidateRequest(
    NativeLevelReplacementManifest Manifest,
    LevelCatalog Catalog,
    string SourceImagePath,
    string SourceCuePath,
    string OutputImagePath,
    string OutputCuePath);

public sealed record StoneHillTownSquareReplacementPatchSummary(
    string File,
    string Kind,
    long LogicalOffset,
    int ByteLength,
    string BeforeSha256,
    string AfterSha256);

public sealed record StoneHillTownSquareReplacementSafetyReport(
    string Status,
    IReadOnlyList<string> WritableScopes,
    IReadOnlyList<string> ProtectedScopes,
    IReadOnlyList<string> RuntimeChecks,
    bool RequiresDuckStationRuntimeProof);

public sealed record StoneHillTownSquareReplacementCandidatePlan(
    DateTimeOffset GeneratedAtUtc,
    string TargetLevelKey,
    string DonorLevelKey,
    NativeWadEntryPreimage TargetOverlayBefore,
    NativeWadEntryPreimage TargetDataBefore,
    NativeWadEntryPreimage DonorOverlay,
    NativeWadEntryPreimage DonorData,
    int OutputOverlayByteLength,
    int OutputDataByteLength,
    string OutputOverlaySha256,
    string OutputDataSha256,
    string OutputWadHeaderSha256,
    string OutputExecutableSha256,
    IReadOnlyList<NativeNestedSubfilePreimage> DonorSubfiles,
    IReadOnlyList<StoneHillTownSquareReplacementPatchSummary> Patches,
    StoneHillTownSquareReplacementSafetyReport Safety);

public sealed record StoneHillTownSquareReplacementCandidateResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    StoneHillTownSquareReplacementCandidatePlan Plan,
    long ChangedWadBytes,
    long ChangedExecutableBytes,
    int RebuiltRawSectorCount,
    bool ExactLogicalDiffBoundaryVerified,
    bool ArtisansPortalPreimagesVerified,
    bool DonorEntriesPreserved,
    bool TownSquareFlyInRelocatedExactly,
    bool TownSquareReturnHomeRelocatedExactly,
    bool ResidualTargetCapacityPreserved,
    bool SourceImagePreserved,
    bool AtomicRenameCompleted);

/// <summary>
/// Disposable V5 research compiler: installs the complete retail Town Square overlay/data pair
/// in Stone Hill's existing slot. It does not participate in normal Create BIN and cannot be
/// promoted without DuckStation evidence.
/// </summary>
public static class StoneHillTownSquareReplacementCandidateComposer
{
    private const int WadLba = 37;
    private const int WadHeaderByteLength = 0x800;
    private const int TargetOverlayEntryIndex = 11;
    private const int TargetDataEntryIndex = 12;
    private const int DonorOverlayEntryIndex = 15;
    private const int DonorDataEntryIndex = 16;
    private const long ExpectedTargetOverlayOffset = 0xB83800;
    private const int ExpectedTargetOverlayByteLength = 0x10000;
    private const long ExpectedTargetDataOffset = 0xB93800;
    private const int ExpectedTargetDataByteLength = 0x362800;
    private const long ExpectedDonorOverlayOffset = 0x118E800;
    private const int ExpectedDonorOverlayByteLength = 0xF800;
    private const long ExpectedDonorDataOffset = 0x119E000;
    private const int ExpectedDonorDataByteLength = 0x2E2000;
    private const int TargetOverlayMarker = 12;
    private const int DonorOverlayMarker = 14;
    private const int ExecutableLba = 53875;
    private const int ExecutableByteLength = 0x66000;
    private const long StoneDispatchFileOffset = 0x4AFB0;
    private const long TownDispatchFileOffset = 0x4B098;
    private const int DispatchByteLength = 0x98;
    private const long Demo0LevelIdFileOffset = 0x5F67C;
    private const long Demo0FrameCountFileOffset = 0x5F68C;
    private const long Demo0StartPoseFileOffset = 0x5F69C;
    private const long DonorFlyInWadOffset = 0x136E000;
    private const long TargetFlyInWadOffset = 0xD63800;
    private const int FlyInGuardByteLength = 0x10;
    private const long DonorReturnHome96WadOffset = 0x1370270;
    private const long DonorReturnHome97WadOffset = 0x13702C8;
    private const long TargetReturnHome96WadOffset = 0xD65A70;
    private const long TargetReturnHome97WadOffset = 0xD65AC8;

    private const string TargetOverlaySha256 =
        "876c0145649bb5b26921858d4d677468463974901dcc416dcba5ccea25caa069";
    private const string TargetDataSha256 =
        "c341a3a10a67590c69d23547f6ad147f05f0b6fb7d0fb1360d572793276e796b";
    private const string DonorOverlaySha256 =
        "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5";
    private const string DonorDataSha256 =
        "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";
    private const string PatchedWadHeaderSha256 =
        "dcce03cf0af4be68ef5015b708dc0cad34bc18210107b9b0f21647a5bd545702";
    private const string RetailExecutableSha256 =
        "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private const string StoneDispatchSha256 =
        "7e394297f054d99c8bdc85d4289be3bc6cea35d10f2e101647a572749bcb613f";
    private const string TownDispatchSha256 =
        "fe4669229aedbbc8ff698d57a237327af36025aaeda0ff5cc2e1c806fd23e50b";
    private const string PatchedExecutableSha256 =
        "558d4f5f0f7dd482b035d5f5793bfc6cf886d9cdd218562f4cedd4b1effbfab9";
    private static readonly byte[] Demo0LevelIdBefore = UInt32Bytes(11);
    private static readonly byte[] Demo0LevelIdAfter = UInt32Bytes(24);
    private static readonly byte[] Demo0FrameCountBefore = UInt32Bytes(860);
    private static readonly byte[] Demo0FrameCountAfter = UInt32Bytes(1100);
    private static readonly byte[] Demo0StartPoseBefore =
        Convert.FromHexString("BE9E02004F2502006A51000020000000");
    private static readonly byte[] Demo0StartPoseAfter =
        Convert.FromHexString("BD1401006D6101003343000080000000");

    public static async Task<StoneHillTownSquareReplacementCandidatePlan> BuildPlanAsync(
        StoneHillTownSquareReplacementCandidateRequest request,
        CancellationToken cancellationToken = default) =>
        (await PrepareAsync(request, cancellationToken)).Plan;

    public static async Task<StoneHillTownSquareReplacementCandidateResult> ExportAsync(
        StoneHillTownSquareReplacementCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        PreparedCandidate prepared = await PrepareAsync(request, cancellationToken);
        string sourceImage = Path.GetFullPath(request.SourceImagePath);
        string sourceCue = Path.GetFullPath(request.SourceCuePath);
        string outputImage = Path.GetFullPath(request.OutputImagePath);
        string outputCue = Path.GetFullPath(request.OutputCuePath);
        NativeLevelReplacementBaselineExporter.EnsureDistinctRoles(
            sourceImage, sourceCue, outputImage, outputCue);
        if (!PathEquals(Path.GetDirectoryName(outputImage), Path.GetDirectoryName(outputCue)))
            throw new InvalidOperationException("The replacement candidate BIN and CUE must share one directory.");
        Directory.CreateDirectory(Path.GetDirectoryName(outputImage)!);
        NativeLevelReplacementBaselineExporter.ValidateCue(sourceCue, sourceImage, "MODE2/2352");

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
            await DiscImageWorkingCopy.StageAsync(sourceImage, temporaryImage, false, cancellationToken);
            DiscLayout layout = DiscImage.DetectLayout(temporaryImage);
            int rebuiltRawSectors;
            await using (FileStream output = new(
                temporaryImage, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                DiscImage.WriteFileBytes(
                    output, layout, WadLba, prepared.TargetOverlay.WadOffset, prepared.OutputOverlay);
                DiscImage.WriteFileBytes(
                    output, layout, WadLba, prepared.TargetData.WadOffset, prepared.DonorDataBytes);
                DiscImage.WriteFileBytes(
                    output,
                    layout,
                    WadLba,
                    (TargetOverlayEntryIndex * 8L) + 4,
                    UInt32Bytes((uint)prepared.DonorOverlay.ByteLength));
                DiscImage.WriteFileBytes(
                    output,
                    layout,
                    WadLba,
                    (TargetDataEntryIndex * 8L) + 4,
                    UInt32Bytes((uint)prepared.DonorData.ByteLength));
                DiscImage.WriteFileBytes(
                    output, layout, prepared.Executable.Lba, StoneDispatchFileOffset, prepared.TownDispatch);
                DiscImage.WriteFileBytes(
                    output, layout, prepared.Executable.Lba, Demo0LevelIdFileOffset, Demo0LevelIdAfter);
                DiscImage.WriteFileBytes(
                    output, layout, prepared.Executable.Lba, Demo0FrameCountFileOffset, Demo0FrameCountAfter);
                DiscImage.WriteFileBytes(
                    output, layout, prepared.Executable.Lba, Demo0StartPoseFileOffset, Demo0StartPoseAfter);
                rebuiltRawSectors =
                    RawMode2Form1SectorIntegrity.RebuildFileRanges(
                        output,
                        layout,
                        WadLba,
                        [
                            ((TargetOverlayEntryIndex * 8L) + 4, 4),
                            ((TargetDataEntryIndex * 8L) + 4, 4),
                            (prepared.TargetOverlay.WadOffset, prepared.OutputOverlay.Length),
                            (prepared.TargetData.WadOffset, prepared.DonorDataBytes.Length)
                        ]) +
                    RawMode2Form1SectorIntegrity.RebuildFileRanges(
                        output,
                        layout,
                        prepared.Executable.Lba,
                        [
                            (StoneDispatchFileOffset, DispatchByteLength),
                            (Demo0LevelIdFileOffset, Demo0LevelIdAfter.Length),
                            (Demo0FrameCountFileOffset, Demo0FrameCountAfter.Length),
                            (Demo0StartPoseFileOffset, Demo0StartPoseAfter.Length)
                        ]);
                output.Flush(flushToDisk: true);
            }

            CandidateReadback readback = VerifyReadback(
                sourceImage,
                temporaryImage,
                prepared,
                cancellationToken);
            string cueText = DiscImage.BuildCueText(sourceCue, Path.GetFileName(outputImage));
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
            string sourceSha256After = await HashFileAsync(sourceImage, cancellationToken);
            bool sourcePreserved = string.Equals(
                sourceSha256After,
                request.Manifest.Source.SourceImageSha256,
                StringComparison.OrdinalIgnoreCase);
            if (!sourcePreserved)
                throw new InvalidDataException("The retail source BIN changed during replacement export.");

            TryDelete(backupImage);
            TryDelete(backupCue);
            return new StoneHillTownSquareReplacementCandidateResult(
                outputImage,
                outputCue,
                outputSha256,
                prepared.Plan,
                readback.ChangedWadBytes,
                readback.ChangedExecutableBytes,
                rebuiltRawSectors,
                ExactLogicalDiffBoundaryVerified: true,
                ArtisansPortalPreimagesVerified: true,
                DonorEntriesPreserved: true,
                TownSquareFlyInRelocatedExactly: true,
                TownSquareReturnHomeRelocatedExactly: true,
                ResidualTargetCapacityPreserved: true,
                SourceImagePreserved: true,
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
                    "The replacement candidate failed and one or more prior outputs could not be restored.",
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
        StoneHillTownSquareReplacementCandidateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Manifest);
        ArgumentNullException.ThrowIfNull(request.Catalog);
        NativeLevelReplacementSourceBinding sourceBinding =
            await NativeLevelReplacementStore.ValidateSourceAsync(
                request.Manifest,
                request.SourceImagePath,
                request.Catalog,
                cancellationToken);
        LevelDefinition townSquare = request.Catalog.FindByKey("townsquare")
            ?? throw new InvalidDataException("Town Square is missing from the retail level catalog.");
        if (townSquare.LevelId != 13 || townSquare.SourceWadEntry != DonorDataEntryIndex)
            throw new InvalidDataException("Town Square no longer resolves to retail level 13 / WAD entry 16.");

        string sourceImage = Path.GetFullPath(request.SourceImagePath);
        DiscLayout layout = DiscImage.DetectLayout(sourceImage);
        await using FileStream source = new(sourceImage, FileMode.Open, FileAccess.Read, FileShare.Read);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(source, layout, name =>
            name.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord executable = DiscImage.FindRootFileRecord(source, layout, name =>
            name.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        if (wad.Lba != WadLba || executable.Lba != ExecutableLba ||
            executable.Size != ExecutableByteLength)
            throw new InvalidDataException("The clean-USA WAD/executable placement changed.");

        byte[] wadHeader = DiscImage.ReadFileBytes(source, layout, wad.Lba, 0, WadHeaderByteLength);
        RequireHash(wadHeader, sourceBinding.WadArchiveHeaderSha256, "retail WAD header");
        NativeWadEntryPreimage targetOverlay = ReadEntry(
            source, layout, wad, wadHeader, TargetOverlayEntryIndex, cancellationToken);
        NativeWadEntryPreimage targetData = ReadEntry(
            source, layout, wad, wadHeader, TargetDataEntryIndex, cancellationToken);
        NativeWadEntryPreimage donorOverlay = ReadEntry(
            source, layout, wad, wadHeader, DonorOverlayEntryIndex, cancellationToken);
        NativeWadEntryPreimage donorData = ReadEntry(
            source, layout, wad, wadHeader, DonorDataEntryIndex, cancellationToken);
        RequireEntry(targetOverlay, ExpectedTargetOverlayOffset, ExpectedTargetOverlayByteLength,
            TargetOverlaySha256, "Stone Hill overlay");
        RequireEntry(targetData, ExpectedTargetDataOffset, ExpectedTargetDataByteLength,
            TargetDataSha256, "Stone Hill data");
        RequireEntry(donorOverlay, ExpectedDonorOverlayOffset, ExpectedDonorOverlayByteLength,
            DonorOverlaySha256, "Town Square overlay");
        RequireEntry(donorData, ExpectedDonorDataOffset, ExpectedDonorDataByteLength,
            DonorDataSha256, "Town Square data");
        if (targetOverlay != sourceBinding.LoadedDataPredecessorEntry ||
            targetData != sourceBinding.LevelDataEntry)
            throw new InvalidDataException("The manifest's Stone Hill overlay/data preimages changed.");
        if (donorOverlay.ByteLength > targetOverlay.ByteLength || donorData.ByteLength > targetData.ByteLength)
            throw new InvalidDataException("The Town Square pair no longer fits Stone Hill's fixed retail capacity.");

        byte[] donorOverlayBytes = DiscImage.ReadFileBytes(
            source, layout, wad.Lba, donorOverlay.WadOffset, donorOverlay.ByteLength);
        byte[] donorDataBytes = DiscImage.ReadFileBytes(
            source, layout, wad.Lba, donorData.WadOffset, donorData.ByteLength);
        if (BinaryPrimitives.ReadInt32LittleEndian(donorOverlayBytes) != DonorOverlayMarker ||
            targetOverlay.FirstWord != TargetOverlayMarker)
            throw new InvalidDataException("The Stone Hill/Town Square overlay loader markers changed.");
        // Word zero is the donor's PsyQ/linker overlay ID, not g_LevelId. Keep the complete
        // donor overlay byte-identical and remap only the executable's level-11 dispatch.
        byte[] outputOverlay = donorOverlayBytes.ToArray();

        byte[] outputWadHeader = wadHeader.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(
            outputWadHeader.AsSpan((TargetOverlayEntryIndex * 8) + 4, 4),
            (uint)donorOverlay.ByteLength);
        BinaryPrimitives.WriteUInt32LittleEndian(
            outputWadHeader.AsSpan((TargetDataEntryIndex * 8) + 4, 4),
            (uint)donorData.ByteLength);
        RequireHash(outputWadHeader, PatchedWadHeaderSha256, "replacement WAD header");

        byte[] executableBytes = DiscImage.ReadFileBytes(
            source, layout, executable.Lba, 0, executable.Size);
        RequireHash(executableBytes, RetailExecutableSha256, "retail executable");
        byte[] stoneDispatch = executableBytes.AsSpan((int)StoneDispatchFileOffset, DispatchByteLength).ToArray();
        byte[] townDispatch = executableBytes.AsSpan((int)TownDispatchFileOffset, DispatchByteLength).ToArray();
        RequireHash(stoneDispatch, StoneDispatchSha256, "Stone Hill overlay dispatch routine");
        RequireHash(townDispatch, TownDispatchSha256, "Town Square overlay dispatch routine");
        byte[] patchedExecutable = executableBytes.ToArray();
        townDispatch.CopyTo(patchedExecutable, (int)StoneDispatchFileOffset);
        RequireSlice(executableBytes, Demo0LevelIdFileOffset, Demo0LevelIdBefore, "Stone Hill demo level ID");
        RequireSlice(executableBytes, Demo0FrameCountFileOffset, Demo0FrameCountBefore, "Stone Hill demo frame count");
        RequireSlice(executableBytes, Demo0StartPoseFileOffset, Demo0StartPoseBefore, "Stone Hill demo start pose");
        Demo0LevelIdAfter.CopyTo(patchedExecutable, (int)Demo0LevelIdFileOffset);
        Demo0FrameCountAfter.CopyTo(patchedExecutable, (int)Demo0FrameCountFileOffset);
        Demo0StartPoseAfter.CopyTo(patchedExecutable, (int)Demo0StartPoseFileOffset);
        RequireHash(patchedExecutable, PatchedExecutableSha256, "replacement executable");

        IReadOnlyList<NativeNestedSubfilePreimage> donorSubfiles = ParseNestedSubfiles(donorDataBytes);
        if (donorSubfiles.Count != 8)
            throw new InvalidDataException("Town Square no longer has the checked eight-part packed archive.");

        IReadOnlyList<StoneHillTownSquareReplacementPatchSummary> patches =
        [
            Summary("WAD.WAD", "target-overlay-payload", targetOverlay.WadOffset,
                DiscImage.ReadFileBytes(source, layout, wad.Lba, targetOverlay.WadOffset, donorOverlay.ByteLength),
                outputOverlay),
            Summary("WAD.WAD", "target-level-data-payload", targetData.WadOffset,
                DiscImage.ReadFileBytes(source, layout, wad.Lba, targetData.WadOffset, donorData.ByteLength),
                donorDataBytes),
            Summary("WAD.WAD", "target-overlay-directory-size", (TargetOverlayEntryIndex * 8L) + 4,
                wadHeader.AsSpan((TargetOverlayEntryIndex * 8) + 4, 4).ToArray(),
                UInt32Bytes((uint)donorOverlay.ByteLength)),
            Summary("WAD.WAD", "target-data-directory-size", (TargetDataEntryIndex * 8L) + 4,
                wadHeader.AsSpan((TargetDataEntryIndex * 8) + 4, 4).ToArray(),
                UInt32Bytes((uint)donorData.ByteLength)),
            Summary(executable.Name, "level-11-overlay-dispatch", StoneDispatchFileOffset,
                stoneDispatch, townDispatch),
            Summary(executable.Name, "retire-stone-hill-title-demo-level", Demo0LevelIdFileOffset,
                Demo0LevelIdBefore, Demo0LevelIdAfter),
            Summary(executable.Name, "retire-stone-hill-title-demo-length", Demo0FrameCountFileOffset,
                Demo0FrameCountBefore, Demo0FrameCountAfter),
            Summary(executable.Name, "retire-stone-hill-title-demo-pose", Demo0StartPoseFileOffset,
                Demo0StartPoseBefore, Demo0StartPoseAfter)
        ];
        StoneHillTownSquareReplacementSafetyReport safety = new(
            "static-readback-passed-runtime-pending",
            WritableScopes:
            [
                "Stone Hill WAD entries 11/12 payload bytes within their original fixed capacities",
                "WAD directory size words for entries 11/12; offsets remain fixed",
                "SCUS level-11 SetOverlayPointers routine and demo-slot-0 retirement row only"
            ],
            ProtectedScopes:
            [
                "Artisans WAD entries 9/10 and Stone Hill portal controls T38/T144/T157",
                "Town Square donor WAD entries 15/16",
                "Every WAD entry except target entries 11/12 and their two size words",
                "Every executable byte outside the level-11 dispatch routine and three demo-slot-0 fields",
                "Normal V4 Create BIN, projects, releases, and update channel"
            ],
            RuntimeChecks:
            [
                "Enter through the Artisans portal still labelled Stone Hill",
                "Confirm Town Square geometry, sky, textures, Mobys, actor sounds, and camera load; music remains Stone Hill in this first slot-identity proof",
                "Collect gems, rescue a dragon, chase the egg thief, die/reload, and pause",
                "Use Town Square's Return Home portal and re-enter from Artisans",
                "Use a fresh save/disposable memory card while slot-11 progression behavior is under test",
                "Allow the title demo cycle once; demo slot 0 should now use the native Doctor Shemp demo instead of reading beyond Town Square data"
            ],
            RequiresDuckStationRuntimeProof: true);
        StoneHillTownSquareReplacementCandidatePlan plan = new(
            DateTimeOffset.UtcNow,
            "stonehill",
            "townsquare",
            targetOverlay,
            targetData,
            donorOverlay,
            donorData,
            outputOverlay.Length,
            donorDataBytes.Length,
            DonorOverlaySha256,
            Hash(donorDataBytes),
            Hash(outputWadHeader),
            PatchedExecutableSha256,
            donorSubfiles,
            patches,
            safety);
        return new PreparedCandidate(
            plan,
            targetOverlay,
            targetData,
            donorOverlay,
            donorData,
            executable,
            outputOverlay,
            donorDataBytes,
            townDispatch,
            sourceBinding.ArtisansPortalControlRows);
    }

    private static CandidateReadback VerifyReadback(
        string sourceImagePath,
        string outputImagePath,
        PreparedCandidate prepared,
        CancellationToken cancellationToken)
    {
        DiscLayout sourceLayout = DiscImage.DetectLayout(sourceImagePath);
        DiscLayout outputLayout = DiscImage.DetectLayout(outputImagePath);
        if (sourceLayout.SectorSize != outputLayout.SectorSize ||
            sourceLayout.UserOffset != outputLayout.UserOffset)
            throw new InvalidDataException("The staged candidate disc layout changed.");
        using FileStream source = File.OpenRead(sourceImagePath);
        using FileStream output = File.OpenRead(outputImagePath);
        DiscFileRecord sourceWad = DiscImage.FindRootFileRecord(source, sourceLayout, name =>
            name.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord outputWad = DiscImage.FindRootFileRecord(output, outputLayout, name =>
            name.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord sourceExe = DiscImage.FindRootFileRecord(source, sourceLayout, name =>
            name.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord outputExe = DiscImage.FindRootFileRecord(output, outputLayout, name =>
            name.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        if (sourceWad != outputWad || sourceExe != outputExe)
            throw new InvalidDataException("The replacement candidate moved a retail disc file extent.");

        RequireHash(
            DiscImage.ReadFileBytes(output, outputLayout, outputWad.Lba, 0, WadHeaderByteLength),
            PatchedWadHeaderSha256,
            "output WAD header");
        RequireHash(
            DiscImage.ReadFileBytes(output, outputLayout, outputWad.Lba,
                prepared.TargetOverlay.WadOffset, prepared.OutputOverlay.Length),
            DonorOverlaySha256,
            "output Stone Hill overlay slot");
        RequireHash(
            DiscImage.ReadFileBytes(output, outputLayout, outputWad.Lba,
                prepared.TargetData.WadOffset, prepared.DonorDataBytes.Length),
            DonorDataSha256,
            "output Stone Hill data slot");
        RequireHash(
            DiscImage.ReadFileBytes(output, outputLayout, outputExe.Lba, 0, outputExe.Size),
            PatchedExecutableSha256,
            "output executable");

        VerifyBytesEqual(
            DiscImage.ReadFileBytes(source, sourceLayout, sourceWad.Lba,
                prepared.DonorOverlay.WadOffset, prepared.DonorOverlay.ByteLength),
            DiscImage.ReadFileBytes(output, outputLayout, outputWad.Lba,
                prepared.DonorOverlay.WadOffset, prepared.DonorOverlay.ByteLength),
            "Town Square donor overlay changed");
        VerifyBytesEqual(
            DiscImage.ReadFileBytes(source, sourceLayout, sourceWad.Lba,
                prepared.DonorData.WadOffset, prepared.DonorData.ByteLength),
            DiscImage.ReadFileBytes(output, outputLayout, outputWad.Lba,
                prepared.DonorData.WadOffset, prepared.DonorData.ByteLength),
            "Town Square donor data changed");
        foreach (NativeMobyRowPreimage portal in prepared.PortalRows)
        {
            byte[] before = DiscImage.ReadFileBytes(
                source, sourceLayout, sourceWad.Lba, portal.WadOffset, MobyLoader.RuntimeRecordStride);
            byte[] after = DiscImage.ReadFileBytes(
                output, outputLayout, outputWad.Lba, portal.WadOffset, MobyLoader.RuntimeRecordStride);
            RequireHash(before, portal.Sha256, $"portal T{portal.TrueIndex} source");
            VerifyBytesEqual(before, after, $"Artisans portal T{portal.TrueIndex} changed");
        }

        VerifyRelocatedBytes(
            source, output, sourceLayout, outputLayout, sourceWad.Lba,
            DonorFlyInWadOffset, TargetFlyInWadOffset, FlyInGuardByteLength, "Town Square fly-in");
        VerifyRelocatedBytes(
            source, output, sourceLayout, outputLayout, sourceWad.Lba,
            DonorReturnHome96WadOffset, TargetReturnHome96WadOffset,
            MobyLoader.RuntimeRecordStride, "Town Square Return Home T96");
        VerifyRelocatedBytes(
            source, output, sourceLayout, outputLayout, sourceWad.Lba,
            DonorReturnHome97WadOffset, TargetReturnHome97WadOffset,
            MobyLoader.RuntimeRecordStride, "Town Square Return Home T97");
        VerifyRelocatedBytes(
            source, output, sourceLayout, outputLayout, sourceWad.Lba,
            prepared.TargetOverlay.WadOffset + prepared.OutputOverlay.Length,
            prepared.TargetOverlay.WadOffset + prepared.OutputOverlay.Length,
            prepared.TargetOverlay.ByteLength - prepared.OutputOverlay.Length,
            "unused Stone Hill overlay capacity");
        VerifyRelocatedBytes(
            source, output, sourceLayout, outputLayout, sourceWad.Lba,
            prepared.TargetData.WadOffset + prepared.DonorDataBytes.Length,
            prepared.TargetData.WadOffset + prepared.DonorDataBytes.Length,
            prepared.TargetData.ByteLength - prepared.DonorDataBytes.Length,
            "unused Stone Hill data capacity");

        (long changedWad, long outsideWad) = CompareLogicalFile(
            source,
            output,
            sourceLayout,
            sourceWad.Lba,
            sourceWad.Size,
            [
                new((TargetOverlayEntryIndex * 8L) + 4, 4),
                new((TargetDataEntryIndex * 8L) + 4, 4),
                new(prepared.TargetOverlay.WadOffset, prepared.OutputOverlay.Length),
                new(prepared.TargetData.WadOffset, prepared.DonorDataBytes.Length)
            ],
            cancellationToken);
        (long changedExe, long outsideExe) = CompareLogicalFile(
            source,
            output,
            sourceLayout,
            sourceExe.Lba,
            sourceExe.Size,
            [
                new(StoneDispatchFileOffset, DispatchByteLength),
                new(Demo0LevelIdFileOffset, Demo0LevelIdAfter.Length),
                new(Demo0FrameCountFileOffset, Demo0FrameCountAfter.Length),
                new(Demo0StartPoseFileOffset, Demo0StartPoseAfter.Length)
            ],
            cancellationToken);
        if (outsideWad != 0 || outsideExe != 0 || changedWad == 0 || changedExe == 0)
            throw new InvalidDataException(
                $"Replacement diff boundary failed: WAD changed={changedWad}, outside={outsideWad}; " +
                $"EXE changed={changedExe}, outside={outsideExe}.");
        return new CandidateReadback(changedWad, changedExe);
    }

    private static NativeWadEntryPreimage ReadEntry(
        FileStream image,
        DiscLayout layout,
        DiscFileRecord wad,
        byte[] wadHeader,
        int index,
        CancellationToken cancellationToken)
    {
        long offset = BinaryPrimitives.ReadUInt32LittleEndian(wadHeader.AsSpan(index * 8, 4));
        int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(
            wadHeader.AsSpan((index * 8) + 4, 4)));
        byte[] head = DiscImage.ReadFileBytes(image, layout, wad.Lba, offset, 8);
        byte[] bytes = DiscImage.ReadFileBytes(image, layout, wad.Lba, offset, length);
        cancellationToken.ThrowIfCancellationRequested();
        return new NativeWadEntryPreimage(
            index,
            offset,
            length,
            BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(0, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(4, 4)),
            Hash(bytes));
    }

    private static IReadOnlyList<NativeNestedSubfilePreimage> ParseNestedSubfiles(byte[] archive)
    {
        int headerLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(archive.AsSpan(0, 4)));
        if (headerLength != 0x800)
            throw new InvalidDataException($"Town Square nested header changed from 0x800 to 0x{headerLength:X}.");
        List<NativeNestedSubfilePreimage> result = [];
        int expectedOffset = headerLength;
        for (int index = 0; index < 8; index++)
        {
            int row = index * 8;
            int offset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(archive.AsSpan(row, 4)));
            int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(archive.AsSpan(row + 4, 4)));
            if (offset != expectedOffset || length <= 0 || (long)offset + length > archive.Length)
                throw new InvalidDataException($"Town Square nested subfile {index} is not a strict packed range.");
            result.Add(new NativeNestedSubfilePreimage(
                index, offset, length, Hash(archive.AsSpan(offset, length))));
            expectedOffset = checked(offset + length);
        }
        if (expectedOffset != archive.Length)
            throw new InvalidDataException("Town Square's eight nested subfiles do not cover its complete archive.");
        return result;
    }

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

    private static void VerifyRelocatedBytes(
        FileStream source,
        FileStream output,
        DiscLayout sourceLayout,
        DiscLayout outputLayout,
        int wadLba,
        long sourceOffset,
        long targetOffset,
        int byteLength,
        string label)
    {
        if (byteLength <= 0)
            return;
        VerifyBytesEqual(
            DiscImage.ReadFileBytes(source, sourceLayout, wadLba, sourceOffset, byteLength),
            DiscImage.ReadFileBytes(output, outputLayout, wadLba, targetOffset, byteLength),
            $"{label} readback differs");
    }

    private static StoneHillTownSquareReplacementPatchSummary Summary(
        string file,
        string kind,
        long offset,
        byte[] before,
        byte[] after) =>
        new(file, kind, offset, after.Length, Hash(before), Hash(after));

    private static void RequireEntry(
        NativeWadEntryPreimage entry,
        long expectedOffset,
        int expectedLength,
        string expectedSha256,
        string label)
    {
        if (entry.WadOffset != expectedOffset || entry.ByteLength != expectedLength ||
            !entry.Sha256.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {label} preimage changed.");
    }

    private static void RequireHash(ReadOnlySpan<byte> bytes, string expected, string label)
    {
        string actual = Hash(bytes);
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {label} SHA-256 is {actual}, expected {expected}.");
    }

    private static void VerifyBytesEqual(byte[] expected, byte[] actual, string message)
    {
        if (!expected.SequenceEqual(actual))
            throw new InvalidDataException(message);
    }

    private static void RequireSlice(byte[] source, long offset, byte[] expected, string label)
    {
        if (offset < 0 || offset + expected.Length > source.Length ||
            !source.AsSpan((int)offset, expected.Length).SequenceEqual(expected))
            throw new InvalidDataException($"The {label} preimage changed.");
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static byte[] UInt32Bytes(uint value)
    {
        byte[] result = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(result, value);
        return result;
    }

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

    private sealed record PreparedCandidate(
        StoneHillTownSquareReplacementCandidatePlan Plan,
        NativeWadEntryPreimage TargetOverlay,
        NativeWadEntryPreimage TargetData,
        NativeWadEntryPreimage DonorOverlay,
        NativeWadEntryPreimage DonorData,
        DiscFileRecord Executable,
        byte[] OutputOverlay,
        byte[] DonorDataBytes,
        byte[] TownDispatch,
        IReadOnlyList<NativeMobyRowPreimage> PortalRows);

    private sealed record CandidateReadback(long ChangedWadBytes, long ChangedExecutableBytes);
    private sealed record LogicalRange(long Offset, long ByteLength)
    {
        public bool Contains(long value) => value >= Offset && value < Offset + ByteLength;
    }
}

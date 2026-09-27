using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Closed original-USA, bootstrap/reservation-only loading experiment. Permission
/// to discover real loader behavior is NOT RequireNativeInstallation approval.
/// No arbitrary executable, manifest, WAD, model or overlay enters this recipe.
/// </summary>
public sealed class NativeCollectibleStartupResearchPlan
{
    public const string Profile = "native-collectible-startup-bootstrap-reservation-research-v1";
    public const string ImageName = "collectible-startup-bootstrap-reservation-r01.bin";
    public const string CueName = "collectible-startup-bootstrap-reservation-r01.cue";
    public const string ReceiptName = "collectible-startup-bootstrap-reservation.receipt.json";
    public const int ExecutableLba = 53875;
    public static IReadOnlyList<int> ChangedSectorLbas { get; } = Array.AsReadOnly(new[] { 53875, 54078 });
    internal static string[] OutputNames => [ImageName, ReceiptName, CueName]; // CUE last at commit.
    private readonly NativeCollectibleColdBootBootstrap bootstrap;
    private readonly NativeFreeFlightResearchSourceCue cue;
    private readonly byte[] original, executable;
    private NativeCollectibleStartupResearchPlan(string source, NativeFreeFlightResearchSourceCue cue,
        NativeCollectibleColdBootBootstrap bootstrap)
    {
        SourceImagePath = source; this.cue = cue; this.bootstrap = bootstrap;
        original = bootstrap.Reservation.CopyOriginalExecutable(); executable = bootstrap.CopyExecutable();
        RequireExactExecutable(executable);
        if (!bootstrap.RevertExact(executable).AsSpan().SequenceEqual(original))
            throw new InvalidDataException("Startup research recipe lost its complete original inverse.");
        ExecutableSha256 = Hash(executable);
        ExpectedImageSha256 = ComputeExpectedImageHash();
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new { Profile, SourceImagePath, SourceCuePath,
            sourceImageSha256 = NativeMobySourceInventory.OriginalImageSha256, sourceCueSha256 = cue.SourceSha256,
            ExecutableSha256, ExpectedImageSha256, ChangedSectorLbas, ResearchDiscoveryPublicationEnabled,
            NativeInstallationApproved, ReadyStateIssued, LifetimeProven, WholeEngineFitProven, CardIoApproved }));
    }
    public string SourceImagePath { get; }
    public string SourceCuePath => cue.CuePath;
    public string ExecutableSha256 { get; }
    public string ExpectedImageSha256 { get; }
    public string PlanSha256 { get; }
    public bool ResearchOnly => true;
    public bool ResearchDiscoveryPublicationEnabled => true;
    public bool NativeInstallationApproved => false;
    public bool ReadyStateIssued => false;
    public bool LifetimeProven => false;
    public bool WholeEngineFitProven => false;
    public bool CardIoApproved => false;
    public bool NormalCreateBinApproved => false;
    public bool RuntimeVerified => false;

    public static NativeCollectibleStartupResearchPlan Capture(string originalImagePath, string originalCuePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalImagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalCuePath);
        string source = Path.GetFullPath(originalImagePath);
        var cue = NativeFreeFlightResearchSourceCue.Capture(originalCuePath, source, ImageName);
        using var usa = NativeRegionalSourceSession.Open(source, NativeDiscRevision.UsaScus94228);
        try
        {
            // Two identities size the EXISTING descriptor only. Neither these
            // identities nor any Ready/manifest/card bytes enter the disc.
            var sizing = DetachedCollectibleManifest.CreateNew(Guid.Parse("12345678-1234-5678-9012-123456789abc"),
                [Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222")]);
            var residence = NativeCollectibleSelectedSlotResidence.Capture(usa, sizing, 0);
            var bootstrap = NativeCollectibleColdBootBootstrap.Create(NativeCollectibleReservationMechanics.Create(residence));
            return new(source, cue, bootstrap);
        }
        finally { try { usa.VerifyUnchanged(); } finally { cue.RequireFresh(); } }
    }

    public void RequireFresh()
    {
        cue.RequireFresh();
        try
        {
            using var usa = NativeRegionalSourceSession.Open(SourceImagePath, NativeDiscRevision.UsaScus94228);
            usa.VerifyUnchanged();
        }
        finally { cue.RequireFresh(); }
    }
    public void RequireNativeInstallation() => bootstrap.RequireNativeInstallation();
    internal byte[] CopyOriginalExecutable() => original.ToArray();
    internal byte[] CopyExecutable() => executable.ToArray();
    internal byte[] CopyCueBytes() => cue.CopyOutputBytes();
    internal byte[] RevertExecutableExact(ReadOnlySpan<byte> bytes) => bootstrap.RevertExact(bytes);

    // Independent fixed recipe recognizer, not a factory or bytes-to-authority API.
    internal static void RequireExactExecutable(ReadOnlySpan<byte> bytes)
    {
        uint[] words = [0x3C02801F, 0x3442F600, 0x3C03801F, 0x3463F800, 0xAC400000,
            0x24420004, 0x0043082B, 0x1420FFFC, 0, 0x08016E38, 0];
        if (bytes.Length != 0x66000 || BinaryPrimitives.ReadUInt32LittleEndian(bytes[0x10..]) != 0x80075640 ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[0x65DA4..]) != 0xA00)
            throw new InvalidDataException("Not the fixed startup research executable afterimage.");
        for (int i = 0; i < words.Length; i++)
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes[(0x65E40 + i * 4)..]) != words[i])
                throw new InvalidDataException("Startup research stub differs from its closed instruction recipe.");
        byte[] reverted = bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(reverted.AsSpan(0x10), 0x8005B8E0);
        BinaryPrimitives.WriteUInt32LittleEndian(reverted.AsSpan(0x65DA4), 0x800);
        reverted.AsSpan(0x65E40, 0x2C).Clear();
        NativeCollectibleColdBootBootstrap.RequireOriginalSource(reverted);
    }

    internal byte[] ExpectedSector(int lba, byte[] before)
    {
        if (lba < 0 || lba >= NativeMobySourceInventory.OriginalImageByteLength / 2352 || before.Length != 2352)
            throw new InvalidDataException("Startup research physical sector exceeds original owner.");
        byte[] expected = before.ToArray();
        if (lba is not (53875 or 54078)) return expected;
        int offset = (lba - ExecutableLba) * 2048;
        if (!before.AsSpan(24, 2048).SequenceEqual(original.AsSpan(offset, 2048)))
            throw new InvalidDataException("Startup research source executable sector beforeimage changed.");
        RawMode2Form1SectorIntegrity.RequireRawSectorIntegrity(before, lba);
        executable.AsSpan(offset, 2048).CopyTo(expected.AsSpan(24, 2048));
        return RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(expected, lba);
    }

    private string ComputeExpectedImageHash()
    {
        using var source = OpenSource();
        using var originalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var expectedHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] before = new byte[2352]; int changed = 0;
        for (int lba = 0; lba < NativeMobySourceInventory.OriginalImageByteLength / 2352; lba++)
        {
            source.ReadExactly(before); originalHash.AppendData(before);
            byte[] expected = ExpectedSector(lba, before); expectedHash.AppendData(expected);
            if (!expected.AsSpan().SequenceEqual(before)) changed++;
        }
        if (changed != 2 || source.Length != NativeMobySourceInventory.OriginalImageByteLength || source.ReadByte() != -1 ||
            Convert.ToHexStringLower(originalHash.GetHashAndReset()) != NativeMobySourceInventory.OriginalImageSha256)
            throw new InvalidDataException("Startup research whole source identity or exact two-sector scope changed.");
        return Convert.ToHexStringLower(expectedHash.GetHashAndReset());
    }

    // Writer uses the existing logical codec for THREE fixed spans. Readback
    // independently reconstructs complete raw sectors, including EDC/ECC and all
    // unchanged physical complement; it does not call this writer.
    internal void WriteNewImage(string path, CancellationToken token, Action firstSector)
    {
        using var source = OpenSource();
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        byte[] first = new byte[2352]; source.ReadExactly(first); output.Write(first); output.Flush(true);
        firstSector(); token.ThrowIfCancellationRequested();
        byte[] buffer = new byte[1024 * 1024]; int count;
        while ((count = source.Read(buffer)) != 0) { token.ThrowIfCancellationRequested(); output.Write(buffer, 0, count); }
        if (source.Length != NativeMobySourceInventory.OriginalImageByteLength || output.Length != source.Length)
            throw new InvalidDataException("Startup research copy changed length.");
        var layout = DiscImage.DetectLayout(output);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24) throw new InvalidDataException("Wrong raw source layout.");
        foreach (var (offset, length) in new[] { (0x10, 4), (0x65DA4, 4), (0x65E40, 0x2C) })
            DiscImage.WriteFileBytes(output, layout, ExecutableLba, offset, executable.AsSpan(offset, length).ToArray());
        output.Flush(true); token.ThrowIfCancellationRequested();
    }

    internal (string Hash, int Changed) VerifyImage(string path)
    {
        using var source = OpenSource(); using var output = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var result = NativeTownSquareClosurePhysicalReadback.Verify(source, output,
            NativeMobySourceInventory.OriginalImageByteLength, NativeMobySourceInventory.OriginalImageSha256, ExpectedSector);
        if (result.Hash != ExpectedImageSha256 || result.Changed != 2)
            throw new InvalidDataException("Startup research image differs from complete expected image.");
        byte[] readback = DiscImage.ReadFileBytes(output, DiscImage.DetectLayout(output), ExecutableLba, 0, 0x66000);
        if (!RevertExecutableExact(readback).AsSpan().SequenceEqual(original))
            throw new InvalidDataException("Readback executable did not invert to complete original source.");
        return result;
    }

    internal byte[] BuildReceipt() => JsonSerializer.SerializeToUtf8Bytes(new
    {
        profile = Profile, PlanSha256, SourceImagePath, SourceCuePath,
        sourceImageSha256 = NativeMobySourceInventory.OriginalImageSha256, sourceCueSha256 = cue.SourceSha256,
        ExecutableSha256, outputImageSha256 = ExpectedImageSha256, outputCueSha256 = Hash(CopyCueBytes()),
        changedRawSectors = ChangedSectorLbas, originalExecutableLba = ExecutableLba, executableSize = 0x66000,
        logicalEdits = new[] { "EXE+10 entry 8005B8E0->80075640", "EXE+65DA4 reserve 800->A00", "EXE+65E40 fixed2C-byte ephemeral stub" },
        zeroReservationStart = "801FF600", zeroReservationEnd = "801FF800", ledgerPhase = "NOT-LOADED zeros, not Ready",
        completeOriginalExecutableInverseVerified = true, wholeImagePhysicalReadback = true,
        wadModelOverlayDirectoryChanged = false, ResearchOnly, ResearchDiscoveryPublicationEnabled,
        NativeInstallationApproved, ReadyStateIssued, LifetimeProven, WholeEngineFitProven, CardIoApproved,
        NormalCreateBinApproved, RuntimeVerified, exceptionRollbackPublication = true,
        crashAtomicPublication = false, hostileFilesystemRaceSafe = false,
        pending = new[] { "BIOS complete load/cache/handoff/IRQ ownership", "real startup and all-transition lifetime",
            "whole-engine memory fit", "native collector and selected-slot lifecycle", "durable card save and product installation" }
    }, new JsonSerializerOptions { WriteIndented = true });

    private FileStream OpenSource()
    {
        var input = new FileStream(SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length == NativeMobySourceInventory.OriginalImageByteLength) return input;
        input.Dispose(); throw new InvalidDataException("Startup research original source length changed.");
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

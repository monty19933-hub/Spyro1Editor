using System.Text.Json;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

public enum NativeId65UploadBackend
{
    CpuFifo,
    Dma
}

public sealed record NativeId65TransferSourceOwner(
    string Id, int Offset, int ByteLength, int PairedUploadOffset, string Sha256);

/// <summary>Relative main-RAM source reads, not SPU destination ownership.</summary>
public sealed record NativeId65TransferSourceRead(
    NativeId65UploadBackend Backend, int Offset, int ByteLength,
    int RequestedByteLength, int NativeBlockByteLength);

/// <summary>
/// Fixed neutral byte composition for tests. Complete input length is checked
/// before copying; neither caller bytes nor caller hashes grant source authority.
/// </summary>
internal sealed class NativeId65NormalUploadTransferLayout
{
    internal const int PairedUploadLength = 0xDE000;
    internal const int VramLength = 0x80000;
    internal const int BankLength = 0x5E000;
    internal const int CpuLength = 0x7EFF0;
    internal const int DmaLength = 0x7F000;
    private readonly byte[] _transferSource;

    private NativeId65NormalUploadTransferLayout(ReadOnlySpan<byte> upload)
    {
        PairedUploadSha256 = NativeId65NormalResourceSource.Hash(upload);
        _transferSource = new byte[DmaLength];
        upload.Slice(VramLength, BankLength).CopyTo(_transferSource);
        upload.Slice(BankLength, DmaLength - BankLength).CopyTo(_transferSource.AsSpan(BankLength));
        TransferSourceSha256 = NativeId65NormalResourceSource.Hash(_transferSource);
        Owners = Array.AsReadOnly(new[]
        {
            new NativeId65TransferSourceOwner("complete-retained-Town-bank-CD-read", 0, BankLength,
                VramLength, NativeId65NormalResourceSource.Hash(_transferSource.AsSpan(0, BankLength))),
            new NativeId65TransferSourceOwner("same-paired-VRAM-CD-read-residue", BankLength,
                DmaLength - BankLength, BankLength,
                NativeId65NormalResourceSource.Hash(_transferSource.AsSpan(BankLength)))
        });
        SourceReads = Array.AsReadOnly(new[]
        {
            new NativeId65TransferSourceRead(NativeId65UploadBackend.CpuFifo, 0, CpuLength, CpuLength, 2),
            new NativeId65TransferSourceRead(NativeId65UploadBackend.Dma, 0, DmaLength, CpuLength, 64)
        });
        int cursor = 0;
        foreach (var owner in Owners)
        {
            if (owner.Offset != cursor || owner.ByteLength <= 0 ||
                owner.PairedUploadOffset < 0 || owner.PairedUploadOffset + (long)owner.ByteLength > upload.Length ||
                !_transferSource.AsSpan(owner.Offset, owner.ByteLength).SequenceEqual(
                    upload.Slice(owner.PairedUploadOffset, owner.ByteLength)))
                throw new InvalidDataException("Normal transfer source lost an exact nonoverlapping phase owner.");
            cursor = checked(cursor + owner.ByteLength);
        }
        if (cursor != DmaLength)
            throw new InvalidDataException("Normal transfer source has an unowned complement.");
    }

    internal bool SourceCertified => false;
    internal string PairedUploadSha256 { get; }
    internal string TransferSourceSha256 { get; }
    internal IReadOnlyList<NativeId65TransferSourceOwner> Owners { get; }
    internal IReadOnlyList<NativeId65TransferSourceRead> SourceReads { get; }
    internal byte[] CopyTransferSourceImage() => _transferSource.ToArray();

    internal static NativeId65NormalUploadTransferLayout Compile(ReadOnlySpan<byte> upload)
    {
        if (upload.Length != PairedUploadLength)
            throw new InvalidDataException("Normal staging requires the complete fixed paired VRAM and Town bank payload.");
        return new(upload);
    }

    internal void RequireExactTransferSource(ReadOnlySpan<byte> bytes)
    {
        if (!bytes.SequenceEqual(_transferSource))
            throw new InvalidDataException("Normal transfer source changed its bank, paired VRAM residue or complete DMA-only tail.");
    }
}

/// <summary>
/// Source-issued detached transfer-source image for the native sequential normal
/// requests. This is not a bank extension, padded subfile, native header, hardware
/// transfer, SPU-state certificate, runtime memory placement or disc publisher.
/// </summary>
public sealed class NativeId65NormalUploadStaging
{
    private readonly NativeId65NormalUploadTransferLayout _layout;

    private NativeId65NormalUploadStaging(NativeId65NormalUploadStagingSource source,
        NativeId65FreshSceneBundle bundle)
    {
        byte[] upload = bundle.CopyUploadPayload();
        source.RequireIssuedInput(bundle, upload);
        _layout = NativeId65NormalUploadTransferLayout.Compile(upload);
        UpstreamPlanSha256 = bundle.PlanSha256;
        Evidence = Array.AsReadOnly(source.Evidence.ToArray());
        PlanSha256 = NativeId65NormalResourceSource.Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            ProfileId, SourceImageSha256, UpstreamPlanSha256, PairedUploadSha256, TransferSourceSha256,
            CpuRequestedByteLength, DmaProgrammedSourceByteLength, LinearSpuDestination, DmaLinearSpuEnd,
            Owners, SourceReads, Evidence,
            CompleteSpuStateProven, SpuWrapSemanticsProven, ReservedLow16PreservationProven,
            TransferTimingProven, PeakRuntimeMemoryProven, HardwareTransfersProven,
            CompleteLevelResourcesProven, IndependentLevelSupported, RuntimeVerified, PublicationApproved
        }));
    }

    public const string ProfileId = "native-id65-normal-upload-transfer-source-clean-usa-v1";
    public string SourceImageSha256 => NativeId65EngineProfileContract.OriginalImageSha256;
    public string UpstreamPlanSha256 { get; }
    public string PairedUploadSha256 => _layout.PairedUploadSha256;
    public string TransferSourceSha256 => _layout.TransferSourceSha256;
    public string PlanSha256 { get; }
    public int CpuRequestedByteLength => NativeId65NormalUploadTransferLayout.CpuLength;
    public int DmaProgrammedSourceByteLength => NativeId65NormalUploadTransferLayout.DmaLength;
    public int LinearSpuDestination => 0x1010;
    public int DmaLinearSpuEnd => LinearSpuDestination + DmaProgrammedSourceByteLength;
    public IReadOnlyList<NativeId65TransferSourceOwner> Owners => _layout.Owners;
    public IReadOnlyList<NativeId65TransferSourceRead> SourceReads => _layout.SourceReads;
    public IReadOnlyList<NativeId65EngineEvidence> Evidence { get; }
    public bool SourceCertified => true;
    public bool CompleteSpuStateProven => false;
    public bool SpuWrapSemanticsProven => false;
    public bool ReservedLow16PreservationProven => false;
    public bool TransferTimingProven => false;
    public bool PeakRuntimeMemoryProven => false;
    public bool HardwareTransfersProven => false;
    public bool CompleteLevelResourcesProven => false;
    public bool IndependentLevelSupported => false;
    public bool RuntimeVerified => false;
    public bool PublicationApproved => false;
    public IReadOnlyList<string> PendingOwners { get; } = Array.AsReadOnly(new[]
    {
        "The image models source bytes after the normal VRAM request followed by the exact Town bank request; asynchronous CD/GPU/SPU completion and interference are not proved.",
        "CPU consumes 0x7EFF0 bytes; DMA programs 0x7F000. Linear destination 0x1010 plus DMA length ends at 0x80010; SPU wrap and low16 ownership remain unresolved.",
        "The native 16-byte boot/default block is at SPU0x1000, not SPU0. Nonzero DMA-only source bytes are retained without a silence, free-storage or preservation claim.",
        "All eight callbacks, required companion/player/particle state, transient PETE/arenas, auxiliary rescue/replay routes and ID65 linkage remain separate obligations.",
        "No native body/header, descriptor length, padding, loader change, RAM address or publication artifact is emitted. Existing complete Town audio is deliberately retained."
    });

    public static async Task<NativeId65NormalUploadStaging> CaptureAsync(string originalImagePath,
        NativeId65FreshSceneBundle issuedBundle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issuedBundle);
        NativeId65NormalUploadStagingSource source =
            await NativeId65NormalUploadStagingSource.CaptureAsync(originalImagePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new(source, issuedBundle);
    }

    public byte[] CopyTransferSourceImage() => _layout.CopyTransferSourceImage();
    public void RequireExactTransferSource(ReadOnlySpan<byte> bytes) => _layout.RequireExactTransferSource(bytes);
}

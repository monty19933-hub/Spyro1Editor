using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Private complete-original issuer; its byte validator cannot issue a public stage.</summary>
internal sealed class NativeId65NormalUploadStagingSource
{
    private const string ExecutableSha256 = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private readonly byte[] _executable;
    private readonly byte[] _bank;
    private static readonly NativeId65EngineEvidence[] Consumers =
    [
        new("normal-loader-VRAM-then-bank-fixed-SPU-request", "EXE", 0x5EB0, 0x230, "f3cdb7634a2e36f2329959401df8bc6006cd623bd2ea7233e4ca43ea53e42c15"),
        new("CD-request-sector-rounding-and-return-delay", "EXE", 0x6E98, 0xEC, "8b6f4462d2b49636bc61bfb1a737d565c895ef489cb8491d6e789189364c5755"),
        new("normal-SPU-address-and-length-call-delays", "EXE", 0x603C, 0x20, "0ecd01fbbbafa7e7c809e25e6d19a7dea97dffc4a89fcf36beae656c022d7fa8"),
        new("capped-SPU-write-wrapper", "EXE", 0x4D2C4, 0x60, "434a098a521f8a9fc6d1cf1841b4c9ae1ebd1ff946baf5ca019dba734c25cce3"),
        new("CPU-or-DMA-source-backend-selector", "EXE", 0x4CC50, 0x88, "9a189f6b37e833e80d24383ce4d2b5207a5a4903e3be71c84e9a6c3ee874ccf0"),
        new("complete-DMA-register-setup-and-return-delay", "EXE", 0x4C9C0, 0x290, "859e35e5d7e0b766beefa2acd5d14f594b85d023e72ce550b696ecbd69ba5eaf"),
        new("complete-CPU-halfword-FIFO-transfer-and-return-delay", "EXE", 0x4C688, 0x1CC, "0286bcb35b87b4a9740cd22f1b726a838a22bb5f7bd1c9a1b146c6ac5983d024"),
        new("SPU-address-and-CPU-DMA-mode-setters", "EXE", 0x4D324, 0x8C, "39c9e8a0250c560b92632948b3f46384e5c48a7675694720ff8e543d69f31971"),
        new("SPU-address-rounding-helper-and-return-delay", "EXE", 0x4CD88, 0xA4, "a80c1085880eb3cca70a28eb95f5864301589eab5bf349ae3a95f3d1acdca7ca"),
        new("boot-address-units-default-block-call-and-24-voice-starts", "EXE", 0x4C4D0, 0x104, "56dcecb5669da65f16d2ab6b72b144378d6a6f06d2601a8f61ff387f3f84691c"),
        new("SPU-DMA-direction-bus-register-writers", "EXE", 0x4CEC8, 0x58, "4d6b9868d9cd65178a582c02eedb05e56c19e7d910cb4e38f70ad6ae78d220e3"),
        new("SPU-DMA-hardware-pointer-table", "EXE", 0x63D54, 0x18, "32ab0ce4f577189dcfe5ef7be0ad5528ee6e905858e8ad286316aac21498a19b"),
        new("boot-default-block-not-low16-SPU-owner", "EXE", 0x63D94, 0x10, "d761d406af2a4a5a15f67c924378ed88d1f85c13f1a37fc7366f59789b3bcd65"),
        new("preceding-VRAM-rectangle-upload-wrapper", "EXE", 0x50228, 0x64, "18db14f842d5a903afb21394f277e4f3cbe92358f111c16e4767ba9c21119713"),
        new("VRAM-queue-copies-rectangle-not-pixel-source", "EXE", 0x52020, 0x2E0, "9f18ab89a978fe204ad08683aae8584757f5dec1c613275196e7119bc205d1ed"),
        new("VRAM-transfer-reads-pixels-and-programs-source-DMA", "EXE", 0x51A34, 0x23C, "1b71dd10b479a1332b2f1d46a61e3d518a68da755343e9480af5497569831dba"),
        new("VRAM-transfer-function-table-and-source-selected-table-pointer", "EXE", 0x6521C, 0x44, "4e585e686756634a746ac3a80e62e921711fa8c4f09a9690d14e4822360f6d1b"),
        new("VRAM-hardware-source-register-pointers", "EXE", 0x65334, 0x14, "af3001525aaaca5ae08a6dfb79bb6aa38be22c71839b862f0a9d01a371b9a633")
    ];

    private NativeId65NormalUploadStagingSource(NativeId65NormalResourceSource source)
    {
        _executable = source.CopyExecutable();
        ValidateSourceExecutable(_executable);
        _bank = source.CopyData().AsSpan(0x80800, NativeId65NormalUploadTransferLayout.BankLength).ToArray();
        Evidence = Array.AsReadOnly(source.Evidence.Concat(Consumers).Append(
            new NativeId65EngineEvidence("complete-retained-Town-sound-bank", "WAD", 0x121E800,
                _bank.Length, NativeId65NormalResourceSource.Hash(_bank))).ToArray());
    }

    internal IReadOnlyList<NativeId65EngineEvidence> Evidence { get; }
    internal byte[] CopyExecutable() => _executable.ToArray();

    internal static async Task<NativeId65NormalUploadStagingSource> CaptureAsync(string originalImagePath,
        CancellationToken token)
    {
        var source = await NativeId65NormalResourceSource.CaptureAsync(originalImagePath, token);
        token.ThrowIfCancellationRequested();
        return new(source);
    }

    internal void RequireIssuedInput(NativeId65NormalResourceRevision bundle, ReadOnlySpan<byte> upload)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (bundle.SourceImageSha256 != NativeId65EngineProfileContract.OriginalImageSha256 ||
            upload.Length != NativeId65NormalUploadTransferLayout.PairedUploadLength ||
            NativeId65NormalResourceSource.Hash(upload) != bundle.UploadSha256 ||
            !upload[NativeId65NormalUploadTransferLayout.VramLength..].SequenceEqual(_bank))
            throw new InvalidDataException("Normal staging requires an exact original-issued paired fresh bundle and its complete retained bank.");
        bundle.RequireExactUpload(upload);
    }

    internal static void ValidateSourceExecutable(ReadOnlySpan<byte> executable)
    {
        if (executable.Length != 0x66000 || NativeId65NormalResourceSource.Hash(executable) != ExecutableSha256)
            throw new InvalidDataException("Normal transfer consumers require the complete unchanged original USA executable.");
        foreach (var span in Consumers)
            NativeId65NormalResourceSource.RequireHash(executable.Slice(checked((int)span.Offset), span.ByteLength), span.Sha256, span.Name);
    }
}

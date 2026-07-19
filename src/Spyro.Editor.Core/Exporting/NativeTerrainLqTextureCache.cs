using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

public sealed class NativeTerrainLqTextureDescriptorPayload
{
    internal NativeTerrainLqTextureDescriptorPayload(
        int descriptorIndex,
        byte[] rawDescriptor,
        byte[] packedIndices,
        byte[] paletteWords)
    {
        DescriptorIndex = descriptorIndex;
        RawDescriptorBytes = rawDescriptor.ToArray();
        PackedIndexBytes = packedIndices.ToArray();
        PaletteWordBytes = paletteWords.ToArray();
        RawDescriptorSha256 = NativeTerrainLqTextureCacheCodec.Sha256Hex(RawDescriptorBytes);
        PackedIndicesSha256 = NativeTerrainLqTextureCacheCodec.Sha256Hex(PackedIndexBytes);
        PaletteWordsSha256 = NativeTerrainLqTextureCacheCodec.Sha256Hex(PaletteWordBytes);
    }

    public int DescriptorIndex { get; }
    public ReadOnlyMemory<byte> RawDescriptor => RawDescriptorBytes;
    public ReadOnlyMemory<byte> PackedIndices => PackedIndexBytes;
    public ReadOnlyMemory<byte> PaletteWords => PaletteWordBytes;
    public string RawDescriptorSha256 { get; }
    public string PackedIndicesSha256 { get; }
    public string PaletteWordsSha256 { get; }
    public int Orientation => (RawDescriptorBytes[7] >> 4) & 7;
    public int Abr => (RawDescriptorBytes[6] >> 5) & 3;

    internal byte[] RawDescriptorBytes { get; }
    internal byte[] PackedIndexBytes { get; }
    internal byte[] PaletteWordBytes { get; }

    public byte ReadPaletteIndex(int x, int y)
    {
        if (x is < 0 or >= NativeTerrainLqTextureCacheCodec.TextureSide ||
            y is < 0 or >= NativeTerrainLqTextureCacheCodec.TextureSide)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "TexLq coordinates must be inside the 32x32 logical image.");
        }

        int packedOffset = (y * NativeTerrainLqTextureCacheCodec.PackedIndexRowBytes) + (x >> 1);
        byte packed = PackedIndexBytes[packedOffset];
        return (byte)((packed >> ((x & 1) * 4)) & 0x0F);
    }

    public ushort ReadPaletteWord(int paletteRow, int paletteIndex)
    {
        if (paletteRow is < 0 or >= NativeTerrainLqTextureCacheCodec.PaletteRowCount)
            throw new ArgumentOutOfRangeException(nameof(paletteRow));
        if (paletteIndex is < 0 or >= NativeTerrainLqTextureCacheCodec.PaletteColorCount)
            throw new ArgumentOutOfRangeException(nameof(paletteIndex));

        int offset = ((paletteRow * NativeTerrainLqTextureCacheCodec.PaletteColorCount) + paletteIndex) * 2;
        return BinaryPrimitives.ReadUInt16LittleEndian(PaletteWordBytes.AsSpan(offset, 2));
    }

    public Rgba32[] MaterializeRgba(int paletteRow)
    {
        if (paletteRow is < 0 or >= NativeTerrainLqTextureCacheCodec.PaletteRowCount)
            throw new ArgumentOutOfRangeException(nameof(paletteRow));

        Rgba32[] pixels = new Rgba32[NativeTerrainLqTextureCacheCodec.TextureSide * NativeTerrainLqTextureCacheCodec.TextureSide];
        for (int y = 0; y < NativeTerrainLqTextureCacheCodec.TextureSide; y++)
        {
            for (int x = 0; x < NativeTerrainLqTextureCacheCodec.TextureSide; x++)
            {
                ushort word = ReadPaletteWord(paletteRow, ReadPaletteIndex(x, y));
                pixels[(y * NativeTerrainLqTextureCacheCodec.TextureSide) + x] = ConvertPsx555(word);
            }
        }
        return pixels;
    }

    private static Rgba32 ConvertPsx555(ushort word)
    {
        static byte Expand(int value) => (byte)((value << 3) | (value >> 2));
        return new Rgba32(
            Expand(word & 0x1F),
            Expand((word >> 5) & 0x1F),
            Expand((word >> 10) & 0x1F),
            word == 0 ? (byte)0 : (byte)255);
    }
}

public sealed class NativeTerrainLqTextureRecordPayload
{
    internal NativeTerrainLqTextureRecordPayload(
        int textureId,
        IReadOnlyList<NativeTerrainLqTextureDescriptorPayload> descriptors)
    {
        TextureId = textureId;
        Descriptors = descriptors.OrderBy(descriptor => descriptor.DescriptorIndex).ToArray();
    }

    public int TextureId { get; }
    public IReadOnlyList<NativeTerrainLqTextureDescriptorPayload> Descriptors { get; }

    public NativeTerrainLqTextureDescriptorPayload GetDescriptor(int descriptorIndex)
    {
        if (descriptorIndex is < 0 or >= NativeTerrainLqTextureCacheCodec.DescriptorCount)
            throw new ArgumentOutOfRangeException(nameof(descriptorIndex));
        return Descriptors.Single(descriptor => descriptor.DescriptorIndex == descriptorIndex);
    }
}

public sealed class NativeTerrainLqTextureSet
{
    private readonly IReadOnlyDictionary<int, NativeTerrainLqTextureRecordPayload> _texturesById;

    internal NativeTerrainLqTextureSet(
        int nativeTextureCount,
        int texturePagesByteLength,
        string texturePagesSha256,
        string originalTextureComponentSha256,
        string initializedLqTableSha256,
        int retailAliasCount,
        IReadOnlyList<NativeTerrainLqTextureRecordPayload> textures)
    {
        NativeTextureCount = nativeTextureCount;
        TexturePagesByteLength = texturePagesByteLength;
        TexturePagesSha256 = texturePagesSha256;
        OriginalTextureComponentSha256 = originalTextureComponentSha256;
        InitializedLqTableSha256 = initializedLqTableSha256;
        RetailAliasCount = retailAliasCount;
        Textures = textures.OrderBy(texture => texture.TextureId).ToArray();
        _texturesById = Textures.ToDictionary(texture => texture.TextureId);
    }

    public int NativeTextureCount { get; }
    public int TexturePagesByteLength { get; }
    public string TexturePagesSha256 { get; }
    public string OriginalTextureComponentSha256 { get; }
    public string InitializedLqTableSha256 { get; }
    public int RetailAliasCount { get; }
    public bool RetailAliasComplete => RetailAliasCount == NativeTextureCount;
    public IReadOnlyList<NativeTerrainLqTextureRecordPayload> Textures { get; }
    public int TextureCount => Textures.Count;

    public bool TryGetTexture(int textureId, out NativeTerrainLqTextureRecordPayload texture)
    {
        if (_texturesById.TryGetValue(textureId, out NativeTerrainLqTextureRecordPayload? found))
        {
            texture = found;
            return true;
        }

        texture = null!;
        return false;
    }
}

public sealed record NativeTerrainLqTextureDecodeResult(
    bool Complete,
    NativeTerrainLqTextureSet? TextureSet,
    int RequestedTextureCount,
    int DecodedTextureCount,
    IReadOnlyList<string> SafetyBlockers);

public sealed record NativeTerrainLqTextureCacheProof(
    int PayloadFormatVersion,
    int RecordSize,
    long PayloadByteLength,
    string PayloadSha256,
    int NativeTextureCount,
    int RecordCount,
    int TexturePagesByteLength,
    string TexturePagesSha256,
    string OriginalTextureComponentSha256,
    string InitializedLqTableSha256,
    int RetailAliasCount);

public sealed record NativeTerrainLqTextureCacheWriteResult(
    string Path,
    long ByteLength,
    string Sha256,
    int RecordCount);

public sealed record NativeTerrainLqTextureCacheReadResult(
    bool Complete,
    NativeTerrainLqTextureSet? TextureSet,
    IReadOnlyList<string> SafetyBlockers);

/// <summary>
/// Decodes and persists Spyro 1's two 32x32, 4-bpp TexLq descriptors per
/// terrain texture. The payload retains logical palette indexes plus all sixteen
/// raw PSX555/STP palette rows; it represents native load/default state only.
/// </summary>
public static class NativeTerrainLqTextureCacheCodec
{
    public const int PayloadFormatVersion = 1;
    public const int HeaderSize = 160;
    public const int DescriptorCount = 2;
    public const int TextureSide = 32;
    public const int BitsPerPixel = 4;
    public const int PackedIndexRowBytes = TextureSide / 2;
    public const int PackedIndexByteCount = PackedIndexRowBytes * TextureSide;
    public const int PaletteRowCount = 16;
    public const int PaletteColorCount = 16;
    public const int PaletteByteCount = PaletteRowCount * PaletteColorCount * 2;
    public const int DescriptorPayloadSize = 8 + PackedIndexByteCount + PaletteByteCount;
    public const int RecordSize = 4 + (DescriptorCount * DescriptorPayloadSize);
    public const string PayloadFileName = "lq-indexed-v1.bin";

    private const int LowDetailRecordBytes = 16;
    private const int HighDetailRecordBytes = 168;
    private const int PackedTexturePageRowBytes = 1024;
    private const int FullVramRightHalfByteX = 1024;
    private const int TexturePageMaxRows = 512;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("S1LQIDX1");
    private static readonly int[][] DescriptorMatrices =
    [
        [ 1,  0,  0,  1],
        [ 0,  1,  1,  0],
        [-1,  0,  0, -1],
        [ 0, -1,  1,  0],
        [ 0,  1,  1,  0],
        [-1,  0,  0,  1],
        [ 0, -1, -1,  0],
        [ 1,  0,  0, -1]
    ];

    public static NativeTerrainLqTextureDecodeResult DecodeNativeLoadState(
        NativeTerrainTextureInitialStateResult initialState,
        ReadOnlySpan<byte> texturePages,
        IEnumerable<int> requestedTextureIds)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(requestedTextureIds);
        List<string> blockers = [];
        int textureCount = initialState.TextureCount;
        int expectedTextureListSize = textureCount > 0
            ? checked(8 + (textureCount * (LowDetailRecordBytes + HighDetailRecordBytes)))
            : 0;
        int expectedHighDetailOffset = textureCount > 0
            ? checked(8 + (textureCount * LowDetailRecordBytes))
            : 0;

        if (!initialState.Complete)
            blockers.Add(initialState.SafetyBlockers.FirstOrDefault() ?? "Native terrain texture initialization is incomplete.");
        if (textureCount <= 0 || textureCount > 128 ||
            initialState.TextureListSize != expectedTextureListSize ||
            initialState.LowDetailTableOffset != 8 ||
            initialState.HighDetailTableOffset != expectedHighDetailOffset ||
            initialState.OriginalTextureData.Length < expectedTextureListSize ||
            initialState.InitializedTextureData.Length < expectedTextureListSize)
        {
            blockers.Add("The initialized native texture component does not have the exact 16-byte LQ plus 168-byte HQ record layout.");
        }
        if (texturePages.Length <= 0 || texturePages.Length > int.MaxValue)
            blockers.Add("The native texture-pages payload is empty or exceeds the supported size.");

        int[] requested = requestedTextureIds.Distinct().Order().ToArray();
        int[] invalidRequested = requested.Where(textureId => textureId < 0 || textureId >= textureCount).ToArray();
        if (invalidRequested.Length > 0)
            blockers.Add($"Requested TexLq ids are outside the native table: {string.Join(", ", invalidRequested)}.");
        if (blockers.Count > 0)
            return new NativeTerrainLqTextureDecodeResult(false, null, requested.Length, 0, blockers);

        ReadOnlySpan<byte> initialized = initialState.InitializedTextureData.AsSpan(0, expectedTextureListSize);
        int aliasCount = 0;
        for (int textureId = 0; textureId < textureCount; textureId++)
        {
            int lowOffset = 8 + (textureId * LowDetailRecordBytes);
            int highOffset = expectedHighDetailOffset + (textureId * HighDetailRecordBytes);
            ReadOnlySpan<byte> first = initialized.Slice(lowOffset, 8);
            if (first.SequenceEqual(initialized.Slice(lowOffset + 8, 8)) &&
                first.SequenceEqual(initialized.Slice(highOffset, 8)))
            {
                aliasCount++;
            }
        }
        if (aliasCount != textureCount)
            blockers.Add($"Only {aliasCount}/{textureCount} initialized texture records preserve the required LQ0/LQ1/HQ-leading alias.");

        List<NativeTerrainLqTextureRecordPayload> textures = [];
        foreach (int textureId in requested.Where(textureId => textureId >= 0 && textureId < textureCount))
        {
            int lowOffset = 8 + (textureId * LowDetailRecordBytes);
            List<NativeTerrainLqTextureDescriptorPayload> descriptors = [];
            for (int descriptorIndex = 0; descriptorIndex < DescriptorCount; descriptorIndex++)
            {
                byte[] raw = initialized.Slice(lowOffset + (descriptorIndex * 8), 8).ToArray();
                if (!TryDecodeDescriptor(
                        texturePages,
                        raw,
                        descriptorIndex,
                        out NativeTerrainLqTextureDescriptorPayload? descriptor,
                        out string failure))
                {
                    blockers.Add($"Texture {textureId} LQ descriptor {descriptorIndex}: {failure}");
                    continue;
                }
                descriptors.Add(descriptor!);
            }

            if (descriptors.Count == DescriptorCount)
                textures.Add(new NativeTerrainLqTextureRecordPayload(textureId, descriptors));
        }

        if (textures.Count != requested.Length)
            blockers.Add($"Decoded {textures.Count}/{requested.Length} requested TexLq records.");
        if (blockers.Count > 0)
            return new NativeTerrainLqTextureDecodeResult(false, null, requested.Length, textures.Count, blockers);

        string texturePagesSha256 = Sha256Hex(texturePages);
        string originalTextureComponentSha256 = Sha256Hex(initialState.OriginalTextureData.AsSpan(0, expectedTextureListSize));
        string initializedLqTableSha256 = Sha256Hex(initialized.Slice(8, textureCount * LowDetailRecordBytes));
        NativeTerrainLqTextureSet set = new(
            textureCount,
            texturePages.Length,
            texturePagesSha256,
            originalTextureComponentSha256,
            initializedLqTableSha256,
            aliasCount,
            textures);
        return new NativeTerrainLqTextureDecodeResult(true, set, requested.Length, textures.Count, []);
    }

    public static async Task<NativeTerrainLqTextureCacheWriteResult> WriteAsync(
        string path,
        NativeTerrainLqTextureSet textureSet,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(textureSet);
        ValidateSetForWrite(textureSet);

        byte[] bytes = new byte[checked(HeaderSize + (textureSet.TextureCount * RecordSize))];
        Magic.CopyTo(bytes, 0);
        WriteInt32(bytes, 8, PayloadFormatVersion);
        WriteInt32(bytes, 12, HeaderSize);
        WriteInt32(bytes, 16, RecordSize);
        WriteInt32(bytes, 20, textureSet.TextureCount);
        WriteInt32(bytes, 24, textureSet.NativeTextureCount);
        WriteInt32(bytes, 28, DescriptorCount);
        WriteInt32(bytes, 32, TextureSide);
        WriteInt32(bytes, 36, BitsPerPixel);
        WriteInt32(bytes, 40, PaletteRowCount);
        WriteInt32(bytes, 44, PaletteColorCount);
        WriteInt32(bytes, 48, PackedIndexByteCount);
        WriteInt32(bytes, 52, textureSet.TexturePagesByteLength);
        WriteInt32(bytes, 56, textureSet.RetailAliasCount);
        WriteHash(bytes, 64, textureSet.TexturePagesSha256);
        WriteHash(bytes, 96, textureSet.OriginalTextureComponentSha256);
        WriteHash(bytes, 128, textureSet.InitializedLqTableSha256);

        int offset = HeaderSize;
        foreach (NativeTerrainLqTextureRecordPayload texture in textureSet.Textures.OrderBy(texture => texture.TextureId))
        {
            WriteInt32(bytes, offset, texture.TextureId);
            offset += 4;
            foreach (NativeTerrainLqTextureDescriptorPayload descriptor in texture.Descriptors.OrderBy(descriptor => descriptor.DescriptorIndex))
            {
                descriptor.RawDescriptorBytes.CopyTo(bytes, offset);
                offset += 8;
                descriptor.PackedIndexBytes.CopyTo(bytes, offset);
                offset += PackedIndexByteCount;
                descriptor.PaletteWordBytes.CopyTo(bytes, offset);
                offset += PaletteByteCount;
            }
        }
        if (offset != bytes.Length)
            throw new InvalidDataException("TexLq sidecar serialization did not consume its exact fixed payload length.");

        string directory = Path.GetDirectoryName(path) ?? ".";
        Directory.CreateDirectory(directory);
        string temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        return new NativeTerrainLqTextureCacheWriteResult(
            path,
            bytes.LongLength,
            Sha256Hex(bytes),
            textureSet.TextureCount);
    }

    public static NativeTerrainLqTextureCacheReadResult ReadValidated(
        string path,
        NativeTerrainLqTextureCacheProof proof)
    {
        ArgumentNullException.ThrowIfNull(proof);
        List<string> blockers = [];
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return Failure("The TexLq sidecar file does not exist.");
        if (proof.PayloadFormatVersion != PayloadFormatVersion || proof.RecordSize != RecordSize ||
            proof.PayloadByteLength != HeaderSize + ((long)proof.RecordCount * RecordSize) ||
            proof.RecordCount < 0 || proof.NativeTextureCount <= 0 || proof.RecordCount > proof.NativeTextureCount ||
            proof.TexturePagesByteLength <= 0 || proof.RetailAliasCount != proof.NativeTextureCount ||
            !IsSha256(proof.PayloadSha256) || !IsSha256(proof.TexturePagesSha256) ||
            !IsSha256(proof.OriginalTextureComponentSha256) || !IsSha256(proof.InitializedLqTableSha256))
        {
            return Failure("The TexLq manifest proof is incomplete or internally inconsistent.");
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.LongLength != proof.PayloadByteLength)
                blockers.Add($"TexLq sidecar length is {bytes.LongLength}, expected {proof.PayloadByteLength}.");
            if (!string.Equals(Sha256Hex(bytes), proof.PayloadSha256, StringComparison.OrdinalIgnoreCase))
                blockers.Add("TexLq sidecar SHA-256 does not match the manifest proof.");
            if (bytes.Length < HeaderSize || !bytes.AsSpan(0, Magic.Length).SequenceEqual(Magic))
                blockers.Add("TexLq sidecar magic/header is invalid.");
            if (blockers.Count > 0)
                return new NativeTerrainLqTextureCacheReadResult(false, null, blockers);

            AssertHeader(bytes, 8, PayloadFormatVersion, "format version", blockers);
            AssertHeader(bytes, 12, HeaderSize, "header size", blockers);
            AssertHeader(bytes, 16, RecordSize, "record size", blockers);
            AssertHeader(bytes, 20, proof.RecordCount, "record count", blockers);
            AssertHeader(bytes, 24, proof.NativeTextureCount, "native texture count", blockers);
            AssertHeader(bytes, 28, DescriptorCount, "descriptor count", blockers);
            AssertHeader(bytes, 32, TextureSide, "texture side", blockers);
            AssertHeader(bytes, 36, BitsPerPixel, "bits per pixel", blockers);
            AssertHeader(bytes, 40, PaletteRowCount, "palette row count", blockers);
            AssertHeader(bytes, 44, PaletteColorCount, "palette color count", blockers);
            AssertHeader(bytes, 48, PackedIndexByteCount, "packed index size", blockers);
            AssertHeader(bytes, 52, proof.TexturePagesByteLength, "texture-pages byte length", blockers);
            AssertHeader(bytes, 56, proof.RetailAliasCount, "retail alias count", blockers);
            AssertHash(bytes, 64, proof.TexturePagesSha256, "texture-pages", blockers);
            AssertHash(bytes, 96, proof.OriginalTextureComponentSha256, "original texture component", blockers);
            AssertHash(bytes, 128, proof.InitializedLqTableSha256, "initialized LQ table", blockers);
            if (blockers.Count > 0)
                return new NativeTerrainLqTextureCacheReadResult(false, null, blockers);

            List<NativeTerrainLqTextureRecordPayload> textures = [];
            int offset = HeaderSize;
            int previousTextureId = -1;
            for (int recordIndex = 0; recordIndex < proof.RecordCount; recordIndex++)
            {
                int textureId = ReadInt32(bytes, offset);
                offset += 4;
                if (textureId <= previousTextureId || textureId < 0 || textureId >= proof.NativeTextureCount)
                    blockers.Add($"TexLq record {recordIndex} has duplicate, unordered, or out-of-range texture id {textureId}.");
                previousTextureId = textureId;

                List<NativeTerrainLqTextureDescriptorPayload> descriptors = [];
                for (int descriptorIndex = 0; descriptorIndex < DescriptorCount; descriptorIndex++)
                {
                    byte[] raw = bytes.AsSpan(offset, 8).ToArray();
                    offset += 8;
                    byte[] packed = bytes.AsSpan(offset, PackedIndexByteCount).ToArray();
                    offset += PackedIndexByteCount;
                    byte[] palettes = bytes.AsSpan(offset, PaletteByteCount).ToArray();
                    offset += PaletteByteCount;
                    if (!HasLqDescriptorShape(raw))
                        blockers.Add($"TexLq texture {textureId} descriptor {descriptorIndex} does not describe a 32x32 tile.");
                    descriptors.Add(new NativeTerrainLqTextureDescriptorPayload(descriptorIndex, raw, packed, palettes));
                }
                textures.Add(new NativeTerrainLqTextureRecordPayload(textureId, descriptors));
            }
            if (offset != bytes.Length)
                blockers.Add("TexLq sidecar has trailing or unconsumed bytes.");
            if (blockers.Count > 0)
                return new NativeTerrainLqTextureCacheReadResult(false, null, blockers);

            NativeTerrainLqTextureSet set = new(
                proof.NativeTextureCount,
                proof.TexturePagesByteLength,
                proof.TexturePagesSha256.ToUpperInvariant(),
                proof.OriginalTextureComponentSha256.ToUpperInvariant(),
                proof.InitializedLqTableSha256.ToUpperInvariant(),
                proof.RetailAliasCount,
                textures);
            return new NativeTerrainLqTextureCacheReadResult(true, set, []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or OverflowException)
        {
            return Failure($"Could not read the TexLq sidecar: {ex.Message}");
        }

        NativeTerrainLqTextureCacheReadResult Failure(string message) =>
            new(false, null, [message]);
    }

    internal static string Sha256Hex(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private static bool TryDecodeDescriptor(
        ReadOnlySpan<byte> texturePages,
        byte[] raw,
        int descriptorIndex,
        out NativeTerrainLqTextureDescriptorPayload? descriptor,
        out string failure)
    {
        descriptor = null;
        failure = "";
        if (!HasLqDescriptorShape(raw))
        {
            failure = "descriptor does not encode a 32x32 TexLq tile";
            return false;
        }

        int region = raw[6];
        int orientation = (raw[7] >> 4) & 7;
        int[] matrix = DescriptorMatrices[orientation];
        int startTexelX = 2048 + ((region * 256) % 2048) + raw[0];
        int startY = ((region & 0x10) != 0 ? 256 : 0) + raw[1];
        if (matrix[0] < 0 || matrix[1] < 0)
            startTexelX += TextureSide - 1;
        if (matrix[2] < 0 || matrix[3] < 0)
            startY += TextureSide - 1;

        byte[] packedIndices = new byte[PackedIndexByteCount];
        for (int y = 0; y < TextureSide; y++)
        {
            for (int x = 0; x < TextureSide; x++)
            {
                int sourceTexelX = startTexelX + (x * matrix[0]) + (y * matrix[1]);
                int sourceY = startY + (x * matrix[2]) + (y * matrix[3]);
                int sourcePackedX = (sourceTexelX >> 1) - FullVramRightHalfByteX;
                long sourceOffset = (sourceY * (long)PackedTexturePageRowBytes) + sourcePackedX;
                if (sourcePackedX < 0 || sourcePackedX >= PackedTexturePageRowBytes ||
                    sourceY < 0 || sourceY >= TexturePageMaxRows ||
                    sourceOffset < 0 || sourceOffset >= texturePages.Length)
                {
                    failure = $"pixel ({x},{y}) maps outside the loaded right-half VRAM payload";
                    return false;
                }

                int index = (texturePages[(int)sourceOffset] >> ((sourceTexelX & 1) * 4)) & 0x0F;
                int destinationOffset = (y * PackedIndexRowBytes) + (x >> 1);
                if ((x & 1) == 0)
                    packedIndices[destinationOffset] = (byte)((packedIndices[destinationOffset] & 0xF0) | index);
                else
                    packedIndices[destinationOffset] = (byte)((packedIndices[destinationOffset] & 0x0F) | (index << 4));
            }
        }

        ushort clutCode = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(2, 2));
        int clutXWord = (clutCode & 0x3F) * 16;
        int baseClutY = (clutCode >> 6) & 0x1FF;
        int palettePackedX = (clutXWord * 2) - FullVramRightHalfByteX;
        byte[] paletteWords = new byte[PaletteByteCount];
        for (int row = 0; row < PaletteRowCount; row++)
        {
            int sourceY = baseClutY + row;
            long sourceOffset = (sourceY * (long)PackedTexturePageRowBytes) + palettePackedX;
            if (palettePackedX < 0 || palettePackedX + (PaletteColorCount * 2) > PackedTexturePageRowBytes ||
                sourceY < 0 || sourceY >= TexturePageMaxRows ||
                sourceOffset < 0 || sourceOffset + (PaletteColorCount * 2) > texturePages.Length)
            {
                failure = $"palette row {row} maps outside the loaded right-half VRAM payload";
                return false;
            }
            texturePages.Slice((int)sourceOffset, PaletteColorCount * 2)
                .CopyTo(paletteWords.AsSpan(row * PaletteColorCount * 2));
        }

        descriptor = new NativeTerrainLqTextureDescriptorPayload(descriptorIndex, raw, packedIndices, paletteWords);
        return true;
    }

    private static bool HasLqDescriptorShape(ReadOnlySpan<byte> raw) =>
        raw.Length == 8 &&
        Math.Max(Math.Abs(raw[4] - raw[0]), Math.Abs(raw[5] - raw[1])) + 1 == TextureSide;

    private static void ValidateSetForWrite(NativeTerrainLqTextureSet textureSet)
    {
        if (textureSet.NativeTextureCount <= 0 || textureSet.NativeTextureCount > 128 ||
            textureSet.TextureCount > textureSet.NativeTextureCount ||
            textureSet.TexturePagesByteLength <= 0 || !textureSet.RetailAliasComplete ||
            !IsSha256(textureSet.TexturePagesSha256) || !IsSha256(textureSet.OriginalTextureComponentSha256) ||
            !IsSha256(textureSet.InitializedLqTableSha256))
        {
            throw new InvalidDataException("The TexLq set does not contain complete native source provenance.");
        }

        int previous = -1;
        foreach (NativeTerrainLqTextureRecordPayload texture in textureSet.Textures)
        {
            if (texture.TextureId <= previous || texture.TextureId < 0 || texture.TextureId >= textureSet.NativeTextureCount ||
                texture.Descriptors.Count != DescriptorCount)
            {
                throw new InvalidDataException("TexLq textures must be unique, ordered, in range, and contain exactly two descriptors.");
            }
            previous = texture.TextureId;
            foreach (NativeTerrainLqTextureDescriptorPayload descriptor in texture.Descriptors)
            {
                if (descriptor.RawDescriptorBytes.Length != 8 || !HasLqDescriptorShape(descriptor.RawDescriptorBytes) ||
                    descriptor.PackedIndexBytes.Length != PackedIndexByteCount ||
                    descriptor.PaletteWordBytes.Length != PaletteByteCount)
                {
                    throw new InvalidDataException($"Texture {texture.TextureId} has an incomplete TexLq descriptor payload.");
                }
            }
        }
    }

    private static bool IsSha256(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length == 64 && value.All(Uri.IsHexDigit);

    private static int ReadInt32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));

    private static void WriteInt32(Span<byte> bytes, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(offset, 4), value);

    private static void WriteHash(Span<byte> bytes, int offset, string value) =>
        Convert.FromHexString(value).CopyTo(bytes.Slice(offset, 32));

    private static void AssertHeader(ReadOnlySpan<byte> bytes, int offset, int expected, string label, List<string> blockers)
    {
        int actual = ReadInt32(bytes, offset);
        if (actual != expected)
            blockers.Add($"TexLq sidecar {label} is {actual}, expected {expected}.");
    }

    private static void AssertHash(ReadOnlySpan<byte> bytes, int offset, string expected, string label, List<string> blockers)
    {
        string actual = Convert.ToHexString(bytes.Slice(offset, 32));
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            blockers.Add($"TexLq sidecar {label} SHA-256 provenance does not match its manifest.");
    }
}

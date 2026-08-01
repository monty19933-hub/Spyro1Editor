using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

public enum NativeTerrainHqMaterialTier : byte
{
    Normal = 0,
    Close = 1
}

public sealed class NativeTerrainHqMaterialDescriptorPayload
{
    internal NativeTerrainHqMaterialDescriptorPayload(
        NativeTerrainHqMaterialTier tier,
        int descriptorIndex,
        int side,
        int abr,
        byte[] rawDescriptor,
        byte[] rawWordBytes)
    {
        Tier = tier;
        DescriptorIndex = descriptorIndex;
        Side = side;
        Abr = abr;
        RawDescriptorBytes = rawDescriptor.ToArray();
        RawWordBytes = rawWordBytes.ToArray();
        BitsPerPixel = RawDescriptorBytes.Length == 8 && (RawDescriptorBytes[6] & 0x80) != 0 ? 8 : 4;
        RawDescriptorSha256 = NativeTerrainHqMaterialCacheCodec.Sha256Hex(RawDescriptorBytes);
        RawWordsSha256 = NativeTerrainHqMaterialCacheCodec.Sha256Hex(RawWordBytes);
        ZeroWordCount = CountWords(static word => word == 0);
        StpSetWordCount = CountWords(static word => (word & 0x8000) != 0);
    }

    public NativeTerrainHqMaterialTier Tier { get; }
    public string TierName => Tier == NativeTerrainHqMaterialTier.Normal ? "hqData" : "hqDataClose";
    public int DescriptorIndex { get; }
    public int Side { get; }
    public int Abr { get; }
    public int BitsPerPixel { get; }
    public int WordCount => RawWordBytes.Length / sizeof(ushort);
    public ReadOnlyMemory<byte> RawDescriptor => RawDescriptorBytes;
    public ReadOnlyMemory<byte> RawWordsLittleEndian => RawWordBytes;
    public string RawDescriptorSha256 { get; }
    public string RawWordsSha256 { get; }
    public int ZeroWordCount { get; }
    public int StpSetWordCount { get; }

    internal byte[] RawDescriptorBytes { get; }
    internal byte[] RawWordBytes { get; }

    public ushort ReadRawWord(int x, int y)
    {
        if (x < 0 || x >= Side || y < 0 || y >= Side)
            throw new ArgumentOutOfRangeException(nameof(x), $"HQ material coordinates must be inside the {Side}x{Side} descriptor tile.");
        int offset = checked(((y * Side) + x) * sizeof(ushort));
        return BinaryPrimitives.ReadUInt16LittleEndian(RawWordBytes.AsSpan(offset, sizeof(ushort)));
    }

    public ushort[] MaterializeRawWords()
    {
        ushort[] words = new ushort[WordCount];
        for (int index = 0; index < words.Length; index++)
            words[index] = BinaryPrimitives.ReadUInt16LittleEndian(RawWordBytes.AsSpan(index * sizeof(ushort), sizeof(ushort)));
        return words;
    }

    private int CountWords(Func<ushort, bool> predicate)
    {
        int count = 0;
        for (int offset = 0; offset < RawWordBytes.Length; offset += sizeof(ushort))
        {
            if (predicate(BinaryPrimitives.ReadUInt16LittleEndian(RawWordBytes.AsSpan(offset, sizeof(ushort)))))
                count++;
        }
        return count;
    }
}

public sealed class NativeTerrainHqMaterialTierPayload
{
    internal NativeTerrainHqMaterialTierPayload(
        NativeTerrainHqMaterialTier tier,
        IReadOnlyList<NativeTerrainHqMaterialDescriptorPayload> descriptors)
    {
        Tier = tier;
        Descriptors = descriptors.OrderBy(descriptor => descriptor.DescriptorIndex).ToArray();
        GridColumns = tier == NativeTerrainHqMaterialTier.Normal
            ? NativeTerrainHqMaterialCacheCodec.NormalGridColumns
            : NativeTerrainHqMaterialCacheCodec.CloseGridColumns;
        TileSide = Descriptors.Select(descriptor => descriptor.Side).Distinct().Single();
        CompositeSide = checked(GridColumns * TileSide);
        CompositeRawWordsSha256 = NativeTerrainHqMaterialCacheCodec.Sha256Hex(BuildCompositeRawWordBytes());
    }

    public NativeTerrainHqMaterialTier Tier { get; }
    public string TierName => Tier == NativeTerrainHqMaterialTier.Normal ? "hqData" : "hqDataClose";
    public int GridColumns { get; }
    public int TileSide { get; }
    public int CompositeSide { get; }
    public IReadOnlyList<NativeTerrainHqMaterialDescriptorPayload> Descriptors { get; }
    public int DescriptorCount => Descriptors.Count;
    public int RawWordCount => Descriptors.Sum(descriptor => descriptor.WordCount);
    public int ZeroWordCount => Descriptors.Sum(descriptor => descriptor.ZeroWordCount);
    public int StpSetWordCount => Descriptors.Sum(descriptor => descriptor.StpSetWordCount);
    public string CompositeRawWordsSha256 { get; }

    public NativeTerrainHqMaterialDescriptorPayload GetDescriptor(int descriptorIndex) =>
        Descriptors.Single(descriptor => descriptor.DescriptorIndex == descriptorIndex);

    public ushort[] MaterializeCompositeRawWords()
    {
        byte[] bytes = BuildCompositeRawWordBytes();
        ushort[] words = new ushort[bytes.Length / sizeof(ushort)];
        for (int index = 0; index < words.Length; index++)
            words[index] = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(index * sizeof(ushort), sizeof(ushort)));
        return words;
    }

    private byte[] BuildCompositeRawWordBytes()
    {
        byte[] result = new byte[checked(CompositeSide * CompositeSide * sizeof(ushort))];
        foreach (NativeTerrainHqMaterialDescriptorPayload descriptor in Descriptors)
        {
            int tileX = descriptor.DescriptorIndex % GridColumns;
            int tileY = descriptor.DescriptorIndex / GridColumns;
            for (int y = 0; y < TileSide; y++)
            {
                int sourceOffset = y * TileSide * sizeof(ushort);
                int destinationOffset = checked((((tileY * TileSide) + y) * CompositeSide + (tileX * TileSide)) * sizeof(ushort));
                descriptor.RawWordBytes.AsSpan(sourceOffset, TileSide * sizeof(ushort))
                    .CopyTo(result.AsSpan(destinationOffset, TileSide * sizeof(ushort)));
            }
        }
        return result;
    }
}

public sealed class NativeTerrainHqMaterialRecordPayload
{
    private readonly IReadOnlyList<NativeTerrainHqMaterialDescriptorPayload> _descriptors;

    internal NativeTerrainHqMaterialRecordPayload(
        int textureId,
        NativeTerrainHqMaterialTierPayload normal,
        NativeTerrainHqMaterialTierPayload close)
    {
        TextureId = textureId;
        Normal = normal;
        Close = close;
        _descriptors = Normal.Descriptors.Concat(Close.Descriptors).ToArray();
    }

    public int TextureId { get; }
    public NativeTerrainHqMaterialTierPayload Normal { get; }
    public NativeTerrainHqMaterialTierPayload Close { get; }
    public IReadOnlyList<NativeTerrainHqMaterialDescriptorPayload> Descriptors => _descriptors;

    public NativeTerrainHqMaterialTierPayload GetTier(NativeTerrainHqMaterialTier tier) => tier switch
    {
        NativeTerrainHqMaterialTier.Normal => Normal,
        NativeTerrainHqMaterialTier.Close => Close,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown native HQ material tier.")
    };
}

public sealed class NativeTerrainHqMaterialSet
{
    private readonly IReadOnlyDictionary<int, NativeTerrainHqMaterialRecordPayload> _texturesById;

    internal NativeTerrainHqMaterialSet(
        int nativeTextureCount,
        int texturePagesByteLength,
        int textureComponentByteLength,
        string texturePagesSha256,
        string originalTextureComponentSha256,
        string initializedTextureComponentSha256,
        string initializedHqTableSha256,
        IReadOnlyList<NativeTerrainHqMaterialRecordPayload> textures)
    {
        NativeTextureCount = nativeTextureCount;
        TexturePagesByteLength = texturePagesByteLength;
        TextureComponentByteLength = textureComponentByteLength;
        TexturePagesSha256 = texturePagesSha256;
        OriginalTextureComponentSha256 = originalTextureComponentSha256;
        InitializedTextureComponentSha256 = initializedTextureComponentSha256;
        InitializedHqTableSha256 = initializedHqTableSha256;
        Textures = textures.OrderBy(texture => texture.TextureId).ToArray();
        _texturesById = Textures.ToDictionary(texture => texture.TextureId);
        TotalDescriptorCount = Textures.Sum(texture => texture.Normal.DescriptorCount + texture.Close.DescriptorCount);
        CompositeCount = checked(Textures.Count * 2);
        RawWordCount = Textures.Sum(texture => texture.Normal.RawWordCount + texture.Close.RawWordCount);
        ZeroWordCount = Textures.Sum(texture => texture.Normal.ZeroWordCount + texture.Close.ZeroWordCount);
        StpSetWordCount = Textures.Sum(texture => texture.Normal.StpSetWordCount + texture.Close.StpSetWordCount);
        AbrDescriptorCounts = Enumerable.Range(0, 4)
            .Select(abr => Textures.Sum(texture => texture.Descriptors.Count(descriptor => descriptor.Abr == abr)))
            .ToArray();
        BitsPerPixelDescriptorCounts = new[] { 4, 8 }
            .Select(bitsPerPixel => Textures.Sum(texture => texture.Descriptors.Count(descriptor => descriptor.BitsPerPixel == bitsPerPixel)))
            .ToArray();
        ContentSha256 = NativeTerrainHqMaterialCacheCodec.ComputeContentSha256(this);
    }

    public int NativeTextureCount { get; }
    public int TexturePagesByteLength { get; }
    public int TextureComponentByteLength { get; }
    public string TexturePagesSha256 { get; }
    public string OriginalTextureComponentSha256 { get; }
    public string InitializedTextureComponentSha256 { get; }
    public string InitializedHqTableSha256 { get; }
    public IReadOnlyList<NativeTerrainHqMaterialRecordPayload> Textures { get; }
    public int TextureCount => Textures.Count;
    public int TotalDescriptorCount { get; }
    public int CompositeCount { get; }
    public int RawWordCount { get; }
    public int ZeroWordCount { get; }
    public int StpSetWordCount { get; }
    public IReadOnlyList<int> AbrDescriptorCounts { get; }
    public IReadOnlyList<int> BitsPerPixelDescriptorCounts { get; }
    /// <summary>
    /// Stable material/provenance fingerprint, independent of the sidecar file
    /// path and suitable for viewport frame-cache keys.
    /// </summary>
    public string ContentSha256 { get; }

    public bool TryGetTexture(int textureId, out NativeTerrainHqMaterialRecordPayload texture)
    {
        if (_texturesById.TryGetValue(textureId, out NativeTerrainHqMaterialRecordPayload? found))
        {
            texture = found;
            return true;
        }
        texture = null!;
        return false;
    }
}

public sealed record NativeTerrainHqMaterialDecodeResult(
    bool Complete,
    NativeTerrainHqMaterialSet? MaterialSet,
    int RequestedTextureCount,
    int DecodedTextureCount,
    IReadOnlyList<string> SafetyBlockers);

public sealed record NativeTerrainHqMaterialCacheProof(
    int PayloadFormatVersion,
    int HeaderSize,
    int DescriptorHeaderSize,
    long PayloadByteLength,
    string PayloadSha256,
    int NativeTextureCount,
    int RecordCount,
    int TotalDescriptorCount,
    int CompositeCount,
    int RawWordCount,
    int ZeroWordCount,
    int StpSetWordCount,
    int TexturePagesByteLength,
    int TextureComponentByteLength,
    string TexturePagesSha256,
    string OriginalTextureComponentSha256,
    string InitializedTextureComponentSha256,
    string InitializedHqTableSha256,
    string ContentSha256);

public sealed record NativeTerrainHqMaterialCacheWriteResult(
    string Path,
    long ByteLength,
    string Sha256,
    int RecordCount,
    int CompositeCount,
    int DescriptorCount,
    int RawWordCount,
    int ZeroWordCount,
    int StpSetWordCount);

public sealed record NativeTerrainHqMaterialCacheReadResult(
    bool Complete,
    NativeTerrainHqMaterialSet? MaterialSet,
    IReadOnlyList<string> SafetyBlockers);

/// <summary>
/// Preserves native-load-initialized normal and close HQ terrain materials as
/// descriptor-local ABR plus exact logical PSX555/STP words. TexLq remains in
/// the separate indexed sidecar so its sixteen distance palettes are not
/// expanded into redundant frames.
/// </summary>
public static class NativeTerrainHqMaterialCacheCodec
{
    public const string MaterialFormat = "psx555-stp-abr-hq-v1";
    public const string PayloadFileName = "hq-materials-v1.bin";
    public const int PayloadFormatVersion = 1;
    public const int HeaderSize = 256;
    public const int RecordHeaderSize = 8;
    public const int DescriptorHeaderSize = 20;
    public const int NormalDescriptorCount = 4;
    public const int CloseDescriptorCount = 16;
    public const int DescriptorCountPerTexture = NormalDescriptorCount + CloseDescriptorCount;
    public const int NormalGridColumns = 2;
    public const int CloseGridColumns = 4;
    public const int RawWordByteCount = sizeof(ushort);

    private const int LowDetailRecordBytes = 16;
    private const int HighDetailRecordBytes = 168;
    private const int PackedTexturePageRowBytes = 1024;
    private const int FullVramTextureByteX = 1024;
    private const int TexturePageMaxRows = 512;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("S1HQMAT1");
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

    public static NativeTerrainHqMaterialDecodeResult DecodeNativeLoadState(
        NativeTerrainTextureInitialStateResult initialState,
        ReadOnlySpan<byte> texturePages,
        IEnumerable<int> requestedTextureIds)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(requestedTextureIds);
        List<string> blockers = [];
        int textureCount = initialState.TextureCount;
        int highDetailOffset = checked(8 + (textureCount * LowDetailRecordBytes));
        int textureComponentByteLength = checked(highDetailOffset + (textureCount * HighDetailRecordBytes));
        if (!initialState.Complete)
            blockers.Add("The native texture load-state initialization is incomplete.");
        if (textureCount <= 0 || textureCount > 128)
            blockers.Add($"The native texture count {textureCount} is outside the supported retail range.");
        if (initialState.OriginalTextureData.Length < textureComponentByteLength ||
            initialState.InitializedTextureData.Length < textureComponentByteLength)
        {
            blockers.Add("The initialized native texture component does not contain the exact LQ plus 168-byte HQ tables.");
        }
        if (texturePages.Length <= 0)
            blockers.Add("The native texture-pages payload is empty.");

        int[] requested = requestedTextureIds.Distinct().Order().ToArray();
        int[] invalid = requested.Where(textureId => textureId < 0 || textureId >= textureCount).ToArray();
        if (invalid.Length > 0)
            blockers.Add($"Requested HQ material ids are outside the native table: {string.Join(", ", invalid)}.");
        if (blockers.Count > 0)
            return new NativeTerrainHqMaterialDecodeResult(false, null, requested.Length, 0, blockers);

        ReadOnlySpan<byte> initialized = initialState.InitializedTextureData.AsSpan(0, textureComponentByteLength);
        List<NativeTerrainHqMaterialRecordPayload> textures = [];
        foreach (int textureId in requested)
        {
            int recordOffset = checked(highDetailOffset + (textureId * HighDetailRecordBytes));
            List<NativeTerrainHqMaterialDescriptorPayload> normal = DecodeTier(
                texturePages,
                initialized,
                textureId,
                recordOffset + 8,
                NativeTerrainHqMaterialTier.Normal,
                NormalDescriptorCount,
                blockers);
            List<NativeTerrainHqMaterialDescriptorPayload> close = DecodeTier(
                texturePages,
                initialized,
                textureId,
                recordOffset + 40,
                NativeTerrainHqMaterialTier.Close,
                CloseDescriptorCount,
                blockers);
            if (normal.Count == NormalDescriptorCount && close.Count == CloseDescriptorCount &&
                normal.Select(descriptor => descriptor.Side).Distinct().Count() == 1 &&
                close.Select(descriptor => descriptor.Side).Distinct().Count() == 1)
            {
                textures.Add(new NativeTerrainHqMaterialRecordPayload(
                    textureId,
                    new NativeTerrainHqMaterialTierPayload(NativeTerrainHqMaterialTier.Normal, normal),
                    new NativeTerrainHqMaterialTierPayload(NativeTerrainHqMaterialTier.Close, close)));
            }
            else if (normal.Count == NormalDescriptorCount && normal.Select(descriptor => descriptor.Side).Distinct().Count() != 1)
            {
                blockers.Add($"Texture {textureId} normal HQ descriptors do not share one logical tile side.");
            }
            else if (close.Count == CloseDescriptorCount && close.Select(descriptor => descriptor.Side).Distinct().Count() != 1)
            {
                blockers.Add($"Texture {textureId} close HQ descriptors do not share one logical tile side.");
            }
        }

        if (textures.Count != requested.Length)
            blockers.Add($"Decoded {textures.Count}/{requested.Length} requested HQ material records.");
        if (blockers.Count > 0)
            return new NativeTerrainHqMaterialDecodeResult(false, null, requested.Length, textures.Count, blockers);

        NativeTerrainHqMaterialSet set = new(
            textureCount,
            texturePages.Length,
            textureComponentByteLength,
            Sha256Hex(texturePages),
            Sha256Hex(initialState.OriginalTextureData.AsSpan(0, textureComponentByteLength)),
            Sha256Hex(initialized),
            Sha256Hex(initialized.Slice(highDetailOffset, textureCount * HighDetailRecordBytes)),
            textures);
        return new NativeTerrainHqMaterialDecodeResult(true, set, requested.Length, textures.Count, []);
    }

    public static async Task<NativeTerrainHqMaterialCacheWriteResult> WriteAsync(
        string path,
        NativeTerrainHqMaterialSet materialSet,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(materialSet);
        ValidateSetForWrite(materialSet);

        int byteLength = checked(HeaderSize + materialSet.Textures.Sum(GetSerializedRecordByteLength));
        byte[] bytes = new byte[byteLength];
        Magic.CopyTo(bytes, 0);
        WriteInt32(bytes, 8, PayloadFormatVersion);
        WriteInt32(bytes, 12, HeaderSize);
        WriteInt32(bytes, 16, DescriptorHeaderSize);
        WriteInt32(bytes, 20, materialSet.TextureCount);
        WriteInt32(bytes, 24, materialSet.NativeTextureCount);
        WriteInt32(bytes, 28, NormalDescriptorCount);
        WriteInt32(bytes, 32, CloseDescriptorCount);
        WriteInt32(bytes, 36, RawWordByteCount);
        WriteInt32(bytes, 40, materialSet.TexturePagesByteLength);
        WriteInt32(bytes, 44, materialSet.TextureComponentByteLength);
        WriteInt32(bytes, 48, materialSet.TotalDescriptorCount);
        WriteInt32(bytes, 52, materialSet.CompositeCount);
        WriteInt32(bytes, 56, materialSet.RawWordCount);
        WriteInt32(bytes, 60, materialSet.ZeroWordCount);
        WriteInt32(bytes, 64, materialSet.StpSetWordCount);
        WriteHash(bytes, 96, materialSet.TexturePagesSha256);
        WriteHash(bytes, 128, materialSet.OriginalTextureComponentSha256);
        WriteHash(bytes, 160, materialSet.InitializedTextureComponentSha256);
        WriteHash(bytes, 192, materialSet.InitializedHqTableSha256);
        WriteHash(bytes, 224, materialSet.ContentSha256);

        int offset = HeaderSize;
        foreach (NativeTerrainHqMaterialRecordPayload texture in materialSet.Textures)
        {
            int recordLength = GetSerializedRecordByteLength(texture);
            WriteInt32(bytes, offset, texture.TextureId);
            WriteInt32(bytes, offset + 4, recordLength);
            offset += RecordHeaderSize;
            foreach (NativeTerrainHqMaterialDescriptorPayload descriptor in texture.Descriptors)
            {
                descriptor.RawDescriptorBytes.CopyTo(bytes, offset);
                bytes[offset + 8] = (byte)descriptor.Tier;
                bytes[offset + 9] = checked((byte)descriptor.DescriptorIndex);
                bytes[offset + 10] = checked((byte)descriptor.Side);
                bytes[offset + 11] = checked((byte)descriptor.Abr);
                WriteInt32(bytes, offset + 12, descriptor.WordCount);
                WriteInt32(bytes, offset + 16, descriptor.RawWordBytes.Length);
                offset += DescriptorHeaderSize;
                descriptor.RawWordBytes.CopyTo(bytes, offset);
                offset += descriptor.RawWordBytes.Length;
            }
        }
        if (offset != bytes.Length)
            throw new InvalidDataException("HQ material sidecar serialization did not consume its exact payload length.");

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

        return new NativeTerrainHqMaterialCacheWriteResult(
            path,
            bytes.LongLength,
            Sha256Hex(bytes),
            materialSet.TextureCount,
            materialSet.CompositeCount,
            materialSet.TotalDescriptorCount,
            materialSet.RawWordCount,
            materialSet.ZeroWordCount,
            materialSet.StpSetWordCount);
    }

    public static NativeTerrainHqMaterialCacheReadResult ReadValidated(
        string path,
        NativeTerrainHqMaterialCacheProof proof)
    {
        ArgumentNullException.ThrowIfNull(proof);
        List<string> blockers = [];
        NativeTerrainHqMaterialCacheReadResult Failure(string message) => new(false, null, [message]);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return Failure("The HQ material sidecar file does not exist.");
        if (proof.PayloadFormatVersion != PayloadFormatVersion || proof.HeaderSize != HeaderSize ||
            proof.DescriptorHeaderSize != DescriptorHeaderSize || proof.PayloadByteLength < HeaderSize ||
            proof.NativeTextureCount is <= 0 or > 128 || proof.RecordCount <= 0 || proof.RecordCount > proof.NativeTextureCount ||
            proof.TotalDescriptorCount != checked(proof.RecordCount * DescriptorCountPerTexture) ||
            proof.CompositeCount != checked(proof.RecordCount * 2) || proof.RawWordCount <= 0 ||
            proof.ZeroWordCount < 0 || proof.ZeroWordCount > proof.RawWordCount ||
            proof.StpSetWordCount < 0 || proof.StpSetWordCount > proof.RawWordCount ||
            proof.TexturePagesByteLength <= 0 ||
            proof.TextureComponentByteLength != checked(8 + (proof.NativeTextureCount * (LowDetailRecordBytes + HighDetailRecordBytes))) ||
            !IsSha256(proof.PayloadSha256) || !IsSha256(proof.TexturePagesSha256) ||
            !IsSha256(proof.OriginalTextureComponentSha256) || !IsSha256(proof.InitializedTextureComponentSha256) ||
            !IsSha256(proof.InitializedHqTableSha256) || !IsSha256(proof.ContentSha256))
        {
            return Failure("The HQ material manifest proof is incomplete or internally inconsistent.");
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.LongLength != proof.PayloadByteLength)
                blockers.Add($"HQ material sidecar length is {bytes.LongLength}, expected {proof.PayloadByteLength}.");
            if (!string.Equals(Sha256Hex(bytes), proof.PayloadSha256, StringComparison.OrdinalIgnoreCase))
                blockers.Add("HQ material sidecar SHA-256 does not match the manifest proof.");
            if (bytes.Length < HeaderSize || !bytes.AsSpan(0, Magic.Length).SequenceEqual(Magic))
                blockers.Add("HQ material sidecar magic/header is invalid.");
            if (blockers.Count > 0)
                return new NativeTerrainHqMaterialCacheReadResult(false, null, blockers);

            AssertHeader(bytes, 8, PayloadFormatVersion, "format version", blockers);
            AssertHeader(bytes, 12, HeaderSize, "header size", blockers);
            AssertHeader(bytes, 16, DescriptorHeaderSize, "descriptor header size", blockers);
            AssertHeader(bytes, 20, proof.RecordCount, "record count", blockers);
            AssertHeader(bytes, 24, proof.NativeTextureCount, "native texture count", blockers);
            AssertHeader(bytes, 28, NormalDescriptorCount, "normal descriptor count", blockers);
            AssertHeader(bytes, 32, CloseDescriptorCount, "close descriptor count", blockers);
            AssertHeader(bytes, 36, RawWordByteCount, "raw word byte count", blockers);
            AssertHeader(bytes, 40, proof.TexturePagesByteLength, "texture-pages byte length", blockers);
            AssertHeader(bytes, 44, proof.TextureComponentByteLength, "texture-component byte length", blockers);
            AssertHeader(bytes, 48, proof.TotalDescriptorCount, "descriptor count", blockers);
            AssertHeader(bytes, 52, proof.CompositeCount, "composite count", blockers);
            AssertHeader(bytes, 56, proof.RawWordCount, "raw word count", blockers);
            AssertHeader(bytes, 60, proof.ZeroWordCount, "zero-word count", blockers);
            AssertHeader(bytes, 64, proof.StpSetWordCount, "STP-set word count", blockers);
            AssertHash(bytes, 96, proof.TexturePagesSha256, "texture-pages", blockers);
            AssertHash(bytes, 128, proof.OriginalTextureComponentSha256, "original texture component", blockers);
            AssertHash(bytes, 160, proof.InitializedTextureComponentSha256, "initialized texture component", blockers);
            AssertHash(bytes, 192, proof.InitializedHqTableSha256, "initialized HQ table", blockers);
            AssertHash(bytes, 224, proof.ContentSha256, "material content", blockers);
            if (blockers.Count > 0)
                return new NativeTerrainHqMaterialCacheReadResult(false, null, blockers);

            List<NativeTerrainHqMaterialRecordPayload> textures = [];
            int offset = HeaderSize;
            int previousTextureId = -1;
            for (int recordIndex = 0; recordIndex < proof.RecordCount; recordIndex++)
            {
                if (offset + RecordHeaderSize > bytes.Length)
                {
                    blockers.Add($"HQ material record {recordIndex} header is truncated.");
                    break;
                }
                int recordStart = offset;
                int textureId = ReadInt32(bytes, offset);
                int recordLength = ReadInt32(bytes, offset + 4);
                offset += RecordHeaderSize;
                if (textureId <= previousTextureId || textureId < 0 || textureId >= proof.NativeTextureCount)
                    blockers.Add($"HQ material record {recordIndex} has duplicate, unordered, or out-of-range texture id {textureId}.");
                previousTextureId = textureId;
                if (recordLength < RecordHeaderSize || recordStart + (long)recordLength > bytes.Length)
                {
                    blockers.Add($"HQ material texture {textureId} has an invalid or truncated record length {recordLength}.");
                    break;
                }

                List<NativeTerrainHqMaterialDescriptorPayload> normal = [];
                List<NativeTerrainHqMaterialDescriptorPayload> close = [];
                for (int descriptorOrdinal = 0; descriptorOrdinal < DescriptorCountPerTexture; descriptorOrdinal++)
                {
                    if (offset + DescriptorHeaderSize > recordStart + recordLength)
                    {
                        blockers.Add($"HQ material texture {textureId} descriptor {descriptorOrdinal} header is truncated.");
                        break;
                    }
                    byte[] raw = bytes.AsSpan(offset, 8).ToArray();
                    int tierValue = bytes[offset + 8];
                    int descriptorIndex = bytes[offset + 9];
                    int side = bytes[offset + 10];
                    int abr = bytes[offset + 11];
                    int wordCount = ReadInt32(bytes, offset + 12);
                    int wordByteLength = ReadInt32(bytes, offset + 16);
                    offset += DescriptorHeaderSize;

                    NativeTerrainHqMaterialTier expectedTier = descriptorOrdinal < NormalDescriptorCount
                        ? NativeTerrainHqMaterialTier.Normal
                        : NativeTerrainHqMaterialTier.Close;
                    int expectedIndex = descriptorOrdinal < NormalDescriptorCount
                        ? descriptorOrdinal
                        : descriptorOrdinal - NormalDescriptorCount;
                    int derivedSide = DeriveTileSide(raw);
                    int derivedAbr = raw.Length == 8 ? (raw[6] >> 5) & 3 : -1;
                    if (tierValue != (int)expectedTier || descriptorIndex != expectedIndex ||
                        side != derivedSide || side is not (16 or 32) || abr != derivedAbr || abr is < 0 or > 3 ||
                        wordCount != checked(side * side) || wordByteLength != checked(wordCount * sizeof(ushort)) ||
                        offset + (long)wordByteLength > recordStart + recordLength)
                    {
                        blockers.Add($"HQ material texture {textureId} descriptor {descriptorOrdinal} has inconsistent tier/index/shape/ABR/word metadata.");
                        if (wordByteLength < 0 || offset + (long)Math.Max(0, wordByteLength) > recordStart + recordLength)
                            break;
                    }
                    byte[] rawWords = bytes.AsSpan(offset, wordByteLength).ToArray();
                    offset += wordByteLength;
                    NativeTerrainHqMaterialDescriptorPayload descriptor = new(
                        expectedTier,
                        expectedIndex,
                        side,
                        abr,
                        raw,
                        rawWords);
                    (expectedTier == NativeTerrainHqMaterialTier.Normal ? normal : close).Add(descriptor);
                }
                if (offset != recordStart + recordLength)
                    blockers.Add($"HQ material texture {textureId} has trailing or unconsumed descriptor bytes.");
                if (normal.Count == NormalDescriptorCount && close.Count == CloseDescriptorCount &&
                    normal.Select(descriptor => descriptor.Side).Distinct().Count() == 1 &&
                    close.Select(descriptor => descriptor.Side).Distinct().Count() == 1)
                {
                    textures.Add(new NativeTerrainHqMaterialRecordPayload(
                        textureId,
                        new NativeTerrainHqMaterialTierPayload(NativeTerrainHqMaterialTier.Normal, normal),
                        new NativeTerrainHqMaterialTierPayload(NativeTerrainHqMaterialTier.Close, close)));
                }
                offset = checked(recordStart + recordLength);
            }
            if (offset != bytes.Length)
                blockers.Add("HQ material sidecar has trailing or unconsumed bytes.");
            if (textures.Count != proof.RecordCount)
                blockers.Add($"HQ material sidecar decoded {textures.Count}/{proof.RecordCount} records.");
            if (blockers.Count > 0)
                return new NativeTerrainHqMaterialCacheReadResult(false, null, blockers);

            NativeTerrainHqMaterialSet set = new(
                proof.NativeTextureCount,
                proof.TexturePagesByteLength,
                proof.TextureComponentByteLength,
                proof.TexturePagesSha256.ToUpperInvariant(),
                proof.OriginalTextureComponentSha256.ToUpperInvariant(),
                proof.InitializedTextureComponentSha256.ToUpperInvariant(),
                proof.InitializedHqTableSha256.ToUpperInvariant(),
                textures);
            if (set.TotalDescriptorCount != proof.TotalDescriptorCount || set.CompositeCount != proof.CompositeCount ||
                set.RawWordCount != proof.RawWordCount || set.ZeroWordCount != proof.ZeroWordCount ||
                set.StpSetWordCount != proof.StpSetWordCount ||
                !string.Equals(set.ContentSha256, proof.ContentSha256, StringComparison.OrdinalIgnoreCase))
            {
                return Failure("HQ material payload aggregate counts do not match the manifest proof.");
            }
            return new NativeTerrainHqMaterialCacheReadResult(true, set, []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or OverflowException)
        {
            return Failure($"Could not read the HQ material sidecar: {ex.Message}");
        }
    }

    internal static string Sha256Hex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    internal static string ComputeContentSha256(NativeTerrainHqMaterialSet materialSet)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.ASCII.GetBytes(MaterialFormat));
        AppendContentInt32(hash, materialSet.NativeTextureCount);
        AppendContentInt32(hash, materialSet.TexturePagesByteLength);
        AppendContentInt32(hash, materialSet.TextureComponentByteLength);
        hash.AppendData(Convert.FromHexString(materialSet.TexturePagesSha256));
        hash.AppendData(Convert.FromHexString(materialSet.OriginalTextureComponentSha256));
        hash.AppendData(Convert.FromHexString(materialSet.InitializedTextureComponentSha256));
        hash.AppendData(Convert.FromHexString(materialSet.InitializedHqTableSha256));
        foreach (NativeTerrainHqMaterialRecordPayload texture in materialSet.Textures)
        {
            AppendContentInt32(hash, texture.TextureId);
            foreach (NativeTerrainHqMaterialDescriptorPayload descriptor in texture.Descriptors)
            {
                AppendContentInt32(hash, (int)descriptor.Tier);
                AppendContentInt32(hash, descriptor.DescriptorIndex);
                AppendContentInt32(hash, descriptor.Side);
                AppendContentInt32(hash, descriptor.Abr);
                hash.AppendData(descriptor.RawDescriptorBytes);
                hash.AppendData(descriptor.RawWordBytes);
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void AppendContentInt32(IncrementalHash hash, int value)
    {
        Span<byte> integer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(integer, value);
        hash.AppendData(integer);
    }

    private static List<NativeTerrainHqMaterialDescriptorPayload> DecodeTier(
        ReadOnlySpan<byte> texturePages,
        ReadOnlySpan<byte> initialized,
        int textureId,
        int descriptorTableOffset,
        NativeTerrainHqMaterialTier tier,
        int descriptorCount,
        List<string> blockers)
    {
        List<NativeTerrainHqMaterialDescriptorPayload> result = [];
        for (int descriptorIndex = 0; descriptorIndex < descriptorCount; descriptorIndex++)
        {
            byte[] raw = initialized.Slice(descriptorTableOffset + (descriptorIndex * 8), 8).ToArray();
            if (!TryDecodeDescriptor(texturePages, tier, descriptorIndex, raw, out NativeTerrainHqMaterialDescriptorPayload? descriptor, out string failure))
            {
                blockers.Add($"Texture {textureId} {TierName(tier)} descriptor {descriptorIndex}: {failure}");
                continue;
            }
            result.Add(descriptor!);
        }
        return result;
    }

    private static bool TryDecodeDescriptor(
        ReadOnlySpan<byte> texturePages,
        NativeTerrainHqMaterialTier tier,
        int descriptorIndex,
        byte[] raw,
        out NativeTerrainHqMaterialDescriptorPayload? descriptor,
        out string failure)
    {
        descriptor = null;
        failure = "";
        int side = DeriveTileSide(raw);
        if (side is not (16 or 32))
        {
            failure = $"descriptor logical side {side} is not a supported native 16 or 32 pixels";
            return false;
        }

        int region = raw[6];
        int bitsPerPixel = (region & 0x80) != 0 ? 8 : 4;
        int pixelsPerPackedByte = 8 / bitsPerPixel;
        int abr = (region >> 5) & 3;
        int orientation = (raw[7] >> 4) & 7;
        int[] matrix = DescriptorMatrices[orientation];
        int pagePackedByteX = (region & 0x0F) * 128;
        int vramYMin = (((region & 0x1F) / 16) * 256) + raw[1];
        int edge = side - 1;
        int startY = vramYMin + ((matrix[2] < 0 || matrix[3] < 0) ? edge : 0);
        int startLocalPixelX = raw[0] + ((matrix[0] < 0 || matrix[1] < 0) ? edge : 0);

        ushort clutCode = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(2, 2));
        int clutXWord = (clutCode & 0x3F) * 16;
        int clutY = (clutCode >> 6) & 0x1FF;
        int paletteByteStart = (clutY * PackedTexturePageRowBytes) + ((clutXWord - 512) * sizeof(ushort));
        int paletteByteLength = (1 << bitsPerPixel) * sizeof(ushort);
        if (clutXWord < 512 || paletteByteStart < 0 || paletteByteStart + paletteByteLength > texturePages.Length)
        {
            failure = "descriptor CLUT maps outside the loaded right-half VRAM payload";
            return false;
        }

        byte[] rawWords = new byte[checked(side * side * sizeof(ushort))];
        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
            {
                int sampleY = startY + (x * matrix[2]) + (y * matrix[3]);
                int sampleLocalPixelX =
                    startLocalPixelX + (x * matrix[0]) + (y * matrix[1]);
                int samplePackedX =
                    pagePackedByteX + (sampleLocalPixelX / pixelsPerPackedByte) - FullVramTextureByteX;
                long pixelOffset = (sampleY * (long)PackedTexturePageRowBytes) + samplePackedX;
                if (samplePackedX < 0 || samplePackedX >= PackedTexturePageRowBytes ||
                    sampleLocalPixelX < 0 ||
                    sampleY < 0 || sampleY >= TexturePageMaxRows || pixelOffset < 0 || pixelOffset >= texturePages.Length)
                {
                    failure = $"pixel ({x},{y}) maps outside the loaded right-half VRAM payload";
                    return false;
                }
                int packedPixel = texturePages[(int)pixelOffset];
                int paletteIndex = bitsPerPixel == 8
                    ? packedPixel
                    : (packedPixel >> ((sampleLocalPixelX & 1) * 4)) & 0x0F;
                int paletteOffset = paletteByteStart + (paletteIndex * sizeof(ushort));
                ushort word = BinaryPrimitives.ReadUInt16LittleEndian(texturePages.Slice(paletteOffset, sizeof(ushort)));
                BinaryPrimitives.WriteUInt16LittleEndian(
                    rawWords.AsSpan(((y * side) + x) * sizeof(ushort), sizeof(ushort)),
                    word);
            }
        }
        descriptor = new NativeTerrainHqMaterialDescriptorPayload(tier, descriptorIndex, side, abr, raw, rawWords);
        return true;
    }

    private static int DeriveTileSide(ReadOnlySpan<byte> raw) =>
        raw.Length == 8
            ? Math.Max(Math.Abs(raw[4] - raw[0]), Math.Abs(raw[5] - raw[1])) + 1
            : -1;

    private static string TierName(NativeTerrainHqMaterialTier tier) =>
        tier == NativeTerrainHqMaterialTier.Normal ? "normal HQ" : "close HQ";

    private static int GetSerializedRecordByteLength(NativeTerrainHqMaterialRecordPayload texture) =>
        checked(RecordHeaderSize + texture.Descriptors.Sum(descriptor => DescriptorHeaderSize + descriptor.RawWordBytes.Length));

    private static void ValidateSetForWrite(NativeTerrainHqMaterialSet materialSet)
    {
        if (materialSet.NativeTextureCount <= 0 || materialSet.NativeTextureCount > 128 ||
            materialSet.TextureCount <= 0 || materialSet.TextureCount > materialSet.NativeTextureCount ||
            materialSet.TexturePagesByteLength <= 0 || materialSet.TextureComponentByteLength <= 0 ||
            !IsSha256(materialSet.TexturePagesSha256) || !IsSha256(materialSet.OriginalTextureComponentSha256) ||
            !IsSha256(materialSet.InitializedTextureComponentSha256) || !IsSha256(materialSet.InitializedHqTableSha256))
        {
            throw new InvalidDataException("The HQ material set does not contain complete native source provenance.");
        }
        int previous = -1;
        foreach (NativeTerrainHqMaterialRecordPayload texture in materialSet.Textures)
        {
            if (texture.TextureId <= previous || texture.TextureId < 0 || texture.TextureId >= materialSet.NativeTextureCount)
                throw new InvalidDataException("HQ material textures must be unique, ordered, and inside the native table.");
            previous = texture.TextureId;
            ValidateTier(texture.TextureId, texture.Normal, NativeTerrainHqMaterialTier.Normal, NormalDescriptorCount);
            ValidateTier(texture.TextureId, texture.Close, NativeTerrainHqMaterialTier.Close, CloseDescriptorCount);
        }
    }

    private static void ValidateTier(
        int textureId,
        NativeTerrainHqMaterialTierPayload tier,
        NativeTerrainHqMaterialTier expectedTier,
        int expectedDescriptorCount)
    {
        if (tier.Tier != expectedTier || tier.DescriptorCount != expectedDescriptorCount ||
            tier.Descriptors.Select(descriptor => descriptor.Side).Distinct().Count() != 1)
        {
            throw new InvalidDataException($"Texture {textureId} has an incomplete or inconsistent {TierName(expectedTier)} tier.");
        }
        for (int index = 0; index < tier.Descriptors.Count; index++)
        {
            NativeTerrainHqMaterialDescriptorPayload descriptor = tier.Descriptors[index];
            if (descriptor.Tier != expectedTier || descriptor.DescriptorIndex != index ||
                descriptor.Side is not (16 or 32) || descriptor.Side != DeriveTileSide(descriptor.RawDescriptorBytes) ||
                descriptor.Abr != ((descriptor.RawDescriptorBytes[6] >> 5) & 3) ||
                descriptor.RawWordBytes.Length != checked(descriptor.Side * descriptor.Side * sizeof(ushort)))
            {
                throw new InvalidDataException($"Texture {textureId} {TierName(expectedTier)} descriptor {index} is incomplete.");
            }
        }
    }

    private static bool IsSha256(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length == 64 && value.All(Uri.IsHexDigit);

    private static int ReadInt32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, sizeof(int)));

    private static void WriteInt32(Span<byte> bytes, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(offset, sizeof(int)), value);

    private static void WriteHash(Span<byte> bytes, int offset, string value) =>
        Convert.FromHexString(value).CopyTo(bytes.Slice(offset, 32));

    private static void AssertHeader(ReadOnlySpan<byte> bytes, int offset, int expected, string label, List<string> blockers)
    {
        int actual = ReadInt32(bytes, offset);
        if (actual != expected)
            blockers.Add($"HQ material sidecar {label} is {actual}, expected {expected}.");
    }

    private static void AssertHash(ReadOnlySpan<byte> bytes, int offset, string expected, string label, List<string> blockers)
    {
        string actual = Convert.ToHexString(bytes.Slice(offset, 32));
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            blockers.Add($"HQ material sidecar {label} SHA-256 provenance does not match its manifest.");
    }
}

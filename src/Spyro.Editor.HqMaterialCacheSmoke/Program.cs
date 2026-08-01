using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

string workspaceRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition[] levels = LevelRealmCatalog.OrderLevels(catalog.Levels.Where(level => level.SourceWadEntry >= 0)).ToArray();
Assert(levels.Length == 35, $"Expected all 35 retail levels, got {levels.Length}.");

using IncrementalHash inventoryHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
using IncrementalHash payloadHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
using IncrementalHash compositeHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
List<LevelInventory> rows = [];
int totalTextures = 0;
int totalComposites = 0;
int totalDescriptors = 0;
int totalRawWords = 0;
int totalZeroWords = 0;
int totalStpWords = 0;
long totalPayloadBytes = 0;
int[] totalAbrDescriptors = new int[4];
int[] totalBitsPerPixelDescriptors = new int[2];
Dictionary<int, int> normalSides = [];
Dictionary<int, int> closeSides = [];

foreach (LevelDefinition level in levels)
{
    Assert(PortableEditorCacheBuilder.TryLoadNativeTerrainLqTextureCache(
            workspaceRoot,
            level.Key,
            out NativeTerrainLqTextureSet? lq) && lq != null,
        $"{level.DisplayName}: public TexLq cache loader failed.");
    Assert(PortableEditorCacheBuilder.TryLoadNativeTerrainHqMaterialCache(
            workspaceRoot,
            level.Key,
            out NativeTerrainHqMaterialSet? materials) && materials != null,
        $"{level.DisplayName}: public HQ material cache loader failed.");
    NativeTerrainLqTextureSet lqSet = lq ?? throw new InvalidOperationException($"{level.DisplayName}: TexLq loader returned no payload.");
    NativeTerrainHqMaterialSet materialSet = materials ?? throw new InvalidOperationException($"{level.DisplayName}: HQ material loader returned no payload.");
    Assert(materialSet.TextureCount == materialSet.NativeTextureCount && materialSet.TextureCount == lqSet.TextureCount,
        $"{level.DisplayName}: HQ/LQ/native texture counts diverge.");
    Assert(materialSet.Textures.Select(texture => texture.TextureId).SequenceEqual(Enumerable.Range(0, materialSet.NativeTextureCount)),
        $"{level.DisplayName}: HQ material records are not a complete ordered native table.");
    Assert(string.Equals(materialSet.TexturePagesSha256, lqSet.TexturePagesSha256, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(materialSet.OriginalTextureComponentSha256, lqSet.OriginalTextureComponentSha256, StringComparison.OrdinalIgnoreCase),
        $"{level.DisplayName}: HQ and LQ sidecars do not share source provenance.");

    string directory = Path.Combine(workspaceRoot, "editor-cache", "terrain-textures", level.Key);
    string payloadPath = Path.Combine(directory, NativeTerrainHqMaterialCacheCodec.PayloadFileName);
    string manifestPath = Path.Combine(directory, "manifest.json");
    using JsonDocument manifestDocument = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
    JsonElement manifest = manifestDocument.RootElement;
    string payloadSha256 = manifest.GetProperty("nativeMaterialPayloadSha256").GetString() ?? "";
    long payloadBytes = manifest.GetProperty("nativeMaterialPayloadByteLength").GetInt64();
    Assert(File.Exists(payloadPath) && new FileInfo(payloadPath).Length == payloadBytes &&
           string.Equals(Sha256Hex(File.ReadAllBytes(payloadPath)), payloadSha256, StringComparison.OrdinalIgnoreCase),
        $"{level.DisplayName}: sidecar file proof changed after the public loader accepted it.");

    AppendUtf8(inventoryHash, level.Key);
    AppendUtf8(inventoryHash, payloadSha256);
    AppendUtf8(inventoryHash, materialSet.ContentSha256);
    AppendInt32(inventoryHash, materialSet.TextureCount);
    AppendInt32(inventoryHash, materialSet.RawWordCount);
    payloadHash.AppendData(File.ReadAllBytes(payloadPath));

    foreach (NativeTerrainHqMaterialRecordPayload texture in materialSet.Textures)
    {
        AssertThrows<ArgumentOutOfRangeException>(
            () => texture.GetTier((NativeTerrainHqMaterialTier)255),
            $"{level.DisplayName} texture {texture.TextureId}: an invalid public tier selector did not fail closed.");
        VerifyTier(level, directory, texture.TextureId, texture.Normal, normalSides, compositeHash);
        VerifyTier(level, directory, texture.TextureId, texture.Close, closeSides, compositeHash);
        foreach (NativeTerrainHqMaterialDescriptorPayload descriptor in texture.Descriptors)
        {
            Assert(descriptor.Abr == ((descriptor.RawDescriptor.Span[6] >> 5) & 3),
                $"{level.DisplayName} texture {texture.TextureId}: descriptor-local ABR does not match the raw descriptor.");
            Assert(descriptor.BitsPerPixel == ((descriptor.RawDescriptor.Span[6] & 0x80) != 0 ? 8 : 4),
                $"{level.DisplayName} texture {texture.TextureId}: descriptor pixel depth does not match native TPAGE bit 7.");
            Assert(descriptor.RawWordsLittleEndian.Length == descriptor.Side * descriptor.Side * sizeof(ushort),
                $"{level.DisplayName} texture {texture.TextureId}: raw word plane has the wrong length.");
            Assert(descriptor.ZeroWordCount == CountWords(descriptor.RawWordsLittleEndian.Span, static word => word == 0) &&
                   descriptor.StpSetWordCount == CountWords(descriptor.RawWordsLittleEndian.Span, static word => (word & 0x8000) != 0),
                $"{level.DisplayName} texture {texture.TextureId}: zero/STP counts do not match the retained words.");
        }
    }

    totalTextures += materialSet.TextureCount;
    totalComposites += materialSet.CompositeCount;
    totalDescriptors += materialSet.TotalDescriptorCount;
    totalRawWords += materialSet.RawWordCount;
    totalZeroWords += materialSet.ZeroWordCount;
    totalStpWords += materialSet.StpSetWordCount;
    totalPayloadBytes += payloadBytes;
    for (int abr = 0; abr < totalAbrDescriptors.Length; abr++)
        totalAbrDescriptors[abr] += materialSet.AbrDescriptorCounts[abr];
    for (int index = 0; index < totalBitsPerPixelDescriptors.Length; index++)
        totalBitsPerPixelDescriptors[index] += materialSet.BitsPerPixelDescriptorCounts[index];
    if (string.Equals(level.Key, "magiccrafters", StringComparison.Ordinal))
    {
        NativeTerrainHqMaterialRecordPayload correctedStone = materialSet.Textures.Single(texture => texture.TextureId == 42);
        Assert(correctedStone.Descriptors.All(descriptor => descriptor.BitsPerPixel == 4),
            "Magic Crafters texture 42 was not decoded as a complete native 4-bpp terrain record.");
        Assert(correctedStone.Normal.CompositeRawWordsSha256 == "6B6E2CFD811167349FB562A61278EEF235B756725F6894588377A55A960BAFBF" &&
               correctedStone.Close.CompositeRawWordsSha256 == correctedStone.Normal.CompositeRawWordsSha256,
            "Magic Crafters texture 42 no longer resolves to the source-faithful stone-wall image at both native subdivision tiers.");
    }
    rows.Add(new LevelInventory(
        level.Key,
        level.DisplayName,
        materialSet.TextureCount,
        materialSet.CompositeCount,
        materialSet.TotalDescriptorCount,
        materialSet.RawWordCount,
        materialSet.ZeroWordCount,
        materialSet.StpSetWordCount,
        materialSet.AbrDescriptorCounts.ToArray(),
        materialSet.BitsPerPixelDescriptorCounts.ToArray(),
        payloadBytes,
        payloadSha256,
        materialSet.ContentSha256));
}

Assert(totalTextures == 2070, $"Expected 2,070 native HQ material textures, got {totalTextures}.");
Assert(totalComposites == 4140, $"Expected 4,140 HQ composites, got {totalComposites}.");
Assert(totalDescriptors == 41400, $"Expected 41,400 descriptor-local HQ material tiles, got {totalDescriptors}.");
Assert(totalRawWords == 17_547_264, $"Expected 17,547,264 retained HQ PSX555/STP words, got {totalRawWords}.");
Assert(totalZeroWords == 0, $"Expected no spurious transparent words after depth-aware HQ decoding, got {totalZeroWords}.");
Assert(totalStpWords == 139_264, $"Expected 139,264 retained STP-set words, got {totalStpWords}.");
Assert(totalAbrDescriptors.SequenceEqual([41_160, 240, 0, 0]),
    $"Expected descriptor ABR inventory 41160/240/0/0, got {string.Join('/', totalAbrDescriptors)}.");
Assert(totalBitsPerPixelDescriptors.SequenceEqual([740, 40_660]),
    $"Expected native 4/8-bpp HQ descriptor inventory 740/40660, got {string.Join('/', totalBitsPerPixelDescriptors)}.");
Assert(normalSides.OrderBy(pair => pair.Key).SequenceEqual(new Dictionary<int, int> { [32] = 2070 }),
    $"Normal HQ tile-side inventory changed: {FormatCounts(normalSides)}.");
Assert(closeSides.OrderBy(pair => pair.Key).SequenceEqual(new Dictionary<int, int> { [16] = 2022, [32] = 48 }),
    $"Close HQ tile-side inventory changed: {FormatCounts(closeSides)}.");

string inventoryFingerprint = Convert.ToHexString(inventoryHash.GetHashAndReset());
string payloadFingerprint = Convert.ToHexString(payloadHash.GetHashAndReset());
string compositeFingerprint = Convert.ToHexString(compositeHash.GetHashAndReset());
Assert(totalPayloadBytes == 35_948_048, $"Expected 35,948,048 HQ material sidecar bytes, got {totalPayloadBytes}.");
Assert(inventoryFingerprint == "CB69331B213C45DE2B3D3D1121D046FAFDB9747C850374F40A501D01258F5484",
    $"HQ material inventory fingerprint changed: {inventoryFingerprint}.");
Assert(payloadFingerprint == "3D15828BA8CF4E09DBB83953AA2A6382FE8BFBFC8903A7B4355E648EC6D3729A",
    $"HQ material payload fingerprint changed: {payloadFingerprint}.");
Assert(compositeFingerprint == "B2F972245E4C7150777CD07D3420C5842B8151CDF1A9C92301AB90F24CBD6D81",
    $"HQ material composite fingerprint changed: {compositeFingerprint}.");

string sourceDiscSettingsPath = Path.Combine(workspaceRoot, "_local", "settings", "source-disc.json");
using JsonDocument sourceDiscSettings = JsonDocument.Parse(File.ReadAllBytes(sourceDiscSettingsPath));
string sourceImagePath = sourceDiscSettings.RootElement.GetProperty("imagePath").GetString()
    ?? throw new InvalidDataException("The source-disc settings did not retain an imagePath.");
LevelDefinition magicCrafters = levels.Single(level =>
    level.Key.Equals("magiccrafters", StringComparison.OrdinalIgnoreCase));
TerrainTextureStorageIsolation correctedStoneIsolation = TerrainPatchExporter.InspectTextureStorageIsolation(
    sourceImagePath,
    magicCrafters,
    targetTextureId: 42,
    descriptorTier: "both",
    residentTextureIds: Enumerable.Range(0, 67).Where(textureId => textureId != 42));
Assert(correctedStoneIsolation.TargetPhysicalNibbleCount == 4160 &&
       correctedStoneIsolation.OverlappingResidentTextureCount == 0 &&
       correctedStoneIsolation.OverlappingPhysicalNibbleCount == 0 &&
       correctedStoneIsolation.UnreadableResidentTextureIds.Count == 0,
    "Magic Crafters texture 42 did not retain its isolated 4,160-nibble depth-aware physical layout.");
Console.WriteLine(
    $"Magic Crafters texture 42 storage: {correctedStoneIsolation.TargetPhysicalNibbleCount} target nibbles, " +
    $"{correctedStoneIsolation.OverlappingResidentTextureCount} overlapping resident texture(s), " +
    $"{correctedStoneIsolation.OverlappingPhysicalNibbleCount} overlapping nibble(s).");

RunTamperFixtures(workspaceRoot);

string outputDirectory = Path.Combine(workspaceRoot, "_local", "research", "native-terrain-translucency");
Directory.CreateDirectory(outputDirectory);
string reportPath = Path.Combine(outputDirectory, "native-hq-material-cache-smoke.json");
File.WriteAllText(reportPath, JsonSerializer.Serialize(new
{
    generatedAt = DateTimeOffset.UtcNow,
    cacheFormatVersion = PortableEditorCacheBuilder.TerrainTexturePreviewCacheFormatVersion,
    decoder = PortableEditorCacheBuilder.TerrainTexturePreviewDecoder,
    materialFormat = NativeTerrainHqMaterialCacheCodec.MaterialFormat,
    levelCount = levels.Length,
    textureCount = totalTextures,
    compositeCount = totalComposites,
    descriptorCount = totalDescriptors,
    rawWordCount = totalRawWords,
    zeroWordCount = totalZeroWords,
    stpSetWordCount = totalStpWords,
    abrDescriptorCounts = totalAbrDescriptors,
    bitsPerPixelDescriptorCounts = totalBitsPerPixelDescriptors,
    normalTileSides = normalSides.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value),
    closeTileSides = closeSides.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value),
    payloadByteLength = totalPayloadBytes,
    inventoryFingerprint,
    payloadFingerprint,
    compositeFingerprint,
    magicCraftersTexture42Storage = correctedStoneIsolation,
    tamperFixtures = new[]
    {
        "payload-byte-with-stale-hash",
        "truncated-payload",
        "payload-byte-with-recomputed-file-hash",
        "descriptor-abr-with-recomputed-file-hash",
        "manifest-payload-hash",
        "manifest-content-hash",
        "manifest-level-key",
        "manifest-preservation-flag",
        "manifest-bits-per-pixel-counts",
        "manifest-tier-composite-hash",
        "missing-sidecar"
    },
    levels = rows
}, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine("Native HQ PSX555/STP material cache smoke passed.");
Console.WriteLine($"Levels/textures/composites/descriptors: {levels.Length}/{totalTextures}/{totalComposites}/{totalDescriptors}");
Console.WriteLine($"Raw/zero/STP words: {totalRawWords}/{totalZeroWords}/{totalStpWords}");
Console.WriteLine($"ABR descriptor counts: {string.Join('/', totalAbrDescriptors)}");
Console.WriteLine($"4/8-bpp descriptor counts: {string.Join('/', totalBitsPerPixelDescriptors)}");
Console.WriteLine($"Normal tile sides: {FormatCounts(normalSides)}; close tile sides: {FormatCounts(closeSides)}");
Console.WriteLine($"Payload bytes: {totalPayloadBytes}");
Console.WriteLine($"Inventory fingerprint: {inventoryFingerprint}");
Console.WriteLine($"Payload fingerprint: {payloadFingerprint}");
Console.WriteLine($"Composite fingerprint: {compositeFingerprint}");
Console.WriteLine($"Report: {reportPath}");

static void VerifyTier(
    LevelDefinition level,
    string directory,
    int textureId,
    NativeTerrainHqMaterialTierPayload tier,
    Dictionary<int, int> sideCounts,
    IncrementalHash compositeHash)
{
    int expectedCompositeSide = checked(tier.GridColumns * tier.TileSide);
    Assert(tier.CompositeSide == expectedCompositeSide &&
           (tier.Tier == NativeTerrainHqMaterialTier.Normal
               ? tier.CompositeSide == 64
               : tier.CompositeSide is 64 or 128),
        $"{level.DisplayName} texture {textureId} {tier.TierName}: unexpected native composite side {tier.CompositeSide}.");
    sideCounts[tier.TileSide] = sideCounts.TryGetValue(tier.TileSide, out int count) ? count + 1 : 1;
    ushort[] rawWords = tier.MaterializeCompositeRawWords();
    Assert(rawWords.Length == tier.CompositeSide * tier.CompositeSide,
        $"{level.DisplayName} texture {textureId} {tier.TierName}: composite word count is incomplete.");
    byte[] rawBytes = new byte[rawWords.Length * sizeof(ushort)];
    for (int index = 0; index < rawWords.Length; index++)
        BinaryPrimitives.WriteUInt16LittleEndian(rawBytes.AsSpan(index * sizeof(ushort), sizeof(ushort)), rawWords[index]);
    Assert(string.Equals(Sha256Hex(rawBytes), tier.CompositeRawWordsSha256, StringComparison.OrdinalIgnoreCase),
        $"{level.DisplayName} texture {textureId} {tier.TierName}: composite fingerprint changed after materialization.");

    string suffix = tier.Tier == NativeTerrainHqMaterialTier.Normal ? "normal" : "close";
    string pngPath = Path.Combine(directory, $"{level.Key}-texture-{textureId:000}-{suffix}.png");
    PngRgba png = ReadPngRgba(pngPath);
    Assert(png.Width == tier.CompositeSide && png.Height == tier.CompositeSide,
        $"{level.DisplayName} texture {textureId} {tier.TierName}: PNG dimensions do not match the retained composite.");
    byte[] expectedRgba = ConvertRawWordsToRgba(rawWords);
    Assert(png.Rgba.SequenceEqual(expectedRgba),
        $"{level.DisplayName} texture {textureId} {tier.TierName}: PNG pixels do not exactly match retained PSX555/STP words.");
    AppendUtf8(compositeHash, level.Key);
    AppendInt32(compositeHash, textureId);
    AppendInt32(compositeHash, (int)tier.Tier);
    compositeHash.AppendData(rawBytes);
}

static void RunTamperFixtures(string workspaceRoot)
{
    string source = Path.Combine(workspaceRoot, "editor-cache", "terrain-textures", "stonehill");
    string fixtureRoot = Path.Combine(workspaceRoot, "_local", "smoke", "hq-material-cache-tamper", "stonehill");
    if (Directory.Exists(Path.GetDirectoryName(fixtureRoot)))
        Directory.Delete(Path.GetDirectoryName(fixtureRoot)!, true);
    Directory.CreateDirectory(fixtureRoot);
    string manifestPath = Path.Combine(fixtureRoot, "manifest.json");
    string lqPath = Path.Combine(fixtureRoot, NativeTerrainLqTextureCacheCodec.PayloadFileName);
    string hqPath = Path.Combine(fixtureRoot, NativeTerrainHqMaterialCacheCodec.PayloadFileName);
    File.Copy(Path.Combine(source, "manifest.json"), manifestPath, true);
    File.Copy(Path.Combine(source, NativeTerrainLqTextureCacheCodec.PayloadFileName), lqPath, true);
    File.Copy(Path.Combine(source, NativeTerrainHqMaterialCacheCodec.PayloadFileName), hqPath, true);
    byte[] originalPayload = File.ReadAllBytes(hqPath);
    JsonNode originalManifest = JsonNode.Parse(File.ReadAllText(manifestPath)) ?? throw new InvalidDataException("Missing tamper manifest.");

    Assert(PortableEditorCacheBuilder.TryLoadNativeTerrainHqMaterialCacheFromDirectory(fixtureRoot, out _),
        "Untampered HQ material fixture did not load.");

    byte[] mutated = originalPayload.ToArray();
    mutated[^1] ^= 0x01;
    File.WriteAllBytes(hqPath, mutated);
    Assert(!PortableEditorCacheBuilder.TryLoadNativeTerrainHqMaterialCacheFromDirectory(fixtureRoot, out _),
        "A payload byte mutation with stale SHA was accepted.");
    Assert(PortableEditorCacheBuilder.TryLoadNativeTerrainLqTextureCacheFromDirectory(fixtureRoot, out _),
        "HQ payload corruption should not make the independent TexLq loader permissive or unavailable.");

    File.WriteAllBytes(hqPath, originalPayload[..^1]);
    Assert(!PortableEditorCacheBuilder.TryLoadNativeTerrainHqMaterialCacheFromDirectory(fixtureRoot, out _),
        "A truncated HQ material payload was accepted.");

    mutated = originalPayload.ToArray();
    mutated[^1] ^= 0x01;
    File.WriteAllBytes(hqPath, mutated);
    JsonNode manifest = originalManifest.DeepClone();
    manifest["nativeMaterialPayloadSha256"] = Sha256Hex(mutated);
    File.WriteAllText(manifestPath, manifest.ToJsonString());
    Assert(!PortableEditorCacheBuilder.TryLoadNativeTerrainHqMaterialCacheFromDirectory(fixtureRoot, out _),
        "A raw-word mutation with a recomputed file SHA bypassed the canonical content fingerprint.");

    mutated = originalPayload.ToArray();
    int firstAbrOffset = NativeTerrainHqMaterialCacheCodec.HeaderSize + NativeTerrainHqMaterialCacheCodec.RecordHeaderSize + 11;
    mutated[firstAbrOffset] = (byte)((mutated[firstAbrOffset] + 1) & 3);
    File.WriteAllBytes(hqPath, mutated);
    manifest = originalManifest.DeepClone();
    manifest["nativeMaterialPayloadSha256"] = Sha256Hex(mutated);
    File.WriteAllText(manifestPath, manifest.ToJsonString());
    Assert(!PortableEditorCacheBuilder.TryLoadNativeTerrainHqMaterialCacheFromDirectory(fixtureRoot, out _),
        "Descriptor-local ABR metadata inconsistent with the raw descriptor was accepted.");

    File.WriteAllBytes(hqPath, originalPayload);
    manifest = originalManifest.DeepClone();
    manifest["nativeMaterialPayloadSha256"] = new string('0', 64);
    File.WriteAllText(manifestPath, manifest.ToJsonString());
    Assert(!PortableEditorCacheBuilder.TryLoadNativeTerrainHqMaterialCacheFromDirectory(fixtureRoot, out _),
        "A tampered manifest payload hash was accepted.");

    manifest = originalManifest.DeepClone();
    manifest["nativeMaterialContentSha256"] = new string('0', 64);
    File.WriteAllText(manifestPath, manifest.ToJsonString());
    Assert(!PortableEditorCacheBuilder.TryLoadNativeTerrainHqMaterialCacheFromDirectory(fixtureRoot, out _),
        "A tampered material content hash was accepted.");

    manifest = originalManifest.DeepClone();
    manifest["levelKey"] = "darkhollow";
    File.WriteAllText(manifestPath, manifest.ToJsonString());
    Assert(!PortableEditorCacheBuilder.TryLoadNativeTerrainHqMaterialCacheFromDirectory(fixtureRoot, out _),
        "A sidecar manifest copied under the wrong level key was accepted.");

    manifest = originalManifest.DeepClone();
    manifest["stpPreserved"] = false;
    File.WriteAllText(manifestPath, manifest.ToJsonString());
    Assert(!PortableEditorCacheBuilder.TryLoadNativeTerrainHqMaterialCacheFromDirectory(fixtureRoot, out _),
        "A manifest that denied STP preservation was accepted.");

    manifest = originalManifest.DeepClone();
    manifest["nativeMaterialBitsPerPixelDescriptorCounts"]![0] =
        manifest["nativeMaterialBitsPerPixelDescriptorCounts"]![0]!.GetValue<int>() + 1;
    File.WriteAllText(manifestPath, manifest.ToJsonString());
    Assert(!PortableEditorCacheBuilder.TryLoadNativeTerrainHqMaterialCacheFromDirectory(fixtureRoot, out _),
        "A tampered 4/8-bpp descriptor inventory was accepted.");

    manifest = originalManifest.DeepClone();
    manifest["hqMaterials"]![0]!["normal"]!["compositeRawWordsSha256"] = new string('0', 64);
    File.WriteAllText(manifestPath, manifest.ToJsonString());
    Assert(!PortableEditorCacheBuilder.TryLoadNativeTerrainHqMaterialCacheFromDirectory(fixtureRoot, out _),
        "A tampered per-tier composite hash was accepted.");

    File.WriteAllText(manifestPath, originalManifest.ToJsonString());
    File.Delete(hqPath);
    Assert(!PortableEditorCacheBuilder.TryLoadNativeTerrainHqMaterialCacheFromDirectory(fixtureRoot, out _),
        "A missing HQ material sidecar was accepted.");
}

static PngRgba ReadPngRgba(string path)
{
    byte[] bytes = File.ReadAllBytes(path);
    ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
    Assert(bytes.AsSpan(0, 8).SequenceEqual(signature), $"Invalid PNG signature: {path}");
    int offset = 8;
    int width = 0;
    int height = 0;
    using MemoryStream compressed = new();
    while (offset + 12 <= bytes.Length)
    {
        int length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
        string type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
        int dataOffset = offset + 8;
        Assert(length >= 0 && dataOffset + length + 4 <= bytes.Length, $"Truncated PNG chunk in {path}.");
        if (type == "IHDR")
        {
            width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(dataOffset, 4));
            height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(dataOffset + 4, 4));
            Assert(bytes[dataOffset + 8] == 8 && bytes[dataOffset + 9] == 6,
                $"Expected 8-bit RGBA PNG fixture: {path}");
        }
        else if (type == "IDAT")
        {
            compressed.Write(bytes, dataOffset, length);
        }
        else if (type == "IEND")
        {
            break;
        }
        offset = dataOffset + length + 4;
    }
    Assert(width > 0 && height > 0, $"Missing PNG dimensions: {path}");
    compressed.Position = 0;
    using ZLibStream zlib = new(compressed, CompressionMode.Decompress);
    using MemoryStream raw = new();
    zlib.CopyTo(raw);
    byte[] scanlines = raw.ToArray();
    int stride = checked(width * 4);
    Assert(scanlines.Length == checked((stride + 1) * height), $"Unexpected PNG scanline length: {path}");
    byte[] rgba = new byte[checked(stride * height)];
    for (int y = 0; y < height; y++)
    {
        int source = y * (stride + 1);
        Assert(scanlines[source] == 0, $"Expected filter-zero PNG fixture: {path}");
        scanlines.AsSpan(source + 1, stride).CopyTo(rgba.AsSpan(y * stride, stride));
    }
    return new PngRgba(width, height, rgba);
}

static byte[] ConvertRawWordsToRgba(IReadOnlyList<ushort> words)
{
    byte[] rgba = new byte[checked(words.Count * 4)];
    for (int index = 0; index < words.Count; index++)
    {
        ushort word = words[index];
        rgba[(index * 4) + 0] = Expand5(word & 0x1F);
        rgba[(index * 4) + 1] = Expand5((word >> 5) & 0x1F);
        rgba[(index * 4) + 2] = Expand5((word >> 10) & 0x1F);
        rgba[(index * 4) + 3] = word == 0 ? (byte)0 : (byte)255;
    }
    return rgba;
}

static byte Expand5(int value) => (byte)((value << 3) | (value >> 2));

static int CountWords(ReadOnlySpan<byte> bytes, Func<ushort, bool> predicate)
{
    int count = 0;
    for (int offset = 0; offset < bytes.Length; offset += sizeof(ushort))
    {
        if (predicate(BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, sizeof(ushort)))))
            count++;
    }
    return count;
}

static void AppendUtf8(IncrementalHash hash, string value)
{
    byte[] bytes = Encoding.UTF8.GetBytes(value);
    AppendInt32(hash, bytes.Length);
    hash.AppendData(bytes);
}

static void AppendInt32(IncrementalHash hash, int value)
{
    Span<byte> bytes = stackalloc byte[sizeof(int)];
    BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
    hash.AppendData(bytes);
}

static string Sha256Hex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));

static string FormatCounts(IReadOnlyDictionary<int, int> counts) =>
    string.Join(", ", counts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}x{pair.Key}:{pair.Value}"));

static void AssertThrows<TException>(Action action, string message) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }
    throw new InvalidOperationException(message);
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed record PngRgba(int Width, int Height, byte[] Rgba);

sealed record LevelInventory(
    string LevelKey,
    string LevelName,
    int TextureCount,
    int CompositeCount,
    int DescriptorCount,
    int RawWordCount,
    int ZeroWordCount,
    int StpSetWordCount,
    IReadOnlyList<int> AbrDescriptorCounts,
    IReadOnlyList<int> BitsPerPixelDescriptorCounts,
    long PayloadByteLength,
    string PayloadSha256,
    string ContentSha256);

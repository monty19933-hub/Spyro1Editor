using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

public static class TerrainPatchExporter
{
    private const int WadLba = 37;
    private const int TexturePagesSubfileIndex = 0;
    private const int ModelSubfileIndex = 1;
    private const int PackedTexturePageRowBytes = 1024;
    private const int FullVramTextureByteX = 1024;
    private const int TexturePageMaxRows = 512;
    private const int HqFourBitPaletteByteLength = 16 * 2;
    private const int HqEightBitPaletteByteLength = 256 * 2;
    private const string AtomicTerrainSwapBlockPrefix = "[atomic terrain swap blocked] ";
    private const int MaxSourceDerivedAppendedCollisionTrianglesPerSpan = 64;
    private static readonly int[][] TextureDescriptorMatrices =
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
    private static readonly int[][] CollisionPointOrders =
    [
        [0, 1, 2],
        [0, 2, 1],
        [1, 0, 2],
        [1, 2, 0],
        [2, 0, 1],
        [2, 1, 0]
    ];

    public static int TextureAssetWadIndexForLevel(LevelDefinition level)
    {
        if (level.SourceWadEntry >= 0)
            return level.SourceWadEntry;

        throw new InvalidOperationException($"Level {level.DisplayName} does not have a source WAD entry that can be mapped to a texture/model asset.");
    }

    public static IReadOnlyList<TerrainTextureSlot> InspectTextureSlots(string sourceImagePath, LevelDefinition level)
    {
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        int textureAssetWadIndex = TextureAssetWadIndexForLevel(level);
        using FileStream imageStream = File.OpenRead(sourceImagePath);
        AssetSubfileInfo texturePagesInfo = GetAssetSubfileInfo(imageStream, layout, textureAssetWadIndex, TexturePagesSubfileIndex);
        AssetSubfileInfo modelInfo = GetAssetSubfileInfo(imageStream, layout, textureAssetWadIndex, ModelSubfileIndex);
        byte[] modelBytes = ReadWadBytes(imageStream, layout, modelInfo.AbsoluteWadOffset, checked((int)modelInfo.SubfileSize));
        return BuildTerrainTextureSlots(
            DecodeTextureRecords(modelBytes),
            texturePagesInfo.SubfileSize);
    }

    /// <summary>
    /// Reads texture-slot readiness after applying the retail level's exact
    /// load-time animation and scrolling initialization. Several native
    /// records are intentionally incomplete on disc and are completed by the
    /// level loader before their first render; treating only the raw table as
    /// donor truth incorrectly greys out otherwise complete native art.
    /// </summary>
    public static IReadOnlyList<TerrainTextureSlot> InspectInitializedTextureSlots(
        string sourceImagePath,
        LevelDefinition level,
        NativeTerrainTextureRuntimeControlAudit runtimeControlAudit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(runtimeControlAudit);
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);
        if (!runtimeControlAudit.Complete)
        {
            throw new InvalidDataException(
                runtimeControlAudit.SafetyBlockers.FirstOrDefault() ??
                "Runtime texture-control inspection is incomplete.");
        }
        if (runtimeControlAudit.TargetWadEntry != level.SourceWadEntry)
        {
            throw new InvalidDataException(
                $"Runtime-control audit WAD entry {runtimeControlAudit.TargetWadEntry} does not match {level.DisplayName} WAD entry {level.SourceWadEntry}.");
        }

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        int textureAssetWadIndex = TextureAssetWadIndexForLevel(level);
        using FileStream imageStream = File.OpenRead(sourceImagePath);
        AssetSubfileInfo texturePagesInfo = GetAssetSubfileInfo(
            imageStream,
            layout,
            textureAssetWadIndex,
            TexturePagesSubfileIndex);
        AssetSubfileInfo modelInfo = GetAssetSubfileInfo(
            imageStream,
            layout,
            textureAssetWadIndex,
            ModelSubfileIndex);
        byte[] modelBytes = ReadWadBytes(
            imageStream,
            layout,
            modelInfo.AbsoluteWadOffset,
            checked((int)modelInfo.SubfileSize));
        NativeTerrainTextureInitialStateResult initialState =
            NativeTerrainTextureRuntimeControlScanner.InitializeTextureRecords(
                runtimeControlAudit,
                modelBytes);
        if (!initialState.Complete)
        {
            throw new InvalidDataException(
                initialState.SafetyBlockers.FirstOrDefault() ??
                "Native terrain texture load-state initialization was incomplete.");
        }

        return BuildTerrainTextureSlots(
            DecodeTextureRecords(initialState.InitializedTextureData),
            texturePagesInfo.SubfileSize);
    }

    private static IReadOnlyList<TerrainTextureSlot> BuildTerrainTextureSlots(
        TextureRecordIndex textureIndex,
        long texturePagesByteLength)
    {
        return textureIndex.Records
            .Select(record =>
            {
                int normalCount = CountReadableHqTextureImageDescriptors(
                    record.HqData,
                    texturePagesByteLength);
                int closeCount = CountReadableHqTextureImageDescriptors(
                    record.HqDataClose,
                    texturePagesByteLength);
                bool hasNormal = TryBuildReadableTextureImageTier(
                    "hqData",
                    record.HqData,
                    2,
                    texturePagesByteLength) != null;
                bool hasClose = TryBuildReadableTextureImageTier(
                    "hqDataClose",
                    record.HqDataClose,
                    4,
                    texturePagesByteLength) != null;
                return new TerrainTextureSlot(
                    TextureId: record.TextureId,
                    HasNormalDescriptors: hasNormal,
                    HasCloseDescriptors: hasClose,
                    NormalDescriptorCount: normalCount,
                    CloseDescriptorCount: closeCount,
                    NormalTopologySignature: hasNormal
                        ? BuildDescriptorTopologySignature(record.HqData, closeTier: false, texturePagesByteLength)
                        : "",
                    CloseTopologySignature: hasClose
                        ? BuildDescriptorTopologySignature(record.HqDataClose, closeTier: true, texturePagesByteLength)
                        : "",
                    CombinedTopologySignature: hasNormal && hasClose
                        ? BuildReadableHqCombinedTopologySignature(record, texturePagesByteLength, out _)
                        : "");
            })
            .ToArray();
    }

    public static TerrainTextureStorageIsolation InspectTextureStorageIsolation(
        string sourceImagePath,
        LevelDefinition level,
        int targetTextureId,
        string descriptorTier,
        IEnumerable<int> residentTextureIds)
    {
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        int textureAssetWadIndex = TextureAssetWadIndexForLevel(level);
        using FileStream imageStream = File.OpenRead(sourceImagePath);
        AssetSubfileInfo texturePagesInfo = GetAssetSubfileInfo(imageStream, layout, textureAssetWadIndex, TexturePagesSubfileIndex);
        AssetSubfileInfo modelInfo = GetAssetSubfileInfo(imageStream, layout, textureAssetWadIndex, ModelSubfileIndex);
        byte[] modelBytes = ReadWadBytes(imageStream, layout, modelInfo.AbsoluteWadOffset, checked((int)modelInfo.SubfileSize));
        TextureRecordIndex textureIndex = DecodeTextureRecords(modelBytes);
        if (targetTextureId < 0 || targetTextureId >= textureIndex.TextureCount)
            throw new ArgumentOutOfRangeException(nameof(targetTextureId), $"Texture {targetTextureId} is outside {level.DisplayName}'s decoded {textureIndex.TextureCount}-texture table.");

        IReadOnlyList<string> targetTiers = ExpandDescriptorTiers(descriptorTier);
        if (!TryBuildTexturePhysicalNibbleLayout(
                textureIndex.Records[targetTextureId],
                targetTiers,
                texturePagesInfo.SubfileSize,
                out TexturePhysicalNibbleReference[] targetReferences,
                out _,
                out string targetFailure))
        {
            throw new InvalidOperationException($"Texture {targetTextureId} storage could not be inspected: {targetFailure}");
        }

        Dictionary<(long RelativeOffset, int Nibble), int> targetKinds = BuildPhysicalNibbleKindMap(targetReferences);
        List<TerrainTextureStorageOverlap> overlaps = [];
        List<int> unreadableResidentTextureIds = [];
        HashSet<(long RelativeOffset, int Nibble)> allOverlappingNibbles = [];
        int[] residents = residentTextureIds
            .Where(textureId => textureId >= 0 && textureId < textureIndex.TextureCount && textureId != targetTextureId)
            .Distinct()
            .Order()
            .ToArray();
        foreach (int residentTextureId in residents)
        {
            TextureRecord residentRecord = textureIndex.Records[residentTextureId];
            List<TexturePhysicalNibbleReference> residentReferences = [];
            bool readableTier = false;
            if (residentRecord.HqData.Count > 0 &&
                TryBuildTexturePhysicalNibbleLayout(
                    residentRecord,
                    ["hqData"],
                    texturePagesInfo.SubfileSize,
                    out TexturePhysicalNibbleReference[] normalReferences,
                    out _,
                    out _))
            {
                residentReferences.AddRange(normalReferences);
                readableTier = true;
            }

            if (residentRecord.HqDataClose.Count > 0 &&
                TryBuildTexturePhysicalNibbleLayout(
                    residentRecord,
                    ["hqDataClose"],
                    texturePagesInfo.SubfileSize,
                    out TexturePhysicalNibbleReference[] closeReferences,
                    out _,
                    out _))
            {
                residentReferences.AddRange(closeReferences);
                readableTier = true;
            }

            if (!readableTier)
            {
                unreadableResidentTextureIds.Add(residentTextureId);
                continue;
            }

            Dictionary<(long RelativeOffset, int Nibble), int> residentKinds = BuildPhysicalNibbleKindMap(residentReferences);
            (long RelativeOffset, int Nibble)[] overlappingNibbles = targetKinds.Keys
                .Where(residentKinds.ContainsKey)
                .ToArray();
            if (overlappingNibbles.Length == 0)
                continue;

            allOverlappingNibbles.UnionWith(overlappingNibbles);
            overlaps.Add(new TerrainTextureStorageOverlap(
                ResidentTextureId: residentTextureId,
                PhysicalNibbleCount: overlappingNibbles.Length,
                TargetPaletteNibbleCount: overlappingNibbles.Count(key => (targetKinds[key] & 1) != 0),
                TargetPixelNibbleCount: overlappingNibbles.Count(key => (targetKinds[key] & 2) != 0),
                ResidentPaletteNibbleCount: overlappingNibbles.Count(key => (residentKinds[key] & 1) != 0),
                ResidentPixelNibbleCount: overlappingNibbles.Count(key => (residentKinds[key] & 2) != 0)));
        }

        return new TerrainTextureStorageIsolation(
            TargetTextureId: targetTextureId,
            DescriptorTier: string.Join("+", targetTiers),
            TargetPhysicalNibbleCount: targetKinds.Count,
            ResidentTextureCount: residents.Length,
            OverlappingResidentTextureCount: overlaps.Count,
            OverlappingPhysicalNibbleCount: allOverlappingNibbles.Count,
            Overlaps: overlaps.OrderByDescending(overlap => overlap.PhysicalNibbleCount).ThenBy(overlap => overlap.ResidentTextureId).ToArray(),
            UnreadableResidentTextureIds: unreadableResidentTextureIds);
    }

    public static async Task<TerrainTextureImageExport?> TryExportTerrainTextureImageAsync(
        string sourceImagePath,
        LevelDefinition level,
        int textureId,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        return await TryExportTerrainTextureImageCoreAsync(
            sourceImagePath,
            level,
            textureId,
            outputPath,
            preferredDescriptorTier: "",
            cancellationToken);
    }

    public static async Task<TerrainTextureImageExport?> TryExportTerrainTextureImageTierAsync(
        string sourceImagePath,
        LevelDefinition level,
        int textureId,
        string outputPath,
        string preferredDescriptorTier,
        CancellationToken cancellationToken = default)
    {
        return await TryExportTerrainTextureImageCoreAsync(
            sourceImagePath,
            level,
            textureId,
            outputPath,
            preferredDescriptorTier,
            cancellationToken);
    }

    /// <summary>
    /// Decodes a set of resident terrain textures while reading the level's
    /// texture pages and descriptor table only once. This is intentionally a
    /// preview/cache operation: it never changes the source image.
    /// </summary>
    public static async Task<IReadOnlyList<TerrainTextureImageExport>> ExportTerrainTextureImagesAsync(
        string sourceImagePath,
        LevelDefinition level,
        IEnumerable<int> textureIds,
        string outputDirectory,
        bool overwrite = false,
        string preferredDescriptorTier = "hqData",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(textureIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);

        HashSet<int> requested = textureIds
            .Where(textureId => textureId >= 0)
            .ToHashSet();
        if (requested.Count == 0)
            return Array.Empty<TerrainTextureImageExport>();

        Directory.CreateDirectory(outputDirectory);
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        int textureAssetWadIndex = TextureAssetWadIndexForLevel(level);
        using FileStream imageStream = File.OpenRead(sourceImagePath);
        AssetSubfileInfo texturePagesInfo = GetAssetSubfileInfo(imageStream, layout, textureAssetWadIndex, TexturePagesSubfileIndex);
        AssetSubfileInfo modelInfo = GetAssetSubfileInfo(imageStream, layout, textureAssetWadIndex, ModelSubfileIndex);
        byte[] texturePages = ReadWadBytes(imageStream, layout, texturePagesInfo.AbsoluteWadOffset, checked((int)texturePagesInfo.SubfileSize));
        byte[] modelBytes = ReadWadBytes(imageStream, layout, modelInfo.AbsoluteWadOffset, checked((int)modelInfo.SubfileSize));
        TextureRecordIndex textureIndex = DecodeTextureRecords(modelBytes);

        List<TerrainTextureImageExport> exports = [];
        foreach (TextureRecord record in textureIndex.Records.Where(record => requested.Contains(record.TextureId)).OrderBy(record => record.TextureId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            (string Tier, int TileSide, int TileGridColumns, int ImageSize, IReadOnlyList<TextureDescriptor> Descriptors)? tier =
                ChooseReadableTextureDescriptorTier(record, texturePagesInfo.SubfileSize, preferredDescriptorTier);
            if (tier == null)
                continue;

            string outputPath = Path.Combine(outputDirectory, $"{level.Key}-texture-{record.TextureId:000}.png");
            if (!overwrite && File.Exists(outputPath))
            {
                exports.Add(new TerrainTextureImageExport(
                    record.TextureId,
                    tier.Value.Tier,
                    tier.Value.ImageSize,
                    tier.Value.ImageSize,
                    tier.Value.Descriptors.Count,
                    tier.Value.ImageSize * tier.Value.ImageSize));
                continue;
            }

            if (!TryDecodeTerrainTextureImage(texturePages, record.TextureId, tier.Value, out Rgba32[] pixels, out TerrainTextureImageExport export))
                continue;

            await TerrainTexturePngWriter.WriteRgbaAsync(
                outputPath,
                export.Width,
                export.Height,
                pixels,
                cancellationToken);
            exports.Add(export);
        }

        return exports;
    }

    /// <summary>
    /// Decodes both native high-detail terrain texture descriptor banks while
    /// reading the level assets only once. Unlike the single-tier preview API,
    /// this method never substitutes one tier for the other: an unreadable
    /// normal or close bank is omitted so the viewport can make an explicit,
    /// truthful fallback choice.
    /// </summary>
    public static async Task<IReadOnlyList<TerrainTextureImageExport>> ExportTerrainTextureImageTiersAsync(
        string sourceImagePath,
        LevelDefinition level,
        IEnumerable<int> textureIds,
        string outputDirectory,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(textureIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);

        HashSet<int> requested = textureIds
            .Where(textureId => textureId >= 0)
            .ToHashSet();
        if (requested.Count == 0)
            return Array.Empty<TerrainTextureImageExport>();

        Directory.CreateDirectory(outputDirectory);
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        int textureAssetWadIndex = TextureAssetWadIndexForLevel(level);
        using FileStream imageStream = File.OpenRead(sourceImagePath);
        AssetSubfileInfo texturePagesInfo = GetAssetSubfileInfo(imageStream, layout, textureAssetWadIndex, TexturePagesSubfileIndex);
        AssetSubfileInfo modelInfo = GetAssetSubfileInfo(imageStream, layout, textureAssetWadIndex, ModelSubfileIndex);
        byte[] texturePages = ReadWadBytes(imageStream, layout, texturePagesInfo.AbsoluteWadOffset, checked((int)texturePagesInfo.SubfileSize));
        byte[] modelBytes = ReadWadBytes(imageStream, layout, modelInfo.AbsoluteWadOffset, checked((int)modelInfo.SubfileSize));
        TextureRecordIndex textureIndex = DecodeTextureRecords(modelBytes);

        List<TerrainTextureImageExport> exports = [];
        foreach (TextureRecord record in textureIndex.Records.Where(record => requested.Contains(record.TextureId)).OrderBy(record => record.TextureId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<(string Tier, int TileSide, int TileGridColumns, int ImageSize, IReadOnlyList<TextureDescriptor> Descriptors)> tiers = [];
            var normal = TryBuildReadableTextureImageTier("hqData", record.HqData, 2, texturePagesInfo.SubfileSize);
            var close = TryBuildReadableTextureImageTier("hqDataClose", record.HqDataClose, 4, texturePagesInfo.SubfileSize);
            if (normal.HasValue)
                tiers.Add(normal.Value);
            if (close.HasValue)
                tiers.Add(close.Value);

            foreach (var tier in tiers)
            {
                NativeTerrainTexturePreviewTier previewTier = NativeTerrainTexturePreviewLod.FromDescriptorTier(tier.Tier);
                string suffix = NativeTerrainTexturePreviewLod.FileSuffix(previewTier);
                string outputPath = Path.Combine(outputDirectory, $"{level.Key}-texture-{record.TextureId:000}-{suffix}.png");
                if (!overwrite && File.Exists(outputPath))
                {
                    exports.Add(new TerrainTextureImageExport(
                        record.TextureId,
                        tier.Tier,
                        tier.ImageSize,
                        tier.ImageSize,
                        tier.Descriptors.Count,
                        tier.ImageSize * tier.ImageSize));
                    continue;
                }

                if (!TryDecodeTerrainTextureImage(texturePages, record.TextureId, tier, out Rgba32[] pixels, out TerrainTextureImageExport export))
                    continue;

                await TerrainTexturePngWriter.WriteRgbaAsync(
                    outputPath,
                    export.Width,
                    export.Height,
                    pixels,
                    cancellationToken);
                exports.Add(export);
            }
        }

        return exports;
    }

    /// <summary>
    /// Reads the native texture component and applies the exact load-time
    /// animation/scroll initialization without writing files or changing the
    /// source disc. This represents func_8002B4AC state, not live gameplay.
    /// </summary>
    public static NativeTerrainTextureInitialStateResult InspectTerrainTextureInitialState(
        string sourceImagePath,
        LevelDefinition level,
        NativeTerrainTextureRuntimeControlAudit runtimeControlAudit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(runtimeControlAudit);
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);
        if (runtimeControlAudit.TargetWadEntry != level.SourceWadEntry)
        {
            throw new InvalidDataException(
                $"Runtime-control audit WAD entry {runtimeControlAudit.TargetWadEntry} does not match {level.DisplayName} WAD entry {level.SourceWadEntry}.");
        }

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        int textureAssetWadIndex = TextureAssetWadIndexForLevel(level);
        using FileStream imageStream = File.OpenRead(sourceImagePath);
        AssetSubfileInfo modelInfo = GetAssetSubfileInfo(imageStream, layout, textureAssetWadIndex, ModelSubfileIndex);
        byte[] modelBytes = ReadWadBytes(
            imageStream,
            layout,
            modelInfo.AbsoluteWadOffset,
            checked((int)modelInfo.SubfileSize));
        return NativeTerrainTextureRuntimeControlScanner.InitializeTextureRecords(runtimeControlAudit, modelBytes);
    }

    /// <summary>
    /// Exports both HQ descriptor tiers from the exact native load-initialized
    /// texture table. Controlled destinations are therefore truthful at t=0;
    /// this method intentionally does not emulate later playback or script calls.
    /// </summary>
    public static async Task<NativeTerrainTextureInitialStateImageExport> ExportTerrainTextureInitialStateImageTiersAsync(
        string sourceImagePath,
        LevelDefinition level,
        NativeTerrainTextureRuntimeControlAudit runtimeControlAudit,
        IEnumerable<int> textureIds,
        string outputDirectory,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(runtimeControlAudit);
        ArgumentNullException.ThrowIfNull(textureIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        if (!runtimeControlAudit.Complete)
        {
            throw new InvalidDataException(
                runtimeControlAudit.SafetyBlockers.FirstOrDefault() ??
                "Runtime texture-control audit is incomplete; initial-state preview export is blocked.");
        }
        if (runtimeControlAudit.TargetWadEntry != level.SourceWadEntry)
        {
            throw new InvalidDataException(
                $"Runtime-control audit WAD entry {runtimeControlAudit.TargetWadEntry} does not match {level.DisplayName} WAD entry {level.SourceWadEntry}.");
        }
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);

        HashSet<int> requested = textureIds
            .Where(textureId => textureId >= 0)
            .ToHashSet();
        if (requested.Count == 0)
        {
            NativeTerrainTextureInitialStateResult emptyState = InspectTerrainTextureInitialState(
                sourceImagePath,
                level,
                runtimeControlAudit);
            return new NativeTerrainTextureInitialStateImageExport(emptyState, Array.Empty<TerrainTextureImageExport>());
        }

        Directory.CreateDirectory(outputDirectory);
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        int textureAssetWadIndex = TextureAssetWadIndexForLevel(level);
        using FileStream imageStream = File.OpenRead(sourceImagePath);
        AssetSubfileInfo texturePagesInfo = GetAssetSubfileInfo(imageStream, layout, textureAssetWadIndex, TexturePagesSubfileIndex);
        AssetSubfileInfo modelInfo = GetAssetSubfileInfo(imageStream, layout, textureAssetWadIndex, ModelSubfileIndex);
        byte[] texturePages = ReadWadBytes(
            imageStream,
            layout,
            texturePagesInfo.AbsoluteWadOffset,
            checked((int)texturePagesInfo.SubfileSize));
        byte[] modelBytes = ReadWadBytes(
            imageStream,
            layout,
            modelInfo.AbsoluteWadOffset,
            checked((int)modelInfo.SubfileSize));
        NativeTerrainTextureInitialStateResult initialState =
            NativeTerrainTextureRuntimeControlScanner.InitializeTextureRecords(runtimeControlAudit, modelBytes);
        if (!initialState.Complete)
        {
            throw new InvalidDataException(
                initialState.SafetyBlockers.FirstOrDefault() ??
                "Native terrain texture load-state initialization was incomplete.");
        }

        NativeTerrainLqTextureDecodeResult lowDetail =
            NativeTerrainLqTextureCacheCodec.DecodeNativeLoadState(
                initialState,
                texturePages,
                requested);
        if (!lowDetail.Complete || lowDetail.TextureSet == null)
        {
            throw new InvalidDataException(
                lowDetail.SafetyBlockers.FirstOrDefault() ??
                "Native terrain TexLq load-state decoding was incomplete.");
        }

        NativeTerrainHqMaterialDecodeResult highDetailMaterials =
            NativeTerrainHqMaterialCacheCodec.DecodeNativeLoadState(
                initialState,
                texturePages,
                requested);
        if (!highDetailMaterials.Complete || highDetailMaterials.MaterialSet == null)
        {
            throw new InvalidDataException(
                highDetailMaterials.SafetyBlockers.FirstOrDefault() ??
                "Native terrain HQ PSX555/STP material decoding was incomplete.");
        }

        TextureRecordIndex textureIndex = DecodeTextureRecords(initialState.InitializedTextureData);
        List<TerrainTextureImageExport> exports = [];
        foreach (TextureRecord record in textureIndex.Records
                     .Where(record => requested.Contains(record.TextureId))
                     .OrderBy(record => record.TextureId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<(string Tier, int TileSide, int TileGridColumns, int ImageSize, IReadOnlyList<TextureDescriptor> Descriptors)> tiers = [];
            var normal = TryBuildReadableTextureImageTier("hqData", record.HqData, 2, texturePagesInfo.SubfileSize);
            var close = TryBuildReadableTextureImageTier("hqDataClose", record.HqDataClose, 4, texturePagesInfo.SubfileSize);
            if (normal.HasValue)
                tiers.Add(normal.Value);
            if (close.HasValue)
                tiers.Add(close.Value);

            foreach (var tier in tiers)
            {
                NativeTerrainTexturePreviewTier previewTier = NativeTerrainTexturePreviewLod.FromDescriptorTier(tier.Tier);
                string suffix = NativeTerrainTexturePreviewLod.FileSuffix(previewTier);
                string outputPath = Path.Combine(outputDirectory, $"{level.Key}-texture-{record.TextureId:000}-{suffix}.png");
                if (!overwrite && File.Exists(outputPath))
                {
                    exports.Add(new TerrainTextureImageExport(
                        record.TextureId,
                        tier.Tier,
                        tier.ImageSize,
                        tier.ImageSize,
                        tier.Descriptors.Count,
                        tier.ImageSize * tier.ImageSize));
                    continue;
                }

                if (!TryDecodeTerrainTextureImage(texturePages, record.TextureId, tier, out Rgba32[] pixels, out TerrainTextureImageExport export))
                    continue;

                await TerrainTexturePngWriter.WriteRgbaAsync(
                    outputPath,
                    export.Width,
                    export.Height,
                    pixels,
                    cancellationToken);
                exports.Add(export);
            }
        }

        return new NativeTerrainTextureInitialStateImageExport(
            initialState,
            exports,
            lowDetail.TextureSet,
            highDetailMaterials.MaterialSet);
    }

    private static async Task<TerrainTextureImageExport?> TryExportTerrainTextureImageCoreAsync(
        string sourceImagePath,
        LevelDefinition level,
        int textureId,
        string outputPath,
        string preferredDescriptorTier,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(sourceImagePath) || textureId < 0)
            return null;

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        int textureAssetWadIndex = TextureAssetWadIndexForLevel(level);
        using FileStream imageStream = File.OpenRead(sourceImagePath);
        AssetSubfileInfo texturePagesInfo = GetAssetSubfileInfo(imageStream, layout, textureAssetWadIndex, TexturePagesSubfileIndex);
        AssetSubfileInfo modelInfo = GetAssetSubfileInfo(imageStream, layout, textureAssetWadIndex, ModelSubfileIndex);
        byte[] texturePages = ReadWadBytes(imageStream, layout, texturePagesInfo.AbsoluteWadOffset, checked((int)texturePagesInfo.SubfileSize));
        byte[] modelBytes = ReadWadBytes(imageStream, layout, modelInfo.AbsoluteWadOffset, checked((int)modelInfo.SubfileSize));
        TextureRecordIndex textureIndex = DecodeTextureRecords(modelBytes);
        TextureRecord? record = textureIndex.Records.FirstOrDefault(candidate => candidate.TextureId == textureId);
        if (record == null)
            return null;

        (string Tier, int TileSide, int TileGridColumns, int ImageSize, IReadOnlyList<TextureDescriptor> Descriptors)? tier = ChooseReadableTextureDescriptorTier(
            record,
            texturePagesInfo.SubfileSize,
            preferredDescriptorTier);
        if (tier == null)
            return null;

        if (!TryDecodeTerrainTextureImage(texturePages, textureId, tier.Value, out Rgba32[] pixels, out TerrainTextureImageExport export))
            return null;

        await TerrainTexturePngWriter.WriteRgbaAsync(outputPath, export.Width, export.Height, pixels, cancellationToken);
        return export;
    }

    private static bool TryDecodeTerrainTextureImage(
        byte[] texturePages,
        int textureId,
        (string Tier, int TileSide, int TileGridColumns, int ImageSize, IReadOnlyList<TextureDescriptor> Descriptors) tier,
        out Rgba32[] pixels,
        out TerrainTextureImageExport export)
    {
        pixels = new Rgba32[tier.ImageSize * tier.ImageSize];
        export = null!;
        int copiedPixels = 0;
        foreach (TextureDescriptor descriptor in tier.Descriptors)
        {
            int destTileX = (descriptor.Index % tier.TileGridColumns) * tier.TileSide;
            int destTileY = (descriptor.Index / tier.TileGridColumns) * tier.TileSide;
            for (int y = 0; y < tier.TileSide; y++)
            {
                for (int x = 0; x < tier.TileSide; x++)
                {
                    if (!TryGetHqTextureImageSampleAddress(
                            descriptor,
                            x,
                            y,
                            tier.TileSide,
                            texturePages.Length,
                            out long relativeOffset,
                            out int nibble))
                    {
                        return false;
                    }

                    int packedPixel = texturePages[checked((int)relativeOffset)];
                    int paletteIndex = descriptor.BitsPerPixel == 8
                        ? packedPixel
                        : (packedPixel >> (nibble * 4)) & 0x0F;
                    int paletteOffset = descriptor.PaletteByteStart + (paletteIndex * 2);
                    if (paletteOffset < 0 || paletteOffset + 2 > texturePages.Length)
                        return false;

                    int destIndex = ((destTileY + y) * tier.ImageSize) + destTileX + x;
                    pixels[destIndex] = ConvertPsx555ToRgba32(ReadUInt16(texturePages, paletteOffset));
                    copiedPixels++;
                }
            }
        }

        if (copiedPixels != pixels.Length)
            return false;

        export = new TerrainTextureImageExport(
            TextureId: textureId,
            DescriptorTier: tier.Tier,
            Width: tier.ImageSize,
            Height: tier.ImageSize,
            DescriptorCount: tier.Descriptors.Count,
            PixelCount: copiedPixels);
        return true;
    }

    public static TerrainTextureSlot? FindUnusedTextureSlot(
        string sourceImagePath,
        LevelDefinition level,
        IEnumerable<int> usedTextureIds,
        bool preferBothDescriptorTiers = true,
        string requiredNormalTopologySignature = "",
        string requiredCloseTopologySignature = "",
        string requiredCombinedTopologySignature = "")
    {
        HashSet<int> used = usedTextureIds.Where(textureId => textureId >= 0).ToHashSet();
        IReadOnlyList<TerrainTextureSlot> slots = InspectTextureSlots(sourceImagePath, level);
        IEnumerable<TerrainTextureSlot> unused = slots
            .Where(slot => !used.Contains(slot.TextureId))
            .Where(slot => string.IsNullOrWhiteSpace(requiredNormalTopologySignature) ||
                string.Equals(slot.NormalTopologySignature, requiredNormalTopologySignature, StringComparison.Ordinal))
            .Where(slot => string.IsNullOrWhiteSpace(requiredCloseTopologySignature) ||
                string.Equals(slot.CloseTopologySignature, requiredCloseTopologySignature, StringComparison.Ordinal))
            .Where(slot => string.IsNullOrWhiteSpace(requiredCombinedTopologySignature) ||
                string.Equals(slot.CombinedTopologySignature, requiredCombinedTopologySignature, StringComparison.Ordinal));

        if (preferBothDescriptorTiers)
        {
            TerrainTextureSlot? both = unused
                .Where(slot => slot.HasNormalDescriptors && slot.HasCloseDescriptors)
                .OrderBy(slot => slot.TextureId)
                .FirstOrDefault();
            if (both != null)
                return both;
        }

        return unused
            .Where(slot => slot.HasNormalDescriptors || slot.HasCloseDescriptors)
            .OrderByDescending(slot => slot.HasNormalDescriptors && slot.HasCloseDescriptors)
            .ThenBy(slot => slot.TextureId)
            .FirstOrDefault();
    }

    public static async Task<TerrainPatchResult> ExportAsync(TerrainPatchRequest request, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", request.SourceImagePath);
        bool hasTerrainEdits = File.Exists(request.TerrainEditsPath);
        bool hasCustomTextures = !string.IsNullOrWhiteSpace(request.CustomTexturesPath) && File.Exists(request.CustomTexturesPath);
        bool hasNativeTextureRelocations = !string.IsNullOrWhiteSpace(request.NativeTextureRelocationsPath) &&
            File.Exists(request.NativeTextureRelocationsPath);
        if (!hasTerrainEdits && !hasCustomTextures && !hasNativeTextureRelocations)
            throw new FileNotFoundException("Missing terrain edit file.", request.TerrainEditsPath);

        string outputPrefix = string.IsNullOrWhiteSpace(request.OutputPrefix)
            ? Path.Combine(Path.GetDirectoryName(request.SourceImagePath) ?? "", $"Spyro the Dragon (USA)-{request.Level.Key}-terrain-edits")
            : request.OutputPrefix;
        string outputImagePath = $"{outputPrefix}.bin";
        string outputCuePath = $"{outputPrefix}.cue";
        string outputPlanPath = $"{outputPrefix}.terrain-patch-plan.json";

        if (request.WriteImage)
        {
            DeleteStaleOutput(outputImagePath);
            DeleteStaleOutput(outputCuePath);
            DeleteStaleOutput(outputPlanPath);
        }

        TerrainPatchPlan plan = await Task.Run(
            () => BuildPlan(
                request.SourceImagePath,
                request.SourceCuePath,
                outputImagePath,
                outputCuePath,
                request.Level,
                request.RamPath,
                request.SourceSearchPath,
                request.TerrainEditsPath,
                request.CustomTexturesPath,
                request.NativeTextureRelocationsPath),
            cancellationToken);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPlanPath) ?? ".");
        await File.WriteAllTextAsync(outputPlanPath, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);

        string[] atomicTerrainSwapBlocks = plan.SkippedEdits
            .Where(IsAtomicTerrainSwapBlock)
            .ToArray();
        if (request.WriteImage && atomicTerrainSwapBlocks.Length > 0)
        {
            DeleteStaleOutput(outputImagePath);
            DeleteStaleOutput(outputCuePath);
            string examples = string.Join(" | ", atomicTerrainSwapBlocks
                .Take(2)
                .Select(RemoveAtomicTerrainSwapBlockPrefix));
            string more = atomicTerrainSwapBlocks.Length > 2
                ? $" (+{atomicTerrainSwapBlocks.Length - 2} more; see the terrain patch plan)"
                : "";
            throw new InvalidOperationException(
                $"Terrain texture/property swap export was stopped before writing a BIN because one requested part could not be applied atomically: {examples}{more}");
        }

        if (request.WriteImage && plan.PatchCount == 0)
        {
            DeleteStaleOutput(outputImagePath);
            DeleteStaleOutput(outputCuePath);
        }
        else if (request.WriteImage)
        {
            DeleteStaleOutput(outputImagePath);
            DeleteStaleOutput(outputCuePath);
            string temporaryOutputImagePath = $"{outputImagePath}.{Guid.NewGuid():N}.tmp";
            string temporaryOutputCuePath = $"{outputCuePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                await DiscImageWorkingCopy.StageAsync(
                    request.SourceImagePath,
                    temporaryOutputImagePath,
                    request.ConsumeDisposableSourceImage,
                    cancellationToken);
                DiscLayout layout = DiscImage.DetectLayout(temporaryOutputImagePath);
                await using (FileStream stream = File.Open(
                                 temporaryOutputImagePath,
                                 FileMode.Open,
                                 FileAccess.ReadWrite,
                                 FileShare.Read))
                {
                    foreach (TerrainPatch patch in plan.Patches)
                    {
                        long wadOffset = ParseRequiredLong(patch.WadRelativeOffset, "patch.wadRelativeOffset");
                        byte[] before = HexToBytes(patch.BeforeHexPreview);
                        byte[] after = HexToBytes(patch.AfterHexPreview);
                        byte[] actualBefore = DiscImage.ReadFileBytes(stream, layout, WadLba, wadOffset, before.Length);
                        if (!actualBefore.SequenceEqual(before))
                        {
                            throw new InvalidDataException(
                                $"The temporary output copy no longer matches source-bound before bytes for {patch.Kind} at WAD offset 0x{wadOffset:X}.");
                        }
                        DiscImage.WriteFileBytes(stream, layout, WadLba, wadOffset, after);
                    }
                    await stream.FlushAsync(cancellationToken);
                    foreach (TerrainPatch patch in plan.Patches)
                    {
                        long wadOffset = ParseRequiredLong(patch.WadRelativeOffset, "patch.wadRelativeOffset");
                        byte[] expectedAfter = HexToBytes(patch.AfterHexPreview);
                        byte[] actualAfter = DiscImage.ReadFileBytes(stream, layout, WadLba, wadOffset, expectedAfter.Length);
                        if (!actualAfter.SequenceEqual(expectedAfter))
                        {
                            throw new InvalidDataException(
                                $"Final temporary-BIN readback failed for {patch.Kind} at WAD offset 0x{wadOffset:X}.");
                        }
                    }
                }

                string cueText = DiscImage.BuildCueText(request.SourceCuePath, Path.GetFileName(outputImagePath));
                await File.WriteAllTextAsync(temporaryOutputCuePath, cueText, Encoding.ASCII, cancellationToken);
                File.Move(temporaryOutputImagePath, outputImagePath, overwrite: true);
                File.Move(temporaryOutputCuePath, outputCuePath, overwrite: true);
            }
            catch
            {
                DeleteStaleOutput(outputImagePath);
                DeleteStaleOutput(outputCuePath);
                throw;
            }
            finally
            {
                DeleteStaleOutput(temporaryOutputImagePath);
                DeleteStaleOutput(temporaryOutputCuePath);
            }
        }

        return new TerrainPatchResult(outputImagePath, outputCuePath, outputPlanPath, plan, request.WriteImage && plan.PatchCount > 0);
    }

    private static void DeleteStaleOutput(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static bool IsAtomicTerrainSwapBlock(string reason) =>
        !string.IsNullOrWhiteSpace(reason) &&
        reason.StartsWith(AtomicTerrainSwapBlockPrefix, StringComparison.Ordinal);

    private static string RemoveAtomicTerrainSwapBlockPrefix(string reason) =>
        IsAtomicTerrainSwapBlock(reason)
            ? reason[AtomicTerrainSwapBlockPrefix.Length..]
            : reason;

    private static void AddAtomicTerrainSwapBlock(List<string> skippedEdits, string reason)
    {
        skippedEdits.Add($"{AtomicTerrainSwapBlockPrefix}{reason}");
    }

    private static bool IsAtomicTerrainFaceSwapEdit(JsonElement edit)
    {
        if (JsonValue.GetBoolean(edit, "nativeSurfaceBehaviorEdit") ||
            JsonValue.GetBoolean(edit, "nativeTextureVisualEdit"))
            return true;

        int editedTextureId = JsonValue.GetInt32(edit, "textureIdEdited", -1);
        int originalTextureId = JsonValue.GetInt32(edit, "textureIdOriginal", -1);
        return editedTextureId >= 0 && editedTextureId != originalTextureId;
    }

    private static void AddNativeTextureRelocationFaceEditConflicts(
        JsonElement editsElement,
        IReadOnlyDictionary<int, NativeTerrainTextureRelocationEdit> relocationsByTarget,
        List<string> skippedEdits)
    {
        if (relocationsByTarget.Count == 0)
            return;

        foreach (JsonElement edit in editsElement.EnumerateArray())
        {
            string runtimeKey = JsonValue.GetString(edit, "runtimeKey", "unknown face");
            int originalTextureId = JsonValue.GetInt32(
                edit,
                "textureIdOriginal",
                JsonValue.GetInt32(edit, "textureId", -1));
            int editedTextureId = JsonValue.GetInt32(edit, "textureIdEdited", originalTextureId);
            bool textureChanged = originalTextureId >= 0 && editedTextureId >= 0 && editedTextureId != originalTextureId;
            bool hasNativeVisual = JsonValue.GetBoolean(edit, "nativeTextureVisualEdit");
            bool hasNativeBehavior = JsonValue.GetBoolean(edit, "nativeSurfaceBehaviorEdit");
            if (!textureChanged && !hasNativeVisual && !hasNativeBehavior)
                continue;

            int[] touchedTextureIds = new[] { originalTextureId, editedTextureId }
                .Where(textureId => textureId >= 0 && relocationsByTarget.ContainsKey(textureId))
                .Distinct()
                .ToArray();
            foreach (int targetTextureId in touchedTextureIds)
            {
                NativeTerrainTextureRelocationEdit relocation = relocationsByTarget[targetTextureId];
                string behaviorSourceLevelKey = JsonValue.GetString(edit, "nativeSurfaceSourceLevelKey");
                bool isRelocationBehaviorAssignment =
                    !relocation.PreservesTargetNativeSurface &&
                    !textureChanged &&
                    !hasNativeVisual &&
                    hasNativeBehavior &&
                    originalTextureId == targetTextureId &&
                    editedTextureId == targetTextureId &&
                    string.Equals(
                        LevelCatalog.NormalizeKey(behaviorSourceLevelKey),
                        LevelCatalog.NormalizeKey(relocation.DonorLevelKey),
                        StringComparison.OrdinalIgnoreCase);
                bool isFaceLocalRelocationAssignment =
                    textureChanged &&
                    originalTextureId != targetTextureId &&
                    editedTextureId == targetTextureId &&
                    !hasNativeVisual &&
                    (relocation.PreservesTargetNativeSurface
                        ? !hasNativeBehavior
                        : !hasNativeBehavior || string.Equals(
                            LevelCatalog.NormalizeKey(behaviorSourceLevelKey),
                            LevelCatalog.NormalizeKey(relocation.DonorLevelKey),
                            StringComparison.OrdinalIgnoreCase));
                if (isRelocationBehaviorAssignment || isFaceLocalRelocationAssignment)
                    continue;

                AddAtomicTerrainSwapBlock(
                    skippedEdits,
                    $"{runtimeKey}: face-local texture/tint/property state overlaps shared relocated texture {targetTextureId} from {relocation.DonorLevelName}; Undo one edit before building.");
            }
        }
    }

    public static TerrainPatchPlan BuildPlan(
        string sourceImagePath,
        string sourceCuePath,
        string outputImagePath,
        string outputCuePath,
        LevelDefinition level,
        string ramPath,
        string sourceSearchPath,
        string terrainEditsPath,
        string customTexturesPath = "",
        string nativeTextureRelocationsPath = "")
    {
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        bool hasRamCapture = File.Exists(ramPath);
        bool hasSourceSearch = File.Exists(sourceSearchPath);
        bool usesSourceDerivedSceneBytes = !hasRamCapture && IsSourceDerivedSourceSearch(sourceSearchPath);
        bool canPatchSceneSectors = hasSourceSearch && (hasRamCapture || usesSourceDerivedSceneBytes);
        int textureAssetWadIndex = TextureAssetWadIndexForLevel(level);
        using FileStream imageStream = File.OpenRead(sourceImagePath);
        AssetSubfileInfo modelSubfileInfo = GetAssetSubfileInfo(imageStream, layout, textureAssetWadIndex, ModelSubfileIndex);
        byte[] ram = canPatchSceneSectors
            ? hasRamCapture ? File.ReadAllBytes(ramPath) : ReadLogicalWad(imageStream, layout)
            : Array.Empty<byte>();
        Dictionary<string, SourceSectorLocation> sourceSectorHits = canPatchSceneSectors
            ? LoadSourceSectorLocations(sourceSearchPath)
            : new Dictionary<string, SourceSectorLocation>(StringComparer.OrdinalIgnoreCase);
        List<string> skippedEdits = new();
        CollisionPatchContext? collisionContext = canPatchSceneSectors && hasRamCapture
            ? TryBuildCollisionPatchContext(imageStream, layout, ram, skippedEdits)
            : null;
        Dictionary<long, TerrainPatch> patchesByWadOffset = new();
        Dictionary<(int SectorOffset, int ColorIndex), TerrainTextureVisualCorner> nativeVisualColorReservations = new();
        IReadOnlyList<NativeTerrainTextureRelocationEdit> nativeTextureRelocations =
            string.IsNullOrWhiteSpace(nativeTextureRelocationsPath)
                ? Array.Empty<NativeTerrainTextureRelocationEdit>()
                : NativeTerrainTextureRelocationEditStore.LoadManifest(nativeTextureRelocationsPath, level.Key);
        IReadOnlyDictionary<int, NativeTerrainTextureRelocationEdit> nativeTextureRelocationsByTarget =
            nativeTextureRelocations.ToDictionary(edit => edit.TargetTextureId);
        Dictionary<int, NativeTerrainSurfaceSourceData> donorSurfaceSourcesByWadEntry = [];
        List<NativeTerrainSurfaceBehaviorAssignment> nativeSurfaceBehaviorAssignments = [];
        List<TerrainSideWallCandidate> sideWallCandidates = new();
        List<TerrainSideWallPatchSummary> sideWallSummaries = new();
        List<long> editedSceneSectorWadOffsets = new();
        if (File.Exists(terrainEditsPath))
        {
            using FileStream editStream = File.OpenRead(terrainEditsPath);
            using JsonDocument editDocument = JsonDocument.Parse(editStream);

            if (!editDocument.RootElement.TryGetProperty("edits", out JsonElement editsElement) || editsElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("The terrain edit file does not contain an edits array.");

            AddNativeTextureRelocationFaceEditConflicts(
                editsElement,
                nativeTextureRelocationsByTarget,
                skippedEdits);

            if (collisionContext == null && canPatchSceneSectors && usesSourceDerivedSceneBytes)
            {
                collisionContext = TryBuildSourceDerivedCollisionPatchContext(
                    ram,
                    sourceSectorHits,
                    editsElement,
                    skippedEdits);
            }

            NativeTerrainSurfaceSourceData? nativeSurfaceSource = null;
            IReadOnlyDictionary<string, NativeCollisionSurfaceTriangle[]>? nativeSurfaceTrianglesByKey = null;
            if (editsElement.EnumerateArray().Any(edit => JsonValue.GetBoolean(edit, "nativeSurfaceBehaviorEdit")))
            {
                try
                {
                    nativeSurfaceSource = PortalSourceDataLocator.LocateTerrainSurfaces(imageStream, layout, level);
                    nativeSurfaceTrianglesByKey = nativeSurfaceSource.CollisionSurfaceTriangles
                        .GroupBy(NativeTerrainSurfaceCatalogBuilder.TriangleKey, StringComparer.Ordinal)
                        .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
                }
                catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException)
                {
                    AddAtomicTerrainSwapBlock(
                        skippedEdits,
                        $"native terrain behavior source collision/surface tables could not be decoded: {ex.Message}");
                }
            }

            foreach (JsonElement edit in editsElement.EnumerateArray())
            {
                string runtimeKey = JsonValue.GetString(edit, "runtimeKey");
                if (string.IsNullOrWhiteSpace(runtimeKey))
                    continue;
                string structureEditMode = JsonValue.GetString(edit, "structureEditMode");
                if (!canPatchSceneSectors)
                {
                    string reason = $"{runtimeKey}: terrain face edits need this level's RAM capture/source-search map or a source-derived terrain map; custom texture art can still be patched.";
                    if (IsAtomicTerrainFaceSwapEdit(edit))
                        AddAtomicTerrainSwapBlock(skippedEdits, reason);
                    else
                        skippedEdits.Add(reason);
                    continue;
                }
                if (!sourceSectorHits.TryGetValue(runtimeKey, out SourceSectorLocation? sourceSectorLocation))
                {
                    string reason = $"{runtimeKey}: no source-sector match found.";
                    if (IsAtomicTerrainFaceSwapEdit(edit))
                        AddAtomicTerrainSwapBlock(skippedEdits, reason);
                    else
                        skippedEdits.Add(reason);
                    continue;
                }
                long sourceSectorWadOffset = sourceSectorLocation.WadOffset;
                if (!editedSceneSectorWadOffsets.Contains(sourceSectorWadOffset))
                    editedSceneSectorWadOffsets.Add(sourceSectorWadOffset);

                int sectorOffset = JsonValue.GetInt32(edit, "sectorOffset", -1);
                if (sectorOffset < 0)
                {
                    string reason = $"{runtimeKey}: edit is missing sector offset; re-save this terrain edit in the native editor.";
                    if (IsAtomicTerrainFaceSwapEdit(edit))
                        AddAtomicTerrainSwapBlock(skippedEdits, reason);
                    else
                        skippedEdits.Add(reason);
                    continue;
                }

                SceneSectorHeader sector = ReadSceneSectorHeader(ram, sectorOffset);
                string detail = JsonValue.GetString(edit, "detail", "hp");
                if (IsStructuralTerrainEdit(structureEditMode))
                {
                    if (IsRemoveFaceEdit(structureEditMode))
                    {
                        AddFaceRemovalPatches(imageStream, layout, ram, collisionContext, sector, sourceSectorWadOffset, sectorOffset, detail, edit, patchesByWadOffset, skippedEdits);
                        continue;
                    }
                    else if (IsAddCloneFaceEdit(structureEditMode))
                    {
                        AddFaceClonePatches(imageStream, layout, ram, collisionContext, sector, sourceSectorHits, modelSubfileInfo, editedSceneSectorWadOffsets, sourceSectorWadOffset, sectorOffset, detail, edit, patchesByWadOffset, skippedEdits);
                        continue;
                    }
                }
                AddTexturePatch(
                    imageStream,
                    layout,
                    ram,
                    level,
                    sector,
                    sourceSectorWadOffset,
                    sectorOffset,
                    edit,
                    nativeVisualColorReservations,
                    patchesByWadOffset,
                    skippedEdits);
                AddNativeSurfaceBehaviorPatches(
                    imageStream,
                    layout,
                    level,
                    nativeSurfaceSource,
                    nativeSurfaceTrianglesByKey,
                    nativeTextureRelocationsByTarget,
                    donorSurfaceSourcesByWadEntry,
                    edit,
                    nativeSurfaceBehaviorAssignments,
                    skippedEdits);
                AddVertexPatches(imageStream, layout, ram, sector, sourceSectorWadOffset, sectorOffset, detail, edit, patchesByWadOffset, skippedEdits);
                AddCollisionPatches(imageStream, layout, ram, collisionContext, sector, detail, edit, patchesByWadOffset, skippedEdits);
                CollectTerrainSideWallCandidates(ram, collisionContext, sector, sourceSectorWadOffset, sectorOffset, detail, edit, sideWallCandidates, skippedEdits);
            }

            AddTerrainSideWallPatches(imageStream, layout, ram, collisionContext, sourceSectorHits, modelSubfileInfo, editedSceneSectorWadOffsets, sideWallCandidates, patchesByWadOffset, skippedEdits, sideWallSummaries);
            AddNativeSurfaceLayoutPatch(
                imageStream,
                layout,
                nativeSurfaceSource,
                nativeSurfaceBehaviorAssignments,
                patchesByWadOffset,
                skippedEdits);
        }

        IReadOnlyList<NativeTerrainTextureRelocationPatchSummary> nativeTextureRelocationSummaries =
            AddNativeTerrainTextureRelocationPatches(
                sourceImagePath,
                sourceCuePath,
                level,
                nativeTextureRelocations,
                imageStream,
                layout,
                patchesByWadOffset,
                skippedEdits);
        NativeTerrainTextureLowDetailCompanionSummary lowDetailTextureCompanion =
            AddNativeTerrainTextureLowDetailCompanionPatches(
                level,
                nativeTextureRelocations,
                nativeTextureRelocationSummaries,
                sourceSectorHits,
                imageStream,
                layout,
                patchesByWadOffset);

        List<CustomTerrainTexturePatchSummary> customTextureSummaries = new();
        if (!string.IsNullOrWhiteSpace(customTexturesPath) && File.Exists(customTexturesPath))
            customTextureSummaries.AddRange(AddCustomTexturePatches(imageStream, layout, level.Key, textureAssetWadIndex, customTexturesPath, patchesByWadOffset, skippedEdits));

        IReadOnlyList<TerrainPatch> patches = patchesByWadOffset.Values
            .OrderBy(patch => ParseRequiredLong(patch.WadRelativeOffset, "patch.wadRelativeOffset"))
            .ToArray();
        return new TerrainPatchPlan(
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceImagePath: sourceImagePath,
            OutputImagePath: outputImagePath,
            OutputCuePath: outputCuePath,
            LevelKey: level.Key,
            LevelName: level.DisplayName,
            RamPath: ramPath,
            SourceSearchPath: sourceSearchPath,
            TerrainEditsPath: terrainEditsPath,
            CustomTexturesPath: customTexturesPath,
            NativeTextureRelocationsPath: nativeTextureRelocationsPath,
            TextureAssetWadIndex: textureAssetWadIndex,
            PatchCount: patches.Count,
            TotalPatchedBytes: patches.Sum(patch => patch.ByteLength),
            CustomTextureImportCount: customTextureSummaries.Count,
            CustomTextureBytePatchCount: patches.Count(patch => patch.Kind.StartsWith("custom-texture-", StringComparison.OrdinalIgnoreCase)),
            CustomTextureImports: customTextureSummaries,
            NativeTextureRelocationCount: nativeTextureRelocations.Count,
            NativeTextureRelocationBytePatchCount: patches.Count(patch => patch.Kind.StartsWith("native-terrain-texture-", StringComparison.OrdinalIgnoreCase)),
            NativeTextureRelocations: nativeTextureRelocationSummaries,
            TerrainSideWalls: sideWallSummaries,
            Patches: patches,
            SkippedEdits: skippedEdits,
            Notes:
            [
                usesSourceDerivedSceneBytes
                    ? $"Patches {level.DisplayName} source-derived scene-sector terrain vertices and texture ids directly from WAD scene offsets because no RAM capture was found."
                    : $"Patches {level.DisplayName} runtime scene-sector terrain vertices and texture ids using the source-search map for this level.",
                $"Custom terrain texture patches target WAD asset {textureAssetWadIndex} subfile {TexturePagesSubfileIndex}, using the level-local texture descriptor table in subfile {ModelSubfileIndex}.",
                collisionContext == null
                    ? "Collision triangle source bytes were not located, so height edits are visual-scene patches only in this plan."
                    : collisionContext.SupportsCollisionIndexRebuild
                        ? $"Collision triangle Z patches are enabled from source WAD offset 0x{collisionContext.SourceTriangleWadOffset:X}."
                        : $"Source-derived exact collision triangle patches are enabled for existing terrain faces from matched WAD offset 0x{collisionContext.SourceTriangleWadOffset:X}.",
                collisionContext == null
                    ? "Terrain side walls need collision triangle source bytes and were not attempted."
                    : collisionContext.SupportsCollisionIndexRebuild
                        ? "Terrain side walls are attempted for exposed raised/lowered high-detail edges, using direct append room or a bounded same-model sector shift when packed terrain needs more space."
                        : collisionContext.SupportsDirectLookupFallback
                            ? "Source-derived terrain can patch existing top collision and can attempt copied terrain/solid side-wall collision through the decoded direct lookup fallback; full collision-index rebuild remains disabled."
                            : "Source-derived terrain can patch existing top collision, but copied terrain and solid side walls still need the collision lookup table decoded.",
                lowDetailTextureCompanion.Note
            ]);
    }

    private static bool IsStructuralTerrainEdit(string mode)
    {
        return IsRemoveFaceEdit(mode) || IsAddCloneFaceEdit(mode);
    }

    private static bool IsRemoveFaceEdit(string mode)
    {
        return string.Equals(mode, "remove-face", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAddCloneFaceEdit(string mode)
    {
        return string.Equals(mode, "add-clone-face", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddFaceClonePatches(
        FileStream imageStream,
        DiscLayout layout,
        byte[] ram,
        CollisionPatchContext? collisionContext,
        SceneSectorHeader sector,
        IReadOnlyDictionary<string, SourceSectorLocation> sourceSectorHits,
        AssetSubfileInfo modelSubfileInfo,
        IReadOnlyList<long> editedSceneSectorWadOffsets,
        long sourceSectorWadOffset,
        int sectorOffset,
        string detail,
        JsonElement edit,
        Dictionary<long, TerrainPatch> patchesByWadOffset,
        List<string> skippedEdits)
    {
        string runtimeKey = JsonValue.GetString(edit, "runtimeKey");
        if (!string.Equals(detail, "hp", StringComparison.OrdinalIgnoreCase))
        {
            skippedEdits.Add($"{runtimeKey}: add-copy terrain currently supports high-detail faces only.");
            return;
        }

        if (sector.NumHpFaces >= 255)
        {
            skippedEdits.Add($"{runtimeKey}: sector already has 255 high-detail faces; cannot append another face without full sector relocation.");
            return;
        }

        int faceOffset = JsonValue.GetInt32(edit, "faceOffset", -1);
        if (faceOffset < sectorOffset || faceOffset + 16 > sectorOffset + sector.SizeBytes)
        {
            skippedEdits.Add($"{runtimeKey}: add-copy edit is missing a valid source face offset; re-save this terrain edit.");
            return;
        }

        int appendSlack = FindSectorAppendSlack(sourceSectorHits, sourceSectorWadOffset, sector.SizeBytes);
        byte[] sourceFaceBytes = ram.AsSpan(faceOffset, 16).ToArray();
        int textureIdEdited = JsonValue.GetInt32(edit, "textureIdEdited", -1);
        if (textureIdEdited >= 0)
            ApplyTextureIdToHpFaceBytes(sourceFaceBytes, textureIdEdited);

        if (TryAddFaceCloneRepackPatches(
            imageStream,
            layout,
            ram,
            sector,
            sourceSectorHits,
            modelSubfileInfo,
            editedSceneSectorWadOffsets,
            sourceSectorWadOffset,
            sectorOffset,
            sourceFaceBytes,
            appendSlack,
            edit,
            patchesByWadOffset,
            skippedEdits))
        {
            bool addedCollision = AddCollisionClonePatches(
                imageStream,
                layout,
                ram,
                collisionContext,
                sector,
                detail,
                edit,
                patchesByWadOffset,
                skippedEdits);
            if (!addedCollision)
                skippedEdits.Add($"{runtimeKey}: add-copy created independent visible terrain vertices, but playable collision was not added for this copy.");
            return;
        }

        skippedEdits.Add($"{runtimeKey}: add-copy could not create independent visible terrain vertices, so no terrain face was appended.");
    }

    private static bool TryAddFaceCloneRepackPatches(
        FileStream imageStream,
        DiscLayout layout,
        byte[] ram,
        SceneSectorHeader sector,
        IReadOnlyDictionary<string, SourceSectorLocation> sourceSectorHits,
        AssetSubfileInfo modelSubfileInfo,
        IReadOnlyList<long> editedSceneSectorWadOffsets,
        long sourceSectorWadOffset,
        int sectorOffset,
        byte[] sourceFaceBytes,
        int appendSlack,
        JsonElement edit,
        Dictionary<long, TerrainPatch> patchesByWadOffset,
        List<string> skippedEdits)
    {
        string runtimeKey = JsonValue.GetString(edit, "runtimeKey");
        IReadOnlyList<float> editedZ = ReadFloatArray(edit, "editedZ");
        IReadOnlyList<float> originalZ = ReadFloatArray(edit, "originalZ");
        IReadOnlyList<Vector2f> editedPoints = ReadVector2Array(edit, "editedPoints");
        IReadOnlyList<Vector2f> originalPoints = ReadVector2Array(edit, "originalPoints");
        int vertexValueCount = MaxValueCount(editedZ.Count, originalZ.Count, editedPoints.Count, originalPoints.Count);
        IReadOnlyList<int> faceVertexIndexes = ResolveEditableVertexIndexes(
            sector,
            "hp",
            ReadIntArray(edit, "vertexIndexes"),
            vertexValueCount,
            originalPoints,
            originalZ);
        if (faceVertexIndexes.Count == 0)
        {
            skippedEdits.Add($"{runtimeKey}: add-copy cannot create independent vertices because the edit has no vertex index list.");
            return false;
        }

        List<int> cloneVertexIndexes = new();
        foreach (int vertexIndex in faceVertexIndexes)
        {
            if (!cloneVertexIndexes.Contains(vertexIndex))
                cloneVertexIndexes.Add(vertexIndex);
        }

        if (cloneVertexIndexes.Count == 0 || cloneVertexIndexes.Count > 4)
        {
            skippedEdits.Add($"{runtimeKey}: add-copy cannot create independent vertices for {cloneVertexIndexes.Count} source vertices.");
            return false;
        }

        int addedBytes = (cloneVertexIndexes.Count * 4) + sourceFaceBytes.Length;
        if (sector.NumHpVertices + cloneVertexIndexes.Count > 255)
        {
            skippedEdits.Add($"{runtimeKey}: add-copy cannot add {cloneVertexIndexes.Count} high-detail vertices because the sector would exceed 255 vertices.");
            return false;
        }

        int effectiveAppendSlack = appendSlack;
        TerrainSectorSuffixShiftPlan? suffixShiftPlan = null;
        if (appendSlack < addedBytes)
        {
            if (TryBuildTerrainSectorSuffixShiftPlan(
                imageStream,
                layout,
                sourceSectorHits,
                modelSubfileInfo,
                editedSceneSectorWadOffsets,
                sourceSectorWadOffset,
                sector.SizeBytes,
                addedBytes,
                out TerrainSectorSuffixShiftPlan? shiftPlan,
                out string shiftSkipReason))
            {
                suffixShiftPlan = shiftPlan;
                effectiveAppendSlack = addedBytes;
            }
            else
            {
                string reason = string.IsNullOrWhiteSpace(shiftSkipReason)
                    ? $"this sector has {Math.Max(appendSlack, 0)} byte(s) of append room."
                    : shiftSkipReason;
                skippedEdits.Add($"{runtimeKey}: add-copy needs {addedBytes} bytes of source-sector room for independent vertices and a copied face, but {reason}");
                return false;
            }
        }

        Dictionary<int, byte> cloneIndexMap = new();
        byte[] cloneVertexBytes = new byte[cloneVertexIndexes.Count * 4];
        for (int i = 0; i < cloneVertexIndexes.Count; i++)
        {
            int sourceVertexIndex = cloneVertexIndexes[i];
            int sourceVertexOffset = GetSceneVertexOffset(sector, "hp", sourceVertexIndex);
            if (sourceVertexOffset < 0)
            {
                skippedEdits.Add($"{runtimeKey}: add-copy source vertex {sourceVertexIndex} is not valid for this high-detail sector.");
                return false;
            }

            int sourcePointIndex = IndexOfVertex(faceVertexIndexes, sourceVertexIndex);
            uint vertexWord = ReadUInt32(ram, sourceVertexOffset);
            if (sourcePointIndex >= 0)
            {
                Vector3f original = DecodeSceneVertex(vertexWord, sector);
                float targetZ = sourcePointIndex < editedZ.Count ? editedZ[sourcePointIndex] : original.Z;
                try
                {
                    if (sourcePointIndex < editedPoints.Count)
                        vertexWord = SetSceneVertexWord(vertexWord, sector, editedPoints[sourcePointIndex], targetZ);
                    else if (sourcePointIndex < editedZ.Count)
                        vertexWord = SetSceneVertexZWord(vertexWord, sector, targetZ);
                }
                catch (InvalidOperationException ex)
                {
                    skippedEdits.Add($"{runtimeKey}: add-copy source vertex {sourceVertexIndex} cannot be safely encoded. {ex.Message}");
                    return false;
                }
            }
            BitConverter.GetBytes(vertexWord).CopyTo(cloneVertexBytes, i * 4);
            cloneIndexMap[sourceVertexIndex] = checked((byte)(sector.NumHpVertices + i));
        }

        byte[] cloneFaceBytes = sourceFaceBytes.ToArray();
        int vertexReferenceCount = Math.Min(faceVertexIndexes.Count, 4);
        for (int i = 0; i < vertexReferenceCount; i++)
        {
            int sourceVertexIndex = cloneFaceBytes[i];
            if (!cloneIndexMap.TryGetValue(sourceVertexIndex, out byte cloneVertexIndex))
            {
                skippedEdits.Add($"{runtimeKey}: add-copy visible face byte {i} references vertex {sourceVertexIndex}, which was not in the editable vertex list.");
                return false;
            }

            cloneFaceBytes[i] = cloneVertexIndex;
        }

        int hpVertexEndOffset = GetHpVertexDataEndOffset(sector);
        int oldTailLength = (sector.Offset + sector.SizeBytes) - hpVertexEndOffset;
        if (oldTailLength < 0)
        {
            skippedEdits.Add($"{runtimeKey}: add-copy could not locate the high-detail sector tail.");
            return false;
        }

        byte[] oldTail = ram.AsSpan(hpVertexEndOffset, oldTailLength).ToArray();
        byte[] shiftedSuffixBytes = suffixShiftPlan?.SuffixBytes ?? Array.Empty<byte>();
        byte[] repackedTail = new byte[cloneVertexBytes.Length + oldTail.Length + cloneFaceBytes.Length + shiftedSuffixBytes.Length];
        cloneVertexBytes.CopyTo(repackedTail, 0);
        oldTail.CopyTo(repackedTail, cloneVertexBytes.Length);
        cloneFaceBytes.CopyTo(repackedTail, cloneVertexBytes.Length + oldTail.Length);
        shiftedSuffixBytes.CopyTo(repackedTail, cloneVertexBytes.Length + oldTail.Length + cloneFaceBytes.Length);

        long vertexCountWadOffset = sourceSectorWadOffset + 20;
        AddPatch(
            imageStream,
            layout,
            patchesByWadOffset,
            vertexCountWadOffset,
            [(byte)sector.NumHpVertices],
            [(byte)(sector.NumHpVertices + cloneVertexIndexes.Count)],
            "terrain-vertex-count-hp",
            runtimeKey,
            $"Increase high-detail vertex count from {sector.NumHpVertices} to {sector.NumHpVertices + cloneVertexIndexes.Count} for an added terrain copy.");

        long faceCountWadOffset = sourceSectorWadOffset + 22;
        AddPatch(
            imageStream,
            layout,
            patchesByWadOffset,
            faceCountWadOffset,
            [(byte)sector.NumHpFaces],
            [(byte)(sector.NumHpFaces + 1)],
            "terrain-face-count-hp",
            runtimeKey,
            $"Increase high-detail face count from {sector.NumHpFaces} to {sector.NumHpFaces + 1}.");

        long repackWadOffset = sourceSectorWadOffset + (hpVertexEndOffset - sectorOffset);
        byte[] expectedBefore = ReadWadBytes(imageStream, layout, repackWadOffset, repackedTail.Length);
        AddPatch(
            imageStream,
            layout,
            patchesByWadOffset,
            repackWadOffset,
            expectedBefore,
            repackedTail,
            "terrain-sector-repack-hp-add-copy",
            runtimeKey,
            suffixShiftPlan == null
                ? $"Repack the high-detail sector tail into {effectiveAppendSlack} byte(s) of slack, adding {cloneVertexIndexes.Count} independent copied terrain vertices and one copied face."
                : $"Repack the high-detail sector tail and shift {suffixShiftPlan.ShiftedSectorCount} later scene sector(s) by {suffixShiftPlan.ShiftBytes} byte(s), adding {cloneVertexIndexes.Count} independent copied terrain vertices and one copied face.");
        return true;
    }

    private static bool AddCollisionClonePatches(
        FileStream imageStream,
        DiscLayout layout,
        byte[] ram,
        CollisionPatchContext? collisionContext,
        SceneSectorHeader sector,
        string detail,
        JsonElement edit,
        Dictionary<long, TerrainPatch> patchesByWadOffset,
        List<string> skippedEdits)
    {
        if (collisionContext == null)
            return false;

        string runtimeKey = JsonValue.GetString(edit, "runtimeKey");
        if (!collisionContext.SupportsCollisionIndexRebuild && !collisionContext.SupportsDirectLookupFallback)
        {
            skippedEdits.Add($"{runtimeKey}: add-copy collision needs the decoded collision lookup table; source-derived exact triangle matches currently support existing terrain faces only.");
            return false;
        }

        IReadOnlyList<float> editedZ = ReadFloatArray(edit, "editedZ");
        IReadOnlyList<float> originalZ = ReadFloatArray(edit, "originalZ");
        IReadOnlyList<Vector2f> editedPoints = ReadVector2Array(edit, "editedPoints");
        IReadOnlyList<Vector2f> originalPoints = ReadVector2Array(edit, "originalPoints");
        IReadOnlyList<int> vertexIndexes = ResolveEditableVertexIndexes(
            sector,
            detail,
            ReadIntArray(edit, "vertexIndexes"),
            MaxValueCount(editedZ.Count, originalZ.Count, editedPoints.Count, originalPoints.Count),
            originalPoints,
            originalZ);
        if (!HasEditedVertexValues(originalZ, editedZ, originalPoints, editedPoints) || vertexIndexes.Count < 3)
            return false;

        int vertexCount = Math.Min(vertexIndexes.Count, Math.Max(editedZ.Count, editedPoints.Count));
        if (vertexCount < 3)
            return false;

        List<CollisionPatchVertex> vertices = new(vertexCount);
        for (int i = 0; i < vertexCount; i++)
        {
            int vertexOffset = GetSceneVertexOffset(sector, detail, vertexIndexes[i]);
            if (vertexOffset < 0)
            {
                skippedEdits.Add($"{runtimeKey}: add-copy collision could not decode source vertex {vertexIndexes[i]}.");
                return false;
            }

            Vector3f original = DecodeSceneVertex(ReadUInt32(ram, vertexOffset), sector);
            Vector2f targetPoint = i < editedPoints.Count ? editedPoints[i] : new Vector2f(original.X, original.Y);
            int targetZ = i < editedZ.Count ? (int)Math.Round(editedZ[i]) : (int)Math.Round(original.Z);
            vertices.Add(new CollisionPatchVertex(original, new Vector3f(targetPoint.X, targetPoint.Y, targetZ)));
        }

        string indexRebuildSkip = "";
        string indexPatchSkip = "";
        if (TryBuildCollisionClonePatchesForIndexRebuild(
            collisionContext,
            runtimeKey,
            vertices,
            out IReadOnlyList<PendingTerrainPatch> pendingPatches,
            out indexRebuildSkip) &&
            TryAddCollisionIndexRebuildPatches(
                imageStream,
                layout,
                ram,
                collisionContext,
                patchesByWadOffset,
                pendingPatches,
                out indexPatchSkip))
        {
            foreach (PendingTerrainPatch pendingPatch in pendingPatches)
            {
                AddPatch(
                    imageStream,
                    layout,
                    patchesByWadOffset,
                    pendingPatch.WadOffset,
                    pendingPatch.Before,
                    pendingPatch.After,
                    pendingPatch.Kind,
                    pendingPatch.RuntimeKey,
                    pendingPatch.Description);
            }

            return true;
        }

        HashSet<int> usedReplacementTriangles = new();
        HashSet<int> usedLookupEntryOffsets = new();
        int matchedTriangles = 0;
        int replacementCandidates = 0;
        int encodedTriangles = 0;
        string directLookupSkip = "";
        for (int i = 1; i < vertices.Count - 1; i++)
        {
            CollisionPatchVertex a = vertices[0];
            CollisionPatchVertex b = vertices[i];
            CollisionPatchVertex c = vertices[i + 1];
            SpyroCollisionPoint[] sourcePoints =
            [
                new SpyroCollisionPoint((int)Math.Round(a.Original.X), (int)Math.Round(a.Original.Y), (int)Math.Round(a.Original.Z)),
                new SpyroCollisionPoint((int)Math.Round(b.Original.X), (int)Math.Round(b.Original.Y), (int)Math.Round(b.Original.Z)),
                new SpyroCollisionPoint((int)Math.Round(c.Original.X), (int)Math.Round(c.Original.Y), (int)Math.Round(c.Original.Z))
            ];
            SpyroCollisionPoint[] targetPoints =
            [
                ToCollisionPoint(a.Target),
                ToCollisionPoint(b.Target),
                ToCollisionPoint(c.Target)
            ];
            string sourceKey = CollisionTriangleKey(sourcePoints);
            if (!collisionContext.TrianglesByKey.TryGetValue(sourceKey, out List<SpyroCollisionTriangle>? sourceTriangles))
                continue;

            foreach (SpyroCollisionTriangle sourceTriangle in sourceTriangles)
            {
                matchedTriangles++;
                if (!TryFindCollisionReplacementWithDirectLookup(
                    collisionContext,
                    sourceTriangle.Index,
                    usedReplacementTriangles,
                    usedLookupEntryOffsets,
                    "collision-lookup-add-copy",
                    "a copied terrain face",
                    allowSharedSourceLookupBorrow: false,
                    out SpyroCollisionTriangle? replacementTriangle,
                    out PendingTerrainPatch? lookupPatch,
                    out string candidateLookupSkip))
                {
                    if (!string.IsNullOrWhiteSpace(candidateLookupSkip))
                        directLookupSkip = candidateLookupSkip;
                    continue;
                }
                if (replacementTriangle == null)
                    continue;

                replacementCandidates++;
                if (!TryBuildCollisionTriangleWords(targetPoints, sourceTriangle.ZWord & 0x0000C000u, out uint newXWord, out uint newYWord, out uint newZWord, out string description))
                    continue;

                encodedTriangles++;
                SpyroCollisionTriangle replacement = replacementTriangle;
                usedReplacementTriangles.Add(replacement.Index);
                if (lookupPatch != null)
                {
                    AddPatch(
                        imageStream,
                        layout,
                        patchesByWadOffset,
                        lookupPatch.WadOffset,
                        lookupPatch.Before,
                        lookupPatch.After,
                        lookupPatch.Kind,
                        lookupPatch.RuntimeKey,
                        lookupPatch.Description);
                    usedLookupEntryOffsets.Add((int)lookupPatch.WadOffset);
                }

                long wadOffset = GetCollisionTriangleWadOffset(collisionContext, replacement);
                byte[] before = new byte[12];
                byte[] after = new byte[12];
                BitConverter.GetBytes(replacement.XWord).CopyTo(before, 0);
                BitConverter.GetBytes(replacement.YWord).CopyTo(before, 4);
                BitConverter.GetBytes(replacement.ZWord).CopyTo(before, 8);
                BitConverter.GetBytes(newXWord).CopyTo(after, 0);
                BitConverter.GetBytes(newYWord).CopyTo(after, 4);
                BitConverter.GetBytes(newZWord).CopyTo(after, 8);
                AddPatch(
                    imageStream,
                    layout,
                    patchesByWadOffset,
                    wadOffset,
                    before,
                    after,
                    "collision-triangle-add-copy",
                    runtimeKey,
                    $"Add playable collision for copied terrain by repurposing degenerate triangle {replacement.Index} from the same lookup group as source triangle {sourceTriangle.Index}: {description}.");
            }
        }

        if (matchedTriangles == 0)
            skippedEdits.Add($"{runtimeKey}: add-copy collision could not find matching source collision triangles.");
        else if (replacementCandidates == 0)
            skippedEdits.Add($"{runtimeKey}: add-copy collision found {matchedTriangles} source triangle(s), but no same-cell degenerate collision placeholder was available{(string.IsNullOrWhiteSpace(directLookupSkip) ? "" : $": {directLookupSkip}")}{(string.IsNullOrWhiteSpace(indexRebuildSkip) ? "" : $" and index rebuild fallback failed: {indexRebuildSkip}")}{(string.IsNullOrWhiteSpace(indexPatchSkip) ? "" : $" ({indexPatchSkip})")}.");
        else if (encodedTriangles == 0)
            skippedEdits.Add($"{runtimeKey}: add-copy collision found {replacementCandidates} placeholder triangle(s), but the copied heights could not be packed into the current collision format{(string.IsNullOrWhiteSpace(indexRebuildSkip) ? "" : $" and index rebuild fallback failed: {indexRebuildSkip}")}{(string.IsNullOrWhiteSpace(indexPatchSkip) ? "" : $" ({indexPatchSkip})")}.");

        return encodedTriangles > 0;
    }

    private static bool TryBuildCollisionClonePatchesForIndexRebuild(
        CollisionPatchContext collisionContext,
        string runtimeKey,
        IReadOnlyList<CollisionPatchVertex> vertices,
        out IReadOnlyList<PendingTerrainPatch> patches,
        out string skipReason)
    {
        patches = Array.Empty<PendingTerrainPatch>();
        skipReason = "";
        if (vertices.Count < 3)
        {
            skipReason = "add-copy collision needs at least three copied vertices.";
            return false;
        }

        List<PendingTerrainPatch> result = new();
        HashSet<int> reservedReplacementIndexes = new();
        int matchedTriangles = 0;
        for (int i = 1; i < vertices.Count - 1; i++)
        {
            CollisionPatchVertex a = vertices[0];
            CollisionPatchVertex b = vertices[i];
            CollisionPatchVertex c = vertices[i + 1];
            SpyroCollisionPoint[] sourcePoints =
            [
                new SpyroCollisionPoint((int)Math.Round(a.Original.X), (int)Math.Round(a.Original.Y), (int)Math.Round(a.Original.Z)),
                new SpyroCollisionPoint((int)Math.Round(b.Original.X), (int)Math.Round(b.Original.Y), (int)Math.Round(b.Original.Z)),
                new SpyroCollisionPoint((int)Math.Round(c.Original.X), (int)Math.Round(c.Original.Y), (int)Math.Round(c.Original.Z))
            ];
            SpyroCollisionPoint[] targetPoints =
            [
                ToCollisionPoint(a.Target),
                ToCollisionPoint(b.Target),
                ToCollisionPoint(c.Target)
            ];

            string sourceKey = CollisionTriangleKey(sourcePoints);
            if (!collisionContext.TrianglesByKey.TryGetValue(sourceKey, out List<SpyroCollisionTriangle>? sourceTriangles))
                continue;

            matchedTriangles += sourceTriangles.Count;
            PendingTerrainPatch? trianglePatch = null;
            foreach (SpyroCollisionTriangle sourceTriangle in sourceTriangles)
            {
                if (!TryBuildCollisionTriangleWords(targetPoints, sourceTriangle.ZWord & 0x0000C000u, out uint newXWord, out uint newYWord, out uint newZWord, out string description))
                {
                    skipReason = "the copied terrain triangle is too tall, too wide, or points downward for the current packed collision triangle format.";
                    continue;
                }

                foreach (int replacementIndex in collisionContext.DegenerateTriangleIndexes)
                {
                    if (reservedReplacementIndexes.Contains(replacementIndex) ||
                        !collisionContext.SourceDegenerateTriangleIndexes.Contains(replacementIndex) ||
                        replacementIndex < 0 ||
                        replacementIndex >= collisionContext.Table.Triangles.Count ||
                        replacementIndex > 0x7FFF ||
                        !collisionContext.SourceTriangleBytesByIndex.TryGetValue(replacementIndex, out byte[]? sourceBefore))
                    {
                        continue;
                    }

                    SpyroCollisionTriangle replacementTriangle = collisionContext.Table.Triangles[replacementIndex];
                    long wadOffset = GetCollisionTriangleWadOffset(collisionContext, replacementTriangle);
                    byte[] after = new byte[12];
                    BitConverter.GetBytes(newXWord).CopyTo(after, 0);
                    BitConverter.GetBytes(newYWord).CopyTo(after, 4);
                    BitConverter.GetBytes(newZWord).CopyTo(after, 8);
                    trianglePatch = new PendingTerrainPatch(
                        WadOffset: wadOffset,
                        Before: sourceBefore.ToArray(),
                        After: after,
                        Kind: "collision-triangle-add-copy",
                        RuntimeKey: runtimeKey,
                        Description: $"Add playable collision for copied terrain by repurposing degenerate triangle {replacementTriangle.Index} from source triangle {sourceTriangle.Index}: {description}.");
                    reservedReplacementIndexes.Add(replacementIndex);
                    break;
                }

                if (trianglePatch != null)
                    break;

                skipReason = "no reusable degenerate collision triangle was available for the rebuilt collision index.";
            }

            if (trianglePatch != null)
                result.Add(trianglePatch);
        }

        if (result.Count > 0)
        {
            patches = result;
            skipReason = "";
            return true;
        }

        if (matchedTriangles == 0)
            skipReason = "add-copy collision could not find matching source collision triangles.";
        else if (string.IsNullOrWhiteSpace(skipReason))
            skipReason = "add-copy collision found source triangles, but no rebuilt-index replacement could be encoded.";
        return false;
    }

    private static void CollectTerrainSideWallCandidates(
        byte[] ram,
        CollisionPatchContext? collisionContext,
        SceneSectorHeader sector,
        long sourceSectorWadOffset,
        int sectorOffset,
        string detail,
        JsonElement edit,
        List<TerrainSideWallCandidate> candidates,
        List<string> skippedEdits)
    {
        if (collisionContext == null)
            return;
        if (!string.Equals(detail, "hp", StringComparison.OrdinalIgnoreCase))
            return;

        string runtimeKey = JsonValue.GetString(edit, "runtimeKey");
        if (!collisionContext.SupportsCollisionIndexRebuild && !collisionContext.SupportsDirectLookupFallback)
        {
            skippedEdits.Add($"{runtimeKey}: terrain side-wall collision needs the decoded collision lookup table; source-derived exact triangle matches currently support top-face collision only.");
            return;
        }

        IReadOnlyList<float> editedZ = ReadFloatArray(edit, "editedZ");
        IReadOnlyList<float> originalZ = ReadFloatArray(edit, "originalZ");
        IReadOnlyList<Vector2f> editedPoints = ReadVector2Array(edit, "editedPoints");
        IReadOnlyList<Vector2f> originalPoints = ReadVector2Array(edit, "originalPoints");
        if (!HasEditedVertexValues(originalZ, editedZ, originalPoints, editedPoints))
            return;

        int faceOffset = JsonValue.GetInt32(edit, "faceOffset", -1);
        if (faceOffset < sectorOffset || faceOffset + 16 > sectorOffset + sector.SizeBytes)
        {
            skippedEdits.Add($"{runtimeKey}: terrain side walls need a valid high-detail source face offset; re-save this terrain edit.");
            return;
        }

        IReadOnlyList<int> vertexIndexes = ResolveEditableVertexIndexes(
            sector,
            detail,
            ReadIntArray(edit, "vertexIndexes"),
            MaxValueCount(editedZ.Count, originalZ.Count, editedPoints.Count, originalPoints.Count),
            originalPoints,
            originalZ);
        int vertexCount = Math.Min(vertexIndexes.Count, MaxValueCount(editedZ.Count, originalZ.Count, editedPoints.Count, originalPoints.Count));
        if (vertexCount < 3)
            return;

        HashSet<int> editedFaceVertexIndexes = vertexIndexes
            .Take(vertexCount)
            .Where(index => index >= 0)
            .ToHashSet();
        List<CollisionPatchVertex> vertices = new(vertexCount);
        for (int i = 0; i < vertexCount; i++)
        {
            int vertexOffset = GetSceneVertexOffset(sector, detail, vertexIndexes[i]);
            if (vertexOffset < 0)
                return;

            Vector3f original = DecodeSceneVertex(ReadUInt32(ram, vertexOffset), sector);
            Vector2f targetPoint = i < editedPoints.Count ? editedPoints[i] : new Vector2f(original.X, original.Y);
            float targetZ = i < editedZ.Count ? editedZ[i] : original.Z;
            Vector3f target = new(targetPoint.X, targetPoint.Y, targetZ);
            vertices.Add(new CollisionPatchVertex(original, target));
        }

        IReadOnlyList<SpyroCollisionTriangle> sourceCollisionTriangles = FindMatchingCollisionTriangles(collisionContext, vertices);
        if (sourceCollisionTriangles.Count == 0)
        {
            skippedEdits.Add($"{runtimeKey}: terrain side walls need at least one matched top collision triangle to borrow a collision lookup group.");
            return;
        }

        byte[] sourceFaceBytes = ram.AsSpan(faceOffset, 16).ToArray();
        int textureIdEdited = JsonValue.GetInt32(edit, "textureIdEdited", -1);
        if (textureIdEdited >= 0)
            ApplyTextureIdToHpFaceBytes(sourceFaceBytes, textureIdEdited);
        for (int i = 0; i < vertices.Count; i++)
        {
            int next = (i + 1) % vertices.Count;
            Vector3f originalA = vertices[i].Original;
            Vector3f originalB = vertices[next].Original;
            Vector3f editedA = vertices[i].Target;
            Vector3f editedB = vertices[next].Target;
            if (!PointChanged(originalA, editedA) && !PointChanged(originalB, editedB))
                continue;
            int originalAIndex = FindReusableSideWallVertexIndex(sector, detail, originalA, editedFaceVertexIndexes);
            int originalBIndex = FindReusableSideWallVertexIndex(sector, detail, originalB, editedFaceVertexIndexes);

            candidates.Add(new TerrainSideWallCandidate(
                RuntimeKey: runtimeKey,
                SectorOffset: sectorOffset,
                SourceSectorWadOffset: sourceSectorWadOffset,
                Sector: sector,
                SourceFaceBytes: sourceFaceBytes,
                OriginalA: originalA,
                OriginalB: originalB,
                EditedA: editedA,
                EditedB: editedB,
                EditedAIndex: vertexIndexes[i],
                EditedBIndex: vertexIndexes[next],
                OriginalAIndex: originalAIndex,
                OriginalBIndex: originalBIndex,
                OriginalEdgeKey: TerrainEdgeKey(originalA, originalB),
                EditedEdgeKey: TerrainEdgeKey(editedA, editedB),
                SourceCollisionTriangles: sourceCollisionTriangles));
        }
    }

    private static void AddTerrainSideWallPatches(
        FileStream imageStream,
        DiscLayout layout,
        byte[] ram,
        CollisionPatchContext? collisionContext,
        IReadOnlyDictionary<string, SourceSectorLocation> sourceSectorHits,
        AssetSubfileInfo modelSubfileInfo,
        IReadOnlyList<long> editedSceneSectorWadOffsets,
        IReadOnlyList<TerrainSideWallCandidate> candidates,
        Dictionary<long, TerrainPatch> patchesByWadOffset,
        List<string> skippedEdits,
        List<TerrainSideWallPatchSummary> sideWallSummaries)
    {
        if (collisionContext == null || candidates.Count == 0)
            return;

        Dictionary<string, List<TerrainSideWallCandidate>> candidatesByOriginalEdge = candidates
            .GroupBy(candidate => candidate.OriginalEdgeKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        TerrainSideWallCandidate[] exposedCandidates = candidates
            .Where(candidate => !HasMatchingEditedTerrainEdge(candidate, candidatesByOriginalEdge))
            .ToArray();
        if (exposedCandidates.Length == 0)
            return;

        Dictionary<string, TerrainSideWallPatchSummaryBuilder> sideWallSummaryBuilders = new(StringComparer.OrdinalIgnoreCase);
        TerrainSideWallPatchSummaryBuilder SummaryFor(TerrainSideWallCandidate candidate)
        {
            string key = $"{candidate.RuntimeKey}|{candidate.SectorOffset:X}";
            if (!sideWallSummaryBuilders.TryGetValue(key, out TerrainSideWallPatchSummaryBuilder? summary))
            {
                summary = new TerrainSideWallPatchSummaryBuilder(candidate.RuntimeKey, candidate.SectorOffset);
                sideWallSummaryBuilders[key] = summary;
            }

            return summary;
        }

        foreach (TerrainSideWallCandidate candidate in exposedCandidates)
            SummaryFor(candidate).ExposedEdgeCount++;

        void SkipSideWallEdge(TerrainSideWallCandidate candidate, string reason)
        {
            TerrainSideWallPatchSummaryBuilder summary = SummaryFor(candidate);
            summary.SkippedEdgeCount++;
            summary.AddSkipReason(reason);
        }

        void SkipSideWallSector(IEnumerable<TerrainSideWallCandidate> sectorCandidates, string reason)
        {
            foreach (TerrainSideWallCandidate candidate in sectorCandidates)
                SkipSideWallEdge(candidate, reason);
        }

        HashSet<int> usedReplacementTriangles = new();
        HashSet<int> usedLookupEntryOffsets = new();
        foreach (IGrouping<int, TerrainSideWallCandidate> sectorGroup in exposedCandidates.GroupBy(candidate => candidate.SectorOffset))
        {
            TerrainSideWallCandidate[] sectorCandidates = sectorGroup.ToArray();
            TerrainSideWallCandidate first = sectorGroup.First();
            SceneSectorHeader sector = first.Sector;
            long sourceSectorWadOffset = first.SourceSectorWadOffset;
            int hpVertexEndOffset = GetHpVertexDataEndOffset(sector);
            long repackWadOffset = sourceSectorWadOffset + (hpVertexEndOffset - sector.Offset);
            int appendSlack = FindSectorAppendSlack(sourceSectorHits, sourceSectorWadOffset, sector.SizeBytes);
            int effectiveAppendSlack = appendSlack;
            TerrainSectorSuffixShiftPlan? suffixShiftPlan = null;
            foreach (IGrouping<string, TerrainSideWallCandidate> runtimeGroup in sectorCandidates.GroupBy(candidate => candidate.RuntimeKey, StringComparer.OrdinalIgnoreCase))
            {
                int requiredAppendBytes = EstimateSideWallAppendBytes(runtimeGroup);
                foreach (TerrainSideWallCandidate candidate in runtimeGroup)
                    SummaryFor(candidate).TrackAppendCapacity(requiredAppendBytes, appendSlack);
            }

            if (patchesByWadOffset.ContainsKey(repackWadOffset))
            {
                string reason = "another terrain structure edit already repacks this high-detail sector.";
                skippedEdits.Add($"sector 0x{sector.Offset:X}: terrain side walls skipped because {reason}");
                SkipSideWallSector(sectorCandidates, reason);
                continue;
            }

            int sectorRequiredAppendBytes = EstimateSideWallAppendBytes(sectorCandidates);
            if (sectorRequiredAppendBytes > appendSlack)
            {
                if (TryBuildTerrainSectorSuffixShiftPlan(
                    imageStream,
                    layout,
                    sourceSectorHits,
                    modelSubfileInfo,
                    editedSceneSectorWadOffsets,
                    sourceSectorWadOffset,
                    sector.SizeBytes,
                    sectorRequiredAppendBytes,
                    out TerrainSectorSuffixShiftPlan? shiftPlan,
                    out string shiftSkipReason))
                {
                    suffixShiftPlan = shiftPlan;
                    effectiveAppendSlack = sectorRequiredAppendBytes;
                }
                else
                {
                    string reason = string.IsNullOrWhiteSpace(shiftSkipReason)
                        ? $"sector needs {sectorRequiredAppendBytes} append byte(s) for new wall vertices/faces, but only {Math.Max(appendSlack, 0)} byte(s) are available."
                        : shiftSkipReason;
                    skippedEdits.Add($"sector 0x{sector.Offset:X}: terrain side walls need {sectorRequiredAppendBytes} append byte(s) for new wall vertices/faces, but only {Math.Max(appendSlack, 0)} byte(s) are available.");
                    skippedEdits.Add($"sector 0x{sector.Offset:X}: terrain side walls skipped because {reason}");
                    SkipSideWallSector(sectorCandidates, reason);
                    continue;
                }
            }

            if (effectiveAppendSlack < 16)
            {
                string reason = $"sector needs {sectorRequiredAppendBytes} append byte(s) for new wall vertices/faces, but only {Math.Max(effectiveAppendSlack, 0)} byte(s) are available.";
                skippedEdits.Add($"sector 0x{sector.Offset:X}: terrain side walls need {sectorRequiredAppendBytes} append byte(s) for new wall vertices/faces, but only {Math.Max(effectiveAppendSlack, 0)} byte(s) are available.");
                SkipSideWallSector(sectorCandidates, reason);
                continue;
            }

            int oldTailLength = (sector.Offset + sector.SizeBytes) - hpVertexEndOffset;
            if (oldTailLength < 0)
            {
                string reason = "could not locate the high-detail sector tail.";
                skippedEdits.Add($"sector 0x{sector.Offset:X}: terrain side walls {reason}");
                SkipSideWallSector(sectorCandidates, reason);
                continue;
            }

            Dictionary<string, int> sideVertexIndexes = new(StringComparer.Ordinal);
            List<uint> sideVertexWords = new();
            List<byte[]> sideFaceBytes = new();
            List<PendingTerrainPatch> collisionPatches = new();
            List<(TerrainSideWallCandidate Candidate, int CollisionTriangles)> emittedCandidates = new();
            HashSet<string> skippedRuntimeKeys = new(StringComparer.OrdinalIgnoreCase);
            HashSet<int> baseUsedReplacementTriangles = new(usedReplacementTriangles);
            HashSet<int> sectorUsedReplacementTriangles = new(baseUsedReplacementTriangles);
            HashSet<int> sectorUsedLookupEntryOffsets = new(usedLookupEntryOffsets);
            bool usesDirectLookupCollision = false;
            foreach (TerrainSideWallCandidate candidate in sectorCandidates)
            {
                if (sideFaceBytes.Count >= 255 - sector.NumHpFaces)
                {
                    string reason = "this high-detail sector would exceed 255 faces.";
                    skippedEdits.Add($"{candidate.RuntimeKey}: terrain side wall skipped because {reason}");
                    SkipSideWallEdge(candidate, reason);
                    continue;
                }

                int newVertexCount = CountNewSideWallVertices(sideVertexIndexes, candidate);
                int requiredBytes = ((sideVertexWords.Count + newVertexCount) * 4) + ((sideFaceBytes.Count + 1) * 16);
                if (sector.NumHpVertices + sideVertexWords.Count + newVertexCount > 255)
                {
                    string reason = "this high-detail sector would exceed 255 vertices.";
                    skippedEdits.Add($"{candidate.RuntimeKey}: terrain side wall skipped because {reason}");
                    SkipSideWallEdge(candidate, reason);
                    continue;
                }
                if (requiredBytes > effectiveAppendSlack)
                {
                    string reason = $"sector 0x{sector.Offset:X} needs {requiredBytes} bytes of append room and only has {effectiveAppendSlack}.";
                    skippedEdits.Add($"{candidate.RuntimeKey}: terrain side wall skipped because {reason}");
                    SkipSideWallEdge(candidate, reason);
                    continue;
                }

                IReadOnlyList<PendingTerrainPatch> candidateCollisionPatches;
                string collisionSkip;
                if (collisionContext.SupportsCollisionIndexRebuild)
                {
                    if (!TryBuildTerrainSideWallCollisionPatchesForIndexRebuild(
                        collisionContext,
                        candidate,
                        sectorUsedReplacementTriangles,
                        out candidateCollisionPatches,
                        out collisionSkip))
                    {
                        if (skippedRuntimeKeys.Add(candidate.RuntimeKey))
                            skippedEdits.Add($"{candidate.RuntimeKey}: terrain side wall collision skipped: {collisionSkip}");
                        SkipSideWallEdge(candidate, collisionSkip);
                        continue;
                    }
                }
                else if (collisionContext.SupportsDirectLookupFallback)
                {
                    if (!TryBuildTerrainSideWallCollisionPatches(
                        collisionContext,
                        candidate,
                        sectorUsedReplacementTriangles,
                        sectorUsedLookupEntryOffsets,
                        out candidateCollisionPatches,
                        out collisionSkip))
                    {
                        if (skippedRuntimeKeys.Add(candidate.RuntimeKey))
                            skippedEdits.Add($"{candidate.RuntimeKey}: terrain side wall collision skipped: {collisionSkip}");
                        SkipSideWallEdge(candidate, collisionSkip);
                        continue;
                    }

                    usesDirectLookupCollision = true;
                }
                else
                {
                    collisionSkip = "collision lookup data is unavailable.";
                    if (skippedRuntimeKeys.Add(candidate.RuntimeKey))
                        skippedEdits.Add($"{candidate.RuntimeKey}: terrain side wall collision skipped: {collisionSkip}");
                    SkipSideWallEdge(candidate, collisionSkip);
                    continue;
                }

                int editedA = candidate.EditedAIndex >= 0
                    ? candidate.EditedAIndex
                    : GetOrAddSideWallVertexIndex(sector, sideVertexIndexes, sideVertexWords, candidate.EditedA);
                int editedB = candidate.EditedBIndex >= 0
                    ? candidate.EditedBIndex
                    : GetOrAddSideWallVertexIndex(sector, sideVertexIndexes, sideVertexWords, candidate.EditedB);
                int originalB = candidate.OriginalBIndex >= 0
                    ? candidate.OriginalBIndex
                    : GetOrAddSideWallVertexIndex(sector, sideVertexIndexes, sideVertexWords, candidate.OriginalB);
                int originalA = candidate.OriginalAIndex >= 0
                    ? candidate.OriginalAIndex
                    : GetOrAddSideWallVertexIndex(sector, sideVertexIndexes, sideVertexWords, candidate.OriginalA);
                byte[] faceBytes = candidate.SourceFaceBytes.ToArray();
                faceBytes[0] = checked((byte)editedA);
                faceBytes[1] = checked((byte)editedB);
                faceBytes[2] = checked((byte)originalB);
                faceBytes[3] = checked((byte)originalA);
                sideFaceBytes.Add(faceBytes);
                collisionPatches.AddRange(candidateCollisionPatches);
                emittedCandidates.Add((candidate, candidateCollisionPatches.Count(patch => string.Equals(patch.Kind, "collision-triangle-side-wall", StringComparison.OrdinalIgnoreCase))));
            }

            if (sideFaceBytes.Count == 0)
                continue;

            if (usesDirectLookupCollision)
            {
                if (!TryValidatePendingTerrainPatches(patchesByWadOffset, collisionPatches, out string directConflictSkip))
                {
                    string reason = $"direct lookup side-wall collision patches conflict: {directConflictSkip}";
                    skippedEdits.Add($"sector 0x{sector.Offset:X}: terrain side walls skipped because {reason}");
                    foreach ((TerrainSideWallCandidate candidate, _) in emittedCandidates)
                        SkipSideWallEdge(candidate, reason);
                    continue;
                }
            }
            else if (!TryAddCollisionIndexRebuildPatches(imageStream, layout, ram, collisionContext, patchesByWadOffset, collisionPatches, out string collisionIndexSkip))
            {
                bool directFallbackBuilt = TryBuildDirectLookupSideWallCollisionPatches(
                    collisionContext,
                    emittedCandidates.Select(item => item.Candidate).ToArray(),
                    baseUsedReplacementTriangles,
                    sectorUsedLookupEntryOffsets,
                    out IReadOnlyList<PendingTerrainPatch> directCollisionPatches,
                    out HashSet<int> directUsedReplacementTriangles,
                    out HashSet<int> directUsedLookupEntryOffsets,
                    out string directLookupSkip);
                string directConflictSkip = "";
                bool directFallbackValid = directFallbackBuilt &&
                    TryValidatePendingTerrainPatches(patchesByWadOffset, directCollisionPatches, out directConflictSkip);
                if (directFallbackValid)
                {
                    collisionPatches = directCollisionPatches.ToList();
                    sectorUsedReplacementTriangles = directUsedReplacementTriangles;
                    sectorUsedLookupEntryOffsets = directUsedLookupEntryOffsets;
                    usesDirectLookupCollision = true;
                }
                else
                {
                    string directReason = string.IsNullOrWhiteSpace(directLookupSkip) ? directConflictSkip : directLookupSkip;
                    string reason = $"rebuilt collision index could not safely include side-wall triangles: {collisionIndexSkip}; direct lookup fallback also failed: {directReason}";
                    skippedEdits.Add($"sector 0x{sector.Offset:X}: terrain side walls skipped because {reason}");
                    foreach ((TerrainSideWallCandidate candidate, _) in emittedCandidates)
                        SkipSideWallEdge(candidate, reason);
                    continue;
                }
            }

            foreach (int replacementIndex in sectorUsedReplacementTriangles)
                usedReplacementTriangles.Add(replacementIndex);
            foreach (int lookupEntryOffset in sectorUsedLookupEntryOffsets)
                usedLookupEntryOffsets.Add(lookupEntryOffset);

            foreach ((TerrainSideWallCandidate candidate, int collisionTriangles) in emittedCandidates)
            {
                TerrainSideWallPatchSummaryBuilder summary = SummaryFor(candidate);
                summary.EmittedFaceCount++;
                summary.CollisionTriangleCount += collisionTriangles;
                summary.AddTextureId(TextureIdFromHpFaceBytes(candidate.SourceFaceBytes));
            }

            byte[] oldTail = ram.AsSpan(hpVertexEndOffset, oldTailLength).ToArray();
            byte[] sideVertexBytes = new byte[sideVertexWords.Count * 4];
            for (int i = 0; i < sideVertexWords.Count; i++)
                BitConverter.GetBytes(sideVertexWords[i]).CopyTo(sideVertexBytes, i * 4);
            byte[] appendedFaceBytes = sideFaceBytes.SelectMany(bytes => bytes).ToArray();
            byte[] shiftedSuffixBytes = suffixShiftPlan?.SuffixBytes ?? Array.Empty<byte>();
            byte[] repackedTail = new byte[sideVertexBytes.Length + oldTail.Length + appendedFaceBytes.Length + shiftedSuffixBytes.Length];
            sideVertexBytes.CopyTo(repackedTail, 0);
            oldTail.CopyTo(repackedTail, sideVertexBytes.Length);
            appendedFaceBytes.CopyTo(repackedTail, sideVertexBytes.Length + oldTail.Length);
            shiftedSuffixBytes.CopyTo(repackedTail, sideVertexBytes.Length + oldTail.Length + appendedFaceBytes.Length);

            long vertexCountWadOffset = sourceSectorWadOffset + 20;
            AddPatch(
                imageStream,
                layout,
                patchesByWadOffset,
                vertexCountWadOffset,
                [(byte)sector.NumHpVertices],
                [(byte)(sector.NumHpVertices + sideVertexWords.Count)],
                "terrain-vertex-count-hp-side-wall",
                $"sector-0x{sector.Offset:X}",
                $"Increase high-detail vertex count from {sector.NumHpVertices} to {sector.NumHpVertices + sideVertexWords.Count} for terrain side walls.");

            long faceCountWadOffset = sourceSectorWadOffset + 22;
            AddPatch(
                imageStream,
                layout,
                patchesByWadOffset,
                faceCountWadOffset,
                [(byte)sector.NumHpFaces],
                [(byte)(sector.NumHpFaces + sideFaceBytes.Count)],
                "terrain-face-count-hp-side-wall",
                $"sector-0x{sector.Offset:X}",
                $"Increase high-detail face count from {sector.NumHpFaces} to {sector.NumHpFaces + sideFaceBytes.Count} for terrain side walls.");

            byte[] expectedBefore = ReadWadBytes(imageStream, layout, repackWadOffset, repackedTail.Length);
            AddPatch(
                imageStream,
                layout,
                patchesByWadOffset,
                repackWadOffset,
                expectedBefore,
                repackedTail,
                "terrain-sector-repack-hp-side-wall",
                $"sector-0x{sector.Offset:X}",
                suffixShiftPlan == null
                    ? $"Repack the high-detail sector tail into {appendSlack} byte(s) of slack, adding {sideVertexWords.Count} side-wall vertices and {sideFaceBytes.Count} side-wall faces{(usesDirectLookupCollision ? " with direct collision lookup fallback" : "")}."
                    : $"Repack the high-detail sector tail and shift {suffixShiftPlan.ShiftedSectorCount} later scene sector(s) by {suffixShiftPlan.ShiftBytes} byte(s), adding {sideVertexWords.Count} side-wall vertices and {sideFaceBytes.Count} side-wall faces{(usesDirectLookupCollision ? " with direct collision lookup fallback" : "")}.");

            foreach (PendingTerrainPatch collisionPatch in collisionPatches)
            {
                AddPatch(
                    imageStream,
                    layout,
                    patchesByWadOffset,
                    collisionPatch.WadOffset,
                    collisionPatch.Before,
                    collisionPatch.After,
                    collisionPatch.Kind,
                    collisionPatch.RuntimeKey,
                    collisionPatch.Description);
            }
        }

        sideWallSummaries.AddRange(sideWallSummaryBuilders.Values
            .Select(summary => summary.ToSummary())
            .OrderBy(summary => summary.RuntimeKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.SectorOffset)
            .ToArray());
    }

    private static bool TryValidatePendingTerrainPatches(
        IReadOnlyDictionary<long, TerrainPatch> existingPatchesByWadOffset,
        IReadOnlyList<PendingTerrainPatch> pendingPatches,
        out string skipReason)
    {
        Dictionary<long, string> pendingAfterByWadOffset = new();
        foreach (PendingTerrainPatch patch in pendingPatches)
        {
            string afterHex = ToHex(patch.After);
            if (existingPatchesByWadOffset.TryGetValue(patch.WadOffset, out TerrainPatch? existing) &&
                !string.Equals(existing.AfterHexPreview, afterHex, StringComparison.OrdinalIgnoreCase))
            {
                skipReason = $"pending patch {patch.Kind} at 0x{patch.WadOffset:X} conflicts with existing {existing.Kind}.";
                return false;
            }

            if (pendingAfterByWadOffset.TryGetValue(patch.WadOffset, out string? pendingAfter) &&
                !string.Equals(pendingAfter, afterHex, StringComparison.OrdinalIgnoreCase))
            {
                skipReason = $"multiple pending side-wall collision patches conflict at 0x{patch.WadOffset:X}.";
                return false;
            }

            pendingAfterByWadOffset[patch.WadOffset] = afterHex;
        }

        skipReason = "";
        return true;
    }

    private static bool HasMatchingEditedTerrainEdge(
        TerrainSideWallCandidate candidate,
        IReadOnlyDictionary<string, List<TerrainSideWallCandidate>> candidatesByOriginalEdge)
    {
        if (!candidatesByOriginalEdge.TryGetValue(candidate.OriginalEdgeKey, out List<TerrainSideWallCandidate>? peers))
            return false;

        return peers.Any(peer =>
            !ReferenceEquals(peer, candidate) &&
            string.Equals(peer.EditedEdgeKey, candidate.EditedEdgeKey, StringComparison.Ordinal));
    }

    private static void ApplyTextureIdToHpFaceBytes(byte[] faceBytes, int textureId)
    {
        if (faceBytes.Length < 12 || textureId < 0)
            return;

        uint oldWord3 = BitConverter.ToUInt32(faceBytes, 8);
        uint newWord3 = (oldWord3 & 0xFFFFFF80u) | (uint)(textureId & 0x7F);
        BitConverter.GetBytes(newWord3).CopyTo(faceBytes, 8);
    }

    private static int TextureIdFromHpFaceBytes(byte[] faceBytes)
    {
        if (faceBytes.Length < 12)
            return -1;

        return (int)(BitConverter.ToUInt32(faceBytes, 8) & 0x7Fu);
    }

    private static int CountNewSideWallVertices(
        IReadOnlyDictionary<string, int> sideVertexIndexes,
        TerrainSideWallCandidate candidate)
    {
        HashSet<string> points = new(StringComparer.Ordinal);
        if (candidate.OriginalBIndex < 0)
            points.Add(TerrainPointKey(candidate.OriginalB));
        if (candidate.OriginalAIndex < 0)
            points.Add(TerrainPointKey(candidate.OriginalA));
        if (candidate.EditedAIndex < 0)
            points.Add(TerrainPointKey(candidate.EditedA));
        if (candidate.EditedBIndex < 0)
            points.Add(TerrainPointKey(candidate.EditedB));
        return points.Count(point => !sideVertexIndexes.ContainsKey(point));
    }

    private static int EstimateSideWallAppendBytes(IEnumerable<TerrainSideWallCandidate> candidates)
    {
        Dictionary<string, int> sideVertexIndexes = new(StringComparer.Ordinal);
        int sideFaceCount = 0;

        void Reserve(Vector3f point)
        {
            string key = TerrainPointKey(point);
            if (!sideVertexIndexes.ContainsKey(key))
                sideVertexIndexes[key] = sideVertexIndexes.Count;
        }

        foreach (TerrainSideWallCandidate candidate in candidates)
        {
            if (candidate.OriginalBIndex < 0)
                Reserve(candidate.OriginalB);
            if (candidate.OriginalAIndex < 0)
                Reserve(candidate.OriginalA);
            if (candidate.EditedAIndex < 0)
                Reserve(candidate.EditedA);
            if (candidate.EditedBIndex < 0)
                Reserve(candidate.EditedB);
            sideFaceCount++;
        }

        return (sideVertexIndexes.Count * 4) + (sideFaceCount * 16);
    }

    private static int GetOrAddSideWallVertexIndex(
        SceneSectorHeader sector,
        Dictionary<string, int> sideVertexIndexes,
        List<uint> sideVertexWords,
        Vector3f point)
    {
        string key = TerrainPointKey(point);
        if (sideVertexIndexes.TryGetValue(key, out int existing))
            return existing;

        int index = sector.NumHpVertices + sideVertexWords.Count;
        sideVertexIndexes[key] = index;
        sideVertexWords.Add(SetSceneVertexWord(0, sector, new Vector2f(point.X, point.Y), point.Z));
        return index;
    }

    private static bool TryBuildTerrainSideWallCollisionPatches(
        CollisionPatchContext collisionContext,
        TerrainSideWallCandidate candidate,
        HashSet<int> usedReplacementTriangles,
        HashSet<int> usedLookupEntryOffsets,
        out IReadOnlyList<PendingTerrainPatch> patches,
        out string skipReason)
    {
        List<PendingTerrainPatch> result = new();
        List<int> reservedReplacementIndexes = new();
        List<int> reservedLookupEntryOffsets = new();
        SpyroCollisionPoint[][] sideTriangles =
        [
            [ToCollisionPoint(candidate.EditedA), ToCollisionPoint(candidate.EditedB), ToCollisionPoint(candidate.OriginalB)],
            [ToCollisionPoint(candidate.EditedA), ToCollisionPoint(candidate.OriginalB), ToCollisionPoint(candidate.OriginalA)]
        ];

        foreach (SpyroCollisionPoint[] sideTriangle in sideTriangles)
        {
            if (!TryBuildTerrainSideWallCollisionPatch(
                collisionContext,
                candidate,
                sideTriangle,
                usedReplacementTriangles,
                usedLookupEntryOffsets,
                reservedReplacementIndexes,
                reservedLookupEntryOffsets,
                out IReadOnlyList<PendingTerrainPatch> trianglePatches,
                out string triangleSkip))
            {
                patches = Array.Empty<PendingTerrainPatch>();
                skipReason = triangleSkip;
                return false;
            }

            result.AddRange(trianglePatches);
        }

        foreach (int replacementIndex in reservedReplacementIndexes)
            usedReplacementTriangles.Add(replacementIndex);
        foreach (int lookupEntryOffset in reservedLookupEntryOffsets)
            usedLookupEntryOffsets.Add(lookupEntryOffset);

        patches = result;
        skipReason = "";
        return result.Count >= 2;
    }

    private static bool TryBuildDirectLookupSideWallCollisionPatches(
        CollisionPatchContext collisionContext,
        IReadOnlyList<TerrainSideWallCandidate> candidates,
        HashSet<int> baseUsedReplacementTriangles,
        HashSet<int> baseUsedLookupEntryOffsets,
        out IReadOnlyList<PendingTerrainPatch> patches,
        out HashSet<int> usedReplacementTriangles,
        out HashSet<int> usedLookupEntryOffsets,
        out string skipReason)
    {
        patches = Array.Empty<PendingTerrainPatch>();
        usedReplacementTriangles = new HashSet<int>(baseUsedReplacementTriangles);
        usedLookupEntryOffsets = new HashSet<int>(baseUsedLookupEntryOffsets);
        skipReason = "";
        if (candidates.Count == 0)
        {
            skipReason = "no emitted side-wall candidates were available for direct lookup fallback.";
            return false;
        }

        List<PendingTerrainPatch> result = new();
        foreach (TerrainSideWallCandidate candidate in candidates)
        {
            if (!TryBuildTerrainSideWallCollisionPatches(
                collisionContext,
                candidate,
                usedReplacementTriangles,
                usedLookupEntryOffsets,
                out IReadOnlyList<PendingTerrainPatch> candidatePatches,
                out string candidateSkip))
            {
                skipReason = $"{candidate.RuntimeKey}: {candidateSkip}";
                return false;
            }

            result.AddRange(candidatePatches);
        }

        patches = result;
        return result.Count >= candidates.Count * 2;
    }

    private static bool TryBuildTerrainSideWallCollisionPatchesForIndexRebuild(
        CollisionPatchContext collisionContext,
        TerrainSideWallCandidate candidate,
        HashSet<int> usedReplacementTriangles,
        out IReadOnlyList<PendingTerrainPatch> patches,
        out string skipReason)
    {
        List<PendingTerrainPatch> result = new();
        List<int> reservedReplacementIndexes = new();
        SpyroCollisionPoint[][] sideTriangles =
        [
            [ToCollisionPoint(candidate.EditedA), ToCollisionPoint(candidate.EditedB), ToCollisionPoint(candidate.OriginalB)],
            [ToCollisionPoint(candidate.EditedA), ToCollisionPoint(candidate.OriginalB), ToCollisionPoint(candidate.OriginalA)]
        ];

        foreach (SpyroCollisionPoint[] sideTriangle in sideTriangles)
        {
            if (!TryBuildTerrainSideWallCollisionTriangleForIndexRebuild(
                collisionContext,
                candidate,
                sideTriangle,
                usedReplacementTriangles,
                reservedReplacementIndexes,
                out PendingTerrainPatch? trianglePatch,
                out string triangleSkip) ||
                trianglePatch == null)
            {
                patches = Array.Empty<PendingTerrainPatch>();
                skipReason = triangleSkip;
                return false;
            }

            result.Add(trianglePatch);
        }

        foreach (int replacementIndex in reservedReplacementIndexes)
            usedReplacementTriangles.Add(replacementIndex);

        patches = result;
        skipReason = "";
        return result.Count >= 2;
    }

    private static bool TryBuildTerrainSideWallCollisionTriangleForIndexRebuild(
        CollisionPatchContext collisionContext,
        TerrainSideWallCandidate candidate,
        IReadOnlyList<SpyroCollisionPoint> sideTriangle,
        HashSet<int> usedReplacementTriangles,
        List<int> reservedReplacementIndexes,
        out PendingTerrainPatch? patch,
        out string skipReason)
    {
        patch = null;
        skipReason = "";
        foreach (SpyroCollisionTriangle sourceTriangle in candidate.SourceCollisionTriangles)
        {
            if (!TryBuildCollisionTriangleWords(sideTriangle, sourceTriangle.ZWord & 0x0000C000u, out uint newXWord, out uint newYWord, out uint newZWord, out string description))
            {
                skipReason = "the wall is too tall, too wide, or points downward for the current packed collision triangle format.";
                continue;
            }

            foreach (int replacementIndex in collisionContext.DegenerateTriangleIndexes)
            {
                if (usedReplacementTriangles.Contains(replacementIndex) ||
                    reservedReplacementIndexes.Contains(replacementIndex) ||
                    !collisionContext.SourceDegenerateTriangleIndexes.Contains(replacementIndex) ||
                    replacementIndex < 0 ||
                    replacementIndex >= collisionContext.Table.Triangles.Count ||
                    replacementIndex > 0x7FFF ||
                    !collisionContext.SourceTriangleBytesByIndex.TryGetValue(replacementIndex, out byte[]? sourceBefore))
                {
                    continue;
                }

                SpyroCollisionTriangle replacementTriangle = collisionContext.Table.Triangles[replacementIndex];
                reservedReplacementIndexes.Add(replacementIndex);
                long wadOffset = GetCollisionTriangleWadOffset(collisionContext, replacementTriangle);
                byte[] before = sourceBefore.ToArray();
                byte[] after = new byte[12];
                BitConverter.GetBytes(newXWord).CopyTo(after, 0);
                BitConverter.GetBytes(newYWord).CopyTo(after, 4);
                BitConverter.GetBytes(newZWord).CopyTo(after, 8);
                patch = new PendingTerrainPatch(
                    WadOffset: wadOffset,
                    Before: before,
                    After: after,
                    Kind: "collision-triangle-side-wall",
                    RuntimeKey: candidate.RuntimeKey,
                    Description: $"Add solid terrain side-wall collision by repurposing degenerate triangle {replacementTriangle.Index} from source triangle {sourceTriangle.Index}: {description}.");
                return true;
            }

            skipReason = "no reusable degenerate collision triangle was available for the rebuilt collision index.";
        }

        skipReason = string.IsNullOrWhiteSpace(skipReason)
            ? "no source collision triangle could be used for this side wall."
            : skipReason;
        return false;
    }

    private static bool TryBuildTerrainSideWallCollisionPatch(
        CollisionPatchContext collisionContext,
        TerrainSideWallCandidate candidate,
        IReadOnlyList<SpyroCollisionPoint> sideTriangle,
        HashSet<int> usedReplacementTriangles,
        HashSet<int> usedLookupEntryOffsets,
        List<int> reservedReplacementIndexes,
        List<int> reservedLookupEntryOffsets,
        out IReadOnlyList<PendingTerrainPatch> patches,
        out string skipReason)
    {
        patches = Array.Empty<PendingTerrainPatch>();
        skipReason = "";
        foreach (SpyroCollisionTriangle sourceTriangle in candidate.SourceCollisionTriangles)
        {
            HashSet<int> blocked = new(usedReplacementTriangles);
            foreach (int reserved in reservedReplacementIndexes)
                blocked.Add(reserved);
            HashSet<int> blockedLookupEntries = new(usedLookupEntryOffsets);
            foreach (int reserved in reservedLookupEntryOffsets)
                blockedLookupEntries.Add(reserved);
            if (!TryFindCollisionReplacementWithDirectLookup(
                collisionContext,
                sourceTriangle.Index,
                blocked,
                blockedLookupEntries,
                "collision-lookup-side-wall",
                "a terrain side wall",
                allowSharedSourceLookupBorrow: true,
                out SpyroCollisionTriangle? replacementTriangle,
                out PendingTerrainPatch? lookupPatch,
                out string directLookupSkip) ||
                replacementTriangle == null)
            {
                if (!string.IsNullOrWhiteSpace(directLookupSkip))
                    skipReason = directLookupSkip;
                continue;
            }

            if (!TryBuildCollisionTriangleWords(sideTriangle, sourceTriangle.ZWord & 0x0000C000u, out uint newXWord, out uint newYWord, out uint newZWord, out string description))
            {
                skipReason = "the wall is too tall, too wide, or points downward for the current packed collision triangle format.";
                continue;
            }

            bool isSourceDerivedAppendTriangle = collisionContext.SourceDerivedAppendTriangleIndexes.Contains(replacementTriangle.Index);
            if (!collisionContext.SourceTriangleBytesByIndex.TryGetValue(replacementTriangle.Index, out byte[]? sourceBefore))
            {
                skipReason = $"replacement collision triangle {replacementTriangle.Index} does not have source bytes.";
                continue;
            }

            if (!collisionContext.SourceDegenerateTriangleIndexes.Contains(replacementTriangle.Index) &&
                !isSourceDerivedAppendTriangle)
            {
                skipReason = $"replacement collision triangle {replacementTriangle.Index} is not a source-confirmed degenerate placeholder or guarded append slot.";
                continue;
            }

            reservedReplacementIndexes.Add(replacementTriangle.Index);
            if (lookupPatch != null)
                reservedLookupEntryOffsets.Add((int)lookupPatch.WadOffset);
            long wadOffset = GetCollisionTriangleWadOffset(collisionContext, replacementTriangle);
            byte[] before = sourceBefore.ToArray();
            byte[] after = new byte[12];
            BitConverter.GetBytes(newXWord).CopyTo(after, 0);
            BitConverter.GetBytes(newYWord).CopyTo(after, 4);
            BitConverter.GetBytes(newZWord).CopyTo(after, 8);
            PendingTerrainPatch trianglePatch = new(
                WadOffset: wadOffset,
                Before: before,
                After: after,
                Kind: "collision-triangle-side-wall",
                RuntimeKey: candidate.RuntimeKey,
                Description: isSourceDerivedAppendTriangle
                    ? $"Add solid terrain side-wall collision by writing appended collision triangle {replacementTriangle.Index} from source triangle {sourceTriangle.Index}: {description}."
                    : $"Add solid terrain side-wall collision by repurposing degenerate triangle {replacementTriangle.Index} from source triangle {sourceTriangle.Index}: {description}.");
            patches = lookupPatch == null ? [trianglePatch] : [lookupPatch, trianglePatch];
            return true;
        }

        skipReason = string.IsNullOrWhiteSpace(skipReason)
            ? "no same-cell degenerate collision placeholder was available."
            : skipReason;
        return false;
    }

    private static IReadOnlyList<SpyroCollisionTriangle> FindMatchingCollisionTriangles(
        CollisionPatchContext collisionContext,
        IReadOnlyList<CollisionPatchVertex> vertices)
    {
        List<SpyroCollisionTriangle> result = new();
        HashSet<int> seen = new();
        for (int i = 1; i < vertices.Count - 1; i++)
        {
            string key = CollisionTriangleKey(
            [
                ToCollisionPoint(vertices[0].Original),
                ToCollisionPoint(vertices[i].Original),
                ToCollisionPoint(vertices[i + 1].Original)
            ]);
            if (!collisionContext.TrianglesByKey.TryGetValue(key, out List<SpyroCollisionTriangle>? triangles))
                continue;

            foreach (SpyroCollisionTriangle triangle in triangles)
            {
                if (seen.Add(triangle.Index))
                    result.Add(triangle);
            }
        }

        return result;
    }

    private static bool PointChanged(Vector3f original, Vector3f edited)
    {
        return Math.Abs(original.X - edited.X) > 0.001f ||
            Math.Abs(original.Y - edited.Y) > 0.001f ||
            Math.Abs(original.Z - edited.Z) > 0.001f;
    }

    private static string TerrainEdgeKey(Vector3f a, Vector3f b)
    {
        string keyA = TerrainPointKey(a);
        string keyB = TerrainPointKey(b);
        return string.CompareOrdinal(keyA, keyB) <= 0 ? $"{keyA}|{keyB}" : $"{keyB}|{keyA}";
    }

    private static string TerrainPointKey(Vector3f point)
    {
        return $"{(int)Math.Round(point.X)},{(int)Math.Round(point.Y)},{(int)Math.Round(point.Z)}";
    }

    private static bool TryFindCollisionReplacementWithDirectLookup(
        CollisionPatchContext collisionContext,
        int sourceTriangleIndex,
        HashSet<int> usedReplacementTriangles,
        HashSet<int> usedLookupEntryWadOffsets,
        string lookupKind,
        string descriptionPurpose,
        bool allowSharedSourceLookupBorrow,
        out SpyroCollisionTriangle? replacementTriangle,
        out PendingTerrainPatch? lookupPatch,
        out string skipReason)
    {
        lookupPatch = null;
        skipReason = "";
        if (TryFindCollisionCloneReplacement(collisionContext, sourceTriangleIndex, usedReplacementTriangles, out replacementTriangle))
            return replacementTriangle != null;

        replacementTriangle = null;
        if (collisionContext.SourceBlocksWadOffset < 0 ||
            !collisionContext.GroupIndexesByTriangleIndex.TryGetValue(sourceTriangleIndex, out List<int>? groupIndexes))
        {
            skipReason = "source collision triangle is not referenced by the decoded collision lookup groups.";
            return false;
        }

        bool hasAppendSlots = TryGetSourceDerivedAppendTriangleIndexes(collisionContext, sourceTriangleIndex, out IReadOnlyList<int> appendIndexes) &&
            appendIndexes.Count > 0;
        bool foundRedundantEntry = false;
        bool foundWritableRedundantEntry = false;
        bool foundSharedSourceEntry = false;
        foreach (int groupIndex in groupIndexes)
        {
            if (groupIndex < 0 || groupIndex >= collisionContext.LookupGroupEntries.Count)
                continue;

            IReadOnlyList<CollisionLookupEntry> groupEntries = collisionContext.LookupGroupEntries[groupIndex];
            CollisionLookupEntry[] duplicateEntries = FindRedundantLookupEntries(collisionContext, groupEntries);
            foreach (CollisionLookupEntry duplicateEntry in duplicateEntries)
            {
                foundRedundantEntry = true;
                int redundantReplacementIndex = duplicateEntry.TriangleIndex;
                if (!usedReplacementTriangles.Contains(redundantReplacementIndex) &&
                    redundantReplacementIndex >= 0 &&
                    redundantReplacementIndex < collisionContext.Table.Triangles.Count &&
                    redundantReplacementIndex <= 0x7FFF &&
                    collisionContext.SourceDegenerateTriangleIndexes.Contains(redundantReplacementIndex) &&
                    collisionContext.SourceTriangleBytesByIndex.ContainsKey(redundantReplacementIndex) &&
                    IsCollisionTriangleOnlyReferencedByRedundantLookupEntries(collisionContext, redundantReplacementIndex))
                {
                    replacementTriangle = collisionContext.Table.Triangles[redundantReplacementIndex];
                    lookupPatch = null;
                    return true;
                }

                long lookupWadOffset = collisionContext.SourceBlocksWadOffset + (duplicateEntry.Offset - collisionContext.Table.BlocksOffset);
                if (lookupWadOffset < 0 ||
                    lookupWadOffset > int.MaxValue ||
                    usedLookupEntryWadOffsets.Contains((int)lookupWadOffset))
                {
                    continue;
                }

                if (!collisionContext.SourceLookupWordsByOffset.TryGetValue(duplicateEntry.Offset, out ushort duplicateSourceWord) ||
                    duplicateSourceWord != duplicateEntry.Word)
                {
                    continue;
                }

                foundWritableRedundantEntry = true;

                foreach (int replacementIndex in collisionContext.UnreferencedDegenerateTriangleIndexes)
                {
                    if (usedReplacementTriangles.Contains(replacementIndex) ||
                        !collisionContext.SourceDegenerateTriangleIndexes.Contains(replacementIndex) ||
                        !collisionContext.SourceTriangleBytesByIndex.ContainsKey(replacementIndex) ||
                        replacementIndex < 0 ||
                        replacementIndex >= collisionContext.Table.Triangles.Count ||
                        replacementIndex > 0x7FFF)
                    {
                        continue;
                    }

                    int replacementSourceIndex = GetCollisionTriangleLookupIndex(collisionContext, replacementIndex);
                    if (replacementSourceIndex < 0 || replacementSourceIndex > 0x7FFF)
                        continue;

                    ushort newWord = (ushort)((duplicateEntry.Word & 0x8000) | (replacementSourceIndex & 0x7FFF));
                    if (newWord == duplicateEntry.Word)
                        continue;

                    replacementTriangle = collisionContext.Table.Triangles[replacementIndex];
                    lookupPatch = new PendingTerrainPatch(
                        WadOffset: lookupWadOffset,
                        Before: BitConverter.GetBytes(duplicateSourceWord),
                        After: BitConverter.GetBytes(newWord),
                        Kind: lookupKind,
                        RuntimeKey: $"collision-cell-{groupIndex}",
                        Description: $"Point duplicate collision lookup cell {groupIndex} entry {duplicateEntry.EntryIndex} at unreferenced degenerate triangle {replacementIndex} for {descriptionPurpose}.");
                    return true;
                }

                if (TryFindSourceDerivedAppendCollisionSlot(
                    collisionContext,
                    sourceTriangleIndex,
                    groupIndex,
                    duplicateEntry,
                    usedReplacementTriangles,
                    usedLookupEntryWadOffsets,
                    lookupKind,
                    descriptionPurpose,
                    out replacementTriangle,
                    out lookupPatch))
                {
                    return replacementTriangle != null;
                }
            }

            if (!allowSharedSourceLookupBorrow || !hasAppendSlots || groupIndexes.Count <= 1)
                continue;

            foreach (CollisionLookupEntry sourceEntry in groupEntries.Where(entry => entry.TriangleIndex == sourceTriangleIndex))
            {
                foundSharedSourceEntry = true;
                if (!TryFindSourceDerivedAppendCollisionSlot(
                    collisionContext,
                    sourceTriangleIndex,
                    groupIndex,
                    sourceEntry,
                    usedReplacementTriangles,
                    usedLookupEntryWadOffsets,
                    lookupKind,
                    descriptionPurpose,
                    out replacementTriangle,
                    out lookupPatch))
                {
                    continue;
                }

                if (lookupPatch != null)
                {
                    lookupPatch = lookupPatch with
                    {
                        Description = $"Point shared source collision lookup cell {groupIndex} entry {sourceEntry.EntryIndex} at appended collision triangle {replacementTriangle?.Index ?? -1} for {descriptionPurpose}; source triangle {sourceTriangleIndex} remains referenced by another collision cell."
                    };
                }

                return replacementTriangle != null;
            }
        }

        if (!foundRedundantEntry)
            skipReason = hasAppendSlots
                ? allowSharedSourceLookupBorrow
                    ? foundSharedSourceEntry
                        ? "guarded append collision slots exist, but no shared source lookup entry could be safely redirected."
                        : "guarded append collision slots exist, but no same-cell duplicate or safely shared source lookup entry was available."
                    : "guarded append collision slots exist, but no same-cell duplicate lookup entry was available."
                : "no same-cell duplicate lookup entry was available for a new collision triangle.";
        else if (!foundWritableRedundantEntry)
            skipReason = "same-cell duplicate lookup entries were already reserved or did not match the source lookup bytes.";
        else if (hasAppendSlots)
            skipReason = "same-cell duplicate lookup entries were available, but no free guarded append collision slot could be addressed.";
        else
            skipReason = "same-cell duplicate lookup entries were available, but no source-confirmed degenerate placeholder or guarded append slot was available.";
        return false;
    }

    private static bool TryFindSourceDerivedAppendCollisionSlot(
        CollisionPatchContext collisionContext,
        int sourceTriangleIndex,
        int groupIndex,
        CollisionLookupEntry duplicateEntry,
        HashSet<int> usedReplacementTriangles,
        HashSet<int> usedLookupEntryWadOffsets,
        string lookupKind,
        string descriptionPurpose,
        out SpyroCollisionTriangle? replacementTriangle,
        out PendingTerrainPatch? lookupPatch)
    {
        replacementTriangle = null;
        lookupPatch = null;
        if (!TryGetSourceDerivedAppendTriangleIndexes(collisionContext, sourceTriangleIndex, out IReadOnlyList<int> appendIndexes) ||
            appendIndexes.Count == 0)
        {
            return false;
        }

        long lookupWadOffset = collisionContext.SourceBlocksWadOffset + (duplicateEntry.Offset - collisionContext.Table.BlocksOffset);
        if (lookupWadOffset < 0 ||
            lookupWadOffset > int.MaxValue ||
            usedLookupEntryWadOffsets.Contains((int)lookupWadOffset))
        {
            return false;
        }

        if (!collisionContext.SourceLookupWordsByOffset.TryGetValue(duplicateEntry.Offset, out ushort sourceWord) ||
            sourceWord != duplicateEntry.Word)
        {
            return false;
        }

        foreach (int appendIndex in appendIndexes)
        {
            if (usedReplacementTriangles.Contains(appendIndex) ||
                !collisionContext.SourceDerivedAppendTriangleIndexes.Contains(appendIndex) ||
                !collisionContext.SourceTriangleBytesByIndex.ContainsKey(appendIndex) ||
                appendIndex < 0 ||
                appendIndex >= collisionContext.Table.Triangles.Count)
            {
                continue;
            }

            int replacementSourceIndex = GetCollisionTriangleLookupIndex(collisionContext, appendIndex);
            if (replacementSourceIndex < 0 || replacementSourceIndex > 0x7FFF)
                continue;

            ushort newWord = (ushort)((duplicateEntry.Word & 0x8000) | (replacementSourceIndex & 0x7FFF));
            if (newWord == duplicateEntry.Word)
                continue;

            replacementTriangle = collisionContext.Table.Triangles[appendIndex];
            lookupPatch = new PendingTerrainPatch(
                WadOffset: lookupWadOffset,
                Before: BitConverter.GetBytes(sourceWord),
                After: BitConverter.GetBytes(newWord),
                Kind: lookupKind,
                RuntimeKey: $"collision-cell-{groupIndex}",
                Description: $"Point duplicate collision lookup cell {groupIndex} entry {duplicateEntry.EntryIndex} at appended collision triangle {appendIndex} for {descriptionPurpose}.");
            return true;
        }

        return false;
    }

    private static bool TryGetSourceDerivedAppendTriangleIndexes(
        CollisionPatchContext collisionContext,
        int sourceTriangleIndex,
        out IReadOnlyList<int> appendIndexes)
    {
        appendIndexes = Array.Empty<int>();
        if (!collisionContext.SourceTriangleLookupIndexByIndex.TryGetValue(sourceTriangleIndex, out int sourceLookupIndex))
            return false;

        int triangleIndexBase = sourceTriangleIndex - sourceLookupIndex;
        if (!collisionContext.SourceDerivedAppendTriangleIndexesByBase.TryGetValue(triangleIndexBase, out IReadOnlyList<int>? foundAppendIndexes))
            return false;

        appendIndexes = foundAppendIndexes;
        return true;
    }

    private static bool IsCollisionTriangleOnlyReferencedByRedundantLookupEntries(
        CollisionPatchContext collisionContext,
        int triangleIndex)
    {
        bool foundReference = false;
        foreach (IReadOnlyList<CollisionLookupEntry> groupEntries in collisionContext.LookupGroupEntries)
        {
            HashSet<int> redundantOffsets = FindRedundantLookupEntries(collisionContext, groupEntries)
                .Select(entry => entry.Offset)
                .ToHashSet();

            foreach (CollisionLookupEntry entry in groupEntries.Where(entry => entry.TriangleIndex == triangleIndex))
            {
                foundReference = true;
                if (!redundantOffsets.Contains(entry.Offset))
                    return false;
            }
        }

        return foundReference;
    }

    private static CollisionLookupEntry[] FindRedundantLookupEntries(
        CollisionPatchContext collisionContext,
        IReadOnlyList<CollisionLookupEntry> entries)
    {
        List<CollisionLookupEntry> redundant = new();
        HashSet<int> seenOffsets = new();
        foreach (CollisionLookupEntry entry in entries
            .GroupBy(entry => entry.TriangleIndex)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Skip(1)))
        {
            if (seenOffsets.Add(entry.Offset))
                redundant.Add(entry);
        }

        foreach (CollisionLookupEntry entry in entries
            .Where(entry => entry.TriangleIndex >= 0 && entry.TriangleIndex < collisionContext.Table.Triangles.Count)
            .GroupBy(entry => CollisionTriangleKey(collisionContext.Table.Triangles[entry.TriangleIndex].Points), StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Skip(1)))
        {
            if (seenOffsets.Add(entry.Offset))
                redundant.Add(entry);
        }

        return redundant.ToArray();
    }

    private static bool TryFindCollisionCloneReplacement(
        CollisionPatchContext collisionContext,
        int sourceTriangleIndex,
        HashSet<int> usedReplacementTriangles,
        out SpyroCollisionTriangle? replacementTriangle)
    {
        replacementTriangle = null;
        if (!collisionContext.GroupIndexesByTriangleIndex.TryGetValue(sourceTriangleIndex, out List<int>? groupIndexes))
            return false;

        foreach (int groupIndex in groupIndexes)
        {
            if (groupIndex < 0 || groupIndex >= collisionContext.LookupGroups.Count)
                continue;

            foreach (int candidateIndex in collisionContext.LookupGroups[groupIndex])
            {
                if (candidateIndex == sourceTriangleIndex ||
                    usedReplacementTriangles.Contains(candidateIndex) ||
                    !collisionContext.DegenerateTriangleIndexes.Contains(candidateIndex) ||
                    !collisionContext.SourceDegenerateTriangleIndexes.Contains(candidateIndex) ||
                    !collisionContext.SourceTriangleBytesByIndex.ContainsKey(candidateIndex) ||
                    candidateIndex < 0 ||
                    candidateIndex >= collisionContext.Table.Triangles.Count)
                {
                    continue;
                }

                replacementTriangle = collisionContext.Table.Triangles[candidateIndex];
                return true;
            }
        }

        return false;
    }

    private static long GetCollisionTriangleWadOffset(CollisionPatchContext collisionContext, SpyroCollisionTriangle triangle)
    {
        if (collisionContext.SourceTriangleWadOffsetsByIndex.TryGetValue(triangle.Index, out long wadOffset))
            return wadOffset;

        return collisionContext.SourceTriangleWadOffset + (triangle.Offset - collisionContext.Table.TriangleOffset);
    }

    private static int GetCollisionTriangleLookupIndex(CollisionPatchContext collisionContext, int triangleIndex)
    {
        return collisionContext.SourceTriangleLookupIndexByIndex.TryGetValue(triangleIndex, out int sourceIndex)
            ? sourceIndex
            : triangleIndex;
    }

    private static int GetHpVertexDataEndOffset(SceneSectorHeader sector)
    {
        int dataStart = sector.Offset + 28;
        int hpVertexStartWords = sector.NumLpVertices + sector.NumLpColours + (sector.NumLpFaces * 2);
        return dataStart + ((hpVertexStartWords + sector.NumHpVertices) * 4);
    }

    private static int IndexOfVertex(IReadOnlyList<int> indexes, int vertexIndex)
    {
        for (int i = 0; i < indexes.Count; i++)
        {
            if (indexes[i] == vertexIndex)
                return i;
        }

        return -1;
    }

    private static int FindSectorAppendSlack(IReadOnlyDictionary<string, SourceSectorLocation> sourceSectorHits, long sourceSectorWadOffset, int sectorSizeBytes)
    {
        long sectorEnd = sourceSectorWadOffset + sectorSizeBytes;
        long nextSector = sourceSectorHits.Values
            .Select(location => location.WadOffset)
            .Where(offset => offset > sourceSectorWadOffset)
            .Distinct()
            .DefaultIfEmpty(-1)
            .Min();
        if (nextSector < 0)
            return 0;

        long slack = nextSector - sectorEnd;
        return slack > int.MaxValue ? int.MaxValue : (int)slack;
    }

    private static bool TryBuildTerrainSectorSuffixShiftPlan(
        FileStream imageStream,
        DiscLayout layout,
        IReadOnlyDictionary<string, SourceSectorLocation> sourceSectorHits,
        AssetSubfileInfo modelSubfileInfo,
        IReadOnlyList<long> editedSceneSectorWadOffsets,
        long sourceSectorWadOffset,
        int sectorSizeBytes,
        int requiredAppendBytes,
        out TerrainSectorSuffixShiftPlan? plan,
        out string skipReason)
    {
        plan = null;
        skipReason = "";
        int appendSlack = Math.Max(0, FindSectorAppendSlack(sourceSectorHits, sourceSectorWadOffset, sectorSizeBytes));
        int shiftBytes = requiredAppendBytes - appendSlack;
        if (shiftBytes <= 0)
        {
            skipReason = "this scene sector already has enough immediate append room.";
            return false;
        }

        long sectorEnd = sourceSectorWadOffset + sectorSizeBytes;
        SourceSectorLocation[] orderedLocations = sourceSectorHits.Values
            .GroupBy(location => location.WadOffset)
            .Select(group => group.First())
            .OrderBy(location => location.WadOffset)
            .ToArray();
        SourceSectorLocation? nextLocation = orderedLocations.FirstOrDefault(location => location.WadOffset > sourceSectorWadOffset);
        if (nextLocation == null || nextLocation.WadOffset < 0)
        {
            skipReason = "no following scene sector was found to shift.";
            return false;
        }

        long suffixStart = nextLocation.WadOffset;
        if (suffixStart < sectorEnd)
        {
            skipReason = "the next scene sector overlaps this sector, so appending side-wall bytes would be unsafe.";
            return false;
        }

        long chainTailEnd = orderedLocations
            .Select(location => location.WadOffset + location.SizeBytes)
            .DefaultIfEmpty(-1)
            .Max();
        long modelEnd = modelSubfileInfo.AbsoluteWadOffset + modelSubfileInfo.SubfileSize;
        if (chainTailEnd < suffixStart || chainTailEnd > modelEnd)
        {
            skipReason = "the scene-sector chain could not be bounded inside the level model data.";
            return false;
        }

        long modelTailSlack = modelEnd - chainTailEnd;
        if (modelTailSlack < shiftBytes)
        {
            skipReason = $"the model data has only {modelTailSlack} tail byte(s), but this side-wall edit needs {shiftBytes}.";
            return false;
        }

        if (editedSceneSectorWadOffsets.Any(offset => offset >= suffixStart && offset < chainTailEnd))
        {
            skipReason = "another terrain edit touches a later scene sector that would be shifted; save/apply this side-wall edit separately until multi-sector rebasing is implemented.";
            return false;
        }

        SourceSectorLocation[] shiftedLocations = orderedLocations
            .Where(location => location.WadOffset >= suffixStart && location.WadOffset < chainTailEnd)
            .ToArray();
        if (shiftedLocations.Length == 0)
        {
            skipReason = "no later scene sectors were available to shift.";
            return false;
        }

        long suffixLength = chainTailEnd - suffixStart;
        if (suffixLength > int.MaxValue)
        {
            skipReason = "the scene-sector suffix is too large to patch safely in one terrain edit.";
            return false;
        }

        plan = new TerrainSectorSuffixShiftPlan(
            ShiftStartWadOffset: suffixStart,
            ChainTailEndWadOffset: chainTailEnd,
            ShiftBytes: shiftBytes,
            ShiftedSectorCount: shiftedLocations.Length,
            SuffixBytes: ReadWadBytes(imageStream, layout, suffixStart, (int)suffixLength));
        return true;
    }

    private static void AddFaceRemovalPatches(
        FileStream imageStream,
        DiscLayout layout,
        byte[] ram,
        CollisionPatchContext? collisionContext,
        SceneSectorHeader sector,
        long sourceSectorWadOffset,
        int sectorOffset,
        string detail,
        JsonElement edit,
        Dictionary<long, TerrainPatch> patchesByWadOffset,
        List<string> skippedEdits)
    {
        string runtimeKey = JsonValue.GetString(edit, "runtimeKey");
        int faceOffset = JsonValue.GetInt32(edit, "faceOffset", -1);
        int faceByteLength = string.Equals(detail, "lp", StringComparison.OrdinalIgnoreCase) ? 8 : 16;
        if (faceOffset < sectorOffset || faceOffset + faceByteLength > sectorOffset + sector.SizeBytes)
        {
            skippedEdits.Add($"{runtimeKey}: remove-face edit is missing a valid {detail} face offset; re-save this terrain edit.");
            return;
        }

        if (!AddCollisionRemovalPatches(imageStream, layout, ram, collisionContext, sector, detail, edit, patchesByWadOffset, skippedEdits))
        {
            skippedEdits.Add($"{runtimeKey}: remove-face was not patched because the matching gameplay collision could not be removed safely.");
            return;
        }

        byte[] before = ram.AsSpan(faceOffset, 4).ToArray();
        byte[] after = [before[0], before[0], before[0], before[0]];
        if (before.SequenceEqual(after))
        {
            skippedEdits.Add($"{runtimeKey}: visible face is already degenerate.");
            return;
        }

        long wadOffset = sourceSectorWadOffset + (faceOffset - sectorOffset);
        AddPatch(
            imageStream,
            layout,
            patchesByWadOffset,
            wadOffset,
            before,
            after,
            $"terrain-face-degenerate-{detail}",
            runtimeKey,
            $"Remove visible terrain face by repeating vertex index {before[0]} without resizing the source sector.");
    }

    private static bool AddCollisionRemovalPatches(
        FileStream imageStream,
        DiscLayout layout,
        byte[] ram,
        CollisionPatchContext? collisionContext,
        SceneSectorHeader sector,
        string detail,
        JsonElement edit,
        Dictionary<long, TerrainPatch> patchesByWadOffset,
        List<string> skippedEdits)
    {
        string runtimeKey = JsonValue.GetString(edit, "runtimeKey");
        if (collisionContext == null)
        {
            skippedEdits.Add($"{runtimeKey}: collision triangle source bytes were not located.");
            return false;
        }

        IReadOnlyList<int> rawVertexIndexes = ReadIntArray(edit, "vertexIndexes");
        IReadOnlyList<float> originalZ = ReadFloatArray(edit, "originalZ");
        IReadOnlyList<Vector2f> originalPoints = ReadVector2Array(edit, "originalPoints");
        int vertexValueCount = MaxValueCount(originalZ.Count, originalPoints.Count);
        if (vertexValueCount <= 0)
            vertexValueCount = rawVertexIndexes.Count;
        IReadOnlyList<int> vertexIndexes = ResolveEditableVertexIndexes(
            sector,
            detail,
            rawVertexIndexes,
            vertexValueCount,
            originalPoints,
            originalZ);
        if (vertexIndexes.Count < 3)
        {
            skippedEdits.Add($"{runtimeKey}: remove-face edit is missing enough vertex indexes to match collision triangles.");
            return false;
        }

        List<SpyroCollisionPoint> points = new(vertexIndexes.Count);
        foreach (int vertexIndex in vertexIndexes)
        {
            int vertexOffset = GetSceneVertexOffset(sector, detail, vertexIndex);
            if (vertexOffset < 0)
            {
                skippedEdits.Add($"{runtimeKey}: vertex {vertexIndex} is not valid for {detail} sector collision removal.");
                return false;
            }

            Vector3f point = DecodeSceneVertex(ReadUInt32(ram, vertexOffset), sector);
            points.Add(new SpyroCollisionPoint((int)Math.Round(point.X), (int)Math.Round(point.Y), (int)Math.Round(point.Z)));
        }

        HashSet<int> patchedTriangles = new();
        int matchedTriangles = 0;
        int encodedTriangles = 0;
        int addedPatches = 0;
        for (int i = 1; i < points.Count - 1; i++)
        {
            string key = CollisionTriangleKey([points[0], points[i], points[i + 1]]);
            if (!collisionContext.TrianglesByKey.TryGetValue(key, out List<SpyroCollisionTriangle>? triangles))
                continue;

            foreach (SpyroCollisionTriangle triangle in triangles)
            {
                matchedTriangles++;
                if (!patchedTriangles.Add(triangle.Index))
                    continue;
                if (!TryBuildDegenerateCollisionTriangleWords(triangle, out uint newXWord, out uint newYWord, out uint newZWord, out string description))
                    continue;
                encodedTriangles++;
                if (newXWord == triangle.XWord && newYWord == triangle.YWord && newZWord == triangle.ZWord)
                    continue;

                long wadOffset = GetCollisionTriangleWadOffset(collisionContext, triangle);
                byte[] before = new byte[12];
                byte[] after = new byte[12];
                BitConverter.GetBytes(triangle.XWord).CopyTo(before, 0);
                BitConverter.GetBytes(triangle.YWord).CopyTo(before, 4);
                BitConverter.GetBytes(triangle.ZWord).CopyTo(before, 8);
                BitConverter.GetBytes(newXWord).CopyTo(after, 0);
                BitConverter.GetBytes(newYWord).CopyTo(after, 4);
                BitConverter.GetBytes(newZWord).CopyTo(after, 8);
                AddPatch(
                    imageStream,
                    layout,
                    patchesByWadOffset,
                    wadOffset,
                    before,
                    after,
                    "collision-triangle-degenerate",
                    runtimeKey,
                    $"Remove playable collision triangle {triangle.Index} by collapsing it to {description}.");
                addedPatches++;
            }
        }

        if (matchedTriangles == 0)
            skippedEdits.Add($"{runtimeKey}: remove-face could not find matching collision triangles.");
        else if (encodedTriangles == 0)
            skippedEdits.Add($"{runtimeKey}: matched {matchedTriangles} collision triangle(s), but could not encode degenerate collision removal.");
        return addedPatches > 0;
    }

    private static bool TryBuildDegenerateCollisionTriangleWords(
        SpyroCollisionTriangle triangle,
        out uint xWord,
        out uint yWord,
        out uint zWord,
        out string description)
    {
        xWord = triangle.XWord;
        yWord = triangle.YWord;
        zWord = triangle.ZWord;
        description = "";
        if (triangle.Points.Count == 0)
            return false;

        SpyroCollisionPoint p1 = triangle.Points[0];
        if (p1.X < 0 || p1.X > 0x3FFF || p1.Y < 0 || p1.Y > 0x3FFF || p1.Z < 0 || p1.Z > 0x3FFF)
            return false;

        xWord = (uint)(p1.X & 0x3FFF);
        yWord = (uint)(p1.Y & 0x3FFF);
        zWord = (triangle.ZWord & 0x0000C000u) | (uint)(p1.Z & 0x3FFF);
        description = $"{p1.X},{p1.Y},{p1.Z}";
        return true;
    }

    private static void AddTexturePatch(
        FileStream imageStream,
        DiscLayout layout,
        byte[] ram,
        LevelDefinition level,
        SceneSectorHeader sector,
        long sourceSectorWadOffset,
        int sectorOffset,
        JsonElement edit,
        Dictionary<(int SectorOffset, int ColorIndex), TerrainTextureVisualCorner> nativeVisualColorReservations,
        Dictionary<long, TerrainPatch> patchesByWadOffset,
        List<string> skippedEdits)
    {
        int textureIdEdited = JsonValue.GetInt32(edit, "textureIdEdited", -1);
        if (textureIdEdited < 0)
            return;

        string runtimeKey = JsonValue.GetString(edit, "runtimeKey");
        bool hasNativeVisualEdit = JsonValue.GetBoolean(edit, "nativeTextureVisualEdit");
        int faceOffset = JsonValue.GetInt32(edit, "faceOffset", -1);
        int requiredFaceBytes = hasNativeVisualEdit ? 16 : 12;
        if (faceOffset < sectorOffset || faceOffset + requiredFaceBytes > sectorOffset + sector.SizeBytes)
        {
            AddAtomicTerrainSwapBlock(
                skippedEdits,
                $"{runtimeKey}: texture edit is missing a valid face offset; re-save this edit.");
            return;
        }

        int word3Offset = faceOffset + 8;
        long wadOffset = sourceSectorWadOffset + (word3Offset - sectorOffset);
        uint oldWord = ReadUInt32(ram, word3Offset);
        if (!hasNativeVisualEdit)
        {
            string textureEditMode = JsonValue.GetString(edit, "textureEditMode");
            if (string.Equals(textureEditMode, "native-resident-face-swap", StringComparison.OrdinalIgnoreCase))
            {
                AddAtomicTerrainSwapBlock(
                    skippedEdits,
                    $"{runtimeKey}: the resident native texture swap is missing its source-verified four-corner near/fade tint recipe; reopen the catalog and reselect the look.");
                return;
            }
            uint newWord = (oldWord & 0xFFFFFF80u) | (uint)(textureIdEdited & 0x7F);
            AddPatch(imageStream, layout, patchesByWadOffset, wadOffset, BitConverter.GetBytes(oldWord), BitConverter.GetBytes(newWord), "texture-id-word3", runtimeKey, $"Set terrain texture id to {textureIdEdited}.");
            return;
        }

        string detail = JsonValue.GetString(edit, "detail", "hp");
        if (!string.Equals(detail, "hp", StringComparison.OrdinalIgnoreCase))
        {
            AddAtomicTerrainSwapBlock(
                skippedEdits,
                $"{runtimeKey}: native texture visuals need one high-detail face.");
            return;
        }

        if (!TryReadNativeTextureVisualEdit(edit, textureIdEdited, out TerrainTextureVisualEdit visualEdit, out string visualEditError))
        {
            AddAtomicTerrainSwapBlock(
                skippedEdits,
                $"{runtimeKey}: {visualEditError}");
            return;
        }
        if (!string.Equals(
                LevelCatalog.NormalizeKey(visualEdit.SourceLevelKey),
                LevelCatalog.NormalizeKey(level.Key),
                StringComparison.OrdinalIgnoreCase))
        {
            AddAtomicTerrainSwapBlock(
                skippedEdits,
                $"{runtimeKey}: resident native texture visuals must come from the same level; saved donor level '{visualEdit.SourceLevelKey}' does not match {level.Key}.");
            return;
        }
        if (visualEdit.SourceTextureId != textureIdEdited)
        {
            AddAtomicTerrainSwapBlock(
                skippedEdits,
                $"{runtimeKey}: saved visual donor texture {visualEdit.SourceTextureId} does not match edited texture {textureIdEdited}.");
            return;
        }
        if (!NativeTerrainTextureVisualInspector.TryInspectLogicalWad(
                ram,
                visualEdit.SourceLevelKey,
                visualEdit.SourceRuntimeKey,
                visualEdit.SourceSectorOffset,
                visualEdit.SourceFaceOffset,
                out TerrainTextureVisualEdit sourceVisual,
                out string sourceVisualError))
        {
            AddAtomicTerrainSwapBlock(
                skippedEdits,
                $"{runtimeKey}: saved visual donor could not be re-read from the selected source BIN: {sourceVisualError}");
            return;
        }
        if (sourceVisual.SourceTextureId != visualEdit.SourceTextureId ||
            !sourceVisual.Corners.SequenceEqual(visualEdit.Corners))
        {
            AddAtomicTerrainSwapBlock(
                skippedEdits,
                $"{runtimeKey}: saved donor texture/tints no longer match source face {visualEdit.SourceRuntimeKey}; reopen the catalog and reselect the look.");
            return;
        }

        TerrainTextureVisualCorner[] uniqueCornerPairs = visualEdit.Corners
            .Distinct()
            .ToArray();
        if (!NativeTerrainTextureVisualInspector.TryFindWritableColorSlotsInLogicalWad(
                ram,
                sectorOffset,
                faceOffset,
                requiredCount: 0,
                out int[] candidateColorIndexes,
                out string privateColorError))
        {
            AddAtomicTerrainSwapBlock(
                skippedEdits,
                $"{runtimeKey}: {privateColorError} Nearby terrain was not recolored.");
            return;
        }

        int hpColorStart = GetHpColorDataStartOffset(sector);
        Dictionary<TerrainTextureVisualCorner, int> colorSlotByPair = new();
        HashSet<int> locallyAllocatedColorIndexes = new();
        foreach (TerrainTextureVisualCorner pair in uniqueCornerPairs)
        {
            int privateColorIndex = -1;
            foreach (int candidate in candidateColorIndexes)
            {
                if (locallyAllocatedColorIndexes.Contains(candidate))
                    continue;
                if (nativeVisualColorReservations.TryGetValue((sectorOffset, candidate), out TerrainTextureVisualCorner? reservedPair) &&
                    reservedPair == pair)
                {
                    privateColorIndex = candidate;
                    break;
                }
            }
            if (privateColorIndex < 0)
            {
                foreach (int candidate in candidateColorIndexes)
                {
                    if (!locallyAllocatedColorIndexes.Contains(candidate) &&
                        !nativeVisualColorReservations.ContainsKey((sectorOffset, candidate)))
                    {
                        privateColorIndex = candidate;
                        break;
                    }
                }
            }
            if (privateColorIndex < 0)
            {
                AddAtomicTerrainSwapBlock(
                    skippedEdits,
                    $"{runtimeKey}: the target sector has no remaining private native tint slot for {uniqueCornerPairs.Length} requested pair(s) after earlier face swaps reserved {nativeVisualColorReservations.Count(reservation => reservation.Key.SectorOffset == sectorOffset)} slot(s). Nearby terrain was not recolored.");
                return;
            }

            NativeTerrainHpColorOffsets colorOffsets =
                NativeTerrainHpColorLayout.GetColorOffsets(
                    hpColorStart,
                    sector.NumHpColours,
                    privateColorIndex);
            if (colorOffsets.Table1Offset < sectorOffset ||
                colorOffsets.Table2Offset + NativeTerrainHpColorLayout.ColorBytes > sectorOffset + sector.SizeBytes)
            {
                AddAtomicTerrainSwapBlock(
                    skippedEdits,
                    $"{runtimeKey}: private high-detail color slot {privateColorIndex} is outside the validated scene sector.");
                return;
            }
            colorSlotByPair[pair] = privateColorIndex;
            locallyAllocatedColorIndexes.Add(privateColorIndex);
        }
        foreach ((TerrainTextureVisualCorner pair, int privateColorIndex) in colorSlotByPair)
        {
            (int SectorOffset, int ColorIndex) reservationKey = (sectorOffset, privateColorIndex);
            if (nativeVisualColorReservations.TryGetValue(reservationKey, out TerrainTextureVisualCorner? reservedPair) &&
                reservedPair != pair)
            {
                AddAtomicTerrainSwapBlock(
                    skippedEdits,
                    $"{runtimeKey}: native tint slot {privateColorIndex} was already reserved for a different near/fade pair. No overlapping tint patch was staged.");
                return;
            }
            nativeVisualColorReservations[reservationKey] = pair;
        }

        uint newVisualWord = (oldWord & 0xFFFFFF80u) | (uint)(textureIdEdited & 0x7F);
        AddPatch(
            imageStream,
            layout,
            patchesByWadOffset,
            wadOffset,
            BitConverter.GetBytes(oldWord),
            BitConverter.GetBytes(newVisualWord),
            "texture-id-word3",
            runtimeKey,
            $"Set terrain texture id to {textureIdEdited} while preserving the target face's native mapping/control bits.");

        byte[] oldColorIndexes = ram.AsSpan(faceOffset + 4, 4).ToArray();
        byte[] newColorIndexes = visualEdit.Corners
            .Select(corner => checked((byte)colorSlotByPair[corner]))
            .ToArray();
        AddPatch(
            imageStream,
            layout,
            patchesByWadOffset,
            sourceSectorWadOffset + ((faceOffset + 4) - sectorOffset),
            oldColorIndexes,
            newColorIndexes,
            "texture-visual-color-indices",
            runtimeKey,
            $"Bind the four face corners to {uniqueCornerPairs.Length} private high-detail native tint slot(s): {string.Join(", ", colorSlotByPair.Values)}.");

        foreach ((TerrainTextureVisualCorner pair, int privateColorIndex) in colorSlotByPair)
        {
            NativeTerrainHpColorOffsets colorOffsets =
                NativeTerrainHpColorLayout.GetColorOffsets(
                    hpColorStart,
                    sector.NumHpColours,
                    privateColorIndex);
            AddNativeTextureVisualColorPatch(
                imageStream,
                layout,
                ram,
                sourceSectorWadOffset,
                sectorOffset,
                colorOffsets.Table2Offset,
                pair.NearColor,
                "texture-visual-near-color",
                runtimeKey,
                $"Set private near tint slot {privateColorIndex} to #{pair.NearColor.R:X2}{pair.NearColor.G:X2}{pair.NearColor.B:X2}.",
                patchesByWadOffset);
            AddNativeTextureVisualColorPatch(
                imageStream,
                layout,
                ram,
                sourceSectorWadOffset,
                sectorOffset,
                colorOffsets.Table1Offset,
                pair.FarColor,
                "texture-visual-far-color",
                runtimeKey,
                $"Set private fade tint slot {privateColorIndex} to #{pair.FarColor.R:X2}{pair.FarColor.G:X2}{pair.FarColor.B:X2}.",
                patchesByWadOffset);
        }
    }

    private static bool TryReadNativeTextureVisualEdit(
        JsonElement edit,
        int fallbackTextureId,
        out TerrainTextureVisualEdit visual,
        out string error)
    {
        visual = null!;
        string sourceLevelKey = JsonValue.GetString(edit, "nativeTextureVisualSourceLevelKey");
        string sourceRuntimeKey = JsonValue.GetString(edit, "nativeTextureVisualSourceRuntimeKey");
        int sourceTextureId = JsonValue.GetInt32(edit, "nativeTextureVisualSourceTextureId", fallbackTextureId);
        int sourceSectorOffset = JsonValue.GetInt32(edit, "nativeTextureVisualSourceSectorOffset", -1);
        int sourceFaceOffset = JsonValue.GetInt32(edit, "nativeTextureVisualSourceFaceOffset", -1);
        if (string.IsNullOrWhiteSpace(sourceLevelKey) || string.IsNullOrWhiteSpace(sourceRuntimeKey) ||
            sourceTextureId < 0 || sourceSectorOffset < 0 || sourceFaceOffset < 0)
        {
            error = "native texture visuals are missing source-bound level, face, texture, or offset provenance; reopen the catalog and reselect the look.";
            return false;
        }

        if (!TryReadTextureVisualColors(
                edit,
                "nativeTextureVisualNearColors",
                "nativeTextureVisualNearColor",
                out ColorRgba[] nearColors) ||
            !TryReadTextureVisualColors(
                edit,
                "nativeTextureVisualFarColors",
                "nativeTextureVisualFarColor",
                out ColorRgba[] farColors))
        {
            error = "native texture visuals are missing four valid source-bound near/fade tint colors.";
            return false;
        }

        TerrainTextureVisualCorner[] corners = Enumerable.Range(0, 4)
            .Select(index => new TerrainTextureVisualCorner(nearColors[index], farColors[index]))
            .ToArray();
        visual = new TerrainTextureVisualEdit(
            sourceTextureId,
            sourceLevelKey,
            sourceRuntimeKey,
            sourceSectorOffset,
            sourceFaceOffset,
            corners[0],
            corners[1],
            corners[2],
            corners[3],
            JsonValue.GetString(edit, "nativeTextureVisualLabel", "native terrain texture visual"));
        error = "";
        return true;
    }

    private static bool TryReadTextureVisualColors(
        JsonElement edit,
        string arrayName,
        string legacyUniformName,
        out ColorRgba[] colors)
    {
        colors = [];
        if (edit.TryGetProperty(arrayName, out JsonElement values) && values.ValueKind == JsonValueKind.Array)
        {
            List<ColorRgba> parsed = new();
            foreach (JsonElement value in values.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.String || !ColorRgba.TryParseHex(value.GetString(), out ColorRgba color))
                    return false;
                parsed.Add(color);
            }

            if (parsed.Count != 4)
                return false;
            colors = parsed.ToArray();
            return true;
        }

        if (!ColorRgba.TryParseHex(JsonValue.GetString(edit, legacyUniformName), out ColorRgba uniform))
            return false;
        colors = [uniform, uniform, uniform, uniform];
        return true;
    }

    private static int GetHpColorDataStartOffset(SceneSectorHeader sector)
    {
        int dataStart = sector.Offset + 28;
        int hpVertexStartWords = sector.NumLpVertices + sector.NumLpColours + (sector.NumLpFaces * 2);
        return dataStart + ((hpVertexStartWords + sector.NumHpVertices) * 4);
    }

    private static void AddNativeTextureVisualColorPatch(
        FileStream imageStream,
        DiscLayout layout,
        byte[] ram,
        long sourceSectorWadOffset,
        int sectorOffset,
        int colorOffset,
        ColorRgba color,
        string kind,
        string runtimeKey,
        string description,
        Dictionary<long, TerrainPatch> patchesByWadOffset)
    {
        byte[] before = ram.AsSpan(colorOffset, 4).ToArray();
        byte[] after = before.ToArray();
        after[0] = color.R;
        after[1] = color.G;
        after[2] = color.B;
        AddPatch(
            imageStream,
            layout,
            patchesByWadOffset,
            sourceSectorWadOffset + (colorOffset - sectorOffset),
            before,
            after,
            kind,
            runtimeKey,
            description);
    }

    private static void AddNativeSurfaceBehaviorPatches(
        FileStream imageStream,
        DiscLayout layout,
        LevelDefinition level,
        NativeTerrainSurfaceSourceData? source,
        IReadOnlyDictionary<string, NativeCollisionSurfaceTriangle[]>? nativeTrianglesByKey,
        IReadOnlyDictionary<int, NativeTerrainTextureRelocationEdit> nativeTextureRelocationsByTarget,
        Dictionary<int, NativeTerrainSurfaceSourceData> donorSurfaceSourcesByWadEntry,
        JsonElement edit,
        List<NativeTerrainSurfaceBehaviorAssignment> assignments,
        List<string> skippedEdits)
    {
        if (!JsonValue.GetBoolean(edit, "nativeSurfaceBehaviorEdit"))
            return;

        string runtimeKey = JsonValue.GetString(edit, "runtimeKey");
        if (source == null || nativeTrianglesByKey == null)
        {
            AddAtomicTerrainSwapBlock(
                skippedEdits,
                $"{runtimeKey}: native terrain behavior was requested, but this level's source surface table is unavailable.");
            return;
        }

        int surfaceType = JsonValue.GetInt32(edit, "nativeSurfaceType", int.MinValue);
        if (surfaceType == int.MinValue)
        {
            AddAtomicTerrainSwapBlock(
                skippedEdits,
                $"{runtimeKey}: native terrain behavior edit is missing its surface type; reapply the texture swap.");
            return;
        }

        NativeTerrainSurfaceSignature signature = new(
            surfaceType,
            JsonValue.GetInt32(edit, "nativeSurfaceParam1"),
            JsonValue.GetInt32(edit, "nativeSurfaceParam2"));
        string sourceLevelKey = JsonValue.GetString(edit, "nativeSurfaceSourceLevelKey");
        bool crossLevel = !string.IsNullOrWhiteSpace(sourceLevelKey) &&
            !string.Equals(LevelCatalog.NormalizeKey(sourceLevelKey), LevelCatalog.NormalizeKey(level.Key), StringComparison.OrdinalIgnoreCase);
        if (signature.SurfaceType is 2 or 3 or 6 || (crossLevel && !signature.IsCrossLevelPortable))
        {
            AddAtomicTerrainSwapBlock(
                skippedEdits,
                $"{runtimeKey}: {signature.Label} is level-linked and cannot be transplanted safely.");
            return;
        }

        NativeTerrainSurfaceDescriptorImport? donorImport = null;
        if (!signature.IsOrdinary)
        {
            PortalSpecialSurfaceRecord? descriptor = source.SpecialSurfaces.FirstOrDefault(surface =>
                surface.Type == signature.SurfaceType &&
                surface.Param1 == signature.Param1 &&
                surface.Param2 == signature.Param2);
            if (descriptor == null)
            {
                int originalTextureId = JsonValue.GetInt32(
                    edit,
                    "textureIdOriginal",
                    JsonValue.GetInt32(edit, "textureId", -1));
                int editedTextureId = JsonValue.GetInt32(edit, "textureIdEdited", originalTextureId);
                int targetTextureId = nativeTextureRelocationsByTarget.ContainsKey(editedTextureId)
                    ? editedTextureId
                    : originalTextureId;
                if (!crossLevel ||
                    targetTextureId < 0 ||
                    !nativeTextureRelocationsByTarget.TryGetValue(targetTextureId, out NativeTerrainTextureRelocationEdit? relocation) ||
                    !string.Equals(
                        LevelCatalog.NormalizeKey(relocation.DonorLevelKey),
                        LevelCatalog.NormalizeKey(sourceLevelKey),
                        StringComparison.OrdinalIgnoreCase))
                {
                    AddAtomicTerrainSwapBlock(
                        skippedEdits,
                        $"{runtimeKey}: {level.DisplayName} has no native {signature.Label} descriptor, and the shared texture relocation does not identify its exact donor level; no behavior bytes were changed.");
                    return;
                }

                try
                {
                    if (!donorSurfaceSourcesByWadEntry.TryGetValue(
                            relocation.DonorWadEntry,
                            out NativeTerrainSurfaceSourceData? donorSource))
                    {
                        donorSource = PortalSourceDataLocator.LocateTerrainSurfaces(
                            imageStream,
                            layout,
                            new LevelDefinition
                            {
                                Key = relocation.DonorLevelKey,
                                DisplayName = relocation.DonorLevelName,
                                SourceWadEntry = relocation.DonorWadEntry
                            });
                        donorSurfaceSourcesByWadEntry[relocation.DonorWadEntry] = donorSource;
                    }

                    PortalSpecialSurfaceRecord? donorDescriptor = donorSource.SpecialSurfaces.FirstOrDefault(surface =>
                        surface.Type == signature.SurfaceType &&
                        surface.Param1 == signature.Param1 &&
                        surface.Param2 == signature.Param2);
                    if (donorDescriptor == null)
                    {
                        AddAtomicTerrainSwapBlock(
                            skippedEdits,
                            $"{runtimeKey}: {relocation.DonorLevelName} no longer contains the staged {signature.Label} descriptor; no behavior bytes were changed.");
                        return;
                    }

                    byte[] rawRecord = donorDescriptor.RawBytes.ToArray();
                    donorImport = new NativeTerrainSurfaceDescriptorImport(
                        signature,
                        rawRecord,
                        donorSource.LevelKey,
                        donorDescriptor.Index,
                        Convert.ToHexString(SHA256.HashData(rawRecord)));
                }
                catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or OverflowException)
                {
                    AddAtomicTerrainSwapBlock(
                        skippedEdits,
                        $"{runtimeKey}: the donor {signature.Label} descriptor could not be read atomically: {ex.Message}");
                    return;
                }
            }
        }

        IReadOnlyList<Vector2f> originalPoints = ReadVector2Array(edit, "originalPoints");
        IReadOnlyList<float> originalZ = ReadFloatArray(edit, "originalZ");
        int pointCount = Math.Min(originalPoints.Count, originalZ.Count);
        if (pointCount < 3)
        {
            AddAtomicTerrainSwapBlock(
                skippedEdits,
                $"{runtimeKey}: native terrain behavior edit is missing the selected face's original points.");
            return;
        }

        List<NativeCollisionSurfaceTriangle> targetTriangles = [];
        for (int i = 1; i < pointCount - 1; i++)
        {
            string triangleKey = string.Join("|", new[]
            {
                NativeTerrainSurfaceCatalogBuilder.PointKey(originalPoints[0].X, originalPoints[0].Y, originalZ[0]),
                NativeTerrainSurfaceCatalogBuilder.PointKey(originalPoints[i].X, originalPoints[i].Y, originalZ[i]),
                NativeTerrainSurfaceCatalogBuilder.PointKey(originalPoints[i + 1].X, originalPoints[i + 1].Y, originalZ[i + 1])
            }.OrderBy(value => value, StringComparer.Ordinal));
            if (!nativeTrianglesByKey.TryGetValue(triangleKey, out NativeCollisionSurfaceTriangle[]? matches) || matches.Length != 1)
            {
                AddAtomicTerrainSwapBlock(
                    skippedEdits,
                    $"{runtimeKey}: native behavior transfer blocked because visible triangle {i}/{pointCount - 2} does not have exactly one collision match.");
                return;
            }

            targetTriangles.Add(matches[0]);
        }

        if (targetTriangles.Select(triangle => triangle.TriangleIndex).Distinct().Count() != targetTriangles.Count)
        {
            AddAtomicTerrainSwapBlock(
                skippedEdits,
                $"{runtimeKey}: native behavior transfer blocked because collision matches are not one-to-one.");
            return;
        }

        foreach (NativeCollisionSurfaceTriangle triangle in targetTriangles)
        {
            if (signature.IsOrdinary && triangle.FlagWadOffset < 0)
                continue; // Beyond-count triangles already have exact implicit ordinary flag 0xFF.

            assignments.Add(new NativeTerrainSurfaceBehaviorAssignment(
                triangle.TriangleIndex,
                signature,
                donorImport,
                runtimeKey,
                signature.Label));
        }
    }

    private static void AddNativeSurfaceLayoutPatch(
        FileStream imageStream,
        DiscLayout layout,
        NativeTerrainSurfaceSourceData? source,
        IReadOnlyList<NativeTerrainSurfaceBehaviorAssignment> assignments,
        Dictionary<long, TerrainPatch> patchesByWadOffset,
        List<string> skippedEdits)
    {
        if (source == null || assignments.Count == 0)
            return;

        byte[] levelData = ReadWadBytes(
            imageStream,
            layout,
            source.LevelDataWadOffset,
            source.LevelDataByteLength);
        NativeTerrainSurfaceExistingPatch[] existingPatches = patchesByWadOffset.Values
            .Select(patch => new NativeTerrainSurfaceExistingPatch(
                ParseRequiredLong(patch.WadRelativeOffset, "patch.wadRelativeOffset"),
                HexToBytes(patch.BeforeHexPreview),
                HexToBytes(patch.AfterHexPreview),
                patch.Kind))
            .ToArray();
        if (!NativeTerrainSurfaceLayoutComposer.TryBuild(
                levelData,
                source,
                assignments,
                existingPatches,
                out NativeTerrainSurfaceLayoutPlan? surfacePlan,
                out string reason) ||
            surfacePlan == null)
        {
            foreach (string runtimeKey in assignments
                         .Select(assignment => assignment.RuntimeKey)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                AddAtomicTerrainSwapBlock(
                    skippedEdits,
                    $"{runtimeKey}: native terrain behavior was not applied atomically: {reason}");
            }
            return;
        }

        if (!surfacePlan.HasChanges)
            return;

        foreach (long consumedOffset in surfacePlan.ConsumedPatchWadOffsets)
            patchesByWadOffset.Remove(consumedOffset);

        string runtimeSummary = string.Join(",", assignments
            .Select(assignment => assignment.RuntimeKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Take(4));
        if (assignments.Select(assignment => assignment.RuntimeKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 4)
            runtimeSummary += ",more";
        AddPatch(
            imageStream,
            layout,
            patchesByWadOffset,
            surfacePlan.WadOffset,
            surfacePlan.Before,
            surfacePlan.After,
            surfacePlan.DescriptorGrowthBytes > 0
                ? "native-surface-layout-import"
                : surfacePlan.FlagGrowthBytes > 0 || surfacePlan.TriangleRemaps.Count > 0
                    ? "collision-surface-flag-promotion"
                : "collision-surface-flag",
            string.IsNullOrWhiteSpace(runtimeSummary) ? "native-surface-batch" : runtimeSummary,
            surfacePlan.DescriptorGrowthBytes > 0
                ? $"Atomically import {surfacePlan.NewSurfaceCount - surfacePlan.OldSurfaceCount} portable native surface descriptor(s), assign {surfacePlan.AssignedTriangleIndexes.Count} collision surface flag(s), move collision data by {surfacePlan.DescriptorGrowthBytes} byte(s), grow flags by {surfacePlan.FlagGrowthBytes} byte(s), and preserve {surfacePlan.ZeroTailBytesAfter} verified zero-tail byte(s)."
                : surfacePlan.FlagGrowthBytes > 0 || surfacePlan.TriangleRemaps.Count > 0
                    ? $"Atomically assign {surfacePlan.AssignedTriangleIndexes.Count} native collision surface flag(s), promote flag count {surfacePlan.OldFlagCount}->{surfacePlan.NewFlagCount}, remap {surfacePlan.TriangleRemaps.Count} beyond-count triangle(s), and shift the parsed level suffix by {surfacePlan.FlagGrowthBytes} byte(s) into verified zero padding ({surfacePlan.ZeroTailBytesAfter} byte(s) remain)."
                    : $"Atomically assign {surfacePlan.AssignedTriangleIndexes.Count} native collision surface flag(s) inside the existing flag table.");
    }

    private static IReadOnlyList<NativeTerrainTextureRelocationPatchSummary> AddNativeTerrainTextureRelocationPatches(
        string sourceImagePath,
        string sourceCuePath,
        LevelDefinition targetLevel,
        IReadOnlyList<NativeTerrainTextureRelocationEdit> edits,
        FileStream imageStream,
        DiscLayout layout,
        Dictionary<long, TerrainPatch> patchesByWadOffset,
        List<string> skippedEdits)
    {
        if (edits.Count == 0)
            return Array.Empty<NativeTerrainTextureRelocationPatchSummary>();

        IReadOnlyList<NativeTerrainTextureRelocationPatchSummary> FailAll(string reason)
        {
            foreach (NativeTerrainTextureRelocationEdit edit in edits)
            {
                AddAtomicTerrainSwapBlock(
                    skippedEdits,
                    $"shared target texture {edit.TargetTextureId} <- {edit.DonorLevelName} texture {edit.DonorTextureId}: {reason}; no native texture-art patch was applied.");
            }
            return Array.Empty<NativeTerrainTextureRelocationPatchSummary>();
        }

        if (edits.Any(edit => edit.UsesAppendedPrivateRecord))
        {
            return FailAll(
                "this saved edit uses an appended-private native record and must be compiled by the exclusive private-record batch writer; the legacy in-place/relocation writer is forbidden from emitting a partial record or a face id that does not exist in the retail table");
        }

        NativeTerrainTextureRuntimeControlAudit runtimeAudit;
        try
        {
            runtimeAudit = NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, targetLevel);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or OverflowException)
        {
            return FailAll($"the source-bound runtime texture-control audit failed ({ex.Message})");
        }
        if (!runtimeAudit.Complete)
        {
            return FailAll(
                $"the runtime texture-control audit is incomplete ({string.Join(" ", runtimeAudit.SafetyBlockers)})");
        }
        int[] controlledTargets = edits
            .Select(edit => edit.TargetTextureId)
            .Where(textureId => !runtimeAudit.IsRuntimePersistentTarget(textureId))
            .Distinct()
            .Order()
            .ToArray();
        if (controlledTargets.Length > 0)
        {
            return FailAll(
                $"target texture record(s) {string.Join(", ", controlledTargets)} are rewritten by native animation or scrolling controls");
        }

        List<PendingNativeTerrainTexturePatch> pending = [];
        List<NativeTerrainTextureRelocationPatchSummary> summaries = [];
        List<NativeTerrainTextureRelocationEdit> relocationFallbacks = [];
        List<string> inPlaceFallbackNotes = [];
        foreach (NativeTerrainTextureRelocationEdit edit in edits.OrderBy(edit => edit.TargetTextureId))
        {
            NativeTerrainTextureInPlaceTransplantRequest request = new(
                edit.TargetTextureId,
                edit.DonorWadEntry,
                edit.DonorTextureId);
            try
            {
                NativeTerrainTextureInPlaceTransplantSourceProof proof =
                    NativeTerrainTextureInPlaceTransplantBuilder.InspectSourceProof(
                        sourceImagePath,
                        targetLevel,
                        request);
                if (NativeTerrainTextureInPlaceTransplantBuilder.TryBuild(
                        sourceImagePath,
                        targetLevel,
                        request,
                        proof,
                        out NativeTerrainTextureInPlaceTransplantPlan? plan,
                        out string inPlaceFailure) &&
                    plan != null &&
                    plan.SourceBindingVerified &&
                    plan.RuntimeControlClearanceVerified &&
                    plan.CompleteOwnershipClosureVerified &&
                    plan.DecodedAndExternalExclusivityVerified &&
                    plan.ExactDonorIndexedPixelsVerified &&
                    plan.ExactDonorPalettesVerified &&
                    plan.LowDetailAliasPreserved &&
                    plan.TargetDescriptorTableUnchanged &&
                    plan.TargetTextureIdPreserved &&
                    plan.LogicalReadbackVerified &&
                    plan.PhysicalAliasConflictCount == 0 &&
                    plan.OutOfOwnershipWriteCount == 0)
                {
                    foreach (NativeTerrainTextureInPlaceTransplantPatch patch in plan.Patches)
                    {
                        pending.Add(new PendingNativeTerrainTexturePatch(
                            patch.WadOffset,
                            patch.Before,
                            patch.After,
                            "native-terrain-texture-in-place",
                            $"texture-{edit.TargetTextureId}",
                            patch.Description));
                    }
                    summaries.Add(new NativeTerrainTextureRelocationPatchSummary(
                        Strategy: "target-owned-in-place",
                        TargetTextureIds: [edit.TargetTextureId],
                        DonorTextures: [$"{edit.DonorLevelName} / texture {edit.DonorTextureId} / WAD {edit.DonorWadEntry}"],
                        CompleteDescriptorCount: plan.CompleteDescriptorCount,
                        TargetOwnedByteCount: plan.TargetOwnedByteCount,
                        PatchCount: plan.Patches.Count,
                        PatchedByteCount: plan.Patches.Sum(patch => patch.ByteLength),
                        RuntimeControlVerified: plan.RuntimeControlClearanceVerified,
                        OwnershipVerified: plan.CompleteOwnershipClosureVerified && plan.DecodedAndExternalExclusivityVerified,
                        ExactIndexedPixelsVerified: plan.ExactDonorIndexedPixelsVerified,
                        ExactPalettesVerified: plan.ExactDonorPalettesVerified,
                        LogicalReadbackVerified: plan.LogicalReadbackVerified,
                        TargetDescriptorMaterialPolicyVerified: true,
                        Notes: plan.Notes.Prepend(RelocationApplyModeNote(edit)).ToArray()));
                    continue;
                }

                relocationFallbacks.Add(edit);
                inPlaceFallbackNotes.Add(
                    $"texture {edit.TargetTextureId}: {(string.IsNullOrWhiteSpace(inPlaceFailure) ? "in-place proof did not satisfy every required invariant" : inPlaceFailure)}");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
            {
                relocationFallbacks.Add(edit);
                inPlaceFallbackNotes.Add($"texture {edit.TargetTextureId}: {ex.Message}");
            }
        }

        if (relocationFallbacks.Count > 0)
        {
            int[] requestedFallbackTargetIds = relocationFallbacks
                .Select(edit => edit.TargetTextureId)
                .Distinct()
                .Order()
                .ToArray();
            Dictionary<int, NativeTerrainTextureRelocationEdit> explicitEdits = edits
                .ToDictionary(edit => edit.TargetTextureId);
            bool TryBuildRelocationAttempt(
                IReadOnlyList<int> promotedTargets,
                out int[] attemptTargetIds,
                out NativeTexturePageRelocationOwnershipProofResult? attemptProof,
                out NativeTerrainTextureRelocationExportPlan? attemptPlan,
                out string attemptFailure)
            {
                attemptTargetIds = [];
                attemptProof = null;
                attemptPlan = null;
                attemptFailure = "";
                try
                {
                    attemptTargetIds = NativeTexturePageOwnershipScanner
                        .FindTerrainTextureStorageOverlapClosure(
                            sourceImagePath,
                            targetLevel,
                            requestedFallbackTargetIds.Concat(promotedTargets).Distinct().Order().ToArray())
                        .ToArray();
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
                {
                    attemptFailure = $"terrain storage-overlap closure failed ({ex.Message})";
                    return false;
                }

                int[] controlledRelocationTargets = attemptTargetIds
                    .Where(textureId => !runtimeAudit.IsRuntimePersistentTarget(textureId))
                    .ToArray();
                if (controlledRelocationTargets.Length > 0)
                {
                    attemptFailure =
                        $"record(s) {string.Join(", ", controlledRelocationTargets)} are rewritten by native animation or scrolling controls";
                    return false;
                }

                if (!NativeTexturePageOwnershipScanner.TryBuildRelocationOwnershipProof(
                        sourceImagePath,
                        targetLevel,
                        attemptTargetIds,
                        out attemptProof,
                        out string ownershipFailure) ||
                    attemptProof == null)
                {
                    attemptFailure = $"relocation ownership proof failed ({ownershipFailure})";
                    return false;
                }

                NativeTerrainTextureRelocationImport[] attemptImports = attemptTargetIds
                    .Select(textureId => explicitEdits.TryGetValue(textureId, out NativeTerrainTextureRelocationEdit? edit)
                        ? new NativeTerrainTextureRelocationImport(
                            edit.TargetTextureId,
                            edit.DonorWadEntry,
                            edit.DonorTextureId,
                            edit.DescriptorTier,
                            edit.PreservesTargetNativeSurface)
                        : new NativeTerrainTextureRelocationImport(
                            textureId,
                            targetLevel.SourceWadEntry,
                            textureId,
                            "both",
                            PreserveTargetDescriptorMaterial: true))
                    .ToArray();
                try
                {
                    attemptPlan = NativeTerrainTextureRelocationComposer.BuildPlan(
                        new NativeTerrainTextureRelocationExportRequest(
                            SourceImagePath: sourceImagePath,
                            SourceCuePath: sourceCuePath,
                            OutputPrefix: Path.Combine(
                                Path.GetDirectoryName(sourceImagePath) ?? "",
                                $".spyro-editor-{targetLevel.Key}-native-texture-proof"),
                            TargetLevel: targetLevel,
                            Imports: attemptImports,
                            OwnershipProof: attemptProof.Proof,
                            WriteImage: false));
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
                {
                    attemptFailure = $"byte-private relocation failed ({ex.Message})";
                    return false;
                }

                NativeTerrainTextureRelocationPlan proved = attemptPlan.Relocation;
                if (!attemptPlan.RuntimeTargetsPersistent ||
                    !proved.ExactDonorIndexedPixelsVerified ||
                    !proved.ExactDonorPalettesVerified ||
                    !proved.LowDetailAliasPreserved ||
                    !proved.LogicalReadbackVerified ||
                    !proved.ProtectedStorageVerified ||
                    !proved.TargetDescriptorMaterialPolicyVerified)
                {
                    attemptFailure = "relocation omitted a required runtime, ownership, complete-record, or exact-readback proof";
                    attemptPlan = null;
                    return false;
                }
                return true;
            }

            int[] availableInPlaceTargets = edits
                .Select(edit => edit.TargetTextureId)
                .Where(textureId => !requestedFallbackTargetIds.Contains(textureId))
                .Distinct()
                .Order()
                .ToArray();
            NativeTerrainTexturePromotionAttemptSet promotionSearch =
                NativeTerrainTextureRelocationAllocator.BuildBoundedPromotionAttempts(
                    availableInPlaceTargets);

            int[] fallbackTargetIds = [];
            NativeTexturePageRelocationOwnershipProofResult? proofResult = null;
            NativeTerrainTextureRelocationExportPlan? relocationExportPlan = null;
            List<string> relocationAttemptFailures = [];
            HashSet<string> attemptedClosures = new(StringComparer.Ordinal);
            foreach (int[] promotionAttempt in promotionSearch.Attempts)
            {
                if (TryBuildRelocationAttempt(
                        promotionAttempt,
                        out int[] attemptTargetIds,
                        out NativeTexturePageRelocationOwnershipProofResult? attemptProof,
                        out NativeTerrainTextureRelocationExportPlan? attemptPlan,
                        out string attemptFailure))
                {
                    fallbackTargetIds = attemptTargetIds;
                    proofResult = attemptProof;
                    relocationExportPlan = attemptPlan;
                    break;
                }

                string closureKey = string.Join(",", attemptTargetIds);
                if (attemptedClosures.Add(closureKey))
                    relocationAttemptFailures.Add($"[{closureKey}] {attemptFailure}");
            }
            if (proofResult == null || relocationExportPlan == null)
            {
                string inPlace = string.Join(" | ", inPlaceFallbackNotes.Take(3));
                string relocationFailures = string.Join(" | ", relocationAttemptFailures.Take(3));
                string pairGuardNote = promotionSearch.PairAttemptsTruncated
                    ? $" Pair promotion search tried the first {promotionSearch.PairAttemptCount:N0} of {promotionSearch.TotalPairCount:N0} lexicographic pairs, then the complete staged set."
                    : "";
                return FailAll(
                    $"target-owned in-place storage was unavailable ({inPlace}), and bounded byte-private relocation attempts failed ({relocationFailures}).{pairGuardNote}");
            }

            int[] promotedInPlaceTargets = fallbackTargetIds
                .Where(textureId =>
                    !requestedFallbackTargetIds.Contains(textureId) &&
                    explicitEdits.ContainsKey(textureId))
                .ToArray();
            if (promotedInPlaceTargets.Length > 0)
            {
                pending.RemoveAll(patch => promotedInPlaceTargets.Any(textureId =>
                    string.Equals(patch.RuntimeKey, $"texture-{textureId}", StringComparison.Ordinal)));
                summaries.RemoveAll(summary => summary.TargetTextureIds.Any(promotedInPlaceTargets.Contains));
                inPlaceFallbackNotes.Add(
                    $"promoted explicit in-place texture edit(s) {string.Join(", ", promotedInPlaceTargets)} into the atomic relocation batch to release enough proven private storage");
            }

            NativeTerrainTextureRelocationPlan relocation = relocationExportPlan.Relocation;
            foreach (NativeTerrainTextureRelocationPatch patch in relocation.Patches)
            {
                pending.Add(new PendingNativeTerrainTexturePatch(
                    patch.WadOffset,
                    patch.Before,
                    patch.After,
                    $"native-terrain-texture-{patch.Kind}",
                    $"texture-batch-{string.Join("-", fallbackTargetIds)}",
                    patch.Description));
            }
            NativeTerrainTextureRelocationEdit[] relocatedExplicitEdits = fallbackTargetIds
                .Where(explicitEdits.ContainsKey)
                .Select(textureId => explicitEdits[textureId])
                .ToArray();
            int[] preservedOverlapTargets = fallbackTargetIds
                .Where(textureId => !explicitEdits.ContainsKey(textureId))
                .ToArray();
            summaries.Add(new NativeTerrainTextureRelocationPatchSummary(
                Strategy: "byte-private-relocation",
                TargetTextureIds: fallbackTargetIds,
                DonorTextures: relocatedExplicitEdits
                    .Select(edit => $"{edit.DonorLevelName} / texture {edit.DonorTextureId} / WAD {edit.DonorWadEntry}")
                    .ToArray(),
                CompleteDescriptorCount: relocation.RewrittenDescriptorCount,
                TargetOwnedByteCount: proofResult.TargetIsolations.Sum(isolation => isolation.TargetOwnedByteCount),
                PatchCount: relocation.Patches.Count,
                PatchedByteCount: relocation.Patches.Sum(patch => patch.ByteLength),
                RuntimeControlVerified: relocationExportPlan.RuntimeTargetsPersistent,
                OwnershipVerified: relocation.ProtectedStorageVerified,
                ExactIndexedPixelsVerified: relocation.ExactDonorIndexedPixelsVerified,
                ExactPalettesVerified: relocation.ExactDonorPalettesVerified,
                LogicalReadbackVerified: relocation.LogicalReadbackVerified,
                TargetDescriptorMaterialPolicyVerified: relocation.TargetDescriptorMaterialPolicyVerified,
                Notes: relocation.Notes
                    .Prepend(preservedOverlapTargets.Length == 0
                        ? "No unedited overlap companion records were required."
                        : $"Atomically preserved unedited overlapping destination record(s) {string.Join(", ", preservedOverlapTargets)}.")
                    .Concat(relocatedExplicitEdits.Select(RelocationApplyModeNote))
                    .Concat(inPlaceFallbackNotes)
                    .ToArray()));
        }

        List<PendingNativeTerrainTexturePatch> accepted = [];
        foreach (PendingNativeTerrainTexturePatch candidate in pending
                     .OrderBy(patch => patch.WadOffset)
                     .ThenBy(patch => patch.Before.Length))
        {
            if (candidate.Before.Length == 0 ||
                candidate.Before.Length != candidate.After.Length ||
                candidate.WadOffset < 0)
            {
                return FailAll("a proof builder emitted an invalid empty, unequal-length, or negative-offset patch");
            }

            PendingNativeTerrainTexturePatch? exactDuplicate = accepted.FirstOrDefault(existing =>
                existing.WadOffset == candidate.WadOffset &&
                existing.Before.AsSpan().SequenceEqual(candidate.Before) &&
                existing.After.AsSpan().SequenceEqual(candidate.After));
            if (exactDuplicate != null)
                continue;

            PendingNativeTerrainTexturePatch? overlap = accepted.FirstOrDefault(existing =>
                NativeTexturePatchRangesOverlap(
                    existing.WadOffset,
                    existing.Before.Length,
                    candidate.WadOffset,
                    candidate.Before.Length));
            if (overlap != null)
            {
                return FailAll(
                    $"proof builders emitted overlapping {overlap.Kind} and {candidate.Kind} ranges at WAD 0x{candidate.WadOffset:X}");
            }

            TerrainPatch? existingTerrainPatch = patchesByWadOffset.Values.FirstOrDefault(existing =>
                NativeTexturePatchRangesOverlap(
                    ParseRequiredLong(existing.WadRelativeOffset, "patch.wadRelativeOffset"),
                    existing.ByteLength,
                    candidate.WadOffset,
                    candidate.Before.Length));
            if (existingTerrainPatch != null)
            {
                return FailAll(
                    $"native texture art overlaps existing {existingTerrainPatch.Kind} patch {existingTerrainPatch.Label}");
            }

            byte[] sourceBefore = ReadWadBytes(
                imageStream,
                layout,
                candidate.WadOffset,
                candidate.Before.Length);
            if (!sourceBefore.SequenceEqual(candidate.Before))
            {
                return FailAll(
                    $"source-bound before bytes changed for {candidate.Kind} at WAD 0x{candidate.WadOffset:X}");
            }
            accepted.Add(candidate);
        }

        foreach (PendingNativeTerrainTexturePatch patch in accepted)
        {
            AddPatch(
                imageStream,
                layout,
                patchesByWadOffset,
                patch.WadOffset,
                patch.Before,
                patch.After,
                patch.Kind,
                patch.RuntimeKey,
                patch.Description);
        }
        return summaries;
    }

    private static NativeTerrainTextureLowDetailCompanionSummary AddNativeTerrainTextureLowDetailCompanionPatches(
        LevelDefinition targetLevel,
        IReadOnlyList<NativeTerrainTextureRelocationEdit> edits,
        IReadOnlyList<NativeTerrainTextureRelocationPatchSummary> relocationSummaries,
        IReadOnlyDictionary<string, SourceSectorLocation> sourceSectorHits,
        FileStream imageStream,
        DiscLayout layout,
        Dictionary<long, TerrainPatch> patchesByWadOffset)
    {
        if (edits.Count == 0)
        {
            return NativeTerrainTextureLowDetailCompanionSummary.NotNeeded(
                "No native cross-level texture replacements require an LP companion.");
        }

        if (relocationSummaries.Count == 0)
        {
            return NativeTerrainTextureLowDetailCompanionSummary.NotNeeded(
                "Native texture-art replacement was not proved, so no LP companion bytes were emitted.");
        }

        HashSet<int> provedTargets = relocationSummaries
            .SelectMany(summary => summary.TargetTextureIds)
            .ToHashSet();
        NativeTerrainTextureRelocationEdit[] provedEdits = edits
            .Where(edit => provedTargets.Contains(edit.TargetTextureId))
            .OrderBy(edit => edit.TargetTextureId)
            .ToArray();
        if (provedEdits.Length != edits.Count)
        {
            throw new InvalidOperationException(
                "Native texture relocation summaries do not cover every requested target; refusing a partial far-LOD companion.");
        }

        if (sourceSectorHits.Count == 0)
        {
            return NativeTerrainTextureLowDetailCompanionSummary.NotNeeded(
                "Native texture art was replaced, but no exact source-sector map was supplied; the LP companion was not attempted in this art-only plan.");
        }

        Dictionary<int, NativeTextureAppearanceAsset> appearanceAssets = [];
        Dictionary<int, NativeTerrainTextureLowDetailReplacement> replacements = [];
        foreach (NativeTerrainTextureRelocationEdit edit in provedEdits)
        {
            NativeTerrainTextureRepresentativeColor targetAppearance =
                ReadNativeTextureRepresentativeColor(
                    imageStream,
                    layout,
                    targetLevel.SourceWadEntry,
                    edit.TargetTextureId,
                    appearanceAssets);
            NativeTerrainTextureRepresentativeColor donorAppearance =
                ReadNativeTextureRepresentativeColor(
                    imageStream,
                    layout,
                    edit.DonorWadEntry,
                    edit.DonorTextureId,
                    appearanceAssets);
            replacements.Add(
                edit.TargetTextureId,
                new NativeTerrainTextureLowDetailReplacement(
                    edit.TargetTextureId,
                    targetAppearance,
                    donorAppearance));
        }

        SourceSectorLocation[] sectors = sourceSectorHits.Values
            .GroupBy(location => location.WadOffset)
            .Select(group => group
                .OrderByDescending(location => location.SizeBytes)
                .First())
            .OrderBy(location => location.WadOffset)
            .ToArray();
        int affectedSectorCount = 0;
        int patchCount = 0;
        int changedColorCount = 0;
        int affectedHighDetailFaceCount = 0;
        int composedPatchCount = 0;
        foreach (SourceSectorLocation location in sectors)
        {
            byte[] header = ReadWadBytes(
                imageStream,
                layout,
                location.WadOffset,
                28);
            int sectorByteLength = SceneSectorByteLength(header, 0);
            if (location.SizeBytes > 0 && location.SizeBytes != sectorByteLength)
            {
                throw new InvalidOperationException(
                    $"Source-sector map for {location.RuntimeKey} says {location.SizeBytes:N0} bytes at WAD 0x{location.WadOffset:X}, but the source header says {sectorByteLength:N0}.");
            }

            TerrainPatch[] allExisting = patchesByWadOffset.Values.ToArray();
            byte[] stagedHeader = header.ToArray();
            foreach (TerrainPatch existing in allExisting)
            {
                long existingOffset = ParseRequiredLong(
                    existing.WadRelativeOffset,
                    "patch.wadRelativeOffset");
                if (!NativeTexturePatchRangesOverlap(
                        location.WadOffset,
                        stagedHeader.Length,
                        existingOffset,
                        existing.ByteLength))
                {
                    continue;
                }

                byte[] existingBefore = HexToBytes(existing.BeforeHexPreview);
                byte[] existingAfter = HexToBytes(existing.AfterHexPreview);
                byte[] imageBefore = ReadWadBytes(
                    imageStream,
                    layout,
                    existingOffset,
                    existing.ByteLength);
                if (existingBefore.Length != existing.ByteLength ||
                    existingAfter.Length != existing.ByteLength ||
                    !imageBefore.SequenceEqual(existingBefore))
                {
                    throw new InvalidOperationException(
                        $"Existing {existing.Kind} patch at WAD 0x{existingOffset:X} has stale source bytes while composing the LP companion.");
                }

                CopyPatchIntersection(
                    existingOffset,
                    existingAfter,
                    location.WadOffset,
                    stagedHeader);
            }

            int stagedSectorByteLength = SceneSectorByteLength(stagedHeader, 0);
            byte[] sourceSector = ReadWadBytes(
                imageStream,
                layout,
                location.WadOffset,
                Math.Max(sectorByteLength, stagedSectorByteLength));
            byte[] stagedSector = sourceSector
                .AsSpan(0, stagedSectorByteLength)
                .ToArray();
            foreach (TerrainPatch existing in allExisting)
            {
                long existingOffset = ParseRequiredLong(
                    existing.WadRelativeOffset,
                    "patch.wadRelativeOffset");
                if (!NativeTexturePatchRangesOverlap(
                        location.WadOffset,
                        stagedSector.Length,
                        existingOffset,
                        existing.ByteLength))
                {
                    continue;
                }

                byte[] existingBefore = HexToBytes(existing.BeforeHexPreview);
                byte[] existingAfter = HexToBytes(existing.AfterHexPreview);
                byte[] imageBefore = ReadWadBytes(
                    imageStream,
                    layout,
                    existingOffset,
                    existing.ByteLength);
                if (existingBefore.Length != existing.ByteLength ||
                    existingAfter.Length != existing.ByteLength ||
                    !imageBefore.SequenceEqual(existingBefore))
                {
                    throw new InvalidOperationException(
                        $"Existing {existing.Kind} patch at WAD 0x{existingOffset:X} has stale source bytes while composing the LP companion.");
                }

                // Structural HP repacks may start inside the retail sector and
                // continue into source-proven append slack. Only their active
                // post-count prefix belongs to this staged sector; the complete
                // original patch remains unchanged in the output plan.
                CopyPatchIntersection(
                    existingOffset,
                    existingAfter,
                    location.WadOffset,
                    stagedSector);
            }

            NativeTerrainTextureLowDetailCompanionPatch companion =
                NativeTerrainTextureLowDetailCompanion.Build(
                    stagedSector,
                    replacements);
            if (companion.AffectedHighDetailFaceCount == 0)
                continue;

            affectedSectorCount++;
            affectedHighDetailFaceCount += companion.AffectedHighDetailFaceCount;
            if (!companion.HasChanges)
                continue;

            long colorWadOffset = checked(location.WadOffset + companion.LowDetailColorOffset);
            byte[] sourceBefore = sourceSector
                .AsSpan(companion.LowDetailColorOffset, companion.Before.Length)
                .ToArray();
            TerrainPatch[] overlapping = patchesByWadOffset.Values
                .Where(existing =>
                {
                    long existingOffset = ParseRequiredLong(
                        existing.WadRelativeOffset,
                        "patch.wadRelativeOffset");
                    return NativeTexturePatchRangesOverlap(
                        colorWadOffset,
                        sourceBefore.Length,
                        existingOffset,
                        existing.ByteLength);
                })
                .ToArray();
            foreach (TerrainPatch existing in overlapping)
            {
                long existingOffset = ParseRequiredLong(
                    existing.WadRelativeOffset,
                    "patch.wadRelativeOffset");
                if (existingOffset < colorWadOffset ||
                    existingOffset + existing.ByteLength > colorWadOffset + sourceBefore.Length)
                {
                    throw new InvalidOperationException(
                        $"Existing {existing.Kind} patch partially overlaps LP color table WAD 0x{colorWadOffset:X}; refusing an ambiguous texture/LOD composition.");
                }
            }
            foreach (TerrainPatch existing in overlapping)
            {
                long existingOffset = ParseRequiredLong(
                    existing.WadRelativeOffset,
                    "patch.wadRelativeOffset");
                patchesByWadOffset.Remove(existingOffset);
                composedPatchCount++;
            }

            AddPatch(
                imageStream,
                layout,
                patchesByWadOffset,
                colorWadOffset,
                sourceBefore,
                companion.After,
                "native-terrain-texture-lp-companion",
                $"sector-0x{location.WadOffset:X}",
                $"Keep far/unfocused terrain synchronized with transplanted texture art: recolor {companion.ChangedLowDetailColorCount}/{companion.LowDetailColorCount} native LP colors from {companion.AffectedHighDetailFaceCount} affected HP face(s), target texture id(s) {string.Join(", ", companion.TargetTextureIds)}; preserve LP geometry, face words, command bytes, and LOD flags.");
            patchCount++;
            changedColorCount += companion.ChangedLowDetailColorCount;
        }

        return new NativeTerrainTextureLowDetailCompanionSummary(
            SourceSectorCount: sectors.Length,
            AffectedSectorCount: affectedSectorCount,
            PatchCount: patchCount,
            ChangedColorCount: changedColorCount,
            AffectedHighDetailFaceCount: affectedHighDetailFaceCount,
            ComposedPatchCount: composedPatchCount,
            Note: patchCount == 0
                ? $"Checked {sectors.Length:N0} exact source sectors for native far-LOD synchronization; no LP RGB bytes needed to change."
                : $"Native texture far-LOD synchronization patched {changedColorCount:N0} LP RGB entries in {patchCount:N0}/{affectedSectorCount:N0} affected sectors ({affectedHighDetailFaceCount:N0} HP face references), composing {composedPatchCount:N0} pre-existing LP color patch(es) without changing LP geometry or LOD flags.");
    }

    private static NativeTerrainTextureRepresentativeColor ReadNativeTextureRepresentativeColor(
        FileStream imageStream,
        DiscLayout layout,
        int wadEntry,
        int textureId,
        Dictionary<int, NativeTextureAppearanceAsset> assets)
    {
        if (!assets.TryGetValue(wadEntry, out NativeTextureAppearanceAsset? asset))
        {
            AssetSubfileInfo texturePagesInfo =
                GetAssetSubfileInfo(imageStream, layout, wadEntry, TexturePagesSubfileIndex);
            AssetSubfileInfo modelInfo =
                GetAssetSubfileInfo(imageStream, layout, wadEntry, ModelSubfileIndex);
            byte[] texturePages = ReadWadBytes(
                imageStream,
                layout,
                texturePagesInfo.AbsoluteWadOffset,
                checked((int)texturePagesInfo.SubfileSize));
            byte[] modelBytes = ReadWadBytes(
                imageStream,
                layout,
                modelInfo.AbsoluteWadOffset,
                checked((int)modelInfo.SubfileSize));
            asset = new NativeTextureAppearanceAsset(
                texturePages,
                texturePagesInfo.SubfileSize,
                DecodeTextureRecords(modelBytes));
            assets.Add(wadEntry, asset);
        }

        TextureRecord? record = asset.TextureIndex.Records
            .FirstOrDefault(candidate => candidate.TextureId == textureId);
        if (record == null)
        {
            throw new InvalidOperationException(
                $"WAD entry {wadEntry} has no native texture record {textureId} for its LP companion.");
        }

        (string Tier, int TileSide, int TileGridColumns, int ImageSize, IReadOnlyList<TextureDescriptor> Descriptors)? tier =
            ChooseReadableTextureDescriptorTier(
                record,
                asset.TexturePagesByteLength,
                "hqData");
        if (tier == null ||
            !TryDecodeTerrainTextureImage(
                asset.TexturePages,
                textureId,
                tier.Value,
                out Rgba32[] pixels,
                out _))
        {
            throw new InvalidOperationException(
                $"WAD entry {wadEntry} texture {textureId} has no complete readable native image for its LP companion.");
        }

        Rgba32[] visible = pixels
            .Where(pixel => pixel.A != 0)
            .ToArray();
        if (visible.Length < 16)
        {
            throw new InvalidOperationException(
                $"WAD entry {wadEntry} texture {textureId} has only {visible.Length} visible texels; refusing an unstable LP companion color.");
        }

        return new NativeTerrainTextureRepresentativeColor(
            visible.Average(pixel => (double)pixel.R),
            visible.Average(pixel => (double)pixel.G),
            visible.Average(pixel => (double)pixel.B));
    }

    private static int SceneSectorByteLength(byte[] bytes, int offset)
    {
        if (offset < 0 || offset + 28 > bytes.Length)
            throw new InvalidDataException("Native scene-sector header is truncated.");
        int numLpVertices = bytes[offset + 16];
        int numLpColors = bytes[offset + 17];
        int numLpFaces = bytes[offset + 18];
        int numHpVertices = bytes[offset + 20];
        int numHpColors = bytes[offset + 21];
        int numHpFaces = bytes[offset + 22];
        int sizeBytes = checked(
            (7 +
             numLpVertices +
             numLpColors +
             (numLpFaces * 2) +
             numHpVertices +
             (numHpColors * 2) +
             (numHpFaces * 4)) * 4);
        if (sizeBytes < 28 || sizeBytes > 0x40000)
            throw new InvalidDataException($"Native scene-sector header describes invalid size 0x{sizeBytes:X}.");
        return sizeBytes;
    }

    private static void CopyPatchIntersection(
        long patchWadOffset,
        byte[] patchAfter,
        long destinationWadOffset,
        byte[] destination)
    {
        long intersectionStart = Math.Max(patchWadOffset, destinationWadOffset);
        long intersectionEnd = Math.Min(
            checked(patchWadOffset + patchAfter.Length),
            checked(destinationWadOffset + destination.Length));
        if (intersectionEnd <= intersectionStart)
            return;

        int sourceOffset = checked((int)(intersectionStart - patchWadOffset));
        int destinationOffset = checked((int)(intersectionStart - destinationWadOffset));
        int byteLength = checked((int)(intersectionEnd - intersectionStart));
        patchAfter.AsSpan(sourceOffset, byteLength)
            .CopyTo(destination.AsSpan(destinationOffset, byteLength));
    }

    private static string RelocationApplyModeNote(NativeTerrainTextureRelocationEdit edit) =>
        edit.PreservesTargetNativeSurface
            ? $"Texture {edit.TargetTextureId} uses art-only preserve-target mode: face texture IDs, HP material/semitransparency bits, descriptor ABR/alpha controls, tint, editor material labels, and collision/surface bytes are intentionally unchanged."
            : $"Texture {edit.TargetTextureId} uses native art plus source-proven surface-property transfer mode.";

    private static bool NativeTexturePatchRangesOverlap(
        long firstOffset,
        int firstLength,
        long secondOffset,
        int secondLength) =>
        firstOffset < checked(secondOffset + secondLength) &&
        secondOffset < checked(firstOffset + firstLength);

    private static void AddVertexPatches(
        FileStream imageStream,
        DiscLayout layout,
        byte[] ram,
        SceneSectorHeader sector,
        long sourceSectorWadOffset,
        int sectorOffset,
        string detail,
        JsonElement edit,
        Dictionary<long, TerrainPatch> patchesByWadOffset,
        List<string> skippedEdits)
    {
        string runtimeKey = JsonValue.GetString(edit, "runtimeKey");
        IReadOnlyList<float> editedZ = ReadFloatArray(edit, "editedZ");
        IReadOnlyList<float> originalZ = ReadFloatArray(edit, "originalZ");
        IReadOnlyList<Vector2f> editedPoints = ReadVector2Array(edit, "editedPoints");
        IReadOnlyList<Vector2f> originalPoints = ReadVector2Array(edit, "originalPoints");
        IReadOnlyList<int> vertexIndexes = ResolveEditableVertexIndexes(
            sector,
            detail,
            ReadIntArray(edit, "vertexIndexes"),
            MaxValueCount(editedZ.Count, originalZ.Count, editedPoints.Count, originalPoints.Count),
            originalPoints,
            originalZ);
        if (!HasEditedVertexValues(originalZ, editedZ, originalPoints, editedPoints))
            return;

        if (vertexIndexes.Count == 0 || (editedZ.Count == 0 && editedPoints.Count == 0))
        {
            skippedEdits.Add($"{runtimeKey}: edit is missing vertex indexes or edited terrain vertex values; re-save this terrain edit.");
            return;
        }

        HashSet<int> seen = new();
        for (int i = 0; i < vertexIndexes.Count && i < Math.Max(editedZ.Count, editedPoints.Count); i++)
        {
            bool zChanged = IsZEditedAt(originalZ, editedZ, i);
            bool pointChanged = IsPointEditedAt(originalPoints, editedPoints, i);
            if (!zChanged && !pointChanged)
                continue;

            int vertexIndex = vertexIndexes[i];
            if (!seen.Add(vertexIndex))
                continue;

            int runtimeVertexOffset = GetSceneVertexOffset(sector, detail, vertexIndex);
            if (runtimeVertexOffset < 0)
            {
                skippedEdits.Add($"{runtimeKey}: vertex {vertexIndex} is not valid for {detail} sector data.");
                continue;
            }

            long wadOffset = sourceSectorWadOffset + (runtimeVertexOffset - sectorOffset);
            uint oldWord = ReadUInt32(ram, runtimeVertexOffset);
            Vector3f original = DecodeSceneVertex(oldWord, sector);
            Vector2f targetPoint = i < editedPoints.Count ? editedPoints[i] : new Vector2f(original.X, original.Y);
            float targetZ = i < editedZ.Count ? editedZ[i] : original.Z;
            uint newWord;
            try
            {
                newWord = pointChanged
                    ? SetSceneVertexWord(oldWord, sector, targetPoint, targetZ)
                    : SetSceneVertexZWord(oldWord, sector, targetZ);
            }
            catch (InvalidOperationException ex)
            {
                skippedEdits.Add($"{runtimeKey}: vertex {vertexIndex} cannot be safely encoded. {ex.Message}");
                continue;
            }
            AddPatch(imageStream, layout, patchesByWadOffset, wadOffset, BitConverter.GetBytes(oldWord), BitConverter.GetBytes(newWord), $"visual-{detail}", runtimeKey, $"Set vertex {vertexIndex} to X {targetPoint.X:0.0}, Y {targetPoint.Y:0.0}, Z {targetZ:0.0}.");
        }
    }

    private static bool HasEditedVertexValues(
        IReadOnlyList<float> originalZ,
        IReadOnlyList<float> editedZ,
        IReadOnlyList<Vector2f> originalPoints,
        IReadOnlyList<Vector2f> editedPoints)
    {
        return HasEditedZValues(originalZ, editedZ) || HasEditedPointValues(originalPoints, editedPoints);
    }

    private static bool HasEditedZValues(IReadOnlyList<float> originalZ, IReadOnlyList<float> editedZ)
    {
        if (editedZ.Count == 0)
            return false;
        if (originalZ.Count == 0)
            return true;

        int count = Math.Min(originalZ.Count, editedZ.Count);
        for (int i = 0; i < count; i++)
        {
            if (Math.Abs(originalZ[i] - editedZ[i]) > 0.001f)
                return true;
        }

        return editedZ.Count != originalZ.Count;
    }

    private static bool HasEditedPointValues(IReadOnlyList<Vector2f> originalPoints, IReadOnlyList<Vector2f> editedPoints)
    {
        if (editedPoints.Count == 0)
            return false;
        if (originalPoints.Count == 0)
            return true;

        int count = Math.Min(originalPoints.Count, editedPoints.Count);
        for (int i = 0; i < count; i++)
        {
            if (Math.Abs(originalPoints[i].X - editedPoints[i].X) > 0.001f ||
                Math.Abs(originalPoints[i].Y - editedPoints[i].Y) > 0.001f)
            {
                return true;
            }
        }

        return editedPoints.Count != originalPoints.Count;
    }

    private static bool IsZEditedAt(IReadOnlyList<float> originalZ, IReadOnlyList<float> editedZ, int index)
    {
        if (index >= editedZ.Count)
            return false;
        if (index >= originalZ.Count)
            return true;

        return Math.Abs(editedZ[index] - originalZ[index]) > 0.001f;
    }

    private static bool IsPointEditedAt(IReadOnlyList<Vector2f> originalPoints, IReadOnlyList<Vector2f> editedPoints, int index)
    {
        if (index >= editedPoints.Count)
            return false;
        if (index >= originalPoints.Count)
            return true;

        return Math.Abs(editedPoints[index].X - originalPoints[index].X) > 0.001f ||
            Math.Abs(editedPoints[index].Y - originalPoints[index].Y) > 0.001f;
    }

    private static CollisionPatchContext? TryBuildSourceDerivedCollisionPatchContext(
        byte[] wad,
        IReadOnlyDictionary<string, SourceSectorLocation> sourceSectorHits,
        JsonElement editsElement,
        List<string> skippedEdits)
    {
        try
        {
            Dictionary<CollisionWordTriple, int> patterns = new();
            foreach (JsonElement edit in editsElement.EnumerateArray())
            {
                string runtimeKey = JsonValue.GetString(edit, "runtimeKey");
                if (string.IsNullOrWhiteSpace(runtimeKey) || !sourceSectorHits.ContainsKey(runtimeKey))
                    continue;

                IReadOnlyList<Vector2f> originalPoints = ReadVector2Array(edit, "originalPoints");
                IReadOnlyList<float> originalZ = ReadFloatArray(edit, "originalZ");
                int pointCount = Math.Min(originalPoints.Count, originalZ.Count);
                if (pointCount < 3)
                    continue;

                for (int i = 1; i < pointCount - 1; i++)
                {
                    SpyroCollisionPoint[] points =
                    [
                        new SpyroCollisionPoint((int)Math.Round(originalPoints[0].X), (int)Math.Round(originalPoints[0].Y), (int)Math.Round(originalZ[0])),
                        new SpyroCollisionPoint((int)Math.Round(originalPoints[i].X), (int)Math.Round(originalPoints[i].Y), (int)Math.Round(originalZ[i])),
                        new SpyroCollisionPoint((int)Math.Round(originalPoints[i + 1].X), (int)Math.Round(originalPoints[i + 1].Y), (int)Math.Round(originalZ[i + 1]))
                    ];
                    foreach (CollisionWordTriple triple in EnumerateSourceDerivedCollisionTriples(points))
                        patterns.TryAdd(triple, 0);
                }
            }

            if (patterns.Count == 0)
                return null;

            long scanStart = 0;
            long scanEnd = wad.Length;
            if (sourceSectorHits.Count > 0)
            {
                long minSector = sourceSectorHits.Values.Min(location => location.WadOffset);
                long maxSectorEnd = sourceSectorHits.Values.Max(location => location.WadOffset + Math.Max(0, location.SizeBytes));
                const long scanMarginBytes = 0x400000;
                scanStart = Math.Max(0, minSector - scanMarginBytes);
                scanEnd = Math.Min(wad.Length, maxSectorEnd + scanMarginBytes);
            }

            List<(long Offset, CollisionWordTriple Triple)> hits = new();
            for (int offset = (int)scanStart; offset + 12 <= scanEnd; offset += 4)
            {
                CollisionWordTriple triple = new(
                    BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(offset, 4)),
                    BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(offset + 4, 4)),
                    BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(offset + 8, 4)));
                if (patterns.ContainsKey(triple))
                    hits.Add((offset, triple));
            }

            if (hits.Count == 0)
            {
                skippedEdits.Add("collision: source-derived exact triangle scan found no matching source collision records for the edited terrain face(s).");
                return null;
            }

            SourceDerivedCollisionBounds bounds = BuildSourceDerivedCollisionBoundsFromSectors(wad, sourceSectorHits.Values);
            if (TryFindSourceDerivedCollisionTableSpans(wad, (int)scanStart, (int)scanEnd, hits.Select(hit => (int)hit.Offset).ToHashSet(), bounds, out IReadOnlyList<SourceDerivedCollisionTableSpan> tableSpans) &&
                tableSpans.Count > 0)
            {
                return BuildSourceDerivedCollisionPatchContextFromTableSpans(wad, tableSpans);
            }

            long sourceTriangleWadOffset = hits.Min(hit => hit.Offset);
            List<SpyroCollisionTriangle> triangles = new(hits.Count);
            Dictionary<int, byte[]> sourceTriangleBytesByIndex = new();
            foreach ((long hitOffset, CollisionWordTriple triple) in hits.OrderBy(hit => hit.Offset))
            {
                long relativeOffset = hitOffset - sourceTriangleWadOffset;
                if (relativeOffset < 0 || relativeOffset > int.MaxValue)
                    continue;

                int index = triangles.Count;
                SpyroCollisionTriangle triangle = BuildCollisionTriangleFromWords(
                    index,
                    checked((int)relativeOffset),
                    triple.XWord,
                    triple.YWord,
                    triple.ZWord);
                triangles.Add(triangle);
                sourceTriangleBytesByIndex[index] = wad.AsSpan((int)hitOffset, 12).ToArray();
            }

            if (triangles.Count == 0)
            {
                skippedEdits.Add("collision: source-derived exact triangle hits were outside the patchable WAD offset range.");
                return null;
            }

            Dictionary<string, List<SpyroCollisionTriangle>> trianglesByKey = new(StringComparer.Ordinal);
            foreach (SpyroCollisionTriangle triangle in triangles)
            {
                string key = CollisionTriangleKey(triangle.Points);
                if (!trianglesByKey.TryGetValue(key, out List<SpyroCollisionTriangle>? list))
                {
                    list = new List<SpyroCollisionTriangle>();
                    trianglesByKey[key] = list;
                }

                list.Add(triangle);
            }

            SpyroCollisionTable table = new(
                Pointers: new SpyroSceneCollisionPointers(0, 0, 0, -1, 0, 0),
                HeaderOffset: -1,
                TriangleCount: triangles.Count,
                BlockTreeOffset: -1,
                BlocksOffset: -1,
                TriangleOffset: 0,
                Etc1: 0,
                Etc2: 0,
                Triangles: triangles);
            return new CollisionPatchContext(
                Table: table,
                SourceTriangleWadOffset: sourceTriangleWadOffset,
                SourceBlockTreeWadOffset: -1,
                SourceBlocksWadOffset: -1,
                SourceTriangleWadOffsetsByIndex: new Dictionary<int, long>(),
                SourceTriangleLookupIndexByIndex: new Dictionary<int, int>(),
                TrianglesByKey: trianglesByKey,
                LookupGroups: Array.Empty<IReadOnlyList<int>>(),
                LookupGroupEntries: Array.Empty<IReadOnlyList<CollisionLookupEntry>>(),
                GroupIndexesByTriangleIndex: new Dictionary<int, List<int>>(),
                DegenerateTriangleIndexes: new HashSet<int>(),
                UnreferencedDegenerateTriangleIndexes: new HashSet<int>(),
                SourceTriangleBytesByIndex: sourceTriangleBytesByIndex,
                SourceDegenerateTriangleIndexes: new HashSet<int>(),
                SourceLookupWordsByOffset: new Dictionary<int, ushort>(),
                SourceDerivedAppendTriangleIndexesByBase: new Dictionary<int, IReadOnlyList<int>>(),
                SourceDerivedAppendTriangleIndexes: new HashSet<int>(),
                SupportsCollisionIndexRebuild: false,
                SupportsDirectLookupFallback: false);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or JsonException)
        {
            skippedEdits.Add($"collision: source-derived exact triangle scan failed: {ex.Message}");
            return null;
        }
    }

    private static CollisionPatchContext BuildSourceDerivedCollisionPatchContextFromTableSpans(byte[] wad, IReadOnlyList<SourceDerivedCollisionTableSpan> spans)
    {
        List<SpyroCollisionTriangle> triangles = new(spans.Sum(span => span.TriangleCount));
        Dictionary<int, byte[]> sourceTriangleBytesByIndex = new();
        Dictionary<int, long> sourceTriangleWadOffsetsByIndex = new();
        Dictionary<int, int> sourceTriangleLookupIndexByIndex = new();
        HashSet<int> sourceDegenerateTriangleIndexes = new();
        List<SourceDerivedCollisionAppendSpan> appendSpans = new();
        List<IReadOnlyList<int>> lookupGroups = new();
        List<IReadOnlyList<CollisionLookupEntry>> lookupGroupEntries = new();
        Dictionary<int, List<int>> groupIndexesByTriangleIndex = new();
        HashSet<int> unreferencedDegenerateTriangleIndexes = new();
        Dictionary<int, ushort> sourceLookupWordsByOffset = new();
        int lookupWordCount = 0;
        foreach (SourceDerivedCollisionTableSpan span in spans)
        {
            int triangleIndexBase = triangles.Count;
            HashSet<int> spanDegenerateTriangleIndexes = new();
            for (int i = 0; i < span.TriangleCount; i++)
            {
                int wadOffset = span.TriangleStartWadOffset + (i * 12);
                uint xWord = BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(wadOffset, 4));
                uint yWord = BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(wadOffset + 4, 4));
                uint zWord = BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(wadOffset + 8, 4));
                int triangleIndex = triangleIndexBase + i;
                SpyroCollisionTriangle triangle = BuildCollisionTriangleFromWords(triangleIndex, wadOffset, xWord, yWord, zWord);
                triangles.Add(triangle);
                sourceTriangleBytesByIndex[triangleIndex] = wad.AsSpan(wadOffset, 12).ToArray();
                sourceTriangleWadOffsetsByIndex[triangleIndex] = wadOffset;
                sourceTriangleLookupIndexByIndex[triangleIndex] = i;
                if (IsDegenerateCollisionTriangle(triangle))
                {
                    sourceDegenerateTriangleIndexes.Add(triangleIndex);
                    spanDegenerateTriangleIndexes.Add(triangleIndex);
                }
            }

            int appendStartWadOffset = span.TriangleStartWadOffset + (span.TriangleCount * 12);
            int appendLimitWadOffset = GetSourceDerivedCollisionAppendLimit(wad.Length, spans, appendStartWadOffset);
            int appendCapacity = CountSourceDerivedCollisionAppendTriangleCapacity(wad, appendStartWadOffset, appendLimitWadOffset, span.TriangleCount);
            if (appendCapacity > 0)
                appendSpans.Add(new SourceDerivedCollisionAppendSpan(triangleIndexBase, span.TriangleCount, appendStartWadOffset, appendCapacity));

            SourceDerivedLookupIndex lookupIndex = BuildSourceDerivedLookupIndex(
                wad,
                span.LookupStartWadOffset,
                span.LookupEndWadOffset,
                span.TriangleCount,
                triangleIndexBase,
                lookupGroupEntries.Count,
                spanDegenerateTriangleIndexes);
            lookupGroups.AddRange(lookupIndex.LookupGroups);
            lookupGroupEntries.AddRange(lookupIndex.LookupGroupEntries);
            foreach ((int triangleIndex, List<int> groupIndexes) in lookupIndex.GroupIndexesByTriangleIndex)
                groupIndexesByTriangleIndex[triangleIndex] = groupIndexes;
            foreach (int triangleIndex in lookupIndex.UnreferencedDegenerateTriangleIndexes)
                unreferencedDegenerateTriangleIndexes.Add(triangleIndex);
            foreach ((int offset, ushort word) in lookupIndex.SourceLookupWordsByOffset)
                sourceLookupWordsByOffset[offset] = word;
            lookupWordCount += lookupIndex.LookupWordCount;
        }

        Dictionary<int, List<int>> appendTriangleIndexesByBaseMutable = new();
        HashSet<int> sourceDerivedAppendTriangleIndexes = new();
        foreach (SourceDerivedCollisionAppendSpan appendSpan in appendSpans)
        {
            for (int i = 0; i < appendSpan.TriangleCapacity; i++)
            {
                int wadOffset = appendSpan.AppendStartWadOffset + (i * 12);
                uint xWord = BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(wadOffset, 4));
                uint yWord = BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(wadOffset + 4, 4));
                uint zWord = BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(wadOffset + 8, 4));
                int triangleIndex = triangles.Count;
                SpyroCollisionTriangle triangle = BuildCollisionTriangleFromWords(triangleIndex, wadOffset, xWord, yWord, zWord);
                triangles.Add(triangle);
                sourceTriangleBytesByIndex[triangleIndex] = wad.AsSpan(wadOffset, 12).ToArray();
                sourceTriangleWadOffsetsByIndex[triangleIndex] = wadOffset;
                sourceTriangleLookupIndexByIndex[triangleIndex] = appendSpan.SourceTriangleCount + i;
                sourceDerivedAppendTriangleIndexes.Add(triangleIndex);
                if (!appendTriangleIndexesByBaseMutable.TryGetValue(appendSpan.TriangleIndexBase, out List<int>? appendIndexes))
                {
                    appendIndexes = new List<int>();
                    appendTriangleIndexesByBaseMutable[appendSpan.TriangleIndexBase] = appendIndexes;
                }

                appendIndexes.Add(triangleIndex);
            }
        }

        Dictionary<int, IReadOnlyList<int>> appendTriangleIndexesByBase = appendTriangleIndexesByBaseMutable
            .ToDictionary(pair => pair.Key, pair => (IReadOnlyList<int>)pair.Value.ToArray());

        Dictionary<string, List<SpyroCollisionTriangle>> trianglesByKey = new(StringComparer.Ordinal);
        foreach (SpyroCollisionTriangle triangle in triangles)
        {
            string key = CollisionTriangleKey(triangle.Points);
            if (!trianglesByKey.TryGetValue(key, out List<SpyroCollisionTriangle>? list))
            {
                list = new List<SpyroCollisionTriangle>();
                trianglesByKey[key] = list;
            }

            list.Add(triangle);
        }

        SpyroCollisionTable table = new(
            Pointers: new SpyroSceneCollisionPointers(0, 0, 0, -1, 0, 0),
            HeaderOffset: -1,
            TriangleCount: triangles.Count,
            BlockTreeOffset: -1,
            BlocksOffset: 0,
            TriangleOffset: 0,
            Etc1: 0,
            Etc2: 0,
            Triangles: triangles);
        return new CollisionPatchContext(
            Table: table,
            SourceTriangleWadOffset: sourceTriangleWadOffsetsByIndex.Count > 0 ? sourceTriangleWadOffsetsByIndex.Values.Min() : 0,
            SourceBlockTreeWadOffset: -1,
            SourceBlocksWadOffset: lookupWordCount > 0 ? 0 : -1,
            SourceTriangleWadOffsetsByIndex: sourceTriangleWadOffsetsByIndex,
            SourceTriangleLookupIndexByIndex: sourceTriangleLookupIndexByIndex,
            TrianglesByKey: trianglesByKey,
            LookupGroups: lookupGroups,
            LookupGroupEntries: lookupGroupEntries,
            GroupIndexesByTriangleIndex: groupIndexesByTriangleIndex,
            DegenerateTriangleIndexes: sourceDegenerateTriangleIndexes,
            UnreferencedDegenerateTriangleIndexes: unreferencedDegenerateTriangleIndexes,
            SourceTriangleBytesByIndex: sourceTriangleBytesByIndex,
            SourceDegenerateTriangleIndexes: sourceDegenerateTriangleIndexes,
            SourceLookupWordsByOffset: sourceLookupWordsByOffset,
            SourceDerivedAppendTriangleIndexesByBase: appendTriangleIndexesByBase,
            SourceDerivedAppendTriangleIndexes: sourceDerivedAppendTriangleIndexes,
            SupportsCollisionIndexRebuild: false,
            SupportsDirectLookupFallback: lookupWordCount > 0);
    }

    private static int GetSourceDerivedCollisionAppendLimit(int wadLength, IReadOnlyList<SourceDerivedCollisionTableSpan> spans, int appendStartWadOffset)
    {
        int limit = wadLength;
        foreach (SourceDerivedCollisionTableSpan span in spans)
        {
            if (span.TriangleStartWadOffset > appendStartWadOffset)
                limit = Math.Min(limit, span.TriangleStartWadOffset);
            if (span.LookupStartWadOffset > appendStartWadOffset)
                limit = Math.Min(limit, span.LookupStartWadOffset);
        }

        return limit;
    }

    private static int CountSourceDerivedCollisionAppendTriangleCapacity(byte[] wad, int appendStartWadOffset, int appendLimitWadOffset, int sourceTriangleCount)
    {
        if (appendStartWadOffset < 0 || appendStartWadOffset >= wad.Length || appendLimitWadOffset <= appendStartWadOffset)
            return 0;

        int localIndexCapacity = Math.Max(0, 0x7FFF - sourceTriangleCount + 1);
        int byteCapacity = (Math.Min(appendLimitWadOffset, wad.Length) - appendStartWadOffset) / 12;
        int maxCapacity = Math.Min(Math.Min(MaxSourceDerivedAppendedCollisionTrianglesPerSpan, localIndexCapacity), byteCapacity);
        int capacity = 0;
        for (int i = 0; i < maxCapacity; i++)
        {
            int wadOffset = appendStartWadOffset + (i * 12);
            if (!IsSourceDerivedCollisionAppendPadding(wad.AsSpan(wadOffset, 12)))
                break;

            capacity++;
        }

        return capacity;
    }

    private static bool IsSourceDerivedCollisionAppendPadding(ReadOnlySpan<byte> bytes)
    {
        bool allZero = true;
        bool allFF = true;
        foreach (byte value in bytes)
        {
            allZero &= value == 0;
            allFF &= value == 0xFF;
            if (!allZero && !allFF)
                return false;
        }

        return allZero || allFF;
    }

    private static SourceDerivedLookupIndex BuildSourceDerivedLookupIndex(
        byte[] wad,
        int lookupStartWadOffset,
        int lookupEndWadOffset,
        int triangleCount,
        int triangleIndexBase,
        int groupIndexBase,
        IReadOnlySet<int> degenerateTriangleIndexes)
    {
        List<List<TempCollisionLookupEntry>> tempGroups = new();
        Dictionary<int, ushort> sourceLookupWordsByOffset = new();
        if (lookupStartWadOffset >= 0 && lookupEndWadOffset > lookupStartWadOffset && lookupEndWadOffset <= wad.Length)
        {
            List<TempCollisionLookupEntry> current = new();
            int entryIndex = 0;
            for (int offset = lookupStartWadOffset; offset + 2 <= lookupEndWadOffset; offset += 2)
            {
                ushort word = BinaryPrimitives.ReadUInt16LittleEndian(wad.AsSpan(offset, 2));
                int localTriangleIndex = word & 0x7FFF;
                if (localTriangleIndex < 0 || localTriangleIndex >= triangleCount)
                    continue;

                int triangleIndex = triangleIndexBase + localTriangleIndex;
                sourceLookupWordsByOffset[offset] = word;
                if ((word & 0x8000) != 0)
                {
                    if (current.Count > 0)
                        tempGroups.Add(current.ToList());
                    current.Clear();
                    entryIndex = 0;
                }

                current.Add(new TempCollisionLookupEntry(entryIndex, triangleIndex, offset, word));
                entryIndex++;
            }

            if (current.Count > 0)
                tempGroups.Add(current.ToList());
        }

        List<IReadOnlyList<int>> lookupGroups = new();
        List<IReadOnlyList<CollisionLookupEntry>> lookupGroupEntries = new();
        Dictionary<int, List<int>> groupIndexesByTriangle = new();
        HashSet<int> referencedTriangleIndexes = new();
        for (int groupIndex = 0; groupIndex < tempGroups.Count; groupIndex++)
        {
            int adjustedGroupIndex = groupIndexBase + groupIndex;
            CollisionLookupEntry[] entries = tempGroups[groupIndex]
                .Select(entry => new CollisionLookupEntry(adjustedGroupIndex, entry.EntryIndex, entry.TriangleIndex, entry.Offset, entry.Word))
                .ToArray();
            lookupGroupEntries.Add(entries);
            lookupGroups.Add(entries.Select(entry => entry.TriangleIndex).ToArray());
            foreach (CollisionLookupEntry entry in entries)
            {
                referencedTriangleIndexes.Add(entry.TriangleIndex);
                if (!groupIndexesByTriangle.TryGetValue(entry.TriangleIndex, out List<int>? groupIndexes))
                {
                    groupIndexes = new List<int>();
                    groupIndexesByTriangle[entry.TriangleIndex] = groupIndexes;
                }

                groupIndexes.Add(adjustedGroupIndex);
            }
        }

        HashSet<int> unreferencedDegenerateTriangleIndexes = degenerateTriangleIndexes
            .Where(index => !referencedTriangleIndexes.Contains(index))
            .ToHashSet();
        return new SourceDerivedLookupIndex(
            lookupGroups,
            lookupGroupEntries,
            groupIndexesByTriangle,
            unreferencedDegenerateTriangleIndexes,
            sourceLookupWordsByOffset,
            sourceLookupWordsByOffset.Count);
    }

    private static SourceDerivedCollisionBounds BuildSourceDerivedCollisionBoundsFromSectors(byte[] wad, IEnumerable<SourceSectorLocation> locations)
    {
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int minZ = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        int maxZ = int.MinValue;
        foreach (SourceSectorLocation location in locations
            .Where(location => location.WadOffset >= 0 && location.WadOffset <= int.MaxValue)
            .GroupBy(location => location.WadOffset)
            .Select(group => group.First()))
        {
            int sectorOffset = (int)location.WadOffset;
            if (sectorOffset < 0 || sectorOffset + 24 > wad.Length)
                continue;

            SceneSectorHeader sector = ReadSceneSectorHeader(wad, sectorOffset);
            for (int i = 0; i < sector.NumHpVertices; i++)
            {
                int vertexOffset = GetSceneVertexOffset(sector, "hp", i);
                if (vertexOffset < 0 || vertexOffset + 4 > wad.Length)
                    continue;

                Vector3f point = DecodeSceneVertex(ReadUInt32(wad, vertexOffset), sector);
                int x = (int)Math.Round(point.X);
                int y = (int)Math.Round(point.Y);
                int z = (int)Math.Round(point.Z);
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                minZ = Math.Min(minZ, z);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                maxZ = Math.Max(maxZ, z);
            }
        }

        if (minX == int.MaxValue)
            return new SourceDerivedCollisionBounds(0, 0x3FFF, 0, 0x3FFF, 0, 0x3FFF);

        return new SourceDerivedCollisionBounds(minX, maxX, minY, maxY, minZ, maxZ);
    }

    private static bool TryFindSourceDerivedCollisionTableSpans(
        byte[] wad,
        int scanStart,
        int scanEnd,
        IReadOnlySet<int> hitOffsets,
        SourceDerivedCollisionBounds bounds,
        out IReadOnlyList<SourceDerivedCollisionTableSpan> spans)
    {
        spans = Array.Empty<SourceDerivedCollisionTableSpan>();
        if (hitOffsets.Count == 0)
            return false;

        int clampedStart = Math.Clamp(scanStart, 0, wad.Length);
        int clampedEnd = Math.Clamp(scanEnd, clampedStart, wad.Length);
        List<SourceDerivedCollisionTableSpan> candidates = new();
        foreach (int residue in hitOffsets.Select(offset => PositiveModulo(offset, 12)).Distinct().Order())
        {
            int firstOffset = clampedStart + PositiveModulo(residue - PositiveModulo(clampedStart, 12), 12);
            int runStart = -1;
            int runCount = 0;
            int hitCount = 0;
            for (int offset = firstOffset; offset + 12 <= clampedEnd; offset += 12)
            {
                if (!LooksLikeSourceDerivedCollisionRecord(wad, offset, bounds))
                {
                    AddSourceDerivedCollisionTableSpanCandidate(candidates, wad, clampedStart, runStart, runCount, hitCount);
                    runStart = -1;
                    runCount = 0;
                    hitCount = 0;
                    continue;
                }

                if (runStart < 0)
                    runStart = offset;
                runCount++;
                if (hitOffsets.Contains(offset))
                    hitCount++;
            }

            AddSourceDerivedCollisionTableSpanCandidate(candidates, wad, clampedStart, runStart, runCount, hitCount);
        }

        SourceDerivedCollisionTableSpan[] ranked = candidates
            .Where(candidate => candidate.LookupWordCount >= 64)
            .OrderByDescending(candidate => candidate.HitCount)
            .ThenByDescending(candidate => candidate.LookupWordCount)
            .ThenByDescending(candidate => candidate.TriangleCount)
            .Take(6)
            .ToArray();
        spans = ranked;
        return ranked.Length > 0;
    }

    private static void AddSourceDerivedCollisionTableSpanCandidate(
        List<SourceDerivedCollisionTableSpan> candidates,
        byte[] wad,
        int scanStart,
        int runStart,
        int runCount,
        int hitCount)
    {
        if (runStart < 0 || runCount < 8 || hitCount <= 0)
            return;

        SourceDerivedCollisionLookupRun lookupRun = FindSourceDerivedCollisionLookupRun(wad, scanStart, runStart, runCount);
        candidates.Add(new SourceDerivedCollisionTableSpan(
            TriangleStartWadOffset: runStart,
            TriangleEndWadOffset: runStart + (runCount * 12),
            TriangleCount: runCount,
            HitCount: hitCount,
            LookupStartWadOffset: lookupRun.StartWadOffset,
            LookupEndWadOffset: lookupRun.EndWadOffset,
            LookupWordCount: lookupRun.WordCount));
    }

    private static SourceDerivedCollisionLookupRun FindSourceDerivedCollisionLookupRun(byte[] wad, int scanStart, int tableStart, int triangleCount)
    {
        if (triangleCount <= 0 || tableStart <= scanStart)
            return new SourceDerivedCollisionLookupRun(-1, -1, 0);

        int lookupScanStart = Math.Max(scanStart, tableStart - 0x40000);
        int runStart = -1;
        int wordCount = 0;
        SourceDerivedCollisionLookupRun best = new(-1, -1, 0);
        for (int offset = lookupScanStart; offset + 2 <= tableStart; offset += 2)
        {
            ushort word = BinaryPrimitives.ReadUInt16LittleEndian(wad.AsSpan(offset, 2));
            int triangleIndex = word & 0x7FFF;
            if (triangleIndex < triangleCount)
            {
                if (runStart < 0)
                    runStart = offset;
                wordCount++;
                continue;
            }

            best = BetterSourceDerivedCollisionLookupRun(best, new SourceDerivedCollisionLookupRun(runStart, offset, wordCount));
            runStart = -1;
            wordCount = 0;
        }

        return BetterSourceDerivedCollisionLookupRun(best, new SourceDerivedCollisionLookupRun(runStart, tableStart, wordCount));
    }

    private static SourceDerivedCollisionLookupRun BetterSourceDerivedCollisionLookupRun(SourceDerivedCollisionLookupRun current, SourceDerivedCollisionLookupRun candidate)
    {
        if (candidate.StartWadOffset < 0 || candidate.WordCount < 16)
            return current;
        if (current.StartWadOffset < 0)
            return candidate;
        return candidate.WordCount > current.WordCount ? candidate : current;
    }

    private static bool LooksLikeSourceDerivedCollisionRecord(byte[] wad, int offset, SourceDerivedCollisionBounds bounds)
    {
        if (offset < 0 || offset + 12 > wad.Length)
            return false;

        uint xWord = BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(offset, 4));
        uint yWord = BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(offset + 4, 4));
        uint zWord = BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(offset + 8, 4));
        DecodeSourceDerivedCollisionWord(xWord, out int x1, out int x2, out int x3);
        DecodeSourceDerivedCollisionWord(yWord, out int y1, out int y2, out int y3);
        int z1 = (int)(zWord & 0x3FFFu);
        int z2 = z1 + (int)((zWord >> 16) & 0xFFu);
        int z3 = z1 + (int)((zWord >> 24) & 0xFFu);
        if (!IsSourceDerivedCollisionPointInBounds(x1, y1, z1, bounds) ||
            !IsSourceDerivedCollisionPointInBounds(x2, y2, z2, bounds) ||
            !IsSourceDerivedCollisionPointInBounds(x3, y3, z3, bounds))
        {
            return false;
        }

        int area2 = ((x2 - x1) * (y3 - y1)) - ((x3 - x1) * (y2 - y1));
        int zSpan = Math.Max(z1, Math.Max(z2, z3)) - Math.Min(z1, Math.Min(z2, z3));
        return area2 != 0 || zSpan > 0;
    }

    private static void DecodeSourceDerivedCollisionWord(uint word, out int a, out int b, out int c)
    {
        a = (int)(word & 0x3FFFu);
        b = a + SignedBits((int)((word >> 14) & 0x1FFu), 9);
        c = a + SignedBits((int)((word >> 23) & 0x1FFu), 9);
    }

    private static bool IsSourceDerivedCollisionPointInBounds(int x, int y, int z, SourceDerivedCollisionBounds bounds)
    {
        const int xyMargin = 1024;
        const int zMargin = 2048;
        return x >= 0 && x <= 0x3FFF &&
            y >= 0 && y <= 0x3FFF &&
            z >= 0 && z <= 0x3FFF &&
            x >= bounds.MinX - xyMargin && x <= bounds.MaxX + xyMargin &&
            y >= bounds.MinY - xyMargin && y <= bounds.MaxY + xyMargin &&
            z >= bounds.MinZ - zMargin && z <= bounds.MaxZ + zMargin;
    }

    private static int PositiveModulo(int value, int divisor)
    {
        int result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private static IEnumerable<CollisionWordTriple> EnumerateSourceDerivedCollisionTriples(IReadOnlyList<SpyroCollisionPoint> points)
    {
        foreach (int[] order in CollisionPointOrders)
        {
            foreach (uint zFlags in new[] { 0x0000u, 0x4000u, 0x8000u, 0xC000u })
            {
                if (TryBuildCollisionTriangleWordsInOrder(points, order, zFlags, out CollisionWordTriple triple))
                    yield return triple;
            }
        }
    }

    private static bool TryBuildCollisionTriangleWordsInOrder(
        IReadOnlyList<SpyroCollisionPoint> points,
        IReadOnlyList<int> order,
        uint zFlags,
        out CollisionWordTriple triple)
    {
        triple = default;
        if (points.Count < 3 || order.Count < 3)
            return false;

        SpyroCollisionPoint p1 = points[order[0]];
        SpyroCollisionPoint p2 = points[order[1]];
        SpyroCollisionPoint p3 = points[order[2]];
        if (!TryEncodeSigned9(p2.X - p1.X, out uint p2Dx) ||
            !TryEncodeSigned9(p3.X - p1.X, out uint p3Dx) ||
            !TryEncodeSigned9(p2.Y - p1.Y, out uint p2Dy) ||
            !TryEncodeSigned9(p3.Y - p1.Y, out uint p3Dy))
        {
            return false;
        }

        int p2Dz = p2.Z - p1.Z;
        int p3Dz = p3.Z - p1.Z;
        if (p1.X < 0 || p1.X > 0x3FFF || p1.Y < 0 || p1.Y > 0x3FFF || p1.Z < 0 || p1.Z > 0x3FFF ||
            p2Dz < 0 || p2Dz > 0xFF || p3Dz < 0 || p3Dz > 0xFF)
        {
            return false;
        }

        triple = new CollisionWordTriple(
            (uint)(p1.X & 0x3FFF) | (p2Dx << 14) | (p3Dx << 23),
            (uint)(p1.Y & 0x3FFF) | (p2Dy << 14) | (p3Dy << 23),
            (zFlags & 0x0000C000u) | (uint)(p1.Z & 0x3FFF) | ((uint)p2Dz << 16) | ((uint)p3Dz << 24));
        return true;
    }

    private static CollisionPatchContext? TryBuildCollisionPatchContext(FileStream imageStream, DiscLayout layout, byte[] ram, List<string> skippedEdits)
    {
        try
        {
            SpyroCollisionTable table = SpyroCollisionDecoder.Decode(ram);
            int tableLength = table.TriangleCount * 12;
            int probeLength = Math.Min(tableLength, 384);
            if (probeLength < 48)
            {
                skippedEdits.Add("collision: decoded triangle table is too small to source-match safely.");
                return null;
            }

            long sourceTriangleWadOffset = FindCollisionTriangleSourceOffset(imageStream, layout, ram, table.TriangleOffset, tableLength, probeLength);
            if (sourceTriangleWadOffset < 0)
            {
                skippedEdits.Add("collision: could not find exact collision triangle bytes in the source image; terrain height patches will be visual-only.");
                return null;
            }

            byte[] sourceTriangleBytes = ReadWadBytes(imageStream, layout, sourceTriangleWadOffset, tableLength);
            Dictionary<int, byte[]> sourceTriangleBytesByIndex = new();
            HashSet<int> sourceDegenerateTriangleIndexes = new();
            for (int i = 0; i < table.TriangleCount; i++)
            {
                int byteOffset = i * 12;
                byte[] sourceBytes = sourceTriangleBytes.AsSpan(byteOffset, 12).ToArray();
                sourceTriangleBytesByIndex[i] = sourceBytes;
                SpyroCollisionTriangle sourceTriangle = BuildCollisionTriangleFromWords(
                    index: i,
                    offset: table.TriangleOffset + byteOffset,
                    xWord: BitConverter.ToUInt32(sourceBytes, 0),
                    yWord: BitConverter.ToUInt32(sourceBytes, 4),
                    zWord: BitConverter.ToUInt32(sourceBytes, 8));
                if (IsDegenerateCollisionTriangle(sourceTriangle))
                    sourceDegenerateTriangleIndexes.Add(i);
            }

            Dictionary<string, List<SpyroCollisionTriangle>> trianglesByKey = new(StringComparer.Ordinal);
            foreach (SpyroCollisionTriangle triangle in table.Triangles)
            {
                string key = CollisionTriangleKey(triangle.Points);
                if (!trianglesByKey.TryGetValue(key, out List<SpyroCollisionTriangle>? triangles))
                {
                    triangles = new List<SpyroCollisionTriangle>();
                    trianglesByKey[key] = triangles;
                }

                triangles.Add(triangle);
            }

            CollisionLookupIndex lookupIndex = BuildCollisionLookupIndex(table, ram);
            long sourceBlockTreeWadOffset = table.BlockTreeOffset >= 0
                ? sourceTriangleWadOffset - (table.TriangleOffset - table.BlockTreeOffset)
                : -1;
            long sourceBlocksWadOffset = table.BlocksOffset >= 0
                ? sourceTriangleWadOffset - (table.TriangleOffset - table.BlocksOffset)
                : -1;
            Dictionary<int, ushort> sourceLookupWordsByOffset = new();
            if (sourceBlocksWadOffset >= 0 &&
                table.BlocksOffset >= 0 &&
                table.TriangleOffset > table.BlocksOffset)
            {
                int blockBytesLength = table.TriangleOffset - table.BlocksOffset;
                byte[] sourceBlockBytes = ReadWadBytes(imageStream, layout, sourceBlocksWadOffset, blockBytesLength);
                for (int byteOffset = 0; byteOffset + 2 <= sourceBlockBytes.Length; byteOffset += 2)
                    sourceLookupWordsByOffset[table.BlocksOffset + byteOffset] = BitConverter.ToUInt16(sourceBlockBytes, byteOffset);
            }

            return new CollisionPatchContext(
                table,
                sourceTriangleWadOffset,
                sourceBlockTreeWadOffset,
                sourceBlocksWadOffset,
                new Dictionary<int, long>(),
                new Dictionary<int, int>(),
                trianglesByKey,
                lookupIndex.LookupGroups,
                lookupIndex.LookupGroupEntries,
                lookupIndex.GroupIndexesByTriangleIndex,
                lookupIndex.DegenerateTriangleIndexes,
                lookupIndex.UnreferencedDegenerateTriangleIndexes,
                sourceTriangleBytesByIndex,
                sourceDegenerateTriangleIndexes,
                sourceLookupWordsByOffset,
                new Dictionary<int, IReadOnlyList<int>>(),
                new HashSet<int>(),
                SupportsCollisionIndexRebuild: true,
                SupportsDirectLookupFallback: true);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException)
        {
            skippedEdits.Add($"collision: {ex.Message}");
            return null;
        }
    }

    private static long FindCollisionTriangleSourceOffset(
        FileStream imageStream,
        DiscLayout layout,
        byte[] ram,
        int triangleOffset,
        int tableLength,
        int probeLength)
    {
        foreach (int probeStart in CollisionProbeStarts(tableLength, probeLength))
        {
            byte[] probe = ram.AsSpan(triangleOffset + probeStart, probeLength).ToArray();
            long found = FindWadUserDataSequence(imageStream, layout, probe);
            if (found >= 0)
                return found - probeStart;
        }

        return -1;
    }

    private static IReadOnlyList<int> CollisionProbeStarts(int tableLength, int probeLength)
    {
        int maxStart = Math.Max(0, tableLength - probeLength);
        List<int> starts = new();
        void Add(int start)
        {
            int clamped = Math.Clamp(start, 0, maxStart);
            if (!starts.Contains(clamped))
                starts.Add(clamped);
        }

        Add(0);
        int denseLimit = Math.Min(maxStart, 4096);
        for (int start = 384; start <= denseLimit; start += 384)
            Add(start);

        for (int start = 8192; start <= Math.Min(maxStart, 65536); start += 8192)
            Add(start);

        Add((maxStart / 2) - ((maxStart / 2) % 12));
        Add(maxStart - (maxStart % 12));
        return starts;
    }

    private static CollisionLookupIndex BuildCollisionLookupIndex(SpyroCollisionTable table, byte[] ram)
    {
        List<List<TempCollisionLookupEntry>> tempGroups = new();
        if (table.BlocksOffset >= 0 && table.TriangleOffset > table.BlocksOffset && table.TriangleOffset <= ram.Length)
        {
            List<TempCollisionLookupEntry> current = new();
            int end = table.TriangleOffset - ((table.TriangleOffset - table.BlocksOffset) % 2);
            int entryIndex = 0;
            for (int offset = table.BlocksOffset; offset + 2 <= end; offset += 2)
            {
                ushort word = BitConverter.ToUInt16(ram, offset);
                int triangleIndex = word & 0x7FFF;
                if (triangleIndex < 0 || triangleIndex >= table.TriangleCount)
                    continue;

                if ((word & 0x8000) != 0)
                {
                    if (current.Count > 0)
                        tempGroups.Add(current.ToList());
                    current.Clear();
                    entryIndex = 0;
                }

                current.Add(new TempCollisionLookupEntry(entryIndex, triangleIndex, offset, word));
                entryIndex++;
            }

            if (current.Count > 0)
                tempGroups.Add(current.ToList());
        }

        List<IReadOnlyList<int>> lookupGroups = new();
        List<IReadOnlyList<CollisionLookupEntry>> lookupGroupEntries = new();
        Dictionary<int, List<int>> groupIndexesByTriangle = new();
        HashSet<int> referencedTriangleIndexes = new();
        for (int groupIndex = 0; groupIndex < tempGroups.Count; groupIndex++)
        {
            CollisionLookupEntry[] entries = tempGroups[groupIndex]
                .Select(entry => new CollisionLookupEntry(groupIndex, entry.EntryIndex, entry.TriangleIndex, entry.Offset, entry.Word))
                .ToArray();
            lookupGroupEntries.Add(entries);
            lookupGroups.Add(entries.Select(entry => entry.TriangleIndex).ToArray());
            foreach (CollisionLookupEntry entry in entries)
            {
                int triangleIndex = entry.TriangleIndex;
                referencedTriangleIndexes.Add(triangleIndex);
                if (!groupIndexesByTriangle.TryGetValue(triangleIndex, out List<int>? groupIndexes))
                {
                    groupIndexes = new List<int>();
                    groupIndexesByTriangle[triangleIndex] = groupIndexes;
                }

                groupIndexes.Add(groupIndex);
            }
        }

        HashSet<int> degenerateTriangleIndexes = new();
        foreach (SpyroCollisionTriangle triangle in table.Triangles)
        {
            if (IsDegenerateCollisionTriangle(triangle))
                degenerateTriangleIndexes.Add(triangle.Index);
        }

        HashSet<int> unreferencedDegenerateTriangleIndexes = degenerateTriangleIndexes
            .Where(index => !referencedTriangleIndexes.Contains(index))
            .ToHashSet();
        return new CollisionLookupIndex(
            lookupGroups,
            lookupGroupEntries,
            groupIndexesByTriangle,
            degenerateTriangleIndexes,
            unreferencedDegenerateTriangleIndexes);
    }

    private static bool IsDegenerateCollisionTriangle(SpyroCollisionTriangle triangle)
    {
        SpyroCollisionPoint a = triangle.P1;
        SpyroCollisionPoint b = triangle.P2;
        SpyroCollisionPoint c = triangle.P3;
        return a == b ||
            a == c ||
            b == c ||
            ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X)) == 0;
    }

    private static bool IsZeroAreaCollisionTriangle3D(SpyroCollisionTriangle triangle)
    {
        SpyroCollisionPoint a = triangle.P1;
        SpyroCollisionPoint b = triangle.P2;
        SpyroCollisionPoint c = triangle.P3;
        if (a == b || a == c || b == c)
            return true;

        long abX = b.X - a.X;
        long abY = b.Y - a.Y;
        long abZ = b.Z - a.Z;
        long acX = c.X - a.X;
        long acY = c.Y - a.Y;
        long acZ = c.Z - a.Z;
        long crossX = (abY * acZ) - (abZ * acY);
        long crossY = (abZ * acX) - (abX * acZ);
        long crossZ = (abX * acY) - (abY * acX);
        return crossX == 0 && crossY == 0 && crossZ == 0;
    }

    private static SpyroCollisionTriangle BuildCollisionTriangleFromWords(int index, int offset, uint xWord, uint yWord, uint zWord)
    {
        int p1X = (int)(xWord & 0x3FFF);
        int p1Y = (int)(yWord & 0x3FFF);
        int p1Z = (int)(zWord & 0x3FFF);
        SpyroCollisionPoint p1 = new(p1X, p1Y, p1Z);
        SpyroCollisionPoint p2 = new(
            p1X + SignedBits((int)((xWord >> 14) & 0x1FF), 9),
            p1Y + SignedBits((int)((yWord >> 14) & 0x1FF), 9),
            p1Z + (int)((zWord >> 16) & 0xFF));
        SpyroCollisionPoint p3 = new(
            p1X + SignedBits((int)((xWord >> 23) & 0x1FF), 9),
            p1Y + SignedBits((int)((yWord >> 23) & 0x1FF), 9),
            p1Z + (int)((zWord >> 24) & 0xFF));
        return new SpyroCollisionTriangle(index, offset, xWord, yWord, zWord, zWord & 0xC000u, p1, p2, p3);
    }

    private static bool TryAddCollisionIndexRebuildPatches(
        FileStream imageStream,
        DiscLayout layout,
        byte[] ram,
        CollisionPatchContext collisionContext,
        Dictionary<long, TerrainPatch> patchesByWadOffset,
        IReadOnlyList<PendingTerrainPatch> pendingCollisionPatches,
        out string skipReason)
    {
        skipReason = "";
        if (pendingCollisionPatches.Count == 0)
            return true;
        if (collisionContext.SourceBlockTreeWadOffset < 0 ||
            collisionContext.SourceBlocksWadOffset < 0 ||
            collisionContext.Table.BlockTreeOffset < 0 ||
            collisionContext.Table.BlocksOffset <= collisionContext.Table.BlockTreeOffset ||
            collisionContext.Table.TriangleOffset <= collisionContext.Table.BlocksOffset)
        {
            skipReason = "collision block tree/lookup offsets are unavailable.";
            return false;
        }

        int treeCapacityBytes = collisionContext.Table.BlocksOffset - collisionContext.Table.BlockTreeOffset;
        int blockCapacityBytes = collisionContext.Table.TriangleOffset - collisionContext.Table.BlocksOffset;
        int triangleBytesLength = checked(collisionContext.Table.TriangleCount * 12);
        if (triangleBytesLength <= 0 ||
            collisionContext.Table.TriangleOffset < 0 ||
            collisionContext.Table.TriangleOffset + triangleBytesLength > ram.Length)
        {
            skipReason = "collision triangle table is outside the RAM capture.";
            return false;
        }

        byte[] triangleBytes = ram.AsSpan(collisionContext.Table.TriangleOffset, triangleBytesLength).ToArray();
        if (!TryApplyCollisionTrianglePatches(collisionContext, patchesByWadOffset, pendingCollisionPatches, triangleBytes, out skipReason))
            return false;
        if (!TryBuildCollisionIndexBytes(triangleBytes, collisionContext.Table.TriangleCount, treeCapacityBytes, blockCapacityBytes, out CollisionIndexBytes? indexBytes, out skipReason) ||
            indexBytes == null)
        {
            return false;
        }

        if (collisionContext.Table.BlockTreeOffset + indexBytes.TreeBytes.Length > ram.Length ||
            collisionContext.Table.BlocksOffset + indexBytes.BlockBytes.Length > ram.Length)
        {
            skipReason = "rebuilt collision index is outside the RAM capture.";
            return false;
        }

        foreach (PendingTerrainPatch collisionPatch in pendingCollisionPatches)
        {
            if (!IsCollisionTrianglePatchKind(collisionPatch.Kind))
                continue;
            byte[] imageBefore = ReadWadBytes(imageStream, layout, collisionPatch.WadOffset, collisionPatch.Before.Length);
            if (!imageBefore.SequenceEqual(collisionPatch.Before))
            {
                skipReason = $"source collision triangle bytes at 0x{collisionPatch.WadOffset:X} are {ToHex(imageBefore)}, expected {ToHex(collisionPatch.Before)}.";
                return false;
            }
        }

        List<PendingTerrainPatch> indexPatches = new();
        byte[] oldTreeBytes = ram.AsSpan(collisionContext.Table.BlockTreeOffset, indexBytes.TreeBytes.Length).ToArray();
        if (!oldTreeBytes.SequenceEqual(indexBytes.TreeBytes))
        {
            indexPatches.Add(new PendingTerrainPatch(
                WadOffset: collisionContext.SourceBlockTreeWadOffset,
                Before: oldTreeBytes,
                After: indexBytes.TreeBytes,
                Kind: "collision-index-block-tree",
                RuntimeKey: "collision-index",
                Description: $"Rebuild collision block tree for edited terrain side-wall triangles ({indexBytes.TreeBytes.Length}/{treeCapacityBytes} bytes)."));
        }

        byte[] oldBlockBytes = ram.AsSpan(collisionContext.Table.BlocksOffset, indexBytes.BlockBytes.Length).ToArray();
        if (!oldBlockBytes.SequenceEqual(indexBytes.BlockBytes))
        {
            indexPatches.Add(new PendingTerrainPatch(
                WadOffset: collisionContext.SourceBlocksWadOffset,
                Before: oldBlockBytes,
                After: indexBytes.BlockBytes,
                Kind: "collision-index-blocks",
                RuntimeKey: "collision-index",
                Description: $"Rebuild collision block lookup for edited terrain side-wall triangles ({indexBytes.BlockBytes.Length}/{blockCapacityBytes} bytes)."));
        }

        foreach (PendingTerrainPatch indexPatch in indexPatches)
        {
            byte[] imageBefore = ReadWadBytes(imageStream, layout, indexPatch.WadOffset, indexPatch.Before.Length);
            if (!imageBefore.SequenceEqual(indexPatch.Before))
            {
                skipReason = $"source collision index bytes at 0x{indexPatch.WadOffset:X} are {ToHex(imageBefore)}, expected {ToHex(indexPatch.Before)}.";
                return false;
            }
        }

        RemoveExistingCollisionIndexPatch(patchesByWadOffset, collisionContext.SourceBlockTreeWadOffset);
        RemoveExistingCollisionIndexPatch(patchesByWadOffset, collisionContext.SourceBlocksWadOffset);
        foreach (PendingTerrainPatch indexPatch in indexPatches)
        {
            AddPatch(
                imageStream,
                layout,
                patchesByWadOffset,
                indexPatch.WadOffset,
                indexPatch.Before,
                indexPatch.After,
                indexPatch.Kind,
                indexPatch.RuntimeKey,
                indexPatch.Description);
        }

        return true;
    }

    private static void RemoveExistingCollisionIndexPatch(Dictionary<long, TerrainPatch> patchesByWadOffset, long wadOffset)
    {
        if (wadOffset < 0)
            return;
        if (patchesByWadOffset.TryGetValue(wadOffset, out TerrainPatch? patch) &&
            patch.Kind.StartsWith("collision-index-", StringComparison.OrdinalIgnoreCase))
        {
            patchesByWadOffset.Remove(wadOffset);
        }
    }

    private static bool TryApplyCollisionTrianglePatches(
        CollisionPatchContext collisionContext,
        Dictionary<long, TerrainPatch> patchesByWadOffset,
        IReadOnlyList<PendingTerrainPatch> pendingCollisionPatches,
        byte[] triangleBytes,
        out string skipReason)
    {
        skipReason = "";
        foreach ((long wadOffset, TerrainPatch patch) in patchesByWadOffset)
        {
            if (!IsCollisionTrianglePatchKind(patch.Kind))
                continue;
            if (!TryApplyCollisionTrianglePatch(collisionContext, wadOffset, HexToBytes(patch.AfterHexPreview), triangleBytes, out skipReason))
                return false;
        }

        foreach (PendingTerrainPatch patch in pendingCollisionPatches)
        {
            if (!IsCollisionTrianglePatchKind(patch.Kind))
                continue;
            if (!TryApplyCollisionTrianglePatch(collisionContext, patch.WadOffset, patch.After, triangleBytes, out skipReason))
                return false;
        }

        return true;
    }

    private static bool IsCollisionTrianglePatchKind(string kind) =>
        kind.StartsWith("collision-triangle", StringComparison.OrdinalIgnoreCase);

    private static bool TryApplyCollisionTrianglePatch(
        CollisionPatchContext collisionContext,
        long wadOffset,
        byte[] after,
        byte[] triangleBytes,
        out string skipReason)
    {
        skipReason = "";
        if (after.Length != 12)
        {
            skipReason = $"collision triangle patch at 0x{wadOffset:X} has {after.Length} bytes instead of 12.";
            return false;
        }

        long relative = wadOffset - collisionContext.SourceTriangleWadOffset;
        if (relative < 0 || relative + after.Length > triangleBytes.Length || (relative % 12) != 0)
        {
            skipReason = $"collision triangle patch at 0x{wadOffset:X} is outside the sourced collision triangle table.";
            return false;
        }

        after.CopyTo(triangleBytes, (int)relative);
        return true;
    }

    internal static bool TryBuildCollisionIndexBytes(
        byte[] triangleBytes,
        int numTriangles,
        int treeCapacityBytes,
        int blockCapacityBytes,
        out CollisionIndexBytes? indexBytes,
        out string skipReason)
    {
        indexBytes = null;
        skipReason = "";
        if (numTriangles <= 0 || triangleBytes.Length < numTriangles * 12)
        {
            skipReason = "collision triangle table is incomplete.";
            return false;
        }

        int[] xBlockSize = new int[256];
        int[] yBlockSize = new int[256];
        int[] zBlockSize = new int[256];
        CollisionTriangleBounds?[] bounds = new CollisionTriangleBounds?[numTriangles];
        for (int i = 0; i < numTriangles; i++)
        {
            SpyroCollisionTriangle triangle = ReadCollisionTriangleFromBytes(triangleBytes, i * 12, i);
            if (IsZeroAreaCollisionTriangle3D(triangle))
                continue;
            if (triangle.Index > 0x7FFF)
            {
                skipReason = $"collision triangle {triangle.Index} cannot be referenced by a 15-bit lookup entry.";
                return false;
            }

            int minX = Math.Min(triangle.P1.X, Math.Min(triangle.P2.X, triangle.P3.X)) >> 8;
            int maxX = Math.Max(triangle.P1.X, Math.Max(triangle.P2.X, triangle.P3.X)) >> 8;
            int minY = Math.Min(triangle.P1.Y, Math.Min(triangle.P2.Y, triangle.P3.Y)) >> 8;
            int maxY = Math.Max(triangle.P1.Y, Math.Max(triangle.P2.Y, triangle.P3.Y)) >> 8;
            int minZ = Math.Min(triangle.P1.Z, Math.Min(triangle.P2.Z, triangle.P3.Z)) >> 8;
            int maxZ = Math.Max(triangle.P1.Z, Math.Max(triangle.P2.Z, triangle.P3.Z)) >> 8;
            if (minX < 0 || minY < 0 || minZ < 0 || maxX > 255 || maxY > 255 || maxZ > 255)
            {
                skipReason = $"collision triangle {triangle.Index} has a block bound outside 0..255.";
                return false;
            }

            bounds[i] = new CollisionTriangleBounds(triangle, minX, maxX, minY, maxY, minZ, maxZ);
            for (int x = minX; x <= maxX; x++)
                xBlockSize[x]++;
            for (int y = minY; y <= maxY; y++)
                yBlockSize[y]++;
            for (int z = minZ; z <= maxZ; z++)
                zBlockSize[z]++;
        }

        List<int>[] xBlocks = new List<int>[256];
        int minXBlock = 256;
        int minYBlock = 256;
        int minZBlock = 256;
        int maxXBlock = 0;
        int maxYBlock = 0;
        int maxZBlock = 0;
        for (int i = 0; i < 256; i++)
        {
            if (xBlockSize[i] > 0)
            {
                xBlocks[i] = new List<int>();
                minXBlock = Math.Min(minXBlock, i);
                maxXBlock = Math.Max(maxXBlock, i);
            }
            if (yBlockSize[i] > 0)
            {
                minYBlock = Math.Min(minYBlock, i);
                maxYBlock = Math.Max(maxYBlock, i);
            }
            if (zBlockSize[i] > 0)
            {
                minZBlock = Math.Min(minZBlock, i);
                maxZBlock = Math.Max(maxZBlock, i);
            }
        }
        if (minXBlock > maxXBlock || minYBlock > maxYBlock || minZBlock > maxZBlock)
        {
            skipReason = "rebuilt collision index has no solid triangles.";
            return false;
        }

        for (int i = 0; i < numTriangles; i++)
        {
            CollisionTriangleBounds? bound = bounds[i];
            if (bound == null)
                continue;
            for (int x = bound.MinXBlock; x <= bound.MaxXBlock; x++)
                xBlocks[x].Add(i);
        }

        Dictionary<(int X, int Y, int Z), int[]> triangleIndexesByCell = new();
        for (int z = minZBlock; z <= maxZBlock; z++)
        {
            for (int y = minYBlock; y <= maxYBlock; y++)
            {
                for (int x = minXBlock; x <= maxXBlock; x++)
                {
                    List<int> triangleIndexesHere = [];
                    if (xBlocks[x] != null)
                    {
                        foreach (int triangleIndex in xBlocks[x])
                        {
                            CollisionTriangleBounds? bound = bounds[triangleIndex];
                            if (bound == null ||
                                bound.MinYBlock > y ||
                                bound.MaxYBlock < y ||
                                bound.MinZBlock > z ||
                                bound.MaxZBlock < z ||
                                !CollisionTriangleTouchesBlock(bound, x, y, z))
                            {
                                continue;
                            }

                            triangleIndexesHere.Add(triangleIndex);
                        }
                    }

                    if (triangleIndexesHere.Count > 0)
                        triangleIndexesByCell[(x, y, z)] = triangleIndexesHere.ToArray();
                }
            }
        }

        Dictionary<string, int[]> uniqueTriangleSets = new(StringComparer.Ordinal);
        foreach (int[] triangleSet in triangleIndexesByCell.Values)
            uniqueTriangleSets.TryAdd(string.Join(',', triangleSet), triangleSet);

        List<ushort> blockList = new();
        Dictionary<string, int> blockOffsetsByTriangleSet = new(StringComparer.Ordinal);
        List<(int Offset, int[] Triangles)> emittedBlockSupersets = [];
        foreach ((string triangleSetKey, int[] triangleSet) in uniqueTriangleSets
            .OrderByDescending(pair => pair.Value.Length)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal))
        {
            (int Offset, int[] Triangles)? smallestSuperset = null;
            foreach ((int candidateOffset, int[] candidateTriangles) in emittedBlockSupersets)
            {
                if (candidateTriangles.Length < triangleSet.Length ||
                    !triangleSet.All(triangleIndex => Array.BinarySearch(candidateTriangles, triangleIndex) >= 0))
                {
                    continue;
                }

                if (smallestSuperset == null || candidateTriangles.Length < smallestSuperset.Value.Triangles.Length)
                    smallestSuperset = (candidateOffset, candidateTriangles);
            }

            int blockOffset;
            if (smallestSuperset != null)
            {
                blockOffset = smallestSuperset.Value.Offset;
            }
            else
            {
                blockOffset = blockList.Count;
                for (int triangleIndex = 0; triangleIndex < triangleSet.Length; triangleIndex++)
                {
                    int word = triangleSet[triangleIndex] | (triangleIndex == 0 ? 0x8000 : 0);
                    if (!TryAddCollisionIndexUInt16(blockList, word, out skipReason))
                        return false;
                }
                emittedBlockSupersets.Add((blockOffset, triangleSet));
            }
            blockOffsetsByTriangleSet[triangleSetKey] = blockOffset;
        }

        List<ushort> zList = new();
        List<ushort> yList = new();
        List<ushort> xList = new();
        Dictionary<string, int> xSegmentOffsetsByContent = new(StringComparer.Ordinal);
        Dictionary<string, int> ySegmentOffsetsByContent = new(StringComparer.Ordinal);

        for (int i = 0; i <= maxZBlock + 1; i++)
        {
            if (!TryAddCollisionIndexUInt16(zList, 0xFFFF, out skipReason))
                return false;
        }

        int maxZSection = 0;
        for (int z = 0; z <= maxZBlock; z++)
        {
            int maxZYSection = -1;
            List<ushort> ySegment = Enumerable.Repeat((ushort)0xFFFF, maxYBlock + 2).ToList();

            if (z >= minZBlock)
            {
                for (int y = 0; y <= maxYBlock; y++)
                {
                    int maxZYXSection = -1;
                    List<ushort> xSegment = Enumerable.Repeat((ushort)0xFFFF, maxXBlock + 2).ToList();

                    if (y >= minYBlock)
                    {
                        for (int x = 0; x <= maxXBlock; x++)
                        {
                            if (triangleIndexesByCell.TryGetValue((x, y, z), out int[]? triangleSet))
                            {
                                string triangleSetKey = string.Join(',', triangleSet);
                                int blockOffset = blockOffsetsByTriangleSet[triangleSetKey];

                                if (!TrySetCollisionIndexUInt16(xSegment, 1 + x, blockOffset, out skipReason))
                                    return false;
                                maxZYXSection = x;
                            }
                        }
                    }

                    if (maxZYXSection != -1)
                    {
                        if (!TrySetCollisionIndexUInt16(xSegment, 0, maxZYXSection + 1, out skipReason))
                            return false;

                        int keep = maxZYXSection + 2;
                        if (keep < xSegment.Count)
                            xSegment.RemoveRange(keep, xSegment.Count - keep);
                        string xSegmentKey = string.Join(',', xSegment);
                        if (!xSegmentOffsetsByContent.TryGetValue(xSegmentKey, out int xSegmentStart))
                        {
                            xSegmentStart = xList.Count;
                            xList.AddRange(xSegment);
                            xSegmentOffsetsByContent[xSegmentKey] = xSegmentStart;
                        }
                        if (!TrySetCollisionIndexUInt16(ySegment, 1 + y, xSegmentStart * 2, out skipReason))
                            return false;
                        maxZYSection = y;
                    }
                }
            }

            if (maxZYSection != -1)
            {
                if (!TrySetCollisionIndexUInt16(ySegment, 0, maxZYSection + 1, out skipReason))
                    return false;

                int keep = maxZYSection + 2;
                if (keep < ySegment.Count)
                    ySegment.RemoveRange(keep, ySegment.Count - keep);
                string ySegmentKey = string.Join(',', ySegment);
                if (!ySegmentOffsetsByContent.TryGetValue(ySegmentKey, out int ySegmentStart))
                {
                    ySegmentStart = yList.Count;
                    yList.AddRange(ySegment);
                    ySegmentOffsetsByContent[ySegmentKey] = ySegmentStart;
                }
                if (!TrySetCollisionIndexUInt16(zList, 1 + z, ySegmentStart * 2, out skipReason))
                    return false;
                maxZSection = z;
            }
        }

        if (!TrySetCollisionIndexUInt16(zList, 0, maxZSection + 1, out skipReason))
            return false;
        if (maxZSection + 2 < zList.Count)
            zList.RemoveRange(maxZSection + 2, zList.Count - (maxZSection + 2));

        int zLen = zList.Count;
        int yLen = yList.Count;
        for (int i = 0; i < zList.Count;)
        {
            int length = zList[i];
            for (int j = 0; j < length; j++)
            {
                int index = i + 1 + j;
                if (zList[index] != 0xFFFF && !TrySetCollisionIndexUInt16(zList, index, zList[index] + (zLen * 2), out skipReason))
                    return false;
            }

            i += length + 1;
        }

        for (int i = 0; i < yList.Count;)
        {
            int length = yList[i];
            for (int j = 0; j < length; j++)
            {
                int index = i + 1 + j;
                if (yList[index] != 0xFFFF && !TrySetCollisionIndexUInt16(yList, index, yList[index] + ((zLen + yLen) * 2), out skipReason))
                    return false;
            }

            i += length + 1;
        }

        byte[] treeBytes = ConvertUInt16ListToBytes(zList.Concat(yList).Concat(xList).ToArray());
        byte[] blockBytes = ConvertUInt16ListToBytes(blockList);
        if (treeBytes.Length > treeCapacityBytes)
        {
            skipReason = $"rebuilt collision tree is {treeBytes.Length} bytes; capacity is {treeCapacityBytes}.";
            return false;
        }
        if (blockBytes.Length > blockCapacityBytes)
        {
            skipReason = $"rebuilt collision blocks are {blockBytes.Length} bytes; capacity is {blockCapacityBytes}.";
            return false;
        }

        indexBytes = new CollisionIndexBytes(treeBytes, blockBytes);
        return true;
    }

    private static bool CollisionTriangleTouchesBlock(CollisionTriangleBounds bounds, int x, int y, int z)
    {
        int blockX1 = x << 8;
        int blockX2 = (x + 1) << 8;
        int blockY1 = y << 8;
        int blockY2 = (y + 1) << 8;
        int blockZ1 = z << 8;
        int blockZ2 = (z + 1) << 8;
        SpyroCollisionPoint[] points = [bounds.Triangle.P1, bounds.Triangle.P2, bounds.Triangle.P3];
        foreach (SpyroCollisionPoint point in points)
        {
            if (point.X >= blockX1 && point.X < blockX2 &&
                point.Y >= blockY1 && point.Y < blockY2 &&
                point.Z >= blockZ1 && point.Z < blockZ2)
            {
                return true;
            }
        }

        for (int p = 0; p < 3; p++)
        {
            SpyroCollisionPoint current = points[p];
            SpyroCollisionPoint next = points[(p + 1) % 3];
            if (next.X != current.X)
            {
                foreach (int testPlane in new[] { blockX1, blockX2 })
                {
                    if ((current.X >= testPlane && next.X <= testPlane) ||
                        (current.X <= testPlane && next.X >= testPlane))
                    {
                        int testY = current.Y + (((next.Y - current.Y) * (testPlane - current.X)) / (next.X - current.X));
                        int testZ = current.Z + (((next.Z - current.Z) * (testPlane - current.X)) / (next.X - current.X));
                        if (testY >= blockY1 && testY < blockY2 && testZ >= blockZ1 && testZ < blockZ2)
                            return true;
                    }
                }
            }

            if (next.Y != current.Y)
            {
                foreach (int testPlane in new[] { blockY1, blockY2 })
                {
                    if ((current.Y >= testPlane && next.Y <= testPlane) ||
                        (current.Y <= testPlane && next.Y >= testPlane))
                    {
                        int testX = current.X + (((next.X - current.X) * (testPlane - current.Y)) / (next.Y - current.Y));
                        int testZ = current.Z + (((next.Z - current.Z) * (testPlane - current.Y)) / (next.Y - current.Y));
                        if (testX >= blockX1 && testX < blockX2 && testZ >= blockZ1 && testZ < blockZ2)
                            return true;
                    }
                }
            }
        }

        return false;
    }

    private static SpyroCollisionTriangle ReadCollisionTriangleFromBytes(byte[] bytes, int offset, int index)
    {
        uint xWord = ReadUInt32(bytes, offset);
        uint yWord = ReadUInt32(bytes, offset + 4);
        uint zWord = ReadUInt32(bytes, offset + 8);
        int p1X = (int)(xWord & 0x3FFF);
        int p1Y = (int)(yWord & 0x3FFF);
        int p1Z = (int)(zWord & 0x3FFF);
        SpyroCollisionPoint p1 = new(p1X, p1Y, p1Z);
        SpyroCollisionPoint p2 = new(
            p1X + SignedBits((int)((xWord >> 14) & 0x1FF), 9),
            p1Y + SignedBits((int)((yWord >> 14) & 0x1FF), 9),
            p1Z + (int)((zWord >> 16) & 0xFF));
        SpyroCollisionPoint p3 = new(
            p1X + SignedBits((int)((xWord >> 23) & 0x1FF), 9),
            p1Y + SignedBits((int)((yWord >> 23) & 0x1FF), 9),
            p1Z + (int)((zWord >> 24) & 0xFF));
        return new SpyroCollisionTriangle(index, offset, xWord, yWord, zWord, zWord & 0xC000u, p1, p2, p3);
    }

    private static int SignedBits(int value, int bits)
    {
        int sign = 1 << (bits - 1);
        int mask = 1 << bits;
        return (value & sign) != 0 ? value - mask : value;
    }

    private static byte[] ConvertUInt16ListToBytes(IReadOnlyList<ushort> values)
    {
        byte[] bytes = new byte[values.Count * 2];
        for (int i = 0; i < values.Count; i++)
        {
            bytes[i * 2] = (byte)(values[i] & 0xFF);
            bytes[(i * 2) + 1] = (byte)((values[i] >> 8) & 0xFF);
        }

        return bytes;
    }

    private static bool TryAddCollisionIndexUInt16(List<ushort> list, int value, out string skipReason)
    {
        skipReason = "";
        if (value < 0 || value > 0xFFFF)
        {
            skipReason = $"rebuilt collision index value 0x{value:X} is outside 16-bit range.";
            return false;
        }

        list.Add((ushort)value);
        return true;
    }

    private static bool TrySetCollisionIndexUInt16(List<ushort> list, int index, int value, out string skipReason)
    {
        skipReason = "";
        if (index < 0 || index >= list.Count || value < 0 || value > 0xFFFF)
        {
            skipReason = $"rebuilt collision index value 0x{value:X} is outside 16-bit range.";
            return false;
        }

        list[index] = (ushort)value;
        return true;
    }

    private static long FindWadUserDataSequence(FileStream imageStream, DiscLayout layout, byte[] needle)
    {
        if (needle.Length == 0)
            return -1;

        long sectorCount = imageStream.Length / layout.SectorSize;
        byte[] carry = Array.Empty<byte>();
        long currentWadOffset = 0;
        for (long lba = WadLba; lba < sectorCount; lba++)
        {
            long imageOffset = (lba * layout.SectorSize) + layout.UserOffset;
            if (imageOffset + 2048 > imageStream.Length)
                break;

            byte[] sector = new byte[2048];
            imageStream.Position = imageOffset;
            int read = imageStream.Read(sector, 0, sector.Length);
            if (read != sector.Length)
                break;

            byte[] buffer;
            if (carry.Length == 0)
            {
                buffer = sector;
            }
            else
            {
                buffer = new byte[carry.Length + sector.Length];
                Array.Copy(carry, 0, buffer, 0, carry.Length);
                Array.Copy(sector, 0, buffer, carry.Length, sector.Length);
            }

            int found = DiscImage.IndexOfBytes(buffer, needle, 0);
            if (found >= 0)
                return currentWadOffset - carry.Length + found;

            int carryLength = Math.Min(Math.Max(needle.Length - 1, 0), buffer.Length);
            carry = new byte[carryLength];
            Array.Copy(buffer, buffer.Length - carryLength, carry, 0, carryLength);
            currentWadOffset += 2048;
        }

        return -1;
    }

    private static void AddCollisionPatches(
        FileStream imageStream,
        DiscLayout layout,
        byte[] ram,
        CollisionPatchContext? collisionContext,
        SceneSectorHeader sector,
        string detail,
        JsonElement edit,
        Dictionary<long, TerrainPatch> patchesByWadOffset,
        List<string> skippedEdits)
    {
        if (collisionContext == null)
            return;

        string runtimeKey = JsonValue.GetString(edit, "runtimeKey");
        IReadOnlyList<float> editedZ = ReadFloatArray(edit, "editedZ");
        IReadOnlyList<float> originalZ = ReadFloatArray(edit, "originalZ");
        IReadOnlyList<Vector2f> editedPoints = ReadVector2Array(edit, "editedPoints");
        IReadOnlyList<Vector2f> originalPoints = ReadVector2Array(edit, "originalPoints");
        IReadOnlyList<int> vertexIndexes = ResolveEditableVertexIndexes(
            sector,
            detail,
            ReadIntArray(edit, "vertexIndexes"),
            MaxValueCount(editedZ.Count, originalZ.Count, editedPoints.Count, originalPoints.Count),
            originalPoints,
            originalZ);
        if (!HasEditedVertexValues(originalZ, editedZ, originalPoints, editedPoints) || vertexIndexes.Count < 3)
            return;

        int vertexCount = Math.Min(vertexIndexes.Count, Math.Max(editedZ.Count, editedPoints.Count));
        if (vertexCount < 3)
            return;

        List<CollisionPatchVertex> vertices = new(vertexCount);
        Dictionary<string, SpyroCollisionPoint> editedPointByOriginalPoint = new(StringComparer.Ordinal);
        for (int i = 0; i < vertexCount; i++)
        {
            int vertexOffset = GetSceneVertexOffset(sector, detail, vertexIndexes[i]);
            if (vertexOffset < 0)
                return;

            Vector3f original = DecodeSceneVertex(ReadUInt32(ram, vertexOffset), sector);
            Vector2f targetPoint = i < editedPoints.Count ? editedPoints[i] : new Vector2f(original.X, original.Y);
            int targetZ = i < editedZ.Count ? (int)Math.Round(editedZ[i]) : (int)Math.Round(original.Z);
            Vector3f target = new(targetPoint.X, targetPoint.Y, targetZ);
            vertices.Add(new CollisionPatchVertex(original, target));
            editedPointByOriginalPoint[CollisionPointKey((int)Math.Round(original.X), (int)Math.Round(original.Y), (int)Math.Round(original.Z))] = ToCollisionPoint(target);
        }

        HashSet<int> patchedTriangles = new();
        int matchedTriangles = 0;
        int encodedTriangles = 0;
        for (int i = 1; i < vertices.Count - 1; i++)
        {
            CollisionPatchVertex a = vertices[0];
            CollisionPatchVertex b = vertices[i];
            CollisionPatchVertex c = vertices[i + 1];
            string key = CollisionTriangleKey(
            [
                new SpyroCollisionPoint((int)Math.Round(a.Original.X), (int)Math.Round(a.Original.Y), (int)Math.Round(a.Original.Z)),
                new SpyroCollisionPoint((int)Math.Round(b.Original.X), (int)Math.Round(b.Original.Y), (int)Math.Round(b.Original.Z)),
                new SpyroCollisionPoint((int)Math.Round(c.Original.X), (int)Math.Round(c.Original.Y), (int)Math.Round(c.Original.Z))
            ]);

            if (!collisionContext.TrianglesByKey.TryGetValue(key, out List<SpyroCollisionTriangle>? triangles))
                continue;

            foreach (SpyroCollisionTriangle triangle in triangles)
            {
                matchedTriangles++;
                if (!patchedTriangles.Add(triangle.Index))
                    continue;
                if (!TryBuildCollisionTriangleWords(triangle, editedPointByOriginalPoint, out uint newXWord, out uint newYWord, out uint newZWord, out string description))
                    continue;
                encodedTriangles++;
                if (newXWord == triangle.XWord && newYWord == triangle.YWord && newZWord == triangle.ZWord)
                    continue;

                long wadOffset = GetCollisionTriangleWadOffset(collisionContext, triangle);
                byte[] before = new byte[12];
                byte[] after = new byte[12];
                BitConverter.GetBytes(triangle.XWord).CopyTo(before, 0);
                BitConverter.GetBytes(triangle.YWord).CopyTo(before, 4);
                BitConverter.GetBytes(triangle.ZWord).CopyTo(before, 8);
                BitConverter.GetBytes(newXWord).CopyTo(after, 0);
                BitConverter.GetBytes(newYWord).CopyTo(after, 4);
                BitConverter.GetBytes(newZWord).CopyTo(after, 8);
                AddPatch(
                    imageStream,
                    layout,
                    patchesByWadOffset,
                    wadOffset,
                    before,
                    after,
                    "collision-triangle",
                    runtimeKey,
                    $"Set collision triangle {triangle.Index} point values to {description}.");
            }
        }

        if (matchedTriangles == 0)
            skippedEdits.Add($"{runtimeKey}: no decoded collision triangles matched this edited terrain face.");
        else if (encodedTriangles == 0)
            skippedEdits.Add($"{runtimeKey}: matched {matchedTriangles} collision triangle(s), but the edited heights could not be packed into the current collision triangle format.");
    }

    private static bool TryBuildCollisionTriangleWords(
        SpyroCollisionTriangle triangle,
        IReadOnlyDictionary<string, SpyroCollisionPoint> editedPointByOriginalPoint,
        out uint xWord,
        out uint yWord,
        out uint zWord,
        out string description)
    {
        xWord = triangle.XWord;
        yWord = triangle.YWord;
        zWord = triangle.ZWord;
        description = "";
        SpyroCollisionPoint[] points = new SpyroCollisionPoint[3];
        for (int i = 0; i < triangle.Points.Count; i++)
        {
            SpyroCollisionPoint point = triangle.Points[i];
            if (!editedPointByOriginalPoint.TryGetValue(CollisionPointKey(point.X, point.Y, point.Z), out SpyroCollisionPoint? target) ||
                target == null)
            {
                return false;
            }

            points[i] = target;
        }

        return TryBuildCollisionTriangleWords(points, triangle.ZWord & 0x0000C000u, out xWord, out yWord, out zWord, out description);
    }

    private static bool TryBuildCollisionTriangleWords(
        IReadOnlyList<SpyroCollisionPoint> points,
        uint zFlags,
        out uint xWord,
        out uint yWord,
        out uint zWord,
        out string description)
    {
        xWord = 0;
        yWord = 0;
        zWord = 0;
        description = "";
        if (points.Count < 3)
            return false;

        foreach (int[] order in CollisionPointOrders)
        {
            SpyroCollisionPoint p1 = points[order[0]];
            SpyroCollisionPoint p2 = points[order[1]];
            SpyroCollisionPoint p3 = points[order[2]];
            if (!TryEncodeSigned9(p2.X - p1.X, out uint p2Dx) ||
                !TryEncodeSigned9(p3.X - p1.X, out uint p3Dx) ||
                !TryEncodeSigned9(p2.Y - p1.Y, out uint p2Dy) ||
                !TryEncodeSigned9(p3.Y - p1.Y, out uint p3Dy))
            {
                continue;
            }

            int p2Dz = p2.Z - p1.Z;
            int p3Dz = p3.Z - p1.Z;
            if (p1.X < 0 || p1.X > 0x3FFF || p1.Y < 0 || p1.Y > 0x3FFF || p1.Z < 0 || p1.Z > 0x3FFF ||
                p2Dz < 0 || p2Dz > 0xFF || p3Dz < 0 || p3Dz > 0xFF)
            {
                continue;
            }

            xWord = (uint)(p1.X & 0x3FFF) | (p2Dx << 14) | (p3Dx << 23);
            yWord = (uint)(p1.Y & 0x3FFF) | (p2Dy << 14) | (p3Dy << 23);
            zWord = (zFlags & 0x0000C000u)
                | (uint)(p1.Z & 0x3FFF)
                | ((uint)p2Dz << 16)
                | ((uint)p3Dz << 24);
            description = $"{p1.X},{p1.Y},{p1.Z}; {p2.X},{p2.Y},{p2.Z}; {p3.X},{p3.Y},{p3.Z}";
            return true;
        }

        return false;
    }

    private static bool TryEncodeSigned9(int value, out uint encoded)
    {
        encoded = 0;
        if (value < -256 || value > 255)
            return false;

        encoded = (uint)(value < 0 ? value + 512 : value) & 0x1FFu;
        return true;
    }

    private static Vector3f DecodeSceneVertex(uint word, SceneSectorHeader sector)
    {
        int sectorX = (int)((sector.XyPos >> 16) & 0xFFFF);
        int sectorY = (int)(sector.XyPos & 0xFFFF);
        int sectorZ = GetSceneSectorBaseZ(sector);
        int x = sectorX + (int)(((word >> 19) & 0x1FFC) >> 2);
        int y = sectorY + (int)(((word >> 8) & 0x1FFC) >> 2);
        int z = sectorZ + (int)(((word << 3) & 0x1FFC) >> 3);
        if (IsFlatSceneSector(sector))
            z >>= 3;

        return new Vector3f(x, y, z);
    }

    private static string CollisionTriangleKey(IEnumerable<SpyroCollisionPoint> points)
    {
        return string.Join("|", points
            .Select(point => CollisionPointKey(point.X, point.Y, point.Z))
            .OrderBy(value => value, StringComparer.Ordinal));
    }

    private static string CollisionPointKey(int x, int y, int z) => $"{x},{y},{z}";

    private static SpyroCollisionPoint ToCollisionPoint(Vector3f point)
    {
        return new SpyroCollisionPoint(
            (int)Math.Round(point.X),
            (int)Math.Round(point.Y),
            (int)Math.Round(point.Z));
    }

    private static int MaxValueCount(params int[] counts)
    {
        int max = 0;
        foreach (int count in counts)
            max = Math.Max(max, count);
        return max;
    }

    private static bool IsSourceDerivedSourceSearch(string sourceSearchPath)
    {
        if (string.IsNullOrWhiteSpace(sourceSearchPath) || !File.Exists(sourceSearchPath))
            return false;

        try
        {
            using FileStream stream = File.OpenRead(sourceSearchPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            string ramPath = JsonValue.GetString(document.RootElement, "ramPath");
            string purpose = JsonValue.GetString(document.RootElement, "purpose");
            return ramPath.StartsWith("source-wad:", StringComparison.OrdinalIgnoreCase)
                || purpose.Contains("source-derived", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static Dictionary<string, SourceSectorLocation> LoadSourceSectorLocations(string sourceSearchPath)
    {
        using FileStream stream = File.OpenRead(sourceSearchPath);
        using JsonDocument document = JsonDocument.Parse(stream);
        Dictionary<string, SourceSectorLocation> result = new(StringComparer.OrdinalIgnoreCase);
        if (!document.RootElement.TryGetProperty("results", out JsonElement results) || results.ValueKind != JsonValueKind.Array)
            return result;

        foreach (JsonElement item in results.EnumerateArray())
        {
            string key = JsonValue.GetString(item, "edit");
            if (string.IsNullOrWhiteSpace(key)
                || !item.TryGetProperty("fullSectorHits", out JsonElement hits)
                || hits.ValueKind != JsonValueKind.Array)
                continue;

            JsonElement hit = hits.EnumerateArray().FirstOrDefault();
            if (hit.ValueKind != JsonValueKind.Object)
                continue;

            long wadOffset = JsonValue.GetInt64(hit, "wadOffset", -1);
            int sectorSizeBytes = JsonValue.GetInt32(item, "sectorSizeBytes", 0);
            if (wadOffset >= 0)
                result[key] = new SourceSectorLocation(
                    RuntimeKey: key,
                    SectorOffset: JsonValue.GetString(item, "sectorOffset"),
                    WadOffset: wadOffset,
                    SizeBytes: sectorSizeBytes);
        }

        return result;
    }

    private static IReadOnlyList<CustomTerrainTexturePatchSummary> AddCustomTexturePatches(
        FileStream imageStream,
        DiscLayout layout,
        string levelKey,
        int textureAssetWadIndex,
        string customTexturesPath,
        Dictionary<long, TerrainPatch> patchesByWadOffset,
        List<string> skippedEdits)
    {
        IReadOnlyList<CustomTerrainTextureImport> imports = CustomTerrainTextureStore.LoadManifest(customTexturesPath);
        if (imports.Count == 0)
            return Array.Empty<CustomTerrainTexturePatchSummary>();

        AssetSubfileInfo texturePagesInfo = GetAssetSubfileInfo(imageStream, layout, textureAssetWadIndex, TexturePagesSubfileIndex);
        AssetSubfileInfo modelInfo = GetAssetSubfileInfo(imageStream, layout, textureAssetWadIndex, ModelSubfileIndex);
        byte[] modelBytes = ReadWadBytes(imageStream, layout, modelInfo.AbsoluteWadOffset, checked((int)modelInfo.SubfileSize));
        TextureRecordIndex textureIndex = DecodeTextureRecords(modelBytes);

        List<CustomTerrainTexturePatchSummary> summaries = new();
        foreach (CustomTerrainTextureImport import in imports)
        {
            if (UsesBlockedLegacyTerrainTexturePath(import, out string legacyBlockReason))
            {
                AddAtomicTerrainSwapBlock(
                    skippedEdits,
                    $"texture {import.TextureId}: {legacyBlockReason}; no texture patches were applied.");
                continue;
            }

            if (import.TextureId < 0 || import.TextureId >= textureIndex.TextureCount)
            {
                AddAtomicTerrainSwapBlock(
                    skippedEdits,
                    $"texture {import.TextureId}: custom texture import blocked because it is outside the decoded {levelKey} texture table; no texture patches were applied.");
                continue;
            }

            string sourcePath = ResolveManifestRelativePath(customTexturesPath, import.SourceImagePath);
            bool useNativeRawTransplant = string.Equals(
                import.SourceKind,
                "borrowed-cross-level-texture-art",
                StringComparison.OrdinalIgnoreCase);
            if (!File.Exists(sourcePath) && !useNativeRawTransplant)
            {
                AddAtomicTerrainSwapBlock(
                    skippedEdits,
                    $"texture {import.TextureId}: custom texture import blocked because the source image is missing at {sourcePath}; no texture patches were applied.");
                continue;
            }

            Dictionary<long, TerrainPatch> importPatchesByWadOffset = new();
            List<CustomTerrainTexturePatchSummary> importSummaries = new();
            List<string> importBlockReasons = new();
            TextureRecord record = textureIndex.Records[import.TextureId];
            if (useNativeRawTransplant)
            {
                if (!TryBuildNativeCrossLevelTexturePatches(
                        imageStream,
                        layout,
                        import,
                        sourcePath,
                        texturePagesInfo,
                        record,
                        importPatchesByWadOffset,
                        importSummaries,
                        out string rawTransplantFailure))
                {
                    importBlockReasons.Add(rawTransplantFailure);
                }
            }
            else
            {
                foreach (string descriptorTier in ExpandDescriptorTiers(import.DescriptorTier))
                {
                    bool closeTier = descriptorTier == "hqDataClose";
                    int tileGridColumns = closeTier ? 4 : 2;
                    IReadOnlyList<TextureDescriptor> descriptors = closeTier ? record.HqDataClose : record.HqData;
                    if (descriptors.Count == 0)
                    {
                        importBlockReasons.Add($"requested tier {descriptorTier} has no descriptors");
                        continue;
                    }

                    var readableTier = TryBuildReadableTextureImageTier(
                        descriptorTier,
                        descriptors,
                        tileGridColumns,
                        texturePagesInfo.SubfileSize);
                    if (readableTier == null)
                    {
                        importBlockReasons.Add(
                            $"requested tier {descriptorTier} is not a complete packed 4/8-bpp native texture record");
                        continue;
                    }

                    int tilePixelSide = readableTier.Value.TileSide;
                    int tileSize = readableTier.Value.ImageSize;
                    int bitsPerPixel = descriptors[0].BitsPerPixel;
                    int paletteColorCount = 1 << bitsPerPixel;
                    int paletteByteLength = paletteColorCount * 2;
                    Rgba32[] pixels;
                    try
                    {
                        pixels = PngRgbaImage.ReadResizedRgba(sourcePath, tileSize, tileSize);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                    {
                        importBlockReasons.Add($"requested tier {descriptorTier} could not read the source image ({ex.Message})");
                        continue;
                    }

                PaletteInfo palette = BuildPalette(pixels, paletteColorCount);
                byte[] paletteBytes = new byte[paletteByteLength];
                for (int i = 0; i < paletteColorCount; i++)
                    WriteUInt16(paletteBytes, i * 2, palette.Words[i]);

                HashSet<int> paletteStarts = new();
                HashSet<int> sourceTexels = new();
                HashSet<ushort> sourcePaletteWords = new();
                int sourceTexelMax = 0;
                int sourcePaletteNonZeroWords = 0;
                Dictionary<long, byte> sourceBytesByRelativeOffset = new();
                Dictionary<long, byte> afterBytesByRelativeOffset = new();
                Dictionary<(long RelativeOffset, int Nibble), byte> assignedPaletteIndexes = new();
                bool hasConflictingTexelAssignments = false;
                bool hasInvalidDescriptor = false;
                string conflictDescription = "";
                foreach (TextureDescriptor descriptor in descriptors)
                {
                    int paletteByteStart = descriptor.PaletteByteStart;
                    if (!CanPatchTextureDescriptor(descriptor, tilePixelSide, texturePagesInfo.SubfileSize) ||
                        paletteByteStart < 0 || paletteByteStart + paletteByteLength > texturePagesInfo.SubfileSize)
                    {
                        importBlockReasons.Add($"requested tier {descriptorTier} descriptor {descriptor.Index} maps outside texture-pages subfile {TexturePagesSubfileIndex}");
                        hasInvalidDescriptor = true;
                        break;
                    }

                    int tile = descriptor.Index;
                    int destTileX = (tile % tileGridColumns) * tilePixelSide;
                    int destTileY = (tile / tileGridColumns) * tilePixelSide;
                    if (paletteStarts.Add(paletteByteStart))
                    {
                        long paletteWadOffset = texturePagesInfo.AbsoluteWadOffset + paletteByteStart;
                        byte[] oldPaletteBytes = ReadWadBytes(imageStream, layout, paletteWadOffset, paletteByteLength);
                        for (int i = 0; i < paletteColorCount; i++)
                        {
                            ushort word = ReadUInt16(oldPaletteBytes, i * 2);
                            sourcePaletteWords.Add(word);
                            if ((word & 0x7FFF) != 0)
                                sourcePaletteNonZeroWords++;
                        }
                    }

                    for (int y = 0; y < tilePixelSide; y++)
                    {
                        for (int x = 0; x < tilePixelSide; x++)
                        {
                            if (!TryGetHqTextureImageSampleAddress(
                                    descriptor,
                                    x,
                                    y,
                                    tilePixelSide,
                                    texturePagesInfo.SubfileSize,
                                    out long relative,
                                    out int nibble))
                            {
                                importBlockReasons.Add($"requested tier {descriptorTier} descriptor {tile} maps outside the texture-page image");
                                hasInvalidDescriptor = true;
                                break;
                            }

                            Rgba32 color = pixels[(destTileY + y) * tileSize + destTileX + x];
                            ushort pixelWord = ConvertColorToPsx555(color);
                            byte paletteIndex = (byte)GetNearestPaletteIndex(pixelWord, palette);
                            int assignmentNibble = bitsPerPixel == 4 ? nibble : 0;
                            var assignmentKey = (relative, assignmentNibble);
                            if (assignedPaletteIndexes.TryGetValue(assignmentKey, out byte assigned) && assigned != paletteIndex)
                            {
                                hasConflictingTexelAssignments = true;
                                conflictDescription = $"descriptor {tile} maps a different atlas color to shared texel 0x{relative:X} nibble {assignmentNibble}";
                                break;
                            }

                            assignedPaletteIndexes[assignmentKey] = paletteIndex;
                            if (!sourceBytesByRelativeOffset.TryGetValue(relative, out byte sourceByte))
                            {
                                sourceByte = ReadWadBytes(imageStream, layout, texturePagesInfo.AbsoluteWadOffset + relative, 1)[0];
                                sourceBytesByRelativeOffset[relative] = sourceByte;
                                afterBytesByRelativeOffset[relative] = sourceByte;
                            }

                            sourceTexels.Add(paletteIndex);
                            sourceTexelMax = Math.Max(sourceTexelMax, paletteIndex);
                            afterBytesByRelativeOffset[relative] = bitsPerPixel == 4
                                ? assignmentNibble == 0
                                    ? (byte)((afterBytesByRelativeOffset[relative] & 0xF0) | (paletteIndex & 0x0F))
                                    : (byte)((afterBytesByRelativeOffset[relative] & 0x0F) | ((paletteIndex & 0x0F) << 4))
                                : paletteIndex;
                        }

                        if (hasConflictingTexelAssignments || hasInvalidDescriptor)
                            break;
                    }

                    if (hasConflictingTexelAssignments || hasInvalidDescriptor)
                        break;
                }

                if (hasConflictingTexelAssignments)
                {
                    importBlockReasons.Add(bitsPerPixel == 4
                        ? $"requested tier {descriptorTier} has a shared-nibble conflict because {conflictDescription}"
                        : $"requested tier {descriptorTier} has a shared-texel conflict because {conflictDescription}");
                    continue;
                }

                if (hasInvalidDescriptor)
                    continue;

                bool hasTierPatchConflict = false;
                foreach (int paletteByteStart in paletteStarts)
                {
                    long paletteWadOffset = texturePagesInfo.AbsoluteWadOffset + paletteByteStart;
                    byte[] oldPaletteBytes = ReadWadBytes(imageStream, layout, paletteWadOffset, paletteByteLength);
                    if (!TryAddTemporaryCustomTexturePatch(
                            imageStream,
                            layout,
                            importPatchesByWadOffset,
                            paletteWadOffset,
                            oldPaletteBytes,
                            paletteBytes,
                            "custom-texture-palette",
                            $"texture-{import.TextureId}-{descriptorTier}",
                            $"Replace texture {import.TextureId} {paletteColorCount}-color palette for {descriptorTier}.",
                            out string patchConflict))
                    {
                        importBlockReasons.Add($"requested tier {descriptorTier} {patchConflict}");
                        hasTierPatchConflict = true;
                        break;
                    }
                }

                if (hasTierPatchConflict)
                    continue;

                foreach ((long relative, byte afterByte) in afterBytesByRelativeOffset.OrderBy(pair => pair.Key))
                {
                    byte beforeByte = sourceBytesByRelativeOffset[relative];
                    if (!TryAddTemporaryCustomTexturePatch(
                        imageStream,
                        layout,
                        importPatchesByWadOffset,
                        texturePagesInfo.AbsoluteWadOffset + relative,
                        [beforeByte],
                        [afterByte],
                        "custom-texture-pixel",
                        $"texture-{import.TextureId}-{descriptorTier}",
                        $"Replace texture {import.TextureId} packed {bitsPerPixel}bpp texel byte for {descriptorTier}.",
                        out string patchConflict))
                    {
                        importBlockReasons.Add($"requested tier {descriptorTier} {patchConflict}");
                        hasTierPatchConflict = true;
                        break;
                    }
                }

                if (hasTierPatchConflict)
                    continue;

                    int pixelPatchCount = afterBytesByRelativeOffset.Count;
                    if (pixelPatchCount > 0 || paletteStarts.Count > 0)
                    {
                        importSummaries.Add(new CustomTerrainTexturePatchSummary(
                        TextureId: import.TextureId,
                        SourceImagePath: sourcePath,
                        SourceImageName: import.SourceImageName,
                        DescriptorTier: descriptorTier,
                        TileSize: tileSize,
                        DescriptorCount: descriptors.Count,
                        PixelPatchCount: pixelPatchCount,
                        PalettePatchCount: paletteStarts.Count,
                        TexelBytesPerPixel: 1,
                        BitsPerPixel: bitsPerPixel,
                        PaletteColorCount: paletteColorCount,
                        SourceTexelUniqueCount: sourceTexels.Count,
                        SourceTexelMax: sourceTexelMax,
                        SourcePaletteUniqueColorCount: sourcePaletteWords.Count,
                        SourcePaletteNonZeroColorCount: sourcePaletteNonZeroWords,
                            PaletteByteStarts: paletteStarts.OrderBy(value => value).Select(value => $"0x{value:X}").ToArray()));
                    }
                }
            }

            foreach ((long wadOffset, TerrainPatch patch) in importPatchesByWadOffset)
            {
                if (patchesByWadOffset.TryGetValue(wadOffset, out TerrainPatch? existing) &&
                    !string.Equals(existing.AfterHexPreview, patch.AfterHexPreview, StringComparison.OrdinalIgnoreCase))
                {
                    importBlockReasons.Add($"a staged patch conflicts with {existing.Kind} at WAD offset 0x{wadOffset:X}");
                    break;
                }
            }

            if (importBlockReasons.Count > 0)
            {
                string reasons = string.Join("; ", importBlockReasons.Distinct(StringComparer.OrdinalIgnoreCase));
                AddAtomicTerrainSwapBlock(
                    skippedEdits,
                    $"texture {import.TextureId}: custom texture import blocked because {reasons}; no texture patches were applied.");
                continue;
            }

            foreach ((long wadOffset, TerrainPatch patch) in importPatchesByWadOffset)
                patchesByWadOffset.TryAdd(wadOffset, patch);
            summaries.AddRange(importSummaries);
        }

        return summaries;
    }

    private static bool UsesBlockedLegacyTerrainTexturePath(
        CustomTerrainTextureImport import,
        out string reason)
    {
        bool nativeCrossLevel = string.Equals(
            import.SourceKind,
            "borrowed-cross-level-texture-art",
            StringComparison.OrdinalIgnoreCase);
        if (nativeCrossLevel)
        {
            bool partialTier = !string.Equals(import.DescriptorTier, "both", StringComparison.OrdinalIgnoreCase);
            string partialNote = partialTier
                ? $" The requested partial tier '{import.DescriptorTier}' is runtime-incomplete."
                : "";
            reason =
                "cross-level native terrain art is blocked until a source-hash-bound runtime ownership closure, " +
                "verified relocation plan, protected-storage proof, and logical readback all pass." + partialNote;
            return true;
        }

        reason =
            "custom PNG terrain art is blocked because this manifest would use the obsolete 0x800-row and fixed 4-bpp close-detail writer instead of the verified packed layout";
        return true;
    }

    private static bool TryBuildNativeCrossLevelTexturePatches(
        FileStream imageStream,
        DiscLayout layout,
        CustomTerrainTextureImport import,
        string previewSourcePath,
        AssetSubfileInfo targetTexturePagesInfo,
        TextureRecord targetRecord,
        Dictionary<long, TerrainPatch> importPatchesByWadOffset,
        List<CustomTerrainTexturePatchSummary> importSummaries,
        out string failureReason)
    {
        if (import.SourceWadEntry < 0 || import.SourceTextureId < 0)
        {
            failureReason = "native cross-level art is missing its source WAD entry or source texture id; the PNG preview was not substituted for the native data";
            return false;
        }

        IReadOnlyList<string> descriptorTiers = ExpandDescriptorTiers(import.DescriptorTier);
        try
        {
            AssetSubfileInfo donorTexturePagesInfo = GetAssetSubfileInfo(
                imageStream,
                layout,
                import.SourceWadEntry,
                TexturePagesSubfileIndex);
            AssetSubfileInfo donorModelInfo = GetAssetSubfileInfo(
                imageStream,
                layout,
                import.SourceWadEntry,
                ModelSubfileIndex);
            byte[] donorModelBytes = ReadWadBytes(
                imageStream,
                layout,
                donorModelInfo.AbsoluteWadOffset,
                checked((int)donorModelInfo.SubfileSize));
            TextureRecordIndex donorTextureIndex = DecodeTextureRecords(donorModelBytes);
            if (import.SourceTextureId >= donorTextureIndex.TextureCount)
            {
                failureReason = $"native donor texture {import.SourceTextureId} is outside source WAD entry {import.SourceWadEntry}'s decoded {donorTextureIndex.TextureCount}-texture table";
                return false;
            }

            TextureRecord donorRecord = donorTextureIndex.Records[import.SourceTextureId];
            string donorTopology = BuildCombinedDescriptorTopologySignature(
                donorRecord,
                descriptorTiers,
                donorTexturePagesInfo.SubfileSize,
                out string donorTopologyFailure);
            if (string.IsNullOrWhiteSpace(donorTopology))
            {
                failureReason = $"native donor descriptor layout is not readable: {donorTopologyFailure}";
                return false;
            }

            string targetTopology = BuildCombinedDescriptorTopologySignature(
                targetRecord,
                descriptorTiers,
                targetTexturePagesInfo.SubfileSize,
                out string targetTopologyFailure);
            if (string.IsNullOrWhiteSpace(targetTopology))
            {
                failureReason = $"target descriptor layout is not writable: {targetTopologyFailure}";
                return false;
            }

            if (!string.Equals(donorTopology, targetTopology, StringComparison.Ordinal))
            {
                failureReason =
                    $"native raw transplant topology mismatch for {string.Join("+", descriptorTiers)} " +
                    $"(donor {donorTopology}; target {targetTopology}); palette bytes, pixel bytes, and close-tier nibbles do not share the same physical alias layout";
                return false;
            }

            if (!TryBuildTexturePhysicalNibbleLayout(
                    donorRecord,
                    descriptorTiers,
                    donorTexturePagesInfo.SubfileSize,
                    out TexturePhysicalNibbleReference[] donorReferences,
                    out _,
                    out string donorLayoutFailure))
            {
                failureReason = $"native donor descriptor layout could not be enumerated: {donorLayoutFailure}";
                return false;
            }

            if (!TryBuildTexturePhysicalNibbleLayout(
                    targetRecord,
                    descriptorTiers,
                    targetTexturePagesInfo.SubfileSize,
                    out TexturePhysicalNibbleReference[] targetReferences,
                    out _,
                    out string targetLayoutFailure))
            {
                failureReason = $"target descriptor layout could not be enumerated: {targetLayoutFailure}";
                return false;
            }

            if (donorReferences.Length != targetReferences.Length)
            {
                failureReason = $"native raw transplant layout length mismatch ({donorReferences.Length} donor nibble(s), {targetReferences.Length} target nibble(s))";
                return false;
            }

            byte[] donorTexturePages = ReadWadBytes(
                imageStream,
                layout,
                donorTexturePagesInfo.AbsoluteWadOffset,
                checked((int)donorTexturePagesInfo.SubfileSize));
            byte[] targetTexturePages = ReadWadBytes(
                imageStream,
                layout,
                targetTexturePagesInfo.AbsoluteWadOffset,
                checked((int)targetTexturePagesInfo.SubfileSize));
            Dictionary<(long RelativeOffset, int Nibble), byte> assignedTargetNibbles = new();
            Dictionary<long, byte> afterBytesByRelativeOffset = new();
            Dictionary<long, int> targetByteKinds = new();
            for (int i = 0; i < donorReferences.Length; i++)
            {
                TexturePhysicalNibbleReference donorReference = donorReferences[i];
                TexturePhysicalNibbleReference targetReference = targetReferences[i];
                byte donorByte = donorTexturePages[checked((int)donorReference.RelativeOffset)];
                byte donorNibble = (byte)((donorByte >> (donorReference.Nibble * 4)) & 0x0F);
                var targetNibbleKey = (targetReference.RelativeOffset, targetReference.Nibble);
                if (assignedTargetNibbles.TryGetValue(targetNibbleKey, out byte existingNibble) && existingNibble != donorNibble)
                {
                    failureReason =
                        $"native raw transplant alias conflict at target byte 0x{targetReference.RelativeOffset:X} nibble {targetReference.Nibble}: " +
                        $"donor semantics request both 0x{existingNibble:X1} and 0x{donorNibble:X1}";
                    return false;
                }

                assignedTargetNibbles[targetNibbleKey] = donorNibble;
                if (!afterBytesByRelativeOffset.TryGetValue(targetReference.RelativeOffset, out byte afterByte))
                    afterByte = targetTexturePages[checked((int)targetReference.RelativeOffset)];
                afterByte = targetReference.Nibble == 0
                    ? (byte)((afterByte & 0xF0) | donorNibble)
                    : (byte)((afterByte & 0x0F) | (donorNibble << 4));
                afterBytesByRelativeOffset[targetReference.RelativeOffset] = afterByte;
                int kindFlag = string.Equals(targetReference.Kind, "palette", StringComparison.Ordinal) ? 1 : 2;
                targetByteKinds[targetReference.RelativeOffset] = targetByteKinds.TryGetValue(targetReference.RelativeOffset, out int existingKinds)
                    ? existingKinds | kindFlag
                    : kindFlag;
            }

            string donorLabel = string.IsNullOrWhiteSpace(import.SourceLevelKey)
                ? $"WAD entry {import.SourceWadEntry} texture {import.SourceTextureId}"
                : $"{import.SourceLevelKey} texture {import.SourceTextureId}";
            foreach ((long relativeOffset, byte afterByte) in afterBytesByRelativeOffset.OrderBy(pair => pair.Key))
            {
                byte beforeByte = targetTexturePages[checked((int)relativeOffset)];
                int kindFlags = targetByteKinds[relativeOffset];
                string dataKind = kindFlags == 1 ? "palette" : kindFlags == 2 ? "pixel" : "shared";
                if (!TryAddTemporaryCustomTexturePatch(
                        imageStream,
                        layout,
                        importPatchesByWadOffset,
                        targetTexturePagesInfo.AbsoluteWadOffset + relativeOffset,
                        [beforeByte],
                        [afterByte],
                        $"custom-texture-native-raw-{dataKind}",
                        $"texture-{import.TextureId}-native-raw",
                        $"Copy exact native indexed {dataKind} data from {donorLabel} into target texture {import.TextureId}.",
                        out string patchConflict))
                {
                    failureReason = $"native raw transplant {patchConflict}";
                    return false;
                }
            }

            foreach (string descriptorTier in descriptorTiers)
            {
                importSummaries.Add(BuildNativeRawTierSummary(
                    import,
                    previewSourcePath,
                    descriptorTier,
                    donorRecord,
                    targetRecord,
                    donorTexturePages,
                    donorTexturePagesInfo.SubfileSize,
                    targetTexturePagesInfo.SubfileSize));
            }

            failureReason = "";
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or OverflowException or ArgumentOutOfRangeException)
        {
            failureReason = $"native raw transplant could not decode source WAD entry {import.SourceWadEntry}: {ex.Message}";
            return false;
        }
    }

    private static CustomTerrainTexturePatchSummary BuildNativeRawTierSummary(
        CustomTerrainTextureImport import,
        string previewSourcePath,
        string descriptorTier,
        TextureRecord donorRecord,
        TextureRecord targetRecord,
        byte[] donorTexturePages,
        long donorTexturePagesSize,
        long targetTexturePagesSize)
    {
        bool closeTier = string.Equals(descriptorTier, "hqDataClose", StringComparison.OrdinalIgnoreCase);
        IReadOnlyList<TextureDescriptor> donorDescriptors = closeTier ? donorRecord.HqDataClose : donorRecord.HqData;
        IReadOnlyList<TextureDescriptor> targetDescriptors = closeTier ? targetRecord.HqDataClose : targetRecord.HqData;
        int tileGridColumns = closeTier ? 4 : 2;
        var donorTier = TryBuildReadableTextureImageTier(
            descriptorTier,
            donorDescriptors,
            tileGridColumns,
            donorTexturePagesSize);
        var targetTier = TryBuildReadableTextureImageTier(
            descriptorTier,
            targetDescriptors,
            tileGridColumns,
            targetTexturePagesSize);
        if (donorTier == null || targetTier == null ||
            donorTier.Value.TileSide != targetTier.Value.TileSide ||
            donorDescriptors[0].BitsPerPixel != targetDescriptors[0].BitsPerPixel)
        {
            throw new InvalidOperationException(
                $"Native raw summary cannot pair incompatible {descriptorTier} descriptor layouts.");
        }

        int tileSide = donorTier.Value.TileSide;
        int bitsPerPixel = donorDescriptors[0].BitsPerPixel;
        int paletteColorCount = 1 << bitsPerPixel;
        int paletteByteLength = paletteColorCount * 2;
        HashSet<long> targetPixelByteOffsets = new();
        HashSet<int> targetPaletteStarts = targetDescriptors.Select(descriptor => descriptor.PaletteByteStart).ToHashSet();
        HashSet<int> donorTexels = new();
        HashSet<ushort> donorPaletteWords = new();
        int donorTexelMax = 0;
        int donorPaletteNonZeroWords = 0;

        foreach ((TextureDescriptor donorDescriptor, TextureDescriptor targetDescriptor) in donorDescriptors
                     .OrderBy(descriptor => descriptor.Index)
                     .Zip(targetDescriptors.OrderBy(descriptor => descriptor.Index)))
        {
            if (!CanPatchTextureDescriptor(donorDescriptor, tileSide, donorTexturePagesSize) ||
                !CanPatchTextureDescriptor(targetDescriptor, tileSide, targetTexturePagesSize))
            {
                continue;
            }

            for (int paletteByte = 0; paletteByte < paletteByteLength; paletteByte += 2)
            {
                ushort word = ReadUInt16(donorTexturePages, donorDescriptor.PaletteByteStart + paletteByte);
                donorPaletteWords.Add(word);
                if ((word & 0x7FFF) != 0)
                    donorPaletteNonZeroWords++;
            }

            for (int y = 0; y < tileSide; y++)
            {
                for (int x = 0; x < tileSide; x++)
                {
                    TryGetHqTextureImageSampleAddress(
                        donorDescriptor,
                        x,
                        y,
                        tileSide,
                        donorTexturePagesSize,
                        out long donorRelative,
                        out int donorNibble);
                    TryGetHqTextureImageSampleAddress(
                        targetDescriptor,
                        x,
                        y,
                        tileSide,
                        targetTexturePagesSize,
                        out long targetRelative,
                        out _);
                    int texel = bitsPerPixel == 4
                        ? (donorTexturePages[checked((int)donorRelative)] >> (donorNibble * 4)) & 0x0F
                        : donorTexturePages[checked((int)donorRelative)];
                    donorTexels.Add(texel);
                    donorTexelMax = Math.Max(donorTexelMax, texel);
                    targetPixelByteOffsets.Add(targetRelative);
                }
            }
        }

        return new CustomTerrainTexturePatchSummary(
            TextureId: import.TextureId,
            SourceImagePath: previewSourcePath,
            SourceImageName: import.SourceImageName,
            DescriptorTier: descriptorTier,
            TileSize: targetTier.Value.ImageSize,
            DescriptorCount: targetDescriptors.Count,
            PixelPatchCount: targetPixelByteOffsets.Count,
            PalettePatchCount: targetPaletteStarts.Count,
            TexelBytesPerPixel: 1,
            BitsPerPixel: bitsPerPixel,
            PaletteColorCount: paletteColorCount,
            SourceTexelUniqueCount: donorTexels.Count,
            SourceTexelMax: donorTexelMax,
            SourcePaletteUniqueColorCount: donorPaletteWords.Count,
            SourcePaletteNonZeroColorCount: donorPaletteNonZeroWords,
            PaletteByteStarts: targetPaletteStarts.OrderBy(value => value).Select(value => $"0x{value:X}").ToArray());
    }

    private static bool TryAddTemporaryCustomTexturePatch(
        FileStream imageStream,
        DiscLayout layout,
        Dictionary<long, TerrainPatch> importPatchesByWadOffset,
        long wadOffset,
        byte[] expectedBefore,
        byte[] after,
        string kind,
        string runtimeKey,
        string description,
        out string conflictDescription)
    {
        if (importPatchesByWadOffset.TryGetValue(wadOffset, out TerrainPatch? existing))
        {
            string afterHex = ToHex(after);
            if (!string.Equals(existing.AfterHexPreview, afterHex, StringComparison.OrdinalIgnoreCase))
            {
                conflictDescription = $"conflicts with another requested descriptor tier at WAD offset 0x{wadOffset:X}";
                return false;
            }

            conflictDescription = "";
            return true;
        }

        AddPatch(
            imageStream,
            layout,
            importPatchesByWadOffset,
            wadOffset,
            expectedBefore,
            after,
            kind,
            runtimeKey,
            description);
        conflictDescription = "";
        return true;
    }

    private static bool CanPatchTextureDescriptor(
        TextureDescriptor descriptor,
        int tileSide,
        long texturePagesSubfileSize) =>
        CanDecodeHqTextureImageDescriptor(descriptor, tileSide, texturePagesSubfileSize);

    private static (string Tier, int TileSide, int TileGridColumns, int ImageSize, IReadOnlyList<TextureDescriptor> Descriptors)? ChooseReadableTextureDescriptorTier(
        TextureRecord record,
        long texturePagesSubfileSize,
        string preferredDescriptorTier = "")
    {
        (string Tier, int TileSide, int TileGridColumns, int ImageSize, IReadOnlyList<TextureDescriptor> Descriptors)? normal =
            TryBuildReadableTextureImageTier("hqData", record.HqData, 2, texturePagesSubfileSize);
        (string Tier, int TileSide, int TileGridColumns, int ImageSize, IReadOnlyList<TextureDescriptor> Descriptors)? close =
            TryBuildReadableTextureImageTier("hqDataClose", record.HqDataClose, 4, texturePagesSubfileSize);

        if (string.Equals(preferredDescriptorTier, "hqDataClose", StringComparison.OrdinalIgnoreCase))
            return close ?? normal;
        if (string.Equals(preferredDescriptorTier, "hqData", StringComparison.OrdinalIgnoreCase))
            return normal ?? close;
        return normal ?? close;
    }

    private static (string Tier, int TileSide, int TileGridColumns, int ImageSize, IReadOnlyList<TextureDescriptor> Descriptors)? TryBuildReadableTextureImageTier(
        string tier,
        IReadOnlyList<TextureDescriptor> descriptors,
        int tileGridColumns,
        long texturePagesSubfileSize)
    {
        if (descriptors.Count != tileGridColumns * tileGridColumns)
            return null;

        int[] tileSides = descriptors
            .Select(DeriveHqTextureTileSide)
            .Distinct()
            .ToArray();
        if (tileSides.Length != 1 || tileSides[0] is not (16 or 32))
            return null;
        if (descriptors.Select(descriptor => descriptor.BitsPerPixel).Distinct().Count() != 1 ||
            descriptors[0].BitsPerPixel is not (4 or 8))
        {
            return null;
        }

        int tileSide = tileSides[0];
        if (descriptors.Any(descriptor => !CanDecodeHqTextureImageDescriptor(descriptor, tileSide, texturePagesSubfileSize)))
            return null;

        return (tier, tileSide, tileGridColumns, checked(tileSide * tileGridColumns), descriptors);
    }

    private static int DeriveHqTextureTileSide(TextureDescriptor descriptor) =>
        Math.Max(
            Math.Abs(descriptor.VramXMax - descriptor.VramXMin),
            Math.Abs(descriptor.VramYMax - descriptor.VramYMin)) + 1;

    private static int CountReadableHqTextureImageDescriptors(
        IReadOnlyList<TextureDescriptor> descriptors,
        long texturePagesSubfileSize)
    {
        return descriptors.Count(descriptor =>
        {
            int tileSide = DeriveHqTextureTileSide(descriptor);
            return tileSide is 16 or 32 &&
                CanDecodeHqTextureImageDescriptor(descriptor, tileSide, texturePagesSubfileSize);
        });
    }

    private static bool CanDecodeHqTextureImageDescriptor(
        TextureDescriptor descriptor,
        int tileSide,
        long texturePagesSubfileSize)
    {
        int paletteByteLength = descriptor.BitsPerPixel switch
        {
            4 => HqFourBitPaletteByteLength,
            8 => HqEightBitPaletteByteLength,
            _ => 0
        };
        if (descriptor.PaletteByteStart < 0 ||
            paletteByteLength == 0 ||
            descriptor.PaletteByteStart + paletteByteLength > texturePagesSubfileSize)
        {
            return false;
        }

        int edge = tileSide - 1;
        return TryGetHqTextureImageSampleAddress(descriptor, 0, 0, tileSide, texturePagesSubfileSize, out _, out _) &&
            TryGetHqTextureImageSampleAddress(descriptor, edge, 0, tileSide, texturePagesSubfileSize, out _, out _) &&
            TryGetHqTextureImageSampleAddress(descriptor, 0, edge, tileSide, texturePagesSubfileSize, out _, out _) &&
            TryGetHqTextureImageSampleAddress(descriptor, edge, edge, tileSide, texturePagesSubfileSize, out _, out _);
    }

    private static bool TryGetHqTextureImageSampleAddress(
        TextureDescriptor descriptor,
        int x,
        int y,
        int tileSide,
        long texturePagesSubfileSize,
        out long relativeOffset,
        out int nibble)
    {
        if (tileSide <= 0 || x < 0 || x >= tileSide || y < 0 || y >= tileSide ||
            descriptor.BitsPerPixel is not (4 or 8))
        {
            relativeOffset = -1;
            nibble = -1;
            return false;
        }

        int[] matrix = TextureDescriptorMatrices[Math.Clamp(descriptor.Orientation, 0, TextureDescriptorMatrices.Length - 1)];
        int xx = matrix[0];
        int xy = matrix[1];
        int yx = matrix[2];
        int yy = matrix[3];
        int edge = tileSide - 1;
        int startY = descriptor.VramYMin + ((yx < 0 || yy < 0) ? edge : 0);
        int sampleY = startY + (x * yx) + (y * yy);
        int startLocalPixelX = descriptor.LocalPixelXMin + ((xx < 0 || xy < 0) ? edge : 0);
        int sampleLocalPixelX = startLocalPixelX + (x * xx) + (y * xy);
        int pixelsPerPackedByte = 8 / descriptor.BitsPerPixel;
        int samplePackedX =
            descriptor.PagePackedByteX +
            (sampleLocalPixelX / pixelsPerPackedByte) -
            FullVramTextureByteX;
        nibble = descriptor.BitsPerPixel == 4 ? sampleLocalPixelX & 1 : 0;
        relativeOffset = (sampleY * (long)PackedTexturePageRowBytes) + samplePackedX;
        return samplePackedX >= 0 && samplePackedX < PackedTexturePageRowBytes &&
            sampleLocalPixelX >= 0 &&
            sampleY >= 0 && sampleY < TexturePageMaxRows &&
            relativeOffset >= 0 && relativeOffset < texturePagesSubfileSize;
    }

    private static string BuildDescriptorTopologySignature(
        IReadOnlyList<TextureDescriptor> descriptors,
        bool closeTier,
        long texturePagesSubfileSize)
    {
        int tileGridColumns = closeTier ? 4 : 2;
        var readableTier = TryBuildReadableTextureImageTier(
            closeTier ? "hqDataClose" : "hqData",
            descriptors,
            tileGridColumns,
            texturePagesSubfileSize);
        if (readableTier == null)
            return "";

        int tileSide = readableTier.Value.TileSide;
        int bitsPerPixel = descriptors[0].BitsPerPixel;
        int referencesPerPixel = bitsPerPixel == 8 ? 2 : 1;
        byte[] canonical = new byte[checked(descriptors.Count * tileSide * tileSide * referencesPerPixel * sizeof(int))];
        Dictionary<(long RelativeOffset, int Nibble), int> classes = new();
        int nextClass = 0;
        int outputOffset = 0;
        foreach (TextureDescriptor descriptor in descriptors.OrderBy(descriptor => descriptor.Index))
        {
            for (int y = 0; y < tileSide; y++)
            {
                for (int x = 0; x < tileSide; x++)
                {
                    if (!TryGetHqTextureImageSampleAddress(
                            descriptor,
                            x,
                            y,
                            tileSide,
                            texturePagesSubfileSize,
                            out long relative,
                            out int nibble))
                    {
                        return "";
                    }

                    int firstNibble = bitsPerPixel == 8 ? 0 : nibble;
                    int lastNibble = bitsPerPixel == 8 ? 1 : nibble;
                    for (int physicalNibble = firstNibble; physicalNibble <= lastNibble; physicalNibble++)
                    {
                        var key = (relative, physicalNibble);
                        if (!classes.TryGetValue(key, out int classId))
                        {
                            classId = nextClass++;
                            classes[key] = classId;
                        }

                        BinaryPrimitives.WriteInt32LittleEndian(canonical.AsSpan(outputOffset, sizeof(int)), classId);
                        outputOffset += sizeof(int);
                    }
                }
            }
        }

        byte[] hash = SHA256.HashData(canonical);
        return $"{bitsPerPixel}bpp-{descriptors.Count}x{tileSide}-{Convert.ToHexString(hash.AsSpan(0, 12))}";
    }

    private static string BuildReadableHqCombinedTopologySignature(
        TextureRecord record,
        long texturePagesSubfileSize,
        out string failureReason)
    {
        List<(string Tier, IReadOnlyList<TextureDescriptor> Descriptors, int GridColumns)> tiers =
        [
            ("hqData", record.HqData, 2),
            ("hqDataClose", record.HqDataClose, 4)
        ];
        List<(long RelativeOffset, int Nibble)> references = [];
        List<string> tierSummaries = [];
        foreach ((string tier, IReadOnlyList<TextureDescriptor> descriptors, int gridColumns) in tiers)
        {
            var readableTier = TryBuildReadableTextureImageTier(
                tier,
                descriptors,
                gridColumns,
                texturePagesSubfileSize);
            if (readableTier == null)
            {
                failureReason = $"requested tier {tier} is not a complete packed 4/8-bpp 16/32-pixel HQ record";
                return "";
            }

            int tileSide = readableTier.Value.TileSide;
            int bitsPerPixel = descriptors[0].BitsPerPixel;
            tierSummaries.Add($"{tier}:{descriptors.Count}x{tileSide}x{bitsPerPixel}bpp");
            foreach (TextureDescriptor descriptor in descriptors.OrderBy(candidate => candidate.Index))
            {
                int paletteByteLength = descriptor.BitsPerPixel == 4
                    ? HqFourBitPaletteByteLength
                    : HqEightBitPaletteByteLength;
                for (int paletteByte = 0; paletteByte < paletteByteLength; paletteByte++)
                {
                    long relativeOffset = descriptor.PaletteByteStart + paletteByte;
                    references.Add((relativeOffset, 0));
                    references.Add((relativeOffset, 1));
                }

                for (int y = 0; y < tileSide; y++)
                {
                    for (int x = 0; x < tileSide; x++)
                    {
                        if (!TryGetHqTextureImageSampleAddress(
                                descriptor,
                                x,
                                y,
                                tileSide,
                                texturePagesSubfileSize,
                                out long relativeOffset,
                                out int nibble))
                        {
                            failureReason = $"requested tier {tier} descriptor {descriptor.Index} has an out-of-range packed {descriptor.BitsPerPixel}-bpp pixel at ({x},{y})";
                            return "";
                        }

                        if (descriptor.BitsPerPixel == 8)
                        {
                            references.Add((relativeOffset, 0));
                            references.Add((relativeOffset, 1));
                        }
                        else
                        {
                            references.Add((relativeOffset, nibble));
                        }
                    }
                }
            }
        }

        byte[] canonical = new byte[checked(references.Count * sizeof(int))];
        Dictionary<(long RelativeOffset, int Nibble), int> classes = [];
        int nextClass = 0;
        int outputOffset = 0;
        foreach ((long relativeOffset, int nibble) in references)
        {
            var key = (relativeOffset, nibble);
            if (!classes.TryGetValue(key, out int classId))
            {
                classId = nextClass++;
                classes[key] = classId;
            }

            BinaryPrimitives.WriteInt32LittleEndian(canonical.AsSpan(outputOffset, sizeof(int)), classId);
            outputOffset += sizeof(int);
        }

        byte[] hash = SHA256.HashData(canonical);
        failureReason = "";
        return $"hq8-packed-{string.Join("+", tierSummaries)}-{references.Count}-{classes.Count}-{Convert.ToHexString(hash.AsSpan(0, 12))}";
    }

    private static string BuildCombinedDescriptorTopologySignature(
        TextureRecord record,
        IReadOnlyList<string> descriptorTiers,
        long texturePagesSubfileSize,
        out string failureReason)
    {
        if (!TryBuildTexturePhysicalNibbleLayout(
                record,
                descriptorTiers,
                texturePagesSubfileSize,
                out TexturePhysicalNibbleReference[] references,
                out string tierSummary,
                out failureReason))
        {
            return "";
        }

        byte[] canonical = new byte[checked(references.Length * sizeof(int))];
        Dictionary<(long RelativeOffset, int Nibble), int> classes = new();
        int nextClass = 0;
        int outputOffset = 0;
        foreach (TexturePhysicalNibbleReference reference in references)
        {
            var key = (reference.RelativeOffset, reference.Nibble);
            if (!classes.TryGetValue(key, out int classId))
            {
                classId = nextClass++;
                classes[key] = classId;
            }

            BinaryPrimitives.WriteInt32LittleEndian(canonical.AsSpan(outputOffset, sizeof(int)), classId);
            outputOffset += sizeof(int);
        }

        byte[] hash = SHA256.HashData(canonical);
        failureReason = "";
        return $"raw-nibble-{tierSummary}-{references.Length}-{classes.Count}-{Convert.ToHexString(hash.AsSpan(0, 12))}";
    }

    private static Dictionary<(long RelativeOffset, int Nibble), int> BuildPhysicalNibbleKindMap(
        IEnumerable<TexturePhysicalNibbleReference> references)
    {
        Dictionary<(long RelativeOffset, int Nibble), int> result = new();
        foreach (TexturePhysicalNibbleReference reference in references)
        {
            var key = (reference.RelativeOffset, reference.Nibble);
            int kind = string.Equals(reference.Kind, "palette", StringComparison.Ordinal) ? 1 : 2;
            result[key] = result.TryGetValue(key, out int existing) ? existing | kind : kind;
        }

        return result;
    }

    private static bool TryBuildTexturePhysicalNibbleLayout(
        TextureRecord record,
        IReadOnlyList<string> descriptorTiers,
        long texturePagesSubfileSize,
        out TexturePhysicalNibbleReference[] references,
        out string tierSummary,
        out string failureReason)
    {
        bool includeNormal = descriptorTiers.Any(tier =>
            string.Equals(tier, "both", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tier, "hqData", StringComparison.OrdinalIgnoreCase));
        bool includeClose = descriptorTiers.Any(tier =>
            string.Equals(tier, "both", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tier, "hqDataClose", StringComparison.OrdinalIgnoreCase));
        List<(string Tier, int GridColumns, IReadOnlyList<TextureDescriptor> Descriptors)> tiers = [];
        if (includeNormal)
            tiers.Add(("hqData", 2, record.HqData));
        if (includeClose)
            tiers.Add(("hqDataClose", 4, record.HqDataClose));

        tierSummary = string.Join("+", tiers.Select(tier => $"{tier.Tier}:{tier.Descriptors.Count}"));
        if (tiers.Count == 0)
        {
            references = Array.Empty<TexturePhysicalNibbleReference>();
            failureReason = "no descriptor tier was requested";
            return false;
        }

        List<TexturePhysicalNibbleReference> result = [];
        List<string> tierSummaries = [];
        foreach ((string tier, int gridColumns, IReadOnlyList<TextureDescriptor> descriptors) in tiers)
        {
            if (descriptors.Count == 0)
            {
                references = Array.Empty<TexturePhysicalNibbleReference>();
                failureReason = $"requested tier {tier} has no descriptors";
                return false;
            }

            var readableTier = TryBuildReadableTextureImageTier(
                tier,
                descriptors,
                gridColumns,
                texturePagesSubfileSize);
            if (readableTier == null)
            {
                references = Array.Empty<TexturePhysicalNibbleReference>();
                failureReason =
                    $"requested tier {tier} is not a complete packed 4/8-bpp native texture record";
                return false;
            }

            int tileSide = readableTier.Value.TileSide;
            int bitsPerPixel = descriptors[0].BitsPerPixel;
            int paletteByteLength = bitsPerPixel == 4
                ? HqFourBitPaletteByteLength
                : HqEightBitPaletteByteLength;
            tierSummaries.Add($"{tier}:{descriptors.Count}x{tileSide}x{bitsPerPixel}bpp");
            foreach (TextureDescriptor descriptor in descriptors.OrderBy(candidate => candidate.Index))
            {
                if (!CanPatchTextureDescriptor(descriptor, tileSide, texturePagesSubfileSize))
                {
                    references = Array.Empty<TexturePhysicalNibbleReference>();
                    failureReason = $"requested tier {tier} descriptor {descriptor.Index} maps outside the texture-pages subfile";
                    return false;
                }

                for (int paletteByte = 0; paletteByte < paletteByteLength; paletteByte++)
                {
                    long relativeOffset = descriptor.PaletteByteStart + paletteByte;
                    result.Add(new TexturePhysicalNibbleReference(tier, "palette", descriptor.Index, relativeOffset, 0));
                    result.Add(new TexturePhysicalNibbleReference(tier, "palette", descriptor.Index, relativeOffset, 1));
                }

                for (int y = 0; y < tileSide; y++)
                {
                    for (int x = 0; x < tileSide; x++)
                    {
                        if (!TryGetHqTextureImageSampleAddress(
                                descriptor,
                                x,
                                y,
                                tileSide,
                                texturePagesSubfileSize,
                                out long relativeOffset,
                                out int nibble))
                        {
                            references = Array.Empty<TexturePhysicalNibbleReference>();
                            failureReason = $"requested tier {tier} descriptor {descriptor.Index} has an out-of-range pixel at ({x},{y})";
                            return false;
                        }

                        if (bitsPerPixel == 8)
                        {
                            result.Add(new TexturePhysicalNibbleReference(tier, "pixel", descriptor.Index, relativeOffset, 0));
                            result.Add(new TexturePhysicalNibbleReference(tier, "pixel", descriptor.Index, relativeOffset, 1));
                        }
                        else
                        {
                            result.Add(new TexturePhysicalNibbleReference(tier, "pixel", descriptor.Index, relativeOffset, nibble));
                        }
                    }
                }
            }
        }

        tierSummary = string.Join("+", tierSummaries);
        references = result.ToArray();
        failureReason = "";
        return true;
    }

    private static Rgba32 ConvertPsx555ToRgba32(ushort word)
    {
        byte r = ExpandPsx5ToByte(word & 0x1F);
        byte g = ExpandPsx5ToByte((word >> 5) & 0x1F);
        byte b = ExpandPsx5ToByte((word >> 10) & 0x1F);
        // On the PSX, only an all-zero texture/CLUT word is intrinsically transparent.
        // Bit 15 (STP) requests semi-transparency only when the drawing primitive also
        // enables it; 0x8000 is therefore opaque black, not transparent or half-alpha.
        return new Rgba32(r, g, b, word == 0 ? (byte)0 : (byte)255);
    }

    private static byte ExpandPsx5ToByte(int value)
    {
        value = Math.Clamp(value, 0, 31);
        return (byte)((value << 3) | (value >> 2));
    }

    private static IReadOnlyList<string> ExpandDescriptorTiers(string descriptorTier)
    {
        if (string.Equals(descriptorTier, "both", StringComparison.OrdinalIgnoreCase))
            return ["hqData", "hqDataClose"];

        return string.Equals(descriptorTier, "hqDataClose", StringComparison.OrdinalIgnoreCase)
            ? ["hqDataClose"]
            : ["hqData"];
    }

    private static string ResolveManifestRelativePath(string manifestPath, string sourceImagePath)
    {
        return Path.IsPathRooted(sourceImagePath)
            ? sourceImagePath
            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath) ?? ".", sourceImagePath));
    }

    private static AssetSubfileInfo GetAssetSubfileInfo(FileStream imageStream, DiscLayout layout, int assetWadIndex, int subfileIndex)
    {
        byte[] wadHeader = ReadWadBytes(imageStream, layout, 0, 4096);
        ArchiveEntry assetEntry = ParseArchiveHeader(wadHeader, 200_000_000)
            .FirstOrDefault(entry => entry.Index == assetWadIndex)
            ?? throw new InvalidOperationException($"Could not find WAD entry {assetWadIndex}.");

        byte[] assetHeader = ReadWadBytes(imageStream, layout, assetEntry.Offset, 4096);
        ArchiveEntry subfile = ParseArchiveHeader(assetHeader, assetEntry.Size)
            .FirstOrDefault(entry => entry.Index == subfileIndex)
            ?? throw new InvalidOperationException($"Could not find asset subfile {subfileIndex} in WAD entry {assetWadIndex}.");

        return new AssetSubfileInfo(assetWadIndex, subfileIndex, assetEntry.Offset, assetEntry.Size, subfile.Offset, subfile.Size, assetEntry.Offset + subfile.Offset);
    }

    private static IReadOnlyList<ArchiveEntry> ParseArchiveHeader(byte[] bytes, long archiveSize)
    {
        List<ArchiveEntry> entries = new();
        long firstDataOffset = ReadUInt32(bytes, 0);
        if (firstDataOffset <= 0 || firstDataOffset > bytes.Length)
            firstDataOffset = bytes.Length;

        for (int offset = 0; offset <= Math.Min(bytes.Length, firstDataOffset) - 8; offset += 8)
        {
            long fileOffset = ReadUInt32(bytes, offset);
            long fileSize = ReadUInt32(bytes, offset + 4);
            if (fileOffset == 0 && fileSize == 0)
                continue;
            if (fileOffset < 0 || fileSize <= 0 || fileOffset + fileSize > archiveSize)
                continue;
            entries.Add(new ArchiveEntry(offset / 8, fileOffset, fileSize));
        }

        return entries;
    }

    private static TextureRecordIndex DecodeTextureRecords(byte[] modelBytes)
    {
        const int lowDetailRecordBytes = 16;
        const int highDetailRecordBytes = 168;
        int textureListSize = (int)ReadUInt32(modelBytes, 0);
        int textureCount = (int)ReadUInt32(modelBytes, 4);
        if (textureListSize <= 8 || textureCount <= 0)
            throw new InvalidOperationException("No plausible texture list found in model subfile.");

        int recordBytes = (textureListSize - 8) / textureCount;
        int expectedTextureListSize = checked(8 + (textureCount * (lowDetailRecordBytes + highDetailRecordBytes)));
        if (recordBytes != lowDetailRecordBytes + highDetailRecordBytes ||
            textureListSize < expectedTextureListSize ||
            expectedTextureListSize > modelBytes.Length)
        {
            throw new InvalidOperationException(
                $"Texture list does not contain the native {lowDetailRecordBytes}-byte LQ table followed by the {highDetailRecordBytes}-byte HQ table ({textureCount} records, 0x{textureListSize:X} bytes).");
        }

        // The retail loader does not interleave LQ and HQ records.  It first
        // reads textureCount 16-byte LQ rows, then indexes a separate array of
        // 168-byte HQ rows as textureId * 0xA8.
        int highDetailTableOffset = checked(8 + (textureCount * lowDetailRecordBytes));

        List<TextureRecord> records = new(textureCount);
        for (int texture = 0; texture < textureCount; texture++)
        {
            int offset = checked(highDetailTableOffset + (texture * highDetailRecordBytes));
            List<TextureDescriptor> hq = new();
            for (int i = 0; i < 4; i++)
                hq.Add(DecodeTextureDescriptor(modelBytes, offset + 8 + (i * 8), i));

            List<TextureDescriptor> hqClose = new();
            for (int i = 0; i < 16; i++)
                hqClose.Add(DecodeTextureDescriptor(modelBytes, offset + 40 + (i * 8), i));

            records.Add(new TextureRecord(texture, hq, hqClose));
        }

        return new TextureRecordIndex(textureListSize, textureCount, recordBytes, records);
    }

    private static TextureDescriptor DecodeTextureDescriptor(byte[] bytes, int offset, int index)
    {
        int xmin = bytes[offset];
        int ymin = bytes[offset + 1];
        int palette = ReadUInt16(bytes, offset + 2);
        int xmax = bytes[offset + 4];
        int ymax = bytes[offset + 5];
        int region = bytes[offset + 6];
        int unknown = bytes[offset + 7];
        int bitsPerPixel = (region & 0x80) != 0 ? 8 : 4;
        return new TextureDescriptor(
            Index: index,
            PaletteByteStart: DecodePackedClutByteStart(palette),
            Orientation: (unknown >> 4) & 7,
            BitsPerPixel: bitsPerPixel,
            PagePackedByteX: (region & 0x0F) * 128,
            LocalPixelXMin: xmin,
            LocalPixelXMax: xmax,
            VramXMin: GetTextureXMin(region, xmin),
            VramYMin: GetTextureYMin(region, ymin),
            VramXMax: GetTextureXMin(region, xmax),
            VramYMax: GetTextureYMin(region, ymax));
    }

    private static int GetTextureXMin(int region, int xmin) => ((region * 128) % 2048) + xmin;

    private static int GetTextureYMin(int region, int ymin) => ((int)Math.Floor((region & 0x1F) / 16.0) * 256) + ymin;

    private static int DecodePackedClutByteStart(int clutCode)
    {
        int clutX = (clutCode & 0x3F) * 16;
        int clutY = (clutCode >> 6) & 0x1FF;
        if (clutX < 512)
            return -1;

        return checked((clutY * 1024) + ((clutX - 512) * 2));
    }

    private static PaletteInfo BuildPalette(IReadOnlyList<Rgba32> pixels, int colorCount = 256)
    {
        colorCount = Math.Clamp(colorCount, 2, 256);
        Dictionary<ushort, int> counts = new();
        foreach (Rgba32 pixel in pixels)
        {
            ushort word = ConvertColorToPsx555(pixel);
            counts[word] = counts.TryGetValue(word, out int count) ? count + 1 : 1;
        }

        ushort[] words = counts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .Select(pair => pair.Key)
            .Take(colorCount)
            .ToArray();
        Array.Resize(ref words, colorCount);
        Dictionary<ushort, int> exact = new();
        for (int i = 0; i < words.Length; i++)
            exact.TryAdd(words[i], i);
        return new PaletteInfo(words, exact, new Dictionary<ushort, int>());
    }

    private static ushort ConvertColorToPsx555(Rgba32 color)
    {
        int r = Math.Clamp((int)Math.Round(color.R * 31.0 / 255.0), 0, 31);
        int g = Math.Clamp((int)Math.Round(color.G * 31.0 / 255.0), 0, 31);
        int b = Math.Clamp((int)Math.Round(color.B * 31.0 / 255.0), 0, 31);
        return (ushort)((b << 10) | (g << 5) | r);
    }

    private static int GetNearestPaletteIndex(ushort word, PaletteInfo palette)
    {
        if (palette.ExactMap.TryGetValue(word, out int exact))
            return exact;
        if (palette.NearestCache.TryGetValue(word, out int cached))
            return cached;

        int bestIndex = 0;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < palette.Words.Length; i++)
        {
            int distance = Psx555Distance(word, palette.Words[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
                if (distance == 0)
                    break;
            }
        }

        palette.NearestCache[word] = bestIndex;
        return bestIndex;
    }

    private static int Psx555Distance(ushort a, ushort b)
    {
        int ar = a & 31;
        int ag = (a >> 5) & 31;
        int ab = (a >> 10) & 31;
        int br = b & 31;
        int bg = (b >> 5) & 31;
        int bb = (b >> 10) & 31;
        int dr = ar - br;
        int dg = ag - bg;
        int db = ab - bb;
        return (dr * dr) + (dg * dg) + (db * db);
    }

    private static void AddPatch(
        FileStream imageStream,
        DiscLayout layout,
        Dictionary<long, TerrainPatch> patchesByWadOffset,
        long wadOffset,
        byte[] expectedBefore,
        byte[] after,
        string kind,
        string runtimeKey,
        string description)
    {
        byte[] imageBefore = ReadWadBytes(imageStream, layout, wadOffset, after.Length);
        if (!imageBefore.SequenceEqual(expectedBefore))
            throw new InvalidOperationException($"Source terrain bytes at 0x{wadOffset:X} are {ToHex(imageBefore)}, expected {ToHex(expectedBefore)}.");

        TerrainPatch patch = new(
            Label: $"{runtimeKey}-{kind}-0x{wadOffset:X}",
            Kind: kind,
            RuntimeKey: runtimeKey,
            WadRelativeOffset: $"0x{wadOffset:X}",
            ImageOffset: $"0x{ConvertWadOffsetToImageOffset(layout, wadOffset):X}",
            ByteLength: after.Length,
            BeforeHexPreview: ToHex(expectedBefore),
            AfterHexPreview: ToHex(after),
            Description: description);
        if (patchesByWadOffset.TryGetValue(wadOffset, out TerrainPatch? existing))
        {
            if (!string.Equals(existing.AfterHexPreview, patch.AfterHexPreview, StringComparison.OrdinalIgnoreCase))
            {
                if (kind.StartsWith("custom-texture-", StringComparison.OrdinalIgnoreCase)
                    || existing.Kind.StartsWith("custom-texture-", StringComparison.OrdinalIgnoreCase))
                    return;

                throw new InvalidOperationException($"Conflicting terrain patches target WAD offset 0x{wadOffset:X}.");
            }
            return;
        }

        patchesByWadOffset[wadOffset] = patch;
    }

    private static SceneSectorHeader ReadSceneSectorHeader(byte[] ram, int offset)
    {
        if (offset < 0 || offset + 28 > ram.Length)
            throw new InvalidOperationException($"Runtime sector 0x{offset:X} is outside the RAM dump.");

        int numLpVertices = ram[offset + 16];
        int numLpColours = ram[offset + 17];
        int numLpFaces = ram[offset + 18];
        int numHpVertices = ram[offset + 20];
        int numHpColours = ram[offset + 21];
        int numHpFaces = ram[offset + 22];
        int sizeWords = 7 + numLpVertices + numLpColours + (numLpFaces * 2) + numHpVertices + (numHpColours * 2) + (numHpFaces * 4);
        int sizeBytes = sizeWords * 4;
        if (sizeBytes < 28 || sizeBytes > 0x40000 || offset + sizeBytes > ram.Length)
            throw new InvalidOperationException($"Runtime sector 0x{offset:X} no longer parses.");

        return new SceneSectorHeader(
            Offset: offset,
            SourceBytes: ram,
            CentreRadiusAndFlags: ReadUInt16(ram, offset + 4),
            XyPos: ReadUInt32(ram, offset + 8),
            ZPos: ReadUInt32(ram, offset + 12),
            SizeBytes: sizeBytes,
            NumLpVertices: numLpVertices,
            NumLpColours: numLpColours,
            NumLpFaces: numLpFaces,
            NumHpVertices: numHpVertices,
            NumHpColours: numHpColours,
            NumHpFaces: numHpFaces);
    }

    private static int GetSceneVertexOffset(SceneSectorHeader sector, string detail, int vertexIndex)
    {
        int dataStart = sector.Offset + 28;
        if (string.Equals(detail, "lp", StringComparison.OrdinalIgnoreCase))
            return vertexIndex >= 0 && vertexIndex < sector.NumLpVertices ? dataStart + (vertexIndex * 4) : -1;
        if (string.Equals(detail, "hp", StringComparison.OrdinalIgnoreCase))
        {
            if (vertexIndex < 0 || vertexIndex >= sector.NumHpVertices)
                return -1;
            int hpVertexStartWords = sector.NumLpVertices + sector.NumLpColours + (sector.NumLpFaces * 2);
            return dataStart + ((hpVertexStartWords + vertexIndex) * 4);
        }

        return -1;
    }

    private static uint SetSceneVertexZWord(uint word, SceneSectorHeader sector, float targetZ)
    {
        int encodedZ = EncodeSceneVertexZ(sector, targetZ);

        return (word & 0xFFFFFC00u) | (uint)(encodedZ & 0x3FF);
    }

    private static uint SetSceneVertexWord(uint word, SceneSectorHeader sector, Vector2f targetPoint, float targetZ)
    {
        int sectorX = (int)((sector.XyPos >> 16) & 0xFFFF);
        int sectorY = (int)(sector.XyPos & 0xFFFF);
        int encodedX = (int)Math.Round(targetPoint.X - sectorX);
        int encodedY = (int)Math.Round(targetPoint.Y - sectorY);
        int encodedZ = EncodeSceneVertexZ(sector, targetZ);
        if (encodedX < 0 || encodedX > 2047)
            throw new InvalidOperationException($"Target X {targetPoint.X:0.0} cannot be encoded in sector 0x{sector.Offset:X}.");
        if (encodedY < 0 || encodedY > 2047)
            throw new InvalidOperationException($"Target Y {targetPoint.Y:0.0} cannot be encoded in sector 0x{sector.Offset:X}.");

        return ((uint)encodedX << 21) | ((uint)encodedY << 10) | (uint)(encodedZ & 0x3FF);
    }

    private static int EncodeSceneVertexZ(SceneSectorHeader sector, float targetZ)
    {
        int baseZ = GetSceneSectorBaseZ(sector);
        int encodedZ = IsFlatSceneSector(sector)
            ? (int)Math.Round((targetZ * 8.0f) - baseZ)
            : (int)Math.Round(targetZ - baseZ);
        if (encodedZ < 0 || encodedZ > 1023)
            throw new InvalidOperationException($"Target Z {targetZ:0.0} cannot be encoded in sector 0x{sector.Offset:X}.");

        return encodedZ;
    }

    private static int GetSceneSectorBaseZ(SceneSectorHeader sector)
    {
        int sectorZ = (int)((sector.ZPos >> 14) & 0xFFFF);
        return sectorZ >> 2;
    }

    private static bool IsFlatSceneSector(SceneSectorHeader sector) => ((sector.CentreRadiusAndFlags >> 12) & 1) == 1;

    private static byte[] ReadLogicalWad(FileStream stream, DiscLayout layout)
    {
        int wadSize = (int)Math.Max(0, Math.Min(110260224L, 2048L * Math.Max(0, (stream.Length / layout.SectorSize) - WadLba)));
        byte[] wad = new byte[wadSize];
        int remaining = wad.Length;
        int written = 0;
        int lba = WadLba;
        while (remaining > 0)
        {
            int toRead = Math.Min(2048, remaining);
            stream.Position = ((long)lba * layout.SectorSize) + layout.UserOffset;
            int read = stream.Read(wad, written, toRead);
            if (read != toRead)
                throw new EndOfStreamException("Could not read logical WAD bytes.");

            written += read;
            remaining -= read;
            lba++;
        }

        return wad;
    }

    private static byte[] ReadWadBytes(FileStream stream, DiscLayout layout, long wadOffset, int length)
    {
        byte[] result = new byte[length];
        int remaining = length;
        int written = 0;
        long absolute = wadOffset;
        while (remaining > 0)
        {
            int sectorOffset = (int)(absolute % 2048);
            int toRead = Math.Min(2048 - sectorOffset, remaining);
            stream.Position = ConvertWadOffsetToImageOffset(layout, absolute);
            int read = stream.Read(result, written, toRead);
            if (read != toRead)
                throw new EndOfStreamException("Could not read WAD bytes.");

            written += toRead;
            remaining -= toRead;
            absolute += toRead;
        }

        return result;
    }

    private static long ConvertWadOffsetToImageOffset(DiscLayout layout, long wadOffset)
    {
        long sector = WadLba + (long)Math.Floor(wadOffset / 2048d);
        long sectorOffset = wadOffset % 2048;
        return (sector * layout.SectorSize) + layout.UserOffset + sectorOffset;
    }

    private static IReadOnlyList<int> ReadIntArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return Array.Empty<int>();

        List<int> result = new();
        foreach (JsonElement value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
                result.Add(number);
            else if (int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                result.Add(parsed);
        }

        return result;
    }

    private static IReadOnlyList<int> NormalizeVertexIndexes(IReadOnlyList<int> indexes, int zValueCount)
    {
        if (indexes.Count <= zValueCount)
            return indexes;

        List<int> result = new();
        HashSet<int> seen = new();
        foreach (int index in indexes)
        {
            if (!seen.Add(index))
                continue;

            result.Add(index);
            if (result.Count == zValueCount)
                break;
        }

        return result.Count == zValueCount ? result : indexes.Take(zValueCount).ToArray();
    }

    private static IReadOnlyList<int> ResolveEditableVertexIndexes(
        SceneSectorHeader sector,
        string detail,
        IReadOnlyList<int> rawIndexes,
        int valueCount,
        IReadOnlyList<Vector2f> originalPoints,
        IReadOnlyList<float> originalZ)
    {
        if (valueCount <= 0)
            valueCount = rawIndexes.Count;

        IReadOnlyList<int> normalized = NormalizeVertexIndexes(rawIndexes, valueCount);
        if (valueCount <= 0 || normalized.Count == 0)
            return normalized;

        if (VertexIndexesMatchOriginal(sector, detail, normalized, valueCount, originalPoints, originalZ))
            return normalized;

        IReadOnlyList<int>? resolvedFromRaw = TryResolveVertexIndexesFromCandidates(
            sector,
            detail,
            rawIndexes.Distinct(),
            valueCount,
            originalPoints,
            originalZ);
        if (resolvedFromRaw != null)
            return resolvedFromRaw;

        int sectorVertexCount = GetSceneVertexCount(sector, detail);
        if (sectorVertexCount > 0)
        {
            IReadOnlyList<int>? resolvedFromSector = TryResolveVertexIndexesFromCandidates(
                sector,
                detail,
                Enumerable.Range(0, sectorVertexCount),
                valueCount,
                originalPoints,
                originalZ);
            if (resolvedFromSector != null)
                return resolvedFromSector;
        }

        return normalized;
    }

    private static bool VertexIndexesMatchOriginal(
        SceneSectorHeader sector,
        string detail,
        IReadOnlyList<int> indexes,
        int valueCount,
        IReadOnlyList<Vector2f> originalPoints,
        IReadOnlyList<float> originalZ)
    {
        if (indexes.Count < valueCount)
            return false;

        for (int i = 0; i < valueCount; i++)
        {
            int vertexOffset = GetSceneVertexOffset(sector, detail, indexes[i]);
            if (vertexOffset < 0)
                return false;

            Vector3f vertex = DecodeSceneVertex(ReadUInt32FromSector(sector, vertexOffset), sector);
            if (!VertexMatchesOriginal(vertex, i, originalPoints, originalZ))
                return false;
        }

        return true;
    }

    private static IReadOnlyList<int>? TryResolveVertexIndexesFromCandidates(
        SceneSectorHeader sector,
        string detail,
        IEnumerable<int> candidates,
        int valueCount,
        IReadOnlyList<Vector2f> originalPoints,
        IReadOnlyList<float> originalZ)
    {
        int[] candidateArray = candidates.ToArray();
        List<int> resolved = new(valueCount);
        HashSet<int> used = new();
        for (int pointIndex = 0; pointIndex < valueCount; pointIndex++)
        {
            bool matched = false;
            foreach (int candidate in candidateArray)
            {
                if (used.Contains(candidate))
                    continue;

                int vertexOffset = GetSceneVertexOffset(sector, detail, candidate);
                if (vertexOffset < 0)
                    continue;

                Vector3f vertex = DecodeSceneVertex(ReadUInt32FromSector(sector, vertexOffset), sector);
                if (!VertexMatchesOriginal(vertex, pointIndex, originalPoints, originalZ))
                    continue;

                resolved.Add(candidate);
                used.Add(candidate);
                matched = true;
                break;
            }

            if (!matched)
                return null;
        }

        return resolved;
    }

    private static uint ReadUInt32FromSector(SceneSectorHeader sector, int absoluteOffset)
    {
        return ReadUInt32(sector.SourceBytes, absoluteOffset);
    }

    private static bool VertexMatchesOriginal(Vector3f vertex, int index, IReadOnlyList<Vector2f> originalPoints, IReadOnlyList<float> originalZ)
    {
        bool hasPoint = index < originalPoints.Count;
        bool hasZ = index < originalZ.Count;
        if (!hasPoint && !hasZ)
            return false;

        if (hasPoint)
        {
            Vector2f point = originalPoints[index];
            if (Math.Abs(vertex.X - point.X) > 1.0f || Math.Abs(vertex.Y - point.Y) > 1.0f)
                return false;
        }

        return !hasZ || Math.Abs(vertex.Z - originalZ[index]) <= 1.0f;
    }

    private static int GetSceneVertexCount(SceneSectorHeader sector, string detail)
    {
        if (string.Equals(detail, "lp", StringComparison.OrdinalIgnoreCase))
            return sector.NumLpVertices;
        if (string.Equals(detail, "hp", StringComparison.OrdinalIgnoreCase))
            return sector.NumHpVertices;

        return 0;
    }

    private static int FindReusableSideWallVertexIndex(
        SceneSectorHeader sector,
        string detail,
        Vector3f point,
        IReadOnlySet<int> excludedIndexes)
    {
        int vertexCount = GetSceneVertexCount(sector, detail);
        for (int index = 0; index < vertexCount; index++)
        {
            if (excludedIndexes.Contains(index))
                continue;

            int vertexOffset = GetSceneVertexOffset(sector, detail, index);
            if (vertexOffset < 0)
                continue;

            Vector3f candidate = DecodeSceneVertex(ReadUInt32FromSector(sector, vertexOffset), sector);
            if (Math.Abs(candidate.X - point.X) <= 1.0f &&
                Math.Abs(candidate.Y - point.Y) <= 1.0f &&
                Math.Abs(candidate.Z - point.Z) <= 1.0f)
            {
                return index;
            }
        }

        return -1;
    }

    private static IReadOnlyList<float> ReadFloatArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return Array.Empty<float>();

        List<float> result = new();
        foreach (JsonElement value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out float number))
                result.Add(number);
            else if (float.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                result.Add(parsed);
        }

        return result;
    }

    private static IReadOnlyList<Vector2f> ReadVector2Array(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return Array.Empty<Vector2f>();

        List<Vector2f> result = new();
        foreach (JsonElement value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                result.Add(new Vector2f(JsonValue.GetSingle(value, "x"), JsonValue.GetSingle(value, "y")));
                continue;
            }

            if (value.ValueKind != JsonValueKind.Array)
                continue;

            List<float> parts = new(2);
            foreach (JsonElement part in value.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.Number && part.TryGetSingle(out float number))
                    parts.Add(number);
                else if (float.TryParse(part.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                    parts.Add(parsed);

                if (parts.Count == 2)
                    break;
            }

            if (parts.Count == 2)
                result.Add(new Vector2f(parts[0], parts[1]));
        }

        return result;
    }

    private static ushort ReadUInt16(byte[] bytes, int offset) => BitConverter.ToUInt16(bytes, offset);

    private static uint ReadUInt32(byte[] bytes, int offset) => BitConverter.ToUInt32(bytes, offset);

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)(value & 0xFF);
        bytes[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static long ParseRequiredLong(string text, string field)
    {
        text = (text ?? "").Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hex))
            return hex;

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
            return parsed;

        throw new InvalidOperationException($"Could not parse {field}: {text}");
    }

    private static string ToHex(byte[] bytes) => string.Join(" ", bytes.Select(value => $"{value:X2}"));

    private static byte[] HexToBytes(string text)
    {
        string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Select(part => Convert.ToByte(part, 16)).ToArray();
    }

    private sealed record SceneSectorHeader(
        int Offset,
        byte[] SourceBytes,
        int CentreRadiusAndFlags,
        uint XyPos,
        uint ZPos,
        int SizeBytes,
        int NumLpVertices,
        int NumLpColours,
        int NumLpFaces,
        int NumHpVertices,
        int NumHpColours,
        int NumHpFaces);

    private sealed record CollisionPatchContext(
        SpyroCollisionTable Table,
        long SourceTriangleWadOffset,
        long SourceBlockTreeWadOffset,
        long SourceBlocksWadOffset,
        IReadOnlyDictionary<int, long> SourceTriangleWadOffsetsByIndex,
        IReadOnlyDictionary<int, int> SourceTriangleLookupIndexByIndex,
        IReadOnlyDictionary<string, List<SpyroCollisionTriangle>> TrianglesByKey,
        IReadOnlyList<IReadOnlyList<int>> LookupGroups,
        IReadOnlyList<IReadOnlyList<CollisionLookupEntry>> LookupGroupEntries,
        IReadOnlyDictionary<int, List<int>> GroupIndexesByTriangleIndex,
        IReadOnlySet<int> DegenerateTriangleIndexes,
        IReadOnlySet<int> UnreferencedDegenerateTriangleIndexes,
        IReadOnlyDictionary<int, byte[]> SourceTriangleBytesByIndex,
        IReadOnlySet<int> SourceDegenerateTriangleIndexes,
        IReadOnlyDictionary<int, ushort> SourceLookupWordsByOffset,
        IReadOnlyDictionary<int, IReadOnlyList<int>> SourceDerivedAppendTriangleIndexesByBase,
        IReadOnlySet<int> SourceDerivedAppendTriangleIndexes,
        bool SupportsCollisionIndexRebuild,
        bool SupportsDirectLookupFallback);

    private readonly record struct CollisionWordTriple(uint XWord, uint YWord, uint ZWord);

    private sealed record CollisionLookupIndex(
        IReadOnlyList<IReadOnlyList<int>> LookupGroups,
        IReadOnlyList<IReadOnlyList<CollisionLookupEntry>> LookupGroupEntries,
        IReadOnlyDictionary<int, List<int>> GroupIndexesByTriangleIndex,
        IReadOnlySet<int> DegenerateTriangleIndexes,
        IReadOnlySet<int> UnreferencedDegenerateTriangleIndexes);

    private sealed record SourceDerivedLookupIndex(
        IReadOnlyList<IReadOnlyList<int>> LookupGroups,
        IReadOnlyList<IReadOnlyList<CollisionLookupEntry>> LookupGroupEntries,
        IReadOnlyDictionary<int, List<int>> GroupIndexesByTriangleIndex,
        IReadOnlySet<int> UnreferencedDegenerateTriangleIndexes,
        IReadOnlyDictionary<int, ushort> SourceLookupWordsByOffset,
        int LookupWordCount);

    private sealed record CollisionLookupEntry(
        int GroupIndex,
        int EntryIndex,
        int TriangleIndex,
        int Offset,
        ushort Word);

    internal sealed record CollisionIndexBytes(
        byte[] TreeBytes,
        byte[] BlockBytes);

    private sealed record SourceSectorLocation(
        string RuntimeKey,
        string SectorOffset,
        long WadOffset,
        int SizeBytes);

    private sealed record SourceDerivedCollisionBounds(
        int MinX,
        int MaxX,
        int MinY,
        int MaxY,
        int MinZ,
        int MaxZ);

    private sealed record SourceDerivedCollisionLookupRun(
        int StartWadOffset,
        int EndWadOffset,
        int WordCount);

    private sealed record SourceDerivedCollisionTableSpan(
        int TriangleStartWadOffset,
        int TriangleEndWadOffset,
        int TriangleCount,
        int HitCount,
        int LookupStartWadOffset,
        int LookupEndWadOffset,
        int LookupWordCount);

    private sealed record SourceDerivedCollisionAppendSpan(
        int TriangleIndexBase,
        int SourceTriangleCount,
        int AppendStartWadOffset,
        int TriangleCapacity);

    private sealed record TerrainSectorSuffixShiftPlan(
        long ShiftStartWadOffset,
        long ChainTailEndWadOffset,
        int ShiftBytes,
        int ShiftedSectorCount,
        byte[] SuffixBytes);

    private sealed record CollisionTriangleBounds(
        SpyroCollisionTriangle Triangle,
        int MinXBlock,
        int MaxXBlock,
        int MinYBlock,
        int MaxYBlock,
        int MinZBlock,
        int MaxZBlock);

    private sealed record TempCollisionLookupEntry(
        int EntryIndex,
        int TriangleIndex,
        int Offset,
        ushort Word);

    private sealed record TerrainSideWallCandidate(
        string RuntimeKey,
        int SectorOffset,
        long SourceSectorWadOffset,
        SceneSectorHeader Sector,
        byte[] SourceFaceBytes,
        Vector3f OriginalA,
        Vector3f OriginalB,
        Vector3f EditedA,
        Vector3f EditedB,
        int EditedAIndex,
        int EditedBIndex,
        int OriginalAIndex,
        int OriginalBIndex,
        string OriginalEdgeKey,
        string EditedEdgeKey,
        IReadOnlyList<SpyroCollisionTriangle> SourceCollisionTriangles);

    private sealed class TerrainSideWallPatchSummaryBuilder
    {
        private readonly List<string> _skipReasons = new();
        private readonly SortedSet<int> _textureIds = new();

        public TerrainSideWallPatchSummaryBuilder(string runtimeKey, int sectorOffset)
        {
            RuntimeKey = runtimeKey;
            SectorOffset = sectorOffset;
        }

        public string RuntimeKey { get; }

        public int SectorOffset { get; }

        public int ExposedEdgeCount { get; set; }

        public int EmittedFaceCount { get; set; }

        public int CollisionTriangleCount { get; set; }

        public int SkippedEdgeCount { get; set; }

        public int RequiredAppendBytes { get; private set; }

        public int SectorSlackBytes { get; private set; }

        public void TrackAppendCapacity(int requiredAppendBytes, int sectorSlackBytes)
        {
            RequiredAppendBytes = Math.Max(RequiredAppendBytes, requiredAppendBytes);
            SectorSlackBytes = Math.Max(SectorSlackBytes, Math.Max(0, sectorSlackBytes));
        }

        public void AddTextureId(int textureId)
        {
            if (textureId >= 0)
                _textureIds.Add(textureId);
        }

        public void AddSkipReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return;
            if (!_skipReasons.Any(existing => string.Equals(existing, reason, StringComparison.OrdinalIgnoreCase)))
                _skipReasons.Add(reason);
        }

        public TerrainSideWallPatchSummary ToSummary() =>
            new(
                RuntimeKey,
                SectorOffset,
                ExposedEdgeCount,
                EmittedFaceCount,
                CollisionTriangleCount,
                SkippedEdgeCount,
                RequiredAppendBytes,
                SectorSlackBytes,
                _textureIds.ToArray(),
                _skipReasons.ToArray());
    }

    private sealed record PendingTerrainPatch(
        long WadOffset,
        byte[] Before,
        byte[] After,
        string Kind,
        string RuntimeKey,
        string Description);

    private sealed record PendingNativeTerrainTexturePatch(
        long WadOffset,
        byte[] Before,
        byte[] After,
        string Kind,
        string RuntimeKey,
        string Description);

    private readonly record struct CollisionPatchVertex(Vector3f Original, Vector3f Target);

    private sealed record ArchiveEntry(int Index, long Offset, long Size);

    private sealed record AssetSubfileInfo(int AssetWadIndex, int SubfileIndex, long AssetOffset, long AssetSize, long SubfileOffset, long SubfileSize, long AbsoluteWadOffset);

    private sealed record TextureRecordIndex(int TextureListSize, int TextureCount, int RecordBytes, IReadOnlyList<TextureRecord> Records);

    private sealed record TextureRecord(int TextureId, IReadOnlyList<TextureDescriptor> HqData, IReadOnlyList<TextureDescriptor> HqDataClose);

    private sealed record NativeTextureAppearanceAsset(
        byte[] TexturePages,
        long TexturePagesByteLength,
        TextureRecordIndex TextureIndex);

    private sealed record NativeTerrainTextureLowDetailCompanionSummary(
        int SourceSectorCount,
        int AffectedSectorCount,
        int PatchCount,
        int ChangedColorCount,
        int AffectedHighDetailFaceCount,
        int ComposedPatchCount,
        string Note)
    {
        public static NativeTerrainTextureLowDetailCompanionSummary NotNeeded(string note) =>
            new(0, 0, 0, 0, 0, 0, note);
    }

    private sealed record TextureDescriptor(
        int Index,
        int PaletteByteStart,
        int Orientation,
        int BitsPerPixel,
        int PagePackedByteX,
        int LocalPixelXMin,
        int LocalPixelXMax,
        int VramXMin,
        int VramYMin,
        int VramXMax,
        int VramYMax);

    private sealed record TexturePhysicalNibbleReference(
        string Tier,
        string Kind,
        int DescriptorIndex,
        long RelativeOffset,
        int Nibble);

    private sealed record PaletteInfo(ushort[] Words, Dictionary<ushort, int> ExactMap, Dictionary<ushort, int> NearestCache);

}

public sealed record TerrainPatchRequest(
    string SourceImagePath,
    string SourceCuePath,
    string OutputPrefix,
    LevelDefinition Level,
    string RamPath,
    string SourceSearchPath,
    string TerrainEditsPath,
    string CustomTexturesPath,
    bool WriteImage,
    string NativeTextureRelocationsPath = "",
    bool ConsumeDisposableSourceImage = false);

public sealed record TerrainTextureSlot(
    int TextureId,
    bool HasNormalDescriptors,
    bool HasCloseDescriptors,
    int NormalDescriptorCount,
    int CloseDescriptorCount,
    string NormalTopologySignature,
    string CloseTopologySignature,
    string CombinedTopologySignature);

public sealed record TerrainTextureStorageIsolation(
    int TargetTextureId,
    string DescriptorTier,
    int TargetPhysicalNibbleCount,
    int ResidentTextureCount,
    int OverlappingResidentTextureCount,
    int OverlappingPhysicalNibbleCount,
    IReadOnlyList<TerrainTextureStorageOverlap> Overlaps,
    IReadOnlyList<int> UnreadableResidentTextureIds)
{
    public bool IsIsolated => OverlappingResidentTextureCount == 0 && UnreadableResidentTextureIds.Count == 0;
}

public sealed record TerrainTextureStorageOverlap(
    int ResidentTextureId,
    int PhysicalNibbleCount,
    int TargetPaletteNibbleCount,
    int TargetPixelNibbleCount,
    int ResidentPaletteNibbleCount,
    int ResidentPixelNibbleCount);

public sealed record TerrainTextureImageExport(
    int TextureId,
    string DescriptorTier,
    int Width,
    int Height,
    int DescriptorCount,
    int PixelCount);

public sealed record NativeTerrainTextureInitialStateImageExport(
    NativeTerrainTextureInitialStateResult InitialState,
    IReadOnlyList<TerrainTextureImageExport> Exports,
    NativeTerrainLqTextureSet? LowDetailTextures = null,
    NativeTerrainHqMaterialSet? HighDetailMaterials = null);

public sealed record TerrainPatchResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputPlanPath,
    TerrainPatchPlan Plan,
    bool WroteImage);

public sealed record TerrainPatchPlan(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    string OutputImagePath,
    string OutputCuePath,
    string LevelKey,
    string LevelName,
    string RamPath,
    string SourceSearchPath,
    string TerrainEditsPath,
    string CustomTexturesPath,
    string NativeTextureRelocationsPath,
    int TextureAssetWadIndex,
    int PatchCount,
    int TotalPatchedBytes,
    int CustomTextureImportCount,
    int CustomTextureBytePatchCount,
    IReadOnlyList<CustomTerrainTexturePatchSummary> CustomTextureImports,
    int NativeTextureRelocationCount,
    int NativeTextureRelocationBytePatchCount,
    IReadOnlyList<NativeTerrainTextureRelocationPatchSummary> NativeTextureRelocations,
    IReadOnlyList<TerrainSideWallPatchSummary> TerrainSideWalls,
    IReadOnlyList<TerrainPatch> Patches,
    IReadOnlyList<string> SkippedEdits,
    IReadOnlyList<string> Notes);

public sealed record NativeTerrainTextureRelocationPatchSummary(
    string Strategy,
    IReadOnlyList<int> TargetTextureIds,
    IReadOnlyList<string> DonorTextures,
    int CompleteDescriptorCount,
    int TargetOwnedByteCount,
    int PatchCount,
    int PatchedByteCount,
    bool RuntimeControlVerified,
    bool OwnershipVerified,
    bool ExactIndexedPixelsVerified,
    bool ExactPalettesVerified,
    bool LogicalReadbackVerified,
    bool TargetDescriptorMaterialPolicyVerified,
    IReadOnlyList<string> Notes);

public sealed record TerrainSideWallPatchSummary(
    string RuntimeKey,
    int SectorOffset,
    int ExposedEdgeCount,
    int EmittedFaceCount,
    int CollisionTriangleCount,
    int SkippedEdgeCount,
    int RequiredAppendBytes,
    int SectorSlackBytes,
    IReadOnlyList<int> TextureIds,
    IReadOnlyList<string> SkipReasons);

public sealed record CustomTerrainTexturePatchSummary(
    int TextureId,
    string SourceImagePath,
    string SourceImageName,
    string DescriptorTier,
    int TileSize,
    int DescriptorCount,
    int PixelPatchCount,
    int PalettePatchCount,
    int TexelBytesPerPixel,
    int BitsPerPixel,
    int PaletteColorCount,
    int SourceTexelUniqueCount,
    int SourceTexelMax,
    int SourcePaletteUniqueColorCount,
    int SourcePaletteNonZeroColorCount,
    IReadOnlyList<string> PaletteByteStarts);

public sealed record TerrainPatch(
    string Label,
    string Kind,
    string RuntimeKey,
    string WadRelativeOffset,
    string ImageOffset,
    int ByteLength,
    string BeforeHexPreview,
    string AfterHexPreview,
    string Description);

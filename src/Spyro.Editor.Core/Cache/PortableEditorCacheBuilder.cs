using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Cache;

public sealed record PortableEditorCacheResult(
    int LevelCount,
    int MobyCacheCount,
    int OverlayCacheCount,
    string CachePath,
    bool ReusedExistingCache = false,
    int TexturePreviewCount = 0,
    int EntryPoseCount = 0);

public static class PortableEditorCacheBuilder
{
    public const int TerrainTexturePreviewCacheFormatVersion = 9;
    public const string TerrainTexturePreviewDecoder = "hq8-dual-lod-plus-lq4-indexed16-plus-raw-hq-psx555-stp-abr-all-native-load-initialized-v8";
    public const string TerrainTexturePreviewStateSemantics = "nativeLoadInitialState";

    public static async Task<PortableEditorCacheResult> BuildAsync(
        EditorWorkspace workspace,
        LevelCatalog catalog,
        bool overwrite = true,
        bool fastReuseExistingCache = false,
        CancellationToken cancellationToken = default)
    {
        string cacheDir = Path.Combine(workspace.RootPath, "editor-cache");
        Directory.CreateDirectory(cacheDir);

        if (!overwrite && fastReuseExistingCache)
        {
            PortableEditorCacheResult? existing = await TryReuseCompleteExistingCacheAsync(catalog, cacheDir, cancellationToken);
            if (existing != null)
                return existing;
        }

        await EnsureWadAnalysisAsync(workspace, cancellationToken);

        PortableLevelEntryPoseCacheBuildResult entryPoseCache =
            await PortableLevelEntryPoseCache.BuildAsync(
                DiscImageLocator.FindImage(workspace),
                cacheDir,
                catalog,
                overwrite,
                cancellationToken);

        List<object> summary = new();
        int mobyCacheCount = 0;
        int overlayCacheCount = 0;
        int texturePreviewCount = 0;

        foreach (LevelDefinition level in catalog.Levels)
        {
            string mobyCacheFile = Path.Combine(cacheDir, $"{level.Key}-mobys.json");
            string overlayCacheFile = Path.Combine(cacheDir, $"{level.Key}-runtime-scene-editor-overlay.json");
            string overlayHealth = "";

            bool hasMobyCache = File.Exists(mobyCacheFile) && !overwrite;
            if (!hasMobyCache)
            {
                if (TryBuildSourceMobyCache(workspace, level, mobyCacheFile))
                {
                    hasMobyCache = true;
                }
                else
                {
                    string ramPath = FindRamPath(workspace, level.Key);
                    if (File.Exists(ramPath))
                    {
                        IReadOnlyList<Moby> mobys = MobyLoader.LoadRamDump(ramPath);
                        await WriteMobyCacheAsync(mobyCacheFile, level, ramPath, mobys, cancellationToken);
                        hasMobyCache = true;
                    }
                }
            }

            bool hasOverlay = File.Exists(overlayCacheFile) && !overwrite;
            if (!hasOverlay)
            {
                string overlayPath = workspace.ResolveFile($"{level.Key}-runtime-scene-editor-overlay.json", "generated-research");
                if (File.Exists(overlayPath))
                {
                    GeometryCacheHealthIssue? sourceIssue = GeometryCacheHealth.InspectOverlay(level.Key, overlayPath);
                    if (sourceIssue?.BlocksLoading == true)
                    {
                        overlayHealth = sourceIssue.Message;
                    }
                    else
                    {
                        File.Copy(overlayPath, overlayCacheFile, true);
                        hasOverlay = true;
                    }
                }
            }
            if (hasOverlay)
            {
                GeometryCacheHealthIssue? cacheIssue = GeometryCacheHealth.InspectOverlay(level.Key, overlayCacheFile);
                if (cacheIssue?.BlocksLoading == true)
                {
                    hasOverlay = false;
                    overlayHealth = cacheIssue.Message;
                }
            }
            if (!hasOverlay && TryBuildSourceOverlay(workspace, level, overlayCacheFile, out string sourceOverlayHealth))
            {
                hasOverlay = true;
                overlayHealth = sourceOverlayHealth;
            }

            if (hasMobyCache)
                mobyCacheCount++;
            if (hasOverlay)
                overlayCacheCount++;

            int levelTexturePreviewCount = hasOverlay
                ? await BuildTerrainTexturePreviewCacheAsync(
                    workspace,
                    level,
                    overlayCacheFile,
                    cacheDir,
                    overwrite,
                    cancellationToken)
                : 0;
            texturePreviewCount += levelTexturePreviewCount;

            summary.Add(BuildSummaryRow(level, hasMobyCache, hasOverlay, overlayHealth, levelTexturePreviewCount));
        }

        await WriteIndexAsync(cacheDir, catalog, summary, entryPoseCache.LevelCount, cancellationToken);
        return new PortableEditorCacheResult(
            catalog.Levels.Count,
            mobyCacheCount,
            overlayCacheCount,
            cacheDir,
            TexturePreviewCount: texturePreviewCount,
            EntryPoseCount: entryPoseCache.LevelCount);
    }

    private static async Task<PortableEditorCacheResult?> TryReuseCompleteExistingCacheAsync(
        LevelCatalog catalog,
        string cacheDir,
        CancellationToken cancellationToken)
    {
        if (!PortableLevelEntryPoseCache.TryLoadCompleteFromCacheDirectory(
                cacheDir,
                catalog,
                out PortableLevelEntryPoseCacheSnapshot entryPoses,
                out _))
        {
            return null;
        }

        List<object> summary = new();
        int mobyCacheCount = 0;
        int overlayCacheCount = 0;
        int totalTexturePreviewCount = 0;
        foreach (LevelDefinition level in catalog.Levels)
        {
            string mobyCacheFile = Path.Combine(cacheDir, $"{level.Key}-mobys.json");
            string overlayCacheFile = Path.Combine(cacheDir, $"{level.Key}-runtime-scene-editor-overlay.json");
            bool hasMobyCache = File.Exists(mobyCacheFile);
            bool hasOverlay = File.Exists(overlayCacheFile);
            string overlayHealth = "";
            if (!hasMobyCache || !hasOverlay)
                return null;

            GeometryCacheHealthIssue? cacheIssue = GeometryCacheHealth.InspectOverlay(level.Key, overlayCacheFile);
            if (cacheIssue?.BlocksLoading == true)
                return null;

            overlayHealth = cacheIssue?.Message ?? "";

            mobyCacheCount++;
            overlayCacheCount++;
            int texturePreviewCount = CountTerrainTexturePreviewFiles(cacheDir, level.Key);
            if (texturePreviewCount <= 0)
                return null;
            totalTexturePreviewCount += texturePreviewCount;
            summary.Add(BuildSummaryRow(level, hasMobyCache, hasOverlay, overlayHealth, texturePreviewCount));
        }

        await WriteIndexAsync(cacheDir, catalog, summary, entryPoses.Poses.Count, cancellationToken);
        return new PortableEditorCacheResult(
            catalog.Levels.Count,
            mobyCacheCount,
            overlayCacheCount,
            cacheDir,
            ReusedExistingCache: true,
            TexturePreviewCount: totalTexturePreviewCount,
            EntryPoseCount: entryPoses.Poses.Count);
    }

    private static object BuildSummaryRow(
        LevelDefinition level,
        bool hasMobyCache,
        bool hasOverlay,
        string overlayHealth,
        int texturePreviewCount)
    {
        return new
        {
            level.Key,
            level.DisplayName,
            hasMobyCache,
            hasOverlay,
            overlayHealth,
            texturePreviewCount,
            mobyCache = hasMobyCache ? $"editor-cache/{level.Key}-mobys.json" : "",
            overlayCache = hasOverlay ? $"editor-cache/{level.Key}-runtime-scene-editor-overlay.json" : "",
            texturePreviewCache = texturePreviewCount > 0 ? $"editor-cache/terrain-textures/{level.Key}/manifest.json" : ""
        };
    }

    private static async Task<int> BuildTerrainTexturePreviewCacheAsync(
        EditorWorkspace workspace,
        LevelDefinition level,
        string overlayPath,
        string cacheDir,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        string sourceImage = DiscImageLocator.FindImage(workspace);
        if (!File.Exists(sourceImage) || !File.Exists(overlayPath))
            return CountTerrainTexturePreviewFiles(cacheDir, level.Key);

        try
        {
            GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
            int[] textureIds = geometry.Polygons
                .Select(polygon => polygon.TextureId)
                .Where(textureId => textureId >= 0)
                .Distinct()
                .OrderBy(textureId => textureId)
                .ToArray();
            if (textureIds.Length == 0)
                return 0;

            NativeTerrainTextureRuntimeControlAudit runtimeControlAudit =
                NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImage, level);
            if (!runtimeControlAudit.Complete)
            {
                InvalidateTerrainTexturePreviewCache(cacheDir, level.Key);
                return 0;
            }

            // HP face byte 0xFF is the native untextured sentinel, not texture
            // record 127. Other out-of-table values likewise cannot be decoded.
            int[] ignoredFaceTextureIds = textureIds
                .Where(textureId => textureId >= runtimeControlAudit.TextureCount)
                .ToArray();
            textureIds = textureIds
                .Where(textureId => textureId < runtimeControlAudit.TextureCount)
                .ToArray();
            if (textureIds.Length == 0)
            {
                InvalidateTerrainTexturePreviewCache(cacheDir, level.Key);
                return 0;
            }

            int[] faceReferencedTextureIds = textureIds;
            HashSet<int> faceReferencedTextureIdSet = faceReferencedTextureIds.ToHashSet();
            int[] animationSourceTextureIds = runtimeControlAudit.AnimationSourceTextureIds.ToArray();
            HashSet<int> animationSourceTextureIdSet = animationSourceTextureIds.ToHashSet();
            int[] runtimeControlledTextureIds = runtimeControlAudit.ControlledTextureIds.ToArray();
            HashSet<int> runtimeControlledTextureIdSet = runtimeControlledTextureIds.ToHashSet();
            int[] controlledWithoutFaceTextureIds = runtimeControlledTextureIds
                .Where(textureId => !faceReferencedTextureIdSet.Contains(textureId))
                .Order()
                .ToArray();
            int[] exportedTextureIds = Enumerable.Range(0, runtimeControlAudit.TextureCount).ToArray();
            int[] nativeUnreferencedTextureIds = exportedTextureIds
                .Where(textureId =>
                    !faceReferencedTextureIdSet.Contains(textureId) &&
                    !animationSourceTextureIdSet.Contains(textureId) &&
                    !runtimeControlledTextureIdSet.Contains(textureId))
                .ToArray();
            HashSet<int> nativeUnreferencedTextureIdSet = nativeUnreferencedTextureIds.ToHashSet();

            string outputDir = TerrainTexturePreviewDirectory(cacheDir, level.Key);
            Directory.CreateDirectory(outputDir);
            bool rewriteTextureImages = overwrite || !IsCurrentTerrainTexturePreviewManifest(outputDir);
            if (rewriteTextureImages)
            {
                foreach (string stalePath in Directory.EnumerateFiles(outputDir, $"{level.Key}-texture-*.png"))
                    File.Delete(stalePath);
            }
            NativeTerrainTextureInitialStateImageExport initializedExport =
                await TerrainPatchExporter.ExportTerrainTextureInitialStateImageTiersAsync(
                    sourceImage,
                    level,
                    runtimeControlAudit,
                    exportedTextureIds,
                    outputDir,
                    rewriteTextureImages,
                    cancellationToken);
            IReadOnlyList<TerrainTextureImageExport> exports = initializedExport.Exports;
            NativeTerrainLqTextureSet lowDetailTextures = initializedExport.LowDetailTextures
                ?? throw new InvalidDataException("The native load-state export did not include its TexLq indexed payload.");
            string lowDetailPayloadPath = Path.Combine(outputDir, NativeTerrainLqTextureCacheCodec.PayloadFileName);
            NativeTerrainLqTextureCacheWriteResult lowDetailPayload =
                await NativeTerrainLqTextureCacheCodec.WriteAsync(
                    lowDetailPayloadPath,
                    lowDetailTextures,
                    cancellationToken);
            NativeTerrainHqMaterialSet highDetailMaterials = initializedExport.HighDetailMaterials
                ?? throw new InvalidDataException("The native load-state export did not include its raw HQ PSX555/STP material payload.");
            string highDetailMaterialPayloadPath = Path.Combine(outputDir, NativeTerrainHqMaterialCacheCodec.PayloadFileName);
            NativeTerrainHqMaterialCacheWriteResult highDetailMaterialPayload =
                await NativeTerrainHqMaterialCacheCodec.WriteAsync(
                    highDetailMaterialPayloadPath,
                    highDetailMaterials,
                    cancellationToken);
            int decodedTextureCount = exports
                .Select(export => export.TextureId)
                .Distinct()
                .Count();
            int completeDualTierTextureCount = exports
                .GroupBy(export => export.TextureId)
                .Count(group =>
                    group.Any(export => string.Equals(export.DescriptorTier, "hqData", StringComparison.OrdinalIgnoreCase)) &&
                    group.Any(export => string.Equals(export.DescriptorTier, "hqDataClose", StringComparison.OrdinalIgnoreCase)));
            bool lowDetailComplete = lowDetailTextures.TextureCount == exportedTextureIds.Length &&
                lowDetailPayload.RecordCount == exportedTextureIds.Length &&
                lowDetailTextures.RetailAliasComplete;
            bool highDetailMaterialComplete = highDetailMaterials.TextureCount == exportedTextureIds.Length &&
                highDetailMaterialPayload.RecordCount == exportedTextureIds.Length &&
                highDetailMaterials.CompositeCount == checked(exportedTextureIds.Length * 2) &&
                highDetailMaterials.TotalDescriptorCount == checked(exportedTextureIds.Length * NativeTerrainHqMaterialCacheCodec.DescriptorCountPerTexture) &&
                highDetailMaterials.TexturePagesByteLength == lowDetailTextures.TexturePagesByteLength &&
                string.Equals(highDetailMaterials.TexturePagesSha256, lowDetailTextures.TexturePagesSha256, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(highDetailMaterials.OriginalTextureComponentSha256, lowDetailTextures.OriginalTextureComponentSha256, StringComparison.OrdinalIgnoreCase);
            bool initialStateComplete = initializedExport.InitialState.Complete &&
                decodedTextureCount == exportedTextureIds.Length &&
                completeDualTierTextureCount == exportedTextureIds.Length &&
                lowDetailComplete &&
                highDetailMaterialComplete;
            object[] runtimeControls = BuildRuntimeControlManifestRows(runtimeControlAudit);

            var manifest = new
            {
                generatedBy = "Spyro.Editor.Core",
                generatedAt = DateTimeOffset.UtcNow.ToString("O"),
                purpose = "Native PS1 terrain texture previews decoded from the selected source disc for the editor viewport.",
                cacheFormatVersion = TerrainTexturePreviewCacheFormatVersion,
                decoder = TerrainTexturePreviewDecoder,
                levelKey = level.Key,
                levelName = level.DisplayName,
                stateSemantics = TerrainTexturePreviewStateSemantics,
                stateDescription = "Native load/default state immediately after func_8002B4AC. This is not the current gameplay frame and playback is not included.",
                runtimePlaybackIncluded = false,
                currentGameplayState = false,
                nativeTextureCount = runtimeControlAudit.TextureCount,
                faceReferencedTextureCount = faceReferencedTextureIds.Length,
                faceReferencedTextureIds,
                ignoredOutOfTableFaceTextureIds = ignoredFaceTextureIds,
                animationSourceDiagnosticTextureCount = animationSourceTextureIds.Length,
                animationSourceTextureIds,
                runtimeControlledTextureCount = runtimeControlledTextureIds.Length,
                controlledWithoutFaceTextureCount = controlledWithoutFaceTextureIds.Length,
                controlledWithoutFaceTextureIds,
                nativeUnreferencedTextureCount = nativeUnreferencedTextureIds.Length,
                nativeUnreferencedTextureIds,
                requestedTextureCount = exportedTextureIds.Length,
                decodedTextureCount,
                decodedFrameCount = exports.Count,
                completeDualTierTextureCount,
                lqStateSemantics = TerrainTexturePreviewStateSemantics,
                lqPayloadFile = Path.GetFileName(lowDetailPayload.Path),
                lqPayloadFormatVersion = NativeTerrainLqTextureCacheCodec.PayloadFormatVersion,
                lqPayloadRecordSize = NativeTerrainLqTextureCacheCodec.RecordSize,
                lqPayloadByteLength = lowDetailPayload.ByteLength,
                lqPayloadSha256 = lowDetailPayload.Sha256,
                lqNativeTextureCount = lowDetailTextures.NativeTextureCount,
                lqRequestedTextureCount = exportedTextureIds.Length,
                lqDecodedTextureCount = lowDetailTextures.TextureCount,
                completeLqTextureCount = lowDetailTextures.TextureCount,
                lqDescriptorCountPerTexture = NativeTerrainLqTextureCacheCodec.DescriptorCount,
                lqTextureSide = NativeTerrainLqTextureCacheCodec.TextureSide,
                lqBitsPerPixel = NativeTerrainLqTextureCacheCodec.BitsPerPixel,
                lqPackedIndexByteCount = NativeTerrainLqTextureCacheCodec.PackedIndexByteCount,
                lqPaletteRowCount = NativeTerrainLqTextureCacheCodec.PaletteRowCount,
                lqPaletteColorCount = NativeTerrainLqTextureCacheCodec.PaletteColorCount,
                lqTexturePagesByteLength = lowDetailTextures.TexturePagesByteLength,
                lqTexturePagesSha256 = lowDetailTextures.TexturePagesSha256,
                lqOriginalTextureComponentSha256 = lowDetailTextures.OriginalTextureComponentSha256,
                lqInitializedTableSha256 = lowDetailTextures.InitializedLqTableSha256,
                lqRetailAliasCount = lowDetailTextures.RetailAliasCount,
                lqRetailAliasComplete = lowDetailTextures.RetailAliasComplete,
                nativeMaterialFormat = NativeTerrainHqMaterialCacheCodec.MaterialFormat,
                nativeMaterialStateSemantics = TerrainTexturePreviewStateSemantics,
                nativeMaterialFile = Path.GetFileName(highDetailMaterialPayload.Path),
                nativeMaterialPayloadFormatVersion = NativeTerrainHqMaterialCacheCodec.PayloadFormatVersion,
                nativeMaterialHeaderSize = NativeTerrainHqMaterialCacheCodec.HeaderSize,
                nativeMaterialDescriptorHeaderSize = NativeTerrainHqMaterialCacheCodec.DescriptorHeaderSize,
                nativeMaterialPayloadByteLength = highDetailMaterialPayload.ByteLength,
                nativeMaterialPayloadSha256 = highDetailMaterialPayload.Sha256,
                nativeMaterialContentSha256 = highDetailMaterials.ContentSha256,
                nativeMaterialNativeTextureCount = highDetailMaterials.NativeTextureCount,
                nativeMaterialRequestedTextureCount = exportedTextureIds.Length,
                nativeMaterialDecodedTextureCount = highDetailMaterials.TextureCount,
                nativeMaterialCompositeCount = highDetailMaterials.CompositeCount,
                nativeMaterialDescriptorCount = highDetailMaterials.TotalDescriptorCount,
                nativeMaterialDescriptorCountPerTexture = NativeTerrainHqMaterialCacheCodec.DescriptorCountPerTexture,
                nativeMaterialNormalDescriptorCount = NativeTerrainHqMaterialCacheCodec.NormalDescriptorCount,
                nativeMaterialCloseDescriptorCount = NativeTerrainHqMaterialCacheCodec.CloseDescriptorCount,
                nativeMaterialRawWordByteCount = NativeTerrainHqMaterialCacheCodec.RawWordByteCount,
                nativeMaterialRawWordCount = highDetailMaterials.RawWordCount,
                nativeMaterialZeroWordCount = highDetailMaterials.ZeroWordCount,
                nativeMaterialStpSetWordCount = highDetailMaterials.StpSetWordCount,
                nativeMaterialAbrDescriptorCounts = highDetailMaterials.AbrDescriptorCounts,
                nativeMaterialTexturePagesByteLength = highDetailMaterials.TexturePagesByteLength,
                nativeMaterialTextureComponentByteLength = highDetailMaterials.TextureComponentByteLength,
                nativeMaterialTexturePagesSha256 = highDetailMaterials.TexturePagesSha256,
                nativeMaterialOriginalTextureComponentSha256 = highDetailMaterials.OriginalTextureComponentSha256,
                nativeMaterialInitializedTextureComponentSha256 = highDetailMaterials.InitializedTextureComponentSha256,
                nativeMaterialInitializedHqTableSha256 = highDetailMaterials.InitializedHqTableSha256,
                rawPsx555Preserved = true,
                zeroWordTransparencyPreserved = true,
                stpPreserved = true,
                descriptorAbrPreserved = true,
                closeDepthEditorUnits = NativeTerrainTexturePreviewLod.CloseDepthEditorUnits,
                runtimeControlComplete = runtimeControlAudit.Complete,
                runtimeControlInitialStateComplete = initialStateComplete,
                runtimeControlTargetWadEntry = runtimeControlAudit.TargetWadEntry,
                runtimeControlSceneByteLength = runtimeControlAudit.SceneByteLength,
                runtimeControlSceneSha256 = runtimeControlAudit.SceneSha256,
                runtimeControlProgramCount = runtimeControlAudit.AnimationControls.Count + runtimeControlAudit.ScrollingControls.Count,
                runtimeControlledTextureIds,
                runtimeInitializedTextureIds = runtimeControlledTextureIds,
                runtimeControls,
                initializationMutations = initializedExport.InitialState.Mutations.Select(mutation => new
                {
                    runtimeKind = RuntimeKindName(mutation.Kind),
                    mutation.ControlId,
                    mutation.PointerIndex,
                    mutation.DestinationTextureId,
                    mutation.InitialSourceTextureId,
                    mutation.InitialPhase,
                    mutation.LqChangedByteCount,
                    mutation.HqChangedByteCount,
                    mutation.OriginalLqSha256,
                    mutation.InitializedLqSha256,
                    mutation.OriginalHqSha256,
                    mutation.InitializedHqSha256
                }).ToArray(),
                textures = exports.Select(export => BuildTextureManifestRow(
                    level.Key,
                    outputDir,
                    export,
                    runtimeControlAudit,
                    animationSourceTextureIdSet,
                    faceReferencedTextureIdSet,
                    nativeUnreferencedTextureIdSet)).ToArray(),
                lqTextures = lowDetailTextures.Textures.Select(texture => BuildLowDetailTextureManifestRow(
                    texture,
                    runtimeControlAudit,
                    animationSourceTextureIdSet,
                    faceReferencedTextureIdSet,
                    nativeUnreferencedTextureIdSet)).ToArray(),
                hqMaterials = highDetailMaterials.Textures.Select(BuildHighDetailMaterialManifestRow).ToArray()
            };
            await using FileStream stream = File.Create(Path.Combine(outputDir, "manifest.json"));
            await JsonSerializer.SerializeAsync(
                stream,
                manifest,
                new JsonSerializerOptions { WriteIndented = true },
                cancellationToken);
            return decodedTextureCount;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            return CountTerrainTexturePreviewFiles(cacheDir, level.Key);
        }
    }

    private static object[] BuildRuntimeControlManifestRows(NativeTerrainTextureRuntimeControlAudit audit)
    {
        List<object> rows = [];
        foreach (NativeTerrainTextureAnimationControl control in audit.AnimationControls.OrderBy(control => control.PointerIndex))
        {
            rows.Add(new
            {
                runtimeKind = RuntimeKindName(NativeTerrainTextureRuntimeControlKind.FullRecordAnimation),
                stateSemantics = TerrainTexturePreviewStateSemantics,
                controlId = control.ControlId,
                pointerIndex = control.PointerIndex,
                sceneRelativeStructureOffset = control.SceneRelativeStructureOffset,
                destinationTextureId = control.DestinationTextureId,
                initialStateFlags = control.InitialStateFlags,
                initialCurrentFrame = control.InitialCurrentFrame,
                initialTicksRemaining = control.InitialTicksRemaining,
                initialSourceTextureId = (int?)control.InitialSourceTextureId,
                initialPhase = (int?)null,
                rawBytesHex = control.RawBytesHex,
                frames = control.Frames.Select(frame => new
                {
                    frameIndex = frame.FrameIndex,
                    durationAndFlags = frame.DurationAndFlags,
                    durationTicks = frame.DurationTicks,
                    stateFlags = frame.StateFlags,
                    forwardNextFrame = frame.ForwardNextFrame,
                    reverseNextFrame = frame.ReverseNextFrame,
                    sourceTextureId = (int?)frame.SourceTextureId,
                    phaseDelta = (int?)null
                }).ToArray()
            });
        }

        foreach (NativeTerrainScrollingTextureControl control in audit.ScrollingControls.OrderBy(control => control.PointerIndex))
        {
            rows.Add(new
            {
                runtimeKind = RuntimeKindName(NativeTerrainTextureRuntimeControlKind.ScrollingDescriptor),
                stateSemantics = TerrainTexturePreviewStateSemantics,
                controlId = control.ControlId,
                pointerIndex = control.PointerIndex,
                sceneRelativeStructureOffset = control.SceneRelativeStructureOffset,
                destinationTextureId = control.DestinationTextureId,
                initialStateFlags = control.InitialStateFlags,
                initialCurrentFrame = control.InitialCurrentFrame,
                initialTicksRemaining = control.InitialTicksRemaining,
                initialSourceTextureId = (int?)null,
                initialPhase = (int?)control.InitialPhase,
                rawBytesHex = control.RawBytesHex,
                frames = control.Frames.Select(frame => new
                {
                    frameIndex = frame.FrameIndex,
                    durationAndFlags = frame.DurationAndFlags,
                    durationTicks = frame.DurationTicks,
                    stateFlags = frame.StateFlags,
                    forwardNextFrame = frame.ForwardNextFrame,
                    reverseNextFrame = frame.ReverseNextFrame,
                    sourceTextureId = (int?)null,
                    phaseDelta = (int?)frame.PhaseDelta
                }).ToArray()
            });
        }

        return rows.ToArray();
    }

    private static object BuildTextureManifestRow(
        string levelKey,
        string outputDirectory,
        TerrainTextureImageExport export,
        NativeTerrainTextureRuntimeControlAudit audit,
        IReadOnlySet<int> animationSourceTextureIds,
        IReadOnlySet<int> faceReferencedTextureIds,
        IReadOnlySet<int> nativeUnreferencedTextureIds)
    {
        NativeTerrainTextureAnimationControl[] animations = audit.AnimationControls
            .Where(control => control.DestinationTextureId == export.TextureId)
            .OrderBy(control => control.PointerIndex)
            .ToArray();
        NativeTerrainScrollingTextureControl[] scrolling = audit.ScrollingControls
            .Where(control => control.DestinationTextureId == export.TextureId)
            .OrderBy(control => control.PointerIndex)
            .ToArray();
        bool runtimeControlled = animations.Length + scrolling.Length > 0;
        bool animationSourceDiagnostic = animationSourceTextureIds.Contains(export.TextureId);
        bool faceReferenced = faceReferencedTextureIds.Contains(export.TextureId);
        bool nativeUnreferenced = nativeUnreferencedTextureIds.Contains(export.TextureId);
        string runtimeKind = animations.Length > 0 && scrolling.Length > 0
            ? "multiple"
            : animations.Length > 0
                ? RuntimeKindName(NativeTerrainTextureRuntimeControlKind.FullRecordAnimation)
                : scrolling.Length > 0
                    ? RuntimeKindName(NativeTerrainTextureRuntimeControlKind.ScrollingDescriptor)
                    : "none";
        string previewState = runtimeControlled
            ? TerrainTexturePreviewStateSemantics
            : animationSourceDiagnostic
                ? "animationSourceDiagnostic"
                : "staticRecord";

        string file = TerrainTexturePreviewFileName(levelKey, export);
        NativeTerrainTexturePreviewFrameProof frameProof = ReadTerrainTexturePreviewFrameProof(
            Path.Combine(outputDirectory, file),
            export.Width,
            export.Height);

        return new
        {
            textureId = export.TextureId,
            descriptorTier = export.DescriptorTier,
            previewTier = NativeTerrainTexturePreviewLod.FileSuffix(
                NativeTerrainTexturePreviewLod.FromDescriptorTier(export.DescriptorTier)),
            width = export.Width,
            height = export.Height,
            descriptorCount = export.DescriptorCount,
            pixelCount = export.PixelCount,
            runtimeControlled,
            runtimeInitializationApplied = runtimeControlled,
            faceReferenced,
            nativeUnreferenced,
            runtimeKind,
            previewState,
            runtimeRole = runtimeControlled
                ? faceReferenced
                    ? "controlledFaceDestination"
                    : "controlledDestinationWithoutFace"
                : animationSourceDiagnostic
                    ? "animationSourceDiagnostic"
                    : faceReferenced
                        ? "faceReferencedStatic"
                        : "nativeUnreferencedStatic",
            runtimeControlIds = animations.Select(control => control.ControlId)
                .Concat(scrolling.Select(control => control.ControlId))
                .ToArray(),
            runtimePointerIndexes = animations.Select(control => control.PointerIndex)
                .Concat(scrolling.Select(control => control.PointerIndex))
                .ToArray(),
            runtimeDestinationTextureId = runtimeControlled ? (int?)export.TextureId : null,
            initialSourceTextureIds = animations.Select(control => control.InitialSourceTextureId).Distinct().Order().ToArray(),
            initialPhases = scrolling.Select(control => control.InitialPhase).Distinct().Order().ToArray(),
            animationSourceDiagnostic,
            file,
            fileByteLength = frameProof.ByteLength,
            fileSha256 = frameProof.Sha256
        };
    }

    private static object BuildLowDetailTextureManifestRow(
        NativeTerrainLqTextureRecordPayload texture,
        NativeTerrainTextureRuntimeControlAudit audit,
        IReadOnlySet<int> animationSourceTextureIds,
        IReadOnlySet<int> faceReferencedTextureIds,
        IReadOnlySet<int> nativeUnreferencedTextureIds)
    {
        NativeTerrainTextureAnimationControl[] animations = audit.AnimationControls
            .Where(control => control.DestinationTextureId == texture.TextureId)
            .OrderBy(control => control.PointerIndex)
            .ToArray();
        NativeTerrainScrollingTextureControl[] scrolling = audit.ScrollingControls
            .Where(control => control.DestinationTextureId == texture.TextureId)
            .OrderBy(control => control.PointerIndex)
            .ToArray();
        bool runtimeControlled = animations.Length + scrolling.Length > 0;
        bool animationSourceDiagnostic = animationSourceTextureIds.Contains(texture.TextureId);
        bool faceReferenced = faceReferencedTextureIds.Contains(texture.TextureId);
        bool nativeUnreferenced = nativeUnreferencedTextureIds.Contains(texture.TextureId);
        return new
        {
            textureId = texture.TextureId,
            descriptorCount = texture.Descriptors.Count,
            side = NativeTerrainLqTextureCacheCodec.TextureSide,
            bitsPerPixel = NativeTerrainLqTextureCacheCodec.BitsPerPixel,
            paletteRowCount = NativeTerrainLqTextureCacheCodec.PaletteRowCount,
            paletteColorCount = NativeTerrainLqTextureCacheCodec.PaletteColorCount,
            runtimeControlled,
            runtimeInitializationApplied = runtimeControlled,
            faceReferenced,
            nativeUnreferenced,
            previewState = runtimeControlled
                ? TerrainTexturePreviewStateSemantics
                : animationSourceDiagnostic
                    ? "animationSourceDiagnostic"
                    : "staticRecord",
            runtimeRole = runtimeControlled
                ? faceReferenced
                    ? "controlledFaceDestination"
                    : "controlledDestinationWithoutFace"
                : animationSourceDiagnostic
                    ? "animationSourceDiagnostic"
                    : faceReferenced
                        ? "faceReferencedStatic"
                        : "nativeUnreferencedStatic",
            animationSourceDiagnostic,
            descriptors = texture.Descriptors.Select(descriptor => new
            {
                descriptorIndex = descriptor.DescriptorIndex,
                rawDescriptorHex = Convert.ToHexString(descriptor.RawDescriptor.Span),
                descriptor.RawDescriptorSha256,
                descriptor.PackedIndicesSha256,
                descriptor.PaletteWordsSha256,
                descriptor.Orientation,
                descriptor.Abr
            }).ToArray()
        };
    }

    private static object BuildHighDetailMaterialManifestRow(NativeTerrainHqMaterialRecordPayload texture)
    {
        static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
        static object BuildTier(NativeTerrainHqMaterialTierPayload tier) => new
        {
            descriptorTier = tier.TierName,
            gridColumns = tier.GridColumns,
            tileSide = tier.TileSide,
            compositeSide = tier.CompositeSide,
            descriptorCount = tier.DescriptorCount,
            rawWordCount = tier.RawWordCount,
            zeroWordCount = tier.ZeroWordCount,
            stpSetWordCount = tier.StpSetWordCount,
            compositeRawWordsSha256 = tier.CompositeRawWordsSha256,
            abrDescriptorCounts = Enumerable.Range(0, 4)
                .Select(abr => tier.Descriptors.Count(descriptor => descriptor.Abr == abr))
                .ToArray(),
            rawDescriptorSetSha256 = Hash(tier.Descriptors.SelectMany(descriptor => descriptor.RawDescriptor.ToArray()).ToArray()),
            rawWordSetSha256 = Hash(tier.Descriptors.SelectMany(descriptor => descriptor.RawWordsLittleEndian.ToArray()).ToArray())
        };

        return new
        {
            textureId = texture.TextureId,
            descriptorCount = texture.Normal.DescriptorCount + texture.Close.DescriptorCount,
            compositeCount = 2,
            rawWordCount = texture.Normal.RawWordCount + texture.Close.RawWordCount,
            zeroWordCount = texture.Normal.ZeroWordCount + texture.Close.ZeroWordCount,
            stpSetWordCount = texture.Normal.StpSetWordCount + texture.Close.StpSetWordCount,
            normal = BuildTier(texture.Normal),
            close = BuildTier(texture.Close)
        };
    }

    private static string RuntimeKindName(NativeTerrainTextureRuntimeControlKind kind) =>
        kind == NativeTerrainTextureRuntimeControlKind.FullRecordAnimation
            ? "fullRecordAnimation"
            : "scrollingDescriptor";

    private static NativeTerrainTexturePreviewFrameProof ReadTerrainTexturePreviewFrameProof(
        string path,
        int expectedWidth,
        int expectedHeight)
    {
        if (!TryReadTerrainTexturePreviewFrameProof(path, out NativeTerrainTexturePreviewFrameProof proof) ||
            proof.Width != expectedWidth || proof.Height != expectedHeight)
        {
            throw new InvalidDataException(
                $"Terrain texture preview frame '{path}' does not match its decoded {expectedWidth}x{expectedHeight} dimensions.");
        }

        return proof;
    }

    public static bool IsNativeTerrainTexturePreviewFrameValid(
        string path,
        int expectedWidth,
        int expectedHeight,
        long expectedByteLength,
        string expectedSha256)
    {
        if (expectedWidth <= 0 || expectedHeight <= 0 || expectedByteLength <= 0 || !IsSha256Hex(expectedSha256) ||
            !TryReadTerrainTexturePreviewFrameProof(path, out NativeTerrainTexturePreviewFrameProof proof))
        {
            return false;
        }

        return proof.Width == expectedWidth && proof.Height == expectedHeight &&
            proof.ByteLength == expectedByteLength &&
            string.Equals(proof.Sha256, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadTerrainTexturePreviewFrameProof(
        string path,
        out NativeTerrainTexturePreviewFrameProof proof)
    {
        proof = default;
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length <= 0)
                return false;

            Span<byte> header = stackalloc byte[24];
            stream.ReadExactly(header);
            ReadOnlySpan<byte> pngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
            if (!header[..pngSignature.Length].SequenceEqual(pngSignature) ||
                BinaryPrimitives.ReadInt32BigEndian(header.Slice(8, 4)) != 13 ||
                !header.Slice(12, 4).SequenceEqual("IHDR"u8))
            {
                return false;
            }

            int width = BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4));
            int height = BinaryPrimitives.ReadInt32BigEndian(header.Slice(20, 4));
            if (width <= 0 || height <= 0)
                return false;

            stream.Position = 0;
            string sha256 = Convert.ToHexString(SHA256.HashData(stream));
            proof = new NativeTerrainTexturePreviewFrameProof(width, height, stream.Length, sha256);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            proof = default;
            return false;
        }
    }

    private static void InvalidateTerrainTexturePreviewCache(string cacheDir, string levelKey)
    {
        string directory = TerrainTexturePreviewDirectory(cacheDir, levelKey);
        if (!Directory.Exists(directory))
            return;

        string manifestPath = Path.Combine(directory, "manifest.json");
        if (File.Exists(manifestPath))
            File.Delete(manifestPath);
        foreach (string stalePath in Directory.EnumerateFiles(directory, $"{levelKey}-texture-*.png"))
            File.Delete(stalePath);
        string lowDetailPayload = Path.Combine(directory, NativeTerrainLqTextureCacheCodec.PayloadFileName);
        if (File.Exists(lowDetailPayload))
            File.Delete(lowDetailPayload);
        string highDetailMaterialPayload = Path.Combine(directory, NativeTerrainHqMaterialCacheCodec.PayloadFileName);
        if (File.Exists(highDetailMaterialPayload))
            File.Delete(highDetailMaterialPayload);
    }

    private static string TerrainTexturePreviewDirectory(string cacheDir, string levelKey) =>
        Path.Combine(cacheDir, "terrain-textures", LevelCatalog.NormalizeKey(levelKey));

    public static bool IsCurrentTerrainTexturePreviewCache(string workspaceRoot, string levelKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
            return false;
        string cacheDir = Path.Combine(workspaceRoot, "editor-cache");
        return IsCurrentTerrainTexturePreviewManifest(TerrainTexturePreviewDirectory(cacheDir, levelKey));
    }

    public static bool TryLoadNativeTerrainLqTextureCache(
        string workspaceRoot,
        string levelKey,
        out NativeTerrainLqTextureSet? textureSet)
    {
        textureSet = null;
        if (string.IsNullOrWhiteSpace(workspaceRoot) || string.IsNullOrWhiteSpace(levelKey))
            return false;

        try
        {
            string cacheDir = Path.Combine(workspaceRoot, "editor-cache");
            string directory = TerrainTexturePreviewDirectory(cacheDir, levelKey);
            return TryLoadNativeTerrainLqTextureCacheFromDirectory(directory, out textureSet);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            textureSet = null;
            return false;
        }
    }

    public static bool TryLoadNativeTerrainHqMaterialCache(
        string workspaceRoot,
        string levelKey,
        out NativeTerrainHqMaterialSet? materialSet)
    {
        materialSet = null;
        if (string.IsNullOrWhiteSpace(workspaceRoot) || string.IsNullOrWhiteSpace(levelKey))
            return false;

        try
        {
            string cacheDir = Path.Combine(workspaceRoot, "editor-cache");
            string directory = TerrainTexturePreviewDirectory(cacheDir, levelKey);
            return TryLoadNativeTerrainHqMaterialCacheFromDirectory(directory, out materialSet);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            materialSet = null;
            return false;
        }
    }

    private static bool IsCurrentTerrainTexturePreviewManifest(string directory)
    {
        return TryLoadNativeTerrainLqTextureCacheFromDirectory(
                directory,
                out NativeTerrainLqTextureSet? lowDetailTextures) &&
            lowDetailTextures != null &&
            TryLoadNativeTerrainHqMaterialCacheFromDirectory(
                directory,
                out NativeTerrainHqMaterialSet? highDetailMaterials) &&
            highDetailMaterials != null &&
            TryValidateNativeTerrainHqTextureCacheFromDirectory(directory, lowDetailTextures, highDetailMaterials);
    }

    public static bool TryLoadNativeTerrainLqTextureCacheFromDirectory(
        string directory,
        out NativeTerrainLqTextureSet? textureSet)
    {
        textureSet = null;
        if (string.IsNullOrWhiteSpace(directory))
            return false;

        try
        {
            string path = Path.Combine(directory, "manifest.json");
            if (!File.Exists(path))
                return false;
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            return JsonValue.GetInt32(document.RootElement, "cacheFormatVersion", 0) == TerrainTexturePreviewCacheFormatVersion &&
                string.Equals(
                    JsonValue.GetString(document.RootElement, "decoder"),
                    TerrainTexturePreviewDecoder,
                    StringComparison.Ordinal) &&
                TryReadLowDetailTextureCache(document.RootElement, directory, out textureSet);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            textureSet = null;
            return false;
        }
    }

    public static bool TryLoadNativeTerrainHqMaterialCacheFromDirectory(
        string directory,
        out NativeTerrainHqMaterialSet? materialSet)
    {
        materialSet = null;
        if (string.IsNullOrWhiteSpace(directory))
            return false;

        try
        {
            string path = Path.Combine(directory, "manifest.json");
            if (!File.Exists(path))
                return false;
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            string manifestLevelKey = LevelCatalog.NormalizeKey(JsonValue.GetString(root, "levelKey"));
            string directoryLevelKey = LevelCatalog.NormalizeKey(Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)));
            if (JsonValue.GetInt32(root, "cacheFormatVersion", 0) != TerrainTexturePreviewCacheFormatVersion ||
                !string.Equals(JsonValue.GetString(root, "decoder"), TerrainTexturePreviewDecoder, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(manifestLevelKey) ||
                !string.Equals(manifestLevelKey, directoryLevelKey, StringComparison.OrdinalIgnoreCase) ||
                !TryReadLowDetailTextureCache(root, directory, out NativeTerrainLqTextureSet? lowDetailTextures) ||
                lowDetailTextures == null)
            {
                return false;
            }
            return TryReadHighDetailMaterialCache(root, directory, lowDetailTextures, out materialSet);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or ArgumentException or InvalidDataException or OverflowException or NotSupportedException)
        {
            materialSet = null;
            return false;
        }
    }

    private static bool TryValidateNativeTerrainHqTextureCacheFromDirectory(
        string directory,
        NativeTerrainLqTextureSet lowDetailTextures,
        NativeTerrainHqMaterialSet highDetailMaterials)
    {
        try
        {
            string manifestPath = Path.Combine(directory, "manifest.json");
            using FileStream stream = File.OpenRead(manifestPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            int nativeTextureCount = JsonValue.GetInt32(root, "nativeTextureCount", -1);
            string levelKey = LevelCatalog.NormalizeKey(JsonValue.GetString(root, "levelKey"));
            HashSet<int> expectedTextureIds = lowDetailTextures.Textures
                .Select(texture => texture.TextureId)
                .ToHashSet();
            if (nativeTextureCount <= 0 || expectedTextureIds.Count != nativeTextureCount ||
                !expectedTextureIds.SetEquals(Enumerable.Range(0, nativeTextureCount)) ||
                highDetailMaterials.NativeTextureCount != nativeTextureCount ||
                highDetailMaterials.TextureCount != nativeTextureCount ||
                !expectedTextureIds.SetEquals(highDetailMaterials.Textures.Select(texture => texture.TextureId)) ||
                !string.Equals(levelKey, Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)), StringComparison.OrdinalIgnoreCase) ||
                !root.TryGetProperty("textures", out JsonElement textures) || textures.ValueKind != JsonValueKind.Array ||
                textures.GetArrayLength() != checked(nativeTextureCount * 2))
            {
                return false;
            }

            string directoryPrefix = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            HashSet<(int TextureId, string Tier)> frameKeys = [];
            foreach (JsonElement texture in textures.EnumerateArray())
            {
                int textureId = JsonValue.GetInt32(texture, "textureId", -1);
                string descriptorTier = JsonValue.GetString(texture, "descriptorTier");
                string previewTier = JsonValue.GetString(texture, "previewTier");
                string suffix = descriptorTier switch
                {
                    "hqData" => "normal",
                    "hqDataClose" => "close",
                    _ => ""
                };
                int width = JsonValue.GetInt32(texture, "width", -1);
                int height = JsonValue.GetInt32(texture, "height", -1);
                int descriptorCount = JsonValue.GetInt32(texture, "descriptorCount", -1);
                int pixelCount = JsonValue.GetInt32(texture, "pixelCount", -1);
                long fileByteLength = JsonValue.GetInt64(texture, "fileByteLength", -1);
                string fileSha256 = JsonValue.GetString(texture, "fileSha256");
                string file = JsonValue.GetString(texture, "file");
                string expectedFile = $"{levelKey}-texture-{textureId:000}-{suffix}.png";
                if (!highDetailMaterials.TryGetTexture(textureId, out NativeTerrainHqMaterialRecordPayload? material))
                    return false;
                NativeTerrainHqMaterialTierPayload? materialTier = descriptorTier switch
                {
                    "hqData" => material.Normal,
                    "hqDataClose" => material.Close,
                    _ => null
                };
                if (!expectedTextureIds.Contains(textureId) || string.IsNullOrEmpty(suffix) ||
                    materialTier == null || width != materialTier.CompositeSide || height != materialTier.CompositeSide ||
                    !frameKeys.Add((textureId, descriptorTier)) ||
                    !string.Equals(previewTier, suffix, StringComparison.Ordinal) ||
                    width <= 0 || height <= 0 || pixelCount != checked(width * height) ||
                    descriptorCount != (descriptorTier == "hqData" ? 4 : 16) ||
                    !string.Equals(file, expectedFile, StringComparison.Ordinal) || Path.GetFileName(file) != file)
                {
                    return false;
                }

                string path = Path.GetFullPath(Path.Combine(directory, file));
                if (!path.StartsWith(directoryPrefix, StringComparison.Ordinal) ||
                    !IsNativeTerrainTexturePreviewFrameValid(
                        path,
                        width,
                        height,
                        fileByteLength,
                        fileSha256))
                {
                    return false;
                }
            }

            return expectedTextureIds.All(textureId =>
                frameKeys.Contains((textureId, "hqData")) &&
                frameKeys.Contains((textureId, "hqDataClose")));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidDataException or OverflowException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryReadHighDetailMaterialCache(
        JsonElement root,
        string directory,
        NativeTerrainLqTextureSet lowDetailTextures,
        out NativeTerrainHqMaterialSet? materialSet)
    {
        materialSet = null;
        try
        {
            string file = JsonValue.GetString(root, "nativeMaterialFile");
            int nativeTextureCount = JsonValue.GetInt32(root, "nativeMaterialNativeTextureCount", -1);
            int recordCount = JsonValue.GetInt32(root, "nativeMaterialDecodedTextureCount", -1);
            int requestedCount = JsonValue.GetInt32(root, "nativeMaterialRequestedTextureCount", -1);
            int descriptorCount = JsonValue.GetInt32(root, "nativeMaterialDescriptorCount", -1);
            int compositeCount = JsonValue.GetInt32(root, "nativeMaterialCompositeCount", -1);
            int rawWordCount = JsonValue.GetInt32(root, "nativeMaterialRawWordCount", -1);
            int zeroWordCount = JsonValue.GetInt32(root, "nativeMaterialZeroWordCount", -1);
            int stpSetWordCount = JsonValue.GetInt32(root, "nativeMaterialStpSetWordCount", -1);
            if (!string.Equals(JsonValue.GetString(root, "nativeMaterialFormat"), NativeTerrainHqMaterialCacheCodec.MaterialFormat, StringComparison.Ordinal) ||
                !string.Equals(JsonValue.GetString(root, "nativeMaterialStateSemantics"), TerrainTexturePreviewStateSemantics, StringComparison.Ordinal) ||
                !string.Equals(file, NativeTerrainHqMaterialCacheCodec.PayloadFileName, StringComparison.Ordinal) ||
                Path.GetFileName(file) != file || nativeTextureCount <= 0 ||
                nativeTextureCount != lowDetailTextures.NativeTextureCount || requestedCount != nativeTextureCount ||
                recordCount != nativeTextureCount || descriptorCount != checked(nativeTextureCount * NativeTerrainHqMaterialCacheCodec.DescriptorCountPerTexture) ||
                compositeCount != checked(nativeTextureCount * 2) || rawWordCount <= 0 ||
                zeroWordCount < 0 || zeroWordCount > rawWordCount ||
                stpSetWordCount < 0 || stpSetWordCount > rawWordCount ||
                JsonValue.GetInt32(root, "nativeMaterialDescriptorCountPerTexture", -1) != NativeTerrainHqMaterialCacheCodec.DescriptorCountPerTexture ||
                JsonValue.GetInt32(root, "nativeMaterialNormalDescriptorCount", -1) != NativeTerrainHqMaterialCacheCodec.NormalDescriptorCount ||
                JsonValue.GetInt32(root, "nativeMaterialCloseDescriptorCount", -1) != NativeTerrainHqMaterialCacheCodec.CloseDescriptorCount ||
                JsonValue.GetInt32(root, "nativeMaterialRawWordByteCount", -1) != NativeTerrainHqMaterialCacheCodec.RawWordByteCount ||
                !TryGetRequiredBoolean(root, "rawPsx555Preserved", out bool rawPreserved) || !rawPreserved ||
                !TryGetRequiredBoolean(root, "zeroWordTransparencyPreserved", out bool zeroPreserved) || !zeroPreserved ||
                !TryGetRequiredBoolean(root, "stpPreserved", out bool stpPreserved) || !stpPreserved ||
                !TryGetRequiredBoolean(root, "descriptorAbrPreserved", out bool abrPreserved) || !abrPreserved ||
                !root.TryGetProperty("hqMaterials", out JsonElement materials) || materials.ValueKind != JsonValueKind.Array ||
                materials.GetArrayLength() != nativeTextureCount)
            {
                return false;
            }

            NativeTerrainHqMaterialCacheProof proof = new(
                JsonValue.GetInt32(root, "nativeMaterialPayloadFormatVersion", -1),
                JsonValue.GetInt32(root, "nativeMaterialHeaderSize", -1),
                JsonValue.GetInt32(root, "nativeMaterialDescriptorHeaderSize", -1),
                JsonValue.GetInt64(root, "nativeMaterialPayloadByteLength", -1),
                JsonValue.GetString(root, "nativeMaterialPayloadSha256"),
                nativeTextureCount,
                recordCount,
                descriptorCount,
                compositeCount,
                rawWordCount,
                zeroWordCount,
                stpSetWordCount,
                JsonValue.GetInt32(root, "nativeMaterialTexturePagesByteLength", -1),
                JsonValue.GetInt32(root, "nativeMaterialTextureComponentByteLength", -1),
                JsonValue.GetString(root, "nativeMaterialTexturePagesSha256"),
                JsonValue.GetString(root, "nativeMaterialOriginalTextureComponentSha256"),
                JsonValue.GetString(root, "nativeMaterialInitializedTextureComponentSha256"),
                JsonValue.GetString(root, "nativeMaterialInitializedHqTableSha256"),
                JsonValue.GetString(root, "nativeMaterialContentSha256"));
            string path = Path.GetFullPath(Path.Combine(directory, file));
            string directoryPrefix = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(directoryPrefix, StringComparison.Ordinal))
                return false;

            NativeTerrainHqMaterialCacheReadResult read = NativeTerrainHqMaterialCacheCodec.ReadValidated(path, proof);
            if (!read.Complete || read.MaterialSet == null)
                return false;
            NativeTerrainHqMaterialSet set = read.MaterialSet;
            if (set.TextureCount != nativeTextureCount || set.NativeTextureCount != lowDetailTextures.NativeTextureCount ||
                set.TexturePagesByteLength != lowDetailTextures.TexturePagesByteLength ||
                !string.Equals(set.TexturePagesSha256, lowDetailTextures.TexturePagesSha256, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(set.OriginalTextureComponentSha256, lowDetailTextures.OriginalTextureComponentSha256, StringComparison.OrdinalIgnoreCase) ||
                !TryReadExactIntArray(root, "nativeMaterialAbrDescriptorCounts", set.AbrDescriptorCounts))
            {
                return false;
            }

            Dictionary<int, NativeTerrainHqMaterialRecordPayload> records = set.Textures.ToDictionary(texture => texture.TextureId);
            HashSet<int> manifestIds = [];
            foreach (JsonElement material in materials.EnumerateArray())
            {
                int textureId = JsonValue.GetInt32(material, "textureId", -1);
                if (!manifestIds.Add(textureId) || !records.TryGetValue(textureId, out NativeTerrainHqMaterialRecordPayload? record) ||
                    JsonValue.GetInt32(material, "descriptorCount", -1) != NativeTerrainHqMaterialCacheCodec.DescriptorCountPerTexture ||
                    JsonValue.GetInt32(material, "compositeCount", -1) != 2 ||
                    JsonValue.GetInt32(material, "rawWordCount", -1) != record.Normal.RawWordCount + record.Close.RawWordCount ||
                    JsonValue.GetInt32(material, "zeroWordCount", -1) != record.Normal.ZeroWordCount + record.Close.ZeroWordCount ||
                    JsonValue.GetInt32(material, "stpSetWordCount", -1) != record.Normal.StpSetWordCount + record.Close.StpSetWordCount ||
                    !material.TryGetProperty("normal", out JsonElement normal) ||
                    !material.TryGetProperty("close", out JsonElement close) ||
                    !TryValidateHighDetailMaterialTierManifest(normal, record.Normal) ||
                    !TryValidateHighDetailMaterialTierManifest(close, record.Close))
                {
                    return false;
                }
            }
            if (!manifestIds.SetEquals(records.Keys))
                return false;

            materialSet = set;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidDataException or OverflowException)
        {
            materialSet = null;
            return false;
        }
    }

    private static bool TryValidateHighDetailMaterialTierManifest(
        JsonElement manifest,
        NativeTerrainHqMaterialTierPayload tier)
    {
        byte[] descriptorBytes = tier.Descriptors.SelectMany(descriptor => descriptor.RawDescriptor.ToArray()).ToArray();
        byte[] rawWordBytes = tier.Descriptors.SelectMany(descriptor => descriptor.RawWordsLittleEndian.ToArray()).ToArray();
        return manifest.ValueKind == JsonValueKind.Object &&
            string.Equals(JsonValue.GetString(manifest, "descriptorTier"), tier.TierName, StringComparison.Ordinal) &&
            JsonValue.GetInt32(manifest, "gridColumns", -1) == tier.GridColumns &&
            JsonValue.GetInt32(manifest, "tileSide", -1) == tier.TileSide &&
            JsonValue.GetInt32(manifest, "compositeSide", -1) == tier.CompositeSide &&
            JsonValue.GetInt32(manifest, "descriptorCount", -1) == tier.DescriptorCount &&
            JsonValue.GetInt32(manifest, "rawWordCount", -1) == tier.RawWordCount &&
            JsonValue.GetInt32(manifest, "zeroWordCount", -1) == tier.ZeroWordCount &&
            JsonValue.GetInt32(manifest, "stpSetWordCount", -1) == tier.StpSetWordCount &&
            string.Equals(JsonValue.GetString(manifest, "compositeRawWordsSha256"), tier.CompositeRawWordsSha256, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(JsonValue.GetString(manifest, "rawDescriptorSetSha256"), Convert.ToHexString(SHA256.HashData(descriptorBytes)), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(JsonValue.GetString(manifest, "rawWordSetSha256"), Convert.ToHexString(SHA256.HashData(rawWordBytes)), StringComparison.OrdinalIgnoreCase) &&
            TryReadExactIntArray(
                manifest,
                "abrDescriptorCounts",
                Enumerable.Range(0, 4).Select(abr => tier.Descriptors.Count(descriptor => descriptor.Abr == abr)).ToArray());
    }

    private static bool TryReadExactIntArray(JsonElement root, string propertyName, IReadOnlyList<int> expected)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement values) || values.ValueKind != JsonValueKind.Array ||
            values.GetArrayLength() != expected.Count)
        {
            return false;
        }
        int index = 0;
        foreach (JsonElement value in values.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int number) || number != expected[index++])
                return false;
        }
        return true;
    }

    private static bool TryReadLowDetailTextureCache(
        JsonElement root,
        string directory,
        out NativeTerrainLqTextureSet? textureSet)
    {
        textureSet = null;
        try
        {
            string file = JsonValue.GetString(root, "lqPayloadFile");
            int recordCount = JsonValue.GetInt32(root, "lqDecodedTextureCount", -1);
            int requestedCount = JsonValue.GetInt32(root, "lqRequestedTextureCount", -1);
            int completeCount = JsonValue.GetInt32(root, "completeLqTextureCount", -1);
            int nativeTextureCount = JsonValue.GetInt32(root, "lqNativeTextureCount", -1);
            if (!string.Equals(JsonValue.GetString(root, "lqStateSemantics"), TerrainTexturePreviewStateSemantics, StringComparison.Ordinal) ||
                !string.Equals(file, NativeTerrainLqTextureCacheCodec.PayloadFileName, StringComparison.Ordinal) ||
                Path.GetFileName(file) != file ||
                JsonValue.GetInt32(root, "lqDescriptorCountPerTexture", -1) != NativeTerrainLqTextureCacheCodec.DescriptorCount ||
                JsonValue.GetInt32(root, "lqTextureSide", -1) != NativeTerrainLqTextureCacheCodec.TextureSide ||
                JsonValue.GetInt32(root, "lqBitsPerPixel", -1) != NativeTerrainLqTextureCacheCodec.BitsPerPixel ||
                JsonValue.GetInt32(root, "lqPackedIndexByteCount", -1) != NativeTerrainLqTextureCacheCodec.PackedIndexByteCount ||
                JsonValue.GetInt32(root, "lqPaletteRowCount", -1) != NativeTerrainLqTextureCacheCodec.PaletteRowCount ||
                JsonValue.GetInt32(root, "lqPaletteColorCount", -1) != NativeTerrainLqTextureCacheCodec.PaletteColorCount ||
                nativeTextureCount <= 0 || requestedCount != nativeTextureCount ||
                recordCount != nativeTextureCount || completeCount != nativeTextureCount ||
                !root.TryGetProperty("lqRetailAliasComplete", out JsonElement aliasComplete) || aliasComplete.ValueKind != JsonValueKind.True ||
                !root.TryGetProperty("lqTextures", out JsonElement lqTextures) || lqTextures.ValueKind != JsonValueKind.Array ||
                lqTextures.GetArrayLength() != recordCount ||
                !TryValidateNativeLoadStateManifest(root, nativeTextureCount, lqTextures))
            {
                return false;
            }

            NativeTerrainLqTextureCacheProof proof = new(
                JsonValue.GetInt32(root, "lqPayloadFormatVersion", -1),
                JsonValue.GetInt32(root, "lqPayloadRecordSize", -1),
                JsonValue.GetInt64(root, "lqPayloadByteLength", -1),
                JsonValue.GetString(root, "lqPayloadSha256"),
                nativeTextureCount,
                recordCount,
                JsonValue.GetInt32(root, "lqTexturePagesByteLength", -1),
                JsonValue.GetString(root, "lqTexturePagesSha256"),
                JsonValue.GetString(root, "lqOriginalTextureComponentSha256"),
                JsonValue.GetString(root, "lqInitializedTableSha256"),
                JsonValue.GetInt32(root, "lqRetailAliasCount", -1));
            string path = Path.GetFullPath(Path.Combine(directory, file));
            string directoryPrefix = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(directoryPrefix, StringComparison.Ordinal))
                return false;

            NativeTerrainLqTextureCacheReadResult read =
                NativeTerrainLqTextureCacheCodec.ReadValidated(path, proof);
            if (!read.Complete || read.TextureSet == null || read.TextureSet.TextureCount != recordCount)
                return false;

            Dictionary<int, NativeTerrainLqTextureRecordPayload> records = read.TextureSet.Textures
                .ToDictionary(texture => texture.TextureId);
            HashSet<int> manifestIds = [];
            foreach (JsonElement texture in lqTextures.EnumerateArray())
            {
                int textureId = JsonValue.GetInt32(texture, "textureId", -1);
                if (!manifestIds.Add(textureId) || !records.TryGetValue(textureId, out NativeTerrainLqTextureRecordPayload? record) ||
                    JsonValue.GetInt32(texture, "descriptorCount", -1) != NativeTerrainLqTextureCacheCodec.DescriptorCount ||
                    !texture.TryGetProperty("descriptors", out JsonElement descriptors) ||
                    descriptors.ValueKind != JsonValueKind.Array || descriptors.GetArrayLength() != NativeTerrainLqTextureCacheCodec.DescriptorCount)
                {
                    return false;
                }

                foreach (JsonElement descriptor in descriptors.EnumerateArray())
                {
                    int descriptorIndex = JsonValue.GetInt32(descriptor, "descriptorIndex", -1);
                    if (descriptorIndex is < 0 or >= NativeTerrainLqTextureCacheCodec.DescriptorCount)
                        return false;
                    NativeTerrainLqTextureDescriptorPayload payload = record.GetDescriptor(descriptorIndex);
                    if (!string.Equals(JsonValue.GetString(descriptor, "rawDescriptorHex"), Convert.ToHexString(payload.RawDescriptor.Span), StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(JsonValue.GetString(descriptor, "RawDescriptorSha256"), payload.RawDescriptorSha256, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(JsonValue.GetString(descriptor, "PackedIndicesSha256"), payload.PackedIndicesSha256, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(JsonValue.GetString(descriptor, "PaletteWordsSha256"), payload.PaletteWordsSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            if (!manifestIds.SetEquals(records.Keys))
                return false;
            textureSet = read.TextureSet;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidDataException or OverflowException)
        {
            textureSet = null;
            return false;
        }
    }

    private static bool TryValidateNativeLoadStateManifest(
        JsonElement root,
        int nativeTextureCount,
        JsonElement lqTextures)
    {
        if (!string.Equals(JsonValue.GetString(root, "stateSemantics"), TerrainTexturePreviewStateSemantics, StringComparison.Ordinal) ||
            !TryGetRequiredBoolean(root, "runtimeControlComplete", out bool controlComplete) || !controlComplete ||
            !TryGetRequiredBoolean(root, "runtimeControlInitialStateComplete", out bool initialStateComplete) || !initialStateComplete ||
            !TryGetRequiredBoolean(root, "runtimePlaybackIncluded", out bool playbackIncluded) || playbackIncluded ||
            !TryGetRequiredBoolean(root, "currentGameplayState", out bool currentGameplayState) || currentGameplayState ||
            JsonValue.GetInt32(root, "nativeTextureCount", -1) != nativeTextureCount ||
            JsonValue.GetInt32(root, "requestedTextureCount", -1) != nativeTextureCount ||
            JsonValue.GetInt32(root, "decodedTextureCount", -1) != nativeTextureCount ||
            JsonValue.GetInt32(root, "completeDualTierTextureCount", -1) != nativeTextureCount ||
            JsonValue.GetInt32(root, "decodedFrameCount", -1) != checked(nativeTextureCount * 2) ||
            JsonValue.GetInt32(root, "runtimeControlTargetWadEntry", -1) < 0 ||
            JsonValue.GetInt32(root, "runtimeControlSceneByteLength", -1) <= 0 ||
            !IsSha256Hex(JsonValue.GetString(root, "runtimeControlSceneSha256")) ||
            !root.TryGetProperty("runtimeControls", out JsonElement runtimeControls) || runtimeControls.ValueKind != JsonValueKind.Array ||
            JsonValue.GetInt32(root, "runtimeControlProgramCount", -1) != runtimeControls.GetArrayLength() ||
            !TryReadUniqueTextureIdSet(root, "runtimeControlledTextureIds", nativeTextureCount, out HashSet<int> controlledTextureIds) ||
            !TryReadUniqueTextureIdSet(root, "runtimeInitializedTextureIds", nativeTextureCount, out HashSet<int> initializedTextureIds) ||
            !controlledTextureIds.SetEquals(initializedTextureIds) ||
            JsonValue.GetInt32(root, "runtimeControlledTextureCount", -1) != controlledTextureIds.Count ||
            !TryReadUniqueTextureIdSet(root, "animationSourceTextureIds", nativeTextureCount, out HashSet<int> animationSourceTextureIds) ||
            JsonValue.GetInt32(root, "animationSourceDiagnosticTextureCount", -1) != animationSourceTextureIds.Count ||
            !TryReadUniqueTextureIdSet(root, "faceReferencedTextureIds", nativeTextureCount, out HashSet<int> faceReferencedTextureIds) ||
            JsonValue.GetInt32(root, "faceReferencedTextureCount", -1) != faceReferencedTextureIds.Count ||
            !TryReadUniqueTextureIdSet(root, "controlledWithoutFaceTextureIds", nativeTextureCount, out HashSet<int> controlledWithoutFaceTextureIds) ||
            JsonValue.GetInt32(root, "controlledWithoutFaceTextureCount", -1) != controlledWithoutFaceTextureIds.Count ||
            !controlledWithoutFaceTextureIds.SetEquals(controlledTextureIds.Where(textureId => !faceReferencedTextureIds.Contains(textureId))) ||
            !TryReadUniqueTextureIdSet(root, "nativeUnreferencedTextureIds", nativeTextureCount, out HashSet<int> nativeUnreferencedTextureIds) ||
            JsonValue.GetInt32(root, "nativeUnreferencedTextureCount", -1) != nativeUnreferencedTextureIds.Count)
        {
            return false;
        }

        HashSet<int> expectedNativeUnreferenced = Enumerable.Range(0, nativeTextureCount)
            .Where(textureId =>
                !faceReferencedTextureIds.Contains(textureId) &&
                !animationSourceTextureIds.Contains(textureId) &&
                !controlledTextureIds.Contains(textureId))
            .ToHashSet();
        if (!nativeUnreferencedTextureIds.SetEquals(expectedNativeUnreferenced) ||
            animationSourceTextureIds.Overlaps(faceReferencedTextureIds) ||
            animationSourceTextureIds.Overlaps(controlledTextureIds) ||
            nativeUnreferencedTextureIds.Overlaps(faceReferencedTextureIds) ||
            nativeUnreferencedTextureIds.Overlaps(animationSourceTextureIds) ||
            nativeUnreferencedTextureIds.Overlaps(controlledTextureIds))
        {
            return false;
        }

        Dictionary<int, RuntimeControlManifestProof> controlsByDestination = [];
        foreach (JsonElement control in runtimeControls.EnumerateArray())
        {
            string kind = JsonValue.GetString(control, "runtimeKind");
            int controlId = JsonValue.GetInt32(control, "controlId", -1);
            int pointerIndex = JsonValue.GetInt32(control, "pointerIndex", -1);
            int sceneOffset = JsonValue.GetInt32(control, "sceneRelativeStructureOffset", -1);
            int destinationTextureId = JsonValue.GetInt32(control, "destinationTextureId", -1);
            int initialCurrentFrame = JsonValue.GetInt32(control, "initialCurrentFrame", -1);
            int initialTicksRemaining = JsonValue.GetInt32(control, "initialTicksRemaining", -1);
            if (kind is not ("fullRecordAnimation" or "scrollingDescriptor") ||
                !string.Equals(JsonValue.GetString(control, "stateSemantics"), TerrainTexturePreviewStateSemantics, StringComparison.Ordinal) ||
                controlId < 0 || pointerIndex < 0 || sceneOffset < 0 || initialTicksRemaining < 0 ||
                destinationTextureId < 0 || destinationTextureId >= nativeTextureCount ||
                string.IsNullOrWhiteSpace(JsonValue.GetString(control, "rawBytesHex")) ||
                controlsByDestination.ContainsKey(destinationTextureId) ||
                !control.TryGetProperty("frames", out JsonElement frames) || frames.ValueKind != JsonValueKind.Array || frames.GetArrayLength() <= 0 ||
                !TryGetNullableInt32(control, "initialSourceTextureId", out int? initialSource) ||
                !TryGetNullableInt32(control, "initialPhase", out int? initialPhase))
            {
                return false;
            }

            if (kind == "fullRecordAnimation")
            {
                if (!initialSource.HasValue || initialSource.Value < 0 || initialSource.Value >= nativeTextureCount || initialPhase.HasValue)
                    return false;
            }
            else if (initialSource.HasValue || !initialPhase.HasValue || initialPhase.Value is < 0 or > 0x7F)
            {
                return false;
            }

            int frameCount = frames.GetArrayLength();
            if (initialCurrentFrame < 0 || initialCurrentFrame >= frameCount)
                return false;
            HashSet<int> frameIndexes = [];
            int? selectedFrameSource = null;
            foreach (JsonElement frame in frames.EnumerateArray())
            {
                int frameIndex = JsonValue.GetInt32(frame, "frameIndex", -1);
                int forward = JsonValue.GetInt32(frame, "forwardNextFrame", -1);
                int reverse = JsonValue.GetInt32(frame, "reverseNextFrame", -1);
                if (frameIndex < 0 || frameIndex >= frameCount || !frameIndexes.Add(frameIndex) ||
                    forward < 0 || forward >= frameCount || reverse < 0 || reverse >= frameCount ||
                    !TryGetNullableInt32(frame, "sourceTextureId", out int? frameSource) ||
                    !TryGetNullableInt32(frame, "phaseDelta", out int? phaseDelta))
                {
                    return false;
                }

                if (kind == "fullRecordAnimation")
                {
                    if (!frameSource.HasValue || frameSource.Value < 0 || frameSource.Value >= nativeTextureCount || phaseDelta.HasValue)
                        return false;
                    if (frameIndex == initialCurrentFrame)
                        selectedFrameSource = frameSource;
                }
                else if (frameSource.HasValue || !phaseDelta.HasValue || phaseDelta.Value is < sbyte.MinValue or > sbyte.MaxValue)
                {
                    return false;
                }
            }
            if (frameIndexes.Count != frameCount ||
                (kind == "fullRecordAnimation" && selectedFrameSource != initialSource))
                return false;

            controlsByDestination[destinationTextureId] = new RuntimeControlManifestProof(
                kind,
                controlId,
                pointerIndex,
                initialSource,
                initialPhase);
        }
        if (!controlledTextureIds.SetEquals(controlsByDestination.Keys))
            return false;

        if (!root.TryGetProperty("initializationMutations", out JsonElement mutations) ||
            mutations.ValueKind != JsonValueKind.Array || mutations.GetArrayLength() != runtimeControls.GetArrayLength())
        {
            return false;
        }

        HashSet<int> mutationDestinations = [];
        foreach (JsonElement mutation in mutations.EnumerateArray())
        {
            string kind = JsonValue.GetString(mutation, "runtimeKind");
            int controlId = JsonValue.GetInt32(mutation, "ControlId", -1);
            int pointerIndex = JsonValue.GetInt32(mutation, "PointerIndex", -1);
            int destinationTextureId = JsonValue.GetInt32(mutation, "DestinationTextureId", -1);
            int lqChanged = JsonValue.GetInt32(mutation, "LqChangedByteCount", -1);
            int hqChanged = JsonValue.GetInt32(mutation, "HqChangedByteCount", -1);
            string originalLqSha256 = JsonValue.GetString(mutation, "OriginalLqSha256");
            string initializedLqSha256 = JsonValue.GetString(mutation, "InitializedLqSha256");
            string originalHqSha256 = JsonValue.GetString(mutation, "OriginalHqSha256");
            string initializedHqSha256 = JsonValue.GetString(mutation, "InitializedHqSha256");
            if (!TryGetNullableInt32(mutation, "InitialSourceTextureId", out int? initialSource) ||
                !TryGetNullableInt32(mutation, "InitialPhase", out int? initialPhase) ||
                kind is not ("fullRecordAnimation" or "scrollingDescriptor") ||
                controlId < 0 || pointerIndex < 0 || destinationTextureId < 0 || destinationTextureId >= nativeTextureCount ||
                lqChanged is < 0 or > 16 || hqChanged is < 0 or > 168 ||
                !IsSha256Hex(originalLqSha256) || !IsSha256Hex(initializedLqSha256) ||
                !IsSha256Hex(originalHqSha256) || !IsSha256Hex(initializedHqSha256) ||
                !mutationDestinations.Add(destinationTextureId) ||
                !controlsByDestination.TryGetValue(destinationTextureId, out RuntimeControlManifestProof? control) ||
                !string.Equals(kind, control.Kind, StringComparison.Ordinal) || controlId != control.ControlId ||
                pointerIndex != control.PointerIndex || initialSource != control.InitialSource || initialPhase != control.InitialPhase)
            {
                return false;
            }
            if (kind == "fullRecordAnimation")
            {
                if (lqChanged != 0 || hqChanged != 0 ||
                    !string.Equals(originalLqSha256, initializedLqSha256, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(originalHqSha256, initializedHqSha256, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            else if (lqChanged + hqChanged <= 0 ||
                     (lqChanged == 0) != string.Equals(originalLqSha256, initializedLqSha256, StringComparison.OrdinalIgnoreCase) ||
                     (hqChanged == 0) != string.Equals(originalHqSha256, initializedHqSha256, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        if (!initializedTextureIds.SetEquals(mutationDestinations))
            return false;

        HashSet<int> rowIds = [];
        foreach (JsonElement texture in lqTextures.EnumerateArray())
        {
            int textureId = JsonValue.GetInt32(texture, "textureId", -1);
            if (textureId < 0 || textureId >= nativeTextureCount || !rowIds.Add(textureId) ||
                !TryGetRequiredBoolean(texture, "runtimeControlled", out bool runtimeControlled) ||
                !TryGetRequiredBoolean(texture, "runtimeInitializationApplied", out bool runtimeInitializationApplied) ||
                !TryGetRequiredBoolean(texture, "animationSourceDiagnostic", out bool animationSourceDiagnostic) ||
                !TryGetRequiredBoolean(texture, "faceReferenced", out bool faceReferenced) ||
                !TryGetRequiredBoolean(texture, "nativeUnreferenced", out bool nativeUnreferenced))
            {
                return false;
            }

            bool expectedControlled = controlledTextureIds.Contains(textureId);
            bool expectedSource = animationSourceTextureIds.Contains(textureId);
            bool expectedFace = faceReferencedTextureIds.Contains(textureId);
            bool expectedUnreferenced = nativeUnreferencedTextureIds.Contains(textureId);
            string expectedState = expectedControlled
                ? TerrainTexturePreviewStateSemantics
                : expectedSource
                    ? "animationSourceDiagnostic"
                    : "staticRecord";
            string expectedRole = expectedControlled
                ? expectedFace
                    ? "controlledFaceDestination"
                    : "controlledDestinationWithoutFace"
                : expectedSource
                    ? "animationSourceDiagnostic"
                    : expectedFace
                        ? "faceReferencedStatic"
                        : "nativeUnreferencedStatic";
            if (runtimeControlled != expectedControlled || runtimeInitializationApplied != expectedControlled ||
                animationSourceDiagnostic != expectedSource || faceReferenced != expectedFace ||
                nativeUnreferenced != expectedUnreferenced ||
                !string.Equals(JsonValue.GetString(texture, "previewState"), expectedState, StringComparison.Ordinal) ||
                !string.Equals(JsonValue.GetString(texture, "runtimeRole"), expectedRole, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return rowIds.SetEquals(Enumerable.Range(0, nativeTextureCount));
    }

    private static bool TryReadUniqueTextureIdSet(
        JsonElement root,
        string propertyName,
        int nativeTextureCount,
        out HashSet<int> result)
    {
        result = [];
        if (!root.TryGetProperty(propertyName, out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return false;
        foreach (JsonElement value in values.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int textureId) ||
                textureId < 0 || textureId >= nativeTextureCount || !result.Add(textureId))
            {
                result.Clear();
                return false;
            }
        }
        return true;
    }

    private static bool TryGetRequiredBoolean(JsonElement root, string propertyName, out bool result)
    {
        result = false;
        if (!root.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return false;
        result = value.GetBoolean();
        return true;
    }

    private static bool TryGetNullableInt32(JsonElement root, string propertyName, out int? result)
    {
        result = null;
        if (!root.TryGetProperty(propertyName, out JsonElement value))
            return false;
        if (value.ValueKind == JsonValueKind.Null)
            return true;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int number))
            return false;
        result = number;
        return true;
    }

    private static bool IsSha256Hex(string value) =>
        value.Length == 64 && value.All(character => Uri.IsHexDigit(character));

    private static int CountTerrainTexturePreviewFiles(string cacheDir, string levelKey)
    {
        string directory = TerrainTexturePreviewDirectory(cacheDir, levelKey);
        if (!Directory.Exists(directory) || !IsCurrentTerrainTexturePreviewManifest(directory))
            return 0;

        try
        {
            using FileStream stream = File.OpenRead(Path.Combine(directory, "manifest.json"));
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("textures", out JsonElement textures) ||
                textures.ValueKind is not JsonValueKind.Array)
            {
                return 0;
            }

            HashSet<int> textureIds = [];
            foreach (JsonElement texture in textures.EnumerateArray())
            {
                if (!texture.TryGetProperty("textureId", out JsonElement textureIdElement) ||
                    !textureIdElement.TryGetInt32(out int textureId) ||
                    textureId < 0 ||
                    !texture.TryGetProperty("file", out JsonElement fileElement) ||
                    fileElement.ValueKind is not JsonValueKind.String)
                {
                    return 0;
                }

                string? file = fileElement.GetString();
                if (string.IsNullOrWhiteSpace(file) || !File.Exists(Path.Combine(directory, file)))
                    return 0;
                textureIds.Add(textureId);
            }

            return textureIds.Count;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static string TerrainTexturePreviewFileName(string levelKey, TerrainTextureImageExport export)
    {
        NativeTerrainTexturePreviewTier tier = NativeTerrainTexturePreviewLod.FromDescriptorTier(export.DescriptorTier);
        return $"{levelKey}-texture-{export.TextureId:000}-{NativeTerrainTexturePreviewLod.FileSuffix(tier)}.png";
    }

    private static async Task WriteIndexAsync(
        string cacheDir,
        LevelCatalog catalog,
        IReadOnlyList<object> summary,
        int entryPoseCount,
        CancellationToken cancellationToken)
    {
        var index = new
        {
            generatedBy = "Spyro.Editor.Core",
            generatedAt = DateTime.Now.ToString("o"),
            purpose = "Portable editor cache index for the Mac/Windows native editor.",
            levelCount = catalog.Levels.Count,
            levelEntryPoseContract = PortableLevelEntryPoseCache.Contract,
            levelEntryPoseCount = entryPoseCount,
            levelEntryPoseCache = entryPoseCount == catalog.Levels.Count
                ? $"editor-cache/{PortableLevelEntryPoseCache.FileName}"
                : "",
            levels = summary
        };

        await using FileStream indexStream = File.Create(Path.Combine(cacheDir, "index.json"));
        await JsonSerializer.SerializeAsync(indexStream, index, cancellationToken: cancellationToken);
    }

    private static async Task EnsureWadAnalysisAsync(EditorWorkspace workspace, CancellationToken cancellationToken)
    {
        string sourceImage = DiscImageLocator.FindImage(workspace);
        if (!File.Exists(sourceImage))
            return;

        string wadAnalysis = WadAnalysisLocator.Find(workspace);
        if (File.Exists(wadAnalysis))
            return;

        await WadAnalysisBuilder.BuildAsync(sourceImage, Path.Combine(workspace.RootPath, "spyro-wad-analysis.json"), cancellationToken: cancellationToken);
    }

    private static string FindRamPath(EditorWorkspace workspace, string levelKey)
    {
        string[] candidates = levelKey == "stonehill"
            ? ["stonehill-before-clean.bin", "stonehill-before-gem-clean.bin", "duckstation-mainram-fresh-stonehill.bin"]
            : [$"{levelKey}-before-clean.bin"];

        foreach (string candidate in candidates)
        {
            string path = workspace.ResolveFile(candidate, "game-and-capture-artifacts");
            if (File.Exists(path))
                return path;
        }

        return Path.Combine(workspace.RootPath, candidates[0]);
    }

    private static bool TryBuildSourceMobyCache(EditorWorkspace workspace, LevelDefinition level, string outputPath)
    {
        if (!level.HasSourceTable)
            return false;

        string sourceImage = DiscImageLocator.FindImage(workspace);
        if (!File.Exists(sourceImage))
            return false;

        try
        {
            SourceMobyCacheBuilder.BuildAsync(sourceImage, level, outputPath).GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException or EndOfStreamException)
        {
            return false;
        }
    }

    private static bool TryBuildSourceOverlay(EditorWorkspace workspace, LevelDefinition level, string outputPath, out string overlayHealth)
    {
        overlayHealth = "";
        if (level.SourceWadEntry < 0)
            return false;

        string sourceImage = DiscImageLocator.FindImage(workspace);
        string wadAnalysis = WadAnalysisLocator.Find(workspace);
        if (!File.Exists(sourceImage) || !File.Exists(wadAnalysis))
            return false;

        try
        {
            SourceSceneOverlayResult result = SourceSceneOverlayExporter.Export(sourceImage, wadAnalysis, level, outputPath);
            overlayHealth = $"Recovered from source WAD entry {result.WadEntry}: {result.SectorCount} sectors, {result.PolygonCount} faces.";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException or EndOfStreamException)
        {
            overlayHealth = $"Source-derived overlay recovery failed: {ex.Message}";
            return false;
        }
    }

    private static async Task WriteMobyCacheAsync(string path, LevelDefinition level, string ramPath, IReadOnlyList<Moby> mobys, CancellationToken cancellationToken)
    {
        var root = new
        {
            generatedBy = "Spyro.Editor.Core",
            generatedAt = DateTime.Now.ToString("o"),
            purpose = "Portable native editor moby cache decoded from a clean DuckStation RAM capture.",
            levelKey = level.Key,
            displayName = level.DisplayName,
            levelId = level.LevelId,
            sourceRamFile = Path.GetFileName(ramPath),
            recordStride = "0x58",
            mobyCount = mobys.Count,
            mobys = mobys.Select(moby => new
            {
                moby.Index,
                moby.TrueIndex,
                moby.LegacyIndex,
                x = Math.Round(moby.Position.X, 4),
                y = Math.Round(moby.Position.Y, 4),
                z = Math.Round(moby.Position.Z, 4),
                rawX = ToRawCoordinate(moby.Position.X),
                rawY = ToRawCoordinate(moby.Position.Y),
                rawZ = ToRawCoordinate(moby.Position.Z),
                typeHex = $"0x{moby.Type:X2}",
                stateHex = $"0x{moby.State:X2}",
                runtimeAddress = $"0x{moby.RuntimeAddress:X8}",
                specialDataPointer = $"0x{moby.SpecialDataPointer:X8}",
                sourceByte36Hex = $"0x{moby.SourceByte36:X2}",
                sourceByte37Hex = $"0x{moby.SourceByte37:X2}",
                sourceByte4FHex = $"0x{moby.SourceByte4F:X2}",
                flag4AHex = $"0x{moby.Flag4A:X2}",
                flag4BHex = $"0x{moby.Flag4B:X2}"
            })
        };

        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, root, cancellationToken: cancellationToken);
    }

    private static int ToRawCoordinate(float value)
    {
        return (int)Math.Round(value * 16f);
    }

    private sealed record RuntimeControlManifestProof(
        string Kind,
        int ControlId,
        int PointerIndex,
        int? InitialSource,
        int? InitialPhase);

    private readonly record struct NativeTerrainTexturePreviewFrameProof(
        int Width,
        int Height,
        long ByteLength,
        string Sha256);
}

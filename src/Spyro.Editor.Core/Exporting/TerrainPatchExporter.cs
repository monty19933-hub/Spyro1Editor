using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core;
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
        TextureRecordIndex textureIndex = DecodeTextureRecords(modelBytes);
        return textureIndex.Records
            .Select(record =>
            {
                int normalCount = record.HqData.Count(descriptor => CanPatchTextureDescriptor(descriptor, texturePagesInfo.SubfileSize));
                int closeCount = record.HqDataClose.Count(descriptor => CanPatchTextureDescriptor(descriptor, texturePagesInfo.SubfileSize));
                return new TerrainTextureSlot(
                    TextureId: record.TextureId,
                    HasNormalDescriptors: normalCount > 0,
                    HasCloseDescriptors: closeCount > 0,
                    NormalDescriptorCount: normalCount,
                    CloseDescriptorCount: closeCount);
            })
            .ToArray();
    }

    public static async Task<TerrainTextureImageExport?> TryExportTerrainTextureImageAsync(
        string sourceImagePath,
        LevelDefinition level,
        int textureId,
        string outputPath,
        CancellationToken cancellationToken = default)
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

        (string Tier, int TileSize, int TileGridColumns, IReadOnlyList<TextureDescriptor> Descriptors)? tier = ChooseReadableTextureDescriptorTier(record, texturePagesInfo.SubfileSize);
        if (tier == null)
            return null;

        string descriptorTier = tier.Value.Tier;
        int tileSize = tier.Value.TileSize;
        int tileGridColumns = tier.Value.TileGridColumns;
        IReadOnlyList<TextureDescriptor> descriptors = tier.Value.Descriptors;
        Rgba32[] pixels = new Rgba32[tileSize * tileSize];
        bool[] written = new bool[pixels.Length];
        int copiedPixels = 0;
        int descriptorCount = 0;
        foreach (TextureDescriptor descriptor in descriptors)
        {
            if (!CanPatchTextureDescriptor(descriptor, texturePagesInfo.SubfileSize))
                continue;

            int paletteByteStart = descriptor.PaletteByteStart;
            if (paletteByteStart < 0 || paletteByteStart + 512 > texturePages.Length)
                continue;

            int tile = descriptor.Index;
            int destTileX = (tile % tileGridColumns) * 32;
            int destTileY = (int)Math.Floor(tile / (double)tileGridColumns) * 32;
            if (destTileX < 0 || destTileY < 0 || destTileX + 32 > tileSize || destTileY + 32 > tileSize)
                continue;

            int[] matrix = TextureDescriptorMatrices[Math.Clamp(descriptor.Orientation, 0, TextureDescriptorMatrices.Length - 1)];
            int xx = matrix[0];
            int xy = matrix[1];
            int yx = matrix[2];
            int yy = matrix[3];
            int srcXStart = descriptor.VramXMin;
            int srcYStart = descriptor.VramYMin;
            if (xx < 0 || xy < 0)
                srcXStart += 31;
            if (yx < 0 || yy < 0)
                srcYStart += 31;

            descriptorCount++;
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    int sx = srcXStart + (x * xx) + (y * xy);
                    int sy = srcYStart + (x * yx) + (y * yy);
                    long relative = (sy * 2048L) + sx;
                    if (sx < 0 || sx >= 2048 || sy < 0 || sy >= 512 || relative < 0 || relative >= texturePages.Length)
                        continue;

                    int paletteIndex = texturePages[relative];
                    int paletteOffset = paletteByteStart + (paletteIndex * 2);
                    if (paletteOffset < 0 || paletteOffset + 2 > texturePages.Length)
                        continue;

                    int destIndex = ((destTileY + y) * tileSize) + destTileX + x;
                    pixels[destIndex] = ConvertPsx555ToRgba32(ReadUInt16(texturePages, paletteOffset));
                    if (!written[destIndex])
                    {
                        written[destIndex] = true;
                        copiedPixels++;
                    }
                }
            }
        }

        if (copiedPixels == 0)
            return null;

        await TerrainTexturePngWriter.WriteRgbaAsync(outputPath, tileSize, tileSize, pixels, cancellationToken);
        return new TerrainTextureImageExport(
            TextureId: textureId,
            DescriptorTier: descriptorTier,
            Width: tileSize,
            Height: tileSize,
            DescriptorCount: descriptorCount,
            PixelCount: copiedPixels);
    }

    public static TerrainTextureSlot? FindUnusedTextureSlot(
        string sourceImagePath,
        LevelDefinition level,
        IEnumerable<int> usedTextureIds,
        bool preferBothDescriptorTiers = true)
    {
        HashSet<int> used = usedTextureIds.Where(textureId => textureId >= 0).ToHashSet();
        IReadOnlyList<TerrainTextureSlot> slots = InspectTextureSlots(sourceImagePath, level);
        IEnumerable<TerrainTextureSlot> unused = slots.Where(slot => !used.Contains(slot.TextureId));

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
        if (!hasTerrainEdits && !hasCustomTextures)
            throw new FileNotFoundException("Missing terrain edit file.", request.TerrainEditsPath);

        string outputPrefix = string.IsNullOrWhiteSpace(request.OutputPrefix)
            ? Path.Combine(Path.GetDirectoryName(request.SourceImagePath) ?? "", $"Spyro the Dragon (USA)-{request.Level.Key}-terrain-edits")
            : request.OutputPrefix;
        string outputImagePath = $"{outputPrefix}.bin";
        string outputCuePath = $"{outputPrefix}.cue";
        string outputPlanPath = $"{outputPrefix}.terrain-patch-plan.json";

        TerrainPatchPlan plan = BuildPlan(
            request.SourceImagePath,
            request.SourceCuePath,
            outputImagePath,
            outputCuePath,
            request.Level,
            request.RamPath,
            request.SourceSearchPath,
            request.TerrainEditsPath,
            request.CustomTexturesPath);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPlanPath) ?? ".");
        await File.WriteAllTextAsync(outputPlanPath, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);

        if (request.WriteImage && plan.PatchCount > 0)
        {
            File.Copy(request.SourceImagePath, outputImagePath, true);
            DiscLayout layout = DiscImage.DetectLayout(outputImagePath);
            await using FileStream stream = File.Open(outputImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            foreach (TerrainPatch patch in plan.Patches)
            {
                long wadOffset = ParseRequiredLong(patch.WadRelativeOffset, "patch.wadRelativeOffset");
                DiscImage.WriteFileBytes(stream, layout, WadLba, wadOffset, HexToBytes(patch.AfterHexPreview));
            }

            string cueText = DiscImage.BuildCueText(request.SourceCuePath, Path.GetFileName(outputImagePath));
            await File.WriteAllTextAsync(outputCuePath, cueText, Encoding.ASCII, cancellationToken);
        }

        return new TerrainPatchResult(outputImagePath, outputCuePath, outputPlanPath, plan, request.WriteImage && plan.PatchCount > 0);
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
        string customTexturesPath = "")
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
        List<TerrainSideWallCandidate> sideWallCandidates = new();
        List<TerrainSideWallPatchSummary> sideWallSummaries = new();
        List<long> editedSceneSectorWadOffsets = new();
        if (File.Exists(terrainEditsPath))
        {
            using FileStream editStream = File.OpenRead(terrainEditsPath);
            using JsonDocument editDocument = JsonDocument.Parse(editStream);

            if (!editDocument.RootElement.TryGetProperty("edits", out JsonElement editsElement) || editsElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("The terrain edit file does not contain an edits array.");

            if (collisionContext == null && canPatchSceneSectors && usesSourceDerivedSceneBytes)
            {
                collisionContext = TryBuildSourceDerivedCollisionPatchContext(
                    ram,
                    sourceSectorHits,
                    editsElement,
                    skippedEdits);
            }

            foreach (JsonElement edit in editsElement.EnumerateArray())
            {
                string runtimeKey = JsonValue.GetString(edit, "runtimeKey");
                if (string.IsNullOrWhiteSpace(runtimeKey))
                    continue;
                string structureEditMode = JsonValue.GetString(edit, "structureEditMode");
                if (!canPatchSceneSectors)
                {
                    skippedEdits.Add($"{runtimeKey}: terrain face edits need this level's RAM capture/source-search map or a source-derived terrain map; custom texture art can still be patched.");
                    continue;
                }
                if (!sourceSectorHits.TryGetValue(runtimeKey, out SourceSectorLocation? sourceSectorLocation))
                {
                    skippedEdits.Add($"{runtimeKey}: no source-sector match found.");
                    continue;
                }
                long sourceSectorWadOffset = sourceSectorLocation.WadOffset;
                if (!editedSceneSectorWadOffsets.Contains(sourceSectorWadOffset))
                    editedSceneSectorWadOffsets.Add(sourceSectorWadOffset);

                int sectorOffset = JsonValue.GetInt32(edit, "sectorOffset", -1);
                if (sectorOffset < 0)
                {
                    skippedEdits.Add($"{runtimeKey}: edit is missing sector offset; re-save this terrain edit in the native editor.");
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
                AddTexturePatch(imageStream, layout, ram, sector, sourceSectorWadOffset, sectorOffset, edit, patchesByWadOffset, skippedEdits);
                AddVertexPatches(imageStream, layout, ram, sector, sourceSectorWadOffset, sectorOffset, detail, edit, patchesByWadOffset, skippedEdits);
                AddCollisionPatches(imageStream, layout, ram, collisionContext, sector, detail, edit, patchesByWadOffset, skippedEdits);
                CollectTerrainSideWallCandidates(ram, collisionContext, sector, sourceSectorWadOffset, sectorOffset, detail, edit, sideWallCandidates, skippedEdits);
            }

            AddTerrainSideWallPatches(imageStream, layout, ram, collisionContext, sourceSectorHits, modelSubfileInfo, editedSceneSectorWadOffsets, sideWallCandidates, patchesByWadOffset, skippedEdits, sideWallSummaries);
        }

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
            TextureAssetWadIndex: textureAssetWadIndex,
            PatchCount: patches.Count,
            TotalPatchedBytes: patches.Sum(patch => patch.ByteLength),
            CustomTextureImportCount: customTextureSummaries.Count,
            CustomTextureBytePatchCount: patches.Count(patch => patch.Kind.StartsWith("custom-texture-", StringComparison.OrdinalIgnoreCase)),
            CustomTextureImports: customTextureSummaries,
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
                            : "Source-derived terrain can patch existing top collision, but copied terrain and solid side walls still need the collision lookup table decoded."
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
        SceneSectorHeader sector,
        long sourceSectorWadOffset,
        int sectorOffset,
        JsonElement edit,
        Dictionary<long, TerrainPatch> patchesByWadOffset,
        List<string> skippedEdits)
    {
        int textureIdEdited = JsonValue.GetInt32(edit, "textureIdEdited", -1);
        if (textureIdEdited < 0)
            return;

        string runtimeKey = JsonValue.GetString(edit, "runtimeKey");
        int faceOffset = JsonValue.GetInt32(edit, "faceOffset", -1);
        if (faceOffset < sectorOffset || faceOffset + 12 > sectorOffset + sector.SizeBytes)
        {
            skippedEdits.Add($"{runtimeKey}: texture edit is missing a valid face offset; re-save this edit.");
            return;
        }

        int word3Offset = faceOffset + 8;
        long wadOffset = sourceSectorWadOffset + (word3Offset - sectorOffset);
        uint oldWord = ReadUInt32(ram, word3Offset);
        uint newWord = (oldWord & 0xFFFFFF80u) | (uint)(textureIdEdited & 0x7F);
        AddPatch(imageStream, layout, patchesByWadOffset, wadOffset, BitConverter.GetBytes(oldWord), BitConverter.GetBytes(newWord), "texture-id-word3", runtimeKey, $"Set terrain texture id to {textureIdEdited}.");
    }

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

    private static bool TryBuildCollisionIndexBytes(
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
            if (IsDegenerateCollisionTriangle(triangle))
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

        List<ushort> zList = new();
        List<ushort> yList = new();
        List<ushort> xList = new();
        List<ushort> blockList = new();

        for (int i = 0; i <= maxZBlock + 1; i++)
        {
            if (!TryAddCollisionIndexUInt16(zList, 0xFFFF, out skipReason))
                return false;
        }

        int maxZSection = 0;
        for (int z = 0; z <= maxZBlock; z++)
        {
            int maxZYSection = -1;
            int ySegmentStart = yList.Count;
            for (int i = 0; i <= maxYBlock + 1; i++)
            {
                if (!TryAddCollisionIndexUInt16(yList, 0xFFFF, out skipReason))
                    return false;
            }

            if (z >= minZBlock)
            {
                for (int y = 0; y <= maxYBlock; y++)
                {
                    int maxZYXSection = -1;
                    int xSegmentStart = xList.Count;
                    for (int i = 0; i <= maxXBlock + 1; i++)
                    {
                        if (!TryAddCollisionIndexUInt16(xList, 0xFFFF, out skipReason))
                            return false;
                    }

                    if (y >= minYBlock)
                    {
                        for (int x = 0; x <= maxXBlock; x++)
                        {
                            int numTrisHere = 0;
                            if (x >= minXBlock && xBlocks[x] != null)
                            {
                                foreach (int triIndex in xBlocks[x])
                                {
                                    CollisionTriangleBounds? bound = bounds[triIndex];
                                    if (bound == null ||
                                        bound.MinYBlock > y ||
                                        bound.MaxYBlock < y ||
                                        bound.MinZBlock > z ||
                                        bound.MaxZBlock < z ||
                                        !CollisionTriangleTouchesBlock(bound, x, y, z))
                                    {
                                        continue;
                                    }

                                    int word = triIndex | (numTrisHere == 0 ? 0x8000 : 0);
                                    if (!TryAddCollisionIndexUInt16(blockList, word, out skipReason))
                                        return false;
                                    numTrisHere++;
                                }
                            }

                            if (numTrisHere > 0)
                            {
                                if (!TrySetCollisionIndexUInt16(xList, xSegmentStart + 1 + x, blockList.Count - numTrisHere, out skipReason))
                                    return false;
                                maxZYXSection = x;
                            }
                        }
                    }

                    if (maxZYXSection != -1)
                    {
                        if (!TrySetCollisionIndexUInt16(xList, xSegmentStart, maxZYXSection + 1, out skipReason) ||
                            !TrySetCollisionIndexUInt16(yList, ySegmentStart + 1 + y, xSegmentStart * 2, out skipReason))
                        {
                            return false;
                        }

                        int keep = maxZYXSection + 2;
                        int segmentLength = maxXBlock + 2;
                        if (keep < segmentLength)
                            xList.RemoveRange(xSegmentStart + keep, segmentLength - keep);
                        maxZYSection = y;
                    }
                    else
                    {
                        xList.RemoveRange(xSegmentStart, maxXBlock + 2);
                    }
                }
            }

            if (maxZYSection != -1)
            {
                if (!TrySetCollisionIndexUInt16(yList, ySegmentStart, maxZYSection + 1, out skipReason) ||
                    !TrySetCollisionIndexUInt16(zList, 1 + z, ySegmentStart * 2, out skipReason))
                {
                    return false;
                }

                int keep = maxZYSection + 2;
                int segmentLength = maxYBlock + 2;
                if (keep < segmentLength)
                    yList.RemoveRange(ySegmentStart + keep, segmentLength - keep);
                maxZSection = z;
            }
            else
            {
                yList.RemoveRange(ySegmentStart, maxYBlock + 2);
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
            if (import.TextureId < 0 || import.TextureId >= textureIndex.TextureCount)
            {
                skippedEdits.Add($"texture {import.TextureId}: outside decoded {levelKey} texture table.");
                continue;
            }

            string sourcePath = ResolveManifestRelativePath(customTexturesPath, import.SourceImagePath);
            if (!File.Exists(sourcePath))
            {
                skippedEdits.Add($"texture {import.TextureId}: missing custom texture image {sourcePath}.");
                continue;
            }

            TextureRecord record = textureIndex.Records[import.TextureId];
            foreach (string descriptorTier in ExpandDescriptorTiers(import.DescriptorTier))
            {
                int tileSize = descriptorTier == "hqDataClose" ? 128 : 64;
                int tileGridColumns = descriptorTier == "hqDataClose" ? 4 : 2;
                IReadOnlyList<TextureDescriptor> descriptors = descriptorTier == "hqDataClose" ? record.HqDataClose : record.HqData;
                if (descriptors.Count == 0)
                {
                    skippedEdits.Add($"texture {import.TextureId}: no {descriptorTier} descriptors found.");
                    continue;
                }

                Rgba32[] pixels = PngRgbaImage.ReadResizedRgba(sourcePath, tileSize, tileSize);
                PaletteInfo palette = BuildPalette(pixels);
                byte[] paletteBytes = new byte[512];
                for (int i = 0; i < 256; i++)
                    WriteUInt16(paletteBytes, i * 2, palette.Words[i]);

                HashSet<int> paletteStarts = new();
                HashSet<int> sourceTexels = new();
                HashSet<ushort> sourcePaletteWords = new();
                int sourceTexelMax = 0;
                int sourcePaletteNonZeroWords = 0;
                int pixelPatchCount = 0;
                foreach (TextureDescriptor descriptor in descriptors)
                {
                    int paletteByteStart = descriptor.PaletteByteStart;
                    if (paletteByteStart < 0 || paletteByteStart + 512 > texturePagesInfo.SubfileSize)
                    {
                        skippedEdits.Add($"texture {import.TextureId}: {descriptorTier} palette at 0x{paletteByteStart:X} is outside texture-pages subfile {TexturePagesSubfileIndex}.");
                        continue;
                    }

                    int tile = descriptor.Index;
                    int destTileX = (tile % tileGridColumns) * 32;
                    int destTileY = (int)Math.Floor(tile / (double)tileGridColumns) * 32;
                    int[] matrix = TextureDescriptorMatrices[Math.Clamp(descriptor.Orientation, 0, TextureDescriptorMatrices.Length - 1)];
                    int xx = matrix[0];
                    int xy = matrix[1];
                    int yx = matrix[2];
                    int yy = matrix[3];
                    int srcXStart = descriptor.VramXMin;
                    int srcYStart = descriptor.VramYMin;
                    if (xx < 0 || xy < 0)
                        srcXStart += 31;
                if (yx < 0 || yy < 0)
                    srcYStart += 31;

                    if (!DescriptorMapsInsideTexturePages(srcXStart, srcYStart, xx, xy, yx, yy, texturePagesInfo.SubfileSize))
                    {
                        skippedEdits.Add($"texture {import.TextureId}: {descriptorTier} descriptor {tile} maps outside the texture-page image.");
                        continue;
                    }

                    if (paletteStarts.Add(paletteByteStart))
                    {
                        long paletteWadOffset = texturePagesInfo.AbsoluteWadOffset + paletteByteStart;
                        byte[] oldPaletteBytes = ReadWadBytes(imageStream, layout, paletteWadOffset, 512);
                        for (int i = 0; i < 256; i++)
                        {
                            ushort word = ReadUInt16(oldPaletteBytes, i * 2);
                            sourcePaletteWords.Add(word);
                            if ((word & 0x7FFF) != 0)
                                sourcePaletteNonZeroWords++;
                        }

                        AddPatch(imageStream, layout, patchesByWadOffset, paletteWadOffset, oldPaletteBytes, paletteBytes, "custom-texture-palette", $"texture-{import.TextureId}-{descriptorTier}", $"Replace texture {import.TextureId} palette for {descriptorTier}.");
                    }

                    for (int y = 0; y < 32; y++)
                    {
                        for (int x = 0; x < 32; x++)
                        {
                            int sx = srcXStart + (x * xx) + (y * xy);
                            int sy = srcYStart + (x * yx) + (y * yy);
                            if (sx < 0 || sx >= 2048 || sy < 0 || sy >= 512)
                                throw new InvalidOperationException($"Texture {import.TextureId} descriptor {tile} maps outside the 2048x512 texture-page VRAM.");

                            long relative = (sy * 2048L) + sx;
                            if (relative < 0 || relative + 1 > texturePagesInfo.SubfileSize)
                                throw new InvalidOperationException($"Texture {import.TextureId} pixel at texture-pages offset 0x{relative:X} is outside subfile {TexturePagesSubfileIndex}.");

                            Rgba32 color = pixels[(destTileY + y) * tileSize + destTileX + x];
                            ushort pixelWord = ConvertColorToPsx555(color);
                            byte paletteIndex = (byte)GetNearestPaletteIndex(pixelWord, palette);
                            long pixelWadOffset = texturePagesInfo.AbsoluteWadOffset + relative;
                            byte[] oldPixelBytes = ReadWadBytes(imageStream, layout, pixelWadOffset, 1);
                            int sourceTexel = oldPixelBytes[0];
                            sourceTexels.Add(sourceTexel);
                            sourceTexelMax = Math.Max(sourceTexelMax, sourceTexel);
                            AddPatch(imageStream, layout, patchesByWadOffset, pixelWadOffset, oldPixelBytes, [paletteIndex], "custom-texture-pixel", $"texture-{import.TextureId}-{descriptorTier}", $"Replace texture {import.TextureId} pixel byte for {descriptorTier}.");
                            pixelPatchCount++;
                        }
                    }
                }

                if (pixelPatchCount > 0 || paletteStarts.Count > 0)
                {
                    summaries.Add(new CustomTerrainTexturePatchSummary(
                        TextureId: import.TextureId,
                        SourceImagePath: sourcePath,
                        SourceImageName: import.SourceImageName,
                        DescriptorTier: descriptorTier,
                        TileSize: tileSize,
                        DescriptorCount: descriptors.Count,
                        PixelPatchCount: pixelPatchCount,
                        PalettePatchCount: paletteStarts.Count,
                        TexelBytesPerPixel: 1,
                        PaletteColorCount: 256,
                        SourceTexelUniqueCount: sourceTexels.Count,
                        SourceTexelMax: sourceTexelMax,
                        SourcePaletteUniqueColorCount: sourcePaletteWords.Count,
                        SourcePaletteNonZeroColorCount: sourcePaletteNonZeroWords,
                        PaletteByteStarts: paletteStarts.OrderBy(value => value).Select(value => $"0x{value:X}").ToArray()));
                }
            }
        }

        return summaries;
    }

    private static bool CanPatchTextureDescriptor(TextureDescriptor descriptor, long texturePagesSubfileSize)
    {
        if (descriptor.PaletteByteStart < 0 || descriptor.PaletteByteStart + 512 > texturePagesSubfileSize)
            return false;

        int[] matrix = TextureDescriptorMatrices[Math.Clamp(descriptor.Orientation, 0, TextureDescriptorMatrices.Length - 1)];
        int srcXStart = descriptor.VramXMin;
        int srcYStart = descriptor.VramYMin;
        if (matrix[0] < 0 || matrix[1] < 0)
            srcXStart += 31;
        if (matrix[2] < 0 || matrix[3] < 0)
            srcYStart += 31;

        return DescriptorMapsInsideTexturePages(srcXStart, srcYStart, matrix[0], matrix[1], matrix[2], matrix[3], texturePagesSubfileSize);
    }

    private static (string Tier, int TileSize, int TileGridColumns, IReadOnlyList<TextureDescriptor> Descriptors)? ChooseReadableTextureDescriptorTier(TextureRecord record, long texturePagesSubfileSize)
    {
        if (record.HqData.Any(descriptor => CanPatchTextureDescriptor(descriptor, texturePagesSubfileSize)))
            return ("hqData", 64, 2, record.HqData);

        if (record.HqDataClose.Any(descriptor => CanPatchTextureDescriptor(descriptor, texturePagesSubfileSize)))
            return ("hqDataClose", 128, 4, record.HqDataClose);

        return null;
    }

    private static Rgba32 ConvertPsx555ToRgba32(ushort word)
    {
        byte r = ExpandPsx5ToByte(word & 0x1F);
        byte g = ExpandPsx5ToByte((word >> 5) & 0x1F);
        byte b = ExpandPsx5ToByte((word >> 10) & 0x1F);
        return new Rgba32(r, g, b, 255);
    }

    private static byte ExpandPsx5ToByte(int value)
    {
        value = Math.Clamp(value, 0, 31);
        return (byte)((value << 3) | (value >> 2));
    }

    private static bool DescriptorMapsInsideTexturePages(int srcXStart, int srcYStart, int xx, int xy, int yx, int yy, long texturePagesSubfileSize)
    {
        foreach ((int x, int y) in new[] { (0, 0), (31, 0), (0, 31), (31, 31) })
        {
            int sx = srcXStart + (x * xx) + (y * xy);
            int sy = srcYStart + (x * yx) + (y * yy);
            if (sx < 0 || sx >= 2048 || sy < 0 || sy >= 512)
                return false;

            long relative = (sy * 2048L) + sx;
            if (relative < 0 || relative + 1 > texturePagesSubfileSize)
                return false;
        }

        return true;
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
        int textureListSize = (int)ReadUInt32(modelBytes, 0);
        int textureCount = (int)ReadUInt32(modelBytes, 4);
        if (textureListSize <= 8 || textureCount <= 0)
            throw new InvalidOperationException("No plausible texture list found in model subfile.");

        int recordBytes = (textureListSize - 8) / textureCount;
        if (((textureListSize - 8) % textureCount) != 0)
            throw new InvalidOperationException("Texture list is not an even fixed-record table.");

        List<TextureRecord> records = new(textureCount);
        for (int texture = 0; texture < textureCount; texture++)
        {
            int offset = 8 + (texture * recordBytes);
            List<TextureDescriptor> hq = new();
            for (int i = 0; i < 4; i++)
                hq.Add(DecodeTextureDescriptor(modelBytes, offset + 24 + (i * 8), i));

            List<TextureDescriptor> hqClose = new();
            for (int i = 0; i < 16; i++)
                hqClose.Add(DecodeTextureDescriptor(modelBytes, offset + 56 + (i * 8), i));

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
        return new TextureDescriptor(
            Index: index,
            PaletteByteStart: palette * 32,
            Orientation: (unknown >> 4) & 7,
            VramXMin: GetTextureXMin(region, xmin),
            VramYMin: GetTextureYMin(region, ymin),
            VramXMax: GetTextureXMin(region, xmax),
            VramYMax: GetTextureYMin(region, ymax));
    }

    private static int GetTextureXMin(int region, int xmin) => ((region * 128) % 2048) + xmin;

    private static int GetTextureYMin(int region, int ymin) => ((int)Math.Floor((region & 0x1F) / 16.0) * 256) + ymin;

    private static PaletteInfo BuildPalette(IReadOnlyList<Rgba32> pixels)
    {
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
            .Take(256)
            .ToArray();
        Array.Resize(ref words, 256);
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

    private sealed record CollisionIndexBytes(
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

    private readonly record struct CollisionPatchVertex(Vector3f Original, Vector3f Target);

    private sealed record ArchiveEntry(int Index, long Offset, long Size);

    private sealed record AssetSubfileInfo(int AssetWadIndex, int SubfileIndex, long AssetOffset, long AssetSize, long SubfileOffset, long SubfileSize, long AbsoluteWadOffset);

    private sealed record TextureRecordIndex(int TextureListSize, int TextureCount, int RecordBytes, IReadOnlyList<TextureRecord> Records);

    private sealed record TextureRecord(int TextureId, IReadOnlyList<TextureDescriptor> HqData, IReadOnlyList<TextureDescriptor> HqDataClose);

    private sealed record TextureDescriptor(int Index, int PaletteByteStart, int Orientation, int VramXMin, int VramYMin, int VramXMax, int VramYMax);

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
    bool WriteImage);

public sealed record TerrainTextureSlot(
    int TextureId,
    bool HasNormalDescriptors,
    bool HasCloseDescriptors,
    int NormalDescriptorCount,
    int CloseDescriptorCount);

public sealed record TerrainTextureImageExport(
    int TextureId,
    string DescriptorTier,
    int Width,
    int Height,
    int DescriptorCount,
    int PixelCount);

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
    int TextureAssetWadIndex,
    int PatchCount,
    int TotalPatchedBytes,
    int CustomTextureImportCount,
    int CustomTextureBytePatchCount,
    IReadOnlyList<CustomTerrainTexturePatchSummary> CustomTextureImports,
    IReadOnlyList<TerrainSideWallPatchSummary> TerrainSideWalls,
    IReadOnlyList<TerrainPatch> Patches,
    IReadOnlyList<string> SkippedEdits,
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

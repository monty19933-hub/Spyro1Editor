using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Skyboxes;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeSkyBatchEdit(LevelDefinition Level, NativeSkyEditPlan Edit);

public sealed record NativeSkyPatch(
    string Label,
    string Kind,
    string TargetLevelKey,
    string TargetLevelName,
    string StorageLevelKey,
    string StorageLevelName,
    bool PortalCopy,
    string EditSource,
    int WadEntry,
    int BlockIndex,
    string BlockOffset,
    string WadOffset,
    string ImageOffset,
    int ByteLength,
    int ChangedByteCount,
    int PaletteWordCount,
    string BeforeSha256,
    string AfterSha256,
    string BeforeHexPreview,
    string AfterHexPreview);

public sealed record NativeSkyPatchPlan(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    string OutputImagePath,
    string OutputCuePath,
    string WadAnalysisPath,
    int EditedLevelCount,
    int PatchCount,
    int TotalWrittenBytes,
    int TotalChangedBytes,
    bool RelocatedWad,
    int WadGrowthBytes,
    int AvailableWadGrowthBytes,
    int ExecutableLbaDelta,
    bool SkyOcclusionBypassApplied,
    string SkyOcclusionScope,
    IReadOnlyList<int> SkyOcclusionScopeLevelIds,
    int ExecutablePatchCount,
    string SkyOcclusionPatchRuntimeAddress,
    int SkyOcclusionPatchExecutableLba,
    string SkyOcclusionPatchImageOffset,
    string SkyOcclusionPayloadRuntimeAddress,
    string SkyOcclusionPayloadImageOffset,
    IReadOnlyList<string> EditedLevelNames,
    IReadOnlyList<NativeSkyPatch> Patches,
    IReadOnlyList<string> SafetyNotes);

public sealed record NativeSkyBatchPatchRequest(
    string SourceImagePath,
    string SourceCuePath,
    string WadAnalysisPath,
    string WorkspacePath,
    string OutputPrefix,
    LevelCatalog Catalog,
    IReadOnlyList<NativeSkyBatchEdit> Edits,
    bool WriteImage,
    bool AllowUnprovenLinkedPortalExpansion = false,
    bool ConsumeDisposableSourceImage = false);

public sealed record NativeSkyPatchResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputPlanPath,
    NativeSkyPatchPlan Plan,
    bool WroteImage);

public static class NativeSkyPatchExporter
{
    private const long MaximumImportedSkyBytes = 1_048_576;

    public static async Task<NativeSkyPatchResult> ExportBatchAsync(
        NativeSkyBatchPatchRequest request,
        CancellationToken cancellationToken = default)
    {
        (NativeSkyPatchPlan plan, IReadOnlyList<NativeSkyRelocationPayload> payloads, NativeSkyWadRelocationPlan? relocation, NativeSkyOcclusionPatchPlan? occlusionPatch) =
            await Task.Run(
                () => BuildPlanAndPayloads(request),
                cancellationToken);
        string outputPlanPath = $"{request.OutputPrefix}.native-sky-patch-plan.json";
        Directory.CreateDirectory(Path.GetDirectoryName(outputPlanPath) ?? ".");
        await using (FileStream output = File.Create(outputPlanPath))
        {
            await JsonSerializer.SerializeAsync(output, plan, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }, cancellationToken);
        }

        bool wroteImage = false;
        if (request.WriteImage && payloads.Count > 0)
        {
            if (relocation?.Required == true)
            {
                await Task.Run(
                    () => NativeSkyWadRelocator.WriteExpandedImage(
                        request.SourceImagePath,
                        plan.OutputImagePath,
                        request.WadAnalysisPath,
                        relocation,
                        payloads),
                    cancellationToken);
                if (request.ConsumeDisposableSourceImage)
                    DeleteDisposablePredecessor(request.SourceImagePath);
            }
            else
            {
                await DiscImageWorkingCopy.StageAsync(
                    request.SourceImagePath,
                    plan.OutputImagePath,
                    request.ConsumeDisposableSourceImage,
                    cancellationToken);
                DiscLayout layout = DiscImage.DetectLayout(plan.OutputImagePath);
                await using FileStream image = File.Open(plan.OutputImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                foreach (NativeSkyRelocationPayload payload in payloads)
                    DiscImage.WriteFileBytes(image, layout, payload.WadLba, payload.OriginalWadOffset, payload.Bytes);
            }
            if (occlusionPatch != null)
                NativeSkyOcclusionPatcher.Apply(plan.OutputImagePath, occlusionPatch);
            string cueText = DiscImage.BuildCueText(request.SourceCuePath, Path.GetFileName(plan.OutputImagePath));
            await File.WriteAllTextAsync(plan.OutputCuePath, cueText, Encoding.ASCII, cancellationToken);
            wroteImage = true;
        }

        return new NativeSkyPatchResult(plan.OutputImagePath, plan.OutputCuePath, outputPlanPath, plan, wroteImage);
    }

    private static void DeleteDisposablePredecessor(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static NativeSkyPatchPlan BuildPlan(NativeSkyBatchPatchRequest request) => BuildPlanAndPayloads(request).Plan;

    private static (NativeSkyPatchPlan Plan, IReadOnlyList<NativeSkyRelocationPayload> Payloads, NativeSkyWadRelocationPlan? Relocation, NativeSkyOcclusionPatchPlan? OcclusionPatch) BuildPlanAndPayloads(NativeSkyBatchPatchRequest request)
    {
        if (!File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", request.SourceImagePath);
        if (!File.Exists(request.WadAnalysisPath))
            throw new FileNotFoundException("Missing WAD analysis.", request.WadAnalysisPath);
        if (request.Edits.Count == 0)
            throw new InvalidOperationException("No saved native sky edits were provided.");

        Spyro1SkyBlockReport layoutReport = Spyro1SkyBlockAnalyzer.Analyze(request.SourceImagePath, request.WadAnalysisPath, request.Catalog);
        Dictionary<string, Spyro1LevelSkyBlockLayout> layouts = layoutReport.Levels
            .ToDictionary(level => LevelCatalog.NormalizeKey(level.Key), StringComparer.OrdinalIgnoreCase);
        DiscLayout discLayout = DiscImage.DetectLayout(request.SourceImagePath);
        using FileStream image = File.Open(request.SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        List<NativeSkyPatch> patches = new();
        List<NativeSkyRelocationPayload> payloads = new();
        HashSet<string> writtenRanges = new(StringComparer.Ordinal);
        HashSet<int> skyVisibilityScopeLevelIds = [];

        foreach (NativeSkyBatchEdit batchEdit in request.Edits)
        {
            string targetKey = LevelCatalog.NormalizeKey(batchEdit.Level.Key);
            if (!layouts.TryGetValue(targetKey, out Spyro1LevelSkyBlockLayout? targetLayout) || targetLayout.SkyBlocks.Count == 0)
                throw new InvalidOperationException($"{batchEdit.Level.DisplayName} does not have a parsed native sky block.");
            Spyro1SkyBlockLayout targetPrimary = targetLayout.SkyBlocks[0];
            NativeSkySource source = BuildEditSource(request, batchEdit, layouts, targetPrimary, image, discLayout, layoutReport.WadLba);
            if (!batchEdit.Edit.IsPalette && !request.AllowUnprovenLinkedPortalExpansion)
            {
                NativeSkyLinkedPortalSafety.ThrowIfUnprovenExpansion(
                    targetLayout,
                    source.BlockBytes.Length,
                    source.DisplayName);
            }
            IReadOnlyList<Spyro1SkyBlockReference> references = targetLayout.LinkedPrimarySkyCopies.Count > 0
                ? targetLayout.LinkedPrimarySkyCopies
                : [new Spyro1SkyBlockReference(targetLayout.Key, targetLayout.DisplayName, targetLayout.WadEntry, 0, targetPrimary.BlockOffset, targetPrimary.ByteLength, true)];
            if (!batchEdit.Edit.IsPalette)
            {
                skyVisibilityScopeLevelIds.Add(batchEdit.Level.LevelId);
                foreach (Spyro1SkyBlockReference reference in references)
                {
                    Spyro1LevelSkyBlockLayout storageLevel = layouts[LevelCatalog.NormalizeKey(reference.LevelKey)];
                    skyVisibilityScopeLevelIds.Add(storageLevel.LevelId);
                }
            }

            foreach (Spyro1SkyBlockReference reference in references)
            {
                Spyro1LevelSkyBlockLayout storageLevel = layouts[LevelCatalog.NormalizeKey(reference.LevelKey)];
                Spyro1SkyBlockLayout storageBlock = storageLevel.SkyBlocks.Single(block => block.Index == reference.BlockIndex);
                long wadOffset = storageLevel.ModelSubfileWadOffset + storageBlock.BlockOffset;
                string rangeKey = $"{layoutReport.WadLba}:{wadOffset}:{storageBlock.ByteLength}";
                if (!writtenRanges.Add(rangeKey))
                    throw new InvalidOperationException($"Saved sky edits overlap at WAD offset 0x{wadOffset:X}.");
                byte[] before = DiscImage.ReadFileBytes(image, discLayout, layoutReport.WadLba, wadOffset, storageBlock.ByteLength);
                byte[] after = batchEdit.Edit.IsPalette
                    ? ApplyPalette(before, storageBlock, batchEdit.Edit)
                    : source.BlockBytes.Length <= before.Length
                        ? BuildPaddedReplacement(before.Length, source.BlockBytes)
                        : source.BlockBytes.ToArray();
                if (!Spyro1SkyBlockAnalyzer.IsValidStandaloneSkyBlock(after))
                    throw new InvalidOperationException($"{batchEdit.Level.DisplayName} produced an invalid native sky block for {storageLevel.DisplayName} block {storageBlock.Index}.");
                if (before.AsSpan().SequenceEqual(after))
                    continue;

                int changedBytes = CountChangedBytes(before, after);
                string kind = batchEdit.Edit.IsPalette
                    ? "native-sky-palette"
                    : batchEdit.Edit.IsOriginalPreset
                        ? "native-sky-original-preset"
                    : batchEdit.Edit.IsSwap
                        ? "native-sky-same-disc-swap"
                        : "native-sky-custom-import";
                int patchIndex = patches.Count;
                patches.Add(new NativeSkyPatch(
                    Label: $"{batchEdit.Level.Key}-{kind}-{storageLevel.Key}-block-{storageBlock.Index}",
                    Kind: kind,
                    TargetLevelKey: batchEdit.Level.Key,
                    TargetLevelName: batchEdit.Level.DisplayName,
                    StorageLevelKey: storageLevel.Key,
                    StorageLevelName: storageLevel.DisplayName,
                    PortalCopy: !string.Equals(storageLevel.Key, batchEdit.Level.Key, StringComparison.OrdinalIgnoreCase) || storageBlock.Index != 0,
                    EditSource: source.Label,
                    WadEntry: storageLevel.WadEntry,
                    BlockIndex: storageBlock.Index,
                    BlockOffset: $"0x{storageBlock.BlockOffset:X}",
                    WadOffset: $"0x{wadOffset:X}",
                    ImageOffset: $"0x{DiscImage.ConvertFileOffsetToImageOffset(discLayout, layoutReport.WadLba, wadOffset):X}",
                    ByteLength: after.Length,
                    ChangedByteCount: changedBytes,
                    PaletteWordCount: storageBlock.PaletteWordCount,
                    BeforeSha256: Hash(before),
                    AfterSha256: Hash(after),
                    BeforeHexPreview: HexPreview(before),
                    AfterHexPreview: HexPreview(after)));
                payloads.Add(new NativeSkyRelocationPayload(
                    patchIndex,
                    layoutReport.WadLba,
                    wadOffset,
                    storageLevel.WadEntry,
                    storageBlock.BlockOffset,
                    before.Length,
                    after));
            }
        }

        NativeSkyWadRelocationPlan? relocation = payloads.Any(payload => payload.Bytes.Length > payload.OriginalLength)
            ? NativeSkyWadRelocator.BuildPlan(request.SourceImagePath, request.WadAnalysisPath, payloads)
            : null;
        if (relocation?.Required == true)
        {
            Dictionary<int, NativeSkyRelocatedPatch> relocatedByPatch = relocation.RelocatedPatches.ToDictionary(patch => patch.PatchIndex);
            for (int index = 0; index < patches.Count; index++)
            {
                NativeSkyRelocatedPatch relocated = relocatedByPatch[index];
                patches[index] = patches[index] with
                {
                    BlockOffset = $"0x{relocated.ModelBlockOffset:X}",
                    WadOffset = $"0x{relocated.WadOffset:X}",
                    ImageOffset = $"0x{relocated.ImageOffset:X}"
                };
            }
        }

        bool needsOcclusionBypass = request.Edits.Any(edit => !edit.Edit.IsPalette);
        NativeSkyOcclusionPatchPlan? occlusionPatch = needsOcclusionBypass
            ? NativeSkyOcclusionPatcher.BuildPlan(
                request.SourceImagePath,
                skyVisibilityScopeLevelIds,
                relocation?.Required == true ? relocation.RelocatedExecutableLba : null)
            : null;

        string outputImagePath = $"{request.OutputPrefix}.bin";
        string outputCuePath = $"{request.OutputPrefix}.cue";
        NativeSkyPatchPlan plan = new(
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceImagePath: request.SourceImagePath,
            OutputImagePath: outputImagePath,
            OutputCuePath: outputCuePath,
            WadAnalysisPath: request.WadAnalysisPath,
            EditedLevelCount: request.Edits.Select(edit => edit.Level.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            PatchCount: patches.Count,
            TotalWrittenBytes: patches.Sum(patch => patch.ByteLength) + (occlusionPatch?.TotalWrittenBytes ?? 0),
            TotalChangedBytes: patches.Sum(patch => patch.ChangedByteCount) + (occlusionPatch?.ChangedByteCount ?? 0),
            RelocatedWad: relocation?.Required == true,
            WadGrowthBytes: relocation?.WadGrowthBytes ?? 0,
            AvailableWadGrowthBytes: relocation?.AvailableGrowthBytes ?? 0,
            ExecutableLbaDelta: relocation?.Required == true
                ? relocation.RelocatedExecutableLba - relocation.OriginalExecutableLba
                : 0,
            SkyOcclusionBypassApplied: occlusionPatch != null,
            SkyOcclusionScope: occlusionPatch == null ? "native" : "edited-destinations-and-linked-portal-contexts",
            SkyOcclusionScopeLevelIds: occlusionPatch?.ScopedLevelIds ?? Array.Empty<int>(),
            ExecutablePatchCount: occlusionPatch?.PatchCount ?? 0,
            SkyOcclusionPatchRuntimeAddress: occlusionPatch?.RuntimeAddress ?? "",
            SkyOcclusionPatchExecutableLba: occlusionPatch?.ExecutableLba ?? 0,
            SkyOcclusionPatchImageOffset: occlusionPatch == null ? "" : $"0x{occlusionPatch.ImageOffset:X}",
            SkyOcclusionPayloadRuntimeAddress: occlusionPatch?.PayloadRuntimeAddress ?? "",
            SkyOcclusionPayloadImageOffset: occlusionPatch == null ? "" : $"0x{occlusionPatch.PayloadImageOffset:X}",
            EditedLevelNames: request.Edits.Select(edit => edit.Level.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Patches: patches,
            SafetyNotes:
            [
                "Loaded sky blocks and byte-identical homeworld portal copies are patched together.",
                request.AllowUnprovenLinkedPortalExpansion
                    ? "RESEARCH ONLY: the linked homeworld portal capacity guard was explicitly bypassed for structural smoke coverage; this plan is not approved for a user Create BIN."
                    : "A child-level replacement may not grow its linked homeworld portal copy; unproven growth is blocked before any BIN, CUE, or patch plan is written.",
                "Same-disc and custom imports stay in place when they fit; oversized blocks use the guarded expanded-WAD path.",
                "Original presets combine source-disc sky geometry with editor-authored color palettes; no donor sky bytes are bundled with the editor.",
                "Smaller donors keep the target block length and zero-fill unused trailing bytes so later sky blocks do not move.",
                "Custom imports must parse as a native Spyro 1 sky block or payload; raw image files are not accepted as skies.",
                occlusionPatch != null
                    ? $"Geometry swaps and custom imports bypass stale sky-occlusion lists only while one of the edited destination/linked portal contexts is loaded (level ids: {string.Join(", ", occlusionPatch.ScopedLevelIds)}). Unedited levels retain retail sky-group selection; normal per-sector view culling remains active."
                    : "Palette-only edits preserve the original sky-occlusion behavior and do not patch the executable.",
                request.AllowUnprovenLinkedPortalExpansion
                    ? "RESEARCH ONLY: flight-stage sky geometry may enter non-flight destinations only under the explicit structural-research bypass; it is not approved for a user Create BIN."
                    : "Flight-stage sky geometry is blocked in non-flight destinations because its authored camera bounds can visibly pop; palette-only flight colors remain available.",
                relocation?.Required == true
                    ? "Oversized skies expand sector-aligned nested level archives and WAD.WAD, relocate the executable into the verified ISO gap, and update ISO directory extents."
                    : "This batch fits the existing native sky capacities and does not relocate WAD.WAD."
            ]);
        return (plan, payloads, relocation, occlusionPatch);
    }

    private static NativeSkySource BuildEditSource(
        NativeSkyBatchPatchRequest request,
        NativeSkyBatchEdit batchEdit,
        IReadOnlyDictionary<string, Spyro1LevelSkyBlockLayout> layouts,
        Spyro1SkyBlockLayout targetPrimary,
        FileStream image,
        DiscLayout discLayout,
        int wadLba)
    {
        if (batchEdit.Edit.IsPalette)
            return new NativeSkySource(NormalizePaletteLabel(batchEdit.Edit), "palette-only edit", Array.Empty<byte>());

        if (batchEdit.Edit.IsSwap || batchEdit.Edit.IsOriginalPreset)
        {
            string donorKey = LevelCatalog.NormalizeKey(batchEdit.Edit.DonorLevelKey);
            if (string.IsNullOrWhiteSpace(donorKey) || !layouts.TryGetValue(donorKey, out Spyro1LevelSkyBlockLayout? donorLayout) || donorLayout.SkyBlocks.Count == 0)
                throw new InvalidOperationException($"{batchEdit.Level.DisplayName} has no valid same-disc sky donor selected.");
            if (string.Equals(donorKey, LevelCatalog.NormalizeKey(batchEdit.Level.Key), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Choose a different level as the sky donor.");
            NativeSkyGeometrySafety.ThrowIfFlightDonorTargetsNonFlight(
                batchEdit.Level,
                donorLayout,
                request.AllowUnprovenLinkedPortalExpansion);
            Spyro1SkyBlockLayout donorBlock = donorLayout.SkyBlocks[0];
            byte[] donorBytes = DiscImage.ReadFileBytes(
                image,
                discLayout,
                wadLba,
                donorLayout.ModelSubfileWadOffset + donorBlock.BlockOffset,
                donorBlock.ByteLength);
            if (batchEdit.Edit.IsOriginalPreset)
            {
                donorBytes = ApplyPalette(donorBytes, donorBlock, batchEdit.Edit);
                OriginalSkyboxPreset preset = SkyboxPresetCatalog.FindOriginal(batchEdit.Edit.PalettePreset);
                return new NativeSkySource(
                    $"original-preset:{preset.Id}:geometry:{donorLayout.Key}",
                    $"{preset.DisplayName} ({donorLayout.DisplayName} geometry)",
                    donorBytes);
            }
            return new NativeSkySource($"same-disc:{donorLayout.Key}", donorLayout.DisplayName, donorBytes);
        }

        if (!batchEdit.Edit.IsImport)
            throw new InvalidOperationException($"Unknown sky edit mode '{batchEdit.Edit.Mode}'.");
        string importPath = ResolveImportPath(request.WorkspacePath, batchEdit.Edit.ImportedSkyPath);
        if (!File.Exists(importPath))
            throw new FileNotFoundException("The saved custom .sky import is missing.", importPath);
        FileInfo info = new(importPath);
        if (info.Length <= 0 || info.Length > MaximumImportedSkyBytes)
            throw new InvalidDataException($"The custom .sky import must be between 1 byte and {MaximumImportedSkyBytes:N0} bytes.");
        byte[] raw = File.ReadAllBytes(importPath);
        string rawHash = Hash(raw);
        if (!string.IsNullOrWhiteSpace(batchEdit.Edit.ImportedSkySha256) &&
            !string.Equals(rawHash, batchEdit.Edit.ImportedSkySha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The saved custom .sky file changed after it was selected. Re-import it before creating a BIN.");
        }
        byte[] normalized = Spyro1SkyBlockAnalyzer.NormalizeStandaloneSkyFile(raw);
        string importName = Path.GetFileName(importPath);
        return new NativeSkySource($"custom:{importName}", importName, normalized);
    }

    private static byte[] ApplyPalette(byte[] before, Spyro1SkyBlockLayout block, NativeSkyEditPlan edit)
    {
        byte[] after = before.ToArray();
        IReadOnlyList<Rgb> gradient = PaletteFor(edit);
        ApplyMappedColor(after, block.BackgroundColorOffset - block.BlockOffset, gradient);
        foreach (Spyro1SkyPartLayout part in block.Parts)
        {
            int localStart = part.ColorTableOffset - block.BlockOffset;
            for (int color = 0; color < part.ColorCount; color++)
                ApplyMappedColor(after, localStart + (color * 4), gradient);
        }
        return after;
    }

    private static IReadOnlyList<Rgb> PaletteFor(NativeSkyEditPlan edit)
    {
        string preset = (edit.PalettePreset ?? "").Trim();
        if (edit.IsOriginalPreset)
        {
            string palette = string.IsNullOrWhiteSpace(edit.CustomPaletteHex)
                ? SkyboxPresetCatalog.FindOriginal(preset).PaletteHex
                : edit.CustomPaletteHex;
            return ParsePalette(palette);
        }
        if (string.Equals(preset, "custom", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(preset, "Custom", StringComparison.OrdinalIgnoreCase))
        {
            return ParsePalette(edit.CustomPaletteHex);
        }
        if (preset.Contains("dusk", StringComparison.OrdinalIgnoreCase) || preset.Contains("sunset", StringComparison.OrdinalIgnoreCase) || preset.Contains("darkhollow", StringComparison.OrdinalIgnoreCase))
            return ParsePalette("#24133F #7A355D #D86A61 #FFD49A");
        return ParsePalette("#07122F #183A72 #4E78AD #B4C7E5");
    }

    private static IReadOnlyList<Rgb> ParsePalette(string text)
    {
        string[] tokens = (text ?? "").Split([' ', ',', ';', '|', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            throw new InvalidOperationException("Custom sky colors need at least one #RRGGBB value.");
        return tokens.Select(token =>
        {
            string clean = token.Trim();
            if (clean.StartsWith('#'))
                clean = clean[1..];
            if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                clean = clean[2..];
            if (clean.Length != 6 || !clean.All(Uri.IsHexDigit))
                throw new InvalidOperationException($"Bad sky color '{token}'. Use #RRGGBB.");
            return new Rgb(
                byte.Parse(clean[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(clean[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(clean[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }).ToArray();
    }

    private static void ApplyMappedColor(byte[] bytes, int offset, IReadOnlyList<Rgb> gradient)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new InvalidDataException("A parsed sky palette offset is outside its block.");
        double luminance = ((0.299 * bytes[offset]) + (0.587 * bytes[offset + 1]) + (0.114 * bytes[offset + 2])) / 255.0;
        Rgb mapped = SampleGradient(gradient, luminance);
        bytes[offset] = mapped.R;
        bytes[offset + 1] = mapped.G;
        bytes[offset + 2] = mapped.B;
    }

    private static Rgb SampleGradient(IReadOnlyList<Rgb> gradient, double t)
    {
        if (gradient.Count == 1)
            return gradient[0];
        double position = Math.Clamp(t, 0, 1) * (gradient.Count - 1);
        int left = Math.Min((int)Math.Floor(position), gradient.Count - 1);
        int right = Math.Min(left + 1, gradient.Count - 1);
        double amount = position - left;
        return new Rgb(
            (byte)Math.Round(gradient[left].R + ((gradient[right].R - gradient[left].R) * amount)),
            (byte)Math.Round(gradient[left].G + ((gradient[right].G - gradient[left].G) * amount)),
            (byte)Math.Round(gradient[left].B + ((gradient[right].B - gradient[left].B) * amount)));
    }

    private static byte[] BuildPaddedReplacement(int targetLength, byte[] donor)
    {
        if (donor.Length > targetLength)
            throw new InvalidOperationException($"Sky donor is {donor.Length:N0} bytes, larger than the {targetLength:N0}-byte target capacity.");
        byte[] result = new byte[targetLength];
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(0, 4), targetLength);
        donor.AsSpan(4).CopyTo(result.AsSpan(4));
        return result;
    }

    private static string ResolveImportPath(string workspacePath, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(workspacePath, path));
    }

    private static string NormalizePaletteLabel(NativeSkyEditPlan edit) =>
        string.Equals(edit.PalettePreset, "custom", StringComparison.OrdinalIgnoreCase)
            ? $"palette:{edit.CustomPaletteHex}"
            : $"palette:{edit.PalettePreset}";
    private static int CountChangedBytes(byte[] before, byte[] after)
    {
        int shared = Math.Min(before.Length, after.Length);
        int changed = Math.Abs(before.Length - after.Length);
        for (int index = 0; index < shared; index++)
        {
            if (before[index] != after[index])
                changed++;
        }
        return changed;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string HexPreview(byte[] bytes) => string.Join(' ', bytes.Take(16).Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));

    private readonly record struct Rgb(byte R, byte G, byte B);
    private sealed record NativeSkySource(string Label, string DisplayName, byte[] BlockBytes);
}

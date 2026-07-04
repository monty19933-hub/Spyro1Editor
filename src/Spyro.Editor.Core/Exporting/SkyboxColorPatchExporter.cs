using System.Globalization;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Skyboxes;

namespace Spyro.Editor.Core.Exporting;

public static class SkyboxColorPatchExporter
{
    private const int WadLba = 37;

    public static IReadOnlySet<string> NativePresetIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Custom",
        "StoneHillNight",
        "StoneHillStableNight",
        "StoneHillBestNight",
        "StoneHillNightKeeper"
    };

    public static async Task<SkyboxColorPatchResult> ExportAsync(SkyboxColorPatchRequest request, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", request.SourceImagePath);
        if (!File.Exists(request.WadAnalysisPath))
            throw new FileNotFoundException("Missing WAD analysis.", request.WadAnalysisPath);

        string outputPrefix = string.IsNullOrWhiteSpace(request.OutputPrefix)
            ? Path.Combine(Path.GetDirectoryName(request.SourceImagePath) ?? "", $"Spyro the Dragon (USA)-stonehill-skycolors-{ToSafeSlug(request.Preset.Id)}")
            : request.OutputPrefix;
        string outputImagePath = $"{outputPrefix}.bin";
        string outputCuePath = $"{outputPrefix}.cue";
        string outputPlanPath = $"{outputPrefix}.skyprimitive-color-palette-plan.json";

        (SkyboxColorPatchPlan plan, IReadOnlyList<SkyboxPatchPayload> payloads) = BuildPlanAndPayloads(
            request.SourceImagePath,
            request.SourceCuePath,
            request.WadAnalysisPath,
            outputImagePath,
            outputCuePath,
            request.Preset,
            request.CustomPaletteHex);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPlanPath) ?? ".");
        await File.WriteAllTextAsync(outputPlanPath, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);

        if (request.WriteImage)
        {
            File.Copy(request.SourceImagePath, outputImagePath, true);
            DiscLayout layout = DiscImage.DetectLayout(outputImagePath);
            await using (FileStream stream = File.Open(outputImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                foreach (SkyboxPatchPayload payload in payloads)
                {
                    DiscImage.WriteFileBytes(stream, layout, WadLba, payload.WadOffset, payload.Bytes);
                }
            }

            string cueText = DiscImage.BuildCueText(request.SourceCuePath, Path.GetFileName(outputImagePath));
            await File.WriteAllTextAsync(outputCuePath, cueText, Encoding.ASCII, cancellationToken);
        }

        return new SkyboxColorPatchResult(outputImagePath, outputCuePath, outputPlanPath, plan, request.WriteImage);
    }

    public static SkyboxColorPatchPlan BuildPlan(string sourceImagePath, string sourceCuePath, string wadAnalysisPath, string outputImagePath, string outputCuePath, SkyboxPreset preset, string customPaletteHex)
    {
        return BuildPlanAndPayloads(sourceImagePath, sourceCuePath, wadAnalysisPath, outputImagePath, outputCuePath, preset, customPaletteHex).Plan;
    }

    private static (SkyboxColorPatchPlan Plan, IReadOnlyList<SkyboxPatchPayload> Payloads) BuildPlanAndPayloads(
        string sourceImagePath,
        string sourceCuePath,
        string wadAnalysisPath,
        string outputImagePath,
        string outputCuePath,
        SkyboxPreset preset,
        string customPaletteHex)
    {
        if (!NativePresetIds.Contains(preset.Id))
            throw new InvalidOperationException($"{preset.DisplayName} still needs a native exporter port.");

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        Dictionary<string, WadSubfile> subfiles = LoadTargetSubfiles(wadAnalysisPath);
        IReadOnlyList<SkyboxTargetRecord> targets = SkyboxTargets.ForPreset(preset.Id);
        IReadOnlyList<PaletteItem> palette = BuildPalette(preset.Id, customPaletteHex);
        if (palette.Count == 0)
            throw new InvalidOperationException("Skybox palette is empty.");

        List<SkyboxColorPatch> patches = new();
        List<SkyboxPatchPayload> payloads = new();
        int cursor = 0;

        using FileStream stream = File.OpenRead(sourceImagePath);
        foreach (SkyboxTargetRecord record in targets)
        {
            for (int word = 0; word < record.WordCount; word++)
            {
                PaletteItem paletteItem = palette[cursor % palette.Count];
                foreach (SkyboxTargetPair pair in record.Pairs())
                {
                    WadSubfile subfile = subfiles[$"{pair.Entry}|{pair.Subfile}"];
                    long targetStart = pair.Start + (word * 4L);
                    if (targetStart < 0 || targetStart + 4 > subfile.Size)
                        throw new InvalidOperationException($"Target range {pair.Entry}|{pair.Subfile}|0x{targetStart:X} is outside the subfile.");

                    long wadOffset = subfile.WadOffset + targetStart;
                    byte[] before = ReadWadBytes(stream, layout, wadOffset, 4);
                    byte[] after = BuildAfterBytes(before, record, paletteItem);
                    patches.Add(new SkyboxColorPatch(
                        Label: $"{pair.Scope}-{preset.Id}-{record.Label}-w{word:00}",
                        Kind: "sky-primitive-color-palette",
                        Target: $"entry-{pair.Entry}-subfile-{pair.Subfile}",
                        TargetSubfileOffset: $"0x{targetStart:X}",
                        ByteLength: 4,
                        TargetWadOffset: $"0x{wadOffset:X}",
                        TargetImageOffset: $"0x{ConvertWadOffsetToImageOffset(layout, wadOffset):X}",
                        PaletteKind: record.RgbHex.Length > 0 ? "target-literal" : paletteItem.Kind,
                        PaletteColor: record.RgbHex.Length > 0 ? record.RgbHex : paletteItem.Color,
                        BeforeHexPreview: ToHex(before),
                        AfterHexPreview: ToHex(after)));
                    payloads.Add(new SkyboxPatchPayload(wadOffset, after));
                }

                cursor++;
            }
        }

        SkyboxColorPatchPlan plan = new(
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceImagePath: sourceImagePath,
            OutputImagePath: outputImagePath,
            OutputCuePath: outputCuePath,
            WadAnalysisPath: wadAnalysisPath,
            Preset: preset.Id,
            PaletteInput: customPaletteHex,
            TargetRecordCount: targets.Count,
            PaletteWordCount: palette.Count,
            PatchCount: patches.Count,
            TotalPatchedBytes: patches.Sum(patch => patch.ByteLength),
            Patches: patches);

        return (plan, payloads);
    }

    private static byte[] BuildAfterBytes(byte[] before, SkyboxTargetRecord record, PaletteItem paletteItem)
    {
        byte[] rgb = record.RgbHex.Length > 0
            ? ParseHexColor(record.RgbHex)
            : paletteItem.Kind switch
            {
                "flat-night" => [0x14, 0x31, 0x66],
                "smooth-night-grade" => ConvertRgbToSmoothNight(before),
                _ => paletteItem.Rgb
            };

        return [rgb[0], rgb[1], rgb[2], before[3]];
    }

    private static IReadOnlyList<PaletteItem> BuildPalette(string presetId, string customPaletteHex)
    {
        return presetId switch
        {
            "Custom" => ParsePalette(customPaletteHex),
            "StoneHillNight" => ParsePalette("#081132 #111D4D #1F316F #314A8C #667CA8 #9DAED0 #CDD5EA #2B235F"),
            "StoneHillStableNight" => [new PaletteItem("smooth-night-grade", "#101F54..#173B78", [])],
            "StoneHillBestNight" or "StoneHillNightKeeper" => [new PaletteItem("flat-night", "#143166", [])],
            _ => throw new InvalidOperationException($"Unsupported native skybox preset: {presetId}")
        };
    }

    private static IReadOnlyList<PaletteItem> ParsePalette(string value)
    {
        string[] tokens = value.Split([' ', ',', ';', '|', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            throw new InvalidOperationException("Custom palette is empty. Use colors like #0B1038 #243A80 #AAB6D8.");

        return tokens.Select(token =>
        {
            byte[] rgb = ParseHexColor(token);
            string clean = token.Trim();
            if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                clean = clean[2..];
            if (!clean.StartsWith('#'))
                clean = $"#{clean}";
            return new PaletteItem("literal", clean, rgb);
        }).ToArray();
    }

    private static byte[] ParseHexColor(string value)
    {
        string text = value.Trim();
        if (text.StartsWith('#'))
            text = text[1..];
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            text = text[2..];
        if (text.Length != 6 || !text.All(Uri.IsHexDigit))
            throw new InvalidOperationException($"Bad color '{value}'. Use #RRGGBB.");

        return
        [
            byte.Parse(text[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(text[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(text[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
        ];
    }

    private static byte[] ConvertRgbToSmoothNight(byte[] before)
    {
        double t = ((0.299 * before[0]) + (0.587 * before[1]) + (0.114 * before[2])) / 255.0;
        t = Math.Clamp(t, 0.0, 1.0);
        return
        [
            (byte)Math.Round(16 + (7 * t)),
            (byte)Math.Round(31 + (28 * t)),
            (byte)Math.Round(84 + (36 * t))
        ];
    }

    private static Dictionary<string, WadSubfile> LoadTargetSubfiles(string wadAnalysisPath)
    {
        using FileStream stream = File.OpenRead(wadAnalysisPath);
        using JsonDocument document = JsonDocument.Parse(stream);
        return new Dictionary<string, WadSubfile>
        {
            ["10|1"] = ReadSubfile(document.RootElement, 10, 1),
            ["12|1"] = ReadSubfile(document.RootElement, 12, 1)
        };
    }

    private static WadSubfile ReadSubfile(JsonElement root, int entryIndex, int subfileIndex)
    {
        foreach (JsonElement entry in root.GetProperty("entries").EnumerateArray())
        {
            if (JsonValue.GetInt32(entry, "index", -1) != entryIndex)
                continue;

            long entryOffset = JsonValue.GetInt64(entry, "offset", -1);
            JsonElement subfiles = entry.GetProperty("level").GetProperty("subfiles");
            foreach (JsonElement subfile in subfiles.EnumerateArray())
            {
                if (JsonValue.GetInt32(subfile, "index", -1) == subfileIndex)
                {
                    long subfileOffset = JsonValue.GetInt64(subfile, "offset", -1);
                    int size = JsonValue.GetInt32(subfile, "size", -1);
                    return new WadSubfile(entryIndex, subfileIndex, entryOffset + subfileOffset, size);
                }
            }
        }

        throw new InvalidOperationException($"WAD entry {entryIndex} subfile {subfileIndex} was not found.");
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

    private static string ToHex(byte[] bytes) => string.Join(" ", bytes.Select(value => $"{value:X2}"));

    private static string ToSafeSlug(string value)
    {
        string slug = new string(value.ToLowerInvariant().Select(ch => char.IsAsciiLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(slug) ? "skybox" : slug;
    }

    private sealed record PaletteItem(string Kind, string Color, byte[] Rgb);
    private sealed record WadSubfile(int EntryIndex, int SubfileIndex, long WadOffset, int Size);
    private sealed record SkyboxPatchPayload(long WadOffset, byte[] Bytes);
}

public sealed record SkyboxColorPatchRequest(
    string SourceImagePath,
    string SourceCuePath,
    string WadAnalysisPath,
    string OutputPrefix,
    SkyboxPreset Preset,
    string CustomPaletteHex,
    bool WriteImage);

public sealed record SkyboxColorPatchResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputPlanPath,
    SkyboxColorPatchPlan Plan,
    bool WroteImage);

public sealed record SkyboxColorPatchPlan(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    string OutputImagePath,
    string OutputCuePath,
    string WadAnalysisPath,
    string Preset,
    string PaletteInput,
    int TargetRecordCount,
    int PaletteWordCount,
    int PatchCount,
    int TotalPatchedBytes,
    IReadOnlyList<SkyboxColorPatch> Patches);

public sealed record SkyboxColorPatch(
    string Label,
    string Kind,
    string Target,
    string TargetSubfileOffset,
    int ByteLength,
    string TargetWadOffset,
    string TargetImageOffset,
    string PaletteKind,
    string PaletteColor,
    string BeforeHexPreview,
    string AfterHexPreview);

internal sealed record SkyboxTargetPair(int Entry, int Subfile, long Start, string Scope);

internal sealed record SkyboxTargetRecord(string Label, int WordCount, long? Portal, long? Loaded, string RgbHex = "")
{
    public IEnumerable<SkyboxTargetPair> Pairs()
    {
        if (Portal.HasValue)
            yield return new SkyboxTargetPair(10, 1, Portal.Value, "portal");
        if (Loaded.HasValue)
            yield return new SkyboxTargetPair(12, 1, Loaded.Value, "loaded");
    }
}

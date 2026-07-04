using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Text;

namespace Spyro.Editor.Core.Exporting;

public static class LevelTextPatchExporter
{
    private static readonly byte[] AnchorStart = Encoding.ASCII.GetBytes("DRAGON X");
    private static readonly byte[] AnchorEnd = Encoding.ASCII.GetBytes("BEHIND YOU.");

    public static async Task<LevelTextPatchResult> ExportAsync(LevelTextPatchRequest request, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", request.SourceImagePath);

        string replacement = TextTargetCatalog.NormalizeReplacement(request.ReplacementText);
        if (!TextTargetCatalog.IsSafeReplacement(replacement, request.Target.MaxLength))
            throw new InvalidOperationException($"Use {request.Target.MaxLength} or fewer safe characters for {request.Target.OriginalText}.");

        string outputPrefix = string.IsNullOrWhiteSpace(request.OutputPrefix)
            ? Path.Combine(Path.GetDirectoryName(request.SourceImagePath) ?? "", $"Spyro the Dragon (USA)-text-{request.Target.ScriptKey.ToLowerInvariant()}-{ToSafeSlug(replacement)}")
            : request.OutputPrefix;
        string outputImagePath = $"{outputPrefix}.bin";
        string outputCuePath = $"{outputPrefix}.cue";
        string outputPlanPath = $"{outputPrefix}.level-text-plan.json";

        LevelTextPatchPlan plan = BuildPlan(request.SourceImagePath, request.SourceCuePath, outputImagePath, outputCuePath, request.Target, replacement);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPlanPath) ?? ".");
        await File.WriteAllTextAsync(outputPlanPath, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);

        if (request.WriteImage)
        {
            File.Copy(request.SourceImagePath, outputImagePath, true);
            await using (FileStream stream = File.Open(outputImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                DiscImage.WriteFileBytes(stream, new DiscLayout(plan.SectorSize, plan.UserOffset, 0, 0), plan.ExeLba, plan.ExeFileOffset, HexToBytes(plan.AfterHexPreview));
            }

            string cueText = DiscImage.BuildCueText(request.SourceCuePath, Path.GetFileName(outputImagePath));
            await File.WriteAllTextAsync(outputCuePath, cueText, Encoding.ASCII, cancellationToken);
        }

        return new LevelTextPatchResult(outputImagePath, outputCuePath, outputPlanPath, plan, request.WriteImage);
    }

    public static LevelTextPatchPlan BuildPlan(string sourceImagePath, string sourceCuePath, string outputImagePath, string outputCuePath, TextTargetEntry target, string replacement)
    {
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        DiscFileRecord exe = DiscImage.FindRootFileRecord(stream, layout, IsExecutableName);
        byte[] exeBytes = DiscImage.ReadFileBytes(stream, layout, exe.Lba, 0, exe.Size);

        int tableStart = DiscImage.IndexOfBytes(exeBytes, AnchorStart, 0);
        if (tableStart < 0)
            throw new InvalidOperationException("Could not find the level text table anchor.");

        int tableEnd = DiscImage.IndexOfBytes(exeBytes, AnchorEnd, tableStart);
        if (tableEnd < 0)
            throw new InvalidOperationException("Could not find the end of the level text table.");

        byte[] originalBytes = Encoding.ASCII.GetBytes(target.OriginalText);
        int targetOffset = DiscImage.IndexOfBytes(exeBytes, originalBytes, tableStart);
        while (targetOffset >= 0 && targetOffset > tableEnd)
            targetOffset = DiscImage.IndexOfBytes(exeBytes, originalBytes, targetOffset + 1);

        if (targetOffset < 0 || targetOffset > tableEnd)
            throw new InvalidOperationException($"Could not find {target.OriginalText} inside the anchored level-name table.");

        byte[] replacementBytes = Encoding.ASCII.GetBytes(replacement);
        byte[] patchBytes = new byte[originalBytes.Length];
        Array.Copy(replacementBytes, 0, patchBytes, 0, replacementBytes.Length);

        byte[] beforeBytes = new byte[originalBytes.Length];
        Array.Copy(exeBytes, targetOffset, beforeBytes, 0, beforeBytes.Length);

        return new LevelTextPatchPlan(
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceImagePath: sourceImagePath,
            OutputImagePath: outputImagePath,
            OutputCuePath: outputCuePath,
            TargetLevelKey: target.ScriptKey,
            OriginalName: target.OriginalText,
            ReplacementName: replacement,
            ExeName: exe.Name,
            ExeLba: exe.Lba,
            ExeFileOffset: targetOffset,
            ImageOffset: DiscImage.ConvertFileOffsetToImageOffset(layout, exe.Lba, targetOffset),
            ByteLength: patchBytes.Length,
            BeforeHexPreview: ToHex(beforeBytes),
            AfterHexPreview: ToHex(patchBytes),
            SectorSize: layout.SectorSize,
            UserOffset: layout.UserOffset,
            Notes:
            [
                "Patches the anchored executable level-name string table only.",
                "Replacement is fixed-length or shorter and remaining bytes are nulled.",
                "This is expected to affect executable text render paths such as fly-in titles; portal label behavior still needs in-game confirmation."
            ]);
    }

    private static bool IsExecutableName(string name)
    {
        string upper = name.ToUpperInvariant();
        return upper.StartsWith("SCUS", StringComparison.Ordinal)
            || upper.StartsWith("SCES", StringComparison.Ordinal)
            || upper.StartsWith("SCPS", StringComparison.Ordinal)
            || upper.StartsWith("SLUS", StringComparison.Ordinal)
            || upper.StartsWith("SLES", StringComparison.Ordinal)
            || upper.StartsWith("SLPS", StringComparison.Ordinal);
    }

    private static string ToHex(byte[] bytes) => string.Join(" ", bytes.Select(value => $"{value:X2}"));

    private static byte[] HexToBytes(string text)
    {
        string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Select(part => Convert.ToByte(part, 16)).ToArray();
    }

    private static string ToSafeSlug(string value)
    {
        string slug = new string(value.ToLowerInvariant().Select(ch => char.IsAsciiLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(slug) ? "text" : slug;
    }
}

public sealed record LevelTextPatchRequest(
    string SourceImagePath,
    string SourceCuePath,
    string OutputPrefix,
    TextTargetEntry Target,
    string ReplacementText,
    bool WriteImage);

public sealed record LevelTextPatchResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputPlanPath,
    LevelTextPatchPlan Plan,
    bool WroteImage);

public sealed record LevelTextPatchPlan(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    string OutputImagePath,
    string OutputCuePath,
    string TargetLevelKey,
    string OriginalName,
    string ReplacementName,
    string ExeName,
    int ExeLba,
    int ExeFileOffset,
    long ImageOffset,
    int ByteLength,
    string BeforeHexPreview,
    string AfterHexPreview,
    int SectorSize,
    int UserOffset,
    IReadOnlyList<string> Notes);

using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

public static class ExeStringPatchExporter
{
    public static async Task<ExeStringPatchResult> ExportAsync(ExeStringPatchRequest request, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", request.SourceImagePath);

        string outputPrefix = string.IsNullOrWhiteSpace(request.OutputPrefix)
            ? Path.Combine(Path.GetDirectoryName(request.SourceImagePath) ?? "", $"Spyro the Dragon (USA)-text-{ToSafeSlug(request.OriginalText)}-{ToSafeSlug(request.ReplacementText)}")
            : request.OutputPrefix;
        string outputImagePath = $"{outputPrefix}.bin";
        string outputCuePath = $"{outputPrefix}.cue";
        string outputPlanPath = $"{outputPrefix}.exe-string-plan.json";

        ExeStringPatchPlan plan = BuildPlan(
            request.SourceImagePath,
            request.SourceCuePath,
            outputImagePath,
            outputCuePath,
            request.OriginalText,
            request.ReplacementText);

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

        return new ExeStringPatchResult(outputImagePath, outputCuePath, outputPlanPath, plan, request.WriteImage);
    }

    public static ExeStringPatchPlan BuildPlan(string sourceImagePath, string sourceCuePath, string outputImagePath, string outputCuePath, string originalText, string replacementText)
    {
        string original = originalText.Trim();
        string replacement = replacementText.Trim();
        if (string.IsNullOrWhiteSpace(original))
            throw new InvalidOperationException("Choose the original executable text to replace.");
        if (string.IsNullOrWhiteSpace(replacement))
            throw new InvalidOperationException("Replacement text cannot be empty.");
        if (!IsPrintableAscii(original) || !IsPrintableAscii(replacement))
            throw new InvalidOperationException("Executable string edits must use printable ASCII text.");

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        DiscFileRecord exe = DiscImage.FindRootFileRecord(stream, layout, IsExecutableName);
        byte[] exeBytes = DiscImage.ReadFileBytes(stream, layout, exe.Lba, 0, exe.Size);

        byte[] originalBytes = Encoding.ASCII.GetBytes(original);
        int targetOffset = FindUniqueOffset(exeBytes, originalBytes, original);
        byte[] replacementBytes = Encoding.ASCII.GetBytes(replacement);
        if (replacementBytes.Length > originalBytes.Length)
            throw new InvalidOperationException($"Use {originalBytes.Length} or fewer bytes for this fixed text slot.");

        byte[] patchBytes = new byte[originalBytes.Length];
        Array.Copy(replacementBytes, 0, patchBytes, 0, replacementBytes.Length);
        byte[] beforeBytes = new byte[originalBytes.Length];
        Array.Copy(exeBytes, targetOffset, beforeBytes, 0, beforeBytes.Length);

        return new ExeStringPatchPlan(
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceImagePath: sourceImagePath,
            OutputImagePath: outputImagePath,
            OutputCuePath: outputCuePath,
            OriginalText: original,
            ReplacementText: replacement,
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
                "Patches one executable ASCII string by exact text match.",
                "Replacement is fixed-length or shorter and remaining bytes are nulled.",
                "Longer text and pointer-moving are deliberately not attempted by this editor path."
            ]);
    }

    private static int FindUniqueOffset(byte[] haystack, byte[] needle, string originalText)
    {
        int found = -1;
        int offset = DiscImage.IndexOfBytes(haystack, needle, 0);
        while (offset >= 0)
        {
            if (found >= 0)
                throw new InvalidOperationException($"{originalText} appears more than once; this editor path needs a unique executable string.");

            found = offset;
            offset = DiscImage.IndexOfBytes(haystack, needle, offset + 1);
        }

        if (found < 0)
            throw new InvalidOperationException($"Could not find {originalText} in the executable.");

        return found;
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

    private static bool IsPrintableAscii(string value)
    {
        return value.All(ch => ch is >= ' ' and <= '~');
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

public sealed record ExeStringPatchRequest(
    string SourceImagePath,
    string SourceCuePath,
    string OutputPrefix,
    string OriginalText,
    string ReplacementText,
    bool WriteImage);

public sealed record ExeStringPatchResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputPlanPath,
    ExeStringPatchPlan Plan,
    bool WroteImage);

public sealed record ExeStringPatchPlan(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    string OutputImagePath,
    string OutputCuePath,
    string OriginalText,
    string ReplacementText,
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

using System.Text;

namespace Spyro.Editor.Core.Exporting;

public sealed record RuntimeCandidateLoadCode(
    string TestName,
    int LevelId,
    string TargetSelection)
{
    public string InputCode =>
        $"Select; then {TestLevelWarpPatch.ActivationSequence}; then {TargetSelection}";
}

public sealed record RuntimeCandidateFinderReveal(
    string CuePath,
    string PairedBinPath,
    string HelperPath,
    string TerminalCommand,
    bool CuePairingVerified,
    bool HelperIsExecutable);

/// <summary>
/// User-facing handoff helpers for disposable runtime CUEs. The generated
/// Finder command is a sidecar only; it never participates in Create BIN,
/// release packaging, or game-image composition.
/// </summary>
public static class RuntimeCandidateTestHandoff
{
    private static readonly IReadOnlyList<RuntimeCandidateLoadCode> Id65Codes =
        Array.AsReadOnly<RuntimeCandidateLoadCode>(
        [
            new("ID65 candidate", 65, "Left, then Down"),
            new("Retail Town Square", 13, TestLevelWarpPatch.TargetSelectionText(13)),
            new("Gnasty's Loot", 64, TestLevelWarpPatch.TargetSelectionText(64)),
            new("Sunny Flight", 15, TestLevelWarpPatch.TargetSelectionText(15))
        ]);

    public static IReadOnlyList<RuntimeCandidateLoadCode> Id65ComparisonLoadCodes => Id65Codes;

    public static void AppendLoadCodeTable(
        StringBuilder builder,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(loadCodes);
        if (loadCodes.Count == 0)
            throw new ArgumentException("At least one runtime test load code is required.", nameof(loadCodes));

        builder.AppendLine("## Load codes");
        builder.AppendLine();
        builder.AppendLine(
            "For each test, cold boot or reset as instructed, reach controllable gameplay, then enter " +
            "that row's complete sequence exactly once:");
        builder.AppendLine();
        builder.AppendLine("| Test | Level ID | Complete controller input |");
        builder.AppendLine("| --- | ---: | --- |");
        foreach (RuntimeCandidateLoadCode loadCode in loadCodes)
        {
            builder.AppendLine(
                $"| {EscapeMarkdownCell(loadCode.TestName)} | {loadCode.LevelId} | " +
                $"`{loadCode.InputCode}` |");
        }
        builder.AppendLine();
    }

    public static void AppendCandidateDiscSection(
        StringBuilder builder,
        RuntimeCandidateFinderReveal finderReveal)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(finderReveal);

        builder.AppendLine("## Candidate disc");
        builder.AppendLine();
        builder.AppendLine($"- CUE: `{Path.GetFileName(finderReveal.CuePath)}`");
        builder.AppendLine(
            $"- Finder: double-click `{Path.GetFileName(finderReveal.HelperPath)}` to reveal that exact CUE.");
        builder.AppendLine($"- Terminal fallback: `{finderReveal.TerminalCommand}`");
        builder.AppendLine("- DuckStation: load the **CUE**, not the BIN.");
        builder.AppendLine();
    }

    public static async Task<RuntimeCandidateFinderReveal> WriteFinderRevealHelperAsync(
        string cuePath,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException(
                "Finder reveal helpers can be generated only on macOS.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(cuePath);
        string fullCuePath = Path.GetFullPath(cuePath);
        if (!File.Exists(fullCuePath))
            throw new FileNotFoundException("The runtime candidate CUE does not exist.", fullCuePath);
        if (!string.Equals(Path.GetExtension(fullCuePath), ".cue", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Finder reveal helpers can target only a CUE file.");

        string directory = Path.GetDirectoryName(fullCuePath) ??
            throw new InvalidOperationException("The runtime candidate CUE has no parent directory.");
        string cueName = Path.GetFileName(fullCuePath);
        string pairedBinPath = ValidateCuePairing(fullCuePath);
        string helperPath = Path.Combine(
            directory,
            $"{Path.GetFileNameWithoutExtension(fullCuePath)}-Reveal-in-Finder.command");
        string script =
            "#!/bin/zsh\n" +
            "set -euo pipefail\n" +
            "\n" +
            $"cue_name={ShellSingleQuote(cueName)}\n" +
            "cue_path=\"${0:A:h}/${cue_name}\"\n" +
            "if [[ ! -f \"$cue_path\" ]]; then\n" +
            "  print -u2 -- \"Missing runtime candidate CUE: $cue_path\"\n" +
            "  exit 1\n" +
            "fi\n" +
            "/usr/bin/open -R \"$cue_path\"\n";
        UnixFileMode helperMode =
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute |
            UnixFileMode.GroupRead |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead |
            UnixFileMode.OtherExecute;
        await WriteTextAtomicallyAsync(
            helperPath,
            script,
            helperMode,
            cancellationToken);

        string readback = await File.ReadAllTextAsync(helperPath, cancellationToken);
        if (!readback.Contains("/usr/bin/open -R \"$cue_path\"", StringComparison.Ordinal) ||
            !readback.Contains($"cue_name={ShellSingleQuote(cueName)}", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The Finder reveal helper failed exact command readback.");
        }
        bool executable = File.GetUnixFileMode(helperPath) == helperMode;
        if (!executable)
            throw new InvalidDataException("The Finder reveal helper does not have exact 0755 permissions.");

        return new RuntimeCandidateFinderReveal(
            fullCuePath,
            pairedBinPath,
            helperPath,
            $"/usr/bin/open -R {ShellSingleQuote(fullCuePath)}",
            CuePairingVerified: true,
            executable);
    }

    public static void VerifyChecklistReadback(
        string checklistText,
        RuntimeCandidateFinderReveal finderReveal,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
    {
        ArgumentNullException.ThrowIfNull(checklistText);
        ArgumentNullException.ThrowIfNull(finderReveal);
        ArgumentNullException.ThrowIfNull(loadCodes);

        if (!checklistText.Contains(Path.GetFileName(finderReveal.CuePath), StringComparison.Ordinal) ||
            !checklistText.Contains(Path.GetFileName(finderReveal.HelperPath), StringComparison.Ordinal) ||
            !checklistText.Contains(finderReveal.TerminalCommand, StringComparison.Ordinal) ||
            !checklistText.Contains("load the **CUE**, not the BIN", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The runtime checklist omitted its exact CUE or Finder reveal handoff.");
        }

        foreach (RuntimeCandidateLoadCode loadCode in loadCodes)
        {
            if (!checklistText.Contains(loadCode.TestName, StringComparison.Ordinal) ||
                !checklistText.Contains(loadCode.LevelId.ToString(), StringComparison.Ordinal) ||
                !checklistText.Contains(loadCode.InputCode, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"The runtime checklist omitted the complete load code for {loadCode.TestName}.");
            }
        }
    }

    private static async Task WriteTextAtomicallyAsync(
        string outputPath,
        string content,
        UnixFileMode outputMode,
        CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(outputPath) ??
            throw new InvalidOperationException("The runtime handoff output has no parent directory.");
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
            await using (FileStream stream = new(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             FileOptions.Asynchronous))
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            if (!OperatingSystem.IsMacOS())
                throw new PlatformNotSupportedException("Finder reveal helpers require macOS permissions.");
            File.SetUnixFileMode(temporaryPath, outputMode);
            File.Move(temporaryPath, outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string ValidateCuePairing(string cuePath)
    {
        string[] fileLines = File.ReadAllLines(cuePath, Encoding.ASCII)
            .Where(line => line.TrimStart().StartsWith("FILE ", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (fileLines.Length != 1)
            throw new InvalidDataException("The runtime candidate CUE must reference exactly one BIN.");

        string fileName = ParseCueFileName(fileLines[0]);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidDataException("The runtime candidate CUE has no readable BIN filename.");
        string directory = Path.GetDirectoryName(cuePath) ??
            throw new InvalidOperationException("The runtime candidate CUE has no parent directory.");
        string pairedBinPath = Path.GetFullPath(
            Path.IsPathRooted(fileName) ? fileName : Path.Combine(directory, fileName));
        string expectedBinPath = Path.ChangeExtension(cuePath, ".bin");
        if (!string.Equals(pairedBinPath, expectedBinPath, StringComparison.Ordinal) ||
            !File.Exists(pairedBinPath))
        {
            throw new InvalidDataException(
                "The runtime candidate CUE does not resolve to its same-prefix paired BIN.");
        }
        return pairedBinPath;
    }

    private static string ParseCueFileName(string line)
    {
        string text = line.Trim();
        int firstQuote = text.IndexOf('"');
        if (firstQuote >= 0)
        {
            int secondQuote = text.IndexOf('"', firstQuote + 1);
            return secondQuote > firstQuote ? text[(firstQuote + 1)..secondQuote] : "";
        }

        string remainder = text.Length > 5 ? text[5..].Trim() : "";
        int binary = remainder.LastIndexOf(" BINARY", StringComparison.OrdinalIgnoreCase);
        return binary >= 0 ? remainder[..binary].Trim() : remainder;
    }

    private static string EscapeMarkdownCell(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal);

    private static string ShellSingleQuote(string value) =>
        $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";
}

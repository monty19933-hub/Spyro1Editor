using System.Text;
using System.Text.RegularExpressions;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Preserves CUE track geometry while binding the edited image by its source path.</summary>
public static class DiscCueSheet
{
    public static Encoding TextEncoding { get; } = new UTF8Encoding(false, true);

    public static string BuildForImage(string sourceCuePath, string sourceImagePath, string outputBinName,
        string? layoutImagePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        if (string.IsNullOrWhiteSpace(outputBinName) || outputBinName.IndexOfAny(['"', '\r', '\n', '/', '\\']) >= 0)
            throw new InvalidDataException("The output CUE requires one safe local image filename.");
        string liveImage = File.Exists(layoutImagePath) ? layoutImagePath! : sourceImagePath;
        DiscLayout layout = DiscImage.DetectLayout(liveImage);
        string expectedMode = layout.SectorSize switch
        {
            2048 => "MODE1/2048",
            2336 => "MODE2/2336",
            2352 => "MODE2/2352",
            _ => throw new InvalidDataException("No CUE track mode is available for this image layout.")
        };
        if (!File.Exists(sourceCuePath))
            return $"FILE \"{outputBinName}\" BINARY\n  TRACK 01 {expectedMode}\n    INDEX 01 00:00:00\n";

        ParsedCue cue = Read(sourceCuePath);
        CueFile[] selected = cue.Files.Where(file => PathsEqual(file.Path, sourceImagePath)).ToArray();
        if (selected.Length == 0 || !selected.Any(file => file.DataModes.Count > 0))
            throw new InvalidDataException("The selected CUE does not reference the selected data image.");
        foreach (CueFile file in selected)
        {
            if (!file.Type.Equals("BINARY", StringComparison.OrdinalIgnoreCase) ||
                file.DataModes.Any(mode => !mode.Equals(expectedMode, StringComparison.OrdinalIgnoreCase) &&
                    !(layout.SectorSize == 2048 && mode.Equals("MODE2/2048", StringComparison.OrdinalIgnoreCase))))
                throw new InvalidDataException("The selected CUE data track mode disagrees with the detected image layout.");
            // Logical ISO extents in this editor are relative to the beginning of the image FILE.
            // A synthetic PREGAP does not consume FILE bytes and remains safe to preserve, but
            // stored pregap/audio sectors before the first data INDEX 01 require offset-aware I/O.
            if (file.Tracks.Count == 0 || file.Tracks[0].IsAudio || file.Tracks[0].Index01Frames != 0)
                throw new InvalidDataException("The edited CUE FILE must begin with a data track whose INDEX 01 is 00:00:00; stored track offsets are not supported.");
        }

        foreach (CueFile file in cue.Files)
        {
            string name;
            if (PathsEqual(file.Path, sourceImagePath))
                name = outputBinName;
            else
            {
                if (!File.Exists(file.Path))
                    throw new FileNotFoundException("A secondary CUE file is missing; its tracks cannot be preserved.", file.Path);
                name = file.Path;
            }
            if (name.IndexOfAny(['"', '\r', '\n']) >= 0)
                throw new InvalidDataException("A CUE file path contains an unrepresentable quote or line break.");
            // Preserve the legacy selected FILE line shape for existing receipt hashes.
            string type = PathsEqual(file.Path, sourceImagePath) ? "BINARY" : file.Type;
            cue.Lines[file.LineIndex] = $"FILE \"{name}\" {type}";
        }
        return string.Join('\n', cue.Lines) + "\n";
    }

    public static string? FindDataImagePath(string cuePath)
    {
        if (!File.Exists(cuePath)) return null;
        string[] paths = Read(cuePath).Files.Where(file => file.DataModes.Count > 0)
            .Select(file => file.Path).Distinct(PathComparer).ToArray();
        return paths.Length switch
        {
            0 => null,
            1 => paths[0],
            _ => throw new InvalidDataException("The CUE contains multiple data images; select the intended data image explicitly.")
        };
    }

    public static bool ReferencesDataImage(string cuePath, string imagePath) =>
        File.Exists(cuePath) && Read(cuePath).Files.Any(file => file.DataModes.Count > 0 && PathsEqual(file.Path, imagePath));

    private static ParsedCue Read(string cuePath)
    {
        string[] lines = File.ReadAllLines(cuePath, TextEncoding);
        return Parse(cuePath, lines);
    }

    internal static bool ReferencesDataImageSnapshot(ReadOnlySpan<byte> boundedCueBytes, string cuePath, string imagePath)
    {
        if (boundedCueBytes.Length is < 1 or > 65536)
            throw new InvalidDataException("CUE snapshot exceeds its bounded owner.");
        using var input = new MemoryStream(boundedCueBytes.ToArray(), writable: false);
        using var reader = new StreamReader(input, TextEncoding, detectEncodingFromByteOrderMarks: true);
        List<string> lines = [];
        while (reader.ReadLine() is string line) lines.Add(line);
        return Parse(cuePath, lines.ToArray()).Files.Any(file => file.DataModes.Count > 0 && PathsEqual(file.Path, imagePath));
    }

    private static ParsedCue Parse(string cuePath, string[] lines)
    {
        List<CueFile> files = [];
        CueFile? current = null;
        CueTrack? currentTrack = null;
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].Trim();
            if (Regex.IsMatch(line, "^FILE(?:\\s|$)", RegexOptions.IgnoreCase))
            {
                Match match = Regex.Match(line, "^FILE\\s+(?:\"(?<name>[^\"]+)\"|(?<name>\\S+))\\s+(?<type>\\S+)\\s*$", RegexOptions.IgnoreCase);
                if (!match.Success)
                    throw new InvalidDataException($"Malformed CUE FILE declaration on line {index + 1}.");
                string name = match.Groups["name"].Value;
                // Both separators appear in portable CUE sheets, independent of host OS.
                name = name.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                string resolved = Path.IsPathRooted(name) ? Path.GetFullPath(name)
                    : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(cuePath))!, name));
                current = new CueFile(index, resolved, match.Groups["type"].Value);
                currentTrack = null;
                files.Add(current);
            }
            else if (Regex.IsMatch(line, "^TRACK(?:\\s|$)", RegexOptions.IgnoreCase))
            {
                Match match = Regex.Match(line, "^TRACK\\s+\\d+\\s+(?<mode>\\S+)\\s*$", RegexOptions.IgnoreCase);
                if (!match.Success || current == null)
                    throw new InvalidDataException($"Malformed or unbound CUE TRACK declaration on line {index + 1}.");
                string mode = match.Groups["mode"].Value;
                currentTrack = new CueTrack(mode);
                current.Tracks.Add(currentTrack);
            }
            else if (Regex.IsMatch(line, "^INDEX(?:\\s|$)", RegexOptions.IgnoreCase))
            {
                Match match = Regex.Match(line, "^INDEX\\s+(?<number>\\d{2})\\s+(?<minutes>\\d+):(?<seconds>\\d{2}):(?<frames>\\d{2})\\s*$", RegexOptions.IgnoreCase);
                if (!match.Success || currentTrack == null ||
                    !int.TryParse(match.Groups["minutes"].Value, out int minutes) ||
                    !int.TryParse(match.Groups["seconds"].Value, out int seconds) || seconds > 59 ||
                    !int.TryParse(match.Groups["frames"].Value, out int frames) || frames > 74)
                    throw new InvalidDataException($"Malformed or unbound CUE INDEX declaration on line {index + 1}.");
                if (match.Groups["number"].Value == "01")
                {
                    if (currentTrack.Index01Frames != null)
                        throw new InvalidDataException($"Duplicate CUE INDEX 01 declaration on line {index + 1}.");
                    currentTrack.Index01Frames = ((long)minutes * 60 + seconds) * 75 + frames;
                }
            }
        }
        if (files.Count == 0) throw new InvalidDataException("The CUE contains no FILE declarations.");
        return new ParsedCue(lines, files);
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static bool PathsEqual(string left, string right) => PathComparer.Equals(CanonicalPath(left), CanonicalPath(right));
    private static string CanonicalPath(string path)
    {
        string full = Path.GetFullPath(path);
        return File.Exists(full) && new FileInfo(full).ResolveLinkTarget(true) is FileSystemInfo target
            ? target.FullName : full;
    }
    private sealed record ParsedCue(string[] Lines, List<CueFile> Files);
    private sealed record CueFile(int LineIndex, string Path, string Type)
    {
        public List<CueTrack> Tracks { get; } = [];
        public IReadOnlyList<string> DataModes => Tracks.Where(track => !track.IsAudio).Select(track => track.Mode).ToArray();
    }
    private sealed record CueTrack(string Mode)
    {
        public bool IsAudio => Mode.Equals("AUDIO", StringComparison.OrdinalIgnoreCase);
        public long? Index01Frames { get; set; }
    }
}

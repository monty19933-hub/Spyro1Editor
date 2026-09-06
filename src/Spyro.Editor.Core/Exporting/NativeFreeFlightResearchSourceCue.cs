using System.Text.RegularExpressions;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Exact source bytes and input-path bindings, not just normalized output text.</summary>
internal sealed class NativeFreeFlightResearchSourceCue
{
    private readonly string _imagePath, _cueIdentity, _imageIdentity;
    private readonly byte[] _source, _output;

    private NativeFreeFlightResearchSourceCue(string cue, string image, byte[] source, byte[] output)
    {
        CuePath = cue;
        _imagePath = image;
        _source = source;
        _output = output;
        _cueIdentity = PathIdentity(cue);
        _imageIdentity = PathIdentity(image);
        SourceSha256 = NativeFreeFlightResearchCandidatePlan.Hash(source);
    }

    internal string CuePath { get; }
    internal string SourceSha256 { get; }
    internal byte[] CopyOutputBytes() => _output.ToArray();

    internal static NativeFreeFlightResearchSourceCue Capture(string cuePath, string imagePath, string outputName)
    {
        cuePath = Path.GetFullPath(cuePath);
        imagePath = Path.GetFullPath(imagePath);
        if (string.IsNullOrWhiteSpace(outputName) || outputName.IndexOfAny(['"', '\r', '\n', '/', '\\']) >= 0)
            throw new InvalidDataException("Research CUE needs one safe image filename.");
        string cueIdentity = PathIdentity(cuePath), imageIdentity = PathIdentity(imagePath);
        byte[] source = Read(cuePath);
        string[] lines = DiscCueSheet.TextEncoding.GetString(source).Split('\n')
            .Select(s => s.Trim()).Where(s => s.Length != 0).ToArray();
        if (lines.Length != 3 || !Regex.IsMatch(lines[0], "^FILE \\\"[^\\\"]+\\\" BINARY$", RegexOptions.IgnoreCase) ||
            !lines[1].Equals("TRACK 01 MODE2/2352", StringComparison.OrdinalIgnoreCase) ||
            !lines[2].Equals("INDEX 01 00:00:00", StringComparison.OrdinalIgnoreCase) ||
            !DiscCueSheet.ReferencesDataImage(cuePath, imagePath))
            throw new InvalidDataException("Research profile requires one source-bound MODE2/2352 track beginning at zero; other CUE geometry is unsupported.");
        byte[] output = DiscCueSheet.TextEncoding.GetBytes($"FILE \"{outputName}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        var snapshot = new NativeFreeFlightResearchSourceCue(cuePath, imagePath, source, output);
        if (snapshot._cueIdentity != cueIdentity || snapshot._imageIdentity != imageIdentity)
            throw new InvalidDataException("Research source CUE path changed during capture.");
        snapshot.RequireFresh();
        return snapshot;
    }

    internal void RequireFresh()
    {
        if (_cueIdentity != PathIdentity(CuePath) || _imageIdentity != PathIdentity(_imagePath) ||
            !_source.AsSpan().SequenceEqual(Read(CuePath)) || !DiscCueSheet.ReferencesDataImage(CuePath, _imagePath))
            throw new InvalidDataException("Research source CUE changed exact bytes, path identity or data-image binding.");
    }

    private static byte[] Read(string path)
    {
        using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length is < 1 or > 65536)
            throw new InvalidDataException("Research source CUE size is outside its bounded owner.");
        byte[] bytes = new byte[(int)input.Length];
        input.ReadExactly(bytes);
        if (input.Length != bytes.Length) throw new InvalidDataException("Research source CUE changed during read.");
        return bytes;
    }

    private static string PathIdentity(string path)
    {
        FileInfo file = new(path);
        if (!file.Exists || Directory.Exists(path))
            throw new InvalidDataException("Research source CUE/image path is missing or not a file.");
        // The checkout intentionally links the original BIN/CUE. Bind those
        // aliases and their parent chains without claiming hostile-race safety.
        List<string> identity = [file.FullName, file.LinkTarget ?? ""];
        FileSystemInfo resolved = file.ResolveLinkTarget(true) ?? file;
        identity.Add(resolved.FullName);
        foreach (string leaf in new[] { file.FullName, resolved.FullName })
        {
            DirectoryInfo? parent = new FileInfo(leaf).Directory;
            for (int depth = 0; parent != null; parent = parent.Parent, depth++)
            {
                if (depth > 64) throw new InvalidDataException("Research source path depth exceeds its bound.");
                identity.Add(parent.FullName + "|" + parent.LinkTarget);
            }
        }
        return string.Join('\n', identity);
    }
}

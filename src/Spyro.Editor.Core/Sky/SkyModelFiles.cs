using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Sky;

/// <summary>What the OBJ and FBX sky readers share: the size limits, colour conversion and finding a picture file a model
/// names (Windows separators and letter case forgiven).</summary>
internal static class SkyModelFiles
{
    public const int MaxTriangles = 2_000_000;
    public const long MaxFileBytes = 200L * 1024 * 1024;
    public const int MaxTextureWidth = 4096;
    public static readonly Rgba32 Grey = new(128, 128, 128, 255);

    public static void CheckFileSize(string path, string what)
    {
        long length = new FileInfo(path).Length;
        if (length > MaxFileBytes)
            throw new InvalidDataException($"The {what} file is larger than 200 MB, which is too large for a sky.");
    }

    public static void CheckTriangles(long count)
    {
        if (count > MaxTriangles)
            throw new InvalidDataException("The model has more than 2,000,000 triangles, which is too many for a sky.");
    }

    public static byte ToByte(double value) => double.IsNaN(value) ? (byte)0 : (byte)Math.Clamp(Math.Round(value * 255), 0, 255);

    public static Rgba32 Color(double r, double g, double b, double a = 1) => new(ToByte(r), ToByte(g), ToByte(b), ToByte(a));

    /// <summary>The first existing file among <paramref name="references"/>: each as written (absolute, or relative to each
    /// folder in turn), then each one's bare file name in each folder. Null when none exists.</summary>
    public static string? Find(IEnumerable<string?> references, IReadOnlyList<string> folders)
    {
        List<string> cleaned = [.. references.Select(Clean).Where(r => r.Length > 0).Distinct()];
        try
        {
            foreach (string reference in cleaned)
            {
                bool windowsAbsolute = (reference.Length >= 2 && reference[1] == ':') || reference.StartsWith("//", StringComparison.Ordinal);
                if (windowsAbsolute) continue;
                if (reference.StartsWith('/'))
                {
                    if (Resolve("/", reference) is string absolute) return absolute;
                    continue;
                }
                foreach (string folder in folders)
                    if (Resolve(folder, reference) is string hit) return hit;
            }
            foreach (string reference in cleaned)
            {
                string name = reference[(reference.LastIndexOf('/') + 1)..];
                if (name.Length == 0 || name is "." or "..") continue;
                foreach (string folder in folders)
                    if (Resolve(folder, name) is string hit) return hit;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
        }
        return null;
    }

    private static string Clean(string? reference)
    {
        string r = (reference ?? "").Trim();
        if (r.Length >= 2 && r[0] == '"' && r[^1] == '"') r = r[1..^1].Trim();
        if (r.StartsWith("file://", StringComparison.OrdinalIgnoreCase)) r = Uri.UnescapeDataString(r[7..]);
        return r.Replace('\\', '/');
    }

    private static string? Resolve(string folder, string relative)
    {
        string exact = Path.GetFullPath(Path.Combine(folder, relative));
        if (File.Exists(exact)) return exact;
        string current = Path.GetFullPath(folder);
        string[] parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (part == ".") continue;
            if (part == "..") { current = Path.GetDirectoryName(current) ?? current; continue; }
            bool last = i == parts.Length - 1;
            string next = Path.Combine(current, part);
            if (last ? File.Exists(next) : Directory.Exists(next)) { current = next; continue; }
            if (!Directory.Exists(current)) return null;
            string? match = (last ? Directory.EnumerateFiles(current) : Directory.EnumerateDirectories(current))
                .FirstOrDefault(p => string.Equals(Path.GetFileName(p), part, StringComparison.OrdinalIgnoreCase));
            if (match == null) return null;
            current = match;
        }
        return File.Exists(current) ? current : null;
    }
}

/// <summary>A model's pictures, each distinct one decoded once; a picture that cannot be read becomes a note and index -1.</summary>
internal sealed class SkyTextureSet(SkyImageDecoder decoder, List<string> notes)
{
    private readonly Dictionary<string, int> byKey = new(StringComparer.OrdinalIgnoreCase);

    public List<SkyImage> Images { get; } = [];

    /// <summary>The index of the picture known by <paramref name="key"/>, decoding <paramref name="read"/>'s bytes the first time.</summary>
    public int Get(string key, string displayName, Func<byte[]> read)
    {
        if (byKey.TryGetValue(key, out int known)) return known;
        int index = -1;
        try
        {
            SkyImage image = decoder(read(), SkyModelFiles.MaxTextureWidth) ?? throw new InvalidDataException("no picture came back");
            Images.Add(image);
            index = Images.Count - 1;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Note(notes, $"The picture “{displayName}” could not be read ({e.Message.TrimEnd('.')}); the faces that use it get their colours instead.");
        }
        byKey[key] = index;
        return index;
    }

    public static void Note(List<string> notes, string note)
    {
        if (!notes.Contains(note)) notes.Add(note);
    }

    public static byte[] ReadFile(string path)
    {
        SkyModelFiles.CheckFileSize(path, "picture");
        return File.ReadAllBytes(path);
    }
}

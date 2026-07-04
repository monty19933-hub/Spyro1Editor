using Spyro.Editor.Core.Workspace;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

public sealed record DiscImageSelection(string ImagePath, string CuePath, bool ImageExists, bool CueExists);

public static class DiscImageLocator
{
    private static readonly string[] ImageNames =
    [
        "Spyro the Dragon (USA).bin",
        "Spyro the Dragon (USA)-CLEAN-ORIGINAL.bin"
    ];

    public static string FindImage(EditorWorkspace workspace)
    {
        string configured = FindConfiguredImage(workspace);
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        foreach (string path in CandidateRoots(workspace).SelectMany(root => ImageNames.Select(name => Path.Combine(root, name))))
        {
            if (File.Exists(path))
                return path;
        }

        return Path.Combine(workspace.RootPath, ImageNames[0]);
    }

    public static DiscImageSelection ConfigureImage(EditorWorkspace workspace, string selectedPath)
    {
        DiscImageSelection selection = ResolveSelection(selectedPath);
        string path = GetConfigPath(workspace);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? workspace.RootPath);

        var json = new
        {
            selectedAt = DateTime.Now.ToString("o"),
            selectedPath = Path.GetFullPath(selectedPath),
            imagePath = selection.ImagePath,
            cuePath = selection.CuePath
        };
        File.WriteAllText(path, JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
        return selection;
    }

    public static DiscImageSelection ResolveSelection(string selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
            return new DiscImageSelection("", "", false, false);

        string fullPath = Path.GetFullPath(selectedPath);
        string extension = Path.GetExtension(fullPath);
        if (extension.Equals(".cue", StringComparison.OrdinalIgnoreCase))
        {
            string image = FindImageForCue(fullPath) ?? Path.ChangeExtension(fullPath, ".bin");
            return new DiscImageSelection(image, fullPath, File.Exists(image), File.Exists(fullPath));
        }

        string cue = FindCueForImage(fullPath);
        return new DiscImageSelection(fullPath, cue, File.Exists(fullPath), File.Exists(cue));
    }

    public static string ResolveImagePath(string selectedPath)
    {
        return ResolveSelection(selectedPath).ImagePath;
    }

    public static string FindCueForImage(string imagePath)
    {
        string preferred = Path.ChangeExtension(imagePath, ".cue");
        if (File.Exists(preferred))
            return preferred;

        string directory = Path.GetDirectoryName(imagePath) ?? "";
        foreach (string name in new[] { "Spyro the Dragon (USA).cue", "Spyro the Dragon (USA)-CLEAN-ORIGINAL.cue" })
        {
            string path = Path.Combine(directory, name);
            if (File.Exists(path))
                return path;
        }

        foreach (string cuePath in EnumerateCueFiles(directory))
        {
            string? cueImage = FindImageForCue(cuePath);
            if (cueImage != null && PathsEqual(cueImage, imagePath))
                return cuePath;
        }

        return preferred;
    }

    private static string FindConfiguredImage(EditorWorkspace workspace)
    {
        string path = GetConfigPath(workspace);
        if (!File.Exists(path))
            return "";

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("imagePath", out JsonElement imageProperty))
            {
                string image = imageProperty.GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(image) && File.Exists(image))
                    return image;
            }
            if (root.TryGetProperty("selectedPath", out JsonElement selectedProperty))
            {
                string selected = selectedProperty.GetString() ?? "";
                string image = ResolveImagePath(selected);
                if (!string.IsNullOrWhiteSpace(image) && File.Exists(image))
                    return image;
            }
        }
        catch (IOException)
        {
        }
        catch (JsonException)
        {
        }

        return "";
    }

    private static string GetConfigPath(EditorWorkspace workspace)
    {
        return Path.Combine(workspace.RootPath, "_local", "settings", "source-disc.json");
    }

    private static string? FindImageForCue(string cuePath)
    {
        if (!File.Exists(cuePath))
            return null;

        string directory = Path.GetDirectoryName(cuePath) ?? "";
        foreach (string line in File.ReadLines(cuePath))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith("FILE ", StringComparison.OrdinalIgnoreCase))
                continue;

            string fileName = ParseCueFileName(trimmed);
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            string resolved = Path.IsPathRooted(fileName)
                ? fileName
                : Path.Combine(directory, fileName);
            return Path.GetFullPath(resolved);
        }

        return null;
    }

    private static string ParseCueFileName(string line)
    {
        int firstQuote = line.IndexOf('"');
        if (firstQuote >= 0)
        {
            int secondQuote = line.IndexOf('"', firstQuote + 1);
            if (secondQuote > firstQuote)
                return line.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }

        string rest = line[5..].Trim();
        int binaryIndex = rest.LastIndexOf(" BINARY", StringComparison.OrdinalIgnoreCase);
        if (binaryIndex >= 0)
            rest = rest[..binaryIndex].Trim();
        return rest;
    }

    private static IEnumerable<string> EnumerateCueFiles(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            yield break;

        foreach (string path in SafeEnumerateFiles(directory, "*.cue"))
            yield return path;
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal);
    }

    private static IEnumerable<string> CandidateRoots(EditorWorkspace workspace)
    {
        yield return workspace.RootPath;

        DirectoryInfo? parent = Directory.GetParent(workspace.RootPath);
        if (parent == null)
            yield break;

        foreach (DirectoryInfo sibling in SafeEnumerateDirectories(parent))
        {
            yield return sibling.FullName;
            foreach (DirectoryInfo nested in SafeEnumerateDirectories(sibling))
                yield return nested.FullName;
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string directory, string searchPattern)
    {
        try
        {
            return Directory.EnumerateFiles(directory, searchPattern).ToArray();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<DirectoryInfo> SafeEnumerateDirectories(DirectoryInfo directory)
    {
        try
        {
            return directory.EnumerateDirectories().ToArray();
        }
        catch (IOException)
        {
            return Array.Empty<DirectoryInfo>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<DirectoryInfo>();
        }
    }
}

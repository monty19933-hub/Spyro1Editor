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

        foreach (string path in WorkspaceArtifactSearchPolicy.EnumerateCandidateRoots(workspace).SelectMany(root => ImageNames.Select(name => Path.Combine(root, name))))
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
        if (CueMatchesImage(preferred, imagePath))
            return preferred;

        string directory = Path.GetDirectoryName(imagePath) ?? "";
        foreach (string name in new[] { "Spyro the Dragon (USA).cue", "Spyro the Dragon (USA)-CLEAN-ORIGINAL.cue" })
        {
            string path = Path.Combine(directory, name);
            if (CueMatchesImage(path, imagePath))
                return path;
        }

        foreach (string cuePath in EnumerateCueFiles(directory))
        {
            if (CueMatchesImage(cuePath, imagePath))
                return cuePath;
        }

        return File.Exists(preferred) ? "" : preferred;
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
        => DiscCueSheet.FindDataImagePath(cuePath);

    private static bool CueMatchesImage(string cuePath, string imagePath)
    {
        try { return DiscCueSheet.ReferencesDataImage(cuePath, imagePath); }
        catch (InvalidDataException) { return false; }
        catch (IOException) { return false; }
        catch (ArgumentException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
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

}

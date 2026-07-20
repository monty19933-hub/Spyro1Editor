using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Editing;

public sealed record NativeMobyPathEditLoadResult(
    int AppliedPathCount,
    int AppliedNodeCount,
    IReadOnlyList<string> BlockedReasons,
    IReadOnlyList<NativeMobyPathBlockedEdit>? BlockedEdits = null)
{
    public IReadOnlyList<NativeMobyPathBlockedEdit> TargetedBlockedEdits =>
        BlockedEdits ?? Array.Empty<NativeMobyPathBlockedEdit>();
}

public sealed record NativeMobyPathBlockedEdit(
    string LevelKey,
    int OwnerTrueIndex,
    string Reason);

/// <summary>
/// Versioned persistence for native path-node edits. This is intentionally
/// separate from MobyEditStore because path nodes are not source Moby records.
/// </summary>
public static class NativeMobyPathEditStore
{
    public const string Format = "spyro-editor-native-moby-path-edits";
    public const int CurrentVersion = 1;

    public static string DefaultFileName(string levelKey) =>
        $"{LevelCatalog.NormalizeKey(levelKey)}-native-moby-path-edits.json";

    public static async Task<int> SaveAsync(
        string path,
        string levelName,
        IEnumerable<NativeMobyPath> nativePaths,
        CancellationToken cancellationToken = default)
    {
        List<object> edits = nativePaths
            .Where(nativePath => nativePath.HasEdits)
            .Select(nativePath => (object)new
            {
                levelKey = nativePath.LevelKey,
                ownerTrueIndex = nativePath.OwnerTrueIndex,
                ownerNativeClassHex = $"0x{nativePath.OwnerNativeClass:X4}",
                propertiesPointerHex = $"0x{nativePath.PropertiesPointer:X8}",
                pathPointerHex = $"0x{nativePath.PathPointer:X8}",
                nodeCount = nativePath.NodeCount,
                originalPathSha256 = nativePath.OriginalPathSha256,
                nodes = nativePath.Nodes
                    .Where(node => node.HasEdit)
                    .Select(node => new
                    {
                        index = node.Index,
                        originalRawX = node.OriginalRawX,
                        originalRawY = node.OriginalRawY,
                        originalRawZ = node.OriginalRawZ,
                        editedRawX = node.RawX,
                        editedRawY = node.RawY,
                        editedRawZ = node.RawZ,
                        preservedUnknownWord = node.UnknownWord
                    })
                    .ToArray()
            })
            .ToList();

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        var document = new
        {
            format = Format,
            version = CurrentVersion,
            generatedAt = DateTimeOffset.Now.ToString("O"),
            editor = "Spyro.Editor.Core",
            levelName = string.IsNullOrWhiteSpace(levelName) ? "Unknown" : levelName,
            note = "In-place native PathData coordinate edits; node headers and fourth words remain unchanged.",
            editCount = edits.Count,
            edits
        };
        JsonSerializerOptions options = new() { WriteIndented = true };
        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, document, options, cancellationToken);
        return edits.Count;
    }

    public static NativeMobyPathEditLoadResult Load(string path, IEnumerable<NativeMobyPath> nativePaths)
    {
        NativeMobyPath[] pathArray = nativePaths.ToArray();
        foreach (NativeMobyPath nativePath in pathArray)
            nativePath.ResetEdits();
        if (!File.Exists(path))
            return new NativeMobyPathEditLoadResult(0, 0, []);

        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        string format = ReadString(root, "format");
        int version = ReadInt32(root, "version", -1);
        if (!string.Equals(format, Format, StringComparison.Ordinal))
            throw new InvalidOperationException($"'{path}' is not a native Moby path edit document.");
        if (version != CurrentVersion)
            throw new InvalidOperationException($"Native Moby path edit version {version} is unsupported; expected {CurrentVersion}.");
        if (!root.TryGetProperty("edits", out JsonElement edits) || edits.ValueKind != JsonValueKind.Array)
            return new NativeMobyPathEditLoadResult(0, 0, []);

        Dictionary<(string LevelKey, int TrueIndex), NativeMobyPath> byOwner = pathArray.ToDictionary(
            nativePath => (LevelCatalog.NormalizeKey(nativePath.LevelKey), nativePath.OwnerTrueIndex));
        List<string> blocked = [];
        List<NativeMobyPathBlockedEdit> blockedEdits = [];
        int appliedPaths = 0;
        int appliedNodes = 0;
        foreach (JsonElement edit in edits.EnumerateArray())
        {
            string levelKey = LevelCatalog.NormalizeKey(ReadString(edit, "levelKey"));
            int trueIndex = ReadInt32(edit, "ownerTrueIndex", -1);
            if (!byOwner.TryGetValue((levelKey, trueIndex), out NativeMobyPath? nativePath))
            {
                string reason = "owner path is not present in the selected disc.";
                blocked.Add($"{levelKey} T{trueIndex}: {reason}");
                blockedEdits.Add(new NativeMobyPathBlockedEdit(levelKey, trueIndex, reason));
                continue;
            }

            string? mismatch = ValidateIdentity(edit, nativePath);
            if (mismatch != null)
            {
                blocked.Add($"{nativePath.LevelName} T{trueIndex}: {mismatch}");
                blockedEdits.Add(new NativeMobyPathBlockedEdit(nativePath.LevelKey, trueIndex, mismatch));
                continue;
            }
            if (!edit.TryGetProperty("nodes", out JsonElement nodes) || nodes.ValueKind != JsonValueKind.Array)
                continue;

            List<(NativePathNode Node, int X, int Y, int Z)> pending = [];
            string? nodeMismatch = null;
            foreach (JsonElement nodeEdit in nodes.EnumerateArray())
            {
                int nodeIndex = ReadInt32(nodeEdit, "index", -1);
                if (nodeIndex < 0 || nodeIndex >= nativePath.Nodes.Count)
                {
                    nodeMismatch = $"saved node {nodeIndex} is outside the current {nativePath.Nodes.Count}-node path.";
                    break;
                }
                NativePathNode node = nativePath.Nodes[nodeIndex];
                if (ReadInt32(nodeEdit, "originalRawX", int.MinValue) != node.OriginalRawX ||
                    ReadInt32(nodeEdit, "originalRawY", int.MinValue) != node.OriginalRawY ||
                    ReadInt32(nodeEdit, "originalRawZ", int.MinValue) != node.OriginalRawZ ||
                    ReadInt32(nodeEdit, "preservedUnknownWord", int.MinValue) != node.UnknownWord)
                {
                    nodeMismatch = $"node {nodeIndex}'s original bytes do not match the selected disc.";
                    break;
                }
                pending.Add((
                    node,
                    ReadInt32(nodeEdit, "editedRawX", node.OriginalRawX),
                    ReadInt32(nodeEdit, "editedRawY", node.OriginalRawY),
                    ReadInt32(nodeEdit, "editedRawZ", node.OriginalRawZ)));
            }

            if (nodeMismatch != null)
            {
                blocked.Add($"{nativePath.LevelName} T{trueIndex}: {nodeMismatch}");
                blockedEdits.Add(new NativeMobyPathBlockedEdit(nativePath.LevelKey, trueIndex, nodeMismatch));
                continue;
            }
            foreach ((NativePathNode node, int rawX, int rawY, int rawZ) in pending)
                node.SetRawPosition(rawX, rawY, rawZ);
            if (pending.Count > 0)
            {
                appliedPaths++;
                appliedNodes += pending.Count;
            }
        }

        return new NativeMobyPathEditLoadResult(appliedPaths, appliedNodes, blocked, blockedEdits);
    }

    private static string? ValidateIdentity(JsonElement edit, NativeMobyPath nativePath)
    {
        if (ReadUInt32(edit, "ownerNativeClassHex") != nativePath.OwnerNativeClass)
            return "native class no longer matches.";
        if (ReadUInt32(edit, "propertiesPointerHex") != nativePath.PropertiesPointer)
            return "properties pointer no longer matches.";
        if (ReadUInt32(edit, "pathPointerHex") != nativePath.PathPointer)
            return "path pointer no longer matches.";
        if (ReadInt32(edit, "nodeCount", -1) != nativePath.NodeCount)
            return "path node count no longer matches.";
        if (!string.Equals(ReadString(edit, "originalPathSha256"), nativePath.OriginalPathSha256, StringComparison.OrdinalIgnoreCase))
            return "original path fingerprint no longer matches.";
        return null;
    }

    private static string ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement property))
            return "";
        return property.ValueKind == JsonValueKind.String ? property.GetString() ?? "" : property.ToString();
    }

    private static int ReadInt32(JsonElement element, string name, int fallback)
    {
        if (!element.TryGetProperty(name, out JsonElement property))
            return fallback;
        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int number))
            return number;
        string text = property.ValueKind == JsonValueKind.String ? property.GetString() ?? "" : property.ToString();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out int hex))
            return hex;
        return int.TryParse(text, out int value) ? value : fallback;
    }

    private static uint ReadUInt32(JsonElement element, string name)
    {
        string text = ReadString(element, name).Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            uint.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out uint hex))
            return hex;
        return uint.TryParse(text, out uint value) ? value : 0;
    }
}

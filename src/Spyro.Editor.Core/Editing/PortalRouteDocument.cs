using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Editing;

public sealed record PortalEndpoint(string HomeworldKey, int PathTrueIndex)
{
    public HomeworldPortalControlDefinition Resolve() => HomeworldPortalControlCatalog.All.SingleOrDefault(p =>
        p.HomeworldKey == HomeworldKey && p.PathTrueIndex == PathTrueIndex)
        ?? throw new InvalidDataException("The selected return portal is not present in the original game.");
}

public sealed record PortalDestinationEdit(PortalEndpoint Portal, int DestinationLevelId);
public sealed record LevelReturnHomeEdit(int LevelId, PortalEndpoint Portal);

/// <summary>Portal identities stay tied to their source controls even when their destination or position changes.</summary>
public sealed record PortalRouteDocument(int SchemaVersion, string SourceImageSha256,
    IReadOnlyList<PortalDestinationEdit> Destinations, IReadOnlyList<LevelReturnHomeEdit> Returns)
{
    public static PortalRouteDocument Empty => new(1, UniversalSourceLibrary.OriginalUsaSha256, [], []);
    public bool HasEdits => Destinations.Count != 0 || Returns.Count != 0;
    public static bool IsLevelId(int id) => id is >= 10 and <= 65 && id % 10 <= 5;

    public PortalRouteDocument Validate()
    {
        if (SchemaVersion != 1 || SourceImageSha256 != UniversalSourceLibrary.OriginalUsaSha256 ||
            Destinations == null || Returns == null || Destinations.Count > 29 || Returns.Count > 36)
            throw new InvalidDataException("Portal routes have an unsupported format or source game. Your saved routes were preserved.");
        HashSet<PortalEndpoint> destinations = [];
        foreach (var edit in Destinations)
        {
            if (edit?.Portal == null || !IsLevelId(edit.DestinationLevelId) || !destinations.Add(edit.Portal))
                throw new InvalidDataException("Portal routes contain an invalid or duplicate destination.");
            _ = edit.Portal.Resolve();
        }
        HashSet<int> returns = [];
        foreach (var edit in Returns)
        {
            if (edit?.Portal == null || !IsLevelId(edit.LevelId) || !returns.Add(edit.LevelId))
                throw new InvalidDataException("Portal routes contain an invalid or duplicate Return Home setting.");
            _ = edit.Portal.Resolve();
        }
        return this;
    }

    public int DestinationFor(PortalEndpoint portal)
    {
        var original = portal.Resolve();
        return Destinations.SingleOrDefault(d => d.Portal == portal)?.DestinationLevelId ?? original.DestinationLevelId;
    }

    public PortalEndpoint? ReturnFor(int levelId)
    {
        if (!IsLevelId(levelId)) throw new ArgumentOutOfRangeException(nameof(levelId));
        if (Returns.SingleOrDefault(r => r.LevelId == levelId) is { } saved) return saved.Portal;
        var original = HomeworldPortalControlCatalog.All.SingleOrDefault(p => p.DestinationLevelId == levelId);
        return original == null ? null : new(original.HomeworldKey, original.PathTrueIndex);
    }

    public PortalRouteDocument WithDestination(PortalEndpoint portal, int destinationLevelId, bool returnHere)
    {
        _ = portal.Resolve();
        if (!IsLevelId(destinationLevelId)) throw new InvalidDataException("Choose a valid destination level.");
        var next = this with
        {
            Destinations = Destinations.Where(d => d.Portal != portal).Append(new(portal, destinationLevelId))
                .OrderBy(d => d.Portal.HomeworldKey, StringComparer.Ordinal).ThenBy(d => d.Portal.PathTrueIndex).ToArray()
        };
        if (returnHere) next = next.WithReturn(destinationLevelId, portal);
        return next.Validate();
    }

    public PortalRouteDocument WithReturn(int levelId, PortalEndpoint? portal)
    {
        if (!IsLevelId(levelId)) throw new InvalidDataException("Choose a valid level for Return Home.");
        IEnumerable<LevelReturnHomeEdit> returns = Returns.Where(r => r.LevelId != levelId);
        if (portal != null) { _ = portal.Resolve(); returns = returns.Append(new(levelId, portal)); }
        return (this with { Returns = returns.OrderBy(r => r.LevelId).ToArray() }).Validate();
    }
}

public sealed record PortalRouteSnapshot(PortalRouteDocument Document, string Fingerprint)
{
    public void RequireCurrent(string workspaceRoot)
    {
        if (PortalRouteStore.Capture(workspaceRoot).Fingerprint != Fingerprint)
            throw new InvalidDataException("Portal routes changed during this operation. Try again with the saved settings.");
    }
}

public static class PortalRouteStore
{
    public const string FileName = "portal-routes.json";
    public const string PreviousFileName = "portal-routes.previous.json";
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static PortalRouteSnapshot Capture(string workspaceRoot)
    {
        string path = Path.Combine(workspaceRoot, FileName);
        if (!File.Exists(path)) return new(PortalRouteDocument.Empty, "absent");
        byte[] bytes = File.ReadAllBytes(path);
        var document = JsonSerializer.Deserialize<PortalRouteDocument>(bytes, Options)
            ?? throw new InvalidDataException("The saved portal routes are empty or unreadable.");
        return new(document.Validate(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    public static PortalRouteSnapshot Save(string workspaceRoot, PortalRouteDocument document, PortalRouteSnapshot expected)
    {
        document.Validate();
        Directory.CreateDirectory(workspaceRoot);
        // Keep the lock file stable: deleting it would let another process lock
        // a different inode while an existing writer still owns the old one.
        using FileStream lease = new(Path.Combine(workspaceRoot, ".portal-routes.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        expected.RequireCurrent(workspaceRoot);
        string path = Path.Combine(workspaceRoot, FileName), stage = path + ".stage-" + Guid.NewGuid().ToString("N");
        string previous = Path.Combine(workspaceRoot, PreviousFileName), previousStage = stage + ".previous";
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, Options);
        try
        {
            File.WriteAllBytes(stage, bytes);
            expected.RequireCurrent(workspaceRoot);
            // Keep a persistent recovery copy as well as the editor's one-step undo.
            byte[] before = File.Exists(path) ? File.ReadAllBytes(path) : JsonSerializer.SerializeToUtf8Bytes(PortalRouteDocument.Empty, Options);
            File.WriteAllBytes(previousStage, before);
            expected.RequireCurrent(workspaceRoot);
            File.Move(previousStage, previous, overwrite: true);
            File.Move(stage, path, overwrite: true);
            return new(document, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }
        finally
        {
            if (File.Exists(stage)) File.Delete(stage);
            if (File.Exists(previousStage)) File.Delete(previousStage);
        }
    }
}

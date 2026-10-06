using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

public sealed record NativeFreeFlightLevelPreference(int LevelId, bool EnableFreeFlight);

/// <summary>User intent only. Neither a saved setting nor its hash grants native export authority.</summary>
public sealed class NativeFreeFlightSettingsSnapshot
{
    internal NativeFreeFlightSettingsSnapshot(string path, string? hash, IReadOnlyList<NativeFreeFlightLevelPreference> preferences,
        string retainedRecoveryDirectory = "")
    { Path = path; ContentSha256 = hash; Preferences = Array.AsReadOnly(preferences.ToArray()); RetainedRecoveryDirectory = retainedRecoveryDirectory; }
    public string Path { get; }
    public string? ContentSha256 { get; }
    public bool Exists => ContentSha256 != null;
    public IReadOnlyList<NativeFreeFlightLevelPreference> Preferences { get; }
    public string RetainedRecoveryDirectory { get; }
    public bool IsEnabled(int levelId) => Preferences.Any(p => p.LevelId == levelId && p.EnableFreeFlight);
    public bool HasExplicitSelection(int levelId) => Preferences.Any(p => p.LevelId == levelId);
    public bool GlobalEnabled => Preferences.Any(p => p.EnableFreeFlight);
    public IReadOnlyList<int> EnabledLevelIds => Preferences.Where(p => p.EnableFreeFlight).Select(p => p.LevelId).Distinct().Order().ToArray();
    public bool WholeGame => Enumerable.Range(10, 55).Where(NativeFreeFlightCompositionPlan.IsNormalLevel).All(IsEnabled);
    public bool NativeExportApproved => false;
}

public static class NativeFreeFlightSettingsStore
{
    public const string FileName = "native-free-flight-settings-v1.json";
    public const string Format = "spyro-editor-native-free-flight-settings";
    public const int MaximumDocumentBytes = 16384;
    // Cooperating writers use the lease/CAS and existing publication transaction.
    // As with the authoring store, arbitrary hostile filesystem races are not certified.
    public const bool RequiresTrustedWorkspaceDirectory = true;
    public const bool HostileFilesystemRaceSafe = false;

    public static NativeFreeFlightSettingsSnapshot Capture(string workspaceRoot)
    {
        string path = ResolvePath(workspaceRoot);
        byte[] bytes;
        try
        {
            using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length is < 1 or > MaximumDocumentBytes)
                throw new InvalidDataException("Free Flight settings exceed the bounded document size.");
            bytes = new byte[checked((int)input.Length)];
            input.ReadExactly(bytes);
            if (input.Length != bytes.Length)
                throw new InvalidDataException("Free Flight settings changed during capture.");
        }
        catch (FileNotFoundException) { return new(path, null, []); }
        return Decode(path, bytes);
    }

    /// <summary>
    /// Save one explicit choice using stale-snapshot refusal and exception rollback.
    /// In particular, false is persisted: it is an instruction to rebuild an old
    /// enabled output from original source, not permission to leave that BIN intact.
    /// The returned preference is never proof that any BIN was exported or tested.
    /// </summary>
    public static NativeFreeFlightSettingsSnapshot Save(NativeFreeFlightSettingsSnapshot expected, int levelId,
        bool enabled, CancellationToken cancellationToken = default) =>
        SaveCore(expected, levelId, enabled, cancellationToken);

    /// <summary>One atomic global choice, represented in the existing v1
    /// project schema as all thirty normal-level preferences. No game save
    /// fields or schema migration are introduced.</summary>
    public static NativeFreeFlightSettingsSnapshot SaveGlobal(NativeFreeFlightSettingsSnapshot expected,
        bool enabled, CancellationToken cancellationToken = default) =>
        SaveCore(expected, null, enabled, cancellationToken);

    private static NativeFreeFlightSettingsSnapshot SaveCore(NativeFreeFlightSettingsSnapshot expected, int? levelId,
        bool enabled, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (levelId is int selected) RequireLevel(selected);
        cancellationToken.ThrowIfCancellationRequested();
        string root = System.IO.Path.GetDirectoryName(expected.Path)!;
        if (ResolvePath(root) != expected.Path)
            throw new InvalidDataException("Free Flight snapshot does not own this preference destination.");
        string leasePath = System.IO.Path.Combine(root, ".free-flight-settings.writer.lock");
        RefuseLinkOrDirectory(leasePath);
        using FileStream lease = new(leasePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        string? stage = null;
        bool retainStaging = false;
        try
        {
            RequireCurrent(expected);
            var values = levelId is int selectedLevel
                ? expected.Preferences.Where(p => p.LevelId != selectedLevel)
                    .Append(new NativeFreeFlightLevelPreference(selectedLevel, enabled)).OrderBy(p => p.LevelId).ToArray()
                : Enumerable.Range(10, 55).Where(NativeFreeFlightCompositionPlan.IsNormalLevel)
                    .Select(id => new NativeFreeFlightLevelPreference(id, enabled)).ToArray();
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                format = Format, version = 1,
                levels = values.Select(p => new { levelId = p.LevelId, enableFreeFlight = p.EnableFreeFlight })
            }, new JsonSerializerOptions { WriteIndented = true });
            var prepared = Decode(expected.Path, bytes);
            if (prepared.ContentSha256 == expected.ContentSha256) return prepared;
            stage = System.IO.Path.Combine(root, $".free-flight-settings-{Guid.NewGuid():N}.tmp");
            using (FileStream output = new(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(bytes); output.Flush(flushToDisk: true); }
            if (!File.ReadAllBytes(stage).AsSpan().SequenceEqual(bytes))
                throw new InvalidDataException("Free Flight settings staging readback differs from its prepared bytes.");
            RequireCurrent(expected);
            var publication = ExportArtifactTransaction.Publish(root, [new(expected.Path, stage)], cancellationToken);
            retainStaging = publication.RetainedRecoveryDirectory.Length != 0;
            return new(prepared.Path, prepared.ContentSha256, prepared.Preferences, publication.RetainedRecoveryDirectory);
        }
        catch (ExportArtifactRollbackException)
        {
            retainStaging = true;
            throw;
        }
        finally
        {
            if (!retainStaging && stage != null) TryDeleteOwned(stage);
            lease.Dispose();
            TryDeleteOwned(leasePath);
        }
    }

    private static NativeFreeFlightSettingsSnapshot Decode(string path, byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            RequireProperties(root, ["format", "version", "levels"]);
            if (root.GetProperty("format").GetString() != Format || root.GetProperty("version").GetInt32() != 1)
                throw new InvalidDataException("Free Flight settings format/version is unsupported.");
            var levels = root.GetProperty("levels");
            if (levels.ValueKind != JsonValueKind.Array || levels.GetArrayLength() > 30)
                throw new InvalidDataException("Free Flight settings require bounded per-level choices.");
            List<NativeFreeFlightLevelPreference> preferences = [];
            HashSet<int> ids = [];
            foreach (var level in levels.EnumerateArray())
            {
                RequireProperties(level, ["levelId", "enableFreeFlight"]);
                int id = level.GetProperty("levelId").GetInt32();
                RequireLevel(id);
                if (!ids.Add(id)) throw new InvalidDataException("Free Flight settings repeat a native level identity.");
                preferences.Add(new(id, level.GetProperty("enableFreeFlight").GetBoolean()));
            }
            return new(path, Convert.ToHexStringLower(SHA256.HashData(bytes)), preferences.OrderBy(p => p.LevelId).ToArray());
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or OverflowException)
        { throw new InvalidDataException("Free Flight settings are malformed; they were not replaced with defaults.", error); }
    }

    private static void RequireProperties(JsonElement value, string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Free Flight settings require an object, not an implicit default.");
        var actual = value.EnumerateObject().Select(p => p.Name).ToArray();
        if (actual.Length != expected.Length || !actual.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Free Flight settings have unknown, missing or duplicate fields.");
    }

    private static string ResolvePath(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        string root = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(workspaceRoot));
        if (!Directory.Exists(root) || new DirectoryInfo(root).LinkTarget != null)
            throw new InvalidDataException("Free Flight settings require an existing, unlinked workspace directory.");
        string path = System.IO.Path.Combine(root, FileName);
        PersistencePathSafety.EnsureWritePathWithinRoot(root, path);
        RefuseLinkOrDirectory(path);
        return path;
    }

    private static void RefuseLinkOrDirectory(string path)
    {
        if (Directory.Exists(path) || new FileInfo(path).LinkTarget != null)
            throw new InvalidDataException("Free Flight preference or writer path is a directory or symbolic link.");
    }

    private static void RequireCurrent(NativeFreeFlightSettingsSnapshot expected)
    {
        var current = Capture(System.IO.Path.GetDirectoryName(expected.Path)!);
        if (current.ContentSha256 != expected.ContentSha256)
            throw new InvalidDataException("Free Flight settings changed since capture. Reload before saving.");
    }

    private static void RequireLevel(int levelId)
    {
        if (levelId < 10 || levelId > 64 || levelId % 10 > 4)
            throw new InvalidDataException("Free Flight currently accepts only normal retail level identities; flight courses and independent ID65 need separate native policies.");
    }

    private static void TryDeleteOwned(string path)
    {
        try { File.Delete(path); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
}

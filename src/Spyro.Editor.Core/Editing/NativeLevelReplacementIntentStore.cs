using System.Text.Json;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Editing;

public sealed record NativeLevelReplacementIntent(
    string Format,
    int Version,
    string IntentId,
    string ProfileId,
    int ProfileRecipeVersion,
    string TargetLevelKey,
    string DonorLevelKey,
    string SourceImageSha256,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Persists the user's selected checked recipe separately from the immutable retail baseline.
/// The document carries no evidence flag: runtime authorization is resolved exclusively from
/// the code-owned profile registry and every field must match it exactly.
/// </summary>
public static class NativeLevelReplacementIntentStore
{
    public const string IntentFormat = "spyro-editor-native-level-replacement-intent";
    public const int CurrentIntentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string GetPath(string workspaceRoot, string targetLevelKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        string key = LevelCatalog.NormalizeKey(targetLevelKey);
        if (!string.Equals(
                key,
                NativeLevelReplacementSupportCatalog.FirstTargetLevelKey,
                StringComparison.Ordinal) ||
            key.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new InvalidOperationException(
                "Only the canonical Stone Hill replacement-intent path is supported.");
        }

        string root = Path.GetFullPath(workspaceRoot);
        string path = Path.Combine(root, $"{key}-native-level-replacement-intent.json");
        PersistencePathSafety.EnsureWritePathWithinRoot(root, path);
        return path;
    }

    public static async Task<NativeLevelReplacementIntent> StartAsync(
        string workspaceRoot,
        string profileId,
        NativeLevelReplacementManifest manifest,
        LevelCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        NativeLevelReplacementProfile profile =
            NativeLevelReplacementProfileRegistry.RequireRuntimeProven(profileId, manifest, catalog);
        NativeLevelReplacementIntent intent = new(
            Format: IntentFormat,
            Version: CurrentIntentVersion,
            IntentId: BuildIntentId(profile),
            ProfileId: profile.Id,
            ProfileRecipeVersion: profile.RecipeVersion,
            TargetLevelKey: profile.NormalizedTargetLevelKey,
            DonorLevelKey: profile.NormalizedDonorLevelKey,
            SourceImageSha256: NativeLevelReplacementProfile.NormalizeSha256(
                manifest.Source.SourceImageSha256),
            CreatedAtUtc: DateTimeOffset.UtcNow);
        await WriteValidatedAsync(workspaceRoot, intent, manifest, catalog, cancellationToken);
        return intent;
    }

    public static NativeLevelReplacementIntent? Load(
        string workspaceRoot,
        string targetLevelKey,
        NativeLevelReplacementManifest manifest,
        LevelCatalog catalog)
    {
        string path = GetPath(workspaceRoot, targetLevelKey);
        if (!File.Exists(path))
            return null;

        try
        {
            NativeLevelReplacementIntent intent =
                JsonSerializer.Deserialize<NativeLevelReplacementIntent>(
                    File.ReadAllText(path),
                    JsonOptions)
                ?? throw new InvalidDataException("The native level-replacement intent is empty.");
            _ = Validate(intent, manifest, catalog);
            if (!string.Equals(
                    LevelCatalog.NormalizeKey(targetLevelKey),
                    LevelCatalog.NormalizeKey(intent.TargetLevelKey),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The replacement-intent filename and target level do not agree.");
            }
            return intent;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or
            InvalidOperationException or InvalidDataException or NullReferenceException)
        {
            throw new InvalidDataException(
                $"The native level-replacement intent cannot be read: {path}",
                exception);
        }
    }

    public static async Task SaveAsync(
        string workspaceRoot,
        NativeLevelReplacementIntent intent,
        NativeLevelReplacementManifest manifest,
        LevelCatalog catalog,
        CancellationToken cancellationToken = default) =>
        await WriteValidatedAsync(workspaceRoot, intent, manifest, catalog, cancellationToken);

    public static bool Delete(string workspaceRoot, string targetLevelKey)
    {
        string path = GetPath(workspaceRoot, targetLevelKey);
        if (!File.Exists(path))
            return false;
        File.Delete(path);
        return true;
    }

    public static NativeLevelReplacementProfile Validate(
        NativeLevelReplacementIntent intent,
        NativeLevelReplacementManifest manifest,
        LevelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(catalog);
        if (!string.Equals(intent.Format, IntentFormat, StringComparison.Ordinal) ||
            intent.Version != CurrentIntentVersion)
        {
            throw new InvalidDataException(
                $"Native level-replacement intent '{intent.Format}' version {intent.Version} is not supported.");
        }

        NativeLevelReplacementProfile profile =
            NativeLevelReplacementProfileRegistry.RequireRuntimeProven(
                intent.ProfileId,
                manifest,
                catalog);
        if (!string.Equals(intent.IntentId, BuildIntentId(profile), StringComparison.Ordinal) ||
            !string.Equals(intent.ProfileId, profile.Id, StringComparison.Ordinal) ||
            intent.ProfileRecipeVersion != profile.RecipeVersion ||
            !string.Equals(
                intent.TargetLevelKey,
                profile.NormalizedTargetLevelKey,
                StringComparison.Ordinal) ||
            !string.Equals(
                intent.DonorLevelKey,
                profile.NormalizedDonorLevelKey,
                StringComparison.Ordinal) ||
            !NativeLevelReplacementProfile.ShaEquals(
                intent.SourceImageSha256,
                profile.SourceImageSha256) ||
            !NativeLevelReplacementProfile.ShaEquals(
                intent.SourceImageSha256,
                manifest.Source.SourceImageSha256) ||
            intent.CreatedAtUtc == default)
        {
            throw new InvalidDataException(
                "The native level-replacement intent does not exactly match its checked profile and retail baseline.");
        }
        return profile;
    }

    private static async Task WriteValidatedAsync(
        string workspaceRoot,
        NativeLevelReplacementIntent intent,
        NativeLevelReplacementManifest manifest,
        LevelCatalog catalog,
        CancellationToken cancellationToken)
    {
        _ = Validate(intent, manifest, catalog);
        string path = GetPath(workspaceRoot, intent.TargetLevelKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough);
            await JsonSerializer.SerializeAsync(stream, intent, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(flushToDisk: true);
            stream.Close();
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string BuildIntentId(NativeLevelReplacementProfile profile) =>
        $"native-level-replacement-intent:{profile.NormalizedTargetLevelKey}<-{profile.NormalizedDonorLevelKey}";
}

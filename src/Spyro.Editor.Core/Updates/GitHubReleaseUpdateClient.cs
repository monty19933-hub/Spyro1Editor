using System.Net.Http.Headers;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace Spyro.Editor.Core.Updates;

public sealed record EditorUpdateAsset(
    string Name,
    Uri DownloadUri,
    long Size,
    string Sha256);

public sealed record EditorUpdateInfo(
    int BetaVersion,
    string ReleaseTag,
    string DisplayName,
    string ReleaseNotes,
    Uri ReleasePage,
    bool Prerelease,
    EditorUpdateAsset Asset,
    string PublicVersion = "");

public sealed class GitHubReleaseUpdateClient
{
    public const string DefaultOwner = "monty19933-hub";
    public const string DefaultRepository = "Spyro1Editor";
    public const long MaxUpdatePackageBytes = 512L * 1024 * 1024;
    public const long MaxApplicationAssemblyBytes = 128L * 1024 * 1024;
    public const int MaxArchiveEntries = 4096;
    public const long MaxArchiveUncompressedBytes = 1024L * 1024 * 1024;
    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repository;

    public GitHubReleaseUpdateClient(
        HttpClient httpClient,
        string owner = DefaultOwner,
        string repository = DefaultRepository)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _owner = RequireSlug(owner, nameof(owner));
        _repository = RequireSlug(repository, nameof(repository));
    }

    public Task<EditorUpdateInfo?> CheckAsync(
        int currentBetaVersion,
        CancellationToken cancellationToken = default) =>
        CheckAsync(new EditorBetaReleaseVersion(currentBetaVersion), cancellationToken);

    public async Task<EditorUpdateInfo?> CheckAsync(
        EditorBetaReleaseVersion currentVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"https://api.github.com/repos/{_owner}/{_repository}/releases?per_page=30");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd($"SpyroEditor/Beta-V{currentVersion.CanonicalVersion}");
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("GitHub returned an invalid releases response.");

        PlatformPackage platform = CurrentPlatformPackage();
        List<(EditorBetaReleaseVersion Version, EditorUpdateInfo Update)> candidates = [];
        foreach (JsonElement release in document.RootElement.EnumerateArray())
        {
            if (ReadBoolean(release, "draft"))
                continue;
            bool prerelease = ReadBoolean(release, "prerelease");
            if (!prerelease)
                continue;
            string displayName = ReadString(release, "name");
            if (!EditorBetaReleaseVersion.TryParseDisplayName(displayName, out EditorBetaReleaseVersion publicVersion) ||
                publicVersion.CompareTo(currentVersion) <= 0)
                continue;
            string tag = ReadString(release, "tag_name");
            if (!EditorBetaReleaseVersion.TryParseReleaseTag(tag, out EditorBetaReleaseVersion tagVersion) ||
                tagVersion != publicVersion)
                continue;
            string releaseNotes = ReadString(release, "body").Trim();
            if (string.IsNullOrWhiteSpace(releaseNotes) || releaseNotes.Length > 256 * 1024)
                continue;
            string expectedAssetName = ExpectedAssetName(publicVersion, platform);
            if (!release.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
                continue;
            JsonElement[] matchingAssets = assets
                .EnumerateArray()
                .Where(asset => string.Equals(ReadString(asset, "name"), expectedAssetName, StringComparison.Ordinal))
                .ToArray();
            if (matchingAssets.Length != 1)
                continue;
            JsonElement assetElement = matchingAssets[0];
            long assetSize = ReadInt64(assetElement, "size");
            if (assetSize <= 0 || assetSize > MaxUpdatePackageBytes)
                continue;
            string digest = ReadString(assetElement, "digest");
            if (!TryNormalizeSha256(digest, out string sha256))
                continue;
            string download = ReadString(assetElement, "browser_download_url");
            if (!TryCreateTrustedRepositoryUri(download, out Uri downloadUri))
                continue;
            string html = ReadString(release, "html_url");
            if (!TryCreateTrustedRepositoryUri(html, out Uri releasePage))
                continue;
            EditorUpdateAsset asset = new(
                expectedAssetName,
                downloadUri,
                assetSize,
                sha256);
            EditorUpdateInfo update = new(
                publicVersion.Major,
                publicVersion.ReleaseTag,
                publicVersion.DisplayName,
                releaseNotes,
                releasePage,
                prerelease,
                asset,
                publicVersion.CanonicalVersion);
            candidates.Add((publicVersion, update));
        }

        return candidates
            .OrderByDescending(candidate => candidate.Version)
            .Select(candidate => candidate.Update)
            .FirstOrDefault();
    }

    public async Task<string> DownloadVerifiedAsync(
        EditorUpdateInfo update,
        string destinationDirectory,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        EditorBetaReleaseVersion publicVersion = RequireUpdateVersion(update);
        if (!EditorBetaReleaseVersion.TryParseReleaseTag(update.ReleaseTag, out EditorBetaReleaseVersion tagVersion) ||
            tagVersion != publicVersion)
        {
            throw new InvalidDataException($"Update tag must be exactly '{publicVersion.ReleaseTag}'.");
        }
        if (!string.Equals(update.DisplayName, publicVersion.DisplayName, StringComparison.Ordinal))
            throw new InvalidDataException($"Update display name must be exactly '{publicVersion.DisplayName}'.");
        if (string.IsNullOrWhiteSpace(update.ReleaseNotes) || update.ReleaseNotes.Length > 256 * 1024)
            throw new InvalidDataException("Update must include a bounded, non-empty changelog.");
        PlatformPackage platform = CurrentPlatformPackage();
        string expectedAssetName = ExpectedAssetName(publicVersion, platform);
        if (!string.Equals(update.Asset.Name, expectedAssetName, StringComparison.Ordinal))
            throw new InvalidDataException($"Update asset must be named exactly '{expectedAssetName}'.");
        if (update.Asset.Size <= 0 || update.Asset.Size > MaxUpdatePackageBytes)
            throw new InvalidDataException($"Update package size must be between 1 byte and {MaxUpdatePackageBytes:N0} bytes.");
        if (!TryNormalizeSha256(update.Asset.Sha256, out string expectedSha256))
            throw new InvalidDataException("Update package does not have a valid SHA-256 digest.");
        if (!TryCreateTrustedRepositoryUri(update.Asset.DownloadUri.ToString(), out _))
            throw new InvalidDataException($"Update package URL must belong to {_owner}/{_repository} on GitHub Releases.");

        string destinationRoot = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(destinationRoot);
        string destinationPath = Path.Combine(destinationRoot, expectedAssetName);
        if (File.Exists(destinationPath) && new FileInfo(destinationPath).Length == update.Asset.Size &&
            string.Equals(await HashFileAsync(destinationPath, cancellationToken), expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            ValidatePackageShape(destinationPath, update, publicVersion, platform);
            progress?.Report(1);
            return destinationPath;
        }

        string temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.download";
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, update.Asset.DownloadUri);
            request.Headers.UserAgent.ParseAdd($"SpyroEditor/Beta-V{publicVersion.CanonicalVersion}");
            using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long contentLength &&
                (contentLength > update.Asset.Size || contentLength > MaxUpdatePackageBytes))
            {
                throw new InvalidDataException($"Update response declares {contentLength:N0} bytes, above the allowed {update.Asset.Size:N0} bytes.");
            }

            long total = 0;
            string actual;
            await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
            {
                await using FileStream output = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[128 * 1024];
                while (true)
                {
                    int read = await input.ReadAsync(buffer, cancellationToken);
                    if (read == 0)
                        break;
                    if (total > update.Asset.Size - read || total > MaxUpdatePackageBytes - read)
                        throw new InvalidDataException("Update response exceeded its declared or maximum allowed size.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    hash.AppendData(buffer, 0, read);
                    total += read;
                    progress?.Report(Math.Clamp((double)total / update.Asset.Size, 0, 1));
                }
                await output.FlushAsync(cancellationToken);
                actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }
            if (total != update.Asset.Size)
                throw new InvalidDataException($"Update download is {total:N0} bytes; GitHub reported {update.Asset.Size:N0} bytes.");
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The downloaded update failed its GitHub SHA-256 verification.");
            ValidatePackageShape(temporaryPath, update, publicVersion, platform);
            File.Move(temporaryPath, destinationPath, overwrite: true);
            progress?.Report(1);
            return destinationPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public static async Task<string> WriteChangelogAsync(
        EditorUpdateInfo update,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        EditorBetaReleaseVersion version = RequireUpdateVersion(update);
        if (!string.Equals(update.DisplayName, version.DisplayName, StringComparison.Ordinal))
            throw new InvalidDataException($"Update display name must be exactly '{version.DisplayName}'.");
        if (!EditorBetaReleaseVersion.TryParseReleaseTag(update.ReleaseTag, out EditorBetaReleaseVersion tagVersion) ||
            tagVersion != version)
        {
            throw new InvalidDataException($"Update tag must be exactly '{version.ReleaseTag}'.");
        }
        string notes = (update.ReleaseNotes ?? "").Trim();
        if (notes.Length == 0 || notes.Length > 256 * 1024)
            throw new InvalidDataException("Update must include a bounded, non-empty changelog.");
        if (!TryCreateTrustedGitHubUri(update.ReleasePage.ToString(), out _))
            throw new InvalidDataException("Update release page must use HTTPS on github.com.");

        string root = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, $"{version.PackageStem}-CHANGELOG.txt");
        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        string content = $"{version.DisplayName}\nRelease: {version.ReleaseTag}\n\nWhat's new\n\n{notes}\n\nFull release notes: {update.ReleasePage}\n";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, content, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
            return path;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public static bool TryNormalizeSha256(string? digest, out string sha256)
    {
        string value = (digest ?? "").Trim();
        if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            value = value[7..];
        if (value.Length == 64 && value.All(Uri.IsHexDigit))
        {
            sha256 = value.ToLowerInvariant();
            return true;
        }
        sha256 = "";
        return false;
    }

    public static EditorBetaReleaseVersion RequireUpdateVersion(EditorUpdateInfo update)
    {
        ArgumentNullException.ThrowIfNull(update);
        EditorBetaReleaseVersion version;
        if (string.IsNullOrEmpty(update.PublicVersion))
        {
            try
            {
                version = new EditorBetaReleaseVersion(update.BetaVersion);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new InvalidDataException("Update does not have a valid public beta version.", exception);
            }
        }
        else if (!EditorBetaReleaseVersion.TryParse(update.PublicVersion, out version))
        {
            throw new InvalidDataException($"Update public version '{update.PublicVersion}' is not canonical.");
        }

        if (!string.IsNullOrEmpty(update.PublicVersion) &&
            !string.Equals(update.PublicVersion, version.CanonicalVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Update public version '{update.PublicVersion}' is not canonical.");
        }

        if (update.BetaVersion != version.Major)
            throw new InvalidDataException("Update public version does not match its legacy public beta major version.");
        return version;
    }

    private static PlatformPackage CurrentPlatformPackage()
    {
        if (OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            return new("osx-arm64", "Spyro Editor.app/Contents/MacOS/Spyro.Editor.App.dll");
        if (OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64)
            return new("win-x64", "support/app/Spyro.Editor.App.dll");
        throw new PlatformNotSupportedException($"No Spyro Editor update package is published for {RuntimeInformation.OSDescription} {RuntimeInformation.ProcessArchitecture}.");
    }

    private static string ExpectedAssetName(EditorBetaReleaseVersion version, PlatformPackage platform) =>
        $"{version.PackageStem}-{platform.RuntimeIdentifier}.zip";

    private static bool TryCreateTrustedGitHubUri(string value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? candidate) &&
            candidate.Scheme == Uri.UriSchemeHttps &&
            string.Equals(candidate.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
            candidate.IsDefaultPort &&
            string.IsNullOrEmpty(candidate.UserInfo) &&
            string.IsNullOrEmpty(candidate.Fragment))
        {
            uri = candidate;
            return true;
        }
        uri = null!;
        return false;
    }

    private bool TryCreateTrustedRepositoryUri(string value, out Uri uri)
    {
        if (TryCreateTrustedGitHubUri(value, out Uri candidate) &&
            candidate.AbsolutePath.StartsWith(
                $"/{_owner}/{_repository}/releases/",
                StringComparison.OrdinalIgnoreCase))
        {
            uri = candidate;
            return true;
        }
        uri = null!;
        return false;
    }

    private static void ValidatePackageShape(
        string packagePath,
        EditorUpdateInfo update,
        EditorBetaReleaseVersion version,
        PlatformPackage platform)
    {
        string expectedRoot = $"{version.PackageStem}-{platform.RuntimeIdentifier}";
        string rootPrefix = expectedRoot + "/";
        string readmePath = rootPrefix + "README.txt";
        string manifestPath = rootPrefix + "release-manifest.json";
        string changelogPath = rootPrefix + "CHANGELOG.md";
        string appDllPath = rootPrefix + platform.AppDllRelativePath;
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(packagePath);
            if (archive.Entries.Count == 0)
                throw new InvalidDataException("Update ZIP is empty.");
            if (archive.Entries.Count > MaxArchiveEntries)
                throw new InvalidDataException($"Update ZIP contains more than {MaxArchiveEntries:N0} entries.");

            Dictionary<string, ZipArchiveEntry> entries = new(StringComparer.Ordinal);
            HashSet<string> seenEntries = new(StringComparer.OrdinalIgnoreCase);
            long totalUncompressedBytes = 0;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (IsLinkEntry(entry))
                    throw new InvalidDataException($"Update ZIP entry '{entry.FullName}' is a symbolic link or reparse point.");
                if (entry.Length > MaxArchiveUncompressedBytes - totalUncompressedBytes)
                {
                    throw new InvalidDataException(
                        $"Update ZIP expands beyond {MaxArchiveUncompressedBytes:N0} bytes.");
                }
                totalUncompressedBytes += entry.Length;
                string name = entry.FullName;
                string[] segments = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (name.Length == 0 || name.Contains('\\') || segments.Any(segment => segment is "." or ".."))
                    throw new InvalidDataException($"Update ZIP entry '{name}' contains an unsafe path segment.");
                bool isPrimaryEntry = string.Equals(name, rootPrefix, StringComparison.Ordinal) ||
                                      name.StartsWith(rootPrefix, StringComparison.Ordinal);
                bool isMacMetadata = string.Equals(name, "__MACOSX/", StringComparison.Ordinal) ||
                                     name.StartsWith("__MACOSX/" + rootPrefix, StringComparison.Ordinal);
                if (!isPrimaryEntry && !isMacMetadata)
                    throw new InvalidDataException($"Update ZIP entry '{name}' is outside the required '{expectedRoot}' root.");
                if (!seenEntries.Add(name))
                    throw new InvalidDataException($"Update ZIP contains duplicate entry '{name}'.");
                if (isMacMetadata)
                    continue;
                entries.Add(name, entry);
            }

            if (!entries.TryGetValue(readmePath, out ZipArchiveEntry? readme) || readme.Length <= 0 || readme.Length > 64 * 1024)
                throw new InvalidDataException("Update ZIP does not contain a sane root README.txt.");
            using (StreamReader reader = new(readme.Open(), detectEncodingFromByteOrderMarks: true))
            {
                string expectedFirstLine = version.DisplayName;
                string? firstLine = reader.ReadLine();
                if (!string.Equals(firstLine, expectedFirstLine, StringComparison.Ordinal))
                    throw new InvalidDataException($"Update README.txt must begin with '{expectedFirstLine}'.");
            }

            if (!entries.TryGetValue(manifestPath, out ZipArchiveEntry? manifestEntry))
                throw new InvalidDataException("Update ZIP is missing release-manifest.json.");
            string manifestJson = ReadBoundedText(manifestEntry, 64 * 1024, "release manifest");
            ReleasePackageManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<ReleasePackageManifest>(manifestJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? throw new JsonException("Release manifest is empty.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Update release-manifest.json is malformed.", exception);
            }
            bool validManifestIdentity = manifest.SchemaVersion switch
            {
                1 => !version.IsIncremental && string.IsNullOrWhiteSpace(manifest.PublicVersion),
                2 => version.IsIncremental &&
                    string.Equals(manifest.PublicVersion, version.CanonicalVersion, StringComparison.Ordinal),
                _ => false
            };
            if (!validManifestIdentity ||
                !string.Equals(manifest.Channel, "beta", StringComparison.Ordinal) ||
                manifest.PublicBeta != version.Major ||
                !string.Equals(manifest.DisplayName, version.DisplayName, StringComparison.Ordinal) ||
                !string.Equals(manifest.Platform, platform.RuntimeIdentifier, StringComparison.Ordinal) ||
                !EditorSemanticVersion.TryParse(manifest.InternalVersion, out _))
            {
                throw new InvalidDataException("Update release manifest does not match its public version, platform, or internal build identity.");
            }

            if (!entries.TryGetValue(changelogPath, out ZipArchiveEntry? changelogEntry))
                throw new InvalidDataException("Update ZIP is missing CHANGELOG.md.");
            string packagedChangelog = NormalizeNewlines(ReadBoundedText(changelogEntry, 256 * 1024, "changelog")).Trim();
            string releaseChangelog = NormalizeNewlines(update.ReleaseNotes).Trim();
            if (!string.Equals(packagedChangelog, releaseChangelog, StringComparison.Ordinal))
                throw new InvalidDataException("Packaged CHANGELOG.md does not match the GitHub Release changelog.");

            if (!entries.TryGetValue(appDllPath, out ZipArchiveEntry? appDll) || appDll.Length <= 0)
                throw new InvalidDataException($"Update ZIP is missing required application file '{appDllPath}'.");
            if (appDll.Length > MaxApplicationAssemblyBytes)
                throw new InvalidDataException($"Update application file '{appDllPath}' is unreasonably large.");
            using MemoryStream appDllImage = new(checked((int)appDll.Length));
            using (Stream appDllStream = appDll.Open())
            {
                byte[] buffer = new byte[128 * 1024];
                long bytesRead = 0;
                while (true)
                {
                    int read = appDllStream.Read(buffer, 0, buffer.Length);
                    if (read == 0)
                        break;
                    bytesRead += read;
                    if (bytesRead > appDll.Length || bytesRead > MaxApplicationAssemblyBytes)
                        throw new InvalidDataException($"Update application file '{appDllPath}' exceeds its declared size.");
                    appDllImage.Write(buffer, 0, read);
                }
                if (bytesRead != appDll.Length)
                    throw new InvalidDataException($"Update application file '{appDllPath}' is truncated.");
            }
            appDllImage.Position = 0;
            EditorAssemblyReleaseIdentity assemblyIdentity = EditorAssemblyReleaseIdentityReader.Read(
                appDllImage,
                $"update application file '{appDllPath}'");
            if (!string.Equals(assemblyIdentity.AssemblyName, EditorAssemblyReleaseIdentityReader.ExpectedAssemblyName, StringComparison.Ordinal) ||
                assemblyIdentity.PublicBetaVersion != manifest.PublicBeta ||
                assemblyIdentity.PublicVersion != version ||
                !assemblyIdentity.HasExplicitPublicVersion ||
                !assemblyIdentity.HasExplicitReleaseManifestSchema ||
                assemblyIdentity.ReleaseManifestSchema != manifest.SchemaVersion ||
                !string.Equals(assemblyIdentity.InternalVersion, manifest.InternalVersion, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Update application assembly metadata does not match the release manifest's public version and internal build identity.");
            }
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new InvalidDataException("Update package is not a readable ZIP archive.", exception);
        }
    }

    private static bool IsLinkEntry(ZipArchiveEntry entry)
    {
        uint attributes = unchecked((uint)entry.ExternalAttributes);
        uint unixFileType = (attributes >> 16) & 0xF000u;
        bool unixSymbolicLink = unixFileType == 0xA000u;
        bool windowsReparsePoint = (attributes & (uint)FileAttributes.ReparsePoint) != 0;
        return unixSymbolicLink || windowsReparsePoint;
    }

    private static string ReadBoundedText(ZipArchiveEntry entry, long maximumBytes, string description)
    {
        if (entry.Length <= 0 || entry.Length > maximumBytes)
            throw new InvalidDataException($"Update {description} has an invalid size.");
        using Stream input = entry.Open();
        using StreamReader reader = new(input, detectEncodingFromByteOrderMarks: true);
        string value = reader.ReadToEnd();
        if (value.Length == 0 || value.Length > maximumBytes)
            throw new InvalidDataException($"Update {description} is empty or too large.");
        return value;
    }

    private static string NormalizeNewlines(string value) =>
        (value ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private sealed record PlatformPackage(string RuntimeIdentifier, string AppDllRelativePath);
    private sealed record ReleasePackageManifest(
        int SchemaVersion,
        string Channel,
        int PublicBeta,
        string DisplayName,
        string InternalVersion,
        string Platform,
        string? PublicVersion = null);

    private static string RequireSlug(string value, string parameter)
    {
        string slug = (value ?? "").Trim();
        if (slug.Length == 0 || slug.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
            throw new ArgumentException("GitHub owner and repository names may contain only letters, numbers, dash, underscore, and period.", parameter);
        return slug;
    }

    private static string ReadString(JsonElement element, string property, string fallback = "") =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private static bool ReadBoolean(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind is JsonValueKind.True;

    private static long ReadInt64(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.TryGetInt64(out long result) ? result : 0;

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken)).ToLowerInvariant();
    }
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Cache;

public sealed record PortableLevelEntryPose(
    string LevelKey,
    string DisplayName,
    int LevelId,
    int SourceWadEntry,
    long WadOffset,
    int RawX,
    int RawY,
    int RawZ,
    int YawByte,
    int EntryDataByteLength)
{
    public double X => RawX / 16.0;
    public double Y => RawY / 16.0;
    public double Z => RawZ / 16.0;

    public FlyInLandingData ToFlyInLandingData() =>
        new(WadOffset, RawX, RawY, RawZ, YawByte, EntryDataByteLength);
}

public sealed class PortableLevelEntryPoseCacheSnapshot
{
    private readonly IReadOnlyDictionary<string, PortableLevelEntryPose> _posesByKey;

    internal PortableLevelEntryPoseCacheSnapshot(
        string sourceImageFileName,
        long sourceImageByteLength,
        string sourceImageSha256,
        IReadOnlyList<PortableLevelEntryPose> poses)
    {
        SourceImageFileName = sourceImageFileName;
        SourceImageByteLength = sourceImageByteLength;
        SourceImageSha256 = sourceImageSha256;
        Poses = poses.ToArray();
        _posesByKey = Poses.ToDictionary(
            pose => LevelCatalog.NormalizeKey(pose.LevelKey),
            StringComparer.OrdinalIgnoreCase);
    }

    public string Contract => PortableLevelEntryPoseCache.Contract;
    public int FormatVersion => PortableLevelEntryPoseCache.FormatVersion;
    public string SourceImageFileName { get; }
    public long SourceImageByteLength { get; }
    public string SourceImageSha256 { get; }
    public IReadOnlyList<PortableLevelEntryPose> Poses { get; }

    public bool TryGetPose(string levelKey, out PortableLevelEntryPose pose) =>
        _posesByKey.TryGetValue(LevelCatalog.NormalizeKey(levelKey), out pose!);
}

public sealed record PortableLevelEntryPoseCacheBuildResult(
    string Path,
    int LevelCount,
    bool ReusedExistingCache);

/// <summary>
/// Portable, source-proven entry poses for every retail level. These records
/// are independent of the destination-only fly-in editor marker: homeworlds
/// need the same retail XYZ/yaw data for a deterministic Game Camera start.
/// </summary>
public static class PortableLevelEntryPoseCache
{
    public const string FileName = "level-entry-poses.json";
    public const string Contract = "spyro1-us-retail-level-entry-poses-v1";
    public const int FormatVersion = 1;
    public const int ExpectedRetailLevelCount = 35;

    private const int MinimumEntryDataByteLength = 0x10;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static async Task<PortableLevelEntryPoseCacheBuildResult> BuildAsync(
        string sourceImagePath,
        string cacheDirectory,
        LevelCatalog catalog,
        bool overwrite = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        ArgumentNullException.ThrowIfNull(catalog);
        RequireRetailCatalog(catalog);

        Directory.CreateDirectory(cacheDirectory);
        string outputPath = Path.Combine(cacheDirectory, FileName);
        bool hasCompleteExistingCache = TryLoadCompleteFromCacheDirectory(
            cacheDirectory,
            catalog,
            out PortableLevelEntryPoseCacheSnapshot existing,
            out _);
        if (!overwrite && hasCompleteExistingCache)
        {
            return new PortableLevelEntryPoseCacheBuildResult(outputPath, existing.Poses.Count, ReusedExistingCache: true);
        }

        if (!File.Exists(sourceImagePath))
        {
            return new PortableLevelEntryPoseCacheBuildResult(
                outputPath,
                hasCompleteExistingCache ? existing.Poses.Count : 0,
                ReusedExistingCache: hasCompleteExistingCache);
        }

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        List<PortableLevelEntryPose> poses = new(catalog.Levels.Count);
        await using FileStream source = File.OpenRead(sourceImagePath);
        foreach (LevelDefinition level in catalog.Levels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FlyInLandingData landing = FlyInLandingLocator.Locate(source, layout, level);
            poses.Add(new PortableLevelEntryPose(
                level.Key,
                level.DisplayName,
                level.LevelId,
                level.SourceWadEntry,
                landing.WadOffset,
                landing.RawX,
                landing.RawY,
                landing.RawZ,
                landing.YawByte,
                landing.EntryDataByteLength));
        }

        source.Position = 0;
        string sourceImageSha256 = Convert.ToHexString(
            await SHA256.HashDataAsync(source, cancellationToken)).ToLowerInvariant();
        FileInfo sourceInfo = new(sourceImagePath);
        string posePayloadSha256 = ComputePosePayloadSha256(poses);
        CacheDocument document = new()
        {
            GeneratedBy = "Spyro.Editor.Core",
            GeneratedAt = DateTimeOffset.UtcNow.ToString("O"),
            Purpose = "Portable source-proven retail entry XYZ/yaw for deterministic editor Game Camera starts without the original disc at runtime.",
            Contract = Contract,
            FormatVersion = FormatVersion,
            LevelCount = poses.Count,
            PosePayloadSha256 = posePayloadSha256,
            Source = new SourceProvenanceDocument
            {
                ImageFileName = sourceInfo.Name,
                ImageByteLength = sourceInfo.Length,
                ImageSha256 = sourceImageSha256,
                DiscSectorSize = layout.SectorSize,
                DiscUserOffset = layout.UserOffset,
                IsoRootExtent = layout.RootExtent,
                IsoRootLength = layout.RootLength,
                WadLba = FlyInLandingLocator.SourceWadLba
            },
            Levels = poses.Select(pose => new PoseDocument
            {
                LevelKey = pose.LevelKey,
                DisplayName = pose.DisplayName,
                LevelId = pose.LevelId,
                SourceWadEntry = pose.SourceWadEntry,
                WadOffset = pose.WadOffset,
                WadOffsetHex = $"0x{pose.WadOffset:X}",
                RawX = pose.RawX,
                RawY = pose.RawY,
                RawZ = pose.RawZ,
                YawByte = pose.YawByte,
                EntryDataByteLength = pose.EntryDataByteLength
            }).ToList()
        };

        string temporaryPath = outputPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (FileStream output = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    output,
                    document,
                    JsonOptions,
                    cancellationToken);
            }
            File.Move(temporaryPath, outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        if (!TryLoadCompleteFromCacheDirectory(
                cacheDirectory,
                catalog,
                out PortableLevelEntryPoseCacheSnapshot readback,
                out string readbackError))
        {
            throw new InvalidDataException($"Level entry-pose cache failed readback validation: {readbackError}");
        }

        return new PortableLevelEntryPoseCacheBuildResult(outputPath, readback.Poses.Count, ReusedExistingCache: false);
    }

    public static bool TryLoadComplete(
        string workspaceRoot,
        LevelCatalog catalog,
        out PortableLevelEntryPoseCacheSnapshot snapshot,
        out string error)
    {
        string cacheDirectory = Path.Combine(workspaceRoot, "editor-cache");
        return TryLoadCompleteFromCacheDirectory(cacheDirectory, catalog, out snapshot, out error);
    }

    public static bool TryLoadCompleteFromCacheDirectory(
        string cacheDirectory,
        LevelCatalog catalog,
        out PortableLevelEntryPoseCacheSnapshot snapshot,
        out string error)
    {
        snapshot = null!;
        error = "";
        try
        {
            RequireRetailCatalog(catalog);
            string path = Path.Combine(cacheDirectory, FileName);
            if (!File.Exists(path))
            {
                error = $"Missing {FileName}.";
                return false;
            }

            using FileStream input = File.OpenRead(path);
            CacheDocument? document = JsonSerializer.Deserialize<CacheDocument>(input, JsonOptions);
            if (document == null)
            {
                error = "The level entry-pose cache is empty.";
                return false;
            }

            if (!string.Equals(document.Contract, Contract, StringComparison.Ordinal) ||
                document.FormatVersion != FormatVersion)
            {
                error = $"Unsupported level entry-pose contract/version '{document.Contract}'/{document.FormatVersion}.";
                return false;
            }

            if (document.Source == null ||
                string.IsNullOrWhiteSpace(document.Source.ImageFileName) ||
                document.Source.ImageByteLength <= 0 ||
                !IsSha256(document.Source.ImageSha256) ||
                document.Source.DiscSectorSize is not (2048 or 2336 or 2352) ||
                document.Source.DiscUserOffset < 0 ||
                document.Source.IsoRootExtent <= 0 ||
                document.Source.IsoRootLength <= 0 ||
                document.Source.WadLba != FlyInLandingLocator.SourceWadLba)
            {
                error = "The level entry-pose cache has incomplete source-disc provenance.";
                return false;
            }

            if (document.LevelCount != ExpectedRetailLevelCount ||
                document.Levels == null ||
                document.Levels.Count != ExpectedRetailLevelCount)
            {
                error = $"The level entry-pose cache contains {document.Levels?.Count ?? 0}/{ExpectedRetailLevelCount} levels.";
                return false;
            }

            Dictionary<string, PoseDocument> rowsByKey = new(StringComparer.OrdinalIgnoreCase);
            foreach (PoseDocument row in document.Levels)
            {
                string normalizedKey = LevelCatalog.NormalizeKey(row.LevelKey);
                if (string.IsNullOrWhiteSpace(normalizedKey) || !rowsByKey.TryAdd(normalizedKey, row))
                {
                    error = $"The level entry-pose cache contains a blank or duplicate level key '{row.LevelKey}'.";
                    return false;
                }
            }

            List<PortableLevelEntryPose> poses = new(catalog.Levels.Count);
            foreach (LevelDefinition level in catalog.Levels)
            {
                string normalizedKey = LevelCatalog.NormalizeKey(level.Key);
                if (!rowsByKey.TryGetValue(normalizedKey, out PoseDocument? row))
                {
                    error = $"The level entry-pose cache is missing {level.DisplayName}.";
                    return false;
                }

                if (!string.Equals(row.LevelKey, level.Key, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(row.DisplayName, level.DisplayName, StringComparison.Ordinal) ||
                    row.LevelId != level.LevelId ||
                    row.SourceWadEntry != level.SourceWadEntry)
                {
                    error = $"The cached identity/provenance for {level.DisplayName} does not match the active level catalog.";
                    return false;
                }

                if (row.WadOffset <= 0 ||
                    !string.Equals(row.WadOffsetHex, $"0x{row.WadOffset:X}", StringComparison.Ordinal) ||
                    !IsPlausibleCoordinate(row.RawX) ||
                    !IsPlausibleCoordinate(row.RawY) ||
                    !IsPlausibleCoordinate(row.RawZ) ||
                    row.YawByte is < 0 or > 255 ||
                    row.EntryDataByteLength < MinimumEntryDataByteLength)
                {
                    error = $"The cached source entry for {level.DisplayName} is structurally invalid.";
                    return false;
                }

                poses.Add(new PortableLevelEntryPose(
                    row.LevelKey,
                    row.DisplayName,
                    row.LevelId,
                    row.SourceWadEntry,
                    row.WadOffset,
                    row.RawX,
                    row.RawY,
                    row.RawZ,
                    row.YawByte,
                    row.EntryDataByteLength));
            }

            if (rowsByKey.Count != poses.Count ||
                !IsSha256(document.PosePayloadSha256) ||
                !string.Equals(
                    document.PosePayloadSha256,
                    ComputePosePayloadSha256(poses),
                    StringComparison.OrdinalIgnoreCase))
            {
                error = "The level entry-pose payload is incomplete or its integrity hash does not match.";
                return false;
            }

            snapshot = new PortableLevelEntryPoseCacheSnapshot(
                document.Source.ImageFileName,
                document.Source.ImageByteLength,
                document.Source.ImageSha256,
                poses);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryLoadPose(
        string workspaceRoot,
        LevelCatalog catalog,
        string levelKey,
        out PortableLevelEntryPose pose,
        out string error)
    {
        pose = null!;
        if (!TryLoadComplete(workspaceRoot, catalog, out PortableLevelEntryPoseCacheSnapshot snapshot, out error))
            return false;
        if (snapshot.TryGetPose(levelKey, out pose))
            return true;

        error = $"The complete level entry-pose cache has no key '{levelKey}'.";
        return false;
    }

    private static string ComputePosePayloadSha256(IReadOnlyList<PortableLevelEntryPose> poses)
    {
        StringBuilder canonical = new();
        foreach (PortableLevelEntryPose pose in poses)
        {
            canonical
                .Append(pose.LevelKey).Append('\0')
                .Append(pose.DisplayName).Append('\0')
                .Append(pose.LevelId.ToString(CultureInfo.InvariantCulture)).Append('\0')
                .Append(pose.SourceWadEntry.ToString(CultureInfo.InvariantCulture)).Append('\0')
                .Append(pose.WadOffset.ToString(CultureInfo.InvariantCulture)).Append('\0')
                .Append(pose.RawX.ToString(CultureInfo.InvariantCulture)).Append('\0')
                .Append(pose.RawY.ToString(CultureInfo.InvariantCulture)).Append('\0')
                .Append(pose.RawZ.ToString(CultureInfo.InvariantCulture)).Append('\0')
                .Append(pose.YawByte.ToString(CultureInfo.InvariantCulture)).Append('\0')
                .Append(pose.EntryDataByteLength.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private static void RequireRetailCatalog(LevelCatalog catalog)
    {
        if (catalog.Levels.Count != ExpectedRetailLevelCount)
        {
            throw new InvalidDataException(
                $"The {Contract} contract requires exactly {ExpectedRetailLevelCount} levels; the active catalog has {catalog.Levels.Count}.");
        }
    }

    private static bool IsPlausibleCoordinate(int value) => value is > -1_000_000 and < 1_000_000;

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character => Uri.IsHexDigit(character));

    private sealed class CacheDocument
    {
        public string GeneratedBy { get; init; } = "";
        public string GeneratedAt { get; init; } = "";
        public string Purpose { get; init; } = "";
        public string Contract { get; init; } = "";
        public int FormatVersion { get; init; }
        public int LevelCount { get; init; }
        public string PosePayloadSha256 { get; init; } = "";
        public SourceProvenanceDocument? Source { get; init; }
        public List<PoseDocument>? Levels { get; init; }
    }

    private sealed class SourceProvenanceDocument
    {
        public string ImageFileName { get; init; } = "";
        public long ImageByteLength { get; init; }
        public string ImageSha256 { get; init; } = "";
        public int DiscSectorSize { get; init; }
        public int DiscUserOffset { get; init; }
        public int IsoRootExtent { get; init; }
        public int IsoRootLength { get; init; }
        public int WadLba { get; init; }
    }

    private sealed class PoseDocument
    {
        public string LevelKey { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public int LevelId { get; init; }
        public int SourceWadEntry { get; init; }
        public long WadOffset { get; init; }
        public string WadOffsetHex { get; init; } = "";
        public int RawX { get; init; }
        public int RawY { get; init; }
        public int RawZ { get; init; }
        public int YawByte { get; init; }
        public int EntryDataByteLength { get; init; }
    }
}

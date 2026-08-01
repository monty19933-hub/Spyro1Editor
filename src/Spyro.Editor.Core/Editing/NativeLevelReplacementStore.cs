using System.Text.Json;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Editing;

public enum NativeLevelReplacementEvidenceStatus
{
    StaticBaselineOnly = 0,
    RuntimeProven = 1
}

public sealed record NativeLevelSlotContract(
    string TargetLevelKey,
    string ScriptKey,
    int LevelId,
    int MetadataWadDirectoryIndex,
    int MetadataAdjacentWadDirectoryIndex,
    int LoadedDataPredecessorWadDirectoryIndex,
    int LevelDataWadDirectoryIndex,
    int LevelDataSubfileIndex,
    int SourceMobyTableSubfileIndex,
    long SourceMobyTableWadOffset,
    long SourceMobyTableRelativeOffset,
    int SourceMobyRecordCount,
    int SourceMobyRecordStride);

public sealed record NativeDiscFilePreimage(
    string Name,
    int Lba,
    int ByteLength,
    string Sha256);

public sealed record NativeWadEntryPreimage(
    int DirectoryIndex,
    long WadOffset,
    int ByteLength,
    uint FirstWord,
    uint SecondWord,
    string Sha256);

public sealed record NativeNestedSubfilePreimage(
    int SubfileIndex,
    int RelativeOffset,
    int ByteLength,
    string Sha256);

public sealed record NativeMobyRowPreimage(
    string OwnerLevelKey,
    int TrueIndex,
    long WadOffset,
    string Sha256);

public sealed record NativeLevelReplacementSourceBinding(
    long SourceImageBytes,
    string SourceImageSha256,
    int DiscSectorSize,
    int DiscUserOffset,
    NativeDiscFilePreimage Executable,
    NativeDiscFilePreimage Wad,
    int WadArchiveHeaderByteLength,
    string WadArchiveHeaderSha256,
    NativeWadEntryPreimage LevelMetadataEntry,
    NativeWadEntryPreimage MetadataAdjacentEntry,
    NativeWadEntryPreimage LoadedDataPredecessorEntry,
    NativeWadEntryPreimage LevelDataEntry,
    int NestedHeaderByteLength,
    string NestedHeaderSha256,
    int NestedDescriptorTableByteLength,
    string NestedDescriptorTableSha256,
    IReadOnlyList<NativeNestedSubfilePreimage> LevelDataSubfiles,
    NativeNestedSubfilePreimage LevelDataSubfile,
    NativeNestedSubfilePreimage SourceMobyTableSubfile,
    int SourceMobyTableByteLength,
    string SourceMobyTableSha256,
    IReadOnlyList<NativeMobyRowPreimage> ArtisansPortalControlRows,
    IReadOnlyList<NativeMobyRowPreimage> StoneHillReturnHomeRows);

public sealed record NativeLevelReplacementManifest(
    string Format,
    int Version,
    string ReplacementId,
    NativeLevelSlotContract Slot,
    NativeLevelReplacementSourceBinding Source,
    NativeLevelReplacementEvidenceStatus EvidenceStatus,
    DateTimeOffset CreatedAtUtc);

public static class NativeLevelReplacementSupportCatalog
{
    public const string ManifestFormat = "spyro-editor-native-level-replacement";
    public const string FirstTargetLevelKey = "stonehill";
    public const int CurrentManifestVersion = 2;
    public const string SupportedUsaRetailSha256 =
        "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    public const long SupportedUsaRetailBytes = 661_547_040;

    private static readonly NativeLevelSlotContract StoneHill = new(
        TargetLevelKey: FirstTargetLevelKey,
        ScriptKey: "StoneHill",
        LevelId: 11,
        MetadataWadDirectoryIndex: 9,
        MetadataAdjacentWadDirectoryIndex: 10,
        LoadedDataPredecessorWadDirectoryIndex: 11,
        LevelDataWadDirectoryIndex: 12,
        LevelDataSubfileIndex: 1,
        SourceMobyTableSubfileIndex: 3,
        SourceMobyTableWadOffset: 0xD72B38,
        SourceMobyTableRelativeOffset: 0x1DF338,
        SourceMobyRecordCount: 195,
        SourceMobyRecordStride: MobyLoader.RuntimeRecordStride);

    public static NativeLevelSlotContract RequireSupported(LevelDefinition level)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (!string.Equals(LevelCatalog.NormalizeKey(level.Key), FirstTargetLevelKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "V5 level replacement currently supports only the existing Stone Hill retail slot. " +
                "A new 36th slot is intentionally outside this first runtime experiment.");
        }

        if (!string.Equals(level.ScriptKey, StoneHill.ScriptKey, StringComparison.Ordinal) ||
            level.LevelId != StoneHill.LevelId ||
            level.SourceWadEntry != StoneHill.LevelDataWadDirectoryIndex ||
            level.SourceRecordCount != StoneHill.SourceMobyRecordCount ||
            !TryParseOffset(level.SourceTableWadOffset, out long tableWadOffset) ||
            !TryParseOffset(level.SourceTableRelativeOffset, out long tableRelativeOffset) ||
            tableWadOffset != StoneHill.SourceMobyTableWadOffset ||
            tableRelativeOffset != StoneHill.SourceMobyTableRelativeOffset)
        {
            throw new InvalidDataException(
                "The Stone Hill catalog contract changed. Replacement research is blocked instead of " +
                "silently targeting another portal, save slot, overlay, Moby table, or WAD entry.");
        }

        return StoneHill;
    }

    public static NativeLevelSlotContract RequireSupported(LevelCatalog catalog, string levelKey)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        LevelDefinition level = catalog.FindByKey(levelKey)
            ?? throw new InvalidOperationException($"Level '{levelKey}' is not present in the retail catalog.");
        return RequireSupported(level);
    }

    public static void RequireSupportedSource(NativeLevelReplacementSourceBinding source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.SourceImageBytes != SupportedUsaRetailBytes ||
            !string.Equals(source.SourceImageSha256, SupportedUsaRetailSha256, StringComparison.OrdinalIgnoreCase) ||
            source.DiscSectorSize != 2352 || source.DiscUserOffset != 24 || source.Wad.Lba != 37)
        {
            throw new InvalidDataException(
                "The Stone Hill replacement baseline requires the supported clean USA retail BIN preimage. " +
                "Modified discs and other regional layouts remain blocked.");
        }
    }

    public static HomeworldPortalControlDefinition RequireStoneHillPortal()
    {
        HomeworldPortalControlDefinition[] matches = HomeworldPortalControlCatalog.All
            .Where(definition =>
                string.Equals(definition.HomeworldKey, "artisans", StringComparison.Ordinal) &&
                definition.DestinationLevelId == StoneHill.LevelId)
            .ToArray();
        if (matches.Length != 1 || matches[0].PathTrueIndex != 38 ||
            matches[0].LetteringTrueIndex != 144 || matches[0].CompanionTrueIndex != 157)
        {
            throw new InvalidDataException(
                "The Artisans-to-Stone-Hill portal identity is missing, duplicated, or changed from T38/T144/T157.");
        }
        return matches[0];
    }

    internal static bool TryParseOffset(string value, out long result)
    {
        string text = (value ?? "").Trim();
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out result)
            : long.TryParse(text, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out result);
    }
}

public static class NativeLevelReplacementStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string GetPath(string workspaceRoot, string targetLevelKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        string key = LevelCatalog.NormalizeKey(targetLevelKey);
        if (!string.Equals(key, NativeLevelReplacementSupportCatalog.FirstTargetLevelKey, StringComparison.Ordinal) ||
            key.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new InvalidOperationException("Only the canonical Stone Hill replacement manifest path is supported.");
        }

        string root = Path.GetFullPath(workspaceRoot);
        string path = Path.Combine(root, $"{key}-native-level-replacement.json");
        PersistencePathSafety.EnsureWritePathWithinRoot(root, path);
        return path;
    }

    public static async Task<NativeLevelReplacementManifest> StartStoneHillAsync(
        string workspaceRoot,
        string sourceImagePath,
        LevelCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        NativeLevelSlotContract slot = NativeLevelReplacementSupportCatalog.RequireSupported(
            catalog,
            NativeLevelReplacementSupportCatalog.FirstTargetLevelKey);
        NativeLevelReplacementSourceBinding source = await NativeLevelSlotLocator.InspectAsync(
            sourceImagePath,
            slot,
            catalog,
            cancellationToken);
        NativeLevelReplacementSupportCatalog.RequireSupportedSource(source);
        NativeLevelReplacementManifest manifest = new(
            Format: NativeLevelReplacementSupportCatalog.ManifestFormat,
            Version: NativeLevelReplacementSupportCatalog.CurrentManifestVersion,
            ReplacementId: $"native-level-replacement:{slot.TargetLevelKey}",
            Slot: slot,
            Source: source,
            EvidenceStatus: NativeLevelReplacementEvidenceStatus.StaticBaselineOnly,
            CreatedAtUtc: DateTimeOffset.UtcNow);
        await WriteValidatedAsync(workspaceRoot, manifest, catalog, cancellationToken);
        return manifest;
    }

    public static NativeLevelReplacementManifest? Load(
        string workspaceRoot,
        string targetLevelKey,
        LevelCatalog catalog)
    {
        string path = GetPath(workspaceRoot, targetLevelKey);
        if (!File.Exists(path))
            return null;

        try
        {
            NativeLevelReplacementManifest manifest =
                JsonSerializer.Deserialize<NativeLevelReplacementManifest>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidDataException("The native level-replacement manifest is empty.");
            Validate(manifest, catalog);
            if (!string.Equals(
                    LevelCatalog.NormalizeKey(targetLevelKey),
                    LevelCatalog.NormalizeKey(manifest.Slot.TargetLevelKey),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("The replacement manifest filename and target level do not agree.");
            }
            return manifest;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or
            InvalidOperationException or InvalidDataException or NullReferenceException)
        {
            throw new InvalidDataException($"The native level-replacement manifest cannot be read: {path}", exception);
        }
    }

    public static async Task SaveAsync(
        string workspaceRoot,
        NativeLevelReplacementManifest manifest,
        LevelCatalog catalog,
        string sourceImagePath,
        CancellationToken cancellationToken = default)
    {
        await ValidateSourceAsync(manifest, sourceImagePath, catalog, cancellationToken);
        await WriteValidatedAsync(workspaceRoot, manifest, catalog, cancellationToken);
    }

    private static async Task WriteValidatedAsync(
        string workspaceRoot,
        NativeLevelReplacementManifest manifest,
        LevelCatalog catalog,
        CancellationToken cancellationToken)
    {
        Validate(manifest, catalog);
        string path = GetPath(workspaceRoot, manifest.Slot.TargetLevelKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Close();
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public static void Validate(NativeLevelReplacementManifest manifest, LevelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(catalog);
        if (manifest.Slot is null || manifest.Source is null)
            throw new InvalidDataException("The native level-replacement manifest is incomplete.");
        if (!string.Equals(manifest.Format, NativeLevelReplacementSupportCatalog.ManifestFormat, StringComparison.Ordinal) ||
            manifest.Version != NativeLevelReplacementSupportCatalog.CurrentManifestVersion)
        {
            throw new InvalidDataException(
                $"Native level-replacement manifest '{manifest.Format}' version {manifest.Version} is not supported.");
        }
        if (!string.Equals(
                manifest.ReplacementId,
                $"native-level-replacement:{LevelCatalog.NormalizeKey(manifest.Slot.TargetLevelKey)}",
                StringComparison.Ordinal))
            throw new InvalidDataException("The native level-replacement identity is invalid.");
        if (manifest.EvidenceStatus != NativeLevelReplacementEvidenceStatus.StaticBaselineOnly)
            throw new InvalidDataException("No Stone Hill replacement compiler has runtime proof yet.");

        NativeLevelSlotContract expected = NativeLevelReplacementSupportCatalog.RequireSupported(
            catalog,
            manifest.Slot.TargetLevelKey);
        if (manifest.Slot != expected)
            throw new InvalidDataException("The saved Stone Hill slot contract no longer matches the checked catalog.");
        ValidateSourceShape(manifest.Source, expected);
        NativeLevelReplacementSupportCatalog.RequireSupportedSource(manifest.Source);
        _ = NativeLevelReplacementSafetyInspector.Inspect(manifest, catalog);
    }

    public static async Task<NativeLevelReplacementSourceBinding> ValidateSourceAsync(
        NativeLevelReplacementManifest manifest,
        string sourceImagePath,
        LevelCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        Validate(manifest, catalog);
        NativeLevelReplacementSourceBinding inspected = await NativeLevelSlotLocator.InspectAsync(
            sourceImagePath,
            manifest.Slot,
            catalog,
            cancellationToken);
        NativeLevelReplacementSupportCatalog.RequireSupportedSource(inspected);
        if (!BindingsEqual(manifest.Source, inspected))
            throw new InvalidDataException("The selected source disc no longer matches every recorded Stone Hill replacement preimage.");
        return inspected;
    }

    private static void ValidateSourceShape(
        NativeLevelReplacementSourceBinding source,
        NativeLevelSlotContract slot)
    {
        if (source.Executable is null || source.Wad is null || source.LevelMetadataEntry is null ||
            source.MetadataAdjacentEntry is null || source.LoadedDataPredecessorEntry is null ||
            source.LevelDataEntry is null || source.LevelDataSubfiles is null ||
            source.LevelDataSubfile is null ||
            source.SourceMobyTableSubfile is null || source.ArtisansPortalControlRows is null ||
            source.StoneHillReturnHomeRows is null)
            throw new InvalidDataException("The native level-replacement source binding is incomplete.");
        if (source.SourceImageBytes <= 0 || source.DiscSectorSize <= 0 || source.DiscUserOffset < 0 ||
            source.WadArchiveHeaderByteLength <= 0 || source.NestedHeaderByteLength <= 0 ||
            source.NestedDescriptorTableByteLength <= 0 || source.SourceMobyTableByteLength !=
            checked(slot.SourceMobyRecordCount * slot.SourceMobyRecordStride))
            throw new InvalidDataException("The native level-replacement source dimensions are invalid.");

        if (source.LevelDataSubfiles.Count != 8 ||
            !source.LevelDataSubfiles.Select(subfile => subfile.SubfileIndex)
                .SequenceEqual(Enumerable.Range(0, 8)) ||
            source.LevelDataSubfiles.Any(subfile =>
                subfile.ByteLength <= 0 || subfile.RelativeOffset < source.NestedHeaderByteLength ||
                !IsSha256(subfile.Sha256)) ||
            source.LevelDataSubfiles[slot.LevelDataSubfileIndex] != source.LevelDataSubfile ||
            source.LevelDataSubfiles[slot.SourceMobyTableSubfileIndex] != source.SourceMobyTableSubfile)
        {
            throw new InvalidDataException(
                "The native level-replacement source does not bind all eight packed Stone Hill subfiles.");
        }

        string[] hashes =
        [
            source.SourceImageSha256,
            source.Executable.Sha256,
            source.Wad.Sha256,
            source.WadArchiveHeaderSha256,
            source.LevelMetadataEntry.Sha256,
            source.MetadataAdjacentEntry.Sha256,
            source.LoadedDataPredecessorEntry.Sha256,
            source.LevelDataEntry.Sha256,
            source.NestedHeaderSha256,
            source.NestedDescriptorTableSha256,
            source.LevelDataSubfile.Sha256,
            source.SourceMobyTableSubfile.Sha256,
            source.SourceMobyTableSha256
        ];
        if (hashes.Any(hash => !IsSha256(hash)) ||
            source.ArtisansPortalControlRows.Any(row => row is null || !IsSha256(row.Sha256)) ||
            source.StoneHillReturnHomeRows.Any(row => row is null || !IsSha256(row.Sha256)))
            throw new InvalidDataException("The native level-replacement source preimages are incomplete.");
    }

    private static bool BindingsEqual(
        NativeLevelReplacementSourceBinding expected,
        NativeLevelReplacementSourceBinding actual) =>
        expected with
        {
            LevelDataSubfiles = actual.LevelDataSubfiles,
            ArtisansPortalControlRows = actual.ArtisansPortalControlRows,
            StoneHillReturnHomeRows = actual.StoneHillReturnHomeRows
        } == actual &&
        expected.LevelDataSubfiles.SequenceEqual(actual.LevelDataSubfiles) &&
        expected.ArtisansPortalControlRows.SequenceEqual(actual.ArtisansPortalControlRows) &&
        expected.StoneHillReturnHomeRows.SequenceEqual(actual.StoneHillReturnHomeRows);

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character => char.IsAsciiHexDigit(character));
}

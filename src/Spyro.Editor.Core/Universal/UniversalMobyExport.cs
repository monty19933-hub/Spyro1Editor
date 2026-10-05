using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Universal;

/// <summary>Read-only access to the unchanged original disc's level data and overlays (the donors).</summary>
public sealed class UniversalSourceLibrary
{
    private readonly Dictionary<string, (LevelDefinition, UniversalLevelData, UniversalOverlay)> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly UniversalDiscImage _disc;
    private readonly LevelCatalog _catalog;

    public UniversalSourceLibrary(string originalImagePath, LevelCatalog catalog)
    {
        _disc = UniversalDiscImage.Read(originalImagePath);
        _catalog = catalog;
        SharedClasses = DecodeSharedClasses(_disc.EntryBytes(8));
        ImagePath = originalImagePath;
    }

    public string ImagePath { get; }

    public const string OriginalUsaSha256 = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";

    /// <summary>Donors must come from the unchanged original USA disc.</summary>
    public static void RequireOriginal(string path)
    {
        using var fs = File.OpenRead(path);
        if (Convert.ToHexStringLower(SHA256.HashData(fs)) != OriginalUsaSha256)
            throw new InvalidDataException("Objects from other levels are copied from the unchanged original Spyro the Dragon (USA) disc. Select that disc as the source image.");
    }
    public UniversalDiscImage Disc => _disc;
    public IReadOnlySet<int> SharedClasses { get; }
    private UniversalSharedModelAssets? _sharedAssets;
    public byte[] SharedModelPackage(int classId) => (_sharedAssets ??= new UniversalSharedModelAssets(_disc)).Package(classId);
    private NativePlayerAnimationSource? _playerAnimations;
    public NativePlayerAnimationSource PlayerAnimations() => _playerAnimations ??=
        NativePlayerAnimationSource.CaptureAsync(ImagePath).GetAwaiter().GetResult();

    public (LevelDefinition Level, UniversalLevelData Data, UniversalOverlay Overlay) Get(string key)
    {
        if (_cache.TryGetValue(key, out var hit)) return hit;
        var level = _catalog.FindByKey(key) ?? throw new InvalidDataException($"Unknown source level '{key}'.");
        var data = new UniversalLevelData(_disc.EntryBytes(level.SourceWadEntry));
        var overlay = new UniversalOverlay(_disc.EntryBytes(level.SourceWadEntry - 1), UniversalExeBindings.Read(_disc.Exe, level.LevelId));
        return _cache[key] = (level, data, overlay);
    }

    internal static IReadOnlySet<int> DecodeSharedClasses(ReadOnlySpan<byte> pete)
    {
        HashSet<int> set = [];
        for (int field = 4; field + 8 <= 0x800; field += 8)
        {
            uint root = BinaryPrimitives.ReadUInt32LittleEndian(pete[field..]);
            uint actor = BinaryPrimitives.ReadUInt32LittleEndian(pete[(field + 4)..]);
            if (root != 0 && actor < 0x200) set.Add((int)actor);
        }
        return set;
    }
}

public sealed record UniversalExportLevelResult(string LevelKey, string LevelName, UniversalComposeResult Compose);

public sealed record UniversalExportResult(string OutputImagePath, string OutputImageSha256,
    IReadOnlyList<UniversalExportLevelResult> Levels, int WadGrowthBytes)
{
    /// <summary>True only for an internal aggregate stage; it is not a completed export.
    /// The final writer must merge these manifests and install/verify shared hooks once.</summary>
    public bool SpecialHooksDeferred { get; init; }
    public IReadOnlyList<(int LevelIndex, int Row, int Colour)> ActiveDragonflyEggs { get; init; } = [];
    public IReadOnlySet<string> SignLevelKeys { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Image-to-image Create BIN stage: applies universal Moby placements for any number of destinations.</summary>
public static class UniversalMobyExport
{
    public const string RequiredExporterFeature = "universal-moby-import";

    /// <param name="removedTreasureAlreadyApplied">True only for a staged image whose ordinary
    /// source-row writer already applied all supplied native removal treasure deltas.</param>
    public static UniversalExportResult Apply(UniversalSourceLibrary sources, string currentImagePath, string outputImagePath,
        LevelCatalog catalog, IReadOnlyDictionary<string, IReadOnlyList<UniversalMobyPlacement>> placementsByLevel,
        Func<string, LevelDefinition, IEnumerable<(long Offset, int Length)>>? protectedVram = null,
        UniversalJapaneseSource? japanese = null,
        IReadOnlyDictionary<string, IReadOnlyCollection<int>>? removedRowsByLevel = null,
        string? identityWorkspace = null,
        bool deferSpecialHooks = false,
        bool removedTreasureAlreadyApplied = false)
    {
        if (string.Equals(Path.GetFullPath(sources.ImagePath), Path.GetFullPath(outputImagePath), StringComparison.Ordinal) ||
            string.Equals(Path.GetFullPath(currentImagePath), Path.GetFullPath(outputImagePath), StringComparison.Ordinal))
            throw new InvalidOperationException("Universal Moby export never overwrites its source image.");
        if (File.Exists(outputImagePath) || Directory.Exists(outputImagePath))
            throw new IOException("Choose a new output file; universal composition never overwrites an existing image.");
        Dictionary<string, IReadOnlyList<UniversalMobyPlacement>> allPlacements = new(placementsByLevel, StringComparer.OrdinalIgnoreCase);
        if (removedRowsByLevel != null)
            foreach (var (key, removed) in removedRowsByLevel) if (removed.Count > 0) allPlacements.TryAdd(key, []);
        var destinationKeys = allPlacements.Keys.Select(k => catalog.FindByKey(k)?.Key ?? k).ToArray();
        if (destinationKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != destinationKeys.Length)
            throw new InvalidDataException("A destination level was supplied more than once under different names.");
        var current = UniversalDiscImage.Read(currentImagePath);
        bool useNativeOwnershipScan = protectedVram == null;
        protectedVram ??= DefaultProtectedVram;
        byte[] exe = current.Exe.ToArray();
        Dictionary<int, byte[]> replacements = [];
        List<UniversalExportLevelResult> results = [];
        Dictionary<string, int> nativePrefixes = [];
        foreach (var (key, placements) in allPlacements.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var level = catalog.FindByKey(key) ?? throw new InvalidDataException($"Unknown destination level '{key}'.");
            var removed = removedRowsByLevel?.FirstOrDefault(k => string.Equals(catalog.FindByKey(k.Key)?.Key, level.Key, StringComparison.OrdinalIgnoreCase)).Value;
            if (placements.Count == 0 && removed is not { Count: > 0 }) continue;
            byte[] levelBytes = current.EntryBytes(level.SourceWadEntry);
            int nativeRows = new UniversalLevelData(levelBytes).Scene().RowCount;
            if (identityWorkspace != null && placements.Any(p => p.PersistentSlot != null))
            {
                UniversalNativeLayoutStore.Validate(identityWorkspace, key, nativeRows);
                nativePrefixes[key] = nativeRows;
            }
            var composed = UniversalMobyComposer.Compose(level, levelBytes,
                current.EntryBytes(level.SourceWadEntry - 1), exe, k => sources.Get(k), sources.SharedClasses,
                protectedVram(currentImagePath, level), placements, SpecialProvider(japanese, sources),
                removed,
                useNativeOwnershipScan ? dropped => DefaultProtectedVram(currentImagePath, level, dropped) : null, sources.SharedModelPackage, sources.PlayerAnimations,
                removedTreasureAlreadyApplied, nativeSharedEntry8:current.EntryBytes(8));
            exe = composed.Executable;
            replacements[level.SourceWadEntry] = composed.LevelData;
            replacements[level.SourceWadEntry - 1] = composed.Overlay;
            results.Add(new(level.Key, level.DisplayName, composed));
        }
        // Dragonfly eggs: (level index, placed row) of every egg, counted by the inventory's own row.
        List<(int LevelIndex, int Row, int Colour)> dragonflyEggs = [];
        foreach (var r in results)
        {
            var placed = allPlacements.First(k => string.Equals(catalog.FindByKey(k.Key)?.Key, r.LevelKey, StringComparison.OrdinalIgnoreCase)).Value;
            var level = catalog.FindByKey(r.LevelKey)!;
            for (int i = 0; i < placed.Count; i++)
                if (!placed[i].Retired && placed[i].SpecialId is { } id && UniversalDragonflyEggs.Variants.Any(v => v.Id == id))
                    dragonflyEggs.Add((CollectedMaskIndex(level), r.Compose.PlacementSaveRows[i], UniversalDragonflyEggs.VariantIndex(id)));
        }
        var signLevels = allPlacements.Where(k => k.Value.Any(p => !p.Retired && UniversalSpecialCatalog.NeedsRenderHook(p.SpecialId)))
            .Select(k => catalog.FindByKey(k.Key)!.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!deferSpecialHooks)
        {
            UniversalSpecialBehaviors.PatchExeCave(exe, sources.Disc.Exe, signLevels.Count > 0, dragonflyEggs, dragonflyEggs.Count > 0 ? japanese?.InventoryEggModel() : null);
            UniversalEggUnlocks.PatchOverlays(catalog, sources.Disc.EntryBytes, current.EntryBytes, replacements, dragonflyEggs.Count);
            if (signLevels.Count > 0)
                RequireSignMarkersOnlyIn(exe, catalog, signLevels, i => replacements.TryGetValue(i, out var r) ? r : current.EntryBytes(i));
        }
        byte[] wad = current.RebuildWad(replacements);
        string staging = outputImagePath + ".pending-" + Guid.NewGuid().ToString("N");
        try
        {
            current.WriteImage(staging, wad, exe);
            string sha;
            using (var fs = File.OpenRead(staging)) sha = Convert.ToHexStringLower(SHA256.HashData(fs));
            // Read back the private staged image before making a successful output visible.
            var readback = UniversalDiscImage.Read(staging);
            foreach (var (idx, bytes) in replacements)
                if (!readback.EntryBytes(idx).AsSpan().SequenceEqual(bytes))
                    throw new InvalidDataException($"Readback of WAD entry {idx} does not match the composed bytes.");
            if (!readback.Exe.AsSpan().SequenceEqual(exe)) throw new InvalidDataException("Readback of the executable does not match.");
            if (identityWorkspace != null)
                foreach (var (key, rows) in nativePrefixes) UniversalNativeLayoutStore.Bind(identityWorkspace, key, rows);
            File.Move(staging, outputImagePath); // refuse a destination created during export
            return new(outputImagePath, sha, results, wad.Length - current.Wad.Length)
            { SpecialHooksDeferred = deferSpecialHooks, ActiveDragonflyEggs = dragonflyEggs.ToArray(), SignLevelKeys = signLevels };
        }
        finally { if (File.Exists(staging)) File.Delete(staging); }
    }

    /// <summary>The sign render hook calls a level's stub only when its update function is preceded by the
    /// marker; prove no other level's bound update function is (the check is exact, per image).</summary>
    public static void RequireSignMarkersOnlyIn(byte[] exe, LevelCatalog catalog, IReadOnlySet<string> signLevels, Func<int, byte[]> entry)
    {
        foreach (var level in catalog.Levels)
        {
            if (level.SourceWadEntry < 1) continue;
            if (!UniversalExeBindings.Read(exe, level.LevelId).TryGetValue(UniversalExeBindings.UpdateGlobal, out var b)) continue;
            byte[] overlay = entry(level.SourceWadEntry - 1);
            long off = (long)b.Value - 12 - UniversalOverlay.Base;
            bool marked = off >= 0 && off + 4 <= overlay.Length &&
                BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan((int)off)) == UniversalSpecialBehaviors.SignHookMarker;
            if (marked != signLevels.Contains(level.Key))
                throw new InvalidDataException($"{level.DisplayName}: the sign render-hook marker is {(marked ? "present in a level without signs" : "missing")}.");
        }
    }

    /// <summary>Index of a level's durable collected-object mask (0x80077908 + index*32): realm*6 + slot.</summary>
    public static int CollectedMaskIndex(LevelDefinition level) => (level.LevelId / 10 - 1) * 6 + level.LevelId % 10;

    public static Func<string, UniversalSpecialAsset?>? SpecialProvider(UniversalJapaneseSource? japanese, UniversalSourceLibrary? sources = null)
    {
        if (japanese == null && sources == null) return null;
        var japaneseAssets = new Lazy<Dictionary<string, UniversalSpecialAsset>>(() =>
            japanese?.Assets().ToDictionary(a => a.Id) ?? []);
        var gems = new Lazy<Dictionary<string, UniversalSpecialAsset>>(() =>
            sources == null ? [] : UniversalHighValueGems.Assets(sources).ToDictionary(a => a.Id));
        return id => (UniversalHighValueGems.IsId(id) ? gems.Value : japaneseAssets.Value).GetValueOrDefault(id);
    }


    public static IEnumerable<(long Offset, int Length)> DefaultProtectedVram(string imagePath, LevelDefinition level) =>
        DefaultProtectedVram(imagePath, level, new HashSet<int>());

    /// <summary>Ownership-qualified free space after fully dropped resident models; every retained
    /// consumer and unclassified nonzero byte remains protected.</summary>
    public static IEnumerable<(long Offset, int Length)> DefaultProtectedVram(string imagePath, LevelDefinition level,
        IReadOnlySet<int> droppedClasses) => NativeTexturePageOwnershipScanner.ScanExcludingResidentClasses(imagePath, level, droppedClasses)
            .ProtectedRanges.Select(r => (r.Offset, r.Length));
}

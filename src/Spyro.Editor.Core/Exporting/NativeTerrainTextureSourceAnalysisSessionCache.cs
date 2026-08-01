using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Process-local memoization for the expensive, immutable retail-source
/// analyses shared by consecutive private-texture packing plans. Entries are
/// usable only with the exact full-BIN, texture-component, and level-data
/// fingerprints produced by the source-binding inspection. Final candidate
/// inspection and readback deliberately continue to use the uncached scanners.
/// </summary>
internal static class NativeTerrainTextureSourceAnalysisSessionCache
{
    private const int RuntimeAuditCapacity = 24;
    private const int OwnershipProofCapacity = 12;

    private static readonly object Gate = new();
    private static readonly BoundedLazyLru<RuntimeAuditKey, NativeTerrainTextureRuntimeControlAudit>
        RuntimeAudits = new(RuntimeAuditCapacity);
    private static readonly BoundedLazyLru<OwnershipProofKey, OwnershipProofOutcome>
        OwnershipProofs = new(OwnershipProofCapacity);

    private static int _runtimeAuditBuildCount;
    private static int _runtimeAuditHitCount;
    private static int _ownershipProofBuildCount;
    private static int _ownershipProofHitCount;

    internal static NativeTerrainTextureRuntimeControlAudit InspectRuntime(
        string sourceImagePath,
        LevelDefinition level,
        NativeTerrainTextureRecordAppendSourceBinding sourceBinding,
        out bool cacheHit)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(sourceBinding);
        ValidateBindingIdentity(sourceImagePath, level, sourceBinding);

        RuntimeAuditKey key = new(
            CaptureSourceStamp(sourceImagePath),
            BuildBindingFingerprint(sourceBinding),
            LevelCatalog.NormalizeKey(level.Key),
            level.SourceWadEntry);
        Lazy<NativeTerrainTextureRuntimeControlAudit> lazy;
        lock (Gate)
        {
            (lazy, cacheHit) = RuntimeAudits.GetOrAdd(
                key,
                () => NativeTerrainTextureRuntimeControlScanner.Inspect(
                    sourceImagePath,
                    level));
            if (cacheHit)
                _runtimeAuditHitCount++;
            else
                _runtimeAuditBuildCount++;
        }

        try
        {
            return lazy.Value;
        }
        catch
        {
            lock (Gate)
                RuntimeAudits.Remove(key, lazy);
            throw;
        }
    }

    internal static bool TryBuildRelocationOwnershipProof(
        string sourceImagePath,
        LevelDefinition level,
        IReadOnlyList<int> targetTextureIds,
        NativeTerrainTextureRecordAppendSourceBinding sourceBinding,
        out NativeTexturePageRelocationOwnershipProofResult? result,
        out string failureReason,
        out bool cacheHit)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(targetTextureIds);
        ArgumentNullException.ThrowIfNull(sourceBinding);
        ValidateBindingIdentity(sourceImagePath, level, sourceBinding);

        int[] exactIds = targetTextureIds.ToArray();
        OwnershipProofKey key = new(
            CaptureSourceStamp(sourceImagePath),
            BuildBindingFingerprint(sourceBinding),
            LevelCatalog.NormalizeKey(level.Key),
            level.SourceWadEntry,
            BuildTargetIdFingerprint(exactIds));
        Lazy<OwnershipProofOutcome> lazy;
        lock (Gate)
        {
            (lazy, cacheHit) = OwnershipProofs.GetOrAdd(
                key,
                () =>
                {
                    bool success =
                        NativeTexturePageOwnershipScanner.TryBuildRelocationOwnershipProof(
                            sourceImagePath,
                            level,
                            exactIds,
                            out NativeTexturePageRelocationOwnershipProofResult? proof,
                            out string reason);
                    return new OwnershipProofOutcome(success, proof, reason);
                });
            if (cacheHit)
                _ownershipProofHitCount++;
            else
                _ownershipProofBuildCount++;
        }

        OwnershipProofOutcome outcome;
        try
        {
            outcome = lazy.Value;
        }
        catch
        {
            lock (Gate)
                OwnershipProofs.Remove(key, lazy);
            throw;
        }

        result = outcome.Result;
        failureReason = outcome.FailureReason;
        return outcome.Success;
    }

    internal static NativeTerrainTextureSourceAnalysisCacheStats SnapshotForTesting()
    {
        lock (Gate)
        {
            return new NativeTerrainTextureSourceAnalysisCacheStats(
                _runtimeAuditBuildCount,
                _runtimeAuditHitCount,
                _ownershipProofBuildCount,
                _ownershipProofHitCount,
                RuntimeAudits.Count,
                OwnershipProofs.Count);
        }
    }

    internal static void ClearForTesting()
    {
        lock (Gate)
        {
            RuntimeAudits.Clear();
            OwnershipProofs.Clear();
            _runtimeAuditBuildCount = 0;
            _runtimeAuditHitCount = 0;
            _ownershipProofBuildCount = 0;
            _ownershipProofHitCount = 0;
        }
    }

    private static void ValidateBindingIdentity(
        string sourceImagePath,
        LevelDefinition level,
        NativeTerrainTextureRecordAppendSourceBinding sourceBinding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing retail source image.", sourceImagePath);
        if (!string.Equals(
                LevelCatalog.NormalizeKey(sourceBinding.TargetLevelKey),
                LevelCatalog.NormalizeKey(level.Key),
                StringComparison.OrdinalIgnoreCase) ||
            sourceBinding.TargetWadEntry != level.SourceWadEntry)
        {
            throw new InvalidDataException(
                "The private-texture source binding belongs to a different destination level or WAD entry.");
        }
        if (!IsSha256(sourceBinding.SourceImageSha256) ||
            !IsSha256(sourceBinding.ExpectedTextureComponentSha256) ||
            !IsSha256(sourceBinding.ExpectedLevelDataSha256))
        {
            throw new InvalidDataException(
                "The private-texture source binding is missing an exact source, texture-component, or level-data fingerprint.");
        }
    }

    private static string BuildBindingFingerprint(
        NativeTerrainTextureRecordAppendSourceBinding binding) =>
        string.Join(
            "|",
            binding.Version,
            binding.SourceImageSha256.ToUpperInvariant(),
            LevelCatalog.NormalizeKey(binding.TargetLevelKey),
            binding.TargetWadEntry,
            binding.ExpectedSourceTextureCount,
            binding.ExpectedTextureComponentSha256.ToUpperInvariant(),
            binding.ExpectedLevelDataSha256.ToUpperInvariant());

    private static string BuildTargetIdFingerprint(IReadOnlyList<int> targetTextureIds) =>
        $"{targetTextureIds.Count}:{string.Join(",", targetTextureIds.Order())}";

    private static SourceFileStamp CaptureSourceStamp(string sourceImagePath)
    {
        string fullPath = Path.GetFullPath(sourceImagePath);
        FileInfo source = new(fullPath);
        source.Refresh();
        if (!source.Exists)
            throw new FileNotFoundException("Missing retail source image.", fullPath);

        string resolvedPath = fullPath;
        FileInfo identity = source;
        try
        {
            if (source.ResolveLinkTarget(returnFinalTarget: true) is FileInfo resolved)
            {
                resolved.Refresh();
                if (resolved.Exists)
                {
                    resolvedPath = Path.GetFullPath(resolved.FullName);
                    identity = resolved;
                }
            }
        }
        catch (IOException)
        {
            // The exact source binding remains authoritative. A temporarily
            // unresolvable symlink simply uses its requested path metadata.
        }

        return new SourceFileStamp(
            fullPath,
            resolvedPath,
            identity.Length,
            identity.CreationTimeUtc.Ticks,
            identity.LastWriteTimeUtc.Ticks);
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } &&
        value.All(character =>
            character is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');

    private sealed record OwnershipProofOutcome(
        bool Success,
        NativeTexturePageRelocationOwnershipProofResult? Result,
        string FailureReason);

    private sealed record SourceFileStamp(
        string RequestedFullPath,
        string ResolvedFullPath,
        long Length,
        long CreationTimeUtcTicks,
        long LastWriteTimeUtcTicks);

    private sealed record RuntimeAuditKey(
        SourceFileStamp Source,
        string BindingFingerprint,
        string LevelKey,
        int WadEntry);

    private sealed record OwnershipProofKey(
        SourceFileStamp Source,
        string BindingFingerprint,
        string LevelKey,
        int WadEntry,
        string TargetIdsFingerprint);

    private sealed class BoundedLazyLru<TKey, TValue>
        where TKey : notnull
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, Entry> _entries = [];
        private readonly LinkedList<TKey> _recency = [];

        internal BoundedLazyLru(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        internal int Count => _entries.Count;

        internal (Lazy<TValue> Value, bool CacheHit) GetOrAdd(
            TKey key,
            Func<TValue> factory)
        {
            if (_entries.TryGetValue(key, out Entry? existing))
            {
                _recency.Remove(existing.Node);
                _recency.AddFirst(existing.Node);
                return (existing.Value, true);
            }

            Lazy<TValue> value = new(
                factory,
                LazyThreadSafetyMode.ExecutionAndPublication);
            LinkedListNode<TKey> node = _recency.AddFirst(key);
            _entries.Add(key, new Entry(value, node));
            while (_entries.Count > _capacity)
            {
                LinkedListNode<TKey> oldest = _recency.Last
                    ?? throw new InvalidOperationException("The session-cache LRU is inconsistent.");
                _recency.RemoveLast();
                _entries.Remove(oldest.Value);
            }
            return (value, false);
        }

        internal void Remove(TKey key, Lazy<TValue> expected)
        {
            if (!_entries.TryGetValue(key, out Entry? entry) ||
                !ReferenceEquals(entry.Value, expected))
            {
                return;
            }
            _entries.Remove(key);
            _recency.Remove(entry.Node);
        }

        internal void Clear()
        {
            _entries.Clear();
            _recency.Clear();
        }

        private sealed record Entry(
            Lazy<TValue> Value,
            LinkedListNode<TKey> Node);
    }
}

internal sealed record NativeTerrainTextureSourceAnalysisCacheStats(
    int RuntimeAuditBuildCount,
    int RuntimeAuditHitCount,
    int OwnershipProofBuildCount,
    int OwnershipProofHitCount,
    int RuntimeAuditEntryCount,
    int OwnershipProofEntryCount);

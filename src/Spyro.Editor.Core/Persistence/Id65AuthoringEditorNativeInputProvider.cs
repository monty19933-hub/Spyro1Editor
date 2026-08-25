using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

/// <summary>
/// Project-bound, read-only source for the three native inputs used by the
/// canonical ID65 editor projection. Callers provide only the exact lab
/// workspace binding; donor paths, retired foundation paths, package bytes,
/// and independently mixed input generations are not part of this surface.
/// </summary>
internal static class Id65AuthoringEditorNativeInputProvider
{
    public const string ProfileId =
        "id65-authoring-editor-native-input-provider-locked-workspace-v1";
    public const int SchemaVersion = 1;
    public const int MaximumCachedInputPairs = 2;
    public const string ExpectedCanonicalInputSetSha256 =
        "4c6fb8be8b43d2617c240196771670c07fd34d20c2731ff8cd7776edf09b3706";

    private static readonly object CacheGate = new();
    private static readonly Dictionary<string, CacheEntry> PairCache =
        new(StringComparer.Ordinal);
    private static long _nextCacheSequence;
    private static int _derivedPairBuildCount;

    public static Task<InputSet> LoadAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest expectedManifest,
        CancellationToken cancellationToken = default) =>
        LoadCoreAsync(paths, expectedManifest, testStageHook: null, cancellationToken);

    internal static Task<InputSet> LoadForSmokeAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest expectedManifest,
        Action<string>? testStageHook,
        CancellationToken cancellationToken = default) =>
        LoadCoreAsync(paths, expectedManifest, testStageHook, cancellationToken);

    internal static ProviderCacheSnapshot CaptureCacheSnapshotForSmoke()
    {
        lock (CacheGate)
        {
            return new(
                PairCache.Count,
                Volatile.Read(ref _derivedPairBuildCount),
                PairCache.Keys.Order(StringComparer.Ordinal).ToArray());
        }
    }

    internal static void ResetCacheForSmoke()
    {
        lock (CacheGate)
        {
            PairCache.Clear();
            _nextCacheSequence = 0;
            Volatile.Write(ref _derivedPairBuildCount, 0);
        }
    }

    private static async Task<InputSet> LoadCoreAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest expectedManifest,
        Action<string>? testStageHook,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(expectedManifest);
        cancellationToken.ThrowIfCancellationRequested();

        WorkspaceCapture before = await CaptureWorkspaceAsync(
            paths,
            expectedManifest,
            cancellationToken);
        testStageHook?.Invoke("after-initial-workspace-capture");
        cancellationToken.ThrowIfCancellationRequested();

        string cacheKey = BuildCacheKey(before, paths.LockedBaseImagePath);
        Lazy<Task<DerivedPair>> lazy = GetOrAddPair(
            cacheKey,
            paths.LockedBaseImagePath,
            testStageHook);
        DerivedPair pair;
        try
        {
            pair = await lazy.Value.WaitAsync(cancellationToken);
        }
        catch
        {
            RemoveFaultedPair(cacheKey, lazy);
            throw;
        }

        testStageHook?.Invoke("after-native-input-derivation");
        cancellationToken.ThrowIfCancellationRequested();
        WorkspaceCapture after = await CaptureWorkspaceAsync(
            paths,
            expectedManifest,
            cancellationToken);
        RequireSameWorkspaceGeneration(before, after);

        string canonicalSha256 = ComputeCanonicalInputSetSha256(after, pair);
        InputSet result = new ProviderInputSet(
            after.BoundSource,
            pair.PrivateTextureWitness,
            pair.GrassBundle,
            canonicalSha256);
        ValidateResult(result, after);
        return result;
    }

    private static Lazy<Task<DerivedPair>> GetOrAddPair(
        string cacheKey,
        string lockedBaseImagePath,
        Action<string>? testStageHook)
    {
        lock (CacheGate)
        {
            if (PairCache.TryGetValue(cacheKey, out CacheEntry? existing))
                return existing.Value;

            string sourcePath = Path.GetFullPath(lockedBaseImagePath);
            Lazy<Task<DerivedPair>>? created = null;
            created = new(
                () => StartDerivedPairBuild(
                    cacheKey,
                    created!,
                    sourcePath,
                    testStageHook),
                LazyThreadSafetyMode.ExecutionAndPublication);
            PairCache.Add(cacheKey, new(++_nextCacheSequence, created));
            while (PairCache.Count > MaximumCachedInputPairs)
            {
                KeyValuePair<string, CacheEntry> oldest = PairCache
                    .OrderBy(item => item.Value.Sequence)
                    .First();
                if (ReferenceEquals(oldest.Value.Value, created))
                    break;
                PairCache.Remove(oldest.Key);
            }
            return created;
        }
    }

    private static Task<DerivedPair> StartDerivedPairBuild(
        string cacheKey,
        Lazy<Task<DerivedPair>> candidate,
        string lockedBaseImagePath,
        Action<string>? testStageHook)
    {
        Task<DerivedPair> task = BuildDerivedPairAndEvictOnFailureAsync(
            cacheKey,
            candidate,
            lockedBaseImagePath,
            testStageHook);
        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return task;
    }

    private static async Task<DerivedPair> BuildDerivedPairAndEvictOnFailureAsync(
        string cacheKey,
        Lazy<Task<DerivedPair>> candidate,
        string lockedBaseImagePath,
        Action<string>? testStageHook)
    {
        try
        {
            return await Task.Run(() => BuildDerivedPair(lockedBaseImagePath, testStageHook));
        }
        catch
        {
            RemoveCachedPair(cacheKey, candidate);
            testStageHook?.Invoke("after-failed-native-input-cache-eviction");
            throw;
        }
    }

    private static DerivedPair BuildDerivedPair(
        string lockedBaseImagePath,
        Action<string>? testStageHook)
    {
        Interlocked.Increment(ref _derivedPairBuildCount);
        testStageHook?.Invoke("before-native-input-derivation");
        Id65V2NativeTextureWitness texture =
            Id65V2NativeTextureCompositionCompiler
                .DerivePinnedT66WitnessFromLockedBase(lockedBaseImagePath);
        Id65MobyDependencyBundleDescriptor grass =
            Id65AuthoringMobyDependencyBundleCompiler
                .CreateArtisansGrassBundleFromLockedBase(lockedBaseImagePath);
        if (texture.CanonicalSha256 !=
                Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 ||
            grass.CanonicalSha256 !=
                Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256)
        {
            throw new InvalidDataException(
                "The project-derived ID65 texture/Moby input pair lost an exact canonical pin.");
        }
        return new(texture, grass);
    }

    private static void RemoveFaultedPair(
        string cacheKey,
        Lazy<Task<DerivedPair>> candidate)
    {
        lock (CacheGate)
        {
            if (PairCache.TryGetValue(cacheKey, out CacheEntry? current) &&
                ReferenceEquals(current.Value, candidate) &&
                (!candidate.IsValueCreated ||
                 candidate.Value.IsFaulted ||
                 candidate.Value.IsCanceled))
            {
                PairCache.Remove(cacheKey);
            }
        }
    }

    private static void RemoveCachedPair(
        string cacheKey,
        Lazy<Task<DerivedPair>> candidate)
    {
        lock (CacheGate)
        {
            if (PairCache.TryGetValue(cacheKey, out CacheEntry? current) &&
                ReferenceEquals(current.Value, candidate))
            {
                PairCache.Remove(cacheKey);
            }
        }
    }

    private static async Task<WorkspaceCapture> CaptureWorkspaceAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest expectedManifest,
        CancellationToken cancellationToken)
    {
        RequireExactWorkspacePaths(paths);
        RefuseReparsePoint(paths.RootPath, "ID65 lab root");
        RefuseReparsePoint(paths.ManifestPath, "ID65 lab manifest");
        RefuseReparsePoint(paths.LockedBaseDirectoryPath, "ID65 lab locked-base root");
        RefuseReparsePoint(paths.LockedBaseImagePath, "ID65 lab locked-base BIN");
        RefuseReparsePoint(paths.LockedBaseCuePath, "ID65 lab locked-base CUE");
        RefuseReparsePoint(paths.AuthoredEditsDirectoryPath, "ID65 lab authored-edits root");

        UnusedLevel65BlankLevelLabProfileRegistry.ValidateManifest(
            expectedManifest,
            paths);
        UnusedLevel65BlankLevelLabManifest published =
            await UnusedLevel65BlankLevelLabBootstrapper
                .ValidatePublishedWorkspaceReadOnlyAsync(paths, cancellationToken);
        if (published != expectedManifest ||
            published.LockedBaseImageSha256 !=
                Id65RewardCensusContract.LockedSourceImageSha256 ||
            published.PromotionAuthorized ||
            published.NormalCreateBinEnabled)
        {
            throw new InvalidDataException(
                "The native-input provider is not bound to the exact unpromoted ID65 lab manifest.");
        }

        FileStamp manifest = CaptureFileStamp(paths.ManifestPath);
        FileStamp image = CaptureFileStamp(paths.LockedBaseImagePath);
        FileStamp cue = CaptureFileStamp(paths.LockedBaseCuePath);
        Id65AuthoringEditorAggregateBoundSource bound =
            Id65AuthoringEditorAggregateBoundSource.Capture(paths.LockedBaseImagePath);
        if (bound.SourceImageSha256 != published.LockedBaseImageSha256 ||
            bound.LockedSource.Row80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            bound.LockedSource.TargetOverlaySha256 !=
                Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 ||
            bound.LockedSource.GlobalExecutableSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256)
        {
            throw new InvalidDataException(
                "The native-input provider's physical locked-source capture changed.");
        }
        return new(published, manifest, image, cue, bound);
    }

    private static void RequireExactWorkspacePaths(
        UnusedLevel65BlankLevelLabWorkspacePaths paths)
    {
        UnusedLevel65BlankLevelLabManifest derived =
            UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(paths);
        if (derived.WorkspaceKey != UnusedLevel65BlankLevelLabProfileRegistry.Key ||
            derived.WorkspaceLayoutVersion !=
                UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutVersion)
        {
            throw new InvalidDataException(
                "The native-input provider received a noncanonical ID65 workspace layout.");
        }
    }

    private static FileStamp CaptureFileStamp(string path)
    {
        FileInfo file = new(Path.GetFullPath(path));
        if (!file.Exists || file.Length <= 0)
            throw new InvalidDataException($"The required native-input file is missing or empty: {file.Name}.");
        return new(file.Length, file.LastWriteTimeUtc.Ticks);
    }

    private static void RefuseReparsePoint(string path, string label)
    {
        FileSystemInfo entry = Directory.Exists(path)
            ? new DirectoryInfo(path)
            : new FileInfo(path);
        entry.Refresh();
        if (!entry.Exists)
            throw new InvalidDataException($"The {label} is missing.");
        if (entry.LinkTarget != null ||
            (entry.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"The {label} must not be a symbolic link or reparse point.");
        }
    }

    private static string BuildCacheKey(
        WorkspaceCapture capture,
        string lockedBaseImagePath) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            SchemaVersion.ToString(),
            Path.GetFullPath(lockedBaseImagePath),
            capture.Manifest.WorkspaceKey,
            capture.Manifest.WorkspaceLayoutVersion.ToString(),
            capture.ImageStamp.ByteLength.ToString(),
            capture.ImageStamp.LastWriteUtcTicks.ToString(),
            capture.BoundSource.SourceImageSha256,
            capture.BoundSource.LockedSource.Row80Sha256,
            capture.BoundSource.LockedSource.TargetOverlaySha256,
            capture.BoundSource.LockedSource.GlobalExecutableSha256,
            Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256,
            Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256
        ])));

    private static string ComputeCanonicalInputSetSha256(
        WorkspaceCapture capture,
        DerivedPair pair) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            SchemaVersion.ToString(),
            capture.Manifest.ProfileId,
            capture.Manifest.ProfileVersion.ToString(),
            capture.Manifest.WorkspaceKey,
            capture.Manifest.WorkspaceLayoutVersion.ToString(),
            capture.Manifest.LockedBaseProfileId,
            capture.BoundSource.SourceImageSha256,
            capture.BoundSource.LockedSource.Row80Sha256,
            capture.BoundSource.LockedSource.TargetOverlaySha256,
            capture.BoundSource.LockedSource.GlobalExecutableSha256,
            pair.PrivateTextureWitness.CanonicalSha256,
            pair.GrassBundle.CanonicalSha256
        ])));

    private static void RequireSameWorkspaceGeneration(
        WorkspaceCapture before,
        WorkspaceCapture after)
    {
        if (before.Manifest != after.Manifest ||
            before.ManifestStamp != after.ManifestStamp ||
            before.ImageStamp != after.ImageStamp ||
            before.CueStamp != after.CueStamp ||
            before.BoundSource.SourceImageSha256 != after.BoundSource.SourceImageSha256 ||
            before.BoundSource.LockedSource.Row80Sha256 !=
                after.BoundSource.LockedSource.Row80Sha256 ||
            before.BoundSource.LockedSource.TargetOverlaySha256 !=
                after.BoundSource.LockedSource.TargetOverlaySha256 ||
            before.BoundSource.LockedSource.GlobalExecutableSha256 !=
                after.BoundSource.LockedSource.GlobalExecutableSha256)
        {
            throw new InvalidDataException(
                "The ID65 lab workspace changed while its native editor inputs were being derived.");
        }
    }

    private static void ValidateResult(InputSet result, WorkspaceCapture capture)
    {
        ValidateForConsumption(result);
        if (result.LockedBaseImageSha256 != capture.BoundSource.SourceImageSha256 ||
            result.LockedRow80Sha256 != capture.BoundSource.LockedSource.Row80Sha256)
        {
            throw new InvalidDataException(
                "The ID65 native input set does not match its captured workspace generation.");
        }
    }

    internal static void ValidateForConsumption(InputSet result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result is not ProviderInputSet ||
            result.ProviderProfileId != ProfileId ||
            result.ProviderSchemaVersion != SchemaVersion ||
            result.LockedBaseImageSha256 != Id65RewardCensusContract.LockedSourceImageSha256 ||
            result.LockedRow80Sha256 != Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            result.BoundSource.LockedSource.TargetOverlaySha256 !=
                Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 ||
            result.BoundSource.LockedSource.GlobalExecutableSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256 ||
            result.PrivateTextureWitnessSha256 !=
                Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 ||
            result.GrassBundleSha256 !=
                Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256 ||
            result.CanonicalInputSetSha256 != ExpectedCanonicalInputSetSha256 ||
            !IsSha256(result.CanonicalInputSetSha256) ||
            result.ContainsFilePaths || result.ExposesNativePayloadBytes ||
            result.WritesFileSystem || result.WritesDiscImage || result.WritesCue ||
            result.AppIntegrated || result.UiIntegrated || result.CreateBinEnabled ||
            result.NormalCreateBinEnabled || result.RuntimeCandidateAuthorized ||
            result.RuntimeProven || result.ReleaseIntegrated || result.PromotionAuthorized ||
            result.Publishable || !result.RequiresTrustedExclusiveWorkspaceDirectory ||
            result.HostileFilesystemRaceSafe || result.CommonWorkspaceLeaseOwned)
        {
            throw new InvalidDataException(
                "The ID65 native input set is not an exact provider-owned input generation.");
        }
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed record CacheEntry(long Sequence, Lazy<Task<DerivedPair>> Value);

    private sealed record DerivedPair(
        Id65V2NativeTextureWitness PrivateTextureWitness,
        Id65MobyDependencyBundleDescriptor GrassBundle);

    private readonly record struct FileStamp(long ByteLength, long LastWriteUtcTicks);

    private sealed record WorkspaceCapture(
        UnusedLevel65BlankLevelLabManifest Manifest,
        FileStamp ManifestStamp,
        FileStamp ImageStamp,
        FileStamp CueStamp,
        Id65AuthoringEditorAggregateBoundSource BoundSource);

    internal abstract class InputSet
    {
        private protected InputSet()
        {
        }

        public string ProviderProfileId => ProfileId;
        public int ProviderSchemaVersion => SchemaVersion;
        public abstract string LockedBaseImageSha256 { get; }
        public abstract string LockedRow80Sha256 { get; }
        public abstract string PrivateTextureWitnessSha256 { get; }
        public abstract string GrassBundleSha256 { get; }
        public abstract string CanonicalInputSetSha256 { get; }

        public bool ContainsFilePaths => false;
        public bool ExposesNativePayloadBytes => false;
        public bool DerivedPayloadCachedInMemory => true;
        public bool DerivedPayloadPersisted => false;
        public bool WritesFileSystem => false;
        public bool WritesDiscImage => false;
        public bool WritesCue => false;
        public bool AppIntegrated => false;
        public bool UiIntegrated => false;
        public bool CreateBinEnabled => false;
        public bool NormalCreateBinEnabled => false;
        public bool RuntimeCandidateAuthorized => false;
        public bool RuntimeProven => false;
        public bool ReleaseIntegrated => false;
        public bool PromotionAuthorized => false;
        public bool Publishable => false;
        public bool RequiresTrustedExclusiveWorkspaceDirectory => true;
        public bool HostileFilesystemRaceSafe => false;
        public bool CommonWorkspaceLeaseOwned => false;

        internal abstract Id65AuthoringEditorAggregateBoundSource BoundSource { get; }
        internal Id65AuthoringCompositeLockedSource LockedSource => BoundSource.LockedSource;
        internal abstract Id65V2NativeTextureWitness PrivateTextureWitness { get; }
        internal abstract Id65MobyDependencyBundleDescriptor GrassBundle { get; }
    }

    private sealed class ProviderInputSet : InputSet
    {
        private readonly Id65AuthoringEditorAggregateBoundSource _boundSource;
        private readonly Id65V2NativeTextureWitness _privateTextureWitness;
        private readonly Id65MobyDependencyBundleDescriptor _grassBundle;

        internal ProviderInputSet(
            Id65AuthoringEditorAggregateBoundSource boundSource,
            Id65V2NativeTextureWitness privateTextureWitness,
            Id65MobyDependencyBundleDescriptor grassBundle,
            string canonicalInputSetSha256)
        {
            _boundSource = boundSource;
            _privateTextureWitness = privateTextureWitness;
            _grassBundle = grassBundle;
            CanonicalInputSetSha256 = canonicalInputSetSha256;
        }

        public override string LockedBaseImageSha256 => _boundSource.SourceImageSha256;
        public override string LockedRow80Sha256 => _boundSource.LockedSource.Row80Sha256;
        public override string PrivateTextureWitnessSha256 =>
            _privateTextureWitness.CanonicalSha256;
        public override string GrassBundleSha256 => _grassBundle.CanonicalSha256;
        public override string CanonicalInputSetSha256 { get; }

        internal override Id65AuthoringEditorAggregateBoundSource BoundSource => _boundSource;
        internal override Id65V2NativeTextureWitness PrivateTextureWitness =>
            _privateTextureWitness;
        internal override Id65MobyDependencyBundleDescriptor GrassBundle => _grassBundle;
    }

    internal sealed record ProviderCacheSnapshot(
        int EntryCount,
        int DerivedPairBuildCount,
        IReadOnlyList<string> CacheKeys);
}

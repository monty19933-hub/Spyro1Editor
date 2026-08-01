using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private const int PrivateTexturePreflightCacheSchemaVersion = 2;
    private const int PrivateTexturePreflightCacheCapacity = 12;
    internal const string PrivateTexturePreflightBuildStartedEvent = "build-started";
    internal const string PrivateTexturePreflightCacheHitEvent = "cache-hit";

    private readonly PrivateTextureSessionMemoizer<NativeTerrainTexturePrivateRecordStagingSourceProof>
        _privateTextureSourceBindingCache = new(PrivateTexturePreflightCacheCapacity);
    private readonly PrivateTextureSessionMemoizer<PrivateTextureFeasibilityResult>
        _privateTextureFeasibilityCache = new(PrivateTexturePreflightCacheCapacity);
    private long _privateTexturePreflightOperationGeneration;
    private int _privateTexturePreflightBuildCount;
    private int _privateTexturePreflightCacheHitCount;

    internal Action<string>? PrivateTexturePreflightObserverForTesting { get; set; }
    internal int PrivateTexturePreflightBuildCountForTesting =>
        Volatile.Read(ref _privateTexturePreflightBuildCount);
    internal int PrivateTexturePreflightCacheHitCountForTesting =>
        Volatile.Read(ref _privateTexturePreflightCacheHitCount);

    private async Task<PrivateTextureFeasibilityResult>
        GetAppendedPrivateTextureFeasibilityForStagingAsync(
            string sourceImagePath,
            string sourceCuePath,
            string wadAnalysisPath,
            LevelDefinition targetLevel,
            IReadOnlyList<NativeTerrainTextureRelocationEdit> savedEdits,
            IReadOnlyList<NativeTerrainTextureRecordExistingPatch> ordinaryLevelDataPatches,
            bool hasCustomImageTexturePatches,
            PrivateTexturePreflightOperationIdentity operationIdentity)
    {
        EnsurePrivateTexturePreflightOperationIsCurrent(operationIdentity);
        PrivateTextureFileStamp sourceStamp =
            CapturePrivateTextureFileStamp(sourceImagePath, "source BIN");
        NativeTerrainTexturePrivateRecordStagingSourceProof stagingSourceProof =
            await GetPrivateTextureSourceProofForStagingAsync(
                sourceStamp,
                targetLevel,
                operationIdentity);
        EnsurePrivateTexturePreflightOperationIsCurrent(operationIdentity);
        NativeTerrainTextureRecordAppendSourceBinding sourceBinding =
            stagingSourceProof.SourceBinding;
        PrivateTextureFileStamp cueStamp =
            CapturePrivateTextureFileStamp(sourceCuePath, "source CUE");
        PrivateTextureFileStamp wadAnalysisStamp =
            CapturePrivateTextureFileStamp(wadAnalysisPath, "WAD analysis");
        string cueSha256 = await GetSmallPrivateTextureFileSha256Async(cueStamp);
        string wadAnalysisSha256 =
            await GetSmallPrivateTextureFileSha256Async(wadAnalysisStamp);
        EnsurePrivateTexturePreflightOperationIsCurrent(operationIdentity);
        NativeTerrainTextureStructuralGrowthPolicy structuralGrowthPolicy =
            AppendedPrivateTerrainTextureResearchGate.ResolveStructuralGrowthPolicy(
                sourceBinding,
                savedEdits.Count(edit => edit.UsesAppendedPrivateRecord));

        NativeTerrainTextureRelocationEdit[] stableSavedEdits = savedEdits.ToArray();
        NativeTerrainTextureRecordExistingPatch[] stableOrdinaryPatches =
            ordinaryLevelDataPatches
                .Select(patch => patch with
                {
                    Before = patch.Before.ToArray(),
                    After = patch.After.ToArray()
                })
                .ToArray();
        string key = BuildPrivateTextureFeasibilityCacheKey(
            sourceStamp,
            cueStamp,
            cueSha256,
            wadAnalysisStamp,
            wadAnalysisSha256,
            targetLevel,
            sourceBinding,
            stableSavedEdits,
            stableOrdinaryPatches,
            hasCustomImageTexturePatches,
            structuralGrowthPolicy);

        PrivateTextureSessionMemoizedResult<PrivateTextureFeasibilityResult> memoized =
            await _privateTextureFeasibilityCache.GetOrCreateAsync(
                key,
                async () =>
                {
                    Interlocked.Increment(ref _privateTexturePreflightBuildCount);
                    PrivateTexturePreflightObserverForTesting?.Invoke(
                        PrivateTexturePreflightBuildStartedEvent);

                    string feasibilityDirectory = Path.Combine(
                        operationIdentity.WorkspaceRootPath,
                        "_local",
                        "research",
                        "appended-private-feasibility");
                    Directory.CreateDirectory(feasibilityDirectory);
                    string outputPrefix = Path.Combine(
                        feasibilityDirectory,
                        $"{SafeFilePart(targetLevel.Key)}-session-preflight-not-exported");
                    NativeTerrainTexturePrivateRecordBatchRequest request = new(
                        sourceStamp.FullPath,
                        cueStamp.FullPath,
                        outputPrefix,
                        wadAnalysisStamp.FullPath,
                        targetLevel,
                        stableSavedEdits,
                        stableOrdinaryPatches,
                        hasCustomImageTexturePatches,
                        structuralGrowthPolicy);

                    (bool built, NativeTerrainTexturePrivateRecordStagingPreflightResult? plan, string failureReason) =
                        await Task.Run(() =>
                        {
                            bool success =
                                NativeTerrainTexturePrivateRecordBatchCompiler
                                    .TryBuildStagingPreflight(
                                    request,
                                    stagingSourceProof,
                                    out NativeTerrainTexturePrivateRecordStagingPreflightResult? builtPlan,
                                    out string reason);
                            return (success, builtPlan, reason);
                        });

                    if (!FileStampStillMatches(sourceStamp) ||
                        !FileStampStillMatches(cueStamp) ||
                        !FileStampStillMatches(wadAnalysisStamp))
                    {
                        return PrivateTextureFeasibilityResult.TransientFailure(
                            sourceBinding,
                            "The source BIN/CUE or WAD analysis changed while the private-texture safety check was running. Run the check again.");
                    }
                    string currentCueSha256 = Sha256PrivateTextureFile(cueStamp.FullPath);
                    string currentWadAnalysisSha256 =
                        Sha256PrivateTextureFile(wadAnalysisStamp.FullPath);
                    if (!string.Equals(
                            currentCueSha256,
                            cueSha256,
                            StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(
                            currentWadAnalysisSha256,
                            wadAnalysisSha256,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return PrivateTextureFeasibilityResult.TransientFailure(
                            sourceBinding,
                            "The source CUE or WAD analysis bytes changed while the private-texture safety check was running. Run the check again.");
                    }
                    if (!built || plan == null)
                    {
                        string reason = string.IsNullOrWhiteSpace(failureReason)
                            ? "The private-texture structural planner rejected this edit without a reason."
                            : failureReason.Trim();
                        return new PrivateTextureFeasibilityResult(
                            Success: false,
                            SourceBinding: sourceBinding,
                            WriterKind: null,
                            AppendedPrivateEditCount: stableSavedEdits.Count(edit =>
                                edit.UsesAppendedPrivateRecord),
                            StructuralGrowthPolicy: structuralGrowthPolicy,
                            FailureReason: reason,
                            Cacheable: IsDeterministicPrivateTextureFeasibilityFailure(reason));
                    }
                    if (plan.SourceBinding != sourceBinding)
                    {
                        return PrivateTextureFeasibilityResult.TransientFailure(
                            sourceBinding,
                            "The source BIN preimage changed between binding and private-texture planning. Run the check again.");
                    }

                    return new PrivateTextureFeasibilityResult(
                        Success: true,
                        SourceBinding: plan.SourceBinding,
                        WriterKind: plan.WriterKind,
                        AppendedPrivateEditCount: plan.AppendedPrivateEditCount,
                        StructuralGrowthPolicy: structuralGrowthPolicy,
                        FailureReason: "",
                        Cacheable: true);
                },
                result => result.Cacheable);

        if (memoized.HitKind != PrivateTextureSessionCacheHitKind.None)
        {
            Interlocked.Increment(ref _privateTexturePreflightCacheHitCount);
            PrivateTexturePreflightObserverForTesting?.Invoke(
                PrivateTexturePreflightCacheHitEvent);
        }
        EnsurePrivateTexturePreflightOperationIsCurrent(operationIdentity);
        return memoized.Value;
    }

    private async Task<NativeTerrainTextureRecordAppendSourceBinding>
        GetPrivateTextureSourceBindingForStagingAsync(
            PrivateTextureFileStamp sourceStamp,
            LevelDefinition targetLevel,
            PrivateTexturePreflightOperationIdentity operationIdentity) =>
        (await GetPrivateTextureSourceProofForStagingAsync(
            sourceStamp,
            targetLevel,
            operationIdentity)).SourceBinding;

    private async Task<NativeTerrainTexturePrivateRecordStagingSourceProof>
        GetPrivateTextureSourceProofForStagingAsync(
            PrivateTextureFileStamp sourceStamp,
            LevelDefinition targetLevel,
            PrivateTexturePreflightOperationIdentity operationIdentity)
    {
        EnsurePrivateTexturePreflightOperationIsCurrent(operationIdentity);
        string key = BuildPrivateTextureSourceBindingCacheKey(
            sourceStamp,
            targetLevel);
        PrivateTextureSessionMemoizedResult<NativeTerrainTexturePrivateRecordStagingSourceProof>
            memoized = await _privateTextureSourceBindingCache.GetOrCreateAsync(
                key,
                () => Task.Run(() =>
                    NativeTerrainTexturePrivateRecordStagingSourceProof.Capture(
                        sourceStamp.FullPath,
                        targetLevel)),
                _ => true);
        EnsurePrivateTexturePreflightOperationIsCurrent(operationIdentity);
        return memoized.Value;
    }

    private static async Task<string> GetSmallPrivateTextureFileSha256Async(
        PrivateTextureFileStamp stamp)
    {
        string sha256 = await Task.Run(() => Sha256PrivateTextureFile(stamp.FullPath));
        if (!FileStampStillMatches(stamp))
        {
            throw new IOException(
                $"The private-texture input changed while hashing {Path.GetFileName(stamp.FullPath)}.");
        }
        return sha256;
    }

    private void ClearPrivateTexturePreflightSessionCache()
    {
        AdvancePrivateTexturePreflightOperationGeneration();
        _privateTextureSourceBindingCache.Clear();
        _privateTextureFeasibilityCache.Clear();
    }

    private void AdvancePrivateTexturePreflightOperationGeneration()
    {
        Interlocked.Increment(ref _privateTexturePreflightOperationGeneration);
        ClearTerrainPrivateTextureCapacityProof();
    }

    private PrivateTexturePreflightOperationIdentity
        CapturePrivateTexturePreflightOperationIdentity(
            string sourceImagePath,
            LevelDefinition targetLevel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        ArgumentNullException.ThrowIfNull(targetLevel);
        LevelDefinition currentLevel = _currentLevel ??
            throw new InvalidOperationException(
                "A current destination level is required for a private-texture preflight.");
        string targetLevelKey = LevelCatalog.NormalizeKey(targetLevel.Key);
        string currentLevelKey = LevelCatalog.NormalizeKey(currentLevel.Key);
        if (!string.Equals(
                targetLevelKey,
                currentLevelKey,
                StringComparison.OrdinalIgnoreCase) ||
            targetLevel.SourceWadEntry != currentLevel.SourceWadEntry)
        {
            throw new StalePrivateTexturePreflightException(
                "The private-texture destination changed before its safety check began.");
        }

        return new PrivateTexturePreflightOperationIdentity(
            Volatile.Read(ref _privateTexturePreflightOperationGeneration),
            Path.GetFullPath(_workspace.RootPath),
            currentLevelKey,
            currentLevel.SourceWadEntry,
            Path.GetFullPath(sourceImagePath));
    }

    private void EnsurePrivateTexturePreflightOperationIsCurrent(
        PrivateTexturePreflightOperationIdentity expected)
    {
        string currentSourceImagePath = FirstExistingDiscImagePath(
            _discImagePathBox.Text,
            _skyboxDiscImagePathBox.Text,
            DiscImageLocator.FindImage(_workspace));
        string? mismatch = FindPrivateTexturePreflightOperationMismatch(
            expected,
            Volatile.Read(ref _privateTexturePreflightOperationGeneration),
            _workspace.RootPath,
            _currentLevel?.Key ?? "",
            _currentLevel?.SourceWadEntry ?? -1,
            currentSourceImagePath);
        if (!string.IsNullOrWhiteSpace(mismatch))
            throw new StalePrivateTexturePreflightException(mismatch);
    }

    private static string? FindPrivateTexturePreflightOperationMismatch(
        PrivateTexturePreflightOperationIdentity expected,
        long currentGeneration,
        string currentWorkspaceRootPath,
        string currentLevelKey,
        int currentTargetWadEntry,
        string currentSourceImagePath)
    {
        if (expected.Generation != currentGeneration)
        {
            return "The private-texture safety check became stale because its workspace, source disc, or loaded level changed. No edit was staged; click the terrain again.";
        }
        if (string.IsNullOrWhiteSpace(currentWorkspaceRootPath) ||
            !PathsEqual(expected.WorkspaceRootPath, currentWorkspaceRootPath))
        {
            return "The private-texture safety check belongs to a different workspace. No edit was staged; click the terrain again.";
        }
        if (!string.Equals(
                expected.TargetLevelKey,
                LevelCatalog.NormalizeKey(currentLevelKey),
                StringComparison.OrdinalIgnoreCase) ||
            expected.TargetWadEntry != currentTargetWadEntry)
        {
            return "The private-texture safety check belongs to a different destination level or WAD entry. No edit was staged; click the terrain again.";
        }
        if (string.IsNullOrWhiteSpace(currentSourceImagePath) ||
            !PathsEqual(expected.SourceImagePath, currentSourceImagePath))
        {
            return "The private-texture safety check belongs to a different source BIN. No edit was staged; click the terrain again.";
        }
        return null;
    }

    private static string BuildPrivateTextureSourceBindingCacheKey(
        PrivateTextureFileStamp sourceStamp,
        LevelDefinition targetLevel)
    {
        using MemoryStream bytes = new();
        using BinaryWriter writer = new(bytes, Encoding.UTF8, leaveOpen: true);
        writer.Write(PrivateTexturePreflightCacheSchemaVersion);
        WritePrivateTextureFileStamp(writer, sourceStamp);
        WritePrivateTextureLevelDefinition(writer, targetLevel);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(bytes.GetBuffer().AsSpan(
            0,
            checked((int)bytes.Length))));
    }

    private static string BuildPrivateTextureFeasibilityCacheKey(
        PrivateTextureFileStamp sourceStamp,
        PrivateTextureFileStamp cueStamp,
        string cueSha256,
        PrivateTextureFileStamp wadAnalysisStamp,
        string wadAnalysisSha256,
        LevelDefinition targetLevel,
        NativeTerrainTextureRecordAppendSourceBinding sourceBinding,
        IReadOnlyList<NativeTerrainTextureRelocationEdit> savedEdits,
        IReadOnlyList<NativeTerrainTextureRecordExistingPatch> ordinaryLevelDataPatches,
        bool hasCustomImageTexturePatches,
        NativeTerrainTextureStructuralGrowthPolicy structuralGrowthPolicy)
    {
        using MemoryStream bytes = new();
        using BinaryWriter writer = new(bytes, Encoding.UTF8, leaveOpen: true);
        writer.Write(PrivateTexturePreflightCacheSchemaVersion);
        WritePrivateTextureFileStamp(writer, sourceStamp);
        WritePrivateTextureFileStamp(writer, cueStamp);
        WritePrivateTextureString(writer, cueSha256);
        WritePrivateTextureFileStamp(writer, wadAnalysisStamp);
        WritePrivateTextureString(writer, wadAnalysisSha256);
        WritePrivateTextureLevelDefinition(writer, targetLevel);
        writer.Write(sourceBinding.Version);
        WritePrivateTextureString(writer, sourceBinding.SourceImageSha256);
        WritePrivateTextureString(writer, sourceBinding.TargetLevelKey);
        writer.Write(sourceBinding.TargetWadEntry);
        writer.Write(sourceBinding.ExpectedSourceTextureCount);
        WritePrivateTextureString(
            writer,
            sourceBinding.ExpectedTextureComponentSha256);
        WritePrivateTextureString(writer, sourceBinding.ExpectedLevelDataSha256);
        writer.Write(hasCustomImageTexturePatches);
        writer.Write((int)structuralGrowthPolicy);

        NativeTerrainTextureRelocationEdit[] orderedEdits = savedEdits
            .OrderBy(edit => edit.TargetTextureId)
            .ThenBy(edit => edit.DonorWadEntry)
            .ThenBy(edit => edit.DonorTextureId)
            .ThenBy(edit => edit.MaterialTemplateTextureId)
            .ToArray();
        writer.Write(orderedEdits.Length);
        foreach (NativeTerrainTextureRelocationEdit edit in orderedEdits)
        {
            writer.Write(edit.TargetTextureId);
            WritePrivateTextureString(writer, edit.DonorLevelKey);
            WritePrivateTextureString(writer, edit.DonorLevelName);
            writer.Write(edit.DonorWadEntry);
            writer.Write(edit.DonorTextureId);
            WritePrivateTextureString(writer, edit.DonorRuntimeKey);
            WritePrivateTextureString(writer, edit.DescriptorTier);
            writer.Write((int)edit.ApplyMode);
            writer.Write((int)edit.TargetRecordKind);
            writer.Write(edit.TargetWadEntry);
            WritePrivateTextureString(writer, edit.SourceImageSha256);
            writer.Write(edit.SourceTextureRecordCount);
            WritePrivateTextureString(
                writer,
                edit.SourceTextureComponentSha256);
            WritePrivateTextureString(writer, edit.SourceLevelDataSha256);
            writer.Write(edit.MaterialTemplateTextureId);
            WritePrivateTextureString(writer, edit.PrivateRecordEditId);
        }

        NativeTerrainTextureRecordExistingPatch[] orderedPatches =
            ordinaryLevelDataPatches
                .OrderBy(patch => patch.WadOffset)
                .ThenBy(patch => patch.Kind, StringComparer.Ordinal)
                .ThenBy(patch => patch.RuntimeKey, StringComparer.Ordinal)
                .ThenBy(patch => patch.Before.Length)
                .ThenBy(patch => patch.After.Length)
                .ToArray();
        writer.Write(orderedPatches.Length);
        foreach (NativeTerrainTextureRecordExistingPatch patch in orderedPatches)
        {
            writer.Write(patch.WadOffset);
            WritePrivateTextureString(
                writer,
                Convert.ToHexString(SHA256.HashData(patch.Before)));
            WritePrivateTextureString(
                writer,
                Convert.ToHexString(SHA256.HashData(patch.After)));
            WritePrivateTextureString(writer, patch.Kind);
            WritePrivateTextureString(writer, patch.RuntimeKey);
        }

        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(bytes.GetBuffer().AsSpan(
            0,
            checked((int)bytes.Length))));
    }

    private static void WritePrivateTextureLevelDefinition(
        BinaryWriter writer,
        LevelDefinition targetLevel)
    {
        WritePrivateTextureString(writer, targetLevel.Key);
        WritePrivateTextureString(writer, targetLevel.ScriptKey);
        WritePrivateTextureString(writer, targetLevel.DisplayName);
        writer.Write(targetLevel.LevelId);
        writer.Write(targetLevel.SourceWadEntry);
        WritePrivateTextureString(writer, targetLevel.SourceTableWadOffset);
        WritePrivateTextureString(writer, targetLevel.SourceTableRelativeOffset);
        writer.Write(targetLevel.SourceRecordCount);
        WritePrivateTextureString(writer, targetLevel.Confidence);
        WritePrivateTextureString(writer, targetLevel.RuntimeMobyPointer);
    }

    private static void WritePrivateTextureFileStamp(
        BinaryWriter writer,
        PrivateTextureFileStamp stamp)
    {
        WritePrivateTextureString(writer, stamp.FullPath);
        writer.Write(stamp.Length);
        writer.Write(stamp.LastWriteTimeUtcTicks);
    }

    private static void WritePrivateTextureString(BinaryWriter writer, string? value)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(value ?? "");
        writer.Write(utf8.Length);
        writer.Write(utf8);
    }

    private static PrivateTextureFileStamp CapturePrivateTextureFileStamp(
        string path,
        string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        FileInfo file = new(fullPath);
        file.Refresh();
        if (!file.Exists)
            throw new FileNotFoundException($"Missing {description}.", fullPath);
        return new PrivateTextureFileStamp(
            fullPath,
            file.Length,
            file.LastWriteTimeUtc.Ticks);
    }

    private static bool FileStampStillMatches(PrivateTextureFileStamp expected)
    {
        try
        {
            return CapturePrivateTextureFileStamp(expected.FullPath, "preflight input") ==
                   expected;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string Sha256PrivateTextureFile(string path)
    {
        using FileStream stream = File.Open(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool IsDeterministicPrivateTextureFeasibilityFailure(
        string failureReason)
    {
        string reason = failureReason ?? "";
        string[] transientMarkers =
        [
            "missing source BIN",
            "missing source CUE",
            "could not find file",
            "being used by another process",
            "access to the path",
            "permission denied",
            "input/output error",
            "I/O error",
            "changed while"
        ];
        return !transientMarkers.Any(marker =>
            reason.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    internal static async Task<string>
        AssertPrivateTextureFeasibilityMemoizerForTestingAsync()
    {
        PrivateTextureSessionMemoizer<int> cache = new(capacity: 2);
        TaskCompletionSource<bool> release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        int builds = 0;
        Func<Task<int>> slowFactory = async () =>
        {
            Interlocked.Increment(ref builds);
            await release.Task;
            return 17;
        };

        Task<PrivateTextureSessionMemoizedResult<int>> first =
            cache.GetOrCreateAsync("same", slowFactory, _ => true);
        Task<PrivateTextureSessionMemoizedResult<int>> concurrent =
            cache.GetOrCreateAsync("same", slowFactory, _ => true);
        release.TrySetResult(true);
        PrivateTextureSessionMemoizedResult<int>[] pair =
            await Task.WhenAll(first, concurrent);
        if (builds != 1 ||
            pair.Count(result =>
                result.HitKind == PrivateTextureSessionCacheHitKind.InFlight) != 1 ||
            pair.Any(result => result.Value != 17))
        {
            throw new InvalidOperationException(
                "The private-texture session cache did not deduplicate identical in-flight safety checks.");
        }

        PrivateTextureSessionMemoizedResult<int> completed =
            await cache.GetOrCreateAsync(
                "same",
                () =>
                {
                    Interlocked.Increment(ref builds);
                    return Task.FromResult(99);
                },
                _ => true);
        if (completed.HitKind != PrivateTextureSessionCacheHitKind.Completed ||
            completed.Value != 17 ||
            builds != 1)
        {
            throw new InvalidOperationException(
                "The completed private-texture structural proof was not reused.");
        }

        PrivateTextureSessionMemoizer<int> staleCache = new(capacity: 1);
        TaskCompletionSource<bool> staleFactoryStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> staleFactoryRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        PrivateTexturePreflightOperationIdentity inFlightIdentity = new(
            Generation: 41,
            WorkspaceRootPath: Path.GetFullPath("/tmp/private-texture-workspace-a"),
            TargetLevelKey: "artisans",
            TargetWadEntry: 63,
            SourceImagePath: Path.GetFullPath("/tmp/private-texture-source-a.bin"));
        Task<PrivateTextureSessionMemoizedResult<int>> staleInFlight =
            staleCache.GetOrCreateAsync(
                "clear-during-build",
                async () =>
                {
                    staleFactoryStarted.TrySetResult(true);
                    await staleFactoryRelease.Task;
                    return 31;
                },
                _ => true);
        await staleFactoryStarted.Task;
        staleCache.Clear();
        long generationAfterClear = inFlightIdentity.Generation + 1;
        staleFactoryRelease.TrySetResult(true);
        PrivateTextureSessionMemoizedResult<int> staleResult = await staleInFlight;
        if (staleResult.Value != 31 ||
            string.IsNullOrWhiteSpace(FindPrivateTexturePreflightOperationMismatch(
                inFlightIdentity,
                generationAfterClear,
                inFlightIdentity.WorkspaceRootPath,
                inFlightIdentity.TargetLevelKey,
                inFlightIdentity.TargetWadEntry,
                inFlightIdentity.SourceImagePath)) ||
            string.IsNullOrWhiteSpace(FindPrivateTexturePreflightOperationMismatch(
                inFlightIdentity,
                inFlightIdentity.Generation,
                inFlightIdentity.WorkspaceRootPath,
                inFlightIdentity.TargetLevelKey,
                inFlightIdentity.TargetWadEntry,
                "/tmp/private-texture-source-b.bin")) ||
            FindPrivateTexturePreflightOperationMismatch(
                inFlightIdentity,
                inFlightIdentity.Generation,
                inFlightIdentity.WorkspaceRootPath,
                inFlightIdentity.TargetLevelKey,
                inFlightIdentity.TargetWadEntry,
                inFlightIdentity.SourceImagePath) != null)
        {
            throw new InvalidOperationException(
                "An in-flight private-texture proof was not rejected after its session generation or source identity changed.");
        }

        _ = await cache.GetOrCreateAsync(
            "changed-contract",
            () =>
            {
                Interlocked.Increment(ref builds);
                return Task.FromResult(23);
            },
            _ => true);
        if (builds != 2)
        {
            throw new InvalidOperationException(
                "A changed private-texture edit contract incorrectly reused an older proof.");
        }

        _ = await cache.GetOrCreateAsync(
            "transient",
            () =>
            {
                Interlocked.Increment(ref builds);
                return Task.FromResult(-1);
            },
            value => value >= 0);
        _ = await cache.GetOrCreateAsync(
            "transient",
            () =>
            {
                Interlocked.Increment(ref builds);
                return Task.FromResult(-1);
            },
            value => value >= 0);
        if (builds != 4)
        {
            throw new InvalidOperationException(
                "An uncacheable/transient private-texture failure was retained.");
        }

        string hashTestDirectory = Path.Combine(
            Path.GetTempPath(),
            $"spyro-private-texture-hash-{Guid.NewGuid():N}");
        Directory.CreateDirectory(hashTestDirectory);
        string hashTestPath = Path.Combine(hashTestDirectory, "analysis.json");
        try
        {
            DateTime fixedWriteTimeUtc = new(
                2026,
                1,
                2,
                3,
                4,
                6,
                DateTimeKind.Utc);
            await File.WriteAllTextAsync(hashTestPath, "AAAA");
            File.SetLastWriteTimeUtc(hashTestPath, fixedWriteTimeUtc);
            PrivateTextureFileStamp hashStampA =
                CapturePrivateTextureFileStamp(hashTestPath, "hash smoke input");
            string hashA =
                await GetSmallPrivateTextureFileSha256Async(hashStampA);
            await File.WriteAllTextAsync(hashTestPath, "BBBB");
            File.SetLastWriteTimeUtc(hashTestPath, fixedWriteTimeUtc);
            PrivateTextureFileStamp hashStampB =
                CapturePrivateTextureFileStamp(hashTestPath, "hash smoke input");
            string hashB =
                await GetSmallPrivateTextureFileSha256Async(hashStampB);
            if (!string.Equals(
                    hashStampA.CacheKey,
                    hashStampB.CacheKey,
                    StringComparison.Ordinal) ||
                string.Equals(hashA, hashB, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Small private-texture inputs were not freshly hashed after a same-size, same-mtime rewrite.");
            }
        }
        finally
        {
            Directory.Delete(hashTestDirectory, recursive: true);
        }

        PrivateTextureFileStamp sourceStamp = new("/tmp/source.bin", 100, 200);
        PrivateTextureFileStamp cueStamp = new("/tmp/source.cue", 10, 20);
        PrivateTextureFileStamp analysisStamp = new("/tmp/wad-analysis.json", 30, 40);
        LevelDefinition target = new()
        {
            Key = "artisans",
            ScriptKey = "artisans",
            DisplayName = "Artisans",
            LevelId = 10,
            SourceWadEntry = 63,
            SourceTableWadOffset = "0x1",
            SourceTableRelativeOffset = "0x2",
            SourceRecordCount = 68,
            Confidence = "verified",
            RuntimeMobyPointer = "0x3"
        };
        NativeTerrainTextureRecordAppendSourceBinding binding = new(
            1,
            new string('A', 64),
            "artisans",
            63,
            68,
            new string('B', 64),
            new string('C', 64));
        NativeTerrainTextureRelocationEdit edit = new(
            68,
            "gnastysworld",
            "Gnasty's World",
            57,
            17,
            "gnastysworld:terrain-texture:17",
            NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
            "/tmp/preview-a.png",
            "preview-a.png",
            "2026-01-01T00:00:00Z",
            NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget,
            NativeTerrainTextureTargetRecordKind.AppendedPrivate,
            63,
            binding.SourceImageSha256,
            binding.ExpectedSourceTextureCount,
            binding.ExpectedTextureComponentSha256,
            binding.ExpectedLevelDataSha256,
            55,
            NativeTerrainTextureRelocationEditStore.BuildAppendedPrivateRecordId(68));
        string canonical = BuildPrivateTextureFeasibilityCacheKey(
            sourceStamp,
            cueStamp,
            new string('D', 64),
            analysisStamp,
            new string('E', 64),
            target,
            binding,
            [edit],
            [],
            hasCustomImageTexturePatches: false,
            structuralGrowthPolicy:
                NativeTerrainTextureStructuralGrowthPolicy.NormalChecked);
        string presentationOnlyChanged = BuildPrivateTextureFeasibilityCacheKey(
            sourceStamp,
            cueStamp,
            new string('D', 64),
            analysisStamp,
            new string('E', 64),
            target,
            binding,
            [edit with
            {
                PreviewImagePath = "/tmp/preview-b.png",
                PreviewImageName = "preview-b.png",
                CreatedAt = "2026-02-02T00:00:00Z"
            }],
            [],
            hasCustomImageTexturePatches: false,
            structuralGrowthPolicy:
                NativeTerrainTextureStructuralGrowthPolicy.NormalChecked);
        string donorChanged = BuildPrivateTextureFeasibilityCacheKey(
            sourceStamp,
            cueStamp,
            new string('D', 64),
            analysisStamp,
            new string('E', 64),
            target,
            binding,
            [edit with { DonorTextureId = 18 }],
            [],
            hasCustomImageTexturePatches: false,
            structuralGrowthPolicy:
                NativeTerrainTextureStructuralGrowthPolicy.NormalChecked);
        string customWriterChanged = BuildPrivateTextureFeasibilityCacheKey(
            sourceStamp,
            cueStamp,
            new string('D', 64),
            analysisStamp,
            new string('E', 64),
            target,
            binding,
            [edit],
            [],
            hasCustomImageTexturePatches: true,
            structuralGrowthPolicy:
                NativeTerrainTextureStructuralGrowthPolicy.NormalChecked);
        string structuralPolicyChanged = BuildPrivateTextureFeasibilityCacheKey(
            sourceStamp,
            cueStamp,
            new string('D', 64),
            analysisStamp,
            new string('E', 64),
            target,
            binding,
            [edit],
            [],
            hasCustomImageTexturePatches: false,
            structuralGrowthPolicy:
                NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch);
        NativeTerrainTexturePrivateRecordBatchRequest normalBuildRequest = new(
            sourceStamp.FullPath,
            cueStamp.FullPath,
            "/tmp/private-texture-not-exported",
            analysisStamp.FullPath,
            target,
            [edit],
            [],
            HasCustomImageTexturePatches: false,
            StructuralGrowthPolicy:
                NativeTerrainTextureStructuralGrowthPolicy.NormalChecked);
        string normalBuildSignature =
            BuildPrivateTerrainBuildPlanInputSignature(normalBuildRequest);
        string staticBuildSignature = BuildPrivateTerrainBuildPlanInputSignature(
            normalBuildRequest with
            {
                StructuralGrowthPolicy =
                    NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch
            });
        if (!string.Equals(canonical, presentationOnlyChanged, StringComparison.Ordinal) ||
            string.Equals(canonical, donorChanged, StringComparison.Ordinal) ||
            string.Equals(canonical, customWriterChanged, StringComparison.Ordinal) ||
            string.Equals(canonical, structuralPolicyChanged, StringComparison.Ordinal) ||
            string.Equals(
                normalBuildSignature,
                staticBuildSignature,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A private-texture cache key either included preview metadata or omitted a structural donor, writer, or growth-policy contract.");
        }

        return "one build served two concurrent requests, the completed proof hit the bounded cache, clear/source identity changes invalidated an in-flight operation, same-size same-mtime small inputs were freshly hashed, a changed contract missed, transient failures were rebuilt, and both feasibility and final-build cache identities ignored preview metadata while tracking structural donor, writer, and growth-policy changes";
    }

    private sealed record PrivateTexturePreflightOperationIdentity(
        long Generation,
        string WorkspaceRootPath,
        string TargetLevelKey,
        int TargetWadEntry,
        string SourceImagePath);

    private sealed class StalePrivateTexturePreflightException : InvalidOperationException
    {
        public StalePrivateTexturePreflightException(string message)
            : base(message)
        {
        }
    }

    private sealed record PrivateTextureFeasibilityResult(
        bool Success,
        NativeTerrainTextureRecordAppendSourceBinding SourceBinding,
        NativeTerrainTexturePrivateImageWriterKind? WriterKind,
        int AppendedPrivateEditCount,
        NativeTerrainTextureStructuralGrowthPolicy StructuralGrowthPolicy,
        string FailureReason,
        bool Cacheable)
    {
        public static PrivateTextureFeasibilityResult TransientFailure(
            NativeTerrainTextureRecordAppendSourceBinding sourceBinding,
            string failureReason) =>
            new(
                Success: false,
                SourceBinding: sourceBinding,
                WriterKind: null,
                AppendedPrivateEditCount: 0,
                StructuralGrowthPolicy:
                    NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
                FailureReason: failureReason,
                Cacheable: false);
    }

    private sealed record PrivateTextureFileStamp(
        string FullPath,
        long Length,
        long LastWriteTimeUtcTicks)
    {
        public string CacheKey =>
            $"{FullPath.Length}:{FullPath}|{Length}|{LastWriteTimeUtcTicks}";
    }
}

internal enum PrivateTextureSessionCacheHitKind
{
    None,
    Completed,
    InFlight
}

internal sealed record PrivateTextureSessionMemoizedResult<T>(
    T Value,
    PrivateTextureSessionCacheHitKind HitKind);

internal sealed class PrivateTextureSessionMemoizer<T>
{
    private readonly int _capacity;
    private readonly object _sync = new();
    private readonly Dictionary<string, T> _completed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LinkedListNode<string>> _nodes =
        new(StringComparer.Ordinal);
    private readonly LinkedList<string> _lru = new();
    private readonly Dictionary<string, Task<T>> _inFlight =
        new(StringComparer.Ordinal);
    private long _generation;

    public PrivateTextureSessionMemoizer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public async Task<PrivateTextureSessionMemoizedResult<T>> GetOrCreateAsync(
        string key,
        Func<Task<T>> factory,
        Func<T, bool> shouldCache)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(shouldCache);

        Task<T> task;
        PrivateTextureSessionCacheHitKind hitKind;
        TaskCompletionSource<T>? starter = null;
        long generation = 0;
        lock (_sync)
        {
            if (_completed.TryGetValue(key, out T? completed))
            {
                TouchLocked(key);
                return new PrivateTextureSessionMemoizedResult<T>(
                    completed,
                    PrivateTextureSessionCacheHitKind.Completed);
            }
            if (_inFlight.TryGetValue(key, out Task<T>? existing))
            {
                task = existing;
                hitKind = PrivateTextureSessionCacheHitKind.InFlight;
            }
            else
            {
                starter = new TaskCompletionSource<T>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                task = starter.Task;
                _inFlight.Add(key, task);
                generation = _generation;
                hitKind = PrivateTextureSessionCacheHitKind.None;
            }
        }

        if (starter != null)
        {
            _ = CompleteFactoryAsync(
                key,
                generation,
                starter,
                factory,
                shouldCache);
        }
        return new PrivateTextureSessionMemoizedResult<T>(
            await task.ConfigureAwait(false),
            hitKind);
    }

    public void Clear()
    {
        lock (_sync)
        {
            _generation++;
            _completed.Clear();
            _nodes.Clear();
            _lru.Clear();
            _inFlight.Clear();
        }
    }

    private async Task CompleteFactoryAsync(
        string key,
        long generation,
        TaskCompletionSource<T> completion,
        Func<Task<T>> factory,
        Func<T, bool> shouldCache)
    {
        try
        {
            T value = await factory().ConfigureAwait(false);
            lock (_sync)
            {
                if (_generation == generation && shouldCache(value))
                    AddCompletedLocked(key, value);
            }
            completion.TrySetResult(value);
        }
        catch (Exception ex)
        {
            completion.TrySetException(ex);
        }
        finally
        {
            lock (_sync)
            {
                if (_inFlight.TryGetValue(key, out Task<T>? task) &&
                    ReferenceEquals(task, completion.Task))
                {
                    _inFlight.Remove(key);
                }
            }
        }
    }

    private void AddCompletedLocked(string key, T value)
    {
        _completed[key] = value;
        TouchLocked(key);
        while (_completed.Count > _capacity && _lru.Last != null)
        {
            string oldest = _lru.Last.Value;
            _lru.RemoveLast();
            _nodes.Remove(oldest);
            _completed.Remove(oldest);
        }
    }

    private void TouchLocked(string key)
    {
        if (_nodes.Remove(key, out LinkedListNode<string>? existing))
            _lru.Remove(existing);
        LinkedListNode<string> node = _lru.AddFirst(key);
        _nodes[key] = node;
    }
}

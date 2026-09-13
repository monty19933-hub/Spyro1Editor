using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeFreeFlightResearchSmoke")]

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeFreeFlightResearchEntry(int Index, int OriginalOffset, int FinalOffset,
    int OriginalLength, int FinalLength, string OriginalSha256, string FinalSha256);

/// <summary>
/// One exact-original Toasty research recipe. Known normal-load extents are a
/// precondition, not peak-memory, gameplay, or ordinary export approval.
/// </summary>
public sealed class NativeFreeFlightResearchCandidatePlan
{
    public const string Profile = "native-toasty-free-flight-research-v1";
    public const int TargetLevelId = 14;
    public const int RequiredNormalBufferGap = 0xA780;
    internal const string ImageName = "toasty-native-free-flight-r01.bin";
    internal const string CueName = "toasty-native-free-flight-r01.cue";
    internal const string ReceiptName = "native-free-flight.receipt.json";
    private readonly NativeFreeFlightImageComposition _composition;
    private readonly NativeFreeFlightResearchSourceCue _cue;
    private readonly MobySourcePatch? _testSelector;

    private NativeFreeFlightResearchCandidatePlan(NativeFreeFlightResearchSourceCue cue,
        NativeFreeFlightImageComposition composition)
    {
        _composition = composition;
        SourceImagePath = composition.SourceImagePath;
        _cue = cue;
        _testSelector = composition.TestSelector;
        AnimationMerge = composition.AnimationMerge ?? throw new InvalidDataException("Research requires enabled player insertion.");
        ExecutablePatch = composition.ExecutablePatch;
        Memory = composition.Memory ?? throw new InvalidDataException("Research requires its exact normal-load memory report.");
        Entries = composition.Entries;
        ExpectedExecutableSha256 = composition.ExpectedExecutableSha256;
        var merge = AnimationMerge;
        var patch = ExecutablePatch;
        PlanSha256 = Hash(AddSelectorMetadata(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Profile = ProfileId, sourceImageSha256 = NativeFreeFlightPatchPlan.SourceImageSha256,
            SourceImagePath, sourceCueSha256 = cue.SourceSha256, sourceCuePath = cue.CuePath,
            animationPlan = merge.PlanSha256, executablePlan = patch.PlanSha256,
            ExpectedExecutableSha256, expectedWadSha256 = composition.ExpectedWadSha256, expectedRootSha256 = composition.ExpectedRootSha256,
            Memory, Entries, RuntimeVerified, NormalCreateBinApproved
        }), false));
    }

    public string SourceImagePath { get; }
    public string PlanSha256 { get; }
    public string ExpectedExecutableSha256 { get; }
    public bool TestLevelSelectorEnabled => _testSelector != null;
    public string ProfileId => TestLevelSelectorEnabled ? "native-toasty-free-flight-fast-entry-research-v2" : Profile;
    public string OutputImageName => TestLevelSelectorEnabled ? "toasty-native-free-flight-fast-entry-r02.bin" : ImageName;
    public string OutputCueName => TestLevelSelectorEnabled ? "toasty-native-free-flight-fast-entry-r02.cue" : CueName;
    internal string[] OutputNames => [OutputImageName, ReceiptName, OutputCueName];
    public int RelocatedExecutableLba => 53882;
    public NativePlayerAnimationMergePlan AnimationMerge { get; }
    public NativeFreeFlightPatchPlan ExecutablePatch { get; }
    public NativeLevelRuntimeMemoryReport Memory { get; }
    public IReadOnlyList<NativeFreeFlightResearchEntry> Entries { get; }
    public bool RuntimeVerified => false;
    public bool NormalCreateBinApproved => false;
    internal DiscLayout Layout => _composition.Layout;
    internal string SourceCuePath => _cue.CuePath;
    internal byte[] CopyCueBytes() => _cue.CopyOutputBytes();
    internal byte[] CopyAnalysisBytes() => _composition.CopyAnalysisBytes();
    internal byte[] CopyExecutable() => _composition.CopyExecutable();
    internal byte[] ApplyToExecutable(ReadOnlySpan<byte> original) => _composition.ApplyToExecutable(original);
    internal NativeSkyRelocationPayload CopyInsertionPayload() => _composition.CopyInsertionPayload();
    internal Task WriteInitialImageAsync(string path, string analysisPath, CancellationToken cancellationToken) =>
        _composition.WriteInitialImageAsync(path, analysisPath, cancellationToken);
    internal void ApplyExecutableToImage(string path) => _composition.ApplyExecutableToImage(path);

    public static Task<NativeFreeFlightResearchCandidatePlan> CaptureAsync(string originalImagePath,
        string originalCuePath, LevelCatalog catalog, CancellationToken cancellationToken = default) =>
        CaptureCoreAsync(originalImagePath, originalCuePath, catalog, false, cancellationToken);

    /// <summary>Separate opt-in diagnostic profile. Native inventory selection is
    /// not normal portal-entry evidence and is never included in ordinary export.</summary>
    public static Task<NativeFreeFlightResearchCandidatePlan> CaptureWithTestLevelSelectorAsync(string originalImagePath,
        string originalCuePath, LevelCatalog catalog, CancellationToken cancellationToken = default) =>
        CaptureCoreAsync(originalImagePath, originalCuePath, catalog, true, cancellationToken);

    private static async Task<NativeFreeFlightResearchCandidatePlan> CaptureCoreAsync(string originalImagePath,
        string originalCuePath, LevelCatalog catalog, bool testLevelSelector, CancellationToken cancellationToken)
    {
        string source = Path.GetFullPath(originalImagePath);
        var cue = NativeFreeFlightResearchSourceCue.Capture(originalCuePath, source,
            testLevelSelector ? "toasty-native-free-flight-fast-entry-r02.bin" : ImageName);
        var composition = await NativeFreeFlightImageComposition.CaptureResearchAsync(source, catalog, testLevelSelector, cancellationToken);
        var result = new NativeFreeFlightResearchCandidatePlan(cue, composition);
        await result.RequireFreshAsync(cancellationToken);
        return result;
    }

    public async Task RequireFreshAsync(CancellationToken cancellationToken = default)
    {
        _cue.RequireFresh();
        await _composition.RequireOriginalAsync(cancellationToken);
        _cue.RequireFresh();
    }

    internal void RequireRelocation(NativeSkyWadRelocationPlan relocation) => _composition.RequireRelocation(relocation);
    internal (string Hash, int Changed) VerifyImage(string path) => _composition.VerifyImage(path);

    internal byte[] BuildReceipt((string Hash, int Changed) verified) => AddSelectorMetadata(JsonSerializer.SerializeToUtf8Bytes(new
    {
        profile = ProfileId, planSha256 = PlanSha256, sourceImageSha256 = NativeFreeFlightPatchPlan.SourceImageSha256,
        SourceImagePath, sourceCuePath = SourceCuePath, sourceCueSha256 = _cue.SourceSha256,
        outputImageSha256 = verified.Hash, changedRawSectors = verified.Changed, outputCueSha256 = Hash(CopyCueBytes()),
        ExpectedExecutableSha256, animationMerge = AnimationMerge, executablePatch = ExecutablePatch,
        Memory, Entries, wholeImagePhysicalReadback = true, wholeTargetReadback = true,
        originalRowsModelsSceneOverlayUnchanged = true, sourceCueBoundAtFreshnessBoundaries = true,
        researchOnly = true, RuntimeVerified, NormalCreateBinApproved,
        exceptionRollbackPublication = true, crashAtomicPublication = false, hostileFilesystemRaceSafe = false,
        pending = new[] { "peak/transient/alternate-mode RAM", "full flight-state effects and sound closure",
            "native ascent/descent/controls/collision", "landing/death/respawn/level transitions", "ordinary editor integration" }
    }, new JsonSerializerOptions { WriteIndented = true }), true);

    private byte[] AddSelectorMetadata(byte[] bytes, bool indented)
    {
        // Keep every existing v1 plan/receipt byte stable, including property
        // order and whitespace. The separate v2 profile adds explicit evidence.
        if (_testSelector == null) return bytes;
        JsonObject document = JsonNode.Parse(bytes)!.AsObject();
        document.Add("testLevelSelector", JsonSerializer.SerializeToNode(_testSelector));
        document.Add("normalPortalEntryVerified", false);
        return JsonSerializer.SerializeToUtf8Bytes(document, new JsonSerializerOptions { WriteIndented = indented });
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

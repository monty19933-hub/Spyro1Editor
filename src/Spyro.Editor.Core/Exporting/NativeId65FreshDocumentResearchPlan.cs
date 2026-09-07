using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Editing;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65FreshDocumentResearchSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Read-only research image expectation for an actual privately compiled fresh
/// document. The complete compilation, not caller hashes or an equal upload,
/// supplies native byte authority. Derived image hashes are not independent
/// recipe certificates, project-save binding, runtime or publication approval.
/// Internal streaming adapters serve only the saved-context publication engine;
/// this plan exposes no public artifact writer, receipt or publisher.
/// </summary>
public sealed class NativeId65FreshDocumentResearchPlan
{
    public const string ProfileId = "native-id65-fresh-document-untextured-N16-research-v1";
    internal const string ImageName = "native-id65-fresh-document-research.bin";
    private readonly NativeId65FreshDiscPlan _disc;
    private readonly byte[] _canonical;
    private readonly byte[] _sourceCue;
    private readonly byte[] _outputCue;
    private readonly string _imageIdentity;
    private readonly string _cueIdentity;

    private NativeId65FreshDocumentResearchPlan(string image, string cue,
        NativeId65FreshDocumentCompilation issued, byte[] canonical, byte[] sourceCue,
        string imageIdentity, string cueIdentity, NativeId65FreshDiscVerification expected)
    {
        SourceImagePath = image;
        SourceCuePath = cue;
        _disc = issued.DiscPlan;
        _canonical = canonical;
        _sourceCue = sourceCue;
        _imageIdentity = imageIdentity;
        _cueIdentity = cueIdentity;
        _outputCue = DiscCueSheet.TextEncoding.GetBytes(
            $"FILE \"{ImageName}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        CanonicalDocumentSha256 = Hash(canonical);
        SourceCueSha256 = Hash(sourceCue);
        ExpectedImageSha256 = expected.OutputSha256;
        ExpectedChangedRawSectors = expected.ChangedSectors;
        DiscPlanSha256 = _disc.PlanSha256;
        BodyPlanSha256 = _disc.BodyPlanSha256;
        BodySha256 = Hash(_disc.CopyBody());
        OverlaySha256 = Hash(_disc.CopyOverlay());
        ExecutableSha256 = Hash(_disc.CopyExecutable());
        WadDirectorySha256 = Hash(_disc.CopyWadDirectory());
        IsoRootDirectorySha256 = Hash(_disc.CopyIsoRootDirectory());
        PendingNativeOwners = Array.AsReadOnly(_disc.PendingOwners.ToArray());
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = ProfileId, sourceImageSha256 = NativeId65FreshDiscSource.OriginalSha256,
            SourceImagePath, SourceCuePath, SourceCueSha256, imageIdentity, cueIdentity,
            DiscPlanSha256, BodyPlanSha256, BodySha256, OverlaySha256, ExecutableSha256,
            WadDirectorySha256, IsoRootDirectorySha256, CanonicalDocumentSha256,
            outputCueSha256 = Hash(_outputCue), ExpectedImageSha256, ExpectedChangedRawSectors,
            imageByteLength = NativeId65FreshDiscSource.ImageByteLength
        }));
    }

    public string SourceImagePath { get; }
    public string SourceCuePath { get; }
    public string SourceCueSha256 { get; }
    public string CanonicalDocumentSha256 { get; }
    public string DiscPlanSha256 { get; }
    public string BodyPlanSha256 { get; }
    public string BodySha256 { get; }
    public string OverlaySha256 { get; }
    public string ExecutableSha256 { get; }
    public string WadDirectorySha256 { get; }
    public string IsoRootDirectorySha256 { get; }
    public string PlanSha256 { get; }
    public string ExpectedImageSha256 { get; }
    public int ExpectedChangedRawSectors { get; }
    public long ExpectedImageByteLength => NativeId65FreshDiscSource.ImageByteLength;
    public IReadOnlyList<string> PendingNativeOwners { get; }
    public bool RuntimeVerified => false;
    public bool NormalCreateBinApproved => false;
    public bool CompleteNativeDependencyClosureProven => false;
    public bool ProjectSaveBindingClaimed => false;
    public byte[] CopyCanonicalDocument() => _canonical.ToArray();

    public static async Task<NativeId65FreshDocumentResearchPlan> CaptureAsync(string originalBin,
        string sourceCue, NativeId65FreshDocumentCompilation issued, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(issued);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCue);
        // This sealed private compilation always issues the current complete
        // untextured body, not an arbitrary DiscPlan paired with neutral intent.
        // Keep its fixed family separate from geometry/music values and lengths.
        byte[] canonical = issued.CopyCanonicalDocument();
        var disc = issued.DiscPlan;
        if (!canonical.AsSpan().SequenceEqual(NativeId65FreshDocumentCodec.SerializeCanonical(issued.Intent)) ||
            Hash(canonical) != issued.CanonicalSha256 ||
            disc.Executable.SelectedBuiltInTrack != issued.Intent.MusicTrackId ||
            disc.CopyBody().Length != 0x1FB800 || disc.FinalExecutableLba != 54921 || disc.FinalWadByteLength != 0x6B32000)
            throw new InvalidDataException("Fresh-document research requires the actual complete untextured N16 compilation and its canonical intent.");
        string image = Path.GetFullPath(originalBin), cue = Path.GetFullPath(sourceCue);
        string imageIdentity = PathIdentity(image), cueIdentity = PathIdentity(cue);
        byte[] cueBytes = ReadCueFile(cue, token);
        ValidateSourceCue(cueBytes, cue, image);
        NativeId65FreshDiscVerification expected;
        using (FileStream source = new(image, FileMode.Open, FileAccess.Read, FileShare.Read))
            expected = Measure(disc, source, token);
        var plan = new NativeId65FreshDocumentResearchPlan(image, cue, issued, canonical,
            cueBytes, imageIdentity, cueIdentity, expected);
        await plan.RequireFreshAsync(token);
        token.ThrowIfCancellationRequested();
        return plan;
    }

    internal byte[] CopyCueBytes() => _outputCue.ToArray();

    // Both initial capture and subsequent expected-image checks execute this
    // same bounded stream algorithm. It writes no file and materializes at most
    // one source sector plus one afterimage sector, not a complete disc buffer.
    internal NativeId65FreshDiscVerification MeasureExpectedImage(Stream source, CancellationToken token)
    {
        NativeId65FreshDiscVerification result = Measure(_disc, source, token);
        RequireExpected(result);
        return result;
    }

    private static NativeId65FreshDiscVerification Measure(NativeId65FreshDiscPlan disc,
        Stream source, CancellationToken token, Stream? output = null, Action? firstSector = null)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead || !source.CanSeek || source.Position != 0 || source.Length != NativeId65FreshDiscSource.ImageByteLength)
            throw new InvalidDataException("Fresh-document expected image requires a complete readable original at offset zero.");
        if (output != null && (!output.CanWrite || !output.CanSeek || output.Position != 0 || output.Length != 0))
            throw new InvalidDataException("Fresh-document streaming requires a new empty writable owner.");
        using var originalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var expectedHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] before = new byte[2352];
        int changed = 0;
        for (int lba = 0; lba < NativeId65FreshDiscSource.SectorCount; lba++)
        {
            token.ThrowIfCancellationRequested();
            source.ReadExactly(before);
            // Every sector, including untouched metadata/data, is authenticated
            // against the SAME private index captured with the original SHA.
            byte[] after = disc.CreateExpectedRawSector(lba, before);
            originalHash.AppendData(before);
            expectedHash.AppendData(after);
            if (!before.AsSpan().SequenceEqual(after)) changed++;
            if (output != null)
            {
                output.Write(after);
                if (lba == 0) firstSector?.Invoke();
            }
        }
        NativeId65FreshDiscSource.RequireActualEof(source, token);
        if (Convert.ToHexStringLower(originalHash.GetHashAndReset()) != NativeId65FreshDiscSource.OriginalSha256)
            throw new InvalidDataException("Fresh-document expected image lost its complete original source binding.");
        if (output != null && (output.Position != NativeId65FreshDiscSource.ImageByteLength || output.Length != output.Position))
            throw new InvalidDataException("Fresh-document streamed output length or position changed.");
        token.ThrowIfCancellationRequested();
        return new(Convert.ToHexStringLower(expectedHash.GetHashAndReset()), changed);
    }

    // The SAME per-sector authenticated measurement loop now supplies the
    // engine's new-only staged file. No duplicate physical relocation grammar.
    internal void WriteNewImage(string path, CancellationToken token, Action firstSector)
    {
        token.ThrowIfCancellationRequested();
        RequireCueFresh(token);
        using FileStream source = new(SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        RequireExpected(Measure(_disc, source, token, output, firstSector));
        output.Flush(flushToDisk: true);
        token.ThrowIfCancellationRequested();
        RequireCueFresh(token);
    }

    internal NativeId65FreshDiscVerification VerifyImage(string path, CancellationToken token)
    {
        using FileStream source = new(SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using FileStream output = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try { return VerifyImage(source, output, token); }
        catch (InvalidDataException error)
        { throw new InvalidDataException("Saved fresh document physical readback failed: " + error.Message, error); }
    }

    internal NativeId65FreshDiscVerification VerifyImage(Stream source, Stream candidate, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidate);
        NativeId65FreshDiscVerification result = _disc.VerifyImage(source, candidate, token);
        RequireExpected(result);
        return result;
    }

    private void RequireExpected(NativeId65FreshDiscVerification result)
    {
        if (result.OutputSha256 != ExpectedImageSha256 || result.ChangedSectors != ExpectedChangedRawSectors)
            throw new InvalidDataException("Fresh-document readback differs from its privately derived complete afterimage.");
    }

    internal async Task RequireFreshAsync(CancellationToken token = default)
    {
        RequireCueFresh(token);
        using FileStream source = new(SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await NativeId65FreshDiscSource.RequireWholeOriginalAsync(source, token);
        RequireCueFresh(token);
    }

    private void RequireCueFresh(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_imageIdentity != PathIdentity(SourceImagePath) || _cueIdentity != PathIdentity(SourceCuePath))
            throw new InvalidDataException("Fresh-document source image/CUE path identity changed.");
        byte[] current = ReadCueFile(SourceCuePath, token);
        if (!_sourceCue.AsSpan().SequenceEqual(current))
            throw new InvalidDataException("Fresh-document source CUE bytes changed.");
        // FILE can name a third alias. Equality and path-relation validation use
        // this SAME bounded snapshot, never another filesystem CUE read.
        ValidateSourceCue(current, SourceCuePath, SourceImagePath);
        if (_imageIdentity != PathIdentity(SourceImagePath) || _cueIdentity != PathIdentity(SourceCuePath))
            throw new InvalidDataException("Fresh-document source path changed during its binding check.");
        token.ThrowIfCancellationRequested();
    }

    private static byte[] ReadCueFile(string cue, CancellationToken token)
    {
        using FileStream input = new(cue, FileMode.Open, FileAccess.Read, FileShare.Read);
        return NativeId65FreshDiscResearchCandidatePlan.ReadCueSnapshot(input, token);
    }

    private static void ValidateSourceCue(ReadOnlySpan<byte> bytes, string cue, string image)
    {
        // Validation only: the returned fixed-starter filename is discarded.
        try { _ = NativeId65FreshDiscResearchCandidatePlan.CompileCueForSmoke(bytes, cue, image); }
        catch (DecoderFallbackException error)
        { throw new InvalidDataException("Fresh-document source CUE is not valid UTF8.", error); }
    }

    private static string PathIdentity(string path)
    {
        FileInfo file = new(path);
        if (!file.Exists || Directory.Exists(path))
            throw new InvalidDataException("Fresh-document source path is missing or not a file.");
        List<string> identity = [file.FullName, file.LinkTarget ?? ""];
        FileSystemInfo resolved = file.ResolveLinkTarget(true) ?? file;
        identity.Add(resolved.FullName);
        foreach (string leaf in new[] { file.FullName, resolved.FullName })
            for (DirectoryInfo? parent = new FileInfo(leaf).Directory; parent != null; parent = parent.Parent)
            {
                if (identity.Count > 132) throw new InvalidDataException("Fresh-document source path depth exceeds its bound.");
                identity.Add(parent.FullName + "|" + parent.LinkTarget);
            }
        return string.Join('\n', identity);
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

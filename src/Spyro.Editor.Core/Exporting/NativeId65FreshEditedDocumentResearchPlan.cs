using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65FreshEditedDocumentResearchSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>One exact edited-document, source-issued untextured N16 LAB/track02 research
/// afterimage. Filesystem readback is not native runtime or normal export approval.</summary>
public sealed class NativeId65FreshEditedDocumentResearchPlan
{
    public const string ProfileId = "native-id65-fresh-edited-landing-track02-research-r01";
    internal const string ImageName = "native-id65-fresh-edited-landing-track02-r01.bin";
    internal const string ReceiptName = "native-id65-fresh-edited-landing-track02-r01.receipt.json";
    internal const string CueName = "native-id65-fresh-edited-landing-track02-r01.cue";
    internal static string[] OutputNames => [ImageName, ReceiptName, CueName];
    private const string ImageHash = "ecc8f61cc16bf86a2f5117b327c49aede18f5c1e6a602c0e67f2ba58565aed16";
    private const string CanonicalJson = """{"kind":"spyro-native-fresh-intent","schema":1,"floor":{"x":8192,"y":8192,"z":8704,"width":2048,"depth":2048},"spawn":{"x":9216,"y":9216,"heading":96},"display":{"floorRgb":[72,132,120],"skyRgb":[44,66,100]},"musicTrackId":2}""";
    private readonly NativeId65FreshDiscPlan _disc;
    private readonly byte[] _canonical;
    private readonly byte[] _sourceCue;
    private readonly byte[] _outputCue;
    private readonly string _cueIdentity;
    private readonly string _imageIdentity;

    private NativeId65FreshEditedDocumentResearchPlan(string image, string cue, NativeId65FreshDocumentCompilation issued,
        byte[] sourceCue, byte[] outputCue, string imageIdentity, string cueIdentity)
    {
        SourceImagePath = image;
        SourceCuePath = cue;
        _disc = issued.DiscPlan;
        _canonical = issued.CopyCanonicalDocument();
        CanonicalDocumentSha256 = Hash(_canonical);
        _sourceCue = sourceCue;
        _outputCue = outputCue;
        _imageIdentity = imageIdentity;
        _cueIdentity = cueIdentity;
        SourceCueSha256 = Hash(sourceCue);
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = ProfileId, source = NativeId65FreshDiscSource.OriginalSha256,
            SourceImagePath, SourceCuePath, SourceCueSha256, imageIdentity, cueIdentity,
            discPlan = _disc.PlanSha256, bodyPlan = _disc.BodyPlanSha256, CanonicalDocumentSha256,
            expectedImage = ImageHash, changedSectors = 1253
        }));
    }

    public string SourceImagePath { get; }
    public string SourceCuePath { get; }
    public string SourceCueSha256 { get; }
    public string PlanSha256 { get; }
    public string CanonicalDocumentSha256 { get; }
    public string ExpectedImageSha256 => ImageHash;
    public bool RuntimeVerified => false;
    public bool NormalCreateBinApproved => false;
    public bool CompleteNativeDependencyClosureProven => false;

    public static async Task<NativeId65FreshEditedDocumentResearchPlan> CaptureAsync(string originalBin,
        string sourceCue, NativeId65FreshDocumentCompilation issued, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(issued);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCue);
        // The actual private compilation owns this intent AND its disc ancestry.
        // Canonical bytes identify the exact revision; hashes never replace issuance.
        var disc = issued.DiscPlan;
        if (!issued.CopyCanonicalDocument().AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(CanonicalJson)) ||
            !disc.SourceCertified || disc.Executable.SelectedBuiltInTrack != 2 ||
            disc.FinalExecutableLba != 54921 || disc.FinalWadByteLength != 0x6B32000 ||
            Hash(disc.CopyBody()) != "bae43882a39dfc1604f4e6d578a354f34f99107f97faa9330600b3e01f34b15b" ||
            Hash(disc.CopyOverlay()) != "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5" ||
            Hash(disc.CopyExecutable()) != "2c1fd87e9fa8ce5c5f16be53e738405cec90c0bfc10ac9cb8cf1ce979b2e5c57" ||
            Hash(disc.CopyWadDirectory()) != "0a227cbf0d9f6b269ae7d41ce610129b0b48590519bbaaa2eac375e8818e23e0" ||
            Hash(disc.CopyIsoRootDirectory()) != "2412be680f008e5ebbf19e64948e84ad1d5eeadf07ab1c858c825c6608631089")
            throw new InvalidDataException("Research publication requires the exact edited document and its privately issued untextured N16/track02 compilation.");
        string image = Path.GetFullPath(originalBin), cue = Path.GetFullPath(sourceCue);
        string imageIdentity = PathIdentity(image), cueIdentity = PathIdentity(cue);
        byte[] bytes = ReadCueFile(cue, token);
        ValidateSourceCue(bytes, cue, image);
        byte[] output = DiscCueSheet.TextEncoding.GetBytes(
            $"FILE \"{ImageName}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        var plan = new NativeId65FreshEditedDocumentResearchPlan(image, cue, issued, bytes, output, imageIdentity, cueIdentity);
        await plan.RequireFreshAsync(token);
        return plan;
    }

    internal byte[] CopyCueBytes() => _outputCue.ToArray();

    internal byte[] BuildReceipt(NativeId65FreshDiscVerification verified)
    {
        RequireVerification(verified);
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 1, profile = ProfileId, planSha256 = PlanSha256,
            sourceImageSha256 = NativeId65FreshDiscSource.OriginalSha256,
            sourceCueSha256 = SourceCueSha256, sourceImagePath = SourceImagePath, sourceCuePath = SourceCuePath,
            discPlanSha256 = _disc.PlanSha256, bodyPlanSha256 = _disc.BodyPlanSha256,
            canonicalDocumentJson = Encoding.UTF8.GetString(_canonical),
            canonicalDocumentSha256 = CanonicalDocumentSha256, projectSaveBindingClaimed = false,
            bodySha256 = Hash(_disc.CopyBody()), overlaySha256 = Hash(_disc.CopyOverlay()),
            executableSha256 = Hash(_disc.CopyExecutable()), wadDirectorySha256 = Hash(_disc.CopyWadDirectory()),
            rootDirectorySha256 = Hash(_disc.CopyIsoRootDirectory()), cueSha256 = Hash(_outputCue),
            imageSha256 = verified.OutputSha256, imageByteLength = NativeId65FreshDiscSource.ImageByteLength,
            changedRawSectors = verified.ChangedSectors, executableLba = _disc.FinalExecutableLba,
            wadByteLength = _disc.FinalWadByteLength, bodyByteLength = 0x1FB800,
            sourceReadbackRequired = true, researchOnly = true,
            runtimeVerified = false, normalCreateBinApproved = false, completeNativeDependencyClosureProven = false,
            checkpointOrForeignStateSupported = false, pendingNativeOwners = _disc.PendingOwners
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    internal async Task RequireFreshAsync(CancellationToken token = default)
    {
        RequireCueFresh(token);
        using FileStream source = new(SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await NativeId65FreshDiscSource.RequireWholeOriginalAsync(source, token);
        RequireCueFresh(token);
    }

    internal void WriteNewImage(string path, CancellationToken token, Action firstSector)
    {
        token.ThrowIfCancellationRequested();
        RequireCueFresh(token);
        using FileStream source = new(SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (source.Length != NativeId65FreshDiscSource.ImageByteLength)
            throw new InvalidDataException("Edited fresh LAB original image length changed before writing.");
        using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var originalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var outputHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] before = new byte[2352];
        int changed = 0;
        for (int lba = 0; lba < NativeId65FreshDiscSource.SectorCount; lba++)
        {
            token.ThrowIfCancellationRequested();
            source.ReadExactly(before);
            // Authenticate every sector BEFORE either changed or untouched output.
            byte[] after = _disc.CreateExpectedRawSector(lba, before);
            originalHash.AppendData(before);
            outputHash.AppendData(after);
            if (!before.AsSpan().SequenceEqual(after)) changed++;
            output.Write(after);
            if (lba == 0) firstSector();
        }
        NativeId65FreshDiscSource.RequireActualEof(source, token);
        if (Convert.ToHexStringLower(originalHash.GetHashAndReset()) != NativeId65FreshDiscSource.OriginalSha256 ||
            output.Position != NativeId65FreshDiscSource.ImageByteLength || output.Length != output.Position)
            throw new InvalidDataException("Edited fresh LAB streaming original/output owner changed.");
        RequireVerification(new(Convert.ToHexStringLower(outputHash.GetHashAndReset()), changed));
        output.Flush(flushToDisk: true);
        token.ThrowIfCancellationRequested();
        RequireCueFresh(token);
    }

    internal NativeId65FreshDiscVerification VerifyImage(string path, CancellationToken token)
    {
        using FileStream source = new(SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using FileStream output = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        NativeId65FreshDiscVerification result;
        try { result = _disc.VerifyImage(source, output, token); }
        catch (InvalidDataException error)
        { throw new InvalidDataException("Edited fresh LAB physical readback failed: " + error.Message, error); }
        RequireVerification(result);
        return result;
    }

    private static void RequireVerification(NativeId65FreshDiscVerification verified)
    {
        ArgumentNullException.ThrowIfNull(verified);
        if (verified.OutputSha256 != ImageHash || verified.ChangedSectors != 1253)
            throw new InvalidDataException("Edited fresh LAB research readback must equal the independently known full image and 1253 changed sectors.");
    }

    private static void ValidateSourceCue(ReadOnlySpan<byte> bytes, string cuePath, string originalBin)
    {
        // Reuse only the legacy helper's strict bounded snapshot validation.
        // Its returned starter filename is intentionally discarded, never emitted.
        _ = NativeId65FreshDiscResearchCandidatePlan.CompileCueForSmoke(bytes, cuePath, originalBin);
    }

    private static byte[] ReadCueFile(string path, CancellationToken token)
    {
        using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return NativeId65FreshDiscResearchCandidatePlan.ReadCueSnapshot(input, token);
    }

    private void RequireCueFresh(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_cueIdentity != PathIdentity(SourceCuePath) || _imageIdentity != PathIdentity(SourceImagePath))
            throw new InvalidDataException("Edited fresh LAB source CUE changed exact bytes, path identity or data-image binding.");
        byte[] current = ReadCueFile(SourceCuePath, token);
        if (!_sourceCue.AsSpan().SequenceEqual(current))
            throw new InvalidDataException("Edited fresh LAB source CUE changed exact bytes, path identity or data-image binding.");
        // FILE may name a third alias distinct from the two supplied paths.
        // Rebind that relation from this same authenticated bounded snapshot;
        // do not reread the filesystem CUE through the legacy parser.
        ValidateSourceCue(current, SourceCuePath, SourceImagePath);
        if (_cueIdentity != PathIdentity(SourceCuePath) || _imageIdentity != PathIdentity(SourceImagePath))
            throw new InvalidDataException("Edited fresh LAB source CUE path changed during its binding check.");
        token.ThrowIfCancellationRequested();
    }

    private static string PathIdentity(string path)
    {
        FileInfo file = new(path);
        if (!file.Exists || Directory.Exists(path))
            throw new InvalidDataException("Edited fresh LAB source CUE/image path is missing or not a file.");
        List<string> identity = [file.FullName, file.LinkTarget ?? ""];
        FileSystemInfo resolved = file.ResolveLinkTarget(true) ?? file;
        identity.Add(resolved.FullName);
        foreach (string leaf in new[] { file.FullName, resolved.FullName })
        {
            DirectoryInfo? parent = new FileInfo(leaf).Directory;
            for (int depth = 0; parent != null; parent = parent.Parent, depth++)
            {
                if (depth > 64) throw new InvalidDataException("Edited fresh LAB source path depth exceeds its bound.");
                identity.Add(parent.FullName + "|" + parent.LinkTarget);
            }
        }
        return string.Join('\n', identity);
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

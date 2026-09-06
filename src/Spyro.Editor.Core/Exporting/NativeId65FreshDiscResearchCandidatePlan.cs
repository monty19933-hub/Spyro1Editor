using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65FreshDiscResearchSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>One exact source-issued untextured A/N16 LAB/track19 research
/// afterimage. Filesystem readback is not native runtime or normal export approval.</summary>
public sealed class NativeId65FreshDiscResearchCandidatePlan
{
    public const string ProfileId = "native-id65-fresh-LAB-track19-research-r01";
    internal const string ImageName = "native-id65-fresh-lab-track19-r01.bin";
    internal const string ReceiptName = "native-id65-fresh-lab-track19-r01.receipt.json";
    internal const string CueName = "native-id65-fresh-lab-track19-r01.cue";
    internal static string[] OutputNames => [ImageName, ReceiptName, CueName];
    private const string ImageHash = "1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188";
    private readonly NativeId65FreshDiscPlan _disc;
    private readonly byte[] _sourceCue;
    private readonly byte[] _outputCue;
    private readonly string _cueIdentity;
    private readonly string _imageIdentity;

    private NativeId65FreshDiscResearchCandidatePlan(string image, string cue, NativeId65FreshDiscPlan disc,
        byte[] sourceCue, byte[] outputCue, string imageIdentity, string cueIdentity)
    {
        SourceImagePath = image;
        SourceCuePath = cue;
        _disc = disc;
        _sourceCue = sourceCue;
        _outputCue = outputCue;
        _imageIdentity = imageIdentity;
        _cueIdentity = cueIdentity;
        SourceCueSha256 = Hash(sourceCue);
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = ProfileId, source = NativeId65FreshDiscSource.OriginalSha256,
            SourceImagePath, SourceCuePath, SourceCueSha256, imageIdentity, cueIdentity,
            discPlan = disc.PlanSha256, bodyPlan = disc.BodyPlanSha256,
            expectedImage = ImageHash, changedSectors = 1253
        }));
    }

    public string SourceImagePath { get; }
    public string SourceCuePath { get; }
    public string SourceCueSha256 { get; }
    public string PlanSha256 { get; }
    public string ExpectedImageSha256 => ImageHash;
    public bool RuntimeVerified => false;
    public bool NormalCreateBinApproved => false;
    public bool CompleteNativeDependencyClosureProven => false;

    public static async Task<NativeId65FreshDiscResearchCandidatePlan> CaptureAsync(string originalBin,
        string sourceCue, NativeId65FreshDiscPlan issuedDisc, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(issuedDisc);
        token.ThrowIfCancellationRequested();
        // The private disc issuer requires its code-owned untextured revision.
        // Complete hashes narrow it further; equal CD length alone is not authority.
        if (!issuedDisc.SourceCertified || issuedDisc.Executable.SelectedBuiltInTrack != 19 ||
            issuedDisc.FinalExecutableLba != 54921 || issuedDisc.FinalWadByteLength != 0x6B32000 ||
            Hash(issuedDisc.CopyBody()) != "b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c" ||
            Hash(issuedDisc.CopyOverlay()) != "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5" ||
            Hash(issuedDisc.CopyExecutable()) != "3473dc92e087c87858434125d5fb1ec879450682e3be64c508de6805029ce7a2" ||
            Hash(issuedDisc.CopyWadDirectory()) != "0a227cbf0d9f6b269ae7d41ce610129b0b48590519bbaaa2eac375e8818e23e0" ||
            Hash(issuedDisc.CopyIsoRootDirectory()) != "2412be680f008e5ebbf19e64948e84ad1d5eeadf07ab1c858c825c6608631089")
            throw new InvalidDataException("Fresh LAB research requires the exact privately issued untextured A/N16/track19 fixture.");
        string image = Path.GetFullPath(originalBin), cue = Path.GetFullPath(sourceCue);
        string imageIdentity = PathIdentity(image), cueIdentity = PathIdentity(cue);
        byte[] bytes = ReadCueFile(cue, token);
        byte[] output = CompileCueForSmoke(bytes, cue, image);
        var plan = new NativeId65FreshDiscResearchCandidatePlan(image, cue, issuedDisc, bytes, output, imageIdentity, cueIdentity);
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
            throw new InvalidDataException("Fresh LAB original image length changed before writing.");
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
            throw new InvalidDataException("Fresh LAB streaming original/output owner changed.");
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
        { throw new InvalidDataException("Fresh LAB physical readback failed: " + error.Message, error); }
        RequireVerification(result);
        return result;
    }

    private static void RequireVerification(NativeId65FreshDiscVerification verified)
    {
        ArgumentNullException.ThrowIfNull(verified);
        if (verified.OutputSha256 != ImageHash || verified.ChangedSectors != 1253)
            throw new InvalidDataException("Fresh LAB research readback must equal the independently known full image and 1253 changed sectors.");
    }

    // Shared by actual capture and every freshness check; neutral bytes cannot issue a plan.
    internal static byte[] ReadCueSnapshot(Stream input, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(input);
        token.ThrowIfCancellationRequested();
        if (!input.CanRead || !input.CanSeek || input.Position != 0)
            throw new InvalidDataException("Fresh LAB source CUE length/readable owner is outside its bound.");
        long declaredLength = input.Length;
        if (declaredLength is < 1 or > 65536)
            throw new InvalidDataException("Fresh LAB source CUE length/readable owner is outside its bound.");
        int length = (int)declaredLength;
        byte[] bytes = new byte[length];
        input.ReadExactly(bytes);
        token.ThrowIfCancellationRequested();
        if (input.Position != length || input.Length != length || input.ReadByte() != -1 ||
            input.Position != length || input.Length != length)
            throw new InvalidDataException("Fresh LAB source CUE length, position or actual EOF changed.");
        token.ThrowIfCancellationRequested();
        return bytes;
    }

    internal static byte[] CompileCueForSmoke(ReadOnlySpan<byte> bytes, string cuePath, string originalBin)
    {
        if (bytes.Length is < 1 or > 65536)
            throw new InvalidDataException("Fresh LAB source CUE exceeds its bounded owner.");
        // Strict UTF8 rejects UTF16/32 BOMs. Strip only UTF8 BOM; StringReader
        // preserves the existing ReadAllLines CR/LF splitting semantics.
        if (bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) bytes = bytes[3..];
        string text = DiscCueSheet.TextEncoding.GetString(bytes);
        using var reader = new StringReader(text);
        List<string> lines = [];
        while (reader.ReadLine() is string line)
            if (!string.IsNullOrWhiteSpace(line)) lines.Add(line.Trim());
        if (lines.Count != 3 || !Regex.IsMatch(lines[0], "^FILE \"[^\"\\r\\n]+\" BINARY$", RegexOptions.IgnoreCase) ||
            !lines[1].Equals("TRACK 01 MODE2/2352", StringComparison.OrdinalIgnoreCase) ||
            !lines[2].Equals("INDEX 01 00:00:00", StringComparison.OrdinalIgnoreCase) ||
            !DiscCueSheet.ReferencesDataImageSnapshot(bytes, cuePath, originalBin))
            throw new InvalidDataException("Fresh LAB source CUE requires its exact single source-bound MODE2/2352 track at zero.");
        return DiscCueSheet.TextEncoding.GetBytes($"FILE \"{ImageName}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
    }

    private static byte[] ReadCueFile(string path, CancellationToken token)
    {
        using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadCueSnapshot(input, token);
    }

    private void RequireCueFresh(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_cueIdentity != PathIdentity(SourceCuePath) || _imageIdentity != PathIdentity(SourceImagePath))
            throw new InvalidDataException("Fresh LAB source CUE changed exact bytes, path identity or data-image binding.");
        byte[] current = ReadCueFile(SourceCuePath, token);
        if (!_sourceCue.AsSpan().SequenceEqual(current))
            throw new InvalidDataException("Fresh LAB source CUE changed exact bytes, path identity or data-image binding.");
        // FILE may name a third alias distinct from the two supplied paths.
        // Rebind that relation from this same authenticated bounded snapshot;
        // do not reread the filesystem CUE through the legacy parser.
        _ = CompileCueForSmoke(current, SourceCuePath, SourceImagePath);
        if (_cueIdentity != PathIdentity(SourceCuePath) || _imageIdentity != PathIdentity(SourceImagePath))
            throw new InvalidDataException("Fresh LAB source CUE path changed during its binding check.");
        token.ThrowIfCancellationRequested();
    }

    private static string PathIdentity(string path)
    {
        FileInfo file = new(path);
        if (!file.Exists || Directory.Exists(path))
            throw new InvalidDataException("Fresh LAB source CUE/image path is missing or not a file.");
        List<string> identity = [file.FullName, file.LinkTarget ?? ""];
        FileSystemInfo resolved = file.ResolveLinkTarget(true) ?? file;
        identity.Add(resolved.FullName);
        foreach (string leaf in new[] { file.FullName, resolved.FullName })
        {
            DirectoryInfo? parent = new FileInfo(leaf).Directory;
            for (int depth = 0; parent != null; parent = parent.Parent, depth++)
            {
                if (depth > 64) throw new InvalidDataException("Fresh LAB source path depth exceeds its bound.");
                identity.Add(parent.FullName + "|" + parent.LinkTarget);
            }
        }
        return string.Join('\n', identity);
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

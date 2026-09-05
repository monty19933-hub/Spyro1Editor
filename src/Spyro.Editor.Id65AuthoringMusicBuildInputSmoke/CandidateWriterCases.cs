using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

internal static class CandidateWriterCases
{
    public static async Task RunAsync(string repositoryRoot)
    {
        string source = Path.Combine(repositoryRoot, "_local", "id65-authoring-aggregate-workspace", "_research", "unusedlevel65blank", "v1", "locked-base", "unusedlevel65blank-locked-base-v1.bin");
        string sourceCue = Path.ChangeExtension(source, ".cue");
        string sourceHash = HashFile(source), cueHash = HashFile(sourceCue);
        string root = Path.Combine(repositoryRoot, "_local", "Spyro.Editor.Id65AuthoringMusicCandidateWriterSmoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Console.WriteLine("Retained music candidate fixture: " + root);
        UnusedLevel65BlankLevelLabWorkspacePaths paths = UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(Path.Combine(root, "workspace"));
        Directory.CreateDirectory(paths.LockedBaseDirectoryPath);
        Directory.CreateDirectory(paths.AuthoredEditsDirectoryPath);
        File.Copy(source, paths.LockedBaseImagePath, overwrite: false);
        await File.WriteAllTextAsync(paths.LockedBaseCuePath, DiscImage.BuildCueText(sourceCue, Path.GetFileName(paths.LockedBaseImagePath)), Encoding.ASCII);
        UnusedLevel65BlankLevelLabManifest manifest = UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(paths);
        await File.WriteAllTextAsync(paths.ManifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true,
            WriteIndented = true, Converters = { new JsonStringEnumConverter() }
        }) + "\n", new UTF8Encoding(false));
        Id65AuthoringEditorDocument document = Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
            Id65RemotePadMobyKind.GrassT107, Id65RemotePadGrassT107Catalog.CenterPositionId, [Id65AuthoringTerrainSetCatalog.Tile1Id]);
        Id65AuthoringEditorDocumentStoreResult stored = await Id65AuthoringEditorDocumentStore.SaveAsync(paths, manifest, document, new(null), null);
        string documentPath = Id65AuthoringEditorDocumentStore.GetPath(paths);
        byte[] documentBefore = File.ReadAllBytes(documentPath);
        Id65AuthoringMusicBuildInput input = Id65AuthoringMusicBuildInput.Create(document, stored.EnvelopeCanonicalSha256, 2);
        Id65AuthoringMusicCandidateRequest request = new(paths, manifest, Id65AuthoringMusicBuildInput.Parse(input.SerializeUtf8()));
        int rejected = 0;
        using (CancellationTokenSource canceled = new())
        {
            canceled.Cancel();
            await RejectAsync(() => Id65AuthoringMusicCandidateWriter.MaterializeAsync(request, canceled.Token));
            Require(!Directory.Exists(Path.Combine(paths.RootPath, Id65AuthoringMusicCandidateWriter.RootDirectoryName)), "A pre-canceled build created candidate output.");
        }
        await RejectAsync(() => Id65AuthoringMusicCandidateWriter.MaterializeForSmokeAsync(request, Id65AuthoringMusicCandidateFaultPoint.AfterMusicWrite));
        string outputRoot = Path.Combine(paths.RootPath, Id65AuthoringMusicCandidateWriter.RootDirectoryName);
        Require(Directory.GetFiles(outputRoot, "*", SearchOption.AllDirectories).Length == 0,
            "An upstream music-write failure left a usable partial candidate or discarded its lease/stage cleanup.");
        string baselineRoot = Path.Combine(paths.RootPath, "materialized-blank-cyclorama-schema6-candidates");
        Dictionary<string, string> baselineBefore = Directory.GetFiles(baselineRoot, "*", SearchOption.AllDirectories).ToDictionary(path => path, HashFile);
        string? competingCue = null;
        await RejectAsync(() => Id65AuthoringMusicCandidateWriter.MaterializeForSmokeAsync(request,
            Id65AuthoringMusicCandidateFaultPoint.None, output =>
            {
                competingCue = Path.Combine(output, Id65AuthoringMusicCandidateWriter.CueFileName);
                File.WriteAllText(competingCue, "competing candidate CUE must survive untouched");
            }));
        Require(competingCue is not null && File.ReadAllText(competingCue) == "competing candidate CUE must survive untouched" &&
            Directory.GetFiles(outputRoot, "*", SearchOption.AllDirectories).SequenceEqual([competingCue]),
            "A competing prepublication artifact was overwritten or partial final files were published.");
        File.Delete(competingCue!); // Only the exact competing file created by this fixture.

        Id65AuthoringMusicCandidateResult result = await Id65AuthoringMusicCandidateWriter.MaterializeAsync(request);
        Require(result.PublishedThisCall && !result.ReusedExistingCandidate && result.RetainedRecoveryDirectory.Length == 0 &&
            result.Receipt.SelectedTrackId == 2 && result.Receipt.OutputImageSha256 == "223a69b9e041a12a23b5207fe85d8eba61f3a57c3ea84b82a8d2fe4805f086c9" &&
            !result.Receipt.RuntimeProven && !result.Receipt.NormalCreateBinEnabled && !result.Receipt.CrashAtomicPublication && result.Receipt.ExceptionRollbackPublication,
            "The published music candidate lost exact output identity or overstated its acceptance/publication boundary.");
        string[] files = Directory.GetFiles(result.CandidateDirectoryPath);
        Require(files.Length == 4 && Id65AuthoringMusicBuildInput.Parse(File.ReadAllBytes(result.CandidateInputPath)).CanonicalSha256 == input.CanonicalSha256,
            "The immutable candidate did not include exactly BIN/input/receipt/CUE with canonical authored intent.");
        Dictionary<string, (string Hash, DateTime Modified)> snapshots = files.ToDictionary(path => path, path => (HashFile(path), File.GetLastWriteTimeUtc(path)));
        Id65AuthoringMusicCandidateResult reused = await Id65AuthoringMusicCandidateWriter.MaterializeAsync(request);
        Require(reused.ReusedExistingCandidate && !reused.PublishedThisCall && reused.Receipt == result.Receipt &&
            snapshots.All(pair => HashFile(pair.Key) == pair.Value.Hash && File.GetLastWriteTimeUtc(pair.Key) == pair.Value.Modified),
            "An idempotent music candidate reuse changed published artifacts or receipt identity.");
        byte[] receiptBytes = File.ReadAllBytes(result.CandidateReceiptPath);
        try
        {
            byte[] changed = receiptBytes.ToArray(); changed[0] ^= 1;
            await File.WriteAllBytesAsync(result.CandidateReceiptPath, changed);
            await RejectAsync(() => Id65AuthoringMusicCandidateWriter.MaterializeAsync(request));
            Require(File.ReadAllBytes(result.CandidateReceiptPath).SequenceEqual(changed), "Conflicting existing receipt was overwritten.");
        }
        finally { await File.WriteAllBytesAsync(result.CandidateReceiptPath, receiptBytes); }
        Id65AuthoringMusicBuildInput stale = Id65AuthoringMusicBuildInput.Create(document, new string('b', 64), 2);
        await RejectAsync(() => Id65AuthoringMusicCandidateWriter.MaterializeAsync(request with { Input = stale }));
        await RunPostPublicationAsync(request);
        Require(baselineBefore.All(pair => HashFile(pair.Key) == pair.Value) &&
            File.ReadAllBytes(documentPath).SequenceEqual(documentBefore) &&
            HashFile(paths.LockedBaseImagePath) == sourceHash && HashFile(source) == sourceHash && HashFile(sourceCue) == cueHash,
            "The candidate writer changed its immutable baseline candidate, canonical document or original source.");
        Console.WriteLine($"ID65_AUTHORING_MUSIC_CANDIDATE_WRITER_SMOKE_OK rejected={rejected} published=1 exactReuse=1 canonicalInputIncluded=1 partialConflictPreserved=1 oldBaselinePreserved=1 sharedPublishNew=1 runtimeAcceptance=0");
        Console.WriteLine("Retained verified music CUE: " + result.CandidateCuePath);
        Console.WriteLine("Retained verified music BIN SHA256: " + result.Receipt.OutputImageSha256);

        async Task RejectAsync(Func<Task> work)
        {
            try { await work(); }
            catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or OperationCanceledException or IOException)
            {
                rejected++; return;
            }
            throw new Exception("An invalid/faulted music candidate build unexpectedly succeeded.");
        }
    }

    internal static async Task RunPostPublicationAsync(Id65AuthoringMusicCandidateRequest baselineRequest)
    {
        // A second immutable track candidate exercises the outcome after the
        // shared transaction commits. Never disturb the first published set.
        Dictionary<string, string> preserved = Directory.GetFiles(
            Path.Combine(baselineRequest.Paths.RootPath, Id65AuthoringMusicCandidateWriter.RootDirectoryName),
            "*", SearchOption.AllDirectories).ToDictionary(path => path, HashFile);
        Id65AuthoringMusicBuildInput input = Id65AuthoringMusicBuildInput.Create(
            baselineRequest.Input.BaselineDocument, baselineRequest.Input.BaselineEnvelopeCanonicalSha256, 26);
        Id65AuthoringMusicCandidateRequest request = baselineRequest with { Input = input };
        string? directory = null;
        bool correctlyReported = false;
        try
        {
            await Id65AuthoringMusicCandidateWriter.MaterializeForSmokeAsync(request,
                Id65AuthoringMusicCandidateFaultPoint.AfterPublication, output => directory = output);
        }
        catch (IOException exception) when (
            exception.Message.StartsWith("Music candidate files were published at ", StringComparison.Ordinal) &&
            exception.Message.Contains("do not treat the candidate as verified", StringComparison.Ordinal))
        {
            correctlyReported = true;
        }
        Require(correctlyReported && directory is not null && Directory.GetFiles(directory).Length == 4,
            "A postpublication failure did not honestly report and retain the complete committed candidate.");
        Dictionary<string, (string Hash, DateTime Modified)> snapshots = Directory.GetFiles(directory!)
            .ToDictionary(path => path, path => (HashFile(path), File.GetLastWriteTimeUtc(path)));
        Id65AuthoringMusicCandidateResult reused = await Id65AuthoringMusicCandidateWriter.MaterializeAsync(request);
        Require(reused.ReusedExistingCandidate && !reused.PublishedThisCall && reused.RetainedRecoveryDirectory.Length == 0 &&
            reused.CandidateDirectoryPath == directory && reused.Receipt.SelectedTrackId == 26 &&
            reused.Receipt.OutputImageSha256 == "f7b06d9100fa72e57c9659b50298016d3a19cfb726b72d57c4d328908854c370" &&
            !reused.Receipt.RuntimeProven && !reused.Receipt.NormalCreateBinEnabled &&
            snapshots.All(pair => HashFile(pair.Key) == pair.Value.Hash && File.GetLastWriteTimeUtc(pair.Key) == pair.Value.Modified) &&
            preserved.All(pair => HashFile(pair.Key) == pair.Value),
            "Strict reuse failed to verify the postpublication candidate without rewriting it or earlier candidates.");
        Console.WriteLine("ID65_AUTHORING_MUSIC_POSTPUBLICATION_SMOKE_OK publishedFailureReported=1 committedSetRetained=1 exactReuse=1 earlierCandidatePreserved=1 runtimeAcceptance=0");
        Console.WriteLine("Retained postpublication verified music CUE: " + reused.CandidateCuePath);
        Console.WriteLine("Retained postpublication verified music BIN SHA256: " + reused.Receipt.OutputImageSha256);
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

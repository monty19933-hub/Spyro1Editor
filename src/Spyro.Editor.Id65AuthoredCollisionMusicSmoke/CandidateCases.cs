using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

internal static class CandidateCases
{
    public static async Task RunAsync(string repository)
    {
        string source = Path.Combine(repository, "_local", "id65-authoring-aggregate-workspace", "_research", "unusedlevel65blank", "v1", "locked-base", "unusedlevel65blank-locked-base-v1.bin");
        string cueSource = Path.ChangeExtension(source, ".cue");
        if (!File.Exists(source) || !File.Exists(cueSource)) throw new InvalidOperationException("The exact existing source fixture is required.");
        string sourceHash = HashFile(source), cueHash = HashFile(cueSource);
        string root = Path.Combine(repository, "_local", "Id65AuthoredCollisionMusicSmoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Console.WriteLine("Retained authored composition fixture: " + root);
        var paths = UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(Path.Combine(root, "workspace"));
        Directory.CreateDirectory(paths.LockedBaseDirectoryPath); Directory.CreateDirectory(paths.AuthoredEditsDirectoryPath);
        File.Copy(source, paths.LockedBaseImagePath, overwrite: false);
        await File.WriteAllTextAsync(paths.LockedBaseCuePath, DiscImage.BuildCueText(cueSource, Path.GetFileName(paths.LockedBaseImagePath)), Encoding.ASCII);
        var manifest = UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(paths);
        await File.WriteAllTextAsync(paths.ManifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, Converters = { new JsonStringEnumConverter() }
        }) + "\n", new UTF8Encoding(false));
        int checks = 0, rejections = 0, states = 0;
        Id65AuthoringEditorDocumentStoreResult? stored = null;
        var docA = Document(Id65RemotePadMobyKind.GrassT107, Id65RemotePadGrassT107Catalog.CenterPositionId, Id65AuthoringTerrainSetCatalog.Tile1Id);
        var requestA = await Save(docA, 2);
        var planA = await Capture(requestA, 1, "39e53a1f21c4c75346a670c4ffea26cdab95498fc67b9e798284ba7dd8adfba2");
        var repeatA = await Id65AuthoredCollisionMusicPublisher.CaptureAsync(requestA);
        Require(planA.DeterministicPlanSha256 == repeatA.DeterministicPlanSha256 && planA.CopyOutputRow80().SequenceEqual(repeatA.CopyOutputRow80()) &&
            planA.CopyOutputExecutable().SequenceEqual(repeatA.CopyOutputExecutable()), "independent repeated compile after exact combined-input reload");

        // A handful of new source-bound transaction checks. They use only this
        // disposable workspace; no old candidate is a staging input or target.
        foreach (var fault in new[] { Id65AuthoredCollisionMusicFault.AfterCopy, Id65AuthoredCollisionMusicFault.AfterWrite,
            Id65AuthoredCollisionMusicFault.AfterReadback, Id65AuthoredCollisionMusicFault.BeforePublication })
        {
            Console.WriteLine("Checking source-bound publication fault: " + fault);
            await RejectAsync(() => Id65AuthoredCollisionMusicPublisher.PublishForSmokeAsync(requestA, fault), typeof(InvalidOperationException));
            RequireNoPublishedA();
        }
        string? competitor = null;
        await RejectAsync(() => Id65AuthoredCollisionMusicPublisher.PublishForSmokeAsync(requestA, Id65AuthoredCollisionMusicFault.None, output =>
        {
            competitor = Path.Combine(output, "competitor.txt"); File.WriteAllText(competitor, "competing-output-preserved", new UTF8Encoding(false));
        }), typeof(InvalidDataException));
        Require(competitor is not null && File.ReadAllText(competitor) == "competing-output-preserved", "competing output survives refused publication");
        File.Delete(competitor!); // Exact test-created sentinel, never a user artifact.
        RequireNoPublishedA();
        Console.WriteLine("Checking canonical document change before commit.");
        await RejectAsync(() => Id65AuthoredCollisionMusicPublisher.PublishForSmokeAsync(requestA, Id65AuthoredCollisionMusicFault.None, _ =>
        {
            stored = Id65AuthoringEditorDocumentStore.SaveAsync(paths, manifest,
                Document(Id65RemotePadMobyKind.None, Id65RemotePadGrassT107Catalog.CenterPositionId),
                new(null), stored!.EnvelopeCanonicalSha256).GetAwaiter().GetResult();
        }), typeof(InvalidDataException));
        RequireNoPublishedA();
        var restoredBeforePublish = await Save(docA, 2);
        Require(restoredBeforePublish.Input.SerializeUtf8().SequenceEqual(requestA.Input.SerializeUtf8()),
            "restoring the canonical document after refused publication restores exact combined intent");
        await RejectAsync(() => Id65AuthoredCollisionMusicPublisher.PublishAsync(requestA, new CancellationToken(canceled: true)), typeof(OperationCanceledException));

        var resultA = await Id65AuthoredCollisionMusicPublisher.PublishAsync(requestA);
        Require(!resultA.Reused && resultA.Readback.SourceImageSha256 == sourceHash, "first candidate is rebuilt from locked source");
        string candidateAHash = HashFile(resultA.Image);
        Require(candidateAHash == resultA.Readback.OutputImageSha256, "first complete published image hash");
        Require(Id65AuthoredCollisionMusicInput.Parse(File.ReadAllBytes(resultA.Input)).CanonicalSha256 == requestA.Input.CanonicalSha256,
            "published complete authored input reload");
        Console.WriteLine("Candidate A: " + resultA.Cue + " sha256=" + candidateAHash);
        Corruptions(planA, resultA.Image);

        string alternate = Id65RemotePadGrassT107Catalog.Positions.First(p => p.PositionId != Id65RemotePadGrassT107Catalog.CenterPositionId).PositionId;
        var requestB = await Save(Document(Id65RemotePadMobyKind.GrassT107, alternate,
            Id65AuthoringTerrainSetCatalog.Tile1Id, Id65AuthoringTerrainSetCatalog.Tile2Id), 26);
        var planB = await Capture(requestB, 2, "30d7721b6b46b9753827ddee249bc2591dc9fc1b5a76db71a4ebffe146cc9f8c");
        await RejectAsync(() => Id65AuthoredCollisionMusicPublisher.PublishAsync(requestA), typeof(InvalidDataException));
        Require(planA.OutputRow80Sha256 != planB.OutputRow80Sha256 && planA.OutputExecutableSha256 != planB.OutputExecutableSha256,
            "terrain plus music change both exact native domains");
        await RejectAsync(() => Id65AuthoredCollisionMusicPublisher.PublishForSmokeAsync(requestB, Id65AuthoredCollisionMusicFault.AfterPublication), typeof(IOException));
        var resultB = await Id65AuthoredCollisionMusicPublisher.PublishAsync(requestB);
        Require(resultB.Reused && resultB.Readback.OutputImageSha256 != resultA.Readback.OutputImageSha256,
            "postcommit failure retains committed files for exact verified reuse");
        Console.WriteLine("Candidate B: " + resultB.Cue + " sha256=" + resultB.Readback.OutputImageSha256);

        var requestC = await Save(Document(Id65RemotePadMobyKind.None, Id65RemotePadGrassT107Catalog.CenterPositionId), 2);
        _ = await Capture(requestC, 0, planA.OutputExecutableSha256);
        var requestD = await Save(Document(Id65RemotePadMobyKind.RedGemT21, Id65RemotePadGrassT107Catalog.CenterPositionId,
            Id65AuthoringTerrainSetCatalog.Tile2Id), 26);
        _ = await Capture(requestD, 1, planB.OutputExecutableSha256);
        var restoredA = await Save(docA, 2);
        var restoredPlanA = await Capture(restoredA, 1, planA.OutputExecutableSha256);
        Require(restoredA.Input.SerializeUtf8().SequenceEqual(requestA.Input.SerializeUtf8()) &&
            restoredPlanA.DeterministicPlanSha256 == planA.DeterministicPlanSha256 &&
            restoredPlanA.CopyOutputRow80().SequenceEqual(planA.CopyOutputRow80()), "A-B-C-D-A re-edit returns exact source-bound identity and bytes");
        var reusedA = await Id65AuthoredCollisionMusicPublisher.PublishAsync(restoredA);
        Require(reusedA.Reused && reusedA.Readback == resultA.Readback && HashFile(resultA.Image) == candidateAHash,
            "reverted authored state reuses complete exact A without changing prior artifacts");

        var collapsedRequest = restoredA with { Input = Id65AuthoredCollisionMusicInput.Create(restoredA.Input.Music, Id65AuthoredCollisionMusicSubstrate.ExactCollapsedSky) };
        var collapsed = await Id65AuthoredCollisionMusicPublisher.CaptureAsync(collapsedRequest);
        Require(collapsed.OutputRow80Sha256 == "9e9b8064516684eebbd86012621d20ceab60e39bfa7711ff427bda71da952fb1" &&
            collapsed.OutputExecutableSha256 == planA.OutputExecutableSha256, "exact collapsed-sky output and music leaves preserve independent frozen goldens");
        var incompatible = await Save(Document(Id65RemotePadMobyKind.None, Id65RemotePadGrassT107Catalog.CenterPositionId), 2);
        await RejectAsync(() => Id65AuthoredCollisionMusicPublisher.CaptureAsync(incompatible with
        { Input = Id65AuthoredCollisionMusicInput.Create(incompatible.Input.Music, Id65AuthoredCollisionMusicSubstrate.ExactCollapsedSky) }), typeof(InvalidDataException));
        _ = await Save(docA, 2);
        Require(HashFile(source) == sourceHash && HashFile(cueSource) == cueHash && HashFile(paths.LockedBaseImagePath) == sourceHash,
            "original and workspace source BIN/CUE preserved");
        Require(Directory.EnumerateFileSystemEntries(paths.AuthoredEditsDirectoryPath).Select(Path.GetFileName)
            .All(name => name == Id65AuthoringEditorDocumentStore.FileName || name == Id65AuthoringEditorDocumentStore.WriterLeaseFileName),
            "only the existing canonical document and persistent writer lease remain; no loose music sidecar");
        await File.WriteAllTextAsync(Path.Combine(root, "evidence.json"), JsonSerializer.Serialize(new
        {
            sourceHash, checks, rejections, states, candidateA = resultA, candidateB = resultB,
            exactCollapsedRow80 = collapsed.OutputRow80Sha256, runtimeProven = false, originalCandidatesUntouched = true,
            editableProjectMusicPersistence = false, completeCanonicalInputRoundtrip = true
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Console.WriteLine($"ID65_AUTHORED_COLLISION_MUSIC_CANDIDATES_OK checks={checks} rejected={rejections} states={states} " +
            "retainedCandidates=2 exactReEdit=1 rebuiltFromLockedSource=1 collisionAndMusic=1 physicalComplement=1 rollback=1 runtime=false");

        async Task<Id65AuthoredCollisionMusicRequest> Save(Id65AuthoringEditorDocument document, int track)
        {
            stored = await Id65AuthoringEditorDocumentStore.SaveAsync(paths, manifest, document, new(null), stored?.EnvelopeCanonicalSha256);
            var input = Id65AuthoredCollisionMusicInput.Create(Id65AuthoringMusicBuildInput.Create(stored.Document, stored.EnvelopeCanonicalSha256, track));
            // One complete in-memory serialized record mirrors the immutable
            // published authored input; it is never a mutable project sidecar.
            return new(paths, manifest, Id65AuthoredCollisionMusicInput.Parse(input.SerializeUtf8()));
        }
        async Task<Id65AuthoredCollisionMusicPlan> Capture(Id65AuthoredCollisionMusicRequest request, int tiles, string exeHash)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Console.WriteLine($"Compiling authored state {states + 1}: tiles={tiles} moby={request.Input.Music.BaselineDocument.RemotePadMobyKind} track={request.Input.Music.SelectedTrackId}.");
            var plan = await Id65AuthoredCollisionMusicPublisher.CaptureAsync(request);
            Require(plan.Collision.SourceOccupiedCells == 4253 && plan.Collision.OutputOccupiedCells == 1 &&
                plan.Collision.OutputReachableTriangles == tiles + 1 && plan.Collision.TriangleCount == 19808 + tiles,
                "authored native collision census survives composed terrain state");
            Require(plan.OutputExecutableSha256 == exeHash, "unchanged independent music compiler executable golden");
            plan.RequireLogicalReadback(plan.CopyOutputRow80(), plan.CopyOutputExecutable()); checks++;
            byte[] wrongRow = plan.CopyOutputRow80(); wrongRow[0] ^= 1;
            Reject(() => plan.RequireLogicalReadback(wrongRow, plan.CopyOutputExecutable()));
            byte[] wrongExe = plan.CopyOutputExecutable(); wrongExe[^1] ^= 1;
            Reject(() => plan.RequireLogicalReadback(plan.CopyOutputRow80(), wrongExe));
            states++;
            Console.WriteLine($"Compiled authored state {states}: plan={plan.DeterministicPlanSha256} seconds={timer.Elapsed.TotalSeconds:F2}.");
            return plan;
        }
        void RequireNoPublishedA()
        {
            string output = Path.Combine(paths.RootPath, Id65AuthoredCollisionMusicPublisher.RootName, "composition-" + planA.DeterministicPlanSha256);
            Require(!File.Exists(Path.Combine(output, Id65AuthoredCollisionMusicPublisher.ImageName)) &&
                !File.Exists(Path.Combine(output, Id65AuthoredCollisionMusicPublisher.CueName)), "precommit failure has no published image or CUE");
        }
        void Corruptions(Id65AuthoredCollisionMusicPlan plan, string image)
        {
            long wadRaw = (long)plan.AffectedRawSectors.First() * 2352;
            long exeRaw = (long)plan.AffectedRawSectors.First(lba => lba >= Id65AuthoringMusicDiscPlan.ExecutableLba) * 2352;
            foreach (long offset in new[] { 0L, wadRaw, wadRaw + 20, wadRaw + 24, wadRaw + 2072, exeRaw + 24, exeRaw + 2072 })
            {
                using FileStream sourceStream = File.OpenRead(paths.LockedBaseImagePath), output = File.OpenRead(image);
                using View corrupt = new(output, flip: offset);
                Reject(() => plan.Verify(sourceStream, corrupt));
            }
            using (FileStream sourceStream = File.OpenRead(paths.LockedBaseImagePath))
            using (FileStream output = File.OpenRead(image))
            using (View left = new(sourceStream, flip: 0))
            using (View right = new(output, flip: 0))
                Reject(() => plan.Verify(left, right));
            foreach (bool sourceSide in new[] { false, true })
            foreach (bool late in new[] { false, true })
            {
                using FileStream sourceStream = File.OpenRead(paths.LockedBaseImagePath), output = File.OpenRead(image);
                using View changedLength = new(sourceSide ? sourceStream : output, lengthDelta: 1, late: late);
                Reject(() => plan.Verify(sourceSide ? changedLength : sourceStream, sourceSide ? output : changedLength));
            }
            using (FileStream sourceStream = File.OpenRead(paths.LockedBaseImagePath))
            using (FileStream output = File.OpenRead(image))
                Reject(() => plan.Verify(sourceStream, output, new CancellationToken(true)), typeof(OperationCanceledException));
            using (FileStream sourceStream = File.OpenRead(paths.LockedBaseImagePath))
            using (MemoryStream tiny = new([1])) Reject(() => plan.Verify(sourceStream, tiny));
            using (FileStream sourceStream = File.OpenRead(paths.LockedBaseImagePath))
            using (FileStream output = File.OpenRead(image))
                Require(plan.Verify(sourceStream, output).OutputImageSha256 == candidateAHash, "read-only corruption views preserve exact published output");
        }
        void Require(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); checks++; }
        void Reject(Action action, Type? expected = null)
        {
            try { action(); }
            catch (Exception exception) when (expected?.IsInstanceOfType(exception) ?? exception is InvalidDataException) { rejections++; return; }
            throw new InvalidOperationException("Expected composition rejection did not occur.");
        }
        async Task RejectAsync(Func<Task> action, Type expected)
        {
            try { await action(); }
            catch (Exception exception) when (expected.IsInstanceOfType(exception)) { rejections++; return; }
            throw new InvalidOperationException("Expected publication rejection did not occur.");
        }
    }
    private static Id65AuthoringEditorDocument Document(Id65RemotePadMobyKind kind, string position, params string[] tiles) =>
        Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(kind, position, tiles);
    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
    private sealed class View(Stream inner, long? flip = null, int lengthDelta = 0, bool late = false) : Stream
    {
        private bool _read;
        public override bool CanRead => true; public override bool CanSeek => true; public override bool CanWrite => false;
        public override long Length => inner.Length + ((!late || _read) ? lengthDelta : 0);
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            long start = inner.Position; int count = inner.Read(buffer); _read = true;
            if (flip is long value && value >= start && value < start + count) buffer[(int)(value - start)] ^= 1;
            return count;
        }
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

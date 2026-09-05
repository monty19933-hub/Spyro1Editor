using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

internal static class NativeGreenWizardCandidateDiscCases
{
    public static void Run(string imagePath)
    {
        NativeGreenWizardCandidateSource source = NativeGreenWizardCandidateSource.Inspect(imagePath);
        NativeGreenWizardCandidateDiscPlan batch = NativeGreenWizardCandidateDiscPlan.Create(
            NativeGreenWizardPropertiesCandidateCompiler.Compile(source, new("wizardpeak", 6, "wizardpeak", [17, 24])));
        Dictionary<long, byte[]> batchSectors = IndependentExpectedSectors(batch, imagePath);
        // The very first production call is the red-first sector compiler seam.
        foreach ((long offset, byte[] expected) in batchSectors)
        {
            byte[] before = new byte[2352]; using FileStream image = File.OpenRead(imagePath);
            image.Position = offset; image.ReadExactly(before);
            Require(batch.CreateExpectedRawSector(checked((int)(offset / 2352)), before).AsSpan().SequenceEqual(expected),
                "The native candidate raw-sector compiler differs from independent complete-scene reconstruction.");
        }
        VerifyPositive(batch, batchSectors);
        NativeGreenWizardCandidateDiscPlan cross = NativeGreenWizardCandidateDiscPlan.Create(
            NativeGreenWizardPropertiesCandidateCompiler.Compile(source, new("wizardpeak", 6, "magiccrafters", [27])));
        VerifyPositive(cross, IndependentExpectedSectors(cross, imagePath));

        long owned = batchSectors.Keys.Min(); int rejected = 0;
        foreach (long position in new[] { 10L, owned, owned + 16, owned + 24, owned + 2072, owned + 2076, owned + 2248 })
            Reject(() => Verify(outputFlip: position));
        Reject(() => Verify(sourceFlip: owned + 24));
        Reject(() => Verify(sourceFlip: 100, outputFlip: 100));
        Reject(() => Verify(outputLengthDelta: -1));
        Reject(() => Verify(outputLengthDelta: 1));
        Reject(() => Verify(growSource: true));
        Reject(() => Verify(growOutput: true));
        Reject(() => Verify(outputFlip: owned + 24, repairParity: true));
        using CancellationTokenSource cancelled = new(); cancelled.Cancel();
        Reject(() => Verify(cancellation: cancelled.Token));
        byte[] originalSector = new byte[2352]; using (FileStream image = File.OpenRead(imagePath))
        { image.Position = owned; image.ReadExactly(originalSector); }
        Reject(() => batch.CreateExpectedRawSector(-1, originalSector));
        Reject(() => batch.CreateExpectedRawSector(0, originalSector));
        Reject(() => batch.CreateExpectedRawSector((int)(owned / 2352), originalSector[..^1]));
        byte[] stalePayload = originalSector.ToArray(); stalePayload[24] ^= 1;
        stalePayload = RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(stalePayload, (int)(owned / 2352));
        Reject(() => batch.CreateExpectedRawSector((int)(owned / 2352), stalePayload));
        VerifyPublicationGuards(batch, imagePath);
        Console.WriteLine($"NATIVE_GREEN_WIZARD_CANDIDATE_DISC_SMOKE_OK batch + cross-level exact whole-disc overlay readback; rejected={rejected}; publisherGuards=4; no BIN/CUE written; runtime=false");

        void VerifyPositive(NativeGreenWizardCandidateDiscPlan plan, Dictionary<long, byte[]> sectors)
        {
            using FileStream original = File.OpenRead(imagePath), baseOutput = File.OpenRead(imagePath);
            using OverlayStream output = new(baseOutput, sectors);
            NativeGreenWizardCandidateReadback readback = plan.Verify(original, output);
            Require(readback.SourceSha256 == NativeGreenWizardCandidateSource.OriginalImageSha256 &&
                    readback.OwnedRawSectors == sectors.Count && readback.ChangedRawSectors > 0 &&
                    readback.ChangedRawSectors <= readback.OwnedRawSectors && readback.ChangedBytes > 0,
                "The full-disc native candidate readback lost its exact source identity or counted ownership.");
            output.Position = 0;
            Require(readback.OutputSha256 == Convert.ToHexStringLower(SHA256.HashData(output)),
                "Whole-disc output identity does not match the independent read-only overlay stream.");
        }
        void Verify(long? sourceFlip = null, long? outputFlip = null, int outputLengthDelta = 0,
            bool growSource = false, bool growOutput = false, bool repairParity = false, CancellationToken cancellation = default)
        {
            using FileStream original = File.OpenRead(imagePath), outputBase = File.OpenRead(imagePath);
            using OverlayStream before = new(original, new Dictionary<long, byte[]>(), sourceFlip, grow: growSource);
            Dictionary<long, byte[]> sectors = batchSectors;
            if (repairParity)
            {
                sectors = batchSectors.ToDictionary(item => item.Key, item => item.Value.ToArray());
                sectors[owned][24] ^= 1;
                sectors[owned] = RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(sectors[owned], (int)(owned / 2352));
                outputFlip = null;
            }
            using OverlayStream after = new(outputBase, sectors, outputFlip, outputLengthDelta, growOutput);
            _ = batch.Verify(before, after, cancellation);
        }
        void Reject(Action action)
        {
            try { action(); }
            catch (Exception error) when (error is InvalidDataException or OperationCanceledException) { rejected++; return; }
            throw new InvalidOperationException("Expected native candidate whole-disc rejection.");
        }
    }

    private static void VerifyPublicationGuards(NativeGreenWizardCandidateDiscPlan plan, string imagePath)
    {
        string cuePath = Path.ChangeExtension(imagePath, ".cue");
        Require(File.Exists(cuePath), "Publication guards require the original image's matching existing CUE.");
        string directory = Directory.CreateTempSubdirectory("spyro-wizard-publisher-guards-").FullName;
        try
        {
            NativeGreenWizardPropertiesCandidateRequest request = new("wizardpeak", 6, "wizardpeak", [17, 24]);
            string absentRoot = Path.Combine(directory, "must-stay-absent");
            using CancellationTokenSource cancelled = new(); cancelled.Cancel();
            RejectPublication(() => NativeGreenWizardPropertiesCandidatePublisher.Publish(imagePath, cuePath, absentRoot, request, cancelled.Token));
            RejectPublication(() => NativeGreenWizardPropertiesCandidatePublisher.Publish(imagePath, Path.Combine(directory, "missing.cue"), absentRoot, request));
            Require(!Directory.Exists(absentRoot), "Rejected or cancelled publication created an output directory.");
            RejectPublication(() => NativeGreenWizardPropertiesCandidatePublisher.Publish(imagePath, cuePath, Path.GetDirectoryName(imagePath)!, request));
            string occupied = Path.Combine(directory, "wizard-" + plan.Candidate.PlanSha256);
            Directory.CreateDirectory(occupied);
            File.WriteAllText(Path.Combine(occupied, "preserve.txt"), "pre-existing user artifact");
            RejectPublication(() => NativeGreenWizardPropertiesCandidatePublisher.Publish(imagePath, cuePath, directory, request));
            Require(Directory.GetFileSystemEntries(occupied).Length == 1 &&
                    File.ReadAllText(Path.Combine(occupied, "preserve.txt")) == "pre-existing user artifact" &&
                    !Directory.EnumerateFiles(directory, "*.bin", SearchOption.AllDirectories).Any() &&
                    !Directory.EnumerateFiles(directory, "*.cue", SearchOption.AllDirectories).Any(),
                "A rejected publisher overwrote an existing target or materialized a game image.");
        }
        finally { Directory.Delete(directory, recursive: true); }
        static void RejectPublication(Action action)
        {
            try { action(); }
            catch (Exception error) when (error is InvalidDataException or IOException or OperationCanceledException) { return; }
            throw new InvalidOperationException("Expected early candidate publication rejection before staging.");
        }
    }

    private static Dictionary<long, byte[]> IndependentExpectedSectors(NativeGreenWizardCandidateDiscPlan plan, string path)
    {
        long sceneStart = 37L * 2048 + plan.Candidate.TargetSceneWadOffset;
        byte[] sourceScene = plan.Candidate.CopySourceScene(), outputScene = plan.Candidate.CopyOutputScene();
        Dictionary<long, byte[]> sectors = [];
        // Derive physical ownership independently from the candidate's original
        // scene leaves, not from the disc mapper's affected-sector collection.
        foreach (NativeGreenWizardCandidateWrite write in plan.Candidate.Writes)
        {
            int first = checked((int)((sceneStart + write.Address.Original.Offset) / 2048));
            int last = checked((int)((sceneStart + write.Address.Original.EndExclusive - 1) / 2048));
            for (int sector = first; sector <= last; sector++)
            {
                long offset = (long)sector * 2352;
                if (sectors.ContainsKey(offset)) continue;
                byte[] raw = new byte[2352]; using FileStream image = File.OpenRead(path);
                image.Position = offset; image.ReadExactly(raw);
                int local = checked((int)((long)sector * 2048 - sceneStart));
                Require(raw.AsSpan(24, 2048).SequenceEqual(sourceScene.AsSpan(local, 2048)),
                    "The independent native sector's complete user preimage differs from its certified scene.");
                outputScene.AsSpan(local, 2048).CopyTo(raw.AsSpan(24, 2048));
                sectors.Add(offset, RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(raw, sector));
            }
        }
        Require(plan.AffectedRawSectors.SequenceEqual(sectors.Keys.Select(offset => (int)(offset / 2352)).Order()),
            "Typed disc mapping and independent source leaves disagree on complete sector ownership.");
        return sectors;
    }

    private sealed class OverlayStream(Stream inner, IReadOnlyDictionary<long, byte[]> sectors,
        long? flip = null, int lengthDelta = 0, bool grow = false) : Stream
    {
        private bool _read;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => inner.Length + lengthDelta + (grow && _read ? 1 : 0);
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            long start = Position; int count = inner.Read(buffer); _read = true;
            foreach ((long offset, byte[] bytes) in sectors)
            {
                long from = Math.Max(start, offset), to = Math.Min(start + count, offset + bytes.Length);
                if (from < to) bytes.AsSpan((int)(from - offset), (int)(to - from)).CopyTo(buffer[(int)(from - start)..]);
            }
            if (flip is long location && location >= start && location < start + count)
                buffer[(int)(location - start)] ^= 1;
            return count;
        }
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

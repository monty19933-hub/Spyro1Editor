using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

internal static class CandidateReadbackCases
{
    public static void Run(Id65CompiledAuthoringCollisionIsolation compiled, string parentPath, string candidatePath)
    {
        int rejected = 0;
        const long rowStart = 37L * 2048 + Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher.TargetDataWadOffset;
        long ownedLba = (rowStart + compiled.OwnedRanges[0].DataRelativeOffset) / 2048;
        long raw = ownedLba * 2352;
        // Every view is read-only. Published candidate bytes are never tampered.
        foreach (long offset in new[] { 10L, raw, raw + 16, raw + 24, raw + 2072, raw + 2076, raw + 2248 })
            Reject(() => Verify(outputFlip: offset));
        Reject(() => Verify(sourceFlip: raw + 24));
        // Identical corruption of both unowned streams must still fail the
        // independent whole-parent certificate, not pass on pairwise equality.
        Reject(() => Verify(sourceFlip: 100, outputFlip: 100));
        Reject(() => Verify(outputLengthDelta: -1));
        Reject(() => Verify(outputLengthDelta: 1));
        Reject(() => Verify(growOutput: true));
        Reject(() => Verify(growSource: true));
        using CancellationTokenSource cancelled = new(); cancelled.Cancel();
        Reject(() => Verify(cancellation: cancelled.Token));
        Console.WriteLine($"ID65_COLLISION_CANDIDATE_READBACK_SMOKE_OK rejected={rejected} " +
            "wholeParentCertificate=1 exactRawAfterimages=1 finalLengths=1 noPublishedMutation=1 runtime=false");

        void Verify(long? sourceFlip = null, long? outputFlip = null,
            int outputLengthDelta = 0, bool growOutput = false, bool growSource = false,
            CancellationToken cancellation = default)
        {
            using FileStream source = File.OpenRead(parentPath), output = File.OpenRead(candidatePath);
            using var before = new FaultView(source, sourceFlip, grow: growSource);
            using var after = new FaultView(output, outputFlip, outputLengthDelta, growOutput);
            _ = Id65CollisionCandidatePublisher.Verify(compiled, before, after, cancellation);
        }
        void Reject(Action action)
        {
            try { action(); }
            catch (Exception exception) when (exception is InvalidDataException or OperationCanceledException)
            { rejected++; return; }
            throw new InvalidOperationException("Expected collision candidate readback rejection.");
        }
    }

    private sealed class FaultView(Stream inner, long? flip = null, int lengthDelta = 0, bool grow = false) : Stream
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
            if (flip is long location && location >= start && location < start + count)
                buffer[checked((int)(location - start))] ^= 1;
            return count;
        }
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

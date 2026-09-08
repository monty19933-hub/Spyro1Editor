using Spyro.Editor.Core.Exporting;

internal static class StreamCases
{
    internal static Task Run(string path, NativeId65FreshDiscPlan plan, string expectedHash, Action<bool, string> check)
    {
        using (var source = File.OpenRead(path))
        using (var original = File.OpenRead(path))
        using (var output = new ExpectedImage(original, plan))
        {
            source.Position = 2352; output.Position = 4704;
            var verified = plan.VerifyImage(source, output);
            check(verified.OutputSha256 == expectedHash && verified.ChangedSectors == 1253,
                "Actual verifier resets nonzero initial positions and succeeds against the independent raw golden.");
        }
        foreach (bool sourceSuffix in new[] { false, true })
        {
            using var originalSource = File.OpenRead(path); using var backing = File.OpenRead(path);
            using var expected = new ExpectedImage(backing, plan);
            using var source = new Probe(originalSource) { ExtraSuffix = sourceSuffix };
            using var output = new Probe(expected) { ExtraSuffix = !sourceSuffix };
            ShapeCases.Reject(() => plan.VerifyImage(source, output), check, "Both actual stream EOFs are probed, not just reported length.");
            check((sourceSuffix ? source : output).SuffixReads == 1, "The hostile suffix was actually consumed/refused.");
        }
        foreach (bool sourceSide in new[] { false, true })
        foreach (string fault in new[] { "length", "position", "bytes" })
        {
            using var originalSource = File.OpenRead(path); using var backing = File.OpenRead(path);
            using var expected = new ExpectedImage(backing, plan);
            using var source = new Probe(originalSource) { Fault = sourceSide ? fault : null };
            using var output = new Probe(expected) { Fault = sourceSide ? null : fault };
            ShapeCases.Reject(() => plan.VerifyImage(source, output), check, "Source/output length, position and untouched data drift cannot verify.");
        }
        foreach (int when in new[] { 0, 1, 140635, 281270 })
        {
            using var source = File.OpenRead(path); using var backing = File.OpenRead(path);
            using var expected = new ExpectedImage(backing, plan);
            using var cancellation = new CancellationTokenSource();
            using var output = new Probe(expected) { AfterRead = end => { if (end >= when*2352L) cancellation.Cancel(); } };
            if (when == 0) cancellation.Cancel();
            try { plan.VerifyImage(source, output, cancellation.Token); throw new InvalidDataException("Cancellation accepted."); }
            catch (OperationCanceledException e) { check(e.CancellationToken == cancellation.Token, "Pre/first/middle/last cancellation returns no partial verification."); }
        }
        return Task.CompletedTask;
    }

    // Control-path fixture only: SourceCases independently binds the complete
    // physical forward image to preproduction Python goldens, not this generator.
    private sealed class ExpectedImage(Stream original, NativeId65FreshDiscPlan plan) : Stream
    {
        private long _position;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => SourceCases.Size;
        public override long Position { get => _position; set { if (value < 0 || value > Length) throw new IOException("Position out of bounds."); _position = value; } }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            int count = checked((int)Math.Min(buffer.Length, Length-_position)), written = 0;
            while (written < count)
            {
                int lba = checked((int)(_position/2352)), at = (int)(_position % 2352);
                byte[] before = new byte[2352]; original.Position = lba*2352L; original.ReadExactly(before);
                byte[] after = plan.CreateExpectedRawSector(lba, before);
                int n = Math.Min(count-written, 2352-at); after.AsSpan(at, n).CopyTo(buffer[written..]);
                written += n; _position += n;
            }
            return count;
        }
        public override long Seek(long offset, SeekOrigin origin) => Position = origin switch
        { SeekOrigin.Begin => offset, SeekOrigin.Current => Position+offset, SeekOrigin.End => Length+offset, _ => throw new ArgumentOutOfRangeException(nameof(origin)) };
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class Probe(Stream inner) : Stream
    {
        private int _reads;
        internal bool ExtraSuffix { get; init; }
        internal int SuffixReads { get; private set; }
        internal string? Fault { get; init; }
        internal Action<long>? AfterRead { get; init; }
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => inner.Length+(Fault == "length" && _reads > 0 ? 1 : 0);
        public override long Position { get => inner.Position+(Fault == "position" && _reads > 0 ? 1 : 0)+SuffixReads; set => inner.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            int count = inner.Read(buffer);
            if (count == 0 && ExtraSuffix && buffer.Length != 0 && SuffixReads == 0)
            { buffer[0] = 0x5A; SuffixReads++; return 1; }
            if (count > 0)
            {
                if (Fault == "bytes" && _reads == 0) buffer[24] ^= 1;
                _reads++; AfterRead?.Invoke(inner.Position);
            }
            return count;
        }
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

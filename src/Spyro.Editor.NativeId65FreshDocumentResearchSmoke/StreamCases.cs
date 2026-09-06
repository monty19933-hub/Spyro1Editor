using System.Text;
using Spyro.Editor.Core.Exporting;
using static PlanCases;

internal static class StreamCases
{
    internal static async Task SourceFree()
    {
        // Exercise the exact bounded reader reused by the new plan, without
        // inventing an issued native source or a filesystem CUE.
        byte[] bytes = "tiny CUE snapshot"u8.ToArray();
        using (var input = new MemoryStream(bytes, writable: false))
            Check(NativeId65FreshDiscResearchCandidatePlan.ReadCueSnapshot(input, default).SequenceEqual(bytes), "Reused reader returns exact owned snapshot.");
        using (var input = new MemoryStream(new byte[65536], writable: false))
            Check(NativeId65FreshDiscResearchCandidatePlan.ReadCueSnapshot(input, default).Length == 65536, "Exact64KiB read bound, not parser/source authority.");
        foreach (string attack in new[] { "oversize", "empty", "position", "extra-eof", "late-position", "late-length", "mid-cancel", "late-cancel" })
        {
            using var cancel = new CancellationTokenSource();
            using var input = new CueStream(bytes, attack, cancel);
            if (attack.EndsWith("cancel", StringComparison.Ordinal))
                await Reject<OperationCanceledException>(() => Task.FromResult(NativeId65FreshDiscResearchCandidatePlan.ReadCueSnapshot(input, cancel.Token)));
            else await Reject<InvalidDataException>(() => Task.FromResult(NativeId65FreshDiscResearchCandidatePlan.ReadCueSnapshot(input, cancel.Token)));
            if (attack is "oversize" or "empty" or "position")
                Check(input.ReadCalls == 0, "Invalid declared CUE owner refuses before reading or size-dependent allocation.");
        }
    }

    internal static async Task Run(string original, string cue, NativeId65FreshDocumentCompilation issued,
        NativeId65FreshDocumentResearchPlan plan)
    {
        foreach (string? text in new string?[] { null, "", " \t" })
        {
            await Reject<ArgumentException>(() => NativeId65FreshDocumentResearchPlan.CaptureAsync(text!, cue, issued));
            await Reject<ArgumentException>(() => NativeId65FreshDocumentResearchPlan.CaptureAsync(original, text!, issued));
        }
        using (var canceled = new CancellationTokenSource())
        {
            canceled.Cancel();
            await Reject<OperationCanceledException>(() => NativeId65FreshDocumentResearchPlan
                .CaptureAsync("must-not-read.bin", "must-not-read.cue", issued, canceled.Token));
        }
        foreach (string attack in new[] { "untouched", "metadata", "wrong-lba", "extra-eof", "eof-position", "eof-length", "oversize", "mid-cancel", "late-cancel", "short" })
        {
            using var cancel = new CancellationTokenSource();
            using var source = new FaultStream(File.OpenRead(original), attack, cancel);
            if (attack.EndsWith("cancel", StringComparison.Ordinal))
                await Reject<OperationCanceledException>(() => Task.FromResult(plan.MeasureExpectedImage(source, cancel.Token)));
            else if (attack == "short")
                await Reject<EndOfStreamException>(() => Task.FromResult(plan.MeasureExpectedImage(source, cancel.Token)));
            else await Reject<InvalidDataException>(() => Task.FromResult(plan.MeasureExpectedImage(source, cancel.Token)));
            if (attack == "oversize") Check(source.ReadCalls == 0, "Declared oversized owner refuses before reading/allocation proportional to claimed length.");
        }
        using (var source = File.OpenRead(original))
        {
            source.Position = 1;
            await Reject<InvalidDataException>(() => Task.FromResult(plan.MeasureExpectedImage(source, default)));
        }
        foreach (int lba in new[] { 0, 16, 22, 37, 53875, 54079, 54921, 55124 })
        {
            using var source = File.OpenRead(original);
            source.Position = lba * 2352L;
            byte[] before = new byte[2352]; source.ReadExactly(before);
            byte[] after = issued.DiscPlan.CreateExpectedRawSector(lba, before);
            Check(issued.DiscPlan.CreateOriginalRawSector(lba, after).SequenceEqual(before), "Full raw inverse on metadata, overwrite/gap, terminal and untouched sectors.");
            foreach (int at in new[] { 12, 16, 18, 20, 24, 2072, 2076, 2351 })
            {
                byte[] corrupt = after.ToArray(); corrupt[at] ^= 1;
                await Reject<InvalidDataException>(() => Task.FromResult(issued.DiscPlan.CreateOriginalRawSector(lba, corrupt)));
            }
        }
        foreach (string attack in new[] { "untouched", "extra-eof", "eof-position", "mid-cancel", "late-cancel" })
        {
            using var source = File.OpenRead(original);
            using var cancel = new CancellationTokenSource();
            using var after = new FaultStream(new GeneratedImage(original, issued.DiscPlan), attack, cancel);
            if (attack.EndsWith("cancel", StringComparison.Ordinal))
                await Reject<OperationCanceledException>(() => Task.FromResult(plan.VerifyImage(source, after, cancel.Token)));
            else await Reject<InvalidDataException>(() => Task.FromResult(plan.VerifyImage(source, after, cancel.Token)));
        }
        await CueSnapshots(original, issued);
        await plan.RequireFreshAsync();
        Console.WriteLine("GENERIC_FRESH_STREAM_CONTROLS_OK outputWritten=false");
    }

    private static async Task CueSnapshots(string original, NativeId65FreshDocumentCompilation issued)
    {
        string root = Directory.CreateTempSubdirectory("spyro-generic-plan-cue-").FullName;
        string alias = Path.Combine(root, "alias.bin"), cue = Path.Combine(root, "source.cue"), wrong = Path.Combine(root, "wrong.bin");
        byte[] valid = "FILE \"alias.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n"u8.ToArray();
        try
        {
            File.CreateSymbolicLink(alias, original);
            File.WriteAllBytes(cue, valid);
            var plan = await NativeId65FreshDocumentResearchPlan.CaptureAsync(original, cue, issued);
            Check(plan.SourceCueSha256 == Hash(valid), "Source CUE hash binds exact snapshot, not normalized text.");
            foreach (byte[] bad in new[]
            {
                Array.Empty<byte>(), new byte[65537], "FILE \"wrong.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n"u8.ToArray(),
                valid.Concat(new byte[] { 255 }).ToArray(), Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(Encoding.UTF8.GetString(valid))).ToArray()
            })
            {
                File.WriteAllBytes(cue, bad);
                await Reject<InvalidDataException>(() => NativeId65FreshDocumentResearchPlan.CaptureAsync(original, cue, issued));
                await Reject<InvalidDataException>(() => plan.RequireFreshAsync());
            }
            File.WriteAllBytes(cue, valid);
            await plan.RequireFreshAsync();
            File.WriteAllBytes(wrong, [1, 2, 3]);
            File.Delete(alias); File.CreateSymbolicLink(alias, wrong);
            Check(File.ReadAllBytes(cue).SequenceEqual(valid), "Retarget only third FILE alias; supplied BIN/CUE paths and text remain equal.");
            await Reject<InvalidDataException>(() => plan.RequireFreshAsync());
        }
        finally
        {
            foreach (string owned in new[] { alias, cue, wrong })
                if (File.Exists(owned) || new FileInfo(owned).LinkTarget != null) File.Delete(owned);
            Directory.Delete(root); // Unknown entries are not recursively removed.
        }
    }

    private sealed class FaultStream(Stream inner, string attack, CancellationTokenSource cancel) : Stream
    {
        private bool _atEof;
        internal int ReadCalls { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => attack == "oversize" || attack == "eof-length" && _atEof ? ImageLength + 1 : ImageLength;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            ReadCalls++;
            long start = inner.Position;
            if (attack == "short" && start >= 2352) return 0;
            if (attack == "wrong-lba" && start == 0) inner.Position = 2352;
            int n = inner.Read(buffer);
            if (attack == "untouched" && start == 0 && n > 24) buffer[24] ^= 1;
            long point = 16 * 2352L + 24;
            if (attack == "metadata" && start <= point && start + n > point) buffer[(int)(point - start)] ^= 1;
            if (attack == "mid-cancel") cancel.Cancel();
            return n;
        }
        public override int ReadByte()
        {
            if (inner.Position == ImageLength)
            {
                _atEof = true;
                if (attack == "eof-position") inner.Position--;
                if (attack == "late-cancel") cancel.Cancel();
                return attack == "extra-eof" ? 0 : -1;
            }
            return inner.ReadByte();
        }
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }

    private sealed class CueStream(byte[] bytes, string attack, CancellationTokenSource cancel) : Stream
    {
        private readonly MemoryStream _inner = new(bytes, writable: false);
        private bool _eof;
        internal int ReadCalls { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => attack == "oversize" ? long.MaxValue : attack == "empty" ? 0 :
            _inner.Length + (_eof && attack == "late-length" ? 1 : 0);
        public override long Position { get => _inner.Position + (attack == "position" ? 1 : 0); set => _inner.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            ReadCalls++;
            int count = _inner.Read(buffer); // Separate exact MemoryStream; no subclass dispatch recursion.
            if (attack == "mid-cancel") cancel.Cancel();
            return count;
        }
        public override int ReadByte()
        {
            _eof = true;
            if (attack == "late-position") _inner.Position--;
            if (attack == "late-cancel") cancel.Cancel();
            return attack == "extra-eof" ? 0 : -1;
        }
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }
}

// One-sector synthetic expected output, never an allocated or written BIN.
internal sealed class GeneratedImage(string original, NativeId65FreshDiscPlan disc) : Stream
{
    private readonly FileStream _source = File.OpenRead(original);
    private byte[] _sector = [];
    private int _lba = -1;
    private long _position;
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => ImageLength;
    public override long Position { get => _position; set => _position = value is >= 0 and <= ImageLength ? value : throw new IOException("Position outside owner."); }
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public override int Read(Span<byte> buffer)
    {
        int total = 0;
        while (!buffer.IsEmpty && _position < Length)
        {
            int lba = (int)(_position / 2352), within = (int)(_position % 2352);
            if (_lba != lba)
            {
                _source.Position = lba * 2352L;
                byte[] before = new byte[2352]; _source.ReadExactly(before);
                _sector = disc.CreateExpectedRawSector(lba, before); _lba = lba;
            }
            int n = Math.Min(buffer.Length, 2352 - within);
            _sector.AsSpan(within, n).CopyTo(buffer); buffer = buffer[n..];
            _position += n; total += n;
        }
        return total;
    }
    public override int ReadByte()
    {
        if (_position == Length) return -1;
        Span<byte> one = stackalloc byte[1]; return Read(one) == 1 ? one[0] : -1;
    }
    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length + offset, _ => throw new IOException() }; return Position;
    }
    public override void Flush() => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) _source.Dispose(); base.Dispose(disposing); }
}

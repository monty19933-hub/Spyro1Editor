using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using static Inspection;

internal static class MeasureCases
{
    private const long Size = 661547040;
    private static NativeId65FreshDiscVerification Measure(NativeId65FreshDiscPlan disc, Stream? source,
        CancellationToken token = default, Stream? output = null, Action? first = null) =>
        (NativeId65FreshDiscVerification)Call(null, typeof(NativeId65FreshDocumentResearchPlan), "Measure", disc, source, token, output, first)!;

    internal static void Source(string path, NativeId65FreshDiscPlan disc, string expected)
    {
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Refuse<OperationCanceledException>(() => Measure(disc, null, cancel.Token), "Current Measure cancellation precedes source null.");
        Refuse<ArgumentNullException>(() => Measure(disc, null), "Source null before stream properties.");
        foreach (string fault in new[] { "unreadable", "unseekable", "initial-position", "initial-length" })
        {
            using var source = new SourceProbe(File.OpenRead(path), fault);
            Refuse<InvalidDataException>(() => Measure(disc, source), "Initial source owner refuses: " + fault);
            Check(source.Reads == 0, "No source bytes read before initial shape refusal.");
        }
        foreach (string fault in new[] { "unwritable", "unseekable", "initial-position", "initial-length" })
        {
            using var source = new SourceProbe(File.OpenRead(path)); using var output = new Sink(fault);
            Refuse<InvalidDataException>(() => Measure(disc, source, output: output), "Initial output refuses: " + fault);
            Check(source.Reads == 0 && output.Count == 0, "Invalid output cannot consume original or emit a sector.");
        }
        foreach (string fault in new[] { "mutation", "short" })
        {
            using var source = new SourceProbe(File.OpenRead(path), fault); using var output = new Sink(); int callbacks = 0;
            if (fault == "short") Refuse<EndOfStreamException>(() => Measure(disc, source, output: output, first: () => callbacks++), "Actual short original read.");
            else Refuse<InvalidDataException>(() => Measure(disc, source, output: output, first: () => callbacks++), "Per-LBA source authentication before output.");
            Check(output.Count == 0 && callbacks == 0, "Failed first source sector cannot be copied or acknowledged.");
        }
        var primary = new IOException("synthetic first-sector checkpoint; not publication");
        using (var source = new SourceProbe(File.OpenRead(path)))
        using (var output = new Sink())
        {
            int callbacks = 0;
            var actual = Refuse<IOException>(() => Measure(disc, source, output: output, first: () =>
            { callbacks++; Check(output.Count == 2352 && source.Position == 2352, "Callback follows first complete output write."); throw primary; }), "First-sector callback order.");
            Check(ReferenceEquals(primary, actual) && callbacks == 1 && output.Count == 2352, "Exact primary preserved; callback not retried.");
        }
        using (var source = new SourceProbe(File.OpenRead(path), "partial"))
        using (var output = new Sink())
        {
            var actual = Refuse<IOException>(() => Measure(disc, source, output: output, first: () => throw primary), "Partial stream reads aggregate before output callback.");
            Check(ReferenceEquals(actual, primary) && source.Reads > 1 && output.Count == 2352, "ReadExactly supports partial reads; one complete emitted sector only.");
        }
        using (var source = File.OpenRead(path))
        using (var output = new Sink("write-failure"))
        {
            int callbacks = 0;
            var error = Refuse<IOException>(() => Measure(disc, source, output: output, first: () => callbacks++), "Sink write failure.");
            Check(ReferenceEquals(error, output.WriteError) && source.Position == 2352 && output.Count == 0 && callbacks == 0,
                "Actual write error precedes first-sector acknowledgement; no publication state is synthesized.");
        }
        foreach (int read in new[] { 1, 8, 281270 })
        {
            using var cts = new CancellationTokenSource();
            using var source = new SourceProbe(File.OpenRead(path), cancelAfterRead: read, cancellation: cts); using var output = new Sink();
            int callbacks = 0;
            var error = Refuse<OperationCanceledException>(() => Measure(disc, source, cts.Token, output, () => callbacks++), "First/middle/final cancellation.");
            Check(error.CancellationToken == cts.Token && output.Count == read * 2352L && callbacks == 1,
                "Current loop checks cancellation at next iteration/EOF; no invented mid-sector barrier.");
        }
        foreach (string fault in new[] { "eof-suffix", "final-length", "final-position" })
        {
            using var source = new SourceProbe(File.OpenRead(path), fault);
            Refuse<InvalidDataException>(() => Measure(disc, source), "Complete source final owner refusal: " + fault);
        }
        foreach (string fault in new[] { "final-length", "final-position" })
        {
            using var source = File.OpenRead(path); using var output = new Sink(fault);
            Refuse<InvalidDataException>(() => Measure(disc, source, output: output), "Output final length/position cannot differ after all writes.");
        }
        using (var source = File.OpenRead(path))
        using (var output = new Sink())
        {
            int callbacks = 0; var result = Measure(disc, source, output: output, first: () => callbacks++);
            Check(result.OutputSha256 == expected && result.ChangedSectors == 1253 && output.Digest() == expected && output.Count == Size && callbacks == 1,
                "Current actual Measure/write loop agrees with known full image; sink hashes bytes without writing a file.");
        }
        using (var source = File.OpenRead(path))
        {
            int callbacks = 0; var result = Measure(disc, source, first: () => callbacks++);
            Check(result.OutputSha256 == expected && result.ChangedSectors == 1253 && callbacks == 0,
                "Read-only Measure does not call an output checkpoint.");
        }
    }

    private sealed class SourceProbe(Stream inner, string fault = "", int cancelAfterRead = -1,
        CancellationTokenSource? cancellation = null) : Stream
    {
        internal int Reads { get; private set; }
        public override bool CanRead => fault != "unreadable";
        public override bool CanSeek => fault != "unseekable";
        public override bool CanWrite => false;
        public override long Length => fault == "initial-length" || fault == "final-length" && inner.Position == Size ? Size + 1 : Size;
        public override long Position { get => fault == "initial-position" ? 1 : fault == "final-position" && inner.Position == Size ? Size - 1 : inner.Position; set => inner.Position = value; }
        public override int Read(byte[] b, int at, int n) => Read(b.AsSpan(at, n));
        public override int Read(Span<byte> b)
        {
            Reads++;
            if (fault == "short") return 0;
            int n;
            if (fault == "partial") n = inner.Read(b[..Math.Min(17, b.Length)]);
            else { inner.ReadExactly(b); n = b.Length; }
            if (fault == "mutation" && n > 0) b[0] ^= 1;
            if (Reads == cancelAfterRead) cancellation!.Cancel();
            return n;
        }
        public override int ReadByte() => fault == "eof-suffix" && inner.Position == Size ? 0 : inner.ReadByte();
        public override long Seek(long n, SeekOrigin o) => inner.Seek(n, o);
        public override void SetLength(long n) => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int n) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
    private sealed class Sink(string fault = "") : Stream
    {
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        internal IOException WriteError { get; } = new("Synthetic stream write failure; not a transaction outcome.");
        internal long Count { get; private set; }
        internal string Digest() => Convert.ToHexStringLower(_hash.GetHashAndReset());
        public override bool CanRead => false;
        public override bool CanWrite => fault != "unwritable";
        public override bool CanSeek => fault != "unseekable";
        public override long Position { get => fault == "initial-position" ? 1 : fault == "final-position" && Count == Size ? Count - 1 : Count; set => throw new NotSupportedException(); }
        public override long Length => fault == "initial-length" || fault == "final-length" && Count == Size ? Count + 1 : Count;
        public override void Write(byte[] b, int o, int n) => Write(b.AsSpan(o, n));
        public override void Write(ReadOnlySpan<byte> b)
        { if (fault == "write-failure") throw WriteError; _hash.AppendData(b); Count += b.Length; }
        public override int Read(byte[] b, int o, int n) => throw new NotSupportedException();
        public override long Seek(long n, SeekOrigin o) => throw new NotSupportedException();
        public override void SetLength(long n) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _hash.Dispose(); base.Dispose(disposing); }
    }
}

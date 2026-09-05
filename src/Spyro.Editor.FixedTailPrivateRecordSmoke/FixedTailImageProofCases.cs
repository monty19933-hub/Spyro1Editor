using System.Security.Cryptography;

internal static class FixedTailImageProofCases
{
    internal static void Run()
    {
        byte[] sourceBytes = GapFixture.Image(2352).AsSpan(0, 2 * 2352).ToArray();
        byte[] outputBytes = sourceBytes.ToArray();
        outputBytes[24] = 19; GapFixture.Rebuild(outputBytes.AsSpan(0, 2352));
        byte[] legacyBytes = outputBytes.ToArray();
        sourceBytes.AsSpan(2072, 280).CopyTo(legacyBytes.AsSpan(2072));
        FixedTailExpectedImage expected = new(sourceBytes.Length, Hash(sourceBytes), Hash(outputBytes), Hash(legacyBytes), 1);
        using (MemoryStream source = new(sourceBytes))
        using (MemoryStream output = new(outputBytes))
            _ = FixedTailImageProof.VerifyStreams(source, output, expected);

        Reject("late output append", sourceBytes, outputBytes, null, AppendByte);
        Reject("late source append", sourceBytes, outputBytes, AppendByte, null);
        Reject("late source and output append", sourceBytes, outputBytes, AppendByte, AppendByte);
        Reject("late source truncation", sourceBytes, outputBytes, stream => stream.SetLength(stream.Length - 1), null);
        Reject("late output truncation", sourceBytes, outputBytes, null, stream => stream.SetLength(stream.Length - 1));
        Reject("late source position change", sourceBytes, outputBytes, stream => stream.Position--, null);
        Reject("late output position change", sourceBytes, outputBytes, null, stream => stream.Position--);
        byte[] damagedSource = sourceBytes.ToArray(); damagedSource[25] = 27;
        // Output/legacy hashes and the changed-sector count still match. Only a
        // frozen whole-source identity catches this altered source preimage.
        Reject("source-only corruption in already changed sector", damagedSource, outputBytes, null, null, "complete original source image");
        byte[] damagedOutput = outputBytes.ToArray(); damagedOutput[25] = 27;
        GapFixture.Rebuild(damagedSource.AsSpan(0, 2352)); GapFixture.Rebuild(damagedOutput.AsSpan(0, 2352));
        Reject("matching source/output corruption", damagedSource, damagedOutput, null, null, "complete original source image");

        void Reject(string name, byte[] sourceData, byte[] outputData, Action<MemoryStream>? sourceEnd, Action<MemoryStream>? outputEnd, string? requiredFailure = null)
        {
            using EndMutationStream source = new(sourceData, sourceEnd), output = new(outputData, outputEnd);
            bool rejected = false;
            try { _ = FixedTailImageProof.VerifyStreams(source, output, expected); }
            catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
            { rejected = requiredFailure == null || exception.Message.Contains(requiredFailure, StringComparison.Ordinal); }
            if (!rejected) throw new InvalidOperationException($"Fixed-tail stream verifier accepted {name}.");
        }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void AppendByte(MemoryStream stream)
    { long position = stream.Position; stream.Position = stream.Length; stream.WriteByte(0); stream.Position = position; }

    // Read-only consumer surface; only the synthetic fixture can mutate backing
    // memory at the final read. No original/published file is opened for writing.
    private sealed class EndMutationStream : Stream
    {
        private readonly MemoryStream _inner = new();
        private readonly long _originalLength;
        private Action<MemoryStream>? _onEnd;
        internal EndMutationStream(byte[] bytes, Action<MemoryStream>? onEnd)
        { _inner.Write(bytes); _inner.Position = 0; _originalLength = bytes.Length; _onEnd = onEnd; }
        public override int Read(Span<byte> buffer)
        {
            int count = _inner.Read(buffer);
            if (_inner.Position >= _originalLength && _onEnd is { } action)
            { _onEnd = null; action(_inner); }
            return count;
        }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }
}

using Spyro.Editor.Core.Exporting;

// Exercises the same bounded stream reader used by source-CUE capture/freshness.
// The neutral byte reader cannot construct a source-issued research plan.
internal static class CueStreamCases
{
    internal static int Run()
    {
        int checks = 0;
        byte[] bytes = "small exact source CUE\n"u8.ToArray();
        using (var stream = new MemoryStream(bytes, writable: false))
        {
            byte[] captured = NativeId65FreshDiscResearchCandidatePlan.ReadCueSnapshot(stream, CancellationToken.None);
            Check(captured.SequenceEqual(bytes) && stream.Position == stream.Length, "exact bounded snapshot reaches EOF");
            captured[0] ^= 1;
            Check(bytes[0] == (byte)'s', "snapshot bytes do not alias the input stream buffer");
        }
        foreach (long length in new[] { 0L, 65537L, long.MaxValue })
        {
            using var stream = new OversizeStream(length);
            Reject(() => NativeId65FreshDiscResearchCandidatePlan.ReadCueSnapshot(stream, CancellationToken.None));
            Check(stream.Reads == 0, "invalid declared size is rejected before reading or materializing its declared owner");
        }
        using (var stream = new MemoryStream(new byte[65536], writable: false))
            Check(NativeId65FreshDiscResearchCandidatePlan.ReadCueSnapshot(stream, CancellationToken.None).Length == 65536,
                "exact maximum snapshot is allowed before grammar validation");
        using (var stream = new MemoryStream(bytes, writable: false))
        {
            stream.Position = 1;
            Reject(() => NativeId65FreshDiscResearchCandidatePlan.ReadCueSnapshot(stream, CancellationToken.None));
            Check(stream.Position == 1, "nonzero initial cursor refuses without silently rewinding");
        }
        foreach (var fault in new[] { EndFault.ExtraByte, EndFault.Position, EndFault.Length })
        {
            using var stream = new HostileEndStream(bytes, fault);
            Reject(() => NativeId65FreshDiscResearchCandidatePlan.ReadCueSnapshot(stream, CancellationToken.None));
            Check(stream.EofReads == 1, "source-CUE reader probes actual EOF and rechecks length/position afterward");
        }
        foreach (var at in new[] { CancelAt.BeforeRead, CancelAt.DuringRead, CancelAt.AtEof })
        {
            using var token = new CancellationTokenSource();
            using var stream = new CancelStream(bytes, at, token);
            if (at == CancelAt.BeforeRead) token.Cancel();
            bool cancelled = false;
            try { NativeId65FreshDiscResearchCandidatePlan.ReadCueSnapshot(stream, token.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled, "early, mid-read and EOF-time cancellation cannot return source bytes");
            if (at == CancelAt.BeforeRead) Check(stream.Reads == 0, "early cancellation precedes input reads");
            if (at == CancelAt.AtEof) Check(stream.EofReads == 1, "late cancellation occurs at the actual EOF probe");
        }
        return checks;

        void Check(bool value, string label)
        {
            if (!value) throw new InvalidDataException(label);
            checks++;
        }

        void Reject(Action action)
        {
            try { action(); }
            catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or InvalidOperationException)
            {
                checks++;
                return;
            }
            throw new InvalidDataException("Unexpected acceptance of hostile CUE snapshot stream.");
        }
    }

    private sealed class OversizeStream(long declaredLength) : MemoryStream(Array.Empty<byte>(), writable: false)
    {
        internal int Reads { get; private set; }
        public override long Length => declaredLength;
        public override int Read(Span<byte> buffer)
        {
            Reads++;
            throw new InvalidDataException("Invalid owner must be refused before reading.");
        }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int ReadByte()
        {
            Reads++;
            return -1;
        }
    }

    private enum EndFault { ExtraByte, Position, Length }

    private sealed class HostileEndStream(byte[] bytes, EndFault fault) : MemoryStream(bytes, writable: false)
    {
        private bool _probed;
        internal int EofReads { get; private set; }
        public override long Length => base.Length + (_probed && fault == EndFault.Length ? 1 : 0);
        public override int ReadByte()
        {
            if (base.Position != base.Length) return base.ReadByte();
            EofReads++;
            _probed = true;
            if (fault == EndFault.Position) Position--;
            return fault == EndFault.ExtraByte ? 0xA5 : -1;
        }
    }

    private enum CancelAt { BeforeRead, DuringRead, AtEof }

    private sealed class CancelStream(byte[] bytes, CancelAt at, CancellationTokenSource token)
        : MemoryStream(bytes, writable: false)
    {
        internal int Reads { get; private set; }
        internal int EofReads { get; private set; }
        public override int Read(Span<byte> buffer)
        {
            // A derived MemoryStream's base span overload can dispatch through
            // our array override. Use its direct array implementation instead.
            byte[] temporary = new byte[Math.Min(buffer.Length, 65536)];
            Reads++;
            int count = base.Read(temporary, 0, temporary.Length);
            temporary.AsSpan(0, count).CopyTo(buffer);
            if (at == CancelAt.DuringRead) token.Cancel();
            return count;
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            Reads++;
            int read = base.Read(buffer, offset, count);
            if (at == CancelAt.DuringRead) token.Cancel();
            return read;
        }
        public override int ReadByte()
        {
            if (Position == Length)
            {
                EofReads++;
                if (at == CancelAt.AtEof) token.Cancel();
            }
            return base.ReadByte();
        }
    }
}

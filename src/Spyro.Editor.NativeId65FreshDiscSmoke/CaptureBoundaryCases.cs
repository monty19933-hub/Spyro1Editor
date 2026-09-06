using Spyro.Editor.Core.Exporting;

/// <summary>
/// Read-only virtual attacks on the new private same-pass issuer. Mutations
/// affect returned bytes only; no source file is opened with write access.
/// </summary>
internal static class CaptureBoundaryCases
{
    internal static async Task Source(string path, NativeId65FreshDiscPlan plan, Action<bool, string> check)
    {
        // Positive verifier control-path coverage. This virtual output is not
        // independent byte evidence: FreshDiscCases separately binds every
        // physical afterimage to the complete raw Python-oracle goldens.
        using (var original = File.OpenRead(path))
        using (var expectedSource = File.OpenRead(path))
        using (var expected = new ExpectedImage(expectedSource, plan))
        {
            NativeId65FreshDiscVerification result = plan.VerifyImage(original, expected);
            check(result.OutputSha256 == "1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188" &&
                result.ChangedSectors == 1253,
                "successful whole verifier returns the independently established hash/count only after all EOF and freshness guards");
        }
        // PVD volume-size bytes are not consumed by layout detection. With an
        // unauthenticated reader this one-time change can pass that parser,
        // disappear before later metadata reads/final hashing, and be missed.
        foreach (long changedByte in new[]
        {
            16L * 2352 + 24 + 80, // PVD field not used by initial layout detection
            22L * 2352 + 24 + 1900, // complete root owner, beyond its parsed rows
            18L * 2352 + 24 + 100, // path-table containing sector, beyond ten-byte logical table
            37L * 2352 + 24 + 2000, // WAD header untouched complement
            53875L * 2352 + 24 + 100, // complete executable owner
            54079L * 2352 + 2348 // canonical Form2 gap's physical EDC owner
        })
        {
            using var file = File.OpenRead(path);
            using var hostile = new ReadProbe(file);
            bool refused = false;
            Action afterIndexCapture = () => hostile.MutateOnceAt = changedByte;
            try
            {
                await NativeId65FreshDiscSource.CaptureForSmokeAsync(hostile,
                    afterIndexCapture, CancellationToken.None);
            }
            catch (InvalidDataException error) when (error.Message.Contains("private authenticated source-LBA digest", StringComparison.Ordinal))
            {
                refused = true;
            }
            check(refused && hostile.Mutations == 1,
                "every later metadata/preimage read authenticates its entire physical sector before parsing, even if corruption is transient");
        }
        using (var file = File.OpenRead(path))
        using (var hostile = new ReadProbe(file) { ExtraEofByte = true })
        {
            bool refused = false;
            try
            {
                await NativeId65FreshDiscSource.CaptureForSmokeAsync(hostile, null, CancellationToken.None);
            }
            catch (InvalidDataException)
            {
                refused = true;
            }
            check(refused && hostile.ExtraEofReads > 0,
                "a stream reporting original Length but yielding an actual extra EOF byte cannot issue a source");
        }
        foreach (int when in new[] { 0, 1, 2 })
        {
            using var file = File.OpenRead(path);
            using var cancellation = new CancellationTokenSource();
            using var hostile = new ReadProbe(file);
            if (when == 0)
                cancellation.Cancel();
            if (when == 1)
                hostile.AfterRead = end => { if (end >= 2352 * 100) cancellation.Cancel(); };
            bool cancelled = false;
            Action? afterIndexCapture = when == 2 ? cancellation.Cancel : null;
            try
            {
                await NativeId65FreshDiscSource.CaptureForSmokeAsync(hostile,
                    afterIndexCapture, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            check(cancelled, "early/mid/same-pass-final cancellation returns no partial source/index");
        }
        foreach (bool sourceSuffix in new[] { true, false })
        {
            using var originalFile = File.OpenRead(path);
            using var expectedFile = File.OpenRead(path);
            using var expected = new ExpectedImage(expectedFile, plan);
            using var source = new ReadProbe(originalFile) { ExtraEofByte = sourceSuffix };
            using var output = new ReadProbe(expected) { ExtraEofByte = !sourceSuffix };
            bool refused = false;
            try
            {
                plan.VerifyImage(source, output, CancellationToken.None);
            }
            catch (InvalidDataException)
            {
                refused = true;
            }
            check(refused && (sourceSuffix ? source.ExtraEofReads : output.ExtraEofReads) > 0,
                "whole verifier probes actual source AND output EOF instead of trusting reported Length/Position");
        }
        foreach (int sectorCount in new[] { 1, 140635, 281270 })
        {
            using var originalFile = File.OpenRead(path);
            using var expectedFile = File.OpenRead(path);
            using var cancellation = new CancellationTokenSource();
            using var expected = new ExpectedImage(expectedFile, plan);
            using var output = new ReadProbe(expected)
            {
                AfterRead = end => { if (end >= 2352L * sectorCount) cancellation.Cancel(); }
            };
            bool cancelled = false;
            try
            {
                plan.VerifyImage(originalFile, output, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            check(cancelled, "first/middle/last expected-sector cancellation cannot return partial verification success");
        }
    }

    // Used only to exercise verifier control paths, never as independent
    // afterimage evidence. FreshDiscCases binds both whole hashes to Python's
    // separately implemented parity/ISO/body/EXE oracle.
    private sealed class ExpectedImage(Stream original, NativeId65FreshDiscPlan plan) : Stream
    {
        private long _position;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => 661547040;
        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > Length)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _position = value;
            }
        }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> destination)
        {
            int count = (int)Math.Min(destination.Length, Length - Position);
            int copied = 0;
            while (copied < count)
            {
                int lba = checked((int)(Position / 2352));
                int offset = checked((int)(Position % 2352));
                int part = Math.Min(2352 - offset, count - copied);
                byte[] sector = new byte[2352];
                original.Position = 2352L * lba;
                original.ReadExactly(sector);
                byte[] after = plan.CreateExpectedRawSector(lba, sector);
                after.AsSpan(offset, part).CopyTo(destination[copied..]);
                copied += part;
                Position += part;
            }
            return count;
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            Position = checked((origin switch
            {
                SeekOrigin.Begin => 0,
                SeekOrigin.Current => Position,
                SeekOrigin.End => Length,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            }) + offset);
            return Position;
        }
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class ReadProbe(Stream source) : Stream
    {
        internal long? MutateOnceAt { get; set; }
        internal int Mutations { get; private set; }
        internal bool ExtraEofByte { get; init; }
        internal int ExtraEofReads { get; private set; }
        internal Action<long>? AfterRead { get; set; }
        private bool _pastPhysicalEnd;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => source.Length;
        public override long Position
        {
            get => source.Position + (_pastPhysicalEnd ? 1 : 0);
            set
            {
                source.Position = value;
                _pastPhysicalEnd = false;
            }
        }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            long start = Position;
            if (buffer.Length > 0 && ExtraEofByte && start == Length)
            {
                buffer[0] = 0xA5;
                _pastPhysicalEnd = true;
                ExtraEofReads++;
                return 1;
            }
            if (_pastPhysicalEnd)
                return 0;
            int length = source.Read(buffer);
            if (MutateOnceAt is long changed && changed >= start && changed < start + length)
            {
                buffer[checked((int)(changed - start))] ^= 1;
                MutateOnceAt = null;
                Mutations++;
            }
            AfterRead?.Invoke(start + length);
            return length;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(buffer, offset, count));
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            _pastPhysicalEnd = false;
            return source.Seek(offset, origin);
        }
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

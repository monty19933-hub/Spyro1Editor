using Spyro.Editor.Core.Exporting;
using static Cases;

// Read-only tests of the concrete chest inverse. The source gate receives an
// already retained physical candidate; no afterimages or full-image buffers are
// generated here. Private wrappers perturb reads, never files or native owners.
internal static class SavedLockedChestInverseCases
{
    private const int SectorBytes = 2352;
    private const long ImageBytes = 661547040;
    private const string InitialBoundary = "Fresh-disc inverse requires a complete readable candidate at offset zero.";

    internal static async Task FreeAsync()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var cancellation = await RejectExact<OperationCanceledException>(() =>
            NativeId65FreshDiscStreaming.VerifyInverse((NativeFreshLockedChestResearchDiscPlan)null!, null!, canceled.Token));
        Check(cancellation.CancellationToken == canceled.Token,
            "Typed chest inverse checks cancellation before disc/candidate validation without opening any input.");
        var missing = await RejectExact<ArgumentNullException>(() =>
            NativeId65FreshDiscStreaming.VerifyInverse((NativeFreshLockedChestResearchDiscPlan)null!, null!, default));
        Check(missing.ParamName == "disc", "Typed chest inverse requires an actual concrete disc before its stream.");
        Console.WriteLine("SAVED_LOCKED_CHEST_INVERSE_FREE_OK originalOpened=false candidateOpened=false nativeImageWritten=false");
    }

    internal static async Task SourceAsync(NativeFreshSavedLevelResearchCandidatePlan actual, string exactCandidateBin)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentException.ThrowIfNullOrWhiteSpace(exactCandidateBin);
        var compilation = actual.LockedChestCompilation ??
            throw new InvalidDataException("Inverse source gate requires the actual saved locked-chest arm.");
        actual.RequireLockedChestAncestry(actual.Saved, compilation);
        var disc = compilation.Disc;
        Check(actual.ExpectedImageByteLength == ImageBytes && actual.ExpectedChangedRawSectors == 1297 &&
            actual.Saved.Document.SchemaVersion == 5 && actual.Saved.Document.Placements.Count == 3 &&
            !actual.RuntimeVerified, "Actual complete schema5 P9 plan, not a caller digest or another disc arm.");
        string path = Path.GetFullPath(exactCandidateBin);
        var before = new FileInfo(path);
        Check(before.Exists && before.LinkTarget == null && !Directory.Exists(path) && before.Length == ImageBytes &&
            path != actual.SourceImagePath, "Existing exact-length candidate file is distinct from the original source.");
        DateTime initialWriteTime = before.LastWriteTimeUtc;
        int initialChecks = Checks, initialRefusals = Refusals;

        // One successful full inverse reads every actual candidate byte. The
        // production method returns only after its reconstructed Original SHA
        // and physical EOF have passed; no restored file is created.
        using (var candidate = Open(path))
        {
            var verified = NativeId65FreshDiscStreaming.VerifyInverse(disc, candidate, default);
            Check(verified.OutputSha256 == actual.ExpectedImageSha256 && verified.ChangedSectors == 1297 &&
                candidate.BytesRead == ImageBytes && candidate.Position == ImageBytes && candidate.ReadByte() == -1,
                "Actual physical candidate authenticates complete Original restoration, exact output hash,1297 changes and EOF.");
            Check(candidate.MaximumReadRequest <= SectorBytes, "Inverse retains bounded sector reads, never a whole-image request.");
        }

        // These malformed stream contracts fail before even one byte is read.
        foreach (var mode in new[] { ReadFault.ShortReportedLength, ReadFault.LongReportedLength,
            ReadFault.WrongPosition, ReadFault.NotSeekable, ReadFault.NotReadable })
        {
            using var candidate = Open(path, mode);
            await RejectExact<InvalidDataException>(() => NativeId65FreshDiscStreaming.VerifyInverse(disc, candidate, default), InitialBoundary);
            Check(candidate.BytesRead == 0 && candidate.ReadCalls == 0,
                "Invalid initial " + mode + " contract refuses before physical reads.");
        }
        var nullStream = await RejectExact<ArgumentNullException>(() =>
            NativeId65FreshDiscStreaming.VerifyInverse(disc, null!, default));
        Check(nullStream.ParamName == "candidate", "Actual chest disc still requires a candidate stream.");

        using (var candidate = Open(path, ReadFault.ShortDelivery))
        {
            await RejectExact<EndOfStreamException>(() => NativeId65FreshDiscStreaming.VerifyInverse(disc, candidate, default));
            Check(candidate.BytesRead == SectorBytes - 1 && candidate.ReadCalls == 2,
                "A stream claiming full length but ending inside its first sector rejects actual short delivery.");
        }
        // LBA0 is untouched. LBA22 is the concrete owned ISO root; each read
        // fault is independently rejected in at most23 sectors (<55KiB).
        foreach (long at in new[] { 24L, 22L * SectorBytes + 24, 22L * SectorBytes + 12, 22L * SectorBytes + 2072 })
        {
            using var candidate = Open(path, ReadFault.CorruptByte, at);
            string message = at < SectorBytes
                ? "Fresh-disc contributing sector failed its private authenticated source-LBA digest."
                : "Wizard inverse afterimage payload/header/parity changed.";
            await RejectExact<InvalidDataException>(() => NativeId65FreshDiscStreaming.VerifyInverse(disc, candidate, default), message);
            Check(candidate.InjectedBytes == 1 && candidate.BytesRead == (at / SectorBytes + 1) * SectorBytes &&
                candidate.BytesRead <= 23L * SectorBytes,
                "Untouched/owned payload/header/parity corruption is checked against actual owners without changing the file.");
        }

        using (var canceled = new CancellationTokenSource())
        using (var candidate = Open(path))
        {
            canceled.Cancel();
            var error = await RejectExact<OperationCanceledException>(() =>
                NativeId65FreshDiscStreaming.VerifyInverse(disc, candidate, canceled.Token));
            Check(error.CancellationToken == canceled.Token && candidate.BytesRead == 0 && candidate.ReadCalls == 0,
                "Pre-canceled actual inverse preserves its exact caller token and does not read the candidate.");
        }
        using (var canceled = new CancellationTokenSource())
        using (var candidate = Open(path, ReadFault.CancelAfterSector, cancellation: canceled))
        {
            var error = await RejectExact<OperationCanceledException>(() =>
                NativeId65FreshDiscStreaming.VerifyInverse(disc, candidate, canceled.Token));
            Check(error.CancellationToken == canceled.Token && canceled.IsCancellationRequested &&
                candidate.BytesRead == SectorBytes && candidate.ReadCalls == 1,
                "Mid-read cancellation is observed after one sector, not deferred until complete-image hashing.");
        }

        // The only second full pass exercises real EOF, not merely Length.
        // Every sector comes from the physical file; the wrapper supplies just
        // one extra byte after its end while continuing to report exact length.
        using (var candidate = Open(path, ReadFault.AppendedByte))
        {
            await RejectExact<InvalidDataException>(() => NativeId65FreshDiscStreaming.VerifyInverse(disc, candidate, default),
                "Fresh-disc physical stream has changed length, position or actual EOF.");
            Check(candidate.BytesRead == ImageBytes && candidate.InjectedBytes == 1 &&
                candidate.Position == ImageBytes + 1 && candidate.MaximumReadRequest <= SectorBytes,
                "A concealed appended byte fails the actual EOF read after all physical sectors were authenticated.");
        }
        actual.RequireLockedChestAncestry(actual.Saved, compilation);
        var after = new FileInfo(path);
        Check(after.Exists && after.LinkTarget == null && after.Length == ImageBytes && after.LastWriteTimeUtc == initialWriteTime,
            "Read-only fault exercise leaves observed candidate length and write time unchanged; not an inode/ABA guarantee.");
        Console.WriteLine($"SAVED_LOCKED_CHEST_INVERSE_SOURCE_OK checks={Checks - initialChecks} refusals={Refusals - initialRefusals} " +
            $"image={actual.ExpectedImageSha256} changedSectors=1297 cleanFullPasses=1 appendedEofFullPasses=1 " +
            "actualCandidateRead=true nativeImageWritten=false runtimeVerified=false");
    }

    private static async Task<T> RejectExact<T>(Action action, string? message = null) where T : Exception
    {
        var error = await Throws<T>(() => { action(); return Task.CompletedTask; });
        Check(error.GetType() == typeof(T) && (message == null || error.Message == message),
            "Expected exact " + typeof(T).Name + " at " + (message ?? "the typed inverse boundary") + "; observed " + error.Message);
        return error;
    }

    private enum ReadFault { None, ShortReportedLength, LongReportedLength, WrongPosition,
        NotSeekable, NotReadable, ShortDelivery, CorruptByte, CancelAfterSector, AppendedByte }

    private static CandidateReadStream Open(string path, ReadFault fault = ReadFault.None,
        long corruptAt = -1, CancellationTokenSource? cancellation = null) =>
        new(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), fault, corruptAt, cancellation);

    // All bytes before EOF come directly from the read-only physical owner.
    // No production callback, issuer or fake captured/native plan is involved.
    private sealed class CandidateReadStream : Stream
    {
        private readonly FileStream source;
        private readonly ReadFault fault;
        private readonly long corruptAt;
        private readonly CancellationTokenSource? cancellation;
        private bool appended;
        internal long BytesRead { get; private set; }
        internal int ReadCalls { get; private set; }
        internal int MaximumReadRequest { get; private set; }
        internal int InjectedBytes { get; private set; }

        internal CandidateReadStream(FileStream source, ReadFault fault, long corruptAt, CancellationTokenSource? cancellation)
        {
            this.source = source; this.fault = fault; this.corruptAt = corruptAt; this.cancellation = cancellation;
            if (fault == ReadFault.WrongPosition) source.Position = 1;
        }
        public override bool CanRead => fault != ReadFault.NotReadable && source.CanRead;
        public override bool CanSeek => fault != ReadFault.NotSeekable && source.CanSeek;
        public override bool CanWrite => false;
        public override long Length => source.Length + (fault == ReadFault.ShortReportedLength ? -1 : fault == ReadFault.LongReportedLength ? 1 : 0);
        public override long Position
        {
            get => source.Position + (appended ? 1 : 0);
            set { source.Position = value; appended = false; }
        }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            ReadCalls++; MaximumReadRequest = Math.Max(MaximumReadRequest, buffer.Length);
            if (buffer.IsEmpty) return 0;
            long at = source.Position;
            if (fault == ReadFault.ShortDelivery)
            {
                if (at >= SectorBytes - 1) return 0;
                buffer = buffer[..(int)Math.Min(buffer.Length, SectorBytes - 1 - at)];
            }
            int count = source.Read(buffer);
            BytesRead += count;
            if (fault == ReadFault.CorruptByte && corruptAt >= at && corruptAt < at + count)
            { buffer[(int)(corruptAt - at)] ^= 1; InjectedBytes++; }
            if (fault == ReadFault.CancelAfterSector && BytesRead >= SectorBytes) cancellation!.Cancel();
            if (count == 0 && fault == ReadFault.AppendedByte && !appended)
            { buffer[0] = 0x5A; appended = true; InjectedBytes++; return 1; }
            return count;
        }
        public override long Seek(long offset, SeekOrigin origin)
        { appended = false; return source.Seek(offset, origin); }
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        { if (disposing) source.Dispose(); base.Dispose(disposing); }
    }
}

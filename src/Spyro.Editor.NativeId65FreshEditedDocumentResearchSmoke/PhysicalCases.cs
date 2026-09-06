using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using static EditedCases;

internal static class PhysicalCases
{
    internal static async Task Run(string original, string cue, NativeId65FreshDocumentCompilation compiled,
        NativeId65FreshEditedDocumentResearchPlan plan, NativeId65FreshDiscPlan starter)
    {
        const string originalHash = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
        const long length = 661547040;
        var disc = compiled.DiscPlan;
        using var source = File.OpenRead(original);
        using var forward = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var inverse = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var legacy = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] before = new byte[2352];
        source.ReadExactly(before);
        byte[] badSource = before.ToArray();
        badSource[24] ^= 1;
        await Reject<InvalidDataException>(() => Task.FromResult(disc.CreateExpectedRawSector(0, badSource)));
        await Reject<InvalidDataException>(() => Task.FromResult(disc.CreateExpectedRawSector(1, before)));
        await Reject<InvalidDataException>(() => Task.FromResult(disc.CreateOriginalRawSector(0, badSource)));
        source.Position = 0;
        int changed = 0;
        for (int lba = 0; lba < 281270; lba++)
        {
            source.ReadExactly(before);
            byte[] after = disc.CreateExpectedRawSector(lba, before);
            byte[] restored = disc.CreateOriginalRawSector(lba, after);
            Check(restored.SequenceEqual(before), "Actual full-physical inverse, including original EXE and Form2 gap.");
            forward.AppendData(after);
            inverse.AppendData(restored);
            legacy.AppendData(starter.CreateExpectedRawSector(lba, before));
            if (!before.SequenceEqual(after)) changed++;
            if (lba is 22 or 37 or 53874 or 53875 or 54078 or 54079 or 54920 or 54921 or 55124)
            {
                foreach (int offset in new[] { 12, 16, 18, 20, 24, 2072, 2076, 2351 })
                {
                    byte[] corrupt = after.ToArray();
                    corrupt[offset] ^= 1;
                    await Reject<InvalidDataException>(() => Task.FromResult(disc.CreateOriginalRawSector(lba, corrupt)));
                }
            }
        }
        Check(source.ReadByte() == -1 && source.Position == length && source.Length == length, "Actual EOF/final source guards.");
        Check(Convert.ToHexStringLower(forward.GetHashAndReset()) == ImageHash && changed == 1253, "Independent edited full image and changed count.");
        Check(Convert.ToHexStringLower(inverse.GetHashAndReset()) == originalHash, "Actual entire original restoration.");
        Check(Convert.ToHexStringLower(legacy.GetHashAndReset()) == "1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188",
            "Actual complete starter afterimage remains byte-exact, not just its expected constant.");
        source.Position = 0;
        using var generated = new AfterimageStream(original, disc);
        var verified = disc.VerifyImage(source, generated);
        Check(verified.OutputSha256 == ImageHash && verified.ChangedSectors == 1253, "VerifyImage returns actual successful full readback without physical output.");
        Check(source.Position == length && generated.Position == length, "Successful verifier consumes full source/output streams.");
        byte[] receipt = plan.BuildReceipt(verified);
        using (var parsed = JsonDocument.Parse(receipt))
        {
            var root = parsed.RootElement;
            Check(root.GetProperty("canonicalDocumentJson").GetString() == System.Text.Encoding.UTF8.GetString(compiled.CopyCanonicalDocument()) &&
                root.GetProperty("canonicalDocumentSha256").GetString() == CanonicalHash, "Receipt binds exact actual compiled document.");
            Check(!root.GetProperty("projectSaveBindingClaimed").GetBoolean() && !root.GetProperty("runtimeVerified").GetBoolean() &&
                !root.GetProperty("normalCreateBinApproved").GetBoolean(), "Receipt does not invent project-save/native approval.");
        }
        receipt[0] ^= 1;
        Check(plan.BuildReceipt(verified)[0] != receipt[0], "Receipt copy cannot mutate plan.");
        await Reject<InvalidDataException>(() => Task.FromResult(plan.BuildReceipt(new(ImageHash, 1252))));
        byte[] outputCue = plan.CopyCueBytes();
        Check(System.Text.Encoding.UTF8.GetString(outputCue) ==
            "FILE \"native-id65-fresh-edited-landing-track02-r01.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n",
            "New CUE emits new fixed name, never the discarded starter helper output.");
        outputCue[0] ^= 1;
        Check(plan.CopyCueBytes()[0] != outputCue[0], "CUE copy cannot mutate plan.");
        foreach (string attack in new[] { "untouched", "extra-eof", "position", "cancel" })
        {
            using var freshSource = File.OpenRead(original);
            using var cancellation = new CancellationTokenSource();
            using var output = new AfterimageStream(original, disc, attack, cancellation);
            if (attack == "cancel")
                await Reject<OperationCanceledException>(() => Task.FromResult(disc.VerifyImage(freshSource, output, cancellation.Token)));
            else
                await Reject<InvalidDataException>(() => Task.FromResult(disc.VerifyImage(freshSource, output)));
        }
        await CueAliases(original, compiled);
        await plan.RequireFreshAsync();
        Console.WriteLine($"EDITED_DOCUMENT_PHYSICAL_OK image={ImageHash} changed={changed} outputWritten=false");
    }

    private static async Task CueAliases(string original, NativeId65FreshDocumentCompilation compiled)
    {
        string root = Directory.CreateTempSubdirectory("spyro-edited-document-cue-").FullName;
        string alias = Path.Combine(root, "alias.bin"), cue = Path.Combine(root, "source.cue"), wrong = Path.Combine(root, "wrong.bin");
        try
        {
            File.CreateSymbolicLink(alias, original);
            File.WriteAllBytes(cue, "FILE \"alias.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n"u8.ToArray());
            var plan = await NativeId65FreshEditedDocumentResearchPlan.CaptureAsync(original, cue, compiled);
            byte[] retained = File.ReadAllBytes(cue);
            File.WriteAllBytes(wrong, [1, 2, 3]);
            File.Delete(alias);
            File.CreateSymbolicLink(alias, wrong);
            Check(File.ReadAllBytes(cue).SequenceEqual(retained), "Only third FILE alias changes, not supplied BIN/CUE paths or text.");
            await Reject<InvalidDataException>(() => plan.RequireFreshAsync());
        }
        finally
        {
            foreach (string owned in new[] { alias, cue, wrong })
                if (File.Exists(owned) || new FileInfo(owned).LinkTarget != null) File.Delete(owned);
            Directory.Delete(root);
        }
    }

    // Exact generated output stream; bounded one-sector cache, no BIN allocation.
    // Hostile variants test actual output EOF/position/cancellation boundaries.
    private sealed class AfterimageStream(string original, NativeId65FreshDiscPlan disc,
        string attack = "", CancellationTokenSource? cancellation = null) : Stream
    {
        private readonly FileStream _source = File.OpenRead(original);
        private byte[] _cached = [];
        private int _cachedLba = -1;
        private long _position;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => 661547040;
        public override long Position { get => _position; set => _position = value >= 0 && value <= Length ? value : throw new IOException("Position outside exact image."); }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            int total = 0;
            while (!buffer.IsEmpty && _position < Length)
            {
                int lba = checked((int)(_position / 2352)), within = (int)(_position % 2352);
                if (lba != _cachedLba)
                {
                    _source.Position = lba * 2352L;
                    byte[] raw = new byte[2352];
                    _source.ReadExactly(raw);
                    _cached = disc.CreateExpectedRawSector(lba, raw);
                    if (attack == "untouched" && lba == 0) _cached[24] ^= 1;
                    _cachedLba = lba;
                }
                int n = Math.Min(buffer.Length, 2352 - within);
                _cached.AsSpan(within, n).CopyTo(buffer);
                buffer = buffer[n..];
                _position += n;
                total += n;
                if (attack == "cancel") cancellation!.Cancel();
            }
            return total;
        }
        public override int ReadByte()
        {
            if (_position == Length)
            {
                if (attack == "position") _position--;
                return attack == "extra-eof" ? 0 : -1;
            }
            Span<byte> one = stackalloc byte[1];
            return Read(one) == 1 ? one[0] : -1;
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            Position = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => Position + offset, SeekOrigin.End => Length + offset, _ => throw new IOException() };
            return Position;
        }
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _source.Dispose(); base.Dispose(disposing); }
    }
}

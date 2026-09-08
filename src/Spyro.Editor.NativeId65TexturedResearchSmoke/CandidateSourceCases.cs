using System.Reflection;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using static Inspection;

internal static class CandidateSourceCases
{
    private const string ImageHash = "998a47930c5f20e36cf87d41bfb9ec3cab237d8d42c443ed706bd50c692611a3";
    internal static async Task Run(string bin, string cue, NativeId65FreshDocumentCompilation compilation)
    {
        var fresh = Get<NativeId65FreshSceneBundle>(compilation, "FreshResourceAncestor");
        var chain = Get<NativeId65NormalResourceChain>(fresh, "ResourceChainAncestor");
        var dark = NativeStaticTerrainTextureSource.Capture(bin, 14, 0);
        var gnasty = NativeStaticTerrainTextureSource.Capture(bin, 70, 0);
        var dark1 = NativeStaticTerrainTextureSource.Capture(bin, 14, 1);
        var texture = await NativeId65FreshTerrainTexturePlan.CaptureAsync(bin, fresh, dark, gnasty);
        var staging = await NativeId65NormalUploadStaging.CaptureTexturedAsync(bin, texture);
        var body = await NativeId65NormalBodyPlan.CaptureTexturedAsync(bin, texture, staging);
        var disc = await NativeId65FreshDiscPlan.CaptureTexturedAsync(bin, body, 19);
        var plan = await NativeId65FreshTexturedResearchCandidatePlan.CaptureAsync(bin, cue, disc);
        Check(plan.ExpectedImageSha256 == ImageHash && plan.ExpectedChangedRawSectors == 1253 &&
            plan.ExpectedImageByteLength == 661547040 && !plan.RuntimeVerified && !plan.NormalCreateBinApproved &&
            !plan.ProjectSaveBindingClaimed && !plan.MaterialDocumentPersistenceBound && !plan.CompleteNativeDependencyClosureProven,
            "Actual fixed-a retains independent full-image golden, not runtime/material-save authority.");
        Call(plan, "RequireAncestry", disc, body, texture, fresh, dark, gnasty);
        var darkAgain = NativeStaticTerrainTextureSource.Capture(bin, 14, 0);
        Refuse<InvalidDataException>(() => Call(plan, "RequireAncestry", disc, body, texture, fresh, darkAgain, gnasty), "Equal source handle cannot replace actual ancestry.");
        var equalTexture = await NativeId65FreshTerrainTexturePlan.CaptureAsync(bin, fresh, darkAgain, gnasty);
        var equalBody = await NativeId65NormalBodyPlan.CaptureTexturedAsync(bin, equalTexture, staging);
        var equalDisc = await NativeId65FreshDiscPlan.CaptureTexturedAsync(bin, equalBody, 19);
        Refuse<InvalidDataException>(() => Call(plan, "RequireAncestry", equalDisc, equalBody, equalTexture, fresh, darkAgain, gnasty), "Equal whole reissue cannot replace existing candidate.");
        var equalPlan = await NativeId65FreshTexturedResearchCandidatePlan.CaptureAsync(bin, cue, equalDisc);
        Check(equalPlan.PlanSha256 == plan.PlanSha256, "Actual equivalent independent issuance may create its own identical candidate.");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var cancellation = await RefuseAsync<OperationCanceledException>(() => NativeId65FreshTexturedResearchCandidatePlan.CaptureAsync("\0", "\0", disc, canceled.Token), "Actual candidate cancellation before path IO.");
        Check(cancellation.CancellationToken == canceled.Token, "Capture preserves token identity.");

        await Reject(compilation.DiscPlan, "untextured A");
        await Reject(await NativeId65FreshDiscPlan.CaptureTexturedAsync(bin, body, 20), "wrong actual selectable track");
        foreach (var selected in new[] {
            await NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(bin, fresh, dark, gnasty),
            await NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(bin, fresh, dark1, gnasty),
            await NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(bin, fresh, gnasty, dark),
            await NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(bin, fresh, dark, dark) })
        {
            var s = await NativeId65NormalUploadStaging.CaptureTexturedAsync(bin, selected);
            var b = await NativeId65NormalBodyPlan.CaptureTexturedAsync(bin, selected, s);
            await Reject(await NativeId65FreshDiscPlan.CaptureTexturedAsync(bin, b, 19), "actual selected material arm, even equal fixed bytes");
        }
        foreach (var scene in new[] {
            await NativeId65FreshSceneBundle.CaptureAsync(bin, chain, new(8976,8960,9216,128), 16),
            await NativeId65FreshSceneBundle.CaptureAsync(bin, chain, new(8960,8960,9216,64), 1) })
        {
            var t = await NativeId65FreshTerrainTexturePlan.CaptureAsync(bin, scene, dark, gnasty);
            var s = await NativeId65NormalUploadStaging.CaptureTexturedAsync(bin, t);
            var b = await NativeId65NormalBodyPlan.CaptureTexturedAsync(bin, t, s);
            await Reject(await NativeId65FreshDiscPlan.CaptureTexturedAsync(bin, b, 19), "actual fixed-b / N1");
        }
        var maxIntent = Spyro.Editor.Core.Editing.NativeId65FreshDocumentIntent.Create(258048,258048,258048,16,16,258049,258049,64,new(80,128,112),new(128,96,64),19);
        var maximum = await NativeId65FreshDocumentCompilation.CaptureAsync(bin, maxIntent);
        Check(Get<NativeId65NormalBodyPlan>(maximum.DiscPlan,"BodyAncestor").DataUsedByteLength == 0xA38, "Genuine untextured A38 witness, not fabricated texture flag.");
        await Reject(maximum.DiscPlan, "untextured A38");

        using var original = File.OpenRead(bin);
        var measured = (NativeId65FreshDiscVerification)Call(plan, "MeasureExpectedImage", original, CancellationToken.None)!;
        Check(measured.OutputSha256 == ImageHash && measured.ChangedSectors == 1253, "Actual candidate helper execution equals preproduction independent full-image golden.");
        original.Position = 0;
        using var expected = new ExpectedImage(bin, disc);
        var verified = (NativeId65FreshDiscVerification)Call(plan, "VerifyImage", original, expected, CancellationToken.None)!;
        Check(verified == measured, "Actual complete Disc readback using a virtual afterimage; not a physical publication.");
        foreach (bool mutateSource in new[] { true, false })
        foreach (int lba in new[] { 0, 22 })
        {
            using var source = new ReadMutation(File.OpenRead(bin), mutateSource ? lba * 2352L + 24 : -1);
            using var after = new ReadMutation(new ExpectedImage(bin, disc), mutateSource ? -1 : lba * 2352L + 24);
            Refuse<InvalidDataException>(() => Call(plan, "VerifyImage", source, after, CancellationToken.None),
                "Actual candidate readback rejects " + (mutateSource ? "source" : "output") + " changed/unchanged-sector mutation.");
            Check(source.CanRead && after.CanRead, "Stream readback never closes caller-owned streams after refusal.");
        }
        MeasureCases.Source(bin, disc, ImageHash); // Same old controlled loop, actual new Disc, no copied writer.
        byte[] expectedCue = Encoding.UTF8.GetBytes("FILE \"native-id65-fresh-textured-fixed-a-track19-r01.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        Check(((byte[])Call(plan,"CopyCueBytes")!).SequenceEqual(expectedCue), "Independent literal new CUE bytes.");
        byte[] receipt = (byte[])Call(plan,"BuildReceipt",verified)!;
        Check(receipt.Length is > 0 and <= 65536, "Actual prospective receipt fits the unchanged bounded companion owner.");
        using var json = JsonDocument.Parse(receipt); var r = json.RootElement;
        Check(r.GetProperty("imageSha256").GetString() == ImageHash && r.GetProperty("changedRawSectors").GetInt32() == 1253 &&
            r.GetProperty("planSha256").GetString() == plan.PlanSha256 && r.GetProperty("cueSha256").GetString() == Hash(expectedCue), "Prospective complete receipt binds actual readback/new companion.");
        foreach (string flag in new[] { "projectSaveBindingClaimed", "materialDocumentPersistenceBound", "runtimeVerified", "normalCreateBinApproved", "completeNativeDependencyClosureProven", "hqRuntimeProven", "peakRuntimeMemoryProven", "checkpointOrForeignStateSupported" })
            Check(!r.GetProperty(flag).GetBoolean(), "Receipt retains false boundary: " + flag);
        receipt[0] ^= 1; expectedCue[0] ^= 1;
        Check(((byte[])Call(plan,"CopyCueBytes")!)[0] == (byte)'F' && ((byte[])Call(plan,"BuildReceipt",verified)!)[0] == (byte)'{', "Companions are defensive copies.");
        object binding = Call(null, Type("NativeId65FreshResearchPublicationBinding"), "FromTextured", plan)!;
        ExtractionCases.ActualOwner(binding, plan);
        Check(Get<object>(binding,"TexturedPlan") == plan && Get<object?>(binding,"WizardPlan") == null && Get<object?>(binding,"SavedContext") == null &&
            Get<bool>(binding,"TracksCommittedFailure") && Get<bool>(binding,"PreserveAnyPostcommitStageOwner") &&
            (CancellationToken)Call(binding,"PostCommitToken",canceled.Token)! == CancellationToken.None &&
            Get<string[]>(binding,"OutputNames").SequenceEqual(new[] { "native-id65-fresh-textured-fixed-a-track19-r01.bin", "native-id65-fresh-textured-fixed-a-track19-r01.receipt.json", "native-id65-fresh-textured-fixed-a-track19-r01.cue" }), "New actual role preserves closed policies/CUE-last without spoofing old roles.");
        string missing = Path.Combine(Path.GetTempPath(), "textured-no-output-" + Guid.NewGuid().ToString("N"));
        await RefuseAsync<InvalidDataException>(() => NativeId65FreshTexturedResearchCandidatePublisher.VerifyAsync(plan, missing), "Missing artifacts still finalize source owners.");
        Check(!Directory.Exists(missing) && !File.Exists(missing), "Read-only Verify never creates missing output.");
        await CandidateSourceFailureCases.ActualInvalidOwners(disc);
        Console.WriteLine($"TEXTURED_CANDIDATE_SOURCE_OK plan={plan.PlanSha256} cue={Hash((byte[])Call(plan,"CopyCueBytes")!)} receipt={Hash((byte[])Call(plan,"BuildReceipt",verified)!)} prospectiveOnly=true physicalOutput=0");

        static async Task Reject(NativeId65FreshDiscPlan actual, string label) =>
            await RefuseAsync<InvalidDataException>(() => NativeId65FreshTexturedResearchCandidatePlan.CaptureAsync("\0", "\0", actual), "Recipe rejection before invalid path IO: " + label);
    }

    private delegate byte[] Sector(int lba, ReadOnlySpan<byte> source);
    private sealed class ReadMutation(Stream inner, long offset) : Stream
    {
        public override bool CanRead => inner.CanRead; public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false; public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override int Read(byte[] b, int o, int n) => Read(b.AsSpan(o,n));
        public override int Read(Span<byte> b)
        {
            long start = inner.Position; int n = inner.Read(b);
            if (offset >= start && offset < start + n) b[(int)(offset-start)] ^= 1;
            return n;
        }
        public override int ReadByte() => inner.ReadByte();
        public override long Seek(long o, SeekOrigin origin) => inner.Seek(o,origin);
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long n) => throw new NotSupportedException();
        public override void Write(byte[] b,int o,int n) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if(disposing)inner.Dispose();base.Dispose(disposing); }
    }
    private sealed class ExpectedImage : Stream
    {
        private readonly FileStream _original; private readonly Sector _sector;
        private long _position; private byte[]? _raw; private int _cached = -1;
        internal ExpectedImage(string path, NativeId65FreshDiscPlan disc)
        { _original = File.OpenRead(path); _sector = disc.GetType().GetMethod("CreateExpectedRawSector", BindingFlags.Instance | BindingFlags.NonPublic)!.CreateDelegate<Sector>(disc); }
        public override bool CanRead => true; public override bool CanSeek => true; public override bool CanWrite => false;
        public override long Length => 661547040;
        public override long Position { get => _position; set { if(value<0||value>Length)throw new IOException();_position=value; } }
        public override int Read(byte[] b,int o,int n) => Read(b.AsSpan(o,n));
        public override int Read(Span<byte> target)
        {
            int done=0;
            while(done<target.Length&&_position<Length)
            {
                int lba=(int)(_position/2352), offset=(int)(_position%2352);
                if(_cached!=lba){_original.Position=lba*2352L;byte[] before=new byte[2352];_original.ReadExactly(before);_raw=_sector(lba,before);_cached=lba;}
                int n=Math.Min(target.Length-done,2352-offset);_raw!.AsSpan(offset,n).CopyTo(target[done..]);done+=n;_position+=n;
            }
            return done;
        }
        protected override void Dispose(bool disposing){if(disposing)_original.Dispose();base.Dispose(disposing);}
        public override long Seek(long o,SeekOrigin origin)=>throw new NotSupportedException();
        public override void Flush()=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();
        public override void Write(byte[] b,int o,int n)=>throw new NotSupportedException();
    }
}

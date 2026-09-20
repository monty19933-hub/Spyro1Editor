using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

internal static class TiledCompilationCases
{
    // Root replaces only with the independently reconstructed oracle result,
    // before running the new production compiler. Never learn this from Core.
    private const string BodyReference = "37157a7248c90665309d8cee21c739a846aca284426cd602c65d627788c06009";
    private const string ImageReference = "7ba0b29b68a1bee415901382c3a3b5345ccb01ca84872502131f68361afdc305";
    private const string OriginalHash = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const long OriginalLength = 661547040;
    private static int _checks;

    internal static async Task Free()
    {
        _checks = 0;
        foreach (var (zCell, expected) in new[] { (44, 0xFFC), (46, 0x1000), (48, 0x1004) })
        {
            byte[] data = NeutralData(BoundaryDocument(zCell));
            Check(data.Length == expected, "Actual ten-tile producer establishes below/full/over allocation fixtures.");
            if (expected <= 0x1000)
                Check(NativeId65TiledNormalDataLayout.DecodeForFixedBody(data).Layout.UsedByteLength == expected,
                    "Canonical measured tiled data can fill the exact existing allocation.");
            else
                Reject<InvalidDataException>(() => NativeId65TiledNormalDataLayout.DecodeForFixedBody(data),
                    "Oversized valid neutral geometry is not fixed-allocation body admission.");
            Reject<InvalidDataException>(() => NativeId65NormalDataLayout.DecodeUntextured(data),
                "Old data admission remains unchanged at new boundaries.");
        }
        foreach (int count in new[] { 1, 2, 3, 4 })
        {
            byte[] data = NeutralData(Document(count));
            Check(NativeId65TiledNormalDataLayout.DecodeForFixedBody(data).Surface.Tiles.Count == count,
                "Admission is actual canonical data size, not a hardcoded tile count or fixture shape.");
            byte[] bad = data.ToArray(); bad[12] ^= 1;
            Reject<InvalidDataException>(() => NativeId65TiledNormalDataLayout.DecodeForFixedBody(bad),
                "Numeric size cannot substitute for canonical geometry.");
        }
        string missing = Missing();
        await RejectAsync<OperationCanceledException>(() => NativeId65TiledDocumentCompilation.CaptureAsync(
            missing, Document(), new(true)), "Canceled compilation refuses before source I/O.");
        await RejectAsync<InvalidDataException>(() => NativeId65TiledDocumentCompilation.CaptureAsync(
            missing, BoundaryDocument(48)), "Whole compilation validates the allocation before original-source I/O.");
        await RejectAsync<ArgumentNullException>(() => NativeId65FreshSceneBundle.CaptureTiledAsync(missing, null!),
            "Missing tiled resource chain refuses before scene source capture.");
        await RejectAsync<ArgumentNullException>(() => NativeId65NormalUploadStaging.CaptureTiledAsync(missing, null!),
            "Missing actual tiled scene refuses before staging source capture.");
        await RejectAsync<ArgumentNullException>(() => NativeId65NormalBodyPlan.CaptureTiledAsync(missing, null!, null!),
            "Missing actual tiled scene/staging refuses before body source capture.");
        await RejectAsync<ArgumentNullException>(() => NativeId65FreshDiscPlan.CaptureTiledAsync(missing, null!),
            "Missing actual tiled body refuses before disc source capture.");
        Reject<ArgumentNullException>(() => NativeId65FreshDiscStreaming.VerifyInverse((NativeId65FreshDiscPlan)null!, Stream.Null, default),
            "Inverse accepts only an actual issued disc.");
        Reject<OperationCanceledException>(() => NativeId65FreshDiscStreaming.VerifyInverse((NativeId65FreshDiscPlan)null!, Stream.Null, new(true)),
            "Canceled inverse refuses before reading any owner.");
        Console.WriteLine($"NATIVE_ID65_TILED_COMPILATION_FREE_OK checks={_checks} sourceRead=false nativeCpuExecuted=false");
    }

    internal static async Task Source(string original)
    {
        await Free();
        RequireOriginal(original);
        try
        {
            var document = Document();
            var compiled = await NativeId65TiledDocumentCompilation.CaptureAsync(original, document);
            var fresh = compiled.FreshResourceAncestor;
            var disc = compiled.DiscPlan;
            var body = disc.BodyAncestor;
            byte[] bodyBytes = disc.CopyBody();
            Check(ReferenceEquals(compiled.Document, document) && compiled.CanonicalSha256 ==
                Hash(NativeFreshLevelDocumentCodec.SerializeCanonical(document)), "Compilation retains the actual full authored document.");
            Check(ReferenceEquals(body.TiledSceneAncestor, fresh) && ReferenceEquals(fresh.TiledSurfaceAncestor!.Document, document),
                "Source-private actual tiled ancestry reaches the complete body/disc, not only matching hashes.");
            Check(body.ResourceProfile == NativeId65NormalResourceProfile.UntexturedGouraudFlatTiles &&
                body.DataUsedByteLength == 0xA58 && body.DataSectorByteLength == 0x1000 &&
                body.ModelRuntimeAddress == 0x8008A924 && body.SceneRuntimeAddress == 0x8009A124 &&
                body.KnownSceneEndAddress == 0x8009D124, "Tiled live addresses derive the actual complete used end.");
            Check(fresh.SceneLayout.Spawn == new NativeId65SceneSpawn(document.SpawnX, document.SpawnY,
                document.SpawnZ, document.Heading) && fresh.SceneLayout.NativeNominalCapacity == 16,
                "Scene spawn and heading come from the same document with fixed N16.");
            Check(bodyBytes.Length == 0x1FB800 && body.LevelDataSha256 == BodyReference && Hash(bodyBytes) == BodyReference,
                "Whole emitted three-tile body matches the independent preimplementation reference.");
            Check(body.Descriptors[1].Offset == 0xDE800 && body.Descriptors[2].Offset == 0xDF800 &&
                body.Descriptors[3].Offset == 0xEF000 &&
                bodyBytes.AsSpan(0xDE800, 0xA58).SequenceEqual(fresh.CopyDataComponents()) &&
                bodyBytes.AsSpan(0xDF258, 0x5A8).IndexOfAnyExcept((byte)0) == -1,
                "Physical packing remains sector-aligned while live data owns only its actual used span.");
            Check(!disc.RuntimeVerified && !disc.PublicationApproved && !disc.NormalCreateBinEnabled &&
                !body.CompleteNativeDependencyClosureProven && !fresh.RuntimeVerified,
                "Source compilation does not promote runtime or publication acceptance.");
            Owners(body);
            string missing = Missing();
            await RejectAsync<InvalidDataException>(() => NativeId65NormalUploadStaging.CaptureAsync(missing, fresh),
                "Old untextured staging refuses actual tiled scene before I/O.");
            await RejectAsync<InvalidDataException>(() => NativeId65NormalBodyPlan.CaptureAsync(missing, fresh, null!),
                "Old untextured body refuses actual tiled scene before I/O.");
            await RejectAsync<InvalidDataException>(() => NativeId65FreshDiscPlan.CaptureAsync(missing, body, 19),
                "Old untextured disc refuses actual tiled body before I/O.");
            await RejectAsync<InvalidDataException>(() => NativeId65FreshDiscPlan.CaptureTexturedAsync(missing, body, 19),
                "Textured disc refuses actual tiled body before I/O.");
            Reject<InvalidDataException>(() => NativeId65NormalResourceRevision.From(fresh), "Old revision factory does not infer tiled approval.");

            var oldIntent = NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 1024, 1024,
                8960, 8960, 64, new(80, 128, 112), new(128, 96, 64), 19);
            var legacy = await NativeId65FreshDocumentCompilation.CaptureAsync(original, oldIntent);
            Check(Hash(legacy.DiscPlan.CopyBody()) == "b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c" &&
                legacy.DiscPlan.BodyPlanSha256 == "48f1f0b4f7740c8a6277a4b440990e1fd26cb609db2153c3c24fc61d70dbbb94",
                "Legacy complete body and source-bound plan remain byte-exact.");
            byte[] independentComplement = legacy.DiscPlan.CopyBody();
            independentComplement.AsSpan(0xDE800, 0x1000).Clear();
            fresh.CopyDataComponents().CopyTo(independentComplement, 0xDE800);
            Check(independentComplement.SequenceEqual(bodyBytes) && disc.CopyOverlay().SequenceEqual(legacy.DiscPlan.CopyOverlay()) &&
                disc.CopyExecutable().SequenceEqual(legacy.DiscPlan.CopyExecutable()),
                "Only the data allocation changes: all header/upload/model/scene/auxiliary/overlay/EXE bytes remain exact for the baseline intent.");
            FullImage(original, disc, legacy.DiscPlan);
            InverseStreams(original, disc);

            var equivalentTile = await NativeId65TiledDocumentCompilation.CaptureAsync(original,
                Make([Tile(0, 8448, 8448, 8704, 1024, 1024)], 8960, 8960, 9216, 64, 19));
            var equivalentDisc = equivalentTile.DiscPlan;
            Check(equivalentDisc.CopyBody().SequenceEqual(legacy.DiscPlan.CopyBody()) &&
                equivalentDisc.CopyOverlay().SequenceEqual(legacy.DiscPlan.CopyOverlay()) &&
                equivalentDisc.CopyExecutable().SequenceEqual(legacy.DiscPlan.CopyExecutable()) &&
                equivalentDisc.CopyWadDirectory().SequenceEqual(legacy.DiscPlan.CopyWadDirectory()) &&
                equivalentDisc.CopyIsoRootDirectory().SequenceEqual(legacy.DiscPlan.CopyIsoRootDirectory()),
                "One tiled document can reproduce every legacy physical hash without acquiring its profile or publication authority.");
            await RejectAsync<InvalidDataException>(() => NativeId65FreshDiscResearchCandidatePlan.CaptureAsync(
                "\0", "\0", equivalentDisc),
                "Byte-identical tiled document refuses at profile validation, before malformed paths can throw ArgumentException.");

            var full = await NativeId65TiledDocumentCompilation.CaptureAsync(original, BoundaryDocument(46));
            var fullBody = full.DiscPlan.BodyAncestor;
            Check(fullBody.DataUsedByteLength == 0x1000 && fullBody.DataSectorByteLength == 0x1000 &&
                fullBody.ModelRuntimeAddress == 0x8008AECC && fullBody.SceneRuntimeAddress == 0x8009A6CC &&
                full.DiscPlan.CopyBody().Length == bodyBytes.Length &&
                fullBody.Owners.All(o => o.ByteLength > 0) && fullBody.PhaseOverlaps.All(o => o.ByteLength > 0),
                "Exact-full canonical allocation emits no fictional zero-length padding owner or overlap.");
            Owners(fullBody);
            Check(full.FreshResourceAncestor.CopyUploadPayload().SequenceEqual(fresh.CopyUploadPayload()) &&
                full.FreshResourceAncestor.CopyModelPayload().SequenceEqual(fresh.CopyModelPayload()),
                "Full data allocation does not consume player, upload or model storage.");

            var otherDocument = Document(2, startX: 8192, width: 1024, heading: 80, track: 2);
            var other = await NativeId65TiledDocumentCompilation.CaptureAsync(original, otherDocument);
            Check(other.FreshResourceAncestor.SceneLayout.Spawn == new NativeId65SceneSpawn(otherDocument.SpawnX,
                otherDocument.SpawnY, otherDocument.SpawnZ, 80) && other.DiscPlan.CopyExecutable().SequenceEqual(
                    NativeId65FreshExecutablePlan.Compile(fresh.TiledSurfaceAncestor!.CopySourceExecutable(), 2).CopyExecutable()),
                "Edited tile count/origin/dimensions/spawn/heading/music flow through actual scene and executable bytes.");
            var stage = await NativeId65NormalUploadStaging.CaptureTiledAsync(original, fresh);
            Check(stage.PairedUploadSha256 == other.FreshResourceAncestor.UploadSha256,
                "Cross-document pairing fixture has equal upload bytes, so upload equality is insufficient.");
            await RejectAsync<InvalidDataException>(() => NativeId65NormalBodyPlan.CaptureTiledAsync(missing,
                other.FreshResourceAncestor, stage), "A same-upload staging object cannot be rebound to another actual document/scene.");
            var equalFresh = await NativeId65FreshSceneBundle.CaptureTiledAsync(original, fresh.ResourceChainAncestor);
            Check(!ReferenceEquals(equalFresh, fresh) && equalFresh.PlanSha256 == fresh.PlanSha256 &&
                equalFresh.DataSha256 == fresh.DataSha256 && equalFresh.UploadSha256 == fresh.UploadSha256,
                "Distinct scene captures with identical document and hashes exercise actual ancestry, not scalar mismatch.");
            await RejectAsync<InvalidDataException>(() => NativeId65NormalBodyPlan.CaptureTiledAsync(missing,
                equalFresh, stage), "Equal-hash issued scene cannot impersonate the staging object's actual retained scene ancestor.");
            await RejectAsync<InvalidDataException>(() => NativeId65NormalUploadStaging.CaptureTiledAsync(missing,
                legacy.FreshResourceAncestor), "New named tiled factory refuses actual legacy ancestry before I/O.");
            var dark = NativeStaticTerrainTextureSource.Capture(original, 14, 0);
            var gnasty = NativeStaticTerrainTextureSource.Capture(original, 70, 0);
            await RejectAsync<InvalidDataException>(() => NativeId65FreshTerrainTexturePlan.CaptureAsync(missing, fresh, dark, gnasty),
                "Valid legacy texture sources cannot enter the fixed two-face allocator with tiled ancestry before I/O.");
            await RejectAsync<InvalidDataException>(() => NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(missing, fresh, dark, dark),
                "Valid selected sources cannot enter the two-face allocator with tiled ancestry before I/O.");
            var tooLargeSurface = await NativeId65TiledSurfaceBundle.CaptureAsync(original, BoundaryDocument(48));
            var companion = await NativeId65CompanionAssetPlan.CaptureAsync(original);
            var tooLargeResources = await NativeId65NormalResourceBundle.CaptureTiledAsync(original, tooLargeSurface, companion);
            var tooLargeChain = NativeId65NormalResourceChain.Compile(tooLargeResources, fresh.ResourceChainAncestor.SceneLists);
            Check(tooLargeChain.CopyDataComponents().Length == 0x1004, "Actual source-issued over-capacity chain remains valid neutral resource content.");
            await RejectAsync<InvalidDataException>(() => NativeId65FreshSceneBundle.CaptureTiledAsync(missing, tooLargeChain),
                "Actual oversized chain cannot acquire scene/body admission before new source I/O.");
            byte[] copy = compiled.CopyCanonicalDocument(); copy.AsSpan().Clear();
            Check(Hash(compiled.CopyCanonicalDocument()) == compiled.CanonicalSha256,
                "Compilation's canonical bytes have no mutable output alias.");
        }
        finally { RequireOriginal(original); }
        Console.WriteLine($"NATIVE_ID65_TILED_COMPILATION_SOURCE_OK checks={_checks} physicalOutputWritten=false nativeCpuExecuted=false");
    }

    private static void Owners(NativeId65NormalBodyPlan body)
    {
        byte[] bytes = body.CopyLevelData(); int end = 0;
        foreach (var owner in body.Owners)
        {
            Check(owner.Offset == end && owner.ByteLength > 0 && Hash(bytes.AsSpan(owner.Offset, owner.ByteLength)) == owner.Sha256,
                "Every body byte belongs to one nonempty ordered exact owner.");
            end += owner.ByteLength;
        }
        Check(end == bytes.Length, "Owners partition the entire body, not only the changed data.");
    }

    private static void FullImage(string path, NativeId65FreshDiscPlan disc, NativeId65FreshDiscPlan legacy)
    {
        using var input = File.OpenRead(path);
        using var forward = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var inverse = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var retained = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] before = new byte[2352]; int changed = 0;
        for (int lba = 0; lba < 281270; lba++)
        {
            input.ReadExactly(before);
            byte[] after = disc.CreateExpectedRawSector(lba, before), original = disc.CreateOriginalRawSector(lba, after);
            Check(original.AsSpan().SequenceEqual(before), "Complete-image inverse preserves every original sector including protected Form2 data.");
            forward.AppendData(after); inverse.AppendData(original); retained.AppendData(legacy.CreateExpectedRawSector(lba, before));
            if (!after.AsSpan().SequenceEqual(before)) changed++;
        }
        Check(input.Position == OriginalLength && input.ReadByte() == -1 && input.Length == OriginalLength,
            "Full image comparison reaches the original actual EOF.");
        string hash = Convert.ToHexStringLower(forward.GetHashAndReset());
        Check(hash == ImageReference && changed == 1253 && Convert.ToHexStringLower(inverse.GetHashAndReset()) == OriginalHash &&
            Convert.ToHexStringLower(retained.GetHashAndReset()) == "1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188",
            "New complete image equals the independent reference; original inverse and legacy image remain exact.");
        Console.WriteLine($"TILED_COMPILATION_PHYSICAL_OK sha256={hash} changed={changed} actualInverse=true physicalOutputWritten=false");
    }

    private static void InverseStreams(string path, NativeId65FreshDiscPlan disc)
    {
        using (var candidate = new CandidateStream(path, disc))
        {
            var measured = NativeId65FreshDiscStreaming.VerifyInverse(disc, candidate, default);
            Check(measured.OutputSha256 == ImageReference && measured.ChangedSectors == 1253 &&
                candidate.Position == OriginalLength, "Shared bounded inverse authenticates the complete candidate and restored original.");
        }
        foreach (int lba in new[] { 0, 22, 55124, 100000 })
        {
            using var candidate = new CandidateStream(path, disc, corruptLba: lba);
            Reject<InvalidDataException>(() => NativeId65FreshDiscStreaming.VerifyInverse(disc, candidate, default),
                "Inverse rejects changed or untouched corruption before returning a complete digest.");
        }
        using (var shortCandidate = new CandidateStream(path, disc, truncated: true))
            Reject<EndOfStreamException>(() => NativeId65FreshDiscStreaming.VerifyInverse(disc, shortCandidate, default),
                "A declared full-length candidate cannot hide a short actual sector read.");
        using (var trailingCandidate = new CandidateStream(path, disc, trailing: true))
            Reject<InvalidDataException>(() => NativeId65FreshDiscStreaming.VerifyInverse(disc, trailingCandidate, default),
                "Declared full length cannot hide a byte beyond actual EOF.");
        Console.WriteLine("TILED_SHARED_INVERSE_OK physicalOutputWritten=false");
    }

    // Read-only generated stream for source tests. The image golden above is
    // independently reconstructed, not learned from this production transform.
    private sealed class CandidateStream : Stream
    {
        private readonly FileStream _source;
        private readonly NativeId65FreshDiscPlan _disc;
        private readonly int _corruptLba;
        private readonly bool _truncated, _trailing;
        private long _position;
        internal CandidateStream(string path, NativeId65FreshDiscPlan disc, int corruptLba = -1,
            bool truncated = false, bool trailing = false)
        { _source = File.OpenRead(path); _disc = disc; _corruptLba = corruptLba; _truncated = truncated; _trailing = trailing; }
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => OriginalLength;
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0) return 0;
            if (_position == OriginalLength)
            {
                if (!_trailing) return 0;
                buffer[0] = 0xCD; _position++; return 1;
            }
            if (_position > OriginalLength || (_truncated && _position >= OriginalLength - 2352)) return 0;
            int lba = checked((int)(_position / 2352)), sectorOffset = (int)(_position % 2352);
            byte[] raw = new byte[2352]; _source.Position = lba * 2352L; _source.ReadExactly(raw);
            byte[] after = _disc.CreateExpectedRawSector(lba, raw);
            if (lba == _corruptLba) after[24] ^= 1;
            int count = Math.Min(buffer.Length, 2352 - sectorOffset);
            after.AsSpan(sectorOffset, count).CopyTo(buffer); _position += count; return count;
        }
        protected override void Dispose(bool disposing) { if (disposing) _source.Dispose(); base.Dispose(disposing); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static NativeFreshLevelDocumentIntent Document(int count = 3, int startX = 8448, int width = 2048,
        int heading = 64, int track = 19) => Make(Enumerable.Range(0, count).Select(i => Tile(i, startX + i * width, 8448, 8704, width, 2048)),
            startX + 512, 8960, 9216, heading, track);
    private static NativeFreshLevelDocumentIntent BoundaryDocument(int zCell) => Make(
        Enumerable.Range(0, 10).Select(i => Tile(i, i * 16, 0, zCell * 4096, 16, 16)), 8, 8, zCell * 4096 + 512, 64, 19);
    private static NativeFreshFlatTileIntent Tile(int index, int x, int y, int z, int width, int depth) =>
        NativeFreshFlatTileIntent.Create((index + 1).ToString("x32"), x, y, z, width, depth, new(80, 128, 112),
            NativeFreshFloorMaterialsIntent.Create(NativeFreshFloorMaterialIntent.BaseFloorColor(), NativeFreshFloorMaterialIntent.BaseFloorColor()));
    private static NativeFreshLevelDocumentIntent Make(IEnumerable<NativeFreshFlatTileIntent> tiles, int x, int y, int z, int heading, int track) =>
        NativeFreshLevelDocumentIntent.CreateTiled(NativeFreshTiledLevelContent.Create(tiles, x, y, z, heading, new(128, 96, 64), track), []);
    private static byte[] NeutralData(NativeFreshLevelDocumentIntent document)
    {
        byte[] surface = NativeFlatTileSurfaceLayout.Compile(document.TiledContent!.Tiles.Select(t => t.ToNativeTile()), 0x406080).CopyBundle();
        byte[] data = new byte[surface.Length + 0x784];
        void W(int at, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(at), value);
        W(0, 8); surface.CopyTo(data, 8);
        int particles = surface.Length + 12, sound = particles + 0x80;
        W(particles, 0x80); W(particles + 4, 10); W(sound, 0x6F8); W(sound + 0x104, 76);
        return data;
    }
    private static void RequireOriginal(string path)
    {
        using var input = File.OpenRead(path);
        Check(input.Length == OriginalLength && Convert.ToHexStringLower(SHA256.HashData(input)) == OriginalHash &&
            input.Position == OriginalLength && input.ReadByte() == -1 && input.Length == OriginalLength,
            "Original complete SHA, length and actual EOF unchanged.");
    }
    private static string Missing() => Path.Combine(Path.GetTempPath(), "spyro-tiled-compile-missing-" + Guid.NewGuid().ToString("N"), "original.bin");
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); _checks++; }
    private static void Reject<T>(Action action, string label) where T : Exception
    { try { action(); } catch (T) { _checks++; return; } throw new InvalidDataException("Expected " + typeof(T).Name + ": " + label); }
    private static async Task RejectAsync<T>(Func<Task> action, string label) where T : Exception
    { try { await action(); } catch (T) { _checks++; return; } throw new InvalidDataException("Expected " + typeof(T).Name + ": " + label); }
}

using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Private original-only image snapshot. Every contributing read after capture
/// is authenticated by its physical LBA digest before any parser sees it.
/// This is not a public metadata issuer or a filesystem/publication lock.
/// </summary>
internal sealed class NativeId65FreshDiscSource
{
    internal const int SectorByteLength = 2352;
    internal const int SectorCount = 281270;
    internal const long ImageByteLength = 661547040;
    internal const string OriginalSha256 = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private readonly byte[] _index;
    private readonly byte[] _executable;
    private readonly byte[] _wadHeader;
    private readonly byte[] _root;
    private readonly IReadOnlyDictionary<int, byte[]> _preimages;

    private NativeId65FreshDiscSource(byte[] index, byte[] executable, byte[] wadHeader,
        byte[] root, IReadOnlyList<NativeFlatIsoFile> rootFiles, Dictionary<int, byte[]> preimages)
    {
        // All arguments are local arrays created by the private exact capture;
        // no caller collection/array is retained or exposed by this object.
        _index = index;
        _executable = executable;
        _wadHeader = wadHeader;
        _root = root;
        RootFiles = Array.AsReadOnly(rootFiles.ToArray());
        _preimages = new System.Collections.ObjectModel.ReadOnlyDictionary<int, byte[]>(preimages);
    }

    internal IReadOnlyList<NativeFlatIsoFile> RootFiles { get; }
    internal byte[] CopyExecutable() => _executable.ToArray();
    internal byte[] CopyWadHeader() => _wadHeader.ToArray();
    internal byte[] CopyRoot() => _root.ToArray();
    internal byte[] CopyPreimage(int lba) => _preimages.TryGetValue(lba, out byte[]? bytes)
        ? bytes.ToArray() : throw new InvalidDataException("Fresh-disc sector has no private changed-preimage owner.");

    internal void RequireRawSector(int lba, ReadOnlySpan<byte> bytes) => RequireSector(_index, lba, bytes);

    internal static Task<NativeId65FreshDiscSource> CaptureAsync(Stream original, CancellationToken token) =>
        CaptureCoreAsync(original, null, token);

    // The test hook shares the actual issuer. It cannot submit metadata/digests,
    // bypass the original SHA, or change the production path-based API.
    internal static Task<NativeId65FreshDiscSource> CaptureForSmokeAsync(Stream original,
        Action? afterIndexCapture, CancellationToken token) => CaptureCoreAsync(original, afterIndexCapture, token);

    private static async Task<NativeId65FreshDiscSource> CaptureCoreAsync(Stream original,
        Action? afterIndexCapture, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(original);
        token.ThrowIfCancellationRequested();
        byte[] index = await CaptureIndexAsync(original, token);
        afterIndexCapture?.Invoke();
        token.ThrowIfCancellationRequested();
        using var authenticated = new IndexedReader(original, index, token);
        DiscLayout layout = DiscImage.DetectLayout(authenticated);
        if (layout != new DiscLayout(2352, 24, 22, 2048))
            throw new InvalidDataException("Fresh-disc capture requires the exact original raw ISO root geometry.");
        IReadOnlyList<NativeFlatIsoFile> rootFiles = NativeWadGrowthSectorPreparation.ReadFlatRootFiles(authenticated, layout);
        RequireFile("WAD.WAD", 37, 0x6927000);
        RequireFile("SCUS_942.28", 53875, 0x66000);
        if (rootFiles.SingleOrDefault(f => f.Name == "PETEXA0.STR")?.Lba != 60000)
            throw new InvalidDataException("Fresh-disc capture lost the original following XA owner.");

        // This existing parser validates every directory/path-table/descriptor
        // owner and complete Form1/gap geometry through the authenticated stream.
        // The record supplies ONLY physical geometry; it is never passed to the
        // NativeSky writer or claimed to be an occupied-entry/payload plan.
        _ = NativeWadGrowthSectorPreparation.Create(authenticated, layout,
            NativeId65FreshDiscGeometry.Compile(0x1FB800).CreateGrowthGeometry());
        byte[] root = DiscImage.ReadFileBytes(authenticated, layout, 22, 0, 2048);
        byte[] header = DiscImage.ReadFileBytes(authenticated, layout, 37, 0, 2048);
        byte[] executable = DiscImage.ReadFileBytes(authenticated, layout, 53875, 0, 0x66000);
        if (!header.AsSpan(79 * 8, 16).SequenceEqual(new byte[16]) ||
            !header.AsSpan(81 * 8, 8).SequenceEqual(Convert.FromHexString("0078C90500200000")))
            throw new InvalidDataException("The exact reserved ID65 pair or occupied adjacent WAD row changed.");
        NativeId65FreshExecutablePlan.RequireOriginal(executable);

        var preimages = new Dictionary<int, byte[]>(1253);
        foreach (int lba in new[] { 22, 37 }.Concat(Enumerable.Range(53874, 1251)))
        {
            token.ThrowIfCancellationRequested();
            byte[] raw = new byte[SectorByteLength];
            authenticated.Position = (long)lba * SectorByteLength;
            authenticated.ReadExactly(raw);
            preimages.Add(lba, raw);
        }
        if (preimages.Count != 1253)
            throw new InvalidDataException("Fresh-disc physical preimage budget changed.");
        await RequireWholeOriginalAsync(original, token);
        token.ThrowIfCancellationRequested();
        return new(index, executable, header, root, rootFiles, preimages);

        void RequireFile(string name, int lba, int length)
        {
            NativeFlatIsoFile? file = rootFiles.SingleOrDefault(f => !f.IsDirectory && f.Name == name);
            if (file == null || file.Lba != lba || file.Size != length)
                throw new InvalidDataException("Fresh-disc source ISO file does not match its original bounded owner.");
        }
    }

    private static async Task<byte[]> CaptureIndexAsync(Stream source, CancellationToken token)
    {
        RequireReadableOriginalLength(source);
        byte[] index = new byte[SectorCount * 32];
        byte[] buffer = new byte[256 * SectorByteLength];
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        source.Position = 0;
        for (int first = 0; first < SectorCount; first += 256)
        {
            token.ThrowIfCancellationRequested();
            int count = Math.Min(256, SectorCount - first);
            await source.ReadExactlyAsync(buffer.AsMemory(0, count * SectorByteLength), token);
            RequireReadableOriginalLength(source);
            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                ReadOnlySpan<byte> sector = buffer.AsSpan(i * SectorByteLength, SectorByteLength);
                hash.AppendData(sector);
                SHA256.HashData(sector, index.AsSpan((first + i) * 32, 32));
            }
        }
        RequireActualEof(source, token);
        if (Convert.ToHexStringLower(hash.GetHashAndReset()) != OriginalSha256)
            throw new InvalidDataException("Fresh-disc source differs from the complete exact original image.");
        token.ThrowIfCancellationRequested();
        return index;
    }

    internal static async Task RequireWholeOriginalAsync(Stream source, CancellationToken token)
    {
        RequireReadableOriginalLength(source);
        byte[] buffer = new byte[256 * SectorByteLength];
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        source.Position = 0;
        for (int first = 0; first < SectorCount; first += 256)
        {
            token.ThrowIfCancellationRequested();
            int count = Math.Min(256, SectorCount - first);
            await source.ReadExactlyAsync(buffer.AsMemory(0, count * SectorByteLength), token);
            RequireReadableOriginalLength(source);
            hash.AppendData(buffer.AsSpan(0, count * SectorByteLength));
        }
        RequireActualEof(source, token);
        if (Convert.ToHexStringLower(hash.GetHashAndReset()) != OriginalSha256)
            throw new InvalidDataException("Fresh-disc original source changed at a complete freshness boundary.");
    }

    internal static void RequireActualEof(Stream stream, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        RequireReadableOriginalLength(stream);
        if (stream.Position != ImageByteLength || stream.ReadByte() != -1 ||
            stream.Position != ImageByteLength || stream.Length != ImageByteLength)
            throw new InvalidDataException("Fresh-disc physical stream has changed length, position or actual EOF.");
        token.ThrowIfCancellationRequested();
    }

    private static void RequireReadableOriginalLength(Stream stream)
    {
        if (!stream.CanRead || !stream.CanSeek || stream.Length != ImageByteLength)
            throw new InvalidDataException("Fresh-disc capture requires an exact-length seekable original source.");
    }

    private static void RequireSector(byte[] index, int lba, ReadOnlySpan<byte> bytes)
    {
        if (lba < 0 || lba >= SectorCount || bytes.Length != SectorByteLength || index.Length != SectorCount * 32)
            throw new InvalidDataException("Fresh-disc physical sector identity or length is out of bounds.");
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(bytes, digest);
        if (!digest.SequenceEqual(index.AsSpan(lba * 32, 32)))
            throw new InvalidDataException("Fresh-disc contributing sector failed its private authenticated source-LBA digest.");
    }

    private sealed class IndexedReader(Stream source, byte[] index, CancellationToken token) : Stream
    {
        private long _position;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length
        {
            get
            {
                RequireReadableOriginalLength(source);
                return ImageByteLength;
            }
        }
        public override long Position
        {
            get => _position;
            set
            {
                token.ThrowIfCancellationRequested();
                if (value < 0 || value > Length)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _position = value;
            }
        }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> destination)
        {
            token.ThrowIfCancellationRequested();
            int count = checked((int)Math.Min(destination.Length, Length - Position));
            int copied = 0;
            byte[] raw = new byte[SectorByteLength];
            while (copied < count)
            {
                token.ThrowIfCancellationRequested();
                int lba = checked((int)(Position / SectorByteLength));
                int offset = checked((int)(Position % SectorByteLength));
                int part = Math.Min(SectorByteLength - offset, count - copied);
                source.Position = (long)lba * SectorByteLength;
                source.ReadExactly(raw);
                RequireReadableOriginalLength(source);
                RequireSector(index, lba, raw);
                raw.AsSpan(offset, part).CopyTo(destination[copied..]);
                copied += part;
                Position += part;
            }
            return count;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(destination.Span));
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(buffer, offset, count));
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
}

using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Analysis;

public enum NativeDiscRevision { UsaScus94228, JapanScps10083 }

/// <summary>Directory ownership only, not a portable model/package extent.</summary>
public sealed record NativeRegionalWadEntry(int Index, int Offset, int ByteLength);

/// <summary>
/// A read-only, exact-revision source session for regional content research.
/// Never substitutes Japanese bytes for a USA certificate or grants export authority.
/// Call VerifyUnchanged after consuming the bytes and before publishing findings.
/// Returned byte arrays are detached; this session owns only a read handle.
/// </summary>
public sealed class NativeRegionalSourceSession : IDisposable
{
    private const int MaximumReadBytes = 16 * 1024 * 1024;
    private readonly string _path;
    private readonly FileStream _image;
    private readonly DiscLayout _layout;
    private readonly Profile _profile;
    private bool _disposed;

    private sealed record Profile(string Serial, long ImageLength, string ImageHash,
        int WadLba, int WadLength, int ExeLba, int ExeLength, string ExeHash);

    private NativeRegionalSourceSession(string path, FileStream image, DiscLayout layout,
        NativeDiscRevision revision, Profile profile, NativeRegionalWadEntry[] entries)
    {
        _path = path; _image = image; _layout = layout; _profile = profile;
        Revision = revision; Entries = Array.AsReadOnly(entries);
    }

    public NativeDiscRevision Revision { get; }
    public string Serial => _profile.Serial;
    public string SourceImageSha256 => _profile.ImageHash;
    public long SourceImageByteLength => _profile.ImageLength;
    public string ExecutableSha256 => _profile.ExeHash;
    public int ExecutableByteLength => _profile.ExeLength;
    public int WadByteLength => _profile.WadLength;
    public IReadOnlyList<NativeRegionalWadEntry> Entries { get; }
    public bool NativeImportApproved => false;

    public static NativeRegionalSourceSession Open(string sourceImagePath, NativeDiscRevision revision)
    {
        Profile profile = RequireProfile(revision);
        string path = Path.GetFullPath(sourceImagePath);
        FileStream image = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            RequireIdentity(image, profile);
            DiscLayout layout = DiscImage.DetectLayout(image);
            if (layout.SectorSize != 2352 || layout.UserOffset != 24)
                throw new InvalidDataException("Regional source requires the authenticated MODE2/2352 image.");
            DiscFileRecord wad = DiscImage.FindRootFileRecord(image, layout, n => n == "WAD.WAD");
            DiscFileRecord exe = DiscImage.FindRootFileRecord(image, layout, n => n == profile.Serial);
            if (wad.Lba != profile.WadLba || wad.Size != profile.WadLength ||
                exe.Lba != profile.ExeLba || exe.Size != profile.ExeLength)
                throw new InvalidDataException("Regional ISO file ownership differs from the authenticated revision.");
            byte[] executable = DiscImage.ReadFileBytes(image, layout, exe.Lba, 0, exe.Size);
            if (Hash(executable) != profile.ExeHash || !executable.AsSpan(0, 8).SequenceEqual("PS-X EXE"u8))
                throw new InvalidDataException("Regional executable differs from its exact source owner.");
            byte[] directory = DiscImage.ReadFileBytes(image, layout, wad.Lba, 0, 2048);
            NativeRegionalWadEntry[] entries = DecodeDirectory(directory, wad.Size);
            var session = new NativeRegionalSourceSession(path, image, layout, revision, profile, entries);
            session.VerifyUnchanged();
            return session;
        }
        catch { image.Dispose(); throw; }
    }

    public byte[] ReadWadEntry(int index)
    {
        ThrowIfDisposed();
        if ((uint)index >= Entries.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        NativeRegionalWadEntry entry = Entries[index];
        if (entry.ByteLength == 0)
            throw new InvalidDataException("An empty WAD slot is not an extractable object or archive.");
        return ReadWadRange(entry.Offset, entry.ByteLength);
    }

    // Shared runtime copies may deliberately cross their directory-entry end.
    // Such reads must be requested explicitly; directory lengths are never
    // silently enlarged or interpreted as proven per-model package boundaries.
    public byte[] ReadWadRange(long offset, int length)
    {
        ThrowIfDisposed();
        RequireRange(offset, length, _profile.WadLength);
        return DiscImage.ReadFileBytes(_image, _layout, _profile.WadLba, offset, length);
    }

    public byte[] ReadExecutableRange(long offset, int length)
    {
        ThrowIfDisposed();
        RequireRange(offset, length, _profile.ExeLength);
        return DiscImage.ReadFileBytes(_image, _layout, _profile.ExeLba, offset, length);
    }

    public void VerifyUnchanged()
    {
        ThrowIfDisposed();
        RequireIdentity(_image, _profile);
        // A held handle alone cannot detect replacement of the named source.
        using FileStream named = new(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        RequireIdentity(named, _profile);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _image.Dispose();
    }

    internal static NativeRegionalWadEntry[] DecodeDirectory(ReadOnlySpan<byte> directory, int wadLength)
    {
        if (directory.Length != 2048 || wadLength < 2048 || wadLength % 2048 != 0)
            throw new InvalidDataException("A regional WAD directory needs its complete sector and aligned owner.");
        var entries = new NativeRegionalWadEntry[256];
        for (int i = 0; i < entries.Length; i++)
        {
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(directory[(i * 8)..]);
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(directory[(i * 8 + 4)..]);
            // Japanese unused slots retain the next allocation offset. Keep
            // that fact, but never parse an empty slot as bytes at that offset.
            if ((offset | length) % 2048 != 0 || offset > wadLength ||
                length > wadLength || offset + (long)length > wadLength || length != 0 && offset < 2048)
                throw new InvalidDataException("A regional WAD entry exceeds or enters its directory owner.");
            entries[i] = new(i, (int)offset, (int)length);
        }
        NativeRegionalWadEntry[] populated = entries.Where(e => e.ByteLength != 0).OrderBy(e => e.Offset).ToArray();
        for (int i = 1; i < populated.Length; i++)
            if (populated[i - 1].Offset + (long)populated[i - 1].ByteLength > populated[i].Offset)
                throw new InvalidDataException("Regional directory entries alias or overlap; ownership is not established.");
        return entries;
    }

    internal static void RequireRange(long offset, int length, int ownerLength)
    {
        if (offset < 0 || length <= 0 || length > MaximumReadBytes || offset > ownerLength ||
            length > ownerLength - offset)
            throw new InvalidDataException("Regional source read exceeds its declared owner or bounded allocation limit.");
    }

    private static Profile RequireProfile(NativeDiscRevision revision) => revision switch
    {
        NativeDiscRevision.UsaScus94228 => new("SCUS_942.28", 661547040,
            "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37",
            37, 0x6927000, 53875, 0x66000,
            "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"),
        NativeDiscRevision.JapanScps10083 => new("SCPS_100.83", 727179600,
            "1bcf4353d6a4d4d0db152d2ebd60879ef85516feaf170f364ff419c54f599ea7",
            500, 112793600, 24, 0x70000,
            "a67c21f441deb3a4df26099d78ddaa29cc66b927a7387718a678eee71664cee4"),
        _ => throw new ArgumentOutOfRangeException(nameof(revision))
    };

    private static void RequireIdentity(FileStream stream, Profile profile)
    {
        if (stream.Length != profile.ImageLength)
            throw new InvalidDataException("Regional source image length differs from the selected revision.");
        stream.Position = 0;
        if (Convert.ToHexStringLower(SHA256.HashData(stream)) != profile.ImageHash ||
            stream.Length != profile.ImageLength)
            throw new InvalidDataException("Regional source requires the exact unchanged selected revision; cross-region substitution is refused.");
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

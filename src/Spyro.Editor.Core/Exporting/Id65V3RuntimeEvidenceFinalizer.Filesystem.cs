using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal static partial class Id65V3RuntimeEvidenceFinalizer
{
    private const ushort DarwinFileTypeMask = 0xF000;
    private const ushort DarwinRegularFile = 0x8000;
    private const ushort DarwinDirectory = 0x4000;
    private const int LockExclusive = 0x02;
    private const int LockNonBlocking = 0x04;
    private const int LockUnlock = 0x08;
    private const int OpenNoFollow = 0x00000100;
    private const int OpenCreate = 0x00000200;
    private const int OpenExclusive = 0x00000800;
    private const int OpenDirectory = 0x00100000;
    private const int OpenCloseOnExec = 0x01000000;
    private const int OpenReadWrite = 0x00000002;
    private const int AtSymlinkNoFollow = 0x0020;
    private const int FcntlGetPath = 50;
    private const int MaximumDarwinPathBytes = 1024;
    private const int GroupOrOtherWrite = 0x12;
    private const int StickyBit = 0x200;
    private const uint RenameExclusive = 0x00000004;

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    private struct DarwinStat
    {
        [FieldOffset(0)] internal int Device;
        [FieldOffset(4)] internal ushort Mode;
        [FieldOffset(6)] internal ushort LinkCount;
        [FieldOffset(8)] internal ulong Inode;
        [FieldOffset(16)] internal uint UserId;
        [FieldOffset(20)] internal uint GroupId;
        [FieldOffset(48)] internal long ModifiedSeconds;
        [FieldOffset(56)] internal long ModifiedNanoseconds;
        [FieldOffset(64)] internal long ChangedSeconds;
        [FieldOffset(72)] internal long ChangedNanoseconds;
        [FieldOffset(96)] internal long ByteLength;
    }

    private sealed record StableIdentity(
        int Device,
        ulong Inode,
        ushort Mode,
        ushort LinkCount,
        uint UserId,
        long ByteLength,
        long ModifiedSeconds,
        long ModifiedNanoseconds,
        long ChangedSeconds,
        long ChangedNanoseconds)
    {
        internal bool SameFile(StableIdentity other) =>
            Device == other.Device && Inode == other.Inode;
    }

    internal sealed record StableFileRead(
        string FullPath,
        string Sha256,
        long ByteLength,
        byte[]? Bytes,
        int Device,
        ulong Inode,
        ushort Mode,
        ushort LinkCount,
        uint UserId,
        long ModifiedSeconds,
        long ModifiedNanoseconds,
        long ChangedSeconds,
        long ChangedNanoseconds);

    private sealed class CoordinatorLease : IDisposable
    {
        private readonly string _path;
        private readonly FileStream _stream;
        private readonly StableIdentity _identity;
        private bool _locked;

        internal CoordinatorLease(string captureStagingRoot)
        {
            _path = Path.Combine(captureStagingRoot, ".capture-coordinator.lock");
            RequireTrustedAbsolutePath(
                _path,
                targetIsDirectory: false,
                allowMissingLeaf: false,
                "coordinator lease");
            RequireNoSymlinkSegments(_path, allowMissingLeaf: false);
            StableIdentity pathBefore = ReadPathIdentity(_path, "coordinator lease before open");
            RequireOwnedRegular(pathBefore, 0x180, "coordinator lease before open");
            _stream = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            _identity = ReadHandleIdentity(_stream, "coordinator lease handle");
            RequireOwnedRegular(_identity, 0x180, "coordinator lease handle");
            if (!pathBefore.SameFile(_identity))
            {
                _stream.Dispose();
                throw new InvalidDataException(
                    "The capture coordinator lease changed between path and handle validation.");
            }
            if (Flock(checked((int)_stream.SafeFileHandle.DangerousGetHandle()),
                    LockExclusive | LockNonBlocking) != 0)
            {
                int error = Marshal.GetLastPInvokeError();
                _stream.Dispose();
                throw new IOException(
                    $"Another capture coordinator or finalizer owns the lease (errno {error}).");
            }
            _locked = true;
            RequireUnchanged();
        }

        internal void RequireUnchanged()
        {
            StableIdentity handle = ReadHandleIdentity(_stream, "coordinator lease recheck");
            StableIdentity path = ReadPathIdentity(_path, "coordinator lease path recheck");
            RequireOwnedRegular(handle, 0x180, "coordinator lease recheck");
            RequireOwnedRegular(path, 0x180, "coordinator lease path recheck");
            if (handle != _identity || !handle.SameFile(path))
                throw new InvalidDataException("The capture coordinator lease identity changed.");
        }

        public void Dispose()
        {
            if (_locked)
            {
                _ = Flock(
                    checked((int)_stream.SafeFileHandle.DangerousGetHandle()),
                    LockUnlock);
                _locked = false;
            }
            _stream.Dispose();
        }
    }

    internal static StableFileRead ReadStableOwnedFile(
        string path,
        int expectedMode,
        long maximumBytes,
        bool keepBytes) =>
        ReadStableTrustedFileCore(
            path,
            maximumBytes,
            keepBytes,
            minimumBytes: 1,
            expectedMode: expectedMode,
            context: "stable owned file read");

    internal static StableFileRead ReadStableTrustedFile(
        string path,
        long maximumBytes,
        bool keepBytes,
        string context) =>
        ReadStableTrustedFileCore(
            path,
            maximumBytes,
            keepBytes,
            minimumBytes: 0,
            expectedMode: null,
            context: context);

    internal static T InspectStableOwnedFileHandle<T>(
        string path,
        int expectedMode,
        long expectedByteLength,
        Func<FileStream, T> inspector,
        string context)
    {
        RequireDarwin();
        if (expectedByteLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(expectedByteLength));
        ArgumentNullException.ThrowIfNull(inspector);
        string full = Path.GetFullPath(path);
        RequireTrustedAbsolutePath(
            full,
            targetIsDirectory: false,
            allowMissingLeaf: false,
            context);
        RequireNoSymlinkSegments(full, allowMissingLeaf: false);
        StableIdentity pathBefore = ReadPathIdentity(full, $"{context} before open");
        RequireOwnedRegular(pathBefore, expectedMode, $"{context} before open");
        if (pathBefore.ByteLength != expectedByteLength)
            throw new InvalidDataException($"The exact-length file '{full}' changed.");

        using FileStream stream = new(full, FileMode.Open, FileAccess.Read, FileShare.Read);
        StableIdentity before = ReadHandleIdentity(stream, $"{context} handle before inspection");
        RequireOwnedRegular(before, expectedMode, $"{context} handle before inspection");
        if (pathBefore != before || before.ByteLength != expectedByteLength ||
            stream.Length != expectedByteLength)
        {
            throw new InvalidDataException(
                $"The file '{full}' changed between path and handle validation.");
        }

        T result = inspector(stream);

        StableIdentity after = ReadHandleIdentity(stream, $"{context} handle after inspection");
        StableIdentity pathAfter = ReadPathIdentity(full, $"{context} path after inspection");
        RequireOwnedRegular(after, expectedMode, $"{context} handle after inspection");
        RequireOwnedRegular(pathAfter, expectedMode, $"{context} path after inspection");
        if (before != after || after != pathAfter || after.ByteLength != expectedByteLength ||
            stream.Length != expectedByteLength)
        {
            throw new InvalidDataException(
                $"The file '{full}' changed or rebound during stable-handle inspection.");
        }
        return result;
    }

    internal static void RequireOwnedDirectoryPath(
        string path,
        int expectedMode,
        string context) =>
        RequireOwnedPrivateDirectory(path, expectedMode, context);

    private static StableFileRead ReadStableTrustedFileCore(
        string path,
        long maximumBytes,
        bool keepBytes,
        long minimumBytes,
        int? expectedMode,
        string context)
    {
        RequireDarwin();
        if (maximumBytes < minimumBytes)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        string full = Path.GetFullPath(path);
        RequireTrustedAbsolutePath(
            full,
            targetIsDirectory: false,
            allowMissingLeaf: false,
            context);
        RequireNoSymlinkSegments(full, allowMissingLeaf: false);
        StableIdentity pathBefore = ReadPathIdentity(full, $"{context} before open");
        RequireTrustedRegular(pathBefore, $"{context} before open");
        if (expectedMode is int exactPathMode)
            RequireOwnedRegular(pathBefore, exactPathMode, $"{context} before open");
        if (pathBefore.ByteLength < minimumBytes || pathBefore.ByteLength > maximumBytes)
            throw new InvalidDataException($"The bounded file '{full}' has an invalid byte length.");

        using FileStream stream = new(full, FileMode.Open, FileAccess.Read, FileShare.Read);
        StableIdentity before = ReadHandleIdentity(stream, $"{context} before read");
        RequireTrustedRegular(before, $"{context} before read");
        if (expectedMode is int exactHandleMode)
            RequireOwnedRegular(before, exactHandleMode, $"{context} before read");
        if (pathBefore != before)
            throw new InvalidDataException($"The file '{full}' changed between path and handle validation.");

        byte[]? bytes = null;
        string sha256;
        if (keepBytes)
        {
            bytes = new byte[checked((int)before.ByteLength)];
            stream.ReadExactly(bytes);
            sha256 = HashBytes(bytes);
        }
        else
        {
            sha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        if (stream.Position != before.ByteLength)
            throw new InvalidDataException($"The read of '{full}' did not consume its exact open handle.");

        StableIdentity after = ReadHandleIdentity(stream, $"{context} after read");
        StableIdentity pathAfter = ReadPathIdentity(full, $"{context} path after read");
        RequireTrustedRegular(after, $"{context} after read");
        RequireTrustedRegular(pathAfter, $"{context} path after read");
        if (expectedMode is int exactAfterMode)
        {
            RequireOwnedRegular(after, exactAfterMode, $"{context} after read");
            RequireOwnedRegular(pathAfter, exactAfterMode, $"{context} path after read");
        }
        if (before != after || after != pathAfter || after.ByteLength != stream.Length)
            throw new InvalidDataException($"The file '{full}' changed during stable-handle readback.");
        return new(
            full,
            sha256,
            after.ByteLength,
            bytes,
            after.Device,
            after.Inode,
            after.Mode,
            after.LinkCount,
            after.UserId,
            after.ModifiedSeconds,
            after.ModifiedNanoseconds,
            after.ChangedSeconds,
            after.ChangedNanoseconds);
    }

    private static void RequireOwnedPrivateDirectory(string path, int expectedMode, string context)
    {
        RequireTrustedAbsolutePath(
            path,
            targetIsDirectory: true,
            allowMissingLeaf: false,
            context);
        RequireNoSymlinkSegments(path, allowMissingLeaf: false);
        StableIdentity identity = ReadPathIdentity(path, context);
        if ((identity.Mode & DarwinFileTypeMask) != DarwinDirectory ||
            identity.UserId != GetUid() || (identity.Mode & 0x0FFF) != expectedMode)
        {
            throw new InvalidDataException(
                $"{context} must be a current-user directory with mode {Convert.ToString(expectedMode, 8)}.");
        }
    }

    private static void RequireOwnedRegular(StableIdentity identity, int expectedMode, string context)
    {
        if ((identity.Mode & DarwinFileTypeMask) != DarwinRegularFile ||
            identity.LinkCount != 1 || identity.UserId != GetUid() ||
            (identity.Mode & 0x0FFF) != expectedMode)
        {
            throw new InvalidDataException(
                $"{context} must be one current-user regular file with mode " +
                $"{Convert.ToString(expectedMode, 8)} and st_nlink == 1 " +
                $"(actual-type=0x{identity.Mode & DarwinFileTypeMask:x4}, " +
                $"mode={Convert.ToString(identity.Mode & 0x0FFF, 8)}, " +
                $"links={identity.LinkCount}, uid={identity.UserId}, expected-uid={GetUid()}).");
        }
    }

    private static void RequireTrustedRegular(StableIdentity identity, string context)
    {
        if ((identity.Mode & DarwinFileTypeMask) != DarwinRegularFile ||
            identity.LinkCount != 1 || identity.UserId != GetUid() ||
            (identity.Mode & GroupOrOtherWrite) != 0)
        {
            throw new InvalidDataException(
                $"{context} must be one current-user regular file without group/other write permission " +
                $"and with st_nlink == 1 (actual-type=0x{identity.Mode & DarwinFileTypeMask:x4}, " +
                $"mode={Convert.ToString(identity.Mode & 0x0FFF, 8)}, links={identity.LinkCount}, " +
                $"uid={identity.UserId}, expected-uid={GetUid()}).");
        }
    }

    private static void RequireNoSymlinkSegments(string path, bool allowMissingLeaf)
    {
        RequireDarwin();
        if (!Path.IsPathFullyQualified(path))
            throw new InvalidDataException("A filesystem boundary path is not absolute.");
        string full = Path.GetFullPath(path);
        string current = Path.GetPathRoot(full) ?? throw new InvalidDataException("Path has no root.");
        string[] segments = full[(Path.GetPathRoot(full)?.Length ?? 0)..]
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < segments.Length; index++)
        {
            current = Path.Combine(current, segments[index]);
            if (LStatDarwin(current, out DarwinStat status) != 0)
            {
                int error = Marshal.GetLastPInvokeError();
                if (allowMissingLeaf && index == segments.Length - 1 && error == 2)
                    return;
                throw new IOException($"Could not lstat path segment '{current}' (errno {error}).");
            }
            const ushort symbolicLink = 0xA000;
            if ((status.Mode & DarwinFileTypeMask) == symbolicLink)
                throw new InvalidDataException($"Filesystem boundary path contains symlink '{current}'.");
            if (index < segments.Length - 1 &&
                (status.Mode & DarwinFileTypeMask) != DarwinDirectory)
            {
                throw new InvalidDataException($"Filesystem boundary ancestry '{current}' is not a directory.");
            }
        }
    }

    private static StableIdentity ReadPathIdentity(string path, string context)
    {
        if (LStatDarwin(path, out DarwinStat status) != 0)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not lstat {context} (errno {error}).");
        }
        return ToIdentity(status);
    }

    private static StableIdentity ReadHandleIdentity(FileStream stream, string context)
    {
        int descriptor = checked((int)stream.SafeFileHandle.DangerousGetHandle());
        if (FStatDarwin(descriptor, out DarwinStat status) != 0)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not fstat {context} (errno {error}).");
        }
        return ToIdentity(status);
    }

    private static StableIdentity ReadDescriptorIdentity(int descriptor, string context)
    {
        if (FStatDarwin(descriptor, out DarwinStat status) != 0)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not fstat {context} (errno {error}).");
        }
        return ToIdentity(status);
    }

    private static bool TryReadAtIdentity(
        int directoryDescriptor,
        string leaf,
        out StableIdentity? identity,
        out int error)
    {
        if (FStatAtDarwin(
                directoryDescriptor,
                leaf,
                out DarwinStat status,
                AtSymlinkNoFollow) == 0)
        {
            identity = ToIdentity(status);
            error = 0;
            return true;
        }
        identity = null;
        error = Marshal.GetLastPInvokeError();
        return false;
    }

    private static StableIdentity ToIdentity(DarwinStat status) => new(
        status.Device,
        status.Inode,
        status.Mode,
        status.LinkCount,
        status.UserId,
        status.ByteLength,
        status.ModifiedSeconds,
        status.ModifiedNanoseconds,
        status.ChangedSeconds,
        status.ChangedNanoseconds);

    private static string RequireRepositoryRootForFinalizer(string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot) || !Path.IsPathFullyQualified(repositoryRoot))
            throw new InvalidDataException("The finalizer repository root must be absolute.");
        string full = Path.GetFullPath(repositoryRoot);
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException("The finalizer repository root is missing.");
        RequireTrustedRepositoryRoot(full, "finalizer repository root");
        return full;
    }

    internal static void RequireTrustedRepositoryRoot(string repositoryRoot, string context)
    {
        string full = Path.GetFullPath(repositoryRoot);
        RequireTrustedAbsolutePath(
            full,
            targetIsDirectory: true,
            allowMissingLeaf: false,
            context);
        StableIdentity identity = ReadPathIdentity(full, context);
        if ((identity.Mode & DarwinFileTypeMask) != DarwinDirectory ||
            identity.UserId != GetUid() ||
            (identity.Mode & GroupOrOtherWrite) != 0)
        {
            throw new InvalidDataException(
                $"{context} must be a current-user real directory without group/other write permission.");
        }
    }

    internal static void RequireTrustedAbsolutePath(
        string path,
        bool targetIsDirectory,
        bool allowMissingLeaf,
        string context)
    {
        RequireDarwin();
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidDataException($"{context} trust-chain path is not absolute.");
        string full = Path.GetFullPath(path);
        RequireNoSymlinkSegments(full, allowMissingLeaf);
        string directory = targetIsDirectory
            ? full
            : Path.GetDirectoryName(full) ??
                throw new InvalidDataException($"{context} trust-chain file has no parent.");
        string root = Path.GetPathRoot(directory) ??
            throw new InvalidDataException($"{context} trust-chain path has no filesystem root.");
        List<string> directories = [root];
        string current = root;
        foreach (string segment in directory[root.Length..].Split(
                     Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            directories.Add(current);
        }

        StableIdentity[] identities = directories.Select((item, index) =>
        {
            StableIdentity identity = ReadPathIdentity(item, $"{context} directory segment {index}");
            if ((identity.Mode & DarwinFileTypeMask) != DarwinDirectory ||
                (identity.UserId != 0 && identity.UserId != GetUid()))
            {
                throw new InvalidDataException(
                    $"{context} directory chain contains a non-directory or foreign-owned segment '{item}'.");
            }
            return identity;
        }).ToArray();

        for (int index = 0; index < identities.Length; index++)
        {
            StableIdentity identity = identities[index];
            if ((identity.Mode & GroupOrOtherWrite) == 0)
                continue;
            bool protectedByStickyParent = identity.UserId == 0 &&
                (identity.Mode & StickyBit) != 0 &&
                NextEntryIsCurrentOwnedAndProtected(
                    full,
                    targetIsDirectory,
                    allowMissingLeaf,
                    directories,
                    identities,
                    index);
            if (!protectedByStickyParent)
            {
                throw new InvalidDataException(
                    $"{context} directory chain segment '{directories[index]}' has unsafe group/other write permission.");
            }
        }
    }

    private static void RequireTrustedExternalAttestationPath(string path)
    {
        string full = Path.GetFullPath(path);
        RequireTrustedAbsolutePath(
            full,
            targetIsDirectory: false,
            allowMissingLeaf: false,
            "external human attestation");
        string parent = Path.GetDirectoryName(full) ??
            throw new InvalidDataException("The external human attestation has no parent directory.");
        StableIdentity parentIdentity = ReadPathIdentity(
            parent,
            "external human-attestation parent");
        if ((parentIdentity.Mode & DarwinFileTypeMask) != DarwinDirectory)
            throw new InvalidDataException("The external human-attestation parent is not a real directory.");
        if (parentIdentity.UserId == GetUid() &&
            (parentIdentity.Mode & GroupOrOtherWrite) == 0)
        {
            return;
        }
        if (parentIdentity.UserId == 0 && (parentIdentity.Mode & StickyBit) != 0)
        {
            StableIdentity leaf = ReadPathIdentity(full, "external human attestation");
            RequireOwnedRegular(leaf, 0x180, "external human attestation under sticky root parent");
            return;
        }
        throw new InvalidDataException(
            "The external human-attestation parent has unsafe group/other write permission or is not " +
            "a current-user directory (the only exception is a root-owned sticky parent protecting an " +
            "existing current-user mode-600 single-link file).");
    }

    private static bool NextEntryIsCurrentOwnedAndProtected(
        string full,
        bool targetIsDirectory,
        bool allowMissingLeaf,
        IReadOnlyList<string> directories,
        IReadOnlyList<StableIdentity> identities,
        int index)
    {
        if (index + 1 < identities.Count)
        {
            StableIdentity next = identities[index + 1];
            return next.UserId == GetUid() &&
                (next.Mode & DarwinFileTypeMask) == DarwinDirectory &&
                (next.Mode & GroupOrOtherWrite) == 0;
        }
        if (targetIsDirectory || allowMissingLeaf)
            return false;
        StableIdentity leaf = ReadPathIdentity(full, "sticky-parent protected leaf");
        return (leaf.Mode & DarwinFileTypeMask) == DarwinRegularFile &&
            leaf.UserId == GetUid() && leaf.LinkCount == 1 &&
            (leaf.Mode & 0x0FFF) == 0x180 &&
            Path.GetDirectoryName(full) == directories[index];
    }

    private static StableFileRead AtomicPublishNoReplace(
        string destination,
        ReadOnlySpan<byte> bytes,
        Action<string>? beforeRename = null,
        Action? afterRenameBeforeDirectoryFsync = null,
        Action<string>? afterRename = null)
    {
        RequireDarwin();
        string full = Path.GetFullPath(destination);
        string parent = Path.GetDirectoryName(full) ??
            throw new InvalidDataException("The evidence destination has no parent directory.");
        string destinationLeaf = Path.GetFileName(full);
        RequireTrustedAbsolutePath(
            full,
            targetIsDirectory: false,
            allowMissingLeaf: true,
            "fixed canonical evidence destination");
        RequireNoSymlinkSegments(parent, allowMissingLeaf: false);
        RequireNoSymlinkSegments(full, allowMissingLeaf: true);
        if (File.Exists(full) || Directory.Exists(full))
            throw new IOException("The fixed canonical evidence destination already exists.");

        string tempLeaf = ".id65-v3-runtime-evidence.tmp-" + Guid.NewGuid().ToString("N");
        string temp = Path.Combine(parent, tempLeaf);
        byte[] payload = bytes.ToArray();
        string expectedSha256 = HashBytes(payload);
        int directoryDescriptor = OpenDarwin(parent, OpenNoFollow | OpenDirectory, 0);
        if (directoryDescriptor < 0)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not open the evidence parent directory (errno {error}).");
        }
        StableIdentity parentHandle;
        try
        {
            parentHandle = ReadDescriptorIdentity(
                directoryDescriptor,
                "evidence parent directory");
            StableIdentity parentPath = ReadPathIdentity(parent, "evidence parent directory path");
            RequireOwnedDirectory(parentHandle, "evidence parent directory");
            RequireOwnedDirectory(parentPath, "evidence parent directory path");
            if (!parentHandle.SameFile(parentPath))
            {
                throw new InvalidDataException(
                    "The evidence parent directory changed between path and handle validation.");
            }
        }
        catch
        {
            _ = CloseDarwin(directoryDescriptor);
            throw;
        }

        FileStream? stream = null;
        StableIdentity? createdIdentity = null;
        bool renamed = false;
        try
        {
            RequireParentDirectoryStillAnchored(parent, directoryDescriptor, parentHandle);
            int tempDescriptor = OpenAtFixedDarwin(
                directoryDescriptor,
                tempLeaf,
                OpenReadWrite | OpenCreate | OpenExclusive | OpenNoFollow | OpenCloseOnExec,
                0x0180);
            if (tempDescriptor < 0)
            {
                int error = Marshal.GetLastPInvokeError();
                throw new IOException($"Could not create the anchored evidence temporary file (errno {error}).");
            }
            try
            {
                createdIdentity = ReadDescriptorIdentity(
                    tempDescriptor,
                    "newly opened evidence temporary file");
                RequireOwnedRegular(createdIdentity, 0x180, "newly opened evidence temporary file");
            }
            catch
            {
                _ = CloseDarwin(tempDescriptor);
                throw;
            }
            SafeFileHandle tempHandle = new((IntPtr)tempDescriptor, ownsHandle: true);
            try
            {
                stream = new FileStream(
                    tempHandle,
                    FileAccess.ReadWrite,
                    64 * 1024,
                    isAsync: false);
            }
            catch
            {
                tempHandle.Dispose();
                throw;
            }
            RequireParentDirectoryStillAnchored(parent, directoryDescriptor, parentHandle);
            if (!TryReadAtIdentity(
                    directoryDescriptor,
                    tempLeaf,
                    out StableIdentity? createdPathIdentity,
                    out int createdPathError) ||
                !createdIdentity.SameFile(createdPathIdentity!))
            {
                throw new InvalidDataException(
                    "The created evidence temporary file is not inside the anchored parent " +
                    $"(errno {createdPathError}).");
            }
            stream.Write(payload);
            stream.Flush(flushToDisk: true);
            StableIdentity writtenIdentity = ReadHandleIdentity(stream, "created evidence temporary file");
            RequireOwnedRegular(writtenIdentity, 0x180, "created evidence temporary file");
            if (!createdIdentity.SameFile(writtenIdentity) ||
                writtenIdentity.ByteLength != payload.LongLength)
                throw new IOException("The evidence temporary file was short-written.");
            RequireParentDirectoryStillAnchored(parent, directoryDescriptor, parentHandle);

            beforeRename?.Invoke(temp);
            _ = VerifyTrackedOpenFile(
                stream,
                directoryDescriptor,
                tempLeaf,
                temp,
                createdIdentity,
                payload,
                expectedSha256,
                "immediately before atomic rename");
            RequireParentDirectoryStillAnchored(parent, directoryDescriptor, parentHandle);
            if (TryReadAtIdentity(
                    directoryDescriptor,
                    destinationLeaf,
                    out _,
                    out int destinationError) || destinationError != 2)
            {
                throw new IOException(
                    "The fixed canonical evidence destination appeared before atomic rename.");
            }
            if (RenameAtExclusive(
                    directoryDescriptor,
                    tempLeaf,
                    directoryDescriptor,
                    destinationLeaf,
                    RenameExclusive) != 0)
            {
                int error = Marshal.GetLastPInvokeError();
                throw new IOException(
                    $"Atomic no-replace publication of the fixed evidence destination failed (errno {error}).");
            }
            renamed = true;
            _ = VerifyTrackedOpenFile(
                stream,
                directoryDescriptor,
                destinationLeaf,
                full,
                createdIdentity,
                payload,
                expectedSha256,
                "immediately after atomic rename");
            afterRenameBeforeDirectoryFsync?.Invoke();
            FsyncDirectory(directoryDescriptor, "published evidence");
            afterRename?.Invoke(full);
            StableFileRead result = VerifyTrackedOpenFile(
                stream,
                directoryDescriptor,
                destinationLeaf,
                full,
                createdIdentity,
                payload,
                expectedSha256,
                "postwrite readback");
            RequireParentDirectoryStillAnchored(parent, directoryDescriptor, parentHandle);
            return result;
        }
        catch (Exception failure)
        {
            if (renamed && createdIdentity is not null)
            {
                try
                {
                    bool removed = RollBackPublishedIfStillOwned(
                        directoryDescriptor,
                        destinationLeaf,
                        createdIdentity,
                        stream,
                        parent);
                    if (!removed)
                    {
                        throw new IOException(
                            "Atomic publication failed after rename; the canonical destination identity " +
                            "drifted, so the replacement was preserved instead of being deleted.",
                            failure);
                    }
                }
                catch (Exception rollbackFailure) when (!ReferenceEquals(rollbackFailure, failure))
                {
                    if (rollbackFailure.InnerException == failure)
                        throw;
                    throw new IOException(
                        "Atomic publication failed and exact-inode rollback also failed.",
                        new AggregateException(failure, rollbackFailure));
                }
            }
            throw;
        }
        finally
        {
            if (!renamed && createdIdentity is not null)
            {
                CleanupTrackedTempIfStillOwned(
                    directoryDescriptor,
                    tempLeaf,
                    createdIdentity,
                    stream,
                    parent);
            }
            stream?.Dispose();
            _ = CloseDarwin(directoryDescriptor);
        }
    }

    private static StableFileRead VerifyTrackedOpenFile(
        FileStream stream,
        int directoryDescriptor,
        string leaf,
        string fullPath,
        StableIdentity createdIdentity,
        byte[] expectedBytes,
        string expectedSha256,
        string context)
    {
        StableIdentity before = ReadHandleIdentity(stream, context + " handle before read");
        RequireOwnedRegular(before, 0x180, context + " handle before read");
        if (!createdIdentity.SameFile(before) || before.ByteLength != expectedBytes.LongLength)
            throw new InvalidDataException($"The operation-owned temporary file identity changed {context}.");
        stream.Position = 0;
        string sha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (stream.Position != expectedBytes.LongLength || sha256 != expectedSha256)
            throw new InvalidDataException($"The operation-owned temporary file bytes changed {context}.");
        StableIdentity after = ReadHandleIdentity(stream, context + " handle after read");
        RequireOwnedRegular(after, 0x180, context + " handle after read");
        if (before != after || !createdIdentity.SameFile(after))
            throw new InvalidDataException($"The operation-owned temporary file changed during {context}.");
        if (!TryReadAtIdentity(directoryDescriptor, leaf, out StableIdentity? path, out int error))
            throw new IOException($"Could not stat the publication pathname {context} (errno {error}).");
        RequireOwnedRegular(path!, 0x180, context + " pathname");
        if (!after.SameFile(path!) || after != path)
            throw new InvalidDataException($"The publication pathname no longer names the created inode {context}.");
        return new(
            fullPath,
            sha256,
            after.ByteLength,
            expectedBytes,
            after.Device,
            after.Inode,
            after.Mode,
            after.LinkCount,
            after.UserId,
            after.ModifiedSeconds,
            after.ModifiedNanoseconds,
            after.ChangedSeconds,
            after.ChangedNanoseconds);
    }

    private static void RequireOwnedDirectory(StableIdentity identity, string context)
    {
        if ((identity.Mode & DarwinFileTypeMask) != DarwinDirectory ||
            identity.UserId != GetUid() ||
            (identity.Mode & GroupOrOtherWrite) != 0)
        {
            throw new InvalidDataException(
                $"{context} must be a current-user directory without group/other write permission " +
                $"(actual-type=0x{identity.Mode & DarwinFileTypeMask:x4}, " +
                $"mode={Convert.ToString(identity.Mode & 0x0FFF, 8)}, " +
                $"uid={identity.UserId}, expected-uid={GetUid()}).");
        }
    }

    private static void RequireParentDirectoryStillAnchored(
        string parent,
        int directoryDescriptor,
        StableIdentity original)
    {
        StableIdentity handle = ReadDescriptorIdentity(directoryDescriptor, "evidence parent recheck");
        StableIdentity path = ReadPathIdentity(parent, "evidence parent path recheck");
        RequireOwnedDirectory(handle, "evidence parent recheck");
        RequireOwnedDirectory(path, "evidence parent path recheck");
        if (!original.SameFile(handle) || !handle.SameFile(path))
            throw new InvalidDataException("The evidence parent directory identity changed during publication.");
    }

    private static void FsyncDirectory(int directoryDescriptor, string context)
    {
        if (Fsync(directoryDescriptor) != 0)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not fsync the evidence parent after {context} (errno {error}).");
        }
    }

    private static bool RollBackPublishedIfStillOwned(
        int directoryDescriptor,
        string destinationLeaf,
        StableIdentity createdIdentity,
        FileStream? stream,
        string parent)
    {
        if (!TryReadAtIdentity(
                directoryDescriptor,
                destinationLeaf,
                out StableIdentity? current,
                out int error))
        {
            if (error == 2)
            {
                CleanupMovedOwnedInodeIfSafe(
                    directoryDescriptor,
                    destinationLeaf,
                    createdIdentity,
                    stream,
                    parent);
                FsyncDirectory(directoryDescriptor, "confirming absent failed publication");
                return true;
            }
            throw new IOException($"Could not inspect failed publication for rollback (errno {error}).");
        }
        if (!createdIdentity.SameFile(current!))
        {
            CleanupMovedOwnedInodeIfSafe(
                directoryDescriptor,
                destinationLeaf,
                createdIdentity,
                stream,
                parent);
            return false;
        }
        if (UnlinkAtDarwin(directoryDescriptor, destinationLeaf, 0) != 0)
        {
            error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not unlink the exact failed publication inode (errno {error}).");
        }
        FsyncDirectory(directoryDescriptor, "rolling back failed publication");
        return true;
    }

    private static void CleanupMovedOwnedInodeIfSafe(
        int directoryDescriptor,
        string occupiedLeaf,
        StableIdentity createdIdentity,
        FileStream? stream,
        string parent)
    {
        if (stream is null)
            return;
        string? movedLeaf = TryGetAnchoredLeafForDescriptor(stream, parent);
        if (movedLeaf is null || movedLeaf == occupiedLeaf ||
            !TryUnlinkAtIfSameFile(directoryDescriptor, movedLeaf, createdIdentity))
        {
            return;
        }
        FsyncDirectory(directoryDescriptor, "cleaning moved operation-owned published bytes");
    }

    private static void CleanupTrackedTempIfStillOwned(
        int directoryDescriptor,
        string tempLeaf,
        StableIdentity createdIdentity,
        FileStream? stream,
        string parent)
    {
        if (TryUnlinkAtIfSameFile(directoryDescriptor, tempLeaf, createdIdentity))
        {
            FsyncDirectory(directoryDescriptor, "cleaning a failed publication temporary file");
            return;
        }
        if (stream is null)
            return;
        string? movedLeaf = TryGetAnchoredLeafForDescriptor(stream, parent);
        if (movedLeaf is null || movedLeaf == tempLeaf ||
            !TryUnlinkAtIfSameFile(directoryDescriptor, movedLeaf, createdIdentity))
        {
            return;
        }
        FsyncDirectory(directoryDescriptor, "cleaning the moved operation-owned temporary inode");
    }

    private static bool TryUnlinkAtIfSameFile(
        int directoryDescriptor,
        string leaf,
        StableIdentity createdIdentity)
    {
        if (!TryReadAtIdentity(
                directoryDescriptor,
                leaf,
                out StableIdentity? current,
                out int error))
        {
            if (error == 2)
                return false;
            throw new IOException($"Could not inspect tracked publication path '{leaf}' (errno {error}).");
        }
        if (!createdIdentity.SameFile(current!))
            return false;
        if (UnlinkAtDarwin(directoryDescriptor, leaf, 0) != 0)
        {
            error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not unlink the exact tracked publication inode (errno {error}).");
        }
        return true;
    }

    private static string? TryGetAnchoredLeafForDescriptor(FileStream stream, string parent)
    {
        int descriptor = checked((int)stream.SafeFileHandle.DangerousGetHandle());
        IntPtr nativeBuffer = Marshal.AllocHGlobal(MaximumDarwinPathBytes);
        try
        {
            for (int offset = 0; offset < MaximumDarwinPathBytes; offset++)
                Marshal.WriteByte(nativeBuffer, offset, 0);
            if (FcntlFixedDarwin(descriptor, FcntlGetPath, nativeBuffer) != 0)
                return null;
            byte[] buffer = new byte[MaximumDarwinPathBytes];
            Marshal.Copy(nativeBuffer, buffer, 0, buffer.Length);
            int terminator = Array.IndexOf(buffer, (byte)0);
            if (terminator <= 0)
                return null;
            string path = Encoding.UTF8.GetString(buffer, 0, terminator);
            string? descriptorParent = Path.GetDirectoryName(path);
            if (descriptorParent is null ||
                Path.GetFullPath(descriptorParent) != Path.GetFullPath(parent))
            {
                return null;
            }
            string leaf = Path.GetFileName(path);
            return string.IsNullOrEmpty(leaf) ? null : leaf;
        }
        finally
        {
            Marshal.FreeHGlobal(nativeBuffer);
        }
    }

    private static string HashBytes(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void RequireDarwin()
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("ID65 V3 finalization requires macOS stable-handle boundaries.");
    }

    [DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
    private static extern int FStatDarwin(int descriptor, out DarwinStat status);

    [DllImport("libc", EntryPoint = "lstat", SetLastError = true)]
    private static extern int LStatDarwin(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out DarwinStat status);

    [DllImport("libc", EntryPoint = "fstatat", SetLastError = true)]
    private static extern int FStatAtDarwin(
        int directoryDescriptor,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out DarwinStat status,
        int flags);

    [DllImport("libc", EntryPoint = "unlinkat", SetLastError = true)]
    private static extern int UnlinkAtDarwin(
        int directoryDescriptor,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags);

    [DllImport("libc", EntryPoint = "getuid")]
    private static extern uint GetUid();

    [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
    private static extern int Flock(int descriptor, int operation);

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int OpenDarwin(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags,
        int mode);

    [DllImport("libc", EntryPoint = "__openat", SetLastError = true)]
    private static extern int OpenAtFixedDarwin(
        int directoryDescriptor,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags,
        ushort mode);

    [DllImport("libc", EntryPoint = "__fcntl", SetLastError = true)]
    private static extern int FcntlFixedDarwin(int descriptor, int command, IntPtr buffer);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int CloseDarwin(int descriptor);

    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static extern int Fsync(int descriptor);

    [DllImport("libc", EntryPoint = "renameatx_np", SetLastError = true)]
    private static extern int RenameAtExclusive(
        int fromDirectoryDescriptor,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string fromPath,
        int toDirectoryDescriptor,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string toPath,
        uint flags);
}

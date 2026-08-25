using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Exporting;

[SupportedOSPlatform("macos")]
internal static partial class Id65AuthoringAggregateV1RuntimeEvidenceFinalizer
{
    private const ushort DarwinFileTypeMask = 0xF000;
    private const ushort DarwinRegularFile = 0x8000;
    private const ushort DarwinDirectory = 0x4000;
    private const int OpenNoFollow = 0x00000100;
    private const int OpenCreate = 0x00000200;
    private const int OpenExclusive = 0x00000800;
    private const int OpenDirectory = 0x00100000;
    private const int OpenCloseOnExec = 0x01000000;
    private const int OpenReadWrite = 0x00000002;
    private const int AtSymlinkNoFollow = 0x0020;
    private const uint RenameExclusive = 0x00000004;

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    private struct AggregateDarwinStat
    {
        [FieldOffset(0)] internal int Device;
        [FieldOffset(4)] internal ushort Mode;
        [FieldOffset(6)] internal ushort LinkCount;
        [FieldOffset(8)] internal ulong Inode;
        [FieldOffset(16)] internal uint UserId;
        [FieldOffset(48)] internal long ModifiedSeconds;
        [FieldOffset(56)] internal long ModifiedNanoseconds;
        [FieldOffset(64)] internal long ChangedSeconds;
        [FieldOffset(72)] internal long ChangedNanoseconds;
        [FieldOffset(96)] internal long ByteLength;
    }

    private sealed record AggregateStableIdentity(
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
        internal bool SameFile(AggregateStableIdentity other) =>
            Device == other.Device && Inode == other.Inode;
    }

    internal sealed record AggregateStableFileRead(
        string FullPath,
        string Sha256,
        long ByteLength,
        byte[] Bytes,
        int Device,
        ulong Inode,
        ushort Mode,
        ushort LinkCount,
        uint UserId,
        long ModifiedSeconds,
        long ModifiedNanoseconds,
        long ChangedSeconds,
        long ChangedNanoseconds);

    private sealed record AggregateAtomicPublication(
        AggregateStableFileRead Read,
        bool ExistingIdentical);

    private static AggregateStableFileRead ReadStableOwnedFile(
        string path,
        int expectedMode,
        long maximumBytes,
        string context,
        Action? afterOpenBeforeRead = null)
    {
        RequireDarwin();
        if (maximumBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        string full = Path.GetFullPath(path);
        if (!Path.IsPathFullyQualified(path) || full != path)
            throw new InvalidDataException($"{context} path must be canonical and absolute.");
        RequireNoSymlinkSegments(full, allowMissingLeaf: false, context);
        AggregateStableIdentity pathBefore = ReadPathIdentity(full, context + " before open");
        RequireOwnedRegular(pathBefore, expectedMode, context + " before open");
        if (pathBefore.ByteLength <= 0 || pathBefore.ByteLength > maximumBytes)
            throw new InvalidDataException($"{context} has an invalid byte length.");

        using FileStream stream = new(full, FileMode.Open, FileAccess.Read, FileShare.Read);
        AggregateStableIdentity before = ReadHandleIdentity(stream, context + " handle before read");
        RequireOwnedRegular(before, expectedMode, context + " handle before read");
        if (pathBefore != before)
            throw new InvalidDataException($"{context} changed between path and handle validation.");

        afterOpenBeforeRead?.Invoke();
        byte[] bytes = new byte[checked((int)before.ByteLength)];
        stream.ReadExactly(bytes);
        if (stream.Position != before.ByteLength)
            throw new InvalidDataException($"{context} was not read to its exact handle length.");
        string sha256 = HashBytes(bytes);

        AggregateStableIdentity after = ReadHandleIdentity(stream, context + " handle after read");
        AggregateStableIdentity pathAfter = ReadPathIdentity(full, context + " path after read");
        RequireOwnedRegular(after, expectedMode, context + " handle after read");
        RequireOwnedRegular(pathAfter, expectedMode, context + " path after read");
        if (before != after || after != pathAfter || stream.Length != after.ByteLength)
            throw new InvalidDataException($"{context} changed or rebound during stable readback.");
        return ToStableRead(full, sha256, bytes, after);
    }

    private static AggregateStableFileRead ToStableRead(
        string path,
        string sha256,
        byte[] bytes,
        AggregateStableIdentity identity) => new(
            path,
            sha256,
            identity.ByteLength,
            bytes,
            identity.Device,
            identity.Inode,
            identity.Mode,
            identity.LinkCount,
            identity.UserId,
            identity.ModifiedSeconds,
            identity.ModifiedNanoseconds,
            identity.ChangedSeconds,
            identity.ChangedNanoseconds);

    private static string StableIdentityManifest(AggregateStableFileRead read) =>
        $"{read.FullPath}\0{read.Device}\0{read.Inode}\0{read.Mode}\0{read.LinkCount}\0" +
        $"{read.UserId}\0{read.ByteLength}\0{read.Sha256}\0{read.ModifiedSeconds}\0" +
        $"{read.ModifiedNanoseconds}\0{read.ChangedSeconds}\0{read.ChangedNanoseconds}\n";

    private static void RequireOwnedDirectory(string path, int expectedMode, string context)
    {
        RequireDarwin();
        string full = Path.GetFullPath(path);
        if (!Path.IsPathFullyQualified(path) || full != path)
            throw new InvalidDataException($"{context} path must be canonical and absolute.");
        RequireNoSymlinkSegments(full, allowMissingLeaf: false, context);
        AggregateStableIdentity identity = ReadPathIdentity(full, context);
        if ((identity.Mode & DarwinFileTypeMask) != DarwinDirectory ||
            identity.UserId != GetUid() || (identity.Mode & 0x0FFF) != expectedMode)
        {
            throw new InvalidDataException(
                $"{context} must be a current-user directory with mode " +
                $"{Convert.ToString(expectedMode, 8)}.");
        }
    }

    private static void CreateOwnedDirectory(string path)
    {
        Directory.CreateDirectory(path);
        File.SetUnixFileMode(path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        RequireOwnedDirectory(Path.GetFullPath(path), 0x1C0, "fixture directory");
    }

    private static void WriteOwnedFile(string path, ReadOnlySpan<byte> bytes)
    {
        FileStreamOptions options = new()
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.WriteThrough,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
        };
        using (FileStream stream = new(path, options))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static AggregateAtomicPublication AtomicPublishNoReplace(
        string destination,
        ReadOnlySpan<byte> bytes,
        Action<string>? beforeRename = null,
        Action? afterRenameBeforeDirectoryFsync = null)
    {
        RequireDarwin();
        string full = Path.GetFullPath(destination);
        if (!Path.IsPathFullyQualified(destination) || destination != full)
            throw new InvalidDataException("Fixture publication destination must be canonical and absolute.");
        string parent = Path.GetDirectoryName(full) ??
            throw new InvalidDataException("Fixture publication destination has no parent.");
        string destinationLeaf = Path.GetFileName(full);
        RequireOwnedDirectory(parent, 0x1C0, "fixture publication parent");
        AggregateStableIdentity parentPathBefore = ReadPathIdentity(
            parent, "fixture publication parent before open");
        RequireNoSymlinkSegments(full, allowMissingLeaf: true, "fixture publication destination");
        byte[] payload = bytes.ToArray();
        string expectedSha256 = HashBytes(payload);
        int directoryDescriptor = OpenDarwin(
            parent, OpenNoFollow | OpenDirectory | OpenCloseOnExec, 0);
        if (directoryDescriptor < 0)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not anchor the fixture publication parent (errno {error}).");
        }
        AggregateStableIdentity parentIdentity;
        try
        {
            parentIdentity = ReadDescriptorIdentity(
                directoryDescriptor, "fixture publication parent handle");
            RequireOwnedDirectoryIdentity(
                parentIdentity, 0x1C0, "fixture publication parent handle");
            if (!parentPathBefore.SameFile(parentIdentity))
                throw new InvalidDataException(
                    "Fixture publication parent changed between path validation and open.");
            RequireParentDirectoryStillAnchored(parent, directoryDescriptor, parentIdentity);
            if (TryReadAtIdentity(
                    directoryDescriptor,
                    destinationLeaf,
                    out AggregateStableIdentity? existingAt,
                    out int destinationError))
            {
                RequireOwnedRegular(existingAt!, 0x180, "existing fixture publication");
                AggregateStableFileRead existing = ReadStableOwnedFile(
                    full, 0x180, Math.Max(payload.LongLength, 1), "existing fixture publication");
                RequireParentDirectoryStillAnchored(parent, directoryDescriptor, parentIdentity);
                if (existing.Device != existingAt!.Device || existing.Inode != existingAt.Inode)
                    throw new InvalidDataException("Existing fixture publication identity changed.");
                if (existing.Sha256 == expectedSha256 &&
                    existing.Bytes.AsSpan().SequenceEqual(payload))
                {
                    beforeRename?.Invoke(full);
                    AggregateStableFileRead confirmed = ReadStableOwnedFile(
                        full,
                        0x180,
                        Math.Max(payload.LongLength, 1),
                        "confirmed existing fixture publication");
                    RequireParentDirectoryStillAnchored(
                        parent, directoryDescriptor, parentIdentity);
                    if (StableIdentityManifest(existing) != StableIdentityManifest(confirmed) ||
                        confirmed.Sha256 != expectedSha256 ||
                        !confirmed.Bytes.AsSpan().SequenceEqual(payload))
                    {
                        throw new InvalidDataException(
                            "Existing fixture publication changed before idempotent acceptance.");
                    }
                    _ = CloseDarwin(directoryDescriptor);
                    return new(confirmed, ExistingIdentical: true);
                }
                throw new IOException("The no-replace destination already contains different bytes.");
            }
            if (destinationError != 2)
                throw new IOException(
                    $"Could not establish an absent fixture destination (errno {destinationError}).");
        }
        catch
        {
            _ = CloseDarwin(directoryDescriptor);
            throw;
        }

        string tempLeaf = ".id65-aggregate-fixture.tmp-" + Guid.NewGuid().ToString("N");
        string temp = Path.Combine(parent, tempLeaf);
        AggregateStableIdentity? createdIdentity = null;
        FileStream? stream = null;
        bool renamed = false;
        try
        {
            RequireParentDirectoryStillAnchored(parent, directoryDescriptor, parentIdentity);
            int tempDescriptor = OpenAtFixedDarwin(
                directoryDescriptor,
                tempLeaf,
                OpenReadWrite | OpenCreate | OpenExclusive | OpenNoFollow | OpenCloseOnExec,
                0x0180);
            if (tempDescriptor < 0)
            {
                int error = Marshal.GetLastPInvokeError();
                throw new IOException($"Could not create anchored fixture temporary file (errno {error}).");
            }
            try
            {
                createdIdentity = ReadDescriptorIdentity(
                    tempDescriptor, "fixture publication temporary descriptor");
                RequireOwnedRegular(createdIdentity, 0x180, "fixture publication temporary file");
            }
            catch
            {
                _ = CloseDarwin(tempDescriptor);
                throw;
            }
            SafeFileHandle safeHandle = new((IntPtr)tempDescriptor, ownsHandle: true);
            try
            {
                stream = new FileStream(safeHandle, FileAccess.ReadWrite, 64 * 1024, isAsync: false);
            }
            catch
            {
                safeHandle.Dispose();
                throw;
            }
            stream.Write(payload);
            stream.Flush(flushToDisk: true);
            _ = VerifyTrackedOpenFile(
                stream,
                directoryDescriptor,
                tempLeaf,
                temp,
                parent,
                parentIdentity,
                createdIdentity,
                payload,
                expectedSha256,
                "after temporary write");

            beforeRename?.Invoke(temp);
            _ = VerifyTrackedOpenFile(
                stream,
                directoryDescriptor,
                tempLeaf,
                temp,
                parent,
                parentIdentity,
                createdIdentity,
                payload,
                expectedSha256,
                "immediately before atomic rename");
            if (RenameAtExclusive(
                    directoryDescriptor, tempLeaf,
                    directoryDescriptor, destinationLeaf,
                    RenameExclusive) != 0)
            {
                int error = Marshal.GetLastPInvokeError();
                throw new IOException($"Atomic no-replace fixture publication failed (errno {error}).");
            }
            renamed = true;
            _ = VerifyTrackedOpenFile(
                stream,
                directoryDescriptor,
                destinationLeaf,
                full,
                parent,
                parentIdentity,
                createdIdentity,
                payload,
                expectedSha256,
                "immediately after atomic rename");
            afterRenameBeforeDirectoryFsync?.Invoke();
            AggregateStableFileRead afterHook = VerifyTrackedOpenFile(
                stream,
                directoryDescriptor,
                destinationLeaf,
                full,
                parent,
                parentIdentity,
                createdIdentity,
                payload,
                expectedSha256,
                "after publication hook");
            FsyncDirectoryDescriptor(directoryDescriptor, "published fixture evidence");
            AggregateStableFileRead durable = VerifyTrackedOpenFile(
                stream,
                directoryDescriptor,
                destinationLeaf,
                full,
                parent,
                parentIdentity,
                createdIdentity,
                payload,
                expectedSha256,
                "after directory fsync");
            if (StableIdentityManifest(afterHook) != StableIdentityManifest(durable))
                throw new InvalidDataException("Published fixture identity changed across directory fsync.");
            return new(durable, ExistingIdentical: false);
        }
        catch (Exception publicationFailure)
        {
            if (renamed && createdIdentity is not null)
            {
                string quarantineLeaf =
                    ".id65-aggregate-fixture.quarantine-" + Guid.NewGuid().ToString("N");
                try
                {
                    RequireParentDirectoryStillAnchored(parent, directoryDescriptor, parentIdentity);
                    if (!TryReadAtIdentity(
                            directoryDescriptor,
                            destinationLeaf,
                            out AggregateStableIdentity? current,
                            out int currentError) ||
                        !createdIdentity.SameFile(current!))
                    {
                        throw new InvalidDataException(
                            "Failed fixture publication destination identity drifted " +
                            $"(errno {currentError}); the unknown replacement was preserved.");
                    }
                    if (RenameAtExclusive(
                            directoryDescriptor, destinationLeaf,
                            directoryDescriptor, quarantineLeaf,
                            RenameExclusive) != 0)
                    {
                        int error = Marshal.GetLastPInvokeError();
                        throw new IOException($"Could not quarantine failed fixture publication (errno {error}).");
                    }
                    if (stream is null)
                        throw new IOException("Quarantined fixture publication lost its open handle.");
                    if (!TryReadAtIdentity(
                            directoryDescriptor,
                            quarantineLeaf,
                            out AggregateStableIdentity? quarantinedPath,
                            out int quarantineError))
                    {
                        throw new IOException(
                            $"Could not verify quarantined fixture publication (errno {quarantineError}).");
                    }
                    AggregateStableIdentity quarantinedHandle = ReadHandleIdentity(
                        stream, "quarantined fixture publication handle");
                    RequireOwnedRegular(
                        quarantinedPath!, 0x180, "quarantined fixture publication path");
                    RequireOwnedRegular(
                        quarantinedHandle, 0x180, "quarantined fixture publication handle");
                    if (!createdIdentity.SameFile(quarantinedPath!) ||
                        quarantinedPath != quarantinedHandle)
                    {
                        throw new InvalidDataException(
                            "Quarantined fixture publication identity changed.");
                    }
                    FsyncDirectoryDescriptor(
                        directoryDescriptor, "quarantined failed fixture publication");
                    RequireParentDirectoryStillAnchored(
                        parent, directoryDescriptor, parentIdentity);
                }
                catch (Exception quarantineFailure)
                {
                    throw new IOException(
                        "Fixture publication failed and its exact inode could not be quarantined.",
                        new AggregateException(publicationFailure, quarantineFailure));
                }
            }
            throw;
        }
        finally
        {
            if (!renamed && createdIdentity is not null)
                CleanupTrackedLeafIfSameIdentity(
                    directoryDescriptor, tempLeaf, createdIdentity, parentIdentity);
            stream?.Dispose();
            _ = CloseDarwin(directoryDescriptor);
        }
    }

    private static AggregateStableFileRead VerifyTrackedOpenFile(
        FileStream stream,
        int directoryDescriptor,
        string leaf,
        string fullPath,
        string parent,
        AggregateStableIdentity parentIdentity,
        AggregateStableIdentity createdIdentity,
        byte[] expectedBytes,
        string expectedSha256,
        string context)
    {
        RequireParentDirectoryStillAnchored(parent, directoryDescriptor, parentIdentity);
        AggregateStableIdentity before = ReadHandleIdentity(stream, context + " handle before read");
        RequireOwnedRegular(before, 0x180, context + " handle before read");
        if (!createdIdentity.SameFile(before) || before.ByteLength != expectedBytes.LongLength)
            throw new InvalidDataException($"Tracked fixture file identity changed {context}.");
        stream.Position = 0;
        byte[] actual = new byte[expectedBytes.Length];
        stream.ReadExactly(actual);
        if (stream.Position != expectedBytes.LongLength || HashBytes(actual) != expectedSha256 ||
            !actual.AsSpan().SequenceEqual(expectedBytes))
        {
            throw new InvalidDataException($"Tracked fixture file bytes changed {context}.");
        }
        AggregateStableIdentity after = ReadHandleIdentity(stream, context + " handle after read");
        RequireOwnedRegular(after, 0x180, context + " handle after read");
        if (before != after || !createdIdentity.SameFile(after))
            throw new InvalidDataException($"Tracked fixture handle changed {context}.");
        if (!TryReadAtIdentity(
                directoryDescriptor,
                leaf,
                out AggregateStableIdentity? pathIdentity,
                out int error) ||
            pathIdentity != after)
        {
            throw new InvalidDataException(
                $"Tracked fixture path changed {context} (errno {error}).");
        }
        RequireParentDirectoryStillAnchored(parent, directoryDescriptor, parentIdentity);
        return ToStableRead(fullPath, expectedSha256, actual, after);
    }

    private static void DeleteOwnedFile(string path)
    {
        string parent = Path.GetDirectoryName(path) ??
            throw new InvalidDataException("Owned fixture cleanup path has no parent.");
        string leaf = Path.GetFileName(path);
        RequireOwnedDirectory(parent, 0x1C0, "owned fixture cleanup parent");
        AggregateStableIdentity parentPathBefore = ReadPathIdentity(
            parent, "owned fixture cleanup parent before open");
        int descriptor = OpenDarwin(
            parent, OpenNoFollow | OpenDirectory | OpenCloseOnExec, 0);
        if (descriptor < 0)
            throw new IOException("Could not anchor owned fixture cleanup parent.");
        try
        {
            AggregateStableIdentity parentIdentity = ReadDescriptorIdentity(
                descriptor, "owned fixture cleanup parent handle");
            RequireOwnedDirectoryIdentity(parentIdentity, 0x1C0, "owned fixture cleanup parent");
            if (!parentPathBefore.SameFile(parentIdentity))
                throw new InvalidDataException(
                    "Owned fixture cleanup parent changed between validation and open.");
            RequireParentDirectoryStillAnchored(parent, descriptor, parentIdentity);
            if (!TryReadAtIdentity(descriptor, leaf, out AggregateStableIdentity? identity, out int error))
                throw new IOException($"Could not inspect owned fixture cleanup leaf (errno {error}).");
            RequireOwnedRegular(identity!, 0x180, "owned fixture cleanup leaf");
            CleanupTrackedLeafIfSameIdentity(descriptor, leaf, identity!, parentIdentity);
        }
        finally
        {
            _ = CloseDarwin(descriptor);
        }
    }

    private static void CleanupTrackedLeafIfSameIdentity(
        int directoryDescriptor,
        string leaf,
        AggregateStableIdentity expected,
        AggregateStableIdentity parentIdentity)
    {
        AggregateStableIdentity parentHandle = ReadDescriptorIdentity(
            directoryDescriptor, "tracked fixture cleanup parent");
        RequireOwnedDirectoryIdentity(
            parentHandle, 0x1C0, "tracked fixture cleanup parent");
        if (!parentIdentity.SameFile(parentHandle))
            throw new InvalidDataException("Tracked fixture cleanup parent identity drifted.");
        if (!TryReadAtIdentity(
                directoryDescriptor, leaf, out AggregateStableIdentity? current, out int error))
        {
            if (error == 2)
                return;
            throw new IOException($"Could not inspect tracked fixture cleanup leaf (errno {error}).");
        }
        RequireOwnedRegular(current!, 0x180, "tracked fixture cleanup leaf");
        if (!expected.SameFile(current!))
            throw new InvalidDataException("Tracked fixture cleanup identity drifted.");
        if (UnlinkAtDarwin(directoryDescriptor, leaf, 0) != 0)
        {
            error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not remove tracked fixture cleanup leaf (errno {error}).");
        }
        FsyncDirectoryDescriptor(directoryDescriptor, "tracked fixture cleanup");
        parentHandle = ReadDescriptorIdentity(
            directoryDescriptor, "tracked fixture cleanup parent after fsync");
        if (!parentIdentity.SameFile(parentHandle))
            throw new InvalidDataException("Tracked fixture cleanup parent identity drifted.");
    }

    private static void FsyncDirectoryDescriptor(int descriptor, string context)
    {
        if (FsyncDarwin(descriptor) != 0)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not fsync directory for {context} (errno {error}).");
        }
    }

    private static void RequireNoSymlinkSegments(
        string path,
        bool allowMissingLeaf,
        string context)
    {
        string full = Path.GetFullPath(path);
        string? current = Path.GetPathRoot(full);
        string[] segments = full[(current?.Length ?? 0)..]
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < segments.Length; index++)
        {
            current = Path.Combine(current ?? string.Empty, segments[index]);
            if (allowMissingLeaf && index == segments.Length - 1 &&
                !File.Exists(current) && !Directory.Exists(current))
            {
                return;
            }
            AggregateStableIdentity identity = ReadPathIdentity(current, context + " path segment");
            ushort type = (ushort)(identity.Mode & DarwinFileTypeMask);
            if (index < segments.Length - 1 && type != DarwinDirectory)
                throw new InvalidDataException($"{context} has a non-directory ancestor.");
            if (index == segments.Length - 1 &&
                type is not DarwinDirectory and not DarwinRegularFile)
            {
                throw new InvalidDataException($"{context} contains a symlink or special-file leaf.");
            }
        }
    }

    private static AggregateStableIdentity ReadPathIdentity(string path, string context)
    {
        if (LStatDarwin(path, out AggregateDarwinStat status) != 0)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not inspect {context} (errno {error}).");
        }
        return ToIdentity(status);
    }

    private static AggregateStableIdentity ReadHandleIdentity(FileStream stream, string context)
    {
        int descriptor = checked((int)stream.SafeFileHandle.DangerousGetHandle());
        if (FStatDarwin(descriptor, out AggregateDarwinStat status) != 0)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not inspect {context} (errno {error}).");
        }
        return ToIdentity(status);
    }

    private static AggregateStableIdentity ReadDescriptorIdentity(int descriptor, string context)
    {
        if (FStatDarwin(descriptor, out AggregateDarwinStat status) != 0)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not inspect {context} (errno {error}).");
        }
        return ToIdentity(status);
    }

    private static bool TryReadAtIdentity(
        int directoryDescriptor,
        string leaf,
        out AggregateStableIdentity? identity,
        out int error)
    {
        if (FStatAtDarwin(
                directoryDescriptor,
                leaf,
                out AggregateDarwinStat status,
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

    private static void RequireParentDirectoryStillAnchored(
        string parent,
        int directoryDescriptor,
        AggregateStableIdentity expected)
    {
        AggregateStableIdentity handle = ReadDescriptorIdentity(
            directoryDescriptor, "fixture publication parent recheck");
        AggregateStableIdentity path = ReadPathIdentity(
            parent, "fixture publication parent path recheck");
        RequireOwnedDirectoryIdentity(handle, 0x1C0, "fixture publication parent recheck");
        RequireOwnedDirectoryIdentity(path, 0x1C0, "fixture publication parent path recheck");
        if (!expected.SameFile(handle) || !handle.SameFile(path))
            throw new InvalidDataException("Fixture publication parent changed or rebound.");
    }

    private static AggregateStableIdentity ToIdentity(AggregateDarwinStat status) => new(
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

    private static void RequireOwnedRegular(
        AggregateStableIdentity identity,
        int expectedMode,
        string context)
    {
        if ((identity.Mode & DarwinFileTypeMask) != DarwinRegularFile ||
            identity.UserId != GetUid() || identity.LinkCount != 1 ||
            (identity.Mode & 0x0FFF) != expectedMode)
        {
            throw new InvalidDataException(
                $"{context} must be one current-user regular file with mode " +
                $"{Convert.ToString(expectedMode, 8)} and link count one.");
        }
    }

    private static void RequireOwnedDirectoryIdentity(
        AggregateStableIdentity identity,
        int expectedMode,
        string context)
    {
        if ((identity.Mode & DarwinFileTypeMask) != DarwinDirectory ||
            identity.UserId != GetUid() || (identity.Mode & 0x0FFF) != expectedMode)
        {
            throw new InvalidDataException(
                $"{context} must be a current-user directory with mode " +
                $"{Convert.ToString(expectedMode, 8)}.");
        }
    }

    private static string HashBytes(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void RequireDarwin()
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException(
                "Aggregate V1 runtime finalizer stable-handle boundaries require macOS.");
    }

    [DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
    private static extern int FStatDarwin(int descriptor, out AggregateDarwinStat status);

    [DllImport("libc", EntryPoint = "lstat", SetLastError = true)]
    private static extern int LStatDarwin(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out AggregateDarwinStat status);

    [DllImport("libc", EntryPoint = "fstatat", SetLastError = true)]
    private static extern int FStatAtDarwin(
        int directoryDescriptor,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out AggregateDarwinStat status,
        int flags);

    [DllImport("libc", EntryPoint = "unlinkat", SetLastError = true)]
    private static extern int UnlinkAtDarwin(
        int directoryDescriptor,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags);

    [DllImport("libc", EntryPoint = "getuid")]
    private static extern uint GetUid();

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

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int CloseDarwin(int descriptor);

    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static extern int FsyncDarwin(int descriptor);

    [DllImport("libc", EntryPoint = "renameatx_np", SetLastError = true)]
    private static extern int RenameAtExclusive(
        int fromDirectoryDescriptor,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string fromPath,
        int toDirectoryDescriptor,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string toPath,
        uint flags);
}

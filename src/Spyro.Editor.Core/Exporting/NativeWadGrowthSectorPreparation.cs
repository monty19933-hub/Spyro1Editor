using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeFlatIsoFile(string Name, int Lba, int Size, int RecordOffset, bool IsDirectory = false, int ParentLba = 0);

/// <summary>
/// Source-bound initialization of newly allocated WAD/EXE gap sectors and the
/// exact old/new EOF/EOR boundaries. It is not a general Form2 conversion API.
/// Complete source ISO geometry and every physical preimage are checked before
/// output mutation; only an already approved structural plan may consume it.
/// </summary>
internal sealed class NativeWadGrowthSectorPreparation
{
    private sealed record SectorWitness(int Lba, string BeforeSha256, bool WasGap, byte? FinalBoundaryFlags);
    private readonly IReadOnlyList<SectorWitness> _sectors;
    private readonly DiscLayout _layout;
    private readonly long _sourceLength;
    private NativeWadGrowthSectorPreparation(DiscLayout layout, long length, IEnumerable<SectorWitness> sectors)
    { _layout = layout; _sourceLength = length; _sectors = Array.AsReadOnly(sectors.ToArray()); }

    internal static NativeWadGrowthSectorPreparation Create(FileStream source, DiscLayout layout, NativeSkyWadRelocationPlan plan)
        => Create((Stream)source, layout, plan);

    internal static NativeWadGrowthSectorPreparation Create(Stream source, DiscLayout layout, NativeSkyWadRelocationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(layout); ArgumentNullException.ThrowIfNull(plan);
        bool cooked = layout.SectorSize == 2048 && layout.UserOffset == 0;
        if (!cooked && !(layout.SectorSize == 2352 && layout.UserOffset == 24) && !(layout.SectorSize == 2336 && layout.UserOffset == 8) ||
            source.Length % layout.SectorSize != 0)
            throw new InvalidDataException("Structural allocation requires an exact supported whole-sector image.");
        IReadOnlyList<NativeFlatIsoFile> rootFiles = ReadFlatRootFiles(source, layout);
        IReadOnlyList<NativeFlatIsoFile> files = ReadCompleteInventory(source, layout, rootFiles);
        NativeFlatIsoFile wad = rootFiles.SingleOrDefault(file => !file.IsDirectory && (file.Name.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase) || file.Name.Equals("WAD", StringComparison.OrdinalIgnoreCase)))
            ?? throw new InvalidDataException("The exact WAD ISO extent is missing or ambiguous.");
        NativeFlatIsoFile executable = rootFiles.SingleOrDefault(file => !file.IsDirectory && IsExecutable(file.Name))
            ?? throw new InvalidDataException("The exact executable ISO extent is missing or ambiguous.");
        if (!plan.Required || plan.WadGrowthBytes <= 0 || plan.WadGrowthBytes % 2048 != 0 ||
            plan.OriginalWadSize <= 0 || plan.OriginalWadSize % 2048 != 0 || plan.ExecutableSize <= 0 || plan.ExecutableSize % 2048 != 0 ||
            plan.WadLba != wad.Lba || plan.OriginalWadSize != wad.Size || plan.OriginalExecutableLba != executable.Lba || plan.ExecutableSize != executable.Size ||
            (long)plan.OriginalWadSize + plan.WadGrowthBytes != plan.ExpandedWadSize ||
            (long)wad.Lba + wad.Size / 2048 != executable.Lba ||
            (long)wad.Lba + plan.ExpandedWadSize / 2048 != plan.RelocatedExecutableLba)
            throw new InvalidDataException("Submitted WAD/EXE growth does not match the exact source ISO extents/alignment.");
        long originalExeEnd = checked(executable.Lba + executable.Size / 2048L);
        long finalExeEnd = checked(plan.RelocatedExecutableLba + executable.Size / 2048L);
        long totalSectors = source.Length / layout.SectorSize;
        if (originalExeEnd < 0 || finalExeEnd <= originalExeEnd || finalExeEnd > totalSectors)
            throw new InvalidDataException("The complete relocated executable exceeds its source-image allocation bounds.");
        NativeFlatIsoFile next = rootFiles.Where(file => file.Lba > executable.Lba).OrderBy(file => file.Lba).FirstOrDefault()
            ?? throw new InvalidDataException("The source ISO has no following extent to bound structural allocation.");
        if (plan.NextFileLba != next.Lba || plan.AvailableGrowthBytes != checked((next.Lba - originalExeEnd) * 2048) || finalExeEnd > next.Lba)
            throw new InvalidDataException("The submitted free-space bound differs from the current ISO file inventory.");
        foreach (NativeFlatIsoFile file in files.Where(file => file != wad && file != executable))
        {
            long end = checked(file.Lba + (file.Size + 2047L) / 2048);
            if (file.Size != 0 && file.Lba < finalExeEnd && wad.Lba < end)
                throw new InvalidDataException("WAD/EXE relocation overlaps another live ISO/XA file extent.");
        }
        long rootEnd = checked(layout.RootExtent + (layout.RootLength + 2047L) / 2048);
        if (layout.RootExtent < finalExeEnd && wad.Lba < rootEnd || wad.Lba < 17)
            throw new InvalidDataException("WAD/EXE relocation overlaps ISO filesystem metadata.");
        ValidateMetadata(source, layout, files, wad.Lba, finalExeEnd);

        int oldWadLast = checked(executable.Lba - 1), oldExeLast = checked((int)originalExeEnd - 1);
        int newWadLast = checked(plan.RelocatedExecutableLba - 1), newExeLast = checked((int)finalExeEnd - 1);
        byte wadFlags = 0, exeFlags = 0;
        if (!cooked)
        {
            // Logical relocation keeps physical headers in place. This narrow
            // writer therefore requires the verified retail-compatible owner
            // contract, rather than silently moving nonzero XA identities.
            for (int lba = wad.Lba; lba < originalExeEnd; lba++)
            {
                byte[] raw = ReadSector(source, layout, lba);
                RawMode2Form1SectorIntegrity.RequireLogicalWriteSector(raw, layout, lba);
                ReadOnlySpan<byte> xa = raw.AsSpan(layout.UserOffset - 8, 4);
                byte allowedFlags = lba == oldWadLast || lba == oldExeLast ? (byte)0x81 : (byte)0;
                if (xa[0] != 0 || xa[1] != 0 || xa[3] != 0 || (xa[2] & ~allowedFlags) != 0x08)
                    throw new InvalidDataException("Source WAD/EXE XA metadata is incompatible with logical structural relocation.");
            }
            wadFlags = (byte)(ReadCheckedForm1(source, layout, oldWadLast)[layout.UserOffset - 6] & 0x81);
            exeFlags = (byte)(ReadCheckedForm1(source, layout, oldExeLast)[layout.UserOffset - 6] & 0x81);
        }
        SortedSet<int> affected = [];
        for (long lba = originalExeEnd; lba < finalExeEnd; lba++) affected.Add(checked((int)lba));
        if (!cooked) affected.UnionWith([oldWadLast, oldExeLast, newWadLast, newExeLast]);
        List<SectorWitness> witnesses = [];
        foreach (int lba in affected)
        {
            byte[] raw = ReadSector(source, layout, lba);
            bool gap = lba >= originalExeEnd;
            if (gap) _ = RawMode2Form1SectorIntegrity.PrepareOwnedZeroPaddingSector(raw, layout, lba);
            else RequireForm1Integrity(raw, layout, lba);
            byte? flags = null;
            if (!cooked && (lba == oldWadLast || lba == oldExeLast || lba == newWadLast || lba == newExeLast))
                flags = lba == newWadLast ? wadFlags : lba == newExeLast ? exeFlags : (byte)0;
            witnesses.Add(new(lba, Hash(raw), gap, flags));
        }
        return new(layout, source.Length, witnesses);
    }

    internal void ApplyToExactCopy(FileStream output)
    {
        if (!output.CanRead || !output.CanWrite || !output.CanSeek || output.Length != _sourceLength)
            throw new InvalidDataException("Structural initialization requires an exact writable source-image copy.");
        // Complete first pass: a malformed/stale later sector cannot leave an
        // earlier promotion or boundary flag changed.
        foreach (SectorWitness witness in _sectors)
            if (Hash(ReadSector(output, _layout, witness.Lba)) != witness.BeforeSha256)
                throw new InvalidDataException("A structural gap/boundary preimage changed before initialization.");
        foreach (SectorWitness witness in _sectors)
        {
            byte[] before = ReadSector(output, _layout, witness.Lba);
            if (Hash(before) != witness.BeforeSha256)
                throw new InvalidDataException("A structural gap/boundary preimage changed during initialization.");
            byte[] after = witness.WasGap
                ? RawMode2Form1SectorIntegrity.PrepareOwnedZeroPaddingSector(before, _layout, witness.Lba)
                : before.ToArray();
            if (witness.FinalBoundaryFlags is byte flags)
            {
                int submode = _layout.UserOffset - 6;
                byte replacement = (byte)((after[submode] & ~0x81) | flags);
                after[submode] = after[submode + 4] = replacement;
                RawMode2Form1SectorIntegrity.RebuildLogicalWriteSector(after, _layout, witness.Lba);
            }
            if (!after.AsSpan().SequenceEqual(before))
            {
                output.Position = checked((long)witness.Lba * _layout.SectorSize);
                output.Write(after);
            }
        }
    }

    internal static IReadOnlyList<NativeFlatIsoFile> ReadFlatRootFiles(FileStream image, DiscLayout layout)
        => ReadFlatRootFiles((Stream)image, layout);

    internal static IReadOnlyList<NativeFlatIsoFile> ReadFlatRootFiles(Stream image, DiscLayout layout)
    {
        return ReadDirectory(image, layout, layout.RootExtent, layout.RootLength, layout.RootExtent);
    }

    private static IReadOnlyList<NativeFlatIsoFile> ReadDirectory(Stream image, DiscLayout layout, int directoryLba, int directoryLength, int parentLba)
    {
        if (directoryLength <= 0 || directoryLength > 64 * 1024)
            throw new InvalidDataException("The structural writer requires bounded ISO directories.");
        List<NativeFlatIsoFile> files = [];
        HashSet<byte> specialNames = [];
        for (int directoryOffset = 0; directoryOffset < directoryLength; directoryOffset += 2048)
        {
            byte[] sector = DiscImage.ReadFileBytes(image, layout, directoryLba, directoryOffset, Math.Min(2048, directoryLength - directoryOffset));
            for (int offset = 0; offset < sector.Length;)
            {
                int length = sector[offset];
                if (length == 0) break;
                if (length < 34 || length > sector.Length - offset)
                    throw new InvalidDataException("An ISO directory record crosses its containing sector.");
                ReadOnlySpan<byte> record = sector.AsSpan(offset, length);
                int nameLength = record[32];
                if (nameLength == 0 || 33 + nameLength + (nameLength % 2 == 0 ? 1 : 0) > length)
                    throw new InvalidDataException("An ISO identifier exceeds its exact record.");
                int lba = Both(record, 2), size = Both(record, 10);
                long endSector = checked(lba + (size + 2047L) / 2048);
                if (checked(endSector * layout.SectorSize) > image.Length || record[1] != 0 || record[26] != 0 || record[27] != 0 ||
                    BinaryPrimitives.ReadUInt16LittleEndian(record[28..]) != 1 || BinaryPrimitives.ReadUInt16BigEndian(record[30..]) != 1)
                    throw new InvalidDataException("ISO file extents, interleaving or volume ownership are unsupported for structural growth.");
                bool dot = nameLength == 1 && record[33] is 0 or 1;
                if (dot)
                {
                    byte special = record[33];
                    if (!specialNames.Add(special) || record[25] != 2 || lba != (special == 0 ? directoryLba : parentLba) ||
                        (special == 0 && size != directoryLength))
                        throw new InvalidDataException("ISO directory self/parent ownership is inconsistent.");
                }
                else
                {
                    if ((record[25] & 0xFC) != 0 || record.Slice(33, nameLength).ContainsAnyExceptInRange((byte)32, (byte)126))
                        throw new InvalidDataException("Unknown associated/multi-extent file or identifier ownership prevents safe ISO gap allocation.");
                    string name = Encoding.ASCII.GetString(record.Slice(33, nameLength)).Replace(";1", "", StringComparison.OrdinalIgnoreCase);
                    if (files.Any(file => file.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException("The ISO root contains ambiguous duplicate file identities.");
                    files.Add(new(name, lba, size, directoryOffset + offset, (record[25] & 2) != 0, directoryLba));
                }
                offset += length;
            }
        }
        if (specialNames.Count != 2) throw new InvalidDataException("An ISO directory lacks exact self/parent ownership records.");
        return files.AsReadOnly();
    }

    private static IReadOnlyList<NativeFlatIsoFile> ReadCompleteInventory(Stream image, DiscLayout layout, IReadOnlyList<NativeFlatIsoFile> root)
    {
        List<NativeFlatIsoFile> inventory = [new("", layout.RootExtent, layout.RootLength, 0, true, layout.RootExtent)];
        inventory.AddRange(root);
        Queue<NativeFlatIsoFile> pending = new(root.Where(file => file.IsDirectory));
        HashSet<int> visited = [layout.RootExtent];
        long directoryBytes = layout.RootLength;
        while (pending.TryDequeue(out NativeFlatIsoFile? directory))
        {
            if (!visited.Add(directory.Lba) || visited.Count > 256 || (directoryBytes += directory.Size) > 8 * 1024 * 1024)
                throw new InvalidDataException("ISO directory aliases, cycles or excessive inventory prevent safe allocation.");
            IReadOnlyList<NativeFlatIsoFile> children = ReadDirectory(image, layout, directory.Lba, directory.Size, directory.ParentLba);
            inventory.AddRange(children);
            foreach (NativeFlatIsoFile child in children.Where(file => file.IsDirectory)) pending.Enqueue(child);
        }
        return inventory.AsReadOnly();
    }

    private static void ValidateMetadata(Stream image, DiscLayout layout, IReadOnlyList<NativeFlatIsoFile> files, long start, long end)
    {
        byte[] pvd = DiscImage.ReadFileBytes(image, layout, 16, 0, 2048);
        int volumeSectors = Both(pvd, 80), pathBytes = Both(pvd, 132);
        if (volumeSectors <= 0 || volumeSectors > image.Length / layout.SectorSize || pathBytes <= 0 || pathBytes > 64 * 1024 ||
            files.Any(file => file.Lba + (file.Size + 2047L) / 2048 > volumeSectors) || end > volumeSectors)
            throw new InvalidDataException("ISO volume/path-table bounds do not certify structural allocation.");
        bool terminated = false;
        for (int lba = 16; lba < 48; lba++)
        {
            byte[] descriptor = DiscImage.ReadFileBytes(image, layout, lba, 0, 2048);
            if (!descriptor.AsSpan(1, 5).SequenceEqual("CD001"u8) || descriptor[6] != 1 ||
                descriptor[0] != (lba == 16 ? 1 : 255))
                throw new InvalidDataException("Unknown ISO volume descriptors prevent exact filesystem ownership.");
            RequireOutside(lba, 1);
            if (descriptor[0] == 255) { terminated = true; break; }
        }
        if (!terminated) throw new InvalidDataException("ISO volume descriptors have no bounded terminator.");
        foreach ((int offset, bool big, bool optional) in new[] { (140, false, false), (144, false, true), (148, true, false), (152, true, true) })
        {
            uint rawLba = big ? BinaryPrimitives.ReadUInt32BigEndian(pvd.AsSpan(offset)) : BinaryPrimitives.ReadUInt32LittleEndian(pvd.AsSpan(offset));
            if (optional && rawLba == 0) continue;
            if (rawLba == 0 || rawLba > int.MaxValue) throw new InvalidDataException("An ISO path table has no supported extent.");
            int lba = (int)rawLba;
            RequireOutside(lba, (pathBytes + 2047L) / 2048);
            byte[] table = DiscImage.ReadFileBytes(image, layout, lba, 0, pathBytes);
            List<(string Name, int Lba, int Parent)> rows = [];
            for (int cursor = 0; cursor < table.Length;)
            {
                if (table.Length - cursor < 8 || table[cursor] == 0 || table[cursor + 1] != 0)
                    throw new InvalidDataException("An ISO path-table record has unknown extent ownership.");
                int nameBytes = table[cursor], length = 8 + nameBytes + nameBytes % 2;
                if (length > table.Length - cursor) throw new InvalidDataException("An ISO path-table identifier exceeds its record.");
                ReadOnlySpan<byte> row = table.AsSpan(cursor, length);
                uint extent = big ? BinaryPrimitives.ReadUInt32BigEndian(row[2..]) : BinaryPrimitives.ReadUInt32LittleEndian(row[2..]);
                int parent = big ? BinaryPrimitives.ReadUInt16BigEndian(row[6..]) : BinaryPrimitives.ReadUInt16LittleEndian(row[6..]);
                if (extent > int.MaxValue || parent < 1 || parent > Math.Max(1, rows.Count))
                    throw new InvalidDataException("An ISO path-table directory/parent is out of bounds.");
                string name = rows.Count == 0 && nameBytes == 1 && row[8] == 0 ? "" : Encoding.ASCII.GetString(row.Slice(8, nameBytes));
                rows.Add((name, (int)extent, parent)); cursor += length;
            }
            NativeFlatIsoFile[] directories = files.Where(file => file.IsDirectory).ToArray();
            if (rows.Count != directories.Length || rows[0] != ("", layout.RootExtent, 1))
                throw new InvalidDataException("ISO path-table directories differ from the complete file inventory.");
            HashSet<int> seen = [];
            foreach (var row in rows)
                if (!seen.Add(row.Lba) || !directories.Any(directory => directory.Lba == row.Lba && directory.Name == row.Name && directory.ParentLba == rows[row.Parent - 1].Lba))
                    throw new InvalidDataException("ISO path-table directory ownership is inconsistent.");
        }

        void RequireOutside(long lba, long sectors)
        {
            if (lba < 0 || sectors <= 0 || lba + sectors > volumeSectors || lba < end && start < lba + sectors)
                throw new InvalidDataException("Structural allocation overlaps ISO descriptor/path-table metadata.");
        }
    }

    private static byte[] ReadCheckedForm1(Stream source, DiscLayout layout, int lba)
    { byte[] bytes = ReadSector(source, layout, lba); RequireForm1Integrity(bytes, layout, lba); return bytes; }
    private static void RequireForm1Integrity(byte[] bytes, DiscLayout layout, int lba)
    {
        RawMode2Form1SectorIntegrity.RequireLogicalWriteSector(bytes, layout, lba);
        if ((bytes[layout.UserOffset - 6] & 0x08) == 0)
            throw new InvalidDataException("A structural file boundary is not MODE2 Form1 data.");
        byte[] rebuilt = bytes.ToArray(); RawMode2Form1SectorIntegrity.RebuildLogicalWriteSector(rebuilt, layout, lba);
        if (!rebuilt.AsSpan().SequenceEqual(bytes)) throw new InvalidDataException("A structural file boundary has invalid EDC/ECC.");
    }
    private static byte[] ReadSector(Stream stream, DiscLayout layout, int lba)
    {
        long offset = checked((long)lba * layout.SectorSize);
        if (lba < 0 || offset > stream.Length - layout.SectorSize) throw new InvalidDataException("A structural sector is outside the exact image.");
        byte[] bytes = new byte[layout.SectorSize]; stream.Position = offset; stream.ReadExactly(bytes); return bytes;
    }
    private static int Both(ReadOnlySpan<byte> record, int offset)
    {
        uint little = BinaryPrimitives.ReadUInt32LittleEndian(record[offset..]), big = BinaryPrimitives.ReadUInt32BigEndian(record[(offset + 4)..]);
        if (little != big || little > int.MaxValue) throw new InvalidDataException("An ISO extent has mismatched endian fields or exceeds supported bounds.");
        return (int)little;
    }
    private static bool IsExecutable(string name) => new[] { "SCUS_", "SCES_", "SLUS_", "SLES_" }.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}

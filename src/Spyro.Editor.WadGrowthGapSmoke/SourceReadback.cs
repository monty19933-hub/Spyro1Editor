using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

internal static class SourceReadback
{
    internal static void Verify(string sourcePath, string outputPath)
    {
        const int sectorSize = 2352;
        using FileStream source = File.OpenRead(sourcePath), output = File.OpenRead(outputPath);
        int checks = 0;
        Check(source.Length == output.Length && source.Length % sectorSize == 0, "Physical size changed.");
        (int sourceWad, int sourceWadSize, int sourceExe, int sourceExeSize, int root, int rootLength) = ReadExtents(source);
        (int outputWad, int outputWadSize, int outputExe, int outputExeSize, int outputRoot, int outputRootLength) = ReadExtents(output);
        Check(sourceWad == outputWad && root == outputRoot && rootLength == outputRootLength && sourceExeSize == outputExeSize &&
            outputWadSize > sourceWadSize && (outputWadSize - sourceWadSize) % 2048 == 0 &&
            outputExe == sourceExe + (outputWadSize - sourceWadSize) / 2048, "Final ISO extent geometry is inconsistent.");
        int outputEnd = outputExe + outputExeSize / 2048;
        using IncrementalHash sourceHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256), outputHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        source.Position = output.Position = 0;
        byte[] before = new byte[sectorSize], after = new byte[sectorSize];
        int changed = 0;
        for (int lba = 0; lba < source.Length / sectorSize; lba++)
        {
            source.ReadExactly(before); output.ReadExactly(after); sourceHash.AppendData(before); outputHash.AppendData(after);
            if (before.AsSpan().SequenceEqual(after)) continue;
            changed++;
            Check(lba >= sourceWad && lba < outputEnd || lba >= root && lba < root + (rootLength + 2047) / 2048,
                "An unowned physical sector changed.");
            GapFixture.CheckPhysicalSector(after, sectorSize, lba, Check);
            Check(before.AsSpan(0, 16).SequenceEqual(after.AsSpan(0, 16)), "Sync/MSF/header bytes changed.");
        }
        string actualSourceHash = Convert.ToHexString(sourceHash.GetHashAndReset());
        Check(actualSourceHash.Equals("fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37", StringComparison.OrdinalIgnoreCase),
            "Original retail source identity changed.");
        for (int index = 0; index < sourceExeSize / 2048; index++)
        {
            byte[] original = LogicalSector(source, sourceExe + index), relocated = LogicalSector(output, outputExe + index);
            Check(original.SequenceEqual(relocated), "Executable payload differs after relocation.");
        }
        Console.WriteLine($"WAD_GROWTH_SOURCE_READBACK_OK checks={checks} independentlyVerifiedChangedSectors={changed} growth={outputWadSize-sourceWadSize} sourceSha256={actualSourceHash} outputSha256={Convert.ToHexString(outputHash.GetHashAndReset())}");

        void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidDataException(message); }
    }

    private static (int, int, int, int, int, int) ReadExtents(FileStream image)
    {
        byte[] pvd = LogicalSector(image, 16);
        int root = Int(pvd, 158), rootLength = Int(pvd, 166), wad = -1, wadSize = 0, exe = -1, exeSize = 0;
        for (int sector = 0; sector < (rootLength + 2047) / 2048; sector++)
        {
            byte[] data = LogicalSector(image, root + sector);
            for (int offset = 0; offset < data.Length && data[offset] != 0; offset += data[offset])
            {
                string name = Encoding.ASCII.GetString(data, offset + 33, data[offset + 32]);
                if (name.StartsWith("WAD.WAD;", StringComparison.Ordinal)) { wad = Int(data, offset + 2); wadSize = Int(data, offset + 10); }
                if (name.StartsWith("SCUS_", StringComparison.Ordinal)) { exe = Int(data, offset + 2); exeSize = Int(data, offset + 10); }
            }
        }
        if (wad < 0 || exe < 0) throw new InvalidDataException("Required source files missing.");
        return (wad, wadSize, exe, exeSize, root, rootLength);
    }
    private static byte[] LogicalSector(FileStream image, int lba)
    { byte[] bytes = new byte[2048]; image.Position = checked(lba * 2352L + 24); image.ReadExactly(bytes); return bytes; }
    private static int Int(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
}

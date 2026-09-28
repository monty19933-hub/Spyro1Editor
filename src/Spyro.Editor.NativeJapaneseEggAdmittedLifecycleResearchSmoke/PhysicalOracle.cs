using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class PhysicalOracle
{
    internal static (string Hash,int Changed) Check(NativeJapaneseEggAdmittedLifecycleResearchPlan plan,Action<bool,string> check)
    {
        var p = plan.Composition; using FileStream source = new(plan.SourceImagePath,FileMode.Open,FileAccess.Read,FileShare.Read);
        byte[] originalWad = DiscImage.ReadFileBytes(source,plan.Layout,37,0,0x6927000), originalExe = p.CopyOriginalExecutable();
        var replacements = new Dictionary<int,byte[]> { [2] = p.CopyCardOverlay(), [9] = p.CopyArtisansOverlay(), [10] = p.CopyLevelData() };
        byte[] wad = new byte[checked(originalWad.Length+p.EntryOwners.Sum(e => e.FinalLength-e.OriginalLength))];
        originalWad.AsSpan(0,0x800).CopyTo(wad); int oldCursor = 0x800, cursor = 0x800;
        for (int index = 0; index < 256; index++)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(originalWad.AsSpan(index*8));
            int length = BinaryPrimitives.ReadInt32LittleEndian(originalWad.AsSpan(index*8+4));
            if (offset == 0 && length == 0) continue;
            check(offset == oldCursor,"Independent original directory coverage.");
            byte[] body = replacements.GetValueOrDefault(index) ?? originalWad.AsSpan(offset,length).ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(wad.AsSpan(index*8),cursor);
            BinaryPrimitives.WriteInt32LittleEndian(wad.AsSpan(index*8+4),body.Length);
            body.CopyTo(wad,cursor); oldCursor += length; cursor += body.Length;
        }
        check(oldCursor == originalWad.Length && cursor == wad.Length && wad.SequenceEqual(plan.CopyWad()),"Independent complete packed WAD and complement.");
        byte[] exe = originalExe.ToArray();
        void Put(int at,uint word) => BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(at),word);
        foreach (int at in new[] { 0x3030,0x1EAFC,0x23E24 }) { Put(at,0x3C028008); Put(at+4,0x24427A38); }
        uint anchor = checked(0x8007AA38u+(uint)replacements[9].Length);
        Put(0x4AF18,0x3C020000|((anchor+0x8000)>>16)); Put(0x4AF1C,0x24420000|(anchor&0xFFFF));
        Put(0x10,0x80075640); Put(0x65DA4,0xA00);
        uint[] stub = [0x3C02801F,0x3442F600,0x3C03801F,0x3463F800,0xAC400000,0x24420004,0x0043082B,0x1420FFFC,0,0x08016E38,0];
        for (int i = 0; i < stub.Length; i++) Put(0x65E40+i*4,stub[i]);
        uint[] reset = [0x3C018007,0xAC205860,0xAC205750,0xAC205810,0xAC20587C,0xAC2056C8,0xAC205830,0xAC2058E8,
            0xAC22580C,0xAC205838,0xAC20583C,0x8C2155A4,0,0x2421F600,0x14200003,0,0x3C018020,0xA020F616,0x3C018007,0];
        for (int i = 0; i < reset.Length; i++) Put(0x2F00+i*4,reset[i]);
        check(exe.SequenceEqual(plan.CopyExecutable()),"Independent complete EXE opcode recipe and complement.");
        int exeLba = 37+wad.Length/0x800, endLba = exeLba+exe.Length/0x800;
        byte[] root = DiscImage.ReadFileBytes(source,plan.Layout,plan.Layout.RootExtent,0,plan.Layout.RootLength); int patched = 0;
        for (int at = 0; at < root.Length;)
        {
            int length = root[at]; if (length == 0) { at = ((at/0x800)+1)*0x800; continue; }
            string name = Encoding.ASCII.GetString(root,at+33,root[at+32]).Split(';')[0];
            if (name is "WAD.WAD" or "SCUS_942.28")
            {
                int extent = name == "WAD.WAD" ? 37 : exeLba, bytes = name == "WAD.WAD" ? wad.Length : exe.Length;
                BinaryPrimitives.WriteInt32LittleEndian(root.AsSpan(at+2),extent); BinaryPrimitives.WriteInt32BigEndian(root.AsSpan(at+6),extent);
                BinaryPrimitives.WriteInt32LittleEndian(root.AsSpan(at+10),bytes); BinaryPrimitives.WriteInt32BigEndian(root.AsSpan(at+14),bytes); patched++;
            }
            at += length;
        }
        check(patched == 2 && root.SequenceEqual(plan.CopyRoot()),"Independent ISO owner pairs and root complement.");
        source.Position = 0; using var beforeHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var afterHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); byte[] raw = new byte[2352]; int changed = 0;
        for (int lba = 0; lba < source.Length/2352; lba++)
        {
            source.ReadExactly(raw); beforeHash.AppendData(raw); byte[] expected = raw.ToArray();
            if (lba >= 54079 && lba < endLba) expected = RawMode2Form1SectorIntegrity.PrepareOwnedZeroPaddingSector(raw,plan.Layout,lba);
            if (lba >= 37 && lba < exeLba) wad.AsSpan((lba-37)*0x800,0x800).CopyTo(expected.AsSpan(24));
            if (lba >= exeLba && lba < endLba) exe.AsSpan((lba-exeLba)*0x800,0x800).CopyTo(expected.AsSpan(24));
            int rootAt = (lba-plan.Layout.RootExtent)*0x800;
            if (rootAt >= 0 && rootAt < root.Length) root.AsSpan(rootAt,Math.Min(0x800,root.Length-rootAt)).CopyTo(expected.AsSpan(24));
            if (lba is 53874 or 54078 || lba == exeLba-1 || lba == endLba-1)
                expected[18] = expected[22] = (byte)((expected[18]&~0x81)|(lba == exeLba-1 || lba == endLba-1 ? 0x81 : 0));
            if (!expected.AsSpan().SequenceEqual(raw)) { expected = RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(expected,lba); changed++; }
            if (!expected.AsSpan().SequenceEqual(plan.ExpectedSector(lba,raw))) throw new Exception("Independent full raw sector differs at " + lba);
            if ((lba < 37 || lba >= endLba) && lba != plan.Layout.RootExtent && !expected.AsSpan().SequenceEqual(raw))
                throw new Exception("Outer physical complement changed at " + lba);
            afterHash.AppendData(expected);
        }
        check(source.Length == NativeMobySourceInventory.OriginalImageByteLength && source.Position == source.Length &&
            Convert.ToHexStringLower(beforeHash.GetHashAndReset()) == NativeMobySourceInventory.OriginalImageSha256,"Complete original identity after every-sector comparison.");
        return (Convert.ToHexStringLower(afterHash.GetHashAndReset()),changed);
    }
}

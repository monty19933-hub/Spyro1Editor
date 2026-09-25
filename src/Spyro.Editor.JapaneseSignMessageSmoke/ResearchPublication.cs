using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

// Explicit developer-only original-JP reference experiment. This is not the
// editor's USA importer, a general publication authority or runtime acceptance.
internal static class ResearchPublication
{
    private static readonly int[] ArtisansSignIndices = Enumerable.Range(173, 15).Concat(Enumerable.Range(194, 3)).ToArray();
    private const string OriginalCueSha256 = "0faaba827d5a480062dca5eb0a74d31b3079ef9a52071ba3c22acb028fea02ba";
    internal static void Write(string originalPath, string cuePath, string newDirectory)
    {
        originalPath = Path.GetFullPath(originalPath); cuePath = Path.GetFullPath(cuePath);
        string output = Path.GetFullPath(newDirectory);
        RefuseLinks(originalPath); RefuseLinks(cuePath); RefuseLinks(output);
        if (Path.Exists(output)) throw new InvalidDataException("Reference output must be a new, absent directory.");
        byte[] cueSnapshot = File.ReadAllBytes(cuePath);
        if (Hash(cueSnapshot) != OriginalCueSha256 ||
            !DiscCueSheet.ReferencesDataImageSnapshot(cueSnapshot, cuePath, originalPath))
            throw new InvalidDataException("Reference experiment requires the exact supplied Japanese CUE/BIN pair.");
        using var source = NativeRegionalSourceSession.Open(originalPath, NativeDiscRevision.JapanScps10083);
        var signs = NativeJapaneseSignSource.CaptureAll(source).Where(s => s.DataWadEntry == 23).ToArray();
        if (!signs.Select(s => s.TrueIndex).SequenceEqual(ArtisansSignIndices))
            throw new InvalidDataException("Original Artisans sign identities changed.");
        var requests = signs.Select(s => new NativeJapaneseSignEditRequest(s,
            NativeJapaneseSignUnicodeCodec.FromEditableText($"{s.TrueIndex} ガパ\n\\c2カメラ\n"))).ToArray();
        var plan = NativeJapaneseSignEditPlan.Compile(source, requests);
        foreach (var request in requests)
            if (!request.Replacement.CopyNativeBytes().AsSpan().SequenceEqual(ExpectedMessage(request.Target.TrueIndex)))
                throw new InvalidDataException("Reference message encoding changed from the independently checked native glyph sequence.");
        if (plan.Writes.Count != 18 || plan.Writes.Any(w => w.DataWadEntry != 23) ||
            !plan.Writes.Select(w => w.TrueIndex).SequenceEqual(ArtisansSignIndices))
            throw new InvalidDataException("Reference writes exceed the existing Artisans sign scope.");
        byte[] outputCueBytes = BuildReferenceCue(cueSnapshot);
        byte[] originalLevel = source.ReadWadEntry(23);
        source.VerifyUnchanged();
        using FileStream original = new(originalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        RequireImage(original, source.SourceImageSha256, source.SourceImageByteLength);
        DiscLayout layout = DiscImage.DetectLayout(original);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(original, layout, n => n == "WAD.WAD");
        if (wad.Lba != 500 || layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("Reference physical owner changed.");
        // Absent directory, random private reservation and CreateNew outputs:
        // failures preserve unverified evidence; no old candidate is overwritten.
        Directory.CreateDirectory(output);
        RefuseLinks(output);
        if (Directory.EnumerateFileSystemEntries(output).Any())
            throw new InvalidDataException("Reference output directory acquired unrelated entries during reservation.");
        string reservationPath = Path.Combine(output, ".research-attempt");
        using var reservation = new FileStream(reservationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        byte[] reservationBytes = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("D"));
        reservation.Write(reservationBytes); reservation.Flush(true);
        string staged = Path.Combine(output, "japanese-sign-reference.unverified.bin");
        string final = Path.Combine(output, "japanese-sign-reference.bin");
        string verifiedOutputHash;
        using (var candidate = new FileStream(staged, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            original.Position = 0; original.CopyTo(candidate); candidate.Flush(true);
            RequireImage(candidate, source.SourceImageSha256, source.SourceImageByteLength);
            foreach (var write in plan.Writes)
            {
                byte[] before = DiscImage.ReadFileBytes(candidate, layout, wad.Lba, write.WadOffset, write.ByteLength);
                if (!before.AsSpan().SequenceEqual(write.CopyBeforeBytes()))
                    throw new InvalidDataException("Reference candidate preimage changed before its sign write.");
                DiscImage.WriteFileBytes(candidate, layout, wad.Lba, write.WadOffset, write.CopyAfterBytes());
            }
            candidate.Flush(true);
            byte[] editedLevel = DiscImage.ReadFileBytes(candidate, layout, wad.Lba,
                source.Entries[23].Offset, originalLevel.Length);
            plan.VerifyLevelReadback(23, originalLevel, editedLevel);
            _ = VerifyPhysicalComplement(original, candidate, layout, wad.Lba, plan);
            verifiedOutputHash = StreamHash(candidate);
        }
        source.VerifyUnchanged();
        RequireImage(original, source.SourceImageSha256, source.SourceImageByteLength);
        if (!File.ReadAllBytes(cuePath).AsSpan().SequenceEqual(cueSnapshot))
            throw new InvalidDataException("Original CUE changed during the reference experiment.");
        RefuseLinks(staged); RefuseLinks(output);
        File.Move(staged, final, overwrite: false);
        WriteNew(Path.Combine(output, "japanese-sign-reference.cue"), outputCueBytes);
        RefuseLinks(final);
        using var namedOutput = File.OpenRead(final);
        RequireImage(namedOutput, verifiedOutputHash, source.SourceImageByteLength);
        string outputHash = verifiedOutputHash;
        var receipt = new
        {
            schemaVersion = 1, profile = "original-japanese-artisans-sign-message-reference-r02",
            source.SourceImageSha256, source.SourceImageByteLength, outputImageSha256 = outputHash,
            outputCueSha256 = Hash(outputCueBytes),
            signs = requests.Select((r, i) => new { r.Target.TrueIndex,
                editableText = NativeJapaneseSignUnicodeCodec.ToEditableText(r.Replacement),
                nativeBytesHex = r.Replacement.NativeBytesHex }),
            writes = plan.Writes.Select(w => new { w.WadOffset, w.ByteLength, w.BeforeSha256, w.AfterSha256,
                w.DataWadEntry, w.TrueIndex, w.TargetRecordSha256, w.FixupSha256 }),
            exactPhysicalComplementVerified = true, rawSectorIntegrityVerified = true,
            originalHeldAndNamedUnchanged = true, nativeUsaImportApproved = false,
            editorIntegrationApproved = false, runtimeVerified = false,
            scope = "Existing Japanese Artisans sign text only; all model/row/pointer/handler/font/save bytes unchanged."
        };
        WriteNew(Path.Combine(output, "reference-receipt.json"),
            JsonSerializer.SerializeToUtf8Bytes(receipt, new JsonSerializerOptions { WriteIndented = true }));
        source.VerifyUnchanged();
        RefuseLinks(cuePath);
        if (!File.ReadAllBytes(cuePath).AsSpan().SequenceEqual(cueSnapshot))
            throw new InvalidDataException("Original CUE changed before reference completion.");
        Console.WriteLine($"JAPANESE_SIGN_REFERENCE_WRITTEN signs=18 sourceUnchanged=true runtimeVerified=false imageSha256={outputHash} output={output}");
    }

    private static int VerifyPhysicalComplement(FileStream original, FileStream candidate, DiscLayout layout,
        int wadLba, NativeJapaneseSignEditPlan plan)
    {
        var sectors = new HashSet<int>(); var payloadBytes = new HashSet<long>();
        foreach (var patch in plan.Writes)
            for (int i = 0; i < patch.ByteLength; i++)
            {
                long physical = DiscImage.ConvertFileOffsetToImageOffset(layout, wadLba, patch.WadOffset + i);
                payloadBytes.Add(physical); sectors.Add(checked((int)(physical / 2352)));
            }
        if (RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(candidate, layout, sectors.Select(s => (s, 1))) != sectors.Count)
            throw new InvalidDataException("Edited sign sector integrity failed.");
        if (original.Length != candidate.Length) throw new InvalidDataException("Reference image length changed.");
        original.Position = candidate.Position = 0;
        byte[] first = new byte[128 * 1024], second = new byte[first.Length];
        for (long position = 0; position < original.Length; position += first.Length)
        {
            int count = (int)Math.Min(first.Length, original.Length - position);
            original.ReadExactly(first.AsSpan(0, count)); candidate.ReadExactly(second.AsSpan(0, count));
            if (first.AsSpan(0, count).SequenceEqual(second.AsSpan(0, count))) continue;
            for (int i = 0; i < count; i++)
            {
                if (first[i] == second[i]) continue;
                long physical = position + i;
                if (!IsAllowedDifference(physical, payloadBytes, sectors))
                    throw new InvalidDataException($"Unrelated reference-disc byte changed at {physical:X}.");
            }
        }
        return sectors.Count;
    }
    private static bool IsAllowedDifference(long physical, IReadOnlySet<long> payloadBytes, IReadOnlySet<int> sectors) =>
        physical >= 0 && (payloadBytes.Contains(physical) || physical / 2352 <= int.MaxValue &&
            sectors.Contains((int)(physical / 2352)) && physical % 2352 >= 2072);

    private static byte[] ExpectedMessage(int index)
    {
        if (!ArtisansSignIndices.Contains(index)) throw new InvalidDataException("Not a reference Artisans sign target.");
        // Native kana table: C5=カ, D9=ハ; BD/BC are the source-sign dakuten/handakuten.
        return Encoding.ASCII.GetBytes(index.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Concat(new byte[] { 0x20, 0xC5, 0xBD, 0xD9, 0xBC, 0xFF, 0x42, 0xC5, 0xE1, 0xE6, 0xFF }).ToArray();
    }

    private static byte[] BuildReferenceCue(byte[] snapshot)
    {
        if (Hash(snapshot) != OriginalCueSha256)
            throw new InvalidDataException("Reference CUE generation requires the exact authenticated snapshot.");
        string[] lines = DiscCueSheet.TextEncoding.GetString(snapshot).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        if (lines.Length != 3 || lines[0] != "FILE \"Spyro the Dragon (Japan).bin\" BINARY" ||
            lines[1] != "  TRACK 01 MODE2/2352" || lines[2] != "    INDEX 01 00:00:00")
            throw new InvalidDataException("Reference CUE snapshot geometry changed.");
        lines[0] = "FILE \"japanese-sign-reference.bin\" BINARY";
        return DiscCueSheet.TextEncoding.GetBytes(string.Join('\n', lines) + "\n");
    }

    internal static void RunSourceFree(Action<bool, string> check)
    {
        foreach (int index in ArtisansSignIndices)
        {
            byte[] expected = ExpectedMessage(index);
            check(expected.Length == 14 && expected.AsSpan().SequenceEqual(
                NativeJapaneseSignUnicodeCodec.FromEditableText($"{index} ガパ\n\\c2カメラ\n").CopyNativeBytes()),
                "Exact 14-byte message includes native final-line flush.");
        }
        void Refuse(Action action)
        { try { action(); } catch (InvalidDataException) { check(true, "Reference refusal"); return; } throw new Exception("Expected reference refusal."); }
        Refuse(() => ExpectedMessage(188)); Refuse(() => ExpectedMessage(193));
        Refuse(() => BuildReferenceCue([]));
        byte[] cue = Encoding.UTF8.GetBytes("FILE \"Spyro the Dragon (Japan).bin\" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n");
        check(Encoding.UTF8.GetString(BuildReferenceCue(cue)) ==
            "FILE \"japanese-sign-reference.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n", "Output CUE derives only from held snapshot.");
        HashSet<long> payload = [2 * 2352 + 24, 2 * 2352 + 2071]; HashSet<int> sectors = [2];
        foreach (long allowed in new long[] { 2 * 2352 + 24, 2 * 2352 + 2071, 2 * 2352 + 2072, 3 * 2352 - 1 })
            check(IsAllowedDifference(allowed, payload, sectors), "Only declared payload and touched-sector integrity differences are admitted.");
        foreach (long refused in new long[] { -1, 2 * 2352, 2 * 2352 + 23, 2 * 2352 + 25, 2352 + 2072, 3 * 2352, long.MaxValue })
            check(!IsAllowedDifference(refused, payload, sectors), "Unrelated payload/header/other-sector differences refused.");
    }
    private static void RefuseLinks(string path)
    {
        for (string? current = path; current != null; current = Path.GetDirectoryName(current))
            if (File.Exists(current) || Directory.Exists(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Reference experiment refuses symbolic-link paths.");
    }
    private static void RequireImage(FileStream stream, string hash, long length)
    {
        if (stream.Length != length || StreamHash(stream) != hash)
            throw new InvalidDataException("Reference image identity changed.");
    }
    private static string StreamHash(Stream stream) { stream.Position = 0; return Hash(SHA256.HashData(stream), alreadyHash: true); }
    private static string Hash(ReadOnlySpan<byte> bytes, bool alreadyHash = false) =>
        Convert.ToHexStringLower(alreadyHash ? bytes : SHA256.HashData(bytes));
    private static void WriteNew(string path, byte[] bytes)
    { using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); stream.Write(bytes); stream.Flush(true); }
}

using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

internal static class CueSnapshotCases
{
    public static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "Id65CompositionCueSnapshot-" + Guid.NewGuid().ToString("N"));
        var paths = UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(Path.Combine(root, "workspace"));
        Directory.CreateDirectory(paths.LockedBaseDirectoryPath);
        // Source-free minimal ISO descriptor, used only by the existing CUE
        // layout detector. This is not a native level/source certificate.
        byte[] image = new byte[18 * 2352];
        Span<byte> pvd = image.AsSpan(16 * 2352 + 24, 2048);
        pvd[0] = 1; "CD001"u8.CopyTo(pvd[1..]); pvd[6] = 1; pvd[156] = 34; pvd[188] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(pvd[128..], 2048);
        BinaryPrimitives.WriteUInt16BigEndian(pvd[130..], 2048);
        Both(pvd, 158, 17); Both(pvd, 166, 2048);
        File.WriteAllBytes(paths.LockedBaseImagePath, image);
        string text = $"FILE \"{Path.GetFileName(paths.LockedBaseImagePath)}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n";
        byte[] original = DiscCueSheet.TextEncoding.GetBytes(text);
        File.WriteAllBytes(paths.LockedBaseCuePath, original);
        string alternateCue = Path.Combine(paths.LockedBaseDirectoryPath, "alternate.cue");
        string alternateImage = Path.Combine(paths.LockedBaseDirectoryPath, "alternate.bin");
        File.WriteAllBytes(alternateCue, original); File.WriteAllBytes(alternateImage, image);
        int checks = 0;
        try
        {
            var frozen = Id65AuthoredCollisionMusicPublisher.SourceCueSnapshot.Capture(paths);
            frozen.RequireFresh(paths); checks++;
            byte[] output = frozen.CopyOutputBytes(); byte[] escaped = frozen.CopyOutputBytes(); escaped[0] ^= 1;
            Check(output.SequenceEqual(frozen.CopyOutputBytes()), "detached immutable rewritten CUE");
            Check(DiscCueSheet.TextEncoding.GetString(output).Contains(Id65AuthoredCollisionMusicPublisher.ImageName, StringComparison.Ordinal),
                "the frozen output remains bound to its publication image name");
            foreach (string changed in new[]
            {
                text.Replace("    INDEX", "    PREGAP 00:01:00\n    INDEX", StringComparison.Ordinal),
                text.Replace("FILE \"", "FILE \"./", StringComparison.Ordinal),
                text.Replace("\n", "\r\n", StringComparison.Ordinal),
                text.Replace(Path.GetFileName(paths.LockedBaseImagePath), "alternate.bin", StringComparison.Ordinal)
            })
            {
                File.WriteAllText(paths.LockedBaseCuePath, changed, DiscCueSheet.TextEncoding);
                Reject(() => frozen.RequireFresh(paths));
                File.WriteAllBytes(paths.LockedBaseCuePath, original);
                frozen.RequireFresh(paths); checks++;
            }
            Reject(() => frozen.RequireFresh(paths with { LockedBaseCuePath = alternateCue }));
            Reject(() => frozen.RequireFresh(paths with { LockedBaseImagePath = alternateImage }));
            File.Delete(paths.LockedBaseCuePath);
            Reject(() => frozen.RequireFresh(paths));
            Reject(() => Id65AuthoredCollisionMusicPublisher.SourceCueSnapshot.Capture(paths));
            File.CreateSymbolicLink(paths.LockedBaseCuePath, alternateCue);
            Reject(() => frozen.RequireFresh(paths));
            Reject(() => Id65AuthoredCollisionMusicPublisher.SourceCueSnapshot.Capture(paths));
            File.Delete(paths.LockedBaseCuePath);
            File.WriteAllBytes(paths.LockedBaseCuePath, original);
            File.Delete(paths.LockedBaseImagePath);
            File.CreateSymbolicLink(paths.LockedBaseImagePath, alternateImage);
            Reject(() => frozen.RequireFresh(paths));
            Reject(() => Id65AuthoredCollisionMusicPublisher.SourceCueSnapshot.Capture(paths));
            File.Delete(paths.LockedBaseImagePath);
            File.WriteAllBytes(paths.LockedBaseImagePath, image);
            frozen.RequireFresh(paths); checks++;
            return checks;
        }
        finally
        {
            // Remove only this test's four explicit tiny files, never recurse.
            foreach (string path in new[] { paths.LockedBaseCuePath, paths.LockedBaseImagePath, alternateCue, alternateImage })
                File.Delete(path);
            Directory.Delete(paths.LockedBaseDirectoryPath, false);
        }
        void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); checks++; }
        void Reject(Action action)
        {
            try { action(); }
            catch (InvalidDataException exception) when (exception.Message.Contains("source CUE", StringComparison.Ordinal)) { checks++; return; }
            throw new InvalidOperationException("Expected exact source CUE rejection did not occur.");
        }
    }
    private static void Both(Span<byte> bytes, int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(bytes[offset..], value);
        BinaryPrimitives.WriteInt32BigEndian(bytes[(offset + 4)..], value);
    }
}

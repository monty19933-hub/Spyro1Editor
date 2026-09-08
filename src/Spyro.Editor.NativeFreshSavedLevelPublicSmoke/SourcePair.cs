using System.Security.Cryptography;
using static PublicCases;

// Public-only explicit source test owner; no Core helper/reflection/IO factory.
internal sealed class SourcePair(string bin, string cue)
{
    internal const string CueHash = "05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766";
    private const string BinHash = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    internal string BinPath => bin;
    internal string CuePath => cue;
    private FileStream? _bin, _cue;
    private string? _binMetadata, _cueMetadata;
    internal void Open()
    {
        _bin = new(bin, FileMode.Open, FileAccess.Read, FileShare.Read);
        _binMetadata = Metadata(bin); Verify(_bin, bin, 661547040, BinHash, _binMetadata);
        _cue = new(cue, FileMode.Open, FileAccess.Read, FileShare.Read);
        _cueMetadata = Metadata(cue); Verify(_cue, cue, 88, CueHash, _cueMetadata);
    }
    internal void Complete(Exception? primary)
    {
        List<Exception> errors = []; if (primary != null) errors.Add(primary);
        if (_bin != null) Attempt(() => Verify(_bin, bin, 661547040, BinHash, _binMetadata), errors);
        Current(bin, 661547040, BinHash, _binMetadata);
        if (_cue != null) Attempt(() => Verify(_cue, cue, 88, CueHash, _cueMetadata), errors);
        Current(cue, 88, CueHash, _cueMetadata);
        if (_cue != null) Attempt(_cue.Dispose, errors);
        if (_bin != null) Attempt(_bin.Dispose, errors);
        ThrowFailures(errors);
        void Current(string path, long length, string hash, string? metadata)
        {
            FileStream? file = null;
            try { file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read); Verify(file, path, length, hash, metadata); }
            catch (Exception error) { errors.Add(error); }
            finally { if (file != null) Attempt(file.Dispose, errors); }
        }
    }
    private static void Verify(Stream stream, string path, long length, string hash, string? metadata)
    {
        List<Exception> errors = [];
        string? before = null, digest = null;
        Attempt(() =>
        {
            before = Metadata(path);
            Check(metadata == null || before == metadata, "Source metadata before full read changed.");
        }, errors);
        // Rejection of the path or initial length must not suppress the actual
        // complete held bytes, independent hash comparison or explicit EOF read.
        Attempt(() => Check(stream.CanRead && stream.CanSeek, "Source stream capabilities."), errors);
        Attempt(() => Check(stream.Length == length, "Initial original length."), errors);
        Attempt(() => stream.Position = 0, errors);
        Attempt(() => digest = Convert.ToHexStringLower(SHA256.HashData(stream)), errors);
        if (digest != null)
            Attempt(() => Check(digest == hash, "Complete original SHA actual=" + digest + " expected=" + hash), errors);
        Attempt(() => Check(stream.Position == length, "Consumed original position."), errors);
        Attempt(() => Check(stream.ReadByte() == -1, "Actual original EOF."), errors);
        Attempt(() => Check(stream.Position == length, "Final original position."), errors);
        Attempt(() => Check(stream.Length == length, "Final original length."), errors);
        Attempt(() =>
        {
            string after = Metadata(path);
            Check((metadata == null || after == metadata) && (before == null || after == before),
                "Source metadata after full read changed.");
        }, errors);
        ThrowFailures(errors);
    }

    private static string Metadata(string path)
    {
        var file = new FileInfo(path);
        Check(file.Exists && file.LinkTarget == null && !Directory.Exists(path), "Original regular nonlink owner.");
        return $"{file.FullName}|{file.Length}|{file.CreationTimeUtc.Ticks}|{file.LastWriteTimeUtc.Ticks}";
    }
}

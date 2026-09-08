using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

// Exact mechanical extraction of the old fixed-a concrete source-owner guard.
// Historical error wording and all read/close order are deliberately unchanged.
internal sealed class NativeFreshResearchSourceOwners
{
    internal readonly string ImagePath, CuePath;
    internal string? ImageIdentity, CueIdentity;
    internal byte[]? CueSnapshot;
    internal FileStream? Bin, Cue, Output;
    internal NativeFreshResearchSourceOwners(string image, string cue, byte[]? snapshot = null,
        string? imageIdentity = null, string? cueIdentity = null)
    { ImagePath = image; CuePath = cue; CueSnapshot = snapshot; ImageIdentity = imageIdentity; CueIdentity = cueIdentity; }

    internal void Open(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string imageIdentity = PathIdentity(ImagePath), cueIdentity = PathIdentity(CuePath);
        if ((ImageIdentity != null && ImageIdentity != imageIdentity) ||
            (CueIdentity != null && CueIdentity != cueIdentity))
            throw new InvalidDataException("Textured research logical source identity changed.");
        ImageIdentity = imageIdentity; CueIdentity = cueIdentity;
        Bin = new FileStream(ImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Cue = new FileStream(CuePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] snapshot = NativeId65FreshDiscResearchCandidatePlan.ReadCueSnapshot(Cue, token);
        if (CueSnapshot != null && !snapshot.SequenceEqual(CueSnapshot))
            throw new InvalidDataException("Textured research source CUE bytes changed.");
        CueSnapshot ??= snapshot;
        ValidateCue(snapshot, CuePath, ImagePath);
    }

    internal void Complete(Exception? primary)
    {
        List<Exception> failures = [];
        if (primary != null) failures.Add(primary);
        // Each observation is attempted independently, even after partial
        // acquisition or cancellation. Reopened handles close independently.
        if (Bin != null) Try(() => RequireOriginal(Bin));
        Current(ImagePath, true);
        if (Cue != null) Try(() => RequireCue(Cue));
        Current(CuePath, false);
        if (Output != null) Try(Output.Dispose);
        if (Cue != null) Try(Cue.Dispose);
        if (Bin != null) Try(Bin.Dispose);
        ThrowFailures(failures);

        void Try(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        void Current(string path, bool image)
        {
            FileStream? current = null;
            try
            {
                // Hold the current name even if its identity later fails.
                current = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (image) RequireOriginal(current); else RequireCue(current);
            }
            catch (Exception error) { failures.Add(error); }
            finally { if (current != null) Try(current.Dispose); }
        }
    }

    private void RequireOriginal(Stream input)
    {
        RequireIdentity(ImagePath, ImageIdentity);
        if (!input.CanRead || !input.CanSeek || input.Length != NativeId65FreshDiscSource.ImageByteLength)
            throw new InvalidDataException("Textured research requires the complete exact-length original source.");
        input.Position = 0;
        string digest = Convert.ToHexStringLower(SHA256.HashData(input));
        NativeId65FreshDiscSource.RequireActualEof(input, CancellationToken.None);
        if (digest != NativeId65FreshDiscSource.OriginalSha256)
            throw new InvalidDataException("Textured research complete original source changed during finalization.");
        RequireIdentity(ImagePath, ImageIdentity);
    }
    private void RequireCue(Stream input)
    {
        RequireIdentity(CuePath, CueIdentity);
        input.Position = 0;
        byte[] bytes = NativeId65FreshDiscResearchCandidatePlan.ReadCueSnapshot(input, CancellationToken.None);
        if (CueSnapshot != null && !bytes.SequenceEqual(CueSnapshot))
            throw new InvalidDataException("Textured research source CUE changed during finalization.");
        ValidateCue(bytes, CuePath, ImagePath);
        RequireIdentity(CuePath, CueIdentity);
    }
    private static void RequireIdentity(string path, string? expected)
    {
        if (expected != null && PathIdentity(path) != expected)
            throw new InvalidDataException("Textured research current logical source path changed.");
    }

    // Pure accounting only. It cannot issue a plan, inject IO or claim a commit.
    internal static void ThrowFailures(IReadOnlyList<Exception> failures)
    {
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1)
            throw new IOException("Textured research operation failed with additional source/close failures.",
                new AggregateException(failures));
    }

    private static void ValidateCue(ReadOnlySpan<byte> bytes, string cue, string image)
    {
        try { _ = NativeId65FreshDiscResearchCandidatePlan.CompileCueForSmoke(bytes, cue, image); }
        catch (DecoderFallbackException error)
        { throw new InvalidDataException("Textured research source CUE is not valid UTF8.", error); }
    }

    private static string PathIdentity(string path)
    {
        FileInfo file = new(path);
        if (!file.Exists || Directory.Exists(path))
            throw new InvalidDataException("Textured research source path is missing or not a file.");
        List<string> identity = [file.FullName, file.LinkTarget ?? ""];
        FileSystemInfo resolved = file.ResolveLinkTarget(true) ?? file;
        identity.Add(resolved.FullName);
        foreach (string leaf in new[] { file.FullName, resolved.FullName })
            for (DirectoryInfo? parent = new FileInfo(leaf).Directory; parent != null; parent = parent.Parent)
            {
                if (identity.Count > 132) throw new InvalidDataException("Textured research source path depth exceeds its bound.");
                identity.Add(parent.FullName + "|" + parent.LinkTarget);
            }
        return string.Join('\n', identity);
    }

}

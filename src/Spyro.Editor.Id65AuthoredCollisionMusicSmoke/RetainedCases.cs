using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

internal static class RetainedCases
{
    // Reopen only an exact test workspace created by CandidateCases. The
    // canonical document is restored after replaying each immutable published
    // input; no candidate is copied, regenerated, patched or replaced.
    public static async Task RunAsync(string fixtureRoot)
    {
        fixtureRoot = Path.GetFullPath(fixtureRoot);
        if (!Path.GetFileName(fixtureRoot).StartsWith("Id65AuthoredCollisionMusicSmoke-", StringComparison.Ordinal) ||
            !Directory.Exists(fixtureRoot) || new DirectoryInfo(fixtureRoot).LinkTarget is not null)
            throw new InvalidDataException("Retained verification requires the exact existing composition test root.");
        var paths = UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(Path.Combine(fixtureRoot, "workspace"));
        var manifest = UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(paths);
        var stored = await Id65AuthoringEditorDocumentStore.LoadAsync(paths, manifest, new(null))
            ?? throw new InvalidDataException("Retained composition document is missing.");
        var originalDocument = stored.Document;
        byte[] originalEnvelope = File.ReadAllBytes(Id65AuthoringEditorDocumentStore.GetPath(paths));
        string sourceBefore = HashFile(paths.LockedBaseImagePath);
        string root = Path.Combine(paths.RootPath, Id65AuthoredCollisionMusicPublisher.RootName);
        string[] directories = Directory.GetDirectories(root, "composition-*").Order(StringComparer.Ordinal).ToArray();
        if (directories.Length != 2) throw new InvalidDataException("Expected exactly two retained composition candidate directories.");
        int verified = 0;
        try
        {
            foreach (string directory in directories)
            {
                byte[] inputBytes = File.ReadAllBytes(Path.Combine(directory, Id65AuthoredCollisionMusicPublisher.InputName));
                var input = Id65AuthoredCollisionMusicInput.Parse(inputBytes);
                stored = await Id65AuthoringEditorDocumentStore.SaveAsync(paths, manifest, input.Music.BaselineDocument,
                    new(null), stored.EnvelopeCanonicalSha256);
                if (stored.EnvelopeCanonicalSha256 != input.Music.BaselineEnvelopeCanonicalSha256)
                    throw new InvalidDataException("Published input did not reproduce its exact canonical stored envelope.");
                string image = Path.Combine(directory, Id65AuthoredCollisionMusicPublisher.ImageName);
                string before = HashFile(image);
                var result = await Id65AuthoredCollisionMusicPublisher.PublishAsync(new(paths, manifest, input));
                if (!result.Reused || result.Directory != directory || result.Readback.OutputImageSha256 != before || HashFile(image) != before ||
                    !File.ReadAllBytes(result.Input).SequenceEqual(inputBytes))
                    throw new InvalidDataException("Retained input/export replay modified or failed to reuse its exact immutable artifact set.");
                Console.WriteLine($"Retained composition verified: track={input.Music.SelectedTrackId} plan={result.Readback.PlanSha256} bin={before}");
                verified++;
            }
        }
        finally
        {
            stored = await Id65AuthoringEditorDocumentStore.SaveAsync(paths, manifest, originalDocument, new(null), stored.EnvelopeCanonicalSha256);
        }
        if (HashFile(paths.LockedBaseImagePath) != sourceBefore ||
            !File.ReadAllBytes(Id65AuthoringEditorDocumentStore.GetPath(paths)).SequenceEqual(originalEnvelope))
            throw new InvalidDataException("Retained verification failed source or exact canonical-document restoration.");
        Console.WriteLine($"ID65_AUTHORED_COLLISION_MUSIC_RETAINED_OK candidates={verified} publishedInputReplay=1 exactReuse=1 sourceAndDocumentRestored=1 runtime=false");
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}

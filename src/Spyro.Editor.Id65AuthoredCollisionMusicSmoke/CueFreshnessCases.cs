using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

internal static class CueFreshnessCases
{
    // No existing source or candidate is mutated. The copied candidate set is
    // used only for strict-reuse tests, never as a compiler or staging input.
    public static async Task RunAsync(string fixtureRoot)
    {
        fixtureRoot = Path.GetFullPath(fixtureRoot);
        if (!Path.GetFileName(fixtureRoot).StartsWith("Id65AuthoredCollisionMusicSmoke-", StringComparison.Ordinal) ||
            !Directory.Exists(fixtureRoot) || new DirectoryInfo(fixtureRoot).LinkTarget is not null)
            throw new InvalidDataException("CUE freshness regression requires an exact retained composition fixture.");
        var original = UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(Path.Combine(fixtureRoot, "workspace"));
        string root = Path.Combine(Path.GetDirectoryName(fixtureRoot)!, "Id65AuthoredCollisionMusicCueSmoke-" + Guid.NewGuid().ToString("N"));
        var paths = UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(Path.Combine(root, "workspace"));
        Directory.CreateDirectory(paths.LockedBaseDirectoryPath); Directory.CreateDirectory(paths.AuthoredEditsDirectoryPath);
        byte[] originalCue = File.ReadAllBytes(original.LockedBaseCuePath);
        string sourceHash = HashFile(original.LockedBaseImagePath);
        File.Copy(original.LockedBaseImagePath, paths.LockedBaseImagePath, false);
        File.WriteAllBytes(paths.LockedBaseCuePath, originalCue);
        var manifest = UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(paths);
        File.WriteAllText(paths.ManifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, Converters = { new JsonStringEnumConverter() } }) + "\n", new UTF8Encoding(false));
        string parent = Directory.GetDirectories(Path.Combine(original.RootPath, Id65AuthoredCollisionMusicPublisher.RootName), "composition-*")
            .Single(directory => Id65AuthoredCollisionMusicInput.Parse(File.ReadAllBytes(Path.Combine(directory,
                Id65AuthoredCollisionMusicPublisher.InputName))).Music.SelectedTrackId == 2);
        string[] names = [Id65AuthoredCollisionMusicPublisher.ImageName, Id65AuthoredCollisionMusicPublisher.InputName,
            Id65AuthoredCollisionMusicPublisher.ReceiptName, Id65AuthoredCollisionMusicPublisher.CueName];
        var parentHashes = names.ToDictionary(name => name, name => HashFile(Path.Combine(parent, name)));
        var input = Id65AuthoredCollisionMusicInput.Parse(File.ReadAllBytes(Path.Combine(parent, Id65AuthoredCollisionMusicPublisher.InputName)));
        var stored = await Id65AuthoringEditorDocumentStore.SaveAsync(paths, manifest, input.Music.BaselineDocument, new(null), null);
        if (stored.EnvelopeCanonicalSha256 != input.Music.BaselineEnvelopeCanonicalSha256)
            throw new InvalidDataException("Isolated source CUE fixture did not reproduce the exact authored envelope.");
        var request = new Id65AuthoredCollisionMusicRequest(paths, manifest, input);
        string output = Path.Combine(paths.RootPath, Id65AuthoredCollisionMusicPublisher.RootName, Path.GetFileName(parent));
        int rejected = 0, checks = 0;
        Console.WriteLine("Retained CUE freshness fixture: " + root);
        try
        {
            foreach (string mutation in new[] { "valid-pregap", "deleted-cue", "equivalent-file-rebinding" })
            {
                await Refuse(mutation, reuse: false);
                Require(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(),
                    "precommit CUE refusal cleans only its own stage and lease without publishing artifacts");
            }
            Directory.CreateDirectory(output);
            foreach (string name in names) File.Copy(Path.Combine(parent, name), Path.Combine(output, name), false);
            foreach (string mutation in new[] { "valid-pregap", "deleted-cue", "equivalent-file-rebinding" })
            {
                await Refuse(mutation, reuse: true);
                Require(names.All(name => HashFile(Path.Combine(output, name)) == parentHashes[name]) &&
                    Directory.GetFileSystemEntries(output).Length == names.Length,
                    "late CUE reuse refusal preserves the exact four-artifact set without residue");
            }
            var exact = await Id65AuthoredCollisionMusicPublisher.PublishAsync(request);
            Require(exact.Reused && exact.Directory == output && names.All(name => HashFile(Path.Combine(output, name)) == parentHashes[name]),
                "restored exact CUE supports strict reuse with all previous artifact hashes unchanged");
        }
        finally { File.WriteAllBytes(paths.LockedBaseCuePath, originalCue); }
        Require(File.ReadAllBytes(original.LockedBaseCuePath).SequenceEqual(originalCue) &&
            HashFile(original.LockedBaseImagePath) == sourceHash && HashFile(paths.LockedBaseImagePath) == sourceHash &&
            names.All(name => HashFile(Path.Combine(parent, name)) == parentHashes[name]), "original source and retained candidates unchanged");
        File.WriteAllText(Path.Combine(root, "cue-freshness.evidence.json"), JsonSerializer.Serialize(new
        {
            checks, rejected, sourceHash, sourceCueSha256 = Hash(originalCue), originalFixture = fixtureRoot,
            candidateHashes = parentHashes, precommitRollback = true, exactReusePreserved = true,
            sourceCueExactBytesRestored = true, originalCandidatesUntouched = true, runtimeProven = false
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Console.WriteLine($"ID65_AUTHORED_COLLISION_MUSIC_CUE_FRESHNESS_OK checks={checks} rejected={rejected} " +
            "precommit=3 reuse=3 rollback=1 exactCueRestored=1 retainedCandidatesUnchanged=1 runtime=false");

        async Task Refuse(string mutation, bool reuse)
        {
            bool invoked = false;
            Console.WriteLine($"Checking late source CUE {mutation}: reuse={reuse}.");
            try
            {
                await Id65AuthoredCollisionMusicPublisher.PublishForSmokeAsync(request,
                    Id65AuthoredCollisionMusicFault.BeforePublication, _ =>
                    {
                        invoked = true;
                        if (mutation == "deleted-cue") File.Delete(paths.LockedBaseCuePath);
                        else
                        {
                            string before = DiscCueSheet.BuildForImage(paths.LockedBaseCuePath, paths.LockedBaseImagePath,
                                Id65AuthoredCollisionMusicPublisher.ImageName);
                            string text = DiscCueSheet.TextEncoding.GetString(originalCue);
                            string changed = mutation == "valid-pregap"
                                ? text.Replace("    INDEX 01 00:00:00", "    PREGAP 00:01:00\n    INDEX 01 00:00:00", StringComparison.Ordinal)
                                : text.Replace("FILE \"", "FILE \"./", StringComparison.Ordinal);
                            if (changed == text) throw new InvalidOperationException("Source CUE mutation made no change.");
                            File.WriteAllText(paths.LockedBaseCuePath, changed, DiscCueSheet.TextEncoding);
                            NativeLevelReplacementBaselineExporter.ValidateCue(paths.LockedBaseCuePath, paths.LockedBaseImagePath, "MODE2/2352");
                            string after = DiscCueSheet.BuildForImage(paths.LockedBaseCuePath, paths.LockedBaseImagePath,
                                Id65AuthoredCollisionMusicPublisher.ImageName);
                            Require((before == after) == (mutation == "equivalent-file-rebinding"),
                                "rebinding must change source identity even when the rewritten output CUE is identical");
                        }
                    });
                throw new InvalidOperationException("Late source CUE change was accepted.");
            }
            catch (InvalidDataException exception) when (invoked && exception.Message.Contains("source CUE", StringComparison.Ordinal))
            { rejected++; }
            finally { File.WriteAllBytes(paths.LockedBaseCuePath, originalCue); }
            Require(invoked, "the production late freshness boundary was exercised");
        }
        void Require(bool condition, string label)
        { if (!condition) throw new InvalidOperationException(label); checks++; }
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string HashFile(string path)
    { using FileStream stream = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(stream)); }
}

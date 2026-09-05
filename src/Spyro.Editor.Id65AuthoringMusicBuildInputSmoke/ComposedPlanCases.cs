using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

internal static class ComposedPlanCases
{
    public static async Task RunAsync(string repositoryRoot)
    {
        string source = Path.Combine(repositoryRoot, "_local", "id65-authoring-aggregate-workspace",
            "_research", "unusedlevel65blank", "v1", "locked-base", "unusedlevel65blank-locked-base-v1.bin");
        string sourceCue = Path.ChangeExtension(source, ".cue");
        Require(File.Exists(source) && File.Exists(sourceCue), "The exact source-aware music fixture is missing.");
        string sourceBefore = HashFile(source), cueBefore = HashFile(sourceCue);
        string root = Path.Combine(repositoryRoot, "_local", "Spyro.Editor.Id65AuthoringMusicDiscPlanSmoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        bool succeeded = false;
        Console.WriteLine("Music composed-plan isolated fixture: " + root);
        try
        {
            UnusedLevel65BlankLevelLabWorkspacePaths paths = UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(Path.Combine(root, "workspace"));
            Directory.CreateDirectory(paths.LockedBaseDirectoryPath);
            Directory.CreateDirectory(paths.AuthoredEditsDirectoryPath);
            File.Copy(source, paths.LockedBaseImagePath, overwrite: false);
            await File.WriteAllTextAsync(paths.LockedBaseCuePath,
                DiscImage.BuildCueText(sourceCue, Path.GetFileName(paths.LockedBaseImagePath)), Encoding.ASCII);
            UnusedLevel65BlankLevelLabManifest manifest = UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(paths);
            await File.WriteAllTextAsync(paths.ManifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            }) + "\n", new UTF8Encoding(false));
            Id65AuthoringEditorDocument document = Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
                Id65RemotePadMobyKind.GrassT107, Id65RemotePadGrassT107Catalog.CenterPositionId, [Id65AuthoringTerrainSetCatalog.Tile1Id]);
            Id65AuthoringEditorDocumentStoreResult stored = await Id65AuthoringEditorDocumentStore.SaveAsync(
                paths, manifest, document, new(null), expectedPriorEnvelopeCanonicalSha256: null);
            byte[] storedBefore = File.ReadAllBytes(Id65AuthoringEditorDocumentStore.GetPath(paths));
            // Reuse the already-proved publisher unchanged. The new feature may
            // inspect its immutable candidate but must never modify it.
            Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult baseline =
                await Id65AuthoringBlankCycloramaAggregateCandidateMaterializer.MaterializeAsync(new(paths, manifest, stored.EnvelopeCanonicalSha256));
            string baselineImageBefore = HashFile(baseline.Paths.CandidateImagePath);
            string baselineCueBefore = HashFile(baseline.Paths.CandidateCuePath);
            string baselineReceiptBefore = HashFile(baseline.Paths.CandidateReceiptPath);
            Id65AuthoringEditorNativeInputProvider.InputSet native = await Id65AuthoringEditorNativeInputProvider.LoadAsync(paths, manifest);
            UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan v3 =
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.BuildStaticPlan(paths.LockedBaseImagePath);
            Id65AuthoringBlankCycloramaAggregatePlanRequest request = new(stored, stored.EnvelopeCanonicalSha256, manifest, native, v3);
            int rejected = 0, tracks = 0;
            foreach (int trackId in new[] { 2, 26 })
            {
                Id65AuthoringMusicBuildInput input = Id65AuthoringMusicBuildInput.Create(document, stored.EnvelopeCanonicalSha256, trackId);
                string inputPath = Path.Combine(root, $"authored-build-input-track{trackId}.json");
                await File.WriteAllBytesAsync(inputPath, input.SerializeUtf8());
                Id65AuthoringMusicBuildInput reloaded = Id65AuthoringMusicBuildInput.Parse(await File.ReadAllBytesAsync(inputPath));
                Id65AuthoringMusicDiscPlan plan = Id65AuthoringMusicDiscPlan.Compile(reloaded, request);
                Id65AuthoringMusicDiscPlan repeat = Id65AuthoringMusicDiscPlan.Compile(Id65AuthoringMusicBuildInput.Parse(reloaded.SerializeUtf8()), request);
                Require(plan.DeterministicPlanSha256 == repeat.DeterministicPlanSha256 &&
                    plan.CopyOutputRow80().SequenceEqual(repeat.CopyOutputRow80()) && plan.CopyOutputExecutable().SequenceEqual(repeat.CopyOutputExecutable()),
                    "The pure combined plan changed across serialized reload and independent compilation.");
                Require(plan.BaselinePlan.DeterministicAggregatePlanSha256 == baseline.Plan.DeterministicAggregatePlanSha256 &&
                    plan.OutputRow80Sha256 == baseline.Receipt.OutputRow80Sha256,
                    "Adding music changed the unchanged schema-6 aggregate plan or WAD80 output.");
                Require(plan.Writes.Count(write => write.Address.Original.Space.Kind == NativeAddressKind.ExecutableFile) == 5 &&
                    plan.Writes.All(write => ReferenceEquals(write.Address.Final.Space, plan.DiscSpace)) &&
                    plan.Wad80DiscExtent.EndExclusive == plan.ExecutableDiscExtent.Offset,
                    "The plan did not map WAD80 and EXE into one disjoint, contiguous physical placement domain.");
                NativeRelocationMap.RequireDisjoint(plan.Writes.Select(write => write.Address.Final));
                Reject(() => Id65AuthoringMusicDiscPlan.Compile(input, request with { ExpectedEnvelopeCanonicalSha256 = new string('b', 64) }), ref rejected);

                string probe = Path.Combine(root, $"track{trackId}-unpublished-readback-probe.bin");
                File.Copy(baseline.Paths.CandidateImagePath, probe, overwrite: false);
                Reject(() => Verify(plan, paths.LockedBaseImagePath, probe), ref rejected);
                DiscLayout layout = DiscImage.DetectLayout(probe);
                using (FileStream image = new(probe, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    DiscFileRecord exe = DiscImage.FindRootFileRecord(image, layout, name => name == "SCUS_942.28");
                    Require(exe.Lba == Id65AuthoringMusicDiscPlan.ExecutableLba && exe.Size == Id65AuthoringMusicDiscPlan.ExecutableByteLength,
                        "The ISO directory executable extent differs from certified music placement.");
                    foreach (Id65AuthoringMusicDiscWrite write in plan.Writes.Where(write => write.Address.Original.Space.Kind == NativeAddressKind.ExecutableFile))
                    {
                        Require(write.Address.Final.Offset == (long)exe.Lba * 2048 + write.Address.Original.Offset,
                            "A typed executable window was mis-mapped into the disc.");
                        byte[] preimage = DiscImage.ReadFileBytes(image, layout, 0, write.Address.Final.Offset, checked((int)write.Address.Final.ByteLength));
                        Require(preimage.SequenceEqual(write.CopyBefore()), "The actual candidate executable preimage differs from the plan.");
                        DiscImage.WriteFileBytes(image, layout, 0, write.Address.Final.Offset, write.CopyAfter());
                    }
                    image.Flush(flushToDisk: true);
                    byte[] row80 = DiscImage.ReadFileBytes(image, layout, 0, plan.Wad80DiscExtent.Offset, checked((int)plan.Wad80DiscExtent.ByteLength));
                    byte[] executable = DiscImage.ReadFileBytes(image, layout, exe.Lba, 0, exe.Size);
                    plan.RequireLogicalReadback(row80, executable);
                    row80[0] ^= 1;
                    Reject(() => plan.RequireLogicalReadback(row80, executable), ref rejected);
                    row80[0] ^= 1; executable[^1] ^= 1;
                    Reject(() => plan.RequireLogicalReadback(row80, executable), ref rejected);
                }
                Id65AuthoringMusicPhysicalReadback readback = Verify(plan, paths.LockedBaseImagePath, probe);
                Require(readback.SourceImageSha256 == sourceBefore && readback.OutputImageSha256 == HashFile(probe) &&
                    readback.PlanSha256 == plan.DeterministicPlanSha256 && readback.VerifiedOwnedRawSectorCount == plan.AffectedRawSectors.Count,
                    "Whole-disc readback lost exact source/output/ownership identity.");
                using (FileStream sourceStream = File.OpenRead(paths.LockedBaseImagePath))
                using (FileStream probeStream = File.OpenRead(probe))
                using (ReportedGrowthStream growingOutput = new(probeStream))
                    Reject(() => Id65AuthoringMusicDiscReadback.Verify(plan, sourceStream, growingOutput), ref rejected);
                int executableSector = plan.AffectedRawSectors.First(sector => sector >= Id65AuthoringMusicDiscPlan.ExecutableLba);
                foreach (long offset in new[] { 0L, (long)executableSector * 2352 + 20, (long)executableSector * 2352 + 2072,
                             (long)Id65AuthoringMusicDiscPlan.ExecutableLba * 2352 + 24 + 0x100 })
                    WithFlippedByte(probe, offset, () => Reject(() => Verify(plan, paths.LockedBaseImagePath, probe), ref rejected));
                // Matching changes in both files evade a naive comparison but
                // must still fail the independently pinned whole-source hash.
                WithFlippedByte(paths.LockedBaseImagePath, 0, () => WithFlippedByte(probe, 0,
                    () => Reject(() => Verify(plan, paths.LockedBaseImagePath, probe), ref rejected)));
                using (FileStream sourceStream = File.OpenRead(paths.LockedBaseImagePath))
                using (FileStream probeStream = File.OpenRead(probe))
                {
                    CancellationTokenSource canceled = new(); canceled.Cancel();
                    try
                    {
                        Id65AuthoringMusicDiscReadback.Verify(plan, sourceStream, probeStream, canceled.Token);
                        throw new InvalidOperationException("Physical readback ignored cancellation.");
                    }
                    catch (OperationCanceledException) { rejected++; }
                }
                using (MemoryStream truncated = new(new byte[1]))
                using (FileStream sourceStream = File.OpenRead(paths.LockedBaseImagePath))
                    Reject(() => Id65AuthoringMusicDiscReadback.Verify(plan, sourceStream, truncated), ref rejected);
                Verify(plan, paths.LockedBaseImagePath, probe);
                Console.WriteLine($"Music raw readback: track={trackId} outputSha256={readback.OutputImageSha256} rawSectors={readback.VerifiedOwnedRawSectorCount} changedRawSectors={readback.ChangedRawSectorCount}");
                tracks++;
            }
            Require(HashFile(baseline.Paths.CandidateImagePath) == baselineImageBefore && HashFile(baseline.Paths.CandidateCuePath) == baselineCueBefore &&
                HashFile(baseline.Paths.CandidateReceiptPath) == baselineReceiptBefore &&
                File.ReadAllBytes(Id65AuthoringEditorDocumentStore.GetPath(paths)).SequenceEqual(storedBefore) &&
                HashFile(paths.LockedBaseImagePath) == sourceBefore && HashFile(source) == sourceBefore && HashFile(sourceCue) == cueBefore,
                "The music plan/readback tests changed an immutable baseline candidate, canonical document or source.");
            succeeded = true;
            Console.WriteLine($"ID65_AUTHORING_MUSIC_DISC_PLAN_SMOKE_OK sourceTracks={tracks} rejected={rejected} canonicalInputReload=1 unchangedSchema6Plan=1 exactExecutableInverse=1 typedDiscPlacement=1 wholePhysicalComplement=1 rawParity=1 baselineCandidatePreserved=1 newPublicationPath=0 runtimeAcceptance=0");
        }
        finally
        {
            if (succeeded) Directory.Delete(root, recursive: true);
            else Console.WriteLine("Retained failed music plan fixture for inspection: " + root);
        }
    }

    private static Id65AuthoringMusicPhysicalReadback Verify(Id65AuthoringMusicDiscPlan plan, string source, string output)
    {
        using FileStream sourceStream = File.OpenRead(source), outputStream = File.OpenRead(output);
        return Id65AuthoringMusicDiscReadback.Verify(plan, sourceStream, outputStream);
    }
    private static void WithFlippedByte(string path, long offset, Action check)
    {
        byte original;
        using (FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            stream.Position = offset; original = checked((byte)stream.ReadByte());
            stream.Position = offset; stream.WriteByte((byte)(original ^ 1)); stream.Flush(true);
        }
        try { check(); }
        finally
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            stream.Position = offset; stream.WriteByte(original); stream.Flush(true);
        }
    }
    private static void Reject(Action action, ref int rejected)
    {
        try { action(); }
        catch (InvalidDataException) { rejected++; return; }
        throw new InvalidOperationException("The composed music plan/readback accepted an invalid identity or byte mutation.");
    }
    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    // The body still reads exactly; only final Length changes. This models an
    // output gaining a tail during readback without changing any fixture file.
    private sealed class ReportedGrowthStream(Stream inner) : Stream
    {
        private bool _read;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => inner.Length + (_read ? 1 : 0);
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override int Read(byte[] buffer, int offset, int count)
        {
            _read = true;
            return inner.Read(buffer, offset, count);
        }
        public override int Read(Span<byte> buffer)
        {
            _read = true;
            return inner.Read(buffer);
        }
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

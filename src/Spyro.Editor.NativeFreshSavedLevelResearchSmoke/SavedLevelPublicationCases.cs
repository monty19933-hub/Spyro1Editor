using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

// Internal explicit reviewed CLI. Default tests only its pre-capture invalid-tiny
// input refusal; no default/source Candidate publication or native output.
internal static class SavedLevelPublicationCases
{
    internal const long ImageLength = 661547040;
    internal const string ExpectedImage = "2533ee961f13e43db03b08f221921cb60e35465e662e51bf221e6c096eb75b70";
    // Observed full-source family regression reference (source92), including
    // complete original inverse. This is not an independent image oracle.
    internal const string ExpectedFamilyImage = "408dfdfc577f991e635e4c0b636344333ca4b2654d89afabd88af88cc1a55c98";
    // Independent test-side reconstruction, frozen before tiled production.
    internal const string ExpectedTiledImage = "7ba0b29b68a1bee415901382c3a3b5345ccb01ca84872502131f68361afdc305";
    internal const string ExpectedTiledMaterialImage = "462c992f80f512f0b6be781b1a79e707e37a0e18f5e5e47356cd332ef61b2ba8";
    // Independent selected-source401/complete-disc403 references, not caller metadata.
    internal const string ExpectedSelectedOneImage = "14e5b3606f652c250df9cdd6c94de1d393186d1b74f1ab64decd5dd35130a77f";
    internal const string ExpectedSelectedThreeImage = "f1c137476833d1c046a78df812b40a47f8dc6558051c823437d7c5b3f86d8e32";
    internal const string OriginalCue = "05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766";
    internal static readonly string[] Modes = ["--publish-saved-level-reviewed", "--verify-saved-level-reviewed",
        "--partial-saved-level-reviewed", "--stale-saved-level-reviewed", "--unconfirmed-saved-level-reviewed",
        "--guard-release-saved-level-reviewed", "--artifact-saved-level-reviewed",
        "--artifact-notify-saved-level-reviewed", "--cancel-saved-level-reviewed",
        "--publish-mixed-saved-level-reviewed", "--verify-mixed-saved-level-reviewed",
        "--stale-mixed-saved-level-reviewed", "--publish-family-saved-level-reviewed",
        "--verify-family-saved-level-reviewed", "--stale-family-saved-level-reviewed",
        "--publish-tiled-saved-level-reviewed", "--verify-tiled-saved-level-reviewed", "--stale-tiled-saved-level-reviewed",
        "--publish-tiled-material-saved-level-reviewed", "--verify-tiled-material-saved-level-reviewed", "--stale-tiled-material-saved-level-reviewed",
        "--publish-selected-tiled-material-saved-level-reviewed", "--verify-selected-tiled-material-saved-level-reviewed", "--stale-selected-tiled-material-saved-level-reviewed"];
    internal sealed record Request(string Mode, string Bin, string Cue, string ProjectRoot, string ProjectId, string Output);
    internal static bool IsMode(string[] args) => args.Length > 0 && Modes.Contains(args[0], StringComparer.Ordinal);
    internal static bool IsMixedMode(string mode) => mode == Modes[9] || mode == Modes[10] || mode == Modes[11];
    internal static bool IsFamilyMode(string mode) => mode == Modes[12] || mode == Modes[13] || mode == Modes[14];
    internal static bool IsTiledMode(string mode) => mode == Modes[15] || mode == Modes[16] || mode == Modes[17];
    internal static bool IsTiledMaterialMode(string mode) => mode == Modes[18] || mode == Modes[19] || mode == Modes[20];
    internal static bool IsSelectedTiledMaterialMode(string mode) => mode == Modes[21] || mode == Modes[22] || mode == Modes[23];
    internal static string OperationMode(string mode) => mode == Modes[9] ? Modes[0] :
        mode == Modes[10] ? Modes[1] : mode == Modes[11] ? Modes[3] :
        mode == Modes[12] ? Modes[0] : mode == Modes[13] ? Modes[1] : mode == Modes[14] ? Modes[3] :
        mode == Modes[15] ? Modes[0] : mode == Modes[16] ? Modes[1] : mode == Modes[17] ? Modes[3] :
        mode == Modes[18] ? Modes[0] : mode == Modes[19] ? Modes[1] : mode == Modes[20] ? Modes[3] :
        mode == Modes[21] ? Modes[0] : mode == Modes[22] ? Modes[1] : mode == Modes[23] ? Modes[3] :
        Modes.Contains(mode, StringComparer.Ordinal) ? mode : throw new ArgumentException("Unknown reviewed saved-level mode.");
    internal static Request Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length != 6 || !IsMode(args) || args.Skip(1).Any(v =>
            string.IsNullOrWhiteSpace(v) || v.Contains('\0') || v.StartsWith("--", StringComparison.Ordinal)) ||
            !Path.IsPathFullyQualified(args[3]) || !Path.IsPathFullyQualified(args[5]) ||
            args[4].Length > 64 || EditorUserDataLayout.NormalizeProjectId(args[4]) != args[4])
            throw new ArgumentException("Exactly MODE BIN CUE ABSOLUTE_PROJECT_ROOT NORMALIZED_PROJECT_ID ABSOLUTE_OUTPUT are required.");
        return new(args[0], args[1], args[2], args[3], args[4], args[5]);
    }

    internal static async Task RunAsync(string[] args)
    {
        var request = Parse(args);
        string operationMode = OperationMode(request.Mode);
        bool mixed = IsMixedMode(request.Mode), family = IsFamilyMode(request.Mode), tiled = IsTiledMode(request.Mode),
            tiledMaterial = IsTiledMaterialMode(request.Mode), selectedMaterial = IsSelectedTiledMaterialMode(request.Mode), verify = operationMode == Modes[1];
        string bin = Path.GetFullPath(request.Bin), cue = Path.GetFullPath(request.Cue);
        string projectRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.ProjectRoot));
        string output = RequireLocation(request.Output, bin, cue, projectRoot, verify);
        var project = new EditorProjectLayout(projectRoot, request.ProjectId);
        var owners = new NativeFreshResearchSourceOwners(bin, cue);
        SavedFiles? savedFiles = null; NativeFreshSavedLevelDocumentReadContext? saved = null;
        NativeFreshSavedLevelResearchCandidatePlan? candidate = null;
        bool returned = false; Exception? primary = null; SavedLevelFaultCases.Outcome? exercise = null;
        NativeId65FreshDiscResearchPublication? result = null; string[]? artifacts = null;
        try
        {
            owners.Open(CancellationToken.None);
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(owners.Bin!, CancellationToken.None);
            Check(owners.CueSnapshot is { Length: 88 } && Hash(owners.CueSnapshot) == OriginalCue,
                "This explicit gate requires the exact original CUE, not merely another valid sheet.");
            savedFiles = SavedFiles.Capture(project); // All small acquired streams close internally, even on error.
            saved = NativeFreshSavedLevelDocumentReadContext.Capture(NativeFreshLevelDocumentStore.Capture(project));
            savedFiles.RequireCurrent();
            // These gates preserve the complete authored fixture and its IDs
            // before invoking the same public saved Candidate factory.
            if (family || tiled || tiledMaterial || selectedMaterial) RequireFixtureDocument(request.Mode, NativeFreshLevelDocumentCodec.SerializeCanonical(saved.Document));
            candidate = mixed || family || tiled || tiledMaterial || selectedMaterial
                ? await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, saved.Snapshot)
                : await NativeFreshSavedLevelResearchCandidatePlan.CaptureAsync(bin, cue, saved);
            if (selectedMaterial)
            {
                var expected = ExpectedRecipe(candidate);
                Check(candidate.TiledCompilation?.TiledMaterialAncestor != null && ReferenceEquals(candidate.SavedSnapshot, saved.Snapshot) &&
                    candidate.ExpectedImageByteLength == ImageLength && candidate.ExpectedChangedRawSectors == expected.Sectors &&
                    candidate.ExpectedImageSha256 == expected.Image,
                    "Exact independent N1/N3 canonical fixture and actual selected material ancestry select the physical recipe; no caller hash or runtime claim.");
            }
            else if (tiledMaterial)
                Check(candidate.TiledCompilation?.TiledMaterialAncestor != null && ReferenceEquals(candidate.SavedSnapshot, saved.Snapshot) &&
                    candidate.ExpectedImageByteLength == ImageLength && candidate.ExpectedChangedRawSectors == 1253 &&
                    candidate.ExpectedImageSha256 == ExpectedTiledMaterialImage,
                    "Exact independently reconstructed tiled-material saved physical recipe; no runtime claim.");
            else if (tiled)
                Check(candidate.TiledCompilation != null && ReferenceEquals(candidate.SavedSnapshot, saved.Snapshot) &&
                    candidate.ExpectedImageByteLength == ImageLength && candidate.ExpectedChangedRawSectors == 1253 &&
                    candidate.ExpectedImageSha256 == ExpectedTiledImage,
                    "Exact independently reconstructed tiled saved physical recipe; no runtime claim.");
            else if (family)
                Check(candidate.FamilyCompilation != null && ReferenceEquals(candidate.SavedSnapshot, saved.Snapshot) &&
                    candidate.ExpectedImageByteLength == ImageLength && candidate.ExpectedChangedRawSectors == 1292 &&
                    candidate.ExpectedImageSha256 == ExpectedFamilyImage,
                    "Exact source-measured saved family physical recipe; no independent-oracle or runtime claim.");
            else if (mixed)
                Check(candidate.MixedDisc != null && ReferenceEquals(candidate.SavedSnapshot, saved.Snapshot) &&
                    candidate.ExpectedImageByteLength == ImageLength && candidate.ExpectedChangedRawSectors == 1290 &&
                    candidate.ExpectedImageSha256 == MixedSavedCases.Expected, "Exact independently derived mixed saved physical recipe.");
            else
                Check(candidate.ExpectedImageByteLength == ImageLength && candidate.ExpectedChangedRawSectors == 1253 &&
                    candidate.ExpectedImageSha256 == ExpectedImage, "Exact independently derived first physical recipe.");
            // The actual neutral document comparison prevents an accidental equal-image alternate recipe.
            RequireFixtureDocument(request.Mode, candidate.CopyCanonicalDocument());
            if (verify)
            {
                string[] before = ReadComplete(candidate, output);
                result = await NativeFreshSavedLevelResearchCandidatePublisher.VerifyAsync(candidate, output);
                RequireResult(candidate, output, result);
                artifacts = ReadComplete(candidate, output);
                Check(before.SequenceEqual(artifacts), "New-process Verify leaves every artifact byte unchanged.");
            }
            else if (operationMode == Modes[0])
            {
                result = await NativeFreshSavedLevelResearchCandidatePublisher.PublishAsync(candidate, output);
                returned = true; // Only an actual Publish return, before any smoke assertions.
                RequireResult(candidate, output, result);
                artifacts = ReadComplete(candidate, output);
            }
            else exercise = await SavedLevelFaultCases.Run(candidate, output, operationMode, savedFiles, () => returned = true);
        }
        catch (Exception error) { primary = error; }

        List<Exception> failures = [];
        // Complete already retains the primary once and attempts every acquired
        // held/current original owner and close. Do not append primary again.
        try { owners.Complete(primary); } catch (Exception error) { failures.Add(error); }
        if (savedFiles != null)
        {
            try { savedFiles.RequireCurrent(exercise?.ExpectedRawAfter); } catch (Exception error) { failures.Add(error); }
            if (exercise?.ExpectedRawAfter == null && saved != null)
                try { NativeFreshDocumentStorage.RequireAggregateCurrent(saved.Snapshot, CancellationToken.None); }
                catch (Exception error) { failures.Add(error); }
        }
        if (failures.Count != 0)
        {
            if (exercise?.Failure != null) failures.Insert(0, exercise.Failure);
            var combined = Fold(failures);
            Console.Error.WriteLine("SAVED_LEVEL_REVIEWED_FAILED " + JsonSerializer.Serialize(new
            {
                request.Mode, output, outcome = Classify(combined, returned, verify),
                success = false, outputsRetained = true, automaticRetry = false,
                errors = Errors(combined).Select(e => new { type = e.GetType().FullName, e.Message })
            }));
            ExceptionDispatchInfo.Capture(combined).Throw();
        }
        Console.WriteLine("SAVED_LEVEL_REVIEWED_OK " + JsonSerializer.Serialize(new
        {
            request.Mode, output, candidate!.PlanSha256, candidate.ExpectedImageSha256,
            savedRawSha256 = saved!.Snapshot.ContentSha256, saved.Snapshot.ManifestSha256,
            canonicalSha256 = Hash(candidate.CopyCanonicalDocument()), legacyPresent = savedFiles!.Legacy != null,
            originalFinal = true, originalHeldAndNamedFinal = true, acquiredOriginalClosesAttempted = true,
            deliberateSavedRawChange = exercise?.ExpectedRawAfter != null,
            savedFinalRawSha256 = Hash(exercise?.ExpectedRawAfter ?? savedFiles.Raw),
            result, artifactSha256 = artifacts,
            exercise = exercise == null ? null : new { exercise.Kind, exercise.AfterReturnCount,
                exercise.FinalArtifactCount, exercise.Evidence, errors = exercise.Failure == null ? [] :
                    Errors(exercise.Failure).Select(e => new { type = e.GetType().FullName, e.Message }).ToArray() },
            runtimeVerified = false, normalExport = false, nativePersistentSaveOwnerProven = false
        })); // Only after every independent final observation/close.
    }

    internal static void RequireFixtureDocument(string mode, ReadOnlySpan<byte> canonical)
    {
        _ = OperationMode(mode);
        if (IsSelectedTiledMaterialMode(mode)) { _ = RequireSelectedFixtureDocument(canonical); return; }
        var document = IsTiledMaterialMode(mode) ? SavedTiledMaterialDocumentFixture.Document() :
            IsTiledMode(mode) ? SavedTiledDocumentFixture.Document() : IsFamilyMode(mode) ? FamilySavedCases.Document() :
            IsMixedMode(mode) ? MixedSavedCases.Document() : Cases.Document(14, 1, 70, 1, 128, 20);
        Check(canonical.SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(document)),
            "Exact actual canonical recipe; no caller hash authority.");
    }

    internal static (string Image, int SourceCount) RequireSelectedFixtureDocument(ReadOnlySpan<byte> canonical)
    {
        if (canonical.SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(
            SelectedTiledMaterialSavedCases.Document([0, 0, 0, 0, 0, 0])))) return (ExpectedSelectedOneImage, 1);
        if (canonical.SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(
            SelectedTiledMaterialSavedCases.Document([0, 1, 2, 0, 1, 2])))) return (ExpectedSelectedThreeImage, 3);
        throw new InvalidDataException("Selected physical gate requires one complete independently pinned N1 or N3 canonical fixture, including every tile ID, descriptor and face.");
    }

    internal static string RequireLocation(string value, string bin, string cue, string project, bool verify)
    {
        Check(Path.IsPathFullyQualified(value) && Path.IsPathFullyQualified(project), "Explicit absolute roots.");
        string output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        Unlinked(output); Unlinked(project);
        Check(Directory.Exists(project) && !File.Exists(project), "Existing actual project required; never created here.");
        Check(Directory.GetParent(output) is { Exists: true }, "Existing output parent required.");
        foreach (string original in new[] { bin, cue, project })
            PersistencePathSafety.EnsureTreesDoNotOverlap(original, output, "Saved-level output overlaps input owner.");
        foreach (string original in new[] { bin, cue })
            PersistencePathSafety.EnsureTreesDoNotOverlap(original, project, "Mutable test project overlaps original owner.");
        if (verify) RequireNames(output); else Absent(output);
        return output;
    }
    internal static void Unlinked(string path)
    {
        for (string? p = path; p != null; p = Directory.GetParent(p)?.FullName)
            Check(new FileInfo(p).LinkTarget == null && new DirectoryInfo(p).LinkTarget == null, "Unlinked ancestors required.");
    }
    internal static void Absent(string path) => Check(!File.Exists(path) && !Directory.Exists(path) &&
        new FileInfo(path).LinkTarget == null && new DirectoryInfo(path).LinkTarget == null, "Absent owner required: " + path);
    internal static void RequireNames(string root)
    {
        Unlinked(root); Check(Directory.Exists(root), "Existing verification directory.");
        Check(Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName).Order(StringComparer.Ordinal)
            .SequenceEqual(NativeFreshSavedLevelResearchCandidatePlan.OutputNames.Order(StringComparer.Ordinal)), "Exact three artifacts only.");
    }
    internal static string[] ReadComplete(NativeFreshSavedLevelResearchCandidatePlan plan, string root)
    {
        RequireNames(root);
        var readback = plan.VerifyImage(Path.Combine(root, NativeFreshSavedLevelResearchCandidatePlan.ImageName), CancellationToken.None);
        var expected = ExpectedRecipe(plan);
        Check(readback.OutputSha256 == expected.Image && readback.ChangedSectors == expected.Sectors,
            plan.FamilyCompilation == null ? "Independent full raw image readback." :
            "Source-measured family regression reference plus full raw image readback; not an independent oracle.");
        byte[] receipt = plan.BuildReceipt(readback), cue = plan.CopyCueBytes();
        Check(ReadSmall(Path.Combine(root, NativeFreshSavedLevelResearchCandidatePlan.ReceiptName), 65536).SequenceEqual(receipt), "Exact actual receipt.");
        Check(ReadSmall(Path.Combine(root, NativeFreshSavedLevelResearchCandidatePlan.CueName), 65536).SequenceEqual(cue), "Exact actual CUE.");
        RequireNames(root); return [readback.OutputSha256, Hash(receipt), Hash(cue)];
    }
    internal static void RequireResult(NativeFreshSavedLevelResearchCandidatePlan plan, string root, NativeId65FreshDiscResearchPublication result)
    {
        var expected = ExpectedRecipe(plan);
        Check(result.PlanSha256 == plan.PlanSha256 &&
            result.ImageSha256 == expected.Image && result.ChangedRawSectors == expected.Sectors &&
            result.WholeImageReadback && result.RetainedRecoveryDirectory.Length == 0 && result.OutputDirectory == root &&
            result.ResearchOnly && !result.RuntimeVerified && !result.NormalCreateBinApproved && !result.CompleteNativeDependencyClosureProven &&
            result.ImagePath == Path.Combine(root, NativeFreshSavedLevelResearchCandidatePlan.ImageName) &&
            result.ReceiptPath == Path.Combine(root, NativeFreshSavedLevelResearchCandidatePlan.ReceiptName) &&
            result.CuePath == Path.Combine(root, NativeFreshSavedLevelResearchCandidatePlan.CueName), "Only actual clean readback result.");
    }
    private static (string Image, int Sectors) ExpectedRecipe(NativeFreshSavedLevelResearchCandidatePlan plan)
    {
        if (plan.TiledCompilation is { } tiled)
        {
            plan.RequireTiledAncestry(plan.Saved, tiled);
            var material = tiled.TiledMaterialAncestor;
            bool selected = tiled.DiscPlan.BodyAncestor.ResourceProfile == NativeId65NormalResourceProfile.DirectLqSelectedStaticMaterialsFlatTiles;
            RequireFixtureDocument(material == null ? Modes[15] : selected ? Modes[21] : Modes[18], plan.CopyCanonicalDocument());
            Check(plan.MixedDisc == null && plan.FamilyCompilation == null &&
                ReferenceEquals(tiled.Document, plan.Saved.Document) &&
                ReferenceEquals(tiled.DiscPlan.BodyAncestor.TiledSceneAncestor, tiled.FreshResourceAncestor),
                "Tiled physical expectation requires the actual complete saved document and issued scene/body/disc chain.");
            if (material != null)
            {
                Check(ReferenceEquals(material.Document, plan.Saved.Document) && ReferenceEquals(material.FreshAncestor, tiled.FreshResourceAncestor) &&
                    ReferenceEquals(tiled.DiscPlan.BodyAncestor.TiledMaterialAncestor, material) &&
                    tiled.DiscPlan.BodyAncestor.ResourceProfile == (selected ? NativeId65NormalResourceProfile.DirectLqSelectedStaticMaterialsFlatTiles :
                        NativeId65NormalResourceProfile.DirectLqTwoStaticMaterialsFlatTiles),
                    "Material physical expectation uses the actual source-issued final material revision, not unresolved Base-only bytes.");
                if (selected)
                {
                    var expected = RequireSelectedFixtureDocument(plan.CopyCanonicalDocument());
                    var descriptors = tiled.Document.TiledContent!.Tiles.SelectMany(tile => new[] { tile.Materials.Triangle0, tile.Materials.Triangle1 })
                        .OfType<NativeFreshRetailTerrainMaterialIntent>().Select(s => (s.SourceWadEntry, s.TextureId)).Distinct().ToArray();
                    material.RequireSelectedAncestry(tiled.FreshResourceAncestor, plan.Saved.Document, material.MaterialSources);
                    Check(material.MaterialSources.Count == expected.SourceCount && material.Allocation.Records.Count == expected.SourceCount &&
                        material.SourceHandles.Count == expected.SourceCount &&
                        material.MaterialSources.Select(s => (s.SourceWadEntry, s.SourceTextureId)).SequenceEqual(descriptors) &&
                        material.OrderedFaceMaterials.Count == 6,
                        "Selected physical expectation binds the actual ordered complete source list and all six faces, not merely a count or equal image hash.");
                    return (expected.Image, 1253);
                }
                return (ExpectedTiledMaterialImage, 1253);
            }
            return (ExpectedTiledImage, 1253);
        }
        if (plan.FamilyCompilation is { } family)
        {
            plan.RequireFamilyAncestry(plan.Saved, family);
            RequireFixtureDocument(Modes[12], plan.CopyCanonicalDocument());
            Check(ReferenceEquals(plan.MixedDisc, family.Disc) &&
                ReferenceEquals(family.Disc.FamilyCompositionAncestor, family.Composition),
                "Family physical expectation requires the actual saved composition and Disc chain.");
            return (ExpectedFamilyImage, 1292);
        }
        return plan.MixedDisc == null ? (ExpectedImage, 1253) : (MixedSavedCases.Expected, 1290);
    }
    internal static byte[] ReadSmall(string path, int max)
    {
        Unlinked(path); Check(File.Exists(path) && !Directory.Exists(path), "Existing bounded regular owner.");
        FileStream? stream = null; byte[]? bytes = null; List<Exception> failures = [];
        try { stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read); bytes = NativeFreshDocumentStorage.ReadBounded(stream, max); }
        catch (Exception error) { failures.Add(error); }
        try { stream?.Dispose(); } catch (Exception error) { failures.Add(error); }
        if (failures.Count != 0) ExceptionDispatchInfo.Capture(Fold(failures)).Throw();
        return bytes!;
    }
    internal static void WriteNewSmall(string path, byte[] bytes)
    {
        Check(bytes.Length is > 0 and <= 65536, "Bounded newly owned evidence only."); Absent(path); Unlinked(path);
        FileStream? output = null; List<Exception> errors = [];
        try { output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); output.Write(bytes); output.Flush(true); }
        catch (Exception error) { errors.Add(error); }
        try { output?.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count != 0) ExceptionDispatchInfo.Capture(Fold(errors)).Throw();
    }
    internal static IEnumerable<Exception> Errors(Exception error)
    {
        yield return error;
        if (error is AggregateException group)
            foreach (var item in group.InnerExceptions) foreach (var leaf in Errors(item)) yield return leaf;
        else if (error.InnerException != null) foreach (var leaf in Errors(error.InnerException)) yield return leaf;
    }
    internal static string Classify(Exception error, bool returned, bool verify) =>
        Errors(error).Any(e => e is NativeId65FreshResearchPublicationUnconfirmedException) ? "unconfirmed" :
        returned || Errors(error).Any(e => e is NativeFreshSavedLevelResearchCommittedException) ? "committed-with-failure" :
        verify ? "verification-failed" : "not-confirmed";
    internal static Exception Fold(IReadOnlyList<Exception> errors) => errors.Count == 1 ? errors[0] :
        new IOException("Saved-level reviewed primary/final/close failures; retain all evidence.", new AggregateException(errors));
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }

    internal sealed record SavedFiles(EditorProjectLayout Project, byte[] Manifest, byte[] Raw, byte[]? Legacy)
    {
        internal static SavedFiles Capture(EditorProjectLayout project) => new(project,
            ReadSmall(project.ManifestPath, 65536), ReadSmall(Path.Combine(project.RootPath, NativeFreshLevelDocumentStore.FileName), 65536),
            ReadLegacy(project));
        private static byte[]? ReadLegacy(EditorProjectLayout project)
        {
            string path = Path.Combine(project.RootPath, NativeId65FreshDocumentStore.FileName);
            Unlinked(path); Check(!Directory.Exists(path), "Legacy path cannot be directory.");
            return File.Exists(path) ? ReadSmall(path, 16384) : null;
        }
        internal void RequireCurrent(byte[]? expectedRaw = null)
        {
            List<Exception> errors = [];
            CheckOne(() => Check(ReadSmall(Project.ManifestPath, 65536).SequenceEqual(Manifest), "Manifest exact raw final."));
            CheckOne(() => Check(ReadSmall(Path.Combine(Project.RootPath, NativeFreshLevelDocumentStore.FileName), 65536)
                .SequenceEqual(expectedRaw ?? Raw), "Aggregate exact expected raw final."));
            CheckOne(() => { byte[]? now = ReadLegacy(Project); Check(now == null ? Legacy == null : Legacy != null && now.SequenceEqual(Legacy), "Legacy presence/raw preserved."); });
            if (errors.Count != 0) ExceptionDispatchInfo.Capture(Fold(errors)).Throw();
            void CheckOne(Action action) { try { action(); } catch (Exception error) { errors.Add(error); } }
        }
    }
}

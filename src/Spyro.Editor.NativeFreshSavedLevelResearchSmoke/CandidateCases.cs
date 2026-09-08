// Source-free/default and explicitly opted source cases. Cases.Save/Document/Hash/
// Throws/Leaves are linked unchanged from the reviewed saved-material smoke.
// New isolated smoke sets its own fresh Cases.Root; default never opens ROM/CUE.
using System.Reflection;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static Cases;

internal static class CandidateCases
{
    internal static async Task Free()
    {
        Type type = typeof(NativeFreshSavedLevelResearchCandidatePlan);
        Check(type.IsSealed && !type.IsPublic && type.GetConstructors().Length == 0,
            "One closed internal Candidate with no public constructors.");
        Check(type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 0,
            "No caller native/hash/serializer authority API.");
        Check(type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .All(p => p.SetMethod == null), "Candidate state is immutable.");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Task<NativeFreshSavedLevelResearchCandidatePlan> task;
        try { task = NativeFreshSavedLevelResearchCandidatePlan.CaptureAsync("\0", "\0", null!, canceled.Token); }
        catch { throw new InvalidDataException("Capture validation must fault its Task, not throw synchronously."); }
        var cancel = await Throws<OperationCanceledException>(() => task);
        Check(cancel.CancellationToken == canceled.Token, "Capture token before null; true NUL remains unopened.");
        await Throws<ArgumentNullException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureAsync("\0", "\0", null!));
        // Publisher/Verify intentionally bind null before token; no generic caller policy.
        await Throws<ArgumentNullException>(() => NativeFreshSavedLevelResearchCandidatePublisher.PublishAsync(null!, "\0", canceled.Token));
        await Throws<ArgumentNullException>(() => NativeFreshSavedLevelResearchCandidatePublisher.VerifyAsync(null!, "\0", canceled.Token));

        foreach (var (label, document) in new[]
        {
            ("schema1", NativeFreshLevelDocumentIntent.Create(Base(), [])),
            ("color", Document().WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(
                NativeFreshFloorMaterialIntent.BaseFloorColor(), Retail(70, 1)))),
            ("wizard", Document().WithContent(Base(), [NativeFreshWizardRouteIntent.Create(
                "11111111111111111111111111111111", 8704, 8960, 8909, 9216, 8960)]))
        })
        {
            var (_, saved) = Save(label, document);
            var error = await Throws<InvalidDataException>(() =>
                NativeFreshSavedLevelResearchCandidatePlan.CaptureAsync("\0", "\0", saved));
            Check(!Leaves(error).Any(e => e is FileNotFoundException or DirectoryNotFoundException),
                "Unsupported first-arm input refuses before source, never falls back to an old role.");
        }
        foreach (string mode in new[] { "raw", "manifest" })
        {
            var (project, saved) = Save("stale-" + mode);
            File.AppendAllText(mode == "raw" ? saved.Snapshot.Path : project.ManifestPath, " ");
            var error = await Throws<InvalidDataException>(() =>
                NativeFreshSavedLevelResearchCandidatePlan.CaptureAsync("\0", "\0", saved));
            Check(!Leaves(error).Any(e => e is ArgumentException or FileNotFoundException),
                "Actual raw/manifest guard precedes path normalization/open.");
        }
        var (_, valid) = Save("empty-path");
        await Throws<ArgumentException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureAsync(" ", "\0", valid));
        Check(!NativeFreshSavedLevelResearchCandidatePlan.OutputNames.SequenceEqual(
            NativeId65FreshTexturedResearchCandidatePlan.OutputNames), "New role cannot silently publish old fixed-a names.");
        Check(NativeFreshSavedLevelResearchCandidatePlan.OutputNames.SequenceEqual(new[] {
            "native-fresh-saved-level-r01.bin", "native-fresh-saved-level-r01.receipt.json", "native-fresh-saved-level-r01.cue" }),
            "Exact single aggregate basename and CUE-last order.");
    }

    // Explicit source gate only. Caller holds full original BIN+CUE before and
    // independently checks/fully closes both named and held owners in its finally.
    internal static async Task Source(string bin, string cue)
    {
        var cases = new[]
        {
            ("fixed-a", Document(14, 0, 70, 0), "998a47930c5f20e36cf87d41bfb9ec3cab237d8d42c443ed706bd50c692611a3"),
            ("selected", Document(), "9e42afbc4144783b533df3dcbfd428da31e70e4d23269d8b0b52987d7d700bb6"),
            ("reversed", Document(70, 1, 14, 1), "99d06e2905deee01e9166f506d40b735abaf81a2ce19bec017b1699903f96e7a"),
            ("same", Document(14, 1, 14, 1), "2999405b7721292776d2bc76500986433e7c3d0d08c137bc28001f30df099281"),
            ("heading-music", Document(14, 1, 70, 1, 128, 20), "2533ee961f13e43db03b08f221921cb60e35465e662e51bf221e6c096eb75b70")
        };
        foreach (var (label, document, expected) in cases)
        {
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            var (_, saved) = Save(label, document);
            var candidate = await NativeFreshSavedLevelResearchCandidatePlan.CaptureAsync(bin, cue, saved);
            Console.WriteLine($"SAVED_LEVEL_CASE_CAPTURE case={label} elapsedMs={elapsed.ElapsedMilliseconds} image={candidate.ExpectedImageSha256} prospectiveOnly=true");
            candidate.RequireAncestry(saved, candidate.MaterialCompilation);
            Check(ReferenceEquals(candidate.MaterialCompilation.Saved, saved) &&
                ReferenceEquals(candidate.MaterialCompilation.Document, saved.Document), "Actual Snapshot object, not equal parsed adoption.");
            Check(candidate.ExpectedImageSha256 == expected && candidate.ExpectedChangedRawSectors == 1253,
                "Existing independently source-derived golden; not a production fixture allowlist.");
            var measured = new NativeId65FreshDiscVerification(expected, 1253);
            byte[] receipt = candidate.BuildReceipt(measured), canonical = candidate.CopyCanonicalDocument();
            Check(receipt.Length is > 0 and <= 65536, "Complete bounded companion before any Engine use.");
            using var parsed = JsonDocument.Parse(receipt);
            var root = parsed.RootElement;
            Check(root.GetProperty("savedRevisionBound").GetBoolean() &&
                !root.TryGetProperty("publicationGuardPassed", out _) &&
                !root.GetProperty("runtimeVerified").GetBoolean() &&
                !root.GetProperty("nativePersistentSaveOwnerProven").GetBoolean(), "Prospective receipt has no future guard/native claim.");
            var binding = root.GetProperty("saved");
            Check(binding.GetProperty("rawSha256").GetString() == saved.Snapshot.ContentSha256 &&
                binding.GetProperty("manifestSha256").GetString() == saved.Snapshot.ManifestSha256 &&
                binding.GetProperty("canonicalSha256").GetString() == Hash(canonical), "Raw and canonical are distinct actual observations.");
            receipt[0] ^= 1; canonical[0] ^= 1;
            Check(candidate.BuildReceipt(measured)[0] != receipt[0] && candidate.CopyCanonicalDocument()[0] != canonical[0],
                "Defensive copies cannot change the bound receipt/document.");
            await Throws<InvalidDataException>(() => { candidate.BuildReceipt(new(expected, 1252)); return Task.CompletedTask; });
            var equal = await NativeFreshSavedLevelResearchCandidatePlan.CaptureAsync(bin, cue, saved);
            Check(equal.ExpectedImageSha256 == candidate.ExpectedImageSha256, "Independent valid reissue remains permitted.");
            await Throws<InvalidDataException>(() => { candidate.RequireAncestry(saved, equal.MaterialCompilation); return Task.CompletedTask; });
            // True raw-only change, not a forged Snapshot: old Candidate now stale;
            // recapture actual Snapshot/ReadContext and recompile independently.
            File.AppendAllText(saved.Snapshot.Path, " \n");
            await Throws<InvalidDataException>(() => candidate.RequireFreshAsync());
            var project = new Spyro.Editor.Core.Persistence.EditorProjectLayout(saved.Snapshot.RootPath, saved.Snapshot.ProjectId);
            var rawReissue = NativeFreshSavedLevelDocumentReadContext.Capture(NativeFreshLevelDocumentStore.Capture(project));
            var newer = await NativeFreshSavedLevelResearchCandidatePlan.CaptureAsync(bin, cue, rawReissue);
            Check(newer.ExpectedImageSha256 == expected && newer.PlanSha256 != candidate.PlanSha256 &&
                newer.CopyCanonicalDocument().SequenceEqual(candidate.CopyCanonicalDocument()), "Same canonical/native bytes do not erase raw-version binding.");
            Console.WriteLine($"SAVED_LEVEL_CASE_SOURCE_OK case={label} elapsedMs={elapsed.ElapsedMilliseconds} checks={Checks} nativeImageWritten=false");
        }
    }
}

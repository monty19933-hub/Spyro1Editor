using System.Reflection;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;
using static Cases;

internal static class SourceCases
{
    internal static async Task Run(string path)
    {
        List<Exception> errors = []; FileStream? original = null;
        try { original = File.OpenRead(path); RequireOriginal(original); await Core(path); }
        catch (Exception error) { errors.Add(error); }
        finally
        {
            if (original != null)
            {
                try { RequireOriginal(original); } catch (Exception error) { errors.Add(error); }
            }
            FileStream? current = null;
            try { current = File.OpenRead(path); RequireOriginal(current); }
            catch (Exception error) { errors.Add(error); }
            finally
            {
                if (current != null)
                    try { current.Dispose(); } catch (Exception error) { errors.Add(error); }
            }
            if (original != null)
                try { original.Dispose(); } catch (Exception error) { errors.Add(error); }
        }
        ThrowFailures(errors);
    }

    private static async Task Core(string path)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "Spyro.Editor.NativeFreshSavedMaterialSmoke.source_saved_material_golden.json")!;
        using var reference = JsonDocument.Parse(stream);
        JsonElement root = reference.RootElement;
        Check(root.GetProperty("all_final_owners_verified").GetBoolean() &&
            !root.GetProperty("physical_output_written").GetBoolean(), "Independent source oracle, not a C# producer or physical candidate.");
        foreach (var row in root.GetProperty("inherited").EnumerateArray().Concat(new[] { root.GetProperty("newly_reconstructed") }))
        {
            string name = row.GetProperty("name").GetString()!;
            int[][] selection = row.GetProperty("selections").EnumerateArray()
                .Select(s => s.EnumerateArray().Select(n => n.GetInt32()).ToArray()).ToArray();
            var (project, saved) = Save(name, Document(selection[0][0], selection[0][1], selection[1][0], selection[1][1],
                row.GetProperty("heading").GetInt32(), row.GetProperty("music").GetInt32()));
            byte[] savedBytes = File.ReadAllBytes(saved.Snapshot.Path), manifest = File.ReadAllBytes(project.ManifestPath);
            var bridge = await NativeFreshSavedMaterialCompilation.CaptureAsync(path, saved);
            Check(bridge.SourceImagePath == Path.GetFullPath(path), "Original path remains bound, not supplied later by a catalog label.");
            Check(ReferenceEquals(bridge.Saved, saved) && ReferenceEquals(bridge.Document, saved.Document) &&
                ReferenceEquals(bridge.BaseCompilation.Intent, saved.Document.Base) &&
                ReferenceEquals(bridge.Texture.FreshAncestor, bridge.BaseCompilation.FreshResourceAncestor),
                "Exact actual saved document/Base/fresh resource ancestry, not Session draft or equal parsed intent.");
            Check(ReferenceEquals(bridge.Texture.MaterialSources[0], bridge.Material0) &&
                ReferenceEquals(bridge.Texture.MaterialSources[1], bridge.Material1) &&
                ReferenceEquals(bridge.Body.TextureAncestor, bridge.Texture) && ReferenceEquals(bridge.Disc.BodyAncestor, bridge.Body),
                "Selected actual source/Texture/Body/Disc reference chain remains ordered.");
            bridge.RequireAncestry(saved, bridge.BaseCompilation, bridge.Material0, bridge.Material1,
                bridge.Texture, bridge.Staging, bridge.Body, bridge.Disc);
            Check(bridge.Material0.SourceWadEntry == selection[0][0] && bridge.Material0.SourceTextureId == selection[0][1] &&
                bridge.Material1.SourceWadEntry == selection[1][0] && bridge.Material1.SourceTextureId == selection[1][1],
                "Exact saved slot descriptors selected the actual source records.");
            Check(ReferenceEquals(bridge.Material0, bridge.Material1) == (name == "same-source"),
                "Only the exact repeated descriptor reuses its source handle; material allocations stay separate.");
            Check(bridge.Texture.BindingProfileId == NativeId65FreshTerrainTexturePlan.SelectedBindingProfileId &&
                bridge.Staging.UpstreamPlanSha256 == bridge.Texture.PlanSha256 &&
                bridge.Body.StagingPlanSha256 == bridge.Staging.PlanSha256, "Selected factory/staging profile is retained, not fixed-a spoofing.");
            Match(bridge.Texture.UploadSha256, "upload_sha256"); Match(bridge.Texture.DataSha256, "data_sha256");
            Match(Hash(bridge.Body.CopyLevelData()), "body_sha256"); Match(Hash(bridge.Disc.CopyIsoRootDirectory()), "root_sha256");
            Match(Hash(bridge.Disc.CopyWadDirectory()), "wad_sha256");
            Check(bridge.Texture.Allocation.Records.Select(r => r.RecordSha256).SequenceEqual(
                row.GetProperty("record_sha256").EnumerateArray().Select(v => v.GetString())), "Every relocated record matches independent native reconstruction.");
            if (name == "heading-music")
            { Match(Hash(bridge.Texture.CopyScene()), "scene_sha256"); Match(Hash(bridge.Disc.CopyExecutable()), "executable_sha256"); }
            using (var source = File.OpenRead(path))
            {
                var measured = NativeId65FreshDiscStreaming.Measure(bridge.Disc, source, CancellationToken.None);
                Match(measured.OutputSha256, "output_sha256");
                Check(measured.ChangedSectors == row.GetProperty("changed_sectors").GetInt32(), "Full measured physical output matches frozen oracle count without writing it.");
            }
            Check(bridge.Disc.FinalExecutableLba == row.GetProperty("final_executable_lba").GetInt32() &&
                bridge.Disc.Executable.SelectedBuiltInTrack == saved.Document.Base.MusicTrackId &&
                bridge.Texture.FreshAncestor.SceneLayout.Spawn.Heading == saved.Document.Base.Heading,
                "Actual authored heading/music survive compilation; no A preset substitution.");
            byte[] canonical = bridge.CopyCanonicalDocument();
            Check(canonical.SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(saved.Document)), "Whole schema2 canonical content retained.");
            canonical[0] ^= 1;
            Check(!canonical.SequenceEqual(bridge.CopyCanonicalDocument()), "Returned canonical copy cannot mutate issued lineage.");
            Check(!bridge.RuntimeVerified && !bridge.PublicationApproved && !bridge.Disc.PublicationApproved &&
                !bridge.Texture.HqRuntimeProven && bridge.Document.Placements.Count == 0, "No runtime/HQ/publication or omitted Wizard claim.");
            Check(File.ReadAllBytes(saved.Snapshot.Path).SequenceEqual(savedBytes) && File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest) &&
                !File.Exists(Path.Combine(project.RootPath, LockName)), "Compile rechecks without adopting, saving or holding a long guard.");
            await Throws<InvalidDataException>(() => NativeId65FreshTexturedResearchCandidatePlan.CaptureAsync(Cases.Missing, "missing.cue", bridge.Disc), "selected");
            if (name == "selected")
            {
                var againSaved = NativeFreshSavedLevelDocumentReadContext.Capture(NativeFreshLevelDocumentStore.Capture(project));
                Check(!ReferenceEquals(againSaved.Document, saved.Document), "Actual reparse supplies genuinely distinct equal intent.");
                await Throws<InvalidDataException>(() => Invoke(() => bridge.RequireAncestry(againSaved, bridge.BaseCompilation,
                    bridge.Material0, bridge.Material1, bridge.Texture, bridge.Staging, bridge.Body, bridge.Disc)));
                var again = await NativeFreshSavedMaterialCompilation.CaptureAsync(path, againSaved);
                Check(again.Disc.PlanSha256 == bridge.Disc.PlanSha256 && !ReferenceEquals(again.Disc, bridge.Disc),
                    "Normal reissuance is valid for its own actual bridge, with deterministic bytes.");
                await Throws<InvalidDataException>(() => Invoke(() => bridge.RequireAncestry(saved, again.BaseCompilation,
                    bridge.Material0, bridge.Material1, bridge.Texture, bridge.Staging, bridge.Body, bridge.Disc)));
                await Throws<InvalidDataException>(() => Invoke(() => bridge.RequireAncestry(saved, bridge.BaseCompilation,
                    again.Material0, bridge.Material1, bridge.Texture, bridge.Staging, bridge.Body, bridge.Disc)));
                await Throws<InvalidDataException>(() => Invoke(() => bridge.RequireAncestry(saved, bridge.BaseCompilation,
                    bridge.Material0, again.Material1, bridge.Texture, bridge.Staging, bridge.Body, bridge.Disc)));
                await Throws<InvalidDataException>(() => Invoke(() => bridge.RequireAncestry(saved, bridge.BaseCompilation,
                    bridge.Material0, bridge.Material1, again.Texture, bridge.Staging, bridge.Body, bridge.Disc)));
                await Throws<InvalidDataException>(() => Invoke(() => bridge.RequireAncestry(saved, bridge.BaseCompilation,
                    bridge.Material0, bridge.Material1, bridge.Texture, again.Staging, bridge.Body, bridge.Disc)));
                await Throws<InvalidDataException>(() => Invoke(() => bridge.RequireAncestry(saved, bridge.BaseCompilation,
                    bridge.Material0, bridge.Material1, bridge.Texture, bridge.Staging, again.Body, bridge.Disc)));
                await Throws<InvalidDataException>(() => Invoke(() => bridge.RequireAncestry(saved, bridge.BaseCompilation,
                    bridge.Material0, bridge.Material1, bridge.Texture, bridge.Staging, bridge.Body, again.Disc)));
            }
            Console.WriteLine("SAVED_MATERIAL_SOURCE case=" + name + " image=" + row.GetProperty("output_sha256").GetString());
            void Match(string actual, string field) => Check(actual == row.GetProperty(field).GetString(), name + " independent " + field);
        }
        foreach (var (entry, id) in new[] { (16, 1), (40, 70), (70, 12) })
        {
            var (_, saved) = Save("ineligible", Document(entry, id));
            byte[] before = File.ReadAllBytes(saved.Snapshot.Path);
            await Throws<InvalidDataException>(() => NativeFreshSavedMaterialCompilation.CaptureAsync(path, saved));
            Check(File.ReadAllBytes(saved.Snapshot.Path).SequenceEqual(before), "Ineligible source does not corrupt saved descriptor intent.");
        }
        // Actual maximum-cell Base is known to encode a different data-prefix
        // length; it is valid neutral authoring, not the 8C8 texture grammar.
        var floor = NativeId65FreshDocumentIntent.Create(63*4096, 63*4096, 63*4096, 16, 16,
            63*4096+1, 63*4096+1, 64, new ColorRgba(80,128,112), new ColorRgba(128,96,64), 19);
        var (_, unsupported) = Save("different-layout", Document().WithContent(floor, []));
        await Throws<InvalidDataException>(() => NativeFreshSavedMaterialCompilation.CaptureAsync(path, unsupported), "two-direct-triangle");
        await BoundaryCases.Run(path);
    }

    private static Task Invoke(Action action) { action(); return Task.CompletedTask; }
}

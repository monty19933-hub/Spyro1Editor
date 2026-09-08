using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using static Inspection;

internal static class SourceCases
{
    private const long ImageSize = 661547040;
    private const string Original = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const string CueHash = "05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766";
    internal static async Task Run(string bin, string cue)
    {
        var owners = new List<(FileStream Stream, long Size, string Hash)>(); var errors = new List<Exception>();
        try
        {
            foreach (var item in new[] { (Path: bin, Size: ImageSize, Hash: Original), (Path: cue, Size: 88L, Hash: CueHash) })
            {
                var stream = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                owners.Add((stream, item.Size, item.Hash)); Require(stream, item.Size, item.Hash);
            }
            await Core(bin, cue);
        }
        catch (Exception e) { errors.Add(e); }
        finally
        {
            foreach (var owner in owners)
            {
                try { Require(owner.Stream, owner.Size, owner.Hash); Check(true, "Independent complete original owner final hash/length/true EOF."); }
                catch (Exception e) { errors.Add(e); }
                try { owner.Stream.Dispose(); } catch (Exception e) { errors.Add(e); }
            }
        }
        Finish(errors);
    }

    private static async Task Core(string bin, string cue)
    {
        var aIntent = NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 1024, 1024, 8960, 8960, 64,
            new(80,128,112), new(128,96,64), 19);
        var a = await NativeId65FreshDocumentCompilation.CaptureAsync(bin, aIntent);
        await LineageCases.Source(bin, a);
        await CandidateSourceCases.Run(bin, cue, a);
        var editedIntent = NativeId65FreshDocumentIntent.Create(8192, 8192, 8704, 2048, 2048, 9216, 9216, 96,
            new(72,132,120), new(44,66,100), 2);
        var editedCompilation = await NativeId65FreshDocumentCompilation.CaptureAsync(bin, editedIntent);
        var starter = await NativeId65FreshDiscResearchCandidatePlan.CaptureAsync(bin, cue, a.DiscPlan);
        var edited = await NativeId65FreshEditedDocumentResearchPlan.CaptureAsync(bin, cue, editedCompilation);
        var generic = await NativeId65FreshDocumentResearchPlan.CaptureAsync(bin, cue, editedCompilation);
        Check(generic.ExpectedImageSha256 == "ecc8f61cc16bf86a2f5117b327c49aede18f5c1e6a602c0e67f2ba58565aed16" &&
            generic.ExpectedChangedRawSectors == 1253, "Unchanged actual generic measured edited golden.");
        var wizard = await Wizard(bin, cue, a);
        var separated = await SeparatedWizard(bin, cue);
        string folder = Directory.CreateTempSubdirectory("textured-binding-store-baseline-").FullName;
        var layout = new EditorProjectLayout(folder, "binding-baseline");
        byte[] manifest = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"projectId\":\"binding-baseline\"}");
        string? documentPath = null; byte[]? documentBytes = null;
        var errors = new List<Exception>();
        try
        {
            File.WriteAllBytes(layout.ManifestPath, manifest);
            var snapshot = NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(layout), editedIntent);
            documentPath = snapshot.Path; documentBytes = File.ReadAllBytes(documentPath);
            var context = NativeId65FreshSavedDocumentExportContext.Capture(snapshot, generic);
            await BindingCases.Source(starter, edited, context, wizard, separated);
            MeasureCases.Source(bin, editedCompilation.DiscPlan, generic.ExpectedImageSha256);
            ExtractionCases.Source(bin, editedCompilation.DiscPlan, generic.ExpectedImageSha256);
            Check(ReferenceEquals(context.Snapshot, snapshot) && ReferenceEquals(context.Plan, generic), "Test keeps actual saved and source-issued objects.");
        }
        catch (Exception e) { errors.Add(e); }
        finally
        {
            // Cleanup is limited to this new tiny fixture's expected names,
            // nonlinked regular files and exact bytes. It does not bind inode
            // identity against an equal-byte replacement or hostile races.
            try
            {
                string[] expected = documentPath == null ? [layout.ManifestPath] : [layout.ManifestPath, documentPath];
                if (!Directory.GetFileSystemEntries(folder).Order().SequenceEqual(expected.Order()) ||
                    new DirectoryInfo(folder).LinkTarget != null || expected.Any(p => new FileInfo(p).LinkTarget != null || !File.Exists(p)) ||
                    !File.ReadAllBytes(layout.ManifestPath).SequenceEqual(manifest) ||
                    documentPath != null && !File.ReadAllBytes(documentPath).SequenceEqual(documentBytes!))
                    throw new IOException("Preserve changed tiny Store fixture: " + folder);
                foreach (string path in expected) File.Delete(path);
                Directory.Delete(folder);
                Check(true, "Only exact self-owned tiny Store fixture removed; no disc output existed.");
            }
            catch (Exception e) { errors.Add(e); }
        }
        Finish(errors);
    }

    private static async Task<NativeId65FreshWizardResearchCandidatePlan> Wizard(string bin, string cue,
        NativeId65FreshDocumentCompilation compilation)
    {
        var family = await NativeWizardFamilyAssetPlan.CaptureAsync(bin);
        var resources = await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(bin, compilation, family);
        var scene = await NativeId65FreshWizardResearchScenePlan.CaptureAsync(bin, compilation, family, resources,
            new(8704,8960,8704,9216,8960));
        var fallback = await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(bin, resources);
        var host = await NativeId65FreshWizardResearchHostPlan.CaptureAsync(bin, scene, fallback);
        var body = await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(bin, host);
        var exe = await NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(bin, body);
        var disc = await NativeId65FreshWizardResearchDiscPlan.CaptureAsync(bin, exe);
        return await NativeId65FreshWizardResearchCandidatePlan.CaptureAsync(bin, cue, disc);
    }
    private static async Task<NativeId65FreshWizardResearchCandidatePlan> SeparatedWizard(string bin, string cue)
    {
        var floor = NativeId65FreshDocumentIntent.Create(8448,8448,8704,2048,2048,8960,8960,64,new(80,128,112),new(128,96,64),19);
        var document = NativeFreshLevelDocumentIntent.Create(floor,
            [NativeFreshWizardRouteIntent.Create("11111111111111111111111111111111",9984,9984,8704,9472,9984)]);
        var compilation = await NativeId65FreshDocumentCompilation.CaptureAsync(bin, floor);
        var family = await NativeWizardFamilyAssetPlan.CaptureAsync(bin);
        var resource = await NativeId65FreshWizardResearchResourcePlan.CaptureAuthoredAsync(bin, compilation, family);
        var scene = await NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync(bin, document, compilation, family, resource);
        var clearance = await NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync(bin, document, scene);
        var fallback = await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(bin, resource);
        var host = await NativeId65FreshWizardResearchHostPlan.CaptureAsync(bin, scene, fallback);
        var body = await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(bin, host);
        var exe = await NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(bin, body);
        var disc = await NativeId65FreshWizardResearchDiscPlan.CaptureAsync(bin, exe);
        var plan = await NativeId65FreshWizardResearchCandidatePlan.CaptureSeparatedAsync(bin, cue, disc, clearance);
        Check(plan.ExpectedImageSha256 == "e57fedebb59ffc9020de00562baccbc5a6e09bffd5f24f665d34d5f03163cbdb" &&
            plan.ExpectedChangedRawSectors == 1290, "Actual authored/clearance separated recipe, no physical publication.");
        return plan;
    }
    private static void Require(FileStream stream, long size, string digest)
    {
        if (stream.Length != size) throw new InvalidDataException("Held source initial length changed.");
        stream.Position = 0;
        if (Convert.ToHexStringLower(SHA256.HashData(stream)) != digest || stream.Position != size || stream.Length != size ||
            stream.ReadByte() != -1 || stream.Position != size || stream.Length != size)
            throw new InvalidDataException("Held source full SHA, length, position or true EOF changed.");
    }
}

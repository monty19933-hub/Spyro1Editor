using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

internal static class NativeMutableWizardPropertiesSourceCases
{
    public static void Run(string sourceImagePath)
    {
        string directory = Path.Combine(Path.GetTempPath(), "spyro-wizard-properties-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "spyro-level-catalog.json"), JsonSerializer.Serialize(new
            {
                levels = new[] { new { key = "wizardpeak", displayName = "Wizard Peak", levelId = 33, sourceWadEntry = 40,
                    sourceTableWadOffset = "0x313F978", sourceTableRelativeOffset = "0x1D4978", sourceRecordCount = 165 } }
            }));
            string editsPath = Path.Combine(directory, "native-edits.json");
            File.WriteAllText(editsPath, JsonSerializer.Serialize(new
            {
                edits = new[] { new { label = "Exact native Wizard T6-to-T24", trueIndex = 24, editKind = "update",
                    crossLevelTemplate = new { id = GreenWizardRuntimeBundleCompatibility.TemplateId,
                        family = "enemyTransform", sourceLevelKey = "wizardpeak", sourceLevelName = "Wizard Peak",
                        sourceTrueIndex = 6, requiredExporterFeature = CrossLevelSwapCatalogBuilder.CandidateExporterFeature } } }
            }));
            LevelDefinition level = LevelCatalog.Load(directory).FindByKey("wizardpeak")!;
            MobySourcePatchPlan sourcePlan = MobySourcePatchExporter.BuildPlan(sourceImagePath, "",
                Path.Combine(directory, "never-written.bin"), Path.Combine(directory, "never-written.cue"), level, editsPath);
            Require(sourcePlan.SkippedEdits.Count == 0 && sourcePlan.Patches.Count == 1 &&
                    sourcePlan.Patches.Single().Kind == "cross-level-existing-slot-candidate",
                "The real normal planner no longer provides the exact source-bound Wizard T24 row.");
            byte[] plannedRow = Convert.FromHexString(sourcePlan.Patches.Single().AfterHexPreview.Replace(" ", ""));
            byte[] sourceScene = WizardPeakGreenWizardNativeSwapExporter.ReadOriginalNativeScene(sourceImagePath);
            WizardPeakGreenWizardNativePropertiesCompilation result =
                WizardPeakGreenWizardNativeSwapExporter.CompileNativeProperties(sourceScene, plannedRow);
            Require(result.LinkPlan.Bindings.Count == 1 && result.LinkPlan.Bindings.Single().FieldOffset == 0x69B8 &&
                    result.LinkPlan.Bindings.Single().Before == 0xCAC0 && result.LinkPlan.Bindings.Single().After == 0xCAC0 &&
                    result.LinkPlan.FixupCountAfter == 0xBF && result.LinkPlan.Writes.Count == 2,
                "The integrated mutable native adapter changed owner identity or fixup count.");
            byte[] properties = Convert.FromHexString(result.LinkPlan.Writes.Single(write => write.Offset == 0xCAC0).AfterHex);
            byte[] fixups = Convert.FromHexString(result.LinkPlan.Writes.Single(write => write.Offset == 0xFC3C).AfterHex);
            byte[] row = Convert.FromHexString(result.InstalledRecordHex);
            byte[] ownedExtent = sourceScene.AsSpan(0xCAC0, 0x74).ToArray(); properties.CopyTo(ownedExtent, 0);
            Require(Hash(properties) == "52f747a5aa5c2c4beff2813aa8722c876d881dff77a564b669ffb24d8fef732f" &&
                    Hash(ownedExtent) == "0aef88ee09413e5ef08f685049698cca6b3e07846d1bafaf34cbf8627936ad73" &&
                    Hash(fixups) == "24a9a1e0d22e8b10b480ad1f71fd3b92c234533e43daf0b1d4d238430ea87e5f" &&
                    Hash(row) == "4316b75d1f06c73b8a49b545fcfe838dcf91e1b9a38a8cd16936c4371a6a2177",
                "The mutable adapter changed a frozen properties, extent, fixup or native-row golden.");
            Require(result.RoutePoints.SequenceEqual(new[]
                { new WizardPeakGreenWizardRoutePoint(39670, 64655, 27935), new WizardPeakGreenWizardRoutePoint(33147, 61511, 27935) }),
                "The integrated native adapter changed either translated route point.");
            byte[] linked = sourceScene.ToArray();
            foreach (NativePropertiesSceneWrite write in result.LinkPlan.Writes)
            {
                byte[] before = Convert.FromHexString(write.BeforeHex);
                Require(linked.AsSpan(write.Offset, before.Length).SequenceEqual(before), "Native adapter write preimage mismatch.");
                Convert.FromHexString(write.AfterHex).CopyTo(linked, write.Offset);
            }
            row.CopyTo(linked, 0x69B8);
            for (int offset = 0; offset < linked.Length; offset++)
                if (offset is not (>= 0x69B8 and < 0x6A10) && offset is not (>= 0xCAC0 and < 0xCB10) &&
                    offset is not (>= 0xFC3C and < 0xFF3C))
                    Require(linked[offset] == sourceScene[offset], $"Native linkage changed protected scene byte0x{offset:X}.");
            foreach (NativePropertiesSceneWrite write in result.LinkPlan.Writes)
                Convert.FromHexString(write.BeforeHex).CopyTo(linked, write.Offset);
            sourceScene.AsSpan(0x69B8, 0x58).CopyTo(linked.AsSpan(0x69B8));
            Require(linked.AsSpan().SequenceEqual(sourceScene), "Integrated native linkage inverse changed its source scene.");

            NativeMutablePropertiesInstance instance = result.Request.Instances.Single();
            Reject(() => NativeScenePropertiesLinker.CompileMutable(result.Request with
                { Instances = [instance with { OwnershipAuthority = null }] }), "authority");
            byte[] alteredScene = sourceScene.ToArray(); alteredScene[^1] ^= 1;
            Reject(() => WizardPeakGreenWizardNativeSwapExporter.CompileNativeProperties(alteredScene, plannedRow), "original");
            Reject(() => NativeScenePropertiesLinker.CompileMutable(result.Request with
                { SourceScene = alteredScene, SourceSha256 = Hash(alteredScene) }), "authority");
            byte[] alteredPayload = instance.Payload.ToArray(); alteredPayload[^1] ^= 1;
            Reject(() => NativeScenePropertiesLinker.CompileMutable(result.Request with
                { Instances = [instance with { Payload = alteredPayload, PayloadSha256 = Hash(alteredPayload) }] }), "authority");
            byte[] movedRow = plannedRow.ToArray(); movedRow[0x0C] ^= 1;
            Reject(() => WizardPeakGreenWizardNativeSwapExporter.CompileNativeProperties(sourceScene, movedRow), "placement");
            Require(sourceScene.AsSpan().SequenceEqual(WizardPeakGreenWizardNativeSwapExporter.ReadOriginalNativeScene(sourceImagePath)) &&
                    !Directory.EnumerateFiles(directory, "*.bin").Any() && !Directory.EnumerateFiles(directory, "*.cue").Any(),
                "The source-backed mutable tests changed native source bytes or published a game image.");
            Console.WriteLine("NATIVE_MUTABLE_WIZARD_PROPERTIES_SOURCE_SMOKE_OK actual normal row plan + integrated adapter; four frozen native goldens; protected complement + inverse; no BIN/CUE");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void Reject(Action operation, string reason)
    {
        try { operation(); }
        catch (InvalidDataException error) when (error.Message.Contains(reason, StringComparison.OrdinalIgnoreCase)) { return; }
        throw new InvalidOperationException($"Expected source-bound native adapter rejection containing '{reason}'.");
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Require(bool value, string text) { if (!value) throw new InvalidOperationException(text); }
}
